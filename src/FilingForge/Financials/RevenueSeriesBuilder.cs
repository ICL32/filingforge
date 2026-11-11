using FilingForge.Models;

namespace FilingForge;

public static class RevenueSeriesBuilder
{
    private static double DaysBetween(string a, string b) =>
        (DateTime.Parse(b) - DateTime.Parse(a)).TotalDays;

    private static bool IsAnnualPeriod(FinancialPoint p)
    {
        if (p.Start is null) return FinancialPointHelpers.IsAnnualReportForm(p.Form);
        var d = DaysBetween(p.Start, p.End);
        return d is >= 340 and <= 400;
    }

    /// <param name="factsAlreadyAligned">
    /// True when the caller has already run these points through a ticker-wide
    /// <see cref="StatementPeriodEnds"/>; see QuarterlySeriesBuilder.BuildQuarterlySeries.
    /// </param>
    public static RevenueSeries Build(
        string ticker,
        IEnumerable<FinancialPoint> revenuePoints,
        bool factsAlreadyAligned = false)
    {
        var points = revenuePoints.ToList();
        var calendar = FiscalCalendar.Build(points);
        var quarters = QuarterlySeriesBuilder.BuildQuarterlySeries(points, factsAlreadyAligned);
        var ttmByEnd = BuildTtmByEnd(quarters, points);
        var messagesByEnd = quarters.ToDictionary(
            q => q.End,
            q => q.ValidationMessages.ToList()
        );

        AddGapWarnings(quarters, messagesByEnd);
        AddAnnualReconciliationWarnings(points, ttmByEnd, messagesByEnd);

        var rows = quarters.Select((q, index) =>
        {
            var messages = messagesByEnd[q.End];
            if (q.Val < 0) messages.Add("negative_revenue");
            var ttmRevenue = ttmByEnd.TryGetValue(q.End, out var ttm) ? ttm : (double?)null;
            var prior = index >= 4 ? quarters[index - 4] : null;
            var yoy = prior is { Val: not 0 }
                ? (q.Val - prior.Val) / Math.Abs(prior.Val) * 100.0
                : (double?)null;
            var (fy, fq, label) = FiscalLabel(q.End, q.FiscalYear, q.FiscalQuarter, calendar);

            return new RevenueQuarterPoint(
                End: q.End,
                Label: label,
                FiscalYear: fy,
                FiscalQuarter: fq,
                QuarterlyRevenue: q.Val,
                TtmRevenue: ttmRevenue,
                RevenueYoyPct: yoy,
                Filed: q.Filed,
                SourceConcept: q.SourceConcept,
                Derivation: q.Derivation,
                ValidationStatus: messages.Count == 0 ? "ok" : "warning",
                ValidationMessages: messages
            );
        }).ToList();

        return new RevenueSeries(
            Ticker: ticker.ToUpperInvariant(),
            Quarters: rows,
            Validation: new RevenueValidationSummary(
                QuarterCount: rows.Count,
                TtmCount: rows.Count(r => r.TtmRevenue.HasValue),
                WarningCount: rows.Count(r => r.ValidationMessages.Count > 0)
            )
        );
    }

    // The fiscal calendar derived from the filer's own year ends wins over the SEC fy/fp on the
    // fact, which describes the filing the fact was taken from rather than the fact's own period
    // (see FiscalCalendar). fy/fp and the calendar-month guess remain as fallbacks for filers
    // with too few annual periods to anchor a calendar.
    private static (int Year, int Quarter, string Label) FiscalLabel(
        string end,
        int? fiscalYear,
        int? fiscalQuarter,
        FiscalCalendar calendar)
    {
        var date = DateTime.Parse(end);
        var derived = calendar.Label(end);
        var year = derived?.FiscalYear ?? fiscalYear ?? date.Year;
        var quarter = derived?.FiscalQuarter
            ?? fiscalQuarter
            ?? (date.Month <= 3 ? 1 : date.Month <= 6 ? 2 : date.Month <= 9 ? 3 : 4);
        return (year, quarter, FiscalCalendar.Format(year, quarter));
    }

    private static Dictionary<string, double> BuildTtmByEnd(
        IReadOnlyList<QuarterlySeriesBuilder.QuarterlyPoint> quarters,
        List<FinancialPoint> rawPoints)
    {
        var result = new Dictionary<string, double>();
        for (var i = 3; i < quarters.Count; i++)
        {
            // Only sum four genuinely consecutive quarters. Non-consecutive windows (e.g. a
            // skipped quarter, or Q3+Q4 pairs from two fiscal years for H1/9M-only IFRS filers)
            // would publish a bogus TTM revenue on /revenue/{ticker}.
            if (!QuarterlySeriesBuilder.IsConsecutiveQuarterWindow(quarters, i)) continue;
            result[quarters[i].End] =
                quarters[i].Val +
                quarters[i - 1].Val +
                quarters[i - 2].Val +
                quarters[i - 3].Val;
        }

        // Supplement with cumulative YTD-based TTM for H1/9M-only IFRS filers (e.g. KLAR).
        foreach (var ttm in QuarterlySeriesBuilder.BuildCumulativeTtmSeries(rawPoints))
            result.TryAdd(ttm.End, ttm.Ttm);

        return result;
    }

    private static void AddGapWarnings(
        IReadOnlyList<QuarterlySeriesBuilder.QuarterlyPoint> quarters,
        Dictionary<string, List<string>> messagesByEnd)
    {
        for (var i = 1; i < quarters.Count; i++)
        {
            var gap = DaysBetween(quarters[i - 1].End, quarters[i].End);
            if (gap is < 70 or > 135)
                messagesByEnd[quarters[i].End].Add("quarter_gap_detected");
        }
    }

    private static void AddAnnualReconciliationWarnings(
        IReadOnlyList<FinancialPoint> points,
        IReadOnlyDictionary<string, double> ttmByEnd,
        Dictionary<string, List<string>> messagesByEnd)
    {
        foreach (var annual in points.Where(IsAnnualPeriod).OrderBy(p => p.Filed))
        {
            if (!ttmByEnd.TryGetValue(annual.End, out var ttm)) continue;
            if (!messagesByEnd.TryGetValue(annual.End, out var messages)) continue;

            var tolerance = Math.Max(Math.Abs(annual.Val) * 0.005, 1.0);
            if (Math.Abs(ttm - annual.Val) > tolerance)
                messages.Add("ttm_does_not_match_annual");
        }
    }
}
