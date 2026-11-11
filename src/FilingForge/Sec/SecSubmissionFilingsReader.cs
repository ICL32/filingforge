using System.Text.Json;

namespace FilingForge;

internal static class SecSubmissionFilingsReader
{
    /// <summary>Recent 10-K/10-Q/20-F/40-F/6-K style filings that can carry statements.</summary>
    internal static IReadOnlyList<LatestSubmissionFiling> ReadRecentFinancialFilings(JsonElement root)
    {
        var all = ReadRecentSubmissionFilings(root, int.MaxValue);
        return [.. all.Where(IsRecentStatementFiling)];
    }

    /// <summary>
    /// Reads company metadata from the submissions JSON root — the SEC-native
    /// replacement for a third-party company profile. All fields are top-level on
    /// every filer's submissions document.
    /// </summary>
    internal static SubmissionCompanyMeta ReadCompanyInfo(JsonElement root)
    {
        var (sic, sicDesc) = ReadSic(root);

        string? Str(string prop) =>
            root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
                ? (string.IsNullOrWhiteSpace(el.GetString()) ? null : el.GetString()!.Trim())
                : null;

        var exchanges = new List<string>();
        if (root.TryGetProperty("exchanges", out var exEl) && exEl.ValueKind == JsonValueKind.Array)
            foreach (var e in exEl.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    exchanges.Add(e.GetString()!.Trim());

        string? cik = null;
        if (root.TryGetProperty("cik", out var cikEl))
            cik = cikEl.ValueKind == JsonValueKind.Number ? cikEl.GetInt64().ToString()
                : cikEl.ValueKind == JsonValueKind.String ? cikEl.GetString()
                : null;

        return new SubmissionCompanyMeta(
            Str("name"),
            sic,
            sicDesc,
            exchanges,
            Str("stateOfIncorporationDescription") ?? Str("stateOfIncorporation"),
            FormatFiscalYearEnd(Str("fiscalYearEnd")),
            Str("category"),
            Str("website"),
            cik?.TrimStart('0'));
    }

    private static string? FormatFiscalYearEnd(string? mmdd)
    {
        if (string.IsNullOrWhiteSpace(mmdd) || mmdd.Length != 4) return null;
        if (!int.TryParse(mmdd[..2], out var m) || !int.TryParse(mmdd[2..], out var d)) return null;
        if (m < 1 || m > 12 || d < 1 || d > 31) return null;
        string[] months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        return $"{months[m - 1]} {d}";
    }

    /// <summary>
    /// Reads the SEC Standard Industrial Classification (SIC) code and its
    /// description from the submissions JSON root. Present on every filer's
    /// submissions document (top-level <c>sic</c> / <c>sicDescription</c>).
    /// </summary>
    internal static (int? Sic, string? SicDescription) ReadSic(JsonElement root)
    {
        int? sic = null;
        if (root.TryGetProperty("sic", out var sicEl))
        {
            if (sicEl.ValueKind == JsonValueKind.String && int.TryParse(sicEl.GetString(), out var parsed))
                sic = parsed;
            else if (sicEl.ValueKind == JsonValueKind.Number && sicEl.TryGetInt32(out var num))
                sic = num;
        }

        string? desc = null;
        if (root.TryGetProperty("sicDescription", out var descEl) && descEl.ValueKind == JsonValueKind.String)
        {
            var raw = descEl.GetString();
            if (!string.IsNullOrWhiteSpace(raw)) desc = raw.Trim();
        }

        return (sic, desc);
    }

    /// <summary>Filings listed under <c>filings.recent</c> of a submissions document, newest first.</summary>
    internal static IReadOnlyList<LatestSubmissionFiling> ReadRecentSubmissionFilings(JsonElement root, int maxScan)
    {
        var hasRecent = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("filings", out var filingsNode)
            && filingsNode.ValueKind == JsonValueKind.Object
            && filingsNode.TryGetProperty("recent", out _);
        return hasRecent
            ? ReadColumns(root.GetProperty("filings").GetProperty("recent"), maxScan)
            : [];
    }

    /// <summary>
    /// Filings in one of the older paged files listed under <c>filings.files</c>. Those files hold
    /// the same columns as <c>filings.recent</c>, just at the top level.
    /// </summary>
    internal static IReadOnlyList<LatestSubmissionFiling> ReadHistoricalSubmissionFilings(JsonElement root, int maxScan) =>
        ReadColumns(root, maxScan);

    // SEC submissions store filings column-wise: one array per field, all indexed alike.
    private static IReadOnlyList<LatestSubmissionFiling> ReadColumns(JsonElement table, int maxScan)
    {
        if (table.ValueKind != JsonValueKind.Object
            || !table.TryGetProperty("accessionNumber", out var accessionNumbers)
            || accessionNumbers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        string? Cell(string column, int row) => GetRecentArrayString(table, column, row);

        var result = new List<LatestSubmissionFiling>();
        var rows = Math.Min(accessionNumbers.GetArrayLength(), maxScan);
        for (var row = 0; row < rows; row++)
        {
            var accession = GetArrayString(accessionNumbers, row);
            if (string.IsNullOrWhiteSpace(accession)) continue;
            result.Add(new LatestSubmissionFiling(
                Accession: accession,
                Form: Cell("form", row),
                FilingDate: ParseDateOnly(Cell("filingDate", row)),
                ReportDate: ParseDateOnly(Cell("reportDate", row)),
                PrimaryDocument: Cell("primaryDocument", row),
                PrimaryDocDescription: Cell("primaryDocDescription", row),
                Items: Cell("items", row)));
        }

        return result;
    }

    internal static bool IsRecentStatementFiling(LatestSubmissionFiling filing)
    {
        return SecFormClassifier.IsRecentStatementFiling(filing.Form, filing.ReportDate);
    }

    internal static bool ShouldRefreshCompanyFacts(string? form)
    {
        return SecFormClassifier.ShouldRefreshCompanyFacts(form);
    }

    internal static bool ShouldRefreshCompanyFacts(LatestSubmissionFiling filing)
    {
        return SecFormClassifier.ShouldRefreshCompanyFacts(filing.Form, filing.ReportDate);
    }

    private static string? GetRecentArrayString(JsonElement recent, string propertyName, int index)
    {
        return recent.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array
            ? GetArrayString(array, index)
            : null;
    }

    private static string? GetArrayString(JsonElement array, int index)
    {
        if (index < 0 || index >= array.GetArrayLength())
            return null;

        var value = array[index];
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateOnly? ParseDateOnly(string? value)
    {
        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }
}

internal sealed record SubmissionCompanyMeta(
    string? Name,
    int? Sic,
    string? SicDescription,
    IReadOnlyList<string> Exchanges,
    string? StateOfIncorporation,
    string? FiscalYearEnd,
    string? Category,
    string? Website,
    string? Cik);

internal sealed record LatestSubmissionFiling(
    string? Accession,
    string? Form,
    DateOnly? FilingDate,
    DateOnly? ReportDate,
    string? PrimaryDocument = null,
    string? PrimaryDocDescription = null,
    string? Items = null);
