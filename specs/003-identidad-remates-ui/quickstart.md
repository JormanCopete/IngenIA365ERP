# Quickstart — Verificación E2E del feature 003

> Reutiliza el entorno y los actores del feature 002
> (`docs/operaciones/setup-local-pruebas.md` y los prompts de continuación):
> API en 5100, Web en 5200 (perfil `http`), smtp4dev en 8025.
> Actores: **Ana** (multi-empresa, MFA activo), **Luis** (mono-empresa),
> **Gina** (MFA activo con recovery codes vigentes), **master**.

## US1 — Cambio de empresa desde el encabezado

1. Login UI con Ana (`/login` → TOTP) → dashboard.
2. Verificar en el header: nombre de la empresa activa + selector visible
   (Ana tiene 2 cooperativas).
3. Cambiar a la otra cooperativa → el header, menús y datos cambian sin
   re-login, en < 3 s (SC-101).
4. Abrir un formulario (p. ej. cambiar contraseña), escribir algo sin
   guardar, intentar cambiar de empresa → aparece la advertencia; cancelar
   conserva todo; confirmar descarta y cambia.
5. Login con Luis → el header muestra su cooperativa SIN selector.

## US2 — Navegación por rol

1. Con Luis (usuario regular): el menú de cuenta muestra contraseña, MFA y
   empresa por defecto; NINGUNA entrada admin/saas.
2. Con Ana (admin): además "Mi cooperativa" → miembros y política MFA de la
   empresa activa.
3. Con master: además la consola SaaS (registrar cooperativa, reset MFA,
   aprobaciones). Verificar que Luis no puede abrir esas URLs (403/401 del
   backend, sin cambios).
4. Recorrer todas las entradas: ninguna pantalla en blanco (SC-102).

## US3 — Recovery codes

1. Login con Gina → en el desafío MFA elegir "usar un código de
   recuperación" → ingresar uno de sus códigos → entra al dashboard y ve
   "te quedan 9 códigos" (SC-103).
2. Reintentar login con el MISMO código → falla con el mensaje genérico de
   código incorrecto.
3. En `/profile/mfa`, regenerar códigos confirmando con password → recibe 10
   nuevos; probar que uno del juego viejo ya no sirve.
4. Verificar en Mongo: `CentralUser.Mfa.RecoveryCodeUsed` y
   `Profile.RecoveryCodesRegenerated`.

## US4 — Continuidad de sesión (solo pestaña)

1. Login con Ana → F5 → sigue en sesión, misma empresa activa (SC-104).
2. Navegar a una ruta interna con la URL (p. ej. `/profile/mfa`) → carga sin
   rebote al login.
3. Logout → F5 → login limpio; en DevTools, `sessionStorage` sin
   `auth_token`/`refresh_token`.
4. (Manual) Cerrar la pestaña y reabrir la URL → exige login (alcance
   solo-pestaña).
5. Adulterar `auth_token` en `sessionStorage` y recargar → login limpio sin
   errores crudos (FR-114).

## US5 — Remates de entrada

1. `/login` muestra "¿Olvidaste tu contraseña?" → lleva a
   `/auth/forgot-password`.
2. Invitar un usuario nuevo a **Pacífico** (sin política MFA) → aceptar
   desde el correo → entra DIRECTO al dashboard de Pacífico sin re-login
   (SC-105).
3. Invitar otro a **Solidaria** (política MFA activa) → aceptar → va al
   enrollment forzado (comportamiento del 002, sin cambios).
4. El header muestra el correo real del usuario y el nombre de la empresa.

## US6 — Higiene + aprobaciones

1. Verificar que `/security/mfa-enrollment` y
   `/security/change-password-required` ya no existen (404 de ruta).
2. Como Ana, crear una solicitud de MFA reset para Luis desde la pantalla de
   aprobaciones; como master (u otro admin), abrir el listado de pendientes,
   ver solicitante/afectado/motivo y aprobarla dos veces según la doble
   aprobación de Fase 0 — sin pegar ningún GUID (SC-106).
3. Login de Luis → exige reconfigurar MFA (efecto del reset ya existente).

## Deuda de tests (SC-107)

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~Identity"
```

Todos los escenarios del 002 + `Security_RecoveryCodeRedeem` en verde
(requiere Docker para Testcontainers).
