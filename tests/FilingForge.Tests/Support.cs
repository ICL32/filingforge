using System.Text;
using System.Text.Json;
using FilingForge.Models;
using FilingForge;

/// <summary>Older method names used throughout the tests, mapped onto <see cref="FilingForgeClient"/>.</summary>
sealed class LegacyClient(ISecCompanyDataSource source)
{
    public FilingForgeClient Inner { get; } = new(source);

    public Task<Fundamentals> FetchFundamentalsAsync(string ticker) => Inner.GetFundamentalsAsync(ticker);
    public Task<Fundamentals> FetchFundamentalsForTraceAsync(string ticker) => Inner.GetFundamentalsAsync(ticker);
    public Task<Fundamentals> FetchFundamentalsCachedAsync(string ticker) => Inner.GetFundamentalsAsync(ticker);
    public Task<FinancialMetricInputs> FetchFinancialMetricInputsAsync(string ticker) => Inner.GetMetricInputsAsync(ticker);
    public Task<StatementSeries> FetchStatementSeriesAsync(string ticker) => Inner.GetStatementsAsync(ticker);
    public async Task<StatementSeries?> FetchStatementSeriesCachedAsync(string ticker) => await Inner.GetStatementsAsync(ticker);
    public async Task<RevenueSeries?> FetchRevenueSeriesCachedAsync(string ticker) => await Inner.GetRevenueAsync(ticker);

    public async Task<List<FinancialPoint>> FetchRevenuePointsAsync(string ticker) =>
        (await FetchRevenueAndCogsPointsAsync(ticker)).RevenuePoints;

    public async Task<RevenueAndCogsPoints> FetchRevenueAndCogsPointsAsync(string ticker)
    {
        var inputs = await Inner.GetMetricInputsAsync(ticker);
        if (inputs.RevenuePoints.Count == 0)
            throw new InvalidOperationException($"No revenue found for {ticker}.");
        return new RevenueAndCogsPoints(
            [.. inputs.RevenuePoints],
            FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.Cogs),
            FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.GrossProfit),
            FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricsCatalog.Ebit),
            FinancialMetricInputBuilder.PointsForAnyProvider(inputs, FinancialMetricInputBuilder.OperatingExpensesConceptsForAnyProvider()));
    }

    public record RevenueAndCogsPoints(
        List<FinancialPoint> RevenuePoints,
        List<FinancialPoint> CogsPoints,
        List<FinancialPoint> GrossProfitPoints,
        List<FinancialPoint> OperatingIncomePoints,
        List<FinancialPoint> OperatingExpensePoints);
}

/// <summary>In-memory SEC source: company facts from a JSON string, filings from a list.</summary>
sealed class InlineSecCompanyDataSource(
    string companyFactsJson,
    IReadOnlyList<SecFilingDocument>? filings = null,
    bool throwOnFilingDocuments = false) : ISecCompanyDataSource, ISecFilingDocumentSource
{
    private readonly IReadOnlyList<SecFilingDocument> _filings = filings ?? [];

    public int FilingDocumentFetchCount { get; private set; }
    public int TrackCount { get; private set; }
    public int CoverShareFetchCount { get; private set; }
    public List<int> CoverShareFetchMaxFilings { get; } = [];
    public int CoverShareStage2FetchCount => CoverShareFetchMaxFilings.Count(n => n != 1);
    public IReadOnlyList<FinancialPoint> CoverShareFacts { get; set; } = [];

    public Task<string> ResolveCikAsync(string ticker, CancellationToken ct = default) => Task.FromResult("0000000003");

    // Feed the streaming reader a few bytes at a time so buffer refills mid-token get exercised.
    public Task<CompanyFacts> GetCompanyFactsAsync(string ticker, CancellationToken ct = default) =>
        CompanyFactsStreamReader.ReadAsync(new ChunkedReadStream(Encoding.UTF8.GetBytes(companyFactsJson), chunkSize: 7), ct);

    public Task<JsonDocument> GetSubmissionsAsync(string ticker, CancellationToken ct = default) =>
        Task.FromResult(JsonDocument.Parse("{}"));

    public Task TrackCompanyInterestAsync(string ticker, CancellationToken ct = default)
    {
        TrackCount++;
        return Task.CompletedTask;
    }

    public Task RefreshFinancialSubmissionsAsync(string ticker, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<SecFilingSummary>> GetRecentFinancialFilingsAsync(string ticker, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SecFilingSummary>>(
            [.. _filings.Select(f => new SecFilingSummary(f.Cik, f.Ticker, f.Form, f.Accession, f.FilingDate, f.ReportDate))]);

    public Task<SecFilingDocumentSet> GetFinancialFilingDocumentsAsync(
        string ticker, IReadOnlyCollection<string> accessions, CancellationToken ct = default)
    {
        FilingDocumentFetchCount++;
        if (throwOnFilingDocuments)
            throw new HttpRequestException("SEC package unavailable");

        var wanted = new HashSet<string>(accessions, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(new SecFilingDocumentSet(
            [.. _filings.Where(f => wanted.Contains(f.Accession))],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<FinancialPoint>> GetRecentCoverShareFactsAsync(
        string ticker, int maxFilings = 12, CancellationToken ct = default)
    {
        CoverShareFetchCount++;
        CoverShareFetchMaxFilings.Add(maxFilings);
        return Task.FromResult(CoverShareFacts);
    }
}

/// <summary>Company facts and submissions read from the JSON files under Fixtures/sec.</summary>
sealed class FixtureSecCompanyDataSource(string fixtureRoot) : ISecCompanyDataSource
{
    public Task<string> ResolveCikAsync(string ticker, CancellationToken ct = default) =>
        ticker.ToUpperInvariant() switch
        {
            "FIX" => Task.FromResult("0000000001"),
            "MISSING" => Task.FromResult("0000000002"),
            var other => throw new KeyNotFoundException($"No fixture CIK for {other}"),
        };

    public async Task<CompanyFacts> GetCompanyFactsAsync(string ticker, CancellationToken ct = default)
    {
        await using var stream = Open("companyfacts", await ResolveCikAsync(ticker, ct));
        return await CompanyFactsStreamReader.ReadAsync(stream, ct);
    }

    public async Task<JsonDocument> GetSubmissionsAsync(string ticker, CancellationToken ct = default)
    {
        await using var stream = Open("submissions", await ResolveCikAsync(ticker, ct));
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public Task TrackCompanyInterestAsync(string ticker, CancellationToken ct = default) => Task.CompletedTask;

    private FileStream Open(string kind, string cik)
    {
        var path = Path.Combine(fixtureRoot, kind, $"CIK{cik}.json");
        return File.Exists(path)
            ? File.OpenRead(path)
            : throw new InvalidOperationException($"Missing test fixture {kind}/CIK{cik}.json");
    }
}
