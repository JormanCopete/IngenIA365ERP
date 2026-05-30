-- =============================================================================
-- File:        20_SEC_LoginAttempts_BackfillColumns.sql
-- Phase:       Fase 0 — Cimientos técnicos · US1 (fix runtime login)
-- Idempotent:  YES (sys.columns guards).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Contexto:
--   El LoginCommandHandler persiste cada intento en SEC_LoginAttempts (entidad
--   AuditableEntityLong → exige Id BIGINT, PublicId, audit cols, RowVersion).
--   Algunos entornos tenían la tabla con columnas mínimas (solo Email, IpAddress,
--   UserAgent, AttemptedAt, FailureReason) — al guardar EF falla con "Invalid
--   column name 'WasSuccessful' / 'PublicId' / 'CreatedAt' / 'RowVersion' / …".
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 20_SEC_LoginAttempts_BackfillColumns.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_LoginAttempts')
BEGIN
    RAISERROR('La tabla SEC_LoginAttempts no existe. Crea el schema base antes de correr esta migración.', 16, 1);
    RETURN;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PublicId' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD PublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_LoginAttempts_PublicId DEFAULT NEWID();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'WasSuccessful' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD WasSuccessful BIT NOT NULL CONSTRAINT DF_SEC_LoginAttempts_WasSuccessful DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'FailureReason' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD FailureReason NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedAt' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_LoginAttempts_CreatedAt DEFAULT SYSUTCDATETIME();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedBy' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_SEC_LoginAttempts_CreatedBy DEFAULT N'SYSTEM';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD UpdatedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedBy' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD UpdatedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsDeleted' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_LoginAttempts_IsDeleted DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DeletedAt' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD DeletedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DeletedBy' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD DeletedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RowVersion' AND Object_ID = OBJECT_ID('dbo.SEC_LoginAttempts'))
    ALTER TABLE dbo.SEC_LoginAttempts ADD RowVersion ROWVERSION NOT NULL;
GO

PRINT 'SEC_LoginAttempts backfill: OK';
GO
