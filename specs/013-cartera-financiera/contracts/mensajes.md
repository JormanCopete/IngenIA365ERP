# Catálogo de mensajes de Cartera

**Feature**: 013 | **Fecha**: 2026-10-05 | **Catálogo**: V1 (nace en K1; lo marcado K2–K4 se declara en K1 y se
emite desde su entrega) | **Base**: `decisiones-transversales.md` §2.6, §2.7, §2.11, §2.12, §2.13, §2.17, §2.18,
§7 (C01–C23, C35, C36), T5, T8–T22, T28, T31, T33, T36, T47, T48, T55–T57, T68, T78, T83–T85, T91, T92, T95, T102, CH3–CH13, CH17, CH20,
CH42; spec FR-003, FR-008, FR-009, FR-012, FR-045–FR-057, FR-080–FR-089, FR-107, FR-112–FR-116, FR-125–FR-130,
FR-142, FR-143, FR-145, FR-149, I-2, I-4, D8, D15, D27, D29 | **Hermanos**: `interfaces.md` (puertos síncronos y
el contrato de obligaciones), `contabilidad.md` (cómo Contabilidad convierte cada hecho en comprobante), `api.md`
(rutas de la bandeja, del registro de participantes y de los dobles); y el contrato de la plataforma que éste
amplía, `specs/012-inventario-comercial/contracts/mensajes.md` (en adelante, **«la 012»**).

Un mensaje de Cartera es un **hecho**: lo registra una operación o un proceso de Cartera en la misma transacción que
guarda lo que cuenta, y lleva todo lo que su destino necesita para actuar **sin leer tablas `LND_`** (FR-008).
Cartera no conoce cuentas: dice qué pasó, a qué crédito, con qué dimensiones y por qué valores; la matriz de
Contabilidad decide las cuentas (FR-126, FR-129). Este documento fija también lo que Cartera **recibe** de Comercial,
Tesorería y Recaudo, y cómo la bandeja de la 012 pasa a tener varios emisores y varios destinos.

Cinco reglas que no se negocian:

- **Nace con lo que cuenta** (T5, T12, T14). `EmisorDeMensajes` agrega el mensaje, sus entregas y sus dependencias
  **sin guardar**, dentro del `SaveChanges` de la operación, de la tanda del proceso o de la transacción final del
  cierre. No hay operación de Cartera sin sus mensajes ni mensaje sin su operación. Cada `LoanTransaction` guarda el
  `MessagePublicId` del hecho que la llevó.
- **No se modifica nunca.** `COR_IntegrationMessages` y sus dependencias son hechos inmutables (`IHechoInmutable`;
  I-5 incluye los mensajes emitidos en el Principio XI). Lo que cambia es la **entrega** por destino (§14).
- **El catálogo dice quién emite qué y adónde** (T10). El módulo de origen, los destinos admitidos y si el proceso
  puede originarlo los declara el tipo en el catálogo, nunca la solicitud de emisión.
- **Un cambio incompatible es otra versión** (§17). Todo nace en v1, salvo `VentaACreditoRegistrada` v2.
- **Nunca lleva cuentas contables** (FR-126; `LosHechosDeCarteraNoLlevanCuentas`).

Dónde vive: los records de Cartera en `Application/Common/Integration/Contracts/Lending/<Type>V1.cs`, con sus bloques
comunes de §9 en la misma carpeta, y el catálogo `CatalogoDeMensajesDeCarteraV1`; los que **recibe** Cartera de
Tesorería y Recaudo (`ResultadoDelGiro`, `EnvioDeDescuentosConfirmado`, `ResultadoDeDescuentos`) también viven ahí,
en `CatalogoDeMensajesHaciaCarteraV1`, porque los define esta especificación (D15, D27) y la feature que los adopte
los emite sin moverlos. `VentaACreditoRegistradaV2` es de Comercial: su record va en `Contracts/Inventory` y su
entrada en `CatalogoDeMensajesV1` (T18). El catálogo agregado `CatalogoDeMensajes` une los tres. Si un record
difiere de este documento, el defecto es del record.

---

## 1. Qué cambia frente a la 012

La bandeja, el despachador, los lotes, el reproceso y el envío posterior son los de la 012 y no se duplican. Lo que
esta feature cambia (E-05, FR-149):

| Pieza | En la 012 | En la 013 | Decisión |
|---|---|---|---|
| Emisores | Sólo Inventario: `EmisorDeMensajes.ModuloDeOrigen = "INV"` | `INV`, `CAR`, `TES`, `REC`; el emisor toma el módulo del tipo y desaparece la constante | T10, CH4 |
| `TipoDeMensaje` | `Record`, `Type`, `Version`, `Destination`, `Kind`, `FormaDeClave` | gana `OriginModule`, `Destinos` (conjunto admitido, reemplaza a `Destination`) y `AdmiteProceso`; las 20 entradas de INV los reciben (`INV`, su destino, `false`) sin cambiar los bytes de sus contratos | T10, T11, T18 |
| Catálogo | `CatalogoDeMensajesV1` | `CatalogoDeMensajes` agrega `CatalogoDeMensajesV1` (INV, sin renombrar ni mover; suma `VentaACreditoRegistradaV2`), `CatalogoDeMensajesDeCarteraV1` (CAR) y `CatalogoDeMensajesHaciaCarteraV1` (TES, REC). `MensajesEntrantes`, el emisor y las versiones aceptadas leen el agregado | T10, CH7 |
| Quién origina | Siempre una persona: el emisor lanza si el actor es el proceso | El proceso origina los tipos con `AdmiteProceso = true` (sólo CAR); `COR_IntegrationMessages.OriginActorKind` y `OriginUserCentralId` nulo (§6) | T11, CH3 |
| `SolicitudDeEmision` | origen, clave, contenidos, modo, cadenas, relacionado | gana `Destino` (cuando el tipo admite varios), `UsuarioDeOrigen` (`UserRefV1?`) y `CadenaDeOrden` (`Guid?`, sólo respuestas a Comercial) | T10–T12 |
| Clase de documento | Enum de Inventario en el sobre | Texto en la columna; el consumidor que no es Inventario la lee de `MensajeEntrante.OriginDocumentClassText` y `RelatedDocumentClassText` (§5) | T10 |
| Destinos | `Accounting`, `Lending` | más `Inventory` (Comercial), `Treasury` (Tesorería) y `Receipts` (Recaudo); un solo consumidor por destino | T15, CH9, CH10 |
| Habilitación de un destino | `Lending` sólo si `ParametrosDeInventario.CarteraIntegracionHabilitadaDesde` ≤ hoy | Consumidor registrado **y** fila activa a hoy en el registro de participantes de la cooperativa (§13) | T16, CH13 |
| Consumo | `Processed`, `AlreadyProcessed`, `Rejected`, `Retry` | más `ResultadoDeConsumo.ValidationFailed` → `DeliveryStatus.ValidationFailed`, `DeliveryAttemptOutcome.ValidationFailed = 5`, alerta `Integracion.ValidacionFallida` (§14) | T17, CH12 |
| Contabilidad como destino | Un módulo de origen | `DestinoContabilidad` reparte por `OriginModule`; `UnidadDeConsumo` gana `OriginModule`; versiones aceptadas por módulo; `PlanearLote` separa por módulo; `TotalesDelLoteAsync` suma los dos recibos | T15, CH6 |
| Despachador | Vacía cada destino antes de pasar al siguiente | Turna los destinos: una tanda por destino y vuelta | T19 |
| Lectura sin consumir | No existe | `IMensajesEntrantes.PendientesAsync(destino, tipos, hastaFechaDeOperacion, página)` | T19 |
| Bandeja | Sólo `/api/inventory/messages` | Estrategia `IVistaDeBandejaDelModulo` por módulo; Cartera en `/api/lending/messages` con `Lending.Messages.*` | T19, CH32 |
| Lotes | `BatchTrigger` 1–6 | más `LendingAccrualRun = 7` y `LendingMonthlyClose = 8`; `ScheduleKey` de Cartera `CAR.{ShortCode}|…` (§14.2) | T15, T56, C06 |

Lo que **no** cambia: la serialización (§4), el índice único `(OriginPublicId, Type, OriginEventKey)`, las cinco
formas de `originEventKey`, el orden por `Id bigint`, la regla de elegibilidad, los reintentos, la verificación de
`PayloadSha256`, el reproceso y el envío posterior. Ningún contrato INV cambia sus bytes
(`ContratosDeMensajesTests`).

---

## 2. Catálogo

`Type` es la cadena guardada, sin tildes; la pantalla muestra la tilde («ObligaciónCreada»). **Corto**: el
`LND_FactTypes.ShortCode` del tipo (3 a 4 letras, único), que va en la `ScheduleKey` (§14.2, C06); sólo lo tienen los
tipos hacia Contabilidad y coincide con la columna «Corto» de `contabilidad.md` §2.1. **Admite proceso**: el tipo
puede quedar con `OriginActorKind = Process` (§6). **Clave**: la forma de `originEventKey` (§7). **Modo**: el modo de
paso por defecto hacia Contabilidad (`Contabilidad.ModoDePaso`, ámbito `LendingFactType`; `NoPasa` mientras la
contabilidad no esté iniciada, T56); «el de su principal» = acompañante que sella el modo del hecho principal de su
unidad (§7.2, C01); «el del original» = reverso que hereda el modo de lo que reversa (C08); los demás destinos
reciben siempre (`Always`). **Comprobante**: el tipo por defecto de `ACC_LendingVoucherMappings` (§2.19 de las
transversales; lo detalla `contabilidad.md`); en una unidad manda el del principal.

### 2.1 Hechos a Contabilidad (`CAR → Accounting`)

| # | `Type` | Corto | `Kind` | Admite proceso | Clave | Lo origina | Modo por defecto | Comp. | E |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `CreditoDesembolsado` | DES | Business | no | `Confirmation` | `LoanOperation` `Disbursement` (cada tramo; también en especie desde una venta) | EnLinea | DS | K1 |
| 2 | `DesembolsoReversado` | DRV | Business | no | `Confirmation` | `LoanOperation` `DisbursementReversal` | el del original (desembolso) | DS | K1 |
| 3 | `CreditoAjustadoDesdeComercial` | ACO | Business | no | `Confirmation` | `LoanOperation` `CommercialAdjustment` | EnLinea | DS | K1 |
| 4 | `InteresCausado` | ICA | Business | sí | `Confirmation:{parte:N}` | Parte de una tanda de la corrida `DailyCycle`, o de la puesta al día de una operación (C04) | PorLotes resumido, `FinDeCausacion` | CA | K1 |
| 5 | `MoraCausada` | MCA | Business | sí | `Confirmation:{parte:N}` | Ídem | PorLotes resumido, `FinDeCausacion` | CA | K1 |
| 6 | `SeguroCausado` | SCA | Business | sí | `Confirmation:{parte:N}` | Ídem | PorLotes resumido, `FinDeCausacion` | CA | K1 |
| 7 | `AjusteDeCausacionPorFechaValor` | AFV | Business | sí | `Confirmation` (acompañante) · `Confirmation:{parte:N}` | Acompaña a la operación fechada atrás que lo provoca, o una parte de la corrida `NationalValueCorrection` (C03, T28) | el de su principal; en la corrida, PorLotes resumido, `FinDeCausacion` | CA | K1 |
| 8 | `CausacionSuspendida` | CSU | Informational | sí | `Confirmation:{tipoDeHecho:N}` · `Confirmation:{crédito:N}` | La operación que cruza el umbral, o una parte del ciclo diario (C02) | Always, sin comprobante | — | K1 |
| 9 | `CausacionReanudada` | CRE | Informational | sí | Ídem | Ídem | Always, sin comprobante | — | K1 |
| 10 | `PagoAplicado` | PAP | Business | no | `Confirmation` | `LoanOperation` `Payment` (contrato de obligaciones) | EnLinea | RC (DN con `Payroll`) | K1 |
| 11 | `PagoReversado` | PRV | Business | no | `Confirmation` | `LoanOperation` `PaymentReversal` | el del original (pago) | RC (DN con `Payroll`) | K1 |
| 12 | `AnticipoRecibido` | ANR | Business | no | `Confirmation` (acompañante) | Acompaña a `PagoAplicado` | el de su principal | RC | K1 |
| 13 | `AnticipoAplicado` | ANA | Business | sí | `Confirmation:{crédito:N}` | Parte del ciclo diario, al vencer la cuota (C02) | EnLinea | RC | K1 |
| 14 | `CobroDeInteresEnSuspenso` | CIS | Business | no | `Confirmation` (acompañante) | Acompaña a `PagoAplicado` | el de su principal | RC | K1 |
| 15 | `SaldoAFavorReconocido` | SFR | Business | sí | `Confirmation` (acompañante) · `Confirmation:{crédito:N}` | Acompaña al pago sin preferencia o al ajuste de Comercial que lo deja; o una parte de la corrida `NationalValueCorrection` (C02) | el de su principal; en la corrida, EnLinea | RC | K1 |
| 16 | `SaldoAFavorResuelto` | SFS | Business | no | `Confirmation` | `LoanOperation` `CreditBalanceResolution` | EnLinea | RC | K1 |
| 17 | `RecuperacionDeCastigada` | RCA | Business | no | `Confirmation` (acompañante) | Acompaña a `PagoAplicado` sobre un castigado | el de su principal | RC | K1 |
| 18 | `DimensionContableCambiada` | DCC | Business | sí | `Confirmation:{tipoDeHecho:N}` · `Confirmation:{crédito:N}` | La operación que cambia la dimensión, o una parte del ciclo diario que la detecta (C02) | EnLinea si la emite una operación; PorLotes `FinDeCausacion` si la emite el proceso (T56) | CN | K1 |
| 19 | `CobroProvisionalIncorporado` | CPI | Business | no | `Confirmation` | `LoanOperation` `ProvisionalCollection` (plantilla al activar) | EnLinea | RC | K1 |
| 20 | `DeterioroCalculado` | DET | Business | sí | `Close:{versión}` | Parte del cierre mensual (transacción final, T47) | PorLotes resumido, `CierreDeCartera` | PV | K1 |
| 21 | `CarteraReclasificadaPorCalificacion` | RCL | Business | sí | `Close:{versión}` | Ídem | PorLotes resumido, `CierreDeCartera` | PV | K1 |
| 22 | `ReversoDeCierre` | RCI | Business | sí | `Reopen:{versión}` | Parte de la reapertura del cierre (corrida `CloseReopen`, C58) | el del original (la parte del cierre) | PV | K1 |
| 23 | `GastoDeCobranzaCausado` | GCO | Business | no | `Confirmation` | `LoanOperation` `Charge` | EnLinea | CA | K4 (declarado en K1) |
| 24 | `CreditoReestructurado` | REE | Business | no | `Confirmation` | `LoanOperation` `TermsChange` | EnLinea | CN | K3 (declarado en K1) |
| 25 | `CreditoModificado` | MOD | Business | no | `Confirmation` | Ídem | EnLinea | CN | K3 (declarado en K1) |
| 26 | `CreditoNovado` | NOV | Business | no | `Confirmation` | Ídem | EnLinea | CN | K3 (declarado en K1) |
| 27 | `CondonacionAprobada` | CON | Business | no | `Confirmation` | `LoanOperation` `Waiver` | EnLinea | CN | K3 (declarado en K1) |
| 28 | `CreditoCastigado` | CAS | Business | no | `Confirmation` | `LoanOperation` `WriteOff` (ítem del lote) | EnLinea | PV | K3 (declarado en K1) |
| 29 | `CarteraActivada` | ACT | Informational | no | `Confirmation` | Activación (`LND_Activations`) | Always, con recibo | — | K1 |
| 30 | `CargaDeCarteraAplicada` | CAP | Informational | no | `Confirmation` | Carga aplicada (`LND_MigrationLoads`) | Always, con recibo | — | K1 |
| 31 | `CierreDeCarteraRealizado` | CIR | Informational | sí | `Close:{versión}` | Cierre mensual (`LND_MonthlyCloses`) | Always, con recibo | — | K1 |
| 32 | `CierreDeCarteraReabierto` | CRA | Informational | sí | `Reopen:{versión}` | Reapertura del cierre | Always, con recibo | — | K1 |
| 33 | `CreditoAprobadoSinDesembolsar` | CAD | Business | sí | `Confirmation` · `Confirmation:{tipoDeHecho:N}` · `Confirmation:{solicitud:N}` | La aprobación de la solicitud; aparte, cada tramo desembolsado y el desistimiento o la anulación; una parte del ciclo diario al vencer la aprobación (FR-042) | NoPasa | CN | K1 (A3) |
| 34 | `CupoRotativoOtorgado` | CRO | Business | sí | `Confirmation` · `Confirmation:{tipoDeHecho:N}` · `Confirmation:{cupo:N}` | El otorgamiento, la ampliación o la reducción del cupo rotativo; una parte del ciclo diario al vencer el cupo (usarlo o pagarlo no lo mueve: 913005 mide el cupo otorgado, C92) | NoPasa | CN | K1 (A3) |
| 35 | `GarantiaConstituida` | GCT | Business | sí | `Confirmation` · `Confirmation:{tipoDeHecho:N}` · `Confirmation:{garantía:N}` | La constitución, la revaluación, la extinción o la ejecución de la garantía; aparte, su liberación por la cancelación del crédito; una parte del ciclo diario cuando la liberación espera la firmeza (D23) | NoPasa | CN | K1 (A3) |
| 48 | `CorreccionMonetariaUvrCausada` | CMU | Business | sí | `Confirmation:{parte:N}` | Parte de una tanda de la corrida `DailyCycle` o `NationalValueCorrection` (corrección de una `UVR`, fase 24), o de la puesta al día de una operación (C04), sólo de los créditos **en UVR** | PorLotes resumido, `FinDeCausacion` | CA | K6b (E12) |

`{parte:N}` es el `PublicId` de la parte (`LND_AccountingFactParts`); `{tipoDeHecho:N}`, el `PublicId` de la fila
del tipo en `LND_FactTypes`; `{crédito:N}`, `{solicitud:N}`, `{cupo:N}` y `{garantía:N}`, el `PublicId` del crédito,
de la solicitud (`LND_CreditApplications`), del cupo (`LND_CreditLimits`) o de la garantía (`LND_Collaterals`) (§7).
Los acompañantes (lista fija de §7.2) no tienen modo propio: sellan el de su principal, y `Contabilidad.ModoDePaso`
no los ofrece en el ámbito `LendingFactType` cuando viajan así (C01).

La fila 48 (numerada después de las de §2.2 y §2.3 porque llegó con K6b, 2026-10-17) es la **corrección monetaria** de los
créditos en UVR (pregunta E12 con su propuesta por defecto, pendiente de la contadora): su contenido está en §10.25.

Las filas 33 a 35 son las **cuentas contingentes** del CUIF (9115, 913005 y 9110) que propone A3 (C15): nacen en K1
con modo `NoPasa`, la contadora decide cuándo pasan, y si el dueño rechaza A3 salen del catálogo sin tocar nada más.
Su contenido está en §10.24.

### 2.2 Mensajes de Cartera a otros destinos

| # | `Type` | Destino | `Kind` | Admite proceso | Clave | Lo origina | E |
|---|---|---|---|---|---|---|---|
| 36 | `ObligacionCreada` | `Inventory` | Business | sí | `Confirmation` | Recibo de la venta (`LND_InboundMessages`) al crear la obligación, o al resolver una venta conciliada en la activación; también el recibo de un `AjusteDeVentaACredito` `Replacement` (C17) | K1 |
| 37 | `ObligacionRechazada` | `Inventory` | Business | sí | `Confirmation:{evaluación:N}` | Recibo de la venta o del ajuste, una por evaluación negativa de conversión | K1 |
| 38 | `OrdenDeGiroEmitida` | `Treasury` | Business | no | `Confirmation` | Orden de giro (`LND_DisbursementOrders`), en el desembolso, también la que se paga por archivo (C10) | K1 |
| 39 | `AnulacionDeOrdenDeGiro` | `Treasury` | Business | no | `Confirmation:{orden:N}` | La misma orden, al anularla un usuario (C12) | K1 |
| 40 | `PagoReversadoPorCartera` | `Receipts` (conjunto admitido en V1; §11.5) | Business | no | `Confirmation` | `LoanOperation` `PaymentReversal` iniciada en Cartera | K1 |
| 41 | `DescuentosPorPagaduria` | `Receipts` | Business | no | `Confirmation` | Archivo de descuentos (`LND_PayrollDeductionFiles`, cada versión) | K2 (declarado en K1) |

### 2.3 Mensajes que recibe Cartera (destino `Lending`)

| # | `Type` | v | Módulo de origen | `Kind` | Clave | Catálogo | Consumo | E |
|---|---|---|---|---|---|---|---|---|
| 42 | `VentaACreditoRegistrada` | 1 | `INV` | Business | `Confirmation:{pago:N}` | `CatalogoDeMensajesV1` (existe) | `ConsumeCommercialCreditMessageCommand` | 012 |
| 43 | `VentaACreditoRegistrada` | 2 | `INV` | Business | `Confirmation:{pago:N}` | `CatalogoDeMensajesV1` (nueva entrada, T18) | ídem | K1 (E-02) |
| 44 | `AjusteDeVentaACredito` | 1 | `INV` | Business | `Confirmation:{pagoOriginal:N}` | `CatalogoDeMensajesV1` (existe) | ídem | 012 |
| 45 | `ResultadoDelGiro` | 1 | `TES` | Business | `Confirmation:{orden:N}` | `CatalogoDeMensajesHaciaCarteraV1` | `ConsumeTreasuryResultCommand` | K1 |
| 46 | `EnvioDeDescuentosConfirmado` | 1 | `REC` | Business | `Confirmation:{archivo:N}` | `CatalogoDeMensajesHaciaCarteraV1` | `ConsumePayrollDeductionResultCommand` | K2 |
| 47 | `ResultadoDeDescuentos` | 1 | `REC` | Business | `Confirmation:{archivo:N}` | `CatalogoDeMensajesHaciaCarteraV1` | ídem | K2 |

El catálogo tiene **47 filas**: 35 hechos `CAR → Accounting`, 6 mensajes de Cartera a otros destinos (41 tipos `CAR`) y
6 que recibe Cartera. Ningún tipo `INV`, `TES` ni `REC` admite proceso: sólo los tipos CAR marcados (T11;
`EmisionesDeProcesoPermitidas`). Tesorería y Recaudo emiten con la persona que actúa o con el usuario de origen que
ellos mismos hereden (C14).

### 2.4 Lo que declara cada entrada del catálogo

```csharp
public sealed record TipoDeMensaje(
    Type Record,
    string Type,
    int Version,
    string OriginModule,                   // "INV" | "CAR" | "TES" | "REC"
    IReadOnlySet<string> Destinos,         // valores de IntegrationDestinations
    IntegrationMessageKind? Kind,          // nulo = el del original (sólo DocumentoAnulado de la 012)
    FormaDeClaveDeEvento FormaDeClave,
    bool AdmiteProceso);
```

- `Destinos` tiene un solo elemento en todos los tipos de V1. `PagoReversadoPorCartera` es el único pensado para
  varios (el destino del recaudador del pago): cuando Aportes registre el suyo, se agrega al conjunto y la solicitud
  dice `Destino`. Agregar un destino al conjunto o cambiar `AdmiteProceso` es un cambio del catálogo, no del
  contenido: no crea versión, pero sí revisión de `ElCatalogoDeMensajesEsUnico` y `EmisionesDeProcesoPermitidas`.
- `ElCatalogoDeMensajesEsUnico` exige `(Type, Version)` único en el agregado, módulo declarado y record único;
  `SoloElEmisorEscribeLaBandeja` exige que sólo `EmisorDeMensajes` escriba `COR_IntegrationMessages`.

---

## 3. Qué emite cada origen de Cartera

El origen de un mensaje es el **documento u operación de Cartera que produce el hecho**, nunca el crédito (T12).
`+` = en la misma transacción.

| Origen (`origin.documentClass`) | Mensajes |
|---|---|
| `LoanDisbursement` (cada tramo) | `CreditoDesembolsado` + `OrdenDeGiroEmitida` (si no es en especie; origen: la orden; también si se pagará por archivo, C10). La recogida de créditos propios emite, por cada crédito recogido, un `PagoAplicado` con su propia operación de pago (`OwnLoanPayoff`, FR-046). Aparte, con A3: `CreditoAprobadoSinDesembolsar` (baja); el uso del cupo rotativo no emite `CupoRotativoOtorgado` (C92) |
| `LoanDisbursement` en especie (consumo de una venta) | `CreditoDesembolsado` + `ObligacionCreada` (origen: el recibo de la venta) |
| `DisbursementReversal` | `DesembolsoReversado` (las filas espejo de la causación del crédito van en su mismo contenido) con su acompañante `AjusteDeCausacionPorFechaValor` cuando el crédito tiene otros tramos (sólo se espeja el capital del tramo; la diferencia de causación sale del recálculo, C49). Si el crédito se anula, en la misma transacción un `PagoReversado` por cada pago de recogida (C11) |
| `CommercialAdjustment` | `CreditoAjustadoDesdeComercial` con su acompañante `SaldoAFavorReconocido` si el ajuste supera lo adeudado |
| `PaymentApplication` | `PagoAplicado` con sus acompañantes `AnticipoRecibido`, `CobroDeInteresEnSuspenso`, `RecuperacionDeCastigada`, `SaldoAFavorReconocido` (abono sin preferencia, D24) y `AjusteDeCausacionPorFechaValor` (fecha valor anterior a lo causado, T33, C03); aparte, si corresponde, `CausacionReanudada` y `DimensionContableCambiada` (el pago no mueve `CupoRotativoOtorgado`, C92) |
| `PaymentReversal` | `PagoReversado` con su acompañante `AjusteDeCausacionPorFechaValor`; `PagoReversadoPorCartera` si lo inició un usuario de Cartera (FR-080); aparte, si corresponde, `CausacionSuspendida`, `DimensionContableCambiada` |
| `CreditBalanceResolution` | `SaldoAFavorResuelto` (+ `PagoAplicado` en la obligación destino, con su propia operación) |
| `ProvisionalCollection` | `CobroProvisionalIncorporado` |
| `LoanCharge` | `GastoDeCobranzaCausado` (K4) |
| `TermsChange` | `CreditoReestructurado`, `CreditoModificado` o `CreditoNovado` (K3); aparte `DimensionContableCambiada` si cambia una dimensión |
| `Waiver` | `CondonacionAprobada` (K3) |
| `WriteOff` | `CreditoCastigado` (K3); aparte `CausacionSuspendida` |
| `RateReset`, `PreferenceChange`, `LoanOpeningBalance` | ninguno (el saldo inicial no genera hechos, FR-140) |
| `AccountingFactPart` de `DailyCycle` | por parte: `InteresCausado`, `MoraCausada`, `SeguroCausado`; por crédito: `AnticipoAplicado`, `CausacionSuspendida`, `CausacionReanudada`, `DimensionContableCambiada`; con A3, al vencer: `CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida` (liberación tras la firmeza) |
| `AccountingFactPart` de la puesta al día de una operación (C04) | por parte: `InteresCausado`, `MoraCausada`, `SeguroCausado` (una parte por tipo; la operación es la tanda) |
| `AccountingFactPart` de `NationalValueCorrection` | por parte: `AjusteDeCausacionPorFechaValor`; por crédito: `SaldoAFavorReconocido` |
| `AccountingFactPart` de `MonthlyClose` | por parte, en este orden: `CarteraReclasificadaPorCalificacion`, `DeterioroCalculado` (T48) |
| `AccountingFactPart` de `CloseReopen` | por parte: `ReversoDeCierre` |
| `MonthlyClose` | `CierreDeCarteraRealizado`; al reabrirlo, `CierreDeCarteraReabierto` |
| `LendingActivation` | `CarteraActivada` |
| `MigrationLoad` | `CargaDeCarteraAplicada` (al aplicarla) |
| `DisbursementOrder` | `OrdenDeGiroEmitida`; `AnulacionDeOrdenDeGiro`; una orden nueva lleva `related` = la anterior |
| `PayrollDeductionFile` (K2) | `DescuentosPorPagaduria` |
| `InboundMessage` (recibo de un mensaje de Comercial) | `ObligacionCreada`, `ObligacionRechazada` |
| `CreditApplication` (A3) | `CreditoAprobadoSinDesembolsar` (al aprobar; baja al desistir o anular) |
| `CreditLimit` (A3) | `CupoRotativoOtorgado` (otorgar, ampliar, reducir) |
| `Collateral` (A3) | `GarantiaConstituida` (constituir, revaluar, extinguir, ejecutar) |

Un **hecho principal** es el que da nombre a la operación (`CreditoDesembolsado`, `PagoAplicado`, `PagoReversado`…).
Los **acompañantes** viajan en su misma unidad; lo que va **aparte** forma su propia unidad (§7.2).

---

## 4. Serialización

La de la 012 §3, sin cambios: `System.Text.Json`, camelCase en inglés, enums como **texto**, decimales exactos, nulos
escritos, `PayloadJson` en UTF-8 exacto con `PayloadSha256`, Guid en formato `D` en minúsculas, toda persona y
sucursal por su `PublicId` (Principio VI) y los catálogos por su código. Lo propio de Cartera:

| Dato | Forma |
|---|---|
| Montos en pesos | `decimal(18,2)` (alias `Monto` de `PrecisionDeCartera`) |
| Montos en UVR (K6b) | `decimal(18,4)` (alias `ValorUvr`), en `uvrAmount`; nulos hasta K6b |
| Tasas | **fracción** `decimal(9,6)` (alias `TasaAnual`): `0.268200` es 26,82 % EA. La tasa diaria de 18 decimales (`TasaEquivalente`) no viaja |
| Fechas | `DateOnly` en la hora de Colombia (`HoyLocal`, T9): `valueDate`, `dueDate`, `cutoffDate`… |
| Instantes | `DateTimeOffset` UTC con `Z`: `registeredAt`, `informationCutoffAt`, `emittedAt` |
| Tipos de Cartera | Como texto con los valores de su enum: `LoanConcept`, `LoanOperationKind`, `PaymentModality`, `PrepaymentChoice`, `LoanStatus`, `RatingSubcategory`, `ImpairmentModel`, `DisbursementOrderStatus`, `PayrollNoveltyKind`, `LendingProcessKind`; y los de puerto `ClaseDeRecaudador`, `ClaseDeMedioDeRecaudo`, `MotivoDeNoDescuento` |
| Códigos de catálogo | Línea (`creditLineCode`), modalidad y sub-segmento (documento `CatalogoDeModalidades`), segmento SARC, pagaduría por su persona, bancos por `COR_Banks.Code` |
| Signo | Como la 012: positivo es el efecto natural del hecho (el que describe `contabilidad.md`); negativo, el contrario. `CreditoAjustadoDesdeComercial`, `AjusteDeCausacionPorFechaValor` y las reasignaciones de pagos (`reallocations`) son los únicos que mezclan signos dentro del mismo contenido |
| Números visibles | El del crédito (`loanNumber`) es el documento cruce (FR-126); el de la operación va en `origin.number` |

Ningún contenido lleva cuentas, ni Id internos, ni datos de una persona que no estén en su copia sellada (I-1:
la orden de giro y la respuesta a Comercial copian la identificación tal como se selló).

---

## 5. El sobre (`IntegrationEnvelopeV1`)

Es el de la 012 §4, el mismo record y las mismas columnas de `COR_IntegrationMessages`; el contenido de §10 a §12
es `PayloadJson`. Lo que cambia es **qué significa cada campo** cuando el emisor no es Inventario:

| Campo | Tipo | En la 012 | Con varios emisores (013) |
|---|---|---|---|
| `messageId` | Guid | `COR_IntegrationMessages.PublicId`, clave de idempotencia del destino | Igual |
| `type` | string | Uno de los veinte | Uno de §2 |
| `version` | int | `1` | `1`, salvo `VentaACreditoRegistrada` v2 |
| `kind` | enum | `Business` / `Informational` | El que declara el tipo; ningún tipo de Cartera hereda `Kind` |
| `originModule` | string(3) | Siempre `"INV"` | El del tipo en el catálogo: `INV`, `CAR`, `TES` o `REC`. `CAR` coincide con `ModuloContable.Cartera` |
| `originEventKey` | string(80) | §10.1 de la 012 | §7 de este documento |
| `origin.kind` | enum | `Document` u `Operation` | CAR: `Document` para una `LoanOperation`, una orden de giro, un archivo de descuentos, el recibo de un mensaje y, con A3, la solicitud, el cupo y la garantía; `Operation` para una parte, un cierre, la activación y una carga. TES y REC: `Document` |
| `origin.documentClass` | texto | Enum `DocumentClass` de Inventario | **Texto** de la columna `OriginDocumentClass` (≤ 40): las clases de §3 (`LoanDisbursement`, `PaymentApplication`, `AccountingFactPart`…). El record tipa el campo con el enum de Inventario, así que para CAR, TES y REC llega nulo: el consumidor lee `MensajeEntrante.OriginDocumentClassText` |
| `origin.documentTypeCode` | string(10) | Tipo de documento de Inventario | CAR: el prefijo del numerador de la clase (`LND_NumberSequences`), nulo en un origen `Operation`. TES y REC: su tipo de documento |
| `origin.number` | string(30) | Número visible | CAR: número de la operación, de la orden o del archivo; en una parte, su rótulo (`CAU-20261214-0007`, `VNC-…`, `CIE-202612-v1-0003`, `REA-…`, y `ALD-PAG-2026-004811-1` en la puesta al día de una operación); en un cierre, `aaaa-mm`; en la activación, su fecha; en el recibo de un mensaje, el número del documento de Comercial que lo emitió |
| `origin.publicId` | Guid | Documento u operación | El origen de §3 |
| `origin.operationDate` | DateOnly | Fecha del documento | **Hacia Contabilidad: la fecha contable** de T36 (la fecha valor si su mes no tiene cierre de Cartera y está abierto en Contabilidad; si no, el primer día del primer mes que cumpla las dos). Causación del día *d*: *d*. Cierre de *M*: último día de *M*. Hacia Tesorería: la fecha contable del desembolso (`interfaces.md` §4.1). Hacia Recaudo: la del reverso o la de la generación del archivo. En las respuestas a Comercial: la de la venta. TES y REC: la de su documento |
| `origin.fiscalUniqueCode` | string | CUFE/CUDE/CUDS | Nulo en CAR, TES y REC |
| `related` | `DocumentRefV1?` | Lo que anula, corrige o devuelve | El original del que depende (§8): pago, desembolso, desembolso en especie, parte del cierre, orden anterior, la orden que se anula; en TES la orden, en REC el archivo. La clase llega en `MensajeEntrante.RelatedDocumentClassText` |
| `chainRootPublicId` | Guid | Raíz del modo heredado | CAR: el propio origen (Cartera **sella** el modo por tipo, T56), salvo en dos casos: los **reversos** (`DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre`), que heredan el modo de su original y llevan la raíz de éste (C08); y las respuestas a Comercial, donde es `CadenaDeOrden` = la venta (`origin.publicId` del mensaje de Comercial). TES y REC: su propio origen |
| `branchPublicId` | Guid | Sucursal de la bodega o del punto | CAR: sucursal contable (`COR_Branches`) vinculada a la oficina del crédito (`TenantBranchPublicId`, T13); en una parte, la de la parte; sin crédito (activación, carga, cierre, archivo de descuentos), la principal. Una oficina con créditos sin sucursal vinculada **no emite**: la operación se rechaza antes de confirmar y el proceso deja el crédito en `LND_ProcessRunErrors` con alerta (C9, CH49) |
| `costCenterPublicId` | Guid? | Centro del documento | Nulo en CAR: el centro lo pone la regla de la matriz (`LendingPostingRule.CostCenterId`) |
| `warehouseCode` | string? | Bodega | Nulo en CAR, TES y REC |
| `personPublicId` | Guid? | Tercero del documento | CAR: tercero del hecho (T13): el **pagador** en pagos y reversos (deudor, codeudor, pagaduría, aseguradora u otro), el deudor en los demás hechos por crédito, el comprador en las respuestas a Comercial, el beneficiario en la orden de giro y su anulación (`interfaces.md` §4.1), la pagaduría en el archivo de descuentos, el deudor (o el titular del cupo o de la garantía) en los contingentes de A3; nulo en partes y operaciones sin persona. TES: el beneficiario; REC: la pagaduría |
| `currency` · `exchangeRate` | string(3) · decimal | `"COP"` · `1` | Igual |
| `originUser` | `UserRefV1` | Quien confirmó; `centralUserId` obligatorio | El usuario de origen resuelto (§6). `centralUserId` **nulo** ⇔ `COR_IntegrationMessages.OriginActorKind = Process`; entonces `name` es `Actor.NombreDelProceso` («Proceso de integración», el nombre del actor que ya existe; C23), el mismo que `contabilidad.md` pone en «registrado por». Es dato, nunca actor |
| `emittedAt` | DateTimeOffset | Instante UTC del `SaveChanges` | Igual |

**Lo que no va en el sobre** (además de lo de la 012): la **fecha valor** y la **de registro** van en el contenido
(T13); `OriginActorKind` es columna, y el consumidor la deduce de `originUser.centralUserId`; el **modo sellado**
vive en la entrega; `PrevalidationOutcome` es columna: Cartera la sella siempre en sus hechos (`Postable`,
`NotPostable` —valor nuevo, C21: la evaluación encontró un hallazgo y no frenó—, `NoResponse`, `NotApplicable`; T56).

**Ejemplo.** La nómina ordinaria de la segunda quincena de diciembre se aprueba el 16 a las 9:20 a. m. (hora de
Colombia) y aplica por el contrato la cuota 9 del crédito `CR-0001234` de un empleado (`interfaces.md` §3.7). El
pago de la quincena tiene fecha valor del fin del período (enmienda A9) y diciembre está abierto, así que la fecha
contable es la misma. Es la unidad `Confirmation` de la operación `PAG-2026-004811`, con comprobante `DN`.

```json
{
  "messageId": "7e1f2a3b-4c5d-4e6f-8a9b-0c1d2e3f4a51",
  "type": "PagoAplicado",
  "version": 1,
  "kind": "Business",
  "originModule": "CAR",
  "originEventKey": "Confirmation",
  "origin": {
    "kind": "Document",
    "documentClass": "PaymentApplication",
    "documentTypeCode": "PAG",
    "number": "PAG-2026-004811",
    "publicId": "2c3d4e5f-6a7b-4c8d-9e0f-1a2b3c4d5e6f",
    "operationDate": "2026-12-15",
    "fiscalUniqueCode": null
  },
  "related": null,
  "chainRootPublicId": "2c3d4e5f-6a7b-4c8d-9e0f-1a2b3c4d5e6f",
  "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
  "costCenterPublicId": null,
  "warehouseCode": null,
  "personPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
  "currency": "COP",
  "exchangeRate": 1,
  "originUser": { "centralUserId": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f70", "name": "Jefe de nómina de ensayo" },
  "emittedAt": "2026-12-16T14:20:31.512Z",
  "payload": {
    "loan": { "loanPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a", "loanNumber": "CR-0001234" },
    "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
    "valueDate": "2026-12-15",
    "registeredAt": "2026-12-16T14:20:31.204Z",
    "collector": {
      "collectorCode": "NOM",
      "collectorClass": "Payroll",
      "idempotencyKey": "NOM:6f1c2a9e4b7d4c0e9a1f3b5d7c9e1a2b:0d4e6f8a1b3c4d5e6f7a8b9c0d1e2f3a",
      "meansClass": "OwnPayrollDeduction",
      "payerPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
      "receipt": { "number": "NM-000318", "date": "2026-12-16", "officePublicId": null, "meansReference": null, "documentKind": "NM" }
    },
    "modality": "CycleInstallment",
    "amountReceived": 412350.00,
    "surplus": 0.00,
    "dimensions": {
      "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza",
      "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false,
      "restructured": false, "writtenOff": false, "collectorClass": "Payroll", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
      "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos"
    },
    "amounts": [
      { "concept": "Principal", "installmentNumber": 9, "amount": 318600.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null },
      { "concept": "CurrentInterest", "installmentNumber": 9, "amount": 89250.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null },
      { "concept": "Insurance", "installmentNumber": 9, "amount": 4500.00, "uvrAmount": null, "onBehalfOfAssociate": true, "reasonCode": null }
    ],
    "reallocations": [],
    "prepaymentChoice": null,
    "newPlan": null,
    "paidOff": false,
    "finalityUntil": null,
    "simulationId": null,
    "deductionFileLinePublicId": null,
    "insuranceClaimPublicId": null,
    "rulesVersion": "LND-2026.1"
  }
}
```

Cuadra: `318.600 + 89.250 + 4.500 = 412.350`, igual a lo recibido, sin excedente.

---

## 6. Quién origina: persona, proceso y usuario de origen

El emisor resuelve el **usuario de origen** de cada solicitud en este orden (T11):

1. la **persona que actúa** (`IActorActual`, con fila en `SEC_Users`);
2. si no hay, el `UsuarioDeOrigen` de la solicitud: el `originUser` del mensaje que se está consumiendo, o la persona
   que ordenó la corrida;
3. si tampoco, el **proceso**: `OriginActorKind = Process`, `OriginUserCentralId` nulo y nombre «Proceso de
   integración».

El tercer caso sólo se admite en los tipos con `AdmiteProceso = true`; en cualquier otro, el emisor lanza y la
operación falla entera (es un defecto, nunca un mensaje silencioso). Lo fija `EmisionesDeProcesoPermitidas`, que
además exige `false` en todo tipo `INV`, `TES` y `REC`: para Inventario sigue intacta la regla de la 012 «el mensaje
lo origina la persona».

| Situación | Actor | Usuario de origen | `OriginActorKind` |
|---|---|---|---|
| Operación de pantalla de Cartera (desembolso, reverso, anulación de orden, activación, carga, reapertura del cierre) | la persona | la persona | `Person` |
| Contrato de obligaciones llamado por Nómina al aprobar | la persona que aprueba | la persona | `Person` |
| Corrida ordenada a mano (`RequestLendingProcessRunCommand`, `OrderMonthlyCloseCommand`) | la persona que la ordenó (T8) | la persona | `Person` |
| Ciclo diario, cierre programado, corrida `NationalValueCorrection` (la encola la réplica de valores nacionales, C05) | el proceso de la cooperativa | — | `Process` |
| Consumo de un mensaje de Comercial, Tesorería o Recaudo | el proceso («Proceso de integración») | el `originUser` del mensaje consumido | `Person` si ese mensaje lo trae; `Process` si no |
| Dobles de prueba (§18) | la persona que prueba | la persona | `Person` |

Tipos que admiten proceso (T11): los masivos de la causación y del cierre (`InteresCausado`, `MoraCausada`,
`SeguroCausado`, `CorreccionMonetariaUvrCausada` (K6b), `DeterioroCalculado`, `CarteraReclasificadaPorCalificacion`, `ReversoDeCierre`),
`CausacionSuspendida`, `CausacionReanudada`, `AnticipoAplicado`, `DimensionContableCambiada`,
`AjusteDeCausacionPorFechaValor`, `SaldoAFavorReconocido`, los informativos de cierre (`CierreDeCarteraRealizado`,
`CierreDeCarteraReabierto`), las respuestas a Comercial (`ObligacionCreada`, `ObligacionRechazada`) y, con A3, los tres
contingentes (`CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida`: el vencimiento de una
aprobación o de un cupo y la liberación tras la firmeza los detecta el ciclo, C15).

**Recaudo sin persona** (C14). Se mantiene T11: `PagoAplicado` y sus acompañantes **no** admiten proceso, y
`SolicitudDeAplicacion` no lleva usuario de origen. La feature 014 (Recaudo) aplica cada pago automático —débito
automático, respuesta de una pagaduría— a nombre de la **persona que cargó o aprobó el lote**; es una restricción de
esa feature, no de este catálogo. K1 no se afecta: Nómina y los dobles actúan con persona.

El usuario de origen es **dato**: el consumidor nunca usa sus permisos ni su alcance (I-2). Contabilidad lo copia como
«registrado por» del comprobante (`contabilidad.md`).

---

## 7. Claves de evento y unidades

### 7.1 `originEventKey`

Sólo las formas de `ClavesDeEvento` (T12): no se crea ninguna. `{x:N}` es un Guid en formato `N` (32 hexadecimales
en minúsculas).

| Valor | Origen | Mensajes |
|---|---|---|
| `Confirmation` | La operación o el documento | El hecho principal de una operación y sus acompañantes (§7.2); `CobroProvisionalIncorporado`; `CarteraActivada`, `CargaDeCarteraAplicada`; `OrdenDeGiroEmitida`; `PagoReversadoPorCartera`; `DescuentosPorPagaduria`; `ObligacionCreada`; con A3, el contingente principal de su solicitud, cupo o garantía |
| `Confirmation:{tipoDeHecho:N}` | La operación | Cada hecho que una operación emite **aparte** de su unidad principal: `CausacionSuspendida`, `CausacionReanudada`, `DimensionContableCambiada` y, con A3, el contingente que mueve una operación ajena (desembolso, pago, cancelación). `tipoDeHecho` es el `PublicId` de la fila del tipo en `LND_FactTypes` (sembrado con Guid fijo) |
| `Confirmation:{parte:N}` | La parte (`LND_AccountingFactParts`) | Masivos: `InteresCausado`, `MoraCausada`, `SeguroCausado`, `CorreccionMonetariaUvrCausada` (K6b) (de la corrida o de la puesta al día de una operación, C04) y el `AjusteDeCausacionPorFechaValor` de la corrida `NationalValueCorrection` (C03) |
| `Confirmation:{crédito:N}` | La parte de su tanda, sucursal y tipo | Hechos por crédito que nace un proceso (C02): `AnticipoAplicado`, `CausacionSuspendida`, `CausacionReanudada`, `DimensionContableCambiada` (ciclo diario) y `SaldoAFavorReconocido` (corrida `NationalValueCorrection`). Un mensaje por crédito, nunca con el crédito como origen; modo propio del tipo |
| `Confirmation:{solicitud:N}` · `Confirmation:{cupo:N}` · `Confirmation:{garantía:N}` | La parte de su tanda | Con A3, el contingente que el ciclo diario detecta (vencimiento de la aprobación o del cupo, liberación de la garantía tras la firmeza), con la misma forma que los hechos por crédito de un proceso |
| `Confirmation:{evaluación:N}` | El recibo del mensaje de Comercial | `ObligacionRechazada`: una por evaluación (`LND_InboundMessageEvaluations.PublicId`), así Comercial ve el motivo vigente tras cada reproceso |
| `Confirmation:{orden:N}` | La orden (Cartera) o el documento de Tesorería | `AnulacionDeOrdenDeGiro`; `ResultadoDelGiro` |
| `Confirmation:{archivo:N}` | El documento de Recaudo | `EnvioDeDescuentosConfirmado`, `ResultadoDeDescuentos` (`archivo` = `PublicId` de la versión del archivo de descuentos) |
| `Confirmation:{pago:N}` · `Confirmation:{pagoOriginal:N}` | El documento de Comercial | `VentaACreditoRegistrada` v1 y v2 · `AjusteDeVentaACredito` (012 §10.1) |
| `Close:{versión}` | La parte del cierre; el cierre | `CarteraReclasificadaPorCalificacion`, `DeterioroCalculado`; `CierreDeCarteraRealizado` |
| `Reopen:{versión}` | La parte de la reapertura; el cierre que se reabre | `ReversoDeCierre`; `CierreDeCarteraReabierto` |

`versión` es la `Version` del `MonthlyClose` (volver a cerrar un mes crea `Version + 1`, T47). En el cierre y su
reapertura el origen de cada mensaje masivo es la **parte**, no el cierre: así dos partes del mismo tipo no chocan
en el único `(OriginPublicId, Type, OriginEventKey)` aunque compartan `Close:{versión}` (T12, T14).

**Partes que no nacen de una corrida.** `LND_AccountingFactParts` no es sólo la parte de un hecho masivo: también es
el origen de los hechos por crédito que nace un proceso (C02) y de la **puesta al día de una operación** (C04). Cuando
una operación pone al día la causación de un crédito (T6, `interfaces.md` §3.3 paso 9), lo causado crea sus propias
partes, una por tipo, con la operación como tanda; cada una es origen de su `InteresCausado`, `MoraCausada` o
`SeguroCausado` con clave `Confirmation:{parte:N}` y el modo de los masivos, y las `LoanTransaction` de esa causación
llevan el `MessagePublicId` de su parte.

### 7.2 Unidades

La **unidad de emisión es una operación y su modo** (C01): los mensajes con el mismo `origin.publicId`, la misma
`originEventKey` y el mismo destino nacen en el mismo `SaveChanges`, tienen las mismas dependencias, se entregan
juntos y dan **un** comprobante (012 §9). Una unidad nunca mezcla modos. Cartera no conoce tipos de comprobante:
Contabilidad resuelve **uno por unidad** a partir del tipo del hecho principal (vive en `ACC_LendingVoucherMappings`).
No hay clave adicional por grupo de modo y comprobante (se descartó `Confirmation:{uuidv5}`).

- **Unidad principal de una operación** (`Confirmation`): el hecho principal y sus **acompañantes**, de esta lista
  fija por tipo de principal:

| Principal | Acompañantes (cuando corresponden) |
|---|---|
| `PagoAplicado` | `AnticipoRecibido`, `CobroDeInteresEnSuspenso`, `RecuperacionDeCastigada`, `SaldoAFavorReconocido` (abono sin preferencia, D24), `AjusteDeCausacionPorFechaValor` (fecha valor anterior a lo causado, C03) |
| `PagoReversado` | `AjusteDeCausacionPorFechaValor` (diferencias de la reconstrucción, T33) |
| `DesembolsoReversado` | `AjusteDeCausacionPorFechaValor` (crédito con otros tramos: recálculo desde la fecha valor del tramo, C49) |
| `CreditoAjustadoDesdeComercial` | `SaldoAFavorReconocido` (el ajuste supera lo adeudado, FR-054) |

  Los acompañantes **sellan el modo del principal** (mismo `Mode` y mismo disparador, hora y granularidad en la
  `ScheduleKey`, cada uno con su código corto): `Contabilidad.ModoDePaso` no se ofrece en el ámbito
  `LendingFactType` para un tipo cuando viaja como acompañante. `AjusteDeCausacionPorFechaValor` y
  `SaldoAFavorReconocido` tienen modo propio sólo cuando los emite su corrida (`NationalValueCorrection`).
- **Lo que va aparte** (`Confirmation:{tipoDeHecho:N}`): `CausacionSuspendida`, `CausacionReanudada`,
  `DimensionContableCambiada` y, con A3, el contingente que mueve una operación ajena. Cada uno es su unidad, con su
  modo sellado.
- **Masivos**: cada parte es una unidad; el resumido del día las agrupa por lote (`contabilidad.md`).
- **Por crédito de un proceso**: cada `(parte, Confirmation:{crédito:N})` es una unidad de un mensaje (C02).
- **Destinos que no son Contabilidad**: toda unidad tiene un solo mensaje. Por eso `AnulacionDeOrdenDeGiro` no usa
  `Confirmation`: compartiría unidad con la orden si las dos esperaran a la vez.

---

## 8. Orden y dependencias

- **El orden es el `Id bigint`** de `COR_IntegrationMessages`, como en la 012. Bajo el cerrojo del crédito (T34) las
  operaciones de un crédito se registran en orden (`LoanControl.Sequence`), así que sus hechos nacen con `Id`
  crecientes; el `Id` nunca sale por HTTP (Principio VI).
- **No hay cadena por crédito** (T12, CH5). Los hechos de un crédito no dependen unos de otros por ser del mismo
  crédito: un hecho rechazado en Contabilidad no congela la contabilidad posterior de ese crédito, y la conciliación
  explica la diferencia con los hechos rechazados o pendientes (T58). El orden que exigen FR-055 y FR-126 lo dan las
  tres aristas siguientes.
- **Aristas** (`COR_IntegrationMessageDependencies`; el emisor las agrega solas):

| Arista | Cuándo | Efecto |
|---|---|---|
| Su propio origen | Siempre: el mensaje apunta al último ya emitido con el mismo origen | Lo que va aparte espera al principal de su operación; `AnulacionDeOrdenDeGiro` espera a `OrdenDeGiroEmitida` (Tesorería nunca recibe una anulación antes que su orden) |
| `related` (el original) | Reverso → pago; desembolso reversado → desembolso; ajuste desde Comercial → desembolso en especie; parte del reverso de cierre → parte del cierre que revierte; orden nueva → orden anterior | Un reverso nunca se contabiliza antes que su original |
| `CadenaDeOrden` | Sólo `ObligacionCreada` y `ObligacionRechazada`, con la venta como raíz | Las respuestas de una venta llegan a Comercial en orden |

- **Elegible para un destino** = entrega `Pending` con `NextAttemptAt` vencido y ninguna dependencia con entrega a
  ese mismo destino en `Pending`, `InBatch` o `Rejected` (012 §9). `Processed`, `NotApplicable` y `ValidationFailed`
  no bloquean. Las aristas sólo bloquean **dentro del destino**: una `PagoAplicado` rechazada en Contabilidad no
  detiene un `PagoReversadoPorCartera` hacia Recaudo.
- **Partes**: no hay aristas entre partes, ni entre la causación y las operaciones del mismo crédito. En el cierre,
  la reclasificación de una partición se emite antes que su deterioro (T48) y el lote las procesa por `Id`.
- **Original que no pasó.** Los reversos heredan el modo de su original (C08): si el original quedó `NotApplicable`
  (modo `NoPasa`), su reverso también, y el envío posterior del original lleva toda su clausura de relacionados, el
  reverso incluido (012 §11). Así un reverso nunca pasa sin su original ni espera a uno que no pasa. Si aun así llegara
  un reverso cuyo original no está contabilizado, Contabilidad responde `Accounting.LendingFact.WaitingForOriginal`
  (`contabilidad.md`).
- **Lo que recibe Cartera.** En el destino `Lending` rige la arista de la 012: un `AjusteDeVentaACredito` espera a su
  venta (`related`); una venta `Rejected` detiene sus ajustes hasta reprocesarla; una venta en `ValidationFailed` no
  los detiene (FR-052). Los mensajes de Tesorería y Recaudo llevan `related` hacia un documento de Cartera (la orden,
  el archivo) cuyos mensajes van a otro destino, así que no esperan a nada en `Lending`: el consumidor comprueba el
  estado de la orden o del archivo y rechaza una transición que no admite, para revisión en la bandeja (§12.4–§12.6;
  para el giro, `interfaces.md` §4.3).
- La bandeja muestra «espera al mensaje X», calculado, no guardado.

---

## 9. Bloques comunes de Cartera

Records en `Contracts/Lending`, también en v1 (sus nombres los fijan las transversales, §2.11, salvo los marcados).
Ninguno usa enums de Inventario. Un cambio incompatible de un bloque es un bloque V2 y, con él, una V2 de cada
mensaje que lo usa. `UserRefV1` se reutiliza de `Contracts/Inventory` (no tiene enums).

**`LoanRefV1`** — `{ loanPublicId: Guid, loanNumber: string }`. El número es el documento cruce (FR-126).

**`LoanFactDimensionsV1`** — todas las dimensiones de la matriz que no dependen del concepto (FR-126, T52), incluidas
las que piden K6a y K6b, para no forzar una V2.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `creditLineCode` | string | sí | Código de la línea (`CodigoDeCatalogo`); dimensión `CreditLineCode` |
| `modalityCode` | string | sí | Modalidad del documento `CatalogoDeModalidades` (`CONSUMO`, `COMERCIAL`, `MICROCREDITO`…) |
| `subsegmentCode` | string | no | `ConLibranza`, `SinLibranza`, `PersonaNatural`, `PersonaJuridica`, `Empleados`, `Inmobiliario`, `Empresarial` |
| `riskCategory` | string | sí | `A`..`E`: la **oficial** del último cierre o del cierre base, nunca la diaria (T48) |
| `riskSubcategory` | `RatingSubcategory` | sí | `None`, `E1`, `E2`; la matriz la lleva a su letra |
| `isEmployee` | bool | sí | Si el deudor es empleado, según la persona maestra hoy (C8) |
| `inGracePeriod` | bool | sí | En período de gracia |
| `restructured` | bool | sí | Reestructurado (dimensión que agrega T52) |
| `writtenOff` | bool | sí | Castigado: el estado `WrittenOff` del crédito a la fecha del hecho (C22); la matriz lo usa para la causación y la recuperación en orden |
| `collectorClass` | `ClaseDeRecaudador` | no | Sólo en los hechos que mueven el puente `RecaudoPorAplicar` (pago, reverso, saldo a favor devuelto, recuperación) |
| `branchPublicId` | Guid | sí | Sucursal contable de la oficina del crédito |
| `impairmentModel` | `ImpairmentModel` | sí | `ArrearsAnnex1` o `ExpectedLossAnnex2` (K6a) del segmento |
| `riskSegment` | string | sí | Segmento SARC: `VIVIENDA`, `CONSUMO`, `MICROCREDITO`, `COMERCIAL_PN`, `COMERCIAL_PJ` |
| `valueUnit` | string | sí | `Pesos`, o `Uvr` en vivienda (K6b) |

**`ConceptAmountV1`** — un valor por concepto (y cuota).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `concept` | `LoanConcept` | sí | `Principal`, `CurrentInterest`, `DefaultInterest`, `CurrentInterestInSuspense`, `DefaultInterestInSuspense`, `Insurance`, `CollectionFee`, `OtherCharge`, `Advance`, `CreditBalance`. En `DeterioroCalculado` es el concepto deteriorado (§10.17) |
| `installmentNumber` | int | no | Cuota; nula en lo que no es de una cuota (saldo a favor, deterioro) |
| `amount` | decimal(18,2) | sí | Valor con su signo (§4) |
| `uvrAmount` | decimal(18,4) | no | Valor en UVR (K6b) |
| `onBehalfOfAssociate` | bool | sí | Si el importe (un seguro, un cargo) es por cuenta del asociado (C22); dimensión `OnBehalfOfAssociate` |
| `reasonCode` | string | no | Dimensión `ReasonCode` de la matriz cuando el rol la exige: la forma de desembolso, quién registró la cuenta por cobrar en especie, el motivo de castigo o condonación, el de un contingente (`ApprovedNotDisbursed`, `RevolvingLimit`, `CollateralReceived`). En `DeterioroCalculado` y `ReversoDeCierre` es el **movimiento del deterioro** (C22): `Increase`, `DecreaseSameYear`, `DecreasePriorYears` (según `Contabilidad.DisminucionDeDeterioro`, C2) o `Transfer`; el nivel es `Individual` en las líneas por crédito y el `kind` de `GeneralImpairmentLineV1` en el general y el adicional |

**`OriginalFactRefV1`** — `{ messageId: Guid, type: string, originPublicId: Guid, number: string }`: el hecho del
que éste depende (FR-126): su mensaje, su tipo y su origen (operación o parte) con su número o rótulo.

**`ExternalDocumentRefV1`** — `{ module: string, publicId: Guid, documentClass: string, number: string }`: un
documento de otro módulo, con la clase como **texto** y el módulo por su código de `ModuloContable` (`INV`, `NOM`,
`TES`; `REC` cuando exista).

**`LoanFactLineV1`** — una línea por crédito de un hecho masivo o por parte.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `loan` | `LoanRefV1` | sí | Crédito y número (cruce) |
| `debtorPersonPublicId` | Guid | sí | Tercero de la línea |
| `counterpartyPersonPublicId` | Guid | no | Otro tercero que la regla puede pedir: la aseguradora del seguro causado |
| `dimensions` | `LoanFactDimensionsV1` | sí | Las del crédito a la fecha del hecho (en la reclasificación, las nuevas) |
| `previousDimensions` | `LoanFactDimensionsV1` | no | `CarteraReclasificadaPorCalificacion` y el `ReversoDeCierre` que la revierte: las anteriores (C22) |
| `amounts` | [`ConceptAmountV1`] | sí | Valores por concepto y cuota |
| `valueDate` | DateOnly | no | El día valor de un ajuste de la corrida `NationalValueCorrection` |

**`LoanFactTotalV1`** (fijado aquí) — `{ dimensions: LoanFactDimensionsV1, amounts: [ConceptAmountV1], loanCount:
int }`: los totales de una parte por combinación de dimensiones (T14). `installmentNumber` va nulo en los totales.

**`LoanPlanSummaryV1`** — resumen del plan vigente tras el hecho.

| Campo | Tipo | Qué es |
|---|---|---|
| `scheduleVersion` | int | Versión del plan (`RepaymentScheduleVersion`) |
| `amortizationSystem` | `AmortizationSystem` | `LevelPayment`, `ConstantPrincipal`, `Bullet` |
| `periodicity` | string | Periodicidad de la línea (`Weekly`, `Biweekly`, `Monthly`…) |
| `installmentCount` | int | Cuotas |
| `installmentAmount` | decimal(18,2) | Cuota fija o la primera |
| `firstDueDate`, `finalDueDate` | DateOnly | Primer y último vencimiento |
| `annualEffectiveRate` | decimal(9,6) | Tasa pactada como fracción |
| `dayCountConvention` | `DayCountConvention` | `Thirty360`, `Actual365`, `Actual360` |
| `rulesVersion` | string | `RulesVersion` sellada (`LND-AAAA.n`, T29) |

**`CollectorRefV1`** (fijado aquí) — quién recaudó y con qué clave (`interfaces.md` §3.2, §3.7).

| Campo | Tipo | Qué es |
|---|---|---|
| `collectorCode` | string | Código del recaudador en el registro (`NOM`, `TST`…; `CAR` en la recogida, que no se registra) |
| `collectorClass` | `ClaseDeRecaudador` | `Receipts`, `Payroll`, `Contributions`, `OwnLoanPayoff`, `Test` |
| `idempotencyKey` | string | La clave con que el recaudador aplicó (`NOM:{run:N}:{obligación:N}`…) |
| `meansClass` | `ClaseDeMedioDeRecaudo` | La clase de medio, nunca el medio de la cooperativa (FR-082) |
| `payerPersonPublicId` | Guid | El pagador |
| `receipt` | `ReceiptRefV1` | El recibo del recaudador |

**`ReceiptRefV1`** (fijado aquí) — `{ number: string, date: DateOnly, officePublicId: Guid?, meansReference:
string?, documentKind: string? }`: la `ReferenciaDeRecibo` del contrato, tal como la dio el recaudador.

**`PayeeV1`** y **`PayoffDeductionV1`** — los fija `interfaces.md` §4.2 (beneficiario sellado de una orden de giro;
crédito recogido con su comprobante y valor). Este catálogo los reutiliza en `OrdenDeGiroEmitida` y en
`CreditoDesembolsado`.

---

## 10. Hechos a Contabilidad (`CAR → Accounting`)

Lo que Contabilidad hace con cada uno (roles, matriz, comprobante, resumido, rechazo) lo fija `contabilidad.md`; la
operación de la matriz es el propio `Type` (`OperacionesDeCartera`). Aquí: origen, clave, relación, tercero, quién lo
origina y contenido. En los ejemplos sólo va el `payload`; el sobre sigue §5.

### 10.0 Formas comunes

**Hecho por crédito.** Todo hecho que no es masivo ni informativo de operación lleva al menos:

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `loan` | `LoanRefV1` | sí | Crédito y número |
| `debtorPersonPublicId` | Guid | sí | Deudor (el tercero del sobre puede ser otro: el pagador) |
| `valueDate` | DateOnly | sí | Fecha valor (FR-012) |
| `registeredAt` | DateTimeOffset | sí | Fecha de registro: el instante R de la operación |
| `dimensions` | `LoanFactDimensionsV1` | sí | Dimensiones **vigentes** a la fecha contable (T55) |
| `amounts` | [`ConceptAmountV1`] | sí | Valores por concepto y cuota |
| `original` | `OriginalFactRefV1` | en los dependientes | El hecho del que depende (FR-126) |
| `rulesVersion` | string | sí | `RulesVersion` del crédito |

**Hecho masivo (parte).** `InteresCausado`, `MoraCausada`, `SeguroCausado`, el `AjusteDeCausacionPorFechaValor` de la
corrida `NationalValueCorrection` (C03), `CarteraReclasificadaPorCalificacion`, `DeterioroCalculado` y `ReversoDeCierre`
(T14). Una parte = fecha o cierre × sucursal contable × tipo × tanda; cabe en la tanda que la produce. Siempre lleva
totales **y** la lista por crédito. La puesta al día de una operación también produce partes (C04, §7.1): una por
tipo, con la operación como tanda.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `runKind` | `LendingProcessKind` | sí, salvo en la puesta al día | `DailyCycle`, `NationalValueCorrection`, `MonthlyClose`, `CloseReopen`; nulo en la puesta al día de una operación |
| `runPublicId` | Guid | sí, salvo en la puesta al día | La corrida (`LND_ProcessRuns`) |
| `catchUpOperation` | objeto | sólo en la puesta al día | `{ operationPublicId: Guid, documentClass: string, number: string }`: la operación que puso al día la causación (C04) |
| `factDate` | DateOnly | sí | El día causado (*d*) o el último día del mes cerrado |
| `batchNumber` | int | sí | La tanda que la produjo (`1` en la puesta al día) |
| `partNumber` | int | sí | Ordinal de la parte en la corrida |
| `closeVersion` | int | no | Versión del cierre, en cierre y reapertura |
| `totals` | [`LoanFactTotalV1`] | sí | Totales por combinación de dimensiones |
| `lines` | [`LoanFactLineV1`] | sí | Una línea por crédito (o por crédito y día valor en el ajuste) |
| `lineCount` | int | sí | Cantidad de líneas |
| `controlTotal` | decimal(18,2) | sí | Σ de `amounts` de las líneas, igual a Σ de los totales |

Una parte de *N* créditos pesa del orden de 600 bytes por línea: con tandas de 250 créditos (`Lending:Process:BatchSize`)
una parte no pasa de unos 150 KB.

### 10.1 `CreditoDesembolsado` v1

Origen `LoanDisbursement` (cada tramo). Clave `Confirmation`. `related` nulo, salvo el reemplazo de una venta
(`inKind.replacesLoan`). Tercero: el deudor. Lo origina la persona que desembolsa o, en especie, el consumo de la venta
con el cajero como usuario de origen. En el mismo guardado nacen la `OrdenDeGiroEmitida` (si no es en especie, también
cuando se pagará por archivo, C10) y, en especie, la `ObligacionCreada`; aparte, con A3, los contingentes que el tramo
mueve (§10.24).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts`: el capital del tramo con `reasonCode` = la forma, o en especie quién registró la cuenta por cobrar; los cargos financiados si la línea los financia |
| `tranche` | objeto | sí | `{ number: int, isLast: bool }` |
| `disbursementForm` | string | sí | `Transfer`, `Check`, `ThirdPartyPayment` (los de la orden, `interfaces.md` §4.2) o `InKind` (FR-045) |
| `payeePersonPublicId` | Guid | no | El beneficiario sellado en la orden (el deudor o un tercero, C22); nulo en especie |
| `grossAmount` | decimal(18,2) | sí | Valor del tramo |
| `payoffDeductions` | [`PayoffDeductionV1`] | sí | Créditos recogidos (FR-046): `{ loan, operationPublicId, amount }`; cada uno tiene su propio `PagoAplicado` (`OwnLoanPayoff`). Vacía si no hay |
| `netToDisburse` | decimal(18,2) | sí | `grossAmount − Σ payoffDeductions.amount`; `0` en especie |
| `disbursementOrderPublicId` | Guid | no | La orden de giro; nula en especie |
| `inKind` | objeto | no | Sólo en especie: `{ saleMessageId: Guid, invoice: ExternalDocumentRefV1, creditPaymentPublicId: Guid, accountsReceivableRecordedBy: "Contabilidad" \| "Cartera", replacesLoan: LoanRefV1? }` (FR-050, FR-056) |
| `plan` | `LoanPlanSummaryV1` | sí | El plan sellado (o regenerado en cada tramo, D12) |

```json
{
  "loan": { "loanPublicId": "7b8c9d0e-1f2a-4b3c-9d4e-5f6a7b8c9d0e", "loanNumber": "CR-0001990" },
  "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000002",
  "valueDate": "2026-12-17",
  "registeredAt": "2026-12-17T15:02:44.120Z",
  "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
  "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 10000000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "Transfer" } ],
  "original": null,
  "rulesVersion": "LND-2026.1",
  "tranche": { "number": 1, "isLast": true },
  "disbursementForm": "Transfer",
  "payeePersonPublicId": "a1b2c3d4-0000-4000-8000-000000000002",
  "grossAmount": 10000000.00,
  "payoffDeductions": [ { "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "operationPublicId": "7d8e9f0a-1b2c-4d3e-8f4a-5b6c7d8e9f61", "amount": 2350000.00 } ],
  "netToDisburse": 7650000.00,
  "disbursementOrderPublicId": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "inKind": null,
  "plan": { "scheduleVersion": 1, "amortizationSystem": "LevelPayment", "periodicity": "Monthly", "installmentCount": 36, "installmentAmount": 392184.00, "firstDueDate": "2027-01-30", "finalDueDate": "2029-12-30", "annualEffectiveRate": 0.245000, "dayCountConvention": "Thirty360", "rulesVersion": "LND-2026.1" }
}
```

### 10.2 `DesembolsoReversado` v1

Origen `DisbursementReversal`. Clave `Confirmation`. `related` = la operación del desembolso; espera a su
`CreditoDesembolsado`. Tercero: el deudor. Lo origina el consumo de `ResultadoDelGiro` (con el usuario de Tesorería
como usuario de origen) o la persona que anula el desembolso. **Hereda el modo del desembolso** (C08). El desembolso
se reversa **desde su origen**: capital del tramo y la causación de ese crédito (FR-048, T33); si el crédito tiene
otros tramos, sólo se espeja el capital del tramo y la diferencia de causación sale del recálculo desde su fecha valor
como acompañante `AjusteDeCausacionPorFechaValor` (C49). Los pagos de recogida (`payoffDeductions`) se conservan
mientras el crédito espera un giro nuevo; si el crédito se anula, la misma transacción emite un `PagoReversado` por
cada uno (clave `{clave CAR}:reverso`) y los recogidos vuelven a su estado (C11, `interfaces.md` §4.3).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `original` → `CreditoDesembolsado`; `amounts` positivos: capital del tramo y lo causado que se anula, con el mismo `reasonCode` del desembolso |
| `cause` | string | sí | `Rejected`, `CheckNotCashed` o `Voided` (resultado del giro); `Manual` si lo anula un usuario con `Lending.Disbursements.Void` |
| `treasuryResultMessageId` | Guid | no | El `ResultadoDelGiro` que lo provocó |
| `reason` | string | no | Motivo |
| `disbursementForm` · `payeePersonPublicId` | string · Guid? | sí · no | Los del desembolso reversado (C22) |
| `inKind` | objeto | no | El del desembolso en especie: factura y quién registró la cuenta por cobrar (C22) |
| `loanStatusAfter` | `LoanStatus` | sí | `PendingDisbursement` (espera un giro nuevo) o `Voided` (anulado con motivo, también después) |
| `payoffReversals` | [`OriginalFactRefV1`] | sí | Los `PagoReversado` de la recogida que nacieron en este mismo guardado porque el crédito se anuló (C11); vacía si queda `PendingDisbursement`. Si se anula después, esos `PagoReversado` nacen con la anulación, sin otro `DesembolsoReversado` |

```json
{
  "loan": { "loanPublicId": "7b8c9d0e-1f2a-4b3c-9d4e-5f6a7b8c9d0e", "loanNumber": "CR-0001990" },
  "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000002",
  "valueDate": "2026-12-17",
  "registeredAt": "2026-12-19T13:40:02.877Z",
  "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
  "amounts": [
    { "concept": "Principal", "installmentNumber": null, "amount": 10000000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "Transfer" },
    { "concept": "CurrentInterest", "installmentNumber": 1, "amount": 11932.40, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null }
  ],
  "original": { "messageId": "5c6d7e8f-9a0b-4c1d-8e2f-3a4b5c6d7e01", "type": "CreditoDesembolsado", "originPublicId": "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c01", "number": "DES-2026-000120" },
  "rulesVersion": "LND-2026.1",
  "cause": "Rejected",
  "treasuryResultMessageId": "6e7f8a9b-0c1d-4e2f-8a3b-4c5d6e7f8a02",
  "reason": "Cuenta destino inexistente",
  "disbursementForm": "Transfer",
  "payeePersonPublicId": "a1b2c3d4-0000-4000-8000-000000000002",
  "inKind": null,
  "loanStatusAfter": "PendingDisbursement",
  "payoffReversals": []
}
```

### 10.3 `CreditoAjustadoDesdeComercial` v1

Origen `CommercialAdjustment` (consumo de `AjusteDeVentaACredito`). Clave `Confirmation`. `related` = el desembolso
en especie; espera a su `CreditoDesembolsado`. Tercero: el deudor. Lo origina el consumo, con el usuario de Comercial
como usuario de origen. El `Replacement` no emite este hecho: crea un crédito nuevo (`CreditoDesembolsado` en especie
con `inKind.replacesLoan`) y su `ObligacionCreada` (FR-054).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` **con signo**: negativo baja el capital (y reversa el interés causado sobre lo devuelto), positivo lo sube (nota débito) |
| `adjustmentClass` | string | sí | `CreditNote`, `Return`, `DebitNote`, `Voiding`, `VoidingByDianRejection` (los de la 012 §8.2) |
| `adjustmentMessageId` | Guid | sí | El `AjusteDeVentaACredito` consumido |
| `adjustmentDocument` | `ExternalDocumentRefV1` | sí | La nota, la devolución o la anulación (`INV`) |
| `invoice` | `ExternalDocumentRefV1` | sí | La venta original |
| `accountsReceivableRecordedBy` | string | sí | El sello de la venta: `Contabilidad` o `Cartera` (FR-056) |
| `creditBalanceRecognized` | decimal(18,2) | sí | Lo que supera lo adeudado y pasa a saldo a favor; viaja en su acompañante `SaldoAFavorReconocido` (§7.2). `0` si no hay |
| `voidedFromOrigin` | bool | sí | `true` en las anulaciones: el crédito queda sin efecto desde su origen |
| `newPlan` | `LoanPlanSummaryV1` | no | El plan recalculado según la línea (FR-031) |
| `validationFailed` | bool | sí | Una nota débito evaluada en contra marca el crédito (FR-052, FR-054) |

```json
{
  "loan": { "loanPublicId": "9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a", "loanNumber": "CR-0002001" },
  "debtorPersonPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "valueDate": "2026-12-22",
  "registeredAt": "2026-12-22T16:11:09.031Z",
  "dimensions": { "creditLineCode": "CONSUMOALM", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
  "amounts": [
    { "concept": "Principal", "installmentNumber": null, "amount": -30000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "Cartera" },
    { "concept": "CurrentInterest", "installmentNumber": 1, "amount": -84.60, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null }
  ],
  "original": { "messageId": "5b6c7d8e-9f0a-4b1c-8d2e-3f4a5b6c7d10", "type": "CreditoDesembolsado", "originPublicId": "4d5e6f7a-8b9c-4d0e-9f1a-2b3c4d5e6f02", "number": "DES-2026-000131" },
  "rulesVersion": "LND-2026.1",
  "adjustmentClass": "Return",
  "adjustmentMessageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4f31",
  "adjustmentDocument": { "module": "INV", "publicId": "aa11bb22-cc33-4d44-8e55-ff6677889900", "documentClass": "CreditNote", "number": "NC01-0088" },
  "invoice": { "module": "INV", "publicId": "6d7e8f90-a1b2-4c3d-8e4f-5a6b7c8d9e0f", "documentClass": "PosEquivalentDocument", "number": "PV01-2207" },
  "accountsReceivableRecordedBy": "Cartera",
  "creditBalanceRecognized": 0.00,
  "voidedFromOrigin": false,
  "newPlan": { "scheduleVersion": 2, "amortizationSystem": "LevelPayment", "periodicity": "Monthly", "installmentCount": 3, "installmentAmount": 41604.00, "firstDueDate": "2027-01-17", "finalDueDate": "2027-03-17", "annualEffectiveRate": 0.268200, "dayCountConvention": "Thirty360", "rulesVersion": "LND-2026.1" },
  "validationFailed": false
}
```

### 10.4 `InteresCausado` v1 · `MoraCausada` v1 · `SeguroCausado` v1

Origen `AccountingFactPart` de la corrida `DailyCycle` (una parte por día × sucursal × tipo × tanda) o de la puesta al
día de una operación (una parte por tipo, con `catchUpOperation`, C04). Clave `Confirmation:{parte:N}`. `related`
nulo. Tercero del sobre nulo (cada línea trae el suyo). Lo origina el proceso, la persona que ordenó la corrida o la
de la operación que puso al día. Modo de los masivos. Forma masiva de §10.0, sin campos propios. Conceptos: `CurrentInterest` (o
`CurrentInterestInSuspense` si el crédito está suspendido) en `InteresCausado`; `DefaultInterest` (o
`DefaultInterestInSuspense`) en `MoraCausada`; `Insurance` con `onBehalfOfAssociate = true` y la aseguradora en
`counterpartyPersonPublicId` en `SeguroCausado` (D9). Causar *d* es `U(A(d)) − U(A(d−1))` (T25): una cuota puede tener
línea en cero.

```json
{
  "runKind": "DailyCycle",
  "runPublicId": "1f2e3d4c-5b6a-4978-8695-a4b3c2d1e0f9",
  "factDate": "2026-12-14",
  "batchNumber": 7,
  "partNumber": 19,
  "closeVersion": null,
  "catchUpOperation": null,
  "totals": [
    { "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
      "amounts": [ { "concept": "CurrentInterest", "installmentNumber": null, "amount": 5557.58, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "loanCount": 2 },
    { "dimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
      "amounts": [ { "concept": "CurrentInterest", "installmentNumber": null, "amount": 2388.05, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "loanCount": 1 }
  ],
  "lines": [
    { "loan": { "loanPublicId": "7b8c9d0e-1f2a-4b3c-9d4e-5f6a7b8c9d0e", "loanNumber": "CR-0001990" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000002", "counterpartyPersonPublicId": null,
      "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
      "previousDimensions": null, "amounts": [ { "concept": "CurrentInterest", "installmentNumber": 1, "amount": 3642.18, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "valueDate": null },
    { "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "counterpartyPersonPublicId": null,
      "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
      "previousDimensions": null, "amounts": [ { "concept": "CurrentInterest", "installmentNumber": 14, "amount": 1915.40, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "valueDate": null },
    { "loan": { "loanPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a", "loanNumber": "CR-0001234" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001", "counterpartyPersonPublicId": null,
      "dimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
      "previousDimensions": null, "amounts": [ { "concept": "CurrentInterest", "installmentNumber": 9, "amount": 2388.05, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "valueDate": null }
  ],
  "lineCount": 3,
  "controlTotal": 7945.63
}
```

`MoraCausada` y `SeguroCausado` llevan la misma forma. Una línea de `SeguroCausado`, por ejemplo:

```json
{ "loan": { "loanPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a", "loanNumber": "CR-0001234" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001", "counterpartyPersonPublicId": "e5f6a7b8-0000-4000-8000-0000000000a1", "dimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "previousDimensions": null, "amounts": [ { "concept": "Insurance", "installmentNumber": 9, "amount": 4500.00, "uvrAmount": null, "onBehalfOfAssociate": true, "reasonCode": null } ], "valueDate": null }
```

### 10.5 `AjusteDeCausacionPorFechaValor` v1

Dos orígenes con la misma forma masiva de §10.0 (C03):

- **acompañante de una operación** fechada atrás —un pago con fecha valor anterior a lo causado, un reverso de pago o
  el reverso de un tramo de un crédito con varios (T33, C49)—: origen la operación, clave `Confirmation`, en la
  unidad de su principal y con su modo (§7.2); `runKind`, `runPublicId` y `catchUpOperation` nulos, `batchNumber` y
  `partNumber` en `1`; una sola línea por crédito y día valor. La causa va en `causeFact`;
- **una parte de la corrida `NationalValueCorrection`** (T28): origen la parte, clave `Confirmation:{parte:N}`, modo
  propio del tipo (por lotes, `FinDeCausacion`). La corrida la **encola la réplica**, no el ciclo:
  `EfectoDeReplicacionCorreccionDeValorNacional` (un `IEfectoDeReplicacion` de Cartera) inserta en la misma
  transacción una corrida `Requested` cuando llega un valor nacional con `SupersedesPublicId` que cubre días ya
  causados, sólo si Cartera está activada (C05). Los códigos que la encolan son `USURA` y, desde la fase 24
  (2026-10-17), `UVR`: ésa sólo mira y recorre los créditos **en UVR**, y la diferencia de su corrección monetaria
  no viaja aquí sino en un `CorreccionMonetariaUvrCausada` de la misma parte de la corrida (§10.25).

Lo origina la persona de la operación o el proceso. Valores **con signo**: positivo causa más, negativo menos; incluye
la reclasificación a suspenso que resulte de reconstruir (`CurrentInterest` negativo y `CurrentInterestInSuspense`
positivo). Fecha contable: la del período abierto (T36); el día valor va en cada línea (`valueDate`).

| Campo propio | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `cause` | string | sí | `ValueDatedOperation` o `NationalValueCorrection` |
| `causeFact` | `OriginalFactRefV1` | no | El hecho principal de la operación que lo provocó (pago, reverso) |
| `correctedNationalValue` | objeto | no | En la corrección: `{ code, key, validFrom, sourcePublicId, sourceSequence }` del valor nacional corregido (`COR_NationalValues`) |

```json
{
  "runKind": "NationalValueCorrection",
  "runPublicId": "2a3b4c5d-6e7f-4a8b-9c0d-1e2f3a4b5c60",
  "factDate": "2026-12-20",
  "batchNumber": 1,
  "partNumber": 1,
  "closeVersion": null,
  "catchUpOperation": null,
  "cause": "NationalValueCorrection",
  "causeFact": null,
  "correctedNationalValue": { "code": "USURA", "key": "CONSUMO", "validFrom": "2026-12-01", "sourcePublicId": "0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d50", "sourceSequence": 2 },
  "totals": [ { "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "DefaultInterest", "installmentNumber": null, "amount": -312.44, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "loanCount": 1 } ],
  "lines": [ { "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "counterpartyPersonPublicId": null, "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "previousDimensions": null, "amounts": [ { "concept": "DefaultInterest", "installmentNumber": 13, "amount": -312.44, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "valueDate": "2026-12-05" } ],
  "lineCount": 1,
  "controlTotal": -312.44
}
```

### 10.6 `CausacionSuspendida` v1 · `CausacionReanudada` v1 (informativos)

Origen: la operación que cruza el umbral (clave `Confirmation:{tipoDeHecho:N}`) o la parte del ciclo diario (clave
`Confirmation:{crédito:N}`). `related` nulo. Tercero: el deudor. Lo origina la persona o el proceso. Sin
comprobante: Contabilidad deja su recibo. Lo causado antes no se reclasifica (T30).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `loan`, `debtorPersonPublicId`, `dimensions`, `registeredAt` | | sí | Como §10.0 |
| `effectiveDate` | DateOnly | sí | Desde cuándo se causa en suspenso, o se deja de hacerlo |
| `cause` | string | sí | Suspensión: `ArrearsThreshold`, `RepeatedRestructuring`, `Insolvency`, `WriteOff` y, desde K3, `Deceased` (el deudor falleció y `Causacion.ContinuaTrasFallecimiento = false` para su línea; FR-097, T102, bloque B2 del 2026-10-15). Reanudación: `ArrearsCured`, `InsolvencyEnded`, `CauseReversed` |
| `daysPastDue` | int | sí | Altura de mora a la fecha efectiva |
| `causeFact` | `OriginalFactRefV1` | no | La operación que lo provocó |

```json
{ "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "B", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "registeredAt": "2026-12-28T05:41:10.006Z", "effectiveDate": "2026-12-28", "cause": "ArrearsThreshold", "daysPastDue": 61, "causeFact": null }
```

`CausacionReanudada` lleva la misma forma (`"cause": "ArrearsCured"`, `"daysPastDue": 0`, y `causeFact` con el pago
que la puso al día).

### 10.7 `PagoAplicado` v1

Origen `PaymentApplication` (la `LoanOperation` de pago es el comprobante, T23). Clave `Confirmation`. `related` nulo.
Tercero: el pagador. Lo origina quien llama al contrato (persona); nunca el proceso (T11, C14). Es el principal de su
unidad: `AnticipoRecibido`, `CobroDeInteresEnSuspenso`, `RecuperacionDeCastigada`, `SaldoAFavorReconocido` y
`AjusteDeCausacionPorFechaValor` lo acompañan (§7.2). `amounts` lleva sólo lo aplicado a la deuda; el anticipo, el
suspenso cobrado, lo recuperado de un castigado, el saldo a favor y el ajuste de causación van en sus acompañantes.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `dimensions.collectorClass` = la clase del recaudador (puente `RecaudoPorAplicar`, T53) |
| `collector` | `CollectorRefV1` | sí | Recaudador, clave, clase de medio, pagador y recibo |
| `modality` | `PaymentModality` | sí | Una de las siete (FR-071) |
| `amountReceived` | decimal(18,2) | sí | Lo recibido |
| `surplus` | decimal(18,2) | sí | El excedente que vuelve al recaudador (D5); no se contabiliza en Cartera |
| `reallocations` | [objeto] | sí | Redistribución de pagos posteriores por una fecha valor anterior (FR-081, D4): `{ payment: OriginalFactRefV1, amounts: [ConceptAmountV1] }`, con signo, suma cero por pago. Vacía casi siempre |
| `prepaymentChoice` | `PrepaymentChoice` | no | Elección del abono extraordinario (o la preferencia vigente, D24) |
| `newPlan` | `LoanPlanSummaryV1` | no | El plan regenerado, si cambió |
| `paidOff` | bool | sí | Si canceló el crédito |
| `finalityUntil` | DateOnly | no | Firmeza de la cancelación (FR-082, D23) |
| `simulationId` | string | no | El de la forma interactiva (T32) |
| `deductionFileLinePublicId` | Guid | no | K2: la línea «descontado pendiente de giro» que cubre (`interfaces.md` §3.7); entonces el tercero es la pagaduría |
| `insuranceClaimPublicId` | Guid | no | La reclamación al seguro que el pago de la aseguradora cierra (T102) |

Ejemplo completo en §5.

### 10.8 `PagoReversado` v1

Origen `PaymentReversal`. Clave `Confirmation`. `related` = la operación del pago; espera a su `PagoAplicado`.
Tercero: el pagador del pago. Lo origina el recaudador por `ReversarAsync` o un usuario de Cartera por
`ReverseLoanPaymentCommand` (que nunca reversa pagos de `Payroll`, T31). **Hereda el modo del pago** (C08). Cada fila
propia del pago tiene su espejo; las diferencias de la reconstrucción van en su acompañante
`AjusteDeCausacionPorFechaValor` (C03) y en `reallocations` (T33). Los valores restituyen el saldo con las
**dimensiones vigentes**, no las del pago (T55).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `original` → `PagoAplicado`; `amounts` positivos: lo que el reverso restituye por concepto y cuota |
| `initiatedBy` | string | sí | `Collector` (por el contrato) o `Lending` (desde Cartera; entonces también nace `PagoReversadoPorCartera`) |
| `reason` | string | sí | Motivo (FR-080) |
| `collector` | `CollectorRefV1` | sí | El del pago reversado |
| `reversedAmount` | decimal(18,2) | sí | Lo aplicado que se reversa |
| `reallocations` | [objeto] | sí | Como en `PagoAplicado`: pagos posteriores redistribuidos (FR-081) |
| `reopened` | bool | sí | El pago reversado había cancelado el crédito |

```json
{
  "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" },
  "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003",
  "valueDate": "2026-12-10",
  "registeredAt": "2026-12-18T19:25:51.300Z",
  "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": "Test", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" },
  "amounts": [
    { "concept": "Principal", "installmentNumber": 13, "amount": 151200.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null },
    { "concept": "CurrentInterest", "installmentNumber": 13, "amount": 58800.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null }
  ],
  "original": { "messageId": "0e1f2a3b-4c5d-4e6f-8a7b-9c0d1e2f3a77", "type": "PagoAplicado", "originPublicId": "8f9a0b1c-2d3e-4f5a-8b6c-7d8e9f0a1b77", "number": "PAG-2026-004502" },
  "rulesVersion": "LND-2026.1",
  "initiatedBy": "Lending",
  "reason": "Cheque devuelto por fondos insuficientes",
  "collector": { "collectorCode": "TST", "collectorClass": "Test", "idempotencyKey": "TST:4b5c6d7e8f904a1b2c3d4e5f6a7b8c9d", "meansClass": "Check", "payerPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "receipt": { "number": "RC-TST-0042", "date": "2026-12-10", "officePublicId": null, "meansReference": "CH-000731", "documentKind": "Recibo" } },
  "reversedAmount": 210000.00,
  "reallocations": [],
  "reopened": false
}
```

### 10.9 `AnticipoRecibido` v1 (acompañante)

En la unidad de su `PagoAplicado`: mismo origen, clave, tercero y modo. Contenido: `loan`, `debtorPersonPublicId`,
`valueDate`, `registeredAt`, `dimensions`, `rulesVersion` y `amounts` con `concept = Advance` y la cuota futura que
cubrirá (D6).

```json
{ "loan": { "loanPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a", "loanNumber": "CR-0001234" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001", "valueDate": "2026-12-15", "registeredAt": "2026-12-16T14:20:31.204Z", "dimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": "Payroll", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Advance", "installmentNumber": 10, "amount": 50000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1" }
```

### 10.10 `AnticipoAplicado` v1

Origen: la parte del ciclo diario (tanda × sucursal × tipo). Clave `Confirmation:{crédito:N}`. Tercero: el deudor. Lo
origina el proceso, al vencer la cuota que el anticipo cubre (aplica parcial si no alcanza, D6).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `valueDate` = vencimiento de la cuota; `amounts` = lo que cubre por concepto |
| `installmentNumber` | int | sí | La cuota |
| `advanceSources` | [`OriginalFactRefV1`] | sí | Los pagos en que se recibió el anticipo |

```json
{ "loan": { "loanPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a", "loanNumber": "CR-0001234" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001", "valueDate": "2026-12-30", "registeredAt": "2026-12-30T05:36:12.415Z", "dimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": 10, "amount": 50000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1", "installmentNumber": 10, "advanceSources": [ { "messageId": "7e1f2a3b-4c5d-4e6f-8a9b-0c1d2e3f4a52", "type": "AnticipoRecibido", "originPublicId": "2c3d4e5f-6a7b-4c8d-9e0f-1a2b3c4d5e6f", "number": "PAG-2026-004811" } ] }
```

### 10.11 `CobroDeInteresEnSuspenso` v1 (acompañante)

En la unidad de su `PagoAplicado`. Contenido: el común por crédito con `amounts` de `CurrentInterestInSuspense` y
`DefaultInterestInSuspense` cobrados (positivos). Contabilidad lo lleva a ingreso y cancela la orden (DC-05).

```json
{ "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "valueDate": "2027-01-12", "registeredAt": "2027-01-12T17:02:00.000Z", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "C", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": "Receipts", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "CurrentInterestInSuspense", "installmentNumber": 14, "amount": 42180.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1" }
```

### 10.12 `SaldoAFavorReconocido` v1

Dos orígenes: **acompañante** del pago sin preferencia o del ajuste de Comercial que lo deja (origen la operación, clave
`Confirmation`, modo de su principal; §7.2, C01), o **hecho por crédito** de la corrida `NationalValueCorrection`
(origen la parte, clave `Confirmation:{crédito:N}`, modo propio; C02). Tercero: el deudor. Lo origina la persona o el
proceso. Es un pasivo de Cartera con el deudor (D5).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` = `CreditBalance` |
| `cause` | string | sí | El origen del saldo (C22): `NoPrepaymentPreference` (pago sin preferencia, D24), `CommercialAdjustment` (ajuste que excede lo adeudado, FR-054), `NationalValueCorrection` (corrección de usura de un interés ya pagado, T28) |
| `causeFact` | `OriginalFactRefV1` | no | El hecho principal de la operación que lo originó |

```json
{ "loan": { "loanPublicId": "9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a", "loanNumber": "CR-0002001" }, "debtorPersonPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "valueDate": "2027-01-05", "registeredAt": "2027-01-05T15:44:20.118Z", "dimensions": { "creditLineCode": "CONSUMOALM", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "CreditBalance", "installmentNumber": null, "amount": 18300.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1", "cause": "CommercialAdjustment", "causeFact": { "messageId": "4f5a6b7c-8d9e-4f0a-8b1c-2d3e4f5a6b19", "type": "CreditoAjustadoDesdeComercial", "originPublicId": "6a7b8c9d-0e1f-4a2b-8c3d-4e5f6a7b8c19", "number": "AJC-2026-000014" } }
```

### 10.13 `SaldoAFavorResuelto` v1

Origen `CreditBalanceResolution` (`ResolverSaldoAFavorAsync`). Clave `Confirmation`. `related` nulo. Tercero: el
deudor. Lo origina quien llama al contrato. Si se aplica a otra obligación o a capital, la aplicación es otra operación
con su propio `PagoAplicado` (`interfaces.md` §3.9).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` = `CreditBalance` o `Advance` que baja; `dimensions.collectorClass` en la devolución |
| `balanceKind` | string | sí | `CreditBalance` o `Advance` |
| `resolution` | string | sí | El destino de la resolución (C22): `RefundThroughCollector` (devuelto), `ApplyToOtherObligation` (aplicado a otra obligación), `ApplyAsExtraordinaryPrincipal` (aplicado a la misma) (`FormaDeResolucion`) |
| `collector` | `CollectorRefV1` | sí | Quien lo resolvió (en la devolución, el que entrega el dinero) |
| `targetPayment` | `OriginalFactRefV1` | no | El `PagoAplicado` de la aplicación |
| `targetLoan` | `LoanRefV1` | no | La obligación destino |

```json
{ "loan": { "loanPublicId": "9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a", "loanNumber": "CR-0002001" }, "debtorPersonPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "valueDate": "2027-01-09", "registeredAt": "2027-01-09T14:00:03.551Z", "dimensions": { "creditLineCode": "CONSUMOALM", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": "Test", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "CreditBalance", "installmentNumber": null, "amount": 18300.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1", "balanceKind": "CreditBalance", "resolution": "RefundThroughCollector", "collector": { "collectorCode": "TST", "collectorClass": "Test", "idempotencyKey": "TST:5d6e7f8a9b0c4d1e2f3a4b5c6d7e8f90", "meansClass": "Cash", "payerPersonPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "receipt": { "number": "EG-TST-0007", "date": "2027-01-09", "officePublicId": null, "meansReference": null, "documentKind": "Egreso" } }, "targetPayment": null, "targetLoan": null }
```

### 10.14 `RecuperacionDeCastigada` v1 (acompañante)

En la unidad de un `PagoAplicado` sobre un crédito `WrittenOff`. Contenido: el común por crédito con `amounts`
recuperados por concepto (positivos) y `dimensions.collectorClass`. Contabilidad baja la castigada en orden y reconoce
el ingreso por recuperación (DC-05).

```json
{ "loan": { "loanPublicId": "5f6a7b8c-9d0e-4f1a-8b2c-3d4e5f6a7b20", "loanNumber": "CR-0000877" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000020", "valueDate": "2027-03-02", "registeredAt": "2027-03-02T16:30:00.000Z", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "E", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": true, "collectorClass": "Receipts", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 400000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1" }
```

### 10.15 `DimensionContableCambiada` v1

Origen: la operación que cambia la dimensión (clave `Confirmation:{tipoDeHecho:N}`) o la parte del ciclo diario que la
detecta (clave `Confirmation:{crédito:N}`). Tercero: el deudor. Lo origina la persona o el proceso. Modo:
`ModoDePasoDeCartera` decide `EnLinea` si la emite una operación y `PorLotes` si la emite el proceso, salvo vigencia
propia del tipo (T56; `ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite`). Mueve los saldos vivos de una combinación a
otra: libranza ↔ sin libranza (FR-116), fin de la gracia, deudor que pasa a ser o deja de ser empleado (C8), cambio de
oficina, modalidad o sub-segmento.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `dimensions` = las nuevas; `amounts` = saldos que se trasladan, positivos |
| `previousDimensions` | `LoanFactDimensionsV1` | sí | Las anteriores |
| `changedDimensions` | [string] | sí | Nombres de los campos de `LoanFactDimensionsV1` que cambiaron |
| `effectiveDate` | DateOnly | sí | Desde cuándo rige |
| `causeFact` | `OriginalFactRefV1` | no | La operación que lo provocó |

```json
{ "loan": { "loanPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a", "loanNumber": "CR-0001234" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000001", "valueDate": "2027-02-01", "registeredAt": "2027-02-01T05:38:40.020Z", "dimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 4127400.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null }, { "concept": "CurrentInterest", "installmentNumber": null, "amount": 2388.05, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1", "previousDimensions": { "creditLineCode": "LIBEMP", "modalityCode": "CONSUMO", "subsegmentCode": "ConLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": true, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "changedDimensions": [ "subsegmentCode", "isEmployee" ], "effectiveDate": "2027-02-01", "causeFact": null }
```

### 10.16 `CobroProvisionalIncorporado` v1

Origen `ProvisionalCollection`: una `LoanOperation` por cobro de la plantilla `lending.provisional-collections`. La
activación valida y guarda los cobros (`LND_ProvisionalCollections`); el **consumidor de Comercial los aplica al nacer
la obligación** de su venta, en el mismo guardado y con clave idempotente (C62). Clave `Confirmation`. Tercero: el
deudor. Usuario de origen: el del mensaje de Comercial consumido (§6). Sin recaudador y en firme al aplicarse; la
matriz lo registra contra la cuenta por cobrar provisional, nunca contra caja (FR-142).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` = la distribución del cobro por concepto |
| `invoice` | `ExternalDocumentRefV1` | sí | La venta cobrada |
| `accountsReceivableRecordedBy` | string | sí | El sello de la venta (`Contabilidad` en el crédito provisional, C22) |
| `accountingReference` | string | sí | Referencia del registro contable o de Tesorería que redujo la provisional |
| `activationPublicId` | Guid | sí | La activación |
| `templateRow` | int | sí | Fila de la plantilla |

```json
{ "loan": { "loanPublicId": "1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d71", "loanNumber": "CR-0001788" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000031", "valueDate": "2026-11-20", "registeredAt": "2026-12-01T13:15:00.000Z", "dimensions": { "creditLineCode": "CONSUMOALM", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": 1, "amount": 60000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1", "invoice": { "module": "INV", "publicId": "77889900-aabb-4ccd-8dee-ff0011223344", "documentClass": "SalesInvoice", "number": "FE01-3301" }, "accountsReceivableRecordedBy": "Contabilidad", "accountingReference": "RC-2026-11-0457", "activationPublicId": "0c1d2e3f-4a5b-4c6d-8e7f-8a9b0c1d2e3f", "templateRow": 12 }
```

### 10.17 `CarteraReclasificadaPorCalificacion` v1 · `DeterioroCalculado` v1

Origen: la parte del cierre mensual (`MonthlyClose` × sucursal × tipo × tanda). Clave `Close:{versión}`. Tercero del
sobre nulo. Lo origina la persona que ordenó el cierre o el proceso (cierre programado). Nacen en la **transacción
final** del cierre, con su encabezado y `CierreDeCarteraRealizado` (T47). Forma masiva de §10.0 con:

| Campo propio | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `year`, `month` | int | sí | Mes cerrado |
| `informationCutoffAt` | DateTimeOffset | sí | R sellado del cierre |
| `generalImpairment` | [`GeneralImpairmentLineV1`] | no | Sólo `DeterioroCalculado`: deterioro general y adicional por dimensión, en delta (T48) |

- **Reclasificación** (primero): cada línea trae `previousDimensions` (categoría del cierre anterior o del cierre base)
  y `dimensions` (la nueva), con **todo** saldo que tenga categoría: capital, intereses, pagos por cuenta, deterioro
  individual de cada concepto y suspenso en orden (T48, C4). Sólo las líneas cuyo crédito cambió de categoría o
  subcategoría.
- **Deterioro** (después): delta en la categoría nueva; `concept` es el concepto deteriorado y `reasonCode` el
  **movimiento** (C22): `Increase` en un aumento, `Transfer` en un traslado entre conceptos o categorías, y una
  disminución partida en `DecreaseSameYear` y `DecreasePriorYears` según `Contabilidad.DisminucionDeDeterioro` (C2). El
  nivel es `Individual` en las líneas por crédito; el general y el adicional van en `generalImpairment`.

**`GeneralImpairmentLineV1`** (fijado aquí) — `{ kind: "General" | "Additional", branchPublicId: Guid, modalityCode:
string, subsegmentCode: string?, riskSegment: string, impairmentModel: ImpairmentModel, amount: decimal(18,2),
reasonCode: string }`: `kind` es el nivel y `reasonCode` el movimiento, con los valores de arriba.

```json
{
  "runKind": "MonthlyClose", "runPublicId": "3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e81", "catchUpOperation": null, "factDate": "2026-12-31", "batchNumber": 3, "partNumber": 7, "closeVersion": 1,
  "year": 2026, "month": 12, "informationCutoffAt": "2027-01-06T13:00:00.000Z",
  "totals": [ { "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "B", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 2930500.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null }, { "concept": "CurrentInterest", "installmentNumber": null, "amount": 61412.30, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "loanCount": 1 } ],
  "lines": [ { "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "counterpartyPersonPublicId": null, "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "B", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "previousDimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 2930500.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null }, { "concept": "CurrentInterest", "installmentNumber": null, "amount": 61412.30, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "valueDate": null } ],
  "lineCount": 1, "controlTotal": 2991912.30, "generalImpairment": null
}
```

Ése es un `CarteraReclasificadaPorCalificacion`. Un `DeterioroCalculado` de la misma partición lleva, por ejemplo, la
línea `{ "concept": "Principal", "amount": 117220.00, "reasonCode": "Increase" }` en categoría `B` y, en
`generalImpairment`, `{ "kind": "General", "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
"modalityCode": "CONSUMO", "subsegmentCode": null, "riskSegment": "CONSUMO", "impairmentModel": "ArrearsAnnex1",
"amount": 2105.40, "reasonCode": "Increase" }`.

### 10.18 `ReversoDeCierre` v1

Origen: la parte de la reapertura. Clave `Reopen:{versión}`. `related` = la parte del cierre que revierte (clase
`AccountingFactPart`); espera a ella. Lo origina la persona que reabre (`ReopenMonthlyCloseCommand`, corrida
`CloseReopen`, C58). Misma fecha contable que el cierre (último día del mes) y **el modo de la parte que revierte**
(C08). Forma masiva con valores positivos que deshacen la parte revertida, con las **dimensiones vigentes** del saldo
(T55): las líneas que revierten una reclasificación traen `previousDimensions`, y las que revierten un deterioro, su
movimiento en `reasonCode` (C22).

| Campo propio | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `reopeningPublicId` | Guid | sí | `LND_MonthlyCloseReopenings` |
| `reason` | string | sí | Motivo de la reapertura |
| `reversedPart` | `OriginalFactRefV1` | sí | El mensaje y la parte del cierre que revierte |
| `reversedType` | string | sí | `CarteraReclasificadaPorCalificacion` o `DeterioroCalculado` |
| `generalImpairment` **(nuevo, US9 bloque 12B)** | [`GeneralImpairmentLineV1`] | no | Sólo al revertir un `DeterioroCalculado` que traía general: el general y el adicional de la parte revertida, para que el reverso los deshaga también (sin esto la reapertura dejaba el general contabilizado). Opcional y aditivo: los consumidores que no lo conocen siguen leyendo el mensaje |

```json
{ "runKind": "CloseReopen", "runPublicId": "4d5e6f7a-8b9c-4d0e-8f1a-2b3c4d5e6f91", "catchUpOperation": null, "factDate": "2026-12-31", "batchNumber": 3, "partNumber": 7, "closeVersion": 1, "reopeningPublicId": "5e6f7a8b-9c0d-4e1f-8a2b-3c4d5e6f7a91", "reason": "Calificación por análisis registrada tarde", "reversedPart": { "messageId": "9a0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c81", "type": "DeterioroCalculado", "originPublicId": "6f7a8b9c-0d1e-4f2a-8b3c-4d5e6f7a8b81", "number": "CIE-202612-v1-0007" }, "reversedType": "DeterioroCalculado", "totals": [], "lines": [], "lineCount": 0, "controlTotal": 0.00 }
```

(Ejemplo recortado: `totals` y `lines` siguen la forma de §10.17.)

### 10.19 `GastoDeCobranzaCausado` v1 (K4)

Origen `LoanCharge`. Clave `Confirmation`. Tercero: el deudor. Lo origina la persona o el proceso de cobranza con
usuario de origen (no admite proceso). Sólo después del `Sent`/`Delivered` del mensaje `CollectionFeeNotice` (T77).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` = `CollectionFee` |
| `noticeDebtorMessagePublicId` | Guid | sí | El mensaje al deudor que informó el gasto (`LND_DebtorMessages`) |
| `stageCode` | string | sí | Etapa de cobranza |
| `payeePersonPublicId` | Guid | no | Casa de cobranza o abogado, si el gasto es una cuenta por pagar a un tercero |

```json
{ "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "valueDate": "2027-04-03", "registeredAt": "2027-04-03T15:00:00.000Z", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "C", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "CollectionFee", "installmentNumber": null, "amount": 35000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": null } ], "original": null, "rulesVersion": "LND-2026.1", "noticeDebtorMessagePublicId": "6a7b8c9d-0e1f-4a2b-8c3d-4e5f6a7b8ca1", "stageCode": "PREJURIDICA", "payeePersonPublicId": null }
```

### 10.20 `CreditoReestructurado` v1 · `CreditoModificado` v1 · `CreditoNovado` v1 (K3)

Origen `TermsChange` (la novedad aprobada). Clave `Confirmation`. Tercero: el deudor. Lo origina la persona que aplica
la novedad. Un cambio de dimensión que la novedad provoque va aparte en `DimensionContableCambiada`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `dimensions` = las nuevas; `amounts` = lo que la novedad mueve: intereses capitalizados (`reasonCode` = `Capitalizacion`; el concepto dice de dónde salen: `CurrentInterest`/`DefaultInterest` = lo causado, `CurrentInterestInSuspense`/`DefaultInterestInSuspense` = lo suspendido, ver la nota), cargos refinanciados |
| `previousDimensions` | `LoanFactDimensionsV1` | sí | La combinación anterior: con `dimensions`, las dos puntas del traslado de cada saldo (C22); en `CreditoNovado`, las del crédito anterior |
| `noveltyPublicId` | Guid | sí | `LND_LoanNovelties` |
| `sessionItemPublicId` | Guid | no | El punto del acta que la aprobó (T64) |
| `previousPlan` · `newPlan` | `LoanPlanSummaryV1` | sí | Plan anterior y nuevo |
| `restructuringCount` | int | sí | Veces reestructurado (dimensión `restructured`) |
| `newLoan` | `LoanRefV1` | sólo `CreditoNovado` | El crédito nuevo enlazado |

```json
{ "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "valueDate": "2027-05-10", "registeredAt": "2027-05-10T16:00:00.000Z", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "C", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": true, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "CurrentInterest", "installmentNumber": null, "amount": 96400.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "Capitalizacion" } ], "original": null, "rulesVersion": "LND-2027.1", "previousDimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "C", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "noveltyPublicId": "7b8c9d0e-1f2a-4b3c-8d4e-5f6a7b8c9db1", "sessionItemPublicId": "8c9d0e1f-2a3b-4c4d-8e5f-6a7b8c9d0eb1", "previousPlan": { "scheduleVersion": 1, "amortizationSystem": "LevelPayment", "periodicity": "Monthly", "installmentCount": 24, "installmentAmount": 210000.00, "firstDueDate": "2026-01-10", "finalDueDate": "2027-12-10", "annualEffectiveRate": 0.245000, "dayCountConvention": "Thirty360", "rulesVersion": "LND-2026.1" }, "newPlan": { "scheduleVersion": 2, "amortizationSystem": "LevelPayment", "periodicity": "Monthly", "installmentCount": 30, "installmentAmount": 121880.00, "firstDueDate": "2027-06-10", "finalDueDate": "2029-11-10", "annualEffectiveRate": 0.245000, "dayCountConvention": "Thirty360", "rulesVersion": "LND-2027.1" }, "restructuringCount": 1, "newLoan": null }
```

`CreditoModificado` lleva la misma forma sin capitalización obligatoria (`"amounts": []` cuando sólo cambia el plan) y
`CreditoNovado`, además, `"newLoan": { "loanPublicId": "…", "loanNumber": "CR-0003105" }`.

**Capitalización** (alineado con `contabilidad.md` §2.1 el 2026-10-13; antes esta tabla decía «rol `IngresoDiferido`» para
todo interés capitalizado). Cartera no elige roles: manda cada importe capitalizado con su concepto y
`reasonCode = Capitalizacion`, y Contabilidad lo traduce así:

- **lo causado** (`CurrentInterest`, `DefaultInterest`) sale de `InteresPorCobrar` y va a `Capital`: débito `Capital`,
  crédito `InteresPorCobrar`;
- **lo suspendido** (`CurrentInterestInSuspense`, `DefaultInterestInSuspense`) va a `Capital` contra `IngresoDiferido`
  (débito `Capital`, crédito `IngresoDiferido`) y, además, **se cancela su orden**: débito `ContraOrdenContingente`, crédito
  `SuspensoEnOrden` por el mismo importe, porque ese interés deja de estar en suspenso al volverse capital. Así lo
  construye `OperacionesDeCartera` para `CreditoReestructurado`, `CreditoModificado` y `CreditoNovado` (caso dorado
  `31-reestructuracion-con-capitalizacion`: 96.400 causados y 20.000 en suspenso). La cancelación de la orden es una
  **propuesta pendiente de validar por la contadora** (C7, como `IngresoDiferido`).

El ejemplo de arriba sólo capitaliza lo causado (`CurrentInterest` 96.400).

### 10.21 `CondonacionAprobada` v1 (K3)

Origen `Waiver`. Clave `Confirmation`. Tercero: el deudor. Lo origina la persona que aplica la condonación aprobada.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` = lo condonado por concepto, con `reasonCode` = motivo de la condonación |
| `waiverPublicId` | Guid | sí | `LND_Waivers` |
| `sessionItemPublicId` | Guid | no | El punto del acta, si la atribución lo exige |
| `impairmentApplied` | [`ConceptAmountV1`] | sí | Por concepto, cuánto de lo condonado sale contra el deterioro individual del crédito (C22); el resto de `amounts` va al gasto. Contabilidad no conoce el deterioro de cada crédito |
| `impairmentShortfall` | decimal(18,2) | sí | Total que va al gasto (Σ `amounts` − Σ `impairmentApplied`) |

```json
{ "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "valueDate": "2027-05-10", "registeredAt": "2027-05-10T15:30:00.000Z", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "C", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "DefaultInterest", "installmentNumber": 15, "amount": 22710.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "AcuerdoDePago" } ], "original": null, "rulesVersion": "LND-2026.1", "waiverPublicId": "9d0e1f2a-3b4c-4d5e-8f6a-7b8c9d0e1fc1", "sessionItemPublicId": null, "impairmentApplied": [ { "concept": "DefaultInterest", "installmentNumber": 15, "amount": 22710.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "AcuerdoDePago" } ], "impairmentShortfall": 0.00 }
```

### 10.22 `CreditoCastigado` v1 (K3)

Origen `WriteOff` (un ítem del lote de castigo). Clave `Confirmation`. Tercero: el deudor. Lo origina la persona que
aplica el lote. Aparte nace `CausacionSuspendida` (`cause = WriteOff`).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| (comunes por crédito) | | | `amounts` = saldos castigados por concepto, con `reasonCode` = causal |
| `writeOffBatchPublicId` | Guid | sí | `LND_WriteOffBatches` |
| `sessionItemPublicId` | Guid | sí | El punto del acta del consejo |
| `impairmentApplied` | [`ConceptAmountV1`] | sí | Por concepto, cuánto del saldo castigado sale contra el deterioro individual (C22) |
| `impairmentShortfall` | decimal(18,2) | sí | Lo que falta de deterioro y va al gasto (C7): Σ `amounts` − Σ `impairmentApplied` |
| `suspenseWrittenOff` | [`ConceptAmountV1`] | sí | El suspenso en orden que sale de `SuspensoEnOrden` al castigar y pasa a `CastigadaEnOrden` (C81); vacía si no hay |

```json
{ "loan": { "loanPublicId": "5f6a7b8c-9d0e-4f1a-8b2c-3d4e5f6a7b20", "loanNumber": "CR-0000877" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000020", "valueDate": "2027-06-30", "registeredAt": "2027-06-30T20:00:00.000Z", "dimensions": { "creditLineCode": "CONSUMO1", "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "riskCategory": "E", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "CONSUMO", "valueUnit": "Pesos" }, "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 1840000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "Irrecuperable" } ], "original": null, "rulesVersion": "LND-2026.1", "writeOffBatchPublicId": "0e1f2a3b-4c5d-4e6f-8a7b-8c9d0e1f2ad1", "sessionItemPublicId": "1f2a3b4c-5d6e-4f7a-8b8c-9d0e1f2a3bd1", "impairmentApplied": [ { "concept": "Principal", "installmentNumber": null, "amount": 1840000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "Irrecuperable" } ], "impairmentShortfall": 0.00, "suspenseWrittenOff": [] }
```

### 10.23 Informativos de la activación, la carga y el cierre

Contabilidad los registra **sin comprobante**, con su recibo en `ACC_LendingPostings`, y los entrega siempre
(`Always`), sin modo de paso ni validación previa. Con ellos `ClosePeriodCommand` aplica las comprobaciones de T57.
`origin.kind = Operation`; tercero nulo; sucursal principal.

| `Type` | Origen y número | Clave | Fecha (`operationDate`) | Contenido |
|---|---|---|---|---|
| `CarteraActivada` | `LND_Activations`; número = fecha de activación | `Confirmation` | Fecha de activación | `activationDate` (DateOnly), `cutoffDate` (DateOnly), `migrationLoadPublicId` (Guid), `baseClosePublicId` (Guid), `loanCount` (int), `principalTotal` (decimal), `activatedBy` (`UserRefV1`) |
| `CargaDeCarteraAplicada` | `LND_MigrationLoads`; número `CORTE-{aaaammdd}-{n}` | `Confirmation` | Fecha en que se aplicó | `migrationLoadPublicId`, `cutoffDate`, `replacesLoadPublicId` (Guid?), `loanCount`, `principalTotal`, `differencesCount` (int), `appliedBy` (`UserRefV1`) |
| `CierreDeCarteraRealizado` | `LND_MonthlyCloses` (la versión); número `aaaa-mm` | `Close:{versión}` | Último día del mes | `year`, `month`, `closeVersion`, `kind` (`MonthlyCloseKind`), `informationCutoffAt`, `marks` ([string]: `ObligatoryModelUnavailable`, `UnvalidatedParameters`, `IncompleteHistory`), `factParts` ([`{ type, parts, total }`]) |
| `CierreDeCarteraReabierto` | El `MonthlyClose` que se reabre; número `aaaa-mm` | `Reopen:{versión}` | Último día del mes | `year`, `month`, `reopenedCloseVersion`, `reopeningPublicId`, `reason`, `reopenedBy` (`UserRefV1`) |

```json
{ "activationDate": "2026-12-01", "cutoffDate": "2026-11-30", "migrationLoadPublicId": "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d01", "baseClosePublicId": "3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e02", "loanCount": 4185, "principalTotal": 18342750412.00, "activatedBy": { "centralUserId": "e1d2c3b4-a596-4877-8695-a4b3c2d1e0f1", "name": "Jefe de cartera de ensayo" } }
```

```json
{ "year": 2026, "month": 12, "closeVersion": 1, "kind": "Monthly", "informationCutoffAt": "2027-01-06T13:00:00.000Z", "marks": [], "factParts": [ { "type": "CarteraReclasificadaPorCalificacion", "parts": 21, "total": 412650300.40 }, { "type": "DeterioroCalculado", "parts": 21, "total": 9840211.75 } ] }
```

`CargaDeCarteraAplicada` y `CierreDeCarteraReabierto` llevan las formas de la tabla, p. ej.
`{ "year": 2026, "month": 12, "reopenedCloseVersion": 1, "reopeningPublicId": "5e6f7a8b-9c0d-4e1f-8a2b-3c4d5e6f7a91",
"reason": "Calificación por análisis registrada tarde", "reopenedBy": { "centralUserId":
"e1d2c3b4-a596-4877-8695-a4b3c2d1e0f1", "name": "Jefe de cartera de ensayo" } }`.

### 10.24 `CreditoAprobadoSinDesembolsar` v1 · `CupoRotativoOtorgado` v1 · `GarantiaConstituida` v1 (A3, C15)

Las cuentas contingentes del CUIF: 9115 (créditos aprobados no desembolsados), 913005 (cupos rotativos) y 9110
(garantías recibidas). Nacen en K1 con modo `NoPasa` y comprobante `CN`; la contadora decide cuándo pasan, y lo
retenido se envía después con su fecha contable original. Admiten proceso (el ciclo detecta el vencimiento de una
aprobación o de un cupo y la liberación de una garantía tras la firmeza). Si el dueño rechaza A3, salen del catálogo
sin tocar nada más.

| Tipo | Origen y clave | Sube (+) | Baja (−) | `reasonCode` |
|---|---|---|---|---|
| `CreditoAprobadoSinDesembolsar` | `CreditApplication` (`Confirmation`) al aprobar, desistir o anular; el tramo desembolsado (`Confirmation:{tipoDeHecho:N}`); una parte del ciclo (`Confirmation:{solicitud:N}`) al vencer la aprobación (FR-042) | se aprueba una solicitud | se desembolsa cada tramo, vence la aprobación, se desiste o se anula | `ApprovedNotDisbursed` |
| `CupoRotativoOtorgado` | `CreditLimit` (`Confirmation`) al otorgar, ampliar o reducir; una parte del ciclo (`Confirmation:{cupo:N}`) al vencer. Mide el cupo **otorgado**: usarlo en un desembolso o restituirlo con un pago no lo mueve (C92) | se otorga o amplía el cupo | se reduce o vence | `RevolvingLimit` |
| `GarantiaConstituida` | `Collateral` (`Confirmation`) al constituir, revaluar, extinguir o ejecutar; la cancelación que la libera (`Confirmation:{tipoDeHecho:N}`); una parte del ciclo (`Confirmation:{garantía:N}`) cuando la liberación espera la firmeza (D23) | se constituye o se revalúa al alza | se libera, se extingue, se ejecuta o se revalúa a la baja | `CollateralReceived` |

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `debtorPersonPublicId` | Guid | sí | El deudor: el solicitante, el titular del cupo o el deudor del crédito que la garantía respalda |
| `loan` | `LoanRefV1` | no | El crédito, cuando ya existe (cruce `PG`); nulo en una aprobación sin desembolso o en un cupo sin uso |
| `subject` | objeto | sí | `{ kind: "CreditApplication" \| "CreditLimit" \| "Collateral", publicId: Guid, number: string }`: la solicitud, el cupo o la garantía |
| `valueDate` · `registeredAt` | DateOnly · DateTimeOffset | sí | Fecha valor y de registro |
| `dimensions` | `LoanFactDimensionsV1` | no | Las del crédito cuando existe; si no, nulo y la matriz resuelve por `creditLineCode` |
| `creditLineCode` | string | sí | La línea de la solicitud, del cupo o del crédito |
| `movement` | string | sí | `Approved`, `Disbursed`, `Expired`, `Withdrawn`, `Voided` (aprobación); `Granted`, `Increased`, `Reduced`, `Expired` (cupo; C92); `Constituted`, `Revalued`, `Released`, `Extinguished`, `Executed` (garantía) |
| `amounts` | [`ConceptAmountV1`] | sí | Una línea con la **variación con signo** (C22): `concept` = `Principal`, `reasonCode` del tipo |
| `balanceAfter` | decimal(18,2) | sí | El saldo contingente tras el hecho (aprobado sin desembolsar, cupo disponible o valor de la garantía) |
| `causeFact` | `OriginalFactRefV1` | no | El hecho principal de la operación que lo movió (desembolso, pago, cancelación) |

```json
{ "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000002", "loan": null, "subject": { "kind": "CreditApplication", "publicId": "2e3f4a5b-6c7d-4e8f-9a0b-1c2d3e4f5a91", "number": "SOL-2026-000311" }, "valueDate": "2026-12-15", "registeredAt": "2026-12-15T16:10:00.000Z", "dimensions": null, "creditLineCode": "CONSUMO1", "movement": "Approved", "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 10000000.00, "uvrAmount": null, "onBehalfOfAssociate": false, "reasonCode": "ApprovedNotDisbursed" } ], "balanceAfter": 10000000.00, "causeFact": null }
```

### 10.25 `CorreccionMonetariaUvrCausada` v1 (K6b, E12)

**K6b (2026-10-17; FR-024, D18; T1049, T1056).** La corrección monetaria de cada día de los créditos **en UVR**: lo que cambió el
valor en pesos de lo que se debe en UVR porque cambió la UVR, sin lo causado ni lo movido ese día (las filas
`MonetaryCorrection` del libro, con `UvrAmount` en cero y `UvrValue` del día). Mismo origen, clave, actor y forma masiva
que `InteresCausado` (§10.4): una parte por día × sucursal × tanda de la corrida `DailyCycle`, o una por la puesta al
día de una operación (`catchUpOperation`, C04). Admite proceso. Código corto `CMU` (`LND_FactTypes`, `DeliveredIn = K6b`,
`LendingFactTypesSeeder`), comprobante `CA` (`lending-voucher-mappings.json`), modo de los masivos.

- **Conceptos**: los del saldo del crédito, con **signo** (positivo si la UVR subió: el activo crece; si el deudor tiene un
  anticipo o saldo a favor, ese pasivo también): `Principal` sin cuota; los demás (`CurrentInterest`, `DefaultInterest`,
  sus suspensos, `Insurance`, `OtherCharge`, `Advance`, `CreditBalance`) por cuota. `uvrAmount` va en `0`: la cantidad en
  UVR no cambia, sólo su valor en pesos.
- **Dimensiones**: las del crédito con `valueUnit = "Uvr"`.
- **Sin UVR publicada del día** el ciclo no causa el crédito en UVR (ni su corrección) desde ese día: error de grupo
  `NationalValues.Missing` y alerta `Cartera.ValorNacionalFaltante`; los créditos en pesos siguen.
- **Contabilidad** lo asienta por la matriz: saldo del concepto contra el rol `Ingreso` con el hecho fijado en la regla
  (`contabilidad.md` §2, fila CMU). La contadora elige la cuenta de la corrección monetaria (E12 sigue abierta, igual que si
  el certificado tributario la incluye: hoy no).

```json
{
  "runKind": "DailyCycle",
  "runPublicId": "1f2e3d4c-5b6a-4978-8695-a4b3c2d1e0f9",
  "factDate": "2026-12-14",
  "batchNumber": 1,
  "partNumber": 4,
  "closeVersion": null,
  "catchUpOperation": null,
  "totals": [
    { "dimensions": { "creditLineCode": "VIVUVR", "modalityCode": "VIVIENDA", "subsegmentCode": null, "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "VIVIENDA", "valueUnit": "Uvr" },
      "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 9876.54, "uvrAmount": 0.0000, "onBehalfOfAssociate": false, "reasonCode": null } ], "loanCount": 1 }
  ],
  "lines": [
    { "loan": { "loanPublicId": "6c7d8e9f-0a1b-4c2d-8e3f-4a5b6c7d8e9f", "loanNumber": "CR-0002107" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000031", "counterpartyPersonPublicId": null,
      "dimensions": { "creditLineCode": "VIVUVR", "modalityCode": "VIVIENDA", "subsegmentCode": null, "riskCategory": "A", "riskSubcategory": "None", "isEmployee": false, "inGracePeriod": false, "restructured": false, "writtenOff": false, "collectorClass": null, "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f", "impairmentModel": "ArrearsAnnex1", "riskSegment": "VIVIENDA", "valueUnit": "Uvr" },
      "previousDimensions": null, "amounts": [ { "concept": "Principal", "installmentNumber": null, "amount": 9876.54, "uvrAmount": 0.0000, "onBehalfOfAssociate": false, "reasonCode": null } ], "valueDate": null }
  ],
  "lineCount": 1,
  "controlTotal": 9876.54
}
```

---

## 11. Mensajes de Cartera a Comercial, Tesorería y Recaudo

Destinos `Inventory`, `Treasury` y `Receipts`; entrega siempre (`Always`), `Pending` desde que nacen. Cada uno espera
a que su destino esté **habilitado** en la cooperativa (§13): hasta entonces se acumula sin intentos ni alertas.

### 11.1 `ObligacionCreada` v1 (`CAR → Inventory`)

Origen: el recibo del mensaje de Comercial (`LND_InboundMessages`, clase `InboundMessage`; número = el del documento
de Comercial). Clave `Confirmation`. `related` nulo; `CadenaDeOrden` = la venta (`chainRootPublicId`). Tercero: el
comprador. Lo origina el consumo de la venta (usuario de origen: quien la confirmó en Comercial) o, para las ventas
conciliadas antes del corte, el consumo que sigue a la activación (FR-143). Nace en el mismo guardado que el crédito y
su `CreditoDesembolsado` en especie. Comercial guarda número y estado, **nunca saldos ni cuotas** (T18; enmienda de
FR-062 de la 012).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `saleMessage` | objeto | sí | `{ messageId: Guid, type: string, version: int }`: el mensaje de Comercial al que responde (`VentaACreditoRegistrada`, o el `AjusteDeVentaACredito` de un reemplazo) |
| `invoice` | `ExternalDocumentRefV1` | sí | La venta (FR-053: siempre la referencia a la factura y al mensaje) |
| `creditPaymentPublicId` | Guid | sí | El pago a crédito de la venta: una obligación por pago (012 §8.1) |
| `loan` | `LoanRefV1` | sí | El crédito y su número |
| `creditLineCode` | string | sí | La línea usada: la del mensaje, la sugerida o la por defecto del tipo de tercero (FR-050) |
| `amount` | decimal(18,2) | sí | Valor financiado (cero en `CancelledBeforeCutoff`) |
| `originDate` | DateOnly | sí | Fecha de la venta |
| `status` | string | sí | `Validated`, `ValidationFailed`, `Migrated` o `CancelledBeforeCutoff` (FR-053, FR-143) |
| `validation` | objeto | no | `{ code, reason }` cuando `status = ValidationFailed`: el motivo en español (FR-052) |
| `plan` | `LoanPlanSummaryV1` | no | Resumen del plan; nulo en `CancelledBeforeCutoff` |
| `numberSource` | string | sí | `Reservation` (el reservado, FR-057), `Assigned` (Cartera no reservó o la reserva no se encontró, D26) o `Migration` |
| `reservationPublicId` | Guid | no | La reserva usada (v2) |
| `replacesLoan` | `LoanRefV1` | no | En un reemplazo, el crédito anulado (FR-054) |

```json
{
  "saleMessage": { "messageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4f20", "type": "VentaACreditoRegistrada", "version": 2 },
  "invoice": { "module": "INV", "publicId": "6d7e8f90-a1b2-4c3d-8e4f-5a6b7c8d9e0f", "documentClass": "PosEquivalentDocument", "number": "PV01-2207" },
  "creditPaymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4b11",
  "loan": { "loanPublicId": "9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a", "loanNumber": "CR-0002001" },
  "creditLineCode": "CONSUMOALM",
  "amount": 150000.00,
  "originDate": "2026-12-17",
  "status": "Validated",
  "validation": null,
  "plan": { "scheduleVersion": 1, "amortizationSystem": "LevelPayment", "periodicity": "Monthly", "installmentCount": 3, "installmentAmount": 52005.00, "firstDueDate": "2027-01-17", "finalDueDate": "2027-03-17", "annualEffectiveRate": 0.268200, "dayCountConvention": "Thirty360", "rulesVersion": "LND-2026.1" },
  "numberSource": "Reservation",
  "reservationPublicId": "f1e2d3c4-b5a6-4978-8a9b-0c1d2e3f4a5b",
  "replacesLoan": null
}
```

**Consumo en Comercial** (`DestinoInventario`, el único consumidor de `Inventory`): escribe
`INV_LendingObligationResponses` con índice único `MessagePublicId` (su recibo), en el mismo `SaveChanges`; la venta y
sus consultas muestran el número y el estado (FR-057, D26). Responde `Processed` con el `PublicId` de la fila como
referencia; una colisión del índice es `AlreadyProcessed`. Acepta `ObligacionCreada` v1 y `ObligacionRechazada` v1.

### 11.2 `ObligacionRechazada` v1 (`CAR → Inventory`)

Origen: el recibo del mensaje de Comercial (de la venta o del ajuste). Clave `Confirmation:{evaluación:N}`: **una por
evaluación**, así un reproceso que vuelve a fallar emite otra con el motivo vigente. `CadenaDeOrden` = la venta.
Tercero: el comprador. Lo origina el consumo (usuario de origen: quien confirmó en Comercial).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `saleMessage` | objeto | sí | `{ messageId, type, version }` del mensaje rechazado |
| `invoice` | `ExternalDocumentRefV1` | sí | La venta |
| `creditPaymentPublicId` | Guid | sí | El pago a crédito |
| `evaluationPublicId` | Guid | sí | `LND_InboundMessageEvaluations.PublicId` |
| `motive` | string | sí | Tabla siguiente |
| `code` | string | sí | Código del error |
| `reason` | string | sí | Motivo en español, que dice qué hacer (FR-013) |
| `retryable` | bool | sí | Si queda para reproceso |

| `motive` | Cuándo | `code` | `retryable` | Entrega del mensaje de Comercial |
|---|---|---|---|---|
| `NoConvertible` | Persona inexistente, línea inválida sin alterna, periodicidad o plazo sin equivalencia, versión no aceptada (FR-053, FR-022), **cuando deja de ser reprocesable**: un reproceso falla de forma definitiva o una persona lo marca no reprocesable (C95). Mientras sea reprocesable no se emite: el recibo queda `Rejected` y la venta se informa `Pending` con su motivo (C40) | `Lending.Sale.PersonNotFound`, `Lending.Sale.CreditLineInvalid`, `Lending.Sale.TermsWithoutEquivalence`, `Integration.VersionNotAccepted` | `false` | `Rejected` (ya no se reprocesa) |
| `AceptadaSinMigrar` | Venta anterior al corte, no migrada con saldo, aceptada con acta en la activación (FR-143, D17) | `Lending.Sale.NotIncorporated` | `false` | `Processed` («no incorporada») |
| `AceptadaFueraDeCartera` | Venta de servicios a asociados confirmada antes de existir `CreditWithoutLending`, marcada así en la conciliación (D29, T83, G11) | `Lending.Sale.NotIncorporated` | `false` | `Processed` («no incorporada») |
| `SinObligacion` | Ajuste de una venta aceptada sin migrar o fuera de Cartera (T17, T18) | `Lending.Sale.NotIncorporated` | `false` | `Processed` («no incorporada») |

```json
{
  "saleMessage": { "messageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4f40", "type": "VentaACreditoRegistrada", "version": 2 },
  "invoice": { "module": "INV", "publicId": "7e8f90a1-b2c3-4d4e-8f5a-6b7c8d9e0f1a", "documentClass": "SalesInvoice", "number": "FE01-3390" },
  "creditPaymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4b40",
  "evaluationPublicId": "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c40",
  "motive": "NoConvertible",
  "code": "Lending.Sale.TermsWithoutEquivalence",
  "reason": "La periodicidad «Biweekly» de la venta no tiene equivalencia en la línea CONSUMOALM y el mensaje se marcó no reprocesable. Anule la venta a crédito o regístrela con otra forma de pago.",
  "retryable": false
}
```

### 11.3 `OrdenDeGiroEmitida` v1 (`CAR → Treasury`)

La define esta especificación y Tesorería la adopta (D27). **Sus campos los fija `interfaces.md` §4.2**
(`OrdenDeGiroEmitidaV1`, con `PayeeV1` y `PayoffDeductionV1`; C13); aquí se fijan su sobre, su clave y su orden.
Origen: la orden (`LND_DisbursementOrders`, clase `DisbursementOrder`). Clave `Confirmation`. `related` nulo, o la orden
anterior en un giro nuevo. Tercero: el **beneficiario**; `operationDate` = fecha contable del desembolso
(`interfaces.md` §4.1). Lo origina la persona que desembolsa; **nace en el guardado del desembolso**, también cuando la
transferencia saldrá en un archivo de desembolsos de Cartera: entonces lleva `paidByFile = true` y Tesorería no la paga
por otra vía (C10). La referencia al archivo queda en la orden de Cartera y el archivo no viaja en ningún mensaje; la
orden existe antes que su archivo y, si el archivo se anula, pasa a otro (T78, `adaptadores-y-archivos.md` §6.2).
`DisbursementFilePublicId` no existe en el contenido.

```json
{
  "orderPublicId": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "orderNumber": "OG-2026-000088",
  "loan": { "loanPublicId": "7b8c9d0e-1f2a-4b3c-9d4e-5f6a7b8c9d0e", "loanNumber": "CR-0001990" },
  "trancheNumber": 1,
  "form": "Transfer",
  "payee": { "personPublicId": "a1b2c3d4-0000-4000-8000-000000000002", "taxIdType": "CC", "taxId": "1000000002", "name": "Asociado de ensayo dos", "bankCode": "0001", "bankAccountNumber": "000123456789", "bankAccountType": "Savings" },
  "grossAmount": 10000000.00,
  "payoffDeductions": [ { "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "operationPublicId": "7d8e9f0a-1b2c-4d3e-8f4a-5b6c7d8e9f61", "amount": 2350000.00 } ],
  "netAmount": 7650000.00,
  "payDate": "2026-12-17",
  "concept": "CR-0001990",
  "paidByFile": true,
  "replacesOrderPublicId": null
}
```

### 11.4 `AnulacionDeOrdenDeGiro` v1 (`CAR → Treasury`)

Campos de `AnulacionDeOrdenDeGiroV1` (`interfaces.md` §4.2, C13). Origen: la misma orden. Clave
`Confirmation:{orden:N}` (C12): no comparte unidad con la `OrdenDeGiroEmitida` aunque las dos esperen a la vez
(§7.2). `related` = la orden como documento (`DocumentRefV1`), no su mensaje; espera a su `OrdenDeGiroEmitida` en
`Treasury` por la arista de su propio origen. Tercero: el beneficiario. Lo origina un usuario con
`Lending.DisbursementOrders.Void`, con motivo; quién y cuándo los dicen `originUser` y `emittedAt` del sobre. La orden
sigue `Issued`, con la anulación emitida, hasta que Tesorería responde `ResultadoDelGiro` `Voided` o, si ya giró,
`Disbursed`: entonces el desembolso no se reversa y se gestiona como un pago (FR-045, FR-048).

```json
{ "orderPublicId": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f", "orderNumber": "OG-2026-000088", "reason": "El deudor pidió el giro a otra cuenta" }
```

### 11.5 `PagoReversadoPorCartera` v1 (`CAR → destino del recaudador`)

Campos de `PagoReversadoPorCarteraV1` (`interfaces.md` §3.15, C13). Origen `PaymentReversal`. Clave `Confirmation`.
`related` = la operación del pago. Tercero: el pagador. Lo origina un usuario de Cartera con atribución que reversa un
pago (`ReverseLoanPaymentCommand`, FR-080), en el mismo guardado que `PagoReversado`. El emisor elige el destino con
`SolicitudDeEmision.Destino` según la clase del recaudador:

| `collectorClass` | Destino |
|---|---|
| `Receipts`, `Test` | `Receipts` (Recaudo, o `RecaudadorDePrueba` fuera de producción) |
| `Contributions` | Ninguno todavía: hasta que la feature de Aportes declare su destino (y se agregue al conjunto `Destinos` del tipo), un pago de Aportes no se reversa desde Cartera (`Lending.Reversal.OwnedByCollector`, C37) |
| `Payroll` | Nunca: un pago de Nómina sólo lo reversa Nómina (`Lending.Reversal.OwnedByCollector`, T31) |
| `OwnLoanPayoff` | Nunca: lo reversa la anulación del crédito que lo recogió, sin mensaje (C11) |

```json
{ "loan": { "loanPublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a6b", "loanNumber": "CR-0001511" }, "payment": { "messageId": "0e1f2a3b-4c5d-4e6f-8a7b-9c0d1e2f3a77", "type": "PagoAplicado", "originPublicId": "8f9a0b1c-2d3e-4f5a-8b6c-7d8e9f0a1b77", "number": "PAG-2026-004502" }, "reversalOperationPublicId": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e77", "reversalNumber": "REV-2026-000063", "collector": { "collectorCode": "TST", "collectorClass": "Test", "idempotencyKey": "TST:4b5c6d7e8f904a1b2c3d4e5f6a7b8c9d", "meansClass": "Check", "payerPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000003", "receipt": { "number": "RC-TST-0042", "date": "2026-12-10", "officePublicId": null, "meansReference": "CH-000731", "documentKind": "Recibo" } }, "reversedAmount": 210000.00, "valueDate": "2026-12-10", "reason": "Cheque devuelto por fondos insuficientes", "reversedBy": { "centralUserId": "e1d2c3b4-a596-4877-8695-a4b3c2d1e0f1", "name": "Jefe de cartera de ensayo" }, "surplusAlreadyReturned": 0.00, "reopened": false }
```

### 11.6 `DescuentosPorPagaduria` v1 (`CAR → Receipts`, K2)

Origen: la versión del archivo de descuentos (`LND_PayrollDeductionFiles`, clase `PayrollDeductionFile`). Clave
`Confirmation`. `related` = la versión que reemplaza, si se regeneró antes del envío. Tercero: la pagaduría. Lo origina
la persona que genera el archivo (`Lending.PayrollDeductionFiles.Generate`). **Sin bytes**: el archivo es un adjunto no
borrable (`LendingPayrollDeductionFile`) que viaja por su `PublicId` y su SHA-256, con sus totales, y además sus
**líneas estructuradas** (`deductionFileLinePublicId`, documento, obligación, valor; C19) para
que Recaudo, sin leer tablas `LND_`, cite al aplicar el giro la línea que cubre (`DeductionFileLinePublicId`,
`interfaces.md` §3.7). Recaudo confirma el envío con `EnvioDeDescuentosConfirmado` y devuelve el resultado con
`ResultadoDeDescuentos` (FR-113–FR-115).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `agreement` | objeto | sí | `{ paymasterPersonPublicId, agreementCode, agreementVersion }` |
| `period` | objeto | sí | `{ year, month, subPeriod: int? }` |
| `fileVersion` | int | sí | Versión del archivo del período |
| `file` | objeto | sí | `{ attachmentPublicId, fileName, sha256, sizeBytes, formatCode }` (formato de ida, `BankFileScope.PayrollDeductionRequest`) |
| `sendBy` | DateOnly | sí | Fecha de envío que fija el convenio |
| `lines` | [objeto] | sí | `{ deductionFileLinePublicId, loan: LoanRefV1, debtorPersonPublicId, payerEmployeeId, installmentNumber: int?, dueDate, noveltyKind: PayrollNoveltyKind, amount, pastDueAmount }` |
| `totals` | objeto | sí | `{ lineCount, amount, pastDueAmount, byNovelty: [{ noveltyKind, lines, amount }] }` |
| `supersedesFile` | objeto | no | `{ filePublicId, fileVersion, messageId }` de la versión reemplazada |
| `generatedBy` | `UserRefV1` | sí | Quien lo generó |

```json
{
  "agreement": { "paymasterPersonPublicId": "f0e1d2c3-0000-4000-8000-0000000000a9", "agreementCode": "PAGMUN01", "agreementVersion": 2 },
  "period": { "year": 2027, "month": 2, "subPeriod": null },
  "fileVersion": 1,
  "file": { "attachmentPublicId": "2d3e4f5a-6b7c-4d8e-9f0a-1b2c3d4e5f61", "fileName": "descuentos-PAGMUN01-202702-v1.txt", "sha256": "4f0c1d9e8b7a6f5e4d3c2b1a0f9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f10", "sizeBytes": 18240, "formatCode": "PAGMUN-IDA" },
  "sendBy": "2027-02-05",
  "lines": [
    { "deductionFileLinePublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a71", "loan": { "loanPublicId": "4f5a6b7c-8d9e-4f0a-8b1c-2d3e4f5a6b71", "loanNumber": "CR-0002410" }, "debtorPersonPublicId": "a1b2c3d4-0000-4000-8000-000000000071", "payerEmployeeId": "EMP-55821", "installmentNumber": 4, "dueDate": "2027-02-28", "noveltyKind": "Unchanged", "amount": 265400.00, "pastDueAmount": 0.00 }
  ],
  "totals": { "lineCount": 1, "amount": 265400.00, "pastDueAmount": 0.00, "byNovelty": [ { "noveltyKind": "Unchanged", "lines": 1, "amount": 265400.00 } ] },
  "supersedesFile": null,
  "generatedBy": { "centralUserId": "e1d2c3b4-a596-4877-8695-a4b3c2d1e0f1", "name": "Jefe de cartera de ensayo" }
}
```

Un archivo de 5.000 líneas pesa del orden de 1,5 MB de contenido; `PayloadJson` no tiene tope (012 §3).

---

## 12. Mensajes que recibe Cartera (destino `Lending`)

Todos los consume `DestinoCartera` (§15). Ninguno admite proceso: llegan con la persona que actuó en su módulo.

### 12.1 `VentaACreditoRegistrada` v1 (`INV → Lending`)

El contrato es el de la 012 §8.1 y no cambia. Lo que Cartera hace con cada campo:

| Campo | Uso en Cartera |
|---|---|
| sobre `personPublicId` | El deudor: debe existir vivo en `COR_People`; si no, el recibo queda `Rejected` y reprocesable (`Lending.Sale.PersonNotFound`), y `ObligacionRechazada` `NoConvertible` sólo cuando deja de serlo (C95) |
| sobre `origin` (`publicId`, `number`, `operationDate`) | La factura y la fecha de origen del crédito (FR-050). Con fecha ≤ corte de la carga activada, la venta sigue FR-143 (§15.3), nunca se procesa en vivo |
| `thirdPartyKind` | Línea por defecto del tipo de tercero; un `Customer` necesita cupo otorgado (FR-049) |
| `person` | Foto informativa; no se copia a Cartera (I-1) |
| `creditPayment` | `amount` = valor financiado; `paymentPublicId` con la factura es la identidad: ninguna factura y pago originan dos obligaciones (FR-055) |
| `terms` | Plazo, cuotas, periodicidad y vencimientos traducidos por `LND_CommercialTermEquivalences` (FR-022); sin equivalencia, `Lending.Sale.TermsWithoutEquivalence` |
| `creditLineCode` · `suggestedCreditLineCode` | La línea (FR-050); inválida y sin alterna, `Lending.Sale.CreditLineInvalid` |
| `pendingValidation` · `origin` | `Validated` sin pendiente: se crea sin reevaluar. Pendiente: se evalúa con lo vigente **a la fecha de la venta**, sin contarla, en orden de llegada: estado, cupo, concentración, usura, línea de origen comercial, listas restrictivas y autorización (FR-051) |
| `approval` | Dato: una aprobación de Comercial no sustituye una atribución de Cartera |
| `consultationEvidence` | Enlace con la consulta auditada de cupo (`EvidenceId`) |
| `accountsReceivableRecordedBy` | Se copia al desembolso en especie (`inKind`) y nunca cambia en la cadena (FR-056) |
| `documentTotal`, `amountDue`, `pointOfSaleCode`, `salesChannelCode` | Informativos |

### 12.2 `VentaACreditoRegistrada` v2 (`INV → Lending`, E-02)

Es la v1 más dos campos al final; su record `VentaACreditoRegistradaV2` va en `Contracts/Inventory` y su entrada en
`CatalogoDeMensajesV1` (`OriginModule = INV`, `Destinos = { Lending }`, `AdmiteProceso = false`). Comercial emite sólo
v2 desde E-02; Cartera acepta v1 y v2 mientras quede alguna entrega v1 (§17). `creditLineCode` ya existe en v1 y llega
lleno cuando Cartera respondió la consulta (T18, CH11).

| Campo nuevo | Tipo | Oblig. | Qué es | Uso en Cartera |
|---|---|---|---|---|
| `reservedLoanNumber` | string | no | El número reservado e impreso en la factura (FR-057) | Número del crédito. `Reserved` o `Expired` → `Used` (una vencida que llega la toma con la alerta informativa `Cartera.ReservaVencida`, T22, C21) |
| `reservationPublicId` | Guid | no | La reserva (`LND_LoanNumberReservations`) | Si no existe o no coincide con el número, **no se rechaza** (C20): Cartera asigna otro, lo anota en la evaluación y responde `numberSource = Assigned`; la obligación se crea siempre (FR-050). `Lending.Reservation.NotFound` queda para las rutas de reserva |

```json
{
  "thirdPartyKind": "Associate",
  "person": { "taxIdType": "CC", "taxId": "1000000001", "name": "Asociado de ensayo" },
  "creditPayment": { "paymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4b11", "lineNumber": 2, "paymentMeansCode": "CREDASOC", "paymentMeansClass": "AssociateCredit", "amount": 150000.00 },
  "documentTotal": 230000.00,
  "amountDue": 230000.00,
  "terms": { "termUnit": "Months", "term": 3, "installments": 3, "periodicity": "Monthly", "firstDueDate": "2027-01-17", "finalDueDate": "2027-03-17" },
  "creditLineCode": "CONSUMOALM",
  "suggestedCreditLineCode": "CONSUMOALM",
  "pendingValidation": false,
  "origin": "Validated",
  "approval": null,
  "consultationEvidence": "9b8a7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d",
  "accountsReceivableRecordedBy": "Cartera",
  "pointOfSaleCode": "PTO01",
  "salesChannelCode": "MOSTRADOR",
  "reservedLoanNumber": "CR-0002001",
  "reservationPublicId": "f1e2d3c4-b5a6-4978-8a9b-0c1d2e3f4a5b"
}
```

### 12.3 `AjusteDeVentaACredito` v1 (`INV → Lending`)

El contrato es el de la 012 §8.2. Espera a su venta (`related`). Cartera busca la obligación por `originalMessageId`
(el recibo de la venta) y, según `adjustmentClass` (FR-054):

| `adjustmentClass` | Efecto en Cartera | Hechos |
|---|---|---|
| `CreditNote`, `Return` | Baja el capital, reversa el interés causado sobre lo devuelto, recalcula el plan; el exceso es saldo a favor | `CreditoAjustadoDesdeComercial` (+ su acompañante `SaldoAFavorReconocido`) |
| `Voiding`, `VoidingByDianRejection` | Deja el crédito sin efecto desde su origen: reversa intereses, mora y cargos; lo pagado queda como saldo a favor | `CreditoAjustadoDesdeComercial` (`voidedFromOrigin`) (+ `SaldoAFavorReconocido`) |
| `DebitNote` | Aumenta el saldo, recalcula el plan y se evalúa como una venta a su fecha; si falla, marca el crédito | `CreditoAjustadoDesdeComercial`; entrega `ValidationFailed` si la evaluación es negativa |
| `Replacement` | Crea un crédito nuevo enlazado al anulado, evaluado como una venta | `CreditoDesembolsado` en especie (`inKind.replacesLoan`) + `ObligacionCreada` (`replacesLoan`) |

- Un ajuste con fecha ≤ corte de una venta **migrada** se da por procesado: ya está en el saldo neto (FR-143); entrega
  `Processed`, sin hechos.
- Un ajuste posterior al corte de una venta migrada o cancelada antes del corte sigue la tabla sobre su obligación
  (una nota crédito o anulación sobre una obligación en cero deja saldo a favor).
- Un ajuste de una venta **aceptada sin migrar** o **fuera de Cartera**: `ObligacionRechazada` `SinObligacion`, no
  reprocesable; recibo `NotIncorporated`.

### 12.4 `ResultadoDelGiro` v1 (`TES → Lending`)

Lo define esta especificación (D27) y Tesorería lo adopta; mientras no exista, lo emite `TesoreriaDePrueba` fuera de
producción (§18). **Sus campos los fija `interfaces.md` §4.2** (`ResultadoDelGiroV1`: orden, `orderMessagePublicId`
—la `OrdenDeGiroEmitida` a la que responde—, resultado, fecha, valor, referencia de Tesorería, cheque y motivo; C13).
Sobre: `originModule = TES`; origen = el documento de Tesorería que registra el resultado; clave
`Confirmation:{orden:N}` (un documento de Tesorería puede informar varias órdenes); `related` = la orden (clase
`DisbursementOrder`); tercero = el beneficiario; usuario de origen = la persona de Tesorería (no admite proceso). Una
orden puede recibir más de un resultado (FR-045).

Efecto: `ConsumeTreasuryResultCommand`, todo en un `SaveChanges` con el recibo y su evaluación, según la tabla de
estados de `interfaces.md` §4.3, que fija también la respuesta de la entrega (`Processed`, o `Rejected` con
`Lending.DisbursementOrder.InvalidTransition`, `Lending.DisbursementOrder.AmountMismatch` o `Generic.NotFound`) y qué
pasa con los créditos recogidos al anular (C11). Un `Rejected`, `Voided` o `CheckNotCashed` aceptado reversa el
desembolso desde su origen con `DesembolsoReversado` (§10.2). Repetir el mismo mensaje no cambia nada (recibo único,
SC-010). Sin resultado en `Giros.DiasSinResultado`, la tarea `cartera.giros-sin-resultado` alerta
`Cartera.GiroSinResultado`.

```json
{ "orderPublicId": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f", "orderMessagePublicId": "6e7f8a9b-0c1d-4e2f-8a3b-4c5d6e7f8a01", "outcome": "Disbursed", "occurredOn": "2026-12-18", "amount": 7650000.00, "treasuryReference": "TRF-88412", "checkNumber": null, "reason": null }
```

### 12.5 `EnvioDeDescuentosConfirmado` v1 (`REC → Lending`, K2)

Lo define esta especificación (D15) y Recaudo lo adopta; mientras no exista, lo emite `RecaudadorDePrueba` fuera de
producción. Sobre: `originModule = REC`; origen = el documento de Recaudo del envío; clave
`Confirmation:{archivo:N}`; `related` = la versión del archivo (clase `PayrollDeductionFile`); tercero = la pagaduría.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `fileMessageId` | Guid | sí | El `DescuentosPorPagaduria` |
| `filePublicId` · `fileVersion` · `fileSha256` | Guid · int · string | sí | La versión enviada y su huella |
| `sentAt` | DateTimeOffset | sí | Cuándo se envió a la pagaduría |
| `channel` | string | sí | `Email`, `Portal`, `Physical` u `Other` |
| `reference` | string | no | Radicado o constancia |
| `sentBy` | `UserRefV1` | sí | Quien lo envió |

Efecto: la versión pasa a `Sent` (FR-113); después, regenerarla responde `Lending.PayrollFile.AlreadySent`. Ya
`Sent`: `Processed` sin efecto. Una versión `Superseded` o `Voided`: `Rejected` con `Lending.PayrollFile.Superseded`.
Huella distinta: `Rejected` con `Lending.PayrollFile.HashMismatch`. Archivo inexistente: `Generic.NotFound`.

```json
{ "fileMessageId": "0f1a2b3c-4d5e-4f6a-8b7c-8d9e0f1a2b61", "filePublicId": "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c61", "fileVersion": 1, "fileSha256": "4f0c1d9e8b7a6f5e4d3c2b1a0f9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f10", "sentAt": "2027-02-04T15:12:00.000Z", "channel": "Portal", "reference": "RAD-2027-00311", "sentBy": { "centralUserId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d61", "name": "Analista de recaudo de ensayo" } }
```

### 12.6 `ResultadoDeDescuentos` v1 (`REC → Lending`, K2)

Recaudo procesa la respuesta de la pagaduría con el formato de vuelta del convenio (`FlatFileReader`) y devuelve el
resultado (FR-115). Sobre como §12.5.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `fileMessageId` · `filePublicId` · `fileVersion` · `fileSha256` | | sí | El archivo de ida al que responde |
| `paymasterPersonPublicId` | Guid | sí | La pagaduría |
| `period` | objeto | sí | `{ year, month, subPeriod: int? }` |
| `responseFile` | objeto | no | `{ attachmentPublicId, sha256 }`: el archivo de la pagaduría tal como lo recibió Recaudo |
| `lines` | [objeto] | sí | `{ deductionFileLinePublicId, loan: LoanRefV1, installmentNumber: int?, expectedAmount, deductedAmount, deductionDate: DateOnly?, outcome: "Deducted" \| "PartiallyDeducted" \| "NotApplied", paymasterResultCode: string?, reason: MotivoDeNoDescuento?, detail: string? }` |
| `totals` | objeto | sí | `{ lineCount, expected, deducted, notApplied }` |
| `processedBy` | `UserRefV1` | sí | Quien procesó la respuesta |

Efecto (`ConsumePayrollDeductionResultCommand`, todo o nada):

- `Deducted` y la parte descontada de `PartiallyDeducted`: la línea queda **«descontado pendiente de giro»** con la
  fecha del descuento; mientras la mora de la pagaduría no supere su umbral no se imputan al deudor días de mora,
  cambio de calificación, gastos ni reporte negativo (FR-114). Recaudo aplica el giro después por el contrato, citando
  `deductionFileLinePublicId`; no hay hecho contable hasta ese pago.
- `NotApplied` y el resto de una parcial: `LND_InstallmentNonDeductions` con origen `PaymasterResult` y el motivo
  traducido por `LND_PayrollAgreementResultCodes`; alerta `Cartera.NoAplicadoDePagaduria`, aviso al deudor por sus
  canales autorizados y la regla del convenio (caja o cobranza) (FR-115).
- Si la versión seguía `Generated` (la confirmación aún no se consumió), pasa también a `Sent`: el resultado prueba
  el envío.
- Una línea que no es del archivo citado, o un `expectedAmount` distinto del enviado: `Rejected` con
  `Lending.PayrollFile.LineMismatch` y la lista de líneas en `data`; Recaudo corrige y emite un resultado nuevo.

```json
{ "fileMessageId": "0f1a2b3c-4d5e-4f6a-8b7c-8d9e0f1a2b61", "filePublicId": "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c61", "fileVersion": 1, "fileSha256": "4f0c1d9e8b7a6f5e4d3c2b1a0f9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f10", "paymasterPersonPublicId": "f0e1d2c3-0000-4000-8000-0000000000a9", "period": { "year": 2027, "month": 2, "subPeriod": null }, "responseFile": { "attachmentPublicId": "1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d61", "sha256": "a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a3b2c1d0e9f8a7b6" }, "lines": [ { "deductionFileLinePublicId": "3e4f5a6b-7c8d-4e9f-8a0b-1c2d3e4f5a71", "loan": { "loanPublicId": "4f5a6b7c-8d9e-4f0a-8b1c-2d3e4f5a6b71", "loanNumber": "CR-0002410" }, "installmentNumber": 4, "expectedAmount": 265400.00, "deductedAmount": 0.00, "deductionDate": null, "outcome": "NotApplied", "paymasterResultCode": "07", "reason": "Leave", "detail": "Licencia no remunerada" } ], "totals": { "lineCount": 1, "expected": 265400.00, "deducted": 0.00, "notApplied": 265400.00 }, "processedBy": { "centralUserId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d61", "name": "Analista de recaudo de ensayo" } }
```

---

## 13. Destinos y registro de participantes

### 13.1 Destinos y módulos de origen

`IntegrationDestinations` es texto (≤ 20), no enum: un destino nuevo es un consumidor nuevo (012). Cada destino tiene
**un solo** `IDestinoDeMensajes`; un segundo hace fallar el arranque con `Integration.Destination.DuplicateConsumer`
(`UnSoloConsumidorPorDestino`; antes el despachador tomaba el primero sin avisar).

| Destino | Consumidor | Recibe | Fila que lo habilita (§13.3) |
|---|---|---|---|
| `Accounting` | `DestinoContabilidad`, que reparte por `OriginModule`: `INV` al consumo de Inventario; `CAR` a `PostLendingFactsCommand` / `PostLendingSummaryGroupCommand` (`contabilidad.md`) | hechos INV y CAR | `CNT`, sembrada activa |
| `Lending` | `DestinoCartera` (§15) | ventas y ajustes (INV), resultados del giro (TES), envío y resultado de descuentos (REC, K2) | `CAR`, activa desde la activación de Cartera |
| `Inventory` | `DestinoInventario` (Comercial; CH9) | `ObligacionCreada`, `ObligacionRechazada` | `INV`, sembrada activa |
| `Treasury` | El de la feature de Tesorería; en prueba, `TesoreriaDePrueba` | `OrdenDeGiroEmitida`, `AnulacionDeOrdenDeGiro` | la que registre Tesorería; `TST` fuera de producción |
| `Receipts` | El de la feature de Recaudo; en prueba, `RecaudadorDePrueba` | `PagoReversadoPorCartera`, `DescuentosPorPagaduria` (K2) | la que registre Recaudo; `TST` fuera de producción |

| `OriginModule` | Módulo | Emite |
|---|---|---|
| `INV` | Comercial | Los 20 de la 012 y `VentaACreditoRegistrada` v2 |
| `CAR` | Cartera (`ModuloContable.Cartera`) | Los 41 de §2.1 y §2.2 (38 sin los tres contingentes de A3) |
| `TES` | Tesorería (`ModuloContable.Tesoreria`) | `ResultadoDelGiro` |
| `REC` | Recaudo (CH10) | `EnvioDeDescuentosConfirmado`, `ResultadoDeDescuentos` |

### 13.2 Registro de participantes por cooperativa

En la base de cada cooperativa (Principio IV), migración `PlataformaParaCartera`:

| Tabla | Entidad | Columnas |
|---|---|---|
| `COR_IntegrationParticipants` | `IntegrationParticipant` | `Code`, `Class` (`IntegrationParticipantClass`), `Role` (`IntegrationParticipantRole`), `Destination` (el valor de `IntegrationDestinations`, sólo con `Role = MessageDestination`), `IsEnabled`, `IsTestDouble`, `ValidFrom`, `ValidTo`, motivo, quién registró. Único `(Code, Role, Destination)` (C36) |
| `COR_IntegrationParticipantAdoptions` | `IntegrationParticipantAdoption` | El participante, el contrato o tipo de mensaje adoptado (`ProveedorDeObligaciones`, `OrdenDeGiroEmitida`…), su versión y su vigencia |

`IntegrationParticipantClass { Receipts = 1, Payroll = 2, Contributions = 3, Treasury = 4, Test = 5, Accounting = 6,
Lending = 7, Inventory = 8 }`; `IntegrationParticipantRole { ObligationsCollector = 1, MessageDestination = 2 }`
(`Domain/Enums/Integration`). Lo lee `IRegistroDeIntegracion` (`interfaces.md` §11.3) y lo escribe
`EscritorDelRegistroDeIntegracion`, que **agrega sin guardar**: la activación de Cartera y la habilitación de su
destino van en el mismo guardado (T16). Administración: `RegisterIntegrationParticipantCommand` (su cuerpo lleva
`destination`, obligatorio con `role = MessageDestination`; C36), `RetireIntegrationParticipantCommand`,
`ListIntegrationParticipantsQuery`; rutas `/api/core/integration-participants`
(+ `{id}/retire`) con `Core.IntegrationParticipants.View/Manage`; pantalla `/admin/participantes-de-integracion`
(`api.md`).

**Semilla** (idempotente, `INSERT … WHERE NOT EXISTS` en los dos motores; CH16):

| `Code` | `Class` | `Role` | `Destination` | Vigencia | Adopciones |
|---|---|---|---|---|---|
| `CNT` | `Accounting` | `MessageDestination` | `Accounting` | desde la migración | ninguna: Contabilidad declara sus versiones en código, por módulo |
| `INV` | `Inventory` | `MessageDestination` | `Inventory` | desde la migración (E-02) | `ObligacionCreada` v1, `ObligacionRechazada` v1 |
| `NOM` | `Payroll` | `ObligationsCollector` | — | desde la migración (E-03) | `ProveedorDeObligaciones` v1 |
| `CAR` | `Lending` | `MessageDestination` | `Lending` | **sembrada inactiva** (`IsEnabled = false`, sin vigencia) hasta que `ActivateLendingCommand` la habilita y abre su vigencia con la fecha de activación (C36) | `VentaACreditoRegistrada` v1 y v2, `AjusteDeVentaACredito` v1, `ResultadoDelGiro` v1; desde K2 `EnvioDeDescuentosConfirmado` v1, `ResultadoDeDescuentos` v1 |

**Lo que registra cada feature**: Recaudo, su fila `Receipts × ObligationsCollector` (adopta `ProveedorDeObligaciones`)
y `Receipts × MessageDestination` (adopta `PagoReversadoPorCartera` v1 y, desde K2, `DescuentosPorPagaduria` v1);
Tesorería, `Treasury × MessageDestination` (adopta `OrdenDeGiroEmitida` v1 y `AnulacionDeOrdenDeGiro` v1); Aportes,
`Contributions × ObligationsCollector`. La recogida de créditos propios (`OwnLoanPayoff`, código `CAR` como
recaudador) **no** se registra: es la propia Cartera (T91). Los dobles, filas `TST` con `Class = Test` e
`IsTestDouble = true` (§18).

Las filas `CNT`, `INV` y `NOM` se siembran con `IsEnabled = true`. Una fila está **activa** a hoy si `IsEnabled` y su
vigencia cubre hoy. Un participante **cuenta** si tiene fila activa a hoy, su implementación está en este despliegue y acepta la versión
que adoptó, y no es un doble en un despliegue de producción (T16). Una adopción de una versión que el despliegue no
implementa: `Integration.Participant.VersionNotSupported`. Un doble en producción: el escritor lo rechaza con
`Integration.Participant.TestDoubleInProduction`.

### 13.3 Habilitación de un destino

`DespachadorDeMensajes.DestinosHabilitadosAsync` **deja de leer** `ParametrosDeInventario.CarteraIntegracionHabilitadaDesde`
(la plataforma deja de depender de un parámetro de Comercial). En cada cooperativa, un destino se recorre si:

1. hay un `IDestinoDeMensajes` registrado con ese `Destino`;
2. `IRegistroDeIntegracion` le da una fila activa a hoy con `Role = MessageDestination` y ese `Destination`;
3. si la fila es de un doble, el despliegue no es de producción.

Si no, sus entregas se saltan **sin intentos ni alertas** y la bandeja las muestra «Pendiente — destino no habilitado
en esta cooperativa». Así, antes de activar Cartera los mensajes de Comercial se acumulan como en la 012 (FR-142), y
`GetSalesDocumentCreditQuery` calcula «disponible» con el registro, no con la sola existencia del consumidor.

**Activar Cartera** (`ActivateLendingCommand`, T84) abre la vigencia de la fila `CAR` en el mismo guardado que
`LND_Activations` y `CarteraActivada`. En producción exige del registro: un recaudador `Receipts` y un destino
`Treasury` **reales** con las versiones vigentes en Cartera (`ProveedorDeObligaciones` = `VersionDelContrato`,
`OrdenDeGiroEmitida` v1 y `AnulacionDeOrdenDeGiro` v1); los dobles no cuentan (FR-089, FR-142). El parámetro de
Comercial sigue sirviendo a Comercial (sello de la cuenta por cobrar, crédito provisional) y la pantalla de activación
muestra que debe igualarse a la fecha de activación.

### 13.4 El despachador

El de la 012 (`integration.dispatch`, por `IEjecutorEnCooperativa`, actor «Proceso de integración» o la persona que
ordenó el lote o el reproceso), con dos cambios: **turna los destinos** —una tanda por destino en cada vuelta, dentro
del presupuesto de la cooperativa—, para que un atraso de `Accounting` no se coma los cinco segundos de SC-005 de la
respuesta a Comercial (T19); y agrupa las unidades también por `OriginModule`. La señal de mensajes nuevos, los
reintentos (`Integration:Retries`) y la verificación de `PayloadSha256` no cambian.

---

## 14. Estados de entrega y su traducción

Una fila de `COR_IntegrationMessageDeliveries` por mensaje y destino (único `(MessageId, Destination)`). El mensaje no
cambia; la entrega sí.

### 14.1 Estado inicial

| Destino · mensaje | `Mode` | `Status` inicial |
|---|---|---|
| `Accounting` · CAR de negocio, modo `EnLinea` | `Online` | `Pending` |
| `Accounting` · CAR de negocio, modo `PorLotes` | `Batch` | `InBatch`, con `ScheduleKey` y `BatchScopeKey` (§14.2); `BatchId` nulo hasta que un lote lo toma |
| `Accounting` · CAR de negocio, modo `NoPasa` | `NotPosted` | `NotApplicable` |
| `Accounting` · CAR informativo | `Always` | `Pending` |
| `Inventory`, `Treasury`, `Receipts` · todos | `Always` | `Pending` |
| `Lending` · todos (INV, TES, REC) | `Always` | `Pending` |

Cartera **sella** el modo de cada tipo al emitir (`ModoDeEntrega.Sellado`, por `ModoDePasoDeCartera` con el parámetro
vigente del tipo, T56), con dos excepciones: los acompañantes sellan el del principal (§7.2, C01) y los reversos
(`DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre`) **heredan el de su original** (C08): un reverso nunca pasa
sin su original ni espera a uno que no pasa. Para ninguno de los dos casos `Contabilidad.ModoDePaso` ofrece el tipo en
el ámbito `LendingFactType`.

### 14.2 Claves de lote de Cartera

- `ScheduleKey` (`ClavesDeLote.Horario`, ≤ 60; C06): `CAR.{ShortCode}|{disparador}|{hora}|{granularidad}`,
  sellados al emitir. `ShortCode` es el `LND_FactTypes.ShortCode` del tipo (columna «Corto» de §2.1; un acompañante
  pone el suyo con el disparador, la hora y la granularidad de su principal). La hora va **vacía** con
  `FinDeCausacion` y `CierreDeCartera`; el disparador `Hora` del parámetro se sella como la constante `HoraDiaria` de
  `ClavesDeLote` (012), así `ScheduleIntegrationBatchesCommand` no cambia. Granularidad: `PorHecho`, `PorDia` o
  `PorDiaYSucursal` (C07; `COR_IntegrationBatches.Granularity` sigue con `PostingGranularity`). Ejemplos:
  `CAR.ICA|FinDeCausacion||PorDia`, `CAR.DET|CierreDeCartera||PorDia`, `CAR.PAP|HoraDiaria|18:00|PorHecho`.
  `ScheduleIntegrationBatchesCommand` usa el respaldo de Inventario (`InventoryDocumentTypes`, `ContabilidadHoraDeLote`)
  sólo con claves INV.
- `BatchScopeKey`: `AccrualRun:{aaaa-mm-dd}` con `FinDeCausacion` y `LendingClose:{aaaa-mm}` con `CierreDeCartera`.
  Las partes de la causación de *d* se sellan con `AccrualRun:{d}`; un hecho `FinDeCausacion` nacido fuera de esa
  corrida (una operación, la puesta al día, la corrida `NationalValueCorrection`) toma el **día siguiente al de su
  registro**, y el lote de la corrida recoge también los alcances anteriores que quedaron sin lote (C09). El lote lo
  crea la corrida (`BatchTrigger.LendingAccrualRun = 7`), el cierre o la reapertura —que es una corrida
  `CloseReopen` con su propio lote `LendingMonthlyClose` y alcance `LendingClose:{aaaa-mm}`, C58—
  (`BatchTrigger.LendingMonthlyClose = 8`) en su propio guardado (T56).

### 14.3 Estados

| `DeliveryStatus` | Qué significa | Llega desde | Pasa a |
|---|---|---|---|
| `Pending` | Espera entrega | emisión; un `Retry` con `NextAttemptAt` | `Processed`, `Rejected`, `ValidationFailed` (sólo `Lending`), o sigue `Pending` |
| `InBatch` | Espera su lote o corre en uno | emisión por lotes; reproceso; envío posterior | `Processed`, `Rejected`, o sigue `InBatch` con `Retry` |
| `Processed` | El destino lo procesó; `ResultReference`, `ResultVoucherTypeCode`, `ResultVoucherNumber` dicen qué dejó (§14.4) | consumo | — |
| `Rejected` | Regla de negocio, con código y motivo; no se reintenta solo | consumo | `InBatch` por `ReprocessMessagesCommand`, con sus dependientes bloqueados |
| `NotApplicable` | Modo `NoPasa` sellado; se conserva para la conciliación (FR-130) | emisión | `InBatch` por `SendNotApplicableMessagesCommand`, con su clausura de relacionados |
| `ValidationFailed` | **Sólo `Lending`**: la obligación se creó con validación fallida (FR-052). Terminal, no bloquea a sus dependientes | consumo, **directo** desde `Pending` o `InBatch` (T17; la 012 lo suponía después de `Processed`: se enmienda su tabla de estados, `mensajes.md` §11 de la 012, en la lista de T97; C18) | — (confirmar por excepción cambia el recibo, no la entrega, B8) |

Bloquean a sus dependientes `Pending`, `InBatch` y `Rejected`. Quién escribe la entrega: el emisor (al nacer),
`RegisterDeliveryResultCommand` (resultado y bitácora `COR_IntegrationDeliveryAttempts`), `OrderIntegrationBatchCommand`,
`ReprocessMessagesCommand` y `SendNotApplicableMessagesCommand`. **El consumidor nunca toca tablas de la plataforma.**
Para los mensajes CAR, el reproceso y el envío posterior se piden desde la bandeja de Cartera (`/api/lending/messages`,
`Lending.Messages.Reprocess` y `.SendNotApplicable`, con motivo), sobre los mismos comandos genéricos; la estrategia
`IVistaDeBandejaDelModulo` de Cartera aplica el alcance por oficina y el modo vigente por tipo (T19).

### 14.4 Traducción del resultado del consumo

| `ResultadoDeConsumo` | `DeliveryAttemptOutcome` | `DeliveryStatus` | Lo que guarda la entrega | Alerta |
|---|---|---|---|---|
| `Processed(ref, tipo, número, sinComprobante)` | `Processed = 1` | `Processed` | Contabilidad: el comprobante (`PublicId`, tipo, número), o el recibo sin comprobante de un informativo. Cartera: `PublicId` y número del crédito creado, o la referencia «no incorporada». Comercial: el `PublicId` de su recibo. Tesorería y Recaudo: el de su recibo | — |
| `AlreadyProcessed(ref, …)` | `AlreadyProcessed = 2` | `Processed` | La misma referencia | — |
| `Rejected(código, motivo, datos)` | `Rejected = 3` | `Rejected` | Código y motivo | `Integracion.MensajeRechazado` |
| `Retry(motivo, código)` | `Retry = 4` | sigue `Pending` / `InBatch`, con `NextAttemptAt` = `min(15 s·2^(n−1), 15 min)` + dispersión | — | `Integracion.MensajeSinEntregar` a los 3 intentos o 15 minutos |
| `ValidationFailed(ref, número, código, motivo)` (nueva, CH12) | `ValidationFailed = 5` (nuevo) | `ValidationFailed` | `ref` = `PublicId` del crédito, `número` = su número, y código y motivo como en un rechazo | `Integracion.ValidacionFallida` (hasta T17 sin escritor) |

- `RegisterDeliveryResultCommand` admite `ValidationFailed` sólo en entregas a `Lending` que estén `Pending` o
  `InBatch`; en cualquier otro destino es un defecto.
- La referencia «no incorporada» es un recibo sin comprobante: `MotivoSinComprobante.NotIncorporated = 3` (valor
  aditivo en el enum de la 012, C21) y `ReferenciasDeResultado.NoIncorporada = "no incorporada"`, junto a
  «informativo» y «sin comprobante (valor cero)» de la 012. Del lado contable C21 suma además
  `NoVoucherReason.SameAccounts` y la alerta `Cartera.HechoNoContabilizable` (`contabilidad.md`).
- **Destinatarios de las alertas** de integración por módulo de origen y destino (T19): un mensaje CAR rechazado
  llega a quien tiene `Lending.Messages.Reprocess` y, si el destino es `Accounting`, a quien tiene
  `Accounting.LendingBatches.Run`; un `ValidationFailed` llega a Cartera y a Comercial (`Inventory.Messages.Reprocess`);
  una coincidencia en listas restrictivas avisa además al oficial (`Cartera.CoincidenciaEnListas`, FR-052).
- Contabilidad: `PeriodClosed` → `Rejected`; `PeriodNotFound` (p. ej. el ejercicio siguiente sin abrir el 1 de enero)
  → `Retry` con la alerta `Contabilidad.PeriodoInexistente` (T36).

---

## 15. El consumidor de Cartera y su recibo

### 15.1 `DestinoCartera`

`IDestinoDeMensajes` con `Destino = Lending`, en `Application/Lending/Integration`, registrado sólo cuando nace
`CarteraNucleo`. Corre por `IEjecutorEnCooperativa`, en un ámbito propio por unidad, con el actor «Proceso de
integración» (I-2); nunca usa los permisos del usuario de origen.

- **`Acepta(type, version)`** (`VersionesAceptadasDeCartera`): **toda** versión de `VentaACreditoRegistrada` y
  `AjusteDeVentaACredito` —así una versión desconocida no se rechaza en el despachador sin respuesta, sino que el
  consumidor deja su evaluación y responde `ObligacionRechazada` con `Integration.VersionNotAccepted`, como pide
  FR-053; es enmienda a la 012 §12, en la lista de T97, C16—; `ResultadoDelGiro` v1; desde K2, `EnvioDeDescuentosConfirmado` v1 y `ResultadoDeDescuentos` v1. Lo demás,
  `Rejected` con `Integration.VersionNotAccepted` en el despachador.
- **Un comando sin ruta por familia** (`LosComandosDeConsumoNoTienenRuta`), `IReintentableAnteConcurrencia`, en el
  molde de `PostInventoryMessagesCommand`: `ConsumeCommercialCreditMessageCommand` (ventas y ajustes),
  `ConsumeTreasuryResultCommand` (resultado del giro), `ConsumePayrollDeductionResultCommand` (envío y resultado de
  descuentos, K2).
- `PlanearLote`: una unidad por trabajo (Cartera no resume). `TotalesDelLoteAsync`: cero (no deja comprobantes).
- El consumidor **no lee la bandeja por su cuenta**: lee por `IMensajesEntrantes` (`LeerAsync`, y `PendientesAsync`
  para la conciliación de §15.4).

### 15.2 El recibo y las evaluaciones

En **un** `SaveChanges` el comando guarda:

1. el **recibo** `LND_InboundMessages` (`LendingInboundMessage`), único por `MessagePublicId`: tipo, versión, módulo
   de origen, estado (`InboundMessageStatus`), el crédito que creó o afectó, y los mensajes que respondió;
2. una **evaluación** `LND_InboundMessageEvaluations` (`LendingInboundMessageEvaluation`, sólo inserción) por cada vez
   que lo evalúa: resultado, código, motivo y cada comprobación de FR-051 con su evidencia;
3. el **efecto**: el crédito con su `LoanOperation` y sus transacciones, el ajuste, el reverso del desembolso, la marca
   en el archivo de descuentos;
4. los **mensajes** que emite: hechos a Contabilidad y respuestas a Comercial.

`InboundMessageStatus { Processed = 1, ValidationFailed = 2, Rejected = 3, NotIncorporated = 4, ConfirmedByException
= 5 }`. Un recibo ya existente en `Processed`, `ValidationFailed`, `NotIncorporated` o `ConfirmedByException` responde
`AlreadyProcessed` con la misma referencia y no emite nada; uno en `Rejected` se vuelve a evaluar (reproceso) y suma
una evaluación. La colisión del índice único por concurrencia se traduce a `AlreadyProcessed`.

### 15.3 Una venta a crédito, paso a paso

`ConsumeCommercialCreditMessageCommand` con un `VentaACreditoRegistrada`:

1. **Recibo**: si existe y no está `Rejected`, `AlreadyProcessed`.
2. **Versión**: desconocida → evaluación y `Rejected` (`Integration.VersionNotAccepted`), reprocesable y sin
   `ObligacionRechazada` (C95).
3. **Antes del corte**: con `origin.operationDate` ≤ corte de la carga activada, §15.4.
4. **Conversión** (FR-053, FR-022): persona viva, línea válida que admite origen comercial, condiciones con
   equivalencia. Si falla → evaluación y `Rejected` (reprocesable); la venta se informa `Pending` con su motivo y
   **no** se emite `ObligacionRechazada` hasta que un reproceso falle de forma definitiva o una persona marque el
   recibo no reprocesable; entonces sale `NoConvertible` con `retryable = false` (C95).
5. **Número**: el reservado de la v2 (`Reserved`/`Expired` → `Used`) o uno nuevo del numerador, al final del orden de
   cerrojos (T22, T34).
6. **Validación** (FR-051): con `pendingValidation`, evalúa a la fecha de la venta contando las anteriores ya creadas.
   Negativa → la obligación se crea igual, marcada «validación fallida» con su motivo, y se bloquea crédito nuevo al
   deudor (`LND_DebtorBlocks`); la marca no cambia causación, cobro ni calificación (FR-052).
7. **Validación previa contable**: advierte y sella `PrevalidationOutcome`, nunca frena (T56; D9).
8. **Efecto y mensajes**: crédito, desembolso en especie, `CreditoDesembolsado` y `ObligacionCreada`.
9. **Respuesta**: `Processed(crédito, null, número)` o `ValidationFailed(crédito, número, código, motivo)`.

### 15.4 Ventas anteriores a la activación (FR-143)

- **Al aplicar la carga** (`ApplyMigrationLoadCommand`), Cartera lista sin consumir las ventas y ajustes acumulados con
  fecha ≤ corte (`IMensajesEntrantes.PendientesAsync(Lending, [VentaACreditoRegistrada, AjusteDeVentaACredito], corte,
  página)`) y los concilia por factura en `LND_MigrationLoadDifferences`: migrada, cancelada antes del corte, no
  migrada con saldo, y aceptada fuera de Cartera (D29, T83). Las ventas que llevan `CreditWithoutLending` no emiten
  mensaje y no aparecen.
- **Al activar**, el destino se habilita y el consumidor procesa los acumulados en orden de `Id`; al ver la fecha ≤
  corte, responde según la conciliación vigente (recalculada en la activación, T84):

| Caso | Respuesta del consumidor | Entrega | Recibo | Mensaje a Comercial |
|---|---|---|---|---|
| Venta creada y validada (o sin pendiente) | `Processed(crédito, número)` | `Processed` | `Processed` | `ObligacionCreada` `Validated` |
| Venta con validación fallida | `ValidationFailed(crédito, número, código, motivo)` | `ValidationFailed` | `ValidationFailed` | `ObligacionCreada` `ValidationFailed` |
| Venta no convertible | `Rejected(código, motivo)` | `Rejected` | `Rejected` | ninguno mientras sea reprocesable; `ObligacionRechazada` `NoConvertible` (no reprocesable) cuando deja de serlo (C95) |
| Venta anterior al corte, **migrada** | `Processed(crédito, número)` | `Processed` | `Processed` | `ObligacionCreada` `Migrated` |
| Venta anterior al corte, **cancelada antes del corte** | `Processed(crédito en cero, número)` | `Processed` | `Processed` | `ObligacionCreada` `CancelledBeforeCutoff` |
| Venta anterior al corte, **aceptada sin migrar** (D17) | `Processed` «no incorporada» | `Processed` | `NotIncorporated` | `ObligacionRechazada` `AceptadaSinMigrar` (no reprocesable) |
| Venta de servicios **aceptada fuera de Cartera** (D29) | `Processed` «no incorporada» | `Processed` | `NotIncorporated` | `ObligacionRechazada` `AceptadaFueraDeCartera` (no reprocesable) |
| Ajuste ≤ corte de una venta migrada | `Processed` | `Processed` | `Processed` | ninguno (ya está en el saldo neto) |
| Ajuste de una venta aceptada sin migrar o fuera de Cartera | `Processed` «no incorporada» | `Processed` | `NotIncorporated` | `ObligacionRechazada` `SinObligacion` (no reprocesable) |
| Recibo ya existente | `AlreadyProcessed` | `Processed` (sin cambio) | sin cambio | ninguno |
| Falla transitoria (cerrojo, base) | `Retry` | `Pending` | — | ninguno |

`EstadoDeValidacionAsync` informa como validadas las migradas y canceladas, y como `NotIncorporated` las aceptadas sin
migrar o fuera de Cartera (FR-143, `EstadosDeValidacion`).

### 15.5 Confirmar por excepción

Un usuario con atribución confirma por excepción una obligación con validación fallida, con motivo (FR-052, salvo los
casos sin excepción de FR-038). Cambia el **recibo** a `ConfirmedByException` y levanta el bloqueo; la **entrega** queda
en `ValidationFailed` como hecho histórico, y `EstadoDeValidacionAsync` responde `Validated` «confirmada por excepción»
(B8). No se emite mensaje.

### 15.6 Alertas del consumidor

`Integracion.ValidacionFallida` (por la entrega, §14.4); `Cartera.CoincidenciaEnListas` (oficial de cumplimiento);
`Cartera.ReservaVencida` cuando una v2 toma una reserva vencida (informativa); `Cartera.NoAplicadoDePagaduria` (K2);
`Integracion.MensajeRechazado` en todo `Rejected`.

---

## 16. Idempotencia

Cuatro capas y una gramática de claves (T21); repetir tres veces el mismo mensaje u operación produce un solo efecto
(SC-010).

| Capa | Mecanismo | Qué evita |
|---|---|---|
| 1 · Pantalla | Comandos `IOperacionIdempotente`, cabecera `Idempotency-Key`, `COR_OperationKeys` (`LosComandosDeCarteraLlevanClave`): desembolso, reverso, anulación de orden, cierre y reapertura, activación, carga, reproceso y envío posterior, dobles de prueba | Que repetir un clic emita dos veces |
| 2 · Contrato y reserva | Clave de negocio de texto + `HuellaDeOperacion.Calcular`, con índice único en el documento de Cartera: `(CollectorCode, IdempotencyKey)` de `LND_LoanOperations` y la de `LND_LoanNumberReservations`. Gramática `{MÓDULO}:{operación}:{obligationPublicId:N}`: `NOM:{run:N}:{obligación:N}`, `CAR:{desembolso:N}:{obligación:N}`; la del recaudador de prueba, `TST:{guid:N}` (C35); derivadas `{clave}:reverso` y `{clave}:destino`; reserva `INV:{paymentPublicId:N}:{monto}` (`interfaces.md` §3.10, §5.2) | Doble aplicación; misma clave con otro contenido → `Lending.Contract.KeyReused` / `Lending.Reservation.KeyReused` |
| 3 · Mensajes | **Emitido**: único `(OriginPublicId, Type, OriginEventKey)`, una doble emisión es un defecto y hace fallar el `SaveChanges`. **Entrega**: `RowVersion` de `COR_IntegrationMessageDeliveries` (012). **Recibido**: recibo único por `messageId` en cada destino, escrito en el mismo `SaveChanges` que su efecto | Doble emisión, doble registro del resultado y doble consumo |
| 4 · Procesos | Marca de agua (`LoanControl.LastAccruedDate`) e índice único de la causación por crédito, concepto, cuota y fecha valor; una parte nace una sola vez por la unicidad de capa 3 | Causar dos veces un día |

**Recibos por destino**: `Accounting` → `ACC_LendingPostings` (único `MessagePublicId`, también para los
informativos); `Lending` → `LND_InboundMessages`; `Inventory` → `INV_LendingObligationResponses`; `Treasury` y
`Receipts` → el que construya la feature que adopte el mensaje: **es requisito del contrato** tener recibo único por
`messageId` en el mismo guardado que su efecto y traducir la colisión a `AlreadyProcessed`. Los dobles consumen sin
estado propio (§18).

Una respuesta nueva tras un reproceso no es un duplicado: cada evaluación tiene su clave
(`Confirmation:{evaluación:N}`).

---

## 17. Versiones

- Todo tipo de Cartera nace en **v1**; `VentaACreditoRegistrada` suma la **v2** (Comercial). El record `<Type>V<n>` y
  su esquema no cambian una vez publicados: agregar, quitar o renombrar un campo, cambiar un tipo, un signo, un valor
  admitido o el significado de un campo crea `<Type>V<n+1>` (012 §12). Lo emitido conserva su versión para siempre.
- Cambiar `Destinos` o `AdmiteProceso` de un tipo es un cambio del catálogo, no del contenido: no crea versión.
- Los campos que piden K6a y K6b (modelo de deterioro, segmento, unidad UVR) están en `LoanFactDimensionsV1` desde V1:
  esas entregas no crean versiones.

| Destino | Acepta (V1 del catálogo) |
|---|---|
| `Accounting` (`DestinoContabilidad`, versiones por módulo) | INV: v1 de los 18 tipos de la 012 que van a Contabilidad. CAR: v1 de los 35 de §2.1 (los de K3 y K4 aceptados desde K1; los tres contingentes, mientras rija A3) |
| `Lending` (`DestinoCartera`) | `VentaACreditoRegistrada` v1 y v2 (y toda otra versión, para responderla, §15.1); `AjusteDeVentaACredito` v1; `ResultadoDelGiro` v1; desde K2 `EnvioDeDescuentosConfirmado` v1 y `ResultadoDeDescuentos` v1 |
| `Inventory` (`DestinoInventario`) | `ObligacionCreada` v1, `ObligacionRechazada` v1 |
| `Treasury` | Lo que adopte en el registro: `OrdenDeGiroEmitida` v1, `AnulacionDeOrdenDeGiro` v1 |
| `Receipts` | Lo que adopte: `PagoReversadoPorCartera` v1; desde K2 `DescuentosPorPagaduria` v1 |

- **Las v1 de Comercial esperan meses.** I1–I6 están en producción desde el 2026-10-01: toda venta a crédito confirmada
  desde entonces dejó un `VentaACreditoRegistrada` v1 acumulado hacia `Lending` (SC-023). Cartera acepta v1 hasta que
  no quede ninguna entrega v1 en `Pending`, `InBatch`, `Rejected` o `NotApplicable`.
- **Versiones hacia Tesorería y Recaudo.** El participante adopta versiones en el registro. Publicar una v2 de un
  mensaje que ellos consumen exige que la adopten antes; mientras tanto Cartera sigue emitiendo la que adoptaron, y la
  activación no cuenta a un participante sin una versión vigente.

---

## 18. Dobles de prueba

Tres conceptos distintos, con un solo criterio de producción, `IHostEnvironment.IsProduction()` (CH20):

| Concepto | Qué es | En producción |
|---|---|---|
| **Dobles de prueba** (`TesoreriaDePrueba`, `RecaudadorDePrueba`) | Implementaciones de los destinos `Treasury` y `Receipts` y del recaudador de prueba, para ejercitar Cartera sin Tesorería ni Recaudo (FR-089) | **No pueden existir**: la API no arranca si la configuración los pide |
| Canales simulados (`SIMULADO`) | Adaptadores externos sin efecto legal (T73) | Se admiten, sin efecto legal |
| Dobles unitarios | Sustitutos de puertos en `tests/` | No se despliegan |

**Registro y guardia** (T20):

- Se registran en DI sólo si `Lending:TestDoubles:Enabled = true` **y** `!IHostEnvironment.IsProduction()`. Si la
  configuración los pide en producción, la API **se niega a arrancar**.
- En cada cooperativa se habilitan con filas `Code = TST`, `Class = Test`, `IsTestDouble = true`:
  `TST × MessageDestination × Treasury`, `TST × ObligationsCollector` y `TST × MessageDestination × Receipts`.
  `EscritorDelRegistroDeIntegracion` las rechaza en producción (`Integration.Participant.TestDoubleInProduction`), y la
  activación en producción no las cuenta.
- Ocupan el destino real: como hay un solo consumidor por destino, cuando llegue la feature de Tesorería o de Recaudo
  el doble se retira; si los dos quedaran registrados, el arranque falla con `Integration.Destination.DuplicateConsumer`.
- Si QA corre con el ambiente `Production`, allí no hay dobles (pregunta B4).

**Qué hacen** (pantalla `/cartera/dobles-de-prueba`, rutas `/api/lending/test-doubles/{treasury, collector}/…`, permiso
`Lending.TestDoubles.Operate`; detalle en `api.md`):

| Doble | Consume | Opera | Emite |
|---|---|---|---|
| `TesoreriaDePrueba` | `OrdenDeGiroEmitida`, `AnulacionDeOrdenDeGiro` (responde `Processed` sin estado propio) | Quien prueba elige el resultado de cada orden | `ResultadoDelGiro` como `TES`: `Disbursed`, `Rejected`, `CheckNotCashed`, `Voided` |
| `RecaudadorDePrueba` | `PagoReversadoPorCartera`; desde K2 `DescuentosPorPagaduria` | Lista, simula, aplica, reversa y resuelve saldos por el contrato (código `TST`, clave `TST:{guid:N}` que la pantalla propone y deja repetir para probar la idempotencia; C35) | Desde K2, como `REC`: `EnvioDeDescuentosConfirmado` y `ResultadoDeDescuentos` a partir de un archivo de respuesta que carga quien prueba, leído con el formato de vuelta del convenio |

Todo lo que emiten pasa por `EmisorDeMensajes` con el módulo del tipo y con **la persona que prueba** como usuario de
origen (T8); sus emisiones son operaciones de pantalla con `Idempotency-Key`. Lo fijan `LosDoblesDePruebaNoSeRegistranEnProduccion`
(recorre el DI con el ambiente de producción y comprueba que la configuración que los pide impide el arranque),
`UnSoloConsumidorPorDestino` y las e2e de la colección «Cartera e2e».

---

## 19. Lo que este contrato fija por su cuenta

Las transversales no bajaban a este detalle; lo decide este documento, en coherencia con ellas y con su §7. Si
`data-model.md`, `contabilidad.md`, `interfaces.md` o `plan.md` dicen otra cosa, gana este documento en tipos, claves
y contenido (transversales §7.6).

1. **Claves de los hechos que una operación emite aparte** (`Confirmation:{tipoDeHecho:N}`) y de los hechos por
   crédito de un proceso (origen = la parte, clave `Confirmation:{crédito:N}`, C02): T12 pide orígenes que nunca sean
   el crédito y unidades homogéneas, sin formas de clave nuevas.
2. **La lista fija de acompañantes** de §7.2 (C01, C03): `PagoAplicado`, `PagoReversado`, `DesembolsoReversado` y
   `CreditoAjustadoDesdeComercial` con los suyos; sellan el modo del principal.
3. **`AnulacionDeOrdenDeGiro` usa `Confirmation:{orden:N}`**, para no compartir unidad con la orden (C12).
4. **El origen de las respuestas a Comercial es el recibo** del mensaje (`LND_InboundMessages`), y una `ObligacionCreada`
   también responde a un `Replacement` (C17).
5. **`DestinoCartera` acepta toda versión de la familia de la venta**, para poder responder «versión no aceptada» (C16).
6. **`DescuentosPorPagaduria` lleva sus líneas** con `deductionFileLinePublicId`, para que Recaudo cite la línea al
   aplicar el giro sin leer `LND_` (C19).
7. **`paidByFile` en la orden** en lugar de la referencia del archivo (C10); los campos y los códigos de rechazo del
   giro los fija `interfaces.md` (C13).
8. **`ScheduleKey` de Cartera con código corto** (`CAR.{ShortCode}|{disparador}|{hora}|{granularidad}`, C06).
9. **Códigos de participante** `CNT`, `INV`, `NOM`, `CAR`, `TST` y las columnas `Destination` e `IsEnabled` del registro
   (C35, C36).
10. **Signo mixto** sólo en `CreditoAjustadoDesdeComercial`, `AjusteDeCausacionPorFechaValor` y las reasignaciones.
11. **El contenido de los contingentes de A3** (§10.24, C15) y los datos que Contabilidad necesita (C22): `writtenOff`,
    `previousDimensions` en las novedades, el movimiento del deterioro, el beneficiario y la forma del desembolso, y lo
    que sale contra el deterioro en condonaciones y castigos.

---

## Nombres fijados en este documento

Con las reglas T1–T4; ninguno reutiliza un nombre heredado.

| Clase | Nombres |
|---|---|
| Bloques de mensaje (`Contracts/Lending`) | `LoanFactTotalV1`, `CollectorRefV1`, `ReceiptRefV1`, `GeneralImpairmentLineV1`; campos `LoanFactDimensionsV1.writtenOff`, `catchUpOperation` de la forma masiva |
| Plataforma | `MotivoSinComprobante.NotIncorporated = 3`; `ReferenciasDeResultado.NoIncorporada` («no incorporada»); `PrevalidationOutcome.NotPostable` (C21); columnas `COR_IntegrationParticipants.Destination` e `IsEnabled` con único `(Code, Role, Destination)` (C36) |
| Códigos de participante | `CNT` (Contabilidad), `INV` (Comercial), `CAR` (destino Cartera y recaudador de la recogida, sin fila), `NOM` (Nómina), `TST` (dobles de prueba) (C35) |
| Códigos cortos (`LND_FactTypes.ShortCode`, C06) | Los de la columna «Corto» de §2.1: `DES`, `DRV`, `ACO`, `ICA`, `MCA`, `SCA`, `AFV`, `CSU`, `CRE`, `PAP`, `PRV`, `ANR`, `ANA`, `CIS`, `SFR`, `SFS`, `RCA`, `DCC`, `CPI`, `DET`, `RCL`, `RCI`, `GCO`, `REE`, `MOD`, `NOV`, `CON`, `CAS`, `ACT`, `CAP`, `CIR`, `CRA`, `CAD`, `CRO`, `GCT` |
| Tipos de mensaje (A3, C15) | `CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida` |
| Clases de origen (texto en `OriginDocumentClass`) | `LoanDisbursement` y `PaymentApplication` (T13), más `DisbursementReversal`, `CommercialAdjustment`, `PaymentReversal`, `CreditBalanceResolution`, `ProvisionalCollection`, `LoanCharge`, `TermsChange`, `Waiver`, `WriteOff`, `RateReset`, `PreferenceChange`, `LoanOpeningBalance`, `AccountingFactPart`, `MonthlyClose`, `LendingActivation`, `MigrationLoad`, `DisbursementOrder`, `PayrollDeductionFile`, `InboundMessage`; con A3, `CreditApplication`, `CreditLimit`, `Collateral` |
| Rótulos | Partes `CAU-{aaaammdd}-{nnnn}`, `VNC-{aaaammdd}-{nnnn}`, `CIE-{aaaamm}-v{versión}-{nnnn}`, `REA-{aaaamm}-v{versión}-{nnnn}`; partes de la puesta al día `ALD-{número de la operación}-{n}`; carga `CORTE-{aaaammdd}-{n}`; `ScheduleKey` `CAR.{ShortCode}|{disparador}|{hora}|{granularidad}` |
| Claves de evento (usos de formas existentes) | `Confirmation:{tipoDeHecho:N}` (`LND_FactTypes.PublicId`, sembrado con Guid fijo), `Confirmation:{crédito:N}`, `Confirmation:{solicitud:N}`, `Confirmation:{cupo:N}`, `Confirmation:{garantía:N}`, `Confirmation:{orden:N}`, `Confirmation:{archivo:N}` |
| Errores | `Lending.Sale.PersonNotFound`, `Lending.Sale.CreditLineInvalid`, `Lending.Sale.TermsWithoutEquivalence`, `Lending.PayrollFile.Superseded`, `Lending.PayrollFile.HashMismatch`, `Lending.PayrollFile.LineMismatch`; los del giro (`Lending.DisbursementOrder.InvalidTransition`, `.AmountMismatch`) los fija `interfaces.md` §4.3 |
| Valores de texto cerrados | `ObligacionRechazada.motive`: `NoConvertible`, `AceptadaSinMigrar`, `AceptadaFueraDeCartera` (y `SinObligacion`, de T18); `ObligacionCreada.numberSource`: `Reservation`, `Assigned`, `Migration`; `disbursementForm`: los de la orden (`Transfer`, `Check`, `ThirdPartyPayment`, de `interfaces.md`) más `InKind`; `DesembolsoReversado.cause`: `Rejected`, `CheckNotCashed`, `Voided`, `Manual`; suspensión: `ArrearsThreshold`, `RepeatedRestructuring`, `Insolvency`, `WriteOff`; reanudación: `ArrearsCured`, `InsolvencyEnded`, `CauseReversed`; `AjusteDeCausacionPorFechaValor.cause`: `ValueDatedOperation`, `NationalValueCorrection`; `SaldoAFavorReconocido.cause`: `NoPrepaymentPreference`, `CommercialAdjustment`, `NationalValueCorrection`; `PagoReversado.initiatedBy`: `Collector`, `Lending`; `EnvioDeDescuentosConfirmado.channel`: `Email`, `Portal`, `Physical`, `Other`; `ResultadoDeDescuentos.outcome`: `Deducted`, `PartiallyDeducted`, `NotApplied`; `GeneralImpairmentLineV1.kind`: `General`, `Additional`; movimiento del deterioro en `reasonCode`: `Increase`, `DecreaseSameYear`, `DecreasePriorYears`, `Transfer`; `valueUnit`: `Pesos`, `Uvr`; `reasonCode` `Capitalizacion`; contingentes: `reasonCode` `ApprovedNotDisbursed`, `RevolvingLimit`, `CollateralReceived`, `subject.kind` `CreditApplication`, `CreditLimit`, `Collateral`, y los `movement` de §10.24 |

## Conciliación

Los puntos que este documento dejó para conciliar se decidieron en `decisiones-transversales.md` §7 (origen `MSG-n`)
y ya están aplicados en el cuerpo.

| Punto | Decisión |
|---|---|
| MSG-1 · orden por crédito | Rige T12 y CH5: sin cadena por crédito; aristas al original, al propio origen y a la venta (§8) |
| MSG-2 · cuentas contingentes (A3) | C15: `CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida`, `AdmiteProceso = true`, `NoPasa`, `CN`, K1; el catálogo pasa de 44 a 47 filas (§2.1, §10.24) |
| MSG-3 · Recaudo sin persona | C14: se mantiene T11; la feature 014 aplica a nombre de quien cargó o aprobó el lote (§6) |
| MSG-4 · origen de los hechos por crédito de un proceso | C02: la parte de su tanda, modo propio, `Confirmation:{crédito:N}` (§7.1) |
| MSG-5 · `AjusteDeCausacionPorFechaValor` | C03: por partes en la corrida `NationalValueCorrection`; acompañante de la operación fechada atrás (§7.2, §10.5) |
| MSG-6 · unidades y modo | C01: unidad = operación y modo; lista fija de acompañantes que sellan el modo del principal; sin clave `Confirmation:{uuidv5}` (§7.2) |
| MSG-7 · orden pagada por archivo | C10: nace al desembolsar con `paidByFile`; sin `DisbursementFilePublicId` (§11.3; `interfaces.md` §4) |
| MSG-8 · nombre del proceso | C23: `Actor.NombreDelProceso` («Proceso de integración») (§5) |
| MSG-9 · versión no aceptada | C16: `Acepta` admite toda versión de la familia de la venta; enmienda a la 012 §12 en T97 (§15.1) |
| MSG-10 · `ObligacionCreada` en un reemplazo | C17 (§2.2, §11.1) |
| MSG-11 · registro de participantes | C36: `Destination` e `IsEnabled`, único `(Code, Role, Destination)`, `destination` en el comando, `Lending` sembrado inactivo; C35: códigos (§13.2) |
| MSG-12 · `ValidationFailed` directo | C18: enmienda a la tabla de estados de la 012, en T97 (§14.3) |
| MSG-13 · `DescuentosPorPagaduria` con líneas | C19 (§11.6) |
| MSG-14 · reserva desconocida en una v2 | C20: no rechaza; `numberSource = Assigned`; `Lending.Reservation.NotFound` sólo en las rutas de reserva (§12.2) |
| MSG-15 · enum de la plataforma | C21: `MotivoSinComprobante.NotIncorporated`, aditivo (§14.4) |
| MSG-16 · alerta de reserva vencida | C21: `Cartera.ReservaVencida` (§12.2, §15.6) |
| MSG-17 · clave de `AnulacionDeOrdenDeGiro` | C12: `Confirmation:{orden:N}`; `related` = la orden como documento; el mensaje de la orden en `orderMessagePublicId` (§11.4) |
| MSG-18 · hermanos por alinear | C35: `TST:{guid:N}`, se retira `TEST:`; C36: `destination` en `RegisterIntegrationParticipantCommand` (§13.2, §16) |
| Otras que este documento aplica | C04 puesta al día por partes (§7.1); C05 la corrida `NationalValueCorrection` la encola la réplica (§10.5); C06 y C07 `ScheduleKey` y `PorHecho` (§14.2); C08 los reversos heredan el modo (§14.1); C09 alcance del lote (§14.2); C11 recogida al anular (§10.2); C13 campos de giro en `interfaces.md` (§11.3–§11.5, §12.4); C22 contenido para Contabilidad (§9, §10); C62 cobros provisionales al nacer la obligación (§10.16) |
