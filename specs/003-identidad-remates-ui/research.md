# Research — Remates de Identidad Central (feature 003)

Decisiones técnicas de Phase 0. Formato: Decisión / Racional / Alternativas.

## D-01 — TenantSwitcher: componente en Shared, datos de `/api/sessions/active-tenants`

**Decisión**: `Shared/Components/TenantSwitcher.razor`, montado en el header
de `MainLayout`. Al inicializar (solo si hay sesión) llama
`TenantSessionClient.GetActiveTenantsAsync()`; se renderiza como selector
únicamente si hay >1 membresía activa (si hay 1, solo el nombre). El cambio
invoca `TenantSessionClient.SwitchAsync(tenantId)` — que desde el cutover ya
adopta la sesión nueva (persistencia + notificación del auth state) — y
navega a `/` sin `forceLoad`.

**Racional**: los servicios y la adopción de sesión ya existen y están
verificados; el componente en Shared lo heredan Web y MAUI (T091 del 002 lo
ubicaba en Web.Client, pero el layout vive en Shared — ponerlo en Web.Client
lo haría invisible para MAUI).

**Alternativas**: leer las empresas de los claims del JWT (no traen la lista
completa ni los nombres); componente en Web.Client (rechazado: el layout que
lo monta está en Shared).

## D-02 — Dirty-state (FR-103): servicio scoped + wrapper de EditForm

**Decisión**: `IFormDirtyStateService` (scoped) con `Register/MarkDirty/
MarkClean/HasDirtyForms` + `DirtyTrackingEditForm.razor` que envuelve
`EditForm` y marca dirty en `OnFieldChanged` y clean en submit/dispose. El
TenantSwitcher consulta `HasDirtyForms` antes de cambiar y muestra el diálogo
de confirmación existente en Shared/Components. La migración de formularios
existentes al wrapper es incremental (este feature migra los formularios de
identidad; el resto adopta el wrapper a demanda).

**Racional**: diseño ya esbozado en T091a del 002; scoped = un estado por
circuito/usuario; el wrapper evita tocar cada formulario a mano.

**Alternativas**: interceptar `beforeunload` del navegador (solo cubre
recarga/cierre, no navegación interna del router); estado por página con
parámetros en cascada (frágil y repetitivo).

## D-03 — Canje de recovery codes: mismo endpoint `mfa/verify`, rama explícita

**Decisión**: `MfaVerifyCommand` gana `UseRecoveryCode` (bool, default
false). Con true, el handler llama el nuevo
`ICentralIdentityProvider.RedeemRecoveryCodeAsync(userId, code)`
(implementado con `UserManager.RedeemTwoFactorRecoveryCodeAsync`, one-shot
atómico del token store) y, si procede, `CountRecoveryCodesAsync` para
devolver `RecoveryCodesRemaining` en la respuesta. Fallo → mismo
`Identity.MfaInvalid` genérico que un TOTP incorrecto (FR-109) y cuenta para
el lockout progresivo. Auditoría: `CentralUser.Mfa.RecoveryCodeUsed`.
El validator relaja el formato solo en la rama recovery (los códigos de
Identity son alfanuméricos con guion, p.ej. `RYBK3-Y7MF3`).

**Racional**: un solo endpoint mantiene el challenge token purpose=mfa-verify
sin superficies nuevas anónimas; Identity ya garantiza el one-shot.

**Alternativas**: endpoint separado `/mfa/verify-recovery` (más superficie,
mismo resultado); autodetección por formato sin flag (ambigua y complica el
mensaje genérico).

## D-04 — Regeneración de códigos: command propio con confirmación de identidad

**Decisión**: `POST /api/profile/mfa/recovery-codes/regenerate`
[purpose=full] → `RegenerateRecoveryCodesCommand(currentPassword? | totpCode?)`
(exactamente uno, validado). El handler confirma la identidad
(`ValidatePasswordAsync` o `VerifyMfaCodeAsync`) y llama
`RegenerateRecoveryCodesAsync` (→ `GenerateNewTwoFactorRecoveryCodesAsync`,
que invalida el juego anterior completo por diseño de Identity). Devuelve los
10 códigos una sola vez. Auditoría: `Profile.RecoveryCodesRegenerated`.
UI: sección en `MfaEnrollmentCentral.razor` (/profile/mfa).

**Racional**: FR-111 exige confirmación de identidad; Identity regenera
reemplazando el juego completo, que es la semántica del spec.

**Alternativas**: regeneración automática al agotarse (rechazada por el
spec — explícita del usuario); exigir re-login completo (fricción excesiva).

## D-05 — Sesión solo-pestaña: `sessionStorage` vía IJSRuntime en Web.Client

**Decisión**: `BrowserSessionSecureStorage : ISecureStorage` en Web.Client
usando `IJSInProcessRuntime` (WASM es síncrono) sobre
`window.sessionStorage`; reemplaza el registro de `WebAssemblySecureStorage`.
El `CustomAuthStateProvider` ya lee de `ISecureStorage` al evaluar el estado
→ al recargar la pestaña, la sesión se rehidrata sola sin más cambios.
`Remove/RemoveAll` en logout ya limpian (FR-114); un token ilegible o
expirado degrada al flujo anónimo existente. El host server (prerender) y
MAUI no cambian.

**Racional**: `sessionStorage` implementa exactamente el alcance decidido en
Clarifications (sobrevive F5 en la pestaña, muere al cerrarla) sin criptas
adicionales; el riesgo XSS es el estándar de SPA y queda documentado en
Complexity Tracking.

**Alternativas**: `localStorage` (persistencia mayor a la decidida);
cookies HttpOnly + patrón BFF (elimina el riesgo XSS pero exige re-arquitectura
del hosting — fuera de alcance, anotado como endurecimiento futuro);
`ProtectedSessionStorage` (solo Blazor Server, la app es WASM).

## D-06 — Navegación por rol: claims del auth state + `AuthorizeView`

**Decisión**: `NavMenu`/`MainLayout` leen el `AuthenticationState` en
cascada: menú de cuenta para todo autenticado (contraseña, MFA, empresa por
defecto); sección "Mi cooperativa" si `tenant_admin == true` (miembros →
`/admin/tenant/{active_tenant_id}/members`, política MFA); sección "Consola
SaaS" si `is_global_master_admin == true` (registrar cooperativa, reset MFA,
aprobaciones). El `active_tenant_id` para armar las rutas admin sale de los
claims. El backend ya restringe el acceso directo por URL (FR-106 se apoya
en los filtros existentes).

**Racional**: los claims ya viajan en el JWT y el provider ya los expone; no
se necesita round-trip para decidir el menú.

**Alternativas**: consultar `/api/auth/me` para armar el menú (round-trip
innecesario para lo que los claims ya dicen; /me se reserva para lo que los
claims NO traen — ver D-07).

## D-07 — Identidad del header: `GetMeAsync` cacheado por sesión

**Decisión**: `CentralAuthClient.GetMeAsync()` llama `GET /api/auth/me` una
vez tras autenticar (y tras cada switch) y cachea el resultado en el client
scoped; el header muestra email y nombre de la empresa activa desde ahí.
Resuelve además el TODO de `MfaEnrollmentCentral` (saber si MFA ya está
activo al cargar).

**Racional**: el claim trae el email pero NO el nombre del tenant (solo el
guid); /me ya devuelve ambos y el estado de MFA — un único consumo cubre
header + página de MFA.

**Alternativas**: nuevo claim `active_tenant_name` en el JWT (cambia el
contrato de tokens por un dato de presentación; rechazado).

## D-08 — Listado de MFA reset: query per-tenant bajo el contexto existente

**Decisión**: `GET /api/auth/mfa/reset/requests?status=Pending`
(autorización: admin del tenant activo o master — mismo guard que el
approve existente) → `ListMfaResetRequestsQuery` sobre
`SEC_MfaResetRequests` del tenant activo, paginada, proyectando
`PublicId, solicitante, afectado, motivo, fechas, estado, aprobaciones`.
`MfaResetApprovals.razor` (/security/mfa-reset-approvals) muestra el listado
con acciones aprobar/rechazar y absorbe `MfaResetRequests.razor` (el alta de
solicitudes queda como formulario dentro de la misma pantalla).

**Racional**: la entidad es per-tenant (Fase 0, doble aprobación); el
middleware ya resuelve el tenant del admin y el fallback por ruta del master
existe desde el 002. Solo falta la lectura.

**Alternativas**: vista global multi-tenant para el master (violaría el
principio IV — cross-tenant; el master opera tenant por tenant como en el
resto de la consola).

## D-09 — Retiro de páginas: eliminar, no ocultar

**Decisión**: se eliminan `MfaEnrollment.razor` (duplicada — el flujo
voluntario es `MfaEnrollmentCentral` y el forzado `MfaEnrollmentForced`) y
`ChangePasswordRequired.razor` (ningún flujo la invoca — el modelo central no
usa `MustChangePassword`); `MfaResetRequests.razor` se absorbe en
`MfaResetApprovals.razor`. Los subárboles legacy `/security/users` y
`/security/roles` (Fase 0, duplicados de `/admin/*`) NO se tocan: fuera de
alcance de identidad central.

**Racional**: assumption del spec ("se eliminan, no se ocultan"); menos
superficie muerta que mantener. Git conserva la historia si hay que
reintroducir.

**Alternativas**: marcar obsoletas con redirect (deuda visual permanente).

## D-10 — Deuda de tests 002: sobre `CentralIdentityApiFixture`, un archivo por escenario

**Decisión**: los 11 tests de integración pendientes del 002 se escriben
contra la fixture existente (contenedores efímeros + DDL oficiales + master
sembrado + capturador de correo), un archivo por escenario con los nombres ya
definidos en tasks.md del 002, más `Security_RecoveryCodeRedeem.cs` para la
US3 de este feature. Los escenarios per-tenant (crossover, admin scope)
siembran su tenant vía el flujo real `POST /api/saas/tenants/with-admin`
(patrón de `EndToEnd_MasterRegisterTenant`).

**Racional**: la fixture quedó lista y validada en T118; reutilizarla evita
una segunda infraestructura y prueba los DDL reales en cada corrida.

**Alternativas**: tests sobre la BD local del desarrollador (no
reproducibles, contaminan el estado del manual).
