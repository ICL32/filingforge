namespace FilingForge;

public static partial class FinancialMetricsCatalog
{
    // Income-statement lines expressed per weighted-average share, each with a basic and a diluted
    // variant. The values themselves are computed by FinancialMetricPerShareBuilder.

    public static readonly FinancialMetricDefinition RevenuePerShareBasic =
        PerShareMetric("revenuePerShareBasic", "Revenue", "revenue", basic: true, FinancialMetricFormulaIds.RevenueDividedByBasicAverageShares);
    public static readonly FinancialMetricDefinition RevenuePerShareDiluted =
        PerShareMetric("revenuePerShareDiluted", "Revenue", "revenue", basic: false, FinancialMetricFormulaIds.RevenueDividedByDilutedAverageShares);
    public static readonly FinancialMetricDefinition CogsPerShareBasic =
        PerShareMetric("cogsPerShareBasic", "Cost of Revenue", "cost of revenue", basic: true, FinancialMetricFormulaIds.CogsDividedByBasicAverageShares);
    public static readonly FinancialMetricDefinition CogsPerShareDiluted =
        PerShareMetric("cogsPerShareDiluted", "Cost of Revenue", "cost of revenue", basic: false, FinancialMetricFormulaIds.CogsDividedByDilutedAverageShares);
    public static readonly FinancialMetricDefinition GrossProfitPerShareBasic =
        PerShareMetric("grossProfitPerShareBasic", "Gross Profit", "gross profit", basic: true, FinancialMetricFormulaIds.GrossProfitDividedByBasicAverageShares);
    public static readonly FinancialMetricDefinition GrossProfitPerShareDiluted =
        PerShareMetric("grossProfitPerShareDiluted", "Gross Profit", "gross profit", basic: false, FinancialMetricFormulaIds.GrossProfitDividedByDilutedAverageShares);
    public static readonly FinancialMetricDefinition OperatingExpensesPerShareBasic =
        PerShareMetric("operatingExpensesPerShareBasic", "Operating Expenses", "total operating expenses", basic: true, FinancialMetricFormulaIds.OperatingExpensesDividedByBasicAverageShares);
    public static readonly FinancialMetricDefinition OperatingExpensesPerShareDiluted =
        PerShareMetric("operatingExpensesPerShareDiluted", "Operating Expenses", "total operating expenses", basic: false, FinancialMetricFormulaIds.OperatingExpensesDividedByDilutedAverageShares);
    public static readonly FinancialMetricDefinition OperatingIncomePerShareBasic =
        PerShareMetric("operatingIncomePerShareBasic", "Operating Income", "operating income (EBIT)", basic: true, FinancialMetricFormulaIds.OperatingIncomeDividedByBasicAverageShares);
    public static readonly FinancialMetricDefinition OperatingIncomePerShareDiluted =
        PerShareMetric("operatingIncomePerShareDiluted", "Operating Income", "operating income (EBIT)", basic: false, FinancialMetricFormulaIds.OperatingIncomeDividedByDilutedAverageShares);
    public static readonly FinancialMetricDefinition PretaxIncomePerShareBasic =
        PerShareMetric("pretaxIncomePerShareBasic", "Pretax Income", "pretax income (EBT)", basic: true, FinancialMetricFormulaIds.PretaxIncomeDividedByBasicAverageShares);
    public static readonly FinancialMetricDefinition PretaxIncomePerShareDiluted =
        PerShareMetric("pretaxIncomePerShareDiluted", "Pretax Income", "pretax income (EBT)", basic: false, FinancialMetricFormulaIds.PretaxIncomeDividedByDilutedAverageShares);

    private static FinancialMetricDefinition PerShareMetric(string id, string line, string lineInSentence, bool basic, string formula)
    {
        var shareKind = basic ? "Basic" : "Diluted";
        return new FinancialMetricDefinition(
            id,
            $"{line} per Share {shareKind}",
            FinancialMetricKind.Flow,
            FinancialMetricGroup.IncomeStatement,
            CostCannotBeNegative: false,
            $"Same-period {lineInSentence} over the {shareKind.ToLowerInvariant()} weighted-average share count.",
            "Left empty unless both the line item and the share count exist for the period and the share count is a positive, finite number.",
            [],
            [formula],
            FinancialMetricTtmPolicy.Blank);
    }
}
