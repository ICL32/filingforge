using FilingForge.Models;

namespace FilingForge;

// A SEC companyfacts payload reduced to the financial-form facts the metric builders read,
// keyed taxonomy -> concept -> unit. Built by streaming the raw JSON (see
// CompanyFactsStreamReader) so a 20-100 MB companyfacts file never materializes as a whole
// JsonDocument (its full UTF-8 bytes plus metadata DB) — parsing many of those whole documents
// at once would need gigabytes of memory. Memory here is bounded to the retained
// financial facts, which are a small fraction of the raw JSON.
public sealed class CompanyFacts
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ConceptFacts>> _facts;

    internal CompanyFacts(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ConceptFacts>> facts,
        IReadOnlyList<string> forms)
    {
        _facts = facts;
        Forms = forms;
    }

    internal static readonly CompanyFacts Empty = new(
        new Dictionary<string, IReadOnlyDictionary<string, ConceptFacts>>(StringComparer.Ordinal),
        []);

    // All filing form codes seen across every entry, pre-financial-form filter, to match the
    // previous JsonElement ReadForms behavior used by the companyfacts profile.
    internal IReadOnlyList<string> Forms { get; }

    // True when the payload carried a non-empty `facts` object. Matches the old
    // `facts.EnumerateObject().Any()` gate: taxonomy keys are retained even when all their
    // concepts filter down to zero financial facts.
    internal bool HasAnyFacts => _facts.Count > 0;

    internal IReadOnlyList<string> Taxonomies =>
        _facts.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();

    // Financial-form facts for a taxonomy/concept/unit, or empty. The concept label is applied by
    // the caller (SourceName for spec reads, raw concept for direct reads) so FinancialPoint.Concept
    // semantics match the previous reader exactly.
    internal IReadOnlyList<RawFact> Raw(string taxonomy, string concept, string unit) =>
        _facts.TryGetValue(taxonomy, out var concepts)
        && concepts.TryGetValue(concept, out var conceptFacts)
        && conceptFacts.Units.TryGetValue(unit, out var facts)
            ? facts
            : [];
}

// One concept's retained facts: unit -> financial-form facts.
internal sealed class ConceptFacts
{
    internal IReadOnlyDictionary<string, IReadOnlyList<RawFact>> Units { get; init; }
        = new Dictionary<string, IReadOnlyList<RawFact>>(StringComparer.Ordinal);
}

// A single companyfacts unit entry, parsed to scalars (no concept label). Mirrors the field set
// ReadFinancialPoints extracted; the concept label is attached when converted to a FinancialPoint.
internal readonly record struct RawFact(
    string? Start,
    string End,
    double Val,
    string Form,
    string Filed,
    int? Fy,
    string? Fp,
    string? Frame,
    string? Accn)
{
    internal FinancialPoint ToPoint(string concept) =>
        new(Start, End, Val, Form, Filed, concept, Fy, Fp, Frame, Accn);
}
