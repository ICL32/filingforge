namespace FilingForge;

public static class SecFormClassifier
{
    public static string Normalize(string? form) =>
        (form ?? "").Trim().ToUpperInvariant();

    public static bool IsAnnualReport(string? form) =>
        Normalize(form) is "10-K" or "10-K/A" or "20-F" or "20-F/A" or "40-F" or "40-F/A";

    public static bool IsQuarterlyReport(string? form) =>
        Normalize(form) is "10-Q" or "10-Q/A";

    // Domestic periodic reports whose cover page carries dei:EntityCommonStockSharesOutstanding
    // in the registrant's traded share units. Foreign reports (20-F/40-F) are excluded because
    // their cover share counts can be per-ADS, which must not silently replace ordinary-share
    // weighted-average denominators.
    public static bool IsDomesticPeriodicReport(string? form) =>
        Normalize(form) is "10-K" or "10-K/A" or "10-Q" or "10-Q/A";

    public static bool IsSixK(string? form) =>
        Normalize(form) is "6-K" or "6-K/A";

    public static bool IsEightK(string? form) =>
        Normalize(form) is "8-K" or "8-K/A";

    public static bool IsFinancialForm(string? form) =>
        IsAnnualReport(form) || IsQuarterlyReport(form) || IsSixK(form);

    public static bool ShouldRefreshCompanyFacts(string? form) =>
        IsAnnualReport(form) || IsQuarterlyReport(form);

    public static bool ShouldRefreshCompanyFacts(string? form, DateOnly? reportDate) =>
        ShouldRefreshCompanyFacts(form) || (IsSixK(form) && reportDate.HasValue);

    public static bool IsRecentStatementFiling(string? form, DateOnly? reportDate) =>
        reportDate is not null && (IsSixK(form) || IsDomesticPeriodicReport(form));

    public static string ClassifyFilingPriority(string? form)
    {
        var normalized = Normalize(form);
        if (normalized.Length == 0)
            return "unknown";

        if (ShouldRefreshCompanyFacts(normalized)
            || IsEightK(normalized)
            || normalized is "S-1" or "S-1/A" or "S-3" or "S-3/A" or "F-1" or "F-1/A" or "F-3" or "F-3/A" or "FWP"
            || normalized.StartsWith("424B", StringComparison.Ordinal)
            || normalized.StartsWith("SC 13", StringComparison.Ordinal))
        {
            return "high";
        }

        if ((normalized is "3" or "4" or "5") || IsSixK(normalized))
            return "medium-high";

        return "normal";
    }
}
