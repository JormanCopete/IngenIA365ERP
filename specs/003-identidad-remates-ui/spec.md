# Feature Specification: Remates de Identidad Central — Switcher, Navegación y Recuperación

**Feature Branch**: `003-identidad-remates-ui`

**Created**: 2026-08-01

**Status**: Draft

**Input**: User description: "Remates de Identidad Central (cierre funcional del feature 002): TenantSwitcher en el encabezado con advertencia de cambios sin guardar; navegación completa de perfil/admin/master; recovery codes canjeables; remates de UX del login (link de recuperación, adopción de sesión al aceptar invitación, header con identidad real); persistencia de sesión ante recarga; limpieza de páginas duplicadas o colgadas y flujo de aprobación de MFA reset operable; cierre de la deuda de tests de integración del feature 002."

## Clarifications

### Session 2026-08-01

- Q: ¿Hasta dónde debe sobrevivir la sesión en el navegador? → A: **Solo la
  pestaña actual** — la sesión sobrevive a recargas (F5) y navegación dentro
  de la misma pestaña; cerrar la pestaña o el navegador cierra la sesión.
  Es el default conservador para un ERP financiero operado en equipos
  compartidos de cooperativas. Un "recordarme en este equipo" opcional queda
  fuera de alcance de este feature.

## Contexto

El feature 002 dejó la identidad central **funcional por dentro** (todos los
flujos operan y están verificados end-to-end) pero **incompleta por fuera**:
varios requisitos de experiencia quedaron sin superficie de usuario. Este
feature cierra esa brecha sin introducir modelo de datos nuevo ni cambiar el
comportamiento de negocio ya verificado.

Brechas puntuales que motiva cada historia (auditadas el 2026-08-01):

- El cambio de empresa en sesión activa (FR-019/020/021 del 002, obligatorios)
  no tiene ningún control visible; el servicio existe pero nadie lo invoca.
- Ninguna página de identidad está enlazada desde la navegación: perfil,
  MFA, contraseña, empresa por defecto, gestión de miembros, política MFA y
  consolas master solo se alcanzan tecleando la URL.
- Los 10 códigos de recuperación que el enrollment muestra al usuario (con la
  instrucción de guardarlos) **no se pueden canjear** por ninguna vía.
- Recargar la página pierde la sesión (el almacenamiento de la sesión es
  volátil), el aceptante de una invitación siempre rebota al login aunque su
  sesión ya esté emitida, el login no ofrece "¿olvidaste tu contraseña?" y el
  encabezado muestra "Usuario" genérico.
- Hay pantallas duplicadas o sin flujo de entrada, y el aprobador de
  solicitudes de MFA reset debe pegar identificadores a mano porque no existe
  un listado de pendientes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cambio de empresa desde el encabezado (Priority: P1)

Una persona con membresías activas en varias cooperativas ve, siempre visible
en el encabezado, un control que muestra su empresa activa y le permite
cambiar a cualquiera de sus otras empresas sin cerrar sesión. Al cambiar, el
contexto completo (menús, permisos, datos) pasa a ser el de la empresa
destino. Si tiene un formulario con cambios sin guardar, el sistema le
advierte y pide confirmación antes de descartar. Un usuario mono-empresa ve
su empresa activa pero no el selector de cambio.

**Why this priority**: son los FR-019/020/021/022 del feature 002 — requisitos
obligatorios del rediseño que hoy no tienen superficie. Sin esto, el modelo
multi-empresa exige cerrar sesión y volver a entrar para cambiar de
cooperativa, lo que contradice la promesa central del producto.

**Independent Test**: con Ana (miembro activa de 2 cooperativas) logueada en
la Empresa A, usar el control del encabezado para pasar a la Empresa B y
verificar que el encabezado, los menús y los datos pasan a ser de B sin
re-login; iniciar un formulario, intentar cambiar de empresa y verificar la
advertencia de cambios sin guardar; con Luis (mono-empresa) verificar que no
aparece el selector.

**Acceptance Scenarios**:

1. **Given** una usuaria con 2 membresías activas logueada en la Empresa A,
   **When** elige la Empresa B en el control del encabezado, **Then** el
   sistema recarga el contexto en menos de 3 segundos y todo lo visible
   (nombre de empresa, menús, datos) corresponde a B (SC-003 del 002).
2. **Given** la misma usuaria con un formulario a medio completar, **When**
   intenta cambiar de empresa, **Then** el sistema le advierte que perderá
   los cambios y solo procede si confirma.
3. **Given** un usuario con una única membresía activa, **When** observa el
   encabezado, **Then** ve el nombre de su empresa activa pero ningún
   selector de cambio.
4. **Given** una usuaria cuya membresía con la Empresa B fue suspendida
   durante la sesión, **When** intenta cambiar a B, **Then** el sistema
   rechaza el cambio con un mensaje claro y conserva la sesión en la empresa
   actual.

---

### User Story 2 - Navegación completa de identidad (Priority: P1)

Cualquier usuario autenticado encuentra en la navegación un menú de cuenta
con sus opciones de identidad: cambiar contraseña, configurar MFA y elegir
empresa por defecto. Un administrador de empresa ve además las opciones de
gestión de su cooperativa (miembros e invitaciones, política MFA). Un master
admin ve la consola global (registrar cooperativa, reset de MFA de usuarios).
Cada opción aparece solo para quien tiene el rol correspondiente.

**Why this priority**: las pantallas existen y funcionan, pero son
inalcanzables sin conocer la URL — para el usuario final es equivalente a que
la función no exista. Es el remate de menor costo y mayor visibilidad.

**Independent Test**: loguear con un usuario regular, un admin de empresa y
el master, y verificar que cada uno ve exactamente las entradas de menú de su
rol y que cada entrada lleva a la pantalla correcta ya existente.

**Acceptance Scenarios**:

1. **Given** un usuario regular autenticado, **When** abre el menú de cuenta,
   **Then** ve cambiar contraseña, configurar MFA y empresa por defecto — y
   ninguna opción administrativa.
2. **Given** una admin de empresa en su empresa activa, **When** abre la
   navegación, **Then** ve además la gestión de miembros y la política MFA de
   SU empresa, y al entrar operan sobre esa empresa.
3. **Given** el master admin, **When** abre la navegación, **Then** ve la
   consola global (registrar cooperativa, reset de MFA) y ningún usuario
   no-master la ve ni puede acceder por URL.
4. **Given** un usuario mono-empresa sin rol admin, **When** navega por todas
   las entradas de su menú, **Then** ninguna lo lleva a una pantalla en
   blanco, rota o de otra empresa.

---

### User Story 3 - Entrar con un código de recuperación (Priority: P2)

Una persona con MFA activo que no tiene acceso a su aplicación de
autenticación (teléfono perdido o cambiado) puede completar el segundo factor
ingresando uno de sus códigos de recuperación de un solo uso. El sistema lo
acepta, lo invalida para siempre, le informa cuántos códigos le quedan y le
sugiere regenerarlos o reconfigurar su MFA. Los códigos también pueden
regenerarse desde el perfil (invalidando el juego anterior completo).

**Why this priority**: hoy el producto muestra 10 códigos al usuario con la
instrucción de guardarlos, pero ninguno sirve — una promesa de seguridad
rota. Sin esto, el único rescate ante un teléfono perdido es contactar al
master admin.

**Independent Test**: con una usuaria con MFA activo y códigos generados,
loguear usando un código de recuperación en lugar del TOTP; verificar que
entra, que el mismo código ya no funciona una segunda vez, y que el contador
de códigos restantes disminuyó; regenerar códigos desde el perfil y verificar
que los viejos quedan invalidados.

**Acceptance Scenarios**:

1. **Given** una usuaria con MFA activo en el desafío de segundo factor,
   **When** elige "usar un código de recuperación" e ingresa un código
   válido, **Then** el sistema la autentica igual que con TOTP y el código
   queda invalidado.
2. **Given** un código de recuperación ya usado, **When** alguien intenta
   usarlo de nuevo, **Then** el sistema lo rechaza con el mismo mensaje
   genérico de código incorrecto.
3. **Given** una usuaria que consumió un código, **When** completa el login,
   **Then** el sistema le informa cuántos códigos le quedan y, si quedan 3 o
   menos, le recomienda regenerarlos.
4. **Given** una usuaria en su perfil, **When** regenera sus códigos de
   recuperación, **Then** recibe un juego nuevo de 10 y todos los anteriores
   quedan invalidados; la operación exige confirmar su segundo factor o
   contraseña.

---

### User Story 4 - La sesión sobrevive a una recarga (Priority: P2)

Un usuario autenticado que recarga la página (F5), abre un enlace interno en
la misma pestaña o sufre un parpadeo del navegador continúa en su sesión sin
volver a loguear, conservando su empresa activa. Al cerrar sesión
explícitamente, no queda rastro recuperable de la sesión en el navegador.

**Why this priority**: hoy cualquier recarga expulsa al usuario al login —
tolerable en demo, inaceptable en operación diaria. Alcance decidido en
Clarifications: la sesión sobrevive **solo dentro de la misma pestaña**;
cerrar la pestaña o el navegador la termina.

**Independent Test**: loguear, recargar con F5 y verificar que la sesión y la
empresa activa se conservan; cerrar sesión y verificar que una recarga
posterior exige login; validar que los artefactos de sesión no quedan
accesibles tras el logout.

**Acceptance Scenarios**:

1. **Given** una usuaria autenticada en su empresa activa, **When** recarga
   la página, **Then** sigue autenticada en la misma empresa sin pasar por el
   login.
2. **Given** una usuaria que cerró sesión, **When** recarga o navega hacia
   atrás, **Then** el sistema exige login y ningún dato de la sesión anterior
   es recuperable desde el navegador.
3. **Given** una sesión cuya credencial temporal expiró durante la
   inactividad, **When** el usuario recarga, **Then** el sistema renueva la
   sesión silenciosamente si aún es válida o lo lleva al login si no.

---

### User Story 5 - Remates del flujo de entrada (Priority: P3)

El login ofrece el enlace "¿Olvidaste tu contraseña?". Quien acepta una
invitación y no tiene ningún desafío MFA pendiente entra directo al tablero
de su nueva empresa, sin repetir el login. El encabezado muestra el correo
real del usuario y el nombre de su empresa activa en lugar de textos
genéricos.

**Why this priority**: pulido de la primera impresión; ninguno bloquea la
operación pero todos se notan en el primer uso.

**Independent Test**: desde el login llegar al flujo de recuperación por el
enlace; aceptar una invitación sin exigencias de MFA y verificar la entrada
directa al tablero; verificar que el encabezado muestra el correo y la
empresa reales del usuario logueado.

**Acceptance Scenarios**:

1. **Given** la pantalla de login, **When** el usuario pulsa "¿Olvidaste tu
   contraseña?", **Then** llega al flujo de recuperación existente.
2. **Given** una invitada nueva a una empresa sin política MFA, **When**
   completa el registro de la invitación, **Then** entra directamente al
   tablero de esa empresa sin pasar por el login.
3. **Given** una invitada a una empresa con política MFA activa, **When**
   completa el registro, **Then** el sistema la lleva al paso de MFA que
   corresponda (comportamiento actual, sin cambios).
4. **Given** una usuaria autenticada, **When** observa el encabezado,
   **Then** ve su correo y el nombre de su empresa activa reales.

---

### User Story 6 - Higiene de pantallas y aprobación de MFA reset operable (Priority: P3)

El producto no expone pantallas duplicadas ni huérfanas: queda un único flujo
de configuración de MFA voluntario, el enrollment forzado y las pantallas sin
flujo de entrada se retiran o se integran. El aprobador de solicitudes de
reset de MFA ve un listado de solicitudes pendientes con la información
necesaria (quién, cuándo, motivo) y aprueba o rechaza desde ahí, sin pegar
identificadores a mano.

**Why this priority**: deuda de mantenimiento y operabilidad; el flujo de
aprobación existe pero es inutilizable en la práctica.

**Independent Test**: verificar que no queda más de una pantalla por función
de MFA (voluntaria/forzada); crear una solicitud de reset de MFA y, como
aprobador, resolverla desde el listado de pendientes sin conocer ningún
identificador técnico.

**Acceptance Scenarios**:

1. **Given** el inventario de pantallas de identidad, **When** se recorren
   todas las rutas, **Then** cada función tiene una única pantalla y toda
   pantalla es alcanzable desde la navegación o desde un flujo.
2. **Given** una solicitud de reset de MFA pendiente, **When** el aprobador
   abre el listado de pendientes, **Then** la ve con solicitante, fecha y
   motivo, y puede aprobarla o rechazarla desde ahí.
3. **Given** una solicitud aprobada, **When** el usuario afectado vuelve a
   loguear, **Then** el sistema le exige reconfigurar su MFA (comportamiento
   ya existente del reset).

### Edge Cases

- Cambio de empresa cuando la única otra membresía fue suspendida durante la
  sesión: el selector debe reflejar el cambio (o rechazar con claridad).
- Cambio de empresa con la advertencia de descarte abierta en dos pestañas.
- Recarga con una credencial de sesión adulterada o expirada en el
  almacenamiento del navegador: tratar como sesión inválida, sin errores
  crudos.
- Uso de un código de recuperación cuando la política de la empresa exige
  MFA: el acceso procede (el código ES el segundo factor), pero el sistema
  recomienda reconfigurar el autenticador.
- Usuario que consume su último código de recuperación: acceso con aviso
  destacado de que debe regenerarlos o reconfigurar MFA de inmediato.
- Listado de aprobaciones cuando la solicitud fue cancelada por el usuario o
  ya expiró: debe desaparecer o mostrarse resuelta, nunca aprobar dos veces.

## Requirements *(mandatory)*

### Functional Requirements

#### Cambio de empresa en sesión (completa FR-019..FR-022 del 002)

- **FR-101**: El encabezado MUST mostrar siempre la empresa activa de la
  sesión y, para usuarios con más de una membresía activa, un control para
  cambiar de empresa que liste únicamente sus empresas activas.
- **FR-102**: Al confirmar un cambio de empresa, el sistema MUST recargar el
  contexto completo de la empresa destino y NEVER mostrar datos de la empresa
  anterior en la nueva sesión activa.
- **FR-103**: Si existe un formulario con cambios sin guardar, el sistema
  MUST advertir y pedir confirmación antes de descartar (FR-022 del 002).
- **FR-104**: Si el cambio falla porque la membresía destino ya no está
  activa, el sistema MUST informar el motivo y conservar la sesión actual.

#### Navegación

- **FR-105**: La navegación MUST exponer las opciones de cuenta (cambiar
  contraseña, MFA, empresa por defecto) a todo usuario autenticado.
- **FR-106**: La navegación MUST exponer la gestión de la empresa (miembros
  e invitaciones, política MFA) solo a administradores de la empresa activa,
  y la consola global solo al master admin. La restricción MUST aplicarse
  también en el acceso directo por URL (ya garantizado por el backend).
- **FR-107**: Toda pantalla de identidad que permanezca en el producto MUST
  ser alcanzable desde la navegación o desde un flujo de usuario.

#### Códigos de recuperación

- **FR-108**: El desafío de segundo factor MUST ofrecer la alternativa de
  ingresar un código de recuperación; un código válido autentica igual que
  el TOTP y queda invalidado de forma permanente e inmediata (un solo uso),
  incluso ante intentos simultáneos.
- **FR-109**: Los códigos usados o inexistentes MUST rechazarse con el mismo
  mensaje genérico del código TOTP incorrecto (sin revelar cuál era el caso).
- **FR-110**: Tras autenticar con un código, el sistema MUST informar la
  cantidad de códigos restantes y recomendar regenerarlos cuando queden 3 o
  menos.
- **FR-111**: El usuario MUST poder regenerar su juego de códigos desde su
  perfil, previa confirmación de identidad (contraseña o TOTP vigente); la
  regeneración invalida el juego anterior completo y queda auditada.
- **FR-112**: Todo uso y regeneración de códigos de recuperación MUST quedar
  en el registro de auditoría con actor y marca de tiempo.

#### Continuidad de sesión

- **FR-113**: La sesión autenticada MUST sobrevivir a recargas y navegación
  dentro de la misma pestaña conservando la empresa activa, y MUST terminar
  al cerrar la pestaña o el navegador (alcance de Clarifications 2026-08-01).
- **FR-114**: Al cerrar sesión, el sistema MUST eliminar todo artefacto de
  sesión del navegador; una sesión adulterada o expirada MUST tratarse como
  inválida y llevar al login sin errores crudos.

#### Remates de entrada

- **FR-115**: La pantalla de login MUST incluir el enlace al flujo de
  recuperación de contraseña.
- **FR-116**: Al aceptar una invitación sin desafíos de MFA pendientes, el
  sistema MUST dejar al usuario autenticado y llevarlo directo al tablero de
  la empresa invitante; con desafíos pendientes, MUST conservar el
  comportamiento actual (paso de MFA correspondiente).
- **FR-117**: El encabezado MUST mostrar el correo del usuario autenticado y
  el nombre de su empresa activa reales.

#### Higiene y aprobación de MFA reset

- **FR-118**: El producto MUST quedar con una única pantalla por función de
  MFA (configuración voluntaria y enrollment forzado); las pantallas
  duplicadas o sin flujo de entrada MUST retirarse o integrarse.
- **FR-119**: El aprobador de solicitudes de reset de MFA MUST disponer de
  un listado de solicitudes pendientes (solicitante, fecha, motivo) desde el
  cual aprobar o rechazar; una solicitud MUST poder resolverse una sola vez.

### Deuda de verificación heredada (entra en el plan de este feature)

Cerrar los tests de integración pendientes del feature 002 —
invite-register-login, replay de invitación, login mono-tenant, mensajes
genéricos, lockout progresivo, enrollment forzado, cambio multi-empresa,
cruce de tenants, alcance del tenant admin, promoción de admin y política
MFA — reutilizando la infraestructura de pruebas ya existente, más la
cobertura de lo nuevo de este feature.

### Key Entities

No se introducen entidades nuevas. Se reutilizan: los códigos de recuperación
ya emitidos por el enrollment (almacén existente), las solicitudes de reset
de MFA existentes (se agrega solo su listado/consulta), y las membresías y
sesiones del feature 002.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-101**: Un usuario multi-empresa cambia de cooperativa desde el
  encabezado en menos de 3 segundos y en ningún caso ve datos de la empresa
  anterior tras el cambio.
- **SC-102**: El 100 % de las pantallas de identidad activas son alcanzables
  desde la navegación o un flujo de usuario (cero pantallas huérfanas), y
  cada rol ve exactamente sus opciones.
- **SC-103**: Una usuaria sin su autenticador completa el login con un código
  de recuperación en menos de 1 minuto; cero códigos reutilizables (el
  segundo intento con el mismo código falla el 100 % de las veces).
- **SC-104**: Cero pérdidas de sesión por recarga de página dentro del
  alcance definido; tras cerrar sesión, cero artefactos de sesión
  recuperables desde el navegador.
- **SC-105**: Una invitada sin exigencias de MFA pasa de "clic en el correo"
  a "tablero de su empresa" sin volver a ingresar credenciales.
- **SC-106**: El aprobador resuelve una solicitud de reset de MFA desde el
  listado en menos de 1 minuto, sin ingresar identificadores técnicos.
- **SC-107**: La deuda de tests de integración del feature 002 queda en
  cero: todos los escenarios listados corren en verde en la suite.

## Assumptions

- **Sin modelo de datos nuevo**: los códigos de recuperación y las
  solicitudes de MFA reset ya se persisten; este feature solo agrega su uso,
  listado y regeneración.
- **Aprobador de MFA reset**: el listado y la aprobación son del master
  admin (coherente con el feature 002, donde el reset de MFA es potestad del
  master); si en el futuro se delega al admin de empresa, será otro feature.
- **Códigos de recuperación**: juego de 10, un solo uso, formato ya definido
  por el enrollment actual; la regeneración es explícita del usuario (no
  automática al agotarse).
- **Pantallas a retirar**: las duplicadas o sin flujo se eliminan del
  producto (no se ocultan); si alguna resulta necesaria más adelante, se
  reintroduce vía spec.
- **Idioma**: español, consistente con el producto.
- **Los comportamientos de negocio del 002 no cambian**: mismas reglas de
  membresías, políticas MFA, invitaciones y auditoría; este feature solo
  agrega superficie y continuidad.

## Out of Scope

- Claims de permisos por tenant para el gating fino de UI (`PermissionGate`)
  — requiere diseño de backend propio.
- Mitigación del timing side-channel del flujo "olvidé mi contraseña".
- Reorganización de la colección de auditoría para eventos globales.
- Delegación de aprobación de MFA reset a admins de empresa.
- Cambios al modelo de autorización por roles/permisos dentro de cada tenant.
