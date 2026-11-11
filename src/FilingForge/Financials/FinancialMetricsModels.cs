using FilingForge.Models;
using System.Text.Json.Serialization;

namespace FilingForge;

public enum FinancialMetricKind
{
    Flow,
    Point
}

/// <summary>How a flow metric's TTM view is formed.</summary>
public enum FinancialMetricTtmPolicy
{
    /// <summary>Sum of four consecutive quarters.</summary>
    SumQuarterly,
    /// <summary>The quarter's own value (for averages such as weighted-average shares).</summary>
    CarryQuarterly,
    /// <summary>No TTM value.</summary>
    Blank
}

public enum FinancialMetricGroup
{
    IncomeStatement,
    CashFlow,
    BalanceSheet,
    Shares
}

public enum FinancialMetricProvider
{
    Unknown,
    UsGaap,
    IfrsFull
}

public static class FinancialMetricProviderNames
{
    public const string Unknown = "unknown";
    public const string UsGaap = "us-gaap";
    public const string IfrsFull = "ifrs-full";

    public static string Name(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.UsGaap => UsGaap,
            FinancialMetricProvider.IfrsFull => IfrsFull,
            _ => Unknown
        };
}

public record CompanyFactsProfile(
    FinancialMetricProvider Provider,
    string PrimaryTaxonomy,
    IReadOnlyList<string> Taxonomies,
    IReadOnlyList<string> Forms,
    int UsGaapMetricFactCount,
    int IfrsMetricFactCount
)
{
    public static readonly CompanyFactsProfile Empty = new(
        FinancialMetricProvider.Unknown,
        FinancialMetricProviderNames.Unknown,
        [],
        [],
        0,
        0
    );
}

public record SecConceptSpec(string Concept, string Unit = "USD", string Taxonomy = "us-gaap")
{
    public string Key => $"{Taxonomy}:{Concept}:{Unit}";
    public string SourceName => Taxonomy == "us-gaap" ? Concept : $"{Taxonomy}:{Concept}";
}

public record FinancialMetricDefinition(
    string Id,
    string DisplayName,
    FinancialMetricKind Kind,
    FinancialMetricGroup Group,
    bool CostCannotBeNegative,
    string Definition,
    string BlankBehavior,
    IReadOnlyList<SecConceptSpec> DirectConcepts,
    IReadOnlyList<string> FallbackOrder,
    FinancialMetricTtmPolicy TtmPolicy = FinancialMetricTtmPolicy.SumQuarterly,
    // When true, an exact SEC trailing-twelve-month duration fact may supply this metric's TTM value
    // where no strict four-quarter window exists (DirectTtmFact). Declared per metric so the engine
    // drives the supplement off the catalog instead of a hardcoded metric list in the build path.
    bool DirectTtmEligible = false
);

public record FinancialMetricInputs(
    IReadOnlyDictionary<string, IReadOnlyList<FinancialPoint>> Facts,
    IReadOnlyList<FinancialPoint> RevenuePoints,
    CompanyFactsProfile Profile
)
{
    public static FinancialMetricInputs Empty { get; } =
        new(new Dictionary<string, IReadOnlyList<FinancialPoint>>(), Array.Empty<FinancialPoint>(), CompanyFactsProfile.Empty);

    public IReadOnlyList<FinancialPoint> PointsFor(SecConceptSpec spec) =>
        Facts.TryGetValue(spec.Key, out var points) ? points : [];
}

public record FinancialMetricValue(
    double? Value,
    string Formula,
    string SourceConcepts,
    string? Filed,
    string? Accession,
    string Derivation,
    string SourceType = "companyfacts",
    string? SourceDocument = null,
    string? SourceLabel = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FinancialMetricInputTrace>? Inputs = null
)
{
    public bool HasValue => Value.HasValue;

    public static FinancialMetricValue Blank(string formula = FinancialMetricFormulaIds.BlankMissingInputs) =>
        new(null, formula, "", null, null, "blank", "blank");
}

public record FinancialMetricInputTrace(
    string Role,
    string? PeriodEnd,
    double? Value,
    string Formula,
    string SourceConcepts,
    string? Filed,
    string? Accession,
    string Derivation,
    string SourceType,
    string? SourceDocument, string? SourceLabel,
    // Only set for duration inputs whose length matters to the formula (e.g. share-day weighting).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PeriodStart = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DurationDays = null
);

public record FinancialMetricPeriodRow(
    string Ticker,
    string PeriodEnd,
    int? FiscalYear,
    int? FiscalQuarter,
    string QuarterLabel,
    string Provider,
    string Taxonomy,
    string ValidationStatus,
    IReadOnlyList<string> ValidationMessages,
    string Filed,
    IReadOnlyDictionary<string, FinancialMetricValue> Quarterly,
    IReadOnlyDictionary<string, FinancialMetricValue> Annual,
    IReadOnlyDictionary<string, FinancialMetricValue> Ttm,
    IReadOnlyDictionary<string, FinancialMetricValue> Point
);

public record FinancialMetricAuditRow(
    string Ticker,
    string PeriodEnd,
    string MetricId,
    string MetricName,
    string ValueType,
    string Provider,
    string Taxonomy,
    double? Value,
    string Formula,
    string SourceConcepts,
    string? Filed,
    string? Accession,
    string Derivation,
    string SourceType,
    string? SourceDocument,
    string? SourceLabel,
    string ValidationStatus,
    string ValidationMessages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FinancialMetricInputTrace>? Inputs = null
);

public record FinancialMetricsBuildResult(
    IReadOnlyList<FinancialMetricPeriodRow> Rows,
    IReadOnlyList<FinancialMetricAuditRow> AuditRows
);

public record FinancialMetricDefinitionTrace(
    string MetricId,
    string MetricName,
    string Group,
    string Kind,
    string Definition,
    IReadOnlyList<string> UsGaapDirectConcepts,
    IReadOnlyList<string> IfrsDirectConcepts,
    IReadOnlyList<string> FallbackOrder,
    string BlankBehavior
);

public record FinancialMetricRawInputsTrace(
    string Ticker,
    CompanyFactsProfile Profile,
    IReadOnlyDictionary<string, IReadOnlyList<FinancialPoint>> Facts,
    IReadOnlyList<FinancialPoint> RevenuePoints
);

public record FinancialMetricsTraceResponse(
    IReadOnlyList<string> Tickers,
    IReadOnlyList<FinancialMetricDefinitionTrace> Definitions,
    IReadOnlyList<FinancialMetricPeriodRow> Rows,
    IReadOnlyList<FinancialMetricAuditRow> AuditRows,
    IReadOnlyList<FinancialMetricRawInputsTrace> RawInputs
);

public static class FinancialMetricFormulaIds
{
    // Cost of revenue summed from a filing table's cost items.
    public const string FilingTableCostComponents = nameof(FilingTableCostComponents);

    // Weighted-average shares: duration-weighted Q4, and the quarter carried into the TTM view.
    public const string WeightedAverageQuarterFromAnnual = "StandaloneWeightedAverageQuarterFromAnnual",
        QuarterlyBasicAverageSharesCarriedToTtm = nameof(QuarterlyBasicAverageSharesCarriedToTtm),
        QuarterlyDilutedAverageSharesCarriedToTtm = nameof(QuarterlyDilutedAverageSharesCarriedToTtm);

    // Line item ÷ weighted-average shares.
    public const string
        RevenueDividedByBasicAverageShares = nameof(RevenueDividedByBasicAverageShares),
        RevenueDividedByDilutedAverageShares = nameof(RevenueDividedByDilutedAverageShares),
        CogsDividedByBasicAverageShares = "CostOfRevenueDividedByBasicAverageShares",
        CogsDividedByDilutedAverageShares = "CostOfRevenueDividedByDilutedAverageShares",
        GrossProfitDividedByBasicAverageShares = nameof(GrossProfitDividedByBasicAverageShares),
        GrossProfitDividedByDilutedAverageShares = nameof(GrossProfitDividedByDilutedAverageShares),
        OperatingExpensesDividedByBasicAverageShares = nameof(OperatingExpensesDividedByBasicAverageShares),
        OperatingExpensesDividedByDilutedAverageShares = nameof(OperatingExpensesDividedByDilutedAverageShares),
        OperatingIncomeDividedByBasicAverageShares = nameof(OperatingIncomeDividedByBasicAverageShares),
        OperatingIncomeDividedByDilutedAverageShares = nameof(OperatingIncomeDividedByDilutedAverageShares),
        PretaxIncomeDividedByBasicAverageShares = nameof(PretaxIncomeDividedByBasicAverageShares),
        PretaxIncomeDividedByDilutedAverageShares = nameof(PretaxIncomeDividedByDilutedAverageShares);

    public const string DirectFact = "DirectFact";
    public const string BankNetRevenue = "InterestIncomeExpenseNetPlusNoninterestIncome";
    public const string DerivedFromH1 = "StandaloneQuarterFromH1";
    public const string DerivedFrom9M = "StandaloneQuarterFrom9M";
    public const string DerivedFromAnnual = "StandaloneQuarterFromAnnual";
    public const string TtmSumQuarterly = "TtmSumQuarterly";
    public const string DirectTtmFact = "DirectTtmFact";
    public const string BlankMissingInputs = "BlankMissingInputs";
    public const string RevenueMinusGrossProfit = "RevenueMinusGrossProfit";
    public const string RevenueMinusEbitMinusOperatingExpenses = "RevenueMinusOperatingIncomeLossMinusOperatingExpenses";
    public const string RevenueMinusCogs = "RevenueMinusCogs";
    public const string OperatingExpensesMinusRnd = "OperatingExpensesMinusResearchAndDevelopment";
    public const string TotalOperatingCostMinusCogsMinusRnd = "TotalOperatingCostMinusCogsMinusResearchAndDevelopment";
    public const string RevenueMinusEbit = "RevenueMinusOperatingProfit";
    public const string CogsPlusOperatingExpenses = "CogsPlusOperatingExpenses";
    public const string EbitPlusDepreciationAmortization = "EbitPlusDepreciationAmortization";
    public const string RevenueMinusTotalOperatingCost = "RevenueMinusTotalOperatingCost";
    public const string GrossProfitMinusOperatingExpenses = "GrossProfitMinusOperatingExpenses";
    public const string NetIncomePlusTax = "NetIncomePlusTax";
    public const string EbitMinusInterestExpense = "EbitMinusInterestExpense";
    public const string InterestIncomeMinusInterestExpense = "InterestIncomeMinusInterestExpense";
    public const string TaxDividedByPretaxIncome = "TaxProvisionDividedByPretaxIncome";
    public const string OperatingCashFlowMinusCapex = "OperatingCashFlowMinusCapex";
    public const string CashPlusShortTermInvestments = "CashPlusShortTermInvestments";
    public const string DebtCurrentPlusLongTermDebt = "DebtCurrentPlusLongTermDebt";
    public const string DebtCurrentPlusCurrentMaturities = "DebtCurrentPlusCurrentMaturities";
    public const string DebtCurrentPlusLongTermDebtPlusFinanceLease = "DebtCurrentPlusLongTermDebtPlusFinanceLease";
    public const string DebtLongTermPlusFinanceLease = "DebtLongTermPlusFinanceLease";
    public const string CurrentAssetsMinusCurrentLiabilities = "CurrentAssetsMinusCurrentLiabilities";
    public const string CommonEquityMinusGoodwillAndIntangibles = "CommonStockEquityMinusGoodwillAndIntangibles";
    public const string NetPpePlusOperatingLeaseRightOfUseAsset = "NetPpePlusOperatingLeaseRightOfUseAsset";
    public const string CapitalLeaseCurrentPlusNoncurrent = "CapitalLeaseCurrentPlusNoncurrent";
    public const string TotalAssetsMinusTotalEquity = "TotalAssetsMinusTotalEquity";
    public const string SellingMarketingPlusGeneralAdministrative = "SellingMarketingPlusGeneralAdministrative";
    public const string GeneralAndAdministrativeExpenseOnly = "GeneralAndAdministrativeExpenseOnly";
    public const string DebtIssuanceLongTermPlusShortTerm = "DebtIssuanceLongTermPlusShortTerm";
    public const string DebtRepaymentComponents = "DebtRepaymentLongTermPlusShortTermPlusFinanceLeasePrincipal";
    public const string DebtRepaymentComponentsWithFinanceLeaseInterest = "DebtRepaymentLongTermPlusShortTermPlusFinanceLeasePrincipalPlusFinanceLeaseInterest";
    public const string WorkingCapitalAssetPresentationSign = "WorkingCapitalAssetIncreaseAsCashOutflow";
    public const string DebtIssuanceMinusRepayment = "DebtIssuanceMinusRepayment";
    public const string LongTermDebtIssuanceMinusRepayment = "LongTermDebtIssuanceMinusRepayment";
    public const string ShortTermDebtIssuanceMinusRepayment = "ShortTermDebtIssuanceMinusRepayment";
    public const string CashAndShortTermMinusCash = "CashAndShortTermInvestmentsMinusCash";
    public const string TotalAssetsMinusCurrentAssets = "TotalAssetsMinusCurrentAssets";
    public const string NetReceivablesPlusAllowance = "NetReceivablesPlusAllowance";
    public const string AccountsPayablePlusAccruedLiabilities = "AccountsPayablePlusAccruedLiabilities";
    public const string LiabilitiesMinusCurrentLiabilities = "LiabilitiesMinusCurrentLiabilities";
    public const string LongTermDebtPlusCapitalLeaseNoncurrent = "LongTermDebtPlusCapitalLeaseNoncurrent";
    public const string TotalDebtMinusCashAndShortTermInvestments = "TotalDebtMinusCashAndShortTermInvestments";
    public const string CommonStockSharesIssuedMinusOutstanding = "CommonStockSharesIssuedMinusOutstanding";
}
