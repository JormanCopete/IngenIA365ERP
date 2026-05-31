-- =============================================================================
-- File:        15a_Admin_CentralIdentity.sql
-- Phase:       Fase 1 — Identidad central · feature 002-identidad-central-federada
-- Idempotent:  YES (CREATE TABLE / CREATE INDEX guarded with IF NOT EXISTS).
-- Reversible:  Sí — DROP TABLE seguro (en orden inverso por FKs).
--
-- DATABASE TARGET: IngenIA365ERP_Admin  (cadena ConnectionStrings:SqlServerAdmin
--                  o TenantConnection — la BD de admin, no la de cada tenant)
--
-- Crea la base de ASP.NET Core Identity bajo nombres con prefijo ADM_*, mapeada
-- por AdminDbContext (IdentityDbContext<CentralUserIdentity, IdentityRole<Guid>, Guid>).
-- Tablas estándar de Identity más los campos custom de CentralUser (data-model §1):
--   - ADM_CentralUsers       (IdentityUser<Guid> + DefaultTenantId, IsGlobalMasterAdmin,
--                            Status, MfaSecret, CreatedAt/By, UpdatedAt/By, soft-delete)
--   - ADM_Roles              (IdentityRole<Guid> — catálogo global de roles SaaS)
--   - ADM_CentralUserRoles   (junction users ↔ roles)
--   - ADM_CentralUserClaims  (claims por usuario)
--   - ADM_CentralUserLogins  (external logins, p.ej. Google/Entra futuros)
--   - ADM_CentralUserTokens  (recovery codes MFA + email confirm + password reset)
--   - ADM_RoleClaims         (claims por rol)
--
-- Convenciones:
--   - PK Guid en CentralUsers/Roles (ASP.NET Identity con TKey=Guid; excepción
--     justificada al principio constitucional VI, ver plan.md Complexity Tracking).
--   - MfaSecret se almacena cifrado (IDataProtectionProvider en runtime); el DDL
--     solo reserva el tamaño suficiente para el ciphertext.
--   - LockoutEnabled = 0 por default (research D-11): el lockout progresivo se
--     gestiona desde Redis (LoginAttemptCounter), no desde ASP.NET Identity.
--   - Soft-delete + audit fields manuales (CentralUser NO hereda AuditableEntity
--     porque ASP.NET Identity exige Id Guid; los campos están declarados).
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP_Admin -i 15a_Admin_CentralIdentity.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-------------------------------------------------------------------------------
-- ADM_CentralUsers  (= AspNetUsers renombrado)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_CentralUsers')
BEGIN
    CREATE TABLE dbo.ADM_CentralUsers
    (
        -- ASP.NET Identity core columns
        Id                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ADM_CentralUsers PRIMARY KEY,
        UserName                NVARCHAR(256)    NULL,
        NormalizedUserName      NVARCHAR(256)    NULL,
        Email                   NVARCHAR(256)    NOT NULL,
        NormalizedEmail         NVARCHAR(256)    NOT NULL,
        EmailConfirmed          BIT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_EmailConfirmed DEFAULT (0),
        PasswordHash            NVARCHAR(256)    NOT NULL,
        SecurityStamp           NVARCHAR(64)     NOT NULL,
        ConcurrencyStamp        NVARCHAR(64)     NOT NULL,
        PhoneNumber             NVARCHAR(40)     NULL,
        PhoneNumberConfirmed    BIT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_PhoneConfirmed DEFAULT (0),
        TwoFactorEnabled        BIT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_TwoFactor DEFAULT (0),
        LockoutEnd              DATETIMEOFFSET   NULL,
        LockoutEnabled          BIT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_LockoutEnabled DEFAULT (0),
        AccessFailedCount       INT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_AccessFailedCount DEFAULT (0),

        -- Custom CentralUser fields (data-model §1)
        MfaSecret               NVARCHAR(512)    NULL,  -- cifrado con IDataProtectionProvider en runtime
        DefaultTenantId         UNIQUEIDENTIFIER NULL,
        IsGlobalMasterAdmin     BIT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_IsMaster DEFAULT (0),
        Status                  INT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_Status DEFAULT (0),  -- 0=Active, 1=Pending, 2=Disabled
        LastLoginAt             DATETIME2        NULL,

        -- Audit + soft-delete (manual; no hereda AuditableEntity por restricción de Identity)
        CreatedAt               DATETIME2        NOT NULL CONSTRAINT DF_ADM_CentralUsers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy               NVARCHAR(256)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(256)    NULL,
        IsDeleted               BIT              NOT NULL CONSTRAINT DF_ADM_CentralUsers_IsDeleted DEFAULT (0),
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(256)    NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_CentralUsers_NormalizedEmail' AND object_id = OBJECT_ID('dbo.ADM_CentralUsers'))
    CREATE UNIQUE INDEX UX_ADM_CentralUsers_NormalizedEmail ON dbo.ADM_CentralUsers (NormalizedEmail);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_CentralUsers_DefaultTenantId' AND object_id = OBJECT_ID('dbo.ADM_CentralUsers'))
    CREATE INDEX IX_ADM_CentralUsers_DefaultTenantId ON dbo.ADM_CentralUsers (DefaultTenantId)
        WHERE DefaultTenantId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_CentralUsers_NormalizedUserName' AND object_id = OBJECT_ID('dbo.ADM_CentralUsers'))
    CREATE UNIQUE INDEX UX_ADM_CentralUsers_NormalizedUserName ON dbo.ADM_CentralUsers (NormalizedUserName)
        WHERE NormalizedUserName IS NOT NULL;
GO

-------------------------------------------------------------------------------
-- ADM_Roles  (= AspNetRoles)  — catálogo SaaS-global (NO permisos por tenant)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_Roles')
BEGIN
    CREATE TABLE dbo.ADM_Roles
    (
        Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ADM_Roles PRIMARY KEY,
        Name                NVARCHAR(256) NULL,
        NormalizedName      NVARCHAR(256) NULL,
        ConcurrencyStamp    NVARCHAR(64) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ADM_Roles_NormalizedName' AND object_id = OBJECT_ID('dbo.ADM_Roles'))
    CREATE UNIQUE INDEX UX_ADM_Roles_NormalizedName ON dbo.ADM_Roles (NormalizedName)
        WHERE NormalizedName IS NOT NULL;
GO

-------------------------------------------------------------------------------
-- ADM_CentralUserRoles  (= AspNetUserRoles)  — junction users ↔ roles
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_CentralUserRoles')
BEGIN
    CREATE TABLE dbo.ADM_CentralUserRoles
    (
        UserId              UNIQUEIDENTIFIER NOT NULL,
        RoleId              UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_ADM_CentralUserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_ADM_CentralUserRoles_User FOREIGN KEY (UserId) REFERENCES dbo.ADM_CentralUsers (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ADM_CentralUserRoles_Role FOREIGN KEY (RoleId) REFERENCES dbo.ADM_Roles (Id)         ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_CentralUserRoles_RoleId' AND object_id = OBJECT_ID('dbo.ADM_CentralUserRoles'))
    CREATE INDEX IX_ADM_CentralUserRoles_RoleId ON dbo.ADM_CentralUserRoles (RoleId);
GO

-------------------------------------------------------------------------------
-- ADM_CentralUserClaims  (= AspNetUserClaims)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_CentralUserClaims')
BEGIN
    CREATE TABLE dbo.ADM_CentralUserClaims
    (
        Id              INT              NOT NULL IDENTITY(1,1) CONSTRAINT PK_ADM_CentralUserClaims PRIMARY KEY,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        ClaimType       NVARCHAR(MAX)    NULL,
        ClaimValue      NVARCHAR(MAX)    NULL,
        CONSTRAINT FK_ADM_CentralUserClaims_User FOREIGN KEY (UserId) REFERENCES dbo.ADM_CentralUsers (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_CentralUserClaims_UserId' AND object_id = OBJECT_ID('dbo.ADM_CentralUserClaims'))
    CREATE INDEX IX_ADM_CentralUserClaims_UserId ON dbo.ADM_CentralUserClaims (UserId);
GO

-------------------------------------------------------------------------------
-- ADM_CentralUserLogins  (= AspNetUserLogins)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_CentralUserLogins')
BEGIN
    CREATE TABLE dbo.ADM_CentralUserLogins
    (
        LoginProvider       NVARCHAR(128)    NOT NULL,
        ProviderKey         NVARCHAR(128)    NOT NULL,
        ProviderDisplayName NVARCHAR(256)    NULL,
        UserId              UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_ADM_CentralUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
        CONSTRAINT FK_ADM_CentralUserLogins_User FOREIGN KEY (UserId) REFERENCES dbo.ADM_CentralUsers (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_CentralUserLogins_UserId' AND object_id = OBJECT_ID('dbo.ADM_CentralUserLogins'))
    CREATE INDEX IX_ADM_CentralUserLogins_UserId ON dbo.ADM_CentralUserLogins (UserId);
GO

-------------------------------------------------------------------------------
-- ADM_CentralUserTokens  (= AspNetUserTokens)
-- Hospeda recovery codes MFA, email confirm tokens, password reset tokens.
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_CentralUserTokens')
BEGIN
    CREATE TABLE dbo.ADM_CentralUserTokens
    (
        UserId          UNIQUEIDENTIFIER NOT NULL,
        LoginProvider   NVARCHAR(128)    NOT NULL,
        Name            NVARCHAR(128)    NOT NULL,
        Value           NVARCHAR(MAX)    NULL,
        CONSTRAINT PK_ADM_CentralUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
        CONSTRAINT FK_ADM_CentralUserTokens_User FOREIGN KEY (UserId) REFERENCES dbo.ADM_CentralUsers (Id) ON DELETE CASCADE
    );
END
GO

-------------------------------------------------------------------------------
-- ADM_RoleClaims  (= AspNetRoleClaims)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ADM_RoleClaims')
BEGIN
    CREATE TABLE dbo.ADM_RoleClaims
    (
        Id          INT              NOT NULL IDENTITY(1,1) CONSTRAINT PK_ADM_RoleClaims PRIMARY KEY,
        RoleId      UNIQUEIDENTIFIER NOT NULL,
        ClaimType   NVARCHAR(MAX)    NULL,
        ClaimValue  NVARCHAR(MAX)    NULL,
        CONSTRAINT FK_ADM_RoleClaims_Role FOREIGN KEY (RoleId) REFERENCES dbo.ADM_Roles (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_RoleClaims_RoleId' AND object_id = OBJECT_ID('dbo.ADM_RoleClaims'))
    CREATE INDEX IX_ADM_RoleClaims_RoleId ON dbo.ADM_RoleClaims (RoleId);
GO

PRINT '15a_Admin_CentralIdentity.sql applied: ADM_CentralUsers + 6 ASP.NET Identity standard tables.';
GO
