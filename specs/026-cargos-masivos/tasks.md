# Tareas: 026 — Cargos masivos de Cartera

**Entrada**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md)
**Pruebas**: unitarias y de arquitectura con cada fase; las de punta a punta (contenedores) una sola vez al final (fase 9).
Rutas: `Dom` = `src/Core/IngenIA365ERP.Domain`, `App` = `src/Core/IngenIA365ERP.Application`, `Pers` = `src/Infrastructure/IngenIA365ERP.Persistence`,
`API` = `src/Presentation/IngenIA365ERP.API`, `Sh` = `src/Presentation/IngenIA365ERP.Shared`.

## Fase 1 — Preparación

- [ ] T001 Enums nuevos y valores nuevos: `LoanOrigin.BulkCharge = 4`, `DisbursementForm.Charge = 5`, `TermsVersionCause.BulkCharge = 13` en `Dom/Enums/Lending/LoanEnums.cs`; `LendingProcessKind.BulkCharge = 9`, `BulkChargeVoid = 10` en `Dom/Enums/Lending/ProcessEnums.cs`; `BulkChargeRunStatus`, `BulkChargeLineStatus`, `BulkChargeSource`, `BulkChargeChannelMode`, `BulkChargeCounterpart` en `Dom/Enums/Lending/BulkChargeEnums.cs`
- [ ] T002 Revisar los `switch` sobre los enums de T001 que fallen al compilar (`dotnet build IngenIA365ERP.slnx`) y darles su caso

## Fase 2 — Fundamentos (bloquea todo)

- [ ] T003 Entidades `BulkChargeRun` y `BulkChargeLine` con transiciones y `MotivosDeCargoMasivo` en `Dom/Entities/Lending/BulkCharges/`; `Loan.BulkChargeLineId` en `Dom/Entities/Lending/Loans/Loan.cs`
- [ ] T004 [P] Reglas puras `ReglasDeCargoMasivo` (exclusión del asociado, forma de pago resultante, primer pago en el ciclo, `HoldsPeriod`) en `Dom/Lending/BulkCharges/ReglasDeCargoMasivo.cs`, con casos en `tests/IngenIA365ERP.Domain.Tests/Lending/BulkCharges/ReglasDeCargoMasivoTests.cs`
- [ ] T005 Configuraciones EF `BulkChargeRunConfiguration` y `BulkChargeLineConfiguration` (índice único filtrado `(CreditLineId, PersonId, Period) WHERE HoldsPeriod`) en `Pers/Configurations/Lending/BulkCharges/`; `Loan.BulkChargeLineId` en su configuración; `DbSet` en `App/Common/Interfaces/IApplicationDbContext*.cs` y el contexto
- [ ] T006 Contratos `CargoMasivoRegistradoV1` y `CargoMasivoAnuladoV1` en `App/Common/Integration/Contracts/Lending/` y su entrada (`CMS`, `CMA`) en `CatalogoDeMensajesDeCarteraV1`

## Fase 3 — HU1: preparar la línea (P1)

**Objetivo**: marcar una versión de línea para cargos masivos con sus valores por defecto y opciones de centrales y cobranza.
**Prueba independiente**: una versión con codeudores no se puede marcar; una marcada guarda y copia sus valores; las versiones viejas conservan su huella.

- [ ] T007 [US1] Cinco columnas en `CreditLineVersion` (`AllowsBulkCharge`, `BulkChargeDefaultAmount`, `BulkChargeDefaultInstallments`, `BulkChargeReportsToBureaus`, `BulkChargeEntersCollection`), mapeo en `Registrar`, huella con `.Si(AllowsBulkCharge, …)` y validación `Lending.CreditLine.BulkChargeIncompatible` en `Dom/Entities/Lending/Configuration/CreditLineVersion.cs`
- [ ] T008 [US1] Borrador y copia desde la vigente en `Dom/Lending/Configuration/BorradoresDeLinea.cs`
- [ ] T009 [US1] `HasDefaultValue` de las columnas en `Pers/Configurations/Lending/Configuration/CreditLineVersionConfiguration.cs`
- [ ] T010 [US1] Input, DTO y `Borrador()` en `App/Lending/Configuration/CreditLineDtos.cs`; lectura en `CreditLineQueries.cs`
- [ ] T011 [US1] Columnas de la plantilla `lending.credit-lines` en `App/Lending/Imports/CatalogoDePlantillasDeCartera.cs` e `ImportCreditLinesCommand.cs`
- [ ] T012 [P] [US1] Pruebas de validación y huella en `tests/IngenIA365ERP.Domain.Tests/Lending/Configuration/CargoMasivoEnLaLineaTests.cs`

## Fase 4 — HU3: contabilidad (P1)

**Objetivo**: los hechos del cargo resuelven débito a capital y crédito a la contrapartida, y la corrida sale en un comprobante `CO`.
**Prueba independiente**: tres hechos con el mismo alcance producen un solo comprobante `CO` cuadrado.

- [ ] T013 [US3] Rol `ContrapartidaDelCargo` en `App/Accounting/Lending/Reglas/RolesDeCuentaDeCartera.cs` (antes del bloque de captaciones; `RolesDeCredito = 29`)
- [ ] T014 [US3] Dos operaciones en `App/Accounting/Lending/Reglas/OperacionesDeCartera.cs` y en `Pers/Seeding/Parametric/LendingFactTypesSeeder.cs`
- [ ] T015 [US3] Casos de `CargoMasivoRegistrado` y `CargoMasivoAnulado` en `App/Accounting/Lending/Contabilizacion/ConstructorDeLineasDeCartera.cs`
- [ ] T016 [P] [US3] `CO` «Cargos masivos» en `Pers/Seeding/Parametric/Data/voucher-types.json` y los mapeos en `lending-voucher-mappings.json`
- [ ] T017 [US3] Fila sugerida por línea marcada en `App/Accounting/Lending/Reglas/GetLendingRulesTemplateQuery.cs`; combinaciones de líneas marcadas en la implementación de `IDimensionesDeCartera`
- [ ] T018 [US3] Hecho de muestra `HechoDeCargoParaValidar` y origen `CargoMasivo` en `App/Lending/Integration/ValidacionPreviaContableDeCartera.cs`
- [ ] T019 [P] [US3] Pruebas del constructor y del resumen en `tests/IngenIA365ERP.Application.Tests/Accounting/Lending/CargoMasivoContableTests.cs`; ajustar contadores de roles y hechos donde los haya

## Fase 5 — HU2: la corrida (P1)

**Objetivo**: preparar, previsualizar, confirmar y ejecutar la corrida.
**Prueba independiente**: una corrida de tres asociados (uno retirado) crea dos obligaciones, omite uno y se puede repetir sin duplicar.

- [ ] T020 [US2] Parámetro `Segregacion.CargoMasivoPorOtraPersona` en `Dom/Lending/Parameters/ParametrosDeCartera.cs`
- [ ] T021 [US2] Permisos `Lending.BulkCharges.Read/Run/Void` en `src/Infrastructure/IngenIA365ERP.Identity/Seed/LendingPermissionCatalogSeeder.cs` y `App/Security/Roles/Plantillas/PerfilesSugeridos.cs`; contadores en `tests/.../PermisosDeCarteraTests.cs` y `SeguridadYHabeasDataDeCarteraTests.cs`
- [ ] T022 [US2] `PoblacionDeCargoMasivo` (activos, filtros, exclusiones, unicidad, forma de pago y convenio) en `App/Lending/BulkCharges/PoblacionDeCargoMasivo.cs`
- [ ] T023 [US2] `VistaPreviaDeCargoMasivo` (líneas `Pending`/`Skipped`, totales, huella, evaluación contable) en `App/Lending/BulkCharges/VistaPreviaDeCargoMasivo.cs`
- [ ] T024 [US2] Comandos crear, cambiar, recalcular, descartar y confirmar con validadores en `App/Lending/BulkCharges/BulkChargeRunCommands.cs`
- [ ] T025 [US2] Consultas opciones, lista, detalle y líneas en `App/Lending/BulkCharges/BulkChargeRunQueries.cs`
- [ ] T026 [US2] `NacimientoDeCargoMasivo` (crédito, operación `Charge`, condiciones `BulkCharge`, plan, posición, vínculo de nómina o pagaduría, hecho sellado) en `App/Lending/BulkCharges/NacimientoDeCargoMasivo.cs`
- [ ] T027 [US2] `EjecutorDeCargoMasivo` (tandas, transacción por línea, cursor, errores, cierre con lote contable y auditoría) en `App/Lending/BulkCharges/EjecutorDeCargoMasivo.cs`; registrar en `App/DependencyInjection.cs` y en `ConductorDeCorridasDeCartera.Clases`
- [ ] T028 [US2] Eventos de auditoría `Lending.BulkCharge.*` en `App/Common/Audit/AuditEventTypes.cs`
- [ ] T029 [US2] Endpoints en `API/Endpoints/Lending/BulkChargesEndpoints.cs`; agregar el archivo a `LosComandosDeCarteraLlevanClave`
- [ ] T030 [P] [US2] Pruebas de población, vista previa, segregación y confirmación en `tests/IngenIA365ERP.Application.Tests/Lending/BulkCharges/`

## Fase 6 — HU4: plano (P2)

- [ ] T031 [US4] Plantilla `lending.bulk-charges` en `CatalogoDePlantillasDeCartera.cs` e `ImportBulkChargeLinesCommand` en `App/Lending/BulkCharges/ImportBulkChargeLinesCommand.cs`; rutas `MapPlantilla` en `BulkChargesEndpoints.cs`
- [ ] T032 [P] [US4] Pruebas del plano en `tests/IngenIA365ERP.Application.Tests/Lending/BulkCharges/PlanoDeCargoMasivoTests.cs`

## Fase 7 — HU5: anulación (P2)

- [ ] T033 [US5] `AnulacionDeCargoMasivo` en `App/Lending/BulkCharges/AnulacionDeCargoMasivo.cs`
- [ ] T034 [US5] `VoidBulkChargeLineCommand` y `VoidBulkChargeRunCommand`; clase `BulkChargeVoid` del ejecutor
- [ ] T035 [P] [US5] Pruebas de anulación en `tests/IngenIA365ERP.Application.Tests/Lending/BulkCharges/AnulacionDeCargoMasivoTests.cs`

## Fase 8 — Centrales, cobranza y origen (HU1, D4)

- [ ] T036 [P] [US1] `BulkChargeNotReported` en `Dom/Entities/Lending/Documents/RegulatoryFileLine.cs` y la regla en `App/Lending/Regulatory/ProyeccionCentralDeRiesgo.cs`; textos en `RegulatoryFileCommands.cs` y `Sh/Services/Cartera/RegulatorioDeCarteraClient.cs`
- [ ] T037 [P] [US1] Exclusión de cobranza en `App/Lending/Collections/CobranzaDelCicloDiario.cs` y `CollectorAssignmentCommands.cs`
- [ ] T038 [P] [US1] Textos de origen y causa en `Sh/Services/Cartera/TextosDeCreditos.cs`; enlace a la corrida en `Sh/Pages/Cartera/CreditoDetalle.razor`

## Fase 9 — HU2–HU6: pantallas y cliente

- [ ] T039 [US2] Cliente `CargosMasivosClient` y DTOs en `Sh/Services/Cartera/`; registro en Web, Web.Client y MAUI
- [ ] T040 [US2] Página `Sh/Pages/Cartera/CargosMasivos.razor` (lista y «Nueva»)
- [ ] T041 [US2] Página `Sh/Pages/Cartera/CargoMasivoDetalle.razor` (pasos, vista previa, confirmar, avance, líneas, anular, exportar, plano)
- [ ] T042 [US1] Editor de versión de línea y resumen: `Sh/Components/Cartera/EditorDeVersionDeLinea.razor`, `ResumenDeVersion.razor`, `Sh/Services/Cartera/CarteraDtos.Lineas.cs`
- [ ] T043 [US6] Menú en `Sh/Layout/NavMenu.razor`, `tools/scripts/generar-mapa-del-menu.py`, `LasPantallasDeCarteraEstanEnElMenu`
- [ ] T044 [US6] Exportación Excel y PDF del detalle (`TablaExportable`) en `App/Lending/BulkCharges/BulkChargeRunQueries.cs` y la ruta `/export`

## Fase 10 — Migración, documentación y cierre

- [ ] T045 Migración par `CargosMasivos` con `tools/scripts/add-migration.ps1 -Name CargosMasivos -Context Application`; revisar que sólo traiga lo de la 026; `check-migration-parity.ps1`
- [ ] T046 [P] Manual `docs/manual/cartera-cargos-masivos.md`, entrada en `docs/manual/cartera.md` y en `Sh/Services/Manual/ManualCatalogo.cs`
- [ ] T047 [P] CLAUDE.md: viñeta «Cargos masivos (feature 026)»
- [ ] T048 Compilar la solución y correr Domain, Application, Architecture y Shared
- [ ] T049 E2e `tests/IngenIA365ERP.API.IntegrationTests/Lending/CargosMasivosTests.cs` en PostgreSQL y SQL Server (una sola vez, al final)

## Dependencias

- T001–T006 antes de todo. HU1 (T007–T012) y HU3 (T013–T019) pueden ir en paralelo; HU2 (T020–T030) necesita las dos.
- HU4 y HU5 necesitan HU2. Fase 8 necesita T007. Pantallas después de los endpoints. Migración (T045) después de todas las entidades y columnas.

## MVP

HU1 + HU3 + HU2 (fases 1–5 con la migración): cargar FDOSOL de diciembre desde la API y, con la fase 9, desde la pantalla.
