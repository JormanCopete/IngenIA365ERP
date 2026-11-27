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
