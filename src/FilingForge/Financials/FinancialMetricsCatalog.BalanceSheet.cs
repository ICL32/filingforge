namespace FilingForge;

public static partial class FinancialMetricsCatalog
{
    public static readonly FinancialMetricDefinition TotalAssets = new(
        "totalAssets",
        "Total Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Total assets.",
        "Blank when no total assets concept exists, or when the direct value is negative.",
        [UsGaap("Assets")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition TotalLiabilities = new(
        "totalLiabilities",
        "Total Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Total liabilities.",
        "Blank when direct liabilities and total-assets-minus-equity fallback inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("Liabilities")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.TotalAssetsMinusTotalEquity]
    );

    public static readonly FinancialMetricDefinition TotalEquityGrossMinorityInterest = new(
        "totalEquityGrossMinorityInterest",
        "Total Equity Gross Minority Interest",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Total equity including any non-controlling interest.",
        "Blank when no equity concept exists.",
        [UsGaap("StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"), UsGaap("StockholdersEquity")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CommonStockEquity = new(
        "commonStockEquity",
        "Common Stock Equity",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Equity attributable to common stockholders / parent owners.",
        "Blank when no common stockholders' equity concept exists.",
        [UsGaap("StockholdersEquity"), UsGaap("StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CapitalLeaseObligations = new(
        "capitalLeaseObligations",
        "Capital Lease Obligations",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Finance/capital lease obligations.",
        "Blank when direct lease obligation and current/noncurrent lease fallback inputs are missing, or when derived value is negative.",
        [UsGaap("FinanceLeaseLiability"), UsGaap("CapitalLeaseObligations")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.CapitalLeaseCurrentPlusNoncurrent]
    );

    public static readonly FinancialMetricDefinition NetTangibleAssets = new(
        "netTangibleAssets",
        "Net Tangible Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Common stock equity less goodwill and intangible assets.",
        "Blank when common equity or goodwill/intangible inputs are missing.",
        [],
        [FinancialMetricFormulaIds.CommonEquityMinusGoodwillAndIntangibles]
    );

    public static readonly FinancialMetricDefinition WorkingCapital = new(
        "workingCapital",
        "Working Capital",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Current assets less current liabilities.",
        "Blank when current assets or current liabilities are missing.",
        [],
        [FinancialMetricFormulaIds.CurrentAssetsMinusCurrentLiabilities]
    );

    public static readonly FinancialMetricDefinition TangibleBookValue = new(
        "tangibleBookValue",
        "Tangible Book Value",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Same SEC-backed value as net tangible assets.",
        "Blank when net tangible assets cannot be derived.",
        [],
        [FinancialMetricFormulaIds.CommonEquityMinusGoodwillAndIntangibles]
    );

    public static readonly FinancialMetricDefinition CashAndShortTermEquivalents = new(
        "cashAndShortTermEquivalents",
        "Cash & Short-Term Equivalents",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Cash, cash equivalents and short-term investments.",
        "Blank when direct cash/STI and cash plus short-term-investment fallback inputs are missing, or when derived value is negative.",
        [UsGaap("CashCashEquivalentsAndShortTermInvestments"), UsGaap("CashAndCashEquivalentsAtCarryingValue"), UsGaap("CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.CashPlusShortTermInvestments]
    );

    public static readonly FinancialMetricDefinition TotalDebt = new(
        "totalDebt",
        "Total Debt",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Short-term plus long-term debt and finance lease obligations.",
        "Blank when direct total debt and debt/finance-lease fallback inputs are missing, or when derived value is negative. Operating lease liabilities are excluded.",
        // Direct all-in total-debt concepts. DebtAndCapitalLeaseObligations is the pre-2019 "Capital"
        // lease naming of DebtAndFinanceLeaseObligations; DebtLongtermAndShorttermCombinedAmount is the
        // aggregate long-term debt (incl. current maturities) some filers report every quarter while
        // tagging LongTermDebt only at year-end (e.g. VTR/SW). All are complete totals returned as-is.
        [
            UsGaap("DebtAndFinanceLeaseObligations"),
            UsGaap("DebtAndCapitalLeaseObligations"),
            UsGaap("DebtLongtermAndShorttermCombinedAmount")
        ],
        [
            FinancialMetricFormulaIds.DirectFact,
            FinancialMetricFormulaIds.DebtCurrentPlusLongTermDebtPlusFinanceLease,
            FinancialMetricFormulaIds.DebtLongTermPlusFinanceLease,
            FinancialMetricFormulaIds.DebtCurrentPlusLongTermDebt
        ]
    );

    public static readonly FinancialMetricDefinition CurrentAssets = new(
        "currentAssets",
        "Current Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Current assets.",
        "Blank when no current-assets concept exists, or when the direct value is negative.",
        [UsGaap("AssetsCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CashAndCashEquivalents = new(
        "cashAndCashEquivalents",
        "Cash And Cash Equivalents",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Cash and cash equivalents, excluding short-term investments when separately reported.",
        "Blank when no cash-only concept exists, or when the direct value is negative.",
        [UsGaap("CashAndCashEquivalentsAtCarryingValue"), UsGaap("Cash"), UsGaap("CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherShortTermInvestments = new(
        "otherShortTermInvestments",
        "Other Short Term Investments",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Short-term investments, directly reported or derived from cash/STI less cash.",
        "Blank when direct short-term investments and cash/STI minus cash inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("ShortTermInvestments"), UsGaap("MarketableSecuritiesCurrent")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.CashAndShortTermMinusCash]
    );

    public static readonly FinancialMetricDefinition Receivables = new(
        "receivables",
        "Receivables",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Accounts receivable, net current.",
        "Blank when no receivables concept exists, or when the direct value is negative.",
        [UsGaap("AccountsReceivableNetCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition AccountsReceivable = new(
        "accountsReceivable",
        "Accounts Receivable",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Accounts receivable, net current.",
        "Blank when no accounts receivable concept exists, or when the direct value is negative.",
        [UsGaap("AccountsReceivableNetCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition GrossAccountsReceivable = new(
        "grossAccountsReceivable",
        "Gross Accounts Receivable",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Gross accounts receivable, directly reported or net receivables plus allowance.",
        "Blank when gross receivables and net receivables plus allowance inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("AccountsReceivableGrossCurrent")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.NetReceivablesPlusAllowance]
    );

    public static readonly FinancialMetricDefinition AllowanceForDoubtfulAccounts = new(
        "allowanceForDoubtfulAccounts",
        "Allowance For Doubtful Accounts",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Allowance for doubtful accounts receivable.",
        "Blank when no allowance concept exists, or when the direct value is negative.",
        [UsGaap("AllowanceForDoubtfulAccountsReceivableCurrent"), UsGaap("AllowanceForDoubtfulAccountsReceivable")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Inventory = new(
        "inventory",
        "Inventory",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Inventory, net.",
        "Blank when no inventory concept exists, or when the direct value is negative.",
        [UsGaap("InventoryNet"), UsGaap("Inventory")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherCurrentAssets = new(
        "otherCurrentAssets",
        "Other Current Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Other current assets.",
        "Blank when no other-current-assets concept exists, or when the direct value is negative.",
        [UsGaap("PrepaidExpenseAndOtherAssetsCurrent"), UsGaap("OtherAssetsCurrent"), UsGaap("OtherRestrictedAssetsCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition TotalNonCurrentAssets = new(
        "totalNonCurrentAssets",
        "Total Non Current Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Non-current assets, directly reported or total assets less current assets.",
        "Blank when direct non-current assets and total/current asset inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("AssetsNoncurrent")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.TotalAssetsMinusCurrentAssets]
    );

    public static readonly FinancialMetricDefinition NetPpe = new(
        "netPpe",
        "Net PPE",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Property, plant and equipment plus finance and operating lease right-of-use assets, net of accumulated depreciation and amortization.",
        "Blank when no net PPE concept exists, or when the direct/derived value is negative.",
        [
            UsGaap("PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetAfterAccumulatedDepreciationAndAmortization"),
            UsGaap("PropertyPlantAndEquipmentNet")
        ],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.NetPpePlusOperatingLeaseRightOfUseAsset]
    );

    public static readonly FinancialMetricDefinition GrossPpe = new(
        "grossPpe",
        "Gross PPE",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Property, plant and equipment before accumulated depreciation and amortization.",
        "Blank when no gross PPE concept exists, or when the direct value is negative.",
        [
            UsGaap("PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetBeforeAccumulatedDepreciationAndAmortization"),
            UsGaap("PropertyPlantAndEquipmentGross")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition AccumulatedDepreciationPpe = new(
        "accumulatedDepreciationPpe",
        "Accumulated Depreciation",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Accumulated depreciation and amortization for property, plant and equipment.",
        "Blank when no accumulated-depreciation PPE concept exists, or when the direct value is negative.",
        [
            UsGaap("PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetAccumulatedDepreciationAndAmortization"),
            UsGaap("AccumulatedDepreciationDepletionAndAmortizationPropertyPlantAndEquipment")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Goodwill = new(
        "goodwill",
        "Goodwill",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Goodwill.",
        "Blank when no goodwill concept exists, or when the direct value is negative.",
        [UsGaap("Goodwill")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherIntangibleAssets = new(
        "otherIntangibleAssets",
        "Other Intangible Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Intangible assets excluding goodwill.",
        "Blank when no other-intangible-assets concept exists, or when the direct value is negative.",
        [UsGaap("FiniteLivedIntangibleAssetsNet"), UsGaap("IntangibleAssetsNetExcludingGoodwill"), UsGaap("IndefiniteLivedIntangibleAssetsExcludingGoodwill")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherNonCurrentAssets = new(
        "otherNonCurrentAssets",
        "Other Non Current Assets",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Other non-current assets.",
        "Blank when no other non-current assets concept exists, or when the direct value is negative.",
        [UsGaap("OtherAssetsNoncurrent"), UsGaap("OtherNoncurrentAssets")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CurrentLiabilities = new(
        "currentLiabilities",
        "Current Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Current liabilities.",
        "Blank when no current-liabilities concept exists, or when the direct value is negative.",
        [UsGaap("LiabilitiesCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition PayablesAndAccruedExpenses = new(
        "payablesAndAccruedExpenses",
        "Payables And Accrued Expenses",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Accounts payable plus accrued liabilities.",
        "Blank when direct payables/accruals and component inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("AccountsPayableAndAccruedLiabilitiesCurrent")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.AccountsPayablePlusAccruedLiabilities]
    );

    public static readonly FinancialMetricDefinition Payables = new(
        "payables",
        "Payables",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Accounts payable.",
        "Blank when no payables concept exists, or when the direct value is negative.",
        [UsGaap("AccountsPayableCurrent"), UsGaap("AccountsPayable")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition AccountsPayable = new(
        "accountsPayable",
        "Accounts Payable",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Accounts payable.",
        "Blank when no accounts payable concept exists, or when the direct value is negative.",
        [UsGaap("AccountsPayableCurrent"), UsGaap("AccountsPayable")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CurrentAccruedExpense = new(
        "currentAccruedExpense",
        "Current Accrued Expense",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Current accrued liabilities.",
        "Blank when no current accrued-liability concept exists, or when the direct value is negative.",
        [UsGaap("AccruedLiabilitiesCurrent"), UsGaap("AccruedLiabilities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CurrentDeferredLiabilities = new(
        "currentDeferredLiabilities",
        "Current Deferred Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Current deferred revenue / contract liabilities.",
        "Blank when no current deferred-liability concept exists, or when the direct value is negative.",
        [UsGaap("ContractWithCustomerLiabilityCurrent"), UsGaap("DeferredRevenueCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CurrentDeferredRevenue = new(
        "currentDeferredRevenue",
        "Current Deferred Revenue",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Current deferred revenue / contract liabilities.",
        "Blank when no current deferred-revenue concept exists, or when the direct value is negative.",
        [UsGaap("ContractWithCustomerLiabilityCurrent"), UsGaap("DeferredRevenueCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition TotalNonCurrentLiabilities = new(
        "totalNonCurrentLiabilities",
        "Total Non Current Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Non-current liabilities, directly reported or total liabilities less current liabilities.",
        "Blank when direct non-current liabilities and total/current liability inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("LiabilitiesNoncurrent")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.LiabilitiesMinusCurrentLiabilities]
    );

    public static readonly FinancialMetricDefinition LongTermDebtAndCapitalLease = new(
        "longTermDebtAndCapitalLease",
        "Long Term Debt And Capital Lease",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Non-current long-term debt plus non-current capital/finance lease obligations.",
        "Blank when direct long-term debt/capital lease and component inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("LongTermDebtAndFinanceLeaseObligationsNoncurrent")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.LongTermDebtPlusCapitalLeaseNoncurrent]
    );

    public static readonly FinancialMetricDefinition LongTermDebt = new(
        "longTermDebt",
        "Long Term Debt",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Long-term debt, non-current.",
        "Blank when no long-term debt concept exists, or when the direct value is negative.",
        [UsGaap("LongTermDebtNoncurrent"), UsGaap("LongTermDebt")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition LongTermCapitalLeaseObligations = new(
        "longTermCapitalLeaseObligations",
        "Long Term Capital Lease Obligations",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Non-current capital/finance lease obligations.",
        "Blank when no non-current lease obligation concept exists, or when the direct value is negative.",
        [UsGaap("FinanceLeaseLiabilityNoncurrent"), UsGaap("CapitalLeaseObligationsNoncurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherNonCurrentLiabilities = new(
        "otherNonCurrentLiabilities",
        "Other Non Current Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Other non-current liabilities.",
        "Blank when no other non-current liabilities concept exists, or when the direct value is negative.",
        [UsGaap("OtherLiabilitiesNoncurrent"), UsGaap("OtherNoncurrentLiabilities"), UsGaap("OtherLiabilitiesAndDeferredRevenueNoncurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CapitalStock = new(
        "capitalStock",
        "Capital Stock",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Preferred plus common stock value when separately reported; direct capital stock otherwise.",
        "Blank when no capital stock concept exists, or when the direct value is negative.",
        [UsGaap("CapitalStock"), UsGaap("CommonStockValue")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition PreferredStock = new(
        "preferredStock",
        "Preferred Stock",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Preferred stock value.",
        "Blank when no preferred stock concept exists, or when the direct value is negative.",
        [UsGaap("PreferredStockValue")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CommonStock = new(
        "commonStock",
        "Common Stock",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Common stock value.",
        "Blank when no common stock concept exists, or when the direct value is negative.",
        [UsGaap("CommonStockValue")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition AdditionalPaidInCapital = new(
        "additionalPaidInCapital",
        "Additional Paid In Capital",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Additional paid-in capital.",
        "Blank when no additional paid-in capital concept exists.",
        [UsGaap("AdditionalPaidInCapital")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition RetainedEarnings = new(
        "retainedEarnings",
        "Retained Earnings",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Retained earnings / accumulated deficit.",
        "Blank when no retained earnings concept exists.",
        [UsGaap("RetainedEarningsAccumulatedDeficit")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition TreasuryStock = new(
        "treasuryStock",
        "Treasury Stock",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Treasury stock value.",
        "Blank when no treasury stock value concept exists, or when the direct value is negative.",
        [UsGaap("TreasuryStockCommonValue"), UsGaap("TreasuryStockValue"), UsGaap("TreasuryStockValueAcquiredCostMethod")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition GainsLossesNotAffectingRetainedEarnings = new(
        "gainsLossesNotAffectingRetainedEarnings",
        "Gains Losses Not Affecting Retained Earnings",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Accumulated other comprehensive income/loss.",
        "Blank when no accumulated other comprehensive income/loss concept exists.",
        [UsGaap("AccumulatedOtherComprehensiveIncomeLossNetOfTax")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherEquityAdjustments = new(
        "otherEquityAdjustments",
        "Other Equity Adjustments",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Other equity adjustments, currently mapped to accumulated other comprehensive income/loss when reported.",
        "Blank when no supported equity-adjustment concept exists.",
        [UsGaap("AccumulatedOtherComprehensiveIncomeLossNetOfTax")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition NetDebt = new(
        "netDebt",
        "Net Debt",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Total debt less cash and short-term investments.",
        "Blank when total debt or cash/STI inputs are missing.",
        [],
        [FinancialMetricFormulaIds.TotalDebtMinusCashAndShortTermInvestments]
    );

    public static readonly FinancialMetricDefinition LongTermInvestments = new(
        "longTermInvestments",
        "Long-Term Investments",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Non-current investments and long-term marketable securities.",
        "Blank when no long-term investments concept exists, or when the direct value is negative.",
        [UsGaap("LongTermInvestments"), UsGaap("MarketableSecuritiesNoncurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition CurrentDebt = new(
        "currentDebt",
        "Short-Term Debt / Current Portion of Long-Term Debt",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Short-term borrowings and the current portion of long-term debt shown on the balance sheet.",
        "Blank when no current-debt concept exists, or when the direct value is negative.",
        [UsGaap("DebtCurrent"), UsGaap("ShortTermBorrowings"), UsGaap("LongTermDebtCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition DeferredTaxLiabilities = new(
        "deferredTaxLiabilities",
        "Deferred Tax Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Non-current deferred tax liabilities.",
        "Blank when no deferred tax liability concept exists, or when the direct value is negative.",
        [UsGaap("DeferredTaxLiabilitiesNoncurrent"), UsGaap("DeferredIncomeTaxLiabilitiesNet")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OtherCurrentLiabilities = new(
        "otherCurrentLiabilities",
        "Other Current Liabilities",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Other current liabilities not itemized in a dedicated row.",
        "Blank when no other-current-liabilities concept exists, or when the direct value is negative.",
        [UsGaap("OtherLiabilitiesCurrent")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition MinorityInterest = new(
        "minorityInterest",
        "Noncontrolling Interest",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: false,
        "Equity attributable to noncontrolling (minority) interests.",
        "Blank when no noncontrolling-interest equity concept exists.",
        [UsGaap("MinorityInterest")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition MezzanineEquity = new(
        "mezzanineEquity",
        "Temporary (Mezzanine) Equity",
        FinancialMetricKind.Point,
        FinancialMetricGroup.BalanceSheet,
        CostCannotBeNegative: true,
        "Temporary/mezzanine equity — redeemable preferred stock or redeemable noncontrolling interest, carried between liabilities and permanent equity, so a full balance sheet foots as Assets = Liabilities + Mezzanine + Equity.",
        "Blank when no temporary-equity concept exists (most filers have none).",
        [
            UsGaap("TemporaryEquityCarryingAmountIncludingPortionAttributableToNoncontrollingInterests"),
            UsGaap("TemporaryEquityCarryingAmountAttributableToParent"),
            UsGaap("RedeemableNoncontrollingInterestEquityCarryingAmount")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );
}
