-- ============================================================
-- IngenIA365ERP — Migration Step 08: SEC, AUD, WEB modules
-- Source: SOLIDO ERP (DBDefinicion.sql)
-- Target: IngenIA365ERP Schema v1.0
-- Depends on: Steps 01-07 (mapping tables + core data migrated)
-- ============================================================
SET NOCOUNT ON;
PRINT N'============================================================';
PRINT N'  Step 08: Migrating SEC_, AUD_, WEB_ modules';
PRINT N'  Started: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';
GO

-- ============================================================
-- SEC_Users ← sys_sasusu
-- ============================================================
PRINT N'';
PRINT N'--- SEC_Users ← sys_sasusu ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[SEC_Users] (
        Username,
        Email,
        PasswordHash,
        PasswordSalt,
        IsActive,
        IsEmailVerified,
        IsMfaEnabled,
        FailedLoginAttempts,
        LegacyLogin,
        LegacyPassword,
        PersonId,
        CanApproveLoansMin,
        CanApproveLoansMax,
        CanOverrideLimits,
        IdentificationNumber,
        CreatedAt,
        CreatedBy
    )
    SELECT
        u.login                                         AS Username,
        NULL                                            AS Email,
        N'LEGACY:' + ISNULL(u.[password], N'')          AS PasswordHash,
        NULL                                            AS PasswordSalt,
        CASE WHEN u.estatus = 'A' THEN 1 ELSE 0 END    AS IsActive,
        0                                               AS IsEmailVerified,
        0                                               AS IsMfaEnabled,
        0                                               AS FailedLoginAttempts,
        u.login                                         AS LegacyLogin,
        u.[password]                                    AS LegacyPassword,
        mp.NewPersonId                                  AS PersonId,
        ISNULL(u.aprocreini, 0)                         AS CanApproveLoansMin,
        ISNULL(u.aprocrefin, 0)                         AS CanApproveLoansMax,
        CASE WHEN u.sobregiro = 'S' THEN 1 ELSE 0 END  AS CanOverrideLimits,
        NULLIF(RTRIM(u.cedula), '')                     AS IdentificationNumber,
        SYSUTCDATETIME()                                AS CreatedAt,
        N'MIGRATION'                                    AS CreatedBy
    FROM [old].dbo.sys_sasusu u
    LEFT JOIN [migration].[Map_People] mp
        ON RTRIM(u.cedula) = mp.OldCodigoTer;

    SET @rowCount = @@ROWCOUNT;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_Users', N'sys_sasusu', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_Users: ' + CAST(@rowCount AS NVARCHAR) + N' rows migrated.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_Users', N'sys_sasusu', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_Users: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- SEC_UserMenuAccess ← sys_menusu (no PK, deduplicate)
-- ============================================================
PRINT N'';
PRINT N'--- SEC_UserMenuAccess ← sys_menusu ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[SEC_UserMenuAccess] (
        UserId,
        MenuType,
        ProgramType,
        ProgramCode,
        ProgramName,
        HasAccess,
        MenuCode,
        SubMenuCode,
        CreatedAt,
        CreatedBy
    )
    SELECT
        su.Id                                           AS UserId,
        m.tipo_menu                                     AS MenuType,
        NULLIF(RTRIM(m.tipo_programa), '')              AS ProgramType,
        NULLIF(RTRIM(m.codigo_programa), '')            AS ProgramCode,
        NULLIF(RTRIM(m.programa), '')                   AS ProgramName,
        CASE WHEN m.Acceso = 'S' THEN 1 ELSE 0 END     AS HasAccess,
        NULLIF(RTRIM(m.codigo_menu), '')                AS MenuCode,
        NULLIF(RTRIM(m.codigo_submenu), '')             AS SubMenuCode,
        SYSUTCDATETIME()                                AS CreatedAt,
        N'MIGRATION'                                    AS CreatedBy
    FROM (
        -- Deduplicate: sys_menusu has no PK, pick latest per unique combo
        SELECT
            tipo_menu, usuario, tipo_programa, codigo_programa,
            programa, Acceso, codigo_menu, codigo_submenu,
            ROW_NUMBER() OVER (
                PARTITION BY tipo_menu, usuario, tipo_programa, codigo_programa, codigo_menu, codigo_submenu
                ORDER BY fechasys DESC
            ) AS rn
        FROM [old].dbo.sys_menusu
    ) m
    INNER JOIN [dbo].[SEC_Users] su
        ON su.LegacyLogin = m.usuario
    WHERE m.rn = 1;

    SET @rowCount = @@ROWCOUNT;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_UserMenuAccess', N'sys_menusu', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_UserMenuAccess: ' + CAST(@rowCount AS NVARCHAR) + N' rows migrated.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_UserMenuAccess', N'sys_menusu', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_UserMenuAccess: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- SEC_Modules ← sys_programa
-- ============================================================
PRINT N'';
PRINT N'--- SEC_Modules ← sys_programa ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[SEC_Modules] (
        UserId,
        ProgramCode,
        Description,
        ProgramType,
        CreatedAt,
        CreatedBy
    )
    SELECT
        su.Id                                           AS UserId,
        NULLIF(RTRIM(p.programa), '')                   AS ProgramCode,
        NULLIF(RTRIM(p.descripcion), '')                AS Description,
        NULLIF(RTRIM(p.tipo_programa), '')              AS ProgramType,
        SYSUTCDATETIME()                                AS CreatedAt,
        N'MIGRATION'                                    AS CreatedBy
    FROM [old].dbo.sys_programa p
    LEFT JOIN [dbo].[SEC_Users] su
        ON su.LegacyLogin = RTRIM(p.usuario);

    SET @rowCount = @@ROWCOUNT;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_Modules', N'sys_programa', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_Modules: ' + CAST(@rowCount AS NVARCHAR) + N' rows migrated.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_Modules', N'sys_programa', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_Modules: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- SEC_UserAssignments ← sys_ComAsigna
-- ============================================================
PRINT N'';
PRINT N'--- SEC_UserAssignments ← sys_ComAsigna ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[SEC_UserAssignments] (
        VoucherTypeCode,
        UserId,
        CreatedAt,
        CreatedBy
    )
    SELECT
        NULLIF(RTRIM(a.CodCompro), '')                  AS VoucherTypeCode,
        su.Id                                           AS UserId,
        SYSUTCDATETIME()                                AS CreatedAt,
        N'MIGRATION'                                    AS CreatedBy
    FROM [old].dbo.sys_ComAsigna a
    INNER JOIN [dbo].[SEC_Users] su
        ON su.LegacyLogin = RTRIM(a.login);

    SET @rowCount = @@ROWCOUNT;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_UserAssignments', N'sys_ComAsigna', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_UserAssignments: ' + CAST(@rowCount AS NVARCHAR) + N' rows migrated.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_UserAssignments', N'sys_ComAsigna', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_UserAssignments: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- SEC_Roles — Seed default roles (new table, no legacy source)
-- ============================================================
PRINT N'';
PRINT N'--- SEC_Roles — Seed defaults ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Only insert if not already seeded
    IF NOT EXISTS (SELECT 1 FROM [dbo].[SEC_Roles] WHERE IsSystemRole = 1)
    BEGIN
        INSERT INTO [dbo].[SEC_Roles] (Name, Description, IsSystemRole, IsActive, CreatedBy)
        VALUES
            (N'Admin',            N'Full system administrator with unrestricted access',            1, 1, N'MIGRATION'),
            (N'Auditor',          N'Read-only access to all modules for audit purposes',            1, 1, N'MIGRATION'),
            (N'Accountant',       N'Access to accounting module (ACC_)',                             1, 1, N'MIGRATION'),
            (N'Cashier',          N'Access to deposits, savings, and cash operations',              1, 1, N'MIGRATION'),
            (N'LoanOfficer',      N'Access to lending/portfolio module (LND_)',                      1, 1, N'MIGRATION'),
            (N'PayrollAdmin',     N'Access to payroll module (PAY_)',                                1, 1, N'MIGRATION'),
            (N'InventoryManager', N'Access to inventory module (INV_)',                              1, 1, N'MIGRATION'),
            (N'ReadOnly',         N'Read-only access to all modules',                               1, 1, N'MIGRATION');

        SET @rowCount = @@ROWCOUNT;
    END;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_Roles', N'(seed data)', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_Roles: ' + CAST(@rowCount AS NVARCHAR) + N' default roles seeded.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_Roles', N'(seed data)', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_Roles: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- SEC_Permissions — Seed default permissions (new table)
-- ============================================================
PRINT N'';
PRINT N'--- SEC_Permissions — Seed defaults ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[SEC_Permissions])
    BEGIN
        INSERT INTO [dbo].[SEC_Permissions] (Resource, Action, Description, CreatedBy)
        VALUES
            -- Core / People
            (N'COR_People',          N'Read',   N'View people records',                  N'MIGRATION'),
            (N'COR_People',          N'Create', N'Create people records',                N'MIGRATION'),
            (N'COR_People',          N'Update', N'Update people records',                N'MIGRATION'),
            (N'COR_People',          N'Delete', N'Delete people records',                N'MIGRATION'),
            -- Accounting
            (N'ACC_JournalEntries',   N'Read',   N'View journal entries',                N'MIGRATION'),
            (N'ACC_JournalEntries',   N'Create', N'Create journal entries',              N'MIGRATION'),
            (N'ACC_JournalEntries',   N'Update', N'Update journal entries',              N'MIGRATION'),
            (N'ACC_JournalEntries',   N'Delete', N'Delete journal entries',              N'MIGRATION'),
            (N'ACC_ChartOfAccounts',  N'Read',   N'View chart of accounts',             N'MIGRATION'),
            (N'ACC_ChartOfAccounts',  N'Create', N'Create accounts',                    N'MIGRATION'),
            (N'ACC_ChartOfAccounts',  N'Update', N'Update accounts',                    N'MIGRATION'),
            -- Lending
            (N'LND_LoanPortfolios',   N'Read',   N'View loan portfolios',               N'MIGRATION'),
            (N'LND_LoanPortfolios',   N'Create', N'Create loan portfolios',             N'MIGRATION'),
            (N'LND_LoanPortfolios',   N'Update', N'Update loan portfolios',             N'MIGRATION'),
            (N'LND_LoanPortfolios',   N'Approve',N'Approve loan applications',          N'MIGRATION'),
            (N'LND_Transactions',     N'Read',   N'View lending transactions',          N'MIGRATION'),
            (N'LND_Transactions',     N'Create', N'Create lending transactions',        N'MIGRATION'),
            -- Payroll
            (N'PAY_Employees',        N'Read',   N'View employee records',              N'MIGRATION'),
            (N'PAY_Employees',        N'Create', N'Create employee records',            N'MIGRATION'),
            (N'PAY_Employees',        N'Update', N'Update employee records',            N'MIGRATION'),
            (N'PAY_PayrollPlanLiquidations', N'Read',   N'View payroll liquidations',   N'MIGRATION'),
            (N'PAY_PayrollPlanLiquidations', N'Create', N'Run payroll liquidations',    N'MIGRATION'),
            -- Inventory
            (N'INV_Products',         N'Read',   N'View products',                      N'MIGRATION'),
            (N'INV_Products',         N'Create', N'Create products',                    N'MIGRATION'),
            (N'INV_Products',         N'Update', N'Update products',                    N'MIGRATION'),
            (N'INV_Transactions',     N'Read',   N'View inventory transactions',        N'MIGRATION'),
            (N'INV_Transactions',     N'Create', N'Create inventory transactions',      N'MIGRATION'),
            -- Security
            (N'SEC_Users',            N'Read',   N'View user accounts',                 N'MIGRATION'),
            (N'SEC_Users',            N'Create', N'Create user accounts',               N'MIGRATION'),
            (N'SEC_Users',            N'Update', N'Update user accounts',               N'MIGRATION'),
            (N'SEC_Users',            N'Delete', N'Disable user accounts',              N'MIGRATION'),
            (N'SEC_Roles',            N'Manage', N'Manage roles and permissions',       N'MIGRATION'),
            -- Audit
            (N'AUD_*',               N'Read',   N'View audit trails',                   N'MIGRATION');

        SET @rowCount = @@ROWCOUNT;
    END;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_Permissions', N'(seed data)', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_Permissions: ' + CAST(@rowCount AS NVARCHAR) + N' default permissions seeded.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_Permissions', N'(seed data)', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_Permissions: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- SEC_RolePermissions — Link Admin role to all permissions
-- ============================================================
PRINT N'';
PRINT N'--- SEC_RolePermissions — Seed Admin→All ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[SEC_RolePermissions])
    BEGIN
        -- Admin gets all permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'Admin';

        SET @rowCount = @@ROWCOUNT;

        -- Auditor gets all Read permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'Auditor' AND p.Action = N'Read';

        SET @rowCount = @rowCount + @@ROWCOUNT;

        -- ReadOnly gets all Read permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'ReadOnly' AND p.Action = N'Read';

        SET @rowCount = @rowCount + @@ROWCOUNT;

        -- Accountant gets ACC_* permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'Accountant' AND p.Resource LIKE N'ACC_%';

        SET @rowCount = @rowCount + @@ROWCOUNT;

        -- LoanOfficer gets LND_* permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'LoanOfficer' AND p.Resource LIKE N'LND_%';

        SET @rowCount = @rowCount + @@ROWCOUNT;

        -- PayrollAdmin gets PAY_* permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'PayrollAdmin' AND p.Resource LIKE N'PAY_%';

        SET @rowCount = @rowCount + @@ROWCOUNT;

        -- InventoryManager gets INV_* permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'InventoryManager' AND p.Resource LIKE N'INV_%';

        SET @rowCount = @rowCount + @@ROWCOUNT;

        -- Cashier gets LND_ Read + deposits permissions
        INSERT INTO [dbo].[SEC_RolePermissions] (RoleId, PermissionId, CreatedBy)
        SELECT r.Id, p.Id, N'MIGRATION'
        FROM [dbo].[SEC_Roles] r
        CROSS JOIN [dbo].[SEC_Permissions] p
        WHERE r.Name = N'Cashier'
          AND (p.Resource LIKE N'LND_%' AND p.Action IN (N'Read', N'Create'));

        SET @rowCount = @rowCount + @@ROWCOUNT;
    END;

    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'SEC_RolePermissions', N'(seed data)', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  SEC_RolePermissions: ' + CAST(@rowCount AS NVARCHAR) + N' role-permission links seeded.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'SEC_RolePermissions', N'(seed data)', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());

    PRINT N'  ERROR SEC_RolePermissions: ' + ERROR_MESSAGE();
END CATCH;
GO


-- ############################################################
-- AUD_ MODULE — 13 legacy audit tables → modern JSON format
-- ############################################################
PRINT N'';
PRINT N'============================================================';
PRINT N'  AUD_ Module: Migrating 13 audit tables';
PRINT N'============================================================';
GO

-- ============================================================
-- AUD_CompanyChanges ← sys_ciaaud
-- ============================================================
PRINT N'--- AUD_CompanyChanges ← sys_ciaaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_CompanyChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.ACCION,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.CODIGO), ''),
        NULL,
        NULL,
        -- OldValues JSON from _ant columns
        (SELECT
            a.NOMBRE_ant        AS [Name],
            a.NIT_ant           AS [TaxId],
            a.DIRECCION_ant     AS [Address],
            a.TELEFONO_ant      AS [Phone],
            a.CIUDAD_ant        AS [City],
            a.DPTO_ant          AS [Department],
            a.NOMRES_ant        AS [ShortName],
            a.ACTIVIDAD_ant     AS [Activity],
            a.CODIGO_DIAN_ant   AS [DianCode],
            a.SALARIO_MINI_LEGAL_ant AS [MinLegalSalary],
            a.COBRA_INT_MORA_ant AS [ChargesLateFee],
            a.CLASE_NOMINA_ant  AS [PayrollType]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        -- NewValues JSON from _act columns
        (SELECT
            a.NOMBRE_act        AS [Name],
            a.NIT_act           AS [TaxId],
            a.DIRECCION_act     AS [Address],
            a.TELEFONO_act      AS [Phone],
            a.CIUDAD_act        AS [City],
            a.DPTO_act          AS [Department],
            a.NOMRES_act        AS [ShortName],
            a.ACTIVIDAD_act     AS [Activity],
            a.CODIGO_DIAN_act   AS [DianCode],
            a.SALARIO_MINI_LEGAL_act AS [MinLegalSalary],
            a.COBRA_INT_MORA_act AS [ChargesLateFee],
            a.CLASE_NOMINA_act  AS [PayrollType]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_ciaaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_CompanyChanges', N'sys_ciaaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_CompanyChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_CompanyChanges', N'sys_ciaaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_CompanyChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_UserChanges ← sys_sasusuaud
-- ============================================================
PRINT N'--- AUD_UserChanges ← sys_sasusuaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_UserChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.Accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.login             AS [Login],
            a.nombre_ant        AS [Name],
            a.grupo_ant         AS [Group],
            a.estatus_ant       AS [Status],
            a.nomusu_ant        AS [UserFullName],
            a.GrabaRetirados_ANT AS [CanSaveWithdrawn],
            a.grabacancelacdats_ANT AS [CanCancelCDT]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.login             AS [Login],
            a.nombre_act        AS [Name],
            a.grupo_act         AS [Group],
            a.estatus_act       AS [Status],
            a.nomusu_act        AS [UserFullName],
            a.GrabaRetirados_ACT AS [CanSaveWithdrawn],
            a.grabacancelacdats_ACT AS [CanCancelCDT]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_sasusuaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_UserChanges', N'sys_sasusuaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_UserChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_UserChanges', N'sys_sasusuaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_UserChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_VoucherTypeChanges ← sys_compro02aud
-- ============================================================
PRINT N'--- AUD_VoucherTypeChanges ← sys_compro02aud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_VoucherTypeChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.accion,
        ISNULL(CAST(a.fechaSys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.CODIGO                    AS [VoucherCode],
            a.NOMBRE_ant                AS [Name],
            a.DOCUMENTO_ant             AS [DocumentCode],
            a.CUENTA_CONTABLE_ant       AS [AccountCode],
            a.ACTUALIZA_CONTA_ant       AS [UpdatesAccounting],
            a.NUM_CONSECU_ant           AS [SequenceNumber],
            a.CONTROL_CONSEC_ant        AS [SequenceControl],
            a.EXIGE_DETALLE_ant         AS [RequiresDetail],
            a.CENCOSTO_ant              AS [CostCenter],
            a.RESTRI_TESORERIA_ant      AS [TreasuryRestriction],
            a.LAVA_ACTIVOS_ant          AS [MoneyLaundering],
            a.modulo_ant                AS [Module]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.CODIGO                    AS [VoucherCode],
            a.NOMBRE_act                AS [Name],
            a.DOCUMENTO_act             AS [DocumentCode],
            a.CUENTA_CONTABLE_act       AS [AccountCode],
            a.ACTUALIZA_CONTA_act       AS [UpdatesAccounting],
            a.NUM_CONSECU_act           AS [SequenceNumber],
            a.CONTROL_CONSEC_act        AS [SequenceControl],
            a.EXIGE_DETALLE_act         AS [RequiresDetail],
            a.CENCOSTO_act              AS [CostCenter],
            a.RESTRI_TESORERIA_act      AS [TreasuryRestriction],
            a.LAVA_ACTIVOS_act          AS [MoneyLaundering],
            a.modulo_act                AS [Module]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_compro02aud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_VoucherTypeChanges', N'sys_compro02aud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_VoucherTypeChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_VoucherTypeChanges', N'sys_compro02aud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_VoucherTypeChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_MasterChanges ← sys_masaud
-- ============================================================
PRINT N'--- AUD_MasterChanges ← sys_masaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_MasterChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.Accion,
        ISNULL(CAST(a.FECHA_REG AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.USUARIO_act), ''),
        NULL,
        NULL,
        (SELECT
            a.CODIGOTER             AS [PersonCode],
            a.APELLIDO_ant          AS [LastName],
            a.NOMBRE_ant            AS [FirstName],
            a.NIT_ant               AS [IdentificationNumber],
            a.DIRECCION_ant         AS [Address],
            a.TELEFONO1_ant         AS [Phone],
            a.MOVIL_ant             AS [Mobile],
            a.EMAIL_ant             AS [Email],
            a.DPTO_CIUDAD_ant       AS [CityId],
            a.EMPRESA_ant           AS [CompanyCode],
            a.AGENCIA_ant           AS [BranchCode],
            a.CENCOSTO_ant          AS [CostCenterCode],
            a.SALARIO_ant           AS [Salary],
            a.TASA_APORTE_ant       AS [ContributionRate],
            a.ESTADO_ant            AS [Status],
            a.CALMAN_ANT            AS [ManualRating]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.CODIGOTER             AS [PersonCode],
            a.APELLIDO_act          AS [LastName],
            a.NOMBRE_act            AS [FirstName],
            a.NIT_act               AS [IdentificationNumber],
            a.DIRECCION_act         AS [Address],
            a.TELEFONO1_act         AS [Phone],
            a.MOVIL_act             AS [Mobile],
            a.EMAIL_act             AS [Email],
            a.DPTO_CIUDAD_act       AS [CityId],
            a.EMPRESA_act           AS [CompanyCode],
            a.AGENCIA_act           AS [BranchCode],
            a.CENCOSTO_act          AS [CostCenterCode],
            a.SALARIO_act           AS [Salary],
            a.TASA_APORTE_act       AS [ContributionRate],
            a.ESTADO_act            AS [Status],
            a.CALMAN_ACT            AS [ManualRating]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_masaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_MasterChanges', N'sys_masaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_MasterChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_MasterChanges', N'sys_masaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_MasterChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_MenuChanges ← sys_menuaud
-- ============================================================
PRINT N'--- AUD_MenuChanges ← sys_menuaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_MenuChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues, AdditionalInfo,
        CreatedBy, CreatedAt
    )
    SELECT
        a.ACCION,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario), ''),
        NULL,
        NULL,
        (SELECT
            a.tipo_menu             AS [MenuType],
            a.tipo_programa         AS [ProgramType],
            a.codigo_programa       AS [ProgramCode],
            a.programa              AS [ProgramName],
            a.Acceso                AS [Access],
            a.codigo_menu           AS [MenuCode],
            a.codigo_submenu        AS [SubMenuCode],
            a.usuario_ant           AS [UserLogin],
            a.nomusu_ant            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.tipo_menu             AS [MenuType],
            a.tipo_programa         AS [ProgramType],
            a.codigo_programa       AS [ProgramCode],
            a.programa              AS [ProgramName],
            a.Acceso                AS [Access],
            a.codigo_menu           AS [MenuCode],
            a.codigo_submenu        AS [SubMenuCode],
            a.usuario_act           AS [UserLogin],
            a.nomusu_act            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        NULLIF(RTRIM(a.ModoGrabacion), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_menuaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_MenuChanges', N'sys_menuaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_MenuChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_MenuChanges', N'sys_menuaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_MenuChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_PeriodChanges ← sys_periodoAud
-- ============================================================
PRINT N'--- AUD_PeriodChanges ← sys_periodoAud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_PeriodChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.Accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.modulo                AS [Module],
            a.anio                  AS [Year],
            a.periodo_ant           AS [Period],
            a.estado_01_ant         AS [Status01],
            a.estado_02_ant         AS [Status02],
            a.estado_03_ant         AS [Status03],
            a.estado_04_ant         AS [Status04],
            a.estado_05_ant         AS [Status05],
            a.estado_06_ant         AS [Status06],
            a.estado_07_ant         AS [Status07],
            a.estado_08_ant         AS [Status08],
            a.estado_09_ant         AS [Status09],
            a.estado_10_ant         AS [Status10],
            a.estado_11_ant         AS [Status11],
            a.estado_12_ant         AS [Status12],
            a.estado_13_ant         AS [Status13],
            a.nomusu_ant            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.modulo                AS [Module],
            a.anio                  AS [Year],
            a.periodo_act           AS [Period],
            a.estado_01_act         AS [Status01],
            a.estado_02_act         AS [Status02],
            a.estado_03_act         AS [Status03],
            a.estado_04_act         AS [Status04],
            a.estado_05_act         AS [Status05],
            a.estado_06_act         AS [Status06],
            a.estado_07_act         AS [Status07],
            a.estado_08_act         AS [Status08],
            a.estado_09_act         AS [Status09],
            a.estado_10_act         AS [Status10],
            a.estado_11_act         AS [Status11],
            a.estado_12_act         AS [Status12],
            a.estado_13_act         AS [Status13],
            a.nomusu_act            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_periodoAud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_PeriodChanges', N'sys_periodoAud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_PeriodChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_PeriodChanges', N'sys_periodoAud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_PeriodChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_AssignmentChanges ← sys_ComAsignaAud
-- ============================================================
PRINT N'--- AUD_AssignmentChanges ← sys_ComAsignaAud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_AssignmentChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.Accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.CodCompro             AS [VoucherCode],
            a.login                 AS [Login],
            a.usuario_ant           AS [UserLogin],
            a.nomusu_ant            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.CodCompro             AS [VoucherCode],
            a.login                 AS [Login],
            a.usuario_act           AS [UserLogin],
            a.nomusu_act            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.sys_ComAsignaAud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_AssignmentChanges', N'sys_ComAsignaAud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_AssignmentChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_AssignmentChanges', N'sys_ComAsignaAud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_AssignmentChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_AccountChanges ← cnt_maecuenAud
-- ============================================================
PRINT N'--- AUD_AccountChanges ← cnt_maecuenAud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_AccountChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.CUENTA                    AS [AccountCode],
            a.NATURA_ant                AS [Nature],
            a.NOMBRE_ant                AS [Name],
            a.NIVEL_ant                 AS [Level],
            a.TASA_ant                  AS [Rate],
            a.AUX_DOMTO_ant             AS [RequiresDocument],
            a.MANE_CENCOS_ant           AS [ManagesCostCenter],
            a.TERCERO_ant               AS [RequiresThirdParty],
            a.APLI_CARTCOOPE_ant        AS [AppliesToLending],
            a.APLI_AHOR_CDT_ant         AS [AppliesToSavingsCDT],
            a.APLI_INVENTA_ant          AS [AppliesToInventory],
            a.APLI_TESORERI_ant         AS [AppliesToTreasury],
            a.APLI_NOMINA_ant           AS [AppliesToPayroll],
            a.APLI_CONTAB_ant           AS [AppliesToAccounting],
            a.APLI_FACTURAC_ant         AS [AppliesToInvoicing],
            a.CENCOS_ant                AS [CostCenter],
            a.nomusu_ant                AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.CUENTA                    AS [AccountCode],
            a.NATURA_act                AS [Nature],
            a.NOMBRE_act                AS [Name],
            a.NIVEL_act                 AS [Level],
            a.TASA_act                  AS [Rate],
            a.AUX_DOMTO_act             AS [RequiresDocument],
            a.MANE_CENCOS_act           AS [ManagesCostCenter],
            a.TERCERO_act               AS [RequiresThirdParty],
            a.APLI_CARTCOOPE_act        AS [AppliesToLending],
            a.APLI_AHOR_CDT_act         AS [AppliesToSavingsCDT],
            a.APLI_INVENTA_act          AS [AppliesToInventory],
            a.APLI_TESORERI_act         AS [AppliesToTreasury],
            a.APLI_NOMINA_act           AS [AppliesToPayroll],
            a.APLI_CONTAB_act           AS [AppliesToAccounting],
            a.APLI_FACTURAC_act         AS [AppliesToInvoicing],
            a.CENCOS_act                AS [CostCenter],
            a.nomusu_act                AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_maecuenAud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_AccountChanges', N'cnt_maecuenAud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_AccountChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_AccountChanges', N'cnt_maecuenAud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_AccountChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_JournalChanges ← cnt_movaud
-- ============================================================
PRINT N'--- AUD_JournalChanges ← cnt_movaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_JournalChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues, AdditionalInfo,
        CreatedBy, CreatedAt
    )
    SELECT
        a.accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        CAST(a.SECUENCIA AS BIGINT),
        (SELECT
            a.compronte_ant         AS [VoucherType],
            a.numero_ant            AS [DocumentNumber],
            a.cuenta_ant            AS [AccountCode],
            a.nit_ant               AS [PersonCode],
            a.agencia_ant           AS [BranchCode],
            a.cencosto_ant          AS [CostCenter],
            a.periodo_ant           AS [Period],
            a.fecha_ant             AS [TransactionDate],
            a.detalle_ant           AS [Description],
            a.vlr_debito_ant        AS [DebitAmount],
            a.vlr_credito_ant       AS [CreditAmount],
            a.vlr_base_ant          AS [BaseAmount],
            a.estado_ant            AS [Status],
            a.factura_ant           AS [InvoiceNumber],
            a.NomUsu_ant            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.compronte_act         AS [VoucherType],
            a.numero_act            AS [DocumentNumber],
            a.cuenta_act            AS [AccountCode],
            a.nit_act               AS [PersonCode],
            a.agencia_act           AS [BranchCode],
            a.cencosto_act          AS [CostCenter],
            a.periodo_act           AS [Period],
            a.fecha_act             AS [TransactionDate],
            a.detalle_act           AS [Description],
            a.vlr_debito_act        AS [DebitAmount],
            a.vlr_credito_act       AS [CreditAmount],
            a.vlr_base_act          AS [BaseAmount],
            a.estado_act            AS [Status],
            a.factura_act           AS [InvoiceNumber],
            a.NomUsu_act            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        NULLIF(RTRIM(a.comentario), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cnt_movaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_JournalChanges', N'cnt_movaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_JournalChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_JournalChanges', N'cnt_movaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_JournalChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_PortfolioTransactionChanges ← cop_movaud
-- ============================================================
PRINT N'--- AUD_PortfolioTransactionChanges ← cop_movaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_PortfolioTransactionChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues, AdditionalInfo,
        CreatedBy, CreatedAt
    )
    SELECT
        a.Accion,
        ISNULL(CAST(a.Fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.USUARIO_act), ''),
        NULL,
        CAST(a.SECUENCIA AS BIGINT),
        (SELECT
            a.COMPRONTE_ant         AS [VoucherType],
            a.NUMERO_DOMTO_ant      AS [DocumentNumber],
            a.CODIGOTER_ant         AS [PersonCode],
            a.LINCRED_ant           AS [CreditLineId],
            a.NUMERO_ant            AS [PortfolioNumber],
            a.CUENTA_ant            AS [AccountCode],
            a.FECHA_MOVTO_ant       AS [TransactionDate],
            a.VLR_DEBITO_ant        AS [DebitAmount],
            a.VLR_CREDITO_ant       AS [CreditAmount],
            a.CENCOS_ant            AS [CostCenter],
            a.AGENCIA_ant           AS [BranchCode],
            a.TASA_INT_ant          AS [InterestRate],
            a.COD_MOVTO_ant         AS [TransactionCode],
            a.NIT_ant               AS [Nit],
            a.detalle_ant           AS [Description],
            a.NomUsu_ant            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.COMPRONTE_act         AS [VoucherType],
            a.NUMERO_DOMTO_act      AS [DocumentNumber],
            a.CODIGOTER_act         AS [PersonCode],
            a.LINCRED_act           AS [CreditLineId],
            a.NUMERO_act            AS [PortfolioNumber],
            a.CUENTA_act            AS [AccountCode],
            a.FECHA_MOVTO_act       AS [TransactionDate],
            a.VLR_DEBITO_act        AS [DebitAmount],
            a.VLR_CREDITO_act       AS [CreditAmount],
            a.CENCOS_act            AS [CostCenter],
            a.AGENCIA_act           AS [BranchCode],
            a.TASA_INT_act          AS [InterestRate],
            a.COD_MOVTO_act         AS [TransactionCode],
            a.NIT_act               AS [Nit],
            a.detalle_act           AS [Description],
            a.NomUsu_act            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        NULLIF(RTRIM(a.comentario), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_movaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_PortfolioTransactionChanges', N'cop_movaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_PortfolioTransactionChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_PortfolioTransactionChanges', N'cop_movaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_PortfolioTransactionChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_PortfolioMasterChanges ← cop_mcaaud
-- ============================================================
PRINT N'--- AUD_PortfolioMasterChanges ← cop_mcaaud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_PortfolioMasterChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.codigoter             AS [PersonCode],
            a.lincred               AS [CreditLine],
            a.numero                AS [PortfolioNumber],
            a.fecdesc_ant           AS [DisbursementDate],
            a.plazo_ant             AS [Term],
            a.valorob_ant           AS [LoanAmount],
            a.saldot_ant            AS [Balance],
            a.cuota_ant             AS [InstallmentAmount],
            a.tasaint_ant           AS [InterestRate],
            a.ciclo_ant             AS [Cycle],
            a.periodd_ant           AS [Periodicity],
            a.clacuo_ant            AS [InstallmentType],
            a.clasei_ant            AS [InterestType],
            a.clades_ant            AS [DeductionType],
            a.codeudor1_ant         AS [Codebtor1],
            a.codeudor2_ant         AS [Codebtor2],
            a.empdsto_ant           AS [DeductionCompany],
            a.Calrest_ant           AS [RestructuringRating],
            a.ReEst_ant             AS [IsRestructured],
            a.Castigo_ant           AS [IsWrittenOff],
            a.demanda_ant           AS [IsInLitigation]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.codigoter             AS [PersonCode],
            a.lincred               AS [CreditLine],
            a.numero                AS [PortfolioNumber],
            a.fecdesc_act           AS [DisbursementDate],
            a.plazo_act             AS [Term],
            a.valorob_act           AS [LoanAmount],
            a.saldot_act            AS [Balance],
            a.cuota_act             AS [InstallmentAmount],
            a.tasaint_act           AS [InterestRate],
            a.ciclo_act             AS [Cycle],
            a.periodd_act           AS [Periodicity],
            a.clacuo_act            AS [InstallmentType],
            a.clasei_act            AS [InterestType],
            a.clades_act            AS [DeductionType],
            a.codeudor1_act         AS [Codebtor1],
            a.codeudor2_act         AS [Codebtor2],
            a.empdsto_act           AS [DeductionCompany],
            a.Calrest_act           AS [RestructuringRating],
            a.ReEst_act             AS [IsRestructured],
            a.Castigo_act           AS [IsWrittenOff],
            a.demanda_act           AS [IsInLitigation]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_mcaaud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_PortfolioMasterChanges', N'cop_mcaaud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_PortfolioMasterChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_PortfolioMasterChanges', N'cop_mcaaud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_PortfolioMasterChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_DefaultChanges ← cop_moraud
-- ============================================================
PRINT N'--- AUD_DefaultChanges ← cop_moraud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_DefaultChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.accion,
        ISNULL(CAST(a.fecha_graba AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario), ''),
        NULL,
        NULL,
        (SELECT
            a.codigoter             AS [PersonCode],
            a.lincred               AS [CreditLine],
            a.numero                AS [PortfolioNumber],
            a.periodo_causa         AS [AccrualPeriod],
            a.periodo_contable      AS [AccountingPeriod],
            a.diasmora_ant          AS [DaysOverdue],
            a.saldocapital_ant      AS [PrincipalBalance],
            a.saldoextra_ant        AS [ExtraBalance],
            a.saldointeres_ant      AS [InterestBalance],
            a.saldomora_ant         AS [DefaultInterestBalance],
            a.saldoseguro_ant       AS [InsuranceBalance],
            a.saldoadmon_ant        AS [AdminFeeBalance],
            a.saldootros_ant        AS [OtherBalance],
            a.capital_abono_ant     AS [PrincipalPayment],
            a.interes_abono_ant     AS [InterestPayment],
            a.mora_abono_ant        AS [DefaultPayment],
            a.seguro_abono_ant      AS [InsurancePayment]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.codigoter             AS [PersonCode],
            a.lincred               AS [CreditLine],
            a.numero                AS [PortfolioNumber],
            a.periodo_causa         AS [AccrualPeriod],
            a.periodo_contable      AS [AccountingPeriod],
            a.diasmora_act          AS [DaysOverdue],
            a.saldocapital_act      AS [PrincipalBalance],
            a.saldoextra_act        AS [ExtraBalance],
            a.saldointeres_act      AS [InterestBalance],
            a.saldomora_act         AS [DefaultInterestBalance],
            a.saldoseguro_act       AS [InsuranceBalance],
            a.saldoadmon_act        AS [AdminFeeBalance],
            a.saldootros_act        AS [OtherBalance],
            a.capital_abono_act     AS [PrincipalPayment],
            a.interes_abono_act     AS [InterestPayment],
            a.mora_abono_act        AS [DefaultPayment],
            a.seguro_abono_act      AS [InsurancePayment]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_moraud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_DefaultChanges', N'cop_moraud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_DefaultChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_DefaultChanges', N'cop_moraud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_DefaultChanges: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- AUD_SavingsChanges ← cop_ahoraud
-- ============================================================
PRINT N'--- AUD_SavingsChanges ← cop_ahoraud ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[AUD_SavingsChanges] (
        Action, ActionDate, UserId, UserName, IpAddress,
        EntityId, OldValues, NewValues,
        CreatedBy, CreatedAt
    )
    SELECT
        a.Accion,
        ISNULL(CAST(a.fechasys AS DATETIME2), SYSUTCDATETIME()),
        NULL,
        NULLIF(RTRIM(a.usuario_act), ''),
        NULL,
        NULL,
        (SELECT
            a.lincred               AS [SavingsLineId],
            a.NOMBRE_ant            AS [Name],
            a.NOMBRE_RESUM_ant      AS [ShortName],
            a.PERPAGO_INT_ant       AS [InterestPaymentPeriod],
            a.SALMIN_INT_ant        AS [MinInterestBalance],
            a.VALMIT_TRAN_ant       AS [MinTransactionAmount],
            a.SALMIN_CUENTA_ant     AS [MinAccountBalance],
            a.PORPAGO_INT_ant       AS [InterestPaymentRate],
            a.VLR_MIN_RFTE_ant      AS [MinWithholdingAmount],
            a.POR_RFTE_ant          AS [WithholdingRate],
            a.DIAS_CANJE_ant        AS [ClearingDays],
            a.FORLIQ_ant            AS [LiquidationMethod],
            a.DIAS_GRACIA_ant       AS [GracePeriodDays],
            a.gravamen_ant          AS [TaxRate],
            a.maxefectivo_ant       AS [MaxCashAmount],
            a.nomusu_ant            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT
            a.lincred               AS [SavingsLineId],
            a.NOMBRE_act            AS [Name],
            a.NOMBRE_RESUM_act      AS [ShortName],
            a.PERPAGO_INT_act       AS [InterestPaymentPeriod],
            a.SALMIN_INT_act        AS [MinInterestBalance],
            a.VALMIT_TRAN_act       AS [MinTransactionAmount],
            a.SALMIN_CUENTA_act     AS [MinAccountBalance],
            a.PORPAGO_INT_act       AS [InterestPaymentRate],
            a.VLR_MIN_RFTE_act      AS [MinWithholdingAmount],
            a.POR_RFTE_act          AS [WithholdingRate],
            a.DIAS_CANJE_act        AS [ClearingDays],
            a.FORLIQ_act            AS [LiquidationMethod],
            a.DIAS_GRACIA_act       AS [GracePeriodDays],
            a.gravamen_act          AS [TaxRate],
            a.maxefectivo_act       AS [MaxCashAmount],
            a.nomusu_act            AS [UserFullName]
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.cop_ahoraud a;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'AUD_SavingsChanges', N'cop_ahoraud', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  AUD_SavingsChanges: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'AUD_SavingsChanges', N'cop_ahoraud', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR AUD_SavingsChanges: ' + ERROR_MESSAGE();
END CATCH;
GO


-- ############################################################
-- WEB_ MODULE — 6 tables, simple INSERT...SELECT
-- ############################################################
PRINT N'';
PRINT N'============================================================';
PRINT N'  WEB_ Module: Migrating 6 web portal tables';
PRINT N'============================================================';
GO

-- ============================================================
-- WEB_LoanApplications ← web_solcred
-- ============================================================
PRINT N'--- WEB_LoanApplications ← web_solcred ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[WEB_LoanApplications] (
        PersonId,
        SequenceNumber,
        ApplicationDate,
        IpAddress,
        CreditLineId,
        InterestRate,
        RequestedAmount,
        TermMonths,
        Periodicity,
        InstallmentAmount,
        Purpose,
        LegacyCodigoTer,
        CreatedAt,
        CreatedBy
    )
    SELECT
        mp.NewPersonId                                  AS PersonId,
        w.consecutivo                                   AS SequenceNumber,
        CAST(w.fecha AS DATE)                           AS ApplicationDate,
        NULLIF(RTRIM(w.ip), '')                         AS IpAddress,
        w.lincred                                       AS CreditLineId,
        w.tasa                                          AS InterestRate,
        w.monto_soli                                    AS RequestedAmount,
        CAST(w.plazo AS INT)                            AS TermMonths,
        NULLIF(RTRIM(w.percidad), '')                   AS Periodicity,
        w.cuota                                         AS InstallmentAmount,
        NULLIF(RTRIM(w.destino), '')                    AS Purpose,
        RTRIM(w.codigoter)                              AS LegacyCodigoTer,
        SYSUTCDATETIME(),
        N'MIGRATION'
    FROM [old].dbo.web_solcred w
    LEFT JOIN [migration].[Map_People] mp
        ON RTRIM(w.codigoter) = mp.OldCodigoTer;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'WEB_LoanApplications', N'web_solcred', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  WEB_LoanApplications: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'WEB_LoanApplications', N'web_solcred', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR WEB_LoanApplications: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- WEB_AuxiliaryApplications ← web_solaux
-- ============================================================
PRINT N'--- WEB_AuxiliaryApplications ← web_solaux ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[WEB_AuxiliaryApplications] (
        PersonId,
        SequenceNumber,
        ApplicationDate,
        IpAddress,
        SubsidyType,
        Amount,
        Reason,
        CreatedAt,
        CreatedBy
    )
    SELECT
        mp.NewPersonId                                  AS PersonId,
        w.consecutivo                                   AS SequenceNumber,
        CAST(w.fecha AS DATE)                           AS ApplicationDate,
        NULLIF(RTRIM(w.ip), '')                         AS IpAddress,
        NULLIF(RTRIM(w.tipo_aux), '')                   AS SubsidyType,
        w.valor                                         AS Amount,
        CAST(w.motivo AS NVARCHAR(MAX))                 AS Reason,
        SYSUTCDATETIME(),
        N'MIGRATION'
    FROM [old].dbo.web_solaux w
    LEFT JOIN [migration].[Map_People] mp
        ON RTRIM(w.codigoter) = mp.OldCodigoTer;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'WEB_AuxiliaryApplications', N'web_solaux', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  WEB_AuxiliaryApplications: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'WEB_AuxiliaryApplications', N'web_solaux', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR WEB_AuxiliaryApplications: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- WEB_AffiliationApplications ← web_solafi
-- ============================================================
PRINT N'--- WEB_AffiliationApplications ← web_solafi ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[WEB_AffiliationApplications] (
        SequenceNumber,
        ApplicationDate,
        IpAddress,
        IdentificationNumber,
        IdentificationType,
        FirstName,
        LastName,
        Address,
        Phone,
        Email,
        EmployerName,
        Salary,
        SpouseFirstName,
        SpouseLastName,
        SpouseIdentificationNumber,
        SpouseIdentificationType,
        SpousePhone,
        SpouseEmail,
        SpouseEmployerName,
        SpouseSalary,
        CreatedAt,
        CreatedBy
    )
    SELECT
        w.consecutivo                                   AS SequenceNumber,
        CAST(w.fecha AS DATE)                           AS ApplicationDate,
        NULLIF(RTRIM(w.ip), '')                         AS IpAddress,
        NULLIF(RTRIM(w.num_doc), '')                    AS IdentificationNumber,
        NULLIF(RTRIM(w.tipo_nit), '')                   AS IdentificationType,
        NULLIF(RTRIM(w.nombre), '')                     AS FirstName,
        NULLIF(RTRIM(w.apellido), '')                   AS LastName,
        NULLIF(RTRIM(w.direccion), '')                  AS Address,
        NULLIF(RTRIM(w.tel), '')                        AS Phone,
        NULLIF(RTRIM(w.email), '')                      AS Email,
        NULLIF(RTRIM(w.empresa), '')                    AS EmployerName,
        CASE WHEN w.salario = '' THEN NULL
             ELSE TRY_CAST(w.salario AS DECIMAL(18,2)) END AS Salary,
        NULLIF(RTRIM(w.nombre_conyu), '')               AS SpouseFirstName,
        NULL                                            AS SpouseLastName,
        NULLIF(RTRIM(w.nit_conyu), '')                  AS SpouseIdentificationNumber,
        NULLIF(RTRIM(w.tipo_nit_conyu), '')             AS SpouseIdentificationType,
        NULLIF(RTRIM(w.tel_conyu), '')                  AS SpousePhone,
        NULL                                            AS SpouseEmail,
        NULLIF(RTRIM(w.empresa_conyu), '')              AS SpouseEmployerName,
        NULL                                            AS SpouseSalary,
        SYSUTCDATETIME(),
        N'MIGRATION'
    FROM [old].dbo.web_solafi w;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'WEB_AffiliationApplications', N'web_solafi', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  WEB_AffiliationApplications: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'WEB_AffiliationApplications', N'web_solafi', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR WEB_AffiliationApplications: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- WEB_Services ← web_maeser
-- ============================================================
PRINT N'--- WEB_Services ← web_maeser ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[WEB_Services] (
        ServiceNumber,
        ServiceType,
        IdentificationNumber,
        CallDate,
        AppointmentDate,
        Observations,
        IsReviewed,
        ReviewedBy,
        Status,
        CreatedAt,
        CreatedBy
    )
    SELECT
        w.numero                                        AS ServiceNumber,
        NULLIF(RTRIM(w.tipo_novedad), '')               AS ServiceType,
        NULLIF(RTRIM(w.cedula), '')                     AS IdentificationNumber,
        CAST(w.fecha_llamada AS DATE)                   AS CallDate,
        CAST(w.fecha_cita AS DATE)                      AS AppointmentDate,
        CAST(w.observaciones AS NVARCHAR(MAX))          AS Observations,
        CASE WHEN w.revisado = 'S' THEN 1 ELSE 0 END   AS IsReviewed,
        NULLIF(RTRIM(w.usuario), '')                    AS ReviewedBy,
        NULLIF(RTRIM(w.estado), '')                     AS Status,
        SYSUTCDATETIME(),
        N'MIGRATION'
    FROM [old].dbo.web_maeser w;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'WEB_Services', N'web_maeser', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  WEB_Services: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'WEB_Services', N'web_maeser', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR WEB_Services: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- WEB_ExtraPayments ← web_extras
-- ============================================================
PRINT N'--- WEB_ExtraPayments ← web_extras ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[WEB_ExtraPayments] (
        SequenceNumber,
        PaymentDate,
        Amount,
        CreatedAt,
        CreatedBy
    )
    SELECT
        w.consecutivo                                   AS SequenceNumber,
        CAST(w.fecha AS DATE)                           AS PaymentDate,
        w.valor                                         AS Amount,
        SYSUTCDATETIME(),
        N'MIGRATION'
    FROM [old].dbo.web_extras w;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'WEB_ExtraPayments', N'web_extras', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  WEB_ExtraPayments: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'WEB_ExtraPayments', N'web_extras', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR WEB_ExtraPayments: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- WEB_DataUpdates ← web_actdatos
-- ============================================================
PRINT N'--- WEB_DataUpdates ← web_actdatos ---';

DECLARE @stepStart DATETIME2 = SYSUTCDATETIME();
DECLARE @rowCount  INT = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[WEB_DataUpdates] (
        PersonId,
        SequenceNumber,
        UpdateDate,
        IpAddress,
        NewAddress,
        NewPhone,
        NewMobilePhone,
        NewEmail,
        NewCity,
        NewEmployerName,
        NewEmployerAddress,
        NewEmployerPhone,
        NewPosition,
        NewMaritalStatus,
        NewHousingType,
        SystemDate,
        IsProcessed,
        ProcessedBy,
        CreatedAt,
        CreatedBy
    )
    SELECT
        mp.NewPersonId                                  AS PersonId,
        w.consecutivo                                   AS SequenceNumber,
        CAST(w.fecha AS DATE)                           AS UpdateDate,
        NULLIF(RTRIM(w.ip), '')                         AS IpAddress,
        NULLIF(RTRIM(w.DIRECCION), '')                  AS NewAddress,
        NULLIF(RTRIM(w.TELEFONO1), '')                  AS NewPhone,
        NULLIF(RTRIM(w.MOVIL), '')                      AS NewMobilePhone,
        NULLIF(RTRIM(w.email), '')                      AS NewEmail,
        CASE WHEN w.DPTO_CIUDAD IS NOT NULL
             THEN CAST(w.DPTO_CIUDAD AS NVARCHAR(100))
             ELSE NULL END                              AS NewCity,
        NULLIF(RTRIM(w.empresa), '')                    AS NewEmployerName,
        NULLIF(RTRIM(w.dir_empresa), '')                AS NewEmployerAddress,
        NULLIF(RTRIM(w.tel_empresa), '')                AS NewEmployerPhone,
        NULLIF(RTRIM(w.cargo_emp), '')                  AS NewPosition,
        NULLIF(RTRIM(w.ESTADO_CIVIL), '')               AS NewMaritalStatus,
        NULLIF(RTRIM(w.TIPO_VIVIENDA), '')              AS NewHousingType,
        CAST(w.FechaSys AS DATETIME2)                   AS SystemDate,
        CASE WHEN w.inf_actualizada = 'S' THEN 1 ELSE 0 END AS IsProcessed,
        NULLIF(RTRIM(w.usuario), '')                    AS ProcessedBy,
        SYSUTCDATETIME(),
        N'MIGRATION'
    FROM [old].dbo.web_actdatos w
    LEFT JOIN [migration].[Map_People] mp
        ON RTRIM(w.codigoter) = mp.OldCodigoTer;

    SET @rowCount = @@ROWCOUNT;
    COMMIT TRANSACTION;

    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status)
    VALUES (8, N'WEB_DataUpdates', N'web_actdatos', @rowCount, @stepStart, SYSUTCDATETIME(), N'Success');

    PRINT N'  WEB_DataUpdates: ' + CAST(@rowCount AS NVARCHAR) + N' rows.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    INSERT INTO [migration].[MigrationLog] (StepNumber, TableName, SourceTable, RowsMigrated, StartedAt, CompletedAt, Status, ErrorMessage)
    VALUES (8, N'WEB_DataUpdates', N'web_actdatos', 0, @stepStart, SYSUTCDATETIME(), N'Failed', ERROR_MESSAGE());
    PRINT N'  ERROR WEB_DataUpdates: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- STEP 08 SUMMARY
-- ============================================================
PRINT N'';
PRINT N'============================================================';
PRINT N'  Step 08 Complete';
PRINT N'  Finished: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'============================================================';

SELECT TableName, SourceTable, RowsMigrated, Status, ErrorMessage
FROM [migration].[MigrationLog]
WHERE StepNumber = 8
ORDER BY Id;

PRINT N'';
PRINT N'SEC tables:  4 migrated + 3 seeded (Roles, Permissions, RolePermissions)';
PRINT N'AUD tables: 13 migrated (legacy _ant/_act → JSON OldValues/NewValues)';
PRINT N'WEB tables:  6 migrated';
GO
