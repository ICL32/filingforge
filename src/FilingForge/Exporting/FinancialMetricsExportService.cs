using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FilingForge;

/// <summary>Renders metric rows for one or more tickers as CSV, per-metric CSV files, XLSX or a trace.</summary>
public class FinancialMetricsExportService(FilingForgeClient client)
{
    private record ExportColumn(
        string Header,
        FinancialMetricGroup? Group,
        bool IsNumeric,
        Func<FinancialMetricPeriodRow, string> Text,
        Func<FinancialMetricPeriodRow, double?>? Number = null
    );

    private record ExportDataset(
        IReadOnlyList<string> Tickers,
        IReadOnlyList<FinancialMetricPeriodRow> Rows,
        IReadOnlyList<FinancialMetricAuditRow> AuditRows,
        IReadOnlyList<FinancialMetricRawInputsTrace> RawInputs
    );

    /// <summary>One wide CSV: a header row, then every period row of every ticker.</summary>
    public async Task<string> BuildCsvAsync(IReadOnlyList<string> tickers, CancellationToken ct = default)
    {
        var columns = Columns();
        var dataset = await BuildDatasetAsync(tickers, ct);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", columns.Select(c => Csv(c.Header))));
        foreach (var row in dataset.Rows.OrderBy(r => r.Ticker, StringComparer.Ordinal).ThenBy(r => r.PeriodEnd, StringComparer.Ordinal))
        {
            var cells = columns.Select(c => Csv(c.IsNumeric ? Num(c.Number?.Invoke(row)) : c.Text(row)));
            csv.AppendLine(string.Join(",", cells));
        }

        return csv.ToString();
    }

    // Upper-cased, de-duplicated, blank-free and sorted.
    private static List<string> NormalizeSymbols(IEnumerable<string> tickers)
    {
        var unique = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticker in tickers)
        {
            var sym = ticker.Trim().ToUpperInvariant();
            if (sym.Length > 0)
                unique.Add(sym);
        }

        return [.. unique];
    }

    public async Task<byte[]> BuildMetricCsvZipAsync(IReadOnlyList<string> tickers, CancellationToken ct = default)
    {
        var files = await BuildMetricCsvFilesAsync(tickers, ct);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    public async Task<IReadOnlyDictionary<string, string>> BuildMetricCsvFilesAsync(
        IReadOnlyList<string> tickers,
        CancellationToken ct = default)
    {
        var dataset = await BuildDatasetAsync(tickers, ct);
        return BuildMetricCsvFiles(dataset);
    }

    public async Task<byte[]> BuildXlsxAsync(IReadOnlyList<string> tickers, CancellationToken ct = default)
    {
        var dataset = await BuildDatasetAsync(tickers, ct);
        var columns = XlsxDataColumns();
        var rows = dataset.Rows.OrderBy(r => r.Ticker).ThenBy(r => r.PeriodEnd).ToList();
        var dataRows = new List<IReadOnlyList<XlsxCell>>(rows.Count + 1)
        {
            columns.Select(c => XlsxCell.TextCell(c.Header, HeaderStyle(c.Group))).ToList()
        };

        foreach (var row in rows)
        {
            dataRows.Add(columns.Select(c =>
                c.IsNumeric
                    ? XlsxCell.NumberCell(c.Number?.Invoke(row))
                    : XlsxCell.TextCell(c.Text(row))
            ).ToList());
        }

        var definitionRows = new List<IReadOnlyList<XlsxCell>>
        {
            new[]
            {
                "metricId", "metricName", "group", "kind", "definition",
                "usGaapDirectConcepts", "ifrsDirectConcepts", "fallbackOrder", "blankBehavior"
            }.Select(h => XlsxCell.TextCell(h, SimpleXlsxWriter.DarkHeaderStyle)).ToList()
        };

        foreach (var metric in FinancialMetricsCatalog.AllMetrics)
        {
            definitionRows.Add([
                XlsxCell.TextCell(metric.Id),
                XlsxCell.TextCell(metric.DisplayName),
                XlsxCell.TextCell(metric.Group.ToString()),
                XlsxCell.TextCell(metric.Kind.ToString()),
                XlsxCell.TextCell(metric.Definition),
                XlsxCell.TextCell(string.Join("|", FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.UsGaap).Select(c => c.SourceName))),
                XlsxCell.TextCell(string.Join("|", FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.IfrsFull).Select(c => c.SourceName))),
                XlsxCell.TextCell(string.Join(" -> ", metric.FallbackOrder)),
                XlsxCell.TextCell(metric.BlankBehavior)
            ]);
        }

        var sheets = new List<XlsxSheet> { new("Data", dataRows) };
        sheets.AddRange(BuildAuditSheets(dataset.AuditRows));
        sheets.Add(new XlsxSheet("Definitions", definitionRows));

        return SimpleXlsxWriter.CreateWorkbook(sheets);
    }

    public async Task<FinancialMetricsTraceResponse> BuildTraceAsync(
        IReadOnlyList<string> tickers,
        bool includeRawInputs = false,
        CancellationToken ct = default)
    {
        var dataset = await BuildTraceDatasetAsync(tickers, ct, includeRawInputs);
        return new FinancialMetricsTraceResponse(
            dataset.Tickers,
            DefinitionTraces(),
            dataset.Rows.OrderBy(r => r.Ticker).ThenBy(r => r.PeriodEnd).ToList(),
            dataset.AuditRows
                .OrderBy(a => a.Ticker)
                .ThenBy(a => a.PeriodEnd)
                .ThenBy(a => a.MetricId)
                .ThenBy(a => a.ValueType)
                .ToList(),
            dataset.RawInputs.OrderBy(r => r.Ticker).ToList()
        );
    }

    private Task<ExportDataset> BuildTraceDatasetAsync(
        IReadOnlyList<string> tickers,
        CancellationToken ct,
        bool includeRawInputs = false) =>
        BuildDatasetAsync(tickers, ct, includeRawInputs);

    private async Task<ExportDataset> BuildDatasetAsync(
        IReadOnlyList<string> tickers,
        CancellationToken ct,
        bool includeRawInputs = false)
    {
        var symbols = NormalizeSymbols(tickers);
        var rows = new ConcurrentBag<FinancialMetricPeriodRow>();
        var auditRows = new ConcurrentBag<FinancialMetricAuditRow>();
        var rawInputs = new ConcurrentBag<FinancialMetricRawInputsTrace>();

        await Parallel.ForEachAsync(
            symbols,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (ticker, token) =>
            {
                try
                {
                    var inputs = await client.GetMetricInputsAsync(ticker, token);
                    if (includeRawInputs)
                        rawInputs.Add(new FinancialMetricRawInputsTrace(ticker, inputs.Profile, inputs.Facts, inputs.RevenuePoints));

                    var built = FinancialMetricsBuilder.Build(ticker, inputs);
                    if (built.Rows.Count == 0)
                    {
                        rows.Add(FinancialMetricsBuilder.ErrorRow(ticker, "No SEC financial metric rows found in SEC company facts."));
                        return;
                    }

                    foreach (var row in built.Rows)
                        rows.Add(row);
                    foreach (var audit in built.AuditRows)
                        auditRows.Add(audit);
                }
                catch (Exception ex) when (ex is KeyNotFoundException or HttpRequestException or InvalidOperationException or JsonException or IOException)
                {
                    rows.Add(FinancialMetricsBuilder.ErrorRow(ticker, ex.Message));
                }
            }
        );

        return new ExportDataset(symbols, [.. rows], [.. auditRows], [.. rawInputs]);
    }

    private static IReadOnlyList<FinancialMetricDefinitionTrace> DefinitionTraces() =>
        FinancialMetricsCatalog.AllMetrics
            .Select(metric => new FinancialMetricDefinitionTrace(
                metric.Id,
                metric.DisplayName,
                metric.Group.ToString(),
                metric.Kind.ToString(),
                metric.Definition,
                FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.UsGaap).Select(c => c.SourceName).ToList(),
                FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.IfrsFull).Select(c => c.SourceName).ToList(),
                metric.FallbackOrder,
                metric.BlankBehavior
            ))
            .ToList();

    private static IReadOnlyDictionary<string, string> BuildMetricCsvFiles(ExportDataset dataset)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var metric in FinancialMetricsCatalog.FlowMetrics)
        {
            files[$"metrics/quarterly/{metric.Id}.csv"] = BuildMetricValueCsv(
                dataset.Rows,
                row => row.Quarterly[metric.Id]);

            files[$"metrics/annual/{metric.Id}.csv"] = BuildMetricValueCsv(
                dataset.Rows,
                row => row.Annual[metric.Id]);

            files[$"metrics/ttm/{metric.Id}.csv"] = BuildMetricValueCsv(
                dataset.Rows,
                row => row.Ttm[metric.Id]);
        }

        foreach (var metric in FinancialMetricsCatalog.PointMetrics)
        {
            files[$"metrics/point/{metric.Id}.csv"] = BuildMetricValueCsv(
                dataset.Rows,
                row => row.Point[metric.Id]);
        }

        return files;
    }

    private static string BuildMetricValueCsv(
        IReadOnlyList<FinancialMetricPeriodRow> rows,
        Func<FinancialMetricPeriodRow, FinancialMetricValue> valueFor)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ticker,periodEnd,provider,taxonomy,value,formula,sourceConcepts,filed,accession,sourceType,sourceDocument,sourceLabel,validationStatus,validationMessages");
        foreach (var row in rows.OrderBy(r => r.Ticker).ThenBy(r => r.PeriodEnd))
        {
            if (row.ValidationStatus == "error")
            {
                AppendMetricCsvRow(sb, row, FinancialMetricValue.Blank());
                continue;
            }

            AppendMetricCsvRow(sb, row, valueFor(row));
        }
        return sb.ToString();
    }

    private static void AppendMetricCsvRow(StringBuilder sb, FinancialMetricPeriodRow row, FinancialMetricValue value)
    {
        sb.AppendLine(string.Join(",", [
            Csv(row.Ticker),
            Csv(row.PeriodEnd),
            Csv(row.Provider),
            Csv(row.Taxonomy),
            Csv(Num(value.Value)),
            Csv(value.Formula),
            Csv(value.SourceConcepts),
            Csv(value.Filed ?? ""),
            Csv(value.Accession ?? ""),
            Csv(value.SourceType),
            Csv(value.SourceDocument ?? ""),
            Csv(value.SourceLabel ?? ""),
            Csv(row.ValidationStatus),
            Csv(string.Join("|", row.ValidationMessages))
        ]));
    }

    private static IReadOnlyList<ExportColumn> Columns()
    {
        var columns = new List<ExportColumn>
        {
            TextColumn("ticker", null, r => r.Ticker),
            TextColumn("periodEnd", null, r => r.PeriodEnd),
            TextColumn("fiscalYear", null, r => r.FiscalYear?.ToString(CultureInfo.InvariantCulture) ?? ""),
            TextColumn("fiscalQuarter", null, r => r.FiscalQuarter?.ToString(CultureInfo.InvariantCulture) ?? ""),
            TextColumn("quarterLabel", null, r => r.QuarterLabel),
            TextColumn("provider", null, r => r.Provider),
            TextColumn("taxonomy", null, r => r.Taxonomy),
            TextColumn("validationStatus", null, r => r.ValidationStatus),
            TextColumn("validationMessages", null, r => string.Join("|", r.ValidationMessages)),
            TextColumn("filed", null, r => r.Filed)
        };

        foreach (var metric in FinancialMetricsCatalog.FlowMetrics)
        {
            var name = Pascal(metric.Id);
            columns.Add(NumberColumn($"quarterly{name}", metric.Group, r => r.Quarterly[metric.Id].Value));
            columns.Add(TextColumn($"quarterly{name}Formula", metric.Group, r => r.Quarterly[metric.Id].Formula));
            columns.Add(TextColumn($"quarterly{name}SourceConcepts", metric.Group, r => r.Quarterly[metric.Id].SourceConcepts));
            columns.Add(NumberColumn($"annual{name}", metric.Group, r => r.Annual[metric.Id].Value));
            columns.Add(TextColumn($"annual{name}Formula", metric.Group, r => r.Annual[metric.Id].Formula));
            columns.Add(TextColumn($"annual{name}SourceConcepts", metric.Group, r => r.Annual[metric.Id].SourceConcepts));
            columns.Add(NumberColumn($"ttm{name}", metric.Group, r => r.Ttm[metric.Id].Value));
            columns.Add(TextColumn($"ttm{name}Formula", metric.Group, r => r.Ttm[metric.Id].Formula));
            columns.Add(TextColumn($"ttm{name}SourceConcepts", metric.Group, r => r.Ttm[metric.Id].SourceConcepts));
        }

        foreach (var metric in FinancialMetricsCatalog.PointMetrics)
        {
            columns.Add(NumberColumn(metric.Id, metric.Group, r => r.Point[metric.Id].Value));
            columns.Add(TextColumn($"{metric.Id}Formula", metric.Group, r => r.Point[metric.Id].Formula));
            columns.Add(TextColumn($"{metric.Id}SourceConcepts", metric.Group, r => r.Point[metric.Id].SourceConcepts));
        }

        return columns;
    }

    private static IReadOnlyList<ExportColumn> XlsxDataColumns()
    {
        var columns = new List<ExportColumn>
        {
            TextColumn("ticker", null, r => r.Ticker),
            TextColumn("periodEnd", null, r => r.PeriodEnd),
            TextColumn("fiscalYear", null, r => r.FiscalYear?.ToString(CultureInfo.InvariantCulture) ?? ""),
            TextColumn("fiscalQuarter", null, r => r.FiscalQuarter?.ToString(CultureInfo.InvariantCulture) ?? ""),
            TextColumn("quarterLabel", null, r => r.QuarterLabel),
            TextColumn("provider", null, r => r.Provider),
            TextColumn("taxonomy", null, r => r.Taxonomy),
            TextColumn("validationStatus", null, r => r.ValidationStatus),
            TextColumn("validationMessages", null, r => string.Join("|", r.ValidationMessages)),
            TextColumn("filed", null, r => r.Filed)
        };

        foreach (var metric in FinancialMetricsCatalog.FlowMetrics)
        {
            var name = Pascal(metric.Id);
            columns.Add(NumberColumn($"quarterly{name}", metric.Group, r => r.Quarterly[metric.Id].Value));
            columns.Add(NumberColumn($"annual{name}", metric.Group, r => r.Annual[metric.Id].Value));
            columns.Add(NumberColumn($"ttm{name}", metric.Group, r => r.Ttm[metric.Id].Value));
        }

        foreach (var metric in FinancialMetricsCatalog.PointMetrics)
            columns.Add(NumberColumn(metric.Id, metric.Group, r => r.Point[metric.Id].Value));

        return columns;
    }

    private static IEnumerable<XlsxSheet> BuildAuditSheets(IReadOnlyList<FinancialMetricAuditRow> auditRows)
    {
        const int maxDataRowsPerSheet = 900_000;
        var header = new[]
        {
            "ticker", "periodEnd", "metricId", "metricName", "valueType", "value",
            "provider", "taxonomy", "formula", "sourceConcepts", "filed", "accession", "derivation", "sourceType", "sourceDocument", "sourceLabel", "validationStatus", "validationMessages"
        }.Select(h => XlsxCell.TextCell(h, SimpleXlsxWriter.DarkHeaderStyle)).ToList();

        var sorted = auditRows
            .OrderBy(a => a.Ticker)
            .ThenBy(a => a.PeriodEnd)
            .ThenBy(a => a.MetricId)
            .ThenBy(a => a.ValueType)
            .ToList();

        if (sorted.Count == 0)
            return [new XlsxSheet("Metric Audit", [header])];

        var sheets = new List<XlsxSheet>();
        for (var offset = 0; offset < sorted.Count; offset += maxDataRowsPerSheet)
        {
            var chunk = sorted.Skip(offset).Take(maxDataRowsPerSheet);
            var rows = new List<IReadOnlyList<XlsxCell>>(maxDataRowsPerSheet + 1) { header };
            foreach (var audit in chunk)
            {
                rows.Add([
                    XlsxCell.TextCell(audit.Ticker),
                    XlsxCell.TextCell(audit.PeriodEnd),
                    XlsxCell.TextCell(audit.MetricId),
                    XlsxCell.TextCell(audit.MetricName),
                    XlsxCell.TextCell(audit.ValueType),
                    XlsxCell.NumberCell(audit.Value),
                    XlsxCell.TextCell(audit.Provider),
                    XlsxCell.TextCell(audit.Taxonomy),
                    XlsxCell.TextCell(audit.Formula),
                    XlsxCell.TextCell(audit.SourceConcepts),
                    XlsxCell.TextCell(audit.Filed),
                    XlsxCell.TextCell(audit.Accession),
                    XlsxCell.TextCell(audit.Derivation),
                    XlsxCell.TextCell(audit.SourceType),
                    XlsxCell.TextCell(audit.SourceDocument ?? ""),
                    XlsxCell.TextCell(audit.SourceLabel ?? ""),
                    XlsxCell.TextCell(audit.ValidationStatus),
                    XlsxCell.TextCell(audit.ValidationMessages)
                ]);
            }

            var suffix = sheets.Count == 0 ? "" : $" {sheets.Count + 1}";
            sheets.Add(new XlsxSheet($"Metric Audit{suffix}", rows));
        }

        return sheets;
    }

    private static ExportColumn TextColumn(string header, FinancialMetricGroup? group, Func<FinancialMetricPeriodRow, string> value) =>
        new(header, group, false, value);

    private static ExportColumn NumberColumn(string header, FinancialMetricGroup? group, Func<FinancialMetricPeriodRow, double?> value) =>
        new(header, group, true, _ => "", value);

    private static int HeaderStyle(FinancialMetricGroup? group) =>
        group switch
        {
            FinancialMetricGroup.IncomeStatement => SimpleXlsxWriter.IncomeHeaderStyle,
            FinancialMetricGroup.CashFlow => SimpleXlsxWriter.CashFlowHeaderStyle,
            FinancialMetricGroup.BalanceSheet => SimpleXlsxWriter.BalanceSheetHeaderStyle,
            FinancialMetricGroup.Shares => SimpleXlsxWriter.SharesHeaderStyle,
            _ => SimpleXlsxWriter.BaseHeaderStyle
        };

    private static string Pascal(string id) =>
        string.IsNullOrWhiteSpace(id) ? id : char.ToUpperInvariant(id[0]) + id[1..];

    private static string Num(double? value) =>
        value.HasValue ? value.Value.ToString("G17", CultureInfo.InvariantCulture) : "";

    private static string Csv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r')) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
