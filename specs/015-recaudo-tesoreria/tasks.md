---

description: "Lista de tareas de la feature 015 — Recaudo y Tesorería"
---

# Tasks: Recaudo y Tesorería — caja, medios de pago, bancos, pagadurías y giros

**Input**: Design documents from `specs/015-recaudo-tesoreria/`
**Prerequisites**: plan.md, spec.md, decisiones-transversales.md (DT), research.md (R-01 a R-40), data-model.md,
contracts/{api,mensajes,contabilidad,contratos-v2,interfaces,archivos-y-plantillas}.md, quickstart.md

**Total**: 497 tareas (las 486 del 2026-10-22 más once con sufijo agregadas el 2026-10-23 tras el `/speckit-analyze`;
ningún número existente cambió).

**Tests**: se incluyen; la constitución, `CLAUDE.md` y `quickstart.md` los exigen. Son de cuatro clases:
- casos dorados JSON de los motores puros (imputación de medios, arqueo, señales de efectivo, huella bancaria, cruce de
  la conciliación, transiciones del resultado, GMF, armado de lotes) en `tests/IngenIA365ERP.Domain.Tests/{Receipts,Treasury}/Casos/`;
- pruebas de Application (InMemory, reloj fijo) y de arquitectura (las dieciséis nuevas y las catorce ampliadas de
  plan.md, «Testing»);
- e2e con `CentralIdentityApiFixture` en PostgreSQL **y** SQL Server, en la colección «Recaudo y Tesorería e2e» y en las
  colecciones de Cartera y Captaciones migradas a los módulos reales;
- rendimiento con `RUN_PERF_TESTS=1` (SC-010, SC-011, SC-023, SC-031, SC-032).

Toda prueba se escribe antes de su implementación y debe fallar primero. **Las e2e contra base de datos se escriben durante
cada fase y se corren una sola vez, al final de R0/T0** (decisión del dueño), en la fase 22. Hay **dos excepciones
declaradas** (plan.md, «Testing»), y ninguna otra:
1. la **e2e de humo de la caja** (T0183c), al cierre del bloque 2 y antes del hito del 04/11, para que el riesgo de la caja
   (contratos, cerrojos, numeración) aparezca con tres semanas de margen (riesgo R1 del analyze);
2. la **puerta de FR-224** (T0301): antes de borrar el código de los dobles se corren, en los dos motores, sólo las e2e de
   Cartera y Captaciones ya migradas, porque el borrado no puede salir sin ellas en verde.

**Migraciones** (decisión del dueño del 2026-10-23): los pares de R0/T0 se regeneran hasta el despliegue en QA del 10/11
(T0271) y ahí se **congelan** con sus huellas; desde ese día no se regenera nada y cada cambio de R0/T0 es un par nuevo y
aditivo (T0281, T0291, T0306), cuyas huellas sella T0337.

**Organization**: las fases siguen las entregas y, dentro de R0/T0, los cuatro bloques de construcción de plan.md
(«Fases de construcción de R0/T0»); dentro de cada bloque, las historias que dependen unas de otras.

| Entrega | Bloque (plan.md) | Contenido | Fases |
|---|---|---|---|
| — | — | Setup | 1 |
| R0 + T0 | 1 · Plataforma y esqueleto (22/10–27/10) | Plataforma fundacional, migración `PlataformaParaRecaudoYTesoreria` | 2 |
| R0 + T0 | 2 · Caja y giros (28/10–04/11) | US12, US1, US2, US3, US10, US11, US26, US5 | 3 a 10 |
| R0 + T0 | 3 · Bancos, pagadurías, cheques y contabilidad (05/11–10/11) — versión al paralelo en QA | US6, US7, US15, US19, US14 (+ contingencia de US2), US8 (+ E-01 c) | 11 a 16 |
| R0 + T0 | 4 · Conciliación, salida en vivo y retiro de los dobles (11/11–19/11) | US25, US13, US9, US21 (parte R0/T0), US24 | 17 a 21 |
| R0 + T0 | 5 · Verificación y congelamiento (20/11–22/11) y salida (23/11–01/12) | e2e una sola vez, rendimiento, congelamiento, ensayo, producción | 22 |
| R1 | — | base R1, US1, US3, US4, US6, US7, US14, US23, US21 (Recaudo) | 23 a 31 |
| T1 | — | base T1, US19, US26, US21 (Tesorería) | 32 a 35 |
| R2 | — | base R2, US15, US16, US21 (canales) | 36 a 39 |
| T2 | — | base T2, US17, US18, US20, US22, US21 (resto) | 40 a 45 |
| Después de T2 | — | Retiro de la Tesorería heredada (destructivo, commit propio) | 46 |
| — | — | Polish | 47 |

**R0 y T0 van juntas** y son el recorte mínimo para el 01/12/2026: lo de R1, T1, R2 y T2 no entra antes de la salida. La
Tesorería heredada (`TRS_`, `/api/treasury/{checks,concepts,invoices,cash-flow}`) **no se toca** hasta su fase de retiro.

**Nombres**: todo nombre de tabla, entidad, enumeración, mensaje, permiso, ruta, parámetro, alerta, migración y prueba
de arquitectura sale de `decisiones-transversales.md` §2 (manda sobre cualquier otra redacción) y de `data-model.md`
(§20 enumeraciones con sus números, §21 errores, §22 alertas, §23 parámetros, §25 migraciones). Lo que no está en DT va
marcado **(nuevo)** y se lleva a DT en la misma tarea.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo, porque toca archivos distintos y no depende de otra tarea incompleta de la misma fase.
- **[Story]**: la historia de spec.md (US1…US26). No va en Setup, en la plataforma fundacional, en la base de una entrega,
  en la verificación de R0/T0, en el retiro de lo heredado ni en Polish.
- «(dueño, fecha límite dd/mm)»: la tarea es **del dueño** o de su equipo (oficial de cumplimiento, contadora,
  tributarista). Corresponde a un pendiente P1–P19 de la spec. **Su ausencia no bloquea el código**: rige el valor por
  defecto de research.md («Pendientes del dueño que tocan el diseño»), que la tarea cita. Lo que bloquea la salida lo dice.
- «(ambiente)»: toca DEV o QA (despliegue por GitOps). «(producción)»: toca producción; sólo con respaldo por base,
  autorización expresa del dueño y GitOps.
- «**ruta crítica**»: la tarea está en la cadena que fija la fecha del 01/12 (sección «Ruta crítica»).

## Path Conventions

Clean Architecture del repositorio, con rutas relativas a la raíz del worktree:
- `src/Core/IngenIA365ERP.Domain` (entidades en `Entities/{Receipts,Treasury}/<Área>`, enums en `Enums/{Receipts,Treasury}`,
  motores puros en `Receipts/{Imputacion,Arqueo,Cumplimiento,Huellas}` y `Treasury/{Resultados,Lotes,Conciliacion,Gmf}`,
  `Common/Taxation/CalculoDeGmf.cs`);
- `src/Core/IngenIA365ERP.Application` (`Receipts/<Área>`, `Treasury/<Área>`, `Accounting/{Receipts,Treasury}`, puertos en
  `Common/Integration/{Receipts,Treasury,Accounting}`);
- `src/Infrastructure/IngenIA365ERP.{Persistence,Persistence.Migrations.PostgreSql,Persistence.Migrations.SqlServer,Identity}`;
- `src/Presentation/IngenIA365ERP.{API,Shared,Web,Web.Client,App}`;
- `tests/IngenIA365ERP.{Domain,Application,Architecture,Shared,API.Integration}Tests`.

El árbol detallado está en plan.md, «Project Structure», y en DT §2.2. Las migraciones se generan **siempre en par** con
`tools/scripts/add-migration.ps1 -Name <Nombre>` y se verifican con `tools/scripts/check-migration-parity.ps1`.

---

## Phase 1: Setup (Shared Infrastructure)

**Goal**: dejar el worktree verificado, la línea base de pruebas anotada, los esqueletos de las pruebas de arquitectura
nuevas compilando con sus listas vacías (sin `return` temprano) y la colección e2e «Recaudo y Tesorería e2e» con su
prueba humo en los dos motores.

**Independent Test**: `dotnet build IngenIA365ERP.slnx` con 0 errores; `dotnet test tests/IngenIA365ERP.Architecture.Tests`
verde con las clases nuevas presentes (cada una recorre su lista vacía); la prueba humo de la colección nueva crea su
cooperativa aislada en PostgreSQL y SQL Server; la línea base anotada coincide con lo que corre hoy.

### Tests for Setup

- [X] T0001 [P] Crear los esqueletos de frontera de tests/IngenIA365ERP.Architecture.Tests/Principles/: `RecaudoYTesoreriaSeHablanPorSusPuertos.cs`, `RecaudoNoConoceContabilidad.cs`, `TesoreriaNoConoceContabilidad.cs`, `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas.cs`, `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras.cs` (R-05, R-22, R-37; FR-003, FR-004) con su lista **vacía** y un `[Fact]` que la recorre y afirma la regla; estilo de `InventarioNoConoceContabilidadNiCartera.cs` (ArchUnitNET); el `<summary>` dice qué fase llena cada lista
- [X] T0002 [P] Crear los esqueletos de operación de tests/IngenIA365ERP.Architecture.Tests/Principles/: `RecaudoYTesoreriaNoTienenValoresLegalesFijos.cs` (literales admitidos los de `LaNominaNoTieneValoresLegalesFijos`; recorre `Domain/{Receipts,Treasury}` y `Application/{Receipts,Treasury}` nuevos, excluida la raíz heredada de `Treasury`), `LosComandosDeRecaudoYTesoreriaLlevanClave.cs`, `LosCerrojosDeRecaudoYTesoreriaVanEnOrden.cs`, `LasCantidadesYMontosDeCajaTienenSuPrecision.cs` (R-12, R-33, R-37; FR-005, FR-007, FR-009, FR-170; SC-020)
- [X] T0003 [P] Crear los esqueletos de procesos y retiro de tests/IngenIA365ERP.Architecture.Tests/Principles/: `NoHayDoblesDePrueba.cs` (lista vacía de tipos permitidos como `IDestinoDeMensajes` de `Receipts` y `Treasury`; la llenan las fases 7 y 12), `NingunProcesoAplicaPagosNiInformaResultados.cs`, `LoDeRecaudoYTesoreriaNoSeReversaDesdeComprobantes.cs`, `LaTesoreriaHeredadaNoVuelve.cs` (lista de las tres tablas `TRS_` vacía hasta la fase 46) (R-26, R-37; FR-145, FR-161, FR-174, FR-223)
- [X] T0004 [P] Crear tests/IngenIA365ERP.Architecture.Tests/Principles/TextosDeRecaudoYTesoreriaTests.cs (molde `TextosDeCarteraTests.cs`): ningún `.razor`, plantilla, informe ni manual de `Pages/Recaudo`, `Pages/Tesoreria`, `Reports/DocumentosDe{Recaudo,Tesoreria}Report.cs` y `docs/manual/{recaudo,tesoreria}-*.md` dice «SOLIDO» ni muestra códigos técnicos; y `LasPantallasDeRecaudoYTesoreriaEstanEnElMenu.cs` (molde `LasPantallasDeCarteraEstanEnElMenu.cs`) con su lista vacía (FR-162; CH14)
- [X] T0005 Anotar en el `<summary>` de cada esqueleto el árbol de carpetas que recorre (DT §2.2, plan.md «Source Code»), **sin crear carpetas vacías ni archivos marcadores**: cada carpeta nace con su primer archivo en la tarea que lo crea; la raíz heredada de `Domain/Entities/Treasury` y `Application/Treasury/{Checks,Invoices,TreasuryConcepts}` queda excluida por nombre (R-04)

### Implementation for Setup

- [X] T0006 Verificar el worktree: `git status` limpio en `015-recaudo-tesoreria`, los tres commits de diseño (`c2d58e6f` spec, `a33d2bd1` correcciones, `9fb502c3` plan) sobre la base; `git fetch` y confirmar que `origin/develop` no avanzó (si avanzó, rebasar antes de empezar, nunca a mitad de una fase); `dotnet build IngenIA365ERP.slnx` con 0 errores; Docker Desktop encendido (apagado por defecto en la máquina de desarrollo)
- [X] T0007 Anotar la línea base en specs/015-recaudo-tesoreria/quickstart.md §1 (subsección nueva «Lo que debe seguir verde»): conteo por proyecto de `dotnet test tests/IngenIA365ERP.{Domain,Application,Architecture,Shared}.Tests` con la fecha y el commit (las e2e no se corren ahora: decisión del dueño); es la cifra contra la que cada fase comprueba que nada de Cartera, Captaciones, Inventario, Nómina ni Contabilidad se cayó
- [X] T0008 Preparar tests/IngenIA365ERP.API.IntegrationTests/Identity/CentralIdentityApiFixture.cs para Recaudo y Tesorería: `ScheduledTasks`, `Integration:Dispatcher` y `AuditForwarder` siguen apagados (las e2e los corren a mano); ayudante de pasada de tareas por nombre (`recaudo.*`, `tesoreria.*`) para conducir el lote del día, el cuadre diario y la vigilancia; sin claves de dobles nuevas
- [X] T0009 [P] Crear tests/IngenIA365ERP.API.IntegrationTests/Receipts/RecaudoYTesoreriaE2E.cs con la colección **«Recaudo y Tesorería e2e»** (`RecaudoYTesoreriaCollection : ICollectionFixture<CentralIdentityApiFixture>`, molde `Lending/CarteraE2E.cs`) y la clase estática `RecaudoYTesoreriaE2E`: `ConClave`, `CooperativaAisladaAsync(fx, nombre, contabilidadIniciada)`, `TokenMaestroAsync`, `EnviarAsync`/`LeerAsync`/`CodigoDeErrorAsync` delegados; el esqueleto de `RegistrarYActivarAsync(coop)` (lo completa la fase 19); una prueba humo que crea una cooperativa aislada (se escribe ahora y corre en la fase 22)
- [X] T0010 Registrar en specs/015-recaudo-tesoreria/research.md («Pendientes del dueño») la tabla de seguimiento P1–P19 con columnas «fecha límite», «rige mientras tanto», «bloquea la salida» y «respuesta», copiada de la spec y de research.md, para que cada tarea del dueño de esta lista la actualice al recibir la respuesta
- [ ] T0011 P16 · Ajustar el plan de implantación de COOFLOPAL (`docs/implementacion/generar-plan.py`): capacitación de Cartera en recaudos (K19) y de Tesorería (M19) con R0/T0 disponibles en QA, paralelo (K24, K26) en QA del 11/11 al 27/11, tareas de la Tesorería heredada (M10, M19, M22, M28, M33) conservadas, ensayo general del 23/11 al 25/11 y go/no-go del 28/11; regenerar el libro (dueño, fecha límite 06/11; rige mientras tanto el calendario de la spec)

---

## Phase 2: Foundational (Blocking Prerequisites) — plataforma `PlataformaParaRecaudoYTesoreria` y esqueleto de los dos módulos

**Goal**: construir, en la plataforma y no en los módulos, lo que toda historia de R0/T0 necesita (E-06, FR-169): las
cuentas bancarias de la persona con doble control (`COR_PersonBankAccounts`), la lista de go/no-go (`COR_GoLiveChecklists`),
los ámbitos nuevos y renombrados del motor de archivos, los formatos genéricos, los contextos de listas, los tipos de
alerta y de adjunto; y el esqueleto de `Receipts` y `Treasury`: códigos de módulo, permisos y plantillas de rol sembrados
completos, parámetros con vigencia, auditoría encadenada, precisión, cerrojos en orden fijo, numeradores, interfaces de
los puertos, grupos de rutas y de menú, filas de configuración (`RCD_Setup`, `TES_Setup`) y la primera generación de
`RecaudoNucleo` y `TesoreriaNucleo`.

**Independent Test**: sin ninguna operación de caja ni de giro, en los dos motores: (a) una persona pide el alta de una
cuenta bancaria de un beneficiario con su certificación, la aprueba otra y queda `Active` con su `PayableFrom`; quien la
pidió no puede aprobarla (`Core.PersonBankAccount.SelfApproval`); (b) la migración copia los campos sueltos de proveedor
como `Imported`; (c) un formato `EXTRACTO-GENERICO` lee un CSV de cinco movimientos; (d) los permisos `Receipts.*` y
`Treasury.*` y las siete plantillas de rol existen tras arrancar la API; (e) una ruta de `/api/receipts` sin permiso
responde 403 y una de una cooperativa sin activar responde `Receipts.NotActivated`; (f) las pruebas de arquitectura
siguen verdes y Cartera, Captaciones, Inventario, Nómina y Contabilidad conservan su línea base.

### Tests for Foundational

- [X] T0012 [P] Escribir tests/IngenIA365ERP.Application.Tests/Core/PersonBankAccounts/CuentasBancariasDePersonaTests.cs (R-21; FR-210; data-model §16.1): alta `PendingApproval` con certificación obligatoria (`Core.PersonBankAccount.CertificateRequired`), aprobación por otra persona (`.SelfApproval` comparando `SEC_Users.Id` por persona), `PayableFrom` = aprobación + `CuentasDestino.DiasDeEspera` (sin valor = sin espera), un cambio crea fila nueva y deja la anterior `Retired` (`ReplacesId`), rechazo, retiro, lectura sensible auditada `Core.PersonBankAccount.Read` y escritura `Core.PersonBankAccount.Changed`
- [X] T0013 [P] Escribir tests/IngenIA365ERP.Application.Tests/Core/PersonBankAccounts/VerificacionDeCuentaDestinoTests.cs para `ICuentasBancariasDePersona.VerificarAsync` (interfaces.md §5): registrada y activa, en espera (`PayableFrom` futuro), cambiada hace N días (`CuentasDestino.DiasDeMarca`), compartida con otro beneficiario, igual a la de un usuario interno, importada sin certificación (FR-210, SC-026)
- [X] T0014 [P] Escribir tests/IngenIA365ERP.Application.Tests/Common/GoLive/ListaDeSalidaTests.cs (R-28; FR-227): lista por cooperativa y salida; condiciones `Automatic` que aporta cada `IVerificacionDeSalida` y `Manual` con evidencia; un `Red` nunca se oculta; decidir `Go` con un rojo exige `Platform.GoLive.Override` con motivo; `PartialGo` con escenario adjunto; alerta `Plataforma.GoLiveEnRojo`
- [X] T0015 [P] Escribir tests/IngenIA365ERP.Application.Tests/Common/BankFiles/AmbitosDeRecaudoYTesoreriaTests.cs (R-20; archivos-y-plantillas.md §1–§4): `FlatFileReader` lee con `BankStatement = 7` los campos de §2 (fecha, signo o débito/crédito, referencia, secuencia, consignante) y `TreasuryPaymentResponse = 9`; `FlatFileWriter` escribe `TreasuryPayments = 3` con el contexto genérico; los cuatro genéricos sembrados (`EXTRACTO-GENERICO`, `PAGOS-GENERICO`, `RESPUESTA-PAGOS-GENERICO`, `RESPUESTA-PAGADURIA-GENERICA`) leen y escriben un ejemplo; dos formatos del mismo banco y ámbito no se cruzan (`Core.BankFileFormat.Overlaps`)
- [X] T0016 [P] Escribir tests/IngenIA365ERP.Application.Tests/Common/Parameters/ParametrosDeRecaudoYTesoreriaTests.cs (data-model §23; FR-009, FR-173, FR-178): cada clave de §23 registrada con su módulo (`RCD`, `TES`, plataforma, `ACC`), su defecto, su fuente y la marca «pendiente de validar» donde dice **P**; las «sin valor» no se siembran y no rigen; `Conservacion.Anios` rechaza un valor menor que 5; una versión nueva no es retroactiva sobre lo sellado
- [X] T0017 [P] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Common/CerrojoDeRecaudoTests.cs y tests/IngenIA365ERP.Application.Tests/Treasury/Common/CerrojoDeTesoreriaTests.cs sobre el SQL que generan `SqlDelCerrojoDeRecaudo` y `SqlDelCerrojoDeTesoreria` (molde `SqlDelCerrojoDeCartera`): una sentencia por nivel ordenada por `Id`, `FOR UPDATE` en PostgreSQL y `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` en SQL Server, niveles de data-model §0.5 en su orden, los niveles ya tomados no esperan; y la dirección Tesorería → Recaudo (CH16): un
comando de Tesorería que llama a `IResultadoDeOrdenesDeRecaudo` o a `IPartidasDesdeConciliacion` toma antes, por
`TomarCerrojosAsync`, los niveles de Recaudo de la tabla de data-model §0.5 y nunca una fila `RCD_` después de una `TES_`
(caso propio con dos transacciones cruzadas: resultado de una orden de Recaudo y anulación de la misma devolución en caja)
(R-12; FR-007, FR-170)
- [X] T0018 [P] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Common/NumeradorDeRecaudoTests.cs y …/Treasury/Common/NumeradorDeTesoreriaTests.cs: clave `RCD:{code}:{prefijo}` y `TES:{code}` en `COR_Sequences`, sin huecos, al final, un rechazo no consume número (R-13; FR-007, FR-026)
- [X] T0019 [P] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Common/ActivacionDelModuloTests.cs: con `RCD_Setup.Status = NotActivated` toda escritura de negocio de Recaudo responde `Receipts.NotActivated` y con `TES_Setup` igual `Treasury.NotActivated`; la configuración y la puesta en marcha sí se admiten antes de activar (FR-150, FR-159)
- [X] T0020 Ampliar tests/IngenIA365ERP.Architecture.Tests/Principles/LosHechosInmutablesNoSeModifican.cs con los espacios `Domain/Entities/Receipts` y `Domain/Entities/Treasury` (fuera de la raíz heredada): toda entidad marcada H en data-model §0.2 implementa `IHechoInmutable` y sólo sus columnas `[EscrituraUnica]` o declaradas mutables cambian (FR-002; Principio XI); la lista la llena cada fase que crea entidades H
- [X] T0021 Llenar las listas de `LosEndpointsProtegidosExigenPermiso` con los grupos `ReceiptsGroup` y `TreasuryGroup` y ampliar `SoloElNumeradorNumera` con `NumeradorDeRecaudo` y `NumeradorDeTesoreria` como únicos escritores de `COR_Sequences` para `RCD:` y `TES:` en tests/IngenIA365ERP.Architecture.Tests/Principles/ (R-13, R-37; FR-011, SC-018)
- [X] T0022 Ampliar tests/IngenIA365ERP.Architecture.Tests/Principles/LasPantallasDicenQueEstanCargando.cs: agregar `Recaudo` y `Tesoreria` a `ModulosMigrados` (las pantallas nuevas de `Pages/Tesoreria` quedan obligadas; las cuatro heredadas `/tesoreria/{cheques,conceptos,facturas,flujo-caja}` se eximen por nombre hasta su retiro) (R-35; FR-018)

### Implementation for Foundational

**Plataforma (E-06)**

- [X] T0023 [P] Renombrar `BankFileScope.SupplierPayments = 3` a `TreasuryPayments = 3` y agregar `BankStatement = 7`, `CollectionAgreementFile = 8`, `TreasuryPaymentResponse = 9`, `PaymentGatewayFile = 10`, `AcquirerSettlement = 11` en src/Core/IngenIA365ERP.Domain/Enums/Core/BankFiles.cs; corregir los usos del nombre viejo (el ámbito 3 no tiene filas) y los orígenes de campo de lectura de archivos-y-plantillas.md §2 en `Application/Common/BankFiles` (R-20; FR-169)
- [X] T0024 Sembrar los formatos genéricos `EXTRACTO-GENERICO` (7), `PAGOS-GENERICO` (3, CSV), `RESPUESTA-PAGOS-GENERICO` (9) y `RESPUESTA-PAGADURIA-GENERICA` (6) «pendientes de validar» en src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Parametric/BankFileFormatsSeeder.cs con `INSERT … WHERE NOT EXISTS` en los dos motores, con su huella de movimiento declarada (FR-196, FR-197, FR-201, FR-213)
- [X] T0025 [P] Agregar `Collection = 6` y `Disbursement = 7` al enum `ScreeningContext` de src/Core/IngenIA365ERP.Domain/Enums/Lending/OriginationEnums.cs (1 a 5 ya están tomados; data-model §20) y su tratamiento en src/Core/IngenIA365ERP.Application/Common/Compliance/IListasRestrictivas.cs y su implementación, con el efecto vinculante que **no** es parametrizable a «sigue» (R-24; FR-098, FR-203)
- [X] T0026 [P] Agregar los 37 tipos de alerta de data-model §22 a src/Core/IngenIA365ERP.Application/Common/Alerts/TiposDeAlerta.cs y a src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Parametric/AlertTypesSeeder.cs, con destinatarios por permiso (`IDestinatariosPorPermiso`) y filtro de alcance (FR-015, FR-169)
- [X] T0027 [P] Agregar los tipos de adjunto `ReceiptsSourceOfFunds`, `ReceiptsContingencyImage`, `ReceiptsCountMinutes`, `ReceiptsPaymasterResponse`, `TreasuryPaymentFile`, `TreasuryPaymentResponse`, `TreasuryStatement`, `TreasuryDeliveryReceipt`, `TreasuryReconciliationReport`, `PersonBankCertificate` a src/Core/IngenIA365ERP.Application/Attachments/Common/AdjuntosDeModulo.cs con su dueño, permiso de lectura y la regla «lo que genera un módulo no se borra» (data-model §16.3; FR-112, FR-156)
- [X] T0028 Crear la entidad src/Core/IngenIA365ERP.Domain/Entities/Core/PersonBankAccount.cs, los enums `PersonBankAccountState` y `PersonBankAccountOrigin` en Domain/Enums/Core y su configuración en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Core/PersonBankAccountConfiguration.cs (FK `Restrict` a `COR_People` y `COR_Banks`, índice `(BankId, AccountNumber)`, `ReplacesId`) (data-model §16.1)
- [X] T0029 Implementar en src/Core/IngenIA365ERP.Application/Core/PersonBankAccounts/ los comandos `RequestPersonBankAccountCommand`, `ApprovePersonBankAccountCommand`, `RejectPersonBankAccountCommand`, `RetirePersonBankAccountCommand` con validador, `Idempotency-Key`, auditoría y alerta `Plataforma.CuentaDestinoCompartida`, y la consulta `ListPersonBankAccountsQuery` como lectura sensible auditada (R-21; FR-210)
- [X] T0030 Implementar `ICuentasBancariasDePersona` (contracts/interfaces.md §5) en src/Core/IngenIA365ERP.Application/Core/PersonBankAccounts/CuentasBancariasDePersona.cs, con `ActivasAsync` y `VerificarAsync` (registrada, espera, cambiada, compartida, usuario interno, importada sin certificación)
- [X] T0031 Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Core/PersonBankAccountsEndpoints.cs (`GET/POST /api/core/people/{id}/bank-accounts`, `POST /api/core/person-bank-accounts/{id}/approve|reject|retire`) con `Core.PersonBankAccounts.View/Request/Approve` (api.md §15)
- [X] T0032 Crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Maestros/CuentasBancariasDePersona.razor (`/maestros/personas/{id}/cuentas-bancarias`) con su cliente `Shared/Services/Core/CuentasBancariasDePersonaClient.cs`, el enlace desde `Components/Personas/PersonaDialog` (sin escribir la persona: la persona se escribe en un solo sitio), subida de la certificación por `adjuntos.js`, aprobar/rechazar con `PermissionGate` e `IndicadorDeCarga` por zona (FR-018, FR-210)
- [X] T0033 Crear las entidades src/Core/IngenIA365ERP.Domain/Entities/Core/GoLive/{GoLiveChecklist,GoLiveChecklistItem}.cs con su configuración, la interfaz `IVerificacionDeSalida` en src/Core/IngenIA365ERP.Application/Common/GoLive/ y los comandos `CreateGoLiveChecklistCommand`, `CheckGoLiveItemCommand`, `OverrideGoLiveItemCommand`, `DecideGoLiveCommand` (R-28; FR-227; data-model §16.2)
- [X] T0034 Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Core/GoLiveEndpoints.cs (`/api/core/go-live-checklists`, `…/items/{itemId}/check|override`, `…/decision`) con `Platform.GoLive.View/Manage/Override` (api.md §14)

**Esqueleto de los dos módulos**

- [X] T0035 [P] Agregar `Recaudo = "REC"` a src/Core/IngenIA365ERP.Application/Accounting/Posting/ModuloContable.cs y `Receipts = 128` a `AccountingModules` en src/Core/IngenIA365ERP.Domain/Enums/Accounting/AccountingEnums.cs (R-02; CH11)
- [X] T0036 [P] Crear src/Core/IngenIA365ERP.Domain/Entities/Receipts/Configuration/ReceiptsSetup.cs y src/Core/IngenIA365ERP.Domain/Entities/Treasury/Activation/TreasurySetup.cs (filas únicas, data-model §1.1 y §9.1), los enums `ReceiptsModuleStatus` y `TreasuryModuleStatus` (§20) y `RCD_PayerControls` (`Entities/Receipts/Operations/PayerControl.cs`, §3.1), con sus configuraciones en Persistence/Configurations/{Receipts,Treasury}
- [X] T0037 [P] Crear los alias de precisión src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts/PrecisionDeRecaudo.cs y …/Treasury/PrecisionDeTesoreria.cs (Monto 18,2; Tarifa 9,6; Cantidad int; Facial 18,2) y llenar `LasCantidadesYMontosDeCajaTienenSuPrecision` (R-33; data-model §0.4)
- [X] T0038 Implementar src/Infrastructure/IngenIA365ERP.Persistence/Receipts/{CerrojoDeRecaudo,SqlDelCerrojoDeRecaudo}.cs y src/Infrastructure/IngenIA365ERP.Persistence/Treasury/{CerrojoDeTesoreria,SqlDelCerrojoDeTesoreria}.cs con los niveles de data-model §0.5 (la fila de `RCD_PayerControls` se crea si falta), incluida la entrada que usan los `TomarCerrojosAsync` de `IResultadoDeOrdenesDeRecaudo` e `IPartidasDesdeConciliacion` para la dirección Tesorería → Recaudo; llenar `LosCerrojosDeRecaudoYTesoreriaVanEnOrden` («sólo estos dos tipos toman cerrojos» y «ningún comando de `Treasury` escribe por esos dos puertos sin llamar antes a `TomarCerrojosAsync`») (R-12; FR-007, FR-170; CH16)
- [X] T0039 Implementar src/Core/IngenIA365ERP.Application/Receipts/Common/NumeradorDeRecaudo.cs y src/Core/IngenIA365ERP.Application/Treasury/Common/NumeradorDeTesoreria.cs con la mecánica del `Numerador` de la 012, llamados al final de cada confirmación (R-13)
- [X] T0040 Crear las interfaces de los puertos entre los módulos (contracts/interfaces.md §2) en src/Core/IngenIA365ERP.Application/Common/Integration/Treasury/{ICuentasBancariasPropias,IMovimientosBancariosParaRecaudo,IRemesasDeTesoreria,IOrdenesDePagoDeTesoreria}.cs y …/Common/Integration/Receipts/{IRecaudoParaConciliacion,IPartidasDesdeConciliacion,IResultadoDeOrdenesDeRecaudo}.cs con sus DTO; llenar la regla de `RecaudoYTesoreriaSeHablanPorSusPuertos` («ningún tipo de `Receipts` nombra un `DbSet` de `Treasury` ni al revés; lo que cruza va por estos puertos») (R-05; FR-003)
- [X] T0041 Crear las interfaces contables (contracts/interfaces.md §3) en src/Core/IngenIA365ERP.Application/Common/Integration/Accounting/{IContabilidadParaRecaudo,IContabilidadParaTesoreria,IDimensionesDeRecaudo,IDimensionesDeTesoreria,IEstadoOperativoParaElCierre}.cs con sus DTO, sin implementación todavía; llenar la regla de `RecaudoNoConoceContabilidad` y `TesoreriaNoConoceContabilidad` (FR-004)
- [X] T0042 Registrar las claves de parámetros de data-model §23 en `ReglasDeParametrosPorModulo` (módulos `RCD` y `TES`; las de plataforma y `ACC` donde dice) con su fuente, defecto y «pendiente de validar», sembradas con `INSERT … WHERE NOT EXISTS` por el sembrador de parámetros existente (src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Parametric/); las «sin valor» no se siembran; los plazos regulatorios (presentación de cheques, giro de libranza, días sin resultado, extractos sin cargar) se siembran como avisos que no bloquean salvo la bandera expresa de la cooperativa, y lo que la norma impone (listas vinculantes, fondo de liquidez) no tiene parámetro para dejar de bloquear (FR-009, FR-010, FR-202; D22, D25, D41)
- [X] T0043 Agregar los módulos de auditoría `Receipts` y `Treasury` a la cadena de sellos (src/Core/IngenIA365ERP.Application/Common/Audit/AuditoriaEncadenada.cs, `COR_AuditOutbox`) con `Conservacion.Anios` (diez por defecto, nunca menos de cinco) y la lista cerrada de lecturas sensibles de R-32 (`Receipts.Person.PayablesRead`, `Treasury.BankBalances.Read`, `Core.PersonBankAccount.Read/Changed`, `Receipts.Sarlaft.ReportRead`, exportaciones) (FR-014, FR-178; SC-019)
- [X] T0044 Crear src/Infrastructure/IngenIA365ERP.Identity/Seed/ReceiptsAndTreasuryPermissionCatalogSeeder.cs con **todos** los permisos de DT §2.6 (los de R1 a T2 incluidos, cuyas rutas responden 404 hasta su entrega), los que llevan monto en `SEC_PermissionAmountLimits` sin valor sembrado (P4), y sembrarlo **desde la API al arrancar** (no el DbMigrator), como `LendingPermissionCatalogSeeder` (FR-011; api.md §1.5)
- [X] T0045 Sembrar en src/Infrastructure/IngenIA365ERP.Identity/Seed/ReceiptsAndTreasuryPermissionCatalogSeeder.cs las siete plantillas de rol `recaudo.cajero`, `recaudo.supervisor`, `recaudo.analista`, `tesoreria.tesorero`, `tesoreria.aprobador`, `tesoreria.conciliador`, `cumplimiento.oficial` con sus permisos de Recaudo y Tesorería y los de Cartera y Captaciones que los contratos verifican sobre quien actúa (`Lending.Payments.BackdateBeforeClose` con monto, `Lending.Accounts.Deposit`, `Lending.Accounts.Withdraw` con monto); `Treasury.Statements.Load` en analista y conciliador (FR-176; R-06)
- [X] T0046 Crear src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/ReceiptsGroup.cs y src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/TreasuryGroup.cs (`ReceiptsGroup.Grupo(app, …)`, `TreasuryGroup.Grupo(app, …)`: sesión, cooperativa resuelta, limitador de la cooperativa, guarda de módulo activo; sólo `PublicId` en rutas y cuerpos y número visible, FR-017) sin tocar las rutas heredadas de `/api/treasury/{checks,concepts,invoices,cash-flow}` (R-04; api.md §1)
- [X] T0047 Reagrupar el menú en src/Presentation/IngenIA365ERP.Shared/Layout/NavMenu.razor: el grupo heredado pasa a «Tesorería · heredada» con la leyenda «cuentas por pagar y cheques de proveedores hasta T2»; los grupos «Recaudo» y «Tesorería» nacen con su primera página (`TodoEnlaceDelMenuTieneSuPagina`) (R-04; FR-162)
- [X] T0048 Registrar en DI (src/Core/IngenIA365ERP.Application/DependencyInjection.cs y src/Infrastructure/IngenIA365ERP.Persistence/DependencyInjection.cs) cerrojos, numeradores, puertos de plataforma y `ICuentasBancariasDePersona`; los `DbSet` nuevos en el contexto de la cooperativa

**Migración**

- [X] T0049 Generar el par `PlataformaParaRecaudoYTesoreria` con `tools/scripts/add-migration.ps1 -Name PlataformaParaRecaudoYTesoreria` en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/: `COR_PersonBankAccounts` (con la copia idempotente de `Person.SupplierBank*` como `Imported` sin certificación, Q4), `COR_GoLiveChecklists`, `COR_GoLiveChecklistItems`, semillas de formatos genéricos, tipos de alerta y de adjunto; revisar el SQL a mano y correr `tools/scripts/check-migration-parity.ps1` (data-model §25 #1; R-34) **ruta crítica**
- [X] T0050 Generar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ la primera versión de los pares `RecaudoNucleo` y `TesoreriaNucleo` con `RCD_Setup`, `RCD_PayerControls` y `TES_Setup` (filas únicas por `INSERT … WHERE NOT EXISTS`); se **regeneran** en cada fase de R0/T0 que suma tablas **hasta el despliegue en QA del 10/11** y se congelan en T0271; después, todo cambio va en un par nuevo (data-model §25 #3 y #4; R-34)
- [X] T0051 Checkpoint de la plataforma: `dotnet build IngenIA365ERP.slnx` con 0 errores; `dotnet test tests/IngenIA365ERP.{Domain,Application,Architecture}.Tests` verde contra la línea base de T0006; `dotnet ef migrations has-pending-model-changes` limpio en los dos motores

**Notas de la construcción de la fase 2 (2026-10-24)**. Las desviaciones están aprobadas por el dueño o fijadas en decisiones-transversales §2.7:

- **T0022**: `Tesoreria` entró a `ModulosMigrados`, y sus cuatro pantallas heredadas quedan fuera por nombre. `Recaudo` entra con su
  primera pantalla, porque la carpeta `Pages/Recaudo` todavía no existe y la prueba exige que exista.
- **T0023**: el origen «Reference» de §2 se llama `MovementReference` (105), porque `Reference` ya era sinónimo de `BatchReference` en las
  definiciones JSON. Los orígenes de lectura son 100–111 (extracto) y 120–125 (respuesta a pagos). Un origen de lectura fuera de su
  ámbito es error al guardar el formato.
- **T0024**: los cuatro genéricos viven en `FormatosGenericosDeRecaudoYTesoreria` y los siembra el `IDataSeeder` paramétrico
  `BankFileFormatsSeeder`, idempotente por código, no un `INSERT … WHERE NOT EXISTS`. El código del formato admite 20 caracteres, así que dos se
  acortan: `RESPUESTA-PAGOS-GENERICO` pasa a `RESP-PAGOS-GENERICO` y `RESPUESTA-PAGADURIA-GENERICA` a `RESP-PAGADURIA-GEN`. La respuesta de la
  pagaduría va en CSV y no en XLSX: el motor común sólo lee archivos planos.
- **T0025**: el tratamiento de los contextos `Collection` y `Disbursement` es `EfectoDeListasPorContexto` en `IListasRestrictivas.cs`: la
  coincidencia vinculante bloquea siempre. La consulta en sí no conoce el contexto.
- **T0027**: sólo la certificación bancaria (`PersonBankCertificate`) admite subidas en esta fase. Los demás tipos subidos por personas
  se leen con el permiso de su pantalla y no se borran; las subidas llegan con su historia.
- **T0029/T0043**: las cuentas bancarias de la persona se auditan en el módulo encadenado `Treasury`. `Receipts` y `GoLive` son módulos
  encadenados nuevos, con diez años de conservación. `Conservacion.Anios` se registra con piso 5, pero el TTL del flujo sigue fijo en diez años:
  hacer que lo lea el parámetro queda para **R1** (decisión del dueño del 2026-10-24: en R0/T0, diez años fijos).
- **T0036/T0038**: `OrdenDeCerrojos` (de ámbito) lleva el orden global entre los dos cerrojos y rechaza una fila `RCD_` pedida después de una
  `TES_` (CH16). El nivel 8 de Recaudo (la cuenta propia) lo toma el cerrojo de Tesorería por su puerto.
- **T0039**: la numeración va en `COR_Sequences`, con la clave en `Name`, el módulo en `DocumentType` y el prefijo en `ShortName`. Lleva el índice
  único filtrado `UK_COR_Sequences_Name_RcdTes` «(nuevo)», aprobado por el dueño.
- **T0042**: las claves `CuentasDestino.*` y `Contabilidad.CierreExigeConciliaciones` (T1) van en el módulo de parámetros `TES`. `Gmf.*` va sólo
  en `TES` en R0/T0. `RCD` y `TES` tienen sus líneas de entregas (`EntregaDeRecaudo`, `EntregaDeTesoreria`) y su ruta propia: no aparecen en la de
  Inventario. La regla de la hora de corte vive en `ReglasDeParametrosDeRecaudo`.
- **T0044**: se suman `PermisosLimitables` de `Receipts` y `Treasury`. `Core.PersonBankAccounts.View`, `Treasury.BankBalances.View` y
  `Receipts.Session.ViewExpected` quedan fuera del `*.View` de Operator, ReadOnly y Auditor.
- **T0045**: las siete plantillas viven en `PerfilesSugeridos.cs` (módulos `Receipts` y `Treasury`), como las de Cartera, y no en el sembrador
  de permisos.
- **T0046**: no hay un limitador por cooperativa con nombre; los grupos usan el global de la API. La guarda de módulo activo es
  `ModuloActivoBehavior` (MediatR) sobre los comandos marcados `IEscrituraDeRecaudo` o `IEscrituraDeTesoreria`, no un filtro de ruta.
- **T0049**: la copia de `SupplierBank*` sólo toma la cuenta cuyo código de banco casa con `COR_Banks.TransferCode` o `LegacyCode`. Las semillas
  de alertas, formatos y tipos de adjunto van por los sembradores paramétricos y por código, no en la migración.
- **T0050**: para separar las tres migraciones se generaron en orden, ignorando temporalmente en el modelo las entidades del par siguiente.
- **T0051**: no se compiló la `.slnx` entera, por el disco C. Se compilaron uno a uno API, Web, Shared y los cinco proyectos de pruebas, con 0
  errores. Resultado de las pruebas: Domain 2.652, Application 6.376, Architecture 800 y Shared 499, todas en verde. La paridad está OK y
  `has-pending-model-changes` da «No changes» en los dos motores.

---

# Entrega R0 + T0 · Bloque 2 — Caja y giros (28/10–04/11)

Hito del bloque (04/11): **un recibo y un giro de punta a punta en DEV**, con Recaudo y Tesorería activados por la
activación mínima (T0183b) y la e2e de humo de la caja en verde (T0183c).

---

## Phase 3: User Story 12 - Seguridad, alcance, segregación y auditoría (Priority: P1) — entrega R0 y T0

**Goal**: el alcance por oficina, punto, caja y pagaduría (Recaudo) y por cuenta bancaria propia (Tesorería) que falla
cerrado, la segregación por persona, las aprobaciones de la 012 (presente en caja, diferida en Tesorería) y los emisores
de auditoría de los dos módulos, antes de la primera pantalla que mueva dinero (FR-011 a FR-014, FR-175, FR-176).

**Independent Test**: con un cajero de la oficina A, pedir un recibo de la oficina B: responde `Generic.NotFound` como
ante lo inexistente; un usuario sin `Receipts.Receipt.Register` recibe 403; una operación sobre la atribución pide el
aprobador presente (428) y, aprobada, se repite con la misma clave; la verificación de la cadena detecta una alteración.

### Tests for User Story 12

- [X] T0052 [P] [US12] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Security/AlcanceDeRecaudoTests.cs (R-30; FR-011, FR-175): oficina, punto, caja y pagaduría; sin fila y sin `Receipts.Scope.All` falla cerrado con `Generic.NotFound`; los recibos de banco y pagaduría, las partidas y los saldos a favor toman el alcance de la cuenta propia o la pagaduría; cobro entre oficinas permitido salvo `Caja.CobroSoloDeSuOficina` (D36)
- [X] T0053 [P] [US12] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Security/AlcanceDeTesoreriaTests.cs (R-30; FR-106): por cuenta bancaria propia; lo que está fuera responde `Generic.NotFound`
- [X] T0054 [P] [US12] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Security/SegregacionPorPersonaTests.cs (R-31; FR-012): quien registra no aprueba lo suyo, comparando la persona de `SEC_Users.Id` (nunca el entero del token); delegación del supervisor con vigencia (FR-184); el aprobador presente con segundo factor (`AprobadorPresente`) y la aprobación diferida multinivel por monto (`IMotorDeAprobaciones`, `SEC_PermissionAmountLimits` sin valor = sin atribución) (FR-013)
- [X] T0055 [US12] Ampliar tests/IngenIA365ERP.Architecture.Tests/Principles/LaSegregacionNoUsaUserIdDelToken.cs con `Application/Receipts` y las carpetas nuevas de `Application/Treasury`
- [X] T0056 [P] [US12] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/SeguridadDeRecaudoYTesoreriaTests.cs (US12 «Independent Test»; SC-018, SC-019): alcance que falla cerrado, ruta sin permiso, aprobador presente con la misma clave, auditoría verificable con `POST /api/audit/integrity/verify` tras alterar un evento; se corre en la fase 22

### Implementation for User Story 12

- [X] T0057 [P] [US12] Crear las entidades de alcance `RCD_UserCollectionPointScopes`, `RCD_UserCashBoxScopes`, `RCD_UserPaymasterScopes` (src/Core/IngenIA365ERP.Domain/Entities/Receipts/Configuration/) y `TES_UserBankAccountScopes` (src/Core/IngenIA365ERP.Domain/Entities/Treasury/BankAccounts/) con sus configuraciones (data-model §1.11, §9.3)
- [X] T0058 [US12] Crear `IAlcanceDeRecaudo` e `IAlcanceDeTesoreria` en src/Core/IngenIA365ERP.Application/Common/Scope/ y sus implementaciones src/Presentation/IngenIA365ERP.API/Services/AlcanceDeRecaudoDeLaPeticion.cs y AlcanceDeTesoreriaDeLaPeticion.cs sobre `UserBranchScope` (molde `IAlcanceDeCartera`), que fallan cerrado (R-30)
- [X] T0059 [US12] Implementar `SetReceiptsUserScopeCommand` y `SetTreasuryUserScopeCommand` en src/Core/IngenIA365ERP.Application/{Receipts,Treasury}/Security/ y las rutas `GET/PUT /api/receipts/users/{userId}/scope` y `/api/treasury/users/{userId}/scope` con `Receipts.Scope.Manage` / `Treasury.Scope.Manage` en src/Presentation/IngenIA365ERP.API/Endpoints/{Receipts,Treasury}/ScopeEndpoints.cs
- [X] T0060 [US12] Crear src/Core/IngenIA365ERP.Application/Receipts/Common/SegregacionDeRecaudo.cs y src/Core/IngenIA365ERP.Application/Treasury/Common/SegregacionDeTesoreria.cs (quien registra no aprueba; quien prepara un lote no lo aprueba ni descarga; quien prepara o aprueba pagos de una cuenta no la concilia; `Lotes.TerceraPersonaParaResultado` sellado) (R-31; FR-012, FR-217)
- [X] T0061 [US12] Crear src/Core/IngenIA365ERP.Application/Receipts/Common/AprobacionPresenteEnCaja.cs (`428` y `Approvals.PresenceRequired` **(nuevo)** con `approvalRequestPublicId` y monto, repetición con el encabezado `X-Approval-Request` **(nuevo)** y la misma `Idempotency-Key`; las fallas del desafío responden `422` con los `Approvals.Presence.*` existentes de `ErroresDeAprobaciones`, nunca `401`; DT §2.7) y src/Core/IngenIA365ERP.Application/Treasury/Common/AprobacionDiferidaEnTesoreria.cs (niveles sucesivos hasta cubrir el monto; aprobar no mueve dinero) sobre el motor de la 012 (R-31; FR-013; api.md §1.4)
- [X] T0062 [US12] Crear los emisores de auditoría src/Core/IngenIA365ERP.Application/Receipts/Common/AuditoriaDeRecaudo.cs y src/Core/IngenIA365ERP.Application/Treasury/Common/AuditoriaDeTesoreria.cs sobre `COR_AuditOutbox` en la transacción, **por el `PublicId` de `ICurrentTenantService`** (no por `ICurrentUserService.TenantId`, que es el Id interno: el defecto que tuvieron `AccountingAuditEmitter` y `PayrollAuditEmitter`), con antes y después y la lista cerrada de lecturas sensibles (R-32; FR-014)
- [X] T0063 [US12] Crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Configuracion/Alcance.razor (`/recaudo/configuracion/alcance`) con su cliente `Shared/Services/Recaudo/AlcanceDeRecaudoClient.cs`, `PermissionGate` e `IndicadorDeCarga` por zona; primer enlace del grupo «Recaudo» del menú
- [ ] T0064 [US12] P4 · Entregar quiénes son cajeros, supervisores y suplentes, tesorero, aprobadores y conciliador; los niveles de aprobación por monto de lotes, traslados y retiros; si bastan dos personas en Tesorería; y el período de espera de las cuentas destino cambiadas; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 13/11; **bloquea la salida**: roles, alcances y segundo factor son condición del go/no-go; rige mientras tanto: roles sembrados sin asignar, límites de monto sin valor = sin atribución, espera sin valor)


**Notas de la construcción de la fase 3 (2026-10-24)**. Desviaciones y decisiones; las que nacen aquí quedan en decisiones-transversales §2.7:

- **T0057**: `TES_UserBankAccountScopes` guarda `BankAccountId` sin clave foránea: `TES_BankAccounts` nace con US10 (fase 7), que agrega la FK al
  regenerar `TesoreriaNucleo`. Las FK de los alcances de Recaudo a puntos y cajas entran con la fase 4 (mismo par regenerado).
- **T0058**: el alcance de Recaudo suma a las cajas de `RCD_UserCashBoxScopes` las cajas de las que el usuario es **cajero vigente**
  (`RCD_CashBoxCashiers`): el cajero opera su caja sin otra fila. Un punto en alcance trae sus cajas, y una oficina asignada en Seguridad trae sus puntos
  para las consultas de configuración. Por decisión del dueño del 2026-10-23 se creó `Treasury.Scope.All` (el administrador lo trae, como
  `Receipts.Scope.All`): con él, el maestro global y el segundo plano, el alcance de Tesorería es total.
- **T0059**: (cerrada en la fase 7; ver sus notas) la mitad de Tesorería (`SetTreasuryUserScopeCommand`, `/api/treasury/users/{userId}/scope`) **quedó pendiente** hasta la fase 7: necesita
  `TES_BankAccounts` para resolver la cuenta por su `PublicId`, y esa tabla nace en la fase 7 (T0102). Se hace con la cuenta propia. Las rutas
  `GET/PUT /api/receipts/users/{userId}/scope` sí están, en `API/Endpoints/Receipts/ScopeEndpoints.cs`.
- **T0060**: la comparación por persona vive en un servicio común, `Common/Security/PersonasDeLosUsuarios` (nuevo), que usan
  `SegregacionDeRecaudo` y `SegregacionDeTesoreria`; así Tesorería no depende de una clase de Recaudo. El resultado con tercera persona es
  `Treasury.Result.ThirdPersonRequired` (nuevo).
- **T0061**: los sujetos de aprobación nuevos son `CashCountDifference`, `CashMeansReclassification`, `ReceiptBackdate`,
  `CashSessionAdjustment`, `ReceiptVoiding`, `PaymentBatchApproval` y `LiquidityFundUse` (nuevo), con regla fija de un nivel cuando la cooperativa
  no configura política; las fuentes son `ReceiptsOperation` y `TreasuryDocument` (nuevo). La solicitud que provoca el `428` se guarda en un
  contexto aparte (`ITenantDbContextFactory.AbrirLaDelAmbito`), porque la transacción de la operación se revierte con el 428. El desafío y la decisión
  del aprobador presente tienen ruta propia en Recaudo (`POST /api/receipts/approvals/{id}/presence-challenge` y `/decide`, permiso
  `Receipts.Session.Open`), porque las de la 012 piden `Inventory.Approvals.View`, que un cajero no tiene. `Approvals.PresenceRequired` responde 428
  en `ErrorEnvelopeFilter`. Los «niveles sucesivos hasta cubrir el monto» de Tesorería son los de la política de la cooperativa (umbrales por nivel);
  sin política rige un nivel cuyo aprobador debe tener una atribución que alcance.
- **T0062**: los emisores agregan el evento al outbox **sin guardar** (`AgregarAsync`) para que nazca en el `SaveChanges` único de la operación; las
  lecturas sensibles guardan su fila (como `LendingAuditEmitter`). `Conservacion.Anios`: en R0/T0 la auditoría sigue con **diez años fijos**; hacer
  que el TTL lea el parámetro queda para R1 (decisión del dueño del 2026-10-24).
- **T0056**: la e2e recorre el alcance que falla cerrado, el 403 y la verificación de la cadena. El aprobador presente con la misma clave lo recorre
  la e2e de la caja (`CajaAbrirArquearYCerrarTests`, la diferencia de arqueo), y la alteración de un evento ya la cubre `IntegridadDeAuditoriaTests`
  para todo módulo encadenado.
- **T0063**: las pantallas de configuración eligen usuarios por una consulta propia, `GET /api/receipts/users` (`ListReceiptsUsersQuery`, sólo id y
  nombre, con `Receipts.Setup.View`), por decisión del dueño del 2026-10-23, en vez de `/api/admin/users`.
- **Pruebas**: T0052–T0054 corrieron en verde (filtradas) el 2026-10-24, antes del cambio de estrategia del dueño; desde entonces las pruebas se
  escriben y se compilan sin correr, y se corren una sola vez al terminar el plan. La e2e T0056 está escrita, sin correr (fase 22).

---

## Phase 4: User Story 1 - Configurar puntos de recaudo, cajas, cajeros y medios de pago (Priority: P1) — entrega R0 (parcial)

**Goal**: configurar sin programar los puntos de cada oficina, sus cajas con base, modo y topes, los cajeros, la bóveda o
la caja principal como bóveda, los medios del catálogo común ofrecidos por punto (efectivo y transferencia en R0; cheque
por parámetro), los tipos de documento, los conceptos varios, los talonarios de contingencia, las suplencias y la
modalidad por canal (FR-021 a FR-028, FR-179 a FR-181, FR-198).

**Independent Test**: crear un punto con dos cajas, ofrecer efectivo y transferencia, asignar un cajero y comprobar que la
caja queda lista para abrir; ofrecer una clase de crédito se rechaza; un código duplicado se avisa antes de guardar; un
cambio de base rige desde la próxima sesión (US1-3).

### Tests for User Story 1

- [X] T0065 [P] [US1] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Setup/PuntosYCajasTests.cs (FR-021, FR-022, FR-028, FR-180, FR-181; data-model §1.2–§1.4): código por `CodigoDeCatalogo` y `Catalogo.CodigoDuplicado`; caja con base, modo (`DailyBase` por defecto, `FixedFund`), tope de aviso, tope de bloqueo (nulo = no bloquea) y caja principal; el cambio rige desde la próxima sesión; el cajero debe tener persona; vigencias sin solaparse
- [X] T0066 [P] [US1] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Setup/MediosPorPuntoTests.cs (FR-023, FR-024; data-model §1.5): `Receipts.PaymentMeans.CreditClassNotAllowed`, `.CardSetupIncomplete`, `.CheckDisabled` con `Caja.AdmiteCheque` falso, `.NotInDelivery` para clases distintas de efectivo, transferencia, consignación y cheque en R0; medio inactivo no se ofrece; medio con pagos no se borra
- [X] T0067 [P] [US1] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Setup/CatalogosDeRecaudoTests.cs (FR-026, FR-027, FR-179, FR-184, FR-186, FR-198): un tipo de documento por clase con su consecutivo y prefijo por punto opcional; conceptos del sistema no se inactivan; concepto con `RequiresInvoice`; talonario con rango sin solaparse; suplencia con vigencia; regla de modalidad por canal con prioridad y vigencia
- [X] T0068 [P] [US1] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Setup/BovedasTests.cs (FR-035, FR-180, FR-182): una bóveda por oficina en modo `Vault` o `MainCashBox` (con su caja principal), custodios con vigencia
- [X] T0069 [P] [US1] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ConfiguracionDeRecaudoTests.cs (US1 «Independent Test», US1-1 a US1-4); se corre en la fase 22

### Implementation for User Story 1

- [X] T0070 [P] [US1] Crear las entidades de configuración en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Configuration/: `CollectionPoint`, `CashBox`, `CashBoxCashier`, `PaymentMeansCollectionPoint`, `ReceiptDocumentType`, `MiscConcept`, `ContingencyBook`, `SupervisorDelegation`, `ChannelModalityRule`, y en Entities/Receipts/Vaults/ `Vault`, `VaultCustodian`; los enums `ReceiptDocumentKind`, `ReceiptChannel`, `CashBoxBaseMode`, `VaultMode` en src/Core/IngenIA365ERP.Domain/Enums/Receipts/ con los números de data-model §20 (data-model §1, §2.6)
- [X] T0071 [US1] Configurar las tablas `RCD_CollectionPoints`, `RCD_CashBoxes`, `RCD_CashBoxCashiers`, `RCD_PaymentMeansCollectionPoints`, `RCD_DocumentTypes`, `RCD_MiscConcepts`, `RCD_ContingencyBooks`, `RCD_SupervisorDelegations`, `RCD_ChannelModalityRules`, `RCD_Vaults`, `RCD_VaultCustodians` en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts/ (UK filtrados por `IsDeleted` en catálogos, FK `Restrict` sólo a plataforma, precisión por alias)
- [X] T0072 [US1] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Setup/ los comandos de crear, actualizar e inactivar puntos y cajas, asignar y retirar cajeros, ofrecer y retirar medios por punto, tipos de documento, conceptos varios, talonarios (registrar, anular), suplencias (registrar, terminar) y reglas de modalidad, con validador, `Idempotency-Key` y auditoría, y sus consultas con alcance (FR-021 a FR-028, FR-184, FR-186, FR-198)
- [X] T0073 [US1] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Vaults/ `CreateVaultCommand`, `UpdateVaultCommand`, `SetVaultCustodiansCommand` (bóveda o caja principal como bóveda) (FR-035, FR-180)
- [X] T0074 [US1] Registrar los catálogos nuevos (puntos, cajas, conceptos, tipos de documento) en `GET /api/catalogos/{catalogo}/codigo/{codigo}` para que `CampoCodigo` avise el duplicado antes de guardar (US1-4; Application/Common/Catalogos)
- [X] T0075 [US1] Sembrar en el par `RecaudoNucleo` (src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/) con `INSERT … WHERE NOT EXISTS`: un tipo de documento por clase de `ReceiptDocumentKind` de R0, los conceptos del sistema («giro devuelto», «sobrantes», «reintegros», «rendimientos», `IsSystem`), y las reglas de modalidad por canal de D38 (créditos `CycleInstallment`; cuentas `Installments` si hay exigibles, si no `Free`) (FR-026, FR-027, FR-198)
- [X] T0076 [US1] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/SetupEndpoints.cs con las rutas de api.md §2 (`/api/receipts/collection-points`, `/cash-boxes`, `/cash-boxes/{id}/cashiers`, `/collection-points/{id}/payment-means`, `/document-types`, `/misc-concepts`, `/contingency-books`, `/vaults`, `/supervisor-delegations`, `/channel-modality-rules`) con `Receipts.Setup.View/Manage`
- [X] T0077 [US1] Crear src/Presentation/IngenIA365ERP.Shared/Services/Recaudo/ConfiguracionDeRecaudoClient.cs y las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Configuracion/{Puntos,Cajas,Medios,TiposDeDocumento,Conceptos,Talonarios,Suplencias}.razor (`/recaudo/configuracion/*`) con `CampoCodigo`, `PermissionGate` e `IndicadorDeCarga` por zona; enlaces en el menú «Recaudo»
- [X] T0078 [US1] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de configuración y la bóveda (data-model §1, §2.6) y correr `tools/scripts/check-migration-parity.ps1`


**Notas de la construcción de la fase 4 (2026-10-24)**. Desviaciones y decisiones; las que nacen aquí quedan en decisiones-transversales §2.7:

- **T0070**: las enumeraciones que data-model §20 no numeraba nacen aquí (`ReceiptsCatalogStatus`, `CashBoxBaseMode`, `VaultMode`,
  `MiscConceptDirection`, `ContingencyBookState`). La modalidad de una regla por canal se guarda como el número de `ModalidadDePago` o
  `ModalidadDeConsignacion` del contrato (el dominio no conoce los enums de Application), y la regla de cuentas de D38 son dos filas por prioridad con
  `OnlyWhenInstallmentsDue`.
- **T0072**: un tipo de documento **activo por clase** (`Receipts.DocumentType.KindTaken`): es el que numera esa clase. A lo sumo una caja principal por
  oficina (`Receipts.CashBox.MainAlreadyExists`). El cajero tiene que tener persona y sus vigencias en una caja no se cruzan. Un medio inactivo no se
  ofrece (`Receipts.PaymentMeans.Inactive`) y retirar uno cierra su vigencia hoy, sin borrar la fila. La tarjeta completa sigue en
  `NotInDelivery` hasta R1. Los errores nuevos quedan en DT §2.7. La auditoría de estos comandos es la de `AuditBehavior` y el interceptor (módulo
  encadenado `Receipts`): no hace falta un evento explícito.
- **T0074**: los catálogos se llaman `puntos-de-recaudo`, `cajas-de-recaudo`, `conceptos-varios` y `tipos-de-recibo` (`cajas` ya era la del punto de
  venta). La caja es única por punto: el buscador avisa la primera con el código y el comando decide por punto.
- **T0075**: tipos `RC`, `RB`, `RP`, `RT`, `AN`, `MC`, `AS`; conceptos del sistema `GIRODEV`, `SOBRANTE`, `REINTEGRO`, `RENDIM`; reglas de modalidad
  para los canales banco, pagaduría y contingencia, vigentes desde el 2026-01-01.
- **T0077**: no hay pantalla de las reglas de modalidad por canal (no estaba en la lista): sólo sus métodos en el cliente. Las bóvedas tienen su
  pantalla, `/recaudo/configuracion/bovedas`.
- **T0078**: `RecaudoNucleo` y `TesoreriaNucleo` se regeneraron en par (quitar los dos, generar Recaudo sin las entidades nuevas de Tesorería y
  después Tesorería). Paridad OK; `has-pending-model-changes` da «No changes» en SQL Server y en PostgreSQL.
- **Pruebas**: T0065–T0068 corrieron en verde (filtradas) el 2026-10-24, antes del cambio de estrategia; la e2e T0069 está escrita, sin correr.

---

## Phase 5: User Story 2 - Abrir, arquear y cerrar la caja por medio de pago (Priority: P1) — entrega R0

**Goal**: la sesión de caja con base contada por denominaciones y valores sellados, los movimientos de caja y de bóveda con
doble custodia, la reclasificación entre medios, el arqueo ciego por medio, las diferencias aprobadas con su tratamiento
(cuenta por cobrar al cajero, gasto, sobrante), el cierre de sesión y del día, la sesión olvidada, el ajuste de una sesión
cerrada, los arqueos sorpresivos y el cuadre diario (FR-029 a FR-036, FR-181 a FR-185).

**Independent Test**: abrir con 500.000 de base, recaudar 1.200.000 en efectivo y 800.000 por transferencia, retirar
1.000.000 a bóveda y cerrar contando 699.000: queda «cerrada con diferencia por aprobar» y otra persona la aprueba con
motivo; el cierre del día sólo procede con todas las sesiones cerradas; el informe por medio cuadra.

### Tests for User Story 2

- [X] T0079 [P] [US2] Escribir los casos dorados del arqueo (≈12) en tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/arqueo-*.json y su lector tests/IngenIA365ERP.Domain.Tests/Receipts/ArqueoDeCajaTests.cs (R-14; FR-031, FR-032): esperado = base + recibos − anulaciones en la sesión ± movimientos ± reclasificaciones; efectivo por denominaciones o total; tarjetas por lote; transferencias por referencias; cheques por relación; tolerancia del medio; arqueo ciego; diferencia cero, faltante, sobrante, dentro y sobre la tolerancia
- [X] T0080 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/SesionDeCajaTests.cs (FR-022, FR-029, US2-1, US2-2): abrir con la base por denominaciones; `Receipts.Session.AlreadyOpen` dice quién la tiene; `.CashierExclusive` con `Caja.UnaSesionPorCajero`; los valores sellados no cambian aunque la caja cambie; sin sesión `Receipts.Session.Required`
- [X] T0081 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/MovimientosDeCajaTests.cs (FR-030, FR-182; US2-7): abastecer desde bóveda y retirar a bóveda con doble custodia (`Caja.DobleCustodia`), traslado entre cajas, reclasificación que no toca el recibo, desde efectivo con soporte cotejado y aprobador presente distinto, al informe de excepciones; sus hechos `MovimientoDeCajaRegistrado` y `MedioReclasificado`
- [X] T0082 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/CierreDeSesionTests.cs (FR-031 a FR-033, FR-183; D6, D7): con arqueo ciego lo esperado no se ve hasta registrar lo contado; dentro de tolerancia → `Closed`; sobre → `ClosedWithDifferencePending` y `ApproveCountDifference` por otra persona (`Receipts.Count.SelfApproval`); faltante a cargo del cajero (`RCD_CashierReceivables` con aviso) o gasto; sobrante a `RCD_CashSurpluses`; hecho `DiferenciaDeArqueoAprobada`; un faltante bajo la tolerancia queda con motivo y sin aprobación (Q7)
- [X] T0083 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/CierreDelDiaYSesionOlvidadaTests.cs (FR-033, FR-034, FR-184, FR-185): cierre del día con `Receipts.Day.SessionsOpen` y `.DifferencesPending`; el suplente cierra con delegación vigente; la tarea `recaudo.sesiones-olvidadas` marca `ForgottenOpen` a la hora de corte y alerta `Recaudo.SesionOlvidada`; el supervisor cierra con testigo y la diferencia la aprueba un tercero; la sesión olvidada no recibe operaciones del día siguiente (`.Forgotten`)
- [X] T0084 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/AjustesArqueosSorpresivosYBovedaTests.cs (FR-033, FR-035, FR-182; US2-5): ajuste de sesión cerrada aprobado y enlazado con su hecho; arqueo sorpresivo de sesión y de bóveda con acta; billete falso retenido con acta; saldo por denominación de la bóveda como proyección que el cuadre verifica
- [X] T0085 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/TopesFaltantesYSobrantesTests.cs (FR-028, FR-181, FR-183): aviso de traslado a bóveda (`Recaudo.TopeDeEfectivo`); tope de bloqueo que rechaza recibos en efectivo (`Receipts.Session.CashBlockCapReached`) hasta trasladar; el descuento por Nómina de un faltante exige la autorización escrita del caso adjunta; el sobrante pasa a ingreso sólo tras `Caja.Sobrantes.DiasAntesDeIngreso` y con aprobación (`SobranteLlevadoAIngreso`)
- [X] T0086 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/CuadreDiarioTests.cs (FR-036, FR-185): la tarea `recaudo.cuadre-diario` corre después del lote del día y compara cajas + bóveda + tránsito contra `SaldoContableAsync` más lo emitido y pendiente de lote; diferencia mostrada, explicable y alertada (`Recaudo.CuadreDiarioConDiferencia`)
- [X] T0087 [P] [US2] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/CajaAbrirArquearYCerrarTests.cs (US2 «Independent Test», quickstart recorrido A pasos 1, 7–9; SC-004, SC-005); se corre en la fase 22

### Implementation for User Story 2

- [X] T0088 [P] [US2] Implementar el motor puro src/Core/IngenIA365ERP.Domain/Receipts/Arqueo/ArqueoDeCaja.cs (sin IO, decimal, métodos de arqueo de la 012) hasta que pasen los casos dorados de T0079 (R-14)
- [X] T0089 [P] [US2] Crear las entidades de sesión en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Sessions/: `CollectionPointDay`, `CashSession`, `CashCount`, `CashCountLine`, `CashCountDenomination`, `CashCountTerminalBatch`, `CashCountReference`, `CashMovement`, `CashMovementDenomination`, `CashSessionAdjustment`, `DayClose`, `DayCloseLine`, `DailySquare`, `CashierReceivable`, `CashSurplus`; y en Entities/Receipts/Vaults/ `VaultMovement`, `VaultMovementDenomination`, `VaultDenominationBalance`; enums `CashSessionState`, `CashMovementKindR`, `CountTreatment` (data-model §2, §4.4, §20); los H con `IHechoInmutable`
- [X] T0090 [US2] Configurar sus tablas en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts/ con el único filtrado `(CashBoxId)` en `Open`/`Closing`/`ForgottenOpen` y el de `(CashierUserId)` con `Caja.UnaSesionPorCajero`, la UK `(CollectionPointId, BusinessDate)` del día del punto (data-model §2.1, §2.2)
- [X] T0091 [US2] Crear el emisor de hechos de Recaudo src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.cs (por `EmisorDeMensajes`, en la transacción, sin cuentas, dimensiones por código, `ReceiptsFactDimensionsV1` y `FactLineV1` de contabilidad.md §2) y sus partes `EmisionDeRecaudo.Caja.cs` con `MovimientoDeCajaRegistrado`, `MedioReclasificado`, `DiferenciaDeArqueoAprobada`, `AjusteDeSesionRegistrado`, `SobranteLlevadoAIngreso`; ampliar `ElCatalogoDeMensajesEsUnico` con estos tipos `REC` (CH5; FR-004, FR-140, FR-141)
- [X] T0092 [US2] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Sessions/ `OpenCashSessionCommand` (base por denominaciones, sella `SealedSettingsJson`), `GetCurrentSessionQuery`, `GetSessionExpectedQuery` (oculto con arqueo ciego hasta registrar lo contado), `RegisterCashMovementCommand`, `ReclassifyMeansCommand` (soporte y aprobador presente), con cerrojo de sesión y `Idempotency-Key` (FR-029, FR-030)
- [X] T0093 [US2] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Sessions/ `RegisterClosingCountCommand`, `ApproveCountDifferenceCommand` (otra persona, motivo, tratamiento por `Caja.TratamientoFaltante`), `CloseForgottenSessionCommand` (testigo), `RegisterSessionAdjustmentCommand`, `RegisterSurpriseCountCommand` (acta) y `CloseCollectionPointDayCommand` (día exclusivo; suplencia) con `ArqueoDeCaja` (FR-031 a FR-034, FR-184)
- [X] T0094 [US2] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Vaults/ `RegisterVaultMovementCommand` (abastece, recibe, conteo, billete retenido con acta; doble custodia; actualiza `RCD_VaultDenominationBalances`) y en …/Receipts/Sessions/ `AttachPayrollDeductionAuthorizationCommand` y `MoveSurplusToIncomeCommand` (FR-035, FR-182, FR-183)
- [X] T0095 [US2] Implementar las tareas `recaudo.sesiones-olvidadas` y `recaudo.cuadre-diario` como `ITareaProgramada` por `IEjecutorEnCooperativa` en src/Core/IngenIA365ERP.Application/Receipts/Sessions/Tareas/, con la hora de corte `Recaudo.HoraDeCorte`; **sin dependencias en `ProgramadorDeTareas`** (no las tiene): el cuadre diario de una cooperativa no corre hasta que el lote de ese día terminó (marca de corrida del lote), y el orden con el ciclo de Cartera es la restricción de configuración de FR-185 (`Recaudo.HoraDeCorte` antes de `Lending:Process:StartTimeLocal` en la línea del día de operación): el validador de una versión de `Recaudo.HoraDeCorte` la comprueba y rechaza con `Receipts.Setup.CutoffAfterLendingCycle` **(nuevo, DT §2.7)**, con caso propio en tests/IngenIA365ERP.Application.Tests/Receipts/Sessions/CierreDelDiaYSesionOlvidadaTests.cs; sólo marcan, comparan y alertan, cooperativa por cooperativa como «Proceso de la cooperativa» (FR-019, FR-174, FR-185; TR43)
- [X] T0096 [US2] Crear los documentos `CierreDeSesion`, `CierreDelDia` y `Acta` (arqueo sorpresivo, billete retenido) en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeRecaudoReport.cs con QuestPDF, número y código de verificación (FR-033, FR-156)
- [X] T0097 [US2] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/SessionsEndpoints.cs y VaultsEndpoints.cs con las rutas de api.md §3 (sesiones, esperado, movimientos, reclasificaciones, conteo, diferencias, sesión olvidada, ajustes, arqueos sorpresivos, cierre del día, bóveda, cuadre diario) y sus permisos
- [X] T0098 [US2] Crear src/Presentation/IngenIA365ERP.Shared/Services/Recaudo/SesionesClient.cs y las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/{Sesiones,CierreDelDia,Boveda}.razor (`/recaudo/sesiones`, `/recaudo/cierre-del-dia`, `/recaudo/boveda`) con el conteo por denominaciones, el arqueo ciego, la aprobación presente, `PermissionGate` e `IndicadorDeCarga` por zona; enlaces en el menú
- [X] T0099 [US2] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de sesión, arqueo, bóveda, cierre, cuadre, faltantes y sobrantes (data-model §2, §4.4)
- [ ] T0100 [US2] P3 · Confirmar si cada oficina de COOFLOPAL maneja bóveda o la caja principal hace de bóveda y registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 06/11; no bloquea: es configuración, sin defecto; cada oficina configura `RCD_Vaults.Mode`)


**Notas de la construcción de la fase 5 (2026-10-24)**. Estrategia del dueño: las pruebas se escriben y se compilan, y se corren una sola vez al
terminar el plan («prueba escrita, sin correr»). Desviaciones y decisiones; las que nacen aquí quedan en decisiones-transversales §2.7:

- **T0079, T0088**: los casos dorados son 13 (`arqueo-*.json`) y el motor `ArqueoDeCaja` es puro (esperado por medio, lo contado según el método de la
  012, diferencia, tolerancia, tratamiento sellado del faltante, lo que hay que aprobar y los medios sin motivo). Prueba escrita, sin correr.
- **T0080–T0086**: escritas sobre `CajaDePrueba` (punto, dos cajas, efectivo y transferencia, billetes, tipos `MC` y `AS`, bóveda con custodios, cerrojo
  simulado que crea la fila de numeración y Contabilidad sin iniciar), sin correr. El caso de `Receipts.Setup.CutoffAfterLendingCycle` vive en
  `CierreDelDiaYSesionOlvidadaTests` (la regla ya existía desde la fase 2).
- **T0087**: la e2e está escrita y llama a `RecaudoYTesoreriaE2E.RegistrarYActivarAsync`, que lanza hasta la fase 19 (activación); la aprobación de la
  diferencia por **otra** persona se prueba en Application (la e2e comprueba `SelfApproval` y que el día no cierra con la diferencia pendiente). Los recibos
  del «Independent Test» (1.200.000 en efectivo y 800.000 por transferencia) se suman a este recorrido en la fase 6.
- **T0089**: `CashSurplus.ToIncomeFrom` es **nulo** mientras `Caja.Sobrantes.DiasAntesDeIngreso` no tenga valor (P8, sin semilla): el sobrante no pasa
  solo. `CashCount` lleva `VerificationCode` (el del informe del cierre y del acta). Siete clases de la 012 (POS) se llaman igual que las de la sesión de
  Recaudo (`CashSession`, `CashCount`…): los `DbSet` de Recaudo que chocan llevan el prefijo `Receipts` (`ReceiptsCashSessions`, `ReceiptsCashCounts`,
  `ReceiptsCashCountLines`, `ReceiptsCashCountDenominations`, `ReceiptsCashCountTerminalBatches`, `ReceiptsDayCloses`, `ReceiptsDayCloseLines`); las tablas
  son las `RCD_` de data-model.
- **T0090**: la UK de una sesión viva por caja es un índice único filtrado `[Status] IN (1, 2, 5)`. La de una por cajero **no puede ser un índice**:
  depende de `Caja.UnaSesionPorCajero`, un parámetro con vigencia; queda un índice de búsqueda filtrado y la apertura la comprueba (DT §2.7).
- **T0091**: el catálogo de Recaudo nace en `Contracts/Receipts/CatalogoDeMensajesDeRecaudoV1.cs` con los cinco hechos de la caja y su código corto
  (`MCR`, `MRC`, `DAA`, `ASR`, `SLI`); `ElCatalogoDeMensajesEsUnico` lo admite y ahora cuenta 8 tipos `REC`; `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas`
  recorre la carpeta. El modo de paso de los hechos de la caja: sin contabilidad iniciada `NotPosted`; con ella, `NoPasa` del parámetro o en línea. Como
  `IContabilidadParaRecaudo` no tenía implementación, hay una **provisional** en `Application/Accounting/Receipts/ContabilidadParaRecaudo.cs` (estado del
  período real, validación previa «contabilizable» sin matriz y saldo contable vacío) hasta la fase 16. La base de apertura no emite hecho (no mueve dinero
  entre cuentas); la remesa que vuelve a la bóveda sí (`EfectivoEnTransito` → `Boveda`).
- **T0092**: los movimientos de bóveda numeran con el tipo `MC` y el prefijo `BV` (otra fila de `COR_Sequences`). La reclasificación pide aprobador
  presente sólo **desde efectivo** (FR-030); entre otros medios basta el permiso. El 428 lleva ahora `data.contentSha256` (la huella que el aprobador
  confirma en `…/decide`). La reclasificación del medio de un recibo concreto (`ReceiptId`) se enlaza en la fase 6, cuando exista el recibo.
- **T0093**: el aprobador de una diferencia puede cambiar el tratamiento del faltante (cuenta por cobrar o gasto): la línea es un hecho y conserva el
  sellado; el elegido va en el hecho y en su efecto. El cierre del día lo decide el permiso `Receipts.Day.Close`; si quien cierra es el suplente con una
  suplencia vigente del punto, el cierre la registra (`SupervisorDelegationId`). `ReceiptCount` y `VoidingCount` quedan en cero hasta la fase 6.
- **T0094**: el billete retenido queda como acta **sin tocar el saldo** de la bóveda (no es dinero de la bóveda); el conteo, igual. La remesa al banco es de
  la fase 7 (Tesorería). Pasar un sobrante a ingreso lo aprueba otra persona que el cajero con `Receipts.Count.ApproveDifference` (no hay aprobador
  presente: no ocurre en la caja). Para los soportes de la caja nace el dueño de adjuntos `ReceiptsCashSupport` (`AdjuntosDeModulo.SoporteDeCaja`), de la
  sesión o la bóveda, que no se borra.
- **T0095**: las dos tareas corren cada 15 y 30 minutos y deciden con la hora de corte de la cooperativa (`Recaudo.HoraDeCorte`); la marca del lote del
  día es `COR_SystemSettings` `RCD.LoteDelDia.Terminado` (la deja `recaudo.lote-del-dia`, T0263); sin contabilidad iniciada no hay lote que esperar. El
  cuadre sin saldo contable deja `LedgerBalance` nulo y lo dice, no compara contra cero; el tránsito y lo pendiente de lote quedan en cero hasta las
  fases 6, 7 y 16.
- **T0096**: `DocumentosDeRecaudoReport` pinta el cierre de sesión, el cierre del día y el acta; el puerto `IDocumentosDeRecaudoEnPdf` vive en Application.
  Rutas: `GET /api/receipts/sessions/{id}/close-report`, `…/collection-points/{id}/days/{date}/report`, `…/surprise-counts/{id}/minutes`.
- **T0097**: además de las de api.md §3: `GET /api/receipts/sessions/{id}/counts`, `GET/POST /api/receipts/cashier-receivables[/{id}/payroll-authorization]`,
  `GET/POST /api/receipts/cash-surpluses[/{id}/to-income]` y `POST /api/receipts/daily-squares/{id}/explain` (DT §2.7).
- **T0098**: pantallas `/recaudo/sesiones`, `/recaudo/cierre-del-dia`, `/recaudo/boveda` y los componentes `ConteoDeBilletes` y
  `AprobacionPresenteDeRecaudo` (molde `AprobacionEnCaja` de la 012). Los soportes y actas se citan por el identificador del adjunto (la subida directa al
  bucket ya existe; la pantalla de subida se integra con la fase 6). Sin probar en navegador.
- **T0099**: el par `RecaudoNucleo` y `TesoreriaNucleo` se regeneró con las 18 tablas nuevas; paridad OK; `has-pending-model-changes` da «No changes» en
  SQL Server y en PostgreSQL.

---

## Phase 6: User Story 3 - Recibo de caja multiproducto por los contratos de Cartera y Captaciones (Priority: P1) — entrega R0 (parcial)

**Goal**: lo que una persona puede pagar en una pantalla; la operación de caja «en preparación» con `PublicId` estable; la
elección de modalidad y de la elección del deudor (Ley 2032); los medios combinados imputados por porción; la confirmación
única `ConfirmacionDeRecibo` (listas → validación previa contable → cerrojos → contratos, obligaciones antes que cuentas →
numeración → hechos → un `SaveChanges`; por decisión del dueño del 2026-10-23 el recibo numera después de los cerrojos de Recaudo y antes de los contratos), todo o nada e idempotente; el recibo inmutable y su documento (FR-037 a FR-045,
FR-047, FR-048, FR-050, FR-170, FR-172, FR-177, FR-188 a FR-192).

**Independent Test**: para un asociado con dos créditos y la cuota de aportes del mes, pagar la cuota del ciclo de uno, un
abono extraordinario al otro con la elección «reducir plazo» y la cuota de aportes, con efectivo y transferencia: un recibo
con tres aplicaciones, cada una con el comprobante de su módulo; repetir con la misma clave devuelve el mismo recibo; un
rechazo en la tercera aplicación no deja nada ni consume número.

### Tests for User Story 3

- [X] T0101 [P] [US3] Escribir los casos dorados de la imputación (≈10) en tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/imputacion-*.json y tests/IngenIA365ERP.Domain.Tests/Receipts/ImputacionDeMediosTests.cs (R-11; FR-041, FR-188; D35): inmediatos primero en el orden de la pantalla y el cheque al final; orden configurable `Recibo.OrdenDeImputacion`; imputación fijada a mano; total de medios = aplicado + vueltas; vueltas sólo en efectivo; ninguna porción en cero; tres aplicaciones con dos medios
- [X] T0102 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/LoQuePuedePagarTests.cs (FR-037): obligaciones como deudor con exigible, cuota del ciclo, vencido, total para cancelar, anticipo, saldo a favor, modalidades y bloqueos por `ListarAsync`; cuotas exigibles de aportes, ahorro permanente y contractual y cuentas para consignar por el contrato de cuentas; conceptos varios; la lectura audita `Receipts.Person.PayablesRead`
- [X] T0103 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/OperacionDeCajaTests.cs (R-07; FR-038, FR-039, FR-189, FR-190): la operación nace `Preparing` con `PublicId` estable; cada aplicación se simula con su módulo; el abono extraordinario trae las dos alternativas y sin la elección del deudor responde `Receipts.Receipt.ChoiceRequired`; la modalidad anticipada que no abona a capital exige la aceptación expresa del aviso; el fondeo de un CDAT por su valor exacto; `AbandonCashOperation`
- [X] T0104 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/ConfirmacionDeReciboTests.cs (R-08 a R-10; FR-042, FR-043, FR-170, FR-172): un lote por contrato y obligaciones antes que cuentas; rechazo por ítem → `422 Receipts.Receipt.ItemRejected` con `data.items[]` y nada queda; `DistributionChanged` con `data.simulations[]` y reconfirmación con la misma clave; excedente en caja `SurplusNotAllocated`; misma clave y huella → el original; otra huella → `409 Operation.KeyReused`; la huella excluye simulación, aprobador e instante; clave por porción `REC:{recibo:N}:{aplicación:N}:{clase}`
- [X] T0105 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/MediosYFechasDelReciboTests.cs (FR-041, FR-047, FR-048, FR-192): `MeansTotalMismatch`, `ChangeOnlyInCash`, `VoucherAlreadyUsed`; fecha valor anterior con `Receipts.Receipt.Backdate`, aprobador presente distinto, motivo, soporte y `Caja.FechaValorAnterior.MaxDias` (`BackdatedNotAllowed`, `BackdatedNeedsApproval`), respetando las reglas de fecha del contrato; la firmeza que devuelve el módulo queda en la aplicación
- [X] T0106 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/PagadoresYConceptosTests.cs (FR-016, FR-040, FR-050, FR-177, FR-179): pagador tercero recibido como tercero por los contratos y recibo enmascarado sin saldos ni mora del beneficiario; pagador ocasional dado de alta en un paso mínimo con alerta `Recaudo.PagadorOcasionalSobreUmbral` sobre el umbral de declaración; concepto vario con tercero o cruce; concepto que exige factura sin factura → `Receipts.Receipt.InvoiceRequired`
- [X] T0107 [US3] Llenar tests/IngenIA365ERP.Architecture.Tests/Principles/LosComandosDeRecaudoYTesoreriaLlevanClave.cs con los comandos de `Receipts` que escriben (`IOperacionIdempotente`) y `LosCerrojosDeRecaudoYTesoreriaVanEnOrden` con la regla «ninguna operación de Recaudo llama a `IContratoDeCuentas` antes que a `IContratoDeObligaciones`» (R-12; FR-005, FR-170); ampliar `LasCuentasSeTocanSoloPorElContrato` y `LaCarteraSoloSeTocaPorSusInterfaces` con `Application/Receipts` como llamador
- [X] T0108 [P] [US3] Escribir las e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ReciboMultiproductoTests.cs (US3 «Independent Test», quickstart recorrido A pasos 2–6 y B), ConfirmarReciboEsTodoONadaTests.cs (fallo inducido en la tercera aplicación y en el numerador; SC-006), ReintentoConLaMismaClaveTests.cs (tres repeticiones por operación; SC-007) y DosCajasMismaPersonaTests.cs (dos cajas sobre créditos y cuentas de la misma persona, cero interbloqueos; SC-032), sobre Cartera y Captaciones reales; se corren en la fase 22

### Implementation for User Story 3

- [X] T0109 [P] [US3] Implementar el motor puro src/Core/IngenIA365ERP.Domain/Receipts/Imputacion/ImputacionDeMedios.cs hasta que pasen los casos de T0101
- [X] T0110 [P] [US3] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/ las entidades `CashOperation`, `Receipt`, `ReceiptMeans`, `ReceiptApplication`, `ReceiptApplicationPortion`, `SourceOfFundsDeclaration` (las cuatro de volumen con `AuditableEntityLong`) y los enums `CashOperationState`, `ScreeningOutcomeR`, `ReceiptState`, `ApplicationState`, `ApplicationTarget`, `ClaseDeMedioDeRecaudo` (data-model §3, §20); los H con `IHechoInmutable` y sus `[EscrituraUnica]` y columnas mutables declaradas (FR-002); `RegisteredAt`, `ValueDate` y `ProposedAccountingDate` en hora de Colombia (FR-008)
- [X] T0111 [US3] Configurar `RCD_CashOperations`, `RCD_Receipts` (UK `(DocumentTypeId, Number)`, índices de data-model §3.3), `RCD_ReceiptMeans` (UK filtrado de bono, nunca el número de tarjeta), `RCD_ReceiptApplications`, `RCD_ReceiptApplicationPortions` (UK `IdempotencyKey`) y `RCD_SourceOfFundsDeclarations` en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts/ y ampliar `LosPagosNoGuardanElNumeroDeTarjeta` con ellas
- [X] T0112 [P] [US3] Crear src/Core/IngenIA365ERP.Application/Receipts/Common/ClavesDeRecaudo.cs (gramática de R-09: porción, reverso `:reverso`, resolución `…:destino`, línea de pagaduría) y src/Core/IngenIA365ERP.Application/Receipts/Common/HuellaDeRecibo.cs sobre `HuellaDeOperacion.Calcular` (R-09, R-10; FR-006, FR-172)
- [X] T0113 [US3] Implementar `GetPayablesQuery` en src/Core/IngenIA365ERP.Application/Receipts/CashDesk/ con `IContratoDeObligaciones.ListarAsync` y `IContratoDeCuentas.ConsultarAsync`/`ListarExigiblesAsync` como `Recaudador("REC", ClaseDeRecaudador.Receipts)`, búsqueda por documento, lector o número de obligación, lectura sensible auditada (FR-037; interfaces.md §1)
- [X] T0114 [US3] Implementar `CreateCashOperationCommand`, `UpdateCashOperationItemsCommand` (simula cada aplicación; dos simulaciones para el abono extraordinario), `UpdateCashOperationMeansCommand` (imputación propuesta o fijada) y `AbandonCashOperationCommand` en src/Core/IngenIA365ERP.Application/Receipts/CashDesk/ (R-07; FR-038 a FR-041, FR-188 a FR-190)
- [X] T0115 [US3] Implementar el flujo único src/Core/IngenIA365ERP.Application/Receipts/CashDesk/ConfirmacionDeRecibo.cs y `ConfirmCashOperationCommand` dentro de `TransaccionExplicita`: validación → listas (gancho que completa la fase 15) → validación previa contable por `IContabilidadParaRecaudo.EvaluarAsync` (sin contabilidad iniciada pasa; la implementación real llega en la fase 16) → `CerrojoDeRecaudo` → `NumeradorDeRecaudo` (decisión del dueño del 2026-10-23: antes de los contratos, que guardan el número) → `AplicarAsync` interactivo con todas las porciones de créditos → `ConsignarAsync` con todas las de cuentas → hechos → un `SaveChanges`; el recibo toma el `PublicId` de la operación, y es el único camino del dinero que entra por la caja (R-07, R-08; FR-001, FR-042, FR-170) **ruta crítica**
- [X] T0116 [US3] Emitir el hecho `ReciboEmitido` por porción de medio (contra el puente de cada módulo por `TargetModule`, `TargetNature`, `CollectorClass`, o el rol del concepto) en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Recibos.cs con modo `PorLotes` por defecto, y ampliar `ElCatalogoDeMensajesEsUnico` (contabilidad.md §3; FR-141)
- [X] T0117 [US3] Implementar el pagador tercero y el pagador ocasional: el alta mínima (documento, nombre, autorización de datos) reutiliza src/Presentation/IngenIA365ERP.Shared/Components/Personas/PersonaDialog sobre `PersonasClient` en modo mínimo (la persona se escribe en un solo sitio), con `IAutorizacionDeDatos` y la alerta al oficial sobre el umbral (FR-016, FR-040, FR-177)
- [X] T0118 [US3] Crear el documento `Recibo` en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeRecaudoReport.cs (tirilla de 80 mm y carta, «copia» al reimprimir, código de verificación, aplicaciones con el comprobante y número de cada módulo, imputación, elección del deudor y aceptación del aviso, firmeza, quien efectúa, declaración; con pagador tercero producto enmascarado y valor) (FR-044, FR-045, FR-177, FR-190)
- [X] T0119 [US3] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/CashDeskEndpoints.cs (`/api/receipts/payables`, `/cash-operations`, `…/items`, `…/means`, `…/declaration`, `…/confirm`, `…/abandon`) y ReceiptsEndpoints.cs (`/api/receipts/receipts`, `…/{id}/document?format=thermal|letter&copy=`) con sus permisos (api.md §4)
- [X] T0120 [US3] Crear src/Presentation/IngenIA365ERP.Shared/Services/Recaudo/CajaClient.cs con `ClaveDeOperacion` (la misma `Idempotency-Key` al reconfirmar tras `DistributionChanged` o tras una aprobación) y la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Caja.razor (`/recaudo/caja`): tres pasos (persona → qué paga → medios); el paso «persona» compone el buscador común `<PersonSearchPicker>` (src/Presentation/IngenIA365ERP.Shared/Components/Shared/PersonSearchPicker.razor, el que ya usan las pantallas de `Components/Cartera`; constitución, Estándares: nunca un buscador ad hoc) con la búsqueda por número de obligación y el lector; atajos `F2` buscar, `F8` medios, `F9` confirmar, `Esc` cancelar, búsqueda que acepta el lector, simulación por ítem con su aviso, las dos alternativas del abono extraordinario, rechazos por ítem por código (nunca por texto), `IndicadorDeCarga` por zona (R-35; FR-018; SC-003)
- [X] T0121 [US3] Crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Recibos.razor (`/recaudo/recibos`) con la búsqueda con alcance, el detalle (enmascarado para pagador tercero) y la reimpresión «copia»; enlaces en el menú
- [X] T0122 [US3] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de operación y recibo (data-model §3)
- [ ] T0123 [US3] Probar a mano la caja en la aplicación instalable (MAUI) y en la Web: atajos, lector, impresión en tirilla y carta (no hay pruebas de navegador en el repositorio); anotar el resultado en specs/015-recaudo-tesoreria/quickstart.md (R-37)
- [ ] T0124 [US3] P13 · Entregar la lista de servicios a asociados (funerario, odontología y similares), cuáles se venden a plazos con financiación de Cartera y cuáles quedan como cuenta por cobrar de Comercial, y si alguna cuenta por cobrar comercial sin Cartera debe cobrarse en la caja antes de R1; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 13/11; no bloquea: rige que R0 cobra lo que financia Cartera por el contrato de obligaciones)


**Notas de la construcción de la fase 6 (2026-10-24)**. Las pruebas se escriben y se compilan; se corren una sola vez al terminar el plan («prueba escrita,
sin correr»). Desviaciones y decisiones; las que nacen aquí quedan en decisiones-transversales §2.7:

- **T0101, T0109**: 12 casos dorados (`imputacion-*.json`) y el motor puro `ImputacionDeMedios`. Prueba escrita, sin correr.
- **T0102–T0106**: escritas sobre `CajaConContratos` (los contratos de obligaciones y de cuentas simulados, una asociada con dos créditos y una cuenta de
  aportes, un concepto vario), sin correr. La repetición con otra huella (`Operation.KeyReused`) la responde `IdempotencyBehavior` y la cubre la e2e; el
  rechazo «no deja nada» se comprueba de verdad en la e2e (en InMemory no hay transacción que revertir).
- **T0107**: `LosComandosDeRecaudoYTesoreriaLlevanClave` ya recorría por reflexión todos los `*Command` de `Receipts` (no hay lista que llenar);
  `LosCerrojosDeRecaudoYTesoreriaVanEnOrden` suma la regla «`AplicarAsync` antes que `ConsignarAsync`» en `Application/Receipts`; `LaCarteraSoloSeTocaPorSusInterfaces`
  y `LosPagosNoGuardanElNumeroDeTarjeta` suman las carpetas y espacios de Recaudo (y `ReceiptMeans`). `LasCuentasSeTocanSoloPorElContrato` ya tenía
  `Application/Receipts`.
- **T0108**: las cuatro e2e están escritas sobre `CajaE2E` (escenario del desembolso de Cartera, dos créditos de la misma persona, la caja y la sesión) y
  llaman a `RegistrarYActivarAsync`, que lanza hasta la fase 19. Las aplicaciones de **aportes** de la e2e se suman cuando la fase 22 componga el escenario
  con el de Captaciones (`CaptacionesE2E`); el segundo cajero de `DosCajasMismaPersonaTests` es el jefe del escenario de Cartera y necesita el rol de cajero.
- **T0110**: los hechos con columnas mutables declaradas (`Receipt.Status`, `ReceiptMeans.Status`, `ReceiptApplication.Status`, `ReceiptApplicationPortion.Status`)
  llevan el atributo nuevo `[ColumnaMutable]`, que `GuardaDeInmutabilidad` admite y `LosHechosInmutablesNoSeModifican` fija (sólo en Recaudo y Tesorería).
  Lo que el contrato informa al aplicar (número del producto, comprobante del módulo, firmeza, la operación del módulo de cada porción) es `[EscrituraUnica]`.
  `MeansClass` se guarda como `PaymentMeansClass` del dominio (los números 1 a 9 y 99 son los de `ClaseDeMedioDeRecaudo`, que vive en Application);
  `ApplicationTarget` ya existía desde la fase 4. El `DbSet` del recibo se llama `IssuedReceipts` (un miembro `Receipts` chocaba con el espacio
  `Application.Receipts`).
- **T0111**: la UK del bono es `(PaymentMeansId, VoucherNumber)` filtrada a bonos vivos; la del recibo, `(DocumentTypeId, Number)` y `OperationKey`. Se
  sumaron las FK que esperaban al recibo: `RCD_CashMovements.ReceiptId` y `RCD_CashCountReferences.ReceiptMeansId`.
- **T0112**: la clave de la porción es `REC:{recibo:N}:{aplicación:N}:{clase}` y, desde el segundo medio **de la misma clase** que cubre la misma
  aplicación, `…:{clase}-{n}` (dos transferencias no comparten clave) (DT §2.7).
- **T0113**: la persona se busca por su `PublicId`, su documento o su código (el lector); **la búsqueda por número de obligación no se construyó**: Cartera no
  ofrece ese puerto (`IConsultaDeObligaciones` busca por persona) y Recaudo no lee `LND_`. Queda para cuando Cartera lo ofrezca (decisión pendiente).
  **Resuelto por la decisión del dueño del 2026-10-24**: Cartera y Captaciones ofrecen el puerto de lectura `IBusquedaDeObligaciones.PorNumeroAsync`
  (`BusquedaPorNumero` → `ObligacionEncontradaDto`: clase `Loan`/`Account`, obligación, número, persona dueña con documento y nombre, estado), declarado con los
  demás puertos de Cartera en `Application/Common/Integration/Lending` e implementado en `Application/Lending/Integration/BusquedaDeObligaciones` (créditos por su
  número y deudor; cuentas de captación y aportes por su número y titular) —`LaCarteraSoloSeTocaPorSusInterfaces` exige ese espacio a todo puerto ofrecido y
  ahora cuenta ocho—. `GetPayablesQuery` suma `ObligationNumber` (`GET /api/receipts/payables?number=`): sin coincidencia, `Receipts.Payables.PersonNotFound`;
  con dos personas, `Receipts.Payables.AmbiguousNumber` (nuevo). La caja (`/recaudo/caja`) busca también por número (campo junto al buscador de personas, Enter).
  Pruebas `BusquedaPorNumeroTests`, escritas sin correr.
- **T0114**: el fondeo exacto del CDAT lo valida el contrato de cuentas al simular y al consignar (la caja no conoce el valor del título).
- **T0115**: `ConfirmacionDeRecibo` numera después de los cerrojos propios y **antes de los contratos** (decisión del dueño del 2026-10-23). Guarda por pasos
  dentro de la misma transacción (las filas hijas necesitan el `Id` de su padre y las entidades no tienen navegaciones): sigue siendo todo o nada. El gancho
  de listas es `IListasDeRecaudo` (responde `Clear` hasta la fase 15). La validación previa contable usa la respuesta provisional de la fase 5. La
  declaración de origen de fondos es exigible desde `Sarlaft.UmbralDeclaracionOrigenFondos` o si el contrato de cuentas la pidió; quien efectúa
  (`DeliveredByRequired`) **no se exige todavía**: falta la regla de cuándo es obligatorio (decisión pendiente).
  **Resuelto por la decisión del dueño del 2026-10-24**: quien efectúa el pago (`Receipts.Compliance.DeliveredByRequired`) es **obligatorio** cuando el efectivo
  del recibo llega al umbral de la transacción individual —el parámetro con vigencia `Sarlaft.UmbralTransaccionIndividual`, sembrado en 5.000.000 «pendiente de
  cumplimiento», nunca un literal— y cuando el pagador no es el titular de lo que paga; en lo demás es opcional. `ConfirmacionDeRecibo` lo valida antes de las
  listas; el error trae `data.reason` (`CashThreshold` o `PayerIsNotHolder`) y `data.threshold`. Para registrarlo en una operación ya creada nace
  `SetCashOperationDeliveredByCommand` (`PUT /api/receipts/cash-operations/{id}/delivered-by`, `Receipts.Receipt.Register`, nuevo), y la caja lo pide al recibir el
  error («Registrar y confirmar»). Pruebas `QuienEfectuaElPagoTests`, escritas sin correr.
- **T0116**: `ReciboEmitido` (`RCE`) con una línea por porción; el modo por defecto es `PorLotes` (`REC.RCE|CierreDeTurno||PorDia`, alcance la sesión),
  `EnLinea` y `NoPasa` según `Contabilidad.ModoDePaso`, y sin contabilidad iniciada `NotPosted`. `ElCatalogoDeMensajesEsUnico` cuenta 9 tipos `REC`.
- **T0117**: el pagador tercero viaja como `PayerPublicId` en cada porción y el recibo sale enmascarado (`RecibosDeCaja.Enmascarar`, sin saldos); el pagador
  que no es asociado y paga en efectivo sobre el umbral levanta `Recaudo.PagadorOcasionalSobreUmbral`. El alta mínima usa `PersonaDialog` (modo compacto,
  con autorización de datos) desde el buscador de la caja.
- **T0118**: el recibo en tirilla de 80 mm (`ContinuousSize`) y carta, con «COPIA», «ANULADO» y el código de verificación.
- **T0119**: las rutas del recibo viven en `CashDeskEndpoints.cs` (no en un `ReceiptsEndpoints.cs` aparte); `DistributionChanged` responde 409. Además de api.md §4:
  `GET /api/receipts/cash-operations/{id}`, `GET /api/receipts/receipts[/{id}]`.
- **T0120, T0121**: `/recaudo/caja` (tres pasos, `PersonSearchPicker`, F2/F8/F9/Esc) y `/recaudo/recibos`, con su cliente `CajaClient` y el menú. Sin probar en
  navegador (T0123 es manual).
- **T0122**: el par `RecaudoNucleo`/`TesoreriaNucleo` regenerado con las seis tablas; paridad y `has-pending-model-changes` limpios en los dos motores.

---

## Phase 7: User Story 10 - Cuentas bancarias propias y ejecución de órdenes de giro (Priority: P1) — entrega T0

**Goal**: las cuentas bancarias propias con su vínculo **único** a la cuenta contable (en Contabilidad), sus saldos y el
fondo de liquidez exclusivo; la bandeja única de órdenes que consume `OrdenDeGiroEmitida` v1 y v2 y su anulación; la
validación de cada orden (cuenta destino registrada, banco con código, listas); el lote por cuenta de origen y forma
(archivo, portal del banco, cheques) con su GMF estimado; la aprobación por monto; el archivo de pago inmutable con su
huella, descargado por otra persona y marcado enviado (FR-100 T0, FR-102, FR-103, FR-106 a FR-116, FR-127, FR-205 a
FR-207, FR-209, FR-213, FR-214).

**Independent Test**: recibir tres órdenes (un desembolso v1, la devolución del retiro de un asociado v2 y una devolución
de Recaudo), armar el lote en la cuenta de pagos con `PAGOS-GENERICO`, ver total, cantidad y GMF estimado, que lo apruebe
otra persona, generar el archivo y descargarlo con una tercera; una orden repetida no se duplica, una con valor cero se
rechaza y un lote desde la cuenta del fondo de liquidez se niega sin uso aprobado.

### Tests for User Story 10

- [X] T0125 [P] [US10] Escribir los casos dorados del GMF (≈8) en tests/IngenIA365ERP.Domain.Tests/Treasury/Casos/gmf-*.json con tests/IngenIA365ERP.Domain.Tests/Treasury/EvaluadorDeGmfDeEgresosTests.cs y tests/IngenIA365ERP.Domain.Tests/Common/Taxation/CalculoDeGmfTests.cs (R-25; FR-100, FR-205, FR-206): tarifa recibida como fracción (incluida la cero), redondeo por parámetro, hecho generador por clase de egreso u orden, exención del desembolso por operación (abono en cuenta del deudor o cheque de cruce de primer beneficiario), lo que el origen ya liquidó no se evalúa
- [X] T0126 [P] [US10] Escribir los casos dorados del armado de lotes (≈6) en tests/IngenIA365ERP.Domain.Tests/Treasury/Casos/lote-*.json con tests/IngenIA365ERP.Domain.Tests/Treasury/ArmadoDeLoteTests.cs: selección por cuenta y forma, pendientes con su motivo, marcas (`RecentAccountChange`, `SharedDestination`, `InternalUserAccount`, `ImportedAccountWithoutCertificate`), total y cantidad al peso, una orden a lo sumo en un lote vigente (FR-110, FR-116; SC-011, SC-013)
- [X] T0127 [P] [US10] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/BankAccounts/CuentasBancariasPropiasTests.cs (FR-102, FR-103, FR-207): banco con `TransferCode` (`Treasury.BankAccount.BankWithoutTransferCode`), UK `(BankId, AccountNumber)`, propósitos con `LiquidityFund` exclusivo (`.LiquidityFundExclusive`), el banco como tercero, vínculo único por `IContabilidadParaTesoreria.VincularCuentaBancariaAsync` en la misma transacción (`Accounting.OwnBankAccount.AlreadyLinked`), sin vínculo `.NotLinked`; saldo en libros, último extracto y disponible proyectado; lectura sensible `Treasury.BankBalances.Read`
- [X] T0128 [P] [US10] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/LiquidityFund/FondoDeLiquidezTests.cs (FR-207; SC-028): ningún lote, remesa ni traslado desde la cuenta del fondo sin un uso `Approved` por el monto (`Treasury.LiquidityFund.UseRequired`); uso con causal de la norma, soporte, aprobación por otra persona y alerta `Tesoreria.UsoDelFondoDeLiquidez`; los abonos no se restringen
- [X] T0129 [P] [US10] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Orders/BandejaDeOrdenesTests.cs (R-18; FR-107 a FR-109, FR-114, FR-115; mensajes.md §5): consumo de v1 y v2 con recibo único en `TES_InboundMessages`; repetida sin efecto; `NetAmount ≤ 0` → `RejectedOnArrival` con `Treasury.Order.InvalidAmount`; `PaidByFile` → no entra a lotes (`.PaidByLendingFile`); `ReplacesOrderPublicId` enlaza la cadena; validación (beneficiario, cuenta registrada activa, `DestinationWaiting`, banco con código, lista vinculante → `Pending` con alerta); anulación según el estado (sale del lote no enviado; `Sent` deja `VoidRequested`); forma efectivo inexistente; Tesorería no registra facturas ni causa gastos: en T0 sólo entran órdenes de un módulo (Cartera, Captaciones, Recaudo), nunca una orden con factura o gasto propio (FR-127)
- [X] T0130 [P] [US10] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Batches/LotesDePagoTests.cs (FR-110, FR-111, FR-113, FR-116, FR-213, FR-214): cuenta de origen del banco del formato (`Treasury.Batch.FormatMismatch`) y no del fondo (`.SourceIsLiquidityFund`); vista previa con el formato vigente o el genérico; GMF estimado; quien prepara no aprueba (`.SelfApproval`); niveles por monto; rechazo de la aprobación vuelve a `Draft`; forma portal del banco sin archivo; pago a tercero con soporte; `Treasury.Order.AlreadyInBatch`
- [X] T0131 [P] [US10] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/PaymentFiles/ArchivoDePagoTests.cs (R-20; FR-112): `FlatFileWriter` en `TreasuryPayments = 3`; adjunto no borrable `TreasuryPaymentFile` con SHA-256 subido **antes** de guardar la fila; huella por línea; descarga por otra persona que la que preparó (`Treasury.File.SameUserDownload`) y auditada; marcar enviado con quién lo cargó y quién lo autorizó en el banco (no emite hecho ni resultado); un archivo no enviado se anula y sus órdenes vuelven a `Ready`
- [X] T0132 [US10] Llenar `NoHayDoblesDePrueba` (`DestinoTesoreria` es el único `IDestinoDeMensajes` de `Treasury` admitido) y ampliar `UnSoloConsumidorPorDestino` y `LosComandosDeConsumoNoTienenRuta` (los `Consume…Command` de Tesorería) en tests/IngenIA365ERP.Architecture.Tests/Principles/ (R-26; FR-151)
- [X] T0133 [P] [US10] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/OrdenesYLotesDeGiroTests.cs (US10 «Independent Test», quickstart recorrido G pasos 1, 3–5) y tests/IngenIA365ERP.API.IntegrationTests/Treasury/FondoDeLiquidezRestringidoTests.cs (SC-028); se corren en la fase 22

### Implementation for User Story 10

- [X] T0134 [P] [US10] Implementar los motores puros src/Core/IngenIA365ERP.Domain/Common/Taxation/CalculoDeGmf.cs y src/Core/IngenIA365ERP.Domain/Treasury/Gmf/EvaluadorDeGmfDeEgresos.cs hasta T0125, y src/Core/IngenIA365ERP.Domain/Treasury/Lotes/ArmadoDeLote.cs hasta T0126
- [X] T0135 [P] [US10] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/BankAccounts/ `OwnBankAccount` (`TES_BankAccounts`), `LiquidityFundUse`; en Entities/Treasury/Payments/ `TreasuryInboundMessage`, `PaymentOrder`, `PaymentBatch`, `PaymentBatchItem`, `PaymentFile`, `PaymentFileLine`; enums `BankAccountPurpose` (flags), `PaymentOrderOrigin`, `PaymentForm`, `PaymentOrderState`, `PaymentBatchForm`, `PaymentBatchState` **(nuevo, valores de data-model §10.3)** con los números de §20 (data-model §9, §10.1–§10.5)
- [X] T0136 [US10] Configurar sus tablas en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury/{BankAccounts,Payments}/ (UK `(Origin, OriginOrderPublicId)`, único filtrado de `TES_PaymentBatchItems` vigentes por orden, índices de data-model §10.2, FK `PersonBankAccountId` a `COR_PersonBankAccounts`, sin FK a `RCD_` ni a `ACC_`)
- [X] T0137 [US10] Crear `ACC_OwnBankAccountLinks` en src/Core/IngenIA365ERP.Domain/Entities/Accounting/Integration/OwnBankAccountLink.cs con su configuración (único vigente en los dos sentidos, `ChartOfAccountId` bancaria por `EsBancaria`) e implementar en src/Core/IngenIA365ERP.Application/Accounting/Treasury/ContabilidadParaTesoreria.cs los métodos `CuentasBancariasDelPlanAsync`, `VincularCuentaBancariaAsync`, `VinculoAsync`, `SaldoEnLibrosAsync` (E-04; R-22; data-model §17.3); generar la primera versión del par `IntegracionContableDeRecaudoYTesoreria` con esa tabla (se regenera en la fase 16)
- [X] T0138 [US10] Implementar en src/Core/IngenIA365ERP.Application/Treasury/BankAccounts/ `CreateOwnBankAccountCommand` (con el vínculo en la misma transacción), `UpdateOwnBankAccountCommand`, `InactivateOwnBankAccountCommand`, `GetOwnBankAccountBalancesQuery` (FR-103; lectura sensible) y la implementación de `ICuentasBancariasPropias` para Recaudo (cuentas de recaudo con alcance) (FR-102, FR-106)
- [X] T0139 [US10] Implementar en src/Core/IngenIA365ERP.Application/Treasury/LiquidityFund/ `RequestLiquidityFundUseCommand`, `ApproveLiquidityFundUseCommand`, `RejectLiquidityFundUseCommand` y la guarda `GuardaDelFondoDeLiquidez` que consultan lotes, remesas y traslados (FR-207)
- [X] T0140 [US10] Implementar el consumidor único src/Core/IngenIA365ERP.Application/Treasury/Integration/DestinoTesoreria.cs (destino `Treasury`) con `ConsumePaymentOrderCommand` (v1 y v2) y `ConsumePaymentOrderVoidCommand` (mensajes.md §5.1, §5.2), como «Proceso de integración», con recibo único en el mismo `SaveChanges` y evaluación `Processed`/`Rejected` visible con reproceso (FR-107, FR-115, FR-151) **ruta crítica**
- [X] T0141 [US10] Registro transitorio de los consumidores reales mientras existan los dobles: `DestinoTesoreria` y `DestinoRecaudo` se registran en DI sólo con `Lending:TestDoubles:Enabled = false` (y los dobles sólo con `true`), para que `ConsumidoresPorDestino.Validar()` nunca vea dos consumidores; anotarlo en src/Core/IngenIA365ERP.Application/Lending/Integration/DoblesDePrueba/DoblesDePruebaDeCarteraEnDi.cs y en docs/operaciones/cartera-dobles-de-prueba.md. Es la **bandera transitoria** del 04/11 al 19/11 (DT Q9, R-26): `false` en DEV desde el hito del 04/11 (T0184), en QA desde el 10/11 (T0271) y en la fixture de las e2e antes de la puerta (T0300a); la fase 19 la borra con los dobles (T0307) (FR-223; CH4) **ruta crítica**
- [X] T0142 [US10] Implementar src/Core/IngenIA365ERP.Application/Treasury/Orders/ValidadorDeOrdenes.cs (mensajes.md §5.3: beneficiario, cuenta registrada activa por `ICuentasBancariasDePersona`, espera, banco con `TransferCode`, listas en `ScreeningContext.Disbursement` sobre beneficiario y titular, marcas), `RevalidatePaymentOrderCommand`, `ListPaymentOrdersQuery` y `GetPaymentOrderChainQuery` (FR-109, FR-121, FR-203, FR-210)
- [X] T0143 [US10] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Batches/ `CreatePaymentBatchCommand`, `UpdatePaymentBatchItemsCommand`, `PreviewPaymentBatchQuery` (formato vigente del banco de la cuenta o `PAGOS-GENERICO`, total, cantidad, GMF estimado por `EvaluadorDeGmfDeEgresos`), `SubmitPaymentBatchCommand`, `ApprovePaymentBatchCommand`/`RejectPaymentBatchCommand` (aprobación diferida por monto; listas vinculantes consultadas antes de aprobar) con `CerrojoDeTesoreria` (FR-110, FR-111, FR-203, FR-206)
- [X] T0144 [US10] Implementar en src/Core/IngenIA365ERP.Application/Treasury/PaymentFiles/ `GeneratePaymentFileCommand` (`FlatFileWriter`, adjunto antes de la fila, SHA-256 por archivo y por línea), `DownloadPaymentFileQuery` (otra persona, auditada), `MarkPaymentFileSentCommand`, `VoidPaymentFileCommand` y la forma «portal del banco» sin archivo (FR-112, FR-213, FR-214)
- [X] T0145 [US10] Emitir el hecho `GmfDePagoCausado` cuando el pago dispone de una cuenta contable a favor de un tercero (devoluciones de saldos a favor y de partidas) en src/Core/IngenIA365ERP.Application/Treasury/Accounting/EmisionDeTesoreria.cs (base del emisor `TES`, `TreasuryFactDimensionsV1`), con la exención por operación sellada en la orden; ampliar `ElCatalogoDeMensajesEsUnico` (FR-142, FR-205, FR-206; Q8)
- [X] T0146 [US10] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/{BankAccountsEndpoints,LiquidityFundEndpoints,PaymentOrdersEndpoints,PaymentBatchesEndpoints,PaymentFilesEndpoints}.cs con las rutas de api.md §9 y §10 (sin la respuesta ni los resultados, que son de US11) y sus permisos
- [X] T0147 [US10] Crear los clientes src/Presentation/IngenIA365ERP.Shared/Services/Tesoreria/{CuentasBancariasClient,OrdenesClient,LotesClient}.cs y las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/{CuentasBancarias,FondoDeLiquidez,Ordenes,Lotes,ArchivosDePago}.razor (`/tesoreria/cuentas-bancarias` con el vínculo contable y el alcance por cuenta, `/tesoreria/fondo-de-liquidez`, `/tesoreria/ordenes` con la cadena de regiros, `/tesoreria/lotes`, `/tesoreria/archivos-de-pago`) con `PermissionGate` e `IndicadorDeCarga` por zona; primer enlace del grupo «Tesorería» del menú
- [X] T0148 [US10] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaNucleo` con las tablas de cuentas, fondo, órdenes, lotes y archivos (data-model §9, §10.1–§10.5)
- [ ] T0149 [US10] P1 · Entregar qué bancos y cuentas usa COOFLOPAL el primer día y sus estructuras de extracto, de pagos y de respuesta de pagos (el formato de pagos de AV Villas sigue pendiente desde la 010, T147); cargarlas como datos en `/maestros/formatos-bancarios` y registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 06/11; **bloquea la salida sólo si no se acepta** `PAGOS-GENERICO`, `EXTRACTO-GENERICO` o la forma «portal del banco», FR-213)
- [ ] T0150 [US10] P14 · Confirmar si COOFLOPAL necesita devolver aportes al retiro en efectivo por caja (exige que Captaciones lo autorice por el contrato, R1) o basta la orden de giro; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 13/11; no bloquea: rige la devolución por orden de giro, FR-193)


**Notas de la construcción de la fase 7 (2026-10-24)**. Estrategia del dueño: las pruebas se escriben y se compilan, y se corren una sola vez al terminar el plan
(«prueba escrita, sin correr»). Desviaciones y decisiones; las que nacen aquí quedan en decisiones-transversales §2.7:

- **T0059 (mitad de Tesorería)**: `GetTreasuryUserScopeQuery`/`SetTreasuryUserScopeCommand` en `Application/Treasury/Security/TreasuryUserScopeCommands.cs` y
  las rutas `GET/PUT /api/treasury/users/{userId}/scope` (`Treasury.Scope.Manage`) en `BankAccountsEndpoints.cs`. `TES_UserBankAccountScopes` ganó su FK a
  `TES_BankAccounts` al regenerar `TesoreriaNucleo`.
- **T0125, T0126, T0134**: 10 casos dorados del GMF (`gmf-*.json`) y 6 del armado (`lote-*.json`). `CalculoDeGmf` (Domain/Common/Taxation) y
  `EvaluadorDeGmfDeEgresos` (Domain/Treasury/Gmf) reciben tarifa, hechos generadores, exención de desembolsos y redondeo como `ParametrosDeGmf`; el desembolso a un
  tercero se **estima** gravado sin hecho propio (lo cobra el banco en el débito, FR-100); sólo las devoluciones de Recaudo emiten `GmfDePagoCausado`.
  «Beneficiario es el deudor» se sella en la orden (`PaymentOrder.BeneficiaryIsDebtor`, nuevo) comparando el tercero del sobre con el beneficiario. Pruebas
  escritas, sin correr.
- **T0127–T0131**: escritas sobre `EscenarioDeTesoreria` (banco con y sin código, cuentas `PAGOS` y `FONDO`, beneficiaria con su cuenta registrada, cerrojo y
  Contabilidad simulados, listas limpias), sin correr.
- **T0132**: `NoHayDoblesDePrueba` admite `DestinoTesoreria`; `UnSoloConsumidorPorDestino` no cuenta la fuente de los dobles pendientes de retiro (el arranque
  sigue validando que sólo uno se registre); `LosComandosDeConsumoNoTienenRuta` suma los dos `Consume…Command`.
- **T0133**: las dos e2e están escritas sobre `TesoreriaE2E` (escenario del desembolso de Cartera, cuenta de pagos vinculada, cuenta destino aprobada por otra
  persona) y llaman a `RegistrarYActivarAsync`, que lanza hasta T0183b/fase 19. Sin correr (fase 22).
- **T0135, T0136**: `OwnBankAccount` lleva `Code` (`CodigoDeCatalogo`, dimensión `OwnBankAccountCode`) y `Name` (nuevos); enums nuevos `OwnBankAccountType`,
  `TreasuryCatalogStatus`, `LiquidityFundCause`, `LiquidityFundUseState`, `PaymentBatchItemFlags`, `PaymentBatchItemState` y, para la fase 8,
  `PaymentResponseSource`, `PaymentResponseOutcome`; `InboundMessageOutcome` (Domain/Enums/Integration) lo comparten `TES_` y `RCD_InboundMessages`. La orden sella
  beneficiario, documento, banco y cuenta del mensaje (`BeneficiaryName`, `BeneficiaryTaxId`, `DestinationBankCode`, `BranchPublicId`, `VoidedBy*`, nuevos). El
  único filtrado «una orden en un lote vigente» usa la columna `PaymentBatchItem.IsLive` (nueva) y el índice `UK_TES_PaymentBatchItems_PaymentOrderId_Live`
  (`[IsLive] = 1`): así no depende del estado de otra tabla. El lote suma `PayDate`, `LiquidityFundUseId` y quién lo envió (`SentAt`, `SentByUserId`,
  `UploadedAtBankBy`, `AuthorizedAtBankBy`). `PaymentFile` y `PaymentFileLine` son hechos (`IHechoInmutable`) con sus columnas de escritura única.
- **T0137**: `IContabilidadParaTesoreria.VincularCuentaBancariaAsync` responde `Task<Result>` (interfaces.md decía `Task`) para que un vínculo repetido sea un rechazo
  y no una excepción, y el puerto suma `EstadoDelPeriodoAsync` (la fecha contable de los hechos). Errores `Accounting.OwnBankAccount.AlreadyLinked` y
  `.NotBankAccount` (nuevo). El par `IntegracionContableDeRecaudoYTesoreria` nace con `ACC_OwnBankAccountLinks` (se regenera en la fase 16).
- **T0138**: además de api.md §9, `GET /api/treasury/bank-accounts/{id}` y `GET /api/treasury/bank-accounts/chart-accounts?bank=` (las cuentas bancarias del plan por
  el puerto, para elegir el vínculo). El vínculo es opcional al crear: sin él la cuenta no sirve de origen de lotes (`Treasury.BankAccount.NotLinked`). El catálogo
  `cuentas-bancarias-propias` avisa el código repetido. Cheques y remesas en tránsito del disponible proyectado quedan en cero hasta la fase 14.
- **T0139**: el uso del fondo numera con la clase `UF`; `GuardaDelFondoDeLiquidez` la consultan los lotes (remesas y traslados en sus fases); `AprobableUsoDelFondo`
  deja el uso aprobado o rechazado; quien lo pidió no lo aprueba (`Treasury.LiquidityFund.SelfApproval`, nuevo).
- **T0140**: `DestinoTesoreria` reparte por la clave de evento (la orden llega con `Confirmation`; su anulación con `Confirmation:{orden:N}`). La orden numera con la
  clase `OP`. Una orden repetida deja su recibo `Processed` sin efecto; una forma que no existe (efectivo) queda `RejectedOnArrival` con `Treasury.Order.InvalidForm`
  (nuevo); el beneficiario inexistente se rechaza sin crear orden (`Treasury.Order.BeneficiaryNotFound`); la anulación de una orden que no está en la bandeja se
  rechaza con `Treasury.Order.UnknownOrder` (nuevo). El resultado «anulada» lo registra `RegistroDeResultadosDelGiro`, que en esta fase sólo deja la orden `Voided`
  con quién anuló sellado: **la emisión de `ResultadoDelGiro` `Voided` llega con la fase 8** (T0158), donde nace `TES_DisbursementResults`.
- **T0141**: la bandera transitoria vive en `API/Integration/DoblesDePruebaDeCartera.cs`: si los dobles no se registran, `ConsumidoresDeRecaudoYTesoreria.Registrar`
  registra `DestinoTesoreria` (y `DestinoRecaudo` desde la fase 10). Anotada en `DoblesDePruebaDeCarteraEnDi.cs` y en `docs/operaciones/cartera-dobles-de-prueba.md` §1.1.
- **T0142**: las listas de Tesorería son el gancho `IListasDeTesoreria` con implementación real (`ListasDeTesoreria`, contexto `Disbursement`, canal vigente de la
  cooperativa) que **sólo lee**: Tesorería no guarda resultados de Cartera. La plataforma no distingue la lista vinculante: una coincidencia exacta (`Match`) cuenta
  como vinculante y una posible (`PossibleMatch`) como no vinculante (**decisión pendiente del dueño con el oficial**). La bandeja no lleva alcance por cuenta (una
  orden no tiene cuenta de origen hasta el lote).
- **T0143**: lote con clase `LP`; la aprobación es la diferida de la 012 (sujeto `PaymentBatchApproval`, un nivel sin política) y `AprobableLoteDePago` deja lote y
  órdenes aprobados y marca usado el uso del fondo. Las listas vinculantes se consultan antes de aprobar (`GuardasDeAprobacionDelLote`;
  `Treasury.Compliance.BindingListMatch`). Además de api.md §10: `GET /payment-batches` y `GET /payment-batches/{id}`. Errores nuevos
  `Treasury.Batch.{NotDraft, NotPendingApproval, Empty, FormatNotFound, NoFileForm, SourceNotPayments}`. Un lote de cheques queda aprobado hasta la fase 14.
- **T0144**: la forma «portal del banco» se marca enviada con `POST /api/treasury/payment-batches/{id}/sent` (`MarkPaymentBatchSentByPortalCommand`, nuevo); la descarga
  es `GET /payment-files/{id}/download` con un enlace firmado (`DownloadPaymentFileCommand`; un adjunto del formato anterior responde `direct: false`); además
  `GET /payment-files`. Anular un archivo no enviado anula el lote y devuelve sus órdenes listas. Si el origen anula una orden con el archivo generado y no enviado,
  el archivo se anula solo y el lote vuelve a aprobado (`LotesDePago.SacarAsync`).
- **T0145**: `EmisionDeTesoreria` y el catálogo `CatalogoDeMensajesDeTesoreriaV1` (`GMP`, y `POG` para la fase 8) con `TreasuryFactDimensionsV1`,
  `TreasuryFactLineV1` y `RolesDeTesoreria`. `GmfDePagoCausado` se emite al «girada» (fase 8), cuando el dinero sale. `RecaudoNoConoceContabilidad` y
  `TesoreriaNoConoceContabilidad` dejan fuera el emisor del propio módulo (`Application.{Receipts,Treasury}.Accounting`), que la regla contaba como Contabilidad.
- **T0146**: las rutas viven en dos archivos, `BankAccountsEndpoints.cs` (cuentas, saldos, alcance, fondo) y `PaymentsEndpoints.cs` (órdenes, lotes, archivos).
- **T0147**: pantallas `/tesoreria/cuentas-bancarias` (vínculo, saldos y alcance por cuenta), `/tesoreria/fondo-de-liquidez`, `/tesoreria/ordenes` (con la cadena de
  regiros), `/tesoreria/lotes` y `/tesoreria/archivos-de-pago`, y el grupo «Tesorería» del menú. Los soportes se citan por el identificador del adjunto. Sin
  probar en navegador.
- **T0148**: `TesoreriaNucleo` se regeneró con las ocho tablas nuevas (y la FK del alcance), conservando la semilla de `TES_Setup`; `IntegracionContableDeRecaudoYTesoreria`
  nace después. El snapshot de PostgreSQL se revirtió copiando el modelo del diseñador de `RecaudoNucleo` (`ef migrations remove` exige conectarse a la base).
  Paridad OK (66 en par); `has-pending-model-changes` da «No changes» en SQL Server y en PostgreSQL.
- **T0149, T0150**: del dueño; no se ejecutan.

---

## Phase 8: User Story 11 - Confirmar, rechazar y regirar (Priority: P1) — entrega T0

**Goal**: la respuesta del banco leída con su formato de vuelta y cotejada línea por línea contra el archivo generado; el
resultado por orden (girada, rechazada, cheque no cobrado, anulada) con las transiciones fijas de FR-215, registrado
siempre por una persona y emitido como `ResultadoDelGiro` v1 sin cambiarlo; el rechazo técnico del archivo; la cadena de
regiros; el hecho de pago al confirmar el banco; la alerta de órdenes sin resultado (FR-118 a FR-123, FR-142, FR-212,
FR-213, FR-215).

**Independent Test**: de un archivo de tres órdenes el banco rechaza una por cuenta inexistente: dos quedan «girada» con
su hecho contra el puente de cada origen y una «rechazada» con su causal; Cartera reversa el desembolso y emite el regiro,
que aparece en la cadena; registrar el mismo resultado dos veces no cambia nada; una línea con la cuenta alterada no se
informa hasta revisarla.

### Tests for User Story 11

- [X] T0151 [P] [US11] Escribir los casos dorados de las transiciones (≈8) en tests/IngenIA365ERP.Domain.Tests/Treasury/Casos/resultado-*.json con tests/IngenIA365ERP.Domain.Tests/Treasury/TransicionesDelResultadoTests.cs (R-18; FR-215; data-model §19.9): `Sent` transferencia admite `Disbursed`/`Rejected`; `Sent` cheque sólo `Disbursed`; `Disbursed` cheque sólo `CheckNotCashed`; `Disbursed` transferencia nada; anulación del origen antes de `Sent` → `Voided`; antes de `Sent` sin anulación → `Treasury.Result.NotSent`
- [X] T0152 [P] [US11] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Results/RespuestaDelBancoTests.cs (FR-118, FR-212): `FlatFileReader` en `TreasuryPaymentResponse = 9` o `RESPUESTA-PAGOS-GENERICO`; cotejo de beneficiario, cuenta y valor contra la huella de la línea del archivo; una discrepancia alerta `Tesoreria.DiscrepanciaEnRespuesta` y la línea queda sin informar hasta `ReviewPaymentResponseLine` (`Treasury.Result.Discrepancy`)
- [X] T0153 [P] [US11] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Results/ResultadoDelGiroTests.cs (R-18, R-19; FR-118 a FR-121, FR-123, FR-213, FR-215): `ResultadoDelGiro` v1 sin cambios, a nombre de la persona que lo registra, clave `Confirmation:{orden:N}`, `Amount = NetAmount`; UK `(PaymentOrderId, Outcome)` repetido sin efecto; registro manual y en bloque con soporte obligatorio (`Treasury.Result.SupportRequired`); «girada» nunca al marcar enviado; rechazo técnico del archivo → `RejectedByBank` y órdenes a `Ready` sin resultado; hecho `PagoDeOrdenGirado` al «girada» con la fecha del débito (Q3), sin hecho en un rechazo previo; las órdenes de Recaudo informan por `IResultadoDeOrdenesDeRecaudo`; la tarea `tesoreria.vigilancia` alerta `Tesoreria.OrdenSinResultado` a `Giros.DiasSinResultado` sin registrar nada
- [X] T0154 [US11] Llenar `NingunProcesoAplicaPagosNiInformaResultados` en tests/IngenIA365ERP.Architecture.Tests/Principles/ (ninguna `ITareaProgramada` de `Receipts` o `Treasury` llama a `IContratoDeObligaciones`, `IContratoDeCuentas` ni a los comandos de resultado) y ampliar `EmisionesDeProcesoPermitidas` para `TES` (R-18; FR-174)
- [X] T0155 [P] [US11] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/GiroRechazadoYRegiroTests.cs (US11 «Independent Test», quickstart recorrido G pasos 6–7) sobre Cartera real; se corre en la fase 22

### Implementation for User Story 11

- [X] T0156 [P] [US11] Implementar el motor puro src/Core/IngenIA365ERP.Domain/Treasury/Resultados/TransicionesDelResultado.cs hasta T0151
- [X] T0157 [P] [US11] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Payments/ `PaymentResponse`, `PaymentResponseLine`, `DisbursementResult` (H, UK `(PaymentOrderId, Outcome)`) y el enum `DisbursementOutcome`; configurarlas en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury/Payments/ (data-model §10.6, §10.7)
- [X] T0158 [US11] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Results/ `LoadPaymentResponseCommand` (cotejo por línea), `ReviewPaymentResponseLineCommand`, `RegisterDisbursementResultCommand` (manual con soporte), `RegisterDisbursementResultsBulkCommand`, `MarkPaymentFileRejectedByBankCommand`, con `TransicionesDelResultado`, emisión de `ResultadoDelGiro` v1 por `EmisorDeMensajes` a nombre de la persona y la implementación de `IResultadoDeOrdenesDeRecaudo` para las órdenes de Recaudo (R-18, R-19; FR-118 a FR-121, FR-212, FR-213)
- [X] T0159 [US11] Emitir `PagoDeOrdenGirado` (contra `PuenteDeGiroDelOrigen` por `OrderOrigin` y `OrderKind`, con la fecha del débito) en src/Core/IngenIA365ERP.Application/Treasury/Accounting/EmisionDeTesoreria.Pagos.cs y ampliar `ElCatalogoDeMensajesEsUnico` (contabilidad.md §4; FR-142, FR-144)
- [X] T0160 [US11] Implementar la tarea `tesoreria.vigilancia` en src/Core/IngenIA365ERP.Application/Treasury/Tareas/VigilanciaDeTesoreria.cs (`ITareaProgramada` por `IEjecutorEnCooperativa`): órdenes sin resultado a `Giros.DiasSinResultado`, cheques sin cobrar, remesas sin confirmar, partidas conciliatorias antiguas, cuadre provisional y política en «advertir» (cada sección la completa su fase); sólo alerta (FR-019, FR-123, FR-174; SC-012; DT §2.5)
- [X] T0161 [US11] Exponer en src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/PaymentResultsEndpoints.cs las rutas `POST /api/treasury/payment-files/{id}/responses`, `GET …/{rid}`, `POST …/{rid}/lines/{lineId}/review`, `POST /api/treasury/payment-orders/{id}/results`, `POST /api/treasury/payment-orders/results/bulk` y `POST /api/treasury/payment-files/{id}/rejected-by-bank` con `Treasury.Results.Register` / `Treasury.PaymentFiles.Void` (api.md §10)
- [X] T0162 [US11] Crear src/Presentation/IngenIA365ERP.Shared/Services/Tesoreria/ResultadosClient.cs y la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Resultados.razor (`/tesoreria/resultados`: carga de la respuesta, líneas con discrepancia por revisar, registro manual y en bloque con soporte, cadena de regiros) con `IndicadorDeCarga` por zona
- [X] T0163 [US11] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaNucleo` con `TES_PaymentResponses`, `TES_PaymentResponseLines` y `TES_DisbursementResults`


**Notas de la construcción de la fase 8 (2026-10-24)**. Pruebas escritas y compiladas, sin correr («prueba escrita, sin correr»). Desviaciones y decisiones;
las que nacen aquí quedan en decisiones-transversales §2.7:

- **T0151, T0156**: 10 casos dorados (`resultado-01..10.json`) sobre `TransicionesDelResultado.Evaluar(estado, forma, pagadaPorArchivo, anulacionDelOrigen,
  resultado)`, que devuelve `TransicionDelResultado(Admite, Hacia, Codigo)`. Además de la tabla de FR-215: la orden que paga un archivo de Cartera (`PaidByFile`)
  admite girada o rechazada estando lista (FR-108), y el pago a tercero sigue las reglas de la transferencia.
- **T0152, T0153**: escritas sobre `EscenarioDeResultados` (lote enviado con su archivo y su huella por línea, formato `RESP-PAGOS-GENERICO`, soporte del registro
  manual, calendario sin festivos), sin correr. El cotejo usa la **huella de la línea** (`LineasDelArchivo.HuellaDeLinea`); el detalle dice qué discrepó
  (documento, cuenta, valor, o «sin línea en el archivo generado»). El resultado informado lleva siempre el **neto de la orden**, nunca el valor de la línea.
- **T0154**: `NingunProcesoAplicaPagosNiInformaResultados` lista las tres tareas de los módulos (`TareaDeSesionesOlvidadas` y `TareaDeCuadreDiario` de la fase 5,
  que faltaban, y `TareaDeVigilanciaDeTesoreria`); `EmisionesDeProcesoPermitidas` ya cubría que ningún tipo `TES` admite proceso y suma
  `El_resultado_del_giro_sale_a_nombre_de_una_persona` (cada `SolicitudDeEmision` del registro lleva `UsuarioDeOrigen`). `DisbursementResult` entra a
  `LosHechosInmutablesNoSeModifican`.
- **T0155**: e2e escrita sobre `TesoreriaE2E` (cuatro órdenes: dos giradas, una rechazada, una con la cuenta alterada), sin correr (fase 22). El regiro de Cartera
  (reverso del desembolso y orden nueva enlazada) no se recorre aquí: la cadena ya la arma la fase 7 (`ReplacesOrderPublicId`).
- **T0157**: `PaymentResponse` suma `BankFileFormatId`, `FileName`, `DiscrepancyCount` e `InformedCount`; `PaymentResponseLine` suma `BeneficiaryTaxId`,
  `BankReference`, `DiscrepancyDetail`, `ReviewedAt`, `ReviewAccepted` y `ReviewNote`; `DisbursementResult` (H) suma `Source`, `RecordedByCentralUserId`,
  `RecordedByName` y `RecordedAt`, y `RecordedByUserId` es **nulo** en el «anulada», que nombra a la persona del origen (no tiene `SEC_Users`).
  `EmittedMessagePublicId` es nulo en las órdenes de Recaudo, que se informan por su puerto. `DisbursementOutcome` ya existía (fase 7).
- **T0158**: `RegistroDeResultadosDelGiro` es el único que registra: UK repetida sin efecto, transición, orden, ítem (`Paid`/`Rejected`) y lote
  (`PartiallyConfirmed`/`Confirmed`), y la emisión de `ResultadoDelGiro` v1 con el **resultado como documento de origen** (su `PublicId`; con la orden como origen,
  dos resultados de la misma orden —girada y cheque no cobrado— chocarían en la UK `(OriginPublicId, Type, OriginEventKey)`), la orden del origen como relacionado
  (`DisbursementOrder`) y `UsuarioDeOrigen` = la persona. `TomarCerrojosDeRecaudoAsync` toma los de Recaudo antes que los de Tesorería; `IResultadoDeOrdenesDeRecaudo`
  se inyecta como colección (su implementación llega en la fase 10, T0177). La anulación del origen ahora **sí emite** el «anulada». Comandos además de los de la tarea:
  `GetPaymentResponseQuery`, `ListPaymentResponsesQuery` y `ListDisbursementResultsQuery`. La segregación de tercera persona (`TES_Setup.ResultWithThirdPerson`)
  se aplica a todos los registros de un lote. El soporte del registro manual es un adjunto nuevo `TreasuryResultSupport` (dueño: la orden o el lote; se sube con
  `Treasury.Results.Register`, se lee con `Treasury.Orders.View`, no se borra). El texto del resultado de la respuesta se traduce (`Pagado`/`Rechazado`, `P`/`R`,
  `1`/`2`…); otro texto rechaza la carga entera (`Treasury.Response.UnknownOutcome`). Un rechazo técnico se niega si alguna orden del archivo ya tiene resultado
  (`Treasury.File.HasResults`). Corregido de la fase 7: `EmisionDeTesoreria` mandaba el origen `Document` sin tipo de documento (el emisor lo exige): ahora `OP`.
- **T0159**: `PagoDeOrdenGirado` (`EmisionDeTesoreria.Pagos.cs`) al «girada» de una orden de un lote (no cheque: el cheque es de la fase 14; la orden pagada por el
  archivo de Cartera no tiene cuenta propia hasta E-01 c), débito `PuenteDeGiroDelOrigen` y crédito `Banco`, con la fecha del débito; con él, `GmfDePagoCausado` si la
  evaluación lo pide. `ElCatalogoDeMensajesEsUnico` ya contaba `POG` desde la fase 7.
- **T0160**: `VigilanciaDeTesoreria.OrdenesSinResultadoAsync` cuenta **días hábiles** (sábado y domingo, y los festivos de `ICalendarioHabil`) desde el envío del lote;
  la tarea corre cada 6 horas (la alerta se deduplica por orden). Las demás secciones las suma cada fase.
- **T0161**: además de api.md §10, `GET /payment-files/{id}/responses` y `GET /payment-orders/{id}/results`. La respuesta llega por multipart (campo `archivo`,
  hasta 10 MB) con `format` opcional.
- **T0162**: pantalla `/tesoreria/resultados` (menú «Tesorería · Giros») y `ResultadosClient`. El soporte se sube con `SubirSoporte` y se toma el último adjunto
  disponible de la orden. Sin probar en navegador.
- **T0163**: `TesoreriaNucleo` regenerado con las tres tablas (conservando la semilla de `TES_Setup`) y después `IntegracionContableDeRecaudoYTesoreria`. Paridad OK
  (66 en par); `has-pending-model-changes` da «No changes» en SQL Server y en PostgreSQL.

---

## Phase 9: User Story 26 - Controles antifraude de los pagos (Priority: P1) — entrega T0 (parcial)

**Goal**: la cuenta destino de un pago sólo si está registrada en el maestro de la persona con doble control, certificación
y espera; el lote marca las órdenes a cuentas cambiadas, compartidas, de un usuario interno o importadas sin certificación
y exige su revisión expresa; el archivo alterado se detecta en la respuesta; las listas vinculantes bloquean al
beneficiario (FR-203 en Tesorería, FR-210, FR-212).

**Independent Test**: cambiar la cuenta bancaria de un beneficiario y comprobar que queda pendiente hasta que otra persona
la aprueba con la certificación; la orden queda marcada en el lote y no se aprueba sin su revisión expresa; una
discrepancia inducida entre el archivo y la respuesta se detecta antes de informar; un beneficiario en lista vinculante no
entra a un lote aprobado.

### Tests for User Story 26

- [X] T0164 [P] [US26] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Batches/MarcasDelLoteTests.cs (FR-210; SC-026): la orden a una cuenta cambiada dentro de `CuentasDestino.DiasDeMarca` lleva `RecentAccountChange` y el lote no se aprueba sin `ExplicitlyReviewedBy` (`Treasury.Batch.ReviewRequired`); cuenta compartida y de usuario interno alertan `Plataforma.CuentaDestinoCompartida` y `Tesoreria.CuentaDestinoCambiada`; las importadas sin certificación se marcan (Q4)
- [X] T0165 [P] [US26] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/CuentaDestinoCambiadaYArchivoAlteradoTests.cs (US26 «Independent Test», quickstart recorrido G pasos 2 y 6; SC-026) y la parte de Tesorería de tests/IngenIA365ERP.API.IntegrationTests/Receipts/ListaVinculanteBloqueaTests.cs (beneficiario en lista vinculante: cero órdenes en un lote aprobado, alerta inmediata `Tesoreria.ListaVinculante`; SC-025); se corren en la fase 22

### Implementation for User Story 26

- [X] T0166 [US26] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Batches/ReviewPaymentBatchItemCommand.cs la revisión expresa por una persona distinta de quien preparó (`POST /api/treasury/payment-batches/{id}/items/{itemId}/review`, `Treasury.Batches.Approve`) y la guarda en `ApprovePaymentBatchCommand` (FR-210)
- [X] T0167 [US26] Completar en src/Core/IngenIA365ERP.Application/Treasury/Orders/ValidadorDeOrdenes.cs y src/Core/IngenIA365ERP.Domain/Treasury/Lotes/ArmadoDeLote.cs las marcas `SharedDestination`, `InternalUserAccount` e `ImportedAccountWithoutCertificate` con `ICuentasBancariasDePersona.VerificarAsync`, y la consulta de listas vinculantes sobre todo beneficiario antes de aprobar el lote (`Treasury.Compliance.BindingListMatch`, alerta inmediata al oficial) (FR-203, FR-210)
- [X] T0168 [US26] Mostrar en src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Lotes.razor las marcas por orden y la revisión expresa, y en Resultados.razor las líneas con discrepancia (FR-212), con `IndicadorDeCarga` por zona
- [X] T0169 [US26] Agregar la vista `recent-account-changes` (órdenes marcadas por cambio de cuenta) al registro de vistas de Tesorería (src/Core/IngenIA365ERP.Application/Treasury/Reports/) que crea la fase 20 (FR-153)


**Notas de la construcción de la fase 9 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones (las nuevas, en
decisiones-transversales §2.7):

- **T0164**: escritas sobre `EscenarioDeTesoreria` con `CuentasDestino.DiasDeMarca = 10`: cuenta cambiada hace 3 días (marca, revisión exigida, quien preparó no
  revisa, otra persona sí), hace 30 (sin marca), compartida con otro beneficiario, importada sin certificación, orden sin marcas y lista vinculante al aprobar.
- **T0165**: las dos e2e están escritas (`CuentaDestinoCambiadaYArchivoAlteradoTests` fija `CuentasDestino.DiasDeEspera = 0` y `.DiasDeMarca = 30` por la ruta de
  parámetros para no depender de la semilla; `Receipts/ListaVinculanteBloqueaTests` cambia el documento del beneficiario para que el canal **simulado** lo encuentre,
  termina en 9 = `Match`, y revalida la orden). La parte de Recaudo de `ListaVinculanteBloqueaTests` la suma la fase 15. Se corren en la fase 22; `ResultadosE2E`
  (nuevo) carga la respuesta por multipart.
- **T0166**: `ReviewPaymentBatchItemCommand` en su archivo; la revisión exige **nota** (la pantalla la pide), se hace en borrador o en aprobación, no cambia la huella
  del lote (no cambia lo que se paga) y responde `Treasury.Batch.ItemNotFlagged` (nuevo) para una orden sin marcas. Cualquier marca admite revisión; sólo
  `RecentAccountChange` la exige para aprobar (`GuardasDeAprobacionDelLote`, `Treasury.Batch.ReviewRequired`, antes de las listas).
- **T0167**: las marcas `SharedDestination`, `InternalUserAccount` e `ImportedAccountWithoutCertificate` (con `ICuentasBancariasDePersona.VerificarAsync`) y la consulta de
  listas antes de aprobar ya estaban desde la fase 7; esta fase suma la **alerta inmediata** `Tesoreria.ListaVinculante` cuando la guarda de aprobación encuentra la
  coincidencia (antes sólo alertaba la validación de la orden).
- **T0168**: `Lotes.razor` pide la nota de la revisión y ofrece revisar cualquier orden marcada; `Resultados.razor` (fase 8) ya muestra las líneas con discrepancia.
- **T0169**: no hay todavía registro de vistas de Tesorería (lo arma la fase 20): nace mínimo en `Application/Treasury/Reports/VistasDeTesoreria.cs` con
  `recent-account-changes` (`RecentAccountChangesQuery`, `TablaExportable`, alcance por cuenta, columna oculta `_orden`) y la ruta JSON
  `GET /api/treasury/reports/recent-account-changes?from=&to=` (`Treasury.Orders.View`); la fase 20 la lleva al centro de reportes con su exportación.
- Sin cambios de esquema: no hay migración en esta fase.

---

## Phase 10: User Story 5 - Anular recibos, reversar aplicaciones y atender los reversos de los módulos (Priority: P1) — entrega R0

**Goal**: la anulación del recibo entero por un documento numerado que reversa cada aplicación vigente por su contrato
(obligaciones antes que cuentas), con aprobación de otra persona; el dinero de un recibo anulado según la sesión; el
consumo de `PagoReversadoPorCartera` y `ConsignacionReversadaPorCartera` con la causa del dinero; los saldos a favor de
pagadores y los de Cartera devueltos **por orden de pago a Tesorería** (R0) (FR-053 parte R0, FR-054 parte R0, FR-057 a
FR-059, FR-062 a FR-064, FR-194, FR-195).

**Independent Test**: anular un recibo de dos aplicaciones y comprobar los dos reversos en Cartera y Captaciones, el
documento de anulación numerado, lo esperado de la sesión rebajado y el hecho espejo; reversar desde Cartera un pago de
Recaudo: queda la aplicación reversada, una persona confirma «dinero recibido» y nace un saldo a favor que se devuelve por
orden de pago.

### Tests for User Story 5

- [X] T0170 [P] [US5] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Voidings/AnulacionDeReciboTests.cs (FR-057, FR-058, FR-064, FR-194; D4, D5): se anula entero con motivo; en la sesión abierta y el mismo día lo pide el cajero y lo aprueba otro presente; después, permiso propio y aprobación de otro (`Receipts.Voiding.SelfApproval`); reversa cada porción vigente con su clave `:reverso` en el orden de FR-170, todo o nada (`.ContractRejected`); omite las aplicaciones ya reversadas; sesión abierta → baja lo esperado; sesión cerrada → saldo a favor del pagador sin tocar sesión; medio electrónico abonado → devolución por orden de pago; fechada en el período abierto; hecho `ReciboAnulado` espejo; `.AlreadyVoided`; rechazo devuelve a `Valid`
- [X] T0171 [P] [US5] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Reversals/ReversosDeModulosTests.cs (mensajes.md §3.1; FR-059, FR-062, FR-063, FR-195): la porción de un recibo vigente pasa a `ReversedByModule` con `RCD_ModuleReversals`; sin causa → `PendingConfirmation` y una persona la confirma (`MoneyReceived` → saldo a favor; `MoneyNotReceived` → baja del medio); alerta `Recaudo.AplicacionReversadaPorModulo`; repetido sin efecto; clave desconocida `Receipts.Reversal.UnknownApplication`; de otro recaudador `.NotOurs`; nunca devuelve ni reaplica solo; lo de Nómina no se anula ni reversa desde Recaudo (`Receipts.Voiding.PayrollOwned`)
- [X] T0172 [P] [US5] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CreditBalances/SaldosAFavorTests.cs (FR-053, FR-054; interfaces.md §2): saldo a favor de pagador por resolver con antigüedad y alerta `Recaudo.SaldoAFavorAntiguo`; devolución por orden de pago a Tesorería (`IOrdenesDePagoDeTesoreria.EmitirAsync` en la misma transacción que la baja del saldo, clave `REC:{orden:N}:{obligación:N}`); saldo a favor de Cartera con `ResolverSaldoAFavorAsync(Form = RefundThroughCollector)`; «rechazada» reabre el saldo y «girada» lo cierra; aplicar el saldo con un recibo nuevo
- [X] T0173 [P] [US5] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/RecaudoRecibeReversosDeCarteraTests.cs (US5 «Independent Test», quickstart recorrido C) sobre Cartera y Captaciones reales; se corre en la fase 22

### Implementation for User Story 5

- [X] T0174 [P] [US5] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/ `ReceiptVoiding`, `ModuleReversal`, `PayerCreditBalance`, `PayerCreditBalanceMovement`, `PayerReceivable`, `ReceiptsInboundMessage` y el enum `ModuleReversalCause`; configurarlas en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts/ (data-model §4.1–§4.3, §6.1)
- [X] T0175 [US5] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Voidings/ `RequestReceiptVoidingCommand`, `ApproveReceiptVoidingCommand` (con monto, aprobador presente en la sesión abierta; reversa por `ReversarAsync` obligaciones primero, cuentas después, una vez por porción), `RejectReceiptVoidingCommand`, con `CerrojoDeRecaudo` y `NumeradorDeRecaudo` (FR-057, FR-058, FR-064, FR-194)
- [X] T0176 [US5] Implementar el consumidor único src/Core/IngenIA365ERP.Application/Receipts/Integration/DestinoRecaudo.cs (destino `Receipts`) con `ConsumeLendingPaymentReversalCommand` para `PagoReversadoPorCartera` v1 y `ConsignacionReversadaPorCartera` v1, recibo único en `RCD_InboundMessages`, y `ConfirmModuleReversalCauseCommand` (FR-059, FR-063, FR-151, FR-195); registrarlo bajo la regla transitoria de T0141; llenar `NoHayDoblesDePrueba` con `DestinoRecaudo` **ruta crítica**
- [X] T0177 [US5] Implementar en src/Core/IngenIA365ERP.Application/Receipts/CreditBalances/ `RefundPayerCreditBalanceByOrderCommand`, `ApplyPayerCreditBalanceCommand` y `RefundLendingCreditBalanceByOrderCommand`, y en src/Core/IngenIA365ERP.Application/Treasury/Orders/ la implementación de `IOrdenesDePagoDeTesoreria` (origen `Receipts`, clases `PayerCreditBalanceRefund`, `LendingCreditBalanceRefund`, `UnidentifiedItemRefund`) y del lado de Recaudo la reacción a `IResultadoDeOrdenesDeRecaudo` (R-05; FR-053, FR-054)
- [X] T0178 [US5] Emitir `ReciboAnulado`, `AplicacionReversadaPorModulo`, `SaldoAFavorDePagadorResuelto` y `SaldoAFavorDeCarteraDevuelto` en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Reversos.cs, enlazados al hecho original (`OriginalFactRefV1`) y en el período abierto; ampliar `ElCatalogoDeMensajesEsUnico` (contabilidad.md §3; FR-064, FR-141)
- [X] T0179 [US5] Crear el documento `Anulacion` en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeRecaudoReport.cs (número, código de verificación, recibo original, motivo, aprobador, reversos, aplicaciones omitidas) (FR-156)
- [X] T0180 [US5] Exponer en src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/{VoidingsEndpoints,ReversalsEndpoints,CreditBalancesEndpoints}.cs las rutas de api.md §4 (`/receipts/{id}/voiding`, `/voidings/{id}/approve|reject`, `/module-reversals`, `/payer-credit-balances`, `/lending-credit-balances/refund-by-order`) con sus permisos
- [X] T0181 [US5] Crear src/Presentation/IngenIA365ERP.Shared/Services/Recaudo/AnulacionesClient.cs y las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/{Anulaciones,ReversosDeModulos,SaldosAFavor}.razor (`/recaudo/anulaciones`, `/recaudo/reversos-de-modulos`, `/recaudo/saldos-a-favor`) con aprobación presente, confirmación de la causa, devolución por orden de pago e `IndicadorDeCarga` por zona
- [X] T0182 [US5] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de anulación, reversos, saldos a favor y mensajes recibidos (data-model §4.1–§4.3, §6.1)
- [X] T0183 [US9] E-01 (e) y E-02 (g), primera parte: `RequisitosDeActivacion` (src/Core/IngenIA365ERP.Application/Lending/Activation/RequisitosDeActivacion.cs) y `RequisitosDeActivacionDeCaptaciones` (…/Lending/Deposits/Activation/) cuentan en **todo** ambiente al recaudador `Receipts` real con `ProveedorDeObligaciones`/`ProveedorDeCuentas` adoptado y al destino `Treasury` real con `OrdenDeGiroEmitida` adoptada; fuera de producción siguen admitiendo los dobles hasta la fase 19, que retira esa rama (contratos-v2.md §2.2, §3.7; CH3) **ruta crítica**
- [X] T0183a [P] [US9] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Activation/ActivacionMinimaTests.cs (R-40; TR42; FR-147, FR-150): con los requisitos básicos (Recaudo: un punto, una caja con cajero, medios efectivo y transferencia, tipos de documento; Tesorería: una cuenta bancaria vinculada y un formato o la forma «portal del banco») `ActivateReceiptsCommand` y `ActivateTreasuryCommand` pasan `RCD_Setup`/`TES_Setup` a `Active`, guardan `RCD_Activations`/`TES_Activations` con `IsMinimal = true` y sin cuadre, y registran en el mismo guardado `REC × ObligationsCollector`, `REC × MessageDestination × Receipts` y `TES × MessageDestination × Treasury` con las adopciones de mensajes.md §7 (sin fila `ObligationsCollector` de Tesorería); sin un requisito → `Receipts.Activation.RequirementsMissing` / `Treasury.Activation.RequirementsMissing` con `data.missing[]`; con la fila del doble `TES` vigente, la cierra antes por `EscritorDelRegistroDeIntegracion.RetirarAsync` (`ValidTo`, motivo en la auditoría) y registra la real; repetir con la misma clave no duplica
- [X] T0183b [US9] Implementar la activación mínima (R-40; TR42; hallazgo I2): crear `RCD_Activations` (src/Core/IngenIA365ERP.Domain/Entities/Receipts/Activation/ReceiptsActivation.cs) y `TES_Activations` (src/Core/IngenIA365ERP.Domain/Entities/Treasury/Activation/TreasuryActivation.cs) con `OpeningSquareId?` e `IsMinimal` y su configuración (data-model §15.1; adelanta lo que T0302 creaba), `RequisitosDeActivacionDeRecaudo` (src/Core/IngenIA365ERP.Application/Receipts/Activation/) y `RequisitosDeActivacionDeTesoreria` (src/Core/IngenIA365ERP.Application/Treasury/Activation/) con los requisitos básicos, `ActivateReceiptsCommand` y `ActivateTreasuryCommand` que registran por `EscritorDelRegistroDeIntegracion` en el mismo guardado, y las rutas `GET /api/receipts/activation/requirements`, `POST /api/receipts/activation` y sus pares de `/api/treasury/activation` en src/Presentation/IngenIA365ERP.API/Endpoints/{Receipts,Treasury}/ActivationEndpoints.cs con `…Activation.View/Activate` (api.md §14); regenerar los pares `RecaudoNucleo` y `TesoreriaNucleo` con las dos tablas (antes del congelamiento del 10/11); la fase 19 completa el mismo comando (FR-147, FR-150) **ruta crítica**
- [X] T0183c [US3] **e2e de humo de la caja** (excepción declarada a «e2e una sola vez», riesgo R1 del analyze): escribir tests/IngenIA365ERP.API.IntegrationTests/Receipts/CajaDeHumoTests.cs **(nuevo, DT §2.7)** en la colección «Recaudo y Tesorería e2e», con el host de esa colección en `Lending:TestDoubles:Enabled = false` (la fixture compartida de Lending pasa a `false` en T0300a), y **correrla ahora** con Docker en `DB_PROVIDER=PostgreSql` y en `DB_PROVIDER=SqlServer` (filtro de quickstart.md §1): cooperativa aislada, configuración mínima, activación mínima de T0183b, Cartera y Captaciones activadas con los participantes reales, abrir una sesión, un recibo de una cuota de crédito y una de aportes en efectivo y transferencia, la misma clave repetida devuelve el mismo recibo, cerrar la sesión; anotar el resultado en specs/015-recaudo-tesoreria/quickstart.md; lo que falle se corrige en su fase antes de T0184 (FR-042, FR-170; SC-006, SC-007)
- [ ] T0184 Hito del bloque 2 (04/11, ambiente): en DEV, con `Lending:TestDoubles:Enabled = false` en el manifiesto de GitOps de DEV (la bandera transitoria de T0141) y Recaudo y Tesorería activados con la activación mínima de T0183b, que registra `REC` (recaudador y destino) y `TES` (destino) sin nada a mano en `/admin/participantes-de-integracion` (la activación completa llega en la fase 19), y con la e2e de humo de T0183c en verde, un recibo de caja multiproducto y un giro por transferencia de punta a punta (recorridos A y G de quickstart.md sin contabilidad); anotar el resultado en specs/015-recaudo-tesoreria/quickstart.md


**Notas de la construcción de la fase 10 (2026-10-24)**. Pruebas escritas y compiladas, sin correr (también la e2e de humo T0183c: el dueño pidió no ejecutar
pruebas). Desviaciones y decisiones; las nuevas quedan en decisiones-transversales §2.7:

- **T0170 a T0172**: escritas sobre `CajaConContratos` (recibo confirmado con contratos simulados) y el motor de aprobaciones simulado; sin correr.
  `TestDbContextFactory` suma la configuración de prueba (sin `RowVersion`, filtro de borrado) de las entidades de Tesorería de las fases 7 y 8, que faltaba,
  y de las nuevas.
- **T0173**: e2e escrita sobre `CajaE2E` y Cartera real (reverso desde la pantalla del crédito, despacho, confirmación de la causa y devolución por orden); se
  corre en la fase 22.
- **T0174**: `ReceiptVoiding.Number` es de **escritura única y nulo** hasta aplicar (lo que queda en aprobación no consume número); la anulación suma
  `DecidedAt`, `DecisionReason` y `VerificationCode`; `ModuleReversal` suma `MessageType`, `Reason`, `ConfirmedAt` y `ConfirmationNote`; `PayerCreditBalance` suma
  `OriginDocumentNumber` y `BranchId` (alcance por oficina); `PayerCreditBalanceMovement` suma `DocumentNumber`, `RecordedBy…` y `Note` (todos nuevos). Enums nuevos
  `ReceiptVoidingState`, `PayerCreditBalanceKind`, `PayerCreditBalanceOrigin` (con `LendingRefund = 7`), `PayerCreditBalanceState`,
  `PayerCreditBalanceMovementKind` (con `RefundRejected = 5`) y `PayerReceivableState`. `PayerReceivable` (§4.5) nace aquí sin comandos: la usa la devolución de
  cheques (fase 13).
- **T0175**: la aprobación es la regla fija `ReceiptVoiding` (un nivel, `Receipts.Voiding.Approve` con monto) por `IMotorDeAprobaciones`; «otra persona presente»
  en la sesión abierta y «permiso propio» después son el mismo comando de aprobar, que rechaza a la misma persona que pidió (`Receipts.Voiding.SelfApproval`) y
  aplica en la misma transacción. El dinero: con la sesión abierta, el efectivo y el cheque bajan lo esperado (la proyección excluye los recibos anulados); con la
  sesión cerrada **o** un medio electrónico (transferencia, consignación, tarjeta) nace un saldo a favor del pagador (`VoidAfterSessionClose`) por lo reversado, que
  se devuelve por orden de pago. Además: `ListReceiptVoidingsQuery` y `GET /api/receipts/voidings`.
- **T0176**: `DestinoRecaudo` acepta en R0 sólo `PagoReversadoPorCartera` v1 y `ConsignacionReversadaPorCartera` v1; `DescuentosPorPagaduria` (v1 y v2) lo suma la
  fase 12 (y la activación lo adopta entonces). La porción que Recaudo ya reversó al anular responde sin efecto; un aviso de un recaudador de clase `Payroll`
  responde `Receipts.Voiding.PayrollOwned`, de otro, `.NotOurs`. La causa se confirma con `MoneyReceived` (saldo a favor, origen `ModuleReversal`) o
  `MoneyNotReceived` (el medio del recibo queda `WrittenOff`); el hecho `AplicacionReversadaPorModulo` sale al confirmar, no al recibir el aviso. Registrado en
  `ConsumidoresDeRecaudoYTesoreria` y en `NoHayDoblesDePrueba`.
- **T0177**: `IOrdenesDePagoDeTesoreria` (`Treasury/Orders/OrdenesDePagoDeTesoreria`) crea la orden de origen `Receipts` en la bandeja, validada como las de giro,
  idempotente por la clave de la solicitud (`OriginOrderPublicId`) y sin guardar; `IResultadoDeOrdenesDeRecaudo` (`Receipts/CreditBalances/ResultadoDeOrdenesDeRecaudo`)
  cierra el saldo a la «girada» y lo reabre a la «rechazada», «cheque no cobrado» o «anulada» (movimiento `RefundRejected`). Los dos validan y auditan
  (`LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras` los nombra). La devolución del saldo de Cartera deja en Recaudo un saldo `LendingRefund` que nace
  devuelto por la orden: si Tesorería la rechaza, queda abierto a favor del beneficiario (Cartera ya lo resolvió). La clave hacia Cartera es `REC:{orden:N}` con el
  sufijo de destino. **Pendiente**: el hecho que devuelve `PuenteDeDevolucion` a `SaldoAFavorDePagadores` cuando la orden se rechaza (lo fija la matriz, fase 16).
  La alerta `Recaudo.SaldoAFavorAntiguo` la da la tarea `recaudo.antiguedad` (fase posterior); la pantalla marca los de más de 30 días.
- **T0178**: códigos cortos `RAN`, `ARM`, `SFP` y `SFC`; tipos de documento del origen `RMOD` (reverso del módulo) y `SAF` (saldo a favor), que el emisor exige en un
  origen `Document`. `RolesDeRecaudo` suma `SaldoAFavorDePagadores` y `PuenteDeDevolucion`. `ElCatalogoDeMensajesEsUnico`: 13 tipos `REC`, 10 hechos.
- **T0179**: `AnulacionModel` y `GetVoidingDocumentQuery`; `IDocumentosDeRecaudoEnPdf.Anulacion`; ruta `GET /api/receipts/voidings/{id}/document`.
- **T0180**: los archivos `VoidingsEndpoints`, `ReversalsEndpoints` y `CreditBalancesEndpoints` (y `ActivationEndpoints`) como pide la tarea.
- **T0181**: pantallas `/recaudo/anulaciones`, `/recaudo/reversos-de-modulos` y `/recaudo/saldos-a-favor` en el grupo Recaudo del menú, con `AnulacionesClient`.
  Sin probar en navegador.
- **T0182, T0183b (migraciones)**: regenerados en par `RecaudoNucleo` (+7 tablas: `RCD_ReceiptVoidings`, `RCD_ModuleReversals`, `RCD_PayerCreditBalances`,
  `RCD_PayerCreditBalanceMovements`, `RCD_PayerReceivables`, `RCD_InboundMessages`, `RCD_Activations`; semillas conservadas), `TesoreriaNucleo` (+`TES_Activations`)
  e `IntegracionContableDeRecaudoYTesoreria`, en ese orden. Paridad OK (66 en par); `has-pending-model-changes` da «No changes» en los dos motores.
- **T0183**: `RequisitosDeActivacion` (Cartera) ya contaba en todo ambiente al recaudador de clase `Receipts` con `ProveedorDeObligaciones` y al destino `Treasury` no
  doble con `OrdenDeGiroEmitida`, y fuera de producción también los dobles; `RequisitosDeActivacionDeCaptaciones` cuenta cualquier recaudador real con
  `ProveedorDeCuentas` y el destino `Treasury` con la v2. Las filas que registra la activación mínima los cumplen: **sin cambio de código**.
- **T0183a, T0183b**: `RequisitosDeActivacionDeRecaudo` (punto, caja con cajero, efectivo y transferencia ofrecidos, tipos de documento del recibo y de la anulación)
  y `RequisitosDeActivacionDeTesoreria` (una cuenta de pagos vigente y vinculada; el formato es informativo porque la forma «portal del banco» siempre está). En
  producción ambos suman el requisito «cuadre» sin cumplir: no hay activación mínima. La activación de Recaudo registra `REC × ObligationsCollector`
  (obligaciones y cuentas v1) y `REC × Receipts` (los dos reversos v1); la de Tesorería cierra la fila vigente del doble `TES` por `RetirarAsync` (auditado) y registra
  `TES × Treasury` (`OrdenDeGiroEmitida` v1 y v2, `AnulacionDeOrdenDeGiro` v1; `ArchivoDeDesembolsos…` cuando exista, E-01 c). Sellan `Recaudo.HoraDeCorte` y
  `Lotes.TerceraPersonaParaResultado`. Los comandos no son `IEscrituraDe…` (el módulo todavía no está activo).
- **T0183c**: `CajaDeHumoTests` escrita (dos cuotas de crédito en efectivo y transferencia; la de aportes cuando el escenario se componga con Captaciones en la
  fase 22) y anotada en quickstart.md; `RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, coop)` ya activa por las rutas (antes lanzaba). **Sin correr.**
- **T0184**: del ambiente; no se ejecuta.

---

# Entrega R0 + T0 · Bloque 3 — Bancos, pagadurías, cheques y contabilidad (05/11–10/11)

Hito del bloque (10/11): **versión al paralelo en QA** (caja, recibo, extracto, pagaduría, giro), activada con la
activación mínima; ese día se **congelan** los pares de R0/T0 (T0271).

---

## Phase 11: User Story 6 - Consignaciones y transferencias recibidas: identificar, aplicar y partidas por identificar (Priority: P1) — entrega R0 (parcial)

**Goal**: el extracto cargado **una vez**, en Tesorería, con la huella por formato (o la plantilla genérica o a mano), y
sobre sus abonos la identificación manual de Recaudo: recibo de canal banco aplicado en forma no interactiva con la fecha
valor del movimiento y la modalidad del canal, línea por línea; la partida por identificar con su antigüedad, su
identificación posterior, su clasificación a concepto con aprobación y su devolución por orden de pago; el giro devuelto
(FR-065 a FR-068, FR-070 a FR-074, FR-171, FR-196 a FR-199).

**Independent Test**: cargar un extracto con cinco abonos y volver a cargarlo («5 omitidos por huella»); identificar cuatro a
mano (uno dividido entre dos créditos con suma exacta) y dejar el quinto como partida; una línea que el contrato rechaza no
tumba las demás y se reprocesa con la misma clave; a los diez días la partida se identifica con la fecha más antigua que
admite el contrato y el recibo lo dice.

### Tests for User Story 6

- [X] T0185 [P] [US6] Escribir los casos dorados de la huella (≈6) en tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/huella-*.json con tests/IngenIA365ERP.Domain.Tests/Receipts/HuellaDeMovimientoBancarioTests.cs (FR-197): con secuencia del banco; sin ella por banco, cuenta, fecha, valor y referencia; dos movimientos iguales → posibles duplicados; huella del archivo entero
- [X] T0186 [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Statements/CargaDeExtractoTests.cs (R-06; FR-065, FR-066, FR-196, FR-197): un solo `LoadBankStatementCommand` desde las dos pantallas con `Treasury.Statements.Load`; formato del banco (ámbito 7), `EXTRACTO-GENERICO` o captura manual con revisión previa y error por línea; líneas ilegibles informadas y no cargadas (`Treasury.Statement.IllegibleLines`); archivo ya cargado por su huella (`.AlreadyLoaded`); líneas omitidas por huella contadas; posibles duplicados con decisión de una persona
- [X] T0187 [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/BankItems/IdentificacionDeAbonosTests.cs (R-17; FR-043, FR-068, FR-071, FR-074, FR-171, FR-198): recibo de canal banco con la fecha valor del movimiento y la modalidad del canal (créditos cuota del ciclo; cuentas cuotas o libre); división entre personas y productos con suma exacta (`Receipts.BankItem.SplitMismatch`, `.OverIdentified`, `.AlreadyIdentified`); cada línea en su `TransaccionExplicita` con su clave y reproceso sin duplicar; excedente como saldo a favor del pagador; transferencia a una cuenta de captación por `ConsignarAsync` como `REC`; en R0 la identificación tardía usa la fecha más antigua que admite el contrato, el recibo lo dice y alerta `Recaudo.FechaValorTardia` al módulo
- [X] T0188 [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/BankItems/PartidasPorIdentificarTests.cs (FR-070, FR-072, FR-073, FR-199; D10): partida sin persona con su hecho de reconocimiento; identificación entera o en partes; clasificación a concepto con aprobación (`Receipts.UnidentifiedItem.ClassificationNeedsApproval`); devolución al consignante por orden de pago; escalones 30, 60 y 90 con alerta `Recaudo.PartidaAntigua` y señal al oficial por consignante repetido; llevarla a ingreso es una decisión registrada con aprobación, nunca automática; giro devuelto como partida identificada al concepto «giro devuelto», enlazada a la orden, con alerta `Tesoreria.GiroDevuelto` al origen y al tesorero, sin cambiar el resultado
- [X] T0189 [P] [US6] Escribir las e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ExtractoIdentificacionYPartidasTests.cs (US6 «Independent Test», quickstart recorrido D; SC-009) y tests/IngenIA365ERP.API.IntegrationTests/Receipts/ProcesoNoInteractivoConLineasRechazadasTests.cs (SC-030); se corren en la fase 22
- [X] T0189a [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/BankItems/AuditoriaDeCorridasTests.cs (Principio X; FR-014; TR37; decisión del dueño del 2026-10-23, CO1): una corrida de 50 líneas con 3 rechazadas deja, por `COR_AuditOutbox` en la transacción de cada tanda, el evento `Receipts.ProcessingRun.Summary` **(nuevo, DT §2.7)** cuya lista `receipts[] { publicId, sha256 }` cubre **exactamente** los 47 recibos aplicados (ninguno falta ni sobra; el reproceso de una rechazada aparece en la tanda del reproceso), cada `sha256` coincide con `HuellaDeRecibo` recalculada desde el recibo y, alterado un recibo, su verificación falla; un evento por línea rechazada; lo mismo en el giro de pagaduría (`ApplyPaymasterRemittanceCommand`, T0208)

### Implementation for User Story 6

- [X] T0190 [P] [US6] Implementar el motor puro src/Core/IngenIA365ERP.Domain/Receipts/Huellas/HuellaDeMovimientoBancario.cs hasta T0185 (lo usa Tesorería por su puerto)
- [X] T0191 [P] [US6] Crear `BankStatement` y `BankStatementLine` (H, `AuditableEntityLong`) en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Statements/ con su configuración (UK `(BankAccountId, Fingerprint)` cuando el formato trae identificador, `PossibleDuplicateOfLineId`, `DuplicateDecision`, `RecaudoStatus` y `ReconciliationStatus` mutables, UK filtrado por `FileSha256`) (data-model §12.1)
- [X] T0192 [P] [US6] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Banks/ `BankItemIdentification`, `UnidentifiedItem`, `UnidentifiedItemResolution`, `ProcessingRun`, `ProcessingRunLine` y el enum `UnidentifiedItemState`, con su configuración (data-model §5)
- [X] T0193 [US6] Implementar `LoadBankStatementCommand` y `GetBankStatementQuery` en src/Core/IngenIA365ERP.Application/Treasury/Statements/ (`FlatFileReader` en `BankStatement = 7`, plantilla genérica o captura, huella, revisión previa, adjunto `TreasuryStatement`) y la implementación de `IMovimientosBancariosParaRecaudo` (abonos por identificar con alcance; `MarcarIdentificacionAsync` actualiza `RecaudoStatus`) (R-06; FR-065, FR-066, FR-196, FR-197)
- [X] T0194 [US6] Implementar src/Core/IngenIA365ERP.Application/Receipts/BankItems/AplicacionNoInteractiva.cs (una `TransaccionExplicita` por línea con su clave, `AplicarAsync`/`ConsignarAsync` `NonInteractive`, modalidad por `RCD_ChannelModalityRules`, la línea rechazada a partida, saldo a favor o no aplicada con su motivo, `RCD_ProcessingRuns` con el evento resumen `Receipts.ProcessingRun.Summary` por tanda —la lista `receipts[] { publicId, sha256 }` de cada recibo aplicado con su huella `HuellaDeRecibo`, por `COR_AuditOutbox` en la misma transacción— y uno por línea rechazada, a nombre de quien confirma por tandas con avance, hasta que pase T0189a) (R-17, R-32; FR-014, FR-171, FR-174; TR37) **ruta crítica**
- [X] T0195 [US6] Implementar en src/Core/IngenIA365ERP.Application/Receipts/BankItems/ `IdentifyBankItemCommand` (con división), `MarkBankItemUnidentifiedCommand`, `ClassifyBankItemCommand` (aprobación), `DecideBankItemDuplicateCommand`, `RetryProcessingRunLineCommand`, y en …/Receipts/BankItems/UnidentifiedItems/ `IdentifyUnidentifiedItemCommand`, `ClassifyUnidentifiedItemCommand`, `RefundUnidentifiedItemByOrderCommand` (por `IOrdenesDePagoDeTesoreria`), `MoveUnidentifiedItemToIncomeCommand` (decisión registrada con aprobación según `Partidas.DestinoVencido`) (FR-067 manual, FR-068, FR-070 a FR-073, FR-199)
- [X] T0196 [US6] Emitir `ReciboDeBancoEmitido`, `PartidaPorIdentificarReconocida` y `PartidaClasificada` en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Bancos.cs, en línea; ampliar `ElCatalogoDeMensajesEsUnico` (contabilidad.md §3)
- [X] T0197 [US6] Implementar la tarea `recaudo.antiguedad` en src/Core/IngenIA365ERP.Application/Receipts/Tareas/AntiguedadDeRecaudo.cs: escalones de partidas, saldos a favor antiguos, cuentas de recaudo sin extracto en `Extractos.DiasHabilesSinCarga` (`Recaudo.ExtractoSinCargar`), partidas abiertas antes del cierre mensual de Cartera (`Recaudo.PartidasAbiertasAlCierre`), y las secciones que completan otras fases (contingencia, cuadre provisional, política en «advertir»); sólo alerta (FR-073, FR-196; DT §2.5)
- [X] T0198 [US6] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/{BankItemsEndpoints,UnidentifiedItemsEndpoints,ProcessingRunsEndpoints}.cs (api.md §5) y src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/StatementsEndpoints.cs (`POST /api/treasury/statements` multipart o captura, `GET /{id}`, `GET /{id}/lines`) con sus permisos
- [X] T0199 [US6] Crear src/Presentation/IngenIA365ERP.Shared/Services/Recaudo/BancosClient.cs, src/Presentation/IngenIA365ERP.Shared/Services/Tesoreria/ExtractosClient.cs y las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/{Extractos,Partidas}.razor (`/recaudo/extractos` diaria con carga, identificación y división; `/recaudo/partidas` por antigüedad) y src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Extractos.razor (`/tesoreria/extractos`), con avance por tandas, revisión previa y error por línea e `IndicadorDeCarga` por zona
- [X] T0200 [US6] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ los pares `RecaudoNucleo` (identificaciones, partidas, corridas) y `TesoreriaNucleo` (extractos) (data-model §5, §12.1)


**Notas de la construcción de la fase 11 (2026-10-24)**. Pruebas escritas y compiladas, sin correr («prueba escrita, sin correr»; estrategia del dueño). Desviaciones y
decisiones; las nuevas quedan en decisiones-transversales §2.7:

- **T0185, T0190**: 6 casos dorados (`huella-01..06.json`; las huellas ya cargadas van como movimientos y la prueba las calcula). `HuellaDeMovimientoBancario` es puro:
  `Calcular` (con secuencia del banco `SEQ|banco|cuenta|secuencia`; sin ella `MOV|banco|cuenta|fecha|valor|referencia` normalizada, en SHA-256), `DelArchivo` y
  `Clasificar` (`Nueva`, `OmitidaPorHuella`, `PosibleDuplicado` con la línea del mismo archivo que duplica). Dos líneas con la misma secuencia en el mismo archivo: la segunda
  se omite.
- **T0186, T0193**: `LoadBankStatementCommand` acepta el archivo (multipart, `POST /api/treasury/statements`) o la captura (`POST /api/treasury/statements/captured`, nuevo);
  `Preview` revisa sin guardar; con líneas ilegibles exige `AcceptIllegible` (`Treasury.Statement.IllegibleLines`). Formato: el pedido, el vigente del banco en el ámbito 7 o
  `EXTRACTO-GENERICO`. Numera con la clase `EX` (nuevo). `BankStatement` es hecho (H); `BankStatementLine` es H con cuatro columnas mutables (`DuplicateDecision`,
  `RecaudoStatus`, `RecaudoIdentifiedAmount` —nueva— y `ReconciliationStatus`). La UK `(BankAccountId, Fingerprint)` se filtra a `HasBankIdentifier = 1` (columna nueva) y
  hay otro índice no único sobre las mismas columnas. Además de api.md §12: `GET /api/treasury/statements` (lista). El puerto `IMovimientosBancariosParaRecaudo` suma
  `LineaAsync`, `DecidirDuplicadoAsync` y `UltimasCargasAsync` (nuevos); `LineaDeExtractoDto` suma cuenta, oficina, banco, estado de Recaudo, decisión de duplicado y número del
  extracto; `FiltroDeAbonos` suma `SoloPendientes` y `OficinaPublicIds`: el puerto **no** aplica el alcance de Tesorería a una persona de Recaudo (filtra por sus oficinas).
- **T0194 (desviación)**: la «una `TransaccionExplicita` por línea» se cumple como **una unidad lógica por línea con su clave**: la línea se repite con su clave y no
  duplica (el recibo toma un `OperationKey` derivado de la clave, `AplicacionNoInteractiva.ClaveDelRecibo`). Dentro de una llamada de la pantalla (una tanda) las líneas
  comparten la transacción de `IdempotencyBehavior`: un rechazo del contrato es una respuesta por ítem y **no revierte** las demás; una excepción revierte la tanda y sus
  líneas quedan pendientes para reprocesar. Los créditos se **simulan antes** de numerar y aplicar: una simulación rechazada rechaza la línea sin escribir; el excedente de la
  modalidad del canal queda como saldo a favor del pagador (`NonInteractiveSurplus`); una parte rechazada después de simular, también (`RejectedLine`). Si el contrato
  rechaza todo, el número vuelve (`NumeradorDeRecaudo.Devolver`, nuevo) y no queda hueco. FR-071 en R0: con `Lending.Payment.ValueDateOutOfRange`, `.ValueDateBeforeClosing`
  o `Lending.Savings.ValueDateOutOfRange` se repite con la `minDate` que devuelve el contrato (u hoy), el recibo guarda la fecha del movimiento en `OriginalValueDate` (columna
  nueva de `RCD_Receipts`) y se alerta `Recaudo.FechaValorTardia`. Una tanda de varias líneas toma sus cerrojos antes de la primera (`LineaNoInteractiva.CerrojosTomados`,
  `OrigenABloquear`): `OrdenDeCerrojos` no admite volver a los pagadores después de numerar. El medio del recibo de banco es el primer `COR_PaymentMeans` activo de clase
  transferencia (o consignación); sin él, `Receipts.BankItem.NoTransferMeans` (nuevo). El recibo de canal banco numera con el tipo `RB` y prefijo su código.
- **T0189a, T0194**: el evento `Receipts.ProcessingRun.Summary` por tanda lleva `receipts[] { publicId, sha256 }` con `HuellaDeRecibo.DelReciboAsync` (nueva: la huella
  recalculada desde lo guardado —número, canal, pagador, fecha, totales, aplicaciones y porciones—); uno `Receipts.ProcessingRun.LineRejected` (nuevo) por línea rechazada.
  En InMemory el evento va al servicio de auditoría (sin cooperativa no hay flujo encadenado): la prueba lee esas llamadas; el outbox lo recorre la e2e.
- **T0195**: comandos además de la tarea: `ListBankItemsQuery`, `ListBankItemIdentificationsQuery` (`GET /bank-items/{lineId}/identifications`, nuevo),
  `GetProcessingRunQuery`, `ListUnidentifiedItemsQuery`. La clasificación (de un abono o de una partida) y llevar a ingreso piden **aprobador presente** (428
  `Approvals.PresenceRequired` y `X-Approval-Request`), con los sujetos nuevos `BankItemClassification` (`Receipts.BankItems.Classify`) y `UnidentifiedItemToIncome`
  (`Receipts.UnidentifiedItems.MoveToIncome`); el código `Receipts.UnidentifiedItem.ClassificationNeedsApproval` queda declarado pero el pedido sin aprobar responde el 428 de
  siempre. Llevar a ingreso exige que `Partidas.DestinoVencido` no sea `SiguenComoPasivo` (el defecto: `Receipts.UnidentifiedItem.DestinationNotIncome`) y que la partida pase
  el último escalón (`.NotDue`). La identificación de una partida sí devuelve el rechazo del contrato como error (es interactiva). La devolución al consignante por orden de pago
  usa la clase `UnidentifiedItemRefund`; si Tesorería informa la orden rechazada, con cheque no cobrado o anulada, `ResultadoDeOrdenesDeRecaudo` devuelve el saldo a la partida
  con la resolución nueva `RefundRejected = 5` y `TomarCerrojosAsync` bloquea también la partida. El reproceso de otras clases de corrida se enchufa por `IReprocesoDeCorrida`
  (nuevo; lo usa la fase 12). Un abono «posible duplicado» no se identifica hasta decidirlo (`Receipts.BankItem.DuplicatePending`, nuevo).
- **T0196**: hechos `ReciboDeBancoEmitido` (`RBE`), `PartidaPorIdentificarReconocida` (`PIR`), `PartidaClasificada` (`PCL`) y `PartidaDevueltaAlConsignante` (`PDC`, nuevo:
  `PartidasPorIdentificar` → `PuenteDeDevolucion`), en línea; roles nuevos `PartidasPorIdentificar`, `SaldoDePagadurias`, `CuentaPorCobrarPagador`, `CustodiaPorLista`;
  tipos de documento del origen `PI` y `CLA` (nuevos). `ElCatalogoDeMensajesEsUnico` cuenta 17 tipos `REC` y 14 hechos.
- **T0197**: `AntiguedadDeRecaudo` y la tarea `recaudo.antiguedad` (cada 24 h): escalones (el último alertado queda en `AgingStep`, la única marca que escribe), el
  consignante repetido (dos o más partidas abiertas) al oficial (`RecipientPermissions = Receipts.Compliance.View`, con el tipo `Recaudo.PartidaAntigua`), los saldos a
  favor de más de 30 días (`Recaudo.SaldoAFavorAntiguo`, pendiente desde la fase 10), las cuentas de recaudo sin extracto en `Extractos.DiasHabilesSinCarga` días hábiles y
  las partidas abiertas del mes en los dos últimos días hábiles. `NingunProcesoAplicaPagosNiInformaResultados` la lista.
- **T0198**: tres archivos (`BankItemsEndpoints`, `UnidentifiedItemsEndpoints`, `ProcessingRunsEndpoints`) y `StatementsEndpoints`; `GET /api/receipts/payables` admite también
  `Receipts.BankItems.Identify`, `Receipts.UnidentifiedItems.Resolve` y `Receipts.Paymasters.ApplyRemittance` (el analista elige los productos de la persona).
- **T0199**: pantallas `/recaudo/extractos` (carga con revisión, abonos, identificación con división, partida, clasificación con aprobación presente, duplicados y reproceso),
  `/recaudo/partidas` y `/tesoreria/extractos` (archivo o captura a mano), en los menús de Recaudo («Bancos y pagadurías») y Tesorería; clientes `BancosClient` (con
  `EnvioDeRecaudo`, el envío con `X-Approval-Request` y el multipart) y `ExtractosClient`, registrados en Web, Web.Client y MAUI. Sin probar en navegador.
- **T0200**: regenerados en par `RecaudoNucleo` (+5 tablas y `RCD_Receipts.OriginalValueDate`), `TesoreriaNucleo` (+2 tablas) e `IntegracionContableDeRecaudoYTesoreria`, en ese
  orden, con las semillas a mano conservadas. Paridad OK (66 en par); `has-pending-model-changes` da «No changes» en SQL Server y en PostgreSQL.

---

## Phase 12: User Story 7 - Pagadurías y libranza (Priority: P1) — entrega R0

**Goal**: Recaudo recibe de Cartera cada archivo de descuentos por pagaduría (v1 y v2), confirma su envío, carga la
respuesta de la pagaduría (formato de vuelta del convenio, plantilla genérica o a mano), devuelve el resultado
(`ResultadoDeDescuentos` v1 o v2), registra el giro y aplica cada línea descontada por el contrato que corresponda en su
propia unidad; lo no aplicado queda como saldo de la pagaduría (FR-075 a FR-081, FR-171, FR-201).

**Independent Test**: con Cartera real, recibir un archivo de 50 líneas (créditos y aportes, v2), confirmar el envío (repetir
no cambia nada), cargar la respuesta con 45 descontadas, 5 no y una fila sin equivalencia, devolver el resultado y aplicar un
giro menor que lo descontado: cada línea es su unidad, las que no cubre siguen pendientes, una cuenta bloqueada queda como
saldo de la pagaduría.

### Tests for User Story 7

- [X] T0201 [P] [US7] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Paymasters/ArchivoDeDescuentosRecibidoTests.cs (R-16; FR-075; mensajes.md §3.2): consumo de `DescuentosPorPagaduria` v1 y v2 con recibo único; líneas estructuradas por crédito y por cuota de cuenta; una versión que reemplaza a otra no enviada la deja `Superseded`; si ya se envió, `Rejected` con `Receipts.Paymaster.SupersedesSentFile` y alerta; vista por pagaduría, convenio y período con totales y descarga auditada
- [X] T0202 [P] [US7] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Paymasters/EnvioYRespuestaDePagaduriaTests.cs (FR-076, FR-077, FR-080, FR-201): `ConfirmPayrollFileSent` emite `EnvioDeDescuentosConfirmado` v1 con canal, fecha, radicado y huella (repetir sin efecto; `HashMismatch` vuelve como evaluación negativa); la respuesta se lee del ámbito 6, de `RESPUESTA-PAGADURIA-GENERICA` o a mano, con vista previa por línea o por concepto; `ResultadoDeDescuentos` v1 o v2 según el archivo; `Receipts.Paymaster.VersionNotAdopted`; `RejectedByLending` (`Lending.PayrollFile.LineMismatch`) se corrige con otra versión; fila sin equivalencia → «no aplicado»
- [X] T0203 [P] [US7] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Paymasters/GiroDePagaduriaTests.cs (R-16, R-17; FR-078, FR-079, FR-080, FR-081, FR-171): giro desde una línea de extracto identificada o un recibo de caja; cada línea en su transacción con `ExternalPaymasterDeduction`, `DeductionFileLinePublicId`, la fecha valor del descuento y la clave `REC:{giro:N}:{línea:N}`; giro menor en el orden de prelación del convenio o del analista; mayor → saldo a favor de la pagaduría; rechazo del contrato (`Lending.Payment.PaymasterReferenceInvalid`, cuenta bloqueada) → saldo de la pagaduría con alerta `Recaudo.NoAplicadoDePagaduria`; resolverlo con soporte; los descuentos de nómina propia no los aplica Recaudo
- [ ] T0204 [P] [US7] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/PagaduriaDePuntaAPuntaTests.cs (US7 «Independent Test», quickstart recorrido F) sobre Cartera y Captaciones reales; se corre en la fase 22

### Implementation for User Story 7

- [X] T0205 [P] [US7] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Paymasters/ `PayrollDeductionFile`, `PayrollDeductionFileLine` (H con `[EscrituraUnica]` del resultado), `PayrollFileSending`, `PaymasterResponse`, `PaymasterResponseLine`, `PaymasterRemittance`, `PaymasterRemittanceLine` con su configuración (data-model §6.2–§6.5)
- [X] T0206 [US7] Agregar a `DestinoRecaudo` el consumo `ConsumePayrollDeductionFileCommand` (v1 y v2) en src/Core/IngenIA365ERP.Application/Receipts/Paymasters/ (mensajes.md §3.2)
- [X] T0207 [US7] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Paymasters/ `ConfirmPayrollFileSentCommand`, `LoadPaymasterResponseCommand` (formato, genérica o manual; adjunto `ReceiptsPaymasterResponse`), `PreviewPaymasterResponseQuery`, `EmitPaymasterResultCommand` (v1 o v2 por `EmisorDeMensajes` a nombre de la persona), y la recepción de la evaluación de Cartera (`AcceptedByLending`/`RejectedByLending`) (FR-076, FR-077, FR-201)
- [X] T0208 [US7] Implementar `RegisterPaymasterRemittanceCommand`, `ApplyPaymasterRemittanceCommand` (sobre `AplicacionNoInteractiva`, una línea por transacción, por los dos contratos) y `ResolvePaymasterBalanceCommand` (saldo de la pagaduría con soporte) en src/Core/IngenIA365ERP.Application/Receipts/Paymasters/, con el alcance por pagaduría (`RCD_UserPaymasterScopes`) (FR-078 a FR-080, FR-175)
- [X] T0209 [US7] Emitir `ReciboDePagaduriaEmitido` y `GiroDePagaduriaRecibido` en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Pagadurias.cs y ampliar `ElCatalogoDeMensajesEsUnico` con `EnvioDeDescuentosConfirmado` y `ResultadoDeDescuentos` emitidos por `REC` (contabilidad.md §3; mensajes.md §4)
- [X] T0210 [US7] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/PaymastersEndpoints.cs (api.md §6) y crear src/Presentation/IngenIA365ERP.Shared/Services/Recaudo/PagaduriasClient.cs con la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Pagadurias.razor (`/recaudo/pagadurias`: archivos por pagaduría y período, confirmar envío, cargar y revisar la respuesta, registrar y aplicar el giro con avance, no aplicados; la versión que falta adoptar se dice) con `IndicadorDeCarga` por zona
- [X] T0211 [US7] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de pagadurías (data-model §6)
- [ ] T0212 [US7] P6 · Entregar las pagadurías de COOFLOPAL, sus convenios, la prelación y la estructura de su respuesta, y confirmar si la libranza es un canal principal; cargar los formatos de vuelta como datos (ámbito 6) y registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 06/11, porque la quincena del 15/12 exige el archivo hacia el 10/12; **bloquea la salida de la pagaduría**, no la de la caja; rige mientras tanto `RESPUESTA-PAGADURIA-GENERICA`)


**Notas de la construcción de la fase 12 (2026-10-24)**. Pruebas escritas y compiladas, sin correr («prueba escrita, sin correr»). Desviaciones y decisiones; las
nuevas quedan en decisiones-transversales §2.7:

- **T0201–T0203**: escritas sobre `PagaduriasDePrueba` (encima de `BancosDePrueba`: la pagaduría, el tipo `RP`, los mensajes entrantes y el registro de participantes
  simulados). La descarga auditada del archivo no se prueba en unitarias (pide el almacén de adjuntos): la cubre la e2e. La parte de pagadurías de T0189a (el evento
  resumen de la tanda) se cubre porque el giro usa la misma `CorridasNoInteractivas.CerrarTandaAsync` que ya prueba `AuditoriaDeCorridasTests`.
- **T0204 (pendiente)**: la e2e de punta a punta necesita un convenio de libranza real de Cartera (versiones, formatos de ida y vuelta, archivo generado por K2) y la
  activación de Recaudo como destino; queda para escribirla junto con las demás e2e de la fase 22, sin marcar.
- **T0205**: siete entidades en `Entities/Receipts/Paymasters` (tablas `RCD_PayrollDeductionFiles`, `…FileLines`, `RCD_PayrollFileSendings`, `RCD_PaymasterResponses`,
  `…ResponseLines`, `RCD_PaymasterRemittances`, `…RemittanceLines`), sin FK hacia `LND_`: Recaudo guarda su copia del archivo que le llega por el mensaje. Hechos (H):
  `PayrollDeductionFileLine` (resultado de la pagaduría con `[EscrituraUnica]`, lo girado `RemittedAmount` como `[ColumnaMutable]`) y `PayrollFileSending`. El
  archivo lleva además `FileNumber` y `BranchId` (la oficina del sobre). Los DbSets se llaman `ReceiptsPayrollDeductionFiles`/`…Lines` porque Cartera ya usa los nombres.
- **T0206**: `ConsumePayrollDeductionFileCommand` (v1 y v2) con recibo único en `RCD_InboundMessages`; `DestinoRecaudo` acepta `DescuentosPorPagaduria` v1 y v2 y elige
  el comando por el tipo del mensaje (lee el primero por `IMensajesEntrantes`). La activación de Recaudo adopta ahora también las dos versiones. Reemplazo: una versión
  posterior deja la anterior `Superseded` si no se envió; si ya se envió o respondió, el mensaje queda `Rejected` con `Receipts.Paymaster.SupersedesSentFile` y la alerta
  `Recaudo.NoAplicadoDePagaduria` (no hay tipo propio para este aviso; se reusa el de la pagaduría).
- **T0207**: `ConfirmPayrollFileSentCommand` (repetir devuelve el archivo sin emitir), `LoadPaymasterResponseCommand` (formato del ámbito 6 pedido, o `RESP-PAGADURIA-GEN`, o
  captura `POST …/responses/captured`, nuevo), `PreviewPaymasterResponseQuery`, `EmitPaymasterResultCommand` y la evaluación de Cartera como comando explícito
  `RefreshPaymasterResponseStatusCommand` (`POST …/responses/{rid}/refresh`, nuevo): lee la entrega del mensaje a `Lending` (sólo lectura de `COR_IntegrationMessageDeliveries`,
  como ya hace Cartera); `Processed` → `AcceptedByLending`, escribe el resultado de cada línea y deja el archivo `Responded`; `Rejected` → `RejectedByLending` con código y
  datos. Desviación: Recaudo no conoce las equivalencias del convenio, así que en la v2 reparte lo descontado por empleado y código **en el orden de las líneas del archivo**
  (el de prelación con que lo armó Cartera); si Cartera repartió distinto, el contrato rechaza la línea al aplicar el giro (`PaymasterReferenceInvalid`) y queda como saldo
  de la pagaduría. Sin fecha del descuento en la respuesta se toma el último día del mes del período. El motivo de no descuento que viaja en la v1 es `Other` (la
  respuesta genérica no trae la taxonomía de Cartera). Además de api.md §6: `GET …/responses` (lista).
- **T0208**: `RegisterPaymasterRemittanceCommand` desde **una línea del extracto** (fuente `BankItem`; el recibo de caja y el cheque recibido quedan para cuando se
  necesiten: el validador sólo admite la línea, desviación), el reparto en el orden del archivo o el del analista (`LineOrder`), excedente con
  `SurplusAsCreditBalance` (`PaymasterSurplus`, sin hecho propio: el dinero ya está en `SaldoDePagadurias`), identificación del abono con `BankItemOutcome.PaymasterRemittance`
  y numeración con la clase `GP` (nuevo). `ApplyPaymasterRemittanceCommand` por `GirosDePagaduria` (tanda con todos los cerrojos antes —configuración, pagadores, líneas del
  giro—), `ProcessingRunKind.PaymasterRemittance`, rechazo → saldo `PaymasterUnapplied` y alerta; el reproceso por `IReprocesoDeCorrida` baja ese saldo si la línea se
  aplica después. `ResolvePaymasterBalanceCommand` (`POST /api/receipts/paymaster-balances/{id}/resolve`, nuevo) exige soporte y delega en la devolución por orden o la
  aplicación a un recibo de «Saldos a favor». El nivel 6 del cerrojo admite ahora `RCD_PayrollDeductionFileLines` (`TablasDeRecaudo.LineasDeDescuento`), que toma el registro
  del giro. Además: `GET /api/receipts/paymaster-remittances` (lista).
- **T0209**: `GiroDePagaduriaRecibido` (`GPR`) y `ReciboDePagaduriaEmitido` (`RPG`), en línea; `EnvioDeDescuentosConfirmado` y `ResultadoDeDescuentos` ya estaban en
  `CatalogoDeMensajesHaciaCarteraV1` como `REC`. `ElCatalogoDeMensajesEsUnico` cuenta 19 tipos `REC` y 16 hechos.
- **T0210**: pantalla `/recaudo/pagadurias` (archivos, envío, respuesta por archivo o captura, revisión, devolución, evaluación, giro y aplicación, líneas no aplicadas) en
  «Bancos y pagadurías»; `PagaduriasClient` registrado en Web, Web.Client y MAUI. La pantalla todavía no resuelve saldos de pagaduría (se hace por la ruta). Sin probar en
  navegador.
- **T0211**: regenerados en par los tres pares de la 015 en orden (+7 tablas en `RecaudoNucleo`), con las semillas a mano conservadas. Paridad OK (66 en par);
  `has-pending-model-changes` da «No changes» en SQL Server y en PostgreSQL.
- **T0212**: tarea del dueño (P6), no ejecutada.

---

## Phase 13: User Story 15 - Cheques recibidos, canje y devolución (Priority: P1) — entrega R0 (parcial)

**Goal**: el cheque recibido como medio cuando el parámetro lo admite (apagado por defecto), con el plazo de presentación
avisado, el posfechado rechazado o en custodia, el canje manual y la **devolución** con reverso por los contratos de sólo
las porciones pagadas con ese cheque (FR-060, FR-083 a FR-086).

**Independent Test**: pagar con un cheque una cuota de crédito y un aporte junto con efectivo, devolverlo por fondos
insuficientes: se reversan sólo las porciones del cheque, el efectivo sigue aplicado, el medio se da de baja sin saldo a
favor, el pagador recibe aviso y la alerta queda; con una devolución ya entregada, queda una cuenta por cobrar al pagador.

### Tests for User Story 15

- [X] T0213 [P] [US15] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Checks/RecepcionYCanjeDeChequesTests.cs (FR-010, FR-083, FR-084, FR-086; D21, D37): con `Caja.AdmiteCheque` apagado el medio no se ofrece; banco, número, cuenta, girador (persona), fecha y valor; de tercero sólo con `Caja.AdmiteChequeDeTercero` (`Receipts.Check.ThirdPartyNotAllowed`); plazo de presentación por plaza, con **caso propio de FR-010** (fuera de plazo sólo avisa y no bloquea mientras `Cheques.BloquearFueraDePlazo` esté apagado; encendida, bloquea); posfechado `Receipts.Check.Postdated` o en custodia sin recibo con `Caja.AdmitePosfechadoEnCustodia`; estados manuales recibido → consignado → en canje → pagado; firmeza y canje informados por los módulos
- [X] T0214 [P] [US15] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Checks/DevolucionDeChequeTests.cs (FR-060, FR-085, FR-188): `ReturnReceivedCheckCommand` con causal del catálogo reversa en una sola operación sólo las porciones pagadas con el cheque (obligaciones antes que cuentas), aunque la cuenta quede en sobregiro técnico con bloqueo; baja del medio `WrittenOff` sin saldo a favor; cuenta por cobrar al pagador si ya se devolvió dinero; aviso al pagador y alerta `Recaudo.ChequeDevuelto`; lista de giradores con devoluciones que advierte al cajero; cargo por devolución apagado (`Cheques.CargoPorDevolucion`) y, si es un servicio, con factura (FR-179); `Receipts.Check.AlreadyReturned`
- [ ] T0215 [P] [US15] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ChequeRecibidoDevueltoTests.cs (US15 «Independent Test», quickstart recorrido H paso 1); se corre en la fase 22

### Implementation for User Story 15

- [X] T0216 [P] [US15] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/ `ReceivedCheck`, `ReceivedCheckEvent` (H) y `CheckReturnCause`, el enum `ReceivedCheckState`, con su configuración (UK `(DraweeBankId, AccountNumber, CheckNumber)` sin filtro) y la semilla de causales «pendiente de validar» en `RecaudoNucleo` (data-model §4.5)
- [X] T0217 [US15] Integrar el cheque recibido como medio en `UpdateCashOperationMeansCommand` y `ConfirmacionDeRecibo` (imputación al final, D35) e implementar en src/Core/IngenIA365ERP.Application/Receipts/Checks/ `MarkReceivedCheckDepositedCommand`, `MarkReceivedCheckInClearingCommand`, `MarkReceivedCheckPaidCommand` y `ReturnReceivedCheckCommand` (reverso por porción con `ReversarAsync` y clave `:reverso`) (FR-060, FR-083, FR-084)
- [X] T0218 [US15] Emitir `ChequeRecibidoDevuelto` en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Cheques.cs (por porción reversada contra `ChequesEnCanje`; con cuenta por cobrar al pagador) y ampliar `ElCatalogoDeMensajesEsUnico` (contabilidad.md §3)
- [X] T0219 [US15] Exponer `/api/receipts/received-checks` (`…/deposit`, `/clearing`, `/paid`, `/return`) en src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/ReceivedChecksEndpoints.cs y crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/ChequesRecibidos.razor (`/recaudo/cheques-recibidos`) con su cliente e `IndicadorDeCarga` por zona
- [X] T0220 [US15] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de cheques recibidos
- [ ] T0221 [US15] P2 · Confirmar si COOFLOPAL recibe cheques en ventanilla (para encender `Caja.AdmiteCheque` en R0) y si tiene convenios de recaudo con referencia o corresponsales (R2); registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 06/11; no bloquea: rige la clase cheque apagada, D37, y sin convenios hasta R2)


**Notas de la construcción de la fase 13 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **T0213, T0214**: escritas sobre `ChequesDePrueba` (encima de `CajaConContratos`: el medio `CH` ofrecido en el punto antes de abrir la sesión, un banco librado, dos
  causales y los reversos simulados). La firmeza y el canje «informados por los módulos» no tienen mensaje en R0: el canje es manual. El cargo por devolución
  (`Cheques.CargoPorDevolucion`) sigue apagado y no se construye su cobro (FR-179 queda para cuando el dueño lo encienda); la prueba no lo cubre.
- **T0215 (pendiente)**: la e2e se escribe con las demás e2e de la fase 22; sin marcar.
- **T0216**: `ReceivedCheck` (no es hecho: su `Status` avanza), `ReceivedCheckEvent` (H) y `CheckReturnCause`, enum `ReceivedCheckState` (`Received`, `InCustody`,
  `Deposited`, `InClearing`, `Paid`, `Returned`). Además de data-model §4.5: `DrawerName` (girador que no es persona del maestro), `PayerPersonId`, `BranchId`, `ReceiptId`,
  `PayerReceivableId`; el catálogo lleva `CountsForDrawerList` (la causal cuenta para la lista de giradores con devoluciones). La FK `RCD_ReceiptMeans.ReceivedCheckId` →
  `RCD_ReceivedChecks` se declara ahora. Semilla de nueve causales «pendiente de validar» (`01` fondos insuficientes … `09` fuera de plazo) en la cola a mano de
  `RecaudoNucleo`. `ApplicationState` suma `ReversedByCheckReturn = 4`.
- **T0217**: el cheque viaja en el medio de la operación (`MedioDeCajaDto.Check`, `ChequeDeCajaDto`, nuevo) y `ChequesRecibidos.ValidarAsync` lo revisa al imputar
  (`UpdateCashOperationMeansCommand`) y otra vez al confirmar (`ConfirmacionDeRecibo` crea la fila y la enlaza al medio); la imputación ya lo dejaba al final (D35).
  Reglas: `Caja.AdmiteCheque` (`Receipts.PaymentMeans.CheckDisabled`), datos (`Receipts.Check.DataRequired`, nuevo), tercero = girador distinto del pagador
  (`.ThirdPartyNotAllowed`), posfechado (`.Postdated`; con `Caja.AdmitePosfechadoEnCustodia` se recibe en custodia por `POST /received-checks/custody`,
  `RegisterCheckInCustodyCommand`, nuevo; `.NotPostdated` si no lo es), plazo por plaza (días en la misma, meses en otra; sólo avisa en la pantalla salvo
  `Cheques.BloquearFueraDePlazo` → `.PresentationDeadlinePassed`) y el mismo cheque una sola vez (`.Duplicate`, nuevo). Las tres transiciones manuales van en un solo
  manejador (`TransicionesDeChequeHandler`) con su evento y `.InvalidTransition` (nuevo). La devolución (`ReturnReceivedCheckCommand`) numera con la clase `CD` (nueva),
  reversa las porciones aplicadas del medio por los contratos (todo o nada: `.ReturnRejectedByContract`, nuevo), deja el medio `WrittenOff` y la aplicación
  `ReversedByCheckReturn`, y crea la cuenta por cobrar al pagador por la diferencia entre el valor del cheque y lo reversado (vueltas o un saldo ya devuelto). El
  «aviso al pagador» es, en R0, la alerta `Recaudo.ChequeDevuelto` a la oficina con el texto para avisarle (no hay canal de mensajes al pagador en Recaudo). Además:
  `GET /received-checks/drawers-with-returns` (FR-086) y `GET /received-checks/return-causes`.
- **T0218**: `ChequeRecibidoDevuelto` (`CRD`), en línea: por porción, puente del módulo (o concepto vario) → `ChequesEnCanje`; la cuenta por cobrar,
  `CuentaPorCobrarPagador` → `ChequesEnCanje`. `ElCatalogoDeMensajesEsUnico` cuenta 20 tipos `REC` y 17 hechos.
- **T0219**: `ReceivedChecksEndpoints`, pantalla `/recaudo/cheques-recibidos` (lista, canje, devolución con causal, giradores con devoluciones) en «Bancos y
  pagadurías», `ChequesRecibidosClient` en Web, Web.Client y MAUI; la pantalla de caja pide banco, número, cuenta, fecha, plaza y girador cuando el medio es cheque. Sin
  probar en navegador.
- **T0221**: tarea del dueño (P2), no ejecutada.
- **T0220**: regenerados en par los tres pares de la 015 en orden (+3 tablas en `RecaudoNucleo` y la FK del medio al cheque), con la semilla de causales a mano. Paridad
  OK (66 en par); `has-pending-model-changes` da «No changes» en SQL Server y en PostgreSQL.

---

## Phase 14: User Story 19 - Chequeras y cheques girados; remesas (Priority: P1) — entrega T0 (parcial)

**Goal**: la chequera mínima del primer día (rango, numeración automática desde la orden, anulación por daño, estados
emitido, entregado, cobrado y anulado, constancia de entrega que informa «girada», custodia de las chequeras en blanco) y
las remesas de efectivo bóveda ↔ banco con acta y sello de bolsa, registradas por Recaudo y Tesorería en la misma
transacción (FR-035 parte T0, FR-105, FR-113, FR-122, FR-134 a FR-136, FR-182 parte T0, FR-219).

**Independent Test**: girar un cheque de una orden, anular el siguiente por dañado y comprobar que la numeración no tiene
huecos sin explicar; entregar el primero con constancia: la orden queda «girada»; remesar desde la bóveda al banco con acta y
sello: queda en tránsito y, sin confirmar a los días configurados, alerta.

### Tests for User Story 19

- [X] T0222 [P] [US19] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Checkbooks/ChequerasYChequesGiradosTests.cs (R-19; FR-113, FR-122, FR-134 a FR-136, FR-219): registro de la chequera con rango y custodio; numeración desde `NextNumber` bajo `CerrojoDeTesoreria`; `Treasury.Checkbook.Exhausted` pide la siguiente y `.NumberOutOfRange`; cheque en blanco dañado `SpoiledBlank` con su número; el hecho `ChequeGiradoEmitido` nace al **emitir**; la entrega con constancia registra «girada»; anulado antes de entregar → `ChequeGiradoAnulado` sin resultado; no cobrado a `Cheques.DiasSinCobrar` alerta `Tesoreria.ChequeSinCobrar` y, anulado con permiso, informa «cheque no cobrado»; cruce y primer beneficiario por defecto, ligados a la exención de GMF; huecos sólo explicados por anulaciones
- [X] T0223 [P] [US19] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Remittances/RemesasTests.cs (R-15; FR-035, FR-105, FR-182): Recaudo despacha desde la bóveda (doble custodia, acta, sello de bolsa) y registra la remesa por `IRemesasDeTesoreria.RegistrarAsync` en la misma transacción; hecho `RemesaDespachada` (REC) y, al confirmar con soporte o por cruce, `RemesaConfirmada` (TES); banco a bóveda; sin confirmar en `Remesas.DiasParaConfirmar` alerta `Tesoreria.RemesaSinConfirmar`; desde una cuenta del fondo de liquidez exige uso aprobado
- [ ] T0224 [P] [US19] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/ChequesGiradosYRemesasTests.cs (US19 «Independent Test», quickstart recorridos H paso 2 e I paso 1); se corre en la fase 22

### Implementation for User Story 19

- [X] T0225 [P] [US19] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Checkbooks/ `Checkbook`, `IssuedCheck`, `IssuedCheckEvent` (H) y en Entities/Treasury/Remittances/ `CashRemittance`; los enums `IssuedCheckState`, `RemittanceState`; configurarlas (UK `CheckNumber` por chequera; el nombre `IssuedCheck` no choca con el heredado `Check`, R-04) (data-model §11)
- [X] T0226 [US19] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Checkbooks/ `RegisterCheckbookCommand`, `VoidCheckbookCommand`, `SpoilBlankCheckCommand`, la emisión de cheques del lote de forma `Checks` (`IssueChecksForBatchCommand`), `DeliverIssuedCheckCommand` (constancia `TreasuryDeliveryReceipt` → resultado «girada») y `VoidIssuedCheckCommand` (antes de entregar o no cobrado) (FR-113, FR-122, FR-134 a FR-136, FR-219)
- [X] T0227 [US19] Implementar `IRemesasDeTesoreria` en src/Core/IngenIA365ERP.Application/Treasury/Remittances/RemesasDeTesoreria.cs, `ConfirmRemittanceCommand` (con soporte) y, del lado de Recaudo, `DispatchVaultRemittanceCommand` en src/Core/IngenIA365ERP.Application/Receipts/Vaults/ (movimiento de bóveda + remesa en la misma transacción) (R-15; FR-035, FR-105)
- [X] T0228 [US19] Emitir `ChequeGiradoEmitido`, `ChequeGiradoAnulado` (causa `SpoiledBeforeDelivery` o `NotCashed`), `RemesaConfirmada` en src/Core/IngenIA365ERP.Application/Treasury/Accounting/EmisionDeTesoreria.Cheques.cs y `RemesaDespachada` en …/Receipts/Accounting/EmisionDeRecaudo.Caja.cs; completar las secciones de cheques sin cobrar y remesas sin confirmar de la tarea T0160
- [X] T0229 [US19] Crear el documento `Cheque` (impresión sobre el formato de la chequera) en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeTesoreriaReport.cs (archivos-y-plantillas.md §7)
- [X] T0230 [US19] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/{CheckbooksEndpoints,IssuedChecksEndpoints,RemittancesEndpoints}.cs (api.md §11, §12) y `POST /api/receipts/vaults/{id}/remittances` (`Receipts.Vault.Remit`); crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/{Chequeras,ChequesGirados,Remesas}.razor (`/tesoreria/chequeras`, `/tesoreria/cheques-girados`, `/tesoreria/remesas`) con su cliente e `IndicadorDeCarga`, y la acción «Remesar» en `/recaudo/boveda` (CH13)
- [X] T0231 [US19] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaNucleo` con chequeras, cheques girados y remesas


**Notas de la construcción de la fase 14 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **T0222, T0223**: escritas sobre `EscenarioDeTesoreria` (las chequeras, los cheques y la remesa del lado de Tesorería) y `CajaDePrueba` (el despacho de la bóveda con el
  puerto simulado: las dos escenas tienen bases distintas). Además, el valor en letras del cheque.
- **T0224 (pendiente)**: la e2e se escribe con las demás e2e de la fase 22; sin marcar.
- **T0225**: `Checkbook`, `IssuedCheck`, `IssuedCheckEvent` (H) en `Entities/Treasury/Checkbooks` y `CashRemittance` en `Entities/Treasury/Remittances`; enums `CheckbookState`,
  `IssuedCheckState`, `RemittanceDirection`, `RemittanceState`. Además de data-model §11: `RegisteredByUserId/At` y `VoidReason` en la chequera; en la remesa
  `SourceDocumentPublicId/Number` (el movimiento de bóveda), `IdempotencyKey` (UK: la clave de quien la registra), `DispatchDate`, `ConfirmationSupportAttachmentPublicId`,
  `ConfirmedOn` y `LiquidityFundUseId`. UK del número del cheque por chequera.
- **T0226**: los comandos de la tarea más `MarkIssuedCheckCashedCommand` (el cobro manual en T0; la conciliación lo cruzará en T1) y las consultas. Desviación de permisos: el
  catálogo sembrado no tiene `Treasury.Checks.Issue`; emitir, el cheque dañado y la impresión piden `Treasury.Checks.Print`. Rutas: `/checks` es área de la Tesorería
  heredada (`TreasuryGroup.AreasHeredadas`), así que los nuevos van en `/api/treasury/checkbooks` e `/api/treasury/issued-checks` (la emisión de un lote es
  `POST /issued-checks/batches/{lote}`). La emisión toma, en orden, configuración → cuenta → chequera → lote → órdenes; deja el lote y sus órdenes enviados. Anular antes de
  entregar deja la orden enviada sin resultado y la misma emisión le da otro cheque del lote; anular un entregado exige que pasen `Cheques.DiasSinCobrar` días
  (`Treasury.Check.StillCashable`, nuevo) y registra «cheque no cobrado» por el registro único de resultados.
- **T0227**: `RemesasDeTesoreria` implementa el puerto, que suma `RecibidaEnBovedaAsync` (la bóveda recibe la remesa del banco; nuevo en el puerto) y amplía
  `SolicitudDeRemesa` con la bóveda, el sello y el acta (opcionales). `DispatchVaultRemittanceCommand` hace en una transacción el movimiento `RemittanceOut` (doble custodia,
  acta y sello obligatorios), la remesa por el puerto y el hecho. El banco → bóveda es `RegisterBankToVaultRemittanceCommand` (`POST /api/treasury/remittances/bank-to-vault`,
  nuevo, con la guarda del fondo de liquidez) y lo cierra el movimiento de bóveda `RemittanceIn` con el `PublicId` de la remesa, que la confirma por el puerto
  (`Receipts.Vault.RemittanceMismatch` si no coincide, nuevo). Además: `GET /api/receipts/own-bank-accounts` (las cuentas de recaudo por el puerto
  `ICuentasBancariasPropias`, para la pantalla de la bóveda).
- **T0228**: `ChequeGiradoEmitido` (`CHE`), `ChequeGiradoAnulado` (`CHA`, causa en la dimensión `Cause`) y `RemesaConfirmada` (`RMC`) de `TES`; `RemesaDespachada` (`RMD`) de
  `REC`. `ElCatalogoDeMensajesEsUnico` cuenta 21 tipos `REC` (18 hechos) y 6 de `TES` (5 hechos). La vigilancia diaria de Tesorería suma `ChequesSinCobrarAsync` y
  `RemesasSinConfirmarAsync` (sólo alertan).
- **T0229**: el cheque en QuestPDF (`DocumentosDeTesoreriaReport.Cheque`, tamaño de cheque, valor en letras `ValorEnLetras`, cruce y primer beneficiario), por el puerto
  `IDocumentosDeTesoreriaEnPdf` y `GET /api/treasury/issued-checks/{id}/document`.
- **T0230**: `CheckbooksEndpoints` (chequeras y cheques girados en un archivo), `RemittancesEndpoints`, `POST /api/receipts/vaults/{id}/remittances`; pantallas
  `/tesoreria/chequeras`, `/tesoreria/cheques-girados`, `/tesoreria/remesas` en el menú de Tesorería y la clase «Remesa al banco» en `/recaudo/boveda` (con la cuenta de
  destino y el identificador de la remesa que vuelve); `ChequesYRemesasClient` en Web, Web.Client y MAUI. Sin probar en navegador.
- **T0231**: regenerados en par los tres pares de la 015 en orden (+4 tablas en `TesoreriaNucleo`). Paridad OK (66 en par); `has-pending-model-changes` da «No changes» en
  SQL Server y en PostgreSQL.

---

## Phase 15: User Story 14 - Control de efectivo y SARLAFT (Priority: P1) — entrega R0 (parcial), y contingencia de la caja (US2)

**Goal**: SARLAFT básico en Recaudo: cuatro parámetros separados con vigencia y fuente, la declaración de origen de fondos
una vez por recibo, quien efectúa la entrega, las listas vinculantes que bloquean siempre y las no vinculantes que alertan
o retienen, el registro propio de transacciones en efectivo con acumulado del mes y las señales de fraccionamiento; y la
contingencia de la caja por talonario (FR-016 alerta, FR-094, FR-095, FR-097, FR-098, FR-186, FR-189, FR-191, FR-202 a
FR-204 parte R0).

**Independent Test**: con el umbral de declaración en 5.000.000, recibir 6.000.000 en efectivo sin declaración: rechazo;
con declaración y soporte: el recibo, con quien efectúa registrado; cuatro pagos de 8.000.000 de la misma persona en una
semana: alerta de fraccionamiento y de acumulado; una persona en lista vinculante: no se recibe su dinero y el oficial
recibe la alerta inmediata; un recibo de contingencia se digita después con su fecha e imagen.

### Tests for User Story 14

- [X] T0232 [P] [US14] Escribir los casos dorados de las señales (≈8) en tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/senales-*.json con tests/IngenIA365ERP.Domain.Tests/Receipts/SenalesDeEfectivoTests.cs (R-24; FR-095, FR-202): acumulado del mes calendario por persona contra el umbral de múltiples; fraccionamiento por ventana, cantidad mínima y fracción del umbral; pagos de terceros frecuentes; consignante no identificado repetido; parámetros recibidos con su versión
- [X] T0233 [P] [US14] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Compliance/DeclaracionYQuienEfectuaTests.cs (FR-094, FR-191; C9, E-02 c): declaración exigida desde `Sarlaft.UmbralDeclaracionOrigenFondos` sobre el efectivo del recibo **o** cuando la simulación de Captaciones la pide (`Receipts.Compliance.DeclarationRequired`); se captura una vez por recibo con su soporte y viaja en `SourceOfFundsAttachmentPublicId` a cada consignación que la exija; guarda la versión del parámetro que la exigió; quien efectúa con documento y nombre sobre el umbral (`.DeliveredByRequired`)
- [X] T0234 [P] [US14] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Compliance/ListasEnLaCajaTests.cs (FR-098, FR-189, FR-203): pagador, quien efectúa y beneficiario contra `IListasRestrictivas` en `ScreeningContext.Collection`; vinculante → operación `Blocked`, sin medios ni confirmación (`Receipts.Compliance.BindingListMatch`) y alerta inmediata `Recaudo.ListaVinculante`, no parametrizable; no vinculante con `Sarlaft.ListasNoVinculantes.Efecto = Sigue` alerta y sigue, con `Retiene` → `Withheld` con el dinero en custodia de la sesión (`DineroRetenidoEnCustodia`) hasta que el oficial la libere (simulación nueva) o se devuelva por orden de pago; la liberación es del oficial con soporte
- [X] T0235 [P] [US14] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Compliance/RegistroDeEfectivoTests.cs (FR-097, FR-204 parte R0): una fila de `RCD_CashTransactions` por recibo con efectivo (pagador, quien efectúa), la anulación escribe una fila `Voiding` con signo contrario, acumulado del mes sólo con lo de Recaudo, `RCD_ComplianceAlertsLog` con la versión del parámetro; alertas `Recaudo.Fraccionamiento` y `Recaudo.AcumuladoDeEfectivo` al oficial
- [X] T0236 [P] [US2] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/ContingenciaDeCajaTests.cs (FR-186; US2-9): talonario registrado; el recibo se digita con la fecha valor del talonario dentro de la ventana del contrato, con `Receipts.Receipt.RegisterContingency`, marca «contingencia», número del talonario además del consecutivo e imagen obligatoria; informe de recibos por digitar y números sin usar; alerta `Recaudo.ContingenciaPorDigitar`
- [ ] T0237 [P] [US14] Escribir las e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ControlDeEfectivoYSarlaftTests.cs (US14 «Independent Test», quickstart recorrido J pasos 2–4) y la parte de Recaudo de ListaVinculanteBloqueaTests.cs (T0165; SC-025), y tests/IngenIA365ERP.API.IntegrationTests/Receipts/ContingenciaDeCajaE2ETests.cs (recorrido K); se corren en la fase 22

### Implementation for User Story 14

- [X] T0238 [P] [US14] Implementar el motor puro src/Core/IngenIA365ERP.Domain/Receipts/Cumplimiento/SenalesDeEfectivo.cs hasta T0232
- [X] T0239 [P] [US14] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Compliance/ `CashTransaction` (H, volumen, índice `(PersonId, Month)`) y `ComplianceAlertLog` con su configuración (data-model §7.1, §7.2)
- [X] T0240 [US14] Completar el gancho de listas de `ConfirmacionDeRecibo` y de `CreateCashOperationCommand` (persona, pagador, quien efectúa, beneficiarios) e implementar en src/Core/IngenIA365ERP.Application/Receipts/Compliance/ `SubmitSourceOfFundsDeclarationCommand` (adjunto `ReceiptsSourceOfFunds`), `ReleaseWithheldOperationCommand` (oficial, `Receipts.Compliance.Release`) y `RefundWithheldMoneyByOrderCommand` (R0: por orden de pago; en efectivo en R1) (FR-094, FR-098, FR-189, FR-191, FR-203)
- [X] T0241 [US14] Implementar src/Core/IngenIA365ERP.Application/Receipts/Compliance/RegistroDeEfectivo.cs (escribe `RCD_CashTransactions` en la transacción del recibo o la anulación; evalúa `SenalesDeEfectivo` con la versión vigente y alerta) y las consultas `ListCashTransactionsQuery` y `ListComplianceSignalsQuery` como lecturas sensibles (FR-095, FR-097, FR-204)
- [X] T0242 [US14] Emitir `DineroRetenidoEnCustodia` y `CustodiaLiberada` en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Caja.cs (movimiento `Custody`/`CustodyRelease` de la sesión) (contabilidad.md §3)
- [X] T0243 [US2] Implementar `RegisterContingencyReceiptCommand` en src/Core/IngenIA365ERP.Application/Receipts/CashDesk/ (canal `Contingency`, sin operación de caja: nace del comando y aplica `NonInteractive` con la modalidad que eligió el cajero en el talonario y la fecha valor del talonario dentro de la ventana del contrato, R-07 e interfaces.md §1.1; imagen `ReceiptsContingencyImage`; número del talonario además del consecutivo) y completar la sección de contingencia de la tarea T0197 (FR-186)
- [X] T0244 [US14] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Receipts/ComplianceEndpoints.cs (`/api/receipts/compliance/cash-transactions`, `/signals`, `/api/receipts/cash-operations/{id}/release`) y `POST /api/receipts/contingency-receipts` en CashDeskEndpoints.cs, con sus permisos (api.md §4, §7)
- [X] T0245 [US14] Crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Cumplimiento.razor (`/recaudo/cumplimiento`: registro de efectivo por persona y mes, señales, operaciones retenidas por liberar) y, en `/recaudo/caja`, la captura de la declaración y de quien efectúa y el modo «digitar contingencia»; con `IndicadorDeCarga` por zona
- [X] T0246 [US14] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoNucleo` con las tablas de cumplimiento (data-model §7.1, §7.2)
- [ ] T0247 [US14] P7 · El oficial de cumplimiento valida los umbrales vigentes del sector solidario (5 y 30 millones o los que rijan en 2026), el estado de la suspensión del reporte, el alcance del Anexo 2 (sólo efectivo o todas), el reporte de ausencia, las señales de fraccionamiento y las listas vinculantes que la plataforma consulta; registrarlo como versiones de los parámetros `Sarlaft.*` y en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 20/11; **bloquea la salida** en umbrales y listas; la estructura del reporte de transacciones, antes del primer vencimiento de enero de 2027; rige mientras tanto 5.000.000 y 30.000.000 «pendiente de validar», reporte `Vigente`, alcance `SoloEfectivo`)


**Notas de la construcción de la fase 15 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **T0232, T0238**: ocho casos dorados `senales-01..08.json` (generados con una hoja de cálculo propia y revisados a mano) y el motor puro
  `Domain/Receipts/Cumplimiento/SenalesDeEfectivo`: acumulado del mes calendario contra `Sarlaft.UmbralTransaccionesMultiples` (la anulación resta), fraccionamiento
  «operaciones;días» por debajo del umbral de declaración que juntos lo alcanzan, pagador tercero frecuente (titulares distintos en el mes) y consignante no identificado
  repetido. Los dos últimos umbrales (3 titulares, 2 partidas) no son parámetros todavía: quedan como constantes nombradas hasta que el oficial los valide (P7).
  **Cerrado el 2026-10-24 (pendiente A1)**: los dos umbrales son ya parámetros `RCD` con vigencia, `Sarlaft.PagadorTerceroFrecuente.Titulares` (3) y
  `Sarlaft.ConsignanteRepetido.Partidas` (2), «pendiente de validar» por el oficial (P7), mínimo 1; los leen `RegistroDeEfectivo.ParametrosAsync` y
  `AntiguedadDeRecaudo.PartidasAsync`, y las constantes `TercerosFrecuentesPorDefecto`, `ConsignanteRepetidoPorDefecto` y `AntiguedadDeRecaudo.ConsignanteRepetido` se retiraron.
- **T0233–T0236**: escritas. La declaración y quien efectúa ya los construyó la fase 6 (T0115); la prueba nueva los recorre de punta a punta. La declaración que pide la
  simulación de Captaciones y su viaje en `SourceOfFundsAttachmentPublicId` ya estaban cubiertos por las pruebas de la fase 6.
- **T0237 (pendiente)**: las e2e se escriben con las demás e2e de la fase 22; sin marcar.
- **T0239**: `CashTransaction` (H, `AuditableEntityLong`, índices `(PersonId, Month)` y `(PayerPersonId, Month)`, UK `(DocumentPublicId, DocumentKind)`) y
  `ComplianceAlertLog` (`DepositorKey` para el consignante, `ParameterVersion` como texto: la versión de `COR_ParameterVersions` o «defecto»). DbSets
  `ReceiptsCashTransactions` y `ReceiptsComplianceAlertLogs`; enums `CashTransactionDocumentKind` y `CashTransactionDirection`; `SenalDeEfectivo` vive en el motor.
- **T0240**: `ListasDeRecaudo` reemplaza al gancho sin consulta (`ListasDeRecaudoSinConsulta`, retirado), con el molde de `ListasDeTesoreria` y el contexto `Collection`.
  Decisión del dueño (pendiente de P7): la coincidencia **exacta** es vinculante y bloquea; la **posible**, no vinculante. La alerta vinculante va por `AlertasDiferidas`
  para sobrevivir al fallo de la confirmación. `CreateCashOperationCommand` consulta desde que nace la operación: vinculante → `Blocked` (sin medios ni confirmación). Con
  `Retiene`, la confirmación responde `Receipts.Compliance.WithholdRequired` (nuevo) y la retención es **su propio paso** (`WithholdCashOperationCommand`,
  `POST …/cash-operations/{id}/withhold`, nuevo), porque un fallo de la confirmación revierte todo; el efectivo entra a la sesión como `Custody`. La liberación
  (`ReleaseWithheldOperationCommand`) la hace otra persona con soporte y sesión abierta (`CustodyRelease`; la operación queda liberada en `ReleasedAt` y la confirmación ya no
  la retiene). `RefundWithheldMoneyByOrderCommand` pide la orden a Tesorería con la clase nueva `WithheldMoneyRefund` (agregada a `OrdenesDePagoDeTesoreria.ClasesDeRecaudo`) y
  el efectivo sigue en la caja hasta la remesa; el rechazo de esa orden reabre la custodia desde el 2026-10-24 (nota de la fase 16). `SubmitSourceOfFundsDeclarationCommand` no se creó: ya existe como
  `DeclareSourceOfFundsCommand` (fase 6).
- **T0241**: `RegistroDeEfectivo` escribe en la transacción del recibo (`ConfirmacionDeRecibo`) y de la anulación (`ApproveReceiptVoidingCommand`), los dos con un parámetro
  opcional para no tocar a quien los construye; las alertas se deduplican por persona, señal y mes. `ListCashTransactionsQuery` y `ListComplianceSignalsQuery` dejan su
  lectura en la auditoría; además `ListWithheldOperationsQuery`.
- **T0242**: `DineroRetenidoEnCustodia` (`DCU`, `Caja` → `CustodiaPorLista`) y `CustodiaLiberada` (`CLB`, `CustodiaPorLista` → `Caja` o `PuenteDeDevolucion`), en su propio
  archivo `EmisionDeRecaudo.Cumplimiento.cs` (la tarea decía `…Caja.cs`). `ElCatalogoDeMensajesEsUnico` cuenta 23 tipos `REC` y 20 hechos.
- **T0243**: `RegisterContingencyReceiptCommand` sobre `AplicacionNoInteractiva` (canal `Contingency`, `AllowLateValueDate = false`, clave
  `REC:contingencia:{talonario:N}:{número}`) y `GetContingencyBookStatusQuery`; la tarea `recaudo.antiguedad` suma `ContingenciaPorDigitarAsync`. El efectivo del recibo de
  contingencia no entra a una sesión: lo cuadra el supervisor con el talonario.
- **T0244**: `ComplianceEndpoints` (las rutas de cumplimiento y también `POST /api/receipts/contingency-receipts` y `GET /api/receipts/contingency-books/{id}/status`, que la
  tarea ponía en `CashDeskEndpoints`).
- **T0245**: pantallas `/recaudo/cumplimiento` (registro, señales, retenidas) y `/recaudo/contingencia` (digitar el talonario y su informe; la tarea lo ponía como modo de
  `/recaudo/caja`), y en la caja la confirmación que pide retener llama a la retención; la declaración y quien efectúa ya se capturaban. `CumplimientoClient` en Web,
  Web.Client y MAUI. Sin probar en navegador.
- **T0247**: tarea del dueño (P7), no ejecutada.
- **T0246**: regenerados en orden los pares `RecaudoNucleo` (`RCD_CashTransactions`, `RCD_ComplianceAlertsLog`), `TesoreriaNucleo` e `IntegracionContableDeRecaudoYTesoreria`
  con sus colas a mano; `check-migration-parity.ps1` en par (Admin 15, Application 66) y `has-pending-model-changes` «No changes» en los dos motores.

---

## Phase 16: User Story 8 - Contabilidad recibe los hechos de Recaudo y Tesorería por su matriz (Priority: P1) — entrega R0 y T0 (+ E-01 c)

**Goal**: la enmienda E-04 a la 009: dos ramas nuevas en `DestinoContabilidad` (`REC`, `TES`) que contabilizan siempre por
`AccountingPoster`; dos matrices con plantilla de filas sugeridas sin cuenta y completitud; el rol `Banco` resuelto por el
vínculo; la validación previa que bloquea en pantalla y avisa en procesos; los recibos de caja por lotes resumidos por día,
punto y medio; los tipos de comprobante; lo de Recaudo y Tesorería no se reversa desde Comprobantes; la conciliación de
puentes y con Contabilidad; y el mensaje del archivo de desembolsos de Cartera (E-01 c) (FR-140 a FR-146 partes R0/T0,
FR-108, FR-164 c, FR-167 parte R0/T0, FR-184, FR-185, FR-209, FR-221).

**Independent Test**: con la matriz completa, emitir un recibo en efectivo y transferencia que paga un crédito y una cuota de
aportes: el lote del día genera un comprobante `RR` resumido y la conciliación de puentes de Cartera y Captaciones da cero;
quitar una regla con «bloquear» impide confirmar en caja y dice hecho, rol, combinación y quién la completa; un comprobante
de Recaudo no se reversa desde Comprobantes.

### Tests for User Story 8

- [X] T0248 [P] [US8] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Receipts/MatrizDeRecaudoTests.cs y tests/IngenIA365ERP.Application.Tests/Accounting/Treasury/MatrizDeTesoreriaTests.cs (R-22; FR-143; contabilidad.md §5, §6): hecho × rol × dimensiones por código; pesos caja 256, punto 128, medio 64, concepto 64, clase de orden 64, módulo y naturaleza 32, oficina 4, hecho 2 sólo en roles de resultado; centro de costo asignado sin peso; empate = error de configuración; vigencia sin retroactividad; el rol `Banco`/`BancoRecaudo` por `ACC_OwnBankAccountLinks` no admite regla; `PuenteDeGiroDelOrigen` por `OrderOrigin` y `OrderKind`
- [X] T0249 [P] [US8] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Receipts/PlantillaYCompletitudTests.cs (FR-143, FR-227): la plantilla lista filas sugeridas **sin cuenta** según la configuración real (medios por punto, conceptos, cuentas propias, clases de orden); completitud por hecho que no esté en «no pasa»; ninguna regla con cuenta sembrada
- [X] T0250 [P] [US8] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Receipts/ValidacionPreviaDeRecaudoYTesoreriaTests.cs (R-23; FR-143, FR-221): con `Contabilidad.FaltaDeRegla = Bloquear` la pantalla no confirma (`Receipts.Prevalidation.NotPostable` / `Treasury.Prevalidation.NotPostable` con `data.errors[] { factType, role, combination, whoFixes }`); en procesos avisa y la línea queda para reproceso; sin respuesta bloquea en pantalla y avisa en procesos; sin contabilidad iniciada o con el hecho en «no pasa» nada se bloquea; pasar a «advertir» exige `Receipts.Accounting.PolicyChange` / `Treasury.Accounting.PolicyChange` y motivo, se audita y alerta cada día (`Recaudo.PoliticaContableEnAdvertir`, `Tesoreria.PoliticaContableEnAdvertir`)
- [X] T0251 [P] [US8] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Receipts/ContabilizacionDeRecaudoYTesoreriaTests.cs (FR-140 a FR-145; contabilidad.md §3, §4, §7): las ramas `REC` y `TES` del enrutador por `AccountingPoster`; cada hecho de R0/T0 con sus roles; un comprobante por unidad; recibos de caja por lotes resumidos por día, punto y medio con tercero o cruce sólo donde la cuenta lo exija; un punto con el día abierto no detiene el lote de los demás (FR-184); los anulados del día anterior van en línea; los correctivos enlazados al original y fechados en el período abierto; `Accounting.Document.ModuleOwned` con «se corrige desde Recaudo / Tesorería»
- [X] T0252 [P] [US8] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Receipts/ConciliacionDePuentesYConContabilidadTests.cs (FR-144, FR-146; SC-014): los hechos `REC` y `TES` saldan `RecaudoPorAplicar`, `DesembolsoPorGirar` y `DevolucionPorPagar` (el rol de Captaciones); la conciliación explica cada diferencia hecho por hecho; caja por oficina, bancos, partidas, saldos a favor y puentes con cero diferencias sin explicar
- [X] T0253 [P] [US10] Escribir tests/IngenIA365ERP.Application.Tests/Lending/Disbursements/ArchivoDeDesembolsosInformadoTests.cs y tests/IngenIA365ERP.Application.Tests/Treasury/Orders/ConsumoDelArchivoDeDesembolsosTests.cs (E-01 c; contratos-v2.md §2.3; FR-108, FR-164): Cartera emite `ArchivoDeDesembolsosGenerado` v1 y `…Anulado` v1 (`CAR → Treasury`, clave `Confirmation:{archivo:N}` y `Voiding:{archivo:N}`, a nombre de quien genera o anula); Tesorería guarda el archivo con sus órdenes y no las paga; el anulado las deja a la espera de otro archivo de Cartera sin resultado
- [X] T0254 [US8] Llenar `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas` (ningún tipo de hecho `REC`/`TES` tiene propiedades de cuenta contable), `LoDeRecaudoYTesoreriaNoSeReversaDesdeComprobantes` y `UnSoloConsumidorPorDestino` (`Accounting` sigue con un solo consumidor, `DestinoContabilidad`) en tests/IngenIA365ERP.Architecture.Tests/Principles/, y cerrar la ampliación de `ElCatalogoDeMensajesEsUnico` con los 25 tipos de R0/T0 y los mensajes de §2 de mensajes.md (CH5)
- [ ] T0255 [P] [US8] Escribir las e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ContabilizacionDeRecaudoYTesoreriaE2ETests.cs (US8 «Independent Test», quickstart recorrido A paso 10) y tests/IngenIA365ERP.API.IntegrationTests/Receipts/ValidacionPreviaContableTests.cs (recorrido E); se corren en la fase 22

### Implementation for User Story 8

- [X] T0256 [P] [US8] Crear en src/Core/IngenIA365ERP.Domain/Entities/Accounting/Integration/ `ReceiptsPostingRule`, `TreasuryPostingRule`, `ReceiptsPosting`, `TreasuryPosting`, `ReceiptsVoucherMapping`, `TreasuryVoucherMapping` (molde `ACC_LendingPostingRules`) con su configuración (data-model §17.1, §17.2)
- [X] T0257 [US8] Implementar src/Core/IngenIA365ERP.Application/Accounting/Receipts/{Reglas,Contabilizacion,Lotes,Plantilla}/ y src/Core/IngenIA365ERP.Application/Accounting/Treasury/{Reglas,Contabilizacion,Plantilla}/: resolución por pesos, `PostReceiptsFactsCommand`, `PostReceiptsSummaryGroupCommand`, `PostTreasuryFactsCommand`, plantilla, completitud y CRUD de reglas con vigencia, siempre por `AccountingPoster` (R-22; FR-143) **ruta crítica**
- [X] T0258 [US8] Agregar las ramas `REC` y `TES` a src/Core/IngenIA365ERP.Application/Accounting/Integration/DestinoContabilidad.cs y sus tipos a `VersionesAceptadas`, sin tocar las ramas `INV` y `CAR` (FR-140; FR-167)
- [X] T0259 [US8] Implementar src/Core/IngenIA365ERP.Application/Accounting/Receipts/ContabilidadParaRecaudo.cs (`EvaluarAsync`, `EstadoDelPeriodoAsync`, `SaldoContableAsync`) y completar `ContabilidadParaTesoreria` (`EvaluarAsync`, `SaldoContableAsync`, `MovimientosDeCuentaBancariaAsync` con `sourceModule`, para que lo heredado y Contabilidad se vean en el saldo, la posición y la conciliación mínima) (R-23; FR-209; interfaces.md §3)
- [X] T0260 [P] [US8] Implementar `IDimensionesDeRecaudo` (puntos, cajas, medios, conceptos) en src/Core/IngenIA365ERP.Application/Receipts/Accounting/DimensionesDeRecaudo.cs e `IDimensionesDeTesoreria` (cuentas propias, clases de orden) en src/Core/IngenIA365ERP.Application/Treasury/Accounting/DimensionesDeTesoreria.cs, que Contabilidad usa para la plantilla (FR-143)
- [X] T0261 [US8] Agregar `Accounting.Document.ModuleOwned` para los comprobantes `REC` y `TES` en `PrepareReversalAsync` (src/Core/IngenIA365ERP.Application/Accounting/Documents/) con el mensaje «se corrige desde Recaudo / Tesorería», y sumar el lado `REC`/`TES` a la conciliación de puentes de la 013 en src/Core/IngenIA365ERP.Application/Lending/Integration/ConciliacionContableDeCartera.cs (FR-144, FR-145)
- [X] T0262 [US8] Implementar el cambio de política `SetReceiptsAccountingPolicyCommand` y `SetTreasuryAccountingPolicyCommand` (`PUT /api/receipts/accounting-policy`, `/api/treasury/accounting-policy`) con permiso, motivo, auditoría y la alerta diaria de las tareas T0197 y T0160 (FR-221)
- [X] T0263 [US8] Implementar la tarea `recaudo.lote-del-dia` en src/Core/IngenIA365ERP.Application/Receipts/Tareas/LoteDelDiaDeRecaudo.cs (dispara `PostReceiptsSummaryGroupCommand` por cooperativa tras la hora de corte, con las sesiones cerradas; las abiertas entran al siguiente con alerta `Recaudo.DiaDelPuntoAbierto`) y deje la marca de corrida que espera `recaudo.cuadre-diario`; el orden con el ciclo diario de Cartera **no** se declara en `ProgramadorDeTareas` (no tiene dependencias): lo garantiza la restricción de configuración de FR-185 que comprueba T0095 (FR-184, FR-185; TR43)
- [X] T0264 [US8] Cotejar los tipos de comprobante sugeridos `RR`, `RB`, `MC`, `AR`, `TP` (y `EG` R1, `TB` T1) contra los 24 vigentes y sembrar los que no choquen, con sus mapeos sin cuenta, en `IntegracionContableDeRecaudoYTesoreria`; un choque se resuelve con otro código **(nuevo)** llevado a DT §2 (contabilidad.md §8; Q2)
- [X] T0265 [US8] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Accounting/{ReceiptsIntegrationEndpoints,TreasuryIntegrationEndpoints}.cs (api.md §16: reglas, plantilla, completitud, tipos de comprobante, lotes, vínculos, `bridges-reconciliation?module=REC|TES`) con `Accounting.ReceiptsMatrix.*`, `Accounting.TreasuryMatrix.*`
- [X] T0266 [US8] Crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Contabilidad/Recaudo/{Matriz,TiposDeComprobante,Completitud,Lotes}.razor (`/contabilidad/recaudo/*`) y src/Presentation/IngenIA365ERP.Shared/Pages/Contabilidad/Tesoreria/{Matriz,TiposDeComprobante,Completitud}.razor (`/contabilidad/tesoreria/*`), con su cliente, la plantilla descargable e `IndicadorDeCarga` por zona; enlaces en el menú de Contabilidad
- [X] T0267 [US10] E-01 (c): crear los contratos src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending/{ArchivoDeDesembolsosGeneradoV1,ArchivoDeDesembolsosAnuladoV1}.cs, emitirlos desde la generación y la anulación del archivo de desembolsos de Cartera (src/Core/IngenIA365ERP.Application/Lending/Integration/EmisionDeCartera.Desembolso.cs), y consumirlos en `DestinoTesoreria` con `ConsumeLendingDisbursementFileCommand` sobre `TES_LendingDisbursementFiles` y `TES_LendingDisbursementFileOrders` (entidades y configuración nuevas en Entities/Treasury/Payments/); enmienda C10 de la 013 anotada en specs/013-cartera-financiera/decisiones-transversales.md (FR-108, FR-164 c; CH2)
- [X] T0268 [US8] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `IntegracionContableDeRecaudoYTesoreria` con las siete tablas `ACC_` y los tipos de comprobante, y el par `TesoreriaNucleo` con los archivos de desembolsos de Cartera (data-model §17, §10.8)
- [ ] T0269 [US8] P12 y P8 · La contadora completa con el dueño las matrices de Recaudo y Tesorería (roles y cuentas de cada hecho) y las cuentas de faltantes, sobrantes y partidas con el plazo del sobrante antes de ingreso, los plazos de las alertas y el destino de las partidas no identificadas; se cargan desde `/contabilidad/recaudo/matriz` y `/contabilidad/tesoreria/matriz` (src/Presentation/IngenIA365ERP.Shared/Pages/Contabilidad/{Recaudo,Tesoreria}/Matriz.razor) y se registran en specs/015-recaudo-tesoreria/research.md y los parámetros `Caja.Sobrantes.DiasAntesDeIngreso` y `Partidas.*` (dueño, fecha límite 20/11; **bloquea la salida**: la completitud al 100 % es condición del go/no-go; rige mientras tanto la plantilla con filas sugeridas sin cuenta)
- [ ] T0270 [US8] P10 · El tributarista confirma los hechos generadores del GMF de los débitos a cuentas contables por clase de egreso, las exenciones de los desembolsos por operación y las de las cuentas de giro, y si el GMF de un pago se causa como gasto y por pagar o lo cobra el banco en el mismo débito (Q8); registrarlo como versiones de `Gmf.*` y en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 20/11; no bloquea: rige cuatro por mil «pendiente de validar», hechos generadores de egresos sólo en devoluciones, «gasto y por pagar»)
- [ ] T0271 Hito del bloque 3 (10/11, ambiente): **antes de desplegar, congelar** los cuatro pares de R0/T0 que existen (`PlataformaParaRecaudoYTesoreria`, `RecaudoNucleo`, `TesoreriaNucleo`, `IntegracionContableDeRecaudoYTesoreria`): última regeneración con `tools/scripts/add-migration.ps1`; revisión a mano del snapshot y del SQL de cada par (sólo las tablas de su lista de DT §2.3, índices con nombre de 63 caracteres o menos, filtros por motor, FK `Restrict`, semillas por `INSERT … WHERE NOT EXISTS`, ninguna operación sobre `TRS_` ni sobre tablas de Cartera o Captaciones); `check-migration-parity.ps1` y `dotnet ef migrations has-pending-model-changes` limpios en los dos motores; las huellas de sus archivos y sus `.Designer` en tests/IngenIA365ERP.Architecture.Tests/Principles/LasMigracionesCongeladasNoSeRegeneran.cs; commit propio. **Desde aquí no se regenera nada de R0/T0**: todo cambio es un par nuevo y aditivo (decisión del dueño del 2026-10-23; TR40, R-34). Después, desplegar en QA por GitOps la versión de R0/T0 disponible (caja, recibo, extracto, pagaduría, giro, contabilidad) con `Lending:TestDoubles:Enabled = false` en QA, sobre una cooperativa de QA con copia de la parametrización de COOFLOPAL, **activada con la activación mínima de T0183b** (no a mano); comprobar el arranque con un solo consumidor por destino y registrar el resultado en specs/015-recaudo-tesoreria/quickstart.md §5; es la versión del paralelo que empieza el 11/11 (FR-226) **ruta crítica**


**Notas de la construcción de la fase 16 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **T0248–T0252**: escritas como pruebas de Application (`Accounting/Receipts/*`, `Accounting/Treasury/MatrizDeTesoreriaTests`, `Treasury/Orders/ConsumoDelArchivoDeDesembolsosTests`,
  `Accounting/Receipts/PoliticaContableTests`), casi todas puras sobre la matriz en memoria (`MatricesDePrueba`). La conciliación de puentes (T0252) prueba el saldo
  por rol y la lectura de las dimensiones del puente; el efecto sobre `ConciliacionContableDeCartera` lo cubrirá la e2e. La prueba de Cartera de T0253
  (`ArchivoDeDesembolsosInformadoTests`) no se escribió aparte: la del consumo comprueba también que el catálogo declara los dos tipos `CAR → Treasury`; la emisión
  desde la generación y la anulación la recorre la e2e.
- **T0255 (pendiente)**: las e2e se escriben con las demás e2e de la fase 22; sin marcar.
- **T0256**: seis entidades en `Domain/Entities/Accounting/Integration` sobre tres bases abstractas (`ReglaContableDeModulo`, `MapeoDeComprobanteDeModulo`,
  `RecepcionContableDeModulo`) en vez de seis clases sueltas; cada una con su tabla. Dimensiones de la regla como columnas por código (`BranchCode`, `CollectionPointCode`,
  `CashBoxCode`, `MeansCode`, `MeansClass`, `MiscConceptCode`, `TargetModule`, `TargetNature`, `CollectorClass`, `OwnBankAccountCode`, `OrderOrigin`, `OrderKind`, `Cause`).
  Pesos: caja 256, punto 128, medio 64, concepto 64, clase de orden 64, módulo, naturaleza, clase de recaudador y origen de la orden 32 cada uno, cuenta propia 16,
  causa 8, oficina 4, hecho 2 (sólo roles de resultado), clase de medio 1 (la tabla de contabilidad.md §6 no fijaba los cinco últimos). El mapeo de tipo de
  comprobante es uno por hecho (sin la excepción por clase de recaudador de Cartera).
- **T0257**: un solo motor común en `Application/Accounting/RecaudoYTesoreria` (catálogo de roles y hechos `MatricesDeRecaudoYTesoreria`, `MatrizVigente`,
  `ConstructorDeComprobanteDeModulo`, `ConsumoDeModulo`, `ContabilizacionDeModulo`, reglas, plantilla, completitud, lotes) en vez de dos árboles
  `{Reglas,Contabilizacion,Lotes,Plantilla}` duplicados; `PostReceiptsFactsCommand`, `PostReceiptsSummaryGroupCommand` y `PostTreasuryFactsCommand` son comandos delgados en
  `Accounting/Receipts/Contabilizacion` y `Accounting/Treasury/Contabilizacion`. El consumidor lee las líneas del `PayloadJson` (todos los hechos de los dos módulos traen
  `lines` con el mismo cuerpo): no conoce cada tipo. La retroactividad se comprueba contra los últimos mil recibos contabilizados del módulo cuyas líneas casarían con la
  regla. El resumen de los recibos de caja es **un comprobante por día y punto** (no por día, punto y medio: el medio ya distingue la cuenta y se suma por cuenta y lado);
  con tercero o cruce sólo donde la cuenta los exige.
- **T0258**: `DestinoContabilidad` reparte `REC`/`TES` y suma los totales de sus lotes; `VersionesAceptadas` incluye los dos módulos. `HorarioDeLote.Modulo` reconoce los
  prefijos `REC.` y `TES.` (antes una clave `REC.RCE` se leía como de Inventario).
- **T0259**: `ContabilidadParaRecaudo` y `ContabilidadParaTesoreria` evalúan con `EvaluacionPreviaDeModulo` (roles obligatorios de cada hecho; una dimensión que la
  pregunta no trae no descarta la regla; roles por vínculo con la cuenta propia si viene) y responden el saldo de un rol con `SaldosPorRol` (sin regla, «sin dato»).
  Siguen construyéndose sin evaluador en las pruebas de las fases anteriores (responden como antes). Las preguntas de la caja y del canal banco siguen yendo sin
  dimensiones: con contabilidad iniciada y la matriz vacía, la política «Bloquear» detiene la confirmación (FR-221). `MovimientosDeCuentaBancariaAsync` ya daba
  `sourceModule` desde la fase 9.
- **Pendiente de T0250 en Tesorería**: las pantallas de Tesorería (resultados del giro, cheques, remesas) todavía no preguntan la validación previa antes de confirmar
  (`Treasury.Prevalidation.NotPostable` no se emite): un hecho sin regla se rechaza al consumirlo y queda para reproceso en la bandeja. La evaluación ya responde por
  `IContabilidadParaTesoreria.EvaluarAsync`; falta el llamado en cada comando.
  **Cerrado el 2026-10-24 (pendiente A2)**: `EmisionDeTesoreria.ValidacionPreviaAsync` (molde del paso 3 de `ConfirmacionDeRecibo`) pregunta antes de los cerrojos y de
  tocar nada, con la cuenta propia en `OwnBankAccountPublicId` para los roles por vínculo; con «Bloquear» responde `Treasury.Prevalidation.NotPostable` con
  `data.errors[] { factType, role, combination, whoFixes }`. La llaman `RegistroDeResultadosDelGiro.RegistrarAsync` (el «girada» de una orden pagada desde cuenta propia:
  `PagoDeOrdenGirado` y `GmfDePagoCausado` si grava; cubre el registro manual, el masivo, la respuesta del banco y su revisión), `IssueChecksForBatchCommand`
  (`ChequeGiradoEmitido`), `VoidIssuedCheckCommand` (`ChequeGiradoAnulado`), `ConfirmRemittanceCommand` y `RegisterBankToVaultRemittanceCommand` (`RemesaConfirmada`).
  Prueba `Treasury/Accounting/ValidacionPreviaEnPantallasDeTesoreriaTests` escrita, sin correr.
- **T0260**: `IDimensionesDeRecaudo` e `IDimensionesDeTesoreria` reemplazan los dos puertos vacíos del Setup (`DimensionesAsync(string)`, sin implementación ni usos) por uno
  que devuelve todos los valores (`DimensionDeMatrizDto`).
- **T0261**: la guarda de `PrepareReversalAsync` y el mensaje de `ReverseDocumentCommand` (`DocumentReceiptsOrTreasuryOwned`, código `Accounting.Document.ModuleOwned`).
  La conciliación de puentes de la 013 suma los hechos `REC`/`TES` sin procesar que tocan `RecaudoPorAplicar` (por clase de recaudador) y `DesembolsoPorGirar`, con el
  signo de la naturaleza del puente; el de `DevolucionPorPagar` de captaciones queda para cuando su conjunto lo exponga. Pendiente de cotejar en la e2e.
- **T0262**: `SetReceiptsAccountingPolicyCommand` y `SetTreasuryAccountingPolicyCommand` escriben por `AddParameterVersionCommand` (el único escritor) y alertan al pasar a
  «advertir»; lo común en `Common/Parameters/CambioDePoliticaContable` para que ningún módulo nombre al otro. La alerta diaria la suman `recaudo.antiguedad`
  (`PoliticaContableEnAdvertirAsync`) y `tesoreria.vigilancia`. Rutas en `AccountingPolicyEndpoints`.
- **T0263**: `recaudo.lote-del-dia` (`LoteDelDiaDeRecaudo`) crea un lote `CashSessionClose` por **sesión cerrada** con recibos en espera (reusa el disparador y el
  índice único por sesión de la 012 en vez de un disparador nuevo) y deja la marca `RCD.LoteDelDia.Terminado`; una sesión abierta alerta `Recaudo.DiaDelPuntoAbierto` y
  sus recibos entran al siguiente.
- **T0264**: los tipos `RR`, `RB`, `MC`, `AR` (REC) y `TP` (TES) cotejados contra los vigentes (ninguno choca: `MC` es una clase de documento de Inventario, no un tipo de
  comprobante) y sembrados **como los demás tipos**, por `voucher-types.json` (`VoucherTypesSeeder`, Order 60), y sus mapeos por `ReceiptsTreasuryVoucherMappingsSeeder`
  (Order 93, cruce `OT`, pendientes de validar), no en la cola de la migración. `EG` y `TB` llegan con R1 y T1.
- **T0265, T0266**: rutas bajo `/api/accounting/receipts` y `/api/accounting/treasury` (reglas, cierre de vigencia, plantilla json y Excel, completitud, tipos de comprobante,
  lotes); `bridges-reconciliation` no se expuso: la conciliación de puentes es la de Cartera (`/api/lending/accounting/*`), que ya suma el lado de Recaudo y Tesorería.
  Las pantallas de matriz, tipos de comprobante y completitud son páginas delgadas sobre tres componentes de `Components/Contabilidad` parametrizados por módulo, cada uno
  con su `IndicadorDeCarga`; los lotes, página propia (sólo Recaudo resume). `MatricesDeRecaudoYTesoreriaClient` en Web, Web.Client y MAUI. Sin probar en navegador.
- **T0267**: `ArchivoDeDesembolsosGenerado`/`…Anulado` v1 con clave `Confirmation:{archivo:N}` en los dos (las claves de evento son un conjunto cerrado y `Voiding:{x}` no
  existe); los emite `EmisionDeCartera.ArchivoDeDesembolsosAsync` en la transacción de la generación y de la anulación; Tesorería los consume con
  `ConsumeLendingDisbursementFileCommand` y, como la anulación de una orden de giro comparte la forma de clave, `DestinoTesoreria` manda esas unidades al enrutador
  `ConsumeKeyedTreasuryUnitCommand`, que decide por el tipo. `TES_LendingDisbursementFiles` y `…FileOrders` nuevas; `PaymentOrder.LendingDisbursementFileId` gana su FK.
  La tesorería de prueba los acepta también. Enmienda C10 anotada en la 013.
- **`PuenteDeDevolucion` (pendiente de las fases 10 y 11)**: el hecho nuevo `DevolucionNoGirada` v1 (`DNG`) lo emite `ResultadoDeOrdenesDeRecaudo` cuando Tesorería informa
  rechazada, anulada o con cheque no cobrado la devolución de un saldo a favor, de una pagaduría, de Cartera o de una partida: `PuenteDeDevolucion` → el rol de origen, con
  `Related` = el hecho de la devolución. La devolución del dinero retenido en custodia sigue sin reabrir la custodia (fase 15).
  **Cerrado el 2026-10-24 (pendiente A3)**: `RefundWithheldMoneyByOrderCommand` deja en la operación la orden, la liberación y su número
  (`RCD_CashOperations.RefundPaymentOrderPublicId`, `RefundReleasePublicId`, `RefundReleaseNumber`, con índice por la orden; agregadas al par `RecaudoNucleo`, sin
  regenerar los otros: sus `.Designer` y el snapshot llevan el mismo bloque, `has-pending-model-changes` «No changes» en los dos motores); y
  `ResultadoDeOrdenesDeRecaudo.InformarCustodiaAsync` con «rechazada», «anulada» o «cheque no cobrado» reabre la custodia (la operación vuelve a `Withheld`, sin
  liberación ni orden, para liberarla o devolverla otra vez) y emite `DevolucionNoGirada` de `PuenteDeDevolucion` a `CustodiaPorLista` (`ReturnsTo = ComplianceCustody`),
  con la liberación como relacionado; «girada» la deja abandonada; repetir el resultado no hace nada. Pruebas en `ListasEnLaCajaTests`, escritas, sin correr.
- **T0268**: regenerados en orden los pares `RecaudoNucleo`, `TesoreriaNucleo` (con `TES_LendingDisbursementFiles`, `…FileOrders` y la FK de
  `TES_PaymentOrders.LendingDisbursementFileId`) e `IntegracionContableDeRecaudoYTesoreria` (las seis tablas `ACC_` nuevas y `ACC_OwnBankAccountLinks`: siete);
  `check-migration-parity.ps1` en par (Admin 15, Application 66) y `has-pending-model-changes` «No changes» en los dos motores. Los nombres de FK largos los recorta EF
  con `~`, como en el resto de los pares.
- **T0269, T0270**: tareas del dueño (P12, P8, P10), no ejecutadas. **T0271**: tarea de ambiente, al final (congelar y desplegar en QA); sin marcar.

---

# Entrega R0 + T0 · Bloque 4 — Conciliación, salida en vivo y retiro de los dobles (11/11–19/11)

Hito del bloque (19/11): **R0 y T0 completas en QA**.

---

## Phase 17: User Story 25 - Conciliación bancaria mínima (Priority: P1) — entrega T0

**Goal**: por cuenta y mes, el extracto (formato del banco, plantilla genérica o digitado) cruzado automáticamente por valor,
fecha con días de holgura y referencia contra recibos de banco, remesas, pagos y cheques de Tesorería, archivos de
desembolsos de Cartera y movimientos contables de la cuenta; marca manual de lo que no cruza; los abonos sin registro pasan
a Recaudo como partidas; informe de partidas conciliatorias; cierre y reapertura; segregación del conciliador (FR-131,
FR-209, FR-216, FR-217).

**Independent Test**: cargar el extracto de un mes con 120 movimientos: 110 cruzan solos, 6 se marcan a mano y 4 quedan como
partidas conciliatorias; el informe cuadra saldo del extracto, partidas y saldo en libros; un abono sin registro aparece como
partida en Recaudo; quien aprobó pagos de la cuenta en el período no puede conciliarla.

### Tests for User Story 25

- [X] T0272 [P] [US25] Escribir los casos dorados del cruce (≈10) en tests/IngenIA365ERP.Domain.Tests/Treasury/Casos/cruce-*.json con tests/IngenIA365ERP.Domain.Tests/Treasury/CruceDeConciliacionTests.cs (FR-216): valor exacto, fecha dentro de la holgura `Conciliacion.DiasDeHolgura`, referencia, uno a uno antes que uno a varios, nada se cruza dos veces, empates sin cruce automático
- [X] T0273 [P] [US25] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Reconciliation/ConciliacionMinimaTests.cs (FR-131, FR-209, FR-216, FR-217): abrir por cuenta y mes; registros por `IRecaudoParaConciliacion`, remesas, pagos, cheques girados, archivos de desembolsos de Cartera y `MovimientosDeCuentaBancariaAsync` (lo heredado y Contabilidad con su `sourceModule`); cruce automático y manual uno a varios, deshacer en una abierta (`Treasury.Reconciliation.AlreadyMatched`); el cheque cruzado pasa a `Cashed` y la remesa a `Confirmed` con su hecho; partidas conciliatorias por clase y antigüedad (`Tesoreria.PartidaConciliatoriaAntigua`); abono sin registro → partida de Recaudo por `IPartidasDesdeConciliacion`; cargo no identificado → `Tesoreria.CargoNoIdentificado`; cierre con `Difference = 0` (`.NotSquared`) e informe; reapertura con motivo y permiso como versión nueva (`.Closed`); conciliador distinto de quien preparó o aprobó pagos de la cuenta en el período (`.SegregationConflict`)
- [X] T0273a [P] [US25] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Periods/ReaperturaYConciliacionDeTesoreriaTests.cs (E-04; TR41; FR-167, FR-216; decisión del dueño del 2026-10-23, I7): `ReopenPeriodCommand` marca `Outdated` las `TES_Reconciliations` cerradas del mes por `IConciliacionBancariaParaContabilidad` **(nuevo, DT §2.7)** y no toca `ACC_BankReconciliations`; el evento `Accounting.Period.Reopened` conserva `outdatedReconciliations` con el conteo que devuelve el puerto; una conciliación `Outdated` se vuelve a conciliar con una versión nueva; sin Tesorería activada el puerto devuelve cero
- [X] T0273b [P] [US25] Crear tests/IngenIA365ERP.Architecture.Tests/Principles/NadieEscribeLaConciliacionBancariaDeContabilidad.cs **(nuevo, DT §2.7)**: ningún tipo de `Application` ni de `API` escribe (agrega, modifica ni borra) los `DbSet` `BankReconciliations`, `BankStatementLines` ni `BankStatementColumnMaps` de `IApplicationDbContext` (tablas `ACC_BankReconciliations`, `ACC_BankStatementLines`, `ACC_BankStatementColumnMaps`); hoy el único escritor es `ReopenPeriodCommandHandler` (src/Core/IngenIA365ERP.Application/Accounting/Periods/PeriodCommands.cs), que T0278a retira (TR41)
- [ ] T0274 [P] [US25] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/ConciliacionMinimaTests.cs (US25 «Independent Test», quickstart recorrido I pasos 2–4); se corre en la fase 22

### Implementation for User Story 25

- [X] T0275 [P] [US25] Implementar el motor puro src/Core/IngenIA365ERP.Domain/Treasury/Conciliacion/CruceDeConciliacion.cs hasta T0272
- [X] T0276 [P] [US25] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Reconciliation/ `Reconciliation`, `ReconciliationMatch` (H, UK filtrados por conciliación vigente), `ReconcilingItem` y el enum `ReconciliationState` con su configuración (data-model §12.2–§12.4)
- [X] T0277 [US25] Implementar `IRecaudoParaConciliacion` y `IPartidasDesdeConciliacion` en src/Core/IngenIA365ERP.Application/Receipts/Integration/ (recibos de canal banco, remesas y partidas de una cuenta y período; crear la partida con `OriginKind = Reconciliation`) con validador y evento de auditoría (`LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`) (R-05)
- [X] T0278 [US25] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Reconciliation/ `OpenReconciliationCommand`, `AutoMatchReconciliationCommand`, `AddManualMatchCommand`, `RemoveMatchCommand`, `AddReconcilingItemCommand`, `CloseReconciliationCommand` (informe, adjunto `TreasuryReconciliationReport`), `ReopenReconciliationCommand`, con `CerrojoDeTesoreria` (nivel conciliación) y la segregación de `SegregacionDeTesoreria` (FR-131, FR-216, FR-217)
- [X] T0278a [US25] Implementar `IConciliacionBancariaParaContabilidad` (interfaz en src/Core/IngenIA365ERP.Application/Common/Integration/Accounting/IConciliacionBancariaParaContabilidad.cs; implementación en src/Core/IngenIA365ERP.Application/Treasury/Reconciliation/ConciliacionBancariaParaContabilidad.cs, con validador y evento de auditoría como pide `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`), agregar `Outdated = 4` a `ReconciliationState` (data-model §12.2, §20) y cambiar `ReopenPeriodCommandHandler` en src/Core/IngenIA365ERP.Application/Accounting/Periods/PeriodCommands.cs para que llame al puerto en vez de marcar `ACC_BankReconciliations`; registrar el puerto en DI; hasta que pasen T0273a y T0273b (E-04; FR-167, FR-216; TR41)
- [X] T0279 [US25] Crear el documento `Conciliacion` (saldo del extracto, partidas por clase y antigüedad, saldo en libros, firmas) en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeTesoreriaReport.cs y completar la sección de partidas conciliatorias antiguas de T0160 (FR-156, FR-217)
- [X] T0280 [US25] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/ReconciliationsEndpoints.cs (api.md §12) y crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Conciliacion.razor (`/tesoreria/conciliacion`) con su cliente, cruces automáticos y manuales, partidas e `IndicadorDeCarga` por zona
- [X] T0280a [US25] Anotar la enmienda E-04 ampliada (la conciliación bancaria es de Tesorería) en specs/009-contabilidad-niif/spec.md junto a FR-055 a FR-060, con el molde «*Enmienda 015 (E-04, 2026-10-23)*» de las enmiendas 012 y 013 que ya tiene, y en su plan.md: las tablas `ACC_BankReconciliations`, `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps` quedan sin uso y su retiro, si se hace, es una migración destructiva aparte con guarda, respaldo y segundo revisor (pendiente, research.md «Pendientes»); quitar de docs/manual/ cualquier receta de conciliación bancaria de Contabilidad y apuntar a la de Tesorería (TR41; CH15)
- [X] T0281 [US25] Generar el par **nuevo y aditivo** `TesoreriaConciliacionMinima` **(DT §2.3)** con `tools/scripts/add-migration.ps1 -Name TesoreriaConciliacionMinima` en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/: `TES_Reconciliations` (con `Outdated`), `TES_ReconciliationMatches`, `TES_ReconcilingItems` (data-model §12.2–§12.4, §25 #5a); `TesoreriaNucleo` está congelado desde T0271 y no se toca; revisar el SQL a mano y correr `check-migration-parity.ps1`


**Notas de la construcción de la fase 17 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **T0272, T0275**: doce casos dorados `cruce-01..12.json` y el motor puro `Domain/Treasury/Conciliacion/CruceDeConciliacion` con tres reglas en orden: el **vínculo**
  con la línea de origen (`StatementLink`: los recibos y partidas de Recaudo que nacieron de un abono lo explican si suman exacto; no estaba en la tarea, sale de que
  Recaudo ya identifica sus abonos por línea), **uno a uno** (`ValueDateReference`: valor exacto, fecha con holgura, la referencia —sin espacios, ceros a la izquierda ni
  mayúsculas— desempata; un empate en cualquiera de los dos sentidos no cruza) y **uno a varios** (`ValueDateReferenceGroup`, sólo después: por la misma referencia o
  todos los del mismo día, si suman exacto y ninguna otra línea abierta los reclama).
- **T0273, T0273a, T0273b**: escritas. `ConciliacionMinimaTests` (Application, InMemory), `ReaperturaYConciliacionDeTesoreriaTests` y la prueba de arquitectura
  `NadieEscribeLaConciliacionBancariaDeContabilidad` (ningún archivo de `Application` ni `API` nombra `.BankReconciliations`, `.BankStatementLines` ni
  `.BankStatementColumnMaps`; sólo los declara `IApplicationDbContext`). `PeriodCommandsTests` cambió su aserción: la reapertura llama al puerto y la fila de
  `ACC_BankReconciliations` queda como estaba.
- **T0274 (pendiente)**: la e2e se escribe con las demás e2e de la fase 22; sin marcar.
- **T0276**: `Reconciliation`, `ReconciliationMatch` (H, `IsLive`/`UndoneBy`/`UndoneAt` mutables; un cruce de uno a varios son varias filas con el mismo `GroupKey`) y
  `ReconcilingItem` (con `StatementLineId` e `IsVoided`), y los enums `ReconciliationState` (con `Outdated = 4`), `ReconciliationKind`, `ReconciliationRecordKind`,
  `ReconciliationMatchMode`, `ReconcilingItemKind`. Columnas que data-model no nombraba: `ClosedByUserId`, `PreviousReconciliationId`, `OutdatedAt`, `OutdatedReason`
  (en la conciliación), `RecordNumber`, `GroupKey` (en el cruce). UK de la conciliación con nombre corto `UK_TES_Reconciliations_Account_Period_Version` (el largo pasaba
  de 63). El UK del cruce es por conciliación y registro vivo; la línea admite varias filas del mismo grupo.
- **T0277**: `Receipts/Integration/RecaudoParaConciliacion` implementa los dos puertos: los registros son los recibos no anulados de la cuenta (por `ValueDate`) y las
  partidas no de puesta en marcha (por `MovementDate`), con la línea de la que nacieron (`RegistroConciliableDto.StatementLinePublicId`, campo opcional nuevo); la partida
  desde la conciliación reusa `PartidasPorIdentificar.ReconocerAsync` (hecho `PartidaPorIdentificarReconocida`) con `OriginKind = Reconciliation` y marca la línea
  identificada por el puerto de Tesorería. Valida y audita (`Receipts.UnidentifiedItem.FromReconciliation`); está en `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`.
- **T0278**: comandos en `Treasury/Reconciliation/ReconciliationCommands.cs` y lo común en `ConciliacionesDeTesoreria`. Decisiones: (1) los registros de los libros son
  los recibos y partidas de Recaudo, remesas, pagos girados (resultado `Disbursed` de órdenes no cheque del lote de la cuenta), cheques girados no anulados, archivos de
  desembolsos de Cartera y los movimientos contables **cuyo `sourceModule` no es `REC` ni `TES`** (esos ya están por su propio registro y se cruzarían dos veces);
  (2) `Difference = StatementBalance − LedgerBalance + Σ signo × valor`, con signo + para consignación y remesa en tránsito, cargo del banco y cargo no identificado, y −
  para cheque sin cobrar, abono del banco y abono no identificado; el **abono no identificado que ya se llevó a Recaudo como partida tiene signo 0** (su hecho lo pone en
  los libros) y su línea queda cruzada con la partida (regla `UnidentifiedItemFromReconciliation`); (3) el saldo del extracto sale de la última línea del mes con saldo o
  del cierre de la última carga, o lo fija quien abre; el de libros, `SaldoEnLibrosAsync` a fin de mes; (4) la segregación compara con quienes prepararon lotes de la cuenta
  con `PayDate` en el mes y quienes decidieron sus aprobaciones, en cada acción (abrir, cruzar, partida, cerrar); (5) deshacer un cruce no devuelve sus efectos (el
  cheque sigue cobrado, la remesa confirmada); (6) reabrir copia cruces y partidas vigentes a la versión nueva; (7) el cruce que confirma una remesa pregunta antes la
  validación previa de `RemesaConfirmada`. Errores nuevos: `Treasury.Reconciliation.{NotOpen, AmountMismatch, RecordNotAvailable, InvalidPeriod, ItemInvalid}`.
- **T0278a**: `IConciliacionBancariaParaContabilidad` (`Common/Integration/Accounting`) e implementación `ConciliacionBancariaParaContabilidad` (valida, audita
  `Treasury.Reconciliation.Outdated` por cada una, sin guardar); `ReopenPeriodCommandHandler` la recibe y el evento `Accounting.Period.Reopened` conserva
  `outdatedReconciliations` con su conteo.
- **T0279**: `DocumentosDeTesoreriaReport.Conciliacion` (QuestPDF) y `IDocumentosDeTesoreriaEnPdf.Conciliacion(ConciliacionModel)`; el informe se guarda como adjunto
  `TreasuryReconciliationReport` antes de cerrar la fila. La sección de partidas antiguas de `tesoreria.vigilancia` es `PartidasConciliatoriasAntiguasAsync` sobre el
  parámetro **nuevo** `Conciliacion.DiasPartidaAntigua` (TES, 30, «pendiente de validar» por el dueño), para no dejar un literal.
- **T0280**: rutas de api.md §12 en `ReconciliationsEndpoints`; pantalla `/tesoreria/conciliacion` (lista, detalle con cifras, líneas y registros por cruzar, cruces y
  partidas, cada zona con `IndicadorDeCarga`), en el menú de Tesorería y en `LasPantallasDeRecaudoYTesoreriaEstanEnElMenu`; `ConciliacionClient` en Web, Web.Client y MAUI.
  Sin probar en navegador.
- **T0280a**: enmienda E-04 anotada en specs/009-contabilidad-niif/spec.md (tras FR-060) y en su plan.md. En docs/manual no había ninguna receta de conciliación bancaria
  de Contabilidad que quitar; el manual del tesorero (T0324) la cubre.
- **T0281**: par nuevo y aditivo `TesoreriaConciliacionMinima` con las tres tablas, generado con `add-migration.ps1` (sólo `CreateTable` de `TES_Reconciliations`,
  `TES_ReconciliationMatches`, `TES_ReconcilingItems`, FK `Restrict`, índices de 61 caracteres o menos, filtro por motor del UK vivo); `check-migration-parity.ps1` en par
  (Admin 15, Application 67) y `has-pending-model-changes` «No changes» en los dos motores. `TesoreriaNucleo` no se tocó.

---

## Phase 18: User Story 13 - Puesta en marcha: saldos iniciales, pendientes y cuadre (Priority: P1) — entrega R0 y T0

**Goal**: las plantillas de puesta en marcha (todo o nada, revisión previa, error por fila y columna) de puntos y cajas,
cajeros y alcances, medios por punto, bóvedas al corte, partidas al corte, cheques en canje, cuentas bancarias con saldo,
chequeras, cheques en tránsito, remesas y consignaciones en tránsito y cuentas bancarias de personas, **sin hechos**; las
cifras de referencia; el cuadre de apertura por cuenta contra Contabilidad, definitivo o provisional (FR-157, FR-158, FR-160,
FR-228 parte del cuadre).

**Independent Test**: cargar una plantilla con un error de fila: no guarda nada y dice fila y columna; corregirla y cargar
bóvedas, cuentas con saldo, cheques en tránsito y partidas al corte sin ningún hecho contable; el cuadre contra Contabilidad
muestra la diferencia y admite un cuadre provisional con motivo y fecha límite.

### Tests for User Story 13

- [X] T0282 [P] [US13] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Imports/PlantillasDePuestaEnMarchaDeRecaudoTests.cs (archivos-y-plantillas.md §5; FR-157, FR-158): `recaudo.puntos-y-cajas`, `recaudo.cajeros-y-alcances` (aviso si el usuario no tiene segundo factor), `recaudo.medios-por-punto` (clases admitidas en la entrega), `recaudo.bovedas-al-corte` (suma = saldo declarado), `recaudo.partidas-al-corte` (fila sin fecha rechazada), `recaudo.cheques-en-canje`; todo o nada, revisión previa, `data.errors[] { row, column, code }`, `IsOpeningLoad = true` y ningún hecho; evento resumen de auditoría
- [X] T0283 [P] [US13] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Imports/PlantillasDePuestaEnMarchaDeTesoreriaTests.cs: `tesoreria.cuentas-bancarias` (vínculo contable por código por E-04, fondo de liquidez exclusivo, saldo al corte), `tesoreria.chequeras`, `tesoreria.cheques-en-transito` (chequera registrada antes), `tesoreria.remesas-y-consignaciones-en-transito`, y `plataforma.cuentas-bancarias-de-personas` (entran `PendingApproval` y las aprueba otra persona) (FR-157, FR-210)
- [X] T0284 [P] [US13] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Activation/CuadreDeAperturaTests.cs (FR-158, FR-160, FR-228): líneas por concepto y dimensión (efectivo por bóveda y caja, partidas, cheques en canje, saldos a favor; saldo por cuenta bancaria, cheques en tránsito, remesas y consignaciones en tránsito) contra `SaldoContableAsync` a la fecha de corte; `Definitive` con diferencia cero o acta aceptada; `Provisional` con `Receipts.Activation.ProvisionalSquare` / `Treasury.Activation.ProvisionalSquare`, motivo y fecha límite; el definitivo queda enlazado al provisional; cifras de referencia cargadas contra lo cargado y lo contable, sin nombrar al sistema de origen
- [ ] T0285 [P] [US13] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/PuestaEnMarchaYCuadreTests.cs (US13 «Independent Test», quickstart recorrido L pasos 1–3; SC-021); se corre en la fase 22

### Implementation for User Story 13

- [X] T0286 [P] [US13] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Activation/ `OpeningSquare`, `OpeningSquareLine`, `ReferenceFigure` y en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Activation/ sus pares `TES_`, con el enum `OpeningSquareKind`, y su configuración (data-model §15.2, §15.3)
- [X] T0287 [US13] Implementar las plantillas como `IComandoDeImportacion` sobre `EjecutorDeImportacion` en src/Core/IngenIA365ERP.Application/Receipts/Imports/ (seis), src/Core/IngenIA365ERP.Application/Treasury/Imports/ (cuatro) y src/Core/IngenIA365ERP.Application/Core/PersonBankAccounts/Imports/ (una), con encabezados en la fila 1 e instrucciones en otra hoja (FR-157)
- [X] T0288 [US13] Implementar `LoadReferenceFiguresCommand` y `ListReferenceFiguresQuery` en src/Core/IngenIA365ERP.Application/{Receipts,Treasury}/Imports/ y `RegisterProvisionalOpeningSquareCommand` / `RegisterDefinitiveOpeningSquareCommand` en src/Core/IngenIA365ERP.Application/{Receipts,Treasury}/Activation/ (FR-158, FR-160, FR-228)
- [X] T0289 [US13] Exponer `/api/receipts/imports/templates/{code}.xlsx`, `POST /api/receipts/imports/{code}?mode=preview|apply`, sus pares de `/api/treasury/imports`, `/reference-figures` y `/opening-squares/current|provisional|definitive` en src/Presentation/IngenIA365ERP.API/Endpoints/{Receipts,Treasury}/ActivationEndpoints.cs con `…Imports.Run`, `…Activation.Square` y `…Activation.ProvisionalSquare` (api.md §14)
- [X] T0290 [US13] Crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/PuestaEnMarcha.razor (`/recaudo/puesta-en-marcha`) y src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/PuestaEnMarcha.razor (`/tesoreria/puesta-en-marcha`): descarga de plantillas, revisión previa con error por fila y columna, cifras de referencia cargado/referencia/contable, cuadre provisional o definitivo; con `IndicadorDeCarga` por zona y ningún texto que nombre al sistema anterior («cifras de referencia»)
- [X] T0291 [US13] Generar el par **nuevo y aditivo** `RecaudoYTesoreriaPuestaEnMarcha` **(DT §2.3)** con `tools/scripts/add-migration.ps1 -Name RecaudoYTesoreriaPuestaEnMarcha` en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/: `RCD_OpeningSquares`, `RCD_OpeningSquareLines`, `RCD_ReferenceFigures`, `TES_OpeningSquares`, `TES_OpeningSquareLines`, `TES_ReferenceFigures` y la FK de `OpeningSquareId` en `RCD_Activations` y `TES_Activations` (data-model §15, §25 #5b); `RecaudoNucleo` y `TesoreriaNucleo` están congelados desde T0271 y no se tocan; revisar el SQL a mano y correr `check-migration-parity.ps1`
- [ ] T0292 [US13] P17 · El oficial de cumplimiento y el dueño definen qué cuentas bancarias constituyen el fondo de liquidez y el procedimiento de aviso previo a la Supersolidaria; se marcan con el propósito `LiquidityFund` en `/tesoreria/cuentas-bancarias` o por `tesoreria.cuentas-bancarias` y se registra en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 20/11; no bloquea: rige que ninguna cuenta tiene el propósito «fondo» hasta marcarla)
- [ ] T0293 [US13] P18 · Definir quién aprueba el cuadre provisional y la fecha límite del definitivo (del 02/12 al 04/12 según el plan de implantación) y asignar `Receipts.Activation.ProvisionalSquare` y `Treasury.Activation.ProvisionalSquare` a esa persona; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 28/11; no bloquea: rige que aprueba quien tenga el permiso)


**Notas de la construcción de la fase 18 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **T0282–T0284**: `Receipts/Imports/PlantillasDePuestaEnMarchaDeRecaudoTests`, `Treasury/Imports/PlantillasDePuestaEnMarchaDeTesoreriaTests` y
  `Receipts/Activation/CuadreDeAperturaTests` (Application, InMemory, con el lector de archivos sustituido como las de la matriz de Cartera), más el motor puro en
  `Domain.Tests/Common/CalculoDelCuadreDeAperturaTests`. La parte de Tesorería del cuadre tiene la misma mecánica (lo común está en `Common/Cuadres`) y la cubre la e2e.
- **T0285 (pendiente)**: la e2e se escribe con las demás e2e de la fase 22; sin marcar.
- **T0286**: las bases comunes en `Domain/Common/Cuadres/CuadreDeApertura.cs` (`CuadreDeAperturaBase`, `LineaDeCuadreDeAperturaBase` (H), `CifraDeReferenciaBase`, los enums
  `OpeningSquareKind` y `OpeningSquareState`, y el motor puro `CalculoDelCuadreDeApertura`) y seis clases concretas, `ReceiptsOpeningSquare`/`…Line`/`ReceiptsReferenceFigure`
  y sus pares `Treasury…`, como las matrices de la fase 16. Columnas que data-model no nombraba: `CutoffDate`, `TotalLoaded/Ledger/Difference`, `RegisteredBy/At`,
  `ApprovedAt` en la cabecera; `DimensionLabel` e `IsComparable` en la línea (la de cheques girados en tránsito es **informativa**: el saldo en libros ya los descuenta);
  `CutoffDate`, `LoadedAt`, `ImportBatchPublicId` y `SourceFileName` en la cifra. Para guardar lo cargado sin hechos hicieron falta columnas en tablas existentes, que van
  en el mismo par: `RCD_ReceivedChecks.IsOpeningLoad`, `TES_CashRemittances.IsOpeningLoad`, `TES_BankAccounts.OpeningBalance` y `OpeningBalanceOn` (el saldo en libros al
  corte de la cuenta), y el valor `VaultMovementKind.OpeningBalance = 7` (el saldo de la bóveda entra como movimiento y proyección por denominación).
- **T0287**: **un comando por módulo con el código de la plantilla** (`ImportReceiptsGoLiveTemplateCommand`, `ImportTreasuryGoLiveTemplateCommand`) en vez de uno por
  plantilla, sobre el mismo `EjecutorDeImportacion` (todo o nada, revisión previa que no guarda, `data.errors[] { row, column, code }`, evento resumen `Import.Reviewed` /
  `Import.Applied`); y `ImportPersonBankAccountsCommand` en `Core/PersonBankAccounts/Imports`. Lo que necesita un Id del primer guardado (las cajas de un punto nuevo, las
  denominaciones del movimiento de bóveda) corre en `despuesDeGuardar`, porque la revisión deshace el contexto pero no un `SaveChanges`. Reglas: la caja repetida, el punto
  de otra oficina, la suma de la bóveda distinta del saldo declarado (`Receipts.Imports.VaultTotalMismatch`) o una bóveda ya cargada (`.VaultAlreadyLoaded`), la partida sin
  fecha o futura, el cheque ya registrado o fuera de canje, el medio de una clase de R1 (`Import.Cell.NotYetAvailable`), la cuenta con el fondo de liquidez acompañado
  (`CuentasPropias.Propositos`) o sin código ACH, la chequera que se cruza (`Treasury.Imports.CheckbookRangeOverlaps`), el cheque en tránsito sin chequera registrada
  (`Treasury.Imports.CheckbookNotFound`). El cajero sin segundo factor sólo avisa (`Receipts.Imports.UserWithoutSecondFactor`); la cuenta de persona sin certificación
  entra `PendingApproval` con el aviso `Core.PersonBankAccount.ImportedWithoutCertificate`. Las partidas toman la cuenta propia por `ICuentasBancariasPropias` (Recaudo no lee
  `TES_`); la remesa en tránsito de puesta en marcha no tiene bóveda (`VaultPublicId = Guid.Empty`). Los números de lo cargado son `PM-…` / `PI-PM-…` / `RM-PM-…` sin
  consumir la numeración. Las reglas de cada fila son las del alta unitaria donde son funciones (`CuentasPropias.Propositos`, `MediosDeRecaudo.DeR0`,
  `ChequesRecibidos.PlazoAsync`); las demás, escritas en la plantilla.
- **T0288**: `LoadReferenceFiguresCommand` no existe aparte: las cifras de referencia son una plantilla más (`recaudo.cifras-de-referencia`,
  `tesoreria.cifras-de-referencia`; volver a cargar un concepto, dimensión y corte da de baja la anterior) y `ListReceiptsReferenceFiguresQuery` /
  `ListTreasuryReferenceFiguresQuery` las cotejan contra la misma propuesta del cuadre. El cuadre: `LineasDelCuadreDeRecaudo` (por oficina: `Boveda`, `PartidasPorIdentificar`,
  `ChequesEnCanje`, `SaldoAFavorDePagadores`; las de pasivo comparan con el signo cambiado) y `LineasDelCuadreDeTesoreria` (`Banco` por cuenta contra el vínculo,
  `ChequesGiradosEnTransito` informativa, `EfectivoEnTransito` por oficina), los dos contra `SaldoContableAsync` del rol («sin dato» si no hay regla). El registro común
  (`Common/Cuadres/RegistroDeCuadres`): **provisional** con motivo y fecha límite posterior al corte (`…Activation.ProvisionalDeadlineInvalid`), `ProvisionalApproved` a
  nombre de quien lo registra con `…Activation.ProvisionalSquare` —ésa es la aprobación (decisión del dueño)—; **definitivo** `Squared` o, con diferencia, sólo con acta
  `AcceptedWithMinutes` (sin acta, `…Activation.SquareHasDifference` con las líneas); el definitivo deja al provisional pendiente `Superseded` y enlazado
  (`DefinitiveSquareId`). Los comandos de Tesorería se llaman `RegisterTreasuryProvisionalOpeningSquareCommand` / `RegisterTreasuryDefinitiveOpeningSquareCommand` (los de
  Recaudo conservan el nombre de la tarea). La alerta diaria del provisional y la guarda del cierre del mes son de la fase 21 (T0321).
- **T0289**: rutas `GET /api/{receipts,treasury}/opening-squares/current?cutoffDate=`, `POST …/provisional`, `POST …/definitive`, `GET /api/{receipts,treasury}/reference-figures`
  y las plantillas por `RutasDePlantilla.MapPlantillaPorRuta` en `GET /api/{receipts,treasury}/imports/{code}/template.xlsx` y `POST …/imports/{code}/import?mode=review|apply`
  (api.md decía `…/imports/templates/{code}.xlsx` y `POST …/imports/{code}?mode=preview|apply`: se usó la forma común de la plataforma, la que entiende `ImportarPlantilla`).
  La de cuentas de personas va bajo `/api/treasury/imports/plataforma.cuentas-bancarias-de-personas`. En `ActivationEndpoints.cs` de cada módulo.
- **T0290**: `/recaudo/puesta-en-marcha` y `/tesoreria/puesta-en-marcha`, páginas delgadas sobre `Components/Recaudo/PuestaEnMarchaDeModulo` (plantillas con `ImportarPlantilla`,
  cifras de referencia y cuadre, cada zona con `IndicadorDeCarga`), en el menú de cada grupo; `PuestaEnMarchaClient` en Web, Web.Client y MAUI; la prueba de arquitectura
  `LasPantallasDeRecaudoYTesoreriaEstanEnElMenu` suma `Las_puestas_en_marcha_estan_en_el_menu`. El acta del definitivo se indica por el identificador de su adjunto (no hay
  todavía un dueño al que subirlo antes de registrar el cuadre). Sin probar en navegador. Ningún texto nombra al sistema anterior.
- **T0291**: par nuevo y aditivo `RecaudoYTesoreriaPuestaEnMarcha` con `add-migration.ps1`: las seis tablas, la FK de `OpeningSquareId` en `RCD_Activations` y `TES_Activations`
  (la columna ya estaba desde T0183b) y las cuatro columnas de arriba; índices de 55 caracteres o menos, FK `Restrict`. `check-migration-parity.ps1` en par (Admin 15,
  Application 68) y `has-pending-model-changes` «No changes» en los dos motores. Los pares anteriores no se tocaron.
- **T0292, T0293**: tareas del dueño (P17, P18), no ejecutadas: rige que ninguna cuenta es fondo de liquidez hasta marcarla y que aprueba el provisional quien tenga el
  permiso.
---

## Phase 19: User Story 9 - Registrarse como participantes, retirar los dobles y liberar la activación de Cartera y Captaciones (Priority: P1) — entrega R0 y T0

**Goal**: la activación de Recaudo y de Tesorería con sus requisitos verificables, que abre en el mismo guardado sus filas del
registro de participantes (`REC` recaudador de obligaciones y de cuentas y destino `Receipts`; `TES` destino `Treasury`, nunca
recaudador); el retiro de los dobles de prueba en todos los ambientes, **después** de migrar a los módulos reales las e2e de
Cartera y Captaciones y verlas en verde en los dos motores (FR-147 a FR-151, FR-159, FR-163, FR-164 e, FR-165 b y g,
FR-223, FR-224).

**Independent Test**: en una cooperativa de prueba con los dobles registrados, desplegar la versión con Recaudo y Tesorería:
la migración cierra las filas de los dobles con su motivo, el arranque encuentra un solo consumidor por destino, registrar
un doble se rechaza en todo ambiente, activar Recaudo y Tesorería registra `REC` y `TES` reales y la activación de Cartera y
de Aportes muestra en verde el recaudador y la tesorería.

### Tests for User Story 9

- [X] T0294 [P] [US9] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Activation/ActivacionDeRecaudoTests.cs (R-26, R-27; FR-147, FR-150, FR-159, FR-176): requisitos (puntos, cajas, al menos un cajero y un supervisor con los permisos de FR-176, medios, tipos de documento; con contabilidad iniciada la matriz completa de los hechos que no estén en «no pasa») con `Receipts.Activation.RequirementsMissing` y `data.missing[]`; activación desde el día siguiente al corte con el mes abierto y un cuadre (definitivo o provisional); en el mismo guardado `REC × ObligationsCollector` con `ProveedorDeObligaciones` v1 y `ProveedorDeCuentas` v1 y `REC × MessageDestination × Receipts` con sus cuatro adopciones; antes de activar los mensajes de negocio quedan pendientes en la bandeja
- [X] T0295 [P] [US9] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Activation/ActivacionDeTesoreriaTests.cs (FR-147, FR-150; D2): cuentas bancarias vinculadas, formatos o la forma de FR-213, chequera si ofrece cheques, matriz completa; `TES × MessageDestination × Treasury` con `OrdenDeGiroEmitida` v1 y v2, `AnulacionDeOrdenDeGiro` v1 y los dos del archivo de desembolsos; **sin** fila `ObligationsCollector` (la clase `ClaseDeRecaudador.Treasury` queda sin uso); versión no adoptada rechazada con su motivo (FR-149)
- [ ] T0296 [P] [US9] Escribir tests/IngenIA365ERP.Application.Tests/Common/Integration/RetiroDeLosDoblesTests.cs (R-26; FR-148, FR-223): `EscritorDelRegistroDeIntegracion` rechaza `Class = Test` o `IsTestDouble = true` en **todo** ambiente (`Integration.Participant.TestDoublesRetired`); `RequisitosDeActivacion` y `RequisitosDeActivacionDeCaptaciones` ya no admiten `Test` fuera de producción; la migración de datos es idempotente, cierra filas y adopciones sin borrar y en producción no hace nada; la fila real `TES` abre una vigencia nueva sobre la misma llave lógica (mensajes.md §8) — *espera la corrida de pruebas del final (puerta T0301)*
- [ ] T0297 [US9] Reemplazar tests/IngenIA365ERP.Architecture.Tests/Principles/LosDoblesDePruebaNoSeRegistranEnProduccion.cs por `NoHayDoblesDePrueba` (ningún tipo implementa `IDestinoDeMensajes` para `Treasury` o `Receipts` salvo `DestinoTesoreria` y `DestinoRecaudo`; no existe el espacio `Lending.Integration.DoblesDePrueba`) y quitar de `UnSoloConsumidorPorDestino` la excepción de los dobles (R-26; SC-027) — *espera la corrida de pruebas del final (puerta T0301)*
- [X] T0298 [US9] Completar `RecaudoYTesoreriaE2E.RegistrarYActivarAsync(coop)` en tests/IngenIA365ERP.API.IntegrationTests/Receipts/RecaudoYTesoreriaE2E.cs: configuración mínima (punto, caja, cajero y supervisor con segundo factor, medios efectivo y transferencia, cuenta de recaudo y de pagos vinculadas, formato genérico), cuadre provisional, activación de los dos módulos y verificación del registro; ayudantes `PagarEnCajaAsync`, `IdentificarAbonoAsync`, `GirarOrdenAsync` y `RegistrarResultadoAsync` sobre las rutas reales **ruta crítica** — *escrita y compilando, sin correr (la corre la puerta T0301)*
- [X] T0299 [US9] Migrar a los módulos reales las e2e de Cartera (FR-224): `CarteraE2E.RegistrarDoblesAsync` y `CarteraE2E.RequisitosDeActivacionAsync` (tests/IngenIA365ERP.API.IntegrationTests/Lending/CarteraE2E.cs) pasan a `RecaudoYTesoreriaE2E.RegistrarYActivarAsync`; reescribir sobre recibos, extractos, pagadurías y giros reales cada prueba que usa `RecaudadorDePrueba`, `RecaudadorDePruebaDeLibranza` o `TesoreriaDePrueba` (inventario con `grep -rl "DoblesDePrueba\|test-doubles\|RecaudadorDePrueba\|TesoreriaDePrueba" tests/`: entre otras `PagosE2E`, `DesembolsoE2E`, `LibranzaE2E`, `ContratoDeObligacionesTests`, `MigracionYActivacionDeCarteraTests`, `ActivacionDeCarteraDePruebaTests`, `DoblesDePruebaDeCarteraTests` —que se reemplaza por `ParticipantesRealesDeRecaudoYTesoreriaTests`—); anotar el inventario en la tarea **ruta crítica** — *escrita y compilando, sin correr (la corre la puerta T0301)*
- [X] T0300 [US9] Migrar a los módulos reales las e2e de Captaciones (FR-224): `CaptacionesE2E.RequisitosDeCaptacionesAsync` (tests/IngenIA365ERP.API.IntegrationTests/Lending/Deposits/CaptacionesE2E.cs) y cada prueba de `Lending/Deposits/` que consigna, retira o recibe giros por los dobles **ruta crítica** — *escrita y compilando, sin correr (la corre la puerta T0301)*
- [X] T0300a [US9] En tests/IngenIA365ERP.API.IntegrationTests/Identity/CentralIdentityApiFixture.cs (hoy `builder.UseSetting("Lending:TestDoubles:Enabled", "true")`, línea ~189) poner la bandera en `false`: con `true`, el registro transitorio de T0141 no registra `DestinoRecaudo` ni `DestinoTesoreria` y las e2e migradas de T0299 y T0300 no tendrían consumidores reales; y borrar tests/IngenIA365ERP.API.IntegrationTests/Lending/DoblesDePruebaDeCarteraTests.cs (la reemplaza `ParticipantesRealesDeRecaudoYTesoreriaTests` de T0299); **antes** de la puerta T0301 (FR-224; hallazgo I1) **ruta crítica** — *escrita y compilando, sin correr (la corre la puerta T0301)*
- [ ] T0301 [US9] **Puerta de FR-224** (excepción a «e2e al final»): con Docker, correr `dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Lending"` con `DB_PROVIDER=PostgreSql` y con `DB_PROVIDER=SqlServer` sobre las e2e migradas de T0299 y T0300 con la fixture de T0300a, todavía con el código de los dobles presente pero sin usarlo; deben quedar en verde en los dos motores **antes** de borrar ese código; anotar conteos en specs/015-recaudo-tesoreria/quickstart.md **ruta crítica** — *espera la corrida de pruebas del final (puerta T0301)*

### Implementation for User Story 9

- [X] T0302 [P] [US9] Completar `RCD_Activations` y `TES_Activations` (creadas en T0183b) para la activación completa: `OpeningSquareId` obligatorio cuando `IsMinimal = false`, `GoLiveChecklistPublicId`, `RequirementsJson` con los requisitos completos (data-model §15.1); una columna que falte va en un par nuevo y aditivo, porque `RecaudoNucleo` y `TesoreriaNucleo` están congelados desde T0271
- [X] T0303 [US9] Completar (nacieron mínimos en T0183b) `RequisitosDeActivacionDeRecaudo` y `ActivateReceiptsCommand` en src/Core/IngenIA365ERP.Application/Receipts/Activation/, y `RequisitosDeActivacionDeTesoreria` y `ActivateTreasuryCommand` en src/Core/IngenIA365ERP.Application/Treasury/Activation/: verifican los requisitos completos (cuadre definitivo o provisional, matriz completa con contabilidad iniciada, supervisor con los permisos de FR-176, chequera si ofrece cheques, la restricción de la hora de corte de FR-185), guardan la fila con `RequirementsJson` y el cuadre, pasan `RCD_Setup`/`TES_Setup` a `Active` y registran las filas del registro por `EscritorDelRegistroDeIntegracion` en el mismo guardado (mensajes.md §7) (R-26, R-27; FR-147, FR-150, FR-159) **ruta crítica**
- [X] T0304 [US9] Completar en ActivationEndpoints.cs (rutas creadas en T0183b) la respuesta de requisitos con los completos, y crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Activacion.razor y src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Activacion.razor (`/recaudo/activacion`, `/tesoreria/activacion`) con la lista de requisitos, el cuadre elegido y la lista de go/no-go a la vista, e `IndicadorDeCarga` por zona
- [X] T0305 [US9] Mostrar en las pantallas de activación de Cartera y de Captaciones (src/Presentation/IngenIA365ERP.Shared/Pages/Cartera/) el recaudador y la tesorería **reales** en sus requisitos (SC-001)
- [ ] T0306 [US9] Generar el par de datos `RetiroDeLosDoblesDePrueba` con `tools/scripts/add-migration.ps1 -Name RetiroDeLosDoblesDePrueba`: par **nuevo** (posterior al congelamiento del 10/11) con un `UPDATE` idempotente **sólo de `ValidTo`** (= fecha del despliegue) con `WHERE "IsTestDouble" = true AND "ValidTo" IS NULL` sobre `COR_IntegrationParticipants`, y el mismo cierre de sus adopciones: `IntegrationParticipant` es `IHechoInmutable` y `ValidTo` (`[EscrituraUnica]`) es su única columna escribible, como en `EscritorDelRegistroDeIntegracion.RetirarAsync`; `IsEnabled` y `Reason` no se tocan y el motivo «retirado al desplegar Recaudo y Tesorería reales» va a un evento de auditoría por fila cerrada; no borra nada; en producción no hay filas (data-model §25 #2; FR-223; hallazgo I4) **ruta crítica** — *espera la corrida de pruebas del final (puerta T0301)*
- [ ] T0307 [US9] Borrar el código de los dobles **después** de T0301 y en el mismo PR: src/Core/IngenIA365ERP.Application/Lending/Integration/DoblesDePrueba/ (entero), src/Presentation/IngenIA365ERP.API/Endpoints/Lending/TestDoublesEndpoints.cs, src/Presentation/IngenIA365ERP.Shared/Pages/Cartera/DoblesDePrueba.razor y su enlace del menú, la clave `Lending:TestDoubles:Enabled` de appsettings y de la fixture, `RecaudadorDePrueba.CodigoDeTesoreria`, el permiso `Lending.TestDoubles.Operate` (dado de baja sin borrar sus asignaciones históricas) y el registro transitorio de T0141 (los consumidores reales quedan siempre registrados); `ClavesDeRecaudador.TST` e `IntegrationParticipantClass.Test` quedan reservados sin uso (R-26; FR-148, FR-163, FR-223) **ruta crítica** — *espera la corrida de pruebas del final (puerta T0301)*
- [ ] T0308 [US9] E-01 (e) y E-02 (g), segunda parte: quitar de `RequisitosDeActivacion` y `RequisitosDeActivacionDeCaptaciones` la rama que admitía la clase `Test` fuera de producción (completa T0183), y hacer que `EscritorDelRegistroDeIntegracion` (src/Core/IngenIA365ERP.Application/Common/Integration/EscritorDelRegistroDeIntegracion.cs) rechace los dobles en todo ambiente; anotar las enmiendas en specs/013-cartera-financiera/decisiones-transversales.md y en la de la 014 (D2, decisión del 2026-10-18 enmendada) (FR-164 e, FR-165 b y g; CH1, CH3) **ruta crítica** — *espera la corrida de pruebas del final (puerta T0301)*
- [ ] T0309 [US9] Quitar `Lending:TestDoubles:Enabled` de los manifiestos de GitOps de DEV y QA y retirar o reescribir docs/operaciones/cartera-dobles-de-prueba.md como «dobles retirados»: qué hace la migración, cómo se ven las filas cerradas en `/admin/participantes-de-integracion` y cómo se registra lo real (ambiente) — *tarea de ambiente (GitOps de DEV y QA); además espera la puerta T0301*
- [ ] T0310 [US9] Verificar con `check-migration-parity.ps1` y `LasMigracionesCongeladasNoSeRegeneran` que `RetiroDeLosDoblesDePrueba` es sólo datos y que ningún par congelado en T0271 cambió (las activaciones ya están en `RecaudoNucleo` y `TesoreriaNucleo` desde T0183b); si la activación completa necesitó esquema, que vaya en un par nuevo y aditivo — *espera la corrida de pruebas del final (puerta T0301)*


**Notas de la construcción de la fase 19 (2026-10-24)**. Pruebas escritas y compiladas, sin correr. Desviaciones y decisiones; las nuevas quedan en
decisiones-transversales §2.7:

- **Regla especial de la fase (decisión del dueño)**: las pruebas se corren una sola vez al final y la puerta T0301 exige las e2e migradas en verde antes de borrar
  los dobles. Por eso **quedan sin ejecutar y sin marcar** T0301 (la puerta), T0296 (prueba de lo que hacen T0306/T0308), T0297 (`NoHayDoblesDePrueba` exige que el
  espacio de los dobles ya no exista), T0306 (el par de datos `RetiroDeLosDoblesDePrueba`, no generado), T0307 (borrado del código de los dobles), T0308 (rechazar los
  dobles en todo ambiente y quitar la rama `Test` de los requisitos de Cartera y Captaciones) y T0310; T0309 es de ambiente. **El código de los dobles sigue en el
  repositorio** y nada de lo hecho exige borrarlo para compilar. *Discrepancia de número*: el encargo nombraba «la migración de datos de los dobles (T0303)»; en esta
  lista esa migración es **T0306**; T0303 es la activación completa, que no depende de la puerta, y se hizo.
- **T0302**: sin esquema nuevo. `RCD_Activations` y `TES_Activations` ya tienen `OpeningSquareId` (con su FK desde el par `RecaudoYTesoreriaPuestaEnMarcha`),
  `GoLiveChecklistPublicId`, `RequirementsJson` e `IsMinimal` desde T0183b; la activación completa los llena. `has-pending-model-changes` «No changes» en los dos motores;
  `check-migration-parity.ps1` en par (Admin 15, Application 68). No se generó ni regeneró ningún par.
- **T0303**: `RequisitosDeActivacionDeRecaudo.EvaluarAsync(fecha, completa, corte, ct)` y `RequisitosDeActivacionDeTesoreria.EvaluarAsync(fecha, completa, corte,
  ofreceCheques, ct)`; la sobrecarga de dos argumentos sigue dando los básicos. Requisitos nuevos: `OpeningSquare` (el último cuadre `Squared`, `AcceptedWithMinutes` o
  `ProvisionalApproved` de la fecha de corte, o el más reciente si no viene), `CashierWithPermissions` y `SupervisorWithPermissions` (FR-176: `Receipts.Session.Open` +
  `Receipts.Receipt.Register` en un cajero asignado; `Receipts.Count.ApproveDifference` + `Receipts.Day.Close` + `Receipts.Voiding.Approve` en algún usuario activo, por
  `IAutoridadDeOtroAprobador`), `OpenAccountingMonth` (FR-159, el mes del día en que rige), `AccountingMatrixComplete` (con Contabilidad iniciada y fuera de «no pasa»;
  por el método nuevo `CompletitudDeLaMatrizAsync` de `IContabilidadParaRecaudo`/`IContabilidadParaTesoreria`, que responde `CompletitudContableDeModuloDto` desde
  `GetModuleCompletenessQuery`: Recaudo y Tesorería no conocen Contabilidad), `CutoffBeforeLendingCycle` (FR-185, con `ReglasDeParametrosDeRecaudo.InicioDelCicloDeCartera`,
  propiedad nueva) y, en Tesorería, `CheckbookIfChecks`. **«Chequera si ofrece cheques»**: el sistema no guarda si una cooperativa gira cheques, así que lo dice quien
  activa (`offersChecks`, se sella en la auditoría); decisión para el dueño. La activación toma el corte del cuadre si no viene, guarda `OpeningSquareId` e
  `IsMinimal = false`; la **mínima** sigue (`Minimal = true`) para el paralelo de QA y en producción responde `Receipts.Activation.MinimalNotAllowed` /
  `Treasury.Activation.MinimalNotAllowed`. Tesorería adopta ahora también `ArchivoDeDesembolsosGenerado` y `…Anulado` v1 (la activación mínima no los adoptaba: los
  consumía sin adopción). `EstadoDeActivacionDto` suma `OpeningSquarePublicId`, `OpeningSquareKind` y `CutoffDate`. `ActivacionMinimaTests` pasa a `Minimal: true`.
- **T0304**: `GET …/activation/requirements?minimal=&cutoffDate=` (+ `offersChecks` en Tesorería) y `POST …/activation { cutoffDate, minimal, offersChecks }`; pantallas
  `/recaudo/activacion` y `/tesoreria/activacion` (requisitos con su detalle, el cuadre que toma, activar), en el menú y en `LasPantallasDeRecaudoYTesoreriaEstanEnElMenu`,
  con `ActivacionDeModuloClient` en Web, Web.Client y MAUI. La lista de go/no-go «a la vista» llega con la fase 21 (T0322), que todavía no existe. Sin probar en navegador.
- **T0305**: los requisitos `ReceiptsCollectorRegistered`, `TreasuryDestinationRegistered` (Cartera) y `AccountsCollectorRegistered`, `TreasuryDestinationRegistered`
  (Captaciones) dicen si el participante es Recaudo/Tesorería real o el de prueba y, si falta, mandan a `/recaudo/activacion` o `/tesoreria/activacion` (quién lo corrige:
  Recaudo / Tesorería, con `Receipts.Activation.Activate` / `Treasury.Activation.Activate`, antes Core). La rama que admite `Test` fuera de producción **no se tocó** (T0308).
  `RequisitosDeActivacionTests` cambia su aserción del permiso que corrige.
- **T0298**: `RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, coop, fx?, corte?, minima, iniciarContabilidad)`: por módulo sin activar, la configuración mínima que le
  falte (medios `EFRYT`/`TRRYT`, punto `RYT` con su caja y el administrador de cajero —y de supervisor: tiene todos los permisos—; banco `BCORYT`, auxiliar `11100596` y
  cuenta de pagos `PAGRYT` vinculada), el cuadre provisional al corte y la activación; con Contabilidad iniciada y la matriz incompleta deja `Contabilidad.ModoDePaso =
  NoPasa` del módulo desde el día de la activación (por `AddParameterVersionCommand` como proceso: **las claves RCD y TES no tienen ruta HTTP** —la de Inventario las
  rechaza por `ModulosConRutaPropia`—; ver «Para el dueño»). Ayudantes nuevos sobre rutas reales: `PagarEnCajaAsync`, `IdentificarAbonoAsync`, `OrdenDeTesoreriaAsync`,
  `GirarOrdenAsync` (lote, aprobación por otra persona, archivo, enviado), `RegistrarResultadoAsync` (resultado manual con su soporte) y `CuentaDePagosAsync`.
- **T0299 (Cartera)** — inventario de `grep -rl "DoblesDePrueba\|test-doubles\|RecaudadorDePrueba\|TesoreriaDePrueba" tests/IngenIA365ERP.API.IntegrationTests` y de quien usaba
  `RegistrarDoblesAsync`: `CarteraE2E` (los dobles → `RecaudoYTesoreriaRealesAsync`; `OpcionesDeActivacion.RegistrarDobles` → `RegistrarRecaudoYTesoreria`),
  `ActivacionDeCarteraPerformanceTests`, `MigracionYActivacionDeCarteraTests`, `PerdidaEsperadaTests`, `DesembolsoE2E` (`ResultadoDelGiroAsync` lo da Tesorería real y
  pide la fixture: despacha la orden, aprueba la cuenta destino si quedó pendiente, gira y registra el resultado; `OrdenDeTesoreriaAsync` nuevo; `TesoreriaDePrueba`
  retirada), `DesembolsoYRecogidaTests`, `IdempotenciaDeCarteraTests` y `CausacionYLiquidacionTests` (los rechazos pasan a desembolsos **por transferencia**: Tesorería
  real no «rechaza» un cheque, lo anula o no se cobra), `LibranzaE2E` (confirmar el envío y responder van por `/api/receipts/payroll-deduction-files/{id}/sent` y
  `…/responses` + `…/emit` de Recaudo real; `ArchivoEnRecaudoAsync`, `ResponderEnRecaudoAsync` nuevos), `PagosE2E`, `ContratoDeObligacionesTests`, `CobranzaYAvisoPrevioTests`,
  `CausacionDiariaPerformanceTests`, `DocumentosE2E`, `DocumentosDeCarteraTests`, `ElReversoAnulaLosDocumentosTests`, `ConcurrenciaPagoYCausacionTests` (el recaudador
  `ClavesDeRecaudo.Recaudador`), `ContabilizacionDeCarteraPorMensajesTests` (la clase del recaudador en la matriz pasa de `Test` a `Receipts`), `UsuariosDeCartera` (el
  usuario `pruebas` con `Receipts.Receipt.Register` en vez de `Lending.TestDoubles.Operate`) y `DoblesDePruebaDeCarteraTests` (**borrada**; la reemplaza
  `ParticipantesRealesDeRecaudoYTesoreriaTests`). **Desviación (para el dueño)**: las e2e que prueban el contrato de obligaciones y el de cuentas por sí mismos
  (idempotencia por clave de negocio, simulación, reverso por clave, saldos a favor, consignaciones, retiros y traslados con la respuesta `RespuestaDelContratoDto`) no tienen
  equivalente en la caja —la caja no expone la respuesta del contrato ni deja elegir la clave—, así que se ejercitan por un **arnés del proyecto de pruebas**,
  `Receipts/RecaudoEnsayoDelContrato` (`/api/e2e/recaudo/*`, registrado sólo en `CentralIdentityApiFixture`, no público: Carter no lo descubre), que llama a los mismos
  contratos **como Recaudo real** (`REC`, clase `Receipts`, en una transacción explícita y con el usuario de la petición). No usa nada de los dobles y sobrevive a T0307; las
  claves de esas pruebas pasan de `TST:` a `REC:`. Los recorridos de punta a punta por caja, extracto, pagaduría y giro reales siguen en las colecciones de Recaudo y Tesorería.
  Riesgos que destapará la puerta: el `PagoReversadoPorCartera` de un pago del arnés llega ahora a `DestinoRecaudo`, que no encuentra un recibo y puede rechazar la entrega;
  y el registro manual del resultado exige el permiso de aprobar lotes al jefe de Cartera de las e2e.
- **T0300 (Captaciones)**: `CaptacionesE2E` (`RegistrarDoblesDeCuentasAsync` retirado → `RecaudoYTesoreriaRealesAsync`), `MigracionDeCaptacionesTests` y las e2e de
  `Lending/Deposits` que consignan, retiran o simulan (`AhorroContractualTests`, `AportesTests`, `AsambleaTests`, `CdatTests`, `ContratoDeCuentasCargaTests`,
  `MovimientosTests`, `RetiroDeAsociadoTests`) por el arnés con claves `REC:`; `ContabilidadDeCaptacionesTests` (clase `Receipts`); `PagaduriasTests` (la respuesta por
  código por Recaudo real); `AsambleaTests` y `CicloDeVidaDeCuentasTests` reciben el resultado del giro de Tesorería real.
- **T0300a**: `CentralIdentityApiFixture` con `Lending:TestDoubles:Enabled = false` y el arnés registrado; `DoblesDePruebaDeCarteraTests.cs` borrada. `CajaDeHumoTests` ya
  espera la activación completa (Cartera activa Recaudo y Tesorería reales al activarse).
- **T0294, T0295**: `Receipts/Activation/ActivacionDeRecaudoTests` y `Treasury/Activation/ActivacionDeTesoreriaTests` (Application, InMemory).

---

## Phase 20: User Story 21 - Informes de recaudo, caja y tesorería, parte R0/T0 (Priority: P2) — entrega R0 y T0

**Goal**: las vistas de R0 y T0 del centro de informes como `TablaExportable` (json, xlsx, pdf) con su permiso de ver y de
exportar y la exportación auditada; las tarjetas del Centro de Reportes que llegan a páginas existentes (FR-152 parte R0,
FR-153 parte T0, FR-154, FR-156).

**Independent Test**: el recaudo del día por medio coincide con los cierres de las sesiones y con los recibos; exportar a xlsx
exige `Receipts.Reports.Export` y deja el evento de auditoría.

### Tests for User Story 21 (parte R0/T0)

- [ ] T0311 [P] [US21] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Reports/InformesDeRecaudoR0Tests.cs (FR-152 R0): `session-close`, `day-close`, `daily-collection` (punto, caja, cajero, medio, canal, módulo y producto o línea: crédito frente a aporte), `receipts-by-cashier`, `voided-receipts`, `exceptions` (fecha valor, reclasificación, contingencia), `unidentified-items` (antigüedad), `payer-credit-balances`, `paymasters`, `returned-checks`, `contingency-pending`; con alcance; columnas ocultas con `_`; `daily-collection` cuadra con los cierres de sesión (US21 «Independent Test»)
- [ ] T0312 [P] [US21] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Reports/InformesDeTesoreriaT0Tests.cs (FR-153 T0): `orders-by-state` (antigüedad), `batches`, `rejected-and-reissue`, `recent-account-changes`, `issued-checks`, `bank-balances`, `minimal-reconciliation`; con alcance por cuenta
- [ ] T0313 [P] [US21] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/InformesDeRecaudoYTesoreriaTests.cs (exportación con permiso y auditada; SC-018); se corre en la fase 22

### Implementation for User Story 21 (parte R0/T0)

- [ ] T0314 [US21] Implementar las vistas de R0 en src/Core/IngenIA365ERP.Application/Receipts/Reports/ y las de T0 en src/Core/IngenIA365ERP.Application/Treasury/Reports/ como `TablaExportable`, con su registro de vistas (R-36; api.md §17)
- [ ] T0315 [US21] Exponer src/Presentation/IngenIA365ERP.API/Endpoints/Reports/ReceiptsReportsEndpoints.cs (`/api/reports/receipts/{vista}?format=`) y TreasuryReportsEndpoints.cs (`/api/reports/treasury/{vista}`) con `Receipts.Reports.View/Export` y `Treasury.Reports.View/Export` (`RequirePermissionWhenExporting`), auditando cada exportación (FR-154)
- [ ] T0316 [US21] Crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Informes.razor (`/recaudo/informes?vista=`) y src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Informes.razor (`/tesoreria/informes?vista=`) con `IndicadorDeCarga` por zona, y las tarjetas de Recaudo y Tesorería en el Centro de Reportes (`/reportes`), cuyos `NavigateTo` revisa `TodoEnlaceDelMenuTieneSuPagina`; llenar `LasPantallasDeRecaudoYTesoreriaEstanEnElMenu` con todas las pantallas de R0/T0

---

## Phase 21: User Story 24 - Salida en vivo de COOFLOPAL: paralelo, ensayo, go/no-go y cuadre provisional (Priority: P1) — entrega R0 y T0

**Goal**: la lista de go/no-go verificable en el sistema con las condiciones automáticas que aporta cada módulo y las manuales;
la guarda que niega el cierre contable del mes de la activación con un cuadre provisional pendiente y su alerta diaria que
escala; el informe del paralelo en QA contra las cifras de referencia; los manuales de R0 y T0 y los runbooks de salida, del
paralelo y del go/no-go (FR-225 a FR-228, FR-167 guarda).

**Independent Test**: en QA, recorrer la lista de go/no-go con una cooperativa de prueba: completitud de matriz al 100 %, un
recibo de prueba por medio, punto y concepto, un giro por forma y cuenta, segundo factor de aprobadores y supervisores; una
condición en rojo se ve y seguir exige la aprobación registrada del dueño; con un cuadre provisional pendiente, cerrar el mes
de la activación se niega.

### Tests for User Story 24

- [ ] T0317 [P] [US24] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Activation/VerificacionesDeSalidaTests.cs (R-28; FR-227): las `IVerificacionDeSalida` de Recaudo y Tesorería (completitud de las dos matrices al 100 % de lo que no esté en «no pasa», recibo de prueba por medio, punto y concepto, giro por forma y cuenta, segundo factor inscrito de cada aprobador y supervisor, roles y alcances asignados) y la de Cartera y Captaciones (sus requisitos de activación en verde); los ítems manuales (cronograma del cambio, escenarios de no-go parcial con su plan de reversa, decisión)
- [ ] T0318 [P] [US24] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Periods/GuardaDelCuadreProvisionalTests.cs (FR-228; E-04; SC-029): `PeriodCommands` consulta `IEstadoOperativoParaElCierre` y niega el cierre del mes de la activación con `Accounting.Period.OpeningSquarePending` mientras el cuadre siga provisional; alerta diaria `Recaudo.CuadreProvisionalPendiente` / `Tesoreria.CuadreProvisionalPendiente` que escala al vencer la fecha límite; el definitivo cierra la alerta y libera el cierre
- [ ] T0319 [P] [US24] Escribir las e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/CuadreProvisionalYCierreDelMesTests.cs (quickstart recorrido L paso 3–4; SC-029) y tests/IngenIA365ERP.API.IntegrationTests/Receipts/ListaDeGoNoGoTests.cs (US24 «Independent Test»); se corren en la fase 22

### Implementation for User Story 24

- [ ] T0320 [US24] Implementar las `IVerificacionDeSalida` en src/Core/IngenIA365ERP.Application/Receipts/Activation/VerificacionDeSalidaDeRecaudo.cs, src/Core/IngenIA365ERP.Application/Treasury/Activation/VerificacionDeSalidaDeTesoreria.cs y src/Core/IngenIA365ERP.Application/Lending/Activation/VerificacionDeSalidaDeCartera.cs (requisitos de Cartera y Captaciones), registradas en DI (FR-227)
- [ ] T0321 [US24] Implementar `IEstadoOperativoParaElCierre` en src/Core/IngenIA365ERP.Application/Receipts/Activation/EstadoOperativoParaElCierre.cs (cuadre provisional pendiente del mes de la activación, de Recaudo y de Tesorería) y consultarlo desde el cierre de mes en src/Core/IngenIA365ERP.Application/Accounting/Periods/ igual que hoy se consulta `CarteraEnElCierre` (`Accounting.Period.OpeningSquarePending`); completar las secciones de cuadre provisional de T0197 y T0160 con el escalamiento al vencer (FR-167, FR-228; interfaces.md §3)
- [ ] T0322 [US24] Crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Administracion/GoNoGo.razor (`/admin/go-no-go`) sobre los comandos de T0033: lista por cooperativa y salida, ítems automáticos con su evidencia, manuales con adjunto (escenarios de no-go parcial), rojos visibles y la aprobación registrada del dueño para seguir (`Platform.GoLive.Override`), con `IndicadorDeCarga` por zona (FR-227)
- [ ] T0323 [US24] Agregar la vista `parallel-comparison` **(nuevo)** en src/Core/IngenIA365ERP.Application/Receipts/Reports/ y src/Presentation/IngenIA365ERP.API/Endpoints/Reports/ReceiptsReportsEndpoints.cs (lo registrado contra las cifras de referencia por día, medio y producto, con la explicación de cada diferencia) para el informe del paralelo en QA (FR-226; SC-024)
- [ ] T0324 [P] [US24] Escribir los manuales de R0 y T0 en docs/manual/: `recaudo-manual-del-cajero.md` (caja de tres pasos, atajos, medios, elección del deudor, declaración, contingencia), `recaudo-manual-del-supervisor.md` (sesiones, arqueo, diferencias, bóveda, cierre del día, anulaciones, reversos, saldos a favor), `recaudo-manual-del-analista.md` (extractos, identificación, partidas, pagadurías, cheques devueltos) y `tesoreria-manual-del-tesorero.md` (cuentas, fondo de liquidez, órdenes, lotes, archivo, resultados, regiro, chequera, remesas, conciliación mínima), con lo que permite R0/T0 y sin nombrar al sistema anterior; enlazarlos en docs/INDICE-DOCUMENTACION.md **antes del ensayo general** (FR-225)
- [ ] T0325 [P] [US24] Escribir docs/manual/recaudo-y-tesoreria-contabilizacion.md para la contadora (hechos, roles, matriz, lotes resumidos, tipos de comprobante, puentes, lo pendiente de validar, P8, P12) y docs/manual/recaudo-cumplimiento-sarlaft.md para el oficial (parámetros, declaración, listas, señales, lo pendiente de validar, P7)
- [ ] T0326 [P] [US24] Escribir los runbooks docs/operaciones/recaudo-tesoreria-salida-en-vivo-cooflopal.md (cronograma del 23/11 al 04/12: ensayo, correcciones, respaldo por base, despliegue, go/no-go y carga definitiva del 28/11, activación de Recaudo y Tesorería y después Cartera y Aportes el 01/12, cuadre de apertura del 02/12 al 04/12, escenarios de no-go parcial con su plan de reversa), docs/operaciones/recaudo-tesoreria-paralelo-en-qa.md (copia de la parametrización de COOFLOPAL en QA, registro de recaudos del 11/11 al 27/11, informe contra cifras de referencia, producción limpia) docs/operaciones/recaudo-tesoreria-despliegue.md (respaldo por base, orden de las migraciones, GitOps de DEV, QA y producción, verificación de health, consumidores y permisos, para R0/T0 y cada entrega posterior) y docs/operaciones/recaudo-tesoreria-go-no-go.md (cada condición, cómo se verifica, quién decide, cómo se registra el seguir con un rojo) (FR-226, FR-227)
- [ ] T0327 [US24] Correr el paralelo en QA del 11/11 al 27/11 con el equipo de COOFLOPAL sobre la versión de T0271 (y las que la sigan): registrar los recaudos de cuotas, aportes y nómina del paralelo, generar `parallel-comparison` al terminar y explicar cada diferencia según docs/operaciones/recaudo-tesoreria-paralelo-en-qa.md (dueño, del 11/11 al 27/11; FR-226; SC-024)
- [ ] T0328 [US24] P15 · Escribir los escenarios de no-go parcial (qué hace la ventanilla si Cartera o Aportes no se activan el 01/12) y su plan de reversa, aprobarlos y adjuntarlos al ítem manual de la lista de go/no-go; registrar la decisión en docs/operaciones/recaudo-tesoreria-salida-en-vivo-cooflopal.md (dueño, fecha límite 20/11; **bloquea la salida**: la lista de go/no-go exige el escenario escrito)
- [ ] T0329 [US24] Hito del bloque 4 (19/11, ambiente): desplegar en QA R0 y T0 completas (con la migración de los dobles y su código borrado), comprobar en `/admin/participantes-de-integracion` las filas de los dobles cerradas con su motivo, activar Recaudo y Tesorería con la activación **completa** en una cooperativa de QA que no se activó con la mínima (la del paralelo sigue con la suya, `IsMinimal = true`) y ver en verde la activación de Cartera y Aportes (specs/015-recaudo-tesoreria/quickstart.md §2)

---

# Entrega R0 + T0 · Bloque 5 — Verificación, congelamiento y salida (20/11–01/12)

---

## Phase 22: Verificación de R0/T0, congelamiento de las migraciones y salida en vivo

**Goal**: correr **una sola vez** la e2e completa en los dos motores, medir el rendimiento, hacer el QA por rol, sellar las
huellas de las migraciones de R0/T0 posteriores al 10/11 (las cuatro primeras se congelaron en T0271), dejar en el PR la
enmienda de la constitución y la desviación III aprobada, y llevar R0 y T0 por el ensayo general, el go/no-go, producción y
la activación del 01/12.

**Independent Test**: la e2e de las colecciones «Recaudo y Tesorería e2e» y de Cartera y Captaciones pasa en PostgreSQL y SQL
Server; `LasMigracionesCongeladasNoSeRegeneran` tiene las huellas de los siete pares de R0/T0; el ensayo general recorre A–L con el
dinero, los módulos y la contabilidad cuadrados (SC-002).

- [ ] T0330 [P] Escribir las pruebas de rendimiento con `[FactDeRendimiento]` (sólo con `RUN_PERF_TESTS=1`) en tests/IngenIA365ERP.API.IntegrationTests/Receipts/Performance/: `CajaConVeinteCajerosPerformanceTests` (p95 de lo que puede pagar una persona < 2 s y de la confirmación < 3 s con 20 cajeros simultáneos sobre créditos y cuentas de las mismas personas, cero interbloqueos; SC-023, SC-032), `RespuestaDePagaduriaPerformanceTests` (5.000 líneas y su giro < 5 min; SC-010), y en …/Treasury/Performance/ `LoteDeMilOrdenesPerformanceTests` (archivo < 1 min y total al peso; SC-011) y `ConciliacionMinimaPerformanceTests` (500 movimientos, ≥ 80 % de cruce automático; SC-031)
- [ ] T0331 Correr **una sola vez** la e2e completa con Docker: `dotnet test tests/IngenIA365ERP.API.IntegrationTests` con `DB_PROVIDER=PostgreSql` y con `DB_PROVIDER=SqlServer` (colecciones «Recaudo y Tesorería e2e», Cartera, Captaciones, Inventario, Nómina, Contabilidad), con el filtro de quickstart.md §1 primero y el proyecto entero después; anotar pasan, omitidas con su motivo y fallas en specs/015-recaudo-tesoreria/quickstart.md (decisión del dueño: e2e al final) **ruta crítica**
- [ ] T0332 Corregir lo que falle en T0331 (los pares de R0/T0 están congelados: un cambio de esquema es un par nuevo y aditivo) y repetir sólo las clases de tests/IngenIA365ERP.API.IntegrationTests que fallaron en los dos motores hasta verde; cada corrección cita su prueba y su FR **ruta crítica**
- [ ] T0333 Correr el rendimiento de T0330 con `RUN_PERF_TESTS=1` en PostgreSQL y anotar las medidas contra SC-010, SC-011, SC-023, SC-031 y SC-032 en specs/015-recaudo-tesoreria/quickstart.md; lo que no cumpla vuelve a su fase con una tarea nueva
- [ ] T0334 Comprobar en tests/IngenIA365ERP.Architecture.Tests/Principles/ que las pruebas de arquitectura de R0/T0 muerden de verdad (ninguna pasa por lista vacía ni por `return` temprano): `RecaudoYTesoreriaSeHablanPorSusPuertos`, `RecaudoNoConoceContabilidad`, `TesoreriaNoConoceContabilidad`, `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas`, `RecaudoYTesoreriaNoTienenValoresLegalesFijos`, `LosComandosDeRecaudoYTesoreriaLlevanClave`, `LosCerrojosDeRecaudoYTesoreriaVanEnOrden`, `NoHayDoblesDePrueba`, `NingunProcesoAplicaPagosNiInformaResultados`, `LoDeRecaudoYTesoreriaNoSeReversaDesdeComprobantes`, `LasCantidadesYMontosDeCajaTienenSuPrecision`, `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras` y las siete ampliadas (SC-015, SC-018, SC-020, SC-027)
- [ ] T0335 QA por rol en QA (ambiente) con las siete plantillas de rol y segundo factor inscrito: cajero, supervisor, analista de recaudo, tesorero, aprobador, conciliador y oficial ven y hacen sólo lo suyo; anotar en specs/015-recaudo-tesoreria/quickstart.md
- [ ] T0336 P19 · El tributarista confirma el plazo de conservación de documentos, soportes y auditoría (diez años, Código de Comercio art. 60) y su fuente; registrarlo como versión de `Conservacion.Anios` (piso cinco) y en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite 28/11, sin fecha en la spec: se propone antes de la carga definitiva; no bloquea: rigen diez años «pendiente de validar»)
- [ ] T0337 **Sellar las huellas** de las migraciones de R0/T0 posteriores al congelamiento del 10/11 (T0271), **sin regenerar ninguna**: `TesoreriaConciliacionMinima` (T0281), `RecaudoYTesoreriaPuestaEnMarcha` (T0291), `RetiroDeLosDoblesDePrueba` (T0306) y cualquier par nuevo que haya hecho falta después; revisar a mano su SQL (sólo lo de su lista de DT §2.3, índices con nombre de 63 caracteres o menos, filtros por motor, FK `Restrict`, semillas por `INSERT … WHERE NOT EXISTS`, ninguna operación sobre `TRS_`, sobre tablas de Cartera o Captaciones ni sobre los cuatro pares congelados); `check-migration-parity.ps1` y `dotnet ef migrations has-pending-model-changes` limpios en los dos motores; agregar las huellas de sus archivos y sus `.Designer` a tests/IngenIA365ERP.Architecture.Tests/Principles/LasMigracionesCongeladasNoSeRegeneran.cs (que ya tiene las cuatro de T0271); commit propio. Desde aquí todo cambio de esquema de R0/T0, también los del ensayo (T0340), es otro par nuevo con su huella (decisión del dueño del 2026-10-23; R-34; TR40) **ruta crítica**
- [ ] T0338 Actualizar CLAUDE.md con la sección de la feature 015, R0/T0 (módulos, frontera, puertos, `ConfirmacionDeRecibo`, claves, cerrojos, extracto único, pagadurías, giros y resultados, contabilidad sin cuentas, SARLAFT básico, fondo de liquidez, dobles retirados, activación con cuadre provisional, go/no-go, lo heredado conviviendo hasta T2) y remedir la tabla «Totales» (rutas, páginas, reportes PDF, pruebas) con los comandos de su columna
- [ ] T0483 Comprobar que todo nombre **(nuevo)** de esta lista está en decisiones-transversales.md §2 (desde el 2026-10-23 están en §2.7: `PaymentBatchState`, `Lending.Payment.BankItemDateMismatch`, `AdoptContractVersionCommand`, la vista `parallel-comparison`, `IReferenciasDePago`, el runbook `docs/operaciones/recaudo-tesoreria-despliegue.md`, `428`/`Approvals.PresenceRequired`/`X-Approval-Request` y los demás) y llevar ahí los que hayan surgido durante la construcción, **antes del ensayo general**; anotar en research.md las respuestas del dueño a P1–P19 y Q1–Q10 (movida de la fase 47 a la 22 el 2026-10-23: los nombres tienen que quedar fijados antes de salir)
- [ ] T0339 Ensayo general en QA del 23/11 al 25/11 con el equipo de COOFLOPAL: recorridos A–L de specs/015-recaudo-tesoreria/quickstart.md §3 con R0 y T0 completas; cada recorrido termina con el dinero, los módulos y la contabilidad cuadrados; anotar el resultado y lo que corregir (dueño, del 23/11 al 25/11; SC-002, SC-022) **ruta crítica**
- [ ] T0340 Corregir lo que el ensayo general encontró (26/11): un cambio de esquema es una migración nueva sobre los pares congelados; repetir los recorridos afectados de specs/015-recaudo-tesoreria/quickstart.md §3 en QA **ruta crítica**
- [ ] T0340a Dejar en la descripción del PR de R0/T0 la referencia a la enmienda 2.0.1 de la constitución (prefijos `RCD_` y `TES_` en los Estándares Técnicos Adicionales, 2026-10-23, commit «docs(constitucion): prefijos RCD_ y TES_ (enmienda 2.0.1)», revisor el dueño, Jorman Copete) y comprobar que el revisor del PR la verificó (constitución, «Flujo de Desarrollo y Compuertas de Calidad»; decisión del dueño 4, CO3)
- [ ] T0340b Dejar en la descripción del PR de R0/T0 la **desviación del Principio III aprobada por el dueño el 2026-10-23**: Recaudo llama directo a los contratos de Cartera y Captaciones y a los puertos entre los dos módulos dentro de su transacción, como T101 de la 013, con cada puerto ejecutando su validador y escribiendo su evento (`LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`), con el enlace a plan.md «Complexity Tracking» (constitución: una violación deliberada exige justificación en la descripción del PR y aprobación del líder técnico; decisión del dueño 5, CO2)
- [ ] T0341 Respaldo por base (`pg_dump` de cada base de cooperativa y de la administrativa) antes de desplegar, y despliegue de R0/T0 a producción por GitOps, siguiendo docs/operaciones/recaudo-tesoreria-despliegue.md, con las seis migraciones aditivas de R0/T0 y la de datos de los dobles (que en producción no hace nada), con autorización expresa del dueño; verificar health 200, un solo consumidor por destino y los permisos sembrados (producción, 27/11) **ruta crítica**
- [ ] T0342 Go/no-go del 28/11 y carga definitiva según docs/operaciones/recaudo-tesoreria-go-no-go.md: recorrer `/admin/go-no-go` en producción, decidir (un rojo exige la aprobación registrada del dueño), comprobar que producción no tiene ningún dato del paralelo y cargar las plantillas de puesta en marcha sobre la base limpia (dueño y producción, 28/11; FR-226, FR-227; SC-024) **ruta crítica**
- [ ] T0343 Salida en vivo del 01/12 según docs/operaciones/recaudo-tesoreria-salida-en-vivo-cooflopal.md: activar Recaudo y Tesorería en COOFLOPAL (con cuadre provisional si no hay definitivo), comprobar el registro `REC` y `TES` reales y activar después Cartera y Aportes (SC-001); abrir la ventanilla (dueño y producción, 01/12) **ruta crítica**
- [ ] T0344 Cuadre definitivo de apertura del 02/12 al 04/12 contra Contabilidad (`/recaudo/puesta-en-marcha`, `/tesoreria/puesta-en-marcha`): registrar el definitivo enlazado al provisional y comprobar que la alerta diaria se cierra y el cierre de diciembre ya no se niega; anotarlo en docs/operaciones/recaudo-tesoreria-salida-en-vivo-cooflopal.md (dueño y contadora, del 02/12 al 04/12; FR-228; SC-021, SC-029)
- [ ] T0345 Medir tras la capacitación con los manuales de T0324 el porcentaje de cajeros de COOFLOPAL que completa su primer recibo multiproducto y su primer cierre sin ayuda del soporte (meta 90 %) y anotarlo en specs/015-recaudo-tesoreria/quickstart.md (dueño, primera semana de diciembre; SC-022)

---

# Entrega R1 · Recaudo completo (después del 01/12; el reporte de transacciones en producción antes del primer vencimiento de enero de 2027)

---

## Phase 23: Base de R1 — versión 2 de los contratos (E-01 a, b; E-02 a, d) y migraciones `RecaudoCompleto` y `CarteraYCaptacionesConPartidaCitada`

**Goal**: la versión 2 del contrato de obligaciones y del de cuentas con la partida bancaria citada (y, en cuentas, la línea
de pagaduría) que exime la ventana de fecha valor; la consulta de la carga de Cartera aplicada sin activar; la cancelación
con entrega por el contrato de cuentas; Recaudo adopta las v2; las dos migraciones nuevas y aditivas de R1 (las de K1 y de la
014 y las de R0/T0 están congeladas) (FR-071 parte R1, FR-164 a y b, FR-165 a y d).

**Independent Test**: una partida identificada veinte días después se aplica con la fecha valor del movimiento por la v2 y
sin mora indebida; un recaudador con sólo v1 que manda la partida recibe `Integration.Participant.VersionNotSupported`; la
activación de Cartera sigue aceptando un recaudador con sólo v1.

- [ ] T0346 [P] Escribir tests/IngenIA365ERP.Application.Tests/Lending/Payments/ContratoDeObligacionesV2Tests.cs (contratos-v2.md §1; R-29): `PagoAAplicar.BankItem`; con v1 → `Integration.Participant.VersionNotSupported`; con `ValueDate = MovementDate` exime `Pagos.DiasMaximosFechaValor` pero no el corte de la carga ni el permiso con monto del mes cerrado; distinta → `Lending.Payment.BankItemDateMismatch` **(nuevo)**; `LND_LoanOperations.BankItemPublicId` guardado y en la huella; la activación de Cartera acepta un recaudador con sólo v1
- [ ] T0347 [P] Escribir tests/IngenIA365ERP.Application.Tests/Lending/Deposits/ContratoDeCuentasV2Tests.cs (contratos-v2.md §3.1, §3.4): `ConsignacionAAplicar.BankItem` con la misma regla; exención de la consignación que cita `DeductionFileLinePublicId` (como Cartera); `RetirarAsync` con `CloseAccount = true` deja de responder `Lending.Savings.DeliveryNotAvailable` cuando el recaudador es `Receipts` real
- [ ] T0348 [P] Escribir tests/IngenIA365ERP.Application.Tests/Lending/Activation/ConsultaDeCargaSinActivarTests.cs (contratos-v2.md §2.1; FR-164 b): `IConsultaDeCargaSinActivar.PersonaAsync` informa si la persona está en una carga aplicada sin activar y sus obligaciones
- [ ] T0349 Implementar la v2 de `IContratoDeObligaciones` (`VersionDelContrato = 2`, `ContratosDeIntegracion.VersionesDelContratoDeObligaciones = [1, 2]`, `PartidaBancariaCitada`) en src/Core/IngenIA365ERP.Application/Common/Integration/Lending/ y su manejo en src/Core/IngenIA365ERP.Application/Lending/Integration/ContratoDeObligacionesDeCartera.cs; la v2 de `IContratoDeCuentas` (`VersionesDelContratoDeCuentas = [1, 2]`) en …/Lending/Integration/ContratoDeCuentasDeCartera.cs; anotar las enmiendas en specs/013-cartera-financiera/decisiones-transversales.md y en la de la 014 (FR-164 a, FR-165 a)
- [ ] T0350 Implementar `IConsultaDeCargaSinActivar` en src/Core/IngenIA365ERP.Application/Lending/Activation/ConsultaDeCargaSinActivar.cs y la cancelación con entrega (E-02 d: liquida, cobra GMF y cargos, debita todo el disponible y devuelve la autorización) en src/Core/IngenIA365ERP.Application/Lending/Deposits/ (FR-164 b, FR-165 d)
- [ ] T0351 Generar el par nuevo y aditivo `CarteraYCaptacionesConPartidaCitada` con `tools/scripts/add-migration.ps1`: `LND_LoanOperations.BankItemPublicId` y `LND_MemberAccountOperations.BankItemPublicId` (Guid nulo) con índices no únicos; las migraciones de K1, de la 014 y de R0/T0 no se tocan (`LasMigracionesCongeladasNoSeRegeneran`) (data-model §18; R-29)
- [ ] T0352 Recaudo adopta las v2: `AdoptContractVersionCommand` **(nuevo)** en src/Core/IngenIA365ERP.Application/Receipts/Activation/ que suma las adopciones `ProveedorDeObligaciones` v2 y `ProveedorDeCuentas` v2 a la fila `REC × ObligationsCollector` por `EscritorDelRegistroDeIntegracion` y actualiza `RCD_Setup.ContractVersionObligations/Accounts`; `AplicacionNoInteractiva` manda `BankItem` cuando la versión adoptada lo permite (FR-071, FR-147, FR-149)
- [ ] T0353 Generar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ la primera versión del par `RecaudoCompleto` (las diez tablas `RCD_` de R1 llegan con sus historias y se regeneran hasta T0404) y activar las rutas de R1 que respondían 404 (api.md §8) (data-model §25 #6)
- [ ] T0354 P11 · La contadora confirma las enmiendas E-01 (a) y E-02 (a) (versión 2 de los contratos para la fecha valor de las partidas tardías) y, mientras tanto, la corrección de la mora indebida por condonación con aprobación; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite antes de construir R1, sin fecha en la spec; no bloquea: rige v1 con la fecha más antigua admitida y la alerta `Recaudo.FechaValorTardia`)

---

## Phase 24: User Story 1 - Resto de medios de pago (Priority: P1) — entrega R1

**Goal**: tarjetas de débito y crédito (red, adquirente, datáfono, lote y aprobación; sin el número de tarjeta), bono de
número único y «otro», ofrecidos por punto y arqueados por su método (FR-023 parte R1, FR-024, FR-031 tarjetas, FR-041 bono).

**Independent Test**: ofrecer tarjeta de débito en un punto sin adquirente se rechaza (`CardSetupIncomplete`); con su datáfono,
pagar con tarjeta registrando lote y aprobación y arquear por lote; un bono usado dos veces se rechaza.

- [ ] T0355 [P] [US1] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Setup/MediosDeR1Tests.cs: las clases de tarjeta, bono y otro dejan de responder `Receipts.PaymentMeans.NotInDelivery` desde R1; tarjeta sin adquirente o datáfono `.CardSetupIncomplete`; captura de lote y aprobación sin número de tarjeta; bono de número único `.VoucherAlreadyUsed` (FR-023, FR-024, FR-041)
- [ ] T0356 [P] [US1] Ampliar los casos dorados del arqueo con lotes de datáfono y cotejo por referencia opcional en tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/arqueo-tarjetas-*.json (FR-031)
- [ ] T0357 [US1] Quitar la restricción de entrega R0 de `RCD_PaymentMeansCollectionPoints` (por la versión de reglas de Recaudo, no por código fijo), capturar en `UpdateCashOperationMeansCommand` red, datáfono, lote y aprobación, y emitir `ReciboEmitido` con el rol `MedioPorCobrar` para tarjetas (contabilidad.md §3, §5) en src/Core/IngenIA365ERP.Application/Receipts/
- [ ] T0358 [US1] Ampliar `/recaudo/configuracion/medios` y `/recaudo/caja` (src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/) con las clases de R1, el lote de datáfono en el arqueo de `/recaudo/sesiones`, con `IndicadorDeCarga` por zona

---

## Phase 25: User Story 3 - Recibo completo: codeudor, lo exigible en una acción, recaudo pendiente de aplicar y avisos (Priority: P1) — entrega R1

**Goal**: obligaciones como codeudor a pedido; preseleccionar todo lo exigible de todos los productos en una acción; el
recaudo pendiente de aplicar para personas de una carga aplicada sin activar, aplicado en la activación; los avisos por
correo con autorización (FR-020 parte R1, FR-037 parte R1, FR-045 correo, FR-046, FR-187).

**Independent Test**: con una persona de una carga aplicada sin activar, el ítem queda como recaudo pendiente de aplicar y se
aplica el día de la activación por una persona que elige la modalidad; `F4` preselecciona lo exigible y corre todas las
simulaciones; el recibo llega por correo sólo con la autorización del canal.

- [ ] T0359 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/ReciboCompletoR1Tests.cs (FR-037, FR-187): `ListarAsync` con `includeCoDebtor`; preselección de todo lo exigible con todas las simulaciones en una acción; quitar ítems después
- [ ] T0360 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/RecaudoPendienteDeAplicarTests.cs (FR-046; D14): sólo si el contrato responde «no habilitado» y la persona está en una carga aplicada sin activar (Captaciones lo informa; Cartera por `IConsultaDeCargaSinActivar`); por persona y producto, por valor libre; se aplica en el guardado de la activación o ese mismo día por una persona con permiso que elige la modalidad, con su fecha valor; fuera de ese caso el ítem se rechaza; hechos `RecaudoPendienteReconocido` y `RecaudoPendienteAplicado`
- [ ] T0361 [P] [US3] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Notifications/AvisosDeRecaudoTests.cs (FR-020, FR-045, FR-177): el recibo y el egreso por correo sólo con la autorización del canal (`IAutorizacionDeDatos`) y con el contenido enmascarado para pagador tercero; SMS y mensajería simulados
- [ ] T0362 [P] [US3] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/ `PendingApplication` y `PendingApplicationResolution` con su configuración (data-model §8.1)
- [ ] T0363 [US3] Implementar en src/Core/IngenIA365ERP.Application/Receipts/CashDesk/ la preselección (`PreselectPayablesCommand`), `includeCoDebtor`, el destino `PendingApplication` en `ConfirmacionDeRecibo` y `ApplyPendingApplicationCommand`; emitir los dos hechos en EmisionDeRecaudo.Recibos.cs (FR-037, FR-046, FR-187)
- [ ] T0364 [US3] Implementar los avisos por correo en src/Core/IngenIA365ERP.Application/Receipts/Notifications/ sobre la mensajería a personas de la plataforma (plantillas versionadas), con el PDF del recibo (FR-020, FR-045)
- [ ] T0365 [US3] Exponer `/api/receipts/pending-applications` (`GET/POST`, `POST /{id}/apply`) y agregar a `/recaudo/caja` el atajo `F4`, la casilla de codeudor y el envío por correo, y la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/RecaudoPendienteDeAplicar.razor con `IndicadorDeCarga` por zona
- [ ] T0366 [US3] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCompleto` con las tablas de recaudo pendiente de aplicar

---

## Phase 26: User Story 4 - Entregar dinero por la caja: retiros, saldos a favor y excedentes (Priority: P1) — entrega R1

**Goal**: el egreso de caja en efectivo: retiro de ahorro autorizado por Captaciones y entregado en una sola operación,
cancelación con entrega, devolución en efectivo de saldos a favor de Cartera y de pagadores, egresos varios por concepto
(apagados por defecto), topes de egreso, anulación del egreso sólo si el dinero vuelve, quien recibe, GMF de los egresos que
disponen de cuentas contables (FR-051 a FR-056, FR-061, FR-099, FR-101, FR-189 devolución en efectivo, FR-191 quien recibe,
FR-205 parte R1).

**Independent Test**: retirar 400.000 en efectivo de una cuenta no exenta: el egreso muestra el GMF de 1.600 que calcula
Captaciones, se entrega en una operación con la autorización y baja lo esperado de la caja; sin efectivo suficiente se
rechaza antes de pedir la autorización.

- [ ] T0367 [P] [US4] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Disbursements/EgresosDeCajaTests.cs (FR-051 a FR-056, FR-101, FR-191): retiro con `SimularAsync` → `RetirarAsync(MeansClass = Cash, Signers, Receiver)` y entrega en la misma transacción, clave `REC:{egreso:N}:{cuenta:N}`; sin efectivo suficiente se rechaza antes de pedir la autorización; aprobador presente sobre la atribución; cancelación con entrega (E-02 d); saldo a favor de Cartera con `ResolverSaldoAFavorAsync` en efectivo y de pagador con egreso; egresos varios apagados por defecto, con aprobación y tope; `DailyCashOutCap`; quien recibe contra las listas; el GMF lo muestra el egreso del retiro
- [ ] T0368 [P] [US4] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Disbursements/AnulacionDeEgresoYGmfTests.cs (FR-061, FR-099, FR-205): la anulación reversa el retiro o la resolución por su contrato y reingresa el dinero sólo si vuelve, con aprobación; el efectivo entregado cuenta para topes, umbrales, alertas y el reporte; el GMF de un egreso que dispone de una cuenta contable a favor de un tercero por los parámetros `Gmf.*` de `RCD` (`GmfDeEgresoCausado`), nunca lo que el origen ya liquidó
- [ ] T0369 [P] [US4] Ampliar los casos dorados del arqueo con egresos de caja (esperado − egresos) en tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/arqueo-egresos-*.json (R-14)
- [ ] T0370 [P] [US4] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/EgresosDeCajaTests.cs (US4 «Independent Test») para la corrida de cierre de R1
- [ ] T0371 [P] [US4] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/ `CashDisbursement` (H) y `CashDisbursementVoiding` (H) con su configuración (data-model §8.1)
- [ ] T0372 [US4] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Disbursements/ `RegisterCashDisbursementCommand` (retiro, cancelación con entrega, saldo a favor de Cartera, saldo a favor de pagador, egreso vario, devolución del dinero retenido en custodia) y `VoidCashDisbursementCommand`, con `CerrojoDeRecaudo`, `NumeradorDeRecaudo`, registro de efectivo `Out` y el GMF de `RCD` (FR-051 a FR-056, FR-061, FR-189, FR-205)
- [ ] T0373 [US4] Emitir `EgresoDeCajaEntregado`, `EgresoDeCajaAnulado` y `GmfDeEgresoCausado` en src/Core/IngenIA365ERP.Application/Receipts/Accounting/EmisionDeRecaudo.Egresos.cs, sembrar el tipo de comprobante `EG` y sus filas sugeridas de matriz (contabilidad.md §3, §8; FR-141)
- [ ] T0374 [US4] Crear el documento `Egreso` firmado en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeRecaudoReport.cs y la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Egresos.razor (`/recaudo/egresos`) con firmas, verificación de identidad, aprobación presente e `IndicadorDeCarga`; exponer `POST /api/receipts/cash-disbursements` y `POST /{id}/void` (`Receipts.Disbursements.*`)
- [ ] T0375 [US4] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCompleto` con los egresos

---

## Phase 27: User Story 6 - Identificación por reglas y consignación reportada (Priority: P1) — entrega R1

**Goal**: las reglas de identificación que proponen el destino de cada abono (referencia, documento del consignante,
consignación reportada, valor exacto de un exigible) y, si el parámetro lo permite, aplican al cargar lo de referencia exacta
con dígito válido a nombre de quien carga; la consignación reportada en ventanilla por verificar hasta el extracto; la
identificación tardía con la fecha valor del movimiento por la v2 (FR-067 parte R1, FR-069, FR-071 parte R1).

**Independent Test**: cargar un extracto con cinco abonos: tres con referencia válida quedan propuestos (o aplicados al cargar
si el parámetro lo permite), uno se identifica por el documento del consignante y uno queda como partida; ≥ 90 % de los abonos
con referencia válida quedan propuestos correctamente (SC-008).

- [ ] T0376 [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/BankItems/ReglasDeIdentificacionTests.cs (FR-067; D15; SC-008): criterios `PaymentReference`, `DepositorTaxId`, `ReportedDeposit`, `ExactDueAmount` por prioridad; la propuesta la confirma una persona; con «aplicar al cargar» y referencia exacta con dígito válido se aplica al cargar el extracto a nombre de quien carga; ninguna tarea programada aplica
- [ ] T0377 [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/BankItems/ConsignacionReportadaTests.cs (FR-069, FR-071; D11): por verificar hasta el extracto (por defecto) o aplicada de inmediato con medio «consignación» pendiente de cruzar; vencida en los días configurados alerta; la identificación tardía con v2 adoptada aplica con la fecha valor del movimiento y sin alerta de mora
- [ ] T0378 [P] [US6] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Banks/ `IdentificationRule` y `ReportedDeposit` con su configuración (data-model §8.1)
- [ ] T0379 [US6] Implementar en src/Core/IngenIA365ERP.Application/Receipts/BankItems/ el motor de propuestas por reglas, la aplicación al cargar dentro de la carga del extracto (a nombre de quien carga, por `AplicacionNoInteractiva`), `RegisterReportedDepositCommand` y su cruce con el extracto, y el uso de `BankItem` en la v2 (FR-067, FR-069, FR-071)
- [ ] T0380 [US6] Exponer `/api/receipts/identification-rules` y `/api/receipts/reported-deposits` y crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/{ReglasDeIdentificacion,ConsignacionesReportadas}.razor con `IndicadorDeCarga`; mostrar las propuestas en `/recaudo/extractos`
- [ ] T0381 [US6] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCompleto` con reglas y consignaciones reportadas

---

## Phase 28: User Story 7 - Calendario legal de pagadurías (Priority: P1) — entrega R1

**Goal**: por pagaduría lo enviado, respondido, girado y pendiente; la fecha de pago de la nómina que informa la pagaduría y la
fecha legal de giro calculada con el calendario hábil como aviso informativo; el informe de pagadurías morosas (FR-082).

**Independent Test**: registrar la fecha de pago de la nómina de una pagaduría y ver la fecha legal de giro a tres días hábiles;
sin giro a esa fecha, alerta `Recaudo.GiroDePagaduriaTardio` sin bloquear nada; el informe de morosas la lista.

- [ ] T0382 [P] [US7] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Paymasters/CalendarioDePagaduriasTests.cs (FR-082; FR-010): `Libranza.DiasHabilesGiro` (3, «pendiente de validar») con `ICalendarioHabil`; aviso informativo; alerta del giro tardío; nada se bloquea
- [ ] T0383 [US7] Crear `RCD_PaymasterCalendar` (src/Core/IngenIA365ERP.Domain/Entities/Receipts/Paymasters/PaymasterCalendarEntry.cs) y su configuración, `RegisterPaymasterPayDateCommand` en src/Core/IngenIA365ERP.Application/Receipts/Paymasters/ y la sección del giro tardío en la tarea T0197
- [ ] T0384 [US7] Mostrar el calendario por pagaduría en src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Pagadurias.razor y regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCompleto`

---

## Phase 29: User Story 14 - Reporte de transacciones y registro único de efectivo (Priority: P1) — entrega R1

**Goal**: el reporte de transacciones a la UIAF (individuales y múltiples) con la estructura como dato versionado «sin
cotejar», generar, validar, regenerar y marcar cargado; el plazo y la suspensión informados; el reporte de ausencia; y el
registro único de transacciones en efectivo con el punto de venta (E-03) (FR-096, FR-099, FR-166 parte R1, FR-204).

**Independent Test**: generar el reporte de un mes con recibos y egresos en efectivo de las dos cajas: cuadra con ellos
(SC-017); con un bloqueante queda una versión `Validated` sin archivo; regenerar deja la anterior `Superseded`; marcar cargado
con radicado cierra el período; cada generación lleva la advertencia de layout sin cotejar.

- [ ] T0385 [P] [US14] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Compliance/ReporteDeTransaccionesTests.cs (R-24; FR-096; archivos-y-plantillas.md §6): layout JSON con vigencia y `verified = false` sobre `Domain/Common/Layouts`; alcance `SoloEfectivo`/`Todas`; individuales sobre el umbral y múltiples del mes por persona; estado `Vigente`/`Suspendido` sellado e informado; reporte de ausencia si `Sarlaft.Reporte.ExigeAusencia`; bloqueantes → `Validated` sin archivo; regenerar → `Superseded`; cargado con radicado; `Recaudo.Sarlaft.LayoutSinCotejar` como advertencia, nunca en el archivo; lectura sensible `Receipts.Sarlaft.ReportRead`
- [ ] T0386 [P] [US14] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Compliance/RegistroUnicoDeEfectivoTests.cs (FR-204; E-03; TR28): los acumulados, señales y el reporte suman el efectivo del punto de venta por `IEfectivoDelPuntoDeVenta` sin copiar sus filas; los egresos en efectivo cuentan (FR-099)
- [ ] T0387 [P] [US14] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ReporteDeTransaccionesTests.cs (SC-017) para la corrida de cierre de R1
- [ ] T0388 [US14] Implementar en Comercial el puerto `IEfectivoDelPuntoDeVenta` (`PorPersonaAsync`, `PorPeriodoAsync`) en src/Core/IngenIA365ERP.Application/Inventory/Integration/EfectivoDelPuntoDeVenta.cs (E-03; contratos-v2.md §4) y anotar la enmienda en specs/012-inventario-comercial/
- [ ] T0389 [P] [US14] Crear `RCD_CashTransactionReports` y `RCD_CashTransactionReportLines` en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Compliance/ con su configuración (data-model §7.3) y el layout src/Core/IngenIA365ERP.Application/Receipts/Compliance/Layouts/uiaf-transacciones-2027-01.json embebido con `verified = false`
- [ ] T0390 [US14] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Compliance/ `GenerateCashTransactionReportCommand`, `RegenerateCashTransactionReportCommand`, `MarkCashTransactionReportUploadedCommand` (radicado y comprobante), `GetCashTransactionReportFileQuery` y el uso de `IEfectivoDelPuntoDeVenta` en `RegistroDeEfectivo` y `SenalesDeEfectivo` (FR-096, FR-204)
- [ ] T0391 [US14] Exponer `/api/receipts/compliance/cash-transaction-reports` (`POST /{id}/regenerate`, `/uploaded`, `GET /{id}/file`) con `Receipts.Sarlaft.Report` y la pestaña «Reporte de transacciones» en `/recaudo/cumplimiento` con `IndicadorDeCarga`
- [ ] T0392 [US14] Regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCompleto` con el reporte de transacciones
- [ ] T0393 [US14] P7 (estructura) · El oficial coteja el layout del reporte de transacciones con el anexo vigente y marca `verified = true` en src/Core/IngenIA365ERP.Application/Receipts/Compliance/Layouts/; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite antes del primer vencimiento de enero de 2027; no bloquea la generación: cada una lleva la advertencia)

---

## Phase 30: User Story 23 - Recaudar cuentas por cobrar comerciales (Priority: P2) — entrega R1

**Goal**: lo vendido a crédito en Comercial que no pasa a Cartera se consulta como pendiente por persona y factura (consulta de
Contabilidad por tercero y documento cruce) y se cobra en el recibo con su hecho con tercero y factura, sin cobrar más que el
saldo ni como concepto vario (FR-049, FR-166 parte R1, FR-167 parte R1).

**Independent Test**: vender a crédito un servicio, cobrarlo en caja junto con una cuota de crédito y comprobar que el hecho
lleva el tercero y la factura como documento cruce y que el saldo pendiente baja; cobrar más que el saldo se rechaza.

- [ ] T0394 [P] [US23] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/CashDesk/CuentasPorCobrarComercialesTests.cs (FR-049, FR-179): consulta de pendientes por persona y factura por `IContabilidadParaRecaudo.CuentasPorCobrarComercialesAsync`; aplicación `CommercialReceivable` con tercero y factura de cruce; no más que el saldo; nunca como concepto vario; antes de activar Cartera, el recibo es la referencia que Cartera incorpora y un informe lo entrega
- [ ] T0395 [P] [US23] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/CuentasPorCobrarComercialesTests.cs (US23 «Independent Test») para la corrida de cierre de R1
- [ ] T0396 [US23] Implementar en Contabilidad la consulta de pendientes por tercero y documento cruce (`CuentasPorCobrarComercialesAsync` en src/Core/IngenIA365ERP.Application/Accounting/Receipts/ContabilidadParaRecaudo.cs) sobre las cuentas que Contabilidad marque por cobrar (E-04; FR-167)
- [ ] T0397 [US23] Crear `RCD_CommercialReceivableApplications` (src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/) con su configuración, el destino `CommercialReceivable` en `ConfirmacionDeRecibo` y `GET /api/receipts/commercial-receivables?person=`; emitir `ReciboEmitido` con el rol `CuentasPorCobrarComerciales` y tercero y cruce (contabilidad.md §3, §5)
- [ ] T0398 [US23] Mostrar las cuentas por cobrar comerciales en el paso «qué paga» de `/recaudo/caja` y regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCompleto`

---

## Phase 31: User Story 21 - Informes y tablero de Recaudo (R1), cierre y promoción de R1 (Priority: P2) — entrega R1

**Goal**: las vistas de R1 (consolidado con Nómina y débito automático de sólo lectura, anulaciones con reemisión, pagadurías
morosas, cheques recibidos y en canje, transacciones en efectivo, carga del recaudo pendiente), el tablero de caja sin datos
personales, los manuales de R1, el congelamiento de las migraciones de R1 y su promoción (FR-081 consolidado, FR-087, FR-152
parte R1, FR-155 parte R1, FR-225 R1; E-05 consulta).

**Independent Test**: el consolidado del día suma caja, bancos, pagadurías, Nómina y débito automático sin datos personales en
el tablero; la e2e de R1 pasa en los dos motores.

- [ ] T0399 [P] [US21] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Reports/InformesDeRecaudoR1Tests.cs: `consolidated-collection`, `voidings-with-reissue`, `delinquent-paymasters`, `received-checks`, `cash-transactions`, `pending-application-load`; y el tablero `GET /api/receipts/dashboard` sin datos personales (FR-087, FR-152, FR-155)
- [ ] T0400 [US21] Implementar en Nómina la consulta de sólo lectura `IRecaudoDeNominaParaConsolidado` (src/Core/IngenIA365ERP.Application/Payroll/Integration/RecaudoDeNominaParaConsolidado.cs; contratos-v2.md §5) y la lectura del débito automático de Cartera por su consulta existente, para el consolidado (FR-081; D26)
- [ ] T0401 [US21] Implementar las vistas de R1 en src/Core/IngenIA365ERP.Application/Receipts/Reports/, el tablero en src/Core/IngenIA365ERP.Application/Receipts/Reports/TableroDeCaja.cs y la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/Tablero.razor (`/recaudo/tablero`) con `IndicadorDeCarga`
- [ ] T0402 [P] [US21] Actualizar los manuales docs/manual/recaudo-manual-del-cajero.md, recaudo-manual-del-supervisor.md y recaudo-manual-del-analista.md con lo de R1 (medios, egresos, reglas, consignación reportada, recaudo pendiente, cuentas por cobrar comerciales, reporte de transacciones) (FR-225)
- [ ] T0403 Correr en tests/IngenIA365ERP.API.IntegrationTests la e2e de R1 (clases de R1 y las de R0/T0 que tocan Recaudo) con `DB_PROVIDER=PostgreSql` y `SqlServer`; corregir hasta verde
- [ ] T0404 Congelar los pares `RecaudoCompleto` y `CarteraYCaptacionesConPartidaCitada` (última regeneración, revisión del SQL, paridad, huellas en tests/IngenIA365ERP.Architecture.Tests/Principles/LasMigracionesCongeladasNoSeRegeneran.cs, commit propio) antes de promover R1
- [ ] T0405 Promover R1 a DEV, QA y producción con respaldo por base y autorización expresa del dueño, siguiendo docs/operaciones/recaudo-tesoreria-despliegue.md; el generador del reporte de transacciones debe estar en producción **antes del primer vencimiento de enero de 2027** si la suspensión no se prorroga (producción, fecha límite primer vencimiento de enero de 2027)

---

# Entrega T1 · Tesorería completa (junto con R1, sin fecha fija)

---

## Phase 32: Base de T1 — migración `TesoreriaCompleta`

**Goal**: el par nuevo y aditivo de T1 y la activación de sus rutas (que respondían 404) (data-model §13.1, §25 #8).

**Independent Test**: el par se aplica sobre una base con R0/T0 congelados sin tocar sus tablas; las rutas de T1 dejan de
responder 404 para quien tiene su permiso.

- [ ] T0406 Generar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ la primera versión del par `TesoreriaCompleta` con `tools/scripts/add-migration.ps1` (las seis tablas `TES_` de T1 llegan con sus historias y se regeneran hasta T0424), sin tocar los pares congelados; activar las rutas de T1 de api.md §13 y sembrar el tipo de comprobante `TB` con sus filas sugeridas de matriz (contabilidad.md §8)

---

## Phase 33: User Story 19 - Traslados entre cuentas propias, cheques al cierre del mes y remesa del punto de venta (Priority: P1) — entrega T1

**Goal**: los traslados entre cuentas propias con aprobación por monto y tránsito hasta el extracto; al cierre del mes, los
cheques emitidos y no entregados reclasificados y los no cobrados más allá del plazo pasados a cuenta por pagar, nunca a
ingreso; la remesa del efectivo del punto de venta por Tesorería (FR-104, FR-166 parte T1, FR-220).

**Independent Test**: trasladar entre dos cuentas propias: queda en tránsito hasta que el extracto lo confirma, con su hecho y
aprobación; al cierre, un cheque emitido no entregado se reclasifica y uno no cobrado de 200 días pasa a cuenta por pagar al
beneficiario.

- [ ] T0407 [P] [US19] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Transfers/TrasladosEntreCuentasTests.cs (FR-104, FR-207): aprobación diferida por monto (`Treasury.Transfers.Approve`); `TrasladoEntreCuentasEnviado` y, al cruzar el extracto o confirmar con soporte, `TrasladoConfirmado`; desde una cuenta del fondo de liquidez exige uso aprobado; segregación
- [ ] T0408 [P] [US19] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Checkbooks/ChequesAlCierreDelMesTests.cs (FR-220): emitidos y no entregados reclasificados al efectivo de la cuenta con `ChequesReclasificadosAlCierre` (y su reverso al inicio del mes); no cobrados más allá de `Cheques.DiasACuentaPorPagar` a `ChequeNoCobradoACuentaPorPagar` al beneficiario, nunca a ingreso; lo ejecuta una persona
- [ ] T0409 [P] [US19] Escribir tests/IngenIA365ERP.Application.Tests/Inventory/Integration/RemesaDelPuntoDeVentaTests.cs (E-03; contratos-v2.md §4): el cierre del día del punto de venta llama a `IRemesasDeTesoreria` en su transacción
- [ ] T0410 [P] [US19] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Remittances/ `Transfer` y en Entities/Treasury/Checkbooks/ `MonthEndCheckRun` con su configuración (data-model §13.1)
- [ ] T0411 [US19] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Transfers/ `RequestTransferCommand`, `ApproveTransferCommand`, `ConfirmTransferCommand` y en …/Treasury/Checkbooks/ `RunMonthEndChecksCommand`; emitir los cuatro hechos en EmisionDeTesoreria.Traslados.cs; el cruce de traslados en la conciliación (FR-104, FR-220)
- [ ] T0412 [US19] Implementar la remesa del punto de venta desde el cierre del día de Comercial (src/Core/IngenIA365ERP.Application/Inventory/Cash/) por `IRemesasDeTesoreria`, y anotar la enmienda E-03 en specs/012-inventario-comercial/ (FR-166)
- [ ] T0413 [US19] Exponer `/api/treasury/transfers` (`POST /{id}/approve`) y `POST /api/treasury/month-end/checks`, y crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/{Traslados,CierreDeCheques}.razor con `IndicadorDeCarga`; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaCompleta`

---

## Phase 34: User Story 26 - Órdenes manuales, firmantes y tokens (Priority: P1) — entrega T1

**Goal**: las órdenes manuales sólo por conceptos de un catálogo cerrado, con soporte, dos aprobadores, tope por concepto y
beneficiario distinto de quien prepara o aprueba, con la retención en la fuente o derivadas a una causación previa; los
firmantes y tokens del portal del banco por cuenta con combinaciones por monto, vigencia, acta y aprobación (FR-107 parte T1,
FR-208, FR-211).

**Independent Test**: una orden manual a nombre de quien la prepara se rechaza; con dos aprobadores y soporte entra a la bandeja
y a un lote; un firmante que se retira de la cooperativa levanta la alerta.

- [ ] T0414 [P] [US26] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Orders/OrdenesManualesTests.cs (FR-211; FR-012): catálogo cerrado `TES_ManualOrderConcepts` con tope y retención exigida; soporte obligatorio; dos aprobadores; beneficiario distinto de quien prepara o aprueba; sin retención que corresponda se deriva a una causación previa en Contabilidad; entra a la bandeja con origen `Manual`
- [ ] T0415 [P] [US26] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/BankAccounts/FirmantesYTokensTests.cs (FR-208): firmantes y tokens por cuenta con combinaciones (firma conjunta por monto) y vigencia; los cambios exigen acta y aprobación; alerta cuando un firmante se retira de la cooperativa
- [ ] T0416 [P] [US26] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Payments/ `ManualOrderConcept` y en Entities/Treasury/BankAccounts/ `BankAccountSigner`, `BankAccountToken` con su configuración (data-model §13.1)
- [ ] T0417 [US26] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Orders/ `CreateManualPaymentOrderCommand` y `ApproveManualPaymentOrderCommand`, el CRUD del catálogo, y en …/Treasury/BankAccounts/ los comandos de firmantes y tokens con su alerta (FR-208, FR-211)
- [ ] T0418 [US26] Exponer `/api/treasury/manual-orders`, `/manual-order-concepts`, `/bank-accounts/{id}/signers|tokens` y crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/{OrdenesManuales,Firmantes}.razor con `IndicadorDeCarga`; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaCompleta`

---

## Phase 35: User Story 21 - Comprobante de pago, aviso al cierre contable e informes de Tesorería (T1), cierre y promoción de T1 (Priority: P2) — entrega T1

**Goal**: el comprobante de pago numerado por pago con soporte al beneficiario por correo con autorización; el aviso de
Contabilidad al cerrar un mes con conciliaciones abiertas o sesiones de caja sin cerrar; las vistas de T1; los manuales de
T1; el congelamiento y la promoción de T1 (FR-020 parte T1, FR-117, FR-132, FR-153 parte T1, FR-225 T1).

**Independent Test**: girar una orden emite su comprobante de pago y lo envía al beneficiario sólo con la autorización del
canal; cerrar un mes con una conciliación abierta avisa (informativo) y, con `Contabilidad.CierreExigeConciliaciones`, bloquea.

- [ ] T0419 [P] [US11] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Results/ComprobanteDePagoTests.cs (FR-020, FR-117): `TES_PaymentVouchers` numerado por pago, documento con código de verificación, envío al beneficiario por correo sólo con autorización
- [ ] T0420 [P] [US25] Escribir tests/IngenIA365ERP.Application.Tests/Accounting/Periods/AvisoDeCierreContableTests.cs (FR-132; D29): `IEstadoOperativoParaElCierre` informa conciliaciones abiertas y sesiones sin cerrar (`Accounting.Period.ReconciliationsOpen`, `.CashSessionsOpen`); aviso por defecto, bloqueante con `Contabilidad.CierreExigeConciliaciones`
- [ ] T0421 [US11] Crear `TES_PaymentVouchers` (src/Core/IngenIA365ERP.Domain/Entities/Treasury/Payments/PaymentVoucher.cs), su emisión al registrar «girada», el documento `ComprobanteDePago` en src/Presentation/IngenIA365ERP.API/Reports/DocumentosDeTesoreriaReport.cs y `GET /api/treasury/payment-vouchers/{id}/document`, `POST /{id}/send` (FR-117)
- [ ] T0422 [US25] Completar src/Core/IngenIA365ERP.Application/Receipts/Activation/EstadoOperativoParaElCierre.cs y src/Core/IngenIA365ERP.Application/Accounting/Periods/PeriodCommands.cs con conciliaciones abiertas y sesiones sin cerrar y el parámetro `Contabilidad.CierreExigeConciliaciones` (`ACC`) en el cierre de mes (FR-132; E-04)
- [ ] T0423 [US21] Implementar las vistas de T1 `payment-vouchers`, `transfers`, `month-end-checks` en src/Core/IngenIA365ERP.Application/Treasury/Reports/ y actualizar docs/manual/tesoreria-manual-del-tesorero.md con lo de T1 (FR-153, FR-225)
- [ ] T0424 Correr en tests/IngenIA365ERP.API.IntegrationTests la e2e de T1 en los dos motores y congelar el par `TesoreriaCompleta` (huellas en tests/IngenIA365ERP.Architecture.Tests/Principles/LasMigracionesCongeladasNoSeRegeneran.cs, commit propio)
- [ ] T0425 Promover T1 a DEV, QA y producción con respaldo por base y autorización expresa del dueño, siguiendo docs/operaciones/recaudo-tesoreria-despliegue.md (producción)

---

# Entrega R2 · Recaudo ampliado

---

## Phase 36: Base de R2 — migración `RecaudoCanales`

**Goal**: el par nuevo y aditivo de R2 y la activación de sus rutas (data-model §8.2, §25 #9).

**Independent Test**: el par se aplica sin tocar los pares congelados; las rutas de R2 dejan de responder 404.

- [ ] T0426 Generar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ la primera versión del par `RecaudoCanales` (las nueve tablas `RCD_` de R2 llegan con sus historias y se regeneran hasta T0438) y activar las rutas de R2 de api.md §8; habilitar los ámbitos `CollectionAgreementFile = 8` y `PaymentGatewayFile = 10` para la lectura

---

## Phase 37: User Story 15 - Canje completo de cheques recibidos (Priority: P1) — entrega R2

**Goal**: el canje completo por la cámara de compensación: el cheque en la remesa del día pasa por consignado, en canje y
pagado por el extracto o devuelto, sin registro manual (FR-084 parte R2, FR-087).

**Independent Test**: tres cheques consignados en la remesa: dos se marcan pagados al cruzar el extracto y uno se devuelve con
su reverso por porción.

- [ ] T0427 [P] [US15] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Checks/CanjeCompletoTests.cs (FR-084): lote de canje `RCD_ClearingBatches` por remesa; pagado por el cruce del extracto; devolución por la misma vía de FR-060
- [ ] T0428 [US15] Crear `RCD_ClearingBatches` (src/Core/IngenIA365ERP.Domain/Entities/Receipts/Operations/ClearingBatch.cs) con su configuración e implementar en src/Core/IngenIA365ERP.Application/Receipts/Checks/ el lote de canje y la marca de pagado desde el extracto; vista `received-checks` con el canje; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCanales`

---

## Phase 38: User Story 16 - Convenios de recaudo y canales externos (Priority: P2) — entrega R2

**Goal**: los convenios con bancos y corresponsales (cuenta recaudadora, referencia con longitud y dígito, formato del archivo
diario, comisión informativa); la referencia de pago estable por producto; la carga del archivo del convenio y de pasarelas
con identificación por referencia y aplicación al cargar a nombre de quien carga, línea por línea; la conciliación del
convenio neta de comisión, IVA y retenciones; el pago de una aseguradora por el canal banco (FR-088 a FR-093, FR-157 parte
R2, FR-200, FR-218 parte R2).

**Independent Test**: configurar un convenio con referencia «número de obligación + dígito», cargar un archivo con diez pagos
(uno con dígito inválido): nueve se aplican al cargar con un recibo por pago y el otro queda como partida; la conciliación del
convenio explica la comisión con su hecho.

- [ ] T0429 [P] [US16] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Agreements/ConveniosDeRecaudoTests.cs (FR-088 a FR-092, FR-197): convenio versionado con referencia por producto o por persona y dígito configurable; referencia de pago estable; archivo del convenio y de pasarela con su huella (no se carga dos veces), validación de estructura y totales, identificación por referencia y aplicación al cargar a nombre de quien carga con un recibo por pago (canal `Agreement`/`Online`), no identificados a partidas; corresponsales como convenios; sin botón de pago en línea (C2 = A)
- [ ] T0430 [P] [US16] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/Agreements/ConciliacionDelConvenioTests.cs (FR-093, FR-218): lo informado contra lo abonado neto de comisión, IVA y cada retención con su tercero; diferencia explicada con `ComisionDeConvenioRegistrada` y, sobre la tolerancia, aprobación
- [ ] T0431 [P] [US6] Escribir tests/IngenIA365ERP.Application.Tests/Receipts/BankItems/PagoDeAseguradoraTests.cs (FR-200): el pago de una aseguradora (reclamación de un seguro de crédito) entra por el canal banco con la aseguradora como pagadora y se aplica por el contrato de obligaciones
- [ ] T0432 [P] [US16] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Receipts/ConvenioDeRecaudoTests.cs (US16 «Independent Test») para la corrida de cierre de R2
- [ ] T0433 [P] [US16] Crear en src/Core/IngenIA365ERP.Domain/Entities/Receipts/Agreements/ `CollectionAgreement`, `CollectionAgreementVersion`, `PaymentReference`, `AgreementFile`, `AgreementFileLine`, `AgreementReconciliation`, `GatewayFile`, `GatewayFileLine` con su configuración (data-model §8.2)
- [ ] T0434 [US16] Implementar en src/Core/IngenIA365ERP.Application/Receipts/Agreements/ los comandos de convenios, referencias, carga de archivos de convenio y de pasarela (sobre `AplicacionNoInteractiva`), conciliación del convenio, la plantilla `recaudo.convenios` y el pagador aseguradora en `IdentifyBankItemCommand`; emitir `ComisionDeConvenioRegistrada` (contabilidad.md §3) (FR-088 a FR-093, FR-157, FR-200)
- [ ] T0435 [US16] Exponer `/api/receipts/collection-agreements` (`…/files`, `…/reconciliations`), `/payment-references` y `/gateway-files`, y crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Recaudo/{Convenios,ArchivosDeConvenio}.razor con `IndicadorDeCarga`; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `RecaudoCanales`
- [ ] T0436 [US16] Ofrecer la referencia de pago estable a los documentos al asociado de Cartera (extracto, plan de pagos) por una consulta de Recaudo (src/Core/IngenIA365ERP.Application/Common/Integration/Receipts/IReferenciasDePago.cs **(nuevo)**), sin que Cartera lea `RCD_` (FR-090; R-05)

---

## Phase 39: User Story 21 - Informes de canales (R2), cierre y promoción de R2 (Priority: P2) — entrega R2

**Goal**: las vistas `channels` y `agreements`, los manuales de R2, el congelamiento y la promoción (FR-152 parte R2, FR-225 R2).

**Independent Test**: la vista de canales cuadra con los recibos por canal; la e2e de R2 pasa en los dos motores.

- [ ] T0437 [P] [US21] Implementar las vistas `channels` y `agreements` en src/Core/IngenIA365ERP.Application/Receipts/Reports/ con su prueba en tests/IngenIA365ERP.Application.Tests/Receipts/Reports/InformesDeRecaudoR2Tests.cs, y actualizar docs/manual/recaudo-manual-del-analista.md con convenios, pasarelas y canje (FR-152, FR-225)
- [ ] T0438 Correr en tests/IngenIA365ERP.API.IntegrationTests la e2e de R2 en los dos motores y congelar el par `RecaudoCanales` (huellas en tests/IngenIA365ERP.Architecture.Tests/Principles/LasMigracionesCongeladasNoSeRegeneran.cs, commit propio)
- [ ] T0439 Promover R2 a DEV, QA y producción con respaldo por base y autorización expresa del dueño, siguiendo docs/operaciones/recaudo-tesoreria-despliegue.md (producción)

---

# Entrega T2 · Tesorería ampliada

---

## Phase 40: Base de T2 — migración `TesoreriaAmpliada`

**Goal**: el par nuevo y aditivo de T2, la activación de sus rutas y del ámbito `AcquirerSettlement = 11` (data-model §13.2,
§25 #10).

**Independent Test**: el par se aplica sin tocar los pares congelados; las rutas de T2 dejan de responder 404.

- [ ] T0440 Generar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ la primera versión del par `TesoreriaAmpliada` (las ocho tablas `TES_` de T2 llegan con sus historias y se regeneran hasta T0466) y activar las rutas de T2 de api.md §13
- [ ] T0441 Crear `TES_LegacyPendingLoads` (src/Core/IngenIA365ERP.Domain/Entities/Treasury/Activation/LegacyPendingLoad.cs) con su configuración dentro del par `TesoreriaAmpliada` antes de congelarlo, para cargar después los pendientes del módulo heredado (data-model §13.2; FR-161)
- [ ] T0442 P5 · Confirmar que los pagos a proveedores y de cuentas por pagar entran en T2 y que, mientras tanto, siguen en la Tesorería heredada y en Contabilidad (FR-209); registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite antes de construir T2, sin fecha en la spec; no bloquea R0/T0)

---

## Phase 41: User Story 17 - Conciliación bancaria completa con el extracto (Priority: P2) — entrega T2

**Goal**: la conciliación completa por cuenta y mes con extractos de varios bancos, emparejamiento automático por reglas
configurables y manual uno a varios, partidas conciliatorias con notas bancarias registradas como hechos con aprobación, las
dispersiones de Nómina y los archivos de Cartera; el GMF real como nota bancaria; comisiones y retenciones desagregadas; la
cuenta bancaria con su banco como tercero para la exógena (FR-100 parte T2, FR-128 a FR-130, FR-168, FR-216 parte T2,
FR-218 parte T2, FR-222).

**Independent Test**: conciliar un mes con 200 movimientos: 190 automáticos, 6 manuales, 2 notas de GMF y 2 partidas; las notas
quedan como hechos aprobados y el informe cuadra; ≥ 85 % emparejado sola y cierre en menos de una hora (SC-016).

- [ ] T0443 [P] [US17] Ampliar los casos dorados del cruce con reglas configurables (referencia, valor, fecha con holgura por regla) en tests/IngenIA365ERP.Domain.Tests/Treasury/Casos/cruce-reglas-*.json (FR-129)
- [ ] T0444 [P] [US17] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Reconciliation/ConciliacionCompletaTests.cs (FR-128 a FR-130, FR-218): extractos de varios bancos; dispersiones de Nómina (`DispersionDeNominaGenerada` v1); notas bancarias (GMF, comisión con IVA, intereses en bruto con retención, impuestos) con aprobación y `NotaBancariaRegistrada`; abonos no identificados a partidas de Recaudo; nada se empareja dos veces
- [ ] T0445 [P] [US17] Escribir tests/IngenIA365ERP.Application.Tests/Payroll/Integration/DispersionInformadaATesoreriaTests.cs (E-05; FR-168): Nómina emite `DispersionDeNominaGenerada` v1 (`NOM → Treasury`) al generar su archivo y sigue pagando con él (D27); Tesorería lo consume como compromiso y para conciliar
- [ ] T0446 [P] [US17] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/ConciliacionCompletaTests.cs (US17 «Independent Test») y `ConciliacionCompletaPerformanceTests` (2.000 movimientos; SC-016) para la corrida de cierre de T2
- [ ] T0447 [P] [US17] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Reconciliation/ `BankNote` y `ReconciliationRule` con su configuración (data-model §13.2)
- [ ] T0448 [US17] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Reconciliation/ la conciliación `Complete`, las reglas, `RegisterBankNoteCommand` con aprobación, el consumo de `DispersionDeNominaGenerada` en `DestinoTesoreria` y su emisión desde Nómina (src/Core/IngenIA365ERP.Application/Payroll/Dispersion/), y el GMF real del extracto como nota; emitir `NotaBancariaRegistrada` en EmisionDeTesoreria.Notas.cs (FR-100, FR-128 a FR-130, FR-168)
- [ ] T0449 [US17] Exponer `/api/treasury/bank-notes` y `/reconciliation-rules`, ampliar `/tesoreria/conciliacion` con reglas, notas y varios bancos, con `IndicadorDeCarga`; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaAmpliada`
- [ ] T0450 [US17] Exógena: comprobar que cada cuenta bancaria propia lleva su banco como tercero (`BankThirdPartyPersonId`) y que Contabilidad obtiene por ella los saldos por cuenta al 31 de diciembre y los pagos y retenciones por tercero para el formato 1012 y los de pagos; prueba en tests/IngenIA365ERP.Application.Tests/Accounting/Treasury/ExogenaDeCuentasBancariasTests.cs (FR-222)

---

## Phase 42: User Story 18 - Pagos a proveedores y de cuentas por pagar (Priority: P2) — entrega T2

**Goal**: las facturas de proveedor confirmadas por pagar en Comercial llegan como órdenes y, al pagarse, Comercial recibe el
pago; las cuentas por pagar contables se consultan por tercero y documento cruce y se convierten en órdenes con pago parcial;
programación por vencimiento; desde T2, FR-001 rige para los pagos a proveedores (FR-001 parte T2, FR-107 parte T2, FR-124
a FR-127, FR-166 parte T2, FR-167 parte T2, FR-209 fin).

**Independent Test**: pagar en un lote dos facturas de proveedor y una cuenta por pagar contable, comprobar el pago informado a
Comercial y el hecho con tercero y documento cruce; una factura anulada en Comercial sale del lote no enviado.

- [ ] T0451 [P] [US18] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Payables/PagosAProveedoresTests.cs (FR-124, FR-126): consumo de `FacturaDeProveedorPorPagar` v1 y `AnulacionDeFacturaDeProveedor` v1 (`INV → Treasury`); orden con vencimiento y neto; al girar, `PagoDeFacturaDeProveedor` v1 (`TES → Inventory`); la anulada sale del lote no enviado; programación por vencimiento
- [ ] T0452 [P] [US18] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Payables/CuentasPorPagarContablesTests.cs (FR-125, FR-127): consulta por `IContabilidadParaTesoreria.CuentasPorPagarAsync`; conversión en órdenes con pago parcial; el pago lleva tercero y cruce (`PagoDeCuentaPorPagarGirado`); Tesorería no registra facturas ni causa gastos
- [ ] T0453 [P] [US18] Escribir la e2e tests/IngenIA365ERP.API.IntegrationTests/Treasury/PagosAProveedoresTests.cs (US18 «Independent Test») para la corrida de cierre de T2
- [ ] T0454 [US18] Emitir desde Comercial `FacturaDeProveedorPorPagar` y su anulación y consumir `PagoDeFacturaDeProveedor` (src/Core/IngenIA365ERP.Application/Inventory/Purchasing/), anotando la enmienda E-03 en specs/012-inventario-comercial/ (FR-166)
- [ ] T0455 [US18] Implementar en Contabilidad `CuentasPorPagarAsync` sobre las cuentas que marque por pagar (E-04; FR-167) y en src/Core/IngenIA365ERP.Application/Treasury/Payables/ la conversión a órdenes (`SupplierInvoice`, `AccountingPayable`), el pago parcial y la programación; emitir `PagoDeProveedorGirado` y `PagoDeCuentaPorPagarGirado` en EmisionDeTesoreria.Pagos.cs (contabilidad.md §4)
- [ ] T0456 [US18] Exponer `/api/treasury/payables` y crear la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/CuentasPorPagar.razor con el calendario de pagos e `IndicadorDeCarga`; actualizar FR-209 en la documentación: desde T2 los pagos a proveedores van por Tesorería (FR-001)

---

## Phase 43: User Story 20 - Posición de liquidez y flujo de caja (Priority: P3) — entrega T2

**Goal**: la posición diaria (bancos, bóvedas y cajas; compromisos; recaudos esperados de Cartera y Captaciones; sin un
componente si su consulta no responde); el flujo proyectado por semanas y meses con categorías y real contra proyectado (que
reemplaza al flujo heredado); el saldo del fondo de liquidez informado a Captaciones; el tablero de liquidez (FR-137 a
FR-139, FR-155 parte T2, FR-164 d, FR-165 e y f).

**Independent Test**: con saldos y compromisos conocidos, la posición del día coincide al peso; sin respuesta de la consulta de
Cartera se muestra sin ese componente y con el aviso.

- [ ] T0457 [P] [US20] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Liquidity/PosicionYFlujoTests.cs (FR-137, FR-138): componentes de la posición, compromisos (lotes aprobados, órdenes en bandeja, cheques en tránsito, dispersiones), recaudos esperados por `IRecaudosEsperadosDeCartera` e `IRecaudosEsperadosDeCaptaciones` (sin respuesta, sin el componente y con aviso); flujo con categorías configurables y real contra proyectado
- [ ] T0458 [P] [US20] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/LiquidityFund/SaldoDelFondoDeLiquidezTests.cs (FR-139; E-02 e): `SaldoDelFondoDeLiquidez` v1 (`TES → Lending`) con las cuentas del fondo y su saldo a la fecha, originado por la persona que concilia o el tesorero
- [ ] T0459 [US20] Implementar en Cartera y Captaciones `IRecaudosEsperadosDeCartera` e `IRecaudosEsperadosDeCaptaciones` (src/Core/IngenIA365ERP.Application/Lending/Integration/) sin datos personales, y anotar las enmiendas E-01 (d) y E-02 (e, f) en sus specs (FR-164 d, FR-165 e y f)
- [ ] T0460 [P] [US20] Crear en src/Core/IngenIA365ERP.Domain/Entities/Treasury/Liquidity/ `CashFlowCategory`, `CashFlowProjection`, `LiquiditySnapshot` con su configuración (data-model §13.2)
- [ ] T0461 [US20] Implementar en src/Core/IngenIA365ERP.Application/Treasury/Liquidity/ la posición diaria, el flujo proyectado, la emisión de `SaldoDelFondoDeLiquidez` y el tablero sin datos personales (`/api/treasury/dashboard`); exponer `/api/treasury/liquidity` y `/cash-flow` y crear las pantallas src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/{Liquidez,FlujoDeCaja,Tablero}.razor con `IndicadorDeCarga`; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaAmpliada` (FR-137 a FR-139, FR-155)

---

## Phase 44: User Story 22 - Conciliación de adquirentes de tarjetas (Priority: P3) — entrega T2

**Goal**: lo cobrado por lote de datáfono en Recaudo y en el punto de venta contra lo abonado por la red, con comisión, IVA y
retenciones como hecho y diferencias con aprobación (FR-133, FR-166 parte T2, FR-218 parte T2).

**Independent Test**: conciliar un día con dos lotes y un abono con comisión y retención: el hecho desagrega comisión, IVA y
retención con su tercero y la diferencia sobre la tolerancia exige aprobación.

- [ ] T0462 [P] [US22] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Reconciliation/ConciliacionDeAdquirentesTests.cs (FR-133, FR-218): lotes de datáfono de Recaudo y del punto de venta por `ILotesDeDatafonoDelPuntoDeVenta`; archivo de la red en `AcquirerSettlement = 11`; `ConciliacionDeAdquirenteRegistrada` con comisión, IVA y retenciones con su tercero; diferencias con aprobación
- [ ] T0463 [US22] Implementar en Comercial `ILotesDeDatafonoDelPuntoDeVenta` (src/Core/IngenIA365ERP.Application/Inventory/Integration/) y anotar la enmienda E-03 (FR-166)
- [ ] T0464 [US22] Crear `TES_AcquirerSettlements` y `TES_AcquirerSettlementLines` (src/Core/IngenIA365ERP.Domain/Entities/Treasury/Reconciliation/) e implementar en src/Core/IngenIA365ERP.Application/Treasury/Reconciliation/Acquirers/ la carga y la conciliación con su hecho; exponer `/api/treasury/acquirer-settlements` y la pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Adquirentes.razor con `IndicadorDeCarga`; regenerar en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/ el par `TesoreriaAmpliada`

---

## Phase 45: User Story 21 - Informes de Tesorería (T2), cierre y promoción de T2 (Priority: P2) — entrega T2

**Goal**: las vistas `reconciliations`, `liquidity`, `cash-flow`, los manuales de T2, el congelamiento y la promoción
(FR-153 parte T2, FR-225 T2).

**Independent Test**: las vistas cuadran con sus pantallas; la e2e de T2 pasa en los dos motores.

- [ ] T0465 [P] [US21] Implementar las vistas de T2 en src/Core/IngenIA365ERP.Application/Treasury/Reports/ con su prueba en tests/IngenIA365ERP.Application.Tests/Treasury/Reports/InformesDeTesoreriaT2Tests.cs y actualizar docs/manual/tesoreria-manual-del-tesorero.md con lo de T2 (FR-153, FR-225)
- [ ] T0466 Correr en tests/IngenIA365ERP.API.IntegrationTests la e2e de T2 en los dos motores (incluida `ConciliacionCompletaPerformanceTests` con `RUN_PERF_TESTS=1`, SC-016) y congelar el par `TesoreriaAmpliada` (huellas en tests/IngenIA365ERP.Architecture.Tests/Principles/LasMigracionesCongeladasNoSeRegeneran.cs, commit propio)
- [ ] T0467 Promover T2 a DEV, QA y producción con respaldo por base y autorización expresa del dueño, siguiendo docs/operaciones/recaudo-tesoreria-despliegue.md (producción)

---

# Después de T2

---

## Phase 46: Retiro de la Tesorería heredada — destructivo con guarda, en commit propio

**Goal**: retirar entero el módulo heredado de Tesorería (cheques, conceptos, facturas y flujo de caja: `TRS_Checks`,
`TRS_Concepts`, `TRS_Invoices`, sus entidades, handlers, rutas y pantallas) cuando T2 ya trae pagos a proveedores y
conciliación completa, con diagnóstico, exportación para archivo, migración de sus pendientes a Tesorería, guarda que se
niega mientras queden filas sin migrar salvo el marcador, respaldo previo, segundo revisor (el dueño) y migración declarada
irreversible (FR-161; C3 = A; data-model §24; Principio XII).

**Independent Test**: en una base con filas heredadas sin migrar, la migración se niega; con los pendientes cargados por
`tesoreria.pendientes-heredados`, la exportación hecha y el marcador `TES.RetiroHeredado.Aprobado`, borra las tres tablas;
`LaTesoreriaHeredadaNoVuelve` falla si reaparece cualquiera de sus nombres; la tarjeta débito heredada no se toca.

- [ ] T0468 P9 · Decidir si lo heredado de tarjeta débito y convenios con redes (`Domain/Entities/Debit`, `/tarjeta-debito/*`) se retira con la guarda junto con lo de Tesorería; registrarlo en specs/015-recaudo-tesoreria/research.md (dueño, fecha límite antes de este retiro, sin fecha en la spec; rige D32: no se toca en esta feature)
- [ ] T0469 Escribir specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql (en los dos motores): por base, filas de `TRS_Checks`, `TRS_Concepts` y `TRS_Invoices` y lista de pendientes (cheques sin cobrar, facturas por pagar); correrlo en cada base de DEV, QA y producción en sólo lectura y anotar el resultado en la cabecera de la migración (ambiente y producción, sólo lectura)
- [ ] T0470 [P] Escribir tests/IngenIA365ERP.Application.Tests/Treasury/Imports/PendientesHeredadosTests.cs: la plantilla `tesoreria.pendientes-heredados` carga a `TES_LegacyPendingLoads` los cheques sin cobrar (como cheques en tránsito de T0) y las facturas por pagar (como órdenes por pagar de T2), cotejados con las cifras de referencia, todo o nada (archivos-y-plantillas.md §5)
- [ ] T0471 [P] Escribir tests/IngenIA365ERP.API.IntegrationTests/Treasury/RetiroDeLaTesoreriaHeredadaTests.cs: la guarda se niega con filas sin migrar y sin el marcador; con el marcador y todo migrado, borra; la migración no tiene `Down` (lanza)
- [ ] T0472 Implementar sobre `TES_LegacyPendingLoads` (creada en la base de T2, T0441) la plantilla `tesoreria.pendientes-heredados` y la exportación para archivo (xlsx y csv con huella como adjunto no borrable) de toda fila existente en src/Core/IngenIA365ERP.Application/Treasury/Imports/ (FR-161)
- [ ] T0473 Correr en cada ambiente con filas la exportación y la carga de pendientes, cotejarlas con las cifras de referencia y registrar el resultado en docs/operaciones/tesoreria-retiro-heredado.md (ambiente y producción, con autorización del dueño)
- [ ] T0474 Generar el par destructivo `RetiroDeLaTesoreriaHeredada` con `tools/scripts/add-migration.ps1`: guarda que cuenta filas no migradas y se niega salvo la fila `COR_SystemSettings` `TES.RetiroHeredado.Aprobado`, marcador `MIGRACION-DESTRUCTIVA-APROBADA` con el resultado de T0469 en la cabecera, `DROP` de las tres tablas, `Down` declarado irreversible; `check-migration-parity.ps1` limpio
- [ ] T0475 Borrar sin alias las entidades `Check`, `TreasuryConcept`, `TreasuryInvoice` de la raíz de src/Core/IngenIA365ERP.Domain/Entities/Treasury/, sus handlers de src/Core/IngenIA365ERP.Application/Treasury/{Checks,Invoices,TreasuryConcepts}/, sus configuraciones, las rutas `/api/treasury/{checks,concepts,invoices,cash-flow}`, las pantallas `/tesoreria/{cheques,conceptos,facturas,flujo-caja}`, el grupo «Tesorería · heredada» del menú, la exención de `LasPantallasDicenQueEstanCargando` y la marca «E3 (feature 009)»; los conceptos varios de Recaudo (FR-027) quedan como reemplazo de los conceptos de tesorería
- [ ] T0476 Llenar tests/IngenIA365ERP.Architecture.Tests/Principles/LaTesoreriaHeredadaNoVuelve.cs con los nombres retirados (tres tablas, tres entidades, cuatro rutas, cuatro pantallas) sobre el modelo EF, los dos snapshots, `Endpoints/` y `Pages/`
- [ ] T0477 Escribir docs/operaciones/tesoreria-retiro-heredado.md (diagnóstico, exportación, carga de pendientes, marcador, respaldo, segundo revisor, aplicación por ambiente, verificación) y hacer el **commit propio** del retiro (migración destructiva, borrado del código y prueba), sin mezclarlo con otro cambio
- [ ] T0478 Segundo revisor: el dueño revisa el commit del retiro, specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql con su resultado y la carga de pendientes, y aprueba por escrito en docs/operaciones/tesoreria-retiro-heredado.md (dueño, antes de aplicar en cualquier ambiente con filas)
- [ ] T0479 Respaldo por base (`pg_dump`) y aplicación del retiro en DEV, QA y producción por GitOps con autorización expresa del dueño; verificar que las bases quedan sin las tablas `TRS_` y que nada más cambió, siguiendo docs/operaciones/tesoreria-retiro-heredado.md (producción)

---

## Phase 47: Polish & Cross-Cutting Concerns

**Goal**: dejar la documentación, las pruebas transversales y las cifras del repositorio al día con todas las entregas.

**Independent Test**: `dotnet build IngenIA365ERP.slnx` con 0 errores, todas las pruebas sin contenedores verdes,
`TextosDeRecaudoYTesoreriaTests` y `PrincipioIX_NoEmptyCatch` verdes sobre las pantallas nuevas, y la tabla «Totales» de
CLAUDE.md remedida.

- [ ] T0480 [P] Crear los índices de recetas docs/manual/recaudo.md y docs/manual/tesoreria.md (molde `docs/manual/cartera.md`) y enlazar en docs/INDICE-DOCUMENTACION.md los manuales, los runbooks y specs/015-recaudo-tesoreria/
- [ ] T0481 [P] Correr `TextosDeRecaudoYTesoreriaTests`, `PrincipioIX_NoEmptyCatch` (también sobre los `.razor`), `LasPantallasNoRamificanPorElTextoDeUnError` y `TodoEnlaceDelMenuTieneSuPagina` sobre todo lo nuevo de R0 a T2 y corregir lo que aparezca (ningún texto dice «SOLIDO»; ninguna pantalla ramifica por texto)
- [ ] T0482 [P] Escribir tests/IngenIA365ERP.Shared.Tests/Recaudo/ClaveDeOperacionTests.cs y tests/IngenIA365ERP.Shared.Tests/Recaudo/CajaEstadoTests.cs (la clave estable al reconfirmar tras `DistributionChanged` o una aprobación; los atajos y el paso de la caja) y tests/IngenIA365ERP.Shared.Tests/Tesoreria/LotesEstadoTests.cs
- [ ] T0484 Re-evaluar la constitución (plan.md «Re-evaluación de la constitución tras el diseño») contra lo construido y anotar en plan.md cualquier desviación nueva
- [ ] T0485 Actualizar CLAUDE.md al cierre de cada entrega (R1, T1, R2, T2, retiro) con su sección y remedir la tabla «Totales» (rutas, páginas, reportes PDF, pruebas sin contenedores y de integración) con los comandos de su columna
- [ ] T0486 Ampliar specs/015-recaudo-tesoreria/quickstart.md con los recorridos de R1, T1, R2 y T2 (egresos, reglas, reporte de transacciones, traslados, convenios, conciliación completa, proveedores, liquidez, adquirentes) a medida que cada entrega se cierra

---

## Dependencies & Execution Order

### Entre fases

**Setup y plataforma**
- **Fase 1 (Setup)**: no depende de nada.
- **Fase 2 (plataforma fundacional)**: depende de la 1. Bloquea todo lo demás: cuentas bancarias de la
  persona, ámbitos de archivos, contextos de listas, alertas, permisos y plantillas de rol, parámetros, auditoría, cerrojos,
  numeradores, interfaces de los puertos y grupos de rutas son la base de cada historia.

**Bloque 2 — Caja y giros**
- **US12 (fase 3)** va primero: toda pantalla que mueve dinero necesita alcance, segregación y aprobaciones.
- **US1 → US2 → US3**: no hay sesión sin caja ni recibo sin sesión. El motor de arqueo (US2) y el de imputación (US3) pueden
  escribirse en paralelo desde el inicio del bloque.
- **US10 → US11 → US26**: no hay resultado sin lote ni marcas sin lote. US10 puede empezar en paralelo con US1, porque sólo
  comparte la plataforma.
- **US5** necesita US3 (recibos que anular) y US10 (la orden de pago para devolver saldos a favor). Cierra el bloque con la
  primera parte de E-01 (e)/E-02 (g), la activación mínima (T0183a, T0183b), la e2e de humo de la caja (T0183c, excepción
  declarada) y el hito del 04/11.

**Bloque 3 — Bancos, pagadurías, cheques y contabilidad**
- **US6** necesita US3 (recibo) y US10 (cuentas bancarias propias, donde vive el extracto).
- **US7** necesita US6 (`AplicacionNoInteractiva` y la línea de extracto que trae el giro).
- **US15** necesita US3 (el cheque es un medio) y US5 (reverso por porción).
- **US19** necesita US10 (chequera de la cuenta, forma cheque del lote) y US2 (bóveda).
- **US14** necesita US3 (`ConfirmacionDeRecibo`, donde engancha listas y declaración).
- **US8** necesita los hechos de US2, US3, US5, US6, US7, US10, US11, US15 y US19. Su matriz puede empezar en paralelo con
  US6 apenas exista el catálogo de hechos. Cierra el bloque con el hito del 10/11 (versión al paralelo en QA), que **congela** los pares de R0/T0 (T0271).

**Bloque 4 — Conciliación, salida en vivo y retiro de los dobles**
- **US25** necesita US6 (extracto), US19 (cheques y remesas) y US8 (movimientos contables de la cuenta). Reemplaza a la
  conciliación de Contabilidad (T0273a, T0273b, T0278a, T0280a; TR41) y va en un par nuevo (T0281).
- **US13** necesita US1, US2, US10, US19 y US8 (saldo contable para el cuadre).
- **US9** necesita US13 (el cuadre para activar) y todo lo anterior para que las e2e migradas de Cartera y Captaciones
  tengan con qué pagar y girar. Orden interno **que no se negocia** (FR-224): ayudante e2e → migrar e2e de Cartera → migrar
  e2e de Captaciones → bandera de la fixture en `false` y fuera `DoblesDePruebaDeCarteraTests` (T0300a) → **puerta**: correrlas en los dos motores → migración de datos de los dobles → borrar el código de los
  dobles → retirar la rama `Test` de los requisitos.
- **US21 parte R0/T0** necesita los datos de todas las historias de R0/T0.
- **US24** necesita US9 (activación), US13 (cuadre) y US21 (informe del paralelo). Los manuales deben estar antes del ensayo.

**Bloque 5 — Verificación y salida**
- Fase 22: depende de todo R0/T0. La e2e completa corre **una sola vez** (T0331); después se sellan las huellas
  de las migraciones nuevas posteriores al 10/11 (T0337; las cuatro primeras se congelaron en T0271), se ensaya (T0339),
  el PR deja la enmienda de la constitución y la desviación III (T0340a, T0340b) y se despliega (T0341).

**R1, T1, R2, T2 y el retiro**
- **R1 y T1** necesitan R0/T0 en producción y congelados. Dentro de R1, la base (v2 de los contratos) va primero; US4 necesita
  la cancelación con entrega de la base; US14 (reporte de transacciones) **debe estar en producción antes del primer
  vencimiento de enero de 2027**. T1 no depende de R1 salvo el comprobante de pago por correo (mensajería de US3 R1).
- **R2 y T2** necesitan R1 y T1. R2 y T2 no dependen entre sí.
- **El retiro de la Tesorería heredada** (fase 46) necesita T2 en producción (T0467): pagos a
  proveedores y conciliación completa.
- **Polish** va al cierre de cada entrega para su documentación y al final de todo.

### Orden de las migraciones

| # | Entrega | Migración | Se genera en | Se congela en |
|---|---|---|---|---|
| 1 | R0/T0 | `PlataformaParaRecaudoYTesoreria` | T0049 | T0271 (10/11) |
| 2 | R0/T0 | `RetiroDeLosDoblesDePrueba` (datos) | T0306, par nuevo | huella en T0337 |
| 3 | R0 | `RecaudoNucleo` | T0050 (y se regenera en cada fase de R0 hasta el 10/11) | T0271 (10/11) |
| 4 | T0 | `TesoreriaNucleo` | T0050 (y se regenera en cada fase de T0 hasta el 10/11) | T0271 (10/11) |
| 5 | R0/T0 | `IntegracionContableDeRecaudoYTesoreria` | T0137, regenerada en la fase 16 | T0271 (10/11) |
| 5a | T0 | `TesoreriaConciliacionMinima` | T0281, par nuevo | huella en T0337 |
| 5b | R0/T0 | `RecaudoYTesoreriaPuestaEnMarcha` | T0291, par nuevo | huella en T0337 |
| 6 | R1 | `RecaudoCompleto` | fase 23 | T0404 |
| 7 | R1 | `CarteraYCaptacionesConPartidaCitada` | fase 23 | T0404 |
| 8 | T1 | `TesoreriaCompleta` | fase 32 | T0424 |
| 9 | R2 | `RecaudoCanales` | fase 36 | T0438 |
| 10 | T2 | `TesoreriaAmpliada` | fase 40 | T0466 |
| 11 | después de T2 | `RetiroDeLaTesoreriaHeredada` (destructiva) | fase 46 | commit propio |

Los pares 1, 3, 4 y 5 se regeneran hasta el despliegue en QA del 10/11 y ahí se **congelan** (T0271): nunca más se
regeneran. Los pares nuevos 5a, 5b y 2 se generan una sola vez cada uno y se aplican en el orden de sus marcas de tiempo
(1 → 3 → 4 → 5 → 5a → 5b → 2, data-model §25); T0337 sella sus huellas. Las e2e de cada entrega posterior se corren al
cerrar esa entrega.

### Dentro de cada fase

1. Pruebas primero, y fallando (casos dorados, Application, arquitectura; las e2e se **escriben** aquí y corren al final).
2. Después enumeraciones y entidades, motores puros, Application, Infrastructure y regeneración del par de la entrega (en
   R0/T0, sólo hasta el congelamiento del 10/11; después, un par nuevo), API,
   cliente y pantallas (siempre con `IndicadorDeCarga`).
3. Al final, la tarea del dueño de la fase, si la hay.

## Ruta crítica (R0/T0 al 01/12)

La cadena que fija la fecha; un atraso en cualquiera corre la salida:

1. Plataforma y migración base: T0049.
2. Recibo por los dos contratos: T0115.
3. Consumidores reales con el registro transitorio: T0140, T0141, T0176.
4. Requisitos de activación que aceptan lo real, activación mínima y hito: T0183 → T0183b → T0184.
5. Procesos no interactivos y contabilidad: T0194, T0257, hito T0271 (que congela las migraciones de R0/T0).
6. **Retiro de los dobles, en este orden**: T0298 → T0299 → T0300 → T0300a (bandera de la fixture en `false` y fuera
   `DoblesDePruebaDeCarteraTests`) → **T0301 (puerta: las e2e de Cartera y Captaciones en verde en los dos motores)** →
   T0306 → T0307 → T0308 (los requisitos de Cartera y Captaciones sin la rama `Test` y el escritor que rechaza los
   dobles en todo ambiente). **La migración de las e2e va antes de borrar el código de los dobles**; borrar antes deja a Cartera y
   Captaciones sin pruebas de punta a punta y sin recaudador ni tesorería en las e2e.
7. Activación: T0303 y el hito T0329.
8. Verificación y salida: T0331 → T0337 → T0339 → T0341 → go/no-go del 28/11 →
   activación del 01/12.

Datos del dueño que bloquean la salida (rige el defecto para el código, pero el go/no-go los exige): P4 (13/11), P7, P12, P8 y
P15 (20/11); P6 bloquea sólo la pagaduría (06/11); P1 sólo si no se acepta el genérico o el portal (06/11).

## Parallel Examples

- **Fase 2**: cuentas bancarias de la persona, go/no-go, ámbitos y formatos, contextos de listas, tipos de alerta y de
  adjunto tocan archivos distintos y van a la vez; permisos, cerrojos y numeradores también. La migración va al final, sola.
- **Bloque 2**: la cadena de Recaudo (US1 → US2 → US3) y la de Tesorería (US10 → US11 → US26) avanzan en paralelo con dos
  personas; los motores puros (`ArqueoDeCaja`, `ImputacionDeMedios`, `ArmadoDeLote`, `CalculoDeGmf`,
  `TransicionesDelResultado`) y sus casos dorados se escriben a la vez desde el primer día del bloque.
- **Bloque 3**: US6 + US7 (bancos y pagadurías), US15 + US14 (cheques recibidos y SARLAFT), US19 (cheques girados y remesas) y la
  matriz de US8 son cuatro frentes independientes; sólo la regeneración de los pares es serial.
- **Bloque 4**: US25 (conciliación) y US13 (puesta en marcha) en paralelo; los manuales y runbooks de US24 (T0324,
  T0326) en paralelo con US9; la migración de las e2e de Cartera (T0299) y la de Captaciones
  (T0300) en paralelo entre sí, ambas antes de la puerta.
- **Verificación**: las pruebas de rendimiento (T0330) se escriben mientras corre la e2e.
- **Después de R0/T0**: R1 y T1 en paralelo; R2 y T2 en paralelo; dentro de cada entrega, las historias tocan carpetas distintas
  y sólo comparten la regeneración del par de su entrega.
- Toda tarea marcada **[P]** dentro de una fase puede asignarse a otra persona o agente sin esperar a las demás de la fase.

## Implementation Strategy

### MVP = R0 + T0

R0 y T0 completas (fases 1 a 22) son lo mínimo para activar COOFLOPAL el 01/12/2026; si un hito se atrasa, rigen las
líneas de recorte de plan.md, que decide el dueño. Puntos de control:
1. **04/11** (T0184): un recibo y un giro de punta a punta en DEV, con la activación mínima y la e2e de humo de la caja.
2. **10/11** (T0271): la versión del paralelo en QA y las migraciones de R0/T0 congeladas.
3. **19/11** (T0329): R0 y T0 completas en QA, dobles retirados.
4. **22/11** (T0337): e2e en verde en los dos motores y huellas de las migraciones nuevas posteriores al 10/11.
5. **23/11–25/11**: ensayo general; **27/11** producción; **28/11** go/no-go y carga definitiva; **01/12** salida.

### Entregas incrementales

R0/T0 → R1 + T1 → R2 + T2 → retiro de lo heredado. Cada entrega tiene su migración par, se cierra con sus e2e verdes en los dos
motores y su congelamiento, y se promueve a DEV, QA y producción sólo con respaldo por base y autorización expresa del dueño.

### Lo que no hace esta lista

No construye la gestión SARLAFT completa (reporte de operaciones sospechosas, segmentación), la facturación electrónica, caja
menor, inversiones, moneda extranjera, caja sin conexión, integraciones en línea con bancos ni botón de pago (C2 = A). No toca
la Tesorería heredada antes de su fase de retiro ni lo heredado de tarjeta débito (D32, salvo que P9 diga otra cosa).

## Notes

- Cada tarea cita el FR, la R, la TR, el CH o la pregunta que la origina. Lo que no está en DT va **(nuevo)** y se lleva a DT
  en la misma tarea.
- Commit por tarea o por grupo lógico; el retiro de los dobles va en un PR con su puerta e2e; el retiro de lo heredado va en
  un commit propio que no mezcla otro cambio.
- Las cifras de CLAUDE.md se vuelven a medir al cierre de cada entrega.
- Ningún texto del producto (pantalla, informe, plantilla, manual) nombra al sistema anterior: «lo heredado» y «cifras de
  referencia».

## Resumen de conteos

| Fase | Título | Tareas | Rango | Por historia |
|---|---|---|---|---|
| 1 | Setup (Shared Infrastructure) | 11 | T0001–T0011 | — 11 |
| 2 | Foundational (Blocking Prerequisites) — plataforma `PlataformaParaRecaudoYTesoreria` y esqueleto de los dos… | 40 | T0012–T0051 | — 40 |
| 3 | User Story 12 - Seguridad, alcance, segregación y auditoría | 13 | T0052–T0064 | US12 13 |
| 4 | User Story 1 - Configurar puntos de recaudo, cajas, cajeros y medios de pago | 14 | T0065–T0078 | US1 14 |
| 5 | User Story 2 - Abrir, arquear y cerrar la caja por medio de pago | 22 | T0079–T0100 | US2 22 |
| 6 | User Story 3 - Recibo de caja multiproducto por los contratos de Cartera y Captaciones | 24 | T0101–T0124 | US3 24 |
| 7 | User Story 10 - Cuentas bancarias propias y ejecución de órdenes de giro | 26 | T0125–T0150 | US10 26 |
| 8 | User Story 11 - Confirmar, rechazar y regirar | 13 | T0151–T0163 | US11 13 |
| 9 | User Story 26 - Controles antifraude de los pagos | 6 | T0164–T0169 | US26 6 |
| 10 | User Story 5 - Anular recibos, reversar aplicaciones y atender los reversos de los módulos | 18 | T0170–T0184 (con T0183a–T0183c) | US5 13, US9 3, US3 1, — 1 |
| 11 | User Story 6 - Consignaciones y transferencias recibidas: identificar, aplicar y partidas por identificar | 17 | T0185–T0200 (con T0189a) | US6 17 |
| 12 | User Story 7 - Pagadurías y libranza | 12 | T0201–T0212 | US7 12 |
| 13 | User Story 15 - Cheques recibidos, canje y devolución | 9 | T0213–T0221 | US15 9 |
| 14 | User Story 19 - Chequeras y cheques girados; remesas | 10 | T0222–T0231 | US19 10 |
| 15 | User Story 14 - Control de efectivo y SARLAFT | 16 | T0232–T0247 | US2 2, US14 14 |
| 16 | User Story 8 - Contabilidad recibe los hechos de Recaudo y Tesorería por su matriz | 24 | T0248–T0271 | US8 21, US10 2, — 1 |
| 17 | User Story 25 - Conciliación bancaria mínima | 14 | T0272–T0281 (con T0273a, T0273b, T0278a, T0280a) | US25 14 |
| 18 | User Story 13 - Puesta en marcha: saldos iniciales, pendientes y cuadre | 12 | T0282–T0293 | US13 12 |
| 19 | User Story 9 - Registrarse como participantes, retirar los dobles y liberar la activación de Cartera y Capt… | 18 | T0294–T0310 (con T0300a) | US9 18 |
| 20 | User Story 21 - Informes de recaudo, caja y tesorería, parte R0/T0 | 6 | T0311–T0316 | US21 6 |
| 21 | User Story 24 - Salida en vivo de COOFLOPAL: paralelo, ensayo, go/no-go y cuadre provisional | 13 | T0317–T0329 | US24 13 |
| 22 | Verificación de R0/T0, congelamiento de las migraciones y salida en vivo | 19 | T0330–T0345 (con T0340a, T0340b y T0483, movida de la fase 47) | — 19 |
| 23 | Base de R1 — versión 2 de los contratos (E-01 a, b; E-02 a, d) y migraciones `RecaudoCompleto` y `CarteraYC… | 9 | T0346–T0354 | — 9 |
| 24 | User Story 1 - Resto de medios de pago | 4 | T0355–T0358 | US1 4 |
| 25 | User Story 3 - Recibo completo: codeudor, lo exigible en una acción, recaudo pendiente de aplicar y avisos | 8 | T0359–T0366 | US3 8 |
| 26 | User Story 4 - Entregar dinero por la caja: retiros, saldos a favor y excedentes | 9 | T0367–T0375 | US4 9 |
| 27 | User Story 6 - Identificación por reglas y consignación reportada | 6 | T0376–T0381 | US6 6 |
| 28 | User Story 7 - Calendario legal de pagadurías | 3 | T0382–T0384 | US7 3 |
| 29 | User Story 14 - Reporte de transacciones y registro único de efectivo | 9 | T0385–T0393 | US14 9 |
| 30 | User Story 23 - Recaudar cuentas por cobrar comerciales | 5 | T0394–T0398 | US23 5 |
| 31 | User Story 21 - Informes y tablero de Recaudo (R1), cierre y promoción de R1 | 7 | T0399–T0405 | US21 4, — 3 |
| 32 | Base de T1 — migración `TesoreriaCompleta` | 1 | T0406–T0406 | — 1 |
| 33 | User Story 19 - Traslados entre cuentas propias, cheques al cierre del mes y remesa del punto de venta | 7 | T0407–T0413 | US19 7 |
| 34 | User Story 26 - Órdenes manuales, firmantes y tokens | 5 | T0414–T0418 | US26 5 |
| 35 | User Story 21 - Comprobante de pago, aviso al cierre contable e informes de Tesorería (T1), cierre y promoc… | 7 | T0419–T0425 | US11 2, US21 1, US25 2, — 2 |
| 36 | Base de R2 — migración `RecaudoCanales` | 1 | T0426–T0426 | — 1 |
| 37 | User Story 15 - Canje completo de cheques recibidos | 2 | T0427–T0428 | US15 2 |
| 38 | User Story 16 - Convenios de recaudo y canales externos | 8 | T0429–T0436 | US6 1, US16 7 |
| 39 | User Story 21 - Informes de canales (R2), cierre y promoción de R2 | 3 | T0437–T0439 | US21 1, — 2 |
| 40 | Base de T2 — migración `TesoreriaAmpliada` | 3 | T0440–T0442 | — 3 |
| 41 | User Story 17 - Conciliación bancaria completa con el extracto | 8 | T0443–T0450 | US17 8 |
| 42 | User Story 18 - Pagos a proveedores y de cuentas por pagar | 6 | T0451–T0456 | US18 6 |
| 43 | User Story 20 - Posición de liquidez y flujo de caja | 5 | T0457–T0461 | US20 5 |
| 44 | User Story 22 - Conciliación de adquirentes de tarjetas | 3 | T0462–T0464 | US22 3 |
| 45 | User Story 21 - Informes de Tesorería (T2), cierre y promoción de T2 | 3 | T0465–T0467 | US21 1, — 2 |
| 46 | Retiro de la Tesorería heredada — destructivo con guarda, en commit propio | 12 | T0468–T0479 | — 12 |
| 47 | Polish & Cross-Cutting Concerns | 6 | T0480–T0486 (sin T0483, movida a la fase 22) | — 6 |

- **Total**: 497 tareas; **201** marcadas [P]. El 2026-10-23 se agregaron once con sufijo (T0183a, T0183b, T0183c, T0189a,
  T0273a, T0273b, T0278a, T0280a, T0300a, T0340a, T0340b) y T0483 se movió de la fase 47 a la 22; ningún número cambió.
- **R0/T0** (fases 1 a 22): 357 tareas (T0001–T0345 con sus once sufijos, y T0483).
- **Ruta crítica**: 27 tareas: T0049, T0115, T0140, T0141, T0176, T0183, T0183b, T0194, T0257, T0271, T0298, T0299, T0300, T0300a, T0301, T0303, T0306, T0307, T0308, T0331, T0332, T0337, T0339, T0340, T0341, T0342, T0343.

Por historia (todas las entregas):

| Historia | Tareas |
|---|---|
| US1 | 18 |
| US2 | 24 |
| US3 | 33 |
| US4 | 9 |
| US5 | 13 |
| US6 | 24 |
| US7 | 15 |
| US8 | 21 |
| US9 | 21 |
| US10 | 28 |
| US11 | 15 |
| US12 | 13 |
| US13 | 12 |
| US14 | 23 |
| US15 | 11 |
| US16 | 7 |
| US17 | 8 |
| US18 | 6 |
| US19 | 17 |
| US20 | 5 |
| US21 | 13 |
| US22 | 3 |
| US23 | 5 |
| US24 | 13 |
| US25 | 16 |
| US26 | 11 |
| sin historia (Setup, plataforma, bases de entrega, verificación, retiro, Polish, hitos) | 113 |

Tareas del dueño (pendientes P1–P19 y actos del dueño):

| Tarea | Pendiente | Fecha límite |
|---|---|---|
| T0011 | P16 | 06/11 |
| T0064 | P4 | 13/11 |
| T0100 | P3 | 06/11 |
| T0124 | P13 | 13/11 |
| T0149 | P1 | 06/11 |
| T0150 | P14 | 13/11 |
| T0212 | P6 | 06/11, porque la quincena del 15/12 exige el archivo hacia el 10/12 |
| T0221 | P2 | 06/11 |
| T0247 | P7 | 20/11 |
| T0269 | P12, P8 | 20/11 |
| T0270 | P10 | 20/11 |
| T0292 | P17 | 20/11 |
| T0293 | P18 | 28/11 |
| T0327 | — | del 11/11 al 27/11 |
| T0328 | P15 | 20/11 |
| T0336 | P19 | 28/11, sin fecha en la spec: se propone antes de la carga definitiva |
| T0339 | — | del 23/11 al 25/11 |
| T0342 | — | 28/11 |
| T0343 | — | 01/12 |
| T0344 | — | del 02/12 al 04/12 |
| T0345 | — | primera semana de diciembre |
| T0354 | P11 | antes de construir R1, sin fecha en la spec |
| T0393 | P7 | antes del primer vencimiento de enero de 2027 |
| T0442 | P5 | antes de construir T2, sin fecha en la spec |
| T0468 | P9 | antes de este retiro, sin fecha en la spec |
| T0478 | — | antes de aplicar en cualquier ambiente con filas |

## Cobertura de requisitos (FR → tareas)

Generada desde las citas de cada tarea («FR-nnn» y los rangos «FR-nnn a FR-mmm»), actualizada a mano el 2026-10-23 con
las tareas con sufijo. Ningún requisito queda sin tarea. FR-106, FR-131, FR-148, FR-163 y FR-193 remiten desde esa fecha a
FR-011, FR-216, FR-223 y FR-114 (spec.md), y sus tareas cubren el requisito al que remiten.

| FR | Tareas |
|---|---|
| FR-001 | T0115, T0456 |
| FR-002 | T0020, T0110 |
| FR-003 | T0001, T0040 |
| FR-004 | T0001, T0041, T0091 |
| FR-005 | T0002, T0107 |
| FR-006 | T0112 |
| FR-007 | T0002, T0017–T0018, T0038 |
| FR-008 | T0110 |
| FR-009 | T0002, T0016, T0042 |
| FR-010 | T0042, T0213, T0382 |
| FR-011 | T0021, T0044, T0052 |
| FR-012 | T0054, T0060, T0414 |
| FR-013 | T0054, T0061 |
| FR-014 | T0043, T0062, T0189a, T0194 |
| FR-015 | T0026 |
| FR-016 | T0106, T0117 |
| FR-017 | T0046 |
| FR-018 | T0022, T0032, T0120 |
| FR-019 | T0095, T0160 |
| FR-020 | T0361, T0364, T0419 |
| FR-021 | T0065, T0072 |
| FR-022 | T0065, T0072, T0080 |
| FR-023 | T0066, T0072, T0355 |
| FR-024 | T0066, T0072, T0355 |
| FR-025 | T0072 |
| FR-026 | T0018, T0067, T0072, T0075 |
| FR-027 | T0067, T0072, T0075, T0475 |
| FR-028 | T0065, T0072, T0085 |
| FR-029 | T0080, T0092 |
| FR-030 | T0081, T0092 |
| FR-031 | T0079, T0082, T0093, T0356 |
| FR-032 | T0079, T0082, T0093 |
| FR-033 | T0082–T0084, T0093, T0096 |
| FR-034 | T0083, T0093 |
| FR-035 | T0068, T0073, T0084, T0094, T0223, T0227 |
| FR-036 | T0086 |
| FR-037 | T0102, T0113, T0359, T0363 |
| FR-038 | T0103, T0114 |
| FR-039 | T0103, T0114 |
| FR-040 | T0106, T0114, T0117 |
| FR-041 | T0101, T0105, T0114, T0355 |
| FR-042 | T0104, T0115 |
| FR-043 | T0104, T0187 |
| FR-044 | T0118 |
| FR-045 | T0118, T0361, T0364 |
| FR-046 | T0360, T0363 |
| FR-047 | T0105 |
| FR-048 | T0105 |
| FR-049 | T0394 |
| FR-050 | T0106 |
| FR-051 | T0367, T0372 |
| FR-052 | T0367, T0372 |
| FR-053 | T0172, T0177, T0367, T0372 |
| FR-054 | T0172, T0177, T0367, T0372 |
| FR-055 | T0367, T0372 |
| FR-056 | T0367, T0372 |
| FR-057 | T0170, T0175 |
| FR-058 | T0170, T0175 |
| FR-059 | T0171, T0176 |
| FR-060 | T0214, T0217, T0427 |
| FR-061 | T0368, T0372 |
| FR-062 | T0171 |
| FR-063 | T0171, T0176 |
| FR-064 | T0170, T0175, T0178 |
| FR-065 | T0186, T0193 |
| FR-066 | T0186, T0193 |
| FR-067 | T0195, T0376, T0379 |
| FR-068 | T0187, T0195 |
| FR-069 | T0377, T0379 |
| FR-070 | T0188, T0195 |
| FR-071 | T0187, T0195, T0352, T0377, T0379 |
| FR-072 | T0188, T0195 |
| FR-073 | T0188, T0195, T0197 |
| FR-074 | T0187 |
| FR-075 | T0201 |
| FR-076 | T0202, T0207 |
| FR-077 | T0202, T0207 |
| FR-078 | T0203, T0208 |
| FR-079 | T0203, T0208 |
| FR-080 | T0202–T0203, T0208 |
| FR-081 | T0203, T0400 |
| FR-082 | T0382 |
| FR-083 | T0213, T0217 |
| FR-084 | T0213, T0217, T0427 |
| FR-085 | T0214 |
| FR-086 | T0213 |
| FR-087 | T0399 |
| FR-088 | T0429, T0434 |
| FR-089 | T0429, T0434 |
| FR-090 | T0429, T0434, T0436 |
| FR-091 | T0429, T0434 |
| FR-092 | T0429, T0434 |
| FR-093 | T0430, T0434 |
| FR-094 | T0233, T0240 |
| FR-095 | T0232, T0241 |
| FR-096 | T0385, T0390 |
| FR-097 | T0235, T0241 |
| FR-098 | T0025, T0234, T0240 |
| FR-099 | T0368, T0386 |
| FR-100 | T0125, T0448 |
| FR-101 | T0367 |
| FR-102 | T0127, T0138 |
| FR-103 | T0127, T0138 |
| FR-104 | T0407, T0411 |
| FR-105 | T0223, T0227 |
| FR-106 | T0053, T0138 |
| FR-107 | T0129, T0140 |
| FR-108 | T0129, T0253, T0267 |
| FR-109 | T0129, T0142 |
| FR-110 | T0126, T0130, T0143 |
| FR-111 | T0130, T0143 |
| FR-112 | T0027, T0131, T0144 |
| FR-113 | T0130, T0222, T0226 |
| FR-114 | T0129 |
| FR-115 | T0129, T0140 |
| FR-116 | T0126, T0130 |
| FR-117 | T0419, T0421 |
| FR-118 | T0152–T0153, T0158 |
| FR-119 | T0153, T0158 |
| FR-120 | T0153, T0158 |
| FR-121 | T0142, T0153, T0158 |
| FR-122 | T0222, T0226 |
| FR-123 | T0153, T0160 |
| FR-124 | T0451 |
| FR-125 | T0452 |
| FR-126 | T0451 |
| FR-127 | T0129, T0452 |
| FR-128 | T0444, T0448 |
| FR-129 | T0443–T0444, T0448 |
| FR-130 | T0444, T0448 |
| FR-131 | T0273, T0278 |
| FR-132 | T0420, T0422 |
| FR-133 | T0462 |
| FR-134 | T0222, T0226 |
| FR-135 | T0222, T0226 |
| FR-136 | T0222, T0226 |
| FR-137 | T0457, T0461 |
| FR-138 | T0457, T0461 |
| FR-139 | T0458, T0461 |
| FR-140 | T0091, T0251, T0258 |
| FR-141 | T0091, T0116, T0178, T0251, T0373 |
| FR-142 | T0145, T0159, T0251 |
| FR-143 | T0248–T0251, T0257, T0260 |
| FR-144 | T0159, T0251–T0252, T0261 |
| FR-145 | T0003, T0251, T0261 |
| FR-146 | T0252 |
| FR-147 | T0183a–T0183b, T0294–T0295, T0303, T0352 |
| FR-148 | T0296, T0307 |
| FR-149 | T0295, T0352 |
| FR-150 | T0019, T0183a–T0183b, T0294–T0295, T0303 |
| FR-151 | T0132, T0140, T0176 |
| FR-152 | T0311, T0399, T0437 |
| FR-153 | T0169, T0312, T0423, T0465 |
| FR-154 | T0315 |
| FR-155 | T0399, T0461 |
| FR-156 | T0027, T0096, T0179, T0279 |
| FR-157 | T0282–T0283, T0287, T0434 |
| FR-158 | T0282, T0284, T0288 |
| FR-159 | T0019, T0294, T0303 |
| FR-160 | T0284, T0288 |
| FR-161 | T0003, T0441, T0472 |
| FR-162 | T0004, T0047 |
| FR-163 | T0307 |
| FR-164 | T0253, T0267, T0308, T0348–T0350, T0459 |
| FR-165 | T0308, T0349–T0350, T0459 |
| FR-166 | T0412, T0454, T0463 |
| FR-167 | T0258, T0273a, T0278a, T0321, T0396, T0455 |
| FR-168 | T0445, T0448 |
| FR-169 | T0023, T0026 |
| FR-170 | T0002, T0017, T0038, T0104, T0107, T0115, T0170 |
| FR-171 | T0187, T0194, T0203 |
| FR-172 | T0104, T0112 |
| FR-173 | T0016 |
| FR-174 | T0003, T0095, T0154, T0160, T0194 |
| FR-175 | T0052, T0208 |
| FR-176 | T0045, T0294 |
| FR-177 | T0106, T0117–T0118, T0361 |
| FR-178 | T0016, T0043 |
| FR-179 | T0067, T0106, T0214, T0394 |
| FR-180 | T0065, T0068, T0073 |
| FR-181 | T0065, T0085 |
| FR-182 | T0068, T0081, T0084, T0094, T0223 |
| FR-183 | T0082, T0085, T0094 |
| FR-184 | T0054, T0067, T0072, T0083, T0093, T0251, T0263 |
| FR-185 | T0083, T0086, T0095, T0263, T0303 |
| FR-186 | T0067, T0072, T0236, T0243 |
| FR-187 | T0359, T0363 |
| FR-188 | T0101, T0114, T0214 |
| FR-189 | T0103, T0114, T0234, T0240, T0372 |
| FR-190 | T0103, T0114, T0118 |
| FR-191 | T0233, T0240, T0367 |
| FR-192 | T0105 |
| FR-193 | T0150 |
| FR-194 | T0170, T0175 |
| FR-195 | T0171, T0176 |
| FR-196 | T0024, T0186, T0193, T0197 |
| FR-197 | T0024, T0185–T0186, T0193, T0429 |
| FR-198 | T0067, T0072, T0075, T0187 |
| FR-199 | T0188, T0195 |
| FR-200 | T0431, T0434 |
| FR-201 | T0024, T0202, T0207 |
| FR-202 | T0042, T0232 |
| FR-203 | T0025, T0142–T0143, T0167, T0234, T0240 |
| FR-204 | T0235, T0241, T0386, T0390 |
| FR-205 | T0125, T0145, T0368, T0372 |
| FR-206 | T0125, T0143, T0145 |
| FR-207 | T0127–T0128, T0139, T0407 |
| FR-208 | T0415, T0417 |
| FR-209 | T0259, T0273, T0442, T0456 |
| FR-210 | T0012–T0013, T0029, T0032, T0142, T0164, T0166–T0167, T0283 |
| FR-211 | T0414, T0417 |
| FR-212 | T0152, T0158, T0168 |
| FR-213 | T0024, T0130, T0144, T0149, T0153, T0158, T0295 |
| FR-214 | T0130, T0144 |
| FR-215 | T0151, T0153 |
| FR-216 | T0272–T0273, T0273a, T0278, T0278a |
| FR-217 | T0060, T0273, T0278–T0279 |
| FR-218 | T0430, T0444, T0462 |
| FR-219 | T0222, T0226 |
| FR-220 | T0408, T0411 |
| FR-221 | T0250, T0262 |
| FR-222 | T0450 |
| FR-223 | T0003, T0141, T0296, T0306–T0307 |
| FR-224 | T0183c, T0299–T0300, T0300a, T0301 |
| FR-225 | T0324, T0402, T0423, T0437, T0465 |
| FR-226 | T0271, T0323, T0326–T0327, T0342 |
| FR-227 | T0014, T0033, T0249, T0317, T0320, T0322, T0326, T0342 |
| FR-228 | T0284, T0288, T0318, T0321, T0344 |

Criterios de éxito (SC → tareas):

| SC | Tareas |
|---|---|
| SC-001 | T0305, T0343 |
| SC-002 | T0339 |
| SC-003 | T0120 |
| SC-004 | T0087 |
| SC-005 | T0087 |
| SC-006 | T0108 |
| SC-007 | T0108 |
| SC-008 | T0376 |
| SC-009 | T0189 |
| SC-010 | T0330, T0333 |
| SC-011 | T0126, T0330, T0333 |
| SC-012 | T0160 |
| SC-013 | T0126 |
| SC-014 | T0252 |
| SC-015 | T0334 |
| SC-016 | T0446, T0466 |
| SC-017 | T0387 |
| SC-018 | T0021, T0056, T0313, T0334 |
| SC-019 | T0043, T0056 |
| SC-020 | T0002, T0334 |
| SC-021 | T0285, T0344 |
| SC-022 | T0339, T0345 |
| SC-023 | T0330, T0333 |
| SC-024 | T0323, T0327, T0342 |
| SC-025 | T0165, T0237 |
| SC-026 | T0013, T0164–T0165 |
| SC-027 | T0297, T0334 |
| SC-028 | T0128, T0133 |
| SC-029 | T0318–T0319, T0344 |
| SC-030 | T0189 |
| SC-031 | T0330, T0333 |
| SC-032 | T0108, T0330, T0333 |
