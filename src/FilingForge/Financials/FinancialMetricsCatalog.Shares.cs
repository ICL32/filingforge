namespace FilingForge;

public static partial class FinancialMetricsCatalog
{
    public static readonly FinancialMetricDefinition SharesOutstanding = new(
        "sharesOutstanding",
        "Shares Outstanding",
        FinancialMetricKind.Point,
        FinancialMetricGroup.Shares,
        CostCannotBeNegative: true,
        "Common shares outstanding.",
        "Blank when no shares outstanding concept exists, or when the direct value is negative.",
        [UsGaap("CommonStockSharesOutstanding", "shares"), new SecConceptSpec("EntityCommonStockSharesOutstanding", "shares", "dei")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition ShareIssued = new(
        "shareIssued",
        "Share Issued",
        FinancialMetricKind.Point,
        FinancialMetricGroup.Shares,
        CostCannotBeNegative: true,
        "Common shares issued.",
        "Blank when no issued-share concept exists, or when the direct value is negative.",
        [UsGaap("CommonStockSharesIssued", "shares")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition OrdinarySharesNumber = new(
        "ordinarySharesNumber",
        "Ordinary Shares Number",
        FinancialMetricKind.Point,
        FinancialMetricGroup.Shares,
        CostCannotBeNegative: true,
        "Ordinary/common shares outstanding.",
        "Blank when no ordinary/common shares outstanding concept exists, or when the direct value is negative.",
        [UsGaap("CommonStockSharesOutstanding", "shares"), new SecConceptSpec("EntityCommonStockSharesOutstanding", "shares", "dei")],
        [FinancialMetricFormulaIds.DirectFact]
    );

    public static readonly FinancialMetricDefinition TreasuryShares = new(
        "treasuryShares",
        "Treasury Shares Number",
        FinancialMetricKind.Point,
        FinancialMetricGroup.Shares,
        CostCannotBeNegative: true,
        "Treasury shares, derived from issued shares less outstanding shares.",
        "Blank when issued or outstanding share inputs are missing, or when the derived value is negative.",
        [],
        [FinancialMetricFormulaIds.CommonStockSharesIssuedMinusOutstanding]
    );
}
