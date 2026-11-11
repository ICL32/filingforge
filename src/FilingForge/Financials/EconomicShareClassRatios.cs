namespace FilingForge;

// Per-filer share-class conversion ratios for the rare economically-unequal dual-class
// registrants. Summing raw per-class share counts is correct for voting-only duals
// (GOOGL/HEI/META: every class has the same economic claim), but wrong when classes carry
// different economic claims. Berkshire's Class A converts to 1,500 Class B shares, so the raw
// A+B sum is ~2.7x too small versus the traded (Class B) equivalent.
//
// Each entry maps a ticker to { share-class member local name -> ratio to the traded class }.
// SecCoverShareReader multiplies each class's latest-instant outstanding count by its ratio
// before summing, yielding a traded-class-equivalent denominator. Tickers absent from the map
// (the overwhelming majority) get ratio 1.0 for every class -> a plain raw sum, unchanged.
//
// Adding a new economically-unequal filer is a one-line entry, verified against the charter /
// 10-K conversion terms and the provider share count / implied market cap.
public static class EconomicShareClassRatios
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Ratios =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            // Berkshire Hathaway: 1 Class A = 1,500 Class B (charter conversion ratio).
            ["BRK-A"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["CommonClassAMember"] = 1.0,
                ["CommonClassBMember"] = 1.0 / 1500.0,
            },
            ["BRK-B"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["CommonClassAMember"] = 1500.0,
                ["CommonClassBMember"] = 1.0,
            },
        };

    public static bool HasOverride(string? ticker) =>
        ticker is not null && Ratios.ContainsKey(Normalize(ticker));

    // Ratio of the given share class to the traded class for the ticker. Defaults to 1.0 when
    // the ticker has no override or the class member is unknown/absent (raw sum).
    public static double RatioFor(string? ticker, string? classMember)
    {
        if (ticker is null || classMember is null)
            return 1.0;
        if (!Ratios.TryGetValue(Normalize(ticker), out var map))
            return 1.0;
        return map.TryGetValue(LocalName(classMember), out var ratio) ? ratio : 1.0;
    }

    // SEC uses dash-form class suffixes (BRK-A / BRK-B); user input may arrive dot-form.
    private static string Normalize(string ticker) =>
        ticker.Trim().ToUpperInvariant().Replace('.', '-');

    // Strips an XBRL namespace prefix: "us-gaap:CommonClassAMember" -> "CommonClassAMember".
    private static string LocalName(string member)
    {
        var idx = member.LastIndexOf(':');
        return idx >= 0 ? member[(idx + 1)..] : member;
    }
}
