-- =============================================================================
-- File:        18_SEC_Users_BackfillColumns.sql
-- Phase:       Fase 0 — Cimientos técnicos · US1 (fix runtime seed)
-- Idempotent:  YES (sys.columns guards).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--   SEC_Users vive en la BD operacional, junto al resto de SEC_* y COR_*.
--
-- Backfill: el entorno de un desarrollador tenía una versión antigua de
-- SEC_Users sin todas las columnas que la entidad User declara hoy. EF
-- genera SELECT con TODAS las columnas → SqlException 207 al arrancar el
-- API por el seeder T047 (DomainSecuritySeedData).
--
-- Esta migración añade cualquier columna faltante con su default razonable.
-- Se puede ejecutar tantas veces como haga falta: cada bloque está
-- guardado contra sys.columns.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 18_SEC_Users_BackfillColumns.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Users')
BEGIN
    RAISERROR('La tabla SEC_Users no existe. Crea el schema base antes de ejecutar esta migración.', 16, 1);
    RETURN;
END;
GO

-- === Columnas históricas de la entidad User ===

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PersonId' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD PersonId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PasswordSalt' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD PasswordSalt NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsEmailVerified' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD IsEmailVerified BIT NOT NULL CONSTRAINT DF_SEC_Users_IsEmailVerified DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsMfaEnabled' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD IsMfaEnabled BIT NOT NULL CONSTRAINT DF_SEC_Users_IsMfaEnabled DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MfaSecret' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD MfaSecret NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'LastPasswordChangeAt' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD LastPasswordChangeAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'LockoutEndAt' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD LockoutEndAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'LegacyLogin' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD LegacyLogin NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CanApproveLoansMin' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD CanApproveLoansMin DECIMAL(18,2) NOT NULL CONSTRAINT DF_SEC_Users_CanApproveMin DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CanApproveLoansMax' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD CanApproveLoansMax DECIMAL(18,2) NOT NULL CONSTRAINT DF_SEC_Users_CanApproveMax DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CanOverrideLimits' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD CanOverrideLimits BIT NOT NULL CONSTRAINT DF_SEC_Users_CanOverrideLimits DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IdentificationNumber' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD IdentificationNumber NVARCHAR(20) NULL;
GO

-- === Columnas nuevas Phase 0 / US1 (T011 + T047) ===

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsSaasOperator' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD IsSaasOperator BIT NOT NULL CONSTRAINT DF_SEC_Users_IsSaasOperator DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MustChangePassword' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_SEC_Users_MustChangePassword DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RowVersion' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    ALTER TABLE dbo.SEC_Users ADD RowVersion ROWVERSION NOT NULL;
GO

PRINT 'SEC_Users backfill: OK';
GO
