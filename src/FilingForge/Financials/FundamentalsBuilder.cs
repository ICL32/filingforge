using Microsoft.Extensions.Logging;
using FilingForge.Models;
using static FilingForge.SecCompanyFactsReader;

namespace FilingForge;

internal static class FundamentalsBuilder
{
    private static readonly IReadOnlyList<SecConceptSpec> EpsConcepts =
    [
        FinancialMetricsCatalog.UsGaap("EarningsPerShareDiluted", "USD/shares"),
        FinancialMetricsCatalog.Ifrs("DilutedEarningsLossPerShare", "USD/shares"),
        FinancialMetricsCatalog.UsGaap("EarningsPerShareBasic", "USD/shares"),
        FinancialMetricsCatalog.Ifrs("BasicEarningsLossPerShare", "USD/shares"),
        FinancialMetricsCatalog.Ifrs("DilutedEarningsLossPerShareFromContinuingOperations", "USD/shares"),
        FinancialMetricsCatalog.Ifrs("BasicEarningsLossPerShareFromContinuingOperations", "USD/shares")
    ];

    private static readonly IReadOnlyList<SecConceptSpec> CurrentAssetsConcepts =
    [
        FinancialMetricsCatalog.UsGaap("AssetsCurrent"),
        FinancialMetricsCatalog.Ifrs("CurrentAssets")
    ];

    private static readonly IReadOnlyList<SecConceptSpec> CurrentLiabilitiesConcepts =
    [
        FinancialMetricsCatalog.UsGaap("LiabilitiesCurrent"),
        FinancialMetricsCatalog.Ifrs("CurrentLiabilities")
    ];

    // Valuation / per-share net income (P/E, EPS readout, ROE) uses net income attributable to the
    // PARENT — what common shareholders actually earn — preferring the parent concept over
    // consolidated ProfitLoss, which also includes the noncontrolling-interest (minority) slice of a
    // partially-owned subsidiary's earnings. The export "Net Income" statement line keeps the
    // consolidated-first ordering (FinancialMetricsCatalog.NetIncome) for faithful statement
    // reproduction; only the fundamentals valuation numerator uses this. For single-class
    // filers without noncontrolling interests the two are identical. Reconstruction (cumulative ->
    // standalone quarter, TTM) is unchanged — this only reorders the source-concept priority.
    // NetIncomeLossAvailableToCommonStockholdersBasic is a different basis (already net of preferred /
    // participating-securities allocation), kept only as a last-resort fallback for filers that tag
    // neither parent NetIncomeLoss nor consolidated ProfitLoss, so per-share metrics fall back to a
    // value rather than blanking; it is never reached when either of the first two concepts exists.
    internal static readonly IReadOnlyList<SecConceptSpec> ValuationNetIncomeConcepts =
    [
        FinancialMetricsCatalog.UsGaap("NetIncomeLoss"),
        FinancialMetricsCatalog.UsGaap("ProfitLoss"),
        FinancialMetricsCatalog.Ifrs("ProfitLossAttributableToOwnersOfParent"),
        FinancialMetricsCatalog.Ifrs("ProfitLossAttributableToOrdinaryEquityHoldersOfParentEntity"),
        FinancialMetricsCatalog.Ifrs("ProfitLoss"),
        FinancialMetricsCatalog.UsGaap("NetIncomeLossAvailableToCommonStockholdersBasic")
    ];

    private static readonly IReadOnlyList<SecConceptSpec> EquityConcepts =
    [
        FinancialMetricsCatalog.UsGaap("StockholdersEquity"),
        FinancialMetricsCatalog.UsGaap("StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"),
        FinancialMetricsCatalog.Ifrs("Equity"),
        FinancialMetricsCatalog.Ifrs("EquityAttributableToOwnersOfParent")
    ];

    internal static Fundamentals Empty { get; } = new([], [], [], [], [], [], [], [], [], [], [], []);

    /// <summary>
    /// Builds the valuation inputs for one company. When <paramref name="filingSource"/> is given,
    /// recent filings are read to fill statement and 6-K EPS gaps left by the XBRL feed.
    /// </summary>
    internal static async Task<Fundamentals> BuildAsync(
        string sym,
        CompanyFacts facts,
        ISecFilingDocumentSource? filingSource,
        ILogger? logger)
    {
        var eps = ReadFactsByPriority(facts, EpsConcepts);
        IReadOnlyList<FinancialPoint>? recentFilingFacts = null;
        if (filingSource is not null)
        {
            recentFilingFacts = (await SecFilingFallbackService.FetchMissingRecentFilingFactsForFundamentalsAsync(
                sym, filingSource, facts, eps, logger)).Facts;
            SecFilingFallbackService.AddMissingFilingEps(eps, recentFilingFacts);
        }

        var shares = ReadSharesOutstanding(facts);
        var inputs = await FinancialMetricInputBuilder.BuildAsync(sym, facts, filingSource, recentFilingFacts, logger);
        // Prefer direct SEC facts for margin numerators. Fall back to the metric-derived path
        // (e.g. RevenueMinusCogs gross profit, RevenueMinusTotalOperatingCost EBIT) only when
        // companyfacts lacks a fact for the latest revenue period — e.g. AMZN, whose only direct
        // GrossProfit fact is stale (2009). The shared flow-metric build runs at most once and only
        // when one of these numerators actually needs it; it is the same derivation the export uses,
        // so fundamentals and the metrics export cannot disagree on a derived gross profit / EBIT.
        var grossProfit = FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.GrossProfit);
        var operatingIncome = FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.Ebit);
        var grossProfitNeedsDerived = NeedsDerivedFlow(grossProfit, inputs.RevenuePoints);
        var operatingIncomeNeedsDerived = NeedsDerivedFlow(operatingIncome, inputs.RevenuePoints);
        if (grossProfitNeedsDerived || operatingIncomeNeedsDerived)
        {
            var flow = FinancialMetricsBuilder.BuildFlowMetricSeries(sym, inputs);
            if (grossProfitNeedsDerived)
            {
                var derivedGrossProfit = flow.Points(FinancialMetricsCatalog.GrossProfit);
                if (derivedGrossProfit.Count > 0)
                    grossProfit = derivedGrossProfit;
            }
            if (operatingIncomeNeedsDerived)
            {
                var derivedOperatingIncome = flow.Points(FinancialMetricsCatalog.Ebit);
                if (derivedOperatingIncome.Count > 0)
                    operatingIncome = derivedOperatingIncome;
            }
        }
        var operatingCashFlow = FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.OperatingCashFlow);
        var capex = FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.Capex);
        // Parent-attributable net income for valuation/per-share (see ValuationNetIncomeConcepts);
        // the consolidated export "Net Income" line is built separately by FinancialMetricsBuilder.
        var netIncome = FinancialMetricInputBuilder.PointsForAnyProvider(inputs, ValuationNetIncomeConcepts);
        var rd = FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.Rnd);

        // Period ends that carry a valuation numerator (earnings / revenue / EPS). A weighted-average
        // share point is only a usable denominator where one of these exists; retaining a share count
        // for any other period would let a stale orphan leak pair with a price bar.
        var valuationEnds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var point in netIncome) valuationEnds.Add(point.End);
        foreach (var point in inputs.RevenuePoints) valuationEnds.Add(point.End);
        foreach (var point in eps) valuationEnds.Add(point.End);

        shares = await AugmentSharesWithCoverFallbackAsync(
            sym, shares, valuationEnds, LatestPeriodEnd(netIncome, inputs.RevenuePoints, eps), filingSource, logger);

        var currentAssets = ReadPointFactsByPriority(facts, CurrentAssetsConcepts);
        var currentLiabilities = ReadPointFactsByPriority(facts, CurrentLiabilitiesConcepts);

        var equityMerged = new Dictionary<string, FinancialPoint>();
        foreach (var p in ReadPointFactsByPriority(facts, EquityConcepts)
                           .OrderBy(p => p.Filed))
        {
            var key = $"{p.End}|{p.Form}";
            if (!equityMerged.ContainsKey(key))
                equityMerged[key] = p;
        }

        return new Fundamentals(
            Eps: eps,
            RevenuePoints: [.. inputs.RevenuePoints],
            SharesOutstanding: shares,
            GrossProfitPoints: grossProfit,
            OperatingIncomePoints: operatingIncome,
            OperatingCashFlowPoints: operatingCashFlow,
            CapexPoints: capex,
            NetIncomeLossPoints: netIncome,
            RdExpensePoints: rd,
            CurrentAssetsPoints: currentAssets,
            CurrentLiabilitiesPoints: currentLiabilities,
            EquityPoints: [.. equityMerged.Values]);
    }

    // True when a direct flow series has no fact for the latest revenue period, so the
    // metric-derived value (e.g. RevenueMinusCogs gross profit, RevenueMinusTotalOperatingCost
    // EBIT) from the full metric build is required to keep the margin numerator current.
    private static bool NeedsDerivedFlow(
        IReadOnlyList<FinancialPoint> directPoints,
        IReadOnlyList<FinancialPoint> revenuePoints)
    {
        if (revenuePoints.Count == 0)
            return false;

        var latestRevenueEnd = revenuePoints
            .Select(p => p.End)
            .Where(end => !string.IsNullOrEmpty(end))
            .DefaultIfEmpty(null)
            .Max(StringComparer.Ordinal);
        if (latestRevenueEnd is null)
            return false;

        return !directPoints.Any(p => string.Equals(p.End, latestRevenueEnd, StringComparison.Ordinal));
    }

    // Point-in-time outstanding-share concepts (as stored by SourceName). Weighted-average
    // and issued-share facts are intentionally excluded: they are valuation fallbacks, not
    // true outstanding counts.
    private static readonly IReadOnlySet<string> OutstandingShareConcepts = new HashSet<string>(StringComparer.Ordinal)
    {
        "CommonStockSharesOutstanding",
        "dei:EntityCommonStockSharesOutstanding",
        "ifrs-full:NumberOfSharesOutstanding"
    };

    // Cover wins over companyfacts only when it exceeds the companyfacts outstanding total by
    // more than this fraction, so single-class / fully-tagged filers (AAPL, GOOGL) keep
    // companyfacts and pay no second fetch. A class dropped by companyfacts dimensioning is
    // material (HEI: ~55M of ~138M), far beyond this tolerance.
    private const double CoverReconcileTolerance = 0.005;

    // Reconcile the companyfacts outstanding-share total against the filing cover page for
    // domestic periodic filers. Two stages keep the common path cheap: stage 1 fetches only the
    // latest cover total (one primary doc) to decide; stage 2 fetches the cover history (for
    // percentile levels) only when cover is preferred.
    //
    // Cover is preferred when companyfacts has no recent point-in-time outstanding fact (fully
    // share-class-dimensioned dual-class filers whose facts companyfacts drops entirely — e.g.
    // ERIE, BRK-B), OR when the latest cover total materially exceeds the companyfacts total
    // (partial dimensioning, where companyfacts retains only some classes — e.g. HEI). Cover
    // facts then supersede any companyfacts fallback leak (a stale per-class weighted-average)
    // that would otherwise poison per-share valuation.
    internal static async Task<List<FinancialPoint>> AugmentSharesWithCoverFallbackAsync(
        string sym,
        List<FinancialPoint> shares,
        IReadOnlySet<string> valuationEnds,
        string? latestIncomeEnd,
        ISecFilingDocumentSource? filingSource,
        ILogger? logger)
    {
        if (filingSource is null)
            return shares;

        var hasRecent = HasRecentOutstandingShare(shares, latestIncomeEnd);

        // Stage 1: latest cover total only (one primary-doc fetch; foreign filers -> empty).
        FinancialPoint? latestCover;
        try
        {
            latestCover = (await filingSource.GetRecentCoverShareFactsAsync(sym, maxFilings: 1))
                .OrderBy(point => point.End, StringComparer.Ordinal)
                .LastOrDefault();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Cover-page share reconciliation (stage 1) failed for {Ticker}", sym);
            return shares;
        }

        var cfTotal = LatestOutstandingTotal(shares);
        var coverExceedsCompanyfacts = latestCover is not null
            && cfTotal is > 0
            && latestCover.Val > cfTotal.Value * (1 + CoverReconcileTolerance);
        var preferCover = !hasRecent || coverExceedsCompanyfacts;

        if (!preferCover)
            return shares;

        // Stage 2: cover history so percentile levels rebuild (reuses the stage-1 latest doc via
        // the disk + in-process cache).
        IReadOnlyList<FinancialPoint> coverFacts;
        try
        {
            coverFacts = await filingSource.GetRecentCoverShareFactsAsync(sym);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Cover-page share reconciliation (stage 2) failed for {Ticker}", sym);
            return shares;
        }

        if (coverFacts.Count == 0)
            return shares;

        // Dedup cover points by period-end (latest filed wins) so a 10-K + 10-K/A for the same
        // period do not double-represent it in the percentile series.
        var dedupedCover = coverFacts
            .GroupBy(point => point.End, StringComparer.Ordinal)
            .Select(group => group.OrderBy(point => point.Filed, StringComparer.Ordinal).Last())
            .ToList();

        logger?.LogInformation(
            "Cover-page share reconciliation for {Ticker}: {Count} cover facts (companyfacts total {CfTotal}, cover latest {CoverTotal})",
            sym,
            dedupedCover.Count,
            cfTotal,
            latestCover?.Val);

        // The pre-cover share series can supply the older ends cover does not reach (a decade of
        // otherwise-blank P/E for share-class-dimensioned filers whose only history is the
        // non-dimensional weighted-average total — META, CMCSA, FOXA, NWSA). Retain it ONLY when it
        // reconciles with cover, proving it represents the full share base. This drops a partial
        // share-class fact (HEI's single retained class, far below cover) and a stale/garbage leak
        // (ERIE's 2542, FOXA's dei val=1) — neither reconciles — so a wrong denominator can never
        // poison per-share, while a few stale outstanding facts alongside a good full weighted-average
        // series no longer discard that series (the previous "any outstanding fact" test did, blanking
        // CMCSA/FOXA/NWSA history).
        var coverByEnd = dedupedCover
            .GroupBy(cover => cover.End, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(cover => cover.Filed, StringComparer.Ordinal).Last(), StringComparer.Ordinal);

        // Reconcile at the latest period end the two series SHARE (same-date, apples-to-apples), so the
        // decision is not distorted by comparing a fallback value and a cover value from different dates
        // (share counts drift with buybacks/issuance). Fall back to latest-of-each only when they never
        // overlap.
        var (reconcileFallback, reconcileCover) = SelectReconcilePair(shares, coverByEnd, latestCover);
        if (!ShareFallbackReconcilesWithCover(reconcileFallback, reconcileCover))
            return dedupedCover;

        return CombineCoverWithShareFallback(dedupedCover, shares, valuationEnds);
    }

    // Picks the (fallback, cover) point pair to reconcile: the fallback point at the latest period end
    // also present in cover (same-date comparison), else the latest positive fallback against the
    // latest cover.
    internal static (FinancialPoint? Fallback, FinancialPoint? Cover) SelectReconcilePair(
        IReadOnlyList<FinancialPoint> fallbackShares,
        IReadOnlyDictionary<string, FinancialPoint> coverByEnd,
        FinancialPoint? latestCover)
    {
        var overlap = fallbackShares
            .Where(point => point.Val > 0 && coverByEnd.ContainsKey(point.End))
            .OrderBy(point => point.End, StringComparer.Ordinal)
            .LastOrDefault();
        if (overlap is not null)
            return (overlap, coverByEnd[overlap.End]);

        var latestFallback = fallbackShares
            .Where(point => point.Val > 0)
            .OrderBy(point => point.End, StringComparer.Ordinal)
            .LastOrDefault();
        return (latestFallback, latestCover);
    }

    // The weighted-average fallback reconciles with cover when its latest value is within tolerance of
    // the latest cover total — i.e. it is the full share base, not a partial class or a stale leak.
    // Diluted weighted-average sits slightly above point-in-time outstanding (dilutive securities), so
    // the band is symmetric and generous; a partial class (~0.4x) or garbage leak falls well outside.
    private const double ShareFallbackReconcileTolerance = 0.2;

    internal static bool ShareFallbackReconcilesWithCover(FinancialPoint? latestFallback, FinancialPoint? latestCover)
    {
        if (latestFallback is null || latestCover is null || latestCover.Val <= 0)
            return false;

        var ratio = latestFallback.Val / latestCover.Val;
        return ratio >= 1 - ShareFallbackReconcileTolerance
            && ratio <= 1 + ShareFallbackReconcileTolerance;
    }

    // Keeps every cover point (authoritative, recent) and appends the prior share-series points for
    // period ends cover does not cover and that carry a valuation numerator, deduped to the latest-
    // filed fact per end. Called only after the prior series' latest value reconciles with the cover
    // total (ShareFallbackReconcilesWithCover), so it represents the full share base and cannot leak a
    // partial-class count into the denominator.
    internal static List<FinancialPoint> CombineCoverWithShareFallback(
        IReadOnlyList<FinancialPoint> dedupedCover,
        IReadOnlyList<FinancialPoint> priorShares,
        IReadOnlySet<string> valuationEnds)
    {
        var coverEnds = new HashSet<string>(dedupedCover.Select(c => c.End), StringComparer.Ordinal);
        var result = new List<FinancialPoint>(dedupedCover);
        result.AddRange(priorShares
            .Where(point => !coverEnds.Contains(point.End) && valuationEnds.Contains(point.End))
            .GroupBy(point => point.End, StringComparer.Ordinal)
            .Select(group => group.OrderBy(point => point.Filed, StringComparer.Ordinal).Last()));
        return result;
    }

    // The companyfacts outstanding-share total at the latest period: the outstanding-concept
    // share point with the greatest End (for partial dimensioning this is the single retained
    // class). Weighted-average / issued leaks are excluded.
    private static double? LatestOutstandingTotal(IReadOnlyList<FinancialPoint> shares)
    {
        FinancialPoint? latest = null;
        foreach (var point in shares)
        {
            if (!IsOutstandingShareConcept(point.Concept))
                continue;
            if (latest is null || string.CompareOrdinal(point.End, latest.End) > 0)
                latest = point;
        }

        return latest?.Val;
    }

    private static bool IsOutstandingShareConcept(string? concept) =>
        concept is not null && OutstandingShareConcepts.Contains(concept);

    // An outstanding-share fact counts as "recent" if it is within the staleness window of the
    // most recent data we have. The anchor is the latest of the income-period end and the newest
    // share fact's end, so a newer non-outstanding fallback fact (e.g. a fresh weighted-average)
    // correctly marks an older outstanding fact as stale even when income periods are absent.
    private static bool HasRecentOutstandingShare(IEnumerable<FinancialPoint> shares, string? latestIncomeEnd)
    {
        var shareList = shares as IReadOnlyList<FinancialPoint> ?? shares.ToList();
        var anchor = MaxOrdinal(latestIncomeEnd, MaxEnd(shareList));
        DateOnly? cutoff = anchor is not null && DateOnly.TryParse(anchor, out var anchorDate)
            ? anchorDate.AddDays(-ValuationCore.MaxFundamentalStaleDays)
            : null;

        return shareList.Any(point =>
            IsOutstandingShareConcept(point.Concept)
            && (cutoff is null
                || (DateOnly.TryParse(point.End, out var end) && end >= cutoff.Value)));
    }

    private static string? LatestPeriodEnd(params IReadOnlyList<FinancialPoint>[] series) =>
        series.Select(MaxEnd).Aggregate((string?)null, MaxOrdinal);

    private static string? MaxEnd(IReadOnlyList<FinancialPoint> points)
    {
        string? max = null;
        foreach (var point in points)
            if (!string.IsNullOrEmpty(point.End))
                max = MaxOrdinal(max, point.End);
        return max;
    }

    private static string? MaxOrdinal(string? left, string? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        return string.CompareOrdinal(left, right) >= 0 ? left : right;
    }

    internal static bool HasUsableData(Fundamentals fundamentals) =>
        fundamentals.Eps.Count > 0
        || fundamentals.RevenuePoints.Count > 0
        || fundamentals.SharesOutstanding.Count > 0
        || fundamentals.GrossProfitPoints.Count > 0
        || fundamentals.OperatingIncomePoints.Count > 0
        || fundamentals.OperatingCashFlowPoints.Count > 0
        || fundamentals.CapexPoints.Count > 0
        || fundamentals.NetIncomeLossPoints.Count > 0
        || fundamentals.RdExpensePoints.Count > 0
        || fundamentals.CurrentAssetsPoints.Count > 0
        || fundamentals.CurrentLiabilitiesPoints.Count > 0
        || fundamentals.EquityPoints.Count > 0;
}
