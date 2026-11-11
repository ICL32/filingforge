namespace FilingForge;

using static FinancialMetricDerivations;

/// <summary>
/// Divides income-statement lines by weighted-average shares, and carries quarterly share
/// counts into the TTM view (an average share count is not summed across quarters).
/// </summary>
internal static class FinancialMetricPerShareBuilder
{
    private sealed record Ratio(
        FinancialMetricDefinition Result,
        FinancialMetricDefinition Line,
        FinancialMetricDefinition Shares,
        string LineRole,
        string Label);

    private static readonly IReadOnlyList<Ratio> Ratios = BuildRatios();

    private static List<Ratio> BuildRatios()
    {
        var lines = new (FinancialMetricDefinition Line, string Role, string Name, FinancialMetricDefinition Basic, FinancialMetricDefinition Diluted)[]
        {
            (FinancialMetricsCatalog.Revenue, "revenue", "Revenue",
                FinancialMetricsCatalog.RevenuePerShareBasic, FinancialMetricsCatalog.RevenuePerShareDiluted),
            (FinancialMetricsCatalog.Cogs, "costOfRevenue", "Cost of Revenue",
                FinancialMetricsCatalog.CogsPerShareBasic, FinancialMetricsCatalog.CogsPerShareDiluted),
            (FinancialMetricsCatalog.GrossProfit, "grossProfit", "Gross Profit",
                FinancialMetricsCatalog.GrossProfitPerShareBasic, FinancialMetricsCatalog.GrossProfitPerShareDiluted),
            (FinancialMetricsCatalog.OperatingExpensesMetric, "operatingExpenses", "Operating Expenses",
                FinancialMetricsCatalog.OperatingExpensesPerShareBasic, FinancialMetricsCatalog.OperatingExpensesPerShareDiluted),
            (FinancialMetricsCatalog.Ebit, "operatingIncome", "Operating Income",
                FinancialMetricsCatalog.OperatingIncomePerShareBasic, FinancialMetricsCatalog.OperatingIncomePerShareDiluted),
            (FinancialMetricsCatalog.Ebt, "pretaxIncome", "Pretax Income",
                FinancialMetricsCatalog.PretaxIncomePerShareBasic, FinancialMetricsCatalog.PretaxIncomePerShareDiluted),
        };

        var ratios = new List<Ratio>();
        foreach (var (line, role, name, basic, diluted) in lines)
        {
            ratios.Add(new Ratio(basic, line, FinancialMetricsCatalog.BasicAverageShares, role, $"{name} / basic weighted-average shares"));
            ratios.Add(new Ratio(diluted, line, FinancialMetricsCatalog.DilutedAverageShares, role, $"{name} / diluted weighted-average shares"));
        }

        return ratios;
    }

    /// <summary>Adds every per-share metric to <paramref name="metrics"/> (one view: quarterly, annual or TTM).</summary>
    internal static void AddDerivedMetrics(Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> metrics)
    {
        var none = new Dictionary<string, FinancialMetricValue>(StringComparer.Ordinal);
        foreach (var ratio in Ratios)
        {
            metrics[ratio.Result.Id] = Divide(
                ratio,
                metrics.GetValueOrDefault(ratio.Line.Id) ?? none,
                metrics.GetValueOrDefault(ratio.Shares.Id) ?? none);
        }
    }

    /// <summary>
    /// TTM view of an average-share metric: the quarter's own value, kept only where it is a
    /// positive finite count.
    /// </summary>
    internal static IReadOnlyDictionary<string, FinancialMetricValue> CarryQuarterlyShares(
        FinancialMetricDefinition metric,
        IReadOnlyDictionary<string, FinancialMetricValue> quarterly)
    {
        var formula = metric.Id == FinancialMetricsCatalog.BasicAverageShares.Id
            ? FinancialMetricFormulaIds.QuarterlyBasicAverageSharesCarriedToTtm
            : FinancialMetricFormulaIds.QuarterlyDilutedAverageSharesCarriedToTtm;

        var carried = new Dictionary<string, FinancialMetricValue>(StringComparer.Ordinal);
        foreach (var (end, q) in quarterly)
        {
            if (q.Value is not { } shares || shares <= 0 || !double.IsFinite(shares))
                continue;

            carried[end] = q with
            {
                Formula = formula,
                Derivation = "derived",
                SourceType = "derived",
                SourceLabel = $"TTM {metric.DisplayName} uses the period's quarterly value",
                Inputs = [TraceInput("quarterlyShares", end, q)],
            };
        }

        return carried;
    }

    private static Dictionary<string, FinancialMetricValue> Divide(
        Ratio ratio,
        IReadOnlyDictionary<string, FinancialMetricValue> line,
        IReadOnlyDictionary<string, FinancialMetricValue> shares)
    {
        var result = new Dictionary<string, FinancialMetricValue>(StringComparer.Ordinal);
        foreach (var end in SortedEnds(line.Keys, shares.Keys))
        {
            if (!line.TryGetValue(end, out var top) || top.Value is not { } numerator
                || !shares.TryGetValue(end, out var bottom) || bottom.Value is not { } denominator)
                continue;
            if (!double.IsFinite(numerator) || !double.IsFinite(denominator) || denominator <= 0)
                continue;

            var perShare = numerator / denominator;
            if (!double.IsFinite(perShare))
                continue;

            FinancialMetricValue[] parts = [top, bottom];
            result[end] = new FinancialMetricValue(
                perShare,
                ratio.Result.FallbackOrder[0],
                JoinSources(parts),
                LatestFiled(parts),
                JoinAccessions(parts),
                "derived",
                "derived",
                null,
                ratio.Label,
                [TraceInput(ratio.LineRole, end, top), TraceInput(ShareRole(ratio.Shares), end, bottom)]);
        }

        return result;
    }

    private static string ShareRole(FinancialMetricDefinition shares) =>
        shares.Id == FinancialMetricsCatalog.BasicAverageShares.Id ? "basicAverageShares" : "dilutedAverageShares";
}
