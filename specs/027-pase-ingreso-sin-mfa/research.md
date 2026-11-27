# Investigación — 027 Pase de ingreso sin segundo factor

## R1 — Dónde entra el pase en el ingreso
- **Decisión**: después del login de siempre, como alternativa al código en el desafío `mfa-verify` (`POST /api/auth/mfa/pass`).
- **Por qué**: el login ya aplica clave, bloqueo por cuenta, usuario activo, documento y política de sesión; el pase sólo reemplaza el código
  (FR-006). Reusar el desafío evita un segundo camino de login.
- **Descartado**: un login propio con el pase (duplicaría reglas); un enlace que abre sesión sin contraseña (lo rechazó el dueño).

## R2 — Secreto y almacenamiento
- **Decisión**: `ISecureTokenGenerator.Generate()` (el de la recuperación por correo), huella SHA-256 de 32 bytes con índice único filtrado.
- **Por qué**: mismo patrón probado; el enlace en claro existe sólo en el correo (FR-003). Reenviar emite un secreto nuevo.

## R3 — Limitar la sesión a la cooperativa del pase
- **Decisión**: el emisor filtra las membresías a la del pase; el access lleva `login_pass_tenant` y el refresh `PaseDeIngresoCooperativa`;
  cambiar o elegir otra cooperativa se rechaza con `Identity.LoginPass.TenantLocked`.
- **Por qué**: `GuardiaDeMetodos` admite `Ninguno` en toda cooperativa con la máscara «todos», así que sin una marca explícita la sesión del
  pase podría saltar a otra cooperativa que exige segundo factor.

## R4 — Método sellado
- **Decisión**: `MetodosMfa.Ninguno`, como el código de respaldo (FR-008). Una cooperativa que restringe métodos manda a inscribir.

## R5 — Anulaciones
- **Decisión**: inmediatas donde ya hay un único punto: concesión de otro (Reemplazado), anular, apagar la opción (en
  `UpdateTenantMfaPolicy`), ingreso con segundo factor (en el emisor). **Perezosas al canjear** para lo demás: sello de seguridad distinto
  del de la concesión (cambio de clave, reset de MFA), usuario no activo, membresía no activa, opción apagada; el canje la anula con su motivo
  y responde el mensaje genérico. El vencimiento no se persiste: es `ExpiresAt <= ahora`.
- **Por qué**: cubrir cada comando de bloqueo, desactivación y retiro con un gancho es frágil; la verificación al canjear es la única que
  importa para la seguridad y la lista muestra el estado calculado.

## R6 — Topes contra abuso
- **Decisión**: consulta sobre `ADM_LoginPasses`: concesiones al usuario en la cooperativa en 7 días < 5 y concesiones de la cooperativa en
  las últimas 24 h < 20 (los reenvíos cuentan). Rechazo `LoginPass.TooMany`.

## R7 — Quién es administrador
- **Decisión**: `TenantMembership.IsTenantAdmin` del destinatario en esa cooperativa. Si lo es, sólo el maestro concede. El actor de
  cooperativa necesita `Security.Users.GrantMfaBypass` (CompanyAdmin lo recibe por `*`).

## R8 — Avisos
- **Decisión**: correos por `IEmailSender` directo con plantillas `LoginPassGranted.html` (al usuario, con el enlace),
  `LoginPassAdminNotice.html` (a los demás administradores activos de la cooperativa, sin enlace) y `LoginPassUsed.html` (al usuario en cada
  uso). Si el correo al usuario falla, el pase queda y la respuesta dice `correoEnviado = false`.

## R9 — Auditoría
- **Decisión**: `AuditoriaDeIdentidad.EnCooperativaAsync` para concesión, reenvío y anulación; `IAuditAppendOnlyWriter` con la cooperativa
  para el uso (exitoso o fallido). Eventos `Identity.LoginPass.Granted|Resent|Voided|Used|UseFailed`.

## R10 — Cliente
- **Decisión**: `/security/pase?t=` guarda el pase en `CentralAuthClient.PaseDeIngreso` (como `ReturnUrl`) y va al login; el desafío lo canjea
  solo. En MAUI, «Tengo un pase de ingreso» acepta el enlace completo o el código y extrae `t`.
