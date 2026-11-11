using FilingForge.Models;

namespace FilingForge;

public static class FinancialStatementSeriesBuilder
{
    public static StatementSeries Build(string ticker, FinancialMetricsBuildResult metrics)
    {
        var quarterly = metrics.Rows
            .Where(row => HasStatementFlow(row.Quarterly))
            .Select(row => BuildRow(row, row.Quarterly, isAnnual: false))
            .OrderBy(row => row.PeriodEnd, StringComparer.Ordinal)
            .ToList();

        var annual = metrics.Rows
            .Where(row => HasStatementFlow(row.Annual))
            .Select(row => BuildRow(row, row.Annual, isAnnual: true))
            .OrderBy(row => row.PeriodEnd, StringComparer.Ordinal)
            .ToList();

        var warningCount = quarterly.Concat(annual)
            .Sum(row => row.ValidationMessages.Count);

        return new StatementSeries(
            ticker.ToUpperInvariant(),
            quarterly,
            annual,
            new StatementValidationSummary(quarterly.Count, annual.Count, warningCount));
    }

    private static StatementPeriodRow BuildRow(
        FinancialMetricPeriodRow row,
        IReadOnlyDictionary<string, FinancialMetricValue> flow,
        bool isAnnual)
    {
        var point = row.Point;
        var provenance = new Dictionary<string, StatementProvenance>(StringComparer.Ordinal);
        AddProvenance(provenance, row, FinancialMetricsCatalog.Revenue.Id, flow[FinancialMetricsCatalog.Revenue.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.NetIncome.Id, flow[FinancialMetricsCatalog.NetIncome.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.GrossProfit.Id, flow[FinancialMetricsCatalog.GrossProfit.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.Ebit.Id, flow[FinancialMetricsCatalog.Ebit.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.OperatingCashFlow.Id, flow[FinancialMetricsCatalog.OperatingCashFlow.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.Capex.Id, flow[FinancialMetricsCatalog.Capex.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.FreeCashFlow.Id, flow[FinancialMetricsCatalog.FreeCashFlow.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.TotalAssets.Id, point[FinancialMetricsCatalog.TotalAssets.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.TotalLiabilities.Id, point[FinancialMetricsCatalog.TotalLiabilities.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.CashAndShortTermEquivalents.Id, point[FinancialMetricsCatalog.CashAndShortTermEquivalents.Id]);
        AddProvenance(provenance, row, FinancialMetricsCatalog.TotalDebt.Id, point[FinancialMetricsCatalog.TotalDebt.Id]);

        var rowLabel = isAnnual
            ? $"FY {row.FiscalYear ?? YearFromDate(row.PeriodEnd)}"
            : row.QuarterLabel;

        return new StatementPeriodRow(
            row.PeriodEnd,
            rowLabel,
            row.FiscalYear,
            isAnnual ? null : row.FiscalQuarter,
            flow[FinancialMetricsCatalog.Revenue.Id].Value,
            flow[FinancialMetricsCatalog.NetIncome.Id].Value,
            flow[FinancialMetricsCatalog.GrossProfit.Id].Value,
            flow[FinancialMetricsCatalog.Ebit.Id].Value,
            flow[FinancialMetricsCatalog.OperatingCashFlow.Id].Value,
            flow[FinancialMetricsCatalog.Capex.Id].Value,
            flow[FinancialMetricsCatalog.FreeCashFlow.Id].Value,
            point[FinancialMetricsCatalog.TotalAssets.Id].Value,
            point[FinancialMetricsCatalog.TotalLiabilities.Id].Value,
            point[FinancialMetricsCatalog.CashAndShortTermEquivalents.Id].Value,
            point[FinancialMetricsCatalog.TotalDebt.Id].Value,
            provenance,
            row.ValidationStatus,
            row.ValidationMessages
        );
    }

    private static bool HasStatementFlow(IReadOnlyDictionary<string, FinancialMetricValue> flow) =>
        flow[FinancialMetricsCatalog.Revenue.Id].HasValue
        || flow[FinancialMetricsCatalog.NetIncome.Id].HasValue
        || flow[FinancialMetricsCatalog.GrossProfit.Id].HasValue
        || flow[FinancialMetricsCatalog.Ebit.Id].HasValue
        || flow[FinancialMetricsCatalog.OperatingCashFlow.Id].HasValue
        || flow[FinancialMetricsCatalog.Capex.Id].HasValue
        || flow[FinancialMetricsCatalog.FreeCashFlow.Id].HasValue;

    private static void AddProvenance(
        Dictionary<string, StatementProvenance> provenance,
        FinancialMetricPeriodRow row,
        string metricId,
        FinancialMetricValue value)
    {
        if (!value.HasValue) return;
        var (provider, taxonomy) = ProviderTaxonomyFor(row, value);
        provenance[metricId] = new StatementProvenance(
            provider,
            taxonomy,
            value.Formula,
            value.SourceConcepts,
            value.Filed,
            value.Accession,
            value.SourceType,
            value.SourceDocument,
            value.SourceLabel);
    }

    private static (string Provider, string Taxonomy) ProviderTaxonomyFor(
        FinancialMetricPeriodRow row,
        FinancialMetricValue value)
    {
        var taxonomies = value.SourceConcepts
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SourceTaxonomy)
            .Where(taxonomy => !string.IsNullOrWhiteSpace(taxonomy))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (taxonomies.Count == 1)
        {
            var taxonomy = taxonomies[0];
            var provider = taxonomy == FinancialMetricProviderNames.IfrsFull
                ? FinancialMetricProviderNames.IfrsFull
                : taxonomy == FinancialMetricProviderNames.UsGaap
                    ? FinancialMetricProviderNames.UsGaap
                    : row.Provider;
            return (provider, taxonomy);
        }

        if (taxonomies.Count > 1)
            return ("mixed", "mixed");

        return (row.Provider, row.Taxonomy);
    }

    private static string SourceTaxonomy(string sourceConcept)
    {
        var concept = sourceConcept.Trim();
        if (concept.StartsWith("ifrs-full:", StringComparison.Ordinal))
            return FinancialMetricProviderNames.IfrsFull;
        if (concept.StartsWith("dei:", StringComparison.Ordinal))
            return "dei";
        if (concept.Contains(':', StringComparison.Ordinal))
            return concept.Split(':', 2)[0];
        return concept.Length == 0 ? "" : FinancialMetricProviderNames.UsGaap;
    }

    private static int YearFromDate(string end) =>
        DateTime.TryParse(end, out var parsed) ? parsed.Year : 0;

}
