-- =====================================================================================
-- Diagnóstico del inventario heredado antes de RetiroDelInventarioHeredado (feature 012, T029)
-- SÓLO LECTURA. Se corre en cada base de cooperativa (una por cooperativa, Principio IV) y en la
-- base por defecto de la aplicación (ingenia365erp). Las bases de cooperativa salen de:
--   ingenia365erp_admin:  SELECT "Name", "DatabaseName" FROM dbo."ADM_Tenants" WHERE NOT "IsDeleted";
--
-- Qué contestar por base (quickstart §2.1, FR-092, Principio XII):
--   1. ¿Alguna de las 23 tablas heredadas tiene filas? Si sí, hace falta la aprobación explícita del
--      dueño para esa base (fila COR_SystemSettings 'INV.RetiroHeredado.Aprobado'), su pg_dump y el
--      nombre del segundo revisor; sin eso la guarda de la migración se niega.
--   2. INV_Salespeople se conserva: cuántas vivas, cuántas de baja, y personas repetidas entre las
--      vivas (chocarían con UK_INV_Salespeople_PersonId filtrado que trae InventarioComercialNucleo).
--   3. Si la aprobación ya existe.
--
-- PostgreSQL (clúster):
--   k3s kubectl -n <ns> exec erp-db-1 -c postgres -- psql -U postgres -d <base> -f - < diagnostico-inventario-heredado.sql
--   (o pegar el bloque POSTGRESQL en psql). Una tabla que no existe en esa base se informa como -1.
-- SQL Server: correr el bloque SQL SERVER en la base de la cooperativa.
-- =====================================================================================

-- ======================== POSTGRESQL ========================
DO $$
DECLARE
    t text;
    n bigint;
    tablas text[] := ARRAY[
        'INV_CommissionParameters','INV_CommissionPriceParams','INV_DiscountTypes','INV_Discounts',
        'INV_Documents','INV_Invoices','INV_Transactions','INV_TransactionTypes','INV_Locations',
        'INV_OrderDocuments','INV_OrderTransactions','INV_PhysicalInventory','INV_Prices',
        'INV_PriceListTypes','INV_PrimaryGroups','INV_ProductAccounts','INV_Products','INV_ProductGroups',
        'INV_SalesPoints','INV_SecondaryGroups','INV_Shifts','INV_VatAccounts','INV_Warehouses'];
    total bigint := 0;
BEGIN
    FOREACH t IN ARRAY tablas LOOP
        IF to_regclass(format('dbo.%I', t)) IS NULL THEN
            RAISE NOTICE '% | -1 (no existe)', t;
        ELSE
            EXECUTE format('SELECT count(*) FROM dbo.%I', t) INTO n;
            total := total + n;
            RAISE NOTICE '% | %', t, n;
        END IF;
    END LOOP;
    RAISE NOTICE 'TOTAL filas heredadas | %', total;

    IF to_regclass('dbo."INV_Salespeople"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."INV_Salespeople" WHERE NOT "IsDeleted"' INTO n;
        RAISE NOTICE 'INV_Salespeople vivas | %', n;
        EXECUTE 'SELECT count(*) FROM dbo."INV_Salespeople" WHERE "IsDeleted"' INTO n;
        RAISE NOTICE 'INV_Salespeople de baja | %', n;
        EXECUTE 'SELECT count(*) FROM (SELECT "PersonId" FROM dbo."INV_Salespeople" WHERE NOT "IsDeleted" GROUP BY "PersonId" HAVING count(*) > 1) x' INTO n;
        RAISE NOTICE 'INV_Salespeople personas repetidas entre vivas | %', n;
    END IF;

    IF to_regclass('dbo."COR_SystemSettings"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."COR_SystemSettings" WHERE "SettingKey" = ''INV.RetiroHeredado.Aprobado''' INTO n;
        RAISE NOTICE 'Aprobacion INV.RetiroHeredado.Aprobado | %', n;
    END IF;
END $$;

-- ======================== SQL SERVER ========================
-- (quitar el comentario del bloque para usarlo)
/*
SET NOCOUNT ON;
DECLARE @t sysname, @n bigint, @total bigint = 0, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT v FROM (VALUES
 (N'INV_CommissionParameters'),(N'INV_CommissionPriceParams'),(N'INV_DiscountTypes'),(N'INV_Discounts'),
 (N'INV_Documents'),(N'INV_Invoices'),(N'INV_Transactions'),(N'INV_TransactionTypes'),(N'INV_Locations'),
 (N'INV_OrderDocuments'),(N'INV_OrderTransactions'),(N'INV_PhysicalInventory'),(N'INV_Prices'),
 (N'INV_PriceListTypes'),(N'INV_PrimaryGroups'),(N'INV_ProductAccounts'),(N'INV_Products'),(N'INV_ProductGroups'),
 (N'INV_SalesPoints'),(N'INV_SecondaryGroups'),(N'INV_Shifts'),(N'INV_VatAccounts'),(N'INV_Warehouses')) x(v);
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@t)) IS NULL
        PRINT @t + N' | -1 (no existe)';
    ELSE
    BEGIN
        SET @sql = N'SELECT @n = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@t);
        EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @n OUTPUT;
        SET @total += @n;
        PRINT @t + N' | ' + CAST(@n AS nvarchar(20));
    END
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;
PRINT N'TOTAL filas heredadas | ' + CAST(@total AS nvarchar(20));
IF OBJECT_ID(N'dbo.INV_Salespeople') IS NOT NULL
BEGIN
    SELECT
        (SELECT COUNT(*) FROM dbo.INV_Salespeople WHERE IsDeleted = 0) AS Vivas,
        (SELECT COUNT(*) FROM dbo.INV_Salespeople WHERE IsDeleted = 1) AS DeBaja,
        (SELECT COUNT(*) FROM (SELECT PersonId FROM dbo.INV_Salespeople WHERE IsDeleted = 0 GROUP BY PersonId HAVING COUNT(*) > 1) x) AS PersonasRepetidas;
END
IF OBJECT_ID(N'dbo.COR_SystemSettings') IS NOT NULL
    SELECT COUNT(*) AS Aprobacion FROM dbo.COR_SystemSettings WHERE SettingKey = N'INV.RetiroHeredado.Aprobado';
*/
