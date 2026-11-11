namespace FilingForge;

public static partial class FinancialMetricsCatalog
{
    public static readonly FinancialMetricDefinition OperatingCashFlow = new(
        "operatingCashFlow",
        "Operating Cash Flow",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Net cash provided by operating activities.",
        "Blank when no operating cash flow concept exists.",
        [UsGaap("NetCashProvidedByUsedInOperatingActivities"), UsGaap("NetCashProvidedByUsedInOperatingActivitiesContinuingOperations")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition InvestingCashFlow = new(
        "investingCashFlow",
        "Investing Cash Flow",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Net cash provided by or used in investing activities.",
        "Blank when no investing cash flow concept exists.",
        [UsGaap("NetCashProvidedByUsedInInvestingActivities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition FinancingCashFlow = new(
        "financingCashFlow",
        "Financing Cash Flow",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Net cash provided by or used in financing activities.",
        "Blank when no financing cash flow concept exists.",
        [UsGaap("NetCashProvidedByUsedInFinancingActivities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition Capex = new(
        "capex",
        "CapEx",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Capital expenditures.",
        "Blank when no CapEx concept exists, or when the direct value is negative.",
        [UsGaap("PaymentsToAcquirePropertyPlantAndEquipment"), UsGaap("PaymentsForCapitalExpenditures"), UsGaap("PaymentsToAcquireProductiveAssets")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition FreeCashFlow = new(
        "freeCashFlow",
        "Free Cash Flow",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Operating cash flow less CapEx.",
        "Blank when OCF or CapEx is missing.",
        [],
        [FinancialMetricFormulaIds.OperatingCashFlowMinusCapex]
    );

    public static readonly FinancialMetricDefinition IssuanceOfCapitalStock = new(
        "issuanceOfCapitalStock",
        "Issuance of Capital Stock",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash proceeds from issuing capital stock.",
        "Blank when no capital-stock issuance concept exists, or when the direct value is negative.",
        [UsGaap("ProceedsFromIssuanceOfCommonStock"), UsGaap("ProceedsFromStockOptionsExercised")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition IssuanceOfDebt = new(
        "issuanceOfDebt",
        "Issuance of Debt",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash proceeds from issuing debt.",
        "Blank when aggregate debt issuance and long/short-term component inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("ProceedsFromIssuanceOfDebt"), UsGaap("ProceedsFromBorrowings")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.DebtIssuanceLongTermPlusShortTerm]
    );

    public static readonly FinancialMetricDefinition RepaymentOfDebt = new(
        "repaymentOfDebt",
        "Repayment of Debt",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash repayments of debt, including identifiable finance lease interest payments when separately reported.",
        "Blank when aggregate debt repayment and long/short-term/lease component inputs are missing, or when the direct/derived value is negative.",
        [UsGaap("RepaymentsOfDebt"), UsGaap("RepaymentsOfBorrowings")],
        [
            FinancialMetricFormulaIds.DirectFact,
            FinancialMetricFormulaIds.DebtRepaymentComponentsWithFinanceLeaseInterest,
            FinancialMetricFormulaIds.DebtRepaymentComponents
        ]
    );

    public static readonly FinancialMetricDefinition IncomeTaxPaidSupplementalData = new(
        "incomeTaxPaidSupplementalData",
        "Income Tax Paid Supplemental Data",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash paid for income taxes, net of refunds when available.",
        "Blank when no income-tax-paid concept exists, or when the direct value is negative.",
        [UsGaap("IncomeTaxesPaidNet"), UsGaap("IncomeTaxesPaid")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition InterestPaidSupplementalData = new(
        "interestPaidSupplementalData",
        "Interest Paid Supplemental Data",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash paid for interest.",
        "Blank when no current interest-paid concept exists, or when the direct value is negative.",
        [UsGaap("InterestPaid"), UsGaap("InterestPaidNet")],
        [FinancialMetricFormulaIds.DirectFact],
        DirectTtmEligible: true
    );

    public static readonly FinancialMetricDefinition StockBasedCompensation = new(
        "stockBasedCompensation",
        "Stock Based Compensation",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Share-based compensation added back in operating cash-flow reconciliation.",
        "Blank when no share-based compensation concept exists, or when the direct value is negative.",
        [UsGaap("ShareBasedCompensation"), UsGaap("AllocatedShareBasedCompensationExpense")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition DeferredTax = new(
        "deferredTax",
        "Deferred Tax",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Deferred income tax expense or benefit in operating cash-flow reconciliation.",
        "Blank when no deferred-tax cash-flow reconciliation concept exists.",
        [UsGaap("DeferredIncomeTaxExpenseBenefit")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ChangeInReceivables = new(
        "changeInReceivables",
        "Change in Receivables",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Cash-flow presentation impact of changes in accounts receivable or receivables plus other operating assets; asset increases are shown as cash outflows.",
        "Blank when no receivables working-capital change concept exists.",
        [UsGaap("IncreaseDecreaseInAccountsReceivable"), UsGaap("IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.WorkingCapitalAssetPresentationSign]
    );

    public static readonly FinancialMetricDefinition ChangeInInventory = new(
        "changeInInventory",
        "Change in Inventory",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Cash-flow presentation impact of changes in inventory; inventory increases are shown as cash outflows.",
        "Blank when no inventory working-capital change concept exists.",
        [UsGaap("IncreaseDecreaseInInventories")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.WorkingCapitalAssetPresentationSign]
    );

    public static readonly FinancialMetricDefinition ChangeInPayables = new(
        "changeInPayables",
        "Change in Payables",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Change in accounts payable.",
        "Blank when no accounts-payable working-capital change concept exists.",
        [UsGaap("IncreaseDecreaseInAccountsPayable")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ChangeInAccruedExpense = new(
        "changeInAccruedExpense",
        "Change in Accrued Expense",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Change in accrued liabilities and other operating liabilities.",
        "Blank when no accrued-liability working-capital change concept exists.",
        [UsGaap("IncreaseDecreaseInAccruedLiabilities"), UsGaap("IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ChangeInOtherWorkingCapital = new(
        "changeInOtherWorkingCapital",
        "Change in Other Working Capital",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Other working-capital changes reported in operating cash-flow reconciliation.",
        "Blank when no other working-capital change concept exists.",
        [UsGaap("IncreaseDecreaseInContractWithCustomerLiability"), UsGaap("IncreaseDecreaseInOtherNoncurrentAssets")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition PurchaseOfPpe = new(
        "purchaseOfPpe",
        "Purchase of PPE",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Purchases of property, plant, equipment, productive assets, or capital expenditures.",
        "Blank when no PPE purchase concept exists, or when the direct value is negative.",
        [UsGaap("PaymentsToAcquirePropertyPlantAndEquipment"), UsGaap("PaymentsToAcquireProductiveAssets"), UsGaap("PaymentsForCapitalExpenditures")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition SaleOfPpe = new(
        "saleOfPpe",
        "Sale of PPE",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Proceeds from sale of property, plant, equipment, or productive assets.",
        "Blank when no PPE sale proceeds concept exists, or when the direct value is negative.",
        [UsGaap("ProceedsFromSaleOfPropertyPlantAndEquipment"), UsGaap("PaymentsForProceedsFromProductiveAssets")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition PurchaseOfBusiness = new(
        "purchaseOfBusiness",
        "Purchase of Business",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash paid to acquire businesses, net of cash acquired when reported.",
        "Blank when no business acquisition cash-payment concept exists, or when the direct value is negative.",
        [UsGaap("PaymentsToAcquireBusinessesNetOfCashAcquired"), UsGaap("PaymentsToAcquireBusinessesAndInterestInAffiliates")],
        [FinancialMetricFormulaIds.DirectFact, FinancialMetricFormulaIds.DirectTtmFact],
        DirectTtmEligible: true
    );

    public static readonly FinancialMetricDefinition PurchaseOfInvestment = new(
        "purchaseOfInvestment",
        "Purchase of Investment",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash paid to acquire investments or marketable securities.",
        "Blank when no investment purchase concept exists, or when the direct value is negative.",
        [UsGaap("PaymentsToAcquireInvestments"), UsGaap("PaymentsToAcquireMarketableSecurities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition SaleOfInvestment = new(
        "saleOfInvestment",
        "Sale of Investment",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Proceeds from sales, maturities, or collections of investments and marketable securities.",
        "Blank when no investment sale/maturity proceeds concept exists, or when the direct value is negative.",
        [UsGaap("ProceedsFromSaleMaturityAndCollectionsOfInvestments"), UsGaap("ProceedsFromSaleAndMaturityOfMarketableSecurities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition LongTermDebtIssuance = new(
        "longTermDebtIssuance",
        "Long Term Debt Issuance",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash proceeds from long-term debt issuance.",
        "Blank when no long-term debt issuance concept exists, or when the direct value is negative.",
        [UsGaap("ProceedsFromIssuanceOfLongTermDebt")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ShortTermDebtIssuance = new(
        "shortTermDebtIssuance",
        "Short Term Debt Issuance",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash proceeds from short-term debt issuance.",
        "Blank when no short-term debt issuance concept exists, or when the direct value is negative.",
        [UsGaap("ProceedsFromShortTermDebt")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition LongTermDebtPayments = new(
        "longTermDebtPayments",
        "Long Term Debt Payments",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash repayments of long-term debt.",
        "Blank when no long-term debt repayment concept exists, or when the direct value is negative.",
        [UsGaap("RepaymentsOfLongTermDebt"), UsGaap("RepaymentsOfLongTermDebtAndCapitalSecurities")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ShortTermDebtPayments = new(
        "shortTermDebtPayments",
        "Short Term Debt Payments",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash repayments of short-term debt.",
        "Blank when no short-term debt repayment concept exists, or when the direct value is negative.",
        [UsGaap("RepaymentsOfShortTermDebt")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition FinanceLeasePrincipalPayments = new(
        "financeLeasePrincipalPayments",
        "Finance Lease Principal Payments",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Finance lease principal repayments.",
        "Blank when no finance lease principal payment concept exists, or when the direct value is negative.",
        [UsGaap("FinanceLeasePrincipalPayments"), UsGaap("RepaymentsOfLongTermCapitalLeaseObligations")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition NetIssuancePaymentsOfDebt = new(
        "netIssuancePaymentsOfDebt",
        "Net Issuance Payments of Debt",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Debt issuance proceeds less debt repayments.",
        "Blank when debt issuance or repayment inputs are missing.",
        [],
        [FinancialMetricFormulaIds.DebtIssuanceMinusRepayment]
    );

    public static readonly FinancialMetricDefinition NetLongTermDebtIssuance = new(
        "netLongTermDebtIssuance",
        "Net Long Term Debt Issuance",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Long-term debt issuance proceeds less long-term debt repayments.",
        "Blank when long-term debt issuance or repayment inputs are missing.",
        [],
        [FinancialMetricFormulaIds.LongTermDebtIssuanceMinusRepayment]
    );

    public static readonly FinancialMetricDefinition NetShortTermDebtIssuance = new(
        "netShortTermDebtIssuance",
        "Net Short Term Debt Issuance",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Short-term debt issuance proceeds less short-term debt repayments.",
        "Blank when short-term debt issuance or repayment inputs are missing.",
        [],
        [FinancialMetricFormulaIds.ShortTermDebtIssuanceMinusRepayment]
    );

    public static readonly FinancialMetricDefinition RepurchaseOfCapitalStock = new(
        "repurchaseOfCapitalStock",
        "Repurchase of Capital Stock",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash paid to repurchase common stock or equity.",
        "Blank when no stock repurchase concept exists, or when the direct value is negative.",
        [UsGaap("PaymentsForRepurchaseOfCommonStock"), UsGaap("PaymentsForRepurchaseOfEquity")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ChangesInCash = new(
        "changesInCash",
        "Changes in Cash",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Period increase/decrease in cash, cash equivalents, restricted cash and equivalents.",
        "Blank when no cash period-increase/decrease concept exists.",
        [
            UsGaap("CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect"),
            UsGaap("CashAndCashEquivalentsPeriodIncreaseDecrease")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition EffectOfExchangeRateChanges = new(
        "effectOfExchangeRateChanges",
        "Effect of Exchange Rate Changes",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: false,
        "Effect of exchange-rate changes on cash and cash equivalents.",
        "Blank when no exchange-rate effect on cash concept exists.",
        [
            UsGaap("EffectOfExchangeRateOnCashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents"),
            UsGaap("EffectOfExchangeRateOnCashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsIncludingDisposalGroupAndDiscontinuedOperations"),
            UsGaap("EffectOfExchangeRateOnCashAndCashEquivalents")
        ],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition EndCashPosition = new(
        "endCashPosition",
        "End Cash Position",
        FinancialMetricKind.Point,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash and cash equivalents at the period end shown in the cash-flow statement.",
        "Blank when no cash position concept exists, or when the direct value is negative.",
        [UsGaap("CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents"), UsGaap("CashAndCashEquivalentsAtCarryingValue")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition DividendsPaid = new(
        "dividendsPaid",
        "Dividends Paid",
        FinancialMetricKind.Flow,
        FinancialMetricGroup.CashFlow,
        CostCannotBeNegative: true,
        "Cash dividends paid to common (and total) shareholders in the financing section.",
        "Blank when no dividends-paid concept exists, or when the direct value is negative (stored positive; displayed as an outflow).",
        [UsGaap("PaymentsOfDividendsCommonStock"), UsGaap("PaymentsOfDividends")],
        [FinancialMetricFormulaIds.DirectFact]
    );
}
