-- =============================================================================
-- File:        17_User_Extra_Flags.sql
-- Phase:       Fase 0 — Cimientos técnicos · US1 (T047)
-- Idempotent:  YES (sys.columns guards).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--   Las tablas SEC_Users y SEC_RefreshTokens viven en la BD operacional.
--
-- Añade a SEC_Users los flags:
--   - IsSaasOperator   : el usuario opera el SaaS, no un tenant.
--   - MustChangePassword : true cuando la contraseña fue establecida por reset
--                          administrativo y debe cambiarse al primer login.
--
-- Además añade a SEC_RefreshTokens:
--   - TokenHash         : SHA-256 hex del token opaco (lookup).
--   - FamilyId          : familia de rotación (FR-012).
--   - RevocationReason  : Rotated / LogoutUser / LogoutAll / ReuseDetected / AdminRevoke.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 17_User_Extra_Flags.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Users')
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE Name = 'IsSaasOperator' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    BEGIN
        ALTER TABLE dbo.SEC_Users
            ADD IsSaasOperator BIT NOT NULL
                CONSTRAINT DF_SEC_Users_IsSaasOperator DEFAULT 0;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE Name = 'MustChangePassword' AND Object_ID = OBJECT_ID('dbo.SEC_Users'))
    BEGIN
        ALTER TABLE dbo.SEC_Users
            ADD MustChangePassword BIT NOT NULL
                CONSTRAINT DF_SEC_Users_MustChangePassword DEFAULT 0;
    END;
END;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_RefreshTokens')
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE Name = 'TokenHash' AND Object_ID = OBJECT_ID('dbo.SEC_RefreshTokens'))
    BEGIN
        ALTER TABLE dbo.SEC_RefreshTokens
            ADD TokenHash NVARCHAR(120) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE Name = 'FamilyId' AND Object_ID = OBJECT_ID('dbo.SEC_RefreshTokens'))
    BEGIN
        ALTER TABLE dbo.SEC_RefreshTokens
            ADD FamilyId UNIQUEIDENTIFIER NOT NULL
                CONSTRAINT DF_SEC_RefreshTokens_FamilyId DEFAULT NEWID();
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE Name = 'RevocationReason' AND Object_ID = OBJECT_ID('dbo.SEC_RefreshTokens'))
    BEGIN
        ALTER TABLE dbo.SEC_RefreshTokens
            ADD RevocationReason NVARCHAR(40) NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_RefreshTokens_TokenHash')
    BEGIN
        CREATE INDEX IX_SEC_RefreshTokens_TokenHash
            ON dbo.SEC_RefreshTokens(TokenHash);
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_RefreshTokens_FamilyId')
    BEGIN
        CREATE INDEX IX_SEC_RefreshTokens_FamilyId
            ON dbo.SEC_RefreshTokens(FamilyId);
    END;
END;
GO
