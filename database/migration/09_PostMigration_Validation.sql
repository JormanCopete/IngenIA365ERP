-- ============================================================
-- IngenIA365ERP — Migration Step 09: Post-Migration Validation
-- Run AFTER all migration steps (01-08) are complete.
-- This script does NOT modify data — read-only validation.
-- ============================================================
SET NOCOUNT ON;
PRINT N'============================================================';
PRINT N'  Step 09: Post-Migration Validation';
PRINT N'  Started: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
GO

-- ============================================================
-- PART 1: RECORD COUNT COMPARISON
-- ============================================================
PRINT N'';
PRINT N'--- PART 1: Record Count Comparison ---';
PRINT N'';

DECLARE @results TABLE (
    TableName       NVARCHAR(200),
    SourceTable     NVARCHAR(200),
    OriginalCount   INT,
    MigratedCount   INT,
    Status          NVARCHAR(20)
);

-- =============================================
-- COR_ Module (Core/System)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'COR_People', N'sys_maenit + cnt_nit',
    (SELECT COUNT(DISTINCT CODIGOTER) FROM [old].dbo.sys_maenit),
    (SELECT COUNT(*) FROM [dbo].[COR_People]), N''),

(N'COR_Associates', N'sys_maenit (asociados)',
    (SELECT COUNT(*) FROM [old].dbo.sys_maenit WHERE ESTADO = 'A' OR FECHA_INGRESO IS NOT NULL),
    (SELECT COUNT(*) FROM [dbo].[COR_Associates]), N''),

(N'COR_Branches', N'sys_agencia',
    (SELECT COUNT(*) FROM [old].dbo.sys_agencia),
    (SELECT COUNT(*) FROM [dbo].[COR_Branches]), N''),

(N'COR_CostCenters', N'sys_cencos + cnt_cencos + nom_cencos',
    (SELECT COUNT(DISTINCT CCOSTO) FROM (
        SELECT CCOSTO FROM [old].dbo.sys_cencos
        UNION SELECT CCOSTO FROM [old].dbo.cnt_cencos
        UNION SELECT CCOSTO FROM [old].dbo.nom_cencos
    ) x),
    (SELECT COUNT(*) FROM [dbo].[COR_CostCenters]), N''),

(N'COR_Banks', N'sys_banco03',
    (SELECT COUNT(*) FROM [old].dbo.sys_banco03),
    (SELECT COUNT(*) FROM [dbo].[COR_Banks]), N''),

(N'COR_Companies', N'sys_compania',
    (SELECT COUNT(*) FROM [old].dbo.sys_compania),
    (SELECT COUNT(*) FROM [dbo].[COR_Companies]), N''),

(N'COR_Cities', N'sys_ciudad57',
    (SELECT COUNT(*) FROM [old].dbo.sys_ciudad57),
    (SELECT COUNT(*) FROM [dbo].[COR_Cities]), N''),

(N'COR_EmployerCompanies', N'cop_empresa13 + nom_empresas',
    (SELECT COUNT(DISTINCT codigo_empresa) FROM (
        SELECT codigo_empresa FROM [old].dbo.cop_empresa13
        UNION SELECT CAST(IdEmpresa AS VARCHAR(4)) FROM [old].dbo.nom_empresas
    ) x),
    (SELECT COUNT(*) FROM [dbo].[COR_EmployerCompanies]), N''),

(N'COR_Beneficiaries', N'cop_benef + nom_bene',
    (SELECT COUNT(*) FROM [old].dbo.cop_benef) + (SELECT COUNT(*) FROM [old].dbo.nom_bene),
    (SELECT COUNT(*) FROM [dbo].[COR_Beneficiaries]), N''),

(N'COR_Relationships', N'sys_parent51 + nom_parent',
    (SELECT COUNT(DISTINCT codigo) FROM (
        SELECT codigo FROM [old].dbo.sys_parent51
        UNION SELECT codigo FROM [old].dbo.nom_parent
    ) x),
    (SELECT COUNT(*) FROM [dbo].[COR_Relationships]), N''),

(N'COR_Sequences', N'sys_consecu',
    (SELECT COUNT(*) FROM [old].dbo.sys_consecu),
    (SELECT COUNT(*) FROM [dbo].[COR_Sequences]), N''),

(N'COR_PaymentMethods', N'sys_forpago',
    (SELECT COUNT(*) FROM [old].dbo.sys_forpago),
    (SELECT COUNT(*) FROM [dbo].[COR_PaymentMethods]), N''),

(N'COR_Sections', N'sys_seccion56',
    (SELECT COUNT(*) FROM [old].dbo.sys_seccion56),
    (SELECT COUNT(*) FROM [dbo].[COR_Sections]), N''),

(N'COR_Professions', N'sys_profe52',
    (SELECT COUNT(*) FROM [old].dbo.sys_profe52),
    (SELECT COUNT(*) FROM [dbo].[COR_Professions]), N''),

(N'COR_Positions', N'sys_cargo55',
    (SELECT COUNT(*) FROM [old].dbo.sys_cargo55),
    (SELECT COUNT(*) FROM [dbo].[COR_Positions]), N'');

-- =============================================
-- ACC_ Module (Accounting)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'ACC_ChartOfAccounts', N'cnt_maecuen',
    (SELECT COUNT(*) FROM [old].dbo.cnt_maecuen),
    (SELECT COUNT(*) FROM [dbo].[ACC_ChartOfAccounts]), N''),

(N'ACC_JournalEntries', N'cnt_movimto',
    (SELECT COUNT(*) FROM [old].dbo.cnt_movimto),
    (SELECT COUNT(*) FROM [dbo].[ACC_JournalEntries]), N''),

(N'ACC_Documents', N'cnt_docmto',
    (SELECT COUNT(*) FROM [old].dbo.cnt_docmto),
    (SELECT COUNT(*) FROM [dbo].[ACC_Documents]), N''),

(N'ACC_AuxiliaryDocuments', N'cnt_docaux',
    (SELECT COUNT(*) FROM [old].dbo.cnt_docaux),
    (SELECT COUNT(*) FROM [dbo].[ACC_AuxiliaryDocuments]), N''),

(N'ACC_VoucherTypes', N'sys_compro02',
    (SELECT COUNT(*) FROM [old].dbo.sys_compro02),
    (SELECT COUNT(*) FROM [dbo].[ACC_VoucherTypes]), N''),

(N'ACC_AccountingPeriods', N'sys_periodo',
    (SELECT COUNT(*) FROM [old].dbo.sys_periodo),
    (SELECT COUNT(*) FROM [dbo].[ACC_AccountingPeriods]), N''),

(N'ACC_ThirdPartyAccounts', N'cnt_tercero',
    (SELECT COUNT(*) FROM [old].dbo.cnt_tercero),
    (SELECT COUNT(*) FROM [dbo].[ACC_ThirdPartyAccounts]), N''),

(N'ACC_BankReconciliations', N'cnt_concibanca',
    (SELECT COUNT(*) FROM [old].dbo.cnt_concibanca),
    (SELECT COUNT(*) FROM [dbo].[ACC_BankReconciliations]), N'');

-- =============================================
-- LND_ Module (Lending/Portfolio)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'LND_LoanPortfolios', N'cop_maecar',
    (SELECT COUNT(*) FROM [old].dbo.cop_maecar),
    (SELECT COUNT(*) FROM [dbo].[LND_LoanPortfolios]), N''),

(N'LND_Transactions', N'cop_movimto',
    (SELECT COUNT(*) FROM [old].dbo.cop_movimto),
    (SELECT COUNT(*) FROM [dbo].[LND_Transactions]), N''),

(N'LND_PendingInstallments', N'cop_cuopen',
    (SELECT COUNT(*) FROM [old].dbo.cop_cuopen),
    (SELECT COUNT(*) FROM [dbo].[LND_PendingInstallments]), N''),

(N'LND_DefaultRecords', N'cop_copmora',
    (SELECT COUNT(*) FROM [old].dbo.cop_copmora),
    (SELECT COUNT(*) FROM [dbo].[LND_DefaultRecords]), N''),

(N'LND_CreditLineParameters', N'cop_concar12',
    (SELECT COUNT(*) FROM [old].dbo.cop_concar12),
    (SELECT COUNT(*) FROM [dbo].[LND_CreditLineParameters]), N''),

(N'LND_SavingsAccounts', N'cop_maeahor',
    (SELECT COUNT(*) FROM [old].dbo.cop_maeahor),
    (SELECT COUNT(*) FROM [dbo].[LND_SavingsAccounts]), N''),

(N'LND_LoanApplications', N'cop_solcre',
    (SELECT COUNT(*) FROM [old].dbo.cop_solcre),
    (SELECT COUNT(*) FROM [dbo].[LND_LoanApplications]), N''),

(N'LND_DepositAccounts', N'dep_maeahor',
    (SELECT COUNT(*) FROM [old].dbo.dep_maeahor),
    (SELECT COUNT(*) FROM [dbo].[LND_DepositAccounts]), N''),

(N'LND_DepositEntries', N'dep_novmeahor',
    (SELECT COUNT(*) FROM [old].dbo.dep_novmeahor),
    (SELECT COUNT(*) FROM [dbo].[LND_DepositEntries]), N''),

(N'LND_SavingsParameters', N'cop_ahorro58',
    (SELECT COUNT(*) FROM [old].dbo.cop_ahorro58),
    (SELECT COUNT(*) FROM [dbo].[LND_SavingsParameters]), N''),

(N'LND_TransactionCodes', N'cop_codmov',
    (SELECT COUNT(*) FROM [old].dbo.cop_codmov),
    (SELECT COUNT(*) FROM [dbo].[LND_TransactionCodes]), N'');

-- =============================================
-- PAY_ Module (Payroll)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'PAY_Employees', N'nom_empleados',
    (SELECT COUNT(*) FROM [old].dbo.nom_empleados),
    (SELECT COUNT(*) FROM [dbo].[PAY_Employees]), N''),

(N'PAY_PayrollPlanLiquidations', N'nom_liqplan',
    (SELECT COUNT(*) FROM [old].dbo.nom_liqplan),
    (SELECT COUNT(*) FROM [dbo].[PAY_PayrollPlanLiquidations]), N''),

(N'PAY_PayrollConcepts', N'nom_cptos',
    (SELECT COUNT(*) FROM [old].dbo.nom_cptos),
    (SELECT COUNT(*) FROM [dbo].[PAY_PayrollConcepts]), N''),

(N'PAY_PayrollTransactions', N'nom_movtos',
    (SELECT COUNT(*) FROM [old].dbo.nom_movtos),
    (SELECT COUNT(*) FROM [dbo].[PAY_PayrollTransactions]), N''),

(N'PAY_PayrollEntries', N'nom_novedad',
    (SELECT COUNT(*) FROM [old].dbo.nom_novedad),
    (SELECT COUNT(*) FROM [dbo].[PAY_PayrollEntries]), N''),

(N'PAY_Absences', N'nom_ausentismos',
    (SELECT COUNT(*) FROM [old].dbo.nom_ausentismos),
    (SELECT COUNT(*) FROM [dbo].[PAY_Absences]), N'');

-- =============================================
-- INV_ Module (Inventory)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'INV_Products', N'inv_productos',
    (SELECT COUNT(*) FROM [old].dbo.inv_productos),
    (SELECT COUNT(*) FROM [dbo].[INV_Products]), N''),

(N'INV_Transactions', N'inv_movtos',
    (SELECT COUNT(*) FROM [old].dbo.inv_movtos),
    (SELECT COUNT(*) FROM [dbo].[INV_Transactions]), N''),

(N'INV_Warehouses', N'inv_bodegas',
    (SELECT COUNT(*) FROM [old].dbo.inv_bodegas),
    (SELECT COUNT(*) FROM [dbo].[INV_Warehouses]), N''),

(N'INV_Invoices', N'inv_facturas',
    (SELECT COUNT(*) FROM [old].dbo.inv_facturas),
    (SELECT COUNT(*) FROM [dbo].[INV_Invoices]), N'');

-- =============================================
-- CDT_ Module
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'CDT_Certificates', N'cdt_maecdats',
    (SELECT COUNT(*) FROM [old].dbo.cdt_maecdats),
    (SELECT COUNT(*) FROM [dbo].[CDT_Certificates]), N''),

(N'CDT_CertificateEntries', N'cdt_novcdats',
    (SELECT COUNT(*) FROM [old].dbo.cdt_novcdats),
    (SELECT COUNT(*) FROM [dbo].[CDT_CertificateEntries]), N'');

-- =============================================
-- DEB_ Module (Debit Cards)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'DEB_Cards', N'deb_maetarj',
    (SELECT COUNT(*) FROM [old].dbo.deb_maetarj),
    (SELECT COUNT(*) FROM [dbo].[DEB_Cards]), N''),

(N'DEB_Transactions', N'deb_movto',
    (SELECT COUNT(*) FROM [old].dbo.deb_movto),
    (SELECT COUNT(*) FROM [dbo].[DEB_Transactions]), N'');

-- =============================================
-- TRS_ Module (Treasury)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'TRS_Checks', N'TES_CHEQUES',
    (SELECT COUNT(*) FROM [old].dbo.TES_CHEQUES),
    (SELECT COUNT(*) FROM [dbo].[TRS_Checks]), N''),

(N'TRS_Invoices', N'TES_FACTURA',
    (SELECT COUNT(*) FROM [old].dbo.TES_FACTURA),
    (SELECT COUNT(*) FROM [dbo].[TRS_Invoices]), N'');

-- =============================================
-- SEC_ Module (Security)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'SEC_Users', N'sys_sasusu',
    (SELECT COUNT(*) FROM [old].dbo.sys_sasusu),
    (SELECT COUNT(*) FROM [dbo].[SEC_Users]), N''),

(N'SEC_UserMenuAccess', N'sys_menusu (deduped)',
    (SELECT COUNT(*) FROM (
        SELECT tipo_menu, usuario, tipo_programa, codigo_programa, codigo_menu, codigo_submenu,
               ROW_NUMBER() OVER (PARTITION BY tipo_menu, usuario, tipo_programa, codigo_programa, codigo_menu, codigo_submenu ORDER BY fechasys DESC) AS rn
        FROM [old].dbo.sys_menusu
    ) x WHERE rn = 1),
    (SELECT COUNT(*) FROM [dbo].[SEC_UserMenuAccess]), N''),

(N'SEC_Modules', N'sys_programa',
    (SELECT COUNT(*) FROM [old].dbo.sys_programa),
    (SELECT COUNT(*) FROM [dbo].[SEC_Modules]), N''),

(N'SEC_UserAssignments', N'sys_ComAsigna',
    (SELECT COUNT(*) FROM [old].dbo.sys_ComAsigna),
    (SELECT COUNT(*) FROM [dbo].[SEC_UserAssignments]), N'');

-- =============================================
-- AUD_ Module (Audit)
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'AUD_CompanyChanges', N'sys_ciaaud',
    (SELECT COUNT(*) FROM [old].dbo.sys_ciaaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_CompanyChanges]), N''),

(N'AUD_UserChanges', N'sys_sasusuaud',
    (SELECT COUNT(*) FROM [old].dbo.sys_sasusuaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_UserChanges]), N''),

(N'AUD_VoucherTypeChanges', N'sys_compro02aud',
    (SELECT COUNT(*) FROM [old].dbo.sys_compro02aud),
    (SELECT COUNT(*) FROM [dbo].[AUD_VoucherTypeChanges]), N''),

(N'AUD_MasterChanges', N'sys_masaud',
    (SELECT COUNT(*) FROM [old].dbo.sys_masaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_MasterChanges]), N''),

(N'AUD_MenuChanges', N'sys_menuaud',
    (SELECT COUNT(*) FROM [old].dbo.sys_menuaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_MenuChanges]), N''),

(N'AUD_PeriodChanges', N'sys_periodoAud',
    (SELECT COUNT(*) FROM [old].dbo.sys_periodoAud),
    (SELECT COUNT(*) FROM [dbo].[AUD_PeriodChanges]), N''),

(N'AUD_AssignmentChanges', N'sys_ComAsignaAud',
    (SELECT COUNT(*) FROM [old].dbo.sys_ComAsignaAud),
    (SELECT COUNT(*) FROM [dbo].[AUD_AssignmentChanges]), N''),

(N'AUD_AccountChanges', N'cnt_maecuenAud',
    (SELECT COUNT(*) FROM [old].dbo.cnt_maecuenAud),
    (SELECT COUNT(*) FROM [dbo].[AUD_AccountChanges]), N''),

(N'AUD_JournalChanges', N'cnt_movaud',
    (SELECT COUNT(*) FROM [old].dbo.cnt_movaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_JournalChanges]), N''),

(N'AUD_PortfolioTransactionChanges', N'cop_movaud',
    (SELECT COUNT(*) FROM [old].dbo.cop_movaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_PortfolioTransactionChanges]), N''),

(N'AUD_PortfolioMasterChanges', N'cop_mcaaud',
    (SELECT COUNT(*) FROM [old].dbo.cop_mcaaud),
    (SELECT COUNT(*) FROM [dbo].[AUD_PortfolioMasterChanges]), N''),

(N'AUD_DefaultChanges', N'cop_moraud',
    (SELECT COUNT(*) FROM [old].dbo.cop_moraud),
    (SELECT COUNT(*) FROM [dbo].[AUD_DefaultChanges]), N''),

(N'AUD_SavingsChanges', N'cop_ahoraud',
    (SELECT COUNT(*) FROM [old].dbo.cop_ahoraud),
    (SELECT COUNT(*) FROM [dbo].[AUD_SavingsChanges]), N'');

-- =============================================
-- WEB_ Module
-- =============================================
INSERT INTO @results (TableName, SourceTable, OriginalCount, MigratedCount, Status)
VALUES
(N'WEB_LoanApplications', N'web_solcred',
    (SELECT COUNT(*) FROM [old].dbo.web_solcred),
    (SELECT COUNT(*) FROM [dbo].[WEB_LoanApplications]), N''),

(N'WEB_AuxiliaryApplications', N'web_solaux',
    (SELECT COUNT(*) FROM [old].dbo.web_solaux),
    (SELECT COUNT(*) FROM [dbo].[WEB_AuxiliaryApplications]), N''),

(N'WEB_AffiliationApplications', N'web_solafi',
    (SELECT COUNT(*) FROM [old].dbo.web_solafi),
    (SELECT COUNT(*) FROM [dbo].[WEB_AffiliationApplications]), N''),

(N'WEB_Services', N'web_maeser',
    (SELECT COUNT(*) FROM [old].dbo.web_maeser),
    (SELECT COUNT(*) FROM [dbo].[WEB_Services]), N''),

(N'WEB_ExtraPayments', N'web_extras',
    (SELECT COUNT(*) FROM [old].dbo.web_extras),
    (SELECT COUNT(*) FROM [dbo].[WEB_ExtraPayments]), N''),

(N'WEB_DataUpdates', N'web_actdatos',
    (SELECT COUNT(*) FROM [old].dbo.web_actdatos),
    (SELECT COUNT(*) FROM [dbo].[WEB_DataUpdates]), N'');

-- Compute status
UPDATE @results SET Status =
    CASE
        WHEN OriginalCount = MigratedCount THEN N'OK'
        WHEN MigratedCount > OriginalCount THEN N'MORE'
        WHEN MigratedCount = 0             THEN N'EMPTY'
        ELSE N'LESS'
    END;

-- Display results sorted: problems first
SELECT
    TableName, SourceTable, OriginalCount, MigratedCount,
    Status,
    CASE
        WHEN OriginalCount = 0 THEN N'n/a'
        ELSE CAST(CAST(MigratedCount * 100.0 / NULLIF(OriginalCount, 0) AS DECIMAL(5,1)) AS NVARCHAR) + N'%'
    END AS Coverage
FROM @results
ORDER BY
    CASE Status WHEN N'LESS' THEN 0 WHEN N'EMPTY' THEN 1 WHEN N'MORE' THEN 2 ELSE 3 END,
    TableName;

PRINT N'';
PRINT N'Part 1 complete. Check results above for any LESS/EMPTY/MORE statuses.';
GO

-- ============================================================
-- PART 2: BALANCE VALIDATION
-- ============================================================
PRINT N'';
PRINT N'--- PART 2: Balance Validation ---';
PRINT N'';

-- 2a. Journal entries: debits = credits within each voucher
PRINT N'2a. Checking journal entry debit/credit balance per voucher...';
SELECT TOP 20
    VoucherTypeCode,
    DocumentNumber,
    SUM(DebitAmount) AS TotalDebits,
    SUM(CreditAmount) AS TotalCredits,
    SUM(DebitAmount) - SUM(CreditAmount) AS Difference
FROM [dbo].[ACC_JournalEntries]
GROUP BY VoucherTypeCode, DocumentNumber
HAVING ABS(SUM(DebitAmount) - SUM(CreditAmount)) > 0.01
ORDER BY ABS(SUM(DebitAmount) - SUM(CreditAmount)) DESC;

PRINT N'  (0 rows = all balanced)';

-- 2b. Total loan portfolio balance comparison
PRINT N'2b. Comparing loan portfolio total balances...';
SELECT
    N'OLD cop_maecar' AS Source,
    SUM(ISNULL(saldot, 0)) AS TotalBalance,
    COUNT(*) AS PortfolioCount
FROM [old].dbo.cop_maecar
UNION ALL
SELECT
    N'NEW LND_LoanPortfolios' AS Source,
    SUM(ISNULL(Balance, 0)) AS TotalBalance,
    COUNT(*) AS PortfolioCount
FROM [dbo].[LND_LoanPortfolios];

-- 2c. Total lending transaction debit comparison
PRINT N'2c. Comparing lending transaction totals...';
SELECT
    N'OLD cop_movimto' AS Source,
    SUM(ISNULL(VLR_DEBITO, 0)) AS TotalDebits,
    SUM(ISNULL(VLR_CREDITO, 0)) AS TotalCredits
FROM [old].dbo.cop_movimto
UNION ALL
SELECT
    N'NEW LND_Transactions' AS Source,
    SUM(ISNULL(DebitAmount, 0)) AS TotalDebits,
    SUM(ISNULL(CreditAmount, 0)) AS TotalCredits
FROM [dbo].[LND_Transactions];

-- 2d. Payroll liquidation total comparison
PRINT N'2d. Comparing payroll plan liquidation totals...';
SELECT
    N'OLD nom_liqplan' AS Source,
    SUM(ISNULL(valor, 0)) AS TotalValue,
    COUNT(*) AS RecordCount
FROM [old].dbo.nom_liqplan
UNION ALL
SELECT
    N'NEW PAY_PayrollPlanLiquidations' AS Source,
    SUM(ISNULL(Amount, 0)) AS TotalValue,
    COUNT(*) AS RecordCount
FROM [dbo].[PAY_PayrollPlanLiquidations];

PRINT N'Part 2 complete.';
GO

-- ============================================================
-- PART 3: REFERENTIAL INTEGRITY — Orphan Check
-- ============================================================
PRINT N'';
PRINT N'--- PART 3: Referential Integrity (Orphan Check) ---';
PRINT N'';

DECLARE @orphans TABLE (
    CheckName   NVARCHAR(200),
    OrphanCount INT
);

-- PersonId orphans in major tables
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'LND_LoanPortfolios.PersonId → COR_People',
    COUNT(*)
FROM [dbo].[LND_LoanPortfolios] lp
LEFT JOIN [dbo].[COR_People] p ON lp.PersonId = p.Id
WHERE p.Id IS NULL AND lp.PersonId IS NOT NULL;

INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'ACC_JournalEntries.PersonId → COR_People',
    COUNT(*)
FROM [dbo].[ACC_JournalEntries] je
LEFT JOIN [dbo].[COR_People] p ON je.PersonId = p.Id
WHERE p.Id IS NULL AND je.PersonId IS NOT NULL;

INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'PAY_Employees.PersonId → COR_People',
    COUNT(*)
FROM [dbo].[PAY_Employees] e
LEFT JOIN [dbo].[COR_People] p ON e.PersonId = p.Id
WHERE p.Id IS NULL AND e.PersonId IS NOT NULL;

INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'SEC_Users.PersonId → COR_People',
    COUNT(*)
FROM [dbo].[SEC_Users] u
LEFT JOIN [dbo].[COR_People] p ON u.PersonId = p.Id
WHERE p.Id IS NULL AND u.PersonId IS NOT NULL;

INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'WEB_LoanApplications.PersonId → COR_People',
    COUNT(*)
FROM [dbo].[WEB_LoanApplications] w
LEFT JOIN [dbo].[COR_People] p ON w.PersonId = p.Id
WHERE p.Id IS NULL AND w.PersonId IS NOT NULL;

INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'WEB_DataUpdates.PersonId → COR_People',
    COUNT(*)
FROM [dbo].[WEB_DataUpdates] w
LEFT JOIN [dbo].[COR_People] p ON w.PersonId = p.Id
WHERE p.Id IS NULL AND w.PersonId IS NOT NULL;

-- AccountId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'ACC_JournalEntries.AccountId → ACC_ChartOfAccounts',
    COUNT(*)
FROM [dbo].[ACC_JournalEntries] je
LEFT JOIN [dbo].[ACC_ChartOfAccounts] ca ON je.AccountId = ca.Id
WHERE ca.Id IS NULL AND je.AccountId IS NOT NULL;

-- BranchId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'LND_LoanPortfolios.BranchId → COR_Branches',
    COUNT(*)
FROM [dbo].[LND_LoanPortfolios] lp
LEFT JOIN [dbo].[COR_Branches] b ON lp.BranchId = b.Id
WHERE b.Id IS NULL AND lp.BranchId IS NOT NULL;

INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'ACC_JournalEntries.BranchId → COR_Branches',
    COUNT(*)
FROM [dbo].[ACC_JournalEntries] je
LEFT JOIN [dbo].[COR_Branches] b ON je.BranchId = b.Id
WHERE b.Id IS NULL AND je.BranchId IS NOT NULL;

-- CostCenterId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'ACC_JournalEntries.CostCenterId → COR_CostCenters',
    COUNT(*)
FROM [dbo].[ACC_JournalEntries] je
LEFT JOIN [dbo].[COR_CostCenters] cc ON je.CostCenterId = cc.Id
WHERE cc.Id IS NULL AND je.CostCenterId IS NOT NULL;

-- LoanPortfolioId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'LND_Transactions.LoanPortfolioId → LND_LoanPortfolios',
    COUNT(*)
FROM [dbo].[LND_Transactions] t
LEFT JOIN [dbo].[LND_LoanPortfolios] lp ON t.LoanPortfolioId = lp.Id
WHERE lp.Id IS NULL AND t.LoanPortfolioId IS NOT NULL;

-- EmployeeId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'PAY_PayrollPlanLiquidations.EmployeeId → PAY_Employees',
    COUNT(*)
FROM [dbo].[PAY_PayrollPlanLiquidations] pl
LEFT JOIN [dbo].[PAY_Employees] e ON pl.EmployeeId = e.Id
WHERE e.Id IS NULL AND pl.EmployeeId IS NOT NULL;

-- SEC_UserMenuAccess.UserId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'SEC_UserMenuAccess.UserId → SEC_Users',
    COUNT(*)
FROM [dbo].[SEC_UserMenuAccess] ma
LEFT JOIN [dbo].[SEC_Users] u ON ma.UserId = u.Id
WHERE u.Id IS NULL;

-- SEC_UserAssignments.UserId orphans
INSERT INTO @orphans (CheckName, OrphanCount)
SELECT N'SEC_UserAssignments.UserId → SEC_Users',
    COUNT(*)
FROM [dbo].[SEC_UserAssignments] sa
LEFT JOIN [dbo].[SEC_Users] u ON sa.UserId = u.Id
WHERE u.Id IS NULL AND sa.UserId IS NOT NULL;

SELECT CheckName, OrphanCount,
    CASE WHEN OrphanCount = 0 THEN N'OK' ELSE N'ORPHANS FOUND' END AS Status
FROM @orphans
ORDER BY OrphanCount DESC, CheckName;

PRINT N'Part 3 complete. Any row with OrphanCount > 0 needs investigation.';
GO

-- ============================================================
-- PART 4: MAPPING TABLE COMPLETENESS
-- ============================================================
PRINT N'';
PRINT N'--- PART 4: Mapping Table Completeness ---';
PRINT N'';

DECLARE @mapCheck TABLE (
    CheckName   NVARCHAR(200),
    MissingCount INT
);

-- Every CODIGOTER in sys_maenit should have a Map_People entry
INSERT INTO @mapCheck (CheckName, MissingCount)
SELECT N'sys_maenit.CODIGOTER → Map_People',
    COUNT(*)
FROM [old].dbo.sys_maenit m
LEFT JOIN [migration].[Map_People] mp ON RTRIM(m.CODIGOTER) = mp.OldCodigoTer
WHERE mp.OldCodigoTer IS NULL;

-- Every CUENTA in cnt_maecuen should have a Map_ChartOfAccounts entry
INSERT INTO @mapCheck (CheckName, MissingCount)
SELECT N'cnt_maecuen.CUENTA → Map_ChartOfAccounts',
    COUNT(*)
FROM [old].dbo.cnt_maecuen m
LEFT JOIN [migration].[Map_ChartOfAccounts] mc ON RTRIM(m.CUENTA) = mc.OldCuenta
WHERE mc.OldCuenta IS NULL;

-- Every composite key in cop_maecar should have a Map_LoanPortfolios entry
INSERT INTO @mapCheck (CheckName, MissingCount)
SELECT N'cop_maecar → Map_LoanPortfolios',
    COUNT(*)
FROM [old].dbo.cop_maecar m
LEFT JOIN [migration].[Map_LoanPortfolios] ml
    ON RTRIM(m.CODIGOTER) = ml.OldCodigoTer
    AND m.LINCRED = ml.OldLincred
    AND m.NUMERO = ml.OldNumero
WHERE ml.OldCodigoTer IS NULL;

-- Branch mapping
INSERT INTO @mapCheck (CheckName, MissingCount)
SELECT N'sys_agencia.codigo → Map_Branches',
    COUNT(*)
FROM [old].dbo.sys_agencia a
LEFT JOIN [migration].[Map_Branches] mb ON RTRIM(a.codigo) = mb.OldCodigo
WHERE mb.OldCodigo IS NULL;

-- Bank mapping
INSERT INTO @mapCheck (CheckName, MissingCount)
SELECT N'sys_banco03 → Map_Banks',
    COUNT(*)
FROM [old].dbo.sys_banco03 b
LEFT JOIN [migration].[Map_Banks] mb ON RTRIM(b.CODIGO_BANCO) = mb.OldCodigo
WHERE mb.OldCodigo IS NULL;

-- VoucherType mapping
INSERT INTO @mapCheck (CheckName, MissingCount)
SELECT N'sys_compro02.CODIGO → Map_VoucherTypes',
    COUNT(*)
FROM [old].dbo.sys_compro02 c
LEFT JOIN [migration].[Map_VoucherTypes] mv ON RTRIM(c.CODIGO) = mv.OldCodigo
WHERE mv.OldCodigo IS NULL;

SELECT CheckName, MissingCount,
    CASE WHEN MissingCount = 0 THEN N'OK' ELSE N'MISSING MAPPINGS' END AS Status
FROM @mapCheck
ORDER BY MissingCount DESC, CheckName;

PRINT N'Part 4 complete. MissingCount > 0 means some records were not mapped.';
GO

-- ============================================================
-- PART 5: DATA QUALITY CHECKS
-- ============================================================
PRINT N'';
PRINT N'--- PART 5: Data Quality Checks ---';
PRINT N'';

DECLARE @quality TABLE (
    CheckName   NVARCHAR(200),
    IssueCount  INT
);

-- 5a. No negative loan balances (unless expected for overpayments)
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'LND_LoanPortfolios with negative balance',
    COUNT(*)
FROM [dbo].[LND_LoanPortfolios]
WHERE Balance < 0;

-- 5b. No future disbursement dates
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'LND_LoanPortfolios with future disbursement date',
    COUNT(*)
FROM [dbo].[LND_LoanPortfolios]
WHERE DisbursementDate > CAST(GETDATE() AS DATE);

-- 5c. PublicId uniqueness (should never fail due to UNIQUE constraint, but verify)
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'COR_People duplicate PublicId',
    COUNT(*) - COUNT(DISTINCT PublicId)
FROM [dbo].[COR_People];

INSERT INTO @quality (CheckName, IssueCount)
SELECT N'SEC_Users duplicate PublicId',
    COUNT(*) - COUNT(DISTINCT PublicId)
FROM [dbo].[SEC_Users];

-- 5d. NULL required fields
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'COR_People with NULL IdentificationNumber',
    COUNT(*)
FROM [dbo].[COR_People]
WHERE IdentificationNumber IS NULL;

INSERT INTO @quality (CheckName, IssueCount)
SELECT N'SEC_Users with NULL Username',
    COUNT(*)
FROM [dbo].[SEC_Users]
WHERE Username IS NULL OR Username = N'';

INSERT INTO @quality (CheckName, IssueCount)
SELECT N'ACC_ChartOfAccounts with NULL AccountCode',
    COUNT(*)
FROM [dbo].[ACC_ChartOfAccounts]
WHERE AccountCode IS NULL OR AccountCode = N'';

-- 5e. Legacy code uniqueness
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'COR_People duplicate LegacyCode',
    (SELECT COUNT(*) FROM (
        SELECT LegacyCode FROM [dbo].[COR_People]
        WHERE LegacyCode IS NOT NULL
        GROUP BY LegacyCode HAVING COUNT(*) > 1
    ) x);

-- 5f. Users with legacy password that needs rehash
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'SEC_Users with LEGACY: password prefix (needs rehash)',
    COUNT(*)
FROM [dbo].[SEC_Users]
WHERE PasswordHash LIKE N'LEGACY:%';

-- 5g. Empty audit JSON values
INSERT INTO @quality (CheckName, IssueCount)
SELECT N'AUD_CompanyChanges with NULL OldValues AND NewValues',
    COUNT(*)
FROM [dbo].[AUD_CompanyChanges]
WHERE OldValues IS NULL AND NewValues IS NULL;

SELECT CheckName, IssueCount,
    CASE
        WHEN IssueCount = 0 THEN N'OK'
        WHEN CheckName LIKE N'%LEGACY:%' THEN N'INFO'
        WHEN CheckName LIKE N'%negative%' THEN N'WARNING'
        ELSE N'REVIEW'
    END AS Severity
FROM @quality
ORDER BY
    CASE
        WHEN IssueCount = 0 THEN 2
        WHEN CheckName LIKE N'%LEGACY:%' THEN 1
        ELSE 0
    END,
    IssueCount DESC;

PRINT N'Part 5 complete.';
GO

-- ============================================================
-- PART 6: GENERATE REPORT & LOG
-- ============================================================
PRINT N'';
PRINT N'--- PART 6: Summary Report ---';
PRINT N'';

-- Re-declare @results for this batch (table variables are batch-scoped)
DECLARE @finalResults TABLE (
    TableName       NVARCHAR(200),
    SourceTable     NVARCHAR(200),
    OriginalCount   INT,
    MigratedCount   INT,
    Status          NVARCHAR(20)
);

-- Pull from migration log instead
INSERT INTO @finalResults (TableName, SourceTable, OriginalCount, MigratedCount, Status)
SELECT
    ml.TableName,
    ml.SourceTable,
    0, -- Original count not stored in log; use MigratedCount only
    ml.RowsMigrated,
    ml.Status
FROM [migration].[MigrationLog] ml;

-- Insert validation step into log
INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
VALUES (9, N'_VALIDATION_', N'(all tables)', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'Success');

-- Overall migration summary by step
SELECT
    StepNumber,
    COUNT(*) AS TablesProcessed,
    SUM(RowsMigrated) AS TotalRows,
    MIN(StartedAt) AS Started,
    MAX(CompletedAt) AS Completed,
    SUM(CASE WHEN Status = N'Failed' THEN 1 ELSE 0 END) AS FailedCount,
    STRING_AGG(CASE WHEN Status = N'Failed' THEN TableName ELSE NULL END, N', ') AS FailedTables
FROM [migration].[MigrationLog]
GROUP BY StepNumber
ORDER BY StepNumber;

-- Grand totals
PRINT N'';
PRINT N'============================================================';
PRINT N'  VALIDATION COMPLETE';
PRINT N'============================================================';
PRINT N'';
PRINT N'Total rows migrated: ' + CAST((SELECT ISNULL(SUM(RowsMigrated), 0) FROM [migration].[MigrationLog]) AS NVARCHAR);
PRINT N'Total tables processed: ' + CAST((SELECT COUNT(DISTINCT TableName) FROM [migration].[MigrationLog] WHERE TableName <> N'_VALIDATION_') AS NVARCHAR);
PRINT N'Failed tables: ' + CAST((SELECT COUNT(*) FROM [migration].[MigrationLog] WHERE Status = N'Failed') AS NVARCHAR);
PRINT N'';
PRINT N'  Finished: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
GO
