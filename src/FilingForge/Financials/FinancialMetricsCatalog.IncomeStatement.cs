namespace FilingForge;

public static partial class FinancialMetricsCatalog
{
    public static readonly FinancialMetricDefinition Revenue = new(
        "revenue",
        "Revenue",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Top-line revenue or bank net revenue.",
        "Blank when no revenue concept or bank net-revenue inputs exist.",
        [
            UsGaap("Revenues"),
            UsGaap("RevenuesNetOfInterestExpense"),
            UsGaap("RevenueFromContractWithCustomerExcludingAssessedTax"),
            UsGaap("RevenueFromContractWithCustomerIncludingAssessedTax"),
            UsGaap("SalesRevenueNet"),
            // Equity-REIT rental revenue: lowest priority so it only supplies revenue when no higher
            // concept covers a period (e.g. EQR). When a higher contract-revenue concept is present but
            // is only a tiny non-lease residual (e.g. CPT), FinancialMetricInputBuilder overrides it
            // with this lease income where the lease income is larger. Kept in the catalog (not just the
            // builder) so metric-presence checks and the raw trace recognize REIT revenue.
            UsGaap("OperatingLeaseLeaseIncome")
        ],
        ["Direct SEC revenue concept", FinancialMetricFormulaIds.BankNetRevenue]
    );

    public static readonly FinancialMetricDefinition Cogs = new(
        "cogs",
        "COGS",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Cost of revenue or goods sold. When a filer reports no single cost total, the cost items listed in its income-statement table are added up.",
        "Empty when no cost fact, no summable cost items and no fallback formula inputs exist, or when the result would be negative.",
        [UsGaap("CostOfGoodsAndServicesSold"), UsGaap("CostOfGoodsSold"), UsGaap("CostOfRevenue")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.FilingTableCostComponents,
         FinancialMetricFormulaIds.RevenueMinusGrossProfit, FinancialMetricFormulaIds.RevenueMinusEbitMinusOperatingExpenses]
    );

    public static readonly FinancialMetricDefinition GrossProfit = new(
        "grossProfit",
        "Gross Profit",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Revenue after COGS.",
        "Blank when direct gross profit and revenue/COGS fallback inputs are missing.",
        [UsGaap("GrossProfit")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.RevenueMinusCogs]
    );

    public static readonly FinancialMetricDefinition Sga = new(
        "sga",
        "SG&A Costs",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Selling, general and administrative expense.",
        "Blank when aggregate SG&A, component, G&A-only, and operating-expense fallback inputs are missing, or when the derived cost is negative.",
        [UsGaap("SellingGeneralAndAdministrativeExpense")],
        [
            FinancialMetricFormulaIds.DirectFact,
            FinancialMetricFormulaIds.SellingMarketingPlusGeneralAdministrative,
            FinancialMetricFormulaIds.GeneralAndAdministrativeExpenseOnly,
            FinancialMetricFormulaIds.OperatingExpensesMinusRnd,
            FinancialMetricFormulaIds.TotalOperatingCostMinusCogsMinusRnd
        ]
    );

    public static readonly FinancialMetricDefinition SellingAndMarketingExpense = new(
        "sellingAndMarketingExpense",
        "Selling & Marketing Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Selling, marketing, and advertising expense.",
        "Blank when no selling/marketing expense concept exists, or when the direct value is negative.",
        [UsGaap("SellingAndMarketingExpense"), UsGaap("MarketingExpense"), UsGaap("MarketingAndAdvertisingExpense"), UsGaap("AdvertisingExpense")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition GeneralAndAdministrativeExpense = new(
        "generalAndAdministrativeExpense",
        "General & Administrative Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "General and administrative expense.",
        "Blank when no general and administrative expense concept exists, or when the direct value is negative.",
        [UsGaap("GeneralAndAdministrativeExpense")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Rnd = new(
        "rnd",
        "R&D Costs",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Research and development expense.",
        "Blank when no R&D concept exists.",
        [
            UsGaap("ResearchAndDevelopmentExpense"),
            UsGaap("ResearchAndDevelopmentExpenseExcludingAcquiredInProcessCost"),
            UsGaap("ResearchAndDevelopmentExpenseSoftwareExcludingAcquiredInProcessCost")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition TotalOperatingCost = new(
        "totalOperatingCost",
        "Total Operating Cost",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Total costs needed to reconcile revenue to operating profit.",
        "Blank when direct costs and revenue/EBIT or COGS/operating-expense fallback inputs are missing, or when derived cost is negative.",
        [UsGaap("CostsAndExpenses")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.RevenueMinusEbit, FinancialMetricFormulaIds.CogsPlusOperatingExpenses]
    );

    public static readonly FinancialMetricDefinition OperatingExpensesMetric = new(
        "operatingExpenses",
        "Operating Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Operating expenses.",
        "Blank when no operating-expense concept exists, or when the direct value is negative.",
        [UsGaap("OperatingExpenses")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Ebitda = new(
        "ebitda",
        "EBITDA",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Earnings before interest, tax, depreciation and amortization.",
        "Blank when direct EBITDA and EBIT plus D&A fallback inputs are missing.",
        [UsGaap("EarningsBeforeInterestTaxesDepreciationAndAmortization")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.EbitPlusDepreciationAmortization]
    );

    public static readonly FinancialMetricDefinition Ebit = new(
        "ebit",
        "Operating Profit (EBIT)",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Operating income / EBIT.",
        "Blank when direct operating income and revenue/cost or gross-profit/operating-expense fallback inputs are missing.",
        [UsGaap("OperatingIncomeLoss")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.RevenueMinusTotalOperatingCost, FinancialMetricFormulaIds.GrossProfitMinusOperatingExpenses]
    );

    public static readonly FinancialMetricDefinition OperatingIncomeAsReported = new(
        "operatingIncomeAsReported",
        "Total Operating Income as Reported",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Reported operating income / loss without formula fallback.",
        "Blank when no direct operating income concept exists.",
        [UsGaap("OperatingIncomeLoss")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition InterestIncome = new(
        "interestIncome",
        "Interest Income",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Interest income.",
        "Blank when no interest income concept exists, or when the direct value is negative.",
        [UsGaap("InterestIncomeNonOperating"), UsGaap("InvestmentIncomeInterest"), UsGaap("InterestIncomeOperating")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition InterestExpense = new(
        "interestExpense",
        "Interest Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Interest expense.",
        "Blank when no interest expense concept exists, or when the direct value is negative.",
        [UsGaap("InterestExpenseNonOperating"), UsGaap("InterestExpense"), UsGaap("InterestAndDebtExpense")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition NetNonOperatingInterestIncomeExpense = new(
        "netNonOperatingInterestIncomeExpense",
        "Net Non Operating Interest Income Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Net non-operating interest income less interest expense.",
        "Blank when direct net interest income/expense and interest income/expense fallback inputs are missing.",
        [UsGaap("InterestIncomeExpenseNonOperatingNet")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.InterestIncomeMinusInterestExpense]
    );

    public static readonly FinancialMetricDefinition NetInterestIncome = new(
        "netInterestIncome",
        "Net Interest Income",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Interest income less interest expense, or bank net interest income.",
        "Blank when direct net interest income and interest income/expense fallback inputs are missing.",
        [UsGaap("InterestIncomeExpenseNet")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.InterestIncomeMinusInterestExpense]
    );

    public static readonly FinancialMetricDefinition OtherIncomeExpense = new(
        "otherIncomeExpense",
        "Other Income Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Other non-operating income or expense.",
        "Blank when no other income/expense concept exists.",
        [UsGaap("NonoperatingIncomeExpense"), UsGaap("OtherNonoperatingIncomeExpense"), UsGaap("OtherIncomeExpenseNet")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Tax = new(
        "tax",
        "Tax",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Income tax expense or benefit.",
        "Blank when no tax concept exists.",
        [UsGaap("IncomeTaxExpenseBenefit")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Ebt = new(
        "ebt",
        "EBT",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Earnings before tax.",
        "Blank when direct EBT and net-income/tax or EBIT/interest fallback inputs are missing.",
        [
            UsGaap("IncomeLossFromContinuingOperationsBeforeIncomeTaxesExtraordinaryItemsNoncontrollingInterest"),
            UsGaap("IncomeLossFromContinuingOperationsBeforeIncomeTaxesMinorityInterestAndIncomeLossFromEquityMethodInvestments"),
            UsGaap("IncomeLossFromContinuingOperationsBeforeIncomeTaxes")
        ],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.NetIncomePlusTax, FinancialMetricFormulaIds.EbitMinusInterestExpense]
    );

    public static readonly FinancialMetricDefinition TaxRateForCalcs = new(
        "taxRateForCalcs",
        "Tax Rate for Calcs",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Tax provision divided by pretax income, shown as a percent.",
        "Blank when tax or pretax income is missing, zero, or non-finite.",
        [],
        [FinancialMetricFormulaIds.TaxDividedByPretaxIncome],
        FinancialMetricTtmPolicy.Blank
    );

    public static readonly FinancialMetricDefinition NetIncome = new(
        "netIncome",
        "Net Income",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Consolidated net income / loss; falls back to parent-attributable net income and common-shareholder income.",
        "Blank when no net income concept exists.",
        [UsGaap("ProfitLoss"), UsGaap("NetIncomeLoss"), UsGaap("NetIncomeLossAvailableToCommonStockholdersBasic")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition NetIncomeCommonStockholders = new(
        "netIncomeCommonStockholders",
        "Net Income Common Stockholders",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Net income available to common stockholders.",
        "Blank when no common-stockholder net income concept exists.",
        [UsGaap("NetIncomeLossAvailableToCommonStockholdersBasic"), UsGaap("NetIncomeLossAvailableToCommonStockholdersDiluted")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition DilutedNiAvailableToCommonStockholders = new(
        "dilutedNiAvailableToCommonStockholders",
        "Diluted NI Available to Com Stockholders",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Diluted net income available to common stockholders.",
        "Blank when no diluted common-stockholder net income concept exists.",
        [UsGaap("NetIncomeLossAvailableToCommonStockholdersDiluted"), UsGaap("NetIncomeLossAvailableToCommonStockholdersBasic")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition NetIncomeContinuingOperations = new(
        "netIncomeContinuingOperations",
        "Net Income from Continuing Operations",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Income or loss from continuing operations after tax.",
        "Blank when no continuing-operations income concept exists.",
        [
            UsGaap("IncomeLossFromContinuingOperationsAfterTax"),
            UsGaap("IncomeLossFromContinuingOperationsIncludingPortionAttributableToNoncontrollingInterest"),
            UsGaap("IncomeLossFromContinuingOperations")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    private static string EpsDefinition(string kind) =>
        $"Reported {kind} earnings per share. The TTM figure adds up the four most recent consecutive standalone quarters.";

    private static string EpsBlankBehavior(string kind) =>
        $"Empty when the filer reports no {kind} EPS; TTM is empty without four consecutive standalone quarters.";

    private static readonly IReadOnlyList<string> EpsFormulas =
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.TtmSumQuarterly];

    private static string AverageSharesDefinition(string kind) =>
        $"Weighted-average {kind} shares outstanding. An average is not summed across quarters, so TTM shows the period's quarterly count.";

    private const string AverageSharesBlankBehavior =
        "Empty unless the quarter is reported directly or can be rebuilt from a full-year and a nine-month average.";

    private static IReadOnlyList<string> AverageSharesFormulas(string carriedFormula) =>
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.WeightedAverageQuarterFromAnnual, carriedFormula];

    public static readonly FinancialMetricDefinition BasicEps = new(
        "basicEPS",
        "Basic EPS",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        EpsDefinition("basic"),
        EpsBlankBehavior("basic"),
        [UsGaap("EarningsPerShareBasic", "USD/shares"), UsGaap("EarningsPerShareBasicAndDiluted", "USD/shares")],
        EpsFormulas,
        TtmPolicy: FinancialMetricTtmPolicy.SumQuarterly
    );

    public static readonly FinancialMetricDefinition DilutedEps = new(
        "dilutedEPS",
        "Diluted EPS",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        EpsDefinition("diluted"),
        EpsBlankBehavior("diluted"),
        [UsGaap("EarningsPerShareDiluted", "USD/shares"), UsGaap("EarningsPerShareBasicAndDiluted", "USD/shares")],
        EpsFormulas,
        TtmPolicy: FinancialMetricTtmPolicy.SumQuarterly
    );

    public static readonly FinancialMetricDefinition BasicAverageShares = new(
        "basicAverageShares",
        "Basic Average Shares",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        AverageSharesDefinition("basic"),
        AverageSharesBlankBehavior,
        [UsGaap("WeightedAverageNumberOfSharesOutstandingBasic", "shares")],
        AverageSharesFormulas(FinancialMetricFormulaIds.QuarterlyBasicAverageSharesCarriedToTtm),
        TtmPolicy: FinancialMetricTtmPolicy.CarryQuarterly
    );

    public static readonly FinancialMetricDefinition DilutedAverageShares = new(
        "dilutedAverageShares",
        "Diluted Average Shares",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        AverageSharesDefinition("diluted"),
        AverageSharesBlankBehavior + " A reconstructed diluted Q4 is approximate: dilution is measured per period, not per day.",
        [UsGaap("WeightedAverageNumberOfDilutedSharesOutstanding", "shares")],
        AverageSharesFormulas(FinancialMetricFormulaIds.QuarterlyDilutedAverageSharesCarriedToTtm),
        TtmPolicy: FinancialMetricTtmPolicy.CarryQuarterly
    );

    public static readonly FinancialMetricDefinition ReconciledDepreciation = new(
        "reconciledDepreciation",
        "Reconciled Depreciation",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: true,
        "Depreciation, depletion and amortization used to reconcile EBIT to EBITDA.",
        "Blank when no depreciation/amortization concept exists, or when the direct value is negative.",
        [
            UsGaap("DepreciationDepletionAndAmortization"),
            UsGaap("DepreciationDepletionAndAmortizationExpense"),
            UsGaap("DepreciationAndAmortization"),
            UsGaap("DepreciationAndAmortizationExpense"),
            UsGaap("Depreciation")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition NoncontrollingInterestIncome = new(
        "noncontrollingInterestIncome",
        "Net Income Attributable to Noncontrolling Interest",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.IncomeStatement,
        CostCannotBeNegative: false,
        "Portion of consolidated net income attributable to noncontrolling (minority) interests.",
        "Blank when no noncontrolling-interest income concept exists; may be negative.",
        [UsGaap("NetIncomeLossAttributableToNoncontrollingInterest")],
        [FinancialMetricFormulaIds.DirectFact]
    );
}
