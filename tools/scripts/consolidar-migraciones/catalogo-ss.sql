SET NOCOUNT ON;
-- Esquema de una base SQL Server en líneas normalizadas, comparables entre dos bases sin depender del orden físico
-- de las columnas ni de los nombres que el motor inventa (las restricciones DEFAULT sin nombre).
WITH lineas AS (
  SELECT 'TABLA|' + s.name + '.' + t.name AS l
  FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0
  UNION ALL
  SELECT 'COL|' + s.name + '.' + t.name + '|' + c.name + '|' + ty.name
         + '(' + CAST(c.max_length AS varchar(10)) + ',' + CAST(c.precision AS varchar(10)) + ',' + CAST(c.scale AS varchar(10)) + ')'
         + '|null=' + CAST(c.is_nullable AS varchar(1))
         + '|identity=' + COALESCE(CAST(ic.seed_value AS varchar(20)) + ',' + CAST(ic.increment_value AS varchar(20)), '')
         + '|computed=' + COALESCE(cc.definition, '') + COALESCE('|persisted=' + CAST(cc.is_persisted AS varchar(1)), '')
         + '|collation=' + COALESCE(c.collation_name, '')
         + '|default=' + COALESCE(dc.definition, '')
         + '|rowguid=' + CAST(c.is_rowguidcol AS varchar(1))
  FROM sys.columns c
  JOIN sys.tables t ON t.object_id = c.object_id AND t.is_ms_shipped = 0
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.types ty ON ty.user_type_id = c.user_type_id
  LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
  LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
  LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
  UNION ALL
  SELECT 'IDX|' + s.name + '.' + t.name + '|' + i.name + '|' + i.type_desc COLLATE DATABASE_DEFAULT + '|unique=' + CAST(i.is_unique AS varchar(1))
         + '|pk=' + CAST(i.is_primary_key AS varchar(1)) + '|uq=' + CAST(i.is_unique_constraint AS varchar(1))
         + '|filter=' + COALESCE(i.filter_definition, '')
         + '|keys=' + COALESCE((SELECT STRING_AGG(c.name + CASE WHEN x.is_descending_key = 1 THEN ' DESC' ELSE '' END, ',')
                                  WITHIN GROUP (ORDER BY x.key_ordinal)
                                FROM sys.index_columns x JOIN sys.columns c ON c.object_id = x.object_id AND c.column_id = x.column_id
                                WHERE x.object_id = i.object_id AND x.index_id = i.index_id AND x.is_included_column = 0), '')
         + '|include=' + COALESCE((SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY c.name)
                                FROM sys.index_columns x JOIN sys.columns c ON c.object_id = x.object_id AND c.column_id = x.column_id
                                WHERE x.object_id = i.object_id AND x.index_id = i.index_id AND x.is_included_column = 1), '')
  FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  WHERE i.type > 0
  UNION ALL
  SELECT 'FK|' + s.name + '.' + t.name + '|' + fk.name + '|' + rs.name + '.' + rt.name
         + '|cols=' + (SELECT STRING_AGG(pc.name + '>' + rc.name, ',') WITHIN GROUP (ORDER BY f.constraint_column_id)
                       FROM sys.foreign_key_columns f
                       JOIN sys.columns pc ON pc.object_id = f.parent_object_id AND pc.column_id = f.parent_column_id
                       JOIN sys.columns rc ON rc.object_id = f.referenced_object_id AND rc.column_id = f.referenced_column_id
                       WHERE f.constraint_object_id = fk.object_id)
         + '|delete=' + fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT + '|update=' + fk.update_referential_action_desc COLLATE DATABASE_DEFAULT
         + '|disabled=' + CAST(fk.is_disabled AS varchar(1)) + '|trusted=' + CAST(1 - fk.is_not_trusted AS varchar(1))
  FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
  UNION ALL
  SELECT 'CHECK|' + s.name + '.' + t.name + '|' + ck.name + '|' + ck.definition
  FROM sys.check_constraints ck JOIN sys.tables t ON t.object_id = ck.parent_object_id
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  UNION ALL
  SELECT 'OBJ|' + s.name + '.' + o.name + '|' + o.type_desc COLLATE DATABASE_DEFAULT + '|' + CONVERT(varchar(64), HASHBYTES('SHA2_256', COALESCE(m.definition, '')), 2)
  FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
  LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
  WHERE o.is_ms_shipped = 0 AND o.type IN ('V', 'P', 'FN', 'IF', 'TF', 'TR', 'SO', 'SN', 'TT')
  UNION ALL
  SELECT 'SCHEMA|' + name FROM sys.schemas WHERE schema_id BETWEEN 5 AND 16383
)
SELECT l FROM lineas ORDER BY l COLLATE Latin1_General_BIN2;
