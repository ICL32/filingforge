using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FilingForge.Models;
using Microsoft.Extensions.Logging;

namespace FilingForge;

/// <summary>
/// Reads company facts, submissions and filing documents straight from SEC EDGAR over HTTP.
/// Keeps submissions and the ticker map in memory for the lifetime of the instance; nothing is
/// written to disk. SEC asks automated clients to identify themselves and to stay under ten
/// requests per second, so a contact string is required and requests are spaced out.
/// </summary>
public sealed class SecEdgarHttpSource : ISecCompanyDataSource, ISecFilingDocumentSource, IDisposable
{
    private const string TickerMapUrl = "https://www.sec.gov/files/company_tickers.json";
    private const string CompanyFactsUrl = "https://data.sec.gov/api/xbrl/companyfacts/CIK{0}.json";
    private const string SubmissionsUrl = "https://data.sec.gov/submissions/CIK{0}.json";
    private const string SubmissionsPageUrl = "https://data.sec.gov/submissions/{0}";
    private const string ArchiveFileUrl = "https://www.sec.gov/Archives/edgar/data/{0}/{1}/{2}";

    /// <summary>Upper bound on statement-bearing filings returned for one company.</summary>
    public const int FinancialFilingLimit = 72;

    private static readonly TimeSpan MinRequestSpacing = TimeSpan.FromMilliseconds(125);

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRequestUtc = DateTime.MinValue;

    private IReadOnlyDictionary<string, string>? _tickerMap;
    private readonly ConcurrentDictionary<string, JsonDocument> _submissions = new(StringComparer.Ordinal);

    // Filed documents never change, so each accession is downloaded at most once per instance.
    private readonly ConcurrentDictionary<string, IReadOnlyList<SecFilingDocument>> _filingDocuments = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="userAgent">
    /// Identifies the caller to SEC, for example <c>"MyApp/1.0 you@example.com"</c>.
    /// </param>
    /// <param name="http">Optional client to reuse; one is created (and disposed) otherwise.</param>
    public SecEdgarHttpSource(string userAgent, HttpClient? http = null, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            throw new ArgumentException("SEC requires a descriptive User-Agent with contact details.", nameof(userAgent));

        _ownsClient = http is null;
        _http = http ?? new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        });
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        _logger = logger;
    }

    // ── ISecCompanyDataSource ───────────────────────────────────────────────────────────────

    public async Task<string> ResolveCikAsync(string ticker, CancellationToken ct = default)
    {
        _tickerMap ??= await LoadTickerMapAsync(ct);
        return SecTickerLookup.TryGetCik(_tickerMap, ticker, out var cik)
            ? cik
            : throw new KeyNotFoundException($"SEC has no CIK for ticker '{ticker}'.");
    }

    public async Task<CompanyFacts> GetCompanyFactsAsync(string ticker, CancellationToken ct = default)
    {
        var cik = await ResolveCikAsync(ticker, ct);
        using var response = await SendAsync(string.Format(CompanyFactsUrl, cik), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return CompanyFacts.Empty;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await CompanyFactsStreamReader.ReadAsync(stream, ct);
    }

    public async Task<JsonDocument> GetSubmissionsAsync(string ticker, CancellationToken ct = default)
    {
        var cik = await ResolveCikAsync(ticker, ct);
        if (_submissions.TryGetValue(cik, out var cached))
            return cached;

        var document = await GetJsonAsync(string.Format(SubmissionsUrl, cik), ct);
        return _submissions.GetOrAdd(cik, document);
    }

    /// <summary>No-op: there is no refresh schedule to enroll a company in.</summary>
    public Task TrackCompanyInterestAsync(string ticker, CancellationToken ct = default) => Task.CompletedTask;

    // ── ISecFilingDocumentSource ────────────────────────────────────────────────────────────

    /// <summary>Forgets the in-memory submissions so the next call sees newly filed reports.</summary>
    public async Task RefreshFinancialSubmissionsAsync(string ticker, CancellationToken ct = default)
    {
        var cik = await ResolveCikAsync(ticker, ct);
        if (_submissions.TryRemove(cik, out var stale))
            stale.Dispose();
    }

    public async Task<IReadOnlyList<SecFilingSummary>> GetRecentFinancialFilingsAsync(
        string ticker, CancellationToken ct = default)
    {
        var cik = await ResolveCikAsync(ticker, ct);
        var filings = await StatementFilingsAsync(ticker, ct);
        var sym = ticker.Trim().ToUpperInvariant();
        return filings
            .Select(f => new SecFilingSummary(
                cik,
                sym,
                f.Form ?? "",
                f.Accession!,
                f.FilingDate!.Value.ToString("yyyy-MM-dd"),
                f.ReportDate!.Value.ToString("yyyy-MM-dd")))
            .ToList();
    }

    public async Task<SecFilingDocumentSet> GetFinancialFilingDocumentsAsync(
        string ticker, IReadOnlyCollection<string> accessions, CancellationToken ct = default)
    {
        var wanted = accessions
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
            return SecFilingDocumentSet.Empty;

        var cik = await ResolveCikAsync(ticker, ct);
        var sym = ticker.Trim().ToUpperInvariant();
        var documents = new List<SecFilingDocument>();
        var degraded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filing in (await StatementFilingsAsync(ticker, ct)).Where(f => wanted.Contains(f.Accession!)))
        {
            if (_filingDocuments.TryGetValue(filing.Accession!, out var known))
            {
                documents.AddRange(known);
                continue;
            }

            try
            {
                List<SecFilingDocument> read;
                // A 10-K/10-Q carries its statements in the primary document, which is far smaller
                // than the complete submission. Other forms (6-K, 20-F...) often keep them in
                // exhibits, so they, and any primary document without statements, use the package.
                if (await PrimaryStatementDocumentAsync(cik, filing, ct) is { } primary)
                {
                    read = [ToDocument(cik, sym, filing, primary)];
                }
                else
                {
                    var parts = await ReadSubmissionPackageAsync(cik, filing.Accession!, ct);
                    read = [.. parts.Where(p => CarriesStatements(p.Type, p.Text)).Select(p => ToDocument(cik, sym, filing, p))];
                }

                _filingDocuments[filing.Accession!] = read;
                documents.AddRange(read);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                degraded.Add(filing.Accession!);
                _logger?.LogInformation("Skipping filing {Accession} for {Ticker}: {Message}", filing.Accession, sym, ex.Message);
            }
        }

        return new SecFilingDocumentSet(documents, degraded);
    }

    public async Task<IReadOnlyList<FinancialPoint>> GetRecentCoverShareFactsAsync(
        string ticker, int maxFilings = 12, CancellationToken ct = default)
    {
        var cik = await ResolveCikAsync(ticker, ct);
        var submissions = await GetSubmissionsAsync(ticker, ct);
        var sym = ticker.Trim().ToUpperInvariant();

        var periodic = SecSubmissionFilingsReader.ReadRecentSubmissionFilings(submissions.RootElement, int.MaxValue)
            .Where(f => !string.IsNullOrWhiteSpace(f.Accession)
                && !string.IsNullOrWhiteSpace(f.PrimaryDocument)
                && IsHtml(f.PrimaryDocument!)
                && SecFormClassifier.IsDomesticPeriodicReport(f.Form))
            .Take(Math.Max(1, maxFilings));

        var facts = new List<FinancialPoint>();
        foreach (var filing in periodic)
        {
            try
            {
                var html = await GetTextAsync(ArchiveUrl(cik, filing.Accession!, Path.GetFileName(filing.PrimaryDocument!)), ct);
                var part = new PackagePart(filing.Form, Path.GetFileName(filing.PrimaryDocument!), html);
                if (SecCoverShareReader.ReadOutstandingShares(ToDocument(cik, sym, filing, part), sym) is { } fact)
                    facts.Add(fact);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _logger?.LogInformation("Skipping cover page {Accession} for {Ticker}: {Message}", filing.Accession, sym, ex.Message);
            }
        }

        return facts;
    }

    // ── Filing lists ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Statement-bearing filings with an accession, filing date and report date, newest first.
    /// Older paged submission files are read only while fewer than <see cref="FinancialFilingLimit"/>
    /// filings have been found.
    /// </summary>
    private async Task<List<LatestSubmissionFiling>> StatementFilingsAsync(string ticker, CancellationToken ct)
    {
        var root = (await GetSubmissionsAsync(ticker, ct)).RootElement;
        var found = SecSubmissionFilingsReader.ReadRecentFinancialFilings(root).ToList();

        foreach (var page in OlderSubmissionPages(root))
        {
            if (found.Count >= FinancialFilingLimit)
                break;
            try
            {
                using var older = await GetJsonAsync(string.Format(SubmissionsPageUrl, Uri.EscapeDataString(page)), ct);
                found.AddRange(SecSubmissionFilingsReader
                    .ReadHistoricalSubmissionFilings(older.RootElement, int.MaxValue)
                    .Where(SecSubmissionFilingsReader.IsRecentStatementFiling));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException && !ct.IsCancellationRequested)
            {
                _logger?.LogInformation("Skipping submissions page {Page}: {Message}", page, ex.Message);
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return found
            .Where(f => !string.IsNullOrWhiteSpace(f.Accession) && f.FilingDate is not null && f.ReportDate is not null)
            .OrderByDescending(f => f.FilingDate)
            .Where(f => seen.Add(f.Accession!))
            .Take(FinancialFilingLimit)
            .ToList();
    }

    private static IEnumerable<string> OlderSubmissionPages(JsonElement root)
    {
        if (!root.TryGetProperty("filings", out var filings)
            || !filings.TryGetProperty("files", out var files)
            || files.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var file in files.EnumerateArray())
        {
            if (file.TryGetProperty("name", out var name)
                && name.GetString() is { Length: > 0 } fileName
                && Path.GetFileName(fileName) == fileName)
                yield return fileName;
        }
    }

    // ── Filing packages ─────────────────────────────────────────────────────────────────────

    private sealed record PackagePart(string? Type, string FileName, string Text);

    private async Task<PackagePart?> PrimaryStatementDocumentAsync(string cik, LatestSubmissionFiling filing, CancellationToken ct)
    {
        if (!SecFormClassifier.IsDomesticPeriodicReport(filing.Form)
            || filing.PrimaryDocument is not { Length: > 0 } primaryPath
            || !IsHtml(primaryPath))
            return null;

        var fileName = Path.GetFileName(primaryPath);
        using var response = await SendAsync(ArchiveUrl(cik, filing.Accession!, fileName), ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync(ct);
        return CarriesStatements(filing.Form, html) ? new PackagePart(filing.Form, fileName, html) : null;
    }

    /// <summary>
    /// Downloads the complete submission (<c>{accession}.txt</c>) and returns its HTML documents.
    /// Graphics, PDFs and XBRL attachments are skipped while streaming, so they never sit in memory.
    /// </summary>
    private async Task<List<PackagePart>> ReadSubmissionPackageAsync(string cik, string accession, CancellationToken ct)
    {
        using var response = await SendAsync(ArchiveUrl(cik, accession, $"{accession}.txt"), ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var parts = new List<PackagePart>();
        string? type = null, fileName = null;
        StringBuilder? body = null;
        var inDocument = false;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Contains("<DOCUMENT>", StringComparison.OrdinalIgnoreCase))
            {
                inDocument = true;
                type = fileName = null;
                body = null;
                continue;
            }

            if (line.Contains("</DOCUMENT>", StringComparison.OrdinalIgnoreCase))
            {
                if (body is not null && fileName is not null)
                    parts.Add(new PackagePart(type, fileName, InnerText(body.ToString())));
                inDocument = false;
                body = null;
                continue;
            }

            if (!inDocument)
                continue;

            if (body is null)
            {
                type ??= HeaderValue(line, "TYPE");
                fileName ??= HeaderValue(line, "FILENAME");
                if (line.TrimStart().StartsWith("<TEXT>", StringComparison.OrdinalIgnoreCase) && fileName is not null && IsHtml(fileName))
                    body = new StringBuilder();
                else
                    continue;
            }

            body.Append(line).Append('\n');
        }

        return parts;
    }

    private static string? HeaderValue(string line, string field)
    {
        var tag = $"<{field}>";
        var trimmed = line.TrimStart();
        return trimmed.StartsWith(tag, StringComparison.OrdinalIgnoreCase) ? trimmed[tag.Length..].Trim() : null;
    }

    private static string InnerText(string body)
    {
        var match = Regex.Match(body, "<TEXT>(.*?)</TEXT>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : body;
    }

    /// <summary>
    /// Report bodies (10-K, 10-Q, 20-F, 40-F, 6-K, 8-K) and press-release / financial exhibits
    /// (EX-99, EX-101) that mention a top-line or bottom-line figure.
    /// </summary>
    private static bool CarriesStatements(string? type, string text)
    {
        var form = SecFormClassifier.Normalize(type);
        var relevant = form.StartsWith("EX-99", StringComparison.OrdinalIgnoreCase)
            || form.StartsWith("EX-101", StringComparison.OrdinalIgnoreCase)
            || SecFormClassifier.IsFinancialForm(form)
            || SecFormClassifier.IsSixK(form)
            || SecFormClassifier.IsEightK(form);
        return relevant
            && (text.Contains("revenue", StringComparison.OrdinalIgnoreCase)
                || text.Contains("net income", StringComparison.OrdinalIgnoreCase)
                || text.Contains("net profit", StringComparison.OrdinalIgnoreCase));
    }

    private static SecFilingDocument ToDocument(string cik, string ticker, LatestSubmissionFiling filing, PackagePart part) =>
        new(
            cik,
            ticker,
            filing.Form ?? "",
            filing.Accession ?? "",
            filing.FilingDate?.ToString("yyyy-MM-dd") ?? "",
            filing.ReportDate?.ToString("yyyy-MM-dd"),
            part.FileName,
            part.Text);

    private static bool IsHtml(string fileName) =>
        fileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

    private static string ArchiveUrl(string cik, string accession, string fileName) =>
        string.Format(
            ArchiveFileUrl,
            cik.TrimStart('0'),
            accession.Replace("-", "", StringComparison.Ordinal),
            Uri.EscapeDataString(fileName));

    // ── HTTP ────────────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyDictionary<string, string>> LoadTickerMapAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync(TickerMapUrl, ct);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Value.TryGetProperty("ticker", out var t) && t.GetString() is { Length: > 0 } ticker
                && entry.Value.TryGetProperty("cik_str", out var c) && c.TryGetInt64(out var cik))
                map.TryAdd(ticker.ToUpperInvariant(), cik.ToString("D10"));
        }

        return map;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await SendAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private async Task<string> GetTextAsync(string url, CancellationToken ct)
    {
        using var response = await SendAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var wait = _lastRequestUtc + MinRequestSpacing - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
            _lastRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }

        return await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public void Dispose()
    {
        foreach (var doc in _submissions.Values)
            doc.Dispose();
        _submissions.Clear();
        _filingDocuments.Clear();
        _gate.Dispose();
        if (_ownsClient)
            _http.Dispose();
    }
}
