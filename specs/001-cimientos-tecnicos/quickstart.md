# Quickstart — Fase 0 Cimientos técnicos

**Feature**: 001-cimientos-tecnicos
**Audience**: dev y QA que validan el cierre de Fase 0.
**Duración estimada**: 45–60 min.

Este recorrido demuestra los 7 user stories del spec + SC-013 (uptime medible) sobre el entorno local. Cada bloque referencia los FR y SC correspondientes.

---

## Prerrequisitos

```bash
dotnet --version            # >= 10.0.5
docker --version            # SQL Server + MongoDB efímeros
git status                  # rama 001-cimientos-tecnicos
```

Servicios externos vía Docker Compose:

```bash
docker compose -f docker/dev.yml up -d sqlserver mongodb redis smtp4dev
```

`smtp4dev` actúa como buzón SMTP local (UI en `http://localhost:5000/smtp4dev`).

Migración inicial y siembra:

```bash
dotnet ef database update --project src/Infrastructure/IngenIA365ERP.Persistence
dotnet run --project src/Presentation/IngenIA365ERP.API -- seed --tenant=demo --nit=900111111-1
```

El seed crea:
- Tenant `demo` (NIT 900111111-1) con schema `tnt_demo`.
- Sucursal `MAT` (casa matriz).
- Cuatro roles built-in (`CompanyAdmin`, `Auditor`, `Operator`, `ReadOnly`).
- Catálogo global de permisos.
- Usuario admin inicial `admin@demo / Demo123!` con `MustChangePassword = true` y MFA desactivado.
- Política habeas data v1.0.0 vigente.

---

## US1 — Acceso seguro multi-empresa con MFA (FR-001..FR-013, SC-001)

### Paso 1.1 — Cambio obligatorio de contraseña

```bash
curl -X POST http://localhost:5290/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Demo123!"}'
```

Respuesta esperada: `mustChangePassword: true` + `mfaChallengeToken`.

```bash
curl -X POST http://localhost:5290/api/auth/password/change \
  -H "Authorization: Bearer <token-de-cambio>" \
  -d '{"currentPassword":"Demo123!","newPassword":"NuevoSegura!2026"}'
```

### Paso 1.2 — Inscripción MFA

```bash
curl -X POST http://localhost:5290/api/auth/mfa/enroll/start \
  -H "Authorization: Bearer <token>" \
  -d '{"password":"NuevoSegura!2026"}'
```

Escanear el SVG QR con la app autenticadora (Google Authenticator/Authy). Confirmar:

```bash
curl -X POST http://localhost:5290/api/auth/mfa/enroll/confirm \
  -d '{"enrollmentToken":"...","totpCode":"123456"}'
```

Guardar los 10 backup codes (estarán en la respuesta UNA SOLA VEZ).

### Paso 1.3 — Login completo con MFA

```bash
curl -X POST http://localhost:5290/api/auth/login \
  -d '{"username":"admin","password":"NuevoSegura!2026"}'
```

```bash
curl -X POST http://localhost:5290/api/auth/mfa/verify \
  -d '{"mfaChallengeToken":"...","totpCode":"123456","branchPublicId":"<MAT.PublicId>"}'
```

Respuesta `accessToken` (30 min) + `refreshToken` (12 h). Validar el TTL en el payload del JWT.

**Verificación independiente** (5 intentos fallidos → bloqueo):
```bash
for i in {1..5}; do curl -X POST .../mfa/verify -d '{"...":"badcode"}'; done
```

Espera respuesta `Auth.AccountLocked` + correo `AccountLocked` en smtp4dev (FR-011).

---

## US2 — Administración de usuarios, roles y permisos granulares (FR-014..FR-020, SC-005)

### Paso 2.1 — Crear roles "Operador de Nómina" y "Auditor"

UI: Admin → Seguridad → Roles → "Nuevo rol" → marcar permisos por entidad/acción.

Equivalente CLI:

```bash
curl -X POST .../api/admin/roles \
  -H "Authorization: Bearer <accessToken>" \
  -d '{"code":"PayrollOperator","name":"Operador de Nómina","permissionCodes":["Employees.View","Employees.Create","Employees.Update"]}'
```

### Paso 2.2 — Probar invisibilidad de endpoint sin permiso

Crear usuario `opnomina` con rol `PayrollOperator`. Loguearse. Llamar:

```bash
curl -X POST .../api/payroll/employees/<id>/approve \
  -H "Authorization: Bearer <token de opnomina>"
```

Espera `404 Not Found` con payload `{ "code": "Generic.NotFound", "message": "Recurso no encontrado." }` — **indistinguible** de un endpoint inexistente (FR-017, SC-005).

### Paso 2.3 — Cambio de permisos sin reinicio

UI: agregar permiso `Employees.Approve` al rol `PayrollOperator`. El siguiente refresh del access token de `opnomina` (≤ 30 min) lo refleja; con refresh inmediato sí (FR-019).

---

## US3 — Audit log inmutable consultable (FR-021..FR-026, SC-003, SC-004, SC-007)

### Paso 3.1 — Generar tráfico auditable

Ejecutar 10 operaciones de creación/edición/eliminación desde dos usuarios distintos (UI o curl).

### Paso 3.2 — Consultar audit log

UI: Auditoría → Consola → filtrar por usuario, rango, entidad.

CLI:

```bash
curl ".../api/audit/events?from=2026-05-28T00:00:00Z&to=2026-05-28T23:59:59Z&pageSize=100" \
  -H "Authorization: Bearer <auditor-token>"
```

Verificar que las 10 entradas aparecen con `timestamp`, `username`, `ip`, `entityType`, `entityPublicId`, `changes`.

### Paso 3.3 — Intentar modificar una entrada

```bash
# Esto debe fallar a nivel de driver:
mongosh ingenia365_audit --eval 'db.audit_events.updateOne({}, {$set:{result:"Tampered"}})'
```

Espera error `not authorized on ingenia365_audit to execute command update` (rol Mongo `appendOnly`).

### Paso 3.4 — Aislamiento por empresa

Crear tenant `demo2`. Autenticarse como auditor de `demo2`. Llamar `/api/audit/events` y verificar que ninguna entrada del tenant `demo` aparece (FR-025).

### Paso 3.5 — Performance

```bash
time curl ".../api/audit/events?from=2026-04-28T00:00:00Z&to=2026-05-28T23:59:59Z&pageSize=200"
```

Tiempo total < 5 s para ventana de 1 mes (SC-004).

---

## US4 — Soft-delete universal y restauración (FR-027..FR-031)

### Paso 4.1 — Eliminar un usuario

```bash
curl -X POST .../api/admin/users/<publicId>/disable \
  -d '{"reason":"Prueba quickstart","concurrencyToken":"..."}'
```

Validar que ya no aparece en `GET /api/admin/users` por defecto, pero sí con `?includeDisabled=true`.

### Paso 4.2 — Restaurar

```bash
curl -X POST .../api/admin/users/<publicId>/restore
```

Audit log refleja `Delete` y `Restore` con autor y timestamp.

### Paso 4.3 — Intentar borrar movimiento contable (futura fase — debe fallar)

Esta validación queda como hook de Architecture.Test que rechaza cualquier `IRequest<Result>` que haga `DELETE FROM ACC_JournalEntries` (principio XI).

---

## US5 — Adjuntos seguros y cifrados (FR-032..FR-036, SC-009)

### Paso 5.1 — Subir PDF

```bash
curl -X POST .../api/attachments \
  -H "Authorization: Bearer <token>" \
  -F "ownerEntityType=User" \
  -F "ownerEntityPublicId=<userPublicId>" \
  -F "file=@cedula.pdf"
```

### Paso 5.2 — Verificar cifrado en reposo

```bash
ls -la storage/attachments/<tenant>/<year>/<month>/
file storage/attachments/<tenant>/<year>/<month>/<hashed-name>
```

Espera `data` (binario sin firma PDF). `cat` del archivo no produce texto legible.

### Paso 5.3 — Descargar y validar SHA-256

```bash
curl -O .../api/attachments/<publicId>
sha256sum cedula.pdf <archivo descargado>
```

Hash idéntico al original.

### Paso 5.4 — Intentar descargar con usuario sin permiso

Loguearse como `ReadOnly` que no tiene `Users.View`. Llamar GET → respuesta 404 indistinguible. Audit log captura el intento.

---

## US6 — Notificaciones (FR-037..FR-040, SC-010)

### Paso 6.1 — Generar notificación de prueba

Provocar bloqueo de cuenta (paso 1.3 con 5 intentos fallidos) o asignar un nuevo rol a un usuario.

### Paso 6.2 — Verificar entrega

- **In-app**: abrir `Notificaciones → Centro` en Blazor → la entrada aparece sin recargar (push SignalR).
- **Email**: abrir smtp4dev en `http://localhost:5000/smtp4dev` → el correo está allí en menos de 60 s (SC-010).

### Paso 6.3 — Marcar como leída

UI: clic en la notificación. Verificar que `unreadCount` baja en 1 y persiste tras refresh.

### Paso 6.4 — Simular fallo SMTP

Detener smtp4dev. Provocar otra notificación. El log de aplicación muestra los 3 reintentos; al agotarse, `NOT_NotificationDeliveryFailures` tiene la entrada. La notificación in-app sigue visible.

---

## US7 — Habeas data (FR-041..FR-044)

### Paso 7.1 — Publicar v2.0.0

```bash
curl -X POST .../api/compliance/habeas-data/policies \
  -d '{"versionLabel":"2.0.0","title":"Política v2","bodyMarkdown":"...","effectiveFrom":"2026-06-01T00:00:00Z"}'
```

### Paso 7.2 — Registrar consentimiento

```bash
curl -X POST .../api/compliance/habeas-data/consents \
  -d '{"personPublicId":"<id>","policyVersionPublicId":"<v2id>","evidenceText":"...","captureChannel":"WebForm","ip":"10.0.0.5"}'
```

### Paso 7.3 — Revocar y verificar propagación

```bash
curl -X POST .../api/compliance/habeas-data/revocations \
  -d '{"personPublicId":"<id>","reason":"Solicitud del titular"}'
```

Validar:
- `GET /persons/<id>/history` muestra ambos eventos.
- En logs del backend aparece `HabeasDataRevokedEvent` publicado por MediatR (Fase 0 todavía no tiene consumidores; sirve como verificación del cableado).

---

## Verificación cruzada — SC-002 (carga) y SC-013 (uptime)

### SC-002 — 100 usuarios concurrentes

```bash
dotnet run --project tests/IngenIA365ERP.Load.Tests -- --scenario Cimientos.Concurrency.100Users
```

Reporte HTML en `tests/IngenIA365ERP.Load.Tests/reports/`. Validar p95 < 2 s y error rate < 1%.

### SC-013 — Disponibilidad

No se mide aquí (requiere observación en producción), pero se valida que:
- El monitor de health (`/health/ready`) responde 200 en < 100 ms.
- Existe runbook de mantenimiento con preaviso 48 h.
- El SLO está documentado en `docs/operaciones/slo.md` (creado por `/speckit-tasks`).

---

## Limpieza

```bash
docker compose -f docker/dev.yml down -v
```

---

## Checklist de cierre de Fase 0

- [ ] 7 user stories ejecutados end-to-end.
- [ ] SC-001 a SC-013 verificados (o documentado el método de verificación si requiere observación).
- [ ] Architecture.Tests verdes (12 principios constitucionales).
- [ ] Integration tests verdes.
- [ ] NBomber report archivado.
- [ ] CHANGELOG actualizado.
