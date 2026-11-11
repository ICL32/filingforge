namespace FilingForge;

public static class SecTickerLookup
{
    public static IReadOnlyList<string> Candidates(string ticker)
    {
        var sym = ticker.Trim().ToUpperInvariant();
        var candidates = new List<string> { sym };

        var dash = sym.Replace(".", "-");
        if (!candidates.Contains(dash, StringComparer.OrdinalIgnoreCase))
            candidates.Add(dash);

        var dot = sym.Replace("-", ".");
        if (!candidates.Contains(dot, StringComparer.OrdinalIgnoreCase))
            candidates.Add(dot);

        return candidates;
    }

    public static bool TryGetCik(IReadOnlyDictionary<string, string> map, string ticker, out string cik)
    {
        foreach (var candidate in Candidates(ticker))
        {
            if (map.TryGetValue(candidate, out cik!))
                return true;
        }

        cik = "";
        return false;
    }
}
