# Contabilidad (feature 009): antes del primer comprobante de una cooperativa

> Qué deja la semilla, qué hay que hacer a mano, en qué orden, y cómo comprobarlo contra la
> base del ambiente. Es el equivalente contable de
> [nomina-primer-periodo.md](nomina-primer-periodo.md).

## 1. Qué deja la semilla (en toda base de cooperativa)

| Tabla | Filas | Origen |
|---|---|---|
| `ACC_AccountCatalogs` | 2: `PUC-SOLIDARIO` (Res. 2015110009615, 695 entradas) y `PUC-COMERCIAL` (Decreto 2650, 1.869 entradas) | `puc-solidario.json`, `puc-comercial.json` |
| `ACC_AccountCatalogEntries` | 2.564 | ídem |
| `ACC_FinancialStatementItems` | 205 (69 rubros × grupos NIIF 1/2/3) | `rubros-niif.json` |
| `ACC_VoucherTypes` | 18 (`CG` manual, `NM` nómina, `DS`/`RC` cartera, `AP` apertura, `CI` cierre…) | `voucher-types.json` |
| `ACC_CrossDocumentTypes` | 8 (`FV`, `FC`, `RC`, `PG`…) | `cross-document-types.json` |
| `SEC_Permissions` | 30 códigos `Accounting.*` | `AccountingPermissionCatalogSeeder` |

Los roles built-in reciben: CompanyAdmin todo; Operator `Vouchers.Create` y `Reports.Export`
(digita borradores, no contabiliza); Auditor `Reports.Export`; todos `*.View`. Todo rol
personalizado recibe `Accounting.Accounts.View` y `Accounting.VoucherTypes.View`
(`LecturaDeMaestros`).

Comprobación:

```sql
SELECT "Code", "Name", (SELECT COUNT(*) FROM "ACC_AccountCatalogEntries" e WHERE e."CatalogId" = c."Id") AS entradas
FROM "ACC_AccountCatalogs" c;
SELECT COUNT(*) FROM "ACC_FinancialStatementItems";
SELECT "Code", "Usage", "ModuleCode", "NextNumber" FROM "ACC_VoucherTypes" ORDER BY "Usage", "Code";
SELECT COUNT(*) FROM "SEC_Permissions" WHERE "Resource" LIKE 'Accounting.%';
```

## 2. Lo que la migración `ContabilidadNiif` hace (y exige)

Es **destructiva** (Principio XII): retira las 33 tablas contables heredadas y vacía
`PAY_ConceptDefinitionAccounts`. Lleva una **guarda**: si `ACC_JournalEntries` o
`ACC_Documents` tienen filas, la migración falla y no toca nada. En DEV y QA (`ingenia365erp`, `coop_prueba`) las dos tablas tenían 0 filas el 2026-09-15
(diagnóstico corrido desde el nodo antes del merge); **producción se diagnostica antes de promover**, y
si no da cero, no se despliega. La cabecera
del archivo lleva el marcador `MIGRACION-DESTRUCTIVA-APROBADA` con la referencia del respaldo
y el segundo revisor (Jorman Copete, designado el 2026-09-17).

Consecuencia operativa: **las cuentas por concepto de nómina hay que volver a
parametrizarlas** (paso 5) después de iniciar la contabilidad.

## 3. Iniciar la contabilidad (una vez por cooperativa)

`Contabilidad › Configuración inicial` (`POST /api/accounting/setup/initialize`, permiso
`Accounting.Setup.Manage`):

1. Catálogo: `PUC-SOLIDARIO` para cooperativas vigiladas por Supersolidaria; `PUC-COMERCIAL`
   para las demás. **El contador lo valida antes** (`POST /catalogs/{code}/validate` deja
   `ValidatedAt/By`); la inicialización sin catálogo validado se admite en DEV/QA pero no
   debería ocurrir en producción.
2. Nivel de movimiento (5 o 6) y longitud de los códigos de nivel 5 y 6 (6 y 8 por defecto).
3. Grupo NIIF (1, 2 o 3), primer ejercicio, sucursal principal, cuatro ojos (sí/no),
   tolerancias.

Deja copiados los niveles 1–4 del catálogo en `ACC_ChartOfAccounts` (`Origin = Catalog`), el
ejercicio con sus doce períodos abiertos y la configuración. **Catálogo, nivel y longitudes
sólo cambian mientras no exista ninguna auxiliar ni ningún movimiento.**

## 4. Auxiliares mínimas

`Contabilidad › Plan de cuentas`: elegir la subcuenta de nivel 4 y «Nueva auxiliar bajo la
seleccionada». Cada auxiliar de nivel de movimiento lleva sus reglas: módulos a los que
aplica, exige tercero / documento cruce / centro / sucursal, bancaria (banco y número), de
impuesto (clase, concepto, base, tarifas con vigencia). **Con movimientos las reglas se
bloquean**; sólo cambian nombre y estado.

Mínimo para la primera nómina: una auxiliar de gasto por concepto de devengo, una de pasivo
por deducción y aporte, y las de aportes a EPS/pensión/ARL/caja con «exige tercero».

## 5. Vincular entidades y parametrizar nómina

1. `Nómina › EPS / ARL / Fondos de pensiones / Fondos de cesantías / Cajas de compensación`
   y `Maestros › Bancos`: «Persona que la representa como tercero» (FR-088). Sin el vínculo,
   las cuentas que exigen tercero hacen fallar la aprobación de nómina nombrando la entidad.
2. `Nómina › Conceptos › Cuentas`: cuenta débito y crédito por concepto (y por centro de
   costo si aplica). Sólo se admiten cuentas de movimiento, activas y habilitadas para
   Nómina; en aportes y provisiones, si la cuenta exige tercero, toda entidad del catálogo
   correspondiente tiene que tener persona vinculada.
3. `Contabilidad › Parametrizaciones inválidas` tiene que quedar **vacía**.

## 6. Primer `NM`

Calcular y aprobar la liquidación en `Nómina › Liquidación`. La aprobación guarda corrida y
comprobante `NM` en una transacción; el comprobante se ve en `Contabilidad › Comprobantes`
(origen «Nómina», sólo lectura, «Ver en Nómina»). Reversar es desde Nómina; desde
Contabilidad responde `Accounting.Document.ModuleOwned`.

Comprobación:

```sql
SELECT d."Number", d."Date", d."Status", d."OriginModule", d."TotalDebit", d."TotalCredit", COUNT(j."Id") AS lineas
FROM "ACC_Documents" d JOIN "ACC_VoucherTypes" v ON v."Id" = d."VoucherTypeId"
LEFT JOIN "ACC_JournalEntries" j ON j."DocumentId" = d."Id" AND j."IsDeleted" = false
WHERE v."Code" = 'NM' GROUP BY d."Id", d."Number", d."Date", d."Status", d."OriginModule", d."TotalDebit", d."TotalCredit";
```

## 7. Cierre mensual

`Contabilidad › Períodos`: cerrar exige que no queden borradores fechados en el mes (la
respuesta los lista); reabrir pide motivo, queda en la auditoría y deja «desactualizadas»
las conciliaciones cerradas del mes. El cierre del ejercicio (`CI`) y la apertura (`AP`)
llegan en E2.

## 8. Qué mirar si algo falla

| Síntoma | Causa | Dónde |
|---|---|---|
| `Accounting.NotInitialized` | La cooperativa no inició contabilidad | Paso 3 |
| `Payroll.ConceptWithoutAccounts` | Concepto sin cuentas (la migración vació la tabla) | Paso 5.2 |
| `Accounting.Line.ThirdPartyRequired` con `data.entity` | Entidad institucional sin persona vinculada | Paso 5.1 |
| `Accounting.Line.AccountNotEnabledForModule` | Auxiliar sin «aplica a Nómina» | Paso 4 |
| `Accounting.Period.Closed` | Mes cerrado; la nómina fecha al último día del período | Paso 7 |
| `Accounting.Document.FourEyes` | Quien registró el borrador intenta contabilizarlo | Configuración: cuatro ojos |
| Buscador de cuentas vacío en otro módulo | Rol sin `Accounting.Accounts.View` | `LecturaDeMaestros` en `BuiltInRolesSeeder` |
