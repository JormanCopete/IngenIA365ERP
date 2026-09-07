---
title: "Guía de Prueba en Producción — Identidad Central Federada"
subtitle: "Fase 1 · Feature 002 · IngenIA365ERP"
author: "Equipo INGENIA 365"
date: "2026-07-26"
---

# Guía de Prueba en Producción — Identidad Central

Este documento guía una prueba **end-to-end** del feature *Identidad Central
Federada* como si estuvieras corriendo el aplicativo en producción real. Todos
los pasos se ejecutan con las mismas llamadas HTTP que la app usará en prod
(no hay atajos de dev).

Está pensado para: QA, líder técnico, sponsor de negocio y cualquiera que
quiera verificar en un ambiente parecido a prod que los 6 flujos críticos
funcionan.

---

## 0. Antes de empezar

### 0.1 Ambiente

| Componente | Valor esperado en prod |
|---|---|
| API pública | `https://api.ingenia365.com` (o el dominio que uses) |
| Web pública | `https://app.ingenia365.com` |
| Correo real | SMTP corporativo (SendGrid / SES / M365) |
| SQL Server | Instancia gestionada con backups |
| MongoDB | Réplica con TLS |
| Redis | Cluster con contraseña + TLS |

> Reemplazá `https://api.ingenia365.com` por la URL real del ambiente donde
> pruebes en cada bloque `curl` de esta guía.

### 0.2 Datos que necesitás preparar antes

1. **Correo del master admin y contraseña inicial**
   Debe existir un `CentralUser` con `IsGlobalMasterAdmin = 1` sembrado vía
   `database/migration/16_Seed_Default_GlobalMasterAdmin.sql` con hash BCrypt
   cost 11. Guardá el email y la contraseña.

2. **Correo de un admin de empresa piloto**
   Alguien que efectivamente pueda abrir su correo (no una casilla dummy) y
   quiera aceptar la invitación. Ejemplo: `admin@cooperativa-piloto.com`.

3. **NIT y razón social de la empresa piloto**
   Se persiste en el registro del tenant y no se puede duplicar por NIT.

4. **Correo de al menos un usuario adicional**
   Para que el admin de la empresa lo invite en el paso 7 y probar el flujo
   de invitación no-admin.

### 0.3 Herramientas del probador

| Herramienta | Para qué |
|---|---|
| Navegador moderno (Chrome/Edge) | Abrir la Web y los links del correo |
| Cliente de correo real | Recibir invitaciones y links de reset |
| Terminal (PowerShell / cURL) | Verificar respuestas de la API |
| Autenticador MFA (Google Authenticator, Authy, 1Password) | Para el paso opcional de activar MFA |

---

## 1. Paso 1 — Login del master admin

**Actor**: Master admin
**Objetivo**: Obtener el JWT operativo del master.

### 1.1 Vía UI

1. Abrí `https://app.ingenia365.com/login`.
2. Ingresá el email del master y su contraseña.
3. Debés ver el dashboard principal con opciones de administración global
   (no específico de un tenant).

### 1.2 Vía cURL (para QA)

```bash
curl -s -X POST https://api.ingenia365.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"master@tu-dominio.com","password":"TU_PASSWORD"}'
```

> **El maestro NO entra con sólo contraseña.** Desde la constitución v2.0.0 el
> segundo factor es obligatorio también para él — es la cuenta que crea
> cooperativas, apaga la política de MFA de una cooperativa ajena y borra el
> segundo factor de cualquiera. Este paso son **dos llamadas**, no una.

**Respuesta esperada** (HTTP 200) — una de estas dos:

```json
{ "challenge": "MfaEnrollmentRequired", "challengeToken": "eyJ…", "isGlobalMasterAdmin": true }
```

La primera vez, porque el maestro todavía no tiene segundo factor. Inscribilo
con el `challengeToken`:

```bash
curl -X POST "$API/api/profile/mfa/enroll" -H "Authorization: Bearer $CHALLENGE"
# → devuelve secretBase32 y el QR. Cargalo en tu app de autenticación.

curl -X POST "$API/api/profile/mfa/confirm" -H "Authorization: Bearer $CHALLENGE" \
  -H "Content-Type: application/json" -d '{"code":"<6 dígitos>"}'
# → devuelve los códigos de recuperación. GUARDALOS: con un solo maestro
#   son la única vía de vuelta si perdés el teléfono.
```

Después repetí el login. A partir de ahí, y siempre:

```json
{ "challenge": "MfaRequired", "challengeToken": "eyJ…", "isGlobalMasterAdmin": true }
```

```bash
curl -X POST "$API/api/auth/mfa/verify" -H "Authorization: Bearer $CHALLENGE" \
  -H "Content-Type: application/json" -d '{"code":"<6 dígitos>","useRecoveryCode":false}'
```

**Ésa** es la respuesta que trae la sesión:

```json
{
  "challenge": "None",
  "centralUserId": "…",
  "isGlobalMasterAdmin": true,
  "accessToken": "eyJ…",
  "refreshToken": "…",
  "activeTenants": []
}
```

**Guardá `accessToken`** — lo usás en el siguiente paso.

**Validaciones**:

- ✅ El login devuelve `challenge` = `MfaRequired` (o `MfaEnrollmentRequired` la
  primera vez) y **sin** `accessToken`. Si te devolviera un `accessToken`
  directamente, el maestro estaría entrando sin segundo factor: eso es un
  defecto, no un atajo.
- ✅ `mfa/verify` devuelve `challenge` = `None` y `isGlobalMasterAdmin` = `true`
- ✅ El JWT (podés decodificarlo en jwt.io) contiene:
  `purpose=full`, `is_global_master_admin=true`, `active_tenant_id` ausente

**Si falla**:

| Síntoma | Causa probable |
|---|---|
| HTTP 401 `Identity.InvalidCredentials` | Password incorrecto o hash mal sembrado |
| HTTP 401 `Identity.MfaInvalid` | Código TOTP incorrecto o reloj desfasado |
| HTTP 401 `Identity.Locked.Soft` | Demasiados intentos → esperá los segundos que indica el mensaje |

---

## 2. Paso 2 — Registrar la primera empresa (tenant)

**Actor**: Master admin
**Objetivo**: Crear el tenant y disparar automáticamente la invitación al
primer admin de esa empresa.

### 2.1 Vía UI

1. Menú lateral → **Empresas** → **Nueva empresa**.
2. Completá el formulario:
   - Nombre comercial: `Cooperativa Piloto`
   - Razón social: `Cooperativa Piloto SAS`
   - NIT: `900555444`
   - Plan: `Basic`
   - Máx. usuarios: `50`
   - Storage MB: `5120`
   - Email del primer admin: `admin@cooperativa-piloto.com`
3. **Guardar**. La Web debe mostrar un toast con "Tenant creado + invitación
   enviada" y navegar al detalle del tenant.

### 2.2 Vía cURL

```bash
curl -s -X POST https://api.ingenia365.com/api/saas/tenants/with-admin \
  -H "Authorization: Bearer <accessToken del master>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Cooperativa Piloto",
    "schemaName": "tenant_piloto",
    "nit": "900555444",
    "legalName": "Cooperativa Piloto SAS",
    "contactEmail": "info@cooperativa-piloto.com",
    "planType": "Basic",
    "maxUsers": 50,
    "storageLimitMb": 5120,
    "firstAdminEmail": "admin@cooperativa-piloto.com"
  }'
```

> **`schemaName` ya no nombra un esquema: nombra la BASE DE DATOS física de la
> cooperativa.** El campo conserva el nombre por compatibilidad, pero desde la
> constitución v2.0.0 este POST **crea una base de datos** —más su base de
> auditoría en MongoDB y su ranura en Redis—, no un espacio dentro de una base
> compartida. Elegí el nombre con ese criterio: es el que verás en el motor.

**Respuesta esperada** (HTTP 200):

```json
{
  "tenantPublicId": "…",
  "invitationPublicId": "…",
  "invitationExpiresAt": "2026-08-02T…",
  "correoEnviado": true,
  "motivoCorreoNoEnviado": null
}
```

**Validaciones**:

- ✅ HTTP 200 (no 500)
- ✅ Los 3 IDs vienen no-null
- ✅ Detrás de escena se emitió el evento auditable `Tenant.Created`
- ✅ **`correoEnviado` es `true`.** No lo des por hecho a partir del 200: la
  empresa y la invitación se crean igual aunque el servidor de correo esté
  caído, y en ese caso la respuesta llega igual con 200 pero con
  `correoEnviado: false` y el motivo en `motivoCorreoNoEnviado`. Si ves
  `false`, **nadie recibió el correo**: la invitación ya existe y hay que
  reenviarla desde `/admin/tenants/{tenantPublicId}/invitaciones`
  (`POST /api/tenants/{tenantPublicId}/invitations/{invitationPublicId}/reenviar`).
  Seguí entonces por la tabla de "Si no llegó" del paso 3.

**Si falla con `Tenant.NitConflict`**: ya existe un tenant con ese NIT —
elegí otro NIT o buscá el existente.

---

## 3. Paso 3 — Verificar el correo de invitación

**Actor**: Admin de la empresa piloto (`admin@cooperativa-piloto.com`)
**Objetivo**: Confirmar que el correo llegó al buzón real y contiene el link
correcto.

1. Abrí el buzón del correo del admin (Gmail, Outlook, etc.).
2. Buscá el correo con asunto **"Invitación a Cooperativa Piloto — IngenIA365ERP"**.
3. Verificá:
   - Remitente correcto (dominio corporativo)
   - Cuerpo contiene el nombre de la empresa (`Cooperativa Piloto`)
   - Botón "Aceptar invitación" es visible y no está roto
   - Link expira en 7 días (indicado en el correo)
   - No fue marcado como spam

**Si no llegó**:

| Síntoma | Acción |
|---|---|
| No aparece en bandeja de entrada | Revisar carpeta Spam / Promociones |
| SMTP corporativo bloqueó el envío | Revisar logs del dispatcher: `NotificationEmailDispatcher` |
| Link apunta a dominio incorrecto | Verificar `IdentityEmail:BaseUrl` en la config de prod (o la variable `IdentityEmail__BaseUrl`). Si el enlace dice `localhost:7200`, esa clave no está definida y rige el valor cableado por defecto |
| Nada sale del servidor de correo | Revisar la sección `Smtp` (`Smtp__Host`, `Smtp__Port`, `Smtp__UseStartTls`, `Smtp__Username`, `Smtp__Password`). Ver [`correo-saliente.md`](correo-saliente.md) |

---

## 4. Paso 4 — Aceptar la invitación (crear identidad)

**Actor**: Admin de la empresa (el que recibió el correo)
**Objetivo**: Crear su CentralUser + membership como tenant admin.

### 4.1 Vía UI (recomendado en pruebas de prod)

1. En el correo, hacé clic en **"Aceptar invitación"**.
2. La Web abre `/auth/accept-invitation?token=…`.
3. La pantalla muestra:
   - Nombre de la empresa que te invita
   - Tu correo (readonly)
   - Tu rol asignado ("Administrador de la empresa")
   - Fecha de expiración de la invitación
4. Como es un correo nuevo (no hay identidad previa), se pide crear
   contraseña:
   - Contraseña: mínimo 12 caracteres
   - Repetir contraseña
5. Aceptar los términos y **Crear cuenta**.

**Validaciones**:

- ✅ La Web valida que la contraseña tenga ≥ 12 chars
- ✅ Si tipeás una contraseña conocida (ej. `password123`), rechaza con
  mensaje "Contraseña filtrada en HaveIBeenPwned"
- ✅ Tras crear la cuenta, quedás logueado y llegás al dashboard de la
  empresa

### 4.2 Vía cURL (para QA)

```bash
curl -s -X POST https://api.ingenia365.com/api/invitations/accept \
  -H "Content-Type: application/json" \
  -d '{
    "token": "<token del link del correo>",
    "registration": { "password": "MiPasswordSegura2026!" }
  }'
```

**Respuesta esperada** (HTTP 200):

```json
{
  "accessToken": "eyJ…",
  "refreshToken": "…",
  "centralUserId": "…",
  "activeTenantPublicId": "…",
  "activeTenantName": "Cooperativa Piloto"
}
```

- ✅ El JWT contiene `active_tenant_id` = tenantPublicId, `tenant_admin=true`
- ✅ La app te logueó automáticamente

---

## 5. Paso 5 — Verificar que el token no se puede reutilizar

**Actor**: Cualquiera con el link
**Objetivo**: Confirmar que la invitación es **single-use** (defensa contra
fugas de link).

Repetí el `POST /api/invitations/accept` con el mismo token. Debe fallar:

```json
{ "code": "Invitation.AlreadyAccepted", "message": "La invitación ya fue aceptada." }
```

**HTTP 410 Gone**. ✅ Correcto: el token quedó consumido.

---

## 6. Paso 6 — Login del admin recién creado

**Actor**: Admin de la empresa
**Objetivo**: Verificar que puede loguearse con las credenciales que acaba de
crear (sin usar más el link de invitación).

### 6.1 Vía UI

1. Cerrá sesión (menú usuario → **Cerrar sesión**).
2. Andá a `https://app.ingenia365.com/login`.
3. Email y password del admin.
4. Debés entrar directo al dashboard de la empresa (auto-selected).

### 6.2 Vía cURL

```bash
curl -s -X POST https://api.ingenia365.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@cooperativa-piloto.com","password":"MiPasswordSegura2026!"}'
```

**Validaciones**:

- ✅ `challenge` = `None`
- ✅ `autoSelected` = `true` (una sola empresa → seleccionada automáticamente)
- ✅ `activeTenantName` = `Cooperativa Piloto`
- ✅ `activeTenants` tiene 1 elemento con `isTenantAdmin: true`

---

## 7. Paso 7 — Admin invita a un miembro regular

**Actor**: Admin de la empresa piloto
**Objetivo**: Confirmar que un admin de tenant (no master) puede invitar
gente a **su propia empresa**.

### 7.1 Vía UI

1. En el dashboard de Cooperativa Piloto → **Configuración** → **Miembros**
   → **Invitar usuario**.
2. Email del invitado (ej. `contador@cooperativa-piloto.com`).
3. **Enviar invitación**.

### 7.2 Vía cURL

```bash
curl -s -X POST "https://api.ingenia365.com/api/tenants/<tenantPublicId>/invitations/" \
  -H "Authorization: Bearer <accessToken del admin de tenant>" \
  -H "Content-Type: application/json" \
  -d '{"email":"contador@cooperativa-piloto.com"}'
```

**Validaciones**:

- ✅ HTTP 200 con nuevo `invitationPublicId`
- ✅ El contador recibe correo real
- ✅ El contador acepta con la misma UX del paso 4 pero **no recibe
  `tenant_admin=true`** — es un usuario regular

---

## 8. Paso 8 — Cambio de contraseña (Profile)

**Actor**: Admin de la empresa (logueado)
**Objetivo**: Confirmar que cambiar la contraseña invalida los refresh tokens
previos.

### 8.1 Vía UI

1. Avatar arriba a la derecha → **Perfil** → **Cambiar contraseña**.
2. Ingresá contraseña actual + nueva (≥12 chars, no Pwned).
3. **Guardar**.

**Validaciones**:

- ✅ La app te muestra "Contraseña actualizada"
- ✅ Los otros dispositivos donde tenías sesión activa quedan deslogueados
  (probá desde otro navegador — el refresh falla con 401)

---

## 9. Paso 9 — Forgot / Reset password

**Actor**: Un usuario que "olvidó" su contraseña
**Objetivo**: Recuperar acceso vía correo.

1. En `/login`, hacer clic en **¿Olvidaste tu contraseña?**
2. Ingresar el correo del admin (o un correo inexistente para probar defensa
   anti-enumeración — en ambos casos la Web responde igual).
3. Revisar correo con asunto **"Recuperar contraseña — IngenIA365ERP"**.
4. Clic en el link → pantalla de nueva contraseña.
5. Ingresar contraseña nueva ≥12 chars.
6. **Guardar**.
7. Login con la nueva contraseña — debe funcionar.

**Validaciones**:

- ✅ Correo con email inexistente **no** revela que no existe (mismo mensaje
  genérico que el válido)
- ✅ Token del link expira en 30 min y es single-use

---

## 10. Paso 10 — Activar MFA (opcional pero recomendado en prod)

**Actor**: Admin de la empresa
**Objetivo**: Activar segundo factor con app de autenticación.

1. Perfil → **Seguridad** → **Activar autenticación de dos factores**.
2. La app muestra un QR y un secreto en base32.
3. Escanear con Google Authenticator / Authy / 1Password.
4. Ingresar el código de 6 dígitos que muestra la app.
5. **Confirmar**.

Verificar el flujo:

1. Cerrar sesión.
2. Login con email + password.
3. Ahora aparece pantalla adicional pidiendo el código de 6 dígitos.
4. Ingresar código actual → entra al dashboard.

---

## 11. Paso 11 — Multi-empresa (opcional)

**Actor**: Un usuario invitado a **dos** empresas (repetir pasos 2 + 4 con
otra empresa y el mismo email)
**Objetivo**: Confirmar el selector de empresa.

1. Login del usuario que tiene 2 memberships.
2. La app **no auto-selecciona** — muestra pantalla "Elegí empresa".
3. Seleccionar empresa A → dashboard de A.
4. En el header, cambiar a empresa B via switcher → dashboard de B (JWT se
   re-emite con nuevo `active_tenant_id`).

---

## 12. Paso 12 — Chequeos post-prueba (checklist QA)

Antes de dar por buena la prueba, verificar en el ambiente:

| Chequeo | Cómo |
|---|---|
| ✅ Auditoría persistida | Consultar MongoDB `audit_events_<tenant>` y ver eventos `Tenant.Created`, `Invitation.Accepted`, `Login.Success` |
| ✅ Refresh tokens en Redis | `redis-cli KEYS "cri:refresh:*"` (deben existir para los usuarios logueados) |
| ✅ Sin errores 5xx en logs | Grep `ERR` en logs de Serilog del último día |
| ✅ Métricas de performance | p95 de `/api/auth/login` < 800 ms bajo carga normal |
| ✅ Backup reciente | Snapshot de SQL Server e índice de MongoDB tomados en las últimas 24h |
| ✅ Correos entregados | Panel de SendGrid/SES muestra `delivered` para todos los envíos |

---

## 13. Criterios de aceptación

La prueba se considera **exitosa** cuando:

1. Master admin puede loguearse y crear tenants.
2. Un admin invitado por email puede aceptar, crear contraseña y loguearse.
3. Un admin de tenant puede invitar miembros regulares.
4. El link de invitación es **single-use** y **expira en 7 días**.
5. Cambio de contraseña **invalida las sesiones previas**.
6. Forgot/reset funciona con protección anti-enumeración.
7. MFA se puede activar y luego es obligatorio en el login.
8. Un usuario multi-empresa ve selector y puede cambiar.
9. Todos los eventos quedan auditados en MongoDB.
10. Cero errores 5xx en los logs durante la prueba.

Si algún criterio falla, **no promover a producción hasta corregir**.

---

## 14. Rollback en caso de emergencia

Si la prueba en pre-prod falla o hay un incidente en prod:

> **Este plan no se puede ejecutar.** La bandera `CentralIdentity:Enabled` no
> existe —no aparece en un solo `.cs` ni `.json` del repositorio— y el flujo de
> Fase 0 al que pretendía volver se retiró el 2026-08-25: sus rutas devuelven
> 404. Un plan de rollback que no funciona es peor que no tenerlo, porque se
> descubre durante el incidente.
>
> **El rollback real es de despliegue, no de configuración**: revertir a la
> imagen anterior. Y hay que saber esto antes de decidirlo: los usuarios creados
> en la identidad central y las cooperativas aprovisionadas durante la ventana
> **no desaparecen** al revertir la imagen — sus bases quedan creadas. Volver
> atrás es volver a una versión del código, no deshacer los datos.

1. ~~**Deshabilitar el flujo nuevo** con feature flag~~
   ~~`CentralIdentity:Enabled = false` en config.~~ — no existe.
2. ~~Los usuarios seguirán autenticándose con el flujo Phase 0 legacy.~~ — retirado.
3. Los tenants creados durante la ventana de prueba pueden marcarse
   `IsActive = 0` desde el master admin.
4. Los correos ya enviados no se revocan — informar al destinatario que
   ignore el link.

---

## 15. Anexos

- **Documentación técnica**: `docs/operaciones/manual-pruebas-identidad-central.md`
- **Setup local para reproducir bugs**: `docs/operaciones/setup-local-pruebas.md`
- **Especificación del feature**: `specs/002-identidad-central-federada/`
- **Runbook de fallos**: `docs/operaciones/runbook-fase0.md`

---

*Documento generado a partir de la ejecución real del flujo el 2026-07-26
contra el ambiente local Windows + WSL2. Ver commits de `002-identidad-central-federada` para el detalle de cambios.*
