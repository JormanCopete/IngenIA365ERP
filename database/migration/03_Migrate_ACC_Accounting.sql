-- ============================================================
-- IngenIA365ERP — Migration Step 03: ACC_ (Accounting) Tables
-- Source: SOLIDO ERP (DBDefinicion.sql)
-- Target: IngenIA365ERP Schema v1.0
-- Prerequisites: 01_Create_MappingTables.sql, 02_Migrate_COR_Core.sql
-- ============================================================
SET NOCOUNT ON;
PRINT N'============================================================';
PRINT N'  Step 03: Migrating ACC_ (Accounting) Tables';
PRINT N'  Started: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
GO

-- ============================================================
-- 3.1 ACC_VoucherTypes (from: sys_compro02)
-- ============================================================
BEGIN TRY
    DECLARE @logId_vt INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_VoucherTypes', 'sys_compro02');
    SET @logId_vt = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_VoucherTypes] (
        [LegacyCode], [Code], [Name], [ShortName], [DocumentType],
        [AccountingAccountCode], [UpdatesAccounting], [NextSequenceNumber],
        [EquivalentAccountCode], [RequiresDetail], [PrintFormat],
        [CostCenterCode], [ControlSequential], [BankReconciliationCode],
        [DebitCredit], [HasValidator], [ValidatorPort], [AutomaticDetail],
        [TreasuryRestriction], [Affects3xMil], [DocumentControlType],
        [MoneyLaundering],
        [EquivalentVoucherCode], [EquivalentDocumentCode], [AccountCode2],
        [Nature], [DianReportFlag], [SequentialFormat], [ReceiptInvoice],
        [InvoiceControlCode], [ReturnOverdue], [ConversionRate],
        [ModuleCode],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(c.CODIGO),
        RTRIM(c.CODIGO),
        RTRIM(c.NOMBRE),
        NULLIF(RTRIM(c.NOMRES), ''),
        NULLIF(RTRIM(c.DOCUMENTO), ''),
        NULLIF(RTRIM(c.CUENTA_CONTABLE), ''),
        CASE WHEN c.ACTUALIZA_CONTA = 'Y' THEN 1 ELSE 0 END,
        c.NUM_CONSECU,
        NULLIF(RTRIM(c.CUENTA_EQUIVA), ''),
        CASE WHEN c.EXIGE_DETALLE = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.FORMA_IMPRIMIR), ''),
        NULLIF(RTRIM(c.CENCOSTO), ''),
        CASE WHEN c.CONTROL_CONSEC = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.DOCONCI_CUENBACA), ''),
        NULLIF(RTRIM(c.DEB_CRE), ''),
        CASE WHEN c.VALIDADORA = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.PUERT_VALIDADORA), ''),
        NULLIF(RTRIM(c.DETALLE_AUTOMATICO), ''),
        CASE WHEN c.RESTRI_TESORERIA = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.AFECTA_3XMIL = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.TIPO_CONTROL_DOMTO), ''),
        CASE WHEN c.LAVA_ACTIVOS = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.EQUICOM), ''),
        NULLIF(RTRIM(c.EQUIDOCU), ''),
        NULLIF(RTRIM(c.CUENTA), ''),
        NULLIF(RTRIM(c.NATURA), ''),
        c.INFDIAN,
        c.FORCONSE,
        NULLIF(RTRIM(c.RecFactura), ''),
        NULLIF(RTRIM(c.CodfacturaCont), ''),
        NULLIF(RTRIM(c.devolvermora), ''),
        c.convrs,
        NULLIF(RTRIM(c.modulo), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_compro02 c;

    PRINT N'Migrating ACC_VoucherTypes... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Populate mapping
    INSERT INTO [migration].[Map_VoucherTypes] (OldCodigo, NewVoucherTypeId)
    SELECT LegacyCode, Id FROM [dbo].[ACC_VoucherTypes] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_VoucherTypes]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_vt;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_vt;
    PRINT N'ERROR migrating ACC_VoucherTypes: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.2 ACC_ChartOfAccounts (from: cnt_maecuen — metadata only)
-- Monthly balance columns (DEB_ENE..CRE_DIC) excluded; they go to ACC_AccountBalances
-- ============================================================
BEGIN TRY
    DECLARE @logId_coa INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_ChartOfAccounts', 'cnt_maecuen');
    SET @logId_coa = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_ChartOfAccounts] (
        [LegacyCode], [AccountCode], [Nature], [Name], [Level], [Rate],
        [RequiresDocument], [ManagesCostCenter], [RequiresThirdParty],
        [IsOperationalAsset], [AppliesToLending], [AppliesToSavingsCDT],
        [AppliesToInventory], [AppliesToTreasury], [AppliesToPayroll],
        [AppliesToAccounting], [AppliesToInvoicing],
        [CostCenterCode], [FixedAssetGroup], [FixedAssetClass],
        [Status], [CashFlowCode], [BankReconciliationCode],
        [WithholdingType], [AccountBelongsTo],
        [ReportFormatId], [ConceptId], [SourceId], [AccountGroup],
        -- Withholding line references
        [WithholdingLineCode], [IcaLineCode], [VatLineCode],
        [SalesWithholdingLineCode], [IncomeTaxDeclaration],
        [GmfLineCode], [IcaBaseLineCode], [GmfBaseLineCode],
        -- Financial statement grouping
        [FinStmtCashFlowNumber], [FinStmtCashFlowSubgroup],
        [FinStmtChangeNumber], [FinStmtChangeSubgroup],
        [FinStmtFinPosNumber], [FinStmtFinPosSubgroup],
        [FinStmtBalSheetNumber], [FinStmtBalSheetSubgroup],
        [FinStmtEquityNumber], [FinStmtEquitySubgroup],
        [FinStmtIncomeNumber],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(c.CUENTA),
        RTRIM(c.CUENTA),
        RTRIM(c.NATURA),
        RTRIM(c.NOMBRE),
        CAST(c.NIVEL AS TINYINT),
        c.TASA,
        CASE WHEN c.AUX_DOMTO = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.MANE_CENCOS = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.TERCERO = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.ACTOPERA = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_CARTCOOPE = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_AHOR_CDT = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_INVENTA = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_TESORERI = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_NOMINA = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_CONTAB = 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.APLI_FACTURAC = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.CENCOS), ''),
        NULLIF(RTRIM(c.GRU_ACT_FIJ), ''),
        NULLIF(RTRIM(c.CLAS_ACT_FIJ), ''),
        c.estado,
        NULLIF(RTRIM(c.FlujodeCaja), ''),
        NULLIF(RTRIM(c.banco_concibanca), ''),
        NULLIF(RTRIM(c.tipoRetencion), ''),
        NULLIF(RTRIM(c.cuentapertenece), ''),
        CASE WHEN c.idformato = 0 THEN NULL ELSE c.idformato END,
        CASE WHEN c.idcpto = 0 THEN NULL ELSE c.idcpto END,
        CASE WHEN c.fuente = 0 THEN NULL ELSE c.fuente END,
        NULLIF(RTRIM(c.grucuenta), ''),
        -- Withholding
        NULLIF(RTRIM(c.DRETEFUENTE), ''),
        NULLIF(RTRIM(c.DRETEICA), ''),
        NULLIF(RTRIM(c.DRETEIVA), ''),
        NULLIF(RTRIM(c.DRETEVENTA), ''),
        CASE WHEN c.DECLA_RENTA = 'Y' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(c.DEGRAMOV), ''),
        NULLIF(RTRIM(c.DRETEICABASE), ''),
        NULLIF(RTRIM(c.degramovbase), ''),
        -- Financial statement grouping
        CASE WHEN c.numeroecfe = 0 THEN NULL ELSE c.numeroecfe END,
        CASE WHEN c.subgrupoecfe = 0 THEN NULL ELSE c.subgrupoecfe END,
        CASE WHEN c.numeroecct = 0 THEN NULL ELSE c.numeroecct END,
        CASE WHEN c.subgrupoecct = 0 THEN NULL ELSE c.subgrupoecct END,
        CASE WHEN c.numeroecsf = 0 THEN NULL ELSE c.numeroecsf END,
        CASE WHEN c.subgrupoecsf = 0 THEN NULL ELSE c.subgrupoecsf END,
        CASE WHEN c.numeroblgr = 0 THEN NULL ELSE c.numeroblgr END,
        CASE WHEN c.subgrupoblgr = 0 THEN NULL ELSE c.subgrupoblgr END,
        CASE WHEN c.numeroecpa = 0 THEN NULL ELSE c.numeroecpa END,
        CASE WHEN c.subgrupoecpa = 0 THEN NULL ELSE c.subgrupoecpa END,
        CASE WHEN c.numeroesre = 0 THEN NULL ELSE c.numeroesre END,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_maecuen c;

    PRINT N'Migrating ACC_ChartOfAccounts... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Populate mapping
    INSERT INTO [migration].[Map_ChartOfAccounts] (OldCuenta, NewAccountId)
    SELECT LegacyCode, Id FROM [dbo].[ACC_ChartOfAccounts] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_ChartOfAccounts]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_coa;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_coa;
    PRINT N'ERROR migrating ACC_ChartOfAccounts: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.3 ACC_AccountBalances (from: cnt_salage — NORMALIZED via CROSS APPLY)
-- Transform 24 monthly columns into rows (up to 13 per source row)
-- ============================================================
BEGIN TRY
    DECLARE @logId_bal INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_AccountBalances', 'cnt_salage');
    SET @logId_bal = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_AccountBalances] (
        [AccountId], [PeriodYear], [PeriodMonth], [BranchId], [CostCenterId],
        [DebitAmount], [CreditAmount], [CreatedBy], [CreatedAt]
    )
    SELECT
        ma.NewAccountId,
        s.periodo,
        v.PeriodMonth,
        br.NewBranchId,
        cc.NewCostCenterId,
        v.DebitAmount,
        v.CreditAmount,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_salage s
    INNER JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(s.cuenta) = ma.OldCuenta
    INNER JOIN [migration].[Map_Branches] br ON RTRIM(s.agencia) = br.OldCodigo
    INNER JOIN [migration].[Map_CostCenters] cc ON RTRIM(s.cencosto) = cc.OldCCosto
    CROSS APPLY (VALUES
        (1,  s.ene_deb, s.ene_cre),
        (2,  s.feb_deb, s.feb_cre),
        (3,  s.mar_deb, s.mar_cre),
        (4,  s.abr_deb, s.abr_cre),
        (5,  s.may_deb, s.may_cre),
        (6,  s.jun_deb, s.jun_cre),
        (7,  s.jul_deb, s.jul_cre),
        (8,  s.ago_deb, s.ago_cre),
        (9,  s.sep_deb, s.sep_cre),
        (10, s.oct_deb, s.oct_cre),
        (11, s.nov_deb, s.nov_cre),
        (12, s.dic_deb, s.dic_cre),
        (13, s.tre_deb, s.tre_cre)
    ) AS v(PeriodMonth, DebitAmount, CreditAmount)
    WHERE v.DebitAmount <> 0 OR v.CreditAmount <> 0; -- Skip zero-value months

    PRINT N'Migrating ACC_AccountBalances... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_bal;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_bal;
    PRINT N'ERROR migrating ACC_AccountBalances: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.4 ACC_Documents (from: cnt_docmto)
-- ============================================================
BEGIN TRY
    DECLARE @logId_doc INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_Documents', 'cnt_docmto');
    SET @logId_doc = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_Documents] (
        [LegacyCompronte], [LegacyNumero],
        [VoucherTypeCode], [DocumentNumber], [Detail],
        [TotalDebit], [TotalCredit], [DocumentDate],
        [IsClosed], [IsVoided], [BeneficiaryId], [PeriodCode],
        [CheckNumber], [BankId], [ModuleCode], [PaymentMethod],
        [InvoiceNumber],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(d.COMPRONTE),
        d.NUMERO,
        RTRIM(d.COMPRONTE),
        d.NUMERO,
        NULLIF(RTRIM(d.DETALLE), ''),
        d.DEBITO,
        d.CREDITO,
        CAST(d.FECHA AS DATE),
        CASE WHEN d.CERRADO = 'S' THEN 1 ELSE 0 END,
        CASE WHEN d.ANULADO = 'S' THEN 1 ELSE 0 END,
        mpb.NewPersonId,
        d.PERIODO,
        NULLIF(RTRIM(d.CHEQUE), ''),
        d.BANCO,
        NULLIF(RTRIM(d.Modulo), ''),
        NULLIF(RTRIM(d.forpag), ''),
        CASE WHEN d.facturaCont = 0 THEN NULL ELSE d.facturaCont END,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_docmto d
    LEFT JOIN [migration].[Map_People] mpb ON RTRIM(d.IdBenef) = mpb.OldCodigoTer;

    PRINT N'Migrating ACC_Documents... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_doc;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_doc;
    PRINT N'ERROR migrating ACC_Documents: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.5 ACC_JournalEntries (from: cnt_movimto)
-- ============================================================
BEGIN TRY
    DECLARE @logId_je INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_JournalEntries', 'cnt_movimto');
    SET @logId_je = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_JournalEntries] (
        [LegacySequence], [VoucherTypeCode], [DocumentNumber],
        [AccountId], [PersonId], [BranchId], [CostCenterId],
        [PeriodCode], [TransactionDate], [Description], [AuxiliaryDocument],
        [DebitAmount], [CreditAmount], [BaseAmount], [Status],
        [InvoiceNumber], [AuxiliaryRequestId], [UserName], [DocumentType],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        m.SECUENCIA,
        RTRIM(m.compronte),
        m.numero,
        ma.NewAccountId,
        mp.NewPersonId,
        br.NewBranchId,
        cc.NewCostCenterId,
        CAST(m.periodo AS NVARCHAR(10)),
        CAST(m.fecha AS DATE),
        NULLIF(RTRIM(m.detalle), ''),
        NULLIF(RTRIM(m.domto_auxiliar), ''),
        m.vlr_debito,
        m.vlr_credito,
        m.vlr_base,
        CASE WHEN m.estado = '' THEN 0 ELSE CAST(CASE WHEN ISNUMERIC(m.estado) = 1 THEN m.estado ELSE '0' END AS INT) END,
        NULLIF(RTRIM(m.factura), ''),
        CASE WHEN m.Idsolaux = 0 THEN NULL ELSE m.Idsolaux END,
        NULLIF(RTRIM(m.NomUsu), ''),
        NULLIF(RTRIM(m.docu_tipo), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_movimto m
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(m.cuenta) = ma.OldCuenta
    LEFT JOIN [migration].[Map_People] mp ON RTRIM(m.nit) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_Branches] br ON RTRIM(m.agencia) = br.OldCodigo
    LEFT JOIN [migration].[Map_CostCenters] cc ON RTRIM(m.cencosto) = cc.OldCCosto;

    PRINT N'Migrating ACC_JournalEntries... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_je;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_je;
    PRINT N'ERROR migrating ACC_JournalEntries: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.6 ACC_AuxiliaryDocuments (from: cnt_docaux)
-- ============================================================
BEGIN TRY
    DECLARE @logId_aux INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_AuxiliaryDocuments', 'cnt_docaux');
    SET @logId_aux = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_AuxiliaryDocuments] (
        [LegacyKey], [PeriodYear], [AuxiliaryType], [AccountId], [PersonId],
        [BranchId], [CostCenterId], [DocumentType], [DocumentNumber],
        [Detail], [DocumentDate], [DueDate], [OriginalValue], [InitialBalance],
        [InvoiceNumber],
        [JanDebit], [JanCredit], [FebDebit], [FebCredit],
        [MarDebit], [MarCredit], [AprDebit], [AprCredit],
        [MayDebit], [MayCredit], [JunDebit], [JunCredit],
        [JulDebit], [JulCredit], [AugDebit], [AugCredit],
        [SepDebit], [SepCredit], [OctDebit], [OctCredit],
        [NovDebit], [NovCredit], [DecDebit], [DecCredit],
        [Period13Amount],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(d.periodo) + '|' + RTRIM(d.DOCU_TIPO) + '|' + RTRIM(d.CUENTA) + '|' + RTRIM(d.NIT),
        d.periodo,
        NULLIF(RTRIM(d.DOCU_TIPO), ''),
        ma.NewAccountId,
        mp.NewPersonId,
        br.NewBranchId,
        cc.NewCostCenterId,
        NULLIF(RTRIM(d.DOCTIPO), ''),
        NULLIF(RTRIM(d.NUMERO_DOMTO), ''),
        NULLIF(RTRIM(d.DETALLE), ''),
        CASE WHEN d.FECHA <= '1900-01-02' THEN NULL ELSE CAST(d.FECHA AS DATE) END,
        CASE WHEN d.FECHA_VEMTO <= '1900-01-02' THEN NULL ELSE CAST(d.FECHA_VEMTO AS DATE) END,
        d.VLR_ORIGINAL,
        d.SALDO_INICIAL,
        NULLIF(RTRIM(d.FACTURA), ''),
        d.DEB_ENE, d.CRE_ENE, d.DEB_FEB, d.CRE_FEB,
        d.DEB_MAR, d.CRE_MAR, d.DEB_ABR, d.CRE_ABR,
        d.DEB_MAY, d.CRE_MAY, d.DEB_JUN, d.CRE_JUN,
        d.DEB_JUL, d.CRE_JUL, d.DEB_AGO, d.CRE_AGO,
        d.DEB_SEP, d.CRE_SEP, d.DEB_OCT, d.CRE_OCT,
        d.DEB_NOV, d.CRE_NOV, d.DEB_DIC, d.CRE_DIC,
        d.TRECE,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_docaux d
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(d.CUENTA) = ma.OldCuenta
    LEFT JOIN [migration].[Map_People] mp ON RTRIM(d.NIT) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_Branches] br ON RTRIM(d.AGENCIA) = br.OldCodigo
    LEFT JOIN [migration].[Map_CostCenters] cc ON RTRIM(d.CENCOS) = cc.OldCCosto;

    PRINT N'Migrating ACC_AuxiliaryDocuments... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_aux;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_aux;
    PRINT N'ERROR migrating ACC_AuxiliaryDocuments: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.7 ACC_ThirdPartyAccounts (from: cnt_tercero)
-- ============================================================
BEGIN TRY
    DECLARE @logId_tp INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_ThirdPartyAccounts', 'cnt_tercero');
    SET @logId_tp = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_ThirdPartyAccounts] (
        [PeriodYear], [AccountId], [PersonId], [BranchId], [CostCenterId],
        [InitialBalance],
        [JanDebit], [JanCredit], [FebDebit], [FebCredit],
        [MarDebit], [MarCredit], [AprDebit], [AprCredit],
        [MayDebit], [MayCredit], [JunDebit], [JunCredit],
        [JulDebit], [JulCredit], [AugDebit], [AugCredit],
        [SepDebit], [SepCredit], [OctDebit], [OctCredit],
        [NovDebit], [NovCredit], [DecDebit], [DecCredit],
        [Period13Debit], [Period13Credit],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        t.periodo,
        ma.NewAccountId,
        mp.NewPersonId,
        br.NewBranchId,
        cc.NewCostCenterId,
        t.saldo_inicial,
        t.ene_deb, t.ene_cre, t.feb_deb, t.feb_cre,
        t.mar_deb, t.mar_cre, t.abr_deb, t.abr_cre,
        t.may_deb, t.may_cre, t.jun_deb, t.jun_cre,
        t.jul_deb, t.jul_cre, t.ago_deb, t.ago_cre,
        t.sep_deb, t.sep_cre, t.oct_deb, t.oct_cre,
        t.nov_deb, t.nov_cre, t.dic_deb, t.dic_cre,
        t.tre_deb, t.tre_cre,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_tercero t
    INNER JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(t.cuenta) = ma.OldCuenta
    INNER JOIN [migration].[Map_People] mp ON RTRIM(t.nit) = mp.OldCodigoTer
    INNER JOIN [migration].[Map_Branches] br ON RTRIM(t.agencia) = br.OldCodigo
    INNER JOIN [migration].[Map_CostCenters] cc ON RTRIM(t.cencosto) = cc.OldCCosto;

    PRINT N'Migrating ACC_ThirdPartyAccounts... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_tp;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_tp;
    PRINT N'ERROR migrating ACC_ThirdPartyAccounts: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.8 ACC_Amortizations (from: cnt_amortiza)
-- ============================================================
BEGIN TRY
    DECLARE @logId_amort INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_Amortizations', 'cnt_amortiza');
    SET @logId_amort = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_Amortizations] (
        [PeriodYear], [AccountId], [BranchId], [CostCenterId], [PersonId],
        [DocumentCode], [CrossAccountId], [CrossCostCenterCode], [CrossPersonTaxId],
        [CrossInitialBalance], [MovementAccountId], [MovementCostCenterCode],
        [MovementPersonTaxId], [MovementInitialBalance],
        [AmortizationDate], [OriginalAmount], [MonthlyAmount], [TermMonths],
        [RemainingBalance], [StartDate], [EndDate], [LastPeriod], [Rate], [Status],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        a.periodo,
        ma.NewAccountId,
        br.NewBranchId,
        cc.NewCostCenterId,
        mp.NewPersonId,
        NULLIF(RTRIM(a.documento), ''),
        ma2.NewAccountId,
        NULLIF(RTRIM(a.cencostoCr), ''),
        NULLIF(RTRIM(a.nitCr), ''),
        a.sal_inicialCr,
        ma3.NewAccountId,
        NULLIF(RTRIM(a.cencostoMov), ''),
        NULLIF(RTRIM(a.nitMov), ''),
        a.sal_inicialMov,
        CASE WHEN a.fecha_amortiza <= '1900-01-02' THEN NULL ELSE CAST(a.fecha_amortiza AS DATE) END,
        a.vlr_original,
        a.vlr_mensual,
        a.plazo,
        a.saldo,
        CASE WHEN a.fecha_inicio <= '1900-01-02' THEN NULL ELSE CAST(a.fecha_inicio AS DATE) END,
        CASE WHEN a.fecha_fin <= '1900-01-02' THEN NULL ELSE CAST(a.fecha_fin AS DATE) END,
        a.ultimo_periodo,
        a.tasa,
        a.estado,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_amortiza a
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(a.cuenta) = ma.OldCuenta
    LEFT JOIN [migration].[Map_Branches] br ON RTRIM(a.agencia) = br.OldCodigo
    LEFT JOIN [migration].[Map_CostCenters] cc ON RTRIM(a.cencosto) = cc.OldCCosto
    LEFT JOIN [migration].[Map_People] mp ON RTRIM(a.nit) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma2 ON RTRIM(a.cuentaCr) = ma2.OldCuenta
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma3 ON RTRIM(a.cuentaMov) = ma3.OldCuenta;

    PRINT N'Migrating ACC_Amortizations... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_amort;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_amort;
    PRINT N'ERROR migrating ACC_Amortizations: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.9 ACC_Depreciations (from: cnt_deprecia)
-- ============================================================
BEGIN TRY
    DECLARE @logId_dep INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_Depreciations', 'cnt_deprecia');
    SET @logId_dep = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_Depreciations] (
        [PeriodYear], [AccountId], [BranchId], [CostCenterId], [PersonId],
        [CrossAccountId], [CrossCostCenterCode], [CrossPersonTaxId], [CrossInitialBalance],
        [MovementAccountId], [MovementCostCenterCode], [MovementPersonTaxId], [MovementInitialBalance],
        [OriginalValue], [DepreciationRate], [MonthlyDepreciation],
        [AccumulatedDepreciation], [NetValue], [UsefulLifeMonths],
        [StartDate], [EndDate], [LastPeriod],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        d.periodo,
        ma.NewAccountId,
        br.NewBranchId,
        cc.NewCostCenterId,
        mp.NewPersonId,
        ma2.NewAccountId,
        NULLIF(RTRIM(d.cencostoCr), ''),
        NULLIF(RTRIM(d.nitCr), ''),
        d.sal_inicialCr,
        ma3.NewAccountId,
        NULLIF(RTRIM(d.cencostoMov), ''),
        NULLIF(RTRIM(d.nitMov), ''),
        d.sal_inicialMov,
        d.vlr_original,
        d.tasa_deprecia,
        d.vlr_mensual,
        d.vlr_acumulado,
        d.vlr_neto,
        d.vida_util,
        CASE WHEN d.fecha_inicio <= '1900-01-02' THEN NULL ELSE CAST(d.fecha_inicio AS DATE) END,
        CASE WHEN d.fecha_fin <= '1900-01-02' THEN NULL ELSE CAST(d.fecha_fin AS DATE) END,
        d.ultimo_periodo,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_deprecia d
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(d.cuenta) = ma.OldCuenta
    LEFT JOIN [migration].[Map_Branches] br ON RTRIM(d.agencia) = br.OldCodigo
    LEFT JOIN [migration].[Map_CostCenters] cc ON RTRIM(d.cencosto) = cc.OldCCosto
    LEFT JOIN [migration].[Map_People] mp ON RTRIM(d.nit) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma2 ON RTRIM(d.cuentaCr) = ma2.OldCuenta
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma3 ON RTRIM(d.cuentaMov) = ma3.OldCuenta;

    PRINT N'Migrating ACC_Depreciations... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_dep;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_dep;
    PRINT N'ERROR migrating ACC_Depreciations: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.10 ACC_BankReconciliations (from: cnt_concibanca)
-- ============================================================
BEGIN TRY
    DECLARE @logId_br INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_BankReconciliations', 'cnt_concibanca');
    SET @logId_br = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_BankReconciliations] (
        [AccountId], [BankId], [PeriodCode], [TransactionDate],
        [DocumentType], [DocumentNumber], [Description],
        [DebitAmount], [CreditAmount], [IsReconciled], [ReconciliationDate],
        [Status], [IsAdditional], [IsClosed], [MovementSequence], [ModuleCode],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ma.NewAccountId,
        mb.NewBankId,
        cb.periodo,
        CAST(cb.fecha AS DATE),
        NULLIF(RTRIM(cb.tipo_docu), ''),
        NULLIF(RTRIM(cb.numero_docu), ''),
        NULLIF(RTRIM(cb.detalle), ''),
        cb.debito,
        cb.credito,
        CASE WHEN cb.conciliado = 'S' THEN 1 ELSE 0 END,
        CASE WHEN cb.fecconcilia <= '1900-01-02' THEN NULL ELSE CAST(cb.fecconcilia AS DATE) END,
        cb.estado,
        CASE WHEN cb.adicional = 'S' THEN 1 ELSE 0 END,
        CASE WHEN cb.cerrado = 'S' THEN 1 ELSE 0 END,
        cb.secuencia_mov,
        NULLIF(RTRIM(cb.modulo), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_concibanca cb
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(cb.cuenta) = ma.OldCuenta
    LEFT JOIN [migration].[Map_Banks] mb ON RTRIM(cb.banco) = mb.OldCodigo;

    PRINT N'Migrating ACC_BankReconciliations... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_br;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_br;
    PRINT N'ERROR migrating ACC_BankReconciliations: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.11 ACC_GmfTaxLines (from: cnt_lineagmf)
-- ============================================================
BEGIN TRY
    DECLARE @logId_gmf INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_GmfTaxLines', 'cnt_lineagmf');
    SET @logId_gmf = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_GmfTaxLines] (
        [LegacyCode], [LineCode], [Description], [AccountCode],
        [TaxRate], [BaseAccountCode], [Sign],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(g.LINEA),
        RTRIM(g.LINEA),
        NULLIF(RTRIM(g.NOMBRE), ''),
        NULL, -- cnt_lineagmf has no cuenta column; set via AFECTA
        0,    -- no rate column in legacy
        NULLIF(RTRIM(g.AFECTA), ''),
        NULLIF(RTRIM(g.signo), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_lineagmf g;

    PRINT N'Migrating ACC_GmfTaxLines... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_gmf;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_gmf;
    PRINT N'ERROR migrating ACC_GmfTaxLines: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.12 ACC_IcaTaxLines (from: cnt_lineaica)
-- ============================================================
BEGIN TRY
    DECLARE @logId_ica INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_IcaTaxLines', 'cnt_lineaica');
    SET @logId_ica = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_IcaTaxLines] (
        [LegacyCode], [LineCode], [Description], [AccountCode],
        [TaxRate], [BaseAccountCode], [Sign],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(LINEA), RTRIM(LINEA), NULLIF(RTRIM(NOMBRE), ''),
        NULLIF(RTRIM(CUENTA), ''), TASA, NULLIF(RTRIM(AFECTA), ''),
        NULLIF(RTRIM(signo), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_lineaica;

    PRINT N'Migrating ACC_IcaTaxLines... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_ica;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_ica;
    PRINT N'ERROR migrating ACC_IcaTaxLines: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.13 ACC_VatTaxLines (from: cnt_lineaiva)
-- ============================================================
BEGIN TRY
    DECLARE @logId_vat INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_VatTaxLines', 'cnt_lineaiva');
    SET @logId_vat = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_VatTaxLines] (
        [LegacyCode], [LineCode], [Description], [AccountCode],
        [TaxRate], [BaseAccountCode], [Sign],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(LINEA), RTRIM(LINEA), NULLIF(RTRIM(NOMBRE), ''),
        NULLIF(RTRIM(CUENTA), ''), TASA, NULLIF(RTRIM(AFECTA), ''),
        NULLIF(RTRIM(signo), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_lineaiva;

    PRINT N'Migrating ACC_VatTaxLines... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_vat;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_vat;
    PRINT N'ERROR migrating ACC_VatTaxLines: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.14 ACC_WithholdingTaxLines (from: cnt_linretefuente)
-- ============================================================
BEGIN TRY
    DECLARE @logId_wth INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_WithholdingTaxLines', 'cnt_linretefuente');
    SET @logId_wth = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_WithholdingTaxLines] (
        [LegacyCode], [LineCode], [Description], [AccountCode],
        [TaxRate], [BaseAccountCode], [Sign],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(LINEA), RTRIM(LINEA), NULLIF(RTRIM(NOMBRE), ''),
        NULLIF(RTRIM(CUENTA), ''), TASA, NULLIF(RTRIM(AFECTA), ''),
        NULLIF(RTRIM(signo), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_linretefuente;

    PRINT N'Migrating ACC_WithholdingTaxLines... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_wth;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_wth;
    PRINT N'ERROR migrating ACC_WithholdingTaxLines: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.15 ACC_IncomeTaxLines (from: cnt_linearenta)
-- ============================================================
BEGIN TRY
    DECLARE @logId_inc INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_IncomeTaxLines', 'cnt_linearenta');
    SET @logId_inc = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_IncomeTaxLines] (
        [LegacyCode], [LineCode], [Description], [AccountCode],
        [TaxRate], [BaseAccountCode], [Sign],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(LINEA), RTRIM(LINEA), NULLIF(RTRIM(NOMBRE), ''),
        NULLIF(RTRIM(CUENTA), ''), TASA, NULLIF(RTRIM(AFECTA), ''),
        NULLIF(RTRIM(signo), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_linearenta;

    PRINT N'Migrating ACC_IncomeTaxLines... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_inc;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_inc;
    PRINT N'ERROR migrating ACC_IncomeTaxLines: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.16 ACC_StampTaxes (from: cnt_estampilla)
-- ============================================================
BEGIN TRY
    DECLARE @logId_stamp INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_StampTaxes', 'cnt_estampilla');
    SET @logId_stamp = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_StampTaxes] (
        [LegacyCode], [Grade], [DebitAccountCode], [CreditAccountCode],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(grado), RTRIM(grado),
        NULLIF(RTRIM(cuentai), ''),
        NULLIF(RTRIM(cuentar), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_estampilla;

    PRINT N'Migrating ACC_StampTaxes... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_stamp;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_stamp;
    PRINT N'ERROR migrating ACC_StampTaxes: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.17 ACC_AccountGroups (from: cnt_grupocuenta)
-- ============================================================
BEGIN TRY
    DECLARE @logId_grp INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_AccountGroups', 'cnt_grupocuenta');
    SET @logId_grp = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_AccountGroups] (
        [GroupNumber], [GroupType], [AccountCode], [Description],
        [ReportOrder], [Level], [FinancialStatementCode], [SpecialCode],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        g.numero,
        NULL, -- No type column in legacy
        NULLIF(RTRIM(g.cuenta), ''),
        NULLIF(RTRIM(g.nombre), ''),
        g.posicion,
        g.suma,
        NULLIF(RTRIM(g.estadosdecambio), ''),
        NULLIF(RTRIM(g.especial), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_grupocuenta g;

    PRINT N'Migrating ACC_AccountGroups... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_grp;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_grp;
    PRINT N'ERROR migrating ACC_AccountGroups: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.18 ACC_GroupNames (from: cnt_nombregrupo)
-- ============================================================
BEGIN TRY
    DECLARE @logId_gn INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_GroupNames', 'cnt_nombregrupo');
    SET @logId_gn = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_GroupNames] ([GroupNumber], [Name], [CreatedBy], [CreatedAt])
    SELECT numero, RTRIM(nombre), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_nombregrupo;

    PRINT N'Migrating ACC_GroupNames... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_gn;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_gn;
    PRINT N'ERROR migrating ACC_GroupNames: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.19 ACC_SubgroupNames (from: cnt_nombresubgrupo)
-- ============================================================
BEGIN TRY
    DECLARE @logId_sgn INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_SubgroupNames', 'cnt_nombresubgrupo');
    SET @logId_sgn = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_SubgroupNames] ([SubgroupNumber], [Name], [CreatedBy], [CreatedAt])
    SELECT numero, RTRIM(nombre), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_nombresubgrupo;

    PRINT N'Migrating ACC_SubgroupNames... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_sgn;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_sgn;
    PRINT N'ERROR migrating ACC_SubgroupNames: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.20 ACC_ExchangeRateHistory (from: cnt_estadosdecambio)
-- ============================================================
BEGIN TRY
    DECLARE @logId_exch INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_ExchangeRateHistory', 'cnt_estadosdecambio');
    SET @logId_exch = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_ExchangeRateHistory] (
        [GroupNumber], [SubgroupNumber], [PeriodCode], [Amount], [StatementCode],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        e.numero, e.subgrupo, e.periodo, e.valor,
        NULLIF(RTRIM(e.estadosdecambio), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_estadosdecambio e;

    PRINT N'Migrating ACC_ExchangeRateHistory... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_exch;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_exch;
    PRINT N'ERROR migrating ACC_ExchangeRateHistory: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.21 ACC_DianReportFormats (from: cnt_parfordian)
-- ============================================================
BEGIN TRY
    DECLARE @logId_dian INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_DianReportFormats', 'cnt_parfordian');
    SET @logId_dian = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_DianReportFormats] (
        [FormatId], [ConceptId], [FormatCode], [Description],
        [Threshold], [MinorTaxId], [BalanceThreshold], [DianTaxId],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        d.formato, d.concepto,
        NULLIF(RTRIM(d.codformato), ''),
        NULLIF(RTRIM(d.descripcion), ''),
        ISNULL(d.cuantia, 0),
        NULLIF(RTRIM(d.nit_menorcuantia), ''),
        ISNULL(d.cuantiasaldo, 0),
        NULLIF(RTRIM(d.nit_dian), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_parfordian d;

    PRINT N'Migrating ACC_DianReportFormats... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_dian;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_dian;
    PRINT N'ERROR migrating ACC_DianReportFormats: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.22 ACC_RiskCategories (from: cnt_riesgo)
-- ============================================================
BEGIN TRY
    DECLARE @logId_risk INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_RiskCategories', 'cnt_riesgo');
    SET @logId_risk = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_RiskCategories] (
        [AccountCode], [PeriodCode], [InitialBalance],
        [Day1], [Day2], [Day3], [Day4], [Day5], [Day6], [Day7],
        [Day8], [Day9], [Day10], [Day11], [Day12], [Day13], [Day14],
        [Day15], [Day16], [Day17], [Day18], [Day19], [Day20], [Day21],
        [Day22], [Day23], [Day24], [Day25], [Day26], [Day27], [Day28],
        [Day29], [Day30], [Day31], [AverageBalance],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(r.cuenta), RTRIM(CAST(r.periodo AS NVARCHAR(10))),
        r.saldo_ini,
        r.dia1, r.dia2, r.dia3, r.dia4, r.dia5, r.dia6, r.dia7,
        r.dia8, r.dia9, r.dia10, r.dia11, r.dia12, r.dia13, r.dia14,
        r.dia15, r.dia16, r.dia17, r.dia18, r.dia19, r.dia20, r.dia21,
        r.dia22, r.dia23, r.dia24, r.dia25, r.dia26, r.dia27, r.dia28,
        r.dia29, r.dia30, r.dia31, r.promedio,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_riesgo r;

    PRINT N'Migrating ACC_RiskCategories... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_risk;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_risk;
    PRINT N'ERROR migrating ACC_RiskCategories: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.23 ACC_AccountingPeriods (from: sys_periodo — NORMALIZED)
-- sys_periodo has columnar layout: per1ini, per1fin, per1est, per2ini...
-- We normalize into 13 rows per (modulo, year) combination
-- ============================================================
BEGIN TRY
    DECLARE @logId_per INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_AccountingPeriods', 'sys_periodo');
    SET @logId_per = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_AccountingPeriods] (
        [ModuleCode], [Year], [PeriodNumber], [StartDate], [EndDate], [Status],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(p.modulo),
        p.anio,
        v.PeriodNumber,
        v.StartDate,
        v.EndDate,
        v.Status,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_periodo p
    CROSS APPLY (VALUES
        (1,  p.per1ini,  p.per1fin,  p.per1est),
        (2,  p.per2ini,  p.per2fin,  p.per2est),
        (3,  p.per3ini,  p.per3fin,  p.per3est),
        (4,  p.per4ini,  p.per4fin,  p.per4est),
        (5,  p.per5ini,  p.per5fin,  p.per5est),
        (6,  p.per6ini,  p.per6fin,  p.per6est),
        (7,  p.per7ini,  p.per7fin,  p.per7est),
        (8,  p.per8ini,  p.per8fin,  p.per8est),
        (9,  p.per9ini,  p.per9fin,  p.per9est),
        (10, p.per10ini, p.per10fin, p.per10est),
        (11, p.per11ini, p.per11fin, p.per11est),
        (12, p.per12ini, p.per12fin, p.per12est),
        (13, p.per13ini, p.per13fin, p.per13est)
    ) AS v(PeriodNumber, StartDate, EndDate, Status)
    WHERE v.StartDate IS NOT NULL AND v.StartDate > '1900-01-02';

    PRINT N'Migrating ACC_AccountingPeriods... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_per;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_per;
    PRINT N'ERROR migrating ACC_AccountingPeriods: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.24 ACC_Budgets (from: cnt_presupto)
-- ============================================================
BEGIN TRY
    DECLARE @logId_bud INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_Budgets', 'cnt_presupto');
    SET @logId_bud = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_Budgets] (
        [PeriodYear], [AccountId], [BranchId], [CostCenterId],
        [JanBudget], [FebBudget], [MarBudget], [AprBudget],
        [MayBudget], [JunBudget], [JulBudget], [AugBudget],
        [SepBudget], [OctBudget], [NovBudget], [DecBudget],
        [TotalBudget],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        b.periodo,
        ma.NewAccountId,
        br.NewBranchId,
        cc.NewCostCenterId,
        b.enero, b.febrero, b.marzo, b.abril,
        b.mayo, b.junio, b.julio, b.agosto,
        b.septiembre, b.octubre, b.noviembre, b.diciembre,
        b.total,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_presupto b
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(b.cuenta) = ma.OldCuenta
    LEFT JOIN [migration].[Map_Branches] br ON RTRIM(b.agencia) = br.OldCodigo
    LEFT JOIN [migration].[Map_CostCenters] cc ON RTRIM(b.cencosto) = cc.OldCCosto;

    PRINT N'Migrating ACC_Budgets... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_bud;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_bud;
    PRINT N'ERROR migrating ACC_Budgets: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 3.25 ACC_FinancialReports (from: cnt_infmedian)
-- ============================================================
BEGIN TRY
    DECLARE @logId_fin INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (3, 'ACC_FinancialReports', 'cnt_infmedian');
    SET @logId_fin = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[ACC_FinancialReports] (
        [FormatId], [ConceptId], [PersonId], [AccountId], [Year],
        [DocumentTypeCode], [VerificationDigit],
        [LastName1], [LastName2], [FirstName], [CompanyName], [FirstName2], [SecondName],
        [Municipality], [Address], [SavingsAccount],
        [Value1], [Value2], [Value3], [Value4], [Value5],
        [Value6], [Value7], [Value8], [Value9], [Value10],
        [ProcessedBySolido],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        i.formato, i.concepto,
        mp.NewPersonId,
        ma.NewAccountId,
        i.anio,
        i.tipo_documento,
        NULLIF(RTRIM(i.digito_verificacion), ''),
        NULLIF(RTRIM(i.primer_apellido), ''),
        NULLIF(RTRIM(i.segundo_apellido), ''),
        NULLIF(RTRIM(i.primer_nombre), ''),
        NULLIF(RTRIM(i.razon_social), ''),
        NULLIF(RTRIM(i.primer_nombre2), ''),
        NULLIF(RTRIM(i.segundo_nombre), ''),
        i.municipio,
        NULLIF(RTRIM(i.direccion), ''),
        NULLIF(RTRIM(i.cuentaahorro), ''),
        i.valor1, i.valor2, i.valor3, i.valor4, i.valor5,
        i.valor6, i.valor7, i.valor8, i.valor9, i.valor10,
        NULLIF(RTRIM(i.procesadosolido), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_infmedian i
    LEFT JOIN [migration].[Map_People] mp ON RTRIM(i.nit) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_ChartOfAccounts] ma ON RTRIM(i.cuenta) = ma.OldCuenta;

    PRINT N'Migrating ACC_FinancialReports... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_fin;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_fin;
    PRINT N'ERROR migrating ACC_FinancialReports: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- Summary
-- ============================================================
PRINT N'';
PRINT N'============================================================';
PRINT N'  Step 03: ACC_ Migration COMPLETE';
PRINT N'  Finished: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
PRINT N'';
PRINT N'Migration summary:';

SELECT TableName, SourceTable, RowsMigrated, Status, ErrorMessage
FROM [migration].[MigrationLog]
WHERE StepNumber = 3
ORDER BY Id;
GO
