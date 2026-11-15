SET NOCOUNT ON;
DECLARE @salida TABLE (l nvarchar(max));
DECLARE @t sysname, @cols nvarchar(max), @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
  SELECT t.name FROM sys.tables t WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND t.name <> '__EFMigrationsHistory' ORDER BY t.name;
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
  SELECT @cols = STRING_AGG(CAST(QUOTENAME(name) AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY name)
  FROM sys.columns WHERE object_id = OBJECT_ID('dbo.' + QUOTENAME(@t)) AND name NOT IN ('PublicId', 'CreatedAt', 'UpdatedAt', 'RowVersion');
  SET @sql = N'SELECT @x = (SELECT ' + @cols + N' FROM dbo.' + QUOTENAME(@t) + N' ORDER BY 1 FOR JSON PATH, INCLUDE_NULL_VALUES)';
  DECLARE @x nvarchar(max) = NULL;
  EXEC sp_executesql @sql, N'@x nvarchar(max) OUTPUT', @x = @x OUTPUT;
  IF @x IS NOT NULL INSERT @salida VALUES (@t + N'|' + @x);
  FETCH NEXT FROM c INTO @t;
END
SELECT l FROM @salida ORDER BY l;
