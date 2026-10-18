# Tasks: Cartera financiera — captaciones: ahorro a la vista, contractual y permanente, CDAT y aportes sociales

**Input**: Design documents from `/specs/014-ahorros-aportes-cdt/`
**Prerequisites**: plan.md, spec.md, research.md (§0 nombres canónicos y §2–§3 conciliación: **mandan**),
data-model.md, contracts/ (api, contrato-de-cuentas, mensajes, contabilidad, plantillas-y-archivos,
documentos), quickstart.md, investigacion-financiera.md

**Tests**: obligatorios en esta feature. La spec exige casos dorados al centavo (FR-026, SC-002, SC-019),
pruebas de arquitectura que fijan fronteras (FR-008, FR-014) y e2e en los dos motores (SC-001 a SC-019), y
la constitución exige `Domain.Tests`, `Application.Tests`, `Architecture.Tests` e `API.IntegrationTests`.

**Organization**: por historia de usuario, en orden de construcción (las entregas K7–K11 agrupan las
historias, ver «Dependencies»). Antes de empezar: **la 013 completa** (Q3) y verificar que los nombres de la
013 que cita `research.md` siguen iguales; si cambiaron, enmendar `research.md` primero.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ir en paralelo (archivos distintos, sin dependencia de tareas pendientes)
- **[Story]**: historia de la spec (US1…US17)
- Rutas abreviadas: `D/` = `src/Core/IngenIA365ERP.Domain/`, `A/` = `src/Core/IngenIA365ERP.Application/`,
  `P/` = `src/Infrastructure/IngenIA365ERP.Persistence/`, `API/` = `src/Presentation/IngenIA365ERP.API/`,
  `SH/` = `src/Presentation/IngenIA365ERP.Shared/`, `TD/` = `tests/IngenIA365ERP.Domain.Tests/`,
  `TA/` = `tests/IngenIA365ERP.Application.Tests/`, `TR/` = `tests/IngenIA365ERP.Architecture.Tests/`,
  `TI/` = `tests/IngenIA365ERP.API.IntegrationTests/`, `TS/` = `tests/IngenIA365ERP.Shared.Tests/`

---

## Phase 1: Setup

**Purpose**: verificar el punto de partida y preparar carpetas, colecciones y semillas vacías.

- [x] T001 Verificar que la 013 está completa (tasks de `specs/013-cartera-financiera/tasks.md` todas `[X]`) y que siguen vigentes los nombres que cita `specs/014-ahorros-aportes-cdt/research.md` (`CreditLine`, `CreditLineVersion`, `LendingSetup`, `LoanTransaction`, `RegistroDeCartera`, `CicloDiarioDeCartera`, `CierreMensualDeCartera`, `LendingPostingRule.ClaveDe`, `RolesDeCuentaDeCartera`, `ClaseDeRecaudador`, `LendingDecisionSubjects`, `BankFileScope` 5 y 6 de K2); confirmar que **K2 de la 013** está construido (`PayrollAgreement`, `BankFileScope` 5 y 6; research C22: si no, las tareas de E-05 pasan a la 013); anotar cualquier diferencia como enmienda al final de `specs/014-ahorros-aportes-cdt/research.md` — hecho el 2026-10-18: diferencias anotadas como enmienda **§5 (E01–E15)** al final de `research.md` (`LendingNumberKind` 16–20, `ClaseDeRecaudador` en Application, `AccountingFactPartSource` 6, `EntregaDeCartera` K7–K11, plantillas en `PerfilesSugeridos`, casos dorados, retiro); ajustadas T013, T014, T018 y T032
- [x] T002 Crear las carpetas vacías de la estructura del plan: `D/Lending/Deposits/`, `D/Entities/Lending/Deposits/{Configuration,Accounts,Transactions,Projections,Restrictions,Processes,Migration}/`, `A/Lending/Deposits/{Lines,Accounts,Operations,Ledger,Accrual,TermDeposits,Contributions,Holds,Charges,Withdrawals,Assembly,Liquidity,Documents,Reports,Migration,Activation}/`, `P/Configurations/Lending/Deposits/`, `API/Endpoints/Lending/Deposits/`, `SH/Pages/Cartera/Captaciones/`, `TD/Lending/Deposits/Casos/`, `TA/Lending/Deposits/`, `TI/Lending/Deposits/` — hecho (carpetas con `.gitkeep` hasta su primer archivo)
- [x] T003 [P] Crear la colección e2e «Captaciones e2e» con su fixture de cooperativa aislada (créditos activados con carga vacía, recaudador y tesorería de prueba registrados con `ProveedorDeCuentas`, parámetros sembrados; desde la activación sin carga de la fase fundacional, T055, activa aportes y captaciones sin carga) en `TI/Lending/Deposits/CaptacionesE2E.cs`, sobre `CentralIdentityApiFixture` — hecho: `TI/Lending/Deposits/CaptacionesE2E.cs` (colección «Captaciones e2e», cooperativa aislada `cap_*` con créditos activados con carga vacía y los dobles `TST` y `TES` de la 013); `CooperativaConCaptacionesAsync` suma la activación de aportes y captaciones cuando exista T055. **e2e en la corrida final** (no se ejecutan hasta el final de todas las fases)
- [x] T004 [P] Crear el lector de casos dorados de captaciones y su `calcular.py` (recalcula los JSON de forma independiente) en `TD/Lending/Deposits/CasosDoradosDeCaptaciones.cs` y `TD/Lending/Deposits/Casos/calcular.py`, con el formato de `TD/Lending/Casos/` — hecho: `CasoDoradoDeCaptaciones` + `CasosDoradosDeCaptacionesTests` y `Casos/calcular.py` (días, tasa diaria compuesta, saldos a (D, R), interés por saldo diario con tramos, liquidación); los JSON llegan con sus motores

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: retiro de lo heredado, línea con naturaleza, libro de cuentas, puertos, contabilidad base,
ciclo diario y activación. **Ninguna historia empieza antes.**

### Retiro de lo heredado (R28, C19, FR-119, FR-120)

- [x] T005 Ejecutar `specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql` en local, DEV, QA y producción y guardar el resultado en `docs/operaciones/captaciones-retiro-heredado.md` (**dueño**: con filas en cualquier tabla, la guarda se niega y el dueño decide antes de seguir) — **parcial (2026-10-18)**: corrido sólo en local (PostgreSQL 5433, `ingenia365erp`: 0 filas en las 45; `ingenia365erp_admin` no las tiene); DEV, QA y producción quedan **del dueño** con la tabla de §9 de `docs/operaciones/captaciones-retiro-heredado.md`
- [x] T006 Escribir la migración par destructiva con guarda `RetiroDeCaptacionesHeredadas` (38 tablas conservadas y 7 `CDT_*`; se niega si alguna tiene filas sin la fila `COR_SystemSettings` `LND.CaptacionesHeredadas.RetiroAprobado`; cabecera `MIGRACION-DESTRUCTIVA-APROBADA` con respaldo y segundo revisor) en `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/` y `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/` — hecho el 2026-10-18 (`20261018143710` PostgreSQL, `20261018143627` SQL Server; guarda `RAISE EXCEPTION` / `THROW 50014`; 45 `DropTable` y ningún otro cambio en el scaffold); su prueba `TI/Lending/Deposits/RetiroDeCaptacionesHeredadasTests.cs` es **e2e en la corrida final**; respaldo y segundo revisor, del dueño
- [x] T007 Retirar el código conservado: `D/Entities/Savings/`, `D/Entities/CDT/`, `A/Savings/`, `A/CDT/`, `P/Configurations/Savings/`, `P/Configurations/CDT/`, `API/Endpoints/Savings/`, `API/Endpoints/CDT/`, `API/Endpoints/Reports/SavingsReportsEndpoints.cs`, `SH/Pages/AhorrosYAportes/`, sus `DbSet` en `P/DbContext/ApplicationDbContext.cs` e `A/Common/Interfaces/IApplicationDbContext.cs`, y la regla de auditoría `Savings` de `ModuloDeAuditoria` — hecho; también `Pages/CDT` (2 pantallas) y su grupo de menú «CDT», `API/Reports/PersonPortfolioReport.cs`, las ramas de ahorro y CDT de `AccountReferenceFinder`; la tarjeta débito responde `DebitCard.SavingsAccountUnavailable` y el detalle de la persona cuenta 0 ahorros hasta T043 (pregunta al dueño)
- [x] T008 Retirar el grupo de menú «Ahorros y aportes» y sus 16 enlaces de `SH/Layout/NavMenu.razor` (el menú nuevo de captaciones llega con cada historia) y las referencias de clientes tipados heredados en `SH/Services/` — hecho; no había clientes tipados heredados en `SH/Services` (las pantallas llamaban `Http` directo); también salieron las dos tarjetas del centro de reportes y los temas del manual
- [x] T009 Enmendar `TR/Principles/ElEspacioLendingEsSoloCartera.cs` y `TR/Helpers/CarteraHeredada.cs`: ya no hay conservado; `LaCarteraHeredadaNoVuelve` cubre las 45 tablas retiradas y prohíbe `Entities.Savings`, `Entities.CDT`, `/api/savings` y `/api/cdt` en `TR/Principles/LaCarteraHeredadaNoVuelve.cs`, y quitar la exención por nombre de `/api/savings/*` en `TR/Principles/LosEndpointsProtegidosExigenPermiso.cs` — hecho; `ElEspacioLendingEsSoloCartera` se absorbió en `LaCarteraHeredadaNoVuelve` (100 nombres heredados, snapshot desde el retiro, carpetas, espacios y rutas); también `NingunaTablaConservadaApuntaALaCarteraRetirada`, `PrincipioXII_MigracionesDestructivas` (la 014 tiene su única destructiva), `LaCarteraSoloSeTocaPorSusInterfaces`, `LasPantallasDicenQueEstanCargando`, `TextosDeCarteraTests` y `RetiroDeLaCarteraHeredadaTests` (e2e de la 013, **en la corrida final**)
- [x] T010 Compilar `IngenIA365ERP.slnx` y dejar verdes Domain, Application, Architecture y Shared tras el retiro (corregir referencias rotas sin reintroducir lo retirado) — hecho el 2026-10-18: build 0 errores; Domain 2.298, Application 5.699, Architecture 634, Shared 467, todas verdes

### Línea con naturaleza (R01, FR-027)

- [x] T011 Agregar `Nature` (`LineNature`, por defecto `Placement`) a `D/Entities/Lending/Configuration/CreditLine.cs` y condicionar `Activar`, `BloquearPorUsura` y `DesbloquearPorUsura` a `Nature == Placement`; error `Lending.CreditLine.NatureMismatch` en `D/Entities/Lending/Configuration/CreditLine.cs` — hecho: `Nature` inmutable, `Nueva(..., nature)`, `EsDeColocacion`, `ValidarNaturaleza`; `AdmiteCreditosNuevos` exige colocación y el bloqueo y desbloqueo por usura ignoran las demás; `AltaDeVersionDeLinea` (registro y validación), `LineaEnAlcanceAsync` (rutas de líneas de crédito), la lista de líneas de crédito, la réplica de usura y las consultas de líneas activas del crédito (activación, cupos, ventas de Comercial, reservas, tablero, informe de usura, migración de créditos) filtran colocación
- [x] T012 Mapear `Nature` y el índice único `UK_LND_CreditLines_ActiveContribution` (una sola línea de aportes activa por cooperativa) en `P/Configurations/Lending/Configuration/CreditLineConfiguration.cs` — hecho, con la migración par `CaptacionesAportes` ya generada (`20261018145620` / `20261018145551`: columna `Nature` con defecto 1 y los dos índices), **regenerable** hasta el congelamiento (T056)
- [x] T013 Comprobar que los casos dorados de crédito (45 JSON de `TD/Lending/Casos` y los de las demás carpetas, research E08) siguen verdes sin cambios (`dotnet test tests/IngenIA365ERP.Domain.Tests --filter Lending`) y que ningún handler de crédito acepta una línea de otra naturaleza, con prueba en `TA/Lending/Lines/LaLineaDeCreditoExigeColocacionTests.cs` — hecho: `Domain.Tests --filter Lending` 1.033 verdes sin tocar ningún JSON; `TA/Lending/Lines/LaLineaDeCreditoExigeColocacionTests.cs` (7 pruebas)

### Enums, valores nacionales, parámetros y permisos

- [x] T014 [P] Crear `D/Enums/Lending/DepositEnums.cs` con los enums de `research.md` §0.3 y los de `data-model.md` §10 (valores fijos) y ampliar al final los existentes según `data-model.md` §1.9 (`IssuedDocumentKind` 7–15, `LendingNumberKind` **16–20** (research E03), `AccountingFactPartSource.AccountCatchUp = 6` (E05), `BankFieldSource` 93–95, `EntregaDeCartera` K7–K11 (E06); `ClaseDeRecaudador.Treasury = 6` va en `A/Common/Integration/Lending/EnumsDelContratoDeCartera.cs`, E04) — hecho: `D/Enums/Lending/DepositEnums.cs` (21 enums de §0.3 más `AccountFlag` de C20 y los 30 de §10) y los valores al final de `IssuedDocumentKind`, `LendingNumberKind`, `AccountingFactPartSource`, `BankFieldSource`, `EntregaDeCartera` y `ClaseDeRecaudador`; `PrefijosDeDocumentosDeCartera` sabe desde qué entrega se emite cada clase nueva y sus prefijos de contracts/documentos.md
- [x] T015 [P] Ampliar `TD/Lending/EnumeracionesDeCarteraTests.cs` para fijar los valores de los enums nuevos y ampliados — hecho (y `DocumentosDeK5Tests` sólo exige disponibles hoy las seis clases de la 013)
- [x] T016 [P] Agregar los valores nacionales `COMPONENTE_INFLACIONARIO`, `LIMITE_INEMBARGABLE`, `TOPE_ENTREGA_SIN_SUCESION` y `TOPE_SEGURO_DEPOSITOS` en `D/Common/NationalValues/CodigosDeValorNacional.cs` con su replicación a cada cooperativa y semilla «pendiente de validar» con fuente — hecho: cuatro códigos con su entrega (K8, K8, K10, K11) y la clave «año gravable» del componente inflacionario; la réplica es genérica (sin cambios); semilla del componente inflacionario 2025 (0,5543, Decreto 898 de 2026) en espera hasta K8; los otros tres sin semilla (los registra el maestro, P5, P6)
- [x] T017 [P] Sembrar los parámetros de la Cartera con vigencia y fuente de `data-model.md` §9.1 (convención `Grupo.Nombre`, C02: retención por clase, umbral diario en UVT, alcance del umbral, componente inflacionario, GMF tarifa/tope/política/hechos generadores, fondo de liquidez, tope de cuota de fondos de empleados, fracción mínima de aportes, límites de participación, mayoría de edad, orden de embargo, fecha valor, canje por clase de medio, umbral de efectivo, mínimo irreducible de la entidad, soportes de entrega, traslado de cuentas abandonadas) más los de research C26 (`Captaciones.AnticipacionAvisoCambioDeTasaDias`, `Captaciones.DiasPlazoDevolucionAportes`, `Captaciones.DiasParaNoReclamados`, `Riesgo.DescuentoAportesDeterioro`, `CuentasAbandonadas.TrasladoHabilitado`) en el catálogo `D/Lending/Parameters/ParametrosDeCartera.cs` — hecho: 28 claves en `ParametrosDeCartera.Definiciones` con su entrega y «pendiente de validar»; claves definitivas en research **E15**; los pisos y techos de aportes son cuatro `LIMITE` nuevos de `CatalogoDeLimitesLegales` (dos sembrados en 0,10, Ley 2496 de 2025)
- [ ] T018 [P] Sembrar los 39 permisos de `contracts/api.md` §2 en `src/Infrastructure/IngenIA365ERP.Identity/Seed/LendingPermissionCatalogSeeder.cs` y las cuatro plantillas de rol (`cartera.jefe-captaciones`, `cartera.asesor-captaciones`, `cartera.analista-aportes`, `cartera.tesorero-liquidez`) en `A/Security/Roles/Plantillas/PerfilesSugeridos.cs` (research E07: con ellas son 17 y la activación las exige todas)
- [ ] T019 [P] Agregar los sujetos de atribución de `research.md` C06 a `D/Lending/Attributions/LendingDecisionSubjects.cs`
- [ ] T020 Ampliar `TR/Principles/LaCarteraNoTieneValoresLegalesFijos.cs` y `TR/Principles/ElMotorDeCarteraEsDecimal.cs` para cubrir `D/Lending/Deposits/` y `A/Lending/Deposits/`

### Libro de cuentas (R02–R04, R08, FR-001, FR-002, FR-011)

- [ ] T021 [P] Crear `MemberAccount` (estados y transiciones de `data-model.md` §3.1) en `D/Entities/Lending/Deposits/Accounts/MemberAccount.cs`
- [ ] T022 [P] Crear `MemberAccountParty` (§3.2) en `D/Entities/Lending/Deposits/Accounts/MemberAccountParty.cs`
- [ ] T023 [P] Crear `MemberAccountOperation` (§4.1, hecho inmutable) en `D/Entities/Lending/Deposits/Transactions/MemberAccountOperation.cs`
- [ ] T024 [P] Crear `MemberAccountTransaction` (§4.2, bigint, tres fechas, reverso espejo, signo de §0.2) en `D/Entities/Lending/Deposits/Transactions/MemberAccountTransaction.cs`
- [ ] T025 [P] Crear `MemberAccountDailyPosition` (§4.3) y `MemberAccountControl` (§4.4) en `D/Entities/Lending/Deposits/Projections/`
- [ ] T026 [P] Crear `AccountPayoutOrder` (§3.9) en `D/Entities/Lending/Deposits/Accounts/AccountPayoutOrder.cs`
- [ ] T027 Crear las configuraciones EF de T021–T026 con precisión (`PrecisionDeCartera`, alias `MontoCausado` decimal(18,6)), índices y filtros en `P/Configurations/Lending/Deposits/`
- [ ] T028 Agregar las columnas nuevas de las 11 tablas existentes de `data-model.md` §1.8 y §8.2 (`COR_RegulatoryProfileVersions` para FR-018, `LND_MigrationLoads.Nature`, `LND_Activations.Nature`, `LND_IssuedDocumentEvents.CausingAccountOperationId`, `LND_AccountingFactParts` con disparador y conteo de cuentas, `LND_AttributionRules.MaxNegotiatedSpread`, `LND_ProcessRunErrors.MemberAccountId` y las de `LND_PayrollAgreementVersions`) con sus configuraciones en `P/Configurations/Lending/` y `P/Configurations/Core/`
- [ ] T029 Ampliar `PrincipioXI_ContableImmutable` a `D/Entities/Lending/Deposits/Transactions/` en `TR/Principles/PrincipioXI_ContableImmutable.cs`
- [ ] T030 Crear el escritor único `RegistroDeCuentas` (operación, transacciones por concepto, posición diaria, marca de agua) en `A/Lending/Deposits/Ledger/RegistroDeCuentas.cs`
- [ ] T031 Agregar el nivel «cuenta» al orden fijo de `CerrojoDeCartera` (setup → persona → crédito → cuenta, por `Id`, en los dos motores) en `P/Lending/CerrojoDeCartera.cs`, `P/Lending/SqlDelCerrojoDeCartera.cs` y la interfaz `A/Lending/Common/ICerrojoDeCartera.cs`
- [ ] T032 Ampliar `NumeradorDeCartera` con las clases de número de cuentas, operaciones, retiros, distribuciones y casos (`data-model.md` §1.3 con los valores 16–20 de research E03) en `A/Lending/Common/NumeradorDeCartera.cs`
- [ ] T033 Crear el motor puro `ReconstruccionDeCuenta` (saldos por concepto a D con información hasta R) y la fachada `MotorDeCaptaciones` en `D/Lending/Deposits/ReconstruccionDeCuenta.cs` y `D/Lending/Deposits/MotorDeCaptaciones.cs`
- [ ] T034 [P] Pruebas de `ReconstruccionDeCuenta` (consignación, retiro, reverso, fecha valor, información hasta R) en `TD/Lending/Deposits/ReconstruccionDeCuentaTests.cs`
- [ ] T035 [P] Prueba de arquitectura `NadieEscribeElLibroDeCuentasFueraDelRegistro` (todas las tablas de `data-model.md` §0.7) en `TR/Principles/NadieEscribeElLibroDeCuentasFueraDelRegistro.cs`
- [ ] T036 [P] Prueba de arquitectura `LaRetencionYElGmfSoloLosCalculanSusMotores` y `LasCaptacionesNoUsanLaUsura` en `TR/Principles/LasCaptacionesRespetanSusMotores.cs`
- [ ] T037 [P] Pruebas de arquitectura de R29 que faltan: `LasCuentasSeTocanSoloPorElContrato` (Nómina, Recaudo y Tesorería sólo por `IContratoDeCuentas`), ampliación de `LosHechosDeCarteraNoLlevanCuentas` a los hechos de captaciones (SC-009) y de `SoloElNumeradorNumera` a las clases nuevas, en `TR/Principles/`
- [ ] T038 [P] Prueba de arquitectura: toda ruta de `API/Endpoints/Lending/Deposits/**` declara `RequirePermission` y toda consulta y comando de `A/Lending/Deposits/` aplica `IAlcanceDeCartera` (amplía `LosEndpointsProtegidosExigenPermiso`) en `TR/Principles/LasCaptacionesExigenPermisoYAlcance.cs`

### Puertos y frontera (R09, R20, R29, FR-008, FR-086, FR-093)

- [ ] T039 Crear el puerto `IContratoDeCuentas` con sus 7 métodos, DTOs y enums de `contracts/contrato-de-cuentas.md` en `A/Common/Integration/Lending/IContratoDeCuentas.cs`, `ContratoDeCuentasDtos.cs` y `EnumsDelContratoDeCuentas.cs`
- [ ] T040 Implementar `ContratoDeCuentasSinActivar` («no habilitado» sin efectos por naturaleza) y registrarlo por defecto en `A/Lending/Integration/ContratoDeCuentasSinActivar.cs` y `A/DependencyInjection.cs`
- [ ] T041 Mover `IConsultaDeAportes` de `A/Common/Integration/Savings/` a `A/Common/Integration/Lending/` con la versión 2 del DTO (`Minimum`, `InstallmentsInArrears`) manteniendo la 1 para quien no la adopte
- [ ] T042 Registrar `ProveedorDeCuentas` en `ContratosDeIntegracion` y sus versiones en `A/Common/Integration/ContratosDeIntegracion.cs`, y la clase `Treasury` en la validación de recaudadores de `A/Lending/Integration/Contrato/ValidadoresDelContrato.cs`
- [ ] T043 Enmendar `TR/Principles/LaCarteraSoloSeTocaPorSusInterfaces.cs`: seis puertos (suma `IContratoDeCuentas`; `IConsultaDeAportes` y `IConsultasDeCartera.ProductosDeCaptacionAsync` en la lista), sin excepciones por nombre; y agregar `ProductosDeCaptacionAsync` a `IConsultasDeCartera` con su implementación real y la «no habilitada» (`contracts/contrato-de-cuentas.md` §5, FR-095) en `A/Common/Integration/Lending/IConsultasDeCartera.cs` y `A/Lending/Integration/`

### Contabilidad base (R19, C03, C10, FR-096 a FR-100, E-03)

- [ ] T044 Agregar las dimensiones 14 a 18 al final de `DimensionDeCartera` y de la clave posicional de `LendingPostingRule.ClaveDe`, con migración de datos que recalcula la clave de las filas existentes, en `D/Entities/Accounting/Lending/LendingPostingRule.cs` y el enum `DimensionDeCartera` de `A/Accounting/Lending/Reglas/RolesDeCuentaDeCartera.cs`
- [ ] T045 Agregar los 17 roles en español de `research.md` C10 y las familias `Captacion` y `Patrimonio` al final de `A/Accounting/Lending/Reglas/RolesDeCuentaDeCartera.cs`
- [ ] T046 [P] Sembrar los tipos de comprobante `MA`, `CC`, `TD`, `AO`, `RA` y dejar inactivos `AH` y `CD` (C17) en la semilla de tipos de comprobante y en `ACC_LendingVoucherMappings`, y el documento cruce `CTA` «Cuenta de captación o aportes» en `cross-document-types.json`
- [ ] T047 [P] Crear los bloques comunes de mensajes (`DepositFactDimensionsV1`, `MemberAccountRefV1`, `TermDepositTermsV1`, `WithholdingDetailV1`, `GmfDetailV1`, `DepositConceptAmountV1`, `DepositFactLineV1`, `DepositFactTotalV1`) de `contracts/mensajes.md` §3 en `A/Common/Integration/Contracts/Lending/Deposits/BloquesComunes.cs`
- [ ] T048 Sembrar los 33 tipos de hecho de `contracts/mensajes.md` §1.1 en `LND_FactTypes` (códigos cortos, `CAV` para `CaptacionesActivadas`, research C23) y la tabla hecho × rol de `contracts/contabilidad.md` en `A/Accounting/Lending/Reglas/OperacionesDeCartera.cs`, para que cada hecho se pueda contabilizar desde la historia que lo emite
- [ ] T049 Ampliar `ConsumoDeCartera` y `ResolutorDeReglasDeCartera` para aceptar hechos de captaciones con las dimensiones nuevas (sin hechos concretos todavía) y el error `Accounting.LendingRule.NatureMismatch` y `AccountFlag` según research C20 (`Standard`, `GmfExempt`, `Garnished`; la inactividad cambia el rol) en `A/Accounting/Lending/`
- [ ] T050 [P] Pruebas del resolutor con las dimensiones 14–18 y los pesos de especificidad intactos en `TA/Accounting/Lending/ResolutorConDimensionesDeCaptacionTests.cs`

### Proceso diario, cierre y activación (R10, R21, FR-031, FR-116)

- [ ] T051 Agregar `PasoDeCaptaciones` (vacío, por tandas, sólo en cooperativas con aportes o captaciones activados, errores a `LND_ProcessRunErrors`) a `CicloDiarioDeCartera` en `A/Lending/Deposits/Accrual/PasoDeCaptaciones.cs` y `A/Lending/Accrual/CicloDiarioDeCartera.cs`
- [ ] T052 Agregar la sección de captaciones (vacía) a `CierreMensualDeCartera` y a la guarda `LendingNotClosed` en `A/Lending/Risk/CierreMensualDeCartera.cs`
- [ ] T053 Agregar las fechas de activación de aportes y de captaciones a `LendingSetup` (`data-model.md` §1.2) en `D/Entities/Lending/Configuration/LendingSetup.cs` y su configuración
- [ ] T054 Crear los requisitos de activación por naturaleza (`LoansActivated`, `AccountsCollectorRegistered`, `PayrollConceptsReady`, `DeliveriesComplete`, `ProfileAllowsClasses`, `LinesWithMatrix`) y el bloqueo por entrega `Lending.Savings.DeliveryNotAvailable` en `A/Lending/Deposits/Activation/RequisitosDeActivacionDeCaptaciones.cs`
- [ ] T055 `ActivateContributionsCommand` y `ActivateDepositsCommand` en su modo **sin carga** (FR-117, cooperativa sin historia: corte + 1, requisitos, mes abierto) que cambian el contrato y la consulta a sus implementaciones reales; con su ruta `POST /api/lending/deposits/activation` de `contracts/api.md` §16 en `API/Endpoints/Lending/Deposits/DepositsMigrationEndpoints.cs`; la fixture e2e (T003) los usa desde la fase 3 y antes siembra una matriz mínima para los hechos de la naturaleza (`LinesWithMatrix`) y registra la tesorería de prueba con `OrdenDeGiroEmitida` v2 adoptada (`TreasuryDestinationRegistered`; la v2 se declara aquí como contrato y la emite US10), y la activación con carga se completa en US9, en `A/Lending/Deposits/Activation/`

### Migración par y pantallas base

- [ ] T056 Generar la migración par `CaptacionesAportes` con las tablas fundacionales (regenerable hasta el congelamiento de la fase final) en los dos proyectos de migraciones — **iniciada** el 2026-10-18 con T012 (sólo `Nature` y sus índices); se regenera al sumar las tablas fundacionales
- [ ] T057 [P] Crear el cliente tipado `CaptacionesClient` y el grupo de menú «Captaciones y aportes» (vacío hasta cada historia) en `SH/Services/Cartera/CaptacionesClient.cs` y `SH/Layout/NavMenu.razor`
- [ ] T058 Sumar el módulo de captaciones a `ModulosMigrados` de `TR/Principles/LasPantallasDicenQueEstanCargando.cs`
- [ ] T059 Compilar y dejar verdes Domain, Application, Architecture, Shared y las e2e existentes de la 013 en los dos motores

**Checkpoint**: lo heredado retirado, la línea tiene naturaleza, el libro, el contrato vacío, la
contabilidad y el ciclo diario existen; ninguna regresión en la 013.

## Phase 3: User Story 1 - Parametrizar líneas de captación y de aportes con vigencia (Priority: P1) 🎯 MVP (K7/K8)

**Goal**: el catálogo común de líneas admite captación y aportes con versiones inmutables con vigencia.

**Independent Test**: crear una línea a la vista con tramos y una de aportes con cuota del 3 % del ingreso,
versionar con fecha futura y consultar a una fecha anterior (quickstart §2, US1).

- [ ] T060 [P] [US1] Crear `DepositLineVersion` (todos los campos de `data-model.md` §2.1) en `D/Entities/Lending/Deposits/Configuration/DepositLineVersion.cs`
- [ ] T061 [P] [US1] Crear `DepositLineRateTier` (§2.2) en `D/Entities/Lending/Deposits/Configuration/DepositLineRateTier.cs`
- [ ] T062 [P] [US1] Crear `ContributionLineVersion` (§2.5) en `D/Entities/Lending/Deposits/Configuration/ContributionLineVersion.cs`
- [ ] T063 [US1] Configuraciones EF de T060–T062 en `P/Configurations/Lending/Deposits/`; `ContributionLineVersion` entra en la migración par `CaptacionesAportes` y las de captación inician la migración par `CaptacionesAhorro`
- [ ] T064 [US1] Invariantes de versión (sin retroactivas, clase permitida por el perfil regulatorio, tope de cuota de fondos de empleados y fracción mínima a aportes, tramos sin solape) en `D/Lending/Deposits/ReglasDeLineaDeCaptacion.cs`, con pruebas en `TD/Lending/Deposits/ReglasDeLineaDeCaptacionTests.cs`
- [ ] T065 [US1] Comandos `CreateDepositLineCommand`, `AddDepositLineVersionCommand`, `CreateContributionLineCommand`, `AddContributionLineVersionCommand` con validadores, sobre `CreditLine` con `Nature`, en `A/Lending/Deposits/Lines/`
- [ ] T066 [US1] Consultas `ListLendingLinesQuery` (filtro `nature`) y `GetLendingLineQuery` (con versión a una fecha, (vigencia bitemporal con `VigenciaBitemporal`) en `A/Lending/Deposits/Lines/`
- [ ] T067 [US1] Aviso de cambio de tasa de una línea a la vista a sus titulares con la anticipación de `Captaciones.AnticipacionAvisoCambioDeTasaDias` (FR-038), por el canal configurado, en `A/Lending/Deposits/Lines/AvisoDeCambioDeTasa.cs`
- [ ] T068 [P] [US1] Pruebas de handlers y validadores (perfil sin captación, código duplicado `Catalogo.CodigoDuplicado`, versión retroactiva) en `TA/Lending/Deposits/Lines/`
- [ ] T069 [US1] Rutas de `contracts/api.md` §3 en `API/Endpoints/Lending/Deposits/LinesEndpoints.cs`
- [ ] T070 [US1] Pantalla `/cartera/lineas` con filtro de naturaleza y editores de versión de captación y de aportes (con `CampoCodigo`, `IndicadorDeCarga`, `PermissionGate`) en `SH/Pages/Cartera/Captaciones/Lineas.razor` y `SH/Pages/Cartera/Captaciones/EditorDeLineaDeCaptacion.razor`
- [ ] T071 [US1] e2e de US1 (escenarios 1 a 4 de la spec) en `TI/Lending/Deposits/LineasDeCaptacionTests.cs`

**Checkpoint**: líneas de captación y aportes configurables; las de crédito intactas.

---

## Phase 4: User Story 2 - Abrir cuentas con titulares, firmantes y beneficiarios (Priority: P1) (K7/K8)

**Goal**: apertura en tres pasos con validaciones, titularidad, menores, beneficiarios y exención de GMF.

**Independent Test**: cuenta conjunta con beneficiarios 60/40, marca de exención y segunda marca rechazada;
cuenta de un menor que pasa al titular a los 18 años (quickstart §2, US2).

- [ ] T072 [P] [US2] Crear `GmfExemptAccount` (§3.8, una vigente por persona) en `D/Entities/Lending/Deposits/Accounts/GmfExemptAccount.cs` y su configuración
- [ ] T073 [US2] Comando `OpenMemberAccountCommand` (asociado vigente si lo exige el perfil o la línea, conocimiento al día, habeas data, listas restrictivas con retención para el oficial, origen de fondos sobre umbral, número por `NumeradorDeCartera`, estado `Opening`/`Active`) en `A/Lending/Deposits/Accounts/OpenMemberAccountCommand.cs`
- [ ] T074 [US2] Comandos de participantes: cotitulares con proporción, manejo indistinto o conjunto, firmantes con límite, representante legal con soporte (adjunto) y beneficiarios que suman 100 %, con vigencia, en `A/Lending/Deposits/Accounts/Parties/`
- [ ] T075 [US2] Comandos `MarkGmfExemptAccountCommand` y `UnmarkGmfExemptAccountCommand` con la política vigente (`Lending.Gmf.AlreadyMarked`) en `A/Lending/Deposits/Accounts/GmfExemption/`
- [ ] T076 [US2] Mayoría de edad en `PasoDeCaptaciones`: cierra la vigencia del representante, marca `RequiresDataUpdate` y avisa al titular y al representante (R22) en `A/Lending/Deposits/Accrual/MayoriaDeEdad.cs`
- [ ] T077 [P] [US2] Pruebas de apertura, participantes, menores y exención (incluida concurrencia del número) en `TA/Lending/Deposits/Accounts/`
- [ ] T078 [US2] Rutas de `contracts/api.md` §4 (apertura, participantes, cuenta) y §10 (exención) en `API/Endpoints/Lending/Deposits/MemberAccountsEndpoints.cs` y `GmfExemptionsEndpoints.cs`
- [ ] T079 [US2] Pantallas `/cartera/ahorros` (lista y apertura con `PersonSearchPicker`) y `/cartera/ahorros/{id}` (ficha con participantes) en `SH/Pages/Cartera/Captaciones/CuentasDeAhorro.razor` y `DetalleDeCuenta.razor`
- [ ] T080 [US2] e2e de US2 (escenarios 1 a 5 y el menor que cumple 18) en `TI/Lending/Deposits/AperturaDeCuentasTests.cs`

**Checkpoint**: cuentas abiertas con titularidad completa.

---

## Phase 5: User Story 3 - Aportes: cuota periódica, recaudo y consulta para la Cartera (Priority: P1) (K7)

**Goal**: cuotas exigibles, recaudo por contrato, nómina y pagadurías, y consulta de aportes real.

**Independent Test**: cuota de enero pagada por el recaudador de prueba; la consulta de aportes devuelve lo
mismo que el extracto; pagaduría con dos grupos de archivo y concepto agrupado (quickstart §2 US3 y §3).

- [ ] T081 [P] [US3] Crear `AccountInstallment` (§3.5) y `AccountInstallmentNonDeduction` (§3.7) en `D/Entities/Lending/Deposits/Accounts/` y sus configuraciones
- [ ] T082 [US3] Generación de cuotas exigibles de aportes y permanente en `PasoDeCaptaciones` (valor fijo, % del ingreso o fracción del SMMLV, reparto con permanente) en `A/Lending/Deposits/Contributions/GeneradorDeCuotas.cs`
- [ ] T083 [US3] Implementar `ContratoDeCuentasDeCartera` para aportes: `ListarExigiblesAsync` y `ConsignarAsync` (cuotas, extraordinarios, límite de participación, saldo a favor según la línea) con auditoría y validador, en `A/Lending/Integration/ContratoDeCuentasDeCartera.cs`
- [ ] T084 [US3] Implementar `ConsultaDeAportesDeCartera` v2 y registrarla para cooperativas con aportes activados; `ContextoDeAportes` reemplaza la bandera `RigeD19` por los parámetros `Riesgo.ExcepcionAportesArrastre` y `Riesgo.DescuentoAportesDeterioro` sembrados en T017 (R20) en `A/Lending/Integration/ConsultaDeAportesDeCartera.cs` y `D/Lending/Risk/ContextoDeAportes.cs`
- [ ] T085 [US3] Hechos `AporteRecibido` y su reverso (`contracts/mensajes.md` §4) emitidos en la transacción en `A/Common/Integration/Contracts/Lending/Deposits/` y `A/Lending/Deposits/Contributions/`
- [ ] T086 [US3] Devolución de aportes sin retiro con validador (`RequestContributionRefundCommand`: sólo exceso sobre el límite o amortización, nunca el mínimo no reducible ni con créditos vivos, FR-042, FR-044) y cesión entre asociados (`RequestContributionAssignmentCommand`, sólo si los estatutos la permiten, FR-048) con aprobación, en `A/Lending/Deposits/Contributions/`
- [ ] T087 [P] [US3] Crear `IssuedDocumentAccount` (`LND_IssuedDocumentAccounts`) y los generadores QuestPDF de constancia y paz y salvo de aportes (`ContributionCertificate`, `ContributionClearance`, `contracts/documentos.md`) con anulación por reverso, en `D/Entities/Lending/Documents/`, `API/Reports/Captaciones/` y `A/Lending/Deposits/Documents/`
- [ ] T088 [P] [US3] Vistas `contributions-by-member` e `installments-in-arrears` del centro de informes en `A/Lending/Reports/VistaDeInformeDeCartera.cs` y `A/Lending/Deposits/Reports/`
- [ ] T089 [US3] Nómina (E-02): conceptos `DESC_APORTES` y `DESC_AHORROS`, tabla `PAY_PayrollAccountDeductions` y aplicación por `ConsignarAsync` al aprobar la corrida, reverso con la reversa de la corrida, en `A/Payroll/` y `P/Configurations/Payroll/`
- [ ] T090 [US3] Pagadurías (FR-125, E-05): `PayrollAgreementFileGroup` y `PayrollAgreementConceptEquivalence` (`data-model.md` §1.4) con configuraciones, en `D/Entities/Lending/Payroll/` (extienden el convenio de K2; prerrequisito C22)
- [ ] T091 [US3] Generación por grupo de archivo con el formato del grupo y `DescuentosPorPagaduria` v2; `FlatFileReader` para la respuesta con equivalencias y reparto (`Priority`/`Proportional`), aplicación por los dos contratos en la misma unidad de trabajo y «no aplicados», en `A/Common/BankFiles/FlatFileReader.cs` y `A/Lending/Payroll/`
- [ ] T092 [P] [US3] Pruebas de cuotas, contrato de aportes, consulta v2, nómina y reparto de pagaduría en `TA/Lending/Deposits/Contributions/` y `TA/Lending/Payroll/PagaduriasConfigurablesTests.cs`
- [ ] T093 [US3] Rutas de `contracts/api.md` §7 y §18.3 en `API/Endpoints/Lending/Deposits/ContributionsEndpoints.cs` y la ruta de Nómina `/api/payroll/runs/{runId}/account-deductions`
- [ ] T094 [US3] Pantallas `/cartera/aportes` y `/cartera/aportes/{id}` y la configuración de grupos y equivalencias en la pantalla de convenios de pagaduría, en `SH/Pages/Cartera/Captaciones/Aportes.razor`, `DetalleDeAportes.razor` y la página de convenios de K2
- [ ] T095 [US3] e2e de US3 (escenarios 1 a 4) y de pagadurías (quickstart §3) en `TI/Lending/Deposits/AportesTests.cs` y `TI/Lending/Deposits/PagaduriasTests.cs`

**Checkpoint**: aportes recaudados por todas las vías; la Cartera ya no usa la plantilla de saldos.

---

## Phase 6: User Story 10 - Retiro del asociado: cruce con la deuda y devolución (Priority: P1) (K7)

**Goal**: el trámite completo del retiro vive en la Cartera.

**Independent Test**: 3.000.000 de aportes, 1.000.000 de ahorro y un crédito de 2.500.000: cruce, devolución
de 1.500.000 y hechos (quickstart §2, US10).

- [ ] T096 [P] [US10] Crear `MembershipWithdrawal` (estados de `data-model.md` §6.1) y `MembershipWithdrawalLine` (§6.2) en `D/Entities/Lending/Deposits/Processes/` y sus configuraciones
- [ ] T097 [US10] Comandos `FileMembershipWithdrawalCommand`, `DesistMembershipWithdrawalCommand` y la aprobación como `SessionItem` de un órgano con acta (sin que quien radica apruebe) en `A/Lending/Deposits/Withdrawals/`
- [ ] T098 [US10] Al aprobar, emitir `RetiroDeAsociadoAprobadoV1`; crear el destino `Associates` con `DestinoAsociados`, participante `ASO` y recibo `COR_AssociateInboundMessages`, y que Asociados cambie el estado (E-04) en `A/Common/Integration/Contracts/Lending/Deposits/`, `A/Core/Associates/` y `P/Configurations/Core/`
- [ ] T099 [US10] Liquidación en K7: aportes por clase, saldos de ahorro sin intereses pendientes (los intereses a la fecha los suma la tarea de US5 y el CDAT la de US6), créditos vivos, pérdidas por parámetro y acta (hecho `PerdidaAplicadaAlRetiro`), mínimo irreducible de la entidad, con validador, en `A/Lending/Deposits/Withdrawals/SettleMembershipWithdrawalCommand.cs`
- [ ] T100 [US10] Cruce interno con la aplicación de pagos del crédito (`InternalOffset`, prelación de los estatutos, cerrojos crédito → cuenta, una sola operación); reverso del pago que arrastra el débito y deja `ToRedo`; hecho `CruceAlRetiroAplicado`, en `A/Lending/Deposits/Withdrawals/CruceDeRetiro.cs`
- [ ] T101 [US10] Devolución por `AccountPayoutOrder` y `OrdenDeGiroEmitida` v2 en el plazo de `Captaciones.DiasPlazoDevolucionAportes` (hechos `DevolucionOrdenada` y `DevolucionGirada`); resultado de Tesorería que reintegra si se rechaza, en `A/Lending/Deposits/Operations/OrdenesDeGiroDeCuentas.cs`
- [ ] T102 [US10] Enmienda E-04: Asociados deja de registrar retiros por su cuenta (`Associate.WithdrawalOwnedByLending`) y la baja de persona consulta productos vivos (`Person.HasLiveDepositProducts`, `ProductosDeCaptacionAsync`) y el reingreso abre cuentas nuevas conservando las anteriores (FR-085) en `A/Core/Associates/`, `A/Core/People/` y `A/Lending/Deposits/Accounts/`
- [ ] T103 [P] [US10] Documento de liquidación de retiro (`MembershipWithdrawalSettlement`, `contracts/documentos.md`) en `API/Reports/Captaciones/` y `A/Lending/Deposits/Documents/`
- [ ] T104 [P] [US10] Pruebas de trámite, liquidación, cruce, pérdidas, mínimo irreducible y reverso en `TA/Lending/Deposits/Withdrawals/`
- [ ] T105 [US10] Rutas de `contracts/api.md` §11 en `API/Endpoints/Lending/Deposits/MembershipWithdrawalsEndpoints.cs`
- [ ] T106 [US10] Pantallas `/cartera/retiros-asociado` y `/cartera/retiros-asociado/{id}` en `SH/Pages/Cartera/Captaciones/RetirosDeAsociado.razor` y `DetalleDeRetiro.razor`
- [ ] T107 [US10] e2e de US10 (escenarios 1 a 4) en `TI/Lending/Deposits/RetiroDeAsociadoTests.cs`

**Checkpoint**: K7 funcional de punta a punta salvo migración y contabilidad completa.

---

## Phase 7: User Story 4 - Consignar, retirar y trasladar por el contrato de la Cartera (Priority: P1) (K8)

**Goal**: todas las operaciones de depósito por el contrato, con GMF, canje, atribuciones, cargos y débito
automático.

**Independent Test**: consignar 1.000.000, retirar 400.000 de cuenta exenta y no exenta, repetir la clave y
retirar sobre la atribución (quickstart §2, US4); casos dorados de GMF.

- [ ] T108 [P] [US4] Motor puro `GravamenMovimientosFinancieros` (política, tope en UVT, hechos generadores, tarifa cero o desactivado desde una fecha) en `D/Lending/Deposits/GravamenMovimientosFinancieros.cs`
- [ ] T109 [P] [US4] Casos dorados de GMF (tope cruzado 6.676; redención de CDAT abonada 41.952; traslado mismo titular; reverso; tarifa cero desde una fecha) en `TD/Lending/Deposits/Casos/gmf-*.json`
- [ ] T110 [P] [US4] Crear `GmfMonthlyUsage` (§4.7) en `D/Entities/Lending/Deposits/Transactions/GmfMonthlyUsage.cs` y su configuración
- [ ] T111 [US4] Completar `ContratoDeCuentasDeCartera`: `ConsultarAsync`, `SimularAsync`, `RetirarAsync`, `TrasladarAsync`, `ReversarAsync` (firmas y manejo, disponible, mínimo, máximos, efectivo, atribución con monto, GMF enlazado, canje por días hábiles de la clase de medio, sobregiro técnico con bloqueo y alerta, marca de movimiento del titular por tipo de transacción FR-058, el disponible que muestra `ConsultarAsync` es el mismo que valida el retiro SC-007) en `A/Lending/Integration/ContratoDeCuentasDeCartera.cs`
- [ ] T112 [US4] Mensaje `ConsignacionReversadaPorCartera` v1 y hechos de consignación, retiro, traslado, GMF y `OperacionDeCuentaReversada` en `A/Common/Integration/Contracts/Lending/Deposits/`
- [ ] T113 [US4] Recaudador `Treasury` (transferencias recibidas) y dobles de prueba `/api/lending/test-doubles/collector/accounts/*` sólo fuera de producción en `A/Lending/Integration/` y `API/Endpoints/Lending/Deposits/TestDoublesEndpoints.cs`
- [ ] T114 [P] [US4] Crear `DepositLineCharge` (§2.3), `AccountCharge` (§4.8) y `AccountChargeExemption` (§4.9) con configuraciones en `D/Entities/Lending/Deposits/`
- [ ] T115 [P] [US4] Motor puro `CargosDeCuenta` (gratuitos, exoneraciones, valor fijo o porcentaje con mínimo y máximo) en `D/Lending/Deposits/CargosDeCuenta.cs` con casos dorados en `TD/Lending/Deposits/Casos/cargo-*.json`
- [ ] T116 [US4] Causación de cargos (periódicos en `PasoDeCaptaciones`, por evento en el contrato), pendiente si no alcanza, condonación con aprobación, cobro del pendiente con la siguiente consignación (hecho `CargoPendienteCobrado`), y mensajes `CargoDeCaptacionCausado`/`Reversado` a Comercial (`CAR → Inventory`) con la respuesta `FacturaDeCargoRegistrada` en `A/Lending/Deposits/Charges/`
- [ ] T117 [US4] Comercial: consumir `CargoDeCaptacionCausado` y `…Reversado` para facturar el cargo gravado y su nota crédito, en el módulo comercial de la 012 (`A/Inventory/Sales/` o donde viva la facturación)
- [ ] T118 [P] [US4] Crear `DirectDebitMandate` (§3.6) en `D/Entities/Lending/Deposits/Accounts/DirectDebitMandate.cs` y su configuración
- [ ] T119 [US4] Débito automático en `PasoDeCaptaciones`: operación interna que debita la cuenta y aplica el pago del crédito (`InternalOffset`, `OwnLoanPayoff`, clave `CAR:debito-automatico:…`), y `PagoAplicado.AdmiteProceso = true` sólo para `OwnLoanPayoff` en `A/Lending/Deposits/Operations/DebitoAutomatico.cs`
- [ ] T120 [US4] Al cumplir el titular 18 años, anular los retiros del representante en espera de aprobación con aviso (FR-033) en `A/Lending/Deposits/Accrual/MayoriaDeEdad.cs`
- [ ] T121 [P] [US4] Vistas `deposit-balances`, `deposit-transactions`, `gmf` y `charges` del centro de informes en `A/Lending/Deposits/Reports/`
- [ ] T122 [P] [US4] Pruebas del contrato (todas las validaciones, idempotencia, canje, sobregiro técnico), cargos y débito automático en `TA/Lending/Deposits/Operations/`
- [ ] T123 [US4] Rutas de `contracts/api.md` §4 (operaciones y cargos) y §9 en `API/Endpoints/Lending/Deposits/MemberAccountsEndpoints.cs` y `DirectDebitsEndpoints.cs`
- [ ] T124 [US4] Pantalla de movimientos, cargos y débito automático en la ficha de cuenta `SH/Pages/Cartera/Captaciones/DetalleDeCuenta.razor` y `MovimientosDeCuenta.razor`
- [ ] T125 [US4] e2e de US4 (escenarios 1 a 6), cargos (quickstart) y débito automático en `TI/Lending/Deposits/MovimientosTests.cs` y `CargosYDebitoAutomaticoTests.cs`
- [ ] T126 [US4] Prueba de carga del contrato con reintentos de la misma clave: cero operaciones duplicadas (`RUN_PERF_TESTS=1`, SC-005) en `TI/Lending/Deposits/ContratoDeCuentasCargaTests.cs`

**Checkpoint**: el dinero entra y sale sólo por el contrato.

---

## Phase 8: User Story 5 - Causar y liquidar intereses con retención en la fuente (Priority: P1) (K8)

**Goal**: causación diaria y liquidación reproducibles, con retención según la norma.

**Independent Test**: casos dorados de interés y retención; liquidar dos veces el mismo período no duplica.

- [ ] T127 [P] [US5] Motor puro `InteresDeCaptacion` (saldo diario, promedio, mínimo; tramos `WholeBalance`/`Stepped`; mínimo para interés; tasa variable) sobre `EquivalenciaDeTasas`, `ContadorDeDias` y `Causacion` en `D/Lending/Deposits/InteresDeCaptacion.cs`
- [ ] T128 [P] [US5] Motor puro `RetencionSobreRendimientos` (perfil, umbral diario sobre el total, alcance del umbral, componente inflacionario, tarifa por clase) en `D/Lending/Deposits/RetencionSobreRendimientos.cs`
- [ ] T129 [P] [US5] Casos dorados: 6.884 por saldo diario, 3.649 por saldo mínimo, tramos escalonados y por total, cambio de tasa a mitad de mes, bisiesto, umbral alcanzado y no, con y sin componente inflacionario, persona exenta, en `TD/Lending/Deposits/Casos/interes-*.json` y `retencion-*.json`
- [ ] T130 [P] [US5] Crear `InterestSettlement` (§4.5) y `WithholdingAccumulation` (§4.6) en `D/Entities/Lending/Deposits/Transactions/` y sus configuraciones
- [ ] T131 [US5] Causación diaria en `PasoDeCaptaciones` por diferencia de acumulados con marca de agua, puesta al día en orden y alerta de atraso, suspensión de causación en cuentas embargadas totalmente o de titular fallecido según la línea (FR-065), en `A/Lending/Deposits/Accrual/CausacionDeCaptaciones.cs`
- [ ] T132 [US5] Liquidación idempotente por cuenta y período (abono neto, retención antes de capitalizar, ajuste de redondeo, acumulado por titular y año) en `A/Lending/Deposits/Accrual/LiquidacionDeIntereses.cs`
- [ ] T133 [US5] Ajuste de causación por fecha valor enlazado y simulación sin efectos (`SimulateInterestSettlementQuery`) en `A/Lending/Deposits/Accrual/`
- [ ] T134 [US5] Hechos `InteresDeCaptacionCausado` (por lotes resumido, partes `CCA-`), `AjusteDeCausacionDeCaptacion`, `InteresDeCaptacionLiquidado` y `RetencionPracticada` en `A/Common/Integration/Contracts/Lending/Deposits/` y `A/Lending/Deposits/Accrual/PartesDeHechosDeCaptaciones.cs`
- [ ] T135 [US5] Cancelación de cuenta con validador (`CloseMemberAccountCommand` y `/close-preview`: liquida a la fecha, retiene, cobra GMF, entrega o traslada el saldo; no con bloqueos, embargos o canje, FR-039) en `A/Lending/Deposits/Accounts/CloseMemberAccountCommand.cs`
- [ ] T136 [US5] Sumar los ahorros con intereses liquidados a la fecha a la liquidación del retiro del asociado (completa T099) en `A/Lending/Deposits/Withdrawals/SettleMembershipWithdrawalCommand.cs`
- [ ] T137 [P] [US5] Vista `interest-and-withholding` del centro de informes en `A/Lending/Deposits/Reports/`
- [ ] T138 [P] [US5] Pruebas de causación, liquidación, fecha valor y simulación en `TA/Lending/Deposits/Accrual/`
- [ ] T139 [US5] Rutas de liquidación y simulación (`contracts/api.md` §4) y la vista de intereses en la ficha de cuenta en `API/Endpoints/Lending/Deposits/MemberAccountsEndpoints.cs` y `SH/Pages/Cartera/Captaciones/InteresesDeCuenta.razor`
- [ ] T140 [US5] e2e de US5 (escenarios 1 a 5) en `TI/Lending/Deposits/InteresesTests.cs`
- [ ] T141 [US5] Prueba de volumen de causación de 100.000 cuentas en menos de 30 minutos (`RUN_PERF_TESTS=1`, SC-006) en `TI/Lending/Deposits/CausacionDeCaptacionesPerformanceTests.cs`

**Checkpoint**: intereses y retención correctos al centavo.

---

## Phase 9: User Story 8 - Contabilidad recibe cada hecho por la matriz de la Cartera (Priority: P1) (K7/K8)

**Goal**: comprobantes correctos, validación previa, conciliación y guarda de cierre para captaciones.

**Independent Test**: con la matriz completa, consignar, causar y liquidar; comprobar comprobantes y
conciliación en cero; quitar una regla y ver el bloqueo (quickstart §2, US8).

- [ ] T142 [US8] Verificar que los hechos emitidos por US3, US4, US5 y US10 se contabilizan con la tabla hecho × rol sembrada en la fase fundacional, y completar lo que falte, en `A/Accounting/Lending/Reglas/OperacionesDeCartera.cs`
- [ ] T143 [US8] Constructor de líneas para captaciones (tercero titular, documento cruce `CTA` = número de cuenta, `AccountFlag` de research C20, `IssueTermBand` sólo en CDAT y contractual) en `A/Accounting/Lending/Contabilizacion/ConstructorDeLineasDeCartera.cs`
- [ ] T144 [US8] Plantilla de la matriz v2 con filas sugeridas sin cuenta y columna `cuentaSugerida` (CUIF de `contracts/contabilidad.md`), completitud por naturaleza, en `A/Accounting/Lending/Plantilla/`
- [ ] T145 [US8] Validación previa de captaciones (bloquea en pantallas, sella y alerta en contrato y procesos) en `A/Common/Integration/Accounting/IContabilidadParaCartera.cs` y su implementación del lado contable
- [ ] T146 [US8] Conciliación por línea de captación, concepto y oficina, y guarda de cierre `LendingNotClosed` con la causación de captaciones, y la vista `deposits-reconciliation`, en `A/Lending/Integration/ConciliacionContableDeCartera.cs` y `A/Accounting/Periods/`
- [ ] T147 [US8] Lo de captaciones no se reversa desde Comprobantes (`Accounting.Document.ModuleOwned`) en `A/Accounting/Documents/DocumentCommands.cs`
- [ ] T148 [P] [US8] Pruebas de consumo (un hecho por cambio de saldo, ninguno con cuentas, SC-009), resolución, completitud, conciliación y guarda en `TA/Accounting/Lending/Captaciones/`
- [ ] T149 [US8] Filtro de naturaleza en `/contabilidad/cartera/matriz` y `/contabilidad/cartera/completitud` en `SH/Pages/Contabilidad/Cartera/`
- [ ] T150 [US8] e2e de US8 (escenarios 1 a 4) en `TI/Lending/Deposits/ContabilidadDeCaptacionesTests.cs`

---

## Phase 10: User Story 9 - Migrar los saldos vigentes y cuadrar antes de activar (Priority: P1) (K7/K8)

**Goal**: cargas todo o nada, saldo inicial sin hechos, cuadre y activación por naturaleza.

**Independent Test**: 500 cuentas con un error en la fila 37; corregir, cuadrar y activar (quickstart US9).

- [ ] T151 [P] [US9] Crear las filas de preparación de `data-model.md` §7.1 (`LND_MigrationLineEquivalenceRows`, `…ContributionAccountRows`, `…AccountInstallmentRows`, `…AccountPartyRows`, `…AccountRows`, `…HoldRows`, `…GmfExemptionRows`, `…TaxYearRows`) en `D/Entities/Lending/Deposits/Migration/` y sus configuraciones
- [ ] T152 [US9] Plantillas de `contracts/plantillas-y-archivos.md` (columna por columna, validaciones) registradas en la plataforma, aceptando `lending.contribution-balances` como fuente de aportes, en `A/Lending/Deposits/Migration/Plantillas/`
- [ ] T153 [US9] Revisión previa todo o nada con errores por fila y columna y carga reemplazable por naturaleza (hecho informativo `CargaDeCaptacionesAplicada`) en `A/Lending/Deposits/Migration/`
- [ ] T154 [US9] Saldo inicial por concepto al corte sin hechos (`OpeningBalance`) y cuadre contra los saldos contables mapeados por la matriz, con acta de diferencias, en `A/Lending/Deposits/Migration/CuadreDeCaptaciones.cs`
- [ ] T155 [US9] Completar `ActivateContributionsCommand` y `ActivateDepositsCommand` **con carga** (corte + 1, acta de diferencias, cuadre recalculado, plazo de fecha valor, mes abierto; hecho `CaptacionesActivadas`), en `A/Lending/Deposits/Activation/`
- [ ] T156 [US9] Cifras de referencia (totales cargados contra los que entrega la cooperativa, sin nombrar el sistema de origen) en `A/Lending/Deposits/Migration/CifrasDeReferencia.cs`
- [ ] T157 [P] [US9] Pruebas de revisión, cuadre y activación en `TA/Lending/Deposits/Migration/`
- [ ] T158 [US9] Rutas de `contracts/api.md` §16 en `API/Endpoints/Lending/Deposits/DepositsMigrationEndpoints.cs`
- [ ] T159 [US9] Pantallas `/cartera/captaciones/migracion` y `/cartera/captaciones/activacion` en `SH/Pages/Cartera/Captaciones/MigracionDeCaptaciones.razor` y `ActivacionDeCaptaciones.razor`
- [ ] T160 [US9] e2e de US9 (escenarios 1 a 4, en los dos motores) en `TI/Lending/Deposits/MigracionDeCaptacionesTests.cs`
- [ ] T161 [US9] Prueba de revisión de 50.000 cuentas en menos de 10 minutos (`RUN_PERF_TESTS=1`, SC-011) en `TI/Lending/Deposits/MigracionPerformanceTests.cs`

---

## Phase 11: User Story 16 - Seguridad, atribuciones y auditoría verificable (Priority: P1)

**Goal**: permisos, alcance por oficina y línea, segregación y lecturas sensibles auditadas en todo lo nuevo.

**Independent Test**: usuario de otra oficina recibe «no encontrado»; la cadena de auditoría se verifica.

- [ ] T162 [US16] Aplicar `IAlcanceDeCartera` (oficinas y líneas de cualquier naturaleza, falla cerrado, evento `Lending.Access.OutOfScope`) en todas las consultas y comandos de `A/Lending/Deposits/`
- [ ] T163 [US16] Reglas de segregación de FR-005 (embargo, apertura excepcional, entrega, devolución, asamblea, tasa negociada) por persona en `D/Lending/Segregation/` y su uso en los handlers
- [ ] T164 [US16] Lecturas sensibles auditadas (saldos y movimientos de un titular, documentos, exportaciones) en `A/Lending/Deposits/` con los eventos de `contracts/api.md`
- [ ] T165 [US16] e2e de seguridad (sin permiso = inexistente, fuera de alcance, segregación, verificación de la cadena) en `TI/Lending/Deposits/SeguridadDeCaptacionesTests.cs`

**Checkpoint P1**: K7 y K8 completas (aportes y ahorro a la vista y permanente).

---

## Phase 12: User Story 6 - Constituir, renovar y redimir un CDAT (Priority: P2) (K9)

**Goal**: CDAT con plan de intereses, gracia, prórroga, renovación, redención, tasa negociada y título.

**Independent Test**: 10.000.000 a 180 días al 10 % EA base 360 mensual: seis pagos de 79.741; prórroga tras
la gracia; tasa negociada en espera del aprobador (quickstart US6).

- [ ] T166 [P] [US6] Motor puro `PlanDeCdat` (plan vencido o anticipado, causación cuadrada al plan, gracia en días hábiles, prórroga con o sin capitalización, renovación, redención con penalidad, `IssueTermBand`) en `D/Lending/Deposits/PlanDeCdat.cs`
- [ ] T167 [P] [US6] Casos dorados de CDAT (481.244; 488.088; 6 × 79.741 = 478.446; 79.111 anticipado; prórroga con capitalización neta de retención; redención anticipada; vencimiento en día no hábil) en `TD/Lending/Deposits/Casos/cdat-*.json`
- [ ] T168 [P] [US6] Motor puro `TasaNegociada` (regla más específica, margen máximo) en `D/Lending/Deposits/TasaNegociada.cs` con pruebas en `TD/Lending/Deposits/TasaNegociadaTests.cs`
- [ ] T169 [P] [US6] Crear `MemberAccountTermsVersion` (§3.3), `TermDepositScheduleItem` (§3.4) y `NegotiatedRateRule` (§2.4) y la fila de preparación `LND_MigrationTermDepositRows` en `D/Entities/Lending/Deposits/` y sus configuraciones; generar la migración par `CaptacionesCdat`
- [ ] T170 [US6] `ConstituteTermDepositCommand` (rango de plazo, tasa de la tabla, destino de intereses, instrucción, entrada por contrato o traslado) en `A/Lending/Deposits/TermDeposits/`
- [ ] T171 [US6] Solicitud y aprobación de tasa negociada por la matriz de atribuciones (`NegotiatedRate`, puntos y monto; aprobador con segundo factor o sesión con acta) también para cuentas a la vista y contractuales cuando la línea lo declare (FR-127), en `A/Lending/Deposits/TermDeposits/NegotiatedRates/`
- [ ] T172 [US6] En `PasoDeCaptaciones`: pago de intereses del plan neto de retención, vencimiento, días de gracia, prórroga automática hasta el máximo, tratamiento sin prórroga, CDAT embargado o de fallecido, reclasificación por `IssueTermBand` (`CaptacionReclasificada`) en `A/Lending/Deposits/TermDeposits/CicloDelCdat.cs`
- [ ] T173 [US6] `RenewTermDepositCommand` y `RedeemTermDepositCommand` (simulación previa, atribución, GMF de la redención abonada) en `A/Lending/Deposits/TermDeposits/`
- [ ] T174 [US6] Hechos `CdatConstituido`, `InteresesDeCdatPagados`, `CdatProrrogado`, `CdatRenovado`, `CdatRedimido`, `CdatCancelado`, `PenalidadAplicada` en `A/Common/Integration/Contracts/Lending/Deposits/`
- [ ] T175 [US6] Sumar los CDAT vigentes a la liquidación del retiro del asociado (siguen hasta su vencimiento salvo que la línea diga otra cosa) en `A/Lending/Deposits/Withdrawals/SettleMembershipWithdrawalCommand.cs`
- [ ] T176 [P] [US6] Vistas `term-deposit-maturities`, `negotiated-rates` y `depositor-concentration` del centro de informes en `A/Lending/Deposits/Reports/`
- [ ] T177 [P] [US6] Pruebas de handlers de CDAT y tasa negociada en `TA/Lending/Deposits/TermDeposits/`
- [ ] T178 [US6] Rutas de `contracts/api.md` §5 y §6 en `API/Endpoints/Lending/Deposits/TermDepositsEndpoints.cs` y `NegotiatedRatesEndpoints.cs`
- [ ] T179 [US6] Pantallas `/cartera/cdat`, `/cartera/cdat/nuevo`, `/cartera/cdat/{id}` y el editor de reglas de tasa negociada en `SH/Pages/Cartera/Captaciones/Cdat*.razor`
- [ ] T180 [US6] e2e de US6 (escenarios 1 a 6) en `TI/Lending/Deposits/CdatTests.cs`

---

## Phase 13: User Story 7 - Ahorro contractual o programado (Priority: P2) (K9)

**Goal**: cuotas, bonificación por cumplimiento e incumplimiento.

**Independent Test**: 12 × 200.000: 2.486.777 cumpliendo; tasa castigo pagando 10.

- [ ] T181 [P] [US7] Motor puro `AhorroContractual` en `D/Lending/Deposits/AhorroContractual.cs` con casos dorados (2.465.306; 2.486.777; bonificación 21.471; incumplimiento) en `TD/Lending/Deposits/Casos/contractual-*.json`
- [ ] T182 [US7] Cuotas del contractual en `GeneradorDeCuotas`, estado de cumplimiento, incumplimiento por tolerancia y vencimiento con traslado o giro en `A/Lending/Deposits/Contractual/`
- [ ] T183 [US7] Hechos de bonificación (hecho `BonificacionReconocida` con la transacción `BonusRecognized`, gasto según la política `BonusRecognition`) en `A/Common/Integration/Contracts/Lending/Deposits/`
- [ ] T184 [P] [US7] Pruebas de handlers del contractual en `TA/Lending/Deposits/Contractual/`
- [ ] T185 [US7] Vista del contrato en la ficha de cuenta en `SH/Pages/Cartera/Captaciones/ContratoDeAhorro.razor`
- [ ] T186 [US7] e2e de US7 (escenarios 1 y 2) en `TI/Lending/Deposits/AhorroContractualTests.cs`

---

## Phase 14: User Story 11 - Bloqueos y embargos con límite inembargable (Priority: P2) (K8)

**Goal**: bloqueos y embargos con respuesta al juzgado y consignación.

**Independent Test**: dos cuentas sobre el límite: sólo el exceso retenido; embargo de aportes rechazado.

- [ ] T187 [P] [US11] Crear `AccountHold` (§5.1), `Garnishment` (§5.2) y `GarnishmentResponse` en `D/Entities/Lending/Deposits/Restrictions/` y sus configuraciones
- [ ] T188 [P] [US11] Motor puro `LimiteInembargable` (suma de cuentas, alimentos, orden de cuentas) en `D/Lending/Deposits/LimiteInembargable.cs` con casos dorados en `TD/Lending/Deposits/Casos/embargo-*.json`
- [ ] T189 [US11] Comandos de bloqueo (incluido el de garantía de un crédito) y de embargo, desembargo, respuesta al juzgado (documento) y consignación por orden de giro (hecho `EmbargoConsignado`); vista `garnishments`, en `A/Lending/Deposits/Holds/`
- [ ] T190 [P] [US11] Pruebas de bloqueos y embargos en `TA/Lending/Deposits/Holds/`
- [ ] T191 [US11] Rutas de `contracts/api.md` §8 y de bloqueos en `API/Endpoints/Lending/Deposits/GarnishmentsEndpoints.cs`
- [ ] T192 [US11] Pantalla `/cartera/embargos` en `SH/Pages/Cartera/Captaciones/Embargos.razor`
- [ ] T193 [US11] e2e de US11 (escenarios 1 a 3) en `TI/Lending/Deposits/EmbargosTests.cs`

---

## Phase 15: User Story 13 - Documentos al titular (Priority: P2) (K8/K9)

**Goal**: los seis tipos de documento que faltan (de los 9; constancia, paz y salvo y liquidación de retiro ya nacen en K7) con verificación y anulación por reverso.

**Independent Test**: certificado tributario anual; reversar una retención de diciembre lo anula.

- [ ] T194 [P] [US13] Eventos de documento nuevos y anulación por reverso de operaciones de cuenta (`IssuedDocumentAccount` ya existe desde US3) en `D/Entities/Lending/Documents/`
- [ ] T195 [US13] Generadores QuestPDF de extracto, certificado de saldo, certificado tributario anual (cuadra con liquidaciones, retenciones y GMF del año, SC-016), certificado de retención, título CDAT «no negociable» y respuesta al juzgado (`GarnishmentResponse`) (`contracts/documentos.md`) en `API/Reports/Captaciones/`
- [ ] T196 [US13] Emisión, copia guardada, anulación automática por reverso y lotes de envío en `A/Lending/Deposits/Documents/`
- [ ] T197 [P] [US13] Pruebas de emisión y anulación en `TA/Lending/Deposits/Documents/`
- [ ] T198 [US13] Rutas de `contracts/api.md` §17 en `API/Endpoints/Lending/Deposits/DepositDocumentsEndpoints.cs`
- [ ] T199 [US13] Emisión desde la ficha de cuenta y de aportes en `SH/Pages/Cartera/Captaciones/DocumentosDeCuenta.razor`
- [ ] T200 [US13] e2e de US13 (escenarios 1 y 2) en `TI/Lending/Deposits/DocumentosDeCaptacionesTests.cs`

---

## Phase 16: User Story 12 - Revalorización de aportes y distribución de excedentes (Priority: P2) (K10)

**Goal**: decisiones de asamblea simuladas, aprobadas y aplicadas en una operación.

**Independent Test**: 5 % con IPC 4 % y fondo menor: limitada y prorrateada.

- [ ] T201 [P] [US12] Motor puro `RevalorizacionYExcedentes` en `D/Lending/Deposits/RevalorizacionYExcedentes.cs` con casos dorados en `TD/Lending/Deposits/Casos/asamblea-*.json`
- [ ] T202 [P] [US12] Crear `AssemblyDistribution` y `AssemblyDistributionLine` (§6.3, §6.4) y sus configuraciones; generar la migración par `CaptacionesAsamblea`
- [ ] T203 [US12] Registrar, simular, aprobar (segundo usuario) y aplicar revalorización, excedentes y amortización; reverso entero; hechos de asamblea, en `A/Lending/Deposits/Assembly/`
- [ ] T204 [P] [US12] Pruebas de asamblea en `TA/Lending/Deposits/Assembly/`
- [ ] T205 [US12] Rutas de `contracts/api.md` §12 y pantalla `/cartera/asamblea` en `API/Endpoints/Lending/Deposits/AssemblyDistributionsEndpoints.cs` y `SH/Pages/Cartera/Captaciones/Asamblea.razor`
- [ ] T206 [US12] e2e de US12 (escenarios 1 y 2) en `TI/Lending/Deposits/AsambleaTests.cs`

---

## Phase 17: User Story 14 - Inactividad, no reclamados y fallecimiento del titular (Priority: P2) (K10)

**Goal**: ciclo de vida de cuentas sin movimiento y de titulares fallecidos.

**Independent Test**: inactiva a los días de la línea (el interés no la reactiva); fallecido con dos
beneficiarios genera dos órdenes de giro.

- [ ] T207 [P] [US14] Crear `DeceasedHolderCase` (§6.5) y `UnclaimedBalance` (§6.6) y sus configuraciones
- [ ] T208 [US14] Inactividad y reactivación en `PasoDeCaptaciones` con reclasificación (`CaptacionReclasificada`) y cargo por inactividad si la línea lo tiene, en `A/Lending/Deposits/Accrual/Inactividad.cs`
- [ ] T209 [US14] Traslado a no reclamados y pago al aparecer el titular en el plazo de `Captaciones.DiasParaNoReclamados` (hecho `SaldoTrasladadoANoReclamados`; vista `unclaimed`) en `A/Lending/Deposits/Unclaimed/`
- [ ] T210 [US14] Fallecimiento: bloqueo, entrega a beneficiarios o sin juicio de sucesión hasta el tope (soportes del parámetro) o por sucesión, aprobación de segundo usuario, órdenes de giro, en `A/Lending/Deposits/Deceased/`
- [ ] T211 [P] [US14] Pruebas de inactividad, no reclamados y fallecimiento en `TA/Lending/Deposits/`
- [ ] T212 [US14] Rutas de `contracts/api.md` §13 y §14 y pantallas `/cartera/fallecimientos` y `/cartera/no-reclamados` en `API/Endpoints/Lending/Deposits/DeceasedCasesEndpoints.cs` y `UnclaimedEndpoints.cs`, y `SH/Pages/Cartera/Captaciones/`
- [ ] T213 [US14] e2e de US14 (escenarios 1 y 2) en `TI/Lending/Deposits/CicloDeVidaDeCuentasTests.cs`

---

## Phase 18: User Story 15 - Fondo de liquidez y depósitos asegurados (Priority: P3) (K11)

**Goal**: base y requerido a cualquier fecha y depósitos asegurados.

**Independent Test**: requerido por banda igual al cálculo manual; alerta si el constituido es menor.

- [ ] T214 [P] [US15] Motor puro `FondoDeLiquidez` (categoría, bandas, transición del Decreto 976 de 2026) en `D/Lending/Deposits/FondoDeLiquidez.cs` con casos dorados en `TD/Lending/Deposits/Casos/liquidez-*.json`
- [ ] T215 [P] [US15] Crear `LiquidityFundSnapshot` y `LiquidityFundHolding` (§6.7, §6.8) y sus configuraciones; generar la migración par `CaptacionesLiquidez`
- [ ] T216 [US15] Cálculo e instantánea, registro del constituido, alerta `Cartera.FondoDeLiquidezInsuficiente` y depósitos asegurados por ahorrador en `A/Lending/Deposits/Liquidity/`
- [ ] T217 [US15] Rutas de `contracts/api.md` §15 y pantalla `/cartera/fondo-de-liquidez` en `API/Endpoints/Lending/Deposits/LiquidityFundEndpoints.cs` y `SH/Pages/Cartera/Captaciones/FondoDeLiquidez.razor`
- [ ] T218 [US15] e2e de US15 en `TI/Lending/Deposits/FondoDeLiquidezTests.cs`

---

## Phase 19: User Story 17 - Informes de gestión y reportes regulatorios (Priority: P3) (K11)

**Goal**: 21 vistas del centro de informes y archivos SICSES y exógena.

**Independent Test**: formato de captaciones de un mes cuadra con la conciliación y sale con la alerta
«formato sin cotejar».

- [ ] T219 [US17] Vistas que faltan de `contracts/api.md` §18.1 (`deposit-accounts-by-status`, `liquidity-fund`, `insured-deposits` y las regulatorias) en `A/Lending/Reports/VistaDeInformeDeCartera.cs` y sus consultas en `A/Lending/Deposits/Reports/`
- [ ] T220 [US17] Layouts versionados como dato con `verified = false` (SICSES captaciones, saldos diarios, fondo de liquidez; exógena 1019 y 1020) en `A/Lending/Regulatory/Layouts/` con el catálogo `CatalogoDeLayoutsDeCartera` sobre `Domain/Common/Layouts` (research C21) y su generador
- [ ] T221 [US17] Rutas de `contracts/api.md` §18.2 en `API/Endpoints/Lending/Deposits/RegulatoryEndpoints.cs`
- [ ] T222 [US17] Tarjetas del Centro de Reportes y vista en `SH/Pages/Cartera/Informes.razor` (los `NavigateTo` llevan a páginas existentes)
- [ ] T223 [P] [US17] Pruebas de vistas y layouts en `TA/Lending/Deposits/Reports/`
- [ ] T224 [US17] e2e de US17 en `TI/Lending/Deposits/InformesDeCaptacionesTests.cs`

---

## Phase 20: Polish & Cross-Cutting Concerns

- [ ] T225 Congelar las migraciones pares (`CaptacionesAportes`, `CaptacionesAhorro`, `CaptacionesCdat`, `CaptacionesAsamblea`, `CaptacionesLiquidez`): regenerarlas una última vez en orden y verificar que aplican sobre una base de la 013 en los dos motores
- [ ] T226 [P] Manual para la contadora y el jefe de captaciones (productos, intereses, retención, GMF, CDAT, aportes, pendientes de validar) en `docs/manual/cartera-captaciones.md`
- [ ] T227 [P] Ampliar `docs/manual/contabilidad-contrato-de-contabilizacion.md` con la matriz de captaciones y las cuentas CUIF sugeridas
- [ ] T228 [P] Runbook de puesta en marcha de captaciones (diagnóstico, retiro heredado, carga, cuadre, activación por naturaleza) en `docs/operaciones/captaciones-puesta-en-marcha.md`
- [ ] T229 Actualizar `CLAUDE.md` (sección de la feature 014 y totales remedidos con los comandos de la tabla «Totales»)
- [ ] T230 Correr todas las pruebas sin contenedores y las e2e completas («Captaciones e2e» y las de la 013) en PostgreSQL y SQL Server, y registrar las cifras
- [ ] T231 Recorrer `specs/014-ahorros-aportes-cdt/quickstart.md` de punta a punta y corregir lo que no cuadre
- [ ] T232 Verificar que ninguna pantalla, informe, plantilla ni documento nombra al sistema anterior (búsqueda en `src/` y `docs/`)
- [ ] T233 Prueba que cuenta los casos dorados de captaciones (mínimo 40, SC-002) en `TD/Lending/Deposits/CasosDoradosDeCaptaciones.cs`
- [ ] T234 **Dueño**: medir con usuarios de la cooperativa piloto los tiempos de apertura y caja (SC-004) y el éxito al primer intento (SC-018), y registrar el resultado en `docs/operaciones/captaciones-puesta-en-marcha.md`
- [ ] T235 **Dueño y contadora**: validar parámetros «pendiente de validar» (P2, P3, P8–P12, P26), la matriz y los casos dorados; cotejar los layouts SICSES y exógena
- [ ] T236 **Dueño**: segundo revisor y respaldos por base antes de aplicar `RetiroDeCaptacionesHeredadas` fuera de local; promoción a DEV y QA

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → historias. La 013 completa es requisito previo (T001).
- **US1 (3)** → **US2 (4)** → **US3 (5)** → **US10 (6)**: K7 (aportes). US10 necesita US3 (saldos de aportes); los ahorros con interés y el CDAT se suman a su liquidación en US5 y US6
  y los créditos de la 013.
- **US4 (7)** → **US5 (8)**: K8 (ahorro). US4 necesita US2; US5 necesita US4 (saldos con movimientos).
- **US8 (9)** necesita los hechos de US3, US4 y US5; **US9 (10)** necesita US8 (cuadre contra la matriz).
- **US16 (11)** cierra K7 y K8 sobre todo lo anterior.
- **US6 (12)** y **US7 (13)**: K9, después de US5. Independientes entre sí.
- **US11 (14)** y **US13 (15)**: después de US5; US13 usa CDAT de US6 para el título.
- **US12 (16)** y **US14 (17)**: K10, después de US3 y US5.
- **US15 (18)** y **US17 (19)**: K11, después de US6.
- **Polish (20)**: al final.

### Within Each User Story

Motores puros y casos dorados → entidades y configuraciones → handlers con validadores y pruebas → rutas →
pantallas → e2e. Cada hecho contable nace con la operación que lo origina. **Toda tarea de comando incluye su
validador FluentValidation (Principio III) y toda tarea de rutas declara `RequirePermission` y aplica el alcance
(Principio de seguridad; lo vigila la prueba de arquitectura de la fase fundacional).**

### Parallel Opportunities

- En Foundational: T014–T019 entre sí; T021–T026 entre sí; T034–T036; T046–T047.
- En cada historia: motor + casos dorados + entidades ([P]) antes de los handlers.
- US6 y US7 en paralelo; US11 y US12 en paralelo; US15 y US17 en paralelo (archivos distintos).

## Parallel Example: User Story 5

```bash
Task: "Motor puro InteresDeCaptacion en D/Lending/Deposits/InteresDeCaptacion.cs"
Task: "Motor puro RetencionSobreRendimientos en D/Lending/Deposits/RetencionSobreRendimientos.cs"
Task: "Casos dorados de interés y retención en TD/Lending/Deposits/Casos/"
Task: "Entidades InterestSettlement y WithholdingAccumulation"
```

## Parallel Example: User Story 6

```bash
Task: "Motor puro PlanDeCdat en D/Lending/Deposits/PlanDeCdat.cs"
Task: "Casos dorados de CDAT en TD/Lending/Deposits/Casos/cdat-*.json"
Task: "Motor puro TasaNegociada en D/Lending/Deposits/TasaNegociada.cs"
Task: "Entidades MemberAccountTermsVersion, TermDepositScheduleItem y NegotiatedRateRule"
```

## Implementation Strategy

### MVP

Setup + Foundational + **K7** (US1 en su parte de aportes, US2, US3, US10) con US8 y US9 en lo que toca a
aportes: es lo que COOFLOPAL necesita. Validar con las e2e de aportes y la consulta de la Cartera.

### Incremental Delivery

K7 → K8 (US4, US5, US8, US9, US16 completas) → K9 (US6, US7) → K10 (US11, US12, US13, US14) → K11 (US15,
US17) → Polish. Por decisión del dueño (Q1), **ninguna** se activa en producción hasta tener todas, con
Recaudo y Tesorería.

### Notes

- Se implementa por bloques con subagentes secuenciales (comparten compilación) y commit por bloque, como
  en la 013.
- `[P]` = archivos distintos sin dependencias pendientes.
- Las migraciones pares son regenerables hasta T225.
