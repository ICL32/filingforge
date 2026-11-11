using FilingForge.Models;

namespace FilingForge;

/// <summary>
/// Quarterly weighted-average share counts. Share averages are not flows, so a missing fourth
/// quarter cannot be found by subtracting nine months from the year. Instead the year's
/// share-days are split by duration:
/// <code>Q4 = (FY average × FY days − 9M average × 9M days) ÷ Q4 days</code>
/// Reported quarters always win; any quarter that cannot be reported or reconstructed stays blank.
/// </summary>
internal static class WeightedAverageShareQuarterBuilder
{
    // Inclusive day counts that identify a period.
    private const int QuarterMinDays = 76, QuarterMaxDays = 106;
    private const int ShortQuarterMinDays = 45, ShortQuarterMaxDays = 150;
    private const double NineMonthMinShareOfYear = 0.68, NineMonthMaxShareOfYear = 0.82;

    // A nine-month average further than this from the full-year average (as a ratio) is treated
    // as being on a different share basis, usually because of a stock split in Q4.
    private const double SameBasisMinRatio = 2d / 3d, SameBasisMaxRatio = 1.5d;

    // Fiscal-year start dates of the FY and 9M facts may differ by a 52/53-week calendar shift.
    private const int FiscalStartToleranceDays = 14;

    internal static Dictionary<string, FinancialMetricQuarterEntry> Build(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric)
    {
        // One list per concept, in catalog priority. FY and 9M facts are only ever paired within
        // a single concept, so a lower-priority concept can still complete a pair the top one lacks.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var factsByConcept = new List<List<FinancialPoint>>();
        foreach (var provider in new[] { FinancialMetricProvider.UsGaap, FinancialMetricProvider.IfrsFull })
        {
            foreach (var spec in FinancialMetricsCatalog.DirectConceptsFor(metric, provider))
            {
                if (seen.Add(spec.Key))
                    factsByConcept.Add([.. inputs.PointsFor(spec)]);
            }
        }

        var quarters = new Dictionary<string, FinancialMetricQuarterEntry>(StringComparer.Ordinal);
        foreach (var facts in factsByConcept)
        {
            foreach (var entry in ReportedQuarters(facts))
                quarters.TryAdd(entry.End, entry);
        }

        var diluted = metric.Id == FinancialMetricsCatalog.DilutedAverageShares.Id;
        foreach (var facts in factsByConcept)
        {
            foreach (var year in LatestPerEnd(facts.Where(IsFullYear)).OrderBy(p => p.End, StringComparer.Ordinal))
            {
                if (quarters.ContainsKey(year.End))
                    continue;
                if (Reconstruct(inputs, facts, year, diluted) is { } q4)
                    quarters[year.End] = q4;
            }
        }

        return quarters;
    }

    // ── Reported quarters ───────────────────────────────────────────────────────────────────

    private static IEnumerable<FinancialMetricQuarterEntry> ReportedQuarters(List<FinancialPoint> facts)
    {
        var quarterFacts = facts.Where(LooksLikeQuarter).ToList();
        foreach (var q in QuarterlySeriesBuilder.BuildQuarterlySeries(quarterFacts, factsAlreadyAligned: true))
        {
            if (double.IsFinite(q.Val) && q.Val > 0)
            {
                yield return new FinancialMetricQuarterEntry(
                    q.End,
                    q.FiscalYear,
                    q.FiscalQuarter,
                    new FinancialMetricValue(q.Val, FinancialMetricFormulaIds.DirectFact, q.SourceConcept ?? "",
                        q.Filed, q.Accession, "reported", q.SourceType, q.SourceDocument, q.SourceLabel),
                    q.ValidationMessages);
            }
        }

        // A 10-K can report an odd-length final period directly (e.g. a 146-day stub after a
        // merger or year-end change). It is a reported average, so it stands in as Q4 as-is.
        var stubs = facts.Where(p => IsAnnualReportStub(p) && double.IsFinite(p.Val) && p.Val > 0);
        foreach (var stub in LatestPerEnd(stubs))
        {
            yield return new FinancialMetricQuarterEntry(
                stub.End,
                stub.Fy,
                4,
                new FinancialMetricValue(stub.Val, FinancialMetricFormulaIds.DirectFact, stub.Concept ?? "",
                    stub.Filed, stub.Accn, "reported", stub.SourceType, stub.SourceDocument, stub.SourceLabel),
                []);
        }
    }

    private static bool LooksLikeQuarter(FinancialPoint p) =>
        p.Start is null
            ? FinancialPointHelpers.IsQuarterlyReportForm(p.Form)
            : Days(p) is >= QuarterMinDays and <= QuarterMaxDays;

    private static bool IsAnnualReportStub(FinancialPoint p) =>
        Days(p) is { } d
        && d is >= ShortQuarterMinDays and <= ShortQuarterMaxDays
        && d is < QuarterMinDays or > QuarterMaxDays
        && FinancialPointHelpers.IsAnnualReportForm(p.Form)
        && string.Equals(p.Fp, "FY", StringComparison.OrdinalIgnoreCase);

    // ── Q4 reconstruction ───────────────────────────────────────────────────────────────────

    private static bool IsFullYear(FinancialPoint p) =>
        Days(p) is not null && FinancialPointHelpers.IsAnnualFlowFact(p) && double.IsFinite(p.Val) && p.Val > 0;

    private static FinancialMetricQuarterEntry? Reconstruct(
        FinancialMetricInputs inputs, List<FinancialPoint> facts, FinancialPoint year, bool diluted)
    {
        if (Days(year) is not { } yearDays || year.Start is null || string.IsNullOrWhiteSpace(year.Concept))
            return null;

        var nineMonths = facts
            .Where(p => IsNineMonthsOf(p, year, yearDays))
            .OrderByDescending(Days)
            .ThenByDescending(p => p.Filed, StringComparer.Ordinal)
            .FirstOrDefault();
        if (nineMonths is null || Days(nineMonths) is not { } nineDays)
            return null;

        var q4Days = yearDays - nineDays;
        if (q4Days is < ShortQuarterMinDays or > ShortQuarterMaxDays)
            return null;

        // Put the nine-month average on the full year's share basis.
        var splits = new List<FinancialPoint>();
        var nineValue = nineMonths.Val;
        if (!SameShareBasis(year.Val, nineValue))
        {
            splits = SplitsBetween(inputs, nineMonths, year);
            if (splits.Count == 0)
                return null;
            nineValue *= splits.Aggregate(1d, (factor, s) => factor * s.Val);
            if (!double.IsFinite(nineValue) || !SameShareBasis(year.Val, nineValue))
                return null;
        }

        if (ShareDaysRemainder(year.Val, yearDays, nineValue, nineDays, q4Days) is not { } q4)
            return null;

        var adjusted = splits.Count > 0;
        var used = new List<FinancialPoint> { year, nineMonths };
        used.AddRange(splits);

        var trace = new List<FinancialMetricInputTrace>
        {
            Trace("annual", year, yearDays),
            Trace("nineMonths", nineMonths, nineDays),
        };
        for (var i = 0; i < splits.Count; i++)
            trace.Add(Trace($"splitFactor{i + 1}", splits[i], null));

        var label = "Q4 average shares = (FY average × FY days − 9M average × 9M days) ÷ Q4 days"
            + (adjusted ? ", 9M restated for a stock split" : "")
            + (diluted ? "; diluted count estimated" : "");

        var value = new FinancialMetricValue(
            q4,
            FinancialMetricFormulaIds.WeightedAverageQuarterFromAnnual,
            Distinct([year.Concept, adjusted ? FinancialMetricsCatalog.StockSplitConversionRatio.Concept : null]) ?? "",
            used.Select(p => p.Filed).Where(f => !string.IsNullOrWhiteSpace(f)).DefaultIfEmpty("").Max(StringComparer.Ordinal) ?? "",
            Distinct(used.Select(p => p.Accn)),
            adjusted ? "duration_weighted_from_annual_split_adjusted" : "duration_weighted_from_annual",
            "derived",
            null,
            label,
            trace);

        return new FinancialMetricQuarterEntry(year.End, year.Fy, 4, value, []);
    }

    private static bool IsNineMonthsOf(FinancialPoint p, FinancialPoint year, int yearDays)
    {
        if (Days(p) is not { } days)
            return false;

        var shareOfYear = (double)days / yearDays;
        return shareOfYear is >= NineMonthMinShareOfYear and <= NineMonthMaxShareOfYear
            && string.Equals(p.Concept, year.Concept, StringComparison.Ordinal)
            && string.CompareOrdinal(p.End, year.End) < 0
            && StartsAlign(p.Start, year.Start)
            && (FinancialPointHelpers.IsQuarterlyReportForm(p.Form) || SecFormClassifier.IsSixK(p.Form))
            && double.IsFinite(p.Val)
            && p.Val > 0;
    }

    private static bool StartsAlign(string? a, string? b) =>
        DateOnly.TryParse(a, out var x)
        && DateOnly.TryParse(b, out var y)
        && Math.Abs(x.DayNumber - y.DayNumber) <= FiscalStartToleranceDays;

    private static bool SameShareBasis(double year, double nineMonths)
    {
        if (!(double.IsFinite(year) && double.IsFinite(nineMonths) && year > 0 && nineMonths > 0))
            return false;
        var ratio = year / nineMonths;
        return ratio is >= SameBasisMinRatio and <= SameBasisMaxRatio;
    }

    /// <summary>
    /// Split ratios dated after the nine-month period and on or before the year end, as known when
    /// the annual report was filed. One fact per event date (the latest filed), in date order.
    /// </summary>
    private static List<FinancialPoint> SplitsBetween(FinancialMetricInputs inputs, FinancialPoint nineMonths, FinancialPoint year)
    {
        if (!DateOnly.TryParse(nineMonths.End, out var after) || !DateOnly.TryParse(year.End, out var through))
            return [];

        bool KnownByAnnualFiling(FinancialPoint s) =>
            string.IsNullOrWhiteSpace(year.Filed)
            || string.IsNullOrWhiteSpace(s.Filed)
            || string.CompareOrdinal(s.Filed, year.Filed) <= 0;

        var candidates = inputs.PointsFor(FinancialMetricsCatalog.StockSplitConversionRatio)
            .Where(s => DateOnly.TryParse(s.End, out var on) && on > after && on <= through
                && double.IsFinite(s.Val) && s.Val > 0
                && KnownByAnnualFiling(s));
        return [.. LatestPerEnd(candidates).OrderBy(s => s.End, StringComparer.Ordinal)];
    }

    /// <summary>Rounded to whole shares (half away from zero) in decimal; null unless positive.</summary>
    private static double? ShareDaysRemainder(double year, int yearDays, double nine, int nineDays, int q4Days)
    {
        decimal shareDays;
        try
        {
            shareDays = (decimal)year * yearDays - (decimal)nine * nineDays;
        }
        catch (OverflowException)
        {
            return null;
        }

        var q4 = (double)decimal.Round(shareDays / q4Days, 0, MidpointRounding.AwayFromZero);
        return double.IsFinite(q4) && q4 > 0 ? q4 : null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Length of a duration fact counting both endpoints, or null for an instant.</summary>
    private static int? Days(FinancialPoint p) =>
        DateOnly.TryParse(p.Start, out var start) && DateOnly.TryParse(p.End, out var end) && end >= start
            ? end.DayNumber - start.DayNumber + 1
            : null;

    private static IEnumerable<FinancialPoint> LatestPerEnd(IEnumerable<FinancialPoint> facts) =>
        facts
            .GroupBy(p => p.End, StringComparer.Ordinal)
            .Select(g => g.OrderBy(p => p.Filed, StringComparer.Ordinal).Last());

    private static string? Distinct(IEnumerable<string?> values)
    {
        var kept = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToList();
        return kept.Count == 0 ? null : string.Join("|", kept);
    }

    private static FinancialMetricInputTrace Trace(string role, FinancialPoint p, int? days) =>
        new(role, p.End, p.Val, FinancialMetricFormulaIds.DirectFact,
            p.Concept ?? (days is null ? FinancialMetricsCatalog.StockSplitConversionRatio.Concept : ""),
            p.Filed, p.Accn, "reported", p.SourceType, p.SourceDocument, p.SourceLabel,
            days is null ? null : p.Start, days);
}
