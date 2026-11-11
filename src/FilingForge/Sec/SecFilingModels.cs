using FilingForge.Models;

namespace FilingForge;

public interface ISecFilingDocumentSource
{
    Task RefreshFinancialSubmissionsAsync(string ticker, CancellationToken ct = default);

    Task<IReadOnlyList<SecFilingSummary>> GetRecentFinancialFilingsAsync(
        string ticker,
        CancellationToken ct = default);

    /// <summary>Statement-bearing documents of the given filings. Partially read filings are flagged.</summary>
    Task<SecFilingDocumentSet> GetFinancialFilingDocumentsAsync(string ticker,
        IReadOnlyCollection<string> accessions,
        CancellationToken ct = default);

    // Cover-page outstanding-share facts from recent periodic filings, used only when
    // companyfacts lacks a recent point-in-time outstanding-share fact (dual-class filers
    // whose share facts are fully share-class dimensioned, so companyfacts drops them).
    Task<IReadOnlyList<FinancialPoint>> GetRecentCoverShareFactsAsync(
        string ticker,
        int maxFilings = 12,
        CancellationToken ct = default);
}

public sealed record SecFilingSummary(
    string Cik,
    string Ticker,
    string Form,
    string Accession,
    string FilingDate,
    string? ReportDate
);

/// <summary>Documents returned for a set of filings, plus the accessions that could not be read in full.</summary>
public sealed record SecFilingDocumentSet(IReadOnlyList<SecFilingDocument> Documents, IReadOnlyCollection<string> DegradedAccessions)
{
    public static SecFilingDocumentSet Empty { get; } = new([], Array.Empty<string>());

    public bool IsDegraded(string accession) =>
        DegradedAccessions.Any(a => string.Equals(a, accession, StringComparison.OrdinalIgnoreCase));
}

public sealed record SecFilingDocument(
    string Cik,
    string Ticker,
    string Form,
    string Accession,
    string FilingDate,
    string? ReportDate,
    string FileName,
    string Content
);

public static class SecFilingConcepts
{
    public static readonly IReadOnlyDictionary<string, SecConceptSpec> BySourceName =
        FinancialMetricsCatalog.RequiredConcepts
            .GroupBy(c => c.SourceName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    public static bool TrySpec(string sourceName, out SecConceptSpec spec) =>
        BySourceName.TryGetValue(sourceName, out spec!);

    public static FinancialPoint Point(
        SecFilingDocument document,
        string sourceName,
        string? start,
        string end,
        double value,
        string sourceType,
        string? sourceLabel = null,
        int sourcePrecision = 0) =>
        new(
            start,
            end,
            value,
            document.Form,
            document.FilingDate,
            sourceName,
            DateOnly.TryParse(end, out var endDate) ? endDate.Year : null,
            FiscalPeriodFromDuration(start, end),
            null,
            document.Accession,
            sourceType,
            document.FileName,
            sourceLabel,
            sourcePrecision
        );

    private static string? FiscalPeriodFromDuration(string? start, string end)
    {
        if (start is null || !DateOnly.TryParse(start, out var s) || !DateOnly.TryParse(end, out var e))
            return null;

        var days = e.DayNumber - s.DayNumber;
        return days switch
        {
            >= 75 and <= 105 => $"Q{((e.Month - 1) / 3) + 1}",
            >= 340 and <= 400 => "FY",
            _ => null
        };
    }
}
