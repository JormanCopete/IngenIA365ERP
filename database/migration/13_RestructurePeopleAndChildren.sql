-- ============================================================================
-- IngenIA365ERP — Migracion 13: Reestructuracion COR_People y tablas hijas
-- ============================================================================
--
-- Contexto:
--   El modelo original (sys_maenit, ~130 cols monolitica) se descompuso a
--   COR_People + COR_Associates + COR_Spouses + PAY_Employees + INV_Salespeople,
--   pero quedaron varios problemas:
--
--   1. COR_People sigue teniendo datos que pertenecen al ROL asociado (empleador
--      externo, salario, pos card, scoring, etc.) - no a la persona base.
--   2. PAY_Employees DUPLICA columnas de Person (LastName, FirstName, Email, etc.)
--      en vez de leerlas via FK.
--   3. INV_Salespeople vive aislada, sin FK a Person, con sus propios
--      Name/Address/Phone duplicados.
--   4. COR_Spouses tiene info laboral del conyuge que en realidad es del rol
--      asociado.
--
-- Este script ejecuta la reorganizacion en SQL Server con ALTER TABLE quirurgico:
--   - Conserva FKs entrantes desde COR_Beneficiaries, COR_References,
--     LND_LoanPortfolios, etc.
--   - Mueve columnas eliminandolas de un lado y creandolas en otro.
--   - NO migra los datos existentes en esas columnas (entorno dev).
--
-- Es idempotente: usa IF EXISTS en cada DROP y NOT EXISTS antes de cada ADD.
--
-- Como ejecutar:
--   sqlcmd -S <server> -d <database> -i 13_RestructurePeopleAndChildren.sql
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'================================================================';
PRINT N'Migracion 13: Reestructuracion COR_People y tablas hijas';
PRINT N'================================================================';
GO

-- ---------------------------------------------------------------------------
-- HELPER: drop default constraint asociado a una columna
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp__DropDefaultIfExists
    @Schema SYSNAME,
    @Table SYSNAME,
    @Column SYSNAME
AS
BEGIN
    DECLARE @ConstraintName SYSNAME;
    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = @Schema AND t.name = @Table AND c.name = @Column;

    IF @ConstraintName IS NOT NULL
    BEGIN
        DECLARE @Sql NVARCHAR(MAX) = N'ALTER TABLE [' + @Schema + N'].[' + @Table
            + N'] DROP CONSTRAINT [' + @ConstraintName + N']';
        EXEC sp_executesql @Sql;
    END;
END;
GO

-- ---------------------------------------------------------------------------
-- HELPER: drop column safe (drops default + indexes + unique constraints)
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp__DropColumnIfExists
    @Schema SYSNAME,
    @Table SYSNAME,
    @Column SYSNAME
AS
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.tables t ON t.object_id = c.object_id
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = @Schema AND t.name = @Table AND c.name = @Column)
    BEGIN
        EXEC dbo.sp__DropDefaultIfExists @Schema, @Table, @Column;

        -- Drop UNIQUE constraints that reference this column
        DECLARE @UqName SYSNAME;
        DECLARE uq_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT DISTINCT kc.name
            FROM sys.key_constraints kc
            INNER JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            INNER JOIN sys.tables t ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @Schema AND t.name = @Table AND c.name = @Column
              AND kc.type = 'UQ';

        OPEN uq_cur;
        FETCH NEXT FROM uq_cur INTO @UqName;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE @DropUq NVARCHAR(MAX) = N'ALTER TABLE [' + @Schema + N'].[' + @Table
                + N'] DROP CONSTRAINT [' + @UqName + N']';
            BEGIN TRY EXEC sp_executesql @DropUq; END TRY
            BEGIN CATCH /* skip */ END CATCH;
            FETCH NEXT FROM uq_cur INTO @UqName;
        END;
        CLOSE uq_cur;
        DEALLOCATE uq_cur;

        -- Drop non-PK / non-unique-constraint indexes that include this column
        DECLARE @IdxName SYSNAME;
        DECLARE idx_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT DISTINCT i.name
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            INNER JOIN sys.tables t ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @Schema AND t.name = @Table AND c.name = @Column
              AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
              AND i.name IS NOT NULL;

        OPEN idx_cur;
        FETCH NEXT FROM idx_cur INTO @IdxName;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE @DropIdx NVARCHAR(MAX) = N'DROP INDEX [' + @IdxName
                + N'] ON [' + @Schema + N'].[' + @Table + N']';
            BEGIN TRY EXEC sp_executesql @DropIdx; END TRY
            BEGIN CATCH /* index might be referenced; skip */ END CATCH;
            FETCH NEXT FROM idx_cur INTO @IdxName;
        END;
        CLOSE idx_cur;
        DEALLOCATE idx_cur;

        DECLARE @Sql NVARCHAR(MAX) = N'ALTER TABLE [' + @Schema + N'].[' + @Table
            + N'] DROP COLUMN [' + @Column + N']';
        EXEC sp_executesql @Sql;
        PRINT N'    drop COL [' + @Schema + N'].[' + @Table + N'].[' + @Column + N']';
    END;
END;
GO

-- ===========================================================================
-- 1. COR_People: limpiar columnas que se mueven o eliminan
-- ===========================================================================
PRINT N'';
PRINT N'1. Limpiando COR_People...';

-- Empleo del asociado en empresa externa (se mueve a COR_Associates)
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'Employer';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'EmployerStartDate';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'SalaryType';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'Salary';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'Severance';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ProfessionId';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'PositionId';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'SeveranceFund';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'OtherIncomeDescription';

-- Datos bancarios del asociado (depositos / excedentes) → COR_Associates
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'BankId';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'BankAccountNumber';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'BankAccountType';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'BankAccountCityId';

-- Datos de scoring/riesgo del rol asociado → COR_Associates
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'AuthCentralRisk';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'PosCardClass';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'PosCardLimit';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'InsuranceRiskRate';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ZoneTypeId';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ZoneId';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsSiplaExempt';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'SiplaExemptDate';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'SiplaUser';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'SinglePromissoryNote';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'PledgesContributions';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'InManagement';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'CreditLimit';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'CpAdmin';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'CpContributions';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'CpLocal';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'CpCommission';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ProfitCenter';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'CapacityPayPct';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsFromGovernment';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsPublicResourceAdmin';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsPensioner';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsInsubordinate';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsOnVacation';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'IsOnUnpaidLeave';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'PensionType';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'SeveranceType';

-- Acceso online (se elimina por completo - decision P8)
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'InternetPassword';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'OnlineConsultation';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ConsultationStatus';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'AffiliationCode';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ConsultationChargeType';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'ConsultationCreditLine';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_People', 'UserPassword';

-- Renombrar Nit* → Supplier* (banking de proveedores/recaudo de clientes)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'NitBankCode')
    EXEC sp_rename 'dbo.COR_People.NitBankCode', 'SupplierBankCode', 'COLUMN';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'NitBankAccountType')
    EXEC sp_rename 'dbo.COR_People.NitBankAccountType', 'SupplierBankAccountType', 'COLUMN';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'NitBankAccountNumber')
    EXEC sp_rename 'dbo.COR_People.NitBankAccountNumber', 'SupplierBankAccountNumber', 'COLUMN';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'NitAdvisorId')
    EXEC sp_rename 'dbo.COR_People.NitAdvisorId', 'SupplierAdvisorId', 'COLUMN';

-- Agregar nuevos flags de rol
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'IsCustomer')
    ALTER TABLE [dbo].[COR_People] ADD [IsCustomer] BIT NOT NULL CONSTRAINT [DF_COR_People_IsCustomer] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'IsSupplier')
    ALTER TABLE [dbo].[COR_People] ADD [IsSupplier] BIT NOT NULL CONSTRAINT [DF_COR_People_IsSupplier] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'IsSalesperson')
    ALTER TABLE [dbo].[COR_People] ADD [IsSalesperson] BIT NOT NULL CONSTRAINT [DF_COR_People_IsSalesperson] DEFAULT 0;

PRINT N'  COR_People listo';
GO

-- ===========================================================================
-- 2. COR_Associates: agregar columnas que vienen de Person + Spouse
-- ===========================================================================
PRINT N'';
PRINT N'2. Ampliando COR_Associates...';

-- 2.a Empleo del asociado en empresa externa (vienen de Person)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalEmployerName')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalEmployerName] NVARCHAR(80) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalEmploymentStartDate')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalEmploymentStartDate] DATE NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalSalary')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalSalary] DECIMAL(17,2) NOT NULL CONSTRAINT [DF_COR_Associates_ExternalSalary] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalSalaryType')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalSalaryType] NVARCHAR(2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalSeverance')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalSeverance] DECIMAL(17,2) NOT NULL CONSTRAINT [DF_COR_Associates_ExternalSeverance] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalSeveranceFund')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalSeveranceFund] NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalProfessionId')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalProfessionId] INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalPositionId')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalPositionId] INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ExternalOtherIncomeDescription')
    ALTER TABLE [dbo].[COR_Associates] ADD [ExternalOtherIncomeDescription] NVARCHAR(120) NULL;

-- 2.b Banca para depositos del asociado (excedentes/aportes)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'DepositBankId')
    ALTER TABLE [dbo].[COR_Associates] ADD [DepositBankId] INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'DepositBankAccountNumber')
    ALTER TABLE [dbo].[COR_Associates] ADD [DepositBankAccountNumber] NVARCHAR(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'DepositBankAccountType')
    ALTER TABLE [dbo].[COR_Associates] ADD [DepositBankAccountType] NVARCHAR(2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'DepositBankAccountCityId')
    ALTER TABLE [dbo].[COR_Associates] ADD [DepositBankAccountCityId] INT NULL;

-- 2.c Scoring/Riesgo (vienen de Person)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'AuthCentralRisk')
    ALTER TABLE [dbo].[COR_Associates] ADD [AuthCentralRisk] BIT NOT NULL CONSTRAINT [DF_COR_Associates_AuthCentralRisk] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'PosCardClass')
    ALTER TABLE [dbo].[COR_Associates] ADD [PosCardClass] NVARCHAR(2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'PosCardLimit')
    ALTER TABLE [dbo].[COR_Associates] ADD [PosCardLimit] DECIMAL(17,2) NOT NULL CONSTRAINT [DF_COR_Associates_PosCardLimit] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'InsuranceRiskRate')
    ALTER TABLE [dbo].[COR_Associates] ADD [InsuranceRiskRate] DECIMAL(9,3) NOT NULL CONSTRAINT [DF_COR_Associates_InsuranceRiskRate] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'AssociateZoneTypeId')
    ALTER TABLE [dbo].[COR_Associates] ADD [AssociateZoneTypeId] INT NOT NULL CONSTRAINT [DF_COR_Associates_AssociateZoneTypeId] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'AssociateZoneId')
    ALTER TABLE [dbo].[COR_Associates] ADD [AssociateZoneId] INT NOT NULL CONSTRAINT [DF_COR_Associates_AssociateZoneId] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsSiplaExempt')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsSiplaExempt] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsSiplaExempt] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SiplaExemptDate')
    ALTER TABLE [dbo].[COR_Associates] ADD [SiplaExemptDate] DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SiplaUser')
    ALTER TABLE [dbo].[COR_Associates] ADD [SiplaUser] NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SinglePromissoryNote')
    ALTER TABLE [dbo].[COR_Associates] ADD [SinglePromissoryNote] BIT NOT NULL CONSTRAINT [DF_COR_Associates_SinglePromissoryNote] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'PledgesContributions')
    ALTER TABLE [dbo].[COR_Associates] ADD [PledgesContributions] BIT NOT NULL CONSTRAINT [DF_COR_Associates_PledgesContributions] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'InManagement')
    ALTER TABLE [dbo].[COR_Associates] ADD [InManagement] BIT NOT NULL CONSTRAINT [DF_COR_Associates_InManagement] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'CreditLimit')
    ALTER TABLE [dbo].[COR_Associates] ADD [CreditLimit] DECIMAL(17,2) NOT NULL CONSTRAINT [DF_COR_Associates_CreditLimit] DEFAULT 0;

-- 2.d Centros contables del rol asociado
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'CpAdmin')
    ALTER TABLE [dbo].[COR_Associates] ADD [CpAdmin] BIT NOT NULL CONSTRAINT [DF_COR_Associates_CpAdmin] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'CpContributions')
    ALTER TABLE [dbo].[COR_Associates] ADD [CpContributions] BIT NOT NULL CONSTRAINT [DF_COR_Associates_CpContributions] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'CpLocal')
    ALTER TABLE [dbo].[COR_Associates] ADD [CpLocal] BIT NOT NULL CONSTRAINT [DF_COR_Associates_CpLocal] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'CpCommission')
    ALTER TABLE [dbo].[COR_Associates] ADD [CpCommission] BIT NOT NULL CONSTRAINT [DF_COR_Associates_CpCommission] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'ProfitCenter')
    ALTER TABLE [dbo].[COR_Associates] ADD [ProfitCenter] NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'CapacityPayPct')
    ALTER TABLE [dbo].[COR_Associates] ADD [CapacityPayPct] BIT NOT NULL CONSTRAINT [DF_COR_Associates_CapacityPayPct] DEFAULT 0;

-- 2.e Estados/banderas del rol
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsFromGovernment')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsFromGovernment] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsFromGovernment] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsPublicResourceAdmin')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsPublicResourceAdmin] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsPublicResourceAdmin] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsPensioner')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsPensioner] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsPensioner] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsInsubordinate')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsInsubordinate] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsInsubordinate] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsOnVacation')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsOnVacation] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsOnVacation] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'IsOnUnpaidLeave')
    ALTER TABLE [dbo].[COR_Associates] ADD [IsOnUnpaidLeave] BIT NOT NULL CONSTRAINT [DF_COR_Associates_IsOnUnpaidLeave] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'AssociatePensionType')
    ALTER TABLE [dbo].[COR_Associates] ADD [AssociatePensionType] NVARCHAR(10) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'AssociateSeveranceType')
    ALTER TABLE [dbo].[COR_Associates] ADD [AssociateSeveranceType] NVARCHAR(10) NULL;

-- 2.f Datos del empleo del CONYUGE (vienen de COR_Spouses, decision P10)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseEmployer')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseEmployer] NVARCHAR(80) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseEmployerAddress')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseEmployerAddress] NVARCHAR(80) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseEmployerStart')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseEmployerStart] DATE NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseSalary')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseSalary] DECIMAL(17,2) NOT NULL CONSTRAINT [DF_COR_Associates_SpouseSalary] DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseSalaryType')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseSalaryType] NVARCHAR(2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpousePosition')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpousePosition] NVARCHAR(60) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseProfession')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseProfession] NVARCHAR(10) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseEducationLevel')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseEducationLevel] NVARCHAR(2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseSeverance')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseSeverance] DECIMAL(17,2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseOtherIncome')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseOtherIncome] DECIMAL(17,2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseOtherIncomeDesc')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseOtherIncomeDesc] NVARCHAR(120) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseCompanyCode')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseCompanyCode] NVARCHAR(10) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseBranchCode')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseBranchCode] NVARCHAR(10) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Associates') AND name = 'SpouseSectionCode')
    ALTER TABLE [dbo].[COR_Associates] ADD [SpouseSectionCode] NVARCHAR(10) NULL;

PRINT N'  COR_Associates ampliado';
GO

-- ===========================================================================
-- 3. COR_Spouses: limpiar columnas que se mueven a COR_Associates
-- ===========================================================================
PRINT N'';
PRINT N'3. Limpiando COR_Spouses (datos laborales -> Associates)...';

EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseEmployer';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseEmployerAddress';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseEmployerStart';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseSalary';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseSalaryType';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpousePosition';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseProfession';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseEducationLevel';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseSeverance';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseOtherIncome';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseOtherIncomeDesc';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseCompanyCode';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseBranchCode';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'COR_Spouses', 'SpouseSectionCode';

PRINT N'  COR_Spouses listo';
GO

-- ===========================================================================
-- 4. PAY_Employees: eliminar columnas duplicadas con Person + bagaje legacy
-- ===========================================================================
PRINT N'';
PRINT N'4. Limpiando PAY_Employees (duplicados con Person + bagaje legacy)...';

-- 4.a Datos personales que ahora se leen de COR_People via FK
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'IdentificationNumber';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'LastName';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'FirstName';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'IssuedAt';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'Address';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'CityId';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'Phone';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'Mobile';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'Email';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'Gender';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'BirthDate';

-- 4.b Bagaje legacy del SOLIDO que ya no se usa
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'MilitaryBooklet';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'MilitaryDistrict';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'DriverLicense';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'LicenseCategory';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'BloodType';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'RhFactor';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'PantsSize';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'ShirtSize';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'ShoeSize';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'HelmetSize';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'OverallSize';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'EmployeeClass';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'PAY_Employees', 'AcademicLevel';

-- 4.c Renombrar BankId/BankAccountNumber/AccountType -> Payroll*
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PAY_Employees') AND name = 'BankId')
    EXEC sp_rename 'dbo.PAY_Employees.BankId', 'PayrollBankId', 'COLUMN';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PAY_Employees') AND name = 'BankAccountNumber')
    EXEC sp_rename 'dbo.PAY_Employees.BankAccountNumber', 'PayrollBankAccountNumber', 'COLUMN';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PAY_Employees') AND name = 'AccountType')
    EXEC sp_rename 'dbo.PAY_Employees.AccountType', 'PayrollBankAccountType', 'COLUMN';

PRINT N'  PAY_Employees listo';
GO

-- ===========================================================================
-- 5. INV_Salespeople: convertir en hija de COR_People
-- ===========================================================================
PRINT N'';
PRINT N'5. Convirtiendo INV_Salespeople en hija de COR_People...';

-- 5.a Agregar PersonId (sera NOT NULL al final)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.INV_Salespeople') AND name = 'PersonId')
    ALTER TABLE [dbo].[INV_Salespeople] ADD [PersonId] INT NULL;

-- 5.b Eliminar columnas duplicadas con Person
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'IdNumber';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'Name';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'LastName';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'Address';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'Phone';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'Mobile';
EXEC dbo.sp__DropColumnIfExists 'dbo', 'INV_Salespeople', 'CityId';

-- 5.c Crear FK PersonId -> COR_People.Id (solo si no existe)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_INV_Salespeople_COR_People')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.INV_Salespeople') AND name = 'PersonId')
BEGIN
    ALTER TABLE [dbo].[INV_Salespeople]
    ADD CONSTRAINT [FK_INV_Salespeople_COR_People]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
END;

-- 5.d Indice sobre PersonId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INV_Salespeople_PersonId' AND object_id = OBJECT_ID('dbo.INV_Salespeople'))
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.INV_Salespeople') AND name = 'PersonId')
    CREATE INDEX [IX_INV_Salespeople_PersonId] ON [dbo].[INV_Salespeople]([PersonId]);

PRINT N'  INV_Salespeople listo';
GO

-- ===========================================================================
-- 6. PAY_Employees.PersonId: hacer NOT NULL (despues de migrar datos en runtime)
-- ===========================================================================
-- NOTA: Si hay registros viejos sin PersonId, ejecutar manualmente:
--   UPDATE PAY_Employees SET PersonId = ... WHERE PersonId IS NULL;
-- y luego:
--   ALTER TABLE PAY_Employees ALTER COLUMN PersonId INT NOT NULL;
-- Lo dejamos NULL temporalmente para no romper inserts vacios en dev.
GO

-- ---------------------------------------------------------------------------
-- Limpieza de helpers
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS dbo.sp__DropColumnIfExists;
DROP PROCEDURE IF EXISTS dbo.sp__DropDefaultIfExists;
GO

PRINT N'';
PRINT N'================================================================';
PRINT N'  Migracion 13 completada exitosamente';
PRINT N'================================================================';
GO
