namespace FilingForge;

// Central SEC metric catalog. Metric definitions live in the statement-section partials
// (FinancialMetricsCatalog.IncomeStatement.cs, FinancialMetricsCatalog.CashFlow.cs,
// FinancialMetricsCatalog.BalanceSheet.cs, FinancialMetricsCatalog.Shares.cs); this file owns
// the cross-metric registries, support concept sets, and provider helpers. The registries are
// assigned in the static constructor because static field initializer order across partial
// files is unspecified, so inline initializers could observe null definition fields.
public static partial class FinancialMetricsCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<SecConceptSpec>> IfrsDirectConcepts;

    public static readonly IReadOnlyList<FinancialMetricDefinition> FlowMetrics;

    public static readonly IReadOnlyList<FinancialMetricDefinition> PointMetrics;

    public static readonly IReadOnlyList<FinancialMetricDefinition> AllMetrics;

    static FinancialMetricsCatalog()
    {
        IfrsDirectConcepts =
            new Dictionary<string, IReadOnlyList<SecConceptSpec>>(StringComparer.Ordinal)
            {
                [Revenue.Id] = [Ifrs("Revenue")],
                [Cogs.Id] = [Ifrs("CostOfSales")],
                [GrossProfit.Id] = [Ifrs("GrossProfit")],
                [Sga.Id] = [Ifrs("SellingGeneralAndAdministrativeExpense")],
                [Rnd.Id] = [Ifrs("ResearchAndDevelopmentExpense")],
                [TotalOperatingCost.Id] = [],
                [OperatingExpensesMetric.Id] = [Ifrs("OperatingExpenses")],
                [Ebitda.Id] = [],
                [Ebit.Id] = [Ifrs("ProfitLossFromOperatingActivities")],
                [OperatingIncomeAsReported.Id] = [Ifrs("ProfitLossFromOperatingActivities")],
                [InterestIncome.Id] = [Ifrs("FinanceIncome"), Ifrs("InterestIncome")],
                [InterestExpense.Id] = [Ifrs("FinanceCosts"), Ifrs("InterestExpenseOnBorrowings")],
                [NetNonOperatingInterestIncomeExpense.Id] = [],
                [NetInterestIncome.Id] = [],
                [OtherIncomeExpense.Id] = [Ifrs("OtherIncome"), Ifrs("OtherExpenseByNature")],
                [Tax.Id] = [Ifrs("IncomeTaxExpenseContinuingOperations"), Ifrs("IncomeTaxExpenseIncome")],
                [Ebt.Id] = [Ifrs("ProfitLossBeforeTax")],
                [TaxRateForCalcs.Id] = [],
                [NetIncome.Id] = [
                    Ifrs("ProfitLoss"),
                    Ifrs("ProfitLossAttributableToOwnersOfParent"),
                    Ifrs("ProfitLossAttributableToOrdinaryEquityHoldersOfParentEntity")
                ],
                [NetIncomeCommonStockholders.Id] = [
                    Ifrs("ProfitLossAttributableToOrdinaryEquityHoldersOfParentEntity"),
                    Ifrs("ProfitLossAttributableToOwnersOfParent")
                ],
                [DilutedNiAvailableToCommonStockholders.Id] = [
                    Ifrs("ProfitLossAttributableToOrdinaryEquityHoldersOfParentEntity"),
                    Ifrs("ProfitLossAttributableToOwnersOfParent")
                ],
                [NetIncomeContinuingOperations.Id] = [Ifrs("ProfitLossFromContinuingOperations")],
                [BasicEps.Id] = [
                    Ifrs("BasicEarningsLossPerShare", "USD/shares"),
                    Ifrs("BasicEarningsLossPerShareFromContinuingOperations", "USD/shares")
                ],
                [DilutedEps.Id] = [
                    Ifrs("DilutedEarningsLossPerShare", "USD/shares"),
                    Ifrs("DilutedEarningsLossPerShareFromContinuingOperations", "USD/shares")
                ],
                [BasicAverageShares.Id] = [Ifrs("WeightedAverageShares", "shares")],
                [DilutedAverageShares.Id] = [Ifrs("AdjustedWeightedAverageShares", "shares"), Ifrs("WeightedAverageShares", "shares")],
                [ReconciledDepreciation.Id] = [],
                [OperatingCashFlow.Id] = [Ifrs("CashFlowsFromUsedInOperatingActivities")],
                [InvestingCashFlow.Id] = [Ifrs("CashFlowsFromUsedInInvestingActivities")],
                [FinancingCashFlow.Id] = [Ifrs("CashFlowsFromUsedInFinancingActivities")],
                [Capex.Id] = [
                    Ifrs("PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities")
                ],
                [FreeCashFlow.Id] = [],
                [IssuanceOfCapitalStock.Id] = [Ifrs("ProceedsFromIssuingShares")],
                [IssuanceOfDebt.Id] = [Ifrs("ProceedsFromBorrowings")],
                [RepaymentOfDebt.Id] = [Ifrs("RepaymentsOfBorrowings")],
                [TotalAssets.Id] = [Ifrs("Assets")],
                [TotalLiabilities.Id] = [Ifrs("Liabilities")],
                [TotalEquityGrossMinorityInterest.Id] = [Ifrs("Equity"), Ifrs("EquityAttributableToOwnersOfParent")],
                [CommonStockEquity.Id] = [Ifrs("EquityAttributableToOwnersOfParent"), Ifrs("Equity")],
                [CapitalLeaseObligations.Id] = [Ifrs("LeaseLiabilities")],
                [NetTangibleAssets.Id] = [],
                [WorkingCapital.Id] = [],
                [TangibleBookValue.Id] = [],
                [CashAndShortTermEquivalents.Id] = [
                    Ifrs("CashAndCashEquivalents"),
                    Ifrs("CashAndCashEquivalentsAndShortTermInvestments")
                ],
                [TotalDebt.Id] = [Ifrs("Borrowings"), Ifrs("LoansAndBorrowings")],
                [EndCashPosition.Id] = [Ifrs("CashAndCashEquivalents")],
                [SharesOutstanding.Id] = [
                    Ifrs("NumberOfSharesOutstanding", "shares"),
                    Ifrs("NumberOfSharesIssued", "shares"),
                    new SecConceptSpec("EntityCommonStockSharesOutstanding", "shares", "dei")
                ],
                [MinorityInterest.Id] = [Ifrs("NoncontrollingInterests")],
                [NoncontrollingInterestIncome.Id] = [Ifrs("ProfitLossAttributableToNoncontrollingInterests")],
                [DividendsPaid.Id] = [Ifrs("DividendsPaidClassifiedAsFinancingActivities"), Ifrs("DividendsPaid")],
                [DeferredTaxLiabilities.Id] = [Ifrs("DeferredTaxLiabilities")],
                [ShareIssued.Id] = [Ifrs("NumberOfSharesIssued", "shares")],
                [OrdinarySharesNumber.Id] = [
                    Ifrs("NumberOfSharesOutstanding", "shares"),
                    new SecConceptSpec("EntityCommonStockSharesOutstanding", "shares", "dei")
                ]
            };

        FlowMetrics =
        [
            Revenue,
            Cogs,
            GrossProfit,
            Sga,
            SellingAndMarketingExpense,
            GeneralAndAdministrativeExpense,
            Rnd,
            TotalOperatingCost,
            OperatingExpensesMetric,
            Ebitda,
            Ebit,
            OperatingIncomeAsReported,
            InterestIncome,
            InterestExpense,
            NetNonOperatingInterestIncomeExpense,
            NetInterestIncome,
            OtherIncomeExpense,
            Tax,
            Ebt,
            TaxRateForCalcs,
            NetIncome,
            NetIncomeCommonStockholders,
            DilutedNiAvailableToCommonStockholders,
            NetIncomeContinuingOperations,
            BasicAverageShares,
            DilutedAverageShares,
            RevenuePerShareBasic, RevenuePerShareDiluted,
            CogsPerShareBasic, CogsPerShareDiluted,
            GrossProfitPerShareBasic, GrossProfitPerShareDiluted,
            OperatingExpensesPerShareBasic, OperatingExpensesPerShareDiluted,
            OperatingIncomePerShareBasic, OperatingIncomePerShareDiluted,
            PretaxIncomePerShareBasic, PretaxIncomePerShareDiluted,
            BasicEps, DilutedEps,
            ReconciledDepreciation,
            OperatingCashFlow,
            IncomeTaxPaidSupplementalData,
            InterestPaidSupplementalData,
            StockBasedCompensation,
            DeferredTax,
            ChangeInReceivables,
            ChangeInInventory,
            ChangeInPayables,
            ChangeInAccruedExpense,
            ChangeInOtherWorkingCapital,
            InvestingCashFlow,
            PurchaseOfPpe,
            SaleOfPpe,
            PurchaseOfBusiness,
            PurchaseOfInvestment,
            SaleOfInvestment,
            FinancingCashFlow,
            Capex,
            FreeCashFlow,
            IssuanceOfCapitalStock,
            IssuanceOfDebt,
            RepaymentOfDebt,
            LongTermDebtIssuance,
            ShortTermDebtIssuance,
            LongTermDebtPayments,
            ShortTermDebtPayments,
            FinanceLeasePrincipalPayments,
            NetIssuancePaymentsOfDebt,
            NetLongTermDebtIssuance,
            NetShortTermDebtIssuance,
            RepurchaseOfCapitalStock,
            DividendsPaid,
            NoncontrollingInterestIncome,
            ChangesInCash,
            EffectOfExchangeRateChanges
        ];

        PointMetrics =
        [
            TotalAssets,
            CurrentAssets,
            CashAndShortTermEquivalents,
            CashAndCashEquivalents,
            OtherShortTermInvestments,
            Receivables,
            AccountsReceivable,
            GrossAccountsReceivable,
            AllowanceForDoubtfulAccounts,
            Inventory,
            OtherCurrentAssets,
            TotalNonCurrentAssets,
            NetPpe,
            GrossPpe,
            AccumulatedDepreciationPpe,
            Goodwill,
            OtherIntangibleAssets,
            OtherNonCurrentAssets,
            TotalLiabilities,
            CurrentLiabilities,
            PayablesAndAccruedExpenses,
            Payables,
            AccountsPayable,
            CurrentAccruedExpense,
            CurrentDeferredLiabilities,
            CurrentDeferredRevenue,
            TotalNonCurrentLiabilities,
            LongTermDebtAndCapitalLease,
            LongTermDebt,
            CurrentDebt,
            LongTermCapitalLeaseObligations,
            DeferredTaxLiabilities,
            OtherCurrentLiabilities,
            OtherNonCurrentLiabilities,
            MinorityInterest,
            MezzanineEquity,
            LongTermInvestments,
            TotalEquityGrossMinorityInterest,
            CommonStockEquity,
            CapitalStock,
            PreferredStock,
            CommonStock,
            AdditionalPaidInCapital,
            RetainedEarnings,
            TreasuryStock,
            GainsLossesNotAffectingRetainedEarnings,
            OtherEquityAdjustments,
            CapitalLeaseObligations,
            NetTangibleAssets,
            WorkingCapital,
            TangibleBookValue,
            TotalDebt,
            NetDebt,
            EndCashPosition,
            SharesOutstanding,
            ShareIssued,
            OrdinarySharesNumber,
            TreasuryShares
        ];

        AllMetrics = FlowMetrics.Concat(PointMetrics).ToList();
    }

    public static readonly SecConceptSpec OperatingExpenses = UsGaap("OperatingExpenses");
    public static readonly IReadOnlyList<SecConceptSpec> DepreciationAmortizationConcepts =
    [
        UsGaap("DepreciationDepletionAndAmortization"),
        UsGaap("DepreciationDepletionAndAmortizationExpense"),
        UsGaap("DepreciationAndAmortization"),
        UsGaap("DepreciationAndAmortizationExpense"),
        UsGaap("Depreciation")
    ];

    public static readonly SecConceptSpec ShortTermInvestments = UsGaap("ShortTermInvestments");
    public static readonly IReadOnlyList<SecConceptSpec> DebtCurrentConcepts =
    [
        UsGaap("DebtCurrent"),
        UsGaap("ShortTermBorrowings"),
        UsGaap("LongTermDebtAndFinanceLeaseObligationsCurrent")
    ];

    // Current maturities of long-term debt reported as a SEPARATE line (us-gaap:LongTermDebtCurrent),
    // distinct from short-term borrowings. Total debt folds this into its current component when the
    // filer reports short-term borrowings and current maturities on separate lines (e.g. ADM), but
    // only when it is not already included by an aggregate current concept or an all-in long-term
    // concept — see CurrentMaturitiesBundledConcepts and the total-debt build in FinancialMetricsBuilder.
    public static readonly IReadOnlyList<SecConceptSpec> LongTermDebtCurrentConcepts =
    [
        UsGaap("LongTermDebtCurrent")
    ];

    // Concepts whose reported value ALREADY includes the current maturities of long-term debt, so a
    // separate LongTermDebtCurrent term must NOT be added on top (mirrors FinanceLeaseBundledConcepts):
    //   - DebtCurrent: aggregate current debt (short-term borrowings + current maturities).
    //   - LongTermDebtAndFinanceLeaseObligationsCurrent: current long-term debt + finance lease.
    //   - LongTermDebt / ...AndFinanceLeaseObligations: all-in (current + noncurrent) long-term totals.
    //   - ifrs-full:CurrentPortionOfLongtermBorrowings: IFRS aggregate current portion.
    // LongTermDebtNoncurrent is intentionally ABSENT — it is noncurrent-only, so current maturities
    // must still be added to it.
    public static readonly IReadOnlySet<string> CurrentMaturitiesBundledConcepts = new HashSet<string>(StringComparer.Ordinal)
    {
        "DebtCurrent",
        "LongTermDebtAndFinanceLeaseObligationsCurrent",
        "LongTermDebtAndCapitalLeaseObligationsCurrent",
        "LongTermDebt",
        "LongTermDebtAndFinanceLeaseObligations",
        "LongTermDebtAndCapitalLeaseObligations",
        "ifrs-full:CurrentPortionOfLongtermBorrowings"
    };

    // LongTermDebtConcepts (the standalone "Long Term Debt" display row) and
    // LongTermDebtForTotalDebtConcepts (the total-debt aggregate input) are intentionally DIFFERENT
    // lists, not duplicates — the priority orders encode different metric intent:
    //   - display prefers the noncurrent-specific concept first (LongTermDebtNoncurrent) so the row
    //     reports long-term debt only;
    //   - total-debt prefers the all-in LongTermDebt (current + noncurrent) first and additionally
    //     accepts the bundled LongTermDebtAndFinanceLeaseObligations form so a filer that reports
    //     only the bundled total is still covered.
    // Do NOT "harmonize" the two lists; that is what previously caused the bundled-finance-lease
    // double count. Concepts whose value already includes finance/capital leases are declared in
    // FinanceLeaseBundledConcepts, and total-debt must not add a separate finance-lease term on top
    // of one (see FinancialMetricsBuilder.BundlesFinanceLease).
    public static readonly IReadOnlyList<SecConceptSpec> LongTermDebtConcepts =
    [
        UsGaap("LongTermDebtNoncurrent"),
        UsGaap("LongTermDebt"),
        UsGaap("LongTermDebtAndFinanceLeaseObligationsNoncurrent")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> LongTermDebtForTotalDebtConcepts =
    [
        UsGaap("LongTermDebt"),
        UsGaap("LongTermDebtAndFinanceLeaseObligations"),
        UsGaap("LongTermDebtAndCapitalLeaseObligations"),
        UsGaap("LongTermDebtNoncurrent"),
        UsGaap("LongTermDebtAndFinanceLeaseObligationsNoncurrent"),
        UsGaap("LongTermDebtAndCapitalLeaseObligationsNoncurrent")
    ];

    // Source-concept names whose reported value already bundles finance/capital lease obligations.
    // Declared explicitly here — instead of sniffing the concept string for "FinanceLease" at
    // runtime — so the total-debt no-double-count invariant lives next to the debt concept lists and
    // cannot silently change when a concept is renamed or a new bundled concept is added. SourceName
    // form: US-GAAP concepts are bare; IFRS concepts carry the "ifrs-full:" prefix.
    public static readonly IReadOnlySet<string> FinanceLeaseBundledConcepts = new HashSet<string>(StringComparer.Ordinal)
    {
        "LongTermDebtAndFinanceLeaseObligations",
        "LongTermDebtAndFinanceLeaseObligationsNoncurrent",
        "LongTermDebtAndFinanceLeaseObligationsCurrent",
        "LongTermDebtAndCapitalLeaseObligations",
        "LongTermDebtAndCapitalLeaseObligationsNoncurrent",
        "LongTermDebtAndCapitalLeaseObligationsCurrent",
        "ifrs-full:NoncurrentFinanceLeaseLiabilities",
        "ifrs-full:CurrentFinanceLeaseLiabilities"
    };

    public static readonly IReadOnlyList<SecConceptSpec> FinanceLeaseInterestPaymentConcepts =
    [
        UsGaap("FinanceLeaseInterestPaymentOnLiability")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> OperatingLeaseRightOfUseAssetConcepts =
    [
        UsGaap("OperatingLeaseRightOfUseAsset")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> CurrentAssetsConcepts =
    [
        UsGaap("AssetsCurrent")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> CurrentLiabilitiesConcepts =
    [
        UsGaap("LiabilitiesCurrent")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> GoodwillAndIntangibleAssetsConcepts =
    [
        UsGaap("GoodwillAndIntangibleAssetsNet")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> IntangibleAssetsConcepts =
    [
        UsGaap("FiniteLivedIntangibleAssetsNet"),
        UsGaap("IndefiniteLivedIntangibleAssetsExcludingGoodwill"),
        UsGaap("IntangibleAssetsNetExcludingGoodwill")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> GoodwillConcepts =
    [
        UsGaap("Goodwill")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> CapitalLeaseCurrentConcepts =
    [
        UsGaap("FinanceLeaseLiabilityCurrent"),
        UsGaap("CapitalLeaseObligationsCurrent")
    ];

    public static readonly IReadOnlyList<SecConceptSpec> CapitalLeaseNoncurrentConcepts =
    [
        UsGaap("FinanceLeaseLiabilityNoncurrent"),
        UsGaap("CapitalLeaseObligationsNoncurrent")
    ];

    /// <summary>
    /// Stock-split ratio. Not a metric: it puts a nine-month share average on the full year's
    /// basis when a split lands between the two.
    /// </summary>
    public static readonly SecConceptSpec StockSplitConversionRatio = UsGaap("StockholdersEquityNoteStockSplitConversionRatio1", unit: "pure");

    public static IReadOnlyList<SecConceptSpec> RequiredConcepts =>
        RequiredConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(RequiredConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    public static IReadOnlyList<SecConceptSpec> RequiredConceptsFor(FinancialMetricProvider provider) =>
        AllMetrics
            .SelectMany(m => DirectConceptsFor(m, provider))
            .Concat(OperatingExpensesConceptsFor(provider))
            .Concat(ShortTermInvestmentsConceptsFor(provider))
            .Concat(DepreciationAmortizationConceptsFor(provider))
            .Concat(DebtCurrentConceptsFor(provider))
            .Concat(LongTermDebtCurrentConceptsFor(provider))
            .Concat(LongTermDebtConceptsFor(provider))
            .Concat(LongTermDebtForTotalDebtConceptsFor(provider))
            .Concat(CurrentAssetsConceptsFor(provider))
            .Concat(CurrentLiabilitiesConceptsFor(provider))
            .Concat(GoodwillAndIntangibleAssetsConceptsFor(provider))
            .Concat(GoodwillConceptsFor(provider))
            .Concat(IntangibleAssetsConceptsFor(provider))
            .Concat(CapitalLeaseCurrentConceptsFor(provider))
            .Concat(CapitalLeaseNoncurrentConceptsFor(provider))
            .Concat(FinanceLeaseInterestPaymentConceptsFor(provider))
            .Concat(OperatingLeaseRightOfUseAssetConceptsFor(provider))
            .Concat(provider is FinancialMetricProvider.UsGaap ? new[] { StockSplitConversionRatio } : [])
            .Concat(provider == FinancialMetricProvider.UsGaap ? [UsGaap("InterestIncomeExpenseNet"), UsGaap("NoninterestIncome")] : [])
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    public static IReadOnlyList<SecConceptSpec> DirectConceptsFor(FinancialMetricDefinition metric, FinancialMetricProvider provider)
    {
        if (provider == FinancialMetricProvider.IfrsFull)
            return IfrsDirectConcepts.TryGetValue(metric.Id, out var concepts) ? concepts : [];

        return provider == FinancialMetricProvider.UsGaap ? metric.DirectConcepts : [];
    }

    public static IReadOnlyList<SecConceptSpec> OperatingExpensesConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("OperatingExpenses")],
            FinancialMetricProvider.UsGaap => [OperatingExpenses],
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> DepreciationAmortizationConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [
                Ifrs("AdjustmentsForDepreciationAndAmortisationExpense"),
                Ifrs("DepreciationAndAmortisationExpense"),
                Ifrs("DepreciationAndAmortisation")
            ],
            FinancialMetricProvider.UsGaap => DepreciationAmortizationConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> ShortTermInvestmentsConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("CurrentInvestments"), Ifrs("ShorttermInvestments")],
            FinancialMetricProvider.UsGaap => [ShortTermInvestments, UsGaap("MarketableSecuritiesCurrent")],
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> CashCombinedConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("CashAndCashEquivalentsAndShortTermInvestments")],
            FinancialMetricProvider.UsGaap => [UsGaap("CashCashEquivalentsAndShortTermInvestments")],
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> CashComponentConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("CashAndCashEquivalents")],
            FinancialMetricProvider.UsGaap => [
                UsGaap("CashAndCashEquivalentsAtCarryingValue"),
                UsGaap("CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents")
            ],
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> DebtDirectConceptsFor(FinancialMetricProvider provider) =>
        DirectConceptsFor(TotalDebt, provider);

    public static IReadOnlyList<SecConceptSpec> DebtCurrentConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [
                Ifrs("CurrentBorrowings"),
                Ifrs("CurrentPortionOfLongtermBorrowings"),
                Ifrs("CurrentLeaseLiabilities"),
                Ifrs("CurrentFinanceLeaseLiabilities")
            ],
            FinancialMetricProvider.UsGaap => DebtCurrentConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> LongTermDebtCurrentConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.UsGaap => LongTermDebtCurrentConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> LongTermDebtConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [
                Ifrs("NoncurrentBorrowings"),
                Ifrs("LongtermBorrowings"),
                Ifrs("NoncurrentLeaseLiabilities"),
                Ifrs("NoncurrentFinanceLeaseLiabilities")
            ],
            FinancialMetricProvider.UsGaap => LongTermDebtConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> LongTermDebtForTotalDebtConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [
                Ifrs("LongtermBorrowings"),
                Ifrs("NoncurrentBorrowings"),
                Ifrs("NoncurrentLeaseLiabilities"),
                Ifrs("NoncurrentFinanceLeaseLiabilities")
            ],
            FinancialMetricProvider.UsGaap => LongTermDebtForTotalDebtConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> FinanceLeaseInterestPaymentConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.UsGaap => FinanceLeaseInterestPaymentConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> OperatingLeaseRightOfUseAssetConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.UsGaap => OperatingLeaseRightOfUseAssetConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> CurrentAssetsConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("CurrentAssets")],
            FinancialMetricProvider.UsGaap => CurrentAssetsConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> CurrentLiabilitiesConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("CurrentLiabilities")],
            FinancialMetricProvider.UsGaap => CurrentLiabilitiesConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> GoodwillAndIntangibleAssetsConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("GoodwillAndIntangibleAssets")],
            FinancialMetricProvider.UsGaap => GoodwillAndIntangibleAssetsConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> GoodwillConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("Goodwill")],
            FinancialMetricProvider.UsGaap => GoodwillConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> IntangibleAssetsConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("IntangibleAssetsOtherThanGoodwill")],
            FinancialMetricProvider.UsGaap => IntangibleAssetsConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> CapitalLeaseCurrentConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("CurrentLeaseLiabilities"), Ifrs("CurrentFinanceLeaseLiabilities")],
            FinancialMetricProvider.UsGaap => CapitalLeaseCurrentConcepts,
            _ => []
        };

    public static IReadOnlyList<SecConceptSpec> CapitalLeaseNoncurrentConceptsFor(FinancialMetricProvider provider) =>
        provider switch
        {
            FinancialMetricProvider.IfrsFull => [Ifrs("NoncurrentLeaseLiabilities"), Ifrs("NoncurrentFinanceLeaseLiabilities")],
            FinancialMetricProvider.UsGaap => CapitalLeaseNoncurrentConcepts,
            _ => []
        };

    public static FinancialMetricDefinition Definition(string metricId) =>
        AllMetrics.First(m => m.Id == metricId);

    public static SecConceptSpec UsGaap(string concept, string unit = "USD") => new(concept, unit);
    public static SecConceptSpec Ifrs(string concept, string unit = "USD") => new(concept, unit, "ifrs-full");
}
