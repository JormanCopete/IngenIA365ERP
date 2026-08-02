# Manual de pruebas funcional — Remates de Identidad Central (feature 003)

**Audiencia**: QA funcional / usuario probador. No requiere conocimientos técnicos
más allá de usar el navegador y (opcionalmente) DevTools.
**Última actualización**: 2026-08-02.

---

## 1. Qué valida este manual

El feature 003 cierra el flujo de identidad de punta a punta. Estas pruebas
validan que un usuario real puede:

1. Cambiar de cooperativa desde el encabezado sin volver a iniciar sesión (US1).
2. Llegar a todas las pantallas desde el menú, viendo solo lo que su rol permite (US2).
3. Entrar con un código de recuperación si perdió su app de autenticación (US3).
4. Recargar la página (F5) sin perder la sesión (US4).
5. Aceptar una invitación y quedar dentro de la aplicación sin re-login (US5).
6. Gestionar solicitudes de reset de MFA sin manipular identificadores técnicos (US6).

Cada prueba es **independiente y completa**: tiene actor, precondiciones, pasos,
resultado esperado y variantes (escenarios negativos y de borde). Ejecutalas en
orden — algunas dejan datos que las siguientes reutilizan.

## 2. Entorno y actores

### Servicios (ver `setup-local-pruebas.md` para levantarlos)

| Servicio | URL | Verificación rápida |
|---|---|---|
| API | http://localhost:5100 | `/swagger` responde |
| Web | http://localhost:5200 | redirige al login |
| smtp4dev (correos) | http://localhost:8025 | bandeja visible |
| MongoDB (auditoría) | localhost:27017 | opcional, para verificar eventos |

### Actores (tabla de credenciales viva — actualizala si cambian)

| Actor | Rol | Credenciales | Detalle |
|---|---|---|---|
| **Ana** | Admin de 2 cooperativas | `ana.perez@coop.solidaria.test` / `Ana-After-Reset-2026` | MFA activo (TOTP). Default: Pacífico |
| **Luis** | Miembro regular de Solidaria | `luis.martinez@coop.solidaria.test` / `Luis-Strong-Pwd-2026` | Sin MFA propio (la política de Solidaria lo forzará) |
| **Gina** | Miembro regular de Solidaria | `gina.torres@coop.solidaria.test` / `Gina-Strong-Pwd-2026` | MFA activo, mono-empresa |
| **Master** | Administrador SaaS global | (credenciales del master de tu entorno) | Sin membresías |

> Regla de oro: **anotá acá los recovery codes cada vez que se generen** — se
> muestran una sola vez. Si Gina no tiene códigos anotados, la Prueba 3
> empieza regenerándolos.

| Cooperativa | Política MFA |
|---|---|
| Coop. Solidaria Dev | **Activa** (fuerza enrollment) |
| Coop. del Pacífico Dev | Apagada |

---

## Prueba 1 — Cambio de cooperativa desde el encabezado (US1)

**Actor**: Ana. **Valida**: FR-101..FR-104, SC-101.

### Escenario 1.1 — Cambio feliz

1. Iniciá sesión con Ana en `/login` (pedirá el código TOTP de su app).
2. Observá el encabezado: debe mostrar **su correo real** y un **selector**
   con sus dos cooperativas, con **Coop. del Pacífico Dev** activa (su default).
3. Cambiá el selector a **Coop. Solidaria Dev**.
4. ✅ Esperado: en menos de 3 segundos volvés al dashboard con la nueva
   cooperativa activa en el encabezado, **sin pasar por el login**.

### Escenario 1.2 — Guardia de cambios sin guardar

1. Con la sesión de Ana, abrí **Cambiar contraseña** (menú de cuenta).
2. Escribí algo en cualquier campo. **No guardes.**
3. Intentá cambiar de cooperativa en el selector.
4. ✅ Esperado: aparece la advertencia *"Tienes cambios sin guardar…"*.
   - **Cancelar**: seguís en la misma cooperativa y el formulario conserva
     lo escrito; el selector vuelve al valor original.
   - Repetí y elegí **Aceptar**: el cambio procede y lo escrito se descarta.

### Escenario 1.3 — Usuario mono-empresa no ve selector

1. Cerrá sesión y entrá con **Gina**.
2. ✅ Esperado: el encabezado muestra "Coop. Solidaria Dev" como **texto fijo**
   (sin selector desplegable).

### Escenario 1.4 (borde) — Membresía revocada durante la sesión

> Requiere un segundo navegador con el master.

1. Con Ana logueada en Pacífico, el master suspende la membresía de Ana en
   Solidaria (pantalla de miembros de Solidaria).
2. Ana intenta cambiar a Solidaria.
3. ✅ Esperado: mensaje de error claro ("Tu membresía puede haber cambiado"),
   **la sesión actual se conserva** y el selector vuelve a Pacífico.
4. Restaurá la membresía de Ana al terminar.

---

## Prueba 2 — Navegación completa por rol (US2)

**Valida**: FR-105..FR-107, SC-102. Se ejecuta tres veces, una por rol.

### Escenario 2.1 — Usuario regular (Gina)

1. Entrá con Gina. Abrí el menú lateral.
2. ✅ Esperado: ve el grupo **Mi Cuenta** (Cambiar contraseña, Autenticación
   MFA, Empresa por defecto). **NO** ve "Mi Cooperativa" ni "Consola SaaS".
3. Hacé clic en su correo en el encabezado: se abre el menú desplegable con
   los mismos 3 enlaces. Recorrelos: ninguno da pantalla en blanco.

### Escenario 2.2 — Admin de cooperativa (Ana)

1. Entrá con Ana (cooperativa activa: Solidaria, donde es admin).
2. ✅ Esperado: además de Mi Cuenta, ve **Mi Cooperativa** con **Miembros**,
   **Política MFA** y **Aprobaciones MFA**. Todas cargan con datos de la
   cooperativa activa.

### Escenario 2.3 — Master admin

1. Entrá con el master.
2. ✅ Esperado: ve además la **Consola SaaS**: Registrar cooperativa,
   Reset de MFA, Aprobaciones.

### Escenario 2.4 (negativo) — El rol no protege solo el menú

1. Con la sesión de **Gina**, pegá a mano la URL `/saas/register-tenant`.
2. ✅ Esperado: la pantalla carga pero **cualquier acción es rechazada por el
   backend** (error de permisos). Lo que protege los datos es la API, el menú
   solo esconde lo irrelevante.

---

## Prueba 3 — Entrar con un código de recuperación (US3)

**Actor**: Gina. **Valida**: FR-108..FR-112, SC-103.

### Preparación (si no hay códigos anotados)

1. Entrá con Gina (password + TOTP) → **Mi Cuenta → Autenticación MFA**.
2. La pantalla dice "MFA está **activo**" y muestra cuántos códigos quedan.
3. **Regenerar códigos de recuperación** → confirmá con la **contraseña**.
4. ✅ Esperado: aparecen **10 códigos nuevos** con la advertencia de que se
   muestran una sola vez. **Anotalos en la tabla de actores.** Cerrá sesión.

### Escenario 3.1 — Canje feliz

1. Login de Gina con email + password. En el desafío MFA, clic en
   **"¿No tienes tu app? Usa un código de recuperación"**.
2. La pantalla cambia: pide un código de recuperación y avisa que cada código
   sirve **una sola vez**.
3. Ingresá el **primer código** de la lista. Verificar.
4. ✅ Esperado: entrás al dashboard normalmente. Tachá el código usado.

### Escenario 3.2 — El código es one-shot

1. Cerrá sesión. Repetí el login usando **el mismo código ya usado**.
2. ✅ Esperado: rechazo con el mensaje **genérico** "Código de recuperación
   incorrecto o ya usado" — sin distinguir si el código existió (nada de
   pistas para un atacante).

### Escenario 3.3 — Contador y aviso de pocos códigos

1. Entrá (TOTP o un código nuevo) → **Autenticación MFA**.
2. ✅ Esperado: el contador bajó exactamente en la cantidad de canjes hechos.
3. (Opcional, largo) Canjeá códigos hasta quedar en 3 o menos: la pantalla
   debe sugerir regenerarlos.

### Escenario 3.4 — Regenerar invalida el juego anterior

1. Regenerá códigos (esta vez confirmá con un **código TOTP** en lugar de la
   contraseña — ambas vías deben funcionar, pero solo una a la vez).
2. Cerrá sesión e intentá entrar con un código **del juego viejo**.
3. ✅ Esperado: rechazo genérico. Un código del **juego nuevo** sí entra.

### Escenario 3.5 (negativo) — Confirmación de identidad inválida

1. En la regeneración, poné una contraseña incorrecta.
2. ✅ Esperado: "La confirmación de identidad no es válida", sin regenerar.
3. Poné contraseña **y** TOTP a la vez: debe exigir exactamente uno.

### Verificación de auditoría (opcional, QA técnico)

En Mongo (`IngenIA365ERP_Audit`, colección `audit_events_`), tras esta prueba
deben existir eventos `CentralUser.Mfa.RecoveryCodeUsed`,
`CentralUser.Mfa.RecoveryCodeFailed` y `Profile.RecoveryCodesRegenerated`
con el correo de Gina.

---

## Prueba 4 — La sesión sobrevive a la recarga (US4)

**Actor**: Ana. **Valida**: FR-113..FR-115, SC-104.

### Escenario 4.1 — F5 conserva sesión y cooperativa

1. Entrá con Ana y cambiá a una cooperativa que NO sea su default.
2. Presioná **F5**.
3. ✅ Esperado: seguís dentro, con el **mismo correo y la misma cooperativa**
   en el encabezado (no la default — la que habías elegido).

### Escenario 4.2 — Deep-link con sesión activa

1. Con la sesión activa, pegá en la URL `/profile/mfa`.
2. ✅ Esperado: la pantalla carga directo, sin rebotar al login.

### Escenario 4.3 — Logout limpia todo

1. Clic en **Salir**. Presioná F5.
2. ✅ Esperado: login limpio. En DevTools → Application → Session Storage:
   **no** deben existir claves `ingenia365:auth_token` ni
   `ingenia365:refresh_token`.

### Escenario 4.4 — Alcance solo-pestaña

1. Entrá con Ana. **Cerrá la pestaña** (no el navegador).
2. Abrí una pestaña nueva con `http://localhost:5200`.
3. ✅ Esperado: pide login. La sesión NO se comparte entre pestañas ni
   sobrevive al cierre — es el comportamiento acordado por seguridad.

### Escenario 4.5 (borde) — Token adulterado

1. Con sesión activa, en DevTools → Console ejecutá:
   `sessionStorage.setItem('ingenia365:auth_token', 'BASURA')` y recargá.
2. ✅ Esperado: login limpio, **sin pantalla de error**, y el storage queda
   sin las claves de sesión.

---

## Prueba 5 — Invitación de punta a punta sin re-login (US5)

**Actores**: master + un usuario nuevo inventado por vos
(p. ej. `qa.remates+01@coop.pacifico.test`). **Valida**: FR-115..FR-117, SC-105.

### Escenario 5.1 — Link de contraseña olvidada

1. Andá a `/login`.
2. ✅ Esperado: existe el enlace **"¿Olvidaste tu contraseña?"** y lleva a
   `/auth/forgot-password`, que responde con mensaje genérico ante cualquier
   correo (exista o no).

### Escenario 5.2 — Invitación a cooperativa SIN política MFA (directo al dashboard)

1. Como master (u admin de Pacífico), invitá al correo nuevo a **Pacífico**.
2. Abrí smtp4dev (http://localhost:8025) y encontrá el correo de invitación.
   Abrí su enlace.
3. La pantalla muestra la cooperativa y el correo invitado. Creá una
   contraseña (mínimo 12 caracteres, que no sea común).
4. ✅ Esperado: al confirmar quedás **dentro del dashboard de Pacífico**,
   con tu correo en el encabezado — **sin pasar por el login**.

### Escenario 5.3 — Invitación a cooperativa CON política MFA (enrollment forzado)

1. Repetí con otro correo nuevo, invitado a **Solidaria**.
2. ✅ Esperado: tras crear la contraseña, en lugar del dashboard te lleva al
   **enrollment forzado de MFA** (escaneás QR, confirmás TOTP, guardás los
   10 códigos) y **después** entrás al dashboard. Tampoco pasa por el login.

### Escenario 5.4 (negativo) — Replay de invitación

1. Volvé a abrir el enlace del correo del escenario 5.2 (ya aceptado).
2. ✅ Esperado: "Esta invitación ya fue aceptada" — sin formulario.

---

## Prueba 6 — Reset de MFA sin GUIDs (US6)

**Actores**: Ana (admin), master (o segundo admin), Gina (afectada).
**Valida**: FR-118, FR-119, SC-106.

### Escenario 6.1 — Las pantallas retiradas ya no existen

1. Pegá a mano: `/security/mfa-enrollment` y
   `/security/change-password-required`.
2. ✅ Esperado: ambas responden **página no encontrada** (404).

### Escenario 6.2 — Crear y aprobar una solicitud desde la consola

1. Como Ana (admin de Solidaria), abrí **Aprobaciones MFA**
   (`/security/mfa-reset-approvals`).
2. En **Nueva solicitud**, ingresá el usuario afectado (Gina) y una
   justificación (p. ej. "perdió el teléfono"). Crear.
3. ✅ Esperado: la solicitud aparece en la tabla de **pendientes** mostrando
   **solicitante, afectado, motivo, fechas y aprobaciones (0/2)** — datos
   legibles, no identificadores.
4. Como un **admin distinto** (master), abrí la misma consola y clic en
   **Aprobar** sobre la fila. La columna pasa a 1/2.
5. Un **tercer** admin (o el master de nuevo si el flujo lo permite… no:
   debe ser **otro** distinto) aprueba la segunda vez → la solicitud
   desaparece del filtro de pendientes (ejecutada).

### Escenario 6.3 (negativos) — Reglas de la doble aprobación

- El **solicitante** (Ana) intenta aprobar su propia solicitud → rechazo.
- El **mismo aprobador** intenta aprobar dos veces → rechazo.
- Una solicitud con más de 24 horas → expira y no puede aprobarse.

### Escenario 6.4 — Efecto del reset

1. Tras la doble aprobación, Gina cierra sesión y vuelve a entrar.
2. ✅ Esperado: su login la lleva a **configurar MFA de nuevo** (la política
   de Solidaria lo exige). Sus códigos anteriores quedaron invalidados —
   actualizá la tabla de actores.

---

## Prueba 7 — Regresión rápida (smoke, ~5 min)

Corré esto después de cualquier cambio para validar que nada se rompió:

| # | Acción | Esperado |
|---|---|---|
| 1 | Login Ana (password + TOTP) | Dashboard con selector de 2 coops |
| 2 | Cambiar de cooperativa | Sin re-login, header actualizado |
| 3 | F5 | Sesión y cooperativa conservadas |
| 4 | Login Gina con recovery code | Entra; contador de restantes baja |
| 5 | `/profile/mfa` de Gina | "MFA activo" + contador correcto |
| 6 | `/security/mfa-enrollment` | 404 |
| 7 | Logout + F5 | Login limpio, storage vacío |

### Suites automatizadas equivalentes (QA técnico)

```bash
dotnet test tests/IngenIA365ERP.Application.Tests
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Identity"
```

Ambas deben quedar en verde (la segunda requiere Docker para Testcontainers).

---

## 8. Registro de resultados

Copiá esta tabla por cada corrida:

| Prueba | Escenarios OK | Escenarios FALLO | Observaciones |
|---|---|---|---|
| 1 — TenantSwitcher | /4 | | |
| 2 — Navegación por rol | /4 | | |
| 3 — Recovery codes | /5 | | |
| 4 — Sesión ante recarga | /5 | | |
| 5 — Invitación E2E | /4 | | |
| 6 — Reset de MFA | /4 | | |
| 7 — Smoke | /7 | | |

**Al reportar un fallo incluí**: actor, escenario, paso exacto, lo esperado
vs. lo observado, y si es de API, el `traceId` del cuerpo del error (todos los
errores lo traen) para cruzarlo con los logs de Serilog.
