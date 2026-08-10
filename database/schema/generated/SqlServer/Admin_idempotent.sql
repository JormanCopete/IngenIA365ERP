-- ============================================================
-- IngenIA365ERP — script idempotente para DBA (feature 004)
-- Contexto : AdminDbContext · Proveedor: SqlServer
-- Generado : 2026-08-04 12:58:15 UTC
-- Desde    : (inicio) → (última migración)
-- Ejecución: re-ejecutable sin efectos (IF NOT EXISTS / historial
--            __EFMigrationsHistory). Reversión: Down() de cada
--            migración (DbMigrator/dotnet-ef) o restore de backup.
-- REQUIERE BACKUP PREVIO en producción (principio XII).
-- ============================================================
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUserLoginAttempts] (
        [Id] bigint NOT NULL IDENTITY,
        [CentralUserId] uniqueidentifier NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [Result] int NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(512) NULL,
        [Timestamp] datetime2 NOT NULL,
        [LockoutAppliedSeconds] int NULL,
        CONSTRAINT [PK_ADM_CentralUserLoginAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUsers] (
        [Id] uniqueidentifier NOT NULL,
        [MfaSecret] nvarchar(512) NULL,
        [DefaultTenantId] uniqueidentifier NULL,
        [IsGlobalMasterAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] int NOT NULL DEFAULT 0,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(256) NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_ADM_CentralUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Invitations] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [InvitedByUserId] uniqueidentifier NOT NULL,
        [InviteAsTenantAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TokenHash] binary(32) NOT NULL,
        [Status] int NOT NULL DEFAULT 0,
        [ExpiresAt] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [AcceptedByCentralUserId] uniqueidentifier NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedByUserId] uniqueidentifier NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Invitations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_PasswordResetTokens] (
        [Id] int NOT NULL IDENTITY,
        [CentralUserId] uniqueidentifier NOT NULL,
        [TokenHash] binary(32) NOT NULL,
        [RequesterIp] nvarchar(45) NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(256) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_ADM_PasswordResetTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_TenantMemberships] (
        [Id] int NOT NULL IDENTITY,
        [CentralUserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL DEFAULT 0,
        [IsTenantAdmin] bit NOT NULL DEFAULT CAST(0 AS bit),
        [InvitedByUserId] uniqueidentifier NULL,
        [InvitedAt] datetime2 NOT NULL,
        [ActivatedAt] datetime2 NULL,
        [SuspendedAt] datetime2 NULL,
        [SuspendedByUserId] uniqueidentifier NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedByUserId] uniqueidentifier NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_TenantMemberships] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_TenantMfaPolicies] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [IsRequired] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ActivatedAt] datetime2 NULL,
        [ActivatedByUserId] uniqueidentifier NULL,
        [DeactivatedAt] datetime2 NULL,
        [DeactivatedByUserId] uniqueidentifier NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_TenantMfaPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Tenants] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [SchemaName] nvarchar(50) NOT NULL,
        [Subdomain] nvarchar(100) NULL,
        [PlanType] nvarchar(50) NOT NULL DEFAULT N'Basic',
        [Nit] nvarchar(20) NULL,
        [LegalName] nvarchar(200) NULL,
        [LegalAddress] nvarchar(300) NULL,
        [TaxRegime] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [MaxUsers] int NOT NULL,
        [StorageLimitMb] bigint NOT NULL,
        [DatabaseName] nvarchar(100) NULL,
        [ContactEmail] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(30) NULL,
        [ActivatedAt] datetime2 NULL,
        [SuspendedAt] datetime2 NULL,
        [ConnectionString] nvarchar(500) NULL,
        [ExpirationDate] datetime2 NULL,
        [Identifier] nvarchar(100) NULL,
        [LicenseType] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_CentralUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_CentralUserClaims_ADM_CentralUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[ADM_CentralUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ADM_CentralUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_ADM_CentralUserLogins_ADM_CentralUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[ADM_CentralUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_CentralUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_ADM_CentralUserTokens_ADM_CentralUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[ADM_CentralUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_CentralUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ADM_CentralUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_ADM_CentralUserRoles_ADM_CentralUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[ADM_CentralUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ADM_CentralUserRoles_ADM_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[ADM_Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_RoleClaims_ADM_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[ADM_Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Branches] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(300) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(200) NULL,
        [IsHeadquarters] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_Branches_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_Subscriptions] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [PlanName] nvarchar(100) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [Currency] nvarchar(5) NOT NULL DEFAULT N'COP',
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [LastPaymentDate] date NULL,
        [NextBillingDate] date NULL,
        [PaymentMethod] nvarchar(50) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_Subscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_Subscriptions_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ADM_TenantSettings] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [SettingKey] nvarchar(200) NOT NULL,
        [SettingValue] nvarchar(max) NULL,
        [ValueType] nvarchar(30) NOT NULL DEFAULT N'String',
        [Description] nvarchar(500) NULL,
        [ModulePrefix] nvarchar(5) NULL,
        [PublicId] uniqueidentifier NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ADM_TenantSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ADM_TenantSettings_ADM_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Branches_PublicId] ON [dbo].[ADM_Branches] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Branches_TenantId_Code] ON [dbo].[ADM_Branches] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ADM_Branches_TenantId_IsHeadquarters] ON [dbo].[ADM_Branches] ([TenantId], [IsHeadquarters]) WHERE [IsHeadquarters] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_CentralUserClaims_UserId] ON [dbo].[ADM_CentralUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_LoginAttempts_Email_Timestamp] ON [dbo].[ADM_CentralUserLoginAttempts] ([NormalizedEmail], [Timestamp] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_LoginAttempts_Ip_Timestamp] ON [dbo].[ADM_CentralUserLoginAttempts] ([IpAddress], [Timestamp] DESC) WHERE [IpAddress] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_CentralUserLogins_UserId] ON [dbo].[ADM_CentralUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_CentralUserRoles_RoleId] ON [dbo].[ADM_CentralUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [dbo].[ADM_CentralUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[ADM_CentralUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_Invitations_Email_Tenant_Status] ON [dbo].[ADM_Invitations] ([NormalizedEmail], [TenantId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Invitations_PublicId] ON [dbo].[ADM_Invitations] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_Invitations_Status_ExpiresAt] ON [dbo].[ADM_Invitations] ([Status], [ExpiresAt]) WHERE [Status] = 0 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_Invitations_Tenant_Status] ON [dbo].[ADM_Invitations] ([TenantId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ADM_Invitations_TokenHash] ON [dbo].[ADM_Invitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_PasswordResetTokens_CentralUserId_ExpiresAt] ON [dbo].[ADM_PasswordResetTokens] ([CentralUserId], [ExpiresAt]) WHERE [ConsumedAt] IS NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_PasswordResetTokens_PublicId] ON [dbo].[ADM_PasswordResetTokens] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_PasswordResetTokens_TokenHash] ON [dbo].[ADM_PasswordResetTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_RoleClaims_RoleId] ON [dbo].[ADM_RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[ADM_Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Subscriptions_PublicId] ON [dbo].[ADM_Subscriptions] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ADM_Subscriptions_TenantId] ON [dbo].[ADM_Subscriptions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_TenantMemberships_CentralUser_Status] ON [dbo].[ADM_TenantMemberships] ([CentralUserId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantMemberships_PublicId] ON [dbo].[ADM_TenantMemberships] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_TenantMemberships_Tenant_ActiveAdmins] ON [dbo].[ADM_TenantMemberships] ([TenantId]) WHERE [IsTenantAdmin] = 1 AND [Status] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ADM_TenantMemberships_Tenant_Status] ON [dbo].[ADM_TenantMemberships] ([TenantId], [Status]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ADM_TenantMemberships_CentralUser_Tenant] ON [dbo].[ADM_TenantMemberships] ([CentralUserId], [TenantId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantMfaPolicies_PublicId] ON [dbo].[ADM_TenantMfaPolicies] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ADM_TenantMfaPolicies_TenantId] ON [dbo].[ADM_TenantMfaPolicies] ([TenantId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ADM_Tenants_Nit] ON [dbo].[ADM_Tenants] ([Nit]) WHERE [Nit] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Tenants_PublicId] ON [dbo].[ADM_Tenants] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_Tenants_SchemaName] ON [dbo].[ADM_Tenants] ([SchemaName]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ADM_Tenants_Subdomain] ON [dbo].[ADM_Tenants] ([Subdomain]) WHERE [Subdomain] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantSettings_PublicId] ON [dbo].[ADM_TenantSettings] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ADM_TenantSettings_TenantId_SettingKey] ON [dbo].[ADM_TenantSettings] ([TenantId], [SettingKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804124946_InitialSchema'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804124946_InitialSchema', N'10.0.10');
END;

COMMIT;
GO

