using FilingForge.Models;

namespace FilingForge;

// Single home for the as-of alignment and fundamental-derivation math shared by the production
// snapshot (AnalysisOrchestrator) and the diagnostic trace (AnalysisTraceBuilder). Both pipelines
// must compute per-share / ROE / current-ratio / margin alignment identically; before this existed
// the two reimplemented the rules and drifted (the AMZN margin bug lived in exactly that gap).
//
// The rules live here once, expressed as pure functions over date accessors so the same code serves
// FinancialPoint, TtmPoint, SharePoint, and the trace's TtmTracePoint without copying arithmetic.
public static class ValuationCore
{
    // A fundamental this far past its filing/period is stale: pairing it with a current price would
    // publish a misleading trailing multiple. One constant, one home — ValuationSeriesBuilder (price
    // multiples) and FundamentalsBuilder (cover-share staleness) both reference it. The window spans
    // more than a year so a normal quarterly cadence never trips it; it only fires on a reporting
    // gap/halt/delisting.
    public const int MaxFundamentalStaleDays = 400;

    // ---- as-of date accessors ------------------------------------------------------------------
    // The market learns a fact on its filing date, so alignment keys on Filed and falls back to the
    // period End only for facts/snapshots that predate the Filed field.

    public static string FiledOrEnd(FinancialPoint point) =>
        string.IsNullOrWhiteSpace(point.Filed) ? point.End : point.Filed;

    public static string FiledOrEnd(SharePoint point) =>
        string.IsNullOrWhiteSpace(point.Filed) ? point.End : point.Filed;

    public static string FiledOrEnd(TtmPoint point) =>
        string.IsNullOrWhiteSpace(point.Filed) ? point.End : point.Filed;

    // As-of date for valuation alignment: the date the market FIRST saw the period. Prefers
    // FirstFiled (earliest disclosure), then Filed (latest restatement), then the period End for
    // facts/snapshots predating FirstFiled. Using this instead of FiledOrEnd keeps a later
    // comparative re-filing from re-dating a period and pairing current prices with stale earnings.
    public static string AsOfDate(TtmPoint point) =>
        !string.IsNullOrWhiteSpace(point.FirstFiled) ? point.FirstFiled : FiledOrEnd(point);

    public static string AsOfDate(SharePoint point) =>
        !string.IsNullOrWhiteSpace(point.FirstFiled) ? point.FirstFiled : FiledOrEnd(point);

    public static string AsOfDate(FinancialPoint point) =>
        !string.IsNullOrWhiteSpace(point.FirstFiled) ? point.FirstFiled : FiledOrEnd(point);

    // Latest of a set of ISO (yyyy-MM-dd) dates by ordinal comparison. Used to date a reconstructed
    // value at the moment all of its inputs were public.
    public static string MaxDate(IEnumerable<string> dates) =>
        dates.OrderBy(d => d, StringComparer.Ordinal).Last();

    // Later of two ISO dates, null-tolerant (returns the non-null one, or null when both are null).
    public static string? LaterDate(string? a, string? b) => (a, b) switch
    {
        (null, null) => null,
        (var x, null) => x,
        (null, var y) => y,
        var (x, y) => string.CompareOrdinal(x, y) >= 0 ? x : y
    };

    // ---- closest-on-or-before-date selection ---------------------------------------------------

    // Last element whose key is <= date; ties resolve to the latest element in sorted order. Binary
    // search over a list the caller has already sorted by the same key.
    public static T? ClosestByKey<T>(IReadOnlyList<T> sortedByKey, string date, Func<T, string> key)
        where T : class
    {
        T? result = null;
        var lo = 0;
        var hi = sortedByKey.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (string.Compare(key(sortedByKey[mid]), date, StringComparison.Ordinal) <= 0)
            {
                result = sortedByKey[mid];
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return result;
    }

    // Latest share fact filed on or before the as-of date. Sorts by filed-or-end then End so ties on
    // the filing date resolve to the later fiscal period, matching the original linear scans.
    public static SharePoint? ClosestShareByFiled(IEnumerable<SharePoint> points, string date)
    {
        var sorted = points
            .OrderBy(AsOfDate, StringComparer.Ordinal)
            .ThenBy(p => p.End, StringComparer.Ordinal)
            .ToList();
        return ClosestByKey(sorted, date, AsOfDate);
    }

    // ---- FinancialPoint period selection -------------------------------------------------------

    // Latest point by fiscal period end, then by filed date for same-period restatements.
    public static FinancialPoint? LatestByEnd(IReadOnlyList<FinancialPoint> points) =>
        points.Count > 0
            ? points
                .OrderBy(p => p.End, StringComparer.Ordinal)
                .ThenBy(p => p.Filed, StringComparer.Ordinal)
                .Last()
            : null;

    // Latest filed restatement of a specific fiscal period end.
    public static FinancialPoint? LatestByEnd(IReadOnlyList<FinancialPoint> points, string end) =>
        points
            .Where(p => string.Equals(p.End, end, StringComparison.Ordinal))
            .OrderBy(p => p.Filed, StringComparer.Ordinal)
            .LastOrDefault();

    // The latest fiscal period end common to both series, with each side's latest restatement for
    // that end. Used for current ratio so assets and liabilities come from the same balance sheet.
    public static (FinancialPoint? Left, FinancialPoint? Right) LatestCommonByEnd(
        IReadOnlyList<FinancialPoint> left,
        IReadOnlyList<FinancialPoint> right)
    {
        var rightEnds = right.Select(p => p.End).ToHashSet(StringComparer.Ordinal);
        var end = left
            .Select(p => p.End)
            .Where(rightEnds.Contains)
            .OrderBy(e => e, StringComparer.Ordinal)
            .LastOrDefault();

        return string.IsNullOrWhiteSpace(end)
            ? (null, null)
            : (LatestByEnd(left, end), LatestByEnd(right, end));
    }

    // ---- derivation rules (where margin/ROE bugs hid) ------------------------------------------
    // Each rule owns its denominator/sign guard once. Cost-like denominators (shares, liabilities)
    // must be strictly positive; equity may be negative so ROE only rejects an exact zero.

    public static double? CurrentRatio(FinancialPoint? currentAssets, FinancialPoint? currentLiabilities) =>
        currentAssets is not null && currentLiabilities is { Val: > 0 }
            ? currentAssets.Val / currentLiabilities.Val
            : null;

    public static double? PerShare(double? value, SharePoint? share) =>
        value is not null && share is { Val: > 0 }
            ? value.Value / share.Val
            : null;

    public static double? Roe(double? ttmNetIncome, FinancialPoint? equity) =>
        ttmNetIncome is not null && equity is not null && equity.Val != 0
            ? ttmNetIncome.Value / equity.Val * 100.0
            : null;

    // Gross/operating margin numerators may only pair with revenue from the SAME TTM period end, so a
    // stale gross-profit/operating-income fact cannot mint a margin against newer revenue.
    public static bool MarginPeriodsAligned(string? numeratorEnd, string? revenueEnd) =>
        !string.IsNullOrEmpty(numeratorEnd)
        && string.Equals(numeratorEnd, revenueEnd, StringComparison.Ordinal);
}
