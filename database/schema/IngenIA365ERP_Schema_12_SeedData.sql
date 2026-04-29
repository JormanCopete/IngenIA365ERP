-- ============================================================
-- IngenIA365ERP — Seed Data (Initial Catalogs)
-- Generated: 2026-03-20
-- Run AFTER all tables, FKs, and indexes are created.
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- 1. Identification Types (Reference Data)
-- Stored as a lookup; used by COR_People.IdentificationType
-- ============================================================
-- NOTE: If COR_IdentificationTypes is a separate table, insert there.
-- Otherwise these values are used as CHECK constraint values in COR_People.

IF OBJECT_ID(N'dbo.COR_IdentificationTypes', N'U') IS NOT NULL
BEGIN
    SET IDENTITY_INSERT [dbo].[COR_IdentificationTypes] ON;

    MERGE INTO [dbo].[COR_IdentificationTypes] AS tgt
    USING (VALUES
        (1, N'CC',   N'Cedula de Ciudadania',                   1, N'SYSTEM', GETUTCDATE()),
        (2, N'NIT',  N'Numero de Identificacion Tributaria',     1, N'SYSTEM', GETUTCDATE()),
        (3, N'CE',   N'Cedula de Extranjeria',                   1, N'SYSTEM', GETUTCDATE()),
        (4, N'PP',   N'Pasaporte',                               1, N'SYSTEM', GETUTCDATE()),
        (5, N'TI',   N'Tarjeta de Identidad',                    1, N'SYSTEM', GETUTCDATE()),
        (6, N'RC',   N'Registro Civil',                           1, N'SYSTEM', GETUTCDATE()),
        (7, N'NUIP', N'Numero Unico de Identificacion Personal', 1, N'SYSTEM', GETUTCDATE())
    ) AS src ([Id], [Code], [Name], [IsActive], [CreatedBy], [CreatedAt])
    ON tgt.[Code] = src.[Code]
    WHEN NOT MATCHED THEN
        INSERT ([Id], [Code], [Name], [IsActive], [CreatedBy], [CreatedAt])
        VALUES (src.[Id], src.[Code], src.[Name], src.[IsActive], src.[CreatedBy], src.[CreatedAt]);

    SET IDENTITY_INSERT [dbo].[COR_IdentificationTypes] OFF;
    PRINT N'Seed: COR_IdentificationTypes - 7 rows inserted/merged.';
END
ELSE
    PRINT N'Seed: COR_IdentificationTypes table not found - skipped (values enforced via CHECK constraint).';
GO

-- ============================================================
-- 2. Colombia: Country, Departments, Cities
-- ============================================================

-- 2a. Country
SET IDENTITY_INSERT [dbo].[COR_Countries] ON;

MERGE INTO [dbo].[COR_Countries] AS tgt
USING (VALUES
    (1, N'CO', N'Colombia', 1, N'SYSTEM', GETUTCDATE())
) AS src ([Id], [IsoCode], [Name], [IsActive], [CreatedBy], [CreatedAt])
ON tgt.[IsoCode] = src.[IsoCode]
WHEN NOT MATCHED THEN
    INSERT ([Id], [IsoCode], [Name], [IsActive], [CreatedBy], [CreatedAt])
    VALUES (src.[Id], src.[IsoCode], src.[Name], src.[IsActive], src.[CreatedBy], src.[CreatedAt]);

SET IDENTITY_INSERT [dbo].[COR_Countries] OFF;
PRINT N'Seed: COR_Countries - 1 row (Colombia).';
GO

-- 2b. Departments (24)
SET IDENTITY_INSERT [dbo].[COR_Departments] ON;

MERGE INTO [dbo].[COR_Departments] AS tgt
USING (VALUES
    ( 1, N'05', N'Antioquia',            1, 1, N'SYSTEM', GETUTCDATE()),
    ( 2, N'08', N'Atlantico',            1, 1, N'SYSTEM', GETUTCDATE()),
    ( 3, N'11', N'Bogota D.C.',          1, 1, N'SYSTEM', GETUTCDATE()),
    ( 4, N'13', N'Bolivar',              1, 1, N'SYSTEM', GETUTCDATE()),
    ( 5, N'15', N'Boyaca',               1, 1, N'SYSTEM', GETUTCDATE()),
    ( 6, N'17', N'Caldas',               1, 1, N'SYSTEM', GETUTCDATE()),
    ( 7, N'18', N'Caqueta',              1, 1, N'SYSTEM', GETUTCDATE()),
    ( 8, N'19', N'Cauca',                1, 1, N'SYSTEM', GETUTCDATE()),
    ( 9, N'20', N'Cesar',                1, 1, N'SYSTEM', GETUTCDATE()),
    (10, N'23', N'Cordoba',              1, 1, N'SYSTEM', GETUTCDATE()),
    (11, N'25', N'Cundinamarca',          1, 1, N'SYSTEM', GETUTCDATE()),
    (12, N'27', N'Choco',                1, 1, N'SYSTEM', GETUTCDATE()),
    (13, N'41', N'Huila',                1, 1, N'SYSTEM', GETUTCDATE()),
    (14, N'44', N'La Guajira',           1, 1, N'SYSTEM', GETUTCDATE()),
    (15, N'47', N'Magdalena',            1, 1, N'SYSTEM', GETUTCDATE()),
    (16, N'50', N'Meta',                 1, 1, N'SYSTEM', GETUTCDATE()),
    (17, N'52', N'Narino',               1, 1, N'SYSTEM', GETUTCDATE()),
    (18, N'54', N'Norte de Santander',   1, 1, N'SYSTEM', GETUTCDATE()),
    (19, N'63', N'Quindio',              1, 1, N'SYSTEM', GETUTCDATE()),
    (20, N'66', N'Risaralda',            1, 1, N'SYSTEM', GETUTCDATE()),
    (21, N'68', N'Santander',            1, 1, N'SYSTEM', GETUTCDATE()),
    (22, N'70', N'Sucre',                1, 1, N'SYSTEM', GETUTCDATE()),
    (23, N'73', N'Tolima',               1, 1, N'SYSTEM', GETUTCDATE()),
    (24, N'76', N'Valle del Cauca',      1, 1, N'SYSTEM', GETUTCDATE())
) AS src ([Id], [DaneCode], [Name], [CountryId], [IsActive], [CreatedBy], [CreatedAt])
ON tgt.[DaneCode] = src.[DaneCode]
WHEN NOT MATCHED THEN
    INSERT ([Id], [DaneCode], [Name], [CountryId], [IsActive], [CreatedBy], [CreatedAt])
    VALUES (src.[Id], src.[DaneCode], src.[Name], src.[CountryId], src.[IsActive], src.[CreatedBy], src.[CreatedAt]);

SET IDENTITY_INSERT [dbo].[COR_Departments] OFF;
PRINT N'Seed: COR_Departments - 24 rows.';
GO

-- 2c. Cities (20 major cities)
SET IDENTITY_INSERT [dbo].[COR_Cities] ON;

MERGE INTO [dbo].[COR_Cities] AS tgt
USING (VALUES
    ( 1, N'11001', N'Bogota',          3,  1, N'SYSTEM', GETUTCDATE()),   -- Bogota D.C.
    ( 2, N'05001', N'Medellin',        1,  1, N'SYSTEM', GETUTCDATE()),   -- Antioquia
    ( 3, N'76001', N'Cali',            24, 1, N'SYSTEM', GETUTCDATE()),   -- Valle del Cauca
    ( 4, N'08001', N'Barranquilla',    2,  1, N'SYSTEM', GETUTCDATE()),   -- Atlantico
    ( 5, N'13001', N'Cartagena',       4,  1, N'SYSTEM', GETUTCDATE()),   -- Bolivar
    ( 6, N'54001', N'Cucuta',          18, 1, N'SYSTEM', GETUTCDATE()),   -- Norte de Santander
    ( 7, N'68001', N'Bucaramanga',     21, 1, N'SYSTEM', GETUTCDATE()),   -- Santander
    ( 8, N'66001', N'Pereira',         20, 1, N'SYSTEM', GETUTCDATE()),   -- Risaralda
    ( 9, N'17001', N'Manizales',       6,  1, N'SYSTEM', GETUTCDATE()),   -- Caldas
    (10, N'73001', N'Ibague',          23, 1, N'SYSTEM', GETUTCDATE()),   -- Tolima
    (11, N'47001', N'Santa Marta',     15, 1, N'SYSTEM', GETUTCDATE()),   -- Magdalena
    (12, N'50001', N'Villavicencio',   16, 1, N'SYSTEM', GETUTCDATE()),   -- Meta
    (13, N'52001', N'Pasto',           17, 1, N'SYSTEM', GETUTCDATE()),   -- Narino
    (14, N'23001', N'Monteria',        10, 1, N'SYSTEM', GETUTCDATE()),   -- Cordoba
    (15, N'41001', N'Neiva',           13, 1, N'SYSTEM', GETUTCDATE()),   -- Huila
    (16, N'63001', N'Armenia',         19, 1, N'SYSTEM', GETUTCDATE()),   -- Quindio
    (17, N'19001', N'Popayan',         8,  1, N'SYSTEM', GETUTCDATE()),   -- Cauca
    (18, N'20001', N'Valledupar',      9,  1, N'SYSTEM', GETUTCDATE()),   -- Cesar
    (19, N'70001', N'Sincelejo',       22, 1, N'SYSTEM', GETUTCDATE()),   -- Sucre
    (20, N'15001', N'Tunja',           5,  1, N'SYSTEM', GETUTCDATE())    -- Boyaca
) AS src ([Id], [DaneCode], [Name], [DepartmentId], [IsActive], [CreatedBy], [CreatedAt])
ON tgt.[DaneCode] = src.[DaneCode]
WHEN NOT MATCHED THEN
    INSERT ([Id], [DaneCode], [Name], [DepartmentId], [IsActive], [CreatedBy], [CreatedAt])
    VALUES (src.[Id], src.[DaneCode], src.[Name], src.[DepartmentId], src.[IsActive], src.[CreatedBy], src.[CreatedAt]);

SET IDENTITY_INSERT [dbo].[COR_Cities] OFF;
PRINT N'Seed: COR_Cities - 20 rows (major Colombian cities).';
GO

-- ============================================================
-- 3. Major Colombian Banks
-- ============================================================
SET IDENTITY_INSERT [dbo].[COR_Banks] ON;

MERGE INTO [dbo].[COR_Banks] AS tgt
USING (VALUES
    ( 1, N'007', N'Bancolombia',                1, N'SYSTEM', GETUTCDATE()),
    ( 2, N'001', N'Banco de Bogota',            1, N'SYSTEM', GETUTCDATE()),
    ( 3, N'051', N'Davivienda',                 1, N'SYSTEM', GETUTCDATE()),
    ( 4, N'013', N'BBVA Colombia',              1, N'SYSTEM', GETUTCDATE()),
    ( 5, N'023', N'Banco de Occidente',         1, N'SYSTEM', GETUTCDATE()),
    ( 6, N'002', N'Banco Popular',              1, N'SYSTEM', GETUTCDATE()),
    ( 7, N'052', N'Banco AV Villas',            1, N'SYSTEM', GETUTCDATE()),
    ( 8, N'032', N'Banco Caja Social',          1, N'SYSTEM', GETUTCDATE()),
    ( 9, N'019', N'Scotiabank Colpatria',       1, N'SYSTEM', GETUTCDATE()),
    (10, N'040', N'Banco Agrario',              1, N'SYSTEM', GETUTCDATE()),
    (11, N'000', N'Banco de la Republica',      1, N'SYSTEM', GETUTCDATE()),
    (12, N'006', N'Itau',                       1, N'SYSTEM', GETUTCDATE()),
    (13, N'009', N'Citibank Colombia',          1, N'SYSTEM', GETUTCDATE()),
    (14, N'012', N'GNB Sudameris',              1, N'SYSTEM', GETUTCDATE()),
    (15, N'060', N'Banco Pichincha',            1, N'SYSTEM', GETUTCDATE())
) AS src ([Id], [BankCode], [Name], [IsActive], [CreatedBy], [CreatedAt])
ON tgt.[BankCode] = src.[BankCode]
WHEN NOT MATCHED THEN
    INSERT ([Id], [BankCode], [Name], [IsActive], [CreatedBy], [CreatedAt])
    VALUES (src.[Id], src.[BankCode], src.[Name], src.[IsActive], src.[CreatedBy], src.[CreatedAt]);

SET IDENTITY_INSERT [dbo].[COR_Banks] OFF;
PRINT N'Seed: COR_Banks - 15 rows (major Colombian banks).';
GO

-- ============================================================
-- 4. Base Roles
-- ============================================================
SET IDENTITY_INSERT [dbo].[SEC_Roles] ON;

MERGE INTO [dbo].[SEC_Roles] AS tgt
USING (VALUES
    (1, N'Admin',            N'System administrator — full access',           1, N'SYSTEM', GETUTCDATE()),
    (2, N'Auditor',          N'Financial auditor — read-only with audit logs', 1, N'SYSTEM', GETUTCDATE()),
    (3, N'Accountant',       N'Accounting operations',                         1, N'SYSTEM', GETUTCDATE()),
    (4, N'Cashier',          N'Teller and cashier operations',                 1, N'SYSTEM', GETUTCDATE()),
    (5, N'LoanOfficer',      N'Loan portfolio management',                     1, N'SYSTEM', GETUTCDATE()),
    (6, N'PayrollAdmin',     N'Payroll management',                            1, N'SYSTEM', GETUTCDATE()),
    (7, N'InventoryManager', N'Inventory operations',                          1, N'SYSTEM', GETUTCDATE()),
    (8, N'ReadOnly',         N'View-only access — no write permissions',       1, N'SYSTEM', GETUTCDATE())
) AS src ([Id], [Name], [Description], [IsActive], [CreatedBy], [CreatedAt])
ON tgt.[Name] = src.[Name]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Name], [Description], [IsActive], [CreatedBy], [CreatedAt])
    VALUES (src.[Id], src.[Name], src.[Description], src.[IsActive], src.[CreatedBy], src.[CreatedAt]);

SET IDENTITY_INSERT [dbo].[SEC_Roles] OFF;
PRINT N'Seed: SEC_Roles - 8 rows.';
GO

-- ============================================================
-- 5. Base Permissions
-- ============================================================
SET IDENTITY_INSERT [dbo].[SEC_Permissions] ON;

MERGE INTO [dbo].[SEC_Permissions] AS tgt
USING (VALUES
    ( 1, N'COR', N'Read',           N'Read access to core/associate data'),
    ( 2, N'COR', N'Write',          N'Create and update core/associate data'),
    ( 3, N'COR', N'Delete',         N'Delete (soft) core/associate data'),
    ( 4, N'ACC', N'Read',           N'Read access to accounting data'),
    ( 5, N'ACC', N'Write',          N'Create and update accounting entries'),
    ( 6, N'LND', N'Read',           N'Read access to lending/portfolio data'),
    ( 7, N'LND', N'Write',          N'Create and update loan operations'),
    ( 8, N'LND', N'ApproveLoan',    N'Approve loan applications'),
    ( 9, N'PAY', N'Read',           N'Read access to payroll data'),
    (10, N'PAY', N'Write',          N'Create and update payroll records'),
    (11, N'PAY', N'RunPayroll',     N'Execute payroll liquidation'),
    (12, N'INV', N'Read',           N'Read access to inventory data'),
    (13, N'INV', N'Write',          N'Create and update inventory records'),
    (14, N'INV', N'AdjustStock',    N'Perform stock adjustments'),
    (15, N'SEC', N'ManageUsers',    N'Create, update, deactivate users'),
    (16, N'SEC', N'ManageRoles',    N'Create and assign roles and permissions'),
    (17, N'AUD', N'Read',           N'Read audit log entries'),
    (18, N'ADM', N'ManageTenants',  N'Create and configure tenants')
) AS src ([Id], [Module], [Action], [Description])
ON tgt.[Module] = src.[Module] AND tgt.[Action] = src.[Action]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Module], [Action], [Description], [CreatedBy], [CreatedAt])
    VALUES (src.[Id], src.[Module], src.[Action], src.[Description], N'SYSTEM', GETUTCDATE());

SET IDENTITY_INSERT [dbo].[SEC_Permissions] OFF;
PRINT N'Seed: SEC_Permissions - 18 rows.';
GO

-- ============================================================
-- 5b. Default Role-Permission Assignments
-- Admin gets ALL permissions. Other roles get subsets.
-- ============================================================
MERGE INTO [dbo].[SEC_RolePermissions] AS tgt
USING (
    -- Admin (RoleId=1) → all 18 permissions
    SELECT 1 AS RoleId, Id AS PermissionId FROM [dbo].[SEC_Permissions]
    UNION ALL
    -- Auditor (RoleId=2) → COR.Read, ACC.Read, LND.Read, PAY.Read, INV.Read, AUD.Read
    SELECT 2, Id FROM [dbo].[SEC_Permissions] WHERE [Action] = N'Read'
    UNION ALL
    -- Accountant (RoleId=3) → ACC.Read, ACC.Write, COR.Read
    SELECT 3, Id FROM [dbo].[SEC_Permissions] WHERE ([Module] = N'ACC') OR ([Module] = N'COR' AND [Action] = N'Read')
    UNION ALL
    -- Cashier (RoleId=4) → COR.Read, LND.Read, LND.Write, INV.Read, INV.Write
    SELECT 4, Id FROM [dbo].[SEC_Permissions] WHERE ([Module] = N'COR' AND [Action] = N'Read')
        OR ([Module] = N'LND' AND [Action] IN (N'Read', N'Write'))
        OR ([Module] = N'INV' AND [Action] IN (N'Read', N'Write'))
    UNION ALL
    -- LoanOfficer (RoleId=5) → COR.Read, COR.Write, LND.Read, LND.Write, LND.ApproveLoan
    SELECT 5, Id FROM [dbo].[SEC_Permissions] WHERE ([Module] = N'COR' AND [Action] IN (N'Read', N'Write'))
        OR ([Module] = N'LND')
    UNION ALL
    -- PayrollAdmin (RoleId=6) → COR.Read, PAY.Read, PAY.Write, PAY.RunPayroll
    SELECT 6, Id FROM [dbo].[SEC_Permissions] WHERE ([Module] = N'COR' AND [Action] = N'Read')
        OR ([Module] = N'PAY')
    UNION ALL
    -- InventoryManager (RoleId=7) → COR.Read, INV.Read, INV.Write, INV.AdjustStock
    SELECT 7, Id FROM [dbo].[SEC_Permissions] WHERE ([Module] = N'COR' AND [Action] = N'Read')
        OR ([Module] = N'INV')
    UNION ALL
    -- ReadOnly (RoleId=8) → all Read permissions
    SELECT 8, Id FROM [dbo].[SEC_Permissions] WHERE [Action] = N'Read'
) AS src ([RoleId], [PermissionId])
ON tgt.[RoleId] = src.[RoleId] AND tgt.[PermissionId] = src.[PermissionId]
WHEN NOT MATCHED THEN
    INSERT ([RoleId], [PermissionId], [CreatedBy], [CreatedAt])
    VALUES (src.[RoleId], src.[PermissionId], N'SYSTEM', GETUTCDATE());

PRINT N'Seed: SEC_RolePermissions - default role-permission assignments.';
GO

-- ============================================================
-- Summary
-- ============================================================
PRINT N'';
PRINT N'============================================================';
PRINT N'IngenIA365ERP Seed Data Complete';
PRINT N'  - Identification Types:  7 rows';
PRINT N'  - Countries:             1 row  (Colombia)';
PRINT N'  - Departments:          24 rows';
PRINT N'  - Cities:               20 rows (major cities)';
PRINT N'  - Banks:                15 rows (major Colombian banks)';
PRINT N'  - Roles:                 8 rows';
PRINT N'  - Permissions:          18 rows';
PRINT N'  - Role-Permissions:     ~70 assignments';
PRINT N'============================================================';
GO
