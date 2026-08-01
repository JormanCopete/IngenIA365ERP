-- =============================================================================
-- File:        15c_Admin_MfaPolicy_LoginAttempts.sql
-- Phase:       Fase 1 — Identidad central · feature 002-identidad-central-federada
-- Idempotent:  YES.  Reversible:  Sí (DROP TABLE seguro).
-- Prerequisite: 15a aplicado (FK opcional a ADM_CentralUsers en LoginAttempts).
--
-- DATABASE TARGET: IngenIA365ERP_Admin
--
-- Tablas:
--   - ADM_TenantMfaPolicies          (FR-003a a FR-003d) — política "MFA obligatorio"
--                                     por tenant, single-row por TenantId.
--   - ADM_CentralUserLoginAttempts   (FR-035, FR-042, FR-046) — telemetría
--                                     APPEND-ONLY de intentos (éxito + fallo).
--                                     Sin soft-delete por diseño; retención 1 año
--                                     vía job de limpieza (T122 análogo).
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP_Admin -i 15c_Admin_MfaPolicy_LoginAttempts.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-------------------------------------------------------------------------------
-- ADM_TenantMfaPolicies
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_TenantMfaPolicies')
BEGIN
    CREATE TABLE dbo.ADM_TenantMfaPolicies
    (
        Id                          INT              NOT NULL IDENTITY(1,1) CONSTRAINT PK_ADM_TenantMfaPolicies PRIMARY KEY,
        PublicId                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ADM_TenantMfaPolicies_PublicId DEFAULT (NEWID()),

        TenantId                    UNIQUEIDENTIFIER NOT NULL,
        IsRequired                  BIT              NOT NULL CONSTRAINT DF_ADM_TenantMfaPolicies_IsRequired DEFAULT (0),
        ActivatedAt                 DATETIME2        NULL,
        ActivatedByUserId           UNIQUEIDENTIFIER NULL,
        DeactivatedAt               DATETIME2        NULL,
        DeactivatedByUserId         UNIQUEIDENTIFIER NULL,

        -- AuditableEntity
        CreatedAt                   DATETIME2        NOT NULL CONSTRAINT DF_ADM_TenantMfaPolicies_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy                   NVARCHAR(256)    NULL,
        UpdatedAt                   DATETIME2        NULL,
        UpdatedBy                   NVARCHAR(256)    NULL,
        IsDeleted                   BIT              NOT NULL CONSTRAINT DF_ADM_TenantMfaPolicies_IsDeleted DEFAULT (0),
        DeletedAt                   DATETIME2        NULL,
        DeletedBy                   NVARCHAR(256)    NULL,
        RowVersion                  ROWVERSION       NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_TenantMfaPolicies_PublicId' AND object_id = OBJECT_ID('dbo.ADM_TenantMfaPolicies'))
    CREATE UNIQUE INDEX UX_ADM_TenantMfaPolicies_PublicId ON dbo.ADM_TenantMfaPolicies (PublicId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_TenantMfaPolicies_TenantId' AND object_id = OBJECT_ID('dbo.ADM_TenantMfaPolicies'))
    CREATE UNIQUE INDEX UX_ADM_TenantMfaPolicies_TenantId ON dbo.ADM_TenantMfaPolicies (TenantId)
        WHERE IsDeleted = 0;
GO

-------------------------------------------------------------------------------
-- ADM_CentralUserLoginAttempts  (append-only telemetry)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_CentralUserLoginAttempts')
BEGIN
    CREATE TABLE dbo.ADM_CentralUserLoginAttempts
    (
        Id                      BIGINT           NOT NULL IDENTITY(1,1) CONSTRAINT PK_ADM_CentralUserLoginAttempts PRIMARY KEY,
        CentralUserId           UNIQUEIDENTIFIER NULL,  -- NULL si email no corresponde a CentralUser existente
        NormalizedEmail         NVARCHAR(256)    NOT NULL,
        Result                  INT              NOT NULL,  -- enum LoginAttemptResult
        IpAddress               NVARCHAR(45)     NULL,
        UserAgent               NVARCHAR(512)    NULL,
        Timestamp               DATETIME2        NOT NULL CONSTRAINT DF_ADM_LoginAttempts_Timestamp DEFAULT (SYSUTCDATETIME()),
        LockoutAppliedSeconds   INT              NULL,

        CONSTRAINT FK_ADM_CentralUserLoginAttempts_User
            FOREIGN KEY (CentralUserId) REFERENCES dbo.ADM_CentralUsers (Id)
    );
END
GO

-- Análisis de fuerza bruta por email.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_LoginAttempts_Email_Timestamp' AND object_id = OBJECT_ID('dbo.ADM_CentralUserLoginAttempts'))
    CREATE INDEX IX_ADM_LoginAttempts_Email_Timestamp ON dbo.ADM_CentralUserLoginAttempts (NormalizedEmail, Timestamp DESC)
        INCLUDE (Result);
GO

-- Análisis forense por IP.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_LoginAttempts_Ip_Timestamp' AND object_id = OBJECT_ID('dbo.ADM_CentralUserLoginAttempts'))
    CREATE INDEX IX_ADM_LoginAttempts_Ip_Timestamp ON dbo.ADM_CentralUserLoginAttempts (IpAddress, Timestamp DESC)
        WHERE IpAddress IS NOT NULL;
GO

PRINT '15c_Admin_MfaPolicy_LoginAttempts.sql applied: ADM_TenantMfaPolicies + ADM_CentralUserLoginAttempts.';
GO
