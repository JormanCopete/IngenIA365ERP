-- ============================================================
-- IngenIA365ERP — Migration Step 01: Create Mapping Tables
-- Source: SOLIDO ERP (DBDefinicion.sql)
-- Target: IngenIA365ERP Schema v1.0
-- ============================================================
SET NOCOUNT ON;
PRINT N'============================================================';
PRINT N'  Step 01: Creating Mapping Tables';
PRINT N'  Started: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
GO

-- ============================================================
-- Create migration schema
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'migration')
    EXEC('CREATE SCHEMA [migration]');
GO

PRINT N'Schema [migration] ready.';
GO

-- ============================================================
-- Drop existing mapping tables if re-running
-- ============================================================
IF OBJECT_ID('migration.MigrationLog', 'U') IS NOT NULL DROP TABLE [migration].[MigrationLog];
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
GO

PRINT N'Dropped existing mapping tables (if any).';
GO

-- ============================================================
-- 1. Map_People
-- Maps: sys_maenit.CODIGOTER / cnt_nit.NIT -> COR_People.Id
-- ============================================================
CREATE TABLE [migration].[Map_People] (
    OldCodigoTer    VARCHAR(14)  NOT NULL PRIMARY KEY,
    NewPersonId     INT          NOT NULL,
    Source          NVARCHAR(20) NOT NULL DEFAULT 'sys_maenit' -- 'sys_maenit' or 'cnt_nit'
);
GO
PRINT N'Created [migration].[Map_People]';
GO

-- ============================================================
-- 2. Map_ChartOfAccounts
-- Maps: cnt_maecuen.CUENTA -> ACC_ChartOfAccounts.Id
-- ============================================================
CREATE TABLE [migration].[Map_ChartOfAccounts] (
    OldCuenta       VARCHAR(12)  NOT NULL PRIMARY KEY,
    NewAccountId    INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_ChartOfAccounts]';
GO

-- ============================================================
-- 3. Map_LoanPortfolios
-- Maps: cop_maecar (CODIGOTER, LINCRED, NUMERO) -> LND_LoanPortfolios.Id
-- ============================================================
CREATE TABLE [migration].[Map_LoanPortfolios] (
    OldCodigoTer    VARCHAR(14)  NOT NULL,
    OldLincred      INT          NOT NULL,
    OldNumero       BIGINT       NOT NULL,
    NewLoanPortfolioId INT       NOT NULL,
    PRIMARY KEY (OldCodigoTer, OldLincred, OldNumero)
);
GO
PRINT N'Created [migration].[Map_LoanPortfolios]';
GO

-- ============================================================
-- 4. Map_Branches
-- Maps: sys_agencia.codigo -> COR_Branches.Id
-- ============================================================
CREATE TABLE [migration].[Map_Branches] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewBranchId     INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Branches]';
GO

-- ============================================================
-- 5. Map_CostCenters
-- Maps: sys_cencos.CCOSTO -> COR_CostCenters.Id
-- ============================================================
CREATE TABLE [migration].[Map_CostCenters] (
    OldCCosto       VARCHAR(8)   NOT NULL PRIMARY KEY,
    NewCostCenterId INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_CostCenters]';
GO

-- ============================================================
-- 6. Map_Banks
-- Maps: sys_banco03.CODIGO_BANCO -> COR_Banks.Id
-- ============================================================
CREATE TABLE [migration].[Map_Banks] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewBankId       INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Banks]';
GO

-- ============================================================
-- 7. Map_VoucherTypes
-- Maps: sys_compro02.CODIGO -> ACC_VoucherTypes.Id
-- ============================================================
CREATE TABLE [migration].[Map_VoucherTypes] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewVoucherTypeId INT         NOT NULL
);
GO
PRINT N'Created [migration].[Map_VoucherTypes]';
GO

-- ============================================================
-- 8. Map_CreditLines
-- Maps: cop_concar12.LINCRED -> LND_CreditLineParameters.Id
-- ============================================================
CREATE TABLE [migration].[Map_CreditLines] (
    OldLincred      INT          NOT NULL PRIMARY KEY,
    NewCreditLineId INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_CreditLines]';
GO

-- ============================================================
-- 9. Map_SavingsAccounts
-- Maps: cop_maeahor.NUM_CUENTA -> LND_SavingsAccounts.Id
-- ============================================================
CREATE TABLE [migration].[Map_SavingsAccounts] (
    OldNumCuenta    VARCHAR(20)  NOT NULL PRIMARY KEY,
    NewSavingsAccountId INT      NOT NULL
);
GO
PRINT N'Created [migration].[Map_SavingsAccounts]';
GO

-- ============================================================
-- 10. Map_DepositAccounts
-- Maps: dep_maeahor.num_cuenta -> LND_DepositAccounts.Id
-- ============================================================
CREATE TABLE [migration].[Map_DepositAccounts] (
    OldNumCuenta    VARCHAR(20)  NOT NULL PRIMARY KEY,
    NewDepositAccountId INT      NOT NULL
);
GO
PRINT N'Created [migration].[Map_DepositAccounts]';
GO

-- ============================================================
-- 11. Map_Employees
-- Maps: nom_empleados (idnomina, idempleado) -> PAY_Employees.Id
-- ============================================================
CREATE TABLE [migration].[Map_Employees] (
    OldIdNomina     INT          NOT NULL,
    OldIdEmpleado   BIGINT       NOT NULL,
    NewEmployeeId   INT          NOT NULL,
    PRIMARY KEY (OldIdNomina, OldIdEmpleado)
);
GO
PRINT N'Created [migration].[Map_Employees]';
GO

-- ============================================================
-- 12. Map_Products
-- Maps: inv_productos.IdProducto -> INV_Products.Id
-- ============================================================
CREATE TABLE [migration].[Map_Products] (
    OldIdProducto   INT          NOT NULL PRIMARY KEY,
    NewProductId    INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Products]';
GO

-- ============================================================
-- 13. Map_CDTCertificates
-- Maps: cdt_maecdats.NUMERO_CDT -> CDT_Certificates.Id
-- ============================================================
CREATE TABLE [migration].[Map_CDTCertificates] (
    OldNumCdat      INT          NOT NULL PRIMARY KEY,
    NewCertificateId INT         NOT NULL
);
GO
PRINT N'Created [migration].[Map_CDTCertificates]';
GO

-- ============================================================
-- 14. Map_EmployerCompanies
-- Maps: cop_empresa13.codigo_empresa / nom_empresas.IdEmpresa -> COR_EmployerCompanies.Id
-- ============================================================
CREATE TABLE [migration].[Map_EmployerCompanies] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewCompanyId    INT          NOT NULL,
    Source          NVARCHAR(20) NOT NULL DEFAULT 'cop_empresa13'
);
GO
PRINT N'Created [migration].[Map_EmployerCompanies]';
GO

-- ============================================================
-- 15. Map_Committees
-- Maps: cop_comite.codigo -> COR_Committees.Id
-- ============================================================
CREATE TABLE [migration].[Map_Committees] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewCommitteeId  INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Committees]';
GO

-- ============================================================
-- 16. Map_Cities
-- Maps: sys_ciudad57.CIUDAD -> COR_Cities.Id
-- ============================================================
CREATE TABLE [migration].[Map_Cities] (
    OldCiudad       INT          NOT NULL PRIMARY KEY,
    NewCityId       INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Cities]';
GO

-- ============================================================
-- 17. Map_Relationships
-- Maps: sys_parent51.codigo -> COR_Relationships.Id
-- ============================================================
CREATE TABLE [migration].[Map_Relationships] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewRelationshipId INT        NOT NULL
);
GO
PRINT N'Created [migration].[Map_Relationships]';
GO

-- ============================================================
-- 18. Map_Sections
-- Maps: sys_seccion56.codigo -> COR_Sections.Id
-- ============================================================
CREATE TABLE [migration].[Map_Sections] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewSectionId    INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Sections]';
GO

-- ============================================================
-- 19. Map_Professions
-- Maps: sys_profe52.CODIGO_PROFESION -> COR_Professions.Id
-- ============================================================
CREATE TABLE [migration].[Map_Professions] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewProfessionId INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Professions]';
GO

-- ============================================================
-- 20. Map_Positions
-- Maps: sys_cargo55.CODIGO_CARGO -> COR_Positions.Id
-- ============================================================
CREATE TABLE [migration].[Map_Positions] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewPositionId   INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Positions]';
GO

-- ============================================================
-- 21. Map_WithdrawalReasons
-- Maps: sys_motret.codigo -> COR_WithdrawalReasons.Id
-- ============================================================
CREATE TABLE [migration].[Map_WithdrawalReasons] (
    OldCodigo       VARCHAR(4)   NOT NULL PRIMARY KEY,
    NewReasonId     INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_WithdrawalReasons]';
GO

-- ============================================================
-- 22. Map_Advisors
-- Maps: cop_asesores.Idcedula -> COR_Advisors.Id
-- ============================================================
CREATE TABLE [migration].[Map_Advisors] (
    OldIdCedula     VARCHAR(14)  NOT NULL PRIMARY KEY,
    NewAdvisorId    INT          NOT NULL
);
GO
PRINT N'Created [migration].[Map_Advisors]';
GO

-- ============================================================
-- Migration Log Table
-- ============================================================
CREATE TABLE [migration].[MigrationLog] (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    StepNumber      INT          NOT NULL,
    TableName       NVARCHAR(200) NOT NULL,
    SourceTable     NVARCHAR(200) NOT NULL,
    RowsMigrated    INT          NOT NULL DEFAULT 0,
    StartedAt       DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAt     DATETIME2    NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Running', -- Running, Success, Failed
    ErrorMessage    NVARCHAR(MAX) NULL
);
GO
PRINT N'Created [migration].[MigrationLog]';
GO

-- ============================================================
-- Create indexes for performance during migration lookups
-- ============================================================
CREATE NONCLUSTERED INDEX IX_Map_People_NewPersonId ON [migration].[Map_People] (NewPersonId);
CREATE NONCLUSTERED INDEX IX_Map_ChartOfAccounts_NewAccountId ON [migration].[Map_ChartOfAccounts] (NewAccountId);
CREATE NONCLUSTERED INDEX IX_Map_Branches_NewBranchId ON [migration].[Map_Branches] (NewBranchId);
CREATE NONCLUSTERED INDEX IX_Map_CostCenters_NewCostCenterId ON [migration].[Map_CostCenters] (NewCostCenterId);
CREATE NONCLUSTERED INDEX IX_Map_Banks_NewBankId ON [migration].[Map_Banks] (NewBankId);
CREATE NONCLUSTERED INDEX IX_Map_VoucherTypes_NewVoucherTypeId ON [migration].[Map_VoucherTypes] (NewVoucherTypeId);
CREATE NONCLUSTERED INDEX IX_Map_EmployerCompanies_NewCompanyId ON [migration].[Map_EmployerCompanies] (NewCompanyId);
CREATE NONCLUSTERED INDEX IX_Map_Committees_NewCommitteeId ON [migration].[Map_Committees] (NewCommitteeId);
CREATE NONCLUSTERED INDEX IX_Map_Cities_NewCityId ON [migration].[Map_Cities] (NewCityId);
GO

PRINT N'============================================================';
PRINT N'  Step 01: COMPLETE - 22 mapping tables + 1 log table created';
PRINT N'  Finished: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
GO
