using FilingForge.Models;

namespace FilingForge;

public static class FinancialPointMerger
{
    // A later filing whose value differs from the earlier one by more than this fraction is treated as
    // a real restatement (recorded as a distinct as-known version); smaller moves are identical
    // comparatives or sub-materiality reclassifications/rounding and are not segmented.
    private const double RestatementMaterialityFraction = 0.005;

    public static void MergePeriodFact(Dictionary<string, FinancialPoint> merged, FinancialPoint point) =>
        MergePeriodFact(merged, point, FinancialPointHelpers.FlowPeriodKey);

    public static void MergePeriodFact(
        Dictionary<string, FinancialPoint> merged,
        FinancialPoint point,
        Func<FinancialPoint, string> keySelector)
    {
        var key = keySelector(point);
        if (!merged.TryGetValue(key, out var existing))
        {
            merged[key] = point with { FirstFiled = point.FirstFiled ?? point.Filed };
            return;
        }

        // First-disclosure date for the period: earliest filing that reported it, across every
        // restatement/comparative seen for this key. Carried independently of the value selection
        // below (which keeps the latest restatement) so a FY-N 10-K's FY-N-1 comparative cannot drag
        // a period's as-of date forward and make a valuation series pair current prices with stale
        // earnings. The kept point always carries this earliest date regardless of which value wins.
        var firstFiled = EarliestFiled(
            existing.FirstFiled ?? existing.Filed,
            point.FirstFiled ?? point.Filed);

        // As-known value history across every filing seen for this period, independent of which value
        // wins below. Null when the period was never materially restated (the common case), so
        // non-restated periods and their caches are byte-identical to before.
        var versions = MergeVersions(existing, point);

        if (!FinancialPointSelection.ShouldReplace(point, existing))
        {
            merged[key] = FinancialPointSelection.SourceRank(point.SourceType) == FinancialPointSelection.SourceRank(existing.SourceType)
                ? existing with
                {
                    Fy = existing.Fy ?? point.Fy,
                    Fp = existing.Fp ?? point.Fp,
                    Frame = existing.Frame ?? point.Frame,
                    FirstFiled = firstFiled,
                    Versions = versions
                }
                : existing with { FirstFiled = firstFiled, Versions = versions };
            return;
        }

        merged[key] = point with
        {
            Fy = existing.Fy ?? point.Fy,
            Fp = existing.Fp ?? point.Fp,
            Frame = existing.Frame ?? point.Frame,
            FirstFiled = firstFiled,
            Versions = versions
        };
    }

    // Collapses every value this period was ever reported at (from both points, including any history
    // they already carry) into the ordered list of MATERIALLY distinct as-known versions. Each raw
    // fact contributes (FirstFiled ?? Filed, Val) — for an unmerged companyfacts fact that is its own
    // filing date, i.e. when the market saw that value. Sorted ascending by filing date; a value is
    // kept only when it moves more than the materiality threshold from the last kept value, so
    // identical comparative re-filings and rounding collapse away. Returns null when fewer than two
    // material versions survive (period was never truly restated).
    private static IReadOnlyList<FinancialPointVersion>? MergeVersions(FinancialPoint existing, FinancialPoint point)
    {
        var candidates = new List<FinancialPointVersion>();
        candidates.AddRange(VersionsOf(existing));
        candidates.AddRange(VersionsOf(point));
        candidates.Sort((a, b) => string.CompareOrdinal(a.Filed, b.Filed));

        var kept = new List<FinancialPointVersion>();
        foreach (var c in candidates)
        {
            if (kept.Count == 0 || IsMaterialChange(kept[^1].Val, c.Val))
                kept.Add(c);
        }

        return kept.Count >= 2 ? kept : null;
    }

    private static IEnumerable<FinancialPointVersion> VersionsOf(FinancialPoint p) =>
        p.Versions ?? [new(Filed: p.FirstFiled ?? p.Filed, Val: p.Val, Accn: p.Accn, Concept: p.Concept)];

    private static bool IsMaterialChange(double from, double to)
    {
        var denom = Math.Max(Math.Abs(from), 1e-9);
        return Math.Abs(to - from) / denom > RestatementMaterialityFraction;
    }

    // Earlier of two filing dates by ordinal (ISO yyyy-MM-dd) comparison; tolerates null/empty.
    private static string? EarliestFiled(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return string.CompareOrdinal(a, b) <= 0 ? a : b;
    }

    public static IReadOnlyList<FinancialPoint> MergeByConceptPriority(
        IEnumerable<string> concepts,
        Func<string, IEnumerable<FinancialPoint>> pointsFor,
        Func<FinancialPoint, string>? keySelector = null)
    {
        var merged = new Dictionary<string, FinancialPoint>();
        foreach (var concept in concepts)
        {
            var conceptMerged = new Dictionary<string, FinancialPoint>();
            foreach (var point in pointsFor(concept).OrderBy(p => p.Filed, StringComparer.Ordinal))
                MergePeriodFact(conceptMerged, point, keySelector ?? FinancialPointHelpers.FlowPeriodKey);

            foreach (var (key, point) in conceptMerged)
                if (!merged.ContainsKey(key))
                    merged[key] = point;
        }

        return [.. merged.Values];
    }

    public static IReadOnlyList<FinancialPoint> MergeByConceptPriority(
        IEnumerable<SecConceptSpec> concepts,
        Func<SecConceptSpec, IEnumerable<FinancialPoint>> pointsFor,
        Func<FinancialPoint, string>? keySelector = null)
    {
        var merged = new Dictionary<string, FinancialPoint>();
        foreach (var spec in concepts)
        {
            var conceptMerged = new Dictionary<string, FinancialPoint>();
            foreach (var point in pointsFor(spec).OrderBy(p => p.Filed, StringComparer.Ordinal))
                MergePeriodFact(conceptMerged, point, keySelector ?? FinancialPointHelpers.FlowPeriodKey);

            foreach (var (key, point) in conceptMerged)
                if (!merged.ContainsKey(key))
                    merged[key] = point;
        }

        return [.. merged.Values];
    }

    public static IReadOnlyList<FinancialPoint> MergeFlowPeriodFacts(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts) =>
        MergeByConceptPriority(concepts, inputs.PointsFor, FinancialPointHelpers.FlowPeriodKey);

    public static IReadOnlyList<FinancialPoint> MergePointFacts(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts) =>
        MergeByConceptPriority(concepts, inputs.PointsFor, p => p.End);

    public static IReadOnlyList<FinancialPoint> MergeSharePointFacts(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts) =>
        MergeSharePointFacts(concepts, inputs.PointsFor, p => p.End);

    public static IReadOnlyList<FinancialPoint> MergeSharePointFacts(
        IEnumerable<SecConceptSpec> concepts,
        Func<SecConceptSpec, IEnumerable<FinancialPoint>> pointsFor,
        Func<FinancialPoint, string>? keySelector = null)
    {
        Func<FinancialPoint, string> finalKeySelector = keySelector ?? (p => p.End);
        var merged = new Dictionary<string, FinancialPoint>();
        foreach (var spec in concepts)
        {
            var conceptMerged = new Dictionary<string, FinancialPoint>();
            foreach (var period in pointsFor(spec).GroupBy(p => $"{p.End}|{p.Form}", StringComparer.Ordinal))
            {
                var point = FinancialPointHelpers.ChooseSharePoint(period);
                if (point is not null)
                    MergePeriodFact(conceptMerged, point, finalKeySelector);
            }

            foreach (var (key, point) in conceptMerged)
                if (!merged.ContainsKey(key))
                    merged[key] = point;
        }

        return [.. merged.Values];
    }
}
