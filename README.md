# FilingForge

FilingForge turns SEC EDGAR data into clean, comparable financial statements. You give it a ticker. It returns quarterly, annual and trailing-twelve-month (TTM) income statement, balance sheet and cash-flow figures. Every number records where it came from.

It is a plain .NET 8 library. It needs no database, cache server or API key, only an internet connection to `sec.gov`.

## Why

SEC's XBRL feed (`companyfacts`) is the best free source of US company financials, but it is hard to use as-is:

- **Quarters are often missing.** Many companies only report Q4 inside the annual report, or report six- and nine-month year-to-date figures instead of the quarter. FilingForge rebuilds standalone quarters from those totals.
- **Concepts change.** Revenue can move from one XBRL concept to another after an accounting change, and companies restate earlier periods. FilingForge merges the concepts and prefers the latest restated value. It also keeps the value as it was first reported, for point-in-time use.
- **Some facts never reach the feed.** For filings where `companyfacts` has gaps, FilingForge reads the statement tables in the filing documents themselves (10-K, 10-Q, 20-F, 6-K exhibits).
- **Averages are not sums.** Weighted-average share counts can't be added across quarters, so a missing Q4 is rebuilt by share-days, with stock splits taken into account.

## What you get

- About 240 metrics across the income statement, balance sheet and cash flow, from US GAAP and IFRS filers.
- Quarterly, annual and TTM views of every flow metric. Point-in-time values for balance-sheet items.
- EPS, weighted-average shares, and per-share versions of the main income-statement lines.
- An audit row for every value: formula, source concepts, filing date, accession number and inputs.
- Export to CSV, per-metric CSV files, XLSX, or a JSON-friendly trace.

## Quick start

```csharp
using FilingForge;

// SEC asks automated clients to identify themselves with contact details.
using var sec = new SecEdgarHttpSource("YourApp/1.0 you@example.com");
var client = new FilingForgeClient(sec);

var statements = await client.GetStatementsAsync("AAPL");
var metrics    = await client.GetMetricsAsync("MSFT");   // rows + audit trail
var revenue    = await client.GetRevenueAsync("NVDA");   // quarterly / annual / TTM revenue

var exporter = new FinancialMetricsExportService(client);
File.WriteAllBytes("financials.xlsx", await exporter.BuildXlsxAsync(["AAPL", "MSFT"]));
```

`SecEdgarHttpSource` spaces requests to stay under SEC's limit of 10 requests per second, and keeps submissions in memory for the lifetime of the instance. To read from your own cache or a database instead, implement `ISecCompanyDataSource`, and optionally `ISecFilingDocumentSource` for the filing-document fallback.

## How quarters are rebuilt

| Missing | Rebuilt as |
|---|---|
| Q2 | six months − Q1 |
| Q3 | nine months − six months, or nine months − Q1 − Q2 |
| Q4 | year − Q1 − Q2 − Q3, or year − nine months |
| Q4 weighted-average shares | (year avg × year days − 9M avg × 9M days) ÷ Q4 days |

Reconstruction uses the version of the annual figure that existed when the quarters were filed. A quarter is never rebuilt next to one that was reported directly.

## Project layout

```
src/FilingForge/
  FilingForgeClient.cs        entry point
  Sec/                        EDGAR access, companyfacts reader, filing statement parser
  Financials/                 metric catalog, quarter/TTM builders, statements, fundamentals
  Exporting/                  CSV / XLSX / trace export
tests/FilingForge.Tests/      tests and snapshot fixtures
```

## Running the tests

```bash
dotnet run --project tests/FilingForge.Tests -c Release
```

Snapshot files live in `tests/FilingForge.Tests/Snapshots`. After an intended output change, set `UPDATE_SNAPSHOTS=1`, run the tests again, and review the diff.

## Disclaimer

FilingForge reshapes public SEC data. It can be wrong where filings are inconsistent or unusual. It is not investment advice: check anything important against the original filing.

## License

MIT. See [LICENSE](LICENSE).
