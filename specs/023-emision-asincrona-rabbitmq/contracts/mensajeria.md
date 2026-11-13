# Contrato de mensajería — 023

El broker sólo **despierta y reparte**. Nada de lo que viaja aquí es fuente de verdad: el consumidor relee la base.

## 1. Topología (la declara la API al arrancar, idempotente)

| Objeto | Tipo | Argumentos |
|---|---|---|
| vhost `/erp` | — | uno por ambiente (cada ambiente tiene su broker) |
| `erp.trabajos` | exchange `direct`, durable | — |
| `erp.muertos.x` | exchange `fanout`, durable | — |
| `erp.fe.emision` | queue quorum, durable | `x-queue-type=quorum`, `x-delivery-limit=5`, `x-dead-letter-exchange=erp.muertos.x` |
| `erp.fe.seguimiento` | ídem | ídem |
| `erp.integracion` | ídem | ídem |
| `erp.muertos` | queue quorum, durable | ligada a `erp.muertos.x`; nadie la consume (sólo métricas y bitácora) |

Bindings: `erp.trabajos` → cada cola con routing key = nombre de la cola.

## 2. El aviso

```json
{
  "v": 1,
  "tipo": "fe.emitir | fe.seguimiento | integracion.entregar",
  "cooperativa": "PublicId de la cooperativa (Guid)",
  "entidad": "PublicId del documento electrónico, o código del destino (\"ACC\", \"CAR\"…) para integracion.entregar",
  "emitidoEn": "2026-11-11T15:04:05.123Z",
  "origen": "cobro | confirmacion | barrido | reproceso | resultado"
}
```

Propiedades AMQP: `persistent` (delivery-mode 2), `content-type=application/json`, `message-id = {tipo}:{cooperativa:N}:{entidad}:{emitidoEn ticks}`,
`type = tipo`. Sin datos personales, sin montos, sin números fiscales.

## 3. Publicar

- Canal dedicado con confirmaciones (`publisherConfirmationsEnabled` y tracking): la publicación termina cuando el broker confirma.
- Éxito → se estampa `EnqueuedAt = ahora` en la fila (documento) — para `integracion.entregar` no hay estampa: el aviso es por
  cooperativa+destino y se colapsa (a lo sumo uno cada `Mensajeria:AvisoIntegracionMinimoMs`, 250 ms, por réplica).
- `PublishException` (nack/return), `AlreadyClosedException`, tiempo agotado (`Mensajeria:ConfirmacionSegundos`, 5) → la fila queda
  como estaba; el barrido la toma. Se registra `[Mensajeria.PublicacionFallida]` con cooperativa y entidad.
- Quién publica: (a) `TareasTrasElCommit` de la petición que creó la orden; (b) `IntentoAnteElCanal` al dejar `FollowUpDueAt`;
  (c) el barrido para lo elegible con `EnqueuedAt` nulo o más viejo que `Mensajeria:RepublicarTrasSegundos` (120); (d) el reproceso.

## 4. Consumir

1. Deserializar; si falla o `v` no es 1 → `BasicReject(requeue: false)` (va a `erp.muertos`) y `[Mensajeria.AvisoIlegible]`.
2. Cooperativa no activa en el directorio → `ack` y `[Mensajeria.CooperativaInactiva]`.
3. Dentro de `IEjecutorEnCooperativa` con el actor «Proceso de integración», canal `Process`, origen `Tarea:cola.{cola}` (el ejecutor sólo admite `Mensaje:`, `Lote:` y `Tarea:`):
   - `fe.emitir` → `EmitElectronicDocumentCommand(documento)` (respeta la espera y el arrendamiento por fila);
   - `fe.seguimiento` → `CompleteElectronicDocumentFollowUpCommand(documento)`;
   - `integracion.entregar` → una pasada en línea del destino en esa cooperativa (`EntregasElegibles.EnLineaAsync` + consumidor del
     destino), bajo el arrendamiento `integration.dispatch` de la cooperativa.
4. El comando terminó (con o sin intento: no le tocaba, lo tenía otro, ya era final) → `BasicAck` **después** de su `SaveChanges`.
5. El comando lanzó → `BasicNack(requeue: true)`; el quorum cuenta la entrega; a la quinta va a `erp.muertos`. La fila no se pierde: el
   barrido la vuelve a publicar.

Prefetch `Mensajeria:Prefetch` (8) por canal de consumo; `Mensajeria:ConcurrenciaPorCola` (4) manejadores simultáneos por réplica.

## 5. Garantías y lo que no garantiza

| Garantía | Cómo |
|---|---|
| Ninguna orden se pierde | Nace en la transacción del hecho (base); el barrido republica o ejecuta. |
| Un documento no se emite dos veces | Arrendamiento por fila + regla del ambiguo (consultar antes de reenviar) + clave `{tenant}:{env}:{Number}:v{n}`. |
| Un aviso duplicado no hace daño | El consumidor relee; lo terminado o no elegible se confirma sin efecto. |
| El orden fiscal no depende de la cola | El número se asigna al confirmar; `WaitsForDocumentId` decide la elegibilidad. |
| **No** garantiza orden de llegada | Las colas pueden entregar en cualquier orden; nadie depende de él. |
| **No** garantiza latencia sin broker | Sin broker, la latencia es la del barrido (15 s). |

## 6. Configuración (`Mensajeria`)

| Clave | Defecto | Nota |
|---|---|---|
| `Proveedor` | `EnProceso` | `RabbitMq` en los ambientes con el componente; `EnProceso` en desarrollo y pruebas unitarias. |
| `Host`, `Puerto`, `VirtualHost`, `Usuario`, `Clave` | —, 5672, `/erp`, —, — | Usuario y clave sólo por Secret (`erp-rabbitmq`). |
| `Prefetch` | 8 | |
| `ConcurrenciaPorCola` | 4 | |
| `ConfirmacionSegundos` | 5 | |
| `RepublicarTrasSegundos` | 120 | |
| `AvisoIntegracionMinimoMs` | 250 | |
| `Consumir` | true | `false` para un host que sólo publica (pruebas de «dos réplicas»). |

Con `Proveedor = RabbitMq` y el broker inalcanzable al arrancar, la API arranca igual (salud `Degraded`), publica en modo «nada se
confirma» y el barrido hace el trabajo; se reconecta sola.
