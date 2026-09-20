# Contabilidad (feature 009): antes del primer comprobante de una cooperativa

> Qué deja la semilla, qué hay que hacer a mano, en qué orden, y cómo comprobarlo contra la
> base del ambiente. Es el equivalente contable de
> [nomina-primer-periodo.md](nomina-primer-periodo.md).

## 1. Qué deja la semilla (en toda base de cooperativa)

| Tabla | Filas | Origen |
|---|---|---|
| `ACC_AccountCatalogs` | 2: `PUC-SOLIDARIO` (CUIF de la Supersolidaria, Res. 2015110009615 y formato SIAC 2023-11-03, 2.110 entradas) y `PUC-COMERCIAL` (Decreto 2650, 1.869 entradas) | `puc-solidario.json`, `puc-comercial.json` |
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
2. Nivel de movimiento (5 o 6). La longitud de los códigos no se configura: una auxiliar de nivel 5
   lleva entre 7 y 9 dígitos y una de nivel 6, hija de una de nivel 5, entre 10 y 12.
3. Grupo NIIF (1, 2 o 3), primer ejercicio, sucursal principal, cuatro ojos (sí/no),
   tolerancias.

Deja copiados los niveles 1–4 del catálogo en `ACC_ChartOfAccounts` (`Origin = Catalog`), el
ejercicio con sus doce períodos abiertos y la configuración. **Catálogo y nivel de movimiento
sólo cambian mientras no exista ninguna auxiliar ni ningún movimiento.**

## 4. Auxiliares mínimas

`Contabilidad › Plan de cuentas`: elegir la subcuenta de nivel 4 y «Nueva auxiliar bajo la
seleccionada». **Donde el CUIF no trae subcuentas** (reservas 32xx, fondos sociales 26xx y 33xx,
provisiones 28xx, excedentes 3505/3605/3905, y los grupos 25, 29, 53, 86, 88 y 98) el botón dice
«Nueva cuenta propia»: la empresa crea la subcuenta (o la cuenta) de 6 (o 4) dígitos y de ella
cuelga la auxiliar. Cada auxiliar de nivel de movimiento lleva sus reglas: módulos a los que
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
las conciliaciones cerradas del mes. El cierre del ejercicio (`CI`) y la apertura importada
(`AP`) **siguen pendientes** (US6 y US13; el cliente ya tiene los métodos, la API no).

## 7b. Consultas e informes (E2, 2026-09-20)

Todo saldo es una suma sobre `ACC_JournalEntries` con `IsPosted` (FR-046): no hay tabla de
saldos. Reglas que aplican a todas las vistas y a sus archivos:

- **Signo por naturaleza**: un saldo positivo va con la naturaleza de la cuenta (débito en
  activos, costos y gastos; crédito en pasivos, patrimonio e ingresos). El encabezado de cada
  informe lo repite.
- **Reversas**: el original y su espejo se muestran los dos (el espejo con la fecha de la
  reversa) y se netean; a una fecha intermedia el saldo muestra el original.
- **Cierre**: el comprobante `CI` del ejercicio consultado queda fuera salvo «incluir cierre»; los
  cierres de ejercicios anteriores siempre cuentan (si no, el balance del segundo año no cuadra).
- **Apertura**: las líneas del `AP` son siempre saldo inicial, caiga o no su fecha en el rango.
- **Alcance de sucursal**: quien tenga sucursales asignadas ve sólo esas, en todas las vistas.
- **Permisos**: `Accounting.Reports.View` para ver; `Accounting.Reports.Export` para Excel, PDF y
  Word (Operador y Auditor lo tienen; Sólo lectura no). Cada exportación queda en la auditoría
  como `Accounting.Report.Exported` con la vista, los filtros y el formato.
- **Encabezado** (FR-045): empresa, NIT, filtros aplicados, período, quién y cuándo; el archivo
  trae los mismos totales que la pantalla.

| Pantalla | Ruta | Qué trae |
|---|---|---|
| Libro auxiliar | `/contabilidad/libro-auxiliar` | Profundización por migas de pan: clases → grupos → cuentas → subcuentas → auxiliares → terceros → documentos cruce → comprobantes → líneas, con saldo inicial, débitos, créditos y saldo final en cada nivel; cada nivel se exporta; «Abrir comprobante» desde el último nivel. Acepta `?node=` y los filtros por query string. |
| Informes | `/contabilidad/informes?vista=` | Balance de prueba (`trial-balance`: nivel, con terceros, con cierre), libro diario (`journal`), libro mayor y balances (`general-ledger`), relación de comprobantes (`voucher-list`), documentos cruce pendientes (`pending-documents`), saldo diario promedio (`daily-average`). Un clic en una cuenta abre el libro auxiliar con los mismos filtros. |
| Estados financieros | `/contabilidad/estados-financieros?estado=` (esf, eri, ecp, efe) | Situación financiera a una fecha con comparativo al mismo día del año anterior (resultado del ejercicio inyectado en el patrimonio; cuentas de orden como memorando), resultado integral del rango con subtotales y comparativo, cambios en el patrimonio, flujo de efectivo indirecto (fila «Diferencia» que debe ser 0). Los rubros salen de `ACC_FinancialStatementItems` por el `NiifItemCode` de cada cuenta de movimiento, medida por el lado que el rubro espera (una pérdida en 3510 resta al patrimonio; un deterioro en 1408 resta al activo); un clic en un rubro del ESF o del ERI abre el libro auxiliar filtrado por ese rubro (`niifItem=`). ECP y EFE también traen comparativo. |
| Estado de cuenta del tercero | `/contabilidad/terceros?person=` | Vista consolidada de una persona: tarjetas (débitos, créditos, saldo, pendientes), saldos por cuenta (clic → libro auxiliar filtrado por el tercero), documentos cruce con saldo pendiente y movimientos con saldo corrido y enlace al comprobante. |

API: `GET /api/reports/accounting/{vista}?format=json|xlsx|pdf|docx&from=&to=&accountFrom=&accountTo=&accountPublicId=&niifItem=&person=&crossDocument=TIPO|NÚMERO&costCenter=&branch=&voucherType=&origin=&user=&level=&withThirdParties=&includeClosing=` (rango de hasta 5 años, 1 en `daily-average`)
(`ledger` además `node=`; `budget-execution` además `year=&month=`). Sin permiso responde 404.

El Centro de Reportes (`/reportes`) enlaza a estas pantallas con la vista preseleccionada; las
cinco tarjetas contables que hasta el 2026-09-20 llevaban a «Página no encontrada» ya no existen.

## 7c. Presupuesto y ejecución (E2, 2026-09-20)

`Contabilidad › Presupuesto` (`/contabilidad/presupuesto`). El ejercicio debe existir en
Períodos (`Accounting.Budget.FiscalYearNotFound` si no). Se presupuestan **cuentas de
movimiento** (una de agrupación responde `Accounting.Budget.AccountNotMovement`), opcionalmente
por sucursal y centro de costo (dentro del alcance de quien digita), doce meses por línea **en pesos** (sin decimales; tope 9.999.999.999.999). Acciones: copiar del año anterior con un
porcentaje (redondeo a pesos), distribuir un total por cuenta (igual: la diferencia de redondeo
cae en diciembre; porcentual: los porcentajes suman 100; manual: los doce valores suman el
total), guardar, aprobar. **Después de aprobado**, cada guardado exige motivo y crea otra
versión (`ACC_Budgets.Version`, la anterior queda `Superseded`); la pestaña Ejecución compara
contra la versión vigente y muestra también el presupuesto inicial acumulado. Ejecución:
presupuestado, ejecutado (movimiento neto por naturaleza), variación y % del mes y acumulado,
por cuenta y agregado hacia arriba por niveles; un clic abre el libro auxiliar del mes. Permisos
`Accounting.Budget.View` / `Accounting.Budget.Manage`. API en `/api/accounting/budgets`
(`GET ?year=&version=`, `POST /`, `PUT /{year}`, `POST /{year}/approve`,
`POST /{year}/copy-from/{previousYear}?adjustPercent=`, `POST /{year}/distribute`,
`GET /execution?year=&month=` —la ejecución bajo el permiso del presupuesto—).

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
