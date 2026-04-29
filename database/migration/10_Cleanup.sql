-- ============================================================
-- IngenIA365ERP — Migration Step 10: Post-Migration Cleanup
-- Run ONLY after validation (Step 09) passes successfully.
-- ============================================================
SET NOCOUNT ON;
PRINT N'============================================================';
PRINT N'  Step 10: Post-Migration Cleanup';
PRINT N'  Started: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
PRINT N'';
GO

-- ============================================================
-- 1. Update statistics on all new tables
-- ============================================================
PRINT N'[1/6] Updating statistics on all tables...';
EXEC sp_MSforeachtable @command1 = N'UPDATE STATISTICS ?';
PRINT N'  Statistics updated.';
GO

-- ============================================================
-- 2. Rebuild indexes (may be fragmented after bulk inserts)
-- ============================================================
PRINT N'';
PRINT N'[2/6] Rebuilding indexes...';

-- Use try/catch per table so ONLINE=ON failures (e.g. LOB columns) fall back gracefully
DECLARE @rebuildSql NVARCHAR(MAX) = N'';

SELECT @rebuildSql = @rebuildSql +
    N'BEGIN TRY ALTER INDEX ALL ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) +
    N' REBUILD WITH (ONLINE = ON, FILLFACTOR = 90); END TRY ' +
    N'BEGIN CATCH ALTER INDEX ALL ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) +
    N' REBUILD WITH (FILLFACTOR = 90); END CATCH; ' + CHAR(13) + CHAR(10)
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = N'dbo'
  AND t.name LIKE N'[A-Z][A-Z][A-Z]_%'  -- Only new-schema tables (COR_, ACC_, LND_, etc.)
ORDER BY t.name;

EXEC sp_executesql @rebuildSql;

PRINT N'  Indexes rebuilt.';
GO

-- ============================================================
-- 3. Verify no orphaned mapping table entries
-- ============================================================
PRINT N'';
PRINT N'[3/6] Verifying mapping table integrity...';

DECLARE @mapOrphans INT = 0;

SELECT @mapOrphans = @mapOrphans + COUNT(*)
FROM [migration].[Map_People] mp
LEFT JOIN [dbo].[COR_People] p ON mp.NewPersonId = p.Id
WHERE p.Id IS NULL;

SELECT @mapOrphans = @mapOrphans + COUNT(*)
FROM [migration].[Map_ChartOfAccounts] mc
LEFT JOIN [dbo].[ACC_ChartOfAccounts] ca ON mc.NewAccountId = ca.Id
WHERE ca.Id IS NULL;

SELECT @mapOrphans = @mapOrphans + COUNT(*)
FROM [migration].[Map_LoanPortfolios] ml
LEFT JOIN [dbo].[LND_LoanPortfolios] lp ON ml.NewLoanPortfolioId = lp.Id
WHERE lp.Id IS NULL;

IF @mapOrphans > 0
    PRINT N'  WARNING: ' + CAST(@mapOrphans AS NVARCHAR) + N' orphaned mapping entries found.';
ELSE
    PRINT N'  Mapping tables verified: 0 orphans.';
GO

-- ============================================================
-- 4. Optionally drop mapping tables
-- UNCOMMENT when confident migration is complete and verified.
-- Keep for at least 30 days post-migration for troubleshooting.
-- ============================================================
PRINT N'';
PRINT N'[4/6] Mapping table cleanup (currently preserved)...';
PRINT N'  Mapping tables are PRESERVED for reference.';
PRINT N'  Uncomment the DROP statements below when ready.';

/*
-- Phase 1: Drop mapping tables (after 30 days)
IF OBJECT_ID('migration.Map_Committees', 'U') IS NOT NULL DROP TABLE [migration].[Map_Committees];
IF OBJECT_ID('migration.Map_EmployerCompanies', 'U') IS NOT NULL DROP TABLE [migration].[Map_EmployerCompanies];
IF OBJECT_ID('migration.Map_CDTCertificates', 'U') IS NOT NULL DROP TABLE [migration].[Map_CDTCertificates];
IF OBJECT_ID('migration.Map_Products', 'U') IS NOT NULL DROP TABLE [migration].[Map_Products];
IF OBJECT_ID('migration.Map_Employees', 'U') IS NOT NULL DROP TABLE [migration].[Map_Employees];
IF OBJECT_ID('migration.Map_DepositAccounts', 'U') IS NOT NULL DROP TABLE [migration].[Map_DepositAccounts];
IF OBJECT_ID('migration.Map_SavingsAccounts', 'U') IS NOT NULL DROP TABLE [migration].[Map_SavingsAccounts];
IF OBJECT_ID('migration.Map_CreditLines', 'U') IS NOT NULL DROP TABLE [migration].[Map_CreditLines];
IF OBJECT_ID('migration.Map_VoucherTypes', 'U') IS NOT NULL DROP TABLE [migration].[Map_VoucherTypes];
IF OBJECT_ID('migration.Map_Banks', 'U') IS NOT NULL DROP TABLE [migration].[Map_Banks];
IF OBJECT_ID('migration.Map_CostCenters', 'U') IS NOT NULL DROP TABLE [migration].[Map_CostCenters];
IF OBJECT_ID('migration.Map_Branches', 'U') IS NOT NULL DROP TABLE [migration].[Map_Branches];
IF OBJECT_ID('migration.Map_LoanPortfolios', 'U') IS NOT NULL DROP TABLE [migration].[Map_LoanPortfolios];
IF OBJECT_ID('migration.Map_ChartOfAccounts', 'U') IS NOT NULL DROP TABLE [migration].[Map_ChartOfAccounts];
IF OBJECT_ID('migration.Map_People', 'U') IS NOT NULL DROP TABLE [migration].[Map_People];
IF OBJECT_ID('migration.Map_Cities', 'U') IS NOT NULL DROP TABLE [migration].[Map_Cities];
IF OBJECT_ID('migration.Map_Relationships', 'U') IS NOT NULL DROP TABLE [migration].[Map_Relationships];
IF OBJECT_ID('migration.Map_Sections', 'U') IS NOT NULL DROP TABLE [migration].[Map_Sections];
IF OBJECT_ID('migration.Map_Professions', 'U') IS NOT NULL DROP TABLE [migration].[Map_Professions];
IF OBJECT_ID('migration.Map_Positions', 'U') IS NOT NULL DROP TABLE [migration].[Map_Positions];
IF OBJECT_ID('migration.Map_WithdrawalReasons', 'U') IS NOT NULL DROP TABLE [migration].[Map_WithdrawalReasons];
IF OBJECT_ID('migration.Map_Advisors', 'U') IS NOT NULL DROP TABLE [migration].[Map_Advisors];
PRINT N'  Mapping tables dropped.';
*/

/*
-- Phase 2: Drop migration log (after 90 days)
IF OBJECT_ID('migration.MigrationLog', 'U') IS NOT NULL DROP TABLE [migration].[MigrationLog];
PRINT N'  Migration log dropped.';
*/

/*
-- Phase 3: Drop migration schema (after 90 days, only if all tables dropped)
DROP SCHEMA [migration];
PRINT N'  Migration schema dropped.';
*/
GO

-- ============================================================
-- 5. Remove LegacyCode / LegacyLogin columns (optional)
-- UNCOMMENT after 6+ months when no code references legacy codes.
-- ============================================================
PRINT N'';
PRINT N'[5/6] Legacy column cleanup (currently preserved)...';
PRINT N'  Legacy columns are PRESERVED for backward compatibility.';
PRINT N'  Uncomment the ALTER TABLE statements below after 6 months.';

/*
-- COR_ legacy columns
ALTER TABLE [dbo].[COR_People] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Branches] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Banks] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_CostCenters] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Companies] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Sections] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Professions] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Positions] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_Relationships] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_EmployerCompanies] DROP COLUMN LegacyCode;
ALTER TABLE [dbo].[COR_WithdrawalReasons] DROP COLUMN LegacyCode;

-- ACC_ legacy columns
ALTER TABLE [dbo].[ACC_VoucherTypes] DROP COLUMN LegacyCode;

-- LND_ legacy columns
ALTER TABLE [dbo].[LND_LoanPortfolios] DROP COLUMN LegacyCodigoTer;

-- SEC_ legacy columns
ALTER TABLE [dbo].[SEC_Users] DROP COLUMN LegacyLogin;
ALTER TABLE [dbo].[SEC_Users] DROP COLUMN LegacyPassword;

-- WEB_ legacy columns
ALTER TABLE [dbo].[WEB_LoanApplications] DROP COLUMN LegacyCodigoTer;

PRINT N'  Legacy columns dropped.';
*/
GO

-- ============================================================
-- 6. Generate final migration report
-- ============================================================
PRINT N'';
PRINT N'[6/6] Generating final migration report...';
PRINT N'';

-- Log this step
INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
VALUES (10, N'_CLEANUP_', N'(maintenance)', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'Success');

-- Summary by step
PRINT N'============================================================';
PRINT N'  MIGRATION REPORT';
PRINT N'============================================================';
PRINT N'';

SELECT
    StepNumber,
    COUNT(*) AS TablesProcessed,
    SUM(RowsMigrated) AS TotalRows,
    MIN(StartedAt) AS FirstStarted,
    MAX(CompletedAt) AS LastCompleted,
    DATEDIFF(SECOND, MIN(StartedAt), MAX(CompletedAt)) AS DurationSeconds,
    SUM(CASE WHEN Status = N'Success' THEN 1 ELSE 0 END) AS Succeeded,
    SUM(CASE WHEN Status = N'Failed' THEN 1 ELSE 0 END) AS Failed,
    STRING_AGG(CASE WHEN Status = N'Failed' THEN TableName ELSE NULL END, N', ') AS FailedTables
FROM [migration].[MigrationLog]
GROUP BY StepNumber
ORDER BY StepNumber;

-- Overall totals
PRINT N'';
PRINT N'------------------------------------------------------------';

DECLARE @totalRows    BIGINT;
DECLARE @totalTables  INT;
DECLARE @failedTables INT;
DECLARE @firstStart   NVARCHAR(30);
DECLARE @lastEnd      NVARCHAR(30);

SELECT
    @totalRows    = ISNULL(SUM(RowsMigrated), 0),
    @totalTables  = COUNT(DISTINCT CASE WHEN TableName NOT LIKE N'_%' THEN TableName END),
    @failedTables = SUM(CASE WHEN Status = N'Failed' THEN 1 ELSE 0 END),
    @firstStart   = CONVERT(NVARCHAR(30), MIN(StartedAt), 120),
    @lastEnd      = CONVERT(NVARCHAR(30), MAX(CompletedAt), 120)
FROM [migration].[MigrationLog];

PRINT N'  Total rows migrated:    ' + CAST(@totalRows AS NVARCHAR);
PRINT N'  Total tables processed: ' + CAST(@totalTables AS NVARCHAR);
PRINT N'  Failed tables:          ' + CAST(@failedTables AS NVARCHAR);
PRINT N'  Migration started:      ' + @firstStart;
PRINT N'  Migration completed:    ' + @lastEnd;

IF @failedTables > 0
BEGIN
    PRINT N'';
    PRINT N'  WARNING: ' + CAST(@failedTables AS NVARCHAR) + N' table(s) failed migration:';
    SELECT TableName, SourceTable, ErrorMessage
    FROM [migration].[MigrationLog]
    WHERE Status = N'Failed';
END;

PRINT N'';
PRINT N'============================================================';

IF @failedTables = 0
    PRINT N'  MIGRATION COMPLETE — ALL STEPS PASSED';
ELSE
    PRINT N'  MIGRATION COMPLETE — WITH FAILURES (see above)';

PRINT N'============================================================';
PRINT N'';
PRINT N'  Finished: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
GO
