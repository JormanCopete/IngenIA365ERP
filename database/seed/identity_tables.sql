-- =============================================
-- IngenIA365ERP Identity Tables
-- Compatible with ASP.NET Core Identity (Int keys)
-- Apply against database: IngenIA365ERP
-- =============================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Drop FKs from auxiliary SEC_* tables first so we can drop the core ones
IF OBJECT_ID('dbo.FK_SEC_RefreshTokens_SEC_Users_UserId','F') IS NOT NULL
    ALTER TABLE dbo.SEC_RefreshTokens DROP CONSTRAINT FK_SEC_RefreshTokens_SEC_Users_UserId;
IF OBJECT_ID('dbo.FK_SEC_UserSessions_SEC_Users_UserId','F') IS NOT NULL
    ALTER TABLE dbo.SEC_UserSessions DROP CONSTRAINT FK_SEC_UserSessions_SEC_Users_UserId;
GO

-- Drop existing tables (in dependency order)
IF OBJECT_ID('dbo.SEC_UserTokens') IS NOT NULL DROP TABLE dbo.SEC_UserTokens;
IF OBJECT_ID('dbo.SEC_UserRoles') IS NOT NULL DROP TABLE dbo.SEC_UserRoles;
IF OBJECT_ID('dbo.SEC_UserLogins') IS NOT NULL DROP TABLE dbo.SEC_UserLogins;
IF OBJECT_ID('dbo.SEC_UserClaims') IS NOT NULL DROP TABLE dbo.SEC_UserClaims;
IF OBJECT_ID('dbo.SEC_RoleClaims') IS NOT NULL DROP TABLE dbo.SEC_RoleClaims;
IF OBJECT_ID('dbo.SEC_RolePermissions') IS NOT NULL DROP TABLE dbo.SEC_RolePermissions;
IF OBJECT_ID('dbo.SEC_Permissions') IS NOT NULL DROP TABLE dbo.SEC_Permissions;
IF OBJECT_ID('dbo.SEC_LoginAttempts') IS NOT NULL DROP TABLE dbo.SEC_LoginAttempts;
IF OBJECT_ID('dbo.SEC_Users') IS NOT NULL DROP TABLE dbo.SEC_Users;
IF OBJECT_ID('dbo.SEC_Roles') IS NOT NULL DROP TABLE dbo.SEC_Roles;
GO

CREATE TABLE [dbo].[SEC_Users] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [TenantId] NVARCHAR(50) NOT NULL DEFAULT '',
    [FullName] NVARCHAR(200) NOT NULL DEFAULT '',
    [IsActive] BIT NOT NULL DEFAULT 1,
    [LastLoginAt] DATETIME2 NULL,
    [FailedLoginAttempts] INT NOT NULL DEFAULT 0,
    [LockedUntil] DATETIME2 NULL,
    [MustChangePassword] BIT NOT NULL DEFAULT 0,
    [MfaEnabled] BIT NOT NULL DEFAULT 0,
    [RefreshToken] NVARCHAR(500) NULL,
    [RefreshTokenExpiry] DATETIME2 NULL,
    [LastLoginIp] NVARCHAR(50) NULL,
    [LastLoginUserAgent] NVARCHAR(500) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy] NVARCHAR(100) NOT NULL DEFAULT 'SYSTEM',
    [UpdatedAt] DATETIME2 NULL,
    [UpdatedBy] NVARCHAR(100) NULL,
    [UserName] NVARCHAR(256) NULL,
    [NormalizedUserName] NVARCHAR(256) NULL,
    [Email] NVARCHAR(256) NULL,
    [NormalizedEmail] NVARCHAR(256) NULL,
    [EmailConfirmed] BIT NOT NULL DEFAULT 0,
    [PasswordHash] NVARCHAR(MAX) NULL,
    [SecurityStamp] NVARCHAR(MAX) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL,
    [PhoneNumber] NVARCHAR(MAX) NULL,
    [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
    [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
    [LockoutEnd] DATETIMEOFFSET NULL,
    [LockoutEnabled] BIT NOT NULL DEFAULT 1,
    [AccessFailedCount] INT NOT NULL DEFAULT 0,
    CONSTRAINT [PK_SEC_Users] PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE UNIQUE INDEX [IX_SEC_Users_PublicId] ON [dbo].[SEC_Users]([PublicId]);
CREATE UNIQUE INDEX [IX_SEC_Users_NormalizedUserName] ON [dbo].[SEC_Users]([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
CREATE INDEX [IX_SEC_Users_NormalizedEmail] ON [dbo].[SEC_Users]([NormalizedEmail]);
CREATE INDEX [IX_SEC_Users_TenantId] ON [dbo].[SEC_Users]([TenantId]);
GO

CREATE TABLE [dbo].[SEC_Roles] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [TenantId] NVARCHAR(50) NOT NULL DEFAULT '',
    [Description] NVARCHAR(500) NULL,
    [IsSystemRole] BIT NOT NULL DEFAULT 0,
    [Name] NVARCHAR(256) NULL,
    [NormalizedName] NVARCHAR(256) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_SEC_Roles] PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE UNIQUE INDEX [IX_SEC_Roles_NormalizedName] ON [dbo].[SEC_Roles]([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE TABLE [dbo].[SEC_UserRoles] (
    [UserId] INT NOT NULL,
    [RoleId] INT NOT NULL,
    CONSTRAINT [PK_SEC_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_SEC_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SEC_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles]([Id]) ON DELETE CASCADE
);
GO
CREATE INDEX [IX_SEC_UserRoles_RoleId] ON [dbo].[SEC_UserRoles]([RoleId]);
GO

CREATE TABLE [dbo].[SEC_UserClaims] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [ClaimType] NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_SEC_UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SEC_UserClaims_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]) ON DELETE CASCADE
);
GO
CREATE INDEX [IX_SEC_UserClaims_UserId] ON [dbo].[SEC_UserClaims]([UserId]);
GO

CREATE TABLE [dbo].[SEC_UserLogins] (
    [LoginProvider] NVARCHAR(450) NOT NULL,
    [ProviderKey] NVARCHAR(450) NOT NULL,
    [ProviderDisplayName] NVARCHAR(MAX) NULL,
    [UserId] INT NOT NULL,
    CONSTRAINT [PK_SEC_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_SEC_UserLogins_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]) ON DELETE CASCADE
);
GO
CREATE INDEX [IX_SEC_UserLogins_UserId] ON [dbo].[SEC_UserLogins]([UserId]);
GO

CREATE TABLE [dbo].[SEC_UserTokens] (
    [UserId] INT NOT NULL,
    [LoginProvider] NVARCHAR(450) NOT NULL,
    [Name] NVARCHAR(450) NOT NULL,
    [Value] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_SEC_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_SEC_UserTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[SEC_RoleClaims] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [RoleId] INT NOT NULL,
    [ClaimType] NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_SEC_RoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SEC_RoleClaims_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles]([Id]) ON DELETE CASCADE
);
GO
CREATE INDEX [IX_SEC_RoleClaims_RoleId] ON [dbo].[SEC_RoleClaims]([RoleId]);
GO

CREATE TABLE [dbo].[SEC_Permissions] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [PublicId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Module] NVARCHAR(100) NOT NULL,
    [Feature] NVARCHAR(100) NOT NULL,
    [Action] NVARCHAR(50) NOT NULL,
    [PermissionCode] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    CONSTRAINT [PK_SEC_Permissions] PRIMARY KEY ([Id])
);
GO
CREATE UNIQUE INDEX [IX_SEC_Permissions_Code] ON [dbo].[SEC_Permissions]([PermissionCode]);
GO

CREATE TABLE [dbo].[SEC_RolePermissions] (
    [RoleId] INT NOT NULL,
    [PermissionId] INT NOT NULL,
    CONSTRAINT [PK_SEC_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_SEC_RolePermissions_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SEC_RolePermissions_Perms] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[SEC_Permissions]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[SEC_LoginAttempts] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] INT NULL,
    [Email] NVARCHAR(256) NOT NULL,
    [TenantId] NVARCHAR(50) NOT NULL DEFAULT '',
    [IpAddress] NVARCHAR(50) NULL,
    [UserAgent] NVARCHAR(500) NULL,
    [Success] BIT NOT NULL DEFAULT 0,
    [FailureReason] NVARCHAR(200) NULL,
    [AttemptedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_SEC_LoginAttempts] PRIMARY KEY ([Id])
);
GO
CREATE INDEX [IX_SEC_LoginAttempts_Email] ON [dbo].[SEC_LoginAttempts]([Email], [AttemptedAt]);
GO

-- Verify
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'SEC_%' ORDER BY TABLE_NAME;
GO
