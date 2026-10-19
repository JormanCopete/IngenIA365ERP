# Runbook: la puesta en marcha de captaciones y aportes — migrar los saldos y activar por naturaleza

> Feature 014 (ahorros, aportes y CDAT dentro de la Cartera), tarea T228. Para el jefe de captaciones, la contadora y quien opera la salida
> en vivo. Molde: [cartera-puesta-en-marcha.md](cartera-puesta-en-marcha.md) (la de los créditos, feature 013), que se da por leída: aquí
> va lo que cambia. Recorrido de ensayo: [quickstart.md](../../specs/014-ahorros-aportes-cdt/quickstart.md) (US9). Columnas de cada
> plantilla: [contracts/plantillas-y-archivos.md](../../specs/014-ahorros-aportes-cdt/contracts/plantillas-y-archivos.md) §1 y §4,
> corregidas por [research.md](../../specs/014-ahorros-aportes-cdt/research.md) §16 (E115–E121). La mecánica común de las plantillas, en
> [plantillas-de-importacion.md](../manual/plantillas-de-importacion.md). El despliegue del programa, en
> [captaciones-despliegue.md](captaciones-despliegue.md); el retiro de lo heredado, en
> [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md). El producto, para la contadora y el jefe de captaciones, en
> [cartera-captaciones.md](../manual/cartera-captaciones.md); la parte contable, en
> [contabilidad-contrato-de-contabilizacion.md](../manual/contabilidad-contrato-de-contabilizacion.md) §10.

La Cartera tiene **tres naturalezas** que se activan **por separado**, cada una con su fecha en `LND_Setup`:

| Naturaleza | Qué cubre | Columna de `LND_Setup` | Se activa en |
|---|---|---|---|
| Créditos (`Placement`) | la cartera de créditos (feature 013) | `ActivationDate` | `/cartera/activacion` |
| **Aportes** (`Contribution`) | aportes sociales: mínimos, ordinarios, extraordinarios, revalorizados | `ContributionsActivationDate` | `/cartera/captaciones/activacion` |
| **Captaciones** (`Deposit`) | ahorro a la vista, CDAT, ahorro contractual y ahorro permanente, juntos | `DepositsActivationDate` | `/cartera/captaciones/activacion` |

El ahorro a la vista, el CDAT, el contractual y el permanente **no se activan por separado**: son clases de una misma naturaleza. Una
cooperativa que no capta ahorro (una que sólo lleva aportes, como COOFLOPAL) activa sólo aportes.

**Los créditos se activan primero** (`Lending.Activation.LoansFirst`): el cruce al retiro y la consulta de aportes los necesitan, y la fecha de
aportes o de captaciones no puede ser anterior a la de créditos. Entre aportes y captaciones el programa no impone orden.

Cada naturaleza se activa de una de dos formas:

- **Con carga** (cooperativa con historia): la cooperativa trae sus saldos **con fecha de corte C** en una carga de migración por partes.
  Aplicar la carga **sólo la guarda**: valida entre partes y cuadra contra Contabilidad. Las cuentas y sus saldos iniciales **nacen al
  activar**, con fecha **C+1**, en una sola transacción (todo o nada).
- **Sin carga** (cooperativa sin historia, FR-117): no hay saldos que traer; la activación rige desde la fecha que se elija (no futura), sin
  carga y sin acta.

## 1. Lo que debe existir antes

La pantalla `/cartera/captaciones/activacion` lista cada requisito con su estado y quién lo arregla
(`GET /api/lending/deposits/activation?nature=Contribution|Deposit`). **Lo que falta bloquea en todos los ambientes** (decisión del dueño
del 2026-10-18, D14: hasta esa fecha, fuera de producción la entrega, el perfil y la matriz quedaban «dispensados»). Lo único que distingue
a DEV y QA es **qué cuenta** como recaudador y como Tesorería: allí valen los dobles de prueba `TST` (recaudador) y `TES` (tesorería),
registrados con sus adopciones ([cartera-dobles-de-prueba.md](cartera-dobles-de-prueba.md)); en producción, nunca. Un ensayo en DEV o QA
prepara, entonces, lo mismo que producción.

| Requisito (código) | Qué pide | Dónde se corrige |
|---|---|---|
| `LoansActivated` | créditos activados con fecha igual o anterior a la de esta naturaleza | `/cartera/activacion` |
| `LoadApplied` · `DifferencesAccepted` | con carga: la carga de la naturaleza aplicada y su cuadre sin diferencias o con todas aceptadas por acta; sin carga, no aplican | `/cartera/captaciones/migracion` |
| `DateInTerm` | con carga: fecha = C+1, ya llegada, dentro del plazo de la carga; sin carga: fecha no futura | la misma pantalla |
| `AccountingMonthOpen` | contabilidad iniciada; un mes cerrado **no impide** activar y sale como aviso (el informativo se registra en el primer mes abierto) | `/contabilidad/periodos` |
| `AccountsCollectorRegistered` | un recaudador (Recaudo, Tesorería o Nómina) con el contrato de cuentas (`ProveedorDeCuentas`) adoptado; en producción, que no sea un doble de prueba | `/admin/participantes-de-integracion` |
| `TreasuryDestinationRegistered` | el destino de Tesorería con la **orden de giro v2** (`OrdenDeGiroEmitida` versión 2) adoptada, en todos los ambientes; en producción, real | `/admin/participantes-de-integracion` |
| `DeliveriesComplete` | el despliegue trae las entregas K7 a K11 (`CatalogoDeCartera.EntregaVigente = K11`) | despliegue ([captaciones-despliegue.md](captaciones-despliegue.md)) |
| `ProfileAllowsClasses` | sólo captaciones: el perfil regulatorio vigente autoriza captar ahorro (`MayTakeDeposits`); aportes no lo necesita | `/admin/perfil-regulatorio` |
| `LinesWithMatrix` | cada línea activa de la naturaleza contabiliza los hechos que emitirá desde el primer día (pregunta a Contabilidad por el puerto) | `/contabilidad/cartera/matriz` y `/contabilidad/cartera/completitud` |
| `DepositRolesCreated` | los cuatro roles sugeridos de captaciones y aportes (§4) | ver §4 |
| `PayrollConceptsReady` | no se evalúa (la Cartera no lee Nómina): sale el **aviso** de revisar que `DESC_APORTES` y `DESC_AHORROS` tengan cuentas contables antes de la primera nómina | `/nomina/conceptos` |

Además de lo que la pantalla revisa:

- **Retiro de lo heredado** hecho en la base, con respaldo ([captaciones-retiro-heredado.md](captaciones-retiro-heredado.md); §2).
- **Personas**: titulares, cotitulares, firmantes, representantes, beneficiarios y asociados. **Ninguna plantilla crea personas**: un
  documento que no existe vivo en Maestros › Personas es un error de fila y columna. Una cuenta de aportes es de un **asociado vigente al
  corte** (`Lending.Savings.MembersOnly`).
- **Órganos de gobierno** con sus miembros (`/cartera/actas`): la aceptación de diferencias y la activación con carga citan el **punto de una
  sesión cerrada**.
- **Oficinas vinculadas** a su sucursal contable (Maestros › Agencias): la columna `oficina` de las plantillas la exige.
- **Valores nacionales** replicados y al día: los de la 013 (`SMMLV`, `UVT`, `FESTIVO`, `IPC`) y los de la 014 (§3.3).
- **Contabilidad iniciada con la apertura al corte** (`/contabilidad/apertura`): el cuadre compara contra los saldos contables a C; sin
  contabilidad, `Lending.Accounting.NotStarted`.
- **`Contabilidad.FaltaDeRegla`** arranca en **Bloquear**: con la contabilidad iniciada, nada de captaciones que no tenga regla se confirma
  (pantalla, contrato de cuentas ni proceso diario). Para operar mientras se completa la matriz, fijar `Advertir` con su vigencia en
  `/cartera/parametros`.

Comprobación del registro de participantes (PostgreSQL / SQL Server):

```sql
SELECT p."Code", p."Class", p."Role", p."Destination", p."ValidTo", a."ContractName", a."Version"
FROM dbo."COR_IntegrationParticipants" p
LEFT JOIN dbo."COR_IntegrationParticipantAdoptions" a ON a."ParticipantId" = p."Id" AND NOT a."IsDeleted"
WHERE NOT p."IsDeleted" ORDER BY p."Code";
```

```sql
SELECT p.[Code], p.[Class], p.[Role], p.[Destination], p.[ValidTo], a.[ContractName], a.[Version]
FROM [dbo].[COR_IntegrationParticipants] p
LEFT JOIN [dbo].[COR_IntegrationParticipantAdoptions] a ON a.[ParticipantId] = p.[Id] AND a.[IsDeleted] = 0
WHERE p.[IsDeleted] = 0 ORDER BY p.[Code];
```

Debe verse `ProveedorDeCuentas` en el recaudador (la
migración `CaptacionesAportes` se la adopta a `NOM`, Nómina, si existe vigente) y `OrdenDeGiroEmitida` versión 2 en Tesorería. En
producción no debe quedar ninguna fila con `Class = Test` vigente.

## 2. Diagnóstico y retiro de lo heredado

Antes de todo, en cada base de cooperativa: el diagnóstico de sólo lectura
[diagnostico-captaciones-heredadas.sql](../../specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql) (las 45 tablas
heredadas con sus filas) y el retiro con la migración destructiva `RetiroDeCaptacionesHeredadas`. Todo el procedimiento —respaldo,
aprobación, segundo revisor, la fila `LND.CaptacionesHeredadas.RetiroAprobado` de la decisión D15 y cómo se vuelve atrás— está en
[captaciones-retiro-heredado.md](captaciones-retiro-heredado.md). **Los saldos heredados no se migran solos**: si el diagnóstico encuentra
ahorros, depósitos, aportes o CDT con filas, esos saldos entran por la carga de migración (§5), no por la base vieja.

## 3. Parámetros, valores nacionales y líneas

### 3.1 Parámetros de la Cartera

`/cartera/parametros` (`COR_ParameterVersions`, con vigencia). Las claves de captaciones nacen con un defecto «pendiente de validar» y la
cooperativa escribe la suya con su vigencia (research E15 y §13). Las que deciden la puesta en marcha:

| Clave | Para qué |
|---|---|
| `Captaciones.DiasMaximosFechaValor` (defecto 5) | el plazo de la activación con carga: corte + estos días |
| `Captaciones.MayoriaDeEdadAnios` (18) | la mayoría de edad del titular menor |
| `Retencion.TarifaAhorro`, `.TarifaCdat`, `.TarifaContractual`, `.TarifaPermanente`, `.UmbralDiarioUvt`, `.AlcanceDelUmbral`, `.ComponenteInflacionarioAplica` | la retención en la fuente sobre rendimientos (P2: el umbral diario y el CDAT, pendiente de la contadora) |
| `Gmf.Tarifa`, `.TopeExencionUvt`, `.PoliticaDeExencion`, `.HechosGeneradores`, `.Redondeo` | el GMF (P3 en fondos de empleados, pendiente) |
| `Aportes.TopeCuotaFondoEmpleados` (0,10), `.FraccionMinima`, `.LimiteParticipacionPersonaNatural`, `.LimiteParticipacionPersonaJuridica`, `.MinimoIrreducibleEntidad` | los topes y pisos de aportes (P12, pendiente) |
| `Captaciones.OrdenDeFondosAlRetiro`, `.DiasPlazoDevolucionAportes`, `.MotivosDeRetiroDeAsociados`, `.DestinatariosReversoTardio` | el retiro del asociado |
| `Captaciones.DiasDeCanjePorClaseDeMedio` | el canje de cheques |
| `Contabilidad.FaltaDeRegla`, `Contabilidad.ModoDePaso` | la validación previa y el modo de paso de cada hecho (§1, y el §10.6 del manual contable) |

### 3.2 Líneas

Las líneas de aportes y de captación se crean **por pantalla**: `/cartera/lineas?naturaleza=aportes` y `/cartera/lineas?naturaleza=captacion`
(«Líneas de aportes» y «Líneas de captación» en el grupo de menú «Captaciones y aportes»). **No hay plantilla de importación de líneas**
(research E115, decisión pendiente del dueño en §16.1). Una sola línea de aportes activa por cooperativa
(`Lending.Contributions.LineAlreadyActive`); una línea de captación sólo se activa si el perfil autoriza captar.

**Cuidado con la fecha (brecha conocida).** La plantilla de cuentas exige que la línea tenga **versión vigente al corte C**, y una versión
de captación o de aportes **no puede regir desde antes de hoy** (`Lending.DepositLine.Retroactive` / `Lending.ContributionLine.Retroactive`).
Con lo construido, las líneas tienen que **crearse a más tardar el mismo día del corte**, con su versión desde esa fecha. Si se crean después
del corte, la importación de las cuentas responde «La línea … no tiene versión vigente al corte» y no hay forma de fecharlas antes. A
diferencia de los créditos (que sí admiten versión retroactiva por la plantilla de líneas en una línea sin uso), aquí no hay salida: planear la
creación de las líneas antes del corte, o pedir al dueño la plantilla de líneas o la versión retroactiva en una línea sin cuentas (anotado en
§9 y en research §26).

### 3.3 Valores nacionales de la 014

Los registra el maestro en `/admin/valores-nacionales` y se replican a cada cooperativa ([valores-nacionales.md](valores-nacionales.md)):

| Código | Para qué | Semilla |
|---|---|---|
| `COMPONENTE_INFLACIONARIO` (clave = año gravable) | lo no gravado de los rendimientos | 2025 = 0,5543, pendiente de validar |
| `LIMITE_INEMBARGABLE` | el límite inembargable de los depósitos | **sin semilla**: lo registra el maestro con su fuente (P5) |
| `TOPE_ENTREGA_SIN_SUCESION` | la entrega a herederos sin juicio de sucesión | **sin semilla** (P6) |
| `TOPE_SEGURO_DEPOSITOS` | el seguro de depósitos (Fogacoop) | 50.000.000 desde 2024-08-19, pendiente de validar |
| `LIMITE` `APORTES_TOPE_CUOTA_FONDO_EMPLEADOS` · `APORTES_FRACCION_MINIMA` | topes de aportes de fondos de empleados | 0,10 · 0,10 |
| `LIMITE` `APORTES_LIMITE_PARTICIPACION_PN` · `_PJ` | límites de participación | **sin semilla** (P12) |
| `LIMITE` `EMBARGO_COACTIVO_SMMLV` | el inembargable del cobro coactivo | 25 |
| `LIMITE` `FONDO_LIQUIDEZ_*`, `IRL_MINIMO` | pisos del fondo de liquidez | sembrados, pendientes |
| `LIMITE` `EXOGENA_1019_UMBRAL_UVT` | umbral del formato 1019 | 24 |

La revalorización de aportes necesita el `IPC` del año (sin él no se calcula).

## 4. Roles sugeridos (D18)

Las plantillas de rol de la Cartera se dividen por módulo (decisión del dueño del 2026-10-18): los trece de créditos los exige la activación
de créditos (`LendingRolesCreated`) y **los cuatro de captaciones y aportes** los exige la de aportes y la de captaciones
(`DepositRolesCreated`):

| Plantilla | Rol | Para quién |
|---|---|---|
| `cartera.jefe-captaciones` | Jefe de captaciones | líneas, cuentas, CDAT, tasas negociadas, embargos, cargos, retiros, fallecimientos, no reclamados y la **carga de migración** (`Lending.Deposits.Migration`); todas las oficinas |
| `cartera.asesor-captaciones` | Asesor de captaciones | apertura, partes de la cuenta, constitución de CDAT, exención de GMF, débitos automáticos, radicar el retiro; **sus oficinas** (hay que asignarle alcance) |
| `cartera.analista-aportes` | Analista de aportes | devoluciones, cesiones, liquidación del retiro, simulación de la asamblea |
| `cartera.tesorero-liquidez` | Tesorero (fondo de liquidez) | fondo de liquidez y saldos agregados, sin datos de titulares |

Se crean con `POST /api/lending/activation/suggested-roles` (`Security.Roles.Create`, `Idempotency-Key`), con el cuerpo
`{ "templateKeys": ["cartera.jefe-captaciones", "cartera.asesor-captaciones", "cartera.analista-aportes", "cartera.tesorero-liquidez"] }`;
sin claves crea los diecisiete. Es idempotente. La activación misma (`Lending.Deposits.Activate`) y la aceptación de diferencias
(`Lending.Migration.AcceptDifferences`) **no** vienen en estas plantillas: asignarlas a quien corresponda. Después, en `/cartera/alcances`,
las oficinas y líneas de cada asesor, y la persona de cada usuario que acepta o activa.

## 5. La carga de migración por partes

`/cartera/captaciones/migracion` (`Lending.Deposits.Migration`) → «Carga nueva» con la **naturaleza** y la **fecha de corte**
(`POST /api/lending/deposits/migration/loads`). Una carga es de una sola naturaleza: aportes y captaciones llevan cargas separadas, cada una
con su corte. Por parte: plantilla vacía (o con los datos cargados, que exige además `Lending.Reports.ExportPersonalData`), diligenciar,
**Revisar** (no guarda; errores con fila y columna) y **Aplicar** (todo o nada para ese archivo). Varias tandas por parte, «Reemplazar la
parte» y avisos entre partes funcionan como en los créditos ([cartera-puesta-en-marcha.md](cartera-puesta-en-marcha.md) §3).

Las partes, en orden de carga (`DefinicionesDeMigracionDeCaptaciones.Todas`):

| Orden | Parte | Naturaleza | Obligatoria | Qué trae |
|---|---|---|---|---|
| 0 | `line-equivalences` | ambas | no | código heredado de cada producto → línea nueva (con la clase en captaciones); va **primero** si las cuentas traen `codigoHeredado` en vez de `linea` |
| 5 | `member-accounts` | captaciones | **sí** | cuentas de ahorro: titular, oficina, estado, saldo de capital, interés causado no liquidado, última liquidación, cargos pendientes, contractual |
| 6 | `member-account-parties` | ambas | no | cotitulares, firmantes, representantes (con su `fundamento`) y beneficiarios |
| 7 | `term-deposits` | captaciones | no | CDAT vigentes: capital, constitución, plazo, tasas, periodicidad, intereses pagados y causados, destino, instrucción al vencimiento |
| 8 | `contribution-accounts` | aportes | **sí** | cuentas de aportes por asociado: mínimos, ordinarios, extraordinarios, revalorizados (con «Tomar de los saldos cargados» si la 013 ya cargó saldos de aportes) |
| 9 | `account-installments` | ambas | no | cuotas atrasadas con lo pagado y el motivo de no descuento |
| 10 | `holds-and-garnishments` | ambas | no | bloqueos y embargos vigentes al corte |
| 11 | `gmf-exemptions` | captaciones | no | la cuenta exenta del GMF y lo exento usado del mes |
| 12 | `tax-year-to-date` | captaciones | no | el acumulado tributario del año (rendimientos, componente inflacionario, retención, GMF) para el certificado |

Detalles que importan:

- **El número migrado se conserva** y el consecutivo de la línea sigue por encima; una cuenta de aportes sin número toma el consecutivo.
- Una cuenta `Inactive` nace inactiva **desde el corte**, no desde su último movimiento (el plazo de no reclamados corre desde la activación).
- El representante legal de un menor va en `member-account-parties` con su `fundamento`; el soporte se sube después: la cuenta nace con
  «actualizar datos».
- El **interés causado y no liquidado** entra como saldo inicial del interés por pagar y **no se vuelve a causar**: la primera liquidación lo
  liquida.
- En aportes, `minimoNoReducible`, `ingresoRegistrado` y `cuotaPactada` son **informativos**: se guardan en la fila y sólo avisan.

**Aplicar la carga** («Aplicar la carga…», con motivo): valida entre partes y, con un error, **no guarda nada**. Si aplica, calcula el cuadre,
deja la carga `Applied` con su **plazo de activación** (corte + `Captaciones.DiasMaximosFechaValor`) y emite el informativo
`CargaDeCaptacionesAplicada`. Otra carga aplicada de la misma naturaleza deja la anterior `Replaced` (nunca se borra). Errores propios:
`Lending.Migration.PartNotForNature`, `.PartNotAvailable`, `.AccountNumberDuplicate`, `.LineEquivalenceMissing`,
`.MinorWithoutRepresentative`, `.AccountNotInLoad`.

## 6. Cuadre contra Contabilidad y cifras de referencia

- **Cuadre** (pestaña «Cuadre», `GET …/loads/{id}/balance-check`, `Lending.Reconciliation.View`, sin titulares): por cada combinación —rol
  del concepto, línea, naturaleza, clase, clase de aporte, marca (exenta si la carga la marca) y sucursal contable de la oficina— lo cargado
  frente a lo contable al corte, **por conjunto de cuentas**, con la misma clave de la conciliación diaria. Sin reglas en la matriz cada
  combinación sale «sin regla» y todo es diferencia: completar la matriz antes de aplicar.
- **Cifras de referencia** (pestaña «Cifras de referencia», `GET`/`PUT …/loads/{id}/reference-figures`): los totales **por línea y concepto**
  que entrega la cooperativa de lo que lleva hoy (`{ lineCode, concept, amount, source }`, con la fuente libre: «Balance de prueba de
  septiembre», «Reporte de saldos»). Se cotejan con lo cargado y salen como segunda columna del cuadre. Se reemplazan enteras mientras la
  carga no esté activada. Son la evidencia para el acta: ninguna pantalla nombra el sistema de origen.
- **Aceptar diferencias** (pestaña «Diferencias», `POST …/loads/{id}/differences/accept`, `Lending.Migration.AcceptDifferences`): varias a
  la vez, con motivo y el **punto de una sesión cerrada** del órgano que las aprueba. Con el par de segregación activo, quien cargó no acepta.

La contadora valida (research §16.2) las cifras de referencia por línea y concepto al corte y qué diferencias se aceptan con acta.

## 7. La activación, por naturaleza

`/cartera/captaciones/activacion` (`Lending.Deposits.Activate`), eligiendo **Aportes** o **Captaciones**:
`POST /api/lending/deposits/activation` con `Idempotency-Key` y
`{ nature, migrationLoadPublicId?, activationDate, sessionItemPublicId?, acknowledgeWarnings, reason }`.

1. **Requisitos** sin faltantes (§1) y **avisos** revisados (mes contable cerrado, conceptos de Nómina) con `acknowledgeWarnings`.
2. **Con carga**: la fecha es **C+1**, ya llegada y **dentro del plazo** (`Lending.Activation.DeadlinePassed` si venció); el **acta** es
   obligatoria: el punto cerrado y aprobado de una sesión sobre esa carga (`Lending.Session.ItemMismatch` si no lo es). Antes de activar se
   **recalcula el cuadre** con los saldos contables de ese momento: si cambió, nace la versión siguiente del cuadre y responde
   `Lending.Activation.DifferencesNotAccepted` (volver a aceptar).
3. **Sin carga**: la fecha elegida, no futura (`Lending.Activation.CutoffInvalid`), sin acta.
4. **Activar.** En una transacción: nacen las cuentas en tandas de 500 con su número, su línea, sus partes y su **saldo inicial por concepto
   al corte** (operación de saldo inicial, **sin hechos contables**: los saldos ya están en la apertura contable), las cuotas atrasadas, la
   marca y el uso del GMF del mes, el acumulado tributario; la carga queda `Activated`; se escribe la fila de `LND_Activations` con su
   naturaleza; se emite **un solo** informativo, `CaptacionesActivadas`; y se pone la fecha de la naturaleza en `LND_Setup`. Desde ese
   momento el **contrato de cuentas** (Recaudo, Nómina, Tesorería) y la **consulta de aportes** (la que usan los motores de crédito) dejan de
   responder «no habilitado».

Errores: `Lending.Savings.DeliveryNotAvailable` (el despliegue no trae la entrega: aportes con K7, captaciones con K8),
`Lending.Activation.AlreadyActivated`, `.LoansFirst`, `Lending.MigrationLoad.NotFound`, `.CutoffInvalid`, `.DeadlinePassed`,
`.DifferencesNotAccepted`, `.RequirementsMissing` (con la lista y quién corrige cada uno) y `Lending.Session.ItemMismatch`. La activación es
todo o nada; la misma `Idempotency-Key` con el mismo cuerpo devuelve la misma activación.

### Verificación (PostgreSQL)

```sql
SELECT "ActivationDate", "ContributionsActivationDate", "DepositsActivationDate" FROM dbo."LND_Setup";
SELECT "Number", "Nature", "CutoffDate", "Status" FROM dbo."LND_MigrationLoads" ORDER BY "Number";   -- Nature 2 captaciones, 3 aportes; Status 4 activada
SELECT "Nature", "ActivationDate", "MigrationLoadId" FROM dbo."LND_Activations" ORDER BY "Id";
SELECT "Nature", "Origin", COUNT(*) FROM dbo."LND_MemberAccounts" GROUP BY "Nature", "Origin";       -- Origin 2 = migrada
SELECT COUNT(*) FROM dbo."LND_MemberAccountOperations" WHERE "Kind" = 1;                             -- saldos iniciales
SELECT "Type", COUNT(*) FROM dbo."COR_IntegrationMessages"
 WHERE "Type" IN ('CargaDeCaptacionesAplicada', 'CaptacionesActivadas', 'ConsignacionEnCuentaAplicada', 'AporteRecibido')
 GROUP BY "Type";                                                                                   -- ningún hecho de saldo por la activación
```

### Verificación (SQL Server)

```sql
SELECT [ActivationDate], [ContributionsActivationDate], [DepositsActivationDate] FROM [dbo].[LND_Setup];
SELECT [Number], [Nature], [CutoffDate], [Status] FROM [dbo].[LND_MigrationLoads] ORDER BY [Number];
SELECT [Nature], [ActivationDate], [MigrationLoadId] FROM [dbo].[LND_Activations] ORDER BY [Id];
SELECT [Nature], [Origin], COUNT(*) FROM [dbo].[LND_MemberAccounts] GROUP BY [Nature], [Origin];
SELECT COUNT(*) FROM [dbo].[LND_MemberAccountOperations] WHERE [Kind] = 1;
SELECT [Type], COUNT(*) FROM [dbo].[COR_IntegrationMessages]
 WHERE [Type] IN ('CargaDeCaptacionesAplicada', 'CaptacionesActivadas', 'ConsignacionEnCuentaAplicada', 'AporteRecibido') GROUP BY [Type];
```

Y por la API: la consulta de una muestra de cuentas a la fecha de corte devuelve lo cargado, y la consulta de aportes de una persona devuelve el
mismo saldo que su extracto de aportes (SC-010).

## 8. Después de activar

- **El primer día**: el paso de captaciones del ciclo diario corre al terminar la corrida `DailyCycle`, **después** de la causación de los
  créditos, sólo para las naturalezas activadas con fecha ≤ el día. Revisar la corrida en la pestaña «Corridas del proceso» de
  `/cartera/bandeja-de-mensajes`: los errores por cuenta quedan en la corrida con el número de la cuenta y el paso `Captaciones`.
- **Contabilidad**: la conciliación (`/cartera/conciliacion-contable`, vista `deposits-reconciliation`) en cero o con cada diferencia explicada
  hecho por hecho; los puentes `OwnLoanPayoff` en cero al cierre del día; la bandeja de mensajes sin rechazos sin explicar
  ([manual contable §10.8](../manual/contabilidad-contrato-de-contabilizacion.md)).
- **Operación**: abrir una cuenta para un asociado existente, consignar y retirar por el recaudador, y registrar el tiempo (§10).
- **Nómina**: la primera nómina con `DESC_APORTES` y `DESC_AHORROS` con sus cuentas.
- **El primer cierre mensual** de la Cartera incluye la sección de captaciones (cierre de causación y de posición, sin SARC). Contabilidad no
  cierra el mes hasta que la Cartera lo cierre (`Accounting.Period.LendingNotClosed`, que dice desde qué día está pendiente la causación de
  captaciones).

### Si falla

- **La activación es todo o nada**: si algo falla no nace ninguna cuenta y la carga sigue `Applied`. Corregir (otra carga con el mismo corte, o
  reemplazar la parte) y volver a activar.
- **Plazo vencido** (`DeadlinePassed`): una carga con **corte nuevo**, aplicarla, volver a cuadrar y aceptar, y activar en su C+1. Recordar que
  las líneas necesitan versión vigente a ese corte (§3.2).
- **`DifferencesNotAccepted` después de aceptar**: alguien contabilizó entre la aceptación y la activación; revisar la versión nueva del cuadre
  y volver a aceptar con acta.
- **`RequirementsMissing`**: la lista dice qué falta y quién lo corrige.

## 9. Brechas conocidas al escribir este runbook

- **Líneas y corte** (§3.2): la versión de una línea de captación o de aportes no puede regir desde antes de hoy y no hay plantilla de líneas;
  la carga exige versión vigente al corte. Crear las líneas a más tardar el día del corte.
- **Los permisos de activar y de aceptar diferencias** no están en las plantillas de rol de captaciones (§4): se asignan aparte.

## 10. Medición con la cooperativa piloto (T234, SC-004, SC-018)

> Para el dueño: registrar aquí la medición con usuarios reales de la cooperativa piloto. **SC-004**: un asesor abre una cuenta para un
> asociado existente en menos de 3 minutos y un cajero registra una consignación o un retiro en menos de 1 minuto. **SC-018**: el 90 % de los
> usuarios de oficina completa apertura, consignación y retiro en su primer intento, sin ayuda.

| Fecha | Cooperativa | Ambiente | Usuario (rol) | Tarea | Tiempo | Primer intento sin ayuda (sí/no) | Observaciones |
|---|---|---|---|---|---|---|---|
| | | | | Apertura de cuenta | | | |
| | | | | Consignación | | | |
| | | | | Retiro | | | |

Resultado: _pendiente_.
