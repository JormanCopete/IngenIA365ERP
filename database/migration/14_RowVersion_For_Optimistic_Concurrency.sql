-- ============================================================================
-- IngenIA365ERP — Migración 14: RowVersion para concurrencia optimista
-- ============================================================================
--
-- Contexto (T032 / Fase 0 — Cimientos técnicos):
--   La constitución exige concurrencia optimista en todas las entidades
--   editables (FR-022). EF Core mapea esa convención a `IsConcurrencyToken`
--   sobre la columna `RowVersion` que aparece en `BaseEntity`/`BaseEntityLong`
--   tras T011. Esta migración añade la columna física en SQL Server a las
--   tablas auditables que aún no la tienen.
--
-- Estrategia:
--   Recorre dinámicamente todas las tablas que tengan `CreatedAt` (criterio
--   de "auditable") y que no tengan `RowVersion`. Añade
--   `RowVersion ROWVERSION NOT NULL`. SQL Server poblará el valor al vuelo;
--   `ROWVERSION` es read-only y no admite default explícito.
--
-- Idempotencia:
--   Reentrante. El cursor solo procesa tablas que no tienen la columna,
--   y el `ALTER TABLE` falla silencioso si otro proceso paralelo ya añadió
--   la columna entre el FETCH y el EXEC.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d <database> -i 14_RowVersion_For_Optimistic_Concurrency.sql
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'================================================================';
PRINT N'Migración 14: Añadir RowVersion a tablas auditables';
PRINT N'================================================================';

DECLARE @TablesModified INT = 0;
DECLARE @SchemaName SYSNAME, @TableName SYSNAME, @Sql NVARCHAR(MAX);

DECLARE TableCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT c.TABLE_SCHEMA, c.TABLE_NAME
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.COLUMN_NAME = N'CreatedAt'
  AND EXISTS (
      SELECT 1 FROM INFORMATION_SCHEMA.TABLES t
      WHERE t.TABLE_SCHEMA = c.TABLE_SCHEMA
        AND t.TABLE_NAME = c.TABLE_NAME
        AND t.TABLE_TYPE = N'BASE TABLE'
  )
  AND NOT EXISTS (
      SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS x
      WHERE x.TABLE_SCHEMA = c.TABLE_SCHEMA
        AND x.TABLE_NAME = c.TABLE_NAME
        AND x.COLUMN_NAME = N'RowVersion'
  )
ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME;

OPEN TableCursor;
FETCH NEXT FROM TableCursor INTO @SchemaName, @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName)
             + N' ADD RowVersion ROWVERSION NOT NULL;';

    BEGIN TRY
        EXEC sp_executesql @Sql;
        SET @TablesModified = @TablesModified + 1;
        PRINT N'  + ' + @SchemaName + N'.' + @TableName + N' — RowVersion añadida.';
    END TRY
    BEGIN CATCH
        PRINT N'  ! ' + @SchemaName + N'.' + @TableName + N' — ' + ERROR_MESSAGE();
    END CATCH;

    FETCH NEXT FROM TableCursor INTO @SchemaName, @TableName;
END;

CLOSE TableCursor;
DEALLOCATE TableCursor;

PRINT N'';
PRINT N'Total de tablas modificadas: ' + CAST(@TablesModified AS NVARCHAR(10));
PRINT N'14_RowVersion_For_Optimistic_Concurrency: OK';
GO
