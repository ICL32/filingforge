using FilingForge.Models;
using FilingForge;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

static FinancialPoint Q(string end, double val, string start, string filed, string? concept = "Revenues", string? accn = null) =>
    new(start, end, val, "10-Q", filed, concept, Accn: accn);

static FinancialPoint Annual(string end, double val, string? start = null, string filed = "2024-02-01") =>
    new(start, end, val, "10-K", filed, "Revenues");

static void Equal(double expected, double actual, string name)
{
    if (Math.Abs(expected - actual) > 0.0001)
        throw new Exception($"{name}: expected {expected}, got {actual}");
}

static void True(bool value, string name)
{
    if (!value) throw new Exception(name);
}


void ImpliesQ4AndTtmFromAnnualWithoutStart()
{
    var points = new[]
    {
        Q("2022-03-31", 10_000, "2022-01-01", "2022-05-01"),
        Q("2022-06-30", 15_000, "2022-04-01", "2022-08-01"),
        Q("2022-09-30", 20_000, "2022-07-01", "2022-11-01"),
        Annual("2022-12-31", 120_000),
        Q("2023-03-31", 10_000, "2023-01-01", "2023-05-01"),
    };

    var series = RevenueSeriesBuilder.Build("TEST", points);
    var q4 = series.Quarters.Single(q => q.End == "2022-12-31");
    var q1Next = series.Quarters.Single(q => q.End == "2023-03-31");

    Equal(75_000, q4.QuarterlyRevenue, "Q4 implied revenue");
    Equal(120_000, q1Next.TtmRevenue!.Value, "rolling TTM revenue");
    Equal(1, q1Next.FiscalQuarter, "backend quarter label");
    True(q1Next.Label == "Q1'23", "backend chart label");
    True(q4.ValidationStatus == "ok", "Q4 should reconcile cleanly");
}

void RevenueSeriesBuilderTreatsNoStartAmendedAnnualAsAnnual()
{
    var points = new[]
    {
        Q("2024-03-31", 100, "2024-01-01", "2024-05-01"),
        Q("2024-06-30", 200, "2024-04-01", "2024-08-01"),
        Q("2024-09-30", 300, "2024-07-01", "2024-11-01"),
        new FinancialPoint(null, "2024-12-31", 1_000, "10-K/A", "2025-03-01", "Revenues", 2024, "FY", Accn: "FYAMEND"),
    };

    var series = RevenueSeriesBuilder.Build("TEST", points);
    var q4 = series.Quarters.Single(q => q.End == "2024-12-31");

    Equal(400, q4.QuarterlyRevenue, "amended no-start annual implies Q4 revenue");
    Equal(1_000, q4.TtmRevenue!.Value, "amended no-start annual reconciles year-end TTM");
}

void DerivesQ3FromNineMonthsMinusQ1Q2()
{
    var points = new[]
    {
        Q("2023-03-31", 10_000, "2023-01-01", "2023-05-01"),
        Q("2023-06-30", 20_000, "2023-04-01", "2023-08-01"),
        new FinancialPoint("2023-01-01", "2023-09-30", 60_000, "10-Q", "2023-11-01", "Revenues"),
        Annual("2023-12-31", 100_000, "2023-01-01", "2024-02-01"),
    };

    var series = RevenueSeriesBuilder.Build("TEST", points);
    var q3 = series.Quarters.Single(q => q.End == "2023-09-30");
    var q4 = series.Quarters.Single(q => q.End == "2023-12-31");

    Equal(30_000, q3.QuarterlyRevenue, "Q3 derived revenue");
    Equal(40_000, q4.QuarterlyRevenue, "Q4 derived revenue");
    Equal(100_000, q4.TtmRevenue!.Value, "year-end TTM revenue");
}

void PreservesFiscalQuarterFromSecMetadata()
{
    var points = new[]
    {
        new FinancialPoint("2025-09-28", "2025-12-27", 100_000, "10-Q", "2026-01-30", "Revenues", 2026, "Q1"),
        new FinancialPoint("2025-09-28", "2025-12-27", 100_000, "10-K", "2026-10-30", "Revenues", 2026, "FY"),
        new FinancialPoint("2025-09-28", "2025-12-27", 100_000, "10-Q", "2027-01-30", "Revenues", 2027, "Q1"),
    };

    var series = RevenueSeriesBuilder.Build("TEST", points);
    var q1 = series.Quarters.Single(q => q.End == "2025-12-27");

    Equal(2026, q1.FiscalYear, "SEC fiscal year");
    Equal(1, q1.FiscalQuarter, "SEC fiscal quarter");
    True(q1.Label == "Q1'26", "SEC fiscal chart label");
}

void PreservesNonCalendarFiscalQuarterOnCalendarPeriod()
{
    var points = new[]
    {
        new FinancialPoint("2025-10-01", "2025-12-31", 100_000, "10-Q", "2026-01-30", "Revenues", 2026, "Q1"),
    };

    var series = RevenueSeriesBuilder.Build("TEST", points);
    var q1 = series.Quarters.Single(q => q.End == "2025-12-31");

    Equal(2026, q1.FiscalYear, "non-calendar SEC fiscal year");
    Equal(1, q1.FiscalQuarter, "non-calendar SEC fiscal quarter");
    True(q1.Label == "Q1'26", "non-calendar SEC chart label");
}

void CalendarComparativeFactsUsePeriodYearForLabels()
{
    var revenueSpec = FinancialMetricsCatalog.Ifrs("Revenue");
    var netIncomeSpec = FinancialMetricsCatalog.Ifrs("ProfitLoss");
    var revenue = new[]
    {
        new FinancialPoint("2024-07-01", "2024-09-30", 706_000_000, "6-K", "2025-11-18", revenueSpec.SourceName, 2024, "Q3", SourceType: "filing-table"),
        new FinancialPoint("2024-01-01", "2024-09-30", 2_029_000_000, "6-K", "2025-11-18", revenueSpec.SourceName, 2024, null, SourceType: "filing-table"),
        new FinancialPoint("2024-01-01", "2024-12-31", 2_811_000_000, "20-F", "2026-02-26", revenueSpec.SourceName, 2025, "FY")
    };
    var netIncome = new[]
    {
        new FinancialPoint("2024-07-01", "2024-09-30", 12_000_000, "6-K", "2025-11-18", netIncomeSpec.SourceName, 2024, "Q3", SourceType: "filing-table"),
        new FinancialPoint("2024-01-01", "2024-09-30", -19_000_000, "6-K", "2025-11-18", netIncomeSpec.SourceName, 2024, null, SourceType: "filing-table"),
        new FinancialPoint("2024-01-01", "2024-12-31", 21_000_000, "20-F", "2026-02-26", netIncomeSpec.SourceName, 2025, "FY")
    };

    var series = RevenueSeriesBuilder.Build("KLAR", revenue);
    var q4 = series.Quarters.Single(q => q.End == "2024-12-31");

    Equal(2024, q4.FiscalYear, "calendar comparative annual uses period year for derived Q4 fiscal year");
    Equal(4, q4.FiscalQuarter, "calendar comparative annual derived Q4 fiscal quarter");
    True(q4.Label == "Q4'24", "calendar comparative annual derived Q4 label");

    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [revenueSpec.Key] = revenue,
            [netIncomeSpec.Key] = netIncome
        },
        revenue,
        CompanyFactsProfile.Empty);
    var built = FinancialMetricsBuilder.Build("KLAR", inputs);
    var statements = FinancialStatementSeriesBuilder.Build("KLAR", built);
    True(statements.Annual.Single(row => row.PeriodEnd == "2024-12-31").Label == "FY 2024", "calendar comparative annual statement label");

    var q4RevenueAudit = built.AuditRows.Single(row =>
        row.PeriodEnd == "2024-12-31"
        && row.MetricId == FinancialMetricsCatalog.Revenue.Id
        && row.ValueType == "quarterly");
    True(q4RevenueAudit.Inputs?.Count == 2, "derived Q4 revenue trace lists annual and 9M inputs");
    True(q4RevenueAudit.Inputs![0].Role == "annual", "derived Q4 revenue trace includes annual input role");
    True(q4RevenueAudit.Inputs[1].Role == "nineMonths", "derived Q4 revenue trace includes 9M input role");
}

// Apple-shaped 52/53-week September filer. The fiscal Q4 ends in September, so labelling off
// the period-end calendar month produced a second "Q3" that collided with the real Q3, and the
// December Q1 of the next fiscal year came out as Q4 of the year before.
void FiscalCalendarLabelsNonCalendarYearEndAsQ4()
{
    var points = new[]
    {
        Q("2015-12-26", 75_872, "2015-09-27", "2016-01-27"),
        Q("2016-03-26", 50_557, "2015-12-27", "2016-04-27"),
        Q("2016-06-25", 42_358, "2016-03-27", "2016-07-27"),
        new FinancialPoint("2015-09-27", "2016-09-24", 215_639, "10-K", "2016-10-26", "Revenues", 2016, "FY"),
        Q("2016-12-31", 78_351, "2016-09-25", "2017-02-01"),
        Q("2017-04-01", 52_896, "2017-01-01", "2017-05-03"),
        Q("2017-07-01", 45_408, "2017-04-02", "2017-08-02"),
        new FinancialPoint("2016-09-25", "2017-09-30", 229_234, "10-K", "2017-11-03", "Revenues", 2017, "FY"),
    };

    var series = RevenueSeriesBuilder.Build("AAPL", points);
    string Label(string end) => series.Quarters.Single(q => q.End == end).Label;

    True(Label("2016-06-25") == "Q3'16", "September filer Q3 label");
    True(Label("2016-09-24") == "Q4'16", "September fiscal year end is Q4, not the calendar Q3");
    True(Label("2016-12-31") == "Q1'17", "December quarter is Q1 of the next fiscal year");
    True(Label("2017-04-01") == "Q2'17", "53-week Q2 that drifts into April");
    True(Label("2017-09-30") == "Q4'17", "next September fiscal year end is Q4");

    var labels = series.Quarters.Select(q => q.Label).ToList();
    True(labels.Count == labels.Distinct().Count(), "September filer produces no duplicate quarter labels");
}

// A prior-year comparative carries the fiscal year of the filing it was taken from, not its own:
// Apple's fiscal Q3 FY2008 appears inside the FY2009 Q3 10-Q tagged fy=2009/fp=Q3, and was
// labelled Q3'09 — colliding with the real Q3 FY2009.
void FiscalCalendarIgnoresFilingScopedFiscalYearOnComparatives()
{
    var points = new[]
    {
        new FinancialPoint("2008-03-30", "2008-06-28", 7_464, "10-Q", "2009-07-22", "Revenues", 2009, "Q3"),
        new FinancialPoint("2007-09-30", "2008-09-27", 37_491, "10-K", "2008-11-05", "Revenues", 2008, "FY"),
        new FinancialPoint("2008-09-28", "2008-12-27", 11_880, "10-K", "2010-10-27", "Revenues", 2010, "Q1"),
        new FinancialPoint("2009-03-29", "2009-06-27", 9_734, "10-K", "2010-10-27", "Revenues", 2009, "Q3"),
        new FinancialPoint("2008-09-28", "2009-09-26", 42_905, "10-K", "2009-10-27", "Revenues", 2009, "FY"),
    };

    var series = RevenueSeriesBuilder.Build("AAPL", points);
    string Label(string end) => series.Quarters.Single(q => q.End == end).Label;

    True(Label("2008-06-28") == "Q3'08", "comparative keeps its own fiscal year, not the filing's");
    True(Label("2008-12-27") == "Q1'09", "restated Q1 comparative keeps its own fiscal year");
    True(Label("2009-06-27") == "Q3'09", "same-quarter next year stays distinct");

    var labels = series.Quarters.Select(q => q.Label).ToList();
    True(labels.Count == labels.Distinct().Count(), "comparatives produce no duplicate quarter labels");
}

// Quarters of the in-progress fiscal year are filed before that year's 10-K exists, so there is
// no annual anchor at or after them yet; the calendar projects one forward.
void FiscalCalendarLabelsQuartersPastTheLastAnnual()
{
    var points = new[]
    {
        new FinancialPoint("2022-09-25", "2023-09-30", 383_285, "10-K", "2023-11-02", "Revenues", 2023, "FY"),
        Q("2023-12-30", 119_575, "2023-10-01", "2024-02-02"),
        Q("2024-03-30", 90_753, "2023-12-31", "2024-05-02"),
        Q("2024-06-29", 85_777, "2024-03-31", "2024-08-01"),
    };

    var series = RevenueSeriesBuilder.Build("AAPL", points);
    string Label(string end) => series.Quarters.Single(q => q.End == end).Label;

    True(Label("2023-12-30") == "Q1'24", "first quarter after the last filed 10-K");
    True(Label("2024-03-30") == "Q2'24", "second quarter after the last filed 10-K");
    True(Label("2024-06-29") == "Q3'24", "third quarter after the last filed 10-K");
}

// Some filers name a fiscal year after the calendar year it starts in, so the year end falls in
// the following calendar year. The offset is taken from the original 10-K and applied to every
// anchor, so the whole series stays on the filer's own naming.
void FiscalCalendarKeepsFilerFiscalYearNumbering()
{
    var points = new[]
    {
        Q("2022-06-30", 95_000, "2022-04-01", "2022-08-05"),
        Q("2022-09-30", 105_000, "2022-07-01", "2022-11-05"),
        Q("2022-12-31", 100_000, "2022-10-01", "2023-02-05"),
        new FinancialPoint("2022-04-01", "2023-03-31", 400_000, "10-K", "2023-06-20", "Revenues", 2022, "FY"),
        Q("2023-06-30", 100_000, "2023-04-01", "2023-08-05"),
        Q("2023-09-30", 110_000, "2023-07-01", "2023-11-05"),
        Q("2023-12-31", 105_000, "2023-10-01", "2024-02-05"),
        new FinancialPoint("2023-04-01", "2024-03-31", 420_000, "10-K", "2024-06-20", "Revenues", 2023, "FY"),
    };

    var series = RevenueSeriesBuilder.Build("TEST", points);
    string Label(string end) => series.Quarters.Single(q => q.End == end).Label;

    True(Label("2023-03-31") == "Q4'22", "fiscal year named after its start year");
    True(Label("2023-06-30") == "Q1'23", "first quarter of the next fiscal year");
    True(Label("2023-12-31") == "Q3'23", "third quarter keeps the filer's numbering");
    True(Label("2024-03-31") == "Q4'23", "next fiscal year end keeps the filer's numbering");
}

// UNH tags PaymentsOfDividendsCommonStock to the dividend payment date with no start, next to
// the same value as a real 3-month duration. The payment date used to become its own period end,
// giving the export a second column for the same fiscal quarter holding only that metric.
void StartlessTransactionDateFactSnapsToStatementPeriodEnd()
{
    var points = new[]
    {
        new FinancialPoint("2024-01-01", "2024-03-31", 1_729, "10-Q", "2024-05-09", "PaymentsOfDividendsCommonStock"),
        new FinancialPoint(null, "2024-03-19", 1_729, "10-K", "2025-02-27", "PaymentsOfDividendsCommonStock"),
        new FinancialPoint("2024-04-01", "2024-06-30", 1_935, "10-Q", "2024-08-09", "PaymentsOfDividendsCommonStock"),
    };

    var quarters = QuarterlySeriesBuilder.BuildQuarterlySeries(points);
    var ends = quarters.Select(q => q.End).ToList();

    True(!ends.Contains("2024-03-19"), "dividend payment date does not become its own period end");
    True(ends.Contains("2024-03-31"), "dividend value stays on its statement period end");
    Equal(1_729, quarters.Single(q => q.End == "2024-03-31").Val, "snapped dividend value");
}

// A start-less fact that is genuinely its own period end — an amended filing that omits the
// start — must not be pulled onto a neighbouring period.
void StartlessFactOnItsOwnPeriodEndIsNotSnapped()
{
    var points = new[]
    {
        new FinancialPoint("2024-01-01", "2024-03-31", 100, "10-Q", "2024-05-09", "Revenues"),
        new FinancialPoint(null, "2024-06-30", 120, "10-Q/A", "2024-09-01", "Revenues"),
    };

    var ends = QuarterlySeriesBuilder.BuildQuarterlySeries(points).Select(q => q.End).ToList();

    True(ends.Contains("2024-06-30"), "amended start-less quarter keeps its own period end");
}

// Periods that predate the earliest annual fact in companyfacts still need a window. Deere and
// Diamondback carry a decade of balance-sheet-only periods before their first 10-K, and every one
// of them used to resolve to the earliest anchor and clamp onto a single label (DECK had 31 rows
// labelled Q1'17).
void FiscalCalendarLabelsPeriodsBeforeTheEarliestAnnual()
{
    var points = new[]
    {
        Q("2016-06-30", 10, "2016-04-01", "2016-08-01"),
        Q("2016-09-30", 11, "2016-07-01", "2016-11-01"),
        Q("2016-12-31", 12, "2016-10-01", "2017-02-01"),
        new FinancialPoint("2016-04-01", "2017-03-31", 50, "10-K", "2017-05-30", "Revenues", 2017, "FY"),
    };

    var calendar = FiscalCalendar.Build(points);

    string Label(string end)
    {
        var r = calendar.Label(end)!.Value;
        return FiscalCalendar.Format(r.FiscalYear, r.FiscalQuarter);
    }

    True(Label("2017-03-31") == "Q4'17", "the earliest annual anchors its own fiscal year");
    True(Label("2016-06-30") == "Q1'17", "quarter inside the anchored year");
    True(Label("2015-12-31") == "Q3'16", "period a year before the earliest annual");
    True(Label("2013-06-30") == "Q1'14", "period several years before the earliest annual");
    True(Label("2007-12-31") == "Q3'08", "period a decade before the earliest annual");
}

// A date days after a REAL filed fiscal year end belongs to the new year's Q1. Only a projected
// anchor gets the prior-year-Q4 tolerance, because the projection is arithmetic and can land a day
// or two off the true year end.
void FiscalCalendarKeepsQ1AfterAFiledYearEnd()
{
    var points = new[]
    {
        new FinancialPoint("2019-01-01", "2019-12-31", 100, "10-K", "2020-02-20", "Revenues", 2019, "FY"),
        new FinancialPoint("2020-01-01", "2020-12-31", 110, "10-K", "2021-02-20", "Revenues", 2020, "FY"),
        new FinancialPoint("2021-01-01", "2021-12-31", 120, "10-K", "2022-02-20", "Revenues", 2021, "FY"),
    };

    var calendar = FiscalCalendar.Build(points);

    string Label(string end)
    {
        var r = calendar.Label(end)!.Value;
        return FiscalCalendar.Format(r.FiscalYear, r.FiscalQuarter);
    }

    True(Label("2020-12-31") == "Q4'20", "the filed year end itself is Q4");
    True(Label("2021-01-05") == "Q1'21", "five days after a filed year end is the new year's Q1");
    True(Label("2021-01-31") == "Q1'21", "a month after a filed year end is still Q1");
}

// Year ends that drift across a calendar boundary can land two fiscal years in the same calendar
// year. Numbering each anchor from its own end year then gives both the same label.
void FiscalCalendarNumbersFiscalYearsInSequence()
{
    var points = new[]
    {
        // 52/53-week filer: fiscal 2015 ends 2016-01-02 and fiscal 2016 ends 2016-12-31, so both
        // year ends fall in calendar 2016.
        new FinancialPoint("2014-01-05", "2015-01-03", 100, "10-K", "2015-03-01", "Revenues", 2014, "FY"),
        new FinancialPoint("2015-01-04", "2016-01-02", 110, "10-K", "2016-03-01", "Revenues", 2015, "FY"),
        new FinancialPoint("2016-01-03", "2016-12-31", 120, "10-K", "2017-02-28", "Revenues", 2016, "FY"),
    };

    var calendar = FiscalCalendar.Build(points);

    string Label(string end)
    {
        var r = calendar.Label(end)!.Value;
        return FiscalCalendar.Format(r.FiscalYear, r.FiscalQuarter);
    }

    var labels = new[] { Label("2015-01-03"), Label("2016-01-02"), Label("2016-12-31") };
    True(labels.Distinct().Count() == 3, $"two year ends in one calendar year stay distinct: {string.Join(",", labels)}");
    True(labels[0] == "Q4'14", "first fiscal year keeps its reported numbering");
    True(labels[1] == "Q4'15", "second fiscal year steps forward one");
    True(labels[2] == "Q4'16", "third fiscal year steps forward one");
}

// Canonicalizing a duration fact's end must move its start by the same amount. Shifting only the
// end changes the period's length, which can push a long quarter past the 105-day bound that
// classifies it as a quarter at all, silently dropping the value.
void CanonicalizingAPeriodEndPreservesItsSpan()
{
    var points = new[]
    {
        // Two filings close fiscal Q1 on dates 8 days apart; the later date is reported twice, so
        // it wins the cluster and the 2024-03-23 fact is pulled onto it.
        new FinancialPoint("2023-12-09", "2024-03-23", 100, "10-Q", "2024-05-01", "Revenues"),
        new FinancialPoint("2023-12-17", "2024-03-31", 100, "10-Q", "2024-05-02", "Revenues"),
        new FinancialPoint("2023-12-17", "2024-03-31", 100, "10-K", "2025-02-01", "Revenues"),
    };

    var aligned = StatementPeriodEnds.Build(points).Apply(points.ToList());
    var moved = aligned.Single(p => p.Filed == "2024-05-01");

    True(moved.End == "2024-03-31", $"end canonicalized, got {moved.End}");
    True(moved.Start == "2023-12-17", $"start moved by the same shift, got {moved.Start}");

    var span = (DateTime.Parse(moved.End) - DateTime.Parse(moved.Start!)).TotalDays;
    Equal(105, span, "period length unchanged by canonicalization");
    True(QuarterlySeriesBuilder.BuildQuarterlySeries(points).Any(q => q.End == "2024-03-31"),
        "the quarter survives canonicalization");
}

void TtmSeriesUsesLatestFiledDateInWindow()
{
    var points = new[]
    {
        Q("2023-03-31", 10, "2023-01-01", "2023-05-01"),
        Q("2023-06-30", 20, "2023-04-01", "2024-03-01"),
        Q("2023-09-30", 30, "2023-07-01", "2023-11-01"),
        Q("2023-12-31", 40, "2023-10-01", "2024-02-15"),
    };

    var ttm = QuarterlySeriesBuilder.BuildTtmSeries(points).Single();

    Equal(100, ttm.Ttm, "TTM sum");
    True(ttm.End == "2023-12-31", "TTM fiscal period end");
    True(ttm.Filed == "2024-03-01", "TTM uses latest filed date from any quarter in the window");
}

void TtmSeriesCarriesConceptProvenance()
{
    var points = new[]
    {
        Q("2023-03-31", 10, "2023-01-01", "2023-05-01", "NetIncomeLoss"),
        Q("2023-06-30", 20, "2023-04-01", "2023-08-01", "NetIncomeLoss"),
        Q("2023-09-30", 30, "2023-07-01", "2023-11-01", "NetIncomeLoss"),
        Q("2023-12-31", 40, "2023-10-01", "2024-02-15", "NetIncomeLoss"),
    };

    var ttm = QuarterlySeriesBuilder.BuildTtmSeries(points).Single();

    True(ttm.SourceConcepts == "NetIncomeLoss", "TTM carries source concept provenance from its quarters");
    True(ttm.Derivation == "reported", "TTM carries derivation provenance from its quarters");
}

void QuarterlySeriesBuilderTreatsNoStartAmendedQuarterAsQuarter()
{
    var points = new[]
    {
        new FinancialPoint(null, "2024-03-31", 123, "10-Q/A", "2024-05-10", "Revenues", 2024, "Q1", Accn: "Q1AMEND"),
    };

    var quarter = QuarterlySeriesBuilder.BuildQuarterlySeries(points).Single();

    Equal(123, quarter.Val, "amended no-start 10-Q is quarterly");
    Equal(1, quarter.FiscalQuarter!.Value, "amended no-start 10-Q keeps fiscal quarter");
}

void QuarterlySeriesPrefersPerAdsEpsOverOrdinaryShareEps()
{
    var points = new[]
    {
        new FinancialPoint(
            "2025-04-01", "2025-06-30", 0.10, "6-K", "2025-08-15",
            "ifrs-full:DilutedEarningsLossPerShare",
            Accn: "Q2",
            SourceType: "filing-table",
            SourceLabel: "Diluted earnings per ADS (in U.S. dollars)"),
        new FinancialPoint(
            "2025-07-01", "2025-09-30", 0.11, "6-K", "2025-11-15",
            "ifrs-full:DilutedEarningsLossPerShare",
            Accn: "Q3",
            SourceType: "filing-table",
            SourceLabel: "Diluted earnings per ADS (in U.S. dollars)"),
        new FinancialPoint(
            "2025-10-01", "2025-12-31", 0.12, "6-K", "2026-03-15",
            "ifrs-full:DilutedEarningsLossPerShare",
            Accn: "Q4",
            SourceType: "filing-table",
            SourceLabel: "Diluted earnings per ADS (in U.S. dollars)"),
        new FinancialPoint(
            "2026-01-01", "2026-03-31", 0.08, "6-K", "2026-05-28",
            "ifrs-full:DilutedEarningsLossPerShare",
            Accn: "ADSQ1",
            SourceType: "filing-table",
            SourceLabel: "Diluted earnings per ADS (in U.S. dollars)"),
        new FinancialPoint(
            "2026-01-01", "2026-03-31", 0.01, "20-F", "2026-06-01",
            "ifrs-full:DilutedEarningsLossPerShare",
            Accn: "ORDQ1",
            SourceType: "companyfacts"),
    };

    var quarterly = QuarterlySeriesBuilder.BuildQuarterlySeries(points);
    var q1 = quarterly.Single(q => q.End == "2026-03-31");
    Equal(0.08, q1.Val, "quarterly EPS prefers per-ADS filing-table fact over later ordinary-share fact");
    True(q1.SourceType == "filing-table", "quarterly EPS source type is per-ADS filing table");
    True(q1.SourceLabel?.Contains("ADS", StringComparison.OrdinalIgnoreCase) == true, "quarterly EPS retains per-ADS source label");

    var ttm = QuarterlySeriesBuilder.BuildTtmSeries(points).Single();
    Equal(0.41, ttm.Ttm, "TTM EPS uses per-ADS quarter instead of ordinary-share duplicate");
}

void QuarterlySeriesDerivesQ2FromActualQ3Boundary()
{
    var points = new[]
    {
        new FinancialPoint("2025-12-29", "2026-03-28", 100, "6-K", "2026-05-01", "ifrs-full:Revenue", Accn: "A1", SourceType: "filing-text", SourcePrecision: 3),
        new FinancialPoint("2025-12-29", "2026-09-26", 600, "6-K", "2026-11-01", "ifrs-full:Revenue", Accn: "A9", SourceType: "filing-text", SourcePrecision: 3),
        new FinancialPoint("2026-06-28", "2026-09-26", 300, "6-K", "2026-11-01", "ifrs-full:Revenue", Accn: "A9", SourceType: "filing-text", SourcePrecision: 3),
    };

    var quarters = QuarterlySeriesBuilder.BuildQuarterlySeries(points);
    var q2 = quarters.Single(q => q.End == "2026-06-27");

    Equal(200, q2.Val, "Q2 from 9M uses actual Q3 start boundary");
    Equal(2, q2.FiscalQuarter!.Value, "derived Q2 keeps fiscal quarter metadata");
}

void QuarterlySeriesDoesNotInventQ2MonthEndForNonCalendarNineMonth()
{
    var points = new[]
    {
        new FinancialPoint("2025-12-29", "2026-03-28", 100, "6-K", "2026-05-01", "ifrs-full:Revenue", Accn: "A1", SourceType: "filing-text", SourcePrecision: 3),
        new FinancialPoint("2025-12-29", "2026-09-26", 600, "6-K", "2026-11-01", "ifrs-full:Revenue", Accn: "A9", SourceType: "filing-text", SourcePrecision: 3),
        new FinancialPoint(null, "2026-09-26", 300, "10-Q", "2026-11-01", "ifrs-full:Revenue", Fp: "Q3", Accn: "A9", SourceType: "filing-text", SourcePrecision: 3),
    };

    var quarters = QuarterlySeriesBuilder.BuildQuarterlySeries(points);

    True(!quarters.Any(q => q.End == "2026-06-30"), "non-calendar 9M without Q3 start does not invent calendar Q2 end");
}

void QuarterlySeriesPrefersHigherPrecisionSameAccessionFact()
{
    var points = new[]
    {
        new FinancialPoint("2026-01-01", "2026-03-31", 11_711_000, "6-K", "2026-05-28", "ifrs-full:Revenue", Accn: "A1", SourceType: "filing-text", SourcePrecision: 3),
        new FinancialPoint("2026-01-01", "2026-03-31", 11_700_000, "6-K", "2026-05-29", "ifrs-full:Revenue", Accn: "A1", SourceType: "filing-text", SourcePrecision: 1),
    };

    var quarter = QuarterlySeriesBuilder.BuildQuarterlySeries(points).Single(q => q.End == "2026-03-31");

    Equal(11_711_000, quarter.Val, "higher precision same-accession fact beats later lower-precision fact");
}

void StatementSeriesIncludesCashFlowOnlyRows()
{
    static Dictionary<string, FinancialMetricValue> BlankFlowValues() =>
        FinancialMetricsCatalog.FlowMetrics.ToDictionary(m => m.Id, _ => FinancialMetricValue.Blank());

    var annual = BlankFlowValues();
    annual[FinancialMetricsCatalog.OperatingCashFlow.Id] = new FinancialMetricValue(
        211_000,
        FinancialMetricFormulaIds.DirectFact,
        "ifrs-full:CashFlowsFromUsedInOperatingActivities",
        "2025-08-28",
        "0001213900-25-081395",
        "reported");
    annual[FinancialMetricsCatalog.Capex.Id] = new FinancialMetricValue(
        37_000,
        FinancialMetricFormulaIds.DirectFact,
        "ifrs-full:PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities",
        "2025-08-28",
        "0001213900-25-081395",
        "reported");
    annual[FinancialMetricsCatalog.FreeCashFlow.Id] = new FinancialMetricValue(
        174_000,
        FinancialMetricFormulaIds.OperatingCashFlowMinusCapex,
        "ifrs-full:CashFlowsFromUsedInOperatingActivities|ifrs-full:PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities",
        "2025-08-28",
        "0001213900-25-081395",
        "derived");

    var row = new FinancialMetricPeriodRow(
        "CFO",
        "2025-06-30",
        2025,
        null,
        "FY 2025",
        FinancialMetricProviderNames.IfrsFull,
        FinancialMetricProviderNames.IfrsFull,
        "ok",
        [],
        "2025-08-28",
        BlankFlowValues(),
        annual,
        BlankFlowValues(),
        FinancialMetricsCatalog.PointMetrics.ToDictionary(m => m.Id, _ => FinancialMetricValue.Blank())
    );

    var statements = FinancialStatementSeriesBuilder.Build("CFO", new FinancialMetricsBuildResult([row], []));
    var annualRow = statements.Annual.Single();

    True(annualRow.Revenue is null, "cash-flow-only statement row does not require revenue");
    Equal(211_000, annualRow.OperatingCashFlow!.Value, "cash-flow-only statement row keeps OCF");
    Equal(37_000, annualRow.Capex!.Value, "cash-flow-only statement row keeps CapEx");
    Equal(174_000, annualRow.FreeCashFlow!.Value, "cash-flow-only statement row keeps FCF");
}

void MergePreservesEarliestFiledAsFirstFiled()
{
    // A period reported first in its original filing and again as a comparative in a much later
    // filing (e.g. a FY-N 10-K carrying FY-N-1 comparatives) must keep the latest restatement's
    // value/Filed but record the ORIGINAL filing date as FirstFiled, so valuation alignment is not
    // dragged ~2 years forward. Regression for the MSFT P/E ~55 chart bug.
    var merged = new Dictionary<string, FinancialPoint>();
    var original = new FinancialPoint("2023-01-01", "2023-03-31", 100, "10-Q", "2023-05-01", "NetIncomeLoss", Accn: "ORIG");
    var laterComparative = new FinancialPoint("2023-01-01", "2023-03-31", 101, "10-K", "2025-07-30", "NetIncomeLoss", Accn: "COMP");

    FinancialPointMerger.MergePeriodFact(merged, original);
    FinancialPointMerger.MergePeriodFact(merged, laterComparative);
    var point = merged.Values.Single();

    Equal(101, point.Val, "merge keeps the latest restatement's value");
    True(point.Filed == "2025-07-30", "merge keeps the latest restatement's Filed for value provenance");
    True(point.FirstFiled == "2023-05-01", "merge preserves the original disclosure date as FirstFiled");
}

void MergeRecordsMaterialRestatementVersion()
{
    // A period reported at 100, repeated as an identical comparative, then materially restated to 110
    // in a later accession. The merge keeps the latest value (110) but must record the as-known value
    // history â€” original 100 at its filing, restated 110 at its filing â€” while ignoring the identical
    // comparative. This history is what lets the chart show the original value on pre-restatement bars.
    var merged = new Dictionary<string, FinancialPoint>();
    FinancialPointMerger.MergePeriodFact(merged, new FinancialPoint("2023-01-01", "2023-03-31", 100, "10-Q", "2023-05-01", "NetIncomeLoss", Accn: "A1"));
    FinancialPointMerger.MergePeriodFact(merged, new FinancialPoint("2023-01-01", "2023-03-31", 100, "10-K", "2024-07-30", "NetIncomeLoss", Accn: "A2"));
    FinancialPointMerger.MergePeriodFact(merged, new FinancialPoint("2023-01-01", "2023-03-31", 110, "10-K/A", "2025-03-01", "NetIncomeLoss", Accn: "A3"));
    var point = merged.Values.Single();

    Equal(110, point.Val, "merge keeps the latest restated value");
    True(point.Versions is { Count: 2 }, "identical comparative adds no version; the material restatement does");
    True(point.Versions![0].Filed == "2023-05-01" && point.Versions[0].Val == 100, "first version = original as-known value and date");
    True(point.Versions[1].Filed == "2025-03-01" && point.Versions[1].Val == 110, "second version = restatement as-known value and date");

    // A sub-0.5% re-file updates the value but is not a restatement â€” no version segment.
    var immaterial = new Dictionary<string, FinancialPoint>();
    FinancialPointMerger.MergePeriodFact(immaterial, new FinancialPoint("2023-01-01", "2023-03-31", 100, "10-Q", "2023-05-01", "NetIncomeLoss", Accn: "B1"));
    FinancialPointMerger.MergePeriodFact(immaterial, new FinancialPoint("2023-01-01", "2023-03-31", 100.3, "10-K", "2024-07-30", "NetIncomeLoss", Accn: "B2"));
    var p2 = immaterial.Values.Single();
    Equal(100.3, p2.Val, "sub-materiality re-file still updates the value");
    True(p2.Versions is null, "a sub-0.5% change is not recorded as a restatement version");
}

void BuildTtmSeriesEmitsPointInTimeSegmentsForRestatedQuarter()
{
    // Four consecutive reported quarters; Q4 was restated from 100 (filed 2024-02-15) to 150 (filed
    // 2025-03-01). BuildTtmSeries must emit two point-in-time TTM segments at the same period end:
    // the original sum (400) as-of the window's first-all-public date, and the restated sum (450)
    // as-of the restatement filing date.
    var quarters = new List<FinancialPoint>
    {
        new("2023-01-01", "2023-03-31", 100, "10-Q", "2023-05-01", "NetIncomeLoss", Accn: "Q1", FirstFiled: "2023-05-01"),
        new("2023-04-01", "2023-06-30", 100, "10-Q", "2023-08-01", "NetIncomeLoss", Accn: "Q2", FirstFiled: "2023-08-01"),
        new("2023-07-01", "2023-09-30", 100, "10-Q", "2023-11-01", "NetIncomeLoss", Accn: "Q3", FirstFiled: "2023-11-01"),
        new("2023-10-01", "2023-12-31", 150, "10-K", "2025-03-01", "NetIncomeLoss", Accn: "Q4", FirstFiled: "2024-02-15",
            Versions: new List<FinancialPointVersion> { new("2024-02-15", 100), new("2025-03-01", 150) }),
    };

    var segments = QuarterlySeriesBuilder.BuildTtmSeries(quarters)
        .Where(t => t.End == "2023-12-31")
        .OrderBy(t => t.FirstFiled, StringComparer.Ordinal)
        .ToList();

    True(segments.Count == 2, "a restated window emits two point-in-time TTM segments");
    Equal(400, segments[0].Ttm, "pre-restatement segment sums the originally-filed quarter values");
    True(segments[0].FirstFiled == "2024-02-15", "first segment as-of = the window's first-all-public date");
    Equal(450, segments[1].Ttm, "post-restatement segment sums the restated quarter value");
    True(segments[1].FirstFiled == "2025-03-01", "second segment as-of = the restatement filing date");
}

void BuildTtmSeriesCarriesWindowProvenanceForTrace()
{
    // The trace no longer re-derives TTM windows; it MAPS the single production builder's output
    // (QuarterlySeriesBuilder.BuildTtmSeries). So the builder must carry the per-window provenance the
    // trace renders â€” the four Quarters, the distinct Accessions, and the first-disclosure FirstFiled
    // distinct from the latest-restatement Filed. This replaces the old reflection-based parity test
    // that compared two hand-written builders (the duplication this unification removed).
    static List<FinancialPoint> FourQuarters(string concept) =>
    [
        new("2023-01-01", "2023-03-31", 100, "10-Q", "2023-05-01", concept, Accn: "A1", FirstFiled: "2023-05-01"),
        new("2023-04-01", "2023-06-30", 100, "10-Q", "2023-08-01", concept, Accn: "A2", FirstFiled: "2023-08-01"),
        new("2023-07-01", "2023-09-30", 100, "10-Q", "2023-11-01", concept, Accn: "A3", FirstFiled: "2023-11-01"),
        // Q4 re-disclosed as a comparative in a 2025 10-K: Filed late, FirstFiled original.
        new("2023-10-01", "2023-12-31", 100, "10-K", "2025-02-15", concept, Accn: "A4", FirstFiled: "2024-02-15"),
    ];

    var window = QuarterlySeriesBuilder.BuildTtmSeries(FourQuarters("NetIncomeLoss"))
        .Single(p => p.End == "2023-12-31");

    Equal(400, window.Ttm, "TTM sums the four quarters");
    True(window.Filed == "2025-02-15", "TTM Filed is the latest restatement in the window");
    True(window.FirstFiled == "2024-02-15", "TTM FirstFiled is when all four quarters were first public");
    True(string.Compare(window.FirstFiled, window.Filed, StringComparison.Ordinal) <= 0, "FirstFiled is on/before Filed");
    True(window.Quarters is { Count: 4 }, "TTM carries its four quarters for the trace to render");
    True(
        window.Accessions is not null
        && window.Accessions.Split('|').OrderBy(a => a, StringComparer.Ordinal).SequenceEqual(["A1", "A2", "A3", "A4"]),
        "TTM carries the distinct quarter accessions");
}

void EdgarClientDerivesOperatingIncomeWhenDirectMissing()
{
    // No direct OperatingIncomeLoss, but revenue and total operating cost are present, so the
    // metric-derived EBIT (RevenueMinusTotalOperatingCost) must populate operating income for the
    // latest revenue period instead of leaving it blank.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": { "USD": [
              { "form": "10-Q", "start": "2026-01-01", "end": "2026-03-31", "val": 1000, "filed": "2026-05-01", "fy": 2026, "fp": "Q1" }
            ] }
          },
          "CostsAndExpenses": {
            "units": { "USD": [
              { "form": "10-Q", "start": "2026-01-01", "end": "2026-03-31", "val": 800, "filed": "2026-05-01", "fy": 2026, "fp": "Q1" }
            ] }
          }
        }
      }
    }
    """;

    var fundamentals = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFundamentalsAsync("DEROI").GetAwaiter().GetResult();

    var operatingIncome = fundamentals.OperatingIncomePoints
        .Where(p => p.End == "2026-03-31")
        .ToList();
    True(operatingIncome.Count > 0, "operating income derived for latest period when direct EBIT is absent");
    Equal(200, operatingIncome[0].Val, "derived EBIT equals revenue minus total operating cost");
}

void CachedCompanyFactsFixtureFeedsEdgarClient()
{
    var client = new LegacyClient(new FixtureSecCompanyDataSource(FixtureRoot()));

    var fundamentals = client.FetchFundamentalsAsync("FIX").GetAwaiter().GetResult();
    True(fundamentals.Eps.Count == 1, "cached fixture diluted EPS parsed");
    True(fundamentals.RevenuePoints.Count == 4, "cached fixture revenue parsed");
    True(fundamentals.SharesOutstanding.Count == 1, "cached fixture shares parsed");

    var fetched = client.FetchRevenueAndCogsPointsAsync("FIX").GetAwaiter().GetResult();
    True(fetched.CogsPoints.Count == 2, "cached fixture COGS parsed");

    var series = RevenueSeriesBuilder.Build("FIX", fetched.RevenuePoints);
    var q3 = series.Quarters.Single(q => q.End == "2023-09-30");
    var q4 = series.Quarters.Single(q => q.End == "2023-12-31");

    Equal(3000, q3.QuarterlyRevenue, "fixture Q3 derived revenue");
    Equal(4000, q4.QuarterlyRevenue, "fixture Q4 derived revenue");
}

void EdgarClientUsesDeiSharesForFundamentals()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-K", "start": "2023-01-01", "end": "2023-12-31", "val": 1000, "filed": "2024-02-01", "fy": 2023, "fp": "FY" }
              ]
            }
          }
        },
        "dei": {
          "EntityCommonStockSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-K", "end": "2023-12-31", "val": 100, "filed": "2024-02-01", "fy": 2023, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var fundamentals = client.FetchFundamentalsAsync("DEISH").GetAwaiter().GetResult();

    True(fundamentals.SharesOutstanding.Count == 1, "DEI shares fallback parsed");
    Equal(100, fundamentals.SharesOutstanding[0].Val, "DEI shares value");
    True(fundamentals.SharesOutstanding[0].Concept == "dei:EntityCommonStockSharesOutstanding", "DEI shares source concept");
}

void EdgarClientUsesWeightedAverageSharesForAnalysisFundamentals()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 1000, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "WeightedAverageNumberOfDilutedSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 100, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var fundamentals = client.FetchFundamentalsAsync("WGTSH").GetAwaiter().GetResult();

    True(fundamentals.SharesOutstanding.Count == 1, "weighted average shares fallback parsed");
    Equal(100, fundamentals.SharesOutstanding[0].Val, "weighted average shares value");
    True(fundamentals.SharesOutstanding[0].Concept == "WeightedAverageNumberOfDilutedSharesOutstanding", "weighted average shares source concept");
}

void EdgarClientUsesCoverPageSharesWhenCompanyfactsLacksRecentOutstanding()
{
    // ERIE-style dual-class filer: every share fact in companyfacts is share-class
    // dimensioned, so companyfacts drops them and retains only a single stale per-class
    // weighted-average leak. The cover-page fallback must supply outstanding shares and the
    // stale weighted-average leak must be dropped so it cannot poison per-share valuation.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "NetIncomeLoss": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2026-01-01", "end": "2026-03-31", "val": 150474000, "filed": "2026-04-23", "fy": 2026, "fp": "Q1" }
              ]
            }
          },
          "WeightedAverageNumberOfDilutedSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-Q", "start": "2021-01-01", "end": "2021-06-30", "val": 2542, "filed": "2021-07-29", "fy": 2021, "fp": "Q2" }
              ]
            }
          }
        }
      }
    }
    """;

    var source = new InlineSecCompanyDataSource(json)
    {
        CoverShareFacts =
        [
            new FinancialPoint(null, "2026-04-17", 46_191_610, "10-Q", "2026-04-23",
                "dei:EntityCommonStockSharesOutstanding", Accn: "C1", SourceType: "filing-cover")
        ]
    };
    var client = new LegacyClient(source);
    var fundamentals = client.FetchFundamentalsAsync("ERIE").GetAwaiter().GetResult();

    True(source.CoverShareStage2FetchCount == 1, "cover history fetched when no recent outstanding fact exists");
    True(fundamentals.SharesOutstanding.Count == 1, "cover-page shares replace the stale weighted-average leak");
    Equal(46_191_610, fundamentals.SharesOutstanding[0].Val, "cover-page outstanding shares used for valuation");
    True(fundamentals.SharesOutstanding[0].Concept == "dei:EntityCommonStockSharesOutstanding", "cover-page share source concept");
}

void EdgarClientKeepsCompanyfactsWhenCoverMatches()
{
    // AAPL/GOOGL-style single-class / fully-tagged filer: companyfacts has a recent
    // outstanding fact and the cover total matches it within tolerance. Reconciliation does a
    // single stage-1 probe, keeps companyfacts, and must NOT fetch the cover history (stage 2).
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "NetIncomeLoss": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2025-01-01", "end": "2025-03-31", "val": 1000, "filed": "2025-05-01", "fy": 2025, "fp": "Q1" }
              ]
            }
          },
          "CommonStockSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-Q", "end": "2025-03-31", "val": 500, "filed": "2025-05-01", "fy": 2025, "fp": "Q1" }
              ]
            }
          }
        }
      }
    }
    """;

    var source = new InlineSecCompanyDataSource(json)
    {
        CoverShareFacts =
        [
            new FinancialPoint(null, "2025-04-10", 500, "10-Q", "2025-05-01",
                "dei:EntityCommonStockSharesOutstanding", SourceType: "filing-cover")
        ]
    };
    var client = new LegacyClient(source);
    var fundamentals = client.FetchFundamentalsAsync("NORM").GetAwaiter().GetResult();

    True(source.CoverShareStage2FetchCount == 0, "cover history not fetched when cover matches companyfacts");
    True(fundamentals.SharesOutstanding.Count == 1, "companyfacts outstanding shares retained");
    Equal(500, fundamentals.SharesOutstanding[0].Val, "companyfacts outstanding share value retained");
}

void EdgarClientPrefersCoverWhenItExceedsCompanyfacts()
{
    // HEI-style partial dimensioning: companyfacts retains a recent single-class outstanding
    // fact (55M) but the other class is dimensioned-dropped, so the cover total (~138M) far
    // exceeds it. Reconciliation must prefer cover and replace the incomplete companyfacts
    // point rather than concatenating (which would double-represent the denominator).
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "NetIncomeLoss": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2025-01-01", "end": "2025-03-31", "val": 1000, "filed": "2025-05-01", "fy": 2025, "fp": "Q1" }
              ]
            }
          },
          "CommonStockSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-Q", "end": "2025-03-31", "val": 55100000, "filed": "2025-05-01", "fy": 2025, "fp": "Q1" }
              ]
            }
          }
        }
      }
    }
    """;

    var source = new InlineSecCompanyDataSource(json)
    {
        CoverShareFacts =
        [
            new FinancialPoint(null, "2025-04-10", 138000000, "10-Q", "2025-05-01",
                "dei:EntityCommonStockSharesOutstanding", SourceType: "filing-cover")
        ]
    };
    var client = new LegacyClient(source);
    var fundamentals = client.FetchFundamentalsAsync("HEI").GetAwaiter().GetResult();

    True(source.CoverShareStage2FetchCount == 1, "cover history fetched when cover exceeds companyfacts");
    True(fundamentals.SharesOutstanding.Count == 1, "incomplete companyfacts point replaced by cover");
    Equal(138000000, fundamentals.SharesOutstanding[0].Val, "cover total used as the denominator");
}

void EdgarClientDedupsCoverSeriesByPeriodEnd()
{
    // A 10-K and a later 10-K/A report the same period-end outstanding count; the corrected
    // (latest filed) value must win and the period must appear once in the percentile series.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "NetIncomeLoss": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2025-01-01", "end": "2025-03-31", "val": 1000, "filed": "2026-02-15", "fy": 2025, "fp": "FY" }
              ]
            }
          },
          "WeightedAverageNumberOfDilutedSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-Q", "start": "2021-01-01", "end": "2021-06-30", "val": 2542, "filed": "2021-07-29", "fy": 2021, "fp": "Q2" }
              ]
            }
          }
        }
      }
    }
    """;

    var source = new InlineSecCompanyDataSource(json)
    {
        CoverShareFacts =
        [
            new FinancialPoint(null, "2025-12-31", 100, "10-K", "2026-02-01",
                "dei:EntityCommonStockSharesOutstanding", SourceType: "filing-cover"),
            new FinancialPoint(null, "2025-12-31", 110, "10-K/A", "2026-02-15",
                "dei:EntityCommonStockSharesOutstanding", SourceType: "filing-cover")
        ]
    };
    var client = new LegacyClient(source);
    var fundamentals = client.FetchFundamentalsAsync("DUP").GetAwaiter().GetResult();

    True(fundamentals.SharesOutstanding.Count == 1, "duplicate period-end collapsed to one cover point");
    Equal(110, fundamentals.SharesOutstanding[0].Val, "latest filed value wins for the duplicated period");
}

void SecCoverShareReaderAppliesEconomicClassRatios()
{
    // BRK-style economically-unequal dual-class: 1 Class A = 1,500 Class B. The cover reports
    // each class with a StatementClassOfStockAxis explicit member, so the reader applies the
    // per-class ratio when the ticker is in the override table, and falls back to a raw sum
    // when it is not.
    const string content = """
    <xbrli:context id="cA"><xbrli:entity><xbrli:segment><xbrldi:explicitMember dimension="us-gaap:StatementClassOfStockAxis">us-gaap:CommonClassAMember</xbrldi:explicitMember></xbrli:segment></xbrli:entity><xbrli:period><xbrli:instant>2026-03-31</xbrli:instant></xbrli:period></xbrli:context>
    <xbrli:context id="cB"><xbrli:entity><xbrli:segment><xbrldi:explicitMember dimension="us-gaap:StatementClassOfStockAxis">us-gaap:CommonClassBMember</xbrldi:explicitMember></xbrli:segment></xbrli:entity><xbrli:period><xbrli:instant>2026-03-31</xbrli:instant></xbrli:period></xbrli:context>
    <span>Class A <ix:nonFraction unitRef="shares" contextRef="cA" decimals="INF" name="dei:EntityCommonStockSharesOutstanding" scale="0">600000</ix:nonFraction></span>
    <span>Class B <ix:nonFraction unitRef="shares" contextRef="cB" decimals="INF" name="dei:EntityCommonStockSharesOutstanding" scale="0">1300000000</ix:nonFraction></span>
    """;

    var document = new SecFilingDocument(
        "0001067983", "BRK-B", "10-Q", "0000950123-26-000001",
        "2026-04-23", "2026-03-31", "brk-20260331.htm", content);

    var brkFact = SecCoverShareReader.ReadOutstandingShares(document, "BRK-B");
    True(brkFact is not null, "per-class cover shares parsed");
    // 600,000 Class A x 1500 + 1,300,000,000 Class B x 1 = 2,200,000,000 B-equivalent.
    Equal(2_200_000_000, brkFact!.Val, "Class A converted to B-equivalent before summing");

    var rawFact = SecCoverShareReader.ReadOutstandingShares(document);
    True(rawFact is not null, "raw cover shares parsed without ticker");
    // No override -> ratio 1.0 for each class -> raw sum.
    Equal(1_300_600_000, rawFact!.Val, "default sums raw per-class counts");
}

void SecCoverShareReaderSumsShareClassContexts()
{
    // Mirrors ERIE's 10-Q cover page: two share classes reported as separate undimensioned
    // dei:EntityCommonStockSharesOutstanding facts whose class distinction lives in the
    // context. Distinct per-class values at the latest instant are summed.
    const string content = """
    <xbrli:context id="c-2"><xbrli:period><xbrli:instant>2026-04-17</xbrli:instant></xbrli:period></xbrli:context>
    <xbrli:context id="c-3"><xbrli:period><xbrli:instant>2026-04-17</xbrli:instant></xbrli:period></xbrli:context>
    <span>Class A <ix:nonFraction unitRef="shares" contextRef="c-2" decimals="INF" name="dei:EntityCommonStockSharesOutstanding" format="ixt:num-dot-decimal" scale="0" id="f-24">46,189,068</ix:nonFraction></span>
    <span>Class B <ix:nonFraction unitRef="shares" contextRef="c-3" decimals="INF" name="dei:EntityCommonStockSharesOutstanding" format="ixt:num-dot-decimal" scale="0" id="f-25">2,542</ix:nonFraction></span>
    """;

    var document = new SecFilingDocument(
        "0000922621", "ERIE", "10-Q", "0001628280-26-026866",
        "2026-04-23", "2026-03-31", "erie-20260331.htm", content);
    var fact = SecCoverShareReader.ReadOutstandingShares(document);

    True(fact is not null, "cover-page shares parsed");
    Equal(46_191_610, fact!.Val, "Class A + Class B share counts summed");
    True(fact.End == "2026-04-17", "cover instant date used as period end");
    True(fact.Concept == "dei:EntityCommonStockSharesOutstanding", "cover-page share source concept");
    True(fact.SourceType == "filing-cover", "cover-page share source type");
    True(fact.Start is null, "cover-page shares are point-in-time");
}

void EdgarClientReadsAmendedFinancialForms()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-Q/A", "start": "2024-01-01", "end": "2024-03-31", "val": 1234, "filed": "2024-05-10", "fy": 2024, "fp": "Q1", "accn": "A1" }
              ]
            }
          }
        }
      }
    }
    """;

    var source = new InlineSecCompanyDataSource(json);
    var client = new LegacyClient(source);
    var fundamentals = client.FetchFundamentalsAsync("AMEND").GetAwaiter().GetResult();

    Equal(1234, fundamentals.RevenuePoints.Single().Val, "amended 10-Q facts are included");
}

void EdgarClientFundamentalsCanSkipTrackingForTrace()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 1234, "filed": "2024-05-10", "fy": 2024, "fp": "Q1", "accn": "A1" }
              ]
            }
          }
        }
      }
    }
    """;

    var source = new InlineSecCompanyDataSource(json);
    var client = new LegacyClient(source);

    var traceFundamentals = client.FetchFundamentalsForTraceAsync("TRACE").GetAwaiter().GetResult();
    Equal(1234, traceFundamentals.RevenuePoints.Single().Val, "trace fundamentals still parse SEC data");

    client.FetchFundamentalsAsync("TRACE").GetAwaiter().GetResult();
}

void EdgarClientUsesBankRevenueFallbackForMissingPeriodsOnly()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 100, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "InterestIncomeExpenseNet": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-04-01", "end": "2024-06-30", "val": 40, "filed": "2024-08-01", "fy": 2024, "fp": "Q2", "accn": "Q2OLD" },
                { "form": "10-Q/A", "start": "2024-04-01", "end": "2024-06-30", "val": 50, "filed": "2024-08-15", "fy": 2024, "fp": "Q2", "accn": "Q2AMEND" }
              ]
            }
          },
          "NoninterestIncome": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-04-01", "end": "2024-06-30", "val": 30, "filed": "2024-08-01", "fy": 2024, "fp": "Q2", "accn": "Q2OLD" },
                { "form": "10-Q/A", "start": "2024-04-01", "end": "2024-06-30", "val": 35, "filed": "2024-08-15", "fy": 2024, "fp": "Q2", "accn": "Q2AMEND" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var fundamentals = client.FetchFundamentalsAsync("BANK").GetAwaiter().GetResult();

    Equal(100, fundamentals.RevenuePoints.Single(p => p.End == "2024-03-31").Val, "direct revenue period is preserved");
    var q2 = fundamentals.RevenuePoints.Single(p => p.End == "2024-06-30");
    Equal(85, q2.Val, "bank fallback uses latest amended revenue components");
    True(q2.Form == "10-Q/A", "bank fallback keeps amended source form");
    True(q2.Accn == "Q2AMEND", "bank fallback keeps amended accession");
}

void EdgarClientDoesNotTrackEmptyCompanyFacts()
{
    var source = new InlineSecCompanyDataSource("""{ "facts": {} }""");
    var client = new LegacyClient(source);

    var fundamentals = client.FetchFundamentalsAsync("EMPTY").GetAwaiter().GetResult();

    True(fundamentals.RevenuePoints.Count == 0, "empty companyfacts returns no fundamentals");
}

void EdgarClientUsesIfrsFundamentalsDespiteSparseUsGaapNotes()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "ResearchAndDevelopmentExpense": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 50, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          }
        },
        "ifrs-full": {
          "DilutedEarningsLossPerShare": {
            "units": {
              "USD/shares": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 0.07, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "BasicEarningsLossPerShare": {
            "units": {
              "USD/shares": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 0.08, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "Revenue": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 1000, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "GrossProfit": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 600, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "ProfitLossFromOperatingActivities": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 150, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "ProfitLoss": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 100, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "CashFlowsFromUsedInOperatingActivities": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 120, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 20, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "CurrentAssets": {
            "units": {
              "USD": [
                { "form": "6-K", "end": "2024-03-31", "val": 900, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "CurrentLiabilities": {
            "units": {
              "USD": [
                { "form": "6-K", "end": "2024-03-31", "val": 300, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "Equity": {
            "units": {
              "USD": [
                { "form": "6-K", "end": "2024-03-31", "val": 700, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          },
          "NumberOfSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "6-K", "end": "2024-03-31", "val": 10000, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var fundamentals = client.FetchFundamentalsAsync("IFRSEPS").GetAwaiter().GetResult();

    Equal(0.07, fundamentals.Eps.Single().Val, "IFRS diluted EPS parsed despite sparse US-GAAP notes");
    True(fundamentals.Eps.Single().Concept == "ifrs-full:DilutedEarningsLossPerShare", "IFRS diluted EPS source concept");
    Equal(1000, fundamentals.RevenuePoints.Single().Val, "IFRS revenue parsed for fundamentals");
    Equal(600, fundamentals.GrossProfitPoints.Single().Val, "IFRS gross profit parsed for fundamentals");
    Equal(150, fundamentals.OperatingIncomePoints.Single().Val, "IFRS operating profit parsed for fundamentals");
    Equal(100, fundamentals.NetIncomeLossPoints.Single().Val, "IFRS net income parsed for fundamentals");
    Equal(120, fundamentals.OperatingCashFlowPoints.Single().Val, "IFRS OCF parsed for fundamentals");
    Equal(20, fundamentals.CapexPoints.Single().Val, "IFRS CapEx parsed for fundamentals");
    Equal(50, fundamentals.RdExpensePoints.Single().Val, "sparse US-GAAP R&D note still parsed");
    Equal(900, fundamentals.CurrentAssetsPoints.Single().Val, "IFRS current assets parsed");
    Equal(300, fundamentals.CurrentLiabilitiesPoints.Single().Val, "IFRS current liabilities parsed");
    Equal(700, fundamentals.EquityPoints.Single().Val, "IFRS equity parsed");
    Equal(10000, fundamentals.SharesOutstanding.Single().Val, "IFRS shares parsed");
}

void EdgarClientPrefersLatestFiledShareFactForFundamentals()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-K", "start": "2019-01-01", "end": "2019-12-31", "val": 1000, "filed": "2020-02-01", "fy": 2019, "fp": "FY" }
              ]
            }
          },
          "CommonStockSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-K", "end": "2019-12-31", "val": 100, "filed": "2020-02-01", "fy": 2019, "fp": "FY" },
                { "form": "10-K", "end": "2019-12-31", "val": 400, "filed": "2020-09-01", "fy": 2019, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var fundamentals = client.FetchFundamentalsAsync("RESTATEDSH").GetAwaiter().GetResult();

    True(fundamentals.SharesOutstanding.Count == 1, "restated shares are deduplicated");
    Equal(400, fundamentals.SharesOutstanding[0].Val, "latest filed share fact wins");
}

void EdgarClientSumsShareClassesWithinAFiling()
{
    // A multi-class filer (e.g. Alphabet Class A/B/C) reports several same-period share facts in
    // one filing; SEC companyfacts strips the class dimension, so they must be summed to a total.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-K", "start": "2023-01-01", "end": "2023-12-31", "val": 1000, "filed": "2024-02-01", "fy": 2023, "fp": "FY" }
              ]
            }
          },
          "CommonStockSharesOutstanding": {
            "units": {
              "shares": [
                { "form": "10-K", "end": "2023-12-31", "val": 5800, "filed": "2024-02-01", "accn": "0001" },
                { "form": "10-K", "end": "2023-12-31", "val": 900,  "filed": "2024-02-01", "accn": "0001" },
                { "form": "10-K", "end": "2023-12-31", "val": 5500, "filed": "2024-02-01", "accn": "0001" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var fundamentals = client.FetchFundamentalsAsync("MULTICLASS").GetAwaiter().GetResult();

    True(fundamentals.SharesOutstanding.Count == 1, "multi-class shares collapse to one period point");
    Equal(12200, fundamentals.SharesOutstanding[0].Val, "share classes in one filing are summed to total shares");
}

void AnalysisNetIncomePrefersParentOverConsolidated()
{
    // Q1: filer reports BOTH consolidated ProfitLoss (incl. the noncontrolling-interest slice) and
    // parent-attributable NetIncomeLoss (smaller) -> valuation must use parent. Q2: only ProfitLoss
    // is tagged -> valuation must fall back to consolidated for that period (no coverage loss), while
    // the export "Net Income" line stays consolidated-first throughout.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "ProfitLoss": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 120, "filed": "2024-05-01", "fp": "Q1" },
            { "form": "10-Q", "start": "2024-04-01", "end": "2024-06-30", "val": 130, "filed": "2024-08-01", "fp": "Q2" }
          ] } },
          "NetIncomeLoss": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 100, "filed": "2024-05-01", "fp": "Q1" }
          ] } }
        }
      }
    }
    """;
    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("NCITEST").GetAwaiter().GetResult();

    var valuation = FinancialMetricInputBuilder
        .PointsForAnyProvider(inputs, FundamentalsBuilder.ValuationNetIncomeConcepts);
    Equal(100, valuation.Single(p => p.End == "2024-03-31").Val, "Q1 valuation net income prefers parent NetIncomeLoss");
    Equal(130, valuation.Single(p => p.End == "2024-06-30").Val, "Q2 valuation net income falls back to consolidated ProfitLoss when parent is untagged");

    var exportNi = FinancialMetricInputBuilder
        .PointsForAnyProvider(inputs, FinancialMetricsCatalog.NetIncome);
    Equal(120, exportNi.Single(p => p.End == "2024-03-31").Val, "Q1 export Net Income keeps consolidated ProfitLoss");
}

void EdgarClientSkipsNullFactValues()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": {
            "units": {
              "USD": [
                { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": null, "filed": "2024-05-01", "fy": null },
                { "form": "10-Q", "start": "2024-04-01", "end": "2024-06-30", "val": 123, "filed": "2024-08-01", "fy": null }
              ]
            }
          }
        }
      }
    }
    """;
    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("NULLVAL").GetAwaiter().GetResult();
    var revenues = inputs.PointsFor(FinancialMetricsCatalog.UsGaap("Revenues"));

    True(revenues.Count == 1, "null SEC fact values are skipped");
    Equal(123, revenues[0].Val, "non-null SEC fact remains");
}

void FinancialMetricsBuilderUsesIfrsProviderAndAnnualPointFacts()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": {
            "units": {
              "USD": [
                { "form": "20-F", "start": "2023-01-01", "end": "2023-12-31", "val": 1000, "filed": "2024-03-01", "fy": 2023, "fp": "FY" }
              ]
            }
          },
          "Assets": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2023-12-31", "val": 5000, "filed": "2024-03-01", "fy": 2023, "fp": "FY" }
              ]
            }
          },
          "Liabilities": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2023-12-31", "val": 2200, "filed": "2024-03-01", "fy": 2023, "fp": "FY" }
              ]
            }
          },
          "CashAndCashEquivalents": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2023-12-31", "val": 800, "filed": "2024-03-01", "fy": 2023, "fp": "FY" }
              ]
            }
          },
          "Borrowings": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2023-12-31", "val": 900, "filed": "2024-03-01", "fy": 2023, "fp": "FY" }
              ]
            }
          },
          "NumberOfSharesIssued": {
            "units": {
              "shares": [
                { "form": "20-F", "end": "2023-12-31", "val": 100, "filed": "2024-03-01", "fy": 2023, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("IFRS").GetAwaiter().GetResult();
    True(inputs.Profile.Provider == FinancialMetricProvider.IfrsFull, "IFRS profile selected");
    True(inputs.Profile.Forms.Contains("20-F"), "IFRS profile records foreign form");

    var result = FinancialMetricsBuilder.Build("IFRS", inputs);
    var row = result.Rows.Single(r => r.PeriodEnd == "2023-12-31");
    True(row.Provider == FinancialMetricProviderNames.IfrsFull, "IFRS provider exported");
    True(!row.Quarterly["revenue"].Value.HasValue, "annual-only IFRS revenue does not invent a quarter");
    Equal(5000, row.Point["totalAssets"].Value!.Value, "IFRS assets parsed");
    True(row.Point["totalAssets"].SourceConcepts == "ifrs-full:Assets", "IFRS source concept includes taxonomy");
    True(result.AuditRows.Any(a => a.Provider == FinancialMetricProviderNames.IfrsFull && a.Taxonomy == FinancialMetricProviderNames.IfrsFull), "IFRS audit metadata exported");
}

void FinancialMetricsBuilderSubtractsIfrsGoodwillAndOtherIntangibles()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": {
            "units": {
              "USD": [
                { "form": "20-F", "start": "2024-01-01", "end": "2024-12-31", "val": 1000, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          },
          "EquityAttributableToOwnersOfParent": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2024-12-31", "val": 2455, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          },
          "Goodwill": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2024-12-31", "val": 613, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          },
          "IntangibleAssetsOtherThanGoodwill": {
            "units": {
              "USD": [
                { "form": "20-F", "end": "2024-12-31", "val": 745, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("IFRS_TBV").GetAwaiter().GetResult();
    var result = FinancialMetricsBuilder.Build("IFRS_TBV", inputs);
    var row = result.Rows.Single(r => r.PeriodEnd == "2024-12-31");

    Equal(1097, row.Point["netTangibleAssets"].Value!.Value, "IFRS net tangible assets subtract goodwill and other intangibles");
    Equal(1097, row.Point["tangibleBookValue"].Value!.Value, "IFRS tangible book value matches net tangible assets");
    True(row.Point["netTangibleAssets"].SourceConcepts == "ifrs-full:EquityAttributableToOwnersOfParent|ifrs-full:Goodwill|ifrs-full:IntangibleAssetsOtherThanGoodwill", "IFRS tangible source concepts include goodwill and other intangibles separately");
}

void FinancialMetricsBuilderUsesIfrsInterimDurationFacts()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-03-31", "val": 100, "filed": "2024-05-01", "fy": 2024, "fp": "Q1" },
                { "form": "6-K", "start": "2024-01-01", "end": "2024-06-30", "val": 250, "filed": "2024-08-01", "fy": 2024, "fp": "Q2" },
                { "form": "6-K", "start": "2024-01-01", "end": "2024-09-30", "val": 450, "filed": "2024-11-01", "fy": 2024, "fp": "Q3" },
                { "form": "20-F", "start": "2024-01-01", "end": "2024-12-31", "val": 700, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("IFRS6K").GetAwaiter().GetResult();
    var result = FinancialMetricsBuilder.Build("IFRS6K", inputs);

    var q2 = result.Rows.Single(r => r.PeriodEnd == "2024-06-30");
    var q3 = result.Rows.Single(r => r.PeriodEnd == "2024-09-30");
    var q4 = result.Rows.Single(r => r.PeriodEnd == "2024-12-31");

    Equal(150, q2.Quarterly["revenue"].Value!.Value, "IFRS Q2 derived from 6-K H1");
    True(q2.Quarterly["revenue"].Formula == FinancialMetricFormulaIds.DerivedFromH1, "IFRS Q2 formula id");
    Equal(200, q3.Quarterly["revenue"].Value!.Value, "IFRS Q3 derived from 6-K 9M");
    True(q3.Quarterly["revenue"].Formula == FinancialMetricFormulaIds.DerivedFrom9M, "IFRS Q3 formula id");
    Equal(250, q4.Quarterly["revenue"].Value!.Value, "IFRS Q4 derived from 20-F annual");
    True(q4.Quarterly["revenue"].Formula == FinancialMetricFormulaIds.DerivedFromAnnual, "IFRS Q4 formula id");
    Equal(700, q4.Ttm["revenue"].Value!.Value, "IFRS TTM from four reconstructed quarters");
    True(q4.Provider == FinancialMetricProviderNames.IfrsFull, "IFRS provider retained for mixed filings");
}

void FinancialMetricsBuilderDerivesCumulativeOnlyIfrsQuartersWithoutQ1()
{
    // IFRS filers that only publish H1 and 9M 6-K reports (no standalone Q1) should still
    // get Q3 = 9M - H1 and Q4 = Annual - 9M. TTM requires four consecutive quarters so it
    // remains blank when only Q3/Q4 of a single year are available.
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": {
            "units": {
              "USD": [
                { "form": "6-K", "start": "2024-01-01", "end": "2024-06-30", "val": 250, "filed": "2024-08-01", "fy": 2024, "fp": "H1" },
                { "form": "6-K", "start": "2024-01-01", "end": "2024-09-30", "val": 450, "filed": "2024-11-01", "fy": 2024, "fp": "Q3" },
                { "form": "20-F", "start": "2024-01-01", "end": "2024-12-31", "val": 700, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("IFRS_NO_Q1").GetAwaiter().GetResult();
    var result = FinancialMetricsBuilder.Build("IFRS_NO_Q1", inputs);

    var q3 = result.Rows.Single(r => r.PeriodEnd == "2024-09-30");
    var q4 = result.Rows.Single(r => r.PeriodEnd == "2024-12-31");

    Equal(200, q3.Quarterly["revenue"].Value!.Value, "Q3 = 9M - H1 derived without Q1");
    True(q3.Quarterly["revenue"].Formula == FinancialMetricFormulaIds.DerivedFrom9M, "Q3 formula id");
    Equal(250, q4.Quarterly["revenue"].Value!.Value, "Q4 = Annual - 9M derived without Q1/Q2/Q3 individually");
    True(q4.Quarterly["revenue"].Formula == FinancialMetricFormulaIds.DerivedFromAnnual, "Q4 formula id");
    True(!q4.Ttm["revenue"].Value.HasValue, "TTM blank when only Q3+Q4 available (need four consecutive quarters)");
}

void FinancialMetricsTtmSkipsNonConsecutiveHalfYearWindows()
{
    // Two years of H1/9M/Annual-only data produce Q3+Q4 for each year.
    // The 4-quarter window [Q3_PY, Q4_PY, Q3_CY, Q4_CY] is not four consecutive quarters
    // (the fiscal-quarter index jumps from Q4_PY straight to Q3_CY), so it must be excluded
    // from TTM to avoid summing H2_PY + H2_CY incorrectly.
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": {
            "units": {
              "USD": [
                { "form": "6-K",  "start": "2023-01-01", "end": "2023-06-30", "val": 200, "filed": "2023-08-01", "fy": 2023, "fp": "H1" },
                { "form": "6-K",  "start": "2023-01-01", "end": "2023-09-30", "val": 350, "filed": "2023-11-01", "fy": 2023, "fp": "Q3" },
                { "form": "20-F", "start": "2023-01-01", "end": "2023-12-31", "val": 550, "filed": "2024-03-01", "fy": 2023, "fp": "FY" },
                { "form": "6-K",  "start": "2024-01-01", "end": "2024-06-30", "val": 250, "filed": "2024-08-01", "fy": 2024, "fp": "H1" },
                { "form": "6-K",  "start": "2024-01-01", "end": "2024-09-30", "val": 450, "filed": "2024-11-01", "fy": 2024, "fp": "Q3" },
                { "form": "20-F", "start": "2024-01-01", "end": "2024-12-31", "val": 700, "filed": "2025-03-01", "fy": 2024, "fp": "FY" }
              ]
            }
          }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("IFRS_TWO_YEAR").GetAwaiter().GetResult();
    var result = FinancialMetricsBuilder.Build("IFRS_TWO_YEAR", inputs);

    // Q3 and Q4 for both years should be derived
    var q3_2023 = result.Rows.Single(r => r.PeriodEnd == "2023-09-30");
    var q4_2023 = result.Rows.Single(r => r.PeriodEnd == "2023-12-31");
    var q3_2024 = result.Rows.Single(r => r.PeriodEnd == "2024-09-30");
    var q4_2024 = result.Rows.Single(r => r.PeriodEnd == "2024-12-31");

    Equal(150, q3_2023.Quarterly["revenue"].Value!.Value, "FY2023 Q3 = 9M - H1");
    Equal(200, q4_2023.Quarterly["revenue"].Value!.Value, "FY2023 Q4 = Annual - 9M");
    Equal(200, q3_2024.Quarterly["revenue"].Value!.Value, "FY2024 Q3 = 9M - H1");
    Equal(250, q4_2024.Quarterly["revenue"].Value!.Value, "FY2024 Q4 = Annual - 9M");

    // TTM must be blank: the window [Q3_2023, Q4_2023, Q3_2024, Q4_2024] is H2_2023 + H2_2024,
    // not four consecutive quarters, so it is not a valid trailing-12-month period.
    True(!q4_2024.Ttm["revenue"].Value.HasValue, "TTM blank for non-consecutive Q3+Q4 window");

    // Also verify BuildTtmSeries (analysis/P/S path) skips the non-consecutive window.
    var revenuePoints2 = new[]
    {
        new FinancialPoint("2023-01-01", "2023-06-30", 200, "6-K",  "2023-08-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2023-01-01", "2023-09-30", 350, "6-K",  "2023-11-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2023-01-01", "2023-12-31", 550, "20-F", "2024-03-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2024-01-01", "2024-06-30", 250, "6-K",  "2024-08-01", "ifrs-full:Revenue", 2024),
        new FinancialPoint("2024-01-01", "2024-09-30", 450, "6-K",  "2024-11-01", "ifrs-full:Revenue", 2024),
        new FinancialPoint("2024-01-01", "2024-12-31", 700, "20-F", "2025-03-01", "ifrs-full:Revenue", 2024),
    };
    var ttmSeries = QuarterlySeriesBuilder.BuildTtmSeries(revenuePoints2);
    // Quarterly-sum TTM is still zero (no 4-consecutive-quarter window). Cumulative YTD TTM
    // now fills the annual and 9M period ends so H1/9M-only filers get a visible TTM.
    Equal(3, ttmSeries.Count, "BuildTtmSeries produces cumulative TTM for Dec'23, Sep'24, Dec'24");
    Equal(550.0, ttmSeries.Single(t => t.End == "2023-12-31").Ttm, "TTM at FY2023 end = annual value");
    Equal(650.0, ttmSeries.Single(t => t.End == "2024-09-30").Ttm, "TTM at Sep'24 = 550 - 350 + 450");
    Equal(700.0, ttmSeries.Single(t => t.End == "2024-12-31").Ttm, "TTM at FY2024 end = annual value");
}

void BuildTtmSeriesSkipsWindowWithSingleMissingQuarter()
{
    // A one-quarter gap (Q3 2023 missing) leaves Q1, Q2, Q4 of 2023 plus Q1 2024. The first-to-last
    // span is only ~366 days, so a broad day-span threshold would wrongly accept it; a true
    // consecutiveness check (fiscal/calendar quarter index) must reject it because Q2â†’Q4 skips Q3.
    var points = new[]
    {
        new FinancialPoint("2023-01-01", "2023-03-31", 10, "10-Q", "2023-05-01", "us-gaap:Revenues", 2023, "Q1"),
        new FinancialPoint("2023-04-01", "2023-06-30", 20, "10-Q", "2023-08-01", "us-gaap:Revenues", 2023, "Q2"),
        // Q3 2023 (period ending 2023-09-30) intentionally absent.
        new FinancialPoint("2023-10-01", "2023-12-31", 40, "10-Q", "2024-02-01", "us-gaap:Revenues", 2023, "Q4"),
        new FinancialPoint("2024-01-01", "2024-03-31", 50, "10-Q", "2024-05-01", "us-gaap:Revenues", 2024, "Q1"),
    };

    var ttmSeries = QuarterlySeriesBuilder.BuildTtmSeries(points);
    True(ttmSeries.Count == 0, "BuildTtmSeries skips a 4-quarter window with a single missing quarter (~366-day span)");
}

void AnnualMinusNineMonthRejectsOverlappingNineMonthPeriod()
{
    var points = new[]
    {
        new FinancialPoint("2023-10-01", "2024-03-31", 200, "6-K", "2024-05-01", "ifrs-full:Revenue", 2024, "H1"),
        new FinancialPoint("2023-10-01", "2024-06-30", 350, "6-K", "2024-08-01", "ifrs-full:Revenue", 2024, "Q3"),
        new FinancialPoint("2024-01-01", "2024-12-31", 700, "20-F", "2025-03-01", "ifrs-full:Revenue", 2024, "FY"),
    };

    var quarters = QuarterlySeriesBuilder.BuildQuarterlySeries(points);

    True(!quarters.Any(q => q.End == "2024-12-31"), "annual-minus-9M requires the 9M start to be inside the annual period");
}

void QuarterlySeriesBuilderComputesCumulativeTtmForHalfYearFilers()
{
    // IFRS H1/9M-only filers can never form a 4-consecutive-quarter TTM window because only
    // Q3 (9Mâˆ’H1) and Q4 (Annualâˆ’9M) are reconstructable per year. BuildCumulativeTtmSeries
    // must derive TTM from annual values and the PriorAnnualâˆ’Prior9M+Current9M formula instead.
    var points = new[]
    {
        new FinancialPoint("2023-01-01", "2023-06-30", 200, "6-K",  "2023-08-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2023-01-01", "2023-09-30", 350, "6-K",  "2023-11-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2023-01-01", "2023-12-31", 550, "20-F", "2024-03-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2024-01-01", "2024-06-30", 250, "6-K",  "2024-08-01", "ifrs-full:Revenue", 2024),
        new FinancialPoint("2024-01-01", "2024-09-30", 450, "6-K",  "2024-11-01", "ifrs-full:Revenue", 2024),
        new FinancialPoint("2024-01-01", "2024-12-31", 700, "20-F", "2025-03-01", "ifrs-full:Revenue", 2024),
    };

    var cumTtm = QuarterlySeriesBuilder.BuildCumulativeTtmSeries(points).OrderBy(t => t.End).ToList();

    Equal(3, cumTtm.Count, "three cumulative TTM points: Dec'23, Sep'24, Dec'24");

    True(cumTtm[0].End == "2023-12-31", "first TTM at FY2023 annual end");
    Equal(550.0, cumTtm[0].Ttm, "FY2023 annual value = TTM");
    True(cumTtm[0].Derivation == "cumulative_annual", "derivation for annual-based TTM");
    True(cumTtm[0].Filed == "2024-03-01", "filed = annual filing date");

    True(cumTtm[1].End == "2024-09-30", "second TTM at 9M 2024 end");
    Equal(650.0, cumTtm[1].Ttm, "TTM = 550 - 350 + 450 at Sep'24");
    True(cumTtm[1].Derivation == "cumulative_9m", "derivation for 9M-based TTM");
    True(cumTtm[1].Filed == "2024-11-01", "filed = latest of annual/prev9M/curr9M dates");

    True(cumTtm[2].End == "2024-12-31", "third TTM at FY2024 annual end");
    Equal(700.0, cumTtm[2].Ttm, "FY2024 annual value = TTM");
    True(cumTtm[2].Derivation == "cumulative_annual", "derivation for annual-based TTM");

    // First 9M (Sep'23) has no prior-year 9M in this dataset: no cumulative TTM point.
    True(cumTtm.All(t => t.End != "2023-09-30"), "no cumulative TTM for first 9M (no prior year)");
}

void RevenueSeriesWithholdsTtmForNonConsecutiveIfrsQuarters()
{
    // /revenue/{ticker} regression: an H1/9M/Annual-only IFRS filer (e.g. KLAR) over two years
    // reconstructs only Q3+Q4 of each year. Those four quarters (H2_2023 + H2_2024) are not
    // consecutive, so RevenueSeriesBuilder must publish the quarters but withhold any TTM revenue.
    var points = new[]
    {
        new FinancialPoint("2023-01-01", "2023-06-30", 200, "6-K",  "2023-08-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2023-01-01", "2023-09-30", 350, "6-K",  "2023-11-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2023-01-01", "2023-12-31", 550, "20-F", "2024-03-01", "ifrs-full:Revenue", 2023),
        new FinancialPoint("2024-01-01", "2024-06-30", 250, "6-K",  "2024-08-01", "ifrs-full:Revenue", 2024),
        new FinancialPoint("2024-01-01", "2024-09-30", 450, "6-K",  "2024-11-01", "ifrs-full:Revenue", 2024),
        new FinancialPoint("2024-01-01", "2024-12-31", 700, "20-F", "2025-03-01", "ifrs-full:Revenue", 2024),
    };

    var series = RevenueSeriesBuilder.Build("KLAR", points);

    Equal(150, series.Quarters.Single(q => q.End == "2023-09-30").QuarterlyRevenue, "FY2023 Q3 = 9M - H1 on /revenue");
    Equal(200, series.Quarters.Single(q => q.End == "2023-12-31").QuarterlyRevenue, "FY2023 Q4 = Annual - 9M on /revenue");
    Equal(200, series.Quarters.Single(q => q.End == "2024-09-30").QuarterlyRevenue, "FY2024 Q3 = 9M - H1 on /revenue");
    Equal(250, series.Quarters.Single(q => q.End == "2024-12-31").QuarterlyRevenue, "FY2024 Q4 = Annual - 9M on /revenue");
    // First 9M (Sep'23) has no prior-year 9M to anchor the cumulative formula: still blank.
    True(series.Quarters.Single(q => q.End == "2023-09-30").TtmRevenue == null, "first 9M period has no prior year: TTM blank");
    // Annual and 9M period ends now get cumulative TTM via BuildCumulativeTtmSeries.
    Equal(550.0, series.Quarters.Single(q => q.End == "2023-12-31").TtmRevenue!.Value, "Q4 2023 TTM = FY2023 annual");
    Equal(650.0, series.Quarters.Single(q => q.End == "2024-09-30").TtmRevenue!.Value, "Q3 2024 TTM = 550 - 350 + 450 (cumulative)");
    Equal(700.0, series.Quarters.Single(q => q.End == "2024-12-31").TtmRevenue!.Value, "Q4 2024 TTM = FY2024 annual");
    Equal(3, series.Validation.TtmCount, "/revenue TTM count is 3 for H1/9M-only IFRS series with cumulative TTM");
}

void StatementSeriesUsesRecentSixKFilingTablesWhenCompanyfactsIsStale()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 40755000, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 963000, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
          ] } }
        }
      }
    }
    """;

    const string filingHtml = """
    <html><body>
      <table>
        <tr><th colspan="3">Condensed Consolidated Statements of Profit or Loss - U.S. dollars in thousands</th></tr>
        <tr><th></th><th>Three Months Ended March 31, 2026</th><th>Three Months Ended March 31, 2025</th></tr>
        <tr><td>Revenue</td><td>11,711</td><td>7,133</td></tr>
        <tr><td>Gross profit</td><td>7,229</td><td>4,815</td></tr>
        <tr><td>Operating profit</td><td>809</td><td>333</td></tr>
        <tr><td>Net profit for the period</td><td>593</td><td>408</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument(
        "0001725332",
        "ALAR",
        "6-K",
        "0001213900-26-061770",
        "2026-05-28",
        "2026-03-31",
        "ea029228701ex99-1.htm",
        filingHtml);
    var client = new LegacyClient(new InlineSecCompanyDataSource(json, [filing]));
    var statements = client.FetchStatementSeriesAsync("ALAR").GetAwaiter().GetResult();

    var q1 = statements.Quarterly.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(11_711_000, q1.Revenue!.Value, "ALAR Q1 2026 statement revenue");
    Equal(593_000, q1.NetIncome!.Value, "ALAR Q1 2026 statement net income");
    True(q1.Provenance["revenue"].SourceType == "filing-table", "Q1 revenue provenance is filing table");
    True(q1.Provenance["netIncome"].SourceLabel == "Net profit for the period", "Q1 net income original row label retained");

    var fy = statements.Annual.Single(r => r.PeriodEnd == "2025-12-31");
    Equal(40_755_000, fy.Revenue!.Value, "ALAR FY2025 annual revenue");
    Equal(963_000, fy.NetIncome!.Value, "ALAR FY2025 annual net income");
    True(fy.Provenance["revenue"].SourceType == "companyfacts", "FY2025 companyfacts beats filing fallback");
}

void RevenueEndpointUsesRecentSixKFilingTablesWhenCompanyfactsIsStale()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 40755000, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 963000, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
          ] } }
        }
      }
    }
    """;

    const string filingHtml = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr><th colspan="3">Condensed Consolidated Statements of Profit or Loss</th></tr>
        <tr><th></th><th>Three Months Ended March 31, 2026</th><th>Three Months Ended March 31, 2025</th></tr>
        <tr><td>Revenue</td><td>11,711</td><td>7,133</td></tr>
        <tr><td>Net profit for the period</td><td>593</td><td>408</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", filingHtml);
    var source = new InlineSecCompanyDataSource(json, [filing]);
    var client = new LegacyClient(source);

    var revenuePoints = client.FetchRevenuePointsAsync("ALAR").GetAwaiter().GetResult();
    var series = RevenueSeriesBuilder.Build("ALAR", revenuePoints);

    Equal(1, source.FilingDocumentFetchCount, "stale revenue endpoint attempts filing package fallback");
    Equal(11_711_000, series.Quarters.Single(q => q.End == "2026-03-31").QuarterlyRevenue, "legacy revenue endpoint includes Q1 2026 filing-table revenue");
    Equal(7_133_000, series.Quarters.Single(q => q.End == "2025-03-31").QuarterlyRevenue, "legacy revenue endpoint includes prior-period filing-table revenue");
}

void FundamentalsUsesRecentSixKFilingTablesForNetIncomeWhenCompanyfactsIsStale()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2020-04-01", "end": "2020-06-30", "val": 1077000, "form": "6-K", "filed": "2020-08-31", "fy": 2020, "fp": "Q2", "accn": "0001213900-20-023537" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2020-04-01", "end": "2020-06-30", "val": -2227000, "form": "6-K", "filed": "2020-08-31", "fy": 2020, "fp": "Q2", "accn": "0001213900-20-023537" }
          ] } }
        }
      }
    }
    """;

    const string filingHtml = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr><th colspan="3">Condensed Consolidated Statements of Profit or Loss</th></tr>
        <tr><th></th><th>Three Months Ended March 31, 2026</th><th>Three Months Ended March 31, 2025</th></tr>
        <tr><td>Revenue</td><td>11,711</td><td>7,133</td></tr>
        <tr><td>Net profit for the period</td><td>593</td><td>408</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", filingHtml);
    var source = new InlineSecCompanyDataSource(json, [filing]);
    var client = new LegacyClient(source);

    var fundamentals = client.FetchFundamentalsAsync("ALAR").GetAwaiter().GetResult();

    Equal(1, source.FilingDocumentFetchCount, "stale fundamentals attempts filing package fallback");
    Equal(11_711_000, fundamentals.RevenuePoints.Single(p => p.End == "2026-03-31").Val, "fundamentals includes recent filing-table revenue");
    Equal(593_000, fundamentals.NetIncomeLossPoints.Single(p => p.End == "2026-03-31").Val, "fundamentals includes recent filing-table net income");
    True(fundamentals.NetIncomeLossPoints.Max(p => p.End) == "2026-03-31", "latest fundamentals net income is no longer stale at Q2 2020");
}

void StatementParserReadsPerAdsEpsWithoutTableUnitMultiplier()
{
    const string html = """
    <html><body>
      <p>(in millions of U.S. dollars, rounded, except per share amounts)</p>
      <table>
        <tr>
          <th></th>
          <th>Three Months Ended March 31, 2026</th>
          <th>Year Ended December 31, 2025</th>
        </tr>
        <tr><td>Revenue</td><td>11.7</td><td>40.7</td></tr>
        <tr><td>Basic earnings per American Depository Share ("ADS") (in U.S. dollars)</td><td>0.08</td><td>0.14</td></tr>
        <tr><td>Non-IFRS basic earnings per ADS (in U.S. dollars)</td><td>0.24</td><td>0.65</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(11_700_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2026-03-31").Val, "revenue keeps table unit multiplier");
    Equal(0.08, parsed.Single(p => p.Concept == "ifrs-full:BasicEarningsLossPerShare" && p.End == "2026-03-31").Val, "Q1 per-ADS EPS is not multiplied by millions");
    Equal(0.14, parsed.Single(p => p.Concept == "ifrs-full:BasicEarningsLossPerShare" && p.End == "2025-12-31").Val, "FY per-ADS EPS is not multiplied by millions");
    True(!parsed.Any(p => p.SourceLabel == "Non-IFRS basic earnings per ADS (in U.S. dollars)"), "non-IFRS EPS row is rejected");
}

void StatementParserReadsKlarnaHiddenStatementText()
{
    const string html = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Interim condensed consolidated statement of profit or loss for the Three Months Ended March&nbsp;31, 2026 (Unaudited)
        Three Months Ended USD millions, except per share amounts Note March 31, 2026 March 31, 2025
        Transaction and service revenue ................................................................. 671 519
        Gain on sale of consumer receivables ........................................................ 57 -
        Interest income ................................................................................................. 284 182
        Total revenue 3 1,012 701
        Processing and servicing costs ..................................................................... (266) (164)
        Operating profit (loss) ....................................................................................... 17 (90)
        Net profit (loss) 1 (99)
      </font></div>
    </body></html>
    """;

    var filing = new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-26-034877", "2026-05-14", "2026-03-31", "exhibitno994klarnagroupplc.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(1_012_000_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2026-03-31").Val, "KLAR Q1 2026 hidden statement revenue");
    Equal(701_000_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2025-03-31").Val, "KLAR Q1 2025 hidden statement revenue");
    Equal(17_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLossFromOperatingActivities" && p.End == "2026-03-31").Val, "KLAR operating profit parsed");
    Equal(1_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2026-03-31").Val, "KLAR net profit parsed");
    True(parsed.Where(p => p.End == "2026-03-31").All(p => p.SourceType == "filing-text"), "hidden statement facts are marked as filing-text");
    True(parsed.All(p => p.SourceLabel is not "Transaction and service revenue"), "component revenue rows are not treated as total revenue");
}

void StatementParserScalesHiddenTextMillionsOfUsd()
{
    const string html = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Consolidated statements of income
        Three Months Ended millions of USD March 31, 2026 March 31, 2025
        Total revenue 11.7 7.1
        Net income 0.6 0.4
      </font></div>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "statement-text.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(11_700_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2026-03-31").Val, "hidden text millions of USD revenue is scaled");
    Equal(600_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2026-03-31").Val, "hidden text millions of USD net income is scaled");
}

void StatementParserRejectsSingleRowHiddenTextBlock()
{
    const string html = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Consolidated statements of income
        Three Months Ended millions of USD March 31, 2026 March 31, 2025
        Total revenue 11.7 7.1
      </font></div>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "statement-text.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    True(!parsed.Any(p => p.SourceType == "filing-text"), "single-row hidden text block is rejected as low confidence");
}

void StructuredStatementTableBeatsHiddenStatementTextForSamePeriod()
{
    const string html = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr><th colspan="2">Condensed Consolidated Statements of Income</th></tr>
        <tr><th></th><th>Three Months Ended March 31, 2026</th></tr>
        <tr><td>Total revenue</td><td>12,345</td></tr>
      </table>
      <div><font size="1" style="font-size:1pt;color:white">
        Consolidated statements of income
        Three Months Ended USD millions March 31, 2026
        Total revenue 11.7
        Net income 0.6
      </font></div>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "mixed.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing).Where(p => p.Concept == "ifrs-full:Revenue").ToList();
    True(parsed.Any(p => p.SourceType == "filing-table"), "structured table revenue parsed");
    True(parsed.Any(p => p.SourceType == "filing-text"), "hidden text revenue parsed");

    var quarter = QuarterlySeriesBuilder.BuildQuarterlySeries(parsed).Single(q => q.End == "2026-03-31");
    Equal(12_345_000, quarter.Val, "structured table beats hidden text for same period");
    True(quarter.SourceType == "filing-table", "selected quarter keeps structured table source type");
}

void StatementParserReadsKlarnaThreeAndNineMonthHiddenStatementText()
{
    const string html = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Interim Consolidated Statements of Profit or Loss for the Three and Nine Months Ended September 30, 2025 and 2024 (Unaudited)
        Three Months Ended Nine Months Ended Amounts in USD millions, except share and per share amounts Note
        September 30, 2025 September 30, 2024 September 30, 2025 September 30, 2024
        Transaction and service revenue ................................ 634 532 1,757 1,535
        Interest income ........................................................ 269 174 670 494
        Total revenue .......................................................... 3 903 706 2,427 2,029
        Operating Profit (loss) .............................................. (83) 13 (218) (20)
        Net Profit (loss) ...................................................... (95) 12 (246) (19)
      </font></div>
    </body></html>
    """;

    var filing = new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-25-052803", "2025-11-18", "2025-09-30", "exhibitno991klarnagroup.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(903_000_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.Start == "2025-07-01" && p.End == "2025-09-30").Val, "KLAR Q3 2025 hidden statement revenue");
    Equal(2_427_000_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.Start == "2025-01-01" && p.End == "2025-09-30").Val, "KLAR 9M 2025 hidden statement revenue");
    Equal(-83_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLossFromOperatingActivities" && p.Start == "2025-07-01" && p.End == "2025-09-30").Val, "KLAR Q3 2025 operating loss parsed");
}

void RevenueEndpointUsesKlarnaHiddenStatementTextWhenCompanyfactsIsAnnualOnly()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 3509000000, "form": "20-F", "filed": "2026-02-26", "fy": 2025, "fp": "FY", "accn": "0002003292-26-000007" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 21000000, "form": "20-F", "filed": "2026-02-26", "fy": 2025, "fp": "FY", "accn": "0002003292-26-000007" }
          ] } }
        }
      }
    }
    """;

    const string filingHtml = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Interim condensed consolidated statement of profit or loss for the Three Months Ended March 31, 2026 (Unaudited)
        Three Months Ended USD millions, except per share amounts Note March 31, 2026 March 31, 2025
        Transaction and service revenue 671 519
        Interest income 284 182
        Total revenue 3 1,012 701
        Operating profit (loss) 17 (90)
        Net profit (loss) 1 (99)
      </font></div>
    </body></html>
    """;

    var filing = new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-26-034877", "2026-05-14", "2026-03-31", "exhibitno994klarnagroupplc.htm", filingHtml);
    var source = new InlineSecCompanyDataSource(json, [filing]);
    var client = new LegacyClient(source);

    var revenuePoints = client.FetchRevenuePointsAsync("KLAR").GetAwaiter().GetResult();
    var series = RevenueSeriesBuilder.Build("KLAR", revenuePoints);

    Equal(1, source.FilingDocumentFetchCount, "KLAR annual-only companyfacts attempts filing package fallback");
    Equal(1_012_000_000, series.Quarters.Single(q => q.End == "2026-03-31").QuarterlyRevenue, "KLAR Q1 2026 revenue reaches legacy revenue endpoint");
    Equal(701_000_000, series.Quarters.Single(q => q.End == "2025-03-31").QuarterlyRevenue, "KLAR Q1 2025 comparison revenue reaches legacy revenue endpoint");
}

void RevenueEndpointReconstructsKlarnaQ1ThroughQ4FromRecentFilingText()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 3509000000, "form": "20-F", "filed": "2026-02-26", "fy": 2025, "fp": "FY", "accn": "0002003292-26-000007" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": -273000000, "form": "20-F", "filed": "2026-02-26", "fy": 2025, "fp": "FY", "accn": "0002003292-26-000007" }
          ] } }
        }
      }
    }
    """;

    const string q1Html = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Interim condensed consolidated statement of profit or loss for the Three Months Ended March 31, 2026 (Unaudited)
        Three Months Ended USD millions, except per share amounts Note March 31, 2026 March 31, 2025
        Transaction and service revenue 671 519
        Interest income 284 182
        Total revenue 3 1,012 701
        Operating profit (loss) 17 (90)
        Net profit (loss) 1 (99)
      </font></div>
    </body></html>
    """;

    const string q3Html = """
    <html><body>
      <div><font size="1" style="font-size:1pt;color:white">
        Interim Consolidated Statements of Profit or Loss for the Three and Nine Months Ended September 30, 2025 and 2024 (Unaudited)
        Three Months Ended Nine Months Ended Amounts in USD millions, except share and per share amounts Note
        September 30, 2025 September 30, 2024 September 30, 2025 September 30, 2024
        Transaction and service revenue 634 532 1,757 1,535
        Interest income 269 174 670 494
        Total revenue 3 903 706 2,427 2,029
        Operating Profit (loss) (83) 13 (218) (20)
        Net Profit (loss) (95) 12 (246) (19)
      </font></div>
    </body></html>
    """;

    var filings = new[]
    {
        new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-26-038366", "2026-05-27", "2026-05-27", "event.htm", "<html></html>"),
        new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-26-034877", "2026-05-14", "2026-03-31", "q1.htm", q1Html),
        new SecFilingDocument("0002003292", "KLAR", "6-K", "0002003292-26-000004", "2026-02-20", "2026-02-20", "event2.htm", "<html></html>"),
        new SecFilingDocument("0002003292", "KLAR", "6-K", "0002003292-26-000002", "2026-02-19", "2026-02-19", "event3.htm", "<html></html>"),
        new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-25-052803", "2025-11-18", "2025-09-30", "q3.htm", q3Html)
    };
    var source = new InlineSecCompanyDataSource(json, filings);
    var client = new LegacyClient(source);

    var revenuePoints = client.FetchRevenuePointsAsync("KLAR").GetAwaiter().GetResult();
    var series = RevenueSeriesBuilder.Build("KLAR", revenuePoints);

    Equal(1, source.FilingDocumentFetchCount, "KLAR fallback fetches recent filing packages once");
    Equal(701_000_000, series.Quarters.Single(q => q.End == "2025-03-31").QuarterlyRevenue, "KLAR Q1 2025 revenue");
    Equal(823_000_000, series.Quarters.Single(q => q.End == "2025-06-30").QuarterlyRevenue, "KLAR Q2 2025 derived from 9M minus Q1 and Q3");
    Equal(903_000_000, series.Quarters.Single(q => q.End == "2025-09-30").QuarterlyRevenue, "KLAR Q3 2025 revenue");
    Equal(1_082_000_000, series.Quarters.Single(q => q.End == "2025-12-31").QuarterlyRevenue, "KLAR Q4 2025 derived from annual minus 9M");
}

void EdgarClientUsesRecentSixKFilingTablesForPerAdsEpsWhenCompanyfactsLacksQuarter()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "DilutedEarningsLossPerShare": {
            "units": {
              "USD/shares": [
                { "start": "2025-01-01", "end": "2025-12-31", "val": 0.01, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
              ]
            }
          }
        }
      }
    }
    """;

    const string filingHtml = """
    <html><body>
      <p>(in millions of U.S. dollars, rounded, except per share amounts)</p>
      <table>
        <tr>
          <th></th>
          <th>Three Months Ended March 31, 2026</th>
          <th>Year Ended December 31, 2025</th>
        </tr>
        <tr><td>Basic earnings per American Depository Share ("ADS") (in U.S. dollars)</td><td>0.08</td><td>0.14</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument(
        "0001725332",
        "ALAR",
        "6-K",
        "0001213900-26-061770",
        "2026-05-28",
        "2026-03-31",
        "ea029228701ex99-1.htm",
        filingHtml);
    var source = new InlineSecCompanyDataSource(json, [filing]);
    var client = new LegacyClient(source);

    var fundamentals = client.FetchFundamentalsAsync("ALAR").GetAwaiter().GetResult();
    var q1 = fundamentals.Eps.Single(p => p.SourceType == "filing-table" && p.End == "2026-03-31");
    var fy = fundamentals.Eps.Single(p => p.SourceType == "filing-table" && p.End == "2025-12-31");

    Equal(1, source.FilingDocumentFetchCount, "missing current EPS period attempts filing package fallback");
    Equal(0.08, q1.Val, "Q1 2026 per-ADS EPS from SEC filing table");
    Equal(0.14, fy.Val, "FY2025 per-ADS EPS from SEC filing table");
    True(q1.SourceLabel?.Contains("ADS", StringComparison.OrdinalIgnoreCase) == true, "per-ADS source label retained");
    True(fundamentals.Eps.Any(p => p.SourceType == "companyfacts" && p.End == "2025-12-31" && Math.Abs(p.Val - 0.01) < 0.000001),
        "original companyfacts ordinary-share EPS remains auditable");
}

void EdgarClientReadsPerAdsEpsWhenAnnualCompanyfactsAlreadyHasOrdinaryShareEps()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "DilutedEarningsLossPerShare": {
            "units": {
              "USD/shares": [
                { "start": "2025-01-01", "end": "2025-12-31", "val": 0.01, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
              ]
            }
          }
        }
      }
    }
    """;

    const string filingHtml = """
    <html><body>
      <p>(in millions of U.S. dollars, rounded, except per share amounts)</p>
      <table>
        <tr>
          <th></th>
          <th>Year Ended December 31, 2025</th>
        </tr>
        <tr><td>Diluted earnings per American Depositary Share ("ADS") (in U.S. dollars)</td><td>0.14</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument(
        "0001725332",
        "ALAR",
        "6-K",
        "0001213900-26-031555",
        "2026-03-19",
        "2025-12-31",
        "ea029228701ex99-1.htm",
        filingHtml);
    var source = new InlineSecCompanyDataSource(json, [filing]);
    var client = new LegacyClient(source);

    var fundamentals = client.FetchFundamentalsAsync("ALAR").GetAwaiter().GetResult();
    var fy = fundamentals.Eps.Single(p => p.SourceType == "filing-table" && p.End == "2025-12-31");

    Equal(1, source.FilingDocumentFetchCount, "ordinary-share annual EPS does not block per-ADS filing fallback");
    Equal(0.14, fy.Val, "FY2025 per-ADS EPS from SEC filing table");
    True(fundamentals.Eps.Any(p => p.SourceType == "companyfacts" && p.End == "2025-12-31" && Math.Abs(p.Val - 0.01) < 0.000001),
        "ordinary-share companyfacts EPS remains auditable");
}

void FinancialMetricsBuilderUsesMixedTaxonomiesPerRow()
{
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "ResearchAndDevelopmentExpense": { "units": { "USD": [
            { "start": "2024-01-01", "end": "2024-03-31", "val": 50, "form": "10-Q", "filed": "2024-05-01", "fy": 2024, "fp": "Q1", "accn": "0000000001-24-000001" }
          ] } }
        },
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2024-01-01", "end": "2024-03-31", "val": 1000, "form": "6-K", "filed": "2024-05-01", "fy": 2024, "fp": "Q1", "accn": "0000000001-24-000001" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2024-01-01", "end": "2024-03-31", "val": 100, "form": "6-K", "filed": "2024-05-01", "fy": 2024, "fp": "Q1", "accn": "0000000001-24-000001" }
          ] } }
        }
      }
    }
    """;

    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var result = FinancialMetricsBuilder.Build("MIX", client.FetchFinancialMetricInputsAsync("MIX").GetAwaiter().GetResult());
    var row = result.Rows.Single(r => r.PeriodEnd == "2024-03-31");

    Equal(1000, row.Quarterly["revenue"].Value!.Value, "IFRS revenue is used despite sparse US-GAAP notes");
    Equal(50, row.Quarterly["rnd"].Value!.Value, "US-GAAP note metric can coexist in same row");
    Equal(100, row.Quarterly["netIncome"].Value!.Value, "IFRS net income is used in same row");

    var statements = FinancialStatementSeriesBuilder.Build("MIX", result);
    var statementRow = statements.Quarterly.Single(r => r.PeriodEnd == "2024-03-31");
    True(statementRow.Provenance["revenue"].Taxonomy == FinancialMetricProviderNames.IfrsFull, "statement revenue provenance keeps IFRS taxonomy");
    True(statementRow.Provenance["netIncome"].Taxonomy == FinancialMetricProviderNames.IfrsFull, "statement net income provenance keeps IFRS taxonomy");
}

void FinancialMetricsBuilderPrefersHigherPrecisionSameAccessionFact()
{
    var revenueSpec = FinancialMetricsCatalog.Ifrs("Revenue");
    var revenue = new[]
    {
        new FinancialPoint("2026-01-01", "2026-03-31", 11_711_000, "6-K", "2026-05-28", "ifrs-full:Revenue", Accn: "A1", SourceType: "filing-text", SourcePrecision: 3),
        new FinancialPoint("2026-01-01", "2026-03-31", 11_700_000, "6-K", "2026-05-29", "ifrs-full:Revenue", Accn: "A1", SourceType: "filing-text", SourcePrecision: 1),
    };
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>> { [revenueSpec.Key] = revenue },
        revenue,
        CompanyFactsProfile.Empty);

    var result = FinancialMetricsBuilder.Build("PREC", inputs);
    var row = result.Rows.Single(r => r.PeriodEnd == "2026-03-31");

    Equal(11_711_000, row.Quarterly["revenue"].Value!.Value, "financial metrics prefer higher precision same-accession fact");
}

void EdgarClientTreatsFilingFallbackAsOptionalWhenPackagesFail()
{
    const string json = """
    {
      "facts": {
        "ifrs-full": {
          "Revenue": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 40755000, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
          ] } },
          "ProfitLoss": { "units": { "USD": [
            { "start": "2025-01-01", "end": "2025-12-31", "val": 963000, "form": "20-F", "filed": "2026-03-19", "fy": 2025, "fp": "FY", "accn": "0001213900-26-031556" }
          ] } }
        }
      }
    }
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", "<html></html>");
    var source = new InlineSecCompanyDataSource(json, [filing], throwOnFilingDocuments: true);
    var client = new LegacyClient(source);

    var statements = client.FetchStatementSeriesAsync("ALAR").GetAwaiter().GetResult();

    Equal(1, source.FilingDocumentFetchCount, "missing recent statement period attempts package fallback once");
    True(!statements.Quarterly.Any(r => r.PeriodEnd == "2026-03-31"), "failed optional filing package does not invent Q1 row");
    Equal(40_755_000, statements.Annual.Single(r => r.PeriodEnd == "2025-12-31").Revenue!.Value, "annual companyfacts still returned");
}

void StatementParserRejectsAmbiguousTables()
{
    var filing = new SecFilingDocument(
        "0001725332",
        "ALAR",
        "6-K",
        "0001213900-26-061770",
        "2026-05-28",
        "2026-03-31",
        "ambiguous.htm",
        "<table><tr><th></th><th>2026</th></tr><tr><td>Revenue</td><td>11,711</td></tr></table>");

    var parsed = SecFilingStatementParser.Parse(filing);
    True(parsed.Count == 0, "ambiguous table without explicit period header is rejected");
}

void StatementParserPreservesPeriodSlotsWhenTableCellsAreBlank()
{
    const string html = """
    <html><body>
      <table>
        <tr><th colspan="3">Statement of Profit or Loss - U.S. dollars in thousands</th></tr>
        <tr><th></th><th>Three Months Ended March 31, 2026</th><th>Three Months Ended March 31, 2025</th></tr>
        <tr><td>Revenue</td><td>-</td><td>7,133</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    True(!parsed.Any(p => p.Concept == "ifrs-full:Revenue" && p.End == "2026-03-31"), "blank current-period cell is not filled by later period value");
    Equal(7_133_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2025-03-31").Val, "later period value keeps its original slot");
}

void StatementParserPrefersPreciseTableOverRoundedSummary()
{
    const string html = """
    <html><body>
      <p>(in millions of U.S. dollars, rounded)</p>
      <table>
        <tr><th></th><th>Three Months Ended March 31, 2026</th></tr>
        <tr><td>Revenue</td><td>11.7</td></tr>
      </table>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr><th></th><th>Three Months Ended March 31, 2026</th></tr>
        <tr><td>Revenue</td><td>11,711</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(11_711_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2026-03-31").Val, "precise table beats rounded summary for same period");
}

void StatementParserUsesHeaderGroupsForMixedQuarterAnnualTables()
{
    const string html = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr>
          <th></th>
          <th colspan="2">For the Three Months Ended December 31,</th>
          <th colspan="2">For the Year Ended December 31,</th>
        </tr>
        <tr>
          <th></th><th>2025</th><th>2024</th><th>2025</th><th>2024</th>
        </tr>
        <tr>
          <td>Revenue</td><td>400</td><td>300</td><td>1,600</td><td>1,200</td>
        </tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2025-12-31", "ea029228701ex99-1.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing).Where(p => p.Concept == "ifrs-full:Revenue").ToList();

    Equal(400_000, parsed.Single(p => p.Start == "2025-10-01" && p.End == "2025-12-31").Val, "Q4 current period stays quarterly");
    Equal(300_000, parsed.Single(p => p.Start == "2024-10-01" && p.End == "2024-12-31").Val, "Q4 prior period stays quarterly");
    Equal(1_600_000, parsed.Single(p => p.Start == "2025-01-01" && p.End == "2025-12-31").Val, "FY current period stays annual");
    Equal(1_200_000, parsed.Single(p => p.Start == "2024-01-01" && p.End == "2024-12-31").Val, "FY prior period stays annual");
}

void StatementParserKeepsCumulativeAndQuarterHeaderGroupsAligned()
{
    const string html = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr>
          <th></th>
          <th colspan="2">For the Nine Months Ended September 30,</th>
          <th colspan="2">For the Three Months Ended September 30,</th>
          <th colspan="1">For the Year Ended December 31,</th>
        </tr>
        <tr>
          <th></th><th>2025</th><th>2024</th><th>2025</th><th>2024</th><th>2024</th>
        </tr>
        <tr>
          <td>Revenue</td><td>3</td><td>28,935</td><td>24,500</td><td>13,011</td><td>7,200</td><td>31,824</td>
        </tr>
        <tr>
          <td>Net profit for the period</td><td>748</td><td>5,300</td><td>53</td><td>100</td><td>5,780</td>
        </tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-25-115061", "2025-11-17", "2025-09-30", "ea026738001ex99-1_alarum.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);
    var parsedRevenue = string.Join("; ", parsed
        .Where(p => p.Concept == "ifrs-full:Revenue")
        .Select(p => $"{p.Start}->{p.End}:{p.Val}"));
    var nineMonthRevenue = parsed.SingleOrDefault(p => p.Concept == "ifrs-full:Revenue" && p.Start == "2025-01-01" && p.End == "2025-09-30");
    var q3Revenue = parsed.SingleOrDefault(p => p.Concept == "ifrs-full:Revenue" && p.Start == "2025-07-01" && p.End == "2025-09-30");

    True(nineMonthRevenue is not null, $"9M revenue period parsed from nine-month column; got {parsedRevenue}");
    Equal(28_935_000, nineMonthRevenue!.Val, "9M revenue remains cumulative");
    True(q3Revenue is not null, $"Q3 revenue period parsed from three-month column; got {parsedRevenue}");
    Equal(13_011_000, q3Revenue!.Val, "Q3 revenue uses three-month column");
    Equal(748_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.Start == "2025-01-01" && p.End == "2025-09-30").Val, "9M net income remains cumulative");
    Equal(53_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.Start == "2025-07-01" && p.End == "2025-09-30").Val, "Q3 net income uses three-month column");
}

void StatementParserReadsIxbrlInstantContextsAndAttributeOrder()
{
    const string html = """
    <html><body>
      <xbrli:context id="dur-q1">
        <xbrli:period>
          <xbrli:startDate>2026-01-01</xbrli:startDate>
          <xbrli:endDate>2026-03-31</xbrli:endDate>
        </xbrli:period>
      </xbrli:context>
      <xbrli:context id="instant-q1">
        <xbrli:period><xbrli:instant>2026-03-31</xbrli:instant></xbrli:period>
      </xbrli:context>
      <ix:nonFraction contextRef="instant-q1" scale="3" name="ifrs-full:Assets">12,345</ix:nonFraction>
      <ix:nonFraction contextRef="dur-q1" sign="-" scale="3" name="ifrs-full:ProfitLoss">593</ix:nonFraction>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001725332", "ALAR", "6-K", "0001213900-26-061770", "2026-05-28", "2026-03-31", "ea029228701ex99-1.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    var assets = parsed.Single(p => p.Concept == "ifrs-full:Assets");
    Equal(12_345_000, assets.Val, "iXBRL instant balance-sheet fact parsed");
    True(assets.Start is null, "instant context has no duration start");
    Equal(-593_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss").Val, "iXBRL sign attribute is applied");
}

void FinancialMetricsBuilderUsesFallbacksAndMetadata()
{
    var client = new LegacyClient(new FixtureSecCompanyDataSource(FixtureRoot()));
    var inputs = client.FetchFinancialMetricInputsAsync("FIX").GetAwaiter().GetResult();
    var result = FinancialMetricsBuilder.Build("FIX", inputs);

    var q1 = result.Rows.Single(r => r.PeriodEnd == "2023-03-31");
    Equal(250, q1.Quarterly["sga"].Value!.Value, "SG&A from operating expenses minus R&D");
    True(q1.Quarterly["sga"].Formula == FinancialMetricFormulaIds.OperatingExpensesMinusRnd, "SG&A formula id");
    Equal(700, q1.Quarterly["totalOperatingCost"].Value!.Value, "total operating cost from revenue minus EBIT");
    Equal(330, q1.Quarterly["ebitda"].Value!.Value, "EBITDA from EBIT plus D&A");
    Equal(245, q1.Quarterly["ebt"].Value!.Value, "EBT from net income plus tax");
    True(q1.Quarterly["netIncome"].SourceConcepts == "ProfitLoss", "net income prefers consolidated concept");
    Equal(400, q1.Quarterly["freeCashFlow"].Value!.Value, "FCF from OCF minus CapEx");
    Equal(1000, q1.Point["cashAndShortTermEquivalents"].Value!.Value, "cash plus short-term investments");
    Equal(1400, q1.Point["totalDebt"].Value!.Value, "current plus long-term debt");

    var q3 = result.Rows.Single(r => r.PeriodEnd == "2023-09-30");
    Equal(1200, q3.Quarterly["cogs"].Value!.Value, "COGS fallback from revenue minus gross profit");
    True(q3.Quarterly["cogs"].Formula == FinancialMetricFormulaIds.RevenueMinusGrossProfit, "COGS fallback formula id");

    var q4 = result.Rows.Single(r => r.PeriodEnd == "2023-12-31");
    True(!q4.Quarterly["cogs"].Value.HasValue, "negative derived COGS stays blank");
    True(q4.Quarterly["cogs"].Formula == FinancialMetricFormulaIds.BlankMissingInputs, "blank COGS formula id");
    True(result.AuditRows.Any(a =>
        a.PeriodEnd == "2023-12-31" &&
        a.MetricId == "cogs" &&
        a.ValueType == "quarterly" &&
        !a.Value.HasValue &&
        a.Formula == FinancialMetricFormulaIds.BlankMissingInputs &&
        a.Derivation == "blank"), "audit includes blank COGS trace row");
}

void FinancialMetricsBuilderAggregatesMultiClassSharePointMetrics()
{
    var outstandingSpec = FinancialMetricsCatalog.DirectConceptsFor(FinancialMetricsCatalog.SharesOutstanding, FinancialMetricProvider.UsGaap).First();
    var issuedSpec = FinancialMetricsCatalog.DirectConceptsFor(FinancialMetricsCatalog.ShareIssued, FinancialMetricProvider.UsGaap).First();
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [outstandingSpec.Key] =
            [
                new FinancialPoint(null, "2023-12-31", 5_800, "10-K", "2024-02-01", outstandingSpec.SourceName, 2023, "FY", Accn: "0001"),
                new FinancialPoint(null, "2023-12-31", 900, "10-K", "2024-02-01", outstandingSpec.SourceName, 2023, "FY", Accn: "0001"),
                new FinancialPoint(null, "2023-12-31", 5_500, "10-K", "2024-02-01", outstandingSpec.SourceName, 2023, "FY", Accn: "0001")
            ],
            [issuedSpec.Key] =
            [
                new FinancialPoint(null, "2023-12-31", 7_000, "10-K", "2024-02-01", issuedSpec.SourceName, 2023, "FY", Accn: "0001"),
                new FinancialPoint(null, "2023-12-31", 8_000, "10-K", "2024-02-01", issuedSpec.SourceName, 2023, "FY", Accn: "0001")
            ]
        },
        [Q("2023-12-31", 1_000, "2023-10-01", "2024-02-01")],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("MULTICLASS", inputs).Rows.Single(r => r.PeriodEnd == "2023-12-31");

    Equal(12_200, row.Point[FinancialMetricsCatalog.SharesOutstanding.Id].Value!.Value, "export shares outstanding sums same-filing share classes");
    Equal(12_200, row.Point[FinancialMetricsCatalog.OrdinarySharesNumber.Id].Value!.Value, "ordinary shares number uses same multi-class aggregation");
    Equal(15_000, row.Point[FinancialMetricsCatalog.ShareIssued.Id].Value!.Value, "issued shares sums same-filing share classes");
    Equal(2_800, row.Point[FinancialMetricsCatalog.TreasuryShares.Id].Value!.Value, "treasury shares derives from aggregated issued and outstanding shares");
}

void FinancialMetricsBuilderKeepsCompanyfactsAnnualRevenueOverFilingFallback()
{
    var revenue = new[]
    {
        new FinancialPoint("2024-01-01", "2024-12-31", 1_000, "10-K", "2025-02-01", "Revenues", 2024, "FY", Accn: "COMPANYFACTS", SourceType: "companyfacts"),
        new FinancialPoint("2024-01-01", "2024-12-31", 900, "10-K", "2025-03-01", "Revenues", 2024, "FY", Accn: "FILINGTABLE", SourceType: "filing-table")
    };
    var inputs = new FinancialMetricInputs(new Dictionary<string, IReadOnlyList<FinancialPoint>>(), revenue, CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("SOURCE", inputs).Rows.Single(r => r.PeriodEnd == "2024-12-31");

    Equal(1_000, row.Annual[FinancialMetricsCatalog.Revenue.Id].Value!.Value, "annual revenue keeps higher-rank companyfacts source");
    True(row.Annual[FinancialMetricsCatalog.Revenue.Id].SourceType == "companyfacts", "annual revenue source type remains companyfacts");
}

void FinancialMetricsBuilderSkipsTtmWhenFlowQuartersHaveGaps()
{
    var revenue = new[]
    {
        Q("2023-03-31", 100, "2023-01-01", "2023-05-01"),
        Q("2023-06-30", 100, "2023-04-01", "2023-08-01"),
        Q("2023-09-30", 100, "2023-07-01", "2023-11-01"),
        Q("2023-12-31", 100, "2023-10-01", "2024-02-15"),
        Q("2024-03-31", 100, "2024-01-01", "2024-05-01"),
    };
    var investingSpec = FinancialMetricsCatalog.UsGaap("NetCashProvidedByUsedInInvestingActivities");
    var investingCashFlow = new[]
    {
        Q("2023-03-31", -10, "2023-01-01", "2023-05-01", investingSpec.Concept),
        Q("2023-06-30", -20, "2023-04-01", "2023-08-01", investingSpec.Concept),
        Q("2023-12-31", -40, "2023-10-01", "2024-02-15", investingSpec.Concept),
        Q("2024-03-31", -50, "2024-01-01", "2024-05-01", investingSpec.Concept),
    };
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [investingSpec.Key] = investingCashFlow
        },
        revenue,
        CompanyFactsProfile.Empty);

    var result = FinancialMetricsBuilder.Build("GAP", inputs);
    var q1Next = result.Rows.Single(r => r.PeriodEnd == "2024-03-31");

    Equal(-50, q1Next.Quarterly[FinancialMetricsCatalog.InvestingCashFlow.Id].Value!.Value, "sparse quarterly cash flow remains visible");
    True(!q1Next.Ttm[FinancialMetricsCatalog.InvestingCashFlow.Id].Value.HasValue, "TTM blanks when a flow metric skips a quarter");
    True(q1Next.Ttm[FinancialMetricsCatalog.InvestingCashFlow.Id].Formula == FinancialMetricFormulaIds.BlankMissingInputs, "blank sparse TTM uses blank formula id");
}

void FinancialMetricsBuilderDoesNotCreateShareOnlyRows()
{
    var shareSpec = new SecConceptSpec("EntityCommonStockSharesOutstanding", "shares", "dei");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [shareSpec.Key] =
            [
                new FinancialPoint(null, "2026-04-22", 10_757, "10-Q", "2026-04-30", shareSpec.SourceName, 2026, "Q2")
            ]
        },
        [Q("2026-03-31", 100, "2026-01-01", "2026-04-30")],
        CompanyFactsProfile.Empty);

    var result = FinancialMetricsBuilder.Build("SHARES", inputs);

    True(result.Rows.Any(r => r.PeriodEnd == "2026-03-31"), "real statement period remains");
    True(!result.Rows.Any(r => r.PeriodEnd == "2026-04-22"), "share-only cover date does not become a financial row");
}

void FinancialMetricsBuilderUsesPresentationSignsAndDirectTtmFacts()
{
    var inventorySpec = FinancialMetricsCatalog.UsGaap("IncreaseDecreaseInInventories");
    var purchaseSpec = FinancialMetricsCatalog.UsGaap("PaymentsToAcquireBusinessesNetOfCashAcquired");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [inventorySpec.Key] =
            [
                Q("2025-06-30", 10, "2025-04-01", "2025-07-30", inventorySpec.SourceName),
                Q("2025-09-30", 20, "2025-07-01", "2025-10-30", inventorySpec.SourceName),
                Q("2025-12-31", -5, "2025-10-01", "2026-02-01", inventorySpec.SourceName),
                Q("2026-03-31", 15, "2026-01-01", "2026-04-30", inventorySpec.SourceName)
            ],
            [purchaseSpec.Key] =
            [
                new FinancialPoint("2025-04-01", "2026-03-31", 19_297, "10-Q", "2026-04-30", purchaseSpec.SourceName, 2026, "Q1")
            ]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("TTM", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");

    Equal(-15, row.Quarterly[FinancialMetricsCatalog.ChangeInInventory.Id].Value!.Value, "inventory increase is presented as cash outflow");
    Equal(-40, row.Ttm[FinancialMetricsCatalog.ChangeInInventory.Id].Value!.Value, "TTM inventory presentation sign sums transformed quarters");
    Equal(19_297, row.Ttm[FinancialMetricsCatalog.PurchaseOfBusiness.Id].Value!.Value, "exact twelve-month business acquisition fact supplements TTM");
    True(row.Ttm[FinancialMetricsCatalog.PurchaseOfBusiness.Id].Formula == FinancialMetricFormulaIds.DirectTtmFact, "direct TTM fact uses traceable formula id");
}

void FinancialMetricsBuilderIncludesSecBackedLeaseDebtAndPpeComponents()
{
    var netPpeSpec = FinancialMetricsCatalog.UsGaap("PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetAfterAccumulatedDepreciationAndAmortization");
    var operatingRouSpec = FinancialMetricsCatalog.UsGaap("OperatingLeaseRightOfUseAsset");
    var currentDebtSpec = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var longTermDebtSpec = FinancialMetricsCatalog.UsGaap("LongTermDebt");
    var financeLeaseSpec = FinancialMetricsCatalog.UsGaap("FinanceLeaseLiability");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [netPpeSpec.Key] = [new FinancialPoint(null, "2026-03-31", 397_458, "10-Q", "2026-04-30", netPpeSpec.SourceName, 2026, "Q1")],
            [operatingRouSpec.Key] = [new FinancialPoint(null, "2026-03-31", 88_741, "10-Q", "2026-04-30", operatingRouSpec.SourceName, 2026, "Q1")],
            [currentDebtSpec.Key] = [new FinancialPoint(null, "2026-03-31", 152, "10-Q", "2026-04-30", currentDebtSpec.SourceName, 2026, "Q1")],
            [longTermDebtSpec.Key] = [new FinancialPoint(null, "2026-03-31", 122_632, "10-Q", "2026-04-30", longTermDebtSpec.SourceName, 2026, "Q1")],
            [financeLeaseSpec.Key] = [new FinancialPoint(null, "2026-03-31", 13_325, "10-Q", "2026-04-30", financeLeaseSpec.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("LEASES", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");

    Equal(486_199, row.Point[FinancialMetricsCatalog.NetPpe.Id].Value!.Value, "net PPE includes operating lease ROU asset");
    Equal(136_109, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value, "total debt includes finance leases and excludes operating leases");
}

void FinancialMetricsBuilderIncludesFinanceLeaseInterestInDebtRepayment()
{
    var longTermSpec = FinancialMetricsCatalog.UsGaap("RepaymentsOfLongTermDebt");
    var shortTermSpec = FinancialMetricsCatalog.UsGaap("RepaymentsOfShortTermDebt");
    var principalSpec = FinancialMetricsCatalog.UsGaap("FinanceLeasePrincipalPayments");
    var interestSpec = FinancialMetricsCatalog.UsGaap("FinanceLeaseInterestPaymentOnLiability");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [longTermSpec.Key] = [Q("2026-03-31", 100, "2026-01-01", "2026-04-30", longTermSpec.SourceName)],
            [shortTermSpec.Key] = [Q("2026-03-31", 25, "2026-01-01", "2026-04-30", shortTermSpec.SourceName)],
            [principalSpec.Key] = [Q("2026-03-31", 10, "2026-01-01", "2026-04-30", principalSpec.SourceName)],
            [interestSpec.Key] = [Q("2026-03-31", 3, "2026-01-01", "2026-04-30", interestSpec.SourceName)]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("REPAY", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");

    Equal(138, row.Quarterly[FinancialMetricsCatalog.RepaymentOfDebt.Id].Value!.Value, "repayment of debt includes finance lease interest component");
    True(row.Quarterly[FinancialMetricsCatalog.RepaymentOfDebt.Id].Formula == FinancialMetricFormulaIds.DebtRepaymentComponentsWithFinanceLeaseInterest, "repayment formula records finance lease interest component");
}

void FinancialMetricsBuilderBlanksTotalDebtFromLongTermDebtOnly()
{
    var longTermDebtSpec = FinancialMetricsCatalog.LongTermDebtConceptsFor(FinancialMetricProvider.UsGaap).First();
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [longTermDebtSpec.Key] =
            [
                new FinancialPoint(null, "2024-12-31", 500, "10-K", "2025-02-01", longTermDebtSpec.SourceName, 2024, "FY")
            ]
        },
        [],
        CompanyFactsProfile.Empty);

    var result = FinancialMetricsBuilder.Build("DEBT", inputs);
    var row = result.Rows.Single(r => r.PeriodEnd == "2024-12-31");

    Equal(500, row.Point[FinancialMetricsCatalog.LongTermDebt.Id].Value!.Value, "long-term debt row remains available");
    True(!row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value.HasValue, "total debt blanks when only long-term debt is present");
    True(row.Point[FinancialMetricsCatalog.TotalDebt.Id].Formula == FinancialMetricFormulaIds.BlankMissingInputs, "long-term-only total debt uses blank formula id");
}

void FinancialMetricsBuilderDoesNotDoubleCountBundledFinanceLeaseInTotalDebt()
{
    // A filer whose long-term debt concept already bundles finance leases
    // (LongTermDebtAndFinanceLeaseObligationsNoncurrent) and ALSO reports a separate finance
    // lease liability must not have the lease counted twice in total debt.
    var currentDebtSpec = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var bundledLongTermSpec = FinancialMetricsCatalog.UsGaap("LongTermDebtAndFinanceLeaseObligationsNoncurrent");
    var financeLeaseSpec = FinancialMetricsCatalog.UsGaap("FinanceLeaseLiability");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [currentDebtSpec.Key] = [new FinancialPoint(null, "2026-03-31", 100, "10-Q", "2026-04-30", currentDebtSpec.SourceName, 2026, "Q1")],
            [bundledLongTermSpec.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", bundledLongTermSpec.SourceName, 2026, "Q1")],
            [financeLeaseSpec.Key] = [new FinancialPoint(null, "2026-03-31", 200, "10-Q", "2026-04-30", financeLeaseSpec.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("BUNDLED", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");

    Equal(1100, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value, "bundled long-term debt is not summed with a separate finance lease again");
}

void FinancialMetricsBuilderDoesNotDoubleCountBundledFinanceLeaseInCurrentDebt()
{
    // The current-debt side can also carry a bundled concept
    // (LongTermDebtAndFinanceLeaseObligationsCurrent). When it does, the separately reported finance
    // lease must not be added on top â€” same invariant as the long-term side, enforced via the
    // declared FinanceLeaseBundledConcepts set rather than a substring guess.
    var bundledCurrentSpec = FinancialMetricsCatalog.UsGaap("LongTermDebtAndFinanceLeaseObligationsCurrent");
    var longTermSpec = FinancialMetricsCatalog.UsGaap("LongTermDebtNoncurrent");
    var financeLeaseSpec = FinancialMetricsCatalog.UsGaap("FinanceLeaseLiability");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [bundledCurrentSpec.Key] = [new FinancialPoint(null, "2026-03-31", 150, "10-Q", "2026-04-30", bundledCurrentSpec.SourceName, 2026, "Q1")],
            [longTermSpec.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", longTermSpec.SourceName, 2026, "Q1")],
            [financeLeaseSpec.Key] = [new FinancialPoint(null, "2026-03-31", 200, "10-Q", "2026-04-30", financeLeaseSpec.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("BUNDLEDCUR", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");

    Equal(1150, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value, "bundled current debt is not summed with a separate finance lease again");
}

void FinancialMetricsExportWritesCsvAndXlsx()
{
    var client = new LegacyClient(new FixtureSecCompanyDataSource(FixtureRoot()));
    var exporter = new FinancialMetricsExportService((client).Inner);

    var csv = exporter.BuildCsvAsync(["FIX"]).GetAwaiter().GetResult();
    True(csv.Contains("quarterlyCogsFormula"), "CSV includes formula columns");
    True(csv.Contains(FinancialMetricFormulaIds.RevenueMinusGrossProfit), "CSV includes fallback formula ids");

    var metricZip = exporter.BuildMetricCsvZipAsync(["FIX"]).GetAwaiter().GetResult();
    True(metricZip.Length > 1000, "metric CSV zip has content");
    True(metricZip[0] == 'P' && metricZip[1] == 'K', "metric CSV zip is a zip package");
    var metricFiles = exporter.BuildMetricCsvFilesAsync(["FIX"]).GetAwaiter().GetResult();
    var expectedMetricFileCount = FinancialMetricsCatalog.FlowMetrics.Count * 3 + FinancialMetricsCatalog.PointMetrics.Count;
    True(metricFiles.Count == expectedMetricFileCount, "metric CSV export has quarterly, annual, TTM, and point CSVs for every catalog metric");
    True(metricFiles.ContainsKey("metrics/quarterly/revenue.csv"), "quarterly revenue CSV exists");
    True(metricFiles.ContainsKey("metrics/annual/revenue.csv"), "annual revenue CSV exists");
    True(metricFiles.ContainsKey("metrics/ttm/revenue.csv"), "TTM revenue CSV exists");
    True(metricFiles.ContainsKey("metrics/point/totalAssets.csv"), "point assets CSV exists");

    var xlsx = exporter.BuildXlsxAsync(["FIX"]).GetAwaiter().GetResult();
    True(xlsx.Length > 1000, "XLSX has content");
    True(xlsx[0] == 'P' && xlsx[1] == 'K', "XLSX is a zip package");

    var trace = exporter.BuildTraceAsync(["FIX"], includeRawInputs: true).GetAwaiter().GetResult();
    True(trace.Definitions.Count == FinancialMetricsCatalog.AllMetrics.Count, "trace includes metric definitions");
    True(trace.RawInputs.Single().Facts.Count > 0, "trace can include raw SEC input facts");
    True(trace.AuditRows.Any(a => a.MetricId == "cogs" && a.ValueType == "quarterly" && a.Derivation == "blank"), "trace includes blank metric audit rows");
}

void FinancialMetricsTraceReturnsAuditRows()
{
    var client = new LegacyClient(new FixtureSecCompanyDataSource(FixtureRoot()));
    var exporter = new FinancialMetricsExportService((client).Inner);

    var trace = exporter.BuildTraceAsync(["FIX"]).GetAwaiter().GetResult();
    True(trace.Tickers.Single() == "FIX", "trace ticker normalized");
    True(trace.Rows.Any(r => r.Provider == "us-gaap"), "trace provider");
    True(trace.Rows.Any(r => r.PeriodEnd == "2023-12-31"), "trace latest period present");
    True(trace.Rows.All(r => r.ValidationStatus != "error"), "trace has no errors");
    True(trace.AuditRows.Any(r =>
        r.MetricId == "cogs"
        && r.ValueType == "quarterly"
        && r.Formula == FinancialMetricFormulaIds.RevenueMinusGrossProfit
        && r.SourceConcepts.Contains("Revenues", StringComparison.Ordinal)),
        "trace exposes formula and SEC source concepts");
    True(trace.AuditRows.Any(r =>
        r.MetricId == "cogs"
        && r.ValueType == "quarterly"
        && r.Formula == FinancialMetricFormulaIds.RevenueMinusGrossProfit
        && r.Inputs is { Count: 2 }
        && r.Inputs.Any(i => i.SourceConcepts.Contains("Revenues", StringComparison.Ordinal))),
        "trace exposes derived formula input components");
}

void FinancialMetricsSnapshotsMatch()
{
    var client = new LegacyClient(new FixtureSecCompanyDataSource(FixtureRoot()));
    var exporter = new FinancialMetricsExportService((client).Inner);

    var csv = exporter.BuildCsvAsync(["FIX"]).GetAwaiter().GetResult();
    MatchSnapshot("financial_metrics_FIX.csv", csv);
    foreach (var (name, content) in exporter.BuildMetricCsvFilesAsync(["FIX"]).GetAwaiter().GetResult())
        MatchSnapshot(Path.Combine("Metrics", name.Replace('/', Path.DirectorySeparatorChar)), content);

    var inputs = client.FetchFinancialMetricInputsAsync("FIX").GetAwaiter().GetResult();
    var result = FinancialMetricsBuilder.Build("FIX", inputs);
    MatchSnapshot("financial_metrics_FIX.audit.tsv", SerializeAuditSnapshot(result.AuditRows));
}

void CachedSubmissionFixtureLoadsFromFile()
{
    var source = new FixtureSecCompanyDataSource(FixtureRoot());
    using var doc = source.GetSubmissionsAsync("FIX").GetAwaiter().GetResult();
    True(doc.RootElement.GetProperty("name").GetString() == "Fixture Corp", "cached submission file lookup");
}

void StatementParserNegatesLossOnlyTableLabels()
{
    // Mirrors POET 6-K ex99-2.htm: loss rows expressed as positive numbers under "Operating Loss"
    // and "Net Loss" headers. Parser must flip sign so downstream metrics are negative.
    const string html = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr>
          <th></th>
          <th>Year Ended December 31, 2025</th>
          <th>Year Ended December 31, 2024</th>
        </tr>
        <tr><td>Revenue</td><td>2,917</td><td>1,483</td></tr>
        <tr><td>Operating loss</td><td>42,091</td><td>30,108</td></tr>
        <tr><td>Net loss</td><td>62,963</td><td>56,741</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001403708", "POET", "6-K", "0001493152-26-023471", "2026-05-15", "2025-12-31", "ex99-2.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(2_917_000, parsed.Single(p => p.Concept == "ifrs-full:Revenue" && p.End == "2025-12-31").Val, "revenue stays positive");
    Equal(-42_091_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLossFromOperatingActivities" && p.End == "2025-12-31").Val, "operating loss negated from positive table value");
    Equal(-30_108_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLossFromOperatingActivities" && p.End == "2024-12-31").Val, "prior year operating loss also negated");
    Equal(-62_963_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2025-12-31").Val, "net loss negated from positive table value");
    Equal(-56_741_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2024-12-31").Val, "prior year net loss also negated");
}

void StatementParserDoesNotNegateIncomeQualifiedLossLabels()
{
    // "Net income (loss)" and "Operating profit (loss)" contain both income/profit AND loss qualifiers.
    // These represent a metric that can swing either way; the raw value is correct as-is.
    const string html = """
    <html><body>
      <p>(in millions of U.S. dollars)</p>
      <table>
        <tr>
          <th></th>
          <th>Three Months Ended March 31, 2026</th>
          <th>Three Months Ended March 31, 2025</th>
        </tr>
        <tr><td>Revenue</td><td>10</td><td>8</td></tr>
        <tr><td>Operating profit (loss)</td><td>17</td><td>-90</td></tr>
        <tr><td>Net income (loss)</td><td>12</td><td>-99</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0002003292", "KLAR", "6-K", "0001628280-26-034877", "2026-05-14", "2026-03-31", "exhibit.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(17_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLossFromOperatingActivities" && p.End == "2026-03-31").Val, "operating profit (loss) positive value not negated");
    Equal(-90_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLossFromOperatingActivities" && p.End == "2025-03-31").Val, "operating profit (loss) negative value not double-negated");
    Equal(12_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2026-03-31").Val, "net income (loss) positive value not negated");
    Equal(-99_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2025-03-31").Val, "net income (loss) negative value not double-negated");
}

void StatementParserDoesNotDoubleNegateAlreadyNegativeLossValues()
{
    // If a table encodes losses as negative numbers under a "Net Loss" header (non-standard but
    // possible), the rawValue <= 0 guard prevents double negation.
    const string html = """
    <html><body>
      <p>(in thousands of U.S. dollars)</p>
      <table>
        <tr>
          <th></th>
          <th>Year Ended December 31, 2025</th>
        </tr>
        <tr><td>Revenue</td><td>5,000</td></tr>
        <tr><td>Net loss</td><td>(25,000)</td></tr>
      </table>
    </body></html>
    """;

    var filing = new SecFilingDocument("0001403708", "POET", "6-K", "0001493152-26-099999", "2026-03-01", "2025-12-31", "ex99-2.htm", html);
    var parsed = SecFilingStatementParser.Parse(filing);

    Equal(-25_000_000, parsed.Single(p => p.Concept == "ifrs-full:ProfitLoss" && p.End == "2025-12-31").Val, "already-negative net loss is not double-negated");
}

static string FixtureRoot()
{
    var fromOutput = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sec");
    if (Directory.Exists(fromOutput)) return fromOutput;

    return Path.Combine(TestProjectRoot(), "Fixtures", "sec");
}

static void MatchSnapshot(string name, string actual)
{
    var path = Path.Combine(TestProjectRoot(), "Snapshots", name);
    var normalized = NormalizeSnapshot(actual);

    if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, normalized, new UTF8Encoding(false));
        return;
    }

    if (!File.Exists(path))
        throw new Exception($"Missing snapshot {path}. Run with UPDATE_SNAPSHOTS=1 to create it.");

    var expected = NormalizeSnapshot(File.ReadAllText(path));
    if (expected == normalized) return;

    throw new Exception($"Snapshot mismatch for {name}. Run with UPDATE_SNAPSHOTS=1 after reviewing the financial metric change.");
}

static string SerializeAuditSnapshot(IEnumerable<FinancialMetricAuditRow> rows)
{
    var sb = new StringBuilder();
    sb.AppendLine("ticker\tperiodEnd\tmetricId\tmetricName\tvalueType\tprovider\ttaxonomy\tvalue\tformula\tsourceConcepts\tfiled\taccession\tderivation\tsourceType\tsourceDocument\tsourceLabel");
    foreach (var row in rows
        .OrderBy(r => r.Ticker)
        .ThenBy(r => r.PeriodEnd)
        .ThenBy(r => r.MetricId)
        .ThenBy(r => r.ValueType))
    {
        sb.Append(row.Ticker).Append('\t')
            .Append(row.PeriodEnd).Append('\t')
            .Append(row.MetricId).Append('\t')
            .Append(row.MetricName).Append('\t')
            .Append(row.ValueType).Append('\t')
            .Append(row.Provider).Append('\t')
            .Append(row.Taxonomy).Append('\t')
            .Append(row.Value?.ToString("G17", CultureInfo.InvariantCulture) ?? "").Append('\t')
            .Append(row.Formula).Append('\t')
            .Append(row.SourceConcepts).Append('\t')
            .Append(row.Filed ?? "").Append('\t')
            .Append(row.Accession ?? "").Append('\t')
            .Append(row.Derivation).Append('\t')
            .Append(row.SourceType).Append('\t')
            .Append(row.SourceDocument ?? "(none)").Append('\t')
            .AppendLine(row.SourceLabel ?? "(none)");
    }
    return sb.ToString();
}

static string NormalizeSnapshot(string value) =>
    value.Replace("\r\n", "\n").Replace("\r", "\n");

static string TestProjectRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var nested = Path.Combine(dir.FullName, "tests", "FilingForge.Tests", "FilingForge.Tests.csproj");
        if (File.Exists(nested)) return Path.GetDirectoryName(nested)!;

        var direct = Path.Combine(dir.FullName, "FilingForge.Tests.csproj");
        if (File.Exists(direct)) return dir.FullName;

        dir = dir.Parent;
    }

    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
}

static T AssertThrows<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T ex)
    {
        return ex;
    }

    throw new Exception($"Expected exception {typeof(T).Name}");
}


static void AccountsPayableIsTradePayablesOnly()
{
    // Guard (vendor mismatch, e.g. PYPL): our Accounts Payable is us-gaap:AccountsPayableCurrent
    // (trade payables) only. the vendor's broader "Accounts Payable" (incl. settlement/other payables)
    // is an intentional definitional difference; do not widen this concept without documentation.
    var ap = FinancialMetricsCatalog.UsGaap("AccountsPayableCurrent");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [ap.Key] = [new FinancialPoint(null, "2026-03-31", 108, "10-Q", "2026-04-30", ap.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("APONLY", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(108, row.Point["accountsPayable"].Value!.Value,
        "accounts payable reports trade payables (AccountsPayableCurrent) only");
}

static void CashUsesCarryingValueNotBankDeposits()
{
    // Guard (vendor mismatch, e.g. STT/MS): cashAndCashEquivalents uses
    // us-gaap:CashAndCashEquivalentsAtCarryingValue and does NOT add bank interest-bearing deposits.
    var cash = FinancialMetricsCatalog.UsGaap("CashAndCashEquivalentsAtCarryingValue");
    var deposits = FinancialMetricsCatalog.UsGaap("InterestBearingDepositsInBanks");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [cash.Key] = [new FinancialPoint(null, "2026-03-31", 3413, "10-Q", "2026-04-30", cash.SourceName, 2026, "Q1")],
            [deposits.Key] = [new FinancialPoint(null, "2026-03-31", 120000, "10-Q", "2026-04-30", deposits.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("CASHSCOPE", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(3413, row.Point["cashAndCashEquivalents"].Value!.Value,
        "cash uses carrying value, excluding bank interbank deposits");
}

static void GrossProfitUsesNarrowCogsScopeForFinancials()
{
    // Guard (vendor mismatch, e.g. CI/EQT): gross profit = Revenue - us-gaap:CostOfGoodsAndServicesSold.
    // The vendor subtracts a broader cost base (insurance benefits/claims) and shows a very different or
    // negative gross profit for financials/energy; our narrow SEC-COGS definition is intentional.
    const string json = """
    { "facts": { "us-gaap": {
      "Revenues": { "units": { "USD": [ { "form":"10-K","start":"2024-01-01","end":"2024-12-31","val":100000,"filed":"2025-02-01","fy":2024,"fp":"FY" } ] } },
      "CostOfGoodsAndServicesSold": { "units": { "USD": [ { "form":"10-K","start":"2024-01-01","end":"2024-12-31","val":84000,"filed":"2025-02-01","fy":2024,"fp":"FY" } ] } }
    } } }
    """;
    var client = new LegacyClient(new InlineSecCompanyDataSource(json));
    var inputs = client.FetchFinancialMetricInputsAsync("GP").GetAwaiter().GetResult();
    var row = FinancialMetricsBuilder.Build("GP", inputs).Rows.Single(r => r.PeriodEnd == "2024-12-31");
    Equal(16000, row.Annual["grossProfit"].Value!.Value,
        "gross profit = revenue - SEC CostOfGoodsAndServicesSold (narrow scope)");
}

// ---- Vendor-comparison regressions (RED until the fix lands) ----

static void TotalDebtIncludesCurrentMaturitiesOfLongTermDebt()
{
    // BUG (vendor mismatch, e.g. ADM/ADBE): total debt must include the current portion of
    // long-term debt. A filer that reports short-term borrowings, a separate current-maturities
    // concept (LongTermDebtCurrent), and noncurrent long-term debt currently drops the current
    // maturities because DebtCurrentConcepts does not list LongTermDebtCurrent (or CommercialPaper).
    var shortTerm = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var currentMaturities = FinancialMetricsCatalog.UsGaap("LongTermDebtCurrent");
    var longTermNoncurrent = FinancialMetricsCatalog.UsGaap("LongTermDebtNoncurrent");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [shortTerm.Key] = [new FinancialPoint(null, "2026-03-31", 100, "10-Q", "2026-04-30", shortTerm.SourceName, 2026, "Q1")],
            [currentMaturities.Key] = [new FinancialPoint(null, "2026-03-31", 50, "10-Q", "2026-04-30", currentMaturities.SourceName, 2026, "Q1")],
            [longTermNoncurrent.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", longTermNoncurrent.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("DEBTCM", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(1150, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "total debt = short-term borrowings + current maturities of long-term debt + noncurrent long-term debt");
}

static void TotalDebtDoesNotDoubleCountCurrentMaturitiesUnderAllInLongTermDebt()
{
    // Guard: when the long-term component is the all-in us-gaap:LongTermDebt (current + noncurrent),
    // it already includes current maturities, so a separately reported LongTermDebtCurrent must NOT
    // be added again.
    var shortTerm = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var currentMaturities = FinancialMetricsCatalog.UsGaap("LongTermDebtCurrent");
    var allInLongTerm = FinancialMetricsCatalog.UsGaap("LongTermDebt");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [shortTerm.Key] = [new FinancialPoint(null, "2026-03-31", 100, "10-Q", "2026-04-30", shortTerm.SourceName, 2026, "Q1")],
            [currentMaturities.Key] = [new FinancialPoint(null, "2026-03-31", 50, "10-Q", "2026-04-30", currentMaturities.SourceName, 2026, "Q1")],
            [allInLongTerm.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", allInLongTerm.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("DEBTALLIN", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(1100, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "all-in LongTermDebt already includes current maturities; do not add LongTermDebtCurrent again");
}

static void TotalDebtDoesNotDoubleCountCurrentMaturitiesUnderAggregateDebtCurrent()
{
    // Guard: us-gaap:DebtCurrent is the aggregate current debt (short-term + current maturities), so
    // a separately reported LongTermDebtCurrent must NOT be added on top of it.
    var debtCurrent = FinancialMetricsCatalog.UsGaap("DebtCurrent");
    var currentMaturities = FinancialMetricsCatalog.UsGaap("LongTermDebtCurrent");
    var longTermNoncurrent = FinancialMetricsCatalog.UsGaap("LongTermDebtNoncurrent");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [debtCurrent.Key] = [new FinancialPoint(null, "2026-03-31", 150, "10-Q", "2026-04-30", debtCurrent.SourceName, 2026, "Q1")],
            [currentMaturities.Key] = [new FinancialPoint(null, "2026-03-31", 50, "10-Q", "2026-04-30", currentMaturities.SourceName, 2026, "Q1")],
            [longTermNoncurrent.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", longTermNoncurrent.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("DEBTAGG", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(1150, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "aggregate DebtCurrent already includes current maturities; total = DebtCurrent + noncurrent long-term debt");
}

static void ShareFallbackReconcilesFullBaseButRejectsPartialAndLeak()
{
    // The weighted-average fallback is retained only when its latest value reconciles with the cover
    // total (proving it is the full share base). CMCSA/FOXA/NWSA keep a full weighted-average history
    // alongside a few stale/garbage outstanding facts and must be retained; HEI's partial retained
    // class and ERIE's stale leak (also FOXA's dei val=1) must be rejected.
    FinancialPoint Cover(double v) => new(null, "2026-03-31", v, "10-Q", "2026-04-28", "dei:EntityCommonStockSharesOutstanding");
    FinancialPoint Fallback(double v) => new(null, "2026-03-31", v, "10-Q", "2026-04-28", "WeightedAverageNumberOfDilutedSharesOutstanding");

    True(FundamentalsBuilder.ShareFallbackReconcilesWithCover(Fallback(3_700_000_000), Cover(3_800_000_000)),
        "full weighted-average base (CMCSA-like) reconciles with cover");
    True(!FundamentalsBuilder.ShareFallbackReconcilesWithCover(Fallback(55_000_000), Cover(138_000_000)),
        "partial retained share class (HEI-like) does not reconcile and is rejected");
    True(!FundamentalsBuilder.ShareFallbackReconcilesWithCover(Fallback(2542), Cover(46_191_610)),
        "stale garbage leak (ERIE-like) does not reconcile and is rejected");
    True(!FundamentalsBuilder.ShareFallbackReconcilesWithCover(Fallback(3_700_000_000), null),
        "no cover total means no reconciliation");
}

static void ShareFallbackKeepsWeightedAverageHistoryUnderCover()
{
    // BUG (META historical P/E blank ~10 years): a fully share-class-dimensioned filer has no
    // point-in-time outstanding fact in companyfacts â€” only the non-dimensional weighted-average
    // total. The cover-page fallback (recent filings only) previously DROPPED that weighted-average
    // history, so every bar older than the cover window had no share count and P/E blanked. Cover must
    // win for the ends it covers and weighted-average must fill the older ends.
    var cover = new List<FinancialPoint>
    {
        new(null, "2026-03-31", 2538, "10-Q", "2026-04-28", "dei:EntityCommonStockSharesOutstanding")
    };
    var weightedAverageHistory = new List<FinancialPoint>
    {
        new(null, "2016-03-31", 2888, "10-Q", "2016-04-27", "WeightedAverageNumberOfDilutedSharesOutstanding"),
        new(null, "2019-03-31", 2850, "10-Q", "2019-04-24", "WeightedAverageNumberOfDilutedSharesOutstanding"),
        new(null, "2026-03-31", 2560, "10-Q", "2026-04-28", "WeightedAverageNumberOfDilutedSharesOutstanding")
    };

    // Every weighted-average end has a matching earnings period; a stale orphan end (2011) does not.
    var valuationEnds = new HashSet<string>(StringComparer.Ordinal) { "2016-03-31", "2019-03-31", "2026-03-31" };
    var weightedAverageWithOrphan = new List<FinancialPoint>(weightedAverageHistory)
    {
        new(null, "2011-03-31", 9999, "10-Q", "2011-04-27", "WeightedAverageNumberOfDilutedSharesOutstanding")
    };

    var combined = FundamentalsBuilder.CombineCoverWithShareFallback(cover, weightedAverageWithOrphan, valuationEnds);

    Equal(2538, combined.Single(p => p.End == "2026-03-31").Val,
        "cover point wins for the period it covers (not the weighted-average)");
    Equal(2888, combined.Single(p => p.End == "2016-03-31").Val,
        "weighted-average history fills an older end cover does not reach");
    True(combined.Any(p => p.End == "2019-03-31"),
        "all older weighted-average ends with earnings are retained for historical per-share valuation");
    True(combined.All(p => p.End != "2011-03-31"),
        "a weighted-average end with no valuation numerator (stale orphan leak) is dropped");
}

static void TotalDebtReadsDebtAndCapitalLeaseObligationsDirectTotal()
{
    // BUG (vendor mismatch, e.g. SW/Smurfit Westrock): filer reports its all-in total debt under the
    // pre-2019 us-gaap:DebtAndCapitalLeaseObligations concept (the "Capital" lease naming) which our
    // direct-total list omitted, so quarterly total debt blanked.
    var direct = FinancialMetricsCatalog.UsGaap("DebtAndCapitalLeaseObligations");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [direct.Key] = [new FinancialPoint(null, "2026-03-31", 14255, "10-Q", "2026-04-28", direct.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("SWTEST", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(14255, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "DebtAndCapitalLeaseObligations is a direct all-in total debt");
}

static void TotalDebtReadsCombinedLongShortDebtDirectTotal()
{
    // BUG (vendor mismatch, e.g. VTR/Ventas): filer reports quarterly total debt under
    // us-gaap:DebtLongtermAndShorttermCombinedAmount (aggregate long-term debt incl. current
    // maturities), while us-gaap:LongTermDebt is tagged only at year-end. Our list omitted it, so
    // quarterly total debt blanked.
    var direct = FinancialMetricsCatalog.UsGaap("DebtLongtermAndShorttermCombinedAmount");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [direct.Key] = [new FinancialPoint(null, "2026-03-31", 12518, "10-Q", "2026-04-28", direct.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("VTRTEST", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(12518, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "DebtLongtermAndShorttermCombinedAmount is a direct all-in total debt");
}

static void TotalDebtDoesNotDoubleCountBundledCapitalLease()
{
    // Guard: the pre-2019 LongTermDebtAndCapitalLeaseObligationsNoncurrent already bundles capital
    // leases; a separately reported finance/capital lease liability must not be added again (mirror of
    // the FinanceLease-named double-count guard).
    var currentDebt = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var bundledLongTerm = FinancialMetricsCatalog.UsGaap("LongTermDebtAndCapitalLeaseObligationsNoncurrent");
    var financeLease = FinancialMetricsCatalog.UsGaap("FinanceLeaseLiability");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [currentDebt.Key] = [new FinancialPoint(null, "2026-03-31", 100, "10-Q", "2026-04-30", currentDebt.SourceName, 2026, "Q1")],
            [bundledLongTerm.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", bundledLongTerm.SourceName, 2026, "Q1")],
            [financeLease.Key] = [new FinancialPoint(null, "2026-03-31", 200, "10-Q", "2026-04-30", financeLease.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("CAPLEASE", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(1100, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "bundled capital-lease long-term concept must not double-count a separate lease liability");
}

static void TotalDebtUsesAllInLongTermDebtAloneWhenNoCurrentDebt()
{
    // BUG (vendor mismatch, e.g. CPT/Camden): a REIT reports a single all-in us-gaap:LongTermDebt line
    // (current + noncurrent) and no separate current-debt concept. Total debt must equal that all-in
    // value, not blank. (A noncurrent-only concept still blanks â€” see
    // FinancialMetricsBuilderBlanksTotalDebtFromLongTermDebtOnly.)
    var allInLongTerm = FinancialMetricsCatalog.UsGaap("LongTermDebt");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [allInLongTerm.Key] = [new FinancialPoint(null, "2026-03-31", 4250, "10-Q", "2026-04-30", allInLongTerm.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("REITDEBT", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(4250, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "all-in LongTermDebt alone (includes current maturities) is total debt for single-line REIT filers");
}

static void RevenueUsesOperatingLeaseIncomeWhenNoOtherRevenueConcept()
{
    // BUG (vendor mismatch, e.g. EQR): a pure lessor REIT tags rental revenue only under
    // us-gaap:OperatingLeaseLeaseIncome and no Revenues/RevenueFromContractWithCustomer total, so
    // revenue was blank. The lease income is the top-line revenue.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "OperatingLeaseLeaseIncome": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 390, "filed": "2024-05-01", "fp": "Q1" }
          ] } }
        }
      }
    }
    """;
    var inputs = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFinancialMetricInputsAsync("EQRTEST").GetAwaiter().GetResult();
    Equal(390, inputs.RevenuePoints.Single(p => p.End == "2024-03-31").Val,
        "REIT with only OperatingLeaseLeaseIncome uses it as revenue");
}

static void RevenuePrefersDominantOperatingLeaseIncomeOverTinyContractResidual()
{
    // BUG (vendor mismatch, e.g. CPT/Camden): filer tags a tiny non-lease
    // RevenueFromContractWithCustomerExcludingAssessedTax residual (2) plus the real rental revenue
    // under OperatingLeaseLeaseIncome (390). Priority merge would pick the tiny contract residual;
    // when the same period's lease income is larger it is the real total revenue (matches the vendor).
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "RevenueFromContractWithCustomerExcludingAssessedTax": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 2, "filed": "2024-05-01", "fp": "Q1" }
          ] } },
          "OperatingLeaseLeaseIncome": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 390, "filed": "2024-05-01", "fp": "Q1" }
          ] } }
        }
      }
    }
    """;
    var inputs = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFinancialMetricInputsAsync("CPTTEST").GetAwaiter().GetResult();
    Equal(390, inputs.RevenuePoints.Single(p => p.End == "2024-03-31").Val,
        "dominant OperatingLeaseLeaseIncome overrides tiny contract-revenue residual");
}

static void RevenueKeepsContractRevenueWhenLargerThanSubleaseIncome()
{
    // Guard: a normal operating company (real sales under RevenueFromContractWithCustomer) may also
    // tag a small sublease OperatingLeaseLeaseIncome. Revenue must stay the contract revenue; lease
    // income must NOT override when it is smaller.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "RevenueFromContractWithCustomerExcludingAssessedTax": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 1000, "filed": "2024-05-01", "fp": "Q1" }
          ] } },
          "OperatingLeaseLeaseIncome": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 50, "filed": "2024-05-01", "fp": "Q1" }
          ] } }
        }
      }
    }
    """;
    var inputs = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFinancialMetricInputsAsync("RETAILTEST").GetAwaiter().GetResult();
    Equal(1000, inputs.RevenuePoints.Single(p => p.End == "2024-03-31").Val,
        "smaller sublease OperatingLeaseLeaseIncome does not override real contract revenue");
}

static void RevenueKeepsExplicitRevenuesTotalOverLeaseIncome()
{
    // Guard: when a filer tags the explicit us-gaap:Revenues total, it wins; the override only
    // applies to contract-revenue residual concepts, never to the Revenues total (which may already
    // include lease income as a component).
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "Revenues": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 1000, "filed": "2024-05-01", "fp": "Q1" }
          ] } },
          "OperatingLeaseLeaseIncome": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-03-31", "val": 800, "filed": "2024-05-01", "fp": "Q1" }
          ] } }
        }
      }
    }
    """;
    var inputs = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFinancialMetricInputsAsync("TOTALTEST").GetAwaiter().GetResult();
    Equal(1000, inputs.RevenuePoints.Single(p => p.End == "2024-03-31").Val,
        "explicit Revenues total is not overridden by OperatingLeaseLeaseIncome component");
}

static void RevenueDropsStandaloneResidualWhenCumulativeLeaseDominatesEnd()
{
    // BUG (review finding): a lessor tags the non-lease RevenueFromContractWithCustomer residual as a
    // STANDALONE quarter (Apr-Jun = 2) but OperatingLeaseLeaseIncome only as CUMULATIVE YTD
    // (Jan-Jun = 787). Same-period override cannot fire (different FlowPeriodKeys), so the tiny
    // standalone residual would win quarterly reconstruction. End-level cleanup must drop it.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "RevenueFromContractWithCustomerExcludingAssessedTax": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-04-01", "end": "2024-06-30", "val": 2, "filed": "2024-08-01", "fp": "Q2" }
          ] } },
          "OperatingLeaseLeaseIncome": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-06-30", "val": 787, "filed": "2024-08-01", "fp": "Q2" }
          ] } }
        }
      }
    }
    """;
    var inputs = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFinancialMetricInputsAsync("LESSOR").GetAwaiter().GetResult();
    var atEnd = inputs.RevenuePoints.Where(p => p.End == "2024-06-30").ToList();
    True(atEnd.All(p => Math.Abs(p.Val - 2) > 0.5),
        "tiny standalone contract residual is dropped when a larger lease income shares its period end");
    True(atEnd.Any(p => Math.Abs(p.Val - 787) < 0.5),
        "the dominant cumulative lease income remains as revenue for that end");
}

static void RevenueKeepsStandaloneContractWhenLeaseAtEndIsSmaller()
{
    // Guard for the cleanup above: a normal company with real standalone contract revenue (1000) and
    // a small cumulative sublease income (50) at the same period end must KEEP the contract revenue.
    const string json = """
    {
      "facts": {
        "us-gaap": {
          "RevenueFromContractWithCustomerExcludingAssessedTax": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-04-01", "end": "2024-06-30", "val": 1000, "filed": "2024-08-01", "fp": "Q2" }
          ] } },
          "OperatingLeaseLeaseIncome": { "units": { "USD": [
            { "form": "10-Q", "start": "2024-01-01", "end": "2024-06-30", "val": 50, "filed": "2024-08-01", "fp": "Q2" }
          ] } }
        }
      }
    }
    """;
    var inputs = new LegacyClient(new InlineSecCompanyDataSource(json))
        .FetchFinancialMetricInputsAsync("NORMAL").GetAwaiter().GetResult();
    True(inputs.RevenuePoints.Any(p => p.End == "2024-06-30" && Math.Abs(p.Val - 1000) < 0.5),
        "real standalone contract revenue is not dropped by a smaller sublease lease income at the same end");
}

static void SelectReconcilePairPrefersOverlappingEnd()
{
    // Review finding: reconcile the fallback against cover at a SHARED period end (apples-to-apples),
    // not by comparing the latest of each (different dates drift with buybacks). When the two series
    // overlap, use the shared end; otherwise fall back to latest-of-each.
    var fallback = new List<FinancialPoint>
    {
        new(null, "2016-03-31", 2888, "10-Q", "2016-04-27", "WeightedAverageNumberOfDilutedSharesOutstanding"),
        new(null, "2026-03-31", 2560, "10-Q", "2026-04-28", "WeightedAverageNumberOfDilutedSharesOutstanding")
    };
    var coverByEnd = new Dictionary<string, FinancialPoint>(StringComparer.Ordinal)
    {
        ["2026-03-31"] = new(null, "2026-03-31", 2538, "10-Q", "2026-04-28", "dei:EntityCommonStockSharesOutstanding")
    };
    var latestCover = new FinancialPoint(null, "2026-04-17", 2538, "10-Q", "2026-04-28", "dei:EntityCommonStockSharesOutstanding");

    var overlap = FundamentalsBuilder.SelectReconcilePair(fallback, coverByEnd, latestCover);
    Equal(2560, overlap.Fallback!.Val, "overlap reconcile uses the fallback point at the shared period end");
    Equal(2538, overlap.Cover!.Val, "overlap reconcile uses the cover point at the same shared period end");

    var noOverlap = FundamentalsBuilder.SelectReconcilePair(
        fallback,
        new Dictionary<string, FinancialPoint>(StringComparer.Ordinal),
        latestCover);
    Equal(2560, noOverlap.Fallback!.Val, "no shared end falls back to the latest positive fallback point");
    Equal(2538, noOverlap.Cover!.Val, "no shared end falls back to the latest cover total");
}

// Regression: TTM must be computed from four consecutive reconstructed quarters even when only SOME
// of them carry fiscal metadata. Fiscal and calendar quarter indices are different scales for an
// off-calendar filer (FY2026 Q1 ending 2025-12-27 -> fiscal 8104, calendar 8103), so mixing the two
// inside one window made a genuine four-quarter run look non-consecutive and blanked TTM.
static void TtmBuilderSumsFourQuartersWhenFiscalMetadataIsPartial()
{
    static FinancialMetricValue Q(double value) =>
        new(value, "DirectQuarterly", "us-gaap:Revenues", "2026-01-30", "0000320193-26-000001", "direct");

    // Apple-style September fiscal year end: these four ends are consecutive quarters.
    var quarterly = new Dictionary<string, FinancialMetricValue>
    {
        ["2025-03-29"] = Q(10),
        ["2025-06-28"] = Q(20),
        ["2025-09-27"] = Q(30),
        ["2025-12-27"] = Q(40),
    };

    static FinancialMetricQuarterEntry Meta(string end, int? fy, int? fq) =>
        new(end, fy, fq, Q(0), []);

    // Only the first and last quarters know their fiscal period; the middle two do not. Before the
    // fix this mixed fiscal (8104) and calendar (8100/8101) indices and produced no TTM at all.
    var metaByEnd = new Dictionary<string, FinancialMetricQuarterEntry>
    {
        ["2025-03-29"] = Meta("2025-03-29", 2025, 2),
        ["2025-06-28"] = Meta("2025-06-28", null, null),
        ["2025-09-27"] = Meta("2025-09-27", null, null),
        ["2025-12-27"] = Meta("2025-12-27", 2026, 1),
    };

    var ttm = FinancialMetricTtmBuilder.Build(quarterly, metaByEnd);

    True(ttm.ContainsKey("2025-12-27"), "TTM exists for the newest of four consecutive quarters");
    Equal(100, ttm["2025-12-27"].Value!.Value, "TTM sums the four quarters");
    True(ttm["2025-12-27"].Formula == FinancialMetricFormulaIds.TtmSumQuarterly, "TTM uses the sum-of-quarters formula");

    // A genuine gap must still blank TTM — the fix must not make consecutiveness meaningless.
    var gapped = new Dictionary<string, FinancialMetricValue>
    {
        ["2025-03-29"] = Q(10),
        ["2025-06-28"] = Q(20),
        // 2025-09-27 missing
        ["2025-12-27"] = Q(40),
        ["2026-03-28"] = Q(50),
    };
    var gappedMeta = new Dictionary<string, FinancialMetricQuarterEntry>
    {
        ["2025-03-29"] = Meta("2025-03-29", null, null),
        ["2025-06-28"] = Meta("2025-06-28", null, null),
        ["2025-12-27"] = Meta("2025-12-27", null, null),
        ["2026-03-28"] = Meta("2026-03-28", null, null),
    };
    var gappedTtm = FinancialMetricTtmBuilder.Build(gapped, gappedMeta);
    True(gappedTtm.Count == 0, "a missing quarter must still blank TTM");

    // 53-week fiscal year (Apple FY2017): the 14-week Q1 ends 2016-12-31 and Q2 ends 2017-04-01, so
    // quarter ends drift ACROSS calendar quarters (Q4 -> Q2 by calendar bucket). Bucketing saw a
    // false gap here; period-end spacing (98d then 91d) correctly reads them as consecutive.
    var week53 = new Dictionary<string, FinancialMetricValue>
    {
        ["2016-09-24"] = Q(10),
        ["2016-12-31"] = Q(20),
        ["2017-04-01"] = Q(30),
        ["2017-07-01"] = Q(40),
    };
    var week53Meta = new Dictionary<string, FinancialMetricQuarterEntry>
    {
        // Deliberately WRONG/duplicated labels, as Apple actually tags these older periods.
        ["2016-09-24"] = Meta("2016-09-24", 2016, 3),
        ["2016-12-31"] = Meta("2016-12-31", 2016, 4),
        ["2017-04-01"] = Meta("2017-04-01", null, null),
        ["2017-07-01"] = Meta("2017-07-01", null, null),
    };
    var week53Ttm = FinancialMetricTtmBuilder.Build(week53, week53Meta);
    True(week53Ttm.ContainsKey("2017-07-01"), "a 53-week fiscal year must still produce TTM");
    Equal(100, week53Ttm["2017-07-01"].Value!.Value, "53-week TTM sums its four quarters");
}

static void TotalDebtPrefersNoncurrentWhenAllInLongTermIsUnderscoped()
{
    // BUG (vendor mismatch, e.g. AJG): a filer may tag us-gaap:LongTermDebt with a narrow value
    // (only current maturities / a single tranche) while carrying the bulk of long-term debt under
    // us-gaap:LongTermDebtNoncurrent. The all-in LongTermDebt concept must be >= its noncurrent
    // portion; when it is smaller it is not the aggregate, so total debt must fall back to the
    // noncurrent-based figure instead of understating debt to the tiny LongTermDebt value.
    var shortTerm = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var allInLongTerm = FinancialMetricsCatalog.UsGaap("LongTermDebt");
    var longTermNoncurrent = FinancialMetricsCatalog.UsGaap("LongTermDebtNoncurrent");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [shortTerm.Key] = [new FinancialPoint(null, "2024-12-31", 200, "10-K", "2025-02-18", shortTerm.SourceName, 2024, "Q4")],
            [allInLongTerm.Key] = [new FinancialPoint(null, "2024-12-31", 23, "10-K", "2025-02-18", allInLongTerm.SourceName, 2024, "Q4")],
            [longTermNoncurrent.Key] = [new FinancialPoint(null, "2024-12-31", 12731, "10-K", "2025-02-18", longTermNoncurrent.SourceName, 2024, "Q4")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("DEBTUNDER", inputs).Rows.Single(r => r.PeriodEnd == "2024-12-31");
    Equal(12931, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "underscoped all-in LongTermDebt (< noncurrent) is ignored; total = short-term borrowings + noncurrent long-term debt");
}

static void TotalDebtKeepsAllInLongTermWhenItExceedsNoncurrent()
{
    // Guard: when the all-in us-gaap:LongTermDebt (current + noncurrent) is genuinely >= its
    // noncurrent portion, it is the correct aggregate and must be used as-is (not swapped for the
    // smaller noncurrent value, and current maturities not re-added).
    var shortTerm = FinancialMetricsCatalog.UsGaap("ShortTermBorrowings");
    var allInLongTerm = FinancialMetricsCatalog.UsGaap("LongTermDebt");
    var longTermNoncurrent = FinancialMetricsCatalog.UsGaap("LongTermDebtNoncurrent");
    var inputs = new FinancialMetricInputs(
        new Dictionary<string, IReadOnlyList<FinancialPoint>>
        {
            [shortTerm.Key] = [new FinancialPoint(null, "2026-03-31", 100, "10-Q", "2026-04-30", shortTerm.SourceName, 2026, "Q1")],
            [allInLongTerm.Key] = [new FinancialPoint(null, "2026-03-31", 1000, "10-Q", "2026-04-30", allInLongTerm.SourceName, 2026, "Q1")],
            [longTermNoncurrent.Key] = [new FinancialPoint(null, "2026-03-31", 900, "10-Q", "2026-04-30", longTermNoncurrent.SourceName, 2026, "Q1")]
        },
        [],
        CompanyFactsProfile.Empty);

    var row = FinancialMetricsBuilder.Build("DEBTALLIN2", inputs).Rows.Single(r => r.PeriodEnd == "2026-03-31");
    Equal(1100, row.Point[FinancialMetricsCatalog.TotalDebt.Id].Value!.Value,
        "all-in LongTermDebt >= noncurrent is the aggregate; total = short-term borrowings + all-in long-term debt");
}



var failures = new List<string>();
var passed = 0;
void Run(string name, Action test)
{
    try { test(); passed++; }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetBaseException().Message}"); }
}
Run("ImpliesQ4AndTtmFromAnnualWithoutStart", ImpliesQ4AndTtmFromAnnualWithoutStart);
Run("RevenueSeriesBuilderTreatsNoStartAmendedAnnualAsAnnual", RevenueSeriesBuilderTreatsNoStartAmendedAnnualAsAnnual);
Run("DerivesQ3FromNineMonthsMinusQ1Q2", DerivesQ3FromNineMonthsMinusQ1Q2);
Run("PreservesFiscalQuarterFromSecMetadata", PreservesFiscalQuarterFromSecMetadata);
Run("PreservesNonCalendarFiscalQuarterOnCalendarPeriod", PreservesNonCalendarFiscalQuarterOnCalendarPeriod);
Run("CalendarComparativeFactsUsePeriodYearForLabels", CalendarComparativeFactsUsePeriodYearForLabels);
Run("FiscalCalendarLabelsNonCalendarYearEndAsQ4", FiscalCalendarLabelsNonCalendarYearEndAsQ4);
Run("FiscalCalendarIgnoresFilingScopedFiscalYearOnComparatives", FiscalCalendarIgnoresFilingScopedFiscalYearOnComparatives);
Run("FiscalCalendarLabelsQuartersPastTheLastAnnual", FiscalCalendarLabelsQuartersPastTheLastAnnual);
Run("FiscalCalendarKeepsFilerFiscalYearNumbering", FiscalCalendarKeepsFilerFiscalYearNumbering);
Run("StartlessTransactionDateFactSnapsToStatementPeriodEnd", StartlessTransactionDateFactSnapsToStatementPeriodEnd);
Run("StartlessFactOnItsOwnPeriodEndIsNotSnapped", StartlessFactOnItsOwnPeriodEndIsNotSnapped);
Run("FiscalCalendarLabelsPeriodsBeforeTheEarliestAnnual", FiscalCalendarLabelsPeriodsBeforeTheEarliestAnnual);
Run("FiscalCalendarKeepsQ1AfterAFiledYearEnd", FiscalCalendarKeepsQ1AfterAFiledYearEnd);
Run("FiscalCalendarNumbersFiscalYearsInSequence", FiscalCalendarNumbersFiscalYearsInSequence);
Run("CanonicalizingAPeriodEndPreservesItsSpan", CanonicalizingAPeriodEndPreservesItsSpan);
Run("TtmSeriesUsesLatestFiledDateInWindow", TtmSeriesUsesLatestFiledDateInWindow);
Run("TtmSeriesCarriesConceptProvenance", TtmSeriesCarriesConceptProvenance);
Run("QuarterlySeriesBuilderTreatsNoStartAmendedQuarterAsQuarter", QuarterlySeriesBuilderTreatsNoStartAmendedQuarterAsQuarter);
Run("QuarterlySeriesPrefersPerAdsEpsOverOrdinaryShareEps", QuarterlySeriesPrefersPerAdsEpsOverOrdinaryShareEps);
Run("QuarterlySeriesDerivesQ2FromActualQ3Boundary", QuarterlySeriesDerivesQ2FromActualQ3Boundary);
Run("QuarterlySeriesDoesNotInventQ2MonthEndForNonCalendarNineMonth", QuarterlySeriesDoesNotInventQ2MonthEndForNonCalendarNineMonth);
Run("QuarterlySeriesPrefersHigherPrecisionSameAccessionFact", QuarterlySeriesPrefersHigherPrecisionSameAccessionFact);
Run("StatementSeriesIncludesCashFlowOnlyRows", StatementSeriesIncludesCashFlowOnlyRows);
Run("MergePreservesEarliestFiledAsFirstFiled", MergePreservesEarliestFiledAsFirstFiled);
Run("MergeRecordsMaterialRestatementVersion", MergeRecordsMaterialRestatementVersion);
Run("BuildTtmSeriesEmitsPointInTimeSegmentsForRestatedQuarter", BuildTtmSeriesEmitsPointInTimeSegmentsForRestatedQuarter);
Run("BuildTtmSeriesCarriesWindowProvenanceForTrace", BuildTtmSeriesCarriesWindowProvenanceForTrace);
Run("EdgarClientDerivesOperatingIncomeWhenDirectMissing", EdgarClientDerivesOperatingIncomeWhenDirectMissing);
Run("CachedCompanyFactsFixtureFeedsEdgarClient", CachedCompanyFactsFixtureFeedsEdgarClient);
Run("EdgarClientUsesDeiSharesForFundamentals", EdgarClientUsesDeiSharesForFundamentals);
Run("EdgarClientUsesWeightedAverageSharesForAnalysisFundamentals", EdgarClientUsesWeightedAverageSharesForAnalysisFundamentals);
Run("EdgarClientReadsAmendedFinancialForms", EdgarClientReadsAmendedFinancialForms);
Run("EdgarClientFundamentalsCanSkipTrackingForTrace", EdgarClientFundamentalsCanSkipTrackingForTrace);
Run("EdgarClientUsesBankRevenueFallbackForMissingPeriodsOnly", EdgarClientUsesBankRevenueFallbackForMissingPeriodsOnly);
Run("EdgarClientDoesNotTrackEmptyCompanyFacts", EdgarClientDoesNotTrackEmptyCompanyFacts);
Run("EdgarClientUsesIfrsFundamentalsDespiteSparseUsGaapNotes", EdgarClientUsesIfrsFundamentalsDespiteSparseUsGaapNotes);
Run("EdgarClientPrefersLatestFiledShareFactForFundamentals", EdgarClientPrefersLatestFiledShareFactForFundamentals);
Run("EdgarClientUsesCoverPageSharesWhenCompanyfactsLacksRecentOutstanding", EdgarClientUsesCoverPageSharesWhenCompanyfactsLacksRecentOutstanding);
Run("EdgarClientKeepsCompanyfactsWhenCoverMatches", EdgarClientKeepsCompanyfactsWhenCoverMatches);
Run("EdgarClientPrefersCoverWhenItExceedsCompanyfacts", EdgarClientPrefersCoverWhenItExceedsCompanyfacts);
Run("EdgarClientDedupsCoverSeriesByPeriodEnd", EdgarClientDedupsCoverSeriesByPeriodEnd);
Run("SecCoverShareReaderSumsShareClassContexts", SecCoverShareReaderSumsShareClassContexts);
Run("SecCoverShareReaderAppliesEconomicClassRatios", SecCoverShareReaderAppliesEconomicClassRatios);
Run("EdgarClientSumsShareClassesWithinAFiling", EdgarClientSumsShareClassesWithinAFiling);
Run("AnalysisNetIncomePrefersParentOverConsolidated", AnalysisNetIncomePrefersParentOverConsolidated);
Run("EdgarClientSkipsNullFactValues", EdgarClientSkipsNullFactValues);
Run("FinancialMetricsBuilderUsesIfrsProviderAndAnnualPointFacts", FinancialMetricsBuilderUsesIfrsProviderAndAnnualPointFacts);
Run("FinancialMetricsBuilderSubtractsIfrsGoodwillAndOtherIntangibles", FinancialMetricsBuilderSubtractsIfrsGoodwillAndOtherIntangibles);
Run("FinancialMetricsBuilderUsesIfrsInterimDurationFacts", FinancialMetricsBuilderUsesIfrsInterimDurationFacts);
Run("FinancialMetricsBuilderDerivesCumulativeOnlyIfrsQuartersWithoutQ1", FinancialMetricsBuilderDerivesCumulativeOnlyIfrsQuartersWithoutQ1);
Run("FinancialMetricsTtmSkipsNonConsecutiveHalfYearWindows", FinancialMetricsTtmSkipsNonConsecutiveHalfYearWindows);
Run("BuildTtmSeriesSkipsWindowWithSingleMissingQuarter", BuildTtmSeriesSkipsWindowWithSingleMissingQuarter);
Run("AnnualMinusNineMonthRejectsOverlappingNineMonthPeriod", AnnualMinusNineMonthRejectsOverlappingNineMonthPeriod);
Run("QuarterlySeriesBuilderComputesCumulativeTtmForHalfYearFilers", QuarterlySeriesBuilderComputesCumulativeTtmForHalfYearFilers);
Run("RevenueSeriesWithholdsTtmForNonConsecutiveIfrsQuarters", RevenueSeriesWithholdsTtmForNonConsecutiveIfrsQuarters);
Run("StatementSeriesUsesRecentSixKFilingTablesWhenCompanyfactsIsStale", StatementSeriesUsesRecentSixKFilingTablesWhenCompanyfactsIsStale);
Run("RevenueEndpointUsesRecentSixKFilingTablesWhenCompanyfactsIsStale", RevenueEndpointUsesRecentSixKFilingTablesWhenCompanyfactsIsStale);
Run("FundamentalsUsesRecentSixKFilingTablesForNetIncomeWhenCompanyfactsIsStale", FundamentalsUsesRecentSixKFilingTablesForNetIncomeWhenCompanyfactsIsStale);
Run("StatementParserReadsPerAdsEpsWithoutTableUnitMultiplier", StatementParserReadsPerAdsEpsWithoutTableUnitMultiplier);
Run("StatementParserReadsKlarnaHiddenStatementText", StatementParserReadsKlarnaHiddenStatementText);
Run("StatementParserScalesHiddenTextMillionsOfUsd", StatementParserScalesHiddenTextMillionsOfUsd);
Run("StatementParserRejectsSingleRowHiddenTextBlock", StatementParserRejectsSingleRowHiddenTextBlock);
Run("StructuredStatementTableBeatsHiddenStatementTextForSamePeriod", StructuredStatementTableBeatsHiddenStatementTextForSamePeriod);
Run("StatementParserReadsKlarnaThreeAndNineMonthHiddenStatementText", StatementParserReadsKlarnaThreeAndNineMonthHiddenStatementText);
Run("RevenueEndpointUsesKlarnaHiddenStatementTextWhenCompanyfactsIsAnnualOnly", RevenueEndpointUsesKlarnaHiddenStatementTextWhenCompanyfactsIsAnnualOnly);
Run("RevenueEndpointReconstructsKlarnaQ1ThroughQ4FromRecentFilingText", RevenueEndpointReconstructsKlarnaQ1ThroughQ4FromRecentFilingText);
Run("EdgarClientUsesRecentSixKFilingTablesForPerAdsEpsWhenCompanyfactsLacksQuarter", EdgarClientUsesRecentSixKFilingTablesForPerAdsEpsWhenCompanyfactsLacksQuarter);
Run("EdgarClientReadsPerAdsEpsWhenAnnualCompanyfactsAlreadyHasOrdinaryShareEps", EdgarClientReadsPerAdsEpsWhenAnnualCompanyfactsAlreadyHasOrdinaryShareEps);
Run("FinancialMetricsBuilderUsesMixedTaxonomiesPerRow", FinancialMetricsBuilderUsesMixedTaxonomiesPerRow);
Run("FinancialMetricsBuilderPrefersHigherPrecisionSameAccessionFact", FinancialMetricsBuilderPrefersHigherPrecisionSameAccessionFact);
Run("EdgarClientTreatsFilingFallbackAsOptionalWhenPackagesFail", EdgarClientTreatsFilingFallbackAsOptionalWhenPackagesFail);
Run("StatementParserRejectsAmbiguousTables", StatementParserRejectsAmbiguousTables);
Run("StatementParserPreservesPeriodSlotsWhenTableCellsAreBlank", StatementParserPreservesPeriodSlotsWhenTableCellsAreBlank);
Run("StatementParserPrefersPreciseTableOverRoundedSummary", StatementParserPrefersPreciseTableOverRoundedSummary);
Run("StatementParserUsesHeaderGroupsForMixedQuarterAnnualTables", StatementParserUsesHeaderGroupsForMixedQuarterAnnualTables);
Run("StatementParserKeepsCumulativeAndQuarterHeaderGroupsAligned", StatementParserKeepsCumulativeAndQuarterHeaderGroupsAligned);
Run("StatementParserReadsIxbrlInstantContextsAndAttributeOrder", StatementParserReadsIxbrlInstantContextsAndAttributeOrder);
Run("FinancialMetricsBuilderUsesFallbacksAndMetadata", FinancialMetricsBuilderUsesFallbacksAndMetadata);
Run("FinancialMetricsBuilderAggregatesMultiClassSharePointMetrics", FinancialMetricsBuilderAggregatesMultiClassSharePointMetrics);
Run("FinancialMetricsBuilderKeepsCompanyfactsAnnualRevenueOverFilingFallback", FinancialMetricsBuilderKeepsCompanyfactsAnnualRevenueOverFilingFallback);
Run("FinancialMetricsBuilderSkipsTtmWhenFlowQuartersHaveGaps", FinancialMetricsBuilderSkipsTtmWhenFlowQuartersHaveGaps);
Run("FinancialMetricsBuilderDoesNotCreateShareOnlyRows", FinancialMetricsBuilderDoesNotCreateShareOnlyRows);
Run("FinancialMetricsBuilderUsesPresentationSignsAndDirectTtmFacts", FinancialMetricsBuilderUsesPresentationSignsAndDirectTtmFacts);
Run("FinancialMetricsBuilderIncludesSecBackedLeaseDebtAndPpeComponents", FinancialMetricsBuilderIncludesSecBackedLeaseDebtAndPpeComponents);
Run("FinancialMetricsBuilderIncludesFinanceLeaseInterestInDebtRepayment", FinancialMetricsBuilderIncludesFinanceLeaseInterestInDebtRepayment);
Run("FinancialMetricsBuilderBlanksTotalDebtFromLongTermDebtOnly", FinancialMetricsBuilderBlanksTotalDebtFromLongTermDebtOnly);
Run("FinancialMetricsBuilderDoesNotDoubleCountBundledFinanceLeaseInTotalDebt", FinancialMetricsBuilderDoesNotDoubleCountBundledFinanceLeaseInTotalDebt);
Run("FinancialMetricsBuilderDoesNotDoubleCountBundledFinanceLeaseInCurrentDebt", FinancialMetricsBuilderDoesNotDoubleCountBundledFinanceLeaseInCurrentDebt);
Run("FinancialMetricsExportWritesCsvAndXlsx", FinancialMetricsExportWritesCsvAndXlsx);
Run("FinancialMetricsTraceReturnsAuditRows", FinancialMetricsTraceReturnsAuditRows);
Run("FinancialMetricsSnapshotsMatch", FinancialMetricsSnapshotsMatch);
Run("CachedSubmissionFixtureLoadsFromFile", CachedSubmissionFixtureLoadsFromFile);
Run("StatementParserNegatesLossOnlyTableLabels", StatementParserNegatesLossOnlyTableLabels);
Run("StatementParserDoesNotNegateIncomeQualifiedLossLabels", StatementParserDoesNotNegateIncomeQualifiedLossLabels);
Run("StatementParserDoesNotDoubleNegateAlreadyNegativeLossValues", StatementParserDoesNotDoubleNegateAlreadyNegativeLossValues);
Run("AccountsPayableIsTradePayablesOnly", AccountsPayableIsTradePayablesOnly);
Run("CashUsesCarryingValueNotBankDeposits", CashUsesCarryingValueNotBankDeposits);
Run("GrossProfitUsesNarrowCogsScopeForFinancials", GrossProfitUsesNarrowCogsScopeForFinancials);
Run("TotalDebtIncludesCurrentMaturitiesOfLongTermDebt", TotalDebtIncludesCurrentMaturitiesOfLongTermDebt);
Run("TotalDebtDoesNotDoubleCountCurrentMaturitiesUnderAllInLongTermDebt", TotalDebtDoesNotDoubleCountCurrentMaturitiesUnderAllInLongTermDebt);
Run("TotalDebtDoesNotDoubleCountCurrentMaturitiesUnderAggregateDebtCurrent", TotalDebtDoesNotDoubleCountCurrentMaturitiesUnderAggregateDebtCurrent);
Run("TotalDebtPrefersNoncurrentWhenAllInLongTermIsUnderscoped", TotalDebtPrefersNoncurrentWhenAllInLongTermIsUnderscoped);
Run("TotalDebtKeepsAllInLongTermWhenItExceedsNoncurrent", TotalDebtKeepsAllInLongTermWhenItExceedsNoncurrent);
Run("TotalDebtUsesAllInLongTermDebtAloneWhenNoCurrentDebt", TotalDebtUsesAllInLongTermDebtAloneWhenNoCurrentDebt);
Run("ShareFallbackKeepsWeightedAverageHistoryUnderCover", ShareFallbackKeepsWeightedAverageHistoryUnderCover);
Run("ShareFallbackReconcilesFullBaseButRejectsPartialAndLeak", ShareFallbackReconcilesFullBaseButRejectsPartialAndLeak);
Run("TotalDebtReadsDebtAndCapitalLeaseObligationsDirectTotal", TotalDebtReadsDebtAndCapitalLeaseObligationsDirectTotal);
Run("TotalDebtReadsCombinedLongShortDebtDirectTotal", TotalDebtReadsCombinedLongShortDebtDirectTotal);
Run("TotalDebtDoesNotDoubleCountBundledCapitalLease", TotalDebtDoesNotDoubleCountBundledCapitalLease);
Run("RevenueUsesOperatingLeaseIncomeWhenNoOtherRevenueConcept", RevenueUsesOperatingLeaseIncomeWhenNoOtherRevenueConcept);
Run("RevenuePrefersDominantOperatingLeaseIncomeOverTinyContractResidual", RevenuePrefersDominantOperatingLeaseIncomeOverTinyContractResidual);
Run("RevenueKeepsContractRevenueWhenLargerThanSubleaseIncome", RevenueKeepsContractRevenueWhenLargerThanSubleaseIncome);
Run("RevenueKeepsExplicitRevenuesTotalOverLeaseIncome", RevenueKeepsExplicitRevenuesTotalOverLeaseIncome);
Run("RevenueDropsStandaloneResidualWhenCumulativeLeaseDominatesEnd", RevenueDropsStandaloneResidualWhenCumulativeLeaseDominatesEnd);
Run("RevenueKeepsStandaloneContractWhenLeaseAtEndIsSmaller", RevenueKeepsStandaloneContractWhenLeaseAtEndIsSmaller);
Run("SelectReconcilePairPrefersOverlappingEnd", SelectReconcilePairPrefersOverlappingEnd);
Run("TtmBuilderSumsFourQuartersWhenFiscalMetadataIsPartial", TtmBuilderSumsFourQuartersWhenFiscalMetadataIsPartial);
foreach (var f in failures) Console.WriteLine("FAIL " + f);
Console.WriteLine($"{passed} passed, {failures.Count} failed");
if (failures.Count > 0) Environment.Exit(1);
Console.WriteLine("All tests passed.");

static string MissingFixtureMessage(string what) => $"Missing test fixture: {what}";
static FinancialMetricsExportService NewMetricsExportService(LegacyClient client) => new(client.Inner);


sealed class ChunkedReadStream(byte[] data, int chunkSize) : Stream
{
    private int _position;

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = data.Length - _position;
        if (remaining <= 0) return 0;
        var n = Math.Min(Math.Min(chunkSize, count), remaining);
        Array.Copy(data, _position, buffer, offset, n);
        _position += n;
        return n;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        Task.FromResult(Read(buffer, offset, count));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var remaining = data.Length - _position;
        if (remaining <= 0) return ValueTask.FromResult(0);
        var n = Math.Min(Math.Min(chunkSize, buffer.Length), remaining);
        data.AsSpan(_position, n).CopyTo(buffer.Span);
        _position += n;
        return ValueTask.FromResult(n);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

sealed class RejectingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException($"Unexpected SEC request during ticker lookup: {request.RequestUri}");
}
