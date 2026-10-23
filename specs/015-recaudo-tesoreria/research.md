# Research — 015 Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | **Rama**: `015-recaudo-tesoreria` | **Spec**: [spec.md](spec.md)

Fase 0 del plan. Cada decisión lleva **Decisión**, **Razón** y **Alternativas**. Ninguna queda abierta:
lo que depende de datos del dueño (P1 a P19 de la spec) queda como **pendiente** con su defecto, y nunca se inventa.
Las decisiones del dueño de `## Clarifications` (C1 = A, C2 = A, C3 = A, D2, el retiro de lo heredado después de T2, el
paralelo en QA, el retiro de los dobles, el mínimo completo de cheques, los servicios como cuentas por cobrar de
Comercial, la conciliación mínima en T1/T0, el cuadre provisional y el recorte R0/T0) y las cinco del 2026-10-23
(auditoría de las corridas con la lista de recibos, migraciones de R0/T0 congeladas el 10/11, conciliación única de
Tesorería, prefijos por enmienda de la constitución, desviación III aprobada) se toman como dadas y no se reabren.

Los nombres que este documento fija los recoge [decisiones-transversales.md](decisiones-transversales.md) §2, que manda
sobre cualquier otra redacción.

## Índice de decisiones

| # | Tema | Decisión corta |
|---|---|---|
| R-01 | Hechos del código | Lo que se reutiliza está construido; lo que falta, enumerado |
| R-02 | Módulos y carpetas | `Receipts` (código `REC`) y `Treasury` (código `TES`), carpetas ya fijadas por pruebas |
| R-03 | Prefijos de tabla | `RCD_` y `TES_`; lo heredado sigue en `TRS_` hasta su retiro |
| R-04 | Convivencia con lo heredado | Lo nuevo vive en subcarpetas y subrutas propias; lo heredado no se toca hasta T2 |
| R-05 | Frontera entre Recaudo y Tesorería | Dos módulos que se hablan por puertos síncronos en la misma unidad de trabajo |
| R-06 | El extracto bancario | Un solo almacén, de Tesorería; Recaudo identifica sobre él |
| R-07 | El recibo y la operación de caja | Operación «en preparación» con `PublicId` estable; el recibo nace al confirmar |
| R-08 | Llamada a los contratos | Dentro de la transacción de Recaudo, obligaciones antes que cuentas, un lote por contrato |
| R-09 | Gramática de claves | `REC:{recibo}:{aplicación}:{clase}`; derivadas `:reverso` y `:destino` |
| R-10 | Huella | Contenido de negocio; ni simulación, ni aprobador, ni instante |
| R-11 | Imputación de medios | Motor puro `ImputacionDeMedios`; porción por clase de medio |
| R-12 | Cerrojos | Orden fijo: Recaudo → contratos (Cartera, Captaciones) → numeración |
| R-13 | Numeración | `NumeradorDeRecaudo` y `NumeradorDeTesoreria` sobre `COR_Sequences`, al final |
| R-14 | Sesión y arqueo | Motor puro `ArqueoDeCaja`; valores sellados al abrir |
| R-15 | Bóveda y remesas | La bóveda es de Recaudo; el tránsito y la confirmación, de Tesorería |
| R-16 | Pagadurías | Recaudo consume `DescuentosPorPagaduria` v1 y v2; cada línea del giro es su unidad |
| R-17 | Procesos no interactivos | Unidad = la línea; `TransaccionExplicita` por línea con su clave |
| R-18 | Orden de giro y resultado | Tesorería consume v1 y v2 y responde `ResultadoDelGiro` v1 sin cambiarlo |
| R-19 | Momento del hecho de pago | Transferencia: al confirmar el banco; cheque: al emitir |
| R-20 | Archivo de pago | `FlatFileWriter` con el ámbito 3 renombrado; adjunto no borrable con SHA-256 |
| R-21 | Cuenta destino del beneficiario | `COR_PersonBankAccounts` con doble control y período de espera (E-06) |
| R-22 | Integración contable | Dos emisores, dos matrices `ACC_Receipts*` y `ACC_Treasury*`, el vínculo cuenta propia ↔ cuenta contable en Contabilidad |
| R-23 | Validación previa | La de la 013 por combinación de dimensiones; bloquea en pantalla, avisa en procesos |
| R-24 | SARLAFT básico | Parámetros con vigencia, registro de transacciones en efectivo propio, listas por `IListasRestrictivas` |
| R-25 | GMF | Parámetros propios de Tesorería y Recaudo con vigencia; aritmética pura común |
| R-26 | Participantes y dobles | Registro real; migración de datos que retira los dobles; el código de los dobles se borra |
| R-27 | Activación | Requisitos verificables; abre las filas del registro en el mismo guardado; cuadre provisional |
| R-28 | Go/no-go | Lista verificable de plataforma (`COR_GoLiveChecklists`) |
| R-29 | Contratos v2 (E-01, E-02) | La partida citada viaja en el ítem; v1 sigue admitido |
| R-30 | Alcance | `IAlcanceDeRecaudo` (oficina, punto, caja, pagaduría) e `IAlcanceDeTesoreria` (cuenta bancaria, convenio) |
| R-31 | Aprobaciones | El motor de la 012; aprobador presente en caja, diferido en Tesorería |
| R-32 | Auditoría | Módulos `Receipts` y `Treasury` en la cadena de sellos; lecturas sensibles con lista cerrada |
| R-33 | Precisión | Montos 18,2; tarifas 9,6 como fracción; conteos enteros |
| R-34 | Migraciones | Pares por entrega, aditivas; los de R0/T0 congelados al desplegarse en QA el 10/11 y pares nuevos después; la destructiva del retiro, después de T2 |
| R-35 | Pantallas | `Shared/Pages/Recaudo/*` y `Shared/Pages/Tesoreria/*` con `IndicadorDeCarga`; caja con atajos |
| R-36 | Informes y documentos | Centro de informes con `TablaExportable`; QuestPDF con código de verificación |
| R-37 | Pruebas | Casos dorados, arquitectura, e2e en los dos motores, la migración de las e2e de Cartera y Captaciones |
| R-38 | Calendario de construcción | Propuesta de fechas para llegar al ensayo del 23/11 |
| R-39 | Conciliación bancaria única | La de Tesorería reemplaza a la de Contabilidad (E-04 a la 009); las `ACC_Bank*` quedan sin uso |
| R-40 | Activación mínima | Requisitos básicos y registro real en el bloque 2; la completa en la fase 19 |

---

## R-01 · Hechos del código (verificados el 2026-10-22 en el worktree)

**Decisión**: el diseño se apoya sólo en lo que ya existe y nombra lo que falta.

| Pieza | Dónde está | Estado |
|---|---|---|
| Bandeja de mensajes, emisor único | `Application/Common/Integration/EmisorDeMensajes.cs`, `COR_IntegrationMessages*` | Construida (012). Módulos de origen `INV`, `CAR`, `TES`, `REC` ya declarados en `ModulosDeOrigen.cs` |
| Un consumidor por destino | `Common/Integration/ConsumidoresPorDestino.Validar()` desde `API/Program.cs`; prueba `UnSoloConsumidorPorDestino` | Construida. Destinos `Treasury` y `Receipts` ya existen en `IntegrationDestinations` y los consumen los dobles |
| Registro de participantes | `COR_IntegrationParticipants`, `COR_IntegrationParticipantAdoptions`, `IRegistroDeIntegracion`, `EscritorDelRegistroDeIntegracion` | Construido (013). Clases `Receipts = 1`, `Treasury = 4`, `Test = 5`; roles `ObligationsCollector`, `MessageDestination` |
| Contrato de obligaciones v1 | `Common/Integration/Lending/IContratoDeObligaciones.cs` | Construido. Cinco métodos; clave única `(CollectorCode, IdempotencyKey)` |
| Contrato de cuentas v1 | `Common/Integration/Lending/IContratoDeCuentas.cs` | Construido. Siete métodos; `RetirarAsync` con `CloseAccount` responde `Lending.Savings.DeliveryNotAvailable` |
| Mensajes que Recaudo y Tesorería reciben y emiten | `Common/Integration/Contracts/Lending/*` y `…/Lending/Deposits/*` | Construidos: `OrdenDeGiroEmitidaV1/V2`, `AnulacionDeOrdenDeGiroV1`, `ResultadoDelGiroV1`, `DescuentosPorPagaduriaV1/V2`, `EnvioDeDescuentosConfirmadoV1`, `ResultadoDeDescuentosV1/V2`, `PagoReversadoPorCarteraV1`, `ConsignacionReversadaPorCarteraV1` |
| Dobles de prueba | `Application/Lending/Integration/DoblesDePrueba/*`, `API/Endpoints/Lending/TestDoublesEndpoints.cs`, `Shared/Pages/Cartera/DoblesDePrueba.razor`, `Lending:TestDoubles:Enabled` | Construidos; se **retiran** (R-26) |
| Requisitos de activación | `Lending/Activation/RequisitosDeActivacion.cs` (150–160) y `Lending/Deposits/Activation/RequisitosDeActivacionDeCaptaciones.cs` (164–176) | Hoy admiten la clase `Test` fuera de producción; cambian (E-01 e, E-02 g) |
| Idempotencia | `Idempotency-Key`, `IOperacionIdempotente`, `IdempotencyBehavior`, `HuellaDeOperacion`, `COR_OperationKeys` | Construida (012) |
| Parámetros con vigencia | `COR_ParameterVersions`, `ILectorDeParametros`, `AddParameterVersionCommand`, `ReglasDeParametrosPorModulo` | Construidos. Prueba `LosParametrosSeLeenEnUnSoloSitio` |
| Aprobaciones | `IMotorDeAprobaciones`, `AprobadorPresente`, `IDesafiosDePresencia`, `SEC_PermissionAmountLimits` | Construidas (012) |
| Ejecución por cooperativa | `IEjecutorEnCooperativa`, `ITareaProgramada`, `ProgramadorDeTareas`, `COR_BackgroundLeases` | Construidas |
| Alertas | `IAlertas`, `TiposDeAlerta`, `COR_Alerts`, `COR_AlertTypes` | Construidas |
| Auditoría encadenada | `AuditoriaEncadenada`, `COR_AuditOutbox`, `COR_AuditChainHeads`, `COR_AuditAnchors` | Construida |
| Plantillas de importación | `Common/Imports/*` (`DefinicionDePlantilla`, `EjecutorDeImportacion`, `IComandoDeImportacion`) | Construidas |
| Archivos planos | `Common/BankFiles/FlatFileWriter`, `FlatFileReader`, `COR_BankFileFormats(Fields)`; `BankFileScope` 1–6; `COR_Banks.TransferCode` | Construidos; ámbito 3 `SupplierPayments` reservado sin uso |
| Medios de pago | `COR_PaymentMeans`, `COR_CashDenominations`, `COR_CardNetworks`, `COR_CardAcquirers`, `COR_CardTerminals`; `PaymentMeansClass` | Construidos (012) |
| Caja del punto de venta | `INV_PointsOfSale`, `INV_CashRegisters`, `INV_CashSessions`, `INV_CashCount*`, `INV_DayClose*` | Construida (012); **no se toca** (C1 = A) |
| Listas restrictivas | `Common/Compliance/IListasRestrictivas`, `COR_RestrictiveListEntries`, `COR_RestrictiveListLoads` | Construidas (013/014) |
| Calendario hábil | `Common/Calendar/ICalendarioHabil` | Construido |
| Contabilidad | `AccountingPoster`, `DestinoContabilidad` (enrutador `INV`, `CAR`), `ACC_InventoryPosting*`, `ACC_LendingPosting*`, `ModuloContable` (`TES` sí, `REC` no), `AccountingModules` (`Treasury = 16`, sin `Receipts`) | Construida; se amplía (E-04) |
| Cuentas bancarias del plan | `ChartOfAccount.BankId`, `BankAccountNumber`, `EsBancaria`; `GET /api/accounting/accounts/bank-accounts` | Construidas |
| Cuentas bancarias de una persona | Sólo campos sueltos (`Person.SupplierBank*`, `Employee.PayrollBank*`) | **No existe** la entidad: la crea E-06 (R-21) |
| Tesorería heredada | `TRS_Checks`, `TRS_Concepts`, `TRS_Invoices`; `App/Treasury/{Checks,Invoices,TreasuryConcepts}`; `/api/treasury/{checks,concepts,invoices,cash-flow}`; `/tesoreria/{cheques,conceptos,facturas,flujo-caja}`; marca «E3 (feature 009)» | Convive hasta T2 (C3 = A) |
| Recaudo | Nada. Las pruebas `LasCuentasSeTocanSoloPorElContrato`, `EmisionesDeProcesoPermitidas` y `ElCatalogoDeMensajesEsUnico` ya nombran `Application/Receipts`, `Domain/Entities/Receipts`, `Application/Treasury`, los tipos `REC` y `TES` | Por construir |

**Razón**: la 012, la 013 y la 014 dejaron construido todo lo transversal (bandeja, registro, contratos, aprobaciones,
alertas, archivos, auditoría). Esta feature es la primera que **consume** esas piezas en vez de crearlas.

**Alternativas**: reconstruir piezas propias (bandeja, numeración, aprobaciones): descartado por FR-003 y por las
pruebas que fijan un solo escritor de cada cosa.

## R-02 · Módulos y carpetas

**Decisión**: dos módulos de código con su espacio de nombres en inglés y sus identificadores en español, como la 012 y
la 013.

| Módulo | Código (`ModuloContable`, `ModulosDeOrigen`, prefijo de claves) | Carpetas |
|---|---|---|
| Recaudo | `REC` (nuevo en `ModuloContable`; ya está en `ModulosDeOrigen`) | `Domain/Entities/Receipts/*`, `Domain/Receipts/*` (motores), `Application/Receipts/*`, `Persistence/Configurations/Receipts`, `API/Endpoints/Receipts/*`, `Shared/Pages/Recaudo/*` |
| Tesorería | `TES` (ya existe en los dos) | `Domain/Entities/Treasury/<Área>/*`, `Domain/Treasury/*`, `Application/Treasury/<Área>/*`, `Persistence/Configurations/Treasury/<Área>`, `API/Endpoints/Treasury/*`, `Shared/Pages/Tesoreria/*` |

`AccountingModules` suma `Receipts = 128` (bandera de cuenta «habilitada para el módulo»); `Treasury = 16` ya existe.
Rutas `/api/receipts/*` y `/api/treasury/*`; pantallas `/recaudo/*` y `/tesoreria/*`. Permisos `Receipts.*` y
`Treasury.*`.

**Razón**: las pruebas de arquitectura ya esperan `Receipts` y `Treasury`; inventar `Collections` chocaría con
`Lending/Collections` (cobranza) y obligaría a reescribir esas pruebas.

**Alternativas**: un solo módulo «Caja y bancos» (descartado por D1: permisos, participantes y emisores distintos);
nombres en español en las carpetas (descartado: rompe el criterio de la 009–014).

## R-03 · Prefijos de tabla

**Decisión**: `RCD_` para Recaudo y `TES_` para Tesorería. Lo heredado de Tesorería sigue en `TRS_` hasta su retiro;
el lado contable en `ACC_Receipts*` y `ACC_Treasury*`; la plataforma en `COR_`. La constitución listaba sólo `TRS_` para
Tesorería: los dos prefijos entran por la **enmienda 2.0.1** de sus Estándares Técnicos Adicionales (2026-10-23, revisor
el dueño; hallazgo CO3 del analyze), que deja `TRS_` para lo heredado hasta su retiro.

**Razón**: el inventario confirma que `RCD_` y `TES_` no se usan (sólo aparece «TES_» en comentarios con los nombres
del sistema de origen de las tablas `TRS_`). Un prefijo distinto del heredado deja visible, tabla por tabla, qué se
retira en el corte final.

**Alternativas**: reutilizar `TRS_` (descartado: mezclaría lo nuevo con lo que se borra con guarda destructiva);
`REC_` (descartado: `REC` es el código de módulo y las claves `REC:` ya son texto de negocio; un prefijo distinto
evita confundir tabla y clave).

## R-04 · Convivencia con la Tesorería heredada hasta T2

**Decisión**:
- lo heredado **no cambia** hasta T2: entidades `Check`, `TreasuryConcept`, `TreasuryInvoice` en la raíz de
  `Domain/Entities/Treasury`, sus handlers en `Application/Treasury/{Checks,Invoices,TreasuryConcepts}`, sus rutas
  `/api/treasury/{checks,concepts,invoices,cash-flow}` y sus pantallas `/tesoreria/{cheques,conceptos,facturas,flujo-caja}`;
- lo nuevo vive en **subcarpetas** (`Treasury/BankAccounts`, `Treasury/Payments`, `Treasury/Checkbooks`,
  `Treasury/Statements`, `Treasury/Reconciliation`, `Treasury/Remittances`, `Treasury/Activation`) y **subrutas**
  distintas: `/api/treasury/bank-accounts`, `/payment-orders`, `/payment-batches`, `/checkbooks`, `/issued-checks`,
  `/statements`, `/reconciliations`, `/remittances`, `/activation`. El cheque girado nuevo se llama `IssuedCheck`
  (tabla `TES_IssuedChecks`) para no chocar con la clase heredada `Check`;
- el menú agrupa lo heredado bajo «Tesorería · heredada» hasta T2, con la leyenda «cuentas por pagar y cheques de
  proveedores hasta T2»; el grupo «Tesorería» nuevo no enlaza las pantallas heredadas;
- **FR-209**: los egresos que COOFLOPAL registre por lo heredado o por Contabilidad aparecen como «movimientos
  contables de la cuenta» en el saldo, la posición y la conciliación mínima, por la consulta de E-04
  (`IContabilidadParaTesoreria.MovimientosDeCuentaBancariaAsync`). Tesorería no lee `TRS_`;
- la marca «E3 (feature 009)» de lo heredado se queda: lo heredado no contabiliza y se retira, no se arregla.

**Razón**: el dueño decidió que COOFLOPAL sale con lo heredado para proveedores y que se retira con guarda después de
T2. Tocarlo ahora sería trabajo perdido y riesgo en producción.

**Alternativas**: mover lo heredado a `Treasury/Legacy` (descartado: cambia espacios de nombres en producción sin
beneficio); renombrar rutas heredadas (descartado: rompe la capacitación de COOFLOPAL).

## R-05 · Frontera entre Recaudo y Tesorería

**Decisión**: son dos módulos con datos propios. **Ninguno escribe ni lee las tablas del otro**: se hablan por puertos
síncronos en `Application/Common/Integration/{Receipts,Treasury}`, que corren en la unidad de trabajo de quien llama
(misma base, misma transacción), como los contratos de la 013.

Las columnas se leen «ofrece → usa»: el módulo que implementa el puerto y el que lo llama.

| Puerto | Lo ofrece | Lo usa | Para qué | Entrega |
|---|---|---|---|---|
| `ICuentasBancariasPropias` | Tesorería | Recaudo | cuentas de recaudo con alcance, banco y estado | R0 |
| `IMovimientosBancariosParaRecaudo` | Tesorería | Recaudo | abonos del extracto por identificar; marca de identificado | R0 |
| `IRemesasDeTesoreria` | Tesorería | Recaudo | registrar la remesa al despachar desde bóveda y saber si se confirmó | T0 |
| `IOrdenesDePagoDeTesoreria` | Tesorería | Recaudo | emitir la orden de pago de una devolución (saldo a favor, partida al consignante) y saber su estado | T0 |
| `IRecaudoParaConciliacion` | Recaudo | Tesorería | recibos de canal banco, remesas y partidas de una cuenta y período para cruzar | T0 |
| `IPartidasDesdeConciliacion` | Recaudo | Tesorería | convertir un abono sin registro en partida por identificar (FR-216) | T0 |
| `IResultadoDeOrdenesDeRecaudo` | Recaudo | Tesorería | informar a Recaudo el resultado de sus órdenes de pago (girada, rechazada) | T0 |

Un mensaje por la bandeja **no** se usa entre ellos: son el mismo despliegue y la misma base, y la orden de pago de
una devolución debe nacer en la misma transacción que la baja del saldo a favor.

**Razón**: FR-003 rige también entre los dos módulos (D1). Los puertos síncronos dan atomicidad sin transacciones
distribuidas, con el precedente del contrato de obligaciones (I-4 de la 013).

**Alternativas**: un solo `DbContext` lógico donde cada uno lee del otro (descartado: la prueba
`RecaudoYTesoreriaSeHablanPorSusPuertos` no podría fijar la frontera); mensajes por la bandeja (descartado: deja
estados intermedios visibles y la devolución sin orden).

## R-06 · Un solo almacén de extractos, de Tesorería

**Decisión**: el extracto y sus movimientos (`TES_BankStatements`, `TES_BankStatementLines`) son de **Tesorería**, la
dueña de las cuentas bancarias propias. Se cargan una vez, con la huella de FR-197, desde la pantalla de Recaudo
(«extractos de recaudo», diaria) o de Tesorería («conciliación», mensual); el comando es uno solo
(`LoadBankStatementCommand`) y su permiso `Treasury.Statements.Load` viene en los roles de analista de recaudo y de
conciliador. Cada abono queda disponible para que Recaudo lo identifique (`RCD_BankItemIdentifications`, por el
`PublicId` de la línea); la conciliación cruza las mismas líneas.

**Razón**: si cada módulo cargara su copia, el mismo movimiento tendría dos huellas y se identificaría o conciliaría
dos veces (FR-066, FR-197, US16-1). Con un solo almacén, «nada se cruza dos veces» se cumple por construcción.

**Alternativas**: el extracto en Recaudo (descartado: la conciliación de cuentas de pagos no es de Recaudo); dos
almacenes con deduplicación cruzada (descartado: frágil y sin dueño claro).

## R-07 · La operación de caja y el recibo

**Decisión**:
- el cajero trabaja sobre una **operación de caja** (`RCD_CashOperations`) que nace «en preparación» con un
  `PublicId` estable, la persona, las aplicaciones elegidas y sus simulaciones. No tiene efectos: no hay recibo, ni
  número, ni hecho. Sus estados son los de FR-189: `Preparing`, `Blocked` (lista vinculante), `Withheld` (lista no
  vinculante con retención, dinero en custodia de la sesión y su hecho), `Confirmed` (dio lugar a un recibo) y
  `Abandoned`;
- **confirmar** crea en una sola transacción el recibo (`RCD_Receipts`, con el **mismo** `PublicId` que la
  operación), sus medios, sus aplicaciones y porciones, llama a los contratos, numera y emite los hechos. Si un contrato
  rechaza un ítem o la distribución cambió, la transacción se revierte y la operación sigue en preparación con el
  motivo por ítem;
- los recibos de canal banco, pagaduría y contingencia no pasan por la operación de caja: nacen del comando que los
  aplica.

**Razón**: FR-042 exige «todo o nada» y «no se consume número»; FR-172 exige que reconfirmar tras «la distribución
cambió» use la misma clave. Con un `PublicId` estable antes del recibo, la clave de cada porción existe desde la
simulación y la repetición es idempotente.

**Alternativas**: crear el recibo en borrador y numerarlo al confirmar (descartado: el recibo es inmutable y un
borrador visible confunde la revisoría); no persistir la preparación (descartado: la retención por lista no vinculante
necesita un estado persistente con dinero en custodia).

## R-08 · Cómo Recaudo llama a los contratos

**Decisión**: dentro de `TransaccionExplicita` de Recaudo:
1. una sola llamada a `IContratoDeObligaciones.AplicarAsync` con **todas** las porciones de créditos del recibo
   (forma `Interactive` en caja, `NonInteractive` en lo demás);
2. después, una sola llamada a `IContratoDeCuentas.ConsignarAsync` con todas las porciones de cuentas;
3. los rechazos por ítem revierten la transacción entera en caja (FR-042) y se muestran; en los procesos no
   interactivos rige R-17;
4. un fallo técnico propaga la excepción y `TransaccionExplicita` reintenta todo (los contratos releen adentro).

El recaudador es `Recaudador("REC", ClaseDeRecaudador.Receipts)` en los dos contratos. La anulación y el cheque
devuelto llaman a `ReversarAsync` (obligaciones primero, cuentas después), una vez por porción, con la clave derivada.

**Razón**: FR-170 fija «obligaciones antes que cuentas» y el cerrojo de la 013/014 es «setup → persona → crédito →
cuenta»; un solo lote por contrato toma todos sus cerrojos en una sentencia ordenada y evita interbloqueos (SC-032).

**Alternativas**: una llamada por aplicación (descartado: varias pasadas de cerrojo y riesgo de orden distinto entre
dos cajas); llamar a Captaciones primero (descartado: contradice FR-170 y el orden del cerrojo).

## R-09 · Gramática de claves de Recaudo y Tesorería

**Decisión**:

| Operación | Clave | Dónde es única |
|---|---|---|
| Porción de una aplicación de un recibo (caja, banco, contingencia) | `REC:{recibo:N}:{aplicación:N}:{claseDeMedio}` (clase como número de `ClaseDeMedioDeRecaudo`) | `LND_LoanOperations` / `LND_MemberAccountOperations` `(CollectorCode, IdempotencyKey)` |
| Línea del giro de una pagaduría | `REC:{giro:N}:{deductionFileLinePublicId:N}` (la fijó la 014) | ídem |
| Reverso (anulación, cheque devuelto) | `{clave de la porción}:reverso` | ídem |
| Resolución de un saldo a favor de Cartera | `REC:{egreso u orden de pago:N}:{obligación:N}`; su aplicación en otra, `…:destino` | ídem |
| Retiro de ahorro en caja (R1) | `REC:{egreso:N}:{cuenta:N}` | ídem |
| Operación de pantalla de Recaudo y Tesorería | `Idempotency-Key` (Guid) | `COR_OperationKeys` |
| Mensaje emitido por Recaudo o Tesorería | `originEventKey` de la bandeja (`Confirmation:{…}`) | `COR_IntegrationMessages` |

`{aplicación}` es el `PublicId` de la aplicación creado al prepararla; `{recibo}` el de la operación de caja (R-07) o
del recibo no interactivo. Tesorería no llama a contratos con clave: sus resultados viajan por mensaje.

**Razón**: FR-006 y FR-188 piden una clave por porción; los contratos sólo admiten texto con el prefijo del módulo
(`ClavesDeRecaudador.EsDelRecaudador`).

**Alternativas**: una clave por aplicación (descartado: un cheque devuelto no podría reversar sólo su porción, US15-4).

## R-10 · Huella de una operación

**Decisión**: la huella (`HuellaDeOperacion.Calcular`) de un recibo se calcula sobre pagador, quien efectúa,
aplicaciones (producto, modalidad, valor, elección del deudor) y medios (clase, medio, valor, referencia); la de un
egreso, sobre la autorización y el valor; la de un lote, sobre la cuenta de origen, la forma y las órdenes; la de un
resultado de giro, sobre la orden, el resultado y el valor. Quedan **fuera** la simulación (`SimulationId`), el token
del aprobador, el instante y el usuario. Las claves no vencen (FR-172).

**Razón**: FR-172, y la misma regla que ya usan los contratos (excluyen `SimulationId`).

**Alternativas**: incluir el aprobador (descartado: reconfirmar tras aprobar sería otra operación).

## R-11 · Imputación de los medios a las aplicaciones

**Decisión**: motor puro `Domain/Receipts/Imputacion/ImputacionDeMedios`: recibe aplicaciones en el orden de la
pantalla y medios con su clase y valor, y devuelve porciones (aplicación × clase de medio × valor). Por defecto los
medios inmediatos (efectivo, transferencia, consignación, tarjetas, bono) cubren primero, en el orden de la pantalla, y
el cheque cubre lo restante (D35); la cooperativa puede cambiar el orden (parámetro `Recibo.OrdenDeImputacion`) y el
cajero puede fijarla a mano antes de confirmar. Valida: total de medios = total aplicado + vueltas; vueltas sólo en
efectivo; ninguna porción en cero. Caso dorado por cada regla.

**Razón**: FR-188 y US15-4 (el cheque devuelto reversa sólo sus porciones).

**Alternativas**: imputar a prorrata (descartado: complica la firmeza y el reverso del cheque sin beneficio).

## R-12 · Cerrojos en orden fijo

**Decisión** (detalle en [data-model.md](data-model.md) §0.5):
- **Recaudo**: `RCD_Setup` compartido → `RCD_CashSessions` de la sesión (exclusivo) → `RCD_Vaults` de la oficina si
  hay movimiento de bóveda → controles de pagador (`RCD_PayerControls`, por `Id` de persona, en orden) → filas de
  origen (saldo a favor, partida, línea de pagaduría, cheque recibido) por `Id` → **contratos** (Cartera: setup →
  persona → crédito; Captaciones: setup → persona → cuenta) → Tesorería por puerto (cuenta bancaria propia) →
  numeración.
- **Tesorería**: `TES_Setup` compartido → `TES_BankAccounts` de origen → `TES_Checkbooks` → `TES_PaymentBatches` →
  `TES_PaymentOrders` por `Id` → numeración.
- En los dos motores con la misma semántica: `SELECT … FOR UPDATE` (PostgreSQL) y `UPDLOCK, ROWLOCK, HOLDLOCK` (SQL
  Server), en una sentencia por nivel ordenada por `Id`, como `CerrojoDeCartera`.
- Una prueba de arquitectura (`LosCerrojosDeRecaudoYTesoreriaVanEnOrden`) fija que sólo `CerrojoDeRecaudo` y
  `CerrojoDeTesoreria` toman cerrojos y que ninguna operación de Recaudo llama a Captaciones antes que a Cartera.

- **Dirección Tesorería → Recaudo** (CH16): cuando un comando de Tesorería llama a `IResultadoDeOrdenesDeRecaudo` (resultado
  de una orden de Recaudo) o a `IPartidasDesdeConciliacion` (abono sin registro), primero llama a `TomarCerrojosAsync` del
  puerto, que toma los niveles de Recaudo (`RCD_Setup` compartido y las filas de origen de esas órdenes por `Id`), y sólo
  después toma `TES_Setup`, la cuenta, el lote, las órdenes o la conciliación. Así ninguna transacción toma una fila
  `RCD_` después de una `TES_` y el orden global Recaudo → Tesorería se respeta en las dos direcciones.

**Razón**: FR-007, FR-170, SC-032. Recaudo toma lo suyo **antes** de llamar a los contratos para que nunca espere a
Cartera con una fila suya tomada en otro orden: los contratos no toman filas `RCD_`.

**Alternativas**: cerrojo optimista con `RowVersion` (descartado: dos cajas cobrando al mismo crédito necesitan
esperar, no fallar; el contrato ya espera).

## R-13 · Numeración

**Decisión**: `NumeradorDeRecaudo` y `NumeradorDeTesoreria` sobre `COR_Sequences`, con la mecánica del `Numerador` de
la 012 (fila por tipo de documento y prefijo, `NextValue` bajo cerrojo), llamados **al final** de la confirmación. Se
amplía `SoloElNumeradorNumera`. Lo que no se confirma no consume número; los recibos de contingencia llevan **además**
el número del talonario.

**Razón**: FR-007 y FR-026 (sin huecos por concurrencia).

**Alternativas**: secuencias de la base (descartado: dejan huecos al revertir).

## R-14 · Sesión de caja y arqueo

**Decisión**: motor puro `Domain/Receipts/Arqueo/ArqueoDeCaja`: esperado por medio = base + recibos vigentes de la
sesión − anulaciones en la sesión ± movimientos de caja ± reclasificaciones − egresos (R1); contado según el método del
medio (denominaciones, total, lote de datáfono, referencias, relación de cheques); diferencia con tolerancia del medio
(catálogo de la 012). La sesión **sella al abrir** el modo de base, los topes, el arqueo ciego, las tolerancias y los
medios del punto (`RCD_CashSessions.SealedSettingsJson`), y no cambia aunque la caja cambie (US1-3). Estados de la
sesión: `Open`, `Closing` (contado registrado, ciego), `ClosedWithDifferencePending`, `Closed`; la sesión olvidada pasa
a `ForgottenOpen` al cruzar la hora de corte y la cierra el supervisor con arqueo diferido y testigo (FR-034). Un
cierre no se reabre: `RCD_CashSessionAdjustments` (FR-033).

**Razón**: FR-029 a FR-034; arqueo reproducible y probado con casos dorados.

**Alternativas**: reutilizar la sesión del punto de venta (`INV_CashSessions`) (descartado por C1 = A).

## R-15 · Bóveda y remesas

**Decisión**: la bóveda (o la caja principal en modo bóveda, FR-180) es de **Recaudo** (`RCD_Vaults`,
`RCD_VaultMovements` con denominaciones y doble custodia, `RCD_VaultDenominationBalances` como proyección que el cuadre
diario verifica). La **remesa** (bóveda ↔ banco) es un documento de **Tesorería** (`TES_CashRemittances`): Recaudo la
registra por `IRemesasDeTesoreria` en la misma transacción que el movimiento de bóveda (acta, sello de bolsa); Recaudo
emite `RemesaDespachada` (bóveda → efectivo en tránsito) y Tesorería emite `RemesaConfirmada` (tránsito → banco) al
cruzarla con el extracto en la conciliación mínima o al confirmarla a mano con soporte. Sin confirmar en
`Remesas.DiasParaConfirmar`, alerta.

**Razón**: FR-035, FR-105, FR-182. El efectivo es de la oficina; el banco y el tránsito, de Tesorería.

**Alternativas**: la remesa entera en Recaudo (descartado: Tesorería no podría conciliarla sin leer `RCD_`).

## R-16 · Pagadurías y libranza

**Decisión**:
- Recaudo es el consumidor del destino `Receipts` (`DestinoRecaudo`) y acepta `DescuentosPorPagaduria` v1 y v2; guarda
  el archivo recibido (`RCD_PayrollDeductionFiles` y sus líneas estructuradas) con su recibo `RCD_InboundMessages`;
- **confirmar el envío** (`ConfirmPayrollFileSentCommand`) emite `EnvioDeDescuentosConfirmado` v1 con canal, fecha,
  radicado y la huella del archivo;
- **cargar la respuesta** lee con `FlatFileReader` el formato de vuelta del convenio (ámbito 6), la plantilla genérica
  o la captura manual (FR-201), muestra la vista previa y emite `ResultadoDeDescuentos` v1 (pagaduría línea a línea) o
  v2 (por código de la pagaduría) según la versión del archivo; un rechazo de Cartera (`LineMismatch`) se muestra para
  corregir y emitir otro;
- **el giro** (`RegisterPaymasterRemittanceCommand` + `ApplyPaymasterRemittanceCommand`) toma el dinero de una línea
  de extracto identificada o de un recibo de caja, y aplica cada línea «descontado pendiente de giro» en su propia
  unidad, en el orden del convenio o del analista, con `ExternalPaymasterDeduction`, la fecha valor del descuento y la
  clave `REC:{giro:N}:{línea:N}`; lo no cubierto sigue pendiente; lo sobrante, saldo a favor de la pagaduría.

Recaudo **no** sabe qué línea está pendiente de giro leyendo `LND_`: lo sabe por el resultado que él mismo devolvió y
por la respuesta de Cartera al aplicar (`Lending.Payment.PaymasterReferenceInvalid` si ya no lo está).

**Razón**: FR-075 a FR-082, contratos de la 013 §11.6/§12.5–12.6 y de la 014 §5.4/§5.7, sin cambiarlos.

**Alternativas**: que Cartera aplique el giro (descartado: la 013 decidió que todo pago entra por el recaudador).

## R-17 · Procesos no interactivos: la línea es la unidad

**Decisión**: en extractos, giros de pagadurías y archivos de convenio, cada línea (o el recibo que la cubre) va en su
**propia** `TransaccionExplicita` con su clave. Un rechazo deja la línea en «no aplicada» con su motivo y el dinero
como partida, saldo de la pagaduría o saldo a favor del pagador; reprocesar repite la misma clave. La corrida queda
registrada (`RCD_ProcessingRuns`, con conteos, totales y huella) y se audita con un evento resumen por corrida que lista
cada recibo aplicado con su `PublicId` y su huella, y uno por línea rechazada (R-32). La aplicación la hace la persona que confirma, de forma sincrónica por tandas con indicador de avance;
nunca un proceso programado (FR-174).

**Razón**: FR-171, SC-010 (5.000 líneas en menos de 5 minutos), SC-030.

**Alternativas**: todo el archivo en una transacción (descartado: una línea rechazada tumbaría 4.999).

## R-18 · Órdenes de giro y resultado del giro

**Decisión**: Tesorería es el consumidor del destino `Treasury` (`DestinoTesoreria`) y acepta `OrdenDeGiroEmitida` v1
y v2, `AnulacionDeOrdenDeGiro` v1 y, con E-01 (c), `ArchivoDeDesembolsosGenerado` v1 y `ArchivoDeDesembolsosAnulado`
v1. Cada orden entra a la bandeja única `TES_PaymentOrders` (origen, clase, beneficiario, cuenta, valor, forma) con
recibo único por mensaje; una repetida no se duplica; una con valor ≤ 0 queda rechazada con su motivo en la bandeja
(evaluación negativa del consumo). El **resultado** (`TES_DisbursementResults`, hecho inmutable) lo registra una
persona y emite `ResultadoDelGiro` v1 **sin cambiarlo**, con las transiciones de FR-215 vigiladas por el motor puro
`Domain/Treasury/Resultados/TransicionesDelResultado`. Ningún proceso emite un resultado
(`EmisionesDeProcesoPermitidas` ya lo exige para `TES`).

**Razón**: los contratos de giro de la 013 y la 014 ya cubren lo que Tesorería necesita; la anulación, el regiro y el
cheque no cobrado caben en v1.

**Alternativas**: una v2 del resultado con causales tipificadas (descartado por ahora: el texto `Reason` basta y
cambiar el contrato exigiría que Cartera adopte otra versión).

## R-19 · Cuándo nace el hecho contable de un pago de Tesorería

**Decisión**:
- **Transferencia**: el hecho `PagoDeOrdenGirado` nace al registrarse «girada» (respuesta del banco, cruce del
  extracto confirmado o registro manual con soporte), con la fecha del débito que informa el banco. Marcar enviado no
  emite hecho: el archivo enviado y no confirmado es un compromiso de la posición, no un movimiento.
- **Cheque**: el hecho `ChequeGiradoEmitido` nace al **emitir** el cheque (sale de la cuenta en libros), y su
  anulación (`ChequeGiradoAnulado`, con causa «dañado antes de entregar» o «no cobrado») lo reversa. Al cierre del mes
  los emitidos no entregados se reclasifican (T1, FR-220).
- Un rechazo de transferencia antes de «girada» no tiene hecho (nunca hubo débito en libros).

**Razón**: FR-119 prohíbe «girada» al marcar enviado; la práctica contable del cheque es registrarlo al girarlo.
Así el puente de giro del origen (`DesembolsoPorGirar`, `DevolucionPorPagar`) se salda exactamente cuando el origen recibe
«girada» (FR-144).

**Alternativas**: hecho al marcar enviado con un puente «pagos en tránsito» (descartado: un rol más y una partida
conciliatoria más por cada lote, sin control adicional).

## R-20 · Archivo de pago

**Decisión**: el lote genera su archivo con `FlatFileWriter` usando el formato vigente del banco de la cuenta de origen
en el ámbito `BankFileScope.TreasuryPayments = 3` (hoy `SupplierPayments`, reservado y sin filas: se **renombra** sin
cambiar el número, FR-169), o el genérico (`CSV-GENERICO`), o la forma «portal del banco» (sin archivo). El archivo es
un adjunto no borrable (`TreasuryPaymentFile`) con SHA-256, subido **antes** de guardar la fila (molde de
`PAY_BankDisbursementFiles`); la descarga exige `Treasury.PaymentFiles.Download` y otra persona que la que preparó; la
respuesta del banco se lee con `FlatFileReader` en el ámbito nuevo `TreasuryPaymentResponse = 9` y cada línea se coteja
contra el archivo (FR-212). Ámbitos nuevos de lectura: `BankStatement = 7` (extracto) y, en R2,
`CollectionAgreementFile = 8` y `PaymentGatewayFile = 10`; en T2, `AcquirerSettlement = 11`.

**Razón**: un solo motor de archivos (D-42 de la 010); el ámbito 3 se reservó para esto.

**Alternativas**: un motor propio de Tesorería (descartado por D-42).

## R-21 · Cuenta destino del beneficiario (E-06)

**Decisión**: nueva tabla de plataforma `COR_PersonBankAccounts` (persona, banco, tipo, número, titular, certificación
adjunta, estado `PendingApproval` / `Active` / `Rejected` / `Retired`, aprobada por, `PayableFrom` = aprobación + espera
configurable). El alta o cambio la registra una persona y la aprueba **otra**; cada cambio es una fila nueva y la
anterior pasa a `Retired` (historia completa). Lectura y escritura sensibles auditadas. Tesorería valida cada orden
contra las cuentas **activas** del beneficiario (FR-109, FR-210): la cuenta del `PayeeV1` debe estar entre ellas; si
`PayableFrom` es futuro, la orden queda marcada; se alerta si la cuenta sirve a varios beneficiarios o coincide con la
de un usuario interno. La migración copia los campos sueltos de hoy (`Person.SupplierBank*`) como `Active` con
`Origin = Imported` y sin certificación (marca visible en el lote). El período de espera (`CuentasDestino.DiasDeEspera`)
**no se siembra** (D40, pendiente del dueño P4): sin valor, no hay espera, pero sigue la marca de «cambiada hace poco»
con `CuentasDestino.DiasDeMarca` (también sin valor sembrado).

**Razón**: no existe hoy una entidad de cuentas de la persona y FR-210 la exige con doble control.

**Alternativas**: validar contra los campos sueltos (descartado: sin doble control ni historia).

## R-22 · Integración contable: dos emisores, dos matrices

**Decisión** (enmienda E-04 a la 009, detalle en [contracts/contabilidad.md](contracts/contabilidad.md)):
- `DestinoContabilidad` suma dos ramas: `REC` a `PostReceiptsFactsCommand` / `PostReceiptsSummaryGroupCommand` y `TES`
  a `PostTreasuryFactsCommand`, siempre por `AccountingPoster`;
- dos matrices con el molde de `ACC_LendingPostingRules` (hecho × rol × dimensiones por código, con pesos de
  especificidad, vigencia, plantilla con filas sugeridas sin cuenta y completitud): `ACC_ReceiptsPostingRules` y
  `ACC_TreasuryPostingRules`, con sus recibos `ACC_ReceiptsPostings` / `ACC_TreasuryPostings` y sus tipos de
  comprobante `ACC_ReceiptsVoucherMappings` / `ACC_TreasuryVoucherMappings`;
- el **vínculo** cuenta bancaria propia ↔ cuenta contable bancaria vive en **Contabilidad** (`ACC_OwnBankAccountLinks`,
  único en los dos sentidos, con vigencia): el tesorero lo elige al registrar la cuenta y lo escribe Contabilidad por
  `IContabilidadParaTesoreria.VincularCuentaBancariaAsync` en la misma transacción. El rol `Banco` de las dos matrices
  se resuelve **por el vínculo**, no por una regla; así los hechos llevan la cuenta propia y nunca la contable
  (FR-102, `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas`);
- modo de paso: recibos de caja **por lotes resumidos** por día, punto y medio (D25); lo demás en línea;
- los comprobantes de Recaudo y Tesorería no se reversan desde Comprobantes
  (`Accounting.Document.ModuleOwned`, FR-145);
- conciliación de puentes: la de la 013 suma los hechos `REC` y `TES` en el lado del recaudador y del pagador.

**Razón**: un módulo que no conoce cuentas necesita que Contabilidad resuelva todo; separar las matrices mantiene el
molde de la 012 y la 013, y el vínculo en Contabilidad evita que Tesorería guarde un identificador contable.

**Alternativas**: una sola matriz «de caja y bancos» (descartado: dos emisores con códigos distintos y permisos de
mantenimiento distintos); el vínculo en `TES_BankAccounts` (descartado: Tesorería guardaría una cuenta contable).

## R-23 · Validación previa contable

**Decisión**: `IContabilidadParaRecaudo.EvaluarAsync` e `IContabilidadParaTesoreria.EvaluarAsync`, con la mecánica de la
013 (una pregunta por combinación de dimensiones, tiempo `Contabilidad.ValidacionPreviaSegundos`): con la política
`Contabilidad.FaltaDeRegla = Bloquear` (defecto) la pantalla no confirma y dice hecho, rol, combinación y quién debe
completarla; en los procesos no interactivos avisa y la línea queda para reproceso. Cambiar la política exige
`Receipts.Accounting.PolicyChange` / `Treasury.Accounting.PolicyChange` y motivo, queda auditado y alerta cada día hasta
volver a «Bloquear» (FR-221). Sin contabilidad iniciada o con el hecho en «no pasa», nada se bloquea.

**Razón**: FR-143, FR-221, US8-1, US8-5; mismo comportamiento que Cartera (A8).

**Alternativas**: validar al consumir (descartado: el error llegaría después de que el dinero se recibió).

## R-24 · SARLAFT básico (R0) y reporte de transacciones (R1)

**Decisión**:
- cuatro parámetros separados con vigencia y fuente, módulo `RCD` (FR-202): `Sarlaft.UmbralDeclaracionOrigenFondos`,
  `Sarlaft.UmbralTransaccionIndividual`, `Sarlaft.UmbralTransaccionesMultiples`, `Sarlaft.Fraccionamiento` (ventana en
  días, operaciones mínimas y fracción del umbral); más `Sarlaft.Reporte.Estado` por período (`Vigente`/`Suspendido`),
  `Sarlaft.Reporte.Alcance` (`SoloEfectivo`/`Todas`) y `Sarlaft.Reporte.ExigeAusencia`. Se siembran 5.000.000 y
  30.000.000 con la fuente (proyecto de circular de la Supersolidaria de diciembre de 2024 y respuesta de la UIAF de
  2025) y «pendiente de validar» (P7). La declaración no tiene valor sembrado propio: por defecto el umbral de
  declaración es el de transacción individual (pendiente de validar);
- registro propio de transacciones en efectivo `RCD_CashTransactions` (una fila por recibo o egreso con efectivo, por
  persona: pagador, quien efectúa, quien recibe), con acumulado del mes calendario; en R1 suma lo del punto de venta por
  la consulta de E-03 (`IEfectivoDelPuntoDeVenta`) sin copiar sus filas;
- declaración de origen de fondos `RCD_SourceOfFundsDeclarations` una vez por recibo, con su adjunto, que viaja en
  `SourceOfFundsAttachmentPublicId` a cada consignación que la exija (contrato de cuentas ya lo admite);
- listas: `IListasRestrictivas` con dos contextos nuevos (`ScreeningContext.Collection`, `.Disbursement`); vinculante
  bloquea siempre (`Receipts.Compliance.BindingListMatch`, `Treasury.Compliance.BindingListMatch`) y alerta de inmediato
  `Recaudo.ListaVinculante` / `Tesoreria.ListaVinculante` al oficial; no vinculante según
  `Sarlaft.ListasNoVinculantes.Efecto` (`Sigue` por defecto, o `Retiene`). La liberación de un falso positivo es la de
  la plataforma, por el oficial, con soporte;
- señales de fraccionamiento y acumulados por el motor puro `Domain/Receipts/Cumplimiento/SenalesDeEfectivo`, que
  alertan `Recaudo.Fraccionamiento` y `Recaudo.AcumuladoDeEfectivo`;
- el **reporte de transacciones** (R1) usa el motor genérico de layouts de la 013 (`Domain/Common/Layouts`) con un
  layout JSON embebido con vigencia y `verified = false` hasta cotejarlo (`Recaudo.Sarlaft.LayoutSinCotejar` en cada
  generación); generar, validar (bloqueantes y alertas), regenerar (la anterior `Superseded`) y marcar cargado con
  radicado, como el SICSES.

**Razón**: FR-094 a FR-099, FR-202 a FR-204; la norma del sector solidario no es la bancaria y está en transición: todo
es dato con vigencia.

**Alternativas**: valores nacionales en la base administrativa (descartado: los umbrales los decide el oficial de cada
cooperativa sobre la norma; y el usuario pidió parámetros con vigencia); reutilizar el parámetro de Captaciones
(descartado por C9: cada módulo conserva el suyo).

## R-25 · GMF

**Decisión**:
- parámetros propios con vigencia y fuente, módulo `TES` (`Gmf.Tarifa` como fracción, `Gmf.HechosGeneradoresDeEgresos`
  por clase de egreso u orden, `Gmf.ExencionDesembolsos` por clase de orden, beneficiario y forma, `Gmf.Redondeo`);
  Recaudo lee los suyos para los egresos de caja en R1 (`RCD`). Se siembra la tarifa de cuatro por mil con su fuente
  (ET art. 871) y «pendiente de validar» (P10); la tarifa cero es un valor válido;
- la aritmética (base × tarifa con redondeo) es pura y común (`Domain/Common/Taxation/CalculoDeGmf`); cada módulo
  conserva su evaluación de hechos generadores (Tesorería: `Domain/Treasury/Gmf/EvaluadorDeGmfDeEgresos`);
- T0: estimación al preparar el lote y hecho `GmfDePagoCausado` cuando el pago dispone de una cuenta contable para
  pagar a un tercero (FR-205); la exención de un desembolso se marca por operación (FR-206) y liga el cheque de primer
  beneficiario. El GMF real que cobra el banco se registra en Contabilidad hasta T2 y aparece en la conciliación mínima;
  desde T2, como nota bancaria;
- no se evalúa lo que el origen ya liquidó (retiros de Captaciones).

**Razón**: FR-100, FR-205, FR-206; Tesorería no puede leer los parámetros `LND` de Captaciones
(`Los_tipos_de_Nomina_y_Tesoreria_no_dependen_de_las_captaciones`, `LosParametrosSeLeenEnUnSoloSitio`).

**Alternativas**: un valor nacional `GMF_TARIFA` (descartado por ahora: Captaciones ya lo tiene como parámetro y el
dueño pidió parámetros; se anota como mejora posible para la feature tributaria).

## R-26 · Participantes, versiones y retiro de los dobles

**Decisión**:
- **Registro real** (`RegisterIntegrationParticipantCommand`, el de la 013) al activar, en el mismo guardado (FR-150):
  - `REC × ObligationsCollector` (clase `Receipts`) con `ProveedorDeObligaciones` v1 y `ProveedorDeCuentas` v1 (v2 al
    adoptar E-01/E-02 en R1);
  - `REC × MessageDestination × Receipts` con `PagoReversadoPorCartera` v1, `ConsignacionReversadaPorCartera` v1,
    `DescuentosPorPagaduria` v1 y v2;
  - `TES × MessageDestination × Treasury` (clase `Treasury`) con `OrdenDeGiroEmitida` v1 y v2,
    `AnulacionDeOrdenDeGiro` v1 y `ArchivoDeDesembolsosGenerado` v1 / `ArchivoDeDesembolsosAnulado` v1.
  Tesorería **no** se registra como recaudadora (D2): `ClaseDeRecaudador.Treasury` y los destinos que la nombran se
  conservan sin uso;
- **Retiro de los dobles** (FR-223) en tres piezas del mismo despliegue:
  1. migración de datos `RetiroDeLosDoblesDePrueba` (idempotente): toda fila vigente (`ValidTo` nulo) con
     `IsTestDouble = true` de `COR_IntegrationParticipants` se **cierra** con `ValidTo` = fecha del despliegue, como
     `EscritorDelRegistroDeIntegracion.RetirarAsync`; es la única columna escribible, porque `IntegrationParticipant` es
     `IHechoInmutable` (`IsEnabled` y `Reason` son de sólo inicio). Sus adopciones se cierran igual; el motivo «retirado
     al desplegar Recaudo y Tesorería reales» va a la auditoría; no se borra nada (historia). En producción no hay filas
     y no hace nada;
  2. el código se borra: `Application/Lending/Integration/DoblesDePrueba/*`, `TestDoublesEndpoints`, `DoblesDePrueba.razor`,
     la clave `Lending:TestDoubles:Enabled`, `RecaudadorDePrueba.CodigoDeTesoreria`, el permiso
     `Lending.TestDoubles.Operate` (se da de baja sin borrar sus asignaciones históricas);
  3. `EscritorDelRegistroDeIntegracion` rechaza `Class = Test` o `IsTestDouble = true` en **todo** ambiente
     (`Integration.Participant.TestDoublesRetired`); `RequisitosDeActivacion` y `RequisitosDeActivacionDeCaptaciones`
     dejan de admitir la clase `Test` fuera de producción (E-01 e, E-02 g). `ClavesDeRecaudador.TST` y
     `IntegrationParticipantClass.Test` quedan como valores reservados sin uso (no se renumera).
- **Orden** (FR-224): primero se reescriben las e2e de Cartera y Captaciones sobre los módulos reales
  (`CarteraE2E.RegistrarDoblesAsync` → `RecaudoYTesoreriaE2E.RegistrarYActivarAsync`) y quedan en verde en los dos
  motores; después se borra el código de los dobles en el mismo PR.
- `LosDoblesDePruebaNoSeRegistranEnProduccion` se reemplaza por `NoHayDoblesDePrueba` (ningún tipo implementa
  `IDestinoDeMensajes` para `Treasury` o `Receipts` salvo `DestinoTesoreria` y `DestinoRecaudo`).

**Razón**: FR-147, FR-148, FR-163, FR-223, FR-224, SC-027; el único consumidor por destino impide que convivan.

- **Bandera transitoria** del 04/11 al 19/11: mientras el código de los dobles exista, `DestinoRecaudo` y
  `DestinoTesoreria` se registran en DI sólo con `Lending:TestDoubles:Enabled = false` (T0141). Se pone en `false` en DEV
  para el hito del 04/11 (T0184), en QA para el paralelo del 10/11 (T0271) y en la fixture de las e2e antes de la puerta
  (T0300a); desaparece con el código de los dobles el 19/11 (T0307). Una cooperativa que todavía tenga la fila del doble
  `TES` vigente la ve cerrada por la activación (mínima o completa) antes de registrar la real (TR42).

**Alternativas**: dejar los dobles detrás de una bandera **como estado final** (descartado por el dueño: la bandera sólo
vive del 04/11 al 19/11); borrar las filas (descartado: Principio VII y la historia de QA).

## R-27 · Activación de Recaudo y Tesorería y cuadre provisional

**Decisión**: `ActivateReceiptsCommand` y `ActivateTreasuryCommand` (fila `RCD_Activations` / `TES_Activations`), cada uno
con su lista de requisitos verificables (`RequisitosDeActivacionDeRecaudo`, `…DeTesoreria`): puntos, cajas, cajeros y
supervisor con permisos (FR-176), medios, tipos de documento; cuentas bancarias vinculadas, formatos o la forma de
FR-213, chequera si ofrece cheques; con Contabilidad iniciada, matriz completa de los hechos que no estén en «no pasa».
El guardado de la activación abre la vigencia de las filas del registro (R-26) y, desde ahí, los mensajes pendientes en
la bandeja se entregan. **Cuadre** (`RCD_OpeningSquares`, `TES_OpeningSquares`): por cuenta (caja por oficina,
bóvedas, bancos, partidas, cheques en tránsito) contra el saldo contable de Contabilidad a la fecha de corte
(`SaldoContableAsync` de E-04); `Definitive` con diferencia cero o acta aceptada; `Provisional` con aprobación de
`Receipts.Activation.ProvisionalSquare` / `Treasury.Activation.ProvisionalSquare`, motivo y fecha límite; alerta diaria
(`Recaudo.CuadreProvisionalPendiente`, `Tesoreria.CuadreProvisionalPendiente`) que escala al vencer, y la **guarda de
cierre** en Contabilidad (`Accounting.Period.OpeningSquarePending`) que niega el cierre del mes de la activación sin el
definitivo (E-04, FR-228). El definitivo cierra la alerta y queda enlazado al provisional.

La activación nace **mínima** en el bloque 2 (R-40) y esta decisión la completa en la fase 19 sobre el mismo comando.

**Razón**: FR-150, FR-157 a FR-159, FR-228, US13, US24.

**Alternativas**: activación sin cuadre (descartado: SC-021).

## R-28 · Lista de go/no-go

**Decisión**: pieza de plataforma `COR_GoLiveChecklists` y `COR_GoLiveChecklistItems`: una lista por cooperativa y
salida, con condiciones **automáticas** (las aporta cada módulo con `IVerificacionDeSalida`: completitud de la matriz,
recibo de prueba por medio, punto y concepto en QA, giro por forma y cuenta, segundo factor inscrito de cada aprobador y
supervisor, roles y alcances, requisitos de activación de Cartera y Captaciones) y **manuales** (cronograma del cambio,
escenarios de no-go parcial con su plan de reversa, decisión). Una condición en rojo no se oculta; seguir exige la
aprobación registrada del dueño (`Platform.GoLive.Override`, con motivo). La activación en producción muestra la lista.

**Razón**: FR-227, US24-1, US24-3; la lista sirve también a Cartera y Captaciones.

**Alternativas**: una hoja de cálculo fuera del sistema (descartado: «verificable en el sistema»).

## R-29 · Versión 2 de los contratos (E-01, E-02)

**Decisión** (detalle en [contracts/contratos-v2.md](contracts/contratos-v2.md)):
- `ProveedorDeObligaciones` **v2**: `PagoAAplicar` suma `BankItem` (`{ bankItemPublicId, movementDate,
  ownBankAccountPublicId }`). Con él, Cartera exime la ventana de fecha valor (`Pagos.DiasMaximosFechaValor`) siempre
  que `ValueDate = movementDate`, sin eximir el corte de la carga ni el permiso con monto de un mes cerrado; guarda la
  partida en `LND_LoanOperations.BankItemPublicId`. La v1 sigue admitida y la activación de Cartera sigue aceptando un
  recaudador que sólo adoptó la v1;
- `ProveedorDeCuentas` **v2**: el mismo `BankItem` en `ConsignacionAAplicar`, más la exención de fecha valor de la
  consignación que cita `DeductionFileLinePublicId` (como ya la tiene Cartera);
- Recaudo adopta v1 en R0 y v2 en R1; mientras una cooperativa tenga sólo v1, la identificación tardía aplica con la
  fecha más antigua admitida, el recibo lo dice y se alerta al módulo (`Recaudo.FechaValorTardia`) para que corrija la
  mora indebida por condonación (FR-071);
- `IContratoDeObligaciones.VersionDelContrato` pasa a `2` y `ContratosDeIntegracion.VersionesDelContratoDeCuentas` a
  `[1, 2]`; una migración nueva y aditiva de la 013/014 (`CarteraYCaptacionesConPartidaCitada`, R1) suma las columnas.

**Razón**: FR-164 (a), FR-165 (a); C8. Los contratos de la 013 ya se congelaron (T0802): todo cambio es versión y
migración nuevas.

**Alternativas**: una ventana más larga en el parámetro (descartado: debilita el control para todos los pagos).

## R-30 · Alcance

**Decisión**: `IAlcanceDeRecaudo` (oficinas, puntos, cajas, pagadurías) e `IAlcanceDeTesoreria` (cuentas bancarias
propias, convenios), con implementaciones en la API (`AlcanceDeRecaudoDeLaPeticion`, `AlcanceDeTesoreriaDeLaPeticion`)
sobre `UserBranchScope` y tablas propias (`RCD_UserCollectionPointScopes`, `RCD_UserCashBoxScopes`,
`RCD_UserPaymasterScopes`, `TES_UserBankAccountScopes`). Falla cerrado y responde `Generic.NotFound` igual que ante lo
inexistente (FR-011, SC-018). Los recibos de banco, convenio y pagaduría, las partidas y los saldos a favor toman el
alcance de la cuenta, el convenio o la pagaduría (FR-175). Un cajero cobra a personas de cualquier oficina salvo que
`Caja.CobroSoloDeSuOficina` lo restrinja (D36).

**Razón**: molde de `IAlcanceDeCartera` e `IAlcanceDeInventario`.

**Alternativas**: alcance por oficina sólo (descartado: un supervisor de un punto vería todos los de su oficina).

## R-31 · Aprobaciones y segregación

**Decisión**: el motor de la 012 (`IMotorDeAprobaciones`, límites en `SEC_PermissionAmountLimits`):
- **caja**: aprobador **presente** con segundo factor (`AprobadorPresente`, `IDesafiosDePresencia`) para anulación en
  la sesión, diferencias sobre la tolerancia, reclasificación de medios, fecha valor anterior, retiros sobre la
  atribución (R1). Tras aprobar, la operación se repite con la misma clave;
- **Tesorería**: aprobación **diferida** multinivel por monto para lotes, traslados (T1), órdenes manuales (T1), usos del
  fondo de liquidez y cambios de cuenta destino; aprobar no mueve dinero, sólo deja el lote `Approved`;
- **segregación por persona** (`SEC_Users.Id` vía la persona, nunca el entero del token, `LaSegregacionNoUsaUserIdDelToken`):
  quien registra no aprueba lo suyo; quien prepara un lote no lo aprueba ni descarga su archivo; quien prepara o
  aprueba pagos de una cuenta en el período no la concilia (FR-217); que una tercera persona registre el resultado es
  configurable (`Lotes.TerceraPersonaParaResultado`, apagado por defecto).

**Razón**: FR-012, FR-013; D18, D19.

**Alternativas**: un motor propio (descartado: duplica la 012).

## R-32 · Auditoría

**Decisión**: módulos de auditoría `Receipts` y `Treasury` en la cadena de sellos (`COR_AuditOutbox` dentro de la
transacción, `AuditOutboxForwarder` a Mongo), con la conservación de FR-178 (`Conservacion.Anios`, diez por defecto,
nunca menos de cinco). Eventos por operación con antes y después; lecturas sensibles con una lista cerrada:
`Receipts.Person.PayablesRead` (lo que puede pagar una persona), `Treasury.BankBalances.Read`,
`Core.PersonBankAccount.Read/Changed`, `Receipts.Sarlaft.ReportRead`, exportaciones. Las corridas no interactivas
(extractos, giros de pagaduría, `AplicacionNoInteractiva`) se auditan con **un evento resumen por corrida**
(`Receipts.ProcessingRun.Summary`, por `COR_AuditOutbox` en la cadena de sellos) que lleva la **lista de cada recibo
aplicado** (`receipts[] { publicId, sha256 }`, la huella `HuellaDeRecibo` del recibo tal como quedó) y uno por línea
rechazada. Es verificable recibo por recibo: recalcular la huella del recibo y compararla con la del evento sellado; la
lista cubre exactamente los recibos de la corrida, incluidos los de reproceso (decisión del dueño del 2026-10-23, CO1).

**Razón**: Principio X (cada operación monetaria queda en la cadena de sellos), FR-014, SC-019, sin saturar la auditoría
con 5.000 eventos por giro: un evento sellado con 5.000 huellas da la misma verificación.

**Alternativas**: auditar cada línea aplicada con su evento (descartado: volumen sin verificación adicional); un evento
resumen sin la lista (la desviación X que traía la 013; descartado por el dueño: no permite verificar recibo por recibo).

## R-33 · Precisión

**Decisión**: alias `PrecisionDeRecaudo` y `PrecisionDeTesoreria`: monto `decimal(18,2)`; tarifa y fracción
`decimal(9,6)`; cantidades de denominación `int`; valor facial de denominación `decimal(18,2)`. Nada en coma flotante
(`LasCantidadesYMontosDeCajaTienenSuPrecision`). El peso es la unidad mínima: el redondeo del GMF lo fija el parámetro.

**Razón**: misma convención que la 012 y la 013.

**Alternativas**: ninguna razonable.

## R-34 · Migraciones

**Decisión**: pares PostgreSQL y SQL Server con `tools/scripts/add-migration.ps1` y paridad verificada por
`check-migration-parity.ps1` (`Feature004_MigrationParity`). Todas aditivas salvo la del retiro heredado:

| Entrega | Migraciones (en orden) |
|---|---|
| R0 + T0 (congeladas al 10/11) | `PlataformaParaRecaudoYTesoreria`, `RecaudoNucleo`, `TesoreriaNucleo`, `IntegracionContableDeRecaudoYTesoreria` |
| R0 + T0 (nuevas después del 10/11) | `TesoreriaConciliacionMinima`, `RecaudoYTesoreriaPuestaEnMarcha`, `RetiroDeLosDoblesDePrueba` (datos) y cualquier cambio posterior de R0/T0 |
| R1 | `RecaudoCompleto`, `CarteraYCaptacionesConPartidaCitada` (E-01/E-02) |
| T1 | `TesoreriaCompleta` |
| R2 | `RecaudoCanales` |
| T2 | `TesoreriaAmpliada` |
| Después de T2 | `RetiroDeLaTesoreriaHeredada` (destructiva con guarda, commit propio) |

Los pares de R0/T0 se regeneran mientras se construyen **hasta el despliegue en QA del 10/11** (T0271; la base de DEV
del 04/11 se recrea) y ahí se **congelan**, con sus huellas en `LasMigracionesCongeladasNoSeRegeneran` (decisión del
dueño del 2026-10-23, I3). Desde ese día no se regenera nada: cada cambio posterior de R0/T0 es un par nuevo, aditivo y
con su huella al cierre (T0337).

**Razón**: Principio XII y el precedente de la 013 (T0802); QA guarda desde el 10/11 los datos del paralelo y una
migración regenerada no se aplicaría sobre ellos.

**Alternativas**: congelar al promover a producción (lo que decía este documento hasta el 2026-10-23; descartado: QA ya
tendría aplicada una versión distinta de la misma migración).

**Alternativas** (de la división por entrega): una sola migración por feature (descartado: R1 y T2 salen meses después).

## R-35 · Pantallas

**Decisión**: `Shared/Pages/Recaudo/*` (`/recaudo/*`) y las nuevas de `Shared/Pages/Tesoreria/*` (`/tesoreria/*`), con
`IndicadorDeCarga` y `EstadoDeCarga` por zona; `Recaudo` y `Tesoreria` entran a `ModulosMigrados` de
`LasPantallasDicenQueEstanCargando`. La **caja** es una pantalla de tres pasos (persona → qué paga → medios) con atajos
de teclado (`F2` buscar, `F4` preseleccionar lo exigible en R1, `F8` medios, `F9` confirmar, `Esc` cancelar), búsqueda
que acepta el lector, y simulación por ítem con su aviso; funciona en Web y en la aplicación instalable (MAUI). El
cliente HTTP manda `Idempotency-Key` estable por operación de caja (`ClaveDeOperacion`). Las pantallas no ramifican por
el texto de un error (`LasPantallasNoRamificanPorElTextoDeUnError`): ramifican por código.

**Razón**: FR-018, FR-162, SC-003.

**Alternativas**: reutilizar la pantalla POS (descartado por C1 = A).

## R-36 · Informes y documentos

**Decisión**: vistas del centro de informes `/api/reports/receipts/{vista}` y `/api/reports/treasury/{vista}` como
`TablaExportable` (json, xlsx, pdf), con `Receipts.Reports.View/Export` y `Treasury.Reports.View/Export`, auditadas
(FR-154). Documentos con número y código de verificación por QuestPDF (`DocumentosDeRecaudoReport`,
`DocumentosDeTesoreriaReport`): recibo (tirilla y carta, «copia» al reimprimir), anulación, informe de cierre de sesión y
del día, egreso (R1), comprobante de pago (T1), informe de conciliación. Con pagador tercero, el recibo muestra sólo el
producto enmascarado y el valor (FR-177).

**Razón**: FR-152 a FR-156.

**Alternativas**: informes propios fuera del centro (descartado: se pierde el permiso de exportar y la auditoría común).

## R-37 · Pruebas

**Decisión**:
- **Casos dorados JSON** en `tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/` y `…/Treasury/Casos/`: imputación de
  medios (≈10), arqueo (≈12), señales de efectivo (≈8), huella del movimiento bancario (≈6), cruce de la conciliación
  mínima (≈10), transiciones del resultado (≈8), evaluador de GMF (≈8), armado de lotes y pendientes (≈6);
- **Application**: cada comando con su validador; idempotencia (misma y otra huella); todo o nada con rechazo inducido
  en el tercer ítem; «la distribución cambió»; procesos no interactivos con líneas rechazadas;
- **Arquitectura**: las dieciséis nuevas y las catorce ampliadas que enumera plan.md («Testing»), entre ellas
  `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras` y `NadieEscribeLaConciliacionBancariaDeContabilidad`;
- **e2e** (Testcontainers, PostgreSQL y SQL Server): colección «Recaudo y Tesorería e2e» con cooperativas aisladas para
  la activación; los recorridos de SC-002; la **migración** de las e2e de Cartera y Captaciones a los módulos reales
  (FR-224); la regla del dueño de correr las e2e contra base de datos al final de las fases se respeta: se escriben
  durante la construcción y se corren juntas al cierre de R0/T0, con dos excepciones declaradas: la e2e de humo de la
  caja al cierre del bloque 2 (T0183c) y la puerta de FR-224 (T0301);
- **Rendimiento** con `RUN_PERF_TESTS=1`: SC-010, SC-011, SC-023, SC-031, SC-032.

**Razón**: Principios II y XI; el precedente de la 013.

**Alternativas**: pruebas de navegador (no hay en el repositorio; la caja y el interop de impresión se prueban a mano).

## R-38 · Calendario de construcción (propuesta)

**Decisión**: la spec deja las fechas de construcción al plan. Propuesta para llegar al ensayo general del 23/11 con R0
y T0 completas, y al paralelo del 11/11 con lo que haya:

| Fechas | Qué | Hito |
|---|---|---|
| 22/10–27/10 | Fase 1: plataforma (E-06, ámbitos, contextos de listas, go/no-go), esqueleto de los dos módulos, permisos y roles, migraciones base, puertos internos | — |
| 28/10–04/11 | Fase 2: R0 caja (puntos, cajas, sesión, arqueo, bóveda), operación de caja y recibo por los dos contratos, anulación; T0 cuentas bancarias, bandeja de órdenes, lotes por archivo y portal, aprobación, resultado y regiro; activación mínima (R-40) y e2e de humo de la caja | 04/11: un recibo y un giro de punta a punta en DEV |
| 05/11–10/11 | Fase 3: extractos y partidas, pagaduría completa, cheque recibido y devuelto, chequera mínima y cheques, remesas, hechos y matrices, validación previa, SARLAFT básico | 10/11: versión al paralelo en QA (caja, recibo, extracto, giro); migraciones de R0/T0 congeladas |
| 11/11–19/11 | Fase 4: conciliación mínima, puesta en marcha y plantillas, activación con cuadre provisional, go/no-go, guarda de cierre, informes R0/T0, manuales, migración de las e2e de Cartera y Captaciones y retiro de los dobles | 19/11: R0 y T0 completas en QA |
| 20/11–22/11 | Fase 5: e2e en los dos motores, rendimiento, huellas de las migraciones nuevas posteriores al 10/11 | 22/11: todo con huella |
| 23/11–25/11 | Ensayo general en QA (SC-002) | go/no-go del ensayo |
| 26/11–27/11 | Correcciones del ensayo y despliegue a producción (respaldos por base) | 27/11: R0 y T0 en producción |
| 28/11 | Go/no-go y carga definitiva | — |
| 01/12 | Activación de Recaudo y Tesorería, después Cartera y Aportes | salida en vivo |

R1/T1 siguen a la salida sin fecha fija, con una restricción: el generador del reporte de transacciones (R1, FR-096)
debe estar en producción **antes del primer vencimiento de enero de 2027** si la suspensión no se prorroga.

**Razón**: el calendario de la spec es inamovible; las fases agrupan lo que tiene dependencia técnica.

**Alternativas**: construir R1 antes de la salida (descartado por el dueño: recorte mínimo). Si un hito se atrasa, rigen
las líneas de recorte de plan.md.

## R-39 · Conciliación bancaria única, de Tesorería

**Decisión** (decisión del dueño del 2026-10-23, I7): la conciliación de Tesorería (FR-216) **reemplaza** a la de
Contabilidad (009, FR-055 a FR-060), como parte de la enmienda E-04: un solo extracto (R-06) y una sola conciliación por
cuenta y mes. En el código:
- `ACC_BankReconciliations`, `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps` (entidades `BankReconciliation`,
  `BankStatementLine`, `BankStatementColumnMap` en `Domain/Entities/Accounting`, configuradas en
  `Persistence/Configurations/Accounting/SatelliteConfigurations.cs`) quedan **sin uso**: ningún comando las escribe
  (`NadieEscribeLaConciliacionBancariaDeContabilidad`). No se borran en esta feature; su retiro, si se hace, es una
  migración destructiva aparte (pendiente, con guarda, respaldo y segundo revisor);
- `ReopenPeriodCommandHandler` (`Application/Accounting/Periods/PeriodCommands.cs`), que hoy marca `Outdated` las
  `ACC_BankReconciliations` cerradas del período, pasa a llamar a `IConciliacionBancariaParaContabilidad` (Tesorería
  ofrece, Contabilidad usa), que marca `Outdated` las `TES_Reconciliations` cerradas de ese mes; el evento
  `Accounting.Period.Reopened` conserva `outdatedReconciliations`;
- la marca «cuenta bancaria» de la cuenta contable (FR-055) sigue en Contabilidad: es la que exige el vínculo
  `ACC_OwnBankAccountLinks`; el banco y el número viven en `TES_BankAccounts`.

**Razón**: dos conciliaciones del mismo banco divergirían; el extracto ya es único (R-06).

**Alternativas**: conservar las dos con sincronización (descartado: dos verdades); que la 009 concilie sobre el extracto de
Tesorería (descartado: Tesorería es dueña de las cuentas, los pagos y los cheques que se cruzan).

## R-40 · Activación mínima en el bloque 2

**Decisión** (hallazgo I2 del analyze): `ActivateReceiptsCommand` y `ActivateTreasuryCommand` nacen en el bloque 2
(T0183a, T0183b), antes del hito del 04/11, con **requisitos básicos** (Recaudo: punto, caja, cajero, medios, tipos de
documento; Tesorería: una cuenta bancaria vinculada y un formato o la forma portal del banco) y el **registro real** de
`REC` (recaudador y destino) y `TES` (destino) en el mismo guardado, con `RCD_Activations` y `TES_Activations` (sin cuadre:
`OpeningSquareId` nulo). Si la cooperativa tiene vigente la fila del doble `TES`, la activación la cierra antes por
`EscritorDelRegistroDeIntegracion.RetirarAsync` (`ValidTo`), con el motivo en la auditoría. Los hitos del 04/11 (DEV) y del
10/11 (QA) la usan en vez del registro a mano. La fase 19 completa el mismo comando: cuadre definitivo o provisional,
matriz completa con contabilidad iniciada, supervisor con los permisos de FR-176, chequera si ofrece cheques, verificación
de la hora de corte (FR-185).

**Razón**: el hito del 04/11 y el paralelo del 11/11 necesitan los participantes reales registrados; registrarlos a mano en
`/admin/participantes-de-integracion` no prueba el camino que se usará el 01/12.

**Alternativas**: registro a mano hasta la fase 19 (descartado: dos caminos al mismo estado).

---

## Pendientes del dueño que tocan el diseño (no se inventan)

| Pendiente | Qué rige mientras tanto | Bloquea |
|---|---|---|
| P1 bancos y estructuras | formato genérico (`CSV-GENERICO`) o forma «portal del banco»; extracto por plantilla genérica o captura | sólo si no se acepta el genérico (06/11) |
| P2 cheques y convenios | clase cheque apagada (D37); sin convenios hasta R2 | no |
| P3 bóveda | cada oficina configura bóveda o caja principal como bóveda; sin defecto | no (es configuración) |
| P4 personal, niveles, espera | roles sembrados sin asignar; límites de monto sin valor = sin atribución; espera sin valor | **sí** (13/11): roles, alcances y segundo factor son condición del go/no-go |
| P6 pagadurías | convenios y formatos de vuelta como dato; plantilla genérica de respuesta | la pagaduría (06/11), no la caja |
| P7 SARLAFT | 5 y 30 millones con «pendiente de validar»; reporte `Vigente`; alcance `SoloEfectivo` | **sí** en umbrales y listas (20/11) |
| P8, P12 matriz | filas sugeridas sin cuenta | **sí** (20/11): completitud al 100 % del go/no-go |
| P10 GMF | 4 × 1000 pendiente de validar; hechos generadores de egresos sin activar salvo devoluciones | no |
| P11 fecha valor de partidas | v1 + corrección por condonación hasta R1 | no |
| P13, P14 servicios y devolución de aportes | R0 cobra lo que financia Cartera; devolución de aportes por orden de giro | no |
| P15 no-go parcial | la lista de go/no-go exige el escenario escrito | **sí** (20/11) |
| P17 fondo de liquidez | cuentas sin propósito «fondo» hasta que se marquen | no |
| P18 cuadre | aprueba quien tenga `…Activation.ProvisionalSquare` | no |
| P19 conservación | diez años pendiente de validar | no |
| P5, P9, P16 | fuera del diseño de R0/T0 | no |
| Retiro de `ACC_BankReconciliations`, `ACC_BankStatementLines`, `ACC_BankStatementColumnMaps` (R-39) | quedan sin uso y nadie las escribe | no; si se hace, migración destructiva aparte con guarda, respaldo y segundo revisor |
| Recortes de R0/T0 (plan.md, «Líneas de recorte») | ninguno: R0/T0 completo | lo decide el dueño en cada hito y se anota aquí |
