namespace FilingForge.Models;

public record Fundamentals(
    List<FinancialPoint> Eps,
    List<FinancialPoint> RevenuePoints,
    List<FinancialPoint> SharesOutstanding,
    List<FinancialPoint> GrossProfitPoints,
    List<FinancialPoint> OperatingIncomePoints,
    List<FinancialPoint> OperatingCashFlowPoints,
    List<FinancialPoint> CapexPoints,
    List<FinancialPoint> NetIncomeLossPoints,
    List<FinancialPoint> RdExpensePoints,
    List<FinancialPoint> CurrentAssetsPoints,
    List<FinancialPoint> CurrentLiabilitiesPoints,
    List<FinancialPoint> EquityPoints
);
