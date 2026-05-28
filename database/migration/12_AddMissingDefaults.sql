-- ============================================================================
-- IngenIA365ERP — Migracion 12: DEFAULTs en columnas NOT NULL sin valor por defecto
-- ============================================================================
--
-- Contexto:
--   El DDL inicial declara muchisimas columnas heredadas del legacy SOLIDO
--   como NOT NULL pero sin DEFAULT. Ejemplos en PAY_Employees:
--     MilitaryBooklet NVARCHAR(14) NOT NULL
--     BloodType NVARCHAR(2) NOT NULL
--     RhFactor NVARCHAR(2) NOT NULL
--     ShirtSize NVARCHAR(4) NOT NULL
--     DriverLicense NVARCHAR(14) NOT NULL
--     ...y muchas mas
--
--   La entidad C# (Employee) NO mapea esas columnas — son de bagaje legacy.
--   Cuando EF Core hace INSERT no las incluye, SQL Server intenta usar el
--   DEFAULT (que no existe), y lanza:
--     SqlException: Cannot insert the value NULL into column 'MilitaryBooklet'
--
-- Estrategia:
--   Recorrer dinamicamente todas las columnas NOT NULL en tablas dbo.* que
--   NO tengan DEFAULT, NO sean IDENTITY, NO sean computadas, y agregar un
--   DEFAULT apropiado segun el tipo:
--      - NVARCHAR/VARCHAR/CHAR/NCHAR/TEXT  → N''
--      - INT/BIGINT/SMALLINT/TINYINT/MONEY → 0
--      - DECIMAL/NUMERIC/FLOAT/REAL        → 0
--      - DATETIME/DATETIME2/DATE/SMALLDT   → '1900-01-01' (sentinel del legacy)
--      - TIME                              → '00:00:00'
--      - BIT                               → 0
--      - UNIQUEIDENTIFIER                  → NEWID()
--
--   Es idempotente: solo toca columnas que aun no tienen DEFAULT.
--
-- Como ejecutar:
--   sqlcmd -S <server> -d <database> -i 12_AddMissingDefaults.sql
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'================================================================';
PRINT N'Migracion 12: Agregar DEFAULTs faltantes a columnas NOT NULL';
PRINT N'================================================================';
PRINT N'';

DECLARE @TablesAffected INT = 0;
DECLARE @ColumnsAffected INT = 0;

DECLARE @SchemaName SYSNAME, @TableName SYSNAME, @ColumnName SYSNAME, @DataType SYSNAME;
DECLARE @DefaultValue NVARCHAR(100);
DECLARE @ConstraintName SYSNAME;
DECLARE @Sql NVARCHAR(MAX);

DECLARE ColCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    LOWER(typ.name) AS DataType
FROM sys.columns c
INNER JOIN sys.tables t ON t.object_id = c.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.types typ ON typ.user_type_id = c.user_type_id
WHERE c.is_nullable = 0           -- NOT NULL
  AND c.default_object_id = 0     -- sin DEFAULT
  AND c.is_identity = 0           -- no es IDENTITY
  AND c.is_computed = 0           -- no es columna calculada
  AND c.is_rowguidcol = 0         -- no es ROWGUIDCOL
  AND s.name = N'dbo'
  AND t.is_ms_shipped = 0         -- no son tablas de sistema
  AND typ.name NOT IN (N'timestamp', N'rowversion', N'image', N'binary', N'varbinary', N'xml', N'sql_variant', N'hierarchyid', N'geography', N'geometry')
ORDER BY t.name, c.column_id;

OPEN ColCursor;
FETCH NEXT FROM ColCursor INTO @SchemaName, @TableName, @ColumnName, @DataType;

DECLARE @LastTable SYSNAME = N'';

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @DefaultValue = NULL;

    -- Mapear tipo -> default value literal
    IF @DataType IN (N'nvarchar', N'varchar', N'nchar', N'char', N'text', N'ntext')
        SET @DefaultValue = N'N''''';                 -- empty string

    ELSE IF @DataType IN (N'int', N'bigint', N'smallint', N'tinyint', N'money', N'smallmoney')
        SET @DefaultValue = N'0';

    ELSE IF @DataType IN (N'decimal', N'numeric', N'float', N'real')
        SET @DefaultValue = N'0';

    ELSE IF @DataType IN (N'datetime', N'datetime2', N'date', N'smalldatetime', N'datetimeoffset')
        SET @DefaultValue = N'''1900-01-01''';        -- sentinel SOLIDO legacy

    ELSE IF @DataType = N'time'
        SET @DefaultValue = N'''00:00:00''';

    ELSE IF @DataType = N'bit'
        SET @DefaultValue = N'0';

    ELSE IF @DataType = N'uniqueidentifier'
        SET @DefaultValue = N'NEWID()';

    IF @DefaultValue IS NOT NULL
    BEGIN
        SET @ConstraintName = N'DF_' + @TableName + N'_' + @ColumnName;

        SET @Sql = N'ALTER TABLE [' + @SchemaName + N'].[' + @TableName + N'] '
                 + N'ADD CONSTRAINT [' + @ConstraintName + N'] '
                 + N'DEFAULT ' + @DefaultValue + N' FOR [' + @ColumnName + N'];';

        BEGIN TRY
            EXEC sp_executesql @Sql;

            IF @LastTable <> @TableName
            BEGIN
                PRINT N'  -> [' + @TableName + N']';
                SET @LastTable = @TableName;
                SET @TablesAffected = @TablesAffected + 1;
            END;

            SET @ColumnsAffected = @ColumnsAffected + 1;
        END TRY
        BEGIN CATCH
            -- Si ya existe el constraint (carrera o re-ejecucion), seguimos.
            DECLARE @ErrNum INT = ERROR_NUMBER();
            IF @ErrNum NOT IN (2714, 1781)  -- 2714: ya existe el nombre. 1781: ya existe DEFAULT en columna.
                PRINT N'  ! Error en [' + @TableName + N'].[' + @ColumnName + N']: ' + ERROR_MESSAGE();
        END CATCH;
    END;

    FETCH NEXT FROM ColCursor INTO @SchemaName, @TableName, @ColumnName, @DataType;
END;

CLOSE ColCursor;
DEALLOCATE ColCursor;

PRINT N'';
PRINT N'================================================================';
PRINT N'  Tablas tocadas:    ' + CAST(@TablesAffected AS NVARCHAR(10));
PRINT N'  Columnas con DEFAULT agregado: ' + CAST(@ColumnsAffected AS NVARCHAR(10));
PRINT N'================================================================';
PRINT N'';
PRINT N'Migracion 12 completada.';
GO
