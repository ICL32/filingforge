using FilingForge.Models;
using static FilingForge.FinancialMetricDerivations;

namespace FilingForge;

public static class FinancialMetricsBuilder
{

    public static FinancialMetricsBuildResult Build(string ticker, FinancialMetricInputs inputs)
    {
        var sym = ticker.ToUpperInvariant();
        var provider = FinancialMetricProviderNames.Name(inputs.Profile.Provider);
        var taxonomy = inputs.Profile.PrimaryTaxonomy;
        inputs = SnapFactsToStatementPeriodEnds(inputs);
        var quarterlyByMetric = BuildQuarterlyFlowMetrics(sym, inputs, out var metaByEnd, out var revenueByEnd);
        var annualByMetric = BuildAnnualFlowMetrics(inputs);
        return BuildResultFromFlow(sym, provider, taxonomy, inputs, quarterlyByMetric, annualByMetric, metaByEnd, revenueByEnd, FiscalCalendarFor(inputs));
    }

    // Quarterly derived flow metrics (revenue -> cogs -> gross profit -> EBIT chain, cash-flow rows,
    // etc.), shared by the full export Build and the analysis input path (see BuildFlowMetricSeries)
    // so a derived flow value such as gross profit cannot differ between the metrics export and the
    // fundamentals snapshot.
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> BuildQuarterlyFlowMetrics(
        string sym,
        FinancialMetricInputs inputs,
        out Dictionary<string, FinancialMetricQuarterEntry> metaByEnd,
        out Dictionary<string, RevenueQuarterPoint> revenueByEnd)
    {
        var revenueSeries = RevenueSeriesBuilder.Build(sym, inputs.RevenuePoints, factsAlreadyAligned: true);
        revenueByEnd = revenueSeries.Quarters.ToDictionary(q => q.End);
        metaByEnd = new Dictionary<string, FinancialMetricQuarterEntry>();

        var revenueEntries = BuildDirectQuarterlyEntries(inputs.RevenuePoints, FinancialMetricsCatalog.Revenue.CostCannotBeNegative, isRevenue: true);
        AddMeta(metaByEnd, revenueEntries);

        var cogsDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.Cogs, metaByEnd);
        var grossProfitDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.GrossProfit, metaByEnd);
        var sgaDirect = DirectQuarterly(inputs, SgaAggregateConcepts(), FinancialMetricsCatalog.Sga.CostCannotBeNegative, metaByEnd);
        var sellingAndMarketingExpense = DirectQuarterly(inputs, FinancialMetricsCatalog.SellingAndMarketingExpense, metaByEnd);
        var generalAndAdministrativeExpense = DirectQuarterly(inputs, FinancialMetricsCatalog.GeneralAndAdministrativeExpense, metaByEnd);
        var rnd = DirectQuarterly(inputs, FinancialMetricsCatalog.Rnd, metaByEnd);
        var totalOperatingCostDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.TotalOperatingCost, metaByEnd);
        var ebitdaDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.Ebitda, metaByEnd);
        var ebitDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.Ebit, metaByEnd);
        var interestIncome = DirectQuarterly(inputs, FinancialMetricsCatalog.InterestIncome, metaByEnd);
        var interestExpense = DirectQuarterly(inputs, FinancialMetricsCatalog.InterestExpense, metaByEnd);
        var netNonOperatingInterestDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.NetNonOperatingInterestIncomeExpense, metaByEnd);
        var netInterestIncomeDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.NetInterestIncome, metaByEnd);
        var otherIncomeExpense = DirectQuarterly(inputs, FinancialMetricsCatalog.OtherIncomeExpense, metaByEnd);
        var tax = DirectQuarterly(inputs, FinancialMetricsCatalog.Tax, metaByEnd);
        var ebtDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.Ebt, metaByEnd);
        var netIncome = DirectQuarterly(inputs, FinancialMetricsCatalog.NetIncome, metaByEnd);
        var netIncomeCommonStockholders = DirectQuarterly(inputs, FinancialMetricsCatalog.NetIncomeCommonStockholders, metaByEnd);
        var dilutedNiAvailableToCommonStockholders = DirectQuarterly(inputs, FinancialMetricsCatalog.DilutedNiAvailableToCommonStockholders, metaByEnd);
        var netIncomeContinuingOperations = DirectQuarterly(inputs, FinancialMetricsCatalog.NetIncomeContinuingOperations, metaByEnd);
        var basicEps = DirectQuarterly(inputs, FinancialMetricsCatalog.BasicEps, metaByEnd);
        var dilutedEps = DirectQuarterly(inputs, FinancialMetricsCatalog.DilutedEps, metaByEnd);
        var basicAverageShares = AverageShareQuarters(inputs, FinancialMetricsCatalog.BasicAverageShares, metaByEnd);
        var dilutedAverageShares = AverageShareQuarters(inputs, FinancialMetricsCatalog.DilutedAverageShares, metaByEnd);
        var operatingCashFlow = DirectQuarterly(inputs, FinancialMetricsCatalog.OperatingCashFlow, metaByEnd);
        var incomeTaxPaidSupplementalData = DirectQuarterly(inputs, FinancialMetricsCatalog.IncomeTaxPaidSupplementalData, metaByEnd);
        var interestPaidSupplementalData = DirectQuarterly(inputs, FinancialMetricsCatalog.InterestPaidSupplementalData, metaByEnd);
        var stockBasedCompensation = DirectQuarterly(inputs, FinancialMetricsCatalog.StockBasedCompensation, metaByEnd);
        var deferredTax = DirectQuarterly(inputs, FinancialMetricsCatalog.DeferredTax, metaByEnd);
        var changeInReceivables = InvertWorkingCapitalAssetSign(DirectQuarterly(inputs, FinancialMetricsCatalog.ChangeInReceivables, metaByEnd));
        var changeInInventory = InvertWorkingCapitalAssetSign(DirectQuarterly(inputs, FinancialMetricsCatalog.ChangeInInventory, metaByEnd));
        var changeInPayables = DirectQuarterly(inputs, FinancialMetricsCatalog.ChangeInPayables, metaByEnd);
        var changeInAccruedExpense = DirectQuarterly(inputs, FinancialMetricsCatalog.ChangeInAccruedExpense, metaByEnd);
        var changeInOtherWorkingCapital = DirectQuarterly(inputs, FinancialMetricsCatalog.ChangeInOtherWorkingCapital, metaByEnd);
        var investingCashFlow = DirectQuarterly(inputs, FinancialMetricsCatalog.InvestingCashFlow, metaByEnd);
        var purchaseOfPpe = DirectQuarterly(inputs, FinancialMetricsCatalog.PurchaseOfPpe, metaByEnd);
        var saleOfPpe = DirectQuarterly(inputs, FinancialMetricsCatalog.SaleOfPpe, metaByEnd);
        var purchaseOfBusiness = DirectQuarterly(inputs, FinancialMetricsCatalog.PurchaseOfBusiness, metaByEnd);
        var purchaseOfInvestment = DirectQuarterly(inputs, FinancialMetricsCatalog.PurchaseOfInvestment, metaByEnd);
        var saleOfInvestment = DirectQuarterly(inputs, FinancialMetricsCatalog.SaleOfInvestment, metaByEnd);
        var financingCashFlow = DirectQuarterly(inputs, FinancialMetricsCatalog.FinancingCashFlow, metaByEnd);
        var capex = DirectQuarterly(inputs, FinancialMetricsCatalog.Capex, metaByEnd);
        var issuanceOfCapitalStock = DirectQuarterly(inputs, FinancialMetricsCatalog.IssuanceOfCapitalStock, metaByEnd);
        var issuanceOfDebtDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.IssuanceOfDebt, metaByEnd);
        var repaymentOfDebtDirect = DirectQuarterly(inputs, FinancialMetricsCatalog.RepaymentOfDebt, metaByEnd);
        var longTermDebtIssuance = DirectQuarterly(inputs, FinancialMetricsCatalog.LongTermDebtIssuance, metaByEnd);
        var shortTermDebtIssuance = DirectQuarterly(inputs, FinancialMetricsCatalog.ShortTermDebtIssuance, metaByEnd);
        var longTermDebtPayments = DirectQuarterly(inputs, FinancialMetricsCatalog.LongTermDebtPayments, metaByEnd);
        var shortTermDebtPayments = DirectQuarterly(inputs, FinancialMetricsCatalog.ShortTermDebtPayments, metaByEnd);
        var financeLeasePrincipalPayments = DirectQuarterly(inputs, FinancialMetricsCatalog.FinanceLeasePrincipalPayments, metaByEnd);
        var financeLeaseInterestPayments = DirectQuarterly(inputs, FinanceLeaseInterestPaymentConcepts(), true, metaByEnd);
        var repurchaseOfCapitalStock = DirectQuarterly(inputs, FinancialMetricsCatalog.RepurchaseOfCapitalStock, metaByEnd);
        var changesInCash = DirectQuarterly(inputs, FinancialMetricsCatalog.ChangesInCash, metaByEnd);
        var effectOfExchangeRateChanges = DirectQuarterly(inputs, FinancialMetricsCatalog.EffectOfExchangeRateChanges, metaByEnd);
        var dividendsPaid = DirectQuarterly(inputs, FinancialMetricsCatalog.DividendsPaid, metaByEnd);
        var noncontrollingInterestIncome = DirectQuarterly(inputs, FinancialMetricsCatalog.NoncontrollingInterestIncome, metaByEnd);

        var operatingExpenses = DirectQuarterly(inputs, OperatingExpensesConcepts(), true, metaByEnd);
        var depreciationAmortization = DirectQuarterly(inputs, DepreciationAmortizationConcepts(), true, metaByEnd);

        var candidateEnds = SortedEnds(
            revenueEntries.Keys,
            cogsDirect.Keys,
            grossProfitDirect.Keys,
            sgaDirect.Keys,
            sellingAndMarketingExpense.Keys,
            generalAndAdministrativeExpense.Keys,
            rnd.Keys,
            totalOperatingCostDirect.Keys,
            ebitdaDirect.Keys,
            ebitDirect.Keys,
            interestIncome.Keys,
            interestExpense.Keys,
            netNonOperatingInterestDirect.Keys,
            netInterestIncomeDirect.Keys,
            otherIncomeExpense.Keys,
            tax.Keys,
            ebtDirect.Keys,
            netIncome.Keys,
            netIncomeCommonStockholders.Keys,
            dilutedNiAvailableToCommonStockholders.Keys,
            netIncomeContinuingOperations.Keys,
            basicEps.Keys,
            dilutedEps.Keys,
            basicAverageShares.Keys,
            dilutedAverageShares.Keys,
            operatingCashFlow.Keys,
            incomeTaxPaidSupplementalData.Keys,
            interestPaidSupplementalData.Keys,
            stockBasedCompensation.Keys,
            deferredTax.Keys,
            changeInReceivables.Keys,
            changeInInventory.Keys,
            changeInPayables.Keys,
            changeInAccruedExpense.Keys,
            changeInOtherWorkingCapital.Keys,
            investingCashFlow.Keys,
            purchaseOfPpe.Keys,
            saleOfPpe.Keys,
            purchaseOfBusiness.Keys,
            purchaseOfInvestment.Keys,
            saleOfInvestment.Keys,
            financingCashFlow.Keys,
            capex.Keys,
            issuanceOfCapitalStock.Keys,
            issuanceOfDebtDirect.Keys,
            repaymentOfDebtDirect.Keys,
            longTermDebtIssuance.Keys,
            shortTermDebtIssuance.Keys,
            longTermDebtPayments.Keys,
            shortTermDebtPayments.Keys,
            financeLeasePrincipalPayments.Keys,
            financeLeaseInterestPayments.Keys,
            repurchaseOfCapitalStock.Keys,
            changesInCash.Keys,
            effectOfExchangeRateChanges.Keys,
            dividendsPaid.Keys,
            noncontrollingInterestIncome.Keys,
            operatingExpenses.Keys,
            depreciationAmortization.Keys
        );

        var revenue = ToValues(revenueEntries);
        var cogs = Resolve(candidateEnds, end =>
            Existing(cogsDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusGrossProfit, true, revenue, grossProfitDirect, end)
            ?? DerivedSubtract3(FinancialMetricFormulaIds.RevenueMinusEbitMinusOperatingExpenses, true, revenue, ebitDirect, operatingExpenses, end)
        );

        var grossProfit = Resolve(candidateEnds, end =>
            RevenueLessSummedCost(revenue, cogs, end)
            ?? Existing(grossProfitDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusCogs, false, revenue, cogs, end)
        );

        var totalOperatingCost = Resolve(candidateEnds, end =>
            Existing(totalOperatingCostDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusEbit, true, revenue, ebitDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.CogsPlusOperatingExpenses, true, cogs, operatingExpenses, end)
        );

        var sga = Resolve(candidateEnds, end =>
            Existing(sgaDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.SellingMarketingPlusGeneralAdministrative, true, sellingAndMarketingExpense, generalAndAdministrativeExpense, end)
            ?? DerivedIdentity(FinancialMetricFormulaIds.GeneralAndAdministrativeExpenseOnly, generalAndAdministrativeExpense, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.OperatingExpensesMinusRnd, true, operatingExpenses, rnd, end)
            ?? DerivedSubtract3(FinancialMetricFormulaIds.TotalOperatingCostMinusCogsMinusRnd, true, totalOperatingCost, cogs, rnd, end)
        );

        var ebit = Resolve(candidateEnds, end =>
            Existing(ebitDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusTotalOperatingCost, false, revenue, totalOperatingCost, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.GrossProfitMinusOperatingExpenses, false, grossProfit, operatingExpenses, end)
        );

        var ebitda = Resolve(candidateEnds, end =>
            Existing(ebitdaDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.EbitPlusDepreciationAmortization, false, ebit, depreciationAmortization, end)
        );

        var netNonOperatingInterestIncomeExpense = Resolve(candidateEnds, end =>
            Existing(netNonOperatingInterestDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.InterestIncomeMinusInterestExpense, false, interestIncome, interestExpense, end)
        );

        var netInterestIncome = Resolve(candidateEnds, end =>
            Existing(netInterestIncomeDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.InterestIncomeMinusInterestExpense, false, interestIncome, interestExpense, end)
        );

        var ebt = Resolve(candidateEnds, end =>
            Existing(ebtDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.NetIncomePlusTax, false, netIncome, tax, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.EbitMinusInterestExpense, false, ebit, interestExpense, end)
        );

        var taxRateForCalcs = Resolve(candidateEnds, end =>
            DerivedPercentDivide(FinancialMetricFormulaIds.TaxDividedByPretaxIncome, tax, ebt, end)
        );

        var freeCashFlow = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.OperatingCashFlowMinusCapex, false, operatingCashFlow, capex, end)
        );

        var issuanceOfDebt = Resolve(candidateEnds, end =>
            Existing(issuanceOfDebtDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.DebtIssuanceLongTermPlusShortTerm, true, longTermDebtIssuance, shortTermDebtIssuance, end)
        );

        var repaymentOfDebt = Resolve(candidateEnds, end =>
            Existing(repaymentOfDebtDirect, end)
            ?? DerivedAdd4(FinancialMetricFormulaIds.DebtRepaymentComponentsWithFinanceLeaseInterest, true, longTermDebtPayments, shortTermDebtPayments, financeLeasePrincipalPayments, financeLeaseInterestPayments, end)
            ?? DerivedAdd3(FinancialMetricFormulaIds.DebtRepaymentComponents, true, longTermDebtPayments, shortTermDebtPayments, financeLeasePrincipalPayments, end)
        );

        var netIssuancePaymentsOfDebt = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.DebtIssuanceMinusRepayment, false, issuanceOfDebt, repaymentOfDebt, end)
        );

        var netLongTermDebtIssuance = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.LongTermDebtIssuanceMinusRepayment, false, longTermDebtIssuance, longTermDebtPayments, end)
        );

        var netShortTermDebtIssuance = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.ShortTermDebtIssuanceMinusRepayment, false, shortTermDebtIssuance, shortTermDebtPayments, end)
        );

        var quarterlyByMetric = new Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>>
        {
            [FinancialMetricsCatalog.Revenue.Id] = revenue,
            [FinancialMetricsCatalog.Cogs.Id] = cogs,
            [FinancialMetricsCatalog.GrossProfit.Id] = grossProfit,
            [FinancialMetricsCatalog.Sga.Id] = sga,
            [FinancialMetricsCatalog.SellingAndMarketingExpense.Id] = sellingAndMarketingExpense,
            [FinancialMetricsCatalog.GeneralAndAdministrativeExpense.Id] = generalAndAdministrativeExpense,
            [FinancialMetricsCatalog.Rnd.Id] = rnd,
            [FinancialMetricsCatalog.TotalOperatingCost.Id] = totalOperatingCost,
            [FinancialMetricsCatalog.OperatingExpensesMetric.Id] = operatingExpenses,
            [FinancialMetricsCatalog.Ebitda.Id] = ebitda,
            [FinancialMetricsCatalog.Ebit.Id] = ebit,
            [FinancialMetricsCatalog.OperatingIncomeAsReported.Id] = ebitDirect,
            [FinancialMetricsCatalog.InterestIncome.Id] = interestIncome,
            [FinancialMetricsCatalog.InterestExpense.Id] = interestExpense,
            [FinancialMetricsCatalog.NetNonOperatingInterestIncomeExpense.Id] = netNonOperatingInterestIncomeExpense,
            [FinancialMetricsCatalog.NetInterestIncome.Id] = netInterestIncome,
            [FinancialMetricsCatalog.OtherIncomeExpense.Id] = otherIncomeExpense,
            [FinancialMetricsCatalog.Tax.Id] = tax,
            [FinancialMetricsCatalog.Ebt.Id] = ebt,
            [FinancialMetricsCatalog.TaxRateForCalcs.Id] = taxRateForCalcs,
            [FinancialMetricsCatalog.NetIncome.Id] = netIncome,
            [FinancialMetricsCatalog.NetIncomeCommonStockholders.Id] = netIncomeCommonStockholders,
            [FinancialMetricsCatalog.DilutedNiAvailableToCommonStockholders.Id] = dilutedNiAvailableToCommonStockholders,
            [FinancialMetricsCatalog.NetIncomeContinuingOperations.Id] = netIncomeContinuingOperations,
            [FinancialMetricsCatalog.BasicEps.Id] = basicEps,
            [FinancialMetricsCatalog.DilutedEps.Id] = dilutedEps,
            [FinancialMetricsCatalog.BasicAverageShares.Id] = basicAverageShares,
            [FinancialMetricsCatalog.DilutedAverageShares.Id] = dilutedAverageShares,
            [FinancialMetricsCatalog.ReconciledDepreciation.Id] = depreciationAmortization,
            [FinancialMetricsCatalog.OperatingCashFlow.Id] = operatingCashFlow,
            [FinancialMetricsCatalog.IncomeTaxPaidSupplementalData.Id] = incomeTaxPaidSupplementalData,
            [FinancialMetricsCatalog.InterestPaidSupplementalData.Id] = interestPaidSupplementalData,
            [FinancialMetricsCatalog.StockBasedCompensation.Id] = stockBasedCompensation,
            [FinancialMetricsCatalog.DeferredTax.Id] = deferredTax,
            [FinancialMetricsCatalog.ChangeInReceivables.Id] = changeInReceivables,
            [FinancialMetricsCatalog.ChangeInInventory.Id] = changeInInventory,
            [FinancialMetricsCatalog.ChangeInPayables.Id] = changeInPayables,
            [FinancialMetricsCatalog.ChangeInAccruedExpense.Id] = changeInAccruedExpense,
            [FinancialMetricsCatalog.ChangeInOtherWorkingCapital.Id] = changeInOtherWorkingCapital,
            [FinancialMetricsCatalog.InvestingCashFlow.Id] = investingCashFlow,
            [FinancialMetricsCatalog.PurchaseOfPpe.Id] = purchaseOfPpe,
            [FinancialMetricsCatalog.SaleOfPpe.Id] = saleOfPpe,
            [FinancialMetricsCatalog.PurchaseOfBusiness.Id] = purchaseOfBusiness,
            [FinancialMetricsCatalog.PurchaseOfInvestment.Id] = purchaseOfInvestment,
            [FinancialMetricsCatalog.SaleOfInvestment.Id] = saleOfInvestment,
            [FinancialMetricsCatalog.FinancingCashFlow.Id] = financingCashFlow,
            [FinancialMetricsCatalog.Capex.Id] = capex,
            [FinancialMetricsCatalog.FreeCashFlow.Id] = freeCashFlow,
            [FinancialMetricsCatalog.IssuanceOfCapitalStock.Id] = issuanceOfCapitalStock,
            [FinancialMetricsCatalog.IssuanceOfDebt.Id] = issuanceOfDebt,
            [FinancialMetricsCatalog.RepaymentOfDebt.Id] = repaymentOfDebt,
            [FinancialMetricsCatalog.LongTermDebtIssuance.Id] = longTermDebtIssuance,
            [FinancialMetricsCatalog.ShortTermDebtIssuance.Id] = shortTermDebtIssuance,
            [FinancialMetricsCatalog.LongTermDebtPayments.Id] = longTermDebtPayments,
            [FinancialMetricsCatalog.ShortTermDebtPayments.Id] = shortTermDebtPayments,
            [FinancialMetricsCatalog.FinanceLeasePrincipalPayments.Id] = financeLeasePrincipalPayments,
            [FinancialMetricsCatalog.NetIssuancePaymentsOfDebt.Id] = netIssuancePaymentsOfDebt,
            [FinancialMetricsCatalog.NetLongTermDebtIssuance.Id] = netLongTermDebtIssuance,
            [FinancialMetricsCatalog.NetShortTermDebtIssuance.Id] = netShortTermDebtIssuance,
            [FinancialMetricsCatalog.RepurchaseOfCapitalStock.Id] = repurchaseOfCapitalStock,
            [FinancialMetricsCatalog.ChangesInCash.Id] = changesInCash,
            [FinancialMetricsCatalog.EffectOfExchangeRateChanges.Id] = effectOfExchangeRateChanges,
            [FinancialMetricsCatalog.DividendsPaid.Id] = dividendsPaid,
            [FinancialMetricsCatalog.NoncontrollingInterestIncome.Id] = noncontrollingInterestIncome
        };

        AddPerShare(quarterlyByMetric);
        return quarterlyByMetric;
    }

    // Flow-only metric series for the analysis input path. Builds the same quarterly + annual derived
    // flow metrics as the export Build, but skips the point-in-time, full-TTM, audit, and row assembly
    // the export needs. FundamentalsBuilder uses this to source derived gross profit / EBIT for the
    // fundamentals margin numerators without depending on the full export build or its 238-metric rows.
    public static FlowMetricSeries BuildFlowMetricSeries(string ticker, FinancialMetricInputs inputs)
    {
        var sym = ticker.ToUpperInvariant();
        inputs = SnapFactsToStatementPeriodEnds(inputs);
        var quarterly = BuildQuarterlyFlowMetrics(sym, inputs, out var metaByEnd, out var revenueByEnd);
        var annual = BuildAnnualFlowMetrics(inputs);
        return new FlowMetricSeries(quarterly, annual, metaByEnd, revenueByEnd, FiscalCalendarFor(inputs));
    }

    private static FinancialMetricsBuildResult BuildResultFromFlow(
        string sym,
        string provider,
        string taxonomy,
        FinancialMetricInputs inputs,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> quarterlyByMetric,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> annualByMetric,
        Dictionary<string, FinancialMetricQuarterEntry> metaByEnd,
        Dictionary<string, RevenueQuarterPoint> revenueByEnd,
        FiscalCalendar calendar)
    {
        var ttmByMetric = quarterlyByMetric.ToDictionary(
            kv => kv.Key,
            kv => TtmView(FinancialMetricsCatalog.Definition(kv.Key), kv.Value, metaByEnd));
        // Metrics flagged DirectTtmEligible may take an exact SEC 12-month duration fact as their TTM
        // when no strict four-quarter window exists. Driven off the catalog flag so adding such a
        // metric is a one-line catalog change, not an edit here.
        foreach (var metric in FinancialMetricsCatalog.FlowMetrics.Where(m => m.DirectTtmEligible))
            SupplementDirectTtmDurationValues(ttmByMetric, inputs, metric);
        SupplementRepaymentOfDebtTtmWithFinanceLeaseInterest(ttmByMetric, inputs);

        ttmByMetric[FinancialMetricsCatalog.TaxRateForCalcs.Id] = Resolve(
            SortedEnds(ttmByMetric[FinancialMetricsCatalog.Tax.Id].Keys, ttmByMetric[FinancialMetricsCatalog.Ebt.Id].Keys),
            end => DerivedPercentDivide(
                FinancialMetricFormulaIds.TaxDividedByPretaxIncome,
                ttmByMetric[FinancialMetricsCatalog.Tax.Id],
                ttmByMetric[FinancialMetricsCatalog.Ebt.Id],
                end)
        );
        AddPerShare(ttmByMetric);

        var pointByMetric = BuildPointMetrics(inputs);
        var statementPointEnds = pointByMetric
            .Where(kv => !IsShareOnlyPointMetric(kv.Key))
            .SelectMany(kv => kv.Value.Keys);
        var rowEnds = SortedEnds(
            quarterlyByMetric.Values.SelectMany(m => m.Keys),
            annualByMetric.Values.SelectMany(m => m.Keys),
            ttmByMetric.Values.SelectMany(m => m.Keys),
            statementPointEnds
        );

        var rows = new List<FinancialMetricPeriodRow>(rowEnds.Count);
        foreach (var end in rowEnds)
        {
            var revenueRow = revenueByEnd.GetValueOrDefault(end);
            var (fy, fq, label) = FiscalLabel(end, revenueRow?.FiscalYear, revenueRow?.FiscalQuarter, metaByEnd.GetValueOrDefault(end), calendar);
            IReadOnlyList<string> validationMessages = revenueRow?.ValidationMessages ?? Array.Empty<string>();
            var validationStatus = revenueRow?.ValidationStatus ?? "ok";

            var quarterly = FinancialMetricsCatalog.FlowMetrics.ToDictionary(
                m => m.Id,
                m => GetOrBlank(quarterlyByMetric[m.Id], end)
            );
            var annual = FinancialMetricsCatalog.FlowMetrics.ToDictionary(
                m => m.Id,
                m => GetOrBlank(annualByMetric[m.Id], end)
            );
            var ttm = FinancialMetricsCatalog.FlowMetrics.ToDictionary(
                m => m.Id,
                m => GetOrBlank(ttmByMetric[m.Id], end)
            );
            var point = FinancialMetricsCatalog.PointMetrics.ToDictionary(
                m => m.Id,
                m => GetOrBlank(pointByMetric[m.Id], end)
            );

            rows.Add(new FinancialMetricPeriodRow(
                sym,
                end,
                fy,
                fq,
                label,
                provider,
                taxonomy,
                validationStatus,
                validationMessages,
                revenueRow?.Filed ?? LatestFiled(quarterly.Values.Concat(annual.Values).Concat(ttm.Values).Concat(point.Values)),
                quarterly,
                annual,
                ttm,
                point
            ));
        }

        var auditRows = BuildAuditRows(rows);
        return new FinancialMetricsBuildResult(rows, auditRows);
    }

    public static FinancialMetricPeriodRow ErrorRow(string ticker, string message)
    {
        return new FinancialMetricPeriodRow(
            ticker.ToUpperInvariant(),
            "",
            null,
            null,
            "",
            FinancialMetricProviderNames.Unknown,
            FinancialMetricProviderNames.Unknown,
            "error",
            [message],
            "",
            FinancialMetricsCatalog.FlowMetrics.ToDictionary(m => m.Id, _ => FinancialMetricValue.Blank()),
            FinancialMetricsCatalog.FlowMetrics.ToDictionary(m => m.Id, _ => FinancialMetricValue.Blank()),
            FinancialMetricsCatalog.FlowMetrics.ToDictionary(m => m.Id, _ => FinancialMetricValue.Blank()),
            FinancialMetricsCatalog.PointMetrics.ToDictionary(m => m.Id, _ => FinancialMetricValue.Blank())
        );
    }

    // Quarterly + annual derived flow metrics produced once, with per-metric FinancialPoint extraction
    // for the analysis input path. Holds the same dictionaries the export Build assembles, so callers
    // get identical derived values without rebuilding point/TTM/audit data.
    public sealed class FlowMetricSeries
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> _quarterly;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> _annual;
        private readonly IReadOnlyDictionary<string, FinancialMetricQuarterEntry> _metaByEnd;
        private readonly IReadOnlyDictionary<string, RevenueQuarterPoint> _revenueByEnd;
        private readonly FiscalCalendar _calendar;

        internal FlowMetricSeries(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> quarterly,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> annual,
            IReadOnlyDictionary<string, FinancialMetricQuarterEntry> metaByEnd,
            IReadOnlyDictionary<string, RevenueQuarterPoint> revenueByEnd,
            FiscalCalendar calendar)
        {
            _quarterly = quarterly;
            _annual = annual;
            _metaByEnd = metaByEnd;
            _revenueByEnd = revenueByEnd;
            _calendar = calendar;
        }

        // Quarterly (10-Q) then annual (10-K) FinancialPoints for one flow metric, sorted by period
        // end then filed date — the shape QuarterlySeriesBuilder expects for TTM reconstruction.
        public List<FinancialPoint> Points(FinancialMetricDefinition metric) =>
            FlowPointsForMetric(metric, _quarterly, _annual, _metaByEnd, _revenueByEnd, _calendar);
    }

    private static List<FinancialPoint> FlowPointsForMetric(
        FinancialMetricDefinition metric,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> quarterly,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> annual,
        IReadOnlyDictionary<string, FinancialMetricQuarterEntry> metaByEnd,
        IReadOnlyDictionary<string, RevenueQuarterPoint> revenueByEnd,
        FiscalCalendar calendar)
    {
        var result = new List<FinancialPoint>();

        if (quarterly.TryGetValue(metric.Id, out var quarterlyValues))
            foreach (var (end, value) in quarterlyValues)
                if (value.HasValue)
                    result.Add(ToFlowPoint(end, value, isAnnual: false, metaByEnd, revenueByEnd, calendar));

        if (annual.TryGetValue(metric.Id, out var annualValues))
            foreach (var (end, value) in annualValues)
                if (value.HasValue)
                    result.Add(ToFlowPoint(end, value, isAnnual: true, metaByEnd, revenueByEnd, calendar));

        return result
            .OrderBy(p => p.End, StringComparer.Ordinal)
            .ThenBy(p => p.Filed, StringComparer.Ordinal)
            .ToList();
    }

    private static FinancialPoint ToFlowPoint(
        string end,
        FinancialMetricValue value,
        bool isAnnual,
        IReadOnlyDictionary<string, FinancialMetricQuarterEntry> metaByEnd,
        IReadOnlyDictionary<string, RevenueQuarterPoint> revenueByEnd,
        FiscalCalendar calendar)
    {
        var revenueRow = revenueByEnd.GetValueOrDefault(end);
        var (fy, fq, _) = FiscalLabel(end, revenueRow?.FiscalYear, revenueRow?.FiscalQuarter, metaByEnd.GetValueOrDefault(end), calendar);
        return new FinancialPoint(
            Start: null,
            End: end,
            Val: value.Value!.Value,
            Form: isAnnual ? "10-K" : "10-Q",
            Filed: value.Filed ?? revenueRow?.Filed ?? "",
            Concept: string.IsNullOrWhiteSpace(value.SourceConcepts) ? value.Formula : value.SourceConcepts,
            Fy: fy,
            Fp: isAnnual ? "FY" : fq.HasValue ? $"Q{fq.Value}" : null,
            Accn: value.Accession,
            SourceType: value.SourceType,
            SourceDocument: value.SourceDocument,
            SourceLabel: value.SourceLabel);
    }

    private static IReadOnlyList<FinancialMetricAuditRow> BuildAuditRows(IReadOnlyList<FinancialMetricPeriodRow> rows)
    {
        var result = new List<FinancialMetricAuditRow>(
            rows.Count * (FinancialMetricsCatalog.FlowMetrics.Count * 3 + FinancialMetricsCatalog.PointMetrics.Count)
        );

        foreach (var row in rows)
        {
            foreach (var metric in FinancialMetricsCatalog.FlowMetrics)
            {
                result.Add(Audit(row, metric, "quarterly", row.Quarterly[metric.Id]));
                result.Add(Audit(row, metric, "annual", row.Annual[metric.Id]));
                result.Add(Audit(row, metric, "ttm", row.Ttm[metric.Id]));
            }
            foreach (var metric in FinancialMetricsCatalog.PointMetrics)
                result.Add(Audit(row, metric, "point", row.Point[metric.Id]));
        }

        return result;
    }

    private static FinancialMetricAuditRow Audit(
        FinancialMetricPeriodRow row,
        FinancialMetricDefinition metric,
        string valueType,
        FinancialMetricValue value) =>
        new(
            row.Ticker,
            row.PeriodEnd,
            metric.Id,
            metric.DisplayName,
            valueType,
            row.Provider,
            row.Taxonomy,
            value.Value,
            value.Formula,
            value.SourceConcepts,
            value.Filed,
            value.Accession,
            value.Derivation,
            value.SourceType,
            value.SourceDocument,
            value.SourceLabel,
            row.ValidationStatus,
            string.Join("|", row.ValidationMessages),
            value.Inputs
        );

    private static IReadOnlyDictionary<string, FinancialMetricValue> BuildPointMetricsValue(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric) =>
        BuildDirectPointValues(
            IsSharePointMetric(metric)
                ? MergeSharePointFacts(inputs, DirectConcepts(metric))
                : MergePointFacts(inputs, DirectConcepts(metric)),
            metric.CostCannotBeNegative);

    private static Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> BuildPointMetrics(FinancialMetricInputs inputs)
    {
        var directByMetric = FinancialMetricsCatalog.PointMetrics.ToDictionary(
            m => m.Id,
            m => BuildPointMetricsValue(inputs, m)
        );
        var totalAssets = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.TotalAssets);
        var totalLiabilitiesDirect = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.TotalLiabilities);
        var totalEquity = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.TotalEquityGrossMinorityInterest);
        var commonStockEquity = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.CommonStockEquity);
        var cashDirect = BuildDirectPointValues(inputs, CashCombinedConcepts(), true);
        var cashComponent = directByMetric[FinancialMetricsCatalog.CashAndCashEquivalents.Id];
        var shortTermInvestments = BuildDirectPointValues(inputs, ShortTermInvestmentsConcepts(), true);
        var debtDirect = BuildDirectPointValues(inputs, DebtDirectConcepts(), true);
        var debtCurrent = BuildDirectPointValues(inputs, DebtCurrentConcepts(), true);
        var currentMaturities = BuildDirectPointValues(inputs, LongTermDebtCurrentConcepts(), true);
        var longTermDebt = BuildDirectPointValues(inputs, LongTermDebtConcepts(), true);
        var longTermDebtForTotalDebt = BuildDirectPointValues(inputs, LongTermDebtForTotalDebtConcepts(), true);
        var currentAssets = BuildDirectPointValues(inputs, CurrentAssetsConcepts(), true);
        var currentLiabilities = BuildDirectPointValues(inputs, CurrentLiabilitiesConcepts(), true);
        var receivables = directByMetric[FinancialMetricsCatalog.Receivables.Id];
        var allowanceForDoubtfulAccounts = directByMetric[FinancialMetricsCatalog.AllowanceForDoubtfulAccounts.Id];
        var accountsPayable = directByMetric[FinancialMetricsCatalog.AccountsPayable.Id];
        var currentAccruedExpense = directByMetric[FinancialMetricsCatalog.CurrentAccruedExpense.Id];
        var goodwillAndIntangibleAssets = BuildDirectPointValues(inputs, GoodwillAndIntangibleAssetsConcepts(), true);
        var goodwill = BuildDirectPointValues(inputs, GoodwillConcepts(), true);
        var intangibleAssets = BuildDirectPointValues(inputs, IntangibleAssetsConcepts(), true);
        var capitalLeaseDirect = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.CapitalLeaseObligations);
        var capitalLeaseCurrent = BuildDirectPointValues(inputs, CapitalLeaseCurrentConcepts(), true);
        var capitalLeaseNoncurrent = BuildDirectPointValues(inputs, CapitalLeaseNoncurrentConcepts(), true);
        var operatingLeaseRightOfUseAsset = BuildDirectPointValues(inputs, OperatingLeaseRightOfUseAssetConcepts(), true);
        var endCashPosition = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.EndCashPosition);
        var shares = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.SharesOutstanding);
        var shareIssued = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.ShareIssued);
        var ordinarySharesNumber = BuildPointMetricsValue(inputs, FinancialMetricsCatalog.OrdinarySharesNumber);

        var pointEnds = SortedEnds(
            directByMetric.Values.SelectMany(m => m.Keys),
            totalAssets.Keys,
            totalLiabilitiesDirect.Keys,
            totalEquity.Keys,
            commonStockEquity.Keys,
            cashDirect.Keys,
            cashComponent.Keys,
            shortTermInvestments.Keys,
            debtDirect.Keys,
            debtCurrent.Keys,
            currentMaturities.Keys,
            longTermDebt.Keys,
            longTermDebtForTotalDebt.Keys,
            currentAssets.Keys,
            currentLiabilities.Keys,
            goodwillAndIntangibleAssets.Keys,
            goodwill.Keys,
            intangibleAssets.Keys,
            capitalLeaseDirect.Keys,
            capitalLeaseCurrent.Keys,
            capitalLeaseNoncurrent.Keys,
            operatingLeaseRightOfUseAsset.Keys,
            endCashPosition.Keys,
            shares.Keys,
            shareIssued.Keys,
            ordinarySharesNumber.Keys
        );

        var cash = Resolve(pointEnds, end =>
            Existing(cashDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.CashPlusShortTermInvestments, true, cashComponent, shortTermInvestments, end)
            ?? Existing(cashComponent, end)
        );

        var totalLiabilities = Resolve(pointEnds, end =>
            Existing(totalLiabilitiesDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.TotalAssetsMinusTotalEquity, true, totalAssets, totalEquity, end)
        );

        var otherShortTermInvestments = Resolve(pointEnds, end =>
            Existing(shortTermInvestments, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.CashAndShortTermMinusCash, true, cash, cashComponent, end)
        );

        var totalNonCurrentAssets = Resolve(pointEnds, end =>
            Existing(directByMetric[FinancialMetricsCatalog.TotalNonCurrentAssets.Id], end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.TotalAssetsMinusCurrentAssets, true, totalAssets, currentAssets, end)
        );

        var grossAccountsReceivable = Resolve(pointEnds, end =>
            Existing(directByMetric[FinancialMetricsCatalog.GrossAccountsReceivable.Id], end)
            ?? DerivedAdd(FinancialMetricFormulaIds.NetReceivablesPlusAllowance, true, receivables, allowanceForDoubtfulAccounts, end)
        );

        var payablesAndAccruedExpenses = Resolve(pointEnds, end =>
            Existing(directByMetric[FinancialMetricsCatalog.PayablesAndAccruedExpenses.Id], end)
            ?? DerivedAdd(FinancialMetricFormulaIds.AccountsPayablePlusAccruedLiabilities, true, accountsPayable, currentAccruedExpense, end)
        );

        var totalNonCurrentLiabilities = Resolve(pointEnds, end =>
            Existing(directByMetric[FinancialMetricsCatalog.TotalNonCurrentLiabilities.Id], end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.LiabilitiesMinusCurrentLiabilities, true, totalLiabilities, currentLiabilities, end)
        );

        var longTermDebtAndCapitalLease = Resolve(pointEnds, end =>
            Existing(directByMetric[FinancialMetricsCatalog.LongTermDebtAndCapitalLease.Id], end)
            ?? DerivedAdd(FinancialMetricFormulaIds.LongTermDebtPlusCapitalLeaseNoncurrent, true, longTermDebt, capitalLeaseNoncurrent, end)
            ?? Existing(longTermDebt, end)
        );

        var workingCapital = Resolve(pointEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.CurrentAssetsMinusCurrentLiabilities, false, currentAssets, currentLiabilities, end)
        );

        var tangibleAssets = Resolve(pointEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.CommonEquityMinusGoodwillAndIntangibles, false, commonStockEquity, goodwillAndIntangibleAssets, end)
            ?? DerivedSubtract3(FinancialMetricFormulaIds.CommonEquityMinusGoodwillAndIntangibles, false, commonStockEquity, goodwill, intangibleAssets, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.CommonEquityMinusGoodwillAndIntangibles, false, commonStockEquity, goodwill, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.CommonEquityMinusGoodwillAndIntangibles, false, commonStockEquity, intangibleAssets, end)
        );

        var capitalLeaseObligations = Resolve(pointEnds, end =>
            Existing(capitalLeaseDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.CapitalLeaseCurrentPlusNoncurrent, true, capitalLeaseCurrent, capitalLeaseNoncurrent, end)
            ?? Existing(capitalLeaseNoncurrent, end)
        );
        var capitalLeaseForTotalDebt = inputs.Profile.Provider == FinancialMetricProvider.IfrsFull
            ? new Dictionary<string, FinancialMetricValue>()
            : capitalLeaseObligations;

        // Long-term component for total debt. A filer may tag us-gaap:LongTermDebt (nominally the
        // all-in current + noncurrent aggregate) with a narrow value — only current maturities or a
        // single tranche — while carrying the bulk under LongTermDebtNoncurrent. The all-in figure
        // must be >= its noncurrent portion; when it is strictly smaller it is not the aggregate, so
        // prefer the noncurrent-based figure (which then lets the current-maturities fold-in below
        // apply) instead of understating total debt to the tiny LongTermDebt value.
        var longTermDebtForTotal = Resolve(pointEnds, end =>
        {
            var forTotal = Existing(longTermDebtForTotalDebt, end);
            var noncurrentBased = Existing(longTermDebt, end);
            if (forTotal is { Value: { } forTotalValue }
                && noncurrentBased is { Value: { } noncurrentValue }
                && FinancialMetricsCatalog.CurrentMaturitiesBundledConcepts.Contains(FirstConcept(forTotal))
                && !FinancialMetricsCatalog.CurrentMaturitiesBundledConcepts.Contains(FirstConcept(noncurrentBased))
                && forTotalValue < noncurrentValue)
            {
                return noncurrentBased;
            }
            return forTotal;
        });

        // Current-debt component for total debt: fold a separately reported current maturities of
        // long-term debt (us-gaap:LongTermDebtCurrent) into the short-term component, but only when
        // neither the chosen current concept nor the chosen long-term concept already includes it
        // (CurrentMaturitiesBundledConcepts) — avoids the double count the debt concept lists warn
        // about (e.g. all-in LongTermDebt already carries current maturities).
        var debtCurrentForTotal = Resolve(pointEnds, end =>
        {
            var currentAlreadyBundled = Existing(debtCurrent, end) is { } current
                && FinancialMetricsCatalog.CurrentMaturitiesBundledConcepts.Contains(FirstConcept(current));
            var longTermAllIn = Existing(longTermDebtForTotal, end) is { } longTerm
                && FinancialMetricsCatalog.CurrentMaturitiesBundledConcepts.Contains(FirstConcept(longTerm));

            if (currentAlreadyBundled || longTermAllIn)
                return Existing(debtCurrent, end);

            return DerivedAdd(FinancialMetricFormulaIds.DebtCurrentPlusCurrentMaturities, true, debtCurrent, currentMaturities, end)
                ?? Existing(debtCurrent, end)
                ?? Existing(currentMaturities, end);
        });

        var debt = Resolve(pointEnds, end =>
        {
            if (Existing(debtDirect, end) is { } direct)
                return direct;

            // Add the separate finance-lease term only when neither chosen debt component already
            // bundles finance leases (declared in FinancialMetricsCatalog.FinanceLeaseBundledConcepts),
            // so a bundled long-term or current debt concept is not double counted.
            var addFinanceLease = !BundlesFinanceLease(Existing(longTermDebtForTotal, end))
                && !BundlesFinanceLease(Existing(debtCurrentForTotal, end));

            if (addFinanceLease)
            {
                if (DerivedAdd3(FinancialMetricFormulaIds.DebtCurrentPlusLongTermDebtPlusFinanceLease, true, debtCurrentForTotal, longTermDebtForTotal, capitalLeaseForTotalDebt, end) is { } withCurrent)
                    return withCurrent;
                if (DerivedAdd(FinancialMetricFormulaIds.DebtLongTermPlusFinanceLease, true, longTermDebtForTotal, capitalLeaseForTotalDebt, end) is { } longTermPlusLease)
                    return longTermPlusLease;
            }

            if (DerivedAdd(FinancialMetricFormulaIds.DebtCurrentPlusLongTermDebt, true, debtCurrentForTotal, longTermDebtForTotal, end) is { } currentPlusLong)
                return currentPlusLong;

            // No separate current-debt concept: an all-in long-term concept (LongTermDebt /
            // LongTermDebtAndFinanceLeaseObligations, in CurrentMaturitiesBundledConcepts) already
            // includes current maturities, so it is total debt on its own — the common shape for REITs
            // that report a single debt line (e.g. CPT). A noncurrent-only concept still blanks because
            // its current portion is genuinely missing.
            if (Existing(longTermDebtForTotal, end) is { } allInLongTerm
                && FinancialMetricsCatalog.CurrentMaturitiesBundledConcepts.Contains(FirstConcept(allInLongTerm)))
            {
                if (addFinanceLease
                    && DerivedAdd(FinancialMetricFormulaIds.DebtLongTermPlusFinanceLease, true, longTermDebtForTotal, capitalLeaseForTotalDebt, end) is { } allInWithLease)
                    return allInWithLease;
                return allInLongTerm;
            }

            // Long-term debt alone (noncurrent-only) is not treated as total debt.
            return null;
        });

        var netPpe = Resolve(pointEnds, end =>
            DerivedAdd(FinancialMetricFormulaIds.NetPpePlusOperatingLeaseRightOfUseAsset, true, directByMetric[FinancialMetricsCatalog.NetPpe.Id], operatingLeaseRightOfUseAsset, end)
            ?? Existing(directByMetric[FinancialMetricsCatalog.NetPpe.Id], end)
        );

        var netDebt = Resolve(pointEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.TotalDebtMinusCashAndShortTermInvestments, false, debt, cash, end)
        );

        var treasuryShares = Resolve(pointEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.CommonStockSharesIssuedMinusOutstanding, true, shareIssued, ordinarySharesNumber, end)
        );

        return new Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>>
        {
            [FinancialMetricsCatalog.TotalAssets.Id] = totalAssets,
            [FinancialMetricsCatalog.CurrentAssets.Id] = currentAssets,
            [FinancialMetricsCatalog.CashAndShortTermEquivalents.Id] = cash,
            [FinancialMetricsCatalog.CashAndCashEquivalents.Id] = cashComponent,
            [FinancialMetricsCatalog.OtherShortTermInvestments.Id] = otherShortTermInvestments,
            [FinancialMetricsCatalog.Receivables.Id] = receivables,
            [FinancialMetricsCatalog.AccountsReceivable.Id] = directByMetric[FinancialMetricsCatalog.AccountsReceivable.Id],
            [FinancialMetricsCatalog.GrossAccountsReceivable.Id] = grossAccountsReceivable,
            [FinancialMetricsCatalog.AllowanceForDoubtfulAccounts.Id] = allowanceForDoubtfulAccounts,
            [FinancialMetricsCatalog.Inventory.Id] = directByMetric[FinancialMetricsCatalog.Inventory.Id],
            [FinancialMetricsCatalog.OtherCurrentAssets.Id] = directByMetric[FinancialMetricsCatalog.OtherCurrentAssets.Id],
            [FinancialMetricsCatalog.TotalNonCurrentAssets.Id] = totalNonCurrentAssets,
            [FinancialMetricsCatalog.NetPpe.Id] = netPpe,
            [FinancialMetricsCatalog.GrossPpe.Id] = directByMetric[FinancialMetricsCatalog.GrossPpe.Id],
            [FinancialMetricsCatalog.AccumulatedDepreciationPpe.Id] = directByMetric[FinancialMetricsCatalog.AccumulatedDepreciationPpe.Id],
            [FinancialMetricsCatalog.Goodwill.Id] = directByMetric[FinancialMetricsCatalog.Goodwill.Id],
            [FinancialMetricsCatalog.OtherIntangibleAssets.Id] = directByMetric[FinancialMetricsCatalog.OtherIntangibleAssets.Id],
            [FinancialMetricsCatalog.OtherNonCurrentAssets.Id] = directByMetric[FinancialMetricsCatalog.OtherNonCurrentAssets.Id],
            [FinancialMetricsCatalog.TotalLiabilities.Id] = totalLiabilities,
            [FinancialMetricsCatalog.CurrentLiabilities.Id] = currentLiabilities,
            [FinancialMetricsCatalog.PayablesAndAccruedExpenses.Id] = payablesAndAccruedExpenses,
            [FinancialMetricsCatalog.Payables.Id] = directByMetric[FinancialMetricsCatalog.Payables.Id],
            [FinancialMetricsCatalog.AccountsPayable.Id] = accountsPayable,
            [FinancialMetricsCatalog.CurrentAccruedExpense.Id] = currentAccruedExpense,
            [FinancialMetricsCatalog.CurrentDeferredLiabilities.Id] = directByMetric[FinancialMetricsCatalog.CurrentDeferredLiabilities.Id],
            [FinancialMetricsCatalog.CurrentDeferredRevenue.Id] = directByMetric[FinancialMetricsCatalog.CurrentDeferredRevenue.Id],
            [FinancialMetricsCatalog.TotalNonCurrentLiabilities.Id] = totalNonCurrentLiabilities,
            [FinancialMetricsCatalog.LongTermDebtAndCapitalLease.Id] = longTermDebtAndCapitalLease,
            [FinancialMetricsCatalog.LongTermDebt.Id] = longTermDebt,
            [FinancialMetricsCatalog.LongTermCapitalLeaseObligations.Id] = capitalLeaseNoncurrent,
            [FinancialMetricsCatalog.OtherNonCurrentLiabilities.Id] = directByMetric[FinancialMetricsCatalog.OtherNonCurrentLiabilities.Id],
            [FinancialMetricsCatalog.CurrentDebt.Id] = directByMetric[FinancialMetricsCatalog.CurrentDebt.Id],
            [FinancialMetricsCatalog.DeferredTaxLiabilities.Id] = directByMetric[FinancialMetricsCatalog.DeferredTaxLiabilities.Id],
            [FinancialMetricsCatalog.OtherCurrentLiabilities.Id] = directByMetric[FinancialMetricsCatalog.OtherCurrentLiabilities.Id],
            [FinancialMetricsCatalog.LongTermInvestments.Id] = directByMetric[FinancialMetricsCatalog.LongTermInvestments.Id],
            [FinancialMetricsCatalog.MinorityInterest.Id] = directByMetric[FinancialMetricsCatalog.MinorityInterest.Id],
            [FinancialMetricsCatalog.MezzanineEquity.Id] = directByMetric[FinancialMetricsCatalog.MezzanineEquity.Id],
            [FinancialMetricsCatalog.TotalEquityGrossMinorityInterest.Id] = totalEquity,
            [FinancialMetricsCatalog.CommonStockEquity.Id] = commonStockEquity,
            [FinancialMetricsCatalog.CapitalStock.Id] = directByMetric[FinancialMetricsCatalog.CapitalStock.Id],
            [FinancialMetricsCatalog.PreferredStock.Id] = directByMetric[FinancialMetricsCatalog.PreferredStock.Id],
            [FinancialMetricsCatalog.CommonStock.Id] = directByMetric[FinancialMetricsCatalog.CommonStock.Id],
            [FinancialMetricsCatalog.AdditionalPaidInCapital.Id] = directByMetric[FinancialMetricsCatalog.AdditionalPaidInCapital.Id],
            [FinancialMetricsCatalog.RetainedEarnings.Id] = directByMetric[FinancialMetricsCatalog.RetainedEarnings.Id],
            [FinancialMetricsCatalog.TreasuryStock.Id] = directByMetric[FinancialMetricsCatalog.TreasuryStock.Id],
            [FinancialMetricsCatalog.GainsLossesNotAffectingRetainedEarnings.Id] = directByMetric[FinancialMetricsCatalog.GainsLossesNotAffectingRetainedEarnings.Id],
            [FinancialMetricsCatalog.OtherEquityAdjustments.Id] = directByMetric[FinancialMetricsCatalog.OtherEquityAdjustments.Id],
            [FinancialMetricsCatalog.CapitalLeaseObligations.Id] = capitalLeaseObligations,
            [FinancialMetricsCatalog.NetTangibleAssets.Id] = tangibleAssets,
            [FinancialMetricsCatalog.WorkingCapital.Id] = workingCapital,
            [FinancialMetricsCatalog.TangibleBookValue.Id] = tangibleAssets,
            [FinancialMetricsCatalog.TotalDebt.Id] = debt,
            [FinancialMetricsCatalog.NetDebt.Id] = netDebt,
            [FinancialMetricsCatalog.EndCashPosition.Id] = endCashPosition,
            [FinancialMetricsCatalog.SharesOutstanding.Id] = shares,
            [FinancialMetricsCatalog.ShareIssued.Id] = shareIssued,
            [FinancialMetricsCatalog.OrdinarySharesNumber.Id] = ordinarySharesNumber,
            [FinancialMetricsCatalog.TreasuryShares.Id] = treasuryShares
        };
    }

    private static Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> BuildAnnualFlowMetrics(FinancialMetricInputs inputs)
    {
        var revenue = BuildDirectAnnualValues(inputs.RevenuePoints, FinancialMetricsCatalog.Revenue.CostCannotBeNegative, isRevenue: true);
        var cogsDirect = DirectAnnual(inputs, FinancialMetricsCatalog.Cogs);
        var grossProfitDirect = DirectAnnual(inputs, FinancialMetricsCatalog.GrossProfit);
        var sgaDirect = DirectAnnual(inputs, SgaAggregateConcepts(), FinancialMetricsCatalog.Sga.CostCannotBeNegative);
        var sellingAndMarketingExpense = DirectAnnual(inputs, FinancialMetricsCatalog.SellingAndMarketingExpense);
        var generalAndAdministrativeExpense = DirectAnnual(inputs, FinancialMetricsCatalog.GeneralAndAdministrativeExpense);
        var rnd = DirectAnnual(inputs, FinancialMetricsCatalog.Rnd);
        var totalOperatingCostDirect = DirectAnnual(inputs, FinancialMetricsCatalog.TotalOperatingCost);
        var ebitdaDirect = DirectAnnual(inputs, FinancialMetricsCatalog.Ebitda);
        var ebitDirect = DirectAnnual(inputs, FinancialMetricsCatalog.Ebit);
        var interestIncome = DirectAnnual(inputs, FinancialMetricsCatalog.InterestIncome);
        var interestExpense = DirectAnnual(inputs, FinancialMetricsCatalog.InterestExpense);
        var netNonOperatingInterestDirect = DirectAnnual(inputs, FinancialMetricsCatalog.NetNonOperatingInterestIncomeExpense);
        var netInterestIncomeDirect = DirectAnnual(inputs, FinancialMetricsCatalog.NetInterestIncome);
        var otherIncomeExpense = DirectAnnual(inputs, FinancialMetricsCatalog.OtherIncomeExpense);
        var tax = DirectAnnual(inputs, FinancialMetricsCatalog.Tax);
        var ebtDirect = DirectAnnual(inputs, FinancialMetricsCatalog.Ebt);
        var netIncome = DirectAnnual(inputs, FinancialMetricsCatalog.NetIncome);
        var netIncomeCommonStockholders = DirectAnnual(inputs, FinancialMetricsCatalog.NetIncomeCommonStockholders);
        var dilutedNiAvailableToCommonStockholders = DirectAnnual(inputs, FinancialMetricsCatalog.DilutedNiAvailableToCommonStockholders);
        var netIncomeContinuingOperations = DirectAnnual(inputs, FinancialMetricsCatalog.NetIncomeContinuingOperations);
        var basicEps = DirectAnnual(inputs, FinancialMetricsCatalog.BasicEps);
        var dilutedEps = DirectAnnual(inputs, FinancialMetricsCatalog.DilutedEps);
        var basicAverageShares = DirectAnnual(inputs, FinancialMetricsCatalog.BasicAverageShares);
        var dilutedAverageShares = DirectAnnual(inputs, FinancialMetricsCatalog.DilutedAverageShares);
        var operatingCashFlow = DirectAnnual(inputs, FinancialMetricsCatalog.OperatingCashFlow);
        var incomeTaxPaidSupplementalData = DirectAnnual(inputs, FinancialMetricsCatalog.IncomeTaxPaidSupplementalData);
        var interestPaidSupplementalData = DirectAnnual(inputs, FinancialMetricsCatalog.InterestPaidSupplementalData);
        var stockBasedCompensation = DirectAnnual(inputs, FinancialMetricsCatalog.StockBasedCompensation);
        var deferredTax = DirectAnnual(inputs, FinancialMetricsCatalog.DeferredTax);
        var changeInReceivables = InvertWorkingCapitalAssetSign(DirectAnnual(inputs, FinancialMetricsCatalog.ChangeInReceivables));
        var changeInInventory = InvertWorkingCapitalAssetSign(DirectAnnual(inputs, FinancialMetricsCatalog.ChangeInInventory));
        var changeInPayables = DirectAnnual(inputs, FinancialMetricsCatalog.ChangeInPayables);
        var changeInAccruedExpense = DirectAnnual(inputs, FinancialMetricsCatalog.ChangeInAccruedExpense);
        var changeInOtherWorkingCapital = DirectAnnual(inputs, FinancialMetricsCatalog.ChangeInOtherWorkingCapital);
        var investingCashFlow = DirectAnnual(inputs, FinancialMetricsCatalog.InvestingCashFlow);
        var purchaseOfPpe = DirectAnnual(inputs, FinancialMetricsCatalog.PurchaseOfPpe);
        var saleOfPpe = DirectAnnual(inputs, FinancialMetricsCatalog.SaleOfPpe);
        var purchaseOfBusiness = DirectAnnual(inputs, FinancialMetricsCatalog.PurchaseOfBusiness);
        var purchaseOfInvestment = DirectAnnual(inputs, FinancialMetricsCatalog.PurchaseOfInvestment);
        var saleOfInvestment = DirectAnnual(inputs, FinancialMetricsCatalog.SaleOfInvestment);
        var financingCashFlow = DirectAnnual(inputs, FinancialMetricsCatalog.FinancingCashFlow);
        var capex = DirectAnnual(inputs, FinancialMetricsCatalog.Capex);
        var issuanceOfCapitalStock = DirectAnnual(inputs, FinancialMetricsCatalog.IssuanceOfCapitalStock);
        var issuanceOfDebtDirect = DirectAnnual(inputs, FinancialMetricsCatalog.IssuanceOfDebt);
        var repaymentOfDebtDirect = DirectAnnual(inputs, FinancialMetricsCatalog.RepaymentOfDebt);
        var longTermDebtIssuance = DirectAnnual(inputs, FinancialMetricsCatalog.LongTermDebtIssuance);
        var shortTermDebtIssuance = DirectAnnual(inputs, FinancialMetricsCatalog.ShortTermDebtIssuance);
        var longTermDebtPayments = DirectAnnual(inputs, FinancialMetricsCatalog.LongTermDebtPayments);
        var shortTermDebtPayments = DirectAnnual(inputs, FinancialMetricsCatalog.ShortTermDebtPayments);
        var financeLeasePrincipalPayments = DirectAnnual(inputs, FinancialMetricsCatalog.FinanceLeasePrincipalPayments);
        var financeLeaseInterestPayments = DirectAnnual(inputs, FinanceLeaseInterestPaymentConcepts(), true);
        var repurchaseOfCapitalStock = DirectAnnual(inputs, FinancialMetricsCatalog.RepurchaseOfCapitalStock);
        var changesInCash = DirectAnnual(inputs, FinancialMetricsCatalog.ChangesInCash);
        var effectOfExchangeRateChanges = DirectAnnual(inputs, FinancialMetricsCatalog.EffectOfExchangeRateChanges);
        var dividendsPaid = DirectAnnual(inputs, FinancialMetricsCatalog.DividendsPaid);
        var noncontrollingInterestIncome = DirectAnnual(inputs, FinancialMetricsCatalog.NoncontrollingInterestIncome);

        var operatingExpenses = DirectAnnual(inputs, OperatingExpensesConcepts(), true);
        var depreciationAmortization = DirectAnnual(inputs, DepreciationAmortizationConcepts(), true);

        var candidateEnds = SortedEnds(
            revenue.Keys,
            cogsDirect.Keys,
            grossProfitDirect.Keys,
            sgaDirect.Keys,
            sellingAndMarketingExpense.Keys,
            generalAndAdministrativeExpense.Keys,
            rnd.Keys,
            totalOperatingCostDirect.Keys,
            ebitdaDirect.Keys,
            ebitDirect.Keys,
            interestIncome.Keys,
            interestExpense.Keys,
            netNonOperatingInterestDirect.Keys,
            netInterestIncomeDirect.Keys,
            otherIncomeExpense.Keys,
            tax.Keys,
            ebtDirect.Keys,
            netIncome.Keys,
            netIncomeCommonStockholders.Keys,
            dilutedNiAvailableToCommonStockholders.Keys,
            netIncomeContinuingOperations.Keys,
            basicEps.Keys,
            dilutedEps.Keys,
            basicAverageShares.Keys,
            dilutedAverageShares.Keys,
            operatingCashFlow.Keys,
            incomeTaxPaidSupplementalData.Keys,
            interestPaidSupplementalData.Keys,
            stockBasedCompensation.Keys,
            deferredTax.Keys,
            changeInReceivables.Keys,
            changeInInventory.Keys,
            changeInPayables.Keys,
            changeInAccruedExpense.Keys,
            changeInOtherWorkingCapital.Keys,
            investingCashFlow.Keys,
            purchaseOfPpe.Keys,
            saleOfPpe.Keys,
            purchaseOfBusiness.Keys,
            purchaseOfInvestment.Keys,
            saleOfInvestment.Keys,
            financingCashFlow.Keys,
            capex.Keys,
            issuanceOfCapitalStock.Keys,
            issuanceOfDebtDirect.Keys,
            repaymentOfDebtDirect.Keys,
            longTermDebtIssuance.Keys,
            shortTermDebtIssuance.Keys,
            longTermDebtPayments.Keys,
            shortTermDebtPayments.Keys,
            financeLeasePrincipalPayments.Keys,
            financeLeaseInterestPayments.Keys,
            repurchaseOfCapitalStock.Keys,
            changesInCash.Keys,
            effectOfExchangeRateChanges.Keys,
            dividendsPaid.Keys,
            noncontrollingInterestIncome.Keys,
            operatingExpenses.Keys,
            depreciationAmortization.Keys
        );

        var cogs = Resolve(candidateEnds, end =>
            Existing(cogsDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusGrossProfit, true, revenue, grossProfitDirect, end)
            ?? DerivedSubtract3(FinancialMetricFormulaIds.RevenueMinusEbitMinusOperatingExpenses, true, revenue, ebitDirect, operatingExpenses, end)
        );

        var grossProfit = Resolve(candidateEnds, end =>
            RevenueLessSummedCost(revenue, cogs, end)
            ?? Existing(grossProfitDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusCogs, false, revenue, cogs, end)
        );

        var totalOperatingCost = Resolve(candidateEnds, end =>
            Existing(totalOperatingCostDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusEbit, true, revenue, ebitDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.CogsPlusOperatingExpenses, true, cogs, operatingExpenses, end)
        );

        var sga = Resolve(candidateEnds, end =>
            Existing(sgaDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.SellingMarketingPlusGeneralAdministrative, true, sellingAndMarketingExpense, generalAndAdministrativeExpense, end)
            ?? DerivedIdentity(FinancialMetricFormulaIds.GeneralAndAdministrativeExpenseOnly, generalAndAdministrativeExpense, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.OperatingExpensesMinusRnd, true, operatingExpenses, rnd, end)
            ?? DerivedSubtract3(FinancialMetricFormulaIds.TotalOperatingCostMinusCogsMinusRnd, true, totalOperatingCost, cogs, rnd, end)
        );

        var ebit = Resolve(candidateEnds, end =>
            Existing(ebitDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusTotalOperatingCost, false, revenue, totalOperatingCost, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.GrossProfitMinusOperatingExpenses, false, grossProfit, operatingExpenses, end)
        );

        var ebitda = Resolve(candidateEnds, end =>
            Existing(ebitdaDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.EbitPlusDepreciationAmortization, false, ebit, depreciationAmortization, end)
        );

        var netNonOperatingInterestIncomeExpense = Resolve(candidateEnds, end =>
            Existing(netNonOperatingInterestDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.InterestIncomeMinusInterestExpense, false, interestIncome, interestExpense, end)
        );

        var netInterestIncome = Resolve(candidateEnds, end =>
            Existing(netInterestIncomeDirect, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.InterestIncomeMinusInterestExpense, false, interestIncome, interestExpense, end)
        );

        var ebt = Resolve(candidateEnds, end =>
            Existing(ebtDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.NetIncomePlusTax, false, netIncome, tax, end)
            ?? DerivedSubtract(FinancialMetricFormulaIds.EbitMinusInterestExpense, false, ebit, interestExpense, end)
        );

        var taxRateForCalcs = Resolve(candidateEnds, end =>
            DerivedPercentDivide(FinancialMetricFormulaIds.TaxDividedByPretaxIncome, tax, ebt, end)
        );

        var freeCashFlow = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.OperatingCashFlowMinusCapex, false, operatingCashFlow, capex, end)
        );

        var issuanceOfDebt = Resolve(candidateEnds, end =>
            Existing(issuanceOfDebtDirect, end)
            ?? DerivedAdd(FinancialMetricFormulaIds.DebtIssuanceLongTermPlusShortTerm, true, longTermDebtIssuance, shortTermDebtIssuance, end)
        );

        var repaymentOfDebt = Resolve(candidateEnds, end =>
            Existing(repaymentOfDebtDirect, end)
            ?? DerivedAdd4(FinancialMetricFormulaIds.DebtRepaymentComponentsWithFinanceLeaseInterest, true, longTermDebtPayments, shortTermDebtPayments, financeLeasePrincipalPayments, financeLeaseInterestPayments, end)
            ?? DerivedAdd3(FinancialMetricFormulaIds.DebtRepaymentComponents, true, longTermDebtPayments, shortTermDebtPayments, financeLeasePrincipalPayments, end)
        );

        var netIssuancePaymentsOfDebt = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.DebtIssuanceMinusRepayment, false, issuanceOfDebt, repaymentOfDebt, end)
        );

        var netLongTermDebtIssuance = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.LongTermDebtIssuanceMinusRepayment, false, longTermDebtIssuance, longTermDebtPayments, end)
        );

        var netShortTermDebtIssuance = Resolve(candidateEnds, end =>
            DerivedSubtract(FinancialMetricFormulaIds.ShortTermDebtIssuanceMinusRepayment, false, shortTermDebtIssuance, shortTermDebtPayments, end)
        );

        var annualByMetric = new Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>>
        {
            [FinancialMetricsCatalog.Revenue.Id] = revenue,
            [FinancialMetricsCatalog.Cogs.Id] = cogs,
            [FinancialMetricsCatalog.GrossProfit.Id] = grossProfit,
            [FinancialMetricsCatalog.Sga.Id] = sga,
            [FinancialMetricsCatalog.SellingAndMarketingExpense.Id] = sellingAndMarketingExpense,
            [FinancialMetricsCatalog.GeneralAndAdministrativeExpense.Id] = generalAndAdministrativeExpense,
            [FinancialMetricsCatalog.Rnd.Id] = rnd,
            [FinancialMetricsCatalog.TotalOperatingCost.Id] = totalOperatingCost,
            [FinancialMetricsCatalog.OperatingExpensesMetric.Id] = operatingExpenses,
            [FinancialMetricsCatalog.Ebitda.Id] = ebitda,
            [FinancialMetricsCatalog.Ebit.Id] = ebit,
            [FinancialMetricsCatalog.OperatingIncomeAsReported.Id] = ebitDirect,
            [FinancialMetricsCatalog.InterestIncome.Id] = interestIncome,
            [FinancialMetricsCatalog.InterestExpense.Id] = interestExpense,
            [FinancialMetricsCatalog.NetNonOperatingInterestIncomeExpense.Id] = netNonOperatingInterestIncomeExpense,
            [FinancialMetricsCatalog.NetInterestIncome.Id] = netInterestIncome,
            [FinancialMetricsCatalog.OtherIncomeExpense.Id] = otherIncomeExpense,
            [FinancialMetricsCatalog.Tax.Id] = tax,
            [FinancialMetricsCatalog.Ebt.Id] = ebt,
            [FinancialMetricsCatalog.TaxRateForCalcs.Id] = taxRateForCalcs,
            [FinancialMetricsCatalog.NetIncome.Id] = netIncome,
            [FinancialMetricsCatalog.NetIncomeCommonStockholders.Id] = netIncomeCommonStockholders,
            [FinancialMetricsCatalog.DilutedNiAvailableToCommonStockholders.Id] = dilutedNiAvailableToCommonStockholders,
            [FinancialMetricsCatalog.NetIncomeContinuingOperations.Id] = netIncomeContinuingOperations,
            [FinancialMetricsCatalog.BasicEps.Id] = basicEps,
            [FinancialMetricsCatalog.DilutedEps.Id] = dilutedEps,
            [FinancialMetricsCatalog.BasicAverageShares.Id] = basicAverageShares,
            [FinancialMetricsCatalog.DilutedAverageShares.Id] = dilutedAverageShares,
            [FinancialMetricsCatalog.ReconciledDepreciation.Id] = depreciationAmortization,
            [FinancialMetricsCatalog.OperatingCashFlow.Id] = operatingCashFlow,
            [FinancialMetricsCatalog.IncomeTaxPaidSupplementalData.Id] = incomeTaxPaidSupplementalData,
            [FinancialMetricsCatalog.InterestPaidSupplementalData.Id] = interestPaidSupplementalData,
            [FinancialMetricsCatalog.StockBasedCompensation.Id] = stockBasedCompensation,
            [FinancialMetricsCatalog.DeferredTax.Id] = deferredTax,
            [FinancialMetricsCatalog.ChangeInReceivables.Id] = changeInReceivables,
            [FinancialMetricsCatalog.ChangeInInventory.Id] = changeInInventory,
            [FinancialMetricsCatalog.ChangeInPayables.Id] = changeInPayables,
            [FinancialMetricsCatalog.ChangeInAccruedExpense.Id] = changeInAccruedExpense,
            [FinancialMetricsCatalog.ChangeInOtherWorkingCapital.Id] = changeInOtherWorkingCapital,
            [FinancialMetricsCatalog.InvestingCashFlow.Id] = investingCashFlow,
            [FinancialMetricsCatalog.PurchaseOfPpe.Id] = purchaseOfPpe,
            [FinancialMetricsCatalog.SaleOfPpe.Id] = saleOfPpe,
            [FinancialMetricsCatalog.PurchaseOfBusiness.Id] = purchaseOfBusiness,
            [FinancialMetricsCatalog.PurchaseOfInvestment.Id] = purchaseOfInvestment,
            [FinancialMetricsCatalog.SaleOfInvestment.Id] = saleOfInvestment,
            [FinancialMetricsCatalog.FinancingCashFlow.Id] = financingCashFlow,
            [FinancialMetricsCatalog.Capex.Id] = capex,
            [FinancialMetricsCatalog.FreeCashFlow.Id] = freeCashFlow,
            [FinancialMetricsCatalog.IssuanceOfCapitalStock.Id] = issuanceOfCapitalStock,
            [FinancialMetricsCatalog.IssuanceOfDebt.Id] = issuanceOfDebt,
            [FinancialMetricsCatalog.RepaymentOfDebt.Id] = repaymentOfDebt,
            [FinancialMetricsCatalog.LongTermDebtIssuance.Id] = longTermDebtIssuance,
            [FinancialMetricsCatalog.ShortTermDebtIssuance.Id] = shortTermDebtIssuance,
            [FinancialMetricsCatalog.LongTermDebtPayments.Id] = longTermDebtPayments,
            [FinancialMetricsCatalog.ShortTermDebtPayments.Id] = shortTermDebtPayments,
            [FinancialMetricsCatalog.FinanceLeasePrincipalPayments.Id] = financeLeasePrincipalPayments,
            [FinancialMetricsCatalog.NetIssuancePaymentsOfDebt.Id] = netIssuancePaymentsOfDebt,
            [FinancialMetricsCatalog.NetLongTermDebtIssuance.Id] = netLongTermDebtIssuance,
            [FinancialMetricsCatalog.NetShortTermDebtIssuance.Id] = netShortTermDebtIssuance,
            [FinancialMetricsCatalog.RepurchaseOfCapitalStock.Id] = repurchaseOfCapitalStock,
            [FinancialMetricsCatalog.ChangesInCash.Id] = changesInCash,
            [FinancialMetricsCatalog.EffectOfExchangeRateChanges.Id] = effectOfExchangeRateChanges,
            [FinancialMetricsCatalog.DividendsPaid.Id] = dividendsPaid,
            [FinancialMetricsCatalog.NoncontrollingInterestIncome.Id] = noncontrollingInterestIncome
        };
        AddPerShare(annualByMetric);
        return annualByMetric;
    }

    private static IReadOnlyDictionary<string, FinancialMetricValue> DirectQuarterly(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric,
        Dictionary<string, FinancialMetricQuarterEntry> metaByEnd) =>
        DirectQuarterly(inputs, DirectConcepts(metric), metric.CostCannotBeNegative, metaByEnd);

    private static IReadOnlyDictionary<string, FinancialMetricValue> DirectAnnual(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric) =>
        DirectAnnual(inputs, DirectConcepts(metric), metric.CostCannotBeNegative);

    private static IReadOnlyDictionary<string, FinancialMetricValue> DirectAnnual(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts,
        bool costCannotBeNegative) =>
        BuildDirectAnnualValues(MergeFlowPeriodFacts(inputs, concepts), costCannotBeNegative, isRevenue: false);

    private static IReadOnlyList<SecConceptSpec> DirectConcepts(FinancialMetricDefinition metric) =>
        FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.DirectConceptsFor(metric, FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> SgaAggregateConcepts() =>
        DirectConcepts(FinancialMetricsCatalog.Sga)
            .Where(c => c.Concept == "SellingGeneralAndAdministrativeExpense")
            .ToList();

    private static IReadOnlyList<SecConceptSpec> OperatingExpensesConcepts() =>
        FinancialMetricsCatalog.OperatingExpensesConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.OperatingExpensesConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> DepreciationAmortizationConcepts() =>
        FinancialMetricsCatalog.DepreciationAmortizationConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.DepreciationAmortizationConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> ShortTermInvestmentsConcepts() =>
        FinancialMetricsCatalog.ShortTermInvestmentsConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.ShortTermInvestmentsConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> CashCombinedConcepts() =>
        FinancialMetricsCatalog.CashCombinedConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.CashCombinedConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> CashComponentConcepts() =>
        FinancialMetricsCatalog.CashComponentConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.CashComponentConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> DebtDirectConcepts() =>
        FinancialMetricsCatalog.DebtDirectConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.DebtDirectConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> DebtCurrentConcepts() =>
        FinancialMetricsCatalog.DebtCurrentConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.DebtCurrentConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> LongTermDebtCurrentConcepts() =>
        FinancialMetricsCatalog.LongTermDebtCurrentConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.LongTermDebtCurrentConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> LongTermDebtConcepts() =>
        FinancialMetricsCatalog.LongTermDebtConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.LongTermDebtConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> LongTermDebtForTotalDebtConcepts() =>
        FinancialMetricsCatalog.LongTermDebtForTotalDebtConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.LongTermDebtForTotalDebtConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> CurrentAssetsConcepts() =>
        FinancialMetricsCatalog.CurrentAssetsConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.CurrentAssetsConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> CurrentLiabilitiesConcepts() =>
        FinancialMetricsCatalog.CurrentLiabilitiesConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.CurrentLiabilitiesConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> GoodwillAndIntangibleAssetsConcepts() =>
        FinancialMetricsCatalog.GoodwillAndIntangibleAssetsConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.GoodwillAndIntangibleAssetsConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> GoodwillConcepts() =>
        FinancialMetricsCatalog.GoodwillConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.GoodwillConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> IntangibleAssetsConcepts() =>
        FinancialMetricsCatalog.IntangibleAssetsConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.IntangibleAssetsConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> CapitalLeaseCurrentConcepts() =>
        FinancialMetricsCatalog.CapitalLeaseCurrentConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.CapitalLeaseCurrentConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> CapitalLeaseNoncurrentConcepts() =>
        FinancialMetricsCatalog.CapitalLeaseNoncurrentConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.CapitalLeaseNoncurrentConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> FinanceLeaseInterestPaymentConcepts() =>
        FinancialMetricsCatalog.FinanceLeaseInterestPaymentConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.FinanceLeaseInterestPaymentConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyList<SecConceptSpec> OperatingLeaseRightOfUseAssetConcepts() =>
        FinancialMetricsCatalog.OperatingLeaseRightOfUseAssetConceptsFor(FinancialMetricProvider.UsGaap)
            .Concat(FinancialMetricsCatalog.OperatingLeaseRightOfUseAssetConceptsFor(FinancialMetricProvider.IfrsFull))
            .GroupBy(c => c.Key)
            .Select(g => g.First())
            .ToList();

    private static IReadOnlyDictionary<string, FinancialMetricValue> InvertWorkingCapitalAssetSign(
        IReadOnlyDictionary<string, FinancialMetricValue> values) =>
        values.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var value = kv.Value;
                if (!value.HasValue)
                    return value;

                return new FinancialMetricValue(
                    -value.Value!.Value,
                    FinancialMetricFormulaIds.WorkingCapitalAssetPresentationSign,
                    value.SourceConcepts,
                    value.Filed,
                    value.Accession,
                    "presentation_sign",
                    "derived",
                    value.SourceDocument,
                    value.SourceLabel,
                    [TraceInput("raw", kv.Key, value)]
                );
            },
            StringComparer.Ordinal);

    private static void SupplementDirectTtmDurationValues(
        Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> ttmByMetric,
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric)
    {
        if (!ttmByMetric.TryGetValue(metric.Id, out var existing))
            return;

        var supplemented = existing.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        foreach (var (end, value) in BuildDirectTtmDurationValues(inputs, metric))
            supplemented.TryAdd(end, value);

        ttmByMetric[metric.Id] = supplemented;
    }

    private static IReadOnlyDictionary<string, FinancialMetricValue> BuildDirectTtmDurationValues(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric) =>
        BuildDirectTtmDurationValues(inputs, DirectConcepts(metric), metric.CostCannotBeNegative);

    private static IReadOnlyDictionary<string, FinancialMetricValue> BuildDirectTtmDurationValues(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts,
        bool costCannotBeNegative)
    {
        var merged = new Dictionary<string, FinancialPoint>(StringComparer.Ordinal);
        foreach (var point in MergeFlowPeriodFacts(inputs, concepts)
                     .Where(IsTwelveMonthDurationFact)
                     .OrderBy(p => p.Filed, StringComparer.Ordinal))
        {
            if (costCannotBeNegative && point.Val < 0) continue;
            FinancialPointMerger.MergePeriodFact(merged, point, p => p.End);
        }

        return merged.Values.ToDictionary(
            p => p.End,
            p => new FinancialMetricValue(
                p.Val,
                FinancialMetricFormulaIds.DirectTtmFact,
                p.Concept ?? "",
                p.Filed,
                p.Accn,
                "direct_ttm",
                p.SourceType,
                p.SourceDocument,
                p.SourceLabel),
            StringComparer.Ordinal);
    }

    private static void SupplementRepaymentOfDebtTtmWithFinanceLeaseInterest(
        Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> ttmByMetric,
        FinancialMetricInputs inputs)
    {
        if (!ttmByMetric.TryGetValue(FinancialMetricsCatalog.RepaymentOfDebt.Id, out var existing))
            return;

        var interestTtm = BuildDirectTtmDurationValues(inputs, FinanceLeaseInterestPaymentConcepts(), costCannotBeNegative: true);
        if (interestTtm.Count == 0)
            return;

        var supplemented = existing.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        foreach (var (end, interest) in interestTtm)
        {
            if (!supplemented.TryGetValue(end, out var repayment) || !repayment.HasValue)
                continue;
            if (repayment.SourceConcepts.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("FinanceLeaseInterestPaymentOnLiability", StringComparer.Ordinal))
                continue;

            var added = DerivedAdd(
                FinancialMetricFormulaIds.DebtRepaymentComponentsWithFinanceLeaseInterest,
                true,
                new Dictionary<string, FinancialMetricValue>(StringComparer.Ordinal) { [end] = repayment },
                interestTtm,
                end);
            if (added is not null)
                supplemented[end] = added;
        }

        ttmByMetric[FinancialMetricsCatalog.RepaymentOfDebt.Id] = supplemented;
    }

    private static bool IsTwelveMonthDurationFact(FinancialPoint point)
    {
        if (string.IsNullOrWhiteSpace(point.Start)) return false;
        if (!DateTime.TryParse(point.Start, out var start) || !DateTime.TryParse(point.End, out var end)) return false;
        var days = (end - start).TotalDays;
        return days is >= 340 and <= 400;
    }

    // True when a debt component value's source concept already bundles finance/capital lease
    // obligations, so a separate finance-lease term must not be added on top of it. The set of
    // bundling concepts is declared in FinancialMetricsCatalog.FinanceLeaseBundledConcepts rather
    // than inferred from the concept string, so the invariant is explicit and reviewable.
    private static bool BundlesFinanceLease(FinancialMetricValue? value) =>
        value?.SourceConcepts is { } concepts
        && concepts
            .Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(FinancialMetricsCatalog.FinanceLeaseBundledConcepts.Contains);

    // The first (highest-priority) source concept a resolved value came from. For a direct fact this
    // is the concept itself; for a derived sum it is the first operand's concept.
    private static string FirstConcept(FinancialMetricValue value)
    {
        var concepts = value.SourceConcepts;
        var separator = concepts.IndexOfAny(['|', ',']);
        return separator < 0 ? concepts : concepts[..separator].Trim();
    }

    private static bool IsShareOnlyPointMetric(string metricId) =>
        metricId == FinancialMetricsCatalog.SharesOutstanding.Id
        || metricId == FinancialMetricsCatalog.ShareIssued.Id
        || metricId == FinancialMetricsCatalog.OrdinarySharesNumber.Id
        || metricId == FinancialMetricsCatalog.TreasuryShares.Id;

    private static bool IsSummedCostBlock(string? sourceType) => sourceType == "filing-table-derived";

    /// <summary>
    /// When cost of revenue came from summing a filing's cost items, gross profit is taken as
    /// revenue minus that cost, ahead of any reported GrossProfit tag (which may cover one segment).
    /// </summary>
    private static FinancialMetricValue? RevenueLessSummedCost(
        IReadOnlyDictionary<string, FinancialMetricValue> revenue,
        IReadOnlyDictionary<string, FinancialMetricValue> cogs,
        string end) =>
        IsSummedCostBlock(Existing(cogs, end)?.SourceType)
            ? DerivedSubtract(FinancialMetricFormulaIds.RevenueMinusCogs, false, revenue, cogs, end)
            : null;

    private static IReadOnlyDictionary<string, FinancialMetricValue> AverageShareQuarters(
        FinancialMetricInputs inputs,
        FinancialMetricDefinition metric,
        Dictionary<string, FinancialMetricQuarterEntry> metaByEnd)
    {
        var quarters = WeightedAverageShareQuarterBuilder.Build(inputs, metric);
        AddMeta(metaByEnd, quarters);
        return ToValues(quarters);
    }

    private static void AddPerShare(Dictionary<string, IReadOnlyDictionary<string, FinancialMetricValue>> view) =>
        FinancialMetricPerShareBuilder.AddDerivedMetrics(view);

    private static IReadOnlyDictionary<string, FinancialMetricValue> TtmView(
        FinancialMetricDefinition metric,
        IReadOnlyDictionary<string, FinancialMetricValue> quarterly,
        Dictionary<string, FinancialMetricQuarterEntry> metaByEnd)
    {
        if (metric.TtmPolicy == FinancialMetricTtmPolicy.SumQuarterly)
            return FinancialMetricTtmBuilder.Build(quarterly, metaByEnd);
        if (metric.TtmPolicy == FinancialMetricTtmPolicy.CarryQuarterly)
            return FinancialMetricPerShareBuilder.CarryQuarterlyShares(metric, quarterly);
        return new Dictionary<string, FinancialMetricValue>();
    }

    private static IReadOnlyDictionary<string, FinancialMetricValue> DirectQuarterly(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts,
        bool costCannotBeNegative,
        Dictionary<string, FinancialMetricQuarterEntry> metaByEnd)
    {
        var entries = BuildDirectQuarterlyEntries(MergeFlowPeriodFacts(inputs, concepts), costCannotBeNegative, isRevenue: false);
        AddMeta(metaByEnd, entries);
        return ToValues(entries);
    }

    private static Dictionary<string, FinancialMetricQuarterEntry> BuildDirectQuarterlyEntries(
        IEnumerable<FinancialPoint> points,
        bool costCannotBeNegative,
        bool isRevenue)
    {
        var result = new Dictionary<string, FinancialMetricQuarterEntry>();
        // Build already ran every fact through one ticker-wide StatementPeriodEnds.
        foreach (var q in QuarterlySeriesBuilder.BuildQuarterlySeries(points, factsAlreadyAligned: true))
        {
            if (costCannotBeNegative && q.Val < 0) continue;
            var formula = QuarterFormula(q, isRevenue);
            result[q.End] = new FinancialMetricQuarterEntry(
                q.End,
                q.FiscalYear,
                q.FiscalQuarter,
                new FinancialMetricValue(
                    q.Val,
                    formula,
                    q.SourceConcept ?? "",
                    q.Filed,
                    q.Accession,
                    q.Derivation,
                    q.SourceType,
                    q.SourceDocument,
                    q.SourceLabel,
                    q.Inputs),
                q.ValidationMessages
            );
        }
        return result;
    }

    private static IReadOnlyDictionary<string, FinancialMetricValue> BuildDirectAnnualValues(
        IEnumerable<FinancialPoint> points,
        bool costCannotBeNegative,
        bool isRevenue)
    {
        var merged = new Dictionary<string, FinancialPoint>(StringComparer.Ordinal);
        foreach (var point in points.Where(IsAnnualFlowFact).OrderBy(p => p.Filed, StringComparer.Ordinal))
        {
            if (costCannotBeNegative && point.Val < 0) continue;
            FinancialPointMerger.MergePeriodFact(merged, point, p => p.End);
        }

        var result = new Dictionary<string, FinancialMetricValue>(StringComparer.Ordinal);
        foreach (var point in merged.Values.OrderBy(p => p.Filed, StringComparer.Ordinal))
        {
            result[point.End] = new FinancialMetricValue(
                point.Val,
                AnnualFormula(point, isRevenue),
                point.Concept ?? "",
                point.Filed,
                point.Accn,
                "reported",
                point.SourceType,
                point.SourceDocument,
                point.SourceLabel
            );
        }
        return result;
    }

    private static string QuarterFormula(QuarterlySeriesBuilder.QuarterlyPoint point, bool isRevenue)
    {
        return point.Derivation switch
        {
            "derived_from_h1" => FinancialMetricFormulaIds.DerivedFromH1,
            "derived_from_9m" => FinancialMetricFormulaIds.DerivedFrom9M,
            "derived_from_annual" => FinancialMetricFormulaIds.DerivedFromAnnual,
            _ when IsSummedCostBlock(point.SourceType) => FinancialMetricFormulaIds.FilingTableCostComponents,
            _ when isRevenue && point.SourceConcept == "InterestIncomeExpenseNet+NoninterestIncome" => FinancialMetricFormulaIds.BankNetRevenue,
            _ => FinancialMetricFormulaIds.DirectFact
        };
    }

    private static string AnnualFormula(FinancialPoint point, bool isRevenue) =>
        IsSummedCostBlock(point.SourceType)
            ? FinancialMetricFormulaIds.FilingTableCostComponents
            : isRevenue && point.Concept == "InterestIncomeExpenseNet+NoninterestIncome"
                ? FinancialMetricFormulaIds.BankNetRevenue
                : FinancialMetricFormulaIds.DirectFact;

    private static bool IsAnnualFlowFact(FinancialPoint point) =>
        FinancialPointHelpers.IsAnnualFlowFact(point);

    private static IReadOnlyDictionary<string, FinancialMetricValue> BuildDirectPointValues(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts,
        bool costCannotBeNegative) =>
        BuildDirectPointValues(MergePointFacts(inputs, concepts), costCannotBeNegative);

    private static IReadOnlyDictionary<string, FinancialMetricValue> BuildDirectPointValues(
        IEnumerable<FinancialPoint> points,
        bool costCannotBeNegative)
    {
        var result = new Dictionary<string, FinancialMetricValue>();
        foreach (var point in points.OrderBy(p => p.Filed))
        {
            if (costCannotBeNegative && point.Val < 0) continue;
            result[point.End] = new FinancialMetricValue(
                point.Val,
                FinancialMetricFormulaIds.DirectFact,
                point.Concept ?? "",
                point.Filed,
                point.Accn,
                "reported",
                point.SourceType,
                point.SourceDocument,
                point.SourceLabel
            );
        }
        return result;
    }

    private static IReadOnlyList<FinancialPoint> MergeFlowPeriodFacts(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts) =>
        FinancialPointMerger.MergeFlowPeriodFacts(inputs, concepts);

    private static IReadOnlyList<FinancialPoint> MergePointFacts(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts) =>
        FinancialPointMerger.MergePointFacts(inputs, concepts);

    private static IReadOnlyList<FinancialPoint> MergeSharePointFacts(
        FinancialMetricInputs inputs,
        IReadOnlyList<SecConceptSpec> concepts) =>
        FinancialPointMerger.MergeSharePointFacts(inputs, concepts);

    private static bool IsSharePointMetric(FinancialMetricDefinition metric) =>
        metric.Id == FinancialMetricsCatalog.SharesOutstanding.Id
        || metric.Id == FinancialMetricsCatalog.ShareIssued.Id
        || metric.Id == FinancialMetricsCatalog.OrdinarySharesNumber.Id;

    private static Dictionary<string, FinancialMetricValue> ToValues(Dictionary<string, FinancialMetricQuarterEntry> entries) =>
        entries.ToDictionary(kv => kv.Key, kv => kv.Value.Value);

    private static void AddMeta(
        Dictionary<string, FinancialMetricQuarterEntry> metaByEnd,
        IReadOnlyDictionary<string, FinancialMetricQuarterEntry> entries)
    {
        foreach (var (end, entry) in entries)
            if (!metaByEnd.ContainsKey(end))
                metaByEnd[end] = entry;
    }

    private static Dictionary<string, FinancialMetricValue> Resolve(
        IReadOnlyList<string> ends,
        Func<string, FinancialMetricValue?> resolver)
    {
        var result = new Dictionary<string, FinancialMetricValue>();
        foreach (var end in ends)
        {
            var value = resolver(end);
            if (value is { HasValue: true })
                result[end] = value;
        }
        return result;
    }

    // The fiscal calendar derived from the filer's own year ends wins over the SEC fy/fp carried
    // on the fact: fy/fp describe the filing a fact was taken from, so a prior-year comparative
    // reports the filing's fiscal year, and anything lifted from a 10-K has fp="FY" and no
    // quarter at all. See FiscalCalendar. fy/fp and the calendar-month guess stay as fallbacks
    // for filers with too few annual periods to anchor a calendar.
    private static (int? FiscalYear, int? FiscalQuarter, string Label) FiscalLabel(
        string end,
        int? fiscalYear,
        int? fiscalQuarter,
        FinancialMetricQuarterEntry? meta,
        FiscalCalendar calendar)
    {
        if (string.IsNullOrWhiteSpace(end)) return (null, null, "");
        var date = DateTime.Parse(end);
        var derived = calendar.Label(end);
        var year = derived?.FiscalYear ?? fiscalYear ?? meta?.FiscalYear ?? date.Year;
        var quarter = derived?.FiscalQuarter
            ?? fiscalQuarter
            ?? meta?.FiscalQuarter
            ?? (date.Month <= 3 ? 1 : date.Month <= 6 ? 2 : date.Month <= 9 ? 3 : 4);
        return (year, quarter, FiscalCalendar.Format(year, quarter));
    }

    // Facts tagged a few days off the statement period end would otherwise each open their own
    // export column: UNH reports dividends paid against the payment date (2024-03-19 next to the
    // real 2024-03-31), and Deere's balance sheet lands on 2014-11-02 while the income statement
    // for the same fiscal year closes on 2014-10-31. Both produce a second column for one fiscal
    // quarter carrying a handful of metrics, and two columns in a quarter share a label.
    //
    // The set of real statement period ends is taken across ALL concepts, since a balance-sheet
    // concept has no duration facts of its own to align against. Share counts are excluded: a
    // cover-page share count is deliberately dated to the cover, and the analysis paths pair it
    // with prices on that date.
    private static FinancialMetricInputs SnapFactsToStatementPeriodEnds(FinancialMetricInputs inputs)
    {
        // Built once for the whole ticker and reused for every concept: the set has to span all
        // concepts anyway, and rebuilding it per concept would redo the same work hundreds of
        // times per export.
        var statementEnds = StatementPeriodEnds.Build(
            inputs.Facts.Values.SelectMany(points => points).Concat(inputs.RevenuePoints));
        if (statementEnds.IsEmpty) return inputs;

        var shareConceptKeys = FinancialMetricsCatalog.PointMetrics
            .Where(IsSharePointMetric)
            .SelectMany(DirectConcepts)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);
        shareConceptKeys.Add(FinancialMetricsCatalog.StockSplitConversionRatio.Key); // keep split dates on the event day

        var snappedFacts = inputs.Facts.ToDictionary(
            kv => kv.Key,
            kv => shareConceptKeys.Contains(kv.Key)
                ? kv.Value
                : (IReadOnlyList<FinancialPoint>)statementEnds.Apply(kv.Value.ToList()),
            StringComparer.Ordinal);

        return inputs with
        {
            Facts = snappedFacts,
            RevenuePoints = statementEnds.Apply(inputs.RevenuePoints.ToList())
        };
    }

    // Anchored on every concept, not revenue alone: Carnival reports no annual revenue fact for
    // some fiscal years, and a missing anchor merges that year end into the following year's
    // window, so the year end and the next Q1 came out with the same label.
    private static FiscalCalendar FiscalCalendarFor(FinancialMetricInputs inputs) =>
        FiscalCalendar.Build(inputs.Facts.Values.SelectMany(points => points).Concat(inputs.RevenuePoints));
}
