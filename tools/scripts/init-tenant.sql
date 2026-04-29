-- ============================================================
-- IngenIA365ERP - Crear tenant de desarrollo
-- Ejecutar contra IngenIA365ERP despues de init-database.sql
-- ============================================================

USE [IngenIA365ERP_Admin];
GO

-- Insertar tenant de desarrollo si no existe
IF NOT EXISTS (SELECT 1 FROM [dbo].[ADM_Tenants] WHERE [Identifier] = 'dev_tenant')
BEGIN
    INSERT INTO [dbo].[ADM_Tenants]
        ([Identifier], [Name], [SchemaName], [LicenseType], [MaxUsers], [CreatedBy])
    VALUES
        ('dev_tenant', 'Cooperativa Demo Desarrollo', 'dev_tenant', 'Enterprise', 100, 'system');

    INSERT INTO [dbo].[ADM_Subscriptions]
        ([TenantId], [PlanName], [StartDate], [EndDate], [MonthlyPrice], [MaxTransactions], [CreatedBy])
    SELECT [Id], 'Enterprise', SYSUTCDATETIME(), DATEADD(YEAR, 1, SYSUTCDATETIME()), 0, 999999, 'system'
    FROM [dbo].[ADM_Tenants] WHERE [Identifier] = 'dev_tenant';

    PRINT 'Tenant de desarrollo creado.';
END
ELSE
    PRINT 'Tenant de desarrollo ya existe.';
GO

-- ============================================================
-- Crear schema del tenant en BD principal
-- ============================================================
USE [IngenIA365ERP];
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dev_tenant')
BEGIN
    EXEC('CREATE SCHEMA [dev_tenant]');
    PRINT 'Schema [dev_tenant] creado en IngenIA365ERP.';
END
ELSE
    PRINT 'Schema [dev_tenant] ya existe.';
GO

-- ============================================================
-- Seed data basico para desarrollo
-- ============================================================

-- Ciudades principales de Colombia (en schema dev_tenant)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dev_tenant' AND TABLE_NAME = 'COR_Cities')
BEGIN
    PRINT 'NOTA: Las tablas del tenant se crean ejecutando los scripts DDL (Schema_01 al Schema_09).';
    PRINT 'Ejecute primero los archivos IngenIA365ERP_Schema_01_COR.sql al Schema_09_Indexes.sql';
    PRINT 'reemplazando [dbo] por [dev_tenant] en cada script.';
END
GO

-- Si las tablas ya existen, insertar seed data
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dev_tenant' AND TABLE_NAME = 'COR_Cities')
BEGIN
    -- Departamentos
    IF NOT EXISTS (SELECT 1 FROM [dev_tenant].[COR_Departments] WHERE [Code] = '11')
    BEGIN
        SET IDENTITY_INSERT [dev_tenant].[COR_Departments] ON;
        INSERT INTO [dev_tenant].[COR_Departments] ([Id], [Code], [Name], [CreatedAt], [CreatedBy])
        VALUES
            (1, '05', 'Antioquia', SYSUTCDATETIME(), 'seed'),
            (2, '08', 'Atlantico', SYSUTCDATETIME(), 'seed'),
            (3, '11', 'Bogota D.C.', SYSUTCDATETIME(), 'seed'),
            (4, '13', 'Bolivar', SYSUTCDATETIME(), 'seed'),
            (5, '15', 'Boyaca', SYSUTCDATETIME(), 'seed'),
            (6, '17', 'Caldas', SYSUTCDATETIME(), 'seed'),
            (7, '19', 'Cauca', SYSUTCDATETIME(), 'seed'),
            (8, '25', 'Cundinamarca', SYSUTCDATETIME(), 'seed'),
            (9, '27', 'Choco', SYSUTCDATETIME(), 'seed'),
            (10, '41', 'Huila', SYSUTCDATETIME(), 'seed'),
            (11, '44', 'La Guajira', SYSUTCDATETIME(), 'seed'),
            (12, '47', 'Magdalena', SYSUTCDATETIME(), 'seed'),
            (13, '50', 'Meta', SYSUTCDATETIME(), 'seed'),
            (14, '52', 'Narino', SYSUTCDATETIME(), 'seed'),
            (15, '54', 'Norte de Santander', SYSUTCDATETIME(), 'seed'),
            (16, '63', 'Quindio', SYSUTCDATETIME(), 'seed'),
            (17, '66', 'Risaralda', SYSUTCDATETIME(), 'seed'),
            (18, '68', 'Santander', SYSUTCDATETIME(), 'seed'),
            (19, '73', 'Tolima', SYSUTCDATETIME(), 'seed'),
            (20, '76', 'Valle del Cauca', SYSUTCDATETIME(), 'seed');
        SET IDENTITY_INSERT [dev_tenant].[COR_Departments] OFF;
        PRINT 'Departamentos insertados.';
    END

    -- Ciudades principales
    IF NOT EXISTS (SELECT 1 FROM [dev_tenant].[COR_Cities] WHERE [Code] = '11001')
    BEGIN
        SET IDENTITY_INSERT [dev_tenant].[COR_Cities] ON;
        INSERT INTO [dev_tenant].[COR_Cities] ([Id], [Code], [Name], [DepartmentId], [CreatedAt], [CreatedBy])
        VALUES
            (1, '11001', 'Bogota', 3, SYSUTCDATETIME(), 'seed'),
            (2, '05001', 'Medellin', 1, SYSUTCDATETIME(), 'seed'),
            (3, '76001', 'Cali', 20, SYSUTCDATETIME(), 'seed'),
            (4, '08001', 'Barranquilla', 2, SYSUTCDATETIME(), 'seed'),
            (5, '13001', 'Cartagena', 4, SYSUTCDATETIME(), 'seed'),
            (6, '68001', 'Bucaramanga', 18, SYSUTCDATETIME(), 'seed'),
            (7, '50001', 'Villavicencio', 13, SYSUTCDATETIME(), 'seed'),
            (8, '66001', 'Pereira', 17, SYSUTCDATETIME(), 'seed'),
            (9, '17001', 'Manizales', 6, SYSUTCDATETIME(), 'seed'),
            (10, '41001', 'Neiva', 10, SYSUTCDATETIME(), 'seed');
        SET IDENTITY_INSERT [dev_tenant].[COR_Cities] OFF;
        PRINT 'Ciudades insertadas.';
    END

    -- Bancos principales
    IF NOT EXISTS (SELECT 1 FROM [dev_tenant].[COR_Banks] WHERE [Code] = '001')
    BEGIN
        SET IDENTITY_INSERT [dev_tenant].[COR_Banks] ON;
        INSERT INTO [dev_tenant].[COR_Banks] ([Id], [Code], [Name], [CreatedAt], [CreatedBy])
        VALUES
            (1, '001', 'Banco de Bogota', SYSUTCDATETIME(), 'seed'),
            (2, '002', 'Banco Popular', SYSUTCDATETIME(), 'seed'),
            (3, '006', 'Banco Itau Corpbanca', SYSUTCDATETIME(), 'seed'),
            (4, '007', 'Bancolombia', SYSUTCDATETIME(), 'seed'),
            (5, '009', 'Citibank', SYSUTCDATETIME(), 'seed'),
            (6, '012', 'Banco GNB Sudameris', SYSUTCDATETIME(), 'seed'),
            (7, '013', 'BBVA Colombia', SYSUTCDATETIME(), 'seed'),
            (8, '019', 'Scotiabank Colpatria', SYSUTCDATETIME(), 'seed'),
            (9, '023', 'Banco de Occidente', SYSUTCDATETIME(), 'seed'),
            (10, '032', 'Banco Caja Social', SYSUTCDATETIME(), 'seed'),
            (11, '040', 'Banco Agrario', SYSUTCDATETIME(), 'seed'),
            (12, '051', 'Davivienda', SYSUTCDATETIME(), 'seed'),
            (13, '052', 'Banco AV Villas', SYSUTCDATETIME(), 'seed'),
            (14, '058', 'Banco Procredit', SYSUTCDATETIME(), 'seed'),
            (15, '060', 'Banco Pichincha', SYSUTCDATETIME(), 'seed'),
            (16, '061', 'Bancoomeva', SYSUTCDATETIME(), 'seed'),
            (17, '062', 'Banco Falabella', SYSUTCDATETIME(), 'seed'),
            (18, '063', 'Banco Finandina', SYSUTCDATETIME(), 'seed'),
            (19, '065', 'Banco Santander', SYSUTCDATETIME(), 'seed'),
            (20, '066', 'Banco Cooperativo Coopcentral', SYSUTCDATETIME(), 'seed');
        SET IDENTITY_INSERT [dev_tenant].[COR_Banks] OFF;
        PRINT 'Bancos insertados.';
    END
END
GO

PRINT '=== Inicializacion de tenant de desarrollo completada ===';
GO
