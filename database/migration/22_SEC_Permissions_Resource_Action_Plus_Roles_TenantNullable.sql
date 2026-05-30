-- =============================================================================
-- File:        22_SEC_Permissions_Resource_Action_Plus_Roles_TenantNullable.sql
-- Phase:       Fase 0 — Cimientos técnicos · US2 (fix runtime seed)
-- Idempotent:  YES (sys.columns guards).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Problemas resueltos:
--   1. SEC_Permissions no tiene `Resource`/`Action`. La tabla se creó con el
--      schema legacy (Module/Feature/PermissionCode). Añadimos las columnas
--      nuevas y backfilleamos desde PermissionCode si existe.
--
--   2. SEC_Roles.TenantId es NOT NULL en algunos entornos. El modelo nuevo
--      lo declara como nullable (rol SaaS-global = TenantId NULL); lo
--      cambiamos para que acepte nulos.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 22_SEC_Permissions_Resource_Action_Plus_Roles_TenantNullable.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-----------------------------------------------------------------------------
-- 1. SEC_Permissions — Resource + Action
-----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Permissions')
BEGIN
    RAISERROR('La tabla SEC_Permissions no existe.', 16, 1);
    RETURN;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Resource' AND Object_ID = OBJECT_ID('dbo.SEC_Permissions'))
BEGIN
    ALTER TABLE dbo.SEC_Permissions ADD [Resource] NVARCHAR(100) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Action' AND Object_ID = OBJECT_ID('dbo.SEC_Permissions'))
BEGIN
    ALTER TABLE dbo.SEC_Permissions ADD [Action] NVARCHAR(50) NULL;
END;
GO

-- Backfill: si hay PermissionCode legacy "Module.Feature.Action",
-- particionamos a Resource = "Module.Feature" y Action = "Action".
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PermissionCode' AND Object_ID = OBJECT_ID('dbo.SEC_Permissions'))
BEGIN
    UPDATE p
    SET p.[Resource] = CASE
            WHEN CHARINDEX('.', REVERSE(PermissionCode)) > 0
                THEN LEFT(PermissionCode, LEN(PermissionCode) - CHARINDEX('.', REVERSE(PermissionCode)))
            ELSE PermissionCode
        END,
        p.[Action] = CASE
            WHEN CHARINDEX('.', REVERSE(PermissionCode)) > 0
                THEN RIGHT(PermissionCode, CHARINDEX('.', REVERSE(PermissionCode)) - 1)
            ELSE NULL
        END
    FROM dbo.SEC_Permissions p
    WHERE (p.[Resource] IS NULL OR p.[Resource] = '')
      AND p.PermissionCode IS NOT NULL AND p.PermissionCode <> '';
END;
GO

-- Si no hay datos para backfillear, dejar las columnas con valor por defecto vacío
-- para poder hacerlas NOT NULL.
UPDATE dbo.SEC_Permissions SET [Resource] = '' WHERE [Resource] IS NULL;
UPDATE dbo.SEC_Permissions SET [Action]   = '' WHERE [Action]   IS NULL;
GO

-- Promover a NOT NULL para que coincida con la entidad de dominio.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'Resource' AND Object_ID = OBJECT_ID('dbo.SEC_Permissions') AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.SEC_Permissions ALTER COLUMN [Resource] NVARCHAR(100) NOT NULL;
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'Action' AND Object_ID = OBJECT_ID('dbo.SEC_Permissions') AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.SEC_Permissions ALTER COLUMN [Action] NVARCHAR(50) NOT NULL;
END;
GO

-- Unique compuesto (Resource, Action) si no existe.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UK_SEC_Permissions_ResourceAction' AND object_id = OBJECT_ID('dbo.SEC_Permissions'))
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM dbo.SEC_Permissions
        WHERE [Resource] <> '' AND [Action] <> ''
        GROUP BY [Resource], [Action]
        HAVING COUNT(*) > 1)
    BEGIN
        CREATE UNIQUE INDEX UK_SEC_Permissions_ResourceAction
            ON dbo.SEC_Permissions([Resource], [Action])
            WHERE [Resource] <> '' AND [Action] <> '';
    END
    ELSE
    BEGIN
        PRINT 'AVISO: SEC_Permissions tiene duplicados (Resource, Action). Limpiar antes de crear el índice.';
    END
END;
GO

-----------------------------------------------------------------------------
-- 2. SEC_Roles.TenantId → NULL permitido
-----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'TenantId' AND Object_ID = OBJECT_ID('dbo.SEC_Roles') AND is_nullable = 0)
BEGIN
    -- Si existe FK obligatoria, no nos preocupamos aquí — el seeder solo
    -- inserta con TenantId=NULL en filas SaaS-global.
    ALTER TABLE dbo.SEC_Roles ALTER COLUMN TenantId INT NULL;
END;
GO

PRINT 'SEC_Permissions (Resource/Action) + SEC_Roles (TenantId nullable) backfill: OK';
GO
