namespace FilingForge.Models;

// One historical value a period was reported at, tied to the SEC filing date the market saw it.
// Ordered ascending by Filed. Used only to give the valuation chart point-in-time (as-known) values
// across a material restatement; see FinancialPoint.Versions.
public record FinancialPointVersion(string Filed, double Val, string? Accn = null, string? Concept = null);

public record FinancialPoint(
    string? Start,
    string End,
    double Val,
    string Form,
    string Filed,
    string? Concept = null,
    int? Fy = null,
    string? Fp = null,
    string? Frame = null,
    string? Accn = null,
    string SourceType = "companyfacts",
    string? SourceDocument = null,
    string? SourceLabel = null,
    int SourcePrecision = 0,
    // Earliest SEC filing date that reported this period, across all restatements/comparatives.
    // `Filed` stays the latest restatement's date (value provenance); `FirstFiled` records when the
    // market FIRST saw the period so valuation time-series alignment is not dragged forward by a
    // later comparative re-filing (a FY-N 10-K carries FY-N-1/N-2 comparatives at a much later date).
    // Null on facts/snapshots predating this field; consumers fall back to `Filed`.
    string? FirstFiled = null,
    // Restatement history for this period: each materially-different (>0.5%) value the period was
    // reported at, keyed on the SEC filing date the market saw it, ascending by Filed. Populated ONLY
    // when a later different-accession filing materially changed the value (a true restatement, not an
    // identical comparative re-filing or sub-0.5% reclassification). `Val` stays the latest restated
    // value (export/statements unchanged); `Versions` lets the valuation chart show the as-known value
    // per price bar instead of leaking the final restatement backward onto pre-restatement bars.
    // Null for non-restated periods and older snapshots — consumers fall back to the single `Val`.
    IReadOnlyList<FinancialPointVersion>? Versions = null
);
