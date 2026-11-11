using System.Globalization;
using static FilingForge.FinancialMetricDerivations;

namespace FilingForge;

internal static class FinancialMetricTtmBuilder
{
    internal static Dictionary<string, FinancialMetricValue> Build(
        IReadOnlyDictionary<string, FinancialMetricValue> quarterly,
        IReadOnlyDictionary<string, FinancialMetricQuarterEntry> metaByEnd)
    {
        var values = quarterly
            .Where(kv => kv.Value.HasValue)
            .OrderBy(kv => kv.Key)
            .ToList();

        var result = new Dictionary<string, FinancialMetricValue>();
        for (var i = 3; i < values.Count; i++)
        {
            var window = new[]
            {
                values[i - 3],
                values[i - 2],
                values[i - 1],
                values[i]
            };
            // metaByEnd is deliberately NOT consulted here: fiscal metadata is missing on some
            // quarters and wrong on others (see IsConsecutiveQuarterWindow), so period-end spacing
            // is the reliable signal. The parameter stays on Build for callers/trace.
            if (!IsConsecutiveQuarterWindow(window)) continue;

            var parts = new[]
            {
                values[i].Value,
                values[i - 1].Value,
                values[i - 2].Value,
                values[i - 3].Value
            };
            result[values[i].Key] = new FinancialMetricValue(
                parts.Sum(v => v.Value.GetValueOrDefault()),
                FinancialMetricFormulaIds.TtmSumQuarterly,
                JoinSources(parts),
                LatestFiled(parts),
                JoinAccessions(parts),
                "derived",
                "derived",
                null,
                null,
                [
                    TraceInput("q1", values[i - 3].Key, values[i - 3].Value),
                    TraceInput("q2", values[i - 2].Key, values[i - 2].Value),
                    TraceInput("q3", values[i - 1].Key, values[i - 1].Value),
                    TraceInput("q4", values[i].Key, values[i].Value)
                ]
            );
        }
        return result;
    }

    private static bool IsConsecutiveQuarterWindow(
        IReadOnlyList<KeyValuePair<string, FinancialMetricValue>> window)
    {
        if (window.Count != 4) return false;

        // Consecutiveness is decided by the ACTUAL SPACING of period ends, not by quarter indices.
        //
        // Index arithmetic fails three ways on real filings:
        //  - fiscal vs calendar are different scales (a September-FY filer's FY2026 Q1 ends in calendar
        //    2025), so a window mixing quarters with and without fiscal metadata looked non-consecutive;
        //  - calendar bucketing breaks on 52/53-week filers whose quarter ends drift across calendar
        //    quarters (Apple FY2017: 2016-12-31 -> 2017-04-01 buckets as Q4 -> Q2, a false gap);
        //  - fiscal metadata is itself unreliable on older periods (Apple tags both 2016-06-25 and
        //    2016-09-24 as Q3'16), which would collapse two distinct quarters onto one index.
        //
        // Period-end spacing has none of those failure modes: consecutive quarters are ~13 weeks apart
        // (91 days; 98 for the 14-week quarter of a 53-week year), while a MISSING quarter leaves a
        // ~182-day gap that stays comfortably outside the window. So a genuine gap still blanks TTM.
        for (var i = 1; i < window.Count; i++)
        {
            if (!TryParseEnd(window[i - 1].Key, out var previous)) return false;
            if (!TryParseEnd(window[i].Key, out var current)) return false;

            var gapDays = (current - previous).TotalDays;
            if (gapDays < MinQuarterGapDays || gapDays > MaxQuarterGapDays) return false;
        }
        return true;
    }

    // One quarter apart. Wide enough for 13-week (91d) and 14-week (98d) quarters plus reporting
    // drift; far below the ~182 days a skipped quarter would produce.
    private const double MinQuarterGapDays = 75;
    private const double MaxQuarterGapDays = 110;

    private static bool TryParseEnd(string end, out DateTime date) =>
        DateTime.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

}
