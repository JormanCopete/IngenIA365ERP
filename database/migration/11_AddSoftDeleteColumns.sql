-- ============================================================================
-- IngenIA365ERP — Migracion 11: Agregar columnas soft-delete (IsDeleted, DeletedAt, DeletedBy)
-- ============================================================================
--
-- Contexto:
--   El codigo .NET (BaseEntity) define las columnas IsDeleted, DeletedAt, DeletedBy
--   y todas las EF Core Configurations tienen un HasQueryFilter(e => !e.IsDeleted).
--   Sin embargo, el DDL inicial (Schema_03, 04, 07 entre otros) fue generado
--   ANTES de que estas columnas se agregaran al BaseEntity, por lo que muchas
--   tablas en SQL Server no las tienen.
--
--   Sintoma: SqlException "Invalid column name 'IsDeleted'" al consultar
--   PAY_PensionProviders, PAY_HealthInsuranceProviders, LND_*, SEC_*, etc.
--
-- Estrategia:
--   Recorrer dinamicamente todas las tablas que tengan ya la columna CreatedAt
--   (es decir, tablas auditables) pero que NO tengan IsDeleted, y agregarlas.
--   Es idempotente: se puede ejecutar varias veces sin error.
--
-- Como ejecutar:
--   sqlcmd -S <server> -d <database> -i 11_AddSoftDeleteColumns.sql
--   (o desde SSMS abrir el script y F5)
--
-- Verificacion al final del script: imprime cuantas tablas se modificaron.
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'================================================================';
PRINT N'Migracion 11: Agregar columnas soft-delete a tablas auditables';
PRINT N'================================================================';
PRINT N'';

DECLARE @TablesModified INT = 0;
DECLARE @SchemaName SYSNAME, @TableName SYSNAME, @Sql NVARCHAR(MAX);

DECLARE TableCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    c.TABLE_SCHEMA,
    c.TABLE_NAME
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.COLUMN_NAME = N'CreatedAt'
  AND EXISTS (
      SELECT 1 FROM INFORMATION_SCHEMA.TABLES t
      WHERE t.TABLE_SCHEMA = c.TABLE_SCHEMA
        AND t.TABLE_NAME = c.TABLE_NAME
        AND t.TABLE_TYPE = N'BASE TABLE'
  )
  -- La tabla NO tiene aun IsDeleted -> es candidata
  AND NOT EXISTS (
      SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS x
      WHERE x.TABLE_SCHEMA = c.TABLE_SCHEMA
        AND x.TABLE_NAME = c.TABLE_NAME
        AND x.COLUMN_NAME = N'IsDeleted'
  )
ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME;

OPEN TableCursor;
FETCH NEXT FROM TableCursor INTO @SchemaName, @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'';

    -- IsDeleted: BIT NOT NULL DEFAULT 0
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = N'IsDeleted'
    )
    BEGIN
        SET @Sql = @Sql
            + N'ALTER TABLE [' + @SchemaName + N'].[' + @TableName + N'] '
            + N'ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_' + @TableName + N'_IsDeleted] DEFAULT 0;' + CHAR(13);
    END;

    -- DeletedAt: DATETIME2 NULL
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = N'DeletedAt'
    )
    BEGIN
        SET @Sql = @Sql
            + N'ALTER TABLE [' + @SchemaName + N'].[' + @TableName + N'] '
            + N'ADD [DeletedAt] DATETIME2 NULL;' + CHAR(13);
    END;

    -- DeletedBy: NVARCHAR(100) NULL
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName AND COLUMN_NAME = N'DeletedBy'
    )
    BEGIN
        SET @Sql = @Sql
            + N'ALTER TABLE [' + @SchemaName + N'].[' + @TableName + N'] '
            + N'ADD [DeletedBy] NVARCHAR(100) NULL;' + CHAR(13);
    END;

    IF LEN(@Sql) > 0
    BEGIN
        PRINT N'  -> Modificando [' + @SchemaName + N'].[' + @TableName + N']';
        EXEC sp_executesql @Sql;
        SET @TablesModified = @TablesModified + 1;
    END;

    FETCH NEXT FROM TableCursor INTO @SchemaName, @TableName;
END;

CLOSE TableCursor;
DEALLOCATE TableCursor;

PRINT N'';
PRINT N'================================================================';
PRINT N'  Tablas modificadas: ' + CAST(@TablesModified AS NVARCHAR(10));
PRINT N'================================================================';
GO

-- ============================================================================
-- Indices recomendados para las queries que filtran por IsDeleted
-- (creacion idempotente: solo si no existen)
-- ============================================================================

PRINT N'';
PRINT N'Creando indices filtrados sobre IsDeleted...';

DECLARE @SchemaName SYSNAME, @TableName SYSNAME, @IndexName SYSNAME, @Sql NVARCHAR(MAX);
DECLARE @IndexesCreated INT = 0;

DECLARE IndexCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT t.TABLE_SCHEMA, t.TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES t
INNER JOIN INFORMATION_SCHEMA.COLUMNS c
    ON c.TABLE_SCHEMA = t.TABLE_SCHEMA
   AND c.TABLE_NAME = t.TABLE_NAME
   AND c.COLUMN_NAME = N'IsDeleted'
WHERE t.TABLE_TYPE = N'BASE TABLE';

OPEN IndexCursor;
FETCH NEXT FROM IndexCursor INTO @SchemaName, @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @IndexName = N'IX_' + @TableName + N'_IsDeleted';

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        INNER JOIN sys.objects o ON o.object_id = i.object_id
        WHERE o.name = @TableName
          AND SCHEMA_NAME(o.schema_id) = @SchemaName
          AND i.name = @IndexName
    )
    BEGIN
        SET @Sql = N'CREATE NONCLUSTERED INDEX [' + @IndexName + N'] '
                 + N'ON [' + @SchemaName + N'].[' + @TableName + N'] ([IsDeleted]) '
                 + N'WHERE [IsDeleted] = 0;';
        EXEC sp_executesql @Sql;
        SET @IndexesCreated = @IndexesCreated + 1;
    END;

    FETCH NEXT FROM IndexCursor INTO @SchemaName, @TableName;
END;

CLOSE IndexCursor;
DEALLOCATE IndexCursor;

PRINT N'  Indices filtrados creados: ' + CAST(@IndexesCreated AS NVARCHAR(10));
PRINT N'';
PRINT N'================================================================';
PRINT N'  Migracion 11 completada exitosamente';
PRINT N'================================================================';
GO
