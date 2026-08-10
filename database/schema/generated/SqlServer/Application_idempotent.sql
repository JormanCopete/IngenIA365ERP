-- ============================================================
-- IngenIA365ERP — script idempotente para DBA (feature 004)
-- Contexto : ApplicationDbContext · Proveedor: SqlServer
-- Generado : 2026-08-04 12:58:19 UTC
-- Desde    : (inicio) → (última migración)
-- Ejecución: re-ejecutable sin efectos (IF NOT EXISTS / historial
--            __EFMigrationsHistory). Reversión: Down() de cada
--            migración (DbMigrator/dotnet-ef) o restore de backup.
-- REQUIERE BACKUP PREVIO en producción (principio XII).
-- ============================================================
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_AccountGroups] (
        [Id] int NOT NULL IDENTITY,
        [GroupNumber] int NULL,
        [GroupType] int NULL,
        [AccountCode] nvarchar(15) NULL,
        [Description] nvarchar(100) NULL,
        [ReportOrder] int NULL,
        [Level] int NULL,
        [ParentGroupId] int NULL,
        [FinancialStatementCode] nvarchar(5) NULL,
        [SpecialCode] nvarchar(2) NULL,
        [ParentGroupId1] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_AccountGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_AccountGroups_ACC_AccountGroups_ParentGroupId] FOREIGN KEY ([ParentGroupId]) REFERENCES [dbo].[ACC_AccountGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_AccountGroups_ACC_AccountGroups_ParentGroupId1] FOREIGN KEY ([ParentGroupId1]) REFERENCES [dbo].[ACC_AccountGroups] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_AccountingPeriods] (
        [Id] int NOT NULL IDENTITY,
        [ModuleCode] nvarchar(5) NOT NULL,
        [Year] int NOT NULL,
        [PeriodNumber] tinyint NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] nvarchar(2) NOT NULL DEFAULT N'O',
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_AccountingPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_BankReconciliationFlats] (
        [Id] bigint NOT NULL IDENTITY,
        [PeriodCode] int NULL,
        [AccountCode] nvarchar(15) NULL,
        [AccountType] nvarchar(20) NULL,
        [TransactionCode] nvarchar(50) NULL,
        [AccountNumber] nvarchar(50) NULL,
        [TransactionDate] nvarchar(10) NULL,
        [DocumentNumber] nvarchar(50) NULL,
        [Amount] decimal(18,2) NULL,
        [TransactionType] nvarchar(5) NULL,
        [UserName] nvarchar(50) NULL,
        [SystemDate] datetime2 NULL,
        [IsProcessed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Description] nvarchar(200) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_BankReconciliationFlats] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_ChartOfAccounts] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(20) NULL,
        [AccountCode] nvarchar(20) NOT NULL,
        [Nature] nvarchar(1) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Level] tinyint NOT NULL,
        [Rate] decimal(10,4) NOT NULL,
        [RequiresDocument] bit NOT NULL,
        [ManagesCostCenter] bit NOT NULL,
        [RequiresThirdParty] bit NOT NULL,
        [IsOperationalAsset] bit NOT NULL,
        [AppliesToLending] bit NOT NULL,
        [AppliesToSavingsCDT] bit NOT NULL,
        [AppliesToInventory] bit NOT NULL,
        [AppliesToTreasury] bit NOT NULL,
        [AppliesToPayroll] bit NOT NULL,
        [AppliesToAccounting] bit NOT NULL,
        [AppliesToInvoicing] bit NOT NULL,
        [CostCenterCode] nvarchar(20) NULL,
        [FixedAssetGroup] nvarchar(10) NULL,
        [FixedAssetClass] nvarchar(10) NULL,
        [Status] int NOT NULL,
        [CashFlowCode] nvarchar(10) NULL,
        [BankReconciliationCode] nvarchar(10) NULL,
        [WithholdingType] nvarchar(5) NULL,
        [AccountBelongsTo] nvarchar(5) NULL,
        [ReportFormatId] int NULL,
        [ConceptId] int NULL,
        [SourceId] int NULL,
        [AccountGroup] nvarchar(10) NULL,
        [WithholdingLineCode] nvarchar(10) NULL,
        [IcaLineCode] nvarchar(10) NULL,
        [VatLineCode] nvarchar(10) NULL,
        [SalesWithholdingLineCode] nvarchar(10) NULL,
        [IncomeTaxDeclaration] bit NOT NULL,
        [GmfLineCode] nvarchar(10) NULL,
        [IcaBaseLineCode] nvarchar(10) NULL,
        [GmfBaseLineCode] nvarchar(10) NULL,
        [FinStmtCashFlowNumber] int NULL,
        [FinStmtCashFlowSubgroup] int NULL,
        [FinStmtChangeNumber] int NULL,
        [FinStmtChangeSubgroup] int NULL,
        [FinStmtFinPosNumber] int NULL,
        [FinStmtFinPosSubgroup] int NULL,
        [FinStmtBalSheetNumber] int NULL,
        [FinStmtBalSheetSubgroup] int NULL,
        [FinStmtEquityNumber] int NULL,
        [FinStmtEquitySubgroup] int NULL,
        [FinStmtIncomeNumber] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ACC_ChartOfAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_DianReportFormats] (
        [Id] int NOT NULL IDENTITY,
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [FormatCode] nvarchar(10) NULL,
        [Description] nvarchar(100) NULL,
        [Threshold] decimal(18,0) NOT NULL,
        [MinorTaxId] nvarchar(20) NULL,
        [BalanceThreshold] decimal(18,0) NOT NULL,
        [DianTaxId] nvarchar(15) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_DianReportFormats] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_ExchangeRateHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [CurrencyCode] nvarchar(5) NULL,
        [EffectiveDate] date NULL,
        [ExchangeRate] decimal(18,6) NOT NULL,
        [SourceAccountCode] nvarchar(15) NULL,
        [TargetAccountCode] nvarchar(15) NULL,
        [GroupNumber] int NULL,
        [SubgroupNumber] int NULL,
        [PeriodCode] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [StatementCode] nvarchar(5) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_ExchangeRateHistory] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_FinancialReportParams] (
        [Id] int NOT NULL IDENTITY,
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [ParameterOption] int NOT NULL,
        [ParameterValue] nvarchar(50) NOT NULL,
        [ValueOption] int NOT NULL,
        [CalculationBase] int NOT NULL,
        [Formula] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_FinancialReportParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_FinancialReportValues] (
        [Id] int NOT NULL IDENTITY,
        [FormatId] int NOT NULL,
        [ValueId] int NOT NULL,
        [ValueCode] nvarchar(20) NULL,
        [Description] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_FinancialReportValues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_FiscalPeriods] (
        [Id] int NOT NULL IDENTITY,
        [PeriodYear] int NOT NULL,
        [PeriodMonth] tinyint NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] nvarchar(2) NOT NULL DEFAULT N'O',
        [ClosedAt] datetime2 NULL,
        [ClosedBy] nvarchar(100) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_FiscalPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_GmfTaxLines] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [LineCode] nvarchar(10) NOT NULL,
        [Description] nvarchar(100) NULL,
        [AccountCode] nvarchar(15) NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [BaseAccountCode] nvarchar(15) NULL,
        [Sign] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_GmfTaxLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_GroupNames] (
        [Id] int NOT NULL IDENTITY,
        [GroupNumber] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_GroupNames] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_IcaTaxLines] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [LineCode] nvarchar(10) NOT NULL,
        [Description] nvarchar(100) NULL,
        [AccountCode] nvarchar(15) NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [BaseAccountCode] nvarchar(15) NULL,
        [Sign] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_IcaTaxLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_IncomeTaxLines] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [LineCode] nvarchar(10) NOT NULL,
        [Description] nvarchar(100) NULL,
        [AccountCode] nvarchar(15) NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [BaseAccountCode] nvarchar(15) NULL,
        [Sign] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_IncomeTaxLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_RiskCategories] (
        [Id] int NOT NULL IDENTITY,
        [AccountCode] nvarchar(15) NOT NULL,
        [PeriodCode] nvarchar(10) NOT NULL,
        [InitialBalance] decimal(18,2) NOT NULL,
        [Day1] decimal(18,2) NOT NULL,
        [Day2] decimal(18,2) NOT NULL,
        [Day3] decimal(18,2) NOT NULL,
        [Day4] decimal(18,2) NOT NULL,
        [Day5] decimal(18,2) NOT NULL,
        [Day6] decimal(18,2) NOT NULL,
        [Day7] decimal(18,2) NOT NULL,
        [Day8] decimal(18,2) NOT NULL,
        [Day9] decimal(18,2) NOT NULL,
        [Day10] decimal(18,2) NOT NULL,
        [Day11] decimal(18,2) NOT NULL,
        [Day12] decimal(18,2) NOT NULL,
        [Day13] decimal(18,2) NOT NULL,
        [Day14] decimal(18,2) NOT NULL,
        [Day15] decimal(18,2) NOT NULL,
        [Day16] decimal(18,2) NOT NULL,
        [Day17] decimal(18,2) NOT NULL,
        [Day18] decimal(18,2) NOT NULL,
        [Day19] decimal(18,2) NOT NULL,
        [Day20] decimal(18,2) NOT NULL,
        [Day21] decimal(18,2) NOT NULL,
        [Day22] decimal(18,2) NOT NULL,
        [Day23] decimal(18,2) NOT NULL,
        [Day24] decimal(18,2) NOT NULL,
        [Day25] decimal(18,2) NOT NULL,
        [Day26] decimal(18,2) NOT NULL,
        [Day27] decimal(18,2) NOT NULL,
        [Day28] decimal(18,2) NOT NULL,
        [Day29] decimal(18,2) NOT NULL,
        [Day30] decimal(18,2) NOT NULL,
        [Day31] decimal(18,2) NOT NULL,
        [AverageBalance] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_RiskCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_StampTaxes] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(5) NULL,
        [Grade] nvarchar(5) NOT NULL,
        [DebitAccountCode] nvarchar(15) NULL,
        [CreditAccountCode] nvarchar(15) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_StampTaxes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_SubgroupNames] (
        [Id] int NOT NULL IDENTITY,
        [SubgroupNumber] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_SubgroupNames] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_TaxFormCodes] (
        [Id] int NOT NULL IDENTITY,
        [FormCode] nvarchar(10) NULL,
        [Description] nvarchar(100) NULL,
        [AccountCode] nvarchar(15) NULL,
        [ConceptCode] nvarchar(10) NULL,
        [TaxType] nvarchar(5) NULL,
        [EconomicActivity] nvarchar(5) NULL,
        [ContributorType] nvarchar(2) NULL,
        [DocumentTypeCode] nvarchar(5) NULL,
        [RepresentationCode] nvarchar(2) NULL,
        [AuditorCode] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_TaxFormCodes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_VatTaxLines] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [LineCode] nvarchar(10) NOT NULL,
        [Description] nvarchar(100) NULL,
        [AccountCode] nvarchar(15) NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [BaseAccountCode] nvarchar(15) NULL,
        [Sign] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_VatTaxLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_VoucherTypes] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Code] nvarchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(20) NULL,
        [DocumentType] nvarchar(5) NULL,
        [AccountingAccountCode] nvarchar(15) NULL,
        [UpdatesAccounting] bit NOT NULL DEFAULT CAST(0 AS bit),
        [NextSequenceNumber] bigint NOT NULL,
        [EquivalentAccountCode] nvarchar(15) NULL,
        [RequiresDetail] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PrintFormat] nvarchar(2) NULL,
        [CostCenterCode] nvarchar(10) NULL,
        [ControlSequential] bit NOT NULL DEFAULT CAST(0 AS bit),
        [BankReconciliationCode] nvarchar(5) NULL,
        [DebitCredit] nvarchar(1) NULL,
        [HasValidator] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ValidatorPort] nvarchar(10) NULL,
        [AutomaticDetail] nvarchar(50) NULL,
        [TreasuryRestriction] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Affects3xMil] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DocumentControlType] nvarchar(2) NULL,
        [MoneyLaundering] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ModuleCode] nvarchar(5) NULL,
        [EquivalentVoucherCode] nvarchar(5) NULL,
        [EquivalentDocumentCode] nvarchar(5) NULL,
        [AccountCode2] nvarchar(15) NULL,
        [Nature] nvarchar(1) NULL,
        [DianReportFlag] smallint NULL,
        [SequentialFormat] smallint NULL,
        [ReceiptInvoice] nvarchar(1) NULL,
        [InvoiceControlCode] nvarchar(5) NULL,
        [ReturnOverdue] nvarchar(1) NULL,
        [ConversionRate] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_VoucherTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ACC_VoucherTypes_Code] UNIQUE ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_WithholdingTaxLines] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [LineCode] nvarchar(10) NOT NULL,
        [Description] nvarchar(100) NULL,
        [AccountCode] nvarchar(15) NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [BaseAccountCode] nvarchar(15) NULL,
        [Sign] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_WithholdingTaxLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUserLoginAttempts] (
        [Id] bigint NOT NULL IDENTITY,
        [CentralUserId] uniqueidentifier NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [Result] int NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(512) NULL,
        [Timestamp] datetime2 NOT NULL,
        [LockoutAppliedSeconds] int NULL,
        CONSTRAINT [PK_ADM_CentralUserLoginAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUsers] (
        [Id] uniqueidentifier NOT NULL,
        [MfaSecret] nvarchar(512) NULL,
        [DefaultTenantId] uniqueidentifier NULL,
        [IsGlobalMasterAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] int NOT NULL DEFAULT 0,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(256) NULL,
        [UserName] nvarchar(max) NULL,
        [NormalizedUserName] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [NormalizedEmail] nvarchar(max) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_ADM_CentralUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Invitations] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [InvitedByUserId] uniqueidentifier NOT NULL,
        [InviteAsTenantAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TokenHash] binary(32) NOT NULL,
        [Status] int NOT NULL DEFAULT 0,
        [ExpiresAt] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [AcceptedByCentralUserId] uniqueidentifier NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedByUserId] uniqueidentifier NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Invitations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_PasswordResetTokens] (
        [Id] int NOT NULL IDENTITY,
        [CentralUserId] uniqueidentifier NOT NULL,
        [TokenHash] binary(32) NOT NULL,
        [RequesterIp] nvarchar(45) NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(256) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_ADM_PasswordResetTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_TenantMemberships] (
        [Id] int NOT NULL IDENTITY,
        [CentralUserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL DEFAULT 0,
        [IsTenantAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [InvitedByUserId] uniqueidentifier NULL,
        [InvitedAt] datetime2 NOT NULL,
        [ActivatedAt] datetime2 NULL,
        [SuspendedAt] datetime2 NULL,
        [SuspendedByUserId] uniqueidentifier NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedByUserId] uniqueidentifier NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_TenantMemberships] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_TenantMfaPolicies] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [IsRequired] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ActivatedAt] datetime2 NULL,
        [ActivatedByUserId] uniqueidentifier NULL,
        [DeactivatedAt] datetime2 NULL,
        [DeactivatedByUserId] uniqueidentifier NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_TenantMfaPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Tenants] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [SchemaName] nvarchar(50) NOT NULL,
        [Subdomain] nvarchar(100) NULL,
        [PlanType] nvarchar(50) NOT NULL DEFAULT N'Basic',
        [Nit] nvarchar(20) NULL,
        [LegalName] nvarchar(200) NULL,
        [LegalAddress] nvarchar(300) NULL,
        [TaxRegime] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [MaxUsers] int NOT NULL,
        [StorageLimitMb] bigint NOT NULL,
        [DatabaseName] nvarchar(100) NULL,
        [ContactEmail] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(30) NULL,
        [ActivatedAt] datetime2 NULL,
        [SuspendedAt] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_AccountChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_AccountChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_AssignmentChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_AssignmentChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_AuditReferences] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] bigint NOT NULL,
        [Action] nvarchar(10) NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [Timestamp] datetime2 NOT NULL,
        [ExternalDocumentId] nvarchar(100) NULL,
        [Summary] nvarchar(500) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_AuditReferences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_CompanyChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_CompanyChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_DefaultChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_DefaultChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_JournalChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] bigint NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_JournalChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_MasterChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_MasterChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_MenuChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_MenuChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_PeriodChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_PeriodChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_PortfolioMasterChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_PortfolioMasterChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_PortfolioTransactionChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] bigint NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_PortfolioTransactionChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_SavingsChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_SavingsChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_UserChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_UserChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AUD_VoucherTypeChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(10) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AdditionalInfo] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AUD_VoucherTypeChanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_Audit] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(1) NOT NULL,
        [ActionDate] datetime2 NULL,
        [UserName] nvarchar(50) NULL,
        [EntityType] nvarchar(50) NULL,
        [EntityId] int NULL,
        [PersonId] int NULL,
        [CreditLineId] int NULL,
        [IssueDateOld] date NULL,
        [IssueDateNew] date NULL,
        [LegalRepIdOld] nvarchar(20) NULL,
        [LegalRepIdNew] nvarchar(20) NULL,
        [LegalRepNameOld] nvarchar(50) NULL,
        [LegalRepNameNew] nvarchar(50) NULL,
        [AddressOld] nvarchar(50) NULL,
        [AddressNew] nvarchar(50) NULL,
        [PhoneOld] nvarchar(20) NULL,
        [PhoneNew] nvarchar(20) NULL,
        [MobileOld] nvarchar(20) NULL,
        [MobileNew] nvarchar(20) NULL,
        [StatusOld] nvarchar(2) NULL,
        [StatusNew] nvarchar(2) NULL,
        [AccrualDateOld] date NULL,
        [AccrualDateNew] date NULL,
        [MaturityDateOld] date NULL,
        [MaturityDateNew] date NULL,
        [TermOld] int NULL,
        [TermNew] int NULL,
        [InterestRateOld] decimal(6,3) NULL,
        [InterestRateNew] decimal(6,3) NULL,
        [AmountOld] decimal(18,2) NULL,
        [AmountNew] decimal(18,2) NULL,
        [CancelledByOld] nvarchar(20) NULL,
        [CancelledByNew] nvarchar(20) NULL,
        [CancellationDateOld] datetime2 NULL,
        [CancellationDateNew] datetime2 NULL,
        [IsCapitalizedOld] nvarchar(2) NULL,
        [IsCapitalizedNew] nvarchar(2) NULL,
        [PreviousCertIdOld] int NULL,
        [PreviousCertIdNew] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_Audit] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_Certificates] (
        [Id] int NOT NULL IDENTITY,
        [CertificateNumber] nvarchar(20) NOT NULL,
        [PersonId] int NOT NULL,
        [CreditLineId] int NOT NULL,
        [IssueDate] date NOT NULL,
        [MaturityDate] date NULL,
        [AccrualDate] date NULL,
        [Amount] decimal(18,2) NOT NULL,
        [InterestRate] decimal(10,6) NOT NULL,
        [Term] int NOT NULL,
        [RenewalType] nvarchar(2) NULL,
        [Status] nvarchar(2) NOT NULL,
        [PaymentMethod] nvarchar(2) NULL,
        [Periodicity] int NULL,
        [BranchId] int NULL,
        [LegalRepId] nvarchar(20) NULL,
        [LegalRepName] nvarchar(50) NULL,
        [BusinessAddress] nvarchar(50) NULL,
        [Phone] nvarchar(20) NULL,
        [Mobile] nvarchar(20) NULL,
        [SignatoryId1] nvarchar(20) NULL,
        [SignatoryId2] nvarchar(20) NULL,
        [SignatoryId3] nvarchar(20) NULL,
        [SignatoryName1] nvarchar(50) NULL,
        [SignatoryName2] nvarchar(50) NULL,
        [SignatoryName3] nvarchar(50) NULL,
        [BeneficiaryId1] nvarchar(20) NULL,
        [BeneficiaryId2] nvarchar(20) NULL,
        [BeneficiaryId3] nvarchar(20) NULL,
        [BeneficiaryId4] nvarchar(20) NULL,
        [BeneficiaryId5] nvarchar(20) NULL,
        [BeneficiaryName1] nvarchar(50) NULL,
        [BeneficiaryName2] nvarchar(50) NULL,
        [BeneficiaryName3] nvarchar(50) NULL,
        [BeneficiaryName4] nvarchar(50) NULL,
        [BeneficiaryName5] nvarchar(50) NULL,
        [BeneficiaryPct1] decimal(6,3) NOT NULL,
        [BeneficiaryPct2] decimal(6,3) NOT NULL,
        [BeneficiaryPct3] decimal(6,3) NOT NULL,
        [BeneficiaryPct4] decimal(6,3) NOT NULL,
        [BeneficiaryPct5] decimal(6,3) NOT NULL,
        [CancelledByUserId] nvarchar(20) NULL,
        [CancellationDate] datetime2 NULL,
        [IsCapitalized] bit NOT NULL,
        [PreviousCertificateId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_Certificates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_ParameterAudit] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(1) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [UserName] nvarchar(50) NULL,
        [CreditLineId] int NULL,
        [DescriptionOld] nvarchar(100) NULL,
        [DescriptionNew] nvarchar(100) NULL,
        [MinRateOld] decimal(5,2) NULL,
        [MinRateNew] decimal(5,2) NULL,
        [AnnualRateOld] decimal(12,9) NULL,
        [AnnualRateNew] decimal(12,9) NULL,
        [WithholdingRateOld] decimal(4,2) NULL,
        [WithholdingRateNew] decimal(4,2) NULL,
        [MinWithholdingAmtOld] decimal(18,2) NULL,
        [MinWithholdingAmtNew] decimal(18,2) NULL,
        [PaymentMethodOld] int NULL,
        [PaymentMethodNew] int NULL,
        [InterestConceptOld] int NULL,
        [InterestConceptNew] int NULL,
        [WithholdingConceptOld] int NULL,
        [WithholdingConceptNew] int NULL,
        [MonthlyIncrementOld] decimal(4,2) NULL,
        [MonthlyIncrementNew] decimal(4,2) NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_ParameterAudit] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_Parameters] (
        [Id] int NOT NULL IDENTITY,
        [CreditLineId] int NOT NULL,
        [Description] nvarchar(100) NULL,
        [MinimumRate] decimal(5,2) NULL,
        [AnnualRate] decimal(12,9) NULL,
        [WithholdingRate] decimal(4,2) NULL,
        [MinWithholdingAmount] decimal(18,2) NULL,
        [InterestConceptId] int NULL,
        [WithholdingConceptId] int NULL,
        [MonthlyIncrement] decimal(4,2) NULL,
        [InterestRate] decimal(10,5) NULL,
        [Term] int NULL,
        [MinAmount] decimal(18,2) NULL,
        [MaxAmount] decimal(18,2) NULL,
        [InterestPaymentType] int NOT NULL,
        [InterestType] nvarchar(2) NOT NULL DEFAULT N'S',
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SourceId] int NOT NULL,
        [TreasuryAccount] nvarchar(15) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_Parameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_RatesByTerm] (
        [Id] int NOT NULL IDENTITY,
        [CreditLineId] int NOT NULL,
        [AmountRangeStart] decimal(18,2) NOT NULL,
        [AmountRangeEnd] decimal(18,2) NOT NULL,
        [TermStart] int NOT NULL,
        [TermEnd] int NOT NULL,
        [InterestRate] decimal(10,5) NULL,
        [LastUpdated] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_RatesByTerm] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CMP_HabeasDataPolicyVersions] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [VersionNumber] int NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [ContentMarkdown] nvarchar(max) NOT NULL,
        [Sha256Hex] nvarchar(64) NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [PublishedBy] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CMP_HabeasDataPolicyVersions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_ActivityPrograms] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(60) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_ActivityPrograms] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Advisors] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(20) NULL,
        [Name] nvarchar(100) NOT NULL,
        [Address] nvarchar(120) NULL,
        [Phone] nvarchar(40) NULL,
        [City] nvarchar(20) NULL,
        [Mobile] nvarchar(30) NULL,
        [Email] nvarchar(120) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Advisors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Agreements] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [AccountNumber] nvarchar(60) NULL,
        [EntityCode] nvarchar(10) NULL,
        [Currency] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [SavingsCode] nvarchar(4) NULL,
        [CheckingCode] nvarchar(4) NULL,
        [BlockCode] nvarchar(4) NULL,
        [AvailabilityOption] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [AvailabilityLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [AvailabilityRate] decimal(10,2) NOT NULL DEFAULT 0.0,
        [AtmOption] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [AtmLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [AtmRate] decimal(10,2) NOT NULL DEFAULT 0.0,
        [AtmTransactions] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [PosOption] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [PosLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [PosRate] decimal(10,2) NOT NULL DEFAULT 0.0,
        [PosTransactions] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [ShowBalances] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [Bin] int NOT NULL DEFAULT 0,
        [AvailableLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CashLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [OutputPath] nvarchar(200) NULL,
        [InputPath] nvarchar(200) NULL,
        [AverageDays] int NOT NULL DEFAULT 0,
        [FreeTransactions] int NOT NULL DEFAULT 0,
        [HandlingFee] int NOT NULL DEFAULT 0,
        [AvailableLimit2] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CashLimit2] decimal(17,2) NOT NULL DEFAULT 0.0,
        [ServiceType] int NOT NULL DEFAULT 0,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Agreements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Attachments] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [OwnerEntityType] nvarchar(100) NOT NULL,
        [OwnerEntityPublicId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Sha256Hex] nvarchar(64) NOT NULL,
        [StoragePath] nvarchar(2000) NOT NULL,
        [StorageProvider] nvarchar(50) NOT NULL DEFAULT N'Local',
        [EncryptedDek] nvarchar(1000) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Attachments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Banks] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(30) NULL,
        [AccountCode] nvarchar(20) NULL,
        [VoucherTypeCode] nvarchar(10) NULL,
        [TransferCode] nvarchar(20) NULL,
        [AccountClass] nvarchar(2) NULL,
        [CheckDigitRequired] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LastCheckNumber] int NULL,
        [AccountingAccountCode] nvarchar(20) NULL,
        [PrintFormat] nvarchar(2) NULL,
        [Copies] smallint NULL,
        [FinancialTaxRate] decimal(6,3) NOT NULL DEFAULT 0.0,
        [FileStructure] nvarchar(4) NULL,
        [ChargesCommission] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CommissionAccount] nvarchar(20) NULL,
        [CommissionType] int NULL,
        [CommissionAmount] decimal(17,4) NULL,
        [PromptForPrinter] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ControlSequential] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Banks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Branches] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Branches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Committees] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(60) NULL,
        [CommitteeType] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Committees] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Companies] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(120) NOT NULL,
        [ShortName] nvarchar(60) NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [TaxIdCheckDigit] nvarchar(2) NULL,
        [Address] nvarchar(80) NULL,
        [Phone] nvarchar(40) NULL,
        [City] nvarchar(40) NULL,
        [Department] nvarchar(40) NULL,
        [Activity] nvarchar(10) NULL,
        [PersonCode] nvarchar(20) NULL,
        [DianCode] nvarchar(6) NULL,
        [DianCodeDescription] nvarchar(40) NULL,
        [DianResolutionNumber] nvarchar(40) NULL,
        [DianResolutionDate] date NULL,
        [DianInvoiceStart] int NOT NULL DEFAULT 0,
        [DianInvoiceEnd] int NOT NULL DEFAULT 0,
        [LegalMinimumWage] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CompanyMinimumWage] decimal(17,2) NOT NULL DEFAULT 0.0,
        [MinimumWage] decimal(17,2) NOT NULL DEFAULT 0.0,
        [ChargesDefaultInterest] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LatePaymentControl] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PaymentControl] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CollectionPeriod] nvarchar(10) NULL,
        [InitialDays] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [FinalDays] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DefaultRate] decimal(7,4) NULL,
        [GraceDays] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [UsuryRate] decimal(7,4) NULL,
        [EffectiveRate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LiquidationBase] nvarchar(2) NULL,
        [LiquidationType] nvarchar(2) NULL,
        [DiscountClass] nvarchar(2) NULL,
        [LiquidationClass] nvarchar(2) NULL,
        [QuotaType] nvarchar(2) NULL,
        [Quota] decimal(5,2) NOT NULL DEFAULT 0.0,
        [ReportClass] nvarchar(2) NULL,
        [FileName] nvarchar(40) NULL,
        [CreditSequenceNum] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        [CreditSequenceCtrl] decimal(15,0) NOT NULL DEFAULT 0.0,
        [NumCodeudores] int NOT NULL DEFAULT 0,
        [DueRangeStart01] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeEnd01] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeStart02] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeEnd02] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeStart03] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeEnd03] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeStart04] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeEnd04] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeStart05] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [DueRangeEnd05] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [ConceptCapital] nvarchar(10) NULL,
        [ConceptInterest] nvarchar(10) NULL,
        [ConceptAdmin] nvarchar(10) NULL,
        [ConceptInsurance] nvarchar(10) NULL,
        [ConceptContributions] nvarchar(10) NULL,
        [ConceptSavings] nvarchar(10) NULL,
        [ConceptAffiliation] nvarchar(10) NULL,
        [ConceptExtra] nvarchar(10) NULL,
        [ConceptImmovable] nvarchar(10) NULL,
        [ConceptService] nvarchar(10) NULL,
        [ConceptOther1] nvarchar(10) NULL,
        [ConceptOther2] nvarchar(10) NULL,
        [ConceptRevaluation] nvarchar(10) NULL,
        [ConceptContribDisp] nvarchar(10) NULL,
        [Concept4Mil] nvarchar(10) NULL,
        [ConceptWithholdingLate] nvarchar(10) NULL,
        [ConceptCdtInterest] nvarchar(10) NULL,
        [ConceptWithholding] nvarchar(10) NULL,
        [ConceptCdt] nvarchar(10) NULL,
        [ConceptSurplus] nvarchar(10) NULL,
        [ConceptCapitalLate] nvarchar(10) NULL,
        [ConceptInterestLate] nvarchar(10) NULL,
        [ConceptAdminLate] nvarchar(10) NULL,
        [ConceptInsuranceLate] nvarchar(10) NULL,
        [ConceptContribLate] nvarchar(10) NULL,
        [ConceptSavingsLate] nvarchar(10) NULL,
        [ConceptAffiliationLate] nvarchar(10) NULL,
        [PriorityCapital] nvarchar(4) NULL,
        [PriorityInterest] nvarchar(4) NULL,
        [PriorityServices] nvarchar(4) NULL,
        [PriorityDefault] nvarchar(4) NULL,
        [PriorityAdmin] nvarchar(4) NULL,
        [PriorityInsurance] nvarchar(4) NULL,
        [PriorityContributions] nvarchar(4) NULL,
        [PrioritySavings] nvarchar(4) NULL,
        [PriorityAffiliation] nvarchar(4) NULL,
        [DailyAmlLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [MonthlyAmlLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [AmlSequence] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        [CausesLegalCollection] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AccountingAccount1] nvarchar(20) NULL,
        [AccountingAccount2] nvarchar(20) NULL,
        [AccountingAccount3] nvarchar(20) NULL,
        [AccountingAccount4] nvarchar(20) NULL,
        [AdjInterest] nvarchar(20) NULL,
        [AdjOrderAccounts] nvarchar(20) NULL,
        [AdjPortfolioProvision] nvarchar(20) NULL,
        [AdjInterestProvision] nvarchar(20) NULL,
        [AdjProvisionPayroll] nvarchar(20) NULL,
        [AdjProvisionCash] nvarchar(20) NULL,
        [AdjCashContrib] nvarchar(10) NULL,
        [AdjCashCapital] nvarchar(10) NULL,
        [AdjCashInterest] nvarchar(10) NULL,
        [AdjCashDefaultInt] nvarchar(10) NULL,
        [AdjCashInsurance] nvarchar(10) NULL,
        [AdjCashService] nvarchar(10) NULL,
        [AdjCashSavings] nvarchar(10) NULL,
        [AdjCashExtra] nvarchar(10) NULL,
        [AdjCashAdmin] nvarchar(10) NULL,
        [RepresentativeName] nvarchar(80) NULL,
        [AuditorName] nvarchar(80) NULL,
        [AuditorLicense] nvarchar(30) NULL,
        [AccountantName] nvarchar(80) NULL,
        [AccountantLicense] nvarchar(30) NULL,
        [CollectionManager] nvarchar(80) NULL,
        [OtherSignerName] nvarchar(80) NULL,
        [OtherSignerPosition] nvarchar(80) NULL,
        [RepresentativeSign] varbinary(max) NULL,
        [AccountantSign] varbinary(max) NULL,
        [AuditorSign] varbinary(max) NULL,
        [CollectionSign] varbinary(max) NULL,
        [OtherSign] varbinary(max) NULL,
        [AutoCreateAccount] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AutoCreateTaxId] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AutoCreateBranch] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AutoCreateCostCenter] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DepositSequence] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CdtSequence] decimal(17,2) NOT NULL DEFAULT 0.0,
        [SequenceControl] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [ControlDepositSeq] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AdvanceConceptType] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [AdvanceVoucherCode] nvarchar(10) NULL,
        [FavorVoucherCode] nvarchar(10) NULL,
        [DebtRecoveryOption] int NOT NULL DEFAULT 0,
        [GenerateQueryCharge] bit NOT NULL DEFAULT CAST(0 AS bit),
        [QueryChargeAmount] decimal(17,2) NOT NULL DEFAULT 0.0,
        [QueryVoucherCode] nvarchar(10) NULL,
        [PayrollClass] nvarchar(2) NULL,
        [SignatureModule] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CalculateBalance] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PayrollApplicationMode] nvarchar(2) NULL,
        [CashApplicationMode] nvarchar(2) NULL,
        [ChargesCodebtor] bit NOT NULL DEFAULT CAST(0 AS bit),
        [OverrideExtra] bit NOT NULL DEFAULT CAST(0 AS bit),
        [OverrideQuota] bit NOT NULL DEFAULT CAST(0 AS bit),
        [OverrideTerm] bit NOT NULL DEFAULT CAST(0 AS bit),
        [OverrideRate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [UnifiedNetwork] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RequiresStudy] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DateRestrictionRC] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AllowCreditQuotaMod] bit NOT NULL DEFAULT CAST(0 AS bit),
        [BankReconciliation] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CalcContribProvision] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ApplyDefaultSuspension] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DefaultSuspensionDays] int NOT NULL DEFAULT 0,
        [DefaultForWithdrawn] bit NOT NULL DEFAULT CAST(0 AS bit),
        [InvoiceSequenceCtrl] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AccrualParam] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DisableCdtRate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SmtpServer] nvarchar(100) NULL,
        [SmtpSenderEmail] nvarchar(120) NULL,
        [SmtpPassword] nvarchar(100) NULL,
        [SmtpPort] int NULL,
        [SmtpEnableSsl] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WebServiceUrl] nvarchar(200) NULL,
        [PromissoryNumber] decimal(10,0) NOT NULL DEFAULT 0.0,
        [PromissoryFormat] nvarchar(2) NULL,
        [PromissoryNotes] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LegalEntityNumber] nvarchar(20) NULL,
        [LegalEntityDate] date NULL,
        [UploadAssociateWeb] int NULL,
        [UploadMovementWeb] int NULL,
        [DownloadWeb] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WithholdingAux] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WithholdingAuxAmount] decimal(17,2) NULL,
        [WithholdingAuxPct] decimal(17,2) NULL,
        [WithholdingAuxAccount] nvarchar(20) NULL,
        [DataPackageSize] int NOT NULL DEFAULT 0,
        [AgreementType] int NOT NULL DEFAULT 0,
        [FeecCode] nvarchar(10) NULL,
        [LicenseType] nvarchar(4) NULL,
        [LicenseExpiryDate] date NULL,
        [RegistrationExpiryDate] date NULL,
        [NoticeDays] int NOT NULL DEFAULT 0,
        [ReportType] nvarchar(4) NULL,
        [LegacyUser] nvarchar(20) NULL,
        [LegacyUserName] nvarchar(80) NULL,
        [LegacySystemDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_CostCenters] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(20) NULL,
        [Name] nvarchar(80) NOT NULL,
        [CompanyName] nvarchar(100) NULL,
        [CompanyTaxId] nvarchar(20) NULL,
        [PayrollType] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [Period] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [PayrollPeriodicity] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [PayrollStatus] nvarchar(30) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_CostCenters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Countries] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Countries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Diseases] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Diseases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_EmployerCompanies] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [LegacyPayrollId] int NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [TaxId] nvarchar(20) NULL,
        [PayerName] nvarchar(60) NULL,
        [Address] nvarchar(120) NULL,
        [City] nvarchar(40) NULL,
        [Phone] nvarchar(40) NULL,
        [Fax] nvarchar(40) NULL,
        [Email] nvarchar(200) NULL,
        [ExpiryDate] date NULL,
        [CutoffDate1] date NULL,
        [CutoffDay1] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [CutoffDate2] date NULL,
        [CutoffDay2] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [CutoffDate3] date NULL,
        [CutoffDay3] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [Term] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [PayrollConceptCode] nvarchar(10) NULL,
        [SubmissionFormat] nvarchar(30) NULL,
        [DiscountPercentage] decimal(10,3) NOT NULL DEFAULT 0.0,
        [DiscountType] nvarchar(2) NULL,
        [IsBlocked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [GroceryPercentage] decimal(6,3) NOT NULL DEFAULT 0.0,
        [ArpRate] decimal(6,3) NOT NULL DEFAULT 0.0,
        [AccountType] int NOT NULL DEFAULT 0,
        [CompanyType] int NOT NULL DEFAULT 0,
        [FundSourceAccount] nvarchar(60) NULL,
        [FixedProvisionAmount] decimal(12,2) NOT NULL DEFAULT 0.0,
        [MinimumWageAmount] decimal(12,2) NOT NULL DEFAULT 0.0,
        [BasicSalaryConceptId] int NULL,
        [TransportConceptId] int NULL,
        [SeveranceConceptId] int NULL,
        [SeveranceInterestId] int NULL,
        [ServiceBonusId] int NULL,
        [VacationConceptId] int NULL,
        [IntegralSalaryId] int NULL,
        [SolidarityFundId] int NULL,
        [IndemnityConceptId] int NULL,
        [SocialContrib1Id] int NULL,
        [SocialContrib2Id] int NULL,
        [SocialContrib3Id] int NULL,
        [ArpAdminConceptId] int NULL,
        [PriorSeveranceId] int NULL,
        [WithholdingTaxId] int NULL,
        [AdminLiquidationType] int NOT NULL DEFAULT 0,
        [SenaConceptId] int NULL,
        [IcbfConceptId] int NULL,
        [NightSurchargeId] int NULL,
        [VacationAbsenceId] int NULL,
        [ServiceBonus1Id] int NULL,
        [ServiceBonus2Id] int NULL,
        [ServiceBonus3Id] int NULL,
        [ConsolidatedVacId] int NULL,
        [TransportDeductionId] int NULL,
        [MaxDeductionPct] decimal(6,2) NOT NULL DEFAULT 0.0,
        [IncludeProvision] int NOT NULL DEFAULT 0,
        [EmitsInvoice] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AnnualCompId] int NULL,
        [SemiannualCompId] int NULL,
        [DiscountCompId] int NULL,
        [ThirdPartyTransfer] int NOT NULL DEFAULT 0,
        [AccountingVoucherId] nvarchar(10) NULL,
        [OffsettingAccount] nvarchar(20) NULL,
        [AccountingUpdateType] int NOT NULL DEFAULT 0,
        [BaseSalary] decimal(17,2) NOT NULL DEFAULT 0.0,
        [SolidarityBracket1] decimal(10,2) NOT NULL DEFAULT 0.0,
        [SolidarityBracket2] decimal(10,2) NOT NULL DEFAULT 0.0,
        [SolidarityBracket3] decimal(10,2) NOT NULL DEFAULT 0.0,
        [SolidarityBracket4] decimal(10,2) NOT NULL DEFAULT 0.0,
        [SolidarityBracket5] decimal(10,2) NOT NULL DEFAULT 0.0,
        [SolidarityRate1] decimal(6,3) NOT NULL DEFAULT 0.0,
        [SolidarityRate2] decimal(6,3) NOT NULL DEFAULT 0.0,
        [SolidarityRate3] decimal(6,3) NOT NULL DEFAULT 0.0,
        [SolidarityRate4] decimal(6,3) NOT NULL DEFAULT 0.0,
        [SolidarityRate5] decimal(6,3) NOT NULL DEFAULT 0.0,
        [SenaApprenticeId] int NULL,
        [MaxDaysCap] int NOT NULL DEFAULT 0,
        [DisabilityCxcConcept] int NOT NULL DEFAULT 0,
        [ProbationDays] int NOT NULL DEFAULT 0,
        [ConsolidatedVacId2] int NOT NULL DEFAULT 0,
        [DisabilityFactor] decimal(12,2) NOT NULL DEFAULT 0.0,
        [UvtValue] decimal(17,2) NOT NULL DEFAULT 0.0,
        [IncomePercentage] decimal(17,4) NOT NULL DEFAULT 0.0,
        [DeductionPercentage] decimal(17,4) NOT NULL DEFAULT 0.0,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_EmployerCompanies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Entities] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Entities] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_InvoiceParameters] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Tag415] nvarchar(20) NULL,
        [Tag8020] nvarchar(4) NULL,
        [Tag3900] nvarchar(4) NULL,
        [Tag96] nvarchar(4) NULL,
        [BarcodeType] nvarchar(4) NULL,
        [InvoicePrintParam] nvarchar(2) NULL,
        [InvoiceGroup] nvarchar(4) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_InvoiceParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_LegalAdvisors] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [Address] nvarchar(120) NULL,
        [Phone] nvarchar(40) NULL,
        [TaxId] nvarchar(30) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_LegalAdvisors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_ListParameters] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Description] nvarchar(120) NOT NULL,
        [ListType] nvarchar(4) NULL,
        [ValidateExpiryDate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_ListParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_NotificationTemplates] (
        [Id] int NOT NULL IDENTITY,
        [TemplateName] nvarchar(200) NOT NULL,
        [Channel] nvarchar(10) NOT NULL,
        [Subject] nvarchar(500) NULL,
        [BodyTemplate] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_NotificationTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_PaymentMethodChecks] (
        [Id] int NOT NULL IDENTITY,
        [VoucherTypeCode] nvarchar(10) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [Amount] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CheckNumber] nvarchar(30) NOT NULL,
        [BankCode] nvarchar(10) NOT NULL,
        [AccountNumber] nvarchar(30) NULL,
        [LegacyUser] nvarchar(30) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_PaymentMethodChecks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_PaymentMethods] (
        [Id] int NOT NULL IDENTITY,
        [VoucherTypeCode] nvarchar(10) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [Cash] decimal(17,2) NOT NULL DEFAULT 0.0,
        [Check] decimal(17,2) NOT NULL DEFAULT 0.0,
        [BankCode] nvarchar(10) NULL,
        [CheckNumber] nvarchar(30) NULL,
        [AccountNumber] nvarchar(30) NULL,
        [DebitCard] decimal(17,2) NOT NULL DEFAULT 0.0,
        [DebitCardNumber] nvarchar(30) NULL,
        [CreditCard] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CreditCardNumber] nvarchar(30) NULL,
        [OtherPayment] decimal(17,2) NOT NULL DEFAULT 0.0,
        [OtherPaymentNumber] nvarchar(30) NULL,
        [TitleAmount] int NOT NULL DEFAULT 0,
        [TitleNumber] nvarchar(60) NULL,
        [PaymentReason] nvarchar(4) NULL,
        [CashReceiptCount] int NOT NULL DEFAULT 0,
        [CheckReceiptCount] int NOT NULL DEFAULT 0,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_PaymentMethods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_PensionSeveranceParams] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_PensionSeveranceParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Positions] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Positions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Professions] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Professions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Relationships] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Relationships] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Sections] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Sections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Sequences] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(30) NULL,
        [DocumentType] nvarchar(10) NULL,
        [IsAutomatic] bit NOT NULL DEFAULT CAST(0 AS bit),
        [NextSequence] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Sequences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_SystemSettings] (
        [Id] int NOT NULL IDENTITY,
        [SettingKey] nvarchar(200) NOT NULL,
        [SettingValue] nvarchar(max) NULL,
        [ValueType] nvarchar(20) NOT NULL DEFAULT N'String',
        [Description] nvarchar(500) NULL,
        [ModulePrefix] nvarchar(10) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_SystemSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_WithdrawalReasons] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_WithdrawalReasons] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_AgreementMembers] (
        [Id] int NOT NULL IDENTITY,
        [ProcessDate] date NOT NULL,
        [UserName] nvarchar(20) NOT NULL,
        [VoucherCode] nvarchar(5) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [IsApplied] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_AgreementMembers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_AgreementParameters] (
        [Id] int NOT NULL IDENTITY,
        [AgreementCode] nvarchar(10) NOT NULL,
        [TokenRS] int NOT NULL,
        [Name] nvarchar(100) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_AgreementParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_Agreements] (
        [Id] int NOT NULL IDENTITY,
        [BatchId] bigint NOT NULL,
        [ProcessDate] nvarchar(10) NOT NULL,
        [ProcessTime] nvarchar(10) NULL,
        [CardNumber] nvarchar(25) NOT NULL,
        [AuthCode] nvarchar(20) NULL,
        [AccountNumber] nvarchar(30) NULL,
        [NetworkCode] nvarchar(2) NULL,
        [TransactionType] nvarchar(2) NULL,
        [Amount] decimal(18,2) NULL,
        [ConceptCode] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_Agreements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_Cards] (
        [Id] int NOT NULL IDENTITY,
        [BankId] int NOT NULL,
        [CardNumber] nvarchar(25) NOT NULL,
        [BinCode] nvarchar(10) NULL,
        [AccountNumber] int NULL,
        [CreditLineId] int NULL,
        [AccountType] nvarchar(5) NULL,
        [ErrorCode] nvarchar(5) NULL,
        [PersonId] int NULL,
        [OperationType] nvarchar(5) NULL,
        [Status] nvarchar(2) NULL,
        [InitialConcept] nvarchar(20) NULL,
        [AvailableBalance] decimal(18,2) NULL,
        [DailyAtmLimit] decimal(18,2) NULL,
        [DailyAtmTransactions] int NULL,
        [DailyPosLimit] decimal(18,2) NULL,
        [DailyPosTransactions] int NULL,
        [IssueDate] date NULL,
        [ExpiryDate] date NULL,
        [LastEventDate] date NULL,
        [ExecutionTime] int NULL,
        [DownloadDate] date NULL,
        [Pin] nvarchar(100) NULL,
        [Mark] int NOT NULL,
        [AvailableLimitType] int NOT NULL,
        [AtmLimitType] int NULL,
        [IsDebitOrCredit] nvarchar(2) NOT NULL DEFAULT N'D',
        [CoSigner1] nvarchar(20) NULL,
        [CoSigner2] nvarchar(20) NULL,
        [CutoffDay] int NOT NULL,
        [CreditLimit] decimal(18,2) NOT NULL,
        [TerminalId] nvarchar(30) NULL,
        [BlockReasonId] int NOT NULL,
        [BlockedByUserId] nvarchar(20) NULL,
        [DomesticAvailableBalance] decimal(18,2) NOT NULL,
        [DomesticAtmLimit] decimal(18,2) NOT NULL,
        [DomesticAtmTransactions] int NOT NULL,
        [DomesticPosLimit] decimal(18,2) NOT NULL,
        [DomesticPosTransactions] int NOT NULL,
        [DomesticAccountNumber] int NOT NULL,
        [ChargesManagement] bit NOT NULL,
        [ChargesManagementDs] bit NOT NULL,
        [LegacyLineId] decimal(18,0) NOT NULL,
        [LimitAssignmentDate] date NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_Cards] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_DailyParameters] (
        [Id] int NOT NULL IDENTITY,
        [ParameterCode] int NOT NULL,
        [BankId] nvarchar(10) NOT NULL,
        [BatchVoucherCode] nvarchar(5) NOT NULL,
        [OnlineVoucherCode] nvarchar(5) NOT NULL,
        [Description] nvarchar(50) NOT NULL,
        [LastUpdateDate] date NULL,
        [NewCardsCount] int NOT NULL,
        [LastCardNumber] nvarchar(25) NULL,
        [ClosingDate] date NULL,
        [ClosingVoucherCode] nvarchar(5) NULL,
        [ClosingSequenceNumber] bigint NOT NULL,
        [PosClosingVoucherCode] nvarchar(5) NULL,
        [PosClosingSequence] int NOT NULL,
        [ClosingFlag] nvarchar(2) NULL,
        [NetworkCommission] decimal(18,2) NOT NULL,
        [OtherNetworkCommission] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_DailyParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_PosTerminals] (
        [Id] int NOT NULL IDENTITY,
        [TerminalCode] nvarchar(20) NOT NULL,
        [InternalCode] int NOT NULL,
        [VoucherCode] nvarchar(5) NULL,
        [MerchantName] nvarchar(100) NULL,
        [Location] nvarchar(100) NULL,
        [Status] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_PosTerminals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_CommissionParameters] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceTypeId] int NOT NULL,
        [CommissionGroupId] int NOT NULL,
        [GroupId] int NOT NULL,
        [SalesRangeStart] decimal(18,2) NOT NULL,
        [SalesRangeEnd] decimal(18,2) NOT NULL,
        [CommissionRate] decimal(6,3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_CommissionParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_CommissionPriceParams] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceTypeId] int NOT NULL,
        [CommissionGroupId] int NOT NULL,
        [GroupId] int NOT NULL,
        [CustomerType] nvarchar(5) NOT NULL,
        [CommissionRate] decimal(6,3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_CommissionPriceParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_DiscountTypes] (
        [Id] int NOT NULL IDENTITY,
        [TypeCode] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NULL,
        [DiscountClass] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_DiscountTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Invoices] (
        [Id] bigint NOT NULL IDENTITY,
        [InvoiceCode] nvarchar(10) NOT NULL,
        [Resolution] nvarchar(20) NULL,
        [ResolutionDate] date NULL,
        [VatRegime] int NOT NULL,
        [Prefix] nvarchar(10) NULL,
        [InitialNumber] nvarchar(20) NULL,
        [FinalNumber] nvarchar(20) NULL,
        [InvoiceConsecutive] bigint NOT NULL,
        [EmployeeVoucherCode] nvarchar(5) NULL,
        [EmployeeCreditLineId] int NULL,
        [EmployeeDeductionType] nvarchar(2) NULL,
        [EmployeeTerm] int NULL,
        [EmployerVoucherCode] nvarchar(5) NULL,
        [EmployerCreditLineId] int NULL,
        [EmployerDeductionType] nvarchar(2) NULL,
        [EmployerTerm] int NULL,
        [AdjustmentTypeId] int NULL,
        [PortfolioVoucherCode] nvarchar(5) NULL,
        [CreditLineId] int NULL,
        [DeductionType] nvarchar(2) NULL,
        [Term] int NULL,
        [UpdatesCosts] int NOT NULL,
        [PrintsBonusTickets] bit NOT NULL,
        [OpensRegister] bit NOT NULL,
        [CommissionGroup] nvarchar(2) NULL,
        [CommissionWithholdingRate] decimal(4,2) NOT NULL,
        [PreventCostUtility] bit NOT NULL,
        [VoucherConsecutive] bit NOT NULL,
        [SingleDiscountOnly] bit NOT NULL,
        [VatWithDiscount] bit NOT NULL,
        [ThirdPartySpecialLineId] nvarchar(5) NULL,
        [ThirdPartyCreditLineId] nvarchar(5) NULL,
        [ThirdPartySpecialVoucher] nvarchar(5) NULL,
        [ThirdPartyVoucherCode] nvarchar(5) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Invoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Locations] (
        [Id] int NOT NULL IDENTITY,
        [LocationCode] int NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [ShortDescription] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Locations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_PriceListTypes] (
        [Id] int NOT NULL IDENTITY,
        [TypeCode] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NULL,
        [PriceClass] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_PriceListTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_PrimaryGroups] (
        [Id] int NOT NULL IDENTITY,
        [GroupCode] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_PrimaryGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_ProductAccounts] (
        [Id] int NOT NULL IDENTITY,
        [ProductGroupId] int NOT NULL,
        [TransactionTypeId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [LocationId] int NOT NULL,
        [VatAccountCode] nvarchar(15) NULL,
        [DiscountAccountCode] nvarchar(15) NULL,
        [TaxableSalesAccountCode] nvarchar(15) NULL,
        [NonTaxableSalesAccountCode] nvarchar(15) NULL,
        [NetAccountCode] nvarchar(15) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_ProductAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_SalesPoints] (
        [Id] int NOT NULL IDENTITY,
        [PointCode] int NOT NULL,
        [UserId] nvarchar(20) NULL,
        [TransactionTypeId] int NULL,
        [PrinterName] nvarchar(50) NULL,
        [Status] int NOT NULL,
        [DateId] datetime2 NULL,
        [ShiftId] int NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [WarehouseId] int NULL,
        [LocationId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_SalesPoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Shifts] (
        [Id] int NOT NULL IDENTITY,
        [ShiftCode] int NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [StartTime] nvarchar(10) NULL,
        [EndTime] nvarchar(10) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Shifts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_TransactionTypes] (
        [Id] int NOT NULL IDENTITY,
        [TypeCode] int NOT NULL,
        [Description] nvarchar(150) NOT NULL,
        [ShortDescription] nvarchar(80) NULL,
        [TransactionVoucherCode] nvarchar(5) NULL,
        [CostVoucherCode] nvarchar(5) NULL,
        [SequenceNumber] decimal(18,0) NOT NULL,
        [ControlsStock] int NOT NULL,
        [DocumentClass] nvarchar(5) NULL,
        [UpdatesAccounting] int NOT NULL,
        [PortfolioVoucherCode] nvarchar(5) NULL,
        [CreditLineId] int NULL,
        [DeductionType] nvarchar(2) NULL,
        [InvoiceControl] nvarchar(5) NULL,
        [TotalInPurchase] bit NOT NULL,
        [CostsProducts] bit NOT NULL,
        [IsReturn] bit NOT NULL,
        [TransfersAccounting] bit NOT NULL,
        [OrderPedido_SustainPrice] bit NOT NULL,
        [AllowsBonus] bit NOT NULL,
        [ValidatesCreditLimit] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_TransactionTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_VatAccounts] (
        [Id] int NOT NULL IDENTITY,
        [ProductGroupId] int NOT NULL,
        [TransactionTypeId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [LocationId] int NOT NULL,
        [VatRate] decimal(6,3) NOT NULL,
        [AccountType] nvarchar(5) NULL,
        [AccountCode] nvarchar(15) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_VatAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AccrualPeriods] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyCode] nvarchar(5) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [AccrualPeriodNumber] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [AccruedAmount] decimal(18,2) NOT NULL,
        [UsuryRate] decimal(6,3) NOT NULL,
        [AccrualScope] nvarchar(3) NOT NULL,
        [AccrualService] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AccrualPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ActivityEnrollments] (
        [Id] bigint NOT NULL IDENTITY,
        [ActivityCode] nvarchar(20) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [BeneficiaryId] nvarchar(20) NOT NULL,
        [RegistrationDate] datetime2 NOT NULL,
        [EntryType] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ActivityEnrollments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ApplicationAssets] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [AssetType] nvarchar(2) NOT NULL,
        [AssetClass] nvarchar(2) NOT NULL,
        [Address] nvarchar(80) NULL,
        [CityCode] int NOT NULL,
        [AssetValue] decimal(18,2) NOT NULL,
        [Brand] nvarchar(80) NULL,
        [Model] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ApplicationAssets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ApplicationCodebtors] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationId] int NOT NULL,
        [CodeudorCode] nvarchar(20) NOT NULL,
        [Salary] decimal(18,2) NULL,
        [OtherIncome] decimal(18,2) NULL,
        [RentalIncome] decimal(18,2) NULL,
        [VariableIncome] decimal(18,2) NULL,
        [EmployerDeductions] decimal(18,2) NULL,
        [ThirdPartyDebts] decimal(18,2) NULL,
        [OtherDeductions] decimal(18,2) NULL,
        [MonthlyAvailable] decimal(18,2) NULL,
        [PensionIncome] decimal(18,2) NOT NULL,
        [PensionDeduction] decimal(18,2) NOT NULL,
        [ParafiscalDeduction] decimal(18,2) NOT NULL,
        [PaymentCapacityPct] nvarchar(2) NOT NULL,
        [PersonalExpenses] decimal(15,3) NOT NULL,
        [EmployerDeductionsCash] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ApplicationCodebtors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ApplicationExtras] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [InstallmentNumber] int NOT NULL,
        [PaymentDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentForm] nvarchar(2) NOT NULL,
        [ExtraType] nvarchar(3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ApplicationExtras] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ApplicationReferences] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [ReferenceType] nvarchar(2) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Address] nvarchar(80) NULL,
        [CityCode] int NOT NULL,
        [Phone] nvarchar(25) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ApplicationReferences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AssociateActivities] (
        [Id] int NOT NULL IDENTITY,
        [ActivityType] nvarchar(2) NOT NULL,
        [ActivityCode] nvarchar(20) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [BeneficiaryId] nvarchar(20) NOT NULL,
        [EnrollmentDate] date NOT NULL,
        [Remarks] nvarchar(120) NULL,
        [Attended] nvarchar(2) NULL,
        [AttendanceDate] datetime2 NULL,
        [ActivityStartDate] datetime2 NULL,
        [ActivityEndDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AssociateActivities] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AssociateDiseases] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [DiseaseCode] int NOT NULL,
        [SystemDate] datetime2 NULL,
        [UserId] nvarchar(60) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AssociateDiseases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AssociateWithdrawals] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [EntryDate] date NOT NULL,
        [PriorStatus] nvarchar(2) NOT NULL,
        [CurrentStatus] nvarchar(2) NOT NULL,
        [ReasonCode] nvarchar(5) NOT NULL,
        [WithdrawalReasonId] int NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [Period] int NOT NULL,
        [ExpirationDate] date NOT NULL,
        [CurrentClass] nvarchar(2) NULL,
        [PriorClass] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AssociateWithdrawals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AuxAppInstallmentBeneficiaries] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationId] int NOT NULL,
        [InstallmentNumber] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [BeneficiaryCode] nvarchar(20) NOT NULL,
        [PaymentDate] date NOT NULL,
        [LineCode] nvarchar(5) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Status] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AuxAppInstallmentBeneficiaries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AuxAppInstallments] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationId] int NOT NULL,
        [InstallmentNumber] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [PaymentDate] date NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [LineCode] nvarchar(5) NOT NULL,
        [IsGenerated] nvarchar(2) NULL,
        [DeathDate] date NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AuxAppInstallments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AuxiliaryApplicationLines] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationId] int NOT NULL,
        [LineCode] nvarchar(5) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [SortOrder] int NOT NULL,
        [Installments] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AuxiliaryApplicationLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AuxiliaryApplications] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [LineCode] nvarchar(5) NOT NULL,
        [ApplicationDate] date NOT NULL,
        [ApprovalDate] date NULL,
        [PaymentDate] date NULL,
        [IsApproved] nvarchar(2) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [RequestedAmount] decimal(18,2) NOT NULL,
        [ApprovedAmount] decimal(18,2) NOT NULL,
        [LineRemarks] nvarchar(max) NULL,
        [ApprovalRemarks] nvarchar(max) NULL,
        [ApplicationRemarks] nvarchar(max) NULL,
        [IsClosed] nvarchar(2) NOT NULL,
        [BeneficiaryId] nvarchar(20) NOT NULL,
        [LegacyIdSolAux] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AuxiliaryApplications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_BiometricRecords] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [SignatureImage] varbinary(max) NULL,
        [FingerprintCode] decimal(10,0) NOT NULL,
        [FingerprintData] varbinary(max) NULL,
        [FingerprintString] nvarchar(500) NOT NULL,
        [PhotoImage] varbinary(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_BiometricRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Blacklist] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [ListCode] int NOT NULL,
        [EntryDate] date NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [IdType] nvarchar(2) NOT NULL,
        [IdentificationNumber] nvarchar(20) NOT NULL,
        [PersonType] nvarchar(2) NULL,
        [CompanyName] nvarchar(80) NOT NULL,
        [PersonName] nvarchar(80) NULL,
        [Nationality] int NOT NULL,
        [Address] nvarchar(80) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [Remarks] nvarchar(max) NULL,
        [IsImported] nvarchar(2) NOT NULL,
        [ExpirationDate] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Blacklist] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CashBases] (
        [Id] bigint NOT NULL IDENTITY,
        [CashierCode] nvarchar(20) NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [TransactionDate] date NOT NULL,
        [EntryType] nvarchar(2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReceivedByUser] nvarchar(20) NOT NULL,
        [DeliveredByUser] nvarchar(20) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CashBases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Cashiers] (
        [Id] int NOT NULL IDENTITY,
        [CashierCode] nvarchar(20) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [VoucherType] nvarchar(5) NOT NULL,
        [Status] nvarchar(2) NULL,
        [OpenDate] date NULL,
        [VoucherConsecutive] int NOT NULL,
        [Description] nvarchar(50) NOT NULL,
        [PasswordTimeout] int NOT NULL,
        [PrinterName] nvarchar(60) NOT NULL,
        [TransactionType] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Cashiers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Checkbooks] (
        [Id] int NOT NULL IDENTITY,
        [AccountNumber] bigint NOT NULL,
        [RangeStart] bigint NOT NULL,
        [RangeEnd] bigint NOT NULL,
        [DeliveryDate] date NOT NULL,
        [IsBlocked] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Checkbooks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CheckClearing] (
        [Id] bigint NOT NULL IDENTITY,
        [AccountNumber] bigint NOT NULL,
        [CheckNumber] decimal(18,2) NOT NULL,
        [DepositDate] date NOT NULL,
        [ClearingDays] int NOT NULL,
        [MaturityDate] date NOT NULL,
        [Plaza] nvarchar(2) NOT NULL,
        [BankCode] nvarchar(5) NULL,
        [Amount] decimal(18,2) NULL,
        [Status] nvarchar(2) NOT NULL,
        [ClearingType] int NOT NULL,
        [UserId] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CheckClearing] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionCases] (
        [Id] bigint NOT NULL IDENTITY,
        [Period] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [UserId] nvarchar(20) NOT NULL,
        [PromiseDate] date NULL,
        [ManagementDate] date NOT NULL,
        [ManagementStartDate] date NULL,
        [Description] nvarchar(max) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [TotalOverdueAmount] decimal(16,3) NOT NULL,
        [EmailSent] nvarchar(2) NULL,
        [CreditLineFrom] int NOT NULL,
        [CreditLineTo] int NOT NULL,
        [DaysFrom] int NOT NULL,
        [DaysTo] int NOT NULL,
        [DeductionClass] nvarchar(2) NOT NULL,
        [CompanyFrom] nvarchar(5) NOT NULL,
        [CompanyTo] nvarchar(5) NOT NULL,
        [LegalCollection] nvarchar(2) NOT NULL,
        [IsManaged] nvarchar(2) NOT NULL,
        [IsCumulative] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionCases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionNoticeParams] (
        [Id] int NOT NULL IDENTITY,
        [NoticeCode] nvarchar(3) NOT NULL,
        [DaysFrom] int NOT NULL,
        [DaysTo] int NOT NULL,
        [Detail1] nvarchar(max) NOT NULL,
        [Detail2] nvarchar(max) NOT NULL,
        [CreatorName] nvarchar(80) NOT NULL,
        [Position] nvarchar(60) NOT NULL,
        [SignatureImage] varbinary(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionNoticeParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionNotices] (
        [Id] bigint NOT NULL IDENTITY,
        [NoticeNumber] nvarchar(3) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [Period] int NOT NULL,
        [ConceptClass] nvarchar(2) NOT NULL,
        [NoticeDate] datetime2 NOT NULL,
        [Address] nvarchar(80) NOT NULL,
        [Phone] nvarchar(60) NULL,
        [CityCode] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Detail1] nvarchar(max) NULL,
        [Detail2] nvarchar(max) NULL,
        [DaysFrom] int NULL,
        [DaysTo] int NULL,
        [SendToCodeudor] nvarchar(2) NOT NULL,
        [TrailingLegend] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionNotices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionPeriods] (
        [Id] int NOT NULL IDENTITY,
        [Period] int NOT NULL,
        [UserId] nvarchar(15) NOT NULL,
        [LastPersonCode] nvarchar(20) NOT NULL,
        [DaysFrom] int NOT NULL,
        [DaysTo] int NOT NULL,
        [CreditLineFrom] int NOT NULL,
        [CreditLineTo] int NOT NULL,
        [DeductionClass] nvarchar(2) NOT NULL,
        [CompanyFrom] nvarchar(5) NOT NULL,
        [CompanyTo] nvarchar(5) NOT NULL,
        [SortOrder] nvarchar(2) NOT NULL,
        [LegalCollection] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ContributionReductionParams] (
        [Id] int NOT NULL IDENTITY,
        [CutoffPeriod] int NOT NULL,
        [AccumulationConcept] int NOT NULL,
        [ExcessAmount] decimal(15,0) NOT NULL,
        [SignatoryName] nvarchar(60) NOT NULL,
        [Position] nvarchar(40) NOT NULL,
        [MemoDetail] nvarchar(max) NOT NULL,
        [ReductionVoucher] nvarchar(5) NOT NULL,
        [WithholdingAccumConcept] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ContributionReductionParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ContributionReductions] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [SavingsLineId] int NOT NULL,
        [Period] int NOT NULL,
        [OpeningBalance] decimal(15,2) NOT NULL,
        [DayBalance1] decimal(15,2) NOT NULL,
        [DayBalance2] decimal(15,2) NOT NULL,
        [DayBalance3] decimal(15,2) NOT NULL,
        [DayBalance4] decimal(15,2) NOT NULL,
        [DayBalance5] decimal(15,2) NOT NULL,
        [DayBalance6] decimal(15,2) NOT NULL,
        [DayBalance7] decimal(15,2) NOT NULL,
        [DayBalance8] decimal(15,2) NOT NULL,
        [DayBalance9] decimal(15,2) NOT NULL,
        [DayBalance10] decimal(15,2) NOT NULL,
        [DayBalance11] decimal(15,2) NOT NULL,
        [DayBalance12] decimal(15,2) NOT NULL,
        [DayBalance13] decimal(15,2) NOT NULL,
        [DayBalance14] decimal(15,2) NOT NULL,
        [DayBalance15] decimal(15,2) NOT NULL,
        [DayBalance16] decimal(15,2) NOT NULL,
        [DayBalance17] decimal(15,2) NOT NULL,
        [DayBalance18] decimal(15,2) NOT NULL,
        [DayBalance19] decimal(15,2) NOT NULL,
        [DayBalance20] decimal(15,2) NOT NULL,
        [DayBalance21] decimal(15,2) NOT NULL,
        [DayBalance22] decimal(15,2) NOT NULL,
        [DayBalance23] decimal(15,2) NOT NULL,
        [DayBalance24] decimal(15,2) NOT NULL,
        [DayBalance25] decimal(15,2) NOT NULL,
        [DayBalance26] decimal(15,2) NOT NULL,
        [DayBalance27] decimal(15,2) NOT NULL,
        [DayBalance28] decimal(15,2) NOT NULL,
        [DayBalance29] decimal(15,2) NOT NULL,
        [DayBalance30] decimal(15,2) NOT NULL,
        [DayBalance31] decimal(15,2) NOT NULL,
        [Average] decimal(15,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ContributionReductions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CreditLineParameters] (
        [Id] int NOT NULL IDENTITY,
        [CreditLineId] int NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [AllowExtension] nvarchar(1) NOT NULL,
        [DefaultInterestFlag] nvarchar(1) NOT NULL,
        [InterestRate] decimal(10,6) NOT NULL,
        [InterestType] nvarchar(5) NOT NULL,
        [MaxTerm] int NOT NULL,
        [CreditLimit] decimal(18,2) NOT NULL,
        [ExtraRate] decimal(10,6) NOT NULL,
        [FinancialInterest] nvarchar(2) NOT NULL,
        [GuaranteeClass] nvarchar(5) NOT NULL,
        [MaxAmount] decimal(18,2) NOT NULL,
        [AffectsFlag] nvarchar(2) NOT NULL,
        [AccrualRate] decimal(10,6) NOT NULL,
        [CapitalForm] nvarchar(2) NOT NULL,
        [AdminRate] decimal(10,6) NOT NULL,
        [InstallmentType] nvarchar(5) NOT NULL,
        [DebitCreditFlag] nvarchar(2) NOT NULL,
        [SumGuarantee] nvarchar(2) NOT NULL,
        [TotalPriorInterest] nvarchar(2) NOT NULL,
        [MonthsInAdvance] int NOT NULL,
        [AccountStatement] nvarchar(2) NOT NULL,
        [ClosingInterest] nvarchar(2) NOT NULL,
        [PrimaryVoucher] nvarchar(2) NOT NULL,
        [HousingLoan] nvarchar(2) NOT NULL,
        [InsuranceRate] decimal(10,6) NOT NULL,
        [BalanceConsult] nvarchar(2) NOT NULL,
        [MinContribution] decimal(18,2) NOT NULL,
        [PendingContribution] nvarchar(3) NOT NULL,
        [AccountCode] nvarchar(20) NOT NULL,
        [GracePeriodFlag] nvarchar(1) NOT NULL,
        [GracePeriodMonths] int NOT NULL,
        [ShowBalance] nvarchar(2) NOT NULL,
        [SecondaryCostCenter] nvarchar(10) NOT NULL,
        [AdminValueMin] decimal(18,2) NOT NULL,
        [AdminValueMax] decimal(18,2) NOT NULL,
        [IncomeTaxFlag] nvarchar(2) NOT NULL,
        [AdminForm] nvarchar(3) NOT NULL,
        [Priority] nvarchar(5) NOT NULL,
        [SavingsCode] nvarchar(2) NOT NULL,
        [GuarantorRequired] nvarchar(1) NOT NULL,
        [AdminClass] nvarchar(2) NOT NULL,
        [CategoryA] nvarchar(5) NOT NULL,
        [CategoryB] nvarchar(5) NOT NULL,
        [CategoryC] nvarchar(5) NOT NULL,
        [CategoryD] nvarchar(5) NOT NULL,
        [CategoryE] nvarchar(5) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [ConsecutiveCode] nvarchar(5) NOT NULL,
        [CdatInterestRate] decimal(18,2) NOT NULL,
        [AdminConceptCode] int NOT NULL,
        [InsuranceConceptCode] int NOT NULL,
        [InterestConceptCode] int NOT NULL,
        [EquivalentRate] nvarchar(2) NOT NULL,
        [AccountInterestIncome] nvarchar(15) NOT NULL,
        [AccountInterestCxC] nvarchar(15) NOT NULL,
        [AccountInterestDefault] nvarchar(15) NOT NULL,
        [AccountInterestAdvance] nvarchar(15) NOT NULL,
        [AccountInterestOrderDebit] nvarchar(15) NOT NULL,
        [AccountInterestOrderCredit] nvarchar(15) NOT NULL,
        [FogaContribution] nvarchar(3) NOT NULL,
        [FogaClass] nvarchar(2) NOT NULL,
        [PayrollCompanyCode] nvarchar(5) NOT NULL,
        [PayrollConceptCode] nvarchar(5) NOT NULL,
        [ColumnCount] int NOT NULL,
        [ColumnTitle] nvarchar(20) NOT NULL,
        [AdditionalChargesConcept] int NOT NULL,
        [TaxRate] decimal(18,2) NOT NULL,
        [InternetEnabled] nvarchar(2) NOT NULL,
        [MaturityBehavior] nvarchar(2) NOT NULL,
        [InsuranceValueMin] decimal(18,2) NOT NULL,
        [InsuranceValueMax] decimal(18,2) NOT NULL,
        [ProjectionItem] int NOT NULL,
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SourceId] int NOT NULL,
        [CapitalizationConcept] nvarchar(5) NOT NULL,
        [SuperintendencyEquivalent] int NOT NULL,
        [ExportCifin] nvarchar(2) NOT NULL,
        [LiquidateDefaultDays] nvarchar(2) NOT NULL,
        [ModifyInstallmentType] nvarchar(2) NULL,
        [InterestTypeCode] int NOT NULL,
        [DtfRate] decimal(18,2) NOT NULL,
        [BlockProjectionDate] nvarchar(2) NOT NULL,
        [BlockOverdueAssociate] int NOT NULL,
        [ExtraPaymentApply] nvarchar(2) NOT NULL,
        [VatAccount] nvarchar(15) NOT NULL,
        [CalculateVat] nvarchar(2) NOT NULL,
        [CreditLimitType] int NOT NULL,
        [CreditLimitCalcMethod] nvarchar(3) NOT NULL,
        [CreditLimitValue] decimal(18,2) NOT NULL,
        [CreditLimitAvailable] decimal(18,2) NOT NULL,
        [CapitalAtRisk] nvarchar(2) NOT NULL,
        [InvoiceEnabled] nvarchar(2) NOT NULL,
        [VatPercentage] decimal(18,2) NOT NULL,
        [VatLineId] int NOT NULL,
        [IsVatLine] nvarchar(2) NOT NULL,
        [InvoiceGroup] nvarchar(3) NOT NULL,
        [CalculationBase] nvarchar(3) NOT NULL,
        [BasePercentage] decimal(18,2) NOT NULL,
        [DiscountConceptId] int NOT NULL,
        [PeaceSalvoSeniority] int NOT NULL,
        [LegacyLinCred] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_LND_CreditLineParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DeductionValues] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyCode] nvarchar(5) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [Period] int NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [IsAdditional] nvarchar(2) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [ConceptCode] nvarchar(8) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DeductionValues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DepositAccounts] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [DepositLineId] int NOT NULL,
        [AccountNumber] bigint NOT NULL,
        [CreationDate] date NOT NULL,
        [LegalRepresentative] nvarchar(20) NOT NULL,
        [RepresentativeName] nvarchar(50) NOT NULL,
        [CommercialAddress] nvarchar(40) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [CellPhone] nvarchar(20) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [ExemptionDate] date NULL,
        [AutoDebit] nvarchar(2) NOT NULL,
        [FirstDeductionDate] date NULL,
        [EntryDate] date NULL,
        [DeductionType] nvarchar(2) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [HasSeal] nvarchar(2) NOT NULL,
        [HasProtector] nvarchar(2) NOT NULL,
        [RegisteredSignatures] int NOT NULL,
        [RequiredSignatures] int NOT NULL,
        [SignatoryId1] nvarchar(20) NOT NULL,
        [SignatoryId2] nvarchar(20) NOT NULL,
        [SignatoryId3] nvarchar(20) NOT NULL,
        [SignatoryName1] nvarchar(50) NOT NULL,
        [SignatoryName2] nvarchar(50) NOT NULL,
        [SignatoryName3] nvarchar(50) NOT NULL,
        [BeneficiaryId1] nvarchar(20) NOT NULL,
        [BeneficiaryId2] nvarchar(20) NOT NULL,
        [BeneficiaryId3] nvarchar(20) NOT NULL,
        [BeneficiaryId4] nvarchar(20) NOT NULL,
        [BeneficiaryId5] nvarchar(20) NOT NULL,
        [BeneficiaryName1] nvarchar(50) NOT NULL,
        [BeneficiaryName2] nvarchar(50) NOT NULL,
        [BeneficiaryName3] nvarchar(50) NOT NULL,
        [BeneficiaryName4] nvarchar(50) NOT NULL,
        [BeneficiaryName5] nvarchar(50) NOT NULL,
        [BeneficiaryPct1] decimal(18,2) NOT NULL,
        [BeneficiaryPct2] decimal(18,2) NOT NULL,
        [BeneficiaryPct3] decimal(18,2) NOT NULL,
        [BeneficiaryPct4] decimal(18,2) NOT NULL,
        [BeneficiaryPct5] decimal(18,2) NOT NULL,
        [IsExempt] nvarchar(2) NOT NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [SystemDate] date NOT NULL,
        [UserId] nvarchar(20) NULL,
        [AccountType] int NOT NULL,
        [MaturityDate] date NULL,
        [CancellationDate] date NULL,
        [CancelledByUser] nvarchar(20) NULL,
        [LegacyNumCuenta] bigint NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DepositAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DepositAudit] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(2) NOT NULL,
        [PersonCode] nvarchar(20) NULL,
        [DepositLineId] int NULL,
        [AccountNumber] bigint NOT NULL,
        [CreationDate_Old] date NULL,
        [CreationDate_New] date NULL,
        [LegalRep_Old] nvarchar(20) NULL,
        [LegalRep_New] nvarchar(20) NULL,
        [RepName_Old] nvarchar(50) NULL,
        [RepName_New] nvarchar(50) NULL,
        [Address_Old] nvarchar(40) NULL,
        [Address_New] nvarchar(40) NULL,
        [Phone_Old] nvarchar(20) NULL,
        [Phone_New] nvarchar(20) NULL,
        [CellPhone_Old] nvarchar(20) NULL,
        [CellPhone_New] nvarchar(20) NULL,
        [Status_Old] nvarchar(2) NULL,
        [Status_New] nvarchar(2) NULL,
        [ExemptionDate_Old] date NULL,
        [ExemptionDate_New] date NULL,
        [AutoDebit_Old] nvarchar(2) NULL,
        [AutoDebit_New] nvarchar(2) NULL,
        [FirstDeduction_Old] date NULL,
        [FirstDeduction_New] date NULL,
        [EntryDate_Old] date NULL,
        [EntryDate_New] date NULL,
        [DeductionType_Old] nvarchar(2) NULL,
        [DeductionType_New] nvarchar(2) NULL,
        [Periodicity_Old] nvarchar(2) NULL,
        [Periodicity_New] nvarchar(2) NULL,
        [Cycle_Old] nvarchar(2) NULL,
        [Cycle_New] nvarchar(2) NULL,
        [Seal_Old] nvarchar(2) NULL,
        [Seal_New] nvarchar(2) NULL,
        [Protector_Old] nvarchar(2) NULL,
        [Protector_New] nvarchar(2) NULL,
        [RegSignatures_Old] int NULL,
        [RegSignatures_New] int NULL,
        [ReqSignatures_Old] int NULL,
        [ReqSignatures_New] int NULL,
        [Exempt_Old] nvarchar(2) NULL,
        [Exempt_New] nvarchar(2) NULL,
        [UserName_Old] nvarchar(50) NULL,
        [UserName_New] nvarchar(50) NULL,
        [SystemDate] datetime2 NOT NULL,
        [AuditUserId] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DepositAudit] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DepositEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [DepositLineId] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [AccountNumber] bigint NOT NULL,
        [EntryDate] date NOT NULL,
        [CreationDate] date NOT NULL,
        [FirstDeductionDate] date NOT NULL,
        [DeductionType] nvarchar(2) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [Term] int NOT NULL,
        [MaturityDate] date NULL,
        [EntryType] nvarchar(2) NOT NULL,
        [UserId] nvarchar(15) NOT NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [SystemDate] date NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DepositEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DepositSeals] (
        [Id] int NOT NULL IDENTITY,
        [AccountNumber] bigint NOT NULL,
        [SealImage] varbinary(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DepositSeals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DepositSignatures] (
        [Id] int NOT NULL IDENTITY,
        [AccountNumber] bigint NOT NULL,
        [SignatureNumber] int NOT NULL,
        [SignatureImage] varbinary(max) NULL,
        [IsRequired] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DepositSignatures] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Documents] (
        [Id] bigint NOT NULL IDENTITY,
        [VoucherType] nvarchar(5) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [AccountCode] nvarchar(20) NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [DebitAmount] decimal(18,2) NULL,
        [CreditAmount] decimal(18,2) NULL,
        [DocumentType] nvarchar(5) NOT NULL,
        [DocumentSequence] int NOT NULL,
        [DocumentDate] date NOT NULL,
        [CheckNumber] nvarchar(15) NOT NULL,
        [RecordFlag] nvarchar(2) NOT NULL,
        [BankCode] nvarchar(5) NOT NULL,
        [IsClosed] nvarchar(2) NOT NULL,
        [IsVoided] nvarchar(2) NOT NULL,
        [ClosedInPortfolio] nvarchar(2) NULL,
        [BeneficiaryId] nvarchar(20) NOT NULL,
        [BeneficiaryCheckId] nvarchar(20) NOT NULL,
        [LegacyCompronte] nvarchar(5) NULL,
        [LegacyNumeroDomto] bigint NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Documents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_HousingApplicationParams] (
        [Id] int NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [HousingClass] int NOT NULL,
        [HousingType] int NOT NULL,
        [SocialInterest] nvarchar(2) NOT NULL,
        [HasSubsidy] nvarchar(2) NOT NULL,
        [NetworkEntity] int NOT NULL,
        [NetworkValue] bigint NOT NULL,
        [DisbursementType] int NOT NULL,
        [CurrencyType] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_HousingApplicationParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_InterestRates] (
        [Id] int NOT NULL IDENTITY,
        [Period] int NOT NULL,
        [CreditLineCode] nvarchar(5) NOT NULL,
        [PortfolioBalance] decimal(15,2) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [PortfolioClass] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_InterestRates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_InvoiceDetails] (
        [Id] int NOT NULL IDENTITY,
        [DetailCode] nvarchar(2) NOT NULL,
        [DetailText] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_InvoiceDetails] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_InvoiceMasters] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NULL,
        [AccountingPeriod] int NULL,
        [Cycle] int NULL,
        [InvoicedAmount] decimal(18,3) NULL,
        [PaymentDeadline] date NULL,
        [Barcode] nvarchar(120) NULL,
        [UserId] nvarchar(20) NULL,
        [SystemDate] datetime2 NULL,
        [InvoiceCode] nvarchar(5) NULL,
        [EncodedData] nvarchar(200) NULL,
        [LastPaymentAmount] decimal(18,2) NOT NULL,
        [LastPaymentDate] nvarchar(20) NOT NULL,
        [PaymentType] nvarchar(3) NOT NULL,
        [InvoiceConsecutive] bigint NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_InvoiceMasters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_MinuteAttendees] (
        [Id] bigint NOT NULL IDENTITY,
        [MinutesType] nvarchar(2) NOT NULL,
        [MinutesNumber] nvarchar(20) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_MinuteAttendees] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Minutes] (
        [Id] int NOT NULL IDENTITY,
        [MinutesType] nvarchar(2) NOT NULL,
        [MinutesNumber] nvarchar(20) NOT NULL,
        [OpeningDate] datetime2 NOT NULL,
        [ClosingDate] datetime2 NULL,
        [Description] nvarchar(max) NULL,
        [Status] nvarchar(2) NOT NULL,
        [UserId] nvarchar(20) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [EntityType] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Minutes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_MoneyLaunderingDeclarations] (
        [Id] bigint NOT NULL IDENTITY,
        [DeclarationId] bigint NOT NULL,
        [DeclarationDate] datetime2 NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [EconomicActivity] nvarchar(40) NOT NULL,
        [VoucherType] nvarchar(5) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [TransactionSequence] int NOT NULL,
        [OperationType] nvarchar(3) NOT NULL,
        [OperationDetail] nvarchar(3) NOT NULL,
        [AffectedProduct] nvarchar(25) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ClientId] nvarchar(20) NOT NULL,
        [ClientIdType] nvarchar(2) NOT NULL,
        [ClientName] nvarchar(120) NOT NULL,
        [ClientSurname] nvarchar(120) NOT NULL,
        [Address] nvarchar(80) NOT NULL,
        [Phone] nvarchar(40) NULL,
        [Remarks] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_MoneyLaunderingDeclarations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PayrollDeductionEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [Period] int NOT NULL,
        [CompanyCode] nvarchar(5) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [EntryType] nvarchar(2) NOT NULL,
        [ConceptCode] nvarchar(20) NOT NULL,
        [StartDate] nvarchar(20) NOT NULL,
        [EndDate] nvarchar(20) NOT NULL,
        [Amount] decimal(12,0) NOT NULL,
        [TotalAmount] decimal(12,0) NOT NULL,
        [AccumulatedAmount] decimal(12,0) NOT NULL,
        [OrderNumber] nvarchar(5) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PayrollDeductionEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PayrollDeductionPeriods] (
        [Id] int NOT NULL IDENTITY,
        [CompanyCode] nvarchar(5) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [Period] int NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [IsAdditional] nvarchar(2) NOT NULL,
        [Description] nvarchar(50) NOT NULL,
        [DeductionClass] nvarchar(2) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [ContributionAmount] decimal(18,2) NOT NULL,
        [LoanAmount] decimal(18,2) NOT NULL,
        [InterestAmount] decimal(18,2) NOT NULL,
        [ExtraAmount] decimal(18,2) NOT NULL,
        [DefaultAmount] decimal(18,2) NOT NULL,
        [InsuranceAmount] decimal(18,2) NOT NULL,
        [AdminAmount] decimal(18,2) NOT NULL,
        [OtherAmount] decimal(18,2) NOT NULL,
        [ArrearsFrom] int NOT NULL,
        [ArrearsTo] int NOT NULL,
        [ArrearsExtras] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PayrollDeductionPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PeriodicityParameters] (
        [Id] int NOT NULL IDENTITY,
        [CompanyCode] nvarchar(5) NOT NULL,
        [DeductionClass] nvarchar(3) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [StartDay] int NOT NULL,
        [EndDay] int NOT NULL,
        [DayCount] nvarchar(3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PeriodicityParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PersonAssets] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [AssetType] nvarchar(2) NOT NULL,
        [AssetClass] nvarchar(2) NOT NULL,
        [Address] nvarchar(80) NULL,
        [CityCode] int NOT NULL,
        [AssetValue] decimal(18,2) NOT NULL,
        [Brand] nvarchar(80) NULL,
        [Model] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PersonAssets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PortfolioAccounts] (
        [Id] int NOT NULL IDENTITY,
        [AccountCode] nvarchar(15) NOT NULL,
        [RecordClass] nvarchar(3) NOT NULL,
        [Category] int NOT NULL,
        [GuaranteeType] int NOT NULL,
        [DeductionClass] int NOT NULL,
        [RiskLevel] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PortfolioAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PortfolioClassifications] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineCode] nvarchar(5) NOT NULL,
        [PortfolioNumber] int NOT NULL,
        [AccountingPeriod] int NOT NULL,
        [CreditClass] int NOT NULL,
        [GuaranteeClass] int NOT NULL,
        [DeductionClass] int NOT NULL,
        [Category] nvarchar(2) NOT NULL,
        [ConceptCode] int NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [PersonName] nvarchar(50) NOT NULL,
        [TotalBalance] decimal(18,2) NOT NULL,
        [InterestBalance] decimal(18,2) NOT NULL,
        [DefaultBalance] decimal(18,2) NOT NULL,
        [OrderBalance] decimal(18,2) NOT NULL,
        [ProvisionBalance] decimal(18,2) NOT NULL,
        [Rate] decimal(10,0) NOT NULL,
        [DaysOverdue] int NOT NULL,
        [ContributionAmount] decimal(18,2) NOT NULL,
        [InterestProvision] decimal(18,2) NOT NULL,
        [DefaultOrderBalance] decimal(18,2) NOT NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PortfolioClassifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ProvisionParameters] (
        [Id] int NOT NULL IDENTITY,
        [Period] int NOT NULL,
        [Code] int NOT NULL,
        [RateB] decimal(6,3) NOT NULL,
        [RateC] decimal(6,3) NOT NULL,
        [RateD] decimal(6,3) NOT NULL,
        [RateE] decimal(6,3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ProvisionParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_SavingsAccounts] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [SavingsLineId] int NOT NULL,
        [AccountNumber] bigint NOT NULL,
        [CreationDate] date NOT NULL,
        [LegalRepresentative] nvarchar(20) NOT NULL,
        [RepresentativeName] nvarchar(50) NOT NULL,
        [CommercialAddress] nvarchar(25) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [CellPhone] nvarchar(20) NOT NULL,
        [UniqueAccount] nvarchar(2) NOT NULL,
        [ExemptionDate] date NULL,
        [AutoDebit] nvarchar(2) NOT NULL,
        [FirstDeductionDate] date NULL,
        [MaturityDate] date NULL,
        [DeductionType] nvarchar(2) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [HasSeal] nvarchar(2) NOT NULL,
        [HasProtector] nvarchar(2) NOT NULL,
        [RegisteredSignatures] int NOT NULL,
        [RequiredSignatures] int NOT NULL,
        [SignatoryId1] nvarchar(20) NOT NULL,
        [SignatoryId2] nvarchar(20) NOT NULL,
        [SignatoryId3] nvarchar(20) NOT NULL,
        [SignatoryName1] nvarchar(50) NOT NULL,
        [SignatoryName2] nvarchar(50) NOT NULL,
        [SignatoryName3] nvarchar(50) NOT NULL,
        [BeneficiaryId1] nvarchar(20) NOT NULL,
        [BeneficiaryId2] nvarchar(20) NOT NULL,
        [BeneficiaryId3] nvarchar(20) NOT NULL,
        [BeneficiaryId4] nvarchar(20) NOT NULL,
        [BeneficiaryId5] nvarchar(20) NOT NULL,
        [BeneficiaryName1] nvarchar(50) NOT NULL,
        [BeneficiaryName2] nvarchar(50) NOT NULL,
        [BeneficiaryName3] nvarchar(50) NOT NULL,
        [BeneficiaryName4] nvarchar(50) NOT NULL,
        [BeneficiaryName5] nvarchar(50) NOT NULL,
        [BeneficiaryPct1] decimal(18,2) NOT NULL,
        [BeneficiaryPct2] decimal(18,2) NOT NULL,
        [BeneficiaryPct3] decimal(18,2) NOT NULL,
        [BeneficiaryPct4] decimal(18,2) NOT NULL,
        [BeneficiaryPct5] decimal(18,2) NOT NULL,
        [LegacyNumCuenta] bigint NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_SavingsAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_SavingsParameters] (
        [Id] int NOT NULL IDENTITY,
        [SavingsLineId] int NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [ShortName] nvarchar(25) NOT NULL,
        [InterestPaymentPeriod] nvarchar(2) NOT NULL,
        [MinInterestBalance] decimal(18,2) NOT NULL,
        [MinTransactionAmount] decimal(18,2) NOT NULL,
        [MinAccountBalance] decimal(18,2) NOT NULL,
        [InterestPaymentRate] decimal(9,5) NOT NULL,
        [MinWithholdingAmount] decimal(18,2) NOT NULL,
        [WithholdingRate] decimal(9,5) NOT NULL,
        [ClearingDays] int NOT NULL,
        [LiquidationForm] nvarchar(2) NOT NULL,
        [GraceDays] int NOT NULL,
        [MaxWithdrawalAmount] decimal(18,2) NOT NULL,
        [InterestConceptCode] nvarchar(3) NOT NULL,
        [WithholdingConceptCode] nvarchar(3) NOT NULL,
        [PaymentForm] nvarchar(2) NOT NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [FourPerMillForm] nvarchar(2) NOT NULL,
        [FourPerMillConcept] nvarchar(5) NOT NULL,
        [FourPerMillCeiling] decimal(18,2) NOT NULL,
        [FourPerMillVoucher] nvarchar(5) NOT NULL,
        [Comment] nvarchar(50) NOT NULL,
        [MaxCashAmount] decimal(18,2) NOT NULL,
        [Consecutive] decimal(18,2) NOT NULL,
        [CheckWithdrawalGmf] int NOT NULL,
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SourceId] int NOT NULL,
        [InterestConceptId] int NOT NULL,
        [OtherClearingDays] int NOT NULL,
        [ValidateWithdrawal] nvarchar(2) NOT NULL,
        [WithdrawalCeiling] decimal(18,2) NOT NULL,
        [ManagesPapForm] nvarchar(2) NOT NULL,
        [TreasuryAccount] nvarchar(15) NOT NULL,
        [LegacyLinCred] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_SavingsParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ScoringParameters] (
        [Id] int NOT NULL IDENTITY,
        [CriterionCode] nvarchar(2) NOT NULL,
        [SubItemCode] nvarchar(3) NOT NULL,
        [CriterionName] nvarchar(120) NOT NULL,
        [SubItemName] nvarchar(120) NOT NULL,
        [Percentage] decimal(4,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ScoringParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ScoringRanges] (
        [Id] int NOT NULL IDENTITY,
        [CriterionCode] nvarchar(2) NOT NULL,
        [SubItemCode] nvarchar(3) NOT NULL,
        [RangeStart] nvarchar(40) NOT NULL,
        [RangeEnd] nvarchar(40) NOT NULL,
        [ScoreValue] int NOT NULL,
        [Equality] nvarchar(3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ScoringRanges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_SiplaParameters] (
        [Id] int NOT NULL IDENTITY,
        [ConceptCode] nvarchar(3) NOT NULL,
        [CompanyCode] nvarchar(5) NOT NULL,
        [MonthlyCreditMoves] decimal(4,2) NOT NULL,
        [MaxBalance] decimal(4,2) NOT NULL,
        [AccountCount] int NOT NULL,
        [AnnualTransactions] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_SiplaParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Subsidies] (
        [Id] int NOT NULL IDENTITY,
        [SubsidyCode] nvarchar(5) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [ShortName] nvarchar(25) NOT NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [Remarks] nvarchar(max) NULL,
        [Amount] int NOT NULL,
        [SubsidyClass] int NOT NULL,
        [CommitteeCode] nvarchar(5) NOT NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [TaxAccount] nvarchar(15) NOT NULL,
        [TaxExpenseAccount] nvarchar(15) NOT NULL,
        [ControlsCeiling] nvarchar(2) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [CurrentCeiling] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [ControlsAssociateCeiling] nvarchar(2) NOT NULL,
        [AssociatePeriodicity] nvarchar(2) NOT NULL,
        [AssociateCeiling] int NOT NULL,
        [AssociateStartDate] date NOT NULL,
        [AssociateEndDate] date NOT NULL,
        [InInstallments] nvarchar(2) NOT NULL,
        [InstallmentCount] int NOT NULL,
        [ControlsSeniority] nvarchar(2) NOT NULL,
        [SeniorityYearsStart] int NOT NULL,
        [SeniorityYearsEnd] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Subsidies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_SubZones] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [Address] nvarchar(120) NOT NULL,
        [Phone] nvarchar(120) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_SubZones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_TransactionCodes] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(3) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [ShortName] nvarchar(25) NOT NULL,
        [TransactionType] nvarchar(3) NOT NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [AdjustAccrual] nvarchar(2) NOT NULL,
        [DebitCreditFlag] nvarchar(2) NOT NULL,
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SourceId] int NOT NULL,
        [LegacyCodMovto] nvarchar(3) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_TransactionCodes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_UnusualTransactionEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [UnusualTransactionId] int NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [CurrentStatus] nvarchar(2) NOT NULL,
        [PriorStatus] nvarchar(2) NULL,
        [Remarks] nvarchar(250) NOT NULL,
        [RegisteredBy] nvarchar(20) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_UnusualTransactionEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_UnusualTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [ConceptCode] nvarchar(3) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CompanyCode] nvarchar(5) NOT NULL,
        [Period] nvarchar(8) NOT NULL,
        [MonthlyCreditMoves] decimal(18,2) NOT NULL,
        [MaxBalance] decimal(18,2) NOT NULL,
        [AccountCount] int NOT NULL,
        [AnnualTransactions] int NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [UnusualType] nvarchar(3) NOT NULL,
        [VoucherType] nvarchar(5) NULL,
        [DocumentNumber] bigint NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_UnusualTransactions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_WithdrawalStatuses] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [RequestDate] date NOT NULL,
        [ReasonCode] nvarchar(5) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [EffectiveDate] date NULL,
        [Remarks] nvarchar(max) NULL,
        [Period] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_WithdrawalStatuses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Zones] (
        [Id] int NOT NULL IDENTITY,
        [ZoneId] int NOT NULL,
        [Code] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [Address] nvarchar(120) NOT NULL,
        [Phone] nvarchar(120) NOT NULL,
        [SubZoneId] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Zones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ZoneTypes] (
        [Id] int NOT NULL IDENTITY,
        [ZoneTypeId] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ZoneTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_AutoContributionParams] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [IdType] int NOT NULL,
        [IdNumber] int NOT NULL,
        [CheckDigit] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Address] nvarchar(100) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [Fax] nvarchar(30) NOT NULL,
        [CityId] int NOT NULL,
        [CityName] nvarchar(50) NOT NULL,
        [DepartmentId] int NOT NULL,
        [DepartmentName] nvarchar(50) NOT NULL,
        [HealthRate] decimal(6,3) NOT NULL,
        [PensionRate] decimal(6,3) NOT NULL,
        [WorkRiskRate] decimal(6,3) NOT NULL,
        [CcfRate] decimal(6,3) NOT NULL,
        [SenaRate] decimal(6,3) NOT NULL,
        [IcbfRate] decimal(6,3) NOT NULL,
        [SolidarityFundRate] decimal(6,3) NOT NULL,
        [EsapRate] decimal(6,3) NOT NULL,
        [EducationMinRate] decimal(6,3) NOT NULL,
        [CcfAdminCode] nvarchar(6) NOT NULL,
        [WorkRiskAdminCode] nvarchar(6) NOT NULL,
        [LatePaymentRate] decimal(6,3) NOT NULL,
        [LinkType] int NOT NULL,
        [ContributionType] int NOT NULL,
        [HealthCoverage] int NOT NULL,
        [ContributionBase] int NOT NULL,
        [EmployerNumber] nvarchar(15) NOT NULL,
        [MinimumWage] decimal(18,2) NOT NULL,
        [ProvisionRegime] int NOT NULL,
        [FormNumber] nvarchar(15) NOT NULL,
        [CorrectionDate] datetime2 NOT NULL,
        [ContributionClass] int NOT NULL,
        [LegalNature] int NOT NULL,
        [EconomicActivityId] int NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [RepresentativeId] nvarchar(15) NOT NULL,
        [RepresentativeCheckDigit] nvarchar(1) NOT NULL,
        [RepresentativeLastName1] nvarchar(30) NOT NULL,
        [RepresentativeLastName2] nvarchar(30) NOT NULL,
        [RepresentativeFirstName1] nvarchar(30) NOT NULL,
        [RepresentativeFirstName2] nvarchar(30) NOT NULL,
        [PresentationMethod] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_AutoContributionParams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_HealthInsuranceProviders] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [CheckDigit] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_HealthInsuranceProviders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PayPeriods] (
        [Id] int NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [Description] nvarchar(100) NULL,
        [PayDate] nvarchar(40) NULL,
        [LiquidationCompanyId] nvarchar(4) NULL,
        [CycleMonth] int NULL,
        [CycleHours] int NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Periodicity] int NULL,
        [AdditionalConcept1] int NULL,
        [AdditionalConcept2] int NULL,
        [AdditionalConcept3] int NULL,
        [AdditionalConcept4] int NULL,
        [OnlyEntries] nvarchar(1) NULL,
        [NoAutoSalaryLiq] nvarchar(1) NULL,
        [NoAbsenceLiq] nvarchar(1) NULL,
        [NoDirectDebitLiq] nvarchar(1) NULL,
        [Status] int NOT NULL,
        [StatusMessage] nvarchar(100) NOT NULL,
        [PeriodId] int NOT NULL,
        [AdvanceLiquidation] nvarchar(1) NOT NULL,
        [AdvanceCrossing] nvarchar(1) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PayPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PayrollConcepts] (
        [Id] int NOT NULL IDENTITY,
        [ConceptCode] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [ConceptClass] int NOT NULL,
        [Nature] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [Factor] decimal(12,2) NOT NULL,
        [Base] int NOT NULL,
        [AffectsSalary] int NOT NULL,
        [DaysComputed] nvarchar(2) NOT NULL,
        [TimesExtended] int NOT NULL,
        [ValueExtended] int NOT NULL,
        [LiquidationBase] int NOT NULL,
        [TopSalary] decimal(18,2) NOT NULL,
        [CertificateLine] nvarchar(4) NOT NULL,
        [CertificateColumn] nvarchar(4) NOT NULL,
        [AffectsBenefits] int NOT NULL,
        [AffectsWithholding] int NOT NULL,
        [IsBenefit] int NOT NULL,
        [MaintainBalance] int NOT NULL,
        [Priority] nvarchar(2) NOT NULL,
        [ProvisionRate] decimal(6,3) NOT NULL,
        [ProvisionBase] int NOT NULL,
        [TaxId] nvarchar(14) NOT NULL,
        [IntegralSalary] int NOT NULL,
        [SingleUnit] int NOT NULL,
        [AdminBase] int NOT NULL,
        [RelatedConceptId] int NOT NULL,
        [PaymentConceptId] int NOT NULL,
        [VatRate] decimal(6,3) NOT NULL,
        [EquivalentCode] nvarchar(6) NOT NULL,
        [MinorRate] decimal(4,2) NOT NULL,
        [MajorRate] decimal(4,2) NOT NULL,
        [AffectsSeverance] int NOT NULL,
        [AffectsBonus] int NOT NULL,
        [AffectsVacation] int NOT NULL,
        [AffectsIndemnity] int NOT NULL,
        [AdminId] nvarchar(6) NOT NULL,
        [IsAutomatic] nvarchar(1) NOT NULL,
        [ConceptSubClass] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PayrollConcepts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PensionProviders] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [CheckDigit] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PensionProviders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PreLiquidationResponses] (
        [Id] bigint NOT NULL IDENTITY,
        [PilaCode] nvarchar(10) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [CheckDigit] int NOT NULL,
        [TotalEmployees] int NOT NULL,
        [IbcAmount] int NOT NULL,
        [ContributionAmount] int NOT NULL,
        [UpcAmount] int NOT NULL,
        [SolidarityAmount] int NOT NULL,
        [GeneralAuth] int NOT NULL,
        [GeneralValue] int NOT NULL,
        [MaternityAuth] int NOT NULL,
        [MaternityValue] int NOT NULL,
        [WorkRiskAuth] nvarchar(15) NOT NULL,
        [WorkRiskValue] int NOT NULL,
        [EntityClass] nvarchar(5) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PreLiquidationResponses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_SeveranceProviders] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [CheckDigit] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_SeveranceProviders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_WithholdingCauses] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [IndemnityType] int NOT NULL,
        [AutoDeductions] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_WithholdingCauses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_WithholdingParameters] (
        [Id] int NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [UvtRangeStart] int NOT NULL,
        [UvtRangeEnd] int NOT NULL,
        [Rate] decimal(17,4) NOT NULL,
        [AdditionalUvt] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_WithholdingParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_WorkRiskProviders] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [CheckDigit] int NOT NULL,
        [Factor] decimal(8,4) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_WorkRiskProviders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_WorkRiskRates] (
        [Id] int NOT NULL IDENTITY,
        [Code] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [Rate] decimal(18,8) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_WorkRiskRates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_LoginAttempts] (
        [Id] bigint NOT NULL IDENTITY,
        [Email] nvarchar(200) NULL,
        [IpAddress] nvarchar(50) NULL,
        [UserAgent] nvarchar(500) NULL,
        [AttemptedAt] datetime2 NOT NULL,
        [WasSuccessful] bit NOT NULL,
        [FailureReason] nvarchar(200) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_LoginAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_Permissions] (
        [Id] int NOT NULL IDENTITY,
        [Resource] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[TRS_Checks] (
        [Id] bigint NOT NULL IDENTITY,
        [ConceptCode] nvarchar(5) NOT NULL,
        [BankId] int NOT NULL,
        [SequentialNumber] int NOT NULL,
        [PersonId] int NULL,
        [VoucherCode] nvarchar(5) NULL,
        [VoucherNumber] int NULL,
        [CheckDate] date NOT NULL,
        [CheckNumber] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [VoidDetail] nvarchar(50) NULL,
        [VoidUserId] nvarchar(20) NULL,
        [VoidVoucherCode] nvarchar(5) NULL,
        [VoidVoucherNumber] int NULL,
        [VoidDate] datetime2 NULL,
        [RecordUserId] nvarchar(20) NULL,
        [RecordDate] datetime2 NULL,
        [Status] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TRS_Checks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[TRS_Concepts] (
        [Id] int NOT NULL IDENTITY,
        [ConceptCode] nvarchar(5) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [ShortName] nvarchar(20) NULL,
        [ConceptType] nvarchar(5) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TRS_Concepts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[TRS_Invoices] (
        [Id] bigint NOT NULL IDENTITY,
        [ConceptCode] nvarchar(5) NOT NULL,
        [ConsecutiveNumber] bigint NOT NULL,
        [EntryDate] date NOT NULL,
        [PeriodCode] int NULL,
        [InvoiceNumber] nvarchar(20) NOT NULL,
        [InvoiceDate] date NOT NULL,
        [PersonId] int NOT NULL,
        [DueDate] date NULL,
        [AccountCode] nvarchar(15) NULL,
        [CostCenterId] nvarchar(10) NULL,
        [BranchId] nvarchar(10) NULL,
        [DocumentCode] nvarchar(15) NULL,
        [Description] nvarchar(200) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ScheduledDate] date NULL,
        [PaymentDate] date NULL,
        [VoucherCode] nvarchar(5) NULL,
        [VoucherNumber] int NULL,
        [BankId] nvarchar(10) NULL,
        [CheckNumber] int NULL,
        [CheckAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(2) NULL,
        [DocumentType] nvarchar(5) NULL,
        [DocumentNumber] nvarchar(20) NULL,
        [PaymentForm] nvarchar(5) NULL,
        [HasCommission] bit NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TRS_Invoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WEB_AffiliationApplications] (
        [Id] bigint NOT NULL IDENTITY,
        [SequenceNumber] int NULL,
        [ApplicationDate] date NULL,
        [IpAddress] nvarchar(50) NULL,
        [IdentificationNumber] nvarchar(20) NULL,
        [IdentificationType] nvarchar(5) NULL,
        [FirstName] nvarchar(150) NULL,
        [LastName] nvarchar(150) NULL,
        [Address] nvarchar(250) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(200) NULL,
        [EmployerName] nvarchar(100) NULL,
        [Salary] decimal(18,2) NULL,
        [SpouseFirstName] nvarchar(150) NULL,
        [SpouseLastName] nvarchar(150) NULL,
        [SpouseIdentificationNumber] nvarchar(20) NULL,
        [SpouseIdentificationType] nvarchar(5) NULL,
        [SpousePhone] nvarchar(30) NULL,
        [SpouseEmail] nvarchar(200) NULL,
        [SpouseEmployerName] nvarchar(100) NULL,
        [SpouseSalary] decimal(18,2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WEB_AffiliationApplications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WEB_AuxiliaryApplications] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonId] int NULL,
        [SequenceNumber] int NULL,
        [ApplicationDate] date NULL,
        [IpAddress] nvarchar(50) NULL,
        [SubsidyType] nvarchar(5) NULL,
        [Amount] decimal(18,2) NULL,
        [Reason] nvarchar(max) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WEB_AuxiliaryApplications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WEB_DataUpdates] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonId] int NULL,
        [SequenceNumber] int NULL,
        [UpdateDate] date NULL,
        [IpAddress] nvarchar(50) NULL,
        [NewAddress] nvarchar(250) NULL,
        [NewPhone] nvarchar(30) NULL,
        [NewMobilePhone] nvarchar(30) NULL,
        [NewEmail] nvarchar(200) NULL,
        [NewCity] nvarchar(100) NULL,
        [NewDepartment] nvarchar(100) NULL,
        [NewEmployerName] nvarchar(100) NULL,
        [NewEmployerAddress] nvarchar(250) NULL,
        [NewEmployerPhone] nvarchar(30) NULL,
        [NewPosition] nvarchar(100) NULL,
        [NewSalary] decimal(18,2) NULL,
        [NewMaritalStatus] nvarchar(5) NULL,
        [NewEducationLevel] nvarchar(5) NULL,
        [NewHousingType] nvarchar(5) NULL,
        [SystemDate] datetime2 NULL,
        [IsProcessed] bit NULL,
        [ProcessedBy] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WEB_DataUpdates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WEB_ExtraPayments] (
        [Id] bigint NOT NULL IDENTITY,
        [SequenceNumber] int NULL,
        [PaymentDate] date NULL,
        [Amount] decimal(18,2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WEB_ExtraPayments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WEB_LoanApplications] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonId] int NULL,
        [SequenceNumber] int NULL,
        [ApplicationDate] date NULL,
        [IpAddress] nvarchar(50) NULL,
        [CreditLineId] int NULL,
        [InterestRate] decimal(10,5) NULL,
        [RequestedAmount] decimal(18,2) NULL,
        [TermMonths] int NULL,
        [Periodicity] nvarchar(2) NULL,
        [InstallmentAmount] decimal(18,2) NULL,
        [Purpose] nvarchar(300) NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WEB_LoanApplications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WEB_Services] (
        [Id] int NOT NULL IDENTITY,
        [ServiceNumber] int NULL,
        [ServiceType] nvarchar(2) NULL,
        [IdentificationNumber] nvarchar(20) NULL,
        [CallDate] date NULL,
        [AppointmentDate] date NULL,
        [Observations] nvarchar(max) NULL,
        [IsReviewed] bit NULL,
        [ReviewedBy] nvarchar(50) NULL,
        [Status] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_WEB_Services] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_AccountSubgroups] (
        [Id] int NOT NULL IDENTITY,
        [GroupId] int NULL,
        [SubgroupNumber] int NOT NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [ReportOrder] int NULL,
        [Level] int NULL,
        [ParentSubgroupId] int NULL,
        [FinancialStatementCode] nvarchar(5) NULL,
        [SpecialCode] nvarchar(1) NULL,
        [GroupId1] int NULL,
        [ParentSubgroupId1] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_AccountSubgroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_AccountSubgroups_ACC_AccountGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[ACC_AccountGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_AccountSubgroups_ACC_AccountGroups_GroupId1] FOREIGN KEY ([GroupId1]) REFERENCES [dbo].[ACC_AccountGroups] ([Id]),
        CONSTRAINT [FK_ACC_AccountSubgroups_ACC_AccountSubgroups_ParentSubgroupId] FOREIGN KEY ([ParentSubgroupId]) REFERENCES [dbo].[ACC_AccountSubgroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_AccountSubgroups_ACC_AccountSubgroups_ParentSubgroupId1] FOREIGN KEY ([ParentSubgroupId1]) REFERENCES [dbo].[ACC_AccountSubgroups] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_Documents] (
        [Id] bigint NOT NULL IDENTITY,
        [LegacyCompronte] nvarchar(5) NULL,
        [LegacyNumero] bigint NULL,
        [VoucherTypeCode] nvarchar(10) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [Detail] nvarchar(200) NULL,
        [TotalDebit] decimal(18,2) NOT NULL,
        [TotalCredit] decimal(18,2) NOT NULL,
        [DocumentDate] date NOT NULL,
        [IsClosed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsVoided] bit NOT NULL DEFAULT CAST(0 AS bit),
        [BeneficiaryId] int NULL,
        [PeriodCode] int NULL,
        [CheckNumber] nvarchar(10) NULL,
        [BankId] smallint NULL,
        [ModuleCode] nvarchar(5) NULL,
        [PaymentMethod] nvarchar(2) NULL,
        [InvoiceNumber] bigint NULL,
        [VoucherTypeId] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeCode] FOREIGN KEY ([VoucherTypeCode]) REFERENCES [dbo].[ACC_VoucherTypes] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeId] FOREIGN KEY ([VoucherTypeId]) REFERENCES [dbo].[ACC_VoucherTypes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Branches] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(300) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(200) NULL,
        [IsHeadquarters] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_Branches_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Subscriptions] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [PlanName] nvarchar(100) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [Currency] nvarchar(5) NOT NULL DEFAULT N'COP',
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [LastPaymentDate] date NULL,
        [NextBillingDate] date NULL,
        [PaymentMethod] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Subscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_Subscriptions_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_TenantSettings] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [SettingKey] nvarchar(200) NOT NULL,
        [SettingValue] nvarchar(max) NULL,
        [ValueType] nvarchar(30) NOT NULL DEFAULT N'String',
        [Description] nvarchar(500) NULL,
        [ModulePrefix] nvarchar(5) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_TenantSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_TenantSettings_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_PasswordPolicies] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NULL,
        [MinLength] int NOT NULL DEFAULT 12,
        [RequireUppercase] bit NOT NULL,
        [RequireLowercase] bit NOT NULL,
        [RequireDigit] bit NOT NULL,
        [RequireSymbol] bit NOT NULL,
        [ExpiryDays] int NOT NULL DEFAULT 90,
        [HistorySize] int NOT NULL DEFAULT 5,
        [LockoutThreshold] int NOT NULL DEFAULT 5,
        [LockoutMinutes] int NOT NULL DEFAULT 15,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_PasswordPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_PasswordPolicies_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_Roles] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [TenantId] int NULL,
        [IsBuiltIn] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsAssignable] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsSystemRole] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_Roles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_Roles_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_AssociateReferences] (
        [Id] int NOT NULL IDENTITY,
        [CertificateId] int NOT NULL,
        [PersonId] int NOT NULL,
        [CreditLineId] int NOT NULL,
        [RelationshipType] nvarchar(5) NOT NULL,
        [ReferenceName] nvarchar(100) NOT NULL,
        [ReferenceAddress] nvarchar(100) NULL,
        [CityId] int NULL,
        [ReferencePhone] nvarchar(30) NULL,
        [ReferenceMobile] nvarchar(30) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_AssociateReferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CDT_AssociateReferences_CDT_Certificates_CertificateId] FOREIGN KEY ([CertificateId]) REFERENCES [dbo].[CDT_Certificates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CDT_CertificateEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [CertificateId] int NOT NULL,
        [PersonId] int NOT NULL,
        [CreditLineId] int NOT NULL,
        [EntryDate] date NOT NULL,
        [OpeningDate] date NULL,
        [EntryType] nvarchar(5) NOT NULL,
        [PreviousRate] decimal(12,3) NOT NULL,
        [CurrentRate] decimal(12,3) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Description] nvarchar(200) NULL,
        [CertificateId1] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CDT_CertificateEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CDT_CertificateEntries_CDT_Certificates_CertificateId] FOREIGN KEY ([CertificateId]) REFERENCES [dbo].[CDT_Certificates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CDT_CertificateEntries_CDT_Certificates_CertificateId1] FOREIGN KEY ([CertificateId1]) REFERENCES [dbo].[CDT_Certificates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CMP_HabeasDataConsents] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [PersonId] int NOT NULL,
        [PolicyVersionId] int NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [ActionAt] datetime2 NOT NULL,
        [ActionBy] nvarchar(100) NOT NULL,
        [Channel] nvarchar(50) NULL,
        [Notes] nvarchar(2000) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CMP_HabeasDataConsents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CMP_HabeasDataConsents_CMP_HabeasDataPolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [dbo].[CMP_HabeasDataPolicyVersions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_BankReconciliationMasters] (
        [Id] int NOT NULL IDENTITY,
        [AccountId] int NOT NULL,
        [BankId] int NOT NULL,
        [PeriodCode] nvarchar(10) NOT NULL,
        [InitialBalance] decimal(18,2) NOT NULL,
        [FinalBalance] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [IsClosed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_BankReconciliationMasters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_BankReconciliationMasters_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_BankReconciliationMasters_COR_Banks_BankId] FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_BankReconciliations] (
        [Id] bigint NOT NULL IDENTITY,
        [AccountId] int NOT NULL,
        [BankId] int NOT NULL,
        [PeriodCode] int NULL,
        [TransactionDate] date NOT NULL,
        [DocumentType] nvarchar(5) NULL,
        [DocumentNumber] nvarchar(20) NULL,
        [Description] nvarchar(200) NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [IsReconciled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ReconciliationDate] date NULL,
        [Status] int NOT NULL,
        [IsAdditional] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsClosed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [MovementSequence] int NULL,
        [ModuleCode] nvarchar(5) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_BankReconciliations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_BankReconciliations_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_BankReconciliations_COR_Banks_BankId] FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_CulturalActivities] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [CommitteeId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_CulturalActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_CulturalActivities_COR_Committees_CommitteeId] FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_RecreationalEvents] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(20) NULL,
        [Description] nvarchar(200) NOT NULL,
        [ActivityType] nvarchar(2) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [Percentage] decimal(7,2) NOT NULL DEFAULT 0.0,
        [Amount] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CommitteeId] int NULL,
        [ActivitySubtype] nvarchar(60) NULL,
        [Capacity] int NULL,
        [ControlNovelty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ActivityProgramId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_RecreationalEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_RecreationalEvents_COR_ActivityPrograms_ActivityProgramId] FOREIGN KEY ([ActivityProgramId]) REFERENCES [dbo].[COR_ActivityPrograms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_RecreationalEvents_COR_Committees_CommitteeId] FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Sports] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(80) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [CommitteeId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Sports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Sports_COR_Committees_CommitteeId] FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_AccountBalances] (
        [Id] bigint NOT NULL IDENTITY,
        [AccountId] int NOT NULL,
        [PeriodYear] int NOT NULL,
        [PeriodMonth] tinyint NOT NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ACC_AccountBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_AccountBalances_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_AccountBalances_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_AccountBalances_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_Amortizations] (
        [Id] bigint NOT NULL IDENTITY,
        [PeriodYear] int NOT NULL,
        [AccountId] int NOT NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [PersonId] int NULL,
        [DocumentCode] nvarchar(10) NULL,
        [CrossAccountId] int NULL,
        [CrossCostCenterCode] nvarchar(10) NULL,
        [CrossPersonTaxId] nvarchar(20) NULL,
        [CrossInitialBalance] decimal(18,2) NOT NULL,
        [MovementAccountId] int NULL,
        [MovementCostCenterCode] nvarchar(10) NULL,
        [MovementPersonTaxId] nvarchar(20) NULL,
        [MovementInitialBalance] decimal(18,2) NOT NULL,
        [AmortizationDate] date NULL,
        [OriginalAmount] decimal(18,2) NOT NULL,
        [MonthlyAmount] decimal(18,2) NOT NULL,
        [TermMonths] int NULL,
        [RemainingBalance] decimal(18,2) NOT NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [LastPeriod] int NULL,
        [Rate] decimal(5,2) NOT NULL,
        [Status] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_Amortizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_Amortizations_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Amortizations_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Amortizations_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_Budgets] (
        [Id] int NOT NULL IDENTITY,
        [PeriodYear] int NOT NULL,
        [AccountId] int NOT NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [JanBudget] decimal(18,2) NULL,
        [FebBudget] decimal(18,2) NULL,
        [MarBudget] decimal(18,2) NULL,
        [AprBudget] decimal(18,2) NULL,
        [MayBudget] decimal(18,2) NULL,
        [JunBudget] decimal(18,2) NULL,
        [JulBudget] decimal(18,2) NULL,
        [AugBudget] decimal(18,2) NULL,
        [SepBudget] decimal(18,2) NULL,
        [OctBudget] decimal(18,2) NULL,
        [NovBudget] decimal(18,2) NULL,
        [DecBudget] decimal(18,2) NULL,
        [TotalBudget] decimal(18,2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_Budgets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_Budgets_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Budgets_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Budgets_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_Depreciations] (
        [Id] bigint NOT NULL IDENTITY,
        [PeriodYear] int NOT NULL,
        [AccountId] int NOT NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [PersonId] int NULL,
        [CrossAccountId] int NULL,
        [CrossCostCenterCode] nvarchar(10) NULL,
        [CrossPersonTaxId] nvarchar(20) NULL,
        [CrossInitialBalance] decimal(18,2) NOT NULL,
        [MovementAccountId] int NULL,
        [MovementCostCenterCode] nvarchar(10) NULL,
        [MovementPersonTaxId] nvarchar(20) NULL,
        [MovementInitialBalance] decimal(18,2) NOT NULL,
        [OriginalValue] decimal(18,2) NOT NULL,
        [DepreciationRate] decimal(5,2) NOT NULL,
        [MonthlyDepreciation] decimal(18,2) NOT NULL,
        [AccumulatedDepreciation] decimal(18,2) NOT NULL,
        [NetValue] decimal(18,2) NOT NULL,
        [UsefulLifeMonths] int NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [LastPeriod] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_Depreciations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_Depreciations_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Depreciations_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_Depreciations_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Departments] (
        [Id] int NOT NULL IDENTITY,
        [CountryId] int NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Departments_COR_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [dbo].[COR_Countries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Courses] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [Name] nvarchar(120) NOT NULL,
        [ShortName] nvarchar(40) NULL,
        [Duration] int NOT NULL DEFAULT 0,
        [TeachingEntityId] int NULL,
        [EducationType] int NOT NULL DEFAULT 0,
        [Percentage] decimal(7,2) NOT NULL DEFAULT 0.0,
        [Amount] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CommitteeId] int NULL,
        [ActivityProgramId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Courses_COR_ActivityPrograms_ActivityProgramId] FOREIGN KEY ([ActivityProgramId]) REFERENCES [dbo].[COR_ActivityPrograms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Courses_COR_Committees_CommitteeId] FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Courses_COR_Entities_TeachingEntityId] FOREIGN KEY ([TeachingEntityId]) REFERENCES [dbo].[COR_Entities] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Notifications] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [RecipientUserPublicId] uniqueidentifier NOT NULL,
        [Type] nvarchar(80) NOT NULL,
        [Subject] nvarchar(500) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [ChannelsMask] int NOT NULL,
        [EmailStatus] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [EmailSentAt] datetime2 NULL,
        [EmailAttemptCount] int NOT NULL,
        [ReadAt] datetime2 NULL,
        [ArchivedAt] datetime2 NULL,
        [NotificationTemplateId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Notifications_COR_NotificationTemplates_NotificationTemplateId] FOREIGN KEY ([NotificationTemplateId]) REFERENCES [dbo].[COR_NotificationTemplates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DEB_Transactions] (
        [Id] bigint NOT NULL IDENTITY,
        [CardId] int NULL,
        [SequenceCode] nvarchar(30) NOT NULL,
        [CardNumber] nvarchar(25) NOT NULL,
        [TransactionDate] datetime2 NULL,
        [Amount] decimal(18,2) NULL,
        [TransactionType] nvarchar(5) NULL,
        [CausalCode] nvarchar(5) NULL,
        [Status] nvarchar(2) NULL,
        [SourceSystem] nvarchar(20) NULL,
        [TransactionTime] nvarchar(10) NULL,
        [NetworkCode] nvarchar(10) NULL,
        [MessageCode] int NULL,
        [VatAmount] decimal(18,2) NULL,
        [VatBase] decimal(18,2) NULL,
        [CommissionAmount] decimal(18,2) NULL,
        [MethodCode] int NULL,
        [ErrorCode] nvarchar(5) NULL,
        [MerchantCode] nvarchar(20) NULL,
        [AuthorizationCode] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_DEB_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DEB_Transactions_DEB_Cards_CardId] FOREIGN KEY ([CardId]) REFERENCES [dbo].[DEB_Cards] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Warehouses] (
        [Id] int NOT NULL IDENTITY,
        [WarehouseCode] int NOT NULL,
        [LocationId] int NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [ShortDescription] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Warehouses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Warehouses_INV_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [dbo].[INV_Locations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_SecondaryGroups] (
        [Id] int NOT NULL IDENTITY,
        [GroupCode] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NULL,
        [PrimaryGroupId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_SecondaryGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_SecondaryGroups_INV_PrimaryGroups_PrimaryGroupId] FOREIGN KEY ([PrimaryGroupId]) REFERENCES [dbo].[INV_PrimaryGroups] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Documents] (
        [Id] bigint NOT NULL IDENTITY,
        [TransactionTypeId] int NOT NULL,
        [SequenceNumber] decimal(18,0) NOT NULL,
        [CustomerId] int NULL,
        [EntryDate] datetime2 NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [UserId] nvarchar(20) NULL,
        [PaymentClassId] int NOT NULL,
        [CashAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [DebitCardAmount] decimal(18,2) NOT NULL,
        [CreditCardAmount] decimal(18,2) NOT NULL,
        [CheckAmount] decimal(18,2) NOT NULL,
        [BankId] nvarchar(10) NULL,
        [ItemCount] int NOT NULL,
        [BankAccountNumber] nvarchar(20) NULL,
        [SalesPointId] int NULL,
        [ShiftId] int NULL,
        [AuditAmount] decimal(18,2) NOT NULL,
        [Detail] nvarchar(100) NULL,
        [InvoiceNumber] bigint NULL,
        [DueDate] date NULL,
        [SalesPersonId] int NULL,
        [ReturnTypeId] int NULL,
        [ReturnSequence] decimal(18,0) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Documents_INV_TransactionTypes_TransactionTypeId] FOREIGN KEY ([TransactionTypeId]) REFERENCES [dbo].[INV_TransactionTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_OrderDocuments] (
        [Id] bigint NOT NULL IDENTITY,
        [TransactionTypeId] int NOT NULL,
        [SequenceNumber] decimal(18,0) NOT NULL,
        [CustomerId] int NULL,
        [EntryDate] datetime2 NOT NULL,
        [TotalAmount] decimal(18,3) NOT NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [SubTotalAmount] decimal(18,3) NOT NULL,
        [Status] nvarchar(5) NULL,
        [UserId] nvarchar(20) NULL,
        [PaymentClassId] int NOT NULL,
        [CashAmount] decimal(18,3) NOT NULL,
        [CreditAmount] decimal(18,3) NOT NULL,
        [DebitCardAmount] decimal(18,3) NOT NULL,
        [CreditCardAmount] decimal(18,3) NOT NULL,
        [CheckAmount] decimal(18,3) NOT NULL,
        [BankId] nvarchar(10) NULL,
        [ItemCount] int NOT NULL,
        [BankAccountNumber] nvarchar(20) NULL,
        [SalesPointId] int NULL,
        [ShiftId] int NULL,
        [AuditAmount] decimal(18,3) NOT NULL,
        [Detail] nvarchar(300) NULL,
        [InvoiceNumber] bigint NULL,
        [ChangeAmount] decimal(17,0) NOT NULL,
        [Periodicity] int NULL,
        [Term] int NULL,
        [DeductionType] int NULL,
        [DiscountDate] datetime2 NULL,
        [InstallmentAmount] decimal(17,2) NOT NULL,
        [InterestRate] decimal(18,0) NOT NULL,
        [DueDate] date NULL,
        [WithholdingAmount] decimal(15,2) NOT NULL,
        [IcaAmount] decimal(15,2) NOT NULL,
        [SalesPersonId] int NULL,
        [ReturnTypeId] int NULL,
        [ReturnSequence] decimal(18,0) NULL,
        [TransferTypeId] int NULL,
        [TransferSequence] decimal(18,0) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_OrderDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_OrderDocuments_INV_TransactionTypes_TransactionTypeId] FOREIGN KEY ([TransactionTypeId]) REFERENCES [dbo].[INV_TransactionTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_AccrualEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccrualPeriod] int NOT NULL,
        [EntryType] nvarchar(2) NOT NULL,
        [Reason] nvarchar(2) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [Installments] int NOT NULL,
        [AffectsCapital] nvarchar(2) NOT NULL,
        [AffectsInterest] nvarchar(2) NOT NULL,
        [AffectsExtras] nvarchar(2) NOT NULL,
        [FixedInstallments] nvarchar(2) NOT NULL,
        [Authorization] nvarchar(20) NOT NULL,
        [EntryDate] date NOT NULL,
        [Remarks] nvarchar(max) NULL,
        [UserId] nvarchar(20) NOT NULL,
        [SystemDate] date NOT NULL,
        [Status] nvarchar(2) NULL,
        [ExpirationDate] date NOT NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [AppliesExtras] nvarchar(2) NOT NULL,
        [AppliesToSavings] nvarchar(2) NULL,
        [AppliesToServices] nvarchar(2) NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_AccrualEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_AccrualEntries_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ApplicationStatuses] (
        [Id] int NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [HasApplication] nvarchar(2) NOT NULL,
        [HasPromissory] nvarchar(2) NOT NULL,
        [HasPayrollDeduction] nvarchar(2) NOT NULL,
        [HasOtherDocs] nvarchar(2) NOT NULL,
        [ApplicationComplete] nvarchar(2) NOT NULL,
        [DataUpdated] nvarchar(2) NOT NULL,
        [ObligationsOk] nvarchar(2) NOT NULL,
        [PaymentCapacity] decimal(18,2) NOT NULL,
        [IncomeProof] nvarchar(2) NOT NULL,
        [CifinScore] decimal(10,6) NOT NULL,
        [IndebtednessLevel] decimal(15,6) NOT NULL,
        [AssetQuality] nvarchar(2) NULL,
        [ContingencyLevel] decimal(15,6) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [Evaluation] nvarchar(max) NOT NULL,
        [ReferenceVerification] nvarchar(max) NOT NULL,
        [UserId] nvarchar(20) NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [SystemDate] date NOT NULL,
        [ClaEne1] nvarchar(2) NULL,
        [ClaEne2] nvarchar(2) NULL,
        [ClaEne3] nvarchar(2) NULL,
        [ClaEne4] nvarchar(2) NULL,
        [ClaEne5] nvarchar(2) NULL,
        [ClaFeb1] nvarchar(2) NULL,
        [ClaFeb2] nvarchar(2) NULL,
        [ClaFeb3] nvarchar(2) NULL,
        [ClaFeb4] nvarchar(2) NULL,
        [ClaFeb5] nvarchar(2) NULL,
        [ClaMar1] nvarchar(2) NULL,
        [ClaMar2] nvarchar(2) NULL,
        [ClaMar3] nvarchar(2) NULL,
        [ClaMar4] nvarchar(2) NULL,
        [ClaMar5] nvarchar(2) NULL,
        [ClaAbr1] nvarchar(2) NULL,
        [ClaAbr2] nvarchar(2) NULL,
        [ClaAbr3] nvarchar(2) NULL,
        [ClaAbr4] nvarchar(2) NULL,
        [ClaAbr5] nvarchar(2) NULL,
        [ClaMay1] nvarchar(2) NULL,
        [ClaMay2] nvarchar(2) NULL,
        [ClaMay3] nvarchar(2) NULL,
        [ClaMay4] nvarchar(2) NULL,
        [ClaMay5] nvarchar(2) NULL,
        [ClaJun1] nvarchar(2) NULL,
        [ClaJun2] nvarchar(2) NULL,
        [ClaJun3] nvarchar(2) NULL,
        [ClaJun4] nvarchar(2) NULL,
        [ClaJun5] nvarchar(2) NULL,
        [ClaJul1] nvarchar(2) NULL,
        [ClaJul2] nvarchar(2) NULL,
        [ClaJul3] nvarchar(2) NULL,
        [ClaJul4] nvarchar(2) NULL,
        [ClaJul5] nvarchar(2) NULL,
        [ClaAgo1] nvarchar(2) NULL,
        [ClaAgo2] nvarchar(2) NULL,
        [ClaAgo3] nvarchar(2) NULL,
        [ClaAgo4] nvarchar(2) NULL,
        [ClaAgo5] nvarchar(2) NULL,
        [ClaSep1] nvarchar(2) NULL,
        [ClaSep2] nvarchar(2) NULL,
        [ClaSep3] nvarchar(2) NULL,
        [ClaSep4] nvarchar(2) NULL,
        [ClaSep5] nvarchar(2) NULL,
        [ClaOct1] nvarchar(2) NULL,
        [ClaOct2] nvarchar(2) NULL,
        [ClaOct3] nvarchar(2) NULL,
        [ClaOct4] nvarchar(2) NULL,
        [ClaOct5] nvarchar(2) NULL,
        [ClaNov1] nvarchar(2) NULL,
        [ClaNov2] nvarchar(2) NULL,
        [ClaNov3] nvarchar(2) NULL,
        [ClaNov4] nvarchar(2) NULL,
        [ClaNov5] nvarchar(2) NULL,
        [ClaDic1] nvarchar(2) NULL,
        [ClaDic2] nvarchar(2) NULL,
        [ClaDic3] nvarchar(2) NULL,
        [ClaDic4] nvarchar(2) NULL,
        [ClaDic5] nvarchar(2) NULL,
        [StudyDate] datetime2 NOT NULL,
        [PayrollPayment] decimal(18,2) NOT NULL,
        [CashPayment] decimal(18,2) NOT NULL,
        [AssetHousing] decimal(18,2) NOT NULL,
        [AssetVehicle] decimal(18,2) NOT NULL,
        [AssetContributions] decimal(18,2) NOT NULL,
        [AssetOther] decimal(18,2) NOT NULL,
        [TotalAssets] decimal(18,2) NOT NULL,
        [LiabilityDebts] decimal(18,2) NOT NULL,
        [LiabilityOther] decimal(18,2) NOT NULL,
        [TotalLiabilities] decimal(18,2) NOT NULL,
        [Equity] decimal(18,2) NOT NULL,
        [TotalLiabilitiesEquity] decimal(18,2) NOT NULL,
        [PersonalExpenses] decimal(18,2) NOT NULL,
        [DebtConsolidation] nvarchar(2) NOT NULL,
        [AccountNumber] int NOT NULL,
        [AverageSalary] decimal(18,2) NOT NULL,
        [AssetCashBank] decimal(18,2) NOT NULL,
        [AssetReceivables] decimal(18,2) NOT NULL,
        [LiabilityBankLoans] decimal(18,2) NOT NULL,
        [LiabilityMortgage] decimal(18,2) NOT NULL,
        [PercentageFlag] nvarchar(2) NOT NULL,
        [AssetSavings] decimal(18,2) NOT NULL,
        [DatacreditoScore] decimal(10,6) NOT NULL,
        [CreditLimit] decimal(18,2) NULL,
        [DiscoveredAmount] decimal(18,2) NOT NULL,
        [DatacreditoRating] nvarchar(3) NOT NULL,
        [ExternalDebtInstallment] decimal(18,2) NOT NULL,
        [ExternalDebtBalance] decimal(18,2) NOT NULL,
        [DatacreditoOverdue] decimal(18,2) NOT NULL,
        [InsuranceExtra] decimal(18,6) NOT NULL,
        [CapitalAtRisk] decimal(15,3) NOT NULL,
        [PaymasterPercentage] decimal(15,2) NOT NULL,
        [PaymasterDeductionType] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ApplicationStatuses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_ApplicationStatuses_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionDateEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [CollectionCaseId] bigint NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [EntryDate] date NOT NULL,
        [PromiseDate] date NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionDateEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_CollectionDateEntries_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionMasters] (
        [Id] int NOT NULL IDENTITY,
        [Period] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] int NOT NULL,
        [UserId] nvarchar(20) NULL,
        [ManagementDate] date NOT NULL,
        [PaymentDate] date NOT NULL,
        [TotalBalance] decimal(18,2) NOT NULL,
        [OverdueCapital] decimal(18,2) NOT NULL,
        [OverdueInterest] decimal(18,2) NOT NULL,
        [OverdueInsurance] decimal(18,2) NOT NULL,
        [OverdueAdmin] decimal(18,2) NOT NULL,
        [AccumulatedDefault] decimal(18,2) NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [DaysOverdue] decimal(18,2) NOT NULL,
        [CollectionCaseId] bigint NOT NULL,
        [OverdueExtras] decimal(18,2) NOT NULL,
        [OverdueOther] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionMasters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_CollectionMasters_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CollectionNoticeDetails] (
        [Id] bigint NOT NULL IDENTITY,
        [NoticeNumber] nvarchar(3) NOT NULL,
        [Period] int NOT NULL,
        [ConceptClass] nvarchar(2) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [Cycle] int NOT NULL,
        [CapitalBalance] decimal(18,2) NOT NULL,
        [ExtraBalance] decimal(18,2) NOT NULL,
        [InterestBalance] decimal(18,2) NOT NULL,
        [DefaultBalance] decimal(18,2) NOT NULL,
        [InsuranceBalance] decimal(18,2) NOT NULL,
        [AdminBalance] decimal(18,2) NOT NULL,
        [OtherBalance] decimal(18,2) NOT NULL,
        [DaysOverdue] int NOT NULL,
        [TotalBalance] decimal(18,2) NOT NULL,
        [MaturityDate] datetime2 NOT NULL,
        [Codeudor1] nvarchar(20) NOT NULL,
        [Phone1] nvarchar(20) NOT NULL,
        [Address1] nvarchar(80) NOT NULL,
        [City1] int NOT NULL,
        [Codeudor2] nvarchar(20) NOT NULL,
        [Phone2] nvarchar(20) NOT NULL,
        [Address2] nvarchar(80) NOT NULL,
        [City2] int NOT NULL,
        [Codeudor3] nvarchar(20) NOT NULL,
        [Phone3] nvarchar(20) NOT NULL,
        [Address3] nvarchar(80) NOT NULL,
        [City3] int NOT NULL,
        [Codeudor4] nvarchar(20) NOT NULL,
        [Phone4] nvarchar(20) NOT NULL,
        [Address4] nvarchar(80) NOT NULL,
        [City4] int NOT NULL,
        [OnlyDefaulted] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CollectionNoticeDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_CollectionNoticeDetails_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CreditLineAudit] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(2) NOT NULL,
        [CreditLineId] int NULL,
        [Description_Old] nvarchar(40) NULL,
        [Description_New] nvarchar(40) NULL,
        [AllowExtension_Old] nvarchar(2) NULL,
        [AllowExtension_New] nvarchar(2) NULL,
        [DefaultInterest_Old] nvarchar(2) NULL,
        [DefaultInterest_New] nvarchar(2) NULL,
        [InterestRate_Old] decimal(10,5) NULL,
        [InterestRate_New] decimal(10,5) NULL,
        [InterestType_Old] nvarchar(2) NULL,
        [InterestType_New] nvarchar(2) NULL,
        [MaxTerm_Old] int NULL,
        [MaxTerm_New] int NULL,
        [CreditLimit_Old] decimal(18,2) NULL,
        [CreditLimit_New] decimal(18,2) NULL,
        [ExtraRate_Old] decimal(18,2) NULL,
        [ExtraRate_New] decimal(18,2) NULL,
        [FinancialInterest_Old] nvarchar(2) NULL,
        [FinancialInterest_New] nvarchar(2) NULL,
        [GuaranteeClass_Old] nvarchar(2) NULL,
        [GuaranteeClass_New] nvarchar(2) NULL,
        [MaxAmount_Old] decimal(18,2) NULL,
        [MaxAmount_New] decimal(18,2) NULL,
        [AffectsFlag_Old] nvarchar(2) NULL,
        [AffectsFlag_New] nvarchar(2) NULL,
        [AccrualRate_Old] decimal(18,2) NULL,
        [AccrualRate_New] decimal(18,2) NULL,
        [CapitalForm_Old] nvarchar(2) NULL,
        [CapitalForm_New] nvarchar(2) NULL,
        [AdminRate_Old] decimal(18,2) NULL,
        [AdminRate_New] decimal(18,2) NULL,
        [InstallmentType_Old] nvarchar(2) NULL,
        [InstallmentType_New] nvarchar(2) NULL,
        [DebitCreditFlag_Old] nvarchar(2) NULL,
        [DebitCreditFlag_New] nvarchar(2) NULL,
        [InsuranceRate_Old] decimal(18,2) NULL,
        [InsuranceRate_New] decimal(18,2) NULL,
        [MinContribution_Old] decimal(18,2) NULL,
        [MinContribution_New] decimal(18,2) NULL,
        [AccountCode_Old] nvarchar(15) NULL,
        [AccountCode_New] nvarchar(15) NULL,
        [GracePeriod_Old] nvarchar(2) NULL,
        [GracePeriod_New] nvarchar(2) NULL,
        [AdminMin_Old] decimal(18,2) NULL,
        [AdminMin_New] decimal(18,2) NULL,
        [AdminMax_Old] decimal(18,2) NULL,
        [AdminMax_New] decimal(18,2) NULL,
        [ShortName_Old] nvarchar(30) NULL,
        [ShortName_New] nvarchar(30) NULL,
        [TaxRate_Old] decimal(18,2) NULL,
        [TaxRate_New] decimal(18,2) NULL,
        [InsMin_Old] decimal(18,2) NULL,
        [InsMin_New] decimal(18,2) NULL,
        [InsMax_Old] decimal(18,2) NULL,
        [InsMax_New] decimal(18,2) NULL,
        [UserId_Old] nvarchar(20) NULL,
        [UserId_New] nvarchar(20) NULL,
        [UserName_Old] nvarchar(50) NULL,
        [UserName_New] nvarchar(50) NULL,
        [SystemDate] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CreditLineAudit] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_CreditLineAudit_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_CreditParameters] (
        [Id] int NOT NULL IDENTITY,
        [BankCode] nvarchar(5) NOT NULL,
        [CreditLimit] decimal(10,0) NOT NULL,
        [AdvanceType] int NOT NULL,
        [AdvanceAmount] decimal(18,2) NOT NULL,
        [AdvanceRate] decimal(18,2) NOT NULL,
        [CreditLineId] int NOT NULL,
        [AdvanceCreditLineId] int NOT NULL,
        [DeductionClass] nvarchar(2) NOT NULL,
        [ExpirationDays] int NOT NULL,
        [CutoffDay] int NOT NULL,
        [Amount1] decimal(18,2) NULL,
        [Term1] int NULL,
        [Period1] int NULL,
        [Amount2] decimal(18,2) NULL,
        [Term2] int NULL,
        [Period2] int NULL,
        [Amount3] decimal(18,2) NULL,
        [Term3] int NULL,
        [Period3] int NULL,
        [Amount4] decimal(18,2) NULL,
        [Term4] int NULL,
        [Period4] int NULL,
        [Amount5] decimal(18,2) NULL,
        [Term5] int NULL,
        [Period5] int NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [TaxRate] decimal(10,5) NOT NULL,
        [Amount6] decimal(18,2) NOT NULL,
        [Term6] int NOT NULL,
        [Period6] int NOT NULL,
        [Amount7] decimal(18,2) NOT NULL,
        [Term7] int NOT NULL,
        [Period7] int NOT NULL,
        [AdvanceTerm1] int NOT NULL,
        [AdvanceTerm2] int NOT NULL,
        [AdvanceTerm3] int NOT NULL,
        [AdvanceTerm4] int NOT NULL,
        [AdvanceTerm5] int NOT NULL,
        [AdvanceTerm6] int NOT NULL,
        [AdvanceTerm7] int NOT NULL,
        [MaintenanceLineId] int NOT NULL,
        [MaintenanceAmount] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_CreditParameters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_CreditParameters_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DefaultLiquidations] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [LiquidationDate] datetime2 NOT NULL,
        [LiquidationBase] int NOT NULL,
        [LiquidationDays] int NOT NULL,
        [LiquidationAmount] int NOT NULL,
        [LastLiquidationDate] datetime2 NOT NULL,
        [CompanyCode] nvarchar(5) NOT NULL,
        [LiquidationRate] decimal(6,3) NOT NULL,
        [DeductionClass] int NOT NULL,
        [GraceDays] int NOT NULL,
        [UsuryRate] decimal(6,3) NOT NULL,
        [LiquidationType] int NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [AccrualPeriod] int NOT NULL,
        [UserId] nvarchar(20) NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DefaultLiquidations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_DefaultLiquidations_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ExtraPaymentBalances] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [ExtraNumber] int NOT NULL,
        [Period] int NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [OpeningBalance] decimal(18,2) NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ExtraPaymentBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_ExtraPaymentBalances_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_HousingParameters] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [HousingClass] int NOT NULL,
        [HousingType] int NOT NULL,
        [SocialInterest] nvarchar(2) NOT NULL,
        [HasSubsidy] nvarchar(2) NOT NULL,
        [NetworkEntity] int NOT NULL,
        [NetworkValue] bigint NOT NULL,
        [DisbursementType] int NOT NULL,
        [CurrencyType] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_HousingParameters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_HousingParameters_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_InsuranceBeneficiaries] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [BeneficiaryId] nvarchar(15) NOT NULL,
        [RelationshipCode] nvarchar(5) NOT NULL,
        [DocumentType] nvarchar(3) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [BirthDate] date NOT NULL,
        [InsurancePercentage] decimal(6,3) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [Address] nvarchar(60) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_InsuranceBeneficiaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_InsuranceBeneficiaries_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_InsurancePolicies] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [Balance] decimal(15,2) NOT NULL,
        [InsuranceAmount] decimal(15,2) NOT NULL,
        [InsuranceDate] date NOT NULL,
        [ExpirationDate] date NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [UserId] nvarchar(20) NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [IsHistorical] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_InsurancePolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_InsurancePolicies_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_InvoiceLineItems] (
        [Id] bigint NOT NULL IDENTITY,
        [InvoiceId] bigint NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccrualPeriod] int NOT NULL,
        [CapitalBalance] decimal(18,3) NULL,
        [ExtraBalance] decimal(18,3) NULL,
        [InterestBalance] decimal(18,3) NULL,
        [DefaultBalance] decimal(18,3) NULL,
        [InsuranceBalance] decimal(18,3) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_InvoiceLineItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_InvoiceLineItems_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_LoanApplications] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [ApplicationDate] date NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [RequestedAmount] decimal(18,2) NOT NULL,
        [InterestRate] decimal(7,6) NOT NULL,
        [Term] int NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [MonthlyPayments] decimal(18,2) NOT NULL,
        [OverdueBalance] decimal(18,2) NOT NULL,
        [AvailableCredit] decimal(18,2) NOT NULL,
        [CoopEntryDate] date NULL,
        [EmployerName] nvarchar(50) NOT NULL,
        [Position] nvarchar(60) NOT NULL,
        [Salary] decimal(18,2) NOT NULL,
        [OtherIncome] decimal(18,2) NOT NULL,
        [EmployerEntryDate] date NULL,
        [MonthlyDeductions] decimal(18,2) NOT NULL,
        [MonthlyFixedExpenses] decimal(18,2) NOT NULL,
        [AvailableMonthly] decimal(18,2) NOT NULL,
        [ContractType] nvarchar(2) NOT NULL,
        [GuaranteeType] nvarchar(3) NOT NULL,
        [GuaranteeDescription] nvarchar(max) NULL,
        [CommercialAppraisal] decimal(18,2) NOT NULL,
        [CadastralAppraisal] decimal(18,2) NOT NULL,
        [IsInsured] nvarchar(2) NOT NULL,
        [InsurancePercentage] decimal(18,2) NOT NULL,
        [InsuranceExpiryDate] date NULL,
        [Codeudor1] nvarchar(20) NOT NULL,
        [Codeudor2] nvarchar(20) NOT NULL,
        [Codeudor3] nvarchar(20) NOT NULL,
        [Codeudor4] nvarchar(20) NOT NULL,
        [ApprovedAmount] decimal(18,2) NOT NULL,
        [ApprovalDate] date NULL,
        [MinutesNumber] nvarchar(20) NOT NULL,
        [MinutesDate] date NULL,
        [ScheduledDate] date NULL,
        [AdditionalContribution] decimal(18,2) NOT NULL,
        [DebtConsolidation] decimal(18,2) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [UserId] nvarchar(20) NULL,
        [RecordDate] date NOT NULL,
        [SpouseWorks] nvarchar(2) NOT NULL,
        [SpouseName] nvarchar(50) NOT NULL,
        [SpouseEmployer] nvarchar(50) NOT NULL,
        [SpouseSalary] decimal(18,2) NOT NULL,
        [SpousePhone] nvarchar(20) NOT NULL,
        [SpouseEmployerAddress] nvarchar(50) NOT NULL,
        [SpouseEmployerCity] nvarchar(25) NOT NULL,
        [SpouseDependents] int NOT NULL,
        [Remarks] nvarchar(max) NULL,
        [VehicleDescription] nvarchar(25) NOT NULL,
        [HasVehicle] nvarchar(2) NOT NULL,
        [OwnsHouse] nvarchar(2) NOT NULL,
        [DisbursementDate] date NULL,
        [IdentificationNumber] nvarchar(20) NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [InstallmentType] nvarchar(2) NOT NULL,
        [InterestType] nvarchar(2) NOT NULL,
        [DeductionType] nvarchar(2) NOT NULL,
        [ClosingInterestType] nvarchar(2) NOT NULL,
        [CapitalizationType] nvarchar(2) NOT NULL,
        [AdminType] nvarchar(2) NOT NULL,
        [InsuranceType] nvarchar(3) NULL,
        [OtherType] nvarchar(2) NOT NULL,
        [AdminForm] nvarchar(3) NOT NULL,
        [AdminConceptCode] int NOT NULL,
        [InsuranceConceptCode] int NOT NULL,
        [OtherConceptCode] int NOT NULL,
        [AdminRate] decimal(12,5) NOT NULL,
        [InsuranceRate] decimal(10,5) NOT NULL,
        [ConceptRate] decimal(18,2) NOT NULL,
        [OtherRate] decimal(18,2) NOT NULL,
        [ExtraPaymentAmount] decimal(18,2) NOT NULL,
        [ContributionsAmount] decimal(18,2) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [ExtraPercentage] decimal(18,2) NOT NULL,
        [AdminInstallment] decimal(18,2) NOT NULL,
        [InsuranceInstallment] decimal(18,2) NOT NULL,
        [CapitalInstallment] decimal(18,2) NOT NULL,
        [InterestInstallment] decimal(18,2) NOT NULL,
        [OtherInstallment] decimal(18,2) NOT NULL,
        [GracePeriodFlag] nvarchar(2) NOT NULL,
        [GracePeriodStart] int NOT NULL,
        [GracePeriodStartDate] date NULL,
        [GracePeriodInstallment] nvarchar(2) NOT NULL,
        [GracePeriodDays] int NOT NULL,
        [GracePeriodEndDate] date NULL,
        [GracePeriodCycle] int NOT NULL,
        [ExtraInMonth] nvarchar(2) NOT NULL,
        [ExtraInAdvance] nvarchar(2) NOT NULL,
        [FirstPaymentFlag] nvarchar(2) NOT NULL,
        [SecondPaymentType] nvarchar(2) NOT NULL,
        [PromissoryNumber] int NOT NULL,
        [PayrollDeductionNumber] int NOT NULL,
        [CdatNumber] int NOT NULL,
        [VariableIncome] decimal(18,2) NOT NULL,
        [RentalIncome] decimal(18,2) NOT NULL,
        [ThirdPartyDebts] decimal(18,2) NOT NULL,
        [AuthorizedFlag] nvarchar(2) NOT NULL,
        [DiscountCompany] nvarchar(5) NOT NULL,
        [InsurerIdentification] nvarchar(20) NULL,
        [InsurerName] nvarchar(50) NULL,
        [PolicyNumber] nvarchar(20) NULL,
        [RegistrationNumber] nvarchar(25) NULL,
        [PensionIncome] decimal(18,2) NOT NULL,
        [PensionDeduction] decimal(18,2) NOT NULL,
        [ParafiscalDeduction] decimal(18,2) NOT NULL,
        [SentToPaymaster] nvarchar(2) NOT NULL,
        [SentToPaymasterDate] date NULL,
        [ReceivedFromPaymasterDate] date NOT NULL,
        [AuthorizingUser] nvarchar(20) NULL,
        [SolicitedInterestRate] decimal(18,2) NOT NULL,
        [SolicitedTerm] int NOT NULL,
        [SolicitedPeriodicity] nvarchar(2) NOT NULL,
        [SolicitedCycle] nvarchar(2) NOT NULL,
        [SolicitedDeductionType] nvarchar(2) NOT NULL,
        [EntryUserId] nvarchar(20) NOT NULL,
        [AuthorizingUserId] nvarchar(20) NOT NULL,
        [EmployerPayrollDeduction] decimal(18,2) NOT NULL,
        [DisbursedAmount] decimal(18,2) NOT NULL,
        [DtfRate] decimal(10,5) NOT NULL,
        [SpreadPoints] decimal(10,5) NOT NULL,
        [EntityType] nvarchar(3) NOT NULL,
        [SolicitedInstallment] decimal(18,2) NOT NULL,
        [PaymentCapacityPct] nvarchar(2) NOT NULL,
        [PaymentCapacityDebtPickup] nvarchar(3) NOT NULL,
        [SpouseIncome] decimal(18,2) NOT NULL,
        [PersonalExpenses] decimal(18,2) NOT NULL,
        [AssetHousing] decimal(18,2) NOT NULL,
        [AssetVehicle] decimal(18,2) NOT NULL,
        [AssetOther] decimal(18,2) NOT NULL,
        [AssetContributions] decimal(18,2) NOT NULL,
        [AssetCashBank] decimal(18,2) NOT NULL,
        [AssetReceivables] decimal(18,2) NOT NULL,
        [AssetSavings] decimal(18,2) NOT NULL,
        [TotalAssets] decimal(18,2) NOT NULL,
        [LiabilityDebts] decimal(18,2) NOT NULL,
        [LiabilityOther] decimal(18,2) NOT NULL,
        [LiabilityBankLoans] decimal(18,2) NOT NULL,
        [LiabilityMortgage] decimal(18,2) NOT NULL,
        [TotalLiabilities] decimal(18,2) NOT NULL,
        [Equity] decimal(18,2) NOT NULL,
        [TotalLiabilitiesEquity] decimal(18,2) NOT NULL,
        [PayrollCapacity] decimal(18,2) NOT NULL,
        [PayrollPercentage] decimal(18,2) NOT NULL,
        [PaymentCapacity] decimal(18,2) NOT NULL,
        [CashPercentage] decimal(18,2) NOT NULL,
        [PaymasterPercentage] decimal(18,2) NOT NULL,
        [PayrollLabel] decimal(18,2) NOT NULL,
        [CashLabel] decimal(18,2) NOT NULL,
        [DiscoveredAmount] decimal(18,2) NOT NULL,
        [IndebtednessLevel] decimal(18,2) NOT NULL,
        [ContingencyLevel] decimal(18,2) NOT NULL,
        [CapitalAtRisk] decimal(18,2) NOT NULL,
        [DeductionCapacity] decimal(18,2) NOT NULL,
        [DeductionPercentage] decimal(18,2) NOT NULL,
        [PaymasterDeductionType] nvarchar(2) NULL,
        [SolicitedDtfRate] decimal(18,2) NOT NULL,
        [SolicitedSpreadPoints] decimal(18,2) NOT NULL,
        [LegacyNumero] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_LoanApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_LoanApplications_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_LoanRestructurings] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [OriginalPersonCode] nvarchar(20) NOT NULL,
        [OriginalCreditLineId] int NOT NULL,
        [OriginalPortfolioNumber] bigint NOT NULL,
        [Category] nvarchar(2) NOT NULL,
        [RestructureDate] date NULL,
        [Amount] decimal(18,2) NULL,
        [SystemDate] date NULL,
        [UserId] nvarchar(20) NOT NULL,
        [TimesRestructured] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_LoanRestructurings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_LoanRestructurings_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_OnlineQueries] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [IdentificationNumber] nvarchar(20) NOT NULL,
        [ItemCode] int NOT NULL,
        [Description] nvarchar(60) NULL,
        [CreditLineId] int NOT NULL,
        [VoucherType] nvarchar(5) NOT NULL,
        [VoucherNumber] bigint NOT NULL,
        [QueryDate] datetime2 NULL,
        [QueryAmount] decimal(18,2) NOT NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [UpdateDate] datetime2 NOT NULL,
        [LegacyConsecutivo] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_OnlineQueries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_OnlineQueries_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PayrollDeductionConcepts] (
        [Id] int NOT NULL IDENTITY,
        [CompanyCode] nvarchar(5) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PayrollConceptCode] nvarchar(8) NOT NULL,
        [InterestConceptCode] nvarchar(8) NOT NULL,
        [ExtraConceptCode] nvarchar(8) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PayrollDeductionConcepts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_PayrollDeductionConcepts_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PayrollDeductions] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyCode] nvarchar(5) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [Period] int NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [IsAdditional] nvarchar(2) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [Description] nvarchar(50) NOT NULL,
        [PaymentCycle] nvarchar(2) NOT NULL,
        [ContributionAmount] decimal(18,2) NOT NULL,
        [LoanAmount] decimal(18,2) NOT NULL,
        [InterestAmount] decimal(18,2) NOT NULL,
        [ExtraAmount] decimal(18,2) NOT NULL,
        [DefaultAmount] decimal(18,2) NOT NULL,
        [InsuranceAmount] decimal(18,2) NOT NULL,
        [AdminAmount] decimal(18,2) NOT NULL,
        [OtherAmount] decimal(18,2) NOT NULL,
        [ContributionApplied] decimal(18,2) NOT NULL,
        [LoanApplied] decimal(18,2) NOT NULL,
        [InterestApplied] decimal(18,2) NOT NULL,
        [ExtraApplied] decimal(18,2) NOT NULL,
        [DefaultApplied] decimal(18,2) NOT NULL,
        [InsuranceApplied] decimal(18,2) NOT NULL,
        [AdminApplied] decimal(18,2) NOT NULL,
        [OtherApplied] decimal(18,2) NOT NULL,
        [CodeudorCode] nvarchar(20) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PayrollDeductions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_PayrollDeductions_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PortfolioBalances] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [Period] int NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [OpeningBalance] decimal(18,2) NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [OpenInstallments] int NOT NULL,
        [CifinStatus] nvarchar(3) NOT NULL,
        [InitialCategory] nvarchar(2) NOT NULL,
        [FinalCategory] nvarchar(2) NOT NULL,
        [InitialLastPaymentDate] datetime2 NULL,
        [FinalLastPaymentDate] datetime2 NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [InterestRate] decimal(10,6) NOT NULL,
        [PaymentCycle] nvarchar(2) NULL,
        [Periodicity] nvarchar(2) NULL,
        [DeductionClass] nvarchar(2) NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PortfolioBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_PortfolioBalances_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PortfolioInvoices] (
        [Id] bigint NOT NULL IDENTITY,
        [InvoiceNumber] decimal(20,0) NOT NULL,
        [Description] nvarchar(120) NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] decimal(18,2) NOT NULL,
        [Period] int NOT NULL,
        [VoucherType] nvarchar(5) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PortfolioInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_PortfolioInvoices_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PreviousInstallments] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccrualPeriod] int NOT NULL,
        [AccountingPeriod] int NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [ExtraAmount] decimal(18,2) NOT NULL,
        [AdvanceCapital] decimal(18,2) NOT NULL,
        [AdvanceInterest] decimal(18,2) NOT NULL,
        [AdvanceExtra] decimal(18,2) NOT NULL,
        [AdvanceInsurance] decimal(18,2) NOT NULL,
        [AdvanceAdmin] decimal(18,2) NOT NULL,
        [AdvanceOther] decimal(18,2) NOT NULL,
        [AdvanceDate] date NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [TransactionSequence] bigint NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PreviousInstallments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_PreviousInstallments_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_RatesByTerm] (
        [Id] int NOT NULL IDENTITY,
        [CreditLineId] int NOT NULL,
        [TermStart] int NOT NULL,
        [TermEnd] int NOT NULL,
        [DiscountType] nvarchar(2) NOT NULL,
        [RateValue] decimal(18,4) NOT NULL,
        [UpdateDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_RatesByTerm] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_RatesByTerm_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_RecreationApplications] (
        [Id] int NOT NULL IDENTITY,
        [ApplicationNumber] int NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [CreditNumber] int NOT NULL,
        [PaymentAmount] decimal(18,2) NOT NULL,
        [AdditionalInterest] decimal(18,2) NULL,
        [FullOrPartial] nvarchar(2) NOT NULL,
        [MaturityDate] date NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_RecreationApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_RecreationApplications_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_RiskAssessments] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] nvarchar(20) NOT NULL,
        [InstallmentNumber] int NOT NULL,
        [Period] nvarchar(8) NOT NULL,
        [InstallmentAmount] decimal(14,2) NOT NULL,
        [ExtraInstallment] decimal(18,2) NOT NULL,
        [InterestAmount] decimal(18,2) NOT NULL,
        [InsuranceAmount] decimal(18,2) NOT NULL,
        [AdminAmount] decimal(18,2) NOT NULL,
        [CapitalPayment] decimal(18,2) NOT NULL,
        [Balance] decimal(14,2) NOT NULL,
        [TotalBalance] decimal(14,2) NOT NULL,
        [PortfolioClass] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_RiskAssessments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_RiskAssessments_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ShortLongTermPortfolio] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [PersonName] nvarchar(60) NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [Balance] decimal(18,2) NULL,
        [Month1] decimal(18,2) NULL,
        [Month2] decimal(18,2) NULL,
        [Month3] decimal(18,2) NULL,
        [Month4] decimal(18,2) NULL,
        [Month5] decimal(18,2) NULL,
        [Month6] decimal(18,2) NULL,
        [Month7] decimal(18,2) NULL,
        [Month8] decimal(18,2) NULL,
        [Month9] decimal(18,2) NULL,
        [Month10] decimal(18,2) NULL,
        [Month11] decimal(18,2) NULL,
        [Month12] decimal(18,2) NULL,
        [MoreThan12] decimal(18,2) NULL,
        [Total] decimal(18,2) NULL,
        [Description] nvarchar(60) NULL,
        [AffectsFlag] nvarchar(20) NULL,
        [FogaClass] nvarchar(3) NULL,
        [CompanyName] nvarchar(60) NULL,
        [CostCenterName] nvarchar(60) NULL,
        [Period] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ShortLongTermPortfolio] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_ShortLongTermPortfolio_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_SiplaGroupParameters] (
        [Id] int NOT NULL IDENTITY,
        [ConceptCode] nvarchar(3) NOT NULL,
        [CompanyCode] nvarchar(5) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_SiplaGroupParameters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_SiplaGroupParameters_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_TermRates] (
        [Id] int NOT NULL IDENTITY,
        [CreditLineId] int NOT NULL,
        [AmountStart] decimal(18,2) NOT NULL,
        [AmountEnd] decimal(18,2) NOT NULL,
        [TermStart] int NOT NULL,
        [TermEnd] int NOT NULL,
        [SeniorityStart] int NOT NULL,
        [SeniorityEnd] int NOT NULL,
        [Rate] decimal(6,3) NOT NULL,
        [UpdateDate] datetime2 NOT NULL,
        [MaxTerm] int NOT NULL,
        [MaxAmount] decimal(18,2) NOT NULL,
        [GuaranteeType] nvarchar(3) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_TermRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_TermRates_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_ConceptAccounts] (
        [Id] int NOT NULL IDENTITY,
        [ConceptId] int NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [ExpenseAccountCode] nvarchar(15) NOT NULL,
        [CounterAccountCode] nvarchar(15) NOT NULL,
        [ProvisionAccountCode] nvarchar(15) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_ConceptAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_ConceptAccounts_PAY_PayrollConcepts_ConceptId] FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_RolePermissions] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_RolePermissions_SEC_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[SEC_Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SEC_RolePermissions_SEC_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Cities] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(10) NULL,
        [DepartmentId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Cities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Cities_COR_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[COR_Departments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_NotificationDeliveryFailures] (
        [Id] bigint NOT NULL IDENTITY,
        [NotificationId] bigint NOT NULL,
        [Channel] nvarchar(20) NOT NULL,
        [AttemptNumber] int NOT NULL,
        [ErrorMessage] nvarchar(2000) NOT NULL,
        [FailedAt] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_NotificationDeliveryFailures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_NotificationDeliveryFailures_COR_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [dbo].[COR_Notifications] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_ProductGroups] (
        [Id] int NOT NULL IDENTITY,
        [GroupCode] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ShortName] nvarchar(50) NULL,
        [SecondaryGroupId] int NULL,
        [RestrictsLimit] bit NOT NULL,
        [MaxSalesQuantity] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_ProductGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_ProductGroups_INV_SecondaryGroups_SecondaryGroupId] FOREIGN KEY ([SecondaryGroupId]) REFERENCES [dbo].[INV_SecondaryGroups] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_People] (
        [Id] int NOT NULL IDENTITY,
        [LegacyCode] nvarchar(20) NULL,
        [LastName] nvarchar(150) NOT NULL,
        [FirstName] nvarchar(150) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [TaxIdCheckDigit] nvarchar(2) NULL,
        [IdIssuedAt] nvarchar(40) NULL,
        [IdType] nvarchar(2) NOT NULL,
        [IdIssueDate] date NULL,
        [PersonType] nvarchar(2) NULL,
        [BusinessName] nvarchar(150) NULL,
        [PreviousCode] nvarchar(20) NULL,
        [NaturalLegalType] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [Address] nvarchar(120) NULL,
        [Phone1] nvarchar(40) NULL,
        [Phone2] nvarchar(40) NULL,
        [Fax] nvarchar(30) NULL,
        [Mobile] nvarchar(30) NULL,
        [Email] nvarchar(120) NULL,
        [CityId] int NULL,
        [MailingAddress] nvarchar(120) NULL,
        [MailingPreference] nvarchar(2) NULL,
        [MailingCityId] int NULL,
        [EmailType] nvarchar(2) NULL,
        [DaneCityCode] nvarchar(20) NULL,
        [Gender] nvarchar(2) NULL,
        [MaritalStatus] nvarchar(2) NULL,
        [DateOfBirth] date NULL,
        [EducationLevel] nvarchar(2) NULL,
        [SocialStratum] nvarchar(4) NULL,
        [HousingType] nvarchar(2) NULL,
        [HasVehicle] bit NOT NULL DEFAULT CAST(0 AS bit),
        [VehicleType] int NOT NULL DEFAULT 0,
        [IsHeadOfHousehold] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WorkShift] nvarchar(2) NULL,
        [WithholdingExempt] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IcaWithholdingExempt] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TaxRegime] nvarchar(2) NULL,
        [IcaType] nvarchar(6) NULL,
        [IsLargeContributor] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IcaRate] decimal(10,5) NULL,
        [DataOrigin] nvarchar(6) NULL,
        [PaymentDays] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [HasTaxLien] bit NOT NULL DEFAULT CAST(0 AS bit),
        [HasSpecialPrice] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsEmployerClient] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SourceWithholding] bit NOT NULL DEFAULT CAST(0 AS bit),
        [NaturalHasRut] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CiiuCode] nvarchar(20) NULL,
        [ThirdPartyType] nvarchar(2) NULL,
        [WithholdingAux] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WithholdingAuxAmount] decimal(17,2) NULL,
        [WithholdingAuxPct] decimal(7,4) NULL,
        [WithholdingAuxAccount] nvarchar(20) NULL,
        [SupplierBankCode] nvarchar(20) NULL,
        [SupplierBankAccountType] nvarchar(2) NULL,
        [SupplierBankAccountNumber] nvarchar(30) NULL,
        [SupplierAdvisorId] nvarchar(20) NULL,
        [IsAssociate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsEmployee] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsAdvisor] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsThirdParty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ReceivesInvoice] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsCustomer] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsSupplier] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsSalesperson] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] nvarchar(2) NULL,
        [IsDisabled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsInsolvent] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsDeceased] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LegacyUser] nvarchar(20) NULL,
        [LegacyUserName] nvarchar(80) NULL,
        [LegacyRecordDate] datetime2 NULL,
        [LegacySystemDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_People] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_People_COR_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [dbo].[COR_Cities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_People_COR_Cities_MailingCityId] FOREIGN KEY ([MailingCityId]) REFERENCES [dbo].[COR_Cities] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Products] (
        [Id] int NOT NULL IDENTITY,
        [ProductCode] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [ShortName] nvarchar(80) NULL,
        [GroupId] int NULL,
        [DiscountTypeId] int NULL,
        [UnitOfMeasure] nvarchar(10) NULL,
        [CostPrice] decimal(18,2) NOT NULL,
        [SalePrice] decimal(18,2) NOT NULL,
        [VatRate] decimal(6,3) NOT NULL,
        [MinStock] int NOT NULL,
        [MaxStock] int NOT NULL,
        [CurrentStock] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Barcode] nvarchar(30) NULL,
        [OtherTax] decimal(10,2) NOT NULL,
        [ControlsStock] bit NOT NULL,
        [RestrictsLimit] bit NOT NULL,
        [MaxSalesQuantity] int NOT NULL,
        [DiscountTypeId1] int NULL,
        [ProductGroupId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Products_INV_DiscountTypes_DiscountTypeId] FOREIGN KEY ([DiscountTypeId]) REFERENCES [dbo].[INV_DiscountTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INV_Products_INV_DiscountTypes_DiscountTypeId1] FOREIGN KEY ([DiscountTypeId1]) REFERENCES [dbo].[INV_DiscountTypes] ([Id]),
        CONSTRAINT [FK_INV_Products_INV_ProductGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[INV_ProductGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INV_Products_INV_ProductGroups_ProductGroupId] FOREIGN KEY ([ProductGroupId]) REFERENCES [dbo].[INV_ProductGroups] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_AuxiliaryDocuments] (
        [Id] bigint NOT NULL IDENTITY,
        [LegacyKey] nvarchar(100) NULL,
        [PeriodYear] int NOT NULL,
        [AuxiliaryType] nvarchar(5) NOT NULL,
        [AccountId] int NOT NULL,
        [PersonId] int NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [DocumentType] nvarchar(5) NULL,
        [DocumentNumber] nvarchar(20) NULL,
        [Detail] nvarchar(200) NULL,
        [DocumentDate] date NULL,
        [DueDate] date NULL,
        [OriginalValue] decimal(18,2) NOT NULL,
        [InitialBalance] decimal(18,2) NOT NULL,
        [InvoiceNumber] nvarchar(20) NULL,
        [JanDebit] decimal(18,2) NOT NULL,
        [JanCredit] decimal(18,2) NOT NULL,
        [FebDebit] decimal(18,2) NOT NULL,
        [FebCredit] decimal(18,2) NOT NULL,
        [MarDebit] decimal(18,2) NOT NULL,
        [MarCredit] decimal(18,2) NOT NULL,
        [AprDebit] decimal(18,2) NOT NULL,
        [AprCredit] decimal(18,2) NOT NULL,
        [MayDebit] decimal(18,2) NOT NULL,
        [MayCredit] decimal(18,2) NOT NULL,
        [JunDebit] decimal(18,2) NOT NULL,
        [JunCredit] decimal(18,2) NOT NULL,
        [JulDebit] decimal(18,2) NOT NULL,
        [JulCredit] decimal(18,2) NOT NULL,
        [AugDebit] decimal(18,2) NOT NULL,
        [AugCredit] decimal(18,2) NOT NULL,
        [SepDebit] decimal(18,2) NOT NULL,
        [SepCredit] decimal(18,2) NOT NULL,
        [OctDebit] decimal(18,2) NOT NULL,
        [OctCredit] decimal(18,2) NOT NULL,
        [NovDebit] decimal(18,2) NOT NULL,
        [NovCredit] decimal(18,2) NOT NULL,
        [DecDebit] decimal(18,2) NOT NULL,
        [DecCredit] decimal(18,2) NOT NULL,
        [Period13Amount] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_AuxiliaryDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_AuxiliaryDocuments_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_AuxiliaryDocuments_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_AuxiliaryDocuments_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_AuxiliaryDocuments_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_FinancialReports] (
        [Id] int NOT NULL IDENTITY,
        [FormatId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [PersonId] int NOT NULL,
        [AccountId] int NOT NULL,
        [Year] int NOT NULL,
        [DocumentTypeCode] int NULL,
        [VerificationDigit] nvarchar(1) NULL,
        [LastName1] nvarchar(80) NULL,
        [LastName2] nvarchar(80) NULL,
        [FirstName] nvarchar(80) NULL,
        [CompanyName] nvarchar(80) NULL,
        [FirstName2] nvarchar(50) NULL,
        [SecondName] nvarchar(50) NULL,
        [Municipality] int NULL,
        [Address] nvarchar(100) NULL,
        [SavingsAccount] nvarchar(50) NULL,
        [Value1] decimal(18,2) NULL,
        [Value2] decimal(18,2) NULL,
        [Value3] decimal(18,2) NULL,
        [Value4] decimal(18,2) NULL,
        [Value5] decimal(18,2) NULL,
        [Value6] decimal(18,2) NULL,
        [Value7] decimal(18,2) NULL,
        [Value8] decimal(18,2) NULL,
        [Value9] decimal(18,2) NULL,
        [Value10] decimal(18,2) NULL,
        [ProcessedByLegacySystem] nvarchar(1) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_FinancialReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_FinancialReports_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_FinancialReports_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_JournalEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [LegacySequence] bigint NULL,
        [VoucherTypeCode] nvarchar(10) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [AccountId] int NOT NULL,
        [PersonId] int NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [PeriodCode] nvarchar(10) NULL,
        [TransactionDate] date NOT NULL,
        [Description] nvarchar(500) NULL,
        [AuxiliaryDocument] nvarchar(50) NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [InvoiceNumber] nvarchar(50) NULL,
        [AuxiliaryRequestId] int NULL,
        [UserName] nvarchar(100) NULL,
        [DocumentType] nvarchar(10) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ACC_JournalEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_JournalEntries_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_JournalEntries_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_JournalEntries_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ACC_JournalEntries_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_ThirdPartyAccounts] (
        [Id] bigint NOT NULL IDENTITY,
        [PeriodYear] int NOT NULL,
        [AccountId] int NOT NULL,
        [PersonId] int NOT NULL,
        [BranchId] int NOT NULL,
        [CostCenterId] int NOT NULL,
        [InitialBalance] decimal(18,2) NOT NULL,
        [JanDebit] decimal(18,2) NOT NULL,
        [JanCredit] decimal(18,2) NOT NULL,
        [FebDebit] decimal(18,2) NOT NULL,
        [FebCredit] decimal(18,2) NOT NULL,
        [MarDebit] decimal(18,2) NOT NULL,
        [MarCredit] decimal(18,2) NOT NULL,
        [AprDebit] decimal(18,2) NOT NULL,
        [AprCredit] decimal(18,2) NOT NULL,
        [MayDebit] decimal(18,2) NOT NULL,
        [MayCredit] decimal(18,2) NOT NULL,
        [JunDebit] decimal(18,2) NOT NULL,
        [JunCredit] decimal(18,2) NOT NULL,
        [JulDebit] decimal(18,2) NOT NULL,
        [JulCredit] decimal(18,2) NOT NULL,
        [AugDebit] decimal(18,2) NOT NULL,
        [AugCredit] decimal(18,2) NOT NULL,
        [SepDebit] decimal(18,2) NOT NULL,
        [SepCredit] decimal(18,2) NOT NULL,
        [OctDebit] decimal(18,2) NOT NULL,
        [OctCredit] decimal(18,2) NOT NULL,
        [NovDebit] decimal(18,2) NOT NULL,
        [NovCredit] decimal(18,2) NOT NULL,
        [DecDebit] decimal(18,2) NOT NULL,
        [DecCredit] decimal(18,2) NOT NULL,
        [Period13Debit] decimal(18,2) NOT NULL,
        [Period13Credit] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_ThirdPartyAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_ThirdPartyAccounts_ACC_ChartOfAccounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_ThirdPartyAccounts_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_ThirdPartyAccounts_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ACC_ThirdPartyAccounts_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_AssociateCategories] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [Category1] nvarchar(2) NULL,
        [Category2] nvarchar(2) NULL,
        [Category3] nvarchar(2) NULL,
        [Category4] nvarchar(2) NULL,
        [Category5] nvarchar(2) NULL,
        [DaysCategory] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_AssociateCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_AssociateCategories_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Associates] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [JoinDate] date NULL,
        [ContributionRate] decimal(10,5) NOT NULL DEFAULT 0.0,
        [BranchId] int NULL,
        [CostCenterId] int NULL,
        [SectionId] int NULL,
        [CutoffDay] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [Status] nvarchar(2) NULL,
        [WithdrawalDate] date NULL,
        [WithdrawalReasonId] int NULL,
        [RejoinDate] date NULL,
        [CategoryRating] nvarchar(2) NULL,
        [AdvisorId] int NULL,
        [DeductionPeriod] nvarchar(2) NULL,
        [DeductionType] nvarchar(2) NULL,
        [Rank] nvarchar(4) NULL,
        [ReferredBy] nvarchar(20) NULL,
        [IsInLegalCollection] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ContractNumber] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [ContractExpiryDate] date NULL,
        [CommitteeId] int NULL,
        [ZoneCode] nvarchar(20) NULL,
        [ContributionPledged] decimal(17,2) NOT NULL DEFAULT 0.0,
        [AssociateClass] nvarchar(4) NULL,
        [PaymentType] nvarchar(4) NULL,
        [SectorCode] nvarchar(10) NULL,
        [LastTransferDate] date NULL,
        [MailingOption] smallint NOT NULL DEFAULT CAST(0 AS smallint),
        [ManualRating] nvarchar(2) NULL,
        [PreviousClass] nvarchar(2) NULL,
        [EmployerCompanyId] int NULL,
        [ExternalEmployerName] nvarchar(80) NULL,
        [ExternalEmploymentStartDate] date NULL,
        [ExternalSalary] decimal(17,2) NOT NULL DEFAULT 0.0,
        [ExternalSalaryType] nvarchar(2) NULL,
        [ExternalSeverance] decimal(17,2) NOT NULL DEFAULT 0.0,
        [ExternalSeveranceFund] nvarchar(100) NULL,
        [ExternalProfessionId] int NULL,
        [ExternalPositionId] int NULL,
        [ExternalOtherIncomeDescription] nvarchar(120) NULL,
        [DepositBankId] int NULL,
        [DepositBankAccountNumber] nvarchar(30) NULL,
        [DepositBankAccountType] nvarchar(2) NULL,
        [DepositBankAccountCityId] int NULL,
        [AuthCentralRisk] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PosCardClass] nvarchar(2) NULL,
        [PosCardLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [InsuranceRiskRate] decimal(9,3) NOT NULL DEFAULT 0.0,
        [AssociateZoneTypeId] int NOT NULL DEFAULT 0,
        [AssociateZoneId] int NOT NULL DEFAULT 0,
        [IsSiplaExempt] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SiplaExemptDate] datetime2 NULL,
        [SiplaUser] nvarchar(20) NULL,
        [SinglePromissoryNote] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PledgesContributions] bit NOT NULL DEFAULT CAST(0 AS bit),
        [InManagement] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreditLimit] decimal(17,2) NOT NULL DEFAULT 0.0,
        [CpAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CpContributions] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CpLocal] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CpCommission] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ProfitCenter] nvarchar(20) NULL,
        [CapacityPayPct] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsFromGovernment] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsPublicResourceAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsPensioner] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsInsubordinate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsOnVacation] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsOnUnpaidLeave] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AssociatePensionType] nvarchar(10) NULL,
        [AssociateSeveranceType] nvarchar(10) NULL,
        [SpouseEmployer] nvarchar(80) NULL,
        [SpouseEmployerAddress] nvarchar(80) NULL,
        [SpouseEmployerStart] date NULL,
        [SpouseSalary] decimal(17,2) NOT NULL DEFAULT 0.0,
        [SpouseSalaryType] nvarchar(2) NULL,
        [SpousePosition] nvarchar(60) NULL,
        [SpouseProfession] nvarchar(10) NULL,
        [SpouseEducationLevel] nvarchar(2) NULL,
        [SpouseSeverance] decimal(17,2) NULL,
        [SpouseOtherIncome] decimal(17,2) NULL,
        [SpouseOtherIncomeDesc] nvarchar(120) NULL,
        [SpouseCompanyCode] nvarchar(10) NULL,
        [SpouseBranchCode] nvarchar(10) NULL,
        [SpouseSectionCode] nvarchar(10) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Associates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Associates_COR_Advisors_AdvisorId] FOREIGN KEY ([AdvisorId]) REFERENCES [dbo].[COR_Advisors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Associates_COR_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Associates_COR_Committees_CommitteeId] FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Associates_COR_CostCenters_CostCenterId] FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Associates_COR_EmployerCompanies_EmployerCompanyId] FOREIGN KEY ([EmployerCompanyId]) REFERENCES [dbo].[COR_EmployerCompanies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Associates_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_COR_Associates_COR_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [dbo].[COR_Sections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Associates_COR_WithdrawalReasons_WithdrawalReasonId] FOREIGN KEY ([WithdrawalReasonId]) REFERENCES [dbo].[COR_WithdrawalReasons] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Beneficiaries] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [BeneficiaryIdNumber] nvarchar(20) NOT NULL,
        [BeneficiaryName] nvarchar(100) NOT NULL,
        [DocumentType] nvarchar(4) NULL,
        [RelationshipId] int NULL,
        [DateOfBirth] date NULL,
        [EducationLevel] nvarchar(2) NULL,
        [HasDisability] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsEmployed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Percentage] decimal(6,3) NOT NULL DEFAULT 0.0,
        [Gender] nvarchar(2) NULL,
        [Phone] nvarchar(20) NULL,
        [Address] nvarchar(120) NULL,
        [CityId] int NULL,
        [BeneficiaryType] nvarchar(2) NOT NULL DEFAULT N'A',
        [Status] nvarchar(2) NULL,
        [LegacyBenefCode] nvarchar(20) NULL,
        [LegacyNewId] nvarchar(20) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Beneficiaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Beneficiaries_COR_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [dbo].[COR_Cities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Beneficiaries_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_Beneficiaries_COR_Relationships_RelationshipId] FOREIGN KEY ([RelationshipId]) REFERENCES [dbo].[COR_Relationships] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_CommitteeMembers] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [CommitteeId] int NOT NULL,
        [LegacyUser] nvarchar(100) NULL,
        [LegacyDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_CommitteeMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_CommitteeMembers_COR_Committees_CommitteeId] FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_CommitteeMembers_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_PeopleFinancial] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [DebtCapacity] decimal(17,2) NOT NULL DEFAULT 0.0,
        [OtherIncome] decimal(17,2) NOT NULL DEFAULT 0.0,
        [TotalAssets] decimal(17,2) NOT NULL DEFAULT 0.0,
        [VariableIncome] decimal(17,2) NOT NULL DEFAULT 0.0,
        [RentalIncome] decimal(17,2) NULL,
        [PensionIncome] decimal(17,2) NULL,
        [ThirdPartyDebts] decimal(17,2) NULL,
        [MonthlyFixedExpenses] decimal(17,2) NULL,
        [PersonalExpenses] decimal(17,2) NULL,
        [PensionDeduction] decimal(17,2) NULL,
        [CreditScore] decimal(6,2) NOT NULL DEFAULT 0.0,
        [CreditBureauScore] decimal(15,3) NOT NULL DEFAULT 0.0,
        [CreditBureauRating] nvarchar(4) NULL,
        [ExternalDebtPayment] decimal(15,3) NOT NULL DEFAULT 0.0,
        [ExternalDebtBalance] decimal(15,3) NOT NULL DEFAULT 0.0,
        [PastDueCreditBureau] decimal(15,3) NOT NULL DEFAULT 0.0,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_PeopleFinancial] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_PeopleFinancial_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_References] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [ReferenceType] nvarchar(2) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Address] nvarchar(120) NULL,
        [Phone] nvarchar(40) NULL,
        [CityId] int NULL,
        [ContactName] nvarchar(120) NULL,
        [ProductType] nvarchar(2) NULL,
        [ProductNumber] int NULL,
        [Mobile] nvarchar(40) NULL,
        [RelationshipCode] nvarchar(10) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_References] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_References_COR_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [dbo].[COR_Cities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_COR_References_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[COR_Spouses] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [SpouseName] nvarchar(80) NULL,
        [SpouseIdNumber] nvarchar(20) NULL,
        [SpouseIdType] nvarchar(2) NULL,
        [SpouseIdIssuedAt] nvarchar(80) NULL,
        [SpouseIdIssueDate] date NULL,
        [SpouseAddress] nvarchar(80) NULL,
        [SpousePhone] nvarchar(30) NULL,
        [SpouseCity] nvarchar(40) NULL,
        [SpouseFax] nvarchar(30) NULL,
        [SpouseDateOfBirth] date NULL,
        [SpouseGender] nvarchar(2) NULL,
        [SpouseMailingPref] nvarchar(2) NULL,
        [SpouseMailingAddress] nvarchar(120) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_COR_Spouses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_COR_Spouses_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Salespeople] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [SalespersonType] int NULL,
        [AppliesCommission] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_INV_Salespeople] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Salespeople_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_LoanPortfolios] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [IdentificationNumber] nvarchar(20) NOT NULL,
        [ApplicationDate] date NOT NULL,
        [ApprovalDate] date NULL,
        [DisbursementDate] date NOT NULL,
        [DiscountStartDate] date NOT NULL,
        [LastAccrualDate] date NULL,
        [LastPaymentDate] date NULL,
        [LastDefaultDate] date NULL,
        [MaturityDate] date NULL,
        [ClosingDate] date NULL,
        [TermMonths] int NOT NULL,
        [RequestedAmount] decimal(18,2) NOT NULL,
        [ApprovedAmount] decimal(18,2) NOT NULL,
        [CurrentBalance] decimal(18,2) NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [InterestRate] decimal(10,6) NOT NULL,
        [PaymentCycle] nvarchar(5) NOT NULL,
        [PaymentPeriodicity] nvarchar(5) NOT NULL,
        [InstallmentType] nvarchar(5) NOT NULL,
        [InterestType] nvarchar(5) NOT NULL,
        [GuaranteeType] nvarchar(5) NOT NULL,
        [DeductionType] nvarchar(5) NOT NULL,
        [PaidInstallments] int NOT NULL,
        [AdminFeeRate] decimal(10,6) NOT NULL,
        [InsuranceRate] decimal(10,6) NOT NULL,
        [CapitalBalanceCurrent] decimal(18,2) NOT NULL,
        [CapitalBalanceMonthly] decimal(18,2) NOT NULL,
        [CapitalBalancePayment] decimal(18,2) NOT NULL,
        [InterestBalanceCurrent] decimal(18,2) NOT NULL,
        [InterestBalanceMonthly] decimal(18,2) NOT NULL,
        [InterestBalancePayment] decimal(18,2) NOT NULL,
        [DefaultBalanceCurrent] decimal(18,2) NOT NULL,
        [DefaultBalanceMonthly] decimal(18,2) NOT NULL,
        [DefaultBalancePayment] decimal(18,2) NOT NULL,
        [AdminBalanceCurrent] decimal(18,2) NOT NULL,
        [AdminBalanceMonthly] decimal(18,2) NOT NULL,
        [AdminBalancePayment] decimal(18,2) NOT NULL,
        [InsuranceBalanceCurrent] decimal(18,2) NOT NULL,
        [InsuranceBalanceMonthly] decimal(18,2) NOT NULL,
        [InsuranceBalancePayment] decimal(18,2) NOT NULL,
        [DocumentType] nvarchar(5) NOT NULL,
        [DocumentNumber] int NOT NULL,
        [AdditionalCharges] decimal(18,2) NOT NULL,
        [ExtraPaymentAmount] decimal(18,2) NOT NULL,
        [CapitalAccrued] decimal(18,2) NOT NULL,
        [InterestAccrued] decimal(18,2) NOT NULL,
        [InsuranceAccrued] decimal(18,2) NOT NULL,
        [AdminAccrued] decimal(18,2) NOT NULL,
        [DefaultInterest] decimal(18,2) NOT NULL,
        [DaysOverdue] int NOT NULL,
        [InitialGracePeriod] nvarchar(2) NOT NULL,
        [GracePeriodStartDate] date NULL,
        [GracePeriodInstallment] nvarchar(2) NOT NULL,
        [GracePeriodDays] int NOT NULL,
        [Codeudor1] nvarchar(20) NOT NULL,
        [Codeudor2] nvarchar(20) NOT NULL,
        [Codeudor3] nvarchar(20) NOT NULL,
        [Codeudor4] nvarchar(20) NOT NULL,
        [GuaranteeValue] decimal(18,2) NOT NULL,
        [ContributionsAmount] decimal(18,2) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [Category] nvarchar(5) NOT NULL,
        [ExtraPercentage] decimal(18,2) NOT NULL,
        [PaidInstallmentsAgency] decimal(18,2) NOT NULL,
        [PendingInstallmentCount] decimal(18,2) NOT NULL,
        [SearchNumber] nvarchar(10) NOT NULL,
        [ProvisionRate] decimal(10,4) NOT NULL,
        [ProvisionAmount] decimal(18,2) NOT NULL,
        [OrderInterest] decimal(18,2) NOT NULL,
        [CxcInterest] decimal(18,2) NOT NULL,
        [LocalClearing] decimal(18,2) NOT NULL,
        [OtherClearing] decimal(18,2) NOT NULL,
        [LegalCollection] nvarchar(5) NOT NULL,
        [LawyerCode] nvarchar(5) NOT NULL,
        [AdminInstallment] decimal(18,2) NOT NULL,
        [InsuranceInstallment] decimal(18,2) NOT NULL,
        [CapitalInstallment] decimal(18,2) NOT NULL,
        [InterestInstallment] decimal(18,2) NOT NULL,
        [OtherInstallment] decimal(18,2) NOT NULL,
        [ApplicationNumber] int NOT NULL,
        [ExtraInMonth] nvarchar(2) NOT NULL,
        [ExtraInAdvance] nvarchar(2) NOT NULL,
        [FirstPaymentFlag] nvarchar(2) NOT NULL,
        [SecondPaymentType] nvarchar(2) NOT NULL,
        [InterestInstallmentAmt] decimal(18,2) NOT NULL,
        [LessAmount] decimal(18,2) NOT NULL,
        [CardNumber] nvarchar(20) NOT NULL,
        [CapitalAppliedPayroll] decimal(18,2) NOT NULL,
        [InterestAppliedPayroll] decimal(18,2) NOT NULL,
        [DefaultAppliedPayroll] decimal(18,2) NOT NULL,
        [InsuranceAppliedPayroll] decimal(18,2) NOT NULL,
        [AdminAppliedPayroll] decimal(18,2) NOT NULL,
        [PayrollCode] nvarchar(8) NOT NULL,
        [InsuranceForm] nvarchar(3) NOT NULL,
        [TotalPriorInterest] nvarchar(2) NOT NULL,
        [DiscountCompany] nvarchar(5) NOT NULL,
        [IncludeAutoDebit] nvarchar(2) NOT NULL,
        [CifinStartDate] date NULL,
        [CifinEndDate] date NULL,
        [CifinDaysOverdue] int NOT NULL,
        [CifinOverdueBalance] decimal(18,2) NOT NULL,
        [RestructureDate] date NULL,
        [RestructureCategory] nvarchar(2) NOT NULL,
        [IsRestructured] nvarchar(2) NOT NULL,
        [AuthCreditBureau] nvarchar(2) NOT NULL,
        [CifinOverdueInstallments] int NOT NULL,
        [IsWrittenOff] nvarchar(2) NOT NULL,
        [TransactionId] nvarchar(15) NOT NULL,
        [DtfRate] decimal(18,2) NOT NULL,
        [SpreadPoints] decimal(18,2) NOT NULL,
        [HasLawsuit] nvarchar(2) NOT NULL,
        [WriteOffCapital] decimal(18,2) NOT NULL,
        [WriteOffInterest] decimal(18,2) NOT NULL,
        [WriteOffDate] datetime2 NULL,
        [WriteOffMinutes] nvarchar(30) NOT NULL,
        [LawyerNotes] nvarchar(250) NOT NULL,
        [WriteOffApprovalDate] datetime2 NULL,
        [TransferConcept] nvarchar(2) NOT NULL,
        [IsFopep] nvarchar(2) NOT NULL,
        [ProposedInterestDate] datetime2 NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [LegacyLinCred] int NULL,
        [LegacyNumero] bigint NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_LND_LoanPortfolios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_LoanPortfolios_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LND_LoanPortfolios_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_Employees] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [CostCenterId] nvarchar(8) NOT NULL,
        [PositionId] int NOT NULL,
        [AreaCode] nvarchar(4) NULL,
        [SectionId] nvarchar(4) NULL,
        [Salary] decimal(18,2) NOT NULL,
        [SalaryType] int NOT NULL,
        [EffectiveDate] datetime2 NULL,
        [ContractType] int NOT NULL,
        [ContractEndDate] datetime2 NOT NULL,
        [JoinDate] datetime2 NOT NULL,
        [TerminationDate] datetime2 NOT NULL,
        [TerminationCause] nvarchar(4) NULL,
        [RehireDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [EmployeeType] int NOT NULL,
        [WithholdingTaxRate] decimal(7,4) NULL,
        [WithholdingCycle] int NOT NULL,
        [WithholdingAvgType] int NOT NULL,
        [DeductibleWithholding] decimal(17,4) NOT NULL,
        [FirstPayCycle] int NOT NULL,
        [ContributionCycle] int NOT NULL,
        [TransportSubsidyClass] int NOT NULL,
        [PaymentMethod] int NOT NULL,
        [PayrollClass] int NOT NULL,
        [HealthInsuranceId] int NOT NULL,
        [PensionFundId] int NOT NULL,
        [WorkRiskId] int NOT NULL,
        [WorkRiskRateId] int NOT NULL,
        [SeveranceFundId] decimal(6,0) NOT NULL,
        [SenaId] int NOT NULL,
        [IcbfId] int NOT NULL,
        [FamilySubsidyId] int NOT NULL,
        [PensionFundMember] nvarchar(1) NULL,
        [PayrollBankId] nvarchar(4) NULL,
        [PayrollBankAccountType] int NOT NULL,
        [PayrollBankAccountNumber] nvarchar(25) NULL,
        [RepresentationExpense] decimal(18,2) NOT NULL,
        [TechnicalBonus] decimal(18,2) NOT NULL,
        [OtherBonus] decimal(18,2) NOT NULL,
        [SeveranceCauseDate] datetime2 NOT NULL,
        [BonusDays] decimal(10,0) NOT NULL,
        [VacationDays] decimal(10,0) NOT NULL,
        [IndemnityDays] decimal(10,0) NOT NULL,
        [SeveranceAvgDays] decimal(10,0) NOT NULL,
        [BonusAvgDays] decimal(10,0) NOT NULL,
        [IndemnityAvgDays] decimal(10,0) NOT NULL,
        [HolidayDays] int NOT NULL,
        [LicenseExpiryDate] datetime2 NOT NULL,
        [VacationAvgDays] decimal(10,0) NOT NULL,
        [VacationCauseDate] datetime2 NOT NULL,
        [BonusCauseDate] datetime2 NOT NULL,
        [SeveranceDaysCalc] decimal(10,0) NOT NULL,
        [IndemnityDaysCalc] decimal(10,0) NOT NULL,
        [IsLiquidated] nvarchar(1) NULL,
        [LiquidationDate] datetime2 NULL,
        [SpecialRegime] nvarchar(1) NULL,
        [ExtraBonusFlag] nvarchar(1) NULL,
        [LegacyIdNomina] int NULL,
        [LegacyIdEmpleado] bigint NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PAY_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_Employees_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_Users] (
        [Id] int NOT NULL IDENTITY,
        [PersonId] int NULL,
        [Username] nvarchar(100) NOT NULL,
        [Email] nvarchar(200) NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [PasswordSalt] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        [IsEmailVerified] bit NOT NULL,
        [IsMfaEnabled] bit NOT NULL,
        [MfaSecret] nvarchar(200) NULL,
        [LastLoginAt] datetime2 NULL,
        [LastPasswordChangeAt] datetime2 NULL,
        [FailedLoginAttempts] int NOT NULL,
        [LockoutEndAt] datetime2 NULL,
        [LegacyLogin] nvarchar(50) NULL,
        [CanApproveLoansMin] decimal(18,2) NOT NULL,
        [CanApproveLoansMax] decimal(18,2) NOT NULL,
        [CanOverrideLimits] bit NOT NULL,
        [IdentificationNumber] nvarchar(20) NULL,
        [IsSaasOperator] bit NOT NULL DEFAULT CAST(0 AS bit),
        [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_Users_COR_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Discounts] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] int NULL,
        [DiscountTypeId] int NULL,
        [DiscountClass] int NULL,
        [CustomerId] nvarchar(20) NULL,
        [ProductClass] nvarchar(5) NULL,
        [CustomerType] nvarchar(5) NULL,
        [PaymentClassId] int NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [QuantityStart] int NOT NULL,
        [QuantityEnd] int NOT NULL,
        [PurchasePeriod] int NULL,
        [PurchaseAmount] int NOT NULL,
        [DiscountRate] decimal(6,3) NOT NULL,
        [PeriodCode] int NULL,
        [GroupId] nvarchar(10) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Discounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Discounts_INV_DiscountTypes_DiscountTypeId] FOREIGN KEY ([DiscountTypeId]) REFERENCES [dbo].[INV_DiscountTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INV_Discounts_INV_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_OrderTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [TransactionTypeId] int NOT NULL,
        [SequenceNumber] decimal(18,0) NOT NULL,
        [TransactionDate] date NOT NULL,
        [InvoiceNumber] nvarchar(20) NULL,
        [ProductId] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [VatRate] decimal(6,3) NOT NULL,
        [DiscountRate] decimal(6,3) NOT NULL,
        [CostAmount] decimal(16,2) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [CustomerId] int NULL,
        [VatAmount] decimal(18,3) NOT NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [UserId] nvarchar(20) NULL,
        [SalesPointId] int NULL,
        [ShiftId] int NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [NetTotal] decimal(18,3) NOT NULL,
        [PeriodCode] int NULL,
        [SaleType] nvarchar(5) NULL,
        [MovementClass] nvarchar(5) NULL,
        [AdminFee] decimal(10,2) NOT NULL,
        [AdminVat] decimal(10,2) NOT NULL,
        [TicketVat] decimal(10,2) NOT NULL,
        [OtherTax] decimal(10,2) NOT NULL,
        [AirportTax] decimal(10,2) NOT NULL,
        [FuelTax] decimal(10,2) NOT NULL,
        [WithholdingRate] decimal(6,3) NOT NULL,
        [WithholdingAmount] decimal(15,2) NOT NULL,
        [IcaAmount] decimal(15,2) NOT NULL,
        [IcaRate] decimal(6,3) NOT NULL,
        [IsPosTransaction] bit NOT NULL,
        [WarehouseId] int NULL,
        [LocationId] int NULL,
        [ConsecutiveNumber] bigint NOT NULL,
        [IsOrderApplied] bit NOT NULL,
        [TransferRecord] nvarchar(100) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_OrderTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_OrderTransactions_INV_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INV_OrderTransactions_INV_TransactionTypes_TransactionTypeId] FOREIGN KEY ([TransactionTypeId]) REFERENCES [dbo].[INV_TransactionTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_PhysicalInventory] (
        [Id] bigint NOT NULL IDENTITY,
        [PeriodCode] nvarchar(10) NOT NULL,
        [ProductId] int NOT NULL,
        [LocationId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [PhysicalCount] int NOT NULL,
        [TheoreticalCount] int NOT NULL,
        [Cost] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_PhysicalInventory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_PhysicalInventory_INV_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Prices] (
        [Id] int NOT NULL IDENTITY,
        [PriceListTypeId] int NOT NULL,
        [ProductId] int NOT NULL,
        [CustomerType] nvarchar(5) NOT NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [PriceValue] decimal(18,2) NOT NULL,
        [PriceClass] int NULL,
        [Description] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Prices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Prices_INV_PriceListTypes_PriceListTypeId] FOREIGN KEY ([PriceListTypeId]) REFERENCES [dbo].[INV_PriceListTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INV_Prices_INV_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[INV_Transactions] (
        [Id] bigint NOT NULL IDENTITY,
        [TransactionTypeId] int NOT NULL,
        [SequenceNumber] decimal(18,0) NOT NULL,
        [TransactionDate] date NOT NULL,
        [InvoiceNumber] nvarchar(20) NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [VatRate] decimal(6,3) NOT NULL,
        [DiscountRate] decimal(6,3) NOT NULL,
        [CostFlag] tinyint NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [CustomerId] int NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [UserId] nvarchar(20) NULL,
        [SalesPointId] int NULL,
        [ShiftId] int NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [NetTotal] decimal(18,2) NOT NULL,
        [AdminFee] decimal(10,2) NOT NULL,
        [AdminVat] decimal(10,2) NOT NULL,
        [TicketVat] decimal(10,2) NOT NULL,
        [OtherTax] decimal(10,2) NOT NULL,
        [AirportTax] decimal(10,2) NOT NULL,
        [FuelTax] decimal(10,2) NOT NULL,
        [IsPosTransaction] bit NOT NULL,
        [WarehouseId] int NULL,
        [LocationId] int NULL,
        [ConsecutiveNumber] bigint NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_INV_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_INV_Transactions_INV_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INV_Transactions_INV_TransactionTypes_TransactionTypeId] FOREIGN KEY ([TransactionTypeId]) REFERENCES [dbo].[INV_TransactionTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ACC_JournalEntryItems] (
        [Id] bigint NOT NULL IDENTITY,
        [JournalEntryId] bigint NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [PersonTaxId] nvarchar(20) NULL,
        [BranchCode] nvarchar(5) NULL,
        [CostCenterCode] nvarchar(10) NULL,
        [TransactionDate] date NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [Description] nvarchar(200) NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [DocumentType] nvarchar(5) NULL,
        [DocumentNumber] nvarchar(20) NULL,
        [Period] nvarchar(10) NULL,
        [VoucherTypeCode] nvarchar(10) NULL,
        [VoucherNumber] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ACC_JournalEntryItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ACC_JournalEntryItems_ACC_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[ACC_JournalEntries] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_DefaultRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccrualPeriod] int NOT NULL,
        [AccountingPeriod] int NOT NULL,
        [DaysOverdue] int NOT NULL,
        [CapitalBalance] decimal(18,2) NOT NULL,
        [ExtraBalance] decimal(18,2) NOT NULL,
        [InterestBalance] decimal(18,2) NOT NULL,
        [DefaultBalance] decimal(18,2) NOT NULL,
        [InsuranceBalance] decimal(18,2) NOT NULL,
        [AdminBalance] decimal(18,2) NOT NULL,
        [OtherBalance] decimal(18,2) NOT NULL,
        [PriorCapitalBalance] decimal(18,2) NOT NULL,
        [PriorExtraBalance] decimal(18,2) NOT NULL,
        [PriorInterestBalance] decimal(18,2) NOT NULL,
        [PriorDefaultBalance] decimal(18,2) NOT NULL,
        [PriorInsuranceBalance] decimal(18,2) NOT NULL,
        [PriorAdminBalance] decimal(18,2) NOT NULL,
        [PriorOtherBalance] decimal(18,2) NOT NULL,
        [AccruedCapital] decimal(18,2) NOT NULL,
        [AccruedExtra] decimal(18,2) NOT NULL,
        [AccruedInterest] decimal(18,2) NOT NULL,
        [AccruedDefault] decimal(18,2) NOT NULL,
        [AccruedInsurance] decimal(18,2) NOT NULL,
        [AccruedAdmin] decimal(18,2) NOT NULL,
        [AccruedOther] decimal(18,2) NOT NULL,
        [PaidCapital] decimal(18,2) NOT NULL,
        [PaidExtra] decimal(18,2) NOT NULL,
        [PaidInterest] decimal(18,2) NOT NULL,
        [PaidDefault] decimal(18,2) NOT NULL,
        [PaidInsurance] decimal(18,2) NOT NULL,
        [PaidAdmin] decimal(18,2) NOT NULL,
        [PaidOther] decimal(18,2) NOT NULL,
        [LastLiquidationDate] date NULL,
        [ExtraNumber] int NOT NULL,
        [CxcFlag] int NOT NULL,
        [IsManaged] nvarchar(2) NOT NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [LoanPortfolioId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_DefaultRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_DefaultRecords_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LND_DefaultRecords_LND_LoanPortfolios_LoanPortfolioId] FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_ExtraPayments] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [ExtraNumber] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentForm] nvarchar(2) NOT NULL,
        [PaymentDate] date NOT NULL,
        [CurrentBalance] decimal(18,2) NOT NULL,
        [ChargesAmount] decimal(18,2) NOT NULL,
        [PaymentsAmount] decimal(18,2) NOT NULL,
        [PaymentCycle] int NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [ExtraType] nvarchar(3) NOT NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [LoanPortfolioId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_ExtraPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_ExtraPayments_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LND_ExtraPayments_LND_LoanPortfolios_LoanPortfolioId] FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Guarantees] (
        [Id] int NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [RegistrationNumber] nvarchar(25) NOT NULL,
        [GuaranteeType] nvarchar(3) NOT NULL,
        [GuaranteeDescription] nvarchar(max) NOT NULL,
        [CadastralAppraisal] decimal(18,2) NOT NULL,
        [CommercialAppraisal] decimal(18,2) NOT NULL,
        [HasInsurance] nvarchar(2) NOT NULL,
        [PolicyNumber] nvarchar(20) NOT NULL,
        [GuaranteeStartDate] date NULL,
        [GuaranteeCancelDate] date NULL,
        [MaturityDate] date NULL,
        [InsurerIdentification] nvarchar(20) NOT NULL,
        [InsurerName] nvarchar(50) NOT NULL,
        [GuaranteeStatus] nvarchar(2) NOT NULL,
        [UserId] nvarchar(20) NOT NULL,
        [InsuredPercentage] decimal(5,2) NULL,
        [Guarantor1] nvarchar(20) NOT NULL,
        [Guarantor2] nvarchar(20) NOT NULL,
        [Guarantor3] nvarchar(20) NOT NULL,
        [Guarantor4] nvarchar(20) NOT NULL,
        [Term] int NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [CdatNumber] int NOT NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [LoanPortfolioId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Guarantees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_Guarantees_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LND_Guarantees_LND_LoanPortfolios_LoanPortfolioId] FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_PendingInstallments] (
        [Id] bigint NOT NULL IDENTITY,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccrualPeriod] int NOT NULL,
        [AccountingPeriod] int NOT NULL,
        [CompanyCode] nvarchar(5) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [Periodicity] nvarchar(2) NOT NULL,
        [Description] nvarchar(50) NOT NULL,
        [Cycle] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [AccruedInterest] decimal(18,2) NOT NULL,
        [AccruedCapital] decimal(18,2) NOT NULL,
        [AccruedExtra] decimal(18,2) NOT NULL,
        [PaidInterest] decimal(18,2) NOT NULL,
        [PaidCapital] decimal(18,2) NOT NULL,
        [PaidExtra] decimal(18,2) NOT NULL,
        [BalanceInterest] decimal(18,2) NOT NULL,
        [BalanceCapital] decimal(18,2) NOT NULL,
        [BalanceExtra] decimal(18,2) NOT NULL,
        [CreditBalance] decimal(18,2) NOT NULL,
        [ExtraNumber] int NOT NULL,
        [ObligationInstallment] decimal(18,2) NOT NULL,
        [TotalInstallment] decimal(18,2) NOT NULL,
        [ExtraPaymentForm] nvarchar(2) NOT NULL,
        [DefaultInterest] decimal(18,2) NOT NULL,
        [DefaultInterestBalance] decimal(18,2) NOT NULL,
        [TransactionDate] date NULL,
        [DeductionType] nvarchar(2) NOT NULL,
        [PriorCapitalBalance] decimal(18,2) NOT NULL,
        [PriorInterestBalance] decimal(18,2) NOT NULL,
        [PriorExtraBalance] decimal(18,2) NOT NULL,
        [DaysToMaturity] int NOT NULL,
        [IsAdvancePayment] nvarchar(2) NOT NULL,
        [DefaultInterestAccrued] decimal(18,2) NOT NULL,
        [DefaultInterestPaid] decimal(18,2) NOT NULL,
        [LastDefaultDate] date NULL,
        [AdvanceAmount] decimal(18,2) NOT NULL,
        [AdvanceCapital] decimal(18,2) NOT NULL,
        [AdvanceInterest] decimal(18,2) NOT NULL,
        [AdvanceExtra] decimal(18,2) NOT NULL,
        [TotalBalance] decimal(18,2) NOT NULL,
        [DaysOverdue] decimal(18,2) NOT NULL,
        [InterestRate] decimal(7,4) NOT NULL,
        [EntryType] nvarchar(2) NOT NULL,
        [PriorInsuranceBalance] decimal(18,2) NOT NULL,
        [PriorAdminBalance] decimal(18,2) NOT NULL,
        [PriorOtherBalance] decimal(18,2) NOT NULL,
        [AccruedInsurance] decimal(18,2) NOT NULL,
        [AccruedAdmin] decimal(18,2) NOT NULL,
        [AccruedOther] decimal(18,2) NOT NULL,
        [PaidInsurance] decimal(18,2) NOT NULL,
        [PaidAdmin] decimal(18,2) NOT NULL,
        [PaidOther] decimal(18,2) NOT NULL,
        [BalanceInsurance] decimal(18,2) NOT NULL,
        [BalanceAdmin] decimal(18,2) NOT NULL,
        [BalanceOther] decimal(18,2) NOT NULL,
        [ProcessDate] date NULL,
        [UserId] nvarchar(20) NOT NULL,
        [SystemDate] date NOT NULL,
        [DeductionClass] nvarchar(2) NOT NULL,
        [GuaranteeClass] nvarchar(3) NOT NULL,
        [EntryReason] nvarchar(2) NOT NULL,
        [DtfRate] decimal(10,5) NOT NULL,
        [SpreadPoints] decimal(10,5) NOT NULL,
        [AccruedAll] nvarchar(2) NOT NULL,
        [AccruedNoExtra] nvarchar(2) NOT NULL,
        [AccruedOnlyExtra] nvarchar(2) NOT NULL,
        [LegacyCodigoTer] nvarchar(20) NULL,
        [LoanPortfolioId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_PendingInstallments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_PendingInstallments_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LND_PendingInstallments_LND_LoanPortfolios_LoanPortfolioId] FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[LND_Transactions] (
        [Id] bigint NOT NULL IDENTITY,
        [VoucherType] nvarchar(5) NOT NULL,
        [DocumentNumber] bigint NOT NULL,
        [PersonCode] nvarchar(20) NOT NULL,
        [CreditLineId] int NOT NULL,
        [PortfolioNumber] bigint NOT NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [TransactionDate] date NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [CostCenterId] nvarchar(10) NOT NULL,
        [BranchId] nvarchar(5) NOT NULL,
        [InterestRate] decimal(10,6) NOT NULL,
        [TransactionCode] nvarchar(3) NOT NULL,
        [Cycles] int NOT NULL,
        [SecondaryAccount] nvarchar(15) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [SearchCode] nvarchar(8) NOT NULL,
        [BankCode] nvarchar(5) NOT NULL,
        [CrossDocumentType] nvarchar(5) NOT NULL,
        [CrossDocumentNumber] nvarchar(20) NULL,
        [LocalCheckAmount] decimal(18,2) NOT NULL,
        [OtherCheckAmount] decimal(18,2) NOT NULL,
        [SecondaryCostCenter] nvarchar(10) NOT NULL,
        [IsAdvancePayment] nvarchar(2) NOT NULL,
        [WithholdingBase] decimal(18,2) NOT NULL,
        [UserId] nvarchar(20) NULL,
        [SystemDate] date NOT NULL,
        [ExtraNumber] int NOT NULL,
        [IdentificationNumber] nvarchar(20) NULL,
        [InvoiceNumber] nvarchar(20) NULL,
        [UserFullName] nvarchar(50) NOT NULL,
        [Period] nvarchar(8) NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [DueDate] date NULL,
        [CheckNumber] nvarchar(20) NOT NULL,
        [CodeudorCode] nvarchar(20) NOT NULL,
        [OverdraftUser] nvarchar(20) NULL,
        [OverdraftAmount] decimal(18,3) NULL,
        [ReliquidatedInstallment] decimal(18,3) NOT NULL,
        [TransactionSource] nvarchar(3) NOT NULL,
        [ApplicationId] int NOT NULL,
        [CdatAccrualDate] datetime2 NULL,
        [AuxiliaryApplicationId] int NOT NULL,
        [DependsOnSequence] bigint NOT NULL,
        [ReliquidationFlag] nvarchar(2) NULL,
        [SavingsLineId] int NULL,
        [LegacySecuencia] bigint NULL,
        [LoanPortfolioId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_LND_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LND_Transactions_LND_CreditLineParameters_CreditLineId] FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LND_Transactions_LND_LoanPortfolios_LoanPortfolioId] FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_Absences] (
        [Id] bigint NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [VacationCauseStart] datetime2 NOT NULL,
        [VacationCauseEnd] datetime2 NOT NULL,
        [AbsenceType] int NOT NULL,
        [DiagnosisCode] int NOT NULL,
        [IncapacityClass] int NOT NULL,
        [IsExtension] int NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [SerialNumber] nvarchar(20) NOT NULL,
        [Hours] int NOT NULL,
        [ExtensionConceptId] int NOT NULL,
        [ExtensionSequence] decimal(12,0) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_Absences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_Absences_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PAY_Absences_PAY_PayrollConcepts_ConceptId] FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_AccountingEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SequenceNumber] int NOT NULL,
        [CostCenterCode] nvarchar(10) NOT NULL,
        [AccountCode] nvarchar(15) NOT NULL,
        [TaxId] nvarchar(20) NOT NULL,
        [DocumentType] nvarchar(5) NOT NULL,
        [DocumentNumber] nvarchar(20) NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [EmployeeId] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_AccountingEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_AccountingEntries_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_BookBalances] (
        [Id] bigint NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [PeriodYear] int NOT NULL,
        [InitialAmount] decimal(18,2) NOT NULL,
        [JanCharge] decimal(18,2) NOT NULL,
        [JanPayment] decimal(18,2) NOT NULL,
        [FebCharge] decimal(18,2) NOT NULL,
        [FebPayment] decimal(18,2) NOT NULL,
        [MarCharge] decimal(18,2) NOT NULL,
        [MarPayment] decimal(18,2) NOT NULL,
        [AprCharge] decimal(18,2) NOT NULL,
        [AprPayment] decimal(18,2) NOT NULL,
        [MayCharge] decimal(18,2) NOT NULL,
        [MayPayment] decimal(18,2) NOT NULL,
        [JunCharge] decimal(18,2) NOT NULL,
        [JunPayment] decimal(18,2) NOT NULL,
        [JulCharge] decimal(18,2) NOT NULL,
        [JulPayment] decimal(18,2) NOT NULL,
        [AugCharge] decimal(18,2) NOT NULL,
        [AugPayment] decimal(18,2) NOT NULL,
        [SepCharge] decimal(18,2) NOT NULL,
        [SepPayment] decimal(18,2) NOT NULL,
        [OctCharge] decimal(18,2) NOT NULL,
        [OctPayment] decimal(18,2) NOT NULL,
        [NovCharge] decimal(18,2) NOT NULL,
        [NovPayment] decimal(18,2) NOT NULL,
        [DecCharge] decimal(18,2) NOT NULL,
        [DecPayment] decimal(18,2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_BookBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_BookBalances_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_DirectDebits] (
        [Id] bigint NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [VoucherCode] nvarchar(10) NOT NULL,
        [DebitDate] datetime2 NULL,
        [DiscountDate] datetime2 NULL,
        [InitialAmount] decimal(18,2) NOT NULL,
        [DiscountCycle] int NOT NULL,
        [InterestRate] decimal(8,4) NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [InstallmentType] int NOT NULL,
        [LiquidationBase] int NOT NULL,
        [NumberOfInstallments] int NOT NULL,
        [UserName] nvarchar(20) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [EntryUser] nvarchar(20) NOT NULL,
        [Status] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_DirectDebits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_DirectDebits_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PAY_DirectDebits_PAY_PayrollConcepts_ConceptId] FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_EmployeeLiquidationDetails] (
        [Id] bigint NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] decimal(6,0) NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [Time] decimal(10,0) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Nature] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_EmployeeLiquidationDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_EmployeeLiquidationDetails_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_EmployeeLiquidationMasters] (
        [Id] bigint NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Salary] decimal(18,2) NOT NULL,
        [JoinDate] datetime2 NOT NULL,
        [ContractType] int NOT NULL,
        [ContractEndDate] datetime2 NOT NULL,
        [SpecialRegime] nvarchar(1) NOT NULL,
        [LiquidationDate] datetime2 NOT NULL,
        [TerminationCause] int NOT NULL,
        [SeveranceUnpaidDays] int NULL,
        [BonusUnpaidDays] int NULL,
        [VacationUnpaidDays] int NULL,
        [PreviousSeveranceAmount] decimal(18,2) NOT NULL,
        [CurrentSeveranceAmount] decimal(18,2) NOT NULL,
        [LastVacationPayDate] datetime2 NOT NULL,
        [LastBonusPayDate] datetime2 NOT NULL,
        [SeveranceBase] decimal(18,2) NOT NULL,
        [BonusBase] decimal(18,2) NOT NULL,
        [VacationBase] decimal(18,2) NOT NULL,
        [IndemnityBase] decimal(18,2) NOT NULL,
        [SeveranceDays] decimal(18,2) NOT NULL,
        [BonusDays] decimal(18,2) NOT NULL,
        [VacationDays] decimal(18,2) NOT NULL,
        [IndemnityDays] decimal(18,2) NOT NULL,
        [IsAccountingPosted] nvarchar(1) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [UserName] nvarchar(14) NOT NULL,
        [LastSeverancePayDate] datetime2 NOT NULL,
        [LastSeveranceInterestDate] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_EmployeeLiquidationMasters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_EmployeeLiquidationMasters_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PayrollEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [Cycle] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [EntityCode] int NOT NULL,
        [EntryType] decimal(3,0) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [AuthorizationNumber] decimal(12,0) NOT NULL,
        [IncapacityAmount] decimal(18,2) NOT NULL,
        [UpcAmount] decimal(18,2) NOT NULL,
        [Days] int NOT NULL,
        [NewEntity] nvarchar(10) NOT NULL,
        [UserName] nvarchar(20) NOT NULL,
        [ProcessDate] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PayrollEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_PayrollEntries_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PayrollPlanLiquidations] (
        [Id] bigint NOT NULL IDENTITY,
        [PayPeriodId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] decimal(6,0) NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [Nature] int NOT NULL,
        [Days] int NOT NULL,
        [Time] decimal(10,0) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] decimal(1,0) NOT NULL,
        [CostCenterCode] nvarchar(8) NOT NULL,
        [UserName] nvarchar(14) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [RecordType] nvarchar(2) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PayrollPlanLiquidations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_PayrollPlanLiquidations_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PayrollTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [PayPeriodId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] int NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [Time] decimal(10,0) NULL,
        [Amount] decimal(18,2) NULL,
        [PaymentMethod] decimal(1,0) NULL,
        [UserName] nvarchar(50) NULL,
        [TransactionDate] datetime2 NULL,
        [Description] nvarchar(200) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PayrollTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_PayrollTransactions_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PAY_PayrollTransactions_PAY_PayrollConcepts_ConceptId] FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_PreLiquidations] (
        [Id] bigint NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Sequence] int NOT NULL,
        [IdentificationNumber] bigint NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [BasicSalary] decimal(18,2) NOT NULL,
        [Ibc] decimal(18,2) NOT NULL,
        [TotalDays] int NOT NULL,
        [PreviousDays] int NOT NULL,
        [EntryDays] int NOT NULL,
        [HealthValue] decimal(18,2) NOT NULL,
        [PensionValue] decimal(18,2) NOT NULL,
        [WorkRiskValue] decimal(18,2) NOT NULL,
        [SolidarityValue] decimal(18,2) NOT NULL,
        [BranchId] int NOT NULL,
        [ClassCode] nvarchar(1) NOT NULL,
        [PensionEntry] nvarchar(1) NOT NULL,
        [HealthEntry] nvarchar(1) NOT NULL,
        [WorkRiskEntry] nvarchar(1) NOT NULL,
        [HealthEntityId] int NOT NULL,
        [PensionEntityId] int NOT NULL,
        [WorkRiskEntityId] int NOT NULL,
        [MaternityValue] decimal(18,2) NOT NULL,
        [GeneralValue] decimal(18,2) NOT NULL,
        [IsNewHire] nvarchar(1) NOT NULL,
        [IsTermination] nvarchar(1) NOT NULL,
        [IsRateChange] nvarchar(1) NOT NULL,
        [IsEntityChange] nvarchar(1) NOT NULL,
        [IsSuspensionPension] nvarchar(1) NOT NULL,
        [IsSuspensionTemp] nvarchar(1) NOT NULL,
        [IsUnpaidLeave] nvarchar(1) NOT NULL,
        [IsGeneralIncapacity] nvarchar(1) NOT NULL,
        [IsMaternityLeave] nvarchar(1) NOT NULL,
        [IsVacation] nvarchar(1) NOT NULL,
        [IsTemporaryTransfer] nvarchar(1) NOT NULL,
        [IsVoluntaryPension] nvarchar(1) NOT NULL,
        [IsWorkRiskIncapacity] nvarchar(1) NOT NULL,
        [WorkRiskRate] decimal(5,3) NOT NULL,
        [UserName] nvarchar(15) NOT NULL,
        [ProcessDate] datetime2 NOT NULL,
        [SalaryClass] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [EmployeeName] nvarchar(40) NOT NULL,
        [RecordNumber] int NOT NULL,
        [TotalEmployees] int NOT NULL,
        [GeneralAuth] int NOT NULL,
        [MaternityAuth] int NOT NULL,
        [UpcValue] int NOT NULL,
        [IsVacationCause] nvarchar(1) NOT NULL,
        [IsVacationEnjoyment] nvarchar(1) NOT NULL,
        [CurrentSalary] decimal(18,2) NOT NULL,
        [MinimumWage] decimal(18,2) NOT NULL,
        [IncapacityClass] int NOT NULL,
        [IsExtension] nvarchar(1) NOT NULL,
        [AffiliationDays] int NOT NULL,
        [IbcWorkRisk] decimal(18,2) NOT NULL,
        [EntryCode] int NOT NULL,
        [WorkRiskIncapacityDays] int NOT NULL,
        [PilaPensionCode] nvarchar(6) NOT NULL,
        [PilaHealthCode] nvarchar(6) NOT NULL,
        [PilaWorkRiskCode] nvarchar(6) NOT NULL,
        [PilaCcfCode] nvarchar(6) NOT NULL,
        [CcfValue] int NOT NULL,
        [IsIntegralSalary] nvarchar(1) NOT NULL,
        [SenaValue] int NOT NULL,
        [IcbfValue] int NOT NULL,
        [EsapValue] int NOT NULL,
        [EducationMinValue] int NOT NULL,
        [IbcCcf] int NOT NULL,
        [SenaContrib] nvarchar(1) NOT NULL,
        [CompensationFundId] int NOT NULL,
        [Period] int NOT NULL,
        [CurrentSalaryFull] decimal(18,2) NOT NULL,
        [VacationEntryDays] int NOT NULL,
        [IncapacityEntryDays] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_PreLiquidations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_PreLiquidations_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_SalaryChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [EffectiveDate] datetime2 NOT NULL,
        [NewSalary] decimal(18,2) NOT NULL,
        [UserName] nvarchar(20) NULL,
        [EntryDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_SalaryChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_SalaryChanges_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_SeveranceHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [PayrollCompanyId] int NOT NULL,
        [PlanId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [CauseStartDate] datetime2 NULL,
        [CauseEndDate] datetime2 NULL,
        [CutoffDate] datetime2 NULL,
        [SalaryBase] decimal(18,5) NULL,
        [DaysWorked] int NULL,
        [AdvanceAmount] decimal(18,3) NULL,
        [InterestAmount] decimal(18,3) NULL,
        [Resolution] nvarchar(30) NULL,
        [ResolutionDate] datetime2 NULL,
        [Destination] nvarchar(100) NULL,
        [AdvanceConceptId] int NULL,
        [InterestConceptId] int NULL,
        [UserName] nvarchar(14) NULL,
        [SystemDate] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_SeveranceHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_SeveranceHistory_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_TaxCertificates] (
        [Id] bigint NOT NULL IDENTITY,
        [PeriodCode] nvarchar(5) NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Value34] decimal(18,2) NOT NULL,
        [Value35] decimal(18,2) NOT NULL,
        [Value36] decimal(18,2) NOT NULL,
        [Value37] decimal(18,2) NOT NULL,
        [Value38] decimal(18,2) NOT NULL,
        [Value39] decimal(18,2) NOT NULL,
        [Value40] decimal(18,2) NOT NULL,
        [Value41] decimal(18,2) NOT NULL,
        [Value42] decimal(18,2) NOT NULL,
        [Value43] decimal(18,2) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IssueDate] datetime2 NOT NULL,
        [IssuedAt] nvarchar(60) NOT NULL,
        [PayerName] nvarchar(60) NOT NULL,
        [PayerTaxId] nvarchar(20) NOT NULL,
        [Threshold] decimal(18,2) NOT NULL,
        [Rate] decimal(8,4) NOT NULL,
        [CityId] int NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_TaxCertificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_TaxCertificates_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PAY_VacationLiquidations] (
        [Id] bigint NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [PayrollCompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ConceptId] decimal(6,0) NOT NULL,
        [SequenceNumber] decimal(12,0) NOT NULL,
        [Nature] int NOT NULL,
        [Days] int NOT NULL,
        [Time] decimal(10,0) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CostCenterCode] nvarchar(8) NOT NULL,
        [UserName] nvarchar(20) NOT NULL,
        [SystemDate] datetime2 NOT NULL,
        [RecordType] nvarchar(2) NOT NULL,
        [LiquidationDate] datetime2 NULL,
        [CauseStartDate] datetime2 NULL,
        [CauseEndDate] datetime2 NULL,
        [LeaveStartDate] datetime2 NULL,
        [LeaveEndDate] datetime2 NULL,
        [ReturnDate] datetime2 NULL,
        [IsAccountingPosted] nvarchar(1) NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PAY_VacationLiquidations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PAY_VacationLiquidations_PAY_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_MfaBackupCodes] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CodeHash] nvarchar(120) NOT NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [UsedAt] datetime2 NULL,
        [BatchId] uniqueidentifier NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_MfaBackupCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_MfaBackupCodes_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_MfaResetRequests] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RequestedBy] int NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [EvidenceAttachmentId] bigint NULL,
        [Status] nvarchar(20) NOT NULL,
        [FirstApproverId] int NULL,
        [FirstApprovalAt] datetime2 NULL,
        [SecondApproverId] int NULL,
        [SecondApprovalAt] datetime2 NULL,
        [ExecutedAt] datetime2 NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_MfaResetRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_MfaResetRequests_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_Modules] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [ProgramCode] nvarchar(30) NULL,
        [Description] nvarchar(100) NULL,
        [ProgramType] nvarchar(2) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_Modules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_Modules_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_PasswordHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [SetAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SEC_PasswordHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_PasswordHistory_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_RefreshTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Token] nvarchar(500) NOT NULL,
        [TokenHash] nvarchar(120) NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [RevocationReason] nvarchar(40) NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedBy] nvarchar(100) NULL,
        [ReplacedByToken] nvarchar(500) NULL,
        [IpAddress] nvarchar(50) NULL,
        [UserAgent] nvarchar(500) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_RefreshTokens_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_UserAssignments] (
        [Id] int NOT NULL IDENTITY,
        [VoucherTypeCode] nvarchar(10) NULL,
        [UserId] int NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_UserAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_UserAssignments_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_UserBranchAssignments] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TenantId] int NOT NULL,
        [BranchId] int NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_UserBranchAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_UserBranchAssignments_ADM_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[ADM_Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SEC_UserBranchAssignments_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SEC_UserBranchAssignments_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_UserMenuAccess] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [MenuType] int NULL,
        [ProgramType] nvarchar(2) NULL,
        [ProgramCode] nvarchar(30) NULL,
        [ProgramName] nvarchar(100) NULL,
        [HasAccess] bit NOT NULL,
        [MenuCode] nvarchar(30) NULL,
        [SubMenuCode] nvarchar(30) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_UserMenuAccess] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_UserMenuAccess_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_UserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SEC_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_SEC_UserRoles_SEC_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SEC_UserRoles_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_UserSessions] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [SessionToken] nvarchar(500) NOT NULL,
        [IpAddress] nvarchar(50) NULL,
        [UserAgent] nvarchar(500) NULL,
        [StartedAt] datetime2 NOT NULL,
        [LastActivityAt] datetime2 NULL,
        [EndedAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_UserSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_UserSessions_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[SEC_UserTenantAssignments] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TenantId] int NOT NULL,
        [IsPrimary] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SEC_UserTenantAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SEC_UserTenantAssignments_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SEC_UserTenantAssignments_SEC_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ACC_AccountBalances_AccountId_PeriodYear_PeriodMonth_BranchId_CostCenterId] ON [dbo].[ACC_AccountBalances] ([AccountId], [PeriodYear], [PeriodMonth], [BranchId], [CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountBalances_BranchId] ON [dbo].[ACC_AccountBalances] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountBalances_CostCenterId] ON [dbo].[ACC_AccountBalances] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ACC_AccountBalances_PublicId] ON [dbo].[ACC_AccountBalances] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountGroups_ParentGroupId] ON [dbo].[ACC_AccountGroups] ([ParentGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountGroups_ParentGroupId1] ON [dbo].[ACC_AccountGroups] ([ParentGroupId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_AccountGroups_PublicId] ON [dbo].[ACC_AccountGroups] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_AccountingPeriods_Natural] ON [dbo].[ACC_AccountingPeriods] ([ModuleCode], [Year], [PeriodNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_AccountingPeriods_PublicId] ON [dbo].[ACC_AccountingPeriods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountSubgroups_GroupId] ON [dbo].[ACC_AccountSubgroups] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountSubgroups_GroupId1] ON [dbo].[ACC_AccountSubgroups] ([GroupId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountSubgroups_ParentSubgroupId] ON [dbo].[ACC_AccountSubgroups] ([ParentSubgroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AccountSubgroups_ParentSubgroupId1] ON [dbo].[ACC_AccountSubgroups] ([ParentSubgroupId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_AccountSubgroups_PublicId] ON [dbo].[ACC_AccountSubgroups] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Amortizations_AccountId] ON [dbo].[ACC_Amortizations] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Amortizations_BranchId] ON [dbo].[ACC_Amortizations] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Amortizations_CostCenterId] ON [dbo].[ACC_Amortizations] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_ACC_Amortizations_Natural] ON [dbo].[ACC_Amortizations] ([PeriodYear], [AccountId], [BranchId], [CostCenterId], [PersonId], [DocumentCode]) WHERE [PersonId] IS NOT NULL AND [DocumentCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_Amortizations_PublicId] ON [dbo].[ACC_Amortizations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AuxiliaryDocuments_AccountId] ON [dbo].[ACC_AuxiliaryDocuments] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AuxiliaryDocuments_BranchId] ON [dbo].[ACC_AuxiliaryDocuments] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AuxiliaryDocuments_CostCenterId] ON [dbo].[ACC_AuxiliaryDocuments] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_AuxiliaryDocuments_PersonId] ON [dbo].[ACC_AuxiliaryDocuments] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_AuxiliaryDocuments_PublicId] ON [dbo].[ACC_AuxiliaryDocuments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_BankReconciliationFlats_PublicId] ON [dbo].[ACC_BankReconciliationFlats] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_BankReconciliationMasters_BankId] ON [dbo].[ACC_BankReconciliationMasters] ([BankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_BankReconciliationMasters_Natural] ON [dbo].[ACC_BankReconciliationMasters] ([AccountId], [BankId], [PeriodCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_BankReconciliationMasters_PublicId] ON [dbo].[ACC_BankReconciliationMasters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_BankReconciliations_AccountId] ON [dbo].[ACC_BankReconciliations] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_BankReconciliations_BankId] ON [dbo].[ACC_BankReconciliations] ([BankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_BankReconciliations_PublicId] ON [dbo].[ACC_BankReconciliations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Budgets_AccountId] ON [dbo].[ACC_Budgets] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Budgets_BranchId] ON [dbo].[ACC_Budgets] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Budgets_CostCenterId] ON [dbo].[ACC_Budgets] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_Budgets_Natural] ON [dbo].[ACC_Budgets] ([PeriodYear], [AccountId], [BranchId], [CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_Budgets_PublicId] ON [dbo].[ACC_Budgets] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ACC_ChartOfAccounts_AccountCode] ON [dbo].[ACC_ChartOfAccounts] ([AccountCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_ChartOfAccounts_LegacyCode] ON [dbo].[ACC_ChartOfAccounts] ([LegacyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ACC_ChartOfAccounts_PublicId] ON [dbo].[ACC_ChartOfAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Depreciations_AccountId] ON [dbo].[ACC_Depreciations] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Depreciations_BranchId] ON [dbo].[ACC_Depreciations] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Depreciations_CostCenterId] ON [dbo].[ACC_Depreciations] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_ACC_Depreciations_Natural] ON [dbo].[ACC_Depreciations] ([PeriodYear], [AccountId], [BranchId], [CostCenterId], [PersonId]) WHERE [PersonId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_Depreciations_PublicId] ON [dbo].[ACC_Depreciations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_DianReportFormats_Natural] ON [dbo].[ACC_DianReportFormats] ([FormatId], [ConceptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_DianReportFormats_PublicId] ON [dbo].[ACC_DianReportFormats] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Documents_VoucherTypeCode] ON [dbo].[ACC_Documents] ([VoucherTypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_Documents_VoucherTypeId] ON [dbo].[ACC_Documents] ([VoucherTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_Documents_PublicId] ON [dbo].[ACC_Documents] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_Documents_VoucherDoc] ON [dbo].[ACC_Documents] ([VoucherTypeCode], [DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_ExchangeRateHistory_PublicId] ON [dbo].[ACC_ExchangeRateHistory] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FinancialReportParams_Natural] ON [dbo].[ACC_FinancialReportParams] ([FormatId], [ConceptId], [ParameterOption], [ParameterValue]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FinancialReportParams_PublicId] ON [dbo].[ACC_FinancialReportParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_FinancialReports_AccountId] ON [dbo].[ACC_FinancialReports] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_FinancialReports_PersonId] ON [dbo].[ACC_FinancialReports] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FinancialReports_Natural] ON [dbo].[ACC_FinancialReports] ([FormatId], [ConceptId], [PersonId], [AccountId], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FinancialReports_PublicId] ON [dbo].[ACC_FinancialReports] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FinancialReportValues_Natural] ON [dbo].[ACC_FinancialReportValues] ([FormatId], [ValueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FinancialReportValues_PublicId] ON [dbo].[ACC_FinancialReportValues] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FiscalPeriods_Natural] ON [dbo].[ACC_FiscalPeriods] ([PeriodYear], [PeriodMonth]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_FiscalPeriods_PublicId] ON [dbo].[ACC_FiscalPeriods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_GmfTaxLines_LineCode] ON [dbo].[ACC_GmfTaxLines] ([LineCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_GmfTaxLines_PublicId] ON [dbo].[ACC_GmfTaxLines] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_GroupNames_PublicId] ON [dbo].[ACC_GroupNames] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_IcaTaxLines_LineCode] ON [dbo].[ACC_IcaTaxLines] ([LineCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_IcaTaxLines_PublicId] ON [dbo].[ACC_IcaTaxLines] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_IncomeTaxLines_LineCode] ON [dbo].[ACC_IncomeTaxLines] ([LineCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_IncomeTaxLines_PublicId] ON [dbo].[ACC_IncomeTaxLines] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntries_AccountId] ON [dbo].[ACC_JournalEntries] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntries_BranchId] ON [dbo].[ACC_JournalEntries] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntries_CostCenterId] ON [dbo].[ACC_JournalEntries] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntries_PersonId] ON [dbo].[ACC_JournalEntries] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ACC_JournalEntries_PublicId] ON [dbo].[ACC_JournalEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntries_TransactionDate] ON [dbo].[ACC_JournalEntries] ([TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntries_VoucherTypeCode_DocumentNumber] ON [dbo].[ACC_JournalEntries] ([VoucherTypeCode], [DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_JournalEntryItems_JournalEntryId] ON [dbo].[ACC_JournalEntryItems] ([JournalEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_JournalEntryItems_PublicId] ON [dbo].[ACC_JournalEntryItems] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_RiskCategories_Natural] ON [dbo].[ACC_RiskCategories] ([AccountCode], [PeriodCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_RiskCategories_PublicId] ON [dbo].[ACC_RiskCategories] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_StampTaxes_Grade] ON [dbo].[ACC_StampTaxes] ([Grade]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_StampTaxes_PublicId] ON [dbo].[ACC_StampTaxes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_SubgroupNames_PublicId] ON [dbo].[ACC_SubgroupNames] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_TaxFormCodes_PublicId] ON [dbo].[ACC_TaxFormCodes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_ThirdPartyAccounts_AccountId] ON [dbo].[ACC_ThirdPartyAccounts] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_ThirdPartyAccounts_BranchId] ON [dbo].[ACC_ThirdPartyAccounts] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_ThirdPartyAccounts_CostCenterId] ON [dbo].[ACC_ThirdPartyAccounts] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ACC_ThirdPartyAccounts_PersonId] ON [dbo].[ACC_ThirdPartyAccounts] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_ThirdPartyAccounts_Natural] ON [dbo].[ACC_ThirdPartyAccounts] ([PeriodYear], [AccountId], [PersonId], [BranchId], [CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_ThirdPartyAccounts_PublicId] ON [dbo].[ACC_ThirdPartyAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_VatTaxLines_LineCode] ON [dbo].[ACC_VatTaxLines] ([LineCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_VatTaxLines_PublicId] ON [dbo].[ACC_VatTaxLines] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_VoucherTypes_Code] ON [dbo].[ACC_VoucherTypes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_VoucherTypes_PublicId] ON [dbo].[ACC_VoucherTypes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_WithholdingTaxLines_LineCode] ON [dbo].[ACC_WithholdingTaxLines] ([LineCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ACC_WithholdingTaxLines_PublicId] ON [dbo].[ACC_WithholdingTaxLines] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Branches_PublicId] ON [dbo].[ADM_Branches] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Branches_TenantId_Code] ON [dbo].[ADM_Branches] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ADM_Branches_TenantId_IsHeadquarters] ON [dbo].[ADM_Branches] ([TenantId], [IsHeadquarters]) WHERE [IsHeadquarters] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_LoginAttempts_Email_Timestamp] ON [dbo].[ADM_CentralUserLoginAttempts] ([NormalizedEmail], [Timestamp] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_LoginAttempts_Ip_Timestamp] ON [dbo].[ADM_CentralUserLoginAttempts] ([IpAddress], [Timestamp] DESC) WHERE [IpAddress] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_Invitations_Email_Tenant_Status] ON [dbo].[ADM_Invitations] ([NormalizedEmail], [TenantId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Invitations_PublicId] ON [dbo].[ADM_Invitations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_Invitations_Status_ExpiresAt] ON [dbo].[ADM_Invitations] ([Status], [ExpiresAt]) WHERE [Status] = 0 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_Invitations_Tenant_Status] ON [dbo].[ADM_Invitations] ([TenantId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ADM_Invitations_TokenHash] ON [dbo].[ADM_Invitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_PasswordResetTokens_CentralUserId_ExpiresAt] ON [dbo].[ADM_PasswordResetTokens] ([CentralUserId], [ExpiresAt]) WHERE [ConsumedAt] IS NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_PasswordResetTokens_PublicId] ON [dbo].[ADM_PasswordResetTokens] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_PasswordResetTokens_TokenHash] ON [dbo].[ADM_PasswordResetTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Subscriptions_PublicId] ON [dbo].[ADM_Subscriptions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_Subscriptions_TenantId] ON [dbo].[ADM_Subscriptions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_TenantMemberships_CentralUser_Status] ON [dbo].[ADM_TenantMemberships] ([CentralUserId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantMemberships_PublicId] ON [dbo].[ADM_TenantMemberships] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_TenantMemberships_Tenant_ActiveAdmins] ON [dbo].[ADM_TenantMemberships] ([TenantId]) WHERE [IsTenantAdmin] = 1 AND [Status] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_TenantMemberships_Tenant_Status] ON [dbo].[ADM_TenantMemberships] ([TenantId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ADM_TenantMemberships_CentralUser_Tenant] ON [dbo].[ADM_TenantMemberships] ([CentralUserId], [TenantId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantMfaPolicies_PublicId] ON [dbo].[ADM_TenantMfaPolicies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ADM_TenantMfaPolicies_TenantId] ON [dbo].[ADM_TenantMfaPolicies] ([TenantId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ADM_Tenants_Nit] ON [dbo].[ADM_Tenants] ([Nit]) WHERE [Nit] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Tenants_PublicId] ON [dbo].[ADM_Tenants] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Tenants_SchemaName] ON [dbo].[ADM_Tenants] ([SchemaName]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ADM_Tenants_Subdomain] ON [dbo].[ADM_Tenants] ([Subdomain]) WHERE [Subdomain] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantSettings_PublicId] ON [dbo].[ADM_TenantSettings] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantSettings_TenantId_SettingKey] ON [dbo].[ADM_TenantSettings] ([TenantId], [SettingKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_AccountChanges_PublicId] ON [dbo].[AUD_AccountChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_AssignmentChanges_PublicId] ON [dbo].[AUD_AssignmentChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_AuditReferences_PublicId] ON [dbo].[AUD_AuditReferences] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_CompanyChanges_PublicId] ON [dbo].[AUD_CompanyChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_DefaultChanges_PublicId] ON [dbo].[AUD_DefaultChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_JournalChanges_PublicId] ON [dbo].[AUD_JournalChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_MasterChanges_PublicId] ON [dbo].[AUD_MasterChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_MenuChanges_PublicId] ON [dbo].[AUD_MenuChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_PeriodChanges_PublicId] ON [dbo].[AUD_PeriodChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_PortfolioMasterChanges_PublicId] ON [dbo].[AUD_PortfolioMasterChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_PortfolioTransactionChanges_PublicId] ON [dbo].[AUD_PortfolioTransactionChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_SavingsChanges_PublicId] ON [dbo].[AUD_SavingsChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_UserChanges_PublicId] ON [dbo].[AUD_UserChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AUD_VoucherTypeChanges_PublicId] ON [dbo].[AUD_VoucherTypeChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CDT_AssociateReferences_CertificateId] ON [dbo].[CDT_AssociateReferences] ([CertificateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_AssociateReferences_PublicId] ON [dbo].[CDT_AssociateReferences] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_Audit_PublicId] ON [dbo].[CDT_Audit] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CDT_CertificateEntries_CertificateId] ON [dbo].[CDT_CertificateEntries] ([CertificateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CDT_CertificateEntries_CertificateId1] ON [dbo].[CDT_CertificateEntries] ([CertificateId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_CertificateEntries_PublicId] ON [dbo].[CDT_CertificateEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_Certificates_CertificateNumber] ON [dbo].[CDT_Certificates] ([CertificateNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_Certificates_PublicId] ON [dbo].[CDT_Certificates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_ParameterAudit_PublicId] ON [dbo].[CDT_ParameterAudit] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_Parameters_CreditLineId] ON [dbo].[CDT_Parameters] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_Parameters_PublicId] ON [dbo].[CDT_Parameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_RatesByTerm_CreditLineId_AmountRangeStart_AmountRangeEnd_TermStart_TermEnd] ON [dbo].[CDT_RatesByTerm] ([CreditLineId], [AmountRangeStart], [AmountRangeEnd], [TermStart], [TermEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CDT_RatesByTerm_PublicId] ON [dbo].[CDT_RatesByTerm] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CMP_HabeasConsents_History] ON [dbo].[CMP_HabeasDataConsents] ([TenantId], [PersonId], [ActionAt]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CMP_HabeasDataConsents_PolicyVersionId] ON [dbo].[CMP_HabeasDataConsents] ([PolicyVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_CMP_HabeasConsents_PublicId] ON [dbo].[CMP_HabeasDataConsents] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_CMP_HabeasPolicyVersions_Current] ON [dbo].[CMP_HabeasDataPolicyVersions] ([TenantId]) WHERE [EffectiveTo] IS NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_CMP_HabeasPolicyVersions_PublicId] ON [dbo].[CMP_HabeasDataPolicyVersions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_CMP_HabeasPolicyVersions_TenantVersion] ON [dbo].[CMP_HabeasDataPolicyVersions] ([TenantId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_ActivityPrograms_PublicId] ON [dbo].[COR_ActivityPrograms] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Advisors_PublicId] ON [dbo].[COR_Advisors] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Agreements_PublicId] ON [dbo].[COR_Agreements] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_COR_AssociateCategories_PersonId] ON [dbo].[COR_AssociateCategories] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_AssociateCategories_PublicId] ON [dbo].[COR_AssociateCategories] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_AdvisorId] ON [dbo].[COR_Associates] ([AdvisorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_BranchId] ON [dbo].[COR_Associates] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_CommitteeId] ON [dbo].[COR_Associates] ([CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_CostCenterId] ON [dbo].[COR_Associates] ([CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_EmployerCompanyId] ON [dbo].[COR_Associates] ([EmployerCompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_SectionId] ON [dbo].[COR_Associates] ([SectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_Status] ON [dbo].[COR_Associates] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Associates_WithdrawalReasonId] ON [dbo].[COR_Associates] ([WithdrawalReasonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Associates_PersonId] ON [dbo].[COR_Associates] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Associates_PublicId] ON [dbo].[COR_Associates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Attachments_Owner] ON [dbo].[COR_Attachments] ([TenantId], [OwnerEntityType], [OwnerEntityPublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Attachments_PublicId] ON [dbo].[COR_Attachments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Banks_PublicId] ON [dbo].[COR_Banks] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Beneficiaries_CityId] ON [dbo].[COR_Beneficiaries] ([CityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Beneficiaries_PersonId] ON [dbo].[COR_Beneficiaries] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Beneficiaries_RelationshipId] ON [dbo].[COR_Beneficiaries] ([RelationshipId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Beneficiaries_PublicId] ON [dbo].[COR_Beneficiaries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Branches_PublicId] ON [dbo].[COR_Branches] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Cities_DepartmentId] ON [dbo].[COR_Cities] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Cities_PublicId] ON [dbo].[COR_Cities] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_CommitteeMembers_CommitteeId] ON [dbo].[COR_CommitteeMembers] ([CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_CommitteeMembers_PersonId] ON [dbo].[COR_CommitteeMembers] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_CommitteeMembers_Person_Committee] ON [dbo].[COR_CommitteeMembers] ([PersonId], [CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_CommitteeMembers_PublicId] ON [dbo].[COR_CommitteeMembers] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Committees_PublicId] ON [dbo].[COR_Committees] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Companies_PublicId] ON [dbo].[COR_Companies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_CostCenters_PublicId] ON [dbo].[COR_CostCenters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Countries_PublicId] ON [dbo].[COR_Countries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Courses_ActivityProgramId] ON [dbo].[COR_Courses] ([ActivityProgramId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Courses_CommitteeId] ON [dbo].[COR_Courses] ([CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Courses_TeachingEntityId] ON [dbo].[COR_Courses] ([TeachingEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Courses_PublicId] ON [dbo].[COR_Courses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_CulturalActivities_CommitteeId] ON [dbo].[COR_CulturalActivities] ([CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_CulturalActivities_PublicId] ON [dbo].[COR_CulturalActivities] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Departments_CountryId] ON [dbo].[COR_Departments] ([CountryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Departments_CountryId_Code] ON [dbo].[COR_Departments] ([CountryId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Departments_PublicId] ON [dbo].[COR_Departments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Diseases_PublicId] ON [dbo].[COR_Diseases] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_EmployerCompanies_PublicId] ON [dbo].[COR_EmployerCompanies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Entities_PublicId] ON [dbo].[COR_Entities] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_InvoiceParameters_PublicId] ON [dbo].[COR_InvoiceParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_LegalAdvisors_PublicId] ON [dbo].[COR_LegalAdvisors] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_ListParameters_PublicId] ON [dbo].[COR_ListParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_NotDeliveryFailures_NotificationId] ON [dbo].[COR_NotificationDeliveryFailures] ([NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_NotDeliveryFailures_PublicId] ON [dbo].[COR_NotificationDeliveryFailures] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_COR_Notifications_EmailStatus] ON [dbo].[COR_Notifications] ([EmailStatus]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_COR_Notifications_Inbox] ON [dbo].[COR_Notifications] ([TenantId], [RecipientUserPublicId], [ReadAt]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Notifications_NotificationTemplateId] ON [dbo].[COR_Notifications] ([NotificationTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Notifications_PublicId] ON [dbo].[COR_Notifications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_NotificationTemplates_Name_Channel] ON [dbo].[COR_NotificationTemplates] ([TemplateName], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_NotificationTemplates_PublicId] ON [dbo].[COR_NotificationTemplates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_PaymentMethodChecks_PublicId] ON [dbo].[COR_PaymentMethodChecks] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_PaymentMethodChecks_Voucher_Doc_Check_Bank] ON [dbo].[COR_PaymentMethodChecks] ([VoucherTypeCode], [DocumentNumber], [CheckNumber], [BankCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_PaymentMethods_PublicId] ON [dbo].[COR_PaymentMethods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_PaymentMethods_Voucher_Doc] ON [dbo].[COR_PaymentMethods] ([VoucherTypeCode], [DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_PensionSeveranceParams_PublicId] ON [dbo].[COR_PensionSeveranceParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_People_CityId] ON [dbo].[COR_People] ([CityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_People_FullName] ON [dbo].[COR_People] ([LastName], [FirstName]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_People_LegacyCode] ON [dbo].[COR_People] ([LegacyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_People_MailingCityId] ON [dbo].[COR_People] ([MailingCityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_People_PublicId] ON [dbo].[COR_People] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_People_TaxId] ON [dbo].[COR_People] ([TaxId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_COR_PeopleFinancial_PersonId] ON [dbo].[COR_PeopleFinancial] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_PeopleFinancial_PublicId] ON [dbo].[COR_PeopleFinancial] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Positions_PublicId] ON [dbo].[COR_Positions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Professions_PublicId] ON [dbo].[COR_Professions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_RecreationalEvents_ActivityProgramId] ON [dbo].[COR_RecreationalEvents] ([ActivityProgramId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_RecreationalEvents_CommitteeId] ON [dbo].[COR_RecreationalEvents] ([CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_RecreationalEvents_PublicId] ON [dbo].[COR_RecreationalEvents] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_References_CityId] ON [dbo].[COR_References] ([CityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_References_PersonId] ON [dbo].[COR_References] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_References_PublicId] ON [dbo].[COR_References] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Relationships_PublicId] ON [dbo].[COR_Relationships] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Sections_PublicId] ON [dbo].[COR_Sections] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Sequences_PublicId] ON [dbo].[COR_Sequences] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_COR_Sports_CommitteeId] ON [dbo].[COR_Sports] ([CommitteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Sports_PublicId] ON [dbo].[COR_Sports] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Spouses_PersonId] ON [dbo].[COR_Spouses] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_Spouses_PublicId] ON [dbo].[COR_Spouses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_SystemSettings_Key] ON [dbo].[COR_SystemSettings] ([SettingKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_SystemSettings_PublicId] ON [dbo].[COR_SystemSettings] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_COR_WithdrawalReasons_PublicId] ON [dbo].[COR_WithdrawalReasons] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_AgreementMembers_PublicId] ON [dbo].[DEB_AgreementMembers] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_AgreementParameters_AgreementCode] ON [dbo].[DEB_AgreementParameters] ([AgreementCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_AgreementParameters_PublicId] ON [dbo].[DEB_AgreementParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_Agreements_PublicId] ON [dbo].[DEB_Agreements] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_Cards_BankId_CardNumber] ON [dbo].[DEB_Cards] ([BankId], [CardNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_Cards_PublicId] ON [dbo].[DEB_Cards] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_DailyParameters_ParameterCode] ON [dbo].[DEB_DailyParameters] ([ParameterCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_DailyParameters_PublicId] ON [dbo].[DEB_DailyParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_PosTerminals_PublicId] ON [dbo].[DEB_PosTerminals] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DEB_Transactions_CardId] ON [dbo].[DEB_Transactions] ([CardId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DEB_Transactions_PublicId] ON [dbo].[DEB_Transactions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_CommissionParameters_PublicId] ON [dbo].[INV_CommissionParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_CommissionPriceParams_PublicId] ON [dbo].[INV_CommissionPriceParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Discounts_DiscountTypeId] ON [dbo].[INV_Discounts] ([DiscountTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Discounts_ProductId] ON [dbo].[INV_Discounts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Discounts_PublicId] ON [dbo].[INV_Discounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_DiscountTypes_PublicId] ON [dbo].[INV_DiscountTypes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_DiscountTypes_TypeCode] ON [dbo].[INV_DiscountTypes] ([TypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Documents_PublicId] ON [dbo].[INV_Documents] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Documents_TransactionTypeId_SequenceNumber] ON [dbo].[INV_Documents] ([TransactionTypeId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Invoices_InvoiceCode] ON [dbo].[INV_Invoices] ([InvoiceCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Invoices_PublicId] ON [dbo].[INV_Invoices] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Locations_LocationCode] ON [dbo].[INV_Locations] ([LocationCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Locations_PublicId] ON [dbo].[INV_Locations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_OrderDocuments_PublicId] ON [dbo].[INV_OrderDocuments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_OrderDocuments_TransactionTypeId_SequenceNumber] ON [dbo].[INV_OrderDocuments] ([TransactionTypeId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_OrderTransactions_ProductId] ON [dbo].[INV_OrderTransactions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_OrderTransactions_PublicId] ON [dbo].[INV_OrderTransactions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_OrderTransactions_TransactionTypeId] ON [dbo].[INV_OrderTransactions] ([TransactionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_PhysicalInventory_ProductId] ON [dbo].[INV_PhysicalInventory] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_PhysicalInventory_PublicId] ON [dbo].[INV_PhysicalInventory] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_PriceListTypes_PublicId] ON [dbo].[INV_PriceListTypes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_PriceListTypes_TypeCode] ON [dbo].[INV_PriceListTypes] ([TypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Prices_PriceListTypeId_ProductId_CustomerType] ON [dbo].[INV_Prices] ([PriceListTypeId], [ProductId], [CustomerType]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Prices_ProductId] ON [dbo].[INV_Prices] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Prices_PublicId] ON [dbo].[INV_Prices] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_PrimaryGroups_GroupCode] ON [dbo].[INV_PrimaryGroups] ([GroupCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_PrimaryGroups_PublicId] ON [dbo].[INV_PrimaryGroups] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_ProductAccounts_ProductGroupId_TransactionTypeId_WarehouseId_LocationId] ON [dbo].[INV_ProductAccounts] ([ProductGroupId], [TransactionTypeId], [WarehouseId], [LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_ProductAccounts_PublicId] ON [dbo].[INV_ProductAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_ProductGroups_GroupCode] ON [dbo].[INV_ProductGroups] ([GroupCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_ProductGroups_PublicId] ON [dbo].[INV_ProductGroups] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_ProductGroups_SecondaryGroupId] ON [dbo].[INV_ProductGroups] ([SecondaryGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Products_DiscountTypeId] ON [dbo].[INV_Products] ([DiscountTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Products_DiscountTypeId1] ON [dbo].[INV_Products] ([DiscountTypeId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Products_GroupId] ON [dbo].[INV_Products] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Products_ProductCode] ON [dbo].[INV_Products] ([ProductCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Products_ProductGroupId] ON [dbo].[INV_Products] ([ProductGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Products_PublicId] ON [dbo].[INV_Products] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_INV_Salespeople_PersonId] ON [dbo].[INV_Salespeople] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_INV_Salespeople_PublicId] ON [dbo].[INV_Salespeople] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_INV_SalesPoints_PointCode_Status_DateId] ON [dbo].[INV_SalesPoints] ([PointCode], [Status], [DateId]) WHERE [DateId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_SalesPoints_PublicId] ON [dbo].[INV_SalesPoints] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_SecondaryGroups_GroupCode] ON [dbo].[INV_SecondaryGroups] ([GroupCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_SecondaryGroups_PrimaryGroupId] ON [dbo].[INV_SecondaryGroups] ([PrimaryGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_SecondaryGroups_PublicId] ON [dbo].[INV_SecondaryGroups] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Shifts_PublicId] ON [dbo].[INV_Shifts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Shifts_ShiftCode] ON [dbo].[INV_Shifts] ([ShiftCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Transactions_ProductId] ON [dbo].[INV_Transactions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Transactions_PublicId] ON [dbo].[INV_Transactions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Transactions_TransactionTypeId] ON [dbo].[INV_Transactions] ([TransactionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_TransactionTypes_PublicId] ON [dbo].[INV_TransactionTypes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_TransactionTypes_TypeCode] ON [dbo].[INV_TransactionTypes] ([TypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_VatAccounts_PublicId] ON [dbo].[INV_VatAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_INV_Warehouses_LocationId] ON [dbo].[INV_Warehouses] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Warehouses_PublicId] ON [dbo].[INV_Warehouses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_INV_Warehouses_WarehouseCode_LocationId] ON [dbo].[INV_Warehouses] ([WarehouseCode], [LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_AccrualEntries_CreditLineId] ON [dbo].[LND_AccrualEntries] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AccrualEntries_PublicId] ON [dbo].[LND_AccrualEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AccrualPeriods_PublicId] ON [dbo].[LND_AccrualPeriods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ActivityEnrollments_PublicId] ON [dbo].[LND_ActivityEnrollments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ApplicationAssets_PublicId] ON [dbo].[LND_ApplicationAssets] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ApplicationCodebtors_PublicId] ON [dbo].[LND_ApplicationCodebtors] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ApplicationExtras_PublicId] ON [dbo].[LND_ApplicationExtras] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ApplicationReferences_PublicId] ON [dbo].[LND_ApplicationReferences] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_ApplicationStatuses_CreditLineId] ON [dbo].[LND_ApplicationStatuses] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ApplicationStatuses_PublicId] ON [dbo].[LND_ApplicationStatuses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AssociateActivities_PublicId] ON [dbo].[LND_AssociateActivities] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AssociateDiseases_PublicId] ON [dbo].[LND_AssociateDiseases] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AssociateWithdrawals_PublicId] ON [dbo].[LND_AssociateWithdrawals] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AuxAppInstallmentBeneficiaries_PublicId] ON [dbo].[LND_AuxAppInstallmentBeneficiaries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AuxAppInstallments_PublicId] ON [dbo].[LND_AuxAppInstallments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AuxiliaryApplicationLines_PublicId] ON [dbo].[LND_AuxiliaryApplicationLines] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_AuxiliaryApplications_PublicId] ON [dbo].[LND_AuxiliaryApplications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_BiometricRecords_Person] ON [dbo].[LND_BiometricRecords] ([PersonCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_BiometricRecords_PublicId] ON [dbo].[LND_BiometricRecords] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Blacklist_PublicId] ON [dbo].[LND_Blacklist] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CashBases_PublicId] ON [dbo].[LND_CashBases] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Cashiers_PublicId] ON [dbo].[LND_Cashiers] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Checkbooks_PublicId] ON [dbo].[LND_Checkbooks] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CheckClearing_PublicId] ON [dbo].[LND_CheckClearing] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionCases_PublicId] ON [dbo].[LND_CollectionCases] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_CollectionDateEntries_CreditLineId] ON [dbo].[LND_CollectionDateEntries] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionDateEntries_PublicId] ON [dbo].[LND_CollectionDateEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_CollectionMasters_CreditLineId] ON [dbo].[LND_CollectionMasters] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionMasters_PublicId] ON [dbo].[LND_CollectionMasters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_CollectionNoticeDetails_CreditLineId] ON [dbo].[LND_CollectionNoticeDetails] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionNoticeDetails_PublicId] ON [dbo].[LND_CollectionNoticeDetails] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionNoticeParams_Code] ON [dbo].[LND_CollectionNoticeParams] ([NoticeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionNoticeParams_PublicId] ON [dbo].[LND_CollectionNoticeParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionNotices_PublicId] ON [dbo].[LND_CollectionNotices] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CollectionPeriods_PublicId] ON [dbo].[LND_CollectionPeriods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ContribRedParams_Period] ON [dbo].[LND_ContributionReductionParams] ([CutoffPeriod]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ContributionReductionParams_PublicId] ON [dbo].[LND_ContributionReductionParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ContributionReductions_PublicId] ON [dbo].[LND_ContributionReductions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_CreditLineAudit_CreditLineId] ON [dbo].[LND_CreditLineAudit] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CreditLineAudit_PublicId] ON [dbo].[LND_CreditLineAudit] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LND_CreditLineParameters_CreditLineId] ON [dbo].[LND_CreditLineParameters] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LND_CreditLineParameters_PublicId] ON [dbo].[LND_CreditLineParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_CreditParameters_CreditLineId] ON [dbo].[LND_CreditParameters] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_CreditParameters_PublicId] ON [dbo].[LND_CreditParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DeductionValues_PublicId] ON [dbo].[LND_DeductionValues] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_DefaultLiquidations_CreditLineId] ON [dbo].[LND_DefaultLiquidations] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DefaultLiquidations_PublicId] ON [dbo].[LND_DefaultLiquidations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_DefaultRecords_CreditLineId] ON [dbo].[LND_DefaultRecords] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_DefaultRecords_LoanPortfolioId] ON [dbo].[LND_DefaultRecords] ([LoanPortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DefaultRecords_PublicId] ON [dbo].[LND_DefaultRecords] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositAccounts_AccountNumber] ON [dbo].[LND_DepositAccounts] ([AccountNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositAccounts_PublicId] ON [dbo].[LND_DepositAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositAudit_PublicId] ON [dbo].[LND_DepositAudit] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositEntries_PublicId] ON [dbo].[LND_DepositEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositSeals_Account] ON [dbo].[LND_DepositSeals] ([AccountNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositSeals_PublicId] ON [dbo].[LND_DepositSeals] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_DepositSignatures_PublicId] ON [dbo].[LND_DepositSignatures] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Documents_PublicId] ON [dbo].[LND_Documents] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_ExtraPaymentBalances_CreditLineId] ON [dbo].[LND_ExtraPaymentBalances] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ExtraPaymentBalances_PublicId] ON [dbo].[LND_ExtraPaymentBalances] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_ExtraPayments_CreditLineId] ON [dbo].[LND_ExtraPayments] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_ExtraPayments_LoanPortfolioId] ON [dbo].[LND_ExtraPayments] ([LoanPortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ExtraPayments_PublicId] ON [dbo].[LND_ExtraPayments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_Guarantees_CreditLineId] ON [dbo].[LND_Guarantees] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_Guarantees_LoanPortfolioId] ON [dbo].[LND_Guarantees] ([LoanPortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Guarantees_PublicId] ON [dbo].[LND_Guarantees] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_HousingApplicationParams_PublicId] ON [dbo].[LND_HousingApplicationParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_HousingAppParams_App] ON [dbo].[LND_HousingApplicationParams] ([ApplicationNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_HousingParameters_CreditLineId] ON [dbo].[LND_HousingParameters] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_HousingParameters_PublicId] ON [dbo].[LND_HousingParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_InsuranceBeneficiaries_CreditLineId] ON [dbo].[LND_InsuranceBeneficiaries] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InsuranceBeneficiaries_PublicId] ON [dbo].[LND_InsuranceBeneficiaries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_InsurancePolicies_CreditLineId] ON [dbo].[LND_InsurancePolicies] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InsurancePolicies_PublicId] ON [dbo].[LND_InsurancePolicies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InterestRates_PublicId] ON [dbo].[LND_InterestRates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InvoiceDetails_Code] ON [dbo].[LND_InvoiceDetails] ([DetailCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InvoiceDetails_PublicId] ON [dbo].[LND_InvoiceDetails] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_InvoiceLineItems_CreditLineId] ON [dbo].[LND_InvoiceLineItems] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InvoiceLineItems_PublicId] ON [dbo].[LND_InvoiceLineItems] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_InvoiceMasters_PublicId] ON [dbo].[LND_InvoiceMasters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_LoanApplications_CreditLineId] ON [dbo].[LND_LoanApplications] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_LoanApplications_Number] ON [dbo].[LND_LoanApplications] ([ApplicationNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_LoanApplications_PublicId] ON [dbo].[LND_LoanApplications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_LoanPortfolios_CreditLineId] ON [dbo].[LND_LoanPortfolios] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_LoanPortfolios_PersonId] ON [dbo].[LND_LoanPortfolios] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LND_LoanPortfolios_PersonId_CreditLineId_PortfolioNumber] ON [dbo].[LND_LoanPortfolios] ([PersonId], [CreditLineId], [PortfolioNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LND_LoanPortfolios_PublicId] ON [dbo].[LND_LoanPortfolios] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_LoanRestructurings_CreditLineId] ON [dbo].[LND_LoanRestructurings] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_LoanRestructurings_PublicId] ON [dbo].[LND_LoanRestructurings] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_MinuteAttendees_PublicId] ON [dbo].[LND_MinuteAttendees] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Minutes_PublicId] ON [dbo].[LND_Minutes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_MoneyLaunderingDeclarations_PublicId] ON [dbo].[LND_MoneyLaunderingDeclarations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_OnlineQueries_CreditLineId] ON [dbo].[LND_OnlineQueries] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_OnlineQueries_PublicId] ON [dbo].[LND_OnlineQueries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PayrollDeductionConcepts_CreditLineId] ON [dbo].[LND_PayrollDeductionConcepts] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PayrollDeductionConcepts_PublicId] ON [dbo].[LND_PayrollDeductionConcepts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PayrollDeductionEntries_PublicId] ON [dbo].[LND_PayrollDeductionEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PayrollDeductionPeriods_PublicId] ON [dbo].[LND_PayrollDeductionPeriods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PayrollDeductions_CreditLineId] ON [dbo].[LND_PayrollDeductions] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PayrollDeductions_PublicId] ON [dbo].[LND_PayrollDeductions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PendingInstallments_CreditLineId] ON [dbo].[LND_PendingInstallments] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PendingInstallments_LoanPortfolioId] ON [dbo].[LND_PendingInstallments] ([LoanPortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PendingInstallments_PublicId] ON [dbo].[LND_PendingInstallments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PeriodicityParameters_PublicId] ON [dbo].[LND_PeriodicityParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PersonAssets_PublicId] ON [dbo].[LND_PersonAssets] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PortfolioAccounts_PublicId] ON [dbo].[LND_PortfolioAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PortfolioBalances_CreditLineId] ON [dbo].[LND_PortfolioBalances] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PortfolioBalances_PublicId] ON [dbo].[LND_PortfolioBalances] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PortfolioClassifications_PublicId] ON [dbo].[LND_PortfolioClassifications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PortfolioInvoices_CreditLineId] ON [dbo].[LND_PortfolioInvoices] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PortfolioInvoices_PublicId] ON [dbo].[LND_PortfolioInvoices] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_PreviousInstallments_CreditLineId] ON [dbo].[LND_PreviousInstallments] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_PreviousInstallments_PublicId] ON [dbo].[LND_PreviousInstallments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ProvisionParameters_PublicId] ON [dbo].[LND_ProvisionParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_RatesByTerm_CreditLineId] ON [dbo].[LND_RatesByTerm] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_RatesByTerm_PublicId] ON [dbo].[LND_RatesByTerm] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_RecreationApplications_CreditLineId] ON [dbo].[LND_RecreationApplications] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_RecreationApplications_PublicId] ON [dbo].[LND_RecreationApplications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_RiskAssessments_CreditLineId] ON [dbo].[LND_RiskAssessments] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_RiskAssessments_PublicId] ON [dbo].[LND_RiskAssessments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SavingsAccounts_AccountNumber] ON [dbo].[LND_SavingsAccounts] ([AccountNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SavingsAccounts_PublicId] ON [dbo].[LND_SavingsAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SavingsParameters_LineId] ON [dbo].[LND_SavingsParameters] ([SavingsLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SavingsParameters_PublicId] ON [dbo].[LND_SavingsParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ScoringParameters_PublicId] ON [dbo].[LND_ScoringParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ScoringRanges_PublicId] ON [dbo].[LND_ScoringRanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_ShortLongTermPortfolio_CreditLineId] ON [dbo].[LND_ShortLongTermPortfolio] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ShortLongTermPortfolio_PublicId] ON [dbo].[LND_ShortLongTermPortfolio] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_SiplaGroupParameters_CreditLineId] ON [dbo].[LND_SiplaGroupParameters] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SiplaGroupParameters_PublicId] ON [dbo].[LND_SiplaGroupParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SiplaParameters_PublicId] ON [dbo].[LND_SiplaParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Subsidies_Code] ON [dbo].[LND_Subsidies] ([SubsidyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Subsidies_PublicId] ON [dbo].[LND_Subsidies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SubZones_Code] ON [dbo].[LND_SubZones] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_SubZones_PublicId] ON [dbo].[LND_SubZones] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_TermRates_CreditLineId] ON [dbo].[LND_TermRates] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_TermRates_PublicId] ON [dbo].[LND_TermRates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_TransactionCodes_Code] ON [dbo].[LND_TransactionCodes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_TransactionCodes_PublicId] ON [dbo].[LND_TransactionCodes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_Transactions_CreditLineId] ON [dbo].[LND_Transactions] ([CreditLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_Transactions_LoanPortfolioId] ON [dbo].[LND_Transactions] ([LoanPortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_Transactions_PersonLine] ON [dbo].[LND_Transactions] ([PersonCode], [CreditLineId], [PortfolioNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LND_Transactions_TransactionDate] ON [dbo].[LND_Transactions] ([TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Transactions_PublicId] ON [dbo].[LND_Transactions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_UnusualTransactionEntries_PublicId] ON [dbo].[LND_UnusualTransactionEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_UnusualTransactions_PublicId] ON [dbo].[LND_UnusualTransactions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_WithdrawalStatuses_PublicId] ON [dbo].[LND_WithdrawalStatuses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_Zones_PublicId] ON [dbo].[LND_Zones] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ZoneTypes_PublicId] ON [dbo].[LND_ZoneTypes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_LND_ZoneTypes_TypeId] ON [dbo].[LND_ZoneTypes] ([ZoneTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_Absences_ConceptId] ON [dbo].[PAY_Absences] ([ConceptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_Absences_EmployeeId] ON [dbo].[PAY_Absences] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_Absences_PayrollCompanyId_EmployeeId_ConceptId_SequenceNumber] ON [dbo].[PAY_Absences] ([PayrollCompanyId], [EmployeeId], [ConceptId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_Absences_PublicId] ON [dbo].[PAY_Absences] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_AccountingEntries_EmployeeId] ON [dbo].[PAY_AccountingEntries] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_AccountingEntries_PublicId] ON [dbo].[PAY_AccountingEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_AutoContributionParams_PublicId] ON [dbo].[PAY_AutoContributionParams] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_BookBalances_EmployeeId] ON [dbo].[PAY_BookBalances] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_BookBalances_PayrollCompanyId_EmployeeId_ConceptId_SequenceNumber_PeriodYear] ON [dbo].[PAY_BookBalances] ([PayrollCompanyId], [EmployeeId], [ConceptId], [SequenceNumber], [PeriodYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_BookBalances_PublicId] ON [dbo].[PAY_BookBalances] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_ConceptAccounts_ConceptId_CostCenterId] ON [dbo].[PAY_ConceptAccounts] ([ConceptId], [CostCenterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_ConceptAccounts_PublicId] ON [dbo].[PAY_ConceptAccounts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_DirectDebits_ConceptId] ON [dbo].[PAY_DirectDebits] ([ConceptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_DirectDebits_EmployeeId] ON [dbo].[PAY_DirectDebits] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_DirectDebits_PayrollCompanyId_EmployeeId_ConceptId_SequenceNumber] ON [dbo].[PAY_DirectDebits] ([PayrollCompanyId], [EmployeeId], [ConceptId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_DirectDebits_PublicId] ON [dbo].[PAY_DirectDebits] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_EmployeeLiquidationDetails_EmployeeId] ON [dbo].[PAY_EmployeeLiquidationDetails] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_EmployeeLiquidationDetails_PublicId] ON [dbo].[PAY_EmployeeLiquidationDetails] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_EmployeeLiquidationMasters_EmployeeId] ON [dbo].[PAY_EmployeeLiquidationMasters] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_EmployeeLiquidationMasters_PublicId] ON [dbo].[PAY_EmployeeLiquidationMasters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_Employees_PayrollCompanyId] ON [dbo].[PAY_Employees] ([PayrollCompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_Employees_Status] ON [dbo].[PAY_Employees] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_PAY_Employees_PersonId] ON [dbo].[PAY_Employees] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UK_PAY_Employees_PublicId] ON [dbo].[PAY_Employees] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_HealthInsuranceProviders_Code] ON [dbo].[PAY_HealthInsuranceProviders] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_HealthInsuranceProviders_PublicId] ON [dbo].[PAY_HealthInsuranceProviders] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayPeriods_PlanId_PayrollCompanyId] ON [dbo].[PAY_PayPeriods] ([PlanId], [PayrollCompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayPeriods_PublicId] ON [dbo].[PAY_PayPeriods] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollConcepts_ConceptCode] ON [dbo].[PAY_PayrollConcepts] ([ConceptCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollConcepts_PublicId] ON [dbo].[PAY_PayrollConcepts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollEntries_Cycle_PayrollCompanyId_EmployeeId] ON [dbo].[PAY_PayrollEntries] ([Cycle], [PayrollCompanyId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_PayrollEntries_EmployeeId] ON [dbo].[PAY_PayrollEntries] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollEntries_PublicId] ON [dbo].[PAY_PayrollEntries] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_PayrollPlanLiquidations_EmployeeId] ON [dbo].[PAY_PayrollPlanLiquidations] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollPlanLiquidations_PayPeriodId_PayrollCompanyId_EmployeeId_ConceptId_SequenceNumber] ON [dbo].[PAY_PayrollPlanLiquidations] ([PayPeriodId], [PayrollCompanyId], [EmployeeId], [ConceptId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollPlanLiquidations_PublicId] ON [dbo].[PAY_PayrollPlanLiquidations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_PayrollTransactions_ConceptId] ON [dbo].[PAY_PayrollTransactions] ([ConceptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_PayrollTransactions_EmployeeId] ON [dbo].[PAY_PayrollTransactions] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollTransactions_PayPeriodId_PayrollCompanyId_EmployeeId_ConceptId_SequenceNumber] ON [dbo].[PAY_PayrollTransactions] ([PayPeriodId], [PayrollCompanyId], [EmployeeId], [ConceptId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PayrollTransactions_PublicId] ON [dbo].[PAY_PayrollTransactions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PensionProviders_Code] ON [dbo].[PAY_PensionProviders] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PensionProviders_PublicId] ON [dbo].[PAY_PensionProviders] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PreLiquidationResponses_PublicId] ON [dbo].[PAY_PreLiquidationResponses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_PreLiquidations_EmployeeId] ON [dbo].[PAY_PreLiquidations] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_PreLiquidations_PublicId] ON [dbo].[PAY_PreLiquidations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_SalaryChanges_EmployeeId] ON [dbo].[PAY_SalaryChanges] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_SalaryChanges_PublicId] ON [dbo].[PAY_SalaryChanges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_SeveranceHistory_EmployeeId] ON [dbo].[PAY_SeveranceHistory] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_SeveranceHistory_PublicId] ON [dbo].[PAY_SeveranceHistory] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_SeveranceProviders_Code] ON [dbo].[PAY_SeveranceProviders] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_SeveranceProviders_PublicId] ON [dbo].[PAY_SeveranceProviders] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_TaxCertificates_EmployeeId] ON [dbo].[PAY_TaxCertificates] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_TaxCertificates_PublicId] ON [dbo].[PAY_TaxCertificates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PAY_VacationLiquidations_EmployeeId] ON [dbo].[PAY_VacationLiquidations] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_VacationLiquidations_PublicId] ON [dbo].[PAY_VacationLiquidations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WithholdingCauses_Code] ON [dbo].[PAY_WithholdingCauses] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WithholdingCauses_PublicId] ON [dbo].[PAY_WithholdingCauses] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WithholdingParameters_PayrollCompanyId_UvtRangeStart_UvtRangeEnd] ON [dbo].[PAY_WithholdingParameters] ([PayrollCompanyId], [UvtRangeStart], [UvtRangeEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WithholdingParameters_PublicId] ON [dbo].[PAY_WithholdingParameters] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WorkRiskProviders_Code] ON [dbo].[PAY_WorkRiskProviders] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WorkRiskProviders_PublicId] ON [dbo].[PAY_WorkRiskProviders] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WorkRiskRates_Code] ON [dbo].[PAY_WorkRiskRates] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PAY_WorkRiskRates_PublicId] ON [dbo].[PAY_WorkRiskRates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_LoginAttempts_PublicId] ON [dbo].[SEC_LoginAttempts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_MfaBackupCodes_PublicId] ON [dbo].[SEC_MfaBackupCodes] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_MfaBackupCodes_User_Batch] ON [dbo].[SEC_MfaBackupCodes] ([UserId], [BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_MfaResetRequests_PublicId] ON [dbo].[SEC_MfaResetRequests] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_MfaResetRequests_User_Status] ON [dbo].[SEC_MfaResetRequests] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_Modules_PublicId] ON [dbo].[SEC_Modules] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SEC_Modules_UserId_ProgramCode] ON [dbo].[SEC_Modules] ([UserId], [ProgramCode]) WHERE [UserId] IS NOT NULL AND [ProgramCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_PasswordHistory_User_SetAt] ON [dbo].[SEC_PasswordHistory] ([UserId], [SetAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_PasswordPolicies_PublicId] ON [dbo].[SEC_PasswordPolicies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_SEC_PasswordPolicies_TenantId_NotDeleted] ON [dbo].[SEC_PasswordPolicies] ([TenantId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_Permissions_PublicId] ON [dbo].[SEC_Permissions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_Permissions_Resource_Action] ON [dbo].[SEC_Permissions] ([Resource], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_RefreshTokens_FamilyId] ON [dbo].[SEC_RefreshTokens] ([FamilyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_RefreshTokens_PublicId] ON [dbo].[SEC_RefreshTokens] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_RefreshTokens_TokenHash] ON [dbo].[SEC_RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_RefreshTokens_UserId] ON [dbo].[SEC_RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_RolePermissions_PermissionId] ON [dbo].[SEC_RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_RolePermissions_PublicId] ON [dbo].[SEC_RolePermissions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_RolePermissions_RoleId_PermissionId] ON [dbo].[SEC_RolePermissions] ([RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_Roles_PublicId] ON [dbo].[SEC_Roles] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_SEC_Roles_Tenant_Code_NotDeleted] ON [dbo].[SEC_Roles] ([TenantId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserAssignments_PublicId] ON [dbo].[SEC_UserAssignments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserAssignments_UserId] ON [dbo].[SEC_UserAssignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SEC_UserAssignments_VoucherTypeCode_UserId] ON [dbo].[SEC_UserAssignments] ([VoucherTypeCode], [UserId]) WHERE [VoucherTypeCode] IS NOT NULL AND [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserBranchAssignments_BranchId] ON [dbo].[SEC_UserBranchAssignments] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserBranchAssignments_PublicId] ON [dbo].[SEC_UserBranchAssignments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserBranchAssignments_TenantId] ON [dbo].[SEC_UserBranchAssignments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserBranchAssignments_UserId_TenantId_BranchId] ON [dbo].[SEC_UserBranchAssignments] ([UserId], [TenantId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SEC_UserBranchAssignments_UserId_TenantId_IsDefault] ON [dbo].[SEC_UserBranchAssignments] ([UserId], [TenantId], [IsDefault]) WHERE [IsDefault] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserMenuAccess_PublicId] ON [dbo].[SEC_UserMenuAccess] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserMenuAccess_UserId] ON [dbo].[SEC_UserMenuAccess] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserRoles_RoleId] ON [dbo].[SEC_UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_Users_PersonId] ON [dbo].[SEC_Users] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_Users_PublicId] ON [dbo].[SEC_Users] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_Users_Username] ON [dbo].[SEC_Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserSessions_PublicId] ON [dbo].[SEC_UserSessions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserSessions_UserId] ON [dbo].[SEC_UserSessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserTenantAssignments_PublicId] ON [dbo].[SEC_UserTenantAssignments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SEC_UserTenantAssignments_TenantId] ON [dbo].[SEC_UserTenantAssignments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SEC_UserTenantAssignments_UserId_IsPrimary] ON [dbo].[SEC_UserTenantAssignments] ([UserId], [IsPrimary]) WHERE [IsPrimary] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SEC_UserTenantAssignments_UserId_TenantId] ON [dbo].[SEC_UserTenantAssignments] ([UserId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TRS_Checks_ConceptCode_BankId_SequentialNumber] ON [dbo].[TRS_Checks] ([ConceptCode], [BankId], [SequentialNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TRS_Checks_PublicId] ON [dbo].[TRS_Checks] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TRS_Concepts_ConceptCode] ON [dbo].[TRS_Concepts] ([ConceptCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TRS_Concepts_PublicId] ON [dbo].[TRS_Concepts] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TRS_Invoices_InvoiceNumber_PersonId_AccountCode] ON [dbo].[TRS_Invoices] ([InvoiceNumber], [PersonId], [AccountCode]) WHERE [AccountCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TRS_Invoices_PublicId] ON [dbo].[TRS_Invoices] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WEB_AffiliationApplications_PublicId] ON [dbo].[WEB_AffiliationApplications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WEB_AuxiliaryApplications_PublicId] ON [dbo].[WEB_AuxiliaryApplications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WEB_DataUpdates_PublicId] ON [dbo].[WEB_DataUpdates] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WEB_ExtraPayments_PublicId] ON [dbo].[WEB_ExtraPayments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WEB_LoanApplications_PublicId] ON [dbo].[WEB_LoanApplications] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WEB_Services_PublicId] ON [dbo].[WEB_Services] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WEB_Services_ServiceNumber_ServiceType_IdentificationNumber] ON [dbo].[WEB_Services] ([ServiceNumber], [ServiceType], [IdentificationNumber]) WHERE [ServiceNumber] IS NOT NULL AND [ServiceType] IS NOT NULL AND [IdentificationNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804122556_InitialSchema'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804122556_InitialSchema', N'10.0.10');
END;

COMMIT;
GO

