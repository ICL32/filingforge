using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FilingForge.Models;

namespace FilingForge;

public static partial class SecFilingStatementParser
{
    private const int MinTextStatementConcepts = 2;

    // Inline-XBRL concepts read straight from a filing. IFRS concepts keep their prefix as the
    // source name; us-gaap concepts drop it, matching companyfacts naming.
    private static readonly string[] IxbrlIfrsConcepts =
    [
        "Revenue", "CostOfSales", "GrossProfit", "ProfitLossFromOperatingActivities", "ProfitLoss",
        "BasicEarningsLossPerShare", "DilutedEarningsLossPerShare", "CashFlowsFromUsedInOperatingActivities",
        "PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities", "Assets", "Liabilities",
    ];

    private static readonly string[] IxbrlUsGaapConcepts =
    [
        "Revenues", "RevenueFromContractWithCustomerExcludingAssessedTax",
        "CostOfGoodsAndServicesSold", "CostOfGoodsSold", "CostOfRevenue",
        "GrossProfit", "OperatingIncomeLoss", "ProfitLoss", "NetIncomeLoss",
        "EarningsPerShareBasic", "EarningsPerShareDiluted",
        "NetCashProvidedByUsedInOperatingActivities", "PaymentsToAcquirePropertyPlantAndEquipment",
        "Assets", "Liabilities",
    ];

    private static readonly IReadOnlyDictionary<string, string> IxbrlConceptMap =
        IxbrlIfrsConcepts.Select(c => (Tag: $"ifrs-full:{c}", Source: $"ifrs-full:{c}"))
            .Concat(IxbrlUsGaapConcepts.Select(c => (Tag: $"us-gaap:{c}", Source: c)))
            .ToDictionary(x => x.Tag, x => x.Source, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> SupplementalSourceNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "EarningsPerShareBasic",
        "EarningsPerShareDiluted",
        "ifrs-full:BasicEarningsLossPerShare",
        "ifrs-full:DilutedEarningsLossPerShare"
    };

    /// <summary>Source names a parsed fact may carry: every catalog concept plus the EPS extras.</summary>
    public static readonly IReadOnlySet<string> RecognizedSourceNames = BuildRecognizedSourceNames();

    private static HashSet<string> BuildRecognizedSourceNames()
    {
        var names = new HashSet<string>(SecFilingConcepts.BySourceName.Keys, StringComparer.Ordinal);
        names.UnionWith(SupplementalSourceNames);
        return names;
    }

    // "Cost of revenue(s)", "Cost of sales", "Cost of goods (and services) (sold)", optionally "Total ...".
    private static readonly Regex CostOfRevenueRow = new(
        @"^(?:total\s+)?costs?\s+of\s+(?:revenues?|sales|goods(?:\s+and\s+services)?(?:\s+sold)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly IReadOnlyList<(Regex Pattern, string SourceName, bool IsPerShare)> RowConcepts =
    [
        (new Regex(@"^(total\s+)?revenues?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:Revenue", false),
        (CostOfRevenueRow, "CostOfRevenue", false),
        (new Regex(@"^gross\s+profit$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:GrossProfit", false),
        (new Regex(@"^operating\s+(profit|income|loss)(\s+\(loss\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:ProfitLossFromOperatingActivities", false),
        (new Regex(@"^(net\s+(profit|income|loss)(\s+\(loss\))?(\s+for\s+the\s+period)?|profit\s+for\s+the\s+period)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:ProfitLoss", false),
        (new Regex(@"^basic\s+earnings\s*(\(loss\))?\s+per\s+(american\s+deposit(ar|or)(y|ies)\s+share|ads|share).*$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:BasicEarningsLossPerShare", true),
        (new Regex(@"^diluted\s+earnings\s*(\(loss\))?\s+per\s+(american\s+deposit(ar|or)(y|ies)\s+share|ads|share).*$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:DilutedEarningsLossPerShare", true),
        (new Regex(@"^(net\s+cash\s+(provided\s+by|from|used\s+in)\s+operating(\s+activities)?|cash\s+flows?\s+from\s+operating(\s+activities)?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:CashFlowsFromUsedInOperatingActivities", false),
        (new Regex(@"^(purchase|acquisition|capital expenditures?|capex).{0,40}(property|equipment|plant|fixed assets)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities", false),
        (new Regex(@"^cash\s+and\s+cash\s+equivalents$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:CashAndCashEquivalents", false),
        (new Regex(@"^total\s+assets$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:Assets", false),
        (new Regex(@"^total\s+liabilit(y|ies)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:Liabilities", false),
        (new Regex(@"^total\s+equity$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ifrs-full:Equity", false)
    ];

    public static IReadOnlyList<FinancialPoint> Parse(SecFilingDocument document)
    {
        var tagged = ParseTaggedFacts(document);
        var tableFacts = ParseStatementTables(document);
        var textFacts = ParseStatementTextBlocks(document);

        return tagged
            .Concat(tableFacts)
            .Concat(textFacts)
            .Where(p => IsRecognizedSourceName(p.Concept))
            .ToList();
    }

    public static bool IsSupplementalEpsSourceName(string? sourceName) =>
        sourceName is not null && SupplementalSourceNames.Contains(sourceName);

    public static bool IsPerAdsSourceLabel(string? sourceLabel) =>
        !string.IsNullOrWhiteSpace(sourceLabel)
        && Regex.IsMatch(
            sourceLabel,
            @"\b(ADS|American\s+Deposit(ar|or)(y|ies))\b",
            RegexOptions.IgnoreCase);

    private static bool IsRecognizedSourceName(string? sourceName) =>
        sourceName is not null && RecognizedSourceNames.Contains(sourceName);

    private static IReadOnlyList<FinancialPoint> ParseTaggedFacts(SecFilingDocument document)
    {
        if (!document.Content.Contains("context", StringComparison.OrdinalIgnoreCase))
            return [];

        var contexts = SecIxbrlParsing.ParseContexts(document.Content);
        if (contexts.Count == 0)
            return [];

        var result = new List<FinancialPoint>();
        foreach (Match fact in SecIxbrlParsing.NonFractionFacts(document.Content))
        {
            var attributes = SecIxbrlParsing.ParseAttributes(fact.Groups["attrs"].Value);
            if (!attributes.TryGetValue("name", out var rawName)) continue;
            if (!IxbrlConceptMap.TryGetValue(rawName, out var sourceName)) continue;
            if (!attributes.TryGetValue("contextRef", out var contextRef)) continue;
            if (!contexts.TryGetValue(contextRef, out var context)) continue;
            if (context.IsSegmented) continue; // segment/axis members are not consolidated totals
            attributes.TryGetValue("scale", out var scale);
            if (!TryParseNumber(HtmlText(fact.Groups["value"].Value), SecIxbrlParsing.ScaleMultiplier(scale ?? ""), out var value)) continue;
            if (attributes.TryGetValue("sign", out var sign) && sign == "-")
                value = -Math.Abs(value);

            result.Add(SecFilingConcepts.Point(
                document,
                sourceName,
                context.Start,
                context.End,
                value,
                "filing-xbrl",
                rawName));
        }

        return result;
    }

    private static IReadOnlyList<FinancialPoint> ParseStatementTables(SecFilingDocument document)
    {
        var result = new Dictionary<string, (FinancialPoint Point, int Precision, int Index)>(StringComparer.Ordinal);
        var index = 0;
        foreach (Match table in TableRegex().Matches(document.Content))
        {
            var html = table.Value;
            var tableText = HtmlText(html);
            if (!HasExplicitPeriodHeader(tableText)) continue;

            var contextText = HtmlText(TextBefore(document.Content, table.Index));
            if (IsExcludedTableContext(contextText)) continue;

            var periods = ExtractPeriods(html, tableText).ToList();
            if (periods.Count == 0) continue;

            var multiplier = UnitMultiplier($"{contextText} {tableText}");
            var precision = UnitPrecision(multiplier, $"{contextText} {tableText}");
            var tableRows = ExtractRows(html).ToList();
            foreach (var cells in tableRows)
            {
                if (cells.Count < 2) continue;
                var label = CleanCell(cells[0]);
                if (!TryMapRow(label, out var sourceName, out var isPerShare)) continue;

                var amounts = RowAmounts(cells, periods.Count, isPerShare ? 1 : multiplier);
                for (var i = 0; i < amounts.Count; i++)
                {
                    if (amounts[i] is not { } amount) continue;
                    var period = periods[i];
                    var point = SecFilingConcepts.Point(
                        document,
                        sourceName,
                        period.Start,
                        period.End,
                        SignedValue(sourceName, label, amount),
                        "filing-table",
                        label,
                        precision);
                    AddTablePoint(result, point, precision, index++);
                }
            }

            foreach (var point in CostOfRevenueFromComponents(document, tableRows, periods, multiplier, precision))
                AddTablePoint(result, point, precision, index++);
        }

        return result.Values
            .OrderBy(entry => entry.Index)
            .Select(entry => entry.Point)
            .ToList();
    }

    private static readonly Regex RevenueHeadingLabel = new(@"^(?:total\s+)?revenues?:?$", RegexOptions.IgnoreCase);
    private static readonly Regex ExpenseSectionHeading = new(@"^(?:operating|costs?\s+and)\s+expenses?:?$", RegexOptions.IgnoreCase);
    private static readonly Regex ExpenseSectionEnd = new(@"^(?:total\s+operating\s+expenses?|operating\s+(?:income|profit|loss))", RegexOptions.IgnoreCase);
    private static readonly Regex CostWord = new(@"\bcosts?\b", RegexOptions.IgnoreCase);
    private static readonly Regex OperatingExpenseLine = new(
        @"^(?:sales?\s+and\s+marketing|selling(?:,\s*general\s+and\s+administrative)?|research\s+and\s+development|general\s+and\s+administrative|depreciation|amortization|restructuring|acquisition|advertising|stock.based|share.based)",
        RegexOptions.IgnoreCase);
    private static readonly Regex CostComponentLine = new(
        @"\bcosts?\b|\bcloud\b|\bhardware\b|\bproduct\s+support\b|\blicense\s+support\b|\bhardware\s+support\b|^services?(?:\s+business)?(?:\s+\(\d+\))?$",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Some income statements have no cost-of-revenue line and instead list cost items
    /// ("Cost of product", "Cost of cloud services", ...) at the top of a single expense section
    /// under revenue. When every such item has a value for a period, their absolute sum is reported
    /// as cost of revenue for that period, tagged <c>filing-table-derived</c>.
    /// </summary>
    private static List<FinancialPoint> CostOfRevenueFromComponents(
        SecFilingDocument document,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<(string Start, string End)> periods,
        double multiplier,
        int precision)
    {
        var labels = rows.Select(r => r.Count > 0 ? CleanCell(r[0]) : "").ToList();
        if (labels.Any(IsExplicitCostOfRevenueLabel))
            return [];

        var sectionStart = labels.FindIndex(l => ExpenseSectionHeading.IsMatch(l));
        if (sectionStart < 0 || !labels.Take(sectionStart).Any(l => RevenueHeadingLabel.IsMatch(l)))
            return [];

        var sectionStop = labels.FindIndex(sectionStart + 1, l => ExpenseSectionEnd.IsMatch(l));
        if (sectionStop <= sectionStart + 1)
            return [];

        var parts = new List<(string Label, IReadOnlyList<double?> Amounts)>();
        for (var r = sectionStart + 1; r < sectionStop; r++)
        {
            if (!IsCostComponentLine(labels[r]))
                continue;
            var amounts = RowAmounts(rows[r], periods.Count, multiplier, dashIsZero: true);
            if (amounts.Any(a => a.HasValue))
                parts.Add((labels[r], amounts));
        }

        // A lone item only counts when it says "cost" outright.
        if (parts.Count == 0 || (parts.Count == 1 && !CostWord.IsMatch(parts[0].Label)))
            return [];

        var label = $"Sum of cost items under {labels[sectionStart]}: {string.Join(" | ", parts.Select(x => x.Label))}";
        var points = new List<FinancialPoint>();
        for (var i = 0; i < periods.Count; i++)
        {
            double total = 0;
            var complete = true;
            foreach (var (_, amounts) in parts)
            {
                if (i >= amounts.Count || amounts[i] is not { } amount)
                {
                    complete = false;
                    break;
                }
                total += Math.Abs(amount);
            }

            if (complete)
            {
                points.Add(SecFilingConcepts.Point(
                    document, "CostOfRevenue", periods[i].Start, periods[i].End, total,
                    "filing-table-derived", label, precision));
            }
        }

        return points;
    }

    private static bool IsCostComponentLine(string label) =>
        !string.IsNullOrWhiteSpace(label)
        && !OperatingExpenseLine.IsMatch(label)
        && CostComponentLine.IsMatch(label);

    /// <summary>
    /// Numbers of one table row, one per period. A leading note-reference cell (e.g. "4") is
    /// skipped when the row has more value cells than periods.
    /// </summary>
    private static IReadOnlyList<double?> RowAmounts(
        IReadOnlyList<string> cells,
        int periodCount,
        double multiplier,
        bool dashIsZero = false)
    {
        IReadOnlyList<string> values = cells.Skip(1).ToList();
        if (values.Count > periodCount && IsLikelyStatementNoteToken(values[0]))
            values = values.Skip(1).ToList();

        return ExtractValuesByPeriod(values, periodCount, multiplier, dashIsZero);
    }

    private static IReadOnlyList<FinancialPoint> ParseStatementTextBlocks(SecFilingDocument document)
    {
        var text = HtmlText(document.Content);
        if (!HasExplicitPeriodHeader(text))
            return [];

        var result = new Dictionary<string, (FinancialPoint Point, int Precision, int Index)>(StringComparer.Ordinal);
        var index = 0;
        foreach (var block in ExtractStatementTextBlocks(text))
        {
            if (!HasExplicitPeriodHeader(block) || !HasExplicitStatementUnit(block)) continue;
            if (IsExcludedTableContext(block)) continue;

            var periods = ExtractTextPeriods(block);
            if (periods.Count == 0 || periods.Count > 4) continue;

            var multiplier = UnitMultiplier(block);
            var precision = UnitPrecision(multiplier, block);
            var rows = new List<(string Label, string SourceName, IReadOnlyList<double?> Values)>();
            foreach (var row in ExtractTextRows(block, periods.Count, multiplier))
            {
                if (TryMapRow(row.Label, out var sourceName, out _))
                    rows.Add((row.Label, sourceName, row.Values));
            }

            if (rows.Select(row => row.SourceName).Distinct(StringComparer.Ordinal).Count() < MinTextStatementConcepts)
                continue;

            foreach (var row in rows)
            {
                for (var i = 0; i < row.Values.Count; i++)
                {
                    if (!row.Values[i].HasValue) continue;
                    var period = periods[i];
                    var rawValue = row.Values[i]!.Value;
                    var point = SecFilingConcepts.Point(
                        document,
                        row.SourceName,
                        period.Start,
                        period.End,
                        SignedValue(row.SourceName, row.Label, rawValue),
                        "filing-text",
                        row.Label,
                        precision);
                    AddTablePoint(result, point, precision, index++);
                }
            }
        }

        return result.Values
            .OrderBy(entry => entry.Index)
            .Select(entry => entry.Point)
            .ToList();
    }

    private static IEnumerable<string> ExtractStatementTextBlocks(string text)
    {
        foreach (Match match in StatementTextStartRegex().Matches(text))
        {
            var start = match.Index;
            var maxEnd = Math.Min(text.Length, start + 4000);
            var nextStart = StatementTextStartRegex().Match(text, start + match.Length);
            if (nextStart.Success && nextStart.Index < maxEnd)
                maxEnd = nextStart.Index;

            var search = text[(start + match.Length)..maxEnd];
            var endMatch = StatementTextEndRegex().Match(search);
            var end = endMatch.Success ? start + match.Length + endMatch.Index : maxEnd;
            if (end > start)
                yield return text[start..end];
        }
    }

    private static IEnumerable<(string Label, IReadOnlyList<double?> Values)> ExtractTextRows(
        string block,
        int periodCount,
        double multiplier)
    {
        foreach (var pattern in TextStatementRowRegexes)
        {
            foreach (Match match in pattern.Matches(block))
            {
                var label = Regex.Replace(match.Groups["label"].Value, @"\s+", " ").Trim();
                var tokens = TextValueRegex()
                    .Matches(match.Groups["values"].Value)
                    .Select(valueMatch => valueMatch.Value)
                    .ToList();
                if (tokens.Count > periodCount && IsLikelyStatementNoteToken(tokens[0]))
                    tokens = tokens.Skip(1).ToList();
                if (tokens.Count != periodCount)
                    continue;

                var values = tokens
                    .Select(token => TryParseNumber(token, multiplier, out var value)
                        ? (double?)value
                        : null)
                    .ToList();

                yield return (label, values);
            }
        }
    }

    private static bool IsLikelyStatementNoteToken(string value)
    {
        var cleaned = CleanCell(value)
            .Replace("$", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Trim();
        return !value.Contains(',', StringComparison.Ordinal)
            && !value.Contains('.', StringComparison.Ordinal)
            && !value.Contains('(', StringComparison.Ordinal)
            && int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 1 and <= 30;
    }

    private static void AddTablePoint(
        Dictionary<string, (FinancialPoint Point, int Precision, int Index)> result,
        FinancialPoint point,
        int precision,
        int index)
    {
        var key = $"{point.Concept}|{point.Start}|{point.End}";
        if (!result.TryGetValue(key, out var existing) || precision > existing.Precision)
            result[key] = (point, precision, index);
    }

    private static bool HasExplicitPeriodHeader(string text) =>
        TextPeriodHeaderRegex().IsMatch(text);

    private static bool HasExplicitStatementUnit(string text) =>
        Regex.IsMatch(
            text,
            @"\b(?:USD|U\.S\.\s+dollars|US\s+dollars)\b.{0,30}\b(?:thousands|millions)\b|\b(?:thousands|millions)\s+of\s+(?:USD|U\.S\.\s+dollars|US\s+dollars)\b",
            RegexOptions.IgnoreCase);

    private static IReadOnlyList<(string Start, string End)> ExtractTextPeriods(string text)
    {
        var periods = ExtractPeriods("", text).ToList();
        return periods.Count > 0 ? periods : ExtractPeriodsFromTextHeaderGroups(text);
    }

    private static IEnumerable<(string Start, string End)> ExtractPeriods(string html, string text)
    {
        var groupedPeriods = ExtractPeriodsFromHeaderGroups(html).ToList();
        if (groupedPeriods.Count > 0)
        {
            foreach (var period in groupedPeriods)
                yield return period;
            yield break;
        }

        var headerText = text.Length > 1200 ? text[..1200] : text;
        var hasQuarterHeader = Regex.IsMatch(text, @"\bthree\s+months?\s+ended\b", RegexOptions.IgnoreCase);
        var hasCumulativeHeader = Regex.IsMatch(text, @"\b(?:six|nine)\s+months?\s+ended\b", RegexOptions.IgnoreCase);
        var hasAnnualHeader = Regex.IsMatch(text, @"\byears?\s+ended\b", RegexOptions.IgnoreCase);
        if ((hasQuarterHeader && hasAnnualHeader) || hasCumulativeHeader)
        {
            // Mixed "three months" / "nine months" (or quarter / year) headers: the group names
            // and the dates sit on separate header rows, so pair them up by order instead.
            foreach (var groupedPeriod in ExtractPeriodsFromTextHeaderGroups(text))
                yield return groupedPeriod;
            yield break;
        }

        var periodKind = hasAnnualHeader ? "annual" : "quarter";

        var splitHeader = MonthDayHeaderRegex().Match(headerText);
        if (splitHeader.Success)
        {
            var month = splitHeader.Groups["month"].Value;
            var day = splitHeader.Groups["day"].Value;
            var seenYears = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match yearMatch in YearRegex().Matches(headerText))
            {
                var year = yearMatch.Value;
                if (!seenYears.Add(year)) continue;
                foreach (var period in PeriodFromDate($"{month} {day}, {year}", periodKind))
                    yield return period;
            }
            yield break;
        }

        foreach (Match match in DateHeaderRegex().Matches(headerText))
            foreach (var period in PeriodFromDate(match.Value, periodKind))
                yield return period;
    }

    private static IReadOnlyList<(string Start, string End)> ExtractPeriodsFromTextHeaderGroups(string text)
    {
        var headerText = text.Length > 1200 ? text[..1200] : text;
        var groups = TextPeriodHeaderRegex()
            .Matches(headerText)
            .Select(match => PeriodKindFromHeader(match.Value))
            .Where(kind => kind is not null)
            .Select(kind => kind!)
            .ToList();
        var unitMatch = StatementUnitMarkerRegex().Match(headerText);
        var dateText = unitMatch.Success ? headerText[unitMatch.Index..] : headerText;
        var dates = DateHeaderRegex()
            .Matches(dateText)
            .Select(match => match.Value)
            .ToList();
        if (groups.Count == 0 || dates.Count == 0)
            return [];

        if (dates.Count % groups.Count != 0)
        {
            var groupCount = Enumerable
                .Range(1, Math.Min(groups.Count, dates.Count))
                .Reverse()
                .FirstOrDefault(count => dates.Count % count == 0);
            if (groupCount == 0)
                return [];

            groups = groups.Skip(groups.Count - groupCount).ToList();
        }

        if (dates.Count % groups.Count != 0)
            return [];

        var datesPerGroup = dates.Count / groups.Count;
        var result = new List<(string Start, string End)>();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var kind = groups[groupIndex];
            foreach (var date in dates.Skip(groupIndex * datesPerGroup).Take(datesPerGroup))
                result.AddRange(PeriodFromDate(date, kind));
        }

        return result;
    }

    private static string? PeriodKindFromHeader(string value)
    {
        if (value.Contains("three", StringComparison.OrdinalIgnoreCase))
            return "quarter";
        if (value.Contains("six", StringComparison.OrdinalIgnoreCase))
            return "h1";
        if (value.Contains("nine", StringComparison.OrdinalIgnoreCase))
            return "nine-month";
        if (value.Contains("year", StringComparison.OrdinalIgnoreCase))
            return "annual";
        return null;
    }

    private static IReadOnlyList<(string Start, string End)> ExtractPeriodsFromHeaderGroups(string html)
    {
        var rows = ExtractTableRows(html).Take(10).ToList();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var groups = rows[rowIndex]
                .Select(TryParsePeriodHeaderGroup)
                .Where(group => group is not null)
                .Select(group => group!)
                .ToList();
            if (groups.Count == 0) continue;

            var periods = new List<(string Start, string End)>();
            var yearCells = rows
                .Skip(rowIndex + 1)
                .Take(4)
                .SelectMany(row => row)
                .Select(TryParseYearCell)
                .Where(cell => cell is not null)
                .Select(cell => cell!)
                .ToList();

            foreach (var group in groups)
            {
                if (group.Year is not null)
                {
                    periods.AddRange(PeriodFromDate($"{group.Month} {group.Day}, {group.Year}", group.Kind));
                    continue;
                }

                var seenYears = new HashSet<string>(StringComparer.Ordinal);
                foreach (var yearCell in yearCells.Where(cell => Overlaps(group.Start, group.End, cell.Start, cell.End)))
                {
                    if (!seenYears.Add(yearCell.Year)) continue;
                    periods.AddRange(PeriodFromDate($"{group.Month} {group.Day}, {yearCell.Year}", group.Kind));
                }
            }

            if (periods.Count > 0)
                return periods;
        }

        return [];
    }

    private static IEnumerable<(string Start, string End)> PeriodFromDate(string value, string periodKind)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            var startDate = periodKind switch
            {
                "annual" => OneYearBefore(endDate).AddDays(1),
                "h1" => PeriodStartByMonthSpan(endDate, 6),
                "nine-month" => PeriodStartByMonthSpan(endDate, 9),
                _ => PeriodStartByMonthSpan(endDate, 3)
            };
            yield return (startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }

    // Same month and day a year earlier; Feb 29 falls back to Feb 28.
    private static DateTime OneYearBefore(DateTime date) =>
        new(date.Year - 1, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(date.Year - 1, date.Month)));

    private static DateTime PeriodStartByMonthSpan(DateTime endDate, int months) =>
        new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1 - months);

    private static IReadOnlyList<double?> ExtractValuesByPeriod(
        IReadOnlyList<string> valueCells,
        int periodCount,
        double multiplier,
        bool dashIsZero = false)
    {
        if (periodCount <= 0)
            return [];

        double? Read(string cell) => CellAmount(cell, multiplier, dashIsZero);

        if (valueCells.Count <= periodCount)
            return valueCells.Take(periodCount).Select(Read).ToList();

        var result = new List<double?>(periodCount);
        for (var i = 0; i < periodCount; i++)
        {
            var start = (int)Math.Round(i * valueCells.Count / (double)periodCount, MidpointRounding.AwayFromZero);
            var end = (int)Math.Round((i + 1) * valueCells.Count / (double)periodCount, MidpointRounding.AwayFromZero);
            var value = valueCells
                .Skip(start)
                .Take(Math.Max(0, end - start))
                .Select(Read)
                .FirstOrDefault(parsed => parsed.HasValue);
            result.Add(value);
        }

        return result;
    }

    // A lone dash means zero only where the caller says so (cost components); elsewhere it is blank.
    private static double? CellAmount(string cell, double multiplier, bool dashIsZero)
    {
        if (dashIsZero && IsDash(cell))
            return 0;
        return TryParseNumber(cell, multiplier, out var amount) ? amount : null;
    }

    private static bool IsDash(string cell)
    {
        var text = CleanCell(cell)
            .Replace("$", "", StringComparison.Ordinal)
            .Replace("\u00a0", " ", StringComparison.Ordinal)
            .Trim();
        return text.Length == 1 && "-\u0097\u2013\u2014".Contains(text[0]);
    }

    private static IEnumerable<IReadOnlyList<string>> ExtractRows(string html) =>
        ExtractTableRows(html)
            .Select(row => row.Select(cell => cell.Text).ToList())
            .Where(cells => cells.Any(cell => cell.Length > 0));

    private static IEnumerable<IReadOnlyList<TableCell>> ExtractTableRows(string html)
    {
        foreach (Match rowMatch in RowRegex().Matches(html))
        {
            var cells = new List<TableCell>();
            var column = 0;
            foreach (Match match in CellRegex().Matches(rowMatch.Value))
            {
                var colspan = ParseColspan(match.Groups["attrs"].Value);
                var text = CleanCell(match.Groups["cell"].Value);
                cells.Add(new TableCell(text, column, column + colspan));
                column += colspan;
            }

            if (cells.Any(cell => cell.Text.Length > 0))
                yield return cells;
        }
    }

    private static int ParseColspan(string attrs)
    {
        var attributes = SecIxbrlParsing.ParseAttributes(attrs);
        return attributes.TryGetValue("colspan", out var raw)
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(1, parsed)
            : 1;
    }

    private static PeriodHeaderGroup? TryParsePeriodHeaderGroup(TableCell cell)
    {
        var quarter = QuarterPeriodHeaderRegex().Match(cell.Text);
        var h1 = H1PeriodHeaderRegex().Match(cell.Text);
        var nineMonth = NineMonthPeriodHeaderRegex().Match(cell.Text);
        var annual = AnnualPeriodHeaderRegex().Match(cell.Text);
        var match = quarter.Success
            ? quarter
            : h1.Success
                ? h1
                : nineMonth.Success
                    ? nineMonth
                    : annual;
        if (!match.Success) return null;

        var year = YearRegex().Matches(cell.Text).Select(m => m.Value).FirstOrDefault();
        return new PeriodHeaderGroup(
            quarter.Success
                ? "quarter"
                : h1.Success
                    ? "h1"
                    : nineMonth.Success
                        ? "nine-month"
                        : "annual",
            match.Groups["month"].Value,
            match.Groups["day"].Value,
            year,
            cell.Start,
            cell.End);
    }

    private static YearHeaderCell? TryParseYearCell(TableCell cell)
    {
        var text = cell.Text.Trim('(', ')', ' ');
        return YearRegex().IsMatch(text) && text.Length == 4
            ? new YearHeaderCell(text, cell.Start, cell.End)
            : null;
    }

    private static bool Overlaps(int leftStart, int leftEnd, int rightStart, int rightEnd) =>
        leftStart < rightEnd && rightStart < leftEnd;

    private static bool TryMapRow(string label, out string sourceName, out bool isPerShare)
    {
        var normalized = Regex.Replace(label, @"\s+", " ").Trim();
        if (IsExcludedStatementLabel(normalized))
        {
            sourceName = "";
            isPerShare = false;
            return false;
        }

        foreach (var (pattern, concept, perShare) in RowConcepts)
        {
            if (!pattern.IsMatch(normalized)) continue;
            sourceName = concept;
            isPerShare = perShare;
            return true;
        }

        sourceName = "";
        isPerShare = false;
        return false;
    }

    private static bool IsExcludedStatementLabel(string label) =>
        Regex.IsMatch(label, @"\b(non-?ifrs|non-?gaap|adjusted|ebitda|margin)\b", RegexOptions.IgnoreCase)
        || Regex.IsMatch(label, @"\bdeferred\s+revenues?\b", RegexOptions.IgnoreCase); // a balance-sheet liability

    private static bool IsExcludedTableContext(string text) =>
        Regex.IsMatch(
            text,
            @"\b(non-?ifrs|non-?gaap|adjusted\s+ebitda|reconcili\w+)\b",
            RegexOptions.IgnoreCase);

    // Returns true when the label is a pure loss label (e.g. "Operating Loss", "Net Loss")
    // with no income/profit qualifier. Financial tables typically show loss amounts as positive
    // numbers under these headings, so the parsed value must be negated to get the signed value.
    // Labels like "Net income (loss)" or "Operating profit (loss)" are not affected.
    private static bool IsLossOnlyLabel(string label) =>
        Regex.IsMatch(label, @"\bloss\b", RegexOptions.IgnoreCase)
        && !Regex.IsMatch(label, @"\b(income|profit)\b", RegexOptions.IgnoreCase);

    private static bool IsExplicitCostOfRevenueLabel(string label) => CostOfRevenueRow.IsMatch(label);

    private static readonly HashSet<string> CostSourceNames = new(StringComparer.Ordinal)
    {
        "CostOfGoodsAndServicesSold", "CostOfGoodsSold", "CostOfRevenue", "ifrs-full:CostOfSales",
    };

    /// <summary>
    /// Costs are stored positive whatever the table shows. A value under a pure loss label
    /// ("Net loss") is stored negative.
    /// </summary>
    private static double SignedValue(string sourceName, string label, double value) =>
        CostSourceNames.Contains(sourceName) ? Math.Abs(value)
        : value > 0 && IsLossOnlyLabel(label) ? -value
        : value;

    private static bool TryParseNumber(string value, double multiplier, out double parsed)
    {
        parsed = 0;
        var cleaned = CleanCell(value)
            .Replace("$", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("\u00a0", " ", StringComparison.Ordinal)
            .Trim();

        if (cleaned.Length == 0 || cleaned is "-" or "\u2014")
            return false;

        var negative = cleaned.StartsWith("(", StringComparison.Ordinal) && cleaned.EndsWith(")", StringComparison.Ordinal);
        cleaned = cleaned.Trim('(', ')');
        if (!double.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            return false;

        parsed = (negative ? -number : number) * multiplier;
        return true;
    }

    private static string CleanCell(string value) =>
        HtmlText(value)
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static string HtmlText(string value) => SecIxbrlParsing.HtmlText(value);

    private static string TextBefore(string text, int index)
    {
        var start = Math.Max(0, index - 1500);
        return text[start..index];
    }

    private static double UnitMultiplier(string text)
    {
        if (Regex.IsMatch(
                text,
                @"\bin\s+thousands\b|\b(?:USD|U\.S\.\s+dollars|US\s+dollars)\b.{0,30}\bthousands\b|\bthousands\s+of\s+(?:USD|U\.S\.\s+dollars|US\s+dollars)\b",
                RegexOptions.IgnoreCase))
            return 1_000;
        if (Regex.IsMatch(
                text,
                @"\bin\s+millions\b|\b(?:USD|U\.S\.\s+dollars|US\s+dollars)\b.{0,30}\bmillions\b|\bmillions\s+of\s+(?:USD|U\.S\.\s+dollars|US\s+dollars)\b",
                RegexOptions.IgnoreCase))
            return 1_000_000;
        return 1;
    }

    private static int UnitPrecision(double multiplier, string text)
    {
        var rank = multiplier switch
        {
            1_000 => 3,
            1 => 2,
            1_000_000 => 1,
            _ => 0
        };

        return text.Contains("rounded", StringComparison.OrdinalIgnoreCase) ? rank - 1 : rank;
    }

    [GeneratedRegex(@"<table\b.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableRegex();

    [GeneratedRegex(@"<tr\b.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RowRegex();

    [GeneratedRegex(@"<t[dh]\b(?<attrs>[^>]*)>(?<cell>.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CellRegex();

    // "<label> <up to five amounts>" inside running text, e.g. "Gross profit 1,234 1,100".
    private static Regex TextRow(string labelPattern) => new(
        $@"\b(?<label>{labelPattern})(?=\s|\.|$)(?:\.|\s)+(?<values>(?:\$?\s*(?:\([^)]+\)|-?\d[\d,]*(?:\.\d+)?|[-\u2014])\s*){{1,5}})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly IReadOnlyList<Regex> TextStatementRowRegexes =
    [
        TextRow(@"Total\s+revenue"),
        TextRow(@"(?:Total\s+)?costs?\s+of\s+(?:revenues?|sales|goods(?:\s+and\s+services)?(?:\s+sold)?)"),
        TextRow(@"Gross\s+profit"),
        TextRow(@"Operating\s+(?:profit|income|loss)(?:\s+\(loss\))?"),
        TextRow(@"Net\s+(?:profit|income|loss)(?:\s+\(loss\))?(?:\s+for\s+the\s+period)?"),
    ];

    [GeneratedRegex(@"\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},\s+\d{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateHeaderRegex();

    [GeneratedRegex(@"\b(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2}),?\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDayHeaderRegex();

    [GeneratedRegex(@"three\s+months?\s+ended\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2}),?", RegexOptions.IgnoreCase)]
    private static partial Regex QuarterPeriodHeaderRegex();

    [GeneratedRegex(@"six\s+months?\s+ended\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2}),?", RegexOptions.IgnoreCase)]
    private static partial Regex H1PeriodHeaderRegex();

    [GeneratedRegex(@"nine\s+months?\s+ended\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2}),?", RegexOptions.IgnoreCase)]
    private static partial Regex NineMonthPeriodHeaderRegex();

    [GeneratedRegex(@"years?\s+ended\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<day>\d{1,2}),?", RegexOptions.IgnoreCase)]
    private static partial Regex AnnualPeriodHeaderRegex();

    [GeneratedRegex(@"\b(?:three|six|nine)\s+months?\s+ended\b|\byears?\s+ended\b", RegexOptions.IgnoreCase)]
    private static partial Regex TextPeriodHeaderRegex();

    [GeneratedRegex(@"\b(?:Amounts\s+in\s+)?(?:USD|U\.S\.\s+dollars|US\s+dollars)\b.{0,30}\b(?:thousands|millions)\b|\b(?:thousands|millions)\s+of\s+(?:USD|U\.S\.\s+dollars|US\s+dollars)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StatementUnitMarkerRegex();

    [GeneratedRegex(@"\b20\d{2}\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\b(?:interim\s+condensed\s+consolidated|interim\s+consolidated|condensed\s+consolidated|consolidated)\s+statements?\s+of\s+(?:profit\s+or\s+loss|operations|income)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StatementTextStartRegex();

    [GeneratedRegex(@"\b(?:statements?\s+of\s+(?:financial\s+position|cash\s+flows?|comprehensive\s+(?:income|loss))|balance\s+sheets?|unlike\s+the\s+balance\s+sheet)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StatementTextEndRegex();

    [GeneratedRegex(@"\$?\s*(?:\([^)]+\)|-?\d[\d,]*(?:\.\d+)?|[-\u2014])", RegexOptions.IgnoreCase)]
    private static partial Regex TextValueRegex();

    private sealed record TableCell(string Text, int Start, int End);
    private sealed record PeriodHeaderGroup(string Kind, string Month, string Day, string? Year, int Start, int End);
    private sealed record YearHeaderCell(string Year, int Start, int End);
}
