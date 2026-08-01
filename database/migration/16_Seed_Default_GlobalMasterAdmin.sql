-- =============================================================================
-- File:        16_Seed_Default_GlobalMasterAdmin.sql
-- Phase:       Fase 1 — Identidad central · feature 002-identidad-central-federada
-- Idempotent:  YES — solo inserta si ADM_CentralUsers no tiene admin master.
-- Reversible:  No automático (los seeds son one-way). Para revertir: DELETE manual.
-- Prerequisite: 15a + 15b aplicados.
--
-- DATABASE TARGET: IngenIA365ERP_Admin
--
-- Crea el primer CentralUser con IsGlobalMasterAdmin=1 para hacer bootstrap
-- del producto. La idempotencia garantiza que correr esto sobre una BD que ya
-- tenga master admin no duplica nada.
--
-- VARIABLES sqlcmd requeridas:
--   :setvar MasterAdminEmail        "master@tu-dominio.com"
--   :setvar MasterAdminPasswordHash "$2a$11$..."   -- BCrypt cost 11, pre-calculado
--
-- Cómo generar el BCrypt hash (cost 11) en .NET:
--   dotnet script -e "Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(\"TuPwd!\", 11))"
-- O en Node.js:
--   node -e "console.log(require('bcrypt').hashSync('TuPwd!', 11))"
-- O usar https://bcrypt-generator.com (cost 11) — sólo para entornos NO productivos.
--
-- Cómo ejecutar (PowerShell):
--   $env:MASTER_ADMIN_EMAIL='master@coop.com'
--   $env:MASTER_ADMIN_PASSWORD_HASH='$2a$11$...'
--   sqlcmd -S localhost -d IngenIA365ERP_Admin `
--     -i 16_Seed_Default_GlobalMasterAdmin.sql `
--     -v MasterAdminEmail="$env:MASTER_ADMIN_EMAIL" `
--     -v MasterAdminPasswordHash="$env:MASTER_ADMIN_PASSWORD_HASH"
--
-- ATENCIÓN: el master admin inicial puede acceder a TODOS los tenants y
-- gestionar suscripciones. Cambiar la contraseña inmediatamente tras el
-- primer login (la app ofrece el flujo Profile/ChangePassword).
-- =============================================================================

SET NOCOUNT ON;
GO

-- Validación de variables: si vienen vacías, abortar con mensaje claro.
IF '$(MasterAdminEmail)' = '' OR '$(MasterAdminEmail)' = '$' + '(MasterAdminEmail)'
BEGIN
    RAISERROR('Variable sqlcmd MasterAdminEmail no fue provista. Use -v MasterAdminEmail=...', 16, 1);
    RETURN;
END
GO

IF '$(MasterAdminPasswordHash)' = '' OR '$(MasterAdminPasswordHash)' = '$' + '(MasterAdminPasswordHash)'
BEGIN
    RAISERROR('Variable sqlcmd MasterAdminPasswordHash no fue provista. Use -v MasterAdminPasswordHash=...', 16, 1);
    RETURN;
END
GO

DECLARE @Email           NVARCHAR(256) = N'$(MasterAdminEmail)';
DECLARE @NormalizedEmail NVARCHAR(256) = UPPER(@Email);
DECLARE @PasswordHash    NVARCHAR(256) = N'$(MasterAdminPasswordHash)';
DECLARE @Now             DATETIME2     = SYSUTCDATETIME();

-- Si YA existe algún master admin (cualquiera), no hacer nada — idempotente.
IF EXISTS (
    SELECT 1 FROM dbo.ADM_CentralUsers
    WHERE IsGlobalMasterAdmin = 1 AND IsDeleted = 0
)
BEGIN
    PRINT '16_Seed: ya existe al menos un master admin. Skipping seed.';
    RETURN;
END

-- Si YA existe un user con este email (no master), abortar — no convertirlo silenciosamente.
IF EXISTS (
    SELECT 1 FROM dbo.ADM_CentralUsers
    WHERE NormalizedEmail = @NormalizedEmail AND IsDeleted = 0
)
BEGIN
    PRINT '16_Seed: ya existe un CentralUser con ese email (no es master). Skipping para evitar promoción silenciosa.';
    RETURN;
END

-- Insertar el master admin.
DECLARE @Id UNIQUEIDENTIFIER = NEWID();

INSERT INTO dbo.ADM_CentralUsers
(
    Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
    PasswordHash, SecurityStamp, ConcurrencyStamp,
    PhoneNumberConfirmed, TwoFactorEnabled,
    LockoutEnabled, AccessFailedCount,
    DefaultTenantId, IsGlobalMasterAdmin, Status,
    CreatedAt, CreatedBy, IsDeleted
)
VALUES
(
    @Id, @Email, @NormalizedEmail, @Email, @NormalizedEmail, 1,
    @PasswordHash,
    CONVERT(NVARCHAR(64), NEWID()),
    CONVERT(NVARCHAR(64), NEWID()),
    0, 0,
    0, 0,
    NULL,            -- master no necesita DefaultTenantId
    1,               -- IsGlobalMasterAdmin
    0,               -- Status = Active
    @Now, N'BOOTSTRAP', 0
);

PRINT CONCAT(
    '16_Seed: master admin creado con email=', @Email,
    ' (Id=', CONVERT(NVARCHAR(36), @Id), '). ',
    'IMPORTANTE: cambia la contraseña tras el primer login.'
);
GO
