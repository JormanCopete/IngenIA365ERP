# Quickstart: Nómina completa — prestaciones, retiro, procedimiento 2, PILA, nómina electrónica y dispersión

**Feature**: 010 | **Date**: 2026-09-20 | **Rama**: `010-nomina-prestaciones-pila-dian`

Cómo comprobar la feature de punta a punta, historia por historia, con los valores que la
investigación calculó a mano (research R1, R5, R8) como patrón. Todo lo que aquí se afirma
sobre un ambiente se comprueba **contra la base del ambiente**, nunca contra el repositorio.

Dos decisiones de integración mandan sobre lo que diga `research.md` cuando difieran, y este
quickstart ya las asume:

- **El servicio de nómina electrónica es sin estado y sin base propia.** Todo lo de cada
  cooperativa —habilitación, documentos, XML sin firmar y firmado, ZIP, `ApplicationResponse`,
  CUNE, estados, intentos, errores traducidos y notas de ajuste— vive en la base de **esa**
  cooperativa (tablas `PAY_ElectronicPayroll*`) y lo escribe el ERP. El servicio recibe el XML
  sin firmar más la identidad de la cooperativa, completa CUNE y `SoftwareSC`, valida el XSD,
  firma y transmite, y devuelve lo firmado y la respuesta para que el ERP lo guarde. Los
  secretos (`.p12` y contraseña, PIN, credencial por cooperativa) son Secrets de Kubernetes que
  crea el dueño con un script del patrón `tools/scripts/crear-secreto-*.ps1`; el ERP y el
  repositorio no los conocen. Se arranca en modo **software propio**; el modo proveedor
  tecnológico se activa por configuración cuando exista la resolución (research R10).
- Las liquidaciones especiales van en las **tablas de corrida existentes** con `Kind`
  (research R2) y las calcula un **motor puro nuevo** que reutiliza las piezas del ordinario
  (research R1). Ningún valor legal en el código; todo con vigencia y semilla 2026 (research R4).

## 1. Compilar y pruebas

### 1.1 Sin contenedores

```bash
dotnet build IngenIA365ERP.CI.slnf -c Release
```

**Motores puros con casos dorados JSON** (Domain). Los casos viven en
`tests/IngenIA365ERP.Domain.Tests/Payroll/Settlements/Casos/*.json` (esquema hermano de
`CasoDorado`: tipo, fecha de corte, empleado con historial de salarios, bases prestacionales
de 6/12 meses, suspensiones, saldos iniciales, movimientos de vacaciones, provisiones,
motivo de retiro, deudas propuestas, `parametros` override, `esperado` por línea y totales) y
en `Payroll/Pila/Casos/*.json` (archivo esperado byte a byte con el layout v30):

```bash
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Payroll.Settlements|FullyQualifiedName~Payroll.Pila|FullyQualifiedName~Payroll.Calculation"
```

Deben quedar verdes, con los valores de §3 como mínimo: prima fija `1.124.547,50`; prima con
ingreso a mitad de semestre y cambio de salario `630.404,72`; cesantías estable `2.749.095`;
ingreso en septiembre `666.666,67` / `26.666,67`; cambio de salario en los últimos tres meses
`2.315.761,67` / `277.891,40`; vacaciones causadas a 18 meses `22,5`; disfrute 28-10 a 10-11
`11` hábiles L–S y `9` L–V; indemnización indefinida 810 días `4.400.000`, término fijo
`15.600.000`, ≥ 10 SMMLV `30.000.000`; definitiva integrada (retiro 15-12-2026); retención de
prima `52.000` y `0`; cesantías gravadas `0` y `1.124.000`; indemnización art. 401-3
`7.800.000`; P2 con las dos secuencias `3,71 %` / `3,44 %`; salario integral y aprendiz por
etapa (lectiva excluida, práctica incluida). Los 22 casos de la ordinaria (`Calculation/Casos`)
siguen verdes: el motor nuevo no toca `PayrollCalculationEngine`, y `DiasHabiles.Contar`
tiene su propia prueba con los festivos de la Ley 51 de 1983 para 2026–2028.

**Application** (`NominaTestData`, InMemory, reloj fijo, servicios reales):

```bash
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Payroll.Settlements|FullyQualifiedName~Payroll.Vacations|FullyQualifiedName~Payroll.Terminations|FullyQualifiedName~Payroll.WithholdingRates|FullyQualifiedName~Payroll.Pila|FullyQualifiedName~Payroll.ElectronicPayroll|FullyQualifiedName~Payroll.Dispersion|FullyQualifiedName~Payroll.OpeningBalances|FullyQualifiedName~Payroll.Policies"
```

Entre otras: cálculo y persistencia por tipo de liquidación; FR-005 (segunda liquidación del
mismo tipo, período y empleado → rechazo mientras la anterior no esté reversada); FR-021
(retiro con fecha en período aprobado → `Payroll.Termination.PeriodApproved` con la indicación); aprobación
que contabiliza en una sola transacción con `SourceType` por tipo; reversión que deja asiento
espejo **y reabre la ficha**; descuento de Cartera aplicado con `ProcessPaymentCommand` y
ajuste hacia abajo con motivo auditado (`Payroll.Settlement.DeductionAdjusted`); cierre de la
vigencia P2 anterior al aprobar (no se borra); cuadre PILA contra los `NM` del mes; el XML de
nómina electrónica valida contra los XSD v1.0.6 embebidos **antes** de salir del ERP; la
transmisión se niega con la habilitación incompleta nombrando lo que falta; nada se marca
`Aceptado` sin CUNE ni `ApplicationResponse`; `RepartoDePermisosTests` con los recursos nuevos
(`Operator` calcula y genera, no aprueba ni transmite).

**Arquitectura**:

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

Las sensibles a esta feature: `LaNominaNoTieneValoresLegalesFijos` (bajo `Domain/Payroll` y
`Application/Payroll` sólo se admiten `0, 1, 2, 0.5, 12, 15, 30, 100, 360`; cae con `180m`,
`6m`, `20m`, `10m`, `70m`, `0.12` incluso dentro de strings, GUIDs y comentarios),
`NingunModuloEscribeMovimientosFueraDelContrato` (el consecutivo interno de nómina electrónica
**no** se llama `NextNumber`), `PrincipioVIII_DualValidation` con los namespaces nuevos
listados desde el primer commit, `LasPantallasDicenQueEstanCargando` (toda `.razor` nueva bajo
`Pages/Nomina`), `TodoEnlaceDelMenuTieneSuPagina`, `PrincipioXII_MigracionesDestructivas`, y la
nueva **«sólo el servicio de nómina electrónica referencia criptografía de firma»**: ningún
proyecto del ERP referencia `SignedXml`/XAdES ni conoce PIN o certificado.

**Servicio de nómina electrónica** (proyecto nuevo, pruebas propias):

```bash
dotnet test tests/IngenIA365.NominaElectronica.Tests
```

CUNE y `SoftwareSC` contra un documento **aceptado en habilitación** (el ejemplo del anexo no
reproduce su propio hash: sin ese documento la prueba queda `Skip` con la razón escrita, ver §6);
firma verificada con `SignedXml.CheckSignature` y `SigPolicyHash` fijo
`dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=`; una petición cuya identidad no coincide con la
cooperativa pedida → 403 sin tocar nada; el servicio **no** tiene `DbContext`.

### 1.2 Con Docker Desktop encendido (e2e)

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~Payroll"
```

Colección «Nomina e2e» sobre `NominaE2E.PrepararAsync` (una cooperativa compartida, PostgreSQL,
Mongo y Redis reales; `DB_PROVIDER` elige el contenedor). Lo nuevo recorre por HTTP: prima →
relación de pago propia → archivo de dispersión → «marcar enviado» deja pagados a los del
archivo → documento de nómina electrónica **generado y validado contra XSD, sin transmitir**;
definitiva con préstamo de Cartera (saldo propuesto, bajado con motivo, aplicado, saldo que
queda) y la siguiente ordinaria sin el empleado; PILA del mes con archivo descargado y cuadre
contra `NM`; y **SC-003 tras cada operación**: provisión en el libro por cuenta = suma de
liquidaciones pendientes. `PayrollCyclePerformanceTests` sigue midiendo sólo con
`RUN_PERF_TESTS=1`. Recordá que Docker Desktop está apagado por defecto en la máquina de
desarrollo.

## 2. Datos de arranque en una cooperativa

Todo esto es **dato**, no despliegue. Lo que deja la semilla y lo que hay que digitar, en el
orden en que se necesita. Complementa `docs/operaciones/nomina-primer-periodo.md` (que sigue
valiendo para la ordinaria) y su §5 «Reaplicar semilla».

```bash
dotnet run --project tools/IngenIA365ERP.DbMigrator -- migrate --scope cooperativas
```

Con dos o más cooperativas declaradas con base propia, `--scope all` se niega (Principio IV).
Tras migrar, `PAY_PayrollRuns` tiene `Kind` (default `Ordinary` en las filas viejas —lo vigila
`DosContextosUnaTablaTests`—), `PayPeriodId` nullable y los índices filtrados por tipo; existen
`PAY_EmployeeBenefitOpeningBalances`, `PAY_VacationMovements`, `PAY_TerminationReasons`,
`PAY_EmploymentTerminations`, `PAY_SettlementDeductions`, `PAY_SeveranceFundDeposits`,
`PAY_Holidays`, `PAY_CompanyPolicies`, `PAY_WithholdingRateCalculations`/`…Months` (N1);
`PAY_PilaSettings`, `PAY_PilaGenerations`/`…GenerationLines`/`…Issues`,
`PAY_ElectronicPayrollSettings`/`…NumberingRanges`/`…Documents`/`…Transmissions` (N2+N3);
`PAY_BankDisbursementFormats`/`…FormatFields` y `PAY_BankDisbursementFiles`/`…FileLines` (N4).
Los nombres son los de `data-model.md` §3.

1. **Parámetros legales 2026** (`PAY_PayrollLegalParameters`): la semilla agrega los códigos
   nuevos de research R4 —`PRIMA_DIAS_ANIO`, `CESANTIAS_DIAS_ANIO`,
   `CESANTIAS_VENTANA_ESTABILIDAD_MESES`, `INT_CESANTIAS_PCT`, `VACACIONES_DIAS_ANIO`,
   `VACACIONES_COMPENSABLE_PCT`, `INDEMNIZACION_TABLA`, `INDEMNIZACION_UMBRAL_SMMLV`,
   `INDEMNIZACION_OBRA_MINIMO_DIAS`, `RETEFTE_P2_DIVISOR` (13), `RETEFTE_*_TOPE_ANUAL_UVT`,
   `CESANTIAS_EXENCION_TOPE_UVT` (350), `CESANTIAS_GRAVADA_TABLA_UVT`,
   `INDEMNIZACION_RETEFTE_PCT/_TOPE_UVT` (20 / 204), `FSP_UMBRAL_SMMLV`, la segunda `FSP_TABLA`
   (Ley 2381, desde 2027-04-01) con `ValidTo` en la de la Ley 797, `IBC_MINIMO_SMMLV`,
   `PILA_IBC_REDONDEO` (1), `PILA_APORTE_REDONDEO_MULTIPLO` (100), `PILA_PLAZO_PAGO_POR_NIT`,
   `DIAN_PLAZO_TRANSMISION_DIAS` (10), los de aprendiz por etapa— y precisa el `Source` de los
   que ya existían. En una cooperativa creada antes: Nómina › Conceptos › «Reaplicar semilla»
   (`POST /api/payroll/concept-definitions/seed`). Comprobar:

   ```bash
   H="Authorization: Bearer $TOKEN"; API=https://<host-del-ambiente>
   curl -s -H "$H" "$API/api/payroll/legal-parameters" | jq '{missingThisYear, missingNextYear}'
   ```

   Los códigos nuevos **no** están en `LegalParameterCodes.Required`: la ordinaria de las
   cooperativas ya desplegadas no se niega si el seeder aún no corrió; lo que se niega,
   nombrando el código, es la liquidación especial o la PILA que lo necesite. Las vigencias
   **2027** de SMMLV, auxilio y UVT no existen todavía (§6.9): sin ellas los intereses de enero
   y la consignación de febrero avisan `Payroll.LegalParameterMissing`.
2. **Políticas por empresa** (`/nomina/politicas`, `PAY_CompanyPolicies`, con vigencia): las
   **once** claves de `CompanyPolicyKeys` — `Exonerada114_1` (COOFLOPAL: **sí**, a confirmar
   con la contadora, §6.8a; migrada de `Payroll.ApplyEmployerExemption`), `SemanaLaboral`
   (`LunesASabado` por defecto), `VacacionesPagoAnticipado` (`true`, D-01),
   `CotizaArlEnVacaciones` (no), `RetefteTopesAnualesModo` (`Mensualizado`),
   `P2SecuenciaDepuracion` (`DepurarLuegoDividir`), `DianPlazoComputo` (`Calendario`),
   `DianMedioPagoMapa` (JSON `Transfer/Check/Cash` → código DIAN), `DeduccionAlRetiroModo`
   (`SaldoTotal`), `ArranqueNominaFecha` (la siembra el seeder con el primer período) y
   `AllowSameUserApproval` (migrada de `COR_SystemSettings`). Una vigencia nueva
   (`POST /api/payroll/company-policies/{key}/versions`) responde 201 `{ publicId, warnings[] }`:
   si hay corridas aprobadas desde `validFrom`, avisa; en `Exonerada114_1` bloquea
   (`Payroll.CompanyPolicy.RetroactiveNotAllowed`). Cada cambio queda en auditoría
   (`Payroll.CompanyPolicy.Changed`) y la explicación de cada valor nombra el modo elegido.
3. **Calendario de festivos** (`/nomina/festivos`, `PAY_Holidays`): la semilla trae 2026–2028
   por la regla de la Ley 51 de 1983 (orígenes `Ley51Fixed`, `Ley51MovedToMonday`,
   `Ley51Easter`); un puente decretado se agrega a mano con origen `Decreed` (o `Manual`, el
   valor por defecto; otro origen → `Payroll.Holiday.OriginInvalid`) y sólo esos dos se retiran
   (`Payroll.Holiday.Seeded`). Verificar que el 2 de noviembre de 2026 está y el 1 de noviembre
   (domingo) no hace falta.
4. **Motivos de retiro** (`PAY_TerminationReasons`, códigos de hasta 10 caracteres,
   `CodigoDeCatalogo`): la semilla deja `RENUNCIA`, `DESP_SINJC` (despido sin justa causa),
   `DESP_JC` (art. 62), `VENC_TERM` (vencimiento del término con preaviso, art. 46),
   `MUTUO_ACDO`, `FIN_OBRA`, `PER_PRUEBA` (art. 78), `MUERTE` y `PENSION`, con la marca
   `GeneratesSeverancePay` que sólo lleva `DESP_SINJC`. La cooperativa puede sumar motivos propios
   sin indemnización; las marcas legales no se editan (`Payroll.Termination.ReasonSeeded`).
5. **Saldos iniciales de prestaciones** (`/nomina/saldos-iniciales`, FR-007): por empleado con
   ingreso anterior al 01-12-2026: `AsOfDate` 30-11-2026, días hábiles de vacaciones pendientes,
   cesantías e intereses acumulados del año, prima acumulada del semestre, con la fuente. Sin
   esta fila, toda liquidación del empleado **advierte** que puede salir corta. Se edita
   mientras ninguna liquidación aprobada la haya consumido; después, ajuste con motivo y fila
   nueva. Auditoría `Payroll.OpeningBalance.Changed`.
6. **Cuentas contables de los conceptos nuevos** (`PAY_ConceptDefinitionAccounts`, quedó vacía
   tras `ContabilidadNiif`): `PRIMA` y `PRIMA_AJUSTE_PROV`, `CESANTIAS` (CxP al fondo),
   `INT_CESANTIAS`, `VACACIONES`, `INDEMNIZACION`, `SALARIO_PENDIENTE`, `RETEFTE_PRIMA`,
   `RETEFTE_CESANTIAS`, `RETEFTE_INDEMNIZACION`, `DESC_CARTERA` (`AffectsAccounting = false`:
   Cartera contabiliza el recaudo). Sin ellas la aprobación se bloquea con
   `Payroll.ConceptWithoutAccounts` y la lista. Las define la contadora (§6.8i). El período
   contable `CNT` del mes de la liquidación debe estar abierto.
7. **Ficha del empleado**, campos nuevos (`PUT /api/payroll/employees/{id}`, bloques `pila` y
   `dian`, `apprenticeStage`, `disbursementBankPublicId`): banco de dispersión (`COR_Banks`,
   código ACH en `TransferCode`; `clearDisbursementBank` lo quita) más la cuenta que ya existía
   (tipo 1 ahorros / 2 corriente, número), procedimiento de retención (1 / 2), tipo/subtipo de
   cotizante PILA (los mismos que `TipoTrabajador` DIAN, D-05), DIVIPOLA departamento y
   municipio laboral, actividad económica (Decreto 768/2022), centro de trabajo, tipo de salario
   F/V/X (derivado de `SalaryType`; integral siempre X), régimen de transición Ley 2381
   (`Unknown`/`Yes`/`No`), tipo de contrato DIAN 1–5, medio de pago y dirección laboral DIAN
   (opcionales), etapa del aprendiz (**obligatoria** si la clase es aprendiz o pasante:
   `Payroll.Employee.ApprenticeStageRequired`); en la persona, segundo apellido y otros nombres
   (`secondLastName`, `otherNames`); y en los catálogos EPS, AFP, ARL y CCF el `PilaCode`
   (distinto del `Code` de la cooperativa).
   Cada faltante es una inconsistencia **bloqueante** de la PILA o de la nómina electrónica con
   enlace a la ficha, no un error silencioso. Fondos, EPS, ARL y cajas deben estar vinculados a
   su **persona** (FR-088 de la 009) para ser terceros contables.
8. **Cuenta bancaria de la empresa y formato de dispersión** (`PAY_BankDisbursementFormats`): cuenta
   origen; el formato de **AV Villas Empresas** se carga como fila de datos con su vigencia
   cuando el dueño lo aporte (§6.1). Hasta entonces sólo hay un formato ficticio para probar la
   mecánica.
9. **Habilitación DIAN en modo software propio** (`/nomina/nomina-electronica` › Habilitación,
   `PAY_ElectronicPayrollSettings` en la base de la cooperativa): NIT y DV, razón social,
   modo `SoftwarePropio`, ambiente `Habilitacion`, `SoftwareID`, `TestSetId`, estado del set,
   rangos **internos** de numeración por tipo de documento y por ambiente (prefijo, desde, hasta,
   vigencia; los de habilitación no valen en producción), DANE del lugar de generación, y el
   **nombre del Secret** de Kubernetes que guarda el `.p12`, su contraseña y el PIN (nunca el
   contenido: el ERP no lo ve). La pantalla dice en qué ambiente está la cooperativa. El Secret
   lo crea el dueño con `tools/scripts/crear-secreto-nomina-electronica.ps1 -Ambiente qa
   -Cooperativa <slug>` (nombre propuesto; mismo patrón que `crear-secreto-smtp.ps1`: la clave se
   pide por teclado, viaja por SSH en STDIN y no se escribe en disco). Sin Secret montado, el
   servicio responde que falta el certificado y la pantalla lo muestra en lenguaje llano; sin
   `SoftwareID`, `TestSetId` o rango vigente, «Transmitir» se niega con lo que falta (FR-031).

## 3. Recorrido manual por historia

Cooperativa `coop_prueba` en QA (o local), con la contadora al lado y los valores de research
R1/R5/R8 como patrón. Fecha del sistema: la que dice cada paso (el reloj de pruebas se fija en
Application; en QA se usan períodos con esas fechas). Todo empleado nuevo se crea por
«Empleados › Nuevo» en un paso (persona + ficha, feature 008).

### 3.1 Prima de servicios de diciembre (US1)

Empleados: **A** con 2.000.000 fijo todo el semestre y auxilio; **B** con ingreso el
15-09-2026, 1.750.905 hasta el 31-10 y 2.000.000 desde el 01-11; **C** con salario integral;
**D** aprendiz en etapa lectiva; **E** con definitiva aprobada en octubre.

1. `/nomina/prima` › Nuevo › semestre 2026-2 › Calcular. Aparecen A y B; C, D y E están en la
   lista de excluidos con la razón (integral; lectiva Ley 2466/2025; prima ya pagada en la
   definitiva).
2. A: base 2.000.000 + 249.095 = 2.249.095; 180 días; **1.124.547,50**. La explicación muestra
   base, días, `PRIMA_DIAS_ANIO` con su vigencia y fuente, y la fórmula `Base × 180 / 360`.
3. B: 106 días; devengado del semestre 3.066.666,67 (46 días) + 4.498.190 (60 días) =
   7.564.856,67; ÷ 12 → **630.404,72**. La explicación va tramo por tramo con fechas.
4. Retención (FR-006a, P1): a A y B les da **0** (prima bajo 6.634.040). Si a un empleado con
   prima de 7.000.000 se le calcula: 25 % exento 1.750.000 → 5.250.000 = 100,24 UVT → tabla 383 →
   redondeo → **52.000**, independiente del salario del mes. Un empleado en P2 ve la prima
   depurada × su porcentaje vigente.
5. Recalcular tras corregir una novedad: versión 2, la 1 queda `Superseded`.
6. Aprobar (`Payroll.ServiceBonus.Approve`; el `Operator` no ve el botón): comprobante contable
   con `SourceType = ServiceBonusRun`, fecha = corte del semestre por defecto (D-04;
   `postingDate` sólo entre el corte y hoy, `Payroll.Settlement.PostingDateInvalid`), débito a la provisión de prima
   acumulada del empleado (líneas `Provision` de las corridas aprobadas + saldo inicial), la
   diferencia al gasto vía `PRIMA_AJUSTE_PROV` (negativa si la provisión supera lo liquidado) y
   CxP al empleado como tercero. El período ordinario de diciembre **no cambia**. Referencia de
   cuadre: caso dorado 20 (quincena de 3.000.000 → provisión de prima 135.325).
7. Relación de pago **propia** de la prima (fecha ≤ 20-12-2026), comprobante del empleado con
   la etiqueta «Prima de servicios», exportación Excel/PDF/Word con encabezado de la cooperativa.
8. Reversar con motivo antes de pagar: asiento espejo; se puede liquidar de nuevo. Con un pago
   marcado, la reversión se bloquea (`Payroll.PaymentBlocksReversal`).
9. Advertencia FR-007: un empleado con ingreso anterior al 01-12-2026 y sin saldo inicial
   muestra el aviso de que la prima puede salir corta.

### 3.2 Cesantías e intereses a 31-12-2026 (US2)

Empleados: **A** 2.500.000 estable + auxilio; **F** con 2.000.000 hasta el 31-10 y 2.400.000
desde el 01-11 (cambio dentro de los tres últimos meses); **G** con ingreso el 01-09-2026 y
2.000.000; **C** integral; **E** retirado con definitiva. A y F con fondos de cesantías
distintos.

1. `/nomina/cesantias-anuales` › 2026 › Calcular. C y E quedan fuera con la razón.
2. A: base último salario + auxilio 2.749.095, 360 días → cesantías **2.749.095**; la explicación
   dice «salario sin variación en los últimos 3 meses (`CESANTIAS_VENTANA_ESTABILIDAD_MESES`)».
3. F: cambió en los últimos tres meses → promedio del año 2.066.666,67 + 249.095 = 2.315.761,67 →
   cesantías **2.315.761,67**; intereses 2.315.761,67 × 360 × 12 % / 360 = **277.891,40**.
4. G: 120 días sobre 2.000.000 → cesantías **666.666,67**; intereses 666.666,67 × 120 × 12 % /
   360 = **26.666,67**.
5. Retención sobre intereses (art. 206 num. 4): promedio de los seis últimos meses en UVT → %
   no gravado por `CESANTIAS_GRAVADA_TABLA_UVT`. Con promedio 20.000.000 (381,87 UVT → 90 %
   exento) e intereses/cesantías pagadas de 22.400.000: gravado 2.240.000 = 42,8 UVT → **0**. Las
   cesantías **consignadas al fondo no se retienen**; la explicación lo dice.
6. Aprobar: comprobante `SeveranceRun` que cancela las provisiones de cesantías e intereses y
   deja CxP **al fondo** (tercero: la persona del fondo) por las cesantías y CxP al empleado por
   los intereses.
7. Relación de pago de **intereses** (fecha ≤ 31-01-2027) y comprobante del empleado. Relación de
   **consignación por fondo** (fondo, empleado, documento, valor, total por fondo) exportable;
   los totales cuadran con la liquidación; registrar la fecha de consignación de cada fondo
   (≤ 14-02-2027).
8. Antes de todo esto en un ambiente real: las vigencias 2027 (§6.9), porque la fecha de pago de
   los intereses cae en enero.

### 3.3 Liquidación definitiva (US3)

Empleado **H**: contrato indefinido, ingreso 01-10-2024 (810 días al retiro), salario
2.400.000, seis días hábiles de vacaciones tomados, un préstamo de la cooperativa con saldo
(capital 1.200.000 + intereses 35.000, 6 cuotas pendientes) y una libranza con un tercero.
Retiro **15-12-2026**, despido sin justa causa, dentro de la quincena abierta.

1. Ficha › «Terminar contrato»: fecha, motivo del catálogo, tipo de contrato. Con fecha dentro de
   un período **aprobado** → `Payroll.Termination.PeriodApproved` con la indicación (reversar el período o
   liquidar en el abierto). Con fecha válida se crea la corrida `Settlement` en borrador; la
   ficha **sigue vigente** hasta aprobar.
2. `/nomina/liquidacion-definitiva` (también desde la ficha), un solo documento con explicación
   por rubro: salario de los días pendientes del período (1 al 15) y auxilio proporcional;
   cesantías del año e intereses proporcionales (base art. 253); prima proporcional del
   semestre; vacaciones pendientes: causadas 810 × 15 / 360 = 33,75 − 6 = 27,75 hábiles × 80.000
   (el caso dorado de 540 días da **22,5** causados y 11,5 × 80.000 = **920.000**); indemnización
   art. 64: 30 días el primer año + 20 × 450/360 = 25 → 55 días × 80.000 = **4.400.000** (< 10
   SMMLV según `INDEMNIZACION_TABLA`). Con renuncia o justa causa la indemnización es 0 y la
   explicación lo dice; con término fijo, el tiempo faltante (caso dorado **15.600.000**); con
   ≥ 10 SMMLV, 20 + 15 (caso dorado **30.000.000**).
3. Retención: vacaciones y salario pendiente con el procedimiento del empleado; indemnización
   con art. 401-3 sólo si el ingreso mensual supera 204 UVT (H no supera: **0**; el caso dorado
   con ingreso 12.000.000 e indemnización 52.000.000 da 52.000.000 × 75 % × 20 % = **7.800.000**).
4. Deducciones propuestas (FR-018a): tabla con cada préstamo de Cartera —capital, intereses,
   cuotas, **saldo total propuesto** 1.235.000 hasta donde alcance el neto— y la libranza con
   sólo las cuotas causadas y no descontadas. Bajar el préstamo a 1.000.000 exige motivo
   (`Payroll.Settlements.AdjustDeduction`); subirlo se rechaza. Propuesto, aplicado, motivo, quién
   y cuándo quedan en la explicación y en la auditoría (`Payroll.Settlement.DeductionAdjusted`);
   se muestra el saldo que quedará en Cartera.
5. Aprobar: comprobante `SettlementRun` contra las provisiones (indemnización a gasto directo;
   `DESC_CARTERA` sin asiento: Cartera registra el recaudo por `ProcessPaymentCommand` con
   referencia al número de la liquidación); la ficha queda terminada (`Payroll.Employee.Terminated`);
   documento para firma en PDF (empresa/NIT, empleado, cargo, fechas, motivo, tipo de contrato,
   rubros con base y días, deducciones con propuesto/aplicado, neto, firmas); saldo del préstamo
   en Cartera bajó exactamente lo aplicado.
6. Calcular la quincena ordinaria del 1 al 15 (la del retiro): H **no aparece** —su último tramo,
   auxilio y novedades de esa quincena los pagó la definitiva (D-29); si ya estaba calculada con H,
   quedó `Stale` al aprobar—. La del 16 al 31 tampoco lo trae. La prima del semestre (§3.1)
   descuenta la que ya se le pagó aquí.
7. Reversar con motivo: asiento espejo, la ficha vuelve a estar vigente
   (`Payroll.Employee.Reinstated`), Cartera recibe la reversión del recaudo, y se puede liquidar
   de nuevo.
8. «Sanción moratoria (art. 65) si pagara hoy» es un cálculo informativo bajo demanda, nunca
   una línea de la liquidación.
9. Todo el recorrido, desde conocer la fecha hasta el PDF firmado, en menos de 15 minutos
   (SC-002).

### 3.4 Vacaciones (US4)

Empleado **I** con 18 meses de antigüedad (540 días), 2.400.000, sin vacaciones tomadas.

1. `/nomina/vacaciones` › saldo: causados **22,5** hábiles (540 × 15 / 360), 0 disfrutados, 0
   compensados, saldo 22,5; el detalle muestra el saldo inicial si lo hay.
2. Registrar disfrute del **28-10-2026 al 10-11-2026**: antes de guardar la pantalla dice **11
   hábiles** con `SemanaLaboral = LunesASabado` (14 calendario; saltados: 01-11 y 08-11
   domingos, 02-11 festivo) y **9** si la política es `LunesAViernes` (saltan además los sábados
   31-10 y 07-11). Cambiar la política en `/nomina/politicas` con vigencia y volver a consultar
   cambia el número; el saldo baja a 11,5.
3. Liquidar: valor día = 2.400.000 / 30 = 80.000 (sin auxilio ni extras: bandera «entra a
   vacaciones/indemnización» del concepto en no); los días que se pagan como vacaciones
   —calendario o sólo hábiles— los fija la contadora (§6.8e) y la explicación nombra la base,
   los días y por qué excluye el auxilio.
4. Aprobar: comprobante `VacationRun` contra `PROV_VACACIONES`; novedad `VACACIONES` con origen
   `VacationLeave` en **los dos períodos** que cubre (2.ª quincena de octubre y 1.ª de
   noviembre); la ordinaria de esos períodos paga esos días como vacaciones y no como salario, y
   los aportes se causan completos sobre el último IBC (ARL según `CotizaArlEnVacaciones`).
5. Compensación en dinero de 12 días con 22,5 causados → rechazo con el máximo permitido
   (**11,25**, `VACACIONES_COMPENSABLE_PCT` = 50).
6. Disfrute que cruza un período ya aprobado → la novedad de ese período se rechaza y se ofrece el
   ajuste retroactivo, como en la ordinaria.

### 3.5 PILA de diciembre con el validador de Aportes en Línea (US5)

Mes de diciembre de 2026 con los once empleados de COOFLOPAL (o los de `coop_prueba`): uno con
ingreso el 10, uno con incapacidad general de tres días, uno retirado el 20, uno con salario
integral, uno con vacaciones, y la empresa marcada exonerada.

1. `/nomina/pila` › diciembre 2026 › **Validar**: la lista de inconsistencias distingue
   **Bloqueante** (sin EPS/AFP/ARL/CCF o sin `PilaCode`, sin DIVIPOLA, sin actividad económica,
   documento de longitud inválida según la Res. 1529/2026, días que no suman 30 sin novedad,
   tarifa sin vigencia, layout sin versión vigente) de **Alerta** (IBC distinto entre
   subsistemas, FSP recalculable por el operador, cotizante del mes anterior sin RET); cada
   una enlaza a la ficha. Quitar la EPS a un empleado → bloqueante; generar se niega.
2. **Generar**: registro tipo 1 de 22 campos/359 posiciones y un tipo 2 de 98 campos/693 por
   línea (layout `at2-v30-2026-07-24.json`); una línea adicional por cada novedad con IBC
   distinto (IGE, VAC); ING y RET del mismo mes en la misma línea; el retirado con RET y 20 días;
   el que ingresó con ING y 21; el integral con IBC al 70 %; IBC al peso superior y aportes al
   múltiplo de 100 superior; exonerados con campo 54 = 0,04, SENA/ICBF en 0 y CCF completa; quien
   gana ≥ 10 SMMLV aporta todo.
3. **Cuadre** antes de descargar: Σ campos 47/55/63/65/67/69 contra los conceptos de aportes de
   los `NM` del mes por subsistema; la diferencia, si la hay, se muestra y hay que explicarla.
4. Descargar el `.txt` (ASCII, CRLF, mayúsculas sin tildes) y en Aportes en Línea, con la cuenta
   del aportante (§6.6): Liquidaciones › Adicionar liquidación › Cargar archivo › **Validar**.
   Sin **Error**; las **Alertas** se anotan y se comparan con las nuestras. Guardar la captura
   y el número de planilla como evidencia (SC-004).
5. Regenerar tras corregir una nómina: versión N+1 vigente, la anterior `Superseded` y
   consultable con su archivo. Marcar «Cargada» con número y fecha de radicación
   (`Payroll.Pila.Uploaded`).
6. Caso **abril 2027**: con la segunda `FSP_TABLA` vigente y un empleado en régimen de
   transición, el FSP sale distinto para quien no lo está; el caso dorado lo fija.

### 3.6 Nómina electrónica en habilitación con el set de pruebas (US6)

Requiere la habilitación completa de §2.9 con Secret montado en el servicio del ambiente y
`TestSetId` de COOFLOPAL (§6.3, §6.4). Mes de diciembre con dos empleados: uno con horas extras
y otro con una deducción de libranza; uno de ellos con prima pagada en el mes.

1. `/nomina/nomina-electronica` › diciembre 2026 › **Generar**: un `NominaIndividual` (TipoXML
   102) por empleado con pago en el mes, sumando las dos quincenas (`FechasPagos` 1–N) y la prima
   (`Devengados/Primas`); `Basico`, `Salud` y `FondoPension` presentes; `NumeroSecuenciaXML` =
   prefijo + consecutivo del rango interno del ambiente `Habilitacion`; `ProveedorXML` con NIT/DV
   de la **cooperativa** (software propio). El ERP valida contra el XSD v1.0.6 embebido y guarda el
   XML sin firmar en su base (`PAY_ElectronicPayrollDocuments`, estado `Generado`); nada salió
   todavía. Un empleado sin pago en el mes no genera documento; uno sin correo o dirección se
   genera igual y la representación gráfica lo dice.
2. **Transmitir** (acción explícita, `Payroll.ElectronicPayroll.Transmit`, sólo `CompanyAdmin`):
   el ERP envía el XML sin firmar y su identidad de cooperativa al servicio; el servicio completa
   CUNE (SHA-384 con el PIN del Secret y Ambiente 2) y `SoftwareSC`, valida, firma XAdES-EPES
   como `supplier` con el `.p12` de la cooperativa, arma el ZIP y llama `SendTestSetAsync` en
   `vpfe-hab`; devuelve XML firmado, ZIP, `ZipKey`/`DianResponse` y `ApplicationResponse`. El ERP
   los guarda y muestra estado, CUNE, ambiente y fecha. Comprobar en la base de la cooperativa
   que están el XML firmado y el `ApplicationResponse`; comprobar que el servicio **no guardó
   nada** (no tiene base).
3. **Rechazo**: forzar un NIExxx (p. ej. un DANE inválido): estado `Rechazado`, errores en
   lenguaje llano del diccionario del servicio, «Notificación» como advertencia; corregir y
   reintentar; el documento conserva su historial de intentos.
4. **En proceso**: si la DIAN no responde, el documento queda `EnProceso` y la consulta es
   **manual** (D-11): el botón «Consultar estado» por documento o por mes pide `GetStatus(CUNE)`
   por medio del servicio (`POST /documents/{id}/refresh-status`); no hay trabajo de fondo en
   esta feature. **Nunca** `Aceptado` sin CUNE ni `ApplicationResponse`; el reenvío del mismo ZIP
   sólo si la consulta no lo encuentra.
5. **Nota de ajuste**: reversar una quincena del mes ya transmitido y generar la nota:
   `NominaIndividualDeAjuste` (103) `Reemplazar` con los nuevos totales referenciando el CUNE
   original; si el empleado queda en cero en el mes, `Eliminar` con totales «0.00». Transmitir y
   ver el CUNE de la nota.
6. **Set de pruebas**: repetir hasta tener 4 documentos y 4 notas aceptadas; el estado del set
   se refleja en Habilitación. Pasado el set, el dueño cambia el ambiente a `Produccion` con
   rangos propios; los documentos de habilitación quedan en su historial.
7. Descargar el XML firmado y la representación gráfica (QuestPDF con QR a
   `catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=CUNE`); la gráfica dice que no
   es un desprendible de pago.
8. Aislamiento (Principio IV y decisión 1): con la identidad de otra cooperativa pedir el
   documento de ésta al servicio → 403; en el ERP, cambiar de cooperativa no muestra documentos
   ajenos porque viven en otra base.
9. Con la habilitación incompleta (quitar el `TestSetId`), «Transmitir» se niega nombrando lo que
   falta (FR-031). Auditoría: `Payroll.ElectronicPayroll.Generated/Transmitted/StatusChanged/
   EnablementChanged`.

### 3.7 Procedimiento 2 (US7)

Empleado **J** marcado en procedimiento 2 con doce meses liquidados en el sistema (o los
disponibles) y un mes de comisiones altas.

1. `/nomina/retencion-procedimiento-2` › semestre 2027-1 (cálculo en diciembre de 2026) ›
   Calcular: la explicación lista los doce meses con ingreso gravable (ordinarias **y** prima;
   cesantías e intereses excluidas), aportes obligatorios restados, depuración (25 % con tope,
   deducciones declaradas con tope 40 %/1.340 UVT), división por `RETEFTE_P2_DIVISOR` = 13 (o por
   los meses de vinculación si son menos, y lo dice), retención teórica con la tabla vigente (la
   del plan si tiene tramos) y porcentaje. Con el ejemplo de la investigación:
   `DepurarLuegoDividir` → **3,71 %**; `DividirLuegoDepurar` → **3,44 %**; la política
   `P2SecuenciaDepuracion` decide y la explicación nombra la secuencia.
2. Aprobar (`Payroll.WithholdingRate.Approve`): la vigencia anterior en
   `PAY_EmployeeWithholdingRates` **se cierra** al 31-12-2026 (no se borra) y la nueva abre el
   01-01-2027 hasta el 30-06-2027; auditoría `Payroll.EmployeeWithholding.Changed`.
3. Calcular la primera quincena de enero de 2027: la línea de retención de J usa el porcentaje
   nuevo y su explicación lo referencia; el motor ordinario no cambió.

### 3.8 Dispersión AV Villas (US8)

Relación de pago de la prima (§3.1) con tres empleados, uno sin cuenta bancaria en la ficha.

1. Desde la relación de pago › **Generar archivo de dispersión**: formato vigente de AV Villas
   (fila de `PAY_BankDisbursementFormats` cargada con la estructura que aportó el dueño, §6.1); una línea
   por empleado con cuenta (tipo de documento, documento, nombre, código del banco destino desde
   `COR_Banks.TransferCode`, tipo 1/2 y número de cuenta, neto, referencia); el tercero aparece en
   **pendientes** para otro medio. Totales del archivo = suma de los netos incluidos.
2. Descargar; cargar en el portal AV Villas Empresas (§6.1); guardar la captura de aceptación
   (SC-007).
3. **Marcar enviado** con la referencia del banco: los dos empleados quedan pagados con la misma
   fecha, medio `Transfer` y `Reference` = referencia del archivo (`MarkPaymentsCommand`), en una
   sola acción; la reversión de esa liquidación queda bloqueada como con la marca manual. El
   tercero se paga a mano como hoy.
4. Cambiar de formato con vigencia desde mañana: el archivo de hoy conserva el suyo; el de
   mañana usa el nuevo. Toda liquidación especial genera su propio archivo con fecha propia
   (FR-011a).

## 4. QA por rol

Con `coop_prueba` en QA y cuatro usuarios —`CompanyAdmin`, `Operator`, `Auditor`, `ReadOnly`—
más la contadora; los permisos se leen por `GET /api/admin/permissions/mine` y `PermissionGate`
esconde lo que no se puede, pero la puerta es el servidor (`LosEndpointsProtegidosExigenPermiso`):

| Acción | CompanyAdmin | Operator | Auditor | ReadOnly |
|---|---|---|---|---|
| Ver liquidaciones, vacaciones, PILA, nómina electrónica, dispersión, políticas | sí | sí | sí | sí |
| Calcular una liquidación especial; registrar disfrute/compensación; digitar saldos iniciales; registrar terminación | sí | sí | no | no |
| Aprobar / reversar una liquidación especial | sí | **no** | no | no |
| Bajar un descuento de Cartera con motivo (`Payroll.Settlements.AdjustDeduction`) | sí | **no** | no | no |
| Calcular porcentaje P2 | sí | sí | no | no |
| Aprobar porcentaje P2 | sí | **no** | no | no |
| Validar y generar PILA; generar documentos DIAN; generar archivo de dispersión | sí | sí | no | no |
| Marcar PILA cargada; **transmitir** a la DIAN; marcar dispersión enviada | sí | **no** | no | no |
| Administrar habilitación DIAN, formatos bancarios, festivos, políticas, motivos de retiro | sí | **no** | no | no |
| Exportar por el centro de reportes | sí | sí | sí (`Payroll.Runs.Export`, el existente) | no |

Además:

1. **Segregación**: con `AllowSameUserApproval = false`, quien calculó (`CalculatedBy`) no puede
   aprobar la misma liquidación; con `true` sí. Igual que la ordinaria.
2. **Operator** intenta aprobar por `curl` con su token → 404 `Generic.NotFound` (la puerta no
   revela el recurso); en pantalla no ve el botón. Sólo lectura recibe lo mismo en toda escritura.
3. **Auditoría** en `/admin/auditoria`: cada acción de §3 con su evento (`Payroll.Settlement.*`,
   `Payroll.Vacation.*`, `Payroll.Pila.*`, `Payroll.ElectronicPayroll.*`, `Payroll.Dispersion.*`,
   `Payroll.CompanyPolicy.Changed`, `Payroll.OpeningBalance.Changed`), con usuario, cooperativa,
   momento y antes/después; y `Module=Navigation` con cada pantalla nueva abierta.
4. **Contadora**: reconstruye a mano, desde la explicación, la prima de A y B, las cesantías de
   F, la indemnización de H y el porcentaje de J, y firma que coinciden **al peso** (SC-001,
   SC-006). Revisa cada comprobante contable en Contabilidad (sólo lectura, «Ver en Nómina»).
5. **Cambio de cooperativa** en la misma sesión: los botones se recalculan sin F5 y no se ve un
   solo documento, PILA o archivo de otra cooperativa (base distinta; decisión 1).
6. **Indicador de carga**: toda grilla, formulario y diálogo nuevo muestra «Cargando…» /
   «Guardando…» por zona; sin colores literales ni `<style>`.

## 5. Promoción a producción

Sólo con autorización expresa del dueño («sí, empujalo»), en este orden, con evidencia de cada
paso en las notas de release.

1. **Respaldo por cooperativa** (Principio XII): `pg_dump -Fc` de cada base de cooperativa y de
   `ingenia365erp_admin`, más el respaldo CNPG a S3 con nombre de la release, **antes** de migrar.
   La migración de la 010 toca tablas de producción con datos vivos (`PAY_PayrollRuns.PayPeriodId`
   pasa a nullable, `Kind` con default, el índice único `UK_PAY_PayrollRuns_Period_Version` se
   reemplaza por índices filtrados) y por eso exige **segundo revisor** documentado en la cabecera
   de la migración, aunque no borre filas.
2. **Migraciones**: las aplica el Job PreSync de Argo (`erp-db-migrate`): base operativa, admin y
   `migrate --scope cooperativas` base por base. `AutoMigrate` sigue apagado en producción.
   Después, en cada base: `Kind = 'Ordinary'` en todas las corridas previas y la cantidad de
   corridas no cambió; `check-migration-parity.ps1` en verde para PostgreSQL y SQL Server.
3. **Semillas**: el arranque de la API siembra los permisos nuevos (`PayrollPermissionCatalogSeeder`)
   y los motivos de retiro, festivos 2026–2028 y parámetros nuevos con `RunParametricSeed`; en
   cada cooperativa confirmar con `GET /api/payroll/legal-parameters` que `missingThisYear` está
   vacío y decidir con el dueño si las **vigencias 2027** entran por `Revisiones()` o por la
   pantalla (§6.9) antes del 31-01-2027. Los roles built-in reciben los patrones de R12; a los
   roles propios de la cooperativa hay que asignarles los permisos nuevos a mano.
4. **Servicio de nómina electrónica**: desplegarlo en el clúster desde GitOps como carga
   separada (misma pipeline de imágenes; sin Ingress público; NetworkPolicy que sólo admite la API
   del ERP del mismo ambiente). Crear los Secrets con el script del dueño, **uno por
   cooperativa** (`.p12`, contraseña, PIN) más la credencial ERP→servicio por cooperativa; el
   nombre del Secret se referencia desde la habilitación de esa cooperativa. Comprobar desde un
   pod de la API que el servicio responde a su health y que rechaza una identidad que no
   coincide con la habilitación. Ningún valor secreto pasa por el repositorio GitOps ni por el
   `appsettings.json`; el clasificador de despliegue ya bloquea pushes de configuración de
   producción sin autorización expresa.
5. **Datos de la cooperativa** antes de la primera liquidación (todo §2, en producción):
   políticas, saldos iniciales al 30-11-2026 digitados y revisados por la contadora, cuentas de
   los conceptos nuevos, `PilaCode` de las administradoras, campos PILA/DIAN de las once fichas,
   cuenta bancaria de la empresa, formato AV Villas, habilitación DIAN con ambiente
   **`Habilitacion`** hasta pasar el set.
6. **Verificación** contra el ambiente: `curl` con token de administrador a los endpoints de
   parámetros, políticas y habilitación (lee la base real); prima de diciembre calculada en
   borrador y cotejada por la contadora antes de aprobar; PILA de diciembre validada en Aportes en
   Línea con la cuenta real; set de pruebas DIAN completo en habilitación **antes** de pasar a
   `Produccion`.
7. **Primera transmisión en producción** (enero de 2027, dentro de los diez primeros días):
   con **segundo revisor** presente (Principio XII), un solo documento primero, CUNE y
   `ApplicationResponse` guardados en la base de la cooperativa, y luego el resto. Anotar los
   `StatusCode` distintos de 00/99 que aparezcan.
8. **Notas de release**: códigos de permiso nuevos, tablas nuevas, el cambio de índice en
   `PAY_PayrollRuns`, la instrucción de digitar saldos iniciales y campos PILA/DIAN antes del
   01-12-2026, y la referencia del respaldo y del revisor, en `docs/operaciones/` como las de la
   005, 006 y 009.

## 6. Lo que el dueño debe aportar antes de cada paso

Tomado de research «Lo que falta del dueño»; sin cada ítem, el paso indicado no se puede probar
ni promover.

| # | Qué | Para qué paso |
|---|---|---|
| 1 | **Estructura del archivo plano de pagos de Banco AV Villas Empresas** (columnas, separador o ancho fijo, longitudes, cuenta origen, tipo de identificación, códigos de banco destino, totales) y confirmar si `COR_Banks.TransferCode` trae el código ACH | §2.8, §3.8, §5.5 (SC-007) |
| 2 | **Modo de operación DIAN**: se arranca en **software propio** por cooperativa (decisión 1); si más adelante se quiere PT, tramitarlo (facturador electrónico habilitado, PT de factura, patrimonio ≥ 20.000 UVT, ISO 27001 o compromiso, visita DIAN, ~2 meses) | §3.6; el modo PT sólo después |
| 3 | **Cuenta de COOFLOPAL en `catalogo-vpfe-hab.dian.gov.co`**: registro como NO OFE u OFE, «Nómina Electrónica → Emisor», modo software propio, registro del software (fabricante Ingenia365) y entrega de **SoftwareID, PIN y TestSetId** | §2.9, §3.6 (SC-005) |
| 4 | **Certificado de firma digital de la cooperativa** (persona jurídica, ECD acreditada por ONAC: Certicámara, GSE, Andes SCD, Camerfirma), `.p12` con contraseña y vigencia, para cargarlo como Secret con el script; quién lo compra y renueva | §2.9, §5.4 |
| 5 | **Un documento aceptado en habilitación** (XML firmado + `ApplicationResponse`) como caso dorado de CUNE, `SoftwareSC` y firma; y registrar los `StatusCode` distintos de 00/99 observados | §1.1 (prueba del servicio deja de estar `Skip`), §3.6 |
| 6 | **Acceso al validador de Aportes en Línea** con la cuenta de COOFLOPAL (perfil Nómina); ideal una planilla ya pagada descargable como patrón de posiciones, formato de tarifas y códigos PILA | §3.5, §5.6 (SC-004) |
| 7 | **Datos del aportante para la PILA**: tipo (1), clase (B), forma de presentación (U/S), código PILA de la ARL, centros de trabajo, actividad económica (Decreto 768/2022), DIVIPOLA de la sede, marca de exonerada ante el operador; **por empleado**: códigos PILA de EPS/AFP/ARL/CCF, régimen de transición Ley 2381, tipo de salario F/V/X, subtipo (pensionados activos), aprendices y etapa, extranjeros, tarifa ARL efectiva | §2.7, §3.5 |
| 8 | **Confirmaciones de la contadora (Rafaela Lastra España)**: (a) exoneración art. 114-1 de COOFLOPAL; (b) topes 790/1.340 UVT acumulado vs mensualizado; (c) secuencia de depuración del P2; (d) ARL en vacaciones; (e) la nómina paga los días calendario del disfrute o sólo los hábiles; (f) plazo DIAN calendario vs hábiles; (g) prestaciones y no salariales en el documento DIAN del mes; (h) aprendices SENA y etapa tras la Ley 2466/2025; (i) cuentas contables de los conceptos nuevos; (j) validar los valores de la tabla de research R4 antes de sembrar; (k) convención, pacto o RIT con jornada L–V o prestaciones extralegales | §2.1, §2.2, §2.6, §3.4, §3.7, §4.4 |
| 9 | **Vigencias 2027** de SMMLV, auxilio de transporte y UVT (se decretan a fines de diciembre de 2026): por `Revisiones()` o por la pantalla, antes de los intereses de enero y la consignación de febrero | §3.2, §5.3 |
| 10 | **Infraestructura del servicio de nómina electrónica**: namespace propio o el de la API con NetworkPolicy, Secrets de Kubernetes creados por el script (decisión 1: no hay base del servicio), si se quiere HSM/Key Vault después, y el **segundo revisor** de la primera transmisión en producción | §5.4, §5.7 |
| 11 | **Textos oficiales limpios**: Res. 1529/2026 y 010/2026 de MinSalud, arts. 190–191 CST, numeración en el Decreto 780/2016 de la licencia no remunerada, norma del redondeo de la retención al múltiplo de mil, decreto transitorio 2026 del SMMLV | `Source` de los parámetros en §2.1 |
| 12 | **Saldos iniciales de prestaciones al 30-11-2026** de los once empleados, revisados por la contadora (o los datos de SOLIDO en `nom_antcesantia`/`nom_maeliqemp` para precargarlos con `database/migration/`, sin reemplazar la revisión) | §2.5, §5.5 |
