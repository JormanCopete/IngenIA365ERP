# Investigación — 028 Recogida de créditos y productos en el desembolso

**Base de código**: `origin/develop` @ `d810d07f` (2026-11-27). Tres revisiones de solo lectura: libranza y cuota enviada, otros productos,
contabilidad y pruebas. Lo verificado lleva su archivo; las decisiones técnicas van como R1–R18 (Decisión · Por qué · Alternativas).

## 0. Lo que ya existe (y se reutiliza)

| Pieza | Dónde | Qué hace hoy |
|---|---|---|
| Solicitud con recogidas | `Application/Lending/Origination/CreditApplicationDtos.cs:39` (`OwnLoanPayoffInput`), `CreditApplicationCommands.cs:230` | Guarda en `LND_CreditApplications.PayoffRequest` (JSON) los créditos autorizados. **No valida** nada al radicar |
| Grupo editable «Recogidas» | `Domain/Lending/Origination/GruposEditablesAlDevolver.cs`, `FotoDeSolicitud.cs:141` | Ya existe para corregir al devolver |
| Requisitos del desembolso | `Disbursements/RequisitosDelDesembolso.cs:442-474` | Valida autorizado, deudor, `Active`, mora (`Lending.Payoff.RequiresRestructuring`) y neto positivo |
| Liquidación para recoger | `Disbursements/IMotorDeCreditoEnDesembolso.cs:66-101` | Reconstrucción a la fecha + cuota completa si aplica (`InteresesAlCancelar`) |
| Aplicación | `Disbursements/RecogidaEnElDesembolso.cs` | `IContratoDeObligaciones.AplicarAsync` con `CAR`/`OwnLoanPayoff`, `InternalOffset`, `FullPayoff` o `FreeAmount`, clave `CAR:{op:N}:{obligación:N}` |
| Anulación | `Disbursements/VoidPendingLoanCommand.cs:64-92` | Reversa cada recogida con `{clave}:reverso` si el crédito sigue pendiente de giro (C11) |
| Liquidación a (D, R) | `Settlement/GetLoanSettlementQuery.cs` | Proyecta a una D futura sin pagos con la información hasta R; huella; `Lending.Payoffs.Read`; audita `Lending.Payoff.Viewed` |
| Simulación de cancelación | `PersonStatement/SimulacionDeCancelacion.cs` | Desglose por concepto de varios créditos a una fecha, con PDF |
| Interés al cancelar | `Lending/Common/InteresesAlCancelar.cs` | Un solo sitio: `Pagos.InteresesAlPagarAntesDelVencimiento`, vivienda, tope del prepago, período calendario |
| e2e | `tests/IngenIA365ERP.API.IntegrationTests/Lending/DesembolsoYRecogidaTests.cs:238,286` | Recorre una recogida por HTTP: neto, `CAR:`, `PagoAplicado`, rechazo de Tesorería, anulación y `:reverso` |

## 1. Hallazgos verificados

- **H1 cuota enviada** — `PayrollDeductionFileLine` sin estado (`Domain/Entities/Lending/Payroll/PayrollDeductionFileLine.cs:192-238`); «pendiente
  de giro» derivado (`Lending/Payroll/PendientesDeGiro.cs:16-20`); «enviada sin respuesta» sin consulta; `ConsumoDeLibranza.cs:320-392` no mira
  el estado del crédito; `GirosDePagaduria.AnotarAsync` (`Receipts/Paymasters/GirosDePagaduria.cs:389-404`) deja `PaymasterUnapplied`;
  `TareasDeLibranza.cs:101-140` alerta sin filtrar por estado; `ProyeccionCentralDeRiesgo.cs:484` lo lee; `ValorParaCancelar` lo incluye
  (`EstadoDelCredito.cs:199-206`). En nómina propia la línea baja a cero con aviso (`ApprovePayrollRunCommand.cs:339-370`).
- **H2 validaciones** — `ValidacionesDeOriginacion.cs:172` (`CapacidadDePago.CuotasPropiasAsync` cuenta todo crédito vivo), `:207` (descuento),
  endeudamiento consolidado y cupo con `ExposicionConsolidadaDelDeudor`, concentración (`ValidacionDeConcentracion`) y, en el desembolso,
  concentración sobre el bruto (`RequisitosDelDesembolso.cs:260`).
- **H3 parcial** — `FreeAmount` (`Domain/Lending/Payments/AplicadorDePagos.cs:117-129`) deja el resto como anticipo; el abono a capital es
  `ExtraordinaryPrincipal` con `EleccionDeAbono`.
- **H4 neto** — la orden usa `r.Recogido` evaluado (`ExecuteLoanDisbursementCommand.cs:184`); el hecho y la auditoría usan lo aplicado
  (`:205-210`).
- **H5 contabilidad** — DS: `Capital` D contra `DesembolsoPorGirar` C por el bruto, ignora `PayoffDeductions`
  (`Accounting/Lending/Contabilizacion/ConstructorDeLineasDeCartera.cs:442-457, 935-939`; caso dorado `01-desembolso-por-transferencia.json`).
  RC: `RecaudoPorAplicar` clase `OwnLoanPayoff` D contra los conceptos (`:538-551`; caso `02`). Roles en `RolesDeCuentaDeCartera.cs:372-373`. El
  neteo depende de que las dos reglas apunten a la misma cuenta (`specs/013…/contracts/contabilidad.md:1188, 1378`), lo que no se verifica y no
  sirve en giro a tercero, regla por línea ni con cruce. La conciliación (`Lending/Integration/SaldosContablesDeCartera.cs:199-229`) espera el
  puente en cero.
- **H6 giro nuevo** — `ReissueDisbursementOrderCommand.cs:105-124` emite `CreditoDesembolsado` con recogidas vacías.
- **H7 garantías** — el cruce interno queda en firme al aplicarse (`FirmezaDelPago.cs:34,41`) y libera garantías y pagaré
  (`Disbursements/Collaterals/GarantiasDeCartera.cs:142-154`).
- **Otros productos** — `IContratoDeCuentas` rechaza `CAR` por registro (`ContratoDeCuentasDeCartera.cs:692-703`), clase de medio
  (`Deposits/Contributions/ConsignacionEnCuentas.cs:468-480`) y clave (`ValidadoresDelContrato.cs:24-25`). El cruce del retiro y el débito
  automático escriben las cuentas por dentro (`CruceDeRetiro.cs:107-131`, `DebitoAutomatico.cs`). Las facturas a crédito de Comercial son saldo
  de Contabilidad y sólo se cobran por un recibo de Recaudo (`Receipts/CashDesk/ConfirmacionDeRecibo.cs:417-421`); `AplicacionNoInteractiva`
  arma recibos sin caja (precedentes: el Canal y `CruceAutomaticoDeAnticipos`); no existe la clase de medio `InternalOffset` en
  `Domain/Enums/Core/PaymentEnums.cs:7-11`.

## 2. Decisiones técnicas

### R1 — Un discriminador de producto, sin romper el contrato de hoy
**Decisión**: `PayoffInput(ProductKind Kind, Guid ProductPublicId, string Mode, decimal? Amount, string? InstallmentTreatment)` con
`ProductKind = Loan | Account | CommercialReceivable`. `CreditApplicationInput.Payoffs` y `DisbursementInput.Payoffs` lo usan; el campo viejo
`ownLoanPayoffs` se sigue aceptando y se traduce a `Kind = Loan` (la e2e existente lo usa). Si vienen los dos, 400 `Request.BodyInvalid`.
**Por qué**: un solo flujo para todos los productos; la API de hoy sigue funcionando.
**Alternativas**: tres listas paralelas (una por producto) — triplica validaciones y huella.

### R2 — La proyección se guarda en filas, no en JSON
**Decisión**: tabla `LND_CreditApplicationPayoffs` (una fila por solicitud y producto) con el desglose proyectado; tabla
`LND_DisbursementPayoffs` (una por operación de desembolso y producto) con lo actual, lo aplicado, la diferencia y su causa. La columna
`LND_CreditApplications.PayoffRequest` **se conserva** (no hay migración destructiva) y deja de escribirse; la migración copia lo que tenga a
filas (`Kind = Loan`, sin proyección) y la `FotoDeSolicitud` lee de las filas.
**Por qué**: el informe de recogidas, la diferencia en el desembolso y la foto al devolver necesitan consultar por producto; el JSON no se
puede filtrar ni indexar. Conservar la columna evita el Principio XII de respaldo y segundo revisor.
**Alternativas**: ampliar el JSON — sin índices ni informe; borrar la columna — destructiva sin necesidad.

### R3 — Un solo sitio liquida una recogida
**Decisión**: `Lending/Payoffs/LiquidacionDeRecogida` (Application) reemplaza `MotorDeCreditoEnDesembolso.LiquidarAsync` y la usan la consulta
de productos (proyección a D con R), la vista previa y el comando del desembolso. Sale de la reconstrucción (`CargadorDeReconstruccion` +
`MotorDeCartera.Reconstruir`), aplica `ModoDeInteresesAlRecoger` (R5) y el tratamiento de la cuota causada (R4), y devuelve el desglose por
concepto, la cuota causada con su envío (R6), lo que se devuelve o se cobra por proporcionales, los días de mora, los bloqueos y la huella.
Prueba de arquitectura `LaRecogidaSeLiquidaEnUnSoloSitio`.
**Por qué**: proyección, vista previa y aplicación tienen que decir lo mismo al peso (H4).
**Alternativas**: llamar a `GetLoanSettlementQuery` por `ISender` — audita cada lectura y no conoce la cuota causada.

### R4 — «Todo menos la cuota causada» es una modalidad del contrato
**Decisión**: `ModalidadDePago.PayoffExceptCurrentInstallment = 8` en el contrato de obligaciones. En el motor puro
(`AplicadorDePagos`): cubre todo lo cobrable salvo los conceptos de la cuota causada `n`, y abona a capital las cuotas posteriores a `n` (las
retira del plan). La cuota `n` queda **tal como se envió** (D13-A): si la causación no la completó (causación diaria), el motor causa en ella lo
que falta del interés del período —el mismo cálculo que `InteresPorCausarDeLaCuotaEnCurso` de la cuota completa— y la marca **congelada**: la
reconstrucción no le causa más interés hasta su vencimiento. La decisión del pago sella `n`, el valor congelado y la regla, así la
reconstrucción lo repite. Créditos en UVR: no se ofrece (siempre total, con explicación). Casos dorados en `Domain.Tests/Lending/Payments/Casos/`
para causación diaria, al inicio del ciclo y por período calendario.
**Por qué**: la pagaduría ya tiene el valor de la cuota; si el crédito no la conserva exacta, cada recogida deja un excedente.
**Alternativas**: un pago parcial `FreeAmount` (deja anticipo, H3); recalcular la cuota sobre el saldo que queda (D13-B, excedente seguro).
**Riesgo**: es lo más delicado del motor; va primero en la entrega E2 con sus casos dorados antes de tocar pantallas.

### R5 — Intereses proporcionales por el mismo sitio que la cancelación
**Decisión**: `InteresesAlCancelar` gana `ModoAlRecogerAsync(credito, rulesVersion, fecha)`: lee `Recogida.InteresesProporcionales` (línea del
crédito recogido con caída al general); `ComoAlCancelar` → el modo de hoy; `Si` → `Corrido`; `No` → `CuotaCompleta`; con las mismas
excepciones legales (vivienda, tope del prepago, valores nacionales que falten). La recogida aplica con `FullPayoff` y el modo sellado en la
decisión del pago, como hoy.
**Por qué**: `Si` y `No` son exactamente los dos cálculos que ya existen; no hace falta un tercer cálculo de intereses.
**Alternativas**: reutilizar sin parámetro nuevo — el dueño lo pidió como parámetro propio de la recogida.

### R6 — Consulta de la cuota enviada
**Decisión**: `Lending/Payroll/CuotaEnviadaDelCredito` (sólo lectura) deriva, por crédito y fecha: `EnviadaSinRespuesta` (línea en archivo
`Sent` sin resultado), `DescontadaPendienteDeGiro` (`PendientesDeGiro`), `NoDescontada`, `EnNominaPropia` (vínculo `OwnPayroll` y cuota del
período en curso; Cartera no lee tablas `PAY_`), con pagaduría, convenio, archivo, período, valor de la línea (corriente y vencido). La usan
`LiquidacionDeRecogida`, la caja (por `IContratoDeObligaciones.ListarAsync`, campo nuevo `SentInstallment` en `ObligacionListadaDto`) y la
pantalla del crédito.
**Por qué**: hoy no existe consulta por crédito (sólo por convenio y período).
**Nómina propia (2026-11-27, pedido de la sesión coordinadora tras el conteo de producción: COOFLOPAL no tiene convenios ni archivos de
pagaduría, sí 2 créditos por nómina propia)**: la cuota también está «enviada» cuando la nómina ya tiene un descuento de cartera vivo para el
crédito. Como Cartera no puede depender de Nómina, Nómina publica un puerto de sólo lectura
`IDescuentosDeNominaParaCartera` (`Application/Common/Integration/Payroll`, un método: por obligaciones y fecha, la novedad
`LoanDeduction` activa del período abierto, la fila `PAY_PayrollLoanDeductions` de una corrida en borrador o aprobada, con período, corrida,
estado y valor), implementado en `Application/Payroll`. Estados: `OwnPayrollGenerated` (novedad en período abierto), `OwnPayrollCalculated`
(corrida en borrador), `OwnPayrollApproved` (aplicada: la cuota ya está pagada y no se recoge). Con «Incluir», la corrida en borrador que
después se aprueba baja ese descuento a cero con aviso (comportamiento de hoy, sin doble cobro), y la pantalla lo explica; con «Excluir», la
cuota queda en el crédito viejo y la nómina la paga exacta al aprobar. `SentInstallmentState.OwnPayroll` se detalla con esos tres estados
(valor nuevo del enum o campo aparte, a decidir en la tarea).

### R7 — Créditos cerrados en la libranza (H1)
**Decisión**:
- `ConsumoDeLibranza`: si el crédito ya no está `Active` al consumir el resultado, el resultado se guarda con `LoanClosedAtResult = true`
  (columna nueva en `LND_PayrollDeductionResults`), no crea no descontado ni aviso al deudor.
- `PendientesDeGiro.Consulta` y la tarea de giros atrasados excluyen esos resultados; la proyección a centrales también.
- El giro: el contrato de obligaciones, al rechazar con `Lending.Payment.ObligationNotPayable` un crédito recogido, agrega en `Data` el
  `successorObligationPublicId` y la regla `Recogida.GiroPosteriorDeCreditoRecogido`. `GirosDePagaduria.AnotarAsync` (Recaudo): con
  `AlCreditoNuevo` aplica la línea al crédito sucesor (mismo recibo, clave `REC:{giro}:{línea}:sucesor`); con `AFavorDelAsociado` deja el saldo
  a favor de la persona; con `SaldoDeLaPagaduria`, como hoy.
**Por qué**: la regla es de Cartera (qué pasa con su crédito), la aplicación es de Recaudo (dueño del giro); el contrato ya es la frontera.

### R8 — Validaciones de la solicitud después de recoger (H2)
**Decisión**: `ContextoDeValidacion` lleva `RecogidasDeLaSolicitud` (crédito, total o parcial, capital y cuota que se liberan). `CapacidadDePago`
excluye la cuota de los recogidos totales y prorratea los parciales; `ExposicionConsolidadaDelDeudor`, cupo y `ValidacionDeConcentracion` restan
el capital recogido. El detalle de cada validación dice cuánto liberó la recogida. En el desembolso, la concentración se mide sobre bruto −
recogido.

### R9 — Desembolso: todas las recogidas, recalculadas, con diferencias (HU3)
**Decisión**: `RequisitosDelDesembolso` toma las recogidas de `LND_CreditApplicationPayoffs` (no del cuerpo; si el cuerpo trae una lista distinta,
`Lending.Payoff.MustMatchApplication`), las liquida con `LiquidacionDeRecogida` a la fecha valor, las compara con lo proyectado y clasifica la
causa por concepto (`Pagos`, `Causacion`, `Mora`, `CuotaCausada`, `Parametros`, `ProductoCancelado`) leyendo las operaciones del producto
entre R y ahora. `Recogida.AlDesembolsar` decide qué se aplica. Las diferencias y los omitidos entran en la huella; el comando exige
`AcknowledgedPayoffDifferences = true` si hay alguna. `Recogida.ToleranciaDeDiferencia` con valor bloquea con
`Lending.Payoff.DifferenceAboveTolerance`. Bajo el cerrojo se vuelve a liquidar; si lo aplicado por el contrato difiere de lo liquidado, aborta
con `Lending.Payoff.AppliedDiffers` (defensa). La orden sale por bruto − **aplicado** (H4). Los tramos siguientes no recogen.

### R10 — Contabilidad que cuadra sola (H5)
**Decisión**: el constructor del DS (`ConstructorDeLineasDeCartera`, operación DES) acredita `DesembolsoPorGirar` por el **neto** y, por cada
`PayoffDeduction`, `RecaudoPorAplicar` con la clase `OwnLoanPayoff`, tercero = deudor y cruce = el producto recogido: **el mismo rol y las
mismas dimensiones** que debita el RC. Sin recogidas, el DS es idéntico al de hoy. `PayoffDeductionV1` gana `ProductKind` (versión del
mensaje compatible: campo opcional, ausente = `Loan`). Casos dorados: `01` se actualiza (con recogidas) y uno nuevo con dos recogidas, giro a
tercero y cuenta con cruce. La conciliación de Cartera queda como está y ahora se cumple. Antes de desplegar, diagnóstico de desembolsos con
recogidas en DEV, QA y PDN (`LND_LoanOperations` con clave `CAR:` de un desembolso): si hay, se listan para la contadora.
**Por qué**: el puente queda en cero por construcción y la contadora no tiene que hacer coincidir dos reglas.
**Alternativas**: un rol nuevo «PuenteDeRecogida» — vuelve a exigir que dos reglas apunten a la misma cuenta.

### R11 — Un solo comprobante (D11)
**Decisión**: parámetro de Contabilidad (clave `Accounting.LendingDisbursementWithPayoffsInOneVoucher`, visible como
`Contabilidad.RecogidaEnUnComprobante`, defecto `false`). Con `true`, `ComprobantePorRecibo` agrupa el DS con los RC cuyo recibo es el número
del DS en un comprobante del tipo del DS. Sin el parámetro, como hoy.

### R12 — Giro nuevo (H6) y garantías (H7)
**Decisión**: `ReissueDisbursementOrderCommand` emite `CreditoDesembolsado` con las `PayoffDeductions` de la operación original y el mismo
neto. `GarantiasDeCartera`: un crédito cancelado por una recogida (`CAR:` de un desembolso) libera garantías y pagaré cuando la orden del
crédito nuevo queda `Paid` (consumo del resultado de Tesorería) o, si se anula, no las libera nunca; el detalle del crédito dice «por liberar
al girar el crédito …».

### R13 — Aportes y ahorros por dentro de Cartera (HU6)
**Decisión**: `Lending/Payoffs/RecogidaDeCuentas` consigna por `ConsignacionEnCuentas` + `RegistroDeCuentas` (como el cruce del retiro, al
revés), con `CAR`/`OwnLoanPayoff`, medio `InternalOffset` y clave `CAR:{op:N}:{cuenta:N}`; `ConsignacionEnCuentas.ClaseDeMedio` admite esa
combinación **sólo** por el camino interno (el puerto público la sigue rechazando: prueba nueva). Modos: `Cuotas` = `Installments`; `Abono` =
`Free` (aporte extraordinario si la línea lo admite, saldo en ahorros). El cerrojo sigue el orden del desembolso y luego persona → cuentas
(`ElOrdenDelCerrojoEsPersonaYLuegoSusCuentas`). La anulación reversa por `ReversionDeCuentas` con `{clave}:reverso`. Contabilidad: la
consignación sale con su hecho de siempre y debita el mismo puente `RecaudoPorAplicar`/`OwnLoanPayoff` (como el cruce del retiro,
`ConstructorDeLineasDeCartera.Captaciones.cs:33-34`). Riesgo a verificar al empezar la entrega: el orden de cerrojos del cruce del retiro
(cuentas y créditos) frente al del desembolso, para no abrir un interbloqueo. 2026-11-27 (T075): verificado, sin riesgo; el orden y la decisión en la enmienda E-14.

### R14 — Facturas a crédito por un recibo sin caja (HU7)
**Decisión**: puerto nuevo `IRecogidaEnRecaudo` en `Common/Integration/Receipts`, implementado por Recaudo sobre `AplicacionNoInteractiva` con un
canal `LoanDisbursement` y una clase de medio nueva `InternalOffset` en el catálogo de medios (semilla); aplica a la factura, emite el
`ReciboEmitido` (Contabilidad y Transporte se enteran como con la caja) y corre en la transacción del desembolso. Contabilidad: Recaudo debita
el rol de su medio y el DS acredita el puente de Cartera; la completitud de las dos matrices avisa si no resuelven a la misma cuenta (única
excepción al «cuadra solo», documentada). El saldo de las facturas se lee por el puerto nuevo, no por `IContabilidadParaCartera`.
**Riesgo**: `AplicacionNoInteractiva` toma sus propios cerrojos y numera; hay que probar que corre dentro de la transacción explícita del
desembolso antes de construir la entrega (tarea de exploración al inicio de E4).

### R15 — Parámetros
**Decisión**: en `ParametrosDeCartera.Definiciones` (módulo Lending, con vigencia): `Recogida.CuotaCausada` (`Eleccion`, General + Línea),
`Recogida.InteresesProporcionales` (`Eleccion`, General + Línea), `Recogida.ProductosAdmitidos` (`Texto` con patrón
`Producto:modo,modo;…`, General + Línea), `Recogida.AlDesembolsar` (`Eleccion`, General + Línea), `Recogida.ToleranciaDeDiferencia`
(`NumeroDecimal`, vacío permitido, General), `Recogida.GiroPosteriorDeCreditoRecogido` (`Eleccion`, General), `Pagos.CuotaEnviadaAlCancelar`
(`Eleccion`, General + Línea). Cada uno con su defecto de hoy. Los de la línea del crédito recogido se leen con la línea de ese crédito; los del
crédito nuevo, con la línea de la solicitud.

### R16 — Pantallas
**Decisión**:
- `Components/Cartera/ProductosParaRecoger.razor` (nuevo): la tabla de productos con marcas, modo, valor parcial, tratamiento de la cuota
  causada, desglose desplegable, avisos y la fecha proyectada; la usan `SolicitudNueva.razor` (paso 2, tras el simulador) y la vista de la
  simulación imprimible.
- `SolicitudNueva.razor`: quita el bloqueo de `UsesAdvancedTerms` para recogidas (sigue para cuotas extraordinarias y gracia), manda `Payoffs`
  y muestra neto y cuotas liberadas.
- `SolicitudDetalle.razor`: sección «Lo que recoge» con lo proyectado y el neto.
- `Desembolsos.razor`: quita el letrero; tabla de recogidas con proyectado / actual / diferencia / causa, omitidos, casilla «Revisé las
  diferencias» y neto real; resultado con lo aplicado por producto.
- `CreditoDetalle.razor` y estado de cuenta: «recogido por …» / «recogió …», cuota dejada a la pagaduría.
- `Caja.razor`: aviso de cuota enviada en F7 y en la cancelación; opción «todo menos la cuota enviada».
Todas con `IndicadorDeCarga` por zona.

### R17 — Informe, auditoría y permisos
**Decisión**: vista `payoffs` en `Lending/Reports/Vistas/PayoffsReportQuery.cs` (`TablaExportable`, filtros período, oficina, línea, asesor,
producto) y en `/cartera/informes`. Auditoría: el evento del desembolso lleva por producto proyectado, actual, aplicado, diferencia y causa, y
quién reconoció; `Lending.Payoff.Viewed` en la proyección y en la vista previa (hoy la vista previa no lo emite). Sin permisos nuevos.

### R18 — Migraciones
**Decisión**: una migración por entrega que cambia esquema, en los dos motores, fechada después de `EsquemaBase`, con
`tools/scripts/add-migration.ps1` y `check-migration-parity.ps1`:
- E1 `RecogidaDeCreditos`: `LND_CreditApplicationPayoffs`, `LND_DisbursementPayoffs`, copia de `PayoffRequest` a filas.
- E2 `CuotaDejadaALaPagaduria`: `LND_PayrollDeductionResults.LoanClosedAtResult` (bit, defecto falso) y las columnas de la cuota dejada en
  `LND_DisbursementPayoffs`.
- E3 y E4: sin esquema previsto (semilla de la clase de medio en E4).
Todas aditivas: sin respaldo previo obligatorio.

## 3. Respuestas del dueño (2026-11-27)

El dueño respondió «OK con las recomendadas» (citado por la sesión coordinadora el 2026-11-27): **D1–D13 quedan con la opción A** de la spec.

- D1 A: `Recogida.CuotaCausada` se lee con la línea del crédito que se recoge.
- D2 A: existe `Preguntar` (el asesor elige por crédito).
- D3: `Recogida.GiroPosteriorDeCreditoRecogido` con defecto `SaldoDeLaPagaduria` (como hoy); para COOFLOPAL la recomendada es `AlCreditoNuevo`
  (se configura en la cooperativa, no es el defecto de fábrica).
- D4 A: la caja también ofrece «todo menos la cuota enviada» con `Pagos.CuotaEnviadaAlCancelar` (defecto `Incluir`) y siempre avisa.
- D5 A: se ofrece `MantenerLoProyectado`, con el aviso de que el producto puede quedar con saldo.
- D6 A: `Recogida.ToleranciaDeDiferencia` vacía de fábrica: sólo se avisa y se reconoce.
- D7 A: la recogida parcial de un crédito abona a capital con la elección del deudor.
- D8 A: las facturas a crédito de Comercial van en esta feature como última entrega (E4).
- D9 A: todas las recogidas autorizadas son obligatorias al desembolsar; quitar una exige devolver la solicitud.
- D10: confirmado, recoger un crédito en mora sigue bloqueado (reestructuración).
- D11 A: `Contabilidad.RecogidaEnUnComprobante` (clave `Accounting.LendingDisbursementWithPayoffsInOneVoucher`), defecto no.
- D12 A: las garantías del crédito recogido se liberan al girar el crédito nuevo.
- D13 A: la cuota que se deja queda tal como se envió, con el interés del período completo.
- **D13 ajustada (2026-11-27, el dueño en la sesión)**: la Ley 1555 de 2012 da el prepago sin penalidad con los intereses liquidados al día del pago. Por
  eso, **dentro del tope legal del prepago** —y cuando el tope no se puede saber por falta del SMMLV o de `PREPAGO_TOPE_SMMLV` a la fecha del desembolso, el
  mismo criterio de `InteresesAlCancelar.ModoAsync` (motivos `WithinLegalPrepaymentCap` y `LegalPrepaymentCapUnknown`)— la cuota que se deja lleva el
  **interés proporcional**: sobre todo el saldo hasta el día de la recogida y, desde ese día hasta su vencimiento, sólo sobre su propio capital. El interés
  de los días que faltan sobre el capital que se recoge no se cobra (lo reconocido de más al inicio del ciclo o del tramo se devuelve con la misma
  operación). Si la pagaduría o la nómina pagan la cuota tal como se envió, lo de más lo trata la regla de excedente de H2
  (`Recogida.GiroPosteriorDeCreditoRecogido`). **Vivienda**: se recoge completa (sin cambio). **Fuera del tope**: D13-A tal como estaba. Independiente de
  `Recogida.InteresesProporcionales`: sólo el motivo legal cambia la cuota dejada. Implementado en T062a (enmienda E-15).

## 4. Enmiendas

### E-01 — Esquema de las recogidas (T005–T007, 2026-11-27)
- **Tres únicos en vez de uno** en `LND_CreditApplicationPayoffs`: `(CreditApplicationId, LoanId)`, `(CreditApplicationId, AccountId)` y
  `(CreditApplicationId, CommercialReceivableReference)`, cada uno filtrado a su identificador no nulo y a las vivas. Por qué: el único de data-model
  sobre cinco columnas que admiten nulos no cuida nada en PostgreSQL (ve distintos los nulos). Un CHECK por tabla (`CK_…_Product`) exige el
  identificador de la clase y sólo ése.
- **Operaciones en `bigint`**: `DisbursementOperationId`, `PaymentOperationId` y `AccountOperationId` son `long`, porque `LND_LoanOperations` y
  `LND_MemberAccountOperations` lo son (data-model decía `int`). `AccountOperationId` lleva FK a `LND_MemberAccountOperations`.
- **Claves**: `IdempotencyKey` de 150 (el largo de `LND_LoanOperations.IdempotencyKey`, no 120), única entre las vivas; `ReversalKey` de 160.
- `InterestMode` admite nulo (fila sin proyección); `ProjectionSha256` va vacía en esas filas y lleva «sha256:» + 64 en las demás (71).
- La copia de `PayoffRequest` va como SQL a mano en el `Up` de los dos motores (no hizo falta una tarea del DbMigrator): `Order` = la posición en el
  JSON, un crédito repetido entra una vez y uno que no existe no entra.

### E-02 — `PayoffInput` (T013, 2026-11-27)
- `Mode` e `InstallmentTreatment` son los enums `PayoffMode` e `InstallmentTreatment` (el JSON los acepta por nombre o por número), no texto;
  `ProductPublicId` admite nulo porque la factura se nombra por `Reference`.
- «Las dos listas a la vez» es que las dos traigan al menos un elemento. `RecogidasPedidas.Unificar` lo devuelve como `Result` con el código
  `Request.BodyInvalid`, y `ErrorEnvelopeFilter` lo saca con 400, como el cuerpo ilegible. Un modo viejo que no sea `Full` ni `Partial` también es
  `Request.BodyInvalid` (el validador de hoy ya los rechazaba).

### E-03 — Causas del comparador de desgloses (T008, 2026-11-27)
- Además de «capital menor → pagos», un anticipo o un saldo a favor mayor también es `Payments` (sólo entra por un pago).
- «Causación» abarca todo lo que se causa con los días —interés corriente, devuelto o cobrado por proporcionales, seguro y administración—
  cuando el capital no cambió; con el capital menor, esos cambios los explica el pago.

### E-04 — La liquidación de la recogida y el modo de intereses (T009–T012, T050, 2026-11-27)
- **Nombre y forma**: `LiquidacionDeRecogida.LiquidarCreditoAsync(Loan, D, R)` devuelve `Result<LiquidacionDeCreditoRecogido>` (no `LiquidarAsync`, para
  que `LaRecogidaSeLiquidaEnUnSoloSitio` distinga sus llamadas de las de `MotorDeCreditoEnDesembolso.LiquidarAsync`). Falla sólo si no se puede liquidar:
  una regla del motor o, con D ≤ hoy, la usura del día sin publicar (como la liquidación del crédito); lo demás son `Bloqueos`.
- **Bloqueos**: mora → `Lending.Payoff.RequiresRestructuring` (el mismo código del desembolso); castigado, insolvencia que negocia, sin saldo y no activo →
  `Lending.Payoff.NotPayable` con el motivo (`WrittenOff`, `Insolvency`, `NoBalance`, `NotActive`), en `ErroresDeRecogida`. `RequisitosDelDesembolso`
  usa ya la liquidación nueva (valor y mora) pero conserva su propia revisión de estado; los demás bloqueos entran al desembolso con T033.
- **Saldo a favor**: la cancelación no lo usa (D7 de la 013), así que el desglose de un crédito lo lleva en cero y la liquidación lo informa aparte
  (`SaldoAFavorSinDescontar` y un aviso). La spec («el valor para cancelar ya los descuenta») vale sólo para el anticipo.
- **Interés devuelto** es `AplicadorDePagos.InteresSinTramo` (por período calendario); **interés cobrado** es lo que agrega la cuota completa, ahora público
  como `AplicadorDePagos.InteresDeLaCuotaCompleta` (refactor sin cambio de comportamiento: lo usan `ValorParaCancelar` y la cancelación). Lo reconocido al
  inicio del ciclo y no causado nunca fue deuda: no aparece como devuelto (T051 puede explicarlo).
- **El valor del desembolso cambia sólo por período calendario**: `MotorDeCreditoEnDesembolso` tomaba el mayor entre su valor y el valor para cancelar, así
  que con período calendario cobraba el interés de un tramo sin empezar que el `FullPayoff` no aplica (quedaba excedente). La liquidación nueva cobra lo
  que aplica el contrato. De fábrica y con la cuota completa, el mismo valor. El cruce del retiro sigue con `MotorDeCreditoEnDesembolso.LiquidarAsync`
  sin cambios.
- **T050**: el discriminador es `AplicacionDePagos.EsRecogidaDelDesembolso` (recaudador de clase `OwnLoanPayoff` y `Receipt.DocumentKind` =
  `InteresesAlCancelar.DocumentoDelDesembolso`, «LoanDisbursement», que ahora usa también `RecogidaEnElDesembolso`). `DistribuirAsync` y `ModoAsync`
  ganan el parámetro opcional `recogida`; el puerto `IContratoDeObligaciones` no cambia. La simulación del contrato no lo lleva (no trae recibo).
- **Sin condiciones selladas**: posición diaria o libro, como el motor; de la posición sale el desglose y lo que no desglose va en otros cargos, para que
  el total sea el de la posición. `CuotaCausadaDeRecogida` gana el vencimiento (opcional, no se guarda).

### E-05 — Los productos para recoger (T014–T022, 2026-11-27)
- **Qué se muestra y qué se recoge**: se listan los créditos del deudor vigentes, pendientes de giro y castigados (lo cancelado, anulado o novado no es
  un producto vivo); sólo el vigente al día se puede recoger. Un crédito que no se pudo liquidar (una regla del motor, la usura del día) no tumba la
  consulta: sale sin poder marcarse con `blockReason = NotSettled` y el mensaje del error. «No admitido» es `NotAdmitted`, con el código
  `Lending.Payoff.ProductNotAdmitted`.
- **La línea del crédito nuevo** se asegura con el alcance de líneas de quien consulta (fuera, el 404 de la línea); los créditos salen de `ListLoansQuery`,
  que aplica el de oficina y línea. La consulta queda exceptuada en `LasConsultasDeCarteraRespetanElAlcance` con ese motivo, como la simulación de
  cancelación. La lectura de `Recogida.ProductosAdmitidos` vive en `AdmisionDeRecogidaVigente` (la usará T024).
- **«Otra solicitud que lo recoge»** cuenta las de estado borrador, radicada, en decisión, devuelta y aprobada; un borrador sin número sale como `""`
  («otra solicitud en borrador»). Es un aviso: no impide marcarlo.
- **Lo marcado**: un producto que no está entre los del deudor es `Lending.Payoff.NotOfApplicant`; uno que no se puede recoger o marcado en un modo que la
  línea no admite no se cuenta y se avisa en el resumen; el parcial se topa en lo que cuesta recoger. En E1 el validador sólo admite `Kind = Loan`.
  `capacityAfter` va nulo: la capacidad después de recoger la calcula la radicación (T025).
- **El DTO gana** (además de api.md §1): `status`, `blockMessage`, `interestModeExplanation`, `selected`/`selectedMode`/`selectedAmount` por producto;
  `selectedCount`, `newLoanAmount`, `newInstallment` y `netNotPositive` en el resumen; `person`, `newLoanLine` y, sólo para imprimir, `letterhead`.
  `origin` es `LoanOrigin` (migrado = crédito) y `channel` es `RepaymentChannel`, nulo sin vínculo de forma de pago; los dos salen como número.
- **La pantalla resume lo marcado sin volver a consultar** (`SeleccionDeRecogida.Resumir`, la misma regla del servidor): cada clic no vuelve a liquidar ni a
  auditar `Lending.Payoff.Viewed`. «Simular» (o cambiar persona, línea o fecha de la simulación) vuelve a consultar.
- **El PDF** (`/payoffs/candidates/pdf`) queda en la auditoría de exportaciones con la clave `payoff-candidates-simulation`. Reusa la hoja, los datos de la
  persona y los avisos de `EstadoDeCuentaDeLaPersonaReport` (ahora `internal`).

### E-06 — Radicar, corregir y decidir una solicitud que recoge (T023–T031, 2026-11-27)
- **Un solo sitio para la recogida de la solicitud**: `Lending/Payoffs/RecogidasDeLaSolicitud` unifica el cuerpo (`payoffs` u `ownLoanPayoffs`), valida,
  proyecta con `LiquidacionDeRecogida` y reescribe las filas; lo usan crear, corregir, editar la radicada (`PrepararAsync`) y radicar
  (`ReproyectarAsync`). Al reescribir, la baja lógica de las filas viejas se guarda **antes** de agregar las nuevas: el único filtrado por crédito no ve la
  baja si la fila nueva entra primero en el mismo guardado.
- **D y R**: D = la de `payoffProjection`, si no la fecha esperada de desembolso, si no hoy; una D pasada se lleva a hoy (no se rechaza) y una R futura a
  ahora. Al radicar, la D que se guardó (la mayor de las filas con proyección; las copiadas de `PayoffRequest` toman la fecha esperada) y R = ahora.
- **Códigos**: una cuenta o una factura en E1 es `Lending.Payoff.ProductNotAdmitted` (además del validador); un crédito que no existe, de otra persona o
  donde el solicitante es codeudor, `NotOfApplicant`; `ModeNotAdmitted` y `NetNotPositive` son nuevos (`ErroresDeRecogida`). El parcial sin valor, un
  producto repetido, más de 50 y la cuota causada «Excluir» (llega con E2) los rechaza el validador de `CreditApplicationInput`.
- **El parcial se proyecta a capital** (`Capital = valor pedido`): un crédito que se puede recoger está al día y a D no tiene nada vencido; si el valor
  cubre lo que cuesta recogerlo, se proyecta el desglose total (caso límite de la spec). El desembolso lo vuelve a liquidar.
- **«Otra solicitud que lo recoge» no se guarda**: sale al leer la solicitud (`payoffs[].inOtherApplication`, la misma lectura de la consulta de
  productos, `RecogidasDeLaSolicitud.EnOtrasSolicitudesAsync`). La solicitud no tiene lista de avisos.
- **R8, reglas finas** (`Origination/RecogidaEnLaValidacion`): el parcial no reduce la cuota; libera de capital el valor pedido topado en el capital del
  crédito en esa medida (nunca negativo). **El consolidado del enrutamiento (quién decide) no cambia**: sigue siendo el bruto (más exposición, nivel más
  alto) y así la huella no se mueve con cada proyección; sólo la validación del endeudamiento mide después de recoger. El cupo libera por
  `DisponibleDeCupoCalculado.CapitalPorCredito` (el puerto `IDisponibleDeCupo` no cambia); la libranza, por `ConveniosDeLibranza.DescuentosVigentesSinAsync`
  (sí suma cuotas vigentes del mismo convenio). La decisión revalida la concentración igual que la radicación. `releasedByPayoffs` va nulo sin recogidas.
- **Desembolso**: la concentración se mide sobre bruto − **capital** liberado (no bruto − recogido, que con los intereses subestimaría la exposición); por
  eso las recogidas se evalúan antes de la concentración, con sus bloqueos anotados en su paso de siempre (el orden de los errores no cambia). Lo
  autorizado son los créditos de las filas vivas; `PayoffRequest` ya no se lee en ningún sitio.
- **La foto** guarda «número modo [valor] [sin la cuota causada]» separado por «; » (`FotoDeSolicitud.TextoDeRecogidas`); la etiqueta sigue siendo
  «Créditos que recoge». Las fotos viejas con el JSON de `PayoffRequest` se normalizan al compararlas (`NormalizarAsync`, en `CambiosAsync`) con las
  reglas de la migración (repetido una vez, inexistente fuera, `Partial` con su valor).
- **La huella del enrutamiento** incluye las recogidas (clase, crédito/cuenta/referencia, modo, valor pedido, cuota causada) sólo si las hay: la de las
  solicitudes sin recogidas no cambia; las que tenían `PayoffRequest` cambian una vez al desplegar (quien decide relee).
- **Pantalla**: la consulta de productos ya no manda lo marcado (el resumen sale en la pantalla con la regla del servidor y un crédito marcado que se
  canceló no tumba la consulta); el PDF manda sólo lo marcado que trajo la consulta. Al corregir sin poder cambiar las recogidas, o sin ver la tabla (sin
  `Lending.Payoffs.Read`), se mandan las guardadas tal cual. «Lo que recoge» del expediente es el componente `LoQueRecoge`.
- **e2e**: `La_recogida_de_un_credito_en_mora_es_una_reestructuracion` radicaba con el crédito ya en mora; ahora se aprueba la víspera de la fecha exigible
  y entra en mora antes de desembolsar (sin correr: va con la fase 13).

### E-07 — Desembolsar con recálculo y diferencias (T032–T037, T040, T041, T043, 2026-11-27)
- **La lista del cuerpo** (`ownLoanPayoffs` o la nueva `payoffs`, que `DisbursementInput` gana) es opcional; si viene, «la misma» es: los mismos productos, el
  mismo modo y, en un parcial, el mismo valor (al centavo). Las dos listas a la vez es `Request.BodyInvalid` (como en la solicitud, E-02). En un tramo
  siguiente no hay nada autorizado: cualquier lista es `Lending.Payoff.MustMatchApplication`. Un crédito de una fila que no existe o ya no es del
  solicitante sigue siendo `Lending.Payoff.NotRequested`.
- **Omitido**: pagado, anulado, novado o activo sin saldo (`NoBalance`). Castigado, insolvencia que negocia o pendiente de giro bloquean con
  `Lending.Payoff.NotPayable` (el motivo de `ErroresDeRecogida`); en mora, `RequiresRestructuring` con la lista de siempre. Un omitido se bloquea con el
  cerrojo (nadie lo reabre mientras tanto) pero no se pone al día ni va al contrato.
- **Lo de ahora de un parcial** es el valor pedido mientras no cubra el total (desglose `Capital = valor`, igual que la proyección de E-06); si lo cubre, es
  un total. **La causa** se completa sólo si hay diferencia por concepto: `Payments` si hay una operación `Payment`, `PaymentReversal`,
  `CreditBalanceResolution` o `ProvisionalCollection` registrada después de R; `Parameters` si cambió el modo de intereses guardado; `CurrentInstallment`
  si cambió el número de la cuota causada.
- **Reconocer** se exige con un omitido o con cualquier concepto distinto (`Causa != None`), aunque el total sea el mismo. **La tolerancia** no aplica al
  omitido (sólo sube el neto) ni a una fila sin proyección. **Las filas sin proyección** (las copiadas de `PayoffRequest`) no tienen con qué compararse:
  sin causa, sin reconocimiento, sin tolerancia; la vista previa lleva `projected` y `difference` nulos. La columna `Difference` de
  `LND_DisbursementPayoffs` sigue la regla de la entidad (`CurrentAmount − ProjectedAmount`, con proyectado 0): el informe (T053) debe mostrar la
  diferencia sólo con proyección (`ProjectedAmount > 0` o `DifferenceCause != None`).
- **`MantenerLoProyectado`** aplica `min(proyectado, ahora)`; si no cancela, va como abono (`ExtraordinaryPrincipal`) y el aviso sale en el producto y en
  los avisos de la vista previa. La fila guarda el modo **aplicado** (total o parcial).
- **T036 sin claves derivadas**: un solo pago `ExtraordinaryPrincipal` cubre lo exigible y abona el resto a capital con la elección de la solicitud
  (`PrepaymentChoice`, sin ella reducir plazo) y, si alcanza el capital por vencer, el contrato lo trata como cancelación. La gramática `CAR` no cambia.
- **Bajo el cerrojo** (después de la puesta al día) las recogidas se vuelven a evaluar con `RequisitosDelDesembolso.ReevaluarRecogidasAsync`; si cambia el
  crédito, el modo, lo que se aplica, la omisión o la diferencia, `Lending.Disbursement.PreviewChanged` con la huella nueva (`HuellaCon`). La defensa
  `Lending.Payoff.AppliedDiffers` vive en `RecogidaEnElDesembolso` (lo aplicado por ítem frente a lo pedido) y aborta antes de guardar.
- **La huella** suma por recogida el modo aplicado, la diferencia, sus causas y la omisión; sin recogidas es la de siempre.
- **Las filas `LND_DisbursementPayoffs`** se escriben después del primer guardado (la operación ya tiene Id); la operación de pago se busca por el
  `OperationPublicId` del comprobante. `AcknowledgedBy` es el nombre del actor (`Actor.Name`), sólo en las filas con diferencia u omisión.
- **La respuesta de `/disbursements`** conserva `payoffs[]` con los aplicados (la pantalla lee `receiptOperationPublicId` como `Guid`) y gana
  `omittedPayoffs[]`; los campos nuevos de cada recogida (`projected`, `current`, `difference`, `causes`, `mode`) van como en la vista previa. En la vista
  previa `kind`, `mode` y `causes` salen como texto, como el resto de `DisbursementDtos`; `detail` lleva sólo los conceptos que cambiaron, con los nombres
  de `breakdown` (api.md §1); `currentInstallment.left` va nulo hasta E2.
- **T037**: la pregunta del contrato se extrajo a `Lending/Integration/PagosParaLaValidacionPrevia` (una combinación por línea, modalidad, sub-segmento y
  sucursal del crédito con condiciones selladas, y la clase del recaudador). La vista previa hace una segunda `DecidirAsync` (pantalla, sin alerta) para
  los recogidos no omitidos; su resultado (`PrevalidacionDeRecogidas`) no se sella en la operación del desembolso —el contrato sella el suyo al aplicar— y
  se suma en `prevalidation` de la respuesta. Su bloqueo entra si no está ya el mismo código.
- **T040**: se marcan las filas de las recogidas que el contrato reversó (por la clave); las omitidas no se tocan.
- `ErroresDeRecogida.Pesos` quedó público y pone el signo antes del «$».

### E-08 — Giro nuevo, garantías del recogido y pantalla del desembolso (T038, T039, T042, T044, 2026-11-27)
- **T038, de dónde salen las recogidas del giro nuevo**: de los pagos `CAR` **vivos** (aplicados y sin reversar) cuya clave empieza por
  `CAR:{desembolso:N}:` de cualquier desembolso del mismo tramo del crédito (`RecogidaEnElDesembolso.VivasAsync`), no de las filas
  `LND_DisbursementPayoffs`: así sirve también para un crédito desembolsado antes de la 028 y pendiente de giro. El monto de cada una es lo aplicado (el
  pago menos el excedente). En una cadena de giros nuevos la clave sigue siendo la del primer desembolso del tramo.
- **La orden del giro nuevo descuenta lo recogido vivo**, no el `PayoffAmount` de la orden anterior: es la regla de FR-016 (bruto − aplicado) y deja la
  orden y el hecho con el mismo neto por construcción; si una recogida ya no está viva, no se descuenta. La operación lleva las recogidas en sus entradas.
- **T039, «liberar» es dejar en firme**: liberar una garantía y anular el pagaré siguen siendo actos de una persona (`ReleaseCollateralCommand`,
  `VoidPromissoryNoteCommand`, con su permiso y motivo); no hay liberación automática al consumir el resultado de Tesorería. Lo que cambia es la firmeza:
  `GarantiasDeCartera.FirmezaDeLaCancelacionAsync` responde `Lending.Collateral.PendingNewLoanDisbursement` (código nuevo, con `newLoanPublicId` y
  `newLoanNumber`, mensaje «Por liberar al girar el crédito {número}: …») mientras el crédito tenga un pago `CAR` vivo de un desembolso cuyo tramo tiene
  órdenes y ninguna `Disbursed` (`PorLiberarAlGirarAsync`). Un tramo sin ninguna orden queda en firme al desembolsarse (hoy ninguna forma omite la orden: el
  validador del desembolso no admite en especie). Cuenta cualquier recogida viva del crédito, también un abono que precedió a una cancelación por caja. Un
  cheque `Disbursed` que pasa a `CheckNotCashed` vuelve a dejarlo pendiente hasta que el giro nuevo quede pagado. `ConsumeTreasuryResultCommand` no
  cambia (sólo su comentario): la regla lee el estado de la orden.
- **El detalle**: `CollateralDto.ReleaseNotice` y `PromissoryNoteDto.ReleaseNotice` («Por liberar al girar el crédito {número}») en las garantías vivas y los
  pagarés no anulados de un crédito **cancelado** que espera el giro; `CreditoDetalle.razor` lo muestra junto al estado.
- **T042**: el `DesembolsoRequest` de Shared deja de tener `OwnLoanPayoffs` (y se retiró `RecogidaRequest`, que nadie más usaba) y gana
  `AcknowledgedPayoffDifferences`. «Lo que recoge» es un componente nuevo, `Components/Cartera/RecogidasDelDesembolso.razor`: `DetalleDeRecogida.razor`
  pinta un `ProductoParaRecogerDto` (la consulta de productos), no la recogida de la vista previa. Dos clases nuevas en `componentes.css`:
  `.diferencia-en-contra` (ámbar: recoger cuesta más, el asociado recibe menos) y `.diferencia-a-favor` (verde). La pantalla usaba `aviso-exito`, que no
  existe en `componentes.css`: pasó a `aviso-ok`.
- **T044**: la e2e `DesembolsoYRecogidaTests` no recorre el giro nuevo ni las garantías del recogido; no se tocó.
- **Hallazgo fuera de alcance (sin corregir)**: el giro nuevo emite la orden por `original.Amount` sin el aporte del fondo (`SubsidyAmount`) ni el
  `Subsidy` del hecho: un giro nuevo de una compra con descuento (2026-11-23) perdería la parte del fondo.

### E-09 — Contabilidad del desembolso con recogidas (T045–T049, 2026-11-27)
- **`PayoffDeductionV1`** gana, además de `ProductKind` (texto; ausente = `Loan`), `AccountRef` (`MemberAccountRefV1`) y `ReceivableRef`
  (`ExternalDocumentRefV1`), dos campos opcionales que el DS necesita para el puente: `BranchPublicId` (la sucursal contable de la oficina del producto
  recogido, la de las dimensiones de su `PagoAplicado`) y `PayerPersonPublicId` (quien paga la recogida, el deudor del crédito nuevo). Los llena
  `EmisionDeCartera.RecogidasDelHechoAsync` en `DesembolsoAsync`, el único sitio que emite el DS (desembolso y giro nuevo), así que la orden de giro
  los lleva también. Un mensaje de antes, sin ellos, se lee con la sucursal del desembolso y su deudor. `ClaseDeProducto()` es método: no viaja.
- **El DS** (T046): el capital va contra «desembolso por girar» por el neto (bruto − recogido; la parte del fondo no cambia) y contra
  `RecaudoPorAplicar` de la clase `OwnLoanPayoff` por cada recogida, con la sucursal del recogido, el pagador como tercero, el número del recogido como
  cruce y **el tipo de cruce del mapeo del recibo** (`PagoAplicado` con esa clase), no el del DS: `Partida.HechoDelCruce` (nuevo, opcional) y
  `TipoDeCruce` lo resuelven, así las dos puntas saldan aunque la contadora le dé otro cruce al DS. En especie no hay recogidas. Una recogida de otra
  clase (aportes, ahorro, factura) es el fallo `Accounting.LendingFact.PayoffKindNotSupported` hasta que E2 y E4 la sepan asentar.
- **El rol al crédito**: `RecaudoPorAplicar` es de naturaleza débito, pero el constructor no limita el lado de ningún rol (ya lo acreditaban
  `PagoReversado` y el cruce al retiro de la 014, D45). Se agregó a los roles crédito de `CreditoDesembolsado` y débito de `DesembolsoReversado` en
  `OperacionesDeCartera` (los leen la retroactividad de las reglas, el catálogo de la pantalla y la prueba de los casos). Como lo saldan dos hechos de
  Cartera con el mismo cruce, la validación de la regla y la completitud ya no avisan naturaleza distinta ni «puente con cruce» para
  `RecaudoPorAplicar|OwnLoanPayoff` (`ReglasDeLaMatrizDeCartera.NaturalezaIndiferente`); a los demás puentes, sí.
- **El reverso del desembolso también** (no estaba en las tareas; sin esto el DS nuevo no cuadraba con un giro rechazado): `DesembolsoReversadoV1`
  gana `PayoffDeductions` (opcional), que `DesembolsoReversadoAsync` copia del `CreditoDesembolsado` del desembolso que se reversa
  (`RecogidasDelDesembolsoAsync`, de su contenido guardado). El DRV es su espejo: «desembolso por girar» vuelve por el neto y el puente recibe lo
  recogido. Entre el reverso y el giro nuevo el puente queda con lo recogido (las recogidas siguen aplicadas, C11); el giro nuevo lo vuelve a acreditar
  y la anulación del crédito lo cierra con sus `PagoReversado`. Un reverso de antes (sin la lista) se contabiliza como siempre; un desembolso con
  recogidas contabilizado antes de la 028 y reversado después dejaría la diferencia entre las dos cuentas si son distintas: entra en el diagnóstico
  de R10.
- **T047**: `DesembolsoConSusRecogidas` (en `Lotes/ComprobantePorRecibo.cs`); la clave `Accounting.LendingDisbursementWithPayoffsInOneVoucher` vive
  en `COR_SystemSettings` (módulo `ACC`, `SystemParametersSeeder`, «false»), como `Accounting.LendingVoucherPerReceipt`, no en
  `COR_ParameterVersions`. Junta la unidad del desembolso con las de los recibos **que nombra** (`PayoffDeductions[].OperationPublicId` es el origen de
  cada `PagoAplicado`, cuyo recibo es el número del desembolso) en la misma fecha contable. La clave de grupo empieza por `Recibo|`
  (`Recibo|Desembolso|{op:N}`) para ir por el handler del recibo (en línea, sin lote, sin sumar); `DelRecibo` delega en `DelDesembolso`: tipo, fecha,
  origen y «registrado por» del DS, su descripción con «· recoge N créditos», primero sus líneas. El planeador lo arma antes que el recibo por recibo
  (si no, las recogidas se juntarían sólo entre ellas). Una tanda partida o un DS que no se puede contabilizar dejan comprobantes separados, que
  cuadran igual.
- **T049, defectos de la conciliación corregidos**: (1) `OrdenesAbiertasAsync` contaba como abiertas las órdenes `Rejected` y `CheckNotCashed` sin
  reemisión, pero su `DesembolsoReversado` ya le devolvió a «desembolso por girar» lo acreditado: la conciliación descuadraba por el neto, con
  recogidas o sin ellas. Abiertas son sólo las `Issued`. (2) `PuentesEnCeroAsync` espera en el puente `OwnLoanPayoff` lo recogido de los tramos con
  todos sus desembolsos reversados (`RecogidoQueEsperaElGiroAsync`). (3) Lo que explica un hecho sin procesar: el DS explica «desembolso por girar» por
  su `netToDisburse` (antes el bruto, y en especie lo que no le tocaba) y el puente por −recogido; el DRV, «desembolso por girar» por −neto (antes nada)
  y el puente por +recogido. Como las órdenes, se mide con el estado de hoy.
- Casos dorados: `01` actualizado (7.700.000 y el puente 2.300.000 en la misma cuenta, una línea por crédito), `68` (dos recogidas en dos sucursales,
  giro a tercero, puente con tercero y cruce; trae `recibos` y `esperadoEnUnComprobante`, que corre `DesembolsoConSusRecogidasTests`) y `69` (el DRV
  de 68). `escenario.json` gana la cuenta 24959503.

### E-10 — Intereses explicados, informe de recogidas y vínculos en el detalle (T051–T057, 2026-11-27)
- **T051, la explicación es una lista, no avisos**: `LiquidacionDeCreditoRecogido` gana `ExplicacionDelInteres` (renglones `RenglonDeIntereses`: clase,
  cuota, días, valor y texto) y `ReconocidoQueSeLibera`; las arma `ExplicacionDeInteresesDeRecogida.Explicar` (pura) con el estado reconstruido a D y lo que
  la liquidación ya decidió: no recalcula valores. Clases (`ClasesDeRenglonDeInteres`): `Accrued` (el corrido de la cuota en curso, de su `Desde` excluido a
  D), `FullInstallment` (los días que faltan hasta el vencimiento), `Proportional` (el motivo legal y lo que la cuota completa habría cobrado), `Returned`
  (por período calendario, lo causado de una cuota cuyo período no empieza: días de su `Desde` a D) y `RecognizedReleased` (lo reconocido al inicio del
  ciclo y no causado a D; informativo, no cambia el valor; el interés reconocido de una cuota cobrada completa no se libera). **Los días los cuenta
  `ContadorDeDias` con la convención sellada del plan** entre fechas del plan: la pantalla no los calcula. Los dos avisos que la liquidación ponía para la
  cuota completa y lo devuelto **salen de `Avisos`** (los reemplazan los renglones; `LiquidacionDeRecogidaTests` se ajustó). En UVR o sin reconstrucción no hay
  renglones. Viajan como `interestExplanation[]` y `recognizedReleased` en los productos para recoger (§1) y en la vista previa del desembolso (§3; vacíos
  en un omitido o un abono a capital), y en el PDF de la simulación. «Lo que recoge» de la solicitud no guarda días: explica desde su desglose
  (`TextosDeRecogida.InteresesDelDesglose`).
- **T053, filtros**: la vista usa los filtros comunes del centro de informes (`branch`, no `office` como decía api.md §5; `line` y `advisor` son los del
  crédito nuevo, por `AmbitoDeInformeDeCartera`) más el propio `productKind` (`Loan`, `Account`, `CommercialReceivable`, sin distinguir mayúsculas; otro valor
  es `Lending.Report.FilterInvalid`). Lista también las **omitidas** (el producto ya estaba cancelado), además de vivas y reversadas, marcadas en «Estado»
  y resaltadas; los totales no cuentan las reversadas. «Con proyección» es la huella de la fila proyectada (`ProjectionSha256`) y, sin fila, la regla de
  E-07. El interés devuelto y cobrado de lo de ahora es lo proyectado más `DifferenceDetail` (vacío sin proyección). «Cuota causada» dice el tratamiento
  (`Incluida` en E1); los valores de la cuota dejada llegan con E2. El decisor es el usuario de la decisión de aprobación (o la persona, si decidió un
  órgano); quien desembolsó, el actor de la operación. La pantalla `/cartera/informes` no tiene control para el asesor (como colocación): viaja por el enlace.
- **T054, un solo lector de los vínculos**: `Lending/Payoffs/VinculosDeRecogida` (recogidos, nuevos y por pago) une las filas `LND_DisbursementPayoffs` con
  los pagos `CAR` de la clave de la recogida sin fila (los desembolsos de antes de la 028), sin contar dos veces el pago que tiene fila (misma clave); un
  pago `CAR` con otra gramática (el cruce del retiro) no es una recogida. `LoanDto` gana `paidOffBy[]` y `paidOff[]` (`LoanPayoffLinkDto`); el libro del
  crédito (`LoanTransactionDto.Description`) dice «Recogido con el crédito …» en las filas del pago, y el informe `transactions` lo agrega al recibo. El
  estado de cuenta de la persona lo muestra en F8 (movimientos), que es donde aparecen los pagos; `SeccionesDelEstadoDeCuenta` no lista movimientos y no
  cambió.
- **T057, puerta de E1 (2026-11-27)**: `dotnet build IngenIA365ERP.slnx -m:1` sin errores; Domain 3.239, Application 9.127, Architecture 931 y Shared 774, todas
  verdes. La única que falló fue `LasConsultasDeCarteraRespetanElAlcance` por la vista nueva: se agregó `PayoffsReportQuery.cs` a su lista de consultas con
  alcance (lo aplica `AmbitoDeInformeDeCartera`).

### E-11 — Todo menos la cuota causada en el motor (T058–T062, 2026-11-27)
- **La cuota que se deja es siempre la del período en curso** (`AplicadorDePagos.CuotaCausada`): la primera que vence después de la fecha valor y cuyo período ya
  empezó; por período calendario, además, con su tramo empezado. Una cuota pedida (`SolicitudDeDistribucion.Cuota`, la que sella la decisión) sólo vale si es
  esa misma. Sin cuota en curso (la fecha es un vencimiento, la gracia, un tramo sin empezar) o con otra pedida, la modalidad es la cancelación total con el
  aviso `Lending.Payment.NoInstallmentToLeave`. Una cuota **ya vencida** (descontada y pendiente de giro) no se deja: la cubre la recogida y lo que gire la
  pagaduría sigue `Recogida.GiroPosteriorDeCreditoRecogido` (fase 9).
- **Lo que paga**: todo lo cobrable salvo las cubetas cuya cuota de orden es la dejada (también la parte de la gracia repartida que se le atribuye), con la
  prelación y el modo de intereses de la cancelación para las demás (la cuota completa nunca se cobra de la dejada; por período calendario se devuelve lo
  de un tramo sin empezar). El capital de las cuotas siguientes se paga **por cuota**, como en la cancelación (no como abono sin cuota).
- **El plan** se corta con `GeneradorDePlan.CortarDespuesDe` (no con `Regenerar`/reducir plazo, que recalcularía la cuota dejada): quedan las cuotas hasta la
  dejada tal como estaban; la entrada queda con Σ capital de esas. La reconstrucción conserva lo movido en el capital de la cuota dejada y deja en cero lo de
  las que salen (`RetirarDelPlan`). Como sólo salen cuotas cuyo período no ha empezado, no tenían nada causado.
- **El interés del período que falta** es la proyección de la cuota completa (`InteresPorCausarDeLaCuotaEnCurso`) de esa cuota; se causa en la fecha valor con
  filas `UnaccruedInstallmentInterest` (el mismo mecanismo de la cuota completa: viaja en el acompañante `AjusteDeCausacionPorFechaValor` y consume lo
  reconocido al inicio del ciclo) pero **no lo cubre el pago**: queda debiéndose (`DistribucionDePago.InteresDeLaCuotaDejada`).
- **La congelación** (`ContextoDeReconstruccion.Congelar`): desde el día siguiente, mientras la cuota no esté vencida (hasta su fecha exigible, art. 829), su
  capital no entra en la base de la corriente. Vencida e impaga causa mora como cualquiera. El seguro y la administración de la cuota **siguen causándose
  como dice el plan** hasta su vencimiento (no se adelantan): por eso `ValorParaCancelarSalvoCuota.QuedaEnLaCuota` suma el seguro y la administración del
  plan, no los causados a la fecha.
- **El anticipo**: se usa primero el de las cuotas que salen, después el general, el de la cuota dejada y el de las anteriores; así la cuota queda debiéndose
  completa y su canal la cierra al peso. Lo que sobre del de las cuotas que salen pasa al anticipo general (en la distribución, como anticipo usado de esa
  cuota y anticipo recibido sin cuota).
- **Fondos que no alcanzan lo demás**: cancelación parcial sobre todo menos la cuota (sella la cuota pedida), sin cortar el plan ni congelar nada.
- **La decisión** sella la cuota en `Installment` (aunque la haya decidido el aplicador), `LeftInstallmentRule = TalComoSeEnvio` (D13-A) y
  `LeftInstallmentInterest`, estos dos como constancia: la reconstrucción vuelve a proyectar el interés con la usura congelada a la fecha valor, como la cuota
  completa. El comprobante repetido lleva el aviso `Lending.Payment.CurrentInstallmentLeft`.
- **T062 se amplió a toda vivienda** (en UVR o con las reglas de vivienda en pesos): dejar la cuota con el interés del período sobre todo el saldo cobraría
  interés del capital que se prepaga, y la Ley 546 de 1999 no admite penalidad por prepago. Se aplica como cancelación total con el aviso
  `Lending.Payment.HousingPaidOffInFull`, también dentro de la reconstrucción en UVR (lo detecta la versión de reglas). `LiquidacionDeCreditoRecogido` gana
  `AdmiteDejarLaCuota`, `RecogidoCompletoPorque` y `TratamientoEfectivo` (pedir «Excluir» a uno que no lo admite es «Incluir»); sin reconstrucción tampoco se
  admite. **Confirmar con el dueño** la vivienda en pesos.
- **T058**: la admisión por recaudador vive en `ClasesDeMedioDeRecaudo.AdmiteModalidad`/`ValidarModalidad` (sólo `OwnLoanPayoff` y `Receipts`; también se
  rechaza `Test`) y la revisan `AplicacionDePagos.AplicarAsync` y la simulación del contrato (también en los pagos anteriores del recibo) con
  `Lending.Payment.ModalityNotAllowed`. El validador admite simular sin valor (como la cancelación total) y una cuota pedida mayor que cero. El listado y el
  detalle del contrato **no la anuncian todavía** en `AdmittedModalities` (la caja la ofrecerá con `Pagos.CuotaEnviadaAlCancelar`, fase 9). La modalidad cuenta
  como cancelación para el modo de intereses, el sello de la cuota completa y el tope legal del prepago (`AplicacionDePagos.PuedeCancelar`). Sin migración:
  `LND_LoanOperations.PaymentModality` es entero. `TextosDePagos` de Shared (pantallas) no se tocó: es de la fase 9.
- Casos dorados a mano en `Domain.Tests/Lending/Payments/Casos/` (01 causación diaria con pago tardío, reverso, otra cuota pedida y fondos insuficientes; 02 al
  inicio del ciclo; 03 período calendario con tramo en curso, sin tramo y cuota parcialmente pagada; 04 anticipo general, de la cuota dejada y de una cuota
  que sale; 05 seguro y administración), con `CancelacionSalvoCuotaTests` (simular = aplicar, valor sin aplicar, cierre al peso, causar día a día = reconstruir,
  vivienda) y `CancelacionSalvoCuotaEnElContratoTests` (Application).

### E-12 — Cuota enviada, nómina propia y recogida con la cuota causada (T063–T066, parte de recogida de T072, 2026-11-27)
- **T063**: la migración `CuotaDejadaALaPagaduria` (aditiva, los dos motores) agrega `LND_PayrollDeductionResults.LoanClosedAtResult` (falso de fábrica; lo
  sellará T067) y `LeftInstallmentNumber`/`LeftInstallmentAmount`/`LeftInstallmentSettledAt` en `LND_DisbursementPayoffs`. `DisbursementPayoff.Evaluar` recibe la
  cuota dejada y su valor (los dos o ninguno, sólo en un crédito recogido excluyéndola) y gana `MarcarCuotaDejadaPagada` (para T070) y `CuotaDejadaPendiente`.
- **`SentInstallmentState` (R6 «Nómina propia»)**: valor nuevo del enum, no campo aparte. `OwnPayroll = 4` se renombró `OwnPayrollGenerated` (mismo número; nada
  lo había guardado) y se agregaron `OwnPayrollCalculated = 5` y `OwnPayrollApproved = 6`. El contrato de obligaciones lleva su espejo
  `EstadoDeCuotaEnviada` (otros módulos no pueden nombrar `Domain.Enums.Lending`), vigilado por `LosEnumsDelContratoNumeranComoElDominio`.
- **T064a, el puerto de Nómina** `IDescuentosDeNominaParaCartera.VivosAsync(obligaciones, fecha)` devuelve todos los descuentos vivos por obligación y período:
  calculado (fila `PAY_PayrollLoanDeductions` `Calculated` de la corrida ordinaria en `Draft`/`Stale` de un período `Open`/`Calculated`), generado (novedad
  `LoanDeduction` activa de ese período sin corrida) y aprobado (fila aplicada de la corrida aprobada de un período que **no había terminado a la fecha**). Los
  vivos no se acotan por la fecha (una nómina atrasada sigue descontando). Su propio enum `EstadoDelDescuentoDeNomina`. La regla de arquitectura es una prueba
  nueva en `LaCarteraSoloSeTocaPorSusInterfaces` (un método, implementado sólo por Nómina, registrado en DI, `CuotaEnviadaDelCredito` lo usa y no lee `PAY_`).
- **T064, qué cuota y qué período**: la cuota por la que se pregunta es la primera que vence en la fecha o después (`CuotaDelEstado`; la que vence hoy todavía
  puede ir en el archivo). En libranza se mira la línea del archivo `Sent` del período que contiene su vencimiento (en un convenio quincenal, la quincena del
  vencimiento); sin resultado, enviada sin respuesta; con el último resultado (el que nadie reemplaza) pendiente de giro, descontada; con algo sin descontar,
  no descontada; descontada y girada, nada (ya está pagada). Si ese período no dice nada, lo descontado y sin girar de una línea anterior del crédito (la más
  reciente). Los archivos se buscan desde dos meses antes de la fecha. La nómina propia sólo se pregunta por los créditos que la libranza no resolvió, y se
  toma el descuento del período que contiene el vencimiento o, si no, el primero vivo.
- **T065**: el listado y el detalle del contrato llevan `SentInstallment` (`CuotaEnviadaDto`, con la frase) y anuncian la modalidad 8 sólo al recaudador que la
  admite (la recogida y Recaudo) y sólo en un crédito vigente, al día y con cuota causada (`AplicadorDePagos.CuotaCausada`: no en vivienda ni sin cuota en
  curso). La cuota enviada se consulta en lote, una vez por listado. El detalle además la agrega a sus avisos. api.md §4 decía que no había métodos nuevos en el
  puerto: se cumple (campos opcionales).
- **T066, la liquidación**: `LiquidacionDeRecogida` recibe `ReglasDeParametrosDeCartera` y `CuotaEnviadaDelCredito`; lee `Recogida.CuotaCausada` con la línea del
  crédito recogido (`CuotaCausadaConfigurada`), calcula todo menos la cuota con `ValorParaCancelarSalvoCuota` tal como está (sin tocar el motor) y lo guarda en
  `SalvoCuota` (cuota, vencimiento, valor, desglose, lo que queda en la cuota). **`ValorARecoger` sigue siendo el total**; lo que rige con un tratamiento lo da
  `Segun(tratamiento)` (valor, desglose, cuota, cuota dejada, renglones del interés sin los de la cuota dejada y la explicación). El **desglose de todo menos la
  cuota** es cada concepto sin lo de la cuota dejada, lo devuelto por período calendario de las demás y, como cierre al peso con lo que cobra el contrato, el
  anticipo que usa (si la resta saliera negativa iría a otros cargos). `AdmiteDejarLaCuota` ahora exige además que haya cuota causada a D
  (`MotivoSinCuotaQueDejar`), no sólo que no sea vivienda.
- **Resolver el tratamiento** (`ResolverTratamiento`): «Excluir» lo excluye solo; «Preguntar» toma lo pedido (sin pedir, incluir); «Incluir» rechaza que se pida
  excluir (`Lending.Payoff.InstallmentTreatmentNotAdmitted`, código nuevo). Después `TratamientoEfectivo`: si el crédito no admite dejarla, se incluye. La
  solicitud lo aplica estricto al guardar (`PrepararAsync`) y sin rechazar al radicar (`ReproyectarAsync`); el desembolso lo vuelve a resolver con el parámetro
  de la fecha valor y la elección guardada, sin rechazar: así una solicitud proyectada a una D sin cuota causada, con «Excluir», la deja si al desembolsar la
  hay (con la causa `CurrentInstallment`). Un parcial nunca la toca; el validador de la solicitud admite `Exclude` sólo en una recogida total.
- **Candidatos**: `toPayOff` y `breakdown` son los del tratamiento que rige sin elegir («Excluir» → todo menos la cuota); `toPayOffExcludingCurrentInstallment` y
  `breakdownExcludingCurrentInstallment` (nuevo), los de dejarla; `currentInstallment` gana `left`, `parameter`, `explanation`, `includedExplanation`,
  `leftExplanation` y `notLeftReason`; `sent` gana la cuota, el convenio, lo corriente y vencido, la corrida y la frase. Con lo marcado (el PDF), un «Preguntar»
  marcado dejándola se muestra con los valores de dejarla y `selectedInstallmentTreatment`. El PDF agrega la explicación de la cuota.
- **Desembolso**: `RecogidaEvaluada` gana `Tratamiento`, `CuotaDejada`, `ValorDejado` y `Segun`; la recogida aplica `PayoffExceptCurrentInstallment` con la cuota
  que se deja (si al aplicar ya no es esa, el contrato cancela todo y `AppliedDiffers` lo detiene); la huella y la relectura bajo el cerrojo comparan la cuota
  dejada; la respuesta lleva `leftInstallment` y `leftInstallmentNumber`; la vista previa, `currentInstallment.left`, `sent` y `explanation`. El texto de la
  explicación vive en `TextosDeCuotaEnviada` (Application), y el detalle de la solicitud la arma con el estado y el archivo guardados (no vuelve a consultar).
- **Vínculos y detalle**: `VinculoDeRecogida` y `LoanPayoffLinkDto` llevan la cuota dejada, su valor, cuándo se pagó y el canal (la forma de pago del recogido);
  el detalle del crédito dice «Cuota dejada a la pagaduría/nómina: cuota N por $X» mientras no esté pagada. El informe `payoffs` dice «Dejada a su canal: cuota
  N por $X».
- **Pendiente o dudoso**: la pregunta abierta al dueño sobre el interés de la cuota dejada dentro del tope legal del prepago (no se tocó el motor); T067–T071 y la
  parte de la caja de T072 quedaron en E-13.

### E-13 — Libranza con crédito cerrado, giro posterior, cuota dejada y caja (T067–T074, parte de caja de T072, 2026-11-27)
- **Crédito cerrado** es cancelado, anulado o novado (`PendientesDeGiro.CreditoCerrado`, lo mismo que el contrato no deja pagar); el **castigado no**: sigue
  recibiendo pagos (recuperación) y su descuento se trata como siempre. `ConsumoDeLibranza` lee el estado ya bajo el cerrojo y sella `LoanClosedAtResult`; ese
  resultado no deja no descontado, ni regla del convenio, ni aviso al deudor, ni cuenta para la alerta `Cartera.NoAplicadoDePagaduria` del archivo.
- **`PendientesDeGiro.Consulta` mira el estado del crédito al consultar**, no sólo la marca (subconsulta por la llave del crédito: barata). Así salen también los
  resultados de antes de la columna, y si el crédito se reabre (se anula el crédito nuevo y se reversa la recogida) su pendiente vuelve. La marca queda para
  decir «descuento de un crédito cancelado»: el cuadre `payroll-deduction-reconciliation` lo pone en el motivo y deja el pendiente de giro en cero. Por la
  consulta salen también la referencia que exige el contrato, la tarea de giros atrasados (alerta y aviso al deudor), la consulta del convenio, el informe de
  pagadurías y la proyección a centrales. `CargadorDeReconstruccion.PendientesDeGiroAsync` no se tocó (el brief lo prohíbe y no hace falta): un crédito
  cancelado no tiene cuota que cubrir, así que el pendiente no cambia su mora.
- **El rechazo con sucesor**: `SucesorDeCreditoRecogido` (Lending/Payoffs) toma el último vínculo de recogida total no reversado de un crédito `PaidOff`
  (`VinculosDeRecogida`: fila `LND_DisbursementPayoffs` o pago `CAR` anterior a la 028) y `Recogida.GiroPosteriorDeCreditoRecogido` (general, a hoy).
  `ErroresDePagos.NoPagableRecogido` lleva en `data` `successorObligationPublicId`, `successorObligationNumber`, `successorFrom` (fecha valor del desembolso
  nuevo) y `afterPayoff`, además de lo de siempre. Las claves, los valores y el lector viven en el contrato (`Common/Integration/Lending/RecogidaEnElContrato.cs`,
  `GiroPosteriorDeCreditoRecogido.Leer`): Recaudo no lee parámetros ni enums de Cartera. `AplicacionDePagos.NoPagableAsync` arma el rechazo en la aplicación y
  en la simulación del contrato. Un crédito cancelado de otra forma, anulado o novado recibe el rechazo de siempre.
- **Recaudo** (`GirosDePagaduria`): `ResultadoNoInteractivo` y `ParteRechazada` ganan `Data` (el rechazo de la simulación o de la aplicación).
  - `AlCreditoNuevo`: otra línea no interactiva —recibo de la pagaduría, el de esa línea del giro (el primer intento no numeró)— al crédito nuevo con valor libre
    (cubre primero lo exigible y deja el resto como anticipo), clase `Transfer` y **sin** referencia de la línea del archivo: lo descontado era del crédito
    viejo, y con `ExternalPaymasterDeduction` el crédito nuevo exigiría la referencia de sus propios pendientes. Fecha valor: la del descuento, nunca antes de
    `successorFrom`; admite fecha tardía. Clave `REC:{giro:N}:{línea:N}:sucesor` (`GirosDePagaduria.ClaveDelSucesor`). Aplicada, la línea queda `Applied` con ese
    recibo; si el crédito nuevo tampoco lo recibe, como siempre.
  - `AFavorDelAsociado`: no había un hecho para pasar un saldo de la pagaduría a una persona; se usa el más cercano, `SaldoAFavorDePagadorResuelto` del saldo de la
    pagaduría con la forma nueva `TransferredToAssociate` (`PayerCreditBalanceMovementKind` 10; se guarda como entero, sin migración) y las líneas
    `SaldoDePagadurias` (débito, la pagaduría) y `SaldoAFavorDePagadores` (crédito, el asociado), roles que el hecho ya admitía (sin cambio en la matriz). El
    saldo del asociado nace `Payer` con origen `PaymasterUnapplied` y la línea del giro lo nombra; reprocesarla se rechaza («resuélvalo en Saldos a favor») y no
    hay alerta de no aplicado.
  - **Excedente**: si la línea se aplica dejando excedente (saldo de la pagaduría), Recaudo le pregunta a Cartera simulando un valor libre sobre el crédito: si
    quedó cancelado y lo había recogido otro, el rechazo trae la regla. `AlCreditoNuevo` aplica el excedente al crédito nuevo (misma clave de sucesor) y baja el
    saldo de la pagaduría (`AppliedByReceipt`); `AFavorDelAsociado` lo traslada. Sin recogida, como siempre.
- **Seguimiento de la cuota dejada** (`SeguimientoDeCuotaDejada`): la marca `AplicacionDePagos` después de registrar el pago (estática, en la misma transacción;
  no escribe el libro): pagada si el pago cancela el crédito o la cuota queda sin capital ni conceptos pendientes. Un reverso no la desmarca (la marca es «una
  vez»). El aviso al asesor es una **notificación** (`SendNotificationCommand`, en la aplicación y por correo) a quien radicó la solicitud del crédito nuevo (o,
  sin él, quien la creó), desde `ConsumoDeLibranza` (pagaduría) y `NoDescontadosDelLote` (nómina propia): las alertas van por permiso, no a una persona, y no se
  agregó tipo de alerta.
- **Caja**: el listado del contrato gana `SentInstallmentAtPayoff` (`Pagos.CuotaEnviadaAlCancelar` de la línea, leído por `InteresesAlCancelar`, sólo con cuota
  enviada), `PayoffExceptSentInstallmentAmount` y `SentInstallmentLeftAmount` (sólo si la cuota sigue en su canal sin respuesta —archivo sin resultado o nómina
  sin aprobar, `CuotaEnviada.SePuedeDejar`—, es la causada que deja la modalidad 8 y el recaudador la admite). `ReglasDeLaCaja` rechaza la modalidad 8 si la
  línea dice «Incluir» o falta el valor: de fábrica la caja cancela todo, como siempre. Pantalla: F7 propone la modalidad 8 con «Excluir»; con «Preguntar», una
  casilla en el ítem; el aviso va en el ítem siempre que se cancela y la lista de lo pagable dice «cuota N enviada a la pagaduría/nómina». La lógica es pura, en
  `Shared/Services/Recaudo/CuotaEnviadaEnLaCaja`. La pantalla no tiene carga nueva (todo llega con «lo que puede pagar»): ninguna zona nueva de `IndicadorDeCarga`.
- **Pendiente o dudoso**: lo que la pagaduría gire por un crédito cancelado **en caja** (no recogido) sigue yendo a saldo de la pagaduría (el parámetro es de la
  recogida); el reverso del pago que cerró la cuota dejada no la desmarca; la cuota descontada pendiente de giro sólo se avisa en caja (vencida, no se deja).
### E-14 — Aportes y ahorros en el desembolso (T075–T083, 2026-11-27)
- **T075, el cerrojo (sin riesgo de interbloqueo)**: el orden fijo de `ICerrojoDeCartera` es setup → personas → créditos → cuentas → numeración, y lo siguen
  todos los caminos que tocan cuentas: el contrato de cuentas (setup, titulares, cuentas con las de los titulares embargados), la liquidación del retiro
  (setup, retirado y codeudores, créditos, cuentas) y el débito automático (setup, titular, crédito y cuenta con `Saltar`). El desembolso toma setup → deudores
  (el solicitante, que es el titular de las cuentas recogidas, y las partes) → créditos (recogidos y del tramo) → **las cuentas recogidas y, si el titular tiene
  un embargo vigente, todas las suyas** (la consignación recalcula el embargo y reescribe los bloqueos de todas sus cuentas: `CuentasDeLasPersonasEmbargadasAsync`,
  como el contrato), **antes de la puesta al día y de numerar nada**. Ningún camino toma una persona o un crédito después de una cuenta
  (`ElOrdenDelCerrojoEsPersonaYLuegoSusCuentas` lo vigila por método), y la numeración (crédito, operación, pago, operación de cuenta) va siempre al final y en
  el mismo orden relativo que el retiro (pago antes que la operación de cuenta). La anulación toma setup → deudor → créditos → cuentas de las consignaciones.
- **T076, el camino interno**: `ConsignacionEnCurso.Interna` (init, que `ContratoDeCuentasDeCartera` nunca enciende) y `ConsignacionEnCuentas.ClaseDeMedio(…,
  interna)`: sólo con él, `OwnLoanPayoff` admite `InternalOffset` (y nada más). El puerto público sigue rechazando `CAR` por el registro de recaudadores y, si
  llegara, por la clase de medio; la clave `CAR:{op:N}:{cuenta:N}` ya la admitía el validador (es la gramática de la recogida).
- **T077, `RecogidaDeCuentas`** (`Lending/Payoffs`): lee las cuentas a D (`CuentaRecogible`: producto, línea, cuotas pendientes que vencen a D o antes, si la
  línea admite abono, bloqueo por estado —sólo activa o inactiva— o titular fallecido), valida (CDAT, admitida, modo del producto y de la línea, abono > 0, cuotas
  pendientes, y el **reparto de la consignación sin escribir** —`ConsignacionEnCuentas.RepartirAsync`— para el límite de participación y el excedente), evalúa al
  desembolsar y consigna. **Por cuotas va `SpecificInstallments` con las cuotas pendientes a la fecha valor** (exactas, no «todas las pendientes»: el reparto de
  `Installments` tomaría también las que vencen después); **el abono va `Free`** (extraordinario en aportes, saldo en ahorros). Las cuentas van **aparte de los
  créditos**: `RequisitosEvaluados.RecogidasDeCuentas` (y `PrevalidacionDeCuentas`), `RecogidaDeCuentaEvaluada`; `Recogido`, el neto, el reconocimiento, la
  tolerancia, la huella (`accountPayoffs`, sólo si hay cuentas: sin ellas la huella no cambia) y la relectura bajo el cerrojo (`RecogidasReevaluadas`) las
  cuentan. Una cuenta cancelada o por cuotas sin nada pendiente se **omite** (como un crédito ya cancelado: el neto sube y se reconoce); la diferencia de una
  cuenta es por pagos (menos) o por las cuotas que vencieron desde la proyección (más, `Accrual`). La consignación sella la validación previa de la vista previa
  (un hecho representativo por línea, naturaleza, clase, oficina y embargo, con `ContratoDeCuentasDeCartera.Representativo` y el recaudador `CAR`). La concentración
  y las validaciones de la solicitud (R8) no cambian: una cuenta no es exposición ni libera cuotas de crédito.
- **La consignación guarda en la transacción del desembolso** (`ConsignacionEnCuentas.AplicarAsync` llama a `SaveChanges`): el crédito nuevo y su operación se
  guardan antes de lo que decía el comentario de E1, pero todo sigue en la misma `TransaccionExplicita` y una falla la revierte entera.
- **Las filas `LND_DisbursementPayoffs`** de una cuenta: `ProductoRecogido.Cuenta`, el modo `Installments`/`Deposit`, sin tratamiento de cuota ni modo de
  intereses, `AccountOperationId` la operación de la cuenta. **La respuesta y la vista previa** llevan la cuenta con `kind = Account`, `productPublicId` (y
  `loanPublicId` vacío), y en `/disbursements` `accountOperationPublicId` (también en el campo del comprobante); `omittedPayoffs[]` gana `kind` y `productPublicId`.
- **`PayoffDeductionV1.DepositFactType`** (nuevo, opcional, aditivo): el hecho con que la cuenta recibió la recogida (`AporteRecibido` o
  `ConsignacionEnCuentaAplicada`); el DS cruza el puente con el mapeo de ese hecho (`Partida.HechoDelCruce`), el **número de la cuenta como cruce**
  (`Partida.Cuenta`, no `Credito`), la sucursal de la oficina de la cuenta (la completa `RecogidasDelHechoAsync`) y el deudor como tercero: las mismas
  dimensiones con que la consignación debita `RecaudoPorAplicar`/`OwnLoanPayoff`. El fallo `PayoffKindNotSupported` queda sólo para las facturas (E4). El DRV
  (reverso del desembolso) lo espeja igual.
- **El giro nuevo y la conciliación**: `RecogidaEnElDesembolso.VivasAsync` suma las consignaciones vivas (`RecogidaDeCuentas.VivasAsync`), así el giro nuevo
  las vuelve a descontar (H6) y `RecogidoQueEsperaElGiroAsync` las espera en el puente.
- **T079**: la anulación reversa cada consignación viva por `ReversionDeCuentas` (`{clave}:reverso`, `OperacionDeCuentaReversada` con la clase `OwnLoanPayoff`
  del original) y marca sus filas.
- **T080, completitud**: Contabilidad no lee parámetros de Cartera; el catálogo del puerto `IDimensionesDeCartera` gana `PayoffDepositFactTypes` (los hechos de
  la consignación de los productos que `Recogida.ProductosAdmitidos` admite, general o de alguna línea de colocación activa) y la completitud pide su **mapeo con
  la clase `OwnLoanPayoff`** (sale en `unmappedOperations` si el general resuelve y el de la clase no). La regla `RecaudoPorAplicar`/`OwnLoanPayoff` ya se pedía
  siempre. Con `Accounting.LendingDisbursementWithPayoffsInOneVoucher`, `ComprobantePorRecibo.Recogida` junta también la consignación de una cuenta recogida.
- **Pantallas (T081)**: `ProductosParaRecoger` lista las cuentas (producto, cuotas pendientes en el detalle, modo cuotas/abono con su valor; de fábrica «La
  cooperativa no los recoge»), `RecogidaMarcada` gana `Kind` y el resumen suma lo pendiente o el abono sin liberar cuotas de crédito; `LoQueRecoge` ya era
  genérico; `RecogidasDelDesembolso` y el resultado del desembolso dicen «Se consigna» y lo consignado. Sin zonas de carga nuevas (todo llega con la consulta).
- **Pendiente o dudoso**: el recorrido completo del comando del desembolso con cuentas (orden por bruto − aplicado con la consignación) no tiene prueba unitaria:
  el escenario de desembolso y el de cuentas son bases distintas; va con la e2e T098. La operación de la cuenta lleva el recibo del desembolso
  (`ReceiptNumber`) y el motivo «Por el desembolso … del crédito …» (`ConsignacionEnCurso.Motivo`, sólo por el camino interno), que ve el libro de la cuenta.
- **El nombre `DepositFactType`** (no `AccountFactType`): `LosHechosDeCarteraNoLlevanCuentas` rechaza en los contratos de Cartera toda propiedad que empiece por
  `Account` (FR-126: un hecho no lleva cuentas contables); por lo mismo, el catálogo del puerto lo llama `PayoffDepositFactTypes`.
- **T083, puerta de E3 (2026-11-27)**: `dotnet build IngenIA365ERP.slnx -m:1` sin errores; Domain 3.265, Application 9.196, Architecture 933 y Shared 790, todas
  verdes. La única que falló fue `LosHechosDeCarteraNoLlevanCuentas` por el nombre del campo nuevo (corregido arriba).

### E-15 — D13 ajustada: la cuota dejada con el interés proporcional dentro del tope legal del prepago (T062a, 2026-11-27)
- **Quién decide**: `InteresesAlCancelar.CuotaDejadaAsync` (Application; un solo sitio, sin valores legales en Domain) devuelve `ReglaDeCuotaDejada`:
  `Proporcional` con `WithinLegalPrepaymentCap` o `LegalPrepaymentCapUnknown` (el mismo `TopeAsync` que `ModoAsync`: SMMLV y `PREPAGO_TOPE_SMMLV` a la fecha del
  desembolso) o `TalComoSeEnvio` (fuera del tope, D13-A). No lee `Recogida.InteresesProporcionales` ni `Pagos.InteresesAlPagarAntesDelVencimiento`. Vivienda no
  deja cuota (si llegara, `Housing`). `AplicacionDePagos.CuotaDejadaAsync` la usa en el reparto (la recogida y la caja, por el contrato) y en el listado;
  `LiquidacionDeRecogida` en la proyección, los candidatos y la vista previa: dicen lo mismo.
- **El motor** recibe la regla en `SolicitudDeDistribucion.CuotaDejadaProporcional` (y `ContenidoDePago`, `DistribucionDePago`). Con ella, lo que se causa en la
  cuota dejada en la fecha valor es `EstadoDelCredito.InteresPorCausarSobreSuCapital(n)`: la misma proyección de la causación que la cuota completa
  (`ReconstruccionDeCredito.InteresPorCausarSobreSuCapital`, usura congelada a la fecha) con la base acotada al capital de `n` desde el día siguiente
  (`EntradaDeReconstruccion.SoloSuCapital`, sólo de la proyección, con su corte de ventana). Lo causado hasta la fecha, sobre todo el saldo, no se toca; la
  congelación fija el valor. **Lo que se cancela no cambia** (sólo lo que queda en la cuota). Aviso nuevo `Lending.Payment.LeftInstallmentProportional`; la
  huella de la simulación lleva la regla sólo si es proporcional.
- **Lo causado de más**: con la causación diaria (también por período calendario, que causa día a día) no hay interés causado adelantado en la deuda; lo que
  sí está adelantado es lo **reconocido** al inicio del ciclo o del tramo. La reconstrucción cierra ese día lo reconocido del interés de la cuota dejada con el
  proporcional (`_cierresDeCuota`) y devuelve lo que ya no se causará con la operación de la recogida (`RecognitionRelease` con su `OperacionId`, que viaja en
  el acompañante `AjusteDeCausacionPorFechaValor` como cualquier devolución de una cancelación). Con D13-A no cambia nada (casos 01–05 intactos).
- **El sello**: `LeftInstallmentRule = Proporcional` y `LeftInstallmentRuleReason` (nuevo) en la decisión; `Contenido()` lee `Proporcional` (cualquier otro
  valor, o ninguno, es D13-A: los pagos de antes no cambian). El comprobante repetido lleva los dos avisos.
- **Lo que se muestra**: `ValorSalvoCuota` y `RecogidaSalvoCuota` ganan `Proporcional`/`Regla`, `QuedaTalComoSeEnvio` y `DeMasSiSePagaComoSeEnvio`. El
  listado del contrato gana `SentInstallmentLeftRule`, `SentInstallmentLeftRuleReason` y `SentInstallmentAsSentAmount` (opcionales); `currentInstallment` de
  los candidatos y de la vista previa, `leftRule`, `leftRuleReason` y `leftAsSent`. El texto de la cuota (`TextosDeCuotaEnviada.EnLaRecogida` y
  `.Proporcional`, y en la caja `CuotaEnviadaEnLaCaja.Aviso`) dice «La cuota N se deja con el interés proporcional (dentro del tope legal del prepago sin
  penalidad, Ley 1555 de 2012): $X…» y qué pasa con lo de más si su canal la paga completa ($Y): de la pagaduría, según
  `Recogida.GiroPosteriorDeCreditoRecogido`; de la nómina, vuelve al neto del empleado; en la caja, saldo de la pagaduría. **No se agregó un renglón** a la
  explicación del interés: la pantalla del desembolso y el detalle ya muestran el texto de la cuota y lo habrían repetido.
- **La caja sin valores nacionales** no llega a aplicar: el pago de Recaudo sella el tope del prepago y sin SMMLV se rechaza (lo de siempre). El motivo
  `LegalPrepaymentCapUnknown` lo aplica la recogida (que no sella el tope).
- **Casos dorados nuevos** (`Domain.Tests/Lending/Payments/Casos/`, el crédito de los casos 01–03: 1.200.000, 3 cuotas de 400.000 de capital, 1,5 % MV, diaria
  0,0005, 30/360; la cuota 2 vence el 15-05 y su interés del plan es 12.000):
  - **06, causación diaria**: R el 30-04 → causado 6.000 (800.000 × 0,0005 × 15) + proporcional 3.000 (400.000 × 0,0005 × 15) = 9.000; queda 409.000 (tal como se
    envió 412.000); se cancelan 400.000. «base»: el canal paga 409.000 y cierra al peso; «pago-por-lo-enviado»: paga 412.000, excedente 3.000; «reverso».
  - **07, al inicio del ciclo** (reconocido el 16-04: 12.000): igual deuda que 06; de lo reconocido se consumen 6.000 + 3.000 y se devuelven 3.000 el 30-04 con R.
  - **08, período calendario con reconocimiento el 01-05 (ingreso directo, como COOFLOPAL)**: R el 10-05 → causado 10.000 (25 días sobre 800.000) + 1.000
    (5 días sobre 400.000) = 11.000; queda 411.000 (tal como se envió 412.000); de lo reconocido (6.000) se consumen 4.000 + 1.000 y se devuelve 1.000 con R.
    «pago-por-lo-enviado»: excedente 1.000.
- **Pruebas que cambiaron**: las de la cuota tal como se envió por el contrato (`CancelacionSalvoCuotaEnElContratoTests`, «Con_Excluir…» de
  `RecogidaConCuotaCausadaTests`) ponen a Ana fuera del tope (`PagosDePrueba.CrearAsync(topeSmmlv: 1m)`, parámetro nuevo); las del proporcional son
  `CuotaDejadaProporcionalEnElContratoTests`, «Con_Excluir_dentro_del_tope…» y la teoría del listado de la caja (sin tope, dentro y fuera). Las que corrían sin
  los valores nacionales ya son proporcionales y siguen verdes (liquidación y contrato dicen lo mismo).
- **Puerta (2026-11-27)**: `dotnet build IngenIA365ERP.slnx -m:1` sin errores; Domain 3.277, Application (filtro `Lending|Receipts.CashDesk|Receipts.Paymasters`)
  3.145, Architecture 933 y Shared 791, todas verdes.
