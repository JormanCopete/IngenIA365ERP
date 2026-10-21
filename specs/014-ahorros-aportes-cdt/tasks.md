# Tasks: Cartera financiera — captaciones: ahorro a la vista, contractual y permanente, CDAT y aportes sociales

**Input**: Design documents from `/specs/014-ahorros-aportes-cdt/`
**Prerequisites**: plan.md, spec.md, research.md (§0 nombres canónicos y §2–§3 conciliación: **mandan**),
data-model.md, contracts/ (api, contrato-de-cuentas, mensajes, contabilidad, plantillas-y-archivos,
documentos), quickstart.md, investigacion-financiera.md

**Tests**: obligatorios en esta feature. La spec exige casos dorados al centavo (FR-026, SC-002, SC-019),
pruebas de arquitectura que fijan fronteras (FR-008, FR-014) y e2e en los dos motores (SC-001 a SC-019), y
la constitución exige `Domain.Tests`, `Application.Tests`, `Architecture.Tests` e `API.IntegrationTests`.

**e2e en la corrida final** (decisión del dueño, 2026-10-18): las pruebas de `TI/` contra base de datos (Testcontainers) se escriben y
compilan con su historia pero se corren **una sola vez al final de todas las fases**; cada tarea e2e se marca con esa nota hasta entonces.

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

- [x] T005 Ejecutar `specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql` en local, DEV, QA y producción y guardar el resultado en `docs/operaciones/captaciones-retiro-heredado.md` (**dueño**: con filas en cualquier tabla, la guarda se niega y el dueño decide antes de seguir) — **parcial (2026-10-18)**: corrido sólo en local (PostgreSQL 5433, `ingenia365erp`: 0 filas en las 45; `ingenia365erp_admin` no las tiene); DEV, QA y producción quedan **del dueño** con la tabla de §9 de `docs/operaciones/captaciones-retiro-heredado.md` — **resuelto el 2026-10-18 por decisión del dueño (D15, research §13)**: no hay clientes en producción; el dueño es el segundo revisor y
  autoriza, y el despliegue inserta la fila `LND.CaptacionesHeredadas.RetiroAprobado` en cada base (§9a del runbook)
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
- [x] T018 [P] Sembrar los 39 permisos de `contracts/api.md` §2 en `src/Infrastructure/IngenIA365ERP.Identity/Seed/LendingPermissionCatalogSeeder.cs` y las cuatro plantillas de rol (`cartera.jefe-captaciones`, `cartera.asesor-captaciones`, `cartera.analista-aportes`, `cartera.tesorero-liquidez`) en `A/Security/Roles/Plantillas/PerfilesSugeridos.cs` (research E07: con ellas son 17 y la activación las exige todas) — hecho: 39 códigos (162 en total) y las cuatro plantillas en `PerfilesSugeridos` (research E07: 17 plantillas, la activación de créditos exige todas); `cartera.administrador` excluye además `Lending.NegotiatedRates.Approve`; enmendadas `PermisosDeCarteraTests`, `PerfilesSugeridosDeCarteraTests`, `CreateSuggestedLendingRolesCommandTests`, `LosCodigosViewDeCarteraNoTienenDatosPersonales` y las e2e `SeguridadYHabeasDataDeCarteraTests` y `UsuariosDeCartera` (**e2e en la corrida final**). Los montos atribuibles de api.md §1.6 (`PermisosLimitables`) quedan para las historias que los usan
- [x] T019 [P] Agregar los sujetos de atribución de `research.md` C06 a `D/Lending/Attributions/LendingDecisionSubjects.cs` — hecho: nueve sujetos al final de `Todos` y la lista `DeCaptaciones`
- [x] T020 Ampliar `TR/Principles/LaCarteraNoTieneValoresLegalesFijos.cs` y `TR/Principles/ElMotorDeCarteraEsDecimal.cs` para cubrir `D/Lending/Deposits/` y `A/Lending/Deposits/` — hecho: `ElMotorDeCarteraEsDecimal` suma `A/Lending/Deposits` (`D/Lending` ya era recursiva) y `LaCarteraNoTieneValoresLegalesFijos` suma `D/Lending/Deposits` a la regla de 7 y 10, con una prueba de cobertura en cada una

### Libro de cuentas (R02–R04, R08, FR-001, FR-002, FR-011)

- [x] T021 [P] Crear `MemberAccount` (estados y transiciones de `data-model.md` §3.1) en `D/Entities/Lending/Deposits/Accounts/MemberAccount.cs` — hecho el 2026-10-18: `Abrir` (naturaleza ≠ colocación, clase sólo en captación), `PuedePasar` con el diagrama de §3.1 y las transiciones `Activar`, `Inactivar`, `Reactivar`, `Vencer`, `PedirCierre`, `RevertirCierre`, `Cerrar`; `AdmiteOperaciones` responde `Lending.Savings.AccountClosed`
- [x] T022 [P] Crear `MemberAccountParty` (§3.2) en `D/Entities/Lending/Deposits/Accounts/MemberAccountParty.cs` — hecho: `Nueva` (representante con fundamento y soporte, beneficiario con conjunto, aportes sólo titular y beneficiarios), `Terminar`, `ProporcionesCuadran`
- [x] T023 [P] Crear `MemberAccountOperation` (§4.1, hecho inmutable) en `D/Entities/Lending/Deposits/Transactions/MemberAccountOperation.cs` — hecho; sin `PayoutOrderId` (la orden nombra su operación) y sin las FK de K8/K10 (research E16); `MembershipWithdrawalId` sin FK hasta T096 (E17)
- [x] T024 [P] Crear `MemberAccountTransaction` (§4.2, bigint, tres fechas, reverso espejo, signo de §0.2) en `D/Entities/Lending/Deposits/Transactions/MemberAccountTransaction.cs` — hecho; familias del signo (`ConceptosDelTitular`, `ConceptosDeAportes`, `ConceptosDeMemoria`) y clases de causación (6 decimales); `InstallmentId` sin FK hasta T081, `TermsVersionId` nace en K9 (E16, E17)
- [x] T025 [P] Crear `MemberAccountDailyPosition` (§4.3) y `MemberAccountControl` (§4.4) en `D/Entities/Lending/Deposits/Projections/` — hecho (`[SinDiffDeAuditoria]` en los dos; la posición es hecho `bigint`)
- [x] T026 [P] Crear `AccountPayoutOrder` (§3.9) en `D/Entities/Lending/Deposits/Accounts/AccountPayoutOrder.cs` — hecho (`Emitir`, `AdmiteResultado`, `Reintegra`, `RegistrarResultado`)
- [x] T027 Crear las configuraciones EF de T021–T026 con precisión (`PrecisionDeCartera`, alias `MontoCausado` decimal(18,6)), índices y filtros en `P/Configurations/Lending/Deposits/` — hecho en `P/Configurations/Lending/Deposits/MemberAccountConfiguration.cs` y `MemberAccountLedgerConfiguration.cs`, con el alias nuevo `MontoCausado` (18,6) en `PrecisionDeCartera`; `CaptacionesAportes` regenerada en los dos motores (`20261018153813` PostgreSQL / `20261018153740` SQL Server, paridad OK); `ModeloDeCarteraNucleoTests` (139 tablas), `LosHechosInmutablesNoSeModifican` y `LosSaldosYTasasDeCarteraTienenSuPrecision` ampliadas
- [x] T028 Agregar las columnas nuevas de las 11 tablas existentes de `data-model.md` §1.8 y §8.2 (`COR_RegulatoryProfileVersions` para FR-018, `LND_MigrationLoads.Nature`, `LND_Activations.Nature`, `LND_IssuedDocumentEvents.CausingAccountOperationId`, `LND_AccountingFactParts` con disparador y conteo de cuentas, `LND_AttributionRules.MaxNegotiatedSpread`, `LND_ProcessRunErrors.MemberAccountId` y las de `LND_PayrollAgreementVersions`) con sus configuraciones en `P/Configurations/Lending/` y `P/Configurations/Core/` — hecho en las 8 tablas que nombra la tarea (`LND_Setup` es T053 y `ACC_LendingPostingRules` T044): perfil regulatorio (5), carga (`Nature`, `AccountCount`, únicos a `(Nature, Status)`), activación (`Nature`, `UK_LND_Activations_Nature` sin filtro, E18), evento de documento (`CausingAccountOperationId` con `CK_..._Cause`), partes contables (`TriggerAccountOperationId`, `AccountCount`), regla de atribución (`MaxNegotiatedSpread`), errores de corrida (`MemberAccountId` con `CK_..._Subject` y único) y versión del convenio (`UsesFileGroups`, `EmployeeIdentification`, `IncludesAccountInstallments`, formatos nulos con `CK_..._Formats`, E19)
- [x] T029 Ampliar `PrincipioXI_ContableImmutable` a `D/Entities/Lending/Deposits/Transactions/` en `TR/Principles/PrincipioXI_ContableImmutable.cs` — hecho: `Entities/Lending/Deposits/Transactions` en el espacio protegido y la prueba `El_libro_de_cuentas_esta_en_el_espacio_protegido_y_nadie_lo_borra_ni_lo_actualiza`
- [x] T030 Crear el escritor único `RegistroDeCuentas` (operación, transacciones por concepto, posición diaria, marca de agua) en `A/Lending/Deposits/Ledger/RegistroDeCuentas.cs` — hecho el 2026-10-18: control, sello monótono, operación, filas (origen único, precisión 6/2 con `Lending.Savings.AmountTooPrecise`, retención y GMF en pareja), espejo y reverso (`Lending.Savings.ReversalOfReversal`), `MarcarReversada`, `LiberarCanje`, causación de a un día y en bloque, marca de liquidación, actividad del titular (FR-058) y posición versionada desde la reconstrucción; registrado en `A/DependencyInjection.cs`. Lo que escribe en K8/K9 (liquidaciones, retención, GMF, condiciones, plan, cuotas) lo suman esas tareas; pruebas en `TA/Lending/Deposits/RegistroDeCuentasTests.cs`
- [x] T031 Agregar el nivel «cuenta» al orden fijo de `CerrojoDeCartera` (setup → persona → crédito → cuenta, por `Id`, en los dos motores) en `P/Lending/CerrojoDeCartera.cs`, `P/Lending/SqlDelCerrojoDeCartera.cs` y la interfaz `A/Lending/Common/ICerrojoDeCartera.cs` — hecho: `BloquearCuentasAsync` (paso 4, después del crédito y antes de la numeración) con `SqlDelCerrojoDeCartera.Cuentas` en los dos motores; pruebas en `CerrojoDeCarteraSqlTests`
- [x] T032 Ampliar `NumeradorDeCartera` con las clases de número de cuentas, operaciones, retiros, distribuciones y casos (`data-model.md` §1.3 con los valores 16–20 de research E03) en `A/Lending/Common/NumeradorDeCartera.cs` — hecho: `NumerarCuentaAsync` (por línea), `NumeroDeOperacionDeCuentaAsync`/`NumerosDeOperacionDeCuentaAsync`, `NumeroDeProcesoDeCaptacionesAsync` (18–20; su tabla suma la búsqueda de existentes al nacer), `NumerarOrdenDeCuentaAsync` (comparte el consecutivo de las órdenes de desembolso y salta en las dos tablas) y `LlevarCuentasPorEncimaDeAsync` (§0.4)
- [x] T033 Crear el motor puro `ReconstruccionDeCuenta` (saldos por concepto a D con información hasta R) y la fachada `MotorDeCaptaciones` en `D/Lending/Deposits/ReconstruccionDeCuenta.cs` y `D/Lending/Deposits/MotorDeCaptaciones.cs` — hecho: `ReconstruccionDeCuenta` (`Reconstruir` a (D, R), `SaldosDiarios`, `Verificar`, `CambiosPorFechaValor`, espejos validados con `Lending.Reversal.OfReversal` y `Lending.Savings.LedgerInconsistent`) y la fachada `MotorDeCaptaciones`; en K7 reconstruye sobre las filas del libro y la serie diaria es la entrada del interés de K8 (research E20)
- [x] T034 [P] Pruebas de `ReconstruccionDeCuenta` (consignación, retiro, reverso, fecha valor, información hasta R) en `TD/Lending/Deposits/ReconstruccionDeCuentaTests.cs` — hecho: 11 pruebas (consignación y retiro, reverso antes y después de R, fecha valor, información hasta R, serie diaria, familias del signo, huella, verificación, espejo de espejo, libro inconsistente)
- [x] T035 [P] Prueba de arquitectura `NadieEscribeElLibroDeCuentasFueraDelRegistro` (todas las tablas de `data-model.md` §0.7) en `TR/Principles/NadieEscribeElLibroDeCuentasFueraDelRegistro.cs` — hecho el 2026-10-18: escrituras por los conjuntos (también los de K8 y K9) o por `Set<>`, las marcas propias de la cuenta y la fila armada que no se entrega al registro; la regla de la 013 admite que `RegistroDeCuentas` mueva las marcas que comparten nombre (E23)
- [x] T036 [P] Prueba de arquitectura `LaRetencionYElGmfSoloLosCalculanSusMotores` y `LasCaptacionesNoUsanLaUsura` en `TR/Principles/LasCaptacionesRespetanSusMotores.cs` — hecho: quien arma `WithholdingApplied`/`GmfCharged` nombra su motor, los motores (K8) son estáticos y puros en `D/Lending/Deposits`, y las carpetas de captaciones no tocan la usura (E23)
- [x] T037 [P] Pruebas de arquitectura de R29 que faltan: `LasCuentasSeTocanSoloPorElContrato` (Nómina, Recaudo y Tesorería sólo por `IContratoDeCuentas`), ampliación de `LosHechosDeCarteraNoLlevanCuentas` a los hechos de captaciones (SC-009) y de `SoloElNumeradorNumera` a las clases nuevas, en `TR/Principles/` — hecho: `TR/Principles/LasCuentasSeTocanSoloPorElContrato.cs` (fuente y ArchUnit; Nómina, Tesorería y la futura carpeta de Recaudo), `LosHechosDeCarteraNoLlevanCuentas` ampliada a `Contracts/Lending/Deposits` con la cuenta del asociado admitida (SC-009) y `SoloElNumeradorNumera` con las clases 16–20
- [x] T038 [P] Prueba de arquitectura: toda ruta de `API/Endpoints/Lending/Deposits/**` declara `RequirePermission` y toda consulta y comando de `A/Lending/Deposits/` aplica `IAlcanceDeCartera` (amplía `LosEndpointsProtegidosExigenPermiso`) en `TR/Principles/LasCaptacionesExigenPermisoYAlcance.cs` — hecho: rutas de `API/Endpoints/Lending/Deposits/**` (recursivo) con `RequirePermission` de `Lending.*` y `LendingGroup.Grupo`, y toda `…Query`/`…Command` de `A/Lending/Deposits` con `IAlcanceDeCartera` o exceptuada con motivo (lista vacía)

### Puertos y frontera (R09, R20, R29, FR-008, FR-086, FR-093)

- [x] T039 Crear el puerto `IContratoDeCuentas` con sus 7 métodos, DTOs y enums de `contracts/contrato-de-cuentas.md` en `A/Common/Integration/Lending/IContratoDeCuentas.cs`, `ContratoDeCuentasDtos.cs` y `EnumsDelContratoDeCuentas.cs` — hecho el 2026-10-18: `IContratoDeCuentas` (7 métodos, `VersionDelContrato = 1`), `ContratoDeCuentasDtos.cs` (los records de §3.5–§3.11) y `EnumsDelContratoDeCuentas.cs` (seis enums; `ClaseDeRecaudador.Treasury` ya estaba, E04); `LosEnumsDelContratoNumeranComoElDominio` los fija contra el dominio
- [x] T040 Implementar `ContratoDeCuentasSinActivar` («no habilitado» sin efectos por naturaleza) y registrarlo por defecto en `A/Lending/Integration/ContratoDeCuentasSinActivar.cs` y `A/DependencyInjection.cs` — hecho en `A/Lending/Integration/ContratoDeCuentasSinActivar.cs` (lee sólo la naturaleza de la cuenta para el código, research E22) y registrado por defecto en `A/DependencyInjection.cs`; pruebas en `TA/Lending/Deposits/PuertosDeCuentasTests.cs`
- [x] T041 Mover `IConsultaDeAportes` de `A/Common/Integration/Savings/` a `A/Common/Integration/Lending/` con la versión 2 del DTO (`Minimum`, `InstallmentsInArrears`) manteniendo la 1 para quien no la adopte — hecho: `A/Common/Integration/Lending/IConsultaDeAportes.cs` con `VersionDelContrato = 2` y `Minimum`/`InstallmentsInArrears` como `init` al final (la v1 los ve en cero); la carpeta `Savings` desapareció y sus usos se mudaron
- [x] T042 Registrar `ProveedorDeCuentas` en `ContratosDeIntegracion` y sus versiones en `A/Common/Integration/ContratosDeIntegracion.cs`, y la clase `Treasury` en la validación de recaudadores de `A/Lending/Integration/Contrato/ValidadoresDelContrato.cs` — hecho: `ContratosDeIntegracion.ProveedorDeCuentas` y `VersionesDelContratoDeCuentas = [1]` en `VersionesSoportadas`; Tesorería con su clave `TES:{documento:N}:{cuenta:N}` en `GramaticaDeClaves` y sus medios (`Transfer`, `BankDeposit`) en `ClasesDeMedioDeRecaudo` (E22)
- [x] T043 Enmendar `TR/Principles/LaCarteraSoloSeTocaPorSusInterfaces.cs`: seis puertos (suma `IContratoDeCuentas`; `IConsultaDeAportes` y `IConsultasDeCartera.ProductosDeCaptacionAsync` en la lista), sin excepciones por nombre; y agregar `ProductosDeCaptacionAsync` a `IConsultasDeCartera` con su implementación real y la «no habilitada» (`contracts/contrato-de-cuentas.md` §5, FR-095) en `A/Common/Integration/Lending/IConsultasDeCartera.cs` y `A/Lending/Integration/` — hecho: seis puertos y la consulta de aportes en `LaCarteraSoloSeTocaPorSusInterfaces` (métodos, versiones, espacio, implementaciones, registro «sin activar», captaciones en las carpetas de Cartera); `ProductosDeCaptacionAsync` con sus DTO, la «no habilitada», la real (`ProductosDeCaptacionDeCartera`, en bloque) y el doble de pruebas; `InventarioNoConoceContabilidadNiCartera` admite el tercer método sin que Inventario lo use; `GetPersonDetailQuery` ya resume ahorros y aportes por la consulta (E22)

### Contabilidad base (R19, C03, C10, FR-096 a FR-100, E-03)

- [x] T044 Agregar las dimensiones 14 a 18 al final de `DimensionDeCartera` y de la clave posicional de `LendingPostingRule.ClaveDe`, con migración de datos que recalcula la clave de las filas existentes, en `D/Entities/Accounting/Lending/LendingPostingRule.cs` y el enum `DimensionDeCartera` de `A/Accounting/Lending/Reglas/RolesDeCuentaDeCartera.cs` — hecho el 2026-10-18: cinco columnas en `ACC_LendingPostingRules`, `ClaveDe` con las cinco al final (`SufijoDeCaptaciones`), presentes, propuesta, comandos y consultas; la migración de datos (`DimensionKey` + `|*|*|*|*|*`) va en `CaptacionesAportes` (T056); plantilla e importación con T144 (research E29)
- [x] T045 Agregar los 17 roles en español de `research.md` C10 y las familias `Captacion` y `Patrimonio` al final de `A/Accounting/Lending/Reglas/RolesDeCuentaDeCartera.cs` — hecho: 17 roles (43), `Captacion = 9`, `Patrimonio = 10`, `TerceroNaturalDeCartera.Titular`, `CruceDeRol.Cuenta`, plazo de emisión condicional en `DimensionesDe`, `RolDelConceptoDeCuenta`, `ConceptosDeCaptacion` y los valores de cada dimensión (research E27)
- [x] T046 [P] Sembrar los tipos de comprobante `MA`, `CC`, `TD`, `AO`, `RA` y dejar inactivos `AH` y `CD` (C17) en la semilla de tipos de comprobante y en `ACC_LendingVoucherMappings`, y el documento cruce `CTA` «Cuenta de captación o aportes» en `cross-document-types.json` — hecho: `MA`, `CC`, `TD`, `AO`, `RA` (29 tipos), `AH` y `CD` inactivos (columna `active` y la inactivación de los sembrados sin comprobantes), 30 mapeos con cruce `CTA` y dos de Nómina en `DN`, `CTA` en `cross-document-types.json` (research E28)
- [x] T047 [P] Crear los bloques comunes de mensajes (`DepositFactDimensionsV1`, `MemberAccountRefV1`, `TermDepositTermsV1`, `WithholdingDetailV1`, `GmfDetailV1`, `DepositConceptAmountV1`, `DepositFactLineV1`, `DepositFactTotalV1`) de `contracts/mensajes.md` §3 en `A/Common/Integration/Contracts/Lending/Deposits/BloquesComunes.cs` — hecho: los ocho bloques de §3
- [x] T048 Sembrar los 33 tipos de hecho de `contracts/mensajes.md` §1.1 en `LND_FactTypes` (códigos cortos, `CAV` para `CaptacionesActivadas`, research C23) y la tabla hecho × rol de `contracts/contabilidad.md` en `A/Accounting/Lending/Reglas/OperacionesDeCartera.cs`, para que cada hecho se pueda contabilizar desde la historia que lo emite — hecho: los 33 records `…V1` con su contenido de §4.1 (research E26), el catálogo de mensajes (76 de Cartera, 70 hechos), `LND_FactTypes` (70) y `OperacionesDeCartera` (70; `EntregaDelHecho` K7 = 8 … K11 = 12, research E25)
- [x] T049 Ampliar `ConsumoDeCartera` y `ResolutorDeReglasDeCartera` para aceptar hechos de captaciones con las dimensiones nuevas (sin hechos concretos todavía) y el error `Accounting.LendingRule.NatureMismatch` y `AccountFlag` según research C20 (`Standard`, `GmfExempt`, `Garnished`; la inactividad cambia el rol) en `A/Accounting/Lending/` — hecho: `ValoresDeCartera`, `MatrizVigenteDeCartera.Coincide`, `UltimoQueCoincideAsync` y la combinación del error con las cinco dimensiones; `Accounting.LendingRule.LineNatureMismatch` (research E24); la inactividad cambia el rol (`RolDelConceptoDeCuenta`); `IDimensionesDeCartera` con la naturaleza de cada línea. `ConsumoDeCartera` no necesitó cambios (deserializa por el catálogo); el constructor de captaciones es T143
- [x] T050 [P] Pruebas del resolutor con las dimensiones 14–18 y los pesos de especificidad intactos en `TA/Accounting/Lending/ResolutorConDimensionesDeCaptacionTests.cs` — hecho: 9 pruebas (clave, pesos intactos, igualdad de clase y marca, plazo de emisión, reglas de crédito sin cambio, forma condicional, naturaleza y valores, de concepto a rol, retroactividad)

### Proceso diario, cierre y activación (R10, R21, FR-031, FR-116)

- [x] T051 Agregar `PasoDeCaptaciones` (vacío, por tandas, sólo en cooperativas con aportes o captaciones activados, errores a `LND_ProcessRunErrors`) a `CicloDiarioDeCartera` en `A/Lending/Deposits/Accrual/PasoDeCaptaciones.cs` y `A/Lending/Accrual/CicloDiarioDeCartera.cs` — hecho el 2026-10-18: `PasoDeCaptaciones` (al terminar la corrida, después de la causación de los créditos; `PasosDelCiclo.Captaciones = 9`; tandas por `Id` de cuenta; subpasos `ISubpasoDeCaptaciones` de cada historia; errores con `MemberAccountId`), research E30
- [x] T052 Agregar la sección de captaciones (vacía) a `CierreMensualDeCartera` y a la guarda `LendingNotClosed` en `A/Lending/Risk/CierreMensualDeCartera.cs` — hecho: `SeccionDeCaptacionesDelCierre` en la transacción final antes del mismo `CierreDeCarteraRealizado`; `CarteraEnElCierre` cuenta `CaptacionesActivadas` para `LendingNotClosed` y avisa la carga de captaciones (research E30)
- [x] T053 Agregar las fechas de activación de aportes y de captaciones a `LendingSetup` (`data-model.md` §1.2) en `D/Entities/Lending/Configuration/LendingSetup.cs` y su configuración — hecho: seis columnas con sus FK e índices, `FechaDeActivacion`/`ActivadaEn` y el único lector `ActivacionesDeCaptacion`, que ya usa `ProductosDeCaptacionDeCartera` (pendiente 2 del bloque B)
- [x] T054 Crear los requisitos de activación por naturaleza (`LoansActivated`, `AccountsCollectorRegistered`, `PayrollConceptsReady`, `DeliveriesComplete`, `ProfileAllowsClasses`, `LinesWithMatrix`) y el bloqueo por entrega `Lending.Savings.DeliveryNotAvailable` en `A/Lending/Deposits/Activation/RequisitosDeActivacionDeCaptaciones.cs` — hecho: `RequisitosDeActivacionDeCaptaciones` (también `LoadApplied`, `DifferencesAccepted`, `DateInTerm`, `AccountingMonthOpen` y `TreasuryDestinationRegistered` de api.md §16) y `EntregaNoDisponible` (aportes K7, captaciones K8, en todo ambiente); el perfil regulatorio acepta los cinco campos de §1.8 (research E31, E32)
- [x] T055 `ActivateContributionsCommand` y `ActivateDepositsCommand` en su modo **sin carga** (FR-117, cooperativa sin historia: corte + 1, requisitos, mes abierto) que cambian el contrato y la consulta a sus implementaciones reales; con su ruta `POST /api/lending/deposits/activation` de `contracts/api.md` §16 en `API/Endpoints/Lending/Deposits/DepositsMigrationEndpoints.cs`; la fixture e2e (T003) los usa desde la fase 3 y antes siembra una matriz mínima para los hechos de la naturaleza (`LinesWithMatrix`) y registra la tesorería de prueba con `OrdenDeGiroEmitida` v2 adoptada (`TreasuryDestinationRegistered`; la v2 se declara aquí como contrato y la emite US10), y la activación con carga se completa en US9, en `A/Lending/Deposits/Activation/` — hecho: `ActivateContributionsCommand`/`ActivateDepositsCommand` sobre `ActivacionDeCaptacionesSinCarga` (fila de `LND_Activations` sin carga ni acta —`MigrationLoadId` y `SessionItemId` nulos—, `CaptacionesActivadas`, fecha en `LND_Setup`, auditoría), `POST /api/lending/deposits/activation` en `DepositsMigrationEndpoints.cs`, `OrdenDeGiroEmitidaV2` declarada y aceptada por la tesorería de prueba, y `CaptacionesE2E.CooperativaConCaptacionesAsync` (re-registra `TST` con `ProveedorDeCuentas` y `TES` con la v2 y activa las dos naturalezas; sin matriz, research E32) **e2e en la corrida final**; el cambio a las implementaciones reales lo registran T083 y T084 leyendo esa fecha (research E31, E33); pruebas en `TA/Lending/Deposits/ActivacionDeCaptacionesSinCargaTests.cs`. La activación con carga, en US9

### Migración par y pantallas base

- [x] T056 Generar la migración par `CaptacionesAportes` con las tablas fundacionales (regenerable hasta el congelamiento de la fase final) en los dos proyectos de migraciones — **iniciada** el 2026-10-18 con T012 (sólo `Nature` y sus índices); se regenera al sumar las tablas fundacionales; **regenerada** el 2026-10-18 con T021–T028 (`20261018153813` PostgreSQL / `20261018153740` SQL Server: las siete tablas del libro de cuentas y las columnas de §1.4, §1.5 y §1.8), faltan las de T053 y siguientes; **regenerada** otra vez el 2026-10-18 con T044, T053 y T055 (`20261018170720` PostgreSQL / `20261018170634` SQL Server, paridad OK) y la migración de datos de la clave de T044 a mano en los dos motores (research E34)
- [x] T057 [P] Crear el cliente tipado `CaptacionesClient` y el grupo de menú «Captaciones y aportes» (vacío hasta cada historia) en `SH/Services/Cartera/CaptacionesClient.cs` y `SH/Layout/NavMenu.razor` — hecho: `CaptacionesClient` (activación sin carga) registrado en Web, Web.Client y MAUI; el grupo «Captaciones y aportes» del menú nace oculto hasta su primer enlace (research E34)
- [x] T058 Sumar el módulo de captaciones a `ModulosMigrados` de `TR/Principles/LasPantallasDicenQueEstanCargando.cs` — hecho: `Cartera/Captaciones` en `ModulosMigrados` (ya la recorría «Cartera»; ahora por su nombre)
- [x] T059 Compilar y dejar verdes Domain, Application, Architecture, Shared y las e2e existentes de la 013 en los dos motores — hecho el 2026-10-18: `dotnet build IngenIA365ERP.slnx` 0 errores; Domain 2.371, Application 5.772, Architecture 661 y Shared 467, todas verdes. Las e2e de la 013 en los dos motores: **e2e en la corrida final** (decisión del dueño)

**Checkpoint**: lo heredado retirado, la línea tiene naturaleza, el libro, el contrato vacío, la
contabilidad y el ciclo diario existen; ninguna regresión en la 013.

## Phase 3: User Story 1 - Parametrizar líneas de captación y de aportes con vigencia (Priority: P1) 🎯 MVP (K7/K8)

**Goal**: el catálogo común de líneas admite captación y aportes con versiones inmutables con vigencia.

**Independent Test**: crear una línea a la vista con tramos y una de aportes con cuota del 3 % del ingreso,
versionar con fecha futura y consultar a una fecha anterior (quickstart §2, US1).

- [x] T060 [P] [US1] Crear `DepositLineVersion` (todos los campos de `data-model.md` §2.1) en `D/Entities/Lending/Deposits/Configuration/DepositLineVersion.cs` — hecho el 2026-10-18: hecho inmutable con `Registrar` (valida y sella `ContentSha256`), `Tramos`, `Huella` y lectores de sus listas JSON; el borrador `BorradorDeVersionDeCaptacion` vive en `D/Lending/Deposits/BorradoresDeLineaDeCaptacion.cs`
- [x] T061 [P] [US1] Crear `DepositLineRateTier` (§2.2) en `D/Entities/Lending/Deposits/Configuration/DepositLineRateTier.cs` — hecho
- [x] T062 [P] [US1] Crear `ContributionLineVersion` (§2.5) en `D/Entities/Lending/Deposits/Configuration/ContributionLineVersion.cs` — hecho, sin tasa, con `Registrar` y huella
- [x] T063 [US1] Configuraciones EF de T060–T062 en `P/Configurations/Lending/Deposits/`; `ContributionLineVersion` entra en la migración par `CaptacionesAportes` y las de captación inician la migración par `CaptacionesAhorro` — hecho el 2026-10-18 en `P/Configurations/Lending/Deposits/DepositLineConfiguration.cs`; **las tres tablas entran en `CaptacionesAportes`** (research E35: `CaptacionesAhorro` no se inicia todavía), regenerada (`20261018174431` PostgreSQL / `20261018174400` SQL Server, paridad OK, el SQL de datos de T044 conservado, el scaffold sin cambios ajenos)
- [x] T064 [US1] Invariantes de versión (sin retroactivas, clase permitida por el perfil regulatorio, tope de cuota de fondos de empleados y fracción mínima a aportes, tramos sin solape) en `D/Lending/Deposits/ReglasDeLineaDeCaptacion.cs`, con pruebas en `TD/Lending/Deposits/ReglasDeLineaDeCaptacionTests.cs` — hecho: también los bloques de cada clase (CDAT y contractual completos y sólo en su clase), la clase inmutable, la anticipación del aviso de cambio de tasa y el límite de participación; 19 pruebas
- [x] T065 [US1] Comandos `CreateDepositLineCommand`, `AddDepositLineVersionCommand`, `CreateContributionLineCommand`, `AddContributionLineVersionCommand` con validadores, sobre `CreditLine` con `Nature`, en `A/Lending/Deposits/Lines/` — hecho el 2026-10-18 (`DepositLineCommands.cs`, `ContributionLineCommands.cs`), con el único escritor `AltaDeVersionDeCaptacion`, `IContextoDeLineasDeCaptacion` (perfil, topes de aportes con su fuente, anticipación, entrega) y además `CloseLendingLineCommand` y `ReopenLendingLineCommand` de api.md §3 (reabrir también activa una línea en borrador, research E38)
- [x] T066 [US1] Consultas `ListLendingLinesQuery` (filtro `nature`) y `GetLendingLineQuery` (con versión a una fecha, (vigencia bitemporal con `VigenciaBitemporal`) en `A/Lending/Deposits/Lines/` — hecho en `LendingLineQueries.cs`: la lista por alcance y por las naturalezas cuyo `View` tiene quien pregunta; el detalle con `date` y `recordedAsOf`; también `ListLendingLineVersionsQuery`, `GetDepositLineVersionQuery` y `GetContributionLineVersionQuery`
- [x] T067 [US1] Aviso de cambio de tasa de una línea a la vista a sus titulares con la anticipación de `Captaciones.AnticipacionAvisoCambioDeTasaDias` (FR-038), por el canal configurado, en `A/Lending/Deposits/Lines/AvisoDeCambioDeTasa.cs` — hecho: anticipación = la mayor entre el parámetro y la de la línea (`Lending.Savings.RateChangeNoticeTooShort`), informativo `CAMBIOTASA` por la mensajería de la Cartera (plantilla sembrada, origen `DepositRateChange`, la cola admite al titular de una cuenta viva; research E41)
- [x] T068 [P] [US1] Pruebas de handlers y validadores (perfil sin captación, código duplicado `Catalogo.CodigoDuplicado`, versión retroactiva) en `TA/Lending/Deposits/Lines/` — hecho: `LineasDeCaptacionTests` (23) y `AvisoDeCambioDeTasaTests` (6)
- [x] T069 [US1] Rutas de `contracts/api.md` §3 en `API/Endpoints/Lending/Deposits/LinesEndpoints.cs` — **parcial (2026-10-18)**: las 12 rutas de líneas, versiones, cerrar y reabrir; las de reglas de tasa negociada (K9), cargos (K8, US4), `rate-quote` (con el motor de K8) y las plantillas (US9) llegan con sus historias (research E42)
- [x] T070 [US1] Pantalla `/cartera/lineas` con filtro de naturaleza y editores de versión de captación y de aportes (con `CampoCodigo`, `IndicadorDeCarga`, `PermissionGate`) en `SH/Pages/Cartera/Captaciones/Lineas.razor` y `SH/Pages/Cartera/Captaciones/EditorDeLineaDeCaptacion.razor` — hecho el 2026-10-18: `/cartera/lineas` (que ya era la de crédito) es el catálogo común con `?naturaleza=credito|captacion|aportes`; lo de captación y aportes vive en el componente `SH/Pages/Cartera/Captaciones/LineasDeCaptacion.razor` (no `Lineas.razor` con `@page`: dos páginas no comparten ruta, research E43) con el editor `EditorDeLineaDeCaptacion.razor`; `CaptacionesClient` suma las rutas de líneas; el grupo de menú «Captaciones y aportes» se muestra con «Líneas de captación» y «Líneas de aportes»
- [x] T071 [US1] e2e de US1 (escenarios 1 a 4 de la spec) en `TI/Lending/Deposits/LineasDeCaptacionTests.cs` — escrita y compilada el 2026-10-18; **e2e en la corrida final** (exige K8 desplegado)

**Checkpoint**: líneas de captación y aportes configurables; las de crédito intactas.

---

## Phase 4: User Story 2 - Abrir cuentas con titulares, firmantes y beneficiarios (Priority: P1) (K7/K8)

**Goal**: apertura en tres pasos con validaciones, titularidad, menores, beneficiarios y exención de GMF.

**Independent Test**: cuenta conjunta con beneficiarios 60/40, marca de exención y segunda marca rechazada;
cuenta de un menor que pasa al titular a los 18 años (quickstart §2, US2).

- [x] T072 [P] [US2] Crear `GmfExemptAccount` (§3.8, una vigente por persona) en `D/Entities/Lending/Deposits/Accounts/GmfExemptAccount.cs` y su configuración — hecho el 2026-10-18: `Marcar` (sólo ahorro: no CDAT ni aportes, `Lending.Gmf.AccountNotSavings`), `Terminar` (admite la víspera de su inicio: reemplazada el mismo día), configuración en `P/Configurations/Lending/Deposits/MemberAccountConfiguration.cs` con `UK_LND_GmfExemptAccounts_LivePerson`; entra en `CaptacionesAportes` regenerada (`20261018184016` PostgreSQL / `20261018183933` SQL Server, paridad OK, SQL de T044 conservado, sin cambios ajenos; research E53)
- [x] T073 [US2] Comando `OpenMemberAccountCommand` (asociado vigente si lo exige el perfil o la línea, conocimiento al día, habeas data, listas restrictivas con retención para el oficial, origen de fondos sobre umbral, número por `NumeradorDeCartera`, estado `Opening`/`Active`) en `A/Lending/Deposits/Accounts/OpenMemberAccountCommand.cs` — hecho el 2026-10-18, con `ApproveMemberAccountOpeningCommand` (segregación radicar × decidir; sólo con las coincidencias liberadas por el oficial), las consultas por alcance (`MemberAccountQueries.cs`), `LectorDeCuentas`, `TitularidadDeCuentas` y las reglas puras `D/Lending/Deposits/ReglasDeTitularidad.cs`; el conocimiento del asociado son los datos mínimos de la persona (SARLAFT es feature propia), las listas usan `ScreeningContext.AccountOpening` y el número es `{código de la línea}-NNNNNN` (research E44–E47, E52)
- [x] T074 [US2] Comandos de participantes: cotitulares con proporción, manejo indistinto o conjunto, firmantes con límite, representante legal con soporte (adjunto) y beneficiarios que suman 100 %, con vigencia, en `A/Lending/Deposits/Accounts/Parties/` — hecho: `SetMemberAccountPartiesCommand` (conjunto completo; lo que no cambia sigue, lo demás se cierra la víspera y nace hoy; los beneficiarios cambian en conjunto con la constancia, `Lending.Savings.BeneficiaryRecordRequired`) y `ConfirmMemberAccountDataUpdateCommand` (api.md lo ponía en K8; nace aquí como salida de la marca de la mayoría de edad); soportes como adjuntos del titular `LendingAccountPartySupport` (research E47, E49, E50)
- [x] T075 [US2] Comandos `MarkGmfExemptAccountCommand` y `UnmarkGmfExemptAccountCommand` con la política vigente (`Lending.Gmf.AlreadyMarked`) en `A/Lending/Deposits/Accounts/GmfExemption/` — hecho: `MarkGmfExemptAccountCommand` (con `replaceCurrent` para cambiar la marca en el mismo comando), `EndGmfExemptAccountCommand` (el nombre del contrato, api.md §10) y `ListGmfExemptAccountsQuery`; la política `PerHolderLimit` o `NotApplicable` responde `Lending.Gmf.PolicyWithoutMark` (research E48)
- [x] T076 [US2] Mayoría de edad en `PasoDeCaptaciones`: cierra la vigencia del representante, marca `RequiresDataUpdate` y avisa al titular y al representante (R22) en `A/Lending/Deposits/Accrual/MayoriaDeEdad.cs` — hecho: el primer `ISubpasoDeCaptaciones` registrado; cierra al representante la víspera del cumpleaños, marca `RequiresDataUpdate` desde el cumpleaños y avisa con la plantilla nueva `MAYOREDAD` (la semilla pasa de 31 a 34 filas; la cola admite al representante vigente); los retiros del representante en espera llegan con K8 (research E49)
- [x] T077 [P] [US2] Pruebas de apertura, participantes, menores y exención (incluida concurrencia del número) en `TA/Lending/Deposits/Accounts/` — hecho: `AperturaDeCuentasTests` (19) y `PartesYExencionTests` (11) sobre `CuentasDePrueba`, más `TD/Lending/Deposits/ReglasDeTitularidadTests` (8); el número consecutivo por línea se prueba aquí y la concurrencia real (cerrojo de la base) en la e2e
- [x] T078 [US2] Rutas de `contracts/api.md` §4 (apertura, participantes, cuenta) y §10 (exención) en `API/Endpoints/Lending/Deposits/MemberAccountsEndpoints.cs` y `GmfExemptionsEndpoints.cs` — hecho: 8 rutas de cuentas (lista, conteos, detalle, apertura, aprobar apertura, partes GET/PUT, actualización de datos) y 3 de exención (lista, marcar, terminar); `/gmf-exemptions/usage` espera el GMF de K8 (research E51)
- [x] T079 [US2] Pantallas `/cartera/ahorros` (lista y apertura con `PersonSearchPicker`) y `/cartera/ahorros/{id}` (ficha con participantes) en `SH/Pages/Cartera/Captaciones/CuentasDeAhorro.razor` y `DetalleDeCuenta.razor` — hecho, con el componente `EditorDeTitularidad.razor`, `PersonaDialog` para la persona nueva, `SubirSoporte` para los soportes del titular, el cliente y los DTO en `SH/Services/Cartera/` y el enlace «Cuentas de ahorro y aportes» en el grupo de captaciones
- [x] T080 [US2] e2e de US2 (escenarios 1 a 5 y el menor que cumple 18) en `TI/Lending/Deposits/AperturaDeCuentasTests.cs` — escrita y compilada el 2026-10-18; **e2e en la corrida final** (exige K8 desplegado)

**Checkpoint**: cuentas abiertas con titularidad completa.

---

## Phase 5: User Story 3 - Aportes: cuota periódica, recaudo y consulta para la Cartera (Priority: P1) (K7)

**Goal**: cuotas exigibles, recaudo por contrato, nómina y pagadurías, y consulta de aportes real.

**Independent Test**: cuota de enero pagada por el recaudador de prueba; la consulta de aportes devuelve lo
mismo que el extracto; pagaduría con dos grupos de archivo y concepto agrupado (quickstart §2 US3 y §3).

- [x] T081 [P] [US3] Crear `AccountInstallment` (§3.5) y `AccountInstallmentNonDeduction` (§3.7) en `D/Entities/Lending/Deposits/Accounts/` y sus configuraciones — hecho (E55).
- [x] T082 [US3] Generación de cuotas exigibles de aportes y permanente en `PasoDeCaptaciones` (valor fijo, % del ingreso o fracción del SMMLV, reparto con permanente) en `A/Lending/Deposits/Contributions/GeneradorDeCuotas.cs` — hecho; ingreso = `COR_Associates.ExternalSalary` (E54).
- [x] T083 [US3] Implementar `ContratoDeCuentasDeCartera` para aportes: `ListarExigiblesAsync` y `ConsignarAsync` (cuotas, extraordinarios, límite de participación, saldo a favor según la línea) con auditoría y validador, en `A/Lending/Integration/ContratoDeCuentasDeCartera.cs` — hecho; ahorro libre, retiros y traslados → K8; sobrepago sin extraordinarios se rechaza (E56).
- [x] T084 [US3] Implementar `ConsultaDeAportesDeCartera` v2 y registrarla para cooperativas con aportes activados; `ContextoDeAportes` reemplaza la bandera `RigeD19` por los parámetros `Riesgo.ExcepcionAportesArrastre` y `Riesgo.DescuentoAportesDeterioro` sembrados en T017 (R20) en `A/Lending/Integration/ConsultaDeAportesDeCartera.cs` y `D/Lending/Risk/ContextoDeAportes.cs` — hecho; la pérdida esperada no descuenta aportes (E62, pregunta para la contadora).
- [x] T085 [US3] Hechos `AporteRecibido` y su reverso (`contracts/mensajes.md` §4) emitidos en la transacción en `A/Common/Integration/Contracts/Lending/Deposits/` y `A/Lending/Deposits/Contributions/` — hecho.
- [x] T086 [US3] Devolución de aportes sin retiro con validador (`RequestContributionRefundCommand`: sólo exceso sobre el límite o amortización, nunca el mínimo no reducible ni con créditos vivos, FR-042, FR-044) y cesión entre asociados (`RequestContributionAssignmentCommand`, sólo si los estatutos la permiten, FR-048) con aprobación, en `A/Lending/Deposits/Contributions/` — hecho; tabla `LND_ContributionRequests`, devolución sólo por orden de giro en K7, `reject` sumado (E58).
- [x] T087 [P] [US3] Crear `IssuedDocumentAccount` (`LND_IssuedDocumentAccounts`) y los generadores QuestPDF de constancia y paz y salvo de aportes (`ContributionCertificate`, `ContributionClearance`, `contracts/documentos.md`) con anulación por reverso, en `D/Entities/Lending/Documents/`, `API/Reports/Captaciones/` y `A/Lending/Deposits/Documents/` — hecho; enlace sin `TermsVersionId` hasta K9; firmeza por fecha valor (E60).
- [x] T088 [P] [US3] Vistas `contributions-by-member` e `installments-in-arrears` del centro de informes en `A/Lending/Reports/VistaDeInformeDeCartera.cs` y `A/Lending/Deposits/Reports/` — hecho.
- [x] T089 [US3] Nómina (E-02): conceptos `DESC_APORTES` y `DESC_AHORROS`, tabla `PAY_PayrollAccountDeductions` y aplicación por `ConsignarAsync` al aprobar la corrida, reverso con la reversa de la corrida, en `A/Payroll/` y `P/Configurations/Payroll/` — hecho; prelación fija aportes → créditos → ahorros; la definitiva no recauda cuentas en K7; NOM adopta `ProveedorDeCuentas` en la migración (E59).
- [x] T090 [US3] Pagadurías (FR-125, E-05): `PayrollAgreementFileGroup` y `PayrollAgreementConceptEquivalence` (`data-model.md` §1.4) con configuraciones, en `D/Entities/Lending/Payroll/` (extienden el convenio de K2; prerrequisito C22) — hecho; fijar grupos o equivalencias registra una versión nueva del convenio (E61).
- [x] T091 [US3] Generación por grupo de archivo con el formato del grupo y `DescuentosPorPagaduria` v2; `FlatFileReader` para la respuesta con equivalencias y reparto (`Priority`/`Proportional`), aplicación por los dos contratos en la misma unidad de trabajo y «no aplicados», en `A/Common/BankFiles/FlatFileReader.cs` y `A/Lending/Payroll/` — **parcial**: el motor puro de equivalencias y reparto (`EquivalenciasDePagaduria`, `RepartoDePagaduria`) está hecho y probado; falta la generación por grupo, `DescuentosPorPagaduria` v2, la lectura de la respuesta con equivalencias y la aplicación por los dos contratos (una cuenta no tiene vínculo con un convenio: dato de diseño pendiente). Generar con grupos responde `Lending.PayrollFile.FileGroupsPending` (E61). **Terminado el 2026-10-18 (decisión del dueño D36,
  research §13)**: la cuenta va al archivo de la pagaduría que es la empresa empleadora de la ficha del asociado; archivo por grupo
  (`GeneratePayrollDeductionFileCommand.FileGroupCode`, formato del grupo, `LineasDeGrupoDeDescuento`), `DescuentosPorPagaduria` v2,
  `ResultadoDeDescuentos` v2 repartido por `RepartoDeRespuestaDePagaduria` (créditos por el camino de la v1, cuotas de cuentas a
  `LND_PayrollDeductionAccountResults` y su no descontado por `RegistroDeCuentas`, «no aplicados» en `LND_PayrollDeductionUnmatchedLines`),
  el giro de cuentas por `ConsignarAsync` con `DeductionFileLinePublicId` (`PendientesDeGiroDeCuentas`) y el doble de Recaudo para la v2;
  `FileGroupsPending` se retiró. Pruebas `ArchivoPorGrupoTests`; e2e `PagaduriasTests` con los pasos 3 y 4 (escrita, sin correr).
- [x] T092 [P] [US3] Pruebas de cuotas, contrato de aportes, consulta v2, nómina y reparto de pagaduría en `TA/Lending/Deposits/Contributions/` y `TA/Lending/Payroll/PagaduriasConfigurablesTests.cs` — hecho (`AportesTests`, `ConsultasDeAportesTests`, `RecaudoDeCuentasPorNominaTests`, `PagaduriasConfigurablesTests`, `EquivalenciasYRepartoDePagaduriaTests`).
- [x] T093 [US3] Rutas de `contracts/api.md` §7 y §18.3 en `API/Endpoints/Lending/Deposits/ContributionsEndpoints.cs` y la ruta de Nómina `/api/payroll/runs/{runId}/account-deductions` — hecho; más el recaudador de prueba de cuentas (§18.4: installments, deposits, reverse) para las e2e (E63).
- [x] T094 [US3] Pantallas `/cartera/aportes` y `/cartera/aportes/{id}` y la configuración de grupos y equivalencias en la pantalla de convenios de pagaduría, en `SH/Pages/Cartera/Captaciones/Aportes.razor`, `DetalleDeAportes.razor` y la página de convenios de K2 — hecho (E63).
- [x] T095 [US3] e2e de US3 (escenarios 1 a 4) y de pagadurías (quickstart §3) en `TI/Lending/Deposits/AportesTests.cs` y `TI/Lending/Deposits/PagaduriasTests.cs` — escritas y compilan; **e2e en la corrida final**. `PagaduriasTests` cubre la configuración, el archivo por grupo y los pasos 3 y 4 del quickstart §3 (T091, 2026-10-18).

**Checkpoint**: aportes recaudados por todas las vías; la Cartera ya no usa la plantilla de saldos.

---

## Phase 6: User Story 10 - Retiro del asociado: cruce con la deuda y devolución (Priority: P1) (K7)

**Goal**: el trámite completo del retiro vive en la Cartera.

**Independent Test**: 3.000.000 de aportes, 1.000.000 de ahorro y un crédito de 2.500.000: cruce, devolución
de 1.500.000 y hechos (quickstart §2, US10).

- [x] T096 [P] [US10] Crear `MembershipWithdrawal` (estados de `data-model.md` §6.1) y `MembershipWithdrawalLine` (§6.2) en `D/Entities/Lending/Deposits/Processes/` y sus configuraciones — hecho el 2026-10-18 (con `Radicar`, `Desistir`, `Aprobar`, `Rechazar`, `Liquidar`, `PorRehacer`, `Pagar`; líneas como hecho), con las FK que esperaban (E17) y `CaptacionesAportes` regenerada (`20261018213853` / `20261018213822`, paridad OK; research E65, E77)
- [x] T097 [US10] Comandos `FileMembershipWithdrawalCommand`, `DesistMembershipWithdrawalCommand` y la aprobación como `SessionItem` de un órgano con acta (sin que quien radica apruebe) en `A/Lending/Deposits/Withdrawals/` — hecho: `DecideMembershipWithdrawalCommand` (aprobar o negar con el punto de una sesión cerrada; segregación radicar × decidir) y el asunto de acta `AsuntoDeRetiroDeAsociado` (research E70)
- [x] T098 [US10] Al aprobar, emitir `RetiroDeAsociadoAprobadoV1`; crear el destino `Associates` con `DestinoAsociados`, participante `ASO` y recibo `COR_AssociateInboundMessages`, y que Asociados cambie el estado (E-04) en `A/Common/Integration/Contracts/Lending/Deposits/`, `A/Core/Associates/` y `P/Configurations/Core/` — hecho: `ConsumeMembershipWithdrawalApprovedCommand` (estado `R` y fecha, recibo único, `Associate.WithdrawalDateMismatch`), `IntegrationParticipantClass.Associates = 9` y el participante `ASO` sembrado a mano en la migración (research E74)
- [x] T099 [US10] Liquidación en K7: aportes por clase, saldos de ahorro sin intereses pendientes (los intereses a la fecha los suma la tarea de US5 y el CDAT la de US6), créditos vivos, pérdidas por parámetro y acta (hecho `PerdidaAplicadaAlRetiro`), mínimo irreducible de la entidad, con validador, en `A/Lending/Deposits/Withdrawals/SettleMembershipWithdrawalCommand.cs` — hecho: el motor puro `D/Lending/Deposits/LiquidacionDeRetiro.cs`, `PlanesDeLiquidacionDeRetiro` con la huella, la vista previa y la liquidación; pérdidas con `{ sessionItemPublicId, year, proportion }` y la alerta `Cartera.DevolucionRetenidaPorMinimo` (research E66–E68, E71)
- [x] T100 [US10] Cruce interno con la aplicación de pagos del crédito (`InternalOffset`, prelación de los estatutos, cerrojos crédito → cuenta, una sola operación); reverso del pago que arrastra el débito y deja `ToRedo`; hecho `CruceAlRetiroAplicado`, en `A/Lending/Deposits/Withdrawals/CruceDeRetiro.cs` — hecho: una operación de cuenta por crédito y cuenta, todo o nada; el arrastre por el enganche `IArrastreDeReversoDePago` de `ReversionDePago` (research E67, E69)
- [x] T101 [US10] Devolución por `AccountPayoutOrder` y `OrdenDeGiroEmitida` v2 en el plazo de `Captaciones.DiasPlazoDevolucionAportes` (hechos `DevolucionOrdenada` y `DevolucionGirada`); resultado de Tesorería que reintegra si se rechaza, en `A/Lending/Deposits/Operations/OrdenesDeGiroDeCuentas.cs` — hecho: una orden por cuenta, `ConsumeTreasuryResultCommand` para las órdenes de cuentas (también las de T086), `Paid` y cuentas canceladas al girar todo, `refund/approve` para lo retenido o reintegrado (research E71, E72)
- [x] T102 [US10] Enmienda E-04: Asociados deja de registrar retiros por su cuenta (`Associate.WithdrawalOwnedByLending`) y la baja de persona consulta productos vivos (`Person.HasLiveDepositProducts`, `ProductosDeCaptacionAsync`) y el reingreso abre cuentas nuevas conservando las anteriores (FR-085) en `A/Core/Associates/`, `A/Core/People/` y `A/Lending/Deposits/Accounts/` — hecho; además, con un retiro aprobado, liquidado o por rehacer no se abren cuentas (research E75)
- [x] T103 [P] [US10] Documento de liquidación de retiro (`MembershipWithdrawalSettlement`, `contracts/documentos.md`) en `API/Reports/Captaciones/` y `A/Lending/Deposits/Documents/` — hecho: `DocumentosDeRetiro`, el cuerpo `CuerpoDeLiquidacionDeRetiro` pintado en `DocumentosDeCarteraReport` (no `API/Reports/Captaciones`) y emitido al liquidar (research E73)
- [x] T104 [P] [US10] Pruebas de trámite, liquidación, cruce, pérdidas, mínimo irreducible y reverso en `TA/Lending/Deposits/Withdrawals/` — hecho: `TA/Lending/Deposits/Withdrawals/RetiroDeAsociadoTests.cs` (13) y `TD/Lending/Deposits/LiquidacionDeRetiroTests.cs` (7)
- [x] T105 [US10] Rutas de `contracts/api.md` §11 en `API/Endpoints/Lending/Deposits/MembershipWithdrawalsEndpoints.cs` — hecho: las nueve rutas
- [x] T106 [US10] Pantallas `/cartera/retiros-asociado` y `/cartera/retiros-asociado/{id}` en `SH/Pages/Cartera/Captaciones/RetirosDeAsociado.razor` y `DetalleDeRetiro.razor` — hecho, con el componente `TablaDeLineasDeRetiro.razor`, los DTO de `SH/Services/Cartera/CarteraDtos.Retiros.cs`, el cliente y el enlace del menú (research E76)
- [x] T107 [US10] e2e de US10 (escenarios 1 a 4) en `TI/Lending/Deposits/RetiroDeAsociadoTests.cs` — escrita y compilada el 2026-10-18; **e2e en la corrida final**

**Checkpoint**: K7 funcional de punta a punta salvo migración y contabilidad completa.

---

## Phase 7: User Story 4 - Consignar, retirar y trasladar por el contrato de la Cartera (Priority: P1) (K8)

**Goal**: todas las operaciones de depósito por el contrato, con GMF, canje, atribuciones, cargos y débito
automático.

**Independent Test**: consignar 1.000.000, retirar 400.000 de cuenta exenta y no exenta, repetir la clave y
retirar sobre la atribución (quickstart §2, US4); casos dorados de GMF.

- [x] T108 [P] [US4] Motor puro `GravamenMovimientosFinancieros` (política, tope en UVT, hechos generadores, tarifa cero o desactivado desde una fecha) en `D/Lending/Deposits/GravamenMovimientosFinancieros.cs`
- [x] T109 [P] [US4] Casos dorados de GMF (tope cruzado 6.676; redención de CDAT abonada 41.952; traslado mismo titular; reverso; tarifa cero desde una fecha) en `TD/Lending/Deposits/Casos/gmf-*.json` — seis casos `gmf-*` verificados por `calcular.py` (E81)
- [x] T110 [P] [US4] Crear `GmfMonthlyUsage` (§4.7) en `D/Entities/Lending/Deposits/Transactions/GmfMonthlyUsage.cs` y su configuración
- [x] T111 [US4] Completar `ContratoDeCuentasDeCartera`: `ConsultarAsync`, `SimularAsync`, `RetirarAsync`, `TrasladarAsync`, `ReversarAsync` (firmas y manejo, disponible, mínimo, máximos, efectivo, atribución con monto, GMF enlazado, canje por días hábiles de la clase de medio, sobregiro técnico con bloqueo y alerta, marca de movimiento del titular por tipo de transacción FR-058, el disponible que muestra `ConsultarAsync` es el mismo que valida el retiro SC-007) en `A/Lending/Integration/ContratoDeCuentasDeCartera.cs` — `CloseAccount` responde `DeliveryNotAvailable` hasta T135; bloqueos y embargos en cero hasta US11 (E78–E80)
- [x] T112 [US4] Mensaje `ConsignacionReversadaPorCartera` v1 y hechos de consignación, retiro, traslado, GMF y `OperacionDeCuentaReversada` en `A/Common/Integration/Contracts/Lending/Deposits/`
- [x] T113 [US4] Recaudador `Treasury` (transferencias recibidas) y dobles de prueba `/api/lending/test-doubles/collector/accounts/*` sólo fuera de producción en `A/Lending/Integration/` y `API/Endpoints/Lending/Deposits/TestDoublesEndpoints.cs` — **parcial**: dobles de cuentas hechos; el recaudador `Treasury` no tiene doble propio (E89). **Terminado el 2026-10-18** (decisión del dueño D52, research §13): doble `TES`
  de clase `Treasury` con `/collector/treasury/accounts/*`
- [x] T114 [P] [US4] Crear `DepositLineCharge` (§2.3), `AccountCharge` (§4.8) y `AccountChargeExemption` (§4.9) con configuraciones en `D/Entities/Lending/Deposits/`
- [x] T115 [P] [US4] Motor puro `CargosDeCuenta` (gratuitos, exoneraciones, valor fijo o porcentaje con mínimo y máximo) en `D/Lending/Deposits/CargosDeCuenta.cs` con casos dorados en `TD/Lending/Deposits/Casos/cargo-*.json` — seis casos `cargo-*` (E83)
- [x] T116 [US4] Causación de cargos (periódicos en `PasoDeCaptaciones`, por evento en el contrato), pendiente si no alcanza, condonación con aprobación, cobro del pendiente con la siguiente consignación (hecho `CargoPendienteCobrado`), y mensajes `CargoDeCaptacionCausado`/`Reversado` a Comercial (`CAR → Inventory`) con la respuesta `FacturaDeCargoRegistrada` en `A/Lending/Deposits/Charges/`
- [x] T117 [US4] Comercial: consumir `CargoDeCaptacionCausado` y `…Reversado` para facturar el cargo gravado y su nota crédito, en el módulo comercial de la 012 (`A/Inventory/Sales/` o donde viva la facturación) — **parcial**: los mensajes salen y esperan en la bandeja; no hay consumidor en Comercial (pregunta para el dueño, E84). `FacturaDeCargoRegistrada` sí se consume. **Terminado el 2026-10-18**
  (decisión del dueño D51, research §13): `ConsumeDepositChargeMessageCommand` deja lista la factura de venta del servicio o su nota crédito
- [x] T118 [P] [US4] Crear `DirectDebitMandate` (§3.6) en `D/Entities/Lending/Deposits/Accounts/DirectDebitMandate.cs` y su configuración
- [x] T119 [US4] Débito automático en `PasoDeCaptaciones`: operación interna que debita la cuenta y aplica el pago del crédito (`InternalOffset`, `OwnLoanPayoff`, clave `CAR:debito-automatico:…`), y `PagoAplicado.AdmiteProceso = true` sólo para `OwnLoanPayoff` en `A/Lending/Deposits/Operations/DebitoAutomatico.cs`
- [x] T120 [US4] Al cumplir el titular 18 años, anular los retiros del representante en espera de aprobación con aviso (FR-033) en `A/Lending/Deposits/Accrual/MayoriaDeEdad.cs` — anula la solicitud de retiro en espera; el aviso es el de la mayoría de edad (E88)
- [x] T121 [P] [US4] Vistas `deposit-balances`, `deposit-transactions`, `gmf` y `charges` del centro de informes en `A/Lending/Deposits/Reports/`
- [x] T122 [P] [US4] Pruebas del contrato (todas las validaciones, idempotencia, canje, sobregiro técnico), cargos y débito automático en `TA/Lending/Deposits/Operations/` — `MovimientosDeCuentaTests` (11)
- [x] T123 [US4] Rutas de `contracts/api.md` §4 (operaciones y cargos) y §9 en `API/Endpoints/Lending/Deposits/MemberAccountsEndpoints.cs` y `DirectDebitsEndpoints.cs` — las de operaciones y cargos en `MemberAccountOperationsEndpoints.cs` (E90)
- [x] T124 [US4] Pantalla de movimientos, cargos y débito automático en la ficha de cuenta `SH/Pages/Cartera/Captaciones/DetalleDeCuenta.razor` y `MovimientosDeCuenta.razor` — componente `MovimientosDeCuenta` en la ficha; el editor de la línea aún no edita cargos (E90)
- [x] T125 [US4] e2e de US4 (escenarios 1 a 6), cargos (quickstart) y débito automático en `TI/Lending/Deposits/MovimientosTests.cs` y `CargosYDebitoAutomaticoTests.cs` — escritas y compiladas el 2026-10-18; **e2e en la corrida final**
- [x] T126 [US4] Prueba de carga del contrato con reintentos de la misma clave: cero operaciones duplicadas (`RUN_PERF_TESTS=1`, SC-005) en `TI/Lending/Deposits/ContratoDeCuentasCargaTests.cs` — escrita y compilada (`RUN_PERF_TESTS=1`); **e2e en la corrida final**

**Checkpoint**: el dinero entra y sale sólo por el contrato.

---

## Phase 8: User Story 5 - Causar y liquidar intereses con retención en la fuente (Priority: P1) (K8)

**Goal**: causación diaria y liquidación reproducibles, con retención según la norma.

**Independent Test**: casos dorados de interés y retención; liquidar dos veces el mismo período no duplica.

- [x] T127 [P] [US5] Motor puro `InteresDeCaptacion` (saldo diario, promedio, mínimo; tramos `WholeBalance`/`Stepped`; mínimo para interés; tasa variable) sobre `EquivalenciaDeTasas`, `ContadorDeDias` y `Causacion` en `D/Lending/Deposits/InteresDeCaptacion.cs` — hecho el 2026-10-18: saldo diario, promedio (interés simple sobre el promedio de lo corrido) y mínimo (tasa del período compuesta sobre el mínimo de lo corrido), tramos, mínimo para interés, suspensión y tasa por día; período de liquidación con `SettlementDay` (research E93)
- [x] T128 [P] [US5] Motor puro `RetencionSobreRendimientos` (perfil, umbral diario sobre el total, alcance del umbral, componente inflacionario, tarifa por clase) en `D/Lending/Deposits/RetencionSobreRendimientos.cs` — hecho: perfil, umbral diario por cuenta sobre el total, alcance, componente inflacionario y tarifa, con el redondeo como parámetro `Retencion.Redondeo` (research E94)
- [x] T129 [P] [US5] Casos dorados: 6.884 por saldo diario, 3.649 por saldo mínimo, tramos escalonados y por total, cambio de tasa a mitad de mes, bisiesto, umbral alcanzado y no, con y sin componente inflacionario, persona exenta, en `TD/Lending/Deposits/Casos/interes-*.json` y `retencion-*.json` — hecho: 9 casos `interes-*` (6.884 diario y promedio, 3.649 mínimo, tramos, cambio de tasa, bisiesto, mínimo para interés, fecha valor) y 8 `retencion-*` (8.702 y 19.524 del CDAT de la investigación, umbral sí y no, persona exenta, autorretenedor), todos verificados por `calcular.py` (research E105)
- [x] T130 [P] [US5] Crear `InterestSettlement` (§4.5) y `WithholdingAccumulation` (§4.6) en `D/Entities/Lending/Deposits/Transactions/` y sus configuraciones — hecho, con `CaptacionesAportes` regenerada (`20261019030947` / `20261019030916`, paridad OK, SQL a mano conservado; research E104)
- [x] T131 [US5] Causación diaria en `PasoDeCaptaciones` por diferencia de acumulados con marca de agua, puesta al día en orden y alerta de atraso, suspensión de causación en cuentas embargadas totalmente o de titular fallecido según la línea (FR-065), en `A/Lending/Deposits/Accrual/CausacionDeCaptaciones.cs` — hecho: subpaso `CausacionDeCaptaciones` (último del paso, después del débito automático), corrección del proceso por parte `AJC-`, alerta `Cartera.CausacionDeCaptacionesAtrasada`; el embargo total se toma de la marca de la cuenta hasta US11 (research E95, E101)
- [x] T132 [US5] Liquidación idempotente por cuenta y período (abono neto, retención antes de capitalizar, ajuste de redondeo, acumulado por titular y año) en `A/Lending/Deposits/Accrual/LiquidacionDeIntereses.cs` — hecho: `LiquidacionDeIntereses` y el subpaso `LiquidacionPeriodicaDeIntereses`; la liquidación no se reversa desde la pantalla (research E98)
- [x] T133 [US5] Ajuste de causación por fecha valor enlazado y simulación sin efectos (`SimulateInterestSettlementQuery`) en `A/Lending/Deposits/Accrual/` — hecho: `AjusteDeCausacionDeCuenta` enlazado a consignaciones, retiros, traslados y reversos; `SimulateInterestSettlementQuery`. Una fecha valor en un período ya liquidado no lo reliquida (research E96, E97; pregunta al dueño)
- [x] T134 [US5] Hechos `InteresDeCaptacionCausado` (por lotes resumido, partes `CCA-`), `AjusteDeCausacionDeCaptacion`, `InteresDeCaptacionLiquidado` y `RetencionPracticada` en `A/Common/Integration/Contracts/Lending/Deposits/` y `A/Lending/Deposits/Accrual/PartesDeHechosDeCaptaciones.cs` — hecho: partes `CCA-` y `AJC-` (`PartesDeHechosDeCaptaciones`), `ALD-` en la puesta al día de una operación; la liquidación emite una unidad por operación con `InteresDeCaptacionLiquidado` y `RetencionPracticada` (research E95, E98)
- [x] T135 [US5] Cancelación de cuenta con validador (`CloseMemberAccountCommand` y `/close-preview`: liquida a la fecha, retiene, cobra GMF, entrega o traslada el saldo; no con bloqueos, embargos o canje, FR-039) en `A/Lending/Deposits/Accounts/CloseMemberAccountCommand.cs` — hecho: sólo ahorro a la vista (aportes y permanente se cancelan con el retiro del asociado; CDAT y contractual, K9); con cargos por cobrar se bloquea (pregunta al dueño); la cancelación por el contrato (`closeAccount`) sigue para Recaudo (research E99)
- [x] T136 [US5] Sumar los ahorros con intereses liquidados a la fecha a la liquidación del retiro del asociado (completa T099) en `A/Lending/Deposits/Withdrawals/SettleMembershipWithdrawalCommand.cs` — hecho: la vista previa suma el neto a la fecha y la liquidación liquida los ahorros antes de cruzar (`LiquidacionALaFecha`; research E100)
- [x] T137 [P] [US5] Vista `interest-and-withholding` del centro de informes en `A/Lending/Deposits/Reports/` — hecho (`InterestReportQueries.cs`)
- [x] T138 [P] [US5] Pruebas de causación, liquidación, fecha valor y simulación en `TA/Lending/Deposits/Accrual/` — hecho: `TA/Lending/Deposits/Accrual/InteresesDeCuentaTests.cs` (13) y la del retiro con intereses en `RetiroDeAsociadoTests`
- [x] T139 [US5] Rutas de liquidación y simulación (`contracts/api.md` §4) y la vista de intereses en la ficha de cuenta en `API/Endpoints/Lending/Deposits/MemberAccountsEndpoints.cs` y `SH/Pages/Cartera/Captaciones/InteresesDeCuenta.razor` — hecho: cinco rutas en `MemberAccountsEndpoints.cs`; `InteresesDeCuenta.razor` es un componente de la ficha, sin ruta propia (research E103)
- [x] T140 [US5] e2e de US5 (escenarios 1 a 5) en `TI/Lending/Deposits/InteresesTests.cs` — escrita y compilada; **e2e en la corrida final**
- [x] T141 [US5] Prueba de volumen de causación de 100.000 cuentas en menos de 30 minutos (`RUN_PERF_TESTS=1`, SC-006) en `TI/Lending/Deposits/CausacionDeCaptacionesPerformanceTests.cs` — escrita y compilada (`RUN_PERF_TESTS=1`); **e2e en la corrida final**

**Checkpoint**: intereses y retención correctos al centavo. Construida el 2026-10-18 (research §14); también el arreglo de D44 (el correo del reverso
tardío sale después del commit, research E92).

---

## Phase 9: User Story 8 - Contabilidad recibe cada hecho por la matriz de la Cartera (Priority: P1) (K7/K8)

**Goal**: comprobantes correctos, validación previa, conciliación y guarda de cierre para captaciones.

**Independent Test**: con la matriz completa, consignar, causar y liquidar; comprobar comprobantes y
conciliación en cero; quitar una regla y ver el bloqueo (quickstart §2, US8).

- [x] T142 [US8] Verificar que los hechos emitidos por US3, US4, US5 y US10 se contabilizan con la tabla hecho × rol sembrada en la fase fundacional, y completar lo que falte, en `A/Accounting/Lending/Reglas/OperacionesDeCartera.cs` — hecho el 2026-10-18: el capital inactivo en GMF, liquidación, retención, cargos y embargo; `PagoReversado` acompañante del reverso (D45); una muestra por cada uno de los 30 hechos de negocio que se desglosa cuadrada en roles de la tabla (`HechoPorRolDeCaptacionesTests`) y el cuadre peso a peso con el libro sobre las operaciones reales (`HechosDelLibroDeCuentasTests`). La validación previa dentro del proceso diario (causación, liquidación, cargos periódicos, débito automático) quedó con T145 (research E108, E113)
- [x] T143 [US8] Constructor de líneas para captaciones (tercero titular, documento cruce `CTA` = número de cuenta, `AccountFlag` de research C20, `IssueTermBand` sólo en CDAT y contractual) en `A/Accounting/Lending/Contabilizacion/ConstructorDeLineasDeCartera.cs` — hecho: `ConstructorDeLineasDeCartera.Captaciones.cs` (concepto → rol por su lado natural y la contrapartida del hecho; los 30 hechos de negocio), el cruce al retiro y el pago en un `RA` con el puente anulado (D45), el reverso con la contrapartida de su original y en el comprobante del original; el reverso del cruce **puede** ir con el del pago en un solo comprobante (probado), pero el emisor sigue sacándolos aparte hasta que el dueño responda (research E106, E107, E110)
- [x] T144 [US8] Plantilla de la matriz v2 con filas sugeridas sin cuenta y columna `cuentaSugerida` (CUIF de `contracts/contabilidad.md`), completitud por naturaleza, en `A/Accounting/Lending/Plantilla/` — hecho: `SugerenciasDeCaptaciones` con las cuentas CUIF como dato embebido (`sugerencias-cuif-captaciones.json`), cinco columnas de dimensión y la informativa, archivo v1 legible; la completitud exige por línea las combinaciones de K7 y K8 y avisa puentes con cruce, causación por cuenta, marca en otra cuenta y el puente de Tesorería (research E111)
- [x] T145 [US8] Validación previa de captaciones (bloquea en pantallas, sella y alerta en contrato y procesos) en `A/Common/Integration/Accounting/IContabilidadParaCartera.cs` y su implementación del lado contable — hecho «igual que los créditos» (D23 y A8 = «Bloquear todo»: con `Bloquear` también el contrato y el proceso detienen; con `Advertir` aplican, sellan y alertan): `HechosRepresentativosDeCaptaciones` y `ValidacionPreviaDeCaptaciones` (pantalla: retiro por giro, traslado, cancelación, liquidación del retiro; proceso: una pregunta por combinación en los cuatro subpasos; activación: todos los hechos de la naturaleza); la firma del puerto no cambió (research E108; pregunta para el dueño sobre el contrato)
- [x] T146 [US8] Conciliación por línea de captación, concepto y oficina, y guarda de cierre `LendingNotClosed` con la causación de captaciones, y la vista `deposits-reconciliation`, en `A/Lending/Integration/ConciliacionContableDeCartera.cs` y `A/Accounting/Periods/` — hecho: `SaldosContablesDeCaptaciones` (sin retención ni GMF, que se concilian con la declaración), el puerto de saldos mapeados con las cinco dimensiones, la vista y el día de causación pendiente en el bloqueo del cierre (research E109, E112)
- [x] T147 [US8] Lo de captaciones no se reversa desde Comprobantes (`Accounting.Document.ModuleOwned`) en `A/Accounting/Documents/DocumentCommands.cs` — hecho: ya lo cubría el origen `CAR`; ahora con el mensaje de captaciones y, en un resumido, cuántas cuentas reúne (research E112)
- [x] T148 [P] [US8] Pruebas de consumo (un hecho por cambio de saldo, ninguno con cuentas, SC-009), resolución, completitud, conciliación y guarda en `TA/Accounting/Lending/Captaciones/` — hecho: 6 clases, 61 pruebas (constructor, hecho × rol, validación previa, plantilla y completitud, cuadre del libro con los hechos, consumo, reverso y cierre)
- [x] T149 [US8] Filtro de naturaleza en `/contabilidad/cartera/matriz` y `/contabilidad/cartera/completitud` en `SH/Pages/Contabilidad/Cartera/` — hecho: filtro de naturaleza, las cinco dimensiones en el alta (el plazo de emisión cuando el capital de CDAT o contractual lo exige), la cuenta sugerida en la completitud y los DTO de `Shared` (research E114)
- [x] T150 [US8] e2e de US8 (escenarios 1 a 4) en `TI/Lending/Deposits/ContabilidadDeCaptacionesTests.cs` — escrita y compilada (y la matriz mínima de la activación en `CaptacionesE2E` ampliada a todos los hechos de la naturaleza); **e2e en la corrida final**

**Checkpoint**: Contabilidad recibe cada hecho de captaciones por la matriz de la Cartera, con validación previa, conciliación y guarda de cierre. Construida
el 2026-10-18 (research §15).

---

## Phase 10: User Story 9 - Migrar los saldos vigentes y cuadrar antes de activar (Priority: P1) (K7/K8)

**Goal**: cargas todo o nada, saldo inicial sin hechos, cuadre y activación por naturaleza.

**Independent Test**: 500 cuentas con un error en la fila 37; corregir, cuadrar y activar (quickstart US9).

- [x] T151 [P] [US9] Crear las filas de preparación de `data-model.md` §7.1 (`LND_MigrationLineEquivalenceRows`, `…ContributionAccountRows`, `…AccountInstallmentRows`, `…AccountPartyRows`, `…AccountRows`, `…HoldRows`, `…GmfExemptionRows`, `…TaxYearRows`) en `D/Entities/Lending/Deposits/Migration/` y sus configuraciones — hecho el 2026-10-19: siete filas de preparación (equivalencias, cuentas de aportes, cuentas de ahorro, partes, cuotas atrasadas, exenciones del GMF, acumulado tributario) y `MigrationLoad.ReferenceFiguresJson`; `…HoldRows` **no** se creó: llega con los bloqueos y embargos (US11) y el CDAT con K9 (research E116). `CaptacionesAportes` regenerada en los dos motores con el SQL a mano intacto
- [x] T152 [US9] Plantillas de `contracts/plantillas-y-archivos.md` (columna por columna, validaciones) registradas en la plataforma, aceptando `lending.contribution-balances` como fuente de aportes, en `A/Lending/Deposits/Migration/Plantillas/` — **parcial**: las plantillas de las partes de la carga, con `lending.contribution-balances` como fuente de aportes, registradas en el catálogo (`lending.deposits.migration`); las equivalencias de productos son una **parte de la carga**, no una hoja de las plantillas de líneas (research E115). **No** se construyeron las plantillas de importación de líneas (`lending.deposit-lines`, `lending.contribution-lines`): las líneas se crean por pantalla. `term-deposits` y `holds-and-garnishments` quedan sin definición hasta K9 y US11 (E116); `term-deposits` completada el 2026-10-19 con la fase 12 (research E139) y `holds-and-garnishments` con la fase 14 (research E166)
- [x] T153 [US9] Revisión previa todo o nada con errores por fila y columna y carga reemplazable por naturaleza (hecho informativo `CargaDeCaptacionesAplicada`) en `A/Lending/Deposits/Migration/` — hecho: revisión que no guarda, aplicación todo o nada (`Import.Invalid` con fila y columna), tandas con reemplazo, validación entre partes al aplicar y la carga anterior `Replaced`; hecho informativo `CargaDeCaptacionesAplicada`. Las cargas de créditos sólo ven las de colocación (E117)
- [x] T154 [US9] Saldo inicial por concepto al corte sin hechos (`OpeningBalance`) y cuadre contra los saldos contables mapeados por la matriz, con acta de diferencias, en `A/Lending/Deposits/Migration/CuadreDeCaptaciones.cs` — hecho: `OpeningBalance` por concepto fechado al corte, sin hechos; el cuadre reutiliza el de la 013 con las dimensiones de captaciones, y la aceptación de diferencias y el acta sirven a las dos naturalezas (E117, E121)
- [x] T155 [US9] Completar `ActivateContributionsCommand` y `ActivateDepositsCommand` **con carga** (corte + 1, acta de diferencias, cuadre recalculado, plazo de fecha valor, mes abierto; hecho `CaptacionesActivadas`), en `A/Lending/Deposits/Activation/` — hecho en `ActivacionDeCaptacionesConCarga` (los dos comandos enrutan por la carga): corte + 1, acta, cuadre recalculado, plazo de fecha valor `Captaciones.DiasMaximosFechaValor`, tandas de 500 cuentas en una sola transacción; el número migrado se conserva y el consecutivo de la línea se sube por encima (E119, E121)
- [x] T156 [US9] Cifras de referencia (totales cargados contra los que entrega la cooperativa, sin nombrar el sistema de origen) en `A/Lending/Deposits/Migration/CifrasDeReferencia.cs` — hecho: cifras de referencia por línea y concepto con su cotejo; ninguna pantalla nombra al sistema anterior
- [x] T157 [P] [US9] Pruebas de revisión, cuadre y activación en `TA/Lending/Deposits/Migration/` — hecho: 16 pruebas (revisión y aplicación 9, activación con carga 7)
- [x] T158 [US9] Rutas de `contracts/api.md` §16 en `API/Endpoints/Lending/Deposits/DepositsMigrationEndpoints.cs` — hecho: todas las rutas de §16 con `Lending.Deposits.Migration` / `.Activate` y alcance
- [x] T159 [US9] Pantallas `/cartera/captaciones/migracion` y `/cartera/captaciones/activacion` en `SH/Pages/Cartera/Captaciones/MigracionDeCaptaciones.razor` y `ActivacionDeCaptaciones.razor` — hecho: «Saldos iniciales» y «Activación» en el menú de puesta en marcha, con indicador de carga por zona
- [x] T160 [US9] e2e de US9 (escenarios 1 a 4, en los dos motores) en `TI/Lending/Deposits/MigracionDeCaptacionesTests.cs` — escrita y compilada (escenarios 1 a 4 sobre aportes: error en la fila 37, cuadre con acta y diferencias aceptadas por la secretaría, carga de ensayo reemplazada, saldo inicial al corte sin hechos); **e2e en la corrida final**
- [x] T161 [US9] Prueba de revisión de 50.000 cuentas en menos de 10 minutos (`RUN_PERF_TESTS=1`, SC-011) en `TI/Lending/Deposits/MigracionPerformanceTests.cs` — escrita y compilada (50.000 cuentas de ahorro en un archivo, 200 titulares; `CAPTACIONES_CUENTAS_DE_MIGRACION` baja el tamaño); **e2e en la corrida final**

**Checkpoint**: los saldos de aportes y captaciones entran por una carga revisada todo o nada, cuadran con acta y nacen sin hechos al día siguiente
del corte. Construida el 2026-10-19 (research §16).

---

## Phase 11: User Story 16 - Seguridad, atribuciones y auditoría verificable (Priority: P1)

**Goal**: permisos, alcance por oficina y línea, segregación y lecturas sensibles auditadas en todo lo nuevo.

**Independent Test**: usuario de otra oficina recibe «no encontrado»; la cadena de auditoría se verifica.

- [x] T162 [US16] Aplicar `IAlcanceDeCartera` (oficinas y líneas de cualquier naturaleza, falla cerrado, evento `Lending.Access.OutOfScope`) en todas las consultas y comandos de `A/Lending/Deposits/` — hecho el 2026-10-19: la auditoría por handler encontró que la **otra** cuenta de una operación se leía sin alcance (destino del traslado, de su simulación, de la entrega de la cancelación y de la cesión de aportes) y también el crédito del débito automático y las cuentas del resumen de aportes de una persona; todos responden ahora el 404 de lo inexistente con el evento. `LasCaptacionesExigenPermisoYAlcance` mira cada handler (no el archivo) y prohíbe leer una cuenta por el pedido fuera de `LectorDeCuentas.CuentaEnAlcanceAsync`; crear líneas queda exceptuado con motivo (research E122, E123)
- [x] T163 [US16] Reglas de segregación de FR-005 (embargo, apertura excepcional, entrega, devolución, asamblea, tasa negociada) por persona en `D/Lending/Segregation/` y su uso en los handlers — hecho: `D/Lending/Segregation/SegregacionDeCaptaciones.cs` (nueve expedientes con su par registrar × aprobar sobre la regla fija de la 013, comparación por persona, raíz derivada de los que cuelgan de un recurso ajeno) usado por la apertura, las solicitudes de aportes, el retiro del asociado y su devolución y los cargos; corrige que quien abría una cuenta no pudiera condonar sus cargos. Embargo (K8), tasa negociada (K9), fallecimiento y asamblea (K10) quedan declarados para sus historias; `LaSegregacionDeCaptacionesUsaSuCatalogo` lo vigila (research E122)
- [x] T164 [US16] Lecturas sensibles auditadas (saldos y movimientos de un titular, documentos, exportaciones) en `A/Lending/Deposits/` con los eventos de `contracts/api.md` — hecho: rutas `GET /accounts/{id}/balance` (saldo a D con lo sabido hasta R, huella, disponible y lo causado con su explicación) y `/statement` (extracto en pantalla) de api.md §4; `Lending.Savings.BalanceViewed` en el saldo, el detalle, la lista de una persona, la vista previa de la cancelación y el resumen de aportes; `.StatementViewed` en el extracto; `.TransactionsViewed` también en el uso del GMF de una persona. Exportaciones y documentos ya los auditaba la 013 (`Lending.Report.Exported`, `Lending.Document.*`). `LasLecturasSensiblesDeCaptacionesSeAuditan`; sin pantalla nueva para el saldo y el extracto (research E124)
- [x] T165 [US16] e2e de seguridad (sin permiso = inexistente, fuera de alcance, segregación, verificación de la cadena) en `TI/Lending/Deposits/SeguridadDeCaptacionesTests.cs` — escrita y compilada (cinco escenarios: retiro sin permiso, asesor de O2 sobre una cuenta de O1 con saldo, extracto y destino de traslado, lecturas auditadas del asesor de O1, cargo registrado y condonado por personas distintas, cadena íntegra y luego alterada y borrada); **e2e en la corrida final**

**Checkpoint P1**: K7 y K8 completas (aportes y ahorro a la vista y permanente). Construida la fase 11 el 2026-10-19 (research §17).

---

## Phase 12: User Story 6 - Constituir, renovar y redimir un CDAT (Priority: P2) (K9)

**Goal**: CDAT con plan de intereses, gracia, prórroga, renovación, redención, tasa negociada y título.

**Independent Test**: 10.000.000 a 180 días al 10 % EA base 360 mensual: seis pagos de 79.741; prórroga tras
la gracia; tasa negociada en espera del aprobador (quickstart US6).

- [x] T166 [P] [US6] Motor puro `PlanDeCdat` (plan vencido o anticipado, causación cuadrada al plan, gracia en días hábiles, prórroga con o sin capitalización, renovación, redención con penalidad, `IssueTermBand`) en `D/Lending/Deposits/PlanDeCdat.cs` — hecho el 2026-10-19: plan por días de la periodicidad, causación cuadrada al plan, gracia en días hábiles, plazo de emisión y redención (research E128, E129)
- [x] T167 [P] [US6] Casos dorados de CDAT (481.244; 488.088; 6 × 79.741 = 478.446; 79.111 anticipado; prórroga con capitalización neta de retención; redención anticipada; vencimiento en día no hábil) en `TD/Lending/Deposits/Casos/cdat-*.json` — hecho: diez casos `cdat-*` (481.244, 488.088, 6 × 79.741, 79.111, prórroga 10.468.564, reliquidación, descuento, festivo, plazo irregular) recalculados por `calcular.py`
- [x] T168 [P] [US6] Motor puro `TasaNegociada` (regla más específica, margen máximo) en `D/Lending/Deposits/TasaNegociada.cs` con pruebas en `TD/Lending/Deposits/TasaNegociadaTests.cs` — hecho: regla más específica, desempate por prioridad, margen máximo; casos `tasa-negociada-01/02` y pruebas propias
- [x] T169 [P] [US6] Crear `MemberAccountTermsVersion` (§3.3), `TermDepositScheduleItem` (§3.4) y `NegotiatedRateRule` (§2.4) y la fila de preparación `LND_MigrationTermDepositRows` en `D/Entities/Lending/Deposits/` y sus configuraciones; generar la migración par `CaptacionesCdat` — hecho: las cuatro tablas, `LND_NegotiatedRateRequests` (nueva, E133), `LND_MigrationTermDepositRows`, `TermsVersionId` en el libro y la propuesta y la instrucción en la cuenta; **no** nace la migración `CaptacionesCdat`: va en `CaptacionesAportes`, regenerada (E35, §18)
- [x] T170 [US6] `ConstituteTermDepositCommand` (rango de plazo, tasa de la tabla, destino de intereses, instrucción, entrada por contrato o traslado) en `A/Lending/Deposits/TermDeposits/` — hecho: simulación, constitución con la apertura común (`AperturaDeCuentas`), propuesta hasta el fondeo, fondeo por traslado (`POST /{id}/fund`, nueva) y por recaudador por el contrato (E130)
- [x] T171 [US6] Solicitud y aprobación de tasa negociada por la matriz de atribuciones (`NegotiatedRate`, puntos y monto; aprobador con segundo factor o sesión con acta) también para cuentas a la vista y contractuales cuando la línea lo declare (FR-127), en `A/Lending/Deposits/TermDeposits/NegotiatedRates/` — hecho: reglas versionadas, criterio de margen en la matriz, solicitudes con nivel o acta y segregación; la decide quien tiene el nivel en su sesión (E133)
- [x] T172 [US6] En `PasoDeCaptaciones`: pago de intereses del plan neto de retención, vencimiento, días de gracia, prórroga automática hasta el máximo, tratamiento sin prórroga, CDAT embargado o de fallecido, reclasificación por `IssueTermBand` (`CaptacionReclasificada`) en `A/Lending/Deposits/TermDeposits/CicloDelCdat.cs` — hecho: causación por plan y subpaso `CicloDelCdat` (pagos, gracia, prórroga, tratamiento, embargado o fallecido, reclasificación) (E134, E135)
- [x] T173 [US6] `RenewTermDepositCommand` y `RedeemTermDepositCommand` (simulación previa, atribución, GMF de la redención abonada) en `A/Lending/Deposits/TermDeposits/` — hecho: instrucción, renovación y redención con simulación, penalidad, atribución con monto y GMF (E136)
- [x] T174 [US6] Hechos `CdatConstituido`, `InteresesDeCdatPagados`, `CdatProrrogado`, `CdatRenovado`, `CdatRedimido`, `CdatCancelado`, `PenalidadAplicada` en `A/Common/Integration/Contracts/Lending/Deposits/` — hecho: los seis hechos y la penalidad con la cuenta contraparte (resuelve `CounterpartUnknown` de la fase 9, E132) y la reclasificación por plazo
- [x] T175 [US6] Sumar los CDAT vigentes a la liquidación del retiro del asociado (siguen hasta su vencimiento salvo que la línea diga otra cosa) en `A/Lending/Deposits/Withdrawals/SettleMembershipWithdrawalCommand.cs` — hecho: líneas informativas `TermDepositBalance`; siguen hasta su vencimiento (E137)
- [x] T176 [P] [US6] Vistas `term-deposit-maturities`, `negotiated-rates` y `depositor-concentration` del centro de informes en `A/Lending/Deposits/Reports/` — hecho (E138)
- [x] T177 [P] [US6] Pruebas de handlers de CDAT y tasa negociada en `TA/Lending/Deposits/TermDeposits/` — hecho: constitución, fondeo, negociación, ciclo, prórroga, renovación, redención, vistas, constructor, migración y retiro
- [x] T178 [US6] Rutas de `contracts/api.md` §5 y §6 en `API/Endpoints/Lending/Deposits/TermDepositsEndpoints.cs` y `NegotiatedRatesEndpoints.cs` — hecho: §5 y §6 más las reglas bajo `/lines/{id}/negotiated-rate-rules` y `POST /{id}/fund`
- [x] T179 [US6] Pantallas `/cartera/cdat`, `/cartera/cdat/nuevo`, `/cartera/cdat/{id}` y el editor de reglas de tasa negociada en `SH/Pages/Cartera/Captaciones/Cdat*.razor` — hecho: `Cdats.razor` (lista, calendario y solicitudes por decidir), `CdatNuevo.razor` (simular y constituir), `CdatDetalle.razor` (condiciones, plan, fondear, instrucción, renovar, redimir, decidir la tasa) y `ReglasDeTasaNegociada.razor` dentro del detalle de la línea; enlace «CDAT» en el menú (research E141)
- [x] T180 [US6] e2e de US6 (escenarios 1 a 6) en `TI/Lending/Deposits/CdatTests.cs` — escrita y compilada (seis escenarios de US6 y la migración de un CDAT en `MigracionDeCaptacionesTests`); **e2e en la corrida final**


**Checkpoint**: CDAT con plan, gracia, prórroga, renovación, redención y tasa negociada. Construida la fase 12 el 2026-10-19 (research §18); el título del CDAT lo emite US13.

---

## Phase 13: User Story 7 - Ahorro contractual o programado (Priority: P2) (K9)

**Goal**: cuotas, bonificación por cumplimiento e incumplimiento.

**Independent Test**: 12 × 200.000: 2.486.777 cumpliendo; tasa castigo pagando 10.

- [x] T181 [P] [US7] Motor puro `AhorroContractual` en `D/Lending/Deposits/AhorroContractual.cs` con casos dorados (2.465.306; 2.486.777; bonificación 21.471; incumplimiento) en `TD/Lending/Deposits/Casos/contractual-*.json` — hecho: valor final, bonificación y reliquidación sobre los aportes, cuotas por meses o por días, siete casos dorados `contractual-01` a `-07` (research E142)
- [x] T182 [US7] Cuotas del contractual en `GeneradorDeCuotas`, estado de cumplimiento, incumplimiento por tolerancia y vencimiento con traslado o giro en `A/Lending/Deposits/Contractual/` — hecho: contrato sellado al abrir con la primera cuota, cuotas del paso diario, subpaso «Contractual» (cumplir, incumplir por atrasos o al vencer, efectos `BreachRate`/`MoveToDemand`/`Cancel`, entrega), días de gracia por parámetro, cancelación anticipada por la ruta de cierre, rutas `/contractual`, `/terms` y `/maturity-instruction`, la migración de saldos del contractual (fase 10) y `CaptacionesAportes` regenerada (research E143–E147, E149–E151, E153)
- [x] T183 [US7] Hechos de bonificación (hecho `BonificacionReconocida` con la transacción `BonusRecognized`, gasto según la política `BonusRecognition`) en `A/Common/Integration/Contracts/Lending/Deposits/` — hecho: provisión (`Provisioned`) y abono al cumplir con su retención y acumulados, reversión al incumplir, `BON` con los roles de capital y el acompañante `RetencionPracticada`, hechos representativos para la validación previa y la activación (research E148)
- [x] T184 [P] [US7] Pruebas de handlers del contractual en `TA/Lending/Deposits/Contractual/` — hecho: quince pruebas de punta a punta sobre el libro (apertura, cuotas, cumplimiento con 21.471, vencimiento con cuotas sin pagar a 32.001, los tres efectos, provisión, cancelación, instrucción, consulta)
- [x] T185 [US7] Vista del contrato en la ficha de cuenta en `SH/Pages/Cartera/Captaciones/ContratoDeAhorro.razor` — hecho: `ContratoDeAhorro.razor` en la ficha, el bloque del contrato en la apertura y el costo de cancelar en la vista previa (research E152)
- [x] T186 [US7] e2e de US7 (escenarios 1 y 2) en `TI/Lending/Deposits/AhorroContractualTests.cs` — escrita y compilada (escenarios 1 y 2); **e2e en la corrida final**


**Checkpoint**: ahorro contractual con contrato sellado, cuotas, bonificación e incumplimiento. Construida la fase 13 el 2026-10-19 (research §19).

---

## Phase 14: User Story 11 - Bloqueos y embargos con límite inembargable (Priority: P2) (K8)

**Goal**: bloqueos y embargos con respuesta al juzgado y consignación.

**Independent Test**: dos cuentas sobre el límite: sólo el exceso retenido; embargo de aportes rechazado.

- [x] T187 [P] [US11] Crear `AccountHold` (§5.1), `Garnishment` (§5.2) y `GarnishmentResponse` en `D/Entities/Lending/Deposits/Restrictions/` y sus configuraciones — hecho el 2026-10-19: las tres tablas, `LND_MigrationHoldRows` (la fila de preparación que la fase 10 dejó pendiente), `AccrualResumedOn` en la cuenta y `GarnishmentId` en la operación; enums `GarnishmentAuthority` y `GarnishmentAccountsScope`; `CaptacionesAportes` regenerada en los dos motores con el SQL a mano intacto (research E157, E166)
- [x] T188 [P] [US11] Motor puro `LimiteInembargable` (suma de cuentas, alimentos, orden de cuentas) en `D/Lending/Deposits/LimiteInembargable.cs` con casos dorados en `TD/Lending/Deposits/Casos/embargo-*.json` — hecho: suma de las protegidas o cuenta más antigua (coactivo), alimentos y persona jurídica sin límite, concurrencia por persona, orden de clases; siete casos `embargo-01` a `-07` por `calcular.py`; seis parámetros `Embargos.*` y el `LIMITE` `EMBARGO_COACTIVO_SMMLV` (research E155, E156)
- [x] T189 [US11] Comandos de bloqueo (incluido el de garantía de un crédito) y de embargo, desembargo, respuesta al juzgado (documento) y consignación por orden de giro (hecho `EmbargoConsignado`); vista `garnishments`, en `A/Lending/Deposits/Holds/` — hecho: `RetencionDeEmbargos` (único escritor de lo retenido, recálculo bajo el cerrojo de la persona, marca y suspensión por embargo total), `ReclasificacionDeCuenta` (`CaptacionReclasificada` por embargo, desembargo y exención del GMF que rige hoy; lista para la inactividad de US14), registro que rige al recibirse, aportes inembargables con su respuesta, respuestas con la plantilla de la entidad (el documento emitido queda para US13), consignación por orden de giro, desembargo total o parcial con segregación, bloqueos, subpaso «Embargos» del ciclo, el disponible que resta bloqueos y embargos, la vista `garnishments` y la parte `holds-and-garnishments` de la migración de saldos (research E157–E166)
- [x] T190 [P] [US11] Pruebas de bloqueos y embargos en `TA/Lending/Deposits/Holds/` — hecho: diez pruebas sobre el libro (exceso sobre el límite, alimentos, aportes, segregación y desembargo, parcial, consignación, retiro, límite faltante, ciclo diario, bloqueos) y dos de la migración en `TA/Lending/Deposits/Migration/`
- [x] T191 [US11] Rutas de `contracts/api.md` §8 y de bloqueos en `API/Endpoints/Lending/Deposits/GarnishmentsEndpoints.cs` — hecho: las seis de §8 más el borrador de la respuesta y las tres de `/accounts/{id}/holds` (research E161)
- [x] T192 [US11] Pantalla `/cartera/embargos` en `SH/Pages/Cartera/Captaciones/Embargos.razor` — hecho: registro con el oficio como soporte de la persona, lo retenido con la explicación del límite, respuestas con borrador, consignación y desembargo; enlace «Embargos» en el menú. Los bloqueos de una cuenta todavía no tienen componente en la ficha (se manejan por API)
- [x] T193 [US11] e2e de US11 (escenarios 1 a 3) en `TI/Lending/Deposits/EmbargosTests.cs` — escrita y compilada (la prueba independiente con el desembargo de US11-3, US11-1 y US11-2); **e2e en la corrida final**


**Checkpoint**: bloqueos y embargos con el límite inembargable por entidad, respuesta al juzgado y consignación por orden de giro. Construida la fase 14 el
2026-10-19 (research §20); también los pendientes de embargos de las fases 9 (reclasificación por la marca), 10 (parte `holds-and-garnishments`), 11
(segregación, alcance y lecturas) y 12 (el CDAT embargado al vencer queda a disposición: revisado).

---

## Phase 15: User Story 13 - Documentos al titular (Priority: P2) (K8/K9)

**Goal**: los seis tipos de documento que faltan (de los 9; constancia, paz y salvo y liquidación de retiro ya nacen en K7) con verificación y anulación por reverso.

**Independent Test**: certificado tributario anual; reversar una retención de diciembre lo anula.

- [x] T194 [P] [US13] Eventos de documento nuevos y anulación por reverso de operaciones de cuenta (`IssuedDocumentAccount` ya existe desde US3) en `D/Entities/Lending/Documents/` — hecho el 2026-10-19: eventos y estados `Superseded` 4 y `Settled` 5, `IssuedDocumentAccount.TermsVersionId`, `IssuedByUserId` nulo para el proceso; motores puros `VigenciaDeDocumentosDeCuentas`, `ProporcionesDeTitularidad`, `CertificadoTributarioDeCaptaciones` y `CertificadoDeRetencionDeCaptaciones`; `CaptacionesAportes` regenerada en los dos motores con el SQL a mano intacto (research E168, E179)
- [x] T195 [US13] Generadores QuestPDF de extracto, certificado de saldo, certificado tributario anual (cuadra con liquidaciones, retenciones y GMF del año, SC-016), certificado de retención, título CDAT «no negociable» y respuesta al juzgado (`GarnishmentResponse`) (`contracts/documentos.md`) en `API/Reports/Captaciones/` — hecho: `DocumentosDeCaptacionesReport` pinta los seis cuerpos dentro de la plantilla común (membrete, firma, código y QR) y la leyenda «CDAT — NO NEGOCIABLE» va destacada; ET art. 381 y DUR 1.6.1.13.2.40/1.6.1.12.12 aplicados (research §21, E174, E176)
- [x] T196 [US13] Emisión, copia guardada, anulación automática por reverso y lotes de envío en `A/Lending/Deposits/Documents/` — hecho: `EmisionDeDocumentosDeCartera` (la emisión en la unidad de trabajo de quien la pide), `DocumentosDeCaptaciones`, la misma copia del extracto, `TitulosDeCdat` (título automático al constituir, prorrogar y renovar; reemplazado y liquidado), `RespuestasAlJuzgado`, la anulación por clase y el lote síncrono (tributario, retención y extractos por la periodicidad de la entidad); cuatro parámetros `Documentos.*` (research E169–E177)
- [x] T197 [P] [US13] Pruebas de emisión y anulación en `TA/Lending/Deposits/Documents/` — hecho: 12 pruebas de Application y 11 de dominio (`DocumentosAlTitularTests`), con SC-016 al 60/40 (research E181)
- [x] T198 [US13] Rutas de `contracts/api.md` §17 en `API/Endpoints/Lending/Deposits/DepositDocumentsEndpoints.cs` — hecho: `POST /documents/batches` (200 con el resumen, E177) y `POST /term-deposits/{id}/titles`; la emisión de la 013 lee `accountPublicId`, `includeRestrictions` e `includePermanentSavings` y la lista filtra por `accountPublicId`
- [x] T199 [US13] Emisión desde la ficha de cuenta y de aportes en `SH/Pages/Cartera/Captaciones/DocumentosDeCuenta.razor` — hecho: en la ficha de la cuenta, del CDAT y de aportes, con el saldo a una fecha y el extracto en pantalla (pendiente de la fase 11); el lote sin pantalla todavía (research E178)
- [x] T200 [US13] e2e de US13 (escenarios 1 y 2) en `TI/Lending/Deposits/DocumentosDeCaptacionesTests.cs` — escrita y compilada (US13-1, US13-2 y los rechazos); **e2e en la corrida final**, con `EntregaVigente` ≥ K8

**Checkpoint**: documentos al titular de captaciones con verificación, anulación por reverso, título del CDAT automático y respuesta al juzgado como documento. Construida la fase 15 el 2026-10-19 (research §21); también los pendientes de documentos de las fases 11 (pantalla del saldo y del extracto), 12 (el título), 13 (bonificación en el certificado y extracto del contrato) y 14 (el documento de la respuesta al juzgado).

---

## Phase 16: User Story 12 - Revalorización de aportes y distribución de excedentes (Priority: P2) (K10)

**Goal**: decisiones de asamblea simuladas, aprobadas y aplicadas en una operación.

**Independent Test**: 5 % con IPC 4 % y fondo menor: limitada y prorrateada.

- [x] T201 [P] [US12] Motor puro `RevalorizacionYExcedentes` en `D/Lending/Deposits/RevalorizacionYExcedentes.cs` con casos dorados en `TD/Lending/Deposits/Casos/asamblea-*.json` — hecho el 2026-10-19: saldo promedio diario del ejercicio (CBCF «promedio día/año», D13) o al cierre, tope del IPC en la revalorización, valor repartido y prorrateo al fondo por el mayor residuo (la suma es el fondo), amortización hasta el mínimo; cinco dorados `asamblea-01` a `-05` por `calcular.py --verificar` (research E182)
- [x] T202 [P] [US12] Crear `AssemblyDistribution` y `AssemblyDistributionLine` (§6.3, §6.4) y sus configuraciones; generar la migración par `CaptacionesAsamblea` — hecho: en `CaptacionesAportes` regenerada en los dos motores (no nace `CaptacionesAsamblea`, E35), con `LND_MemberAccountOperations.AssemblyDistributionId`, el estado `Reversed` 5 y `PayoutPurpose.SurplusPayment` 8; SQL a mano intacto y paridad OK (research E183)
- [x] T203 [US12] Registrar, simular, aprobar (segundo usuario) y aplicar revalorización, excedentes y amortización; reverso entero; hechos de asamblea, en `A/Lending/Deposits/Assembly/` — hecho: borrador, simulación sin clave, aplicación por otra persona con el punto del acta cerrada (asunto nuevo `AssemblyDistribution`), el acta como soporte (`LendingAssemblySupport`) y la huella; excedentes a aportes, ahorro o pago; reverso entero con un solo hecho espejo y corrección que hereda el acta; seis parámetros `Asamblea.*`; alcance total. Sin retención sobre lo abonado ni ruta para volver a girar un pago rechazado (preguntas de research §22.1)
- [x] T204 [P] [US12] Pruebas de asamblea en `TA/Lending/Deposits/Assembly/` — hecho: `AsambleaTests` (10) y `HechosDeAsambleaTests` (3, el constructor) (research E192)
- [x] T205 [US12] Rutas de `contracts/api.md` §12 y pantalla `/cartera/asamblea` en `API/Endpoints/Lending/Deposits/AssemblyDistributionsEndpoints.cs` y `SH/Pages/Cartera/Captaciones/Asamblea.razor` — hecho: siete rutas (más `POST /{id}/void`) y la pantalla con su enlace en el menú (research E191)
- [x] T206 [US12] e2e de US12 (escenarios 1 y 2) en `TI/Lending/Deposits/AsambleaTests.cs` — escrita y compilada; **e2e en la corrida final**, con `EntregaVigente` ≥ K10

**Checkpoint**: revalorización de aportes, distribución de excedentes y amortización simuladas, aplicadas con el acta por otra persona y reversadas enteras. Construida la fase 16 el 2026-10-19 (research §22).

---

## Phase 17: User Story 14 - Inactividad, no reclamados y fallecimiento del titular (Priority: P2) (K10)

**Goal**: ciclo de vida de cuentas sin movimiento y de titulares fallecidos.

**Independent Test**: inactiva a los días de la línea (el interés no la reactiva); fallecido con dos
beneficiarios genera dos órdenes de giro.

- [x] T207 [P] [US14] Crear `DeceasedHolderCase` (§6.5) y `UnclaimedBalance` (§6.6) y sus configuraciones — hecho el 2026-10-19: las dos tablas, `DeceasedHolderCaseId` en el bloqueo, la operación y la orden, el concepto `Unclaimed` 11, la operación y el propósito `UnclaimedPayment` y el estado `DeliveryPending` 5; `CaptacionesAportes` regenerada en los dos motores con el SQL a mano intacto (research E193, E194)
- [x] T208 [US14] Inactividad y reactivación en `PasoDeCaptaciones` con reclasificación (`CaptacionReclasificada`) y cargo por inactividad si la línea lo tiene, en `A/Lending/Deposits/Accrual/Inactividad.cs` — hecho: subpaso `Inactividad` (la migrada desde el corte), avisos `PREINACTIV`/`INACTIVA`, `ReactivacionDeCuenta` en la consignación, el retiro, el traslado y el contractual, y el subpaso `MarcasQueRigen` de la exención del GMF a futuro (pendiente de la fase 14); el cargo por inactividad lo causa el subpaso de cargos sobre la cuenta ya inactiva (research E195–E197)
- [x] T209 [US14] Traslado a no reclamados y pago al aparecer el titular en el plazo de `Captaciones.DiasParaNoReclamados` (hecho `SaldoTrasladadoANoReclamados`; vista `unclaimed`) en `A/Lending/Deposits/Unclaimed/` — hecho: lo devuelto y no cobrado y las inactivas por reglamento (`CuentasAbandonadas.*`), subpaso del ciclo, traslado a mano todo o nada, pago por giro o traslado sobre la cuenta cancelada y el giro devuelto que la retiene otra vez; el traslado al ICETEX no se construyó (Ley 1777 y Decreto 953, pregunta) y el enganche de los excedentes rechazados quedó anotado sin decidir (research E198, E199, E205)
- [x] T210 [US14] Fallecimiento: bloqueo, entrega a beneficiarios o sin juicio de sucesión hasta el tope (soportes del parámetro) o por sucesión, aprobación de segundo usuario, órdenes de giro, en `A/Lending/Deposits/Deceased/` — hecho: registro con el registro civil, bloqueo total y suspensión según la línea (`TitularesFallecidos`), vista previa, entrega registrada y aprobada por otra persona, reparto al centavo, giros o traslados y el caso entregado al girarse la última; anulación de un registro errado (research E200–E202)
- [x] T211 [P] [US14] Pruebas de inactividad, no reclamados y fallecimiento en `TA/Lending/Deposits/` — hecho: `CicloDeVida/CicloDeVidaDeCuentasTests` (11) (research E204)
- [x] T212 [US14] Rutas de `contracts/api.md` §13 y §14 y pantallas `/cartera/fallecimientos` y `/cartera/no-reclamados` en `API/Endpoints/Lending/Deposits/DeceasedCasesEndpoints.cs` y `UnclaimedEndpoints.cs`, y `SH/Pages/Cartera/Captaciones/` — hecho: once rutas (con `POST /deceased-cases/{id}/void` y la vista `unclaimed`) y las dos pantallas con su enlace en el menú (research E203)
- [x] T213 [US14] e2e de US14 (escenarios 1 y 2) en `TI/Lending/Deposits/CicloDeVidaDeCuentasTests.cs` — escrita y compilada; **e2e en la corrida final**, con `EntregaVigente` ≥ K10

**Checkpoint**: inactividad por los días de la línea con su reclasificación y reactivación, saldos no reclamados con su pago, y fallecimiento del titular con la
entrega aprobada por otra persona. Construida la fase 17 el 2026-10-19 (research §23); también los pendientes de las fases 10 (la inactividad de la migrada
desde el corte) y 14 (la reclasificación por la inactividad y por la exención a futuro); el de la fase 16 (excedentes rechazados) quedó anotado sin decidir.

---

## Phase 18: User Story 15 - Fondo de liquidez y depósitos asegurados (Priority: P3) (K11)

**Goal**: base y requerido a cualquier fecha y depósitos asegurados.

**Independent Test**: requerido por banda igual al cálculo manual; alerta si el constituido es menor.

- [x] T214 [P] [US15] Motor puro `FondoDeLiquidez` (categoría, bandas, transición del Decreto 976 de 2026) en `D/Lending/Deposits/FondoDeLiquidez.cs` con casos dorados en `TD/Lending/Deposits/Casos/liquidez-*.json` — hecho el 2026-10-19: régimen general (10 % y 2 %) y por madurez residual de la categoría plena (10 %, 5 %, 0 %; permanente sólo al retiro al 5 %) con la transición de 42 meses, pisos legales `LIMITE`; además los motores puros `RiesgoDeLiquidez` (IRL y brecha por bandas) y `DepositosAsegurados`; siete dorados `liquidez-01` a `-07` por `calcular.py --verificar` (research E207, E208)
- [x] T215 [P] [US15] Crear `LiquidityFundSnapshot` y `LiquidityFundHolding` (§6.7, §6.8) y sus configuraciones; generar la migración par `CaptacionesLiquidez` — hecho: en `CaptacionesAportes` regenerada en los dos motores (no nace `CaptacionesLiquidez`, E35), con la medición del IRL en la foto, `LiquidityHoldingKind.GovernmentBond` 4 y el alias de precisión `Indicador`; SQL a mano intacto y paridad OK (research E209)
- [x] T216 [US15] Cálculo e instantánea, registro del constituido, alerta `Cartera.FondoDeLiquidezInsuficiente` y depósitos asegurados por ahorrador en `A/Lending/Deposits/Liquidity/` — hecho: posición a una fecha sobre el libro, foto única por fecha con sus alertas (insuficiente, en el margen, IRL bajo) al tesorero y a la gerencia, IRL y brecha, inversiones con soporte (`LendingLiquidityFundSupport`), depósitos asegurados por ahorrador hasta `TOPE_SEGURO_DEPOSITOS` (sembrado), corte del ciclo diario por la periodicidad de la entidad, diez parámetros nuevos y cinco `LIMITE`, alcance total; pruebas `Liquidity/FondoDeLiquidezTests` (9) (research E210–E213)
- [x] T217 [US15] Rutas de `contracts/api.md` §15 y pantalla `/cartera/fondo-de-liquidez` en `API/Endpoints/Lending/Deposits/LiquidityFundEndpoints.cs` y `SH/Pages/Cartera/Captaciones/FondoDeLiquidez.razor` — hecho: las seis rutas de §15 más `GET /risk`, las vistas `liquidity-fund` e `insured-deposits` (adelantadas de T219) y la pantalla con su enlace en el menú (research E210, E214)
- [x] T218 [US15] e2e de US15 en `TI/Lending/Deposits/FondoDeLiquidezTests.cs` — escrita y compilada; **e2e en la corrida final**

**Checkpoint**: fondo de liquidez a cualquier fecha (régimen general, por madurez y transición del Decreto 976 de 2026) contra lo constituido, con sus alertas; IRL y brecha de liquidez; depósitos asegurados por ahorrador. Construida la fase 18 el 2026-10-19 (research §24); también el pendiente de la fase 17 (la validación contable previa de la entrega por fallecimiento y del traslado y el pago de no reclamados, E206).

---

## Phase 19: User Story 17 - Informes de gestión y reportes regulatorios (Priority: P3) (K11)

**Goal**: 21 vistas del centro de informes y archivos SICSES y exógena.

**Independent Test**: formato de captaciones de un mes cuadra con la conciliación y sale con la alerta
«formato sin cotejar».

- [x] T219 [US17] Vistas que faltan de `contracts/api.md` §18.1 (`deposit-accounts-by-status`, `liquidity-fund`, `insured-deposits` y las regulatorias) — hecho el 2026-10-19: `liquidity-fund` e `insured-deposits` venían de T216/T217 (E213); las demás de §18.1 ya existían de fases anteriores (saldos, movimientos, GMF, cargos, intereses y retención, conciliación, vencimientos de CDAT, tasas negociadas, concentración, embargos, no reclamados, aportes por asociado, cuotas en mora). Se suman `deposit-accounts-by-status` y la vista previa de **siete** formatos (las cinco de §18.1 más `sicses-cotitulares` y `sicses-aportes`) en `A/Lending/Deposits/Reports/RegulatoryReportQueries.cs` (research E216, E221)
- [x] T220 [US17] Layouts versionados como dato con `verified = false` (SICSES captaciones, saldos diarios, fondo de liquidez; exógena 1019 y 1020) en `A/Lending/Regulatory/Layouts/` con el catálogo `CatalogoDeLayoutsDeCartera` sobre `Domain/Common/Layouts` (research C21) y su generador — hecho: siete layouts (SICSES 9026, 9068, 9013, 169 y 9067; exógena 1019 v9 y 1020 v8) con su fuente citada, `RegulatoryFileKind` 3 y 4 con `FormatCode` (`CaptacionesAportes` regenerada en los dos motores), `ProyeccionDeCaptaciones` y la generación de la 013 extendida; LIMITE `EXOGENA_1019_UMBRAL_UVT` y el motor puro `EstadisticasDeSaldos` (research E216–E220)
- [x] T221 [US17] Rutas de `contracts/api.md` §18.2 en `API/Endpoints/Lending/Deposits/RegulatoryEndpoints.cs` — hecho: `validate` y `generate` por `{formatCode}` con el período en el cuerpo y `/files?format=` (research E222)
- [x] T222 [US17] Tarjetas del Centro de Reportes y vista en `SH/Pages/Cartera/Informes.razor` (los `NavigateTo` llevan a páginas existentes) — hecho: sección «Captaciones y aportes» del centro de reportes, año, mes y estados de cuenta en `/cartera/informes`, y la pantalla nueva `/cartera/captaciones/reportes-regulatorios` con su enlace en el menú (research E223)
- [x] T223 [P] [US17] Pruebas de vistas y layouts en `TA/Lending/Deposits/Reports/` — hecho: `FormatosDeCaptacionesTests` (catálogo y siete archivos dorados `Casos/*.esperado.txt`) e `InformesDeCaptacionesTests` (8, sobre el libro real), más `TD/Lending/Deposits/EstadisticasDeSaldosTests` (5)
- [x] T224 [US17] e2e de US17 en `TI/Lending/Deposits/InformesDeCaptacionesTests.cs` — escrita y compilada; **e2e en la corrida final**

**Checkpoint**: los formatos del SICSES de captaciones (9026, 9068, 9013, 169, 9067) y de la exógena (1019, 1020) como dato sin cotejar, validados, generados,
regenerados y marcados cargados con la mecánica de la 013; la vista previa de cada uno en el centro de informes y las cuentas por estado. Construida la fase 19
el 2026-10-19 (research §25).

---

## Phase 20: Polish & Cross-Cutting Concerns

- [x] T225 Congelar las migraciones pares (`CaptacionesAportes`, `CaptacionesAhorro`, `CaptacionesCdat`, `CaptacionesAsamblea`, `CaptacionesLiquidez`): regenerarlas una última vez en orden y verificar que aplican sobre una base de la 013 en los dos motores — hecho el 2026-10-19: sólo existe `CaptacionesAportes` (E35) y no hizo falta regenerarla (modelo sin cambios y paridad OK en los dos motores); congelada por huella con `LasMigracionesCongeladasNoSeRegeneran` junto con `RetiroDeCaptacionesHeredadas` y las de la 013; `EntregaVigente` sube a K11; que aplican sobre la 013 lo prueba la corrida e2e final (research E227, E228)
- [x] T226 [P] Manual para la contadora y el jefe de captaciones (productos, intereses, retención, GMF, CDAT, aportes, pendientes de validar) en `docs/manual/cartera-captaciones.md` — hecho: índice y ocho manuales `cartera-captaciones-*.md`, y `cartera.md` al día (research E235)
- [x] T227 [P] Ampliar `docs/manual/contabilidad-contrato-de-contabilizacion.md` con la matriz de captaciones y las cuentas CUIF sugeridas — hecho: §10 (research E235, E236)
- [x] T228 [P] Runbook de puesta en marcha de captaciones (diagnóstico, retiro heredado, carga, cuadre, activación por naturaleza) en `docs/operaciones/captaciones-puesta-en-marcha.md` — hecho, más `captaciones-despliegue.md` y la P20 de `estado-y-pendientes.md` (research E235)
- [ ] T229 Actualizar `CLAUDE.md` (sección de la feature 014 y totales remedidos con los comandos de la tabla «Totales») — totales medidos el 2026-10-19; el texto lo escribe el dueño
- [ ] T230 Correr todas las pruebas sin contenedores y las e2e completas («Captaciones e2e» y las de la 013) en PostgreSQL y SQL Server, y registrar las cifras — las cuatro suites sin contenedores verdes el 2026-10-19; **e2e en la corrida final** (la lanza el dueño)
- [x] T231 Recorrer `specs/014-ahorros-aportes-cdt/quickstart.md` de punta a punta y corregir lo que no cuadre — hecho en papel (research E235)
- [x] T232 Verificar que ninguna pantalla, informe, plantilla ni documento nombra al sistema anterior (búsqueda en `src/` y `docs/`) — hecho; `TextosDeCarteraTests` ampliada (research E230)
- [x] T233 Prueba que cuenta los casos dorados de captaciones (mínimo 40, SC-002) en `TD/Lending/Deposits/CasosDoradosDeCaptaciones.cs` — hecho: 67 casos (research E229)
- [ ] T234 **Dueño**: medir con usuarios de la cooperativa piloto los tiempos de apertura y caja (SC-004) y el éxito al primer intento (SC-018), y registrar el resultado en `docs/operaciones/captaciones-puesta-en-marcha.md`
- [ ] T235 **Dueño y contadora**: validar parámetros «pendiente de validar» (P2, P3, P8–P12, P26), la matriz y los casos dorados; cotejar los layouts SICSES y exógena
- [ ] T236 **Dueño**: segundo revisor y respaldos por base antes de aplicar `RetiroDeCaptacionesHeredadas` fuera de local; promoción a DEV y QA
  — el 2026-10-18 el dueño se nombró segundo revisor y autorizó (D15); queda la promoción

---

## Decisiones del dueño del 2026-10-18 (research §13)

Construidas en la misma tanda (rama `014-ahorros-aportes-cdt`). Las e2e se escribieron y compilan; se corren una sola vez al final de todas las fases.

- [x] T237 D14: requisitos de activación de captaciones y aportes en todos los ambientes (sin `Waived`; fuera de producción cuentan los dobles) y fixture
  `CaptacionesE2E.RequisitosDeCaptacionesAsync` (enmienda E32)
- [x] T238 D15: T005 resuelto por el dueño; §9a de `docs/operaciones/captaciones-retiro-heredado.md` (la guarda no se tocó)
- [x] T239 D17: `CompanyAdmin` sin `Lending.NegotiatedRates.Approve`
- [x] T240 D18: roles sugeridos por módulo (`DeCreditos`, 13; `DeCaptaciones`, 4) y requisito `DepositRolesCreated` (enmienda E07)
- [x] T241 D19: Tesorería paga créditos con `ProveedorDeObligaciones` adoptado; `PagoReversadoPorCartera` también a Tesorería
- [x] T242 D20: la ficha de la persona cuenta sólo lo del titular; cuentas como cotitular en ventana aparte (`GET /api/core/people/{id}/co-held-accounts`)
- [x] T243 D23: verificado que el tipo de comprobante y el modo de paso de los hechos de captaciones son editables por la cooperativa (sin código)
- [x] T244 D29: `Aportes.TopeCuotaFondoEmpleados` con defecto 0,10 (el `LIMITE` nacional sigue acotándolo: pregunta abierta)
- [x] T245 D34: constancia de beneficiarios exigida al abrir la cuenta (`Lending.Savings.BeneficiaryRecordRequired`)
- [x] T246 D35: plantillas de aviso editables por pantalla con las variables de la 014
- [x] T247 D36: archivo de pagaduría por grupo con cuotas de cuentas, mensajes v2 y respuesta repartida por código (cierra T091)
- [x] T248 D37: política de Nómina `PrelacionDescuentosDeCartera` con vigencia (defecto aportes → créditos → ahorros)
- [x] T249 D39: pérdida esperada descuenta los aportes del valor expuesto según el Anexo 2, num. 4.2, y el Anexo 1, num. 5.1 (corrige E62)
- [x] T250 D41: parámetro `Captaciones.OrdenDeFondosAlRetiro` (defecto aportes → ahorros)
- [x] T251 D43: el mínimo irreducible retiene sólo los aportes
- [x] T252 D44: reverso tardío reabre la cuenta con saldo a favor, permiso `Lending.MembershipWithdrawals.ReverseAfterPayment`, alerta y correo a revisoría,
  gerencia y `Captaciones.DestinatariosReversoTardio` (enmienda E69)
- [x] T253 D45: cruce y pago del crédito en la misma unidad contable; al liquidar se elige qué pasa con el saldo pendiente (`PendingLoanBalanceHandling`)
- [x] T254 D46: `RetiroDeAsociadoAprobado.withdrawalReasonCode` por `Captaciones.MotivosDeRetiroDeAsociados`; Asociados llena `WithdrawalReasonId`
- [x] T255 D51: Comercial factura los cargos gravados (cierra T117)
- [x] T256 D52: doble de Tesorería como recaudador de cuentas (cierra T113)
- [x] T257 D53: aprobación de retiros y consignaciones en efectivo configurable por cooperativa (`/cartera/captaciones/aprobaciones`)
- [x] T258 D54: redondeo del GMF, débito automático como «retiro» y canje de cheques como parámetros editables (pendientes de validar)
- Sin cambio, anotadas en research §13.3: D16 (tarjeta débito, enmienda E14), D21, D22, D24 (AH/CD sólo para migración y saldos iniciales), D25–D28,
  D30–D33, D38 (la definitiva recauda sólo créditos), D40 (excepción de arrastre correcta) y D42.

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
