-- Filas de todas las tablas con datos (salvo el historial), como jsonb sin las columnas que cambian en cada corrida.
DO $$
DECLARE r record; n bigint;
BEGIN
  CREATE TEMP TABLE salida(l text);
  FOR r IN SELECT c.relname FROM pg_class c JOIN pg_namespace s ON s.oid = c.relnamespace
           WHERE s.nspname = 'dbo' AND c.relkind = 'r' AND c.relname <> '__EFMigrationsHistory' ORDER BY 1 LOOP
    EXECUTE format('INSERT INTO salida SELECT %L || ''|'' || (to_jsonb(t) - ''PublicId'' - ''CreatedAt'' - ''UpdatedAt'')::text FROM dbo.%I t', r.relname, r.relname);
  END LOOP;
END $$;
SELECT l FROM salida ORDER BY l;
