-- =============================================================================
-- File:        21_Roles_Scope_BuiltIn.sql
-- Phase:       Fase 0 — Cimientos técnicos · US2 (T068)
-- Idempotent:  YES (sys.columns + sys.indexes guards).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Añade a SEC_Roles las columnas que materializan el RBAC scopeado por tenant:
--   - Code         : identidad de máquina (CompanyAdmin, Auditor, Operator, ReadOnly).
--   - TenantId     : null = rol SaaS-global; otro = scope tenant.
--   - IsBuiltIn    : rol provisto por el sistema. CompanyAdmin/Auditor no se borran.
--   - IsAssignable : false en roles internos no asignables a usuarios reales.
--
-- Más índice único compuesto (TenantId, Code) filtrado por IsDeleted=0.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 21_Roles_Scope_BuiltIn.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Roles')
BEGIN
    RAISERROR('La tabla SEC_Roles no existe. Crea el schema base antes de correr esta migración.', 16, 1);
    RETURN;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Code' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD Code NVARCHAR(40) NOT NULL CONSTRAINT DF_SEC_Roles_Code DEFAULT N'';
GO

-- Backfill: si Code está vacío y Name no lo está, usa Name como Code (uppercase, sin espacios).
UPDATE dbo.SEC_Roles
SET    Code = UPPER(REPLACE(Name, ' ', ''))
WHERE  (Code IS NULL OR Code = N'')
  AND  Name IS NOT NULL AND Name <> N'';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'TenantId' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD TenantId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsBuiltIn' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD IsBuiltIn BIT NOT NULL CONSTRAINT DF_SEC_Roles_IsBuiltIn DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsAssignable' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD IsAssignable BIT NOT NULL CONSTRAINT DF_SEC_Roles_IsAssignable DEFAULT 1;
GO

-- Marcar legacy IsSystemRole=1 también como IsBuiltIn (compatibilidad — los roles
-- previamente sembrados como "system" pasan a built-in en el modelo nuevo).
UPDATE dbo.SEC_Roles
SET    IsBuiltIn = 1
WHERE  IsSystemRole = 1 AND IsBuiltIn = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SEC_Roles_Tenant_Code_NotDeleted' AND object_id = OBJECT_ID('dbo.SEC_Roles'))
BEGIN
    -- Solo se crea si no hay duplicados existentes (evita fallo en bases con datos legados sucios).
    IF NOT EXISTS (
        SELECT 1 FROM dbo.SEC_Roles
        WHERE IsDeleted = 0
        GROUP BY TenantId, Code
        HAVING COUNT(*) > 1)
    BEGIN
        CREATE UNIQUE INDEX UX_SEC_Roles_Tenant_Code_NotDeleted
            ON dbo.SEC_Roles(TenantId, Code)
            WHERE IsDeleted = 0;
    END
    ELSE
    BEGIN
        PRINT 'AVISO: SEC_Roles contiene duplicados (TenantId, Code). Limpiar antes de crear el índice único.';
    END
END;
GO

PRINT 'SEC_Roles backfill (Code/TenantId/IsBuiltIn/IsAssignable): OK';
GO
