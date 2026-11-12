# Runbook — mensajería RabbitMQ (feature 023)

## 1. Qué es y qué no es

- RabbitMQ **sólo despierta y reparte** el trabajo de fondo de la API: emitir documentos electrónicos (`erp.fe.emision`), su seguimiento
  —representación y correo— (`erp.fe.seguimiento`) y la bandeja de integración (`erp.integracion`). Colas **quorum**, mensajes persistentes,
  confirmación del broker al publicar, ack manual después de guardar, prefetch 8 y 4 manejadores por cola y réplica.
- **La verdad está en la base de cada cooperativa** (`COR_ElectronicDocuments`, `COR_IntegrationMessageDeliveries`). Los avisos llevan sólo
  PublicIds. Si el broker se cae o pierde avisos, **no se pierde nada**: la API sigue vendiendo, `/health/ready` dice `rabbitmq: Degraded`
  (HTTP 200) y el barrido de cada réplica emite cada 15 s como antes de la 023.
- Un aviso que hace fallar al consumidor 5 veces se aparta a `erp.muertos` (sólo para diagnóstico); la fila sigue y el barrido la
  republica. Los **muertos de negocio** (tope de intentos agotado) están en la base y en **Administración › Mensajes muertos**.

## 2. Activar en un ambiente

1. Secret (desde el repo del ERP, nunca en GitOps):

   ```bash
   pwsh tools/scripts/crear-secreto-rabbitmq.ps1 -Ambiente dev
   ```

2. GitOps: agregar `- ../../components/mensajeria` a `components:` del overlay (`workloads/erp/overlays/<amb>/kustomization.yaml`) y
   sincronizar. El componente trae el StatefulSet `erp-rabbitmq` (1 réplica, PVC 2 GiB, 100m/256Mi pedidos, 1 CPU/512Mi de límite,
   `vm_memory_high_watermark` 320 MiB) y el parche de la API (`Mensajeria__*` y el puerto `metrics` 9464).
3. **PDN además**: subir el `ResourceQuota` de `overlays/pdn/disponibilidad.yaml` (requests.memory +256Mi, limits.memory +512Mi,
   persistentvolumeclaims +1) y sumar al agente de `infrastructure/pdn/monitoreo.yaml` el scrape de la API (puerto `metrics`) y de
   `erp-rabbitmq:15692`. Antes de PDN: autorización expresa del dueño, respaldo de las bases (`pg_dump` por base) y **el aval de la contadora
   a la tirilla sin código único** (anotar fecha y nombre aquí).
4. Verificar:

   ```bash
   kubectl -n erp-dev exec erp-rabbitmq-0 -- rabbitmq-diagnostics -q check_running
   ```

   ```bash
   kubectl -n erp-dev exec erp-rabbitmq-0 -- rabbitmqctl -p /erp list_queues name type messages consumers
   ```

   Deben aparecer las cuatro colas, `quorum`, con consumidores = réplicas × 1. En la API: `curl -s localhost:9464/metrics | grep erp_`
   desde el pod, y `/health/ready` con `rabbitmq: Healthy`.

## 3. Migración de base

`EmisionAsincrona` (par PostgreSQL / SQL Server), **aditiva**: columnas nuevas con valor por defecto o nulas e índices filtrados. Reversible
con su `Down`. La aplica el DbMigrator como cualquier otra.

## 4. Operación

| Síntoma | Qué mirar | Qué hacer |
|---|---|---|
| Alerta `ErpTrabajosMuertos` / `Dian.DocumentoSinEnviar` | Mensajes muertos: último motivo | Resolver la causa (canal, credencial, almacén) y reprocesar con motivo. |
| `ErpEmisionAtrasada` | `erp_trabajos_edad_mas_viejo_segundos`, logs `[FE.*]` y `[Mensajeria.*]` con el PublicId | Canal lento o caído; contingencia según la política de la cooperativa. |
| `ErpRabbitMqCaido` | `kubectl describe pod erp-rabbitmq-0`, eventos, memoria | La API sigue: el barrido emite. Reiniciar el pod; el volumen conserva las colas. |
| Cola `erp.muertos` crece | `rabbitmqctl list_queues`, logs `[Mensajeria.ConsumoFallido]` | Un manejador lanza siempre: ver la excepción; la fila la reprocesa el barrido. Vaciar `erp.muertos` es seguro (`rabbitmqctl purge_queue erp.muertos -p /erp`). |

Logs útiles: `[Mensajeria.Conectada]`, `[Mensajeria.SinBroker]`, `[Mensajeria.PublicacionFallida]`, `[FE.Cola.Intento]`,
`[FE.DocumentoMuerto]`, `[FE.SeguimientoDetenido]`, `[Integracion.EntregaMuerta]`, `[FE.CobroSinValidar]`.

## 5. Volver atrás

Quitar el componente del overlay y sincronizar: la API vuelve a `Mensajeria__Proveedor=EnProceso` (por defecto) y el barrido hace todo. No
hay datos que migrar ni perder. Borrar la PVC `data-erp-rabbitmq-0` después si no se volverá a activar.

## 6. Rotar la clave

Borrar el Secret y el StatefulSet con su PVC (el broker no guarda nada que no esté en la base), crear el Secret de nuevo con el guion y
sincronizar.
