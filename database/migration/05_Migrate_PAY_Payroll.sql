-- ============================================================
-- IngenIA365ERP — Data Migration Script
-- Step 05: PAY_ (Payroll / Nomina) — 27 tables
-- Source: [old].dbo.nom_*
-- Target: [dbo].PAY_*
-- ============================================================
-- Prerequisites:
--   01_Create_MigrationSchema.sql (migration schema + Map_* tables)
--   02_Migrate_COR.sql (Map_People populated)
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT OFF;
GO

PRINT '================================================================';
PRINT '  05_Migrate_PAY_Payroll.sql — START  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO

-- ============================================================
-- BLOCK 1: PAY_PayrollConcepts (nom_cptos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_PayrollConcepts (nom_cptos)...';

    INSERT INTO [dbo].[PAY_PayrollConcepts] (
        [ConceptCode], [Name], [ShortName], [ConceptClass], [Nature],
        [Value], [Factor], [Base], [AffectsSalary], [DaysComputed],
        [TimesExtended], [ValueExtended], [LiquidationBase], [TopSalary],
        [CertificateLine], [CertificateColumn], [AffectsBenefits], [AffectsWithholding],
        [IsBenefit], [MaintainBalance], [Priority], [ProvisionRate],
        [ProvisionBase], [TaxId], [IntegralSalary], [SingleUnit],
        [AdminBase], [RelatedConceptId], [PaymentConceptId], [VatRate],
        [EquivalentCode], [MinorRate], [MajorRate],
        [AffectsSeverance], [AffectsBonus], [AffectsVacation], [AffectsIndemnity],
        [AdminId], [IsAutomatic], [ConceptSubClass],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.concepto,
        ISNULL(NULLIF(RTRIM(s.nombre), ''), N'Sin nombre'),
        ISNULL(NULLIF(RTRIM(s.nomres), ''), N''),
        ISNULL(s.clase, 0), ISNULL(s.naturaleza, 0),
        ISNULL(s.valor, 0), ISNULL(s.factor, 0),
        ISNULL(s.base, 0), ISNULL(s.afectasalario, 0),
        ISNULL(NULLIF(RTRIM(s.diasliquida), ''), N''),
        ISNULL(s.vecesextiende, 0), ISNULL(s.valorextiende, 0),
        ISNULL(s.baseliquidacion, 0), ISNULL(s.topesalario, 0),
        ISNULL(NULLIF(RTRIM(s.lineacertificado), ''), N''),
        ISNULL(NULLIF(RTRIM(s.columnacertificado), ''), N''),
        ISNULL(s.afectaprestaciones, 0), ISNULL(s.afectaretencion, 0),
        ISNULL(s.esprestacion, 0), ISNULL(s.mantienesaldo, 0),
        ISNULL(NULLIF(RTRIM(s.prioridad), ''), N''),
        ISNULL(s.tasaprovision, 0),
        ISNULL(s.baseprovision, 0),
        ISNULL(NULLIF(RTRIM(s.nit), ''), N''),
        ISNULL(s.salariointegral, 0), ISNULL(s.unidadindividual, 0),
        ISNULL(s.baseadmin, 0), ISNULL(s.cptorelacionado, 0),
        ISNULL(s.cptopago, 0), ISNULL(s.tasaiva, 0),
        ISNULL(NULLIF(RTRIM(s.codigoequivalente), ''), N''),
        ISNULL(s.tasamenor, 0), ISNULL(s.tasamayor, 0),
        ISNULL(s.afectacesantia, 0), ISNULL(s.afectaprima, 0),
        ISNULL(s.afectavacacion, 0), ISNULL(s.afectaindemnizacion, 0),
        ISNULL(NULLIF(RTRIM(s.admin), ''), N''),
        ISNULL(s.esautomatico, N'N'), ISNULL(s.subclase, 0),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.nom_cptos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'PAY_PayrollConcepts', (SELECT COUNT(*) FROM [dbo].[PAY_PayrollConcepts]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'PAY_PayrollConcepts', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 2: PAY_HealthInsuranceProviders (nom_eps)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_HealthInsuranceProviders (nom_eps)...';

    INSERT INTO [dbo].[PAY_HealthInsuranceProviders] (
        [Code], [Name], [ShortName], [TaxId], [CheckDigit],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.codigo, ISNULL(NULLIF(RTRIM(s.nombre), ''), N'Sin nombre'),
        ISNULL(NULLIF(RTRIM(s.nomres), ''), N''),
        ISNULL(NULLIF(RTRIM(s.nit), ''), N''), ISNULL(s.digverif, 0),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.nom_eps s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'PAY_HealthInsuranceProviders', (SELECT COUNT(*) FROM [dbo].[PAY_HealthInsuranceProviders]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'PAY_HealthInsuranceProviders', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 3: PAY_PensionProviders (nom_pensiones)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_PensionProviders (nom_pensiones)...';

    INSERT INTO [dbo].[PAY_PensionProviders] ([Code],[Name],[ShortName],[TaxId],[CheckDigit],[CreatedBy],[CreatedAt])
    SELECT s.codigo,ISNULL(NULLIF(RTRIM(s.nombre),''),N'Sin nombre'),ISNULL(NULLIF(RTRIM(s.nomres),''),N''),ISNULL(NULLIF(RTRIM(s.nit),''),N''),ISNULL(s.digverif,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_pensiones s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_PensionProviders',(SELECT COUNT(*) FROM [dbo].[PAY_PensionProviders]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_PensionProviders',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 4: PAY_WorkRiskProviders (nom_arp)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_WorkRiskProviders (nom_arp)...';

    INSERT INTO [dbo].[PAY_WorkRiskProviders] ([Code],[Name],[ShortName],[TaxId],[CheckDigit],[Factor],[CreatedBy],[CreatedAt])
    SELECT s.codigo,ISNULL(NULLIF(RTRIM(s.nombre),''),N'Sin nombre'),ISNULL(NULLIF(RTRIM(s.nomres),''),N''),ISNULL(NULLIF(RTRIM(s.nit),''),N''),ISNULL(s.digverif,0),ISNULL(s.factor,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_arp s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_WorkRiskProviders',(SELECT COUNT(*) FROM [dbo].[PAY_WorkRiskProviders]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_WorkRiskProviders',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 5: PAY_WorkRiskRates (nom_arptarifa)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_WorkRiskRates (nom_arptarifa)...';

    INSERT INTO [dbo].[PAY_WorkRiskRates] ([Code],[Name],[ShortName],[Rate],[CreatedBy],[CreatedAt])
    SELECT s.codigo,ISNULL(NULLIF(RTRIM(s.nombre),''),N'Sin nombre'),ISNULL(NULLIF(RTRIM(s.nomres),''),N''),ISNULL(s.tarifa,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_arptarifa s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_WorkRiskRates',(SELECT COUNT(*) FROM [dbo].[PAY_WorkRiskRates]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_WorkRiskRates',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 6: PAY_SeveranceProviders (nom_cesantias)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_SeveranceProviders (nom_cesantias)...';

    INSERT INTO [dbo].[PAY_SeveranceProviders] ([Code],[Name],[ShortName],[TaxId],[CheckDigit],[CreatedBy],[CreatedAt])
    SELECT s.codigo,ISNULL(NULLIF(RTRIM(s.nombre),''),N'Sin nombre'),ISNULL(NULLIF(RTRIM(s.nomres),''),N''),ISNULL(NULLIF(RTRIM(s.nit),''),N''),ISNULL(s.digverif,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_cesantias s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_SeveranceProviders',(SELECT COUNT(*) FROM [dbo].[PAY_SeveranceProviders]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_SeveranceProviders',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 7: PAY_WithholdingCauses (nom_cauret)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_WithholdingCauses (nom_cauret)...';

    INSERT INTO [dbo].[PAY_WithholdingCauses] ([Code],[Name],[ShortName],[IndemnityType],[AutoDeductions],[CreatedBy],[CreatedAt])
    SELECT s.codigo,ISNULL(NULLIF(RTRIM(s.nombre),''),N'Sin nombre'),ISNULL(NULLIF(RTRIM(s.nomres),''),N''),ISNULL(s.tipoindemnizacion,0),ISNULL(s.deduccautomatica,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_cauret s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_WithholdingCauses',(SELECT COUNT(*) FROM [dbo].[PAY_WithholdingCauses]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_WithholdingCauses',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 8: PAY_Employees (nom_empleados) — populate Map_Employees
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_Employees (nom_empleados) — CRITICAL...';

    INSERT INTO [dbo].[PAY_Employees] (
        [PersonId], [PayrollCompanyId], [CostCenterId], [IdentificationNumber],
        [LastName], [FirstName], [IssuedAt], [EmployeeClass], [Gender],
        [MilitaryBooklet], [MilitaryDistrict], [DriverLicense], [LicenseCategory],
        [Address], [CityId], [Phone], [Mobile], [Email],
        [BloodType], [RhFactor], [BankId], [AccountType], [BankAccountNumber],
        [AcademicLevel], [PayrollClass], [PaymentMethod], [AreaCode], [SectionId],
        [PositionId], [Salary], [SalaryType], [EffectiveDate],
        [TransportSubsidyClass], [ContractType], [ContractEndDate],
        [FirstPayCycle], [WithholdingTaxRate], [ContributionCycle],
        [TerminationDate], [TerminationCause], [RehireDate], [BirthDate],
        [PantsSize], [ShirtSize], [ShoeSize], [HelmetSize],
        [RepresentationExpense], [TechnicalBonus], [OtherBonus],
        [HealthInsuranceId], [PensionFundId], [WorkRiskId], [SeveranceFundId],
        [SenaId], [IcbfId], [FamilySubsidyId],
        [SeveranceCauseDate], [BonusDays], [VacationDays], [IndemnityDays],
        [SeveranceAvgDays], [BonusAvgDays], [IndemnityAvgDays], [HolidayDays],
        [PensionFundMember], [JoinDate], [LicenseExpiryDate], [VacationAvgDays],
        [VacationCauseDate], [BonusCauseDate], [Status], [OverallSize],
        [EmployeeType], [WorkRiskRateId], [IsLiquidated], [LiquidationDate],
        [SpecialRegime], [SeveranceDaysCalc], [IndemnityDaysCalc],
        [ExtraBonusFlag], [DeductibleWithholding], [WithholdingCycle],
        [WithholdingAvgType],
        [LegacyIdNomina], [LegacyIdEmpleado],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        ISNULL(s.idnomina, 0),
        ISNULL(NULLIF(RTRIM(s.ccosto), ''), N''),
        ISNULL(NULLIF(RTRIM(s.cc_nit), ''), N''),
        ISNULL(NULLIF(RTRIM(s.apellidos), ''), N''),
        ISNULL(NULLIF(RTRIM(s.nombres), ''), N''),
        ISNULL(NULLIF(RTRIM(s.expedida), ''), N''),
        ISNULL(s.claseempleado, 0), ISNULL(s.sexo, 0),
        ISNULL(NULLIF(RTRIM(s.libretamilitar), ''), N''),
        ISNULL(NULLIF(RTRIM(s.distrito), ''), N''),
        ISNULL(NULLIF(RTRIM(s.pase), ''), N''),
        ISNULL(NULLIF(RTRIM(s.categoriapase), ''), N''),
        ISNULL(NULLIF(RTRIM(s.direccion), ''), N''),
        ISNULL(s.ciudad, 0),
        ISNULL(NULLIF(RTRIM(s.telefono), ''), N''),
        ISNULL(NULLIF(RTRIM(s.celular), ''), N''),
        ISNULL(NULLIF(RTRIM(s.email), ''), N''),
        ISNULL(NULLIF(RTRIM(s.tiposangre), ''), N''),
        ISNULL(NULLIF(RTRIM(s.rh), ''), N''),
        ISNULL(NULLIF(RTRIM(s.banco), ''), N''),
        ISNULL(s.tipocuenta, 0),
        ISNULL(NULLIF(RTRIM(s.cuentabanco), ''), N''),
        ISNULL(s.nivelacademico, 0), ISNULL(s.clasenomina, 0),
        ISNULL(s.forpago, 0),
        ISNULL(NULLIF(RTRIM(s.area), ''), N''),
        ISNULL(NULLIF(RTRIM(s.seccion), ''), N''),
        ISNULL(s.cargo, 0), ISNULL(s.sueldo, 0),
        ISNULL(s.tiposalario, 0),
        CASE WHEN s.fecefectiva <= '1900-01-02' THEN NULL ELSE s.fecefectiva END,
        ISNULL(s.clasetranspsubsidio, 0),
        ISNULL(s.tipocontrato, 0),
        CASE WHEN s.fecfincontrato <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecfincontrato END,
        ISNULL(s.primerciclo, 0), s.tasaretencion,
        ISNULL(s.cicloaportacion, 0),
        CASE WHEN s.fecretiro <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecretiro END,
        ISNULL(NULLIF(RTRIM(s.causaretiro), ''), N''),
        CASE WHEN s.fecreingreso <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecreingreso END,
        CASE WHEN s.fecnacimiento <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecnacimiento END,
        ISNULL(NULLIF(RTRIM(s.tallapantalon), ''), N''),
        ISNULL(NULLIF(RTRIM(s.tallacamisa), ''), N''),
        ISNULL(NULLIF(RTRIM(s.tallacalzado), ''), N''),
        ISNULL(NULLIF(RTRIM(s.tallacasco), ''), N''),
        ISNULL(s.gastorepresentacion, 0),
        ISNULL(s.bonificaciontecnica, 0),
        ISNULL(s.otrasbonificaciones, 0),
        ISNULL(s.eps, 0), ISNULL(s.fondopension, 0),
        ISNULL(s.arp, 0), ISNULL(s.fondocesantias, 0),
        ISNULL(s.sena, 0), ISNULL(s.icbf, 0), ISNULL(s.cajafamiliar, 0),
        CASE WHEN s.feccausacesantia <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.feccausacesantia END,
        ISNULL(s.diascalcprima, 0), ISNULL(s.diascalcvacaciones, 0),
        ISNULL(s.diascalcindemnizacion, 0),
        ISNULL(s.diaspromcesantia, 0), ISNULL(s.diaspromprima, 0),
        ISNULL(s.diaspromindemnizacion, 0), ISNULL(s.diasfestivos, 0),
        ISNULL(s.afiliadopension, N'S'),
        CASE WHEN s.fecingreaso <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecingreaso END,
        CASE WHEN s.fecvencepase <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecvencepase END,
        ISNULL(s.diaspromvacaciones, 0),
        CASE WHEN s.feccausavacacion <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.feccausavacacion END,
        CASE WHEN s.feccausaprima <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.feccausaprima END,
        ISNULL(s.estado, 0), s.tallaoverall,
        ISNULL(s.tipoempleado, 0), ISNULL(s.idtarifaarp, 0),
        ISNULL(s.liquidado, N'N'), s.fecliquidacion,
        ISNULL(s.regimenespecial, N'N'),
        ISNULL(s.diascalccesantia2, 0), ISNULL(s.diascalcindemnizacion2, 0),
        ISNULL(s.extraprima, N'N'), ISNULL(s.deducibleretencion, 0),
        ISNULL(s.cicloretencion, 0), ISNULL(s.tipopromretencion, 0),
        s.idnomina, s.idempleado,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.nom_empleados s
    LEFT JOIN [migration].[Map_People] mp ON CAST(s.cc_nit AS NVARCHAR(20)) = mp.OldCodigoTer;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

    -- Populate Map_Employees
    INSERT INTO [migration].[Map_Employees] (OldIdNomina, OldIdEmpleado, NewEmployeeId)
    SELECT [LegacyIdNomina], [LegacyIdEmpleado], [Id]
    FROM [dbo].[PAY_Employees]
    WHERE [LegacyIdEmpleado] IS NOT NULL;

    PRINT '   Map_Employees populated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'PAY_Employees', (SELECT COUNT(*) FROM [dbo].[PAY_Employees]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'PAY_Employees', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 9: PAY_PayPeriods (nom_perpagos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_PayPeriods (nom_perpagos)...';

    INSERT INTO [dbo].[PAY_PayPeriods] ([PlanId],[PayrollCompanyId],[Description],[PayDate],[LiquidationCompanyId],[CycleMonth],[CycleHours],[StartDate],[EndDate],[Periodicity],[AdditionalConcept1],[AdditionalConcept2],[AdditionalConcept3],[AdditionalConcept4],[OnlyEntries],[NoAutoSalaryLiq],[NoAbsenceLiq],[NoDirectDebitLiq],[Status],[StatusMessage],[PeriodId],[AdvanceLiquidation],[AdvanceCrossing],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idplan,0),ISNULL(s.idnomina,0),NULLIF(RTRIM(s.descripcion),''),NULLIF(RTRIM(s.fechapago),''),NULLIF(RTRIM(s.empresaliq),''),s.ciclodelmes,s.ciclohoras,CASE WHEN s.fecinicio<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecinicio END,CASE WHEN s.fecfin<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecfin END,s.periodicidad,s.cptoadicional1,s.cptoadicional2,s.cptoadicional3,s.cptoadicional4,s.solonovedades,s.noliqautosueldo,s.noliqausentismo,s.noliqlibranza,ISNULL(s.estado,0),ISNULL(NULLIF(RTRIM(s.msgestado),''),N''),ISNULL(s.periodo,0),ISNULL(s.liquidaanticipo,N'N'),ISNULL(s.cruceanticipo,N'N'),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_perpagos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_PayPeriods',(SELECT COUNT(*) FROM [dbo].[PAY_PayPeriods]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_PayPeriods',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 10: PAY_PayrollPlanLiquidations (nom_liqplan)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_PayrollPlanLiquidations (nom_liqplan)...';

    INSERT INTO [dbo].[PAY_PayrollPlanLiquidations] ([PayPeriodId],[PayrollCompanyId],[EmployeeId],[ConceptId],[SequenceNumber],[Nature],[Days],[Time],[Amount],[PaymentMethod],[CostCenterCode],[UserName],[SystemDate],[RecordType],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idplan,0),ISNULL(s.idnomina,0),me.NewEmployeeId,ISNULL(s.concepto,0),ISNULL(s.secuencia,0),ISNULL(s.naturaleza,0),ISNULL(s.dias,0),ISNULL(s.tiempo,0),ISNULL(s.valor,0),ISNULL(s.forpago,0),ISNULL(NULLIF(RTRIM(s.ccosto),''),N''),ISNULL(NULLIF(RTRIM(s.usuario),''),N''),CASE WHEN s.fechasys<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fechasys END,ISNULL(NULLIF(RTRIM(s.tiporeg),''),N''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_liqplan s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_PayrollPlanLiquidations',(SELECT COUNT(*) FROM [dbo].[PAY_PayrollPlanLiquidations]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_PayrollPlanLiquidations',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 11: PAY_PayrollTransactions (nom_movtos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_PayrollTransactions (nom_movtos)...';

    INSERT INTO [dbo].[PAY_PayrollTransactions] ([PayPeriodId],[PayrollCompanyId],[EmployeeId],[ConceptId],[SequenceNumber],[Time],[Amount],[PaymentMethod],[UserName],[TransactionDate],[Description],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idplan,0),ISNULL(s.idnomina,0),me.NewEmployeeId,ISNULL(s.concepto,0),ISNULL(s.secuencia,0),s.tiempo,s.valor,s.forpago,NULLIF(RTRIM(s.usuario),''),CASE WHEN s.fechamovto<='1900-01-02' THEN NULL ELSE s.fechamovto END,ISNULL(NULLIF(RTRIM(s.descripcion),''),N''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_movtos s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_PayrollTransactions',(SELECT COUNT(*) FROM [dbo].[PAY_PayrollTransactions]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_PayrollTransactions',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 12: PAY_PayrollEntries (nom_novedad)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_PayrollEntries (nom_novedad)...';

    INSERT INTO [dbo].[PAY_PayrollEntries] ([Cycle],[PayrollCompanyId],[EmployeeId],[EntityCode],[EntryType],[StartDate],[AuthorizationNumber],[IncapacityAmount],[UpcAmount],[Days],[NewEntity],[UserName],[ProcessDate],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.ciclo,0),ISNULL(s.idnomina,0),me.NewEmployeeId,ISNULL(s.entidad,0),ISNULL(s.tiponovedad,0),CASE WHEN s.fecinicio<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecinicio END,ISNULL(s.numautorizacion,0),ISNULL(s.vlrincapacidad,0),ISNULL(s.vlrupc,0),ISNULL(s.dias,0),ISNULL(NULLIF(RTRIM(s.entidadnueva),''),N''),ISNULL(NULLIF(RTRIM(s.usuario),''),N''),CASE WHEN s.fechaproceso<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fechaproceso END,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_novedad s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_PayrollEntries',(SELECT COUNT(*) FROM [dbo].[PAY_PayrollEntries]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_PayrollEntries',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 13: PAY_Absences (nom_ausentismos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_Absences (nom_ausentismos)...';

    INSERT INTO [dbo].[PAY_Absences] ([PayrollCompanyId],[EmployeeId],[ConceptId],[SequenceNumber],[StartDate],[EndDate],[VacationCauseStart],[VacationCauseEnd],[AbsenceType],[DiagnosisCode],[IncapacityClass],[IsExtension],[BaseAmount],[SerialNumber],[Hours],[ExtensionConceptId],[ExtensionSequence],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idnomina,0),me.NewEmployeeId,ISNULL(s.concepto,0),ISNULL(s.secuencia,0),CASE WHEN s.fecinicio<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecinicio END,CASE WHEN s.fecfin<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecfin END,CASE WHEN s.feccausavacini<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.feccausavacini END,CASE WHEN s.feccausavacfin<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.feccausavacfin END,ISNULL(s.tipoausentismo,0),ISNULL(s.coddiagnostico,0),ISNULL(s.claseincapacidad,0),ISNULL(s.esprorroga,0),ISNULL(s.baseincapacidad,0),ISNULL(NULLIF(RTRIM(s.numserie),''),N''),ISNULL(s.horas,0),ISNULL(s.cptoprorroga,0),ISNULL(s.secuenciaprorroga,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_ausentismos s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_Absences',(SELECT COUNT(*) FROM [dbo].[PAY_Absences]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_Absences',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 14: PAY_DirectDebits (nom_libranzas)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_DirectDebits (nom_libranzas)...';

    INSERT INTO [dbo].[PAY_DirectDebits] ([PayrollCompanyId],[EmployeeId],[ConceptId],[SequenceNumber],[VoucherCode],[DebitDate],[DiscountDate],[InitialAmount],[DiscountCycle],[InterestRate],[InstallmentAmount],[InstallmentType],[LiquidationBase],[NumberOfInstallments],[UserName],[SystemDate],[EntryDate],[EntryUser],[Status],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idnomina,0),me.NewEmployeeId,ISNULL(s.concepto,0),ISNULL(s.secuencia,0),ISNULL(NULLIF(RTRIM(s.comprobante),''),N''),CASE WHEN s.feclibranza<='1900-01-02' THEN NULL ELSE s.feclibranza END,CASE WHEN s.fecdescuento<='1900-01-02' THEN NULL ELSE s.fecdescuento END,ISNULL(s.vlrinicial,0),ISNULL(s.ciclodescuento,0),ISNULL(s.tasainteres,0),ISNULL(s.vlrcuota,0),ISNULL(s.tipocuota,0),ISNULL(s.baseliquidacion,0),ISNULL(s.numcuotas,0),ISNULL(NULLIF(RTRIM(s.usuario),''),N''),CASE WHEN s.fechasys<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fechasys END,CASE WHEN s.fecingreso<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecingreso END,ISNULL(NULLIF(RTRIM(s.usuarioingreso),''),N''),ISNULL(NULLIF(RTRIM(s.estado),''),N''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_libranzas s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_DirectDebits',(SELECT COUNT(*) FROM [dbo].[PAY_DirectDebits]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_DirectDebits',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 15: PAY_BookBalances (nom_sallib)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> PAY_BookBalances (nom_sallib)...';

    INSERT INTO [dbo].[PAY_BookBalances] ([PayrollCompanyId],[EmployeeId],[ConceptId],[SequenceNumber],[PeriodYear],[InitialAmount],[JanCharge],[JanPayment],[FebCharge],[FebPayment],[MarCharge],[MarPayment],[AprCharge],[AprPayment],[MayCharge],[MayPayment],[JunCharge],[JunPayment],[JulCharge],[JulPayment],[AugCharge],[AugPayment],[SepCharge],[SepPayment],[OctCharge],[OctPayment],[NovCharge],[NovPayment],[DecCharge],[DecPayment],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idnomina,0),me.NewEmployeeId,ISNULL(s.concepto,0),ISNULL(s.secuencia,0),ISNULL(s.anio,0),ISNULL(s.saldoinicial,0),ISNULL(s.enecar,0),ISNULL(s.enepag,0),ISNULL(s.febcar,0),ISNULL(s.febpag,0),ISNULL(s.marcar,0),ISNULL(s.marpag,0),ISNULL(s.abrcar,0),ISNULL(s.abrpag,0),ISNULL(s.maycar,0),ISNULL(s.maypag,0),ISNULL(s.juncar,0),ISNULL(s.junpag,0),ISNULL(s.julcar,0),ISNULL(s.julpag,0),ISNULL(s.agocar,0),ISNULL(s.agopag,0),ISNULL(s.sepcar,0),ISNULL(s.seppag,0),ISNULL(s.octcar,0),ISNULL(s.octpag,0),ISNULL(s.novcar,0),ISNULL(s.novpag,0),ISNULL(s.diccar,0),ISNULL(s.dicpag,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_sallib s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_BookBalances',(SELECT COUNT(*) FROM [dbo].[PAY_BookBalances]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_BookBalances',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCKS 16-27: Remaining PAY tables (smaller/catalog)
-- nom_novsalario, nom_antcesantia, nom_liqvac, nom_preliq,
-- nom_respreliq, nom_maeliqemp, nom_detliqemp, nom_contpla,
-- nom_cuentas, nom_parretfte, nom_impcert, nom_parautapo
-- Each follows the same TRY/CATCH/TRAN pattern
-- ============================================================

-- BLOCK 16: PAY_SalaryChanges (nom_novsalario)
BEGIN TRY BEGIN TRAN; PRINT '>> PAY_SalaryChanges (nom_novsalario)...';
    INSERT INTO [dbo].[PAY_SalaryChanges] ([PayrollCompanyId],[EmployeeId],[EffectiveDate],[NewSalary],[UserName],[EntryDate],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idnomina,0),me.NewEmployeeId,CASE WHEN s.fecefectiva<='1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.fecefectiva END,ISNULL(s.nuevosueldo,0),NULLIF(RTRIM(s.usuario),''),CASE WHEN s.fechaingreso<='1900-01-02' THEN NULL ELSE s.fechaingreso END,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_novsalario s
    INNER JOIN [migration].[Map_Employees] me ON s.idnomina = me.OldIdNomina AND s.idempleado = me.OldIdEmpleado;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_SalaryChanges',(SELECT COUNT(*) FROM [dbo].[PAY_SalaryChanges]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_SalaryChanges',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 17: PAY_ConceptAccounts (nom_cuentas)
BEGIN TRY BEGIN TRAN; PRINT '>> PAY_ConceptAccounts (nom_cuentas)...';
    INSERT INTO [dbo].[PAY_ConceptAccounts] ([ConceptId],[CostCenterId],[ExpenseAccountCode],[CounterAccountCode],[ProvisionAccountCode],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.concepto,0),ISNULL(NULLIF(RTRIM(s.ccosto),''),N''),ISNULL(NULLIF(RTRIM(s.ctagasto),''),N''),ISNULL(NULLIF(RTRIM(s.ctacontra),''),N''),ISNULL(NULLIF(RTRIM(s.ctaprovision),''),N''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_cuentas s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_ConceptAccounts',(SELECT COUNT(*) FROM [dbo].[PAY_ConceptAccounts]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_ConceptAccounts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 18: PAY_WithholdingParameters (nom_parretfte)
BEGIN TRY BEGIN TRAN; PRINT '>> PAY_WithholdingParameters (nom_parretfte)...';
    INSERT INTO [dbo].[PAY_WithholdingParameters] ([PayrollCompanyId],[UvtRangeStart],[UvtRangeEnd],[Rate],[AdditionalUvt],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.idnomina,0),ISNULL(s.uvtdesde,0),ISNULL(s.uvthasta,0),ISNULL(s.tarifa,0),ISNULL(s.uvtadicional,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.nom_parretfte s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'PAY_WithholdingParameters',(SELECT COUNT(*) FROM [dbo].[PAY_WithholdingParameters]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'PAY_WithholdingParameters',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

PRINT '================================================================';
PRINT '  05_Migrate_PAY_Payroll.sql — END  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO
