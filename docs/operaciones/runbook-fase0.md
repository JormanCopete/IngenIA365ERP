# Runbook operacional — Fase 0

> Procedimientos para incidentes comunes del cimiento técnico.
> Cada sección sigue el patrón **Síntomas → Diagnóstico → Mitigación → Postmortem**.

## 1. Lockout masivo de cuentas

**Síntomas**: pico de notificaciones `AccountLocked`; usuarios reportan
"contraseña correcta no funciona"; soporte con tickets en cascada.

**Diagnóstico**:
- Revisar el contador en Redis, que es donde vive el bloqueo:
  `bloqueo:password:<CORREO EN MAYÚSCULAS>` (existe y con TTL ⇒ bloqueado) e
  `intentos:password:<CORREO EN MAYÚSCULAS>` (cuántos fallos lleva).
  **No** mirar `SEC_Users.FailedLoginAttempts` ni `LockoutEndAt`: esas columnas
  quedaron sin nadie que las escriba cuando el login pasó a la identidad
  central, y responden siempre lo mismo.
- Para desbloquear a alguien, la pantalla de usuarios ya limpia ese contador;
  a mano es `DEL` de las dos claves.
- Cruzar IPs en audit log (`Module=Security`, `Action=LoginFailed`) — ¿es
  un ataque distribuido o un cliente legítimo con cache de password viejo?
- Revisar dashboard de latencia: ¿`/api/auth/login` está timeouting y los
  reintentos del cliente cuentan como fallos?

**Mitigación**:
- **Si ataque**: agregar IPs ofensoras a la deny-list del rate limiter
  (`AspNetCoreRateLimit` ya activo); cambiar threshold de lockout
  temporalmente vía `appsettings.Production.json` → `Security:LockoutPolicy`.
- **Si latencia**: revisar `/health/ready`. Si SQL Server p99 > 2 s, ver §3.
- **Si cliente legítimo**: `POST /api/admin/users/{publicId}/unlock`
  (permiso `Security.Users.Unlock`).

**Postmortem**: documentar IPs, tenant afectado, vector y duración.

---

## 2. Fallo de envío de correo (SMTP down)

**Síntomas**: `NotificationDeliveryFailure` creciendo; dashboard muestra
`COR_Notifications.EmailStatus = 'Failed'` en aumento.

**Diagnóstico**:
- `SELECT TOP 50 FailedAt, ErrorMessage FROM COR_NotificationDeliveryFailures ORDER BY FailedAt DESC`.
- Probar SMTP directo: `telnet smtp.tenant.com 587` desde el pod de la API.
- Revisar credenciales: rotación reciente puede haber invalidado el password.

**Mitigación**:
- **Si SMTP del proveedor down**: nada que hacer salvo esperar. Las
  notificaciones in-app siguen visibles (best-effort design — T119).
- **Si credenciales rotadas**: actualizar `Smtp:Username`/`Smtp:Password`
  en el vault; reciclar pods.
- **Si DNS roto**: ajustar `Smtp:Host` al IP directamente, mientras se
  arregla DNS.
- Tras restablecer, el dispatcher (`NotificationEmailDispatcher`) levanta
  las `Pending` automáticamente en su próximo poll (15 s).

**Postmortem**: causa raíz, número de notificaciones impactadas, lag total.

---

## 3. MongoDB down (audit log)

**Síntomas**: `AuditBehavior` lanza excepciones en logs; algunos handlers
fallan al cerrar — el write a Mongo es síncrono.

**Diagnóstico**:
- `/health/ready` reporta `mongodb: Unhealthy`.
- Logs Serilog con `MongoConnectionException`.
- Verificar cluster Mongo (réplicas, primary election).

**Mitigación**:
- **Modo emergencia**: cambiar variable de entorno
  `AUDIT__FALLBACK_TO_DISK=true` (TBD: requiere implementación). Mientras
  tanto, `AppendOnlyAuditWriter` retorna éxito y los events se pierden —
  esto es un compromiso documentado para no romper operación, **pero
  rompe la promesa append-only** y debe corregirse antes del próximo
  cierre regulatorio.
- Reiniciar el primary de Mongo / dejar que el secondary haga failover.
- Una vez recuperado, ejecutar `AuditIndexBootstrap` (T026) manualmente
  para asegurar índices.

**Postmortem**: tiempo sin audit log, # de operaciones afectadas, plan
para implementar fallback persistente real.

---

## 4. Rotación de claves RSA (JWT) o KEK (DataProtection)

**Cuándo**: programada cada 12 meses, o inmediata tras sospecha de leak.

**Procedimiento (RSA — JWT signing)**:

> **No hay anillo de claves.** Este runbook describía `IRsaKeyProvider.RotateAsync()`
> y una convivencia de 24 h entre la clave vieja y la nueva. Nada de eso existe:
> hay **una** clave, la del PEM en `JwtSettings:PrivateKeyPath`, cargada al
> arrancar. Cambiarla invalida **todos** los tokens en circulación de golpe,
> incluidos los que están en vuelo. No es un defecto que se pueda esquivar con
> el procedimiento; es la consecuencia de tener una sola clave, y hay que
> planificar la ventana en consecuencia.

1. Generar el par nuevo y dejarlo donde apunte `JwtSettings:PrivateKeyPath`
   (montado como secreto en el despliegue, nunca versionado).
2. Reiniciar el servicio. Desde ese instante, todo access y refresh token
   emitido con la clave anterior deja de validar: cada persona vuelve a iniciar
   sesión, **con su segundo factor**.
3. Comprobar que el arranque no cayó a una clave efímera: en `Production` el
   servicio falla al arrancar si no encuentra el PEM (`RsaKeyGuard`), y ese
   fallo es la señal de que el secreto no se montó bien. Un arranque limpio con
   login correcto es la verificación.
4. Si lo que se filtró es un **token** y no la clave, no hace falta rotar nada:
   cada persona privilegiada llama a `POST /api/auth/logout-all` con **su
   propio** token. Rota su `SecurityStamp` y con eso caen todas sus sesiones, en
   todos sus dispositivos. No es un endpoint de administración: cierra las
   sesiones de quien llama, no las de otro.

**Procedimiento (KEK — DataProtection)**:
1. `IDataProtectionProvider.CreateProtector(...)` mantiene N claves en su
   ring. Rotar = nueva clave activa, viejas siguen para `Unprotect`.
2. **PDFs firmados (T089)**: la `KeyVersion` en `AuditPdfExport` se incluye
   en `X-Audit-Key-Version` — el verificador SaaS acepta claves anteriores
   listadas en `AuditSignatureSettings.Keys[]` (T089). Mantenerlas mientras
   haya PDFs vigentes (TTL audit log = 5 años).
3. **Adjuntos cifrados (T106)**: las DEK envueltas con la KEK vieja se
   re-envuelven on-demand al descifrar — no requiere migración batch.

**Postmortem**: registrar la rotación en `audit_events` con `Action=KeyRotated`.

---

## 5. Rate limiter saturado (false positives)

**Síntomas**: cliente legítimo recibe 429; soporte con tickets de
"se me bloquea aleatoriamente".

**Diagnóstico**:
- Revisar `AspNetCoreRateLimit:IpRateLimiting` en `appsettings.json`.
- ¿El cliente está detrás de NAT compartido? Múltiples usuarios con misma IP.

**Mitigación**:
- Subir umbrales para el endpoint específico.
- Cambiar a rate limiting por `ClientId` (header) en lugar de IP cuando hay NAT.

**Postmortem**: ajustar baseline numérico.

---

## 6. Consola de operaciones rápidas

| Comando | Cuándo |
| --- | --- |
| `curl https://api/health/ready \| jq` | Triage inicial — confirma deps |
| `dotnet test tests/IngenIA365ERP.Architecture.Tests` | Antes de cualquier merge crítico |
| `dotnet run --project tools/MessageLintTool/ -- .` | Pre-merge, FR-048 |
| `gh workflow run load-nightly.yml` | Manual antes de release |

---

## Contactos

- **On-call P0**: ingenieria-fase0@ingenia365.local (rotación semanal).
- **Compliance/Regulatorio**: legal@ingenia365.local (incidentes que toquen
  habeas data o audit log).
- **Cliente impactado**: el `CompanyAdmin` del tenant recibe in-app + correo
  automático en P0/P1.
