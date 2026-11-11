using FilingForge.Models;

namespace FilingForge;

/// <summary>
/// Shared period-classification helpers used by both the analysis pipeline
/// (FilingForgeClient) and the financial metrics export pipeline (FinancialMetricsBuilder).
/// Keeping them in one place ensures both paths apply identical period boundaries.
/// </summary>
internal static class FinancialPointHelpers
{
    internal static bool IsAnnualReportForm(string form)
    {
        return SecFormClassifier.IsAnnualReport(form);
    }

    internal static bool IsQuarterlyReportForm(string form)
    {
        return SecFormClassifier.IsQuarterlyReport(form);
    }

    internal static bool IsAnnualFlowFact(FinancialPoint p)
    {
        if (p.Start is null)
            return IsAnnualReportForm(p.Form);

        var days = (DateTime.Parse(p.End) - DateTime.Parse(p.Start)).TotalDays;
        if (days is < 340 or > 400) return false;

        return IsAnnualReportForm(p.Form)
            || (!IsQuarterlyReportForm(p.Form) && string.Equals(p.Fp, "FY", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Classifies a financial fact into a period bucket for deduplication.
    /// Instant facts (no Start) are bucketed by form; duration facts by day count.
    /// </summary>
    internal static string PeriodBucket(FinancialPoint p)
    {
        if (p.Start is null)
            return IsQuarterlyReportForm(p.Form)
                ? "quarter"
                : IsAnnualReportForm(p.Form)
                    ? "annual"
                    : "instant";

        var days = (DateTime.Parse(p.End) - DateTime.Parse(p.Start)).TotalDays;
        if (IsAnnualFlowFact(p)) return "annual";

        return days switch
        {
            >= 75  and <= 105 => "quarter",
            >= 165 and <= 200 => "h1",
            >= 250 and <= 295 => "9m",
            _ => $"duration:{p.Start}"
        };
    }

    /// <summary>
    /// Deduplication key for flow-period facts: period-end date + period bucket.
    /// Both the analysis and export pipelines must use this key so they pick
    /// the same canonical fact for any given reporting period.
    /// </summary>
    internal static string FlowPeriodKey(FinancialPoint p) =>
        $"{p.End}|{PeriodBucket(p)}";

    // SEC companyfacts strips share-class dimensions for instant share facts. For
    // multi-class filers, sum distinct class values from the latest filing; duration
    // weighted-average share facts are not share classes and keep last-filed-wins.
    internal static FinancialPoint? ChooseSharePoint(IEnumerable<FinancialPoint> periodFacts)
    {
        var facts = periodFacts.ToList();
        if (facts.Count == 0) return null;

        var isPointInTime = facts.All(p => p.Start is null);
        if (!isPointInTime)
            return facts.OrderBy(p => p.Filed, StringComparer.Ordinal).Last();

        var latestFiling = facts
            .GroupBy(p => p.Filed, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Last();

        var totalShares = latestFiling.Select(p => p.Val).Distinct().Sum();
        return latestFiling.First() with { Val = totalShares };
    }
}
