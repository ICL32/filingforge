using FilingForge.Models;

namespace FilingForge;

/// <summary>
/// The period ends a filer actually reports statements against, and the mapping that pulls
/// off-period facts onto them.
///
/// Two kinds of fact would otherwise each open their own export column for a quarter that already
/// has one, and two columns in one fiscal quarter necessarily share a label:
///   * transaction-dated facts — UNH reports PaymentsOfDividendsCommonStock against the dividend
///     payment date (2024-03-19, no start) beside the same value as a real
///     2024-01-01..2024-03-31 duration;
///   * the same fiscal quarter closed on different dates across filings — Deere ends fiscal 2015
///     on 2015-10-31 in one filing and 2015-11-01 in another, and filers assembled from a merger
///     carry both predecessors' week-end conventions 6-7 days apart.
///
/// Built once per ticker and shared across every concept: a balance-sheet concept has no duration
/// facts of its own to align against, so the set has to be taken across all of them.
/// </summary>
public sealed class StatementPeriodEnds
{
    // Distinct reported ends, ascending. Deduplicated so the nearest-end lookup can binary search
    // rather than scan one entry per duration fact.
    private readonly List<DateTime> _ends;

    // Reported end -> the end its cluster collapses onto.
    private readonly Dictionary<DateTime, DateTime> _canonical;

    // A start-less fact is pulled onto a real period end within this many days. Under half a
    // quarter, so a transaction date can only ever reach the period it actually falls in and the
    // nearest end always wins. Acquisition-dated balance-sheet facts (Cboe tags goodwill to
    // 2015-08-07) sit at the wide end of the range.
    private const double SnapWindowDays = 45;

    // Reported ends this close together are the same fiscal quarter under two conventions. An
    // order of magnitude below the ~91 days between real quarters.
    private const double ClusterDays = 10;

    private StatementPeriodEnds(List<DateTime> ends, Dictionary<DateTime, DateTime> canonical)
    {
        _ends = ends;
        _canonical = canonical;
    }

    public bool IsEmpty => _ends.Count == 0;

    public static StatementPeriodEnds Build(IEnumerable<FinancialPoint> points)
    {
        // Only duration facts state a reporting period; an instant fact is what we align.
        var counts = new Dictionary<DateTime, int>();
        foreach (var p in points)
            if (p.Start != null && DateTime.TryParse(p.End, out var end))
                counts[end] = counts.GetValueOrDefault(end) + 1;

        if (counts.Count == 0) return new StatementPeriodEnds([], []);

        var ends = counts.Keys.OrderBy(e => e).ToList();
        return new StatementPeriodEnds(ends, CanonicalEnds(ends, counts));
    }

    public List<FinancialPoint> Apply(List<FinancialPoint> points)
    {
        if (IsEmpty) return points;

        var result = new List<FinancialPoint>(points.Count);
        foreach (var point in points)
            result.Add(Align(point));
        return result;
    }

    private FinancialPoint Align(FinancialPoint point)
    {
        if (!DateTime.TryParse(point.End, out var end)) return point;

        var target = SnapTarget(point, end);
        if (target is null) return point;

        // A duration fact's Start moves with its End. Shifting only the End would change the
        // period's length by up to ClusterDays, which is enough to push a 104-day quarter past
        // IsQuarterPeriod's 105-day bound and drop the value entirely.
        if (point.Start is null || !DateTime.TryParse(point.Start, out var start))
            return point with { End = Format(target.Value) };

        var shift = (int)(target.Value - end).TotalDays;
        return point with { Start = Format(start.AddDays(shift)), End = Format(target.Value) };
    }

    /// <summary>
    /// The end this fact should be reported against, or null when it is already there.
    /// </summary>
    private DateTime? SnapTarget(FinancialPoint point, DateTime end)
    {
        // A duration fact already states its own period; only its end is normalised onto the
        // cluster's canonical date.
        if (point.Start != null)
        {
            if (!_canonical.TryGetValue(end, out var target) || target == end) return null;
            return target;
        }

        var nearest = NearestEnd(end);
        if (nearest is null) return null;

        var snapped = _canonical.TryGetValue(nearest.Value, out var canonical) ? canonical : nearest.Value;
        return snapped == end ? null : snapped;
    }

    // Nearest reported end within the snap window, by binary search over the distinct ends.
    private DateTime? NearestEnd(DateTime end)
    {
        var index = _ends.BinarySearch(end);
        if (index >= 0) return _ends[index];

        // Insertion point: only its two neighbours can be the nearest end.
        var next = ~index;
        var before = Candidate(next - 1, end);
        var after = Candidate(next, end);

        if (before is null) return after;
        if (after is null) return before;

        // Ties go to the later end, matching CanonicalEnds.
        return (end - before.Value).TotalDays >= (after.Value - end).TotalDays ? after : before;
    }

    private DateTime? Candidate(int index, DateTime end)
    {
        if (index < 0 || index >= _ends.Count) return null;
        return Math.Abs((_ends[index] - end).TotalDays) <= SnapWindowDays ? _ends[index] : null;
    }

    private static Dictionary<DateTime, DateTime> CanonicalEnds(
        List<DateTime> ends,
        Dictionary<DateTime, int> counts)
    {
        var map = new Dictionary<DateTime, DateTime>();
        var cluster = new List<DateTime> { ends[0] };

        void Flush()
        {
            // The date the filer reports most often wins; ties go to the later one.
            var winner = cluster.OrderByDescending(e => counts[e]).ThenByDescending(e => e).First();
            foreach (var end in cluster) map[end] = winner;
        }

        foreach (var end in ends.Skip(1))
        {
            if ((end - cluster[^1]).TotalDays <= ClusterDays)
            {
                cluster.Add(end);
                continue;
            }
            Flush();
            cluster = [end];
        }
        Flush();
        return map;
    }

    private static string Format(DateTime end) => end.ToString("yyyy-MM-dd");
}
