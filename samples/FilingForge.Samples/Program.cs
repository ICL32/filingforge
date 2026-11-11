using System.Globalization;
using System.Text;
using FilingForge;

// FilingForge sample commands.
//
//   dotnet run --project samples/FilingForge.Samples -- statements AAPL
//   dotnet run --project samples/FilingForge.Samples -- metric AAPL revenue
//   dotnet run --project samples/FilingForge.Samples -- trace AAPL revenue
//   dotnet run --project samples/FilingForge.Samples -- export AAPL MSFT
//   dotnet run --project samples/FilingForge.Samples -- metrics-doc docs/metrics.md
//
// SEC asks automated clients to identify themselves. Set FILINGFORGE_USER_AGENT to something
// like "YourApp/1.0 you@example.com" before running the commands that reach SEC.

if (args.Length == 0)
{
    Console.WriteLine("usage: statements <TICKER> | metric <TICKER> <metricId> | trace <TICKER> <metricId> | export <TICKER...> | metrics-doc <path>");
    return 1;
}

if (args[0] == "metrics-doc")
{
    File.WriteAllText(args[1], MetricsDoc.Build(), new UTF8Encoding(false));
    Console.WriteLine($"wrote {args[1]}");
    return 0;
}

var userAgent = Environment.GetEnvironmentVariable("FILINGFORGE_USER_AGENT");
if (string.IsNullOrWhiteSpace(userAgent))
{
    Console.Error.WriteLine("Set FILINGFORGE_USER_AGENT, e.g. \"YourApp/1.0 you@example.com\".");
    return 1;
}

using var sec = new SecEdgarHttpSource(userAgent);
var client = new FilingForgeClient(sec);

switch (args[0])
{
    case "statements":
    {
        var statements = await client.GetStatementsAsync(args[1]);
        Console.WriteLine($"{statements.Ticker}: last 8 quarters (USD millions)");
        Console.WriteLine($"{"Quarter",-9}{"Revenue",12}{"Gross",12}{"Operating",12}{"Net",12}{"FCF",12}");
        foreach (var q in statements.Quarterly.TakeLast(8))
            Console.WriteLine($"{q.Label,-9}{M(q.Revenue),12}{M(q.GrossProfit),12}{M(q.OperatingProfit),12}{M(q.NetIncome),12}{M(q.FreeCashFlow),12}");
        break;
    }

    case "metric":
    {
        var metrics = await client.GetMetricsAsync(args[1]);
        var id = args[2];
        Console.WriteLine($"{"Period end",-12}{"Quarter",14}{"TTM",14}  formula   (USD millions for money metrics)");
        foreach (var row in metrics.Rows.TakeLast(12))
        {
            var q = row.Quarterly.GetValueOrDefault(id);
            var t = row.Ttm.GetValueOrDefault(id);
            Console.WriteLine($"{row.PeriodEnd,-12}{Scaled(q?.Value),14}{Scaled(t?.Value),14}  {q?.Formula}");
        }
        break;
    }

    case "trace":
    {
        var metrics = await client.GetMetricsAsync(args[1]);
        var latest = metrics.AuditRows
            .Where(a => a.MetricId == args[2] && a.ValueType == "quarterly" && a.Value is not null)
            .MaxBy(a => a.PeriodEnd);
        if (latest is null)
        {
            Console.WriteLine("no value");
            break;
        }

        Console.WriteLine($"{latest.MetricName}, quarter ending {latest.PeriodEnd}: {Scaled(latest.Value)}");
        Console.WriteLine($"  formula     {latest.Formula}");
        Console.WriteLine($"  concepts    {latest.SourceConcepts}");
        Console.WriteLine($"  filed       {latest.Filed}  accession {latest.Accession}");
        Console.WriteLine($"  source      {latest.SourceType} {latest.SourceDocument}");
        foreach (var input in latest.Inputs ?? [])
            Console.WriteLine($"  input       {input.Role,-10} {input.PeriodEnd}  {Scaled(input.Value),12}  filed {input.Filed}");
        break;
    }

    case "export":
    {
        var exporter = new FinancialMetricsExportService(client);
        var tickers = args.Skip(1).ToArray();
        await File.WriteAllBytesAsync("financials.xlsx", await exporter.BuildXlsxAsync(tickers));
        await File.WriteAllTextAsync("financials.csv", await exporter.BuildCsvAsync(tickers));
        Console.WriteLine("wrote financials.xlsx and financials.csv");
        break;
    }

    default:
        Console.Error.WriteLine($"unknown command {args[0]}");
        return 1;
}

return 0;

static string M(double? value) => value is { } v ? (v / 1e6).ToString("N0", CultureInfo.InvariantCulture) : "-";
// Large values (money, share counts) in millions; small ones (EPS, ratios) as-is.
static string Scaled(double? value) => value is { } v
    ? Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("N0", CultureInfo.InvariantCulture) + "M" : v.ToString("N2", CultureInfo.InvariantCulture)
    : "-";

static string N(double? value) => value is { } v ? v.ToString("N2", CultureInfo.InvariantCulture) : "-";

static class MetricsDoc
{
    public static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Metric reference");
        sb.AppendLine();
        sb.AppendLine("Generated from `FinancialMetricsCatalog` by `samples/FilingForge.Samples` (`metrics-doc`). Do not edit by hand.");
        sb.AppendLine();
        sb.AppendLine($"{FinancialMetricsCatalog.AllMetrics.Count} metrics: {FinancialMetricsCatalog.FlowMetrics.Count} flow metrics (quarterly, annual and TTM values) and {FinancialMetricsCatalog.PointMetrics.Count} point-in-time metrics (balance-sheet style, one value per period end).");
        sb.AppendLine();
        sb.AppendLine("Each metric is filled from the first source that has a value, in the order of the **Sources** column: a reported SEC concept (`DirectFact`) first, then the listed formulas.");
        sb.AppendLine();

        foreach (var group in FinancialMetricsCatalog.AllMetrics.GroupBy(m => m.Group))
        {
            sb.AppendLine($"## {Title(group.Key.ToString())}");
            sb.AppendLine();
            sb.AppendLine("| Id | Name | Kind | Definition | Sources | SEC concepts |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var m in group)
            {
                var concepts = FinancialMetricsCatalog.DirectConceptsFor(m, FinancialMetricProvider.UsGaap).Select(c => c.SourceName)
                    .Concat(FinancialMetricsCatalog.DirectConceptsFor(m, FinancialMetricProvider.IfrsFull).Select(c => c.SourceName))
                    .Distinct()
                    .Take(4)
                    .ToList();
                var more = FinancialMetricsCatalog.DirectConceptsFor(m, FinancialMetricProvider.UsGaap).Count
                           + FinancialMetricsCatalog.DirectConceptsFor(m, FinancialMetricProvider.IfrsFull).Count > concepts.Count ? ", …" : "";
                sb.AppendLine($"| `{m.Id}` | {Cell(m.DisplayName)} | {m.Kind} | {Cell(m.Definition)} | {Cell(string.Join(" → ", m.FallbackOrder))} | {Cell(string.Join(", ", concepts.Select(c => $"`{c}`")) + more)} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    private static string Title(string pascal) =>
        string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
