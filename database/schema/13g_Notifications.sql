-- ============================================================================
-- IngenIA365ERP — Schema 13g: COR_Notifications + COR_NotificationDeliveryFailures
-- ============================================================================
--
-- Contexto (T115 / Fase 0 — US6):
--   Notificaciones in-app + correo con reintentos y diagnóstico de fallos
--   (FR-040..FR-042). Persistencia del log canónico que el handler real
--   (T118) crea, el dispatcher de email (T119) procesa y el inbox Blazor
--   (T122) lee.
--
-- Idempotencia:
--   `CREATE TABLE` / `CREATE INDEX` envueltos en `IF NOT EXISTS`.
-- ============================================================================

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'COR_Notifications')
BEGIN
    CREATE TABLE [dbo].[COR_Notifications]
    (
        [Id]                     BIGINT IDENTITY(1,1) NOT NULL,
        [PublicId]               UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_COR_Notifications_PublicId] DEFAULT NEWID(),
        [TenantId]               INT NOT NULL,
        [RecipientUserPublicId]  UNIQUEIDENTIFIER NOT NULL,
        [Type]                   NVARCHAR(80) NOT NULL,
        [Subject]                NVARCHAR(500) NOT NULL,
        [Body]                   NVARCHAR(MAX) NOT NULL,
        [ChannelsMask]           INT NOT NULL,
        [EmailStatus]            NVARCHAR(20) NOT NULL CONSTRAINT [DF_COR_Notifications_EmailStatus] DEFAULT N'Pending',
        [EmailSentAt]            DATETIME2(7) NULL,
        [EmailAttemptCount]      INT NOT NULL CONSTRAINT [DF_COR_Notifications_EmailAttemptCount] DEFAULT 0,
        [ReadAt]                 DATETIME2(7) NULL,
        [ArchivedAt]             DATETIME2(7) NULL,
        [RowVersion]             ROWVERSION NOT NULL,
        [CreatedAt]              DATETIME2(7) NOT NULL CONSTRAINT [DF_COR_Notifications_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [CreatedBy]              NVARCHAR(100) NULL,
        [UpdatedAt]              DATETIME2(7) NULL,
        [UpdatedBy]              NVARCHAR(100) NULL,
        [IsDeleted]              BIT NOT NULL CONSTRAINT [DF_COR_Notifications_IsDeleted] DEFAULT 0,
        [DeletedAt]              DATETIME2(7) NULL,
        [DeletedBy]              NVARCHAR(100) NULL,
        CONSTRAINT [PK_COR_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UK_COR_Notifications_PublicId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UK_COR_Notifications_PublicId]
        ON [dbo].[COR_Notifications]([PublicId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_COR_Notifications_Inbox')
BEGIN
    -- Patrón de query del inbox: por tenant, recipient, ordenar por ReadAt/CreatedAt.
    CREATE NONCLUSTERED INDEX [IX_COR_Notifications_Inbox]
        ON [dbo].[COR_Notifications]([TenantId], [RecipientUserPublicId], [ReadAt])
        INCLUDE ([Type], [Subject], [CreatedAt], [ArchivedAt])
        WHERE [IsDeleted] = 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_COR_Notifications_EmailStatus')
BEGIN
    -- Cola del email dispatcher (Pending + Failed con retry pendiente).
    CREATE NONCLUSTERED INDEX [IX_COR_Notifications_EmailStatus]
        ON [dbo].[COR_Notifications]([EmailStatus])
        INCLUDE ([RecipientUserPublicId], [Subject], [Body], [ChannelsMask], [EmailAttemptCount])
        WHERE [IsDeleted] = 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'COR_NotificationDeliveryFailures')
BEGIN
    CREATE TABLE [dbo].[COR_NotificationDeliveryFailures]
    (
        [Id]              BIGINT IDENTITY(1,1) NOT NULL,
        [PublicId]        UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_COR_NotDelFail_PublicId] DEFAULT NEWID(),
        [NotificationId]  BIGINT NOT NULL,
        [Channel]         NVARCHAR(20) NOT NULL,
        [AttemptNumber]   INT NOT NULL,
        [ErrorMessage]    NVARCHAR(2000) NOT NULL,
        [FailedAt]        DATETIME2(7) NOT NULL,
        [RowVersion]      ROWVERSION NOT NULL,
        [CreatedAt]       DATETIME2(7) NOT NULL CONSTRAINT [DF_COR_NotDelFail_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [CreatedBy]       NVARCHAR(100) NULL,
        [UpdatedAt]       DATETIME2(7) NULL,
        [UpdatedBy]       NVARCHAR(100) NULL,
        [IsDeleted]       BIT NOT NULL CONSTRAINT [DF_COR_NotDelFail_IsDeleted] DEFAULT 0,
        [DeletedAt]       DATETIME2(7) NULL,
        [DeletedBy]       NVARCHAR(100) NULL,
        CONSTRAINT [PK_COR_NotificationDeliveryFailures] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_COR_NotDelFail_Notifications] FOREIGN KEY ([NotificationId])
            REFERENCES [dbo].[COR_Notifications]([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_COR_NotDeliveryFailures_NotificationId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_COR_NotDeliveryFailures_NotificationId]
        ON [dbo].[COR_NotificationDeliveryFailures]([NotificationId]);
END
GO
