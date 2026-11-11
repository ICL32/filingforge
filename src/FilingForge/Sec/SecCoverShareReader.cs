using System.Globalization;
using System.Text.RegularExpressions;
using FilingForge.Models;

namespace FilingForge;

// Parses cover-page dei:EntityCommonStockSharesOutstanding inline-XBRL facts from the
// primary document of a periodic filing (10-K/10-Q).
//
// SEC `companyfacts` strips share-class dimensions from instant share facts. Dual-class
// registrants whose share facts are ALL dimensioned by share class (e.g. ERIE Class A +
// Class B) therefore lose every outstanding-share fact from companyfacts, leaving only
// undimensioned leaks such as a single stale per-class weighted-average value. The filing
// cover page reports outstanding shares per class undimensioned in the fact tag (the class
// distinction lives in the XBRL context), so it is the reliable source. Per-class values at
// the latest cover instant are summed, matching the multi-class share policy in
// FinancialPointHelpers.ChooseSharePoint.
public static class SecCoverShareReader
{
    // dei SourceName convention matches SecConceptSpec.SourceName for the dei taxonomy
    // ("dei:EntityCommonStockSharesOutstanding"), so the merged fact is recognized as a
    // point-in-time outstanding-share concept downstream.
    private const string CoverShareConcept = "dei:EntityCommonStockSharesOutstanding";
    private const string CoverShareIxName = "dei:EntityCommonStockSharesOutstanding";

    public static FinancialPoint? ReadOutstandingShares(SecFilingDocument document, string? ticker = null)
    {
        if (string.IsNullOrEmpty(document.Content))
            return null;

        var contexts = SecIxbrlParsing.ParseContexts(document.Content);
        if (contexts.Count == 0)
            return null;

        // One value per share class, keyed by the context's StatementClassOfStockAxis member
        // (falling back to the context id when the cover is not class-dimensioned). Last tag
        // wins for a repeated class so duplicated cover tags do not double count; distinct
        // classes are real share classes and are summed at the latest cover instant.
        var byClass = new Dictionary<string, (string Instant, double Value, string? ClassMember)>(StringComparer.Ordinal);
        foreach (Match fact in SecIxbrlParsing.NonFractionFacts(document.Content))
        {
            var attributes = SecIxbrlParsing.ParseAttributes(fact.Groups["attrs"].Value);
            if (!attributes.TryGetValue("name", out var name)) continue;
            if (!string.Equals(name, CoverShareIxName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!attributes.TryGetValue("contextRef", out var contextRef)) continue;
            if (!contexts.TryGetValue(contextRef, out var context)) continue;

            attributes.TryGetValue("scale", out var scale);
            if (!TryParseShares(SecIxbrlParsing.HtmlText(fact.Groups["value"].Value), SecIxbrlParsing.ScaleMultiplier(scale ?? ""), out var value))
                continue;
            if (value <= 0) continue;

            var key = context.ClassMember ?? contextRef;
            byClass[key] = (context.End, value, context.ClassMember);
        }

        if (byClass.Count == 0)
            return null;

        var latestInstant = byClass.Values
            .Select(entry => entry.Instant)
            .Max(StringComparer.Ordinal);

        // Traded-class-equivalent total: per-class count x its conversion ratio (1.0 for the
        // voting-only / single-class default, so unchanged for ERIE/HEI/GOOGL).
        var total = byClass.Values
            .Where(entry => string.Equals(entry.Instant, latestInstant, StringComparison.Ordinal))
            .Sum(entry => entry.Value * EconomicShareClassRatios.RatioFor(ticker, entry.ClassMember));

        if (total <= 0)
            return null;

        return new FinancialPoint(
            Start: null,
            End: latestInstant!,
            Val: total,
            Form: document.Form,
            Filed: document.FilingDate,
            Concept: CoverShareConcept,
            Fy: DateOnly.TryParse(latestInstant, out var instantDate) ? instantDate.Year : null,
            Fp: null,
            Frame: null,
            Accn: document.Accession,
            SourceType: "filing-cover",
            SourceDocument: document.FileName,
            SourceLabel: "Cover page shares outstanding");
    }

    private static bool TryParseShares(string text, double scale, out double value)
    {
        value = 0;
        var cleaned = SeparatorRegex.Replace(text, "");
        if (cleaned.Length == 0)
            return false;
        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;
        value = parsed * scale;
        return true;
    }

    private static readonly Regex SeparatorRegex = new(@"[,\s]", RegexOptions.Compiled);
}
