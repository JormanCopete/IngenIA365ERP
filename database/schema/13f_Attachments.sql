-- ============================================================================
-- IngenIA365ERP — Schema 13f: COR_Attachments (Adjuntos cifrados)
-- ============================================================================
--
-- Contexto (T105 / Fase 0 — US5):
--   Adjuntos cifrados en reposo (FR-030/FR-031). Cada blob se cifra con
--   AES-256-GCM usando una DEK random por archivo, envuelta con la KEK de
--   DataProtection. El blob físico vive en `LocalEncryptedFileStore` u otro
--   backend compatible con `IBlobStore`; la BD solo guarda metadata + DEK
--   envuelta + hash de integridad.
--
-- Propiedad (ownership):
--   Cada adjunto pertenece a una entidad del dominio identificada por
--   (OwnerEntityType, OwnerEntityPublicId). El permiso para descargar se
--   evalúa contra esa entidad propietaria — NO contra el adjunto en sí.
--
-- Idempotencia:
--   `CREATE TABLE` y `CREATE INDEX` envueltos en `IF NOT EXISTS`. El script
--   puede ejecutarse N veces sin fallar.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d <database> -i 13f_Attachments.sql
-- ============================================================================

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'COR_Attachments')
BEGIN
    CREATE TABLE [dbo].[COR_Attachments]
    (
        [Id]                   BIGINT IDENTITY(1,1) NOT NULL,
        [PublicId]             UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_COR_Attachments_PublicId] DEFAULT NEWID(),
        [TenantId]             INT NOT NULL,
        [OwnerEntityType]      NVARCHAR(100) NOT NULL,
        [OwnerEntityPublicId]  UNIQUEIDENTIFIER NOT NULL,
        [FileName]             NVARCHAR(500) NOT NULL,
        [ContentType]          NVARCHAR(200) NOT NULL,
        [SizeBytes]            BIGINT NOT NULL,
        [Sha256Hex]            NVARCHAR(64) NOT NULL,
        [StoragePath]          NVARCHAR(2000) NOT NULL,
        [StorageProvider]      NVARCHAR(50) NOT NULL CONSTRAINT [DF_COR_Attachments_StorageProvider] DEFAULT N'Local',
        [EncryptedDek]         NVARCHAR(1000) NOT NULL,
        [RowVersion]           ROWVERSION NOT NULL,
        [CreatedAt]            DATETIME2(7) NOT NULL CONSTRAINT [DF_COR_Attachments_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [CreatedBy]            NVARCHAR(100) NULL,
        [UpdatedAt]            DATETIME2(7) NULL,
        [UpdatedBy]            NVARCHAR(100) NULL,
        [IsDeleted]            BIT NOT NULL CONSTRAINT [DF_COR_Attachments_IsDeleted] DEFAULT 0,
        [DeletedAt]            DATETIME2(7) NULL,
        [DeletedBy]            NVARCHAR(100) NULL,
        CONSTRAINT [PK_COR_Attachments] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UK_COR_Attachments_PublicId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UK_COR_Attachments_PublicId]
        ON [dbo].[COR_Attachments]([PublicId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_COR_Attachments_Owner')
BEGIN
    -- Lookup patrón "lista adjuntos de la entidad X del tenant T".
    CREATE NONCLUSTERED INDEX [IX_COR_Attachments_Owner]
        ON [dbo].[COR_Attachments]([TenantId], [OwnerEntityType], [OwnerEntityPublicId])
        INCLUDE ([FileName], [ContentType], [SizeBytes], [CreatedAt])
        WHERE [IsDeleted] = 0;
END
GO
