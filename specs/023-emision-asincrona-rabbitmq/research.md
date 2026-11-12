# Research — 023 Emisión electrónica asíncrona

Fecha: 2026-11-11. Base: develop `b3934111`. Cada decisión lleva su porqué y lo que se descartó.

## R1. La bandeja de salida ya existe: no se crea otra

- **Decisión**: la orden de emitir sigue siendo la fila de `COR_ElectronicDocuments` (nace `Pending` con `NextAttemptAt` en la
  transacción de confirmación) y la orden de entregar un mensaje de integración sigue siendo `COR_IntegrationMessageDeliveries`.
  El trabajo posterior a una emisión (representación, adjuntos, correo) se vuelve una **orden durable más** sobre la misma fila del
  documento (`FollowUpDueAt`), no una tabla nueva.
- **Porqué**: el pedido exige «reutilizar o extender, no inventar otro». Esas filas ya son el outbox: nacen con el hecho, tienen
  estado, intentos, próxima hora y arrendamiento.
- **Descartado**: una tabla `COR_Outbox` genérica de órdenes a publicar (duplicaría el estado y abriría la pregunta de cuál manda);
  MassTransit con su outbox EF (otro modelo de estado, otra tabla por cooperativa, y una dependencia grande para tres colas).

## R2. Papel de RabbitMQ: despertar y repartir (D2)

- **Decisión**: el mensaje en RabbitMQ es un **aviso sin datos de negocio** `{cooperativa, tipo, entidad}`. El consumidor relee la
  base y actúa sólo si la fila sigue elegible; si no, confirma sin efecto. La verdad (estado, intentos, próxima hora, muerto) vive en
  la base de cada cooperativa.
- **Publicación**: (1) **tras el commit** de la petición, `TareasTrasElCommit` publica las órdenes nacidas en ella con confirmación del
  broker y estampa `EnqueuedAt`; (2) el **barrido de respaldo** (los procesadores de hoy) publica lo elegible cuyo `EnqueuedAt` falta o
  es más viejo que `Mensajeria:RepublicarTrasSegundos` (120 s); si el broker no está, **hace el trabajo él mismo** como hoy. Así un
  aviso perdido, un broker caído o un nack retrasan, nunca pierden ni detienen (FR-008, FR-010).
- **Reintentos**: la espera entre intentos la sigue llevando `NextAttemptAt` en la base; el barrido republica cuando vence. No se usan
  colas con TTL por espera: la precisión del barrido (15 s) basta con esperas ≥ 15 s y no hay que sincronizar dos relojes.
- **Descartado**: RabbitMQ como cola principal sin respaldo (el dueño lo descartó); colas de reintento con TTL + DLX por escalón
  (duplican la política que ya está en `EsperasDeReintento` y `ReintentosDeIntegracion`).

## R3. El cobro no hace el intento: lo espera (D7)

- **Decisión**: `EsperaEnLineaDelPos` deja de llamar al canal. Tras el commit, el cobro publica la orden y **lee la fila** cada 150 ms
  hasta `Dian.EsperaMaximaPosSegundos` (defecto nuevo 2 s, 0 = no espera). Quien emite es un consumidor (cualquier réplica). Si
  validó a tiempo, la tirilla lleva CUDE y QR; si no, sale sin ellos con la leyenda «pendiente de validación DIAN».
- **Porqué**: hoy, si la espera vence a mitad de la llamada, el token se cancela, el arrendamiento queda tomado hasta que vence (120 s)
  y el intento queda ambiguo. Esperando la fila, nadie corta una llamada a medias; el arrendamiento lo suelta el consumidor al
  registrar. Además la espera del cobro deja de contar como falla del circuito (lo cuenta el consumidor con el resultado real).
- **Sin broker**: el cobro publica en el canal en proceso (R5) y el consumidor local emite; la espera es la misma.
- **Descartado**: calcular el CUDE en el ERP (exige el PIN del software; con proveedor tecnológico el CUDE es del proveedor y una
  discrepancia dejaría un QR que nunca valida; D7).

## R4. Topología de colas

| Cola (quorum, durable) | Aviso | Consumidor |
|---|---|---|
| `erp.fe.emision` | `fe.emitir` {cooperativa, documento} | `EmitElectronicDocumentCommand(documento)` |
| `erp.fe.seguimiento` | `fe.seguimiento` {cooperativa, documento} | `CompleteElectronicDocumentFollowUpCommand` (nuevo): representación, adjuntos, correo |
| `erp.integracion` | `integracion.entregar` {cooperativa, destino} | una pasada en línea del destino en esa cooperativa (`DespachadorDeMensajes`, bajo su arrendamiento) |
| `erp.muertos` | lo que no se pudo leer o superó `x-delivery-limit` | sólo se registra y se mide; nadie lo procesa |

- Intercambio directo `erp.trabajos`; clave de ruteo = nombre de la cola. Mensajes `persistent`, `content-type: application/json`,
  `message-id` = `{tipo}:{cooperativa:N}:{entidad:N}:{marca}` para trazabilidad (no para deduplicar: la idempotencia está en la base).
- Quorum con `x-delivery-limit = 5` y `x-dead-letter-exchange = erp.muertos.x`: un aviso que hace lanzar al consumidor cinco veces se
  aparta; la fila sigue en la base y el barrido la republica más tarde (FR-023).
- Prefetch (`BasicQos`) 8 por consumidor y `consumerDispatchConcurrency` 4 por réplica (configurables); ack **después** del
  `SaveChanges` del comando; nack con reencolar si el comando lanzó; ack sin efecto si la fila no estaba elegible o la tiene otro.
- **Aislamiento (Principio IV)**: el broker es uno por ambiente (vhost `/erp`) y lo comparten las cooperativas, pero **no guarda datos
  de ninguna**: sólo el `PublicId` de la cooperativa y de la entidad. El trabajo se hace con `IEjecutorEnCooperativa`, que abre la base de
  esa cooperativa. Un aviso de una cooperativa inactiva se confirma y se registra. Justificado en Complexity Tracking.

## R5. Biblioteca y adaptador

- **Decisión**: `RabbitMQ.Client` 7.x directo, en un proyecto nuevo `src/Infrastructure/IngenIA365ERP.Messaging`, único que la
  referencia (prueba de arquitectura `SoloElAdaptadorDeMensajeriaConoceAlBroker`). Application define el puerto
  `IColaDeTrabajos` (publicar con confirmación → `bool`) y `IManejadorDeAviso` por tipo. Dos implementaciones:
  `ColaRabbitMq` y `ColaEnProceso` (un `Channel<T>` acotado, uno por réplica: para DEV sin broker, pruebas unitarias y como
  modo degradado explícito con `Mensajeria:Proveedor = EnProceso`).
- Canal de publicación con `CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)`:
  `BasicPublishAsync` termina cuando el broker confirma y lanza `PublishException` ante nack/return o `AlreadyClosedException` si se cae
  la conexión; cualquiera de las dos deja la orden sin `EnqueuedAt` (la toma el barrido).
- Reconexión: `AutomaticRecoveryEnabled` + `TopologyRecoveryEnabled`; la topología se declara al arrancar (idempotente).
- **Descartado**: MassTransit / Wolverine (R1); `Oragon.RabbitMQ` (capa extra sin beneficio aquí).

## R6. Tope de intentos y muertos (D3)

- Parámetros nuevos con vigencia (`COR_ParameterVersions`, catálogo como los de I4):
  `Dian.TopeDeIntentos` (entero, 12, ámbito cooperativa) e `Integracion.TopeDeIntentos` (entero, 12).
- **Qué cuenta**: sólo los intentos con resultado transitorio — en documentos, `ChannelUnavailable`, `InProcess` sin respuesta y
  `NotFound` repetido (lo que hoy `CuentaComoFallaDelCanal` o deja `Pending/Sent`); en integración, las fallas que
  `RegisterDeliveryResultCommand` ya trata como reintentables. Un rechazo (DIAN o negocio) nunca cuenta y nunca muere.
- **Contador**: `TransientFailures` (nuevo) que se reinicia al validar, al reprocesar y en el caso a/b; `AttemptCount` sigue siendo la
  historia total.
- **Morir**: al registrar un resultado transitorio con `TransientFailures ≥ tope` → `DeadAt = ahora`, `NextAttemptAt = null`, alerta
  `Dian.DocumentoSinEnviar` / `Integracion.EntregaDetenida` con `DedupKey` por entidad. En contingencia no muere antes de
  `TransmissionDeadline` (FR-025): sigue reintentando y la alerta del plazo es la de hoy.
- **Reprocesar** (`ReprocessDeadLettersCommand`, motivo 10–500): `DeadAt = null`, `TransientFailures = 0`, `NextAttemptAt = ahora`,
  publica el aviso; auditoría encadenada (`COR_AuditOutbox`) con el motivo y la lista; atiende la alerta.

## R7. Lo que sale del intento hacia el seguimiento

Hoy `IntentoAnteElCanal.DespuesAsync` hace en línea, tras guardar: circuito, contingencia, alerta de rechazo, alerta «sin validar»,
cierre del evento RADIAN, **representación PDF**, **entrega** (correo + adjunto). Se quedan en el intento las piezas baratas y de
estado (circuito, contingencia, alertas, RADIAN). Pasan al seguimiento (`FollowUpDueAt`) la representación y la entrega, más la
subida del canónico que hoy hace `GuardadoDeArtefactos` tras el commit de la confirmación. El seguimiento es idempotente por las
marcas que ya existen: canónico y representación en la versión (`CanonicalAttachmentPublicId`, `GraphicPdfAttachmentPublicId`), `EmailSentAt`.

## R8. Observabilidad

- La API no expone métricas hoy. Se agrega `prometheus-net.AspNetCore` (estable, sin dependencia de OTel) con un servidor de métricas
  en un **puerto aparte** (`Metricas:Puerto`, 9464) que no publica el Service del Ingress.
- Medidores: `erp_trabajos_pendientes{cola,cooperativa}`, `erp_trabajos_edad_mas_viejo_segundos{cola,cooperativa}`,
  `erp_trabajos_muertos{cola,cooperativa}` (los calcula el barrido por cooperativa: es la verdad de la base);
  `erp_mensajeria_publicaciones_total{cola,resultado}`, `erp_mensajeria_consumos_total{cola,resultado}`,
  `erp_cobro_espera_segundos` (histograma). La profundidad del broker sale de su propio plugin `rabbitmq_prometheus` (15692).
- El agente Prometheus de PDN (`infrastructure/pdn/monitoreo.yaml`) suma dos `scrape_configs` (API 9464 y RabbitMQ 15692); las reglas
  van en `infrastructure/nonprod/alertas-mensajeria.yaml` (muertos > 0, edad > 10 min, broker caído).
- Logs: todo `[Mensajeria.*]` y `[FE.*]` lleva `{TenantPublicId}` y `{DocumentoPublicId}`/`{MensajePublicId}` (FR-029).

## R9. Salud

`rabbitmq` como chequeo `ready` que devuelve **Degraded** (HTTP 200) cuando el broker no responde, con veredicto cacheado 30 s como
`BlobStoreHealthCheck`: un broker caído no saca las réplicas del Service (FR-012).

## R10. Despliegue

- Componente GitOps `workloads/erp/components/mensajeria`: `StatefulSet erp-rabbitmq` (1 réplica, `rabbitmq:4.1-management-alpine`
  con `rabbitmq_prometheus`), `volumeClaimTemplates` 2Gi (`volumeMode: Filesystem` explícito), Service headless + ClusterIP,
  `vm_memory_high_watermark.absolute = 320MiB`, `disk_free_limit.absolute = 256MiB`; recursos req 100m/256Mi, lim 1/512Mi; probes
  `rabbitmq-diagnostics -q ping` / `check_port_connectivity` con `timeoutSeconds` explícito; parche a `erp-api` con
  `Mensajeria__*` y usuario/clave por `secretKeyRef` al Secret `erp-rabbitmq` (`optional: false`: sin él no arranca, como
  `auditoria-protegida`).
- Secret creado fuera del repo por `tools/scripts/crear-secreto-rabbitmq.ps1` (usuario `erp`, clave aleatoria, Erlang cookie).
- PDN: el `ResourceQuota` de `disponibilidad.yaml` suma 256Mi de request y 512Mi de límite y una PVC; se ajusta en el mismo commit.
- Orden de puesta: DEV → QA → PDN; la API sin el componente sigue en `EnProceso` (sin broker), así el despliegue del código y el del
  broker son independientes.

## R11. Impresión directa (D5)

- `INV_CashRegisters.DirectPrint` (bool, falso). En MAUI, `IImpresionDeDocumentos` ya imprime nativo: con la marca, imprime sin vista
  previa las `PrintCopies`. En web, `pos.js.imprimir` ya usa un iframe oculto: con la marca se omite la vista previa propia y se llama
  `print()` por copia; el diálogo del navegador sólo desaparece con Chrome en `--kiosk-printing` (manual).

## R12. Pruebas

- Unitarias: clasificación transitorio/definitivo, tope y muerte, contingencia sin morir antes del plazo, reproceso, espera del cobro
  (con `ColaEnProceso` y reloj falso), seguimiento idempotente, tirilla sin CUDE.
- Arquitectura: `SoloElAdaptadorDeMensajeriaConoceAlBroker`, `NingunHandlerDePeticionLlamaAlCanal` (sólo los manejadores de aviso,
  el comando de emisión y la consulta manual llaman `ICanalDeEmisionElectronica`).
- e2e: fixture derivada `ApiConMensajeriaFixture` (patrón de `ApiConAlmacenS3Fixture`) con `Testcontainers.RabbitMq` 4.* y dos
  hosts sobre la misma base para «dos réplicas»; escenarios: broker caído al confirmar, réplica que cae entre llamada y registro
  (canal simulado con «perder respuesta»), aviso duplicado, tope y reproceso, cobro con canal lento. No se agrega el contenedor a la
  fixture base (multiplicaría la carga de las ~25 clases).
