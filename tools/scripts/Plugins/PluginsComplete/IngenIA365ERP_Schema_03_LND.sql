-- ============================================================
-- IngenIA365ERP — Database Schema v1.0
-- Module: LND_ (Lending/Cartera) — 93 tables
-- Includes: Loans, Savings, Deposits, Collections, SIPLA
-- Generated: 2026-03-20
-- ============================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- #76. LND_LoanPortfolios — Master loan/portfolio records
-- Original: cop_maecar
-- ============================================================
CREATE TABLE [dbo].[LND_LoanPortfolios] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]                  INT              NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [IdentificationNumber]      NVARCHAR(20)     NOT NULL,
    [ApplicationDate]           DATE             NOT NULL,
    [ApprovalDate]              DATE             NULL,
    [DisbursementDate]          DATE             NOT NULL,
    [DiscountStartDate]         DATE             NOT NULL,
    [LastAccrualDate]           DATE             NULL,
    [LastPaymentDate]           DATE             NULL,
    [LastDefaultDate]           DATE             NULL,
    [MaturityDate]              DATE             NULL,
    [ClosingDate]               DATE             NULL,
    [TermMonths]                INT              NOT NULL,
    [RequestedAmount]           DECIMAL(18,2)    NOT NULL,
    [ApprovedAmount]            DECIMAL(18,2)    NOT NULL,
    [CurrentBalance]            DECIMAL(18,2)    NOT NULL,
    [InstallmentAmount]         DECIMAL(18,2)    NOT NULL,
    [InterestRate]              DECIMAL(10,6)    NOT NULL,
    [PaymentCycle]              NVARCHAR(2)      NOT NULL,
    [PaymentPeriodicity]        NVARCHAR(2)      NOT NULL,
    [InstallmentType]           NVARCHAR(2)      NOT NULL,
    [InterestType]              NVARCHAR(2)      NOT NULL,
    [GuaranteeType]             NVARCHAR(5)      NOT NULL,
    [DeductionType]             NVARCHAR(2)      NOT NULL,
    [PaidInstallments]          INT              NOT NULL,
    [AdminFeeRate]              DECIMAL(12,5)    NOT NULL,
    [InsuranceRate]             DECIMAL(10,5)    NOT NULL,
    [CapitalBalanceCurrent]     DECIMAL(18,2)    NOT NULL,
    [CapitalBalanceMonthly]     DECIMAL(18,2)    NOT NULL,
    [CapitalBalancePayment]     DECIMAL(18,2)    NOT NULL,
    [InterestBalanceCurrent]    DECIMAL(18,2)    NOT NULL,
    [InterestBalanceMonthly]    DECIMAL(18,2)    NOT NULL,
    [InterestBalancePayment]    DECIMAL(18,2)    NOT NULL,
    [DefaultBalanceCurrent]     DECIMAL(18,2)    NOT NULL,
    [DefaultBalanceMonthly]     DECIMAL(18,2)    NOT NULL,
    [DefaultBalancePayment]     DECIMAL(18,2)    NOT NULL,
    [AdminBalanceCurrent]       DECIMAL(18,2)    NOT NULL,
    [AdminBalanceMonthly]       DECIMAL(18,2)    NOT NULL,
    [AdminBalancePayment]       DECIMAL(18,2)    NOT NULL,
    [InsuranceBalanceCurrent]   DECIMAL(18,2)    NOT NULL,
    [InsuranceBalanceMonthly]   DECIMAL(18,2)    NOT NULL,
    [InsuranceBalancePayment]   DECIMAL(18,2)    NOT NULL,
    [DocumentType]              NVARCHAR(5)      NOT NULL,
    [DocumentNumber]            INT              NOT NULL,
    [AdditionalCharges]         DECIMAL(18,2)    NOT NULL,
    [ExtraPaymentAmount]        DECIMAL(18,2)    NOT NULL,
    [CapitalAccrued]            DECIMAL(18,2)    NOT NULL,
    [InterestAccrued]           DECIMAL(18,2)    NOT NULL,
    [InsuranceAccrued]          DECIMAL(18,2)    NOT NULL,
    [AdminAccrued]              DECIMAL(18,2)    NOT NULL,
    [DefaultInterest]           DECIMAL(18,2)    NOT NULL,
    [DaysOverdue]               INT              NOT NULL,
    [InitialGracePeriod]        NVARCHAR(2)      NOT NULL,
    [GracePeriodStartDate]      DATE             NULL,
    [GracePeriodInstallment]    NVARCHAR(2)      NOT NULL,
    [GracePeriodDays]           INT              NOT NULL,
    [Codeudor1]                 NVARCHAR(20)     NOT NULL,
    [Codeudor2]                 NVARCHAR(20)     NOT NULL,
    [Codeudor3]                 NVARCHAR(20)     NOT NULL,
    [Codeudor4]                 NVARCHAR(20)     NOT NULL,
    [GuaranteeValue]            DECIMAL(18,2)    NOT NULL,
    [ContributionsAmount]       DECIMAL(18,2)    NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [Category]                  NVARCHAR(2)      NOT NULL,
    [ExtraPercentage]           DECIMAL(5,2)     NOT NULL,
    [PaidInstallmentsAgency]    DECIMAL(5,2)     NOT NULL,
    [PendingInstallmentCount]   DECIMAL(5,2)     NOT NULL,
    [SearchNumber]              NVARCHAR(10)     NOT NULL,
    [ProvisionRate]             DECIMAL(8,5)     NOT NULL,
    [ProvisionAmount]           DECIMAL(18,2)    NOT NULL,
    [OrderInterest]             DECIMAL(18,2)    NOT NULL,
    [CxcInterest]               DECIMAL(18,2)    NOT NULL,
    [LocalClearing]             DECIMAL(18,2)    NOT NULL,
    [OtherClearing]             DECIMAL(18,2)    NOT NULL,
    [LegalCollection]           NVARCHAR(2)      NOT NULL,
    [LawyerCode]                NVARCHAR(5)      NOT NULL,
    [AdminInstallment]          DECIMAL(18,2)    NOT NULL,
    [InsuranceInstallment]      DECIMAL(18,2)    NOT NULL,
    [CapitalInstallment]        DECIMAL(18,2)    NOT NULL,
    [InterestInstallment]       DECIMAL(18,2)    NOT NULL,
    [OtherInstallment]          DECIMAL(18,2)    NOT NULL,
    [ApplicationNumber]         INT              NOT NULL,
    [ExtraInMonth]              NVARCHAR(2)      NOT NULL,
    [ExtraInAdvance]            NVARCHAR(2)      NOT NULL,
    [FirstPaymentFlag]          NVARCHAR(2)      NOT NULL,
    [SecondPaymentType]         NVARCHAR(2)      NOT NULL,
    [InterestInstallmentAmt]    DECIMAL(18,2)    NOT NULL,
    [LessAmount]                DECIMAL(18,2)    NOT NULL,
    [CardNumber]                NVARCHAR(20)     NOT NULL,
    [CapitalAppliedPayroll]     DECIMAL(18,2)    NOT NULL,
    [InterestAppliedPayroll]    DECIMAL(18,2)    NOT NULL,
    [DefaultAppliedPayroll]     DECIMAL(18,2)    NOT NULL,
    [InsuranceAppliedPayroll]   DECIMAL(18,2)    NOT NULL,
    [AdminAppliedPayroll]       DECIMAL(18,2)    NOT NULL,
    [PayrollCode]               NVARCHAR(8)      NOT NULL,
    [InsuranceForm]             NVARCHAR(3)      NOT NULL,
    [TotalPriorInterest]        NVARCHAR(2)      NOT NULL,
    [DiscountCompany]           NVARCHAR(5)      NOT NULL,
    [IncludeAutoDebit]          NVARCHAR(2)      NOT NULL,
    [CifinStartDate]            DATE             NULL,
    [CifinEndDate]              DATE             NULL,
    [CifinDaysOverdue]          INT              NOT NULL,
    [CifinOverdueBalance]       DECIMAL(18,2)    NOT NULL,
    [RestructureDate]           DATE             NULL,
    [RestructureCategory]       NVARCHAR(2)      NOT NULL,
    [IsRestructured]            NVARCHAR(2)      NOT NULL,
    [AuthCreditBureau]          NVARCHAR(2)      NOT NULL,
    [CifinOverdueInstallments]  INT              NOT NULL,
    [IsWrittenOff]              NVARCHAR(2)      NOT NULL,
    [TransactionId]             NVARCHAR(15)     NOT NULL,
    [DtfRate]                   DECIMAL(10,5)    NOT NULL,
    [SpreadPoints]              DECIMAL(10,5)    NOT NULL,
    [HasLawsuit]                NVARCHAR(2)      NOT NULL,
    [WriteOffCapital]           DECIMAL(18,3)    NOT NULL,
    [WriteOffInterest]          DECIMAL(18,3)    NOT NULL,
    [WriteOffDate]              DATETIME2        NULL,
    [WriteOffMinutes]           NVARCHAR(30)     NOT NULL,
    [LawyerNotes]               NVARCHAR(250)    NOT NULL,
    [WriteOffApprovalDate]      DATETIME2        NULL,
    [TransferConcept]           NVARCHAR(2)      NOT NULL,
    [IsFopep]                   NVARCHAR(2)      NOT NULL,
    [ProposedInterestDate]      DATETIME2        NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [LegacyLinCred]             INT              NULL,
    [LegacyNumero]              BIGINT           NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_LoanPortfolios] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_LoanPortfolios_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_LoanPortfolios_Person_Line_Number] UNIQUE ([PersonId], [CreditLineId], [PortfolioNumber])
);
GO

-- ============================================================
-- #77. LND_Transactions — Portfolio movement records
-- Original: cop_movimto
-- ============================================================
CREATE TABLE [dbo].[LND_Transactions] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [VoucherType]               NVARCHAR(5)      NOT NULL,
    [DocumentNumber]            BIGINT           NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [TransactionDate]           DATE             NOT NULL,
    [DebitAmount]               DECIMAL(18,2)    NOT NULL,
    [CreditAmount]              DECIMAL(18,2)    NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [InterestRate]              DECIMAL(10,6)    NOT NULL,
    [TransactionCode]           NVARCHAR(3)      NOT NULL,
    [Cycles]                    INT              NOT NULL,
    [SecondaryAccount]          NVARCHAR(15)     NOT NULL,
    [DiscountAmount]            DECIMAL(18,2)    NOT NULL,
    [SearchCode]                NVARCHAR(8)      NOT NULL,
    [BankCode]                  NVARCHAR(5)      NOT NULL,
    [CrossDocumentType]         NVARCHAR(5)      NOT NULL,
    [CrossDocumentNumber]       NVARCHAR(20)     NULL,
    [LocalCheckAmount]          DECIMAL(18,2)    NOT NULL,
    [OtherCheckAmount]          DECIMAL(18,2)    NOT NULL,
    [SecondaryCostCenter]       NVARCHAR(10)     NOT NULL,
    [IsAdvancePayment]          NVARCHAR(2)      NOT NULL,
    [WithholdingBase]           DECIMAL(18,2)    NOT NULL,
    [UserId]                    NVARCHAR(20)     NULL,
    [SystemDate]                DATE             NOT NULL,
    [ExtraNumber]               INT              NOT NULL,
    [IdentificationNumber]      NVARCHAR(20)     NULL,
    [InvoiceNumber]             NVARCHAR(20)     NULL,
    [UserFullName]              NVARCHAR(50)     NOT NULL,
    [Period]                    NVARCHAR(8)      NOT NULL,
    [Description]               NVARCHAR(100)    NOT NULL,
    [DueDate]                   DATE             NULL,
    [CheckNumber]               NVARCHAR(20)     NOT NULL,
    [CodeudorCode]              NVARCHAR(20)     NOT NULL,
    [OverdraftUser]             NVARCHAR(20)     NULL,
    [OverdraftAmount]           DECIMAL(18,3)    NULL,
    [ReliquidatedInstallment]   DECIMAL(18,3)    NOT NULL,
    [TransactionSource]         NVARCHAR(3)      NOT NULL,
    [ApplicationId]             INT              NOT NULL,
    [CdatAccrualDate]           DATETIME2        NULL,
    [AuxiliaryApplicationId]    INT              NOT NULL,
    [DependsOnSequence]         BIGINT           NOT NULL,
    [ReliquidationFlag]         NVARCHAR(2)      NULL,
    [SavingsLineId]             INT              NULL,
    [LegacySecuencia]           BIGINT           NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Transactions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Transactions_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #78. LND_PendingInstallments — Open installment schedule
-- Original: cop_cuopen
-- ============================================================
CREATE TABLE [dbo].[LND_PendingInstallments] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [AccrualPeriod]             INT              NOT NULL,
    [AccountingPeriod]          INT              NOT NULL,
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [Description]               NVARCHAR(50)     NOT NULL,
    [Cycle]                     INT              NOT NULL,
    [TotalAmount]               DECIMAL(18,2)    NOT NULL,
    [AccruedInterest]           DECIMAL(18,2)    NOT NULL,
    [AccruedCapital]            DECIMAL(18,2)    NOT NULL,
    [AccruedExtra]              DECIMAL(18,2)    NOT NULL,
    [PaidInterest]              DECIMAL(18,2)    NOT NULL,
    [PaidCapital]               DECIMAL(18,2)    NOT NULL,
    [PaidExtra]                 DECIMAL(18,2)    NOT NULL,
    [BalanceInterest]           DECIMAL(18,2)    NOT NULL,
    [BalanceCapital]            DECIMAL(18,2)    NOT NULL,
    [BalanceExtra]              DECIMAL(18,2)    NOT NULL,
    [CreditBalance]             DECIMAL(18,2)    NOT NULL,
    [ExtraNumber]               INT              NOT NULL,
    [ObligationInstallment]     DECIMAL(18,2)    NOT NULL,
    [TotalInstallment]          DECIMAL(18,2)    NOT NULL,
    [ExtraPaymentForm]          NVARCHAR(2)      NOT NULL,
    [DefaultInterest]           DECIMAL(18,2)    NOT NULL,
    [DefaultInterestBalance]    DECIMAL(18,2)    NOT NULL,
    [TransactionDate]           DATE             NULL,
    [DeductionType]             NVARCHAR(2)      NOT NULL,
    [PriorCapitalBalance]       DECIMAL(18,2)    NOT NULL,
    [PriorInterestBalance]      DECIMAL(18,2)    NOT NULL,
    [PriorExtraBalance]         DECIMAL(18,2)    NOT NULL,
    [DaysToMaturity]            INT              NOT NULL,
    [IsAdvancePayment]          NVARCHAR(2)      NOT NULL,
    [DefaultInterestAccrued]    DECIMAL(18,2)    NOT NULL,
    [DefaultInterestPaid]       DECIMAL(18,2)    NOT NULL,
    [LastDefaultDate]           DATE             NULL,
    [AdvanceAmount]             DECIMAL(18,2)    NOT NULL,
    [AdvanceCapital]            DECIMAL(18,2)    NOT NULL,
    [AdvanceInterest]           DECIMAL(18,2)    NOT NULL,
    [AdvanceExtra]              DECIMAL(18,2)    NOT NULL,
    [TotalBalance]              DECIMAL(18,2)    NOT NULL,
    [DaysOverdue]               DECIMAL(18,2)    NOT NULL,
    [InterestRate]              DECIMAL(7,4)     NOT NULL,
    [EntryType]                 NVARCHAR(2)      NOT NULL,
    [PriorInsuranceBalance]     DECIMAL(18,2)    NOT NULL,
    [PriorAdminBalance]         DECIMAL(18,2)    NOT NULL,
    [PriorOtherBalance]         DECIMAL(18,2)    NOT NULL,
    [AccruedInsurance]          DECIMAL(18,2)    NOT NULL,
    [AccruedAdmin]              DECIMAL(18,2)    NOT NULL,
    [AccruedOther]              DECIMAL(18,2)    NOT NULL,
    [PaidInsurance]             DECIMAL(18,2)    NOT NULL,
    [PaidAdmin]                 DECIMAL(18,2)    NOT NULL,
    [PaidOther]                 DECIMAL(18,2)    NOT NULL,
    [BalanceInsurance]          DECIMAL(18,2)    NOT NULL,
    [BalanceAdmin]              DECIMAL(18,2)    NOT NULL,
    [BalanceOther]              DECIMAL(18,2)    NOT NULL,
    [ProcessDate]               DATE             NULL,
    [UserId]                    NVARCHAR(20)     NOT NULL,
    [SystemDate]                DATE             NOT NULL,
    [DeductionClass]            NVARCHAR(2)      NOT NULL,
    [GuaranteeClass]            NVARCHAR(3)      NOT NULL,
    [EntryReason]               NVARCHAR(2)      NOT NULL,
    [DtfRate]                   DECIMAL(10,5)    NOT NULL,
    [SpreadPoints]              DECIMAL(10,5)    NOT NULL,
    [AccruedAll]                NVARCHAR(2)      NOT NULL,
    [AccruedNoExtra]            NVARCHAR(2)      NOT NULL,
    [AccruedOnlyExtra]          NVARCHAR(2)      NOT NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PendingInstallments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PendingInstallments_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #79. LND_PortfolioClassifications — Period classification snapshots
-- Original: cop_copclas
-- ============================================================
CREATE TABLE [dbo].[LND_PortfolioClassifications] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineCode]            NVARCHAR(5)      NOT NULL,
    [PortfolioNumber]           INT              NOT NULL,
    [AccountingPeriod]          INT              NOT NULL,
    [CreditClass]               INT              NOT NULL,
    [GuaranteeClass]            INT              NOT NULL,
    [DeductionClass]            INT              NOT NULL,
    [Category]                  NVARCHAR(2)      NOT NULL,
    [ConceptCode]               INT              NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [PersonName]                NVARCHAR(50)     NOT NULL,
    [TotalBalance]              DECIMAL(18,2)    NOT NULL,
    [InterestBalance]           DECIMAL(18,2)    NOT NULL,
    [DefaultBalance]            DECIMAL(18,2)    NOT NULL,
    [OrderBalance]              DECIMAL(18,2)    NOT NULL,
    [ProvisionBalance]          DECIMAL(18,2)    NOT NULL,
    [Rate]                      DECIMAL(10,0)    NOT NULL,
    [DaysOverdue]               INT              NOT NULL,
    [ContributionAmount]        DECIMAL(18,2)    NOT NULL,
    [InterestProvision]         DECIMAL(18,2)    NOT NULL,
    [DefaultOrderBalance]       DECIMAL(18,2)    NOT NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PortfolioClassifications] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PortfolioClassifications_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #80. LND_CreditLineParameters — Credit line configuration
-- Original: cop_concar12
-- ============================================================
CREATE TABLE [dbo].[LND_CreditLineParameters] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CreditLineId]              INT              NOT NULL,
    [Description]               NVARCHAR(40)     NOT NULL,
    [AllowExtension]            NVARCHAR(2)      NOT NULL,
    [DefaultInterestFlag]       NVARCHAR(2)      NOT NULL,
    [InterestRate]              DECIMAL(10,5)    NOT NULL,
    [InterestType]              NVARCHAR(2)      NOT NULL,
    [MaxTerm]                   INT              NOT NULL,
    [CreditLimit]               DECIMAL(10,5)    NOT NULL,
    [ExtraRate]                 DECIMAL(6,2)     NOT NULL,
    [FinancialInterest]         NVARCHAR(2)      NOT NULL,
    [GuaranteeClass]            NVARCHAR(2)      NOT NULL,
    [MaxAmount]                 DECIMAL(14,2)    NOT NULL,
    [AffectsFlag]               NVARCHAR(2)      NOT NULL,
    [AccrualRate]               DECIMAL(10,5)    NOT NULL,
    [CapitalForm]               NVARCHAR(2)      NOT NULL,
    [AdminRate]                 DECIMAL(12,5)    NOT NULL,
    [InstallmentType]           NVARCHAR(2)      NOT NULL,
    [DebitCreditFlag]           NVARCHAR(2)      NOT NULL,
    [SumGuarantee]              NVARCHAR(2)      NOT NULL,
    [TotalPriorInterest]        NVARCHAR(2)      NOT NULL,
    [MonthsInAdvance]           INT              NOT NULL,
    [AccountStatement]          NVARCHAR(2)      NOT NULL,
    [ClosingInterest]           NVARCHAR(2)      NOT NULL,
    [PrimaryVoucher]            NVARCHAR(2)      NOT NULL,
    [HousingLoan]               NVARCHAR(2)      NOT NULL,
    [InsuranceRate]             DECIMAL(10,5)    NOT NULL,
    [BalanceConsult]            NVARCHAR(2)      NOT NULL,
    [MinContribution]           DECIMAL(14,2)    NOT NULL,
    [PendingContribution]       NVARCHAR(3)      NOT NULL,
    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [GracePeriodFlag]           NVARCHAR(2)      NOT NULL,
    [GracePeriodMonths]         INT              NOT NULL,
    [ShowBalance]               NVARCHAR(2)      NOT NULL,
    [SecondaryCostCenter]       NVARCHAR(10)     NOT NULL,
    [AdminValueMin]             DECIMAL(15,2)    NOT NULL,
    [AdminValueMax]             DECIMAL(15,2)    NOT NULL,
    [IncomeTaxFlag]             NVARCHAR(2)      NOT NULL,
    [AdminForm]                 NVARCHAR(3)      NOT NULL,
    [Priority]                  NVARCHAR(3)      NOT NULL,
    [SavingsCode]               NVARCHAR(2)      NOT NULL,
    [GuarantorRequired]         NVARCHAR(2)      NOT NULL,
    [AdminClass]                NVARCHAR(2)      NOT NULL,
    [CategoryA]                 NVARCHAR(5)      NOT NULL,
    [CategoryB]                 NVARCHAR(5)      NOT NULL,
    [CategoryC]                 NVARCHAR(5)      NOT NULL,
    [CategoryD]                 NVARCHAR(5)      NOT NULL,
    [CategoryE]                 NVARCHAR(5)      NOT NULL,
    [ShortName]                 NVARCHAR(30)     NOT NULL,
    [ConsecutiveCode]           NVARCHAR(5)      NOT NULL,
    [CdatInterestRate]          DECIMAL(10,5)    NOT NULL,
    [AdminConceptCode]          INT              NOT NULL,
    [InsuranceConceptCode]      INT              NOT NULL,
    [InterestConceptCode]       INT              NOT NULL,
    [EquivalentRate]            NVARCHAR(2)      NOT NULL,
    [AccountInterestIncome]     NVARCHAR(15)     NOT NULL,
    [AccountInterestCxC]        NVARCHAR(15)     NOT NULL,
    [AccountInterestDefault]    NVARCHAR(15)     NOT NULL,
    [AccountInterestAdvance]    NVARCHAR(15)     NOT NULL,
    [AccountInterestOrderDebit] NVARCHAR(15)     NOT NULL,
    [AccountInterestOrderCredit]NVARCHAR(15)     NOT NULL,
    [FogaContribution]          NVARCHAR(3)      NOT NULL,
    [FogaClass]                 NVARCHAR(2)      NOT NULL,
    [PayrollCompanyCode]        NVARCHAR(5)      NOT NULL,
    [PayrollConceptCode]        NVARCHAR(5)      NOT NULL,
    [ColumnCount]               INT              NOT NULL,
    [ColumnTitle]               NVARCHAR(20)     NOT NULL,
    [AdditionalChargesConcept]  INT              NOT NULL,
    [TaxRate]                   DECIMAL(6,3)     NOT NULL,
    [InternetEnabled]           NVARCHAR(2)      NOT NULL,
    [MaturityBehavior]          NVARCHAR(2)      NOT NULL,
    [InsuranceValueMin]         DECIMAL(18,0)    NOT NULL,
    [InsuranceValueMax]         DECIMAL(18,0)    NOT NULL,
    [ProjectionItem]            INT              NOT NULL,
    [FormatId]                  INT              NOT NULL,
    [ConceptId]                 INT              NOT NULL,
    [SourceId]                  INT              NOT NULL,
    [CapitalizationConcept]     NVARCHAR(5)      NOT NULL,
    [SuperintendencyEquivalent] INT              NOT NULL,
    [ExportCifin]               NVARCHAR(2)      NOT NULL,
    [LiquidateDefaultDays]      NVARCHAR(2)      NOT NULL,
    [ModifyInstallmentType]     NVARCHAR(2)      NULL,
    [InterestTypeCode]          INT              NOT NULL,
    [DtfRate]                   DECIMAL(10,5)    NOT NULL,
    [BlockProjectionDate]       NVARCHAR(2)      NOT NULL,
    [BlockOverdueAssociate]     INT              NOT NULL,
    [ExtraPaymentApply]         NVARCHAR(2)      NOT NULL,
    [VatAccount]                NVARCHAR(15)     NOT NULL,
    [CalculateVat]              NVARCHAR(2)      NOT NULL,
    [CreditLimitType]           INT              NOT NULL,
    [CreditLimitCalcMethod]     NVARCHAR(3)      NOT NULL,
    [CreditLimitValue]          DECIMAL(18,2)    NOT NULL,
    [CreditLimitAvailable]      DECIMAL(5,2)     NOT NULL,
    [CapitalAtRisk]             NVARCHAR(2)      NOT NULL,
    [InvoiceEnabled]            NVARCHAR(2)      NOT NULL,
    [VatPercentage]             DECIMAL(5,3)     NOT NULL,
    [VatLineId]                 INT              NOT NULL,
    [IsVatLine]                 NVARCHAR(2)      NOT NULL,
    [InvoiceGroup]              NVARCHAR(3)      NOT NULL,
    [CalculationBase]           NVARCHAR(3)      NOT NULL,
    [BasePercentage]            DECIMAL(18,3)    NOT NULL,
    [DiscountConceptId]         INT              NOT NULL,
    [PeaceSalvoSeniority]       INT              NOT NULL,
    [LegacyLinCred]             INT              NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CreditLineParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CreditLineParameters_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_CreditLineParameters_LineId] UNIQUE ([CreditLineId])
);
GO

-- ============================================================
-- #81. LND_Documents — Voucher/document headers for portfolio
-- Original: cop_docmto
-- ============================================================
CREATE TABLE [dbo].[LND_Documents] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [VoucherType]               NVARCHAR(5)      NOT NULL,
    [DocumentNumber]            BIGINT           NOT NULL,
    [AccountCode]               NVARCHAR(20)     NOT NULL,
    [Description]               NVARCHAR(100)    NOT NULL,
    [DebitAmount]               DECIMAL(18,2)    NULL,
    [CreditAmount]              DECIMAL(18,2)    NULL,
    [DocumentType]              NVARCHAR(5)      NOT NULL,
    [DocumentSequence]          INT              NOT NULL,
    [DocumentDate]              DATE             NOT NULL,
    [CheckNumber]               NVARCHAR(15)     NOT NULL,
    [RecordFlag]                NVARCHAR(2)      NOT NULL,
    [BankCode]                  NVARCHAR(5)      NOT NULL,
    [IsClosed]                  NVARCHAR(2)      NOT NULL,
    [IsVoided]                  NVARCHAR(2)      NOT NULL,
    [ClosedInPortfolio]         NVARCHAR(2)      NULL,
    [BeneficiaryId]             NVARCHAR(20)     NOT NULL,
    [BeneficiaryCheckId]        NVARCHAR(20)     NOT NULL,
    [LegacyCompronte]           NVARCHAR(5)      NULL,
    [LegacyNumeroDomto]         BIGINT           NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Documents] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Documents_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #82. LND_ExtraPayments — Extra payment definitions per loan
-- Original: cop_extras
-- ============================================================
CREATE TABLE [dbo].[LND_ExtraPayments] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [ExtraNumber]               INT              NOT NULL,
    [Amount]                    DECIMAL(18,2)    NOT NULL,
    [PaymentForm]               NVARCHAR(2)      NOT NULL,
    [PaymentDate]               DATE             NOT NULL,
    [CurrentBalance]            DECIMAL(18,2)    NOT NULL,
    [ChargesAmount]             DECIMAL(18,2)    NOT NULL,
    [PaymentsAmount]            DECIMAL(18,2)    NOT NULL,
    [PaymentCycle]              INT              NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL,
    [ExtraType]                 NVARCHAR(3)      NOT NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ExtraPayments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ExtraPayments_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #83. LND_PortfolioBalances — Period-end balance snapshots
-- Original: cop_salmaecar
-- ============================================================
CREATE TABLE [dbo].[LND_PortfolioBalances] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [Period]                    INT              NOT NULL,
    [Balance]                   DECIMAL(18,2)    NOT NULL,
    [OpeningBalance]            DECIMAL(18,2)    NOT NULL,
    [DebitAmount]               DECIMAL(18,2)    NOT NULL,
    [CreditAmount]              DECIMAL(18,2)    NOT NULL,
    [OpenInstallments]          INT              NOT NULL,
    [CifinStatus]               NVARCHAR(3)      NOT NULL,
    [InitialCategory]           NVARCHAR(2)      NOT NULL,
    [FinalCategory]             NVARCHAR(2)      NOT NULL,
    [InitialLastPaymentDate]    DATETIME2        NULL,
    [FinalLastPaymentDate]      DATETIME2        NULL,
    [InstallmentAmount]         DECIMAL(18,2)    NOT NULL,
    [InterestRate]              DECIMAL(10,6)    NOT NULL,
    [PaymentCycle]              NVARCHAR(2)      NULL,
    [Periodicity]               NVARCHAR(2)      NULL,
    [DeductionClass]            NVARCHAR(2)      NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PortfolioBalances] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PortfolioBalances_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #84. LND_DefaultRecords — Overdue balance detail per period
-- Original: cop_copmora
-- ============================================================
CREATE TABLE [dbo].[LND_DefaultRecords] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [AccrualPeriod]             INT              NOT NULL,
    [AccountingPeriod]          INT              NOT NULL,
    [DaysOverdue]               INT              NOT NULL,
    [CapitalBalance]            DECIMAL(18,2)    NOT NULL,
    [ExtraBalance]              DECIMAL(18,2)    NOT NULL,
    [InterestBalance]           DECIMAL(18,2)    NOT NULL,
    [DefaultBalance]            DECIMAL(18,2)    NOT NULL,
    [InsuranceBalance]          DECIMAL(18,2)    NOT NULL,
    [AdminBalance]              DECIMAL(18,2)    NOT NULL,
    [OtherBalance]              DECIMAL(18,2)    NOT NULL,
    [PriorCapitalBalance]       DECIMAL(18,2)    NOT NULL,
    [PriorExtraBalance]         DECIMAL(18,2)    NOT NULL,
    [PriorInterestBalance]      DECIMAL(18,2)    NOT NULL,
    [PriorDefaultBalance]       DECIMAL(18,2)    NOT NULL,
    [PriorInsuranceBalance]     DECIMAL(18,2)    NOT NULL,
    [PriorAdminBalance]         DECIMAL(18,2)    NOT NULL,
    [PriorOtherBalance]         DECIMAL(18,2)    NOT NULL,
    [AccruedCapital]            DECIMAL(18,2)    NOT NULL,
    [AccruedExtra]              DECIMAL(18,2)    NOT NULL,
    [AccruedInterest]           DECIMAL(18,2)    NOT NULL,
    [AccruedDefault]            DECIMAL(18,2)    NOT NULL,
    [AccruedInsurance]          DECIMAL(18,2)    NOT NULL,
    [AccruedAdmin]              DECIMAL(18,2)    NOT NULL,
    [AccruedOther]              DECIMAL(18,2)    NOT NULL,
    [PaidCapital]               DECIMAL(18,2)    NOT NULL,
    [PaidExtra]                 DECIMAL(18,2)    NOT NULL,
    [PaidInterest]              DECIMAL(18,2)    NOT NULL,
    [PaidDefault]               DECIMAL(18,2)    NOT NULL,
    [PaidInsurance]             DECIMAL(18,2)    NOT NULL,
    [PaidAdmin]                 DECIMAL(18,2)    NOT NULL,
    [PaidOther]                 DECIMAL(18,2)    NOT NULL,
    [LastLiquidationDate]       DATE             NULL,
    [ExtraNumber]               INT              NOT NULL,
    [CxcFlag]                   INT              NOT NULL,
    [IsManaged]                 NVARCHAR(2)      NOT NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DefaultRecords] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DefaultRecords_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #85. LND_AccrualEntries — Accrual/novation entries
-- Original: cop_caunov
-- ============================================================
CREATE TABLE [dbo].[LND_AccrualEntries] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [AccrualPeriod]             INT              NOT NULL,
    [EntryType]                 NVARCHAR(2)      NOT NULL,
    [Reason]                    NVARCHAR(2)      NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [Installments]              INT              NOT NULL,
    [AffectsCapital]            NVARCHAR(2)      NOT NULL,
    [AffectsInterest]           NVARCHAR(2)      NOT NULL,
    [AffectsExtras]             NVARCHAR(2)      NOT NULL,
    [FixedInstallments]         NVARCHAR(2)      NOT NULL,
    [Authorization]             NVARCHAR(20)     NOT NULL,
    [EntryDate]                 DATE             NOT NULL,
    [Remarks]                   NVARCHAR(MAX)    NULL,
    [UserId]                    NVARCHAR(20)     NOT NULL,
    [SystemDate]                DATE             NOT NULL,
    [Status]                    NVARCHAR(2)      NULL,
    [ExpirationDate]            DATE             NOT NULL,
    [UserFullName]              NVARCHAR(50)     NOT NULL,
    [AppliesExtras]             NVARCHAR(2)      NOT NULL,
    [AppliesToSavings]          NVARCHAR(2)      NULL,
    [AppliesToServices]         NVARCHAR(2)      NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AccrualEntries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AccrualEntries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #86. LND_DefaultLiquidations — Default interest liquidation log
-- Original: cop_liqmor
-- ============================================================
CREATE TABLE [dbo].[LND_DefaultLiquidations] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [LiquidationDate]           DATETIME2        NOT NULL,
    [LiquidationBase]           INT              NOT NULL,
    [LiquidationDays]           INT              NOT NULL,
    [LiquidationAmount]         INT              NOT NULL,
    [LastLiquidationDate]       DATETIME2        NOT NULL,
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [LiquidationRate]           DECIMAL(6,3)     NOT NULL,
    [DeductionClass]            INT              NOT NULL,
    [GraceDays]                 INT              NOT NULL,
    [UsuryRate]                 DECIMAL(6,3)     NOT NULL,
    [LiquidationType]           INT              NOT NULL,
    [SystemDate]                DATETIME2        NOT NULL,
    [AccrualPeriod]             INT              NOT NULL,
    [UserId]                    NVARCHAR(20)     NULL,
    [UserFullName]              NVARCHAR(50)     NOT NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DefaultLiquidations] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DefaultLiquidations_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #87. LND_TransactionCodes — Transaction code catalog
-- Original: cop_codmov
-- ============================================================
CREATE TABLE [dbo].[LND_TransactionCodes] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [TransactionCode]           NVARCHAR(3)      NOT NULL,
    [Name]                      NVARCHAR(50)     NOT NULL,
    [ShortName]                 NVARCHAR(25)     NOT NULL,
    [TransactionType]           NVARCHAR(3)      NOT NULL,
    [AccountCode]               NVARCHAR(15)     NOT NULL,
    [AdjustAccrual]             NVARCHAR(2)      NOT NULL,
    [DebitCreditFlag]           NVARCHAR(2)      NOT NULL,
    [FormatId]                  INT              NOT NULL,
    [ConceptId]                 INT              NOT NULL,
    [SourceId]                  INT              NOT NULL,
    [LegacyCodMovto]            NVARCHAR(3)      NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_TransactionCodes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_TransactionCodes_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_TransactionCodes_Code] UNIQUE ([TransactionCode])
);
GO

-- ============================================================
-- #88. LND_Guarantees — Loan guarantee records
-- Original: cop_garantia
-- ============================================================
CREATE TABLE [dbo].[LND_Guarantees] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [RegistrationNumber]        NVARCHAR(25)     NOT NULL,
    [GuaranteeType]             NVARCHAR(3)      NOT NULL,
    [GuaranteeDescription]      NVARCHAR(MAX)    NOT NULL,
    [CadastralAppraisal]        DECIMAL(18,2)    NOT NULL,
    [CommercialAppraisal]       DECIMAL(18,2)    NOT NULL,
    [HasInsurance]              NVARCHAR(2)      NOT NULL,
    [PolicyNumber]              NVARCHAR(20)     NOT NULL,
    [GuaranteeStartDate]        DATE             NULL,
    [GuaranteeCancelDate]       DATE             NULL,
    [MaturityDate]              DATE             NULL,
    [InsurerIdentification]     NVARCHAR(20)     NOT NULL,
    [InsurerName]               NVARCHAR(50)     NOT NULL,
    [GuaranteeStatus]           NVARCHAR(2)      NOT NULL,
    [UserId]                    NVARCHAR(20)     NOT NULL,
    [InsuredPercentage]         DECIMAL(5,2)     NULL,
    [Guarantor1]                NVARCHAR(20)     NOT NULL,
    [Guarantor2]                NVARCHAR(20)     NOT NULL,
    [Guarantor3]                NVARCHAR(20)     NOT NULL,
    [Guarantor4]                NVARCHAR(20)     NOT NULL,
    [Term]                      INT              NOT NULL,
    [Balance]                   DECIMAL(18,2)    NOT NULL,
    [CdatNumber]                INT              NOT NULL,
    [LegacyCodigoTer]           NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Guarantees] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Guarantees_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #89. LND_LoanApplications — Credit application requests
-- Original: cop_solcre
-- ============================================================
CREATE TABLE [dbo].[LND_LoanApplications] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber]         INT              NOT NULL,
    [ApplicationDate]           DATE             NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [RequestedAmount]           DECIMAL(18,2)    NOT NULL,
    [InterestRate]              DECIMAL(7,6)     NOT NULL,
    [Term]                      INT              NOT NULL,
    [InstallmentAmount]         DECIMAL(18,2)    NOT NULL,
    [MonthlyPayments]           DECIMAL(18,2)    NOT NULL,
    [OverdueBalance]            DECIMAL(18,2)    NOT NULL,
    [AvailableCredit]           DECIMAL(18,2)    NOT NULL,
    [CoopEntryDate]             DATE             NULL,
    [EmployerName]              NVARCHAR(50)     NOT NULL,
    [Position]                  NVARCHAR(60)     NOT NULL,
    [Salary]                    DECIMAL(18,2)    NOT NULL,
    [OtherIncome]               DECIMAL(18,2)    NOT NULL,
    [EmployerEntryDate]         DATE             NULL,
    [MonthlyDeductions]         DECIMAL(18,2)    NOT NULL,
    [MonthlyFixedExpenses]      DECIMAL(18,2)    NOT NULL,
    [AvailableMonthly]          DECIMAL(18,2)    NOT NULL,
    [ContractType]              NVARCHAR(2)      NOT NULL,
    [GuaranteeType]             NVARCHAR(3)      NOT NULL,
    [GuaranteeDescription]      NVARCHAR(MAX)    NULL,
    [CommercialAppraisal]       DECIMAL(18,2)    NOT NULL,
    [CadastralAppraisal]        DECIMAL(18,2)    NOT NULL,
    [IsInsured]                 NVARCHAR(2)      NOT NULL,
    [InsurancePercentage]       DECIMAL(8,4)     NOT NULL,
    [InsuranceExpiryDate]       DATE             NULL,
    [Codeudor1]                 NVARCHAR(20)     NOT NULL,
    [Codeudor2]                 NVARCHAR(20)     NOT NULL,
    [Codeudor3]                 NVARCHAR(20)     NOT NULL,
    [Codeudor4]                 NVARCHAR(20)     NOT NULL,
    [ApprovedAmount]            DECIMAL(18,2)    NOT NULL,
    [ApprovalDate]              DATE             NULL,
    [MinutesNumber]             NVARCHAR(20)     NOT NULL,
    [MinutesDate]               DATE             NULL,
    [ScheduledDate]             DATE             NULL,
    [AdditionalContribution]    DECIMAL(18,2)    NOT NULL,
    [DebtConsolidation]         DECIMAL(18,2)    NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL,
    [UserId]                    NVARCHAR(20)     NULL,
    [RecordDate]                DATE             NOT NULL,
    [SpouseWorks]               NVARCHAR(2)      NOT NULL,
    [SpouseName]                NVARCHAR(50)     NOT NULL,
    [SpouseEmployer]            NVARCHAR(50)     NOT NULL,
    [SpouseSalary]              DECIMAL(18,2)    NOT NULL,
    [SpousePhone]               NVARCHAR(20)     NOT NULL,
    [SpouseEmployerAddress]     NVARCHAR(50)     NOT NULL,
    [SpouseEmployerCity]        NVARCHAR(25)     NOT NULL,
    [SpouseDependents]          INT              NOT NULL,
    [Remarks]                   NVARCHAR(MAX)    NULL,
    [VehicleDescription]        NVARCHAR(25)     NOT NULL,
    [HasVehicle]                NVARCHAR(2)      NOT NULL,
    [OwnsHouse]                 NVARCHAR(2)      NOT NULL,
    [DisbursementDate]          DATE             NULL,
    [IdentificationNumber]      NVARCHAR(20)     NOT NULL,
    [PaymentCycle]              NVARCHAR(2)      NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [InstallmentType]           NVARCHAR(2)      NOT NULL,
    [InterestType]              NVARCHAR(2)      NOT NULL,
    [DeductionType]             NVARCHAR(2)      NOT NULL,
    [ClosingInterestType]       NVARCHAR(2)      NOT NULL,
    [CapitalizationType]        NVARCHAR(2)      NOT NULL,
    [AdminType]                 NVARCHAR(2)      NOT NULL,
    [InsuranceType]             NVARCHAR(3)      NULL,
    [OtherType]                 NVARCHAR(2)      NOT NULL,
    [AdminForm]                 NVARCHAR(3)      NOT NULL,
    [AdminConceptCode]          INT              NOT NULL,
    [InsuranceConceptCode]      INT              NOT NULL,
    [OtherConceptCode]          INT              NOT NULL,
    [AdminRate]                 DECIMAL(12,5)    NOT NULL,
    [InsuranceRate]             DECIMAL(10,5)    NOT NULL,
    [ConceptRate]               DECIMAL(10,6)    NOT NULL,
    [OtherRate]                 DECIMAL(10,6)    NOT NULL,
    [ExtraPaymentAmount]        DECIMAL(18,2)    NOT NULL,
    [ContributionsAmount]       DECIMAL(18,2)    NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [ExtraPercentage]           DECIMAL(5,2)     NOT NULL,
    [AdminInstallment]          DECIMAL(18,2)    NOT NULL,
    [InsuranceInstallment]      DECIMAL(18,2)    NOT NULL,
    [CapitalInstallment]        DECIMAL(18,2)    NOT NULL,
    [InterestInstallment]       DECIMAL(18,2)    NOT NULL,
    [OtherInstallment]          DECIMAL(18,2)    NOT NULL,
    [GracePeriodFlag]           NVARCHAR(2)      NOT NULL,
    [GracePeriodStart]          INT              NOT NULL,
    [GracePeriodStartDate]      DATE             NULL,
    [GracePeriodInstallment]    NVARCHAR(2)      NOT NULL,
    [GracePeriodDays]           INT              NOT NULL,
    [GracePeriodEndDate]        DATE             NULL,
    [GracePeriodCycle]          INT              NOT NULL,
    [ExtraInMonth]              NVARCHAR(2)      NOT NULL,
    [ExtraInAdvance]            NVARCHAR(2)      NOT NULL,
    [FirstPaymentFlag]          NVARCHAR(2)      NOT NULL,
    [SecondPaymentType]         NVARCHAR(2)      NOT NULL,
    [PromissoryNumber]          INT              NOT NULL,
    [PayrollDeductionNumber]    INT              NOT NULL,
    [CdatNumber]                INT              NOT NULL,
    [VariableIncome]            DECIMAL(18,2)    NOT NULL,
    [RentalIncome]              DECIMAL(18,2)    NOT NULL,
    [ThirdPartyDebts]           DECIMAL(18,2)    NOT NULL,
    [AuthorizedFlag]            NVARCHAR(2)      NOT NULL,
    [DiscountCompany]           NVARCHAR(5)      NOT NULL,
    [InsurerIdentification]     NVARCHAR(20)     NULL,
    [InsurerName]               NVARCHAR(50)     NULL,
    [PolicyNumber]              NVARCHAR(20)     NULL,
    [RegistrationNumber]        NVARCHAR(25)     NULL,
    [PensionIncome]             DECIMAL(18,2)    NOT NULL,
    [PensionDeduction]          DECIMAL(18,2)    NOT NULL,
    [ParafiscalDeduction]       DECIMAL(18,2)    NOT NULL,
    [SentToPaymaster]           NVARCHAR(2)      NOT NULL,
    [SentToPaymasterDate]       DATE             NULL,
    [ReceivedFromPaymasterDate] DATE             NOT NULL,
    [AuthorizingUser]           NVARCHAR(20)     NULL,
    [SolicitedInterestRate]     DECIMAL(7,6)     NOT NULL,
    [SolicitedTerm]             INT              NOT NULL,
    [SolicitedPeriodicity]      NVARCHAR(2)      NOT NULL,
    [SolicitedCycle]            NVARCHAR(2)      NOT NULL,
    [SolicitedDeductionType]    NVARCHAR(2)      NOT NULL,
    [EntryUserId]               NVARCHAR(20)     NOT NULL,
    [AuthorizingUserId]         NVARCHAR(20)     NOT NULL,
    [EmployerPayrollDeduction]  DECIMAL(18,2)    NOT NULL,
    [DisbursedAmount]           DECIMAL(18,2)    NOT NULL,
    [DtfRate]                   DECIMAL(10,5)    NOT NULL,
    [SpreadPoints]              DECIMAL(10,5)    NOT NULL,
    [EntityType]                NVARCHAR(3)      NOT NULL,
    [SolicitedInstallment]      DECIMAL(18,2)    NOT NULL,
    [PaymentCapacityPct]        NVARCHAR(2)      NOT NULL,
    [PaymentCapacityDebtPickup] NVARCHAR(3)      NOT NULL,
    [SpouseIncome]              DECIMAL(18,2)    NOT NULL,
    [PersonalExpenses]          DECIMAL(18,2)    NOT NULL,
    [AssetHousing]              DECIMAL(15,3)    NOT NULL,
    [AssetVehicle]              DECIMAL(15,3)    NOT NULL,
    [AssetOther]                DECIMAL(15,3)    NOT NULL,
    [AssetContributions]        DECIMAL(15,3)    NOT NULL,
    [AssetCashBank]             DECIMAL(15,3)    NOT NULL,
    [AssetReceivables]          DECIMAL(15,3)    NOT NULL,
    [AssetSavings]              DECIMAL(15,3)    NOT NULL,
    [TotalAssets]               DECIMAL(15,3)    NOT NULL,
    [LiabilityDebts]            DECIMAL(15,3)    NOT NULL,
    [LiabilityOther]            DECIMAL(15,3)    NOT NULL,
    [LiabilityBankLoans]        DECIMAL(15,3)    NOT NULL,
    [LiabilityMortgage]         DECIMAL(15,3)    NOT NULL,
    [TotalLiabilities]          DECIMAL(15,3)    NOT NULL,
    [Equity]                    DECIMAL(15,3)    NOT NULL,
    [TotalLiabilitiesEquity]    DECIMAL(15,3)    NOT NULL,
    [PayrollCapacity]           DECIMAL(15,3)    NOT NULL,
    [PayrollPercentage]         DECIMAL(15,2)    NOT NULL,
    [PaymentCapacity]           DECIMAL(15,3)    NOT NULL,
    [CashPercentage]            DECIMAL(15,2)    NOT NULL,
    [PaymasterPercentage]       DECIMAL(10,3)    NOT NULL,
    [PayrollLabel]              DECIMAL(15,3)    NOT NULL,
    [CashLabel]                 DECIMAL(15,3)    NOT NULL,
    [DiscoveredAmount]          DECIMAL(10,3)    NOT NULL,
    [IndebtednessLevel]         DECIMAL(15,3)    NOT NULL,
    [ContingencyLevel]          DECIMAL(15,3)    NOT NULL,
    [CapitalAtRisk]             DECIMAL(15,3)    NOT NULL,
    [DeductionCapacity]         DECIMAL(15,3)    NOT NULL,
    [DeductionPercentage]       DECIMAL(15,2)    NOT NULL,
    [PaymasterDeductionType]    NVARCHAR(2)      NULL,
    [SolicitedDtfRate]          DECIMAL(10,5)    NOT NULL,
    [SolicitedSpreadPoints]     DECIMAL(10,5)    NOT NULL,
    [LegacyNumero]              INT              NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_LoanApplications] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_LoanApplications_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_LoanApplications_Number] UNIQUE ([ApplicationNumber])
);
GO

-- ============================================================
-- #90. LND_AuxiliaryApplications — Auxiliary service applications
-- Original: cop_solaux
-- ============================================================
CREATE TABLE [dbo].[LND_AuxiliaryApplications] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [LineCode]                  NVARCHAR(5)      NOT NULL,
    [ApplicationDate]           DATE             NOT NULL,
    [ApprovalDate]              DATE             NULL,
    [PaymentDate]               DATE             NULL,
    [IsApproved]                NVARCHAR(2)      NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL,
    [RequestedAmount]           DECIMAL(18,2)    NOT NULL,
    [ApprovedAmount]            DECIMAL(18,2)    NOT NULL,
    [LineRemarks]               NVARCHAR(MAX)    NULL,
    [ApprovalRemarks]           NVARCHAR(MAX)    NULL,
    [ApplicationRemarks]        NVARCHAR(MAX)    NULL,
    [IsClosed]                  NVARCHAR(2)      NOT NULL,
    [BeneficiaryId]             NVARCHAR(20)     NOT NULL,
    [LegacyIdSolAux]            INT              NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AuxiliaryApplications] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AuxiliaryApplications_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #91. LND_AuxAppInstallments — Installments for auxiliary apps
-- Original: cop_solauxcuota
-- ============================================================
CREATE TABLE [dbo].[LND_AuxAppInstallments] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationId]             INT              NOT NULL,
    [InstallmentNumber]         INT              NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [PaymentDate]               DATE             NOT NULL,
    [Amount]                    DECIMAL(18,3)    NOT NULL,
    [LineCode]                  NVARCHAR(5)      NOT NULL,
    [IsGenerated]               NVARCHAR(2)      NULL,
    [DeathDate]                 DATE             NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AuxAppInstallments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AuxAppInstallments_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #92. LND_AuxAppInstallmentBeneficiaries — Beneficiaries per installment
-- Original: cop_solauxcuotabenef
-- ============================================================
CREATE TABLE [dbo].[LND_AuxAppInstallmentBeneficiaries] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationId]             INT              NOT NULL,
    [InstallmentNumber]         INT              NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [BeneficiaryCode]           NVARCHAR(20)     NOT NULL,
    [PaymentDate]               DATE             NOT NULL,
    [LineCode]                  NVARCHAR(5)      NOT NULL,
    [Amount]                    DECIMAL(18,3)    NOT NULL,
    [Status]                    NVARCHAR(2)      NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AuxAppInstBenef] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AuxAppInstBenef_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #93. LND_ApplicationAssets — Assets declared in applications
-- Original: cop_solbienes
-- ============================================================
CREATE TABLE [dbo].[LND_ApplicationAssets] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber]         INT              NOT NULL,
    [AssetType]                 NVARCHAR(2)      NOT NULL,
    [AssetClass]                NVARCHAR(2)      NOT NULL,
    [Address]                   NVARCHAR(80)     NULL,
    [CityCode]                  INT              NOT NULL,
    [AssetValue]                DECIMAL(18,2)    NOT NULL,
    [Brand]                     NVARCHAR(80)     NULL,
    [Model]                     NVARCHAR(20)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ApplicationAssets] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ApplicationAssets_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #94. LND_ApplicationCodebtors — Co-debtors per application
-- Original: cop_solcodeudor
-- ============================================================
CREATE TABLE [dbo].[LND_ApplicationCodebtors] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationId]             INT              NOT NULL,
    [CodeudorCode]              NVARCHAR(20)     NOT NULL,
    [Salary]                    DECIMAL(18,2)    NULL,
    [OtherIncome]               DECIMAL(18,2)    NULL,
    [RentalIncome]              DECIMAL(18,2)    NULL,
    [VariableIncome]            DECIMAL(18,2)    NULL,
    [EmployerDeductions]        DECIMAL(18,2)    NULL,
    [ThirdPartyDebts]           DECIMAL(18,2)    NULL,
    [OtherDeductions]           DECIMAL(18,2)    NULL,
    [MonthlyAvailable]          DECIMAL(18,2)    NULL,
    [PensionIncome]             DECIMAL(18,2)    NOT NULL,
    [PensionDeduction]          DECIMAL(18,2)    NOT NULL,
    [ParafiscalDeduction]       DECIMAL(18,2)    NOT NULL,
    [PaymentCapacityPct]        NVARCHAR(2)      NOT NULL,
    [PersonalExpenses]          DECIMAL(15,3)    NOT NULL,
    [EmployerDeductionsCash]    DECIMAL(18,2)    NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ApplicationCodebtors] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ApplicationCodebtors_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #95. LND_ApplicationReferences — References per application
-- Original: cop_solreferencia
-- ============================================================
CREATE TABLE [dbo].[LND_ApplicationReferences] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber]         INT              NOT NULL,
    [ReferenceType]             NVARCHAR(2)      NOT NULL,
    [Name]                      NVARCHAR(80)     NOT NULL,
    [Address]                   NVARCHAR(80)     NULL,
    [CityCode]                  INT              NOT NULL,
    [Phone]                     NVARCHAR(25)     NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ApplicationReferences] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ApplicationReferences_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #96. LND_HousingApplicationParams — Housing loan params per app
-- Original: cop_solparviv
-- ============================================================
CREATE TABLE [dbo].[LND_HousingApplicationParams] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber]         INT              NOT NULL,
    [HousingClass]              INT              NOT NULL,
    [HousingType]               INT              NOT NULL,
    [SocialInterest]            NVARCHAR(2)      NOT NULL,
    [HasSubsidy]                NVARCHAR(2)      NOT NULL,
    [NetworkEntity]             INT              NOT NULL,
    [NetworkValue]              BIGINT           NOT NULL,
    [DisbursementType]          INT              NOT NULL,
    [CurrencyType]              INT              NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_HousingApplicationParams] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_HousingApplicationParams_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_HousingAppParams_App] UNIQUE ([ApplicationNumber])
);
GO

-- ============================================================
-- #97. LND_ApplicationExtras — Extra payments in application
-- Original: cop_extrasoli
-- ============================================================
CREATE TABLE [dbo].[LND_ApplicationExtras] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber]         INT              NOT NULL,
    [InstallmentNumber]         INT              NOT NULL,
    [PaymentDate]               DATE             NOT NULL,
    [Amount]                    DECIMAL(18,2)    NOT NULL,
    [PaymentForm]               NVARCHAR(2)      NOT NULL,
    [ExtraType]                 NVARCHAR(3)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ApplicationExtras] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ApplicationExtras_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #98. LND_ApplicationStatuses — Application evaluation status
-- Original: cop_estsol
-- ============================================================
CREATE TABLE [dbo].[LND_ApplicationStatuses] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber]         INT              NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [HasApplication]            NVARCHAR(2)      NOT NULL,
    [HasPromissory]             NVARCHAR(2)      NOT NULL,
    [HasPayrollDeduction]       NVARCHAR(2)      NOT NULL,
    [HasOtherDocs]              NVARCHAR(2)      NOT NULL,
    [ApplicationComplete]       NVARCHAR(2)      NOT NULL,
    [DataUpdated]               NVARCHAR(2)      NOT NULL,
    [ObligationsOk]             NVARCHAR(2)      NOT NULL,
    [PaymentCapacity]           DECIMAL(18,2)    NOT NULL,
    [IncomeProof]               NVARCHAR(2)      NOT NULL,
    [CifinScore]                DECIMAL(10,6)    NOT NULL,
    [IndebtednessLevel]         DECIMAL(15,6)    NOT NULL,
    [AssetQuality]              NVARCHAR(2)      NULL,
    [ContingencyLevel]          DECIMAL(15,6)    NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL,
    [Evaluation]                NVARCHAR(MAX)    NOT NULL,
    [ReferenceVerification]     NVARCHAR(MAX)    NOT NULL,
    [UserId]                    NVARCHAR(20)     NULL,
    [UserFullName]              NVARCHAR(50)     NOT NULL,
    [SystemDate]                DATE             NOT NULL,
    [ClaEne1] NVARCHAR(2) NULL, [ClaEne2] NVARCHAR(2) NULL, [ClaEne3] NVARCHAR(2) NULL, [ClaEne4] NVARCHAR(2) NULL, [ClaEne5] NVARCHAR(2) NULL,
    [ClaFeb1] NVARCHAR(2) NULL, [ClaFeb2] NVARCHAR(2) NULL, [ClaFeb3] NVARCHAR(2) NULL, [ClaFeb4] NVARCHAR(2) NULL, [ClaFeb5] NVARCHAR(2) NULL,
    [ClaMar1] NVARCHAR(2) NULL, [ClaMar2] NVARCHAR(2) NULL, [ClaMar3] NVARCHAR(2) NULL, [ClaMar4] NVARCHAR(2) NULL, [ClaMar5] NVARCHAR(2) NULL,
    [ClaAbr1] NVARCHAR(2) NULL, [ClaAbr2] NVARCHAR(2) NULL, [ClaAbr3] NVARCHAR(2) NULL, [ClaAbr4] NVARCHAR(2) NULL, [ClaAbr5] NVARCHAR(2) NULL,
    [ClaMay1] NVARCHAR(2) NULL, [ClaMay2] NVARCHAR(2) NULL, [ClaMay3] NVARCHAR(2) NULL, [ClaMay4] NVARCHAR(2) NULL, [ClaMay5] NVARCHAR(2) NULL,
    [ClaJun1] NVARCHAR(2) NULL, [ClaJun2] NVARCHAR(2) NULL, [ClaJun3] NVARCHAR(2) NULL, [ClaJun4] NVARCHAR(2) NULL, [ClaJun5] NVARCHAR(2) NULL,
    [ClaJul1] NVARCHAR(2) NULL, [ClaJul2] NVARCHAR(2) NULL, [ClaJul3] NVARCHAR(2) NULL, [ClaJul4] NVARCHAR(2) NULL, [ClaJul5] NVARCHAR(2) NULL,
    [ClaAgo1] NVARCHAR(2) NULL, [ClaAgo2] NVARCHAR(2) NULL, [ClaAgo3] NVARCHAR(2) NULL, [ClaAgo4] NVARCHAR(2) NULL, [ClaAgo5] NVARCHAR(2) NULL,
    [ClaSep1] NVARCHAR(2) NULL, [ClaSep2] NVARCHAR(2) NULL, [ClaSep3] NVARCHAR(2) NULL, [ClaSep4] NVARCHAR(2) NULL, [ClaSep5] NVARCHAR(2) NULL,
    [ClaOct1] NVARCHAR(2) NULL, [ClaOct2] NVARCHAR(2) NULL, [ClaOct3] NVARCHAR(2) NULL, [ClaOct4] NVARCHAR(2) NULL, [ClaOct5] NVARCHAR(2) NULL,
    [ClaNov1] NVARCHAR(2) NULL, [ClaNov2] NVARCHAR(2) NULL, [ClaNov3] NVARCHAR(2) NULL, [ClaNov4] NVARCHAR(2) NULL, [ClaNov5] NVARCHAR(2) NULL,
    [ClaDic1] NVARCHAR(2) NULL, [ClaDic2] NVARCHAR(2) NULL, [ClaDic3] NVARCHAR(2) NULL, [ClaDic4] NVARCHAR(2) NULL, [ClaDic5] NVARCHAR(2) NULL,
    [StudyDate]                 DATETIME2        NOT NULL,
    [PayrollPayment]            DECIMAL(15,3)    NOT NULL,
    [CashPayment]               DECIMAL(15,3)    NOT NULL,
    [AssetHousing]              DECIMAL(15,3)    NOT NULL,
    [AssetVehicle]              DECIMAL(15,3)    NOT NULL,
    [AssetContributions]        DECIMAL(15,3)    NOT NULL,
    [AssetOther]                DECIMAL(15,3)    NOT NULL,
    [TotalAssets]               DECIMAL(15,3)    NOT NULL,
    [LiabilityDebts]            DECIMAL(15,3)    NOT NULL,
    [LiabilityOther]            DECIMAL(15,3)    NOT NULL,
    [TotalLiabilities]          DECIMAL(15,3)    NOT NULL,
    [Equity]                    DECIMAL(15,3)    NOT NULL,
    [TotalLiabilitiesEquity]    DECIMAL(15,3)    NOT NULL,
    [PersonalExpenses]          DECIMAL(15,3)    NOT NULL,
    [DebtConsolidation]         NVARCHAR(2)      NOT NULL,
    [AccountNumber]             INT              NOT NULL,
    [AverageSalary]             DECIMAL(18,2)    NOT NULL,
    [AssetCashBank]             DECIMAL(15,3)    NOT NULL,
    [AssetReceivables]          DECIMAL(15,3)    NOT NULL,
    [LiabilityBankLoans]        DECIMAL(15,3)    NOT NULL,
    [LiabilityMortgage]         DECIMAL(15,3)    NOT NULL,
    [PercentageFlag]            NVARCHAR(2)      NOT NULL,
    [AssetSavings]              DECIMAL(15,3)    NOT NULL,
    [DatacreditoScore]          DECIMAL(10,6)    NOT NULL,
    [CreditLimit]               DECIMAL(18,2)    NULL,
    [DiscoveredAmount]          DECIMAL(15,3)    NOT NULL,
    [DatacreditoRating]         NVARCHAR(3)      NOT NULL,
    [ExternalDebtInstallment]   DECIMAL(15,3)    NOT NULL,
    [ExternalDebtBalance]       DECIMAL(15,3)    NOT NULL,
    [DatacreditoOverdue]        DECIMAL(15,3)    NOT NULL,
    [InsuranceExtra]            DECIMAL(18,6)    NOT NULL,
    [CapitalAtRisk]             DECIMAL(15,3)    NOT NULL,
    [PaymasterPercentage]       DECIMAL(15,2)    NOT NULL,
    [PaymasterDeductionType]    NVARCHAR(2)      NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ApplicationStatuses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ApplicationStatuses_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #99. LND_AuxiliaryApplicationLines — Lines within aux apps
-- Original: cop_linsolaux
-- ============================================================
CREATE TABLE [dbo].[LND_AuxiliaryApplicationLines] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationId]             INT              NOT NULL,
    [LineCode]                  NVARCHAR(5)      NOT NULL,
    [Amount]                    DECIMAL(18,3)    NOT NULL,
    [SortOrder]                 INT              NOT NULL,
    [Installments]              INT              NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AuxiliaryApplicationLines] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AuxiliaryApplicationLines_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #100. LND_PayrollDeductions — Payroll deduction detail per loan
-- Original: cop_nomdes
-- ============================================================
CREATE TABLE [dbo].[LND_PayrollDeductions] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [Period]                    INT              NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [IsAdditional]              NVARCHAR(2)      NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [Description]               NVARCHAR(50)     NOT NULL,
    [PaymentCycle]              NVARCHAR(2)      NOT NULL,
    [ContributionAmount]        DECIMAL(18,2)    NOT NULL,
    [LoanAmount]                DECIMAL(18,2)    NOT NULL,
    [InterestAmount]            DECIMAL(18,2)    NOT NULL,
    [ExtraAmount]               DECIMAL(18,2)    NOT NULL,
    [DefaultAmount]             DECIMAL(18,2)    NOT NULL,
    [InsuranceAmount]           DECIMAL(18,2)    NOT NULL,
    [AdminAmount]               DECIMAL(18,2)    NOT NULL,
    [OtherAmount]               DECIMAL(18,2)    NOT NULL,
    [ContributionApplied]       DECIMAL(18,2)    NOT NULL,
    [LoanApplied]               DECIMAL(18,2)    NOT NULL,
    [InterestApplied]           DECIMAL(18,2)    NOT NULL,
    [ExtraApplied]              DECIMAL(18,2)    NOT NULL,
    [DefaultApplied]            DECIMAL(18,2)    NOT NULL,
    [InsuranceApplied]          DECIMAL(18,2)    NOT NULL,
    [AdminApplied]              DECIMAL(18,2)    NOT NULL,
    [OtherApplied]              DECIMAL(18,2)    NOT NULL,
    [CodeudorCode]              NVARCHAR(20)     NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PayrollDeductions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PayrollDeductions_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #101. LND_DeductionValues — Deduction concept values per person
-- Original: cop_valdesc
-- ============================================================
CREATE TABLE [dbo].[LND_DeductionValues] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [Period]                    INT              NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [IsAdditional]              NVARCHAR(2)      NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [ConceptCode]               NVARCHAR(8)      NOT NULL,
    [Amount]                    DECIMAL(18,2)    NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DeductionValues] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DeductionValues_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #102. LND_PayrollDeductionConcepts — Payroll concept mapping
-- Original: cop_nomconce
-- ============================================================
CREATE TABLE [dbo].[LND_PayrollDeductionConcepts] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PayrollConceptCode]        NVARCHAR(8)      NOT NULL,
    [InterestConceptCode]       NVARCHAR(8)      NOT NULL,
    [ExtraConceptCode]          NVARCHAR(8)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PayrollDeductionConcepts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PayrollDeductionConcepts_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #103. LND_PayrollDeductionPeriods — Payroll deduction period headers
-- Original: cop_nompla
-- ============================================================
CREATE TABLE [dbo].[LND_PayrollDeductionPeriods] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [BranchId]                  NVARCHAR(5)      NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [Period]                    INT              NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [IsAdditional]              NVARCHAR(2)      NOT NULL,
    [Description]               NVARCHAR(50)     NOT NULL,
    [DeductionClass]            NVARCHAR(2)      NOT NULL,
    [StartDate]                 DATE             NOT NULL,
    [EndDate]                   DATE             NOT NULL,
    [PaymentCycle]              NVARCHAR(2)      NOT NULL,
    [ContributionAmount]        DECIMAL(18,2)    NOT NULL,
    [LoanAmount]                DECIMAL(18,2)    NOT NULL,
    [InterestAmount]            DECIMAL(18,2)    NOT NULL,
    [ExtraAmount]               DECIMAL(18,2)    NOT NULL,
    [DefaultAmount]             DECIMAL(18,2)    NOT NULL,
    [InsuranceAmount]           DECIMAL(18,2)    NOT NULL,
    [AdminAmount]               DECIMAL(18,2)    NOT NULL,
    [OtherAmount]               DECIMAL(18,2)    NOT NULL,
    [ArrearsFrom]               INT              NOT NULL,
    [ArrearsTo]                 INT              NOT NULL,
    [ArrearsExtras]             NVARCHAR(2)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PayrollDeductionPeriods] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PayrollDeductionPeriods_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #104. LND_PayrollDeductionEntries — Payroll novation entries
-- Original: cop_nomnov
-- ============================================================
CREATE TABLE [dbo].[LND_PayrollDeductionEntries] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Period]                    INT              NOT NULL,
    [CompanyCode]               NVARCHAR(5)      NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [EntryType]                 NVARCHAR(2)      NOT NULL,
    [ConceptCode]               NVARCHAR(20)     NOT NULL,
    [StartDate]                 NVARCHAR(20)     NOT NULL,
    [EndDate]                   NVARCHAR(20)     NOT NULL,
    [Amount]                    DECIMAL(12,0)    NOT NULL,
    [TotalAmount]               DECIMAL(12,0)    NOT NULL,
    [AccumulatedAmount]         DECIMAL(12,0)    NOT NULL,
    [OrderNumber]               NVARCHAR(5)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PayrollDeductionEntries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PayrollDeductionEntries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #105. LND_SavingsAccounts — Savings account master (cooperative)
-- Original: cop_maeahor
-- ============================================================
CREATE TABLE [dbo].[LND_SavingsAccounts] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [SavingsLineId]             INT              NOT NULL,
    [AccountNumber]             BIGINT           NOT NULL,
    [CreationDate]              DATE             NOT NULL,
    [LegalRepresentative]       NVARCHAR(20)     NOT NULL,
    [RepresentativeName]        NVARCHAR(50)     NOT NULL,
    [CommercialAddress]         NVARCHAR(25)     NOT NULL,
    [Phone]                     NVARCHAR(20)     NOT NULL,
    [CellPhone]                 NVARCHAR(20)     NOT NULL,
    [UniqueAccount]             NVARCHAR(2)      NOT NULL,
    [ExemptionDate]             DATE             NULL,
    [AutoDebit]                 NVARCHAR(2)      NOT NULL,
    [FirstDeductionDate]        DATE             NULL,
    [MaturityDate]              DATE             NULL,
    [DeductionType]             NVARCHAR(2)      NOT NULL,
    [Periodicity]               NVARCHAR(2)      NOT NULL,
    [PaymentCycle]              NVARCHAR(2)      NOT NULL,
    [HasSeal]                   NVARCHAR(2)      NOT NULL,
    [HasProtector]              NVARCHAR(2)      NOT NULL,
    [RegisteredSignatures]      INT              NOT NULL,
    [RequiredSignatures]        INT              NOT NULL,
    [SignatoryId1]              NVARCHAR(20)     NOT NULL,
    [SignatoryId2]              NVARCHAR(20)     NOT NULL,
    [SignatoryId3]              NVARCHAR(20)     NOT NULL,
    [SignatoryName1]            NVARCHAR(50)     NOT NULL,
    [SignatoryName2]            NVARCHAR(50)     NOT NULL,
    [SignatoryName3]            NVARCHAR(50)     NOT NULL,
    [BeneficiaryId1]            NVARCHAR(20)     NOT NULL,
    [BeneficiaryId2]            NVARCHAR(20)     NOT NULL,
    [BeneficiaryId3]            NVARCHAR(20)     NOT NULL,
    [BeneficiaryId4]            NVARCHAR(20)     NOT NULL,
    [BeneficiaryId5]            NVARCHAR(20)     NOT NULL,
    [BeneficiaryName1]          NVARCHAR(50)     NOT NULL,
    [BeneficiaryName2]          NVARCHAR(50)     NOT NULL,
    [BeneficiaryName3]          NVARCHAR(50)     NOT NULL,
    [BeneficiaryName4]          NVARCHAR(50)     NOT NULL,
    [BeneficiaryName5]          NVARCHAR(50)     NOT NULL,
    [BeneficiaryPct1]           DECIMAL(6,3)     NOT NULL,
    [BeneficiaryPct2]           DECIMAL(6,3)     NOT NULL,
    [BeneficiaryPct3]           DECIMAL(6,3)     NOT NULL,
    [BeneficiaryPct4]           DECIMAL(6,3)     NOT NULL,
    [BeneficiaryPct5]           DECIMAL(6,3)     NOT NULL,
    [LegacyNumCuenta]           BIGINT           NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_SavingsAccounts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_SavingsAccounts_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_SavingsAccounts_AccountNumber] UNIQUE ([AccountNumber])
);
GO

-- ============================================================
-- #106. LND_SavingsParameters — Savings line configuration
-- Original: cop_ahorro58
-- ============================================================
CREATE TABLE [dbo].[LND_SavingsParameters] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [SavingsLineId]             INT              NOT NULL,
    [Name]                      NVARCHAR(50)     NOT NULL,
    [ShortName]                 NVARCHAR(25)     NOT NULL,
    [InterestPaymentPeriod]     NVARCHAR(2)      NOT NULL,
    [MinInterestBalance]        DECIMAL(18,2)    NOT NULL,
    [MinTransactionAmount]      DECIMAL(18,2)    NOT NULL,
    [MinAccountBalance]         DECIMAL(18,2)    NOT NULL,
    [InterestPaymentRate]       DECIMAL(9,5)     NOT NULL,
    [MinWithholdingAmount]      DECIMAL(18,2)    NOT NULL,
    [WithholdingRate]           DECIMAL(9,5)     NOT NULL,
    [ClearingDays]              INT              NOT NULL,
    [LiquidationForm]           NVARCHAR(2)      NOT NULL,
    [GraceDays]                 INT              NOT NULL,
    [MaxWithdrawalAmount]       DECIMAL(18,2)    NOT NULL,
    [InterestConceptCode]       NVARCHAR(3)      NOT NULL,
    [WithholdingConceptCode]    NVARCHAR(3)      NOT NULL,
    [PaymentForm]               NVARCHAR(2)      NOT NULL,
    [TaxRate]                   DECIMAL(10,5)    NOT NULL,
    [FourPerMillForm]           NVARCHAR(2)      NOT NULL,
    [FourPerMillConcept]        NVARCHAR(5)      NOT NULL,
    [FourPerMillCeiling]        DECIMAL(18,2)    NOT NULL,
    [FourPerMillVoucher]        NVARCHAR(5)      NOT NULL,
    [Comment]                   NVARCHAR(50)     NOT NULL,
    [MaxCashAmount]             DECIMAL(18,2)    NOT NULL,
    [Consecutive]               DECIMAL(12,0)    NOT NULL,
    [CheckWithdrawalGmf]        INT              NOT NULL,
    [FormatId]                  INT              NOT NULL,
    [ConceptId]                 INT              NOT NULL,
    [SourceId]                  INT              NOT NULL,
    [InterestConceptId]         INT              NOT NULL,
    [OtherClearingDays]         INT              NOT NULL,
    [ValidateWithdrawal]        NVARCHAR(2)      NOT NULL,
    [WithdrawalCeiling]         DECIMAL(15,2)    NOT NULL,
    [ManagesPapForm]            NVARCHAR(2)      NOT NULL,
    [TreasuryAccount]           NVARCHAR(15)     NOT NULL,
    [LegacyLinCred]             INT              NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_SavingsParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_SavingsParameters_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_SavingsParameters_LineId] UNIQUE ([SavingsLineId])
);
GO

-- ============================================================
-- #107. LND_InterestRates — Interest rate snapshots per period/line
-- Original: cop_claint
-- ============================================================
CREATE TABLE [dbo].[LND_InterestRates] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Period]                    INT              NOT NULL,
    [CreditLineCode]            NVARCHAR(5)      NOT NULL,
    [PortfolioBalance]          DECIMAL(15,2)    NOT NULL,
    [CostCenterId]              NVARCHAR(10)     NOT NULL,
    [PortfolioClass]            INT              NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_InterestRates] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_InterestRates_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #108. LND_CollectionCases — Collection management cases
-- Original: cop_maegescob
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionCases] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Period]                    INT              NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [UserId]                    NVARCHAR(20)     NOT NULL,
    [PromiseDate]               DATE             NULL,
    [ManagementDate]            DATE             NOT NULL,
    [ManagementStartDate]       DATE             NULL,
    [Description]               NVARCHAR(MAX)    NOT NULL,
    [Status]                    NVARCHAR(2)      NOT NULL,
    [TotalOverdueAmount]        DECIMAL(16,3)    NOT NULL,
    [EmailSent]                 NVARCHAR(2)      NULL,
    [CreditLineFrom]            INT              NOT NULL,
    [CreditLineTo]              INT              NOT NULL,
    [DaysFrom]                  INT              NOT NULL,
    [DaysTo]                    INT              NOT NULL,
    [DeductionClass]            NVARCHAR(2)      NOT NULL,
    [CompanyFrom]               NVARCHAR(5)      NOT NULL,
    [CompanyTo]                 NVARCHAR(5)      NOT NULL,
    [LegalCollection]           NVARCHAR(2)      NOT NULL,
    [IsManaged]                 NVARCHAR(2)      NOT NULL,
    [IsCumulative]              NVARCHAR(2)      NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionCases] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionCases_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #109. LND_CollectionMasters — Collection detail per loan
-- Original: cop_gesmaes
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionMasters] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Period]                    INT              NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           INT              NOT NULL,
    [UserId]                    NVARCHAR(20)     NULL,
    [ManagementDate]            DATE             NOT NULL,
    [PaymentDate]               DATE             NOT NULL,
    [TotalBalance]              DECIMAL(18,2)    NOT NULL,
    [OverdueCapital]            DECIMAL(18,2)    NOT NULL,
    [OverdueInterest]           DECIMAL(18,2)    NOT NULL,
    [OverdueInsurance]          DECIMAL(18,2)    NOT NULL,
    [OverdueAdmin]              DECIMAL(18,2)    NOT NULL,
    [AccumulatedDefault]        DECIMAL(18,2)    NOT NULL,
    [InstallmentAmount]         DECIMAL(18,2)    NOT NULL,
    [DaysOverdue]               DECIMAL(18,2)    NOT NULL,
    [CollectionCaseId]          BIGINT           NOT NULL,
    [OverdueExtras]             DECIMAL(18,2)    NOT NULL,
    [OverdueOther]              DECIMAL(18,2)    NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionMasters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionMasters_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #110. LND_CollectionPeriods — Collection period params per user
-- Original: cop_gespara
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionPeriods] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Period]                    INT              NOT NULL,
    [UserId]                    NVARCHAR(15)     NOT NULL,
    [LastPersonCode]            NVARCHAR(20)     NOT NULL,
    [DaysFrom]                  INT              NOT NULL,
    [DaysTo]                    INT              NOT NULL,
    [CreditLineFrom]            INT              NOT NULL,
    [CreditLineTo]              INT              NOT NULL,
    [DeductionClass]            NVARCHAR(2)      NOT NULL,
    [CompanyFrom]               NVARCHAR(5)      NOT NULL,
    [CompanyTo]                 NVARCHAR(5)      NOT NULL,
    [SortOrder]                 NVARCHAR(2)      NOT NULL,
    [LegalCollection]           NVARCHAR(2)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionPeriods] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionPeriods_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #111. LND_CollectionDateEntries — Collection date entries
-- Original: cop_novfecgestion
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionDateEntries] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CollectionCaseId]          BIGINT           NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [EntryDate]                 DATE             NOT NULL,
    [PromiseDate]               DATE             NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionDateEntries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionDateEntries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #112. LND_CollectionNotices — Collection notice headers
-- Original: cop_maecircobro
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionNotices] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [NoticeNumber]              NVARCHAR(3)      NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [Period]                    INT              NOT NULL,
    [ConceptClass]              NVARCHAR(2)      NOT NULL,
    [NoticeDate]                DATETIME2        NOT NULL,
    [Address]                   NVARCHAR(80)     NOT NULL,
    [Phone]                     NVARCHAR(60)     NULL,
    [CityCode]                  INT              NOT NULL,
    [TotalAmount]               DECIMAL(18,2)    NOT NULL,
    [Detail1]                   NVARCHAR(MAX)    NULL,
    [Detail2]                   NVARCHAR(MAX)    NULL,
    [DaysFrom]                  INT              NULL,
    [DaysTo]                    INT              NULL,
    [SendToCodeudor]            NVARCHAR(2)      NOT NULL,
    [TrailingLegend]            NVARCHAR(2)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionNotices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionNotices_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #113. LND_CollectionNoticeDetails — Line items per notice
-- Original: cop_detcircobro
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionNoticeDetails] (
    [Id]                        BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [NoticeNumber]              NVARCHAR(3)      NOT NULL,
    [Period]                    INT              NOT NULL,
    [ConceptClass]              NVARCHAR(2)      NOT NULL,
    [PersonCode]                NVARCHAR(20)     NOT NULL,
    [CreditLineId]              INT              NOT NULL,
    [PortfolioNumber]           BIGINT           NOT NULL,
    [Cycle]                     INT              NOT NULL,
    [CapitalBalance]            DECIMAL(18,2)    NOT NULL,
    [ExtraBalance]              DECIMAL(18,2)    NOT NULL,
    [InterestBalance]           DECIMAL(18,2)    NOT NULL,
    [DefaultBalance]            DECIMAL(18,2)    NOT NULL,
    [InsuranceBalance]          DECIMAL(18,2)    NOT NULL,
    [AdminBalance]              DECIMAL(18,2)    NOT NULL,
    [OtherBalance]              DECIMAL(18,2)    NOT NULL,
    [DaysOverdue]               INT              NOT NULL,
    [TotalBalance]              DECIMAL(18,2)    NOT NULL,
    [MaturityDate]              DATETIME2        NOT NULL,
    [Codeudor1] NVARCHAR(20) NOT NULL, [Phone1] NVARCHAR(20) NOT NULL, [Address1] NVARCHAR(80) NOT NULL, [City1] INT NOT NULL,
    [Codeudor2] NVARCHAR(20) NOT NULL, [Phone2] NVARCHAR(20) NOT NULL, [Address2] NVARCHAR(80) NOT NULL, [City2] INT NOT NULL,
    [Codeudor3] NVARCHAR(20) NOT NULL, [Phone3] NVARCHAR(20) NOT NULL, [Address3] NVARCHAR(80) NOT NULL, [City3] INT NOT NULL,
    [Codeudor4] NVARCHAR(20) NOT NULL, [Phone4] NVARCHAR(20) NOT NULL, [Address4] NVARCHAR(80) NOT NULL, [City4] INT NOT NULL,
    [OnlyDefaulted]             NVARCHAR(2)      NOT NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionNoticeDetails] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionNoticeDetails_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #114. LND_CollectionNoticeParams — Notice templates
-- Original: cop_paracircular
-- ============================================================
CREATE TABLE [dbo].[LND_CollectionNoticeParams] (
    [Id]                        INT              IDENTITY(1,1) NOT NULL,
    [PublicId]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [NoticeCode]                NVARCHAR(3)      NOT NULL,
    [DaysFrom]                  INT              NOT NULL,
    [DaysTo]                    INT              NOT NULL,
    [Detail1]                   NVARCHAR(MAX)    NOT NULL,
    [Detail2]                   NVARCHAR(MAX)    NOT NULL,
    [CreatorName]               NVARCHAR(80)     NOT NULL,
    [Position]                  NVARCHAR(60)     NOT NULL,
    [SignatureImage]            VARBINARY(MAX)   NULL,
    [CreatedBy]                 NVARCHAR(100)    NOT NULL DEFAULT SYSTEM_USER,
    [CreatedAt]                 DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy]                 NVARCHAR(100)    NULL,
    [UpdatedAt]                 DATETIME2        NULL,
    [IsActive]                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CollectionNoticeParams] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CollectionNoticeParams_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_CollectionNoticeParams_Code] UNIQUE ([NoticeCode])
);
GO

-- ============================================================
-- #115-#158 and #159-#167 and #270: Remaining tables
-- ============================================================

-- #115. LND_RiskAssessments (cop_riesgo)
CREATE TABLE [dbo].[LND_RiskAssessments] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] NVARCHAR(20) NOT NULL,
    [InstallmentNumber] INT NOT NULL, [Period] NVARCHAR(8) NOT NULL,
    [InstallmentAmount] DECIMAL(14,2) NOT NULL, [ExtraInstallment] DECIMAL(14,2) NOT NULL,
    [InterestAmount] DECIMAL(14,2) NOT NULL, [InsuranceAmount] DECIMAL(14,2) NOT NULL,
    [AdminAmount] DECIMAL(14,2) NOT NULL, [CapitalPayment] DECIMAL(14,2) NOT NULL,
    [Balance] DECIMAL(14,2) NOT NULL, [TotalBalance] DECIMAL(14,2) NOT NULL, [PortfolioClass] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_RiskAssessments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_RiskAssessments_PublicId] UNIQUE ([PublicId])
);
GO

-- #116. LND_PreviousInstallments (cop_cuoant)
CREATE TABLE [dbo].[LND_PreviousInstallments] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL,
    [AccrualPeriod] INT NOT NULL, [AccountingPeriod] INT NOT NULL,
    [InstallmentAmount] DECIMAL(18,2) NOT NULL, [ExtraAmount] DECIMAL(18,2) NOT NULL,
    [AdvanceCapital] DECIMAL(18,2) NOT NULL, [AdvanceInterest] DECIMAL(18,2) NOT NULL,
    [AdvanceExtra] DECIMAL(18,2) NOT NULL, [AdvanceInsurance] DECIMAL(18,2) NOT NULL,
    [AdvanceAdmin] DECIMAL(18,2) NOT NULL, [AdvanceOther] DECIMAL(18,2) NOT NULL,
    [AdvanceDate] DATE NOT NULL, [Status] NVARCHAR(2) NOT NULL, [TransactionSequence] BIGINT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PreviousInstallments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PreviousInstallments_PublicId] UNIQUE ([PublicId])
);
GO

-- #117. LND_ProvisionParameters (cop_parprov)
CREATE TABLE [dbo].[LND_ProvisionParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Period] INT NOT NULL, [Code] INT NOT NULL,
    [RateB] DECIMAL(6,3) NOT NULL, [RateC] DECIMAL(6,3) NOT NULL, [RateD] DECIMAL(6,3) NOT NULL, [RateE] DECIMAL(6,3) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ProvisionParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ProvisionParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- #118. LND_AssociateWithdrawals (cop_retiros)
CREATE TABLE [dbo].[LND_AssociateWithdrawals] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [EntryDate] DATE NOT NULL,
    [PriorStatus] NVARCHAR(2) NOT NULL, [CurrentStatus] NVARCHAR(2) NOT NULL,
    [ReasonCode] NVARCHAR(5) NOT NULL, [WithdrawalReasonId] INT NOT NULL,
    [SystemDate] DATETIME2 NOT NULL, [UserFullName] NVARCHAR(50) NOT NULL, [Period] INT NOT NULL,
    [ExpirationDate] DATE NOT NULL, [CurrentClass] NVARCHAR(2) NULL, [PriorClass] NVARCHAR(2) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AssociateWithdrawals] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AssociateWithdrawals_PublicId] UNIQUE ([PublicId])
);
GO

-- #119. LND_Minutes (cop_acta)
CREATE TABLE [dbo].[LND_Minutes] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [MinutesType] NVARCHAR(2) NOT NULL, [MinutesNumber] NVARCHAR(20) NOT NULL,
    [OpeningDate] DATETIME2 NOT NULL, [ClosingDate] DATETIME2 NULL, [Description] NVARCHAR(MAX) NULL,
    [Status] NVARCHAR(2) NOT NULL, [UserId] NVARCHAR(20) NOT NULL, [SystemDate] DATETIME2 NOT NULL, [EntityType] NVARCHAR(2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Minutes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Minutes_PublicId] UNIQUE ([PublicId])
);
GO

-- #120. LND_MinuteAttendees (cop_asoacta)
CREATE TABLE [dbo].[LND_MinuteAttendees] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [MinutesType] NVARCHAR(2) NOT NULL, [MinutesNumber] NVARCHAR(20) NOT NULL,
    [PersonCode] NVARCHAR(20) NOT NULL, [SystemDate] DATETIME2 NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_MinuteAttendees] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_MinuteAttendees_PublicId] UNIQUE ([PublicId])
);
GO

-- #121. LND_LoanRestructurings (cop_maerest)
CREATE TABLE [dbo].[LND_LoanRestructurings] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL,
    [OriginalPersonCode] NVARCHAR(20) NOT NULL, [OriginalCreditLineId] INT NOT NULL, [OriginalPortfolioNumber] BIGINT NOT NULL,
    [Category] NVARCHAR(2) NOT NULL, [RestructureDate] DATE NULL, [Amount] DECIMAL(18,2) NULL,
    [SystemDate] DATE NULL, [UserId] NVARCHAR(20) NOT NULL, [TimesRestructured] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_LoanRestructurings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_LoanRestructurings_PublicId] UNIQUE ([PublicId])
);
GO

-- #122. LND_ShortLongTermPortfolio (cop_carteraclp)
CREATE TABLE [dbo].[LND_ShortLongTermPortfolio] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [PersonName] NVARCHAR(60) NULL,
    [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL, [AccountCode] NVARCHAR(15) NOT NULL,
    [Balance] DECIMAL(18,2) NULL,
    [Month1] DECIMAL(18,2) NULL, [Month2] DECIMAL(18,2) NULL, [Month3] DECIMAL(18,2) NULL, [Month4] DECIMAL(18,2) NULL,
    [Month5] DECIMAL(18,2) NULL, [Month6] DECIMAL(18,2) NULL, [Month7] DECIMAL(18,2) NULL, [Month8] DECIMAL(18,2) NULL,
    [Month9] DECIMAL(18,2) NULL, [Month10] DECIMAL(18,2) NULL, [Month11] DECIMAL(18,2) NULL, [Month12] DECIMAL(18,2) NULL,
    [MoreThan12] DECIMAL(18,2) NULL, [Total] DECIMAL(18,2) NULL,
    [Description] NVARCHAR(60) NULL, [AffectsFlag] NVARCHAR(20) NULL, [FogaClass] NVARCHAR(3) NULL,
    [CompanyName] NVARCHAR(60) NULL, [CostCenterName] NVARCHAR(60) NULL, [Period] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ShortLongTermPortfolio] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ShortLongTermPortfolio_PublicId] UNIQUE ([PublicId])
);
GO

-- #123. LND_AccrualPeriods (cop_percau)
CREATE TABLE [dbo].[LND_AccrualPeriods] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CompanyCode] NVARCHAR(5) NOT NULL, [BranchId] NVARCHAR(5) NOT NULL, [CostCenterId] NVARCHAR(10) NOT NULL,
    [Periodicity] NVARCHAR(2) NOT NULL, [PaymentCycle] NVARCHAR(2) NOT NULL, [AccrualPeriod] INT NOT NULL,
    [StartDate] DATE NOT NULL, [EndDate] DATE NOT NULL, [AccruedAmount] DECIMAL(18,2) NOT NULL,
    [UsuryRate] DECIMAL(6,3) NOT NULL, [AccrualScope] NVARCHAR(3) NOT NULL, [AccrualService] NVARCHAR(2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AccrualPeriods] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AccrualPeriods_PublicId] UNIQUE ([PublicId])
);
GO

-- #124. LND_InsurancePolicies (cop_seguros + cop_seguross)
CREATE TABLE [dbo].[LND_InsurancePolicies] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL,
    [Balance] DECIMAL(15,2) NOT NULL, [InsuranceAmount] DECIMAL(15,2) NOT NULL,
    [InsuranceDate] DATE NOT NULL, [ExpirationDate] DATE NOT NULL,
    [SystemDate] DATETIME2 NOT NULL, [UserId] NVARCHAR(20) NULL, [UserFullName] NVARCHAR(50) NOT NULL,
    [IsHistorical] BIT NOT NULL DEFAULT 0,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_InsurancePolicies] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_InsurancePolicies_PublicId] UNIQUE ([PublicId])
);
GO

-- #125. LND_InsuranceBeneficiaries (cop_benefseg)
CREATE TABLE [dbo].[LND_InsuranceBeneficiaries] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL,
    [BeneficiaryId] NVARCHAR(15) NOT NULL, [RelationshipCode] NVARCHAR(5) NOT NULL,
    [DocumentType] NVARCHAR(3) NOT NULL, [Name] NVARCHAR(60) NOT NULL, [BirthDate] DATE NOT NULL,
    [InsurancePercentage] DECIMAL(6,3) NOT NULL, [Phone] NVARCHAR(20) NOT NULL, [Address] NVARCHAR(60) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_InsuranceBeneficiaries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_InsuranceBeneficiaries_PublicId] UNIQUE ([PublicId])
);
GO

-- #126. LND_ContributionReductions (cop_redapo + cop_redaportes)
CREATE TABLE [dbo].[LND_ContributionReductions] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [SavingsLineId] INT NOT NULL, [Period] INT NOT NULL,
    [OpeningBalance] DECIMAL(15,2) NOT NULL,
    [DayBalance1] DECIMAL(15,2) NOT NULL, [DayBalance2] DECIMAL(15,2) NOT NULL, [DayBalance3] DECIMAL(15,2) NOT NULL,
    [DayBalance4] DECIMAL(15,2) NOT NULL, [DayBalance5] DECIMAL(15,2) NOT NULL, [DayBalance6] DECIMAL(15,2) NOT NULL,
    [DayBalance7] DECIMAL(15,2) NOT NULL, [DayBalance8] DECIMAL(15,2) NOT NULL, [DayBalance9] DECIMAL(15,2) NOT NULL,
    [DayBalance10] DECIMAL(15,2) NOT NULL, [DayBalance11] DECIMAL(15,2) NOT NULL, [DayBalance12] DECIMAL(15,2) NOT NULL,
    [DayBalance13] DECIMAL(15,2) NOT NULL, [DayBalance14] DECIMAL(15,2) NOT NULL, [DayBalance15] DECIMAL(15,2) NOT NULL,
    [DayBalance16] DECIMAL(15,2) NOT NULL, [DayBalance17] DECIMAL(15,2) NOT NULL, [DayBalance18] DECIMAL(15,2) NOT NULL,
    [DayBalance19] DECIMAL(15,2) NOT NULL, [DayBalance20] DECIMAL(15,2) NOT NULL, [DayBalance21] DECIMAL(15,2) NOT NULL,
    [DayBalance22] DECIMAL(15,2) NOT NULL, [DayBalance23] DECIMAL(15,2) NOT NULL, [DayBalance24] DECIMAL(15,2) NOT NULL,
    [DayBalance25] DECIMAL(15,2) NOT NULL, [DayBalance26] DECIMAL(15,2) NOT NULL, [DayBalance27] DECIMAL(15,2) NOT NULL,
    [DayBalance28] DECIMAL(15,2) NOT NULL, [DayBalance29] DECIMAL(15,2) NOT NULL, [DayBalance30] DECIMAL(15,2) NOT NULL,
    [DayBalance31] DECIMAL(15,2) NOT NULL, [Average] DECIMAL(15,2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ContributionReductions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ContributionReductions_PublicId] UNIQUE ([PublicId])
);
GO

-- #127. LND_ContributionReductionParams (cop_parredaportes)
CREATE TABLE [dbo].[LND_ContributionReductionParams] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CutoffPeriod] INT NOT NULL, [AccumulationConcept] INT NOT NULL, [ExcessAmount] DECIMAL(15,0) NOT NULL,
    [SignatoryName] NVARCHAR(60) NOT NULL, [Position] NVARCHAR(40) NOT NULL, [MemoDetail] NVARCHAR(MAX) NOT NULL,
    [ReductionVoucher] NVARCHAR(5) NOT NULL, [WithholdingAccumConcept] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ContributionReductionParams] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ContribRedParams_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_ContribRedParams_Period] UNIQUE ([CutoffPeriod])
);
GO

-- #128. LND_AssociateActivities (cop_actiaso + cop_actirecrea + cop_novactividad)
CREATE TABLE [dbo].[LND_AssociateActivities] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ActivityType] NVARCHAR(2) NOT NULL, [ActivityCode] NVARCHAR(20) NOT NULL,
    [PersonCode] NVARCHAR(20) NOT NULL, [BeneficiaryId] NVARCHAR(20) NOT NULL,
    [EnrollmentDate] DATE NOT NULL, [Remarks] NVARCHAR(120) NULL,
    [Attended] NVARCHAR(2) NULL, [AttendanceDate] DATETIME2 NULL,
    [ActivityStartDate] DATETIME2 NULL, [ActivityEndDate] DATETIME2 NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AssociateActivities] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AssociateActivities_PublicId] UNIQUE ([PublicId])
);
GO

-- #129. LND_ActivityEnrollments (cop_novactividad — nueva)
CREATE TABLE [dbo].[LND_ActivityEnrollments] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ActivityCode] NVARCHAR(20) NOT NULL, [PersonCode] NVARCHAR(20) NOT NULL,
    [BeneficiaryId] NVARCHAR(20) NOT NULL, [RegistrationDate] DATETIME2 NOT NULL, [EntryType] NVARCHAR(2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ActivityEnrollments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ActivityEnrollments_PublicId] UNIQUE ([PublicId])
);
GO

-- #130. LND_PersonAssets (cop_maenitbienes)
CREATE TABLE [dbo].[LND_PersonAssets] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [AssetType] NVARCHAR(2) NOT NULL, [AssetClass] NVARCHAR(2) NOT NULL,
    [Address] NVARCHAR(80) NULL, [CityCode] INT NOT NULL, [AssetValue] DECIMAL(18,2) NOT NULL,
    [Brand] NVARCHAR(80) NULL, [Model] NVARCHAR(20) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PersonAssets] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PersonAssets_PublicId] UNIQUE ([PublicId])
);
GO

-- #131. LND_BiometricRecords (cop_huellafirma)
CREATE TABLE [dbo].[LND_BiometricRecords] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [SignatureImage] VARBINARY(MAX) NOT NULL,
    [FingerprintCode] DECIMAL(10,0) NOT NULL, [FingerprintData] VARBINARY(MAX) NOT NULL,
    [FingerprintString] NVARCHAR(500) NOT NULL, [PhotoImage] VARBINARY(MAX) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_BiometricRecords] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_BiometricRecords_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_BiometricRecords_Person] UNIQUE ([PersonCode])
);
GO

-- #132. LND_Blacklist (cop_listanegra)
CREATE TABLE [dbo].[LND_Blacklist] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [ListCode] INT NOT NULL, [EntryDate] DATE NOT NULL,
    [Status] NVARCHAR(2) NOT NULL, [IdType] NVARCHAR(2) NOT NULL, [IdentificationNumber] NVARCHAR(20) NOT NULL,
    [PersonType] NVARCHAR(2) NULL, [CompanyName] NVARCHAR(80) NOT NULL, [PersonName] NVARCHAR(80) NULL,
    [Nationality] INT NOT NULL, [Address] NVARCHAR(80) NOT NULL, [Phone] NVARCHAR(20) NOT NULL,
    [Remarks] NVARCHAR(MAX) NULL, [IsImported] NVARCHAR(2) NOT NULL, [ExpirationDate] DATETIME2 NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Blacklist] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Blacklist_PublicId] UNIQUE ([PublicId])
);
GO

-- #133. LND_MoneyLaunderingDeclarations (cop_declavado)
CREATE TABLE [dbo].[LND_MoneyLaunderingDeclarations] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [DeclarationId] BIGINT NOT NULL, [DeclarationDate] DATETIME2 NOT NULL, [PersonCode] NVARCHAR(20) NOT NULL,
    [EconomicActivity] NVARCHAR(40) NOT NULL, [VoucherType] NVARCHAR(5) NOT NULL, [DocumentNumber] BIGINT NOT NULL,
    [TransactionSequence] INT NOT NULL, [OperationType] NVARCHAR(3) NOT NULL, [OperationDetail] NVARCHAR(3) NOT NULL,
    [AffectedProduct] NVARCHAR(25) NOT NULL, [Amount] DECIMAL(18,2) NOT NULL,
    [ClientId] NVARCHAR(20) NOT NULL, [ClientIdType] NVARCHAR(2) NOT NULL,
    [ClientName] NVARCHAR(120) NOT NULL, [ClientSurname] NVARCHAR(120) NOT NULL,
    [Address] NVARCHAR(80) NOT NULL, [Phone] NVARCHAR(40) NULL, [Remarks] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_MoneyLaunderingDecl] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_MoneyLaunderingDecl_PublicId] UNIQUE ([PublicId])
);
GO

-- #134. LND_ExtraPaymentBalances (cop_salextras)
CREATE TABLE [dbo].[LND_ExtraPaymentBalances] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL,
    [ExtraNumber] INT NOT NULL, [Period] INT NOT NULL,
    [Balance] DECIMAL(18,2) NOT NULL, [OpeningBalance] DECIMAL(18,2) NOT NULL,
    [DebitAmount] DECIMAL(18,2) NOT NULL, [CreditAmount] DECIMAL(18,2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ExtraPaymentBalances] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ExtraPaymentBalances_PublicId] UNIQUE ([PublicId])
);
GO

-- #135. LND_InvoiceDetails (cop_detallefactura)
CREATE TABLE [dbo].[LND_InvoiceDetails] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [DetailCode] NVARCHAR(2) NOT NULL, [DetailText] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_InvoiceDetails] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_InvoiceDetails_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_InvoiceDetails_Code] UNIQUE ([DetailCode])
);
GO

-- #136. LND_PortfolioInvoices (cop_facturacartera)
CREATE TABLE [dbo].[LND_PortfolioInvoices] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [InvoiceNumber] DECIMAL(20,0) NOT NULL, [Description] NVARCHAR(120) NOT NULL,
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] DECIMAL(20,0) NOT NULL,
    [Period] INT NOT NULL, [VoucherType] NVARCHAR(5) NOT NULL, [DocumentNumber] BIGINT NOT NULL, [Amount] DECIMAL(18,2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PortfolioInvoices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PortfolioInvoices_PublicId] UNIQUE ([PublicId])
);
GO

-- #137. LND_InvoiceMasters (cop_maefact)
CREATE TABLE [dbo].[LND_InvoiceMasters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NULL, [AccountingPeriod] INT NULL, [Cycle] INT NULL,
    [InvoicedAmount] DECIMAL(18,3) NULL, [PaymentDeadline] DATE NULL,
    [Barcode] NVARCHAR(120) NULL, [UserId] NVARCHAR(20) NULL, [SystemDate] DATETIME2 NULL,
    [InvoiceCode] NVARCHAR(5) NULL, [EncodedData] NVARCHAR(200) NULL,
    [LastPaymentAmount] DECIMAL(18,2) NOT NULL, [LastPaymentDate] NVARCHAR(20) NOT NULL,
    [PaymentType] NVARCHAR(3) NOT NULL, [InvoiceConsecutive] BIGINT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_InvoiceMasters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_InvoiceMasters_PublicId] UNIQUE ([PublicId])
);
GO

-- #138. LND_InvoiceLineItems (cop_detfact)
CREATE TABLE [dbo].[LND_InvoiceLineItems] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [InvoiceId] BIGINT NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL, [AccrualPeriod] INT NOT NULL,
    [CapitalBalance] DECIMAL(18,3) NULL, [ExtraBalance] DECIMAL(18,3) NULL,
    [InterestBalance] DECIMAL(18,3) NULL, [DefaultBalance] DECIMAL(18,3) NULL, [InsuranceBalance] DECIMAL(18,3) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_InvoiceLineItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_InvoiceLineItems_PublicId] UNIQUE ([PublicId])
);
GO

-- #139. LND_AssociateDiseases (cop_enfermedadAsoc)
CREATE TABLE [dbo].[LND_AssociateDiseases] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [DiseaseCode] INT NOT NULL,
    [SystemDate] DATETIME2 NULL, [UserId] NVARCHAR(60) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_AssociateDiseases] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_AssociateDiseases_PublicId] UNIQUE ([PublicId])
);
GO

-- #140. LND_WithdrawalStatuses (cop_estretiro)
CREATE TABLE [dbo].[LND_WithdrawalStatuses] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [RequestDate] DATE NOT NULL, [ReasonCode] NVARCHAR(5) NOT NULL,
    [Status] NVARCHAR(2) NOT NULL, [EffectiveDate] DATE NULL, [Remarks] NVARCHAR(MAX) NULL, [Period] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_WithdrawalStatuses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_WithdrawalStatuses_PublicId] UNIQUE ([PublicId])
);
GO

-- #141. LND_HousingParameters (cop_parviv)
CREATE TABLE [dbo].[LND_HousingParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [CreditLineId] INT NOT NULL, [PortfolioNumber] BIGINT NOT NULL,
    [HousingClass] INT NOT NULL, [HousingType] INT NOT NULL, [SocialInterest] NVARCHAR(2) NOT NULL,
    [HasSubsidy] NVARCHAR(2) NOT NULL, [NetworkEntity] INT NOT NULL, [NetworkValue] BIGINT NOT NULL,
    [DisbursementType] INT NOT NULL, [CurrencyType] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_HousingParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_HousingParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- #142. LND_SiplaParameters (cop_param_sipla)
CREATE TABLE [dbo].[LND_SiplaParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ConceptCode] NVARCHAR(3) NOT NULL, [CompanyCode] NVARCHAR(5) NOT NULL,
    [MonthlyCreditMoves] DECIMAL(4,2) NOT NULL, [MaxBalance] DECIMAL(4,2) NOT NULL,
    [AccountCount] INT NOT NULL, [AnnualTransactions] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_SiplaParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_SiplaParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- #143. LND_SiplaGroupParameters (cop_param_grup_sipla)
CREATE TABLE [dbo].[LND_SiplaGroupParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ConceptCode] NVARCHAR(3) NOT NULL, [CompanyCode] NVARCHAR(5) NOT NULL, [CreditLineId] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_SiplaGroupParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_SiplaGroupParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- #144. LND_UnusualTransactions (cop_sipla_inusuales)
CREATE TABLE [dbo].[LND_UnusualTransactions] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ConceptCode] NVARCHAR(3) NOT NULL, [PersonCode] NVARCHAR(20) NOT NULL, [CompanyCode] NVARCHAR(5) NOT NULL,
    [Period] NVARCHAR(8) NOT NULL, [MonthlyCreditMoves] DECIMAL(18,2) NOT NULL, [MaxBalance] DECIMAL(18,2) NOT NULL,
    [AccountCount] INT NOT NULL, [AnnualTransactions] INT NOT NULL,
    [EntryDate] DATETIME2 NOT NULL, [UnusualType] NVARCHAR(3) NOT NULL,
    [VoucherType] NVARCHAR(5) NULL, [DocumentNumber] BIGINT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_UnusualTransactions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_UnusualTransactions_PublicId] UNIQUE ([PublicId])
);
GO

-- #145. LND_UnusualTransactionEntries (cop_Sipla_Novedades)
CREATE TABLE [dbo].[LND_UnusualTransactionEntries] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [UnusualTransactionId] INT NOT NULL, [EntryDate] DATETIME2 NOT NULL,
    [CurrentStatus] NVARCHAR(2) NOT NULL, [PriorStatus] NVARCHAR(2) NULL,
    [Remarks] NVARCHAR(250) NOT NULL, [RegisteredBy] NVARCHAR(20) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_UnusualTransactionEntries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_UnusualTransactionEntries_PublicId] UNIQUE ([PublicId])
);
GO

-- #146. LND_ScoringParameters (cop_paramscoring)
CREATE TABLE [dbo].[LND_ScoringParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CriterionCode] NVARCHAR(2) NOT NULL, [SubItemCode] NVARCHAR(3) NOT NULL,
    [CriterionName] NVARCHAR(120) NOT NULL, [SubItemName] NVARCHAR(120) NOT NULL, [Percentage] DECIMAL(4,2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ScoringParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ScoringParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- #147. LND_ScoringRanges (cop_rangoscoring)
CREATE TABLE [dbo].[LND_ScoringRanges] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CriterionCode] NVARCHAR(2) NOT NULL, [SubItemCode] NVARCHAR(3) NOT NULL,
    [RangeStart] NVARCHAR(40) NOT NULL, [RangeEnd] NVARCHAR(40) NOT NULL, [ScoreValue] INT NOT NULL, [Equality] NVARCHAR(3) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ScoringRanges] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ScoringRanges_PublicId] UNIQUE ([PublicId])
);
GO

-- #148. LND_PeriodicityParameters (cop_paramPeriocidad)
CREATE TABLE [dbo].[LND_PeriodicityParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CompanyCode] NVARCHAR(5) NOT NULL, [DeductionClass] NVARCHAR(3) NOT NULL, [Periodicity] NVARCHAR(2) NOT NULL,
    [StartDay] INT NOT NULL, [EndDay] INT NOT NULL, [DayCount] NVARCHAR(3) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PeriodicityParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PeriodicityParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- #149. LND_RatesByTerm (cop_partasasplazo)
CREATE TABLE [dbo].[LND_RatesByTerm] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CreditLineId] INT NOT NULL, [TermStart] INT NOT NULL, [TermEnd] INT NOT NULL,
    [DiscountType] NVARCHAR(2) NOT NULL, [RateValue] DECIMAL(18,4) NOT NULL, [UpdateDate] DATETIME2 NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_RatesByTerm] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_RatesByTerm_PublicId] UNIQUE ([PublicId])
);
GO

-- #150. LND_TermRates (cop_tasasplazos)
CREATE TABLE [dbo].[LND_TermRates] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CreditLineId] INT NOT NULL, [AmountStart] DECIMAL(18,2) NOT NULL, [AmountEnd] DECIMAL(18,2) NOT NULL,
    [TermStart] INT NOT NULL, [TermEnd] INT NOT NULL, [SeniorityStart] INT NOT NULL, [SeniorityEnd] INT NOT NULL,
    [Rate] DECIMAL(6,3) NOT NULL, [UpdateDate] DATETIME2 NOT NULL,
    [MaxTerm] INT NOT NULL, [MaxAmount] DECIMAL(18,2) NOT NULL, [GuaranteeType] NVARCHAR(3) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_TermRates] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_TermRates_PublicId] UNIQUE ([PublicId])
);
GO

-- #151. LND_PortfolioAccounts (cop_copctas)
CREATE TABLE [dbo].[LND_PortfolioAccounts] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [AccountCode] NVARCHAR(15) NOT NULL, [RecordClass] NVARCHAR(3) NOT NULL,
    [Category] INT NOT NULL, [GuaranteeType] INT NOT NULL, [DeductionClass] INT NOT NULL, [RiskLevel] NVARCHAR(2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_PortfolioAccounts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_PortfolioAccounts_PublicId] UNIQUE ([PublicId])
);
GO

-- #152. LND_Zones (cop_zonas)
CREATE TABLE [dbo].[LND_Zones] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ZoneId] INT NOT NULL, [Code] INT NOT NULL, [Name] NVARCHAR(120) NOT NULL, [ShortName] NVARCHAR(50) NOT NULL,
    [Address] NVARCHAR(120) NOT NULL, [Phone] NVARCHAR(120) NOT NULL, [SubZoneId] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Zones] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Zones_PublicId] UNIQUE ([PublicId])
);
GO

-- #153. LND_SubZones (cop_subzonas)
CREATE TABLE [dbo].[LND_SubZones] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Code] INT NOT NULL, [Name] NVARCHAR(120) NOT NULL, [ShortName] NVARCHAR(50) NOT NULL,
    [Address] NVARCHAR(120) NOT NULL, [Phone] NVARCHAR(120) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_SubZones] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_SubZones_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_SubZones_Code] UNIQUE ([Code])
);
GO

-- #154. LND_ZoneTypes (cop_Tipozonas)
CREATE TABLE [dbo].[LND_ZoneTypes] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ZoneTypeId] INT NOT NULL, [Name] NVARCHAR(120) NOT NULL, [ShortName] NVARCHAR(50) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_ZoneTypes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_ZoneTypes_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_ZoneTypes_TypeId] UNIQUE ([ZoneTypeId])
);
GO

-- #155. LND_Subsidies (cop_auxilio)
CREATE TABLE [dbo].[LND_Subsidies] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [SubsidyCode] NVARCHAR(5) NOT NULL, [Name] NVARCHAR(50) NOT NULL, [ShortName] NVARCHAR(25) NOT NULL,
    [AccountCode] NVARCHAR(15) NOT NULL, [Remarks] NVARCHAR(MAX) NULL, [Amount] INT NOT NULL,
    [SubsidyClass] INT NOT NULL, [CommitteeCode] NVARCHAR(5) NOT NULL,
    [TaxRate] DECIMAL(10,5) NOT NULL, [TaxAccount] NVARCHAR(15) NOT NULL, [TaxExpenseAccount] NVARCHAR(15) NOT NULL,
    [ControlsCeiling] NVARCHAR(2) NOT NULL, [Periodicity] NVARCHAR(2) NOT NULL, [CurrentCeiling] INT NOT NULL,
    [StartDate] DATE NOT NULL, [EndDate] DATE NOT NULL,
    [ControlsAssociateCeiling] NVARCHAR(2) NOT NULL, [AssociatePeriodicity] NVARCHAR(2) NOT NULL,
    [AssociateCeiling] INT NOT NULL, [AssociateStartDate] DATE NOT NULL, [AssociateEndDate] DATE NOT NULL,
    [InInstallments] NVARCHAR(2) NOT NULL, [InstallmentCount] INT NOT NULL,
    [ControlsSeniority] NVARCHAR(2) NOT NULL, [SeniorityYearsStart] INT NOT NULL, [SeniorityYearsEnd] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Subsidies] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Subsidies_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_Subsidies_Code] UNIQUE ([SubsidyCode])
);
GO

-- #156. LND_RecreationApplications (COP_SOLRECR)
CREATE TABLE [dbo].[LND_RecreationApplications] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ApplicationNumber] INT NOT NULL, [PersonCode] NVARCHAR(20) NOT NULL,
    [CreditLineId] INT NOT NULL, [CreditNumber] INT NOT NULL,
    [PaymentAmount] DECIMAL(18,2) NOT NULL, [AdditionalInterest] DECIMAL(18,2) NULL,
    [FullOrPartial] NVARCHAR(2) NOT NULL, [MaturityDate] DATE NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_RecreationApplications] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_RecreationApplications_PublicId] UNIQUE ([PublicId])
);
GO

-- #157. LND_CreditLineAudit (cop_LinAud)
CREATE TABLE [dbo].[LND_CreditLineAudit] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Action] NVARCHAR(2) NOT NULL, [CreditLineId] INT NULL,
    [Description_Old] NVARCHAR(40) NULL, [Description_New] NVARCHAR(40) NULL,
    [AllowExtension_Old] NVARCHAR(2) NULL, [AllowExtension_New] NVARCHAR(2) NULL,
    [DefaultInterest_Old] NVARCHAR(2) NULL, [DefaultInterest_New] NVARCHAR(2) NULL,
    [InterestRate_Old] DECIMAL(10,5) NULL, [InterestRate_New] DECIMAL(10,5) NULL,
    [InterestType_Old] NVARCHAR(2) NULL, [InterestType_New] NVARCHAR(2) NULL,
    [MaxTerm_Old] INT NULL, [MaxTerm_New] INT NULL,
    [CreditLimit_Old] DECIMAL(10,5) NULL, [CreditLimit_New] DECIMAL(10,5) NULL,
    [ExtraRate_Old] DECIMAL(6,2) NULL, [ExtraRate_New] DECIMAL(6,2) NULL,
    [FinancialInterest_Old] NVARCHAR(2) NULL, [FinancialInterest_New] NVARCHAR(2) NULL,
    [GuaranteeClass_Old] NVARCHAR(2) NULL, [GuaranteeClass_New] NVARCHAR(2) NULL,
    [MaxAmount_Old] DECIMAL(14,2) NULL, [MaxAmount_New] DECIMAL(14,2) NULL,
    [AffectsFlag_Old] NVARCHAR(2) NULL, [AffectsFlag_New] NVARCHAR(2) NULL,
    [AccrualRate_Old] DECIMAL(10,5) NULL, [AccrualRate_New] DECIMAL(10,5) NULL,
    [CapitalForm_Old] NVARCHAR(2) NULL, [CapitalForm_New] NVARCHAR(2) NULL,
    [AdminRate_Old] DECIMAL(10,5) NULL, [AdminRate_New] DECIMAL(10,5) NULL,
    [InstallmentType_Old] NVARCHAR(2) NULL, [InstallmentType_New] NVARCHAR(2) NULL,
    [DebitCreditFlag_Old] NVARCHAR(2) NULL, [DebitCreditFlag_New] NVARCHAR(2) NULL,
    [InsuranceRate_Old] DECIMAL(10,5) NULL, [InsuranceRate_New] DECIMAL(10,5) NULL,
    [MinContribution_Old] DECIMAL(14,2) NULL, [MinContribution_New] DECIMAL(14,2) NULL,
    [AccountCode_Old] NVARCHAR(15) NULL, [AccountCode_New] NVARCHAR(15) NULL,
    [GracePeriod_Old] NVARCHAR(2) NULL, [GracePeriod_New] NVARCHAR(2) NULL,
    [AdminMin_Old] DECIMAL(15,2) NULL, [AdminMin_New] DECIMAL(15,2) NULL,
    [AdminMax_Old] DECIMAL(15,2) NULL, [AdminMax_New] DECIMAL(15,2) NULL,
    [ShortName_Old] NVARCHAR(30) NULL, [ShortName_New] NVARCHAR(30) NULL,
    [TaxRate_Old] DECIMAL(6,3) NULL, [TaxRate_New] DECIMAL(6,3) NULL,
    [InsMin_Old] DECIMAL(18,0) NULL, [InsMin_New] DECIMAL(18,0) NULL,
    [InsMax_Old] DECIMAL(18,0) NULL, [InsMax_New] DECIMAL(18,0) NULL,
    [UserId_Old] NVARCHAR(20) NULL, [UserId_New] NVARCHAR(20) NULL,
    [UserName_Old] NVARCHAR(50) NULL, [UserName_New] NVARCHAR(50) NULL,
    [SystemDate] DATETIME2 NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CreditLineAudit] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CreditLineAudit_PublicId] UNIQUE ([PublicId])
);
GO

-- #158. LND_CreditParameters (cre_parame01)
CREATE TABLE [dbo].[LND_CreditParameters] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [BankCode] NVARCHAR(5) NOT NULL, [CreditLimit] DECIMAL(10,0) NOT NULL,
    [AdvanceType] INT NOT NULL, [AdvanceAmount] DECIMAL(18,0) NOT NULL, [AdvanceRate] DECIMAL(3,0) NOT NULL,
    [CreditLineId] INT NOT NULL, [AdvanceCreditLineId] INT NOT NULL, [DeductionClass] NVARCHAR(2) NOT NULL,
    [ExpirationDays] INT NOT NULL, [CutoffDay] INT NOT NULL,
    [Amount1] DECIMAL(18,0) NULL, [Term1] INT NULL, [Period1] INT NULL,
    [Amount2] DECIMAL(18,0) NULL, [Term2] INT NULL, [Period2] INT NULL,
    [Amount3] DECIMAL(18,0) NULL, [Term3] INT NULL, [Period3] INT NULL,
    [Amount4] DECIMAL(18,0) NULL, [Term4] INT NULL, [Period4] INT NULL,
    [Amount5] DECIMAL(18,0) NULL, [Term5] INT NULL, [Period5] INT NULL,
    [BranchId] NVARCHAR(5) NOT NULL, [TaxRate] DECIMAL(10,5) NOT NULL,
    [Amount6] DECIMAL(18,0) NOT NULL, [Term6] INT NOT NULL, [Period6] INT NOT NULL,
    [Amount7] DECIMAL(18,0) NOT NULL, [Term7] INT NOT NULL, [Period7] INT NOT NULL,
    [AdvanceTerm1] INT NOT NULL, [AdvanceTerm2] INT NOT NULL, [AdvanceTerm3] INT NOT NULL,
    [AdvanceTerm4] INT NOT NULL, [AdvanceTerm5] INT NOT NULL, [AdvanceTerm6] INT NOT NULL, [AdvanceTerm7] INT NOT NULL,
    [MaintenanceLineId] INT NOT NULL, [MaintenanceAmount] DECIMAL(18,2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CreditParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CreditParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- DEPOSIT TABLES (dep_ mapped to LND_)
-- ============================================================

-- #159. LND_DepositAccounts (dep_maeahor)
CREATE TABLE [dbo].[LND_DepositAccounts] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [DepositLineId] INT NOT NULL, [AccountNumber] BIGINT NOT NULL,
    [CreationDate] DATE NOT NULL, [LegalRepresentative] NVARCHAR(20) NOT NULL, [RepresentativeName] NVARCHAR(50) NOT NULL,
    [CommercialAddress] NVARCHAR(40) NOT NULL, [Phone] NVARCHAR(20) NOT NULL, [CellPhone] NVARCHAR(20) NOT NULL,
    [Status] NVARCHAR(2) NOT NULL, [ExemptionDate] DATE NULL, [AutoDebit] NVARCHAR(2) NOT NULL,
    [FirstDeductionDate] DATE NULL, [EntryDate] DATE NULL, [DeductionType] NVARCHAR(2) NOT NULL,
    [Periodicity] NVARCHAR(2) NOT NULL, [PaymentCycle] NVARCHAR(2) NOT NULL,
    [HasSeal] NVARCHAR(2) NOT NULL, [HasProtector] NVARCHAR(2) NOT NULL,
    [RegisteredSignatures] INT NOT NULL, [RequiredSignatures] INT NOT NULL,
    [SignatoryId1] NVARCHAR(20) NOT NULL, [SignatoryId2] NVARCHAR(20) NOT NULL, [SignatoryId3] NVARCHAR(20) NOT NULL,
    [SignatoryName1] NVARCHAR(50) NOT NULL, [SignatoryName2] NVARCHAR(50) NOT NULL, [SignatoryName3] NVARCHAR(50) NOT NULL,
    [BeneficiaryId1] NVARCHAR(20) NOT NULL, [BeneficiaryId2] NVARCHAR(20) NOT NULL, [BeneficiaryId3] NVARCHAR(20) NOT NULL,
    [BeneficiaryId4] NVARCHAR(20) NOT NULL, [BeneficiaryId5] NVARCHAR(20) NOT NULL,
    [BeneficiaryName1] NVARCHAR(50) NOT NULL, [BeneficiaryName2] NVARCHAR(50) NOT NULL, [BeneficiaryName3] NVARCHAR(50) NOT NULL,
    [BeneficiaryName4] NVARCHAR(50) NOT NULL, [BeneficiaryName5] NVARCHAR(50) NOT NULL,
    [BeneficiaryPct1] DECIMAL(6,3) NOT NULL, [BeneficiaryPct2] DECIMAL(6,3) NOT NULL, [BeneficiaryPct3] DECIMAL(6,3) NOT NULL,
    [BeneficiaryPct4] DECIMAL(6,3) NOT NULL, [BeneficiaryPct5] DECIMAL(6,3) NOT NULL,
    [IsExempt] NVARCHAR(2) NOT NULL, [UserFullName] NVARCHAR(50) NOT NULL, [SystemDate] DATE NOT NULL,
    [UserId] NVARCHAR(20) NULL, [AccountType] INT NOT NULL, [MaturityDate] DATE NULL,
    [CancellationDate] DATE NULL, [CancelledByUser] NVARCHAR(20) NULL,
    [LegacyNumCuenta] BIGINT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DepositAccounts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DepositAccounts_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_DepositAccounts_AccountNumber] UNIQUE ([AccountNumber])
);
GO

-- #160. LND_DepositEntries (dep_novmeahor)
CREATE TABLE [dbo].[LND_DepositEntries] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [DepositLineId] INT NOT NULL, [PersonCode] NVARCHAR(20) NOT NULL, [AccountNumber] BIGINT NOT NULL,
    [EntryDate] DATE NOT NULL, [CreationDate] DATE NOT NULL, [FirstDeductionDate] DATE NOT NULL,
    [DeductionType] NVARCHAR(2) NOT NULL, [Periodicity] NVARCHAR(2) NOT NULL, [PaymentCycle] NVARCHAR(2) NOT NULL,
    [InstallmentAmount] DECIMAL(18,2) NOT NULL, [Term] INT NOT NULL, [MaturityDate] DATE NULL,
    [EntryType] NVARCHAR(2) NOT NULL, [UserId] NVARCHAR(15) NOT NULL, [UserFullName] NVARCHAR(50) NOT NULL, [SystemDate] DATE NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DepositEntries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DepositEntries_PublicId] UNIQUE ([PublicId])
);
GO

-- #161. LND_DepositSignatures (dep_firmas)
CREATE TABLE [dbo].[LND_DepositSignatures] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [AccountNumber] BIGINT NOT NULL, [SignatureNumber] INT NOT NULL, [SignatureImage] VARBINARY(MAX) NULL, [IsRequired] NVARCHAR(2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DepositSignatures] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DepositSignatures_PublicId] UNIQUE ([PublicId])
);
GO

-- #162. LND_DepositSeals (dep_sellos)
CREATE TABLE [dbo].[LND_DepositSeals] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [AccountNumber] BIGINT NOT NULL, [SealImage] VARBINARY(MAX) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DepositSeals] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DepositSeals_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_LND_DepositSeals_Account] UNIQUE ([AccountNumber])
);
GO

-- #163. LND_Cashiers (dep_cajeros)
CREATE TABLE [dbo].[LND_Cashiers] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CashierCode] NVARCHAR(20) NOT NULL, [Name] NVARCHAR(50) NOT NULL, [VoucherType] NVARCHAR(5) NOT NULL,
    [Status] NVARCHAR(2) NULL, [OpenDate] DATE NULL, [VoucherConsecutive] INT NOT NULL,
    [Description] NVARCHAR(50) NOT NULL, [PasswordTimeout] INT NOT NULL, [PrinterName] NVARCHAR(60) NOT NULL, [TransactionType] INT NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Cashiers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Cashiers_PublicId] UNIQUE ([PublicId])
);
GO

-- #164. LND_CashBases (dep_basecaja)
CREATE TABLE [dbo].[LND_CashBases] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CashierCode] NVARCHAR(20) NOT NULL, [EntryDate] DATETIME2 NOT NULL, [TransactionDate] DATE NOT NULL,
    [EntryType] NVARCHAR(2) NOT NULL, [Amount] DECIMAL(18,2) NOT NULL,
    [ReceivedByUser] NVARCHAR(20) NOT NULL, [DeliveredByUser] NVARCHAR(20) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CashBases] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CashBases_PublicId] UNIQUE ([PublicId])
);
GO

-- #165. LND_CheckClearing (dep_checanje)
CREATE TABLE [dbo].[LND_CheckClearing] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [AccountNumber] BIGINT NOT NULL, [CheckNumber] DECIMAL(18,2) NOT NULL, [DepositDate] DATE NOT NULL,
    [ClearingDays] INT NOT NULL, [MaturityDate] DATE NOT NULL, [Plaza] NVARCHAR(2) NOT NULL,
    [BankCode] NVARCHAR(5) NULL, [Amount] DECIMAL(18,2) NULL, [Status] NVARCHAR(2) NOT NULL,
    [ClearingType] INT NOT NULL, [UserId] NVARCHAR(20) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_CheckClearing] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_CheckClearing_PublicId] UNIQUE ([PublicId])
);
GO

-- #166. LND_Checkbooks (dep_talonario)
CREATE TABLE [dbo].[LND_Checkbooks] (
    [Id] INT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [AccountNumber] BIGINT NOT NULL, [RangeStart] BIGINT NOT NULL, [RangeEnd] BIGINT NOT NULL,
    [DeliveryDate] DATE NOT NULL, [IsBlocked] NVARCHAR(2) NOT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_Checkbooks] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_Checkbooks_PublicId] UNIQUE ([PublicId])
);
GO

-- #167. LND_DepositAudit (dep_maeaud)
CREATE TABLE [dbo].[LND_DepositAudit] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Action] NVARCHAR(2) NOT NULL, [PersonCode] NVARCHAR(20) NULL, [DepositLineId] INT NULL, [AccountNumber] BIGINT NOT NULL,
    [CreationDate_Old] DATE NULL, [CreationDate_New] DATE NULL,
    [LegalRep_Old] NVARCHAR(20) NULL, [LegalRep_New] NVARCHAR(20) NULL,
    [RepName_Old] NVARCHAR(50) NULL, [RepName_New] NVARCHAR(50) NULL,
    [Address_Old] NVARCHAR(40) NULL, [Address_New] NVARCHAR(40) NULL,
    [Phone_Old] NVARCHAR(20) NULL, [Phone_New] NVARCHAR(20) NULL,
    [CellPhone_Old] NVARCHAR(20) NULL, [CellPhone_New] NVARCHAR(20) NULL,
    [Status_Old] NVARCHAR(2) NULL, [Status_New] NVARCHAR(2) NULL,
    [ExemptionDate_Old] DATE NULL, [ExemptionDate_New] DATE NULL,
    [AutoDebit_Old] NVARCHAR(2) NULL, [AutoDebit_New] NVARCHAR(2) NULL,
    [FirstDeduction_Old] DATE NULL, [FirstDeduction_New] DATE NULL,
    [EntryDate_Old] DATE NULL, [EntryDate_New] DATE NULL,
    [DeductionType_Old] NVARCHAR(2) NULL, [DeductionType_New] NVARCHAR(2) NULL,
    [Periodicity_Old] NVARCHAR(2) NULL, [Periodicity_New] NVARCHAR(2) NULL,
    [Cycle_Old] NVARCHAR(2) NULL, [Cycle_New] NVARCHAR(2) NULL,
    [Seal_Old] NVARCHAR(2) NULL, [Seal_New] NVARCHAR(2) NULL,
    [Protector_Old] NVARCHAR(2) NULL, [Protector_New] NVARCHAR(2) NULL,
    [RegSignatures_Old] INT NULL, [RegSignatures_New] INT NULL,
    [ReqSignatures_Old] INT NULL, [ReqSignatures_New] INT NULL,
    [Exempt_Old] NVARCHAR(2) NULL, [Exempt_New] NVARCHAR(2) NULL,
    [UserName_Old] NVARCHAR(50) NULL, [UserName_New] NVARCHAR(50) NULL,
    [SystemDate] DATETIME2 NOT NULL, [AuditUserId] NVARCHAR(50) NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_DepositAudit] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_DepositAudit_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- #270. LND_OnlineQueries — Online query/consultation log
-- Original: lin_consulta
-- ============================================================
CREATE TABLE [dbo].[LND_OnlineQueries] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL, [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonCode] NVARCHAR(20) NOT NULL, [IdentificationNumber] NVARCHAR(20) NOT NULL,
    [ItemCode] INT NOT NULL, [Description] NVARCHAR(60) NULL,
    [CreditLineId] INT NOT NULL, [VoucherType] NVARCHAR(5) NOT NULL, [VoucherNumber] BIGINT NOT NULL,
    [QueryDate] DATETIME2 NULL, [QueryAmount] DECIMAL(18,2) NOT NULL,
    [UserFullName] NVARCHAR(50) NOT NULL, [UpdateDate] DATETIME2 NOT NULL,
    [LegacyConsecutivo] INT NULL,
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER, [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [UpdatedBy] NVARCHAR(100) NULL, [UpdatedAt] DATETIME2 NULL, [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LND_OnlineQueries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_LND_OnlineQueries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- End of LND_ module — 93 CREATE TABLE statements
-- Tables #76-#158 (83 core) + #159-#167 (9 deposits) + #270 (LND_OnlineQueries) = 93 total
-- ============================================================
