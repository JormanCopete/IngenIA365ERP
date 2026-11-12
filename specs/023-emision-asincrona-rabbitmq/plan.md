# Implementation Plan: Emisión electrónica asíncrona — el cobro no espera a la DIAN

**Branch**: `claude/dian-asincrona-rabbitmq-798014` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/023-emision-asincrona-rabbitmq/spec.md` (decisiones D1–D7 del dueño).

## Summary

El cobro del POS tarda porque, tras confirmar, intenta la emisión y espera la validación hasta 15 s, y además genera la
representación, sube adjuntos y manda el correo dentro de la misma petición. La garantía de no pérdida **ya existe**: la orden de
emitir es la fila del documento electrónico, que nace en la transacción de la venta, y un procesador de fondo con arrendamientos la
reintenta. El plan:

1. **El cobro espera la fila, no hace el intento** (R3): publica el aviso y lee el estado hasta `Dian.EsperaMaximaPosSegundos`
   (defecto 2 s). Validado → tirilla con CUDE/QR; si no → tirilla igual, sin CUDE, con «pendiente de validación DIAN» (D7).
2. **Todo lo secundario sale del intento** a un **seguimiento durable** sobre la misma fila (`FollowUpDueAt`, R7): representación,
   canónico y correo, con reintentos y tope propios. La factura de oficina deja de subir el canónico tras el commit.
3. **RabbitMQ despierta y reparte** (D2, R2, R4): avisos sin datos de negocio en colas quorum (`erp.fe.emision`,
   `erp.fe.seguimiento`, `erp.integracion`) con confirmación del publicador, ack manual tras el `SaveChanges` y prefetch acotado. El
   barrido de hoy queda de respaldo: republica lo vencido o, sin broker, hace el trabajo él mismo.
4. **Tope y muertos** (D3, R6): `TransientFailures` contra `Dian.TopeDeIntentos`/`Integracion.TopeDeIntentos` (12); agotado,
   `DeadAt`, alerta y pantalla `/admin/mensajes-muertos` con reproceso auditado (D6, dos permisos).
5. **Observabilidad, salud y despliegue** (R8–R10): métricas Prometheus en puerto aparte, chequeo `rabbitmq` degradado, componente
   GitOps `mensajeria`, Secret por guion.
6. **Impresión directa por caja** (D5, R11): `DirectPrint` junto a las copias.

## Technical Context

**Language/Version**: C# / .NET 10.0.5

**Primary Dependencies**: MediatR, FluentValidation, EF Core (PostgreSQL y SQL Server), Carter, Blazor (Shared, Web, MAUI),
**RabbitMQ.Client 7.x** (nuevo, sólo en `IngenIA365ERP.Messaging`), **prometheus-net.AspNetCore 8.x** (nuevo, sólo en la API),
**Testcontainers.RabbitMq 4.*** (nuevo, sólo en las e2e).

**Storage**: base por cooperativa (columnas nuevas, migración par aditiva `EmisionAsincrona`); RabbitMQ 4.1 por ambiente (efímero
en sentido de negocio: nada que se pierda ahí es irrecuperable).

**Testing**: xUnit + NSubstitute (Application), Architecture.Tests (reflexión + escaneo de fuentes, como
`ElProveedorTecnologicoSoloLoConoceSuAdaptador`), API.IntegrationTests con Testcontainers (PostgreSQL y SQL Server).

**Target Platform**: k3s en VPS (PDN 6 vCPU / 12 GB; la API ya usa ~1,6 GB), Argo CD, overlays dev/qa/pdn.

**Project Type**: web-service + Blazor (web y MAUI).

**Performance Goals**: cobro ≤ espera del punto de venta + 0,5 s (con espera 0, p95 < 1 s); validación p95 < 10 s con canal sano;
muerto visible < 1 min tras el último intento.

**Constraints**: cero pérdida, cero doble emisión, varias réplicas, broker opcional (la API arranca y vende sin él), RabbitMQ con
≤ 512 MiB de límite en PDN, credenciales fuera del repo público, compilar con `-m:1`.

**Scale/Scope**: decenas de cooperativas por ambiente; cientos a pocos miles de documentos por día por cooperativa; 3 colas.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec con D1–D7, este plan, tasks a continuación. |
| II   | Clean Architecture | PASS | Puertos `IColaDeTrabajos`/`IManejadorDeAviso` en Application; RabbitMQ sólo en el proyecto nuevo `Infrastructure/IngenIA365ERP.Messaging`, registrado desde `Program.cs`; prueba `SoloElAdaptadorDeMensajeriaConoceAlBroker`. |
| III  | CQRS + MediatR | PASS | El consumidor envía comandos (`EmitElectronicDocumentCommand`, `CompleteElectronicDocumentFollowUpCommand`, `ReprocessDeadLettersCommand`, `ListDeadLettersQuery`), cada uno con su validador; endpoints sólo reenvían. |
| IV   | Multi-tenancy | PASS con nota | Todo trabajo corre en `IEjecutorEnCooperativa` contra la base de la cooperativa del aviso. El broker es compartido por ambiente pero **no almacena datos de cooperativa** (sólo PublicIds); ver Complexity Tracking. El barrido sigue recorriendo el directorio cooperativa por cooperativa. |
| V    | Person centralizada | N/A | No toca personas. |
| VI   | PublicId | PASS | Avisos, rutas y DTOs sólo con PublicId; los muertos se reprocesan por PublicId. |
| VII  | Soft-delete + auditoría | PASS | Sólo columnas nuevas en entidades `AuditableEntity` existentes; reproceso auditado por `COR_AuditOutbox`. |
| VIII | Validación dual | PASS | Validador de reproceso (motivo 10–500, 1–200 ítems) y el diálogo pide el motivo; `DirectPrint` booleano. |
| IX   | Errores visibles | PASS | Fallas de publicación/consumo con `[Mensajeria.*]` y contexto; los `Seguro(...)` del intento dejan de tragarse la representación y la entrega: pasan al seguimiento con error guardado y muerte alertada. |
| X    | Trazabilidad | PASS | Reproceso y cambio de caja por el pipeline; los avisos no llevan datos sensibles. |
| XI   | Inmutabilidad contable | N/A | No toca asientos; los mensajes a Contabilidad siguen por `DestinoContabilidad`/`AccountingPoster`. |
| XII  | Migraciones | PASS | Una migración par aditiva (columnas nulas o con default, índices filtrados), reversible; sin datos. |
| UI   | IndicadorDeCarga / tokens | PASS | La pantalla de muertos y los cambios de la bandeja usan `IndicadorDeCarga` + `EstadoDeCarga`, sin colores literales. |

## Project Structure

### Documentation (this feature)

```text
specs/023-emision-asincrona-rabbitmq/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/ mensajeria.md, api.md
├── checklists/requirements.md
└── tasks.md            (/speckit-tasks)
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/ElectronicInvoicing/ElectronicDocument.cs           (+ TransientFailures, DeadAt, DeadAttemptLimit, EnqueuedAt, FollowUp*, RepresentationAttachmentPublicId)
├── Entities/Integration/IntegrationMessageDelivery.cs           (+ TransientFailures, DeadAt, DeadAttemptLimit)
├── Entities/Inventory/Pos/CashRegister.cs                       (+ DirectPrint)
└── ElectronicInvoicing/ParametrosDeFacturacionElectronica.cs    (defecto 2 s, Dian.TopeDeIntentos)

src/Core/IngenIA365ERP.Application/
├── Common/Mensajeria/          IColaDeTrabajos, Aviso, TiposDeAviso, IManejadorDeAviso, PublicacionTrasElCommit, TopeDeIntentos
├── Common/DeadLetters/         ListDeadLettersQuery, GetDeadLetterQuery, ReprocessDeadLettersCommand(+Validator), DeadLetterDtos
├── ElectronicInvoicing/Documents/
│   ├── IntentoAnteElCanal.cs                 (transitorio vs definitivo, tope, muerte, FollowUpDueAt; sin representación ni entrega en línea)
│   ├── CompleteElectronicDocumentFollowUpCommand.cs  (nuevo: canónico, representación, entrega; idempotente)
│   └── ManejadoresDeAvisoFe.cs               (fe.emitir, fe.seguimiento)
├── Inventory/Pos/EsperaEnLineaDelPos.cs      (espera la fila; tirilla siempre; ya no cuenta fallas del circuito)
├── Inventory/Integration/EmisionFiscalDeLaConfirmacion.cs (FollowUpDueAt en vez de subir tras el commit; publica)
├── Common/Integration/RegisterDeliveryResultCommand.cs, EntregasElegibles.cs (tope, DeadAt, excluir muertas)
└── Common/Parameters/…                       (Integracion.TopeDeIntentos)

src/Infrastructure/IngenIA365ERP.Messaging/   (NUEVO; único con RabbitMQ.Client)
├── ColaRabbitMq.cs, ConexionRabbitMq.cs, TopologiaDeColas.cs, ConsumidorDeColas.cs (BackgroundService)
├── ColaEnProceso.cs, OpcionesDeMensajeria.cs, RabbitMqHealthCheck.cs, DependencyInjection.cs

src/Infrastructure/IngenIA365ERP.ElectronicInvoicing/Processor/ProcesadorDeDocumentosElectronicos.cs
                                              (barrido: publica lo vencido o lo hace; seguimientos; métricas por cooperativa)
src/Infrastructure/IngenIA365ERP.Persistence/ Configurations (+ columnas, índices), Migrations.PostgreSql|SqlServer/EmisionAsincrona

src/Presentation/IngenIA365ERP.API/
├── Program.cs                     (AddMessaging, métricas en puerto aparte, consumidor)
├── Integration/DespachadorDeMensajes.cs  (pasada en línea por cooperativa+destino reutilizable; publica avisos; tope)
├── Integration/ManejadorDeAvisoDeIntegracion.cs
├── Endpoints/Platform/DeadLettersEndpoints.cs
└── Observability/MetricasDeTrabajos.cs
src/Presentation/IngenIA365ERP.Shared/
├── Pages/Administracion/MensajesMuertos.razor (nuevo), bandeja FE (filtros y columnas), Pos (tirilla sin CUDE, impresión directa),
├── Pages/Inventario/… caja (DirectPrint), wwwroot/js/pos.js (copias sin vista previa)

tests/  Application.Tests (tope, muerte, espera, seguimiento), Architecture.Tests (2 reglas nuevas),
        API.IntegrationTests/Messaging (ApiConMensajeriaFixture + e2e del quickstart)
docs/manual/emision-asincrona-y-mensajes-muertos.md, docs/operaciones/mensajeria-rabbitmq.md
tools/scripts/crear-secreto-rabbitmq.ps1
D:/PSNL/wt-gitops: workloads/erp/components/mensajeria/, overlays (dev → qa → pdn), infrastructure/pdn/monitoreo.yaml, alertas
```

**Structure Decision**: Clean Architecture de siempre. El único proyecto nuevo es el adaptador de mensajería, por la misma razón por
la que el canal electrónico vive en el suyo: que la librería del broker no se filtre a Application ni a la API (salvo `Program.cs`).

## Phases

| Fase | Contenido | Sale con |
|---|---|---|
| A. Fundamentos | Puertos de mensajería, `ColaEnProceso`, columnas + migración, parámetros, alertas, permisos | compila; unitarias del tope |
| B. US1 + US2 | Cobro que espera la fila (D7), tirilla sin CUDE, intento sin representación/entrega, seguimiento durable, adaptador RabbitMQ, consumidor, barrido que publica o ejecuta | e2e de cobro y de cero pérdida |
| C. US3 + US4 | Tope, muerte, alertas, reproceso, pantalla de muertos, estado visible en venta y bandeja | e2e de tope y reproceso |
| D. US5 + US6 | Factura de oficina sin espera; canónico/representación/correo por seguimiento | e2e de oficina y seguimiento |
| E. US7 | Integración por la cola (aviso por cooperativa+destino), tope y muertos de entregas | e2e una sola vez con dos hosts |
| F. US9 | `DirectPrint` en caja, POS y MAUI | manual |
| G. US8 + despliegue | Métricas, salud, componente GitOps, Secret, scrape y alertas, runbook, manual | DEV verde |
| Polish | CLAUDE.md, totales, `/speckit-analyze`, PR contra develop | PR |

## Riesgos

- **Tirilla sin CUDE**: requiere el aval de la contadora antes de PDN (anotado en el runbook). COOFLOPAL está hoy en canal simulado.
- **Cuota de PDN** (`ResourceQuota` requests.memory 5Gi): se mide el uso real y se ajusta en el mismo commit del componente.
- **Dos procesos tocando el mismo documento** (barrido + consumidor + reproceso): el arrendamiento por fila ya lo resuelve; e2e con
  dos hosts lo fija.
- **Carga de las e2e**: el broker sólo en la fixture derivada, no en la base.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Broker compartido por las cooperativas de un ambiente (Principio IV nombra SQL, Mongo y Redis por cooperativa) | Reparto entre réplicas y despertar inmediato pedidos por el dueño (D2) | Un vhost o broker por cooperativa multiplica conexiones, colas y memoria en un VPS de 12 GB sin ganar aislamiento: los avisos sólo llevan PublicIds, ningún dato de negocio, y el trabajo siempre se hace contra la base de la cooperativa por `IEjecutorEnCooperativa`. |
| Proyecto nuevo `IngenIA365ERP.Messaging` | Aislar `RabbitMQ.Client` (Principio II, como el canal electrónico) | Ponerlo en Persistence o en la API mezclaría la librería del broker con capas que no deben conocerla. |
