# Contrato: plantillas de migración, layouts regulatorios y archivos de pagaduría

**Feature**: 014 | **Fecha**: 2026-10-14 | **Entregas**: K7 (líneas de aportes, cuentas y cuotas de aportes, convenio
de pagaduría ampliado) · K8 (líneas y cuentas de captación, bloqueos y embargos, GMF, tributario del año) · K9 (CDAT y
contractual) · K11 (SICSES y exógena) | **Base**: `research.md` R17, R24, R26, R27; spec FR-111–FR-118, FR-125, FR-126,
US9, US17 | **Molde**: `specs/013-cartera-financiera/contracts/plantillas.md` y `adaptadores-y-archivos.md` §6–§7 (en
adelante **«la 013»**) | **Hermanos**: `api.md` §3, §16, §18; `contabilidad.md` §4

**El modelo de cada plantilla se congela antes de publicarla** (la 013 §0): cambiar una columna después exige una
versión nueva de la plantilla que siga leyendo la anterior. Ninguna plantilla, columna ni instrucción nombra al
sistema anterior: se habla de «lo heredado», «código heredado» y «cifras de referencia».

---

## 1. Reglas comunes

Rige la 013 §0 sin cambios: libro `.xlsx` de `PlantillaDeImportacion` con **sólo encabezados en la fila 1** y la hoja
«Instrucciones» al final; encabezados `camelCase` en español; tipos de valor de la 013 §0.4 (`Codigo`, `Texto`,
`Entero`, `Monto`, `Porcentaje` en puntos, `Fecha`, `SiNo`, `Lista`, `Enumeracion`, `Persona`, `Sucursal`); `review`
no guarda; `apply` es **todo o nada** con 422 `Import.Invalid` y `data.errors[] { row, column, code, message }`;
**ninguna plantilla crea personas** (un documento que no está vivo en `COR_People` es error de fila y columna, I-1);
mismos códigos de error que el alta unitaria; alcance de oficina y línea de quien importa; `Idempotency-Key` en
revisar y aplicar; auditoría `Import.Reviewed`/`.Applied` con SHA-256; descarga con datos con
`Lending.Reports.ExportPersonalData` cuando trae personas.

Tipos de valor que suma este contrato **(nuevo en contracts)**:

| Tipo | Cómo se escribe | En captaciones |
|---|---|---|
| `NumeroDeCuenta` | texto 20, `^[A-Za-z0-9-]+$`, se guarda en mayúsculas | El número que **conserva** la cuenta migrada; único en la carga y frente a las cuentas vivas |
| `Periodo` | `AAAA-MM` o `AAAA-MM-Qn` (quincena) | Cuotas exigibles |
| `Plazo` | entero en días | CDAT |

### 1.1 Las plantillas y su orden de carga

| # | Clave | Hojas | Ruta base | Comando | Naturaleza | E |
|---|---|---|---|---|---|---|
| 1 | `lending.contribution-lines` | `Lineas`, `Equivalencias` | `/api/lending/lines/contribution` | `ImportContributionLinesCommand` | Contribution | K7 |
| 2 | `lending.deposit-lines` | `Lineas`, `Tramos`, `Cargos`, `ReglasDeTasaNegociada`, `Equivalencias` | `/api/lending/lines/deposit` | `ImportDepositLinesCommand` | Deposit | K8 |
| 3 | Matriz contable (versión 2 de la plantilla) | `Datos` | `/api/accounting/lending/rules` | `ImportLendingPostingRulesCommand` | ambas | K7 |
| 4 | `lending.payroll-agreements` (versión 2) | `Convenios`, `Contactos`, `CodigosDeRespuesta`, `GruposDeArchivo`, `ConceptosEquivalentes`, `MiembrosDelConcepto`, `EquivalenciasDeIdentificacion` | `/api/lending/payroll-agreements` | `ImportPayrollAgreementsCommand` | ambas | K7 |
| 5 | `lending.member-accounts` | `Datos` | parte de la carga | `ImportDepositMigrationPartCommand` | Deposit | K8 |
| 6 | `lending.member-account-parties` | `Datos` | parte | ídem | ambas | K7 |
| 7 | `lending.term-deposits` | `Datos` | parte | ídem | Deposit | K9 |
| 8 | `lending.contribution-accounts` | `Datos` | parte | ídem | Contribution | K7 |
| 9 | `lending.account-installments` | `Datos` | parte | ídem | ambas | K7 |
| 10 | `lending.holds-and-garnishments` | `Datos` | parte | ídem | ambas (aportes: sólo garantía) | K8 |
| 11 | `lending.gmf-exemptions` | `Datos` | parte | ídem | Deposit | K8 |
| 12 | `lending.tax-year-to-date` | `Datos` | parte | ídem | Deposit | K8 |

Las partes 5 a 12 se importan en una **carga de migración por naturaleza** (`api.md` §16) con la ruta
`/api/lending/deposits/migration/loads/{id}/parts/{part}`, donde `{part}` es la clave sin `lending.`
(`member-accounts`, `member-account-parties`, `term-deposits`, `contribution-accounts`, `account-installments`,
`holds-and-garnishments`, `gmf-exemptions`, `tax-year-to-date`). Importar a una carga una parte que no es de su
naturaleza responde `Lending.Migration.PartNotForNature`. Cada parte escribe su **tabla de preparación**
`LND_Migration*Row` (research §0.2) y admite **varios archivos** (`replace=true` reemplaza la parte, como en la 013
§5.1). Aplicar **sólo guarda la carga**: las cuentas nacen al activar, con una transacción de saldo inicial **por
concepto** fechada al corte y **sin hechos contables** (FR-114).

| Parte | Tabla de preparación **(nuevo en contracts, salvo la primera)** | Oblig. |
|---|---|---|
| `member-accounts` | `LND_MigrationAccountRows` | sí (captaciones) |
| `member-account-parties` | `LND_MigrationAccountPartyRows` | no |
| `term-deposits` | `LND_MigrationTermDepositRows` | si hay CDAT |
| `contribution-accounts` | `LND_MigrationContributionAccountRows` | sí (aportes) |
| `account-installments` | `LND_MigrationAccountInstallmentRows` | no |
| `holds-and-garnishments` | `LND_MigrationAccountHoldRows` | no |
| `gmf-exemptions` | `LND_MigrationGmfExemptionRows` | no |
| `tax-year-to-date` | `LND_MigrationTaxYearToDateRows` | no |

**La plantilla `lending.contribution-balances` de la 013** (D19) se acepta como **fuente** de los saldos de aportes:
la pantalla de la carga ofrece «Tomar de los saldos cargados» que convierte la última carga con corte = el corte de la
migración en filas de `contribution-accounts` (aportes en `aportesOrdinarios`, ahorro permanente como aviso para la
parte `member-accounts`), que luego se revisan y corrigen como cualquier parte (FR-113, Q1).

### 1.2 Lo que no tiene plantilla y debe existir antes

Además de lo de la 013 §0.2 (personas, oficinas con sucursal contable, bancos, valores nacionales replicados, perfil
regulatorio, órganos, roles y alcances, plan de cuentas): **los créditos activados** (R21: la activación de captaciones
o aportes exige créditos activados primero), las **personas de los beneficiarios y representantes** (se dan de alta
con el buscador común), los **soportes** de representación y de embargos como adjuntos (se citan por su
`attachmentPublicId` o se suben después), y los parámetros con fuente de research R26.

### 1.3 Permisos por plantilla

| # | Descargar vacía | Importar | Descargar con datos |
|---|---|---|---|
| 1 | `Lending.ContributionLines.View` | `Lending.ContributionLines.Manage` | `Lending.ContributionLines.View` |
| 2 | `Lending.DepositLines.View` | `Lending.DepositLines.Manage` | `Lending.DepositLines.View` |
| 3 | `Accounting.LendingRules.View` | `Accounting.LendingRules.Manage` | `Accounting.LendingRules.View` |
| 4 | `Lending.PayrollAgreements.View` | `Lending.PayrollAgreements.Manage` | `Lending.PayrollAgreements.View` |
| 5–12 | `Lending.Deposits.Migration` | `Lending.Deposits.Migration` | `Lending.Deposits.Migration` + `Lending.Reports.ExportPersonalData` |

---

## 2. Líneas de captación (`lending.deposit-lines`, FR-028, FR-127, FR-128)

Escribe `LND_CreditLines` (`Nature = Deposit`), `LND_DepositLineVersions`, `LND_DepositLineRateTiers`,
`LND_DepositLineCharges` y `LND_NegotiatedRateRules` con **las mismas reglas que el alta y la versión unitarias**
(`api.md` §3). Cada fila de `Lineas` es una versión: llave `codigo` + `vigenteDesde`; una posterior cierra la anterior
la víspera; una anterior a hoy en una línea con cuentas se rechaza (`Lending.DepositLine.Retroactive`). Las hojas
hijas cuelgan de `linea` + `vigenteDesde` y reemplazan las de la versión entera. Crear o cerrar una versión pide
`reason`.

### Hoja `Lineas`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | Único en **todo** el catálogo de líneas (cualquier naturaleza): `Catalogo.CodigoDuplicado` con el nombre de la existente (US1-4) |
| `vigenteDesde` | fecha | sí | |
| `nombre` | texto 120 | sí | |
| `clase` | `DepositClass`: `Demand`, `Contractual`, `Permanent`, `TermDeposit` | sí | Fija: no cambia entre versiones (`Lending.DepositLine.ClassImmutable`); el perfil regulatorio debe autorizarla (`Lending.Savings.LineNotAllowedByProfile`) |
| `activa` | sí/no | no (sí) | |
| `oficinas` | lista de sucursales o `*` | no (`*`) | |
| `exigeAsociado` | sí/no | no (sí) | FR-030; sólo «no» si el perfil capta de no asociados |
| `admiteCotitulares` · `admiteMenores` · `admiteRetirosParciales` | sí/no | no | |
| `tipoDeTasa` | `Fixed`, `Tiered`, `Variable` | sí | `Tiered` exige filas en `Tramos` |
| `tasaEA` | porcentaje | con `Fixed` | ≥ 0 |
| `modoDeTramos` | `RateTierMode`: `WholeBalance`, `Stepped` | con `Tiered` | Por defecto `WholeBalance` (D8) |
| `indice` · `claveDelIndice` · `margen` · `tasaPiso` · `tasaTecho` | como en las líneas de crédito | con `Variable` | Sin usura |
| `metodoDeSaldo` | `BalanceMethod`: `DailyBalance`, `AverageBalance`, `MinimumBalance` | no (`DailyBalance`, D31) | |
| `minimoParaAbrir` · `minimoParaMantener` · `minimoParaInteres` | monto | no (0) | |
| `convencionDeDias` | `DayCountConvention` | no (`Actual365`, D7) | |
| `equivalencia` | `RateEquivalence` | no (`Compound`) | |
| `redondeoUnidad` · `redondeoModo` | como en crédito | no (1, `AwayFromZero`) | El momento es siempre por liquidación (D9) |
| `periodicidadDeLiquidacion` | `Monthly`, `Quarterly`, `SemiAnnual`, `Annual`, `AtMaturity` | sí | |
| `claseTributaria` | `Demand`, `TermDeposit`, `Contractual`, `Permanent` | no (la clase) | Elige `RETENCION_TARIFA_*` |
| `aplicaGmf` | sí/no | no (sí) | |
| `retiroMaximoPorOperacion` · `retiroMaximoDiario` · `efectivoMaximo` | monto | no | |
| `diasParaInactividad` | entero | no (360, D21) | |
| `causaInactiva` | sí/no | no (sí) | |
| `plazoMinimoDias` · `plazoMaximoDias` | entero | con `TermDeposit` | |
| `periodicidadesDeIntereses` | lista: `Monthly`, `Bimonthly`, `Quarterly`, `SemiAnnual`, `Annual`, `AtMaturity` | con `TermDeposit` | |
| `modalidadesDeIntereses` | lista `InterestTiming`: `Arrears`, `Advance` | con `TermDeposit` | |
| `prorrogaAutomatica` · `maximoDeProrrogas` | sí/no · entero | con `TermDeposit` | D17: sí, sin máximo |
| `reglaDeTasaEnProrroga` | `RolloverRateRule`: `SameRate`, `CurrentTableRate`, `Agreed` | con `TermDeposit` | D17: `CurrentTableRate` |
| `diasDeGraciaHabiles` · `graciaCausa` | entero · sí/no | con `TermDeposit` | D17: 3, no |
| `trasVencimiento` | `AfterMaturityTreatment`: `StopAccruing`, `MoveToDemand` | con `TermDeposit` | `MoveToDemand` exige `lineaALaVista` |
| `lineaALaVista` | código | con `MoveToDemand` | Línea `Demand` de la misma carga o existente |
| `redencionAnticipada` | sí/no | con `TermDeposit` | D18: no |
| `tipoDePenalidad` · `tasaDePenalidadEA` · `porcentajeDePenalidad` | `RepricingRate`/`FixedDiscount` · porcentaje · porcentaje | con redención anticipada | |
| `prorrogaCapitaliza` | sí/no | con `TermDeposit` | D17: no |
| `cuotaMinima` · `periodicidadDeCuota` · `plazoMesesDesde` · `plazoMesesHasta` · `tasaBonificadaEA` · `cuotasToleradas` · `efectoIncumplimiento` (`LoseBonus`, `CancelAndRefund`) · `tasaCastigoEA` | | con `Contractual` | FR-074 |
| `admiteTasaNegociada` · `prorrogaConservaMargen` | sí/no | no (no, no; D34) | |
| `notas` | texto 400 | no | |

### Hoja `Tramos` (FR-028)

Llave `linea` + `vigenteDesde` + `saldoDesde` + `plazoDesde` + `montoDesde`. Columnas: `linea`, `vigenteDesde`,
`saldoDesde`, `saldoHasta`, `plazoDesde`, `plazoHasta` (días, CDAT), `montoDesde`, `montoHasta`, `tasaEA`. Los tramos
no se solapan (`Lending.DepositLine.TiersOverlap` **(nuevo en contracts)**) y cubren desde cero.

### Hoja `Cargos` (FR-128; apagados por defecto, D35)

Llave `linea` + `vigenteDesde` + `codigo`. Columnas: `codigo` (código 10), `tipo` (`ChargeKind`: `MaintenanceFee`,
`ExtraOperation`, `ExtraDocument`, `MediumReplacement`, `Inactivity`, `Other`), `nombre`, `valorFijo`, `porcentaje`,
`minimo`, `maximo`, `evento` (`Withdrawal`, `Deposit`, `Statement`, `Certificate`, `MediumReplacement`, `Periodic`),
`periodicidad`, `gratuitosPorPeriodo`, `gravadoConIva` (sí/no), `conceptoTributario` (código del catálogo de Core, con
gravado), `siNoAlcanza` (`KeepPending`, `Waive`), `exoneraciones` (lista: `AverageBalanceAtLeast:{monto}`,
`MinimumBalanceAtLeast:{monto}`, `SeniorityMonthsAtLeast:{n}`, `EmployeeHolder`, `GmfExemptAccount`, `MinorHolder`,
`Office:{código}`).

### Hoja `ReglasDeTasaNegociada` (FR-127)

Llave `linea` + `vigenteDesde` + `orden`. Columnas: `antiguedadMinimaMeses`, `montoDesde`, `montoHasta`, `plazoDesde`,
`plazoHasta`, `tasaBaseDesde`, `tasaBaseHasta`, `saldoDelTitularDesde`, `oficinas`, `canales`, `margenMaximoEA`
(porcentaje en puntos de tasa efectiva), `vigenteHasta`. Dos reglas con los mismos criterios y vigencia cruzada:
`Lending.NegotiatedRate.RuleOverlaps`.

### Hoja `Equivalencias` (FR-113)

La equivalencia de cada producto heredado con la línea nueva, para que la migración resuelva la línea. Llave
`codigoHeredado`. Columnas: `codigoHeredado` (texto 20), `descripcionHeredada`, `linea` (código de esta plantilla o
existente), `clase`. Una cuenta heredada sin equivalencia es error en la carga (Edge Cases: «se mapea antes de
cargar»). Escribe `LND_DepositLineLegacyEquivalences` **(nuevo en contracts)**.

## 3. Línea de aportes (`lending.contribution-lines`, FR-029)

Escribe `LND_CreditLines` (`Nature = Contribution`) y `LND_ContributionLineVersions`. Llave `codigo` + `vigenteDesde`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` · `vigenteDesde` · `nombre` · `activa` | | sí | Como en §2 |
| `minimoNoReducible` | monto | sí | FR-042 |
| `tipoDeCuota` | `FixedAmount`, `IncomePercent`, `MinimumWageFraction` | sí | |
| `valorDeCuota` | monto o porcentaje según el tipo | sí | `IncomePercent` en puntos (3 = 3 %); `MinimumWageFraction` como fracción del SMMLV |
| `redondeoDeCuota` | `0,01`, `1`, `100`, `1000` | no (100) | |
| `periodicidad` | `Monthly`, `Biweekly`, `Weekly`, `TenDay` | sí | |
| `lineaDeAhorroPermanente` · `porcentajeAPermanente` | código · porcentaje | no | Reparto de la cuota (US3-4); en fondos de empleados, el porcentaje a aportes ≥ `FRACCION_MINIMA_APORTES` y la cuota total ≤ `TOPE_CUOTA_FONDO_EMPLEADOS_PCT` del ingreso (`Lending.Contributions.InstallmentCapExceeded`, con el tope y su fuente) |
| `admiteExtraordinarios` | sí/no | no (sí) | |
| `excedenteDeCuota` | `Extraordinary`, `CreditBalance` | no (`Extraordinary`) | |
| `parametroDeLimiteIndividual` | `LIMITE_PARTICIPACION_APORTES_PN` o `_PJ` | no | FR-043 |
| `plazoDeDevolucionDias` | entero | sí | Estatutario (FR-079) |
| `admiteAmortizacion` · `admiteCesion` | sí/no | no (no) | FR-047, FR-048 |
| `notas` | texto 400 | no | |

Hoja `Equivalencias` como en §2 (`LND_ContributionLineLegacyEquivalences` **(nuevo en contracts)**).

---

## 4. Partes de la migración

### 4.1 `member-accounts` — llave `numero` (FR-113)

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numero` | número de cuenta | sí | Único en la carga y frente a las cuentas vivas; **es el número que conserva** la cuenta |
| `titular` | persona | sí | Viva; si la línea exige asociado, asociado vigente al corte (`Lending.Savings.MembersOnly`) |
| `oficina` | sucursal | sí | Vinculada a una sucursal contable (C9 de la 013) |
| `linea` o `codigoHeredado` | código · texto | uno de los dos | Resuelve la línea por la equivalencia (§2); con versión vigente al corte |
| `clase` | `DepositClass` | sí | Debe coincidir con la de la línea; `TermDeposit` va en la parte `term-deposits` |
| `fechaDeApertura` | fecha | sí | ≤ corte |
| `estado` | `Active`, `Inactive` | sí | `Inactive` con `fechaUltimoMovimientoDelTitular` |
| `fechaUltimoMovimientoDelTitular` | fecha | no | Para la inactividad (FR-058, FR-084) |
| `manejo` | `HandlingMode`: `Indistinct`, `Joint` | no (`Indistinct`) | |
| `saldoCapital` | monto ≥ 0 | sí | Saldo inicial del concepto `Principal` al corte |
| `interesCausadoNoLiquidado` | monto ≥ 0 | no (0) | Entra como `InterestPayable` y se liquida en la primera liquidación **sin volver a causarse** (Edge Cases) |
| `ultimaLiquidacionHasta` | fecha | sí si hay interés causado | Marca `LastSettledThrough`; la causación arranca el día siguiente al corte |
| `cargosPendientes` | monto ≥ 0 | no | `ChargeReceivable` |
| `cuotaContractual` · `cuotasPagadas` · `cuotasAtrasadas` · `fechaDeVencimientoContractual` · `tasaPactadaEA` | | con `Contractual` | Sellan `MemberAccountTermsVersion`; los atrasos se cargan en `account-installments` |
| `tasaNegociadaMargenEA` | porcentaje | no | Se sella como tasa negociada migrada (sin aprobador; nota «migrada») |
| `canjePendiente` | monto ≥ 0 | no | Valor en canje al corte; se libera con los días de su clase |
| `soporteDeOrigenDeFondos` | texto 200 | no | Referencia |

### 4.2 `member-account-parties` — llave `cuenta` + `persona` + `rol`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `cuenta` | número de cuenta | sí | De `member-accounts`, `term-deposits` o `contribution-accounts` de la carga (aviso al importar, error al aplicar si no está) |
| `persona` | persona | sí | |
| `rol` | `MemberAccountPartyRole`: `CoHolder`, `Signatory`, `LegalRepresentative`, `Beneficiary` | sí | El titular va en la cuenta, no aquí |
| `proporcion` | porcentaje | con `CoHolder` | La del titular es el resto; sin proporción, partes iguales (certificado tributario, Edge Cases) |
| `limiteDeFirma` | monto | no | `Signatory` |
| `porcentajeBeneficiario` | porcentaje | con `Beneficiary` | Por cuenta suman 100 (`Lending.Savings.BeneficiariesMustSum100`) |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | Un representante de menor con `vigenteHasta` = la víspera de la mayoría de edad si no se informa |
| `soporte` | texto 200 | con `LegalRepresentative` | Registro civil o decisión judicial (FR-033); el adjunto se sube después y la cuenta queda con la marca «soporte pendiente» hasta entonces |

### 4.3 `term-deposits` — llave `numero` (FR-113, FR-067)

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numero` · `titular` · `oficina` · `linea`/`codigoHeredado` | | sí | Como §4.1; la línea debe ser `TermDeposit` |
| `numeroDeTitulo` | texto 20 | sí | El título vigente; las prórrogas siguientes llevan sufijo de versión |
| `capital` | monto > 0 | sí | |
| `fechaDeConstitucion` | fecha | sí | Del título original |
| `fechaDeInicioVigente` | fecha | sí | De la versión vigente (tras prórrogas) |
| `plazoDias` | plazo | sí | Dentro del rango de la línea (aviso si lo excede: lo pactado no se reescribe) |
| `fechaDeVencimiento` | fecha | sí | > corte; = inicio + plazo (o el siguiente hábil, D19) |
| `tasaPactadaEA` | porcentaje | sí | La sellada; sin revisar contra la tabla |
| `tasaDeTablaEA` · `margenNegociadoEA` | porcentaje | no | Si se negoció |
| `periodicidadDeIntereses` · `modalidad` | `Monthly`… · `Arrears`/`Advance` | sí | |
| `periodosPagados` | entero | sí | Del plan de la versión vigente |
| `interesCausadoNoPagado` | monto ≥ 0 | sí | Del período en curso al corte; el motor `PlanDeCdat` regenera el plan con las condiciones y lo compara: una diferencia de más de un peso con su cálculo es `Lending.TermDeposit.ScheduleMismatch` con los dos valores |
| `destinoDeIntereses` | `Account` + `cuentaDestino`, o `DisbursementOrder` | sí | La cuenta destino, de la carga o viva |
| `instruccionAlVencimiento` | `Rollover`, `RenewWithConditions`, `CancelAndPay` | sí | |
| `prorrogasHechas` | entero | no (0) | Se conserva como dato (spec, «datos migrados de entregas posteriores») |
| `plazoDeEmision` | `IssueTermBand` | no | Calculado del plazo pactado; si se informa y difiere, error |
| `embargado` · `titularFallecido` | sí/no | no | Se cruzan con `holds-and-garnishments` |

### 4.4 `contribution-accounts` — llave `asociado`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `asociado` | persona | sí | Asociado vigente al corte; una sola cuenta de aportes por asociado |
| `numero` | número de cuenta | no | Si falta, se numera al activar con el consecutivo de la línea |
| `oficina` · `linea`/`codigoHeredado` | | sí | La línea de aportes |
| `fechaDeVinculacion` | fecha | sí | Antigüedad (tasa negociada, cargos) |
| `aportesMinimos` | monto ≥ 0 | sí | Concepto `ContributionMinimum` |
| `aportesOrdinarios` · `aportesExtraordinarios` · `aportesRevalorizados` | monto ≥ 0 | sí · no · no | Conceptos `ContributionOrdinary`, `ContributionExtraordinary`, `ContributionRevaluation` |
| `minimoNoReducible` | monto | no (el de la línea) | Si el estatuto fija uno individual |
| `ingresoRegistrado` | monto | con cuota `IncomePercent` | Base de la cuota |
| `cuotaPactada` | monto | no | Si difiere del cálculo de la línea, aviso |

Regla: la suma de aportes del archivo cuadra con las cifras de referencia y con Contabilidad en el cuadre (§4.9).

### 4.5 `account-installments` — llave `cuenta` + `periodo` (FR-040)

Cuotas atrasadas al corte de aportes, permanente y contractual. Columnas: `cuenta` (número), `periodo`,
`fechaDeVencimiento` (≤ corte), `valor`, `pagado`, `motivoNoDescontado` (`MotivoDeNoDescuento`, opcional). Nacen
`Pending` o `PartiallyPaid` y **siguen exigibles** después de activar (se ven en `ListarExigiblesAsync`).

### 4.6 `holds-and-garnishments` — llave `cuenta` + `tipo` + `referencia`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `cuenta` | número de cuenta | sí | |
| `tipo` | `Hold`, `Garnishment`, `Deceased` | sí | Sobre aportes sólo `Hold` con motivo `LoanCollateral` (los aportes no se embargan, FR-055) |
| `motivo` | `HoldReason` | con `Hold` | `LoanCollateral` exige `credito` (número de un crédito vivo de la Cartera) |
| `valor` · `total` | monto · sí/no | sí | |
| `referencia` | texto 40 | sí | Oficio, orden interna o certificado de defunción |
| `juzgado` · `demandante` | persona | con `Garnishment` | |
| `esPorAlimentos` | sí/no | con `Garnishment` | |
| `fechaDelOficio` · `desde` | fecha | sí | |
| `retenido` | monto | con `Garnishment` | Lo retenido al corte (se conserva, no se recalcula) |

### 4.7 `gmf-exemptions` — llave `persona`

Columnas: `persona`, `cuentaExenta` (número de cuenta de ahorro de la carga), `vigenteDesde`, `exentoUsadoDelMes`
(monto: el acumulado del mes del corte, `LND_GmfMonthlyUsage`). Una persona con dos cuentas exentas: error
`Lending.Gmf.AlreadyMarked` en la fila (con la política `SingleMarkedAccount`).

### 4.8 `tax-year-to-date` — llave `titular` + `cuenta` + `concepto`

Rendimientos, retenciones y GMF del año del corte **anteriores al corte**, para el certificado tributario anual
(FR-113, Edge Cases: «se cargan como información, sin hechos»). Columnas: `titular`, `cuenta`, `anio`, `concepto`
(`Interest`, `Withholding`, `Gmf`, `InflationaryComponent`), `valor`, `baseDeRetencion`. Escribe
`LND_WithholdingAccumulations` al activar.

### 4.9 Validaciones entre partes al aplicar

`ApplyDepositMigrationLoadCommand`, todo o nada (`Lending.Migration.CrossPartErrors` con `{ part, row, column,
message }`):

- toda referencia a una cuenta existe en la carga o viva; toda cuenta de captación tiene línea con versión al corte;
- beneficiarios al 100 % por cuenta; un menor titular tiene representante vigente;
- `term-deposits`: el plan regenerado coincide (`ScheduleMismatch`); la cuenta destino de intereses existe;
- `gmf-exemptions`: una cuenta exenta por persona con la política vigente;
- `holds-and-garnishments`: lo retenido no excede el saldo; ningún embargo sobre aportes;
- el **cuadre** (FR-115): por línea, concepto y conjunto de cuentas contra `SaldosDeCuentasMapeadasAsync` al corte, con
  las **cifras de referencia** registradas (FR-118) como segunda columna de cotejo; las diferencias se aceptan con acta
  (`api.md` §16).

Errores propios **(nuevo en contracts)**: `Lending.Migration.AccountNumberDuplicate`,
`Lending.Migration.LineEquivalenceMissing`, `Lending.Migration.MinorWithoutRepresentative`.

---

## 5. Convenio de pagaduría ampliado (`lending.payroll-agreements` v2, FR-125, E-05, R17)

La versión 2 de la plantilla conserva las tres hojas de la 013 §8 y suma cuatro. Escribe además
`LND_PayrollAgreementFileGroups` y `LND_PayrollAgreementConceptEquivalences` (research §0.2) y sus filas hijas
**(nuevas en contracts)** `LND_PayrollAgreementConceptMembers`, `LND_PayrollAgreementIdentityEquivalences`,
`LND_PayrollAgreementCauseEquivalences`. Todo con vigencia de la versión del convenio (`convenio` + `vigenteDesde`).

### Hoja `GruposDeArchivo`

Llave `convenio` + `vigenteDesde` + `codigo`. Un convenio tiene **un grupo** (un solo archivo) o **varios** (archivos
separados por grupo de conceptos, cada uno con su formato y su calendario).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `convenio` · `vigenteDesde` | código · fecha | sí | |
| `codigo` | código 10 | sí | `CREDITOS`, `APORTESAH`… |
| `nombre` | texto 120 | sí | |
| `formatoDeEnvio` · `formatoDeRespuesta` | código de formato bancario | sí | Ámbitos `PayrollDeductionRequest` (5, envío) y `PayrollDeductionResult` (6, respuesta) de K2 (§5.5) |
| `diaDeCorte` · `diaDeEnvio` · `diaDePago` · `antelacionDias` | entero | sí | El calendario del grupo |

### Hoja `ConceptosEquivalentes`

Llave `convenio` + `vigenteDesde` + `codigoPagaduria`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigoPagaduria` | texto 10 | sí | Como lo escribe la pagaduría en sus archivos |
| `descripcion` | texto 120 | sí | |
| `grupo` | código | sí | De `GruposDeArchivo` |
| `reparto` | `Priority`, `Proportional` | sí | Sin reparto no se genera el archivo (`Lending.PayrollAgreement.EquivalenceWithoutSplit`) |
| `orden` | entero | sí | Prelación **entre conceptos** cuando lo descontado no alcanza |

### Hoja `MiembrosDelConcepto`

Llave `convenio` + `vigenteDesde` + `codigoPagaduria` + `linea` + `concepto`. Qué líneas, **de cualquier naturaleza**, y
qué conceptos se agrupan bajo cada código de la pagaduría.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigoPagaduria` | texto 10 | sí | De `ConceptosEquivalentes` |
| `linea` | código | sí | Línea de crédito, de captación (`Permanent`, `Contractual`) o de aportes |
| `concepto` | `Installment` (cuota del crédito), `Contribution`, `PermanentSavings`, `ContractualSavings` | sí | |
| `prioridad` | entero | con `Priority` | Orden dentro del código |

Una línea con cuota por pagaduría que no está en ningún miembro: `Lending.PayrollAgreement.LineWithoutGroup` al
generar, con la línea (FR-125).

### Hoja `EquivalenciasDeIdentificacion`

Columnas: `convenio`, `vigenteDesde`, `persona`, `codigoEnLaPagaduria` (texto 20, el código del empleado),
`documentoEnLaPagaduria` (texto 20, si difiere). Sin fila, se usa el documento del maestro.

La hoja `CodigosDeRespuesta` de la 013 cumple la **equivalencia de causas** (código de la pagaduría →
`MotivoDeNoDescuento` o «descontado»); suma la columna `aplicaA` (`Loans`, `Accounts`, `Both`, por defecto `Both`).

### 5.5 Formatos de envío y respuesta como dato

Formatos de archivo plano de la plataforma (`COR_BankFileFormats` / `COR_BankFileFormatFields`, motor
`FlatFileWriter`; lectura con `FlatFileReader`, R17), sin programar: campos, longitudes, separadores, codificación,
registros de cabecera, detalle y totales. Se usan los ámbitos de K2 (`PayrollDeductionRequest = 5` para el envío y
`PayrollDeductionResult = 6` para la respuesta), sin ámbito nuevo (research C01); admiten líneas de créditos y de
cuentas con los orígenes de campo nuevos.

Orígenes de campo que suma este contrato **(nuevo en contracts)**, además de los genéricos (`Payee*`, `Amount`,
`Concept`):

| Origen | Envío | Respuesta | Qué es |
|---|---|---|---|
| `PaymasterConceptCode` | sí | sí | El código equivalente de la pagaduría |
| `PaymasterEmployeeId` | sí | sí | El código del empleado (o el documento) por la equivalencia de identificación |
| `PayerDocumentType` · `PayerDocumentNumber` | sí | sí | El documento del deudor o titular |
| `PeriodYear` · `PeriodMonth` · `SubPeriod` | sí | sí | |
| `NoveltyKind` | sí | no | `New`, `Change`, `Cancellation`, `Unchanged` |
| `InstallmentAmount` | sí | no | Lo enviado por código y persona |
| `DeductedAmount` | no | sí | Lo descontado |
| `ResultCode` | no | sí | Código de respuesta (equivalencia de causas) |
| `DeductionDate` | no | sí | |
| `LineCount` · `TotalAmount` | totales | totales | |

**Vista previa** con datos reales antes de generar (`POST /api/lending/payroll-deduction-files/preview` con
`fileGroupCode`): el archivo tal como saldría y los impedimentos. **Una pagaduría que cambia de formato**: versión nueva
del formato con vigencia; los archivos ya generados conservan el suyo (Edge Cases). Un formato que ya generó archivos
no cambia de estructura (regla de la 010 N4).

---

## 6. Matriz contable (versión 2 de la plantilla, `contabilidad.md` §4)

Columnas de la 013 §9 más: `naturaleza` (`LineNature` o `*`), `claseDeCaptacion` (`DepositClass` o `*`),
`plazoDeEmision` (`IssueTermBand` o `*`), `claseDeAporte` (`ContributionClass` o `*`), `marcaDeCuenta` (`Standard`,
`GmfExempt`, `Garnished` o `*`) y `cuentaSugerida` (texto, **informativo**: el lector no lo aplica; lo genera
`GetLendingRulesTemplateQuery` con las sugerencias CUIF «pendiente de la contadora»). Un archivo de la versión 1 se
sigue leyendo: las columnas nuevas faltan y valen `*`. La hoja «Instrucciones» se genera de `OperacionesDeCartera` y
`RolesDeCuentaDeCartera`.

| operacion | rol | claseDeCaptacion | plazoDeEmision | claseDeAporte | marcaDeCuenta | cuenta | cuentaSugerida | vigenteDesde |
|---|---|---|---|---|---|---|---|---|
| * | DepositPrincipal | TermDeposit | SixToTwelve | | Standard | | 211010 | AAAA-MM-01 |
| InteresDeCaptacionCausado | InterestExpense | Demand | | | | | 615005 | AAAA-MM-01 |
| * | ContributionCapital | | | Ordinary | | | 310505 | AAAA-MM-01 |

## 7. Valores nacionales nuevos (`saas.national-values`, R26, FR-015)

Filas nuevas del registro central (plantilla de la 013 §10, maestro con segundo factor), replicadas a cada cooperativa:

| `codigo` | Unidad | Fuente de ejemplo | Uso |
|---|---|---|---|
| `COMPONENTE_INFLACIONARIO` | porcentaje | Decreto 898 de 2026 (55,43 % del año gravable 2025) | Retención (FR-021); «último publicado» = la vigencia más reciente con `vigenteDesde` ≤ D |
| `LIMITE_INEMBARGABLE` | monto | Circular de la Superfinanciera del año | Embargos (FR-055) |
| `TOPE_ENTREGA_SIN_SUCESION` | monto | EOSF art. 127 y su actualización | Fallecimiento (FR-082) |

Los parámetros de la Cartera de R26 (`RETENCION_TARIFA_*`, `UMBRAL_RETENCION_UVT_DIA`, `GMF_*`, `FONDO_LIQUIDEZ_*`…) se
siembran con fuente y «pendiente de validar» y se editan en `/cartera/parametros` (no tienen plantilla).

---

## 8. Layouts regulatorios como dato versionado (FR-111, FR-112, R24)

### 8.1 El motor y los archivos

El motor genérico de la 013 §7 (`Domain/Common/Layouts`: `LayoutDeArchivo`, `ValidadorDeLayout`, `EscritorPorLayout`)
sin cambios. Archivos JSON embebidos en `Application/Lending/Regulatory/Layouts/` con el esquema de la 013 §7.2, que
`CatalogoDeLayoutsDeCartera` carga y valida **al arrancar** (un JSON mal formado impide arrancar). Nombres
**(nuevo en contracts)**:

| `formatCode` (`api.md` §18.2) | Archivo | `kind` | Periodicidad | Proyección |
|---|---|---|---|---|
| `sicses-captaciones` | `sicses-captaciones-{v}-{aaaa-mm-dd}.json` | `DepositsSicses` | mensual | `ProyeccionSicsesCaptaciones`: una fila por cuenta viva al cierre del mes (titular a la fecha de corte, línea, clase, saldo por concepto, tasa, fechas, plazo de emisión, marcas), del cierre mensual de la Cartera |
| `sicses-saldos-diarios` | `sicses-saldos-diarios-{v}-{aaaa-mm-dd}.json` | `DepositsSicses` | mensual con detalle diario | `ProyeccionSaldosDiarios`: por día del mes, saldos agregados por clase desde `LND_MemberAccountDailyPositions` (D6) |
| `sicses-fondo-liquidez` | `sicses-fondo-liquidez-{v}-{aaaa-mm-dd}.json` | `DepositsSicses` | mensual | `ProyeccionFondoDeLiquidez`: la instantánea `LND_LiquidityFundSnapshots` del último día del mes y el constituido |
| `exogena-1019` | `exogena-1019-{v}-{aaaa-mm-dd}.json` | `Exogenous` | anual | `ProyeccionExogena1019`: por titular y cuenta de ahorro, saldo al 31 de diciembre, rendimientos, retención (por la proporción de cada cotitular) |
| `exogena-1020` | `exogena-1020-{v}-{aaaa-mm-dd}.json` | `Exogenous` | anual | `ProyeccionExogena1020`: por titular y CDAT (y lo que la contadora defina para aportes y permanente, P9), saldo al 31 de diciembre, rendimientos, retención |

### 8.2 Cotejo

**Todos los campos nacen con `verified = false`** hasta que la contadora los coteje con el instructivo vigente
(P8, P9) y un archivo aceptado por el validador. Un layout con campos sin cotejar **es vigente** y generar deja la
advertencia `Lending.Regulatory.LayoutSinCotejar`, guardada con la generación y mostrada como **«formato sin
cotejar»** (US17-1, precedente D-43 de la PILA). Cotejar es publicar una versión nueva del JSON con `verified: true`,
`verifiedBy`, `verifiedOn` y `verifiedAgainst`.

Forma (campos **ilustrativos**; la estructura real la aporta el dueño con el instructivo):

```jsonc
{
  "code": "SICSES-CAPTACIONES-v1",
  "kind": "DepositsSicses",
  "version": "1",
  "source": "Formato de captaciones del SICSES (por cotejar con el instructivo vigente)",
  "validFrom": "2027-01-01", "validTo": null,
  "fileKind": "Delimited", "delimiter": "|", "encoding": "utf-8", "lineEnding": "CRLF",
  "records": [
    { "code": "Detail", "repeat": "PorCuenta", "fields": [
      { "number": 1, "name": "Tipo de identificación", "order": 1, "kind": "A", "length": 2, "format": "Text", "required": true,
        "path": "Titular.TipoDeDocumento", "allowedValues": ["CC", "CE", "NI", "PA", "TI", "RC"], "verified": false },
      { "number": 2, "name": "Número de identificación", "order": 2, "kind": "A", "length": 15, "format": "Text", "required": true,
        "path": "Titular.NumeroDeDocumento", "verified": false },
      { "number": 3, "name": "Código de la línea", "order": 3, "kind": "A", "length": 10, "format": "Text", "required": true,
        "path": "Cuenta.Linea", "verified": false },
      { "number": 4, "name": "Número de la cuenta", "order": 4, "kind": "A", "length": 20, "format": "Text", "required": true,
        "path": "Cuenta.Numero", "verified": false },
      { "number": 5, "name": "Saldo de capital", "order": 5, "kind": "N", "length": 18, "format": "Amount2", "required": true,
        "path": "Saldos.Capital", "verified": false },
      { "number": 6, "name": "Intereses por pagar", "order": 6, "kind": "N", "length": 18, "format": "Amount2", "required": true,
        "path": "Saldos.InteresPorPagar", "verified": false },
      { "number": 7, "name": "Tasa efectiva anual", "order": 7, "kind": "N", "length": 9, "format": "Rate", "required": true,
        "path": "Condiciones.TasaEA", "verified": false },
      { "number": 8, "name": "Fecha de vencimiento", "order": 8, "kind": "F", "length": 8, "format": "Date", "pattern": "yyyyMMdd",
        "required": false, "path": "Condiciones.Vencimiento", "verified": false } ] }
  ],
  "notes": ["Campos reconstruidos; cada uno lleva verified=false hasta cotejarlo con el instructivo y un archivo aceptado."]
}
```

### 8.3 Validar, generar y marcar la carga

Mecánica de la 013 §7.5 sobre `LND_RegulatoryFiles` con `Kind` `DepositsSicses` o `Exogenous` **(nuevos en
contracts)** y `PeriodMonth` nulo en la exógena: validar no guarda; generar con bloqueantes deja una generación
`Validated` sin archivo; regenerar crea la versión siguiente y deja la anterior `Superseded`; marcar cargado anota el
radicado y el período ya no se regenera. La exógena exige el año cerrado (`Lending.Regulatory.PeriodNotClosed`); el
SICSES mensual exige el cierre mensual de la Cartera del mes. Pruebas: archivos dorados
`Application.Tests/Lending/Regulatory/Casos/*.esperado.txt` comparados byte a byte, regenerables sólo con
`CARTERA_ESCRIBIR_ESPERADO=1`.

---

## 9. Nombres fijados en este documento (nuevo en contracts)

- **Tipos de valor**: `NumeroDeCuenta`, `Periodo`, `Plazo`.
- **Tablas de preparación** (salvo `LND_MigrationAccountRows`, que nombra research): `LND_MigrationAccountPartyRows`,
  `LND_MigrationTermDepositRows`, `LND_MigrationContributionAccountRows`, `LND_MigrationAccountInstallmentRows`,
  `LND_MigrationAccountHoldRows`, `LND_MigrationGmfExemptionRows`, `LND_MigrationTaxYearToDateRows`.
- **Equivalencias heredadas**: `LND_DepositLineLegacyEquivalences`, `LND_ContributionLineLegacyEquivalences`.
- **Convenio**: hojas `GruposDeArchivo`, `ConceptosEquivalentes`, `MiembrosDelConcepto`, `EquivalenciasDeIdentificacion`;
  tablas `LND_PayrollAgreementConceptMembers`, `LND_PayrollAgreementIdentityEquivalences`,
  `LND_PayrollAgreementCauseEquivalences`; columna `aplicaA`; `BankFileFormat.Direction` (`Outbound`, `Inbound`);
  orígenes de campo `PaymasterConceptCode`, `PaymasterEmployeeId`, `PayerDocumentType`, `PayerDocumentNumber`,
  `PeriodYear`, `PeriodMonth`, `SubPeriod`, `NoveltyKind`, `InstallmentAmount`, `DeductedAmount`, `ResultCode`,
  `DeductionDate`; conceptos de miembro `Installment`, `Contribution`, `PermanentSavings`, `ContractualSavings`.
- **Matriz**: columnas `naturaleza`, `claseDeCaptacion`, `plazoDeEmision`, `claseDeAporte`, `marcaDeCuenta`,
  `cuentaSugerida`.
- **Layouts**: `formatCode` y archivos de §8.1; `RegulatoryFileKind.DepositsSicses`, `.Exogenous`; proyecciones
  `ProyeccionSicsesCaptaciones`, `ProyeccionSaldosDiarios`, `ProyeccionFondoDeLiquidez`, `ProyeccionExogena1019`,
  `ProyeccionExogena1020`.
- **Errores**: `Lending.DepositLine.TiersOverlap`, `Lending.Migration.AccountNumberDuplicate`,
  `Lending.Migration.LineEquivalenceMissing`, `Lending.Migration.MinorWithoutRepresentative`.
- **Valores admitidos**: `periodicidadDeLiquidacion` (`Monthly`, `Quarterly`, `SemiAnnual`, `Annual`, `AtMaturity`),
  `evento` de cargo (`Withdrawal`, `Deposit`, `Statement`, `Certificate`, `MediumReplacement`, `Periodic`), formas de
  exoneración (`AverageBalanceAtLeast`, `MinimumBalanceAtLeast`, `SeniorityMonthsAtLeast`, `EmployeeHolder`,
  `GmfExemptAccount`, `MinorHolder`, `Office`), conceptos de `tax-year-to-date` (`Interest`, `Withholding`, `Gmf`,
  `InflationaryComponent`).

## 10. Conciliación

| Punto | Decisión |
|---|---|
| research R27 lista diez plantillas; las líneas también llevan equivalencia con lo heredado (FR-113) | La equivalencia va como hoja de la plantilla de líneas, no como plantilla aparte |
| Plan pendiente del CDAT | No se carga cuota a cuota: se regenera con `PlanDeCdat` desde las condiciones selladas y se coteja con el interés causado informado |
| `PaymasterDeduction` frente a los ámbitos 5 y 6 de la 013 | **Rige research C01**: se usan los ámbitos 5 (envío) y 6 (respuesta) de K2, sin ámbito nuevo |
| Exógena de aportes y permanente | Depende de P9 (contadora); el layout nace con `verified = false` y la proyección los incluye sólo si la contadora lo confirma |
