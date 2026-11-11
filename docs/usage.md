# Using FilingForge

- [Setup](#setup)
- [The client](#the-client)
- [Metric rows](#metric-rows)
- [Provenance: where each number came from](#provenance-where-each-number-came-from)
- [Statements, revenue and fundamentals](#statements-revenue-and-fundamentals)
- [Exports](#exports)
- [Your own data source](#your-own-data-source)
- [Performance and SEC limits](#performance-and-sec-limits)

## Setup

Reference `src/FilingForge/FilingForge.csproj` from your project. FilingForge targets .NET 8 and depends only on `Microsoft.Extensions.Logging.Abstractions`.

```csharp
using FilingForge;

using var sec = new SecEdgarHttpSource("YourApp/1.0 you@example.com");
var client = new FilingForgeClient(sec);
```

SEC requires automated clients to send a User-Agent that names the app and gives a contact address, and to stay under ten requests per second. `SecEdgarHttpSource` handles the pacing; you provide the User-Agent. Keep **one instance** for the life of your app: it caches the ticker map, submissions and downloaded filing documents in memory.

Both constructors accept an optional `ILogger` if you want to see what is being fetched and why:

```csharp
var sec = new SecEdgarHttpSource(userAgent, http: myHttpClient, logger: myLogger);
var client = new FilingForgeClient(sec, logger: myLogger);
```

## The client

| Method | Returns | Use it for |
|---|---|---|
| `GetMetricsAsync(ticker)` | `FinancialMetricsBuildResult` | Everything: all 134 metrics per period, plus an audit trail |
| `GetStatementsAsync(ticker)` | `StatementSeries` | A compact income/balance/cash-flow table per quarter and year |
| `GetRevenueAsync(ticker)` | `RevenueSeries` | Quarterly revenue with TTM and year-over-year growth |
| `GetFundamentalsAsync(ticker)` | `Fundamentals` | Raw point series (EPS, shares, margins' numerators…) for your own valuation math |
| `GetMetricInputsAsync(ticker)` | `FinancialMetricInputs` | The merged SEC facts before any metric is computed (debugging, research) |

Tickers are case-insensitive. Class shares work in either form (`BRK-B` or `BRK.B`). An unknown ticker throws `KeyNotFoundException`.

## Metric rows

`GetMetricsAsync` returns one `FinancialMetricPeriodRow` per fiscal period end, oldest first:

```csharp
var result = await client.GetMetricsAsync("MSFT");

foreach (var row in result.Rows)
{
    // row.PeriodEnd    "2026-06-30"
    // row.QuarterLabel "Q4'26"   (fiscal, using the company's own year end)
    // row.FiscalYear / row.FiscalQuarter

    FinancialMetricValue q   = row.Quarterly["revenue"];  // standalone quarter
    FinancialMetricValue fy  = row.Annual["revenue"];     // set on fiscal-year-end rows
    FinancialMetricValue ttm = row.Ttm["revenue"];        // trailing four quarters
    FinancialMetricValue pt  = row.Point["totalAssets"];  // balance-sheet value at PeriodEnd

    if (q.HasValue) Console.WriteLine($"{row.QuarterLabel}: {q.Value:N0}");
}
```

- **Flow metrics** (income and cash flow) live in `Quarterly`, `Annual` and `Ttm`.
- **Point metrics** (balance sheet, shares outstanding) live in `Point`.
- Every metric id is present in every row. A missing value is a `FinancialMetricValue` with `Value == null` and formula `BlankMissingInputs`, so you can index without `TryGetValue`.

All metric ids, names and definitions are in [metrics.md](metrics.md). They're also available in code:

```csharp
foreach (var m in FinancialMetricsCatalog.AllMetrics)
    Console.WriteLine($"{m.Id,-30} {m.DisplayName,-35} {m.Group}");
```

## Provenance: where each number came from

Every `FinancialMetricValue` carries:

| Field | Meaning | Example |
|---|---|---|
| `Formula` | How the value was produced | `DirectFact`, `StandaloneQuarterFromAnnual`, `RevenueMinusCogs`, `TtmSumQuarterly` |
| `SourceConcepts` | XBRL concept(s) used, `\|`-separated | `RevenueFromContractWithCustomerExcludingAssessedTax` |
| `Filed` | Filing date of the latest input | `2026-11-03` |
| `Accession` | SEC accession number(s) | `0000320193-26-000026` |
| `Derivation` | `reported`, `derived_from_annual`, `derived_from_9m`, … | |
| `SourceType` | Where the input came from (below) | `companyfacts` |
| `SourceDocument` | Filing document file name, for filing-document values | `aapl-20260926.htm` |
| `Inputs` | For derived values: each input with its role, period, value and filing | `annual`, `q1`, `q2`, `q3` |

`SourceType` values, from most to least trusted:

| SourceType | What it is |
|---|---|
| `filing-table-derived` | Cost of revenue summed from the cost lines of a filing's income-statement table, used when no single cost total is tagged |
| `companyfacts` | SEC's XBRL `companyfacts` API, the normal source |
| `filing-xbrl` | Inline XBRL tags read from a filing document |
| `filing-table` | A number read from an HTML statement table in a filing |
| `filing-text` | A number read from statement text in a filing |
| `derived` / `blank` | Computed from other metrics / no value |

`result.AuditRows` flattens all of this to one row per metric, period and view (`quarterly`, `annual`, `ttm`, `point`), which is convenient for filtering or loading into a database:

```csharp
var reconstructed = result.AuditRows
    .Where(a => a.ValueType == "quarterly" && a.Formula == FinancialMetricFormulaIds.DerivedFromAnnual);
```

Each row also has a `ValidationStatus` (`ok`, `warning` or `error`) and `ValidationMessages`, codes such as `ttm_does_not_match_annual` when four quarters don't add up to the reported year.

## Statements, revenue and fundamentals

```csharp
StatementSeries s = await client.GetStatementsAsync("AAPL");
foreach (var q in s.Quarterly)        // also s.Annual
    Console.WriteLine($"{q.Label}  revenue {q.Revenue}  net {q.NetIncome}  fcf {q.FreeCashFlow}  debt {q.TotalDebt}");
// q.Provenance["revenue"] tells you the formula, concepts and filing behind that cell.

RevenueSeries r = await client.GetRevenueAsync("AAPL");
foreach (var q in r.Quarters)
    Console.WriteLine($"{q.Label}  {q.QuarterlyRevenue:N0}  ttm {q.TtmRevenue:N0}  yoy {q.RevenueYoyPct:F1}%");

Fundamentals f = await client.GetFundamentalsAsync("AAPL");
// f.Eps, f.SharesOutstanding, f.NetIncomeLossPoints, f.OperatingCashFlowPoints, f.CapexPoints, ...
// Each is a list of FinancialPoint (start, end, value, form, filed, concept, accession).
```

`GetRevenueAsync` throws `InvalidOperationException` when SEC has no revenue for the company (for example a shell company or a fund).

## Exports

```csharp
var exporter = new FinancialMetricsExportService(client);
string[] tickers = ["AAPL", "MSFT", "NVDA"];

string csv  = await exporter.BuildCsvAsync(tickers);                // one wide CSV, all metrics
byte[] xlsx = await exporter.BuildXlsxAsync(tickers);               // Data + audit + Definitions sheets
byte[] zip  = await exporter.BuildMetricCsvZipAsync(tickers);       // one CSV per metric and view
var trace   = await exporter.BuildTraceAsync(tickers, includeRawInputs: true); // JSON-serializable
```

Tickers that fail (unknown, no data) don't abort the export: they appear as an error row with the message.

## Your own data source

`SecEdgarHttpSource` is one implementation of two interfaces. Implement them yourself to read from a cache, a database or SEC's bulk files:

```csharp
public interface ISecCompanyDataSource
{
    Task<string> ResolveCikAsync(string ticker, CancellationToken ct = default);
    Task<CompanyFacts> GetCompanyFactsAsync(string ticker, CancellationToken ct = default);
    Task<JsonDocument> GetSubmissionsAsync(string ticker, CancellationToken ct = default);
    Task TrackCompanyInterestAsync(string ticker, CancellationToken ct = default); // may be a no-op
}

// Optional: implement this too and FilingForge fills gaps from filing documents.
public interface ISecFilingDocumentSource { ... }
```

For example, SEC publishes every company's facts nightly as one ZIP (`companyfacts.zip`). This source reads the unzipped files, so the whole market can be processed offline without hitting SEC per ticker:

```csharp
sealed class BulkCompanyFacts(string folder, IReadOnlyDictionary<string, string> cikByTicker) : ISecCompanyDataSource
{
    public Task<string> ResolveCikAsync(string ticker, CancellationToken ct = default) =>
        Task.FromResult(cikByTicker[ticker.ToUpperInvariant()]);

    public async Task<CompanyFacts> GetCompanyFactsAsync(string ticker, CancellationToken ct = default)
    {
        var cik = await ResolveCikAsync(ticker, ct);
        await using var file = File.OpenRead(Path.Combine(folder, $"CIK{cik}.json"));
        return await CompanyFactsStreamReader.ReadAsync(file, ct);   // streams; never loads the whole JSON
    }

    public Task<JsonDocument> GetSubmissionsAsync(string ticker, CancellationToken ct = default) =>
        Task.FromResult(JsonDocument.Parse("{}"));

    public Task TrackCompanyInterestAsync(string ticker, CancellationToken ct = default) => Task.CompletedTask;
}

var client = new FilingForgeClient(new BulkCompanyFacts(@"D:\sec\companyfacts", cikMap));
```

Without an `ISecFilingDocumentSource` there is no filing-document fallback: values come only from XBRL. For most US companies, that makes no difference.

## Performance and SEC limits

- A company with complete XBRL data takes about **one request** (its company facts) and a few seconds.
- Filing documents are downloaded only for periods with gaps. The first call for a company that needs them (foreign filers, unusual cost reporting) can take 10–60 seconds at SEC's rate limit. Later calls on the same `SecEdgarHttpSource` reuse what was downloaded.
- For many tickers, run them sequentially or with low parallelism. The shared rate gate keeps you within SEC's limits either way.
- To refresh after new filings, create a new `SecEdgarHttpSource`, or call `RefreshFinancialSubmissionsAsync(ticker)`.
