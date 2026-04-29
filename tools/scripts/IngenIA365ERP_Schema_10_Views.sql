-- ============================================================
-- IngenIA365ERP — Views (20 most used)
-- Generated: 2026-03-20
-- Each view maps to a legacy view or common report query
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- 1. VW_COR_PeopleFullProfile
-- Full profile: COR_People + COR_Associates + COR_PeopleFinancial
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_COR_PeopleFullProfile]
AS
SELECT
    p.[Id]                      AS PersonId,
    p.[IdentificationNumber],
    p.[IdentificationType],
    p.[FirstName],
    p.[MiddleName],
    p.[LastName],
    p.[SecondLastName],
    RTRIM(p.[FirstName] + ' ' + ISNULL(p.[MiddleName] + ' ', '') + p.[LastName] + ' ' + ISNULL(p.[SecondLastName], '')) AS FullName,
    p.[BirthDate],
    p.[Gender],
    p.[Email],
    p.[PhoneNumber],
    p.[MobileNumber],
    p.[Address],
    c.[Name]                    AS CityName,
    d.[Name]                    AS DepartmentName,
    co.[Name]                   AS CountryName,
    a.[Id]                      AS AssociateId,
    a.[JoinDate],
    a.[WithdrawalDate],
    a.[Status]                  AS AssociateStatus,
    a.[IsActive]                AS AssociateIsActive,
    a.[LegacyCode]              AS AssociateLegacyCode,
    pf.[MonthlyIncome],
    pf.[MonthlyExpenses],
    pf.[TotalAssets],
    pf.[TotalLiabilities],
    pf.[NetWorth],
    p.[IsDeleted],
    p.[CreatedAt]
FROM [dbo].[COR_People] p
LEFT JOIN [dbo].[COR_Associates] a        ON a.[PersonId] = p.[Id]
LEFT JOIN [dbo].[COR_PeopleFinancial] pf  ON pf.[PersonId] = p.[Id]
LEFT JOIN [dbo].[COR_Cities] c            ON c.[Id] = p.[CityId]
LEFT JOIN [dbo].[COR_Departments] d       ON d.[Id] = c.[DepartmentId]
LEFT JOIN [dbo].[COR_Countries] co        ON co.[Id] = d.[CountryId]
WHERE p.[IsDeleted] = 0;
GO

-- ============================================================
-- 2. VW_LND_PortfolioSummary (replaces cop_saldos_vw)
-- Loan portfolio with person name and credit line info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_PortfolioSummary]
AS
SELECT
    lp.[Id]                     AS LoanPortfolioId,
    lp.[LoanNumber],
    lp.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    cl.[Name]                   AS CreditLineName,
    cl.[Modality],
    lp.[OriginalAmount],
    lp.[CurrentBalance],
    lp.[InterestRate],
    lp.[TermMonths],
    lp.[DisbursementDate],
    lp.[MaturityDate],
    lp.[DaysOverdue],
    lp.[OverdueAmount],
    lp.[Status],
    lp.[BranchId],
    b.[Name]                    AS BranchName,
    lp.[LegacyCode]
FROM [dbo].[LND_LoanPortfolios] lp
INNER JOIN [dbo].[COR_People] p                    ON p.[Id] = lp.[PersonId]
INNER JOIN [dbo].[LND_CreditLineParameters] cl     ON cl.[Id] = lp.[CreditLineId]
LEFT  JOIN [dbo].[COR_Branches] b                  ON b.[Id] = lp.[BranchId];
GO

-- ============================================================
-- 3. VW_LND_PortfolioArrears (replaces cop_copmora_vw)
-- Only portfolios with DaysOverdue > 0
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_PortfolioArrears]
AS
SELECT
    lp.[Id]                     AS LoanPortfolioId,
    lp.[LoanNumber],
    lp.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    cl.[Name]                   AS CreditLineName,
    cl.[Modality],
    lp.[CurrentBalance],
    lp.[DaysOverdue],
    lp.[OverdueAmount],
    lp.[Status],
    [dbo].[FN_LND_CreditRatingByCifin](lp.[DaysOverdue], cl.[Modality]) AS CifinRating,
    [dbo].[FN_LND_DefaultDaysRating](lp.[DaysOverdue], lp.[CurrentBalance]) AS DefaultRating,
    [dbo].[FN_LND_DefaultAgeRating](lp.[DaysOverdue]) AS AgeRating,
    lp.[BranchId],
    b.[Name]                    AS BranchName
FROM [dbo].[LND_LoanPortfolios] lp
INNER JOIN [dbo].[COR_People] p                    ON p.[Id] = lp.[PersonId]
INNER JOIN [dbo].[LND_CreditLineParameters] cl     ON cl.[Id] = lp.[CreditLineId]
LEFT  JOIN [dbo].[COR_Branches] b                  ON b.[Id] = lp.[BranchId]
WHERE lp.[DaysOverdue] > 0;
GO

-- ============================================================
-- 4. VW_LND_PayrollDeductionsSummary (replaces cop_nomdes_vw)
-- Payroll deductions aggregated by person
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_PayrollDeductionsSummary]
AS
SELECT
    pd.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    COUNT(DISTINCT pd.[LoanPortfolioId])       AS ActiveLoanCount,
    SUM(pd.[DeductionAmount])                  AS TotalDeductionAmount,
    MIN(pd.[PeriodDate])                       AS EarliestPeriod,
    MAX(pd.[PeriodDate])                       AS LatestPeriod
FROM [dbo].[LND_PayrollDeductions] pd
INNER JOIN [dbo].[COR_People] p ON p.[Id] = pd.[PersonId]
GROUP BY pd.[PersonId], p.[IdentificationNumber], p.[FirstName], p.[LastName];
GO

-- ============================================================
-- 5. VW_ACC_AccountBalanceSummary (replaces cnt_salcuen_vw)
-- Account balances aggregated by account
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_ACC_AccountBalanceSummary]
AS
SELECT
    ab.[AccountId],
    ca.[AccountCode],
    ca.[AccountName],
    ca.[AccountLevel],
    ca.[NatureType],
    SUM(ab.[OpeningBalance])   AS TotalOpeningBalance,
    SUM(ab.[DebitBalance])     AS TotalDebits,
    SUM(ab.[CreditBalance])   AS TotalCredits,
    SUM(ab.[ClosingBalance])  AS TotalClosingBalance,
    ab.[FiscalYear],
    ab.[FiscalMonth]
FROM [dbo].[ACC_AccountBalances] ab
INNER JOIN [dbo].[ACC_ChartOfAccounts] ca ON ca.[Id] = ab.[AccountId]
GROUP BY ab.[AccountId], ca.[AccountCode], ca.[AccountName], ca.[AccountLevel],
         ca.[NatureType], ab.[FiscalYear], ab.[FiscalMonth];
GO

-- ============================================================
-- 6. VW_ACC_JournalEntrySummary (replaces cop_moviresu_vw)
-- Journal entries with account and person names
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_ACC_JournalEntrySummary]
AS
SELECT
    je.[Id]                     AS JournalEntryId,
    je.[TransactionDate],
    je.[VoucherNumber],
    je.[VoucherTypeCode],
    ca.[AccountCode],
    ca.[AccountName],
    je.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    je.[Description],
    je.[DebitAmount],
    je.[CreditAmount],
    je.[BranchId],
    b.[Name]                    AS BranchName,
    je.[CostCenterId],
    cc.[Name]                   AS CostCenterName
FROM [dbo].[ACC_JournalEntries] je
INNER JOIN [dbo].[ACC_ChartOfAccounts] ca  ON ca.[Id] = je.[AccountId]
LEFT  JOIN [dbo].[COR_People] p            ON p.[Id] = je.[PersonId]
LEFT  JOIN [dbo].[COR_Branches] b          ON b.[Id] = je.[BranchId]
LEFT  JOIN [dbo].[COR_CostCenters] cc      ON cc.[Id] = je.[CostCenterId];
GO

-- ============================================================
-- 7. VW_LND_PortfolioClassificationSummary (replaces cop_copclar_vw)
-- Portfolio classifications aggregated
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_PortfolioClassificationSummary]
AS
SELECT
    pc.[ClassificationCode],
    pc.[ClassificationName],
    pc.[ClassificationDate],
    COUNT(*)                    AS LoanCount,
    SUM(lp.[CurrentBalance])   AS TotalBalance,
    SUM(lp.[OverdueAmount])   AS TotalOverdueAmount,
    AVG(lp.[DaysOverdue])     AS AvgDaysOverdue
FROM [dbo].[LND_PortfolioClassifications] pc
INNER JOIN [dbo].[LND_LoanPortfolios] lp ON lp.[Id] = pc.[LoanPortfolioId]
GROUP BY pc.[ClassificationCode], pc.[ClassificationName], pc.[ClassificationDate];
GO

-- ============================================================
-- 8. VW_LND_InstallmentSchedule (replaces cuotas_vw)
-- Pending installments with portfolio and person info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_InstallmentSchedule]
AS
SELECT
    pi.[Id]                     AS InstallmentId,
    pi.[LoanPortfolioId],
    lp.[LoanNumber],
    pi.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    pi.[InstallmentNumber],
    pi.[DueDate],
    pi.[InstallmentAmount],
    pi.[PrincipalAmount],
    pi.[InterestAmount],
    pi.[InsuranceAmount],
    pi.[PaidAmount],
    pi.[RemainingAmount],
    pi.[IsPaid],
    pi.[PaymentDate],
    DATEDIFF(DAY, pi.[DueDate], GETDATE()) AS DaysFromDue
FROM [dbo].[LND_PendingInstallments] pi
INNER JOIN [dbo].[LND_LoanPortfolios] lp  ON lp.[Id] = pi.[LoanPortfolioId]
INNER JOIN [dbo].[COR_People] p            ON p.[Id] = pi.[PersonId];
GO

-- ============================================================
-- 9. VW_PAY_EmployeeFullProfile
-- Employees with person data and provider names
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_PAY_EmployeeFullProfile]
AS
SELECT
    e.[Id]                      AS EmployeeId,
    e.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS FullName,
    p.[Email],
    p.[MobileNumber],
    e.[EmployeeCode],
    e.[Position],
    e.[Department],
    e.[HireDate],
    e.[TerminationDate],
    e.[Salary],
    e.[TransportSubsidy],
    e.[ContractType],
    e.[IsActive],
    cc.[Name]                   AS CostCenterName,
    hip.[Name]                  AS HealthInsuranceName,
    pp.[Name]                   AS PensionProviderName,
    wrp.[Name]                  AS WorkRiskProviderName,
    sp.[Name]                   AS SeveranceProviderName,
    bk.[Name]                   AS BankName,
    e.[BankAccountNumber]
FROM [dbo].[PAY_Employees] e
INNER JOIN [dbo].[COR_People] p                         ON p.[Id] = e.[PersonId]
LEFT  JOIN [dbo].[COR_CostCenters] cc                   ON cc.[Id] = e.[CostCenterId]
LEFT  JOIN [dbo].[PAY_HealthInsuranceProviders] hip      ON hip.[Id] = e.[HealthInsuranceProviderId]
LEFT  JOIN [dbo].[PAY_PensionProviders] pp               ON pp.[Id] = e.[PensionProviderId]
LEFT  JOIN [dbo].[PAY_WorkRiskProviders] wrp             ON wrp.[Id] = e.[WorkRiskProviderId]
LEFT  JOIN [dbo].[PAY_SeveranceProviders] sp             ON sp.[Id] = e.[SeveranceProviderId]
LEFT  JOIN [dbo].[COR_Banks] bk                          ON bk.[Id] = e.[BankId];
GO

-- ============================================================
-- 10. VW_PAY_PayrollLiquidationSummary (replaces nom_liqplan07_vw)
-- Payroll liquidation summary by employee and period
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_PAY_PayrollLiquidationSummary]
AS
SELECT
    pl.[EmployeeId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS EmployeeName,
    pl.[PeriodStartDate],
    pl.[PeriodEndDate],
    pl.[PayrollType],
    SUM(CASE WHEN pl.[TransactionType] = 'E' THEN pl.[Amount] ELSE 0 END) AS TotalEarnings,
    SUM(CASE WHEN pl.[TransactionType] = 'D' THEN pl.[Amount] ELSE 0 END) AS TotalDeductions,
    SUM(CASE WHEN pl.[TransactionType] = 'E' THEN pl.[Amount] ELSE 0 END)
  - SUM(CASE WHEN pl.[TransactionType] = 'D' THEN pl.[Amount] ELSE 0 END) AS NetPay
FROM [dbo].[PAY_PayrollPlanLiquidations] pl
INNER JOIN [dbo].[PAY_Employees] e     ON e.[Id] = pl.[EmployeeId]
INNER JOIN [dbo].[COR_People] p        ON p.[Id] = e.[PersonId]
GROUP BY pl.[EmployeeId], p.[IdentificationNumber], p.[FirstName], p.[LastName],
         pl.[PeriodStartDate], pl.[PeriodEndDate], pl.[PayrollType];
GO

-- ============================================================
-- 11. VW_INV_ProductCatalog
-- Products with group hierarchy
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_INV_ProductCatalog]
AS
SELECT
    pr.[Id]                     AS ProductId,
    pr.[ProductCode],
    pr.[ProductName],
    pr.[Barcode],
    pr.[UnitPrice],
    pr.[Cost],
    pr.[StockQuantity],
    pr.[MinimumStock],
    pr.[TaxRate],
    pr.[IsActive],
    pg.[Name]                   AS ProductGroupName,
    sg.[Name]                   AS SecondaryGroupName,
    prg.[Name]                  AS PrimaryGroupName,
    dt.[Name]                   AS DiscountTypeName,
    pr.[LegacyCode]
FROM [dbo].[INV_Products] pr
LEFT JOIN [dbo].[INV_ProductGroups] pg     ON pg.[Id] = pr.[ProductGroupId]
LEFT JOIN [dbo].[INV_SecondaryGroups] sg   ON sg.[Id] = pg.[SecondaryGroupId]
LEFT JOIN [dbo].[INV_PrimaryGroups] prg    ON prg.[Id] = sg.[PrimaryGroupId]
LEFT JOIN [dbo].[INV_DiscountTypes] dt     ON dt.[Id] = pr.[DiscountTypeId];
GO

-- ============================================================
-- 12. VW_INV_TransactionSummary
-- Inventory transactions with type and product info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_INV_TransactionSummary]
AS
SELECT
    t.[Id]                      AS TransactionId,
    t.[TransactionDate],
    t.[TransactionNumber],
    tt.[Name]                   AS TransactionTypeName,
    tt.[Code]                   AS TransactionTypeCode,
    pr.[ProductCode],
    pr.[ProductName],
    t.[Quantity],
    t.[UnitPrice],
    t.[TotalAmount],
    t.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName
FROM [dbo].[INV_Transactions] t
INNER JOIN [dbo].[INV_TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
INNER JOIN [dbo].[INV_Products] pr         ON pr.[Id] = t.[ProductId]
LEFT  JOIN [dbo].[COR_People] p            ON p.[Id] = t.[PersonId];
GO

-- ============================================================
-- 13. VW_INV_DailySales (replaces inv_cierrecaja_vw)
-- Sales grouped by date for cash register closing
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_INV_DailySales]
AS
SELECT
    CAST(t.[TransactionDate] AS DATE)  AS SaleDate,
    t.[PointOfSaleId],
    t.[ShiftId],
    COUNT(*)                           AS TransactionCount,
    SUM(t.[TotalAmount])              AS GrossSales,
    SUM(t.[TaxAmount])                AS TotalTax,
    SUM(t.[DiscountAmount])           AS TotalDiscounts,
    SUM(t.[TotalAmount] - ISNULL(t.[DiscountAmount], 0)) AS NetSales
FROM [dbo].[INV_Transactions] t
INNER JOIN [dbo].[INV_TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
WHERE tt.[Code] IN ('VTA', 'FAC')  -- Sales and invoice types
GROUP BY CAST(t.[TransactionDate] AS DATE), t.[PointOfSaleId], t.[ShiftId];
GO

-- ============================================================
-- 14. VW_LND_CreditRiskReport (replaces cop_cifin_vw)
-- Loan portfolios with full credit risk classification
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_CreditRiskReport]
AS
SELECT
    lp.[Id]                     AS LoanPortfolioId,
    lp.[LoanNumber],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    cl.[Name]                   AS CreditLineName,
    cl.[Modality],
    lp.[OriginalAmount],
    lp.[CurrentBalance],
    lp.[DaysOverdue],
    lp.[OverdueAmount],
    lp.[DisbursementDate],
    lp.[MaturityDate],
    [dbo].[FN_LND_CreditRatingByCifin](lp.[DaysOverdue], cl.[Modality])     AS CifinRating,
    [dbo].[FN_LND_DefaultDaysRating](lp.[DaysOverdue], lp.[CurrentBalance])  AS DefaultDaysRating,
    [dbo].[FN_LND_DefaultAgeRating](lp.[DaysOverdue])                        AS DefaultAgeRating,
    [dbo].[FN_LND_DefaultAgeRatingCifin](lp.[DaysOverdue])                   AS DefaultAgeRatingCifin,
    [dbo].[FN_LND_CifinStatus](lp.[DaysOverdue], cl.[Modality])              AS CifinStatus,
    lp.[Status]
FROM [dbo].[LND_LoanPortfolios] lp
INNER JOIN [dbo].[COR_People] p                    ON p.[Id] = lp.[PersonId]
INNER JOIN [dbo].[LND_CreditLineParameters] cl     ON cl.[Id] = lp.[CreditLineId]
WHERE lp.[Status] <> 'C';  -- Exclude cancelled
GO

-- ============================================================
-- 15. VW_LND_AssociatePortfolioSummary (replaces cop_superasocia_vw)
-- Associates with all portfolio balances summarized
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_AssociatePortfolioSummary]
AS
SELECT
    a.[Id]                      AS AssociateId,
    a.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    a.[Status]                  AS AssociateStatus,
    -- Loan totals
    ISNULL(loan.LoanCount, 0)              AS ActiveLoanCount,
    ISNULL(loan.TotalLoanBalance, 0)       AS TotalLoanBalance,
    ISNULL(loan.TotalOverdueAmount, 0)     AS TotalOverdueAmount,
    ISNULL(loan.MaxDaysOverdue, 0)         AS MaxDaysOverdue,
    -- Savings totals
    ISNULL(sav.SavingsCount, 0)            AS SavingsAccountCount,
    ISNULL(sav.TotalSavingsBalance, 0)     AS TotalSavingsBalance,
    -- Deposit totals
    ISNULL(dep.DepositCount, 0)            AS DepositAccountCount,
    ISNULL(dep.TotalDepositBalance, 0)     AS TotalDepositBalance
FROM [dbo].[COR_Associates] a
INNER JOIN [dbo].[COR_People] p ON p.[Id] = a.[PersonId]
LEFT JOIN (
    SELECT [PersonId],
           COUNT(*)            AS LoanCount,
           SUM([CurrentBalance]) AS TotalLoanBalance,
           SUM([OverdueAmount])  AS TotalOverdueAmount,
           MAX([DaysOverdue])    AS MaxDaysOverdue
    FROM [dbo].[LND_LoanPortfolios]
    WHERE [Status] NOT IN ('C', 'P')  -- Exclude cancelled and paid-off
    GROUP BY [PersonId]
) loan ON loan.[PersonId] = a.[PersonId]
LEFT JOIN (
    SELECT [PersonId],
           COUNT(*)              AS SavingsCount,
           SUM([CurrentBalance]) AS TotalSavingsBalance
    FROM [dbo].[LND_SavingsAccounts]
    WHERE [IsActive] = 1
    GROUP BY [PersonId]
) sav ON sav.[PersonId] = a.[PersonId]
LEFT JOIN (
    SELECT [PersonId],
           COUNT(*)              AS DepositCount,
           SUM([CurrentBalance]) AS TotalDepositBalance
    FROM [dbo].[LND_DepositAccounts]
    WHERE [IsActive] = 1
    GROUP BY [PersonId]
) dep ON dep.[PersonId] = a.[PersonId];
GO

-- ============================================================
-- 16. VW_ACC_ThirdPartySummary (replaces cnt_supertercero_vw)
-- Third-party accounts with person info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_ACC_ThirdPartySummary]
AS
SELECT
    tpa.[Id]                    AS ThirdPartyAccountId,
    tpa.[AccountId],
    ca.[AccountCode],
    ca.[AccountName],
    tpa.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    tpa.[DebitBalance],
    tpa.[CreditBalance],
    tpa.[CurrentBalance],
    tpa.[FiscalYear],
    b.[Name]                    AS BranchName,
    cc.[Name]                   AS CostCenterName
FROM [dbo].[ACC_ThirdPartyAccounts] tpa
INNER JOIN [dbo].[ACC_ChartOfAccounts] ca  ON ca.[Id] = tpa.[AccountId]
INNER JOIN [dbo].[COR_People] p            ON p.[Id] = tpa.[PersonId]
LEFT  JOIN [dbo].[COR_Branches] b          ON b.[Id] = tpa.[BranchId]
LEFT  JOIN [dbo].[COR_CostCenters] cc      ON cc.[Id] = tpa.[CostCenterId];
GO

-- ============================================================
-- 17. VW_LND_DepositAccountSummary
-- Deposit accounts with person info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_LND_DepositAccountSummary]
AS
SELECT
    da.[Id]                     AS DepositAccountId,
    da.[AccountNumber],
    da.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    da.[AccountType],
    da.[CurrentBalance],
    da.[InterestRate],
    da.[OpenDate],
    da.[LastTransactionDate],
    da.[IsActive],
    da.[LegacyCode]
FROM [dbo].[LND_DepositAccounts] da
INNER JOIN [dbo].[COR_People] p ON p.[Id] = da.[PersonId];
GO

-- ============================================================
-- 18. VW_CDT_CertificateSummary
-- CDT certificates with person info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_CDT_CertificateSummary]
AS
SELECT
    cdt.[Id]                    AS CertificateId,
    cdt.[CertificateNumber],
    cdt.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    cl.[Name]                   AS CreditLineName,
    cdt.[PrincipalAmount],
    cdt.[InterestRate],
    cdt.[IssueDate],
    cdt.[MaturityDate],
    cdt.[TermDays],
    cdt.[AccruedInterest],
    cdt.[Status],
    DATEDIFF(DAY, GETDATE(), cdt.[MaturityDate]) AS DaysToMaturity
FROM [dbo].[CDT_Certificates] cdt
INNER JOIN [dbo].[COR_People] p                    ON p.[Id] = cdt.[PersonId]
LEFT  JOIN [dbo].[LND_CreditLineParameters] cl     ON cl.[Id] = cdt.[CreditLineId];
GO

-- ============================================================
-- 19. VW_TRS_CheckStatus
-- Checks with bank and person info
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_TRS_CheckStatus]
AS
SELECT
    ch.[Id]                     AS CheckId,
    ch.[CheckNumber],
    ch.[CheckDate],
    ch.[Amount],
    ch.[Status],
    ch.[PersonId],
    p.[IdentificationNumber],
    RTRIM(p.[FirstName] + ' ' + p.[LastName]) AS PersonName,
    ch.[BankId],
    bk.[Name]                   AS BankName,
    ch.[BankAccountNumber],
    ch.[Description],
    ch.[VoidDate],
    ch.[ClearDate]
FROM [dbo].[TRS_Checks] ch
INNER JOIN [dbo].[COR_People] p    ON p.[Id] = ch.[PersonId]
INNER JOIN [dbo].[COR_Banks] bk    ON bk.[Id] = ch.[BankId];
GO

-- ============================================================
-- 20. VW_SEC_UserPermissions
-- Users with roles and permissions (flattened)
-- ============================================================
CREATE OR ALTER VIEW [dbo].[VW_SEC_UserPermissions]
AS
SELECT
    u.[Id]                      AS UserId,
    u.[Username],
    u.[Email],
    u.[IsActive]                AS UserIsActive,
    r.[Id]                      AS RoleId,
    r.[Name]                    AS RoleName,
    perm.[Id]                   AS PermissionId,
    perm.[Module],
    perm.[Action],
    perm.[Description]          AS PermissionDescription
FROM [dbo].[SEC_Users] u
INNER JOIN [dbo].[SEC_UserRoles] ur        ON ur.[UserId] = u.[Id]
INNER JOIN [dbo].[SEC_Roles] r             ON r.[Id] = ur.[RoleId]
INNER JOIN [dbo].[SEC_RolePermissions] rp  ON rp.[RoleId] = r.[Id]
INNER JOIN [dbo].[SEC_Permissions] perm    ON perm.[Id] = rp.[PermissionId]
WHERE u.[IsActive] = 1
  AND r.[IsActive] = 1;
GO

PRINT N'Views created successfully. Total: 20 views.';
GO
