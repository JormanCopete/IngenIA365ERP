-- =============================================================================
-- File:        26_Backfill_Gaps_Tenant.sql
-- Phase:       Fase 1 — Identidad Central Federada (feature 002) · gaps 1-5
-- Idempotent:  YES (sys.columns / sys.indexes / sys.key_constraints guards).
-- Reversible:  Parcial. Los ADD COLUMN son reversibles (DROP COLUMN); el
--              reemplazo de PK de SEC_RolePermissions (gap 4) y el drop del
--              indice legacy (gap 3) requieren backup previo para revertir.
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Origen: gaps de esquema descubiertos al levantar el stack local para las
-- pruebas end-to-end del feature 002 (2026-07-02 y 2026-07-31). Las tablas
-- legacy de Fase 0 no coinciden con las entidades actuales y las migraciones
-- 18-23 no cubrieron estas columnas. Documentados originalmente en
-- docs/operaciones/setup-local-pruebas.md §3.1 (gaps 1-5); este script es la
-- version formal para el pipeline oficial.
--
-- Sintomas que resuelve:
--   · Gap 1: SqlNullValueException / Invalid column name 'PasswordHash' o
--     'FailedLoginAttempts' en seeders de SEC_Users.
--   · Gap 2: Invalid column name 'CreatedAt', 'IsDeleted', ... en
--     SEC_Permissions.
--   · Gap 3: Cannot insert duplicate key row in IX_SEC_Permissions_Code
--     (indice legacy sobre PermissionCode vacio bloquea inserts nuevos).
--   · Gap 4: InvalidCastException Int64->Int32 en seeders Security
--     (SEC_RolePermissions.Id era PK compuesta / BIGINT; la entidad
--     RolePermission : AuditableEntity espera Id INT).
--   · Gap 5: Invalid column name 'EmailStatus' cada 15s en logs
--     (COR_Notifications con schema previo al refactor).
--
-- ⚠ Gap 5 solo es valido con COR_Notifications VACIA (0 filas): los NOT NULL
--   con default marcarian filas existentes con TenantId=0 y recipient vacio.
--
-- Como ejecutar:
--   sqlcmd -S <server> -E -C -I -d IngenIA365ERP -i 26_Backfill_Gaps_Tenant.sql
--   (el flag -I es obligatorio: hay filtered indexes que exigen QUOTED_IDENTIFIER ON)
-- =============================================================================

SET NOCOUNT ON;
GO

-- ===========================================================================
-- Gap 1: SEC_Users — PasswordHash y FailedLoginAttempts (mig 18 los omitio)
-- ===========================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Users') AND name = 'PasswordHash')
    ALTER TABLE dbo.SEC_Users ADD PasswordHash NVARCHAR(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Users') AND name = 'FailedLoginAttempts')
    ALTER TABLE dbo.SEC_Users ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_SEC_Users_FailedLoginAttempts DEFAULT 0;
GO
UPDATE dbo.SEC_Users SET PasswordHash = N'' WHERE PasswordHash IS NULL;
GO

-- ===========================================================================
-- Gap 2: SEC_Permissions — columnas de auditoria (AuditableEntity)
-- ===========================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'CreatedAt')
    ALTER TABLE dbo.SEC_Permissions ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_Permissions_CreatedAt DEFAULT SYSUTCDATETIME();
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'CreatedBy') ALTER TABLE dbo.SEC_Permissions ADD CreatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'UpdatedAt') ALTER TABLE dbo.SEC_Permissions ADD UpdatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'UpdatedBy') ALTER TABLE dbo.SEC_Permissions ADD UpdatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'IsDeleted') ALTER TABLE dbo.SEC_Permissions ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_Permissions_IsDeleted DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'DeletedAt') ALTER TABLE dbo.SEC_Permissions ADD DeletedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'DeletedBy') ALTER TABLE dbo.SEC_Permissions ADD DeletedBy NVARCHAR(200) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'RowVersion') ALTER TABLE dbo.SEC_Permissions ADD RowVersion ROWVERSION NOT NULL;
GO

-- ===========================================================================
-- Gap 3: drop del unique index legacy sobre PermissionCode
--   (el schema nuevo usa (Resource, Action) via UK_SEC_Permissions_ResourceAction)
-- ===========================================================================
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_Permissions_Code' AND object_id = OBJECT_ID('dbo.SEC_Permissions'))
    DROP INDEX IX_SEC_Permissions_Code ON dbo.SEC_Permissions;
GO

-- ===========================================================================
-- Gap 4: SEC_RolePermissions — Id INT IDENTITY + PublicId + auditoria.
--   La tabla legacy tenia PK compuesta (RoleId, PermissionId); se reemplaza
--   por Id INT y unique(RoleId, PermissionId) preserva la integridad.
-- ===========================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'PublicId')
    ALTER TABLE dbo.SEC_RolePermissions ADD PublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_RolePermissions_PublicId DEFAULT NEWID();
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'CreatedAt')
    ALTER TABLE dbo.SEC_RolePermissions ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_RolePermissions_CreatedAt DEFAULT SYSUTCDATETIME();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'CreatedBy') ALTER TABLE dbo.SEC_RolePermissions ADD CreatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'UpdatedAt') ALTER TABLE dbo.SEC_RolePermissions ADD UpdatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'UpdatedBy') ALTER TABLE dbo.SEC_RolePermissions ADD UpdatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'IsDeleted') ALTER TABLE dbo.SEC_RolePermissions ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_RolePermissions_IsDeleted DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'DeletedAt') ALTER TABLE dbo.SEC_RolePermissions ADD DeletedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'DeletedBy') ALTER TABLE dbo.SEC_RolePermissions ADD DeletedBy NVARCHAR(200) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'Id')
BEGIN
    ALTER TABLE dbo.SEC_RolePermissions DROP CONSTRAINT PK_SEC_RolePermissions;
    ALTER TABLE dbo.SEC_RolePermissions ADD Id INT IDENTITY(1,1) NOT NULL;
    ALTER TABLE dbo.SEC_RolePermissions ADD CONSTRAINT PK_SEC_RolePermissions PRIMARY KEY (Id);
    CREATE UNIQUE INDEX UX_SEC_RolePermissions_Role_Permission ON dbo.SEC_RolePermissions(RoleId, PermissionId);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'RowVersion')
    ALTER TABLE dbo.SEC_RolePermissions ADD RowVersion ROWVERSION NOT NULL;
GO

-- ===========================================================================
-- Gap 5: COR_Notifications — columnas del refactor de notificaciones.
--   ⚠ Ejecutar SOLO con la tabla vacia (ver header).
-- ===========================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'TenantId') ALTER TABLE dbo.COR_Notifications ADD TenantId INT NOT NULL CONSTRAINT DF_COR_Notifications_TenantId DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'RecipientUserPublicId') ALTER TABLE dbo.COR_Notifications ADD RecipientUserPublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_COR_Notifications_RecipientUserPublicId DEFAULT '00000000-0000-0000-0000-000000000000';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'Type') ALTER TABLE dbo.COR_Notifications ADD [Type] NVARCHAR(80) NOT NULL CONSTRAINT DF_COR_Notifications_Type DEFAULT N'';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'ChannelsMask') ALTER TABLE dbo.COR_Notifications ADD ChannelsMask INT NOT NULL CONSTRAINT DF_COR_Notifications_ChannelsMask DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'EmailStatus') ALTER TABLE dbo.COR_Notifications ADD EmailStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_COR_Notifications_EmailStatus DEFAULT N'Pending';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'EmailSentAt') ALTER TABLE dbo.COR_Notifications ADD EmailSentAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'EmailAttemptCount') ALTER TABLE dbo.COR_Notifications ADD EmailAttemptCount INT NOT NULL CONSTRAINT DF_COR_Notifications_EmailAttemptCount DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'ReadAt') ALTER TABLE dbo.COR_Notifications ADD ReadAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'ArchivedAt') ALTER TABLE dbo.COR_Notifications ADD ArchivedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'NotificationTemplateId') ALTER TABLE dbo.COR_Notifications ADD NotificationTemplateId INT NULL;
GO

PRINT '+ 26_Backfill_Gaps_Tenant aplicado (gaps 1-5).';
GO
