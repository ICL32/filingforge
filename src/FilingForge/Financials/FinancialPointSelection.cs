using FilingForge.Models;

namespace FilingForge;

internal static class FinancialPointSelection
{
    internal static bool ShouldReplace(FinancialPoint candidate, FinancialPoint existing)
    {
        var candidateRank = SourceRank(candidate.SourceType);
        var existingRank = SourceRank(existing.SourceType);
        if (candidateRank != existingRank)
            return candidateRank > existingRank;

        return ShouldReplaceEqualRank(
            candidate.SourcePrecision,
            candidate.Accn,
            existing.SourcePrecision,
            existing.Accn);
    }

    internal static bool ShouldReplaceEqualRank(
        int candidatePrecision,
        string? candidateAccession,
        int existingPrecision,
        string? existingAccession)
    {
        if (!SameNonEmptyAccession(candidateAccession, existingAccession))
            return true;

        if (candidatePrecision != existingPrecision)
            return candidatePrecision > existingPrecision;

        return false;
    }

    internal static int SourceRank(string? sourceType) =>
        sourceType switch
        {
            "companyfacts" => 4,
            "filing-table-derived" => 5, // summed cost block; outranks a cost tag that may cover one segment
            "filing-xbrl" => 3,
            "filing-table" => 2,
            "filing-text" => 1,
            _ => 0
        };

    private static bool SameNonEmptyAccession(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
