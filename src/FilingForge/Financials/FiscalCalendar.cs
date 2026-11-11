using FilingForge.Models;

namespace FilingForge;

/// <summary>
/// Fiscal-year/quarter labels derived from a filer's own fiscal year ends.
///
/// SEC companyfacts <c>fy</c>/<c>fp</c> describe the FILING a fact was taken from, not the
/// fact's own period: the prior-year comparative inside a FY2009 10-Q carries fy=2009/fp=Q3
/// even though it is fiscal Q3 of FY2008, and every fact lifted from a 10-K carries fp="FY"
/// with no quarter at all. Labelling off either one — or off the period-end calendar month —
/// mislabels every off-calendar filer (Apple's fiscal Q4 ends in September and was labelled
/// Q3, colliding with the real Q3).
///
/// This derives labels from period-end geometry alone:
///   1. Anchors = period ends of 12-month duration facts (the filer's fiscal year ends).
///   2. Fiscal year number = the first anchor's end year plus a fixed offset, then one step per
///      fiscal year the gap to the next anchor spans, so the numbers strictly increase. The
///      offset is the modal <c>fy - end.Year</c> over facts that are 12-month, fp="FY", and
///      filed 30-180 days after the end — i.e. the ORIGINAL 10-K for that year, never a later
///      comparative. One offset for the whole filer keeps filers that name a fiscal year after
///      its start year self-consistent across the series.
///   3. Quarter = the period end's position inside its anchor window, by day spacing, so
///      52/53-week ends that drift across calendar quarters still land on the right quarter.
///
/// Anchors are extended forward and backward by the observed year length so periods outside
/// the filed 10-K range (the in-progress fiscal year, pre-XBRL comparatives) still label.
/// </summary>
public sealed class FiscalCalendar
{
    // A fiscal year end and the fiscal year number it belongs to.
    private readonly List<(DateTime End, int Year)> _anchors;

    private FiscalCalendar(List<(DateTime End, int Year)> anchors) => _anchors = anchors;

    public bool IsEmpty => _anchors.Count == 0;

    public static FiscalCalendar Build(IEnumerable<FinancialPoint> points)
    {
        var all = points.ToList();

        // Count facts per candidate year end so a cluster of near-identical ends collapses
        // onto the date the filer actually reports most often.
        var counts = new Dictionary<DateTime, int>();
        var offsets = new Dictionary<int, int>();

        foreach (var p in all)
        {
            if (!IsAnnualDuration(p, out var end)) continue;
            counts[end] = counts.GetValueOrDefault(end) + 1;

            if (p.Fp != "FY" || !p.Fy.HasValue || !DateTime.TryParse(p.Filed, out var filed)) continue;
            var lag = (filed - end).TotalDays;
            if (lag is < 30 or > 180) continue;
            var offset = p.Fy.Value - end.Year;
            offsets[offset] = offsets.GetValueOrDefault(offset) + 1;
        }

        if (counts.Count == 0) return new FiscalCalendar([]);

        var yearOffset = offsets.Count == 0
            ? 0
            : offsets.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;

        var ends = CollapseNearbyEnds(counts);
        var anchors = FillMissingYears(AssignFiscalYears(ends, yearOffset));
        return new FiscalCalendar(anchors);
    }

    /// <summary>
    /// Fiscal year and quarter for a period end, or null when no usable anchor window exists
    /// (too few fiscal year ends, or a window whose length is not a plausible fiscal year).
    /// </summary>
    public (int FiscalYear, int FiscalQuarter)? Label(string? end)
    {
        if (_anchors.Count == 0 || !DateTime.TryParse(end, out var date)) return null;

        var (nextEnd, nextYear) = AnchorAtOrAfter(date);
        if (nextEnd == default) return null;

        var (previous, previousProjected) = PreviousAnchorEnd(nextEnd);
        var window = (nextEnd - previous).TotalDays;
        if (window is < 300 or > 460) return null;

        if (date == nextEnd) return (nextYear, 4);

        // Sitting ON a PROJECTED window's opening edge means this is the prior fiscal year's
        // close: the projection is arithmetic and can land a day or two off the real year end
        // (Apple's 2006-09-30). A real filed anchor needs no such tolerance — a date days after a
        // genuine year end is that new year's Q1, not the old year's Q4.
        if (previousProjected && (date - previous).TotalDays <= 10) return (nextYear - 1, 4);

        var quarter = (int)Math.Round((date - previous).TotalDays / (window / 4.0), MidpointRounding.AwayFromZero);
        return (nextYear, Math.Clamp(quarter, 1, 4));
    }

    public static string Format(int fiscalYear, int fiscalQuarter) =>
        $"Q{fiscalQuarter}'{fiscalYear % 100:00}";

    // Fiscal year numbers must strictly increase along the anchors. Deriving each one from its own
    // end year breaks when a filer's year ends drift across a calendar boundary (a year end in
    // early January followed by one in late December lands both in the same calendar year), which
    // hands two anchors the same number and so two fiscal years the same labels.
    //
    // Each anchor's own end year is still preferred whenever it is available and agrees with the
    // sequence, so a single odd gap cannot shift every later label; the running sequence is only
    // used where the end year would break monotonicity or disagree by more than a year.
    private static List<(DateTime End, int Year)> AssignFiscalYears(List<DateTime> ends, int yearOffset)
    {
        var anchors = new List<(DateTime End, int Year)> { (ends[0], ends[0].Year + yearOffset) };
        for (var i = 1; i < ends.Count; i++)
        {
            var span = (ends[i] - ends[i - 1]).TotalDays;
            var years = Math.Max(1, (int)Math.Round(span / 365.25, MidpointRounding.AwayFromZero));
            var sequential = anchors[^1].Year + years;
            var reported = ends[i].Year + yearOffset;
            // Only a consecutive year end may fall back on its own reported year. Across a gap the
            // reported year can sit below the sequence, and taking it would collapse a multi-year
            // gap to one year — leaving FillMissingYears nothing to interpolate and the window too
            // long for Label to resolve, so every quarter inside it would go unlabelled.
            var usable = years == 1
                && reported > anchors[^1].Year
                && Math.Abs(reported - sequential) <= 1;
            anchors.Add((ends[i], usable ? reported : sequential));
        }
        return anchors;
    }

    // A filer can be missing an annual fact for one year (Microsoft has no fiscal 2007 annual in
    // companyfacts), leaving a two-year gap between anchors. Quarters inside that gap would sit in
    // a window too long to be a fiscal year and would go unlabelled, so the missing year ends are
    // projected in at the filer's own year length.
    private static List<(DateTime End, int Year)> FillMissingYears(List<(DateTime End, int Year)> anchors)
    {
        if (anchors.Count < 2) return anchors;

        var filled = new List<(DateTime End, int Year)> { anchors[0] };
        for (var i = 1; i < anchors.Count; i++)
        {
            var previous = anchors[i - 1];
            var current = anchors[i];
            var years = current.Year - previous.Year;
            // Only interpolate whole missing years, and only across a short gap: beyond a few
            // years the projected ends are guesses, and a filer absent that long is more likely
            // to have changed its fiscal calendar than to have simply skipped filings.
            if (years > 1 && years <= 6)
            {
                var span = (current.End - previous.End).TotalDays / years;
                if (span is >= 300 and <= 460)
                    for (var step = 1; step < years; step++)
                        filled.Add((previous.End.AddDays(span * step), previous.Year + step));
            }
            filled.Add(current);
        }
        return filled;
    }

    // The filer's own year length, used to project anchors past the filed range.
    private double YearStep()
    {
        if (_anchors.Count < 2) return 364;
        var gaps = new List<double>();
        for (var i = 1; i < _anchors.Count; i++)
        {
            var gap = (_anchors[i].End - _anchors[i - 1].End).TotalDays;
            if (gap is >= 300 and <= 460) gaps.Add(gap);
        }
        if (gaps.Count == 0) return 364;
        // Median, so a single odd gap cannot set the step. 52/53-week filers alternate ~364/371,
        // and the list is in anchor order, so it must be sorted before indexing the middle.
        gaps.Sort();
        return gaps[gaps.Count / 2];
    }

    // First anchor at or after the date, projecting forward past the last filed 10-K so
    // quarters of the in-progress fiscal year still label.
    private (DateTime End, int Year) AnchorAtOrAfter(DateTime date)
    {
        // Before the earliest filed annual: project backward. Comparatives that predate the first
        // 10-K in companyfacts sit here — Deere and Diamondback carry a decade of balance-sheet
        // periods before their first annual — and every one of them otherwise resolved to the
        // earliest anchor and clamped onto a single label.
        // This must be checked before the forward scan, which would return that first anchor.
        if (date <= _anchors[0].End)
        {
            var backStep = YearStep();
            var backEnd = _anchors[0].End;
            var backYear = _anchors[0].Year;
            for (var i = 0; i < 40 && backEnd.AddDays(-backStep) >= date; i++)
            {
                backEnd = backEnd.AddDays(-backStep);
                backYear--;
            }
            return backEnd.AddDays(-backStep) < date ? (backEnd, backYear) : default;
        }

        foreach (var anchor in _anchors)
            if (anchor.End >= date) return anchor;

        var last = _anchors[^1];
        var step = YearStep();
        var end = last.End;
        var year = last.Year;
        // Bounded: a period more than a few years past the last filing is not projected.
        for (var i = 0; i < 6 && end < date; i++)
        {
            end = end.AddDays(step);
            year++;
        }
        return end >= date ? (end, year) : default;
    }

    // The anchor before this one, projecting backward for pre-XBRL comparatives that predate
    // the earliest filed 10-K.
    private (DateTime End, bool Projected) PreviousAnchorEnd(DateTime anchorEnd)
    {
        DateTime? previous = null;
        foreach (var anchor in _anchors)
            if (anchor.End < anchorEnd) previous = anchor.End;

        return previous is null
            ? (anchorEnd.AddDays(-YearStep()), true)
            : (previous.Value, false);
    }

    // Must be a 12-month period AND an annual statement fact. A rolling twelve-month fact filed on
    // a 10-Q is not a fiscal year end, and admitting one plants a spurious anchor that shifts every
    // quarter in the surrounding window (Deere's fiscal 2015 quarters each came out one late).
    private static bool IsAnnualDuration(FinancialPoint p, out DateTime end)
    {
        end = default;
        if (p.Start is null
            || !FinancialPointHelpers.IsAnnualFlowFact(p)
            || !DateTime.TryParse(p.Start, out var start)
            || !DateTime.TryParse(p.End, out var parsed))
            return false;

        var days = (parsed - start).TotalDays;
        if (days is < 340 or > 400) return false;
        end = parsed;
        return true;
    }

    // Two concepts in one 10-K can report year ends a few days apart (52/53-week filers tag
    // some schedules to a month end). Keep one end per fiscal year: the most-reported date.
    private static List<DateTime> CollapseNearbyEnds(Dictionary<DateTime, int> counts)
    {
        var ordered = counts.Keys.OrderBy(e => e).ToList();
        var result = new List<DateTime>();
        var cluster = new List<DateTime> { ordered[0] };

        foreach (var end in ordered.Skip(1))
        {
            if ((end - cluster[^1]).TotalDays < 300)
            {
                cluster.Add(end);
                continue;
            }
            result.Add(BestOf(cluster, counts));
            cluster = [end];
        }
        result.Add(BestOf(cluster, counts));
        return result;
    }

    private static DateTime BestOf(List<DateTime> cluster, Dictionary<DateTime, int> counts) =>
        cluster
            .OrderByDescending(e => counts[e])
            .ThenByDescending(e => e)
            .First();
}
