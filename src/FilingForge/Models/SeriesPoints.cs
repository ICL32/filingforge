namespace FilingForge;

public record TtmPoint(string End, double Ttm, string? Filed = null, string? SourceConcepts = null, string? Derivation = null, string? FirstFiled = null, string? Accessions = null, IReadOnlyList<QuarterlySeriesBuilder.QuarterlyPoint>? Quarters = null);

public record SharePoint(string End, double Val, string? Filed = null, string? SourceConcept = null, string? Accession = null, string? FirstFiled = null);
