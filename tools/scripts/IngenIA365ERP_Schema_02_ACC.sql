-- ============================================================
-- IngenIA365ERP — Database Schema v1.0
-- Module: ACC_ (Accounting/Contabilidad) — 33 tables
-- ============================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. ACC_ChartOfAccounts (from: cnt_maecuen)
-- Master chart of accounts — monthly balances removed to ACC_AccountBalances
-- ============================================================
CREATE TABLE [dbo].[ACC_ChartOfAccounts] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(15)     NULL,

    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [Nature]                    NVARCHAR(1)      NOT NULL,
    [Name]                      NVARCHAR(150)    NOT NULL,
    [Level]                     TINYINT          NOT NULL,
    [Rate]                      DECIMAL(7,4)     NOT NULL DEFAULT 0,
    [RequiresDocument]          BIT              NOT NULL DEFAULT 0,
    [ManagesCostCenter]         BIT              NOT NULL DEFAULT 0,
    [RequiresThirdParty]        BIT              NOT NULL DEFAULT 0,
    [IsOperationalAsset]        BIT              NOT NULL DEFAULT 0,
    [AppliesToLending]          BIT              NOT NULL DEFAULT 0,
    [AppliesToSavingsCDT]       BIT              NOT NULL DEFAULT 0,
    [AppliesToInventory]        BIT              NOT NULL DEFAULT 0,
    [AppliesToTreasury]         BIT              NOT NULL DEFAULT 0,
    [AppliesToPayroll]          BIT              NOT NULL DEFAULT 0,
    [AppliesToAccounting]       BIT              NOT NULL DEFAULT 0,
    [AppliesToInvoicing]        BIT              NOT NULL DEFAULT 0,
    [CostCenterCode]            NVARCHAR(10)     NULL,
    [FixedAssetGroup]           NVARCHAR(5)      NULL,
    [FixedAssetClass]           NVARCHAR(2)      NULL,
    [Status]                    INT              NOT NULL DEFAULT 0,
    [CashFlowCode]              NVARCHAR(5)      NULL,
    [BankReconciliationCode]    NVARCHAR(5)      NULL,
    [WithholdingType]           NVARCHAR(2)      NULL,
    [AccountBelongsTo]          NVARCHAR(5)      NULL,
    [ReportFormatId]            INT              NULL,
    [ConceptId]                 INT              NULL,
    [SourceId]                  INT              NULL,
    [AccountGroup]              NVARCHAR(2)      NULL,

    -- Withholding / tax line references (legacy: DRETEFUENTE, DRETEICA, DRETEIVA, DRETEVENTA, DECLA_RENTA, DEGRAMOV, DRETEICABASE, degramovbase)
    [WithholdingLineCode]       NVARCHAR(5)      NULL,
    [IcaLineCode]               NVARCHAR(5)      NULL,
    [VatLineCode]               NVARCHAR(5)      NULL,
    [SalesWithholdingLineCode]  NVARCHAR(5)      NULL,
    [IncomeTaxDeclaration]      BIT              NOT NULL DEFAULT 0,
    [GmfLineCode]               NVARCHAR(5)      NULL,
    [IcaBaseLineCode]           NVARCHAR(5)      NULL,
    [GmfBaseLineCode]           NVARCHAR(5)      NULL,

    -- Financial statement grouping (legacy: numeroecfe/subgrupoecfe, etc.)
    [FinStmtCashFlowNumber]     INT              NULL,
    [FinStmtCashFlowSubgroup]   INT              NULL,
    [FinStmtChangeNumber]       INT              NULL,
    [FinStmtChangeSubgroup]     INT              NULL,
    [FinStmtFinPosNumber]       INT              NULL,
    [FinStmtFinPosSubgroup]     INT              NULL,
    [FinStmtBalSheetNumber]     INT              NULL,
    [FinStmtBalSheetSubgroup]   INT              NULL,
    [FinStmtEquityNumber]       INT              NULL,
    [FinStmtEquitySubgroup]     INT              NULL,
    [FinStmtIncomeNumber]       INT              NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_ChartOfAccounts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_ChartOfAccounts_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_ChartOfAccounts_AccountCode] UNIQUE ([AccountCode])
);
GO

-- ============================================================
-- 2. ACC_AccountBalances (from: cnt_salage — normalized)
-- Monthly debit/credit balances per account/branch/cost-center
-- ============================================================
CREATE TABLE [dbo].[ACC_AccountBalances] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [AccountId]                 INT              NOT NULL,
    [PeriodYear]                INT              NOT NULL,
    [PeriodMonth]               TINYINT          NOT NULL,  -- 1-13 (13 = adjustment period)
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [DebitAmount]               DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [CreditAmount]              DECIMAL(18,2)    NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_AccountBalances] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_AccountBalances_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_AccountBalances_Natural] UNIQUE ([AccountId], [PeriodYear], [PeriodMonth], [BranchId], [CostCenterId])
);
GO

-- ============================================================
-- 3. ACC_JournalEntries (from: cnt_movimto)
-- Individual accounting journal entry lines
-- ============================================================
CREATE TABLE [dbo].[ACC_JournalEntries] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacySequence]            BIGINT           NULL,

    [VoucherTypeCode]           NVARCHAR(10)     NOT NULL,
    [DocumentNumber]            BIGINT           NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [PersonId]                  INT              NULL,
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [PeriodCode]                NVARCHAR(10)     NULL,
    [TransactionDate]           DATE             NOT NULL,
    [Description]               NVARCHAR(200)    NULL,
    [AuxiliaryDocument]         NVARCHAR(10)     NULL,
    [DebitAmount]               DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [CreditAmount]              DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [BaseAmount]                DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Status]                    INT              NOT NULL DEFAULT 0,
    [InvoiceNumber]             NVARCHAR(20)     NULL,
    [AuxiliaryRequestId]        INT              NULL,
    [UserName]                  NVARCHAR(50)     NULL,
    [DocumentType]              NVARCHAR(5)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_JournalEntries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_JournalEntries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 4. ACC_Documents (from: cnt_docmto)
-- Accounting document headers (vouchers)
-- ============================================================
CREATE TABLE [dbo].[ACC_Documents] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCompronte]           NVARCHAR(5)      NULL,
    [LegacyNumero]              BIGINT           NULL,

    [VoucherTypeCode]           NVARCHAR(10)     NOT NULL,
    [DocumentNumber]            BIGINT           NOT NULL,
    [Detail]                    NVARCHAR(200)    NULL,
    [TotalDebit]                DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [TotalCredit]               DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DocumentDate]              DATE             NOT NULL,
    [IsClosed]                  BIT              NOT NULL DEFAULT 0,
    [IsVoided]                  BIT              NOT NULL DEFAULT 0,
    [BeneficiaryId]             INT              NULL,
    [PeriodCode]                INT              NULL,
    [CheckNumber]               NVARCHAR(10)     NULL,
    [BankId]                    SMALLINT         NULL,
    [ModuleCode]                NVARCHAR(5)      NULL,
    [PaymentMethod]             NVARCHAR(2)      NULL,
    [InvoiceNumber]             BIGINT           NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_Documents] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_Documents_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_Documents_VoucherDoc] UNIQUE ([VoucherTypeCode], [DocumentNumber])
);
GO

-- ============================================================
-- 5. ACC_AuxiliaryDocuments (from: cnt_docaux)
-- Auxiliary document summary with monthly debit/credit (denormalized)
-- ============================================================
CREATE TABLE [dbo].[ACC_AuxiliaryDocuments] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyKey]                 NVARCHAR(100)    NULL,

    [PeriodYear]                INT              NOT NULL,
    [AuxiliaryType]             NVARCHAR(5)      NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [PersonId]                  INT              NULL,
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [DocumentType]              NVARCHAR(5)      NULL,
    [DocumentNumber]            NVARCHAR(20)     NULL,
    [Detail]                    NVARCHAR(200)    NULL,
    [DocumentDate]              DATE             NULL,
    [DueDate]                   DATE             NULL,
    [OriginalValue]             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [InitialBalance]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [InvoiceNumber]             NVARCHAR(20)     NULL,

    -- Monthly debit/credit columns (denormalized summary)
    [JanDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JanCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [FebDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [FebCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MarDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MarCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AprDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AprCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MayDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MayCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JunDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JunCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JulDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JulCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AugDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AugCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [SepDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [SepCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [OctDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [OctCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [NovDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [NovCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DecDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DecCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Period13Amount]            DECIMAL(18,2)    NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_AuxiliaryDocuments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_AuxiliaryDocuments_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 6. ACC_ThirdPartyAccounts (from: cnt_tercero)
-- Third-party (NIT) balances per account/branch/cost-center/year
-- ============================================================
CREATE TABLE [dbo].[ACC_ThirdPartyAccounts] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [PeriodYear]                INT              NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [PersonId]                  INT              NOT NULL,
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [InitialBalance]            DECIMAL(18,2)    NOT NULL DEFAULT 0,

    -- Monthly debit/credit columns
    [JanDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JanCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [FebDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [FebCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MarDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MarCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AprDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AprCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MayDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MayCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JunDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JunCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JulDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [JulCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AugDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AugCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [SepDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [SepCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [OctDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [OctCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [NovDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [NovCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DecDebit]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DecCredit]                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Period13Debit]             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Period13Credit]            DECIMAL(18,2)    NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_ThirdPartyAccounts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_ThirdPartyAccounts_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_ThirdPartyAccounts_Natural] UNIQUE ([PeriodYear], [AccountId], [PersonId], [BranchId], [CostCenterId])
);
GO

-- ============================================================
-- 7. ACC_Amortizations (from: cnt_amortiza)
-- Amortization schedules
-- ============================================================
CREATE TABLE [dbo].[ACC_Amortizations] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [PeriodYear]                INT              NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [PersonId]                  INT              NULL,
    [DocumentCode]              NVARCHAR(10)     NULL,
    [CrossAccountId]            INT              NULL,
    [CrossCostCenterCode]       NVARCHAR(10)     NULL,
    [CrossPersonTaxId]          NVARCHAR(20)     NULL,
    [CrossInitialBalance]       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MovementAccountId]         INT              NULL,
    [MovementCostCenterCode]    NVARCHAR(10)     NULL,
    [MovementPersonTaxId]       NVARCHAR(20)     NULL,
    [MovementInitialBalance]    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AmortizationDate]          DATE             NULL,
    [OriginalAmount]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MonthlyAmount]             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [TermMonths]                INT              NULL,
    [RemainingBalance]          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [StartDate]                 DATE             NULL,
    [EndDate]                   DATE             NULL,
    [LastPeriod]                INT              NULL,
    [Rate]                      DECIMAL(5,2)     NOT NULL DEFAULT 0,
    [Status]                    INT              NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_Amortizations] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_Amortizations_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_Amortizations_Natural] UNIQUE ([PeriodYear], [AccountId], [BranchId], [CostCenterId], [PersonId], [DocumentCode])
);
GO

-- ============================================================
-- 8. ACC_Depreciations (from: cnt_deprecia)
-- Depreciation schedules for fixed assets
-- ============================================================
CREATE TABLE [dbo].[ACC_Depreciations] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [PeriodYear]                INT              NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [PersonId]                  INT              NULL,
    [CrossAccountId]            INT              NULL,
    [CrossCostCenterCode]       NVARCHAR(10)     NULL,
    [CrossPersonTaxId]          NVARCHAR(20)     NULL,
    [CrossInitialBalance]       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [MovementAccountId]         INT              NULL,
    [MovementCostCenterCode]    NVARCHAR(10)     NULL,
    [MovementPersonTaxId]       NVARCHAR(20)     NULL,
    [MovementInitialBalance]    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [OriginalValue]             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DepreciationRate]          DECIMAL(5,2)     NOT NULL DEFAULT 0,
    [MonthlyDepreciation]       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AccumulatedDepreciation]   DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [NetValue]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [UsefulLifeMonths]          INT              NULL,
    [StartDate]                 DATE             NULL,
    [EndDate]                   DATE             NULL,
    [LastPeriod]                INT              NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_Depreciations] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_Depreciations_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_Depreciations_Natural] UNIQUE ([PeriodYear], [AccountId], [BranchId], [CostCenterId], [PersonId])
);
GO

-- ============================================================
-- 9. ACC_JournalEntryItems (from: cnt_movitem)
-- Journal entry detail items (staging/temporary entries)
-- ============================================================
CREATE TABLE [dbo].[ACC_JournalEntryItems] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [JournalEntryId]            BIGINT           NULL,
    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [PersonTaxId]               NVARCHAR(20)     NULL,
    [BranchCode]                NVARCHAR(5)      NULL,
    [CostCenterCode]            NVARCHAR(10)     NULL,
    [TransactionDate]           DATE             NOT NULL,
    [DebitAmount]               DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [CreditAmount]              DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Description]               NVARCHAR(200)    NULL,
    [BaseAmount]                DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [DocumentType]              NVARCHAR(5)      NULL,
    [DocumentNumber]            NVARCHAR(20)     NULL,
    [Period]                    NVARCHAR(10)     NULL,
    [VoucherTypeCode]           NVARCHAR(10)     NULL,
    [VoucherNumber]             INT              NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_JournalEntryItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_JournalEntryItems_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 10. ACC_Budgets (from: cnt_presupto)
-- Annual budget by account/branch/cost-center with monthly columns
-- ============================================================
CREATE TABLE [dbo].[ACC_Budgets] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [PeriodYear]                INT              NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [BranchId]                  INT              NOT NULL,
    [CostCenterId]              INT              NOT NULL,
    [JanBudget]                 DECIMAL(18,2)    NULL,
    [FebBudget]                 DECIMAL(18,2)    NULL,
    [MarBudget]                 DECIMAL(18,2)    NULL,
    [AprBudget]                 DECIMAL(18,2)    NULL,
    [MayBudget]                 DECIMAL(18,2)    NULL,
    [JunBudget]                 DECIMAL(18,2)    NULL,
    [JulBudget]                 DECIMAL(18,2)    NULL,
    [AugBudget]                 DECIMAL(18,2)    NULL,
    [SepBudget]                 DECIMAL(18,2)    NULL,
    [OctBudget]                 DECIMAL(18,2)    NULL,
    [NovBudget]                 DECIMAL(18,2)    NULL,
    [DecBudget]                 DECIMAL(18,2)    NULL,
    [TotalBudget]               DECIMAL(18,2)    NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_Budgets] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_Budgets_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_Budgets_Natural] UNIQUE ([PeriodYear], [AccountId], [BranchId], [CostCenterId])
);
GO

-- ============================================================
-- 11. ACC_RiskCategories (from: cnt_riesgo)
-- Daily balance tracking for risk categorization
-- ============================================================
CREATE TABLE [dbo].[ACC_RiskCategories] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [PeriodCode]                NVARCHAR(10)     NOT NULL,
    [InitialBalance]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day1]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day2]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day3]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day4]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day5]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day6]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day7]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day8]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day9]                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day10]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day11]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day12]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day13]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day14]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day15]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day16]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day17]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day18]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day19]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day20]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day21]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day22]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day23]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day24]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day25]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day26]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day27]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day28]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day29]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day30]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Day31]                     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [AverageBalance]            DECIMAL(18,2)    NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_RiskCategories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_RiskCategories_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_RiskCategories_Natural] UNIQUE ([AccountCode], [PeriodCode])
);
GO

-- ============================================================
-- 12. ACC_BankReconciliations (from: cnt_concibanca)
-- Bank reconciliation transaction lines
-- ============================================================
CREATE TABLE [dbo].[ACC_BankReconciliations] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [AccountId]                 INT              NOT NULL,
    [BankId]                    INT              NOT NULL,
    [PeriodCode]                INT              NULL,
    [TransactionDate]           DATE             NOT NULL,
    [DocumentType]              NVARCHAR(5)      NULL,
    [DocumentNumber]            NVARCHAR(20)     NULL,
    [Description]               NVARCHAR(200)    NULL,
    [DebitAmount]               DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [CreditAmount]              DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [IsReconciled]              BIT              NOT NULL DEFAULT 0,
    [ReconciliationDate]        DATE             NULL,
    [Status]                    INT              NOT NULL DEFAULT 0,
    [IsAdditional]              BIT              NOT NULL DEFAULT 0,
    [IsClosed]                  BIT              NOT NULL DEFAULT 0,
    [MovementSequence]          INT              NULL,
    [ModuleCode]                NVARCHAR(5)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_BankReconciliations] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_BankReconciliations_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 13. ACC_BankReconciliationFlats (from: cnt_concibancaplano)
-- Flat-file bank reconciliation imports
-- ============================================================
CREATE TABLE [dbo].[ACC_BankReconciliationFlats] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [PeriodCode]                INT              NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [AccountType]               NVARCHAR(20)     NULL,
    [TransactionCode]           NVARCHAR(50)     NULL,
    [AccountNumber]             NVARCHAR(50)     NULL,
    [TransactionDate]           NVARCHAR(10)     NULL,
    [DocumentNumber]            NVARCHAR(50)     NULL,
    [Amount]                    DECIMAL(18,2)    NULL,
    [TransactionType]           NVARCHAR(5)      NULL,
    [UserName]                  NVARCHAR(50)     NULL,
    [SystemDate]                DATETIME2        NULL,
    [IsProcessed]               BIT              NOT NULL DEFAULT 0,
    [Description]               NVARCHAR(200)    NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_BankReconciliationFlats] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_BankReconciliationFlats_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 14. ACC_BankReconciliationMasters (from: cnt_maeconcibanca)
-- Bank reconciliation period master records
-- ============================================================
CREATE TABLE [dbo].[ACC_BankReconciliationMasters] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [AccountId]                 INT              NOT NULL,
    [BankId]                    INT              NOT NULL,
    [PeriodCode]                NVARCHAR(10)     NOT NULL,
    [InitialBalance]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [FinalBalance]              DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [Status]                    INT              NOT NULL DEFAULT 0,
    [IsClosed]                  BIT              NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_BankReconciliationMasters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_BankReconciliationMasters_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_BankReconciliationMasters_Natural] UNIQUE ([AccountId], [BankId], [PeriodCode])
);
GO

-- ============================================================
-- 15. ACC_GmfTaxLines (from: cnt_lineagmf)
-- GMF (Gravamen a los Movimientos Financieros) tax line definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_GmfTaxLines] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(10)     NULL,

    [LineCode]                  NVARCHAR(10)     NOT NULL,
    [Description]               NVARCHAR(100)    NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [TaxRate]                   DECIMAL(10,5)    NOT NULL DEFAULT 0,
    [BaseAccountCode]           NVARCHAR(15)     NULL,
    [Sign]                      NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_GmfTaxLines] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_GmfTaxLines_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_GmfTaxLines_LineCode] UNIQUE ([LineCode])
);
GO

-- ============================================================
-- 16. ACC_IcaTaxLines (from: cnt_lineaica)
-- ICA (Industria y Comercio) tax line definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_IcaTaxLines] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(10)     NULL,

    [LineCode]                  NVARCHAR(10)     NOT NULL,
    [Description]               NVARCHAR(100)    NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [TaxRate]                   DECIMAL(10,5)    NOT NULL DEFAULT 0,
    [BaseAccountCode]           NVARCHAR(15)     NULL,
    [Sign]                      NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_IcaTaxLines] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_IcaTaxLines_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_IcaTaxLines_LineCode] UNIQUE ([LineCode])
);
GO

-- ============================================================
-- 17. ACC_VatTaxLines (from: cnt_lineaiva)
-- VAT (IVA) tax line definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_VatTaxLines] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(10)     NULL,

    [LineCode]                  NVARCHAR(10)     NOT NULL,
    [Description]               NVARCHAR(100)    NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [TaxRate]                   DECIMAL(10,5)    NOT NULL DEFAULT 0,
    [BaseAccountCode]           NVARCHAR(15)     NULL,
    [Sign]                      NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_VatTaxLines] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_VatTaxLines_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_VatTaxLines_LineCode] UNIQUE ([LineCode])
);
GO

-- ============================================================
-- 18. ACC_IncomeTaxLines (from: cnt_linearenta)
-- Income tax (Renta) line definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_IncomeTaxLines] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(10)     NULL,

    [LineCode]                  NVARCHAR(10)     NOT NULL,
    [Description]               NVARCHAR(100)    NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [TaxRate]                   DECIMAL(10,5)    NOT NULL DEFAULT 0,
    [BaseAccountCode]           NVARCHAR(15)     NULL,
    [Sign]                      NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_IncomeTaxLines] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_IncomeTaxLines_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_IncomeTaxLines_LineCode] UNIQUE ([LineCode])
);
GO

-- ============================================================
-- 19. ACC_WithholdingTaxLines (from: cnt_linretefuente)
-- Withholding tax (Retefuente) line definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_WithholdingTaxLines] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(10)     NULL,

    [LineCode]                  NVARCHAR(10)     NOT NULL,
    [Description]               NVARCHAR(100)    NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [TaxRate]                   DECIMAL(10,5)    NOT NULL DEFAULT 0,
    [BaseAccountCode]           NVARCHAR(15)     NULL,
    [Sign]                      NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_WithholdingTaxLines] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_WithholdingTaxLines_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_WithholdingTaxLines_LineCode] UNIQUE ([LineCode])
);
GO

-- ============================================================
-- 20. ACC_DianReportFormats (from: cnt_parfordian)
-- DIAN report format/concept configuration
-- ============================================================
CREATE TABLE [dbo].[ACC_DianReportFormats] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [FormatId]                  INT              NOT NULL,
    [ConceptId]                 INT              NOT NULL,
    [FormatCode]                NVARCHAR(10)     NULL,
    [Description]               NVARCHAR(100)    NULL,
    [Threshold]                 DECIMAL(18,0)    NOT NULL DEFAULT 0,
    [MinorTaxId]                NVARCHAR(20)     NULL,
    [BalanceThreshold]          DECIMAL(18,0)    NOT NULL DEFAULT 0,
    [DianTaxId]                 NVARCHAR(15)     NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_DianReportFormats] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_DianReportFormats_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_DianReportFormats_Natural] UNIQUE ([FormatId], [ConceptId])
);
GO

-- ============================================================
-- 21. ACC_FinancialReportParams (from: cnt_parinfmedian)
-- Financial report parameter configuration
-- ============================================================
CREATE TABLE [dbo].[ACC_FinancialReportParams] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [FormatId]                  INT              NOT NULL,
    [ConceptId]                 INT              NOT NULL,
    [ParameterOption]           INT              NOT NULL,
    [ParameterValue]            NVARCHAR(50)     NOT NULL,
    [ValueOption]               INT              NOT NULL DEFAULT 0,
    [CalculationBase]           INT              NOT NULL DEFAULT 0,
    [Formula]                   INT              NOT NULL DEFAULT 0,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_FinancialReportParams] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_FinancialReportParams_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_FinancialReportParams_Natural] UNIQUE ([FormatId], [ConceptId], [ParameterOption], [ParameterValue])
);
GO

-- ============================================================
-- 22. ACC_FinancialReportValues (from: cnt_parvalmedian)
-- Financial report value definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_FinancialReportValues] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [FormatId]                  INT              NOT NULL,
    [ValueId]                   INT              NOT NULL,
    [ValueCode]                 NVARCHAR(20)     NULL,
    [Description]               NVARCHAR(100)    NOT NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_FinancialReportValues] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_FinancialReportValues_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_FinancialReportValues_Natural] UNIQUE ([FormatId], [ValueId])
);
GO

-- ============================================================
-- 23. ACC_FinancialReports (from: cnt_infmedian)
-- Financial report data (media magnetica / DIAN information)
-- ============================================================
CREATE TABLE [dbo].[ACC_FinancialReports] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [FormatId]                  INT              NOT NULL,
    [ConceptId]                 INT              NOT NULL,
    [PersonId]                  INT              NOT NULL,
    [AccountId]                 INT              NOT NULL,
    [Year]                      INT              NOT NULL,
    [DocumentTypeCode]          INT              NULL,
    [VerificationDigit]         NVARCHAR(1)      NULL,
    [LastName1]                 NVARCHAR(80)     NULL,
    [LastName2]                 NVARCHAR(80)     NULL,
    [FirstName]                 NVARCHAR(80)     NULL,
    [CompanyName]               NVARCHAR(80)     NULL,
    [FirstName2]                NVARCHAR(50)     NULL,
    [SecondName]                NVARCHAR(50)     NULL,
    [Municipality]              INT              NULL,
    [Address]                   NVARCHAR(100)    NULL,
    [SavingsAccount]            NVARCHAR(50)     NULL,
    [Value1]                    DECIMAL(18,2)    NULL,
    [Value2]                    DECIMAL(18,2)    NULL,
    [Value3]                    DECIMAL(18,2)    NULL,
    [Value4]                    DECIMAL(18,2)    NULL,
    [Value5]                    DECIMAL(18,2)    NULL,
    [Value6]                    DECIMAL(18,2)    NULL,
    [Value7]                    DECIMAL(18,2)    NULL,
    [Value8]                    DECIMAL(18,2)    NULL,
    [Value9]                    DECIMAL(18,2)    NULL,
    [Value10]                   DECIMAL(18,2)    NULL,
    [ProcessedBySolido]         NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_FinancialReports] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_FinancialReports_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_FinancialReports_Natural] UNIQUE ([FormatId], [ConceptId], [PersonId], [AccountId], [Year])
);
GO

-- ============================================================
-- 24. ACC_TaxFormCodes (from: cnt_codforimpu)
-- Tax form codes (economic activity, contributor type, etc.)
-- ============================================================
CREATE TABLE [dbo].[ACC_TaxFormCodes] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [FormCode]                  NVARCHAR(10)     NULL,
    [Description]               NVARCHAR(100)    NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [ConceptCode]               NVARCHAR(10)     NULL,
    [TaxType]                   NVARCHAR(5)      NULL,

    -- Legacy columns from cnt_codforimpu
    [EconomicActivity]          NVARCHAR(5)      NULL,
    [ContributorType]           NVARCHAR(2)      NULL,
    [DocumentTypeCode]          NVARCHAR(5)      NULL,
    [RepresentationCode]        NVARCHAR(2)      NULL,
    [AuditorCode]               NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_TaxFormCodes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_TaxFormCodes_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 25. ACC_StampTaxes (from: cnt_estampilla)
-- Stamp tax configuration by grade
-- ============================================================
CREATE TABLE [dbo].[ACC_StampTaxes] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(5)      NULL,

    [Grade]                     NVARCHAR(5)      NOT NULL,
    [DebitAccountCode]          NVARCHAR(15)     NULL,
    [CreditAccountCode]        NVARCHAR(15)     NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_StampTaxes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_StampTaxes_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_StampTaxes_Grade] UNIQUE ([Grade])
);
GO

-- ============================================================
-- 26. ACC_AccountGroups (from: cnt_grupocuenta)
-- Account grouping for financial statement structure
-- ============================================================
CREATE TABLE [dbo].[ACC_AccountGroups] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [GroupNumber]               INT              NULL,
    [GroupType]                 INT              NULL,
    [AccountCode]               NVARCHAR(15)     NULL,
    [Description]               NVARCHAR(100)    NULL,
    [ReportOrder]               INT              NULL,
    [Level]                     INT              NULL,
    [ParentGroupId]             INT              NULL,
    [FinancialStatementCode]    NVARCHAR(5)      NULL,
    [SpecialCode]               NVARCHAR(2)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_AccountGroups] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_AccountGroups_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 27. ACC_AccountSubgroups (from: cnt_subgrupocuenta)
-- Account subgrouping for financial statement structure
-- ============================================================
CREATE TABLE [dbo].[ACC_AccountSubgroups] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [GroupId]                   INT              NULL,
    [SubgroupNumber]            INT              NOT NULL,
    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [Description]               NVARCHAR(100)    NOT NULL,
    [ReportOrder]               INT              NULL,
    [Level]                     INT              NULL,
    [ParentSubgroupId]          INT              NULL,
    [FinancialStatementCode]    NVARCHAR(5)      NULL,
    [SpecialCode]               NVARCHAR(1)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_AccountSubgroups] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_AccountSubgroups_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 28. ACC_GroupNames (from: cnt_nombregrupo)
-- Group name definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_GroupNames] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [GroupNumber]               INT              NOT NULL,
    [Name]                      NVARCHAR(100)    NOT NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_GroupNames] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_GroupNames_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 29. ACC_SubgroupNames (from: cnt_nombresubgrupo)
-- Subgroup name definitions
-- ============================================================
CREATE TABLE [dbo].[ACC_SubgroupNames] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [SubgroupNumber]            INT              NOT NULL,
    [Name]                      NVARCHAR(100)    NOT NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_SubgroupNames] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_SubgroupNames_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 30. ACC_ExchangeRateHistory (from: cnt_estadosdecambio)
-- Exchange rate / statement of changes history
-- ============================================================
CREATE TABLE [dbo].[ACC_ExchangeRateHistory] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [CurrencyCode]              NVARCHAR(5)      NULL,
    [EffectiveDate]             DATE             NULL,
    [ExchangeRate]              DECIMAL(18,6)    NOT NULL DEFAULT 0,
    [SourceAccountCode]         NVARCHAR(15)     NULL,
    [TargetAccountCode]         NVARCHAR(15)     NULL,

    -- Legacy columns from cnt_estadosdecambio
    [GroupNumber]               INT              NULL,
    [SubgroupNumber]            INT              NULL,
    [PeriodCode]                INT              NULL,
    [Amount]                    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    [StatementCode]             NVARCHAR(5)      NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_ExchangeRateHistory] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_ExchangeRateHistory_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 31. ACC_FiscalPeriods (new table)
-- Fiscal period management (open/closed status)
-- ============================================================
CREATE TABLE [dbo].[ACC_FiscalPeriods] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [PeriodYear]                INT              NOT NULL,
    [PeriodMonth]               TINYINT          NOT NULL,  -- 1-13 (13 = adjustment)
    [StartDate]                 DATE             NOT NULL,
    [EndDate]                   DATE             NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL DEFAULT N'O',  -- O=Open, C=Closed
    [ClosedAt]                  DATETIME2        NULL,
    [ClosedBy]                  NVARCHAR(100)    NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_FiscalPeriods] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_FiscalPeriods_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_FiscalPeriods_Natural] UNIQUE ([PeriodYear], [PeriodMonth])
);
GO

-- ============================================================
-- 32. ACC_VoucherTypes (from: sys_compro02)
-- Voucher/document type configuration
-- ============================================================
CREATE TABLE [dbo].[ACC_VoucherTypes] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]                NVARCHAR(10)     NULL,

    [Code]                      NVARCHAR(10)     NOT NULL,
    [Name]                      NVARCHAR(100)    NOT NULL,
    [ShortName]                 NVARCHAR(20)     NULL,
    [DocumentType]              NVARCHAR(5)      NULL,
    [AccountingAccountCode]     NVARCHAR(15)     NULL,
    [UpdatesAccounting]         BIT              NOT NULL DEFAULT 0,
    [NextSequenceNumber]        BIGINT           NOT NULL DEFAULT 0,
    [EquivalentAccountCode]     NVARCHAR(15)     NULL,
    [RequiresDetail]            BIT              NOT NULL DEFAULT 0,
    [PrintFormat]               NVARCHAR(2)      NULL,
    [CostCenterCode]            NVARCHAR(10)     NULL,
    [ControlSequential]         BIT              NOT NULL DEFAULT 0,
    [BankReconciliationCode]    NVARCHAR(5)      NULL,
    [DebitCredit]               NVARCHAR(1)      NULL,
    [HasValidator]              BIT              NOT NULL DEFAULT 0,
    [ValidatorPort]             NVARCHAR(10)     NULL,
    [AutomaticDetail]           NVARCHAR(50)     NULL,
    [TreasuryRestriction]       BIT              NOT NULL DEFAULT 0,
    [Affects3xMil]              BIT              NOT NULL DEFAULT 0,
    [DocumentControlType]       NVARCHAR(2)      NULL,
    [MoneyLaundering]           BIT              NOT NULL DEFAULT 0,
    [ModuleCode]                NVARCHAR(5)      NULL,

    -- Additional legacy columns from sys_compro02
    [EquivalentVoucherCode]     NVARCHAR(5)      NULL,
    [EquivalentDocumentCode]    NVARCHAR(5)      NULL,
    [AccountCode2]              NVARCHAR(15)     NULL,
    [Nature]                    NVARCHAR(1)      NULL,
    [DianReportFlag]            SMALLINT         NULL,
    [SequentialFormat]          SMALLINT         NULL,
    [ReceiptInvoice]            NVARCHAR(1)      NULL,
    [InvoiceControlCode]        NVARCHAR(5)      NULL,
    [ReturnOverdue]             NVARCHAR(1)      NULL,
    [ConversionRate]            INT              NULL,

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_VoucherTypes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_VoucherTypes_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_VoucherTypes_Code] UNIQUE ([Code])
);
GO

-- ============================================================
-- 33. ACC_AccountingPeriods (from: sys_periodo — normalized)
-- Accounting period configuration per module/year (normalized from 13 column groups)
-- ============================================================
CREATE TABLE [dbo].[ACC_AccountingPeriods] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    [ModuleCode]                NVARCHAR(5)      NOT NULL,
    [Year]                      INT              NOT NULL,
    [PeriodNumber]              TINYINT          NOT NULL,  -- 1-13
    [StartDate]                 DATE             NOT NULL,
    [EndDate]                   DATE             NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL DEFAULT N'O',  -- O=Open, C=Closed

    -- Audit
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]                 NVARCHAR(100)    NOT NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [IsDeleted]                 BIT              NOT NULL DEFAULT 0,
    [DeletedAt]                 DATETIME2        NULL,
    [DeletedBy]                 NVARCHAR(100)    NULL,

    CONSTRAINT [PK_ACC_AccountingPeriods] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_ACC_AccountingPeriods_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_ACC_AccountingPeriods_Natural] UNIQUE ([ModuleCode], [Year], [PeriodNumber])
);
GO

-- ============================================================
-- End of ACC_ module — 33 tables
-- ============================================================
