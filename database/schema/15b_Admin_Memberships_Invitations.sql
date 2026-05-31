-- =============================================================================
-- File:        15b_Admin_Memberships_Invitations.sql
-- Phase:       Fase 1 — Identidad central · feature 002-identidad-central-federada
-- Idempotent:  YES.  Reversible:  Sí (DROP en orden inverso de FKs).
-- Prerequisite: 15a_Admin_CentralIdentity.sql aplicado (FK a ADM_CentralUsers).
--
-- DATABASE TARGET: IngenIA365ERP_Admin
--
-- Tablas:
--   - ADM_TenantMemberships  (FR-009 / FR-009a) — relación N:N persona↔empresa
--                             con máquina de estados Invited/Active/Suspended/Revoked.
--                             UNIQUE (CentralUserId, TenantId): una membresía por par.
--                             Índice filtrado para chequeo rápido del "último admin"
--                             (FR-040).
--   - ADM_Invitations        (FR-027 a FR-031) — token de un solo uso (hash SHA-256).
--                             UNIQUE TokenHash; índices por (NormalizedEmail, TenantId,
--                             Status) y (Status, ExpiresAt) para el InvitationExpiryJob.
--
-- TenantId NO declara FK física a ADM_Tenants para mantener flexibilidad de
-- multi-BD (algunos despliegues pueden separar ADM_Tenants en otra base);
-- la integridad la garantiza la lógica de aplicación.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP_Admin -i 15b_Admin_Memberships_Invitations.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-------------------------------------------------------------------------------
-- ADM_TenantMemberships
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_TenantMemberships')
BEGIN
    CREATE TABLE dbo.ADM_TenantMemberships
    (
        -- Identidad
        Id                      INT              NOT NULL IDENTITY(1,1) CONSTRAINT PK_ADM_TenantMemberships PRIMARY KEY,
        PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ADM_TenantMemberships_PublicId DEFAULT (NEWID()),

        -- Relación
        CentralUserId           UNIQUEIDENTIFIER NOT NULL,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Estado (data-model §2 + research D-09)
        Status                  INT              NOT NULL CONSTRAINT DF_ADM_TenantMemberships_Status DEFAULT (0),  -- 0=Invited, 1=Active, 2=Suspended, 3=Revoked
        IsTenantAdmin           BIT              NOT NULL CONSTRAINT DF_ADM_TenantMemberships_IsAdmin DEFAULT (0),

        -- Trazabilidad de transiciones
        InvitedByUserId         UNIQUEIDENTIFIER NULL,
        InvitedAt               DATETIME2        NOT NULL CONSTRAINT DF_ADM_TenantMemberships_InvitedAt DEFAULT (SYSUTCDATETIME()),
        ActivatedAt             DATETIME2        NULL,
        SuspendedAt             DATETIME2        NULL,
        SuspendedByUserId       UNIQUEIDENTIFIER NULL,
        RevokedAt               DATETIME2        NULL,
        RevokedByUserId         UNIQUEIDENTIFIER NULL,

        -- AuditableEntity (heredado vía Domain)
        CreatedAt               DATETIME2        NOT NULL CONSTRAINT DF_ADM_TenantMemberships_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy               NVARCHAR(256)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(256)    NULL,
        IsDeleted               BIT              NOT NULL CONSTRAINT DF_ADM_TenantMemberships_IsDeleted DEFAULT (0),
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(256)    NULL,
        RowVersion              ROWVERSION       NOT NULL,

        CONSTRAINT FK_ADM_TenantMemberships_CentralUser
            FOREIGN KEY (CentralUserId) REFERENCES dbo.ADM_CentralUsers (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_TenantMemberships_PublicId' AND object_id = OBJECT_ID('dbo.ADM_TenantMemberships'))
    CREATE UNIQUE INDEX UX_ADM_TenantMemberships_PublicId ON dbo.ADM_TenantMemberships (PublicId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_TenantMemberships_CentralUser_Tenant' AND object_id = OBJECT_ID('dbo.ADM_TenantMemberships'))
    CREATE UNIQUE INDEX UX_ADM_TenantMemberships_CentralUser_Tenant ON dbo.ADM_TenantMemberships (CentralUserId, TenantId)
        WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_TenantMemberships_Tenant_Status' AND object_id = OBJECT_ID('dbo.ADM_TenantMemberships'))
    CREATE INDEX IX_ADM_TenantMemberships_Tenant_Status ON dbo.ADM_TenantMemberships (TenantId, Status)
        INCLUDE (CentralUserId, IsTenantAdmin)
        WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_TenantMemberships_CentralUser_Status' AND object_id = OBJECT_ID('dbo.ADM_TenantMemberships'))
    CREATE INDEX IX_ADM_TenantMemberships_CentralUser_Status ON dbo.ADM_TenantMemberships (CentralUserId, Status)
        INCLUDE (TenantId, IsTenantAdmin)
        WHERE IsDeleted = 0;
GO

-- Salvaguarda "último admin" (FR-040, research D-09): index filtrado para conteos baratos.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_TenantMemberships_Tenant_ActiveAdmins' AND object_id = OBJECT_ID('dbo.ADM_TenantMemberships'))
    CREATE INDEX IX_ADM_TenantMemberships_Tenant_ActiveAdmins ON dbo.ADM_TenantMemberships (TenantId)
        WHERE IsTenantAdmin = 1 AND Status = 1 AND IsDeleted = 0;
GO

-------------------------------------------------------------------------------
-- ADM_Invitations
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_Invitations')
BEGIN
    CREATE TABLE dbo.ADM_Invitations
    (
        Id                          INT              NOT NULL IDENTITY(1,1) CONSTRAINT PK_ADM_Invitations PRIMARY KEY,
        PublicId                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ADM_Invitations_PublicId DEFAULT (NEWID()),

        -- Datos
        Email                       NVARCHAR(256)    NOT NULL,
        NormalizedEmail             NVARCHAR(256)    NOT NULL,
        TenantId                    UNIQUEIDENTIFIER NOT NULL,
        InvitedByUserId             UNIQUEIDENTIFIER NOT NULL,
        InviteAsTenantAdmin         BIT              NOT NULL CONSTRAINT DF_ADM_Invitations_AsAdmin DEFAULT (0),

        -- Token (research D-05) — SHA-256 hash (32 bytes); el plano nunca se persiste.
        TokenHash                   BINARY(32)       NOT NULL,

        -- Estado (Pending=0, Accepted=1, Expired=2, Revoked=3)
        Status                      INT              NOT NULL CONSTRAINT DF_ADM_Invitations_Status DEFAULT (0),
        ExpiresAt                   DATETIME2        NOT NULL,
        AcceptedAt                  DATETIME2        NULL,
        AcceptedByCentralUserId     UNIQUEIDENTIFIER NULL,
        RevokedAt                   DATETIME2        NULL,
        RevokedByUserId             UNIQUEIDENTIFIER NULL,

        -- AuditableEntity
        CreatedAt                   DATETIME2        NOT NULL CONSTRAINT DF_ADM_Invitations_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy                   NVARCHAR(256)    NULL,
        UpdatedAt                   DATETIME2        NULL,
        UpdatedBy                   NVARCHAR(256)    NULL,
        IsDeleted                   BIT              NOT NULL CONSTRAINT DF_ADM_Invitations_IsDeleted DEFAULT (0),
        DeletedAt                   DATETIME2        NULL,
        DeletedBy                   NVARCHAR(256)    NULL,
        RowVersion                  ROWVERSION       NOT NULL,

        CONSTRAINT FK_ADM_Invitations_InvitedBy
            FOREIGN KEY (InvitedByUserId) REFERENCES dbo.ADM_CentralUsers (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_Invitations_PublicId' AND object_id = OBJECT_ID('dbo.ADM_Invitations'))
    CREATE UNIQUE INDEX UX_ADM_Invitations_PublicId ON dbo.ADM_Invitations (PublicId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_Invitations_TokenHash' AND object_id = OBJECT_ID('dbo.ADM_Invitations'))
    CREATE UNIQUE INDEX UX_ADM_Invitations_TokenHash ON dbo.ADM_Invitations (TokenHash);
GO

-- Edge case: invitación duplicada al mismo email/tenant cuando hay una Pending.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_Invitations_Email_Tenant_Status' AND object_id = OBJECT_ID('dbo.ADM_Invitations'))
    CREATE INDEX IX_ADM_Invitations_Email_Tenant_Status ON dbo.ADM_Invitations (NormalizedEmail, TenantId, Status)
        WHERE IsDeleted = 0;
GO

-- InvitationExpiryJob (T122) — busca Pending con ExpiresAt < now.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_Invitations_Status_ExpiresAt' AND object_id = OBJECT_ID('dbo.ADM_Invitations'))
    CREATE INDEX IX_ADM_Invitations_Status_ExpiresAt ON dbo.ADM_Invitations (Status, ExpiresAt)
        WHERE Status = 0 AND IsDeleted = 0;
GO

-- Listados del admin de empresa.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_Invitations_Tenant_Status' AND object_id = OBJECT_ID('dbo.ADM_Invitations'))
    CREATE INDEX IX_ADM_Invitations_Tenant_Status ON dbo.ADM_Invitations (TenantId, Status)
        INCLUDE (Email, ExpiresAt, AcceptedAt)
        WHERE IsDeleted = 0;
GO

PRINT '15b_Admin_Memberships_Invitations.sql applied: ADM_TenantMemberships + ADM_Invitations.';
GO
