-- =============================================================================
-- File:        25_SEC_Roles_TenantId_Convert_To_Int_v2.sql
-- Phase:       Fase 0 — Cimientos técnicos · US2 (fix migración 24)
-- Idempotent:  YES.
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Contexto:
--   La migración 24 falló por un orden de operaciones imposible:
--     - El UPDATE tried to set TenantId = NULL pero la columna era NOT NULL.
--     - El ALTER COLUMN INT falló porque 'system' seguía ahí.
--   La 24 no abortó tras esos errores (SET XACT_ABORT desactivado por default).
--
--   Orden correcto:
--     1. Drop dependencias (default + índice) — idempotente si la 23/24 ya lo hizo.
--     2. ALTER COLUMN TenantId NVARCHAR(50) NULL — permite NULL pero MANTIENE el tipo,
--        por lo que NO necesita convertir valores existentes.
--     3. UPDATE TenantId = NULL donde TRY_CAST AS INT IS NULL — sanea 'system' y similares.
--     4. ALTER COLUMN TenantId INT NULL — ya es seguro porque todos los valores
--        son numéricos o NULL.
--     5. Recrear el índice único compuesto.
--
--   El uso de SET XACT_ABORT ON + transacción garantiza que si algo falla,
--   nada queda a medias.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 25_SEC_Roles_TenantId_Convert_To_Int_v2.sql
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Roles')
BEGIN
    RAISERROR('La tabla SEC_Roles no existe.', 16, 1);
    RETURN;
END;
GO

-- Si TenantId ya es INT, no hay nada que hacer.
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

BEGIN TRANSACTION;
GO

-----------------------------------------------------------------------------
-- 1. Drop default constraint sobre TenantId si quedó.
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
END
ELSE
    PRINT N'  = No había default constraint sobre TenantId.';
GO

-----------------------------------------------------------------------------
-- 2. Drop índice único compuesto si está.
-----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SEC_Roles_Tenant_Code_NotDeleted'
      AND object_id = OBJECT_ID('dbo.SEC_Roles'))
BEGIN
    DROP INDEX UX_SEC_Roles_Tenant_Code_NotDeleted ON dbo.SEC_Roles;
    PRINT N'  + Índice UX_SEC_Roles_Tenant_Code_NotDeleted eliminado (temporal).';
END
ELSE
    PRINT N'  = Índice UX_SEC_Roles_Tenant_Code_NotDeleted no existía.';
GO

-----------------------------------------------------------------------------
-- 3. Allow NULL manteniendo el tipo actual.
-----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'TenantId'
      AND Object_ID = OBJECT_ID('dbo.SEC_Roles')
      AND is_nullable = 0)
BEGIN
    -- Detectar el max length actual (puede ser cualquier NVARCHAR).
    DECLARE @maxlen SMALLINT;
    SELECT @maxlen = max_length
    FROM sys.columns
    WHERE Name = 'TenantId' AND Object_ID = OBJECT_ID('dbo.SEC_Roles');
    -- sys.columns.max_length es bytes; NVARCHAR usa 2 bytes/char. -1 = MAX.
    DECLARE @chars SMALLINT = CASE WHEN @maxlen = -1 THEN -1 ELSE @maxlen / 2 END;
    -- Por seguridad usamos un ancho razonable; 255 cubre cualquier identificador.
    DECLARE @target_len NVARCHAR(10) = CASE
        WHEN @chars = -1 THEN N'MAX'
        WHEN @chars < 50 THEN N'50'
        WHEN @chars > 4000 THEN N'4000'
        ELSE CAST(@chars AS NVARCHAR(10))
    END;
    DECLARE @sql NVARCHAR(MAX) = N'ALTER TABLE dbo.SEC_Roles ALTER COLUMN TenantId NVARCHAR(' + @target_len + N') NULL';
    EXEC sp_executesql @sql;
    PRINT N'  + SEC_Roles.TenantId ahora permite NULL (temporalmente NVARCHAR(' + @target_len + N')).';
END
ELSE
    PRINT N'  = SEC_Roles.TenantId ya permitía NULL.';
GO

-----------------------------------------------------------------------------
-- 4. Sanear valores no-numéricos a NULL.
-----------------------------------------------------------------------------
DECLARE @cleaned INT;
UPDATE dbo.SEC_Roles
SET TenantId = NULL
WHERE TenantId IS NOT NULL
  AND TRY_CAST(TenantId AS INT) IS NULL;
SET @cleaned = @@ROWCOUNT;
PRINT CONCAT(N'  + Valores no-numéricos saneados a NULL: ', @cleaned);
GO

-----------------------------------------------------------------------------
-- 5. ALTER COLUMN → INT NULL.
-----------------------------------------------------------------------------
ALTER TABLE dbo.SEC_Roles ALTER COLUMN TenantId INT NULL;
PRINT N'  + SEC_Roles.TenantId ahora es INT NULL.';
GO

-----------------------------------------------------------------------------
-- 6. Recrear el índice único compuesto.
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
END
ELSE
    PRINT N'  = Índice UX_SEC_Roles_Tenant_Code_NotDeleted ya existía.';
GO

COMMIT TRANSACTION;
PRINT N'SEC_Roles.TenantId convertido a INT NULL (v2): OK';
GO
