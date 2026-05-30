-- =============================================================================
-- File:        23_SEC_Roles_TenantId_Nullable.sql
-- Phase:       Fase 0 — Cimientos técnicos · US2 (fix migración 22)
-- Idempotent:  YES (verifica el estado antes de cada operación).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Contexto:
--   La migración 22 intentó hacer SEC_Roles.TenantId nullable pero falló por
--   dependencias: (a) una default constraint auto-generada DF__SEC_Roles__Tenan…
--   y (b) el índice único UX_SEC_Roles_Tenant_Code_NotDeleted (creado en
--   migración 21).
--
-- Estrategia: drop deps → alter column → recreate index.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 23_SEC_Roles_TenantId_Nullable.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Roles')
BEGIN
    RAISERROR('La tabla SEC_Roles no existe. Crea el schema base antes.', 16, 1);
    RETURN;
END;
GO

-----------------------------------------------------------------------------
-- 1. Drop default constraint sobre TenantId (nombre auto-generado).
-----------------------------------------------------------------------------
DECLARE @df_name SYSNAME;
SELECT @df_name = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c
  ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID('dbo.SEC_Roles')
  AND c.name = 'TenantId';

IF @df_name IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'ALTER TABLE dbo.SEC_Roles DROP CONSTRAINT [' + @df_name + N']';
    EXEC sp_executesql @sql;
    PRINT N'  + Default constraint ' + @df_name + N' eliminada.';
END
ELSE
    PRINT N'  = No había default constraint sobre TenantId.';
GO

-----------------------------------------------------------------------------
-- 2. Drop índice único compuesto (lo recreamos al final).
-----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SEC_Roles_Tenant_Code_NotDeleted'
      AND object_id = OBJECT_ID('dbo.SEC_Roles'))
BEGIN
    DROP INDEX UX_SEC_Roles_Tenant_Code_NotDeleted ON dbo.SEC_Roles;
    PRINT N'  + Índice UX_SEC_Roles_Tenant_Code_NotDeleted eliminado (temporal).';
END;
GO

-----------------------------------------------------------------------------
-- 3. ALTER COLUMN — ahora libre de dependencias.
-----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'TenantId'
      AND Object_ID = OBJECT_ID('dbo.SEC_Roles')
      AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.SEC_Roles ALTER COLUMN TenantId INT NULL;
    PRINT N'  + SEC_Roles.TenantId ahora permite NULL.';
END
ELSE
    PRINT N'  = SEC_Roles.TenantId ya era nullable.';
GO

-----------------------------------------------------------------------------
-- 4. Recrear el índice único compuesto (mismo definitions que migración 21).
-----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SEC_Roles_Tenant_Code_NotDeleted'
      AND object_id = OBJECT_ID('dbo.SEC_Roles'))
BEGIN
    -- Verificar duplicados antes de crear el unique.
    IF NOT EXISTS (
        SELECT 1 FROM dbo.SEC_Roles
        WHERE IsDeleted = 0
        GROUP BY TenantId, Code
        HAVING COUNT(*) > 1)
    BEGIN
        CREATE UNIQUE INDEX UX_SEC_Roles_Tenant_Code_NotDeleted
            ON dbo.SEC_Roles(TenantId, Code)
            WHERE IsDeleted = 0;
        PRINT N'  + Índice UX_SEC_Roles_Tenant_Code_NotDeleted recreado.';
    END
    ELSE
        PRINT N'  ! Hay duplicados (TenantId, Code) — limpiar antes de recrear el índice único.';
END;
GO

PRINT N'SEC_Roles.TenantId nullable: OK';
GO
