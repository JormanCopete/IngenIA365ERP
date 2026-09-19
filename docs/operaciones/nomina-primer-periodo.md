# Nómina: el primer período de una cooperativa

Runbook de la feature 005 (novedades por período y liquidación). Qué tiene que existir
en la base de una cooperativa antes de que alguien pueda calcular su primera nómina, cómo
comprobarlo **contra la base del ambiente** (no contra el repositorio), qué hacer cuando
el cálculo se niega por un parámetro sin vigencia, y cómo cargar el año nuevo.

Regla de la casa que aplica aquí: el programa **no trae ningún valor legal fijo**. Salario
mínimo, auxilio de transporte, UVT, porcentajes y tablas viven en `PAY_PayrollLegalParameters`
con fecha de vigencia. Si falta uno para la fecha del período, el cálculo se niega y lo
nombra. Eso no es un error del programa: es el aviso de que hay que registrar el dato.

---

## 1. Qué deja la semilla

Al arrancar la API con `Database:AutoMigrate = true` y `Database:Seed:RunParametricSeed = true`
(así están `appsettings.Development.json` y el fixture de integración), cada base de
cooperativa recibe, de forma idempotente:

| Qué | Dónde | Cuánto | Quién lo siembra |
|---|---|---|---|
| Plan de nómina por defecto `DEFAULT` («Nómina general», mensual) | `PAY_PayrollPlans` | 1 fila, `IsDefault = 1` | la migración `NominaNovedadesYLiquidacion` lo inserta antes de las FK; `PayrollPlansSeeder` lo completa |
| Definiciones de conceptos (salario, auxilio, horas y recargos, incapacidades y licencias, salud, pensión, FSP, retención, aportes del empleador, parafiscales, provisiones, descuentos, cartera) | `PAY_PayrollConceptDefinitions` | 40, `Origin = Seed`, `ValidFrom = 2026-01-01` | `PayrollConceptDefinitionsSeeder` |
| Parámetros legales con vigencia 2026 (SMMLV, auxilio, UVT, porcentajes, tabla de retención en UVT, tabla FSP) | `PAY_PayrollLegalParameters` + `PAY_PayrollLegalParameterRanges` | 33 códigos | `PayrollLegalParametersSeeder` |
| Tabla de retención **propia de un plan** (`/nomina/parametros-retencion`): si el plan del período tiene tramos, reemplazan a `RETEFTE_TABLA_UVT` para ese plan; sin tramos, aplica la legal (desde el 2026-09-19) | `PAY_WithholdingParameters` (`PayrollPlanId`) | vacía | — |
| Las cinco **clases de riesgo ARL** (I a V) | `PAY_WorkRiskRates` | 5 filas, `Code` 1..5 | `WorkRiskClassesSeeder` (desde el 2026-09-11) |
| Tipo de comprobante `NM` («Nómina», módulo `NOM`) | `ACC_VoucherTypes` | 1 | `PayrollVoucherTypeSeeder` |
| Permisos `Payroll.Plans.*`, `Payroll.Novelties.*`, `Payroll.Runs.*`, `Payroll.Payments.*`, `Payroll.Payslips.*`, `Payroll.Concepts.*`, `Payroll.LegalParameters.*` | catálogo de permisos de la cooperativa | — | `PayrollPermissionCatalogSeeder` (Identity) |
| Políticas `Payroll.Rounding`, `Payroll.VariationThresholdPercent`, `Payroll.AllowSameUserApproval`, `Payroll.ApplyEmployerExemption` | `SystemSettings` | 4 claves | `CatalogSeeders` |

Lo que la semilla **no** deja, porque es decisión de cada cooperativa:

- Las **cuentas contables de cada concepto** (`PAY_PayrollConceptDefinitionAccounts`). Sin
  ellas la aprobación se bloquea con `Payroll.ConceptWithoutAccounts` y la lista de códigos.
  Se cargan en Nómina › Conceptos › ícono de cuentas (`PUT /api/payroll/concept-definitions/{code}/accounts`).
- El **período contable `CNT` abierto** del mes que se va a liquidar (Contabilidad › Períodos).
  Sin él: `Payroll.AccountingPeriodClosed`.
- Al menos una **sucursal** y un **centro de costo**; el centro de costo del empleado se
  resuelve por su `LegacyCode`, y si no coincide se usa el primero.
- Empleados con **persona**, **plan**, **salario**, **fecha de ingreso** y **afiliaciones**
  (salud, pensión, ARL con clase 1..5, caja). Cada afiliación faltante es un bloqueo
  visible en el borrador, no un error silencioso.

  **La clase de riesgo ARL se elige en la ficha** (Nómina › Empleados › pestaña de
  seguridad social › «Clase de riesgo ARL»), y la lista sale de `PAY_WorkRiskRates`
  (Nómina › Clases de riesgo ARL). La ficha guarda la **fila** (`WorkRiskRateId`), no el
  número; la clase es el `Code` de esa fila y es lo que el motor traduce al parámetro
  `ARL_CLASE_{I..V}_PCT`. La tarifa que muestra esa tabla es informativa: el porcentaje
  que se liquida es el del parámetro legal vigente. Hasta el 2026-09-11 esto fallaba
  de dos maneras a la vez —la tabla nacía vacía y la ficha no tenía dónde elegirla—,
  así que **toda** liquidación salía con «Sin clase de riesgo ARL registrada en la
  ficha». En una cooperativa creada antes de esa fecha: reaplicar la semilla (§5) y
  luego abrir cada empleado y elegir su clase.

  **Fondo de cesantías y caja de compensación** (desde el 2026-09-12) también se
  eligen en la ficha, de los catálogos Nómina › Cesantías y Nómina › Cajas de
  compensación (este último nace vacío: la semilla no trae cajas, se cargan a mano).
  Ninguno de los dos cambia la liquidación mensual —la provisión de cesantías y el
  aporte a caja se calculan igual—; importan para la consignación anual, la planilla
  y los reportes por entidad. La migración
  `CajasDeCompensacionYFondoDeCesantiasEnLaFicha` estrecha `SeveranceFundId` de
  `decimal(6,0)` a entero (sin pérdida: siempre valió 0) y crea
  `PAY_FamilyCompensationFunds`.

## 2. Cómo comprobarlo contra la base del ambiente

Con un token de administrador de la cooperativa (segundo factor incluido). Se mira la
respuesta de la API, que lee la base real; nada de esto se infiere del repositorio.

```bash
H="Authorization: Bearer $TOKEN"; API=https://<host-del-ambiente>

# Plan por defecto
curl -s -H "$H" "$API/api/payroll/plans" | jq '.[] | select(.isDefault)'

# Conceptos vigentes (deben ser 40 de origen Seed, salvo los que la cooperativa haya revisado)
curl -s -H "$H" "$API/api/payroll/concept-definitions" | jq 'length'

# Parámetros legales: qué códigos requeridos NO tienen vigencia este año o el siguiente
curl -s -H "$H" "$API/api/payroll/legal-parameters" | jq '{missingThisYear, missingNextYear}'
```

Si `missingThisYear` no está vacío, el primer período del año **no se va a poder calcular**
hasta registrar esas vigencias (sección 4).

Desde SQL, si hace falta mirar la base directamente (una cooperativa = una base):

```sql
SELECT Code, Name, IsDefault FROM PAY_PayrollPlans WHERE IsDeleted = 0;
SELECT COUNT(*) AS Conceptos FROM PAY_PayrollConceptDefinitions WHERE IsActive = 1 AND IsDeleted = 0;
SELECT Code, ValidFrom, ValidTo, Value FROM PAY_PayrollLegalParameters WHERE IsDeleted = 0 ORDER BY Code, ValidFrom;
SELECT Code, Name, ModuleCode FROM ACC_VoucherTypes WHERE Code = 'NM';
```

En PostgreSQL los nombres van entre comillas dobles y `IsDeleted = false`.

## 3. Cuando el cálculo responde `Payroll.LegalParameterMissing`

El mensaje trae los códigos y la fecha: *«No hay vigencia al 31/01/2027 para los parámetros
legales: SMMLV, AUX_TRANSPORTE, UVT»*. No es un fallo del programa y no se arregla
reiniciando nada.

1. Confirmar con `GET /api/payroll/legal-parameters` qué códigos aparecen en `missingThisYear`.
2. Registrar la vigencia de cada uno (sección 4) con la fuente normativa (decreto o resolución).
3. Volver a calcular. Cada línea del borrador muestra el parámetro y la vigencia que usó, así
   que se puede comprobar en la primera liquidación del año que el valor es el correcto.

Si el código que falta **no está en el catálogo** (`Payroll.LegalParameterNew`), la
primera vigencia exige además nombre y tipo (`kind`). Sólo debería pasar con conceptos
propios de la cooperativa que referencian un código nuevo.

## 4. Cargar la vigencia de un año nuevo

Cuando el Gobierno fija salario mínimo, auxilio de transporte y UVT (diciembre–enero), y
cuando cambia cualquier tarifa o tabla:

- **Pantalla**: Nómina › Parámetros legales › fila del código › «Nueva vigencia»: fecha desde,
  valor (o la tabla de tramos: desde, hasta, tarifa, fijo — contiguos, sin huecos, último abierto)
  y la fuente. La vigencia anterior se cierra el día antes.
- **API**:

```bash
curl -s -X POST -H "$H" -H "Content-Type: application/json" \
  -d '{"validFrom":"2027-01-01","value":1900000,"source":"Decreto xxxx de 2026"}' \
  "$API/api/payroll/legal-parameters/SMMLV/versions"
```

Un solapamiento con una vigencia existente responde `Payroll.LegalParameterOverlap`; una
tabla con huecos o sin tramo final abierto, `Payroll.RangeTableInvalid`. Ninguna vigencia
se edita ni se borra: se agrega la siguiente.

Los borradores ya calculados de períodos que cubran la fecha quedan **desactualizados** y
hay que recalcularlos antes de aprobar.

## 4b. Periodicidades, sub-períodos y recurrentes (feature 006, 2026-09-12)

- El plan admite **Mensual (30)**, **Quincenal (15)**, **Decadal (10: 1–10, 11–20, 21–fin)** y
  **Semanal (7)**. El motor prorratea todo por los días del plan; la periodicidad se puede
  cambiar mientras el plan no tenga períodos ni liquidaciones.
- Cada período lleva **número dentro del mes** (quincena 1/2, década 1–3, semana 1–5) y **mes
  de imputación**; se proponen desde la fecha de inicio y se ajustan al crear (una semana que
  cruza de mes se imputa al que decida la persona). La duración se valida contra el plan:
  sólo el último período del mes admite lo que el calendario le quite o le sume.
- Una recurrente tiene **«Aplica en»**: cada período, sólo el primero del mes o sólo el último
  (en semanal, la mayor semana creada del mes). El valor es por período en que aplica.
- Un período **Calculado** se devuelve a Abierto con **«Descartar borrador»** (Liquidación):
  la corrida queda descartada con motivo, las recurrentes generadas se anulan y se regeneran al
  recalcular; nada se borra ni toca contabilidad. Una aprobada sigue reversándose.
- **Reportes de nómina** (`/reportes/nomina`): comprobante por empleado, resumen por concepto,
  detalle empleado × concepto, novedades del período e histórico por empleado; Excel, PDF y
  Word con los mismos totales. Reemplaza a «Comprobante Nómina», que llamaba a una ruta
  inexistente.

## 4c. Jornada laboral y recargos (Ley 2101 de 2021 y Ley 2466 de 2025)

La semilla lleva la ley **por vigencias**, y las revisiones de mitad de año se aplican también a
cooperativas que ya tenían la semilla (al arrancar la API o con «Reaplicar semilla»): se
inserta la versión nueva y se cierra la anterior el día antes, **sólo si la anterior es de la
semilla y sigue abierta** —una vigencia o versión propia de la cooperativa no se pisa.

**Semilla base 2026** (una sola vigencia, `2026-01-01`, abierta). Decisión del 2026-09-13: la
plataforma está en pruebas y ninguna nómina real del primer semestre de 2026 se liquida aquí,
así que la base lleva **directamente la norma de julio de 2026** en vez de dos vigencias:

| Qué | Semilla base (desde el 01/01/2026) | Norma | Próximo escalón (no cargado) |
|---|---|---|---|
| `HORAS_MES` (valor hora = salario / horas) | **210** (42 h/semana) | Ley 2101 de 2021, vigente desde el 15/07/2026 | — |
| `RECARGO_DOMINICAL` | **0,90** | Ley 2466 de 2025, vigente desde el 01/07/2026 | 1,00 desde el 01/07/2027 |
| `HEX_DOM_DIURNA` / `HEX_DOM_NOCTURNA` | **2,15 / 2,65** | extra ordinaria (1,25 / 1,75) + recargo dominical | 2,25 / 2,75 desde el 01/07/2027 |

Historia: la semilla original traía 240 h, 0,75 y 2,00 / 2,50; el 2026-09-12 pasó a 220 / 0,80 /
2,05 / 2,55 con revisiones de julio (210 / 0,90 / 2,15 / 2,65); el 2026-09-13 la base tomó los
valores de julio. Las cooperativas ya sembradas se llevaron a esos valores con migraciones de
datos (`BaseLegal2026Corregida`, `HorasMesBase2026`, `NormaJulio2026ComoBase`) que **sólo tocan
la fila que sigue siendo la de la semilla**; las vigencias de julio que ya existían se conservan
(mismo valor; nada se borra, Principio XII), así que esas cooperativas muestran dos filas iguales
y las nuevas una. Esta tabla es la única fuente de la semilla base: si la contadora determina
otro valor, se cambia aquí, en el seeder y con una migración de datos, nunca sólo en una base.
El escalón de 2027 se carga como revisión (`Revisiones()` de ambos seeders) cuando se confirme.

Lo que la ley cambia y **el módulo no decide**: la jornada nocturna empieza a las **19:00**
(desde el 25/12/2025) —quien registra la novedad cuenta las horas nocturnas con esa
frontera; el sistema sólo multiplica—, y el máximo de horas extra sigue siendo cosa de quien
autoriza. Estos valores están cargados con la lectura de la norma a septiembre de 2026 y
**deben confirmarse con la contadora** antes de la primera nómina que los use; si difieren,
se corrigen con «Nueva vigencia» / «Nueva versión», nunca en el código.

## 5. Reaplicar la semilla

Tras una actualización que traiga conceptos, parámetros o clases ARL nuevos, o si alguien
borró por error algo de la semilla:

- **Pantalla**: Nómina › Conceptos › «Reaplicar semilla».
- **API**: `POST /api/payroll/concept-definitions/seed` (permiso `Payroll.Concepts.Manage`).

Inserta lo que falte y **no toca** lo que la cooperativa ya ajustó (versiones propias,
cuentas, vigencias registradas a mano). Es idempotente: correrla dos veces no duplica nada.

## 6. Las migraciones de esta entrega

Todas pareadas por proveedor (`Persistence.Migrations.PostgreSql` y `.SqlServer`), en
`Application/`. En DEV y QA las aplica `AutoMigrate` al arrancar la API, base por base
(operativa y cada cooperativa; hecho el 2026-09-06, `coop_prueba` incluida). En producción
`AutoMigrate` está apagado: las aplicó el Job PreSync de Argo (`erp-db-migrate`) sobre la
base operativa el 2026-09-07, y desde el 2026-09-11 el mismo Job tiene un tercer paso,
`migrate --scope cooperativas`, que lleva al día la base de cada cooperativa activa con
el mismo aprovisionador que usa la API en DEV y QA (P14 cerrado).

| Migración | Qué hace | Cuidado |
|---|---|---|
| `NominaNovedadesYLiquidacion` | 14 tablas nuevas de nómina, columnas en empleados y períodos, plan `DEFAULT` insertado antes de las FK | Idempotente; reversible |
| `NominaTablasPorRangos` | Unidad y marginalidad de las tablas por rangos, con relleno de las de retención y FSP | Idempotente; reversible |
| `NominaDetalleDeCorrida` | Bases y notas por empleado en la corrida; FKs al comprobante contable | Reversible |
| `RetiroDeVoucherTypeIdSombraEnDocumentos` | **Destructiva.** Quita de `ACC_Documents` la columna sombra `VoucherTypeId`, que duplicaba `VoucherTypeCode` y rompía todo comprobante contable creado por la API | **Backup de cada base de cooperativa y segundo revisor antes de aplicarla en un ambiente** (Principio XII); anotar las referencias en la cabecera de la migración. Su `Down` reconstruye la columna desde el código |

La última no es de nómina: la destapó la prueba e2e de aprobación, y afecta a Contabilidad.
Sin ella, `POST /api/accounting/documents` responde 500 contra PostgreSQL y SQL Server.

**Aplicada en producción el 2026-09-07** con lo que exige el Principio XII: respaldo CNPG
`erp-db-pre-nomina-mfa-20260907` a S3 (archivado continuo, PITR) más `pg_dump -Fc` de
`ingenia365erp` e `ingenia365erp_admin` en el nodo, y Jorman Copete como revisor que
autorizó la promoción. Las referencias quedaron en la cabecera de la migración. Después
del despliegue, `information_schema.columns` ya no lista `VoucherTypeId` en `ACC_Documents`
y la semilla dejó 41 definiciones de concepto, 41 parámetros legales, el plan por defecto
y el comprobante `NM`.

## 7. Lo que este runbook no cubre

- Reversar una liquidación aprobada, marcar pagos o enviar comprobantes: está en el manual
  dentro de la aplicación (guía «Liquidación de nómina»).
- Correo saliente para los comprobantes: `docs/operaciones/correo-saliente.md`. Si el
  ambiente no lo tiene, el envío responde `Payroll.EmailNotConfigured` antes de intentar y
  los comprobantes se descargan en PDF.
- La migración de datos de nóminas históricas del sistema anterior: las tablas legadas
  (`PAY_PayrollTransactions`, `PAY_PayrollEntries`, `PAY_PayrollConcepts`) se conservan de
  sólo lectura; el catálogo heredado se ve en Nómina › Conceptos › «Catálogo heredado».
