# Feature Specification: Fase 0 — Cimientos técnicos del Módulo de Nómina

**Feature Branch**: `001-cimientos-tecnicos`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "Plan de Desarrollo por Fases — Módulo de Nómina. FASE 0 — Cimientos técnicos. Objetivo: construir la infraestructura sobre la que se montan todos los módulos. Alcance: multi-empresa/multi-sede con NIT y sucursales; autenticación con MFA y políticas de expiración; roles y permisos granulares; audit log inmutable append-only; soft-delete universal; adjuntos cifrados; notificaciones correo+en-app; habeas data. Entregables: API base con autenticación, panel de administración de usuarios/roles, consulta del audit log con filtros. Aceptación: toda acción de módulos posteriores queda registrada automáticamente, sin permiso no se ve el endpoint, 100 usuarios concurrentes sin degradación."

## Clarifications

### Session 2026-05-28

- Q: ¿Cuál es el objetivo de disponibilidad (uptime) que el servicio debe ofrecer a las cooperativas? → A: 99.5% mensual (≈ 3h 39m de indisponibilidad permitida por mes), con ventana de mantenimiento programado fuera de horario laboral.
- Q: ¿Cuál es la vigencia del token de sesión y la política de inactividad? → A: Access token 30 min, refresh token 12 h, cierre automático tras 30 min de inactividad.
- Q: ¿Cómo se resuelve la edición concurrente de un mismo registro? → A: Concurrencia optimista con sello de versión: el segundo en guardar es rechazado con mensaje accionable y debe recargar antes de reintentar.
- Q: ¿Qué formatos de exportación debe ofrecer el audit log? → A: CSV (análisis interno) + PDF firmado con hash de integridad y página de portada (entrega regulatoria).
- Q: ¿Cómo se recupera el acceso cuando un usuario pierde su segundo factor? → A: Códigos de respaldo generados al inscribir MFA (uso único) + reset administrativo con doble aprobación de dos administradores de empresa y verificación documental fuera de banda; toda la operación queda registrada en auditoría.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Acceso seguro multi-empresa con MFA (Priority: P1)

Un usuario administrador de una cooperativa entra al sistema, identifica su empresa (cooperativa con NIT) y, opcionalmente, su sucursal de trabajo. Tras escribir usuario y contraseña, el sistema le exige un segundo factor (código de seis dígitos generado por una app autenticadora) y, si todo es válido, lo deja autenticado dentro del contexto de su empresa. Si la contraseña venció, el sistema lo obliga a cambiarla antes de continuar.

**Why this priority**: sin un mecanismo de acceso seguro y vinculado a la empresa correcta, ningún otro módulo puede funcionar. El aislamiento empresa-por-empresa es requisito legal frente a la Superintendencia de la Economía Solidaria y MFA es el factor de seguridad mínimo aceptable para datos financieros de asociados.

**Independent Test**: registrar manualmente una empresa con su NIT, registrar un usuario con rol administrador, vincularlo a la empresa, completar el alta de su segundo factor y verificar que (a) el login exige contraseña + segundo factor, (b) un intento con segundo factor incorrecto falla, (c) tras N intentos fallidos consecutivos la cuenta queda bloqueada temporalmente, (d) el token de sesión emitido identifica inequívocamente la empresa del usuario, (e) una contraseña expirada obliga a cambiarla antes de operar.

**Acceptance Scenarios**:

1. **Given** un usuario activo con MFA configurado y contraseña vigente, **When** envía credenciales correctas y luego un código MFA válido, **Then** recibe un token de sesión que lleva su identidad, su empresa y su sucursal (si aplica), y queda autorizado para llamar endpoints protegidos.
2. **Given** un usuario activo, **When** envía contraseña correcta pero código MFA inválido cinco veces consecutivas, **Then** el sistema bloquea temporalmente la cuenta durante al menos 15 minutos, registra el evento y notifica al usuario por correo.
3. **Given** un usuario cuya contraseña expiró según la política de la empresa, **When** envía credenciales correctas y MFA válido, **Then** el sistema responde "cambio de contraseña requerido" y exige una nueva contraseña que cumpla la política antes de emitir un token operativo.
4. **Given** una empresa con dos sucursales y un usuario asignado a ambas, **When** el usuario inicia sesión y elige una sucursal específica, **Then** el contexto de la sesión refleja esa sucursal y persiste hasta cierre de sesión o cambio explícito.

---

### User Story 2 - Administración de usuarios, roles y permisos granulares (Priority: P1)

Un administrador entra a un panel donde puede dar de alta usuarios para su empresa, asignarles roles, y definir qué pueden hacer esos roles sobre cada entidad de negocio (ver, crear, editar, eliminar, aprobar). Un usuario sin permiso para "ver empleados" no encuentra siquiera el menú ni el endpoint que listaría empleados — la respuesta del servidor es indistinguible de "ese recurso no existe".

**Why this priority**: la autenticación sin autorización deja a todos los usuarios viendo todo. Los roles granulares son condición previa para que cualquier módulo posterior (asociados, contabilidad, nómina) tenga sentido en operación real. Sin esta capa, no podemos garantizar el principio de mínimo privilegio que SARLAFT exige.

**Independent Test**: crear dos roles ("Operador de Nómina" y "Auditor"), asignarlos a dos usuarios distintos, definir que el operador puede crear y editar empleados pero no aprobarlos, y el auditor solo puede ver y aprobar. Verificar que cada usuario solo encuentra los menús y endpoints permitidos por su rol, que los demás devuelven 403/404, y que un cambio de permisos surte efecto en la siguiente petición del usuario.

**Acceptance Scenarios**:

1. **Given** un rol "Operador de Nómina" con permiso "Empleado.Crear" pero sin "Empleado.Aprobar", **When** un usuario con ese rol intenta llamar el endpoint de aprobación de empleados, **Then** recibe un rechazo que no revela la existencia del endpoint (404 o equivalente) y la negativa queda registrada.
2. **Given** un administrador de empresa, **When** crea un rol nuevo y le marca permisos por entidad/acción, **Then** los usuarios con ese rol ven los menús y endpoints autorizados, y los demás permanecen invisibles.
3. **Given** un usuario con permisos parciales, **When** el administrador le agrega un permiso adicional, **Then** la siguiente petición del usuario refleja el nuevo permiso sin necesidad de reiniciar sesión más allá de un refresco de token.
4. **Given** un usuario asignado a la empresa A, **When** intenta acceder a datos de la empresa B usando manipulación de cabeceras o parámetros, **Then** el acceso falla y el intento queda registrado como evento de seguridad.

---

### User Story 3 - Audit log inmutable consultable (Priority: P2)

Cualquier acción que cree, modifique o elimine datos en cualquier módulo queda registrada automáticamente en un registro de auditoría inmutable. Un auditor puede entrar al sistema y filtrar el registro por usuario, fecha, empresa, entidad afectada y tipo de acción, y para cada entrada ver: quién, cuándo, desde qué IP, qué entidad, qué acción y los valores antes/después de la modificación. Nadie — ni siquiera un administrador — puede editar ni borrar entradas existentes.

**Why this priority**: la traza regulatoria es obligación SARLAFT y SIPLA. La auditoría debe estar viva antes que los módulos que produce datos auditables; meterla después implica perder la historia inicial. La consulta filtrada es indispensable durante una visita de la Superintendencia.

**Independent Test**: realizar diez acciones distintas (crear, editar, eliminar) desde dos usuarios de la misma empresa, abrir el panel de consulta del audit log, filtrar por usuario y verificar que cada acción aparece con sus campos completos. Intentar editar o borrar una entrada desde el código de aplicación y desde la base de datos directamente: la edición debe fallar (la tabla es append-only y el control técnico lo impide) o ser detectable por mecanismos de integridad.

**Acceptance Scenarios**:

1. **Given** un usuario realiza una creación, modificación o eliminación de datos en cualquier módulo, **When** la transacción completa con éxito, **Then** existe en el audit log una entrada con timestamp UTC, identidad del usuario, identidad de la empresa, IP de origen, agente, tipo de operación, entidad afectada (tipo + identificador público) y los valores antes/después de los campos modificados.
2. **Given** un auditor entra a la consulta del audit log, **When** aplica filtros de fecha, usuario, entidad y tipo de acción, **Then** recibe los resultados que coinciden con todos los filtros, paginados, en menos de cinco segundos para ventanas de un mes.
3. **Given** una entrada existente en el audit log, **When** se intenta modificarla o eliminarla mediante cualquier ruta de la aplicación, **Then** la operación es rechazada y el intento de manipulación queda a su vez registrado.
4. **Given** acciones realizadas en distintas empresas, **When** un auditor de la empresa A consulta el log, **Then** solo ve registros de la empresa A; los de la empresa B no aparecen.
5. **Given** el sistema lleva más de cinco años en operación, **When** se consulta el log de una acción de hace 4 años y 11 meses, **Then** la entrada sigue disponible (no fue purgada antes del plazo regulatorio).

---

### User Story 4 - Soft-delete universal y restauración (Priority: P2)

Cuando un usuario "elimina" un registro de cualquier módulo, ese registro deja de aparecer en listados, búsquedas y reportes operativos, pero no se borra físicamente. Un usuario con permiso de restauración puede recuperar registros borrados por equivocación, con visibilidad de quién los borró, cuándo y desde dónde.

**Why this priority**: prevenir pérdida de datos por error humano y garantizar trazabilidad. Es un comportamiento transversal que, igual que la auditoría, debe estar instalado antes de que los módulos hijos empiecen a producir registros borrables.

**Independent Test**: crear una entidad de prueba, "eliminarla" desde el panel correspondiente, confirmar que desaparece de las listas estándar, abrir la papelera/historial y verificar que aparece con autor de la eliminación, fecha y origen. Restaurarla y verificar que vuelve a aparecer en las listas estándar.

**Acceptance Scenarios**:

1. **Given** un usuario con permiso "Eliminar" sobre una entidad, **When** la elimina desde el panel, **Then** el registro deja de aparecer en listados, búsquedas y reportes operativos por defecto, pero permanece físicamente en la base con la marca de eliminación, autor, fecha UTC y motivo (si la operación pidió uno).
2. **Given** un usuario con permiso "Restaurar" sobre una entidad eliminada, **When** la restaura, **Then** el registro vuelve a aparecer en listados y queda asentado en auditoría quién lo restauró, cuándo y desde dónde.
3. **Given** un movimiento contable ya asentado (asiento, transacción de préstamo, transacción de nómina), **When** un usuario intenta eliminarlo desde la aplicación, **Then** la operación es rechazada — los movimientos contables solo se corrigen con movimientos reversos.

---

### User Story 5 - Adjuntos seguros y cifrados (Priority: P3)

Un usuario puede asociar a un trámite o entidad documentos respaldatorios (PDF, imágenes, hojas de cálculo). Los documentos quedan almacenados de forma segura — cifrados en reposo — y solo son recuperables por usuarios con permiso sobre la entidad a la que están vinculados.

**Why this priority**: capacidad transversal que múltiples módulos necesitarán (cédulas, contratos, soportes contables, autorizaciones). No bloquea las capacidades anteriores, pero debe quedar lista antes de que el primer módulo de negocio la requiera.

**Independent Test**: subir un documento PDF a un trámite ficticio, comprobar que el archivo en almacenamiento no es legible sin la clave (no se obtiene texto plano abriéndolo directamente), descargarlo desde la aplicación con un usuario autorizado y verificar que el contenido recuperado coincide con el original. Intentar la descarga con un usuario sin permiso y confirmar que es rechazada.

**Acceptance Scenarios**:

1. **Given** un usuario con permiso sobre una entidad, **When** sube un documento dentro del tamaño y tipo permitidos, **Then** el sistema lo almacena cifrado en reposo, devuelve un identificador público para referenciarlo, y registra el alta en auditoría.
2. **Given** un documento existente, **When** un usuario sin permiso sobre la entidad asociada intenta descargarlo, **Then** la operación falla con un rechazo no revelador y el intento queda registrado.
3. **Given** un documento adjunto, **When** se "elimina" desde la aplicación, **Then** queda como soft-deleted pero el archivo cifrado permanece para auditoría hasta que un proceso autorizado de purga lo retire según política de retención.
4. **Given** un usuario sube un archivo de tipo o tamaño no permitido, **When** la aplicación recibe la petición, **Then** la rechaza con mensaje accionable en español y no almacena nada.

---

### User Story 6 - Notificaciones por correo y en aplicación (Priority: P3)

El sistema puede notificar a los usuarios eventos relevantes — bloqueo de cuenta, cambio de contraseña, asignación de un nuevo rol, aprobación pendiente — tanto dentro de la aplicación (centro de notificaciones) como por correo electrónico. El usuario puede ver sus notificaciones, marcarlas como leídas y consultar el historial.

**Why this priority**: capacidad transversal que múltiples módulos necesitarán. Cada módulo posterior podrá emitir notificaciones sin reimplementar el canal.

**Independent Test**: disparar manualmente una notificación de prueba para un usuario, verificar que aparece en su centro de notificaciones dentro de la aplicación al refrescar, y que llega un correo a su dirección registrada con el mismo contenido. Marcar la notificación como leída y comprobar que el cambio persiste.

**Acceptance Scenarios**:

1. **Given** un módulo emite una notificación dirigida a un usuario, **When** el evento es procesado, **Then** la notificación aparece en el centro de notificaciones del usuario y se envía un correo a su dirección registrada en un plazo razonable.
2. **Given** una notificación entregada al usuario, **When** la marca como leída en la aplicación, **Then** su estado persiste entre sesiones y deja de figurar en el contador de no leídas.
3. **Given** el envío de correo falla temporalmente, **When** el sistema reintenta, **Then** la notificación en aplicación permanece visible y el correo se entrega tras los reintentos; si los reintentos se agotan, el fallo queda registrado para diagnóstico.

---

### User Story 7 - Registro de autorización habeas data (Priority: P3)

Cada titular de datos personales (asociado, empleado, proveedor, cliente) tiene asociada una autorización de tratamiento de datos personales con fecha, versión de la política aceptada y constancia de la aceptación. Los auditores pueden consultar el historial de autorizaciones y revocaciones por titular.

**Why this priority**: requisito legal colombiano (Ley 1581 de 2012 y decretos reglamentarios) que se debe satisfacer antes de empezar a tratar datos personales de los titulares en los módulos siguientes. No bloquea las capacidades técnicas básicas, pero condiciona la legalidad del tratamiento desde el primer asociado dado de alta.

**Independent Test**: registrar una versión de la política de tratamiento de datos, asociar un titular, recibir su autorización con fecha y versión aceptada, y comprobar que el sistema retiene la evidencia y permite consultarla. Revocar la autorización del titular y verificar que el evento queda registrado y consultable.

**Acceptance Scenarios**:

1. **Given** una versión publicada de la política de tratamiento de datos personales, **When** un titular acepta esa versión, **Then** queda registrado: titular, versión aceptada, timestamp UTC, evidencia de la aceptación (texto del consentimiento) y, si la captura es electrónica, IP de origen.
2. **Given** un titular con autorización vigente, **When** la revoca, **Then** queda registrada la revocación con timestamp y motivo (si se aportó), y el sistema marca al titular como con tratamiento restringido a partir de ese momento.
3. **Given** un auditor con permiso, **When** consulta el historial de un titular, **Then** ve todas las versiones aceptadas, fechas, IPs cuando aplique, y los eventos de revocación.

---

### Edge Cases

- ¿Qué pasa si un usuario pertenece a varias empresas? — el contexto de empresa se elige en login y queda fijo durante la sesión; cambiar de empresa requiere nuevo login o cambio explícito de contexto que dispara nueva autorización.
- ¿Qué pasa si un administrador se queda sin segundo factor (pérdida del dispositivo)? — primero usa uno de sus códigos de respaldo de un solo uso; si los agotó, escala al reset administrativo con doble aprobación y verificación documental fuera de banda; el flujo deja huella completa en auditoría.
- ¿Qué pasa cuando se borra un rol que está asignado a usuarios activos? — la eliminación del rol queda bloqueada hasta reasignar o quitar a esos usuarios; o el rol se conserva en estado inactivo y los usuarios pierden esos permisos.
- ¿Qué pasa si la auditoría no puede escribir su entrada (almacén caído)? — la operación de negocio falla y se devuelve error al usuario; jamás se completa una escritura sensible sin su correspondiente entrada de auditoría.
- ¿Qué pasa si un adjunto excede el tamaño máximo o tiene tipo MIME no permitido? — la subida se rechaza antes de almacenarse, con mensaje accionable.
- ¿Qué pasa cuando un usuario revoca su autorización habeas data? — su titularidad pasa a tratamiento restringido; los módulos que dependen de su consentimiento bloquean nuevas operaciones sobre sus datos y notifican al área de cumplimiento.
- ¿Qué pasa si una contraseña histórica del mismo usuario se intenta reutilizar? — se rechaza si está dentro de la ventana de no-reuso definida por la política.
- ¿Qué pasa si el reloj del servidor se desincroniza con el reloj del cliente al validar un código MFA basado en tiempo? — la tolerancia configurada permite ±1 ventana; fuera de ello el código se rechaza.
- ¿Qué pasa si un correo de notificación rebota? — el fallo queda registrado, la notificación en aplicación permanece, y el administrador puede ver direcciones problemáticas.
- ¿Qué pasa cuando dos usuarios editan el mismo registro a la vez? — el segundo en guardar recibe un mensaje que identifica al primer editor (usuario y timestamp) y debe recargar para reintentar; el sistema nunca sobreescribe silenciosamente.

## Requirements *(mandatory)*

### Functional Requirements

**Multi-empresa y multi-sede**

- **FR-001**: El sistema MUST permitir registrar múltiples empresas (cooperativas) cada una con su NIT único, razón social, datos de contacto y datos legales.
- **FR-002**: El sistema MUST permitir registrar sucursales por empresa, cada sucursal con identificador, ubicación y estado activo/inactivo.
- **FR-003**: El sistema MUST aislar los datos operativos de una empresa de los de cualquier otra, sin que ninguna consulta legítima pueda cruzar empresas.
- **FR-004**: El sistema MUST resolver la empresa activa de cada petición a partir de la identidad autenticada del usuario, sin depender de valores que el cliente pueda manipular libremente.

**Autenticación y políticas de credencial**

- **FR-005**: El sistema MUST autenticar usuarios mediante usuario + contraseña + segundo factor (MFA basado en aplicación autenticadora con códigos de tiempo, TOTP).
- **FR-006**: El sistema MUST permitir a cada usuario inscribir y rotar su segundo factor con verificación previa de su contraseña vigente.
- **FR-007**: El sistema MUST almacenar las contraseñas usando un algoritmo de hashing resistente a fuerza bruta (BCrypt con coste mínimo 11, conforme al estándar técnico vigente del proyecto).
- **FR-008**: El sistema MUST aplicar una política de complejidad mínima de contraseña configurable por empresa (largo mínimo, mezcla de tipos de caracteres) y rechazar contraseñas que no la cumplan.
- **FR-009**: El sistema MUST exigir cambio de contraseña tras vencimiento del plazo configurado (por defecto 90 días, configurable por empresa entre 30 y 180 días).
- **FR-010**: El sistema MUST impedir reutilizar las últimas N contraseñas del mismo usuario (N configurable por empresa, mínimo 5).
- **FR-011**: El sistema MUST bloquear temporalmente la cuenta tras un número configurable de intentos fallidos consecutivos de autenticación o de MFA, y notificar al usuario del bloqueo por correo.
- **FR-012**: El sistema MUST emitir tokens de sesión con vigencia limitada y mecanismo de renovación: el access token vence a los 30 minutos de emitido, el refresh token a las 12 horas, y la sesión se cierra automáticamente tras 30 minutos consecutivos de inactividad del usuario. Los tokens MUST identificar inequívocamente al usuario, la empresa y la sucursal activa (si aplica).
- **FR-013**: El sistema MUST ofrecer un flujo de recuperación de cuenta para el caso de pérdida del segundo factor con dos vías escalonadas: (a) **Códigos de respaldo de un solo uso** — al inscribir MFA, el sistema MUST generar y entregar al usuario un conjunto de al menos 8 códigos de respaldo; cada código permite UN ÚNICO inicio de sesión sin TOTP, queda invalidado tras su uso, y los códigos restantes pueden regenerarse desde el perfil del usuario invalidando los anteriores. (b) **Reset administrativo con doble aprobación** — cuando los códigos se agotan o se pierden, el flujo exige solicitud del usuario, aprobación independiente de dos administradores de la empresa y constancia documental fuera de banda (cédula del titular, evidencia de identidad). El sistema MUST registrar en auditoría: la solicitud, la identidad de los dos aprobadores, la evidencia adjunta, el momento de habilitación del nuevo MFA y notificar al usuario por correo el cambio efectuado.

**Roles y permisos granulares**

- **FR-014**: El sistema MUST modelar permisos como tuplas Entidad × Acción, con las acciones mínimas: Ver, Crear, Editar, Eliminar, Aprobar, Restaurar, Exportar.
- **FR-015**: El sistema MUST permitir definir roles que agrupan permisos, y asignar uno o más roles a cada usuario dentro del contexto de una empresa.
- **FR-016**: El sistema MUST resolver los permisos efectivos de un usuario como la unión de los permisos de todos sus roles activos en la empresa actual.
- **FR-017**: El sistema MUST denegar el acceso a cualquier endpoint protegido cuando el usuario no posee el permiso requerido, de forma indistinguible de "recurso no existente" para no revelar la existencia del endpoint.
- **FR-018**: El sistema MUST ocultar en los menús del frontend las opciones para las que el usuario no tiene permisos.
- **FR-019**: El sistema MUST aplicar los cambios de permisos en la siguiente petición autenticada del usuario, como máximo tras un refresco de token (es decir, en una ventana no mayor a 30 minutos, equivalente al TTL del access token), sin requerir reinicio de servicios.
- **FR-020**: El sistema MUST proveer roles preconstruidos mínimos: Administrador de Empresa, Auditor, Operador, Consulta — modificables y eliminables salvo que rompan invariantes.

**Audit log inmutable**

- **FR-021**: El sistema MUST registrar automáticamente toda operación de creación, modificación o eliminación realizada por cualquier módulo, con: timestamp UTC, identidad del usuario, identidad de la empresa, IP de origen, agente del cliente, tipo de operación, tipo de entidad, identificador público de la entidad y los valores antes/después de los campos modificados.
- **FR-022**: El sistema MUST rechazar cualquier modificación o eliminación de entradas existentes del audit log, tanto desde flujos de aplicación como desde flujos administrativos ordinarios.
- **FR-023**: El sistema MUST retener las entradas del audit log al menos durante 5 años contados desde su creación, conforme al estándar regulatorio SARLAFT.
- **FR-024**: El sistema MUST proveer un panel de consulta del audit log con filtros por rango de fechas, usuario, empresa, entidad, tipo de acción e identificador de la entidad afectada, paginación y exportación. La exportación MUST ofrecer dos formatos: (a) CSV con todos los campos de la consulta para análisis interno y (b) PDF firmado con hash de integridad y página de portada (datos de la empresa, rango exportado, usuario solicitante, total de registros y huella criptográfica del archivo) destinado a entrega regulatoria.
- **FR-025**: El sistema MUST aislar las consultas del audit log por empresa (un auditor de la empresa A no ve registros de la empresa B).
- **FR-026**: El sistema MUST garantizar que si la escritura del registro de auditoría falla, la operación de negocio asociada también falle — nunca se persiste un cambio sensible sin su huella.

**Soft-delete universal**

- **FR-027**: El sistema MUST modelar todo registro persistido con marcadores de creación (autor, fecha UTC), última actualización (autor, fecha UTC) y eliminación lógica (autor, fecha UTC).
- **FR-028**: El sistema MUST excluir por defecto los registros marcados como eliminados de listados, búsquedas y reportes operativos.
- **FR-029**: El sistema MUST permitir, a usuarios con permiso de restauración, recuperar un registro eliminado y registrar en auditoría la restauración.
- **FR-030**: El sistema MUST prohibir el borrado físico de registros desde flujos de usuario o de aplicación.
- **FR-031**: El sistema MUST rechazar la eliminación lógica de movimientos contables asentados (asientos, transacciones de préstamo, transacciones de nómina): la corrección se hace con movimiento reverso referenciando el original.

**Adjuntos cifrados**

- **FR-032**: El sistema MUST permitir adjuntar documentos a entidades de cualquier módulo con tipo MIME y tamaño dentro de límites configurables.
- **FR-033**: El sistema MUST almacenar los documentos cifrados en reposo, de forma que su contenido no sea legible inspeccionando directamente el almacén.
- **FR-034**: El sistema MUST autorizar la descarga de un documento únicamente a usuarios con permiso de visualización sobre la entidad a la que está vinculado.
- **FR-035**: El sistema MUST registrar en auditoría las acciones de alta, descarga y eliminación lógica de adjuntos.
- **FR-036**: El sistema MUST rechazar archivos cuyo tipo MIME o tamaño exceda los límites configurados, con mensaje accionable en español.

**Notificaciones**

- **FR-037**: El sistema MUST proveer un servicio interno de notificaciones que entregue mensajes por dos canales: centro de notificaciones dentro de la aplicación y correo electrónico.
- **FR-038**: El sistema MUST permitir a los usuarios consultar, marcar como leídas y archivar sus notificaciones; el estado debe persistir entre sesiones.
- **FR-039**: El sistema MUST reintentar los envíos de correo fallidos con una política de reintento configurable, y registrar los fallos permanentes para diagnóstico.
- **FR-040**: El sistema MUST generar notificaciones automáticamente para eventos de seguridad mínimos: bloqueo de cuenta, cambio de contraseña, cambio de segundo factor, asignación o retiro de rol.

**Habeas data**

- **FR-041**: El sistema MUST registrar para cada titular de datos personales la versión vigente de la política de tratamiento aceptada, la fecha y la evidencia de la aceptación.
- **FR-042**: El sistema MUST mantener un historial de versiones publicadas de la política de tratamiento, con fecha de publicación, vigencia y texto.
- **FR-043**: El sistema MUST permitir registrar revocaciones de la autorización con fecha, motivo opcional y propagación inmediata del estado restringido del titular hacia el resto de los módulos.
- **FR-044**: El sistema MUST proveer a auditores y al área de cumplimiento la consulta del historial de aceptaciones y revocaciones por titular.

**Operación, observabilidad y rendimiento**

- **FR-045**: El sistema MUST soportar de forma sostenida al menos 100 usuarios concurrentes realizando operaciones típicas de la fase (login, consulta de auditoría, administración de usuarios y roles, descarga de adjuntos) sin degradación perceptible para el usuario.
- **FR-046**: El sistema MUST exponer un panel de administración para gestión de empresas, sucursales, usuarios, roles y permisos.
- **FR-047**: El sistema MUST exponer un panel de consulta del audit log con los filtros descritos en FR-024.
- **FR-048**: El sistema MUST presentar todos los mensajes de error al usuario en español, accionables y con código de error con espacio de nombres (formato `Modulo.CondicionEspecifica`).
- **FR-049**: El sistema MUST aplicar concurrencia optimista a toda escritura sobre entidades editables: cada registro persiste un sello de versión y, ante dos modificaciones que parten del mismo sello, la segunda escritura MUST ser rechazada con mensaje accionable que indique quién modificó el registro, cuándo y la instrucción de recargar antes de reintentar. Bajo ninguna circunstancia una escritura sobreescribe silenciosamente cambios de otro usuario.

### Key Entities *(include if feature involves data)*

- **Empresa (Cooperativa)**: representa una entidad legal cliente del SaaS. Atributos clave: NIT único, razón social, datos legales, estado activo. Es la unidad de aislamiento de datos.
- **Sucursal**: unidad operativa dentro de una empresa. Atributos clave: nombre, ubicación, estado. Pertenece a una sola empresa.
- **Usuario**: identidad que puede autenticarse. Atributos clave: nombre de usuario, correo, hash de contraseña, segundo factor inscrito, fecha de último cambio de contraseña, intentos fallidos recientes, estado (activo, bloqueado, deshabilitado), pertenencias a empresas y sucursales, fecha de última sesión.
- **Rol**: agrupación nombrada de permisos dentro de una empresa. Puede ser preconstruido o personalizado. Atributos clave: nombre, empresa, descripción, conjunto de permisos.
- **Permiso**: tupla Entidad × Acción que autoriza una operación específica. Acciones mínimas: Ver, Crear, Editar, Eliminar, Aprobar, Restaurar, Exportar.
- **Asignación de Rol a Usuario**: vínculo que asocia un usuario, un rol y una empresa, con fecha de inicio y, opcionalmente, fecha de fin.
- **Entrada de Audit Log**: registro inmutable de una acción. Atributos clave: timestamp UTC, identidad del usuario, identidad de la empresa, IP, agente del cliente, tipo de operación, tipo y identificador público de la entidad afectada, payload antes, payload después.
- **Adjunto**: documento asociado a una entidad. Atributos clave: identificador público, nombre original, tipo MIME, tamaño, hash, fecha de alta, autor, referencia a la entidad propietaria, marca cifrada de ubicación en almacén, estado (vigente, eliminado lógicamente).
- **Notificación**: mensaje dirigido a un usuario. Atributos clave: destinatario, tipo, asunto, cuerpo, canales de entrega, estado (no leída, leída, archivada), timestamps de entrega y lectura.
- **Política de Tratamiento de Datos**: versión publicada del documento de habeas data. Atributos clave: número de versión, fecha de publicación, vigencia, texto completo.
- **Autorización Habeas Data**: aceptación o revocación por titular. Atributos clave: titular, versión de política aceptada, fecha UTC, evidencia, IP cuando aplique, estado (aceptada, revocada), motivo si revocada.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona autorizada puede completar su autenticación con MFA en menos de 30 segundos en condiciones normales (95% de los intentos exitosos).
- **SC-002**: El sistema sostiene 100 usuarios concurrentes realizando operaciones típicas de la fase con tiempos de respuesta percibidos por el usuario por debajo de 2 segundos en el 95% de las peticiones.
- **SC-003**: El 100% de las operaciones de creación, modificación o eliminación realizadas en cualquier módulo posterior queda reflejado en el audit log dentro de la misma transacción que las origina.
- **SC-004**: Un auditor obtiene los resultados de una consulta filtrada del audit log para una ventana de un mes en menos de 5 segundos.
- **SC-005**: Un usuario sin permiso sobre un endpoint protegido recibe respuesta indistinguible de "recurso no existente" en el 100% de los intentos, y el intento queda registrado.
- **SC-006**: Los cambios de permisos surten efecto, como máximo, en la siguiente petición autenticada del usuario tras un refresco de token; el 100% de los usuarios afectados ve el efecto sin reiniciar servicios.
- **SC-007**: La consulta del audit log para una acción realizada hace 4 años y 11 meses devuelve la entrada sin pérdida, en el 100% de los casos.
- **SC-008**: Las contraseñas de todos los usuarios persistidas en el sistema están protegidas con BCrypt de coste mínimo 11 — una verificación automática lo confirma en cada ejecución de la suite de integridad.
- **SC-009**: Inspeccionar directamente el almacén físico de adjuntos no permite recuperar texto plano de los documentos en el 100% de los casos auditados.
- **SC-010**: La cuenta de cualquier usuario queda bloqueada tras el umbral configurado de intentos fallidos consecutivos, con notificación por correo entregada al usuario en menos de 60 segundos en el 99% de los casos.
- **SC-011**: Para cada titular de datos personales registrado tras la puesta en producción de la fase, existe un registro habeas data con versión de política aceptada, fecha y evidencia, en el 100% de los casos.
- **SC-012**: Un administrador de empresa completa el alta de un nuevo usuario (datos, asignación de rol, envío de invitación) en menos de 2 minutos.
- **SC-013**: El servicio mantiene una disponibilidad mensual de al menos 99.5% medida sobre la ventana hábil de operación, equivalente a un máximo de aproximadamente 3 horas 39 minutos de indisponibilidad por mes; las ventanas de mantenimiento programado se anuncian con al menos 48 horas de antelación, se ejecutan fuera del horario laboral y no se contabilizan contra el SLO.

## Assumptions

- **MFA por TOTP**: el segundo factor es un código basado en tiempo (TOTP) generado por una aplicación autenticadora compatible (Google Authenticator, Authy, Microsoft Authenticator). No se incluyen en esta fase canales adicionales (SMS, llamada). Se eligió TOTP por ser el factor compatible con dispositivos que no requieren red móvil ni costos por mensaje y por ser el estándar de la industria para sistemas internos.
- **Hashing de contraseña con BCrypt**: se elige BCrypt con coste mínimo 11 por alineación con la constitución técnica del proyecto y por su disponibilidad madura en el ecosistema .NET. El requisito original mencionaba "argon2/bcrypt" y se selecciona BCrypt.
- **Política de contraseña por defecto**: 12 caracteres mínimo, al menos un número, una mayúscula y un símbolo; vencimiento 90 días; no reutilizar las últimas 5; bloqueo tras 5 intentos fallidos por 15 minutos. Los valores son configurables por empresa.
- **Retención de auditoría**: 5 años en activo, conforme al mínimo SARLAFT. La política de purga posterior a 5 años se trata en una fase futura.
- **Token de sesión**: la sesión persiste mientras el token sea válido; el cambio de empresa requiere autenticación contextual nueva. El detalle del mecanismo de transporte es decisión técnica del plan.
- **Multi-tenancy con aislamiento**: cada empresa opera sobre su propio espacio de datos lógico, conforme al principio constitucional de schema-per-tenant. La administración de empresas y NITs es función global del operador del SaaS y no de los usuarios de cada cooperativa.
- **Cifrado de adjuntos**: cifrado simétrico AES-256 en reposo es el estándar mínimo asumido; la rotación de claves se trata como tarea operativa fuera del alcance funcional de esta fase.
- **Canales de notificación en esta fase**: solo correo electrónico (SMTP) y centro de notificaciones in-app. SMS, push móvil y mensajería instantánea quedan fuera del alcance.
- **Habeas data — Ley 1581 de 2012**: la política de tratamiento se redacta y publica externamente; el sistema almacena, versiona y referencia el texto. La definición del texto legal no es responsabilidad técnica de esta fase.
- **Idioma de operación**: español para toda interacción con el usuario final (mensajes, mensajes de error, etiquetas), conforme a la constitución del proyecto.
- **Periodo de prueba de carga**: la verificación de 100 usuarios concurrentes asume un perfil mixto de operaciones (60% lectura, 30% escritura, 10% subida/descarga de adjuntos) sobre datos sintéticos representativos.
- **Single Sign-On externo**: no contemplado en esta fase. La identidad se administra dentro del sistema.
- **Recuperación ante pérdida de MFA**: cubierta por la combinación de (a) códigos de respaldo de un solo uso generados al inscribir MFA y (b) reset administrativo con doble aprobación de dos administradores de empresa y verificación documental fuera de banda (detallado en FR-013).
- **Auditoría persiste a nivel de campo modificado, no de objeto completo**: el "antes/después" referenciado en el audit log corresponde a los campos efectivamente modificados, no a un dump completo de la entidad, para mantener volumen y legibilidad razonables.
- **Roles preconstruidos**: se entregan al menos Administrador de Empresa, Auditor, Operador y Consulta; los administradores pueden crear roles personalizados adicionales pero no eliminar los preconstruidos esenciales (al menos Administrador de Empresa y Auditor).
