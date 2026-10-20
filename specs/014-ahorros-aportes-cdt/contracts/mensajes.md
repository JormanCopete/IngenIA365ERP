# Catálogo de mensajes de captaciones y aportes

**Feature**: 014 | **Fecha**: 2026-10-14 | **Catálogo**: entradas nuevas en `CatalogoDeMensajesDeCarteraV1` y
`CatalogoDeMensajesHaciaCarteraV1` (cambio de catálogo, sin versión nueva del catálogo) | **Base**: `research.md` §0,
R03, R10, R14–R19, R24; spec FR-076, FR-089, FR-091, FR-092, FR-096–FR-100, FR-125, FR-128, E-01–E-05 | **Molde**:
`specs/013-cartera-financiera/contracts/mensajes.md` (en adelante **«la 013»**) | **Hermanos**: `contabilidad.md`
(qué hace Contabilidad con cada hecho), `contrato-de-cuentas.md`, `api.md`

Un mensaje de captaciones es un **hecho de la Cartera**: mismo emisor (`originModule = "CAR"`), misma bandeja, mismo
sobre (`IntegrationEnvelopeV1`), misma serialización, mismas claves de evento, mismo orden y mismas reglas que la 013,
que **no se repiten** aquí. Lo que cambia es el catálogo: hechos nuevos hacia Contabilidad, un destino nuevo
(`Associates`), dos versiones nuevas de mensajes existentes y los mensajes con Comercial por los cargos.

Las cinco reglas de la 013 rigen igual: **nace con lo que cuenta** (en el `SaveChanges` de la operación, de la tanda
o del cierre; cada `MemberAccountTransaction` guarda el `MessagePublicId` de su hecho); **no se modifica nunca**; **el
catálogo dice quién emite qué y adónde**; **un cambio incompatible es otra versión**; **nunca lleva cuentas
contables** (`LosHechosDeCarteraNoLlevanCuentas` recorre también `Contracts/Lending/Deposits`).

Dónde vive: records en `src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending/Deposits/<Type>V1.cs`
(research R19), con sus bloques comunes (§3) en la misma carpeta. Las versiones nuevas de mensajes compartidos con los
créditos (`OrdenDeGiroEmitidaV2`, `DescuentosPorPagaduriaV2`, `ResultadoDeDescuentosV2`) van en `Contracts/Lending`
junto a su v1. `FacturaDeCargoRegistradaV1`, que emite Comercial pero define esta especificación, va en
`Contracts/Lending/Deposits` y en `CatalogoDeMensajesHaciaCarteraV1` (molde de `ResultadoDelGiro`, la 013 §1).

---

## 1. Catálogo

Columnas como en la 013 §2: **Corto** = `LND_FactTypes.ShortCode` (3 letras, único, va en la `ScheduleKey`
`CAR.{ShortCode}|…`); **Proceso** = `AdmiteProceso`; **Clave** = forma de `originEventKey`; **Modo** = modo de paso
por defecto hacia Contabilidad (`Contabilidad.ModoDePaso`, ámbito `LendingFactType`; **`NoPasa` mientras la
contabilidad no esté iniciada**); **Comp.** = tipo de comprobante por defecto de `ACC_LendingVoucherMappings`
(`contabilidad.md` §5). «el del principal» = acompañante; «el del original» = reverso que hereda.

### 1.1 Hechos a Contabilidad (`CAR → Accounting`, FR-096)

| # | `Type` | Corto | `Kind` | Proceso | Clave | Lo origina | Modo | Comp. | E |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `ConsignacionEnCuentaAplicada` | CCN | Business | no | `Confirmation` | `MemberAccountOperation` de consignación en una cuenta de captación (contrato o depósito inicial de la apertura) | EnLinea | MA | K8 |
| 2 | `AporteRecibido` | APR | Business | no | `Confirmation` | Consignación en una cuenta de aportes (cuota, extraordinario) | EnLinea | AO | K7 |
| 3 | `RetiroDeCuentaAplicado` | RCT | Business | sí | `Confirmation` | Retiro (contrato, retiro por giro de la pantalla, cancelación, **débito automático** del ciclo diario) | EnLinea | MA | K8 |
| 4 | `TrasladoEntreCuentasAplicado` | TRC | Business | sí, sólo el traslado del contractual al vencer (E243) | `Confirmation` | Traslado entre cuentas (contrato o pantalla) | EnLinea | MA | K7 |
| 5 | `GmfRetenido` | GMF | Business | sí | `Confirmation` (acompañante) | Acompaña a la disposición que lo genera (FR-053, FR-066) | el del principal | el del principal | K8 |
| 6 | `InteresDeCaptacionCausado` | ICC | Business | sí | `Confirmation:{parte:N}` | Parte de la tanda del paso de captaciones del ciclo diario, o de la puesta al día de una operación | PorLotes resumido, `FinDeCausacion` | CC | K8 |
| 7 | `AjusteDeCausacionDeCaptacion` | AJC | Business | sí | `Confirmation` · `Confirmation:{parte:N}` | Sale **aparte** de la operación fechada atrás (FR-062), con el mismo origen y su propio comprobante, causa `ValueDatedOperation` y el hecho principal en `causeFact` (enmienda E96); o parte de una corrección del proceso | el de la operación; en la corrida, PorLotes resumido | CC | K8 |
| 8 | `InteresDeCaptacionLiquidado` | ILQ | Business | sí | `Confirmation:{parte:N}` · `Confirmation` | Parte de la liquidación periódica del ciclo diario; o la liquidación manual (`SettleAccountInterestCommand`) | PorLotes resumido si la emite el proceso; EnLinea si la emite una operación | CC | K8 |
| 9 | `RetencionPracticada` | RTF | Business | sí | `Confirmation` (acompañante) · `Confirmation:{parte:N}` | Acompaña al pago o abono de intereses (CDAT, cancelación, prórroga, redención) o va en la parte de la liquidación (FR-063) | el del principal | el del principal | K8 |
| 10 | `CdatConstituido` | CDC | Business | no | `Confirmation` | Fondeo del CDAT (consignación exacta o traslado) | EnLinea | TD | K9 |
| 11 | `InteresesDeCdatPagados` | CDP | Business | sí | `Confirmation` | Pago de un período del plan, por el ciclo diario (FR-069) | EnLinea | TD | K9 |
| 12 | `CdatProrrogado` | CDR | Business | sí | `Confirmation` | Prórroga automática pasados los días de gracia (FR-070) | EnLinea | TD | K9 |
| 13 | `CdatRenovado` | CDN | Business | no | `Confirmation` | Renovación con condiciones nuevas (US6) | EnLinea | TD | K9 |
| 14 | `CdatRedimido` | CDD | Business | no | `Confirmation` | Redención anticipada (FR-071) | EnLinea | TD | K9 |
| 15 | `CdatCancelado` | CDX | Business | sí | `Confirmation` | Cancelación al vencimiento (instrucción o redención en gracia) o paso a la vista | EnLinea | TD | K9 |
| 16 | `PenalidadAplicada` | PEN | Business | sí | `Confirmation` (acompañante) | Acompaña a `CdatRedimido` y al incumplimiento del contractual | el del principal | el del principal | K9 |
| 17 | `CaptacionReclasificada` | RCC | Business | sí | `Confirmation:{tipoDeHecho:N}` · `Confirmation:{cuenta:N}` | La operación que cambia el plazo de emisión, la inactividad, el embargo o la exención; o la parte del ciclo diario que lo detecta (FR-073, FR-084) | EnLinea si la emite una operación; PorLotes `FinDeCausacion` si la emite el proceso | CN | K8 |
| 18 | `BonificacionReconocida` | BON | Business | sí | `Confirmation` | Vencimiento cumplido del contractual (FR-075; o provisión, P26) | EnLinea | CC | K9 |
| 19 | `CargoDeCuentaCausado` | CGA | Business | sí | `Confirmation` | Cargo de evento (operación) o periódico (ciclo diario) (FR-128) | EnLinea | MA | K8 |
| 20 | `CargoPendienteCobrado` | CGP | Business | no | `Confirmation` (acompañante) | Acompaña a la consignación que cobra un cargo pendiente | el del principal | el del principal | K8 |
| 21 | `CargoDeCuentaCondonado` | CGN | Business | no | `Confirmation` | Condonación o exoneración individual (FR-128) | EnLinea | MA | K8 |
| 22 | `RevalorizacionDeAportesAplicada` | RVA | Business | no | `Confirmation` | Aplicación de la decisión de asamblea (FR-045), forma masiva con líneas por asociado | EnLinea | AO | K10 |
| 23 | `ExcedentesDistribuidos` | EXD | Business | no | `Confirmation` | Ídem (FR-046) | EnLinea | AO | K10 |
| 24 | `AportesAmortizados` | AMZ | Business | no | `Confirmation` | Amortización o readquisición (FR-047) | EnLinea | AO | K10 |
| 25 | `CruceAlRetiroAplicado` | CRR | Business | no | `Confirmation` | Liquidación del retiro del asociado: el débito de aportes y ahorros que paga los créditos (FR-077) | EnLinea | RA | K7 |
| 26 | `PerdidaAplicadaAlRetiro` | PDR | Business | no | `Confirmation` | Va **solo**, en su propia operación `InternalOffset` sin crédito, antes del cruce (puede haber pérdida sin cruce; enmienda E68) | EnLinea | RA | K7 |
| 27 | `DevolucionOrdenada` | DVO | Business | sí | `Confirmation` | Devolución del retiro, devolución sin retiro, entrega a beneficiarios, pago de no reclamados (FR-079, FR-083) | EnLinea | RA | K7 |
| 28 | `DevolucionGirada` | DVG | Informational | no | `Confirmation:{orden:N}` | Consumo de `ResultadoDelGiro` `Disbursed` de una orden de captaciones | Always, con recibo | — | K7 |
| 29 | `SaldoTrasladadoANoReclamados` | SNR | Business | sí | `Confirmation` | Traslado a no reclamados (FR-081) | EnLinea | MA | K10 |
| 30 | `EmbargoConsignado` | EMC | Business | no | `Confirmation` | Consignación al juzgado (FR-055) | EnLinea | MA | K8 |
| 31 | `OperacionDeCuentaReversada` | RVC | Business | no | `Confirmation` | Reverso de cualquier operación de 1 a 30 (contrato, pantalla, giro rechazado, reverso del pago del cruce o del débito) | el del original | el del original | K7 |
| 32 | `CaptacionesActivadas` | CAV | Informational | no | `Confirmation` | Activación de aportes o de captaciones (campo `nature`) | Always, con recibo | — | K7 |
| 33 | `CargaDeCaptacionesAplicada` | CAC | Informational | no | `Confirmation` | Carga de migración aplicada | Always, con recibo | — | K7 |

Los cierres usan los informativos existentes (`CierreDeCarteraRealizado`, `CierreDeCarteraReabierto`): el cierre
mensual de la Cartera incluye la sección de captaciones (R10) y no emite un tipo propio.

### 1.2 Mensajes a otros destinos

| # | `Type` | v | Destino | `Kind` | Proceso | Clave | Lo origina | E |
|---|---|---|---|---|---|---|---|---|
| 34 | `RetiroDeAsociadoAprobado` | 1 | `Associates` | Business | no | `Confirmation` | El retiro del asociado al aprobarse (`MembershipWithdrawal`, FR-076) | K7 |
| 35 | `ConsignacionReversadaPorCartera` | 1 | `Receipts` o `Treasury` (el del recaudador) | Business | no | `Confirmation` | Reverso desde la Cartera de una consignación de un recaudador (FR-092) | K7 |
| 36 | `OrdenDeGiroEmitida` | **2** | `Treasury` | Business | sí | `Confirmation` | Orden de giro de captaciones (`DisbursementOrder` con `Kind` ≠ `LoanDisbursement`, FR-091) | K7 |
| 37 | `DescuentosPorPagaduria` | **2** | `Receipts` | Business | no | `Confirmation` | Archivo de descuentos de un grupo del convenio con créditos y cuotas de cuentas (FR-125) | K7 |
| 38 | `CargoDeCaptacionCausado` | 1 | `Inventory` | Business | sí | `Confirmation` | Cargo gravado causado (FR-128, D20 de la 013) | K8 |
| 39 | `CargoDeCaptacionReversado` | 1 | `Inventory` | Business | no | `Confirmation` | Reverso de un cargo gravado (nota crédito) | K8 |

### 1.3 Mensajes que recibe la Cartera (destino `Lending`)

| # | `Type` | v | Módulo de origen | `Kind` | Clave | Consumo | E |
|---|---|---|---|---|---|---|---|
| 40 | `FacturaDeCargoRegistrada` | 1 | `INV` | Informational | `Confirmation:{cargo:N}` | `ConsumeChargeInvoiceRegisteredCommand` (sólo guarda el estado de la factura) | K8 |
| 41 | `ResultadoDeDescuentos` | **2** | `REC` | Business | `Confirmation:{archivo:N}` | `ConsumePayrollDeductionResultCommand` (reparte por equivalencia) | K7 |
| — | `ResultadoDelGiro` | 1 (existente) | `TES` | Business | `Confirmation:{orden:N}` | `ConsumeTreasuryResultCommand`, con los efectos nuevos de §4.3 | K7 |

El catálogo suma **41 filas**: 33 hechos `CAR → Accounting`, 6 mensajes de la Cartera a otros destinos (dos de ellos
v2) y 2 que recibe (uno v2). `ResultadoDelGiro` v1 y `AnulacionDeOrdenDeGiro` v1 se reutilizan sin cambio de contenido.

### 1.4 Cambios de catálogo sobre tipos existentes (sin versión nueva)

- `PagoAplicado` (la 013 #10) pasa a `AdmiteProceso = true` **sólo** para la clase de recaudador `OwnLoanPayoff` con
  clave `CAR:debito-automatico:…`: el débito automático lo hace el ciclo diario (R16). `EmisionesDeProcesoPermitidas`
  se enmienda para admitirlo con esa condición; la regla C14 de la 013 sigue para Recaudo.
- `PagoReversadoPorCartera` no cambia: el cruce al retiro y el débito automático son internos y no lo emiten.
- `IntegrationDestinations` suma `Associates` **(nuevo en contracts)**; un solo consumidor, `DestinoAsociados` en
  `Application/Core/Associates/Integration` **(nuevo en contracts)**, registrado con la fila de participante de código
  `ASO` **(nuevo en contracts)**, rol `MessageDestination`, sembrada por la plataforma.

---

## 2. Qué emite cada origen

El origen de un mensaje es la operación o el documento de la Cartera que produce el hecho, **nunca la cuenta**.
`+` = en la misma transacción.

| Origen (`origin.documentClass`) **(nuevo en contracts)** | Mensajes |
|---|---|
| `AccountDeposit` | `ConsignacionEnCuentaAplicada` o `AporteRecibido` (+ `CargoPendienteCobrado`, `AjusteDeCausacionDeCaptacion`); aparte, `CaptacionReclasificada` (reactivación) |
| `AccountWithdrawal` | `RetiroDeCuentaAplicado` (+ `GmfRetenido`, `AjusteDeCausacionDeCaptacion`) + `OrdenDeGiroEmitida` v2 si es por giro (origen: la orden); en el débito automático, + `PagoAplicado` en el crédito con su propia operación |
| `AccountTransfer` | `TrasladoEntreCuentasAplicado` (+ `GmfRetenido`, `AjusteDeCausacionDeCaptacion`) |
| `AccountClosing` | `InteresDeCaptacionLiquidado` + `RetencionPracticada` + `RetiroDeCuentaAplicado` (+ `GmfRetenido`) + la orden si se gira |
| `AccountReversal` | `OperacionDeCuentaReversada`; `ConsignacionReversadaPorCartera` si un usuario de la Cartera reversó la consignación de un recaudador |
| `InterestSettlement` (manual) | `InteresDeCaptacionLiquidado` (+ `RetencionPracticada`) |
| `TermDepositConstitution` | `CdatConstituido` (+ `GmfRetenido` si se fondea desde ahorro) |
| `TermDepositInterestPayment` | `InteresesDeCdatPagados` (+ `RetencionPracticada`, `GmfRetenido`) + `OrdenDeGiroEmitida` v2 (`TermDepositInterest`) si el destino es un tercero |
| `TermDepositRollover` | `CdatProrrogado` (+ `RetencionPracticada`, `CaptacionReclasificada` como acompañante si cambia el plazo de emisión) |
| `TermDepositRenewal` | `CdatRenovado` (+ `RetencionPracticada`, `GmfRetenido`, `CaptacionReclasificada`) |
| `TermDepositRedemption` | `CdatRedimido` o `CdatCancelado` (+ `RetencionPracticada`, `PenalidadAplicada`, `GmfRetenido`, `AjusteDeCausacionDeCaptacion`) + la orden si se gira |
| `ContractualMaturity` | `BonificacionReconocida` o `PenalidadAplicada` (incumplido) + `TrasladoEntreCuentasAplicado` o `DevolucionOrdenada` según la instrucción |
| `AccountCharge` | `CargoDeCuentaCausado` (+ `CargoDeCaptacionCausado` a Comercial si está gravado); condonarlo: `CargoDeCuentaCondonado`; reversarlo: `OperacionDeCuentaReversada` + `CargoDeCaptacionReversado` |
| `MembershipWithdrawal` | al aprobar, `RetiroDeAsociadoAprobado` |
| `MembershipWithdrawalSettlement` | por crédito cruzado, **una** unidad con origen la operación de pago (`OwnLoanPayoff`): su `PagoAplicado` (lo emite el contrato de obligaciones) y un `CruceAlRetiroAplicado` con la parte de cada cuenta (`parts`), el principal —decisión del dueño del 2026-10-18, D45: mismo comprobante—; aparte, `PerdidaAplicadaAlRetiro` (su operación), `DevolucionOrdenada` + la orden |
| `ContributionRefund` · `DeceasedDelivery` · `UnclaimedPayment` | `DevolucionOrdenada` + la orden |
| `AssemblyDistribution` | `RevalorizacionDeAportesAplicada`, `ExcedentesDistribuidos` o `AportesAmortizados` (+ órdenes si el excedente se paga) |
| `UnclaimedTransfer` | `SaldoTrasladadoANoReclamados` |
| `GarnishmentConsignment` | `EmbargoConsignado` + la orden (`GarnishmentConsignment`) |
| `AccountingFactPart` del paso de captaciones | por parte: `InteresDeCaptacionCausado`, `AjusteDeCausacionDeCaptacion` (corrección); por cuenta (`Confirmation:{cuenta:N}`): `CaptacionReclasificada` (inactividad, plazo). La liquidación periódica **no** va por partes: cada liquidación es su operación `InterestSettlement` (`LQI-`) y emite `InteresDeCaptacionLiquidado` (+ `RetencionPracticada`) como una unidad, por lotes resumido si la emite el proceso (enmienda E98) |
| `DisbursementOrder` (captaciones) | `OrdenDeGiroEmitida` v2; `AnulacionDeOrdenDeGiro` v1 |
| `DepositsActivation` · `DepositsMigrationLoad` | `CaptacionesActivadas` · `CargaDeCaptacionesAplicada` |
| `PayrollDeductionFile` (grupo con cuentas) | `DescuentosPorPagaduria` v2 |
| `InboundMessage` (resultado del giro) | `DevolucionGirada`; si el giro se rechaza, `OperacionDeCuentaReversada` del origen de la orden |
| `AccountOpeningBalance` (activación) | **ninguno**: el saldo inicial no genera hechos (FR-114) |

### 2.1 Unidades y acompañantes (la 013 §7.2, C01)

| Principal | Acompañantes (cuando corresponden) |
|---|---|
| `ConsignacionEnCuentaAplicada` · `AporteRecibido` | `CargoPendienteCobrado`, `AjusteDeCausacionDeCaptacion` |
| `RetiroDeCuentaAplicado` · `TrasladoEntreCuentasAplicado` | `GmfRetenido`, `AjusteDeCausacionDeCaptacion` |
| `InteresDeCaptacionLiquidado` (manual) | `RetencionPracticada` |
| `InteresesDeCdatPagados` | `RetencionPracticada`, `GmfRetenido` |
| `CdatConstituido` | `GmfRetenido` |
| `CdatProrrogado` · `CdatRenovado` | `RetencionPracticada`, `GmfRetenido`, `CaptacionReclasificada` |
| `CdatRedimido` · `CdatCancelado` | `RetencionPracticada`, `PenalidadAplicada`, `GmfRetenido`, `AjusteDeCausacionDeCaptacion` |
| `CruceAlRetiroAplicado` | desde D45 (2026-10-18) el `PagoAplicado` de su crédito y los acompañantes de éste; `PerdidaAplicadaAlRetiro` ya no es acompañante (va en su propia operación, enmienda E68). Antes de D45: también el `PagoAplicado` de su crédito y los acompañantes de éste (`AnticipoRecibido`, `CobroDeInteresEnSuspenso`, `RecuperacionDeCastigada`, `SaldoAFavorReconocido`, `AjusteDeCausacionPorFechaValor`) |
| `BonificacionReconocida` | `PenalidadAplicada` y `RetencionPracticada` (enmienda E148) |
| `OperacionDeCuentaReversada` | `AjusteDeCausacionDeCaptacion`; `PagoReversado` en el reverso del cruce al retiro (enmienda E110) |

**Enmienda E96**: el `AjusteDeCausacionDeCaptacion` de una operación fechada en días ya causados (consignación, retiro,
traslado, reverso) **no** viaja como acompañante: sale aparte, con el mismo origen y su propio comprobante. Las filas de
esta tabla que lo nombran como acompañante quedan como referencia del diseño original.

Los acompañantes sellan el modo de su principal y salen en **un** comprobante; el tipo lo da el principal. Lo que va
**aparte** (`Confirmation:{tipoDeHecho:N}`): `CaptacionReclasificada` cuando la emite una operación ajena al CDAT
(reactivación por consignación, embargo, marca de exención). Una unidad nunca mezcla modos.

### 2.2 Partes del paso de captaciones (la 013 §7.1)

`LND_AccountingFactParts` se reutiliza: una parte = fecha × sucursal contable × tipo × tanda del paso de
captaciones, con totales por combinación de dimensiones **y** la lista por cuenta. Rótulos **(nuevo en contracts)**:
`CCA-{aaaammdd}-{nnnn}` (causación) y `AJC-{aaaammdd}-{nnnn}` (corrección); la liquidación y su retención no usan
partes `LQC-`: la operación `InterestSettlement` (`LQI-`) ya es su origen (enmienda E98); la puesta al día de una operación, `ALD-{número de la operación}-{n}` (el de la 013). Con tandas de
`Lending:Process:BatchSize` una parte no pasa de unos 150 KB.

---

## 3. Bloques comunes (`Contracts/Lending/Deposits`) (nuevo en contracts)

**`MemberAccountRefV1`** — `{ accountPublicId: Guid, accountNumber: string }`. El número es el documento cruce
(FR-097, `contabilidad.md` §5).

**`DepositFactDimensionsV1`** — las dimensiones de la matriz que no dependen del concepto (FR-097, R19):

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `creditLineCode` | string | sí | Código de la línea (la identidad es común, R01); dimensión `CreditLineCode` |
| `lineNature` | `LineNature` | sí | `Deposit` o `Contribution` |
| `depositClass` | `DepositClass` | en captación | `Demand`, `Contractual`, `Permanent`, `TermDeposit` |
| `issueTermBand` | `IssueTermBand` | en CDAT | `UnderSixMonths`, `SixToTwelve`, `TwelveToEighteen`, `EighteenOrMore`, por el plazo **pactado** de la versión vigente |
| `accountFlag` | string | sí | `Standard`, `GmfExempt`, `Garnished` (`contabilidad.md` §3) |
| `inactive` | bool | sí | La cuenta está inactiva: decide `DepositPrincipal` o `DepositInactivePrincipal` |
| `isEmployee` | bool | sí | El titular es empleado (estado actual de la persona, C8 de la 013) |
| `branchPublicId` | Guid | sí | Sucursal contable de la oficina de la cuenta |

**`DepositConceptAmountV1`** — un valor por concepto:

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `concept` | `MemberAccountConcept` | sí | `Principal`, `InterestPayable`, `Withholding`, `Gmf`, `ContributionMinimum`, `ContributionOrdinary`, `ContributionExtraordinary`, `ContributionRevaluation`, `ChargeReceivable`, `Bonus` |
| `contributionClass` | `ContributionClass` | en aportes | La clase del aporte; dimensión `ContributionClass` (la deriva del concepto, pero viaja para no recalcular) |
| `amount` | decimal(18,2) | sí | Con signo (+ = la Cartera le debe más al titular, R03) |
| `accruedAmount` | decimal(18,6) | no | El valor con los decimales de la causación, sólo en `InteresDeCaptacionCausado` (la suma redondeada va en `amount`) |
| `reasonCode` | string | no | Dimensión `ReasonCode` cuando el rol la exige: el `OrderKind` de una orden, el `ChargeKind` de un cargo, la causa de una reclasificación |

**`DepositFactLineV1`** — una línea por cuenta en las partes y en las formas masivas (asamblea):
`{ account: MemberAccountRefV1, holderPersonPublicId, dimensions: DepositFactDimensionsV1, previousDimensions?:
DepositFactDimensionsV1, amounts: [DepositConceptAmountV1], valueDate? }`.

**`DepositFactTotalV1`** — `{ dimensions, amounts, accountCount }`.

**`TermDepositTermsV1`** — la versión sellada del CDAT: `{ termsVersion, titleNumber, amount, termDays, startDate,
maturityDate, effectiveAnnual, tableRate, negotiatedMarginEa?, interestPeriodicity, timing (InterestTiming),
dayCount, issueTermBand, rolloverNumber, rulesVersion }`.

**`WithholdingDetailV1`** — `{ base, inflationaryComponentPercent?, taxableBase, rate, thresholdUvtPerDay, uvt,
thresholdReached: bool, holderTaxProfile: Standard\|NonTaxpayer\|SelfWithholder\|NoProfile, year, conceptCode }`
(FR-021, FR-063; alimenta el certificado, `LND_WithholdingAccumulations`).

**`GmfDetailV1`** — `{ base, policy (GmfExemptionPolicy), exemptAccountPublicId?, exemptUsedBefore, exemptApplied,
taxedBase, rate, generatingEvent, generatingDepositClass }` (FR-022). `dimensions` del hecho son las de la cuenta
**debitada**; `generatingDepositClass` es la clase del producto que generó el hecho (el CDAT en su redención abonada en
ahorro) y decide la clase de `GmfPayable` (`contabilidad.md` §2).

Se reutilizan de la 013: `CollectorRefV1`, `ReceiptRefV1`, `OriginalFactRefV1`, `UserRefV1`, `PayeeV1`, `LoanRefV1`.

---

## 4. Hechos a Contabilidad

### 4.0 Forma común

Todo hecho por cuenta (todos salvo las partes, las formas masivas y los informativos) lleva al menos:

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `account` | `MemberAccountRefV1` | sí | Cuenta y número |
| `holderPersonPublicId` | Guid | sí | Titular (el sobre puede traer otro tercero: el depositante, el pagador) |
| `valueDate` | DateOnly | sí | Fecha valor |
| `registeredAt` | DateTimeOffset | sí | Instante R de la operación |
| `dimensions` | `DepositFactDimensionsV1` | sí | Dimensiones **vigentes** a la fecha contable |
| `amounts` | [`DepositConceptAmountV1`] | sí | Valores por concepto |
| `original` | `OriginalFactRefV1` | en los dependientes | El hecho del que depende (reversos, acompañantes) |
| `operationNumber` | string | sí | Número visible de la operación |
| `rulesVersion` | string | sí | Versión de reglas sellada |

Las **partes** (`InteresDeCaptacionCausado`, la liquidación periódica y la corrección) usan la forma masiva de la
013 §10.0 con `DepositFactLineV1` y `DepositFactTotalV1` en vez de los de crédito: `runKind` (`DailyCycle`),
`runPublicId`, `catchUpOperation?`, `factDate`, `batchNumber`, `partNumber`, `totals[]`, `lines[]`, `lineCount`,
`controlTotal`. El **sobre**: `personPublicId` = el titular en los hechos por cuenta, el depositante en consignaciones
y aportes, el beneficiario en devoluciones; `branchPublicId` = la sucursal contable vinculada a la oficina de la
cuenta (una oficina sin sucursal vinculada no emite: la operación se rechaza, C9 de la 013); `operationDate` = la
fecha contable (la 013 §5, T36); `related` = el original en reversos y la orden anterior en un giro nuevo.

### 4.1 Contenido propio de cada hecho

| `Type` | Campos además de los comunes | Ejemplo de `amounts` |
|---|---|---|
| `ConsignacionEnCuentaAplicada` | `collector: CollectorRefV1`, `modality`, `pendingClearance: decimal` (valor en canje), `reactivated: bool`, `isOpeningDeposit: bool` | `[{ Principal, 1000000.00 }]` |
| `AporteRecibido` | `collector`, `installmentsCovered: [{ installmentPublicId, period, amount }]`, `overpaymentAs: Extraordinary\|CreditBalance?` | `[{ ContributionOrdinary, 50000.00, contributionClass: Ordinary }]` |
| `RetiroDeCuentaAplicado` | `collector?` (contrato) · `disbursementOrderPublicId?` (por giro) · `internal?: { kind: DirectDebit, loan: LoanRefV1, paymentOperationPublicId }`, `meansClass`, `closesAccount: bool` | `[{ Principal, -400000.00, reasonCode: "DepositWithdrawal"? }]` |
| `TrasladoEntreCuentasAplicado` | `target: MemberAccountRefV1`, `targetHolderPersonPublicId`, `targetDimensions: DepositFactDimensionsV1`, `targetAmounts: [DepositConceptAmountV1]` | origen `[{ Principal, -300000 }]`, destino `[{ Principal, 300000 }]` |
| `GmfRetenido` | `gmf: GmfDetailV1` | `[{ Gmf, 1600.00 }]` (débito al capital del titular) |
| `InteresDeCaptacionCausado` | forma de parte | líneas `[{ InterestPayable, 161.97, accruedAmount: 161.968742 }]` |
| `AjusteDeCausacionDeCaptacion` | `cause: ValueDatedOperation\|Redemption\|RateCorrection\|ContractualBreach`, `days: [{ date, previous, recalculated }]` | `[{ InterestPayable, -12.40 }]` (signo mixto) |
| `InteresDeCaptacionLiquidado` | `periodFrom`, `periodTo`, `accrued`, `settled`, `roundingAdjustment`, `capitalized: bool` | `[{ InterestPayable, -6884.00 }, { Principal, 6884.00 }]` |
| `RetencionPracticada` | `withholding: WithholdingDetailV1` | `[{ Withholding, 3190.00 }]` (débito al capital o al interés por pagar) |
| `CdatConstituido` | `terms: TermDepositTermsV1`, `funding: { kind: Collector\|TransferFromAccount, collector?, sourceAccount? }`, `negotiated?: { rulePublicId, approverUserPublicId }` | `[{ Principal, 10000000.00 }]` |
| `InteresesDeCdatPagados` | `scheduleNumber`, `periodFrom`, `periodTo`, `destination: { kind: Account\|DisbursementOrder, targetAccount?, orderPublicId? }`, `timing` | `[{ InterestPayable, -79741.00 }]` |
| `CdatProrrogado` | `previousTerms`, `newTerms`, `capitalizedInterest`, `rolloverRateRule` | `[{ InterestPayable, -488088 }, { Principal, 468564 }]` |
| `CdatRenovado` | `previousTerms`, `newTerms`, `amountChange?: { kind: Increase\|Decrease, amount, funding?, destination? }` | |
| `CdatRedimido` | `terms`, `penalty: { kind, repricedRate?, amount }`, `destination` | `[{ Principal, -10000000 }, { InterestPayable, -… }]` |
| `CdatCancelado` | `terms`, `cause: Instruction\|GraceRedemption\|MovedToDemand\|NoRolloverRate\|MaxRolloversReached`, `destination` | |
| `PenalidadAplicada` | `kind: TermDepositEarlyRedemption\|ContractualBreach`, `basis` | `[{ InterestPayable, -2300.00 }]` o `[{ Principal, -… }]` |
| `CaptacionReclasificada` | `cause: IssueTermBand\|Inactivity\|Reactivation\|Garnishment\|GarnishmentRelease\|GmfExemption\|GmfExemptionEnd`, `previousDimensions` | los saldos que se mueven, con las dimensiones nuevas en `dimensions` |
| `BonificacionReconocida` | `contractual: { installments, paid, bonusRateEa, baseRateEa }` | `[{ Bonus, 21471.00 }]` |
| `CargoDeCuentaCausado` | `charge: { chargePublicId, kind (ChargeKind), code, event?, taxedWithVat }`, `status: Collected\|Pending` | `[{ Principal, -2000.00, reasonCode: "ExtraOperation" }]` o `[{ ChargeReceivable, 2000.00 }]` |
| `CargoPendienteCobrado` | `charges: [{ chargePublicId, amount }]` | `[{ ChargeReceivable, -2000 }, { Principal, -2000 }]` |
| `CargoDeCuentaCondonado` | `charge`, `previousStatus`, `exemptionPublicId?` | `[{ ChargeReceivable, -2000 }]` |
| `RevalorizacionDeAportesAplicada` · `ExcedentesDistribuidos` · `AportesAmortizados` | forma masiva: `distribution: { publicId, kind, year, sessionItemPublicId, fund, percent?, value?, base }`, `totals[]`, `lines[]`, `destination?` | líneas `[{ ContributionRevaluation, 15000, contributionClass: Revalued }]` |
| `CruceAlRetiroAplicado` | `withdrawal: { publicId, number, cause }`, `offsets: [{ loan: LoanRefV1, paymentOperationPublicId, amount }]` (uno: el crédito de la unidad), `parts: [{ account, dimensions, amounts, operationNumber }]` (D45: una por cuenta que pagó el crédito; `account`, `dimensions`, `amounts` y `operationNumber` de arriba repiten la primera) | `[{ ContributionOrdinary, -2500000 }]` |
| `PerdidaAplicadaAlRetiro` | `loss: { year, proportion, sessionItemPublicId }` | `[{ ContributionOrdinary, -120000 }]` |
| `DevolucionOrdenada` | `orderKind` (`OrderKind`), `orderPublicId?`, `targetAccount?`, `payeePersonPublicId`, `source: MembershipWithdrawal\|ContributionRefund\|DeceasedDelivery\|UnclaimedPayment` | `[{ ContributionOrdinary, -1500000, reasonCode: "MembershipRefund" }]` |
| `DevolucionGirada` | `orderPublicId`, `orderKind`, `outcome`, `treasuryReference`, `occurredOn` | sin importes (informativo) |
| `SaldoTrasladadoANoReclamados` | `origin: UncashedRefund\|InactiveBalance\|UncashedPayout` (el nombre de `UnclaimedSource`, enmienda E198), `unclaimedPublicId` | `[{ Principal, -350000 }]` |
| `EmbargoConsignado` | `garnishment: { publicId, officeReference, courtPersonPublicId, isAlimony }`, `orderPublicId` | `[{ Principal, -800000 }]` |
| `OperacionDeCuentaReversada` | `original: OriginalFactRefV1` (obligatorio), `reason`, `initiatedBy: Collector\|Lending\|TreasuryResult\|LoanPaymentReversal`, `mirrors: [{ type, amounts[] }]` (un bloque por cada hecho de la unidad original) | espejo exacto con las dimensiones **vigentes** a la fecha contable (la 013 §9) |
| `CaptacionesActivadas` | `nature`, `activationDate`, `loadPublicId?`, `accountCount` (enmienda E26), `openingTransactions` | — |
| `CargaDeCaptacionesAplicada` | `nature`, `loadPublicId`, `cutoffDate`, `parts[]`, `totalsByLineAndConcept[]` | — |

**Signos**: positivo = la Cartera le debe más al titular (R03). Un hecho cuadra solo en Contabilidad
(`contabilidad.md` §2). Signo mixto sólo en `AjusteDeCausacionDeCaptacion`, `InteresDeCaptacionLiquidado`,
`CdatProrrogado`, `CdatRenovado`, `CaptacionReclasificada` y los traslados.

**Ejemplo** — una consignación de 1.000.000 en efectivo por el recaudador de prueba (sólo `payload`):

```json
{
  "account": { "accountPublicId": "5c1e2f3a-4b5c-4d6e-8f70-1a2b3c4d5e01", "accountNumber": "AH-0000412" },
  "holderPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
  "valueDate": "2027-02-03", "registeredAt": "2027-02-03T15:10:02.110Z",
  "dimensions": { "creditLineCode": "AHVISTA", "lineNature": "Deposit", "depositClass": "Demand", "issueTermBand": null,
                  "accountFlag": "GmfExempt", "inactive": false, "isEmployee": false, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f" },
  "amounts": [ { "concept": "Principal", "contributionClass": null, "amount": 1000000.00, "accruedAmount": null, "reasonCode": null } ],
  "original": null, "operationNumber": "MCA-2027-000118", "rulesVersion": "LND-2027.1",
  "collector": { "collectorCode": "TST", "collectorClass": "Test", "idempotencyKey": "TST:4b5c6d7e8f904a1b2c3d4e5f6a7b8c01",
                 "meansClass": "Cash", "payerPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
                 "receipt": { "number": "RC-TST-0101", "date": "2027-02-03", "officePublicId": null, "meansReference": null, "documentKind": "Recibo" } },
  "modality": "Free", "pendingClearance": 0.00, "reactivated": false, "isOpeningDeposit": false
}
```

---

## 5. Mensajes a otros destinos

### 5.1 `RetiroDeAsociadoAprobado` v1 (`CAR → Associates`, FR-076, FR-124, E-04)

Decisión del dueño del 2026-10-18 (D46): suma `withdrawalReasonCode` (opcional), el código del motivo de retiro de Asociados
(`COR_WithdrawalReasons.LegacyCode`) que la cooperativa le asigna a la causa en `Captaciones.MotivosDeRetiroDeAsociados`;
Asociados llena con él `COR_Associates.WithdrawalReasonId` (un código que no tiene deja el motivo vacío y no impide el retiro).

Origen `MembershipWithdrawal`. Clave `Confirmation`. Tercero: el asociado. Lo origina la persona que aprueba el
retiro (no admite proceso), en el mismo guardado que la aprobación.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `withdrawalPublicId` · `withdrawalNumber` | Guid · string | sí | El trámite |
| `personPublicId` | Guid | sí | El asociado |
| `cause` | `MembershipWithdrawalCause` | sí | `Voluntary`, `Exclusion`, `Death`, `Other` |
| `causeDetail` | string | no | La causa configurada cuando es `Other` |
| `effectiveDate` | DateOnly | sí | Fecha del retiro que Asociados registra |
| `approval` | objeto | sí | `{ sessionItemPublicId, sessionNumber, governingBody, decidedOn }` |
| `approvedBy` | `UserRefV1` | sí | |

**Efecto en Asociados** (`ConsumeMembershipWithdrawalApprovedCommand`, sin ruta): cambia el estado del asociado a
retirado con la fecha y la causa, en un `SaveChanges` con su recibo único por `messageId`; repetir no cambia nada.
Asociado inexistente o ya retirado con otra fecha: `Rejected` con `Associate.NotFound` o
`Associate.WithdrawalDateMismatch` **(nuevo en contracts)**, para revisión en la bandeja. Asociados ya no retira por
su cuenta (`Associate.WithdrawalOwnedByLending`, `api.md` §18.5).

### 5.2 `ConsignacionReversadaPorCartera` v1 (`CAR → destino del recaudador`, FR-092)

Molde de `PagoReversadoPorCartera` de la 013. Origen `AccountReversal`. Clave `Confirmation`. `related` = la operación
de la consignación. Tercero: el depositante. Lo origina un usuario de la Cartera con `Lending.Accounts.Reverse`.
`Destinos = { Receipts, Treasury }`: el emisor elige por la clase del recaudador (`Receipts` y `Test` → `Receipts`;
`Treasury` → `Treasury`; `Payroll` nunca, lo reversa Nómina).

```csharp
public sealed record ConsignacionReversadaPorCarteraV1
{
    public const string Type = "ConsignacionReversadaPorCartera";
    public MemberAccountRefV1 Account { get; init; } = new();
    public OriginalFactRefV1 Deposit { get; init; } = new();      // la consignación reversada
    public Guid ReversalOperationPublicId { get; init; }
    public string ReversalNumber { get; init; } = string.Empty;
    public CollectorRefV1 Collector { get; init; } = new();       // con la clave y el recibo originales
    public decimal ReversedAmount { get; init; }
    public DateOnly ValueDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public UserRefV1 ReversedBy { get; init; } = new();
    public bool LeftTechnicalOverdraft { get; init; }             // siempre falso desde la Cartera (api.md §4)
}
```

### 5.3 `OrdenDeGiroEmitida` v2 (`CAR → Treasury`, FR-091)

**Se reutiliza la orden de los desembolsos con tipos nuevos**: misma tabla de órdenes (`LND_DisbursementOrders`, con
`Kind` y cuenta opcional **(nuevo en contracts)**), mismo ciclo de vida (`DisbursementOrderStatus`), misma anulación
(`AnulacionDeOrdenDeGiro` v1) y mismo resultado (`ResultadoDelGiro` v1). Como el contenido cambia (el crédito deja de
ser obligatorio y se suman el tipo y la cuenta), es una **v2** (la 013 §17): los desembolsos de crédito siguen
emitiendo **v1** mientras Tesorería no adopte la v2; las órdenes de captaciones salen **sólo en v2**, y activar
aportes o captaciones en producción exige un destino `Treasury` con la v2 adoptada.

```csharp
public sealed record OrdenDeGiroEmitidaV2
{
    public const string Type = "OrdenDeGiroEmitida";
    public Guid OrderPublicId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string OrderKind { get; init; } = string.Empty;           // OrderKind, abajo
    public LoanRefV1? Loan { get; init; }                            // sólo LoanDisbursement
    public int? TrancheNumber { get; init; }                         // sólo LoanDisbursement
    public MemberAccountRefV1? Account { get; init; }                // la cuenta de origen del dinero
    public Guid? SourceDocumentPublicId { get; init; }               // retiro, CDAT, devolución, entrega, embargo
    public string Form { get; init; } = string.Empty;                // Transfer | Check | ThirdPartyPayment
    public PayeeV1 Payee { get; init; } = new();
    public decimal GrossAmount { get; init; }
    public IReadOnlyList<PayoffDeductionV1> PayoffDeductions { get; init; } = [];
    public decimal NetAmount { get; init; }
    public DateOnly PayDate { get; init; }
    public string Concept { get; init; } = string.Empty;             // número de la cuenta o del crédito
    public bool PaidByFile { get; init; }
    public Guid? ReplacesOrderPublicId { get; init; }
}
```

`OrderKind` **(nuevo en contracts)**: `LoanDisbursement` (sólo v1 hoy), `DepositWithdrawal` (retiro por
transferencia), `MembershipRefund` (devolución del retiro), `ContributionRefund` (devolución sin retiro),
`DeceasedDelivery` (entrega a beneficiarios), `TermDepositInterest` (intereses del CDAT a un tercero o a otra
entidad), `TermDepositRedemption` (redención o cancelación por giro), `ContractualMaturity`, `UnclaimedPayment`,
`GarnishmentConsignment` (consignación al juzgado), `SurplusPayment` (excedentes pagados). Es también el `ReasonCode`
del rol puente `RefundPayable` (`contabilidad.md`).

**Ciclo de vida para captaciones** (`ConsumeTreasuryResultCommand`, misma tabla de estados de la 013 interfaces §4.3):

| Resultado | Efecto |
|---|---|
| `Disbursed` | La orden queda `Disbursed`; se emite `DevolucionGirada` (informativo); un retiro de asociado pasa a `Paid` cuando todas sus órdenes lo están; una entrega por fallecimiento cierra la cuenta al pagarse la última |
| `Rejected` · `Voided` · `CheckNotCashed` | **Reintegra el saldo** desde el origen de la orden (FR-091): `OperacionDeCuentaReversada` del retiro, de la devolución o de la consignación al juzgado, con `initiatedBy = TreasuryResult`; la devolución de un retiro vuelve a «por devolver» y admite un giro nuevo (`reissue`) |
| `Disbursed` tras una anulación emitida | La anulación no tuvo efecto: no se reintegra |

### 5.4 `DescuentosPorPagaduria` v2 (`CAR → Receipts`, FR-125, E-05)

La v1 exige un crédito en cada línea; la v2 admite **cuotas de cuentas** y agrupa por **grupo de archivo** y
**concepto equivalente** de la pagaduría. Origen: la versión del archivo del grupo. Clave `Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `agreement` | objeto | sí | `{ paymasterPersonPublicId, agreementCode, agreementVersion }` |
| `fileGroup` | objeto | sí | `{ code, name, version }` (`PayrollAgreementFileGroup`) |
| `period` · `fileVersion` · `file` · `sendBy` | | sí | como la v1 (`file.formatCode` del ámbito `PayrollDeductionRequest` 5 de K2) |
| `lines` | [objeto] | sí | `{ deductionFileLinePublicId, kind: Loan\|AccountInstallment, loan?: LoanRefV1, account?: MemberAccountRefV1, installmentPublicId?, installmentNumber?, personPublicId, payerEmployeeId, paymasterConceptCode, dueDate, noveltyKind, amount, pastDueAmount }` |
| `conceptTotals` | [objeto] | sí | `{ paymasterConceptCode, lines, amount }`: lo que la pagaduría verá por cada código suyo |
| `totals` · `supersedesFile` · `generatedBy` | | sí · no · sí | como la v1 |

Recaudo adopta la v2 en el registro; mientras no la adopte, un convenio con cuotas de cuentas no genera archivo
(`Lending.PayrollAgreement.ReceiptsVersionNotAdopted` **(nuevo en contracts)**) y los convenios sólo de créditos siguen
en v1.

**Construido (T091, decisión del dueño del 2026-10-18, D36).** El archivo de un grupo sale siempre en v2 (los
convenios sin grupos, en v1); la adopción se exige sólo si lleva cuotas de cuentas. `lines[].kind` es `Loan` o
`AccountInstallment`; `fileGroup.version` es el número de la versión del convenio. Qué cuentas van: las de aportes y
de ahorro permanente o contractual, activas o inactivas, del asociado cuya **empresa empleadora en su ficha**
(`COR_Associates.EmployerCompanyId` → `COR_EmployerCompanies.TaxId`) tiene el NIT de la pagaduría del convenio; una
línea por cuenta con lo pendiente de la cuota del período (y lo vencido si el convenio lo lleva); `payerEmployeeId`
es el del vínculo de libranza de la persona con el convenio o, sin él, su documento.

### 5.5 `CargoDeCaptacionCausado` v1 y `CargoDeCaptacionReversado` v1 (`CAR → Inventory`, FR-128, R14)

La Cartera **no calcula IVA ni emite facturas**: avisa a Comercial, que factura electrónicamente el cargo y su IVA
(D20 de la 013). Origen `AccountCharge`. Clave `Confirmation`. Tercero: el titular. Lo origina la persona o el proceso
(cargos periódicos).

**Consumo en Comercial** (decisión del dueño del 2026-10-18, D51; cierra T117): `DestinoInventario` los reparte a
`ConsumeDepositChargeMessageCommand`, que usa el flujo de ventas existente. El causado deja **lista** la factura de venta
del servicio —borrador `SalesInvoice` con el ítem `charge.commercialItemCode` (`INV_Products.Code`), cantidad 1, precio
`amountBeforeTax` (el IVA lo calcula la precificación de ventas), el titular como adquirente y la referencia externa
`CGO:{chargePublicId:N}` (la llave: repetir no crea otra)— con el tipo y la bodega de `Ventas.TipoDeFacturaDeCargosDeCartera`
y `Ventas.BodegaDeCargosDeCartera` (vacíos, el primer tipo y la primera bodega activos). El reversado descarta el
borrador o, si la factura ya se confirmó, deja lista la nota crédito total. Sin el ítem, el tipo o la bodega responde
`Rejected` (`Inventory.ChargeInvoice.ItemNotFound` · `.DocumentTypeMissing` · `.WarehouseMissing`) y espera el
reproceso. Confirmar y emitir electrónicamente es del flujo de ventas (I3/I4); `FacturaDeCargoRegistrada` la emitirá esa
emisión.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `charge` | objeto | sí | `{ chargePublicId, kind (ChargeKind), code, name, event?, periodFrom?, periodTo? }` |
| `account` | `MemberAccountRefV1` | sí | |
| `holderPersonPublicId` | Guid | sí | El adquirente |
| `amountBeforeTax` | decimal(18,2) | sí | Valor del cargo (la base); el IVA lo calcula Comercial con su catálogo tributario |
| `taxConceptCode` | string | sí | El concepto tributario del cargo en el catálogo de Core (`/maestros/impuestos`) |
| `status` | string | sí | `Collected` o `Pending` |
| `causedOn` | DateOnly | sí | |
| `original` | `OriginalFactRefV1` | sólo en el reverso | El `CargoDeCaptacionCausado` que se anula (nota crédito) |
| `reason` | string | sólo en el reverso | |

### 5.6 `FacturaDeCargoRegistrada` v1 (`INV → Lending`, informativa)

Comercial informa el estado de la factura del cargo; la Cartera **sólo lo guarda** para mostrarlo (Edge Cases: una
factura rechazada por la DIAN no cambia el cargo). Clave `Confirmation:{cargo:N}` (el `messageId` del
`CargoDeCaptacionCausado`). Campos: `chargeMessageId`, `chargePublicId`, `document: ExternalDocumentRefV1` (factura o
nota crédito), `fiscalUniqueCode?`, `status: Issued\|Accepted\|Rejected\|Voided`, `statusDetail?`, `occurredOn`.
Consumo `ConsumeChargeInvoiceRegisteredCommand` en un `SaveChanges` con su recibo; un cargo inexistente: `Rejected`
con `Generic.NotFound`.

### 5.7 `ResultadoDeDescuentos` v2 (`REC → Lending`, FR-125)

Como la v1 de la 013 §12.6 más: `fileGroupCode` y `conceptLines: [{ payerEmployeeId, personPublicId?,
paymasterConceptCode, expected, deducted, paymasterResultCode?, deductionDate? }]` (lo que la pagaduría informa **por
código suyo**, sin conocer las líneas de la Cartera). `lines[]` de la v1 sigue admitido para pagadurías que responden
línea a línea.

**Efecto** (`ConsumePayrollDeductionResultCommand`, todo o nada): reparte cada `conceptLine` entre las líneas del
archivo con su equivalencia (`Priority`: en orden de prelación; `Proportional`: a prorrata de lo enviado, el residuo
de redondeo a la última); cada línea queda «descontado pendiente de giro» o «no descontado» con la causa traducida
por las equivalencias de causas del convenio; un código o empleado sin equivalencia queda en **no aplicados** con
`Lending.PayrollFile.ConceptWithoutEquivalence` o `.EmployeeWithoutEquivalence` **(nuevo en contracts)** y no bloquea
lo demás. Después, Recaudo aplica el giro por los dos contratos (`contrato-de-cuentas.md` §6.4).

**Construido (T091, D36).** `ConsumePayrollDeductionResultCommand` acepta v1 y v2 (`VersionesAceptadasDeCartera`). Las
filas del mismo empleado y código se suman; la prioridad de cada línea se sella al generar
(`LND_PayrollDeductionFileLines.AllocationPriority`, `LND_PayrollDeductionFileAccountLines.AllocationPriority`). Lo de
los créditos sigue el camino de la v1 (resultado, no aplicado, seguimiento); lo de las cuotas de cuentas queda en
`LND_PayrollDeductionAccountResults` —lo descontado, pendiente de giro; lo no descontado, no descontado de la cuota por
`RegistroDeCuentas` (sigue exigible)—; lo que no casa queda en `LND_PayrollDeductionUnmatchedLines` con
`.ConceptWithoutEquivalence`, `.EmployeeWithoutEquivalence` o, si lo descontado supera lo enviado, `.DeductedExceedsSent`
**(nuevo)**. Un `fileGroupCode` que no es el del archivo rechaza el mensaje con `.LineMismatch`.

---

## 6. Quién origina

Las reglas de la 013 §6. Tipos de captaciones que **admiten proceso** (`AdmiteProceso = true`): los de la columna
«Proceso» de §1.1 (causación, liquidación, retención, ajuste, pagos y prórrogas del CDAT, cancelación por instrucción,
reclasificación, bonificación, penalidad del contractual, cargos periódicos, débito automático, devoluciones a no
reclamados y pagos programados), `OrdenDeGiroEmitida` v2 y `CargoDeCaptacionCausado`. Ningún tipo `INV` ni `REC`
admite proceso. El proceso es «Proceso de la cooperativa» y su nombre en el sobre es `Actor.NombreDelProceso`
(FR-017).

---

## 7. Destinos y registro

| Destino | Consumidor | Acepta (catálogo) |
|---|---|---|
| `Accounting` | `DestinoContabilidad` → `PostLendingFactsCommand` / `PostLendingSummaryGroupCommand` | CAR v1 de los 33 de §1.1 (además de los 35 de la 013) |
| `Associates` | `DestinoAsociados` | `RetiroDeAsociadoAprobado` v1 |
| `Treasury` | la feature de Tesorería o `TesoreriaDePrueba` | `OrdenDeGiroEmitida` v1 y v2, `AnulacionDeOrdenDeGiro` v1, `ConsignacionReversadaPorCartera` v1 |
| `Receipts` | la feature de Recaudo o `RecaudadorDePrueba` | `PagoReversadoPorCartera` v1, `ConsignacionReversadaPorCartera` v1, `DescuentosPorPagaduria` v1 y v2 |
| `Inventory` | `DestinoInventario` | `CargoDeCaptacionCausado` v1, `CargoDeCaptacionReversado` v1 (además de los de la 013) |
| `Lending` | `DestinoCartera` | `FacturaDeCargoRegistrada` v1, `ResultadoDeDescuentos` v1 y v2, `ResultadoDelGiro` v1 |

Los dobles de prueba (`TesoreriaDePrueba`, `RecaudadorDePrueba`) consumen las versiones nuevas sin estado propio y
`RecaudadorDePrueba` emite `ResultadoDeDescuentos` v2 a partir del archivo de respuesta que carga quien prueba.

## 8. Idempotencia y versiones

- **Emitido**: único `(OriginPublicId, Type, OriginEventKey)` (la 013 §16); una doble emisión hace fallar el guardado.
- **Recibido**: recibo único por `messageId` en cada destino (`ACC_LendingPostings`, `LND_InboundMessages`, el de
  Asociados `COR_AssociateInboundMessages` **(nuevo en contracts)**, el de Comercial, el de Tesorería y Recaudo).
- **Operaciones**: la clave del contrato o de la pantalla (`contrato-de-cuentas.md` §3.12); los procesos, por la
  marca de agua de cada cuenta (`MemberAccountControl.LastAccruedDate`, `.LastSettledThrough`) y el único de la
  liquidación por cuenta y período (SC-003).
- **Versiones**: todo tipo nuevo nace en v1; `OrdenDeGiroEmitida`, `DescuentosPorPagaduria` y `ResultadoDeDescuentos`
  suman la v2. Lo emitido conserva su versión para siempre.

---

## 9. Nombres fijados en este documento (nuevo en contracts)

- **Tipos de mensaje**: los 33 de §1.1 (salvo los que nombra research R14/R15: `CargoDeCaptacionCausado`,
  `CargoDeCaptacionReversado`, `FacturaDeCargoRegistrada`, `RetiroDeAsociadoAprobado`): `ConsignacionEnCuentaAplicada`,
  `AporteRecibido`, `RetiroDeCuentaAplicado`, `TrasladoEntreCuentasAplicado`, `GmfRetenido`,
  `InteresDeCaptacionCausado`, `AjusteDeCausacionDeCaptacion`, `InteresDeCaptacionLiquidado`, `RetencionPracticada`,
  `CdatConstituido`, `InteresesDeCdatPagados`, `CdatProrrogado`, `CdatRenovado`, `CdatRedimido`, `CdatCancelado`,
  `PenalidadAplicada`, `CaptacionReclasificada`, `BonificacionReconocida`, `CargoDeCuentaCausado`,
  `CargoPendienteCobrado`, `CargoDeCuentaCondonado`, `RevalorizacionDeAportesAplicada`, `ExcedentesDistribuidos`,
  `AportesAmortizados`, `CruceAlRetiroAplicado`, `PerdidaAplicadaAlRetiro`, `DevolucionOrdenada`, `DevolucionGirada`,
  `SaldoTrasladadoANoReclamados`, `EmbargoConsignado`, `OperacionDeCuentaReversada`, `CaptacionesActivadas`,
  `CargaDeCaptacionesAplicada`; y `ConsignacionReversadaPorCartera`, `OrdenDeGiroEmitidaV2`,
  `DescuentosPorPagaduriaV2`, `ResultadoDeDescuentosV2`.
- **Códigos cortos**: `CCN`, `APR`, `RCT`, `TRC`, `GMF`, `ICC`, `AJC`, `ILQ`, `RTF`, `CDC`, `CDP`, `CDR`, `CDN`, `CDD`,
  `CDX`, `PEN`, `RCC`, `BON`, `CGA`, `CGP`, `CGN`, `RVA`, `EXD`, `AMZ`, `CRR`, `PDR`, `DVO`, `DVG`, `SNR`, `EMC`, `RVC`,
  `CAV`, `CAC` (ninguno choca con los 35 de la 013).
- **Bloques**: `MemberAccountRefV1`, `DepositFactDimensionsV1`, `DepositConceptAmountV1`, `DepositFactLineV1`,
  `DepositFactTotalV1`, `TermDepositTermsV1`, `WithholdingDetailV1`, `GmfDetailV1`.
- **Clases de origen** (texto en `OriginDocumentClass`): `AccountDeposit`, `AccountWithdrawal`, `AccountTransfer`,
  `AccountClosing`, `AccountReversal`, `InterestSettlement`, `TermDepositConstitution`, `TermDepositInterestPayment`,
  `TermDepositRollover`, `TermDepositRenewal`, `TermDepositRedemption`, `ContractualMaturity`, `AccountCharge`,
  `MembershipWithdrawal`, `MembershipWithdrawalSettlement`, `ContributionRefund`, `DeceasedDelivery`,
  `UnclaimedPayment`, `AssemblyDistribution`, `UnclaimedTransfer`, `GarnishmentConsignment`, `DepositsActivation`,
  `DepositsMigrationLoad`, `AccountOpeningBalance`.
- **Plataforma**: destino `IntegrationDestinations.Associates`, consumidor `DestinoAsociados`, participante `ASO`,
  recibo `COR_AssociateInboundMessages`, comandos `ConsumeMembershipWithdrawalApprovedCommand` y
  `ConsumeChargeInvoiceRegisteredCommand`; columnas `Kind` y cuenta de `LND_DisbursementOrders`.
- **Valores cerrados**: `OrderKind` (§5.3); causas de `CaptacionReclasificada`, `AjusteDeCausacionDeCaptacion`,
  `CdatCancelado`, `OperacionDeCuentaReversada.initiatedBy`; `accountFlag` (`Standard`, `GmfExempt`, `Garnished`);
  `holderTaxProfile`; `FacturaDeCargoRegistrada.status`.
- **Rótulos de parte**: `CCA-…`, `AJC-…` (`LQC-…` no nació, enmienda E98).
- **Errores**: `Associate.WithdrawalDateMismatch`, `Lending.PayrollAgreement.ReceiptsVersionNotAdopted`,
  `Lending.PayrollFile.ConceptWithoutEquivalence`, `Lending.PayrollFile.EmployeeWithoutEquivalence`.

## 10. Conciliación

| Punto | Decisión |
|---|---|
| FR-096 lista «reclasificación por plazo de emisión y por inactividad» como dos hechos | Un solo tipo `CaptacionReclasificada` con `cause`: las dos mueven saldos entre combinaciones de dimensiones igual que `DimensionContableCambiada` de la 013, y la causa viaja en el contenido. También cubre la marca de embargo y de exención, que cambian `accountFlag` |
| «Retención practicada» | Hecho propio (`RetencionPracticada`) como acompañante o en la parte de la liquidación, para que el certificado y la conciliación de 243535 lo vean aparte del interés |
| «Devolución girada» | Informativo: el pago del giro lo contabiliza Tesorería contra el puente `RefundPayable`; la Cartera sólo deja constancia |
| «Y el reverso de cada uno» | Un tipo genérico `OperacionDeCuentaReversada` con el espejo de cada hecho de la unidad original (molde de `ReversoDeCierre`), en vez de un reverso por tipo |
| Reusar la orden de los desembolsos | Misma entidad, ciclo y resultado; el contenido cambia, así que es la v2 de `OrdenDeGiroEmitida` (la 013 §17 no admite campos nuevos en la v1) |
| Débito automático por el proceso y C14 de la 013 | Cambio de catálogo de `PagoAplicado` (`AdmiteProceso`) acotado a `OwnLoanPayoff` (§1.4) |
