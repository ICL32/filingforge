using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace FilingForge;

// Shared inline-XBRL (iXBRL) parsing primitives for SEC filing documents: context-date
// extraction, fact enumeration, attribute parsing, HTML-text normalization, and numeric
// scaling. Owned in one place so the statement parser (SecFilingStatementParser) and the
// cover-page share reader (SecCoverShareReader) cannot drift on the iXBRL grammar.
internal static partial class SecIxbrlParsing
{
    /// <summary>A parsed XBRL context.</summary>
    /// <param name="ClassMember">Share-class member (StatementClassOfStockAxis), e.g. "us-gaap:CommonClassAMember"; null when absent.</param>
    /// <param name="IsSegmented">True when the context names any axis member at all (segment, product, region, share class...).</param>
    internal readonly record struct IxContext(string? Start, string End, string? ClassMember, bool IsSegmented);

    // Parses XBRL contexts into id -> (Start, End, ClassMember). End is the duration end date
    // or, for instant contexts, the instant date. Contexts without a usable period are skipped.
    internal static Dictionary<string, IxContext> ParseContexts(string content)
    {
        var contexts = new Dictionary<string, IxContext>(StringComparer.Ordinal);
        foreach (Match match in ContextRegex().Matches(content))
        {
            var id = match.Groups["id"].Value;
            var body = match.Groups["body"].Value;
            var start = FirstGroup(StartDateRegex().Match(body), "date");
            var instant = FirstGroup(InstantDateRegex().Match(body), "date");
            var end = FirstGroup(EndDateRegex().Match(body), "date") ?? instant;
            var classMember = FirstGroup(ClassMemberRegex().Match(body), "member")?.Trim();
            if (string.IsNullOrWhiteSpace(id) || end is null || (start is null && instant is null))
                continue;

            var shareClass = string.IsNullOrWhiteSpace(classMember) ? null : classMember;
            contexts[id] = new IxContext(start, end, shareClass, AnyAxisMemberRegex().IsMatch(body));
        }

        return contexts;
    }

    internal static IEnumerable<Match> NonFractionFacts(string content) =>
        IxNonFractionRegex().Matches(content);

    internal static Dictionary<string, string> ParseAttributes(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex().Matches(value))
        {
            var rawValue = match.Groups["double"].Success
                ? match.Groups["double"].Value
                : match.Groups["single"].Value;
            result[match.Groups["name"].Value] = WebUtility.HtmlDecode(rawValue);
        }

        return result;
    }

    internal static string HtmlText(string value)
    {
        var noTags = TagRegex().Replace(value, " ");
        return WebUtility.HtmlDecode(WhitespaceRegex().Replace(noTags, " ")).Trim();
    }

    internal static double ScaleMultiplier(string scale) =>
        int.TryParse(scale, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Pow(10, parsed)
            : 1;

    internal static string? FirstGroup(Match match, string name) =>
        match.Success ? match.Groups[name].Value : null;

    [GeneratedRegex(@"<(?:xbrli:)?context[^>]*\bid\s*=\s*[""'](?<id>[^""']+)[""'][^>]*>(?<body>.*?)</(?:xbrli:)?context>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ContextRegex();

    [GeneratedRegex(@"<xbrli:startDate>\s*(?<date>\d{4}-\d{2}-\d{2})\s*</xbrli:startDate>", RegexOptions.IgnoreCase)]
    private static partial Regex StartDateRegex();

    [GeneratedRegex(@"<xbrli:endDate>\s*(?<date>\d{4}-\d{2}-\d{2})\s*</xbrli:endDate>", RegexOptions.IgnoreCase)]
    private static partial Regex EndDateRegex();

    [GeneratedRegex(@"<xbrli:instant>\s*(?<date>\d{4}-\d{2}-\d{2})\s*</xbrli:instant>", RegexOptions.IgnoreCase)]
    private static partial Regex InstantDateRegex();

    [GeneratedRegex(@"<(?:\w+:)?explicitMember\b[^>]*\bdimension\s*=\s*[""'][^""']*StatementClassOfStockAxis[""'][^>]*>\s*(?<member>[^<\s]+)\s*</(?:\w+:)?explicitMember>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ClassMemberRegex();

    // Opening tag of an explicit or typed dimension member, any namespace prefix.
    [GeneratedRegex(@"<(?:\w+:)?(?:typed|explicit)Member\b", RegexOptions.IgnoreCase)]
    private static partial Regex AnyAxisMemberRegex();

    [GeneratedRegex(@"<ix:nonFraction(?<attrs>[^>]*)>(?<value>.*?)</ix:nonFraction>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IxNonFractionRegex();

    [GeneratedRegex(@"\b(?<name>[A-Za-z_:][\w:.-]*)\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)')", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
