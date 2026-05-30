-- ============================================================================
-- IngenIA365ERP — Schema 13h: CMP_HabeasDataPolicyVersions + CMP_HabeasDataConsents
-- ============================================================================
--
-- Contexto (T126 / Fase 0 — US7):
--   Habeas data (Ley 1581/2012 Colombia). El tenant publica versiones de su
--   política y cada titular acepta/revoca contra una versión específica.
--   El historial es append-only — revocar = añadir fila Action=Revoked, no
--   editar la fila de Accept.
--
-- Idempotencia: `CREATE TABLE` / `CREATE INDEX` con `IF NOT EXISTS`.
-- ============================================================================

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CMP_HabeasDataPolicyVersions')
BEGIN
    CREATE TABLE [dbo].[CMP_HabeasDataPolicyVersions]
    (
        [Id]                 INT IDENTITY(1,1) NOT NULL,
        [PublicId]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_CMP_HabeasPolicy_PublicId] DEFAULT NEWID(),
        [TenantId]           INT NOT NULL,
        [VersionNumber]      INT NOT NULL,
        [Title]              NVARCHAR(300) NOT NULL,
        [ContentMarkdown]    NVARCHAR(MAX) NOT NULL,
        [Sha256Hex]          NVARCHAR(64) NOT NULL,
        [EffectiveFrom]      DATETIME2(7) NOT NULL,
        [EffectiveTo]        DATETIME2(7) NULL,
        [PublishedBy]        NVARCHAR(100) NOT NULL,
        [RowVersion]         ROWVERSION NOT NULL,
        [CreatedAt]          DATETIME2(7) NOT NULL CONSTRAINT [DF_CMP_HabeasPolicy_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [CreatedBy]          NVARCHAR(100) NULL,
        [UpdatedAt]          DATETIME2(7) NULL,
        [UpdatedBy]          NVARCHAR(100) NULL,
        [IsDeleted]          BIT NOT NULL CONSTRAINT [DF_CMP_HabeasPolicy_IsDeleted] DEFAULT 0,
        [DeletedAt]          DATETIME2(7) NULL,
        [DeletedBy]          NVARCHAR(100) NULL,
        CONSTRAINT [PK_CMP_HabeasDataPolicyVersions] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UK_CMP_HabeasPolicyVersions_PublicId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UK_CMP_HabeasPolicyVersions_PublicId]
        ON [dbo].[CMP_HabeasDataPolicyVersions]([PublicId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UK_CMP_HabeasPolicyVersions_TenantVersion')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UK_CMP_HabeasPolicyVersions_TenantVersion]
        ON [dbo].[CMP_HabeasDataPolicyVersions]([TenantId], [VersionNumber]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UK_CMP_HabeasPolicyVersions_Current')
BEGIN
    -- A lo sumo UNA versión vigente por tenant (EffectiveTo IS NULL).
    CREATE UNIQUE NONCLUSTERED INDEX [UK_CMP_HabeasPolicyVersions_Current]
        ON [dbo].[CMP_HabeasDataPolicyVersions]([TenantId])
        WHERE [EffectiveTo] IS NULL AND [IsDeleted] = 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CMP_HabeasDataConsents')
BEGIN
    CREATE TABLE [dbo].[CMP_HabeasDataConsents]
    (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [PublicId]          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_CMP_HabeasConsents_PublicId] DEFAULT NEWID(),
        [TenantId]          INT NOT NULL,
        [PersonId]          INT NOT NULL,
        [PolicyVersionId]   INT NOT NULL,
        [Action]            NVARCHAR(20) NOT NULL,
        [ActionAt]          DATETIME2(7) NOT NULL,
        [ActionBy]          NVARCHAR(100) NOT NULL,
        [Channel]           NVARCHAR(50) NULL,
        [Notes]             NVARCHAR(2000) NULL,
        [RowVersion]        ROWVERSION NOT NULL,
        [CreatedAt]         DATETIME2(7) NOT NULL CONSTRAINT [DF_CMP_HabeasConsents_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [CreatedBy]         NVARCHAR(100) NULL,
        [UpdatedAt]         DATETIME2(7) NULL,
        [UpdatedBy]         NVARCHAR(100) NULL,
        [IsDeleted]         BIT NOT NULL CONSTRAINT [DF_CMP_HabeasConsents_IsDeleted] DEFAULT 0,
        [DeletedAt]         DATETIME2(7) NULL,
        [DeletedBy]         NVARCHAR(100) NULL,
        CONSTRAINT [PK_CMP_HabeasDataConsents] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CMP_HabeasConsents_PolicyVersion] FOREIGN KEY ([PolicyVersionId])
            REFERENCES [dbo].[CMP_HabeasDataPolicyVersions]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UK_CMP_HabeasConsents_PublicId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UK_CMP_HabeasConsents_PublicId]
        ON [dbo].[CMP_HabeasDataConsents]([PublicId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CMP_HabeasConsents_History')
BEGIN
    -- Patrón "historial cronológico del titular X en tenant T".
    CREATE NONCLUSTERED INDEX [IX_CMP_HabeasConsents_History]
        ON [dbo].[CMP_HabeasDataConsents]([TenantId], [PersonId], [ActionAt])
        INCLUDE ([PolicyVersionId], [Action], [Channel])
        WHERE [IsDeleted] = 0;
END
GO
