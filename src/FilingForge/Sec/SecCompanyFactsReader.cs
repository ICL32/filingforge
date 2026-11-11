using FilingForge.Models;

namespace FilingForge;

internal static class SecCompanyFactsReader
{
    // Direct taxonomy/concept/unit read; the concept label becomes FinancialPoint.Concept.
    internal static List<FinancialPoint> ReadConcept(CompanyFacts facts, string taxonomy, string concept, string unit)
    {
        var raw = facts.Raw(taxonomy, concept, unit);
        var result = new List<FinancialPoint>(raw.Count);
        foreach (var fact in raw)
            result.Add(fact.ToPoint(concept));
        return result;
    }

    internal static List<FinancialPoint> ReadUsdConcept(CompanyFacts facts, string concept) =>
        ReadConcept(facts, "us-gaap", concept, "USD");

    internal static List<FinancialPoint> ReadConcept(CompanyFacts facts, SecConceptSpec spec)
    {
        var raw = facts.Raw(spec.Taxonomy, spec.Concept, spec.Unit);
        var result = new List<FinancialPoint>(raw.Count);
        foreach (var fact in raw)
            result.Add(fact.ToPoint(spec.SourceName));
        return result;
    }

    internal static List<FinancialPoint> ReadFactsByPriority(
        CompanyFacts facts,
        IEnumerable<SecConceptSpec> concepts,
        Func<FinancialPoint, string>? keySelector = null) =>
        FinancialPointMerger
            .MergeByConceptPriority(concepts, spec => ReadConcept(facts, spec), keySelector)
            .ToList();

    internal static List<FinancialPoint> ReadPointFactsByPriority(
        CompanyFacts facts,
        IEnumerable<SecConceptSpec> concepts) =>
        ReadFactsByPriority(facts, concepts, point => $"{point.End}|{point.Form}");

    internal static List<FinancialPoint> BuildBankNetRevenuePoints(CompanyFacts facts)
    {
        var netInterest = ReadUsdConcept(facts, "InterestIncomeExpenseNet");
        var nonInterestByPeriod = new Dictionary<string, FinancialPoint>();
        foreach (var p in ReadUsdConcept(facts, "NoninterestIncome").OrderBy(p => p.Filed))
            FinancialPointMerger.MergePeriodFact(nonInterestByPeriod, p);

        var result = new List<FinancialPoint>();
        foreach (var net in netInterest.OrderBy(p => p.Filed))
        {
            if (!nonInterestByPeriod.TryGetValue(FinancialPointHelpers.FlowPeriodKey(net), out var nonInterest)) continue;
            result.Add(new FinancialPoint(
                net.Start,
                net.End,
                net.Val + nonInterest.Val,
                net.Form,
                string.CompareOrdinal(net.Filed, nonInterest.Filed) >= 0 ? net.Filed : nonInterest.Filed,
                "InterestIncomeExpenseNet+NoninterestIncome",
                net.Fy ?? nonInterest.Fy,
                net.Fp ?? nonInterest.Fp,
                net.Frame ?? nonInterest.Frame,
                net.Accn ?? nonInterest.Accn
            ));
        }

        return result;
    }

    internal static List<FinancialPoint> ReadSharesOutstanding(CompanyFacts facts)
    {
        var concepts = FinancialMetricsCatalog
            .DirectConceptsFor(FinancialMetricsCatalog.SharesOutstanding, FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.DirectConceptsFor(FinancialMetricsCatalog.SharesOutstanding, FinancialMetricProvider.IfrsFull))
            .Concat([
                FinancialMetricsCatalog.UsGaap("WeightedAverageNumberOfDilutedSharesOutstanding", "shares"),
                FinancialMetricsCatalog.UsGaap("WeightedAverageNumberOfSharesOutstandingBasic", "shares"),
                FinancialMetricsCatalog.Ifrs("AdjustedWeightedAverageShares", "shares"),
                FinancialMetricsCatalog.Ifrs("WeightedAverageShares", "shares")
            ])
            .GroupBy(c => c.Key)
            .Select(g => g.First());

        return FinancialPointMerger
            .MergeSharePointFacts(concepts, spec => ReadConcept(facts, spec), point => $"{point.End}|{point.Form}")
            .ToList();
    }

    internal static CompanyFactsProfile BuildCompanyFactsProfile(CompanyFacts facts)
    {
        var usGaapCount = CountMetricFacts(facts, FinancialMetricProvider.UsGaap);
        var ifrsCount = CountMetricFacts(facts, FinancialMetricProvider.IfrsFull);
        var provider = usGaapCount > 0
            ? FinancialMetricProvider.UsGaap
            : ifrsCount > 0
                ? FinancialMetricProvider.IfrsFull
                : FinancialMetricProvider.Unknown;

        return new CompanyFactsProfile(
            provider,
            FinancialMetricProviderNames.Name(provider),
            facts.Taxonomies,
            facts.Forms,
            usGaapCount,
            ifrsCount
        );
    }

    private static int CountMetricFacts(CompanyFacts facts, FinancialMetricProvider provider)
    {
        var count = 0;
        var primaryTaxonomy = FinancialMetricProviderNames.Name(provider);
        var concepts = FinancialMetricsCatalog.AllMetrics
            .SelectMany(m => FinancialMetricsCatalog.DirectConceptsFor(m, provider));
        if (provider == FinancialMetricProvider.UsGaap)
            concepts = concepts.Concat([FinancialMetricsCatalog.UsGaap("InterestIncomeExpenseNet"), FinancialMetricsCatalog.UsGaap("NoninterestIncome")]);

        foreach (var spec in concepts.Where(s => s.Taxonomy == primaryTaxonomy).DistinctBy(s => s.Key))
            count += ReadConcept(facts, spec).Count;
        return count;
    }
}
