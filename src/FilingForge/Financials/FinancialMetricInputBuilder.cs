using Microsoft.Extensions.Logging;
using FilingForge.Models;
using static FilingForge.SecCompanyFactsReader;

namespace FilingForge;

internal static class FinancialMetricInputBuilder
{
    /// <summary>
    /// Reads every catalog concept plus the merged revenue series. Gaps are filled from filing
    /// tables: from <paramref name="recentFilingFacts"/> when the caller already parsed them,
    /// otherwise by asking <paramref name="filingSource"/> (when given) for the filings.
    /// </summary>
    internal static async Task<FinancialMetricInputs> BuildAsync(
        string sym,
        CompanyFacts facts,
        ISecFilingDocumentSource? filingSource,
        IReadOnlyList<FinancialPoint>? recentFilingFacts = null,
        ILogger? logger = null)
    {
        var profile = BuildCompanyFactsProfile(facts);
        var result = ReadRequiredConceptFacts(facts);
        var revenuePoints = BuildRevenuePoints(facts);

        if (recentFilingFacts is not null)
            AddParsedFilingFacts(result, revenuePoints, recentFilingFacts);
        else if (filingSource is not null)
            await SecFilingFallbackService.AddMissingRecentFilingFactsAsync(sym, filingSource, result, revenuePoints, logger);

        return new FinancialMetricInputs(result, revenuePoints, profile);
    }

    internal static Dictionary<string, IReadOnlyList<FinancialPoint>> ReadRequiredConceptFacts(CompanyFacts facts)
    {
        var result = new Dictionary<string, IReadOnlyList<FinancialPoint>>();
        foreach (var spec in FinancialMetricsCatalog.RequiredConcepts)
            result[spec.Key] = ReadConcept(facts, spec);

        return result;
    }

    // Contract-revenue concepts that, post-ASC 606/842, may carry only the small non-lease residual
    // for a pure lessor (the rental revenue is tagged separately under OperatingLeaseLeaseIncome).
    private static readonly HashSet<string> ContractRevenueResidualConcepts = new(StringComparer.Ordinal)
    {
        "RevenueFromContractWithCustomerExcludingAssessedTax",
        "RevenueFromContractWithCustomerIncludingAssessedTax"
    };

    internal static List<FinancialPoint> BuildRevenuePoints(CompanyFacts facts)
    {
        var revMerged = new Dictionary<string, FinancialPoint>();

        // Walk every non-lease revenue fact oldest filing first, so a restated comparative filed
        // later (possibly under a different concept after a policy change) overwrites the stale
        // figure. Within one filing date the catalog's concept order decides.
        var contractConcepts = DirectConceptsForAnyProvider(FinancialMetricsCatalog.Revenue)
            .Where(spec => spec.SourceName != "OperatingLeaseLeaseIncome")
            .ToList();
        var byFilingThenRank = contractConcepts
            .SelectMany((spec, rank) => ReadConcept(facts, spec).Select(point => (point, rank)))
            .OrderBy(x => x.point.Filed, StringComparer.Ordinal)
            .ThenBy(x => x.rank)
            .Select(x => x.point);
        foreach (var point in byFilingThenRank)
            FinancialPointMerger.MergePeriodFact(revMerged, point, FinancialPointHelpers.FlowPeriodKey);

        // Operating-lease income is the last-resort top line: it only claims periods nothing else
        // reported (pure lessors). ApplyOperatingLeaseRevenue then fixes periods where a small
        // contract-revenue residual was picked although the lease line is the real revenue.
        var leaseIncome = FinancialPointMerger.MergeByConceptPriority(
            [FinancialMetricsCatalog.UsGaap("OperatingLeaseLeaseIncome")],
            spec => ReadConcept(facts, spec),
            FinancialPointHelpers.FlowPeriodKey);
        foreach (var lease in leaseIncome)
            revMerged.TryAdd(FinancialPointHelpers.FlowPeriodKey(lease), lease);

        ApplyOperatingLeaseRevenue(facts, revMerged);

        // Bank net-revenue reads us-gaap concepts directly; they return empty for non-bank or
        // IFRS filers, so no us-gaap-presence guard is needed.
        var bankMerged = new Dictionary<string, FinancialPoint>();
        foreach (var p in BuildBankNetRevenuePoints(facts))
            FinancialPointMerger.MergePeriodFact(bankMerged, p);

        foreach (var (key, point) in bankMerged)
            if (!revMerged.ContainsKey(key))
                revMerged[key] = point;

        return [.. revMerged.Values];
    }

    private static void ApplyOperatingLeaseRevenue(
        CompanyFacts facts,
        Dictionary<string, FinancialPoint> revMerged)
    {
        var leasePoints = FinancialPointMerger.MergeByConceptPriority(
            [FinancialMetricsCatalog.UsGaap("OperatingLeaseLeaseIncome")],
            spec => ReadConcept(facts, spec),
            FinancialPointHelpers.FlowPeriodKey);
        if (leasePoints.Count == 0)
            return;

        // (1) Same-period override: when the exact-period lease income exceeds a chosen contract
        //     residual, it is the real top line. (2) End-level cleanup: drop any remaining contract
        //     residual whose period END has a larger lease income at ANY duration — otherwise a
        //     standalone tiny residual (RFCWC tagged per quarter) would beat a cumulative-only lease
        //     series (lease tagged only YTD) during quarterly reconstruction, leaking the residual as
        //     the quarter's revenue. Only ever removes a contract residual smaller than the lease, so a
        //     normal company's real contract revenue (larger than any sublease income) is untouched.
        var leaseMaxByEnd = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var lease in leasePoints)
        {
            var key = FinancialPointHelpers.FlowPeriodKey(lease);
            if (revMerged.TryGetValue(key, out var chosen)
                && lease.Val > chosen.Val
                && IsContractRevenueResidual(chosen))
            {
                revMerged[key] = lease;
            }
            if (!leaseMaxByEnd.TryGetValue(lease.End, out var max) || lease.Val > max)
                leaseMaxByEnd[lease.End] = lease.Val;
        }

        foreach (var key in revMerged.Keys.ToList())
        {
            var point = revMerged[key];
            if (IsContractRevenueResidual(point)
                && leaseMaxByEnd.TryGetValue(point.End, out var leaseMax)
                && leaseMax > point.Val)
            {
                revMerged.Remove(key);
            }
        }
    }

    private static bool IsContractRevenueResidual(FinancialPoint point) =>
        point.Concept is { } concept && ContractRevenueResidualConcepts.Contains(StripTaxonomyPrefix(concept));

    private static string StripTaxonomyPrefix(string concept)
    {
        var colon = concept.IndexOf(':');
        return colon < 0 ? concept : concept[(colon + 1)..];
    }

    internal static IReadOnlyList<SecConceptSpec> DirectConceptsForAnyProvider(FinancialMetricDefinition metric) =>
        FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    internal static IReadOnlyList<SecConceptSpec> OperatingExpensesConceptsForAnyProvider() =>
        FinancialMetricsCatalog.OperatingExpensesConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.OperatingExpensesConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    internal static List<FinancialPoint> PointsForAnyProvider(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric) =>
        PointsForAnyProvider(inputs, DirectConceptsForAnyProvider(metric));

    internal static List<FinancialPoint> PointsForAnyProvider(
        FinancialMetricInputs inputs,
        IEnumerable<SecConceptSpec> concepts)
        => FinancialPointMerger
            .MergeByConceptPriority(concepts, inputs.PointsFor, FinancialPointHelpers.FlowPeriodKey)
            .ToList();

    internal static void AddParsedFilingFacts(
        Dictionary<string, IReadOnlyList<FinancialPoint>> result,
        List<FinancialPoint> revenuePoints,
        IEnumerable<FinancialPoint> parsed)
    {
        foreach (var point in parsed)
        {
            if (point.Concept is null || !SecFilingConcepts.TrySpec(point.Concept, out var spec))
                continue;

            List<FinancialPoint> existing = result.TryGetValue(spec.Key, out var points)
                ? points.ToList()
                : [];
            existing.Add(point);
            result[spec.Key] = existing;

            if (spec.SourceName == "Revenues"
                || spec.SourceName == "ifrs-full:Revenue"
                || spec.SourceName == "RevenueFromContractWithCustomerExcludingAssessedTax")
            {
                revenuePoints.Add(point);
            }
        }
    }

    internal static bool HasMetricForPeriod(
        IReadOnlyDictionary<string, IReadOnlyList<FinancialPoint>> result,
        FinancialMetricDefinition metric,
        string periodEnd)
    {
        foreach (var spec in DirectConceptsForAnyProvider(metric))
        {
            if (result.TryGetValue(spec.Key, out var points)
                && points.Any(point => string.Equals(point.End, periodEnd, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
