# Data model — 023 Emisión electrónica asíncrona

Todo es **aditivo** y vive en la base de cada cooperativa. Una sola migración par (PostgreSQL y SQL Server),
`EmisionAsincrona`, sin datos que mover salvo el defecto de los parámetros (que viven en el catálogo, no en la base).

## 1. `COR_ElectronicDocuments` (existente) — columnas nuevas

| Columna | Tipo | Nulo | Para qué |
|---|---|---|---|
| `TransientFailures` | int, default 0 | no | Intentos transitorios seguidos desde el último reinicio (R6). Lo que mide el tope. |
| `DeadAt` | datetime | sí | Momento en que agotó el tope. Con valor: «muerto»; `NextAttemptAt` queda nulo. |
| `DeadAttemptLimit` | int | sí | El tope vigente al morir (informativo; el tope puede cambiar después). |
| `EnqueuedAt` | datetime | sí | Último aviso confirmado por el broker. Lo usa el barrido para no republicar lo que ya está en la cola. |
| `FollowUpDueAt` | datetime | sí | Orden durable del seguimiento (representación, canónico, correo). Nula = nada pendiente. |
| `FollowUpAttempts` | int, default 0 | no | Intentos del seguimiento (mismo tope `Dian.TopeDeIntentos`). |
| `FollowUpDeadAt` | datetime | sí | El seguimiento agotó su tope. |
| `FollowUpLastError` | varchar(500) | sí | Último motivo del seguimiento. |

Columnas de reintento/arrendamiento ya llevan `[NoAuditar]`; las nuevas también (su historia es la transmisión y la auditoría del
reproceso).

**Índices nuevos** (filtrados donde el motor lo permite):
- `IX_COR_ElectronicDocuments_Dead` sobre `(DeadAt)` filtrado `DeadAt IS NOT NULL` — la pantalla de muertos.
- `IX_COR_ElectronicDocuments_FollowUp` sobre `(FollowUpDueAt)` filtrado `FollowUpDueAt IS NOT NULL AND FollowUpDeadAt IS NULL`.

### Estado derivado (no se agrega valor al enum)

`ElectronicDocumentStatus` no cambia (lo gobierna `TransicionesDelDocumentoElectronico`). El estado que ve la persona se deriva:

| Mostrado | Regla |
|---|---|
| Pendiente de envío | `Pending`, `AttemptCount = 0` |
| En reintento | `Pending` o `Sent`, `AttemptCount > 0`, `DeadAt` nulo — muestra `TransientFailures` / tope y `NextAttemptAt` |
| Enviado | `Sent`, `TransientFailures = 0` |
| Aceptado / con notificaciones | `Validated` / `ValidatedWithNotices` |
| Rechazado | `Rejected` |
| Muerto | `DeadAt` con valor |
| En contingencia | `IssuerContingency` / `DianContingency` (más «en reintento» o «muerto» si aplica) |

### Transiciones nuevas

```text
registrar resultado transitorio:  TransientFailures++ ;
    si TransientFailures ≥ tope y (no contingencia o ahora ≥ TransmissionDeadline): DeadAt=ahora, DeadAttemptLimit=tope, NextAttemptAt=null, alerta
    si no: NextAttemptAt = ahora + EsperasDeReintento.Tras(AttemptCount)
registrar respuesta definitiva (validado, rechazado, contingencia aceptada): TransientFailures = 0
entregable (validado o 04 recién abierta): FollowUpDueAt = ahora (en la MISMA transacción del resultado)
confirmación que registra el documento: FollowUpDueAt = ahora (sube el canónico)
reprocesar muerto: DeadAt=null, DeadAttemptLimit=null, TransientFailures=0, NextAttemptAt=ahora   (+ auditoría)
reprocesar seguimiento muerto: FollowUpDeadAt=null, FollowUpAttempts=0, FollowUpDueAt=ahora
seguimiento completo: FollowUpDueAt=null, FollowUpLastError=null
```

## 2. `COR_IntegrationMessageDeliveries` (existente) — columnas nuevas

| Columna | Tipo | Nulo | Para qué |
|---|---|---|---|
| `TransientFailures` | int, default 0 | no | Como en documentos. `Attempts` sigue siendo la historia. |
| `DeadAt` | datetime | sí | Agotó `Integracion.TopeDeIntentos`; `NextAttemptAt` nulo; sale de `EntregasElegibles`. |
| `DeadAttemptLimit` | int | sí | Tope al morir. |

`DeliveryStatus` no cambia: una entrega muerta sigue `Pending` con `DeadAt`. `EntregasElegibles.EnLineaAsync` y la de lotes
excluyen `DeadAt IS NOT NULL`. Las que dependen de una muerta (aristas §9) esperan, como hoy esperan a una pendiente.

Índice `IX_COR_IntegrationMessageDeliveries_Dead` sobre `(DeadAt)` filtrado.

## 3. `INV_CashRegisters` (existente) — columna nueva

| Columna | Tipo | Nulo | Para qué |
|---|---|---|---|
| `DirectPrint` | bool, default false | no | Imprimir directo a la impresora predeterminada, sin vista previa (D5). |

## 4. Parámetros (catálogo, `COR_ParameterVersions`)

| Clave | Tipo | Defecto | Ámbito | Nota |
|---|---|---|---|---|
| `Dian.EsperaMaximaPosSegundos` | entero 0–15 | **2** (antes 15) | cooperativa, punto de venta | Cambia defecto y descripción (D7). Versiones guardadas siguen rigiendo. |
| `Dian.TopeDeIntentos` | entero 1–100 | 12 | cooperativa | Nuevo (D3). Documentos y su seguimiento. |
| `Integracion.TopeDeIntentos` | entero 1–100 | 12 | cooperativa | Nuevo (D3). Entregas de la bandeja. |

## 5. Alertas (catálogo `TiposDeAlerta`, sembrado)

| Tipo | Cuándo | DedupKey | Se atiende |
|---|---|---|---|
| `Dian.DocumentoSinEnviar` | documento muerto | `{tipo}:{documento:N}` | al reprocesar o al validar |
| `Dian.SeguimientoDetenido` | seguimiento muerto | `{tipo}:{documento:N}` | al reprocesar o completar |
| `Integracion.EntregaDetenida` | entrega muerta | `{tipo}:{entrega:N}` | al reprocesar o procesar |

## 6. Permisos (los siembra la API al arrancar)

| Permiso | Roles sembrados |
|---|---|
| `Platform.DeadLetters.View` | `CompanyAdmin`; `Auditor`, `Operator` y `ReadOnly` lo reciben por su comodín `*.View` |
| `Platform.DeadLetters.Reprocess` | `CompanyAdmin` |

No existe un rol «Supervisor» sembrado (los cuatro son `CompanyAdmin`, `Auditor`, `Operator`, `ReadOnly`): el supervisor de caja de
una cooperativa es un rol propio al que el administrador le da `View` (y `Reprocess` si quiere). El manual lo dice.

## 7. Auditoría

`Platform.DeadLetters.Reprocessed` por `COR_AuditOutbox` (cadena de sellos) con `{ kind, ids[], reason }`. El cambio de
`DirectPrint` lo audita el comando de la caja como cualquier otro campo.

## 8. Lo que NO es dato

- El aviso en RabbitMQ (`contracts/mensajeria.md`): efímero, sin datos de negocio, nunca fuente de verdad.
- Las métricas: se calculan en el barrido; nada se guarda.
