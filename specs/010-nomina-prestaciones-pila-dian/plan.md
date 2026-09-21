# Implementation Plan: Nómina completa — prestaciones, retiro, procedimiento 2, PILA, nómina electrónica y dispersión

**Branch**: `010-nomina-prestaciones-pila-dian` | **Date**: 2026-09-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/010-nomina-prestaciones-pila-dian/spec.md`

## Summary

Completar la nómina con los ocho procesos que la 005 dejó afuera, todos consumidores de la
nómina ordinaria que ya está en producción: cuatro **liquidaciones especiales** (prima de
servicios, cesantías e intereses, vacaciones, definitiva por retiro) calculadas por un **motor
puro nuevo** (`SettlementCalculationEngine`, Domain) que reutiliza las piezas del ordinario
—explicación, tramos de salario, parámetros con vigencia— y se persisten en las **mismas tablas
de corrida** con un `Kind`, para heredar pago, comprobante del empleado, reversión y reportes;
**procedimiento 2** como cálculo semestral explicado que escribe la vigencia en la ficha; **PILA**
como generador del archivo plano de la Res. 2388/2016 (planilla E, Aportes en Línea) con
validación, versiones e inconsistencias; **nómina electrónica** con el documento DIAN (anexo v1.0,
Res. 013/2021 compilada en la 227/2025) construido en el ERP y **firmado y transmitido por un
servicio central de Ingenia365 sin estado** —los datos de cada cooperativa viven en su base;
el servicio sólo custodia certificados y credenciales en Secrets y habla con la DIAN—, en modo
«software propio» primero y «proveedor tecnológico» cuando exista la habilitación; y
**dispersión bancaria** con un formato parametrizable cargado como dato (AV Villas). Ningún valor
legal en el código: 40 parámetros nuevos con vigencia y su semilla 2026 (research R4), políticas
por empresa (exoneración 114-1, semana laboral, calendario) con vigencia. Todo por el contrato
único de contabilidad, con permisos propios por proceso, auditoría y exportación al centro de
reportes. Detalle de cada decisión en [research.md](research.md) (R1–R14).

## Technical Context

**Language/Version**: C# / .NET 10.0.5 (API, Domain, Application, servicio nuevo); Blazor
(Shared, Web, WebAssembly) con Syncfusion 33.2.8 por paquetes de componente.

**Primary Dependencies**: Carter (minimal APIs), MediatR + FluentValidation, EF Core 10
(PostgreSQL y SQL Server por migraciones pares), QuestPDF (documento de liquidación para firma,
representación gráfica con QR de la nómina electrónica), ClosedXML/OpenXML (exportación),
`System.Security.Cryptography.Xml` (`SignedXml`) para la firma XAdES-EPES y WS-Security **sólo en
el servicio de nómina electrónica** (research R10: XAdES construido a mano sobre `SignedXml` o
FirmaXadesNetCore LGPL —decidir en la primera tarea del servicio, con la prueba de arquitectura
«sólo el servicio referencia criptografía de firma»—), `HttpClient` + SOAP 1.2 armado a mano
para `WcfDianCustomerServices` (el binding WS-Security X.509 no lo cubre WCF Core). Sin librerías
nuevas en Domain.

**Storage**: PostgreSQL/SQL Server por cooperativa (tablas `PAY_*` nuevas y columnas nuevas en
`PAY_PayrollRuns`, `PAY_Employees`, `COR_People`), MongoDB por cooperativa (auditoría), Redis
(sin uso nuevo), almacenamiento de objetos ya existente (`IngenIA365ERP.Storage`, `IBlobStore`)
para los blobs inmutables de nómina electrónica (XML firmado, ZIP, ApplicationResponse, PDF) y
los archivos PILA y de dispersión generados. **El servicio central no tiene base de datos**
(Principio IV): recibe, firma, transmite, devuelve.

**Testing**: xUnit + FluentAssertions + NSubstitute; casos dorados JSON en
`Domain.Tests/Payroll/Settlements/Casos/` y `Domain.Tests/Payroll/Pila/Casos/`; Application
sobre `NominaTestData` (InMemory); arquitectura (`LaNominaNoTieneValoresLegalesFijos`,
`NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioVIII_DualValidation` con los
namespaces nuevos, `LasPantallasDicenQueEstanCargando`, `TodoEnlaceDelMenuTieneSuPagina`);
e2e con Testcontainers (colección «Nomina e2e»); el servicio central con pruebas propias y un
**simulador de la DIAN** (respuestas grabadas del ambiente de habilitación) para no depender de la
red; validadores externos documentados en `quickstart.md` (Aportes en Línea, set de pruebas DIAN,
portal AV Villas).

**Target Platform**: Linux (k3s, contenedores) para API, Web y el servicio nuevo; navegador para
la interfaz.

**Project Type**: web-service + SPA existentes, más **un servicio interno nuevo**
(`src/Servicios/IngenIA365.NominaElectronica`) desplegado en el mismo clúster, sin exposición
pública, con `NetworkPolicy` que sólo admite tráfico desde la API.

**Performance Goals**: una cooperativa de decenas de empleados: cualquier liquidación especial,
la PILA de un mes y la generación de los documentos DIAN de un mes en menos de 10 s en pantalla;
transmisión a la DIAN por documento en menos de 30 s con reintento manual; las corridas
ordinarias no se degradan (índices filtrados nuevos en `PAY_PayrollRuns`).

**Constraints**: ningún literal legal en `Domain/Payroll` ni `Application/Payroll` (SC-008);
corridas inmutables con versión; toda escritura al libro por `AccountingPoster`; certificados,
PIN y credenciales sólo en Secrets del clúster (nunca en el ERP, la base de la cooperativa ni el
repositorio); nada se transmite a la DIAN sin acción explícita; documentos DIAN y sus respuestas
conservados ≥ 5 años; respaldo por cooperativa incluye lo nuevo sin pasos aparte.

**Scale/Scope**: 8 historias, ~40 parámetros legales nuevos, ~14 tablas nuevas y ~12 columnas
nuevas, 3 migraciones pares aditivas (una por entrega N1, N2+N3 y N4; ninguna destructiva), ~11
pantallas nuevas y una ficha ampliada, ~60 rutas nuevas (`/api/payroll/settlements|vacations|
terminations|benefit-balances|withholding-rates|pila|electronic-payroll|disbursements|policies`),
1 servicio nuevo con 4 rutas internas, ~8 vistas nuevas del centro de reportes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec → clarify (10 respuestas del dueño) → research (R1–R14) → este plan → tasks. Las ocho correcciones que trajo la investigación se aplican a la spec en la Fase 1. |
| II   | Clean Architecture | PASS | Motor de liquidaciones y reglas PILA/CUNE/porcentaje P2 en Domain (puros, sin IO); handlers en Application; firma y SOAP sólo en el servicio nuevo, que no referencia Domain ni Persistence (recibe XML). Una prueba de arquitectura lo fija. |
| III  | CQRS + MediatR | PASS | Comandos/consultas con `AbstractValidator` cada uno; los namespaces nuevos entran a `PrincipioVIII_DualValidation.InScopeNamespacePrefixes` en el primer commit (R12). Endpoints sólo reenvían. |
| IV   | Multi-tenancy | PASS | Toda tabla nueva vive en la base de la cooperativa. El servicio central **no guarda datos de clientes** (desvío deliberado respecto de R10, que proponía una base propia multi-tenant): habilitación, documentos, respuestas y CUNE los persiste el ERP en `PAY_ElectronicPayroll*` de cada cooperativa; el servicio custodia secretos por cooperativa en Secrets y exige que la identidad de la petición coincida con la cooperativa. Sin trabajos de fondo que barran cooperativas: la transmisión es una acción de la persona. |
| V    | Person centralizada | PASS | Los campos nuevos de identificación para la DIAN (segundo apellido/otros nombres separados, municipio DANE) van en `COR_People`; en `PAY_Employees` sólo lo laboral (tipo de trabajador PILA, tipo de contrato DIAN, alto riesgo, cuenta bancaria del pago). |
| VI   | PublicId externo | PASS | Todas las rutas y DTOs por `PublicId`; el número del documento DIAN y el CUNE son identificadores del negocio, no `Id`. |
| VII  | Soft-delete + auditoría | PASS | Entidades nuevas `AuditableEntity`; índices únicos filtrados por `IsDeleted`; movimientos de vacaciones y saldos iniciales se anulan con motivo, nunca se borran. |
| VIII | Validación dual | PASS | FluentValidation por comando; las pantallas validan antes (días, fechas, montos en pesos) y la API vuelve a validar. |
| IX   | Errores visibles | PASS | Respuestas de la DIAN traducidas y guardadas (códigos `NIExxx`); ningún `catch` vacío; el servicio central devuelve el error crudo y el ERP lo muestra. |
| X    | Trazabilidad SIPLA/SARLAFT | PASS | `PayrollAuditEmitter` (ya corregido para escribir en la base del PublicId) con eventos por proceso (R12); transmisión, generación de archivos y marca de enviado auditadas. |
| XI   | Inmutabilidad contable | PASS | Cada liquidación aprobada contabiliza por `AccountingPoster` en la misma transacción y sólo se reversa con espejo; PILA y documentos DIAN son versiones, nunca se editan. |
| XII  | Migraciones idempotentes y reversibles | PASS | Tres migraciones pares aditivas (`NominaPrestacionesYDian`, `NominaPilaYNominaElectronica`, `NominaDispersionBancaria`: columnas, tablas, índices filtrados que reemplazan `UK_PAY_PayrollRuns_Period_Version`); semillas de parámetros/conceptos/motivos/festivos por `Revisiones()` idempotentes; sin borrado de datos → sin marcador destructivo. La primera transmisión a la DIAN en producción exige **segundo revisor** nombrado por el dueño (research R14). |
| UI   | Indicador de carga; sin colores literales ni `<style>` | PASS | Pantallas bajo `Pages/Nomina` (módulo migrado); `IndicadorDeCarga` + `EstadoDeCarga` por zona; menú sólo a páginas con `@page`. |

## Project Structure

### Documentation (this feature)

```text
specs/010-nomina-prestaciones-pila-dian/
├── plan.md              # este archivo
├── spec.md              # con las correcciones de la investigación
├── research.md          # Fase 0: R1–R14, semilla 2026, lo que falta del dueño, riesgos
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/
│   ├── api.md                          # rutas /api/payroll/* nuevas, permisos, errores, reportes
│   ├── servicio-nomina-electronica.md  # contrato ERP → servicio central y mapeo al WS de la DIAN
│   └── archivos.md                     # layout PILA 2388, formato de dispersión parametrizable, consignación por fondo
├── checklists/requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Payroll/Settlements/            # motor puro nuevo: SettlementCalculationEngine, SettlementInput, reglas por tipo,
│                                   #   SettlementParameterCodes, calendario laboral (días hábiles, festivos)
├── Payroll/Pila/                   # reglas puras: IBC, novedades, tarifas/exoneraciones, redondeos, layout 2388 (registros 1 y 2)
├── Payroll/ElectronicPayroll/      # construcción del XML NominaIndividual/DeAjuste (sin firma), CUNE, SoftwareSC, reglas NIE
├── Payroll/Withholding/            # porcentaje fijo procedimiento 2 (12 meses ÷ 13, depuración, tabla)
├── Entities/Payroll/               # PayrollRun.Kind/CutoffDate…, EmployeeBenefitOpeningBalance, VacationMovement,
│                                   #   EmploymentTermination(+Reason), SettlementDeduction, WithholdingRateCalculation,
│                                   #   PilaGeneration(+Line,+Issue), ElectronicPayrollSettings/Document/Transmission,
│                                   #   BankDisbursementFormat/File, CompanyPolicy, Holiday
└── Enums/Payroll/                  # PayrollRunKind, SettlementStatus, VacationMovementKind, ElectronicPayrollStatus…

src/Core/IngenIA365ERP.Application/Payroll/
├── Settlements/{Prima,Cesantias,Vacaciones,Definitiva}/  # Calculate/Approve/Reverse + queries con explicación
├── OpeningBalances/  Vacations/  Terminations/  WithholdingRates/  Pila/  ElectronicPayroll/  Dispersion/  Policies/
├── Services/          # SettlementInputLoader (bases prestacionales de 6/12 meses desde corridas aprobadas, provisiones,
│                      #   deudas de Cartera), SettlementAccountingPoster (cancela provisión, diferencia al gasto, CxP),
│                      #   IElectronicPayrollSigner (puerto al servicio central), IBankFileFormatter
└── Reports/           # vistas nuevas de TablaExportable para /api/reports/payroll/{vista}

src/Infrastructure/IngenIA365ERP.Persistence/
├── Configurations/Payroll/*        # tablas y columnas nuevas, índices filtrados
├── Seeding/Parametric/*            # parámetros 2026 (Revisiones), conceptos nuevos, motivos de retiro, festivos 2026–2027,
│                                   #   políticas por defecto, formato de dispersión vacío «AV Villas»
└── Migrations.{PostgreSql,SqlServer}/  # NominaPrestacionesYDian (par)

src/Infrastructure/IngenIA365ERP.Identity/Seed/   # PayrollPermissionCatalogSeeder (recursos nuevos), BuiltInRolesSeeder

src/Presentation/IngenIA365ERP.API/
├── Endpoints/Payroll/{Settlements,Vacations,Terminations,BenefitBalances,WithholdingRates,Pila,ElectronicPayroll,Disbursements,Policies}Endpoints.cs
├── Reports/{SettlementDocumentReport,ElectronicPayrollGraphicReport}.cs   # QuestPDF
└── Services/ElectronicPayrollSignerClient.cs   # HttpClient al servicio central (JWT de servicio por cooperativa)

src/Servicios/IngenIA365.NominaElectronica/     # servicio central sin estado (.NET 10 minimal API)
├── Firma/          # XAdES-EPES sobre SignedXml, política v2 y su hash, certificados desde Secrets
├── Dian/           # SOAP 1.2 + WS-Security X.509, SendNominaSync/SendTestSetAsync/GetStatus/GetStatusZip, diccionario NIExxx
├── Endpoints/      # POST firmar-y-transmitir, POST set-de-pruebas, POST estado, GET salud
└── Dockerfile      # imagen propia; GitOps: Deployment + NetworkPolicy + Secrets montados

src/Presentation/IngenIA365ERP.Shared/
├── Pages/Nomina/{Prima,CesantiasAnuales,Vacaciones,LiquidacionDefinitiva,RetencionProcedimiento2,Pila,NominaElectronica,
│                 Dispersion,SaldosIniciales,Festivos,Politicas}.razor   # + ficha del empleado ampliada
├── Services/Nomina/NominaClient.{Prima,CesantiasAnuales,Vacaciones,Definitiva,Retencion2,Pila,NominaElectronica,Dispersion,
│                                  SaldosIniciales,Festivos,Politicas}.cs
└── Layout/NavMenu.razor, Services/Manual/ManualCatalogo.cs

tests/
├── IngenIA365ERP.Domain.Tests/Payroll/{Settlements,Pila,ElectronicPayroll,Withholding}/   # casos dorados JSON + pruebas
├── IngenIA365ERP.Application.Tests/Payroll/{Settlements,Vacations,Terminations,Pila,ElectronicPayroll,Dispersion}/
├── IngenIA365ERP.Architecture.Tests/Principles/   # SoloElServicioDeNominaElectronicaFirma.cs; listas ampliadas
├── IngenIA365.NominaElectronica.Tests/           # firma verificable, SOAP contra simulador DIAN, política
└── IngenIA365ERP.API.IntegrationTests/Payroll/    # e2e: prima → pago → dispersión → documento DIAN generado; definitiva; PILA

tools/scripts/crear-secreto-nomina-electronica.ps1   # certificado .p12 + contraseña + PIN por cooperativa → Secret (Read-Host -AsSecureString)
```

**Structure Decision**: se extienden los proyectos existentes (Domain, Application, Persistence,
Identity, API, Shared) siguiendo el molde de la feature 005, y se agrega **un** proyecto nuevo,
el servicio de firma y transmisión, porque es el único lugar donde pueden vivir certificados y
credenciales y porque debe poder atender a varias cooperativas (y, más adelante, actuar como
proveedor tecnológico) sin que el ERP toque criptografía. No se crean tablas propias de
«liquidación»: las corridas existentes ganan un `Kind` (research R2), lo que evita duplicar
pago, comprobante, reversión y reportes.

## Complexity Tracking

Sin violaciones. El único punto que requiere justificación es el **proyecto nuevo** (servicio
de nómina electrónica): la alternativa de firmar dentro de la API pondría el certificado y el
PIN de cada cooperativa en el proceso que atiende a todas y en la base de cada una (prohibido
por FR-031a y por el principio de mínimo privilegio); la alternativa de una base propia
multi-tenant en el servicio (research R10) violaría el Principio IV y se descartó: el servicio
queda sin estado.

## Decisiones

Se adoptan las decisiones R1–R14 de [research.md](research.md) con estas precisiones:

| Tema | Decisión del plan |
|---|---|
| Motor (R1) | `SettlementCalculationEngine` puro con `SettlementInput`; reutiliza `Explanation`, `SalaryTranches`, `CalendarConventions`, `LineFactory` y un helper de tabla por rangos extraído de `RangeTableRule`. Días 30/360. Casos dorados con los ejemplos numéricos de research. |
| Persistencia (R2) | `PayrollRun.Kind` (`Ordinary`, `ServiceBonus`, `Severance`, `Vacation`, `Settlement`), `PayPeriodId` nullable, `CutoffDate`, `Year`, `Semester`; índices únicos filtrados por tipo; la definitiva es una corrida de un empleado; `SourceType` contable por tipo. |
| Saldos iniciales (R3) | `PAY_EmployeeBenefitOpeningBalances`; el motor los explica como paso propio; advertencia cuando el ingreso es anterior al arranque y no hay saldo. |
| Parámetros (R4) | 40 códigos nuevos con `Source` exacto y vigencia 2026; listas `Required` **por proceso** (no en la de la nómina ordinaria); políticas por empresa en `PAY_CompanyPolicies` con vigencia, leídas por `PayrollPolicyReader`. Los valores 2027 (SMMLV, auxilio, UVT) entran por la pantalla de parámetros o por `Revisiones()` en diciembre. |
| Retención (R5) | Automática por norma en cada tipo (FR-006a); topes y tarifas por parámetro; explicación paso a paso. |
| Vacaciones (R6) | Calendario laboral (festivos con vigencia + semana laboral por empresa); movimientos derivan el saldo; la novedad de nómina se registra por período cubierto. |
| Definitiva (R7) | `EmploymentTermination` con catálogo de motivos (`GeneratesSeverancePay`); deudas de Cartera propuestas por saldo total (préstamos propios) y por cuotas causadas (libranzas), ajustables hacia abajo con motivo y auditadas; aprobar cierra la ficha y aplica los pagos en Cartera por su propio comando; reversar reabre. |
| Procedimiento 2 (R8) | Doce meses anteriores ÷ 13 (o meses de vinculación), depuración de la ordinaria, tabla vigente, vigencia semestral en `EmployeeWithholdingRate`; cálculo guardado con su explicación. |
| PILA (R9) | Layout 2388 v30 como dato versionado (registros 1 y 2), planilla E para Aportes en Línea, redondeos por parámetro (IBC al peso, aportes al múltiplo de 100), tabla FSP 2027 + bandera de transición, generaciones con versión, inconsistencias con enlace, cuadre con `NM`. |
| Nómina electrónica (R10 con desvío IV) | ERP: construcción del XML (anexo v1.0, XSD 1.0.6 embebidos), documentos mensuales por empleado acumulando quincenas y especiales, notas de ajuste, habilitación por cooperativa, estados, blobs inmutables; **servicio central sin estado**: CUNE/SoftwareSC, validación XSD, firma XAdES-EPES (política v2, hash fijo), SOAP WS-Security, diccionario de códigos; modo software propio → PT por configuración; secretos por Secret. Plazo «diez primeros días del mes siguiente (calendario)» como política por empresa hasta que la contadora confirme. |
| Dispersión (R11) | `BankDisbursementFormat` (definición de columnas como dato, con vigencia) + `BankDisbursementFile` (estados Generado/Enviado, marca de pagado en bloque); formato «AV Villas» se carga cuando el dueño aporte la estructura; un CSV genérico de referencia para pruebas. |
| Permisos y auditoría (R12) | Recursos y eventos nuevos como en research; namespaces en la prueba de validación dual desde el primer commit. |
| Pantallas y reportes (R13) | Once páginas bajo `Pages/Nomina`, ficha ampliada, vistas nuevas en `ReportesDeNomina`, dos PDF QuestPDF. |
| Pruebas (R14) | Casos dorados de liquidación, PILA y DIAN; e2e del ciclo; simulador de la DIAN en el servicio; validadores externos en quickstart con evidencia. |

## Entregas

Cuatro entregas en esta misma rama o en ramas hijas, por calendario legal:

| Entrega | Historias | Contenido | Fecha objetivo |
|---|---|---|---|
| **N1 prestaciones** | US1, US2, US3, US4 + saldos iniciales, políticas, calendario | motor, corridas con `Kind`, parámetros 2026, cuatro liquidaciones con aprobación/contabilización/pago/reversión, definitiva con Cartera y documento para firma, vacaciones con novedad, retención por norma, pantallas y reportes | antes del 1 de diciembre de 2026 (prima el 20-12) |
| **N2 PILA + procedimiento 2** | US5, US7 | generador 2388 con validación y versiones; cálculo P2 semestral | diciembre de 2026 (primera PILA en enero) |
| **N3 nómina electrónica** | US6 | XML, notas de ajuste, habilitación por cooperativa, servicio central, set de pruebas en habilitación, representación gráfica | set de pruebas en noviembre; producción en los diez primeros días de enero de 2027 |
| **N4 dispersión y cierre** | US8 | formato parametrizable, archivo AV Villas, marca de pagado en bloque; documentación, CLAUDE.md, quickstart, promoción | diciembre de 2026 |

## Riesgos y lo que falta del dueño

Resumen (detalle en research «Lo que falta del dueño» y «Riesgos»): estructura del archivo de
AV Villas; registro de COOFLOPAL en el catálogo de la DIAN (modo, SoftwareID, PIN, TestSetId) y
su certificado de firma de una ECD acreditada; viabilidad y plazo de Ingenia365 como proveedor
tecnológico (no viable a corto plazo: patrimonio ≥ 20.000 UVT, ISO 27001, visita); acceso al
validador de Aportes en Línea y datos del aportante; confirmaciones de la contadora
(exoneración 114-1, topes 790/1.340, ARL en vacaciones, plazo DIAN, prestaciones en el documento
DIAN, aprendices, cuentas contables de los conceptos nuevos); vigencias 2027; infraestructura y
segundo revisor de la primera transmisión. La reforma pensional (Ley 2381/2024, desde el
01-04-2027) se prevé con una segunda tabla FSP y una bandera de transición, sin más.


## Decisiones de integración tras la Fase 1

Los cuatro artefactos de la Fase 1 (`spec.md` corregida, `data-model.md`, `contracts/*`,
`quickstart.md`) dejaron dudas; se resuelven aquí y **mandan** sobre cualquier redacción distinta
en ellos. Fuente de verdad por tema: nombres de permisos, rutas y errores → `contracts/api.md`;
tablas y columnas → `data-model.md`; contrato ERP↔servicio → `contracts/servicio-nomina-electronica.md`;
layouts → `contracts/archivos.md`. `quickstart.md` se alinea a esos nombres en la Fase 2.

| # | Duda | Decisión |
|---|---|---|
| D-01 | Quién paga los días de vacaciones | La liquidación `Vacation` los paga **anticipadamente** (política `VacacionesPagoAnticipado = sí` por defecto) por los **días calendario** del disfrute sobre el salario ordinario; la nómina ordinaria de los períodos cubiertos recibe la novedad informativa `AUSENCIA_VACACIONES` que reduce los días de salario y no paga nada. La contadora puede cambiar la política a «paga la nómina ordinaria». |
| D-02 | Prima y cesantías anuales por empresa o por plan | Una corrida por empresa y período (`Year`, `Semester`); un plan de nómina no cambia la prestación. |
| D-03 | Vacaciones colectivas | Fuera de alcance: una corrida `Vacation` por empleado y movimiento. |
| D-04 | Fecha del comprobante contable de una liquidación especial | `CutoffDate` (30-06, 31-12, fecha de retiro, fin del disfrute) para causar contra la provisión en el mes correcto; el pago lleva `PayDate` propio. |
| D-05 | Tipo de trabajador DIAN vs cotizante PILA | La DIAN adoptó los códigos PILA de tipo de cotizante (tabla 5.5.1): **un solo par de columnas** en la ficha. |
| D-06 | Segundo apellido y otros nombres | Columnas nullable en `COR_People`; la ficha de persona las captura y **propone** la partición de `LastName`/`FirstName` por espacios para confirmar; sin migración de datos. |
| D-07 | Fechas límite legales | `LegalParameterKind.DateInYear` con `Value` = MMDD (p. ej. 1220) y helper de lectura; sirven sólo para avisos en pantalla. |
| D-08 | Tabla de indemnización con dos valores por tramo | `FixedValue` = días del primer año, `Rate` = días por año adicional; la pantalla de parámetros rotula según `Kind`. |
| D-09 | Umbral de exoneración | El parámetro legal que **ya existe**, `EXONERACION_PARAFISCALES_TOPE_SMMLV` (`LegalParameterCodes.PayrollExemptionThresholdSmmlv`, 10) + política por empresa `Exonerada114_1` sí/no con vigencia (migrada de `Payroll.ApplyEmployerExemption`). No se crea `EXONERACION_114_1_UMBRAL_SMMLV`. |
| D-10 | Código ACH del banco destino | Tarea de N4: verificar `COR_Banks.TransferCode`; si no es el código ACH, agregar `AchCode` y sembrarlo. |
| D-11 | Consulta del estado «en proceso» en la DIAN | **Manual** en esta feature (botón «Consultar estado» por documento y por mes); un trabajo de fondo por cooperativa queda para después, porque hoy no existe ninguno y exige recorrer el directorio abriendo cada conexión (Principio IV). |
| D-12 | Número de migraciones | **Tres** pares aditivas alineadas con las entregas: `NominaPrestacionesYDian` (N1), `NominaPilaYNominaElectronica` (N2+N3), `NominaDispersionBancaria` (N4). Corrige el «dos» del contexto técnico. |
| D-13 | ¿La primera migración es destructiva? | No: sólo agrega columnas e índices y reemplaza el índice único de corridas; ningún dato se pierde hacia adelante. `Down` con guarda. Respaldo previo como en toda promoción. |
| D-14 | Líneas PILA: JSON + columnas tipadas | Aceptado; se agregan columnas si un reporte las necesita. |
| D-15 | Formato AV Villas sin estructura | Nace inactivo; US8 se prueba con `CSV-GENERICO`; SC-007 queda pendiente del dueño. |
| D-16 | Modo PT declarado por el servicio | Sí: `GET /v1/version` del contrato devuelve `modos: { softwarePropio, proveedorTecnologico }` (no hay ruta `/capabilities`); la habilitación de la cooperativa sólo puede elegir `ProveedorTecnologico` si el servicio lo anuncia. |
| D-17 | Retención ≥ 5 años de los blobs DIAN | Regla operativa documentada (no existe purga de adjuntos); una columna `RetainUntil` queda para cuando exista purga. |
| — | Permisos | Los de `contracts/api.md` §1 y `PayrollPermissionCatalogSeeder`: `Payroll.ServiceBonus`, `Payroll.Severance`, `Payroll.Vacations`, `Payroll.Settlements` (definitiva, descuentos y, con `Manage`, el catálogo de motivos de retiro), `Payroll.BenefitBalances`, `Payroll.WithholdingRate`, `Payroll.Pila`, `Payroll.ElectronicPayroll`, `Payroll.Disbursement` (`Manage` = formatos por banco), `Payroll.CompanyPolicies`, `Payroll.Holidays`; la exportación sigue siendo `Payroll.Runs.Export`. Aprobar/reversar/transmitir/marcar enviado/ajustar descuentos sólo `CompanyAdmin`; `Operator` ve, registra, calcula y genera; `Auditor`/`ReadOnly` `*.View`. |
| — | `POST /api/payroll/employees/{id}/terminate` | Se **conserva** mientras la pantalla de la ficha lo use; en N1 la ficha pasa a «Terminar contrato» → crea la terminación y abre la definitiva (`/api/payroll/settlements/terminations`); el endpoint viejo se retira al final de N1, sin alias, con la pantalla ya migrada. |
| — | Rutas de corrida existentes | `approve`, `reverse` y `discard` de `/api/payroll/runs/{runId}` rechazan `Kind != Ordinary` con 422 `Payroll.Settlement.UseSettlementRoute`; pagos, comprobantes y relación de pago sirven a cualquier `Kind`. |
| — | Fechas de pago en el documento DIAN | Se leen de las marcas de pago (`PayrollPayment.PaidAt`); un empleado sin marca en el mes se omite salvo `acknowledgeUnpaid` explícito, y la pantalla lo lista. |
| — | Aprendiz en etapa práctica y parafiscales | Parámetro por tipo de cotizante (`APORTES_APRENDIZ_PRACTICA_PARAFISCALES` sí/no); por defecto **no** (caja, SENA e ICBF no se causan por aprendices) hasta que la contadora confirme. |
| — | Numeración DIAN | Consecutivo interno del empleador con prefijo, por tipo de documento y ambiente (`LastIssuedNumber`, nunca `NextNumber`, por la prueba de arquitectura del contrato contable); sin rango autorizado por la DIAN. |
| D-18 | Claves de política | Son **once**, no siete: a las de R4 se suman `VacacionesPagoAnticipado` (D-01), `DianMedioPagoMapa` (derivación del medio de pago DIAN), `DeduccionAlRetiroModo` (propuesta de descuentos de la definitiva) y `ArranqueNominaFecha` (la siembra el seeder con el primer período). Catálogo cerrado en `CompanyPolicyKeys`; `contracts/api.md` §10.1 y `data-model.md` §2.1 las listan. |
| D-19 | Vigencia nueva de política | `POST /company-policies/{key}/versions` responde 201 `{ publicId, warnings[] }`. Una vigencia anterior a corridas aprobadas **avisa** en `warnings` y sigue; sólo `Exonerada114_1` bloquea (`Payroll.CompanyPolicy.RetroactiveNotAllowed`) porque cambia aportes ya contabilizados. Toda vigencia nueva marca los borradores como desactualizados. |
| D-20 | Origen de los festivos | `HolidayOrigin` tiene cinco valores: `Ley51Fixed`, `Ley51MovedToMonday`, `Ley51Easter` (los pone la semilla) y `Decreed`, `Manual` (los registra la cooperativa; `Manual` por defecto; otro origen → `Payroll.Holiday.OriginInvalid`). Sólo esos dos se retiran. |
| D-21 | Fecha del comprobante de toda liquidación especial | Confirma D-04 para las cuatro: `postingDate` por defecto = `CutoffDate` y sólo entre el corte y hoy (`SettlementAccountingPoster.ResolverFecha`, `Payroll.Settlement.PostingDateInvalid`). El «hoy por defecto para la prima» que decía `contracts/api.md` §3.1 se retira. |
| D-22 | Pasante sin contrato de aprendizaje | `reasonCode` `Pasante` en `SettlementReasonCodes` (FR-009): sin prestaciones ni indemnización art. 64; queda en `excluded` de prima, cesantías y definitiva. |
| D-23 | Códigos de motivos de retiro | Van por `CodigoDeCatalogo` (10 caracteres): `RENUNCIA`, `DESP_SINJC`, `DESP_JC`, `VENC_TERM`, `MUTUO_ACDO`, `FIN_OBRA`, `PER_PRUEBA`, `MUERTE`, `PENSION`. |
| D-24 | `RunSummaryDto` | Los campos nuevos son `kind` (texto), `cutoffDate`, `payDate`, `year`, `semester`, `employeePublicId` y `warnings: [{ code, message, data }]`; el período sigue siendo `periodPublicId` (nullable), no `payPeriodPublicId`. |
| D-25 | Ficha PILA/DIAN | `salaryTypeCode` F/V/X se deriva de `SalaryType` (0/1/2; `IntegralSalary` siempre X) y no es columna nueva; `ApprenticeStage` es obligatoria si la clase es `Apprentice` o `Intern` (`Payroll.Employee.ApprenticeStageRequired`); el `PUT` quita el banco de dispersión con `clearDisbursementBank`; el bloque `dian` lleva además `paymentMethodCode` y `workAddress` opcionales. Otros nombres de la persona es `OtherNames` (no `MiddleName`). |
| D-26 | Parámetros faltantes por proceso | `GET /legal-parameters/missing?process=` acepta `LegalParameterProcess`: `Ordinary`, `Settlements`, `Pila`, `WithholdingRates`, `ElectronicPayroll` (los dos primeros y el último no estaban en el contrato). |
| D-27 | Tablas de N2–N4 | Los nombres que valen son los de `data-model.md` §3: `PAY_PilaGenerationLines`, `PAY_ElectronicPayrollSettings`/`…NumberingRanges`/`…Transmissions`, `PAY_BankDisbursementFormats`/`…Files`, `PAY_SettlementDeductions`; `quickstart.md` y `contracts/api.md` §8 quedaron alineados en T038. |
| D-28 | Ajuste de provisión en un disfrute o compensación parcial | La provisión de vacaciones cubre **todos** los días hábiles pendientes, no sólo los del movimiento que se paga: una corrida `Vacation` compara lo liquidado con la parte de la provisión que corresponde a sus días —`Accrued × días hábiles del movimiento / días hábiles pendientes antes de él`— y deja el resto provisionado; si consume todos los pendientes (o más, anticipadas) cancela toda la provisión. La definitiva, la prima y las cesantías siguen cancelando la provisión completa. Hasta la revisión de N1 cada disfrute parcial liberaba toda `PROV_VACACIONES` y el siguiente salía «corto» al gasto (contradecía SC-003). `ProvisionAdjustmentRule.ProvisionDeLosDiasLiquidados`; caso dorado 19. |
| D-29 | Días que paga el disfrute | Precisa D-01: la corrida `Vacation` paga los días del descanso contados por el **calendario comercial** de la nómina (`CalendarConventions.Days`: el 31 no existe y el último día del mes vale 30), que son exactamente los que la ordinaria descuenta con la novedad `AUSENCIA_VACACIONES`, de modo que salario más vacaciones suman siempre el mes completo. Los días calendario reales siguen en el movimiento (`CalendarDays`) para la explicación y la nómina electrónica. Con los días reales, un disfrute del 14 al 28 de febrero pagaba 15 y la ordinaria descontaba 17 (el empleado perdía dos días) y uno del 17 al 31 de octubre cobraba 31 días del mes. `VacationRule.DiasComerciales`; caso dorado 20. |

**Re-evaluación de la constitución tras el diseño**: sin cambios en las compuertas. El servicio
central sigue sin estado (data-model.md §«Principio IV» lo enumera); las tres migraciones son
aditivas; los namespaces nuevos entran en `PrincipioVIII_DualValidation` en el primer commit de N1.
