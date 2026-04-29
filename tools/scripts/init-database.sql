-- ============================================================
-- IngenIA365ERP - Inicializacion de Base de Datos
-- Ejecutar contra la instancia SQL Server como SA
-- ============================================================

-- Crear BD principal si no existe
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'IngenIA365ERP')
BEGIN
    CREATE DATABASE [IngenIA365ERP];
    PRINT 'Base de datos IngenIA365ERP creada.';
END
ELSE
    PRINT 'Base de datos IngenIA365ERP ya existe.';
GO

-- Crear BD admin para tenants
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'IngenIA365ERP_Admin')
BEGIN
    CREATE DATABASE [IngenIA365ERP_Admin];
    PRINT 'Base de datos IngenIA365ERP_Admin creada.';
END
ELSE
    PRINT 'Base de datos IngenIA365ERP_Admin ya existe.';
GO

-- ============================================================
-- Configurar BD Admin con tablas de multi-tenancy
-- ============================================================
USE [IngenIA365ERP_Admin];
GO

-- ADM_Tenants
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ADM_Tenants')
BEGIN
    CREATE TABLE [dbo].[ADM_Tenants] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Identifier]         NVARCHAR(100) NOT NULL,
        [Name]               NVARCHAR(200) NOT NULL,
        [ConnectionString]   NVARCHAR(500) NULL,
        [SchemaName]         NVARCHAR(100) NOT NULL,
        [IsActive]           BIT NOT NULL DEFAULT 1,
        [LicenseType]        NVARCHAR(50) NOT NULL DEFAULT 'Standard',
        [MaxUsers]           INT NOT NULL DEFAULT 50,
        [ExpirationDate]     DATETIME2 NULL,
        [CreatedAt]          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]          NVARCHAR(100) NULL,
        [UpdatedAt]          DATETIME2 NULL,
        [UpdatedBy]          NVARCHAR(100) NULL,
        [IsDeleted]          BIT NOT NULL DEFAULT 0,
        [DeletedAt]          DATETIME2 NULL,
        [DeletedBy]          NVARCHAR(100) NULL,
        CONSTRAINT [PK_ADM_Tenants] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_ADM_Tenants_PublicId] UNIQUE ([PublicId]),
        CONSTRAINT [UQ_ADM_Tenants_Identifier] UNIQUE ([Identifier])
    );
    PRINT 'Tabla ADM_Tenants creada.';
END
GO

-- ADM_Subscriptions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ADM_Subscriptions')
BEGIN
    CREATE TABLE [dbo].[ADM_Subscriptions] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [TenantId]           INT NOT NULL,
        [PlanName]           NVARCHAR(100) NOT NULL,
        [StartDate]          DATETIME2 NOT NULL,
        [EndDate]            DATETIME2 NULL,
        [IsActive]           BIT NOT NULL DEFAULT 1,
        [MonthlyPrice]       DECIMAL(18,2) NOT NULL DEFAULT 0,
        [MaxTransactions]    INT NOT NULL DEFAULT 100000,
        [CreatedAt]          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]          NVARCHAR(100) NULL,
        [UpdatedAt]          DATETIME2 NULL,
        [UpdatedBy]          NVARCHAR(100) NULL,
        [IsDeleted]          BIT NOT NULL DEFAULT 0,
        [DeletedAt]          DATETIME2 NULL,
        [DeletedBy]          NVARCHAR(100) NULL,
        CONSTRAINT [PK_ADM_Subscriptions] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_ADM_Subscriptions_PublicId] UNIQUE ([PublicId]),
        CONSTRAINT [FK_ADM_Subscriptions_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants]([Id])
    );
    PRINT 'Tabla ADM_Subscriptions creada.';
END
GO

-- ADM_TenantSettings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ADM_TenantSettings')
BEGIN
    CREATE TABLE [dbo].[ADM_TenantSettings] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [TenantId]           INT NOT NULL,
        [SettingKey]         NVARCHAR(200) NOT NULL,
        [SettingValue]       NVARCHAR(MAX) NULL,
        [Category]           NVARCHAR(100) NOT NULL DEFAULT 'General',
        [CreatedAt]          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]          NVARCHAR(100) NULL,
        [UpdatedAt]          DATETIME2 NULL,
        [UpdatedBy]          NVARCHAR(100) NULL,
        [IsDeleted]          BIT NOT NULL DEFAULT 0,
        [DeletedAt]          DATETIME2 NULL,
        [DeletedBy]          NVARCHAR(100) NULL,
        CONSTRAINT [PK_ADM_TenantSettings] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_ADM_TenantSettings_PublicId] UNIQUE ([PublicId]),
        CONSTRAINT [UQ_ADM_TenantSettings_Key] UNIQUE ([TenantId], [SettingKey]),
        CONSTRAINT [FK_ADM_TenantSettings_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants]([Id])
    );
    PRINT 'Tabla ADM_TenantSettings creada.';
END
GO

PRINT '=== Inicializacion de base de datos completada ===';
GO
