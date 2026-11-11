# Metric reference

Generated from `FinancialMetricsCatalog` by `samples/FilingForge.Samples` (`metrics-doc`). Do not edit by hand.

134 metrics: 76 flow metrics (quarterly, annual and TTM values) and 58 point-in-time metrics (balance-sheet style, one value per period end).

Each metric is filled from the first source that has a value, in the order of the **Sources** column: a reported SEC concept (`DirectFact`) first, then the listed formulas.

## Income Statement

| Id | Name | Kind | Definition | Sources | SEC concepts |
|---|---|---|---|---|---|
| `revenue` | Revenue | Flow | Top-line revenue or bank net revenue. | Direct SEC revenue concept → InterestIncomeExpenseNetPlusNoninterestIncome | `Revenues`, `RevenuesNetOfInterestExpense`, `RevenueFromContractWithCustomerExcludingAssessedTax`, `RevenueFromContractWithCustomerIncludingAssessedTax`, … |
| `cogs` | COGS | Flow | Cost of revenue or goods sold. When a filer reports no single cost total, the cost items listed in its income-statement table are added up. | DirectFact → FilingTableCostComponents → RevenueMinusGrossProfit → RevenueMinusOperatingIncomeLossMinusOperatingExpenses | `CostOfGoodsAndServicesSold`, `CostOfGoodsSold`, `CostOfRevenue`, `ifrs-full:CostOfSales` |
| `grossProfit` | Gross Profit | Flow | Revenue after COGS. | DirectFact → RevenueMinusCogs | `GrossProfit`, `ifrs-full:GrossProfit` |
| `sga` | SG&A Costs | Flow | Selling, general and administrative expense. | DirectFact → SellingMarketingPlusGeneralAdministrative → GeneralAndAdministrativeExpenseOnly → OperatingExpensesMinusResearchAndDevelopment → TotalOperatingCostMinusCogsMinusResearchAndDevelopment | `SellingGeneralAndAdministrativeExpense`, `ifrs-full:SellingGeneralAndAdministrativeExpense` |
| `sellingAndMarketingExpense` | Selling & Marketing Expense | Flow | Selling, marketing, and advertising expense. | DirectFact | `SellingAndMarketingExpense`, `MarketingExpense`, `MarketingAndAdvertisingExpense`, `AdvertisingExpense` |
| `generalAndAdministrativeExpense` | General & Administrative Expense | Flow | General and administrative expense. | DirectFact | `GeneralAndAdministrativeExpense` |
| `rnd` | R&D Costs | Flow | Research and development expense. | DirectFact | `ResearchAndDevelopmentExpense`, `ResearchAndDevelopmentExpenseExcludingAcquiredInProcessCost`, `ResearchAndDevelopmentExpenseSoftwareExcludingAcquiredInProcessCost`, `ifrs-full:ResearchAndDevelopmentExpense` |
| `totalOperatingCost` | Total Operating Cost | Flow | Total costs needed to reconcile revenue to operating profit. | DirectFact → RevenueMinusOperatingProfit → CogsPlusOperatingExpenses | `CostsAndExpenses` |
| `operatingExpenses` | Operating Expense | Flow | Operating expenses. | DirectFact | `OperatingExpenses`, `ifrs-full:OperatingExpenses` |
| `ebitda` | EBITDA | Flow | Earnings before interest, tax, depreciation and amortization. | DirectFact → EbitPlusDepreciationAmortization | `EarningsBeforeInterestTaxesDepreciationAndAmortization` |
| `ebit` | Operating Profit (EBIT) | Flow | Operating income / EBIT. | DirectFact → RevenueMinusTotalOperatingCost → GrossProfitMinusOperatingExpenses | `OperatingIncomeLoss`, `ifrs-full:ProfitLossFromOperatingActivities` |
| `operatingIncomeAsReported` | Total Operating Income as Reported | Flow | Reported operating income / loss without formula fallback. | DirectFact | `OperatingIncomeLoss`, `ifrs-full:ProfitLossFromOperatingActivities` |
| `interestIncome` | Interest Income | Flow | Interest income. | DirectFact | `InterestIncomeNonOperating`, `InvestmentIncomeInterest`, `InterestIncomeOperating`, `ifrs-full:FinanceIncome`, … |
| `interestExpense` | Interest Expense | Flow | Interest expense. | DirectFact | `InterestExpenseNonOperating`, `InterestExpense`, `InterestAndDebtExpense`, `ifrs-full:FinanceCosts`, … |
| `netNonOperatingInterestIncomeExpense` | Net Non Operating Interest Income Expense | Flow | Net non-operating interest income less interest expense. | DirectFact → InterestIncomeMinusInterestExpense | `InterestIncomeExpenseNonOperatingNet` |
| `netInterestIncome` | Net Interest Income | Flow | Interest income less interest expense, or bank net interest income. | DirectFact → InterestIncomeMinusInterestExpense | `InterestIncomeExpenseNet` |
| `otherIncomeExpense` | Other Income Expense | Flow | Other non-operating income or expense. | DirectFact | `NonoperatingIncomeExpense`, `OtherNonoperatingIncomeExpense`, `OtherIncomeExpenseNet`, `ifrs-full:OtherIncome`, … |
| `tax` | Tax | Flow | Income tax expense or benefit. | DirectFact | `IncomeTaxExpenseBenefit`, `ifrs-full:IncomeTaxExpenseContinuingOperations`, `ifrs-full:IncomeTaxExpenseIncome` |
| `ebt` | EBT | Flow | Earnings before tax. | DirectFact → NetIncomePlusTax → EbitMinusInterestExpense | `IncomeLossFromContinuingOperationsBeforeIncomeTaxesExtraordinaryItemsNoncontrollingInterest`, `IncomeLossFromContinuingOperationsBeforeIncomeTaxesMinorityInterestAndIncomeLossFromEquityMethodInvestments`, `IncomeLossFromContinuingOperationsBeforeIncomeTaxes`, `ifrs-full:ProfitLossBeforeTax` |
| `taxRateForCalcs` | Tax Rate for Calcs | Flow | Tax provision divided by pretax income, shown as a percent. | TaxProvisionDividedByPretaxIncome |  |
| `netIncome` | Net Income | Flow | Consolidated net income / loss; falls back to parent-attributable net income and common-shareholder income. | DirectFact | `ProfitLoss`, `NetIncomeLoss`, `NetIncomeLossAvailableToCommonStockholdersBasic`, `ifrs-full:ProfitLoss`, … |
| `netIncomeCommonStockholders` | Net Income Common Stockholders | Flow | Net income available to common stockholders. | DirectFact | `NetIncomeLossAvailableToCommonStockholdersBasic`, `NetIncomeLossAvailableToCommonStockholdersDiluted`, `ifrs-full:ProfitLossAttributableToOrdinaryEquityHoldersOfParentEntity`, `ifrs-full:ProfitLossAttributableToOwnersOfParent` |
| `dilutedNiAvailableToCommonStockholders` | Diluted NI Available to Com Stockholders | Flow | Diluted net income available to common stockholders. | DirectFact | `NetIncomeLossAvailableToCommonStockholdersDiluted`, `NetIncomeLossAvailableToCommonStockholdersBasic`, `ifrs-full:ProfitLossAttributableToOrdinaryEquityHoldersOfParentEntity`, `ifrs-full:ProfitLossAttributableToOwnersOfParent` |
| `netIncomeContinuingOperations` | Net Income from Continuing Operations | Flow | Income or loss from continuing operations after tax. | DirectFact | `IncomeLossFromContinuingOperationsAfterTax`, `IncomeLossFromContinuingOperationsIncludingPortionAttributableToNoncontrollingInterest`, `IncomeLossFromContinuingOperations`, `ifrs-full:ProfitLossFromContinuingOperations` |
| `basicAverageShares` | Basic Average Shares | Flow | Weighted-average basic shares outstanding. An average is not summed across quarters, so TTM shows the period's quarterly count. | DirectFact → StandaloneWeightedAverageQuarterFromAnnual → QuarterlyBasicAverageSharesCarriedToTtm | `WeightedAverageNumberOfSharesOutstandingBasic`, `ifrs-full:WeightedAverageShares` |
| `dilutedAverageShares` | Diluted Average Shares | Flow | Weighted-average diluted shares outstanding. An average is not summed across quarters, so TTM shows the period's quarterly count. | DirectFact → StandaloneWeightedAverageQuarterFromAnnual → QuarterlyDilutedAverageSharesCarriedToTtm | `WeightedAverageNumberOfDilutedSharesOutstanding`, `ifrs-full:AdjustedWeightedAverageShares`, `ifrs-full:WeightedAverageShares` |
| `revenuePerShareBasic` | Revenue per Share Basic | Flow | Same-period revenue over the basic weighted-average share count. | RevenueDividedByBasicAverageShares |  |
| `revenuePerShareDiluted` | Revenue per Share Diluted | Flow | Same-period revenue over the diluted weighted-average share count. | RevenueDividedByDilutedAverageShares |  |
| `cogsPerShareBasic` | Cost of Revenue per Share Basic | Flow | Same-period cost of revenue over the basic weighted-average share count. | CostOfRevenueDividedByBasicAverageShares |  |
| `cogsPerShareDiluted` | Cost of Revenue per Share Diluted | Flow | Same-period cost of revenue over the diluted weighted-average share count. | CostOfRevenueDividedByDilutedAverageShares |  |
| `grossProfitPerShareBasic` | Gross Profit per Share Basic | Flow | Same-period gross profit over the basic weighted-average share count. | GrossProfitDividedByBasicAverageShares |  |
| `grossProfitPerShareDiluted` | Gross Profit per Share Diluted | Flow | Same-period gross profit over the diluted weighted-average share count. | GrossProfitDividedByDilutedAverageShares |  |
| `operatingExpensesPerShareBasic` | Operating Expenses per Share Basic | Flow | Same-period total operating expenses over the basic weighted-average share count. | OperatingExpensesDividedByBasicAverageShares |  |
| `operatingExpensesPerShareDiluted` | Operating Expenses per Share Diluted | Flow | Same-period total operating expenses over the diluted weighted-average share count. | OperatingExpensesDividedByDilutedAverageShares |  |
| `operatingIncomePerShareBasic` | Operating Income per Share Basic | Flow | Same-period operating income (EBIT) over the basic weighted-average share count. | OperatingIncomeDividedByBasicAverageShares |  |
| `operatingIncomePerShareDiluted` | Operating Income per Share Diluted | Flow | Same-period operating income (EBIT) over the diluted weighted-average share count. | OperatingIncomeDividedByDilutedAverageShares |  |
| `pretaxIncomePerShareBasic` | Pretax Income per Share Basic | Flow | Same-period pretax income (EBT) over the basic weighted-average share count. | PretaxIncomeDividedByBasicAverageShares |  |
| `pretaxIncomePerShareDiluted` | Pretax Income per Share Diluted | Flow | Same-period pretax income (EBT) over the diluted weighted-average share count. | PretaxIncomeDividedByDilutedAverageShares |  |
| `basicEPS` | Basic EPS | Flow | Reported basic earnings per share. The TTM figure adds up the four most recent consecutive standalone quarters. | DirectFact → TtmSumQuarterly | `EarningsPerShareBasic`, `EarningsPerShareBasicAndDiluted`, `ifrs-full:BasicEarningsLossPerShare`, `ifrs-full:BasicEarningsLossPerShareFromContinuingOperations` |
| `dilutedEPS` | Diluted EPS | Flow | Reported diluted earnings per share. The TTM figure adds up the four most recent consecutive standalone quarters. | DirectFact → TtmSumQuarterly | `EarningsPerShareDiluted`, `EarningsPerShareBasicAndDiluted`, `ifrs-full:DilutedEarningsLossPerShare`, `ifrs-full:DilutedEarningsLossPerShareFromContinuingOperations` |
| `reconciledDepreciation` | Reconciled Depreciation | Flow | Depreciation, depletion and amortization used to reconcile EBIT to EBITDA. | DirectFact | `DepreciationDepletionAndAmortization`, `DepreciationDepletionAndAmortizationExpense`, `DepreciationAndAmortization`, `DepreciationAndAmortizationExpense`, … |
| `noncontrollingInterestIncome` | Net Income Attributable to Noncontrolling Interest | Flow | Portion of consolidated net income attributable to noncontrolling (minority) interests. | DirectFact | `NetIncomeLossAttributableToNoncontrollingInterest`, `ifrs-full:ProfitLossAttributableToNoncontrollingInterests` |

## Cash Flow

| Id | Name | Kind | Definition | Sources | SEC concepts |
|---|---|---|---|---|---|
| `operatingCashFlow` | Operating Cash Flow | Flow | Net cash provided by operating activities. | DirectFact | `NetCashProvidedByUsedInOperatingActivities`, `NetCashProvidedByUsedInOperatingActivitiesContinuingOperations`, `ifrs-full:CashFlowsFromUsedInOperatingActivities` |
| `incomeTaxPaidSupplementalData` | Income Tax Paid Supplemental Data | Flow | Cash paid for income taxes, net of refunds when available. | DirectFact | `IncomeTaxesPaidNet`, `IncomeTaxesPaid` |
| `interestPaidSupplementalData` | Interest Paid Supplemental Data | Flow | Cash paid for interest. | DirectFact | `InterestPaid`, `InterestPaidNet` |
| `stockBasedCompensation` | Stock Based Compensation | Flow | Share-based compensation added back in operating cash-flow reconciliation. | DirectFact | `ShareBasedCompensation`, `AllocatedShareBasedCompensationExpense` |
| `deferredTax` | Deferred Tax | Flow | Deferred income tax expense or benefit in operating cash-flow reconciliation. | DirectFact | `DeferredIncomeTaxExpenseBenefit` |
| `changeInReceivables` | Change in Receivables | Flow | Cash-flow presentation impact of changes in accounts receivable or receivables plus other operating assets; asset increases are shown as cash outflows. | DirectFact → WorkingCapitalAssetIncreaseAsCashOutflow | `IncreaseDecreaseInAccountsReceivable`, `IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets` |
| `changeInInventory` | Change in Inventory | Flow | Cash-flow presentation impact of changes in inventory; inventory increases are shown as cash outflows. | DirectFact → WorkingCapitalAssetIncreaseAsCashOutflow | `IncreaseDecreaseInInventories` |
| `changeInPayables` | Change in Payables | Flow | Change in accounts payable. | DirectFact | `IncreaseDecreaseInAccountsPayable` |
| `changeInAccruedExpense` | Change in Accrued Expense | Flow | Change in accrued liabilities and other operating liabilities. | DirectFact | `IncreaseDecreaseInAccruedLiabilities`, `IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities` |
| `changeInOtherWorkingCapital` | Change in Other Working Capital | Flow | Other working-capital changes reported in operating cash-flow reconciliation. | DirectFact | `IncreaseDecreaseInContractWithCustomerLiability`, `IncreaseDecreaseInOtherNoncurrentAssets` |
| `investingCashFlow` | Investing Cash Flow | Flow | Net cash provided by or used in investing activities. | DirectFact | `NetCashProvidedByUsedInInvestingActivities`, `ifrs-full:CashFlowsFromUsedInInvestingActivities` |
| `purchaseOfPpe` | Purchase of PPE | Flow | Purchases of property, plant, equipment, productive assets, or capital expenditures. | DirectFact | `PaymentsToAcquirePropertyPlantAndEquipment`, `PaymentsToAcquireProductiveAssets`, `PaymentsForCapitalExpenditures` |
| `saleOfPpe` | Sale of PPE | Flow | Proceeds from sale of property, plant, equipment, or productive assets. | DirectFact | `ProceedsFromSaleOfPropertyPlantAndEquipment`, `PaymentsForProceedsFromProductiveAssets` |
| `purchaseOfBusiness` | Purchase of Business | Flow | Cash paid to acquire businesses, net of cash acquired when reported. | DirectFact → DirectTtmFact | `PaymentsToAcquireBusinessesNetOfCashAcquired`, `PaymentsToAcquireBusinessesAndInterestInAffiliates` |
| `purchaseOfInvestment` | Purchase of Investment | Flow | Cash paid to acquire investments or marketable securities. | DirectFact | `PaymentsToAcquireInvestments`, `PaymentsToAcquireMarketableSecurities` |
| `saleOfInvestment` | Sale of Investment | Flow | Proceeds from sales, maturities, or collections of investments and marketable securities. | DirectFact | `ProceedsFromSaleMaturityAndCollectionsOfInvestments`, `ProceedsFromSaleAndMaturityOfMarketableSecurities` |
| `financingCashFlow` | Financing Cash Flow | Flow | Net cash provided by or used in financing activities. | DirectFact | `NetCashProvidedByUsedInFinancingActivities`, `ifrs-full:CashFlowsFromUsedInFinancingActivities` |
| `capex` | CapEx | Flow | Capital expenditures. | DirectFact | `PaymentsToAcquirePropertyPlantAndEquipment`, `PaymentsForCapitalExpenditures`, `PaymentsToAcquireProductiveAssets`, `ifrs-full:PurchaseOfPropertyPlantAndEquipmentClassifiedAsInvestingActivities` |
| `freeCashFlow` | Free Cash Flow | Flow | Operating cash flow less CapEx. | OperatingCashFlowMinusCapex |  |
| `issuanceOfCapitalStock` | Issuance of Capital Stock | Flow | Cash proceeds from issuing capital stock. | DirectFact | `ProceedsFromIssuanceOfCommonStock`, `ProceedsFromStockOptionsExercised`, `ifrs-full:ProceedsFromIssuingShares` |
| `issuanceOfDebt` | Issuance of Debt | Flow | Cash proceeds from issuing debt. | DirectFact → DebtIssuanceLongTermPlusShortTerm | `ProceedsFromIssuanceOfDebt`, `ProceedsFromBorrowings`, `ifrs-full:ProceedsFromBorrowings` |
| `repaymentOfDebt` | Repayment of Debt | Flow | Cash repayments of debt, including identifiable finance lease interest payments when separately reported. | DirectFact → DebtRepaymentLongTermPlusShortTermPlusFinanceLeasePrincipalPlusFinanceLeaseInterest → DebtRepaymentLongTermPlusShortTermPlusFinanceLeasePrincipal | `RepaymentsOfDebt`, `RepaymentsOfBorrowings`, `ifrs-full:RepaymentsOfBorrowings` |
| `longTermDebtIssuance` | Long Term Debt Issuance | Flow | Cash proceeds from long-term debt issuance. | DirectFact | `ProceedsFromIssuanceOfLongTermDebt` |
| `shortTermDebtIssuance` | Short Term Debt Issuance | Flow | Cash proceeds from short-term debt issuance. | DirectFact | `ProceedsFromShortTermDebt` |
| `longTermDebtPayments` | Long Term Debt Payments | Flow | Cash repayments of long-term debt. | DirectFact | `RepaymentsOfLongTermDebt`, `RepaymentsOfLongTermDebtAndCapitalSecurities` |
| `shortTermDebtPayments` | Short Term Debt Payments | Flow | Cash repayments of short-term debt. | DirectFact | `RepaymentsOfShortTermDebt` |
| `financeLeasePrincipalPayments` | Finance Lease Principal Payments | Flow | Finance lease principal repayments. | DirectFact | `FinanceLeasePrincipalPayments`, `RepaymentsOfLongTermCapitalLeaseObligations` |
| `netIssuancePaymentsOfDebt` | Net Issuance Payments of Debt | Flow | Debt issuance proceeds less debt repayments. | DebtIssuanceMinusRepayment |  |
| `netLongTermDebtIssuance` | Net Long Term Debt Issuance | Flow | Long-term debt issuance proceeds less long-term debt repayments. | LongTermDebtIssuanceMinusRepayment |  |
| `netShortTermDebtIssuance` | Net Short Term Debt Issuance | Flow | Short-term debt issuance proceeds less short-term debt repayments. | ShortTermDebtIssuanceMinusRepayment |  |
| `repurchaseOfCapitalStock` | Repurchase of Capital Stock | Flow | Cash paid to repurchase common stock or equity. | DirectFact | `PaymentsForRepurchaseOfCommonStock`, `PaymentsForRepurchaseOfEquity` |
| `dividendsPaid` | Dividends Paid | Flow | Cash dividends paid to common (and total) shareholders in the financing section. | DirectFact | `PaymentsOfDividendsCommonStock`, `PaymentsOfDividends`, `ifrs-full:DividendsPaidClassifiedAsFinancingActivities`, `ifrs-full:DividendsPaid` |
| `changesInCash` | Changes in Cash | Flow | Period increase/decrease in cash, cash equivalents, restricted cash and equivalents. | DirectFact | `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect`, `CashAndCashEquivalentsPeriodIncreaseDecrease` |
| `effectOfExchangeRateChanges` | Effect of Exchange Rate Changes | Flow | Effect of exchange-rate changes on cash and cash equivalents. | DirectFact | `EffectOfExchangeRateOnCashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents`, `EffectOfExchangeRateOnCashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsIncludingDisposalGroupAndDiscontinuedOperations`, `EffectOfExchangeRateOnCashAndCashEquivalents` |
| `endCashPosition` | End Cash Position | Point | Cash and cash equivalents at the period end shown in the cash-flow statement. | DirectFact | `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents`, `CashAndCashEquivalentsAtCarryingValue`, `ifrs-full:CashAndCashEquivalents` |

## Balance Sheet

| Id | Name | Kind | Definition | Sources | SEC concepts |
|---|---|---|---|---|---|
| `totalAssets` | Total Assets | Point | Total assets. | DirectFact | `Assets`, `ifrs-full:Assets` |
| `currentAssets` | Current Assets | Point | Current assets. | DirectFact | `AssetsCurrent` |
| `cashAndShortTermEquivalents` | Cash & Short-Term Equivalents | Point | Cash, cash equivalents and short-term investments. | DirectFact → CashPlusShortTermInvestments | `CashCashEquivalentsAndShortTermInvestments`, `CashAndCashEquivalentsAtCarryingValue`, `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents`, `ifrs-full:CashAndCashEquivalents`, … |
| `cashAndCashEquivalents` | Cash And Cash Equivalents | Point | Cash and cash equivalents, excluding short-term investments when separately reported. | DirectFact | `CashAndCashEquivalentsAtCarryingValue`, `Cash`, `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents` |
| `otherShortTermInvestments` | Other Short Term Investments | Point | Short-term investments, directly reported or derived from cash/STI less cash. | DirectFact → CashAndShortTermInvestmentsMinusCash | `ShortTermInvestments`, `MarketableSecuritiesCurrent` |
| `receivables` | Receivables | Point | Accounts receivable, net current. | DirectFact | `AccountsReceivableNetCurrent` |
| `accountsReceivable` | Accounts Receivable | Point | Accounts receivable, net current. | DirectFact | `AccountsReceivableNetCurrent` |
| `grossAccountsReceivable` | Gross Accounts Receivable | Point | Gross accounts receivable, directly reported or net receivables plus allowance. | DirectFact → NetReceivablesPlusAllowance | `AccountsReceivableGrossCurrent` |
| `allowanceForDoubtfulAccounts` | Allowance For Doubtful Accounts | Point | Allowance for doubtful accounts receivable. | DirectFact | `AllowanceForDoubtfulAccountsReceivableCurrent`, `AllowanceForDoubtfulAccountsReceivable` |
| `inventory` | Inventory | Point | Inventory, net. | DirectFact | `InventoryNet`, `Inventory` |
| `otherCurrentAssets` | Other Current Assets | Point | Other current assets. | DirectFact | `PrepaidExpenseAndOtherAssetsCurrent`, `OtherAssetsCurrent`, `OtherRestrictedAssetsCurrent` |
| `totalNonCurrentAssets` | Total Non Current Assets | Point | Non-current assets, directly reported or total assets less current assets. | DirectFact → TotalAssetsMinusCurrentAssets | `AssetsNoncurrent` |
| `netPpe` | Net PPE | Point | Property, plant and equipment plus finance and operating lease right-of-use assets, net of accumulated depreciation and amortization. | DirectFact → NetPpePlusOperatingLeaseRightOfUseAsset | `PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetAfterAccumulatedDepreciationAndAmortization`, `PropertyPlantAndEquipmentNet` |
| `grossPpe` | Gross PPE | Point | Property, plant and equipment before accumulated depreciation and amortization. | DirectFact | `PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetBeforeAccumulatedDepreciationAndAmortization`, `PropertyPlantAndEquipmentGross` |
| `accumulatedDepreciationPpe` | Accumulated Depreciation | Point | Accumulated depreciation and amortization for property, plant and equipment. | DirectFact | `PropertyPlantAndEquipmentAndFinanceLeaseRightOfUseAssetAccumulatedDepreciationAndAmortization`, `AccumulatedDepreciationDepletionAndAmortizationPropertyPlantAndEquipment` |
| `goodwill` | Goodwill | Point | Goodwill. | DirectFact | `Goodwill` |
| `otherIntangibleAssets` | Other Intangible Assets | Point | Intangible assets excluding goodwill. | DirectFact | `FiniteLivedIntangibleAssetsNet`, `IntangibleAssetsNetExcludingGoodwill`, `IndefiniteLivedIntangibleAssetsExcludingGoodwill` |
| `otherNonCurrentAssets` | Other Non Current Assets | Point | Other non-current assets. | DirectFact | `OtherAssetsNoncurrent`, `OtherNoncurrentAssets` |
| `totalLiabilities` | Total Liabilities | Point | Total liabilities. | DirectFact → TotalAssetsMinusTotalEquity | `Liabilities`, `ifrs-full:Liabilities` |
| `currentLiabilities` | Current Liabilities | Point | Current liabilities. | DirectFact | `LiabilitiesCurrent` |
| `payablesAndAccruedExpenses` | Payables And Accrued Expenses | Point | Accounts payable plus accrued liabilities. | DirectFact → AccountsPayablePlusAccruedLiabilities | `AccountsPayableAndAccruedLiabilitiesCurrent` |
| `payables` | Payables | Point | Accounts payable. | DirectFact | `AccountsPayableCurrent`, `AccountsPayable` |
| `accountsPayable` | Accounts Payable | Point | Accounts payable. | DirectFact | `AccountsPayableCurrent`, `AccountsPayable` |
| `currentAccruedExpense` | Current Accrued Expense | Point | Current accrued liabilities. | DirectFact | `AccruedLiabilitiesCurrent`, `AccruedLiabilities` |
| `currentDeferredLiabilities` | Current Deferred Liabilities | Point | Current deferred revenue / contract liabilities. | DirectFact | `ContractWithCustomerLiabilityCurrent`, `DeferredRevenueCurrent` |
| `currentDeferredRevenue` | Current Deferred Revenue | Point | Current deferred revenue / contract liabilities. | DirectFact | `ContractWithCustomerLiabilityCurrent`, `DeferredRevenueCurrent` |
| `totalNonCurrentLiabilities` | Total Non Current Liabilities | Point | Non-current liabilities, directly reported or total liabilities less current liabilities. | DirectFact → LiabilitiesMinusCurrentLiabilities | `LiabilitiesNoncurrent` |
| `longTermDebtAndCapitalLease` | Long Term Debt And Capital Lease | Point | Non-current long-term debt plus non-current capital/finance lease obligations. | DirectFact → LongTermDebtPlusCapitalLeaseNoncurrent | `LongTermDebtAndFinanceLeaseObligationsNoncurrent` |
| `longTermDebt` | Long Term Debt | Point | Long-term debt, non-current. | DirectFact | `LongTermDebtNoncurrent`, `LongTermDebt` |
| `currentDebt` | Short-Term Debt / Current Portion of Long-Term Debt | Point | Short-term borrowings and the current portion of long-term debt shown on the balance sheet. | DirectFact | `DebtCurrent`, `ShortTermBorrowings`, `LongTermDebtCurrent` |
| `longTermCapitalLeaseObligations` | Long Term Capital Lease Obligations | Point | Non-current capital/finance lease obligations. | DirectFact | `FinanceLeaseLiabilityNoncurrent`, `CapitalLeaseObligationsNoncurrent` |
| `deferredTaxLiabilities` | Deferred Tax Liabilities | Point | Non-current deferred tax liabilities. | DirectFact | `DeferredTaxLiabilitiesNoncurrent`, `DeferredIncomeTaxLiabilitiesNet`, `ifrs-full:DeferredTaxLiabilities` |
| `otherCurrentLiabilities` | Other Current Liabilities | Point | Other current liabilities not itemized in a dedicated row. | DirectFact | `OtherLiabilitiesCurrent` |
| `otherNonCurrentLiabilities` | Other Non Current Liabilities | Point | Other non-current liabilities. | DirectFact | `OtherLiabilitiesNoncurrent`, `OtherNoncurrentLiabilities`, `OtherLiabilitiesAndDeferredRevenueNoncurrent` |
| `minorityInterest` | Noncontrolling Interest | Point | Equity attributable to noncontrolling (minority) interests. | DirectFact | `MinorityInterest`, `ifrs-full:NoncontrollingInterests` |
| `mezzanineEquity` | Temporary (Mezzanine) Equity | Point | Temporary/mezzanine equity — redeemable preferred stock or redeemable noncontrolling interest, carried between liabilities and permanent equity, so a full balance sheet foots as Assets = Liabilities + Mezzanine + Equity. | DirectFact | `TemporaryEquityCarryingAmountIncludingPortionAttributableToNoncontrollingInterests`, `TemporaryEquityCarryingAmountAttributableToParent`, `RedeemableNoncontrollingInterestEquityCarryingAmount` |
| `longTermInvestments` | Long-Term Investments | Point | Non-current investments and long-term marketable securities. | DirectFact | `LongTermInvestments`, `MarketableSecuritiesNoncurrent` |
| `totalEquityGrossMinorityInterest` | Total Equity Gross Minority Interest | Point | Total equity including any non-controlling interest. | DirectFact | `StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest`, `StockholdersEquity`, `ifrs-full:Equity`, `ifrs-full:EquityAttributableToOwnersOfParent` |
| `commonStockEquity` | Common Stock Equity | Point | Equity attributable to common stockholders / parent owners. | DirectFact | `StockholdersEquity`, `StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest`, `ifrs-full:EquityAttributableToOwnersOfParent`, `ifrs-full:Equity` |
| `capitalStock` | Capital Stock | Point | Preferred plus common stock value when separately reported; direct capital stock otherwise. | DirectFact | `CapitalStock`, `CommonStockValue` |
| `preferredStock` | Preferred Stock | Point | Preferred stock value. | DirectFact | `PreferredStockValue` |
| `commonStock` | Common Stock | Point | Common stock value. | DirectFact | `CommonStockValue` |
| `additionalPaidInCapital` | Additional Paid In Capital | Point | Additional paid-in capital. | DirectFact | `AdditionalPaidInCapital` |
| `retainedEarnings` | Retained Earnings | Point | Retained earnings / accumulated deficit. | DirectFact | `RetainedEarningsAccumulatedDeficit` |
| `treasuryStock` | Treasury Stock | Point | Treasury stock value. | DirectFact | `TreasuryStockCommonValue`, `TreasuryStockValue`, `TreasuryStockValueAcquiredCostMethod` |
| `gainsLossesNotAffectingRetainedEarnings` | Gains Losses Not Affecting Retained Earnings | Point | Accumulated other comprehensive income/loss. | DirectFact | `AccumulatedOtherComprehensiveIncomeLossNetOfTax` |
| `otherEquityAdjustments` | Other Equity Adjustments | Point | Other equity adjustments, currently mapped to accumulated other comprehensive income/loss when reported. | DirectFact | `AccumulatedOtherComprehensiveIncomeLossNetOfTax` |
| `capitalLeaseObligations` | Capital Lease Obligations | Point | Finance/capital lease obligations. | DirectFact → CapitalLeaseCurrentPlusNoncurrent | `FinanceLeaseLiability`, `CapitalLeaseObligations`, `ifrs-full:LeaseLiabilities` |
| `netTangibleAssets` | Net Tangible Assets | Point | Common stock equity less goodwill and intangible assets. | CommonStockEquityMinusGoodwillAndIntangibles |  |
| `workingCapital` | Working Capital | Point | Current assets less current liabilities. | CurrentAssetsMinusCurrentLiabilities |  |
| `tangibleBookValue` | Tangible Book Value | Point | Same SEC-backed value as net tangible assets. | CommonStockEquityMinusGoodwillAndIntangibles |  |
| `totalDebt` | Total Debt | Point | Short-term plus long-term debt and finance lease obligations. | DirectFact → DebtCurrentPlusLongTermDebtPlusFinanceLease → DebtLongTermPlusFinanceLease → DebtCurrentPlusLongTermDebt | `DebtAndFinanceLeaseObligations`, `DebtAndCapitalLeaseObligations`, `DebtLongtermAndShorttermCombinedAmount`, `ifrs-full:Borrowings`, … |
| `netDebt` | Net Debt | Point | Total debt less cash and short-term investments. | TotalDebtMinusCashAndShortTermInvestments |  |

## Shares

| Id | Name | Kind | Definition | Sources | SEC concepts |
|---|---|---|---|---|---|
| `sharesOutstanding` | Shares Outstanding | Point | Common shares outstanding. | DirectFact | `CommonStockSharesOutstanding`, `dei:EntityCommonStockSharesOutstanding`, `ifrs-full:NumberOfSharesOutstanding`, `ifrs-full:NumberOfSharesIssued`, … |
| `shareIssued` | Share Issued | Point | Common shares issued. | DirectFact | `CommonStockSharesIssued`, `ifrs-full:NumberOfSharesIssued` |
| `ordinarySharesNumber` | Ordinary Shares Number | Point | Ordinary/common shares outstanding. | DirectFact | `CommonStockSharesOutstanding`, `dei:EntityCommonStockSharesOutstanding`, `ifrs-full:NumberOfSharesOutstanding`, … |
| `treasuryShares` | Treasury Shares Number | Point | Treasury shares, derived from issued shares less outstanding shares. | CommonStockSharesIssuedMinusOutstanding |  |

