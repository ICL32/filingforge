using FilingForge.Models;
using Microsoft.Extensions.Logging;

namespace FilingForge;

/// <summary>
/// Fills gaps in the XBRL company-facts feed by reading the statement tables of the filings
/// themselves. A filing is only opened when the feed is missing something for its report date.
/// </summary>
internal static class SecFilingFallbackService
{
    // How far back (in filings) the statement gap check looks, and how many filings are
    // requested from the document source per round trip.
    private const int StatementFallbackFilingLimit = 72;
    private const int DocumentRequestSize = 8;
    private const int EpsFallbackFilingLimit = 6;

    internal readonly record struct FallbackFacts(IReadOnlyList<FinancialPoint> Facts, bool Complete);

    /// <summary>
    /// Adds statement facts parsed from recent filings whose report period has no revenue, cost of
    /// revenue, gross profit or net income in <paramref name="result"/>. Returns false when any
    /// needed filing could not be read in full.
    /// </summary>
    internal static async Task<bool> AddMissingRecentFilingFactsAsync(
        string ticker,
        ISecFilingDocumentSource filingSource,
        Dictionary<string, IReadOnlyList<FinancialPoint>> result,
        List<FinancialPoint> revenuePoints,
        ILogger? logger)
    {
        var filings = await TryListFilingsAsync(filingSource, ticker);
        if (filings is null)
            return false;

        var wanted = AccessionsWhere(filings, f => NeedsStatementFallback(f, result), StatementFallbackFilingLimit);
        if (wanted.Count == 0)
            return true;

        var parsed = await ReadFilingFactsAsync(ticker, filingSource, filings, wanted, logger);
        FinancialMetricInputBuilder.AddParsedFilingFacts(result, revenuePoints, parsed.Facts);
        return parsed.Complete;
    }

    /// <summary>
    /// Same gap check as <see cref="AddMissingRecentFilingFactsAsync"/>, plus 6-K filings whose EPS
    /// (or per-ADS EPS) is absent. Returns the parsed facts instead of merging them.
    /// </summary>
    internal static async Task<FallbackFacts> FetchMissingRecentFilingFactsForFundamentalsAsync(
        string ticker,
        ISecFilingDocumentSource filingSource,
        CompanyFacts facts,
        IReadOnlyList<FinancialPoint> eps,
        ILogger? logger)
    {
        var filings = await TryListFilingsAsync(filingSource, ticker);
        if (filings is null)
            return new FallbackFacts([], false);

        var known = FinancialMetricInputBuilder.ReadRequiredConceptFacts(facts);
        var wanted = AccessionsWhere(filings, f => NeedsStatementFallback(f, known), StatementFallbackFilingLimit)
            .Concat(AccessionsWhere(filings, f => NeedsEpsFallback(f, eps), EpsFallbackFilingLimit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (wanted.Count == 0)
            return new FallbackFacts([], true);

        return await ReadFilingFactsAsync(ticker, filingSource, filings, wanted, logger);
    }

    private static async Task<IReadOnlyList<SecFilingSummary>?> TryListFilingsAsync(
        ISecFilingDocumentSource source, string ticker)
    {
        try
        {
            return await source.GetRecentFinancialFilingsAsync(ticker);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> AccessionsWhere(
        IEnumerable<SecFilingSummary> filings, Func<SecFilingSummary, bool> predicate, int limit) =>
        filings
            .Where(predicate)
            .Select(f => f.Accession)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

    /// <summary>
    /// Downloads and parses the given filings. Facts come back in the order the filings were listed
    /// by the source (newest first), so callers that keep the first fact per key keep the newest.
    /// </summary>
    private static async Task<FallbackFacts> ReadFilingFactsAsync(
        string ticker,
        ISecFilingDocumentSource source,
        IReadOnlyList<SecFilingSummary> filings,
        IReadOnlyList<string> accessions,
        ILogger? logger)
    {
        var rank = filings
            .Select((f, i) => (f.Accession, i))
            .Where(x => !string.IsNullOrWhiteSpace(x.Accession))
            .GroupBy(x => x.Accession, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(x => x.i), StringComparer.OrdinalIgnoreCase);
        var queue = accessions
            .OrderBy(a => rank.TryGetValue(a, out var r) ? r : int.MaxValue)
            .ToList();

        var factsByAccession = new Dictionary<string, List<FinancialPoint>>(StringComparer.OrdinalIgnoreCase);
        var complete = true;
        var documents = 0;

        for (var offset = 0; offset < queue.Count; offset += DocumentRequestSize)
        {
            var request = queue.GetRange(offset, Math.Min(DocumentRequestSize, queue.Count - offset));
            SecFilingDocumentSet set;
            try
            {
                set = await source.GetFinancialFilingDocumentsAsync(ticker, request);
            }
            catch (Exception ex)
            {
                complete = false;
                logger?.LogWarning(ex, "Filing fallback could not read {Count} filings for {Ticker}", request.Count, ticker);
                continue;
            }

            if (request.Any(set.IsDegraded))
                complete = false;

            foreach (var document in set.Documents)
            {
                documents++;
                if (!factsByAccession.TryGetValue(document.Accession, out var list))
                    factsByAccession[document.Accession] = list = [];
                list.AddRange(SecFilingStatementParser.Parse(document));
            }
        }

        var facts = queue
            .Where(factsByAccession.ContainsKey)
            .SelectMany(a => factsByAccession[a])
            .ToList();
        logger?.LogInformation(
            "Filing fallback for {Ticker}: {Filings} filings, {Documents} documents, {Facts} facts, complete={Complete}",
            ticker, queue.Count, documents, facts.Count, complete);
        return new FallbackFacts(facts, complete);
    }

    internal static void AddMissingFilingEps(List<FinancialPoint> eps, IEnumerable<FinancialPoint> parsed)
    {
        var existingKeys = eps
            .Select(EpsPointKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var point in parsed
                     .Where(point => SecFilingStatementParser.IsSupplementalEpsSourceName(point.Concept))
                     .OrderBy(point => point.Filed, StringComparer.Ordinal))
        {
            if (existingKeys.Add(EpsPointKey(point)))
                eps.Add(point);
        }
    }

    // Only consolidated headline lines are checked; a filing is worth opening when any is absent
    // for its report date, since a reported COGS or gross profit concept may cover a single segment.
    private static readonly FinancialMetricDefinition[] HeadlineStatementMetrics =
    [
        FinancialMetricsCatalog.Revenue,
        FinancialMetricsCatalog.Cogs,
        FinancialMetricsCatalog.GrossProfit,
        FinancialMetricsCatalog.NetIncome,
    ];

    private static bool NeedsStatementFallback(
        SecFilingSummary filing,
        IReadOnlyDictionary<string, IReadOnlyList<FinancialPoint>> result)
    {
        if (string.IsNullOrWhiteSpace(filing.Accession) || string.IsNullOrWhiteSpace(filing.ReportDate))
            return false;

        return HeadlineStatementMetrics.Any(metric =>
            !FinancialMetricInputBuilder.HasMetricForPeriod(result, metric, filing.ReportDate));
    }

    private static bool NeedsEpsFallback(SecFilingSummary filing, IReadOnlyList<FinancialPoint> eps)
    {
        if (string.IsNullOrWhiteSpace(filing.Accession) || string.IsNullOrWhiteSpace(filing.ReportDate))
            return false;

        if (!SecFormClassifier.IsSixK(filing.Form))
            return false;

        var hasQuarterOrAnnualEps = eps.Any(point =>
            string.Equals(point.End, filing.ReportDate, StringComparison.Ordinal)
            && IsQuarterOrAnnualDuration(point));
        var hasPerAdsEps = eps.Any(point =>
            string.Equals(point.End, filing.ReportDate, StringComparison.Ordinal)
            && IsQuarterOrAnnualDuration(point)
            && IsPerAdsEpsPoint(point));

        return !hasQuarterOrAnnualEps || !hasPerAdsEps;
    }

    private static bool IsPerAdsEpsPoint(FinancialPoint point) =>
        string.Equals(point.SourceType, "filing-table", StringComparison.OrdinalIgnoreCase)
        && SecFilingStatementParser.IsPerAdsSourceLabel(point.SourceLabel);

    internal static bool IsQuarterOrAnnualDuration(FinancialPoint point)
    {
        if (point.Start is null)
            return SecFormClassifier.IsQuarterlyReport(point.Form)
                || SecFormClassifier.IsAnnualReport(point.Form);

        if (!DateOnly.TryParse(point.Start, out var start) || !DateOnly.TryParse(point.End, out var end))
            return false;

        var days = end.DayNumber - start.DayNumber;
        return days is >= 75 and <= 105 or >= 340 and <= 400;
    }

    private static string EpsPointKey(FinancialPoint point) =>
        $"{point.Concept}|{point.Start}|{point.End}|{point.Accn}|{point.SourceType}|{point.SourceLabel}";
}
