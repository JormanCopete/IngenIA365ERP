# Contrato contable de Cartera (enmienda E-01 de la 009)

**Feature**: 013 | **Fecha**: 2026-10-05 | **Entregas**: K1 (los hechos de K3 y K4 se declaran en K1) | **Base**:
`decisiones-transversales.md` §2.4, §2.9, §2.10, §2.11, §2.15–§2.20, T11–T15, T36, T47–T49, T51–T59, T84, T92,
T96, T97, T101; spec FR-008, FR-012, FR-041, FR-045–FR-047, FR-056, FR-088, FR-107, FR-112, FR-125–FR-130,
FR-139–FR-143, FR-146, FR-147, C3, C4, D7, D8, D12, D16, D17, D19, I-2, I-5, US7 | **Molde**:
`specs/012-inventario-comercial/contracts/contabilidad.md` | **Hermanos**: `mensajes.md` (qué trae cada hecho),
`api.md` (rutas de Cartera), `plantillas.md` (mecánica común de importación);
`specs/009-contabilidad-niif/contracts/contabilizacion.md` (el contrato que se enmienda)

Lo que Contabilidad construye en esta feature para recibir a Cartera por hechos, y lo que Cartera necesita de
Contabilidad. Es la enmienda **E-01** (FR-146, C3): la 009 asignaba a Cartera cinco tipos de comprobante por
llamada directa al contrato, con las cuentas guardadas como texto en la línea de crédito; la 013 la reemplaza por
el molde que la 012 aplicó a Inventario. **El único camino al libro sigue siendo `AccountingPoster`**: lo que
cambia es quién lo llama y cuándo. La atomicidad pasa a ser dos, una a cada lado:

- en Cartera, **operación + transacciones del libro + hecho** en un `SaveChanges` (T5: «un hecho nace en la
  transacción que guarda lo que cuenta»);
- en Contabilidad, **comprobante + recibo del hecho** (`ACC_LendingPostings`) en un `SaveChanges`.

Lo que **no** cambia: las reglas 1 a 11 de `AccountingPoster` y `AccountLineRules`, la numeración, la
inmutabilidad, la matriz y el consumidor de Inventario (las e2e «Inventario e2e» siguen verdes sin tocarse) y las
pruebas `NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioXI_ContableImmutable` y
`LaContabilidadNoTieneCuentasEnCodigo`.

## 1. Frontera y piezas

### 1.1 Piezas

| Pieza | Dónde | Qué hace |
|---|---|---|
| `ACC_LendingPostingRules` · `LendingPostingRule` | `Domain/Entities/Accounting/Lending` | La matriz (§3). |
| `ACC_LendingVoucherMappings` · `LendingVoucherMapping` | ídem | Hecho (y clase de recaudador) → tipo de comprobante y documento cruce (§5). |
| `ACC_LendingPostings` · `LendingPosting` (hecho inmutable, `bigint`) | `Domain/Entities/Accounting/Transactions` | Recibo único por hecho procesado (§4.3). |
| `ACC_RegulatoryIndicators` · `RegulatoryIndicator` | `Domain/Entities/Accounting/Lending` | Patrimonio técnico y relación de solvencia que registra el contador (§15). |
| `Reglas/{OperacionesDeCartera, RolesDeCuentaDeCartera, OrigenesDeCartera, ResolutorDeReglasDeCartera, MatrizVigenteDeCartera, ReglasDeLaMatrizDeCartera}` | `Application/Accounting/Lending` | Catálogos fijos, resolución y validación de reglas (§2, §3). |
| `CreateLendingPostingRuleCommand`, `AddLendingPostingRuleVersionCommand`, `DeactivateLendingPostingRuleCommand`, `ImportLendingPostingRulesCommand`, `GetLendingRulesTemplateQuery`, `SetLendingVoucherMappingCommand`, `ResolveLendingRuleQuery` | ídem | Administración de la matriz (§3.5–§3.7). |
| `Contabilizacion/{ConstructorDeLineasDeCartera, ConsumoDeCartera, TiposDeComprobanteDeCartera}`, `PostLendingFactsCommand`, `PostLendingSummaryGroupCommand` | ídem | El consumidor (§7, §10). `ConstructorDeLineasDeCartera` no hace IO. |
| `Lotes/AgrupadorDeResumidosDeCartera` | ídem | Grupos del resumido (§10.4). |
| `Consultas/{EvaluateLendingPostingQuery, GetLendingAccountBalancesQuery, GetAccountingPeriodStatusQuery, GetEntityIndicatorsQuery, GetLendingRulesCompletenessQuery, PreviewLendingBatchQuery, ListLendingPostingsQuery, GetLendingBatchTotalsQuery}`, `RegisterRegulatoryIndicatorCommand` y el adaptador `ContabilidadParaCartera` | ídem | Lo que Cartera y la contadora consultan (§13–§16). |
| `ConjuntosDeCuentas`, el recibo común y la revisión de versión y moneda | `Application/Accounting/Integration` (carpeta común nueva, en inglés por T1; C82) | Lo genérico que se extrae del consumidor de Inventario sin cambiar su comportamiento (T51): unión-búsqueda combinación ↔ cuenta (§14). |
| `DestinoContabilidad` (existe) | se muda a `Application/Accounting/Integration` | Único consumidor del destino `Accounting`; reparte por módulo de origen (§6). |
| `IContabilidadParaCartera` | `Application/Common/Integration/Accounting` | Lo único de Contabilidad que Cartera conoce (§16.1). |
| `IDimensionesDeCartera` (lo implementa Cartera: `DimensionesDeCartera`) | `Application/Common/Integration/Lending` | Lo único de Cartera que Contabilidad conoce, además de la bandeja (§16.2). |
| `EmisionDeCartera`, `ModoDePasoDeCartera`, `ValidacionPreviaContableDeCartera`, `FechaContable`, `ConciliacionContableDeCartera`, `SaldosContablesDeCartera` | `Application/Lending/Integration` | El lado de Cartera (§8, §10, §13, §14). |

### 1.2 Frontera

Lo vigilan `CarteraNoConoceContabilidad` (en las dos direcciones), `LosHechosDeCarteraNoLlevanCuentas` y
`LaCarteraSoloSeTocaPorSusInterfaces` (T51, T59, T90).

- **Cartera no depende de `*.Accounting*`** salvo `Application.Common.Integration.Accounting`, e
  `IContabilidadParaCartera` declara **exactamente** los cinco métodos de §16.1. Cartera no lee tablas `ACC_`, no
  llama a `AccountingPoster`, no elige el tipo de comprobante ni fecha el comprobante: sólo calcula la **fecha
  contable de su hecho** (§8), que Contabilidad respeta.
- **`Application/Accounting/Lending` no depende** de `Domain.Entities.Lending` ni de `Application.Lending`. Lee
  los hechos por `IMensajesEntrantes`; los códigos de Cartera (líneas, modalidades, sub-segmentos, conceptos, clases
  de recaudador y combinaciones en uso), por `IDimensionesDeCartera`; y de Core y de la plataforma, directamente,
  `COR_Branches`, `COR_CostCenters`, `COR_People`, `COR_IntegrationParticipants` y la bandeja.
- **Ningún hecho lleva una cuenta** (FR-126): ni código, ni Id, ni nombre, en ningún record de
  `Contracts/Lending`. La prueba recorre por reflexión los records del catálogo de Cartera y rechaza toda propiedad
  `Account*` o `Cuenta*`.
- **Las dimensiones de Cartera van en la matriz por código**, sin llaves a `LND_`; sólo cuenta, sucursal y centro
  de costo llevan llave `Restrict`. Consecuencia para Cartera: el código de una línea no cambia una vez creado (ya
  lo exige `CodigoDeCatalogo`), y modalidades y sub-segmentos son los códigos del documento nacional
  `CatalogoDeModalidades` (T44).
- Los comandos de consumo no tienen ruta ni piden permisos (`LosComandosDeConsumoNoTienenRuta`).
- Las cuentas que Contabilidad devuelve en la conciliación (§14) son **dato para mostrar**: Cartera nunca las
  guarda ni decide con ellas.

## 2. Hechos y roles

### 2.1 Catálogo de hechos (`OperacionesDeCartera`)

Datos fijos en código, uno por tipo de hecho de §2.11 de las transversales. El código de operación de la matriz
**es el tipo del hecho** (`Type` del mensaje). Clase: **B** = de negocio, con comprobante; **I** = informativo,
con recibo y sin comprobante. «Corto» es el código del tipo de hecho que va en la `ScheduleKey` (§10.2). El lado
es el del importe positivo; un importe negativo lo invierte (molde de la 012). El detalle de cada rol está en §2.2
y los asientos de ejemplo, en §24.

| Hecho (`Type`) | Corto | Clase | Débito | Crédito | Modo por defecto | Comp. | E |
|---|---|---|---|---|---|---|---|
| `CreditoDesembolsado` | DES | B | `Capital` | `DesembolsoPorGirar` (transferencia, cheque o giro a tercero); en especie, `PuenteComercial` (venta sellada «Cartera») o `CuentaPorCobrarComercial` (sellada «Contabilidad», FR-056) | EnLinea | DS | K1 |
| `DesembolsoReversado` | DRV | B | lo que acreditó el desembolso; `Ingreso`, `SeguroPorPagar` y `ContraOrdenContingente` por la causación que se anula | `Capital`; `InteresPorCobrar`, `PagoPorCuentaDelAsociado` y `SuspensoEnOrden` por la causación que se anula | hereda del desembolso (§10.1) | DS | K1 |
| `CreditoAjustadoDesdeComercial` | ACO | B | nota débito: `Capital`. Nota crédito, devolución o anulación: el rol de Comercial que selló la venta; `Ingreso` por el interés que se reversa | nota débito: el rol de Comercial. Las demás: `Capital`; `InteresPorCobrar` | EnLinea | DS | K1 |
| `InteresCausado` | ICA | B | `InteresPorCobrar` (`CurrentInterest`); suspendido: `SuspensoEnOrden`; castigado: `CastigadaEnOrden` | `Ingreso`; suspendido: `ContraOrdenContingente`; castigado: `ContraOrdenControl` | PorLotes resumido, `FinDeCausacion` | CA | K1 |
| `MoraCausada` | MCA | B | ídem, con `DefaultInterest` | ídem | ídem | CA | K1 |
| `SeguroCausado` | SCA | B | `PagoPorCuentaDelAsociado` (`Insurance`) | `SeguroPorPagar` | ídem | CA | K1 |
| `AjusteDeCausacionPorFechaValor` | AFV | B | los roles de `InteresCausado`, `MoraCausada` o `SeguroCausado`, con signo | ídem | ídem | CA | K1 |
| `CorreccionMonetariaUvrCausada` | CMU | B | si la UVR sube: el rol de saldo del concepto (`Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado`, `CargoPorCobrar`; suspendido o castigado, el de orden como en `InteresCausado`); si baja, al revés | `Ingreso` con el hecho fijado en la regla (`CMU`); en un `Anticipo` o `SaldoAFavor` del deudor, el signo se invierte | PorLotes resumido, `FinDeCausacion` | CA | K6b (E12) |
| `PagoRedistribuidoPorValorNacional` | PRN | B | positivo: el rol de saldo del concepto que el pago cubre de menos (`Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado`, `CargoPorCobrar`); el suspenso, `Ingreso` y `SuspensoEnOrden`; en un castigado, `IngresoPorRecuperacion` y `CastigadaEnOrden` | positivo: `Anticipo` o `SaldoAFavor` que el pago deja de más; las contras de orden. Negativo, al revés; sin el puente del recaudo | PorLotes resumido, `FinDeCausacion` | CA | K6b |
| `PagoAplicado` | PAP | B | `RecaudoPorAplicar` (clase del recaudador); sus `reallocations`, como `PRN` (§31) | por concepto: `Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado`, `CargoPorCobrar`; sus `reallocations`, como `PRN` (§31) | EnLinea | RC; DN si `Payroll` | K1 |
| `PagoReversado` | PRV | B | lo que acreditó el pago, con las dimensiones vigentes (§9); sus `reallocations`, como `PRN` (§31) | `RecaudoPorAplicar`; sus `reallocations`, como `PRN` (§31) | hereda del pago | RC; DN si `Payroll` | K1 |
| `AnticipoRecibido` | ANR | B | `RecaudoPorAplicar` | `Anticipo` | EnLinea (unidad del pago) | RC | K1 |
| `AnticipoAplicado` | ANA | B | `Anticipo` | los conceptos de la cuota cubierta | EnLinea | RC | K1 |
| `CobroDeInteresEnSuspenso` | CIS | B | `RecaudoPorAplicar`; `ContraOrdenContingente` | `Ingreso` (concepto en suspenso); `SuspensoEnOrden` | EnLinea (unidad del pago) | RC | K1 |
| `SaldoAFavorReconocido` | SFR | B | según su origen (`cause`, C93): `RecaudoPorAplicar` (`NoPrepaymentPreference`, abono sin preferencia), el rol de Comercial (`CommercialAdjustment`, ajuste que excede lo adeudado) o `Ingreso` (`NationalValueCorrection`, corrección de usura de un interés ya pagado) | `SaldoAFavor` | EnLinea | RC | K1 |
| `SaldoAFavorResuelto` | SFS | B | `SaldoAFavor` o `Anticipo` | `RecaudoPorAplicar` (`RefundThroughCollector` o `ApplyToOtherObligation`); los conceptos (`ApplyAsExtraordinaryPrincipal`) | EnLinea | RC | K1 |
| `RecuperacionDeCastigada` | RCA | B | `RecaudoPorAplicar`; `ContraOrdenControl` | `IngresoPorRecuperacion`; `CastigadaEnOrden` | EnLinea (unidad del pago) | RC; DN si `Payroll` | K1 |
| `DimensionContableCambiada` | DCC | B | cada rol con la combinación nueva | el mismo rol con la anterior (al revés en los de naturaleza crédito) | EnLinea si la emite una operación; PorLotes resumido, `FinDeCausacion`, si la emite el proceso | CN | K1 |
| `CobroProvisionalIncorporado` | CPI | B | `CuentaPorCobrarComercial` | los conceptos | EnLinea | RC | K1 |
| `DeterioroCalculado` | DET | B | aumento: `GastoDeterioro`; disminución y traslado: el deterioro que baja | aumento: `DeterioroIndividual` o `DeterioroGeneral`; disminución: `GastoDeterioro` (mismo ejercicio) o `RecuperacionDeterioro`; traslado: el deterioro que sube | PorLotes resumido, `CierreDeCartera` | PV | K1 |
| `CarteraReclasificadaPorCalificacion` | RCL | B | cada rol con la categoría nueva | el mismo rol con la anterior (al revés en `DeterioroIndividual`) | ídem | PV | K1 |
| `ReversoDeCierre` | RCI | B | el espejo de la parte del cierre que revierte, con las dimensiones vigentes | ídem | hereda de la parte del cierre | PV | K1 |
| `GastoDeCobranzaCausado` | GCO | B | propio: `CargoPorCobrar`; por cuenta del asociado: `PagoPorCuentaDelAsociado` | propio: `Ingreso`; por cuenta del asociado: `CuentaPorPagarATercero` | EnLinea | CA | K4 |
| `CreditoReestructurado` | REE | B | cada rol con `Restructured = true`; capitalización: `Capital`; lo suspendido capitalizado: también `ContraOrdenContingente` | el mismo rol con la combinación anterior; capitalización: `InteresPorCobrar` (lo causado) o `IngresoDiferido` (lo suspendido); lo suspendido capitalizado: también `SuspensoEnOrden` (se cancela su orden, pendiente de validar) | EnLinea | CN | K3 |
| `CreditoModificado` | MOD | B | como `DimensionContableCambiada`, si cambió alguna dimensión | ídem | EnLinea | CN | K3 |
| `CreditoNovado` | NOV | B | los roles del crédito nuevo (hereda categoría y marca) | los mismos roles del crédito anterior; capitalización como `CreditoReestructurado` | EnLinea | CN | K3 |
| `CondonacionAprobada` | CON | B | `DeterioroIndividual` hasta su saldo y `GastoDeCastigoOCondonacion` (`Waiver`) por el resto; suspenso: `ContraOrdenContingente` | el concepto condonado; suspenso: `SuspensoEnOrden` | EnLinea | CN | K3 |
| `CreditoCastigado` | CAS | B | `DeterioroIndividual` hasta su saldo y `GastoDeCastigoOCondonacion` (`WriteOff`) por el resto; `CastigadaEnOrden` | los conceptos del activo; `ContraOrdenControl`; el suspenso sale de `SuspensoEnOrden` (§2.2) | EnLinea | PV | K3 |
| `CausacionSuspendida` · `CausacionReanudada` | CSU · CRE | I | — | — | Always | — | K1 |
| `CarteraActivada` · `CargaDeCarteraAplicada` · `CierreDeCarteraRealizado` · `CierreDeCarteraReabierto` | ACT · CAP · CIR · CRA | I | — | — | Always | — | K1 |
| `CreditoAprobadoSinDesembolsar` · `CupoRotativoOtorgado` · `GarantiaConstituida` (sujetos a A3, §19; C15) | CAD · CRO · GCT | B | `ContraContingenteAcreedora` | `ContingenteAcreedora` | NoPasa | CN | K1 |

Reglas que valen para todos:

- **Cada hecho cuadra solo**: sus líneas suman lo mismo al débito y al crédito. El constructor lo verifica
  (`Accounting.LendingFact.Unbalanced`, defecto del emisor).
- **Unidad** (T12, C01): **una operación y su modo**. Los acompañantes de un hecho principal forman una lista fija por
  tipo (`mensajes.md`) y **sellan el modo del principal**: `Contabilidad.ModoDePaso` no se ofrece en el ámbito
  `LendingFactType` para un acompañante. Comparten `originEventKey` y salen en **un** comprobante: Cartera no conoce
  tipos de comprobante, y Contabilidad resuelve el de la unidad **a partir del tipo del hecho principal** (§4.2). No
  hay clave adicional por grupo de modo y comprobante. `PagoAplicado` + `AnticipoRecibido` +
  `CobroDeInteresEnSuspenso` + el `SaldoAFavorReconocido` del abono sin preferencia salen en un RC (C93); `CreditoAjustadoDesdeComercial` + el `SaldoAFavorReconocido` que deja,
  en un DS. El `AjusteDeCausacionPorFechaValor` que provoca una operación fechada atrás es acompañante de esa
  operación (C03); el que emite la corrida `NationalValueCorrection` va por partes y por lotes. El mapeo de un hecho
  que viaja en la unidad de otro no se usa.
- **Masivos por partes** (T14): `InteresCausado`, `MoraCausada`, `SeguroCausado`, `CorreccionMonetariaUvrCausada` (K6b),
  `PagoRedistribuidoPorValorNacional` (K6b), `DeterioroCalculado`,
  `CarteraReclasificadaPorCalificacion` y `ReversoDeCierre` llegan por partes (fecha o cierre × sucursal × tipo ×
  tanda), con totales por combinación y **la lista por crédito**. El constructor arma las líneas desde la lista por
  crédito: así una cuenta que exige tercero o cruce conserva el detalle (§10.4).
- **Proceso como origen** (T11): admiten que los origine el proceso los masivos, `CausacionSuspendida`,
  `CausacionReanudada`, `AnticipoAplicado`, `DimensionContableCambiada`, `AjusteDeCausacionPorFechaValor`,
  `SaldoAFavorReconocido`, `CierreDeCarteraRealizado`, `CierreDeCarteraReabierto` y, si se aprueba A3, los tres de
  §19 (`AdmiteProceso = true`, C15). Contabilidad no los trata distinto: el recibo guarda `OriginActorKind`, y el
  «registrado por» de su comprobante es `Actor.NombreDelProceso` («Proceso de integración», C23).
- **Capitalización** (`CreditoReestructurado`, `CreditoModificado`, `CreditoNovado`; nota del 2026-10-13). Lo causado que
  se capitaliza sale de `InteresPorCobrar` y lo suspendido va contra `IngresoDiferido`; **además se cancela la orden del
  suspenso capitalizado**: débito `ContraOrdenContingente`, crédito `SuspensoEnOrden` por el mismo importe, porque ese
  interés deja de estar en suspenso al volverse capital (sin esto, `SuspensoEnOrden` quedaría con un saldo que Cartera ya
  no tiene y la conciliación no cuadraría). Así lo construye `OperacionesDeCartera` (bloque A2, T0646; caso dorado 31).
  La cancelación es una **propuesta pendiente de validar por la contadora** (C7, como `IngresoDiferido`); `mensajes.md`
  §10.20 se alineó con esta regla.

### 2.2 Roles (`RolesDeCuentaDeCartera`)

Datos fijos en código con su naturaleza, sus dimensiones y su tercero natural. **Exigidas**: se comparan por
igualdad exacta y la regla nunca las deja en `*`. **Opcionales**: casan por igualdad o con `*` y suman peso (§3.3).
Una dimensión que el rol no admite va en `*` en la regla y el valor del hecho se ignora. La última columna es
**orientación** sobre el CUIF sembrado: la cuenta la elige la contadora en la matriz.

Abreviaturas. Exigidas: Mod = `ModalityCode`, Cat = `RiskCategory`, Con = `ConceptCode`, Cla = `CollectorClass`,
Mot = `ReasonCode`. Opcionales (peso): Lín = línea (512), Emp = empleado (256), Gra = gracia (128), Sub =
sub-segmento (64), Ree = reestructurado (32), PCA = por cuenta del asociado (16), Suc = sucursal (4), Op =
operación (2, sólo roles de resultado).

| Rol | Nat. | Familia | Exigidas | Opcionales | Tercero natural | Cruce | Orientación en el CUIF sembrado |
|---|---|---|---|---|---|---|---|
| `Capital` | D | Saldo | Mod, Cat | Lín, Emp, Sub, Ree, Suc | deudor | crédito | 1441, 1442, 1448, 1455, 1462, 1469, 1476, por categoría |
| `InteresPorCobrar` | D | Saldo | Mod, Cat, Con (`CurrentInterest`, `DefaultInterest`) | Lín, Emp, Gra, Sub, Ree, Suc | deudor | crédito | 1443, 1449, 1456, 1463, 1470, 1477; «con periodos de gracia» (p. ej. 144330) |
| `PagoPorCuentaDelAsociado` | D | Saldo | Mod, Cat, Con (`Insurance`, `CollectionFee`, `OtherCharge`) | Lín, Emp, Gra, Sub, Ree, Suc | deudor | crédito | 1444, 1450, 1457, 1464, 1474, 1478 |
| `CargoPorCobrar` | D | Saldo | Con (`CollectionFee`, `OtherCharge`) | Lín, Sub, Suc | deudor | crédito | otras cuentas por cobrar |
| `DeterioroIndividual` | C | Saldo | Mod, Cat, Con (los del activo) | Lín, Emp, Gra, Sub, Ree, Suc | deudor | crédito | 1445–1447, 1451–1453, 1458–1460, 1465–1467, 1471, 1472, 1475, 1479–1481 |
| `DeterioroGeneral` | C | Saldo | Mot (`General`, `Additional`) | Suc | — | — | 146805, 146810 |
| `SuspensoEnOrden` | D | Orden | Mod, Cat, Con (`CurrentInterestInSuspense`, `DefaultInterestInSuspense`) | Lín, Emp, Sub, Ree, Suc | deudor | crédito | 8115 (A y B sólo de reestructurados), 8116 |
| `ContraOrdenContingente` | C | Orden | — | Suc | — | — | 86 |
| `CastigadaEnOrden` | D | Orden | Con | Lín, Sub, Suc | deudor | crédito | 831015 |
| `ContraOrdenControl` | C | Orden | — | Suc | — | — | 88 |
| `Anticipo` | C | Pasivo con el deudor | — | Lín, Suc | deudor | crédito | el pasivo que fije la contadora |
| `SaldoAFavor` | C | Pasivo con el deudor | — | Lín, Suc | deudor | crédito | ídem |
| `SeguroPorPagar` | C | Pasivo | — | Lín, Suc | aseguradora | — | cuenta por pagar a la aseguradora |
| `CuentaPorPagarATercero` | C | Pasivo | — | Suc | el tercero del cargo | — | cuenta por pagar |
| `RecaudoPorAplicar` | D | Puente | Cla | Suc | pagador | conviene que no lo exija | un puente por recaudador (§17) |
| `DesembolsoPorGirar` | C | Puente | Mot (`Transfer`, `Check`, `ThirdPartyPayment`) | Lín, Suc | beneficiario | conviene que no lo exija | puente con Tesorería |
| `PuenteComercial` | C | Puente | — | Lín, Suc | deudor | `FV` + factura | el puente que usó la venta (FR-056) |
| `CuentaPorCobrarComercial` | C | Puente | — | Lín, Suc | deudor | `FV` + factura | la cuenta por cobrar provisional de la 012 |
| `Ingreso` | C | Resultado | Mod, Con | Op, Lín, Emp, Gra, Sub, Ree, PCA, Suc | deudor, si la cuenta lo exige | — | 4150 por modalidad, gracia y mora |
| `GastoDeterioro` | D | Resultado | Mot (`Individual`, `General`, `Additional`); con `Individual`, también Mod y Con | Op, Lín, Emp, Gra, Sub, Suc | — | — | 5115 por modalidad y concepto; 511529 general; 511563 adicional |
| `RecuperacionDeterioro` | C | Resultado | ídem | ídem | — | — | 4225 por modalidad y concepto; 422524 general |
| `GastoDeCastigoOCondonacion` | D | Resultado | Mot (`WriteOff`, `Waiver`), Con | Op, Lín, Sub, PCA, Suc | deudor, si la cuenta lo exige | — | el gasto que fije la contadora (C7) |
| `IngresoPorRecuperacion` | C | Resultado | — | Op, Lín, Sub, Suc | deudor, si la cuenta lo exige | — | 422546 |
| `IngresoDiferido` | C | Diferido | Mod | Lín, Suc | deudor | crédito | el diferido que fije la contadora (C7) |
| `ContingenteAcreedora` | C | Contingente (A3) | Mot (`ApprovedNotDisbursed`, `RevolvingLimit`, `CollateralReceived`) | Lín, Suc | deudor | crédito | 9115, 913005, 9110 |
| `ContraContingenteAcreedora` | D | Contingente (A3) | Mot | Suc | — | — | 9605 |

Los nombres de los dos contingentes siguen el CUIF (C91, que reemplaza los de C15): 91xx y 913005 son acreedoras
contingentes, de naturaleza **crédito**, y 9605 es su contra, de naturaleza **débito**.

**De concepto a rol.** El constructor elige el rol de saldo por el concepto de cada importe (`LoanConcept`):

| Concepto | Rol |
|---|---|
| `Principal` | `Capital` |
| `CurrentInterest`, `DefaultInterest` | `InteresPorCobrar` |
| `CurrentInterestInSuspense`, `DefaultInterestInSuspense` | `SuspensoEnOrden` (con `ContraOrdenContingente`); en un crédito castigado, `CastigadaEnOrden` (con `ContraOrdenControl`) |
| `Insurance` | `PagoPorCuentaDelAsociado` |
| `CollectionFee`, `OtherCharge` | `PagoPorCuentaDelAsociado` si el importe viene «por cuenta del asociado»; si no, `CargoPorCobrar` |
| `Advance` | `Anticipo` |
| `CreditBalance` | `SaldoAFavor` |

Precisiones:

- **Naturaleza crédito.** En los roles de naturaleza crédito (`DeterioroIndividual`, `DeterioroGeneral`, pasivos y
  puentes de crédito), mover un saldo de una combinación a otra es débito a la anterior y crédito a la nueva.
- **Castigo y suspenso** (FR-096, T30). Después del castigo, el interés en suspenso se lleva en control:
  `CastigadaEnOrden` contra `ContraOrdenControl`. Al castigar, el saldo de `SuspensoEnOrden` del crédito sale
  (débito a `ContraOrdenContingente`, crédito a `SuspensoEnOrden`) y entra a control (débito a `CastigadaEnOrden`
  con el concepto en suspenso, crédito a `ContraOrdenControl`). Lo fija C81; es una propuesta «pendiente de
  validar» por la contadora (C7).
- **Puentes sin cruce.** `RecaudoPorAplicar` y `DesembolsoPorGirar` conviene que no exijan documento cruce: el otro
  lado (Recaudo, Nómina, Tesorería) contabiliza con su propio documento y no saldarían por documento. La completitud
  lo avisa (§3.7).

### 2.3 Lo que el constructor lee de cada hecho

El contenido exacto lo fija `mensajes.md` (bloques `LoanRefV1`, `LoanFactDimensionsV1`, `ConceptAmountV1`,
`OriginalFactRefV1`, `ExternalDocumentRefV1`, `LoanFactLineV1`). Esto es lo que este contrato **necesita** de él;
ninguno de estos datos es una cuenta.

| Dato | En qué hechos | Para qué |
|---|---|---|
| Sobre: `OperationDate` (fecha contable), `BranchPublicId`, `PersonPublicId`, `Related`, `OriginUser`, `OriginActorKind`, `Currency` | todos | Fecha, sucursal y tercero del comprobante; dependencia del original; usuario de origen como dato |
| Crédito: `PublicId` y número | todos los de un crédito; en las partes, en cada línea por crédito | Documento cruce `PG` y tercero |
| Dimensiones del crédito a la fecha contable: línea, modalidad, sub-segmento, categoría contable, empleado, gracia, reestructurado, castigado | todos los de un crédito | Resolver la matriz (§3.3) |
| Importes por concepto con signo, y si el importe de un cargo es «por cuenta del asociado» | todos los de negocio | Rol por concepto (§2.2) |
| Combinación **anterior y nueva** (con su sucursal) de cada saldo que se mueve | `DimensionContableCambiada`, `CarteraReclasificadaPorCalificacion`, `CreditoReestructurado`, `CreditoModificado`, `CreditoNovado`, `ReversoDeCierre` | Las dos puntas del traslado |
| Clase de recaudador y pagador | `PagoAplicado`, `PagoReversado`, `AnticipoRecibido`, `CobroDeInteresEnSuspenso`, `RecuperacionDeCastigada`, `SaldoAFavorResuelto` | `RecaudoPorAplicar` y el mapeo DN |
| Forma del desembolso, beneficiario, quién registró la cuenta por cobrar (`Cartera` o `Contabilidad`) y la factura | `CreditoDesembolsado`, `DesembolsoReversado`, `CreditoAjustadoDesdeComercial`, `CobroProvisionalIncorporado` | `DesembolsoPorGirar` con su motivo, o el rol de Comercial con cruce `FV` |
| Movimiento del deterioro en el `reasonCode` (`Increase`, `DecreaseSameYear`, `DecreasePriorYears`, `Transfer`) y nivel según la línea (`Individual` en las líneas por crédito; `General` o `Additional` en el `kind` de `GeneralImpairmentLineV1`) (C93) | `DeterioroCalculado`, `ReversoDeCierre` | Gasto, recuperación o traslado (§11) |
| Origen del saldo a favor (`cause`: `NoPrepaymentPreference`, `CommercialAdjustment`, `NationalValueCorrection`) y forma de su resolución (`resolution`, `FormaDeResolucion`: `RefundThroughCollector`, `ApplyToOtherObligation`, `ApplyAsExtraordinaryPrincipal`) (C93) | `SaldoAFavorReconocido`, `SaldoAFavorResuelto` | La contrapartida |
| Cuánto de cada concepto sale contra el deterioro y cuánto contra el gasto | `CondonacionAprobada`, `CreditoCastigado` | Cartera conoce el deterioro de cada crédito; Contabilidad no |
| Tipo de contingencia y su variación con signo | los tres de §19 | `ContingenteAcreedora` y `ContraContingenteAcreedora` |

## 3. La matriz de Cartera (`ACC_LendingPostingRules`)

Parametrizable sin programar, con vigencia y especificidad (FR-129). Una sola tabla con `Role`: un motor de
resolución, una plantilla, una consulta de completitud y un solo punto de validación, como en la 012. Reemplaza las
cuentas como texto de `LND_CreditLineParameters`, `LND_PortfolioAccounts` y `LND_ProvisionParameters`, que se
retiran (§20).

### 3.1 Una regla

| Columna | Qué es |
|---|---|
| `Operation` | Código del hecho (§2.1) o `*`. En los roles de saldo, orden, pasivo, puente, diferido y contingentes, **siempre `*`** (§3.3). |
| `Role` | Código de `RolesDeCuentaDeCartera` (§2.2). |
| `CreditLineCode` | Código de la línea (`LND_CreditLines`), o nulo (= cualquiera). |
| `ModalityCode` · `SubsegmentCode` | Códigos del `CatalogoDeModalidades` vigente (`CONSUMO`, `ConLibranza`…), o nulos. |
| `RiskCategory` | `A`, `B`, `C`, `D` o `E`, o nulo. Las subcategorías van a su letra (E1 y E2 → E). |
| `ConceptCode` | Valor de `LoanConcept` como texto, o nulo. |
| `IsEmployee` · `InGracePeriod` · `OnBehalfOfAssociate` · `Restructured` | `bool?`; nulo = cualquiera. |
| `CollectorClass` | Valor de `ClaseDeRecaudador` como texto (`Receipts`, `Payroll`, `Contributions`, `OwnLoanPayoff`, `Test`), o nulo. |
| `ReasonCode` | Motivo del rol (§2.2), o nulo. |
| `BranchId` | Sucursal (`COR_Branches`, `Restrict`), o nula. Se compara con la sucursal del hecho. |
| `CostCenterId` | Centro de costo (`COR_CostCenters`, `Restrict`), o nulo. **Se asigna a la línea, no se compara** (§3.3). |
| `AccountId` | Cuenta (`ACC_ChartOfAccounts`, `Restrict`). |
| `ValidFrom` · `ValidTo` | Vigencia; `ValidTo` nulo = abierta. |
| `Notes` | Quién decidió y por qué (la fuente de la contadora). |
| `DimensionKey` (300) | Clave normalizada de la regla, con `*` donde no fija valor. Índice único filtrado `(DimensionKey, ValidFrom)` entre no borradas, con `HasDatabaseName` (un único con columnas nulas se comporta distinto en los dos motores). |
| `SpecificityWeight` (`int`) | Suma de los pesos de las opcionales que la regla fija; se calcula al guardar. |

Formato de `DimensionKey` (los booleanos como `1`/`0`; la sucursal por su `Id`; el centro de costo **no** entra):

```text
{Operation}|{Role}|{CreditLineCode}|{ModalityCode}|{SubsegmentCode}|{RiskCategory}|{ConceptCode}|
{IsEmployee}|{InGracePeriod}|{OnBehalfOfAssociate}|{Restructured}|{CollectorClass}|{ReasonCode}|{BranchId}

*|Capital|*|CONSUMO|ConLibranza|A|*|0|*|*|*|*|*|*          capital de consumo con libranza, categoría A, no empleado
*|Capital|*|CONSUMO|*|A|*|1|*|*|*|*|*|*                    capital de créditos a empleados de consumo, categoría A
*|RecaudoPorAplicar|*|*|*|*|*|*|*|*|*|Payroll|*|*          puente de los descuentos de nómina
```

Entidad `LendingPostingRule`: `AuditableEntity`, `PublicId` y filtro de borrado lógico. Nada se borra: una regla
vieja hace falta para explicar lo ya contabilizado.

### 3.2 Valores admitidos

| Dimensión | Valores | Fuente y validación |
|---|---|---|
| Hecho | Los de §2.1, o `*` | `OperacionesDeCartera` |
| Línea | Códigos de líneas (también cerradas) | `IDimensionesDeCartera.CatalogoAsync` |
| Modalidad · sub-segmento | Códigos del `CatalogoDeModalidades` vigente; el sub-segmento debe ser de la modalidad | ídem (Cartera los toma del documento nacional, T44) |
| Categoría | `A`–`E` | Fija |
| Concepto | Los de `LoanConcept` que admite el rol | `RolesDeCuentaDeCartera` |
| Clase de recaudador | `Receipts`, `Payroll`, `Contributions`, `OwnLoanPayoff`, `Test` | `ClaseDeRecaudador` (§2.7 de las transversales) |
| Motivo | `Transfer`, `Check`, `ThirdPartyPayment` (`DesembolsoPorGirar`); `General`, `Additional` (`DeterioroGeneral`); `Individual`, `General`, `Additional` (`GastoDeterioro`, `RecuperacionDeterioro`); `WriteOff`, `Waiver` (`GastoDeCastigoOCondonacion`); `ApprovedNotDisbursed`, `RevolvingLimit`, `CollateralReceived` (contingentes) | `RolesDeCuentaDeCartera` |
| Booleanos | sí, no, cualquiera | — |
| Sucursal · centro | Activos | Core |

### 3.3 Resolución por especificidad (`ResolutorDeReglasDeCartera`, `MatrizVigenteDeCartera`)

`ResolutorDeReglasDeCartera.CargarAsync` carga en bloque las reglas de un rango de fechas;
`MatrizVigenteDeCartera.Resolver` es puro y es el mismo código en la validación previa, el consumo, la vista
previa, la conciliación y `ResolveLendingRuleQuery`. Para cada importe del hecho con **valor distinto de cero** (un
pago sin mora no pide regla de mora):

1. **Fecha de las reglas**: la fecha contable del hecho (`Origin.OperationDate`). También en los reversos, que se
   resuelven con la matriz vigente en **su** fecha (§9).
2. **Valores buscados**: el hecho; el rol (§2.2); línea, modalidad, sub-segmento, categoría contable, empleado,
   gracia y reestructurado del crédito (en las partes, los de cada línea por crédito); el concepto y si es «por
   cuenta del asociado»; la clase de recaudador; el motivo; la sucursal del hecho (en un cambio de oficina, la de
   cada punta). Los `PublicId` se traducen a `Id` de Core.
3. **Candidatas**: reglas no borradas del rol, vigentes en la fecha, con `Operation` igual al hecho o `*`, exigidas
   iguales y opcionales iguales o `*`.
4. **Gana la de mayor peso.** Pesos: línea 512, empleado 256, gracia 128, sub-segmento 64, reestructurado 32, por
   cuenta del asociado 16, sucursal 4 y, sólo en los roles de resultado, operación 2. No hay empate posible: los
   pesos son potencias de dos, así que dos candidatas con el mismo peso fijan las mismas dimensiones y, si ambas
   casan, tienen la misma `DimensionKey`, cuyas vigencias no se cruzan.
5. **Sin candidata**: `Accounting.LendingRule.Missing` con `data: { operation, role, values, date, loans }`.
6. **Centro de costo** (C77): Cartera no conoce centros de costo y sus hechos no los traen. La línea lleva el
   `CostCenterId` de la regla ganadora si la cuenta maneja centro; si la cuenta lo exige y la regla no lo fija, es
   el error de la regla 9 de la 009 (`Accounting.Line.CostCenterRequired`), que corrige la matriz.

**Por qué el hecho va en `*` en los roles de saldo** (C77: obligatorio en saldo, orden, pasivo, puente, diferido y
contingentes; opcional con peso 2 en los de resultado). Un saldo vive en una sola cuenta, sea cual sea el hecho que lo
mueve: si el capital de consumo A fuera a una cuenta al desembolsar y a otra al pagar, ningún auxiliar cuadraría
con Cartera. Además, la conciliación (§14) resuelve combinaciones de saldo, que no tienen hecho. En los roles de
resultado el hecho sí puede fijarse (peso 2), por ejemplo para separar el ingreso que se reversa en un ajuste de
Comercial; casi nunca hace falta, porque el concepto y el motivo ya separan.

### 3.4 La regla de categoría

- La categoría que lleva un hecho es la **categoría contable vigente** del crédito: la oficial del último cierre
  vigente de Cartera (mensual, o el cierre base de la activación). **Nunca** la calificación diaria, que es
  informativa (FR-099). Las subcategorías van a su letra.
- Un crédito que nace después del último cierre lleva la categoría con que nace, sellada en su
  `CreditoDesembolsado`: la que el motor de calificación da a un crédito nuevo (`RatingCause.NewLoan`). El crédito
  novado hereda la del anterior (FR-090) y el migrado, la del cierre base.
- La categoría contable **sólo cambia** con `CarteraReclasificadaPorCalificacion` (al cerrar) y con
  `ReversoDeCierre` (al reabrir). El arrastre, la calificación por análisis, la evaluación de cartera y la
  reestructuración llegan a la contabilidad por el cierre siguiente, no antes.
- Un reverso o un ajuste lleva la categoría vigente en **su** fecha contable, no la del original (§9).
- Las demás dimensiones cambian sólo con un hecho: la gracia, la libranza (sub-segmento), la oficina, la modalidad
  y el reestructurado por `DimensionContableCambiada` o por la novedad que las cambia; «deudor empleado» sale del
  **estado actual de la persona maestra** (C8): el ciclo diario detecta el cambio y emite
  `DimensionContableCambiada`.

Por qué: si la categoría saliera de la calificación diaria, el saldo contable cambiaría cada día sin un hecho, y la
conciliación por categoría (SC-006) no cuadraría nunca.

### 3.5 Vigencia y cambios

- **Crear** abre una clave nueva (`CreateLendingPostingRuleCommand`); **agregar versión** cierra la anterior la
  víspera (`AddLendingPostingRuleVersionCommand`); **desactivar** fija `ValidTo` con motivo
  (`DeactivateLendingPostingRuleCommand`). Nada se borra.
- Dos versiones de la misma clave no se cruzan: `Accounting.LendingRule.Overlaps`.
- **Sin retroactividad sobre lo contabilizado**: una versión nueva no puede empezar en o antes de la fecha contable
  más reciente ya contabilizada de un hecho que use ese rol y esa clave
  (`Accounting.LendingRule.RetroactiveOverPosted`, con esa fecha y el comprobante). Una regla **nueva** (clave que
  no existía) sí puede empezar antes si ningún hecho ya contabilizado desde su `ValidFrom` tiene un importe que case
  con ella; el resolutor lo comprueba sobre el contenido de esos hechos. Así se rescatan hechos rechazados por regla
  faltante sin cambiar el resultado de nada contabilizado. Un mapeo equivocado ya contabilizado se corrige con un
  comprobante manual `CG` de reclasificación.

### 3.6 Qué valida guardar una regla (`ReglasDeLaMatrizDeCartera`)

- La cuenta, con `AccountEligibility.Verificar(cuenta, ModuloContable.Cartera)`: de movimiento, activa y
  habilitada para CAR. Si no, `Accounting.Account.NotEligible`.
- Las exigidas del rol presentes; las que el rol no admite, en `*`; `Operation` en `*` en los roles que lo exigen.
  Si no, el validador responde 400.
- Los códigos existen: línea, modalidad y sub-segmento por `IDimensionesDeCartera`; concepto, clase de recaudador,
  motivo y categoría en sus catálogos fijos.
- **Aviso** (se guarda y la completitud lo lista): la naturaleza de la cuenta no es la del rol (p. ej. un
  `DeterioroIndividual` en una cuenta de naturaleza débito).

### 3.7 Plantilla, completitud y «¿a qué cuenta va?»

- **Plantilla**: `GET /api/accounting/lending/rules/template.xlsx` (`GetLendingRulesTemplateQuery`) y
  `POST /api/accounting/lending/rules/import?mode=review|apply` (`ImportLendingPostingRulesCommand`), con la
  mecánica común de `plantillas.md` (encabezados en la fila 1 e instrucciones en otra hoja, todo o nada,
  `ImportResultDto`, 422 `Import.Invalid` con fila y columna) y **las mismas reglas que crear una a una**, porque
  reutiliza las de §3.5 y §3.6. Columnas: `operacion`, `rol`, `linea`, `modalidad`, `subsegmento`, `categoria`,
  `concepto`, `empleado`, `gracia`, `porCuentaDelAsociado`, `reestructurado`, `claseDeRecaudador`, `motivo`,
  `sucursal`, `centroDeCosto`, `cuenta`, `vigenteDesde`, `vigenteHasta`, `notas` (booleanos `S`, `N` o vacío). Una
  fila con una clave que ya existe y una fecha posterior es una versión nueva. La plantilla descargada trae **filas
  sugeridas sin cuenta**: los roles por modalidad y categoría de las líneas activas, con la estructura del CUIF
  sembrado, marcadas «pendiente de validar». COOFLOPAL la diligencia con su contadora.
- **Completitud** (`GetLendingRulesCompletenessQuery`, `GET /api/accounting/lending/completeness?date=`). Recibe de
  `IDimensionesDeCartera` las combinaciones en uso y lee de la plataforma los recaudadores registrados. Devuelve:
  - combinaciones en uso y roles exigidos **sin regla vigente**;
  - clases de recaudador registradas sin `RecaudoPorAplicar` (`OwnLoanPayoff` siempre se pide: es la propia
    Cartera y no está en el registro, T91);
  - reglas cuya cuenta **dejó de ser elegible** (inactiva, de agrupación o sin CAR habilitado);
  - hechos sin tipo de comprobante mapeado, o mapeados a uno inactivo;
  - avisos: puentes que exigen documento cruce; cuentas de la causación resumida que exigen tercero o cruce (una
    línea por crédito y por día, C5); naturaleza de la cuenta distinta de la del rol.
- **«¿A qué cuenta va esta combinación?»** (`ResolveLendingRuleQuery`, `GET /api/accounting/lending/rules/resolve`):
  recibe hecho, rol, dimensiones y fecha, y responde la regla ganadora con su peso y las demás candidatas. Es la
  herramienta de la contadora para revisar la matriz.
- `GET /api/accounting/lending/rules/catalog`: los hechos, los roles con sus exigidas, opcionales y motivos, y los
  códigos de Cartera, para la pantalla y la plantilla.
- `ListInvalidParameterizationsQuery` (FR-017 de la 009) y `AccountReferenceFinder` miran la matriz de Cartera: una
  cuenta de la matriz no se elimina, e inactivarla avisa.

### 3.8 Pantallas y permisos

`/contabilidad/cartera/matriz` (pestañas por familia de rol: cartera e intereses; deterioro; orden y castigada;
recaudo y puentes; desembolso y Comercial; pasivos con el deudor; resultados; contingentes. Al elegir una cuenta
muestra sus reglas, FR-016 de la 009), `/contabilidad/cartera/tipos-de-comprobante`,
`/contabilidad/cartera/completitud`, `/contabilidad/cartera/lotes`, `/contabilidad/cartera/lotes/{Id:guid}` y
`/contabilidad/indicadores-regulatorios`; el aviso de Cartera en `/contabilidad/periodos` (§12). Permisos nuevos en
`AccountingPermissionCatalogSeeder`: `Accounting.LendingRules.View` y `.Manage` (matriz, mapeo, plantilla,
completitud, resolución), `Accounting.LendingBatches.View` y `.Run` (ver, previsualizar y ordenar lotes),
`Accounting.RegulatoryIndicators.View` y `.Manage` (§15). Todas las pantallas llevan `PermissionGate` e
`IndicadorDeCarga`, están en el menú y las vigila `LasPantallasDeCarteraEstanEnElMenu`.

## 4. Tablas del lado contable

Todas en la base de cada cooperativa, en la migración par aditiva **`IntegracionContableDeCartera`** (§2.26 de las
transversales; la última de K1).

### 4.1 `ACC_LendingPostingRules`

Ver §3.1.

### 4.2 `ACC_LendingVoucherMappings` · `LendingVoucherMapping`

| Columna | Qué es |
|---|---|
| `Operation` | El hecho (§2.1). |
| `CollectorClass` | Valor de `ClaseDeRecaudador`, o `*` (= toda la operación). |
| `MappingKey` | `{Operation}\|{CollectorClass}`; único entre las filas vivas. |
| `VoucherTypeId` | Tipo de comprobante (`Restrict`): `Usage = Module`, `ModuleCode = CAR`, activo. |
| `CrossDocumentTypeId` | Tipo de documento cruce por defecto (`Restrict`); `PG` en la semilla. |
| `Notes` | Por qué. |

Sin vigencia, como en la 012: un cambio de mapeo rige para lo que se contabilice después. Resolución: primero
`(hecho, clase)`, después `(hecho, *)`; sin fila, `Accounting.VoucherType.NotFound`; un tipo de otro módulo o
inactivo, `Accounting.VoucherType.NotAllowedForModule`. **El tipo de la unidad lo da su hecho principal** (§2.1).
Se administra con `SetLendingVoucherMappingCommand` (`GET` · `PUT /api/accounting/lending/voucher-mappings`).

### 4.3 `ACC_LendingPostings` · `LendingPosting`

Recibo de cada hecho procesado. Hecho inmutable (`AuditableEntityLong`, `IHechoInmutable`), en
`Domain/Entities/Accounting/Transactions` (así `PrincipioXI_ContableImmutable` lo cubre sin cambios). Una fila por
hecho, escrita en el **mismo** `SaveChanges` que el comprobante. Es también el vínculo comprobante ↔ hechos: en un
resumido, los hechos de un comprobante son las filas con su `AccountingDocumentId`.

| Columna | Qué es |
|---|---|
| `MessagePublicId` | Único **sin filtro**: una colisión es `AlreadyProcessed`. |
| `MessageType` · `MessageVersion` · `MessageKind` | Tipo, versión y clase del hecho. |
| `SourceModule` | `CAR`. |
| `SourcePublicId` | Lo que originó el hecho: la operación del crédito, la parte de una corrida o de un cierre, la orden de giro, la activación, la carga o el cierre. |
| `SourceDocumentClass` · `SourceDocumentTypeCode` · `SourceDocumentNumber` | El texto del sobre (`LoanDisbursement`, `PaymentApplication`…; el número de la operación). |
| `LoanPublicId` · `LoanNumber` | El crédito del hecho; nulos en las partes y en los informativos. |
| `RelatedMessagePublicId` | El hecho original: el pago de un reverso, el desembolso de un reverso o de un ajuste de Comercial, la parte del cierre de un reverso de cierre, el `CierreDeCarteraRealizado` de una reapertura. |
| `OperationDate` | La fecha contable, que es la del comprobante. |
| `AccountingDocumentId` | Nulo en informativos, valor cero o mismas cuentas. |
| `NoVoucherReason` | `Informational`, `ZeroValue` o `SameAccounts` (las dos puntas de un traslado resolvieron la misma cuenta, sucursal, tercero y cruce); nulo si hay comprobante. |
| `BatchPublicId` | El `COR_IntegrationBatches` por el que pasó, si pasó por uno. |
| `OriginActorKind` · `OriginUserCentralId` (nulo admitido) · `OriginUserName` | Quién originó el hecho: una persona o el proceso de la cooperativa (T11). Dato, nunca actor. |
| `ActorKind` · `ActorUserId` · `ActorName` | Quién contabilizó: `Actor.NombreDelProceso` («Proceso de integración») o la persona que ordenó el lote o el reproceso (§22). |
| `ProcessedAt` | Cuándo. |

### 4.4 `ACC_RegulatoryIndicators` · `RegulatoryIndicator`

Patrimonio técnico y relación de solvencia que registra el contador (T59, §15). Hecho de sólo inserción: corregir
es registrar otro del mismo corte, que reemplaza al anterior.

| Columna | Qué es |
|---|---|
| `IndicatorCode` | `TechnicalCapital` o `SolvencyRatio`. |
| `CutoffDate` | Corte al que corresponde. |
| `Value` (20,6) · `Unit` | Valor y unidad: `Pesos` en el patrimonio técnico, `Fraccion` en la solvencia. |
| `Source` | De dónde sale (p. ej. «formato de patrimonio técnico de diciembre de 2026»). |
| `SupportAttachmentPublicId` | Adjunto de soporte, tipo `RegulatoryIndicatorSupport` (con subida, `Accounting.RegulatoryIndicators.Manage`; no se borra una vez registrado el indicador). |
| `SupersedesPublicId` | El que corrige, si corrige uno del mismo corte. |
| `Notes` · `RegisteredAt` · `RegisteredBy` | Quién y cuándo. |

### 4.5 Semillas

- `voucher-types.json` gana `CN` «Novedades de cartera» (`Usage = Module`, `ModuleCode = CAR`). `VoucherTypesSeeder`
  registra en el log la colisión con un `CN` que la cooperativa ya tenga con otro uso, en vez de saltarla en
  silencio.
- `lending-voucher-mappings.json` y `LendingVoucherMappingsSeeder` (Order **89**; la 88 es de
  `NationalValuesReplicaSeeder`, CH36) dejan las filas de §5.
- No se siembra ninguna regla: una regla necesita cuenta. Lo sembrado son los catálogos fijos (en código) y las
  filas sugeridas de la plantilla (§3.7).
- Los seis permisos de §3.8 los siembra la API al arrancar, nunca el DbMigrator.

## 5. Tipos de comprobante y documento cruce (T54)

Se reutilizan los tipos que la 009 ya siembra para CAR y se agrega `CN`. Todo «pendiente de validar» por la
contadora (C6).

| Tipo | Uso para Cartera | Filas de la semilla (`Operation\|CollectorClass`) |
|---|---|---|
| `DS` | Desembolso, su reverso y los ajustes desde Comercial | `CreditoDesembolsado\|*`, `DesembolsoReversado\|*`, `CreditoAjustadoDesdeComercial\|*` |
| `RC` | Pagos, reversos, anticipos, saldos a favor, cobro de suspenso, recuperación de castigada, cobro provisional | `PagoAplicado\|*`, `PagoReversado\|*`, `AnticipoRecibido\|*`, `AnticipoAplicado\|*`, `CobroDeInteresEnSuspenso\|*`, `SaldoAFavorReconocido\|*`, `SaldoAFavorResuelto\|*`, `RecuperacionDeCastigada\|*`, `CobroProvisionalIncorporado\|*` |
| `DN` | Lo que recauda Nómina (la recuperación de castigadas incluida, C79) | `PagoAplicado\|Payroll`, `PagoReversado\|Payroll`, `RecuperacionDeCastigada\|Payroll` |
| `CA` | Interés, mora, seguro, gasto de cobranza, ajuste por fecha valor, corrección monetaria UVR y reparto de un pago corregido por la UVR (K6b) | `InteresCausado\|*`, `MoraCausada\|*`, `SeguroCausado\|*`, `AjusteDeCausacionPorFechaValor\|*`, `GastoDeCobranzaCausado\|*`, `CorreccionMonetariaUvrCausada\|*`, `PagoRedistribuidoPorValorNacional\|*` |
| `PV` | Deterioro, reclasificación por calificación, castigo y reverso de cierre | `DeterioroCalculado\|*`, `CarteraReclasificadaPorCalificacion\|*`, `ReversoDeCierre\|*`, `CreditoCastigado\|*` |
| `CN` (nuevo) | Novedades: reestructuración, modificación, novación, condonación, cambio de dimensión y contingentes | `DimensionContableCambiada\|*`, `CreditoReestructurado\|*`, `CreditoModificado\|*`, `CreditoNovado\|*`, `CondonacionAprobada\|*`, `CreditoAprobadoSinDesembolsar\|*`, `CupoRotativoOtorgado\|*`, `GarantiaConstituida\|*` |

**Documento cruce**, sólo en las líneas cuya cuenta lo exige:

1. Por defecto, el tipo del mapeo (`PG`) con el **número del crédito** (FR-126, FR-044). En una novación, cada
   punta lleva el número de su crédito.
2. `PuenteComercial` y `CuentaPorCobrarComercial` (los dos, C79) cruzan **siempre contra la factura** (`FV` + número): saldan lo
   que la venta registró con ese mismo cruce (012, §3.4), y así el saldo por tercero y documento queda en cero.
3. Los puentes de recaudo y de desembolso conviene que no lo exijan (§2.2). Si lo exigen, llevan `PG` con el número
   del crédito, y no saldarán contra el documento del recaudador.

Si la contadora objeta `PG` («pagaré») para un crédito nacido en Comercial, que no tiene pagaré, la alternativa es
un cruce nuevo `CR` «Crédito de cartera» (pregunta C6): cambia la semilla, no el contrato.

## 6. El destino `Accounting` con dos emisores (T15)

Inventario (`INV`) y Cartera (`CAR`) emiten al mismo destino `Accounting`. El despachador toma **un** consumidor
por destino, así que no hay un segundo consumidor: hay un enrutador.

- `DestinoContabilidad` sigue siendo el **único** `IDestinoDeMensajes` de `Accounting`. Si otro consumidor declara
  el mismo destino, la API no arranca (`Integration.Destination.DuplicateConsumer`, `UnSoloConsumidorPorDestino`).
- **`Acepta(type, version)`** busca el tipo en el catálogo agregado (`CatalogoDeMensajes`): si su `OriginModule` es
  `INV`, responde con las versiones aceptadas de Inventario; si es `CAR`, con las de Cartera (v1 de los tipos de
  §2.1). `VersionesAceptadas` pasa a ser por módulo.
- **`ConsumirAsync`** reparte por `UnidadDeConsumo.OriginModule` (campo nuevo, T15): `INV` va a
  `PostInventoryMessagesCommand` o `PostInventorySummaryGroupCommand`, sin cambios; `CAR`, a `PostLendingFactsCommand`
  (§7) o `PostLendingSummaryGroupCommand` (§10.4).
- **`PlanearLote`** separa por módulo antes de agrupar: Inventario con `AgrupadorDeResumidos`, Cartera con
  `AgrupadorDeResumidosDeCartera`. Un lote nunca mezcla los dos módulos (sus claves de horario y de alcance son
  distintas); el lote manual se pide por módulo (`OrderIntegrationBatchCommand` gana `OriginModule`).
- **`TotalesDelLoteAsync`** suma los recibos de los dos (`ACC_InventoryPostings` y `ACC_LendingPostings`);
  `GetLendingBatchTotalsQuery` da los de Cartera.
- **Pendientes por módulo**: la lógica de `PendingInventoryMessagesQuery` pasa a
  `ListPendingIntegrationMessagesQuery(módulo, desde, hasta)`. `PendingInventoryMessagesQuery` queda filtrando `INV`;
  el cierre contable consulta los dos módulos (§12). Sin este filtro, el aviso del cierre contaría los hechos de
  Cartera como pendientes de Inventario.
- `ScheduleIntegrationBatchesCommand` usa el respaldo de Inventario (sus tipos de documento y
  `ContabilidadHoraDeLote`) **sólo** con claves `INV`; las claves de Cartera traen la hora sellada (§10.2).

Prueba de que el enrutador no tocó a Inventario: las e2e «Inventario e2e» siguen verdes sin cambios.

## 7. De hecho a comprobante

### 7.1 La unidad y el comando

**Unidad de contabilización**: los hechos con el mismo `origin.publicId` y `originEventKey` con destino
Contabilidad → **un comprobante** (T12). Su tipo lo da el hecho principal (§2.1, §4.2).

`PostLendingFactsCommand(IReadOnlyList<Guid> MessagePublicIds, Guid? BatchPublicId)`, con
`IReintentableAnteConcurrencia` y validador hermano, sin ruta y sin permisos. Lo envía el despachador en un ámbito
DI propio por unidad. Dentro del handler (lo exige el reintento, que descarta el contexto entero):

1. Lee los hechos por `IMensajesEntrantes` (con `OriginDocumentClassText`, T10) y verifica que forman una unidad.
2. Versión aceptada (§6); si no, `Rejected` con `Integration.VersionNotAccepted`.
3. Moneda `COP` y tasa 1; si no, `Rejected` con `Accounting.LendingFact.CurrencyNotSupported`.
4. Si ya hay recibo de alguno: `AlreadyProcessed` con su referencia.
5. Si depende de un original (`Related`) cuya entrega a Contabilidad está `Pending`, `InBatch` o `Rejected`:
   `Retry` con `Accounting.LendingFact.WaitingForOriginal`. Un original `NotApplicable` no retiene: los reversos
   heredan el modo de su original (§10.1) y nunca llegan a ese caso.
6. Informativo: agrega su recibo (`Informational`) sin comprobante y termina (§7.4).
7. Arma el `PostingRequest` con `ConstructorDeLineasDeCartera` (§7.3) y verifica que cada hecho cuadra y que ningún
   importe tiene más de dos decimales; si no, `Rejected` con `Accounting.LendingFact.Unbalanced`.
8. Sin líneas distintas de cero: recibo `ZeroValue`. Si las líneas se anulan porque las dos puntas de un traslado
   resolvieron la misma cuenta, sucursal, tercero y cruce: recibo `SameAccounts`.
9. `AccountingPoster.PrepareAsync`: valida con las reglas 1 a 11, numera y **agrega sin guardar**. Un fallo es
   `Rejected` con el código de la 009 y sus errores por línea, traducidos a hecho, concepto y crédito, **salvo**
   `Accounting.Period.NotFound`, que es `Retry` y levanta la alerta `Contabilidad.PeriodoInexistente` (§8).
10. Agrega una fila de `ACC_LendingPostings` por hecho (§4.3).
11. **Un** `SaveChangesAsync`. Si choca contra el único de `MessagePublicId` (otra réplica ganó), la colisión se
    traduce a `AlreadyProcessed`.

El consumidor **no toca** tablas de la plataforma: el despachador, en otro ámbito, registra el resultado con
`RegisterDeliveryResultCommand`. `ResultadoDeConsumo.Processed` lleva el `PublicId`, el tipo y el número del
comprobante, que la plataforma guarda en la entrega (`ResultReference`, `ResultVoucherTypeCode`,
`ResultVoucherNumber`): así la bandeja y los hechos de Cartera (`/api/lending/accounting/facts`) muestran el
comprobante **sin leer ninguna tabla `ACC_`** (FR-008).

### 7.2 Resultados

| Situación | `ResultadoDeConsumo` | Código |
|---|---|---|
| Comprobante guardado | `Processed(PublicId del comprobante, código del tipo, número)` | — |
| Informativo, valor cero o mismas cuentas | `Processed(PublicId del recibo, nulo, nulo)` | — |
| Recibo existente o colisión del índice | `AlreadyProcessed(la misma referencia)` | — |
| Versión no aceptada | `Rejected` | `Integration.VersionNotAccepted` |
| Moneda o tasa distintas | `Rejected` | `Accounting.LendingFact.CurrencyNotSupported` |
| El hecho no cuadra o trae más de dos decimales | `Rejected` | `Accounting.LendingFact.Unbalanced` |
| Sin regla | `Rejected` | `Accounting.LendingRule.Missing` |
| Tipo de comprobante sin mapeo o de otro módulo | `Rejected` | `Accounting.VoucherType.NotFound` · `.NotAllowedForModule` |
| Reglas de la 009: contabilidad sin iniciar, período cerrado, cuenta, tercero, cruce, centro, base, cuadre | `Rejected` | `Accounting.NotInitialized`, `Accounting.Period.Closed`, `Accounting.Line.*`, `Accounting.Document.Unbalanced`, con `data.errors` |
| Período inexistente (p. ej. el 1 de enero sin abrir el ejercicio) | `Retry` y alerta `Contabilidad.PeriodoInexistente` | `Accounting.Period.NotFound` |
| Original aún sin contabilizar | `Retry` | `Accounting.LendingFact.WaitingForOriginal` |
| Excepción, base no disponible, concurrencia que sobrevive a los reintentos | `Retry` | — |

Un rechazo queda en la bandeja de Cartera (`/cartera/bandeja-de-mensajes`) con su motivo y quién corrige (§13.4), y
levanta `Integracion.MensajeRechazado` para los responsables de los dos lados. Se recupera corrigiendo la causa
(una regla, un mapeo, una cuenta) y reprocesando: `ReprocessMessagesCommand` por `/api/lending/messages/reprocess`
(`Lending.Messages.Reprocess`).

### 7.3 Reglas comunes del `PostingRequest`

- **`VoucherTypeCode`**: el del mapeo del hecho principal (§4.2).
- **`Date`**: `Origin.OperationDate`, que es la **fecha contable** que Cartera calculó (§8). Nunca la del lote ni la
  de hoy. Contabilidad no la corre: si su mes está cerrado, el hecho se rechaza.
- **`Description`**: «{clase} {número} · crédito {número}» (p. ej. «Aplicación de pago 000981 · crédito 000123»); en
  un resumido, «Lote {n} · {tipo} · {fecha} · {k} créditos».
- **`Origin`**: `AccountingOrigin("CAR", "LendingDocument", origin.publicId)` por unidad;
  `AccountingOrigin("CAR", "LendingPostingBatch", lote.PublicId)` en un resumido (`OrigenesDeCartera`).
  `EnlacesDeOrigen` (Application y Shared) gana los dos: el primero lleva al hecho en la bandeja de Cartera; el
  segundo, al lote en `/contabilidad/cartera/lotes/{Id:guid}`.
- **`Kind`**: siempre `Regular`, también en reversos y correcciones (§9).
- **`RegistradoPor`**: `UsuarioDeOrigen(originUser.CentralUserId, originUser.Name)` cuando el hecho lo originó una
  persona; nulo cuando lo originó el proceso y en un resumido, cuyos orígenes quedan en `ACC_LendingPostings`. Con
  nulo, el comprobante muestra como «registrado por» el nombre del actor que ya existe, `Actor.NombreDelProceso`
  («Proceso de integración», C23); no se crea un nombre nuevo.
- **Sucursal** de cada línea: la del hecho (`BranchPublicId` → `COR_Branches.Id`), que es la sucursal contable de la
  oficina del crédito (T13). En un cambio de oficina, cada punta lleva la suya; en las partes, la de la parte.
- **Centro de costo**: el de la regla ganadora, sólo si la cuenta lo maneja (§3.3).
- **Tercero**: el natural del rol (§2.2) en toda línea cuya cuenta lo exige; si el rol no tiene tercero natural, la
  persona del sobre; en las partes, el deudor de cada línea por crédito. Si la cuenta lo exige y no hay, error de la
  regla 7 de la 009.
- **Documento cruce**: §5.
- **`TaxBase`**: Cartera no lleva impuestos (si el interés de una línea de origen comercial es gravado, lo factura
  Comercial, D20). Una cuenta de la matriz que exija base gravable es un error de la regla 10 que corrige la matriz.
- **Importes**: dos decimales, como vienen; un negativo invierte el lado; se descartan los ceros. Las líneas con la
  misma cuenta, lado, sucursal, centro, tercero y documento cruce se suman.
- **Mapa de líneas**: cada línea contable guarda de qué hecho, concepto y crédito salió, para que un error diga
  qué corregir.

### 7.4 Hechos informativos

`CausacionSuspendida`, `CausacionReanudada`, `CarteraActivada`, `CargaDeCarteraAplicada`,
`CierreDeCarteraRealizado` y `CierreDeCarteraReabierto` dejan su recibo (`Informational`) sin validar reglas ni
generar comprobante. La suspensión no reclasifica lo causado antes (T30): el deterioro lo cubre al ciento por
ciento en el cierre (FR-103). Los cuatro últimos los lee el cierre contable (§12):

| Informativo | `OperationDate` | `Related` | Qué dice |
|---|---|---|---|
| `CarteraActivada` | la fecha de activación | — | Desde qué mes Contabilidad no cierra sin Cartera |
| `CargaDeCarteraAplicada` | la fecha de corte de la carga | — | La última reemplaza a las anteriores |
| `CierreDeCarteraRealizado` | el último día del mes cerrado | — | Mes y versión del cierre |
| `CierreDeCarteraReabierto` | el último día del mes reabierto | el `CierreDeCarteraRealizado` que reabre | Mes y versión |

## 8. Fecha contable (T36, FR-012)

Cartera la calcula **al emitir**, con `IContabilidadParaCartera.EstadoDelPeriodoAsync`, y la pone en
`Origin.OperationDate`. Contabilidad fecha el comprobante con ella y nunca la corre.

```text
mes ← mes de la fecha valor
mientras (Cartera tiene un cierre vigente de mes) o (Contabilidad tiene mes cerrado):
    mes ← mes siguiente
fecha contable ← fecha valor, si mes = mes de la fecha valor; si no, el primer día de mes
```

- Un mes que **no existe** en Contabilidad no se salta: la fecha contable queda en él y el consumidor reintenta
  (`Retry` con la alerta `Contabilidad.PeriodoInexistente`, para quienes tienen `Accounting.Periods.*`) hasta que se
  abra el ejercicio. Así la causación del 1 de enero no se rechaza por un vacío administrativo.
- La causación del día *d* se fecha *d* (el cierre de un mes exige la causación al día, así que *d* siempre cae en un
  mes sin cierre de Cartera).
- Los hechos del cierre del mes *M* y su reverso se fechan el último día de *M*; el cierre exige el período contable
  abierto (T47) y la reapertura sólo se admite antes del cierre contable (D12).
- Un pago con fecha valor de un mes que Cartera ya cerró, su `AjusteDeCausacionPorFechaValor` y un reverso de pago
  con su recálculo se fechan en el primer mes abierto (FR-078, FR-080): lo cerrado no se reescribe.
- `EstadoDelPeriodoAsync` es una lectura en proceso sobre la misma base: si falla, falla la operación como
  cualquier otra lectura (Principio IX), sin fecha supuesta; el proceso deja el crédito para la pasada siguiente.
  `Contabilidad.PoliticaSinRespuesta` es sólo de la validación previa (§13.2).
- Cartera no se activa sin el mes abierto en Contabilidad (FR-142), así que todo hecho de negocio nace con la
  contabilidad iniciada. El `NoPasa` por contabilidad sin iniciar (§10.1) queda como defensa; antes de activar sólo
  sale el informativo `CargaDeCarteraAplicada`, que no tiene comprobante.
- `PeriodClosed` en el consumidor (Contabilidad cerró el mes entre la emisión y el consumo) es `Rejected`. §12 lo
  impide para todo mes que Cartera no haya cerrado y avisa de lo pendiente en los demás; si pasa, se recupera
  reabriendo el mes según la 009 y reprocesando, con la fecha original.

## 9. Correcciones: comprobantes nuevos (T55)

- **Nada de Cartera usa `PrepareReversalAsync`.** La guarda del poster rechaza un original o un origen `CAR` con
  `Accounting.Document.LendingCorrectsWithNewVoucher`. `ReverseDocumentCommand` conserva
  `Accounting.Document.ModuleOwned` con un mensaje propio para Cartera: «se corrige en Cartera (reverso del pago, del
  desembolso o reapertura del cierre) y llega como comprobante nuevo»; en un resumido dice cuántos créditos reúne. Lo
  vigila `LoDeCarteraSeCorrigeConComprobanteNuevo`.
- Cada reverso (`DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre`) es un comprobante **nuevo**,
  `Kind = Regular`, de su propio hecho y **su propia fecha contable**, enlazado al original por
  `RelatedMessagePublicId`. El comprobante original nunca se marca reversado ni se toca.
- **Las líneas del reverso llevan las dimensiones vigentes** del saldo que restituyen en su fecha contable, y la
  regla se resuelve con la matriz vigente en esa fecha. Es la diferencia con el espejo de la 012: un pago de un
  crédito en A, reversado después de un cierre que lo pasó a C, restituye capital que hoy vive en C. Espejar A
  dejaría saldo en el auxiliar de A, que Cartera ya tiene en cero, y lo dejaría faltando en el de C: la conciliación
  por categoría (SC-006) no cuadraría (§24, ejemplo 5).
- En el libro de Cartera, el reverso es siempre fila espejo con `ReversesTransactionId` más las diferencias de la
  reconstrucción (T33). Este comprobante contabiliza esas filas; las diferencias de causación viajan como
  `AjusteDeCausacionPorFechaValor`, en su propio modo.
- Contabilidad nunca corrige por su cuenta un comprobante nacido de Cartera (C3). Un `CG` manual de
  reclasificación de la contadora sigue siendo posible, p. ej. si cambió la cuenta de un puente entre un pago y su
  reverso y quedó un residuo en la cuenta vieja.

## 10. Modo de paso por tipo de hecho, disparadores y lotes (T56, FR-128)

### 10.1 Sellado y defectos

Cada hecho **sella su modo al emitirse** (`ModoDeEntrega.Sellado`) y la entrega lo copia. `ModoDePasoDeCartera` lo
decide con los parámetros de Cartera (módulo `LND`, ámbito `LendingFactType` sobre `LND_FactTypes`, T43):

1. la vigencia de `Contabilidad.ModoDePaso` del tipo de hecho; si no hay,
2. la vigencia general (ámbito `None`); si no hay,
3. el defecto del tipo (tabla); pero **sin vigencia guardada y con la contabilidad sin iniciar, `NoPasa`**
   (`EstadoDelPeriodoAsync` lo dice). Un modo guardado a propósito se respeta: quien guarda «en línea» sin
   contabilidad iniciada recibe `Accounting.NotInitialized` en cada hecho, que queda en la bandeja.

| Tipos | Defecto (D8) |
|---|---|
| `InteresCausado`, `MoraCausada`, `SeguroCausado`, `AjusteDeCausacionPorFechaValor`, `CorreccionMonetariaUvrCausada` (K6b), `PagoRedistribuidoPorValorNacional` (K6b) | PorLotes, resumido, disparador `FinDeCausacion` |
| `DimensionContableCambiada` | sin vigencia propia: EnLinea si la emite una operación, PorLotes resumido `FinDeCausacion` si la emite el proceso; con vigencia propia, rige para los dos (`ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite`) |
| `DeterioroCalculado`, `CarteraReclasificadaPorCalificacion` | PorLotes, resumido, disparador `CierreDeCartera` |
| `DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre` | **heredan** el modo sellado de su original (`ModoDeEntrega.Heredado`, el mecanismo de la 012 para las anulaciones), con el alcance de lote de su propia operación |
| `CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida` | NoPasa (§19) |
| Acompañantes de un hecho principal (lista fija de `mensajes.md`) | **el del principal** (C01); el parámetro no se ofrece para ellos |
| Informativos | `Always` (lo fija el catálogo; no se parametrizan) |
| Todos los demás | EnLinea |

Que los reversos hereden (C08) garantiza que un reverso nunca pase sin su original ni quede esperando a uno que no
pasa.
Lo confirmado con `NoPasa` se envía después con «Enviar lo que no pasaba» (`SendNotApplicableMessagesCommand`,
`POST /api/lending/messages/send-not-applicable`, `Lending.Messages.SendNotApplicable`), con su fecha contable
original.

Parámetros (§2.20 de las transversales): `Contabilidad.ModoDePaso` (`EnLinea`, `PorLotes`, `NoPasa`);
`Contabilidad.Granularidad` (`PorHecho`, `PorDia`, `PorDiaYSucursal`; `PorHecho` lo suma C07 por FR-128);
`Contabilidad.DisparadorDeLote` (`Hora`, `FinDeCausacion`, `CierreDeCartera`); `Contabilidad.HoraDeLote`. Todos con
ámbito `LendingFactType`. La granularidad va en la `ScheduleKey` (§10.2); `COR_IntegrationBatches.Granularity` sigue
con el `PostingGranularity` de la plataforma (resumido).

### 10.2 Disparadores y lotes

Un lote es un `COR_IntegrationBatches` de la plataforma (Cartera lo ve sin leer `ACC_`).

| Disparador | `BatchTrigger` | `BatchScopeKey` | Quién crea el lote, y con qué |
|---|---|---|---|
| `FinDeCausacion` | `LendingAccrualRun = 7` | `AccrualRun:{aaaa-mm-dd}` | La corrida `DailyCycle` del día *d*, en el guardado con que termina: toma las entregas `InBatch` sin lote con alcance `AccrualRun:{x}`, *x* ≤ *d*. Las partes de la causación de *d* se sellan con `AccrualRun:{d}`; un hecho `FinDeCausacion` que nace fuera de esa corrida durante el día *d* (el ajuste por fecha valor de un pago, un cambio de dimensión de una operación, la corrida `NationalValueCorrection`) se sella con `AccrualRun:{d+1}` (el día siguiente al de su registro, C09), y así entra en el lote de la corrida siguiente, que recoge también los alcances anteriores sin lote. |
| `CierreDeCartera` | `LendingMonthlyClose = 8` | `LendingClose:{aaaa-mm}` | La transacción final del cierre (T47) y la de su reapertura: cada una toma las entregas `InBatch` sin lote de ese alcance (las del cierre, o los reversos de la reapertura). |
| `Hora` | `Scheduled` | — | `ScheduleIntegrationBatchesCommand` a la `Contabilidad.HoraDeLote` sellada (hora de Colombia). En la clave se sella el disparador `HoraDiaria` de la plataforma. |
| — | `Manual` | — | `OrderIntegrationBatchCommand` (`POST /api/accounting/lending/batches`, `Accounting.LendingBatches.Run`, motivo y `Idempotency-Key`, 202), con `OriginModule = CAR` y el `cutoffMessagePublicId` de su vista previa. |
| — | `Reprocess` · `SendNotApplicable` | — | `ReprocessMessagesCommand` y `SendNotApplicableMessagesCommand`, por las rutas de Cartera. |

`ScheduleKey` de Cartera (`ClavesDeLote.Horario`, 60 caracteres como máximo, C06):
`CAR.{ShortCode}|{disparador}|{hora}|{granularidad}`, con `LND_FactTypes.ShortCode` (3 a 4 letras, único; los de
§2.1). La hora va vacía en `FinDeCausacion` y `CierreDeCartera`; el disparador `Hora` del parámetro se sella como la
constante `HoraDiaria` de `ClavesDeLote` (012), para que `ScheduleIntegrationBatchesCommand` no cambie. Ejemplos: `CAR.ICA|FinDeCausacion||PorDia`, `CAR.DET|CierreDeCartera||PorDia`,
`CAR.PAP|HoraDiaria|02:00|PorHecho`. `ClavesDeLote` gana las constantes `FinDeCausacion` y `CierreDeCartera`.

Estados del lote, cierre, totales, número, alerta `Integracion.LoteNoCorrio` y `cutoffMessagePublicId` (traducido a
`CutoffMessageId`, que nunca sale por HTTP): los de la 012, sin cambios. Procesar otra vez un lote o un hecho no
crea nada (recibo único).

### 10.3 Por lotes, un comprobante por hecho

Con `Contabilidad.Granularidad = PorHecho`, `PlanearLote` devuelve una unidad por operación y el despachador envía un
`PostLendingFactsCommand(…, BatchPublicId)` por unidad, en pasadas que sólo toman lo que ya tiene sus dependencias
satisfechas. Cada comprobante lleva la fecha contable de su unidad.

### 10.4 Por lotes resumido: la causación diaria en un comprobante por día (FR-128, US7-3)

`AgrupadorDeResumidosDeCartera` arma los grupos con la clave `{fecha contable}|{tipo de comprobante}` («fecha
contable + tipo» de T56 es tipo de **comprobante**, C09) (granularidad
`PorDia`) o `{fecha contable}|{tipo de comprobante}|{sucursal}` (`PorDiaYSucursal`). Con `PorDia` sale **un
comprobante por día** y tipo (un `CA` con el interés, la mora, el seguro y los ajustes del día; un `CN` con los
cambios de dimensión que detectó el proceso), y **cada línea lleva su sucursal**. El despachador envía un
`PostLendingSummaryGroupCommand(BatchPublicId, GroupKey, MessagePublicIds)` por grupo:

1. Arma el `PostingRequest` de cada unidad del grupo (cada parte) como si fuera por hecho y los valida todos con
   `ValidarVariosAsync` (carga en bloque, sin seguimiento, sin numerar).
2. **Excluye** las que fallan, con su motivo (`Rejected`), y a sus dependientes del mismo lote, que quedan
   esperando: una parte mala no tumba la causación del día.
3. Suma las líneas válidas por **cuenta, lado, sucursal y centro**, **débitos y créditos por separado, sin
   netear**.
4. **No suma** las líneas cuya cuenta exige tercero, documento cruce o base: conservan una línea **por crédito**,
   con su deudor y `PG` + número del crédito.
5. Un solo `PrepareAsync` por grupo, fechado en la fecha contable del grupo (nunca la del lote), con origen
   `AccountingOrigin("CAR", "LendingPostingBatch", lote.PublicId)` y `RegistradoPor` nulo.
6. Una fila de `ACC_LendingPostings` por hecho del grupo, todas con el mismo `AccountingDocumentId` y el
   `BatchPublicId`.
7. Un `SaveChanges` por grupo.

**Conserva las dimensiones**: cada combinación (línea, modalidad, sub-segmento, categoría, concepto, empleado,
gracia, reestructurado, sucursal) resuelve su propia cuenta; sólo se suman las que la matriz llevó a la misma
cuenta, que es decisión de la contadora. **Conserva la lista de créditos**: los recibos del comprobante llevan a sus
hechos, y cada parte trae su lista por crédito; `GET /api/accounting/lending/batches/{id}` la muestra con los
comprobantes y los rechazos del lote.

Volumen (C5): con 20.000 créditos y cuentas de interés que exigen tercero y cruce, unas 20.000 líneas por día. Se
acepta y se mide con `CausacionDiariaPerformanceTests` (`RUN_PERF_TESTS=1`); si pesa, la contadora puede quitar
«exige tercero» de las cuentas de interés o usar `PorDiaYSucursal`.

### 10.5 Vista previa y rendimiento

`PreviewLendingBatchQuery` (`POST /api/accounting/lending/batches/preview`, `Accounting.LendingBatches.View`)
devuelve los grupos con sus comprobantes propuestos (tipo, fecha, líneas por sucursal, totales), los hechos de cada
grupo, los errores de los que quedarían fuera y el `cutoffMessagePublicId`. No numera, no agrega ni guarda nada. El
lote carga una vez las reglas vigentes, las cuentas, los tipos, los períodos, las sucursales y los terceros que usa,
y los reutiliza en cada grupo.

## 11. El cierre mensual de Cartera en Contabilidad (T47, T48, FR-107)

### 11.1 Qué emite y cuándo

El cierre del mes *M* (`OrderMonthlyCloseCommand` → corrida `MonthlyClose`) calcula por tandas y guarda sus líneas
sin mensajes; en su **transacción final** inserta el encabezado, las partes (`LND_AccountingFactParts`, una por tanda
× sucursal × tipo) con sus hechos, el informativo `CierreDeCarteraRealizado` y el lote `LendingClose:{aaaa-mm}`.
Todos los hechos del cierre se fechan el **último día de *M***, que debe estar abierto en Contabilidad. Por partición
y en este orden:

1. `CarteraReclasificadaPorCalificacion`: **todo** saldo con dimensión de categoría pasa de la categoría anterior a
   la nueva (§11.2).
2. `DeterioroCalculado`: el **delta** del deterioro individual en la categoría nueva, el del general y el del
   adicional, y sus traslados (§11.3).
3. `CierreDeCarteraRealizado` (informativo, §7.4).

El modo por defecto es por lotes resumido con disparador `CierreDeCartera`: sale un `PV` por día (el último de *M*)
con cada línea en su sucursal. El primer cierre tras la activación se compara con el **cierre base**, que no emitió
nada (FR-140): Contabilidad ya tenía esos saldos y el cuadre de activación lo comprobó (§14.3). La pérdida esperada
(K6a) usa los mismos dos hechos, sin hechos nuevos.

### 11.2 Reclasificación completa por categoría

Para cada crédito cuya categoría contable cambia (§3.4), por cada concepto con saldo al último día de *M* (por fecha
contable):

| Saldo | Débito | Crédito |
|---|---|---|
| Capital, intereses, pagos por cuenta del asociado (roles de naturaleza débito) | el rol con la categoría **nueva** | el mismo rol con la categoría **anterior** |
| Suspenso en orden (`SuspensoEnOrden`) | ídem | ídem |
| Deterioro individual de cada concepto (naturaleza crédito) | `DeterioroIndividual` con la categoría **anterior** | `DeterioroIndividual` con la **nueva** |

`CargoPorCobrar`, las contras de orden, la castigada y los pasivos no tienen categoría y no se reclasifican. Un
cambio entre subcategorías de la misma letra (E1 → E2) no produce líneas. Si la contadora lleva dos categorías a la
misma auxiliar, las dos puntas resuelven la misma cuenta y el constructor no genera líneas (recibo `SameAccounts`).
Esto **amplía FR-107**, que sólo nombra capital e intereses: en el CUIF el deterioro y el suspenso también van por
categoría, y sin trasladarlos el deterioro de un crédito que pasó de A a C se quedaría en la cuenta de A (pregunta
C4, enmienda de T97).

### 11.3 Deterioro en delta

Cartera calcula, por crédito y concepto, el deterioro individual del cierre en la categoría nueva y lo compara con el
**saldo vigente** de ese deterioro (el del cierre anterior, menos lo consumido por condonaciones y castigos del mes,
ya reclasificado). La diferencia viaja con su movimiento:

| Movimiento | Débito | Crédito | Cuándo |
|---|---|---|---|
| `Increase` | `GastoDeterioro` (`Individual`, modalidad, concepto) | `DeterioroIndividual` (categoría nueva) | El deterioro sube |
| `DecreaseSameYear` | `DeterioroIndividual` | `GastoDeterioro` (`Individual`) | El deterioro baja, hasta lo que se llevó al gasto **en el mismo ejercicio** por ese crédito y concepto |
| `DecreasePriorYears` | `DeterioroIndividual` | `RecuperacionDeterioro` (`Individual`) | El resto de la disminución |
| `Transfer` | `DeterioroGeneral` (`Additional`) | `DeterioroIndividual` | Uso del adicional para compensar el individual, en los términos de la norma (FR-104) |

El general y el adicional siguen la misma tabla con `DeterioroGeneral` y el motivo `General` o `Additional`. La
regla de la disminución es el parámetro `Contabilidad.DisminucionDeDeterioro`: `MismoEjercicioPrimero` (defecto,
práctica colombiana) o `TodoARecuperacion`; el ejercicio es el año calendario. Los dos, «pendiente de validar» por
la contadora (C2). Expresar el deterioro como delta en la categoría nueva evita inflar el gasto y la recuperación
del mes con una liberación y una constitución completas.

### 11.4 Reapertura

Sólo el último cierre, con motivo y atribución y **antes del cierre contable de *M*** (D12; Cartera lo pregunta con
`EstadoDelPeriodoAsync`). En una transacción, Cartera inserta la reapertura y emite, por partes, `ReversoDeCierre`
(clave `Reopen:{versión}`, origen la parte de la reapertura, `Related` la parte del cierre que revierte) y el
informativo `CierreDeCarteraReabierto`. El reverso:

- se fecha el mismo último día de *M*;
- hereda el modo de la parte del cierre (por lotes, `CierreDeCartera`), y su transacción crea un lote nuevo con el
  mismo alcance `LendingClose:{aaaa-mm}`, que toma sólo los reversos;
- invierte cada línea de la parte del cierre con las **dimensiones vigentes** al reverso (la categoría vuelve a la
  del cierre anterior; las demás, las de ese momento), resueltas con la matriz vigente ese día (§9).

Volver a cerrar *M* crea la versión siguiente y emite de nuevo.

## 12. Contabilidad no cierra un mes que Cartera no cerró (T57, FR-146)

Contabilidad lo sabe **sin leer `LND_` y sin un puerto inverso**: por los cuatro informativos de Cartera, que dejan
recibo en `ACC_LendingPostings` (§7.4), y por las entregas de la bandeja.

`ClosePeriodCommand(int Year, int Month, bool AcknowledgeInventoryPending = false, bool AcknowledgeLendingPending =
false, bool AcknowledgeLendingLoad = false)`. Después de la comprobación de borradores y del aviso de Inventario:

| Comprobación | Código | Se puede reconocer | Cuándo |
|---|---|---|---|
| (a) **Bloqueo** | `Accounting.Period.LendingNotClosed` (422) | **No** | Hay un recibo `CarteraActivada` con fecha ≤ fin de *M*, y no hay un recibo `CierreDeCarteraRealizado` de *M* que no esté reabierto (es decir, sin un `CierreDeCarteraReabierto` cuyo `RelatedMessagePublicId` sea él). `data: { month, activationDate, lastLendingClose }` |
| (b) **Aviso** | `Accounting.Period.LendingPending` (422) | Sí, `AcknowledgeLendingPending` | Hay entregas de Cartera a Contabilidad en `Pending`, `InBatch` o `Rejected` con fecha contable en *M*, informativos incluidos (`ListPendingIntegrationMessagesQuery` con módulo `CAR`). `data: { pending, inBatch, rejected, oldestOperationDate, types[] }` |
| (c) **Aviso** | `Accounting.Period.LendingLoadWillExpire` (422) | Sí, `AcknowledgeLendingLoad` | La última `CargaDeCarteraAplicada` (por emisión), sin una `CarteraActivada` posterior, tiene su día siguiente al corte dentro de *M*: cerrar *M* deja esa carga sin poder activarse (FR-142). `data: { cutoffDate, loadPublicId }` |

- **Sin `CarteraActivada`, Contabilidad cierra como hoy**: sólo pueden saltar (b) y (c). Lo cubre
  `CarteraDesplegadaSinActivarTests`.
- La regla rige desde el mes de la fecha de activación. Si el corte no es fin de mes, el primer cierre de Cartera es
  el del mes que contiene la activación, comparado con el cierre base (T49), y Contabilidad puede cerrar ese mes
  apenas Cartera lo cierra.
- La pantalla `/contabilidad/periodos` muestra el estado de Cartera del mes («Cartera cerró noviembre, versión 1, el
  3 de diciembre», o el bloqueo con enlace a `/cartera/cierres`) y ofrece **Procesar ahora** (lote manual de Cartera,
  `Accounting.LendingBatches.Run`) o **Cerrar de todos modos** en los avisos. Todo reconocimiento queda auditado.
- Latencia: un informativo se procesa en segundos; mientras no se procesa, (b) lo ve pendiente y avisa.
- `ReopenPeriodCommand` y `CloseFiscalYearCommand` no cambian. Reabrir un mes en Contabilidad no reabre el de
  Cartera.

## 13. Validación previa: «¿es contabilizable?» (FR-127, T56, enmienda A8)

### 13.1 Quién pregunta y cómo

`ValidacionPreviaContableDeCartera` arma los hechos **tal como se emitirían** (los mismos objetos de
`EmisionDeCartera`), los reduce a **una pregunta por combinación distinta** de hecho, rol y dimensiones, y llama a
`IContabilidadParaCartera.EvaluarAsync` **fuera del cerrojo** (T34) y con un tiempo máximo de
`Contabilidad.ValidacionPreviaSegundos` (3 s). Una excepción o el tiempo agotado es «sin respuesta». Una nómina de 500
empleados se reduce a unas pocas combinaciones. Lo evaluado y lo emitido son el mismo contrato, y el consumidor los
construye con el mismo `ConstructorDeLineasDeCartera`.

`EvaluateLendingPostingQuery` (detrás de `EvaluarAsync`) agrupa en unidades, arma cada `PostingRequest` con el
constructor del consumidor, lo somete a `AccountingPoster.ValidarVariosAsync` (reglas 1 a 11, carga en bloque, sin
seguimiento, sin numerar) y suma las comprobaciones de la matriz: regla faltante, tipo de comprobante sin mapear o
inactivo, hecho que no cuadra, más de dos decimales. Sólo evalúa lo que pasaría a Contabilidad: no los hechos
`NoPasa`, ni los informativos, ni los mensajes a otros destinos.

### 13.2 Cuándo bloquea y cuándo advierte

| Origen de la operación | Con hallazgos (`IsPostable = false`) | Sin respuesta |
|---|---|---|
| **Pantallas de Cartera**: desembolso, novedad (reestructuración, modificación, novación), condonación, castigo | `Contabilidad.FaltaDeRegla = Bloquear` (el defecto desde el 2026-10-14): no confirma, `Lending.Prevalidation.NotPostable` (422) con `data.errors[]`. `Advertir`: confirma, sella `PrevalidationOutcome = NotPostable` y alerta | `Contabilidad.PoliticaSinRespuesta = Bloquear`: `Lending.Prevalidation.NoResponse` (422). `ConfirmarConPendiente` (defecto, D7): confirma y sella `NoResponse` |
| **Contrato de obligaciones** (Recaudo, Nómina, Aportes, recogida de créditos propios, recaudador de prueba) | `Bloquear`: **rechaza el ítem** (`Rejected`) con `Lending.Prevalidation.NotPostable` y `data.errors[]`, no escribe nada de ese pago y alerta; los ítems de otras combinaciones del lote siguen. **Nómina** no aprueba: la ordinaria y la definitiva fallan enteras con `Payroll.LoanDeductionNotPostable` (422, `data.loans[] { loanNumber, message, lending }`) y su transacción revierte lo que Cartera alcanzó a aplicar (I-4). `Advertir`: aplica, sella `NotPostable` y alerta | siempre aplica y sella `NoResponse` |
| **Consumidor de Comercial** (obligación desde una venta, ajustes) | `Bloquear`: la obligación **no nace**; el recibo queda `Rejected` y reprocesable (como lo no convertible, C95: sin `ObligacionRechazada`, la venta se informa `Pending` con el motivo) y alerta; corregida la matriz, se reprocesa desde la bandeja. La venta de Comercial ya está confirmada: lo que espera es su obligación en Cartera. `Advertir`: D9, crea la obligación aunque la evaluación falle | ídem |
| **Procesos** (causación diaria, corrección por valor nacional, cierre mensual) | `Bloquear`: la tanda **no avanza** —no se guarda nada de ella, el cursor no se mueve—, la corrida queda `Failed` con `Lending.Prevalidation.NotPostable` en `LND_ProcessRunErrors` y sale la alerta; corregida la matriz, «Reintentar» la retoma. Con `Advertir` los procesos **no preguntan** (`ValidacionPreviaDeProceso` sólo evalúa si la política podría bloquear, sin tiempo máximo porque corre dentro de la transacción de la tanda) | no preguntan |
| **Cobros provisionales al activar** | advierte (no se evalúan en K1) | — |
| **Reserva de número de crédito** | no se evalúa: corre dentro de la confirmación de Comercial y no produce hecho (T22) | — |

- `Contabilidad.FaltaDeRegla` gobierna **todo** hallazgo de la evaluación, no sólo la regla faltante: una cuenta
  inactiva en la matriz es el mismo vacío de parametrización. `PoliticaSinRespuesta = Bloquear` se limita a las
  operaciones de pantalla (C80): la respuesta a A8 no la tocó.
- **Sin contabilidad iniciada o con el hecho en `NoPasa` no se pregunta nada y no se bloquea nada** (§10.1), en ningún
  origen.
- **El defecto de `Contabilidad.FaltaDeRegla` es `Bloquear`** (decisión del dueño del 2026-10-14): con la contabilidad
  iniciada, la matriz de Cartera tiene que estar completa para lo que la cooperativa opere, o `Advertir` fijado a
  propósito (por vigencia o por tipo de hecho).
- `PrevalidationOutcome` gana `NotPostable = 4` («evaluado no contabilizable, confirmado con advertencia») además de
  `Postable`, `NoResponse` y `NotApplicable`. La bandeja compara lo sellado con el resultado de la entrega.
- Alerta `Cartera.HechoNoContabilizable` para quienes tienen `Accounting.LendingRules.Manage` y
  `Lending.Messages.Reprocess`, con la combinación y la operación; una por combinación y día (`DedupKey`).
- **Por qué**: la propuesta de la enmienda A8 limitaba «Bloquear» a las pantallas (un pago que ya ocurrió no se
  rechaza por un vacío de la matriz); el dueño respondió **«Bloquear todo»** el 2026-10-14 (decisiones-transversales §5,
  A8): quien elige `Bloquear` prefiere que nada pase sin poder contabilizarse, aunque eso detenga una nómina o una
  tanda. FR-127 y US7-2 quedan como estaban (sin recorte). Con `Advertir`, lo que se aplicó sin regla queda rechazado
  en la bandeja, se ve en la conciliación y se recupera creando la regla y reprocesando.

### 13.3 Respuesta de Cartera y quién corrige

`Lending.Prevalidation.NotPostable` lleva `data.errors[] { factType, role, combination, account, rule, whoFixes }`.
En pantalla el mensaje va en español claro y dice qué falta y quién lo corrige, sin códigos técnicos (FR-013).

| Hallazgo | Módulo | Pantalla | Permiso |
|---|---|---|---|
| `Accounting.LendingRule.Missing` | Contabilidad | `/contabilidad/cartera/matriz` | `Accounting.LendingRules.Manage` |
| `Accounting.VoucherType.NotFound` · `.NotAllowedForModule`; `Accounting.Line.CrossDocumentTypeInvalid` | Contabilidad | `/contabilidad/cartera/tipos-de-comprobante` | `Accounting.LendingRules.Manage` |
| `Accounting.Line.AccountNotMovement` · `.AccountInactive` · `.AccountNotEnabledForModule` · `.CostCenterNotAllowed` · `.TaxBaseRequired` | Contabilidad | `/contabilidad/plan-de-cuentas` | `Accounting.Accounts.Manage` |
| `Accounting.Line.CostCenterRequired` · `.CrossDocumentRequired` · `.ThirdPartyRequired` | Contabilidad | `/contabilidad/cartera/matriz` (centro en la regla) o el plan de cuentas (quitar la exigencia) | `Accounting.LendingRules.Manage` |
| `Accounting.Line.ThirdPartyInvalid` | Core | Personas | `Core.People.Update` |
| `Accounting.Period.Closed` · `.NotFound` | Contabilidad | `/contabilidad/periodos` | `Accounting.Periods.Reopen` (`.CloseYear` si falta el ejercicio) |
| `Accounting.NotInitialized` | Contabilidad | `/contabilidad/configuracion` | `Accounting.Setup.Manage` |
| `Accounting.LendingFact.Unbalanced`, más de dos decimales | Cartera | — | defecto del emisor: soporte |

## 14. Conciliación y cuadre de activación (T58, FR-130, FR-141)

### 14.1 Lo que responde Contabilidad

`IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync(corte, combinaciones)` delega en
`GetLendingAccountBalancesQuery`:

1. Resuelve cada combinación de Cartera (rol de saldo, dimensiones y sucursal; hecho `*`) con la matriz vigente al
   corte (§3.3).
2. Arma **conjuntos de cuentas** como componentes conexos del grafo combinación ↔ cuenta, por familia de rol
   (`ConjuntosDeCuentas`, generalizado de la 012): si la contadora lleva varias categorías a una auxiliar, o una
   combinación tiene reglas por sucursal, el conjunto las junta, y sólo se compara por conjunto.
3. Lee el saldo de cada cuenta y sucursal **sólo** por `MovimientosContables` (`HastaInclusive`, con
   `AlcanceDeSucursales.SinRestriccion`): incluye la apertura `AP`; el cierre `CI` no toca las clases 1, 8 ni 9.
4. Devuelve, por conjunto, sus combinaciones, sus cuentas con el saldo por sucursal y el saldo; aparte, las
   combinaciones sin regla. Sin contabilidad iniciada, `Accounting.NotInitialized`.

### 14.2 La conciliación (`ConciliacionContableDeCartera`)

- **El saldo de Cartera** de una combinación a un corte es la suma de sus transacciones del libro por **fecha
  contable** ≤ corte, con la combinación que Cartera informó en el último hecho que movió o reclasificó ese saldo. Es
  el invariante que hace exacta la comparación: Contabilidad tiene exactamente lo que Cartera le dijo.
- **Qué compara**:

| Familia | Por | Esperado de Cartera |
|---|---|---|
| Capital | modalidad, sub-segmento y categoría (y línea, empleado o reestructurado si la matriz los separa) | su saldo |
| Intereses | ídem, más concepto y gracia | ídem |
| Pagos por cuenta y cargos por cobrar | ídem | ídem |
| Deterioro individual | modalidad, categoría y concepto | ídem |
| Deterioro general y adicional | motivo | ídem |
| Anticipos · saldos a favor | — | ídem |
| Suspenso en orden · castigada en control | modalidad, categoría y concepto · concepto | ídem |
| Puentes: `RecaudoPorAplicar` por clase, `DesembolsoPorGirar`, `PuenteComercial`, `CuentaPorCobrarComercial` | clase o motivo | cero; en `DesembolsoPorGirar`, las órdenes de giro emitidas sin «girada» ni anulación que Cartera conoce por `ResultadoDelGiro` |
| Contingentes (si pasan, §19) | motivo | su saldo |

- **Explicación**: cada diferencia se explica con los hechos de Cartera de fecha contable ≤ corte que están
  `Pending`, `InBatch`, `Rejected` o `NotApplicable` (no pasa), recalculados por conjunto con el mismo constructor,
  y se listan hecho por hecho. Con cero hechos sin procesar, la diferencia es cero (SC-006). En los puentes, lo que
  los hechos de Cartera no explican es del otro lado (el recaudador o Tesorería no han contabilizado) y se muestra
  así.
- **Descuadre sin explicar**: alerta `Cartera.DescuadreContable` al contador y al jefe de cartera, una por corte y
  conjunto.
- **Dónde**: `GET /api/lending/accounting/reconciliation?date=` (`Lending.Reconciliation.View`), pantalla
  `/cartera/conciliacion-contable`. Filtra por el alcance de oficinas del usuario (`IAlcanceDeCartera`): sin
  `Lending.Scope.AllOffices` se ven las sucursales de sus oficinas. Se presenta en cada cierre mensual al último día
  del mes, después de procesar su lote (FR-107, FR-130).

### 14.3 Cuadre de activación (FR-141)

La misma consulta, con las combinaciones de la carga aplicada al corte: por cada crédito migrado, su modalidad,
sub-segmento, **categoría al corte**, línea, empleado, gracia, reestructurado y sucursal, y por concepto sus saldos
(capital, intereses, suspenso, pagos por cuenta, anticipos, saldos a favor, castigada) y su deterioro individual,
más el general. Compara por concepto, modalidad, sub-segmento, categoría y cuenta; las diferencias van a
`LND_MigrationLoadDifferences` y sólo se aceptan con motivo y un punto de acta (D17, T64). Se calcula al aplicar la
carga y otra vez al activar (T84): si cambió, la aceptación anterior deja de valer. El saldo inicial no genera
hechos (FR-140): Contabilidad ya tiene esos saldos, por su apertura o por su operación anterior. La modalidad
vivienda queda fuera del cuadre hasta K6b. Ruta: `GET /api/lending/migration/loads/{id}/balance-check` (`api.md`).

Del lado contable, la activación (T84) además **exige** la regla `RecaudoPorAplicar` con `CollectorClass = Payroll`
resuelta (desde §17 `DESC_CARTERA` contabiliza) y toda oficina con créditos vinculada a una sucursal contable (T13,
CH49); y **muestra** la completitud de la matriz (§3.7), que conviene dejar vacía antes del ensayo.

## 15. Indicadores de la entidad (T59, FR-041, FR-100, FR-103)

`IContabilidadParaCartera.IndicadoresDeLaEntidadAsync(fecha)` delega en `GetEntityIndicatorsQuery`:

| Indicador | De dónde sale |
|---|---|
| Resultado del ejercicio a la fecha | Saldo de las clases 4 a 7 del ejercicio de la fecha, sin el comprobante de cierre `CI`, por `MovimientosContables` |
| Pérdidas acumuladas | Saldo de las cuentas de la **clase 3 de naturaleza débito** (C78) (en el CUIF sembrado, las «pérdidas» 3510, 3610 y 3910, y 3810, que en una cooperativa no tiene saldo). Se eligen por clase y naturaleza, no por código: `LaContabilidadNoTieneCuentasEnCodigo` no admite códigos de cuenta como literales |
| Patrimonio técnico · relación de solvencia | El último `RegulatoryIndicator` con corte ≤ fecha, con su corte, su fuente y su soporte. No se calculan: tienen deducciones de norma (Decreto 961 de 2018) y activos ponderados por riesgo que el plan de cuentas no da |

- **Vigencia** (C33): Contabilidad devuelve **el último registrado** con su corte, sin juzgar si sigue vigente, y
  Cartera decide con su parámetro `Contabilidad.IndicadoresVigenciaMeses` (6, módulo `LND`). Un indicador más viejo,
  o ninguno, es «no disponible».
- **Sin dato vigente**:
  - excepción de aportes del arrastre y descuento de aportes en el deterioro (FR-100, FR-103): lo conservador de
    D19, no se aplican;
  - **límite de concentración** (FR-041), que bloquea: se bloquean el desembolso, la asignación de cupo y la novedad
    que aumenten el endeudamiento (`Lending.Concentration.TechnicalCapitalUnavailable`, «patrimonio técnico no
    disponible») y se alerta al contador (`Cartera.PatrimonioTecnicoNoDisponible`). La alternativa, usar el último
    registrado con advertencia, es la pregunta C3.
- **Registro**: `RegisterRegulatoryIndicatorCommand` (`POST /api/accounting/regulatory-indicators`,
  `Accounting.RegulatoryIndicators.Manage`) y la lista (`GET`, `.View`), pantalla `/contabilidad/indicadores-regulatorios`,
  con el adjunto de soporte. Auditoría `Accounting.RegulatoryIndicator.Registered`. Si la solvencia no aplica a la
  cooperativa (COOFLOPAL no tiene actividad financiera, pregunta C3), no se registra y FR-100 y FR-103 siguen en lo
  conservador.

## 16. Puertos de la frontera (T59, T90)

### 16.1 `IContabilidadParaCartera` (Contabilidad → Cartera)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Accounting;

public interface IContabilidadParaCartera
{
    // FR-127: ¿las líneas que generarían estos hechos tienen regla y pasan las reglas de la 009?
    Task<Result<ResultadoDeEvaluacionContableDto>> EvaluarAsync(
        IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct);

    // FR-130, FR-141: saldo contable, por conjunto de cuentas, de las combinaciones de Cartera a un corte.
    Task<Result<SaldosMapeadosDeCarteraDto>> SaldosDeCuentasMapeadasAsync(
        DateOnly corte, IReadOnlyList<CombinacionDeSaldo> combinaciones, CancellationToken ct);

    // FR-012, FR-107, FR-142: contabilidad iniciada, estado del mes de la fecha y primer mes abierto desde ella.
    Task<Result<EstadoDelPeriodoDto>> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct);

    // FR-041, FR-100, FR-103: resultado, pérdidas, patrimonio técnico y solvencia a una fecha.
    Task<Result<IndicadoresDeLaEntidadDto>> IndicadoresDeLaEntidadAsync(DateOnly fecha, CancellationToken ct);

    // FR-047: cuentas bancarias del plan, de un banco si se da, para elegir la de origen del archivo de desembolsos.
    Task<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>> CuentasBancariasAsync(
        Guid? bancoPublicId, CancellationToken ct);
}

// Existentes (012): MensajeContableDto(IntegrationEnvelopeV1 Envelope, object Payload), QuienCorrigeDto,
// CuentaDelConjuntoDto, SaldoPorSucursalDto.

public sealed record ResultadoDeEvaluacionContableDto(
    bool IsPostable,
    IReadOnlyList<HallazgoDeCarteraDto> Errors,
    IReadOnlyList<HallazgoDeCarteraDto> Warnings);

public sealed record HallazgoDeCarteraDto(
    string FactType, string? Role, IReadOnlyDictionary<string, string>? Combination,
    string? AccountCode, string Rule, string Message, QuienCorrigeDto WhoFixes);

// Entrada (sin sufijo, §2.1 de las transversales): una combinación de saldo; el hecho es siempre «*».
public sealed record CombinacionDeSaldo(
    string Role, string? CreditLineCode, string? ModalityCode, string? SubsegmentCode, string? RiskCategory,
    string? ConceptCode, bool? IsEmployee, bool? InGracePeriod, bool? OnBehalfOfAssociate, bool? Restructured,
    string? CollectorClass, string? ReasonCode, Guid BranchPublicId);

public sealed record SaldosMapeadosDeCarteraDto(
    IReadOnlyList<ConjuntoDeCuentasDeCarteraDto> Sets,
    IReadOnlyList<int> UnresolvedCombinations);            // índices de la lista de entrada

public sealed record ConjuntoDeCuentasDeCarteraDto(
    string RoleFamily,
    IReadOnlyList<int> Combinations,                       // índices de la lista de entrada
    IReadOnlyList<CuentaDelConjuntoDto> Accounts,
    decimal Balance);

public enum EstadoDePeriodoContable { Open = 1, Closed = 2, NotFound = 3 }

public sealed record EstadoDelPeriodoDto(
    bool AccountingInitialized, EstadoDePeriodoContable State, DateOnly? FirstOpenMonthFrom);

public sealed record IndicadoresDeLaEntidadDto(
    DateOnly Date, decimal YearResult, decimal AccumulatedLosses,
    IndicadorRegistradoDto? TechnicalCapital, IndicadorRegistradoDto? SolvencyRatio);

public sealed record IndicadorRegistradoDto(
    Guid RegulatoryIndicatorPublicId, decimal Value, string Unit, DateOnly CutoffDate, string Source);

public sealed record CuentaBancariaDeOrigenDto(
    Guid AccountPublicId, string AccountCode, string AccountName,
    Guid BankPublicId, string BankName, string? AccountNumber, string? TransferCode);
```

**Roles de saldo que admite el contrato** (C29). `CombinacionDeSaldo.Role` y `CombinacionDeCarteraEnUsoDto.Role`
llevan el nombre del rol de `RolesDeCuentaDeCartera` como texto, y sólo estos, que son los de hecho `*` (§3.3):

| Familia | Roles |
|---|---|
| Saldo | `Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado`, `CargoPorCobrar`, `DeterioroIndividual`, `DeterioroGeneral` |
| Orden | `SuspensoEnOrden`, `ContraOrdenContingente`, `CastigadaEnOrden`, `ContraOrdenControl` |
| Pasivo | `Anticipo`, `SaldoAFavor`, `SeguroPorPagar`, `CuentaPorPagarATercero` |
| Puente | `RecaudoPorAplicar`, `DesembolsoPorGirar`, `PuenteComercial`, `CuentaPorCobrarComercial` |
| Diferido | `IngresoDiferido` |
| Contingente (sólo si A3 se aprueba) | `ContingenteAcreedora`, `ContraContingenteAcreedora` |

Un rol de resultado (`Ingreso`, `GastoDeterioro`, `RecuperacionDeterioro`, `GastoDeCastigoOCondonacion`,
`IngresoPorRecuperacion`) o un nombre desconocido en la entrada es un defecto del llamador: el validador del puerto
lo rechaza antes de consultar. Cartera toma la lista de aquí; `interfaces.md` remite.

El adaptador `ContabilidadParaCartera` corre **en proceso** (sin HTTP) y delega en las consultas de §1.1;
`CuentasBancariasAsync` reutiliza `ListBankAccountsQuery` (cuentas del plan con banco, activas, con su número y su
código ACH). Las rutas de Contabilidad para la contadora exigen sus permisos; el puerto no los pide, porque lo usa
Cartera dentro de una operación ya autorizada (molde de la 012).

### 16.2 `IDimensionesDeCartera` (Cartera → Contabilidad)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Lending;

public interface IDimensionesDeCartera
{
    // Códigos para validar una regla al guardarla y para la plantilla.
    Task<CatalogoDeDimensionesDeCarteraDto> CatalogoAsync(CancellationToken ct);

    // Combinaciones con saldo o movimiento a una fecha, para la completitud.
    Task<IReadOnlyList<CombinacionDeCarteraEnUsoDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct);
}

public sealed record CatalogoDeDimensionesDeCarteraDto(
    IReadOnlyList<LineaDeCarteraDto> CreditLines,
    IReadOnlyList<ModalidadDeCarteraDto> Modalities,
    IReadOnlyList<string> Concepts,                        // LoanConcept
    IReadOnlyList<string> CollectorClasses,                // ClaseDeRecaudador
    IReadOnlyList<string> FactTypes);

public sealed record LineaDeCarteraDto(string Code, string Name, string ModalityCode, string? SubsegmentCode, bool Active);

public sealed record ModalidadDeCarteraDto(string Code, string Name, IReadOnlyList<string> SubsegmentCodes);

public sealed record CombinacionDeCarteraEnUsoDto(
    string Role, string? CreditLineCode, string ModalityCode, string? SubsegmentCode, string? RiskCategory,
    string? ConceptCode, bool IsEmployee, bool InGracePeriod, bool Restructured, Guid BranchPublicId,
    DateOnly LastUsedAt);
```

Lo implementa `DimensionesDeCartera` (`Application/Lending/Integration`), sin el alcance de oficinas de Cartera (los
puertos para otros módulos no lo aplican, T62). Es interfaz nueva de la lista cerrada de FR-008: la **enmienda A1**.
`LaCarteraSoloSeTocaPorSusInterfaces` fija sus dos métodos, y `CarteraNoConoceContabilidad` los cinco de §16.1.

## 17. Puentes de recaudo y el descuento de nómina (`DESC_CARTERA`, T53, pregunta C1)

### 17.1 Un puente por clase de recaudador

Cartera no conoce cajas, medios de pago ni pasivos del recaudador (FR-008). Todo dinero que reduce una obligación
entra por el contrato (T91) y Cartera lo contabiliza contra **`RecaudoPorAplicar`**, resuelto por la clase del
recaudador. Cada recaudador contabiliza **su** lado contra la misma cuenta:

| Clase | Lado del recaudador (lo contabiliza él) | Lado de Cartera (`PagoAplicado` y su unidad) |
|---|---|---|
| `Receipts` (Recaudo) | caja o banco / puente | puente / conceptos |
| `Payroll` (Nómina) | `DESC_CARTERA`: salarios por pagar / puente «recaudos por nómina para Cartera» (en el NM o en la definitiva) | puente / conceptos (comprobante `DN`) |
| `Contributions` (Aportes, en un retiro) | aportes / puente | puente / conceptos |
| `OwnLoanPayoff` (recogida en un desembolso) | — (es la propia Cartera) | puente / conceptos del crédito recogido; la contadora lleva este puente **a la misma cuenta que `DesembolsoPorGirar`**, así el neto es lo que queda por girar |
| `Test` (doble de prueba, fuera de producción) | el doble no contabiliza | puente de prueba |

El saldo de cada puente es exactamente lo recaudado que Cartera todavía no contabilizó, o lo que Cartera contabilizó
y el recaudador no: la conciliación lo explica sola (§14.2). `SaldoAFavorResuelto` (devuelto) y
`RecuperacionDeCastigada` usan el mismo puente. La pagaduría externa **no** es recaudador: paga por Recaudo, con la
pagaduría como pagador y tercero (§2.7 de las transversales); desde K2 no hay puente nuevo.

### 17.2 Enmienda D-08 de la 010: `DESC_CARTERA` vuelve a contabilizar en Nómina

Hoy la línea `DESC_CARTERA` no genera asiento («Cartera ya contabilizó», D-08) y el pasivo con el empleado lo bajaba
el recaudo heredado con la cuenta débito del concepto, que Nómina le pasaba. Con esta enmienda:

- `DESC_CARTERA` contabiliza en el `NM` y en la definitiva (`AffectsAccounting = true`) con las cuentas del concepto:
  la **crédito** es el puente «recaudos por nómina para Cartera»; la débito, la de salarios por pagar que la nómina
  ya usa en sus deducciones. Lo valida la contadora.
- Se retiran la excepción de `LineFactory` (`ICalculationRule`), la de `ProposedDeductionsRule` y la de
  `AdjustSettlementDeductionCommand`, las omisiones de `SettlementAccountingPoster` y `PayrollAccountingPoster`, y
  `CuentaDebitoDelDescuentoAsync`.
- Nómina aplica el lote por el contrato **antes** de armar el comprobante (T92): el `NM` lleva lo descontado de
  verdad. Un rechazo o un excedente baja la línea a lo aplicado, y el `NoDescontado` no tiene asiento (es una marca
  en Cartera, `LND_InstallmentNonDeductions`).
- Reversar la nómina o la definitiva reversa sus recaudos por el contrato: Nómina reversa su `NM` (su propio
  mecanismo, la 009 no cambia para Nómina) y Cartera emite `PagoReversado` (`DN`) contra el mismo puente. Un usuario
  de Cartera no reversa estos pagos (`Lending.Reversal.OwnedByCollector`).
- **Antes de activar**: la activación exige la regla `RecaudoPorAplicar` con `CollectorClass = Payroll` (T84) y
  muestra, como aviso, si `DESC_CARTERA` tiene cuentas (la pantalla lo pregunta a Nómina por
  `GET /api/payroll/concept-definitions/{code}/accounts`, porque Cartera no lee `PAY_`). Sin cuentas, la primera
  nómina con descuentos de Cartera no se aprueba (`Payroll.Settlement.ConceptAccountsMissing`).
- Riesgo en producción: hoy la ordinaria no contabiliza `DESC_CARTERA` y nadie hace el `DN`; con la cartera heredada
  vacía (diagnóstico de T87) no queda residuo que corregir.

## 18. Cobros provisionales incorporados (FR-142)

Durante el crédito provisional de la 012, Contabilidad registró la cuenta por cobrar de cada venta a crédito
(sellada «Contabilidad») y la redujo con los cobros recibidos por fuera (caja o banco contra la cuenta por cobrar
provisional). Al activar:

1. La obligación de cada venta posterior al corte nace con `CreditoDesembolsado` en especie: débito `Capital`,
   crédito `CuentaPorCobrarComercial` por el **valor de la venta**, con cruce `FV` + factura (FR-056). Esto
   reclasifica la cuenta por cobrar a cartera de créditos sin duplicarla (US7-6).
2. Cada cobro de la plantilla `lending.provisional-collections` se registra como transacción de pago con su fecha
   valor, sin recaudador (`ProvisionalCollection`), y emite **`CobroProvisionalIncorporado`**: débito
   `CuentaPorCobrarComercial` (cruce `FV` + factura), crédito los conceptos que cubrió. Así la cuenta por cobrar
   provisional queda en cero por factura y el crédito, con el saldo real.
3. **Nunca contra caja ni bancos**: el dinero ya lo contabilizó Contabilidad cuando lo recibió.

Comprobante `RC`, en línea, fecha contable según §8 (normalmente la de activación, porque sus fechas valor caen entre
el corte y la activación).

## 19. Cuentas contingentes (sujeto a la pregunta A3)

El CUIF trae cuentas contingentes que FR-125 no cubre: 9115 (créditos aprobados no desembolsados), 913005
(créditos rotativos) y 9110 (bienes y valores recibidos en garantía), con su contra 9605. A3 propone emitir hechos
para ellas desde K1, con modo `NoPasa` por defecto, y dejar a la contadora decidir cuándo pasan. **C15** los nombra
(la propuesta por defecto de A3 rige si el dueño no responde) para que el catálogo de mensajes v1 no tenga que
cambiar después; el catálogo pasa de 44 a 47 tipos:

| Hecho | Corto | Sube cuando | Baja cuando | Motivo |
|---|---|---|---|---|
| `CreditoAprobadoSinDesembolsar` (9115) | CAD | se aprueba una solicitud | se desembolsa (cada tramo), vence la aprobación (FR-042) o se desiste | `ApprovedNotDisbursed` |
| `CupoRotativoOtorgado` (913005) | CRO | se otorga o amplía un cupo rotativo | se reduce o vence (usarlo o pagarlo no lo mueve: 913005 mide el cupo otorgado, C92) | `RevolvingLimit` |
| `GarantiaConstituida` (9110) | GCT | se constituye o se revalúa al alza una garantía | se libera, se extingue, se ejecuta o se revalúa a la baja | `CollateralReceived` |

Los tres son `Business`, con signo, modo `NoPasa`, comprobante `CN`, clave `Confirmation` sobre su operación y K1.
Cada uno lleva la variación con signo: débito `ContraContingenteAcreedora`, crédito `ContingenteAcreedora` (un
negativo invierte; C91), tercero el deudor, cruce `PG` + número del crédito cuando ya lo tiene; si todavía no lo tiene
(una aprobación, un cupo rotativo), sin cruce: conviene que 9115 y 913005 no lo exijan. **`AdmiteProceso = true`**:
el vencimiento de una aprobación o de un cupo lo detecta el ciclo, y la liberación tras la firmeza también es del
proceso. Si el dueño rechaza A3, salen del catálogo (y sus dos roles de la matriz) sin tocar nada más; `mensajes.md`
define su contenido.

## 20. Retiro de los escritores heredados (T96, DC-19)

El contrato de la 009 deja de asignar a Cartera comprobantes por llamada directa. En K1, con el retiro de la cartera
heredada (paso 3 de T88, y la migración destructiva `RetiroDeLaCarteraHeredada` en el paso 4):

| Se retira | Qué hacía | Lo reemplaza |
|---|---|---|
| `DisburseLoanCommand` | `DS` con `CreditLineParameter.AccountCode` y `COR_Banks.AccountingAccountCode` | `CreditoDesembolsado` por la matriz |
| `RecaudoDeCredito`, `ProcessPaymentCommand` y su llamada desde `ApproveSettlementCommand` | `RC` con caja o banco y las cuentas de texto de la línea; en la definitiva, la cuenta débito de `DESC_CARTERA` | El contrato de obligaciones y `PagoAplicado` (§17) |
| `CuentaDebitoDelDescuentoAsync`, `FormaDePagoNomina` y la excepción D-08 | Le pasaban a Cartera una cuenta de Nómina | El puente de §17.2 |
| `AccrueInterestCommand` (marcador «E3 (feature 009)») | `CA` pendiente | `InteresCausado`, `MoraCausada`, `SeguroCausado` |
| `ClassifyPortfolioCommand` (ídem) | `PV` pendiente | `DeterioroCalculado` y `CarteraReclasificadaPorCalificacion` |
| `ProcessPayrollDeductionCommand` (ídem) y `PayrollDeductionEntries` | `DN` pendiente; rebajaba saldos antes de aprobar la nómina | Nómina por el contrato (T92) |
| Las cuentas como texto de `LND_CreditLineParameters`, `LND_PortfolioAccounts` y `LND_ProvisionParameters`, con sus tablas | Cuentas sin validar | `ACC_LendingPostingRules` |
| La rama de `AccountReferenceFinder` que leía esas tablas | Impedía borrar cuentas «usadas» por Cartera | La matriz de Cartera (en el **mismo commit** del retiro, o deja de compilar) |

Se conservan `LiquidateSavingsInterestCommand` (se muda a `Application/Savings` con lo conservado, T3) y
`LiquidateCDTInterestCommand` (`Application/CDT/InterestLiquidation`, sin cambios). Los `ACC_Documents` con origen
`CAR` que un ambiente tenga no se tocan: son inmutables (pregunta H4). Las filas de Cartera de
`specs/009-contabilidad-niif/contracts/contabilizacion.md` se reemplazan por la referencia a este contrato (§25).

## 21. Rutas y permisos

### 21.1 Lado contable (`/api/accounting/lending`, `/api/accounting/regulatory-indicators`)

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /api/accounting/lending/rules` | `Accounting.LendingRules.View` | Lista con filtros (hecho, rol, dimensiones, fecha, cuenta) |
| `GET /api/accounting/lending/rules/catalog` | ídem | Hechos, roles con exigidas, opcionales y motivos, y códigos de Cartera |
| `POST /api/accounting/lending/rules` | `Accounting.LendingRules.Manage` | `CreateLendingPostingRuleCommand` |
| `POST /api/accounting/lending/rules/{id}/versions` | ídem | `AddLendingPostingRuleVersionCommand` |
| `POST /api/accounting/lending/rules/{id}/deactivate` | ídem | `DeactivateLendingPostingRuleCommand` (con motivo) |
| `GET /api/accounting/lending/rules/template.xlsx` | `Accounting.LendingRules.View` | `GetLendingRulesTemplateQuery` |
| `POST /api/accounting/lending/rules/import?mode=review\|apply` | `Accounting.LendingRules.Manage` | `ImportLendingPostingRulesCommand`, todo o nada |
| `GET /api/accounting/lending/rules/resolve` | `Accounting.LendingRules.View` | `ResolveLendingRuleQuery` |
| `GET /api/accounting/lending/completeness?date=` | ídem | `GetLendingRulesCompletenessQuery` |
| `GET` · `PUT /api/accounting/lending/voucher-mappings` | `.View` · `.Manage` | Mapeo de tipos de comprobante (`SetLendingVoucherMappingCommand`) |
| `GET /api/accounting/lending/batches` | `Accounting.LendingBatches.View` | Lotes de Cartera, con estado y totales (`GetLendingBatchTotalsQuery`) |
| `POST /api/accounting/lending/batches/preview` | ídem | `PreviewLendingBatchQuery` |
| `POST /api/accounting/lending/batches` | `Accounting.LendingBatches.Run` | Lote manual (202, `Idempotency-Key`, motivo) |
| `GET /api/accounting/lending/batches/{id}` | `Accounting.LendingBatches.View` | Hechos, créditos, comprobantes y rechazos del lote |
| `GET /api/accounting/lending/postings?document=` | `Accounting.Vouchers.View` | `ListLendingPostingsQuery`: recibos de un comprobante o de un origen |
| `GET` · `POST /api/accounting/regulatory-indicators` | `Accounting.RegulatoryIndicators.View` · `.Manage` | Lista y `RegisterRegulatoryIndicatorCommand` |

Sin permiso, el mismo 404 que algo inexistente. La matriz, el mapeo y los indicadores siguen la mecánica de la 009,
sin clave de operación; la orden de lote la lleva.

### 21.2 Lado de Cartera (los detalla `api.md`)

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /api/lending/accounting/reconciliation?date=` | `Lending.Reconciliation.View` | Conciliación (§14.2) |
| `GET /api/lending/accounting/facts` | `Lending.Messages.Read` | Hechos contables de Cartera con su estado y su comprobante (de la entrega, sin leer `ACC_`) |
| `GET /api/lending/migration/loads/{id}/balance-check` | los de migración | Cuadre de activación (§14.3) |
| `POST /api/lending/messages/reprocess` · `/send-not-applicable` | `Lending.Messages.Reprocess` · `.SendNotApplicable` | Reproceso y envío posterior |

## 22. Actor y auditoría

| Quién procesa | `Actor` | `PostedBy` del comprobante | `RegisteredBy` |
|---|---|---|---|
| En línea, lote de fin de causación, lote de cierre de Cartera, lote programado | `Kind = Process`, «Proceso de integración», canal `Process`, origen `Mensaje:{id}` o `Lote:{número}`, sin IP | «Proceso de integración» | el usuario de origen si fue una persona; si fue el proceso o es un resumido, `Actor.NombreDelProceso` («Proceso de integración», C23) |
| Lote manual, reproceso, envío posterior | `Kind = Person`: quien lo ordenó, con su IP y su motivo | esa persona | ídem |

- El usuario de origen viaja en el hecho y queda como **dato** en el comprobante y en `ACC_LendingPostings`; nunca es
  actor y el procesador no usa sus permisos. Un hecho que originó el proceso de la cooperativa lleva
  `OriginActorKind = Process` y usuario central nulo (T11; sin nombres ficticios, CH3).
- No se crea usuario técnico. El cuatro ojos de la 009 no aplica: un comprobante de módulo se contabiliza al
  prepararse.
- Auditoría: `AuditBehavior` registra los comandos con su actor y origen; `AccountingAuditEmitter` agrega
  `Accounting.Lending.Posted`, `Accounting.Lending.Rejected`, `Accounting.Lending.BatchProcessed`,
  `Accounting.LendingRules.Imported` y `Accounting.RegulatoryIndicator.Registered`, escritos por el `PublicId` de la
  cooperativa; sin cooperativa resuelta, lanza. Contabilidad sigue fuera de la cadena de sellos (C1 de la 012); la
  auditoría de Cartera (módulo `Lending`, encadenado) registra la emisión de cada hecho con su operación.

## 23. Errores

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Accounting.LendingRule.Missing` | 422 / bandeja | sin regla vigente para un rol con importe | `operation, role, values, date, loans` |
| `Accounting.LendingRule.Overlaps` | 422 | vigencias cruzadas de una misma clave | la regla existente |
| `Accounting.LendingRule.RetroactiveOverPosted` | 422 | versión que empieza en o antes de lo ya contabilizado | `lastPostedDate, document` |
| `Accounting.LendingFact.Unbalanced` | bandeja | el hecho no cuadra o trae más de dos decimales; defecto del emisor | `difference` |
| `Accounting.LendingFact.CurrencyNotSupported` | bandeja | moneda distinta de COP o tasa distinta de 1 | `currency, exchangeRate` |
| `Accounting.LendingFact.WaitingForOriginal` | reintento | el original todavía no está contabilizado | `originalMessageId` |
| `Accounting.Period.LendingNotClosed` | 422, sin reconocimiento | cierre contable de un mes que Cartera, activada, no cerró | `month, activationDate, lastLendingClose` |
| `Accounting.Period.LendingPending` | 422, con reconocimiento | hechos de Cartera pendientes, en lote o rechazados en el mes | `pending, inBatch, rejected, oldestOperationDate, types` |
| `Accounting.Period.LendingLoadWillExpire` | 422, con reconocimiento | cerrar el mes deja sin activar la carga aplicada | `cutoffDate, loadPublicId` |
| `Accounting.Document.LendingCorrectsWithNewVoucher` | 422 | `PrepareReversalAsync` sobre algo de CAR | `origin` |
| `Accounting.Document.ModuleOwned` (existente) | 422 | reversar desde Comprobantes un comprobante CAR, con el mensaje de §9 | `origin` |
| `Accounting.Period.NotFound` (existente) | reintento, para CAR | mes sin abrir en Contabilidad, con alerta `Contabilidad.PeriodoInexistente` | `year, month` |
| `Integration.VersionNotAccepted` (existente) | bandeja | versión que el destino no acepta | `type, version` |
| `Lending.Prevalidation.NotPostable` (lado Cartera) | 422 | la validación previa encontró hallazgos y `FaltaDeRegla = Bloquear`, en una operación de pantalla | `errors[] { factType, role, combination, account, rule, whoFixes }` |
| `Lending.Prevalidation.NoResponse` (lado Cartera) | 422 | sin respuesta y `PoliticaSinRespuesta = Bloquear`, en una operación de pantalla | `seconds` |
| `Lending.Concentration.TechnicalCapitalUnavailable` (lado Cartera) | 422 | sin patrimonio técnico vigente, una operación que aumenta el endeudamiento (§15) | `lastCutoff` |

## 24. Ejemplos de asientos por hecho (para la contadora)

Con **roles y dimensiones, no cuentas**: la cuenta de cada línea la decide la matriz. Las cifras son ilustrativas (los
porcentajes de deterioro del Anexo 1 están «pendientes de validar»). Datos comunes: crédito **000123** de Ana,
línea `LIB01`, modalidad `CONSUMO`, sub-segmento `ConLibranza`, sucursal Principal, no empleada, sin gracia, no
reestructurado, categoría contable A. «Cruce» sólo aparece si la cuenta lo exige.

**1. Desembolso por transferencia con recogida de otro crédito** (`CreditoDesembolsado`, `DS`, 2026-11-03; el
crédito 000077 de Ana se recoge con 2.300.000)

| # | Rol · dimensiones | Débito | Crédito | Tercero | Cruce |
|---|---|---|---|---|---|
| 1 | `Capital` · CONSUMO · ConLibranza · A · LIB01 | 10.000.000,00 | | Ana | PG 000123 |
| 2 | `DesembolsoPorGirar` · `Transfer` | | 10.000.000,00 | Ana | |

La recogida es un pago del crédito 000077 por el contrato (`PagoAplicado`, clase `OwnLoanPayoff`, `RC`):

| # | Rol · dimensiones | Débito | Crédito | Tercero | Cruce |
|---|---|---|---|---|---|
| 1 | `RecaudoPorAplicar` · `OwnLoanPayoff` | 2.300.000,00 | | Ana | |
| 2 | `Capital` · CONSUMO · ConLibranza · A | | 2.250.000,00 | Ana | PG 000077 |
| 3 | `InteresPorCobrar` · CONSUMO · A · `CurrentInterest` | | 50.000,00 | Ana | PG 000077 |

Si la contadora lleva el puente `OwnLoanPayoff` a la misma cuenta que `DesembolsoPorGirar`, esa cuenta queda en
7.700.000, que es lo que la orden de giro le pide a Tesorería; Tesorería la cancela al girar.

**2. Causación del día** (`InteresCausado`, parte de la sucursal Principal, `CA` resumido del 2026-11-14)

| # | Rol · dimensiones | Débito | Crédito | Tercero | Cruce |
|---|---|---|---|---|---|
| 1 | `InteresPorCobrar` · CONSUMO · ConLibranza · A · `CurrentInterest` | 4.302,38 | | Ana | PG 000123 |
| 2 | `InteresPorCobrar` · CONSUMO · ConLibranza · A · `CurrentInterest` | 2.150,10 | | Luis | PG 000124 |
| 3 | `Ingreso` · CONSUMO · `CurrentInterest` | | 6.452,48 | | |

Las líneas 1 y 2 quedan una por crédito porque la cuenta exige tercero y cruce; la 3 se suma con la de todos los
créditos del día que resuelven la misma cuenta. El mismo comprobante trae la mora y el seguro del día.

**3. Pago por caja con anticipo** (`PagoAplicado` + `AnticipoRecibido`, una unidad, `RC`, 2026-11-20; Ana paga
700.000: la cuota vencida, 500.000, y 200.000 de anticipo)

| # | Rol · dimensiones | Débito | Crédito | Tercero | Cruce |
|---|---|---|---|---|---|
| 1 | `RecaudoPorAplicar` · `Receipts` | 700.000,00 | | Ana | |
| 2 | `Capital` · CONSUMO · ConLibranza · A | | 380.000,00 | Ana | PG 000123 |
| 3 | `InteresPorCobrar` · CONSUMO · A · `CurrentInterest` | | 105.000,00 | Ana | PG 000123 |
| 4 | `PagoPorCuentaDelAsociado` · CONSUMO · A · `Insurance` | | 15.000,00 | Ana | PG 000123 |
| 5 | `Anticipo` | | 200.000,00 | Ana | PG 000123 |

Recaudo, por su lado, contabiliza caja contra el mismo puente: el puente queda en cero.

**4. Anticipo aplicado al vencer la cuota** (`AnticipoAplicado`, `RC`, 2026-12-15; el anticipo no alcanza la cuota
y se aplica con la prelación, D6)

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `Anticipo` | 200.000,00 | |
| 2 | `PagoPorCuentaDelAsociado` · CONSUMO · A · `Insurance` | | 15.000,00 |
| 3 | `InteresPorCobrar` · CONSUMO · A · `CurrentInterest` | | 102.000,00 |
| 4 | `Capital` · CONSUMO · ConLibranza · A | | 83.000,00 |

**5. Reverso de un pago después de un cierre que cambió la categoría** (`PagoReversado`, `RC`; el cheque del pago
del ejemplo 3 vuelve devuelto el 2026-12-05, antes de que el anticipo se aplique; el cierre de noviembre ya pasó el
crédito a C)

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `Capital` · CONSUMO · ConLibranza · **C** | 380.000,00 | |
| 2 | `InteresPorCobrar` · CONSUMO · **C** · `CurrentInterest` | 105.000,00 | |
| 3 | `PagoPorCuentaDelAsociado` · CONSUMO · **C** · `Insurance` | 15.000,00 | |
| 4 | `Anticipo` | 200.000,00 | |
| 5 | `RecaudoPorAplicar` · `Receipts` | | 700.000,00 |

Fecha contable 2026-12-01 (noviembre está cerrado en Cartera, §8). El pago acreditó cuentas de A; el reverso debita
las de **C**, porque en diciembre el saldo vive en C (§9). Espejar A dejaría 500.000 en las cuentas de A, que el
cierre ya dejó en cero, y le faltarían a las de C. El interés
y la mora que el crédito habría causado sin el pago llegan aparte, como `AjusteDeCausacionPorFechaValor` en el `CA`
resumido.

**6. Descuento de la nómina propia** (`PagoAplicado`, clase `Payroll`, `DN`; fecha valor el fin de la quincena,
2026-11-30)

Nómina, en su `NM`: débito salarios por pagar 450.000; crédito el puente «recaudos por nómina para Cartera» 450.000
(cuentas del concepto `DESC_CARTERA`). Cartera:

| # | Rol · dimensiones | Débito | Crédito | Tercero |
|---|---|---|---|---|
| 1 | `RecaudoPorAplicar` · `Payroll` | 450.000,00 | | Ana |
| 2 | `Capital` · CONSUMO · ConLibranza · A | | 340.000,00 | Ana |
| 3 | `InteresPorCobrar` · CONSUMO · A · `CurrentInterest` | | 110.000,00 | Ana |

El puente queda en cero. Si Cartera rechaza el descuento, la línea del `NM` baja a cero en la misma aprobación y el
puente tampoco se mueve.

**7. Cierre de noviembre: el crédito pasa de A a C** (`CarteraReclasificadaPorCalificacion` + `DeterioroCalculado`,
`PV` resumido del 2026-11-30; saldos al 30: capital 9.000.000, interés 120.000, seguro 15.000; deterioro de A: capital
90.000, interés 1.200; deterioro en C: capital 1.800.000, interés 24.000, seguro 3.000)

| # | Hecho | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|---|
| 1 | Reclasificación | `Capital` · CONSUMO · ConLibranza · C | 9.000.000,00 | |
| 2 | | `Capital` · CONSUMO · ConLibranza · A | | 9.000.000,00 |
| 3 | | `InteresPorCobrar` · C · `CurrentInterest` | 120.000,00 | |
| 4 | | `InteresPorCobrar` · A · `CurrentInterest` | | 120.000,00 |
| 5 | | `PagoPorCuentaDelAsociado` · C · `Insurance` | 15.000,00 | |
| 6 | | `PagoPorCuentaDelAsociado` · A · `Insurance` | | 15.000,00 |
| 7 | | `DeterioroIndividual` · A · `Principal` | 90.000,00 | |
| 8 | | `DeterioroIndividual` · C · `Principal` | | 90.000,00 |
| 9 | | `DeterioroIndividual` · A · `CurrentInterest` | 1.200,00 | |
| 10 | | `DeterioroIndividual` · C · `CurrentInterest` | | 1.200,00 |
| 11 | Deterioro (`Increase`) | `GastoDeterioro` · `Individual` · CONSUMO · `Principal` | 1.710.000,00 | |
| 12 | | `DeterioroIndividual` · C · `Principal` | | 1.710.000,00 |
| 13 | | `GastoDeterioro` · `Individual` · CONSUMO · `CurrentInterest` | 22.800,00 | |
| 14 | | `DeterioroIndividual` · C · `CurrentInterest` | | 22.800,00 |
| 15 | | `GastoDeterioro` · `Individual` · CONSUMO · `Insurance` | 3.000,00 | |
| 16 | | `DeterioroIndividual` · C · `Insurance` | | 3.000,00 |
| | | **Sumas** | **10.962.000,00** | **10.962.000,00** |

En el CUIF el deterioro de consumo no separa la libranza (1445): si la contadora lleva `ConLibranza` y
`SinLibranza` a la misma auxiliar, el resumido las suma.

**8. Deterioro que baja en otro ejercicio** (`DeterioroCalculado`, `PV`, cierre de marzo de 2027; el crédito vuelve
a A y su deterioro de capital baja de 1.800.000 a 85.000)

Primero se reclasifica de C a A (como el ejemplo 7, al revés). Después, la disminución de 1.715.000: como el gasto de
ese crédito y concepto se reconoció en 2026, todo es `DecreasePriorYears`:

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `DeterioroIndividual` · A · `Principal` | 1.715.000,00 | |
| 2 | `RecuperacionDeterioro` · `Individual` · CONSUMO · `Principal` | | 1.715.000,00 |

Si el aumento hubiera sido en 2027, la disminución iría primero contra `GastoDeterioro` (`DecreaseSameYear`).

**9. Interés en suspenso y su cobro** (crédito 000200 de Luis, `CONSUMO`, `SinLibranza`, categoría D, con la
causación suspendida; `CausacionSuspendida` no tuvo comprobante)

Causación del día, en el `CA` resumido:

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `SuspensoEnOrden` · CONSUMO · D · `CurrentInterestInSuspense` | 3.000,00 | |
| 2 | `ContraOrdenContingente` | | 3.000,00 |

Un pago cubre 90.000 de interés en suspenso (`PagoAplicado` + `CobroDeInteresEnSuspenso`, `RC`):

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `RecaudoPorAplicar` · `Receipts` | 90.000,00 | |
| 2 | `Ingreso` · CONSUMO · `CurrentInterestInSuspense` | | 90.000,00 |
| 3 | `ContraOrdenContingente` | 90.000,00 | |
| 4 | `SuspensoEnOrden` · CONSUMO · D · `CurrentInterestInSuspense` | | 90.000,00 |

**10. Crédito nacido de una venta de Comercial** (`CreditoDesembolsado` en especie, `DS`; factura PV01-1532 por
1.200.000, línea `COM01`, `CONSUMO`, `SinLibranza`, crédito nuevo en A)

| Venta sellada | Débito | Crédito |
|---|---|---|
| «Contabilidad» (crédito provisional de la 012) | `Capital` · CONSUMO · SinLibranza · A · COM01 1.200.000,00 (PG + número del crédito) | `CuentaPorCobrarComercial` 1.200.000,00 (FV PV01-1532) |
| «Cartera» (después de activar, D16) | ídem | `PuenteComercial` 1.200.000,00 (FV PV01-1532) |

En los dos casos la cuenta por cobrar o el puente que debitó la venta queda en cero por factura: nunca dos veces
(FR-056, US7-6).

**11. Cobro provisional incorporado al activar** (`CobroProvisionalIncorporado`, `RC`; la venta del ejemplo 10 tuvo
un cobro en caja de 300.000 que Contabilidad registró contra la cuenta por cobrar provisional)

| # | Rol · dimensiones | Débito | Crédito | Cruce |
|---|---|---|---|---|
| 1 | `CuentaPorCobrarComercial` | 300.000,00 | | FV PV01-1532 |
| 2 | `Capital` · CONSUMO · SinLibranza · A · COM01 | | 300.000,00 | PG + número |

La cuenta por cobrar provisional: +1.200.000 (venta) − 300.000 (cobro en caja) − 1.200.000 (ejemplo 10) + 300.000
(este) = 0. El crédito queda en 900.000.

**12. La deudora pasa a ser empleada** (`DimensionContableCambiada`, emitida por el ciclo diario, `CN` resumido)

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `Capital` · CONSUMO · ConLibranza · C · empleado **sí** | 9.000.000,00 | |
| 2 | `Capital` · CONSUMO · ConLibranza · C · empleado **no** | | 9.000.000,00 |
| 3 | `DeterioroIndividual` · C · `Principal` · empleado **no** | 1.800.000,00 | |
| 4 | `DeterioroIndividual` · C · `Principal` · empleado **sí** | | 1.800.000,00 |

Más las líneas de intereses y pagos por cuenta. En el CUIF sembrado, el capital pasa de 1441 a 1469 («créditos a
empleados» le gana a la libranza, por eso empleado pesa más que sub-segmento).

**13. Condonación de mora** (`CondonacionAprobada`, `CN`, K3; 80.000 de mora en categoría B, con 4.000 de deterioro
de ese concepto)

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `DeterioroIndividual` · B · `DefaultInterest` | 4.000,00 | |
| 2 | `GastoDeCastigoOCondonacion` · `Waiver` · `DefaultInterest` | 76.000,00 | |
| 3 | `InteresPorCobrar` · B · `DefaultInterest` | | 80.000,00 |

**14. Castigo y recuperación** (`CreditoCastigado`, `PV`, K3; crédito 000300 en E: capital 3.000.000, interés
200.000, deterioro al 100 %, suspenso en orden 150.000)

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `DeterioroIndividual` · E · `Principal` | 3.000.000,00 | |
| 2 | `DeterioroIndividual` · E · `CurrentInterest` | 200.000,00 | |
| 3 | `Capital` · E | | 3.000.000,00 |
| 4 | `InteresPorCobrar` · E · `CurrentInterest` | | 200.000,00 |
| 5 | `CastigadaEnOrden` · `Principal` | 3.000.000,00 | |
| 6 | `CastigadaEnOrden` · `CurrentInterest` | 200.000,00 | |
| 7 | `ContraOrdenControl` | | 3.200.000,00 |
| 8 | `ContraOrdenContingente` | 150.000,00 | |
| 9 | `SuspensoEnOrden` · E · `CurrentInterestInSuspense` | | 150.000,00 |
| 10 | `CastigadaEnOrden` · `CurrentInterestInSuspense` | 150.000,00 | |
| 11 | `ContraOrdenControl` | | 150.000,00 |

Un pago de 500.000 sobre el castigado (`RecuperacionDeCastigada`, `RC`), con la prelación del crédito:

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `RecaudoPorAplicar` · `Receipts` | 500.000,00 | |
| 2 | `IngresoPorRecuperacion` | | 500.000,00 |
| 3 | `ContraOrdenControl` | 500.000,00 | |
| 4 | `CastigadaEnOrden` · `CurrentInterestInSuspense` | | 150.000,00 |
| 5 | `CastigadaEnOrden` · `CurrentInterest` | | 200.000,00 |
| 6 | `CastigadaEnOrden` · `Principal` | | 150.000,00 |

**15. Corrección de usura de un mes ya pagado** (corrida `NationalValueCorrection`; la usura de octubre se corrige a
la baja: se cobraron de más 12.000 ya pagados y se causaron de más 3.000 aún sin pagar)

| Hecho | Débito | Crédito |
|---|---|---|
| `SaldoAFavorReconocido` (`NationalValueCorrection`, `RC`) | `Ingreso` · CONSUMO · `CurrentInterest` 12.000,00 | `SaldoAFavor` 12.000,00 |
| `AjusteDeCausacionPorFechaValor` (`CA` resumido) | `Ingreso` · CONSUMO · `CurrentInterest` 3.000,00 | `InteresPorCobrar` · `CurrentInterest` 3.000,00 |

**Nota: capitalización en una reestructuración** (`CreditoReestructurado`, `CN`, K3; agregada el 2026-10-13, caso dorado
31; no es uno de los quince). Se capitalizan 96.400 de interés causado y 20.000 de interés en suspenso:

| # | Rol · dimensiones | Débito | Crédito |
|---|---|---|---|
| 1 | `Capital` · CONSUMO · SinLibranza · C · reestructurado | 116.400,00 | |
| 2 | `InteresPorCobrar` · C · `CurrentInterest` | | 96.400,00 |
| 3 | `IngresoDiferido` · CONSUMO | | 20.000,00 |
| 4 | `ContraOrdenContingente` | 20.000,00 | |
| 5 | `SuspensoEnOrden` · C · `CurrentInterestInSuspense` | | 20.000,00 |

Las líneas 4 y 5 cancelan la orden del suspenso capitalizado: **propuesta pendiente de validar por la contadora** (C7).

## 25. Enmienda de la 009 (E-01)

Todo lo que cambia fuera de la 013 para que la 009 diga lo que el código hace (Principio I). Se aplica en K1, en el
mismo cambio que publica el consumidor de Cartera, con el molde de la enmienda de la 012.

### 25.1 `specs/009-contabilidad-niif/spec.md`

| Dónde | Enmienda |
|---|---|
| Entrada (l.7) | Nota: «Cartera financiera llega por hechos de la bandeja con validación previa (feature 013, E-01)». |
| Alcance (l.131) | «… después Tesorería y CDT/Ahorros; Inventario y Cartera, por mensajes con su matriz de reglas (012, 013)». |
| US4, introducción (l.322) | «salvo Inventario y Cartera, que preguntan antes de confirmar y, si Contabilidad rechaza después, dejan el hecho en su bandeja». |
| US4, escenario 5 | «No aplica a Cartera: su fecha contable salta al primer mes abierto (013 FR-012); un rechazo por período cerrado llega a la bandeja y se recupera reabriendo y reprocesando». |
| US4, escenario 7 (l.360) | Quitar Cartera; agregar «Cartera: 013 US7». |
| FR-016 | Agregar «la matriz de reglas de Cartera (013 FR-129)». |
| FR-017 | Agregar «incluidas las reglas de la matriz de Cartera». |
| FR-022 | Agregar: «y MUST negarse a cerrar un mes que Cartera, activada, no haya cerrado, y avisar con reconocimiento auditado de los hechos de Cartera pendientes y de una carga aplicada que vencería (013 FR-146)». |
| FR-030 | Agregar: «Excepción: lo nacido de Cartera se corrige con un comprobante nuevo de su propio hecho y fecha, con las dimensiones vigentes del saldo (013 T55)». |
| FR-036 | Agregar: «Cartera no usa el contrato sincrónico: la atomicidad es operación + hecho en Cartera y comprobante + recibo en Contabilidad». |
| FR-037 | Agregar: «El origen de un comprobante resumido de Cartera es el lote (`LendingPostingBatch`)». |
| FR-038 | Agregar: «En Cartera, por su hecho de reverso o por la reapertura del cierre». |
| FR-039 | «Cartera contabiliza por el contrato a través de su consumidor (`PostLendingFactsCommand`), no desde sus comandos». |
| FR-040 | Cartera: `DS`, `RC`, `DN`, `CA`, `PV` y `CN`, mapeables por hecho y clase de recaudador; tercero y sucursal, los del hecho. |
| FR-041 | Agregar: «Hacia Cartera, en la validación previa (antes de confirmar) o en la bandeja (después), con el hecho, la combinación, la regla y quién corrige». |
| SC-004 | Para Cartera: «cero operaciones sin hecho y cero comprobantes de origen CAR sin recibo». |

### 25.2 `specs/009-contabilidad-niif/contracts/contabilizacion.md`

- §2: fila nueva «reversión de algo de CAR → `Accounting.Document.LendingCorrectsWithNewVoucher`».
- §3: nota «Cartera no usa este molde: ver `specs/013-cartera-financiera/contracts/contabilidad.md`».
- §4: las cinco filas de Cartera (desembolso `DS`, recaudo `RC`, causación `CA`, clasificación y provisión `PV`,
  descuento de nómina `DN`, con cuentas de `LND_CreditLineParameters`) se reemplazan por una: «**Cartera** · por
  hechos | `DS`, `RC`, `DN`, `CA`, `PV`, `CN` (`ACC_LendingVoucherMappings`) | por la matriz
  `ACC_LendingPostingRules` | el del hecho | `PG` + número del crédito, o `FV` en los roles de Comercial | 013
  `contracts/contabilidad.md`; corregir → comprobante nuevo».
- §5: «salvo lo de Inventario y lo de Cartera, que no se reversan: se corrigen con un comprobante nuevo».

### 25.3 Los demás artefactos de la 009

- `research.md`: R2 (Cartera no llama al contrato desde su operación); R16 (la entrada de Cartera pasa a «matriz
  `ACC_LendingPostingRules` por hechos (013); las cuentas como texto de la cartera heredada se retiran con
  `RetiroDeLaCarteraHeredada`»); R17 (E3 sin Cartera).
- `data-model.md`: en las referencias que impiden eliminar una cuenta, salen `LND_CreditLineParameters`,
  `LND_PortfolioAccounts` y `LND_ProvisionParameters` y entra `ACC_LendingPostingRules`; la semilla de tipos suma
  `CN`; sección nueva con las cuatro tablas de §4, remitiendo a la 013.
- `tasks.md` y `plan.md`: las tareas que dejaban a Cartera en E3 se marcan «reemplazadas por la 013»; E3 sin Cartera.
- `docs/manual/contabilidad-contrato-de-contabilizacion.md`: sección «Cartera: por hechos» (matriz, validación
  previa, modos, regla de cierre, comprobantes nuevos).
- `CLAUDE.md`, párrafo de Contabilidad: los escritores con el marcador «E3 (feature 009)» de Cartera desaparecen con
  el retiro; las cifras de tipos de comprobante (+1, `CN`) y de permisos contables (+6) se actualizan.

### 25.4 Código de la 009 y de la plataforma que cambia

`AccountingPoster` (guarda CAR en `PrepareReversalAsync`), `AccountingErrors` (los códigos de §23), `ReverseDocumentCommand`
(mensaje para CAR), `ClosePeriodCommand` (dos reconocimientos y la comprobación de §12), `EnlacesDeOrigen` en
Application y Shared (`LendingDocument`, `LendingPostingBatch`), `AccountReferenceFinder` (fuera la cartera heredada,
entra la matriz de Cartera, en el mismo commit del retiro), `ListInvalidParameterizationsQuery`, `VoucherTypesSeeder`
(`CN` y colisiones al log), `AccountingPermissionCatalogSeeder` (seis permisos), `AccountingAuditEmitter` (eventos de
§22), `IApplicationDbContext` (cuatro `DbSet`), `DestinoContabilidad` y `VersionesAceptadas` (por módulo, §6),
`UnidadDeConsumo.OriginModule`, `OrderIntegrationBatchCommand.OriginModule`, `ListPendingIntegrationMessagesQuery`,
`ClavesDeLote` (`FinDeCausacion`, `CierreDeCartera`), `BatchTrigger` (7 y 8), `PrevalidationOutcome` (`NotPostable`),
y la extracción a `Application/Accounting/Integration` de lo común del consumidor de Inventario (`ConjuntosDeCuentas`,
recibo, versión y moneda) sin cambiar su comportamiento.

## 26. Pruebas

| Prueba | Qué fija |
|---|---|
| `CarteraNoConoceContabilidad` (nueva) | Cartera no depende de `*.Accounting*` salvo `Common.Integration.Accounting`; `IContabilidadParaCartera` declara exactamente sus cinco métodos; `Application/Accounting/Lending` no depende de `Domain.Entities.Lending` ni de `Application.Lending` |
| `LosHechosDeCarteraNoLlevanCuentas` (nueva) | Ningún record de `Contracts/Lending` tiene propiedades de cuenta |
| `LoDeCarteraSeCorrigeConComprobanteNuevo` (nueva) | `PrepareReversalAsync` rechaza CAR; nadie de Cartera lo llama |
| `LaCarteraSoloSeTocaPorSusInterfaces` (nueva) | Los dos métodos de `IDimensionesDeCartera` |
| `UnSoloConsumidorPorDestino`, `EmisionesDeProcesoPermitidas`, `ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite` (nuevas) | §6, §2.1, §10.1 |
| `NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioXI_ContableImmutable`, `LaContabilidadNoTieneCuentasEnCodigo`, `LosComandosDeConsumoNoTienenRuta`, `LosHechosInmutablesNoSeModifican` | Siguen verdes; las dos últimas cubren `PostLendingFactsCommand`, `PostLendingSummaryGroupCommand` y `LendingPosting` |
| `Application.Tests/Accounting/Lending/*` (nuevas) | `MatrizVigenteDeCartera` (exigidas, opcionales, pesos, hecho `*`, vigencia, centro asignado); `ConstructorDeLineasDeCartera` con **casos dorados JSON por hecho** (los quince de §24 entre ellos, signos y traslados); consumo idempotente con colisión del índice; `AgrupadorDeResumidosDeCartera` (una línea por crédito en cuentas con tercero, sin netear, sucursal por línea); completitud; conjuntos de cuentas; `ClosePeriodCommand` con las tres comprobaciones de §12; validación previa por origen (§13.2) |
| `AccountingPosterTests`, pruebas de `ReverseDocumentCommand` y las que cuentan tipos y permisos sembrados | Guarda CAR, mensaje para CAR, +1 tipo y +6 permisos |
| `ContabilizacionDeCarteraPorMensajesTests` (e2e, colección «Cartera e2e», cooperativa aislada) | La prueba independiente de US7: desembolsar, causar, pagar y reversar en un día; cerrar el mes; comprobantes según la matriz, reclasificación por categoría y conciliación sin diferencias; un rechazo por regla faltante, creación de la regla y reproceso; período inexistente → reintento; `ElBalanceDePruebaCuadra` al final |
| `CarteraDesplegadaSinActivarTests` (e2e) | Contabilidad cierra como hoy sin `CarteraActivada`; con una carga aplicada avisa `LendingLoadWillExpire` |
| `NominaRecaudaPorElContratoTests` (e2e) | El `NM` con `DESC_CARTERA` contra el puente y el `DN` dejan el puente en cero; reversar la nómina reversa los dos |
| `CausacionDiariaPerformanceTests` (`RUN_PERF_TESTS=1`) | El `CA` resumido de 50.000 créditos con cuentas que exigen tercero (C5) |
| «Inventario e2e» | Sin cambios: el enrutador no tocó a Inventario |

## 27. Nombres fijados en este documento

Nombres que las transversales no traían y que este contrato fija con las reglas T1–T4. La §7 de las transversales
los aceptó (C21 para el vocabulario nuevo); ante una duda sobre roles y matriz, gana este contrato (§7.6).

- **Hechos y roles (sujetos a A3; los hechos son de C15, los roles de C91)**: `CreditoAprobadoSinDesembolsar`,
  `CupoRotativoOtorgado`, `GarantiaConstituida`; roles `ContingenteAcreedora`, `ContraContingenteAcreedora`.
- **Códigos cortos de los tipos de hecho** (§2.1, `LND_FactTypes.ShortCode` de la `ScheduleKey`, C06): `DES`, `DRV`, `ACO`, `ICA`, `MCA`, `SCA`,
  `AFV`, `PAP`, `PRV`, `ANR`, `ANA`, `CIS`, `SFR`, `SFS`, `RCA`, `DCC`, `CPI`, `DET`, `RCL`, `RCI`, `GCO`, `REE`,
  `MOD`, `NOV`, `CON`, `CAS`, `CSU`, `CRE`, `ACT`, `CAP`, `CIR`, `CRA`, `CAD`, `CRO`, `GCT`.
- **Motivos de la matriz** (`ReasonCode`): `Transfer`, `Check`, `ThirdPartyPayment`; `General`, `Additional`,
  `Individual`; `WriteOff`, `Waiver`; `ApprovedNotDisbursed`, `RevolvingLimit`, `CollateralReceived`.
- **Valores del contenido** que el constructor necesita (§2.3): movimiento del deterioro `Increase`,
  `DecreaseSameYear`, `DecreasePriorYears`, `Transfer` (el nivel sale de la línea); origen del saldo a favor
  `NoPrepaymentPreference`, `CommercialAdjustment`, `NationalValueCorrection`; forma de su resolución
  (`FormaDeResolucion`) `RefundThroughCollector`, `ApplyToOtherObligation`, `ApplyAsExtraordinaryPrincipal`. Son los
  valores de `mensajes.md` (C93).
- **Parámetros y enums**: valor `PorHecho` de `Contabilidad.Granularidad`; `PrevalidationOutcome.NotPostable = 4`;
  `NoVoucherReason = SameAccounts`; `EstadoDePeriodoContable { Open, Closed, NotFound }`.
- **Columnas**: `ACC_LendingPostings.OriginActorKind`, `LoanPublicId`, `LoanNumber` (y las demás de §4.3 calcadas de
  `ACC_InventoryPostings`); `ACC_LendingVoucherMappings.MappingKey`; `ACC_RegulatoryIndicators.IndicatorCode`
  (`TechnicalCapital`, `SolvencyRatio`), `CutoffDate`, `Value`, `Unit`, `Source`, `SupportAttachmentPublicId`,
  `SupersedesPublicId`; formato de `DimensionKey` (§3.1).
- **Adjunto**: tipo `RegulatoryIndicatorSupport` en `AdjuntosDeModulo`.
- **DTO de los puertos**: `ResultadoDeEvaluacionContableDto`, `HallazgoDeCarteraDto`, `CombinacionDeSaldo`,
  `SaldosMapeadosDeCarteraDto`, `ConjuntoDeCuentasDeCarteraDto`, `EstadoDelPeriodoDto`, `IndicadoresDeLaEntidadDto`,
  `IndicadorRegistradoDto`, `CuentaBancariaDeOrigenDto`; `CatalogoDeDimensionesDeCarteraDto`, `LineaDeCarteraDto`,
  `ModalidadDeCarteraDto`, `CombinacionDeCarteraEnUsoDto`; métodos `IDimensionesDeCartera.CatalogoAsync` y
  `.CombinacionesEnUsoAsync`.
- **Alerta**: `Cartera.HechoNoContabilizable`.
- **Auditoría** (propuestos por el frente contable, no estaban en las transversales):
  `Accounting.Lending.Posted`, `.Rejected`, `.BatchProcessed`, `Accounting.LendingRules.Imported`,
  `Accounting.RegulatoryIndicator.Registered`.
- **Carpeta y piezas**: `Application/Accounting/Integration` (lo común del consumidor; `DestinoContabilidad` se muda
  ahí); constantes `ClavesDeLote.FinDeCausacion` y `.CierreDeCartera`; campo `OrderIntegrationBatchCommand.OriginModule`;
  parámetros `AcknowledgeLendingPending` y `AcknowledgeLendingLoad` de `ClosePeriodCommand`.
- **Prueba e2e**: `ContabilizacionDeCarteraPorMensajesTests`.

## 28. Conciliación

Los puntos que este contrato dejó para conciliar se decidieron en `decisiones-transversales.md` §7; el cuerpo ya
aplica cada decisión.

| Punto | Decisión |
|---|---|
| CON-1 · hecho en `*` | C77: obligatorio en saldo, orden, pasivo, puente, diferido y contingentes; opcional con peso 2 en resultado (§3.1, §3.3) |
| CON-2 · centro de costo | C77: se asigna a la línea y no se compara; no pesa ni entra en `DimensionKey` (§3.3) |
| CON-3 · unidad | C01: una operación y su modo; los acompañantes sellan el del principal; un comprobante por unidad a partir del hecho principal (§2.1, §10.1) |
| CON-4 · modo de los reversos | C08: heredan el de su original (§10.1) |
| CON-5 · granularidad por hecho | C07: `PorHecho` (§10.1, §10.3) |
| CON-6 · disparador `Hora` | C06: se sella `HoraDiaria` (§10.2) |
| CON-7 · `ScheduleKey` con código corto | C06: `LND_FactTypes.ShortCode`, 3 a 4 letras, único (§2.1, §10.2) |
| CON-8 · alcance `FinDeCausacion` | C09: día siguiente al registro; recoge alcances anteriores sin lote; «tipo» = tipo de comprobante (§10.2, §10.4) |
| CON-9 · pérdidas por código | C78: clase 3 y naturaleza débito (§15) |
| CON-10 · vigencia de los indicadores | C33: Contabilidad devuelve el último con su corte; Cartera decide (§15) |
| CON-11 · `DN` para la recuperación | C79 (§5) |
| CON-12 · cruce `FV` en los dos roles de Comercial | C79 (§5) |
| CON-13 · contingentes | C15: `CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida`; roles `ContingenteAcreedora` y `ContraContingenteAcreedora` (C91); `AdmiteProceso = true`; condicionados a A3 (§2.1, §2.2, §19) |
| CON-14 · vocabulario nuevo | C21: aceptado (§13.2, §4.3, §4.4, §27) |
| CON-15 · alcance de `FaltaDeRegla` | C80 (§13.2) |
| CON-16 · contenido de `mensajes.md` | C22: lo trae `mensajes.md` (§2.3) |
| CON-17 · suspenso tras el castigo | C81: a `CastigadaEnOrden`, pendiente de validar (§2.2) |
| CON-18 · carpeta común | C82: `Application/Accounting/Integration` (§1.1) |
| — · usuario de origen del proceso | C23: «registrado por» = `Actor.NombreDelProceso` (§7.3, §22) |
| — · roles de saldo del contrato | C29: lista publicada en §16.1 |

---

## 29. Enmienda K6b (2026-10-17): la corrección monetaria UVR (E12)

Con K6b (vivienda Ley 546 y UVR; FR-024, D18) Cartera emite un hecho masivo nuevo, `CorreccionMonetariaUvrCausada` (código corto
`CMU`, `LND_FactTypes.DeliveredIn = K6b`, comprobante `CA`, modo de los masivos, admite proceso; `mensajes.md` §10.25). Rige la
**propuesta por defecto de E12** (DT §7.7) mientras la contadora no responda:

- **Sin rol nuevo**: el lado de resultado es el rol `Ingreso` con el hecho fijado en la regla (`hecho = CMU`, peso 2 de la matriz);
  el lado del saldo, el rol del concepto que cambió de valor, con las mismas dimensiones (y `valueUnit = Uvr`). La contadora crea la
  regla de `CMU` × `Ingreso` con la cuenta que elija para la corrección monetaria; sin ella, con `Contabilidad.FaltaDeRegla =
  Bloquear` el ciclo detiene su tanda y alerta, como con cualquier hecho sin regla. El hecho viaja con signo: una UVR que baja
  invierte los lados.
- **Comprobante**: el `CA` del día, resumido con la causación (una línea por crédito donde la cuenta exige tercero o cruce).
- **Conciliación**: el saldo en pesos de un crédito en UVR es Σ causado + Σ corrección − Σ pagado: sin este hecho, el cuadre de
  vivienda no cierra contra el libro.
- **Abierto**: si el certificado tributario incluye la corrección monetaria pagada junto a los intereses de vivienda (hoy no).

Lo fija el caso 50 de `ConstructorDeLineasDeCarteraTests` (`Casos/50-correccion-monetaria-uvr.json`).

---

## 30. Enmienda K6b (2026-10-17, decisión del dueño): el pago aplicado en un día cuya UVR se corrige

Corregir la `UVR` de un día ya causado encola la corrida `NationalValueCorrection` de los créditos en UVR (§29, `mensajes.md` §10.5). Si ese día
se aplicó un pago, lo recibido en pesos no cambia pero el pago cubre en pesos otra mezcla de conceptos. Hasta esta enmienda esa diferencia
viajaba mezclada en el `AjusteDeCausacionPorFechaValor` y su capital no tenía rol (concepto sin rol: el ajuste de causación sólo conoce
intereses, seguro y cargos contra su ingreso). Ahora Cartera la separa y la emite en un hecho masivo nuevo, `PagoRedistribuidoPorValorNacional`
(código corto `PRN`, `LND_FactTypes.DeliveredIn = K6b`, comprobante `CA`, modo de los masivos, admite proceso; `mensajes.md` §10.26):

- **Sin rol nuevo y sin el puente del recaudo**: es una redistribución entre los saldos del mismo crédito. Con el signo del libro, un importe
  positivo de lo que se debe va al **débito** de su rol de saldo (el pago lo cubrió de menos) y uno positivo del anticipo o del saldo a favor
  va al **crédito** de `Anticipo` o `SaldoAFavor` (el pago dejó más); un negativo invierte. Cuadra solo, porque lo recibido no cambia.
- **Suspenso y castigado**: lo cobrado del suspenso se reconoce como ingreso al cobrarlo (`CobroDeInteresEnSuspenso`), así que su
  redistribución mueve el `Ingreso` (concepto del suspenso, hecho `*` o `PRN`) y la orden (`SuspensoEnOrden` contra `ContraOrdenContingente`);
  en un crédito castigado lo cobrado es recuperación: `IngresoPorRecuperacion` y `CastigadaEnOrden` contra `ContraOrdenControl`.
- **Matriz**: todos son roles de saldo, orden o pasivo (hecho `*`) salvo los dos ingresos, que admiten hecho fijo; las reglas de `*` ya
  sugeridas en la plantilla los resuelven, así que **la plantilla no suma filas** para este hecho. Con `Contabilidad.FaltaDeRegla =
  Bloquear` y sin regla, la tanda de la corrida se detiene y alerta, como con cualquier hecho.
- **Comprobante**: el `CA` de la corrida, con la fecha contable del período abierto; el día valor del pago va en la línea (`valueDate`). El
  pago y su comprobante `RC`/`DN` no se tocan.
- **También la usura** (decisión del dueño del 2026-10-18): la corrida de una `USURA` separa el reparto igual que la de una `UVR` y lo emite
  en el mismo `PagoRedistribuidoPorValorNacional`: con la usura más baja el pago cubre menos interés y más capital (o deja más anticipo o saldo
  a favor). Antes el capital quedaba en el ajuste de causación, sin rol. **Efecto sobre C93**: el saldo a favor que deja un pago es parte de
  su reparto y viaja aquí —`SaldoAFavor` al crédito, el interés que el pago deja de cubrir al débito de `InteresPorCobrar`, que el ajuste de
  causación baja contra el `Ingreso`—; el asiento neto es el mismo que hacía `SaldoAFavorReconocido` con causa `NationalValueCorrection`
  (`Ingreso` contra `SaldoAFavor`), pero en dos hechos. La corrida ya no emite ese `SaldoAFavorReconocido` por un pago; queda sólo para un
  saldo a favor que no nazca de un reparto (hoy ninguno: el saldo a favor sólo lo crean pagos y ajustes de Comercial).

Lo fijan los casos 51 (UVR que sube: capital al débito, interés por cobrar al crédito) y 52 (UVR que baja con anticipo) de
`ConstructorDeLineasDeCarteraTests`, y en Cartera `RepartoDePagoConUvrCorregidaTests` y `RepartoDePagoConUsuraCorregidaTests` (Domain) y
`CorreccionDeValorNacionalTests`.

---

## 31. Enmienda (2026-10-18, decisión del dueño): las `reallocations` de un pago o un reverso

Un pago con fecha valor anterior (`PagoAplicado`) o el reverso de un pago (`PagoReversado`) puede cambiar lo que cubren los **pagos
posteriores** del mismo crédito: Cartera registra esa diferencia como `PaymentReallocation` de la operación y la manda en `reallocations`
(`mensajes.md` §10.7 y §10.8), pero hasta esta enmienda Contabilidad sólo leía `amounts` y la reasignación no se asentaba. Ahora:

- **Se asienta en el comprobante de la operación que la causó** (el `RC` o `DN` del pago o del reverso), no en un hecho aparte: nace en la
  misma transacción, la lleva el mismo mensaje (sus filas `PaymentReallocation` tienen el `MessagePublicId` del `PagoAplicado` o del
  `PagoReversado`) y la regla es un comprobante por unidad (C01).
- **Con el molde de `PRN`** (§30, `ConstructorDeLineasDeCartera.Redistribucion`): sin rol nuevo y sin el puente del recaudo, porque lo
  recibido por los pagos posteriores no cambia. Cada reasignación viene con el signo de la **distribución** (después − antes: positivo es que
  el pago posterior cubre más de un concepto o deja más anticipo o saldo a favor); el constructor la pasa al signo del libro —lo que se debe,
  invertido; el anticipo y el saldo a favor, igual— y la asienta: lo que el posterior cubre de menos vuelve al débito de su rol de saldo, el
  anticipo o saldo a favor que deja de más sale al crédito, el suspenso por `Ingreso` y su orden, un castigado por `IngresoPorRecuperacion` y
  `CastigadaEnOrden`.
- **Dimensiones**: las vigentes del hecho que la lleva (§9), como el resto de su comprobante; una reasignación suma cero por pago y cuadra sola.
- **Por qué no `PRN` ni un hecho hermano**: `PRN` es un hecho masivo por partes de una corrida, por lotes y en el `CA`; la reasignación es de
  una operación en línea y ya viaja en su mensaje. Un hecho nuevo habría duplicado el contenido, sumado una fila a `LND_FactTypes` y partido en
  dos comprobantes lo que es una sola operación; reutilizar el **constructor** de `PRN` da el mismo asiento sin tocar el catálogo.
- **Matriz**: `PagoAplicado` suma a sus roles los de la reasignación (`Ingreso`, `IngresoPorRecuperacion`, `SuspensoEnOrden`,
  `CastigadaEnOrden`, `Anticipo`, `SaldoAFavor` y las contras de orden); `PagoReversado` ya los tenía. Los de saldo, orden y pasivo se
  resuelven con la regla de hecho `*`; la plantilla no suma filas.
- **Fuera de esta enmienda**: las filas `PaymentReallocation` de un `CreditoAjustadoDesdeComercial` (un ajuste de Comercial que redistribuye
  pagos posteriores) no viajan en `reallocations` de su hecho; quedan anotadas como pregunta en `tasks.md`.

Lo fijan los casos 53 (pago con fecha valor anterior) y 54 (reverso) de `ConstructorDeLineasDeCarteraTests`.

