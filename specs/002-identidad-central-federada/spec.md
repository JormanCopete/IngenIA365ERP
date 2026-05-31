# Feature Specification: Identidad Central con Autorización Federada por Empresa

**Feature Branch**: `002-identidad-central-federada`

**Created**: 2026-05-30

**Status**: Draft

**Input**: User description: "Rediseño a Identidad Central con Autorización Federada por Empresa — eliminar la selección manual de cliente al iniciar sesión y migrar a un modelo donde la credencial vive una sola vez en la BD Admin, manteniendo roles/perfiles/permisos por tenant."

## Clarifications

### Session 2026-05-30

- Q: ¿Quién dentro de un tenant puede promover o degradar a otros administradores de esa misma empresa? → A: Cualquier admin activo del tenant puede promover/degradar a otros admins del mismo tenant (siempre que se respete la salvaguarda del "último admin activo"). El master admin actúa como respaldo.
- Q: ¿Qué transiciones de estado de membresía son válidas y cuáles requieren una nueva invitación? → A: `Suspended → Active` es reversible sin nueva invitación. `Revoked → Active` SOLO se logra emitiendo una nueva invitación a la persona; al aceptarla, se reactiva la membresía existente (no se crea una duplicada).
- Q: ¿Qué política de exigencia de MFA debe soportar el sistema en v1? → A: MFA opt-in por persona + el admin de empresa puede activar una política "MFA obligatorio para todos los miembros de mi empresa". Cuando esa política está activa, los miembros que aún no tienen MFA son obligados a configurarlo en su próximo login antes de poder operar.
- Q: ¿Qué política de contraseña debe aplicar el sistema para la identidad central? → A: Política NIST SP 800-63B moderna: mínimo 12 caracteres, sin obligación de mezclar tipos, sin rotación periódica obligatoria, validación contra una lista de contraseñas comprometidas conocidas (HaveIBeenPwned o equivalente), bloqueo temporal tras intentos fallidos repetidos.
- Q: ¿Cuántas implementaciones concretas de envío de correo deben estar disponibles en v1 y cuáles? → A: Solo SMTP propio en v1. La interfaz de envío de correo queda lista para incorporar otros proveedores (Azure Communication Services, SendGrid u otro) en versiones futuras sin cambios al resto del sistema.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Onboarding mediante invitación (Priority: P1)

Una persona que aún no tiene credencial recibe un correo de invitación a una empresa. Hace clic en el enlace, completa un formulario corto para crear su credencial central (correo + contraseña) y queda asociada como miembro activo de la empresa que la invitó. Si la persona ya tenía credencial central por una invitación previa de otra empresa, el sistema reconoce su identidad por correo y, en lugar de pedirle registrarse de nuevo, solo le confirma la nueva membresía con la empresa actual reutilizando su contraseña existente.

**Why this priority**: sin este flujo no existe forma alguna de que un usuario entre al sistema. Es el único camino de alta — no hay registro público abierto. Es la piedra angular del modelo "una identidad, varias empresas".

**Independent Test**: emitir una invitación a un correo nuevo, abrir el enlace en una sesión limpia, completar registro, y verificar que la persona puede iniciar sesión y queda asociada únicamente a la empresa que la invitó. Repetir con un correo ya registrado por otra invitación previa y verificar que NO se le pide nueva contraseña y queda asociado a ambas empresas.

**Acceptance Scenarios**:

1. **Given** una empresa emite una invitación a un correo que nunca antes ha sido registrado, **When** el destinatario abre el enlace dentro del periodo de validez y completa el formulario de registro, **Then** se crea su credencial central, se activa su membresía con esa empresa y queda autenticado en el dashboard de esa empresa.
2. **Given** una persona ya tiene credencial central por una invitación previa de la Empresa A, **When** la Empresa B la invita y la persona abre el enlace, **Then** el sistema reconoce su correo, no le pide crear contraseña nueva, le solicita iniciar sesión (o confirma con sesión activa) y activa su membresía con la Empresa B.
3. **Given** un enlace de invitación cuyo periodo de validez ya expiró, **When** el destinatario lo abre, **Then** el sistema muestra un mensaje claro de "invitación expirada" y NO crea ni activa nada; el destinatario puede solicitar una nueva invitación.
4. **Given** un enlace de invitación ya consumido por aceptación, **When** alguien intenta abrirlo de nuevo (mismo destinatario o un tercero con el enlace), **Then** el sistema rechaza el segundo uso indicando que la invitación ya fue aceptada.

---

### User Story 2 - Login centralizado sin selección manual de cliente (Priority: P1)

Una persona ya registrada entra a la URL única del producto, ingresa su correo y contraseña (y segundo factor si lo tiene activado) y obtiene acceso al ERP sin necesidad de elegir manualmente una empresa de un combo box. Si solo es miembro activo de una empresa, el sistema la lleva directamente al dashboard de esa empresa. Si no es miembro activo de ninguna empresa, el sistema le explica con claridad que debe solicitar una invitación.

**Why this priority**: es el reemplazo directo del flujo actual (combo box de cliente). Sin este flujo, los usuarios ya invitados no pueden entrar al sistema. Es lo que el negocio percibe como "el cambio visible" del rediseño.

**Independent Test**: con un usuario miembro activo de exactamente una empresa, iniciar sesión y verificar que entra directamente al dashboard de esa empresa sin ningún paso intermedio de selección. Con un usuario sin membresías activas, verificar que recibe un mensaje explicativo sin combo box y sin acceso al ERP.

**Acceptance Scenarios**:

1. **Given** un usuario activo en exactamente una empresa, **When** ingresa correo y contraseña correctos, **Then** el sistema lo lleva directamente al dashboard de esa empresa sin mostrar selector ni combo box.
2. **Given** un usuario que tiene segundo factor activado y es activo en una sola empresa, **When** ingresa correo y contraseña correctos, **Then** el sistema le solicita el código del segundo factor antes de entrar al dashboard de la empresa.
3. **Given** un usuario sin membresías activas (todas revocadas, suspendidas o inexistentes), **When** ingresa credenciales válidas, **Then** el sistema lo autentica pero le presenta un mensaje "No tienes acceso a ninguna empresa. Solicita una invitación." y no permite acceder al ERP.
4. **Given** un usuario que ingresa una contraseña incorrecta o un correo no registrado, **When** intenta autenticarse, **Then** el sistema rechaza el intento con un mensaje genérico que no revela si el correo existe o no.
5. **Given** la pantalla de login del producto, **When** un visitante la abre, **Then** no aparece ningún combo box de cliente ni listado de empresas — únicamente los campos correo y contraseña.

---

### User Story 3 - Selector y cambio de empresa para usuarios multi-empresa (Priority: P2)

Una persona que es miembro activa de varias empresas, al iniciar sesión, ve un selector que lista únicamente las empresas donde es miembro activo. Elige una y entra al dashboard de esa empresa. Una vez dentro, puede cambiar a otra de sus empresas desde un control visible en el encabezado sin necesidad de cerrar sesión y volver a entrar. El cambio recarga el contexto de la empresa destino (sus permisos, sus menús, sus datos) sin mezclar nada con la empresa anterior. Adicionalmente, la persona puede fijar una empresa por defecto para que el próximo inicio de sesión la lleve directamente allí, saltándose el selector mientras esa empresa siga siendo válida para ella.

**Why this priority**: cubre a los usuarios que pertenecen a más de una cooperativa (asesores, consultores externos, personal del grupo). Sin este flujo, esos usuarios quedarían bloqueados o necesitarían múltiples cuentas, lo que rompe el modelo de "una identidad". Es valor importante pero el sistema puede operar primero con usuarios mono-empresa.

**Independent Test**: con un usuario activo en tres empresas, iniciar sesión y verificar que el selector lista exactamente esas tres (ni más, ni menos). Entrar a una, cambiar a otra desde el encabezado y verificar que los permisos y datos visibles son los de la nueva empresa. Configurar una de ellas como por defecto, cerrar sesión, volver a entrar y verificar que el sistema lleva directo a la empresa por defecto sin mostrar el selector.

**Acceptance Scenarios**:

1. **Given** un usuario activo en tres empresas y sin empresa por defecto configurada, **When** inicia sesión, **Then** el sistema le muestra un selector con exactamente esas tres empresas listadas por nombre y ninguna otra.
2. **Given** un usuario logueado dentro de la Empresa A y miembro activo de las empresas A, B, C, **When** usa el control de cambio de empresa en el encabezado para ir a la Empresa B, **Then** el sistema recarga el contexto y a partir de ese momento todos los permisos, menús, datos y reportes corresponden a la Empresa B; ningún dato de A queda accesible en pantalla.
3. **Given** un usuario activo en varias empresas que selecciona "Establecer como empresa por defecto" estando dentro de la Empresa B, **When** cierra sesión y vuelve a iniciar sesión, **Then** el sistema lo lleva directamente al dashboard de la Empresa B sin pasar por el selector.
4. **Given** un usuario con empresa por defecto configurada en la Empresa B y cuya membresía con la Empresa B fue revocada, **When** inicia sesión, **Then** el sistema ignora la empresa por defecto inválida, muestra el selector con sus empresas restantes activas y, si solo le queda una activa, entra directo a esa.
5. **Given** un usuario logueado dentro de la Empresa A, **When** un administrador suspende o revoca su membresía con la Empresa A durante la sesión, **Then** la próxima operación del usuario contra la Empresa A es rechazada y el sistema lo redirige al selector (o a la pantalla de "sin empresas" si era la única).

---

### User Story 4 - Gestión de usuarios por administrador de empresa (Priority: P2)

Un usuario marcado como administrador de una empresa puede invitar a nuevos miembros a su propia empresa indicando el correo destinatario, y posteriormente puede asignarles roles y permisos dentro del modelo de autorización existente de esa empresa. No puede ver, invitar ni gestionar usuarios de otras empresas, ni acceder a las configuraciones globales del producto (suscripciones, listado global de tenants).

**Why this priority**: habilita la operación autónoma de cada cooperativa sin depender del administrador master para cada alta. Es indispensable para que el negocio escale, pero el sistema puede arrancar piloto con el master admin emitiendo todas las invitaciones.

**Independent Test**: con un usuario administrador de la Empresa A, emitir una invitación desde la consola de su empresa y verificar que (a) el destinatario la recibe asociada a la Empresa A y a ninguna otra; (b) el administrador no encuentra opciones para invitar a otras empresas; (c) el administrador no ve datos ni usuarios de otras empresas.

**Acceptance Scenarios**:

1. **Given** un usuario con flag de administrador de empresa en la Empresa A, **When** emite una invitación desde la pantalla de gestión de usuarios de la Empresa A, **Then** se genera la invitación únicamente para la Empresa A y se envía correo al destinatario con el enlace.
2. **Given** un administrador de la Empresa A, **When** intenta acceder a la pantalla de gestión de usuarios de la Empresa B (por URL directa o por cualquier otro medio), **Then** el sistema le niega el acceso.
3. **Given** un nuevo miembro recién activado en la Empresa A por aceptar una invitación, **When** el administrador de la Empresa A le asigna roles y perfiles desde la consola de la Empresa A, **Then** los permisos quedan registrados en el modelo de autorización de la Empresa A y son los que se aplican al miembro al operar dentro de esa empresa.
4. **Given** un administrador de la Empresa A, **When** intenta invitar a un usuario marcando la opción "administrador master" o similar de alcance global, **Then** el sistema no le ofrece ni permite esa opción.

---

### User Story 5 - Gobierno por administrador master (Priority: P3)

Una persona designada como administrador master del producto puede crear tenants nuevos, gestionar suscripciones, e invitar al primer administrador de cada empresa (o invitar administradores adicionales) para que esa empresa pueda operar autónomamente. Es la única figura con capacidad de designar administradores de empresa y de ver el catálogo global de tenants y suscripciones.

**Why this priority**: necesario para incorporar nuevas cooperativas al producto, pero un piloto con un solo tenant puede funcionar sin esta función operativa al inicio. La gestión de suscripciones puede ejecutarse manualmente al principio.

**Independent Test**: con un usuario marcado como administrador master, crear un tenant nuevo, emitir una invitación marcada como "administrador de empresa" hacia un correo nuevo, y verificar que el destinatario, al aceptar, queda automáticamente como administrador de la empresa recién creada.

**Acceptance Scenarios**:

1. **Given** un usuario administrador master, **When** crea un tenant nuevo y emite una invitación marcándola como "administrador de empresa" para esa empresa, **Then** el destinatario recibe un correo con enlace y, al aceptar, queda como miembro activo y administrador de esa empresa.
2. **Given** un usuario administrador master, **When** accede a la consola de gobierno global, **Then** puede ver el listado completo de tenants existentes y el estado de sus suscripciones.
3. **Given** un usuario administrador master, **When** revoca la membresía o el rol de administrador de un usuario en una empresa, **Then** el cambio surte efecto en el siguiente intento de operación de ese usuario sobre esa empresa.
4. **Given** un usuario que NO es administrador master, **When** intenta acceder a la consola de gobierno global o a la creación de tenants, **Then** el sistema le niega el acceso.

---

### Edge Cases

- **Invitación a un correo ya invitado y aún pendiente**: si una empresa emite una nueva invitación al mismo correo y para la misma empresa cuando aún hay una pendiente, el sistema debe reemplazar la pendiente anterior (o reusarla) y no acumular invitaciones duplicadas.
- **Reaceptación de invitación tras revocación**: un usuario cuya membresía con una empresa fue revocada puede ser invitado de nuevo más tarde; el flujo debe reactivar la membresía existente, no crear duplicados.
- **Cambio de contraseña**: el cambio de contraseña aplica a la identidad central y por tanto afecta el acceso a TODAS las empresas del usuario simultáneamente. El usuario debe ser advertido de esto al cambiarla.
- **Pérdida del segundo factor**: un usuario que pierde acceso al dispositivo de segundo factor necesita un mecanismo de recuperación que NO comprometa la seguridad (por ejemplo, recuperación gestionada por el administrador master y registrada en auditoría).
- **Empresa por defecto eliminada o desactivada**: si la empresa marcada como por defecto deja de existir o queda inactiva, la preferencia debe limpiarse silenciosamente y el siguiente login mostrar el selector normal.
- **Único administrador de una empresa**: el sistema debe impedir que un administrador único de una empresa se autorrevoque o se autoexpulse de su propio rol de administrador sin que exista al menos otro administrador en esa empresa, para evitar dejar la empresa sin gobierno.
- **Usuario con cero membresías que intenta entrar repetidamente**: el sistema debe mostrar consistentemente el mensaje "Solicita una invitación" y registrar el intento en auditoría sin permitir acceso al ERP.
- **Token de invitación interceptado**: el enlace de invitación se invalida al primer uso exitoso y al ser revocado por quien lo emitió; no puede ser reutilizado.
- **Concurrencia en aceptación de invitación**: dos pestañas o dispositivos que intentan aceptar la misma invitación simultáneamente deben dar lugar a una sola activación de membresía; el segundo intento recibe el resultado del primero sin duplicar la membresía.
- **Cambio de empresa con cambios sin guardar**: si el usuario está en medio de un formulario al cambiar de empresa, el sistema debe advertirle que perderá cambios no guardados antes de proceder.

## Requirements *(mandatory)*

### Functional Requirements

#### Identidad y autenticación central

- **FR-001**: El sistema MUST mantener la credencial de cada persona (correo, contraseña, configuración de segundo factor) en un único almacén central compartido entre todas las empresas, y NEVER duplicarla por empresa.
- **FR-002**: El sistema MUST tratar el correo electrónico de manera única e insensible a mayúsculas/minúsculas como identificador de la identidad central de una persona.
- **FR-003**: El sistema MUST permitir activar segundo factor de autenticación de manera opcional por persona, y MUST exigirlo en cada inicio de sesión cuando esté activado.
- **FR-003a**: El sistema MUST permitir a un administrador de empresa activar una política "MFA obligatorio" para todos los miembros de su empresa. La política aplica solo a esa empresa.
- **FR-003b**: Cuando una empresa tiene la política "MFA obligatorio" activa, el sistema MUST forzar a cualquier miembro de esa empresa sin MFA configurado a configurarlo en su próximo inicio de sesión, antes de permitirle operar dentro de la empresa. NEVER se permite saltar este paso mientras la política esté activa.
- **FR-003c**: Si una persona es miembro activa de varias empresas y AL MENOS UNA de ellas tiene "MFA obligatorio" activa, el sistema MUST exigirle MFA en el inicio de sesión central (no se puede tener MFA solo para algunas empresas — la credencial central es única).
- **FR-003d**: El sistema MUST registrar en auditoría toda activación o desactivación de la política "MFA obligatorio" en una empresa, identificando actor, empresa y marca de tiempo.
- **FR-004**: El sistema MUST autenticar a la persona contra la identidad central antes de cualquier consideración de empresa.
- **FR-005**: El sistema MUST emitir, tras una autenticación exitosa y la selección/resolución de empresa, un contenedor de sesión que contenga al menos: identificador de la identidad central, correo, indicador de si la persona es administrador master, y la empresa activa de la sesión.
- **FR-006**: El sistema MUST permitir abstraer el mecanismo de autenticación detrás de una interfaz de proveedor de identidad, de manera que la lógica de negocio NEVER dependa de la implementación concreta del almacén de credenciales (esto es para habilitar una futura sustitución por un proveedor externo sin reescribir flujos).

#### Acceso por URL única

- **FR-007**: El sistema MUST exponer un único punto de entrada de inicio de sesión para todas las empresas; NEVER habrá subdominios distintos por empresa ni combos de selección de cliente en la pantalla de login.
- **FR-008**: El sistema MUST resolver la empresa activa de la sesión a partir del contexto del usuario autenticado, NEVER a partir del subdominio ni de un parámetro elegido por el usuario en la pantalla de login.

#### Membresía y autorización federada

- **FR-009**: El sistema MUST mantener, en el ámbito central, la relación entre cada persona y las empresas a las que pertenece, con estados que distingan al menos: invitada, activa, suspendida y revocada.
- **FR-009a**: El sistema MUST permitir las siguientes transiciones de estado de membresía y rechazar el resto:
  - `Invited → Active` (al aceptar invitación).
  - `Invited → Revoked` (al revocar la invitación pendiente o al expirar).
  - `Active → Suspended` (acción reversible del administrador).
  - `Suspended → Active` (reactivación sin nueva invitación).
  - `Active → Revoked` y `Suspended → Revoked` (acción terminal del administrador).
  - `Revoked → Active` SOLO es posible emitiendo una nueva invitación a la misma persona y empresa; al aceptarla, se reactiva la membresía existente (NEVER se crea un duplicado).
- **FR-010**: El sistema MUST impedir el acceso operativo a una empresa cuando el estado de membresía de la persona en esa empresa no sea "activa".
- **FR-011**: El sistema MUST resolver los roles, perfiles y permisos operativos de la persona dentro de una empresa consultando exclusivamente el almacén de autorización de esa empresa; NEVER se derivarán permisos desde el contenedor de sesión central ni desde la información de la identidad central.
- **FR-012**: El sistema MUST verificar, en cada operación realizada dentro de una empresa, que (a) la sesión central es válida; (b) existe una membresía activa entre la persona y la empresa de la sesión; (c) los permisos asignados en la empresa autorizan la operación. La ausencia de cualquiera de las tres condiciones MUST resultar en denegación.

#### Flujo de login

- **FR-013**: Tras una autenticación exitosa, si la persona tiene exactamente una membresía activa, el sistema MUST entrar directamente al dashboard de esa empresa sin solicitar selección.
- **FR-014**: Tras una autenticación exitosa, si la persona tiene más de una membresía activa, el sistema MUST presentar un selector que liste únicamente las empresas en las que está activa.
- **FR-015**: Tras una autenticación exitosa, si la persona no tiene ninguna membresía activa, el sistema MUST presentarle un mensaje claro indicando que debe solicitar una invitación y NEVER permitir el acceso a ninguna empresa.
- **FR-016**: El sistema MUST permitir a cada persona configurar una empresa preferida por defecto entre aquellas en las que es miembro activa.
- **FR-017**: Tras una autenticación exitosa, si la persona tiene más de una membresía activa Y tiene una empresa por defecto configurada Y esa empresa por defecto sigue siendo válida (activa), el sistema MUST llevarla directamente al dashboard de esa empresa por defecto sin mostrar el selector.
- **FR-018**: Si la empresa por defecto configurada deja de ser válida, el sistema MUST descartar silenciosamente esa preferencia en el siguiente login y comportarse como si no hubiera empresa por defecto.

#### Cambio de empresa en sesión activa

- **FR-019**: El sistema MUST ofrecer a las personas con más de una membresía activa un control visible y persistente, ubicado en el encabezado, para cambiar a otra de sus empresas sin cerrar sesión.
- **FR-020**: Al cambiar de empresa en sesión activa, el sistema MUST recargar el contexto completo de la empresa destino (permisos, menús, datos visibles) y NEVER mezclar información de la empresa anterior en la nueva sesión activa.
- **FR-021**: Al cambiar de empresa, el sistema MUST actualizar el contenedor de sesión con el identificador de la empresa activa nueva.
- **FR-022**: Si la persona tiene cambios sin guardar en pantalla al iniciar un cambio de empresa, el sistema MUST advertirle y solicitar confirmación antes de descartarlos.

#### Invitaciones

- **FR-023**: El sistema MUST tratar la invitación por correo electrónico como el ÚNICO mecanismo para crear una nueva identidad central o una nueva membresía con una empresa. NEVER existirá un formulario público de registro abierto.
- **FR-024**: El sistema MUST permitir a un administrador de empresa emitir invitaciones para nuevos miembros únicamente de su propia empresa.
- **FR-025**: El sistema MUST permitir al administrador master emitir invitaciones a cualquier empresa, incluyendo invitaciones marcadas como "administrador de empresa".
- **FR-026**: El sistema MUST impedir que un administrador de empresa emita invitaciones marcadas como "administrador" (esa potestad es exclusiva del administrador master).
- **FR-027**: Cada invitación MUST contener un enlace con un token criptográficamente seguro, de un solo uso, con periodo de validez limitado.
- **FR-028**: El sistema MUST invalidar automáticamente el token de invitación al aceptarla, al revocarla o al expirar.
- **FR-029**: El sistema MUST manejar dos casos al abrir un enlace de invitación: (a) si el correo destinatario aún no tiene identidad central, presentar un formulario de creación de credencial y, al completarlo correctamente, crear la identidad central y activar la membresía; (b) si el correo ya tiene identidad central, solicitar autenticación (o reutilizar la sesión activa) y, una vez autenticado, activar la nueva membresía sin volver a pedir contraseña.
- **FR-030**: El sistema MUST garantizar que una invitación aceptada no pueda reutilizarse, incluso ante intentos simultáneos desde múltiples dispositivos.
- **FR-031**: El sistema MUST permitir a quien emitió una invitación revocarla mientras esté pendiente, invalidando el token correspondiente.
- **FR-032**: El sistema MUST registrar en auditoría toda invitación emitida, aceptada, expirada o revocada, identificando emisor, destinatario, empresa, marca de tiempo, resultado y, cuando aplique, IP y agente del aceptante.

#### Auditoría y trazabilidad

- **FR-033**: El sistema MUST registrar en auditoría todo cambio de empresa activa dentro de una sesión, identificando persona, empresa anterior, empresa nueva y marca de tiempo.
- **FR-034**: El sistema MUST registrar en auditoría todo cambio de estado de una membresía (activación, suspensión, revocación) identificando actor responsable y marca de tiempo.
- **FR-035**: El sistema MUST registrar en auditoría los intentos de inicio de sesión exitosos y fallidos, sin almacenar contraseñas en claro ni en formato recuperable.
- **FR-036**: El sistema MUST conservar los registros de auditoría con la retención mínima exigida por las normas financieras aplicables al producto.

#### Gestión por administrador master

- **FR-037**: El sistema MUST permitir al administrador master crear nuevos tenants y gestionar sus suscripciones.
- **FR-038**: El sistema MUST permitir al administrador master designar a uno o varios administradores de empresa para cualquier tenant.
- **FR-039**: El sistema MUST permitir al administrador master suspender o revocar membresías y roles administrativos de cualquier persona en cualquier tenant.

#### Reglas de salvaguarda

- **FR-040**: El sistema MUST impedir que una empresa quede sin al menos un administrador activo; la auto-revocación o auto-degradación del último administrador de una empresa MUST ser rechazada.
- **FR-040a**: El sistema MUST permitir que cualquier administrador activo de una empresa promueva a otro miembro activo de su empresa al rol de administrador, o degrade a otro administrador de su empresa al rol de miembro regular, siempre que la operación respete FR-040. El administrador master conserva la misma facultad como respaldo sobre cualquier empresa.
- **FR-040b**: El sistema MUST registrar en auditoría toda promoción o degradación del rol de administrador de empresa, identificando actor, persona afectada, empresa y marca de tiempo.
- **FR-041**: El sistema MUST devolver mensajes de error genéricos en el flujo de login que no revelen si un correo está o no registrado.
- **FR-042**: El sistema MUST aplicar protección frente a fuerza bruta en el inicio de sesión (por ejemplo, ralentización progresiva o bloqueo temporal por intentos repetidos fallidos).
- **FR-043**: El sistema MUST aceptar contraseñas de longitud mínima de 12 caracteres y NEVER imponer obligación de mezclar tipos (mayúsculas, minúsculas, números, símbolos) ni rotación periódica obligatoria, siguiendo las recomendaciones vigentes de NIST SP 800-63B.
- **FR-044**: El sistema MUST validar toda contraseña propuesta (en alta, cambio o reset) contra una lista de contraseñas conocidas comprometidas (por ejemplo, HaveIBeenPwned u origen equivalente) y rechazarla con mensaje accionable si aparece en la lista.
- **FR-045**: El sistema MUST mostrar al usuario, durante el registro o cambio de contraseña, retroalimentación clara en tiempo real sobre el cumplimiento de los requisitos (longitud mínima y, cuando aplique, comprobación contra lista de comprometidas), tanto en cliente como en servidor (validación dual conforme al principio constitucional VIII).
- **FR-046**: El sistema MUST aplicar bloqueo temporal del intento de login tras un número configurable de intentos fallidos consecutivos por mismo correo, con duración progresiva, y registrar el bloqueo en auditoría.

### Key Entities *(include if feature involves data)*

- **Identidad Central (CentralUser)**: representa a una persona única en el ámbito global del producto. Atributos relevantes: identificador único, correo (único, normalizado), credencial de autenticación, estado (activa, pendiente, deshabilitada), configuración de segundo factor, preferencia de empresa por defecto, indicador de administrador master, momento de creación y de último inicio de sesión. NEVER almacena roles ni permisos operativos.
- **Membresía con Empresa (TenantMembership)**: representa la relación entre una identidad central y una empresa específica. Atributos relevantes: persona, empresa, estado (invitada, activa, suspendida, revocada), indicador de si la persona es administrador de esa empresa, quién invitó, momento de invitación y de activación. Una persona puede tener varias membresías; el par (persona, empresa) es único.
- **Invitación (Invitation)**: representa una solicitud de incorporar a un correo a una empresa específica. Atributos relevantes: correo destinatario, empresa de destino, indicador de invitación como administrador, quién emitió, token de uso único, momento de creación, periodo de validez, estado (pendiente, aceptada, expirada, revocada), momento de aceptación. El token es criptográficamente seguro y se invalida al primer uso o al revocarse.
- **Empresa / Tenant (Tenant)**: ya existe en el modelo del producto; en esta feature se incorpora únicamente como referencia desde Membresía e Invitación, sin modificar sus atributos ni su gobierno interno. Cada empresa sigue manteniendo su propio almacén separado de roles, perfiles y permisos.
- **Usuario de Tenant (Tenant User)**: la entidad existente en el almacén de cada empresa que representa al miembro y agrupa sus roles/perfiles operativos. En esta feature se vincula a la identidad central por referencia; NEVER vuelve a almacenar credencial propia.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100 % de los inicios de sesión en producción ocurren sin presentar un combo box ni un selector de cliente previo a la autenticación.
- **SC-002**: Una persona miembro de una sola empresa completa su inicio de sesión y llega al dashboard en menos de 5 segundos sin acción manual de selección de empresa.
- **SC-003**: Una persona miembro de varias empresas completa el cambio de empresa en sesión activa en menos de 3 segundos desde el clic en el control de cambio hasta ver el dashboard de la nueva empresa.
- **SC-004**: El 100 % de las altas de usuarios nuevos ocurren a través de una invitación aceptada — cero altas creadas por un formulario público de registro abierto.
- **SC-005**: Cero incidentes de cruce de datos entre empresas tras el cambio de empresa en sesión activa, medidos por pruebas automatizadas y auditoría manual.
- **SC-006**: Cero usuarios capaces de iniciar sesión y ver datos de una empresa donde su membresía no esté activa, verificado por pruebas de seguridad.
- **SC-007**: El 100 % de las invitaciones emitidas en producción quedan registradas en auditoría con emisor, destinatario, empresa, resultado (aceptada/expirada/revocada) y marca de tiempo.
- **SC-008**: Tasa de éxito de aceptación de invitaciones (clic en enlace → membresía activa) superior al 90 % medida durante el primer trimestre tras lanzamiento.
- **SC-009**: Cero invitaciones aceptadas más de una vez (el sistema rechaza correctamente el segundo intento en el 100 % de los casos).
- **SC-010**: Tiempo medio para incorporar a un usuario nuevo a una empresa (desde emisión de invitación hasta acceso operativo) inferior a 5 minutos en condiciones normales.
- **SC-011**: El 100 % de los cambios de empresa activa en sesión quedan registrados en auditoría con persona, empresa origen, empresa destino y marca de tiempo.
- **SC-012**: Cero empresas en producción que queden sin al menos un administrador activo tras una operación de revocación o degradación.

## Assumptions

- **Identidad y autenticación**: la implementación inicial usará el mecanismo de identidad y emisión de tokens nativo del marco de aplicación del producto, pero estará abstraída detrás de una interfaz de proveedor de identidad para permitir una futura sustitución por un proveedor externo gestionado (por ejemplo, Entra External ID) sin reescribir flujos de negocio.
- **Sin migración de usuarios**: es una instalación nueva. NO se entrega lógica ni scripts de migración de usuarios existentes desde el sistema anterior.
- **Mensajería de correo**: el envío de correos de invitación se abstrae detrás de una interfaz única de envío de correo. La única implementación disponible en v1 es **SMTP propio** (alineado con el despliegue en VPS). Otros proveedores (Azure Communication Services, SendGrid u otros) se podrán incorporar en versiones futuras detrás de la misma interfaz, sin tocar la lógica de negocio.
- **Periodo de validez de invitación**: por defecto las invitaciones son válidas durante 7 días naturales desde su emisión, salvo configuración explícita en contrario por el administrador master.
- **Idioma de la interfaz y correos**: español, consistente con el resto del producto.
- **Multi-tenancy schema-per-tenant**: se mantiene intacta la separación de bases de datos / esquemas por tenant para datos operativos. La identidad central, la membresía y las invitaciones viven exclusivamente en el almacén global de administración del producto; los roles, perfiles y permisos siguen viviendo en el almacén de cada tenant.
- **Modelo de autorización por empresa**: NO se modifica el modelo de roles/perfiles/permisos existente dentro de cada tenant. Los administradores de empresa siguen gestionándolos con las herramientas actuales.
- **Recuperación de segundo factor**: existe un procedimiento operativo (no automatizado en esta feature) por el cual el administrador master puede restablecer el segundo factor de una persona, con registro completo en auditoría. La automatización (códigos de recuperación auto-servicio) queda fuera del alcance v1.
- **Recuperación de contraseña**: el flujo "olvidé mi contraseña" sigue la lógica estándar (enlace temporal por correo) y se aplica a la identidad central (afecta a todas las empresas del usuario). Su diseño detallado se asume estándar y se cubre en el plan, no en el spec.
- **Sesión y expiración**: la sesión central tiene una expiración por inactividad razonable (por ejemplo, 30 a 60 minutos) y un mecanismo de renovación silenciosa estándar; los detalles técnicos se definen en el plan.
- **Auditoría de seguridad**: los eventos de identidad y membresía se registran en el almacén de auditoría existente del producto, con la retención mínima exigida por la normativa financiera aplicable (cinco años o superior según norma vigente).
- **Empresa por defecto**: la preferencia se almacena en la identidad central de cada persona y es configurable solo por la propia persona desde su perfil.
