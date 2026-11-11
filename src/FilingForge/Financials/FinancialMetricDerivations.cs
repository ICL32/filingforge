namespace FilingForge;

internal static class FinancialMetricDerivations
{
    internal static FinancialMetricValue? Existing(IReadOnlyDictionary<string, FinancialMetricValue> map, string end) =>
        map.TryGetValue(end, out var value) && value.HasValue ? value : null;

    internal static FinancialMetricValue? DerivedIdentity(
        string formula,
        IReadOnlyDictionary<string, FinancialMetricValue> values,
        string end)
    {
        var value = Existing(values, end);
        if (value is null) return null;
        return new FinancialMetricValue(
            value.Value,
            formula,
            value.SourceConcepts,
            value.Filed,
            value.Accession,
            "derived",
            value.SourceType,
            value.SourceDocument,
            value.SourceLabel,
            [TraceInput("input", end, value)]
        );
    }

    internal static FinancialMetricValue? DerivedAdd(
        string formula,
        bool nonNegative,
        IReadOnlyDictionary<string, FinancialMetricValue> left,
        IReadOnlyDictionary<string, FinancialMetricValue> right,
        string end)
    {
        if (!TryGet(left, end, out var l) || !TryGet(right, end, out var r)) return null;
        return Derived(formula, l.Value.GetValueOrDefault() + r.Value.GetValueOrDefault(), nonNegative, l, r);
    }

    internal static FinancialMetricValue? DerivedAdd3(
        string formula,
        bool nonNegative,
        IReadOnlyDictionary<string, FinancialMetricValue> left,
        IReadOnlyDictionary<string, FinancialMetricValue> right,
        IReadOnlyDictionary<string, FinancialMetricValue> third,
        string end)
    {
        if (!TryGet(left, end, out var l) || !TryGet(right, end, out var r) || !TryGet(third, end, out var t)) return null;
        return Derived(formula, l.Value.GetValueOrDefault() + r.Value.GetValueOrDefault() + t.Value.GetValueOrDefault(), nonNegative, l, r, t);
    }

    internal static FinancialMetricValue? DerivedAdd4(
        string formula,
        bool nonNegative,
        IReadOnlyDictionary<string, FinancialMetricValue> first,
        IReadOnlyDictionary<string, FinancialMetricValue> second,
        IReadOnlyDictionary<string, FinancialMetricValue> third,
        IReadOnlyDictionary<string, FinancialMetricValue> fourth,
        string end)
    {
        if (!TryGet(first, end, out var a)
            || !TryGet(second, end, out var b)
            || !TryGet(third, end, out var c)
            || !TryGet(fourth, end, out var d))
        {
            return null;
        }

        return Derived(
            formula,
            a.Value.GetValueOrDefault() + b.Value.GetValueOrDefault() + c.Value.GetValueOrDefault() + d.Value.GetValueOrDefault(),
            nonNegative,
            a,
            b,
            c,
            d);
    }

    internal static FinancialMetricValue? DerivedSubtract(
        string formula,
        bool nonNegative,
        IReadOnlyDictionary<string, FinancialMetricValue> left,
        IReadOnlyDictionary<string, FinancialMetricValue> right,
        string end)
    {
        if (!TryGet(left, end, out var l) || !TryGet(right, end, out var r)) return null;
        return Derived(formula, l.Value.GetValueOrDefault() - r.Value.GetValueOrDefault(), nonNegative, l, r);
    }

    internal static FinancialMetricValue? DerivedSubtract3(
        string formula,
        bool nonNegative,
        IReadOnlyDictionary<string, FinancialMetricValue> left,
        IReadOnlyDictionary<string, FinancialMetricValue> minusOne,
        IReadOnlyDictionary<string, FinancialMetricValue> minusTwo,
        string end)
    {
        if (!TryGet(left, end, out var l) || !TryGet(minusOne, end, out var r1) || !TryGet(minusTwo, end, out var r2))
            return null;
        return Derived(
            formula,
            l.Value.GetValueOrDefault() - r1.Value.GetValueOrDefault() - r2.Value.GetValueOrDefault(),
            nonNegative,
            l,
            r1,
            r2
        );
    }

    internal static FinancialMetricValue? DerivedPercentDivide(
        string formula,
        IReadOnlyDictionary<string, FinancialMetricValue> numerator,
        IReadOnlyDictionary<string, FinancialMetricValue> denominator,
        string end)
    {
        if (!TryGet(numerator, end, out var n) || !TryGet(denominator, end, out var d)) return null;
        var denominatorValue = d.Value.GetValueOrDefault();
        if (denominatorValue == 0 || !double.IsFinite(denominatorValue)) return null;
        var value = n.Value.GetValueOrDefault() / denominatorValue * 100;
        if (!double.IsFinite(value)) return null;
        return Derived(formula, value, false, n, d);
    }

    internal static bool TryGet(IReadOnlyDictionary<string, FinancialMetricValue> map, string end, out FinancialMetricValue value)
    {
        if (map.TryGetValue(end, out value!) && value.HasValue)
            return true;
        value = FinancialMetricValue.Blank();
        return false;
    }

    internal static FinancialMetricValue? Derived(
        string formula,
        double value,
        bool nonNegative,
        params FinancialMetricValue[] inputs)
    {
        if (nonNegative && value < 0) return null;
        return new FinancialMetricValue(
            value,
            formula,
            JoinSources(inputs),
            LatestFiled(inputs),
            JoinAccessions(inputs),
            "derived",
            "derived",
            null,
            null,
            TraceInputs(inputs)
        );
    }

    internal static FinancialMetricValue GetOrBlank(IReadOnlyDictionary<string, FinancialMetricValue> values, string end) =>
        values.TryGetValue(end, out var value) ? value : FinancialMetricValue.Blank();

    internal static string JoinSources(IEnumerable<FinancialMetricValue> values) =>
        string.Join(
            "|",
            values
                .SelectMany(v => v.SourceConcepts.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal)
        );

    internal static string LatestFiled(IEnumerable<FinancialMetricValue> values) =>
        values
            .Select(v => v.Filed)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .DefaultIfEmpty("")
            .Max(StringComparer.Ordinal) ?? "";

    internal static string JoinAccessions(IEnumerable<FinancialMetricValue> values) =>
        string.Join(
            "|",
            values
                .SelectMany(v => (v.Accession ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal)
        );

    internal static IReadOnlyList<FinancialMetricInputTrace> TraceInputs(params FinancialMetricValue[] values) =>
        values.Select((value, index) => TraceInput($"input{index + 1}", null, value)).ToList();

    internal static FinancialMetricInputTrace TraceInput(string role, string? periodEnd, FinancialMetricValue value) =>
        new(
            role,
            periodEnd,
            value.Value,
            value.Formula,
            value.SourceConcepts,
            value.Filed,
            value.Accession,
            value.Derivation,
            value.SourceType,
            value.SourceDocument,
            value.SourceLabel
        );

    internal static IReadOnlyList<string> SortedEnds(params IEnumerable<string>[] groups) =>
        groups.SelectMany(g => g).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

}
