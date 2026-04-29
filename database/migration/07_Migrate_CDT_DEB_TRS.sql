-- ============================================================
-- IngenIA365ERP — Data Migration Script
-- Step 07: CDT_ (7) + DEB_ (7) + TRS_ (3) = 17 tables
-- Source: [old].dbo.cdt_*, deb_*, TES_*
-- Target: [dbo].CDT_*, DEB_*, TRS_*
-- ============================================================
-- Prerequisites:
--   01_Create_MigrationSchema.sql (migration schema + Map_* tables)
--   02_Migrate_COR.sql (Map_People, Map_Banks populated)
--   04_Migrate_LND.sql (Map_CreditLines populated)
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT OFF;
GO

PRINT '================================================================';
PRINT '  07_Migrate_CDT_DEB_TRS.sql — START  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO

-- ************************************************************
-- CDT_ — Certificates of Deposit (7 tables)
-- ************************************************************

-- ============================================================
-- CDT BLOCK 1: CDT_Parameters (cdt_parame58)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_Parameters (cdt_parame58)...';

    INSERT INTO [dbo].[CDT_Parameters] (
        [CreditLineId], [Description], [MinimumRate], [AnnualRate],
        [WithholdingRate], [MinWithholdingAmount],
        [InterestConceptId], [WithholdingConceptId], [MonthlyIncrement],
        [InterestRate], [Term], [MinAmount], [MaxAmount],
        [InterestPaymentType], [InterestType],
        [FormatId], [ConceptId], [SourceId], [TreasuryAccount],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.lincred,
        NULLIF(RTRIM(s.Descripcion), ''),
        s.TasaMin, s.PorAnual,
        s.PorRetfte, s.VlrMinRet,
        s.CptoInt, s.CptoRet, s.IncreMen,
        s.tasa, s.plazo, s.vlrminimo, s.vlrmaximo,
        ISNULL(s.ForPagInt, 0), ISNULL(s.tipointeres, N'S'),
        ISNULL(s.formato, 0), ISNULL(s.concepto, 0), ISNULL(s.fuente, 0),
        NULLIF(RTRIM(s.ctatesoreria), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cdt_parame58 s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'CDT_Parameters', (SELECT COUNT(*) FROM [dbo].[CDT_Parameters]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'CDT_Parameters', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- CDT BLOCK 2: CDT_Certificates (cdt_maecdats) — populate Map_CDTCertificates
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_Certificates (cdt_maecdats) — CRITICAL...';

    INSERT INTO [dbo].[CDT_Certificates] (
        [CertificateNumber], [PersonId], [CreditLineId],
        [IssueDate], [MaturityDate], [AccrualDate],
        [Amount], [InterestRate], [Term],
        [RenewalType], [Status], [PaymentMethod], [Periodicity], [BranchId],
        [LegalRepId], [LegalRepName], [BusinessAddress], [Phone], [Mobile],
        [SignatoryId1], [SignatoryId2], [SignatoryId3],
        [SignatoryName1], [SignatoryName2], [SignatoryName3],
        [BeneficiaryId1], [BeneficiaryId2], [BeneficiaryId3], [BeneficiaryId4], [BeneficiaryId5],
        [BeneficiaryName1], [BeneficiaryName2], [BeneficiaryName3], [BeneficiaryName4], [BeneficiaryName5],
        [BeneficiaryPct1], [BeneficiaryPct2], [BeneficiaryPct3], [BeneficiaryPct4], [BeneficiaryPct5],
        [CancelledByUserId], [CancellationDate],
        [IsCapitalized], [PreviousCertificateId],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        CAST(s.num_cdat AS NVARCHAR(20)),
        mp.NewPersonId,
        ISNULL(s.lincred, 0),
        CASE WHEN s.fec_crea <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fec_crea AS DATE) END,
        CASE WHEN s.Fecvence <= '1900-01-02' THEN NULL ELSE CAST(s.Fecvence AS DATE) END,
        CASE WHEN s.fec_causacion <= '1900-01-02' THEN NULL ELSE CAST(s.fec_causacion AS DATE) END,
        ISNULL(s.Valorob, 0), ISNULL(s.TasaInt, 0), ISNULL(s.Plazo, 0),
        NULLIF(RTRIM(s.tipocdat), ''),
        ISNULL(s.Estado, N'A'),
        NULLIF(RTRIM(s.consignainteres), ''), s.periodicidad,
        s.agencia,
        NULLIF(RTRIM(s.rep_legal), ''), NULLIF(RTRIM(s.nom_repres), ''),
        NULLIF(RTRIM(s.dir_comerci), ''), NULLIF(RTRIM(s.telefono), ''), NULLIF(RTRIM(s.celular), ''),
        NULLIF(RTRIM(s.cc_nit_firmareq1), ''), NULLIF(RTRIM(s.cc_nit_firmareq2), ''), NULLIF(RTRIM(s.cc_nit_firmareq3), ''),
        NULLIF(RTRIM(s.nom_firmareq1), ''), NULLIF(RTRIM(s.nom_firmareq2), ''), NULLIF(RTRIM(s.nom_firmareq3), ''),
        NULLIF(RTRIM(s.cc_nit_benefi1), ''), NULLIF(RTRIM(s.cc_nit_benefi2), ''), NULLIF(RTRIM(s.cc_nit_benefi3), ''),
        NULLIF(RTRIM(s.cc_nit_benefi4), ''), NULLIF(RTRIM(s.cc_nit_benefi5), ''),
        NULLIF(RTRIM(s.nom_benefi1), ''), NULLIF(RTRIM(s.nom_benefi2), ''), NULLIF(RTRIM(s.nom_benefi3), ''),
        NULLIF(RTRIM(s.nom_benefi4), ''), NULLIF(RTRIM(s.nom_benefi5), ''),
        ISNULL(s.porcbenef1, 0), ISNULL(s.porcbenef2, 0), ISNULL(s.porcbenef3, 0),
        ISNULL(s.porcbenef4, 0), ISNULL(s.porcbenef5, 0),
        NULLIF(RTRIM(s.usucancelacdat), ''),
        CASE WHEN s.feccancela <= '1900-01-02' THEN NULL ELSE s.feccancela END,
        ISNULL(s.Marca_Capitalizado, 0),
        s.num_cdat_Anterior,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cdt_maecdats s
    INNER JOIN [migration].[Map_People] mp ON s.codigoter = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

    -- Populate Map_CDTCertificates
    INSERT INTO [migration].[Map_CDTCertificates] (OldNumCdat, NewCDTCertificateId)
    SELECT CAST([CertificateNumber] AS INT), [Id]
    FROM [dbo].[CDT_Certificates];

    PRINT '   Map_CDTCertificates populated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'CDT_Certificates', (SELECT COUNT(*) FROM [dbo].[CDT_Certificates]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'CDT_Certificates', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- CDT BLOCK 3: CDT_CertificateEntries (cdt_novcdats)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_CertificateEntries (cdt_novcdats)...';

    INSERT INTO [dbo].[CDT_CertificateEntries] (
        [CertificateId], [PersonId], [CreditLineId],
        [EntryDate], [OpeningDate], [EntryType],
        [PreviousRate], [CurrentRate], [Amount], [Description],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mc.NewCDTCertificateId,
        mp.NewPersonId,
        ISNULL(s.lincred, 0),
        CASE WHEN s.FechaNovedad <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.FechaNovedad AS DATE) END,
        CASE WHEN s.FechaApertura <= '1900-01-02' THEN NULL ELSE CAST(s.FechaApertura AS DATE) END,
        ISNULL(NULLIF(RTRIM(s.TipoNovedad), ''), N''),
        ISNULL(s.TasaAnterior, 0), ISNULL(s.TasaActual, 0),
        ISNULL(s.ValorCdat, 0),
        NULLIF(RTRIM(s.detalle), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cdt_novcdats s
    INNER JOIN [migration].[Map_CDTCertificates] mc ON s.numcdat = mc.OldNumCdat
    INNER JOIN [migration].[Map_People] mp ON s.codigoter = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'CDT_CertificateEntries', (SELECT COUNT(*) FROM [dbo].[CDT_CertificateEntries]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'CDT_CertificateEntries', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- CDT BLOCK 4: CDT_RatesByTerm (cdt_tasasplazos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_RatesByTerm (cdt_tasasplazos)...';

    INSERT INTO [dbo].[CDT_RatesByTerm] ([CreditLineId],[AmountRangeStart],[AmountRangeEnd],[TermStart],[TermEnd],[InterestRate],[LastUpdated],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.lincred,0),ISNULL(s.vlrinicial,0),ISNULL(s.vlrfinal,0),ISNULL(s.plazoinicial,0),ISNULL(s.plazofinal,0),s.tasa,CASE WHEN s.fecactualizacion<='1900-01-02' THEN NULL ELSE s.fecactualizacion END,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cdt_tasasplazos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'CDT_RatesByTerm',(SELECT COUNT(*) FROM [dbo].[CDT_RatesByTerm]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'CDT_RatesByTerm',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- CDT BLOCK 5: CDT_AssociateReferences (cdt_asoreferencia)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_AssociateReferences (cdt_asoreferencia)...';

    INSERT INTO [dbo].[CDT_AssociateReferences] ([CertificateId],[PersonId],[CreditLineId],[RelationshipType],[ReferenceName],[ReferenceAddress],[CityId],[ReferencePhone],[ReferenceMobile],[CreatedBy],[CreatedAt])
    SELECT mc.NewCDTCertificateId,mp.NewPersonId,ISNULL(s.lincred,0),ISNULL(NULLIF(RTRIM(s.TipoReferencia),''),N''),ISNULL(NULLIF(RTRIM(s.Nombre),''),N''),NULLIF(RTRIM(s.direccion),''),s.ciudad,NULLIF(RTRIM(s.telefono),''),NULLIF(RTRIM(s.Celular),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cdt_asoreferencia s
    INNER JOIN [migration].[Map_CDTCertificates] mc ON s.NumCdat = mc.OldNumCdat
    INNER JOIN [migration].[Map_People] mp ON s.codigoter = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'CDT_AssociateReferences',(SELECT COUNT(*) FROM [dbo].[CDT_AssociateReferences]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'CDT_AssociateReferences',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- CDT BLOCK 6: CDT_Audit (cdt_maeaud) — copy as-is
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_Audit (cdt_maeaud)...';

    INSERT INTO [dbo].[CDT_Audit] ([Action],[ActionDate],[UserName],[EntityType],[EntityId],[PersonId],[CreditLineId],[IssueDateOld],[IssueDateNew],[LegalRepIdOld],[LegalRepIdNew],[LegalRepNameOld],[LegalRepNameNew],[AddressOld],[AddressNew],[PhoneOld],[PhoneNew],[MobileOld],[MobileNew],[StatusOld],[StatusNew],[AccrualDateOld],[AccrualDateNew],[MaturityDateOld],[MaturityDateNew],[TermOld],[TermNew],[InterestRateOld],[InterestRateNew],[AmountOld],[AmountNew],[CancelledByOld],[CancelledByNew],[CancellationDateOld],[CancellationDateNew],[IsCapitalizedOld],[IsCapitalizedNew],[PreviousCertIdOld],[PreviousCertIdNew],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.Accion,N''),s.fechasys,NULLIF(RTRIM(s.usuario_act),''),N'CDT',s.num_cdat,mp.NewPersonId,s.lincred,CASE WHEN s.fec_crea_ant<='1900-01-02' THEN NULL ELSE CAST(s.fec_crea_ant AS DATE) END,CASE WHEN s.fec_crea_act<='1900-01-02' THEN NULL ELSE CAST(s.fec_crea_act AS DATE) END,NULLIF(RTRIM(s.rep_legal_ant),''),NULLIF(RTRIM(s.rep_legal_act),''),NULLIF(RTRIM(s.nom_repres_ant),''),NULLIF(RTRIM(s.nom_repres_act),''),NULLIF(RTRIM(s.dir_comerci_ant),''),NULLIF(RTRIM(s.dir_comerci_act),''),NULLIF(RTRIM(s.telefono_ant),''),NULLIF(RTRIM(s.telefono_act),''),NULLIF(RTRIM(s.celular_ant),''),NULLIF(RTRIM(s.celular_act),''),NULLIF(RTRIM(s.Estado_ant),''),NULLIF(RTRIM(s.Estado_act),''),CASE WHEN s.fec_causacion_ant<='1900-01-02' THEN NULL ELSE CAST(s.fec_causacion_ant AS DATE) END,CASE WHEN s.fec_causacion_act<='1900-01-02' THEN NULL ELSE CAST(s.fec_causacion_act AS DATE) END,CASE WHEN s.Fecvence_ant<='1900-01-02' THEN NULL ELSE CAST(s.Fecvence_ant AS DATE) END,CASE WHEN s.Fecvence_act<='1900-01-02' THEN NULL ELSE CAST(s.Fecvence_act AS DATE) END,s.Plazo_ant,s.Plazo_act,s.TasaInt_ant,s.TasaInt_act,s.Valorob_ant,s.Valorob_act,NULLIF(RTRIM(s.usucancelacdat_ant),''),NULLIF(RTRIM(s.usucancelacdat_act),''),CASE WHEN s.feccancela_ant<='1900-01-02' THEN NULL ELSE s.feccancela_ant END,CASE WHEN s.feccancela_act<='1900-01-02' THEN NULL ELSE s.feccancela_act END,NULLIF(RTRIM(s.Marca_Capitalizado_ant),''),NULLIF(RTRIM(s.Marca_Capitalizado_act),''),s.num_cdat_Anterior_ant,s.num_cdat_Anterior_act,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cdt_maeaud s
    LEFT JOIN [migration].[Map_People] mp ON s.codigoter = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'CDT_Audit',(SELECT COUNT(*) FROM [dbo].[CDT_Audit]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'CDT_Audit',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- CDT BLOCK 7: CDT_ParameterAudit (cdt_paramaud) — copy as-is
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> CDT_ParameterAudit (cdt_paramaud)...';

    INSERT INTO [dbo].[CDT_ParameterAudit] ([Action],[ActionDate],[UserName],[CreditLineId],[DescriptionOld],[DescriptionNew],[MinRateOld],[MinRateNew],[AnnualRateOld],[AnnualRateNew],[WithholdingRateOld],[WithholdingRateNew],[MinWithholdingAmtOld],[MinWithholdingAmtNew],[PaymentMethodOld],[PaymentMethodNew],[InterestConceptOld],[InterestConceptNew],[WithholdingConceptOld],[WithholdingConceptNew],[MonthlyIncrementOld],[MonthlyIncrementNew],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.Accion,N''),s.fechasys,NULLIF(RTRIM(s.usuario_act),''),s.lincred,NULLIF(RTRIM(s.Descripcion_ant),''),NULLIF(RTRIM(s.Descripcion_act),''),s.TasaMin_ant,s.TasaMin_act,s.PorAnual_ant,s.PorAnual_act,s.PorRetfte_ant,s.PorRetfte_act,s.VlrMinRet_ant,s.VlrMinRet_act,s.FormaPag_ant,s.FormaPag_act,s.CptoInt_ant,s.CptoInt_act,s.CptoRet_ant,s.CptoRet_act,s.IncreMen_ant,s.IncreMen_act,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.cdt_paramaud s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'CDT_ParameterAudit',(SELECT COUNT(*) FROM [dbo].[CDT_ParameterAudit]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'CDT_ParameterAudit',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ************************************************************
-- DEB_ — Debit Cards (7 tables)
-- ************************************************************

-- ============================================================
-- DEB BLOCK 1: DEB_AgreementParameters (deb_parconv)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> DEB_AgreementParameters (deb_parconv)...';

    INSERT INTO [dbo].[DEB_AgreementParameters] ([AgreementCode],[TokenRS],[Name],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.idconv),''),N'0'),ISNULL(s.tokenrs,0),NULLIF(RTRIM(s.nombre),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_parconv s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_AgreementParameters',(SELECT COUNT(*) FROM [dbo].[DEB_AgreementParameters]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_AgreementParameters',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- DEB BLOCK 2: DEB_Cards (deb_maetarj) — PersonId from Map_People
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> DEB_Cards (deb_maetarj)...';

    INSERT INTO [dbo].[DEB_Cards] ([BankId],[CardNumber],[BinCode],[AccountNumber],[CreditLineId],[AccountType],[ErrorCode],[PersonId],[OperationType],[Status],[InitialConcept],[AvailableBalance],[DailyAtmLimit],[DailyAtmTransactions],[DailyPosLimit],[DailyPosTransactions],[IssueDate],[ExpiryDate],[LastEventDate],[ExecutionTime],[DownloadDate],[Pin],[Mark],[AvailableLimitType],[AtmLimitType],[IsDebitOrCredit],[CoSigner1],[CoSigner2],[CutoffDay],[CreditLimit],[TerminalId],[BlockReasonId],[BlockedByUserId],[DomesticAvailableBalance],[DomesticAtmLimit],[DomesticAtmTransactions],[DomesticPosLimit],[DomesticPosTransactions],[DomesticAccountNumber],[ChargesManagement],[ChargesManagementDs],[LegacyLineId],[LimitAssignmentDate],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.Banco,0),ISNULL(NULLIF(RTRIM(s.Tarjeta),''),N'0'),NULLIF(RTRIM(s.Bin),''),s.Cuenta,s.Lincred,NULLIF(RTRIM(s.TipoCta),''),NULLIF(RTRIM(s.Error),''),mp.NewPersonId,NULLIF(RTRIM(s.Operacion),''),NULLIF(RTRIM(s.Estado),''),NULLIF(RTRIM(s.Cpto_Ini),''),s.Disponible,s.CupoCajero,s.TranCajero,s.CupoPos,s.TranPos,CASE WHEN s.FecAsignacion<='1900-01-02' THEN NULL ELSE CAST(s.FecAsignacion AS DATE) END,CASE WHEN s.fechavence<='1900-01-02' THEN NULL ELSE CAST(s.fechavence AS DATE) END,CASE WHEN s.FecNovedad<='1900-01-02' THEN NULL ELSE CAST(s.FecNovedad AS DATE) END,s.HoraEjccion,CASE WHEN s.FecDscrgue<='1900-01-02' THEN NULL ELSE CAST(s.FecDscrgue AS DATE) END,NULLIF(RTRIM(s.Pin),''),ISNULL(s.Marca,0),ISNULL(s.tipoDisponible,0),s.ClaseTopeCajero,ISNULL(s.debcre,N'D'),NULLIF(RTRIM(s.Codeudor1),''),NULLIF(RTRIM(s.Codeudor2),''),ISNULL(s.diacorte,0),ISNULL(s.cupocredito,0),NULLIF(RTRIM(s.Trmnal),''),ISNULL(s.motivobloqueo,0),NULLIF(RTRIM(s.UsuarioBloqueo),''),ISNULL(s.DisponibleNal,0),ISNULL(s.CupoCajeroNal,0),ISNULL(s.TranCajeroNal,0),ISNULL(s.CupoPosNal,0),ISNULL(s.TranPosNal,0),ISNULL(s.CuentaNal,0),ISNULL(s.cobrosmanejo,0),ISNULL(s.cobrosmanejods,0),ISNULL(s.lineaid,0),CASE WHEN s.FecAsignaCupo<='1900-01-02' THEN NULL ELSE CAST(s.FecAsignaCupo AS DATE) END,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_maetarj s
    LEFT JOIN [migration].[Map_People] mp ON s.Codigoter = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_Cards',(SELECT COUNT(*) FROM [dbo].[DEB_Cards]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_Cards',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- DEB BLOCK 3: DEB_Transactions (deb_movto)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> DEB_Transactions (deb_movto)...';

    INSERT INTO [dbo].[DEB_Transactions] ([CardId],[SequenceCode],[CardNumber],[TransactionDate],[Amount],[TransactionType],[CausalCode],[Status],[SourceSystem],[TransactionTime],[NetworkCode],[MessageCode],[VatAmount],[VatBase],[CommissionAmount],[MethodCode],[ErrorCode],[MerchantCode],[AuthorizationCode],[CreatedBy],[CreatedAt])
    SELECT dc.Id,ISNULL(NULLIF(RTRIM(s.Secuencia),''),N'0'),ISNULL(NULLIF(RTRIM(s.Tarjeta),''),N'0'),CASE WHEN s.FechaMovto<='1900-01-02' THEN NULL ELSE s.FechaMovto END,s.Monto,NULLIF(RTRIM(s.Id),''),NULLIF(RTRIM(s.Causal),''),NULLIF(RTRIM(s.Estado),''),NULLIF(RTRIM(s.Source),''),NULLIF(RTRIM(s.Hora),''),NULLIF(RTRIM(s.Net),''),s.Message,s.Iva,s.BaseIva,s.Comision,s.Metodo,NULLIF(RTRIM(s.Error),''),NULLIF(RTRIM(s.codigomercante),''),NULLIF(RTRIM(s.codigoautorizacion),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_movto s
    LEFT JOIN [dbo].[DEB_Cards] dc ON RTRIM(s.Tarjeta) = dc.CardNumber AND s.Banco = dc.BankId;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_Transactions',(SELECT COUNT(*) FROM [dbo].[DEB_Transactions]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_Transactions',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- DEB BLOCK 4-7: Remaining DEB tables
-- ============================================================

-- DEB BLOCK 4: DEB_PosTerminals (deb_pardatafonos)
BEGIN TRY BEGIN TRAN; PRINT '>> DEB_PosTerminals (deb_pardatafonos)...';
    INSERT INTO [dbo].[DEB_PosTerminals] ([TerminalCode],[InternalCode],[VoucherCode],[MerchantName],[Location],[Status],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.terminal),''),N'0'),ISNULL(s.idcodigo,0),NULLIF(RTRIM(s.cpte),''),NULLIF(RTRIM(s.nombremercante),''),NULLIF(RTRIM(s.ubicacion),''),NULLIF(RTRIM(s.estado),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_pardatafonos s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_PosTerminals',(SELECT COUNT(*) FROM [dbo].[DEB_PosTerminals]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_PosTerminals',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- DEB BLOCK 5: DEB_DailyParameters (deb_pardiario)
BEGIN TRY BEGIN TRAN; PRINT '>> DEB_DailyParameters (deb_pardiario)...';
    INSERT INTO [dbo].[DEB_DailyParameters] ([ParameterCode],[BankId],[BatchVoucherCode],[OnlineVoucherCode],[Description],[LastUpdateDate],[NewCardsCount],[LastCardNumber],[ClosingDate],[ClosingVoucherCode],[ClosingSequenceNumber],[PosClosingVoucherCode],[PosClosingSequence],[ClosingFlag],[NetworkCommission],[OtherNetworkCommission],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.IdCodigo,0),ISNULL(NULLIF(RTRIM(s.Banco),''),N'0'),ISNULL(NULLIF(RTRIM(s.Cptebatch),''),N''),ISNULL(NULLIF(RTRIM(s.CpteLinea),''),N''),ISNULL(NULLIF(RTRIM(s.Detalle),''),N''),CASE WHEN s.fechaActualizacion<='1900-01-02' THEN NULL ELSE CAST(s.fechaActualizacion AS DATE) END,ISNULL(s.tarjetasnuevas,0),NULLIF(RTRIM(s.UltimaTarjeta),''),CASE WHEN s.FechaCierre<='1900-01-02' THEN NULL ELSE CAST(s.FechaCierre AS DATE) END,NULLIF(RTRIM(s.CpteCierre),''),ISNULL(s.consecCierre,0),NULLIF(RTRIM(s.CpteCierreDatafono),''),ISNULL(s.consecCierreDatafono,0),NULLIF(RTRIM(s.cierre),''),ISNULL(s.comisionred,0),ISNULL(s.comisionotrared,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_pardiario s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_DailyParameters',(SELECT COUNT(*) FROM [dbo].[DEB_DailyParameters]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_DailyParameters',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- DEB BLOCK 6: DEB_Agreements (deb_enpacto)
BEGIN TRY BEGIN TRAN; PRINT '>> DEB_Agreements (deb_enpacto)...';
    INSERT INTO [dbo].[DEB_Agreements] ([BatchId],[ProcessDate],[ProcessTime],[CardNumber],[AuthCode],[AccountNumber],[NetworkCode],[TransactionType],[Amount],[ConceptCode],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.id,0),ISNULL(NULLIF(RTRIM(s.FECH),''),N''),NULLIF(RTRIM(s.HORA),''),ISNULL(NULLIF(RTRIM(s.TARJ),''),N''),NULLIF(RTRIM(s.NAUD),''),NULLIF(RTRIM(s.CUEN),''),NULLIF(RTRIM(s.NETW),''),NULLIF(RTRIM(s.TIPO),''),s.MONT,NULLIF(RTRIM(s.CONC),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_enpacto s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_Agreements',(SELECT COUNT(*) FROM [dbo].[DEB_Agreements]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_Agreements',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- DEB BLOCK 7: DEB_AgreementMembers (deb_enpactors)
BEGIN TRY BEGIN TRAN; PRINT '>> DEB_AgreementMembers (deb_enpactors)...';
    INSERT INTO [dbo].[DEB_AgreementMembers] ([ProcessDate],[UserName],[VoucherCode],[DocumentNumber],[IsApplied],[CreatedBy],[CreatedAt])
    SELECT CASE WHEN s.fecha<='1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.fecha AS DATE) END,ISNULL(NULLIF(RTRIM(s.usuario),''),N''),ISNULL(NULLIF(RTRIM(s.comprobante),''),N''),ISNULL(s.numero_domto,0),ISNULL(s.aplicado,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.deb_enpactors s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'DEB_AgreementMembers',(SELECT COUNT(*) FROM [dbo].[DEB_AgreementMembers]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'DEB_AgreementMembers',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ************************************************************
-- TRS_ — Treasury (3 tables)
-- ************************************************************

-- ============================================================
-- TRS BLOCK 1: TRS_Concepts (TES_CPTOS)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> TRS_Concepts (TES_CPTOS)...';

    INSERT INTO [dbo].[TRS_Concepts] ([ConceptCode],[Name],[ShortName],[ConceptType],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.CONCEPTO),''),N'0'),ISNULL(NULLIF(RTRIM(s.NOMBRE),''),N'Sin nombre'),NULLIF(RTRIM(s.NOMRES),''),NULLIF(RTRIM(s.TIPO_CPTO),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.TES_CPTOS s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'TRS_Concepts',(SELECT COUNT(*) FROM [dbo].[TRS_Concepts]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'TRS_Concepts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- TRS BLOCK 2: TRS_Checks (TES_CHEQUES) — PersonId from Map_People, BankId from Map_Banks
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> TRS_Checks (TES_CHEQUES)...';

    INSERT INTO [dbo].[TRS_Checks] (
        [ConceptCode], [BankId], [SequentialNumber], [PersonId],
        [VoucherCode], [VoucherNumber], [CheckDate], [CheckNumber], [Amount],
        [VoidDetail], [VoidUserId], [VoidVoucherCode], [VoidVoucherNumber], [VoidDate],
        [RecordUserId], [RecordDate], [Status],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.CONCEPTO), ''), N''),
        mb.NewBankId,
        ISNULL(s.CONSECUTIVO, 0),
        mp.NewPersonId,
        NULLIF(RTRIM(s.COMPROBANTE), ''), s.NUME_COMPRO,
        CASE WHEN s.FECHA_CHEQUE <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.FECHA_CHEQUE AS DATE) END,
        ISNULL(s.NUMERO_CHEQUE, 0), ISNULL(s.VALOR, 0),
        NULLIF(RTRIM(s.DETALLE_ANULA), ''), NULLIF(RTRIM(s.USUARIO_ANULA), ''),
        NULLIF(RTRIM(s.COMPRO_ANULA), ''), s.NUMCOMP_ANULA,
        CASE WHEN s.FECHA_ANULA <= '1900-01-02' THEN NULL ELSE s.FECHA_ANULA END,
        NULLIF(RTRIM(s.USUARIO_GRABA), ''),
        CASE WHEN s.FECHASYS_GRABA <= '1900-01-02' THEN NULL ELSE s.FECHASYS_GRABA END,
        NULLIF(RTRIM(s.ESTADO), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.TES_CHEQUES s
    LEFT JOIN [migration].[Map_People] mp ON CAST(s.NIT AS NVARCHAR(20)) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_Banks] mb ON s.BANCO = mb.OldBankCode;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'TRS_Checks', (SELECT COUNT(*) FROM [dbo].[TRS_Checks]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'TRS_Checks', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- TRS BLOCK 3: TRS_Invoices (TES_FACTURA)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> TRS_Invoices (TES_FACTURA)...';

    INSERT INTO [dbo].[TRS_Invoices] (
        [ConceptCode], [ConsecutiveNumber], [EntryDate], [PeriodCode],
        [InvoiceNumber], [InvoiceDate], [PersonId], [DueDate],
        [AccountCode], [CostCenterId], [BranchId], [DocumentCode],
        [Description], [Amount], [ScheduledDate], [PaymentDate],
        [VoucherCode], [VoucherNumber], [BankId], [CheckNumber], [CheckAmount],
        [Status], [DocumentType], [DocumentNumber], [PaymentForm], [HasCommission],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(NULLIF(RTRIM(s.CONCEPTO), ''), N''),
        ISNULL(s.CONSECUTIVO, 0),
        CASE WHEN s.FECHA <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.FECHA AS DATE) END,
        s.PERIODO,
        ISNULL(NULLIF(RTRIM(s.FACTURA), ''), N'0'),
        CASE WHEN s.FECHA_FAC <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.FECHA_FAC AS DATE) END,
        mp.NewPersonId,
        CASE WHEN s.FEC_VEMTO <= '1900-01-02' THEN NULL ELSE CAST(s.FEC_VEMTO AS DATE) END,
        NULLIF(RTRIM(s.CUENTA_CONTABLE), ''),
        NULLIF(RTRIM(s.CCOSTO), ''), NULLIF(RTRIM(s.AGENCIA), ''),
        NULLIF(RTRIM(s.DOCUMNTO), ''),
        NULLIF(RTRIM(s.DETALLE), ''),
        ISNULL(s.VALOR, 0),
        CASE WHEN s.FEC_PROGRAMA <= '1900-01-02' THEN NULL ELSE CAST(s.FEC_PROGRAMA AS DATE) END,
        CASE WHEN s.FEC_PAGO <= '1900-01-02' THEN NULL ELSE CAST(s.FEC_PAGO AS DATE) END,
        NULLIF(RTRIM(s.COMBTE), ''), s.NUME_CPMPTO,
        NULLIF(RTRIM(s.BANCO), ''), s.NUMERO_CHEQUE, ISNULL(s.VALOR_CHEQUE, 0),
        NULLIF(RTRIM(s.ESTADO), ''),
        NULLIF(RTRIM(s.docu_tipo), ''), NULLIF(RTRIM(s.docu_numero), ''),
        NULLIF(RTRIM(s.forpag), ''), s.comision,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.TES_FACTURA s
    LEFT JOIN [migration].[Map_People] mp ON CAST(s.NIT AS NVARCHAR(20)) = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'TRS_Invoices', (SELECT COUNT(*) FROM [dbo].[TRS_Invoices]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'TRS_Invoices', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

PRINT '================================================================';
PRINT '  07_Migrate_CDT_DEB_TRS.sql — END  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO
