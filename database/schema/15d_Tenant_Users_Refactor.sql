-- =============================================================================
-- File:        15d_Tenant_Users_Refactor.sql
-- Phase:       Fase 1 — Identidad central · feature 002-identidad-central-federada
-- Idempotent:  YES (todos los DROP/ADD están guardados con IF COL_LENGTH).
-- Reversible:  NO. Una vez ejecutado contra una BD con datos, no se puede
--              recuperar la columna PasswordHash ni los hashes que contenía.
--
-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  ⚠️  WARNING — REQUIERE BD VIRGEN. IRREVERSIBLE SIN RESTORE.             ║
-- ║                                                                          ║
-- ║  Este script ELIMINA columnas que contienen credenciales y bloqueos:     ║
-- ║      PasswordHash, PasswordSalt, MfaSecret,                              ║
-- ║      IsEmailVerified, FailedLoginAttempts, LockoutEndAt,                 ║
-- ║      MustChangePassword, LastPasswordChangeAt,                           ║
-- ║      LegacyLogin, IsSaasOperator                                         ║
-- ║                                                                          ║
-- ║  Estas columnas migran a ADM_CentralUsers (BD IngenIA365ERP_Admin) bajo  ║
-- ║  ASP.NET Core Identity con TKey=Guid. Tras ejecutar este script,         ║
-- ║  SEC_Users queda como tabla de PERFIL OPERACIONAL del usuario en el      ║
-- ║  tenant (alias, persona, límites de aprobación) — la credencial real     ║
-- ║  se valida contra ADM_CentralUsers usando CentralUserId como FK lógica.  ║
-- ║                                                                          ║
-- ║  NO ejecutar contra BD productiva sin antes:                             ║
-- ║   1) Haber implementado US1 (invitaciones) y US2 (login centralizado)    ║
-- ║      — los handlers legacy de Fase 0 que usan PasswordHash dejarán de    ║
-- ║      funcionar al desaparecer la columna.                                ║
-- ║   2) Haber migrado los usuarios existentes a ADM_CentralUsers vía        ║
-- ║      script de migración separado (fuera del alcance de Feature 002).    ║
-- ║   3) Tener backup full de la BD.                                         ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
--
-- DATABASE TARGET: la BD operacional del tenant (cadena ConnectionStrings:
--                  SqlServer o DefaultConnection — NO la BD Admin).
--
-- Cambios:
--   ALTER TABLE dbo.SEC_Users
--     DROP COLUMN PasswordHash, PasswordSalt, MfaSecret,
--                 IsEmailVerified, FailedLoginAttempts, LockoutEndAt,
--                 MustChangePassword, LastPasswordChangeAt,
--                 LegacyLogin, IsSaasOperator
--     ADD CentralUserId          UNIQUEIDENTIFIER NOT NULL UNIQUE
--     ADD CentralUserPublicEmail NVARCHAR(256)    NULL
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 15d_Tenant_Users_Refactor.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-------------------------------------------------------------------------------
-- 1) Drop default constraints sobre las columnas a eliminar.
--    SQL Server no permite DROP COLUMN sin haber soltado primero los defaults
--    nombrados; usamos sys.default_constraints para resolverlos en runtime.
-------------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Users')
BEGIN
    DECLARE @colsToDrop TABLE (ColumnName SYSNAME);
    INSERT INTO @colsToDrop (ColumnName) VALUES
        ('PasswordHash'),
        ('PasswordSalt'),
        ('MfaSecret'),
        ('IsEmailVerified'),
        ('FailedLoginAttempts'),
        ('LockoutEndAt'),
        ('MustChangePassword'),
        ('LastPasswordChangeAt'),
        ('LegacyLogin'),
        ('IsSaasOperator');

    DECLARE @dropDefaultsSql NVARCHAR(MAX) = N'';
    SELECT @dropDefaultsSql = @dropDefaultsSql
         + N'ALTER TABLE dbo.SEC_Users DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(10)
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.SEC_Users')
      AND c.name IN (SELECT ColumnName FROM @colsToDrop);

    IF LEN(@dropDefaultsSql) > 0
    BEGIN
        PRINT '15d: removing default constraints on legacy credential columns';
        EXEC sp_executesql @dropDefaultsSql;
    END
END
GO

-------------------------------------------------------------------------------
-- 2) Drop legacy credential / lockout / status columns.
--    Idempotente: cada DROP comprueba COL_LENGTH antes de actuar.
-------------------------------------------------------------------------------
IF COL_LENGTH('dbo.SEC_Users', 'PasswordHash') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN PasswordHash;
GO

IF COL_LENGTH('dbo.SEC_Users', 'PasswordSalt') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN PasswordSalt;
GO

IF COL_LENGTH('dbo.SEC_Users', 'MfaSecret') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN MfaSecret;
GO

IF COL_LENGTH('dbo.SEC_Users', 'IsEmailVerified') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN IsEmailVerified;
GO

IF COL_LENGTH('dbo.SEC_Users', 'FailedLoginAttempts') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN FailedLoginAttempts;
GO

IF COL_LENGTH('dbo.SEC_Users', 'LockoutEndAt') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN LockoutEndAt;
GO

IF COL_LENGTH('dbo.SEC_Users', 'MustChangePassword') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN MustChangePassword;
GO

IF COL_LENGTH('dbo.SEC_Users', 'LastPasswordChangeAt') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN LastPasswordChangeAt;
GO

IF COL_LENGTH('dbo.SEC_Users', 'LegacyLogin') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN LegacyLogin;
GO

IF COL_LENGTH('dbo.SEC_Users', 'IsSaasOperator') IS NOT NULL
    ALTER TABLE dbo.SEC_Users DROP COLUMN IsSaasOperator;
GO

-------------------------------------------------------------------------------
-- 3) Add CentralUserId (FK lógica a ADM_CentralUsers.Id en BD Admin).
--    NOT NULL + UNIQUE: cada SEC_Users es 1:1 con una identidad central.
--    No declaramos FK SQL formal porque ADM_CentralUsers vive en OTRA BD
--    (IngenIA365ERP_Admin) — las FKs cross-database no las soporta SQL Server.
--    La integridad se valida en el TenantUserProvisioner (T056, US1).
-------------------------------------------------------------------------------
IF COL_LENGTH('dbo.SEC_Users', 'CentralUserId') IS NULL
BEGIN
    ALTER TABLE dbo.SEC_Users
        ADD CentralUserId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_SEC_Users_CentralUserId DEFAULT (NEWID());
    -- El DEFAULT existe solo para satisfacer NOT NULL en filas pre-existentes
    -- (que no deberían existir tras ejecutar este script en BD virgen).
    -- Producción real lo poblará el TenantUserProvisioner antes de INSERT.
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SEC_Users_CentralUserId' AND object_id = OBJECT_ID('dbo.SEC_Users'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_SEC_Users_CentralUserId
        ON dbo.SEC_Users (CentralUserId);
END
GO

-------------------------------------------------------------------------------
-- 4) Add CentralUserPublicEmail (email "actual" del usuario en la identidad
--    central, replicado por conveniencia para listados y reportes del tenant
--    sin tener que cruzar BDs en cada query). Se mantiene sincronizado por
--    eventos de dominio cuando el usuario cambia su email central.
-------------------------------------------------------------------------------
IF COL_LENGTH('dbo.SEC_Users', 'CentralUserPublicEmail') IS NULL
BEGIN
    ALTER TABLE dbo.SEC_Users
        ADD CentralUserPublicEmail NVARCHAR(256) NULL;
END
GO

PRINT '15d: SEC_Users refactor completed — central identity wired.';
GO
