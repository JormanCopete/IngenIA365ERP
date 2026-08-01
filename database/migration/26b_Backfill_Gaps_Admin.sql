-- =============================================================================
-- File:        26b_Backfill_Gaps_Admin.sql
-- Phase:       Fase 1 — Identidad Central Federada (feature 002) · gaps 6-7
-- Idempotent:  YES (sys.columns / sys.key_constraints guards).
-- Reversible:  Parcial. Los ADD COLUMN son reversibles (DROP COLUMN); el
--              drop de UQ_ADM_Tenants_Identifier y la recreacion del Id de
--              ADM_PasswordResetTokens requieren backup previo para revertir.
--
-- ▸ DATABASE TARGET: IngenIA365ERP_Admin  (cadena SqlServerAdmin)
--   Complementaria de 26_Backfill_Gaps_Tenant.sql (mismo lote).
--
-- Origen: gaps de esquema descubiertos en las pruebas end-to-end del
-- feature 002. Documentados originalmente en
-- docs/operaciones/setup-local-pruebas.md §3.1 (gaps 6-7); este script es la
-- version formal para el pipeline oficial.
--
-- Sintomas que resuelve:
--   · Gap 6: los INSERT de RegisterTenantWithAdmin fallaban porque la tabla
--     ADM_Tenants legacy no tenia las columnas del refactor de tenants con
--     perfil comercial, y las columnas legacy Identifier/LicenseType eran
--     NOT NULL pero ya no se mapean en la entidad Tenant.
--   · Gap 7: InvalidCastException Int64->Int32 (HTTP 500) en
--     POST /api/auth/password/forgot — el DDL 15e creo
--     ADM_PasswordResetTokens.Id como BIGINT IDENTITY pero la entidad
--     PasswordResetToken : AuditableEntity espera Id INT (mismo defecto que
--     el gap 4 de SEC_RolePermissions).
--
-- ⚠ Gap 7 BORRA las filas de ADM_PasswordResetTokens antes de recrear el Id
--   (son tokens de reset transitorios: los pendientes expiran en 1 hora y el
--   PasswordResetTokenCleanupJob purga los consumidos; perderlos solo obliga
--   a re-solicitar un reset en curso).
--
-- Como ejecutar:
--   sqlcmd -S <server> -E -C -I -d IngenIA365ERP_Admin -i 26b_Backfill_Gaps_Admin.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-- ===========================================================================
-- Gap 6: ADM_Tenants — columnas del refactor + legacy nullable
--   Incluye ademas las columnas base/auditoria y legales que un ADM_Tenants
--   100% legacy (shape ErpTenantInfo de Fase 0) no tiene: sin ellas, todo
--   SELECT del Tenant actual truena con Invalid column name (descubierto al
--   correr los DDL contra una BD virgen en el integration test T118).
-- ===========================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'PublicId') ALTER TABLE dbo.ADM_Tenants ADD PublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ADM_Tenants_PublicId DEFAULT NEWID();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'Nit') ALTER TABLE dbo.ADM_Tenants ADD Nit NVARCHAR(20) NOT NULL CONSTRAINT DF_ADM_Tenants_Nit DEFAULT N'';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'LegalName') ALTER TABLE dbo.ADM_Tenants ADD LegalName NVARCHAR(300) NOT NULL CONSTRAINT DF_ADM_Tenants_LegalName DEFAULT N'';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'LegalAddress') ALTER TABLE dbo.ADM_Tenants ADD LegalAddress NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'TaxRegime') ALTER TABLE dbo.ADM_Tenants ADD TaxRegime NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'CreatedBy') ALTER TABLE dbo.ADM_Tenants ADD CreatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'UpdatedAt') ALTER TABLE dbo.ADM_Tenants ADD UpdatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'UpdatedBy') ALTER TABLE dbo.ADM_Tenants ADD UpdatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'IsDeleted') ALTER TABLE dbo.ADM_Tenants ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ADM_Tenants_IsDeleted DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'DeletedAt') ALTER TABLE dbo.ADM_Tenants ADD DeletedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'DeletedBy') ALTER TABLE dbo.ADM_Tenants ADD DeletedBy NVARCHAR(200) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'Subdomain') ALTER TABLE dbo.ADM_Tenants ADD Subdomain NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'PlanType') ALTER TABLE dbo.ADM_Tenants ADD PlanType NVARCHAR(50) NOT NULL CONSTRAINT DF_ADM_Tenants_PlanType DEFAULT N'Basic';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'StorageLimitMb') ALTER TABLE dbo.ADM_Tenants ADD StorageLimitMb BIGINT NOT NULL CONSTRAINT DF_ADM_Tenants_StorageLimitMb DEFAULT 5120;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'DatabaseName') ALTER TABLE dbo.ADM_Tenants ADD DatabaseName NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'ContactEmail') ALTER TABLE dbo.ADM_Tenants ADD ContactEmail NVARCHAR(200) NOT NULL CONSTRAINT DF_ADM_Tenants_ContactEmail DEFAULT N'';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'ContactPhone') ALTER TABLE dbo.ADM_Tenants ADD ContactPhone NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'ActivatedAt') ALTER TABLE dbo.ADM_Tenants ADD ActivatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'SuspendedAt') ALTER TABLE dbo.ADM_Tenants ADD SuspendedAt DATETIME2(0) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'RowVersion') ALTER TABLE dbo.ADM_Tenants ADD RowVersion ROWVERSION NOT NULL;
GO

-- Legacy: Identifier + LicenseType eran NOT NULL pero ya no se mapean en la
-- entidad Tenant. Drop de la unique + volverlas nullable para los INSERT
-- del handler RegisterTenantWithAdmin.
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ADM_Tenants_Identifier')
    ALTER TABLE dbo.ADM_Tenants DROP CONSTRAINT UQ_ADM_Tenants_Identifier;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'Identifier' AND is_nullable = 0)
    ALTER TABLE dbo.ADM_Tenants ALTER COLUMN Identifier NVARCHAR(100) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'LicenseType' AND is_nullable = 0)
    ALTER TABLE dbo.ADM_Tenants ALTER COLUMN LicenseType NVARCHAR(50) NULL;
GO

-- ===========================================================================
-- Gap 7: ADM_PasswordResetTokens.Id BIGINT -> INT IDENTITY
-- ===========================================================================
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
           WHERE c.object_id = OBJECT_ID('dbo.ADM_PasswordResetTokens') AND c.name = 'Id' AND t.name = 'bigint')
BEGIN
    DELETE FROM dbo.ADM_PasswordResetTokens;  -- tokens transitorios (ver header)
    DECLARE @pk sysname = (SELECT name FROM sys.key_constraints
                           WHERE parent_object_id = OBJECT_ID('dbo.ADM_PasswordResetTokens') AND type = 'PK');
    IF @pk IS NOT NULL EXEC('ALTER TABLE dbo.ADM_PasswordResetTokens DROP CONSTRAINT [' + @pk + ']');
    ALTER TABLE dbo.ADM_PasswordResetTokens DROP COLUMN Id;
    ALTER TABLE dbo.ADM_PasswordResetTokens ADD Id INT IDENTITY(1,1) NOT NULL;
    ALTER TABLE dbo.ADM_PasswordResetTokens ADD CONSTRAINT PK_ADM_PasswordResetTokens PRIMARY KEY (Id);
    PRINT '+ Gap 7: Id recreado como INT IDENTITY';
END
ELSE
    PRINT '= Gap 7: Id ya es INT — sin cambios';
GO

PRINT '+ 26b_Backfill_Gaps_Admin aplicado (gaps 6-7).';
GO
