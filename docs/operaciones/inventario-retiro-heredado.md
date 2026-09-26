# Inventario (feature 012): retirar las tablas del inventario heredado

> Qué hace la migración destructiva `RetiroDelInventarioHeredado`, qué hay que tener antes de aplicarla
> en una base, cómo comprobarlo y cómo se vuelve atrás. Es el primer paso de la promoción de la entrega
> I1 y la única migración destructiva de la feature (FR-092, Principio XII). Equivale, para el inventario,
> a la §2 de [contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md) (`ContabilidadNiif`).

## 1. Qué se retira y qué se conserva

El traslado a medias de SOLIDO había dejado **23 tablas `INV_*`** que ninguna pantalla nueva usa:

`INV_CommissionParameters`, `INV_CommissionPriceParams`, `INV_DiscountTypes`, `INV_Discounts`,
`INV_Documents`, `INV_Invoices`, `INV_Transactions`, `INV_TransactionTypes`, `INV_Locations`,
`INV_OrderDocuments`, `INV_OrderTransactions`, `INV_PhysicalInventory`, `INV_Prices`,
`INV_PriceListTypes`, `INV_PrimaryGroups`, `INV_ProductAccounts`, `INV_Products`, `INV_ProductGroups`,
`INV_SalesPoints`, `INV_SecondaryGroups`, `INV_Shifts`, `INV_VatAccounts`, `INV_Warehouses`.

**`INV_Salespeople` (vendedores) se conserva, con sus filas** (Principio V: es el rol de una persona).
La operación de vendedores se reemplazó por la de FR-031 (`RolDeVendedor`, `/ventas/vendedores`); la
migración `InventarioComercialNucleo` le agrega después el índice único `UK_INV_Salespeople_PersonId`
**filtrado a las vivas**, así que dos vendedores vivos de la misma persona harían fallar esa otra
migración (el diagnóstico lo cuenta).

Tres de los nombres vuelven con otro esquema: `INV_Documents`, `INV_Products` e `INV_Warehouses` los
recrea `InventarioComercialNucleo` para el módulo nuevo. Por eso el retiro se generó sobre un modelo
intermedio sin ninguna de las 23 entidades y **va antes** de `PlataformaParaInventario` y de
`InventarioComercialNucleo`, en commit propio.

## 2. Qué hace la migración, en orden

Par PostgreSQL / SQL Server en `…Persistence.Migrations.{PostgreSql,SqlServer}/Application/20260925164923_RetiroDelInventarioHeredado.cs`,
con la cabecera `MIGRACION-DESTRUCTIVA-APROBADA` que exige `PrincipioXII_MigracionesDestructivas`:

1. **Guarda.** Cuenta las filas de cada una de las 23 tablas (una tabla que no existe en esa base se
   salta). Si alguna tiene filas **y** la base no trae la fila viva
   `COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'`, se niega nombrando cada tabla con su
   cantidad —PostgreSQL con `RAISE EXCEPTION`, SQL Server con `THROW 50012`— y **no toca nada**:

   ```
   RetiroDelInventarioHeredado: las tablas del inventario heredado tienen filas: INV_ProductGroups (1 filas).
   La migracion las borra y solo se aplica sobre tablas vacias o con la aprobacion del dueno para esta
   cooperativa (fila COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'), previo pg_dump y
   segundo revisor (feature 012, FR-092, Principio XII).
   ```

   Con las 23 vacías, la guarda pasa sola: no hace falta la fila de aprobación.
2. Suelta las claves foráneas de las 23 tablas (entre ellas y hacia `ACC_Documents`), para que el orden
   de los borrados no importe.
3. Borra las 23 tablas.

`Down()` las **recrea vacías** con la definición que conocía el snapshot. En datos es irreversible: lo que
había se recupera del respaldo (§6), no del `Down()`.

Lo prueba `RetiroDelInventarioHeredadoTests` (colección «Inventario e2e», los dos motores): baja una
cooperativa aislada a la migración anterior, inserta una fila en `INV_ProductGroups`, comprueba que la
guarda la nombra, siembra la aprobación, comprueba que pasa y que el vendedor sigue.

## 3. Antes de aplicar en un ambiente compartido

Por **cada base** que la migración va a tocar —cada base de cooperativa y la base por defecto
`ingenia365erp`; la administrativa no tiene estas tablas—:

1. **Diagnóstico.** Correr [specs/012-inventario-comercial/diagnostico-inventario-heredado.sql](../../specs/012-inventario-comercial/diagnostico-inventario-heredado.sql),
   que es de sólo lectura. Da las filas de cada una de las 23 tablas (−1 si no existe), las de
   `INV_Salespeople` (vivas, de baja y personas repetidas entre las vivas) y si ya existe la aprobación.
   Las bases salen de la administrativa:

   ```sql
   -- en ingenia365erp_admin
   SELECT "Name", "DatabaseName" FROM dbo."ADM_Tenants" WHERE NOT "IsDeleted";
   ```

   ```bash
   k3s kubectl -n <ns> exec -i erp-db-1 -c postgres -- psql -U postgres -d <base> -f - < diagnostico-inventario-heredado.sql
   ```

   (En SQL Server, el bloque `SQL SERVER` del mismo archivo, en la base de la cooperativa.)
2. **Respaldo.** `pg_dump -Fc` de la base, **inmediatamente antes** de aplicar, con el nombre de la
   convención de las promociones anteriores, en `/root/respaldos/` del nodo:

   ```bash
   k3s kubectl -n <ns> exec erp-db-1 -c postgres -- pg_dump -Fc -U postgres -d <base> > /root/respaldos/<base>-AAAAMMDD-pre-f012.dump
   ```

   Se respalda también la administrativa (`ingenia365erp_admin`) aunque esta migración no la toque: la
   promoción de I1 trae `PlataformaParaInventario` e `InventarioComercialNucleo` detrás. Se anota el
   tamaño y que el archivo empieza con `PGDMP`. En DEV el respaldo es el propio contenedor.
3. **Si una base tiene filas en las heredadas**, no se aplica sin la **aprobación explícita del dueño
   para esa cooperativa** y el **nombre del segundo revisor**. Con las dos cosas, y el respaldo hecho, se
   siembra la fila que abre la guarda **sólo en esa base**:

   ```sql
   INSERT INTO dbo."COR_SystemSettings" ("SettingKey", "SettingValue", "ValueType", "ModulePrefix", "Description", "PublicId", "CreatedAt", "CreatedBy", "IsDeleted")
   VALUES ('INV.RetiroHeredado.Aprobado',
           '<fecha> <quien aprueba>; respaldo <base>-AAAAMMDD-pre-f012.dump; segundo revisor <nombre>',
           'String', 'INV',
           'Aprobación del retiro del inventario heredado (feature 012, FR-092)',
           gen_random_uuid(), now() at time zone 'utc', '<quien la siembra>', false);
   ```

   (Comprobar las columnas de `COR_SystemSettings` en la base antes de insertar; la guarda sólo mira
   `SettingKey` e `IsDeleted`.) El valor lleva la referencia del respaldo y el revisor: queda como
   constancia en la propia base.
4. **Vendedores repetidos.** Si el diagnóstico muestra una persona con dos vendedores vivos, se da de baja
   uno antes de promover: no lo detiene el retiro, lo detiene `InventarioComercialNucleo`.
5. **Segundo revisor** de las tres migraciones de I1 anotado en la cabecera del retiro (hoy dice
   «pendiente, lo nombra el dueño antes de promover»). Es la tarea T985 de la feature.

**Estado al 2026-09-25** (diagnóstico corrido con autorización del dueño, T030): DEV `ingenia365erp`, QA
`ingenia365erp` y `coop_prueba`, producción `cooflopal` e `ingenia365erp`: **0 filas** en las 23 tablas,
0 vendedores y sin aprobación en todas. La guarda pasa sola y no hace falta sembrar la fila. El respaldo y
el segundo revisor siguen siendo parte de la promoción, y el diagnóstico **se repite el mismo día** de
promover: una cooperativa aprovisionada después no está en esa lista.

## 4. Cómo se aplica

Como toda migración en los ambientes del clúster: por el **Job PreSync del DbMigrator** en la
sincronización de Argo (`AutoMigrate` está apagado en QA y producción), base por base, en el orden
`RetiroDelInventarioHeredado` → `PlataformaParaInventario` → `InventarioComercialNucleo`. Si la guarda se
niega en una base, el Job falla en ella y la sincronización no avanza: se diagnostica esa base, se decide
con el dueño y se vuelve a sincronizar. En DEV local, `AutoMigrate` la aplica al arrancar.

## 5. Cómo comprobarlo contra la base

```sql
-- Las 23 ya no están (salvo las tres que vuelven con el esquema nuevo tras InventarioComercialNucleo).
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'dbo' AND table_name IN ('INV_CommissionParameters','INV_Invoices','INV_Transactions',
  'INV_PrimaryGroups','INV_ProductAccounts','INV_Shifts','INV_VatAccounts','INV_SalesPoints');   -- 0 filas

-- Los vendedores siguen, con el mismo conteo que dio el diagnóstico.
SELECT COUNT(*) FILTER (WHERE NOT "IsDeleted") AS vivos, COUNT(*) AS todos FROM dbo."INV_Salespeople";

-- La migración quedó registrada.
SELECT "MigrationId" FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" LIKE '%RetiroDelInventarioHeredado';
```

## 6. Volver atrás

- **Antes de que la cooperativa opere con el inventario nuevo**, lo correcto es **restaurar el respaldo**
  de esa base (`pg_restore` del `*-pre-f012.dump` sobre una base recreada) y volver la imagen. El nodo no
  tiene `pg_restore` y el contenedor no acepta el archivo por `stdin`: se copia el archivo al pod
  (`kubectl cp`) y se restaura desde allí.
- **El `Down()` no devuelve datos**: recrea las 23 tablas vacías. Además, con `InventarioComercialNucleo`
  aplicada, no se puede bajar el retiro sin bajar antes esa migración y `PlataformaParaInventario`
  (`INV_Documents`, `INV_Products` e `INV_Warehouses` existirían dos veces), y bajar
  `InventarioComercialNucleo` borra todo lo que el módulo nuevo haya escrito. Sirve en una base de
  ensayo; en un ambiente compartido, es restore.
- **Después de que la cooperativa operó**, volver atrás es perder lo que operó: no se hace sin decisión
  del dueño.

## 7. Síntomas

| Síntoma | Causa | Qué hacer |
|---|---|---|
| El Job PreSync falla con `RetiroDelInventarioHeredado: las tablas del inventario heredado tienen filas: …` | esa base tiene datos heredados y no tiene la aprobación | diagnóstico, decisión del dueño, respaldo, revisor y la fila de §3.3; volver a sincronizar |
| SQL Server: error `50012` con el mismo texto | ídem, en el motor SQL Server | ídem |
| Falla `InventarioComercialNucleo` al crear `UK_INV_Salespeople_PersonId` | una persona con dos vendedores vivos | dar de baja el sobrante (queda la fila, Principio VII) y volver a sincronizar |
| Una cooperativa nueva no está en el diagnóstico | se aprovisionó después del 2026-09-25 | repetir el diagnóstico el día de la promoción |
