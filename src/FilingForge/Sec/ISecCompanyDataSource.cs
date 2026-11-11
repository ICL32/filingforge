using System.Text.Json;

namespace FilingForge;

public interface ISecCompanyDataSource
{
    Task<string> ResolveCikAsync(string ticker, CancellationToken ct = default);
    Task<CompanyFacts> GetCompanyFactsAsync(string ticker, CancellationToken ct = default);
    Task<JsonDocument> GetSubmissionsAsync(string ticker, CancellationToken ct = default);
    Task TrackCompanyInterestAsync(string ticker, CancellationToken ct = default);
}
