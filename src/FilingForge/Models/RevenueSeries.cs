namespace FilingForge.Models;

public record RevenueQuarterPoint(
    string End,
    string Label,
    int FiscalYear,
    int FiscalQuarter,
    double QuarterlyRevenue,
    double? TtmRevenue,
    double? RevenueYoyPct,
    string Filed,
    string? SourceConcept,
    string Derivation,
    string ValidationStatus,
    IReadOnlyList<string> ValidationMessages
);

public record RevenueValidationSummary(
    int QuarterCount,
    int TtmCount,
    int WarningCount
);

public record RevenueSeries(
    string Ticker,
    IReadOnlyList<RevenueQuarterPoint> Quarters,
    RevenueValidationSummary Validation
);

public record StatementProvenance(
    string Provider,
    string Taxonomy,
    string Formula,
    string SourceConcepts,
    string? Filed,
    string? Accession,
    string SourceType,
    string? SourceDocument,
    string? SourceLabel
);

public record StatementPeriodRow(
    string PeriodEnd,
    string Label,
    int? FiscalYear,
    int? FiscalQuarter,
    double? Revenue,
    double? NetIncome,
    double? GrossProfit,
    double? OperatingProfit,
    double? OperatingCashFlow,
    double? Capex,
    double? FreeCashFlow,
    double? TotalAssets,
    double? TotalLiabilities,
    double? CashAndShortTermEquivalents,
    double? TotalDebt,
    IReadOnlyDictionary<string, StatementProvenance> Provenance,
    string ValidationStatus,
    IReadOnlyList<string> ValidationMessages
);

public record StatementValidationSummary(
    int QuarterlyCount,
    int AnnualCount,
    int WarningCount
);

public record StatementSeries(
    string Ticker,
    IReadOnlyList<StatementPeriodRow> Quarterly,
    IReadOnlyList<StatementPeriodRow> Annual,
    StatementValidationSummary Validation
);
