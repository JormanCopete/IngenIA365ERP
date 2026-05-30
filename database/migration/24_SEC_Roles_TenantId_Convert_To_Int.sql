-- =============================================================================
-- File:        24_SEC_Roles_TenantId_Convert_To_Int.sql
-- Phase:       Fase 0 — Cimientos técnicos · US2 (fix migración 23)
-- Idempotent:  YES.
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Contexto:
--   El SEC_Roles.TenantId del entorno está como NVARCHAR (heredado de
--   IdentitySeedData que insertaba 'system'). El modelo nuevo lo declara
--   `int?` (FK a ADM_Tenants.Id). La migración 23 dropeó las dependencias
--   pero el ALTER COLUMN INT NULL falló al intentar convertir 'system'.
--
--   Plan:
--     1. UPDATE: poner NULL en cualquier valor no-numérico (TRY_CAST).
--     2. Drop dependencias (default + índice) si quedaron — la 23 ya las
--        eliminó, pero re-aplicamos la guarda por idempotencia.
--     3. ALTER COLUMN → INT NULL.
--     4. Recrear el índice único compuesto (igual definitions que 21/23).
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 24_SEC_Roles_TenantId_Convert_To_Int.sql
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Roles')
BEGIN
    RAISERROR('La tabla SEC_Roles no existe.', 16, 1);
    RETURN;
END;
GO

-- Si TenantId ya es INT, nada que hacer.
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types  t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.SEC_Roles')
      AND c.name = 'TenantId'
      AND t.name IN ('int', 'integer'))
BEGIN
    PRINT N'  = SEC_Roles.TenantId ya es INT — nada que hacer.';
    RETURN;
END;
GO

-----------------------------------------------------------------------------
-- 1. Sanear valores no-numéricos a NULL (TRY_CAST).
-----------------------------------------------------------------------------
DECLARE @cleaned INT;
UPDATE dbo.SEC_Roles
SET TenantId = NULL
WHERE TenantId IS NOT NULL
  AND TRY_CAST(TenantId AS INT) IS NULL;
SET @cleaned = @@ROWCOUNT;
PRINT CONCAT(N'  + Valores no-numéricos en SEC_Roles.TenantId convertidos a NULL: ', @cleaned);
GO

-----------------------------------------------------------------------------
-- 2. Drop default constraint (si la 23 no la dropeó por algún motivo).
-----------------------------------------------------------------------------
DECLARE @df_name SYSNAME;
SELECT @df_name = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID('dbo.SEC_Roles') AND c.name = 'TenantId';

IF @df_name IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.SEC_Roles DROP CONSTRAINT [' + @df_name + N']');
    PRINT N'  + Default constraint ' + @df_name + N' eliminada.';
END;
GO

-----------------------------------------------------------------------------
-- 3. Drop índice único compuesto.
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
-- 4. ALTER COLUMN → INT NULL.
-----------------------------------------------------------------------------
ALTER TABLE dbo.SEC_Roles ALTER COLUMN TenantId INT NULL;
PRINT N'  + SEC_Roles.TenantId ahora es INT NULL.';
GO

-----------------------------------------------------------------------------
-- 5. Recrear el índice único compuesto.
-----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SEC_Roles_Tenant_Code_NotDeleted'
      AND object_id = OBJECT_ID('dbo.SEC_Roles'))
BEGIN
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

PRINT N'SEC_Roles.TenantId convertido a INT NULL: OK';
GO
