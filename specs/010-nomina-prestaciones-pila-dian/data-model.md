# Data Model: Nómina completa — prestaciones, retiro, procedimiento 2, PILA, nómina electrónica y dispersión

**Feature**: 010 | **Date**: 2026-09-20 | **Rama**: `010-nomina-prestaciones-pila-dian`

Fase 1 del plan. Lo que cambia en el esquema `PAY_` (y dos columnas en `COR_People`) para las
ocho historias de la spec, sobre las decisiones R1–R14 de [research.md](research.md) y con las
cinco precisiones de quien integra (plan › Decisiones): **el servicio de nómina electrónica no
tiene base de datos** —todo lo de cada cooperativa vive en la base de esa cooperativa—, las
liquidaciones especiales van en las tablas de corrida con `Kind`, ningún valor legal en el
código, PILA formato 2388 para Aportes en Línea y dispersión con formato en datos.

Convenciones, las mismas de la 009: toda entidad hereda de `AuditableEntity` (`int Id`,
`PublicId`, soft-delete, `RowVersion`, `CreatedAt/By`, `UpdatedAt/By`) salvo donde se dice
`AuditableEntityLong`; toda configuración declara `HasQueryFilter(e => !e.IsDeleted)`; decimales
`18,2` salvo que se indique (porcentajes `9,4`, UVT `18,4`, días `8,2`); fechas de negocio
`date`, marcas de tiempo `datetime` UTC; los índices con filtro se escriben en T-SQL y los traduce
`ProviderModelConventions`; FK con `Restrict` siempre (nada se borra en cascada en nómina).
Nombres de entidad en inglés; los nombres que ya existen **no se renombran** (`PayrollRun`,
`Employee`, `EmployeeWithholdingRate`, `PayrollLegalParameter`…). Las **transacciones**
(`PAY_PayrollRuns*`, generaciones PILA, documentos DIAN, archivos de dispersión) son inmutables
(Principio XI): se versionan o se reversan, nunca se editan.

---

## 1. Cambios a entidades existentes

### 1.1 `PAY_PayrollRuns` — `PayrollRun` (R2)

Hoy una corrida es siempre de un período. Pasa a tener **tipo**, y el período se vuelve opcional.

| Columna | Tipo | Regla |
|---|---|---|
| `Kind` | tinyint **NOT NULL DEFAULT 0** (`PayrollRunKind`: `Ordinary=0`, `ServiceBonus=1`, `Severance=2`, `Vacation=3`, `Settlement=4`) | el default va **en la base**, no sólo en el CLR (lección de `DosContextosUnaTablaTests`): las corridas de producción quedan `Ordinary` |
| `PayPeriodId` | int → **nullable** | `Kind = Ordinary ⇔ PayPeriodId IS NOT NULL`; en las otras cuatro es NULL |
| `CutoffDate` | date, nullable | fecha de corte del cálculo: prima 30-06 / 31-12 del semestre; cesantías 31-12 del año; vacaciones el día anterior al inicio del disfrute (o la fecha de la compensación); definitiva la fecha de retiro. Obligatoria si `Kind ≠ Ordinary` |
| `PayDate` | date, nullable | fecha de pago propia de la liquidación especial (FR-011a); es la fecha del comprobante `NM` de la aprobación (la ordinaria sigue fechando al fin del período) |
| `Year` | smallint, nullable | obligatoria en `ServiceBonus` y `Severance` |
| `Semester` | tinyint, nullable (1 \| 2) | obligatoria en `ServiceBonus` |
| `EmployeeId` | int, nullable, FK `PAY_Employees` | **sólo** en `Vacation` y `Settlement` (corridas de un solo empleado): desnormalizado para poder indexar por empleado y corte; el comando exige que sea igual al único `PayrollRunEmployee.EmployeeId` |
| `TerminationId` | int, nullable, FK `PAY_EmploymentTerminations` | sólo `Settlement`; todas las versiones de la definitiva apuntan a la misma terminación |
| `VacationMovementId` | int, nullable, FK `PAY_VacationMovements` | sólo `Vacation`; el movimiento que la originó |

**Índices**. Se retira `UK_PAY_PayrollRuns_Period_Version (PayPeriodId, Version)` y entran
cinco filtrados (uno por tipo; dos en vez de un `IN` para no depender de que el traductor de
filtros entienda `OR`):

| Nombre | Columnas | Filtro |
|---|---|---|
| `UK_PAY_PayrollRuns_Ordinary_Period_Version` | `(PayPeriodId, Version)` | `[Kind] = 0` |
| `UK_PAY_PayrollRuns_ServiceBonus_Year_Semester_Version` | `(Year, Semester, Version)` | `[Kind] = 1` |
| `UK_PAY_PayrollRuns_Severance_Year_Version` | `(Year, Version)` | `[Kind] = 2` |
| `UK_PAY_PayrollRuns_Vacation_Employee_Cutoff_Version` | `(EmployeeId, CutoffDate, Version)` | `[Kind] = 3` |
| `UK_PAY_PayrollRuns_Settlement_Employee_Cutoff_Version` | `(EmployeeId, CutoffDate, Version)` | `[Kind] = 4` |

Más `IX_PAY_PayrollRuns_Kind_Status (Kind, Status)` para las listas por tipo, y se conserva
`IX_PAY_PayrollRuns_Period_Status`.

**Invariantes**. La prima y las cesantías anuales son **una corrida por empresa** con todos los
empleados con derecho (una versión por recálculo; la unicidad es por `Year[/Semester]`); las
consultas que hoy asumen período (historial, comparativo, `balance-check`, `GetPaymentRegister`
por `periodId`) filtran `Kind = Ordinary`. FR-005 (segunda liquidación del mismo tipo, período y
empleado) se hace cumplir en el comando —busca corridas `Draft`/`Stale`/`Approved` del mismo
`Kind` con el mismo empleado y corte— y el índice es la red. **Transiciones** iguales a la
ordinaria: `Draft → Superseded` (recálculo o descarte), `Draft → Approved` (contabiliza en la
misma transacción; en `Settlement` además cierra la ficha y aplica los descuentos en Cartera),
`Approved → Reversed` (asiento espejo; en `Settlement` reabre la ficha; en `Vacation` devuelve el
movimiento a `Registered` y anula las novedades generadas). Una corrida especial no tiene
`ExceptionsJson` de período ni `ApprovedWithoutSegregation` distinto: la segregación compara
aprobador con `CalculatedBy`. `SourceType` contable por tipo: `ServiceBonusRun`, `SeveranceRun`,
`VacationRun`, `SettlementRun` (la ordinaria sigue con el suyo).

### 1.2 `PAY_PayrollRunEmployees` — `PayrollRunEmployee`

Sin columnas nuevas: `BasesJson` explica las bases del período (aquí, del semestre, del año o del
tiempo servido, con los tramos de salario en `SalaryTranchesJson`), `NotesJson` recibe las
advertencias propias de las liquidaciones —«saldo inicial no registrado y el ingreso es anterior al
arranque», «prima proporcional ya pagada en la definitiva del …»— y `Flags` gana dos valores en
`RunEmployeeFlag`: `OpeningBalanceMissing = 32` (Edge Cases) y `DeductionOverNet = 64` (los
descuentos propuestos superan el neto). Los valores que consume la liquidación (provisión
acumulada, saldo inicial, días ya disfrutados) quedan como pasos de la explicación de cada línea.

### 1.3 `PAY_PayrollRunLines` — `PayrollRunLine`

| Columna | Tipo | Regla |
|---|---|---|
| `SettlementDeductionId` | int, nullable, FK `PAY_SettlementDeductions` | la línea `DESC_CARTERA` / `LIBRANZA` de una definitiva apunta al descuento (propuesto/aplicado/motivo) del que salió |

Nada más: `Quantity`, `BaseAmount`, `Factor`, `RangeFrom/To`, `LegalParameterId` y
`ExplanationJson` ya cubren «base, días, parámetro con vigencia, fórmula» (FR-002).

### 1.4 `PAY_Employees` — `Employee`

Sólo lo laboral (Principio V). `WithholdingProcedure` (1 | 2) **ya existe** y se conserva; el
porcentaje del procedimiento 2 sigue en `PAY_EmployeeWithholdingRates`.

| Columna | Tipo | Para | Regla |
|---|---|---|---|
| `PilaContributorType` | nvarchar(2), nullable | PILA campo 5 y DIAN `TipoTrabajador` (la tabla 5.5.1 del anexo DIAN usa los mismos códigos que el tipo de cotizante PILA) | `01` dependiente, `12` aprendiz lectiva (sólo períodos anteriores a ago-2025), `19` aprendiz lectiva Ley 2466, `51` tiempo parcial…; vacío = lo deriva la regla paramétrica (`EmployeeClass` + `ApprenticeStage`) |
| `PilaContributorSubType` | nvarchar(2), nullable | PILA campo 6 y DIAN `SubTipoTrabajador` | `00` no aplica, `01` pensionado por vejez activo… |
| `HighRiskPension` | bit NOT NULL DEFAULT 0 | PILA campo 79; DIAN `AltoRiesgoPension` | |
| `DianContractType` | tinyint, nullable | DIAN `TipoContrato` | 1 término fijo, 2 indefinido, 3 obra o labor, 4 aprendizaje, 5 prácticas o pasantías. El `ContractType` actual es el código del legado y no se reinterpreta |
| `DianPaymentMethodCode` | nvarchar(3), nullable | DIAN `Pago/Metodo` (tabla 5.3.3.2) | vacío = se deriva de `PaymentMethod` (transferencia → `47`, cheque → `20`, efectivo → `10`) por la tabla de equivalencias sembrada en `PAY_CompanyPolicies` (`DianMedioPagoMapa`); si viene, manda |
| `ApprenticeStage` | tinyint, nullable (`ApprenticeStage`: `Lective=1`, `Practical=2`) | prestaciones y PILA del aprendiz (Ley 2466/2025) | obligatoria si `EmployeeClass ∈ {Apprentice, Intern}` |
| `PensionTransitionRegime` | tinyint NOT NULL DEFAULT 0 (`Unknown=0`, `Yes=1`, `No=2`) | tabla FSP desde 2027-04-01 (Ley 2381/2024) | `Unknown` es alerta PILA a partir de abril de 2027 |
| `ForeignNotRequiredToContributePension` | bit DEFAULT 0 | PILA campo 8 | |
| `ColombianAbroad` | bit DEFAULT 0 | PILA campo 9 | |
| `WorkMunicipalityDaneCode` | nvarchar(5), nullable | PILA campos 9-10 (departamento = 2 primeros dígitos, municipio = 3 últimos) y DIAN `LugarTrabajo` | vacío = el de la empresa (`PAY_PilaSettings.MunicipalityDaneCode`) |
| `WorkAddress` | nvarchar(120), nullable | DIAN `LugarTrabajoDireccion` | vacío = dirección de la empresa |
| `EconomicActivityCode` | nvarchar(7), nullable | PILA campo 98 (Decreto 768/2022) | vacío = el de la empresa |
| `WorkCenterCode` | nvarchar(9), nullable | PILA campo 62 | |
| `DisbursementBankId` | int, nullable, FK `COR_Banks` | banco destino de la dispersión (código ACH en `Bank.TransferCode`) | reemplaza al uso de `PayrollBankId` (nvarchar(4), código del legado), que se conserva y se rellena por dato: `COR_Banks.LegacyCode = PayrollBankId` |
| `PayrollBankAccountType` | int (existe) | 1 ahorros, 2 corriente | se le da nombre `BankAccountType` en Domain; sin cambio de columna |
| `PayrollBankAccountNumber` | nvarchar(25) (existe) | | sin cambio; «empleado sin cuenta» = NULL o vacío |

Índices nuevos: `IX_PAY_Employees_DisbursementBank (DisbursementBankId)`. `TerminationDate`,
`TerminationCause` y `Status = -1` se **conservan** como hoy y los escribe únicamente la
aprobación de la definitiva (`TerminationCause` recibe el nombre del motivo del catálogo); la
verdad del retiro es `PAY_EmploymentTerminations` (§2.5). `SeveranceFundId`, `HealthInsuranceId`,
`PensionFundId`, `WorkRiskId`, `WorkRiskRateId`, `FamilySubsidyId` se reutilizan tal cual.

### 1.5 `COR_People` — `Person`

| Columna | Tipo | Regla |
|---|---|---|
| `SecondLastName` | nvarchar(150), nullable | DIAN `SegundoApellido`; PILA campo 11 |
| `OtherNames` | nvarchar(150), nullable | DIAN `OtrosNombres`; PILA campo 13 |

`FirstName` y `LastName` siguen siendo el primer nombre y el primer apellido. **No hay migración de
datos que parta `LastName`** (partir «De la Hoz Mejía» por el espacio se equivoca): la validación
previa de PILA y DIAN marca como inconsistencia la persona cuyo `LastName` tiene más de una palabra
y `SecondLastName` está vacío, con enlace a Personas. `DaneCityCode` (nvarchar(20)) **ya existe** y
es el municipio DANE de residencia; el validador exige cinco dígitos cuando la persona es empleado
(DIAN no lo pide para el trabajador —usa el lugar de trabajo—, pero la representación gráfica lo
muestra). Nada más se toca en la persona.

### 1.6 `PAY_LegalParameters` — `PayrollLegalParameter` (R4)

Sin columnas nuevas. Dos convenciones nuevas sobre lo que ya hay:

- `LegalParameterKind` gana `DateInYear = 3`: `Value` guarda `MMDD` (0630, 1220, 0131, 0214) para
  las fechas límite legales, que sólo sirven para el aviso del calendario.
- Una `RangeTable` con **dos valores por tramo** (la tabla de indemnización del art. 64 CST) usa
  `FixedValue` = días del primer año y `Rate` = días por cada año adicional; `RangeIsMarginal =
  false`; `RangeUnitParameterCode = SMMLV`. La explicación lo dice con esas dos etiquetas.

Los códigos nuevos van a listas **por proceso** (`SettlementParameterCodes.Required`,
`PilaParameterCodes.Required`, `WithholdingRateParameterCodes.Required`,
`ElectronicPayrollParameterCodes.Required`) y **no** a `LegalParameterCodes.Required`, para no
negar la nómina ordinaria de las cooperativas ya desplegadas. Lista completa (vigencia
`2026-01-01` salvo que se indique; `Source` = norma exacta, artículo incluido; los que ya existen
sólo precisan `Source` o `ValidTo` por la migración de datos de §4):

| Código | Kind | Valor 2026 | Norma (`Source`) | Estado |
|---|---|---|---|---|
| `SMMLV` | Amount | 1.750.905 | Decreto 1469 de 2025 (Decreto 159/2026, mismo valor) | existe; precisar Source |
| `AUX_TRANSPORTE` | Amount | 249.095 | Decreto 1470 de 2025 | existe; precisar Source |
| `UVT` | Amount | 52.374 | Resolución DIAN 000238 de 2025 | existe; precisar Source |
| `PRIMA_DIAS_ANIO` | Amount | 30 | CST art. 306 (Ley 1788/2016) | nuevo |
| `PRIMA_FECHA_LIMITE_S1` / `_S2` | DateInYear | 0630 / 1220 | CST art. 306 | nuevo (aviso) |
| `CESANTIAS_DIAS_ANIO` | Amount | 30 | CST art. 249; Ley 50/1990 art. 99 | nuevo |
| `CESANTIAS_VENTANA_ESTABILIDAD_MESES` | Amount | 3 | CST art. 253 (Decreto 2351/1965 art. 17) | nuevo |
| `CESANTIAS_FECHA_LIMITE_CONSIGNACION` | DateInYear | 0214 | Ley 50/1990 art. 99 num. 3 | nuevo (aviso) |
| `INT_CESANTIAS_PCT` | Percent | 12 | Ley 52/1975 art. 1; Ley 50/1990 art. 99 num. 2 | nuevo |
| `INT_CESANTIAS_FECHA_LIMITE` | DateInYear | 0131 | Ley 52/1975 art. 1; Decreto 116/1976 art. 2 | nuevo (aviso) |
| `VACACIONES_DIAS_ANIO` | Amount | 15 (hábiles) | CST art. 186 | nuevo |
| `VACACIONES_COMPENSABLE_PCT` | Percent | 50 | CST art. 189 num. 1 (Ley 1429/2010 art. 20) | nuevo; `ValidFrom` 2010-12-29 |
| `INDEMNIZACION_TABLA` | RangeTable (SMMLV, no marginal; `FixedValue` días 1.er año, `Rate` días por año adicional) | <10: 30 / 20; ≥10: 20 / 15 | CST art. 64 (Ley 789/2002 art. 28) | nuevo; `ValidFrom` 2002-12-27 |
| `INDEMNIZACION_UMBRAL_SMMLV` | Amount | 10 | CST art. 64 | nuevo |
| `INDEMNIZACION_OBRA_MINIMO_DIAS` | Amount | 15 | CST art. 64 | nuevo |
| `SANCION_MORA_ART65_TOPE_MESES` | Amount | 24 | CST art. 65 (Ley 789/2002 art. 29) | nuevo (informativo) |
| `SALARIO_INTEGRAL_MINIMO_SMMLV` / `_FACTOR_PCT` | Amount / Percent | 10 / 30 | CST art. 132 | nuevo (validación de ficha) |
| `SALARIO_INTEGRAL_BASE_PCT` | Percent | 70 | CST art. 132 num. 3; Ley 100/1993 art. 18 | existe |
| `APRENDIZ_LECTIVA_APOYO_PCT` / `APRENDIZ_PRACTICA_APOYO_PCT` | Percent | 75 / 100 | Ley 2466/2025 art. 21; Circular Mintrabajo 0083/2025 | nuevo; `ValidFrom` 2025-06-25 |
| `SALUD_APRENDIZ_PCT` | Percent | 12,5 | Ley 2466/2025 art. 21 | existe; precisar Source |
| `RETEFTE_RENTA_EXENTA_TOPE_ANUAL_UVT` | Amount | 790 | ET art. 206 num. 10 (Ley 2277/2022 art. 2) | nuevo; `ValidFrom` 2023-01-01 |
| `RETEFTE_DEDUCCIONES_TOPE_ANUAL_UVT` | Amount | 1.340 | ET art. 388; DUR 1.2.4.1.6 par. 3 (Decreto 2231/2023 art. 9) | nuevo; `ValidFrom` 2023-12-22 |
| `RETEFTE_P2_DIVISOR` | Amount | 13 | ET art. 386 | nuevo |
| `CESANTIAS_EXENCION_TOPE_UVT` | Amount | 350 | ET art. 206 num. 4 | nuevo |
| `CESANTIAS_GRAVADA_TABLA_UVT` | RangeTable (UVT, no marginal; `Rate` = % **no gravado**) | ≤350: 100; 350-410: 90; 410-470: 80; 470-530: 60; 530-590: 40; 590-650: 20; >650: 0 | ET art. 206 num. 4 | nuevo |
| `INDEMNIZACION_RETEFTE_PCT` / `_TOPE_UVT` | Percent / Amount | 20 / 204 | ET art. 401-3 (Ley 788/2002 art. 92); Concepto DIAN 30573/2015 | nuevo |
| `FSP_UMBRAL_SMMLV` | Amount | 4 | Ley 100/1993 art. 27; Ley 797/2003 art. 8 | nuevo |
| `FSP_TABLA` (Ley 797) | RangeTable | (la sembrada) | Ley 797/2003 art. 8 | existe; **`ValidTo` = 2027-03-31** y Source |
| `FSP_TABLA` (Ley 2381, sin transición) | RangeTable (SMMLV, no marginal) | 4-7: 1,5; 7-11: 1,8; 11-19: 2,5; 19-20: 2,8; ≥20: 3,0 | Ley 2381/2024 art. 20; Sentencia C-264/2026 | nuevo; **`ValidFrom` 2027-04-01** (en `Revisiones()`) |
| `IBC_MINIMO_SMMLV` | Amount | 1 | Ley 797/2003 art. 5; AT2 v30 | nuevo |
| `PILA_IBC_REDONDEO` | Amount | 1 (al peso superior) | Decreto 780/2016 art. 3.2.1.5 (Decreto 1990/2016) | nuevo |
| `PILA_APORTE_REDONDEO_MULTIPLO` | Amount | 100 (al múltiplo superior) | Decreto 780/2016 art. 3.2.1.5 | nuevo |
| `PILA_PLAZO_PAGO_POR_NIT` | RangeTable (unidad nula: dos últimos dígitos del NIT; `FixedValue` = día hábil) | 00-07: 2 … 94-99: 16 | Decreto 780/2016 art. 3.2.2.1 (Decreto 923/2017) | nuevo (aviso) |
| `DIAN_PLAZO_TRANSMISION_DIAS` | Amount | 10 | Res. 227/2025 art. 1.5.3.4.1.1 | nuevo |
| `SALUD_*`, `PENSION_*`, `ARL_CLASE_*`, `CAJA_PCT`, `SENA_PCT`, `ICBF_PCT`, `EXONERACION_PARAFISCALES_TOPE_SMMLV`, `IBC_TOPE_SMMLV`, `RETEFTE_TABLA_UVT`, `RETEFTE_RENTA_EXENTA_*`, `RETEFTE_DEDUCCIONES_TOPE_*`, `RETEFTE_DEPENDIENTES_*`, `RETEFTE_INT_VIVIENDA_TOPE_UVT`, `RETEFTE_MED_PREPAGADA_TOPE_UVT`, `RETEFTE_REDONDEO`, `HORAS_MES` | | | | existen; sólo `Source` |
| `SMMLV`, `AUX_TRANSPORTE`, `UVT` **2027** | Amount | se decretan a fin de 2026 | | **faltan**: entran por `Revisiones()` o por la pantalla antes de los intereses de enero |

### 1.7 `PAY_ConceptDefinitions` — `PayrollConceptDefinition`

| Columna | Tipo | Regla |
|---|---|---|
| `AffectsVacationBase` | bit NOT NULL DEFAULT 0 | R6: «entra a la base de vacaciones e indemnización» (CST art. 192: salario ordinario sin auxilio, sin extras, sin trabajo en descanso obligatorio). La semilla lo pone en `SALARIO`, `COMISION`, `BONIF_SALARIAL`, `RECARGO_NOCTURNO`; no en `AUX_TRANSPORTE`, `HEX_*`, `RECARGO_DOMINICAL` (a confirmar con la contadora) |
| `DianElement` | nvarchar(60), nullable | ruta del concepto en el XML de la DIAN (anexo 3.1): `Devengados/Basico`, `Devengados/Transporte/AuxilioTransporte`, `Devengados/HorasExtras/HEDs`…, `Devengados/Primas/Prima`, `Devengados/Cesantias`, `Devengados/Cesantias/@PagoIntereses`, `Devengados/Vacaciones/VacacionesComunes`, `Devengados/Vacaciones/VacacionesCompensadas`, `Devengados/Indemnizacion`, `Devengados/Bonificaciones/BonificacionS`, `Deducciones/Salud`, `Deducciones/FondoPension`, `Deducciones/FondoSP`, `Deducciones/Libranzas/Libranza`, `Deducciones/Cooperativa`, `Deducciones/RetencionFuente`, `Deducciones/OtrasDeducciones`… Nulo = el concepto no va al documento (provisiones, aportes del empleador, informativos). Un concepto con valor y sin ruta es inconsistencia bloqueante al generar |

Ambas columnas se actualizan **en su sitio** en las versiones sembradas (no alteran ningún valor
calculado de la nómina ordinaria, así que no exigen versión nueva); en conceptos propios de la
cooperativa las edita ella. Conceptos **nuevos** de la semilla (Order 70), todos `Origin = Seed`,
`IsAutomatic = true` (los pone el motor de liquidaciones, no una novedad), `ApplicableClasses`
según la regla legal:

| Código | Nature | Forma | Bases | Para |
|---|---|---|---|---|
| `PRIMA` | Earning | motor | retención: sí (P2) | prima pagada (semestre o proporcional en la definitiva) |
| `CESANTIAS` | Earning | motor | ninguna | cesantías liquidadas (al fondo en la anual; al empleado en la definitiva) |
| `INT_CESANTIAS` | Earning | motor | ninguna | intereses pagados al empleado |
| `VACACIONES_LIQ` | Earning | motor | retención: sí | vacaciones disfrutadas pagadas en la liquidación |
| `VACACIONES_COMP` | Earning | motor | aportes y retención: sí | vacaciones compensadas en dinero (cotizan) |
| `INDEMNIZACION` | Earning | motor | ninguna | art. 64 CST |
| `BONIF_RETIRO` | Earning | novedad de la definitiva | ninguna | bonificación por retiro voluntario (mismo tratamiento tributario que la indemnización) |
| `SALARIO_PENDIENTE` | Earning | motor | las cuatro | salario de los días del período en la definitiva |
| `PRIMA_AJUSTE_PROV`, `CESANTIAS_AJUSTE_PROV`, `INT_CESANTIAS_AJUSTE_PROV`, `VACACIONES_AJUSTE_PROV` | Provision | motor | ninguna | diferencia entre lo liquidado y la provisión acumulada; **admite negativo** (liberación); el poster invierte débito/crédito con valor negativo |
| `RETEFTE_PRIMA`, `RETEFTE_CESANTIAS`, `RETEFTE_INDEMNIZACION` | Deduction | motor | ninguna | retención independiente de cada rubro (R5); la de salario pendiente y vacaciones usa `RETEFTE` |
| `AUSENCIA_VACACIONES` | Informative | novedad generada | `ReducesWorkedDays`, `RequiresDates` | la novedad que la liquidación de vacaciones deja en cada período cubierto cuando la política `VacacionesPagoAnticipado = true`: la ordinaria descuenta los días del salario **sin pagarlos** (los pagó la liquidación). Con la política en `false` se genera la novedad `VACACIONES` existente (la ordinaria paga los días) y la liquidación sólo contabiliza la provisión |

Se reutilizan sin cambio: `SALARIO`, `AUX_TRANSPORTE`, `SALUD_EMP`, `PENSION_EMP`, `FSP`,
`RETEFTE`, `DESC_CARTERA` (con `AffectsAccounting = false`: Cartera contabiliza), `LIBRANZA`,
`VACACIONES`, `PROV_*`. Los conceptos nuevos necesitan cuentas en `PAY_ConceptDefinitionAccounts`
(vacía tras `ContabilidadNiif`): lo parametriza la contadora
(`docs/operaciones/nomina-primer-periodo.md`).

### 1.8 Otras columnas que ganan tablas existentes

| Tabla | Columna | Para |
|---|---|---|
| `PAY_Novelties` | `VacationMovementId` (int, nullable, FK `PAY_VacationMovements`) | la novedad que generó un disfrute; `NoveltyOrigin` gana `VacationLeave = 6` (regla de anulación/regeneración de las recurrentes) |
| `PAY_EmployeeWithholdingRates` | `Origin` (tinyint NOT NULL DEFAULT 0: `Manual=0`, `Calculated=1`), `SourceCalculationId` (int, nullable, FK `PAY_WithholdingRateCalculations`) | R8: la aprobación **cierra** la vigencia anterior (`ValidTo` = día anterior) en vez de reemplazarla, y la nueva sabe de qué cálculo salió |
| `PAY_HealthInsuranceProviders`, `PAY_PensionProviders`, `PAY_WorkRiskProviders`, `PAY_FamilyCompensationFunds` | `PilaCode` (nvarchar(6), nullable; único filtrado `[PilaCode] IS NOT NULL AND [IsDeleted] = 0`) | código PILA de la administradora (campos 31/33/35/77), distinto del `Code` de la cooperativa (`CodigoDeCatalogo`) |
| `PAY_PensionProviders` | `IsAccai` (bit DEFAULT 0) | administradora del componente complementario (Ley 2381, desde 2027-04-01); informativo en 2026 |
| `PAY_PayrollPayments` | `BankDisbursementFileId` (int, nullable, FK `PAY_BankDisbursementFiles`) | la marca de pagado que dejó «marcar enviado» sabe de qué archivo vino; `Reference` sigue llevando la referencia (60) |
| `COR_Banks` | — | `TransferCode` (20) se usa como código ACH/Superintendencia del banco destino; **verificar** que el dato del legado lo traiga (duda D-10) |

---

## 2. Entidades nuevas

### 2.1 `PAY_CompanyPolicies` — `CompanyPolicy` (políticas por empresa con vigencia, R4)

Clave-valor con vigencia; lo lee `PayrollPolicyReader.ReadAsync(fecha)` y las claves de nómina
ordinaria que hoy están en `COR_SystemSettings` (`Payroll.ApplyEmployerExemption`,
`Payroll.AllowSameUserApproval`) se **copian** aquí por dato y el lector cae a `COR_SystemSettings`
sólo si la clave no existe (compatibilidad hasta retirarla).

| Campo | Tipo | Regla |
|---|---|---|
| `Key` | nvarchar(60) | catálogo cerrado en Domain (`CompanyPolicyKeys`) |
| `Value` | nvarchar(400) | texto; el lector lo tipa (`bool`, enum, decimal, JSON corto) |
| `ValidFrom` | date | |
| `ValidTo` | date, nullable | |
| `Notes` | nvarchar(300), nullable | quién decidió y por qué (la contadora) |

Único `(Key, ValidFrom)` filtrado `[IsDeleted] = 0`; dos vigencias de la misma clave no se cruzan
(regla del comando). Claves y valores por defecto (los siembra `CompanyPoliciesSeeder` sólo si la
clave no existe):

| Clave | Valores | Defecto | Quién la lee |
|---|---|---|---|
| `Exonerada114_1` | `true` / `false` | el valor migrado de `Payroll.ApplyEmployerExemption`, o `false` | aportes del empleador de la ordinaria y PILA campo 33 (FR-024a); el umbral en SMMLV es el parámetro legal `EXONERACION_PARAFISCALES_TOPE_SMMLV` |
| `SemanaLaboral` | `LunesASabado` / `LunesAViernes` | `LunesASabado` | `DiasHabiles.Contar` (vacaciones, plazo DIAN en modo hábiles) |
| `VacacionesPagoAnticipado` | `true` / `false` | `true` | qué novedad deja el disfrute (§1.7 `AUSENCIA_VACACIONES` vs `VACACIONES`) |
| `CotizaArlEnVacaciones` | `true` / `false` | `false` | PILA y aportes del empleador |
| `RetefteTopesAnualesModo` | `Acumulado` / `Mensualizado` | `Mensualizado` (lo que hoy hace la ordinaria) | depuración de retención (R5) |
| `P2SecuenciaDepuracion` | `DepurarLuegoDividir` / `DividirLuegoDepurar` | `DepurarLuegoDividir` | cálculo P2 (R8) |
| `DianPlazoComputo` | `Calendario` / `Habiles` | `Calendario` | aviso de plazo de transmisión |
| `DianMedioPagoMapa` | JSON `{ "Transfer": "47", "Check": "20", "Cash": "10" }` | ese | derivación de `DianPaymentMethodCode` |
| `DeduccionAlRetiroModo` | `SaldoTotal` / `SoloCuotasCausadas` / `NoProponer` | `SaldoTotal` | propuesta de descuentos de la definitiva (FR-018a) |
| `ArranqueNominaFecha` | `yyyy-MM-dd` | la fecha del primer período de la cooperativa | advertencia «ingreso anterior al arranque sin saldo inicial» |
| `AllowSameUserApproval` | `true` / `false` | migrado de `Payroll.AllowSameUserApproval` | segregación |

### 2.2 `PAY_Holidays` — `Holiday` (calendario de festivos, R6)

| Campo | Tipo | Regla |
|---|---|---|
| `Date` | date, único filtrado `[IsDeleted] = 0` | |
| `Name` | nvarchar(80) | |
| `Origin` | tinyint (`Ley51Fixed=1`, `Ley51MovedToMonday=2`, `Ley51Easter=3`, `Decreed=4`, `Manual=5`) | |
| `Year` | smallint | desnormalizado para el índice `(Year)` |

Semilla 2026–2028 calculada con la regla de la Ley 51 de 1983 (fijos, trasladados al lunes, los
que dependen de Pascua); inserta lo que falta por `Date`, nunca toca ni borra una fila `Decreed`
o `Manual`. Es un catálogo simple **con** auditoría (un puente decretado se registra y queda quién
lo hizo).

### 2.3 `PAY_EmployeeBenefitOpeningBalances` — `EmployeeBenefitOpeningBalance` (R3, FR-007)

| Campo | Tipo | Regla |
|---|---|---|
| `EmployeeId` | FK `PAY_Employees` | |
| `AsOfDate` | date | fecha de corte del saldo (30-11-2026 en COOFLOPAL) |
| `Kind` | tinyint (`Opening=1`, `Adjustment=2`) | |
| `PendingVacationDays` | decimal(8,2) | hábiles pendientes |
| `AccruedSeverance` | 18,2 | cesantías causadas del año a `AsOfDate` |
| `AccruedSeveranceInterest` | 18,2 | |
| `AccruedServiceBonus` | 18,2 | prima causada del semestre a `AsOfDate` |
| `ServiceBonusDaysAccrued`, `SeveranceDaysAccrued` | int, nullable | días ya contados (para que la proporción no los duplique) |
| `Notes` | nvarchar(500) | |
| `AdjustsBalanceId` | FK self, nullable | sólo `Adjustment` |
| `AdjustmentReason` | nvarchar(300), nullable | obligatorio en `Adjustment` |
| `ConsumedByRunId` | int, nullable, FK `PAY_PayrollRuns` | la primera liquidación **aprobada** que lo usó; lo escribe la aprobación |

Único `(EmployeeId, AsOfDate, Kind)` filtrado `[IsDeleted] = 0`. **Invariantes**: se edita sólo
mientras `ConsumedByRunId IS NULL`; después, corregir es una fila `Adjustment` con motivo; la
reversión de la liquidación consumidora vuelve `ConsumedByRunId` a NULL. El motor lo recibe como
«tramo inicial» y lo explica como paso propio («Saldo inicial al 30-11-2026 digitado por … el …»).
Auditoría `Payroll.OpeningBalance.Changed`.

### 2.4 `PAY_VacationMovements` — `VacationMovement` (R6, FR-014 a FR-017)

| Campo | Tipo | Regla |
|---|---|---|
| `EmployeeId` | FK | |
| `Kind` | tinyint (`Enjoyment=1`, `Compensation=2`, `Adjustment=3`, `SettlementPayout=4`) | la causación **no** es movimiento: se deriva (`DíasTrabajados × VACACIONES_DIAS_ANIO / 360` menos suspensiones) |
| `StartDate`, `EndDate` | date | obligatorias en `Enjoyment`; en `Compensation` `StartDate` = fecha de la compensación |
| `BusinessDays` | decimal(8,2) | hábiles que consume (en `Enjoyment` los cuenta `DiasHabiles.Contar` y quedan **congelados**; en `Adjustment` con signo) |
| `CalendarDays` | int | |
| `WeekPolicyUsed` | nvarchar(20) | `SemanaLaboral` vigente al registrar |
| `SkippedDaysJson` | nvarchar(max) | `[{ fecha, motivo }]` de los domingos y festivos saltados |
| `Status` | tinyint (`Registered=0`, `Liquidated=1`, `Cancelled=2`) | |
| `PayrollRunId` | int, nullable, FK `PAY_PayrollRuns` | la corrida `Vacation` (o `Settlement`) que lo liquidó |
| `Notes`, `CancelReason` | nvarchar(300) | |

Índices: `(EmployeeId, StartDate)`, `(Status)`. **Invariantes**: dos `Enjoyment` activos del mismo
empleado no se cruzan en fechas; una `Compensation` no puede llevar el total compensado sobre lo
causado por encima de `VACACIONES_COMPENSABLE_PCT` (se rechaza con el máximo permitido, FR-016);
al retiro, `SettlementPayout` con el total pendiente. **Transiciones**: `Registered → Liquidated`
(al aprobar la corrida), `Liquidated → Registered` (al reversarla), `Registered → Cancelled`
(motivo; anula las novedades `VacationLeave` que generó si sus períodos siguen abiertos; si alguno
está aprobado, se rechaza y se ofrece el ajuste retroactivo). Saldo = causado + saldo inicial −
Σ `BusinessDays` de `Enjoyment`/`Compensation`/`SettlementPayout` ± `Adjustment`; nunca se guarda.

### 2.5 `PAY_TerminationReasons` — `TerminationReason` y `PAY_EmploymentTerminations` — `EmploymentTermination` (R7)

**`TerminationReason`** (catálogo con semilla del programa; la cooperativa agrega los suyos sin
indemnización):

| Campo | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(10) (`CodigoDeCatalogo`), único filtrado `[IsDeleted] = 0` | |
| `Name` | nvarchar(120) | |
| `GeneratesSeverancePay` | bit | «genera indemnización» |
| `RequiresContractEndDate` | bit | vencimiento del término fijo / obra |
| `LegalBasis` | nvarchar(120), nullable | artículo |
| `IsSeeded`, `IsActive` | bit | un sembrado no se elimina ni cambia de marca |

Semilla (códigos de hasta 10 caracteres, la regla de `CodigoDeCatalogo`; hasta el 2026-09-21 decía
`DESP_SIN_JC`, `VENC_TERMINO` y `MUTUO_ACUERDO`, que no caben en la columna): `RENUNCIA` (no), `DESP_SINJC` despido sin justa causa (sí; CST art. 64), `DESP_JC`
despido con justa causa (no; art. 62), `VENC_TERM` vencimiento del término con preaviso (no;
art. 46), `MUTUO_ACDO` (no), `FIN_OBRA` terminación de la obra (no), `PER_PRUEBA` (no; art. 78),
`MUERTE` (no), `PENSION` reconocimiento de pensión (no).

**`EmploymentTermination`**:

| Campo | Tipo | Regla |
|---|---|---|
| `EmployeeId` | FK | |
| `TerminationDate` | date | FR-021: debe caer en un período `Open` del plan del empleado; si cae en uno `Approved` → `Payroll.PeriodApproved` con la indicación de reversar o liquidar en el abierto |
| `TerminationReasonId` | FK | |
| `ContractTypeAtTermination` | tinyint (`DianContractType`) | copiado de la ficha, editable aquí |
| `ContractEndDate` | date, nullable | término fijo / obra: para «el tiempo que faltaba» |
| `Status` | tinyint (`Registered=0`, `Settled=1`, `Reinstated=2`, `Cancelled=3`) | |
| `Notes` | nvarchar(500) | |
| `SettlementDocumentAttachmentPublicId` | uniqueidentifier, nullable | PDF para firma generado al aprobar (`COR_Attachments`, `OwnerEntityType = "EmploymentTermination"`), inmutable |
| `SignedDocumentAttachmentPublicId` | uniqueidentifier, nullable | copia firmada que sube la responsable (opcional) |
| `ReinstatedAt`, `ReinstatedBy`, `ReinstateReason` | | al reversar la definitiva aprobada |

Único filtrado `(EmployeeId)` con `[Status] IN (0, 1)` escrito como dos índices (`= 0` y `= 1`) si
el traductor no admite `IN`: una sola terminación viva por ficha. **Transiciones**: registrar
crea la terminación y la corrida `Settlement` en borrador en la misma acción; `Registered →
Settled` al aprobar la definitiva (misma transacción: `Employee.Status = -1`, `TerminationDate`,
`TerminationCause` = nombre del motivo, `Person.IsEmployee = false`, pagos en Cartera por
`ProcessPaymentCommand`, `VacationMovement.SettlementPayout`); `Settled → Reinstated` al reversar
(ficha vigente de nuevo: `Status`, `TerminationDate = MaxValue`, `Person.IsEmployee = true`; los
recaudos de Cartera se reversan por el comando de Cartera, nunca a mano); `Registered → Cancelled`
(descarta el borrador). Un `Reinstated` puede volver a terminarse: fila nueva. Auditoría
`Payroll.Employee.Terminated/Reinstated`.

### 2.6 `PAY_SettlementDeductions` — `SettlementDeduction` (FR-018a)

| Campo | Tipo | Regla |
|---|---|---|
| `TerminationId` | FK `PAY_EmploymentTerminations` | |
| `Kind` | tinyint (`CooperativeLoan=1`, `ThirdPartyLibranza=2`, `Other=3`) | |
| `LoanPortfolioId` | int, nullable, FK `LND_LoanPortfolios` | `CooperativeLoan` |
| `RecurringNoveltyId` | int, nullable, FK `PAY_RecurringNovelties` | `ThirdPartyLibranza` |
| `Description` | nvarchar(200) | número de obligación, tercero |
| `ProposedAmount` | 18,2 | saldo total (préstamo) o cuotas causadas no descontadas (libranza) |
| `ProposedBreakdownJson` | nvarchar(max) | `{ capital, intereses, mora, cuotasPendientes, cuotasCausadas }` leído de Cartera al proponer |
| `AppliedAmount` | 18,2 | `0 ≤ Applied ≤ Proposed` |
| `AdjustmentReason` | nvarchar(300), nullable | **obligatorio** si `Applied < Proposed` |
| `AdjustedBy`, `AdjustedAt` | nvarchar(100), datetime, nullable | |
| `Status` | tinyint (`Proposed=0`, `Adjusted=1`, `Applied=2`, `Reverted=3`) | |
| `CarteraTransactionPublicId` | uniqueidentifier, nullable | el recaudo que dejó `ProcessPaymentCommand` al aprobar |
| `RemainingBalanceAfter` | 18,2, nullable | `PaymentResultDto.Remaining` |

Único `(TerminationId, LoanPortfolioId)` filtrado `[LoanPortfolioId] IS NOT NULL AND [IsDeleted] = 0` y
`(TerminationId, RecurringNoveltyId)` filtrado `[RecurringNoveltyId] IS NOT NULL AND [IsDeleted] = 0`
(revisión N1, migración `SettlementDeductionsUnicosEntreVivas`: el recálculo retira en blando la deuda
que Cartera ya no trae y, si la obligación vuelve, crea otra fila con la misma llave; sin excluir las
eliminadas ese INSERT respondía 500). **Invariantes**:
Σ `AppliedAmount` ≤ neto antes de descuentos (si no, `RunEmployeeFlag.DeductionOverNet` y la
responsable baja alguno); sólo se **baja**, nunca se sube sobre lo propuesto; recalcular la
definitiva **no** repropone lo ya ajustado (respeta `Adjusted`) salvo que el saldo en Cartera haya
cambiado (aviso). **Transiciones**: `Proposed → Adjusted` (con motivo), `Proposed|Adjusted →
Applied` (al aprobar), `Applied → Reverted` (al reversar). Cada cambio emite
`Payroll.Settlement.DeductionAdjusted` con propuesto, aplicado, motivo, quién y cuándo.

### 2.7 `PAY_WithholdingRateCalculations` — `WithholdingRateCalculation` (R8) y `PAY_WithholdingRateCalculationMonths`

| Campo | Tipo | Regla |
|---|---|---|
| `EmployeeId` | FK | `WithholdingProcedure = 2` |
| `TargetYear`, `TargetSemester` | smallint, tinyint | semestre al que **regirá** el porcentaje (julio–diciembre o enero–junio) |
| `Version` | int | recálculo = versión nueva, la anterior `Superseded` |
| `CalculatedAt`, `CalculatedBy` | | |
| `MonthsConsidered` | tinyint | 12 o los de vinculación |
| `Divisor` | decimal(6,2) | `RETEFTE_P2_DIVISOR` o los meses |
| `TotalGrossIncome`, `TotalMandatoryContributions`, `TotalDeclaredDeductions`, `TotalExemptIncome` | 18,2 | |
| `DepuratedBase` | 18,2 | |
| `AverageMonthlyBase` | 18,2 | |
| `UvtValueUsed` | 18,4 | |
| `AverageInUvt` | 18,4 | |
| `TheoreticalWithholding` | 18,2 | |
| `RatePercent` | decimal(6,3) | mismo largo que `EmployeeWithholdingRate.RatePercent` |
| `DepurationSequence` | nvarchar(30) | la política aplicada |
| `TableParameterId` | FK `PAY_LegalParameters` | tabla usada (`RETEFTE_TABLA_UVT` o la del plan: `PlanTableUsed` bit) |
| `Status` | tinyint (`Calculated=0`, `Approved=1`, `Superseded=2`, `Rejected=3`) | |
| `ApprovedAt`, `ApprovedBy` | | |
| `ResultingRateId` | int, nullable, FK `PAY_EmployeeWithholdingRates` | la vigencia que abrió |
| `ExplanationJson` | nvarchar(max) | pasos, mes a mes |

Único `(EmployeeId, TargetYear, TargetSemester, Version)`. Hija **`WithholdingRateCalculationMonth`**
(`PAY_WithholdingRateCalculationMonths`): `CalculationId`, `Year`, `Month`, `GrossIncome`,
`MandatoryContributions`, `IncludedSpecialRuns` (bit: prima sí; cesantías e intereses **no**, art.
386), `SourceRunsJson` (PublicId y versión de cada corrida sumada). Único `(CalculationId, Year,
Month)`. **Transiciones**: `Calculated → Approved` cierra la `EmployeeWithholdingRate` vigente
(`ValidTo` = día anterior al inicio del semestre) y abre la nueva (`ValidFrom` 1-jul/1-ene,
`ValidTo` 31-dic/30-jun, `Origin = Calculated`, `SourceCalculationId`); auditoría
`Payroll.WithholdingRate.Approved` y la misma `Payroll.EmployeeWithholding.Changed` de hoy.
`Calculated → Superseded` por recálculo; `Calculated → Rejected` con motivo.

### 2.7a `PAY_SeveranceFundDeposits` — `SeveranceFundDeposit` (FR-012)

La consignación de las cesantías anuales a cada fondo. El contrato (`api.md` §3.2,
`POST /{runId}/funds/{fundId}/mark-deposited`) la pedía y la tabla no estaba en este documento;
se agregó al implementar N1 (T010, 2026-09-21).

| Campo | Tipo | Regla |
|---|---|---|
| `PayrollRunId` | FK `PAY_PayrollRuns` | la corrida `Severance` **aprobada** |
| `SeveranceFundId` | FK `PAY_SeveranceProviders` | |
| `DepositedAt` | date | fecha de la consignación, digitada al marcar |
| `DepositedBy` | nvarchar(100) | quién marcó |
| `Reference` | nvarchar(60), nullable | referencia del pago o planilla del fondo |
| `Amount` | 18,2 | lo consignado, como quedó en la relación por fondo al marcar |

Único `(PayrollRunId, SeveranceFundId)`. **Invariantes**: sólo sobre corrida `Approved`
(`Payroll.Severance.NotApproved`); marcar dos veces → `Payroll.Severance.AlreadyDeposited`; la
reversión de la corrida **no** la borra (la consignación ocurrió aunque el asiento se reverse; queda
como historial y la pantalla lo muestra). El aviso de `CESANTIAS_FECHA_LIMITE_CONSIGNACION` se calla
para los fondos que tienen fila. Auditoría `Payroll.Severance.Deposited`.

### 2.8 PILA (R9): `PAY_PilaSettings`, `PAY_PilaGenerations`, `PAY_PilaGenerationLines`, `PAY_PilaIssues`

El **layout** (registros tipo 1 y 2 del AT2 v30) es un recurso JSON embebido con `ValidFrom`
(`Application/Payroll/Pila/Layouts/at2-v30-2026-07-24.json`), no una tabla; cada generación
guarda qué versión usó.

**`PilaSettings`** (fila única; datos del aportante que la Res. 2388 pide y la empresa no tiene):

| Campo | Tipo | Regla |
|---|---|---|
| `ContributorType` | nvarchar(1) | campo 8 del registro 1 (`1` empleador) |
| `ContributorClass` | nvarchar(1) | campo 11 (`A` ≥ 200 cotizantes, `B` < 200, `C`, `D`, `I`) |
| `PresentationForm` | nvarchar(1) | campo 9 (`U` única, `S` sucursal) |
| `BranchCode`, `BranchName` | nvarchar(10), nvarchar(40) | campos 10-11 si `S` |
| `ArlPilaCode` | nvarchar(6) | campo 12 |
| `MunicipalityDaneCode` | nvarchar(5) | departamento + municipio de la sede (defecto de la ficha) |
| `EconomicActivityCode` | nvarchar(7) | Decreto 768/2022 (defecto de la ficha) |
| `OperatorName` | nvarchar(40) | «Aportes en Línea» (informativo) |
| `PlanillaType` | nvarchar(1) | `E` (planillas N y A quedan fuera) |

El NIT/DV se leen de `COR_Companies` (`TaxId`, `TaxIdCheckDigit`); la fecha límite de pago se
propone con `PILA_PLAZO_PAGO_POR_NIT` y los dos últimos dígitos del NIT.

**`PilaGeneration`** (transacción, inmutable):

| Campo | Tipo | Regla |
|---|---|---|
| `Year`, `Month` | smallint, tinyint | período de **pago** (campo 16); el de salud (campo 15) es el anterior |
| `Version` | int | |
| `Status` | tinyint (`Validated=0`, `Generated=1`, `Uploaded=2`, `Superseded=3`) | `Validated` = con inconsistencias bloqueantes, sin archivo |
| `LayoutVersion` | nvarchar(40) | `at2-v30-2026-07-24` |
| `GeneratedAt`, `GeneratedBy` | | |
| `ExemptionApplied` | bit | `Exonerada114_1` vigente al primer día del período |
| `ContributorCount`, `LineCount` | int | campos 19 y 20 |
| `TotalIbcHealth`, `TotalIbcPension`, `TotalIbcWorkRisk`, `TotalIbcFamilyCompensation` | 18,2 | |
| `TotalHealth`, `TotalPension`, `TotalSolidarityFund`, `TotalWorkRisk`, `TotalFamilyCompensation`, `TotalSena`, `TotalIcbf`, `TotalContributions` | 18,2 | Σ campos 47/55/(51+52)/63/65/67/69 |
| `ReconciliationJson` | nvarchar(max) | cuadre por subsistema contra los `NM` del mes (FR-027): esperado, archivo, diferencia |
| `SourceRunsJson` | nvarchar(max) | PublicId y versión de las corridas aprobadas sumadas |
| `FileName`, `FileSha256` | nvarchar(120), nvarchar(64) | |
| `FileAttachmentPublicId` | uniqueidentifier, nullable | `.txt` en `COR_Attachments` (`OwnerEntityType = "PilaGeneration"`) |
| `BlockingIssueCount`, `WarningCount` | int | |
| `ProposedPaymentDueDate` | date, nullable | |
| `UploadedAt`, `UploadedBy`, `OperatorFilingNumber` (nvarchar(40)), `OperatorFilingDate` | | digitados al marcar «cargada» |

Único `(Year, Month, Version)`. **Transiciones**: generar crea `Validated` (si hay bloqueantes) o
`Generated` (con archivo); `Generated → Uploaded` (radicado en el operador; permiso
`Payroll.Pila.MarkUploaded`); regenerar crea `Version + 1` y deja la anterior `Superseded`
(consultable, con su archivo). Nada se edita.

**`PilaGenerationLine`** (`AuditableEntityLong`; una fila por registro tipo 2):

| Campo | Tipo | Regla |
|---|---|---|
| `GenerationId` | FK | |
| `LineNumber` | int | campo 1 |
| `EmployeeId` | FK | |
| `ContributorType`, `ContributorSubType` | nvarchar(2) | campos 5-6 |
| `NoveltyFlags` | nvarchar(20) | `ING`, `RET`, `VSP`, `VST`, `SLN`, `IGE`, `LMA`, `VAC`, `IRL`… tal como salieron (campos 21-30), concatenadas |
| `DaysHealth`, `DaysPension`, `DaysWorkRisk`, `DaysFamilyCompensation` | tinyint | campos 36-39 |
| `Salary` | 18,2 | campo 40 |
| `IbcHealth`, `IbcPension`, `IbcWorkRisk`, `IbcFamilyCompensation` | 18,2 | campos 42-45 |
| `HealthRate`, `PensionRate`, `WorkRiskRate`, `FamilyCompensationRate`, `SenaRate`, `IcbfRate` | 9,4 | campos 46/54/62/64/66/68 |
| `Health`, `Pension`, `SolidarityFund`, `SubsistenceFund`, `WorkRisk`, `FamilyCompensation`, `Sena`, `Icbf` | 18,2 | campos 47/55/51/52/63/65/67/69 |
| `FieldsJson` | nvarchar(max) | los 98 campos por número, ya formateados (`{"1":"00001","2":"CC",…}`) |
| `RecordText` | nvarchar(800) | la línea de 693 posiciones tal como se escribió |
| `ExplanationJson` | nvarchar(max) | por valor: de qué líneas de corrida salió, redondeo aplicado, tarifa y su vigencia |

Único `(GenerationId, LineNumber)`; índice `(GenerationId, EmployeeId)`.

**`PilaIssue`**:

| Campo | Tipo | Regla |
|---|---|---|
| `GenerationId` | FK | |
| `Severity` | tinyint (`Blocking=1`, `Warning=2`) | taxonomía de Aportes en Línea (Error / Alerta) |
| `Code` | nvarchar(40) | `Pila.SinEps`, `Pila.SinCodigoPila`, `Pila.DocumentoLargo`, `Pila.DiasNoSuman30`, `Pila.TarifaSinVigencia`, `Pila.IbcDistintoEntreSubsistemas`… |
| `FieldNumber` | tinyint, nullable | |
| `EmployeeId` | int, nullable, FK | |
| `Message` | nvarchar(500) | en lenguaje llano |
| `LinkRoute` | nvarchar(200), nullable | ruta a la ficha / catálogo |

Índice `(GenerationId, Severity)`.

### 2.9 Nómina electrónica (R10 con la precisión IV): habilitación, numeración, documentos, transmisiones

Todo vive **aquí**, en la base de la cooperativa. El servicio central no guarda nada (§5).

**`PAY_ElectronicPayrollSettings` — `ElectronicPayrollSettings`** (fila única):

| Campo | Tipo | Regla |
|---|---|---|
| `EmployerTaxId`, `EmployerCheckDigit` | nvarchar(15), nvarchar(1) | NIT y DV (se proponen desde `COR_Companies`, se confirman aquí) |
| `EmployerBusinessName` | nvarchar(150) | `RazonSocial` |
| `EmployerMunicipalityDaneCode` | nvarchar(5) | `LugarGeneracionXML` y `Empleador` |
| `EmployerAddress` | nvarchar(120) | |
| `EmployerCountryCode` | nvarchar(2) | `CO` |
| `Mode` | tinyint (`OwnSoftware=1`, `TechnologyProvider=2`) | se arranca en `OwnSoftware`; `TechnologyProvider` sólo cuando el servicio lo tenga habilitado (lo dice el servicio en `GET /v1/version` → `modos.proveedorTecnologico`, no una columna; D-16) |
| `Environment` | tinyint (`Production=1`, `Testing=2`) | los mismos códigos que el atributo `Ambiente` del XML |
| `SoftwareId` | nvarchar(36) | del catálogo DIAN de la cooperativa (modo propio) |
| `TestSetId` | nvarchar(36), nullable | |
| `TestSetStatus` | tinyint (`NotStarted=0`, `InProgress=1`, `Accepted=2`, `Failed=3`) | |
| `TestSetAcceptedAt`, `TestSetResultJson` | | |
| `CertificateSecretName` | nvarchar(120) | **sólo el nombre** del Secret de Kubernetes con el `.p12` y su contraseña (patrón `nomina-electronica-{slug}-certificado`); lo crea `tools/scripts/crear-secreto-nomina-electronica.ps1` |
| `PinSecretName` | nvarchar(120) | idem para el PIN (`nomina-electronica-{slug}-pin`) |
| `CertificateThumbprint`, `CertificateExpiresAt` | nvarchar(64), date, nullable | los devuelve el servicio al validar la habilitación; para la alerta de 30 días |
| `IsEnabled` | bit | `false` hasta que estén NIT, `SoftwareId`, los dos nombres de Secret y (en producción) el set aceptado; transmitir con `IsEnabled = false` → `ElectronicPayroll.EnablementIncomplete` con la lista de lo que falta (FR-031) |
| `EnabledAt`, `EnabledBy` | | |

Ningún secreto, PIN, `.p12` ni contraseña se guarda en esta tabla ni en ninguna otra del ERP.
La autenticación ERP → servicio no necesita un secreto por cooperativa en la base: la API acuña un
token de servicio RS256 con la llave de la identidad central y el claim `tenant`, y el servicio lo
verifica con la clave pública y exige que el claim coincida con el `{tenantId}` de la ruta y que los
nombres de Secret pedidos lleven el prefijo de ese tenant.

**`PAY_ElectronicPayrollNumberingRanges` — `ElectronicPayrollNumberingRange`** (numeración
**interna** del empleador, sin resolución DIAN; por tipo y ambiente, los de pruebas no valen en
producción):

| Campo | Tipo | Regla |
|---|---|---|
| `DocumentType` | smallint (102 \| 103) | |
| `Environment` | tinyint | |
| `Prefix` | nvarchar(10) | letras y dígitos, sin espacios ni guiones (`NumeroSecuenciaXML` = prefijo + consecutivo) |
| `RangeFrom`, `RangeTo` | bigint | |
| `LastIssuedNumber` | bigint | **no** se llama `NextNumber` (dispararía `NingunModuloEscribeMovimientosFueraDelContrato`); se incrementa bajo `RowVersion` en la misma transacción que crea el documento |
| `ValidFrom`, `ValidTo` | date | |
| `IsActive` | bit | |

Único `(DocumentType, Environment, Prefix, ValidFrom)` filtrado `[IsDeleted] = 0`; un solo rango
activo por `(DocumentType, Environment)` (regla del comando). Agotado → `ElectronicPayroll.RangeExhausted`.

**`PAY_ElectronicPayrollDocuments` — `ElectronicPayrollDocument`** (transacción, inmutable):

| Campo | Tipo | Regla |
|---|---|---|
| `EmployeeId` | FK | |
| `Year`, `Month` | smallint, tinyint | mes del pago |
| `DocumentType` | smallint (102 individual, 103 ajuste) | |
| `NoteType` | tinyint, nullable (`Replace=1`, `Eliminate=2`) | sólo 103 |
| `AdjustsDocumentId` | int, nullable, FK self | el documento (o nota) que esta nota reemplaza o elimina |
| `ReplacedByDocumentId` | int, nullable, FK self | referencia en el otro sentido |
| `NumberingRangeId` | FK | |
| `Prefix`, `Consecutive`, `Number` | nvarchar(10), bigint, nvarchar(30) | `Number` = prefijo + consecutivo; **nunca se reutiliza** (ni el de un rechazado) |
| `Environment` | tinyint | el de la habilitación al generar |
| `GenerationDate`, `GenerationTime` | date, time | `FechaGen`/`HoraGen` (con GMT) que entran al CUNE |
| `Cune` | nvarchar(96), nullable | lo devuelve el servicio al completar el XML; único filtrado `[Cune] IS NOT NULL` |
| `Status` | tinyint (`Generated=0`, `Signed=1`, `InProcess=2`, `Accepted=3`, `Rejected=4`, `Superseded=5`) | |
| `AttemptCount` | int | |
| `LastTransmissionId` | int, nullable, FK `PAY_ElectronicPayrollTransmissions` | |
| `TotalAccrued`, `TotalDeductions`, `TotalNet` | 18,2 | `DevengadosTotal`, `DeduccionesTotal`, `ComprobanteTotal` (en `Eliminate` los tres en cero) |
| `WorkedDays` | int | `TiempoLaborado` |
| `PaymentDatesJson` | nvarchar(400) | `FechasPagos` (las quincenas y las especiales del mes) |
| `SourceRunsJson` | nvarchar(max) | PublicId, versión, tipo y totales de cada corrida sumada |
| `SourceFingerprint` | nvarchar(64) | SHA-256 de las líneas fuente normalizadas: si cambia tras `Accepted`, hace falta nota de ajuste |
| `UnsignedXmlAttachmentPublicId` | uniqueidentifier | lo que el ERP construyó |
| `SignedXmlAttachmentPublicId`, `ZipAttachmentPublicId`, `ApplicationResponseAttachmentPublicId`, `GraphicPdfAttachmentPublicId` | uniqueidentifier, nullable | los devuelve el servicio (firmado, ZIP, ApplicationResponse) o los genera el ERP (PDF con QR); `COR_Attachments` con `OwnerEntityType = "ElectronicPayrollDocument"`, inmutables, retención ≥ 5 años |
| `ZipKey` | nvarchar(40), nullable | set de pruebas (`SendTestSetAsync`) |
| `DianStatusCode`, `DianStatusDescription` | nvarchar(2), nvarchar(300), nullable | último estado |
| `AcceptedAt` | datetime, nullable | |
| `TranslatedErrorsJson` | nvarchar(max), nullable | `[{ regla: "NIE…", tipo: "Rechazo|Notificación", texto llano }]` del último intento |
| `QrUrl` | nvarchar(200), nullable | `catalogo-vpfe[-hab]…/searchqr?documentkey=CUNE` |

Índices: único `(Environment, Prefix, Consecutive)`; `(EmployeeId, Year, Month, DocumentType)`;
`(Status)`; `(Cune)` filtrado. **Invariantes**: un solo 102 «vivo» (`Status ∉ {Rejected,
Superseded}` y no eliminado por nota) por `(EmployeeId, Year, Month, Environment)` (regla del
comando); un empleado sin pago en el mes no genera documento; una nota puede ajustar a otra nota;
nunca `Accepted` sin `Cune` **y** `ApplicationResponse`. **Transiciones**: `Generated →
Signed` (el servicio devolvió XML firmado + CUNE, aún no transmitido; sólo si se pidió firmar sin
transmitir) → `InProcess` (enviado, sin respuesta definitiva) → `Accepted` | `Rejected`;
`InProcess` se resuelve **consultando** `GetStatus(CUNE)` con backoff (1, 5, 15, 60 min, máx. 24 h;
un `IHostedService` que recorre el directorio y abre la conexión de **cada** cooperativa, Principio
IV), nunca reenviando; `Rejected` → se corrige y se genera un documento **nuevo** con número nuevo
(el rechazado conserva su historial); `Accepted` con `SourceFingerprint` cambiado → se genera una
nota 103 (`Replace` con los nuevos totales; `Eliminate` si el mes queda en cero) y el original pasa
a `Superseded` **sólo cuando la nota es `Accepted`**. Nada se transmite sin la acción explícita
(`Payroll.ElectronicPayroll.Transmit`).

**`PAY_ElectronicPayrollTransmissions` — `ElectronicPayrollTransmission`** (un intento):

| Campo | Tipo | Regla |
|---|---|---|
| `DocumentId` | FK | |
| `Attempt` | int | |
| `Operation` | tinyint (`SendNominaSync=1`, `SendTestSetAsync=2`, `GetStatus=3`, `GetStatusZip=4`, `SignOnly=5`) | |
| `RequestedAt`, `RequestedBy` | | `RequestedBy` = `system` en las consultas automáticas de estado |
| `CompletedAt`, `DurationMs` | datetime, int, nullable | |
| `Environment` | tinyint | |
| `ServiceCorrelationId` | uniqueidentifier | el que devuelve el servicio (para cruzar con sus logs, que no llevan XML) |
| `ServiceHttpStatus` | smallint, nullable | |
| `Outcome` | tinyint (`Accepted=1`, `Rejected=2`, `InProcess=3`, `TransportError=4`, `ServiceError=5`, `Signed=6`) | |
| `DianStatusCode`, `IsValid`, `StatusDescription`, `StatusMessage` | nvarchar(2), bit nullable, nvarchar(300), nvarchar(1000) | crudos de `DianResponse` |
| `RawErrorsJson`, `TranslatedErrorsJson` | nvarchar(max), nullable | `ErrorMessage[]` tal cual y traducidos por el diccionario NIE del servicio |
| `XmlDocumentKey`, `ZipKey` | nvarchar(96), nvarchar(40), nullable | |
| `ApplicationResponseAttachmentPublicId` | uniqueidentifier, nullable | la respuesta de **este** intento |

Único `(DocumentId, Attempt)`. Nunca se edita ni se borra.

### 2.10 Dispersión bancaria (R11): `PAY_BankDisbursementFormats`, `…FormatFields`, `PAY_BankDisbursementFiles`, `…FileLines`

**`BankDisbursementFormat`** (formato en datos, con vigencia; una fila por banco y versión):

| Campo | Tipo | Regla |
|---|---|---|
| `BankId` | FK `COR_Banks` | banco pagador |
| `Code` | nvarchar(10) (`CodigoDeCatalogo`), único filtrado `[IsDeleted] = 0` | `AVVILLAS-1`, `CSV-GENERICO` |
| `Name` | nvarchar(120) | |
| `ValidFrom`, `ValidTo` | date | un cambio de banco o de formato a mitad de año no toca los archivos anteriores (Edge Cases) |
| `FileKind` | tinyint (`FixedWidth=1`, `Delimited=2`) | |
| `Delimiter` | nvarchar(5), nullable | `Delimited` |
| `Encoding` | nvarchar(20) | `ASCII`, `UTF-8`, `Windows-1252` |
| `LineEnding` | tinyint (`Crlf=1`, `Lf=2`) | |
| `DecimalPlaces`, `DecimalSeparator`, `AmountInCents` | tinyint, nvarchar(1), bit | |
| `DateFormat` | nvarchar(20) | `yyyyMMdd`… |
| `TextTransform` | tinyint (`None=0`, `Upper=1`, `UpperNoAccents=2`) | |
| `FileNamePattern` | nvarchar(120) | `PAGO_{yyyyMMdd}_{seq}.txt` |
| `HasHeader`, `HasTrailer` | bit | |
| `OriginAccountNumber`, `OriginAccountType` | nvarchar(30), tinyint | cuenta origen de la cooperativa (FR-032) |
| `OriginAgreementCode` | nvarchar(20), nullable | convenio / código de empresa en el banco |
| `Origin` | tinyint (`Seed=1`, `Custom=2`) | |
| `IsActive` | bit | |
| `Notes` | nvarchar(500) | «estructura pendiente del dueño» en la fila AV Villas |

**`BankDisbursementFormatField`**:

| Campo | Tipo | Regla |
|---|---|---|
| `FormatId` | FK (Restrict) | |
| `Record` | tinyint (`Header=1`, `Detail=2`, `Trailer=3`) | |
| `Order` | int | |
| `Name` | nvarchar(60) | |
| `Source` | tinyint (`Constant`, `OriginAccountNumber`, `OriginAccountType`, `OriginAgreementCode`, `CompanyTaxId`, `CompanyName`, `EmployeeIdType`, `EmployeeTaxId`, `EmployeeFullName`, `EmployeeEmail`, `DestinationBankCode`, `DestinationAccountType`, `DestinationAccountNumber`, `Amount`, `Reference`, `PaymentDate`, `FileDate`, `LineSequence`, `DetailCount`, `TotalAmount`) | |
| `ConstantValue` | nvarchar(120), nullable | |
| `StartPosition`, `Length` | int, nullable / int | posición 1-based (ancho fijo) y largo (o máximo, delimitado) |
| `Alignment` | tinyint (`Left=1`, `Right=2`) | |
| `PadChar` | nvarchar(1) | espacio o `0` |
| `DataType` | tinyint (`Text`, `Integer`, `Amount`, `Date`) | |
| `Format` | nvarchar(40), nullable | formato de fecha o de importe si difiere del general |
| `ValueMapJson` | nvarchar(400), nullable | equivalencias (`{"1":"CA","2":"CC"}`, `{"C":"CC","E":"CE","P":"PA"}`) |
| `Required` | bit | valor vacío en campo requerido → el empleado queda en pendientes con motivo |

Único `(FormatId, Record, Order)`; en ancho fijo el validador exige que los campos no se solapen y
cubran el registro.

**`BankDisbursementFile`** (transacción):

| Campo | Tipo | Regla |
|---|---|---|
| `PayrollRunId` | FK `PAY_PayrollRuns` | corrida **Approved** (ordinaria o especial: cada una su archivo, FR-011a) |
| `BankId`, `FormatId` | FK | el formato vigente al generar |
| `Status` | tinyint (`Generated=0`, `Sent=1`, `Voided=2`) | |
| `GeneratedAt`, `GeneratedBy` | | |
| `PaymentDate` | date | la que va al archivo y a `PayrollPayment.PaidAt` |
| `Reference` | nvarchar(60) | referencia del archivo (va a `PayrollPayment.Reference`) |
| `LineCount`, `TotalAmount` | int, 18,2 | cuadran con la relación de pago menos los excluidos |
| `ExcludedCount`, `ExcludedJson` | int, nvarchar(max) | empleados sin cuenta o con campo requerido vacío, con motivo (quedan en «pendientes») |
| `FileName`, `FileSha256`, `FileAttachmentPublicId` | | `COR_Attachments` (`OwnerEntityType = "BankDisbursementFile"`) |
| `SentAt`, `SentBy`, `SentNotes` | | |
| `VoidedAt`, `VoidedBy`, `VoidReason` | | |

**`BankDisbursementFileLine`**: `FileId`, `LineNumber`, `PayrollRunEmployeeId` (FK), `EmployeeId`,
`DestinationBankId`, `AccountType`, `AccountNumber` (25), `Amount`, `RecordText` (nvarchar(600)),
`PayrollPaymentId` (int, nullable: la marca que dejó «enviado»). Único `(FileId, LineNumber)`;
índice `(PayrollRunEmployeeId)`.

**Invariantes y transiciones**: sólo se genera desde una corrida `Approved`; un
`PayrollRunEmployee` está en a lo sumo un archivo no anulado de su corrida (regla del comando) y
si ya tiene pago vigente no entra; `Generated → Sent` llama `MarkPaymentsCommand(RunPublicId,
empleados del archivo, PaidAt = PaymentDate, Transfer, Reference)` en la misma transacción y
bloquea la reversión como la marca manual (`Payroll.PaymentBlocksReversal`); `Generated → Voided`
(nada pagado); `Sent` **no** se anula: cada marca se retira una a una con
`RevertPaymentMarkCommand` como hoy. Auditoría `Payroll.Dispersion.Generated/Sent`.

---

## 3. Resumen de tablas, claves e índices únicos

| Tabla | Entidad | Único (filtrado donde se dice) |
|---|---|---|
| `PAY_PayrollRuns` (cambia) | `PayrollRun` | cinco filtrados por `Kind` (§1.1) |
| `PAY_CompanyPolicies` | `CompanyPolicy` | `(Key, ValidFrom)` · `[IsDeleted] = 0` |
| `PAY_Holidays` | `Holiday` | `(Date)` · `[IsDeleted] = 0` |
| `PAY_EmployeeBenefitOpeningBalances` | `EmployeeBenefitOpeningBalance` | `(EmployeeId, AsOfDate, Kind)` · `[IsDeleted] = 0` |
| `PAY_VacationMovements` | `VacationMovement` | — (cruce de fechas: regla del comando) |
| `PAY_TerminationReasons` | `TerminationReason` | `(Code)` · `[IsDeleted] = 0` |
| `PAY_EmploymentTerminations` | `EmploymentTermination` | `(EmployeeId)` · `[Status] = 0` y `(EmployeeId)` · `[Status] = 1` |
| `PAY_SettlementDeductions` | `SettlementDeduction` | `(TerminationId, LoanPortfolioId)` · not null y `[IsDeleted] = 0`; `(TerminationId, RecurringNoveltyId)` · not null y `[IsDeleted] = 0` |
| `PAY_WithholdingRateCalculations` | `WithholdingRateCalculation` | `(EmployeeId, TargetYear, TargetSemester, Version)` |
| `PAY_WithholdingRateCalculationMonths` | `WithholdingRateCalculationMonth` | `(CalculationId, Year, Month)` |
| `PAY_SeveranceFundDeposits` | `SeveranceFundDeposit` | `(PayrollRunId, SeveranceFundId)` |
| `PAY_PilaSettings` | `PilaSettings` | fila única (regla del comando) |
| `PAY_PilaGenerations` | `PilaGeneration` | `(Year, Month, Version)` |
| `PAY_PilaGenerationLines` | `PilaGenerationLine` (Long) | `(GenerationId, LineNumber)` |
| `PAY_PilaIssues` | `PilaIssue` | — |
| `PAY_ElectronicPayrollSettings` | `ElectronicPayrollSettings` | fila única |
| `PAY_ElectronicPayrollNumberingRanges` | `ElectronicPayrollNumberingRange` | `(DocumentType, Environment, Prefix, ValidFrom)` · `[IsDeleted] = 0` |
| `PAY_ElectronicPayrollDocuments` | `ElectronicPayrollDocument` | `(Environment, Prefix, Consecutive)`; `(Cune)` · not null |
| `PAY_ElectronicPayrollTransmissions` | `ElectronicPayrollTransmission` | `(DocumentId, Attempt)` |
| `PAY_BankDisbursementFormats` | `BankDisbursementFormat` | `(Code)` · `[IsDeleted] = 0` |
| `PAY_BankDisbursementFormatFields` | `BankDisbursementFormatField` | `(FormatId, Record, Order)` |
| `PAY_BankDisbursementFiles` | `BankDisbursementFile` | — |
| `PAY_BankDisbursementFileLines` | `BankDisbursementFileLine` | `(FileId, LineNumber)` |

Toda FK es `Restrict`. Todas las tablas nuevas llevan `PublicId` único, soft-delete y filtro de
consulta; las de transacción (corridas, generaciones PILA y sus líneas, documentos y transmisiones
DIAN, archivos de dispersión y sus líneas) quedan además bajo la vigilancia de
`PrincipioXI_ContableImmutable` (ningún comando las actualiza ni las borra; la lista de tipos
inmutables de esa prueba se amplía con ellas).

---

## 4. Migraciones y semillas

### 4.1 Migraciones EF (par PostgreSQL / SQL Server)

Tres migraciones **aditivas**, una por bloque de entrega, para que N1 pueda promoverse sin cargar
con el esquema de la PILA o la DIAN. Ninguna borra datos ni tablas, así que ninguna lleva el
marcador `MIGRACION-DESTRUCTIVA-APROBADA`; producción exige de todos modos respaldo por
cooperativa antes de aplicarlas (Principio IV: la aplica `tools/IngenIA365ERP.DbMigrator` base por
base; la API sólo verifica).

1. **`NominaPrestacionesYDian`** (entrega N1; el nombre lo fijó el plan):
   - `PAY_PayrollRuns`: `Kind` con **default 0 en la base**, `PayPeriodId` nullable, `CutoffDate`,
     `PayDate`, `Year`, `Semester`, `EmployeeId`, `TerminationId`, `VacationMovementId`;
     `DropIndex UK_PAY_PayrollRuns_Period_Version` y los cinco filtrados de §1.1. Este cambio toca
     la tabla más viva de producción: respaldo, y una prueba estilo `DosContextosUnaTablaTests` que
     inserta una corrida sin `Kind` desde los dos proveedores y comprueba que queda `Ordinary`.
   - `PAY_PayrollRunLines.SettlementDeductionId`; `PAY_Novelties.VacationMovementId`;
     `PAY_EmployeeWithholdingRates.Origin/SourceCalculationId`;
     `PAY_ConceptDefinitions.AffectsVacationBase/DianElement`; `COR_People.SecondLastName/OtherNames`;
     las columnas de `PAY_Employees` de §1.4 (con FK a `COR_Banks`).
   - Tablas nuevas: `PAY_CompanyPolicies`, `PAY_Holidays`, `PAY_EmployeeBenefitOpeningBalances`,
     `PAY_VacationMovements`, `PAY_TerminationReasons`, `PAY_EmploymentTerminations`,
     `PAY_SettlementDeductions`, `PAY_WithholdingRateCalculations` (+ `Months`),
     `PAY_SeveranceFundDeposits` (§2.7a).
   - **Datos (idempotentes, `INSERT … WHERE NOT EXISTS` / `UPDATE … WHERE` con la condición
     exacta)**: copiar `Payroll.ApplyEmployerExemption` → `Exonerada114_1` y
     `Payroll.AllowSameUserApproval` → `AllowSameUserApproval` en `PAY_CompanyPolicies` con
     `ValidFrom = 2026-01-01`; rellenar `PAY_Employees.DisbursementBankId` desde
     `COR_Banks.LegacyCode = PayrollBankId`; precisar `Source` de los parámetros existentes **sólo
     donde** `Source` todavía es el texto genérico de la semilla (una vigencia editada a mano no se
     pisa); poner `ValidTo = 2027-03-31` a la `FSP_TABLA` vigente si sigue abierta.
   - `Down`: quita columnas y tablas y recrea `UK_PAY_PayrollRuns_Period_Version`. Es reversible
     **sólo mientras no exista una corrida con `Kind ≠ 0`** (con `PayPeriodId` NULL el índice no
     se puede recrear): la migración lo declara en la cabecera y el `Down` falla con mensaje antes
     de tocar nada si las hay.
2. **`NominaPilaYNominaElectronica`** (N2 + N3): `PilaCode` e `IsAccai` en los catálogos;
   `PAY_PilaSettings`, `PAY_PilaGenerations`, `PAY_PilaGenerationLines`, `PAY_PilaIssues`;
   `PAY_ElectronicPayrollSettings`, `PAY_ElectronicPayrollNumberingRanges`,
   `PAY_ElectronicPayrollDocuments`, `PAY_ElectronicPayrollTransmissions`. Reversible (todo nuevo).
3. **`NominaDispersionBancaria`** (N4): las cuatro tablas de §2.10 y
   `PAY_PayrollPayments.BankDisbursementFileId`. Reversible.

El plan cuenta «2 migraciones pares»; si N2–N4 se promueven juntas, la 2 y la 3 se funden en una
(`NominaPilaDianYDispersion`). Cada una se genera dos veces (`--project
IngenIA365ERP.Persistence.Migrations.PostgreSql` y `…SqlServer`) y se comprueba con
`DbMigrator` contra las dos bases de prueba antes del commit.

### 4.2 Semillas nuevas y cómo alcanzan a las bases ya sembradas

Todas son `IDataSeeder` con `Scope = Tenant`, idempotentes por clave natural, y corren en el alta
de cooperativa y al arrancar (`SeedOrchestrator`), así que las cooperativas ya desplegadas las
reciben en el siguiente despliegue **sin migración de datos**; lo que la persona ya editó no se
pisa.

| Seeder | Order | Clave natural | Qué hace en una base ya sembrada |
|---|---|---|---|
| `PayrollConceptDefinitionsSeeder` (existente) | 70 | `Code` | inserta los conceptos de §1.7 que falten; en las versiones **sembradas** pone `AffectsVacationBase` y `DianElement` en su sitio; no toca conceptos `Custom` |
| `PayrollLegalParametersSeeder` (existente) | 71 | `(Code, ValidFrom)` | inserta los códigos nuevos de §1.6 con vigencia 2026; `Revisiones()` deja de estar vacía: `FSP_TABLA` Ley 2381 desde 2027-04-01 cerrando la anterior el 2027-03-31 sólo si sigue abierta y nadie la tocó; los valores 2027 de SMMLV/auxilio/UVT entran ahí cuando se decreten |
| `TerminationReasonsSeeder` | 72 | `Code` | inserta los nueve motivos; corrige `Name`/`LegalBasis` de los `IsSeeded`; nunca cambia `GeneratesSeverancePay` de uno existente ni borra los propios |
| `HolidaysSeeder` | 73 | `Date` | 2026–2028 por la regla de la Ley 51; inserta lo que falta; no toca `Decreed`/`Manual`; cada año se extiende un año más |
| `CompanyPoliciesSeeder` | 74 | `Key` | inserta la clave con su valor por defecto **sólo si no existe ninguna vigencia**; `Exonerada114_1` y `AllowSameUserApproval` las trae la migración de datos primero |
| `BankDisbursementFormatsSeeder` | 75 | `Code` | `CSV-GENERICO` (delimitado, activo: sirve para pruebas y como plantilla) y `AVVILLAS-1` **sin campos e inactivo** con `Notes` «estructura pendiente del dueño»; cuando el dueño la aporte, se carga por la pantalla de formatos o como versión nueva del seeder (la fila ya cargada a mano no se pisa) |
| `PayrollPermissionCatalogSeeder` (existente) | en `PhaseZeroSecuritySeeder` | recurso/acción | los recursos y patrones de rol de R12 |

Sin semilla: `PAY_PilaSettings` y `PAY_ElectronicPayrollSettings` (fila única que crea la
cooperativa desde su pantalla), `PAY_ElectronicPayrollNumberingRanges` (los define la cooperativa
por ambiente), `PAY_EmployeeBenefitOpeningBalances` (digitación auditada, FR-007). El layout PILA
`at2-v30-2026-07-24.json` y los XSD DIAN v1.0.6 son recursos embebidos, no datos de la base.

---

## 5. Dónde vive cada dato (Principio IV)

**En la base de la cooperativa** (`IngenIA365ERP_<slug>`), y por tanto en su respaldo, su
restauración y su retención por cooperativa, sin pasos aparte:

- Todo lo de §1 y §2: corridas especiales y sus líneas, saldos iniciales, movimientos de
  vacaciones, terminaciones y descuentos, cálculos del procedimiento 2, políticas, festivos,
  generaciones PILA con sus líneas, inconsistencias y archivo, la **habilitación DIAN** (NIT, modo,
  ambiente, `SoftwareId`, `TestSetId`, estado del set, rangos, y **sólo los nombres** de los
  Secrets), cada **documento** (XML sin firmar y firmado, ZIP, ApplicationResponse, PDF, CUNE,
  estado, intentos, errores crudos y traducidos), cada **transmisión**, formatos y archivos de
  dispersión.
- Los blobs, en `COR_Attachments` de esa misma base (cifrados con la clave por adjunto que
  envuelve el llavero de DataProtection de la cooperativa), con `OwnerEntityType`
  `ElectronicPayrollDocument` / `PilaGeneration` / `BankDisbursementFile` / `EmploymentTermination`.
- La auditoría, en la base MongoDB de esa cooperativa (`Payroll.ElectronicPayroll.Transmitted`
  lleva número, CUNE y resultado; nunca el XML).

**En el servicio `IngenIA365.NominaElectronica` NO existe**:

- Ninguna base de datos, ninguna tabla, ningún archivo de clientes: no hay `Habilitacion`,
  `Certificado`, `Documento` ni `Envio` (la R10 los proponía; la precisión IV los retira). Recibe
  `{ tenantId, xmlSinFirmar, softwareId, ambiente, modo, certificateSecretName, pinSecretName }`,
  completa CUNE y `SoftwareSC`, valida XSD, firma, transmite (o sólo firma, o sólo consulta estado)
  y **devuelve** XML firmado, ZIP, `DianResponse` y ApplicationResponse; lo que no devolvió en esa
  respuesta se perdió, y por eso el ERP guarda todo en la misma transacción que registra el intento.
- Ningún secreto en su código, imagen ni configuración: los `.p12`, sus contraseñas y el PIN de
  cada cooperativa viven en Secrets de Kubernetes (`nomina-electronica-{slug}-certificado`,
  `nomina-electronica-{slug}-pin`), creados por `tools/scripts/crear-secreto-nomina-electronica.ps1`
  (patrón de `crear-secreto-smtp.ps1`) y montados en el pod; en modo proveedor tecnológico, un
  Secret más de Ingenia365 (`nomina-electronica-ingenia365-certificado`). El servicio sólo abre el
  Secret cuyo nombre lleva el prefijo del tenant del token; un nombre de otro tenant se rechaza
  con 403 y queda en su log.
- Ningún índice de tenants ni «lista de cooperativas»: la identidad la aporta cada petición (token
  RS256 de la API con claim `tenant`, verificado contra la clave pública de la identidad central) y
  el servicio no sabe cuántas cooperativas existen.
- Ningún trabajo de fondo: no reintenta, no consulta estados por su cuenta, no barre nada. Quien
  reintenta y consulta es el ERP, cooperativa por cooperativa.
- Logs sin cuerpos: `tenantId`, `ServiceCorrelationId`, operación, `StatusCode`, duración y los
  códigos NIE; nunca el XML, el CUNE completo ni datos del trabajador. El diccionario NIE → texto
  llano es un recurso embebido del servicio, no dato de nadie.

La idempotencia es del ERP: `(Environment, Prefix, Consecutive)` es único en la cooperativa y el
servicio, sin estado, firmaría dos veces el mismo XML si se lo pidieran; el ERP no lo pide porque
un documento `InProcess` se **consulta** por CUNE antes de cualquier reenvío. Mover una cooperativa
de instancia o restaurarla en otra parte no toca nada del servicio, que es lo que exige el
Principio IV: el destino físico es un dato de `ADM_Tenants`, y aquí no hay ningún otro sitio donde
un dato de esa cooperativa pudiera haberse quedado.

---

## Dudas para el plan

- **D-01 · Vacaciones: quién paga los días del disfrute.** El modelo admite los dos modos con
  `VacacionesPagoAnticipado` (§1.7, §2.1): la liquidación paga y la ordinaria registra sólo la
  ausencia (`AUSENCIA_VACACIONES`, nuevo, informativo), o la ordinaria paga con `VACACIONES` y la
  liquidación sólo mueve la provisión. La spec (US4) admite ambas lecturas; la contadora decide el
  defecto (pendiente (e) de research) antes de sembrar. Relacionado: si paga los días calendario o
  sólo los hábiles.
- **D-02 · Prima y cesantías anuales por empresa o por plan.** El índice único es `(Year[,
  Semester], Version)`: una corrida con todos los empleados. Si una cooperativa con dos planes
  quiere liquidar la prima por plan, hay que sumar `PayrollPlanId` a la clave (y a la pantalla).
- **D-03 · Corridas `Vacation` de un solo empleado.** El modelo ata cada liquidación de vacaciones a
  un movimiento y a un empleado (`EmployeeId` en la corrida). Unas vacaciones colectivas exigirían
  una corrida de varios empleados; ¿alguna cooperativa las da?
- **D-04 · Fecha del comprobante de una liquidación especial.** Propuesto: `PayDate` (fecha de
  pago propia). La alternativa es `CutoffDate` (fin del semestre / del año), que para la prima de
  diciembre pagada el 15 cae **después** del pago. Decide la contadora.
- **D-05 · `TipoTrabajador` DIAN = tipo de cotizante PILA.** Se usa un solo par de columnas
  (`PilaContributorType/SubType`) porque las tablas coinciden en códigos; confirmar contra la tabla
  5.5.1 del anexo antes de que la DIAN rechace un `19`.
- **D-06 · Segundo apellido y otros nombres.** No hay migración que parta `LastName`; PILA y DIAN se
  bloquean por inconsistencia hasta que la cooperativa complete a las personas empleadas. ¿Se
  acepta que sea trabajo previo al 01-12-2026 o se quiere una propuesta automática (última palabra
  = segundo apellido) que la persona confirme?
- **D-07 · Fechas límite como parámetro `DateInYear` (MMDD).** Se eligió no cambiar el esquema de
  `PAY_LegalParameters`. Si se prefiere una columna `TextValue`, es una columna más en la migración 1.
- **D-08 · Tabla de indemnización con dos valores por tramo** (`FixedValue` = días 1.er año,
  `Rate` = días por año adicional). Funciona con la entidad actual pero el nombre `Rate` engaña
  en la pantalla de parámetros: ¿rotular por `Kind` de la tabla o añadir `SecondValue`?
- **D-09 · Umbral de exoneración por empresa.** La spec dice «exonerada sí/no **y** el umbral»;
  aquí el umbral es el parámetro legal `EXONERACION_PARAFISCALES_TOPE_SMMLV` (10) y la política
  sólo dice sí/no. Si una entidad tuviera un umbral distinto del legal (no se conoce ninguna),
  haría falta una clave `Exonerada114_1UmbralSmmlv`.
- **D-10 · `COR_Banks.TransferCode` como código ACH del banco destino.** Sin verificar en los datos
  del legado; si no lo trae, hay que sumar `AchCode` a `COR_Banks` y sembrar los códigos de la
  Superintendencia.
- **D-11 · Consulta automática de estado (`GetStatus`) desde el ERP.** El backoff exige un
  `IHostedService` que recorra el directorio y abra la conexión de cada cooperativa (Principio IV);
  hoy no existe ningún trabajo de fondo así en el ERP. ¿Se acepta, o la consulta es sólo manual
  («Actualizar estado») en la primera entrega?
- **D-12 · Tres migraciones o dos.** El plan dice dos; aquí van tres alineadas con las entregas
  N1 / N2+N3 / N4. Fundir 2 y 3 si se promueven juntas.
- **D-13 · Reversión del `Down` de `NominaPrestacionesYDian`.** Declarado reversible sólo sin
  corridas especiales. ¿Basta con la guarda o se quiere además el respaldo documentado como en las
  destructivas, dado que toca `PAY_PayrollRuns` en producción?
- **D-14 · `PAY_PilaGenerationLines` con 98 campos en `FieldsJson` + 30 columnas tipadas.** Se
  eligió el JSON para no acoplar el esquema al anexo (v30 hoy); las columnas tipadas cubren el
  cuadre y la consulta. Si el centro de reportes necesita filtrar por otro campo, se suma columna.
- **D-15 · Formato AV Villas.** La fila `AVVILLAS-1` nace vacía e inactiva; hasta que el dueño
  aporte la estructura, la US8 se prueba con `CSV-GENERICO` y SC-007 no se puede cerrar.
- **D-16 · Modo proveedor tecnológico.** La capacidad la declara el servicio (`GET
  /v1/version` → `modos`), no la base de la cooperativa; la cooperativa sólo elige `Mode` si el
  servicio lo ofrece. El contrato ERP↔servicio (`contracts/servicio-nomina-electronica.md` §3) ya lo incluye.
- **D-17 · Retención de blobs ≥ 5 años.** `COR_Attachments` no tiene fecha de retención ni
  política de purga; la nómina electrónica exige conservar 5 años (ET art. 632). ¿Se añade
  `RetainUntil` al adjunto en esta feature o se documenta como regla operativa?
