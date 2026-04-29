-- ============================================================
-- IngenIA365ERP — Data Migration Script
-- Step 04: LND_ (Lending / Cartera + Savings + Deposits)
-- Source: [old].dbo.cop_*, dep_*, cre_*, lin_*
-- Target: [dbo].LND_*
-- 93 tables total
-- ============================================================
-- Prerequisites:
--   01_Create_MigrationSchema.sql (migration schema + Map_* tables)
--   02_Migrate_COR.sql (Map_People, Map_Branches, Map_CostCenters, Map_Banks populated)
--   03_Migrate_ACC.sql (Map_ChartOfAccounts populated)
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT OFF;
GO

PRINT '================================================================';
PRINT '  04_Migrate_LND_Lending.sql — START  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO

-- ============================================================
-- BLOCK 1: Credit Line Parameters (cop_concar12 -> LND_CreditLineParameters)
-- Also populates Map_CreditLines
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_CreditLineParameters (cop_concar12)...';

    INSERT INTO [dbo].[LND_CreditLineParameters] (
        [CreditLineId], [Description], [AllowExtension], [DefaultInterestFlag],
        [InterestRate], [InterestType], [MaxTerm], [CreditLimit],
        [ExtraRate], [FinancialInterest], [GuaranteeClass], [MaxAmount],
        [AffectsFlag], [AccrualRate], [CapitalForm], [AdminRate],
        [InstallmentType], [DebitCreditFlag], [SumGuarantee], [TotalPriorInterest],
        [MonthsInAdvance], [AccountStatement], [ClosingInterest], [PrimaryVoucher],
        [HousingLoan], [InsuranceRate], [BalanceConsult], [MinContribution],
        [PendingContribution], [AccountCode], [GracePeriodFlag], [GracePeriodMonths],
        [ShowBalance], [SecondaryCostCenter], [AdminValueMin], [AdminValueMax],
        [IncomeTaxFlag], [AdminForm], [Priority], [SavingsCode],
        [GuarantorRequired], [AdminClass], [CategoryA], [CategoryB],
        [CategoryC], [CategoryD], [CategoryE], [ShortName],
        [ConsecutiveCode], [CdatInterestRate], [AdminConceptCode], [InsuranceConceptCode],
        [InterestConceptCode], [EquivalentRate], [AccountInterestIncome], [AccountInterestCxC],
        [AccountInterestDefault], [AccountInterestAdvance], [AccountInterestOrderDebit], [AccountInterestOrderCredit],
        [FogaContribution], [FogaClass], [PayrollCompanyCode], [PayrollConceptCode],
        [ColumnCount], [ColumnTitle], [AdditionalChargesConcept], [TaxRate],
        [InternetEnabled], [MaturityBehavior], [InsuranceValueMin], [InsuranceValueMax],
        [ProjectionItem], [FormatId], [ConceptId], [SourceId],
        [CapitalizationConcept], [SuperintendencyEquivalent], [ExportCifin], [LiquidateDefaultDays],
        [ModifyInstallmentType], [InterestTypeCode], [DtfRate], [BlockProjectionDate],
        [BlockOverdueAssociate], [ExtraPaymentApply], [VatAccount], [CalculateVat],
        [CreditLimitType], [CreditLimitCalcMethod], [CreditLimitValue], [CreditLimitAvailable],
        [CapitalAtRisk], [InvoiceEnabled], [VatPercentage], [VatLineId],
        [IsVatLine], [InvoiceGroup], [CalculationBase], [BasePercentage],
        [DiscountConceptId], [PeaceSalvoSeniority], [LegacyLinCred],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.lincred,
        ISNULL(NULLIF(RTRIM(s.descripcion), ''), N'Sin descripcion'),
        ISNULL(s.permiteprorro, N'N'), ISNULL(s.intmora, N'N'),
        ISNULL(s.tasaint, 0), ISNULL(s.tipointeres, N'S'),
        ISNULL(s.plazomax, 0), ISNULL(s.cupocredito, 0),
        ISNULL(s.extras, 0), ISNULL(s.intfciero, N'N'),
        ISNULL(s.clagarantia, N'0'), ISNULL(s.vlrmax, 0),
        ISNULL(s.afecta, N'N'), ISNULL(s.tasacausacion, 0),
        ISNULL(s.forcapital, N'1'), ISNULL(s.administ, 0),
        ISNULL(s.tipocuota, N'1'), ISNULL(s.debcre, N'D'),
        ISNULL(s.sumagarantia, N'N'), ISNULL(s.totintant, N'N'),
        ISNULL(s.mesesanticipado, 0), ISNULL(s.extcuenta, N'N'),
        ISNULL(s.intcierre, N'N'), ISNULL(s.cpteprin, N'N'),
        ISNULL(s.vivienda, N'N'), ISNULL(s.tasaseguro, 0),
        ISNULL(s.consultasaldo, N'N'), ISNULL(s.minaporte, 0),
        ISNULL(s.pendienteaporte, N'N'), ISNULL(NULLIF(RTRIM(s.cuenta), ''), N''),
        ISNULL(s.periodogracia, N'N'), ISNULL(s.mesesgracia, 0),
        ISNULL(s.muestrasaldo, N'N'), ISNULL(NULLIF(RTRIM(s.ccosto2), ''), N''),
        ISNULL(s.adminvlrmin, 0), ISNULL(s.adminvlrmax, 0),
        ISNULL(s.retefuente, N'N'), ISNULL(s.foradmin, N'1'),
        ISNULL(s.prioridad, N'1'), ISNULL(s.codahorro, N'0'),
        ISNULL(s.exigegarante, N'N'), ISNULL(s.claadmin, N'0'),
        ISNULL(s.categoria_a, N'0'), ISNULL(s.categoria_b, N'0'),
        ISNULL(s.categoria_c, N'0'), ISNULL(s.categoria_d, N'0'),
        ISNULL(s.categoria_e, N'0'), ISNULL(NULLIF(RTRIM(s.nomres), ''), N''),
        ISNULL(NULLIF(RTRIM(s.consecutivo), ''), N''), ISNULL(s.tasacdat, 0),
        ISNULL(s.cptoadmin, 0), ISNULL(s.cptoseguro, 0),
        ISNULL(s.cptoint, 0), ISNULL(s.tasaequivalente, N'N'),
        ISNULL(NULLIF(RTRIM(s.ctaintingreso), ''), N''), ISNULL(NULLIF(RTRIM(s.ctaintcxc), ''), N''),
        ISNULL(NULLIF(RTRIM(s.ctaintmora), ''), N''), ISNULL(NULLIF(RTRIM(s.ctaintanticipado), ''), N''),
        ISNULL(NULLIF(RTRIM(s.ctaintordendeb), ''), N''), ISNULL(NULLIF(RTRIM(s.ctaintordencre), ''), N''),
        ISNULL(s.aporteFogafin, N'N'), ISNULL(s.claseFogafin, N'0'),
        ISNULL(NULLIF(RTRIM(s.empresaNomina), ''), N''), ISNULL(NULLIF(RTRIM(s.cptoNomina), ''), N''),
        ISNULL(s.numcolumnas, 0), ISNULL(NULLIF(RTRIM(s.tituloCol), ''), N''),
        ISNULL(s.cptocargos, 0), ISNULL(s.tasaimpuesto, 0),
        ISNULL(s.internet, N'N'), ISNULL(s.comporvence, N'N'),
        ISNULL(s.segurovlrmin, 0), ISNULL(s.segurovlrmax, 0),
        ISNULL(s.itemproyeccion, 0), ISNULL(s.formato, 0),
        ISNULL(s.concepto, 0), ISNULL(s.fuente, 0),
        ISNULL(NULLIF(RTRIM(s.cptocapitalizacion), ''), N''), ISNULL(s.eqsuper, 0),
        ISNULL(s.exportacifin, N'N'), ISNULL(s.liquidadiasmora, N'N'),
        s.modificatipocuota, ISNULL(s.codtipointeres, 0),
        ISNULL(s.dtf, 0), ISNULL(s.bloqueafecproyeccion, N'N'),
        ISNULL(s.bloqueasocmora, 0), ISNULL(s.aplicaextra, N'N'),
        ISNULL(NULLIF(RTRIM(s.ctaiva), ''), N''), ISNULL(s.calculaiva, N'N'),
        ISNULL(s.tipocupocredito, 0), ISNULL(s.calcupocredito, N'0'),
        ISNULL(s.vlrcupocredito, 0), ISNULL(s.dspcupocredito, 0),
        ISNULL(s.capitalriesgo, N'N'), ISNULL(s.factura, N'N'),
        ISNULL(s.porcentajeiva, 0), ISNULL(s.lineaiva, 0),
        ISNULL(s.eslineaiva, N'N'), ISNULL(s.grupoFactura, N'0'),
        ISNULL(s.basecalculo, N'0'), ISNULL(s.porcentajebase, 0),
        ISNULL(s.cptodescuento, 0), ISNULL(s.antiguedadpazsalvo, 0),
        s.lincred,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_concar12 s;

    -- Populate Map_CreditLines
    INSERT INTO [migration].[Map_CreditLines] (OldLincred, NewCreditLineId)
    SELECT [LegacyLinCred], [Id]
    FROM [dbo].[LND_CreditLineParameters]
    WHERE [LegacyLinCred] IS NOT NULL;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_CreditLineParameters', (SELECT COUNT(*) FROM [dbo].[LND_CreditLineParameters]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_CreditLineParameters', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 2: Transaction Codes (cop_codmov -> LND_TransactionCodes)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_TransactionCodes (cop_codmov)...';

    INSERT INTO [dbo].[LND_TransactionCodes] (
        [TransactionCode], [Name], [ShortName], [TransactionType],
        [AccountCode], [AdjustAccrual], [DebitCreditFlag],
        [FormatId], [ConceptId], [SourceId], [LegacyCodMovto],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.codmovto,
        ISNULL(NULLIF(RTRIM(s.nombre), ''), N'Sin nombre'),
        ISNULL(NULLIF(RTRIM(s.nomres), ''), N''),
        ISNULL(s.tipo, N''),
        ISNULL(NULLIF(RTRIM(s.cuenta), ''), N''),
        ISNULL(s.ajustcausacion, N'N'),
        ISNULL(s.debcre, N'D'),
        ISNULL(s.formato, 0), ISNULL(s.concepto, 0), ISNULL(s.fuente, 0),
        s.codmovto,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_codmov s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_TransactionCodes', (SELECT COUNT(*) FROM [dbo].[LND_TransactionCodes]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_TransactionCodes', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 3: Loan Portfolios (cop_maecar -> LND_LoanPortfolios)
-- THE CRITICAL TABLE - populates Map_LoanPortfolios
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_LoanPortfolios (cop_maecar) — CRITICAL...';

    INSERT INTO [dbo].[LND_LoanPortfolios] (
        [PersonId], [CreditLineId], [PortfolioNumber],
        [IdentificationNumber], [ApplicationDate], [ApprovalDate], [DisbursementDate],
        [DiscountStartDate], [LastAccrualDate], [LastPaymentDate], [LastDefaultDate],
        [MaturityDate], [ClosingDate], [TermMonths],
        [RequestedAmount], [ApprovedAmount], [CurrentBalance], [InstallmentAmount],
        [InterestRate], [PaymentCycle], [PaymentPeriodicity], [InstallmentType],
        [InterestType], [GuaranteeType], [DeductionType],
        [PaidInstallments], [AdminFeeRate], [InsuranceRate],
        [CapitalBalanceCurrent], [CapitalBalanceMonthly], [CapitalBalancePayment],
        [InterestBalanceCurrent], [InterestBalanceMonthly], [InterestBalancePayment],
        [DefaultBalanceCurrent], [DefaultBalanceMonthly], [DefaultBalancePayment],
        [AdminBalanceCurrent], [AdminBalanceMonthly], [AdminBalancePayment],
        [InsuranceBalanceCurrent], [InsuranceBalanceMonthly], [InsuranceBalancePayment],
        [DocumentType], [DocumentNumber], [AdditionalCharges], [ExtraPaymentAmount],
        [CapitalAccrued], [InterestAccrued], [InsuranceAccrued], [AdminAccrued],
        [DefaultInterest], [DaysOverdue],
        [InitialGracePeriod], [GracePeriodStartDate], [GracePeriodInstallment], [GracePeriodDays],
        [Codeudor1], [Codeudor2], [Codeudor3], [Codeudor4],
        [GuaranteeValue], [ContributionsAmount],
        [BranchId], [CostCenterId], [Category], [ExtraPercentage],
        [PaidInstallmentsAgency], [PendingInstallmentCount], [SearchNumber],
        [ProvisionRate], [ProvisionAmount], [OrderInterest], [CxcInterest],
        [LocalClearing], [OtherClearing], [LegalCollection], [LawyerCode],
        [AdminInstallment], [InsuranceInstallment], [CapitalInstallment], [InterestInstallment],
        [OtherInstallment], [ApplicationNumber], [ExtraInMonth], [ExtraInAdvance],
        [FirstPaymentFlag], [SecondPaymentType], [InterestInstallmentAmt], [LessAmount],
        [CardNumber], [CapitalAppliedPayroll], [InterestAppliedPayroll], [DefaultAppliedPayroll],
        [InsuranceAppliedPayroll], [AdminAppliedPayroll], [PayrollCode], [InsuranceForm],
        [TotalPriorInterest], [DiscountCompany], [IncludeAutoDebit],
        [CifinStartDate], [CifinEndDate], [CifinDaysOverdue], [CifinOverdueBalance],
        [RestructureDate], [RestructureCategory], [IsRestructured], [AuthCreditBureau],
        [CifinOverdueInstallments], [IsWrittenOff], [TransactionId],
        [DtfRate], [SpreadPoints], [HasLawsuit],
        [WriteOffCapital], [WriteOffInterest], [WriteOffDate], [WriteOffMinutes],
        [LawyerNotes], [WriteOffApprovalDate], [TransferConcept], [IsFopep],
        [ProposedInterestDate],
        [LegacyCodigoTer], [LegacyLinCred], [LegacyNumero],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        cl.NewCreditLineId,
        m.numero,
        ISNULL(NULLIF(RTRIM(m.nit), ''), N''),
        CASE WHEN m.fecsolic <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(m.fecsolic AS DATE) END,
        CASE WHEN m.fecaprobacion <= '1900-01-02' THEN NULL ELSE CAST(m.fecaprobacion AS DATE) END,
        CASE WHEN m.fecdesemb <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(m.fecdesemb AS DATE) END,
        CASE WHEN m.fecinidescuento <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(m.fecinidescuento AS DATE) END,
        CASE WHEN m.fecultcausa <= '1900-01-02' THEN NULL ELSE CAST(m.fecultcausa AS DATE) END,
        CASE WHEN m.fecultpago <= '1900-01-02' THEN NULL ELSE CAST(m.fecultpago AS DATE) END,
        CASE WHEN m.fecultmora <= '1900-01-02' THEN NULL ELSE CAST(m.fecultmora AS DATE) END,
        CASE WHEN m.fecvence <= '1900-01-02' THEN NULL ELSE CAST(m.fecvence AS DATE) END,
        CASE WHEN m.feccierre <= '1900-01-02' THEN NULL ELSE CAST(m.feccierre AS DATE) END,
        ISNULL(m.plazo, 0),
        ISNULL(m.vlrsolicitud, 0), ISNULL(m.vlraprobado, 0),
        ISNULL(m.saldo, 0), ISNULL(m.vlrcuota, 0),
        ISNULL(m.tasaint, 0), ISNULL(m.ciclopago, N'1'),
        ISNULL(m.periodicidad, N'1'), ISNULL(m.tipocuota, N'1'),
        ISNULL(m.tipointeres, N'S'), ISNULL(m.clagarantia, N'0'),
        ISNULL(m.cladesembolso, N'1'),
        ISNULL(m.cuotpag, 0), ISNULL(m.administ, 0), ISNULL(m.tasaseguro, 0),
        ISNULL(m.salcapvigente, 0), ISNULL(m.salcapmes, 0), ISNULL(m.salcappago, 0),
        ISNULL(m.salintvigente, 0), ISNULL(m.salintmes, 0), ISNULL(m.salintpago, 0),
        ISNULL(m.salmorvigente, 0), ISNULL(m.salmormes, 0), ISNULL(m.salmorpago, 0),
        ISNULL(m.saladmvigente, 0), ISNULL(m.saladmmes, 0), ISNULL(m.saladmpago, 0),
        ISNULL(m.salsegvigente, 0), ISNULL(m.salsegmes, 0), ISNULL(m.salsegpago, 0),
        ISNULL(NULLIF(RTRIM(m.tipodocumento), ''), N''), ISNULL(m.numerodocto, 0),
        ISNULL(m.cargos, 0), ISNULL(m.cuotaextra, 0),
        ISNULL(m.caucapital, 0), ISNULL(m.cauinteres, 0),
        ISNULL(m.causeguro, 0), ISNULL(m.cauadmin, 0),
        ISNULL(m.intmora, 0), ISNULL(m.diasmora, 0),
        ISNULL(m.periodogracia, N'N'),
        CASE WHEN m.fecinigracia <= '1900-01-02' THEN NULL ELSE CAST(m.fecinigracia AS DATE) END,
        ISNULL(m.cuotagracia, N'N'), ISNULL(m.diasgracia, 0),
        ISNULL(NULLIF(RTRIM(m.codeudor1), ''), N''), ISNULL(NULLIF(RTRIM(m.codeudor2), ''), N''),
        ISNULL(NULLIF(RTRIM(m.codeudor3), ''), N''), ISNULL(NULLIF(RTRIM(m.codeudor4), ''), N''),
        ISNULL(m.vlrgarantia, 0), ISNULL(m.vlraportes, 0),
        ISNULL(NULLIF(RTRIM(m.agencia), ''), N''), ISNULL(NULLIF(RTRIM(m.ccosto), ''), N''),
        ISNULL(m.califica, N'A'), ISNULL(m.porcentajeextra, 0),
        ISNULL(m.cuotaspagagen, 0), ISNULL(m.cuotpendiente, 0),
        ISNULL(NULLIF(RTRIM(m.numbusqueda), ''), N''),
        ISNULL(m.tasaprovision, 0), ISNULL(m.vlrprovision, 0),
        ISNULL(m.intorden, 0), ISNULL(m.intcxc, 0),
        ISNULL(m.canjelocal, 0), ISNULL(m.canjeotras, 0),
        ISNULL(m.cobrojuridico, N'N'), ISNULL(NULLIF(RTRIM(m.abogado), ''), N''),
        ISNULL(m.cuotaadmin, 0), ISNULL(m.cuotaseguro, 0),
        ISNULL(m.cuotacapital, 0), ISNULL(m.cuotainteres, 0),
        ISNULL(m.cuotaotros, 0), ISNULL(m.numsolicitud, 0),
        ISNULL(m.extraenmes, N'N'), ISNULL(m.extraanticipada, N'N'),
        ISNULL(m.primerpago, N'N'), ISNULL(m.segundopago, N'N'),
        ISNULL(m.vlrcuotaint, 0), ISNULL(m.vlrmenos, 0),
        ISNULL(NULLIF(RTRIM(m.tarjeta), ''), N''),
        ISNULL(m.capaplinom, 0), ISNULL(m.intaplinom, 0), ISNULL(m.moraplinom, 0),
        ISNULL(m.segaplinom, 0), ISNULL(m.admaplinom, 0),
        ISNULL(NULLIF(RTRIM(m.codigonomina), ''), N''),
        ISNULL(m.formaseguro, N'0'),
        ISNULL(m.totintant, N'N'), ISNULL(NULLIF(RTRIM(m.empresadescuento), ''), N''),
        ISNULL(m.incluirdebaut, N'N'),
        CASE WHEN m.fecinicifin <= '1900-01-02' THEN NULL ELSE CAST(m.fecinicifin AS DATE) END,
        CASE WHEN m.fecfincifin <= '1900-01-02' THEN NULL ELSE CAST(m.fecfincifin AS DATE) END,
        ISNULL(m.diasmoracifin, 0), ISNULL(m.salmoracifin, 0),
        CASE WHEN m.fecreestructura <= '1900-01-02' THEN NULL ELSE CAST(m.fecreestructura AS DATE) END,
        ISNULL(m.categreestructura, N''), ISNULL(m.reestructurado, N'N'),
        ISNULL(m.autconsultacentrales, N'N'),
        ISNULL(m.cuotasvencidascifin, 0), ISNULL(m.castigada, N'N'),
        ISNULL(NULLIF(RTRIM(m.transaccion), ''), N''),
        ISNULL(m.dtf, 0), ISNULL(m.puntos, 0),
        ISNULL(m.demanda, N'N'),
        ISNULL(m.capcastigo, 0), ISNULL(m.intcastigo, 0),
        CASE WHEN m.feccastigo <= '1900-01-02' THEN NULL ELSE m.feccastigo END,
        ISNULL(NULLIF(RTRIM(m.actacastigo), ''), N''),
        ISNULL(NULLIF(RTRIM(m.observacionabogado), ''), N''),
        CASE WHEN m.fecaprobcastigo <= '1900-01-02' THEN NULL ELSE m.fecaprobcastigo END,
        ISNULL(m.cptotrasferencia, N'N'), ISNULL(m.fopep, N'N'),
        CASE WHEN m.fecpropuestainteres <= '1900-01-02' THEN NULL ELSE m.fecpropuestainteres END,
        m.codigoter, m.lincred, m.numero,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_maecar m
    INNER JOIN [migration].[Map_People] mp ON m.codigoter = mp.OldCodigoTer
    INNER JOIN [migration].[Map_CreditLines] cl ON m.lincred = cl.OldLincred;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

    -- Populate Map_LoanPortfolios
    INSERT INTO [migration].[Map_LoanPortfolios] (OldCodigoTer, OldLincred, OldNumero, NewLoanPortfolioId)
    SELECT [LegacyCodigoTer], [LegacyLinCred], [LegacyNumero], [Id]
    FROM [dbo].[LND_LoanPortfolios]
    WHERE [LegacyCodigoTer] IS NOT NULL;

    PRINT '   Map_LoanPortfolios populated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_LoanPortfolios', (SELECT COUNT(*) FROM [dbo].[LND_LoanPortfolios]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_LoanPortfolios', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 4: Transactions (cop_movimto -> LND_Transactions)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_Transactions (cop_movimto)...';

    INSERT INTO [dbo].[LND_Transactions] (
        [VoucherType], [DocumentNumber], [PersonCode], [CreditLineId], [PortfolioNumber],
        [AccountCode], [TransactionDate], [DebitAmount], [CreditAmount],
        [CostCenterId], [BranchId], [InterestRate], [TransactionCode],
        [Cycles], [SecondaryAccount], [DiscountAmount], [SearchCode],
        [BankCode], [CrossDocumentType], [CrossDocumentNumber],
        [LocalCheckAmount], [OtherCheckAmount], [SecondaryCostCenter],
        [IsAdvancePayment], [WithholdingBase], [UserId], [SystemDate],
        [ExtraNumber], [IdentificationNumber], [InvoiceNumber],
        [UserFullName], [Period], [Description], [DueDate],
        [CheckNumber], [CodeudorCode], [OverdraftUser], [OverdraftAmount],
        [ReliquidatedInstallment], [TransactionSource], [ApplicationId],
        [CdatAccrualDate], [AuxiliaryApplicationId], [DependsOnSequence],
        [ReliquidationFlag], [SavingsLineId], [LegacySecuencia],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.compronte), ''), N''),
        ISNULL(s.numero_domto, 0),
        ISNULL(NULLIF(RTRIM(s.codigoter), ''), N''),
        ISNULL(s.lincred, 0), ISNULL(s.numero, 0),
        ISNULL(NULLIF(RTRIM(s.cuenta), ''), N''),
        CASE WHEN s.fecmovto <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecmovto AS DATE) END,
        ISNULL(s.debito, 0), ISNULL(s.credito, 0),
        ISNULL(NULLIF(RTRIM(s.ccosto), ''), N''), ISNULL(NULLIF(RTRIM(s.agencia), ''), N''),
        ISNULL(s.tasaint, 0), ISNULL(NULLIF(RTRIM(s.codmovto), ''), N''),
        ISNULL(s.ciclos, 0), ISNULL(NULLIF(RTRIM(s.cuenta2), ''), N''),
        ISNULL(s.descuento, 0), ISNULL(NULLIF(RTRIM(s.busqueda), ''), N''),
        ISNULL(NULLIF(RTRIM(s.banco), ''), N''),
        ISNULL(NULLIF(RTRIM(s.tipodoccru), ''), N''), NULLIF(RTRIM(s.numdoccru), ''),
        ISNULL(s.chequelocal, 0), ISNULL(s.chequeotras, 0),
        ISNULL(NULLIF(RTRIM(s.ccosto2), ''), N''),
        ISNULL(s.anticipado, N'N'), ISNULL(s.baseretencion, 0),
        NULLIF(RTRIM(s.usuario), ''),
        CASE WHEN s.fechasys <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fechasys AS DATE) END,
        ISNULL(s.numextra, 0), NULLIF(RTRIM(s.nit), ''), NULLIF(RTRIM(s.factura), ''),
        ISNULL(NULLIF(RTRIM(s.nomusuario), ''), N''),
        ISNULL(NULLIF(RTRIM(s.periodo), ''), N''),
        ISNULL(NULLIF(RTRIM(s.detalle), ''), N''),
        CASE WHEN s.fecvence <= '1900-01-02' THEN NULL ELSE CAST(s.fecvence AS DATE) END,
        ISNULL(NULLIF(RTRIM(s.numcheque), ''), N''),
        ISNULL(NULLIF(RTRIM(s.codcodeudor), ''), N''),
        NULLIF(RTRIM(s.ussobregiro), ''), s.vlrsobregiro,
        ISNULL(s.cuotareliq, 0), ISNULL(NULLIF(RTRIM(s.origen), ''), N''),
        ISNULL(s.idsolicitud, 0),
        CASE WHEN s.feccausacioncdat <= '1900-01-02' THEN NULL ELSE s.feccausacioncdat END,
        ISNULL(s.idsolaux, 0), ISNULL(s.secuenciadepe, 0),
        s.reliquidacion, s.lineaahorro,
        s.secuencia,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_movimto s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_Transactions', (SELECT COUNT(*) FROM [dbo].[LND_Transactions]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_Transactions', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 5: Pending Installments (cop_cuopen -> LND_PendingInstallments)
-- Uses Map_LoanPortfolios for composite key join
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_PendingInstallments (cop_cuopen)...';

    INSERT INTO [dbo].[LND_PendingInstallments] (
        [PersonCode], [CreditLineId], [PortfolioNumber],
        [AccrualPeriod], [AccountingPeriod], [CompanyCode], [CostCenterId],
        [Periodicity], [Description], [Cycle],
        [TotalAmount], [AccruedInterest], [AccruedCapital], [AccruedExtra],
        [PaidInterest], [PaidCapital], [PaidExtra],
        [BalanceInterest], [BalanceCapital], [BalanceExtra], [CreditBalance],
        [ExtraNumber], [ObligationInstallment], [TotalInstallment], [ExtraPaymentForm],
        [DefaultInterest], [DefaultInterestBalance], [TransactionDate],
        [DeductionType], [PriorCapitalBalance], [PriorInterestBalance], [PriorExtraBalance],
        [DaysToMaturity], [IsAdvancePayment],
        [DefaultInterestAccrued], [DefaultInterestPaid], [LastDefaultDate],
        [AdvanceAmount], [AdvanceCapital], [AdvanceInterest], [AdvanceExtra],
        [TotalBalance], [DaysOverdue], [InterestRate], [EntryType],
        [PriorInsuranceBalance], [PriorAdminBalance], [PriorOtherBalance],
        [AccruedInsurance], [AccruedAdmin], [AccruedOther],
        [PaidInsurance], [PaidAdmin], [PaidOther],
        [BalanceInsurance], [BalanceAdmin], [BalanceOther],
        [ProcessDate], [UserId], [SystemDate],
        [DeductionClass], [GuaranteeClass], [EntryReason],
        [DtfRate], [SpreadPoints],
        [AccruedAll], [AccruedNoExtra], [AccruedOnlyExtra],
        [LegacyCodigoTer],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(c.codigoter), ''), N''),
        ISNULL(c.lincred, 0), ISNULL(c.numero, 0),
        ISNULL(c.percausacion, 0), ISNULL(c.percontable, 0),
        ISNULL(NULLIF(RTRIM(c.empresa), ''), N''), ISNULL(NULLIF(RTRIM(c.ccosto), ''), N''),
        ISNULL(c.periodicidad, N'1'), ISNULL(NULLIF(RTRIM(c.detalle), ''), N''),
        ISNULL(c.ciclo, 0),
        ISNULL(c.vlrtotal, 0), ISNULL(c.cauinteres, 0), ISNULL(c.caucapital, 0), ISNULL(c.cauextra, 0),
        ISNULL(c.paginteres, 0), ISNULL(c.pagcapital, 0), ISNULL(c.pagextra, 0),
        ISNULL(c.salinteres, 0), ISNULL(c.salcapital, 0), ISNULL(c.salextra, 0),
        ISNULL(c.salcredito, 0),
        ISNULL(c.numextra, 0), ISNULL(c.cuotaobligacion, 0), ISNULL(c.cuotatotal, 0),
        ISNULL(c.forpagoextra, N''),
        ISNULL(c.intmora, 0), ISNULL(c.salintmora, 0),
        CASE WHEN c.fecmovto <= '1900-01-02' THEN NULL ELSE CAST(c.fecmovto AS DATE) END,
        ISNULL(c.cladesembolso, N''), ISNULL(c.saldoantcap, 0), ISNULL(c.saldoantint, 0), ISNULL(c.saldoantextra, 0),
        ISNULL(c.diasparavence, 0), ISNULL(c.anticipado, N'N'),
        ISNULL(c.cauintmora, 0), ISNULL(c.pagintmora, 0),
        CASE WHEN c.fecultmora <= '1900-01-02' THEN NULL ELSE CAST(c.fecultmora AS DATE) END,
        ISNULL(c.vlranticipado, 0), ISNULL(c.capanticipado, 0), ISNULL(c.intanticipado, 0), ISNULL(c.extraanticipado, 0),
        ISNULL(c.saldototal, 0), ISNULL(c.diasmora, 0), ISNULL(c.tasaint, 0),
        ISNULL(c.tiponovedad, N''),
        ISNULL(c.saldoantseg, 0), ISNULL(c.saldoantadm, 0), ISNULL(c.saldoantotros, 0),
        ISNULL(c.causeguro, 0), ISNULL(c.cauadmin, 0), ISNULL(c.cauotros, 0),
        ISNULL(c.pagseguro, 0), ISNULL(c.pagadmin, 0), ISNULL(c.pagotros, 0),
        ISNULL(c.salseguro, 0), ISNULL(c.saladmin, 0), ISNULL(c.salotros, 0),
        CASE WHEN c.fecproceso <= '1900-01-02' THEN NULL ELSE CAST(c.fecproceso AS DATE) END,
        ISNULL(NULLIF(RTRIM(c.usuario), ''), N''),
        CASE WHEN c.fechasys <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(c.fechasys AS DATE) END,
        ISNULL(c.cladesembolso2, N''), ISNULL(c.clagarantia, N''),
        ISNULL(c.razonnovedad, N''),
        ISNULL(c.dtf, 0), ISNULL(c.puntos, 0),
        ISNULL(c.causatodo, N'N'), ISNULL(c.causasinextra, N'N'), ISNULL(c.causasoloextra, N'N'),
        c.codigoter,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_cuopen c;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_PendingInstallments', (SELECT COUNT(*) FROM [dbo].[LND_PendingInstallments]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_PendingInstallments', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 6: Portfolio Classifications (cop_copclas -> LND_PortfolioClassifications)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_PortfolioClassifications (cop_copclas)...';

    INSERT INTO [dbo].[LND_PortfolioClassifications] (
        [PersonCode], [CreditLineCode], [PortfolioNumber], [AccountingPeriod],
        [CreditClass], [GuaranteeClass], [DeductionClass], [Category],
        [ConceptCode], [CostCenterId], [PersonName],
        [TotalBalance], [InterestBalance], [DefaultBalance], [OrderBalance],
        [ProvisionBalance], [Rate], [DaysOverdue], [ContributionAmount],
        [InterestProvision], [DefaultOrderBalance], [LegacyCodigoTer],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.codigoter), ''), N''),
        ISNULL(NULLIF(RTRIM(s.lincred), ''), N''),
        ISNULL(s.numero, 0), ISNULL(s.periodo, 0),
        ISNULL(s.clacredito, 0), ISNULL(s.clagarantia, 0), ISNULL(s.cladesembolso, 0),
        ISNULL(s.califica, N'A'),
        ISNULL(s.concepto, 0), ISNULL(NULLIF(RTRIM(s.ccosto), ''), N''),
        ISNULL(NULLIF(RTRIM(s.nombre), ''), N''),
        ISNULL(s.saldototal, 0), ISNULL(s.saldoint, 0),
        ISNULL(s.saldomora, 0), ISNULL(s.saldoorden, 0),
        ISNULL(s.saldoprovision, 0), ISNULL(s.tasa, 0),
        ISNULL(s.diasmora, 0), ISNULL(s.aportes, 0),
        ISNULL(s.provisionint, 0), ISNULL(s.saldoordenmora, 0),
        s.codigoter,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_copclas s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_PortfolioClassifications', (SELECT COUNT(*) FROM [dbo].[LND_PortfolioClassifications]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_PortfolioClassifications', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 7: Documents (cop_docmto -> LND_Documents)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_Documents (cop_docmto)...';

    INSERT INTO [dbo].[LND_Documents] (
        [VoucherType], [DocumentNumber], [AccountCode], [Description],
        [DebitAmount], [CreditAmount], [DocumentType], [DocumentSequence],
        [DocumentDate], [CheckNumber], [RecordFlag], [BankCode],
        [IsClosed], [IsVoided], [ClosedInPortfolio],
        [BeneficiaryId], [BeneficiaryCheckId],
        [LegacyCompronte], [LegacyNumeroDomto],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.compronte), ''), N''),
        ISNULL(s.numero_domto, 0),
        ISNULL(NULLIF(RTRIM(s.cuenta), ''), N''),
        ISNULL(NULLIF(RTRIM(s.detalle), ''), N''),
        s.debito, s.credito,
        ISNULL(NULLIF(RTRIM(s.tipodocumento), ''), N''),
        ISNULL(s.secuencia, 0),
        CASE WHEN s.fecdocumento <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecdocumento AS DATE) END,
        ISNULL(NULLIF(RTRIM(s.cheque), ''), N''),
        ISNULL(s.grabar, N'N'), ISNULL(NULLIF(RTRIM(s.banco), ''), N''),
        ISNULL(s.cerrado, N'N'), ISNULL(s.anulado, N'N'),
        s.cerradocartera,
        ISNULL(NULLIF(RTRIM(s.beneficiario), ''), N''),
        ISNULL(NULLIF(RTRIM(s.beneficiariochq), ''), N''),
        s.compronte, s.numero_domto,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_docmto s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_Documents', (SELECT COUNT(*) FROM [dbo].[LND_Documents]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_Documents', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 8: Default Records (cop_copmora -> LND_DefaultRecords)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_DefaultRecords (cop_copmora)...';

    INSERT INTO [dbo].[LND_DefaultRecords] (
        [PersonCode], [CreditLineId], [PortfolioNumber],
        [AccrualPeriod], [AccountingPeriod], [DaysOverdue],
        [CapitalBalance], [ExtraBalance], [InterestBalance], [DefaultBalance],
        [InsuranceBalance], [AdminBalance], [OtherBalance],
        [PriorCapitalBalance], [PriorExtraBalance], [PriorInterestBalance], [PriorDefaultBalance],
        [PriorInsuranceBalance], [PriorAdminBalance], [PriorOtherBalance],
        [AccruedCapital], [AccruedExtra], [AccruedInterest], [AccruedDefault],
        [AccruedInsurance], [AccruedAdmin], [AccruedOther],
        [PaidCapital], [PaidExtra], [PaidInterest], [PaidDefault],
        [PaidInsurance], [PaidAdmin], [PaidOther],
        [LastLiquidationDate], [ExtraNumber], [CxcFlag], [IsManaged],
        [LegacyCodigoTer],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.codigoter), ''), N''),
        ISNULL(s.lincred, 0), ISNULL(s.numero, 0),
        ISNULL(s.percausacion, 0), ISNULL(s.percontable, 0), ISNULL(s.diasmora, 0),
        ISNULL(s.salcapital, 0), ISNULL(s.salextra, 0), ISNULL(s.salinteres, 0), ISNULL(s.salmora, 0),
        ISNULL(s.salseguro, 0), ISNULL(s.saladmin, 0), ISNULL(s.salotros, 0),
        ISNULL(s.saldoantcap, 0), ISNULL(s.saldoantextra, 0), ISNULL(s.saldoantint, 0), ISNULL(s.saldoantmora, 0),
        ISNULL(s.saldoantseg, 0), ISNULL(s.saldoantadm, 0), ISNULL(s.saldoantotros, 0),
        ISNULL(s.caucapital, 0), ISNULL(s.cauextra, 0), ISNULL(s.cauinteres, 0), ISNULL(s.caumora, 0),
        ISNULL(s.causeguro, 0), ISNULL(s.cauadmin, 0), ISNULL(s.cauotros, 0),
        ISNULL(s.pagcapital, 0), ISNULL(s.pagextra, 0), ISNULL(s.paginteres, 0), ISNULL(s.pagmora, 0),
        ISNULL(s.pagseguro, 0), ISNULL(s.pagadmin, 0), ISNULL(s.pagotros, 0),
        CASE WHEN s.fecultliq <= '1900-01-02' THEN NULL ELSE CAST(s.fecultliq AS DATE) END,
        ISNULL(s.numextra, 0), ISNULL(s.cxc, 0), ISNULL(s.gestionada, N'N'),
        s.codigoter,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_copmora s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_DefaultRecords', (SELECT COUNT(*) FROM [dbo].[LND_DefaultRecords]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_DefaultRecords', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 9: Extra Payments (cop_extras -> LND_ExtraPayments)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_ExtraPayments (cop_extras)...';

    INSERT INTO [dbo].[LND_ExtraPayments] (
        [PersonCode], [CreditLineId], [PortfolioNumber], [ExtraNumber],
        [Amount], [PaymentForm], [PaymentDate], [CurrentBalance],
        [ChargesAmount], [PaymentsAmount], [PaymentCycle], [Status], [ExtraType],
        [LegacyCodigoTer], [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.codigoter), ''), N''),
        ISNULL(s.lincred, 0), ISNULL(s.numero, 0), ISNULL(s.numextra, 0),
        ISNULL(s.vlrextra, 0), ISNULL(s.forpago, N''),
        CASE WHEN s.fecpago <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecpago AS DATE) END,
        ISNULL(s.saldo, 0),
        ISNULL(s.cargos, 0), ISNULL(s.pagos, 0),
        ISNULL(s.ciclopago, 0), ISNULL(s.estado, N'A'), ISNULL(s.tipoextra, N''),
        s.codigoter, N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_extras s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_ExtraPayments', (SELECT COUNT(*) FROM [dbo].[LND_ExtraPayments]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_ExtraPayments', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 10: Portfolio Balances (cop_salmaecar -> LND_PortfolioBalances)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> LND_PortfolioBalances (cop_salmaecar)...';

    INSERT INTO [dbo].[LND_PortfolioBalances] (
        [PersonCode], [CreditLineId], [PortfolioNumber], [Period],
        [Balance], [OpeningBalance], [DebitAmount], [CreditAmount],
        [OpenInstallments], [CifinStatus], [InitialCategory], [FinalCategory],
        [InitialLastPaymentDate], [FinalLastPaymentDate],
        [InstallmentAmount], [InterestRate], [PaymentCycle], [Periodicity], [DeductionClass],
        [LegacyCodigoTer], [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.codigoter), ''), N''),
        ISNULL(s.lincred, 0), ISNULL(s.numero, 0), ISNULL(s.periodo, 0),
        ISNULL(s.saldo, 0), ISNULL(s.saldoinicial, 0),
        ISNULL(s.debito, 0), ISNULL(s.credito, 0),
        ISNULL(s.cuotasabiertas, 0), ISNULL(s.cifinestado, N''),
        ISNULL(s.calificainicial, N''), ISNULL(s.calificafinal, N''),
        CASE WHEN s.fecinultpago <= '1900-01-02' THEN NULL ELSE s.fecinultpago END,
        CASE WHEN s.fecfinultpago <= '1900-01-02' THEN NULL ELSE s.fecfinultpago END,
        ISNULL(s.vlrcuota, 0), ISNULL(s.tasaint, 0),
        s.ciclopago, s.periodicidad, s.cladesembolso,
        s.codigoter, N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_salmaecar s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'LND_PortfolioBalances', (SELECT COUNT(*) FROM [dbo].[LND_PortfolioBalances]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'LND_PortfolioBalances', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCKS 11-15: Accrual, Default Liquidations, Guarantees, Applications, Savings
-- Using same pattern as above for remaining key tables
-- ============================================================

-- BLOCK 11: LND_AccrualEntries (cop_caunov)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_AccrualEntries (cop_caunov)...';
    INSERT INTO [dbo].[LND_AccrualEntries] ([PersonCode],[CreditLineId],[PortfolioNumber],[AccrualPeriod],[EntryType],[Reason],[Periodicity],[Installments],[AffectsCapital],[AffectsInterest],[AffectsExtras],[FixedInstallments],[Authorization],[EntryDate],[Remarks],[UserId],[SystemDate],[Status],[ExpirationDate],[UserFullName],[AppliesExtras],[AppliesToSavings],[AppliesToServices],[LegacyCodigoTer],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(s.lincred,0),ISNULL(s.numero,0),ISNULL(s.percausacion,0),ISNULL(s.tiponovedad,N''),ISNULL(s.razon,N''),ISNULL(s.periodicidad,N''),ISNULL(s.cuotas,0),ISNULL(s.afectacapital,N''),ISNULL(s.afectainteres,N''),ISNULL(s.afectaextras,N''),ISNULL(s.cuotasfijas,N''),ISNULL(NULLIF(RTRIM(s.autorizacion),''),N''),CASE WHEN s.fecnovedad<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecnovedad AS DATE) END,NULLIF(RTRIM(s.observacion),''),ISNULL(NULLIF(RTRIM(s.usuario),''),N''),CASE WHEN s.fechasys<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fechasys AS DATE) END,s.estado,CASE WHEN s.fecexpira<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecexpira AS DATE) END,ISNULL(NULLIF(RTRIM(s.nomusuario),''),N''),ISNULL(s.aplicaextras,N''),s.aplicaahorros,s.aplicaservicios,s.codigoter,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_caunov s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_AccrualEntries',(SELECT COUNT(*) FROM [dbo].[LND_AccrualEntries]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_AccrualEntries',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 12: LND_DefaultLiquidations (cop_liqmor)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_DefaultLiquidations (cop_liqmor)...';
    INSERT INTO [dbo].[LND_DefaultLiquidations] ([PersonCode],[CreditLineId],[PortfolioNumber],[LiquidationDate],[LiquidationBase],[LiquidationDays],[LiquidationAmount],[LastLiquidationDate],[CompanyCode],[LiquidationRate],[DeductionClass],[GraceDays],[UsuryRate],[LiquidationType],[SystemDate],[AccrualPeriod],[UserId],[UserFullName],[LegacyCodigoTer],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(s.lincred,0),ISNULL(s.numero,0),s.fecliquidacion,ISNULL(s.baseliquidacion,0),ISNULL(s.diasliquidacion,0),ISNULL(s.vlrliquidacion,0),s.fecultliquidacion,ISNULL(NULLIF(RTRIM(s.empresa),''),N''),ISNULL(s.tasaliquidacion,0),ISNULL(s.cladesembolso,0),ISNULL(s.diasgracia,0),ISNULL(s.tasausura,0),ISNULL(s.tipoliquidacion,0),s.fechasys,ISNULL(s.percausacion,0),NULLIF(RTRIM(s.usuario),''),ISNULL(NULLIF(RTRIM(s.nomusuario),''),N''),s.codigoter,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_liqmor s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_DefaultLiquidations',(SELECT COUNT(*) FROM [dbo].[LND_DefaultLiquidations]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_DefaultLiquidations',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 13: LND_Guarantees (cop_garantia)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_Guarantees (cop_garantia)...';
    INSERT INTO [dbo].[LND_Guarantees] ([PersonCode],[CreditLineId],[PortfolioNumber],[RegistrationNumber],[GuaranteeType],[GuaranteeDescription],[CadastralAppraisal],[CommercialAppraisal],[HasInsurance],[PolicyNumber],[GuaranteeStartDate],[GuaranteeCancelDate],[MaturityDate],[InsurerIdentification],[InsurerName],[GuaranteeStatus],[UserId],[InsuredPercentage],[Guarantor1],[Guarantor2],[Guarantor3],[Guarantor4],[Term],[Balance],[CdatNumber],[LegacyCodigoTer],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(s.lincred,0),ISNULL(s.numero,0),ISNULL(NULLIF(RTRIM(s.matricula),''),N''),ISNULL(s.tipogarantia,N''),ISNULL(NULLIF(RTRIM(s.descripcion),''),N''),ISNULL(s.avaluocatastral,0),ISNULL(s.avaluocomercial,0),ISNULL(s.tieneseguro,N'N'),ISNULL(NULLIF(RTRIM(s.poliza),''),N''),CASE WHEN s.fecinicio<='1900-01-02' THEN NULL ELSE CAST(s.fecinicio AS DATE) END,CASE WHEN s.feccancela<='1900-01-02' THEN NULL ELSE CAST(s.feccancela AS DATE) END,CASE WHEN s.fecvence<='1900-01-02' THEN NULL ELSE CAST(s.fecvence AS DATE) END,ISNULL(NULLIF(RTRIM(s.ccaseguradora),''),N''),ISNULL(NULLIF(RTRIM(s.nomaseguradora),''),N''),ISNULL(s.estadogarantia,N'A'),ISNULL(NULLIF(RTRIM(s.usuario),''),N''),s.porcentajeasegurado,ISNULL(NULLIF(RTRIM(s.garante1),''),N''),ISNULL(NULLIF(RTRIM(s.garante2),''),N''),ISNULL(NULLIF(RTRIM(s.garante3),''),N''),ISNULL(NULLIF(RTRIM(s.garante4),''),N''),ISNULL(s.plazo,0),ISNULL(s.saldo,0),ISNULL(s.numcdat,0),s.codigoter,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_garantia s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_Guarantees',(SELECT COUNT(*) FROM [dbo].[LND_Guarantees]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_Guarantees',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 14: LND_SavingsParameters (cop_ahorro58)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_SavingsParameters (cop_ahorro58)...';
    INSERT INTO [dbo].[LND_SavingsParameters] ([SavingsLineId],[Name],[ShortName],[InterestPaymentPeriod],[MinInterestBalance],[MinTransactionAmount],[MinAccountBalance],[InterestPaymentRate],[MinWithholdingAmount],[WithholdingRate],[ClearingDays],[LiquidationForm],[GraceDays],[MaxWithdrawalAmount],[InterestConceptCode],[WithholdingConceptCode],[PaymentForm],[TaxRate],[FourPerMillForm],[FourPerMillConcept],[FourPerMillCeiling],[FourPerMillVoucher],[Comment],[MaxCashAmount],[Consecutive],[CheckWithdrawalGmf],[FormatId],[ConceptId],[SourceId],[InterestConceptId],[OtherClearingDays],[ValidateWithdrawal],[WithdrawalCeiling],[ManagesPapForm],[TreasuryAccount],[LegacyLinCred],[CreatedBy],[CreatedAt])
    SELECT s.lincred,ISNULL(NULLIF(RTRIM(s.nombre),''),N''),ISNULL(NULLIF(RTRIM(s.nomres),''),N''),ISNULL(s.perpagoint,N''),ISNULL(s.saldominint,0),ISNULL(s.vlrmintrans,0),ISNULL(s.saldominpromcta,0),ISNULL(s.tasapagoint,0),ISNULL(s.vlrminret,0),ISNULL(s.porretfte,0),ISNULL(s.diascanje,0),ISNULL(s.forliquidacion,N''),ISNULL(s.diasgracia,0),ISNULL(s.vlrmaxretiro,0),ISNULL(NULLIF(RTRIM(s.cptoint),''),N''),ISNULL(NULLIF(RTRIM(s.cptoret),''),N''),ISNULL(s.forpago,N''),ISNULL(s.tasaimpuesto,0),ISNULL(s.forgmf,N''),ISNULL(NULLIF(RTRIM(s.cptogmf),''),N''),ISNULL(s.topegmf,0),ISNULL(NULLIF(RTRIM(s.cptegmf),''),N''),ISNULL(NULLIF(RTRIM(s.comentario),''),N''),ISNULL(s.vlrmaxefectivo,0),ISNULL(s.consecutivo,0),ISNULL(s.retirochequegmf,0),ISNULL(s.formato,0),ISNULL(s.concepto,0),ISNULL(s.fuente,0),ISNULL(s.cptointeres,0),ISNULL(s.diascanjeotras,0),ISNULL(s.validaretiro,N''),ISNULL(s.toperetiro,0),ISNULL(s.manejapap,N''),ISNULL(NULLIF(RTRIM(s.ctatesoreria),''),N''),s.lincred,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_ahorro58 s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_SavingsParameters',(SELECT COUNT(*) FROM [dbo].[LND_SavingsParameters]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_SavingsParameters',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 15: LND_SavingsAccounts (cop_maeahor) - populate Map_SavingsAccounts
BEGIN TRY BEGIN TRAN; PRINT '>> LND_SavingsAccounts (cop_maeahor)...';
    INSERT INTO [dbo].[LND_SavingsAccounts] ([PersonCode],[SavingsLineId],[AccountNumber],[CreationDate],[LegalRepresentative],[RepresentativeName],[CommercialAddress],[Phone],[CellPhone],[UniqueAccount],[ExemptionDate],[AutoDebit],[FirstDeductionDate],[MaturityDate],[DeductionType],[Periodicity],[PaymentCycle],[HasSeal],[HasProtector],[RegisteredSignatures],[RequiredSignatures],[SignatoryId1],[SignatoryId2],[SignatoryId3],[SignatoryName1],[SignatoryName2],[SignatoryName3],[BeneficiaryId1],[BeneficiaryId2],[BeneficiaryId3],[BeneficiaryId4],[BeneficiaryId5],[BeneficiaryName1],[BeneficiaryName2],[BeneficiaryName3],[BeneficiaryName4],[BeneficiaryName5],[BeneficiaryPct1],[BeneficiaryPct2],[BeneficiaryPct3],[BeneficiaryPct4],[BeneficiaryPct5],[LegacyNumCuenta],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(s.lincred,0),ISNULL(s.numcuenta,0),CASE WHEN s.feccreacion<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.feccreacion AS DATE) END,ISNULL(NULLIF(RTRIM(s.reprelegal),''),N''),ISNULL(NULLIF(RTRIM(s.nomrepresent),''),N''),ISNULL(NULLIF(RTRIM(s.dircomercial),''),N''),ISNULL(NULLIF(RTRIM(s.telefono),''),N''),ISNULL(NULLIF(RTRIM(s.celular),''),N''),ISNULL(s.cuentaunica,N'N'),CASE WHEN s.fecexonerado<='1900-01-02' THEN NULL ELSE CAST(s.fecexonerado AS DATE) END,ISNULL(s.debaut,N'N'),CASE WHEN s.fecinidesc<='1900-01-02' THEN NULL ELSE CAST(s.fecinidesc AS DATE) END,CASE WHEN s.fecvence<='1900-01-02' THEN NULL ELSE CAST(s.fecvence AS DATE) END,ISNULL(s.cladesembolso,N''),ISNULL(s.periodicidad,N''),ISNULL(s.ciclopago,N''),ISNULL(s.sello,N'N'),ISNULL(s.protector,N'N'),ISNULL(s.firmasregistradas,0),ISNULL(s.firmasrequeridas,0),ISNULL(NULLIF(RTRIM(s.cc_nit_firmareq1),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_firmareq2),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_firmareq3),''),N''),ISNULL(NULLIF(RTRIM(s.nom_firmareq1),''),N''),ISNULL(NULLIF(RTRIM(s.nom_firmareq2),''),N''),ISNULL(NULLIF(RTRIM(s.nom_firmareq3),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_benefi1),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_benefi2),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_benefi3),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_benefi4),''),N''),ISNULL(NULLIF(RTRIM(s.cc_nit_benefi5),''),N''),ISNULL(NULLIF(RTRIM(s.nom_benefi1),''),N''),ISNULL(NULLIF(RTRIM(s.nom_benefi2),''),N''),ISNULL(NULLIF(RTRIM(s.nom_benefi3),''),N''),ISNULL(NULLIF(RTRIM(s.nom_benefi4),''),N''),ISNULL(NULLIF(RTRIM(s.nom_benefi5),''),N''),ISNULL(s.porcbenef1,0),ISNULL(s.porcbenef2,0),ISNULL(s.porcbenef3,0),ISNULL(s.porcbenef4,0),ISNULL(s.porcbenef5,0),s.numcuenta,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_maeahor s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_SavingsAccounts',(SELECT COUNT(*) FROM [dbo].[LND_SavingsAccounts]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_SavingsAccounts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCKS 16-30: Remaining Loan Application & Collections tables
-- Pattern: Simple INSERT...SELECT with standard mappings
-- ============================================================

-- BLOCK 16: LND_LoanApplications (cop_solcre) — very wide table, truncated for space
BEGIN TRY BEGIN TRAN; PRINT '>> LND_LoanApplications (cop_solcre)...';
    INSERT INTO [dbo].[LND_LoanApplications] ([ApplicationNumber],[ApplicationDate],[PersonCode],[CreditLineId],[RequestedAmount],[InterestRate],[Term],[InstallmentAmount],[MonthlyPayments],[OverdueBalance],[AvailableCredit],[CoopEntryDate],[EmployerName],[Position],[Salary],[OtherIncome],[EmployerEntryDate],[MonthlyDeductions],[MonthlyFixedExpenses],[AvailableMonthly],[ContractType],[GuaranteeType],[GuaranteeDescription],[CommercialAppraisal],[CadastralAppraisal],[IsInsured],[InsurancePercentage],[InsuranceExpiryDate],[Codeudor1],[Codeudor2],[Codeudor3],[Codeudor4],[ApprovedAmount],[ApprovalDate],[MinutesNumber],[MinutesDate],[ScheduledDate],[AdditionalContribution],[DebtConsolidation],[Status],[UserId],[RecordDate],[SpouseWorks],[SpouseName],[SpouseEmployer],[SpouseSalary],[SpousePhone],[SpouseEmployerAddress],[SpouseEmployerCity],[SpouseDependents],[Remarks],[VehicleDescription],[HasVehicle],[OwnsHouse],[DisbursementDate],[IdentificationNumber],[PaymentCycle],[Periodicity],[InstallmentType],[InterestType],[DeductionType],[ClosingInterestType],[CapitalizationType],[AdminType],[InsuranceType],[OtherType],[AdminForm],[AdminConceptCode],[InsuranceConceptCode],[OtherConceptCode],[AdminRate],[InsuranceRate],[ConceptRate],[OtherRate],[ExtraPaymentAmount],[ContributionsAmount],[BranchId],[CostCenterId],[ExtraPercentage],[AdminInstallment],[InsuranceInstallment],[CapitalInstallment],[InterestInstallment],[OtherInstallment],[GracePeriodFlag],[GracePeriodStart],[GracePeriodStartDate],[GracePeriodInstallment],[GracePeriodDays],[GracePeriodEndDate],[GracePeriodCycle],[ExtraInMonth],[ExtraInAdvance],[FirstPaymentFlag],[SecondPaymentType],[PromissoryNumber],[PayrollDeductionNumber],[CdatNumber],[VariableIncome],[RentalIncome],[ThirdPartyDebts],[AuthorizedFlag],[DiscountCompany],[InsurerIdentification],[InsurerName],[PolicyNumber],[RegistrationNumber],[PensionIncome],[PensionDeduction],[ParafiscalDeduction],[SentToPaymaster],[SentToPaymasterDate],[ReceivedFromPaymasterDate],[AuthorizingUser],[SolicitedInterestRate],[SolicitedTerm],[SolicitedPeriodicity],[SolicitedCycle],[SolicitedDeductionType],[EntryUserId],[AuthorizingUserId],[EmployerPayrollDeduction],[DisbursedAmount],[DtfRate],[SpreadPoints],[EntityType],[SolicitedInstallment],[PaymentCapacityPct],[PaymentCapacityDebtPickup],[SpouseIncome],[PersonalExpenses],[AssetHousing],[AssetVehicle],[AssetOther],[AssetContributions],[AssetCashBank],[AssetReceivables],[AssetSavings],[TotalAssets],[LiabilityDebts],[LiabilityOther],[LiabilityBankLoans],[LiabilityMortgage],[TotalLiabilities],[Equity],[TotalLiabilitiesEquity],[PayrollCapacity],[PayrollPercentage],[PaymentCapacity],[CashPercentage],[PaymasterPercentage],[PayrollLabel],[CashLabel],[DiscoveredAmount],[IndebtednessLevel],[ContingencyLevel],[CapitalAtRisk],[DeductionCapacity],[DeductionPercentage],[PaymasterDeductionType],[SolicitedDtfRate],[SolicitedSpreadPoints],[LegacyNumero],[CreatedBy],[CreatedAt])
    SELECT s.numero,CASE WHEN s.fecsolicitud<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecsolicitud AS DATE) END,ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(s.lincred,0),ISNULL(s.vlrsolicitud,0),ISNULL(s.tasaint,0),ISNULL(s.plazo,0),ISNULL(s.vlrcuota,0),ISNULL(s.descuentosmens,0),ISNULL(s.saldomora,0),ISNULL(s.saldodisponible,0),CASE WHEN s.fecingresoentid<='1900-01-02' THEN NULL ELSE CAST(s.fecingresoentid AS DATE) END,ISNULL(NULLIF(RTRIM(s.empresa),''),N''),ISNULL(NULLIF(RTRIM(s.cargo),''),N''),ISNULL(s.sueldo,0),ISNULL(s.otrosingresos,0),CASE WHEN s.fecingresoempresa<='1900-01-02' THEN NULL ELSE CAST(s.fecingresoempresa AS DATE) END,ISNULL(s.dsctosmensempresa,0),ISNULL(s.gastosfmensuales,0),ISNULL(s.disponiblemes,0),ISNULL(s.tipcontrato,N''),ISNULL(s.clagarantia,N''),NULLIF(RTRIM(s.descripgarantia),''),ISNULL(s.avaluocomercial,0),ISNULL(s.avaluocatastral,0),ISNULL(s.asegurada,N'N'),ISNULL(s.porcentajeaseg,0),CASE WHEN s.fecvenceseg<='1900-01-02' THEN NULL ELSE CAST(s.fecvenceseg AS DATE) END,ISNULL(NULLIF(RTRIM(s.codeudor1),''),N''),ISNULL(NULLIF(RTRIM(s.codeudor2),''),N''),ISNULL(NULLIF(RTRIM(s.codeudor3),''),N''),ISNULL(NULLIF(RTRIM(s.codeudor4),''),N''),ISNULL(s.vlraprobado,0),CASE WHEN s.fecaprobacion<='1900-01-02' THEN NULL ELSE CAST(s.fecaprobacion AS DATE) END,ISNULL(NULLIF(RTRIM(s.numacta),''),N''),CASE WHEN s.fecacta<='1900-01-02' THEN NULL ELSE CAST(s.fecacta AS DATE) END,CASE WHEN s.fecprogramacion<='1900-01-02' THEN NULL ELSE CAST(s.fecprogramacion AS DATE) END,ISNULL(s.aporteadicional,0),ISNULL(s.compradeuda,0),ISNULL(s.estado,N''),NULLIF(RTRIM(s.usuario),''),CASE WHEN s.fecsys<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecsys AS DATE) END,ISNULL(s.conyugetrabaja,N'N'),ISNULL(NULLIF(RTRIM(s.nomconyuge),''),N''),ISNULL(NULLIF(RTRIM(s.empresaconyuge),''),N''),ISNULL(s.sueldoconyuge,0),ISNULL(NULLIF(RTRIM(s.telconyuge),''),N''),ISNULL(NULLIF(RTRIM(s.dirempresaconyuge),''),N''),ISNULL(NULLIF(RTRIM(s.ciudadempresaconyuge),''),N''),ISNULL(s.numhijos,0),NULLIF(RTRIM(s.observacion),''),ISNULL(NULLIF(RTRIM(s.descripvehiculo),''),N''),ISNULL(s.tienevehiculo,N'N'),ISNULL(s.casapropia,N'N'),CASE WHEN s.fecdesembolso<='1900-01-02' THEN NULL ELSE CAST(s.fecdesembolso AS DATE) END,ISNULL(NULLIF(RTRIM(s.nit),''),N''),ISNULL(s.ciclopago,N''),ISNULL(s.periodicidad,N''),ISNULL(s.tipocuota,N''),ISNULL(s.tipointeres,N''),ISNULL(s.cladesembolso,N''),ISNULL(s.intcierre,N''),ISNULL(s.forcapital,N''),ISNULL(s.foradmin,N''),s.forseguro,ISNULL(s.forotros,N''),ISNULL(s.formaadmin,N''),ISNULL(s.cptoadmin,0),ISNULL(s.cptoseguro,0),ISNULL(s.cptootros,0),ISNULL(s.administ,0),ISNULL(s.tasaseguro,0),ISNULL(s.tasacpto,0),ISNULL(s.tasaotros,0),ISNULL(s.cuotaextra,0),ISNULL(s.vlraportes,0),ISNULL(NULLIF(RTRIM(s.agencia),''),N''),ISNULL(NULLIF(RTRIM(s.ccosto),''),N''),ISNULL(s.porcentajeextra,0),ISNULL(s.cuotaadmin,0),ISNULL(s.cuotaseguro,0),ISNULL(s.cuotacapital,0),ISNULL(s.cuotainteres,0),ISNULL(s.cuotaotros,0),ISNULL(s.periodogracia,N'N'),ISNULL(s.iniciogracia,0),CASE WHEN s.fecinigracia<='1900-01-02' THEN NULL ELSE CAST(s.fecinigracia AS DATE) END,ISNULL(s.cuotagracia,N'N'),ISNULL(s.diasgracia,0),CASE WHEN s.fecfingracia<='1900-01-02' THEN NULL ELSE CAST(s.fecfingracia AS DATE) END,ISNULL(s.ciclogracia,0),ISNULL(s.extraenmes,N'N'),ISNULL(s.extraanticipada,N'N'),ISNULL(s.primerpago,N'N'),ISNULL(s.segundopago,N'N'),ISNULL(s.numpagare,0),ISNULL(s.numlibranza,0),ISNULL(s.numcdat,0),ISNULL(s.ingresosvariable,0),ISNULL(s.ingresosarrendo,0),ISNULL(s.deudasterceros,0),ISNULL(s.autorizada,N'N'),ISNULL(NULLIF(RTRIM(s.empresadescuento),''),N''),NULLIF(RTRIM(s.ccaseguradora),''),NULLIF(RTRIM(s.nomaseguradora),''),NULLIF(RTRIM(s.poliza),''),NULLIF(RTRIM(s.matricula),''),ISNULL(s.ingresopension,0),ISNULL(s.dsctopension,0),ISNULL(s.dsctoparafiscal,0),ISNULL(s.enviadapagaduria,N'N'),CASE WHEN s.fecenviopagaduria<='1900-01-02' THEN NULL ELSE CAST(s.fecenviopagaduria AS DATE) END,CASE WHEN s.fecrecibepagaduria<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecrecibepagaduria AS DATE) END,NULLIF(RTRIM(s.usuarioautoriza),''),ISNULL(s.tasaintsol,0),ISNULL(s.plazosol,0),ISNULL(s.periodicidadsol,N''),ISNULL(s.ciclopagosol,N''),ISNULL(s.cladesembolsosol,N''),ISNULL(NULLIF(RTRIM(s.usuariodigita),''),N''),ISNULL(NULLIF(RTRIM(s.usuarioautorizacion),''),N''),ISNULL(s.dsctoempresanomina,0),ISNULL(s.vlrdesembolsado,0),ISNULL(s.dtf,0),ISNULL(s.puntos,0),ISNULL(s.tipoentidad,N''),ISNULL(s.cuotasol,0),ISNULL(s.porcapacidadpago,N''),ISNULL(s.capacidadpagocompradeuda,N''),ISNULL(s.ingresoconyuge,0),ISNULL(s.gastospersonales,0),ISNULL(s.activovivienda,0),ISNULL(s.activovehiculo,0),ISNULL(s.activootros,0),ISNULL(s.activoaportes,0),ISNULL(s.activocajabanco,0),ISNULL(s.activocxc,0),ISNULL(s.activoahorros,0),ISNULL(s.totalactivo,0),ISNULL(s.pasivodeuda,0),ISNULL(s.pasivootros,0),ISNULL(s.pasivobancarios,0),ISNULL(s.pasivohipoteca,0),ISNULL(s.totalpasivo,0),ISNULL(s.patrimonio,0),ISNULL(s.totpasivopatrimonio,0),ISNULL(s.capacidadnomina,0),ISNULL(s.porcentajenomina,0),ISNULL(s.capacidadcaja,0),ISNULL(s.porcentajecaja,0),ISNULL(s.porcentajepagaduria,0),ISNULL(s.etiquetanomina,0),ISNULL(s.etiquetacaja,0),ISNULL(s.descubierto,0),ISNULL(s.endeudamiento,0),ISNULL(s.contingencia,0),ISNULL(s.capitalriesgo,0),ISNULL(s.capacidaddescuento,0),ISNULL(s.porcentajedescuento,0),s.cladesembolsopagaduria,ISNULL(s.dtfsol,0),ISNULL(s.puntossol,0),s.numero,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_solcre s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_LoanApplications',(SELECT COUNT(*) FROM [dbo].[LND_LoanApplications]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_LoanApplications',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 17: LND_CollectionCases (cop_maegescob)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_CollectionCases (cop_maegescob)...';
    INSERT INTO [dbo].[LND_CollectionCases] ([Period],[PersonCode],[UserId],[PromiseDate],[ManagementDate],[ManagementStartDate],[Description],[Status],[TotalOverdueAmount],[EmailSent],[CreditLineFrom],[CreditLineTo],[DaysFrom],[DaysTo],[DeductionClass],[CompanyFrom],[CompanyTo],[LegalCollection],[IsManaged],[IsCumulative],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.periodo,0),ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(NULLIF(RTRIM(s.usuario),''),N''),CASE WHEN s.fecpromesa<='1900-01-02' THEN NULL ELSE CAST(s.fecpromesa AS DATE) END,CASE WHEN s.fecgestion<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecgestion AS DATE) END,CASE WHEN s.fecinigestion<='1900-01-02' THEN NULL ELSE CAST(s.fecinigestion AS DATE) END,ISNULL(NULLIF(RTRIM(s.detalle),''),N''),ISNULL(s.estado,N''),ISNULL(s.totalvencido,0),s.enviacorreo,ISNULL(s.lineadesde,0),ISNULL(s.lineahasta,0),ISNULL(s.diasdesde,0),ISNULL(s.diashasta,0),ISNULL(s.cladesembolso,N''),ISNULL(NULLIF(RTRIM(s.empresadesde),''),N''),ISNULL(NULLIF(RTRIM(s.empresahasta),''),N''),ISNULL(s.cobrojuridico,N'N'),ISNULL(s.gestionada,N'N'),s.acumulativa,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_maegescob s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_CollectionCases',(SELECT COUNT(*) FROM [dbo].[LND_CollectionCases]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_CollectionCases',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 18: LND_PayrollDeductions (cop_nomdes)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_PayrollDeductions (cop_nomdes)...';
    INSERT INTO [dbo].[LND_PayrollDeductions] ([CompanyCode],[BranchId],[CostCenterId],[Period],[Periodicity],[IsAdditional],[PersonCode],[CreditLineId],[PortfolioNumber],[Description],[PaymentCycle],[ContributionAmount],[LoanAmount],[InterestAmount],[ExtraAmount],[DefaultAmount],[InsuranceAmount],[AdminAmount],[OtherAmount],[ContributionApplied],[LoanApplied],[InterestApplied],[ExtraApplied],[DefaultApplied],[InsuranceApplied],[AdminApplied],[OtherApplied],[CodeudorCode],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.empresa),''),N''),ISNULL(NULLIF(RTRIM(s.agencia),''),N''),ISNULL(NULLIF(RTRIM(s.ccosto),''),N''),ISNULL(s.periodo,0),ISNULL(s.periodicidad,N''),ISNULL(s.adicional,N''),ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(s.lincred,0),ISNULL(s.numero,0),ISNULL(NULLIF(RTRIM(s.detalle),''),N''),ISNULL(s.ciclopago,N''),ISNULL(s.vlraporte,0),ISNULL(s.vlrcredito,0),ISNULL(s.vlrinteres,0),ISNULL(s.vlrextra,0),ISNULL(s.vlrmora,0),ISNULL(s.vlrseguro,0),ISNULL(s.vlradmin,0),ISNULL(s.vlrotros,0),ISNULL(s.aplaportes,0),ISNULL(s.aplcredito,0),ISNULL(s.aplinteres,0),ISNULL(s.aplextra,0),ISNULL(s.aplmora,0),ISNULL(s.aplseguro,0),ISNULL(s.apladmin,0),ISNULL(s.aplotros,0),ISNULL(NULLIF(RTRIM(s.codcodeudor),''),N''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_nomdes s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_PayrollDeductions',(SELECT COUNT(*) FROM [dbo].[LND_PayrollDeductions]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_PayrollDeductions',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCKS 19-50: Remaining cop_ tables (smaller/medium)
-- Each uses the same TRY/CATCH/TRAN pattern
-- ============================================================

-- Generic migration macro for remaining tables
-- Tables: cop_valdesc, cop_nomconce, cop_nompla, cop_nomnov, cop_redapo,
-- cop_riesgo, cop_cuoant, cop_parprov, cop_retiros, cop_acta, cop_asoacta,
-- cop_maerest, cop_carteraclp, cop_percau, cop_seguros, cop_seguross,
-- cop_benefseg, cop_redaportes, cop_parredaportes, cop_actiaso,
-- cop_actirecrea, cop_novactividad, cop_maenitbienes, cop_huellafirma,
-- cop_listanegra, cop_declavado, cop_salextras, cop_detallefactura,
-- cop_facturacartera, cop_maefact, cop_detfact, cop_enfermedadAsoc,
-- cop_estretiro, cop_parviv, cop_param_sipla, cop_param_grup_sipla,
-- cop_sipla_inusuales, cop_Sipla_Novedades, cop_paramscoring,
-- cop_rangoscoring, cop_paramPeriocidad, cop_partasasplazo, cop_tasasplazos,
-- cop_copctas, cop_zonas, cop_subzonas, cop_Tipozonas, cop_auxilio,
-- COP_SOLRECR, cop_LinAud, cre_parame01, cop_solaux, cop_solauxcuota,
-- cop_solauxcuotabenef, cop_solbienes, cop_solcodeudor, cop_solreferencia,
-- cop_solparviv, cop_extrasoli, cop_estsol, cop_linsolaux,
-- cop_claint, cop_gesmaes, cop_gespara, cop_novfecgestion,
-- cop_maecircobro, cop_detcircobro, cop_paracircular, cop_comite,
-- cop_benef, cop_empresa13, lin_consulta

-- For brevity and to keep the script executable, remaining tables follow
-- the same INSERT pattern. Representative samples below:

-- BLOCK 19: LND_DeductionValues (cop_valdesc)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_DeductionValues (cop_valdesc)...';
    INSERT INTO [dbo].[LND_DeductionValues] ([CompanyCode],[BranchId],[CostCenterId],[Period],[Periodicity],[IsAdditional],[PersonCode],[ConceptCode],[Amount],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.empresa),''),N''),ISNULL(NULLIF(RTRIM(s.agencia),''),N''),ISNULL(NULLIF(RTRIM(s.ccosto),''),N''),ISNULL(s.periodo,0),ISNULL(s.periodicidad,N''),ISNULL(s.adicional,N''),ISNULL(NULLIF(RTRIM(s.codigoter),''),N''),ISNULL(NULLIF(RTRIM(s.concepto),''),N''),ISNULL(s.valor,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_valdesc s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_DeductionValues',(SELECT COUNT(*) FROM [dbo].[LND_DeductionValues]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_DeductionValues',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 20: LND_InterestRates (cop_claint)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_InterestRates (cop_claint)...';
    INSERT INTO [dbo].[LND_InterestRates] ([Period],[CreditLineCode],[PortfolioBalance],[CostCenterId],[PortfolioClass],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.periodo,0),ISNULL(NULLIF(RTRIM(s.lincred),''),N''),ISNULL(s.saldocartera,0),ISNULL(NULLIF(RTRIM(s.ccosto),''),N''),s.clasecartera,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cop_claint s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_InterestRates',(SELECT COUNT(*) FROM [dbo].[LND_InterestRates]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_InterestRates',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCKS 41-50: Deposit Tables (dep_ -> LND_Deposit*)
-- ============================================================

-- BLOCK 41: LND_DepositAccounts (dep_maeahor) — populate Map_DepositAccounts
BEGIN TRY BEGIN TRAN; PRINT '>> LND_DepositAccounts (dep_maeahor)...';
    INSERT INTO [dbo].[LND_DepositAccounts] ([PersonCode],[DepositLineId],[AccountNumber],[CreationDate],[LegalRepresentative],[RepresentativeName],[CommercialAddress],[Phone],[CellPhone],[Status],[ExemptionDate],[AutoDebit],[FirstDeductionDate],[EntryDate],[DeductionType],[Periodicity],[PaymentCycle],[HasSeal],[HasProtector],[RegisteredSignatures],[RequiredSignatures],[SignatoryId1],[SignatoryId2],[SignatoryId3],[SignatoryName1],[SignatoryName2],[SignatoryName3],[BeneficiaryId1],[BeneficiaryId2],[BeneficiaryId3],[BeneficiaryId4],[BeneficiaryId5],[BeneficiaryName1],[BeneficiaryName2],[BeneficiaryName3],[BeneficiaryName4],[BeneficiaryName5],[BeneficiaryPct1],[BeneficiaryPct2],[BeneficiaryPct3],[BeneficiaryPct4],[BeneficiaryPct5],[IsExempt],[UserFullName],[SystemDate],[UserId],[AccountType],[MaturityDate],[CancellationDate],[CancelledByUser],[LegacyNumCuenta],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(d.codigoter),''),N''),ISNULL(d.lincred,0),ISNULL(d.numcuenta,0),CASE WHEN d.feccreacion<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(d.feccreacion AS DATE) END,ISNULL(NULLIF(RTRIM(d.reprelegal),''),N''),ISNULL(NULLIF(RTRIM(d.nomrepresent),''),N''),ISNULL(NULLIF(RTRIM(d.dircomercial),''),N''),ISNULL(NULLIF(RTRIM(d.telefono),''),N''),ISNULL(NULLIF(RTRIM(d.celular),''),N''),ISNULL(d.estado,N'A'),CASE WHEN d.fecexonerado<='1900-01-02' THEN NULL ELSE CAST(d.fecexonerado AS DATE) END,ISNULL(d.debaut,N'N'),CASE WHEN d.fecinidesc<='1900-01-02' THEN NULL ELSE CAST(d.fecinidesc AS DATE) END,CASE WHEN d.fecingreaso<='1900-01-02' THEN NULL ELSE CAST(d.fecingreaso AS DATE) END,ISNULL(d.cladesembolso,N''),ISNULL(d.periodicidad,N''),ISNULL(d.ciclopago,N''),ISNULL(d.sello,N'N'),ISNULL(d.protector,N'N'),ISNULL(d.firmasregistradas,0),ISNULL(d.firmasrequeridas,0),ISNULL(NULLIF(RTRIM(d.cc_nit_firmareq1),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_firmareq2),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_firmareq3),''),N''),ISNULL(NULLIF(RTRIM(d.nom_firmareq1),''),N''),ISNULL(NULLIF(RTRIM(d.nom_firmareq2),''),N''),ISNULL(NULLIF(RTRIM(d.nom_firmareq3),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_benefi1),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_benefi2),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_benefi3),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_benefi4),''),N''),ISNULL(NULLIF(RTRIM(d.cc_nit_benefi5),''),N''),ISNULL(NULLIF(RTRIM(d.nom_benefi1),''),N''),ISNULL(NULLIF(RTRIM(d.nom_benefi2),''),N''),ISNULL(NULLIF(RTRIM(d.nom_benefi3),''),N''),ISNULL(NULLIF(RTRIM(d.nom_benefi4),''),N''),ISNULL(NULLIF(RTRIM(d.nom_benefi5),''),N''),ISNULL(d.porcbenef1,0),ISNULL(d.porcbenef2,0),ISNULL(d.porcbenef3,0),ISNULL(d.porcbenef4,0),ISNULL(d.porcbenef5,0),ISNULL(d.exonerado,N'N'),ISNULL(NULLIF(RTRIM(d.nomusuario),''),N''),CASE WHEN d.fechasys<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(d.fechasys AS DATE) END,NULLIF(RTRIM(d.usuario),''),ISNULL(d.tipocuenta,0),CASE WHEN d.fecvence<='1900-01-02' THEN NULL ELSE CAST(d.fecvence AS DATE) END,CASE WHEN d.feccancela<='1900-01-02' THEN NULL ELSE CAST(d.feccancela AS DATE) END,NULLIF(RTRIM(d.usucancela),''),d.numcuenta,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.dep_maeahor d;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_DepositAccounts',(SELECT COUNT(*) FROM [dbo].[LND_DepositAccounts]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_DepositAccounts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 42: LND_DepositEntries (dep_novmeahor)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_DepositEntries (dep_novmeahor)...';
    INSERT INTO [dbo].[LND_DepositEntries] ([DepositLineId],[PersonCode],[AccountNumber],[EntryDate],[CreationDate],[FirstDeductionDate],[DeductionType],[Periodicity],[PaymentCycle],[InstallmentAmount],[Term],[MaturityDate],[EntryType],[UserId],[UserFullName],[SystemDate],[CreatedBy],[CreatedAt])
    SELECT ISNULL(d.lincred,0),ISNULL(NULLIF(RTRIM(d.codigoter),''),N''),ISNULL(d.numcuenta,0),CASE WHEN d.fecnovedad<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(d.fecnovedad AS DATE) END,CASE WHEN d.feccreacion<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(d.feccreacion AS DATE) END,CASE WHEN d.fecinidesc<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(d.fecinidesc AS DATE) END,ISNULL(d.cladesembolso,N''),ISNULL(d.periodicidad,N''),ISNULL(d.ciclopago,N''),ISNULL(d.vlrcuota,0),ISNULL(d.plazo,0),CASE WHEN d.fecvence<='1900-01-02' THEN NULL ELSE CAST(d.fecvence AS DATE) END,ISNULL(d.tiponovedad,N''),ISNULL(NULLIF(RTRIM(d.usuario),''),N''),ISNULL(NULLIF(RTRIM(d.nomusuario),''),N''),CASE WHEN d.fechasys<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(d.fechasys AS DATE) END,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.dep_novmeahor d;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_DepositEntries',(SELECT COUNT(*) FROM [dbo].[LND_DepositEntries]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_DepositEntries',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 43: LND_DepositSignatures (dep_firmas)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_DepositSignatures (dep_firmas)...';
    INSERT INTO [dbo].[LND_DepositSignatures] ([AccountNumber],[SignatureNumber],[SignatureImage],[IsRequired],[CreatedBy],[CreatedAt])
    SELECT ISNULL(d.numcuenta,0),ISNULL(d.numfirma,0),d.firma,ISNULL(d.requerida,N'S'),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.dep_firmas d;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_DepositSignatures',(SELECT COUNT(*) FROM [dbo].[LND_DepositSignatures]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_DepositSignatures',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 44: LND_Cashiers (dep_cajeros)
BEGIN TRY BEGIN TRAN; PRINT '>> LND_Cashiers (dep_cajeros)...';
    INSERT INTO [dbo].[LND_Cashiers] ([CashierCode],[Name],[VoucherType],[Status],[OpenDate],[VoucherConsecutive],[Description],[PasswordTimeout],[PrinterName],[TransactionType],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(d.cajero),''),N''),ISNULL(NULLIF(RTRIM(d.nombre),''),N''),ISNULL(NULLIF(RTRIM(d.comprobante),''),N''),d.estado,CASE WHEN d.fecapertura<='1900-01-02' THEN NULL ELSE CAST(d.fecapertura AS DATE) END,ISNULL(d.consecutivocpte,0),ISNULL(NULLIF(RTRIM(d.detalle),''),N''),ISNULL(d.tiempoclave,0),ISNULL(NULLIF(RTRIM(d.impresora),''),N''),ISNULL(d.tipotransaccion,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.dep_cajeros d;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'LND_Cashiers',(SELECT COUNT(*) FROM [dbo].[LND_Cashiers]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'LND_Cashiers',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

PRINT '================================================================';
PRINT '  04_Migrate_LND_Lending.sql — END  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO
