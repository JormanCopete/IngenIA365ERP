# Data Model — Novedades y Liquidación Periódica de Nómina (Feature 005)

**Phase 1 output** · Branch `005-nomina-novedades-liquidacion` · 2026-09-05

Todas las tablas nuevas viven en la **base de cada cooperativa** (Principio IV),
prefijo `PAY_`, heredan `AuditableEntity` (Principio VII: `HasQueryFilter(!IsDeleted)`),
exponen `PublicId` (Principio VI) y se crean con **una migración en par**
(`add-migration.ps1 -Name NominaNovedadesYLiquidacion -Context Application`). Los
datos de semilla van por `IDataSeeder`, nunca en la migración (Principio XII).

Identificadores en inglés (constitución); nombres de pantalla en español.

---

## 1. Mapa

```
PayrollPlan 1 ──< PayPeriod 1 ──< PayrollNovelty >── 1 PayrollConceptDefinition (versión)
     │                 │                 │                        │
     └──< Employee     └──< PayrollRun ──< PayrollRunEmployee ──< PayrollRunLine ──> LegalParameter (versión)
                              │                  │                      └──────────> PayrollNovelty (origen)
                              ├──> AccountingDocument (NM)              ├──< PayrollPayment
                              └──> AccountingDocument (reversión)       └──< PayslipDelivery

PayrollRecurringNovelty >── Employee, PayrollConceptDefinition
SalaryChange (existente)  >── Employee          EmployeeWithholdingRate >── Employee
EmployeeTaxDeduction      >── Employee          PayrollConceptDefinitionAccount >── ChartOfAccount, CostCenter
```

Legado que **no se toca** y queda de sólo lectura: `PAY_PayrollConcepts`,
`PAY_ConceptAccounts`, `PAY_PayrollEntries`, `PAY_PayrollTransactions`,
`PAY_PayrollPlanLiquidations`, `PAY_WithholdingParameters`,
`PAY_AutoContributionParams`.

---

## 2. Entidades nuevas

### 2.1 `PayrollPlan` → `PAY_PayrollPlans`

| Campo | Tipo | Regla |
|---|---|---|
| `Code` | string(20) | único por cooperativa; inmutable |
| `Name` | string(100) | |
| `Periodicity` | enum `PayrollPeriodicity { Monthly = 30, Biweekly = 15 }` | los días del período salen de aquí (FR-008) |
| `IsDefault` | bool | exactamente uno por cooperativa; lo crea la semilla |
| `IsActive` | bool | un plan con empleados no se desactiva |

Índices: `UK (Code)`, `UX (IsDefault) WHERE IsDefault = 1` (filtro portable vía
`ProviderModelConventions`).

### 2.2 `PayPeriod` (existente, **extendido**) → `PAY_PayPeriods`

| Campo nuevo / cambiado | Tipo | Regla |
|---|---|---|
| `PayrollPlanId` | int FK → `PayrollPlan` | obligatorio; la migración lo rellena con el plan por defecto para filas existentes |
| `Status` | enum `PayPeriodStatus { Open = 0, Calculated = 1, Approved = 2, Reversed = 3 }` | sustituye el `int` libre; `StatusMessage` se conserva como texto informativo |
| `ApprovedAt`, `ApprovedBy`, `RunPublicId` | | referencia a la corrida vigente |

Reglas: dos períodos del mismo plan no se superponen (validador consulta
`StartDate`/`EndDate`); `PlanId` legado (número de planilla) no se reinterpreta.

Transiciones: `Open → Calculated` (calcular) · `Calculated → Open` (cualquier cambio
que deje la corrida `Stale` no cambia el período; sólo recalcular) ·
`Calculated → Approved` (aprobar) · `Approved → Reversed → Open` (reversar).

### 2.3 `Employee` (existente, **extendido**) → `PAY_Employees`

| Campo nuevo | Tipo | Regla |
|---|---|---|
| `PayrollPlanId` | int FK | obligatorio; migración lo rellena con el plan por defecto |
| `PayrollPlanEffectiveFrom` | date | fecha de efecto del último cambio de plan |
| `WithholdingProcedure` | byte (1 ó 2) | por defecto 1 |
| `EmployeeClass` | enum `EmployeeClass { Standard, IntegralSalary, Apprentice, Intern, Pensioner }` | decide qué conceptos aplican (máscara en la definición) |

Correo, nombre y documento siguen en `COR_People` (Principio V).

### 2.4 `EmployeeWithholdingRate` → `PAY_EmployeeWithholdingRates`

`EmployeeId`, `RatePercent` (decimal 6,3), `ValidFrom`, `ValidTo?`. Sin solapes por
empleado. Sólo aplica si `WithholdingProcedure = 2`.

### 2.5 `EmployeeTaxDeduction` → `PAY_EmployeeTaxDeductions`

`EmployeeId`, `Kind` (enum `TaxDeductionKind { HousingInterest, PrepaidHealth,
Dependents, VoluntaryPension, AfcSavings }`), `MonthlyAmount?`, `Percent?`,
`ValidFrom`, `ValidTo?`. Los topes de cada clase vienen de parámetros legales.

### 2.6 `PayrollConceptDefinition` → `PAY_ConceptDefinitions` (una fila por versión)

| Campo | Tipo | Regla |
|---|---|---|
| `Code` | string(30) | estable entre versiones; único junto con `ValidFrom` |
| `Name` | string(120) | |
| `Nature` | enum `ConceptNature { Earning, Deduction, EmployerContribution, Provision, Informative }` | |
| `CalculationKind` | enum `CalculationKind { FixedAmount, PercentOfBase, QuantityTimesUnit, RangeTable, CompositeOfConcepts }` | FR-009 |
| `FixedAmount?` | decimal(18,2) | `FixedAmount` |
| `BaseKind?` | enum `CalculationBase { BasicSalary, SalaryEarnings, ContributionBase, BenefitsBase, WithholdingBase, TransportAllowanceBase }` | `PercentOfBase`, `RangeTable` |
| `Percent?` | decimal(9,4) | `PercentOfBase`; puede venir de un parámetro legal (`PercentParameterCode`) |
| `UnitKind?` | enum `UnitKind { OrdinaryHour, Day, HourWithSurcharge }` + `UnitFactor?` decimal(9,4) | `QuantityTimesUnit` (la hora ordinaria = salario/240, día = salario/30; el recargo es el factor) |
| `TableParameterCode?` | string(40) | `RangeTable`: código del parámetro legal con rangos |
| `ComponentConceptCodes?` | string(400) | `CompositeOfConcepts`: códigos separados por `;` con signo y peso (`+HEX_DIURNA*1;+COMISION*1`) |
| `AffectsSalaryBase`, `AffectsContributionBase`, `AffectsBenefitsBase`, `AffectsWithholdingBase` | bool | qué bases alimenta |
| `IsBenefitRelated` | bool | prestacional |
| `AllowsRepeatInPeriod` | bool | FR-002 |
| `MaxQuantity?`, `MaxAmount?` | decimal | topes |
| `ApplicableClasses` | int (máscara de `EmployeeClass`) | 0 = todas |
| `RequiresDates`, `RequiresQuantity`, `RequiresAmount` | bool | qué pide la novedad |
| `IsAutomatic` | bool | lo genera el motor sin novedad (salario, auxilio, aportes, provisiones) |
| `Origin` | enum `ConceptOrigin { Seed, Custom, TranslatedLegacy }` | `Seed` no se borra ni cambia de `Code` |
| `LegacyConceptId?` | int | referencia a `PAY_PayrollConcepts` si `TranslatedLegacy` |
| `ValidFrom`, `ValidTo?` | date | versión; `ValidTo` nulo = vigente |
| `IsActive` | bool | desactivar cierra la vigencia |

Índices: `UK (Code, ValidFrom)`, `IX (Code, ValidTo)`. Validaciones (FR-028): campos
obligatorios según `CalculationKind`; `ComponentConceptCodes` existen y no forman
ciclo (búsqueda en profundidad sobre las versiones vigentes); `TableParameterCode`
existe y es `RangeTable`.

### 2.7 `PayrollConceptDefinitionAccount` → `PAY_ConceptDefinitionAccounts`

`ConceptCode` (string, aplica a todas las versiones), `CostCenterId?` (nulo = por
defecto), `DebitAccountId` FK `ChartOfAccounts`, `CreditAccountId` FK. Único
`(ConceptCode, CostCenterId)`. La aprobación exige una fila por cada concepto
liquidado (con centro de costo o por defecto) — FR-019.

### 2.8 `PayrollLegalParameter` → `PAY_LegalParameters` (una fila por vigencia)

`Code` (string 40; catálogo en `LegalParameterCodes`), `Name`, `Kind` (enum
`LegalParameterKind { Amount, Percent, RangeTable }`), `Value?` decimal(18,4),
`ValidFrom`, `ValidTo?`, `Source` (texto: decreto o resolución). Único `(Code,
ValidFrom)`. Hijas `PayrollLegalParameterRange` → `PAY_LegalParameterRanges`:
`FromValue`, `ToValue?`, `Rate?`, `FixedValue?`, `Order`.

Códigos requeridos por el motor (semilla 2026): `SMMLV`, `AUX_TRANSPORTE`,
`AUX_TRANSPORTE_TOPE_SMMLV`, `UVT`, `SALUD_EMPLEADO_PCT`, `PENSION_EMPLEADO_PCT`,
`SALUD_EMPLEADOR_PCT`, `PENSION_EMPLEADOR_PCT`, `FSP_TABLA`, `ARL_CLASE_I..V_PCT`,
`SENA_PCT`, `ICBF_PCT`, `CAJA_PCT`, `PROV_CESANTIAS_PCT`, `PROV_INT_CESANTIAS_PCT`,
`PROV_PRIMA_PCT`, `PROV_VACACIONES_PCT`, `RETEFTE_TABLA_UVT`,
`RETEFTE_RENTA_EXENTA_PCT`, `RETEFTE_RENTA_EXENTA_TOPE_UVT`,
`RETEFTE_DEDUCCIONES_TOPE_PCT`, `MAX_DEDUCCION_SALARIO_PCT`,
`SALARIO_INTEGRAL_BASE_PCT`, `INCAPACIDAD_EMPLEADOR_DIAS`,
`INCAPACIDAD_EMPLEADOR_PCT`, `HORAS_MES`.

### 2.9 `PayrollNovelty` → `PAY_Novelties`

| Campo | Tipo | Regla |
|---|---|---|
| `PayPeriodId`, `EmployeeId`, `ConceptDefinitionId` (versión) | FK | período `Open` al crear (FR-001) |
| `Quantity?` decimal(10,2), `Amount?` decimal(18,2) | | según lo que exige el concepto |
| `StartDate?`, `EndDate?` | date | si `RequiresDates` |
| `DaysInPeriod`, `CarryOverDays` | int | calculados al guardar (FR-003) |
| `Notes` | string(500) | |
| `Status` | enum `NoveltyStatus { Active, Superseded, Cancelled }` | |
| `StatusReason?` | string(300) | obligatorio al corregir o anular |
| `Origin` | enum `NoveltyOrigin { Manual, Import, Recurring, Retroactive, LoanDeduction, CarryOver }` | |
| `SupersedesNoveltyId?`, `RecurringNoveltyId?`, `ImportBatchId?` (Guid), `RetroactiveOfPeriodId?`, `LoanPortfolioId?`, `CarriedFromNoveltyId?` | | trazabilidad de origen |
| `InstallmentNumber?`, `InstallmentTotal?` | int | recurrentes |

Índices: `IX (PayPeriodId, EmployeeId, Status)`, `IX (PayPeriodId, ConceptDefinitionId)`.
Regla de repetición: no dos `Active` del mismo `(PayPeriodId, EmployeeId, Code)`
si `AllowsRepeatInPeriod = false`.

### 2.10 `PayrollRecurringNovelty` → `PAY_RecurringNovelties`

`EmployeeId`, `ConceptCode`, `Quantity?`, `Amount?`, `StartDate`, `EndDate?`,
`TotalInstallments?`, `InstallmentsIssued`, `IsActive`, `Notes`. Al calcular un
período se materializa como `PayrollNovelty(Origin = Recurring)` si no existe ya para
ese período; al aprobar se incrementa `InstallmentsIssued`.

### 2.11 `PayrollRun` → `PAY_PayrollRuns` (namespace `Entities/Payroll/Transactions`)

| Campo | Tipo | Regla |
|---|---|---|
| `PayPeriodId`, `Version` | | `UK (PayPeriodId, Version)` |
| `Status` | enum `PayrollRunStatus { Draft, Stale, Superseded, Approved, Reversed }` | |
| `CalculatedAt`, `CalculatedBy`, `ApprovedAt?`, `ApprovedBy?`, `ReversedAt?`, `ReversedBy?`, `ReversalReason?` | | |
| `InputsHash` | string(64) | SHA-256 de los insumos normalizados (FR-014) |
| `EmployeeCount`, `TotalEarnings`, `TotalDeductions`, `TotalEmployerContributions`, `TotalProvisions`, `TotalNet`, `RoundingAdjustment` | decimal(18,2) | |
| `AccountingDocumentId?`, `ReversalAccountingDocumentId?` | FK `AccountingDocuments` | |
| `ApprovedWithoutSegregation` | bool | FR-021 |
| `ExceptionsJson?` | string | excepciones autorizadas (empleado, causa, autorizador, motivo) |

### 2.12 `PayrollRunEmployee` → `PAY_PayrollRunEmployees` (Transactions)

`PayrollRunId`, `EmployeeId`, `PayrollPlanId`, `DaysWorked`, `SalaryTranchesJson`
(tramos: desde, hasta, días, salario), `EmployeeClass`, `TotalEarnings`,
`TotalDeductions`, `TotalEmployerContributions`, `TotalProvisions`, `NetPay`,
`Flags` (máscara `RunEmployeeFlag { NegativeNet, DeductionsOverMax,
MissingAffiliation, ConceptWithoutAccounts, WithholdingRateMissing }`),
`ChangedFromPreviousRun` bool. `UK (PayrollRunId, EmployeeId)`.

### 2.13 `PayrollRunLine` → `PAY_PayrollRunLines` (Transactions)

| Campo | Tipo |
|---|---|
| `PayrollRunEmployeeId`, `ConceptDefinitionId` (versión), `ConceptCode`, `Nature` | |
| `Quantity?`, `BaseAmount?`, `Factor?`, `RangeFrom?`, `RangeTo?`, `Amount` decimal(18,2) | |
| `LegalParameterId?` (versión), `NoveltyId?` | origen |
| `ExplanationJson` | estructura `{ form, base:{kind,value}, factor, parameter:{code,validFrom,value}, range, novelty:{publicId,description}, steps:[{label,value}] }` |
| `AffectsAccounting` bool | falso para `LoanDeduction` (D-08) e informativos |
| `Order` | int |

Nunca se actualizan ni se borran (Principio XI; test `PrincipioXI_ContableImmutable`).

### 2.14 `PayrollPayment` → `PAY_PayrollPayments`

`PayrollRunEmployeeId`, `PaidAt`, `PaymentMethod` (enum `PaymentMethod { Transfer,
Check, Cash }`), `Reference` (string 60), `PaidBy`, `IsReverted`, `RevertedAt?`,
`RevertedBy?`, `RevertReason?`. Un pago vigente (`IsReverted = false`) por
empleado y corrida. Cualquier pago vigente bloquea la reversión (FR-040).

### 2.15 `PayslipDelivery` → `PAY_PayslipDeliveries`

`PayrollRunEmployeeId`, `RecipientEmail`, `RequestedAt`, `RequestedBy`, `SentAt?`,
`Status` (enum `DeliveryStatus { Sent, Failed }`), `ErrorMessage?`, `AttemptNumber`.

### 2.16 `SystemSetting` (existente): parámetros de la cooperativa (D-09)

`Payroll.Rounding` (`Peso` | `Centavo`), `Payroll.VariationThresholdPercent`
(decimal), `Payroll.AllowSameUserApproval` (bool). `ModulePrefix = "PAY"`.

---

## 3. Modelo del motor (Domain, no persistido)

```
CalculationInput
  Period { StartDate, EndDate, DaysInPeriod (30|15), PlanPeriodicity }
  Employee { PublicId, Class, SalaryTranches[{From,To,Salary}], JoinDate, TerminationDate?,
             Affiliations{Health,Pension,WorkRisk(Class),Severance}, WithholdingProcedure,
             WithholdingRate?, TaxDeductions[], TransportAllowanceEligible? (lo decide el motor) }
  Novelties[{ Code, Quantity?, Amount?, StartDate?, EndDate?, DaysInPeriod, Origin, NoveltyPublicId }]
  Concepts[versiones vigentes a Period.EndDate]
  Parameters[versiones vigentes a Period.EndDate]  (falta alguno requerido → CalculationRefused)
  Policies { Rounding, MaxDeductionPercent (parámetro), … }

CalculationResult
  Lines[{ Code, Nature, Quantity, Base, Factor, Range, Amount, ParameterCode, NoveltyPublicId, Explanation, AffectsAccounting }]
  Totals { Earnings, Deductions, EmployerContributions, Provisions, Net, RoundingAdjustment }
  Flags { NegativeNet, DeductionsOverMax, MissingAffiliation, WithholdingRateMissing }
  InputsHash
```

Orden de evaluación: automáticos base (salario por tramos → auxilio de transporte
→ novedades de devengo) → bases (salarial, aportes con tope de 25 SMMLV desde
parámetro, prestacional, retención) → deducciones de ley (salud, pensión, FSP por
tabla, retención P1/P2) → deducciones autorizadas (novedades, cartera) con tope de
`MAX_DEDUCCION_SALARIO_PCT` y saldo diferido → aportes del empleador y provisiones
(no afectan el neto) → redondeo y ajuste.

---

## 4. Estados y transiciones

```
PayPeriod:   Open ──calcular──> Calculated ──aprobar──> Approved ──reversar──> Reversed ──(auto)──> Open
PayrollRun:  Draft ──(cambio de insumo)──> Stale
             Draft|Stale ──(nuevo cálculo)──> Superseded      (la nueva corrida nace Draft)
             Draft ──aprobar──> Approved ──reversar──> Reversed
PayrollNovelty: Active ──corregir──> Superseded (+ nueva Active)   Active ──anular──> Cancelled
PayrollPayment: vigente ──retirar marca (permiso+motivo)──> IsReverted
```

Invariantes: a lo sumo una corrida `Draft|Stale` por período; exactamente una
`Approved` por período `Approved`; ninguna novedad cambia de estado en un período
`Approved`; ninguna línea de una corrida `Approved` cambia.

---

## 5. Migraciones

| Nombre (par PostgreSQL/SqlServer) | Contenido |
|---|---|
| `NominaPlanesYPeriodos` | `PAY_PayrollPlans`; columnas nuevas en `PAY_PayPeriods` y `PAY_Employees`; relleno del plan por defecto para filas existentes (DML idempotente separado en migración propia si la política del repo lo exige); enum `Status` |
| `NominaConceptosYParametros` | `PAY_ConceptDefinitions`, `PAY_ConceptDefinitionAccounts`, `PAY_LegalParameters`, `PAY_LegalParameterRanges`, `PAY_EmployeeWithholdingRates`, `PAY_EmployeeTaxDeductions` |
| `NominaNovedades` | `PAY_Novelties`, `PAY_RecurringNovelties` |
| `NominaLiquidacion` | `PAY_PayrollRuns`, `PAY_PayrollRunEmployees`, `PAY_PayrollRunLines`, `PAY_PayrollPayments`, `PAY_PayslipDeliveries` |

Todas reversibles (`Down` elimina lo creado; las columnas añadidas se quitan). No hay
migración destructiva: las tablas legadas quedan intactas.
