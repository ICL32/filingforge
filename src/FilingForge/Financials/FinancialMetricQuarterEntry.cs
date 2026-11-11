namespace FilingForge;

internal sealed record FinancialMetricQuarterEntry(
    string End,
    int? FiscalYear,
    int? FiscalQuarter,
    FinancialMetricValue Value,
    IReadOnlyList<string> ValidationMessages
);
