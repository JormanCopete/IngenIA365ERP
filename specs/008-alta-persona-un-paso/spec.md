# Feature Specification: Alta de persona en un paso desde los módulos

**Feature Branch**: `008-alta-persona-un-paso`
**Created**: 2026-09-13
**Status**: Aprobada por el dueño (decisiones del 2026-09-13)
**Input**: Solicitud del usuario: `/maestros/personas` es la opción centralizada que controla los
datos básicos de personas y empresas; su pestaña «Roles» determina en qué módulos aparece cada
persona. Registrar un empleado obliga a ir antes a Personas, crear la persona y volver (pasos
dobles). Se pide un alta en un paso, evaluando (3.1) abrir Personas como ventana emergente con el
rol premarcado y encadenar al diálogo de empleado, o (3.2) guardar persona y empleado de una vez
desde la misma ventana si el usuario tiene permiso. Debe servir también para `/asociados/registro`
y otras opciones, cuidando integridad de datos, seguridad, permisos, y que el formulario de persona
evolucione en un solo sitio.

## Contexto

`COR_People` es la fuente única de datos personales (constitución, Principio V). Los roles son
banderas en la persona; tres de ellas (Empleado, Asociado, Vendedor) reflejan una fila en una tabla
hija (`PAY_Employees`, `COR_Associates`, `INV_Salespeople`) y las otras cinco (Cliente, Proveedor,
Asesor, Tercero contable, Recibe factura) son sólo una marca.

Al revisar el código se encontraron tres problemas además del doble paso:

1. **Integridad**: las pantallas de Empleados y Asociados actualizan la persona enviando sólo su
   propia bandera, y la actualización sobrescribe las ocho: registrar como empleado a una persona
   asociada le quita «Asociado» (y viceversa).
2. **Tres copias del formulario de persona**: el diálogo está escrito dentro de Personas y
   copiado a mano, con campos y catálogos distintos, en Empleados y Asociados.
3. **Sin permisos efectivos**: consultar y modificar personas, empleados y asociados sólo exige
   estar autenticado; los permisos no llegan al cliente y el componente que debía ocultar
   botones no funciona.

## Clarifications

### Session 2026-09-13

- Q: ¿Un diálogo con un solo Guardar (3.2) o dos diálogos encadenados (3.1)? → A: **3.2** para roles
  con tabla hija (empleado, asociado, vendedor): un diálogo, un «Registrar», persona y rol creados
  de forma atómica; sin permiso de crear personas sólo se elige una existente. **3.1** (diálogo de
  Personas con la casilla premarcada) para los roles que son sólo una marca (fase 2).
- Q: Con persona existente, ¿se editan sus datos dentro del diálogo del módulo? → A: **No**: van de
  sólo lectura y un botón «Editar datos de la persona» abre el diálogo compartido de Personas (si
  tiene permiso). Un solo sitio escribe la persona.
- Q: ¿Qué puede hacer el rol Operador? → A: **Crear y editar** personas, empleados y asociados; **no**
  terminar contratos ni eliminar personas (quedan para el administrador de la cooperativa).
- Q: ¿Qué pasa con los roles existentes al exigir permisos? → A: **Conservan la lectura** de
  Personas, Empleados y Asociados; la escritura la asigna el administrador.
- Q: ¿Qué pasa cuando el documento pertenece a una persona **eliminada** (eliminación lógica; el
  índice único del documento no distingue eliminadas)? → A: **Rechazar con aviso claro** («ese
  documento pertenece a una persona eliminada el {fecha}») **y ofrecer «Restaurar persona»** a
  quien tenga el permiso de eliminar personas: se reactiva la **misma** fila (historial intacto,
  auditado) y el alta del rol continúa. Hoy pasa la validación y rompe en la base con un error
  genérico.
- Q: ¿Cómo se trata el **reingreso** de una persona con contrato terminado? → A: **Ficha nueva**:
  la retirada queda como historial («Retirado») con sus liquidaciones intactas; «Empleado» significa
  «existe una ficha viva»; la consulta de empleado por persona y el modo edición miran **sólo la
  ficha viva**; la «fecha de reingreso» heredada de SOLIDO no se usa. Hoy el registro ya crea la
  segunda ficha, pero la consulta por persona devolvía cualquiera de las dos.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar un empleado nuevo en un solo paso (Priority: P1)

Quien lleva la nómina abre «Nuevo empleado», busca a la persona por documento, no existe, y desde
ahí mismo digita sus datos personales y laborales y pulsa **un** «Registrar». La persona queda
creada con el rol Empleado y el empleado queda registrado, o no queda nada.

**Why this priority**: es el caso que motivó la solicitud; hoy son dos pantallas y una pestaña
nueva del navegador.

**Independent Test**: con un usuario con permiso de crear personas y empleados, registrar un
empleado con documento inexistente en un solo diálogo; verificar en Personas que aparece con el
distintivo «Empleado» y en Nómina como empleado activo.

**Acceptance Scenarios**:

1. **Given** un usuario con permiso de crear personas y empleados, **When** busca un documento
   que no existe y elige «Crear persona nueva con ese documento», **Then** el diálogo de empleado
   se abre con las pestañas de persona editables (documento prellenado) y las pestañas laborales,
   y un único botón «Registrar».
2. **Given** ese diálogo completo, **When** pulsa «Registrar», **Then** persona y empleado se
   crean juntos; si falla cualquiera de los dos, no queda ninguno y el aviso dice por qué.
3. **Given** que el documento ya existe, **When** pulsa «Registrar», **Then** ve «Ya existe
   {nombre} con ese documento» y un botón «Usar esa persona» que reabre el diálogo con la
   persona existente.
4. **Given** un usuario **sin** permiso de crear personas, **When** busca un documento inexistente,
   **Then** no se le ofrece crear; el texto le indica pedir el registro a quien administra
   Personas, y sólo puede continuar con una persona existente.
5. **Given** que el documento pertenece a una persona **eliminada**, **When** pulsa «Registrar»,
   **Then** ve «Ese documento pertenece a una persona eliminada el {fecha}: {nombre}» y, sólo si
   tiene permiso de eliminar personas, el botón «Restaurar persona»; al restaurar, la misma
   persona vuelve a estar vigente y el diálogo continúa con ella como persona existente. Sin ese
   permiso, el aviso le indica pedir la restauración a quien administra Personas.

---

### User Story 2 - Registrar un asociado nuevo en un solo paso (Priority: P1)

Mismo flujo para «Nuevo asociado» en `/asociados/registro`: persona + afiliación en un solo
«Registrar».

**Independent Test**: registrar un asociado con documento inexistente en un solo diálogo y
verlo en Personas con el distintivo «Asociado».

**Acceptance Scenarios**:

1. **Given** permiso de crear personas y asociados, **When** registra un asociado con persona
   nueva, **Then** ambos quedan creados juntos o ninguno.
2. **Given** una persona ya asociada, **When** intenta registrarla otra vez, **Then** el sistema
   la abre en modo edición del asociado existente.

---

### User Story 3 - Las banderas de rol no se pisan entre módulos (Priority: P1)

Una persona que es asociada y empleada conserva ambos distintivos aunque se edite desde
cualquier pantalla; los distintivos derivados los pone y quita el módulo que crea o retira el
rol, nunca un formulario.

**Independent Test**: registrar como empleada a una persona asociada, luego editar su correo en
Personas; ambos distintivos siguen; terminar el contrato quita sólo «Empleado».

**Acceptance Scenarios**:

1. **Given** una persona asociada, **When** se la registra como empleada, **Then** sigue siendo
   asociada.
2. **Given** una persona asociada y empleada, **When** alguien edita su correo en Personas,
   **Then** conserva ambos roles.
3. **Given** un empleado activo, **When** se termina su contrato, **Then** deja de tener el
   distintivo «Empleado» y conserva los demás. **Given** un vendedor, **When** se elimina su
   ficha de vendedor, **Then** deja de tener «Vendedor» (hoy la ficha se elimina y la bandera
   queda encendida).
4. **Given** la pestaña Roles de Personas, **When** se abre una persona empleada, **Then**
   «Empleado» aparece como distintivo no editable con la acción «Registrar como…» para los
   roles que le falten.
5. **Given** bases ya sembradas con banderas desalineadas de sus tablas hijas, **When** se
   despliega esta feature, **Then** las banderas derivadas quedan recalculadas desde las tablas
   hijas.
6. **Given** una persona con contrato terminado, **When** se la registra de nuevo como empleada,
   **Then** se crea una ficha nueva, la retirada sigue visible como historial con sus
   liquidaciones, la persona recupera «Empleado», y al buscarla desde Nómina se abre la ficha
   viva (nunca la retirada).

---

### User Story 4 - Un solo formulario de persona (Priority: P2)

Personas, Empleados y Asociados usan el mismo formulario (mismos campos, mismos catálogos, misma
validación). Un campo nuevo se agrega una vez y aparece en los tres.

**Independent Test**: agregar un campo al formulario compartido y verlo en las tres pantallas
sin tocarlas.

**Acceptance Scenarios**:

1. **Given** las tres pantallas, **When** se compara el formulario de persona, **Then** los
   catálogos (tipo de documento, género, estado civil, nivel educativo, estado) son idénticos.
2. **Given** un módulo con persona existente, **When** pulsa «Editar datos de la persona»,
   **Then** se abre el mismo diálogo de Personas y al guardar el módulo refleja el cambio.

---

### User Story 5 - Permisos efectivos sobre personas, empleados y asociados (Priority: P2)

El servidor exige permiso para consultar, crear, editar, eliminar y dar de baja; la interfaz
oculta lo que el usuario no puede hacer y se recalcula al cambiar de cooperativa.

**Independent Test**: con un usuario de sólo lectura, la API rechaza el alta y la pantalla no
muestra los botones; con Operador, crea y edita pero no ve «Terminar contrato» ni «Eliminar».

**Acceptance Scenarios**:

1. **Given** un usuario sin permiso de crear empleados, **When** intenta registrar uno por la
   API, **Then** recibe la misma respuesta que para una ruta inexistente.
2. **Given** un rol personalizado existente antes de esta feature, **When** se despliega,
   **Then** conserva la consulta de personas, empleados y asociados.
3. **Given** un usuario con dos cooperativas y permisos distintos, **When** cambia de
   cooperativa, **Then** los botones se recalculan sin recargar la página.

---

### Edge Cases

- Documento existente pero la persona está inactiva o retirada: el alta del rol se permite;
  el módulo decide (hoy Nómina registra igual; cartera exige asociado activo).
- Persona con contrato terminado que vuelve: «Nuevo empleado» la trata como persona sin rol
  Empleado (registro de ficha nueva); la ficha retirada sigue en la grilla como «Retirado» y se
  puede consultar, no editar como si fuera la vigente.
- Dos usuarios crean la misma persona a la vez: uno recibe «ya existe» con el nombre y la opción
  de usarla.
- Documento de una persona eliminada (eliminación lógica): nunca se crea una segunda fila; el
  aviso dice que está eliminada y desde cuándo, y ofrece «Restaurar persona» sólo a quien tiene
  permiso de eliminar personas. Restaurar reactiva la misma fila —conserva cartera, asientos y
  corridas que la referencian— y queda en la auditoría; las banderas derivadas se recalculan al
  restaurar (una persona eliminada no puede tener empleado activo, pero sí filas hijas históricas).
- Persona creada y alta del rol fallida por un dato laboral inválido: no queda persona nueva;
  el aviso señala el campo.
- Usuario con permiso de crear personas pero no empleados: no ve «Nuevo empleado»; en Personas
  sí puede crear la persona y verá «Registrar como empleado…» sólo si tiene ese permiso.
- Cambio de cooperativa con el diálogo abierto: cambiar de cooperativa pasa por la pantalla de
  selección y navega fuera de la página, así que el diálogo desaparece con ella; al volver, los
  permisos se recalculan (la caché del cliente se vacía al autenticarse en la nueva cooperativa).
- Búsqueda con término de sólo dígitos (hasta 20) → el documento se prellena; cualquier otro
  término (nombre, documento con guion o letras) → el documento queda vacío.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Desde «Nuevo empleado» y «Nuevo asociado», si la persona no existe y el usuario
  tiene permiso de crear personas, el sistema MUST permitir digitar persona y rol en el mismo
  diálogo y crearlos con una sola acción atómica (los dos o ninguno).
- **FR-002**: Sin permiso de crear personas, el sistema MUST ofrecer sólo la selección de una
  persona existente e indicar a quién pedir el registro; MUST NOT abrir otra pestaña.
- **FR-003**: Las banderas Empleado, Asociado y Vendedor MUST escribirse únicamente por la
  operación que crea o retira el rol; ninguna edición de la persona las cambia. «Empleado»
  significa «existe una ficha de empleado viva (no retirada, no eliminada)».
- **FR-004**: Las banderas Cliente, Proveedor, Asesor, Tercero contable y Recibe factura MUST ser
  editables en Personas.
- **FR-005**: Con persona existente, los módulos MUST mostrar sus datos personales de sólo lectura
  y ofrecer «Editar datos de la persona» al diálogo compartido, sólo con permiso de editar personas.
- **FR-006**: Un solo formulario de persona (campos, catálogos, validación) MUST alimentar
  Personas, Empleados y Asociados; ninguna pantalla MUST declarar su propia copia.
- **FR-007**: El sistema MUST rechazar un documento duplicado indicando el nombre de la persona
  existente y permitir «usar esa persona». Si la persona existente está **eliminada**, el rechazo
  MUST decirlo con la fecha y MUST NOT crear una segunda fila con ese documento.
- **FR-008**: Consultar, crear, editar, eliminar y restaurar personas (restaurar con el permiso de
  eliminar); consultar, crear y editar asociados;
  consultar, crear, editar y terminar empleados MUST exigir permisos propios; sin permiso la
  respuesta MUST ser indistinguible de una ruta inexistente.
- **FR-009**: El rol Operador MUST poder crear y editar personas, empleados y asociados y MUST NOT
  terminar contratos ni eliminar personas; el administrador de la cooperativa MUST poder todo.
- **FR-010**: Todo rol existente antes de esta feature MUST conservar la consulta de personas,
  empleados y asociados.
- **FR-011**: La interfaz MUST conocer los permisos efectivos del usuario en la cooperativa activa
  y ocultar las acciones no permitidas; MUST recalcularlos al cambiar de cooperativa.
- **FR-012**: Las banderas derivadas de las bases ya sembradas MUST reconciliarse con sus tablas
  hijas en el mismo despliegue.
- **FR-013**: La búsqueda de personas MUST devolver todas las banderas y MUST poder filtrar por rol.
- **FR-014**: Todo alta compuesta MUST quedar en la auditoría como una sola operación con sus datos.
- **FR-015**: Toda zona que espere datos MUST mostrar el indicador de carga (regla vigente).
- **FR-016**: Quien tenga permiso de eliminar personas MUST poder **restaurar** una persona
  eliminada desde ese mismo aviso; la restauración reactiva la misma fila, recalcula sus
  banderas derivadas y queda auditada. Sin ese permiso, el aviso MUST indicar a quién pedirla.
- **FR-017**: El reingreso de una persona con contrato terminado MUST crear una ficha nueva; la
  retirada MUST conservarse como historial con sus liquidaciones. La consulta de empleado por
  persona y el modo edición MUST devolver sólo la ficha viva; MUST NOT existir dos fichas vivas
  para la misma persona.

### Key Entities

- **Persona**: datos de identificación, contacto y demografía; banderas de rol simples
  (editables) y derivadas (reflejo de una tabla hija). Su documento es único incluso frente a
  personas eliminadas: una eliminada se restaura, nunca se duplica.
- **Empleado**: datos laborales, seguridad social, plan y banca de nómina; siempre cuelga de una
  persona; su existencia activa enciende «Empleado». Una persona puede tener varias fichas a lo
  largo del tiempo (una por vínculo laboral) pero a lo sumo **una viva**; las retiradas son
  historial inmutable. La «fecha de reingreso» heredada no se usa.
- **Asociado**: afiliación, empleo externo, banca de depósito, cónyuge; cuelga de una persona;
  enciende «Asociado».
- **Permiso**: código `Recurso.Acción` en el catálogo de la cooperativa, asignado a roles;
  resuelto por petición en el servidor y expuesto al cliente para la cooperativa activa.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Registrar un empleado con persona nueva toma **una** pantalla y **un** guardado
  (hoy: dos pantallas, dos guardados y una pestaña nueva).
- **SC-002**: Cero pérdidas de banderas: una persona con dos roles los conserva tras cualquier
  edición desde cualquier pantalla (prueba automatizada de regresión).
- **SC-003**: El formulario de persona existe **una** vez en el código (prueba de arquitectura).
- **SC-004**: El 100 % de las rutas de personas, empleados y asociados exige permiso (prueba de
  arquitectura); un usuario de sólo lectura no puede crear por la API.
- **SC-005**: Ningún rol existente pierde la consulta el día del despliegue.

## Assumptions

- Los roles que son sólo una marca (Cliente, Proveedor, Asesor, Tercero, Recibe factura) se
  siguen creando desde Personas; su alta desde otros módulos con la casilla premarcada es la
  fase 2 y reutiliza el diálogo compartido.
- La unicidad de persona sigue siendo por número de documento (el NIT de una persona natural es
  su cédula).
- Los ocho buscadores ad-hoc de personas de otros módulos (Cartera, Contabilidad, Tesorería,
  Tarjetas) se migran al buscador compartido en la fase 2; quedan en una lista de excepciones de
  la prueba de arquitectura.
- Vendedor (Inventario) sigue el mismo modelo que empleado/asociado, pero su pantalla no cambia
  en esta feature.
- Un asociado retirado (con fecha de retiro) conserva el distintivo «Asociado» mientras exista
  su afiliación; sólo eliminar la afiliación lo apaga. El retiro de asociados no cambia en esta
  feature; si más adelante el retiro debe apagar la bandera, lo hará el comando de retiro
  (misma regla: la escribe quien crea o retira la fila hija).
