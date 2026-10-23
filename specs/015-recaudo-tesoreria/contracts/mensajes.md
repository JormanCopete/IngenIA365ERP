# Mensajes de Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | Molde: `013/contracts/mensajes.md` y `014/contracts/mensajes.md`

Catálogo de lo que Recaudo (`REC`) y Tesorería (`TES`) reciben y emiten por la bandeja de la 012, con Cartera y
Captaciones (`CAR`), Comercial (`INV`), Nómina (`NOM`) y Contabilidad. Sobre, serialización, entrega garantizada, claves
de evento, dependencias, estados de entrega y reproceso son los de la 013 (§4 a §8, §14) **sin cambios**. Los hechos
a Contabilidad están en [contabilidad.md](contabilidad.md); los mensajes nuevos o versionados de otras features, en
[contratos-v2.md](contratos-v2.md).

## 1. Reglas comunes

- **Un consumidor por destino**: `DestinoRecaudo` consume `Receipts`; `DestinoTesoreria` consume `Treasury`. Los dobles
  `RecaudadorDePrueba` y `TesoreriaDePrueba` se **retiran** en el mismo despliegue (FR-223); si quedara uno, el arranque
  falla con `Integration.Destination.DuplicateConsumer` (`UnSoloConsumidorPorDestino`).
- **Recibo único**: cada mensaje consumido deja su fila en `RCD_InboundMessages` o `TES_InboundMessages` (único por
  `MessagePublicId`) en el mismo `SaveChanges` que su efecto; repetir no cambia nada (FR-151).
- **Evaluación**: `Processed` o `Rejected` con código y motivo, visibles en la bandeja con reproceso (FR-015).
- **Quién origina**: todo mensaje de Recaudo y Tesorería lo origina **una persona** (la que confirma, carga o registra),
  nunca el proceso (`EmisionesDeProcesoPermitidas` ya lo exige para `TES` y `REC`; FR-174). Los consumidores corren como
  «Proceso de integración» y no emiten mensajes de negocio salvo los hechos contables de su efecto.
- **Versiones**: lo emitido conserva su versión; un tipo o versión no adoptado por el destino queda pendiente sin
  intentos y la bandeja lo muestra (FR-149). Antes de activar, los mensajes de negocio quedan pendientes en la bandeja
  (FR-150).

## 2. Catálogo

### 2.1 Lo que recibe Recaudo (destino `Receipts`)

| Tipo y versión | Emisor | Consumo | Entrega |
|---|---|---|---|
| `PagoReversadoPorCartera` v1 | `CAR` (créditos) | `ConsumeLendingPaymentReversalCommand`: registra la aplicación reversada por el módulo (FR-059, FR-195) | R0 |
| `ConsignacionReversadaPorCartera` v1 | `CAR` (captaciones) | ídem para consignaciones | R0 |
| `DescuentosPorPagaduria` v1 · v2 | `CAR` | `ConsumePayrollDeductionFileCommand`: guarda el archivo y sus líneas (FR-075) | R0 |

### 2.2 Lo que emite Recaudo (a `Lending`)

| Tipo y versión | Cuándo | Entrega |
|---|---|---|
| `EnvioDeDescuentosConfirmado` v1 | al confirmar el envío (FR-076) | R0 |
| `ResultadoDeDescuentos` v1 · v2 | al devolver la respuesta de la pagaduría (FR-077); la versión sigue a la del archivo | R0 |

### 2.3 Lo que recibe Tesorería (destino `Treasury`)

| Tipo y versión | Emisor | Consumo | Entrega |
|---|---|---|---|
| `OrdenDeGiroEmitida` v1 | `CAR` (desembolsos) | `ConsumePaymentOrderCommand`: entra a la bandeja única (FR-107) | T0 |
| `OrdenDeGiroEmitida` v2 | `CAR` (desembolsos con v2, captaciones) | ídem, con `OrderKind` y cuenta | T0 |
| `AnulacionDeOrdenDeGiro` v1 | `CAR` | `ConsumePaymentOrderVoidCommand` (FR-115) | T0 |
| `ArchivoDeDesembolsosGenerado` v1 · `ArchivoDeDesembolsosAnulado` v1 | `CAR` (E-01 c) | `ConsumeLendingDisbursementFileCommand` (FR-108, D16) | T0 |
| `FacturaDeProveedorPorPagar` v1 · `AnulacionDeFacturaDeProveedor` v1 | `INV` (E-03) | órdenes de pago de proveedores | T2 |
| `DispersionDeNominaGenerada` v1 | `NOM` (E-05) | compromiso y conciliación | T2 |

### 2.4 Lo que emite Tesorería

| Tipo y versión | Destino | Cuándo | Entrega |
|---|---|---|---|
| `ResultadoDelGiro` v1 | `Lending` | al registrar un resultado (FR-118), siempre a nombre de la persona | T0 |
| `PagoDeFacturaDeProveedor` v1 | `Inventory` | al girar el pago de una factura (E-03) | T2 |
| `SaldoDelFondoDeLiquidez` v1 | `Lending` | al pedirlo Captaciones o al cierre de cada día (E-02 e); lo origina la persona que concilia o el tesorero | T2 |

### 2.5 Hechos a Contabilidad

Los de [contabilidad.md](contabilidad.md) §3 y §4 (`REC → Accounting`, `TES → Accounting`). `ElCatalogoDeMensajesEsUnico`
se amplía con estos tipos y con los de §2.2 y §2.4 (hoy fija 1 tipo `TES` y 3 `REC`).

## 3. Consumo de Recaudo

### 3.1 `PagoReversadoPorCartera` v1 y `ConsignacionReversadaPorCartera` v1

Con `Collector.IdempotencyKey` Recaudo encuentra la porción (`RCD_ReceiptApplicationPortions.IdempotencyKey`), su
aplicación y su recibo.

| Situación | Efecto | Evaluación |
|---|---|---|
| Porción de un recibo vigente | `RCD_ModuleReversals` con la causa: si el aviso no la trae, `PendingConfirmation` y una persona la confirma (`MoneyReceived` → saldo a favor del pagador; `MoneyNotReceived` → baja del medio sin saldo a favor); la aplicación pasa a `ReversedByModule`; alerta `Recaudo.AplicacionReversadaPorModulo` al supervisor; hecho `AplicacionReversadaPorModulo` al confirmar la causa | `Processed` |
| Recibo ya anulado o porción ya reversada | sin efecto (US5-5) | `Processed` |
| Clave que Recaudo no conoce | — | `Rejected` con `Receipts.Reversal.UnknownApplication` (FR-063) |
| Clave de otro recaudador | — | `Rejected` con `Receipts.Reversal.NotOurs` |

Recaudo **nunca** devuelve ni reaplica solo (FR-059): el saldo a favor queda por resolver.

### 3.2 `DescuentosPorPagaduria` v1 y v2

Guarda `RCD_PayrollDeductionFiles` y sus líneas con la versión del mensaje. Una versión que reemplaza a otra
(`supersedesFile`) deja la anterior `Superseded` si no se había enviado; si ya se había enviado, `Rejected` con
`Receipts.Paymaster.SupersedesSentFile` y alerta (Cartera ya lo impide: `Lending.PayrollFile.AlreadySent`). La pantalla
muestra el archivo por pagaduría, convenio y período con sus totales y su descarga (adjunto no borrable de Cartera,
lectura por `PublicId`).

## 4. Emisión de Recaudo

### 4.1 `EnvioDeDescuentosConfirmado` v1

Origen: `RCD_PayrollFileSendings`; clave `Confirmation:{archivo:N}`; `related` = la versión del archivo; tercero = la
pagaduría. Campos de la 013 §12.5 sin cambio (`fileMessageId`, `filePublicId`, `fileVersion`, `fileSha256`, `sentAt`,
`channel`, `reference`, `sentBy`). Una respuesta `HashMismatch` o `Superseded` de Cartera vuelve como evaluación
negativa y la pantalla la muestra (US7-4).

### 4.2 `ResultadoDeDescuentos` v1 y v2

Origen: `RCD_PaymasterResponses` (la versión de la respuesta); clave `Confirmation:{respuesta:N}`; `related` = el
archivo. v1 cuando la pagaduría responde línea a línea y el archivo era v1; v2 (con `fileGroupCode` y `conceptLines`) si
el archivo era v2. Si Cartera lo rechaza (`Lending.PayrollFile.LineMismatch` con la lista en `data`), la respuesta queda
`RejectedByLending` y se corrige con otra versión (FR-077). Si Recaudo no adoptó la v2 en el registro, Cartera no genera
archivos v2 y la pantalla de pagadurías dice qué versión adoptar (US7-5, `Receipts.Paymaster.VersionNotAdopted`).

## 5. Consumo de Tesorería

### 5.1 `OrdenDeGiroEmitida` v1 y v2

`ConsumePaymentOrderCommand` crea `TES_PaymentOrders` (origen `LendingDisbursement` o `DepositsPayout`), en un
`SaveChanges` con su recibo:

| Situación | Efecto | Evaluación |
|---|---|---|
| Orden nueva válida | `Received` → validación (§5.3) → `Ready` o `Pending` con motivos | `Processed` |
| Misma orden repetida (`OriginOrderPublicId`) | sin efecto (US10-6) | `Processed` |
| `NetAmount ≤ 0` | `RejectedOnArrival` con `Treasury.Order.InvalidAmount` (US10-7) | `Rejected` |
| `PaidByFile = true` | `Ready` con `PaidByLendingFile`; nunca entra a un lote (FR-108) | `Processed` |
| `ReplacesOrderPublicId` | enlaza `ReplacesOrderId` (cadena de regiros, FR-121) | `Processed` |

### 5.2 `AnulacionDeOrdenDeGiro` v1

| Estado de la orden | Efecto |
|---|---|
| `Received`, `Pending`, `Ready` | `Voided`; emite `ResultadoDelGiro` `Voided` a nombre de **la persona que anuló en el origen** (`voidedBy` del sobre como usuario de origen) |
| `InBatch` o `Approved` en un lote sin archivo enviado | sale del lote (el ítem queda `Removed`, el lote se recalcula) y `Voided` con su resultado |
| `Sent` | `VoidRequested = true`; sigue hasta conocer el resultado (FR-115) |
| `Disbursed`, `Rejected` | sin efecto; `Processed` (el origen ya tiene el resultado) |

Excepción a «una persona registra»: el resultado `Voided` responde a la anulación de una persona del origen y la nombra
a ella; no lo decide ningún proceso (FR-215).

### 5.3 Validación de una orden (FR-109, FR-203, FR-210)

En el consumo y en cada armado de lote (`ValidadorDeOrdenes`, sin escribir nada fuera de la orden):
1. beneficiario existe en el maestro;
2. cuenta destino informada y **registrada activa** en `COR_PersonBankAccounts` del beneficiario
   (`Treasury.Order.DestinationNotRegistered`); con `PayableFrom` futuro, marca `DestinationWaiting`;
3. banco con `TransferCode` (`Treasury.BankAccount.BankWithoutTransferCode`);
4. listas: el beneficiario y el titular de la cuenta contra `IListasRestrictivas` (`ScreeningContext.Disbursement`): una
   coincidencia vinculante deja la orden `Pending` con `BindingListMatch` y alerta al oficial (US10-8);
5. marcas: cuenta cambiada en los últimos `CuentasDestino.DiasDeMarca`, cuenta compartida, cuenta de un usuario interno,
   cuenta importada sin certificación.

### 5.4 `ArchivoDeDesembolsosGenerado` v1 y `ArchivoDeDesembolsosAnulado` v1 (E-01 c)

Guarda `TES_LendingDisbursementFiles` con las órdenes que cubre; la conciliación cruza su débito con las órdenes; el
anulado devuelve las órdenes a la espera de otro archivo (de Cartera) sin resultado. Detalle de campos en
[contratos-v2.md](contratos-v2.md) §2.3.

## 6. Emisión de Tesorería: `ResultadoDelGiro` v1 (sin cambios)

Origen: `TES_DisbursementResults` (cada resultado es su propio documento); clave `Confirmation:{ordenPublicId:N}` sobre ese
origen; `related` = la orden; `OrderMessagePublicId` = el mensaje de la orden al que responde. Campos de la 013 §4.2:
`OrderPublicId`, `OrderMessagePublicId`, `Outcome` (`Disbursed` | `Rejected` | `CheckNotCashed` | `Voided`), `OccurredOn`,
`Amount` (= `NetAmount`, FR-116), `TreasuryReference` (número del lote o del cheque y referencia del banco), `CheckNumber?`,
`Reason?` (causal del banco). Las órdenes de Recaudo (devoluciones) no usan mensaje: Tesorería informa a Recaudo por el
puerto `IResultadoDeOrdenesDeRecaudo` en la misma transacción (R-05).

## 7. Registro de participantes (FR-147)

Lo escribe la activación de cada módulo con `EscritorDelRegistroDeIntegracion`, en el mismo guardado:

| `Code` | `Class` | `Role` | `Destination` | Adopciones (R0/T0) | Adopciones posteriores |
|---|---|---|---|---|---|
| `REC` | `Receipts` | `ObligationsCollector` | — | `ProveedorDeObligaciones` v1, `ProveedorDeCuentas` v1 | v2 de los dos en R1 (E-01 a, E-02 a) |
| `REC` | `Receipts` | `MessageDestination` | `Receipts` | `PagoReversadoPorCartera` v1, `ConsignacionReversadaPorCartera` v1, `DescuentosPorPagaduria` v1 y v2 | — |
| `TES` | `Treasury` | `MessageDestination` | `Treasury` | `OrdenDeGiroEmitida` v1 y v2, `AnulacionDeOrdenDeGiro` v1, `ArchivoDeDesembolsosGenerado` v1, `ArchivoDeDesembolsosAnulado` v1 | `FacturaDeProveedorPorPagar` v1, `DispersionDeNominaGenerada` v1 (T2) |

Tesorería **no** tiene fila `ObligationsCollector` (D2). La clase `ClaseDeRecaudador.Treasury`, la gramática
`TES:{documento:N}:{cuenta:N}` de la 014 y el destino `Treasury` en `PagoReversadoPorCartera` se conservan sin uso.

## 8. Retiro de los dobles (FR-223)

| Pieza | Qué se hace |
|---|---|
| Filas `TST` (`Class = Test`, `IsTestDouble = true`) y la fila `TES` del doble en cooperativas de DEV y QA | migración `RetiroDeLosDoblesDePrueba`: `IsEnabled = false`, `ValidTo` = fecha del despliegue, motivo; adopciones cerradas; nada se borra |
| `TesoreriaDePrueba`, `RecaudadorDePrueba`, sus comandos, `TestDoublesEndpoints`, `DoblesDePrueba.razor`, `Lending:TestDoubles:Enabled` | se borran del código |
| `EscritorDelRegistroDeIntegracion` | rechaza `Class = Test` o `IsTestDouble = true` en todo ambiente (`Integration.Participant.TestDoublesRetired`) |
| `RequisitosDeActivacion` (Cartera) y `RequisitosDeActivacionDeCaptaciones` | sólo cuentan `Receipts` y `Treasury` reales, en todo ambiente (E-01 e, E-02 g) |
| Pruebas | `NoHayDoblesDePrueba` reemplaza a `LosDoblesDePruebaNoSeRegistranEnProduccion`; las e2e de Cartera y Captaciones registran y activan Recaudo y Tesorería reales (`RecaudoYTesoreriaE2E.RegistrarYActivarAsync`) |

Una fila del doble `TES` y la fila real `TES` comparten código: la migración retira primero la del doble (con
`IsTestDouble = true`) y la activación de Tesorería registra la real; el único `(Code, Role, Destination)` de la 013 se
respeta porque la fila retirada queda con vigencia cerrada y el escritor registra una vigencia nueva sobre la misma
llave lógica (como cualquier re-registro de la 013: cierra la anterior y abre otra, US9-2).

## 9. Pruebas que lo fijan

`UnSoloConsumidorPorDestino`, `ElCatalogoDeMensajesEsUnico` (ampliada), `EmisionesDeProcesoPermitidas`,
`NoHayDoblesDePrueba`, `LosComandosDeConsumoNoTienenRuta` (los `Consume…Command` no tienen ruta), e2e
`RecaudoRecibeReversosDeCarteraTests`, `PagaduriaDePuntaAPuntaTests`, `GiroRechazadoYRegiroTests` en los dos motores.
