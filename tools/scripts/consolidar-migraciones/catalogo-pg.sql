-- Esquema de una base PostgreSQL en líneas normalizadas, comparables entre dos bases sin depender del orden
-- físico de las columnas ni de nombres generados. Una línea por objeto; se ordena al final.
WITH
tablas AS (
  SELECT c.oid, n.nspname AS esquema, c.relname AS tabla, c.relkind
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
  WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
    AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
    AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = c.oid AND d.deptype = 'e')
),
lineas AS (
  SELECT 'TABLA|' || esquema || '.' || tabla || '|' || relkind::text AS l FROM tablas
  UNION ALL
  SELECT 'COL|' || t.esquema || '.' || t.tabla || '|' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
         || '|notnull=' || a.attnotnull
         || '|default=' || coalesce(pg_get_expr(ad.adbin, ad.adrelid), '')
         || '|identity=' || a.attidentity::text || '|generated=' || a.attgenerated::text
         || '|collation=' || coalesce((SELECT collname FROM pg_collation WHERE oid = a.attcollation AND a.attcollation <> 100), '')
  FROM tablas t JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum > 0 AND NOT a.attisdropped
  LEFT JOIN pg_attrdef ad ON ad.adrelid = a.attrelid AND ad.adnum = a.attnum
  UNION ALL
  SELECT 'CON|' || t.esquema || '.' || t.tabla || '|' || co.conname || '|' || co.contype::text || '|' || pg_get_constraintdef(co.oid)
  FROM pg_constraint co JOIN tablas t ON t.oid = co.conrelid
  UNION ALL
  SELECT 'IDX|' || t.esquema || '.' || t.tabla || '|' || ic.relname || '|' || pg_get_indexdef(i.indexrelid)
  FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid JOIN tablas t ON t.oid = i.indrelid
  UNION ALL
  SELECT 'SEQ|' || n.nspname || '.' || c.relname || '|' || format_type(s.seqtypid, NULL) || '|start=' || s.seqstart
         || '|inc=' || s.seqincrement || '|min=' || s.seqmin || '|max=' || s.seqmax || '|cycle=' || s.seqcycle
  FROM pg_sequence s JOIN pg_class c ON c.oid = s.seqrelid JOIN pg_namespace n ON n.oid = c.relnamespace
  UNION ALL
  SELECT 'EXT|' || e.extname || '|schema=' || n.nspname FROM pg_extension e JOIN pg_namespace n ON n.oid = e.extnamespace
  UNION ALL
  SELECT 'SCHEMA|' || nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema'
  UNION ALL
  SELECT 'FUN|' || n.nspname || '.' || p.proname || '|' || md5(pg_get_functiondef(p.oid))
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
  WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
    AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = p.oid AND d.deptype = 'e')
  UNION ALL
  SELECT 'TRG|' || t.esquema || '.' || t.tabla || '|' || tg.tgname || '|' || pg_get_triggerdef(tg.oid)
  FROM pg_trigger tg JOIN tablas t ON t.oid = tg.tgrelid WHERE NOT tg.tgisinternal
  UNION ALL
  SELECT 'VIEW|' || t.esquema || '.' || t.tabla || '|' || md5(pg_get_viewdef(t.oid)) FROM tablas t WHERE t.relkind IN ('v', 'm')
)
SELECT l FROM lineas ORDER BY l;
