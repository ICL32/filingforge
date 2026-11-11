using FilingForge.Models;
using Microsoft.Extensions.Logging;

namespace FilingForge;

/// <summary>
/// Entry point of the engine: turns a ticker into reconstructed financial statements, metric
/// rows and valuation inputs. Data comes from an <see cref="ISecCompanyDataSource"/>; when that
/// source can also serve filing documents, statement gaps in the XBRL feed are filled from the
/// filings themselves.
/// </summary>
/// <example>
/// <code>
/// using var sec = new SecEdgarHttpSource("MyApp/1.0 me@example.com");
/// var client = new FilingForgeClient(sec);
/// var statements = await client.GetStatementsAsync("AAPL");
/// </code>
/// </example>
public sealed class FilingForgeClient(ISecCompanyDataSource source, ILogger? logger = null)
{
    private ISecFilingDocumentSource? Filings => source as ISecFilingDocumentSource;

    /// <summary>Raw, merged SEC facts for every catalog concept plus the revenue series.</summary>
    public async Task<FinancialMetricInputs> GetMetricInputsAsync(string ticker, CancellationToken ct = default)
    {
        var sym = Normalize(ticker);
        await RefreshFilingListAsync(sym, ct);
        var facts = await source.GetCompanyFactsAsync(sym, ct);
        if (!facts.HasAnyFacts)
            return FinancialMetricInputs.Empty;

        return await FinancialMetricInputBuilder.BuildAsync(sym, facts, Filings, logger: logger);
    }

    /// <summary>Quarterly, annual, TTM and point-in-time metric rows plus their audit trail.</summary>
    public async Task<FinancialMetricsBuildResult> GetMetricsAsync(string ticker, CancellationToken ct = default)
    {
        var sym = Normalize(ticker);
        return FinancialMetricsBuilder.Build(sym, await GetMetricInputsAsync(sym, ct));
    }

    /// <summary>Income statement, balance sheet and cash-flow statement series.</summary>
    public async Task<StatementSeries> GetStatementsAsync(string ticker, CancellationToken ct = default)
    {
        var sym = Normalize(ticker);
        return FinancialStatementSeriesBuilder.Build(sym, await GetMetricsAsync(sym, ct));
    }

    /// <summary>Quarterly, annual and TTM revenue.</summary>
    /// <exception cref="InvalidOperationException">SEC holds no revenue for the company.</exception>
    public async Task<RevenueSeries> GetRevenueAsync(string ticker, CancellationToken ct = default)
    {
        var sym = Normalize(ticker);
        var inputs = await GetMetricInputsAsync(sym, ct);
        if (inputs.RevenuePoints.Count == 0)
            throw new InvalidOperationException($"No revenue found for {sym} in SEC company facts or recent filings.");

        return RevenueSeriesBuilder.Build(sym, [.. inputs.RevenuePoints]);
    }

    /// <summary>EPS, revenue, margins, cash flow, shares and balance-sheet inputs for valuation.</summary>
    public async Task<Fundamentals> GetFundamentalsAsync(string ticker, CancellationToken ct = default)
    {
        var sym = Normalize(ticker);
        await RefreshFilingListAsync(sym, ct);
        var facts = await source.GetCompanyFactsAsync(sym, ct);
        return facts.HasAnyFacts
            ? await FundamentalsBuilder.BuildAsync(sym, facts, Filings, logger)
            : FundamentalsBuilder.Empty;
    }

    private async Task RefreshFilingListAsync(string sym, CancellationToken ct)
    {
        if (Filings is null)
            return;
        try
        {
            await Filings.RefreshFinancialSubmissionsAsync(sym, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "Could not refresh the filing list for {Ticker}; using what is loaded", sym);
        }
    }

    private static string Normalize(string ticker) => ticker.Trim().ToUpperInvariant();
}
