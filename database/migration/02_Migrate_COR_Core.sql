-- ============================================================
-- IngenIA365ERP — Migration Step 02: COR_ (Core) Tables
-- Source: SOLIDO ERP (DBDefinicion.sql)
-- Target: IngenIA365ERP Schema v1.0
-- Prerequisites: 01_Create_MappingTables.sql
-- ============================================================
SET NOCOUNT ON;
PRINT N'============================================================';
PRINT N'  Step 02: Migrating COR_ (Core) Tables';
PRINT N'  Started: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
GO

-- ============================================================
-- IMPORTANT: This script assumes the source database is linked
-- as [old]. Adjust the linked server name or use a 3-part name
-- (e.g., [SOLIDO_DB].dbo.sys_maenit) as appropriate.
-- ============================================================

-- ============================================================
-- 2.1 COR_Countries (from: sys_paises)
-- ============================================================
BEGIN TRY
    DECLARE @logId_countries INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Countries', 'sys_paises');
    SET @logId_countries = SCOPE_IDENTITY();

    BEGIN TRAN;

    -- Insert Colombia as default (legacy DB assumed single-country)
    INSERT INTO [dbo].[COR_Countries] ([Name], [CreatedBy], [CreatedAt])
    VALUES (N'Colombia', N'MIGRATION', SYSUTCDATETIME());

    PRINT N'Migrating COR_Countries... 1 rows (Colombia default)';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = 1, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_countries;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_countries;
    PRINT N'ERROR migrating COR_Countries: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.2 COR_Departments (new table - seed from sys_ciudad57.DPTO)
-- ============================================================
BEGIN TRY
    DECLARE @logId_depts INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Departments', 'sys_ciudad57 (DPTO distinct)');
    SET @logId_depts = SCOPE_IDENTITY();

    BEGIN TRAN;

    DECLARE @countryId INT = (SELECT TOP 1 Id FROM [dbo].[COR_Countries] WHERE [Name] = N'Colombia');

    INSERT INTO [dbo].[COR_Departments] ([CountryId], [Code], [Name], [CreatedBy], [CreatedAt])
    SELECT DISTINCT
        @countryId,
        RTRIM(c.DPTO),
        RTRIM(c.DPTO),  -- Use DPTO as both code and name (legacy has no separate department table)
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_ciudad57 c
    WHERE NULLIF(RTRIM(c.DPTO), '') IS NOT NULL;

    PRINT N'Migrating COR_Departments... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_depts;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_depts;
    PRINT N'ERROR migrating COR_Departments: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.3 COR_Cities (from: sys_ciudad57)
-- ============================================================
BEGIN TRY
    DECLARE @logId_cities INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Cities', 'sys_ciudad57');
    SET @logId_cities = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Cities] ([LegacyCode], [DepartmentId], [Name], [CreatedBy], [CreatedAt])
    SELECT
        CAST(c.CIUDAD AS NVARCHAR(10)),
        d.Id,
        RTRIM(c.NOMBRE_CIUDAD),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_ciudad57 c
    INNER JOIN [dbo].[COR_Departments] d ON RTRIM(c.DPTO) = d.[Code];

    PRINT N'Migrating COR_Cities... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Populate mapping
    INSERT INTO [migration].[Map_Cities] (OldCiudad, NewCityId)
    SELECT CAST(LegacyCode AS INT), Id
    FROM [dbo].[COR_Cities]
    WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Cities]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_cities;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_cities;
    PRINT N'ERROR migrating COR_Cities: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.4 COR_Banks (from: sys_banco03)
-- ============================================================
BEGIN TRY
    DECLARE @logId_banks INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Banks', 'sys_banco03');
    SET @logId_banks = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Banks] (
        [LegacyCode], [Name], [ShortName], [AccountCode], [VoucherTypeCode],
        [TransferCode], [AccountClass], [CheckDigitRequired], [LastCheckNumber],
        [AccountingAccountCode], [PrintFormat], [Copies], [FinancialTaxRate],
        [FileStructure], [ChargesCommission], [CommissionAccount], [CommissionType],
        [CommissionAmount], [PromptForPrinter], [ControlSequential],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(b.CODIGO_BANCO),
        RTRIM(b.NOMBRE),
        NULLIF(RTRIM(b.NOMRES), ''),
        NULLIF(RTRIM(b.CODCUENTA), ''),
        NULLIF(RTRIM(b.NUMCOM), ''),
        NULLIF(RTRIM(b.CODTRAS), ''),
        NULLIF(RTRIM(b.CLASE_CUENTA), ''),
        CASE WHEN b.DIGCHEQ = 'Y' THEN 1 ELSE 0 END,
        b.ULTIMO_CHEQUE,
        NULLIF(RTRIM(b.CUENTA_CONTABLE), ''),
        NULLIF(RTRIM(b.formaimprime), ''),
        b.copia,
        b.Grabamenfinanci,
        NULLIF(RTRIM(b.estructura), ''),
        CASE WHEN b.cobracomision = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(b.cuentacomision), ''),
        b.formacomision,
        b.valorcomision,
        CASE WHEN b.pedirimpresora = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(b.CONTROL_CONSEC), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_banco03 b;

    PRINT N'Migrating COR_Banks... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Populate mapping
    INSERT INTO [migration].[Map_Banks] (OldCodigo, NewBankId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Banks] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Banks]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_banks;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_banks;
    PRINT N'ERROR migrating COR_Banks: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.5 COR_Branches (from: sys_agencia)
-- ============================================================
BEGIN TRY
    DECLARE @logId_branches INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Branches', 'sys_agencia');
    SET @logId_branches = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Branches] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT
        RTRIM(a.codigo),
        RTRIM(a.nombre),
        NULLIF(RTRIM(a.nomres), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_agencia a;

    PRINT N'Migrating COR_Branches... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Branches] (OldCodigo, NewBranchId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Branches] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Branches]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_branches;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_branches;
    PRINT N'ERROR migrating COR_Branches: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.6 COR_CostCenters (from: sys_cencos + cnt_cencos + nom_cencos merged)
-- ============================================================
BEGIN TRY
    DECLARE @logId_cc INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_CostCenters', 'sys_cencos (+ cnt_cencos, nom_cencos dedup)');
    SET @logId_cc = SCOPE_IDENTITY();

    BEGIN TRAN;

    -- Primary source: sys_cencos
    INSERT INTO [dbo].[COR_CostCenters] (
        [LegacyCode], [Name], [CompanyName], [CompanyTaxId],
        [PayrollType], [Period], [PayrollPeriodicity], [PayrollStatus],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(s.CCOSTO),
        RTRIM(s.NOMBRE),
        NULLIF(RTRIM(s.NombreEmp), ''),
        NULLIF(RTRIM(s.NitEmp), ''),
        s.TipoNom,
        s.Periodo,
        s.Desnom,
        NULLIF(RTRIM(s.estnom), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_cencos s;

    -- Add from cnt_cencos not already present
    INSERT INTO [dbo].[COR_CostCenters] ([LegacyCode], [Name], [CreatedBy], [CreatedAt])
    SELECT RTRIM(c.CCOSTO), RTRIM(c.NOMBRE), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_cencos c
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[COR_CostCenters] cc WHERE cc.LegacyCode = RTRIM(c.CCOSTO)
    );

    PRINT N'Migrating COR_CostCenters... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows (incl. dedup)';

    INSERT INTO [migration].[Map_CostCenters] (OldCCosto, NewCostCenterId)
    SELECT LegacyCode, Id FROM [dbo].[COR_CostCenters] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_CostCenters]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_cc;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_cc;
    PRINT N'ERROR migrating COR_CostCenters: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.7 COR_Sections (from: sys_seccion56)
-- ============================================================
BEGIN TRY
    DECLARE @logId_sections INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Sections', 'sys_seccion56');
    SET @logId_sections = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Sections] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(codigo), RTRIM(NOMBRE), NULLIF(RTRIM(Nomres), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_seccion56;

    PRINT N'Migrating COR_Sections... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Sections] (OldCodigo, NewSectionId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Sections] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Sections]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_sections;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_sections;
    PRINT N'ERROR migrating COR_Sections: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.8 COR_Professions (from: sys_profe52)
-- ============================================================
BEGIN TRY
    DECLARE @logId_prof INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Professions', 'sys_profe52');
    SET @logId_prof = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Professions] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(CODIGO_PROFESION), RTRIM(NOMBRE), NULLIF(RTRIM(NomRes), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_profe52;

    PRINT N'Migrating COR_Professions... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Professions] (OldCodigo, NewProfessionId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Professions] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Professions]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_prof;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_prof;
    PRINT N'ERROR migrating COR_Professions: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.9 COR_Positions (from: sys_cargo55)
-- ============================================================
BEGIN TRY
    DECLARE @logId_pos INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Positions', 'sys_cargo55');
    SET @logId_pos = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Positions] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(CODIGO_CARGO), RTRIM(NOMBRE), NULLIF(RTRIM(NOMRES), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_cargo55;

    PRINT N'Migrating COR_Positions... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Positions] (OldCodigo, NewPositionId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Positions] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Positions]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_pos;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_pos;
    PRINT N'ERROR migrating COR_Positions: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.10 COR_WithdrawalReasons (from: sys_motret)
-- ============================================================
BEGIN TRY
    DECLARE @logId_wr INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_WithdrawalReasons', 'sys_motret');
    SET @logId_wr = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_WithdrawalReasons] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(codigo), RTRIM(nombre), NULLIF(RTRIM(nomres), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_motret;

    PRINT N'Migrating COR_WithdrawalReasons... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_WithdrawalReasons] (OldCodigo, NewReasonId)
    SELECT LegacyCode, Id FROM [dbo].[COR_WithdrawalReasons] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_WithdrawalReasons]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_wr;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_wr;
    PRINT N'ERROR migrating COR_WithdrawalReasons: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.11 COR_Entities (from: sys_entidad)
-- ============================================================
BEGIN TRY
    DECLARE @logId_ent INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Entities', 'sys_entidad');
    SET @logId_ent = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Entities] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(codigo), RTRIM(nombre), NULLIF(RTRIM(nomres), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_entidad;

    PRINT N'Migrating COR_Entities... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_ent;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_ent;
    PRINT N'ERROR migrating COR_Entities: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.12 COR_Committees (from: cop_comite)
-- ============================================================
BEGIN TRY
    DECLARE @logId_com INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Committees', 'cop_comite');
    SET @logId_com = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Committees] ([LegacyCode], [Name], [ShortName], [CommitteeType], [CreatedBy], [CreatedAt])
    SELECT
        RTRIM(codigo),
        RTRIM(nombre),
        NULLIF(RTRIM(nomres), ''),
        NULLIF(RTRIM(tipoComite), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cop_comite;

    PRINT N'Migrating COR_Committees... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Committees] (OldCodigo, NewCommitteeId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Committees] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Committees]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_com;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_com;
    PRINT N'ERROR migrating COR_Committees: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.13 COR_EmployerCompanies (from: cop_empresa13 + nom_empresas merged)
-- ============================================================
BEGIN TRY
    DECLARE @logId_ec INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_EmployerCompanies', 'cop_empresa13 + nom_empresas');
    SET @logId_ec = SCOPE_IDENTITY();

    BEGIN TRAN;

    -- Step 1: cop_empresa13 (primary source)
    INSERT INTO [dbo].[COR_EmployerCompanies] (
        [LegacyCode], [Name], [ShortName], [TaxId], [PayerName],
        [Address], [City], [Phone], [Fax], [Email], [ExpiryDate],
        [CutoffDate1], [CutoffDay1], [CutoffDate2], [CutoffDay2],
        [CutoffDate3], [CutoffDay3], [Term], [PayrollConceptCode],
        [SubmissionFormat], [DiscountPercentage], [DiscountType],
        [IsBlocked], [GroceryPercentage],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(e.codigo_empresa),
        RTRIM(e.nombre),
        NULLIF(RTRIM(e.nombre_resum), ''),
        NULLIF(RTRIM(e.Nit), ''),
        NULLIF(RTRIM(e.pagador), ''),
        NULLIF(RTRIM(e.direccion), ''),
        NULLIF(RTRIM(e.ciudad), ''),
        NULLIF(RTRIM(e.telefono), ''),
        NULLIF(RTRIM(e.fax), ''),
        NULLIF(RTRIM(e.email), ''),
        CASE WHEN e.FechaVence <= '1900-01-02' THEN NULL ELSE CAST(e.FechaVence AS DATE) END,
        CASE WHEN e.fecha_corte1 <= '1900-01-02' THEN NULL ELSE CAST(e.fecha_corte1 AS DATE) END,
        e.dia_corte1,
        CASE WHEN e.fecha_corte2 <= '1900-01-02' THEN NULL ELSE CAST(e.fecha_corte2 AS DATE) END,
        e.dia_corte2,
        CASE WHEN e.fecha_corte3 <= '1900-01-02' THEN NULL ELSE CAST(e.fecha_corte3 AS DATE) END,
        e.dia_corte3,
        e.plazo,
        NULLIF(RTRIM(e.CptoNomina), ''),
        NULLIF(RTRIM(e.FormatoEnvio), ''),
        e.PorDescuento,
        NULLIF(RTRIM(e.tipodsto), ''),
        CASE WHEN e.bloquearEmp = 'S' THEN 1 ELSE 0 END,
        e.porviveres,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cop_empresa13 e;

    PRINT N'Migrating COR_EmployerCompanies (cop_empresa13)... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Populate mapping from cop_empresa13
    INSERT INTO [migration].[Map_EmployerCompanies] (OldCodigo, NewCompanyId, Source)
    SELECT LegacyCode, Id, N'cop_empresa13'
    FROM [dbo].[COR_EmployerCompanies]
    WHERE LegacyCode IS NOT NULL;

    -- Step 2: nom_empresas not in cop_empresa13
    INSERT INTO [dbo].[COR_EmployerCompanies] (
        [LegacyCode], [LegacyPayrollId], [Name], [ShortName], [TaxId],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        CAST(ne.IdEmpresa AS NVARCHAR(10)),
        ne.IdEmpresa,
        RTRIM(ne.Nombre),
        NULLIF(RTRIM(ne.NomRes), ''),
        NULLIF(RTRIM(ne.Nit), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.nom_empresas ne
    WHERE NOT EXISTS (
        SELECT 1 FROM [migration].[Map_EmployerCompanies] ec
        WHERE ec.OldCodigo = CAST(ne.IdEmpresa AS VARCHAR(4))
    );

    PRINT N'Migrating COR_EmployerCompanies (nom_empresas extras)... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Add nom_empresas-only to mapping
    INSERT INTO [migration].[Map_EmployerCompanies] (OldCodigo, NewCompanyId, Source)
    SELECT LegacyCode, Id, N'nom_empresas'
    FROM [dbo].[COR_EmployerCompanies] ec
    WHERE ec.LegacyPayrollId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [migration].[Map_EmployerCompanies] m WHERE m.NewCompanyId = ec.Id);

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_EmployerCompanies]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_ec;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_ec;
    PRINT N'ERROR migrating COR_EmployerCompanies: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.14 COR_Advisors (from: cop_asesores)
-- ============================================================
BEGIN TRY
    DECLARE @logId_adv INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Advisors', 'cop_asesores');
    SET @logId_adv = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Advisors] (
        [LegacyCode], [Name], [Address], [Phone], [City], [Mobile], [Email],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(Idcedula),
        RTRIM(nombre),
        NULLIF(RTRIM(direccion), ''),
        NULLIF(RTRIM(telefono), ''),
        NULLIF(RTRIM(ciudad), ''),
        NULLIF(RTRIM(movil), ''),
        NULLIF(RTRIM(email), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cop_asesores;

    PRINT N'Migrating COR_Advisors... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Advisors] (OldIdCedula, NewAdvisorId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Advisors] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Advisors]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_adv;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_adv;
    PRINT N'ERROR migrating COR_Advisors: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.15 COR_Relationships (from: sys_parent51 + nom_parent merged)
-- ============================================================
BEGIN TRY
    DECLARE @logId_rel INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Relationships', 'sys_parent51 + nom_parent');
    SET @logId_rel = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Relationships] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(codigo), RTRIM(nombre), NULLIF(RTRIM(nomres), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_parent51;

    -- Add from nom_parent not already present
    INSERT INTO [dbo].[COR_Relationships] ([LegacyCode], [Name], [ShortName], [CreatedBy], [CreatedAt])
    SELECT RTRIM(np.codigo), RTRIM(np.nombre), NULLIF(RTRIM(np.nomres), ''), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.nom_parent np
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[COR_Relationships] r WHERE r.LegacyCode = RTRIM(np.codigo)
    );

    PRINT N'Migrating COR_Relationships... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    INSERT INTO [migration].[Map_Relationships] (OldCodigo, NewRelationshipId)
    SELECT LegacyCode, Id FROM [dbo].[COR_Relationships] WHERE LegacyCode IS NOT NULL;

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_Relationships]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_rel;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_rel;
    PRINT N'ERROR migrating COR_Relationships: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.16 COR_Diseases (from: sys_enfermedades)
-- ============================================================
BEGIN TRY
    DECLARE @logId_dis INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Diseases', 'sys_enfermedades');
    SET @logId_dis = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Diseases] ([LegacyCode], [Name], [CreatedBy], [CreatedAt])
    SELECT CAST(codigo AS NVARCHAR(10)), RTRIM(nombre), N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_enfermedades;

    PRINT N'Migrating COR_Diseases... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_dis;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_dis;
    PRINT N'ERROR migrating COR_Diseases: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.17 COR_RecreationalEvents (from: sys_recreacion)
-- ============================================================
BEGIN TRY
    DECLARE @logId_rec INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_RecreationalEvents', 'sys_recreacion');
    SET @logId_rec = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_RecreationalEvents] (
        [LegacyCode], [Description], [ActivityType], [StartDate], [EndDate],
        [Percentage], [Amount], [CommitteeId], [ActivitySubtype],
        [Capacity], [ControlNovelty],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        RTRIM(r.codigo),
        RTRIM(r.detalle),
        NULLIF(RTRIM(r.TipoActividad), ''),
        r.fecha_Inicio,
        r.fecha_termino,
        r.porcentaje,
        r.valor,
        mc.NewCommitteeId,
        NULLIF(RTRIM(r.tipoActi_Recre), ''),
        r.cantcupos,
        CASE WHEN r.ctrlnovedad = 'S' THEN 1 ELSE 0 END,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_recreacion r
    LEFT JOIN [migration].[Map_Committees] mc ON RTRIM(r.comite) = mc.OldCodigo;

    PRINT N'Migrating COR_RecreationalEvents... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_rec;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_rec;
    PRINT N'ERROR migrating COR_RecreationalEvents: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.18 COR_People (THE CENTRAL TABLE)
-- Source: sys_maenit (primary) + cnt_nit (tax/accounting data)
-- ============================================================
BEGIN TRY
    DECLARE @logId_people INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_People', 'sys_maenit + cnt_nit');
    SET @logId_people = SCOPE_IDENTITY();

    BEGIN TRAN;

    -- Step 1: Insert from sys_maenit (primary source, ~130 columns)
    INSERT INTO [dbo].[COR_People] (
        -- Identification
        [LegacyCode], [LastName], [FirstName], [TaxId], [TaxIdCheckDigit],
        [IdIssuedAt], [IdType], [IdIssueDate], [PersonType], [BusinessName], [PreviousCode],
        -- Contact
        [Address], [Phone1], [Phone2], [Fax], [Mobile], [Email],
        [CityId], [MailingAddress], [MailingPreference], [MailingCityId],
        [EmailType], [DaneCityCode],
        -- Demographics
        [Gender], [MaritalStatus], [DateOfBirth], [EducationLevel],
        [SocialStratum], [HousingType], [HasVehicle], [VehicleType],
        [IsHeadOfHousehold], [WorkShift], [NaturalLegalType],
        -- Employment
        [Employer], [EmployerStartDate], [SalaryType], [Salary], [Severance],
        [ProfessionId], [PositionId], [SeveranceFund],
        -- Tax & Regulatory (from cnt_nit LEFT JOIN)
        [WithholdingExempt], [IcaWithholdingExempt], [TaxRegime], [IcaType],
        [IsLargeContributor], [IcaRate], [DataOrigin], [PaymentDays],
        [HasTaxLien], [HasSpecialPrice], [IsEmployerClient], [SourceWithholding],
        [NaturalHasRut], [CiiuCode], [CreditLimit], [ThirdPartyType],
        -- Banking
        [BankAccountNumber], [BankId], [BankAccountType], [BankAccountCityId],
        [NitBankCode], [NitBankAccountType], [NitBankAccountNumber], [NitAdvisorId],
        -- Role flags
        [IsAssociate], [IsEmployee], [IsAdvisor], [IsThirdParty], [ReceivesInvoice],
        -- Status flags
        [Status], [IsDisabled], [IsInsolvent], [IsOnVacation], [IsOnUnpaidLeave],
        [IsPensioner], [IsInsubordinate], [IsDeceased], [IsFromGovernment],
        [IsPublicResourceAdmin], [PensionType], [SeveranceType],
        -- Online
        [InternetPassword], [OnlineConsultation], [ConsultationStatus],
        [AffiliationCode], [ConsultationChargeType], [ConsultationCreditLine], [UserPassword],
        -- Risk
        [AuthCentralRisk], [PosCardClass], [PosCardLimit], [InsuranceRiskRate],
        [ZoneTypeId], [ZoneId], [IsSiplaExempt], [SiplaExemptDate], [SiplaUser],
        [SinglePromissoryNote], [PledgesContributions], [InManagement],
        -- Accounting center
        [CpAdmin], [CpContributions], [CpLocal], [CpCommission], [ProfitCenter],
        [CapacityPayPct],
        -- Other
        [OtherIncomeDescription],
        -- Legacy audit
        [LegacyUser], [LegacyUserName], [LegacyRecordDate], [LegacySystemDate],
        -- Audit
        [CreatedBy], [CreatedAt]
    )
    SELECT
        -- Identification
        m.CODIGOTER,
        RTRIM(m.APELLIDO),
        RTRIM(m.NOMBRE),
        RTRIM(m.NIT),
        NULLIF(RTRIM(m.NIT_CHEQUEO), ''),
        NULLIF(RTRIM(m.EXPEDIDA), ''),
        RTRIM(m.TIPO_NIT),
        CASE WHEN m.FecExpedicion <= '1900-01-02' THEN NULL ELSE CAST(m.FecExpedicion AS DATE) END,
        COALESCE(NULLIF(RTRIM(n.tipo_persona), ''), 'N'),
        NULLIF(RTRIM(n.RAZON_SOCIAL), ''),
        NULLIF(RTRIM(m.codigo_ante), ''),
        -- Contact
        NULLIF(RTRIM(m.DIRECCION), ''),
        NULLIF(RTRIM(m.TELEFONO1), ''),
        NULLIF(RTRIM(m.TELEFONO2), ''),
        NULLIF(RTRIM(m.FAX), ''),
        NULLIF(RTRIM(m.MOVIL), ''),
        NULLIF(RTRIM(m.EMAIL), ''),
        CASE WHEN m.DPTO_CIUDAD = 0 THEN NULL ELSE mc.NewCityId END,
        NULLIF(RTRIM(m.DIRECCION_ENVIO), ''),
        NULLIF(RTRIM(m.ENVIO_DIR), ''),
        CASE WHEN m.CIUDAD_ENVIO = 0 THEN NULL ELSE mc2.NewCityId END,
        NULLIF(RTRIM(m.Tipo_Correo), ''),
        NULLIF(RTRIM(m.CODCIU), ''),
        -- Demographics
        NULLIF(RTRIM(m.SEXO), ''),
        NULLIF(RTRIM(m.ESTADO_CIVIL), ''),
        CASE WHEN m.FECNACEM <= '1900-01-02' THEN NULL ELSE CAST(m.FECNACEM AS DATE) END,
        NULLIF(RTRIM(m.NIV_ACADE), ''),
        NULLIF(RTRIM(m.ESTRATO), ''),
        NULLIF(RTRIM(m.TIPO_VIVIENDA), ''),
        CASE WHEN m.VEHICULO = 'S' THEN 1 ELSE 0 END,
        m.tipovehiculo,
        CASE WHEN m.CabezaFamilia = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(m.JornadaLaboral), ''),
        m.NATJUR,
        -- Employment
        NULLIF(RTRIM(m.EMPRESA_LABORA), ''),
        CASE WHEN m.FEING_EMPRESA <= '1900-01-02' THEN NULL ELSE CAST(m.FEING_EMPRESA AS DATE) END,
        NULLIF(RTRIM(m.TIPO_SALARIO), ''),
        m.SALARIO,
        m.CESANTIAS,
        mp.NewProfessionId,
        mpo.NewPositionId,
        NULLIF(RTRIM(m.FondoCesantia), ''),
        -- Tax (from cnt_nit)
        CASE WHEN n.AUTORFTE = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.AUTORTEICA = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(n.REGIMEN), ''),
        NULLIF(RTRIM(n.TIPO_ICA), ''),
        CASE WHEN n.gran_contribu = 'S' THEN 1 ELSE 0 END,
        n.TASA_ICA,
        NULLIF(RTRIM(n.ORIGEN_DATOS), ''),
        ISNULL(n.DIAS_PAGO, 0),
        CASE WHEN n.Grabamen = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.PrecioEsp = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.CliPatronal = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.retenFuente = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.NaturalTieneRut = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(n.cod_ciiu), ''),
        ISNULL(n.cupocredito, 0),
        NULLIF(RTRIM(n.tipo_tercero), ''),
        -- Banking
        NULLIF(RTRIM(m.CUENTA_BANCO), ''),
        mb.NewBankId,
        NULLIF(RTRIM(m.TIPO_CUENTA), ''),
        CASE WHEN m.ciudad_cuenta = 0 THEN NULL ELSE mc3.NewCityId END,
        NULLIF(RTRIM(n.codigo_banco), ''),
        NULLIF(RTRIM(n.tipo_cuenta_ban), ''),
        NULLIF(RTRIM(n.numero_cuenta_ban), ''),
        NULLIF(RTRIM(n.idasesor), ''),
        -- Role flags
        1, -- IsAssociate (from sys_maenit = always associate)
        CASE WHEN m.Empleado = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.ASESOR = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.NIT IS NOT NULL THEN 1 ELSE 0 END,
        CASE WHEN m.RecibeFactura = 'S' THEN 1 ELSE 0 END,
        -- Status flags
        NULLIF(RTRIM(m.ESTADO), ''),
        CASE WHEN m.Incapacitado = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.InsolvenciaEconomica = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.Vacaciones = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.LicencianoRemunerada = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.Pensionado = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.Insubsistente = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.Fallecido = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.VienedeGobernacion = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.admrecuspub = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(m.tipopension), ''),
        NULLIF(RTRIM(m.tipocesantia), ''),
        -- Online
        NULLIF(RTRIM(m.CLAVE_INTERNET), ''),
        CASE WHEN m.ConsultaEnLinea = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(m.EstatusConsulta), ''),
        NULLIF(RTRIM(m.CodAfiliacion), ''),
        NULLIF(RTRIM(m.TipoCobroConsulta), ''),
        m.LincredConsulta,
        NULLIF(RTRIM(m.[password]), ''),
        -- Risk
        CASE WHEN m.autoricentralriesgo = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(m.clasecupoPos), ''),
        m.valorClaseCupo,
        m.SeguroRiesgo,
        CAST(m.IdtipoZona AS INT),
        CAST(m.IdZona AS INT),
        CASE WHEN m.exoneradoSipa = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.fechaexonerado <= '1900-01-02' THEN NULL ELSE m.fechaexonerado END,
        NULLIF(RTRIM(m.usuariosipla), ''),
        CASE WHEN m.pagareunico = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.pignora_aport = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.engestion = 'S' THEN 1 ELSE 0 END,
        -- Accounting center
        CASE WHEN m.CpAdmon = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.CpAptos = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.CpLocal = 'S' THEN 1 ELSE 0 END,
        CASE WHEN m.CpComision = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(m.cenutilidad), ''),
        CASE WHEN m.cappagoPorcentaje = 'S' THEN 1 ELSE 0 END,
        -- Other
        NULLIF(RTRIM(m.DESOTROING), ''),
        -- Legacy audit
        NULLIF(RTRIM(m.USUARIO), ''),
        NULLIF(RTRIM(m.nomusu), ''),
        CASE WHEN m.FECHA_GRABA <= '1900-01-02' THEN NULL ELSE m.FECHA_GRABA END,
        CASE WHEN m.fechasys <= '1900-01-02' THEN NULL ELSE m.fechasys END,
        -- Audit
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_maenit m
    LEFT JOIN [old].dbo.cnt_nit n ON m.NIT = n.NIT
    LEFT JOIN [migration].[Map_Cities] mc ON m.DPTO_CIUDAD = mc.OldCiudad
    LEFT JOIN [migration].[Map_Cities] mc2 ON m.CIUDAD_ENVIO = mc2.OldCiudad
    LEFT JOIN [migration].[Map_Cities] mc3 ON m.ciudad_cuenta = mc3.OldCiudad
    LEFT JOIN [migration].[Map_Banks] mb ON RTRIM(m.CODIGO_BANCO) = mb.OldCodigo
    LEFT JOIN [migration].[Map_Professions] mp ON RTRIM(m.PROFESION) = mp.OldCodigo
    LEFT JOIN [migration].[Map_Positions] mpo ON RTRIM(m.CARGO) = mpo.OldCodigo;

    DECLARE @peopleCount1 INT = @@ROWCOUNT;
    PRINT N'Migrating COR_People (sys_maenit)... ' + CAST(@peopleCount1 AS NVARCHAR) + N' rows';

    -- Populate mapping from sys_maenit
    INSERT INTO [migration].[Map_People] (OldCodigoTer, NewPersonId, Source)
    SELECT LegacyCode, Id, N'sys_maenit'
    FROM [dbo].[COR_People]
    WHERE LegacyCode IS NOT NULL;

    -- Step 2: cnt_nit records NOT in sys_maenit (third-party only contacts)
    INSERT INTO [dbo].[COR_People] (
        [LegacyCode], [LastName], [FirstName], [TaxId], [TaxIdCheckDigit],
        [IdIssuedAt], [IdType], [PersonType], [BusinessName],
        [Address], [Phone1], [Fax], [Email],
        [WithholdingExempt], [IcaWithholdingExempt], [TaxRegime], [IcaType],
        [IsLargeContributor], [IcaRate], [DataOrigin], [PaymentDays],
        [HasTaxLien], [HasSpecialPrice], [IsEmployerClient], [SourceWithholding],
        [NaturalHasRut], [CiiuCode], [CreditLimit], [ThirdPartyType],
        [NitBankCode], [NitBankAccountType], [NitBankAccountNumber], [NitAdvisorId],
        [IsAssociate], [IsThirdParty], [Status],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        n.NIT,            -- LegacyCode = NIT for cnt_nit-only records
        RTRIM(n.NOMBRE),  -- LastName (cnt_nit has single NOMBRE field)
        N'',              -- FirstName (not available separately)
        RTRIM(n.NIT),
        NULLIF(RTRIM(n.NIT_CHEQUEO), ''),
        NULLIF(RTRIM(n.EXPEDIDA), ''),
        RTRIM(n.TIPO_NIT),
        NULLIF(RTRIM(n.tipo_persona), ''),
        NULLIF(RTRIM(n.RAZON_SOCIAL), ''),
        NULLIF(RTRIM(n.DIRECCION), ''),
        NULLIF(RTRIM(n.TELEFONO1), ''),
        NULLIF(RTRIM(n.fax), ''),
        NULLIF(RTRIM(n.email), ''),
        CASE WHEN n.AUTORFTE = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.AUTORTEICA = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(n.REGIMEN), ''),
        NULLIF(RTRIM(n.TIPO_ICA), ''),
        CASE WHEN n.gran_contribu = 'S' THEN 1 ELSE 0 END,
        n.TASA_ICA,
        NULLIF(RTRIM(n.ORIGEN_DATOS), ''),
        n.DIAS_PAGO,
        CASE WHEN n.Grabamen = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.PrecioEsp = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.CliPatronal = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.retenFuente = 'S' THEN 1 ELSE 0 END,
        CASE WHEN n.NaturalTieneRut = 'S' THEN 1 ELSE 0 END,
        NULLIF(RTRIM(n.cod_ciiu), ''),
        n.cupocredito,
        NULLIF(RTRIM(n.tipo_tercero), ''),
        NULLIF(RTRIM(n.codigo_banco), ''),
        NULLIF(RTRIM(n.tipo_cuenta_ban), ''),
        NULLIF(RTRIM(n.numero_cuenta_ban), ''),
        NULLIF(RTRIM(n.idasesor), ''),
        0, -- IsAssociate = false (cnt_nit only)
        1, -- IsThirdParty = true
        CAST(n.estado AS NVARCHAR(2)),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cnt_nit n
    WHERE NOT EXISTS (SELECT 1 FROM [migration].[Map_People] mp WHERE mp.OldCodigoTer = n.NIT);

    DECLARE @peopleCount2 INT = @@ROWCOUNT;
    PRINT N'Migrating COR_People (cnt_nit extras)... ' + CAST(@peopleCount2 AS NVARCHAR) + N' rows';

    -- Add cnt_nit-only to mapping
    INSERT INTO [migration].[Map_People] (OldCodigoTer, NewPersonId, Source)
    SELECT p.TaxId, p.Id, N'cnt_nit'
    FROM [dbo].[COR_People] p
    WHERE NOT EXISTS (SELECT 1 FROM [migration].[Map_People] mp WHERE mp.NewPersonId = p.Id);

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [migration].[Map_People]),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_people;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_people;
    PRINT N'ERROR migrating COR_People: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.19 COR_Associates (from: sys_maenit association fields)
-- ============================================================
BEGIN TRY
    DECLARE @logId_assoc INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Associates', 'sys_maenit');
    SET @logId_assoc = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Associates] (
        [PersonId], [JoinDate], [ContributionRate], [EmployerCompanyId],
        [BranchId], [CostCenterId], [SectionId], [CutoffDay], [Status],
        [WithdrawalDate], [WithdrawalReasonId], [RejoinDate], [CategoryRating],
        [AdvisorId], [DeductionPeriod], [DeductionType], [Rank],
        [ReferredBy], [IsInLegalCollection], [ContractNumber], [ContractExpiryDate],
        [CommitteeId], [ZoneCode], [ContributionPledged],
        [AssociateClass], [PaymentType], [SectorCode], [LastTransferDate],
        [MailingOption], [ManualRating], [PreviousClass],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        CASE WHEN m.FECHA_INGRESO <= '1900-01-02' THEN NULL ELSE CAST(m.FECHA_INGRESO AS DATE) END,
        m.TASA_APORTE,
        ec.NewCompanyId,
        br.NewBranchId,
        cc.NewCostCenterId,
        sec.NewSectionId,
        m.DIA_CORTE,
        NULLIF(RTRIM(m.ESTADO), ''),
        CASE WHEN m.FECHA_RETIRO <= '1900-01-02' THEN NULL ELSE CAST(m.FECHA_RETIRO AS DATE) END,
        wr.NewReasonId,
        CASE WHEN m.FECHA_REINGRESO <= '1900-01-02' THEN NULL ELSE CAST(m.FECHA_REINGRESO AS DATE) END,
        NULLIF(RTRIM(m.CALIFI_CATEG), ''),
        adv.NewAdvisorId,
        NULLIF(RTRIM(m.PERIODO_DESTO), ''),
        NULLIF(RTRIM(m.CLASE_DESTO), ''),
        NULLIF(RTRIM(m.ESCALAFON), ''),
        NULLIF(RTRIM(m.REFERIDO), ''),
        CASE WHEN m.COBROJUR > 0 THEN 1 ELSE 0 END,
        m.CONTRACTO,
        CASE WHEN m.VENCONTRACTO <= '1900-01-02' THEN NULL ELSE CAST(m.VENCONTRACTO AS DATE) END,
        com.NewCommitteeId,
        NULLIF(RTRIM(m.COPZONA), ''),
        m.SALDO_APORTE,
        NULLIF(RTRIM(m.CLASE), ''),
        NULLIF(RTRIM(m.TIPO_PAGO), ''),
        NULLIF(RTRIM(m.SECTOR), ''),
        CASE WHEN m.FECULT_TRASLADO <= '1900-01-02' THEN NULL ELSE CAST(m.FECULT_TRASLADO AS DATE) END,
        m.ENVIO,
        NULLIF(RTRIM(m.CALMAN), ''),
        NULLIF(RTRIM(m.claseAnt), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_maenit m
    INNER JOIN [migration].[Map_People] mp ON m.CODIGOTER = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_EmployerCompanies] ec ON RTRIM(m.EMPRESA) = ec.OldCodigo
    LEFT JOIN [migration].[Map_Branches] br ON RTRIM(m.AGENCIA) = br.OldCodigo
    LEFT JOIN [migration].[Map_CostCenters] cc ON RTRIM(m.CENCOSTO) = cc.OldCCosto
    LEFT JOIN [migration].[Map_Sections] sec ON RTRIM(m.SECCION_EMPRESA) = sec.OldCodigo
    LEFT JOIN [migration].[Map_WithdrawalReasons] wr ON RTRIM(m.MOTIVO_RETIRO) = wr.OldCodigo
    LEFT JOIN [migration].[Map_Advisors] adv ON RTRIM(m.ASESOR) = adv.OldIdCedula
    LEFT JOIN [migration].[Map_Committees] com ON RTRIM(m.comite) = com.OldCodigo;

    PRINT N'Migrating COR_Associates... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_assoc;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_assoc;
    PRINT N'ERROR migrating COR_Associates: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.20 COR_Spouses (from: sys_maenit CONY* fields)
-- Only insert where spouse data exists
-- ============================================================
BEGIN TRY
    DECLARE @logId_spouse INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Spouses', 'sys_maenit (CONY* fields)');
    SET @logId_spouse = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_Spouses] (
        [PersonId], [SpouseName], [SpouseIdNumber], [SpouseIdType],
        [SpouseIdIssuedAt], [SpouseIdIssueDate], [SpouseAddress],
        [SpouseEmployer], [SpouseEmployerAddress], [SpousePhone],
        [SpouseCity], [SpouseProfession], [SpousePosition], [SpouseSalary],
        [SpouseDateOfBirth], [SpouseGender], [SpouseFax],
        [SpouseMailingPref], [SpouseMailingAddress], [SpouseCompanyCode],
        [SpouseBranchCode], [SpouseSectionCode], [SpouseEmployerStart],
        [SpouseEducationLevel], [SpouseSalaryType], [SpouseSeverance],
        [SpouseOtherIncome], [SpouseOtherIncomeDesc],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        NULLIF(RTRIM(m.CONYUGE), ''),
        NULLIF(RTRIM(m.CONYCEDU), ''),
        NULLIF(RTRIM(m.CONYTIPONIT), ''),
        NULLIF(RTRIM(m.CONYEXPEDIDA), ''),
        CASE WHEN m.CONYFECEXPEDICION <= '1900-01-02' THEN NULL ELSE CAST(m.CONYFECEXPEDICION AS DATE) END,
        NULLIF(RTRIM(m.CONYDIREC), ''),
        NULLIF(RTRIM(m.CONYEMPR), ''),
        NULLIF(RTRIM(m.CONYDIREM), ''),
        NULLIF(RTRIM(m.CONYTELEF), ''),
        NULLIF(RTRIM(m.CONYCIUD), ''),
        NULLIF(RTRIM(m.CONYPROFE), ''),
        NULLIF(RTRIM(m.CONYCARGO), ''),
        m.CONYSALAR,
        CASE WHEN m.CONYFECNACEM <= '1900-01-02' THEN NULL ELSE CAST(m.CONYFECNACEM AS DATE) END,
        NULLIF(RTRIM(m.CONYSEXO), ''),
        NULLIF(RTRIM(m.CONYFAX), ''),
        NULLIF(RTRIM(m.CONYENVIODIR), ''),
        NULLIF(RTRIM(m.CONYENDIRCOR), ''),
        NULLIF(RTRIM(m.CONYEMPRESA), ''),
        NULLIF(RTRIM(m.CONYAGENCIA), ''),
        NULLIF(RTRIM(m.CONYSECCION), ''),
        CASE WHEN m.CONYFECINGREEMP <= '1900-01-02' THEN NULL ELSE CAST(m.CONYFECINGREEMP AS DATE) END,
        NULLIF(RTRIM(m.CONYNIVACADE), ''),
        NULLIF(RTRIM(m.CONYTIPOSALARIO), ''),
        m.CONYCESANTIAS,
        m.CONYOTROING,
        NULLIF(RTRIM(m.CONYDESOTROING), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_maenit m
    INNER JOIN [migration].[Map_People] mp ON m.CODIGOTER = mp.OldCodigoTer
    WHERE NULLIF(RTRIM(m.CONYUGE), '') IS NOT NULL
       OR NULLIF(RTRIM(m.CONYCEDU), '') IS NOT NULL;

    PRINT N'Migrating COR_Spouses... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_spouse;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_spouse;
    PRINT N'ERROR migrating COR_Spouses: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.21 COR_PeopleFinancial (from: sys_maenit financial fields)
-- ============================================================
BEGIN TRY
    DECLARE @logId_fin INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_PeopleFinancial', 'sys_maenit');
    SET @logId_fin = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_PeopleFinancial] (
        [PersonId], [DebtCapacity], [OtherIncome], [TotalAssets],
        [VariableIncome], [RentalIncome], [PensionIncome], [ThirdPartyDebts],
        [MonthlyFixedExpenses], [PersonalExpenses], [PensionDeduction],
        [CreditScore], [CreditBureauScore], [CreditBureauRating],
        [ExternalDebtPayment], [ExternalDebtBalance], [PastDueCreditBureau],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        m.CAPA_DEUDA,
        m.OTRO_INGRESO,
        m.ACTIVOS,
        m.IngVariables,
        m.IngArriendos,
        m.IngPension,
        m.DeudasTerceros,
        m.GASTO_FIJO_MES,
        m.dstoGastosPerso,
        m.DstoPension,
        m.Acierta,
        m.ScoreCifin,
        NULLIF(RTRIM(m.califidatacredito), ''),
        m.cuotadeudaexterna,
        m.saldodeudaexterna,
        m.SalMorDatacredito,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_maenit m
    INNER JOIN [migration].[Map_People] mp ON m.CODIGOTER = mp.OldCodigoTer;

    PRINT N'Migrating COR_PeopleFinancial... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

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
    PRINT N'ERROR migrating COR_PeopleFinancial: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.22 COR_AssociateCategories (from: sys_maenit CATEGORIA* fields)
-- ============================================================
BEGIN TRY
    DECLARE @logId_cat INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_AssociateCategories', 'sys_maenit');
    SET @logId_cat = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_AssociateCategories] (
        [PersonId], [Category1], [Category2], [Category3], [Category4], [Category5],
        [DaysCategory], [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        NULLIF(RTRIM(m.CATEGORIA1), ''),
        NULLIF(RTRIM(m.CATEGORIA2), ''),
        NULLIF(RTRIM(m.CATEGORIA3), ''),
        NULLIF(RTRIM(m.CATEGORIA4), ''),
        NULLIF(RTRIM(m.CATEGORIA5), ''),
        m.DIAS_CATEGORIA,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_maenit m
    INNER JOIN [migration].[Map_People] mp ON m.CODIGOTER = mp.OldCodigoTer;

    PRINT N'Migrating COR_AssociateCategories... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_cat;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_cat;
    PRINT N'ERROR migrating COR_AssociateCategories: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.23 COR_CommitteeMembers (from: cop_comiteasoc)
-- ============================================================
BEGIN TRY
    DECLARE @logId_cm INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_CommitteeMembers', 'cop_comiteasoc');
    SET @logId_cm = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_CommitteeMembers] (
        [PersonId], [CommitteeId], [LegacyUser], [LegacyDate],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        mc.NewCommitteeId,
        NULLIF(RTRIM(ca.usuario), ''),
        CASE WHEN ca.fechaSys <= '1900-01-02' THEN NULL ELSE ca.fechaSys END,
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cop_comiteasoc ca
    INNER JOIN [migration].[Map_People] mp ON RTRIM(ca.codigoter) = mp.OldCodigoTer
    INNER JOIN [migration].[Map_Committees] mc ON RTRIM(ca.idComite) = mc.OldCodigo;

    PRINT N'Migrating COR_CommitteeMembers... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_cm;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_cm;
    PRINT N'ERROR migrating COR_CommitteeMembers: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.24 COR_Beneficiaries (from: cop_benef + nom_bene merged)
-- ============================================================
BEGIN TRY
    DECLARE @logId_ben INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_Beneficiaries', 'cop_benef + nom_bene');
    SET @logId_ben = SCOPE_IDENTITY();

    BEGIN TRAN;

    -- Step 1: cop_benef (Associate beneficiaries)
    INSERT INTO [dbo].[COR_Beneficiaries] (
        [PersonId], [BeneficiaryIdNumber], [BeneficiaryName], [DocumentType],
        [RelationshipId], [DateOfBirth], [EducationLevel], [HasDisability],
        [IsEmployed], [Percentage], [Gender], [Phone], [Address], [CityId],
        [BeneficiaryType], [Status], [LegacyBenefCode], [LegacyNewId],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        RTRIM(b.Cedula),
        RTRIM(b.Nombre),
        NULLIF(RTRIM(b.TipoDocumento), ''),
        mr.NewRelationshipId,
        CASE WHEN b.FechaNacimiento <= '1900-01-02' THEN NULL ELSE CAST(b.FechaNacimiento AS DATE) END,
        NULLIF(RTRIM(b.NivelAcademico), ''),
        CASE WHEN b.Discapacidad = 'S' THEN 1 ELSE 0 END,
        CASE WHEN b.Trabaja = 'S' THEN 1 ELSE 0 END,
        b.porcentaje,
        NULLIF(RTRIM(b.Sexo), ''),
        NULLIF(RTRIM(b.Telefono), ''),
        NULLIF(RTRIM(b.direccion), ''),
        CASE WHEN b.ciudad = 0 THEN NULL ELSE mcity.NewCityId END,
        'A', -- BeneficiaryType = Associate
        NULLIF(RTRIM(b.estado), ''),
        NULLIF(RTRIM(b.codigobenef), ''),
        NULLIF(RTRIM(b.IdBenefNuevo), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.cop_benef b
    INNER JOIN [migration].[Map_People] mp ON RTRIM(b.Codigoter) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_Relationships] mr ON RTRIM(b.CodParentesco) = mr.OldCodigo
    LEFT JOIN [migration].[Map_Cities] mcity ON b.ciudad = mcity.OldCiudad;

    PRINT N'Migrating COR_Beneficiaries (cop_benef)... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    -- Step 2: nom_bene (Employee beneficiaries)
    INSERT INTO [dbo].[COR_Beneficiaries] (
        [PersonId], [BeneficiaryIdNumber], [BeneficiaryName],
        [RelationshipId], [DateOfBirth], [Percentage],
        [BeneficiaryType],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        RTRIM(nb.cedula),
        RTRIM(ISNULL(nb.nombres, '') + ' ' + ISNULL(nb.apellidos, '')),
        mr.NewRelationshipId,
        CASE WHEN nb.fecnac <= '1900-01-02' THEN NULL ELSE CAST(nb.fecnac AS DATE) END,
        nb.porcentaje,
        'E', -- BeneficiaryType = Employee
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.nom_bene nb
    INNER JOIN [migration].[Map_People] mp ON CAST(nb.idempleado AS VARCHAR(14)) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_Relationships] mr ON RTRIM(nb.Idparent) = mr.OldCodigo;

    PRINT N'Migrating COR_Beneficiaries (nom_bene)... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = (SELECT COUNT(*) FROM [dbo].[COR_Beneficiaries] WHERE CreatedBy = N'MIGRATION'),
        CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_ben;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_ben;
    PRINT N'ERROR migrating COR_Beneficiaries: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 2.25 COR_References (from: sys_referencia)
-- ============================================================
BEGIN TRY
    DECLARE @logId_ref INT;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable)
    VALUES (2, 'COR_References', 'sys_referencia');
    SET @logId_ref = SCOPE_IDENTITY();

    BEGIN TRAN;

    INSERT INTO [dbo].[COR_References] (
        [PersonId], [ReferenceType], [Name], [Address], [Phone],
        [CityId], [ContactName], [ProductType], [ProductNumber],
        [Mobile], [RelationshipCode],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        mp.NewPersonId,
        RTRIM(r.TipoReferencia),
        RTRIM(r.nombre),
        NULLIF(RTRIM(r.direccion), ''),
        NULLIF(RTRIM(r.Telefono), ''),
        CASE WHEN r.ciudad = 0 THEN NULL ELSE mcity.NewCityId END,
        NULLIF(RTRIM(r.contacto), ''),
        NULLIF(RTRIM(r.Tipo_Producto), ''),
        r.NroPrducto,
        NULLIF(RTRIM(r.celular), ''),
        NULLIF(RTRIM(r.parentesco), ''),
        N'MIGRATION',
        SYSUTCDATETIME()
    FROM [old].dbo.sys_referencia r
    INNER JOIN [migration].[Map_People] mp ON RTRIM(r.codigoter) = mp.OldCodigoTer
    LEFT JOIN [migration].[Map_Cities] mcity ON r.ciudad = mcity.OldCiudad;

    PRINT N'Migrating COR_References... ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' rows';

    UPDATE [migration].[MigrationLog]
    SET RowsMigrated = @@ROWCOUNT, CompletedAt = SYSUTCDATETIME(), Status = 'Success'
    WHERE Id = @logId_ref;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    UPDATE [migration].[MigrationLog]
    SET Status = 'Failed', ErrorMessage = ERROR_MESSAGE(), CompletedAt = SYSUTCDATETIME()
    WHERE Id = @logId_ref;
    PRINT N'ERROR migrating COR_References: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- Summary
-- ============================================================
PRINT N'';
PRINT N'============================================================';
PRINT N'  Step 02: COR_ Migration COMPLETE';
PRINT N'  Finished: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
PRINT N'';
PRINT N'Migration summary:';

SELECT TableName, SourceTable, RowsMigrated, Status, ErrorMessage
FROM [migration].[MigrationLog]
WHERE StepNumber = 2
ORDER BY Id;
GO
