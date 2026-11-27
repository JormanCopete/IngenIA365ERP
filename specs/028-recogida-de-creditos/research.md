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
(cuentas y créditos) frente al del desembolso, para no abrir un interbloqueo.

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
