# Contrato contable de captaciones y aportes (enmienda E-03 de la 009)

**Feature**: 014 | **Fecha**: 2026-10-14 | **Entregas**: K7 (roles, dimensiones y hechos de aportes y retiro) · K8–K11
(los demás, declarados en K7) | **Base**: `research.md` R19, R10, R14, R15, R26; spec FR-096–FR-100, FR-123, US8,
P1, D23, D24 | **Molde**: `specs/013-cartera-financiera/contracts/contabilidad.md` (en adelante **«la 013»**) y el
código vigente `Application/Accounting/Lending/Reglas/{RolesDeCuentaDeCartera, OperacionesDeCartera}.cs` |
**Hermanos**: `mensajes.md` (qué trae cada hecho), `plantillas-y-archivos.md` (columnas de la plantilla de la matriz)

Las captaciones **no tienen un lado contable propio**: son hechos de la Cartera (emisor `CAR`) que entran por el mismo
destino (`DestinoContabilidad` → `PostLendingFactsCommand` / `PostLendingSummaryGroupCommand`), se resuelven en la
**misma matriz** `ACC_LendingPostingRules`, se validan con la misma validación previa, se concilian con la misma
conciliación y los vigila la misma guarda de cierre. Lo que esta enmienda suma es **datos en los catálogos fijos**
(hechos, roles, dimensiones, tipos de comprobante) y una columna por dimensión en la regla. **No hay un tercer
emisor** (C7 de la spec). El único camino al libro sigue siendo `AccountingPoster`.

Lo que **no** cambia: la frontera (`CarteraNoConoceContabilidad` en las dos direcciones, `LosHechosDeCarteraNoLlevanCuentas`,
que ahora recorre `Contracts/Lending/Deposits`), los pesos de especificidad de la 013 (Línea 512, Empleado 256,
Gracia 128, Sub-segmento 64, Reestructurado 32, PorCuentaDelAsociado 16, Sucursal 4, Hecho 2 sólo en roles de
resultado), el centro de costo que se asigna y no pesa (C77), la vigencia sin retroactividad sobre lo contabilizado,
las reglas de `AccountLineRules` y la inmutabilidad. **No se siembra ninguna regla**: la plantilla sugiere cuentas y
la contadora las confirma (P1).

---

## 1. Dimensiones nuevas (FR-097)

### 1.1 Al final de la clave posicional

`DimensionDeCartera` suma valores **al final** (nunca se renumera) y `ACC_LendingPostingRules` suma una columna por
dimensión, nula = cualquiera:

| Valor | Dimensión | Columna (texto, nula) | Nombre en la API y la plantilla | Valores admitidos | Fuente |
|---|---|---|---|---|---|
| 14 | `LineNature` | `LineNature` | `lineNature` | `Deposit`, `Contribution` (`Placement` no se usa en roles nuevos) | `LineNature` |
| 15 | `DepositClass` | `DepositClass` | `depositClass` | `Demand`, `Contractual`, `Permanent`, `TermDeposit` | `DepositClass` |
| 16 | `IssueTermBand` | `IssueTermBand` | `issueTermBand` | `UnderSixMonths`, `SixToTwelve`, `TwelveToEighteen`, `EighteenOrMore` | `IssueTermBand` |
| 17 | `ContributionClass` | `ContributionClass` | `contributionClass` | `Minimum`, `Ordinary`, `Extraordinary`, `Revalued` | `ContributionClass` |
| 18 | `AccountFlag` | `AccountFlag` | `accountFlag` | `Standard`, `GmfExempt`, `Garnished` | fija **(nuevo en contracts)** |

`DimensionKey` (300) suma cinco posiciones al final:

```text
{Operation}|{Role}|{CreditLineCode}|{ModalityCode}|{SubsegmentCode}|{RiskCategory}|{ConceptCode}|
{IsEmployee}|{InGracePeriod}|{OnBehalfOfAssociate}|{Restructured}|{CollectorClass}|{ReasonCode}|{BranchId}|
{LineNature}|{DepositClass}|{IssueTermBand}|{ContributionClass}|{AccountFlag}

*|DepositPrincipal|*|*|*|*|*|*|*|*|*|*|*|*|*|TermDeposit|SixToTwelve|*|Standard      capital de CDAT de 6 a 12 meses
*|InterestExpense|*|*|*|*|*|*|*|*|*|*|*|*|*|Demand|*|*|*                          gasto por intereses del ahorro a la vista
*|ContributionCapital|*|*|*|*|*|*|*|*|*|*|*|*|*|*|*|Ordinary|*                    aportes ordinarios
*|RecaudoPorAplicar|*|*|*|*|*|*|*|*|*|*|Payroll|*|*|*|*|*|*|*                       el puente de Nómina, el de la 013
```

**Migración de datos** (en la migración aditiva de la 014, no destructiva): toda regla existente recalcula su
`DimensionKey` con `|*|*|*|*|*` al final; el índice único filtrado `(DimensionKey, ValidFrom)` se conserva. Ninguna
regla de crédito cambia de resolución (los roles de la 013 no declaran las dimensiones nuevas).

### 1.2 Cómo pesan

**Las dimensiones nuevas no pesan**: son **exigidas por igualdad** cuando el rol las declara y van en `*` cuando no
(R19). Dos exigencias **condicionales** se resuelven en `RolesDeCuentaDeCartera.DimensionesDe(rol, motivo)` igual que
`GastoDeterioro` con `Individual` en la 013: el rol declara la condición y el constructor la evalúa con el valor del
hecho.

| Rol | Exige además | Cuando |
|---|---|---|
| `DepositPrincipal`, `DepositInactivePrincipal` | `IssueTermBand` | `DepositClass` ∈ { `TermDeposit`, `Contractual` } (el CUIF separa 2110 por plazo de emisión y 2125 por corto y largo plazo) |
| `DepositPrincipal` | `AccountFlag` | siempre (en las demás no se declara) |

`ConceptCode` admite, en los roles nuevos, los valores de `MemberAccountConcept` como texto
(`RolesDeCuentaDeCartera.ConceptosDeCaptacion` **(nuevo en contracts)**). `ReasonCode` admite el `OrderKind` en
`RefundPayable` y el `ChargeKind` en `ChargeIncome`. `CollectorClass` admite además `Treasury`
(`contrato-de-cuentas.md` §3.2).

### 1.3 Valores que pasan por `IDimensionesDeCartera`

`IDimensionesDeCartera.CatalogoAsync` devuelve las líneas **de las tres naturalezas** con su `lineNature` y su
`depositClass`, y las combinaciones en uso suman `(lineNature, depositClass, issueTermBand, contributionClass,
accountFlag)`. Contabilidad sigue sin leer tablas `LND_`.

---

## 2. Roles nuevos (FR-099, R19)

Se agregan **al final** de `RolesDeCuentaDeCartera.Todos` (26 → **43**). `FamiliaDeRol` suma `Captacion = 9` y
`Patrimonio = 10`; `TerceroNaturalDeCartera` suma `Titular = 6`; `CruceDeRol` suma `Cuenta = 3` (tipo de documento
cruce `CTA` con el número de la cuenta, §5) **(nuevos en contracts)**.

Abreviaturas: Nat = `LineNature`, Cls = `DepositClass`, Plz = `IssueTermBand` (condicional, §1.2), CAp =
`ContributionClass`, Mrc = `AccountFlag`, Mot = `ReasonCode`, Cla = `CollectorClass`. Opcionales: Lín (512), Suc (4),
Op (2, sólo resultado).

| # | Rol | Nat. | Familia | Exigidas | Opcionales | Tercero natural | Cruce | Sugerencia CUIF (pendiente de la contadora) |
|---|---|---|---|---|---|---|---|---|
| 27 | `DepositPrincipal` | C | Captacion | Cls, Mrc, Plz (cond.) | Lín, Suc | titular | cuenta | 210505 (a la vista activas); 211005, 211010, 211015, 211020 (CDAT por plazo de emisión); 212505 y 212515 (contractual corto y largo, activas); 213005 o 213010 (permanente) |
| 28 | `DepositInactivePrincipal` | C | Captacion | Cls, Plz (cond.) | Lín, Suc | titular | cuenta | 210510 (a la vista inactivas); 212510 y 212520 (contractual inactivas) |
| 29 | `InterestPayable` | C | Captacion | Cls | Lín, Suc | titular | cuenta | 210595, 211095, 212595, 213095 |
| 30 | `InterestExpense` | D | Resultado | Cls | Op, Lín, Suc | titular, si la cuenta lo exige | — | 615005, 615010, 615015, 615020 |
| 31 | `WithholdingPayable` | C | Pasivo | Cls | Suc | — | — | 243535 |
| 32 | `GmfPayable` | C | Pasivo | Cls | Suc | — | — | 243005, 243010, 243015, 243020 |
| 33 | `BonusExpense` | D | Resultado | Cls | Op, Lín, Suc | — | — | 615015 o una auxiliar propia (P26) |
| 34 | `ChargeIncome` | C | Resultado | Mot (`ChargeKind`) | Op, Lín, Suc | titular, si la cuenta lo exige | — | la de ingresos por servicios que fije la contadora (sin sugerencia) |
| 35 | `ChargeReceivable` | D | Saldo | — | Lín, Suc | titular | cuenta | la cuenta por cobrar que fije la contadora |
| 36 | `ContributionCapital` | C | Patrimonio | CAp | Lín, Suc | titular | cuenta | 310505 (ordinarios), 310510 (extraordinarios), 3110 (mínimos no reducibles), 310505 o la que fije para revalorizados |
| 37 | `RevaluationFund` | D | Patrimonio | — | Suc | — | — | 3310 (fondo para revalorización de aportes) |
| 38 | `SurplusFund` | D | Patrimonio | — | Suc | — | — | 3505 (excedentes) o la reserva que fije el acta |
| 39 | `RefundPayable` | C | Puente | Nat, Mot (`OrderKind`) | Lín, Suc | beneficiario | conviene que no lo exija | 246505 (aportes de exasociados), 246510 (depósitos de exasociados), 246515 (retornos decretados) |
| 40 | `UnclaimedPayable` | C | Pasivo | Nat | Suc | titular | cuenta | 246595 o la que fije la contadora |
| 41 | `GarnishmentPayable` | C | Puente | — | Suc | beneficiario (el juzgado) | — | la cuenta por pagar que fije la contadora |
| 42 | `PenaltyIncome` | C | Resultado | Cls | Op, Lín, Suc | titular, si la cuenta lo exige | — | la de otros ingresos que fije la contadora |
| 43 | `AbsorbedLoss` **(nuevo en contracts)** | C | Patrimonio | — | Suc | — | — | 3510 (pérdidas, DB) o la de resultados acumulados |

Precisiones:

- **Naturaleza crédito** en los roles de captación y patrimonio: mover un saldo de una combinación a otra
  (reclasificación) es débito a la anterior y crédito a la nueva, como en la 013.
- **`AbsorbedLoss`** es de naturaleza crédito porque recibe el **abono** que extingue la pérdida (la cuenta 3510 es
  débito; abonarla la reduce). La propuesta es «pendiente de validar» (FR-078, D15).
- **`RefundPayable`** es el **puente con Tesorería** de todas las órdenes de captaciones (como `DesembolsoPorGirar` en
  los créditos): el pago lo contabiliza Tesorería con su documento contra la misma cuenta. Conviene que no exija
  documento cruce (la completitud lo avisa).
- **`GmfPayable` y su clase**: `Cls` es la clase del **producto que genera el hecho generador**, no la de la cuenta
  debitada: la redención de un CDAT abonada en ahorro debita 2105 y acredita la cuenta del GMF «sobre CDAT»
  (243010); un retiro de ahorro, la «sobre depósitos de ahorro» (243005). `GmfDetailV1.generatingEvent` lo dice.
- **Ahorro permanente corto o largo** (213005, 213010): la línea no tiene plazo; la contadora fija una sola cuenta por
  regla o pide una dimensión nueva en una versión posterior.
- **Los aportes no causan intereses**: ningún hecho mueve `InterestExpense` ni `InterestPayable` con `Nat = Contribution`.
- **De concepto a rol** (`RolesDeCuentaDeCartera.RolDelConceptoDeCuenta` **(nuevo en contracts)**):

| `MemberAccountConcept` | Rol |
|---|---|
| `Principal` | `DepositPrincipal` (o `DepositInactivePrincipal` si `inactive = true`) |
| `InterestPayable` | `InterestPayable` |
| `Withholding` | `WithholdingPayable` (la contrapartida del concepto en el titular es el capital o el interés) |
| `Gmf` | `GmfPayable` |
| `ContributionMinimum` · `ContributionOrdinary` · `ContributionExtraordinary` · `ContributionRevaluation` | `ContributionCapital` con `CAp` = `Minimum`, `Ordinary`, `Extraordinary`, `Revalued` |
| `ChargeReceivable` | `ChargeReceivable` |
| `Bonus` | `InterestPayable` (la bonificación se reconoce como interés por pagar, §3 fila 18) |

---

## 3. Hecho × rol (FR-096)

Datos fijos en `OperacionesDeCartera.Todas`, agregados al final (35 → **68**). Clase B = de negocio con comprobante;
I = informativo con recibo y sin comprobante. El lado es el del importe positivo; un negativo lo invierte. Todos
cuadran solos (`Accounting.LendingFact.Unbalanced` es defecto del emisor). `EntregaDelHecho` suma `K7 = 7`, `K8 = 8`,
`K9 = 9`, `K10 = 10`, `K11 = 11` **(nuevo en contracts)**.

| # | Hecho | Corto | Clase | Débito | Crédito | Modo por defecto | Comp. | E |
|---|---|---|---|---|---|---|---|---|
| 1 | `ConsignacionEnCuentaAplicada` | CCN | B | `RecaudoPorAplicar` (Cla del recaudador) | `DepositPrincipal` | EnLinea | MA; DN si `Payroll` | K8 |
| 2 | `AporteRecibido` | APR | B | `RecaudoPorAplicar` (Cla) | `ContributionCapital` (CAp de cada concepto) | EnLinea | AO; DN si `Payroll` | K7 |
| 3 | `RetiroDeCuentaAplicado` | RCT | B | `DepositPrincipal` | `RecaudoPorAplicar` (Cla: el del recaudador; `OwnLoanPayoff` en el débito automático) o `RefundPayable` (Mot `DepositWithdrawal`, retiro por giro) | EnLinea | MA | K8 |
| 4 | `TrasladoEntreCuentasAplicado` | TRC | B | rol de saldo de la cuenta origen (`DepositPrincipal`, `DepositInactivePrincipal` o `ContributionCapital`) | rol de saldo de la cuenta destino | EnLinea | MA | K7 |
| 5 | `GmfRetenido` | GMF | B | `DepositPrincipal` | `GmfPayable` | el del principal | el del principal | K8 |
| 6 | `InteresDeCaptacionCausado` | ICC | B | `InterestExpense` | `InterestPayable` | PorLotes resumido, `FinDeCausacion` | CC | K8 |
| 7 | `AjusteDeCausacionDeCaptacion` | AJC | B | `InterestExpense` | `InterestPayable` (con signo: la reliquidación a la baja invierte) | el del principal; en la corrida, PorLotes resumido | CC | K8 |
| 8 | `InteresDeCaptacionLiquidado` | ILQ | B | `InterestPayable` | `DepositPrincipal` (abono o capitalización en la misma cuenta) | PorLotes resumido si es del proceso; EnLinea si es manual | CC | K8 |
| 9 | `RetencionPracticada` | RTF | B | `DepositPrincipal` (si el interés ya se abonó) o `InterestPayable` (si se paga desde el interés: CDAT, prórroga, redención) | `WithholdingPayable` | el del principal | el del principal | K8 |
| 10 | `CdatConstituido` | CDC | B | `RecaudoPorAplicar` (Cla) o `DepositPrincipal` de la cuenta de ahorro origen | `DepositPrincipal` (`TermDeposit`, Plz) | EnLinea | TD | K9 |
| 11 | `InteresesDeCdatPagados` | CDP | B | `InterestPayable` | `DepositPrincipal` de la cuenta destino o `RefundPayable` (Mot `TermDepositInterest`) | EnLinea | TD | K9 |
| 12 | `CdatProrrogado` | CDR | B | `InterestPayable` (lo capitalizado, neto) | `DepositPrincipal` (`TermDeposit`, Plz nuevo). Sin capitalización no lleva importes: recibo sin comprobante | EnLinea | TD | K9 |
| 13 | `CdatRenovado` | CDN | B | lo de `CdatProrrogado`; aumento: `RecaudoPorAplicar` o `DepositPrincipal` origen; disminución: `DepositPrincipal` (`TermDeposit`) | lo de `CdatProrrogado`; aumento: `DepositPrincipal` (`TermDeposit`); disminución: el destino | EnLinea | TD | K9 |
| 14 | `CdatRedimido` | CDD | B | `DepositPrincipal` (`TermDeposit`); `InterestPayable` (lo causado neto) | el destino: `DepositPrincipal` de ahorro, `RefundPayable` (Mot `TermDepositRedemption`) o `RecaudoPorAplicar` (Cla) | EnLinea | TD | K9 |
| 15 | `CdatCancelado` | CDX | B | ídem | ídem; pasa a la vista: `DepositPrincipal` (`Demand`) | EnLinea | TD | K9 |
| 16 | `PenalidadAplicada` | PEN | B | `InterestPayable` o `DepositPrincipal` (descuento fijo sobre el capital) | `PenaltyIncome` | el del principal | el del principal | K9 |
| 17 | `CaptacionReclasificada` | RCC | B | cada rol de saldo (`DepositPrincipal`, `DepositInactivePrincipal`, `InterestPayable`) con la combinación anterior | el mismo rol con la nueva (al ser de naturaleza crédito, el sentido se invierte respecto de la 013) | EnLinea si la emite una operación; PorLotes `FinDeCausacion` si la emite el proceso | CN | K8 |
| 18 | `BonificacionReconocida` | BON | B | `BonusExpense` | `InterestPayable` (se abona con la liquidación al vencimiento) | EnLinea | CC | K9 |
| 19 | `CargoDeCuentaCausado` | CGA | B | `DepositPrincipal` (cobrado) o `ChargeReceivable` (pendiente) | `ChargeIncome` (Mot `ChargeKind`) | EnLinea | MA | K8 |
| 20 | `CargoPendienteCobrado` | CGP | B | `DepositPrincipal` | `ChargeReceivable` | el del principal | el del principal | K8 |
| 21 | `CargoDeCuentaCondonado` | CGN | B | `ChargeIncome` | `ChargeReceivable` (pendiente) o `DepositPrincipal` (reintegro de uno cobrado) | EnLinea | MA | K8 |
| 22 | `RevalorizacionDeAportesAplicada` | RVA | B | `RevaluationFund` | `ContributionCapital` (`Revalued`) | EnLinea | AO | K10 |
| 23 | `ExcedentesDistribuidos` | EXD | B | `SurplusFund` | `ContributionCapital`, `DepositPrincipal` o `RefundPayable` (Mot `SurplusPayment`) según el destino | EnLinea | AO | K10 |
| 24 | `AportesAmortizados` | AMZ | B | `ContributionCapital` | `RefundPayable` (Mot `ContributionRefund`) | EnLinea | AO | K10 |
| 25 | `CruceAlRetiroAplicado` | CRR | B | `ContributionCapital`, `DepositPrincipal`, `DepositInactivePrincipal` | `RecaudoPorAplicar` (Cla `OwnLoanPayoff`): lo salda el `PagoAplicado` de su crédito, que debita el mismo puente **en el mismo comprobante** (D45, 2026-10-18) | EnLinea | RA | K7 |
| 26 | `PerdidaAplicadaAlRetiro` | PDR | B | `ContributionCapital` | `AbsorbedLoss` | el del principal | RA | K7 |
| 27 | `DevolucionOrdenada` | DVO | B | `ContributionCapital`, `DepositPrincipal`, `DepositInactivePrincipal` o `UnclaimedPayable` | `RefundPayable` (Mot `OrderKind`) o `DepositPrincipal` de otra cuenta (traslado) | EnLinea | RA | K7 |
| 28 | `DevolucionGirada` | DVG | I | — | — | Always | — | K7 |
| 29 | `SaldoTrasladadoANoReclamados` | SNR | B | `RefundPayable` (devolución no cobrada) o `DepositPrincipal`, `DepositInactivePrincipal` | `UnclaimedPayable` | EnLinea | MA | K10 |
| 30 | `EmbargoConsignado` | EMC | B | `DepositPrincipal` | `GarnishmentPayable` | EnLinea | MA | K8 |
| 31 | `OperacionDeCuentaReversada` | RVC | B | el espejo de lo que acreditó la unidad original, con las dimensiones **vigentes** (la 013 §9) | el espejo de lo que debitó | hereda del original | el del original | K7 |
| 32 | `CaptacionesActivadas` | CAV | I | — | — | Always | — | K7 |
| 33 | `CargaDeCaptacionesAplicada` | CAC | I | — | — | Always | — | K7 |

Reglas que valen para todos (además de las de la 013 §2.1):

- **Unidad**: una operación y su modo; los acompañantes (`mensajes.md` §2.1) sellan el modo del principal y salen en
  **un** comprobante, con el tipo del principal.
- **El débito automático netea el puente**: `RetiroDeCuentaAplicado` acredita `RecaudoPorAplicar` con `OwnLoanPayoff`, y el
  `PagoAplicado` del crédito (la 013) lo debita en la misma transacción, con su propio comprobante (`RC`). La conciliación
  del puente `OwnLoanPayoff` debe dar cero al cierre de cada día (§8).
- **El cruce al retiro y el pago del crédito van en el mismo comprobante** (decisión del dueño del 2026-10-18, D45; hasta
  esa fecha iban en dos, unidos por el puente): el `CruceAlRetiroAplicado` de cada crédito viaja en la unidad de su
  `PagoAplicado` (origen la operación de pago y su clave) y es el **principal**, así que el comprobante es del tipo del
  cruce (`RA`, editable en el mapeo hecho → tipo de cada cooperativa). El constructor de US8 (T143) arma en él el débito de
  aportes y ahorros por parte (`parts`) y el crédito de la cartera; el crédito y el débito del puente `OwnLoanPayoff` quedan
  en el mismo comprobante (se anulan entre sí). El **excedente** a devolver va por `DevolucionOrdenada` a
  `DevolucionPorPagar` (motivo `MembershipRefund`): la cuenta puente de Tesorería para pagarlo. Lo que el asociado sigue
  debiendo se queda en sus créditos (sin asiento) y quien liquida elige si pasan a caja.
- **Interés anticipado** (CDAT con `timing = Advance`): `InteresesDeCdatPagados` debita `InterestPayable` antes de
  causarlo; la cuenta queda con saldo débito durante el período y la causación la lleva a cero. Si la contadora quiere
  un diferido de gasto pagado por anticipado, es un rol nuevo en una versión posterior (pendiente, P1).
- **Penalidad por reliquidación** (`RepricingRate`): no es `PenalidadAplicada`; es `AjusteDeCausacionDeCaptacion` a la
  baja (devuelve gasto del ejercicio). La penalidad `FixedDiscount` sí es `PenalidadAplicada` contra `PenaltyIncome`.
  Pendiente de validar (P17).
- **IVA de los cargos**: la Cartera debita el cargo **sin IVA**; el IVA lo calcula, factura y registra Comercial
  (FR-128, D20 de la 013). Cómo se cobra el IVA al titular lo fija la enmienda de Comercial; pendiente de validar con
  la contadora.
- **Proceso como origen**: los de la columna «Proceso» de `mensajes.md` §1.1; el «registrado por» del comprobante es
  `Actor.NombreDelProceso`.
- **Saldo inicial**: la activación no emite hechos contables (FR-114); Contabilidad ya tiene sus saldos y la carga
  se cuadra contra ellos (§8.2).

---

## 4. La matriz y su plantilla

### 4.1 Regla, resolución y validación

La regla de la 013 §3.1 más las cinco columnas de §1.1. `ResolutorDeReglasDeCartera` y `MatrizVigenteDeCartera` no
cambian de algoritmo: las dimensiones nuevas se comparan por igualdad cuando el rol las exige (o la condición las
exige) y se ignoran cuando no. `ReglasDeLaMatrizDeCartera` suma: una dimensión exigida en `*` responde
`Accounting.LendingRule.RequiredDimension` (el de la 013) con el nombre de la dimensión nueva; un valor fuera de su
enum, `Accounting.LendingRule.InvalidDimensionValue` (el de la 013); una regla de rol de captación con
`lineNature = Placement`, `Accounting.LendingRule.NatureMismatch` **(nuevo en contracts)**.

### 4.2 Plantilla con filas sugeridas (P1)

`GetLendingRulesTemplateQuery` genera la hoja `Datos` de la matriz con **una fila sugerida por cada combinación
hecho × rol × valores exigidos** de las naturalezas activables por el perfil de la cooperativa, **sin cuenta** y con
la columna nueva `cuentaSugerida` **(nuevo en contracts)**: texto informativo que el lector **no** aplica. Las
sugerencias son **«sugerencia, pendiente de la contadora»** y salen de §2. Ninguna regla se crea sola.

Filas sugeridas (muestra; la plantilla las genera todas):

| operacion | rol | depositClass | issueTermBand | contributionClass | accountFlag | motivo / clase | cuentaSugerida | nota |
|---|---|---|---|---|---|---|---|---|
| `*` | `DepositPrincipal` | Demand | | | Standard | | 210505 | sugerencia, pendiente de la contadora |
| `*` | `DepositPrincipal` | Demand | | | GmfExempt | | 210505 | ídem (la marca no cambia de cuenta salvo decisión) |
| `*` | `DepositPrincipal` | Demand | | | Garnished | | 210505 | ídem |
| `*` | `DepositInactivePrincipal` | Demand | | | | | 210510 | ídem |
| `*` | `DepositPrincipal` | TermDeposit | UnderSixMonths | | Standard | | 211005 | ídem |
| `*` | `DepositPrincipal` | TermDeposit | SixToTwelve | | Standard | | 211010 | ídem |
| `*` | `DepositPrincipal` | TermDeposit | TwelveToEighteen | | Standard | | 211015 | ídem |
| `*` | `DepositPrincipal` | TermDeposit | EighteenOrMore | | Standard | | 211020 | ídem |
| `*` | `DepositPrincipal` | Contractual | UnderSixMonths, SixToTwelve | | Standard | | 212505 | corto plazo, activas |
| `*` | `DepositPrincipal` | Contractual | TwelveToEighteen, EighteenOrMore | | Standard | | 212515 | largo plazo, activas |
| `*` | `DepositInactivePrincipal` | Contractual | (corto) · (largo) | | | | 212510 · 212520 | inactivas |
| `*` | `DepositPrincipal` | Permanent | | | Standard | | 213005 o 213010 | la contadora elige |
| `*` | `InterestPayable` | Demand · TermDeposit · Contractual · Permanent | | | | | 210595 · 211095 · 212595 · 213095 | |
| `InteresDeCaptacionCausado` | `InterestExpense` | Demand · TermDeposit · Contractual · Permanent | | | | | 615005 · 615010 · 615015 · 615020 | |
| `*` | `WithholdingPayable` | (cada clase) | | | | | 243535 | |
| `*` | `GmfPayable` | Demand · TermDeposit · Contractual · Permanent | | | | | 243005 · 243010 · 243015 · 243020 | |
| `*` | `ContributionCapital` | | | Ordinary · Extraordinary · Minimum · Revalued | | | 310505 · 310510 · 3110 · 310505 | 3105xx según el CUIF sembrado |
| `*` | `RevaluationFund` | | | | | | 3310 | |
| `*` | `RefundPayable` | | | | | `MembershipRefund` (Contribution) · (Deposit) | 246505 · 246510 | |
| `*` | `RecaudoPorAplicar` | | | | | `Treasury` | (puente con Tesorería) | sin sugerencia |
| `*` | `RefundPayable` · `GarnishmentPayable` · `UnclaimedPayable` · `ChargeIncome` · `ChargeReceivable` · `PenaltyIncome` · `BonusExpense` · `SurplusFund` · `AbsorbedLoss` | | | | | | según §2 | la contadora decide |

La plantilla (columnas en `plantillas-y-archivos.md` §6) suma las columnas `naturaleza`, `claseDeCaptacion`,
`plazoDeEmision`, `claseDeAporte`, `marcaDeCuenta` y `cuentaSugerida`. Es una **versión nueva** de la plantilla de la
matriz que sigue leyendo la anterior (sin esas columnas, `*`).

### 4.3 Completitud

`GET /api/accounting/lending/completeness?date=` (la 013) suma, por cada naturaleza **activada o por activar**, las
combinaciones exigidas sin regla y las advertencias: `RefundPayable` o `GarnishmentPayable` que exigen documento cruce;
`DepositPrincipal` con `GmfExempt` o `Garnished` en otra cuenta que `Standard` (aviso informativo); el puente
`RecaudoPorAplicar` de `Treasury` sin regla. La activación de aportes o captaciones exige completitud de sus hechos
(`LinesWithMatrix`, `api.md` §16).

---

## 5. Tipos de comprobante y documento cruce (FR-123, E-03)

Cinco tipos nuevos en la semilla de `voucher-types.json` (`Usage = Module`, `Module = CAR`) y sus mapeos por defecto en
`lending-voucher-mappings.json` (`ACC_LendingVoucherMappings`, hecho y clase de recaudador → tipo):

| Código | Nombre | Hechos por defecto |
|---|---|---|
| `MA` | Movimientos de cuentas de captación y aportes | `ConsignacionEnCuentaAplicada`, `RetiroDeCuentaAplicado`, `TrasladoEntreCuentasAplicado`, `CargoDeCuentaCausado`, `CargoDeCuentaCondonado`, `SaldoTrasladadoANoReclamados`, `EmbargoConsignado` |
| `CC` | Causación de captaciones (**por lotes resumido por día**) | `InteresDeCaptacionCausado`, `AjusteDeCausacionDeCaptacion`, `InteresDeCaptacionLiquidado`, `BonificacionReconocida` |
| `TD` | Certificados de depósito de ahorro a término | `CdatConstituido`, `InteresesDeCdatPagados`, `CdatProrrogado`, `CdatRenovado`, `CdatRedimido`, `CdatCancelado` |
| `AO` | Aportes sociales y asamblea | `AporteRecibido`, `RevalorizacionDeAportesAplicada`, `ExcedentesDistribuidos`, `AportesAmortizados` |
| `RA` | Retiro de asociados | `CruceAlRetiroAplicado`, `DevolucionOrdenada` |

- `ConsignacionEnCuentaAplicada` y `AporteRecibido` con `CollectorClass = Payroll` van a `DN` (el de la 013 para
  Nómina). `CaptacionReclasificada` va a `CN` (el de la 013). Los acompañantes y `OperacionDeCuentaReversada` toman el
  del principal o del original.
- La semilla suma 5 tipos (24 → **29** de Cartera) y los mapeos de los 30 hechos de negocio. Los tipos `AH`
  («Intereses de ahorros») y `CD` («Liquidación de intereses de CDT») que la 009 sembró para el módulo heredado
  **no se usan**: quedan inactivos al retirar lo heredado (decisión de la contadora si se borran de la semilla).
- **Documento cruce**: tipo nuevo `CTA` «Cuenta de captación o aportes» en `cross-document-types.json`
  **(nuevo en contracts)**; el número es el de la cuenta (`MemberAccountRefV1.accountNumber`), o el del título del
  CDAT si la contadora lo pide en el mapeo. Los roles con `CruceDeRol.Cuenta` lo llevan sólo si la cuenta contable
  exige documento.

---

## 6. Modo de paso (FR-098, D24)

- **Por defecto**: `EnLinea` todo, salvo la causación, la liquidación periódica y su retención (`PorLotes` resumido,
  disparador `FinDeCausacion`, un comprobante `CC` por día con la sucursal en cada línea y una línea por cuenta donde
  la cuenta contable exige tercero o cruce, molde de la 013 §10.4), los informativos (`Always`) y los reversos
  (heredan).
- **Sin contabilidad iniciada, `NoPasa`** (regla vigente de la plataforma, `ModoDePasoVigente`); un modo guardado a
  propósito se respeta.
- El modo se sella en la entrega al emitir (`Contabilidad.ModoDePaso`, ámbito `LendingFactType`). Los acompañantes no
  se ofrecen en ese ámbito (C01).

## 7. Validación previa (D23, enmienda A8 de la 013)

`IContabilidadParaCartera.EvaluarAsync` sin cambio de firma. **Bloquea sólo en las pantallas de la Cartera**
(`Lending.Prevalidation.NotPostable` / `.NoResponse`: apertura con depósito por traslado, retiro por giro, traslado,
cancelación, constitución y redención del CDAT, retiro del asociado, asamblea, entrega, embargo consignado); en el
contrato de cuentas y en los procesos **aplica, sella y alerta** (`Cartera.HechoNoContabilizable`). US8-2.

## 8. Conciliación, cierre y correcciones (FR-100)

### 8.1 Conciliación por conjuntos de cuentas

`ConciliacionContableDeCartera` suma las familias `Captacion` y `Patrimonio` y los pasivos y puentes nuevos. Por
**fecha contable**, la Cartera agrupa su saldo por combinación `(rol, línea, clase, plazo de emisión, clase de aporte,
marca, oficina)` desde `LND_MemberAccountDailyPositions`; Contabilidad devuelve los saldos de las cuentas que la matriz
vigente asigna a esas combinaciones (`SaldosDeCuentasMapeadasAsync`) y las une en **conjuntos** (unión-búsqueda
combinación ↔ cuenta, la 013 §14). Cada diferencia se explica hecho por hecho: pendientes, en lote, rechazados, no
pasados. `ReconciliationDto.sets[].family` admite `Captacion` y `Patrimonio`; `combinations[]` suma las dimensiones
nuevas. Puentes que deben cuadrar en cero al cierre del día: `RecaudoPorAplicar` con `OwnLoanPayoff` (débito
automático y cruce) y, con su Tesorería al día, `RefundPayable` y `GarnishmentPayable` contra lo girado. Un descuadre
sin explicar alerta `Cartera.DescuadreContable` (SC-008).

### 8.2 Cuadre de activación (FR-115)

`GetDepositMigrationBalanceCheckQuery` (`api.md` §16) compara los saldos de la carga **por línea, concepto y conjunto
de cuentas** con `SaldosDeCuentasMapeadasAsync` al corte. Las diferencias sólo se aceptan con motivo y acta.

### 8.3 Guarda de cierre (E-03)

- El cierre mensual de la Cartera incluye la **sección de captaciones**: cierre de causación y de posición a fin de
  mes, sin SARC (R10). Emite el mismo `CierreDeCarteraRealizado`.
- `Accounting.Period.LendingNotClosed` (sin reconocimiento) **cubre las captaciones**: Contabilidad no cierra un mes en
  que la Cartera, con aportes o captaciones activados con fecha ≤ fin de mes, no cerró (US8-3); el mensaje dice el día
  de causación pendiente.
- Avisos reconocibles `Accounting.Period.LendingPending` (hechos de captaciones pendientes, en lote o rechazados) y
  `.LendingLoadWillExpire` (carga de captaciones por vencer), leyendo sólo `ACC_LendingPostings` y las entregas.

### 8.4 Correcciones

Lo de captaciones **no se reversa desde Comprobantes** (FR-099, US8-4): `Accounting.Document.ModuleOwned` con el
mensaje propio de la Cartera y `Accounting.Document.LendingCorrectsWithNewVoucher` en `PrepareReversalAsync`. Se
corrige con `OperacionDeCuentaReversada` desde la Cartera: comprobante nuevo con su propia fecha contable y las
dimensiones vigentes del saldo.

---

## 9. Ejemplos de asientos (para la contadora; cuentas sugeridas, pendientes de validar)

**CDAT de 10.000.000 a 180 días, 10 % EA, base 360, intereses al vencimiento** (investigación §3.2; sin retención si
el umbral diario aplica al CDAT, P2; con retención del 4 %, 19.524 sobre 488.088):

| Hecho | Débito | Crédito |
|---|---|---|
| `CdatConstituido` (desde ahorro) | 210505 · 10.000.000 | 211010 · 10.000.000 |
| `GmfRetenido` (si el traslado ahorro → CDAT está gravado, D32) | 210505 · 40.000 | 243005 · 40.000 |
| `InteresDeCaptacionCausado` (resumido por día, 180 días; el último día absorbe el redondeo contra el plan) | 615010 · 488.088 | 211095 · 488.088 |
| `CdatCancelado` al vencimiento, abonado en la cuenta de ahorro | 211010 · 10.000.000; 211095 · 488.088 | 210505 · 10.488.088 |
| `GmfRetenido` de la redención abonada en cuenta (D32; `GmfPayable` con la clase del CDAT, investigación §3.9) | 210505 · 41.952 | 243010 · 41.952 |

Con pago **mensual vencido** el ciclo cambia: seis `InteresesDeCdatPagados` de 79.741 (211095 / 210505) y el
`CdatCancelado` sólo mueve el capital.

**Ahorro a la vista, mes con interés de 6.884 y retención del 7 % sólo si algún día alcanza el umbral** (no lo alcanza
en el ejemplo): `InteresDeCaptacionCausado` 615005 / 210595 por 6.884 en el mes; `InteresDeCaptacionLiquidado` 210595 /
210505 por 6.884; sin `RetencionPracticada`.

**Retiro de 400.000 de una cuenta no exenta** (US4-3): `RetiroDeCuentaAplicado` 210505 / puente de Recaudo 400.000;
`GmfRetenido` 210505 / 243005 1.600.

**Retiro del asociado** (US10): aportes 3.000.000, ahorro 1.000.000, crédito 2.500.000:
`CruceAlRetiroAplicado` 310505 · 2.500.000 / puente `OwnLoanPayoff` 2.500.000 (el `PagoAplicado` del crédito debita el
puente y acredita la cartera); `DevolucionOrdenada` 310505 · 500.000 y 210505 · 1.000.000 / 246505 · 500.000 y
246510 · 1.000.000; Tesorería paga contra 2465.

**Revalorización al 4 % (limitada por IPC) sobre un fondo aprobado**: `RevalorizacionDeAportesAplicada` 3310 / 310505.

---

## 10. Enmienda E-03 de la 009

- `specs/009-contabilidad-niif/spec.md`: los hechos de la Cartera (FR-036 y FR-039 enmendados por la 013) incluyen
  los de captaciones y aportes; los tipos `MA`, `CC`, `TD`, `AO`, `RA`; la guarda de cierre cubre la causación de
  captaciones.
- `contracts/contabilizacion.md` de la 009: el catálogo de módulos sigue con `CAR` como único emisor de la Cartera.
- Código de la 009 que cambia: `RolesDeCuentaDeCartera` (17 roles, `ConceptosDeCaptacion`, `RolDelConceptoDeCuenta`,
  condición de `IssueTermBand`), `DimensionDeCartera` (cinco valores), `FamiliaDeRol`, `TerceroNaturalDeCartera`,
  `CruceDeRol`, `OperacionesDeCartera` (33 hechos), `EntregaDelHecho`, `LendingPostingRule` (cinco columnas y la
  clave), `ConstructorDeLineasDeCartera` (bloques `Deposit*`), `ConciliacionContableDeCartera`, las semillas
  `voucher-types.json`, `lending-voucher-mappings.json`, `cross-document-types.json`, y la plantilla de la matriz.

## 11. Pruebas

`OperacionesDeCartera` cotejado con la semilla `LND_FactTypes` (los 33 nuevos); `CarteraNoConoceContabilidad` y
`LosHechosDeCarteraNoLlevanCuentas` sobre `Contracts/Lending/Deposits`; casos de Application del constructor: cada
hecho cuadra solo, el resumido de un día con 100.000 cuentas en un comprobante por sucursal, la condición de
`IssueTermBand`, el débito automático que netea el puente; e2e «Captaciones e2e» (US8): matriz completa, consignar,
causar un mes, liquidar, conciliación en cero; borrar una regla y ver el bloqueo en la pantalla y el sello en el
contrato; cierre contable impedido sin el cierre de la Cartera; reverso desde Comprobantes rechazado.

---

## 12. Nombres fijados en este documento (nuevo en contracts)

- Valores de enum: `DimensionDeCartera.{LineNature = 14, DepositClass = 15, IssueTermBand = 16, ContributionClass = 17,
  AccountFlag = 18}`; `FamiliaDeRol.{Captacion = 9, Patrimonio = 10}`; `TerceroNaturalDeCartera.Titular = 6`;
  `CruceDeRol.Cuenta = 3`; `EntregaDelHecho.{K7 … K11}`.
- Columnas de `ACC_LendingPostingRules`: `LineNature`, `DepositClass`, `IssueTermBand`, `ContributionClass`,
  `AccountFlag`; valores de `AccountFlag`: `Standard`, `GmfExempt`, `Garnished`.
- Rol `AbsorbedLoss` (el 43.º; los otros 16 los nombra R19).
- Miembros `RolesDeCuentaDeCartera.ConceptosDeCaptacion`, `.RolDelConceptoDeCuenta`.
- Tipos de comprobante: nombres de `MA`, `CC`, `TD`, `AO`, `RA` (los códigos los fija R19); tipo de documento cruce
  `CTA`.
- Error `Accounting.LendingRule.NatureMismatch`; columna de plantilla `cuentaSugerida` y las cinco de dimensión.
- Motivos (`ReasonCode`) de `RefundPayable`: los `OrderKind` de `mensajes.md` §5.3; de `ChargeIncome`: los
  `ChargeKind`.

## 13. Conciliación

| Punto | Decisión |
|---|---|
| R19 dice «dimensiones exigidas por igualdad cuando el rol las declara» y el CUIF separa 2110 y 2125 por plazo pero no 2105 | Exigencia **condicional** de `IssueTermBand` sólo en CDAT y contractual, con el mecanismo que ya existe para el deterioro individual |
| `AccountFlag` y `DepositInactivePrincipal` a la vez | La inactividad cambia de **rol** (el CUIF tiene subcuentas de inactivas); embargo y exención cambian de **marca** dentro del mismo rol, y la plantilla sugiere la misma cuenta |
| Nombres de roles en inglés (R19) frente a los de la 013 en español (`Capital`, `InteresPorCobrar`) | **Rige research C10**: en el código los roles nuevos van en español (`CapitalDeCaptacion`, `InteresPorPagar`…); los nombres en inglés de este documento son referencias |
| Pérdidas al retiro (FR-078) sin rol en R19 | Rol nuevo `AbsorbedLoss`, pendiente de validar con la contadora |
| Amortización de aportes | El hecho sólo mueve el aporte del asociado contra el puente; el traslado del fondo de amortización (3305) a aportes amortizados lo registra la contadora con el acta (fuera de la Cartera) |
