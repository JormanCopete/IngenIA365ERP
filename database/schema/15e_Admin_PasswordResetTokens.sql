-- =============================================================================
-- File:        15e_Admin_PasswordResetTokens.sql
-- Phase:       Fase 1 — Identidad central · Phase 4b (Profile & Recovery)
-- Idempotent:  YES (CREATE TABLE / CREATE INDEX guarded con IF NOT EXISTS).
-- Reversible:  Sí — DROP TABLE seguro.
--
-- DATABASE TARGET: IngenIA365ERP_Admin
--
-- Tabla:
--   - ADM_PasswordResetTokens (T079g) — tokens de un solo uso del flujo
--     "olvidé mi contraseña". Persiste el HASH SHA-256 del plano (32 bytes);
--     el plano nunca toca disco. TTL 1 hora; consumibles una sola vez.
--
-- Concurrencia: RowVersion + lock distribuido en el handler garantizan
-- single-use estricto contra clicks simultáneos del enlace.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP_Admin -i 15e_Admin_PasswordResetTokens.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_PasswordResetTokens')
BEGIN
    CREATE TABLE dbo.ADM_PasswordResetTokens
    (
        Id              BIGINT           NOT NULL IDENTITY(1,1)
                                         CONSTRAINT PK_ADM_PasswordResetTokens PRIMARY KEY,
        PublicId        UNIQUEIDENTIFIER NOT NULL
                                         CONSTRAINT DF_ADM_PasswordResetTokens_PublicId DEFAULT (NEWID()),

        CentralUserId   UNIQUEIDENTIFIER NOT NULL,
        TokenHash       BINARY(32)       NOT NULL,
        RequesterIp     NVARCHAR(45)     NULL,

        ExpiresAt       DATETIME2        NOT NULL,
        ConsumedAt      DATETIME2        NULL,

        -- Auditoría heredada de AuditableEntity (CreatedAt/By, UpdatedAt/By).
        CreatedAt       DATETIME2        NOT NULL
                                         CONSTRAINT DF_ADM_PasswordResetTokens_CreatedAt
                                         DEFAULT (SYSUTCDATETIME()),
        CreatedBy       NVARCHAR(256)    NULL,
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       NVARCHAR(256)    NULL,

        -- Soft-delete (de BaseEntity).
        IsDeleted       BIT              NOT NULL
                                         CONSTRAINT DF_ADM_PasswordResetTokens_IsDeleted DEFAULT (0),
        DeletedAt       DATETIME2        NULL,
        DeletedBy       NVARCHAR(256)    NULL,

        -- Concurrencia optimista.
        RowVersion      ROWVERSION       NOT NULL,

        CONSTRAINT UX_ADM_PasswordResetTokens_PublicId UNIQUE (PublicId),
        CONSTRAINT UX_ADM_PasswordResetTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_ADM_PasswordResetTokens_CentralUser
            FOREIGN KEY (CentralUserId)
            REFERENCES dbo.ADM_CentralUsers (Id)
            ON DELETE CASCADE
    );
END
GO

-- Índice para resolver "token activo más reciente del usuario" en O(log N).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ADM_PasswordResetTokens_CentralUserId_ExpiresAt'
      AND object_id = OBJECT_ID('dbo.ADM_PasswordResetTokens'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ADM_PasswordResetTokens_CentralUserId_ExpiresAt
        ON dbo.ADM_PasswordResetTokens (CentralUserId, ExpiresAt DESC)
        WHERE ConsumedAt IS NULL AND IsDeleted = 0;
END
GO

PRINT '15e: ADM_PasswordResetTokens listo.';
GO
