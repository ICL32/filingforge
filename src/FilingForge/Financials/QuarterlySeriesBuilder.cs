using FilingForge.Models;
using static FilingForge.ValuationCore;

namespace FilingForge;

public static class QuarterlySeriesBuilder
{
    public record QuarterlyPoint(
        string End,
        double Val,
        string Filed,
        int? FiscalYear,
        int? FiscalQuarter,
        string? SourceConcept,
        string? Accession,
        string SourceType,
        string? SourceDocument,
        string? SourceLabel,
        string Derivation,
        IReadOnlyList<FinancialMetricInputTrace>? Inputs,
        IReadOnlyList<string> ValidationMessages,
        // First-disclosure date of the quarter (reported) or of its latest-disclosed input
        // (reconstructed), so a TTM window built from these aligns on when the market first saw all
        // four quarters rather than a later comparative re-filing. Falls back to Filed when null.
        string? FirstFiled = null,
        // As-known restatement value history for a directly-reported quarter (see FinancialPoint.
        // Versions). Null for non-restated quarters and for reconstructed quarters (which keep their
        // single merged value). Lets BuildTtmSeries emit point-in-time TTM segments for the chart.
        IReadOnlyList<FinancialPointVersion>? Versions = null
    );

    private static double DaysBetween(string a, string b) =>
        (DateTime.Parse(b) - DateTime.Parse(a)).TotalDays;

    private static bool IsQuarterPeriod(FinancialPoint p)
    {
        if (p.Start is null) return FinancialPointHelpers.IsQuarterlyReportForm(p.Form);
        var d = DaysBetween(p.Start, p.End);
        return d is >= 75 and <= 105;
    }

    private static bool IsAnnualPeriod(FinancialPoint p) =>
        FinancialPointHelpers.IsAnnualFlowFact(p);

    // H1 = first-half YTD (~6 months), 9M = nine-month YTD
    private static bool IsH1Period(FinancialPoint p) =>
        p.Start != null && DaysBetween(p.Start, p.End) is >= 165 and <= 200;

    private static bool Is9MPeriod(FinancialPoint p) =>
        p.Start != null && DaysBetween(p.Start, p.End) is >= 250 and <= 295;

    private static readonly HashSet<string> EpsConceptNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BasicEarningsLossPerShare",
        "DilutedEarningsLossPerShare",
        "BasicEarningsLossPerShareFromContinuingOperations",
        "DilutedEarningsLossPerShareFromContinuingOperations",
        "EarningsPerShareBasic",
        "EarningsPerShareDiluted",
    };

    private record QuarterEntry(
        double Val,
        string Filed,
        string? Start,
        int? FiscalYear,
        int? FiscalQuarter,
        string? SourceConcept,
        string? Accession,
        string SourceType,
        string? SourceDocument,
        string? SourceLabel,
        int SourcePrecision,
        string Derivation,
        IReadOnlyList<FinancialMetricInputTrace>? Inputs,
        List<string> ValidationMessages,
        string? FirstFiled = null,
        IReadOnlyList<FinancialPointVersion>? Versions = null
    );

    /// <param name="factsAlreadyAligned">
    /// True when the caller has already run the facts through a ticker-wide
    /// <see cref="StatementPeriodEnds"/>. Aligning again here would rebuild the map once per
    /// metric, and would do it from a single metric's duration ends — so a metric whose own ends
    /// differ from the ticker-wide canonical date would land the same quarter on a different date
    /// than the rest of the export.
    /// </param>
    public static List<QuarterlyPoint> BuildQuarterlySeries(
        IEnumerable<FinancialPoint> points,
        bool factsAlreadyAligned = false)
    {
        var pointList = points.ToList();
        var allPoints = factsAlreadyAligned
            ? pointList
            : StatementPeriodEnds.Build(pointList).Apply(pointList);
        var annuals   = allPoints.Where(IsAnnualPeriod).ToList();
        var quarters  = allPoints.Where(p => IsQuarterPeriod(p) && !LooksLikeAnnualTotalOnQuarter(p, annuals)).ToList();
        var h1s       = allPoints.Where(IsH1Period).ToList();
        var nineMs    = allPoints.Where(Is9MPeriod).ToList();

        quarters.Sort((a, b) =>
        {
            int aHas = a.Start != null ? 1 : 0;
            int bHas = b.Start != null ? 1 : 0;
            return aHas != bHas ? aHas - bHas
                                : string.Compare(a.Filed, b.Filed, StringComparison.Ordinal);
        });

        var qByEnd = new Dictionary<string, QuarterEntry>();
        foreach (var q in quarters)
        {
            var messages = new List<string>();
            qByEnd.TryGetValue(q.End, out var previous);
            if (previous is not null && !ShouldSelectQuarter(q, previous))
                continue;

            if (previous is not null) messages.Add("duplicate_period_selected");
            var (fy, fq) = FiscalPeriod(q, previous, null);
            qByEnd[q.End] = new QuarterEntry(
                q.Val,
                q.Filed,
                q.Start,
                fy,
                fq,
                q.Concept,
                q.Accn,
                q.SourceType,
                q.SourceDocument,
                q.SourceLabel,
                q.SourcePrecision,
                "reported",
                null,
                messages,
                FirstFiledOf(q),
                q.Versions
            );
        }

        // Derive standalone Q2 from H1 cumulative: Q2 = H1 - Q1
        foreach (var h1 in h1s.OrderBy(p => p.Filed))
        {
            if (HasQuarterNear(qByEnd, h1.End)) continue;
            var h1Start = DateTime.Parse(h1.Start!);
            // Find Q1: ends between H1 start and H1 end, exclusive
            var q1 = qByEnd
                .Where(kv => { var d = DateTime.Parse(kv.Key); return d > h1Start && d < DateTime.Parse(h1.End); })
                .OrderBy(kv => kv.Key)
                .Select(kv => (End: kv.Key, kv.Value))
                .FirstOrDefault();
            if (q1.End != null)
                qByEnd[h1.End] = new QuarterEntry(
                    h1.Val - q1.Value.Val,
                    h1.Filed,
                    DayAfter(q1.End),
                    FiscalYearForPeriod(h1),
                    2,
                    h1.Concept,
                    h1.Accn,
                    h1.SourceType,
                    h1.SourceDocument,
                    h1.SourceLabel,
                    h1.SourcePrecision,
                    "derived_from_h1",
                    [Input("h1", h1), Input("q1", q1.End, q1.Value)],
                    [],
                    MaxFirstFiled(FirstFiledOf(h1), FirstFiledOf(q1.Value))
                );
        }

        // Derive standalone Q3 from 9M cumulative: Q3 = 9M - H1, or 9M - Q1 - Q2.
        foreach (var nineM in nineMs.OrderBy(p => p.Filed))
        {
            if (HasQuarterNear(qByEnd, nineM.End)) continue;
            var nineMStart = DateTime.Parse(nineM.Start!);

            var priorQs = qByEnd
                .Where(kv =>
                {
                    var d = DateTime.Parse(kv.Key);
                    return d > nineMStart && d < DateTime.Parse(nineM.End);
                })
                .OrderBy(kv => kv.Key)
                .Select(kv => (End: kv.Key, kv.Value))
                .ToList();

            if (priorQs.Count == 0)
            {
                // No quarters derived yet for this fiscal year. Attempt Q3 = 9M - H1 directly
                // using date containment: find the H1 whose period falls inside the 9M period.
                // This handles IFRS filers (e.g. H1/9M-only 6-K reporters) that never file a
                // standalone Q1, which would otherwise leave Q3 permanently blank.
                var directH1 = h1s
                    .Where(p =>
                    {
                        var h1Start = DateTime.Parse(p.Start!);
                        var h1End   = DateTime.Parse(p.End);
                        return h1Start >= nineMStart && h1End > nineMStart && h1End < DateTime.Parse(nineM.End);
                    })
                    .OrderByDescending(p => p.Filed)
                    .FirstOrDefault();

                if (directH1 != null)
                    qByEnd[nineM.End] = new QuarterEntry(
                        nineM.Val - directH1.Val,
                        nineM.Filed,
                        DayAfter(directH1.End),
                        FiscalYearForPeriod(nineM),
                        3,
                        nineM.Concept,
                        nineM.Accn,
                        nineM.SourceType,
                        nineM.SourceDocument,
                        nineM.SourceLabel,
                        nineM.SourcePrecision,
                        "derived_from_9m",
                        [Input("nineMonths", nineM), Input("h1", directH1)],
                        [],
                        MaxFirstFiled(FirstFiledOf(nineM), FirstFiledOf(directH1))
                    );
                continue;
            }

            var h1End = priorQs[^1].End;
            var matchingH1 = h1s.Where(p => p.End == h1End).OrderByDescending(p => p.Filed).FirstOrDefault();

            if (matchingH1 != null)
            {
                qByEnd[nineM.End] = new QuarterEntry(
                    nineM.Val - matchingH1.Val,
                    nineM.Filed,
                    DayAfter(matchingH1.End),
                    FiscalYearForPeriod(nineM),
                    3,
                    nineM.Concept,
                    nineM.Accn,
                    nineM.SourceType,
                    nineM.SourceDocument,
                    nineM.SourceLabel,
                    nineM.SourcePrecision,
                    "derived_from_9m",
                    [Input("nineMonths", nineM), Input("h1", matchingH1)],
                    [],
                    MaxFirstFiled(FirstFiledOf(nineM), FirstFiledOf(matchingH1))
                );
            }
            else if (priorQs.Count >= 2)
            {
                qByEnd[nineM.End] = new QuarterEntry(
                    nineM.Val - priorQs.Take(2).Sum(q => q.Value.Val),
                    nineM.Filed,
                    DayAfter(priorQs[1].End),
                    FiscalYearForPeriod(nineM),
                    3,
                    nineM.Concept,
                    nineM.Accn,
                    nineM.SourceType,
                    nineM.SourceDocument,
                    nineM.SourceLabel,
                    nineM.SourcePrecision,
                    "derived_from_9m",
                    [Input("nineMonths", nineM), Input("q1", priorQs[0].End, priorQs[0].Value), Input("q2", priorQs[1].End, priorQs[1].Value)],
                    [],
                    MaxFirstFiled(FirstFiledOf(nineM), FirstFiledOf(priorQs[0].Value), FirstFiledOf(priorQs[1].Value))
                );
            }
        }

        // Derive standalone Q2 from 9M cumulative when a filer reports Q1 and Q3,
        // but does not publish a separate H1/Q2 period: Q2 = 9M - Q1 - Q3.
        foreach (var nineM in nineMs.OrderBy(p => p.Filed))
        {
            var nineMStart = DateTime.Parse(nineM.Start!);
            var nineMEnd = DateTime.Parse(nineM.End);

            if (!qByEnd.TryGetValue(nineM.End, out var q3) || q3.Derivation != "reported")
                continue;

            var q1UpperBound = DateTime.TryParse(q3.Start, out var q3Start)
                ? q3Start
                : nineMEnd;
            var q1 = qByEnd
                .Where(kv =>
                {
                    var d = DateTime.Parse(kv.Key);
                    return d > nineMStart && d < q1UpperBound;
                })
                .OrderBy(kv => kv.Key)
                .LastOrDefault();
            if (q1.Key is null)
                continue;

            if (!TryBuildQ2FromNineMonthPeriod(nineM, q1.Key, q1.Value, q3, out var q2Start, out var q2End))
                continue;

            if (HasQuarterNear(qByEnd, q2End)) continue;

            qByEnd[q2End] = new QuarterEntry(
                nineM.Val - q1.Value.Val - q3.Val,
                LatestFiled(nineM.Filed, q1.Value.Filed, q3.Filed),
                q2Start,
                FiscalYearForPeriod(nineM),
                2,
                nineM.Concept,
                nineM.Accn,
                nineM.SourceType,
                nineM.SourceDocument,
                nineM.SourceLabel,
                nineM.SourcePrecision,
                "derived_from_9m",
                [Input("nineMonths", nineM), Input("q1", q1.Key, q1.Value), Input("q3", nineM.End, q3)],
                [],
                MaxFirstFiled(FirstFiledOf(nineM), FirstFiledOf(q1.Value), FirstFiledOf(q3))
            );
        }

        foreach (var ann in annuals)
        {
            var annEnd   = DateTime.Parse(ann.End);
            var annStart = ann.Start != null ? DateTime.Parse(ann.Start) : annEnd.AddYears(-1);

            var threeQs = qByEnd
                .Where(kv => { var d = DateTime.Parse(kv.Key); return d > annStart && d < annEnd; })
                .OrderBy(kv => kv.Key)
                .ToList();

            if (HasQuarterNear(qByEnd, ann.End))
                continue;

            if (threeQs.Count == 3)
            {
                var year = AnnualAsKnownAt(ann, LatestFiled([.. threeQs.Select(kv => kv.Value.Filed)]));
                qByEnd[ann.End] = Q4FromYear(
                    year,
                    threeQs.Sum(kv => kv.Value.Val),
                    DayAfter(threeQs[^1].Key),
                    [
                        Input("annual", year),
                        Input("q1", threeQs[0].Key, threeQs[0].Value),
                        Input("q2", threeQs[1].Key, threeQs[1].Value),
                        Input("q3", threeQs[2].Key, threeQs[2].Value)
                    ],
                    FirstFiledOf(year),
                    FirstFiledOf(threeQs[0].Value),
                    FirstFiledOf(threeQs[1].Value),
                    FirstFiledOf(threeQs[2].Value));
                continue;
            }

            // Fewer than 3 quarters exist (e.g. H1/9M-only filer with no Q1/Q2 individually).
            // Q4 = Annual - 9M is mathematically identical to Annual - Q1 - Q2 - Q3 because
            // 9M = Q1+Q2+Q3. Require the 9M period to be fully contained in this annual period
            // (start >= annual start AND end inside the annual window) so an overlapping or
            // restated 9M from a different fiscal year is never subtracted.
            var matching9M = nineMs
                .Where(p =>
                {
                    if (p.Start == null) return false;
                    var s = DateTime.Parse(p.Start);
                    var d = DateTime.Parse(p.End);
                    return s >= annStart && d > annStart && d < annEnd;
                })
                .OrderByDescending(p => p.Filed)
                .FirstOrDefault();
            if (threeQs.Count > 3 || matching9M is null)
                continue;

            var fullYear = AnnualAsKnownAt(ann, matching9M.Filed);
            qByEnd[ann.End] = Q4FromYear(
                fullYear,
                matching9M.Val,
                DayAfter(matching9M.End),
                [Input("annual", fullYear), Input("nineMonths", matching9M)],
                FirstFiledOf(fullYear),
                FirstFiledOf(matching9M));
        }

        return qByEnd
            .Select(kv => new QuarterlyPoint(
                End: kv.Key,
                Val: kv.Value.Val,
                Filed: kv.Value.Filed,
                FiscalYear: kv.Value.FiscalYear,
                FiscalQuarter: kv.Value.FiscalQuarter,
                SourceConcept: kv.Value.SourceConcept,
                Accession: kv.Value.Accession,
                SourceType: kv.Value.SourceType,
                SourceDocument: kv.Value.SourceDocument,
                SourceLabel: kv.Value.SourceLabel,
                Derivation: kv.Value.Derivation,
                Inputs: kv.Value.Inputs,
                ValidationMessages: kv.Value.ValidationMessages,
                FirstFiled: kv.Value.FirstFiled,
                Versions: kv.Value.Versions
            ))
            .OrderBy(q => q.End)
            .ToList();
    }

    private static DateTime EndOfMonth(DateTime value) =>
        new(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month));

    private static string DayAfter(string value) =>
        DateTime.Parse(value).AddDays(1).ToString("yyyy-MM-dd");

    private static bool TryBuildQ2FromNineMonthPeriod(
        FinancialPoint nineM,
        string q1EndText,
        QuarterEntry q1,
        QuarterEntry q3,
        out string q2Start,
        out string q2End)
    {
        q2Start = "";
        q2End = "";

        if (!DateTime.TryParse(nineM.Start, out var nineMStart)
            || !DateTime.TryParse(nineM.End, out var nineMEnd)
            || !DateTime.TryParse(q1EndText, out var q1End))
            return false;

        if (DateTime.TryParse(q3.Start, out var q3Start))
        {
            if (!DateTime.TryParse(q1.Start, out var q1Start) || q1Start != nineMStart)
                return false;

            var q2StartDate = q1End.AddDays(1);
            var q2EndDate = q3Start.AddDays(-1);
            if (q2StartDate > q2EndDate || q3Start > nineMEnd)
                return false;
            if ((q2EndDate - q2StartDate).TotalDays is < 75 or > 105)
                return false;

            q2Start = q2StartDate.ToString("yyyy-MM-dd");
            q2End = q2EndDate.ToString("yyyy-MM-dd");
            return true;
        }

        if (!IsCalendarAlignedQ2FromNineMonths(nineMStart, nineMEnd, q1.Start, q1End, q3))
            return false;

        q2Start = q1End.AddDays(1).ToString("yyyy-MM-dd");
        q2End = EndOfMonth(nineMEnd.AddMonths(-3)).ToString("yyyy-MM-dd");
        return true;
    }

    private static bool IsCalendarAlignedQ2FromNineMonths(
        DateTime nineMStart,
        DateTime nineMEnd,
        string? q1StartText,
        DateTime q1End,
        QuarterEntry q3)
    {
        if (!DateTime.TryParse(q1StartText, out var q1Start))
            return false;

        return nineMStart.Month == 1
            && nineMStart.Day == 1
            && nineMEnd.Month == 9
            && nineMEnd.Day == 30
            && q1Start == nineMStart
            && q1End.Month == 3
            && q1End.Day == 31
            && DateTime.TryParse(q3.Start, out _) == false
            && q3.FiscalQuarter == 3;
    }

    private static string LatestFiled(params string[] filedDates) =>
        filedDates.OrderBy(date => date, StringComparer.Ordinal).Last();

    // First-disclosure date of a point/quarter (falls back to its latest-restatement Filed when the
    // earliest date is unknown — e.g. snapshots predating FirstFiled).
    private static string FirstFiledOf(FinancialPoint p) => p.FirstFiled ?? p.Filed;
    private static string FirstFiledOf(QuarterEntry e) => e.FirstFiled ?? e.Filed;

    // A reconstructed quarter is first public when ALL of its inputs are public, so its
    // first-disclosure date is the latest of the inputs' first-disclosure dates.
    private static string MaxFirstFiled(params string?[] dates) =>
        dates.Where(d => !string.IsNullOrEmpty(d)).Select(d => d!).OrderBy(d => d, StringComparer.Ordinal).Last();

    private static FinancialMetricInputTrace Input(string role, FinancialPoint point) =>
        new(
            role,
            point.End,
            point.Val,
            FinancialMetricFormulaIds.DirectFact,
            point.Concept ?? "",
            point.Filed,
            point.Accn,
            "reported",
            point.SourceType,
            point.SourceDocument,
            point.SourceLabel
        );

    private static FinancialMetricInputTrace Input(string role, string periodEnd, QuarterEntry entry) =>
        new(
            role,
            periodEnd,
            entry.Val,
            entry.Derivation switch
            {
                "derived_from_h1" => FinancialMetricFormulaIds.DerivedFromH1,
                "derived_from_9m" => FinancialMetricFormulaIds.DerivedFrom9M,
                "derived_from_annual" => FinancialMetricFormulaIds.DerivedFromAnnual,
                _ => FinancialMetricFormulaIds.DirectFact
            },
            entry.SourceConcept ?? "",
            entry.Filed,
            entry.Accession,
            entry.Derivation,
            entry.SourceType,
            entry.SourceDocument,
            entry.SourceLabel
        );

    /// <param name="factsAlreadyAligned">
    /// See <see cref="BuildQuarterlySeries"/>. Callers building several metrics off one fundamentals
    /// set should align them together and pass true, so the period-end set is not rebuilt per metric
    /// from that metric's own ends.
    /// </param>
    public static List<TtmPoint> BuildTtmSeries(
        IEnumerable<FinancialPoint> points,
        bool factsAlreadyAligned = false)
    {
        var allPoints = points.ToList();
        var sorted    = BuildQuarterlySeries(allPoints, factsAlreadyAligned);

        var result = new List<TtmPoint>(sorted.Count);
        for (var i = 3; i < sorted.Count; i++)
        {
            if (!IsConsecutiveQuarterWindow(sorted, i)) continue;

            var filed = sorted[i - 3].Filed;
            for (var j = i - 2; j <= i; j++)
                if (string.Compare(sorted[j].Filed, filed, StringComparison.Ordinal) > 0)
                    filed = sorted[j].Filed;

            var window = new[] { sorted[i], sorted[i - 1], sorted[i - 2], sorted[i - 3] };

            // FirstFiled = latest first-disclosure date in the window: when the market first saw all
            // four quarters, free of any later comparative re-filing. Drives valuation alignment.
            var firstFiled = MaxFirstFiled(window.Select(q => q.FirstFiled ?? q.Filed).ToArray());

            var sourceConcepts = JoinDistinct(window.Select(q => q.SourceConcept));
            var derivation     = JoinDistinct(window.Select(q => q.Derivation));
            var accessions     = JoinDistinct(window.Select(q => q.Accession));
            var quartersTrace  = window.OrderBy(q => q.End, StringComparer.Ordinal).ToList();

            // Common case — no quarter in the window was materially restated: emit exactly one TTM
            // point, byte-identical to before. Filed = latest filing date in the window (earliest
            // point all 4 quarters were public). Accessions/Quarters carry provenance so the trace can
            // map this point directly instead of rebuilding the window (single builder, no drift).
            if (!window.Any(q => q.Versions is { Count: > 0 }))
            {
                result.Add(new TtmPoint(
                    End:            sorted[i].End,
                    Ttm:            sorted[i].Val + sorted[i - 1].Val + sorted[i - 2].Val + sorted[i - 3].Val,
                    Filed:          filed,
                    SourceConcepts: sourceConcepts,
                    Derivation:     derivation,
                    FirstFiled:     firstFiled,
                    Accessions:     accessions,
                    Quarters:       quartersTrace
                ));
                continue;
            }

            // A quarter in this window was restated. Emit one point-in-time TTM segment per date the
            // window's sum changed, so ValuationCore.ClosestByKey pairs each price bar with the value
            // the market actually knew then — the restated total only appears on bars on/after its
            // filing date, not backward onto pre-restatement bars.
            result.AddRange(BuildWindowSegments(
                window, sorted[i].End, firstFiled, sourceConcepts, derivation, accessions, quartersTrace));
        }

        // Supplement with cumulative YTD-based TTM for H1/9M-only IFRS filers where no window
        // of four consecutive quarters can be formed (e.g. KLAR: only Q3+Q4 per fiscal year).
        var covered = new HashSet<string>(result.Select(t => t.End), StringComparer.Ordinal);
        result.AddRange(BuildCumulativeTtmSeries(allPoints).Where(t => !covered.Contains(t.End)));
        result.Sort((a, b) => string.Compare(a.End, b.End, StringComparison.Ordinal));
        return result;
    }

    /// <summary>Q4 = full year minus what the first three quarters (or nine months) already hold.</summary>
    private static QuarterEntry Q4FromYear(
        FinancialPoint year,
        double alreadyReported,
        string start,
        IReadOnlyList<FinancialMetricInputTrace> inputs,
        params string?[] firstFiledDates) =>
        new(
            year.Val - alreadyReported,
            year.Filed,
            start,
            FiscalYearForPeriod(year),
            4,
            year.Concept,
            year.Accn,
            year.SourceType,
            year.SourceDocument,
            year.SourceLabel,
            year.SourcePrecision,
            "derived_from_annual",
            inputs,
            [],
            MaxFirstFiled(firstFiledDates));

    /// <summary>
    /// A three-month fact from a 10-K/20-F that is really the annual figure mislabelled. True when
    /// it equals (any known version of) an annual value with the same end date, or when it comes
    /// from companyfacts without an SEC frame, which leaves it unverifiable. A framed fact with a
    /// different value is a genuine Q4 disclosure.
    /// </summary>
    private static bool LooksLikeAnnualTotalOnQuarter(FinancialPoint quarter, IReadOnlyList<FinancialPoint> annuals)
    {
        if (!FinancialPointHelpers.IsAnnualReportForm(quarter.Form) || string.IsNullOrWhiteSpace(quarter.Accn))
            return false;

        var annualsAtEnd = annuals.Where(a => a.End == quarter.End).ToList();
        if (annualsAtEnd.Count == 0)
            return false;

        foreach (var annual in annualsAtEnd)
        {
            if (NearlyEqual(quarter.Val, annual.Val))
                return true;
            foreach (var version in annual.Versions ?? [])
                if (NearlyEqual(quarter.Val, version.Val))
                    return true;
        }

        return string.Equals(quarter.SourceType, "companyfacts", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(quarter.Frame);
    }

    /// <summary>
    /// True when a quarter already ends within ten days of <paramref name="end"/>. Derived
    /// boundaries of 52/53-week filers can be a day off the reported ones; real quarter ends are
    /// months apart, so anything this close is the same quarter.
    /// </summary>
    private static bool HasQuarterNear(IReadOnlyDictionary<string, QuarterEntry> quarters, string end)
    {
        if (!DateTime.TryParse(end, out var target))
            return false;
        foreach (var key in quarters.Keys)
        {
            if (DateTime.TryParse(key, out var existing) && Math.Abs((existing - target).TotalDays) <= 10)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The annual figure as it stood when <paramref name="asOfFiled"/> was filed. A later 10-K can
    /// recast the year without recasting its quarters; subtracting old quarters from a new total
    /// would leave a bogus Q4. Versions of the annual's own concept are preferred when present.
    /// </summary>
    private static FinancialPoint AnnualAsKnownAt(FinancialPoint annual, string asOfFiled)
    {
        if (annual.Versions is not { Count: > 0 } all)
            return annual;

        IReadOnlyList<FinancialPointVersion> history = all;
        if (!string.IsNullOrWhiteSpace(annual.Concept))
        {
            var sameConcept = all.Where(v => v.Concept == annual.Concept).ToList();
            if (sameConcept.Count > 0)
                history = sameConcept;
        }

        var chosen = history.LastOrDefault(v => string.CompareOrdinal(v.Filed, asOfFiled) <= 0) ?? history[0];
        return annual with { Val = chosen.Val, Filed = chosen.Filed, Accn = chosen.Accn ?? annual.Accn, FirstFiled = chosen.Filed };
    }

    private static bool NearlyEqual(double x, double y) =>
        Math.Abs(x - y) / Math.Max(1d, Math.Max(Math.Abs(x), Math.Abs(y))) <= 1e-6;

    // Expands one restated TTM window into point-in-time segments, one per date the window's sum
    // changed. Boundaries = the window's first-all-public date plus every restatement filing date of
    // its quarters after that. Each segment sums the value each quarter was known at as of the
    // boundary; consecutive segments with an unchanged sum collapse. All segments share the period
    // End; they differ only by FirstFiled (their as-of date), which is what ClosestByKey selects on.
    private static IEnumerable<TtmPoint> BuildWindowSegments(
        IReadOnlyList<QuarterlyPoint> window,
        string end,
        string baselineFirstFiled,
        string? sourceConcepts,
        string? derivation,
        string? accessions,
        IReadOnlyList<QuarterlyPoint> quartersTrace)
    {
        var boundaries = new SortedSet<string>(StringComparer.Ordinal) { baselineFirstFiled };
        foreach (var q in window)
            if (q.Versions is not null)
                foreach (var v in q.Versions)
                    if (string.CompareOrdinal(v.Filed, baselineFirstFiled) > 0)
                        boundaries.Add(v.Filed);

        var segments = new List<TtmPoint>();
        foreach (var boundary in boundaries)
        {
            var sum = 0.0;
            foreach (var q in window)
                sum += QuarterValueAsOf(q, boundary);

            // Collapse a boundary that did not change the total (e.g. two quarters restated on the
            // same filing already coalesced, or a version below the window's aggregate materiality).
            if (segments.Count > 0 && segments[^1].Ttm == sum)
                continue;

            segments.Add(new TtmPoint(
                End:            end,
                Ttm:            sum,
                Filed:          boundary,
                SourceConcepts: sourceConcepts,
                Derivation:     derivation,
                FirstFiled:     boundary,
                Accessions:     accessions,
                Quarters:       quartersTrace));
        }

        return segments;
    }

    // Value a quarter was known at as of `date`: the latest restatement version filed on/before the
    // date, or the earliest version when the date precedes them all (a pre-first-disclosure bar sees
    // the original value). Quarters with no version history return their single value.
    private static double QuarterValueAsOf(QuarterlyPoint q, string date)
    {
        if (q.Versions is not { Count: > 0 } versions)
            return q.Val;

        var chosen = versions[0];
        foreach (var v in versions)
            if (string.CompareOrdinal(v.Filed, date) <= 0)
                chosen = v;
        return chosen.Val;
    }

    /// <summary>
    /// Merges OCF and CapEx TTM series by end date into FCF = OCF − CapEx. CapEx in SEC EDGAR is a
    /// positive outflow, so it is subtracted. The single FCF builder shared by the production snapshot
    /// (<see cref="AnalysisOrchestrator"/>) and the diagnostic trace (<see cref="AnalysisTraceBuilder"/>),
    /// so P/FCF cannot drift between the two paths. Filed = later of the two filings (both must be
    /// public before FCF is valid); FirstFiled = later of the two first-disclosure dates so valuation
    /// alignment uses the moment both inputs were first public. SourceConcepts/Accessions are joined
    /// provenance the trace renders; production ignores them.
    /// </summary>
    public static List<TtmPoint> BuildFcfSeries(
        IReadOnlyList<TtmPoint> ttmOcf,
        IReadOnlyList<TtmPoint> ttmCapex)
    {
        // OCF and CapEx TTM series may each carry several point-in-time segments for the same period
        // End (a restated window emits one segment per restatement date, differing only by FirstFiled;
        // see BuildTtmSeries/BuildWindowSegments). Group CapEx by End instead of assuming a unique End
        // — ToDictionary(p => p.End) threw "same key" for any ticker with a restated OCF/CapEx window
        // (e.g. META, CapEx restated at 2016-03-31), which failed the entire analysis. Each OCF segment
        // pairs with the CapEx segment for the same End known as-of that segment's FirstFiled, so FCF
        // stays lookahead-safe.
        var capexByEnd = ttmCapex
            .GroupBy(p => p.End, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TtmPoint>)[.. g.OrderBy(p => p.FirstFiled ?? p.Filed, StringComparer.Ordinal)],
                StringComparer.Ordinal);
        var result = new List<TtmPoint>(ttmOcf.Count);

        foreach (var ocf in ttmOcf)
        {
            if (!capexByEnd.TryGetValue(ocf.End, out var capexSegments))
                continue;

            var capexPoint = ClosestKnownSegment(capexSegments, ocf.FirstFiled ?? ocf.Filed);
            var filed = LaterDate(ocf.Filed, capexPoint.Filed);
            var firstFiled = LaterDate(ocf.FirstFiled ?? ocf.Filed, capexPoint.FirstFiled ?? capexPoint.Filed);

            result.Add(new TtmPoint(
                End:            ocf.End,
                Ttm:            ocf.Ttm - capexPoint.Ttm,
                Filed:          filed,
                SourceConcepts: JoinDistinct(new[] { ocf.SourceConcepts, capexPoint.SourceConcepts }),
                Derivation:     "ttmOperatingCashFlow - ttmCapex",
                FirstFiled:     firstFiled,
                Accessions:     JoinDistinct(new[] { ocf.Accessions, capexPoint.Accessions })));
        }

        return result;
    }

    // Picks the point-in-time TTM segment for one period End known as-of a given date: the latest
    // segment whose FirstFiled is on or before asOf, or the earliest segment when none is yet known
    // (mirrors the closest-known selection used to pair price bars to fundamentals). Segments must be
    // pre-sorted ascending by FirstFiled.
    //
    // When every segment was first filed AFTER asOf (the OCF as-of predates all CapEx disclosures) this
    // returns the earliest segment, which is NOT a lookahead exposure: the caller sets the FCF point's
    // FirstFiled to LaterDate(ocf.FirstFiled, capex.FirstFiled) = that earliest CapEx disclosure, so the
    // resulting FCF point only pairs with price bars on/after the CapEx became public. The returned
    // value and the point's as-of date stay consistent.
    private static TtmPoint ClosestKnownSegment(IReadOnlyList<TtmPoint> segmentsAscending, string asOf)
    {
        var chosen = segmentsAscending[0];
        foreach (var segment in segmentsAscending)
            if (string.CompareOrdinal(segment.FirstFiled ?? segment.Filed, asOf) <= 0)
                chosen = segment;
        return chosen;
    }

    /// <summary>
    /// Computes TTM values from cumulative (YTD) filings for filers that never publish
    /// standalone quarterly periods — e.g. IFRS H1/9M-only reporters such as KLAR.
    /// <list type="bullet">
    ///   <item>Annual period ends: TTM = the annual value itself (a 12-month total).</item>
    ///   <item>9M period ends: TTM = PriorAnnual − Prior9M + Current9M.</item>
    /// </list>
    /// Use alongside <see cref="BuildTtmSeries"/> so quarterly-sum TTM takes priority when both
    /// apply to the same period end; cumulative values only fill period ends with no quarterly-sum TTM.
    /// </summary>
    public static List<TtmPoint> BuildCumulativeTtmSeries(IEnumerable<FinancialPoint> rawPoints)
    {
        var allPoints = rawPoints.ToList();
        var annuals   = allPoints.Where(IsAnnualPeriod).ToList();
        var nineMs    = allPoints.Where(Is9MPeriod).ToList();
        var result    = new List<TtmPoint>();

        // Annual period ends: the annual value is itself a trailing-12-month total.
        foreach (var ann in annuals)
            result.Add(new TtmPoint(
                End:            ann.End,
                Ttm:            ann.Val,
                Filed:          ann.Filed,
                SourceConcepts: ann.Concept,
                Derivation:     "cumulative_annual",
                FirstFiled:     FirstFiledOf(ann),
                Accessions:     ann.Accn
            ));

        // 9M period ends: TTM = PriorAnnual − Prior9M + Current9M.
        // Prior9M ends ~1 year before the current 9M; the annual bridges the two.
        foreach (var curr9M in nineMs)
        {
            var currEnd = DateTime.Parse(curr9M.End);

            var prev9M = nineMs
                .Where(p => { var gap = (currEnd - DateTime.Parse(p.End)).TotalDays; return gap is >= 335 and <= 395; })
                .OrderByDescending(p => p.Filed)
                .FirstOrDefault();
            if (prev9M is null) continue;

            var prevEnd = DateTime.Parse(prev9M.End);
            var ann = annuals
                .Where(p => { var d = DateTime.Parse(p.End); return d > prevEnd && d < currEnd; })
                .OrderByDescending(p => p.Filed)
                .FirstOrDefault();
            if (ann is null) continue;

            var filed = new[] { ann.Filed, prev9M.Filed, curr9M.Filed }
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .First();
            result.Add(new TtmPoint(
                End:            curr9M.End,
                Ttm:            ann.Val - prev9M.Val + curr9M.Val,
                Filed:          filed,
                SourceConcepts: JoinDistinct(new string?[] { ann.Concept, prev9M.Concept, curr9M.Concept }),
                Derivation:     "cumulative_9m",
                FirstFiled:     MaxFirstFiled(FirstFiledOf(ann), FirstFiledOf(prev9M), FirstFiledOf(curr9M)),
                Accessions:     JoinDistinct(new[] { ann.Accn, prev9M.Accn, curr9M.Accn })
            ));
        }

        return result;
    }

    /// <summary>
    /// True when the four-quarter window ending at <paramref name="endIndex"/> in the
    /// end-date-sorted <paramref name="sorted"/> list is four consecutive quarters.
    /// Consecutiveness is measured by fiscal-quarter index (falling back to the calendar
    /// quarter of each period end), so a missing quarter is detected even when the calendar
    /// span stays under a year — e.g. H1/9M-only IFRS filers whose only reconstructable
    /// quarters are Q3+Q4 of separate fiscal years, or any series with a single skipped
    /// quarter. Mirrors FinancialMetricsBuilder.IsConsecutiveQuarterWindow so the analysis
    /// TTM series (revenue, P/E, P/S, trace) and the export TTM series agree.
    /// </summary>
    public static bool IsConsecutiveQuarterWindow(IReadOnlyList<QuarterlyPoint> sorted, int endIndex)
    {
        if (endIndex < 3 || endIndex >= sorted.Count) return false;

        int? previous = null;
        for (var j = endIndex - 3; j <= endIndex; j++)
        {
            var index = QuarterIndex(sorted[j]);
            if (!index.HasValue) return false;
            if (previous.HasValue && index.Value != previous.Value + 1) return false;
            previous = index;
        }
        return true;
    }

    private static int? QuarterIndex(QuarterlyPoint q)
    {
        if (q.FiscalYear.HasValue && q.FiscalQuarter is >= 1 and <= 4)
            return q.FiscalYear.Value * 4 + q.FiscalQuarter.Value - 1;
        if (!DateTime.TryParse(q.End, out var date)) return null;
        return date.Year * 4 + (date.Month - 1) / 3;
    }

    private static (int? FiscalYear, int? FiscalQuarter) FiscalPeriod(
        FinancialPoint point,
        QuarterEntry? previous,
        int? forcedQuarter)
    {
        // Preserve the already-established fiscal metadata across restatements:
        // a later filing for the same period may carry a different Fy (e.g., a fiscal
        // year boundary change), but the originally assigned quarter label is canonical.
        if (previous is { FiscalYear: not null, FiscalQuarter: not null })
            return (previous.FiscalYear, previous.FiscalQuarter);
        var quarter = forcedQuarter ?? ParseQuarter(point.Fp);
        var fiscalYear = FiscalYearForPeriod(point);
        if (quarter.HasValue && fiscalYear.HasValue) return (fiscalYear, quarter);
        return (null, null);
    }

    private static int? FiscalYearForPeriod(FinancialPoint point)
    {
        if (!point.Fy.HasValue)
            return null;

        if (!DateTime.TryParse(point.Start, out var start) || !DateTime.TryParse(point.End, out var end))
            return point.Fy;

        return ShouldUseCalendarPeriodYear(point, start, end) ? end.Year : point.Fy;
    }

    private static bool ShouldUseCalendarPeriodYear(FinancialPoint point, DateTime start, DateTime end)
    {
        if (point.Fy == end.Year || !IsCalendarAlignedFlowPeriod(start, end))
            return false;

        // SEC companyfacts can attach the filing fiscal year to calendar-year comparative
        // periods. Only override fiscal metadata when the period shape itself is calendar
        // reporting, not when a non-calendar filer legitimately reports Q1 as Oct-Dec.
        if (IsCalendarYearToDatePeriod(start, end))
            return true;

        var fiscalQuarter = ParseQuarter(point.Fp);
        return fiscalQuarter.HasValue && fiscalQuarter.Value == CalendarQuarter(end);
    }

    private static bool IsCalendarAlignedFlowPeriod(DateTime start, DateTime end)
    {
        if (start.Day != 1 || start.Year != end.Year)
            return false;

        if (start.Month == 1 && end.Month == 3 && end.Day == 31)
            return true;
        if (start.Month == 4 && end.Month == 6 && end.Day == 30)
            return true;
        if (start.Month == 7 && end.Month == 9 && end.Day == 30)
            return true;
        if (start.Month == 10 && end.Month == 12 && end.Day == 31)
            return true;

        return start.Month == 1
            && ((end.Month == 6 && end.Day == 30)
                || (end.Month == 9 && end.Day == 30)
                || (end.Month == 12 && end.Day == 31));
    }

    private static bool IsCalendarYearToDatePeriod(DateTime start, DateTime end) =>
        start.Month == 1
        && start.Day == 1
        && ((end.Month == 6 && end.Day == 30)
            || (end.Month == 9 && end.Day == 30)
            || (end.Month == 12 && end.Day == 31));

    private static int CalendarQuarter(DateTime end) => ((end.Month - 1) / 3) + 1;

    // Pipe-joins the distinct non-empty values (SEC concepts or derivations) of a TTM window's
    // quarters, preserving first-seen order, for the debug concept trace. Returns null when empty.
    private static string? JoinDistinct(IEnumerable<string?> values)
    {
        var distinct = values
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .Distinct()
            .ToList();
        return distinct.Count > 0 ? string.Join("|", distinct) : null;
    }

    private static bool ShouldSelectQuarter(FinancialPoint candidate, QuarterEntry existing)
    {
        var candidatePerAds = IsPerAdsEps(candidate.Concept, candidate.SourceType, candidate.SourceLabel);
        var existingPerAds = IsPerAdsEps(existing.SourceConcept, existing.SourceType, existing.SourceLabel);
        if (candidatePerAds != existingPerAds)
            return candidatePerAds;

        var candidateRank = FinancialPointSelection.SourceRank(candidate.SourceType);
        var existingRank = FinancialPointSelection.SourceRank(existing.SourceType);
        if (candidateRank != existingRank)
            return candidateRank > existingRank;

        return FinancialPointSelection.ShouldReplaceEqualRank(
            candidate.SourcePrecision,
            candidate.Accn,
            existing.SourcePrecision,
            existing.Accession);
    }

    private static bool IsPerAdsEps(string? concept, string sourceType, string? sourceLabel) =>
        string.Equals(sourceType, "filing-table", StringComparison.OrdinalIgnoreCase)
        && IsEpsConcept(concept)
        && SecFilingStatementParser.IsPerAdsSourceLabel(sourceLabel);

    private static bool IsEpsConcept(string? concept)
    {
        if (string.IsNullOrWhiteSpace(concept)) return false;
        var parts = concept.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && EpsConceptNames.Contains(parts[^1]);
    }

    private static int? ParseQuarter(string? fp) =>
        fp is "Q1" or "Q2" or "Q3" or "Q4" ? fp[1] - '0' : null;
}
