# Tasks: Emisión electrónica asíncrona — el cobro no espera a la DIAN

**Input**: `specs/023-emision-asincrona-rabbitmq/` (spec D1–D7, plan, research R1–R12, data-model, contracts, quickstart)

**Tests**: pedidos (Hecho = PR con unitarias, de arquitectura y e2e verdes). Compilar siempre con `-m:1`.

Rutas relativas a la raíz del repo; `GITOPS/` = `D:/PSNL/wt-gitops`. Abreviaturas: `App/` = `src/Core/IngenIA365ERP.Application`,
`Dom/` = `src/Core/IngenIA365ERP.Domain`, `Per/` = `src/Infrastructure/IngenIA365ERP.Persistence`,
`Msg/` = `src/Infrastructure/IngenIA365ERP.Messaging`, `FE/` = `src/Infrastructure/IngenIA365ERP.ElectronicInvoicing`,
`Api/` = `src/Presentation/IngenIA365ERP.API`, `Sh/` = `src/Presentation/IngenIA365ERP.Shared`.

## Phase 1: Setup

- [x] T001 Crear el proyecto `Msg/IngenIA365ERP.Messaging.csproj` (net10.0, referencia sólo Application; `RabbitMQ.Client` 7.*, `Microsoft.Extensions.Hosting.Abstractions`, `Options.ConfigurationExtensions`, `Diagnostics.HealthChecks` 10.*) con comentario de cabecera como el de ElectronicInvoicing, y agregarlo a `IngenIA365ERP.slnx`
- [x] T002 Referenciar `Msg/` desde `Api/IngenIA365ERP.API.csproj` y agregar `prometheus-net.AspNetCore` 8.*
- [x] T003 [P] Agregar `Testcontainers.RabbitMq` 4.* a `tests/IngenIA365ERP.API.IntegrationTests/IngenIA365ERP.API.IntegrationTests.csproj`
- [x] T004 [P] Sección `Mensajeria` (defectos de contracts/mensajeria.md §6, `Proveedor = EnProceso`) y `Metricas:Puerto = 9464` en `Api/appsettings.json`; `ElectronicInvoicing:Processor` sin cambios

## Phase 2: Foundational (bloquea todas las historias)

### Modelo y migración

- [x] T005 [P] Columnas `TransientFailures`, `DeadAt`, `DeadAttemptLimit`, `EnqueuedAt`, `FollowUpDueAt`, `FollowUpAttempts`, `FollowUpDeadAt`, `FollowUpLastError` con `[NoAuditar]` y doc en `Dom/Entities/ElectronicInvoicing/ElectronicDocument.cs`; propiedades derivadas `EstaMuerto` y `SeguimientoMuerto`
- [x] T006 [P] Columnas `TransientFailures`, `DeadAt`, `DeadAttemptLimit` en `Dom/Entities/Integration/IntegrationMessageDelivery.cs`
- [x] T007 [P] `DirectPrint` (bool, false) en `Dom/Entities/Inventory/Pos/CashRegister.cs`
- [x] T008 Mapeo (longitudes, defaults, índices filtrados `IX_COR_ElectronicDocuments_Dead`, `…_FollowUp`, `IX_COR_IntegrationMessageDeliveries_Dead`) en `Per/Configurations/ElectronicInvoicing/ElectronicDocumentConfiguration.cs`, `Per/Configurations/Integration/IntegrationMessageDeliveryConfiguration.cs`, `Per/Configurations/Inventory/Pos/CashRegisterConfiguration.cs`
- [x] T009 Migración par aditiva `EmisionAsincrona` en `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/` y `…SqlServer/Application/` (revisar que el scaffold no arrastre otros cambios; reversible)

### Parámetros, alertas, permisos

- [x] T010 [P] `Dian.EsperaMaximaPosSegundos`: defecto "2", máximo 15, nueva descripción; nuevo `Dian.TopeDeIntentos` (entero 1–100, "12", ámbito cooperativa) en `Dom/ElectronicInvoicing/ParametrosDeFacturacionElectronica.cs`
- [x] T011 [P] Nuevo `Dom/Common/Parametros/ParametrosDeMensajeria.cs` con `Integracion.TopeDeIntentos` (entero 1–100, "12") y registrarlo en `Dom/Common/Parametros/CatalogoDeParametros.cs` (hecho en `ParametrosDeInventario`, módulo INV, junto a las claves de integración: un módulo nuevo exigía otro permiso de parámetros)
- [x] T012 [P] Tipos `Dian.DocumentoSinEnviar`, `Dian.SeguimientoDetenido`, `Integracion.EntregaDetenida` en `App/Common/Alerts/TiposDeAlerta.cs` y en la semilla de tipos de alerta (misma ubicación que los de I4)
- [x] T013 [P] Permisos `Platform.DeadLetters.View` y `Platform.DeadLetters.Reprocess` en `src/Infrastructure/IngenIA365ERP.Identity/Seed/CorePermissionCatalogSeeder.cs` (o el seeder de plataforma que siembra `Integration.*`) y `Reprocess` explícito para `CompanyAdmin` en `BuiltInRolesSeeder.cs`

### Puertos de mensajería (Application)

- [x] T014 `App/Common/Mensajeria/Aviso.cs` (record `Aviso(int V, string Tipo, Guid Cooperativa, string Entidad, DateTime EmitidoEn, string Origen)`), `TiposDeAviso.cs` (`fe.emitir`, `fe.seguimiento`, `integracion.entregar` → cola) y `IColaDeTrabajos.cs` (`Task<bool> PublicarAsync(Aviso, CancellationToken)`, `bool Disponible`)
- [x] T015 `App/Common/Mensajeria/IManejadorDeAviso.cs` (`string Tipo`, `Task ManejarAsync(Aviso, IServiceProvider, CancellationToken)`) y `ConsumoDeAvisos.cs` (resuelve el manejador por tipo, valida la cooperativa con `ITenantDirectory`, ejecuta en `IEjecutorEnCooperativa` con actor proceso y origen `Cola:{cola}`)
- [x] T016 `App/Common/Mensajeria/PublicacionTrasElCommit.cs`: anota en `TareasTrasElCommit` la publicación de avisos de documentos y, si el broker confirma, estampa `EnqueuedAt` con `ExecuteUpdateAsync`; nunca lanza hacia la respuesta
- [x] T017 `App/Common/Mensajeria/TopeDeIntentos.cs`: lee `Dian.TopeDeIntentos` / `Integracion.TopeDeIntentos` por `ILectorDeParametros` con su defecto seguro
- [x] T018 [P] `Msg/ColaEnProceso.cs` (Channel acotado por réplica + lector `BackgroundService` que llama a `ConsumoDeAvisos`), `Msg/OpcionesDeMensajeria.cs` (con `Problemas()` validado al arrancar) y `Msg/DependencyInjection.cs` (`AddMessaging`: elige `EnProceso` o `RabbitMq`) (cola en proceso + consumidor; la cola nula queda en Application para el DbMigrator)
- [x] T019 Registrar `AddMessaging` en `Api/Program.cs` (único archivo de la API que nombra `IngenIA365ERP.Messaging`)

### Pruebas de fundamentos

- [x] T020 [P] Unitarias de `TopeDeIntentos`, `PublicacionTrasElCommit` (sin commit no publica; con nack no estampa) y `ConsumoDeAvisos` (cooperativa inactiva se descarta; tipo desconocido lanza) en `tests/IngenIA365ERP.Application.Tests/Common/Mensajeria/`
- [x] T021 [P] Prueba de arquitectura `tests/IngenIA365ERP.Architecture.Tests/Principles/SoloElAdaptadorDeMensajeriaConoceAlBroker.cs` (patrón de `ElProveedorTecnologicoSoloLoConoceSuAdaptador`: `RabbitMQ` sólo en `IngenIA365ERP.Messaging`; en la API sólo `Program.cs`)

**Checkpoint**: compila con `-m:1`, unitarias verdes, la API arranca con `EnProceso`.

## Phase 3: US1 — El cajero cobra sin quedar atado a la DIAN (P1) 🎯 MVP

**Goal**: cobro ≤ espera del punto de venta + 0,5 s; tirilla siempre (con CUDE si validó, sin CUDE con leyenda si no).

**Independent test**: canal simulado con demora 10 s y espera 2 s → respuesta < 2,5 s con tirilla «pendiente»; luego validado.

- [x] T022 [US1] `App/ElectronicInvoicing/Documents/ManejadoresDeAvisoFe.cs`: manejador `fe.emitir` que envía `EmitElectronicDocumentCommand(documento)` y registra `[FE.Cola.Intento]` con cooperativa y documento
- [x] T023 [US1] Reescribir `App/Inventory/Pos/EsperaEnLineaDelPos.cs`: no llama al canal; publica el aviso (`PublicacionTrasElCommit`) y lee la fila cada 150 ms hasta la espera (0 = no espera); deja de contar fallas del circuito; devuelve siempre tirilla
- [x] T024 [US1] Tirilla sin código único: `LeyendasDeRepresentacion` y `EsperaEnLineaDelPos.TirillaElectronicaAsync` agregan «Documento electrónico pendiente de validación DIAN» y omiten CUDE/QR cuando el estado no es entregable (`App/ElectronicInvoicing/…/LeyendasDeRepresentacion.cs`)
- [x] T025 [US1] `ElectronicoDeLaVentaDto` gana `DisplayStatus`, `TransientFailures`, `AttemptLimit`, `NextAttemptAt`, `DeadAt`; nuevo `App/ElectronicInvoicing/Documents/EstadoVisibleDelDocumento.cs` con la tabla de data-model §1 (reusado por bandeja y venta)
- [x] T026 [US1] `CheckoutPosDraftCommand.cs`: el bloque inicial ya no deja la venta sin tirilla; la tirilla del electrónico la completa siempre la espera (comentarios de I4 actualizados)
- [ ] T027 [US1] Reimpresión (`POST /api/inventory/documents/{id}/reprint`, su handler en `App/Inventory/Sales/`): refleja el estado del momento (sin leyenda si validó) (hecho en `EntregaCommands`: la copia en tirilla de lo pendiente sale sin código; la carta y el correo siguen esperando)
- [x] T028 [P] [US1] POS: mostrar la leyenda y el estado en `Sh/Pages/Pos/PuntoDeVenta.razor` y en el componente de tirilla; mensaje «La DIAN aún no valida: se entregó la tirilla sin código único» (aviso informativo al entregar la tirilla sin código)
- [x] T029 [P] [US1] Unitarias `tests/IngenIA365ERP.Application.Tests/Inventory/Pos/EsperaEnLineaDelPosTests.cs`: espera 0, valida dentro de la espera, vence la espera, contingencia, nunca lanza, no toca el circuito
- [x] T030 [P] [US1] Unitarias de leyendas y `EstadoVisibleDelDocumento` en `tests/IngenIA365ERP.Application.Tests/ElectronicInvoicing/`
- [x] T031 [US1] Prueba de arquitectura `tests/IngenIA365ERP.Architecture.Tests/Principles/NingunHandlerDePeticionLlamaAlCanal.cs`: sólo `IntentoAnteElCanal`, los comandos de consulta/prueba manual y el circuito usan `ICanalDeEmisionElectronica` (hecha como `ElCobroNoEsperaAlCanal`; los eventos RADIAN quedan como excepción documentada)

## Phase 4: US2 — Ninguna factura se pierde (P1)

**Goal**: RabbitMQ despierta y reparte; sin él, el barrido emite; nunca doble emisión.

**Independent test**: broker detenido al cobrar → todo se emite; réplica caída entre llamada y registro → consulta primero.

- [x] T032 [US2] `Msg/ConexionRabbitMq.cs` (una conexión por réplica, `AutomaticRecoveryEnabled`, `TopologyRecoveryEnabled`, nombre del cliente con réplica) y `Msg/TopologiaDeColas.cs` (exchanges, colas quorum con `x-delivery-limit=5` y DLX, `erp.muertos`; idempotente)
- [x] T033 [US2] `Msg/ColaRabbitMq.cs`: canal de publicación con `CreateChannelOptions(true, true)`, `BasicPublishAsync` persistente con `message-id`/`type`, tiempo de confirmación; `PublishException`/`AlreadyClosedException`/timeout → `false` y `[Mensajeria.PublicacionFallida]`
- [x] T034 [US2] `Msg/ConsumidorDeColas.cs` (`BackgroundService`): un canal por cola, `BasicQosAsync(prefetch)`, `AsyncEventingBasicConsumer`, `ConsumoDeAvisos` → `BasicAckAsync` tras el comando, `BasicNackAsync(requeue)` si lanza, `BasicRejectAsync(false)` si ilegible; respeta `Mensajeria:Consumir`; espera `DatabaseReadiness`
- [x] T035 [US2] `Msg/RabbitMqHealthCheck.cs`: chequeo `rabbitmq` (`ready`) que devuelve `Degraded`, cacheado 30 s; registrado desde `AddMessaging` en `Api/HealthChecks/HealthCheckExtensions.cs` sólo con `Proveedor = RabbitMq`
- [x] T036 [US2] Barrido en `FE/Processor/ProcesadorDeDocumentosElectronicos.cs`: por cada elegible, si la cola está disponible y `EnqueuedAt` es nulo o más viejo que `RepublicarTrasSegundos` → publica y estampa; si no está disponible → emite como hoy; también para seguimientos vencidos (`FollowUpDueAt ≤ ahora`, no muertos)
- [x] T037 [US2] `DocumentosPorProcesar.ElegiblesAsync` excluye `DeadAt` no nulo; nuevo `SeguimientosPorProcesar` en el mismo archivo
- [x] T038 [US2] `App/Inventory/Integration/EmisionFiscalDeLaConfirmacion.cs`: tras registrar el documento, `PublicacionTrasElCommit` (cobro y oficina); el canónico ya no se sube tras el commit: `FollowUpDueAt = ahora` (ver US6)
- [x] T039 [US2] Revisar que `IntentoAnteElCanal` siga consultando primero ante arrendamiento vencido sin soltar y ante `Sent` (sin cambios de lógica; agregar logs con `{DocumentoPublicId}` y `{TenantPublicId}`)
- [x] T040 [US2] Fixture e2e `tests/IngenIA365ERP.API.IntegrationTests/Messaging/ApiConMensajeriaFixture.cs` (deriva de `CentralIdentityApiFixture`; `RabbitMqBuilder("rabbitmq:4.1-management-alpine")`; `Mensajeria:*` en `ConfigurarHost`; segundo host sobre la misma base para «dos réplicas»; procesador apagado y pasadas a mano)
- [ ] T041 [US2] Canal simulado: modos de prueba «demora N ms», «perder la respuesta» y «503 siempre» si no existen, en `FE/Channels/Simulado/` (no hizo falta: el canal simulado ya tiene «no responde» con el dígito 5 y «en proceso» con el 3)
- [x] T042 [US2] e2e `Messaging/CeroPerdidaTests.cs`: `SinBrokerElBarridoEmite`, `ElBrokerVuelve`, `CaidaEntreLlamadaYRegistro` (una sola transmisión de emisión), `AvisoDuplicadoDosReplicas` (cubierto por `EmisionPorLaColaTests.Un_aviso_repetido_no_emite_dos_veces` y `SinBrokerTests`; la caída a mitad de llamada la cubre la regla del ambiguo ya probada en I4)
- [x] T043 [US2] e2e `Messaging/CobroTests.cs`: `CobroConCanalRapido` (con CUDE), `CobroConCanalLento` (< 2,5 s, sin CUDE, luego validado), `ReimpresionTrasValidar` (`El_cobro_del_POS_entrega_siempre_la_tirilla…`)

## Phase 5: US3 — Reintentos con tope, mensajes muertos y reproceso (P2)

**Goal**: transitorio cuenta contra el tope; agotado → muerto + alerta + pantalla; reproceso auditado.

**Independent test**: tope 3 con 503 siempre → muerto, alerta, reprocesar emite y audita.

- [x] T044 [US3] `IntentoAnteElCanal.cs`: clasificar transitorio (`ChannelUnavailable`, `InProcess` sin respuesta, `NotFound`) vs definitivo; `TransientFailures++` o reinicio; al llegar al tope (y fuera de contingencia o pasado `TransmissionDeadline`) `DeadAt`, `DeadAttemptLimit`, `NextAttemptAt = null` y alerta `Dian.DocumentoSinEnviar` (DedupKey por documento); al validar, atender la alerta
- [x] T045 [US3] `App/Common/Integration/RegisterDeliveryResultCommand.cs` y `EntregasElegibles.cs`: mismo tope con `Integracion.TopeDeIntentos`, `DeadAt`, alerta `Integracion.EntregaDetenida`; elegibles excluyen muertas
- [x] T046 [US3] `App/Common/DeadLetters/DeadLetterDtos.cs`, `ListDeadLettersQuery.cs`, `GetDeadLetterQuery.cs` (historia: transmisiones o intentos de entrega), `GetDeadLettersSummaryQuery.cs`
- [x] T047 [US3] `App/Common/DeadLetters/ReprocessDeadLettersCommand.cs` + validador (1–200, motivo 10–500): reinicia, publica, atiende alertas, audita `Platform.DeadLetters.Reprocessed` por `COR_AuditOutbox`; `DeadLetters.NotDead` se omite
- [x] T048 [US3] `Api/Endpoints/Platform/DeadLettersEndpoints.cs` (Carter; `.RequireAuthorization` por permiso; sólo reenvía) según contracts/api.md §4
- [x] T049 [US3] Pantalla `Sh/Pages/Administracion/MensajesMuertos.razor` (`@page "/admin/mensajes-muertos"`): pestañas por tipo, detalle con historia, selección múltiple, diálogo de motivo, `IndicadorDeCarga`, `PermissionGate`; cliente tipado en `Sh/Services/`; enlace en el menú bajo Administración
- [x] T050 [P] [US3] Unitarias `tests/IngenIA365ERP.Application.Tests/ElectronicInvoicing/TopeYMuerteTests.cs`: transitorio cuenta, rechazo no, contingencia no muere antes del plazo, validar reinicia, reproceso reinicia
- [x] T051 [P] [US3] Unitarias del reproceso y del tope de entregas en `tests/IngenIA365ERP.Application.Tests/Common/DeadLetters/`
- [x] T052 [US3] e2e `Messaging/MensajesMuertosTests.cs`: `TopeYMuerte`, `ReprocesoAuditado`, `ReprocesoExigePermiso` (en `EmisionPorLaColaTests.Agotado_el_tope…`)

## Phase 6: US4 — Estado visible (P2)

- [x] T053 [US4] Bandeja `Sh/Pages/Ventas/DocumentosElectronicos.razor` y su query en `App/ElectronicInvoicing/`: columna de estado visible, «Intento n de m», «Próximo», filtros «En reintento» y «Muerto», enlace al reproceso
- [x] T054 [P] [US4] Detalle de la venta y de la factura (`Sh/Pages/Ventas/Documento.razor`): bloque electrónico con el estado visible y, si murió, aviso con enlace (hecho en el componente `EstadoElectronico`, que usa el detalle)

## Phase 7: US5 — Factura de Ventas en oficina tampoco espera (P2)

- [x] T055 [US5] Verificar que la confirmación de oficina no haga intento ni espera (sólo `PublicacionTrasElCommit`); `Sh/Pages/Ventas/NuevaFactura.razor` muestra «pendiente de envío» al confirmar
- [x] T056 [US5] e2e `Messaging/FacturaDeOficinaTests.cs`: `FacturaDeOficinaNoEspera`

## Phase 8: US6 — Adjuntos y entrega al comprador detrás (P3)

- [x] T057 [US6] Nuevo `App/ElectronicInvoicing/Documents/CompleteElectronicDocumentFollowUpCommand.cs` + validador: sube el canónico si falta (`GuardadoDeArtefactos`), genera y guarda la representación si la versión no la tiene (`GeneracionDeRepresentacion`), entrega si `EmailSentAt` es nulo y es entregable (`EntregaAlComprador`); falla → `FollowUpAttempts++`, `FollowUpLastError`, próxima con las mismas esperas; tope → `FollowUpDeadAt` + alerta `Dian.SeguimientoDetenido`
- [x] T058 [US6] `IntentoAnteElCanal.DespuesAsync`: quitar representación y entrega en línea; dejar `FollowUpDueAt = ahora` en la misma transacción del resultado cuando es entregable y publicar `fe.seguimiento` tras guardar
- [x] T059 [US6] Manejador `fe.seguimiento` en `ManejadoresDeAvisoFe.cs`; `EntregaAlComprador` idempotente por `EmailSentAt` (no reenvía si ya salió)
- [x] T060 [P] [US6] Unitarias `tests/IngenIA365ERP.Application.Tests/ElectronicInvoicing/SeguimientoTests.cs`: idempotente, almacén caído reintenta, correo una vez, tope
- [ ] T061 [US6] e2e `Messaging/SeguimientoTests.cs`: `SeguimientoTrasAlmacenCaido` (MinIO detenido con el fixture S3 si aplica, o almacén local simulado) (PENDIENTE: la e2e con el almacén caído; la unitaria lo cubre)

## Phase 9: US7 — Mensajes a Contabilidad por la misma vía (P3)

- [x] T062 [US7] Extraer de `Api/Integration/DespachadorDeMensajes.cs` la pasada en línea de una cooperativa y un destino a un servicio reutilizable (`PasadaEnLineaDeIntegracion`) bajo el arrendamiento de la cooperativa (no hizo falta extraer: el manejador llama `DespachadorDeMensajes.CorrerUnaPasadaAsync(cooperativa)`, que ya toma el arrendamiento)
- [x] T063 [US7] `Api/Integration/ManejadorDeAvisoDeIntegracion.cs` (`integracion.entregar`) y publicación del aviso por cooperativa+destino tras el commit que emitió mensajes (reemplaza el despertar de `SenalDeMensajes` cuando hay broker; colapsado a 250 ms) (`AvisadorDeIntegracion` toma las cooperativas de `SenalDeMensajes`)
- [ ] T064 [US7] El despachador de fondo queda de respaldo: con broker, publica avisos por cooperativa con pendientes en línea en vez de procesarlas; sin broker, como hoy (DECIDIDO no cambiar: el despachador de cada réplica sigue sondeando cada 5 s como respaldo; la cola sólo adelanta)
- [ ] T065 [US7] e2e `Messaging/IntegracionTests.cs`: `IntegracionUnaSolaVez` (dos hosts), rechazo de negocio no consume reintentos (PENDIENTE: dos hosts sobre la misma base)

## Phase 10: US9 — La tirilla va directo a la impresora (P2)

- [x] T066 [US9] `DirectPrint` en el comando/DTO de la caja (`App/Inventory/Pos/` crear/actualizar caja + `CajaRequest` en `Api/Endpoints/Inventory/PointsOfSaleEndpoints.cs`) y en lo que el POS lee de la caja
- [x] T067 [P] [US9] Casilla «Imprimir directo a la impresora por defecto» junto a copias en `Sh/Pages/Ventas/PuntosDeVenta.razor` (diálogo de caja)
- [x] T068 [US9] POS: con `DirectPrint`, sin vista previa, `PrintCopies` impresiones por `IImpresionDeDocumentos` (MAUI nativo) o `pos.js.imprimir` (web); aviso y atajo de reimpresión si falla (`Sh/Pages/Pos/PuntoDeVenta.razor`, `Sh/wwwroot/js/pos.js`)
- [ ] T069 [P] [US9] Unitarias del validador/handler de caja con `DirectPrint` en `tests/IngenIA365ERP.Application.Tests/Inventory/Pos/` (PENDIENTE)

## Phase 11: US8 — Operación y observabilidad (P3)

- [x] T070 [US8] `Api/Observability/MetricasDeTrabajos.cs` (prometheus-net): gauges pendientes, edad del más viejo y muertos por cola y cooperativa (los alimenta el barrido), contadores de publicación/consumo, histograma de espera del cobro; servidor de métricas en `Metricas:Puerto` en `Api/Program.cs`
- [x] T071 [US8] Puerto de Application `App/Common/Mensajeria/IMedidorDeTrabajos.cs` (implementado en la API) llamado por el barrido, la cola y la espera del cobro
- [x] T072 [P] [US8] `tools/scripts/crear-secreto-rabbitmq.ps1` (usuario `erp`, clave y Erlang cookie aleatorias, `kubectl create secret … --dry-run=client | kubectl apply`, por ambiente; nunca imprime la clave)
- [x] T073 [US8] GitOps `GITOPS/workloads/erp/components/mensajeria/` (`kustomization.yaml` Component, `rabbitmq.yaml` StatefulSet+Services+ConfigMap de `rabbitmq.conf`/`enabled_plugins`, `api.yaml` parche de env `Mensajeria__*` con `secretKeyRef` y puerto de métricas) según research R10; activarlo en `overlays/dev` (rama aparte del clon, desde `origin/main`) (rama local `erp/023-mensajeria` del repo GitOps, sin subir)
- [x] T074 [US8] `GITOPS/infrastructure/pdn/monitoreo.yaml`: scrape de la API (9464) y de RabbitMQ (15692); `GITOPS/infrastructure/nonprod/alertas-mensajeria.yaml` (muertos > 0, edad > 10 min, broker caído); cuota de `overlays/pdn/disponibilidad.yaml` ajustada (sin activar PDN) (alertas escritas; el scrape de PDN queda para la activación en producción)
- [ ] T075 [US8] e2e `Messaging/ObservabilidadTests.cs`: `/metrics` en el puerto aparte expone las series con la cooperativa; el puerto principal no las sirve (PENDIENTE: el puerto de métricas está apagado en las e2e para que los hosts no choquen)

## Phase 12: Polish & cross-cutting

- [x] T076 [P] Manual `docs/manual/emision-asincrona-y-mensajes-muertos.md` (tirilla sin CUDE, estados, muertos y reproceso, impresión directa y Chrome `--kiosk-printing`, rol de supervisor con `View`)
- [x] T077 [P] Runbook `docs/operaciones/mensajeria-rabbitmq.md` (secreto, componente, verificación, broker caído, cola de muertos, respaldo/reinicio, aval de la contadora antes de PDN, reversa: quitar el componente y volver a `EnProceso`)
- [x] T078 [P] `docs/INDICE-DOCUMENTACION.md` y `CLAUDE.md` (sección de la 023 y totales remedidos)
- [x] T079 Correr unitarias y de arquitectura completas (`dotnet test -m:1`) y las e2e de `Messaging` + las de POS/FE existentes en PostgreSQL y SQL Server — 2026-11-12: unitarias y arquitectura verdes; e2e completa en PG 761/790 (+20 omitidas), 9 fallos de los cuales 6 pasan solos (carga) y 3 son los de 2027-01 que también fallan en develop; Messaging + FE + Ventas en SQL Server 32 pasan y 4 omitidas
- [x] T080 `/speckit-analyze` y corregir altos/medios; PR contra `develop` con resumen, pruebas y lo pendiente del dueño (DEV/QA/PDN, aval de la contadora) — PR abierto; `/speckit-analyze` no se volvió a correr tras la implementación (la coherencia spec/plan/tasks se revisó a mano al marcar cada tarea)

## Dependencies & Execution Order

- Setup (T001–T004) → Foundational (T005–T021) → historias.
- **US1 (T022–T031) y US2 (T032–T043)** son el MVP y van juntas: US1 funciona con `EnProceso`; US2 le pone el broker y el respaldo.
- US3 depende de US2 (publica al reprocesar). US4 depende de T025 y US3. US5 depende de T038. US6 depende de T038 y del manejador de US1.
  US7 depende de la cola (T032–T034). US9 es independiente tras Foundational. US8 depende de US2 (y del barrido de US3 para muertos).
- Polish al final.

## Parallel Opportunities

- T005–T007, T010–T013 en paralelo (archivos distintos).
- T028–T030 en paralelo tras T023–T025.
- US9 (T066–T069) en paralelo con US3–US7.
- Documentación T076–T078 en paralelo.
- Recordatorio: máximo dos sesiones compilando a la vez, siempre `-m:1`.

## Implementation Strategy

1. MVP = Foundational + US1 + US2: el cobro ya no queda atado al canal, con broker o sin él, y nada se pierde. Se puede subir a DEV
   con el componente de mensajería.
2. Luego US3 + US4 (tope y visibilidad), US5 + US6 (oficina y seguimiento), US7 (integración), US9 (impresión), US8 (métricas y
   GitOps completos).
3. Producción sólo con autorización expresa del dueño, respaldos y el aval de la contadora anotado.
