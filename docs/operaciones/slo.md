# Service Level Objectives — Fase 0

> Promesa operacional del cimiento técnico (Fase 0). Cuando aterricen los
> módulos de negocio, cada uno declara sus propios SLOs adicionales.

## Disponibilidad

| Métrica | Objetivo |
| --- | --- |
| **API uptime mensual** | 99.5 % (≤ 3 h 39 min downtime/mes) |
| **Tiempo de respuesta p95 (`/api/*`)** | < 800 ms (excluye exports) |
| **Tiempo de respuesta p99 (`/api/*`)** | < 2 000 ms |
| **Audit log query p95 (rango ≤ 1 mes)** | < 5 s (SC-004) |

## Ventana de mantenimiento

- **Programada**: domingos 03:00–05:00 COT (UTC−5).
- **Notificación**: in-app + correo a `CompanyAdmin` con 72 h de antelación.
- **El tiempo de mantenimiento programado NO consume error budget** (cláusula
  contractual SaaS).

## Error budget

- 99.5 % mensual = **3 h 39 min** de error budget cada calendario.
- Cuando el budget restante baja del **30 %**, se congelan despliegues no
  críticos y todo el sprint pasa a estabilización.
- Cuando se agota: **post-mortem público** dentro de 5 días hábiles.

## Cómo medimos

- **Healthchecks** (T136):
  - `/health/live` — proceso vivo. Probe del orquestador cada 10 s.
  - `/health/ready` — SQL Server + MongoDB + Redis + BlobStore sanos. Si
    cualquier dep está down, el pod sale del pool de tráfico.
- **Latencia**: histograma desde Serilog request logging + dashboard.
- **Disponibilidad**: synthetic check externo contra `/health/ready` cada
  60 s desde 3 regiones; el 1 minuto que falla las 3 cuenta como downtime.

## SLOs adyacentes (Fase 0)

- **Audit log durabilidad**: 5 años retenidos en MongoDB con TTL (T026/T093).
- **BCrypt cost de passwords**: 100 % de hashes con cost ≥ 11 (SC-008, gate
  `PasswordHashIntegrityTests` T141a).
- **Notificaciones**: in-app entregado < 5 s; correo con 4 reintentos antes
  de marcar `Failed` y persistir `NotificationDeliveryFailure` (T119).
- **Adjuntos**: cifrados AES-256-GCM en reposo (FR-031, T106); GC de blobs
  huérfanos < 24 h tras soft-delete de la metadata.
