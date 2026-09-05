# Research — Novedades y Liquidación Periódica de Nómina (Feature 005)

**Phase 0 output** · Branch `005-nomina-novedades-liquidacion` · 2026-09-05

Formato por decisión: **Decision** / **Rationale** / **Alternatives considered**.
Todo lo aquí decidido salió de leer el código que existe, no de suponerlo; donde un
hallazgo cambia lo que la spec daba por hecho, se dice.

---

## Hallazgos que condicionan el diseño

| # | Hallazgo (verificado en el código) | Consecuencia |
|---|---|---|
| H-1 | `PAY_PayPeriods.PlanId` con índice único `(PlanId, PayrollCompanyId)`: en el legado **plan = planilla** (número de corrida), no «plan de nómina». | El plan de nómina de la spec necesita **su propia entidad y columna** (`PayrollPlanId`); `PlanId` no se reinterpreta. |
| H-2 | `PrincipioXI_ContableImmutable` prohíbe `Remove()`/`RemoveRange()` sobre entidades del namespace `Entities/Payroll/Transactions` (hoy vacío: el test pasa vacuamente). | Las líneas de liquidación van a ese namespace y **no se borran nunca**: cada recálculo crea una liquidación nueva y marca la anterior como reemplazada. |
| H-3 | `ProcessPayrollCommand` actual: tasas fijas (4 %, 4 %, 8,5 %, 12 %), `salario/30` sin tramos, sin auxilio ni provisiones ni tabla, escribe en `PAY_PayrollTransactions` con `ConceptId = 0` para las deducciones. | Se **retira** junto con `RegisterPayrollEntryCommand`; sus tablas quedan como legado de consulta. Nada de lo nuevo escribe en `PAY_PayrollTransactions` ni `PAY_PayrollEntries`. |
| H-4 | Existe `IDistributedLock` (Redis, `TryAcquireAsync(key, ttl)`) usado por `AcceptInvitation` y `ResetPassword`. | Se reutiliza para «un solo cálculo por período» (FR-015). |
| H-5 | Framework de semillas `IDataSeeder` (categoría `Parametric`, alcance `Tenant`, idempotente por clave natural, `CreatedBy = system:seed`), orquestado al arranque y al aprovisionar una cooperativa. | La semilla de conceptos y parámetros legales (FR-038) es un `IDataSeeder`, no una migración de datos (Principio XII: DDL separado de datos). |
| H-6 | Permisos: catálogo `SEC_Permissions` sembrado por `DomainPermissionCatalogSeeder` (`Resource.Action`), exigidos con `.RequirePermission("…")`; los endpoints de nómina actuales sólo tienen `RequireAuthorization()`. | Se crea un seeder hermano `PayrollPermissionCatalogSeeder` y **todos** los endpoints nuevos exigen permiso. |
| H-7 | `CreateDocumentCommand` es el patrón canónico de asiento: valida período contable (`AccountingPeriods`, módulo `CNT`, `Status != "C"`), cuadre, numera desde `VoucherType.NextSequenceNumber`, crea `AccountingDocument` + `JournalEntry` en el mismo `SaveChanges`. | La aprobación replica ese patrón **dentro de su propia transacción**; no invoca el comando (una sola unidad de trabajo, FR-023). |
| H-8 | `PayslipReport` (QuestPDF) existe en `API/Reports` pero el endpoint PDF devuelve «not yet implemented». `IEmailSender.EmailMessage` no admite adjuntos. | Se implementa el PDF de verdad y se extiende `EmailMessage` con adjuntos opcionales (compatible hacia atrás). |
| H-9 | `Employee` es hija de `Person` (Principio V): nombre, documento y **correo** viven en `COR_People.Email`. | El envío del comprobante usa `Person.Email`; nada de correo en `PAY_Employees`. |
| H-10 | `PrincipioVIII_DualValidation` sólo cubre namespaces de Fase 0; Payroll está fuera como deuda heredada. | Los namespaces nuevos de nómina **entran** a la lista cubierta: cada comando nuevo lleva validador o el test falla. |
| H-11 | `PayPeriod.Status` es `int` (0 abierto, 1 «liquidado» por el handler retirado) con `StatusMessage` de texto libre. | Se define un enum con transiciones explícitas; el valor 1 se reinterpreta como «calculado (borrador)» porque ninguna corrida real lo usó. |
| H-12 | `ConceptAccount` legado guarda **códigos** de cuenta como texto; la contabilidad moderna referencia `ChartOfAccount.Id`. | Las cuentas por concepto de la definición nueva son FK a `ChartOfAccounts`, no códigos. |
| H-13 | `ProcessPayrollDeductionCommand` (Cartera) ya genera descuentos por nómina por período de pago y **contabiliza por su cuenta**. | Esos descuentos entran a la liquidación como novedades de origen «Cartera», sin recontabilizar: la línea es informativa para el neto y no genera asiento. |
| H-14 | Pantallas maestro usan `SfGrid` + `SfDialog` con `HttpClient` plano; las pantallas de identidad usan clientes tipados que adjuntan el token desde `CentralAuthClient` (prerender seguro). | Las pantallas nuevas usan un cliente tipado `NominaClient` con ese patrón. |

---

## D-01 — Motor de cálculo: puro, en Domain, por formas predefinidas

**Decision**: `Domain/Payroll/Calculation/` contiene un motor **puro** (sin EF, sin
IO): recibe un `CalculationInput` (empleado con sus tramos de salario y afiliaciones,
período, novedades vigentes, versiones de concepto aplicables, parámetros legales
vigentes, políticas de la cooperativa) y devuelve `CalculationResult` (líneas con
`Explanation`, totales, banderas). Cada forma de cálculo de FR-009 es una clase
`ICalculationRule` (`FixedAmountRule`, `PercentOfBaseRule`, `QuantityTimesUnitRule`,
`RangeTableRule`, `CompositeOfConceptsRule`) elegida por el enum
`CalculationKind` de la definición del concepto. El orden de evaluación se deriva
del grafo de dependencias (bases y conceptos compuestos), no de un campo de
prioridad; los ciclos se rechazan al guardar la definición (FR-028).

**Rationale**: la spec exige repetibilidad (FR-014) y explicación por línea
(FR-013): un motor puro se prueba con casos dorados en `Domain.Tests` sin base de
datos, y la explicación se construye en el mismo sitio donde se calcula. Cinco
formas fijas (decisión Q1 de la spec) permiten que cada una explique en sus propios
términos; un evaluador de expresiones no podría.

**Alternatives considered**: (a) calcular en el handler de Application con EF
—mezcla IO y aritmética, imposible de probar con cientos de casos rápidos—; (b)
evaluador de fórmulas (NCalc o similar) —descartado por la spec—; (c) `PayrollConcept`
legado con sus cuarenta atributos como definición —semántica opaca, sin vigencias,
sin explicación (H-3).

## D-02 — Conceptos con vigencia en una entidad nueva; el legado queda como referencia

**Decision**: entidad `PayrollConceptDefinition` (`PAY_ConceptDefinitions`), una fila
por **versión**: `Code` estable + `ValidFrom`/`ValidTo`; modificar crea una versión
nueva y cierra la anterior (FR-029). Trae naturaleza, forma de cálculo y sus
parámetros, bases que afecta, prestacional, repetición, topes, clases de empleado y
`Origin` (`Seed`, `Custom`, `TranslatedLegacy` con `LegacyConceptId`). Las cuentas
contables por concepto y centro de costo van en `PAY_ConceptDefinitionAccounts`
con FK a `ChartOfAccounts` (H-12). `PAY_PayrollConcepts` y `PAY_ConceptAccounts`
no se tocan ni se borran: se exponen de sólo lectura como «Catálogo heredado».

**Rationale**: las liquidaciones aprobadas deben seguir mostrando la definición con
la que se calcularon (FR-029): una fila por versión referenciada desde la línea de
liquidación lo garantiza sin copiar la definición dentro de cada línea. Separar del
legado evita heredar semántica que nadie recuerda (decisión Q2).

**Alternatives considered**: (a) versionar con tabla de historial aparte —dos tablas
para una sola idea—; (b) reutilizar `PayrollConcept` añadiendo columnas —40 atributos
legados conviviendo con los nuevos, y su índice único por `ConceptCode` impide
versiones—.

## D-03 — Parámetros legales con vigencia y tablas por rangos

**Decision**: `PayrollLegalParameter` (`PAY_LegalParameters`): `Code`, `Kind`
(`Amount`, `Percent`, `RangeTable`), `Value`, `ValidFrom`, `ValidTo`; las tablas
(retención en la fuente en UVT, fondo de solidaridad en múltiplos de SMMLV) tienen
filas hijas `PAY_LegalParameterRanges` (`FromValue`, `ToValue`, `Rate`,
`FixedValue`). Los códigos requeridos por el motor están en una constante de Domain
(`LegalParameterCodes`) y el motor **se niega a calcular** si alguno no tiene
vigencia a la fecha de fin del período (FR-011), nombrándolo.

**Rationale**: FR-010 prohíbe cualquier valor legal fijo; una tabla con vigencias
es la forma mínima que soporta «cambió el año, cambio un dato» (SC-003). Los rangos
en tabla hija evitan JSON opaco y se editan en pantalla.

**Alternatives considered**: (a) `SystemSetting` (clave-valor sin vigencia) —no
soporta vigencias ni tablas—; (b) `WithholdingParameter`/`AutoContributionParam`
legados —sin vigencia, semántica parcial—; se conservan como referencia.

## D-04 — Novedades en entidad nueva con historial por sustitución

**Decision**: `PayrollNovelty` (`PAY_Novelties`): período, empleado, versión de
concepto, cantidad o valor, fechas, días en el período y días trasladados,
observación, `Status` (`Active`, `Superseded`, `Cancelled`), `Origin` (`Manual`,
`Import`, `Recurring`, `Retroactive`, `LoanDeduction`), referencias de origen
(`RecurringNoveltyId`, `ImportBatchId`, `RetroactiveOfPeriodId`, `LoanPortfolioId`) y
`SupersedesNoveltyId`. **Corregir** crea una novedad nueva que apunta a la
anterior y la marca `Superseded` con motivo; **anular** marca `Cancelled` con
motivo. Nada se borra (FR-005). `PayrollRecurringNovelty`
(`PAY_RecurringNovelties`) genera una novedad por período al calcular (número de
cuota incluido). Los cambios de salario **reutilizan** `PAY_SalaryChanges` como
historial por fecha de efecto; `Employee.Salary` se mantiene como espejo del último
vigente.

**Rationale**: `PAY_PayrollEntries` legado mezcla campos de incapacidad con los de
novedad genérica y su PK compuesta no admite versiones; una entidad nueva con
sustitución encadenada da historial sin tabla de versiones y sin `Remove`.

**Alternatives considered**: (a) reutilizar `PayrollEntry` —forma equivocada (H-3)—;
(b) tabla `NoveltyVersions` —duplica la fila para cada versión, sin ganancia—;
(c) generar las recurrentes al crearlas para N períodos futuros —períodos que aún no
existen; se generan al calcular—.

## D-05 — Liquidación: una corrida nueva por cálculo, nunca se edita

**Decision**: `PayrollRun` (`PAY_PayrollRuns`): período, `Version` (1, 2, …),
`Status` (`Draft`, `Stale`, `Superseded`, `Approved`, `Reversed`), quién y cuándo
calculó y aprobó, totales, hash de insumos, referencia al comprobante contable y al de
reversión. `PayrollRunEmployee` (`PAY_PayrollRunEmployees`): días liquidados, tramos
de salario, totales, banderas de bloqueo. `PayrollRunLine`
(`PAY_PayrollRunLines`, namespace `Entities/Payroll/Transactions`): concepto
(versión), naturaleza, cantidad, base, factor o tramo, parámetro y vigencia,
novedad de origen, valor, explicación estructurada. Cada cálculo crea una corrida
**nueva** y marca la anterior en borrador como `Superseded`; los cambios de novedad,
salario, concepto o parámetro marcan la corrida en borrador como `Stale` (FR-016).
El cálculo entero va en **una transacción** (un `SaveChangesAsync`): o queda completa
o no deja rastro (FR-015).

**Rationale**: H-2 impide borrar líneas; una corrida por cálculo es además la forma
natural de «qué empleados cambiaron respecto del borrador anterior» (comparar dos
corridas) y de conservar evidencia de lo que se revisó. El hash de insumos hace
comprobable la repetibilidad (FR-014).

**Alternatives considered**: (a) actualizar el borrador in-place —requiere `Remove`
de líneas, prohibido—; (b) borrador en memoria/caché sin persistir —no sobrevive a la
sesión ni se puede revisar por otra persona—.

## D-06 — Concurrencia: lock distribuido por período más unicidad en base

**Decision**: el comando de cálculo toma `IDistributedLock` con clave
`payroll:run:{tenantPublicId}:{periodPublicId}` y TTL de cinco minutos; si no lo
obtiene responde `Payroll.RunInProgress`. Como segunda barrera, índice único
`(PayPeriodId, Version)` en `PAY_PayrollRuns`.

**Rationale**: H-4; el lock evita el trabajo duplicado y el índice hace imposible
que dos corridas del mismo número convivan aunque el lock expire.

**Alternatives considered**: (a) sólo índice único —la segunda petición trabaja un
minuto para fallar al final—; (b) `sp_getapplock`/`pg_advisory_lock` —dependen del
motor y del alcance de sesión de EF; el lock Redis ya existe y es agnóstico—.

## D-07 — Aprobación y contabilización en un solo acto

**Decision**: `ApprovePayrollRunCommand` valida (corrida `Draft` no `Stale`, ningún
bloqueo sin excepción autorizada, todo concepto con cuentas, período contable
abierto, segregación de funciones), genera el `AccountingDocument` tipo `NM` con sus
`JournalEntry` (una línea por concepto y centro de costo, débitos y créditos según la
naturaleza), cambia el período a `Approved`, congela novedades y corrida, y guarda
**todo en una transacción**. Si el asiento falla, no hay aprobación (FR-023). La
reversión (`ReversePayrollRunCommand`) genera el asiento reverso con referencia al
original, marca la corrida `Reversed` y devuelve el período a `Open` (FR-032).

**Rationale**: H-7 da el patrón; ejecutarlo dentro de la misma unidad de trabajo es
lo único que garantiza «no existe nómina aprobada sin comprobante». El tipo de
comprobante `NM` se siembra si no existe (semilla paramétrica).

**Alternatives considered**: (a) enviar `CreateDocumentCommand` por MediatR desde el
handler —dos `SaveChanges`, dos transacciones, la ventana de inconsistencia que
FR-023 prohíbe—; (b) contabilizar en un paso separado —permite nómina aprobada sin
asiento—.

## D-08 — Descuentos de cartera como novedades de origen «Cartera»

**Decision**: al calcular, el motor incorpora como novedades `LoanDeduction` los
descuentos por nómina del período que ya produjo Cartera
(`ProcessPayrollDeductionCommand`, H-13), sólo para efectos del neto y del
comprobante de pago; **no** generan asiento en la nómina porque Cartera ya lo hizo.
La línea lo dice en su explicación.

**Rationale**: evitar doble contabilización y a la vez que el empleado vea el
descuento en su comprobante. Es el mismo criterio de FR-013: cada línea dice de dónde
sale.

**Alternatives considered**: (a) que la nómina contabilice y Cartera deje de hacerlo
—cambia un módulo ajeno fuera de alcance—; (b) ignorar los descuentos de cartera —el
neto sería mentira—.

## D-09 — Redondeo, políticas y segregación en parámetros de la cooperativa

**Decision**: tres `SystemSetting` con `ModulePrefix = "PAY"`: `Payroll.Rounding`
(`Peso` por defecto), `Payroll.VariationThresholdPercent` (10 por defecto) y
`Payroll.AllowSameUserApproval` (`false`). La diferencia por redondeo se imputa a
un concepto de semilla `AJUSTE_REDONDEO` (FR-017).

**Rationale**: son decisiones de la cooperativa, no valores legales con vigencia;
`COR_SystemSettings` ya tiene pantalla y permisos (`Security.Parameters.*`).

**Alternatives considered**: columnas en `PayrollPlan` —el redondeo y la
segregación no son por plan—.

## D-10 — Semilla de conceptos, parámetros y tipo de comprobante

**Decision**: tres `IDataSeeder` paramétricos de alcance `Tenant`:
`PayrollConceptDefinitionsSeeder` (≈28 conceptos estándar con forma de cálculo,
bases y `Origin = Seed`, `ValidFrom` 2026-01-01), `PayrollLegalParametersSeeder`
(códigos requeridos **con vigencia 2026** y sus rangos: SMMLV, auxilio de transporte
y tope, UVT, tabla de retención, FSP, porcentajes de salud, pensión, ARL por clase,
parafiscales, provisiones, máximo de deducción, base del salario integral) y
`PayrollVoucherTypeSeeder` (tipo `NM` si falta). Idempotentes por `Code`/`ValidFrom`;
nunca actualizan lo existente. La semilla también se puede reaplicar desde la
pantalla (`POST …/seed`) con el mismo seeder.

**Rationale**: H-5; la semilla paramétrica corre al aprovisionar una cooperativa y al
arrancar, así que la primera nómina encuentra el catálogo (FR-038, SC-003). Los
valores legales concretos son **datos** de la semilla del año, no constantes del
motor: el motor sólo conoce los códigos.

**Alternatives considered**: migración de datos EF —mezcla DDL con datos (XII) y no
corre al aprovisionar—; JSON en `wwwroot` —no llega a las cooperativas existentes—.

## D-11 — Retención en la fuente: procedimiento 1 en el motor, 2 como dato del empleado

**Decision**: `RangeTableRule` sobre la base depurada calculada por el motor
(`WithholdingBaseBuilder`: devengos gravados − aportes obligatorios − deducciones
declaradas con sus topes − renta exenta del 25 % con su tope, todo desde parámetros)
convertida a UVT. Para `Employee.WithholdingProcedure = 2`, el motor aplica el
porcentaje vigente de `PAY_EmployeeWithholdingRates` y se niega si no hay vigencia
(FR-039). Las deducciones declaradas viven en `PAY_EmployeeTaxDeductions` con
vigencia.

**Rationale**: decisión Q3 de la spec; la depuración es parametrizable y la
explicación muestra cada paso.

**Alternatives considered**: usar `Employee.WithholdingTaxRate` legado —sin vigencia
semestral ni historial—.

## D-12 — Comprobante de pago: PDF real y envío manual con adjunto

**Decision**: `IPayslipPdfRenderer` (interfaz en Application) implementado en el
proyecto API reutilizando `PayslipReport` (QuestPDF ya vive ahí); el endpoint PDF
existente deja de devolver «not implemented». `EmailMessage` gana
`IReadOnlyList<EmailAttachment>? Attachments = null` y `SmtpEmailSender` los adjunta.
`IPayslipEmailDispatcher` (Application, patrón de `IInvitationEmailDispatcher`)
envía uno a uno, registra cada intento en `PAY_PayslipDeliveries` y nunca se dispara
solo (FR-026).

**Rationale**: H-8 y H-9; la implementación en la raíz de composición (API) respeta
el Principio II —Presentation depende de Application, nunca al revés— sin crear un
proyecto de infraestructura para un solo renderizador.

**Alternatives considered**: mover QuestPDF a `Storage` —arrastra los quince reportes
existentes—; enviar sin adjunto con enlace —el empleado no tiene sesión en el
sistema—.

## D-13 — Carga masiva: CSV con plantilla, validación completa antes de aplicar

**Decision**: `INoveltyFileParser` (Application) implementado en `Storage` con
CsvHelper (`;` como separador, UTF-8, plantilla descargable con encabezados en
español); `ImportNoveltiesCommand` valida **todas** las filas (mismas reglas que el
registro individual, más el formato) y aplica el lote sólo si ninguna falla,
devolviendo fila y causa de cada error (FR-031). Cada novedad importada lleva
`Origin = Import` e `ImportBatchId`.

**Rationale**: CSV es lo que exportan las hojas de cálculo de las cooperativas; la
validación total antes de aplicar es lo que la spec exige y evita lotes a medias.

**Alternatives considered**: XLSX (ClosedXML) —paquete nuevo por poca ganancia; queda
para después si lo piden—; aplicar filas válidas y reportar inválidas —lote a medias
que nadie sabe cómo quedó—.

## D-14 — Retiro del cálculo preliminar y ajuste de las pruebas de arquitectura

**Decision**: se eliminan `ProcessPayrollCommand`, `RegisterPayrollEntryCommand`,
los endpoints `POST /api/payroll/process/{id}` y `POST /api/payroll/entries`, y se
reescriben `summary`, `detail` y `payslip` sobre las tablas nuevas.
`PrincipioVIII_DualValidation` incorpora los namespaces
`IngenIA365ERP.Application.Payroll.Novelties`, `.Runs`, `.Concepts`,
`.LegalParameters`, `.Plans`, `.Payments`, `.Payslips`, `.EmployeeTax`.
`PrincipioXI_ContableImmutable` ya protege `Entities/Payroll/Transactions`, donde
entran `PayrollRunLine`, `PayrollRun` y `PayrollRunEmployee`. Un test de arquitectura
nuevo fija que ningún archivo de Domain/Payroll contenga un literal de porcentaje o
tope legal (FR-010): busca decimales con `m` fuera de los archivos de prueba y de la
semilla.

**Rationale**: H-3, H-10, H-2. Dejar el cálculo viejo alcanzable sería dejar dos
nóminas posibles.

**Alternatives considered**: mantener los endpoints viejos «por compatibilidad» —no
tienen consumidores: las dos pantallas son marcadores—.

## D-15 — Pantallas y cliente tipado

**Decision**: cinco pantallas en `Shared/Pages/Nomina/`: `Novedades.razor`
(reescrita), `Liquidacion.razor` (reescrita), `PlanesNomina.razor` (nueva),
`ParametrosLegales.razor` (nueva) y `Conceptos.razor` (reescrita sobre definiciones,
con pestaña «Catálogo heredado» y «Probar en seco»). Un cliente tipado `NominaClient`
en `Shared/Services/Nomina/` con el patrón de `ParametrosClient` (token desde
`CentralAuthClient`). Las pantallas usan `SfGrid`/`SfDialog` como los maestros,
`PersonSearchPicker` para elegir empleado, y componentes propios pequeños:
`SelectorDePeriodo`, `ExplicacionDeLinea`, `PanelDeAprobacion`. Cada pantalla nueva
entra al `ManualCatalogo` (feature del manual, 2026-09-04) con su guía.

**Rationale**: H-14; el prerender del servidor exige el cliente tipado; las
convenciones de grilla y diálogo ya están en la hoja de componentes.

**Alternatives considered**: una sola pantalla «Nómina» con pestañas —mezcla tres
roles (analista, responsable, administradora) y tres permisos en una URL—.

## D-16 — Estrategia de pruebas

**Decision**:
- `Domain.Tests/Payroll/Calculation/`: casos dorados del motor (los veinte empleados
  de SC-001 como fixtures: ingreso a mitad de período, incapacidad, cambio de salario,
  integral, retención P1 y P2, tope de deducciones, neto negativo, redondeo) y una
  prueba de repetibilidad (mismo input → mismo hash).
- `Application.Tests/Payroll/`: validadores y handlers con `TestApplicationDbContext`
  (InMemory) —registrar novedad, corregir, cancelar, calcular (lock y transacción con
  `IDistributedLock` sustituido), aprobar (bloqueos, segregación, asiento cuadrado),
  reversar, marcar pago, importar (lote inválido no aplica nada).
- `API.IntegrationTests/Payroll/`: recorrido completo por HTTP contra Testcontainers
  (período → novedades → calcular → aprobar → relación de pago → comprobante PDF),
  y permisos (sin permiso, 404 indistinguible).
- `Architecture.Tests`: VIII con los namespaces nuevos, XI vigente, y el test nuevo
  «sin valores legales fijos en Domain/Payroll».

**Rationale**: la spec pide precisión al peso y explicación; eso se prueba en el
motor puro con casos que la contadora pueda leer. Los handlers prueban reglas de
flujo, no aritmética.

**Alternatives considered**: sólo integración —lenta, y los casos dorados de cálculo
serían cientos—.
