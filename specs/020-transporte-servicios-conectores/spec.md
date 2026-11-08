# Feature Specification: Transporte, servicios por vehículo y conectores externos

**Feature Branch**: `020-transporte-servicios-conectores`

**Created**: 2026-11-08

**Status**: Borrador con las decisiones del dueño del 2026-11-08 (A–L y Q1); siguiente paso `/speckit-plan`

**Input**: pedido del dueño en la sesión «Activación de master»: cuando se paguen ciertos conceptos
(servicios facturados o fijos del asociado), el ERP debe comunicarse con una API externa para
activar un servicio de la cooperativa (radio, app o ambos en una cooperativa de taxis). Debe ser
configurable, habilitado por el maestro para cada empresa y configurado por el administrador de la
empresa en su servicio. Antes de eso, un módulo pequeño de Transporte: vehículos con su propietario
(asociado), activables, con cambio de propietario, conductores, y carga y actualización masiva.

**Material de referencia** (fuera del repositorio, porque trae datos reales de una cooperativa): la
documentación de la API de créditos de un proveedor de radio-taxi y la ficha de vehículo de una
aplicación anterior. Aquí sólo se usan ejemplos genéricos.

## Contexto

Una cooperativa de transporte de taxis vincula vehículos de sus asociados (y de terceros) a su
parque automotor. La norma del sector (Decreto 1079 de 2015) exige que la empresa lleve, por
vehículo: la vinculación (contrato con el propietario y tarjeta de operación), los documentos con
vencimiento (SOAT, revisión técnico-mecánica, pólizas de responsabilidad civil contractual y
extracontractual) y los conductores. Hoy el ERP no conoce el vehículo: ni la placa, ni el número
interno, ni quién es su dueño.

Encima de eso, la cooperativa vende **servicios por vehículo** —radio, app de pedidos, otros— que
presta un proveedor externo. Cada mes factura esos servicios; cuando el propietario los paga, el
proveedor debe enterarse para activar el servicio de ese vehículo. Hoy eso se hace a mano. El
proveedor ofrece una API: iniciar sesión, obtener un token y «asignar crédito» a un vehículo
identificado por su número de móvil, con un identificador propio que se devuelve para conciliar.

Dos reglas ordenan la feature:

1. **Nada queda acoplado al código.** Otra cooperativa puede no usar Transporte, o usar servicios
   y proveedores distintos. El módulo se **habilita por empresa** y los conectores son **datos**
   que define el maestro, no programas por proveedor.
2. **Cada pieza tiene un dueño.** El maestro habilita el módulo, define los conectores y decide qué
   empresa puede usar cuál. El administrador de la empresa pone sus credenciales y decide qué
   servicio va a qué conector. El cajero no hace nada distinto: cobra como siempre, y la
   notificación sale sola.

## Clarifications

### Session 2026-11-08 (formularios del dueño)

- Q: ¿Quién decide si una cooperativa tiene Transporte? → A: el maestro lo habilita por empresa; sin
  habilitar no se ve el menú ni responden las rutas.
- Q: ¿Quién puede ser propietario? → A: cualquier persona; si es asociado vigente la vinculación es
  «asociado», si no «externo», y se deduce.
- Q: ¿Cuánto de conductores? → A: sólo registro básico (persona como conductor y vehículos
  asignados); licencias y tarjeta de control quedan fuera.
- Q: ¿Estados y cambio de propietario? → A: Activo / Inactivo / Retirado con fecha, motivo e
  historia; cambio de propietario con fecha efectiva y motivo; las deudas del saliente se informan,
  no bloquean.
- Q: ¿Qué datos? → A: ficha técnica, vinculación, documentos con vencimiento y equipos.
- Q: ¿Tipos de documento? → A: catálogo por empresa con semilla de los legales.
- Q: ¿Vencimientos? → A: se informan (tablero e informe), no bloquean.
- Q: ¿Número interno y código radio-móvil? → A: distintos; el radio-móvil es el que identifica al
  vehículo ante el proveedor.
- Q: ¿Rodamiento? → A: se guardan los mínimos diario y mensual como datos, sin control.
- Q: ¿Plantillas? → A: vehículos, documentos, conductores y cambios de estado o propietario.
- Q: ¿Catálogos? → A: por empresa, con semilla.
- Q: ¿Una o dos features? → A: una sola.
- Q: ¿Qué pagos disparan la notificación? → A: recibo de caja, banco/convenio/pasarela y
  nómina/pagaduría (no al facturar).
- Q: ¿Qué son los conceptos fijos? → A: servicios que se facturan cada mes, en lote o uno a uno,
  según los vehículos activos y sus propietarios; la factura queda en Cartera para pagarse, o se
  paga en el acto.
- Q: ¿Radio y app? → A: servicios independientes; cada vehículo elige los suyos y cada uno se
  notifica cuando se paga.
- Q: ¿Factura? → A: una factura por vehículo.
- Q: ¿Precio? → A: el del producto de Comercial, con ajuste por vehículo.
- Q: ¿Pago parcial? → A: sólo se notifican las líneas pagadas del todo.
- Q: ¿Anulación tras notificar? → A: la anulación procede; se marca y se avisa.
- Q: ¿Momento? → A: en segundo plano, con reintentos y tablero.
- Q: ¿Qué tan genérico? → A: conector HTTP/JSON configurable en pantalla.
- Q: ¿Credenciales? → A: las pone el administrador de la empresa.
- Q: ¿Fecha? → A: todo antes del 01/12/2026, construido por fases.
- Q1: Factura de un vehículo con radio y app y un pago que cubre sólo una línea: ¿cuál se salda?
  → A: en la caja el cajero elige el servicio; por los canales sin elección se salda por la
  prioridad que la empresa da a cada servicio (FR-056).

## User Scenarios & Testing *(mandatory)*

Las historias van en el orden de construcción. Cada una deja algo usable por sí misma.

### User Story 1 - El maestro habilita Transporte para una empresa (Priority: P1)

El maestro entra a su consola, abre una empresa y enciende el módulo Transporte. Desde ese momento
los usuarios de esa empresa con los permisos de Transporte ven el menú; en las demás empresas
Transporte no existe para nadie.

**Why this priority**: es la puerta de todo lo demás y nace aquí: hoy el ERP no tiene módulos que se
enciendan por empresa.

**Independent Test**: con dos empresas, habilitar Transporte en una y comprobar que en la otra el
menú no aparece y las rutas de Transporte responden «módulo no habilitado», incluso para un
administrador con todos los permisos.

**Acceptance Scenarios**:

1. **Given** una empresa sin Transporte, **When** el maestro lo habilita con un motivo, **Then** sus
   usuarios con permiso ven el menú y el cambio queda auditado con el maestro, la fecha y el motivo.
2. **Given** una empresa con Transporte y vehículos, **When** el maestro lo deshabilita, **Then** el
   menú desaparece, las rutas responden «módulo no habilitado», los datos se conservan intactos y
   vuelven a verse al habilitarlo otra vez.
3. **Given** un usuario de una empresa (aunque sea su administrador), **When** intenta habilitar un
   módulo, **Then** se le niega: sólo el maestro habilita módulos.

---

### User Story 2 - Registrar un vehículo con su propietario y llevar su vida (Priority: P1)

Un operador registra un taxi: placa, número interno, código radio-móvil, ficha técnica, vinculación,
equipos y propietario (busca la persona por documento; si no existe, la crea desde el mismo
diálogo de persona que usa el resto del ERP). El sistema muestra si la vinculación es «asociado» o
«externo». Luego el vehículo se inactiva por una suspensión, se reactiva, cambia de propietario por
una venta y, años después, se retira de la empresa. Todo queda en su historia.

**Why this priority**: es el dato del que cuelga todo: documentos, conductores, servicios,
facturación y la notificación al proveedor.

**Independent Test**: crear un vehículo, cambiar su estado dos veces y su propietario una, y ver en
la ficha la historia completa con fechas, motivos y usuarios; buscarlo por placa, número interno,
radio-móvil y documento del propietario.

**Acceptance Scenarios**:

1. **Given** una persona que es asociado vigente, **When** se registra como propietaria de un
   vehículo nuevo, **Then** el vehículo queda Activo con vinculación «asociado», y la ficha lo
   muestra sin que nadie la haya digitado.
2. **Given** una placa que ya tiene otro vehículo vivo, **When** se intenta registrar otra vez,
   **Then** se rechaza con un mensaje que nombra el vehículo existente. Lo mismo con el número
   interno y con el código radio-móvil.
3. **Given** un vehículo Activo, **When** se inactiva con fecha y motivo, **Then** queda Inactivo,
   la historia guarda el cambio y deja de entrar en la facturación mensual mientras siga así.
4. **Given** un vehículo cuyo propietario tiene saldos vencidos en Cartera, **When** se cambia de
   propietario con fecha efectiva y motivo, **Then** el cambio procede, la pantalla muestra las
   deudas del saliente antes de confirmar, y la historia guarda propietario anterior, nuevo, fecha y
   motivo.
5. **Given** un vehículo, **When** se retira (desvinculación) con fecha y motivo, **Then** queda
   Retirado, su placa, número interno y radio-móvil quedan libres para un registro nuevo, y el
   retirado se sigue consultando con toda su historia.

---

### User Story 3 - Cargar y actualizar el parque automotor en lote (Priority: P1)

Al implantar, la cooperativa trae cientos de vehículos de su sistema anterior. Descarga la
plantilla, la llena, la revisa sin guardar, corrige lo que el sistema le marca fila por fila y la
aplica. Meses después usa otra plantilla para cargar las renovaciones de SOAT de todo el parque, y
otra para retirar o cambiar de propietario a varios vehículos a la vez.

**Why this priority**: sin carga masiva no hay salida en vivo; nadie digita cientos de fichas.

**Independent Test**: cargar una plantilla de vehículos con filas válidas e inválidas en modo
revisión, ver el error de cada fila con su columna, corregir, aplicar, y comprobar que una segunda
carga con «actualizar» cambia sólo las celdas con valor.

**Acceptance Scenarios**:

1. **Given** una plantilla de vehículos con una fila cuyo propietario no existe, **When** se revisa,
   **Then** nada se guarda y el resultado dice la fila, la columna y el motivo.
2. **Given** una plantilla válida, **When** se aplica, **Then** se crean o actualizan todos los
   vehículos o ninguno, y el resultado dice por fila si creó, actualizó o dejó sin cambio.
3. **Given** una plantilla de documentos con la renovación del SOAT de 300 vehículos, **When** se
   aplica, **Then** cada vehículo queda con su documento nuevo vigente y el anterior en la historia.
4. **Given** una plantilla de cambios con retiros y cambios de propietario, **When** se aplica,
   **Then** cada cambio queda como si se hubiera hecho uno a uno, con su fecha y motivo.

---

### User Story 4 - Documentos del vehículo y tablero de vencimientos (Priority: P2)

El área de transporte registra el SOAT, la técnico-mecánica, la tarjeta de operación y las pólizas
de cada vehículo, y cada mañana revisa el tablero de lo vencido y lo que vence en los próximos días,
por vehículo y propietario, para llamar a tiempo.

**Why this priority**: es la obligación legal más visible del sector y hoy se lleva en hojas de
cálculo; no bloquea la salida si se hace después de los vehículos.

**Independent Test**: registrar documentos con vencimientos pasados, próximos y lejanos, y comprobar
que el tablero y su exportación los clasifican según los días de aviso de cada tipo.

**Acceptance Scenarios**:

1. **Given** el catálogo sembrado, **When** la empresa marca la revisión preventiva como no
   obligatoria y cambia los días de aviso del SOAT a 30, **Then** el tablero usa esos valores para
   esa empresa y no para otras.
2. **Given** un vehículo con el SOAT vencido, **When** se consulta, **Then** la ficha muestra el
   aviso, el vehículo sigue Activo y la facturación lo sigue incluyendo (se informa, no se bloquea).
3. **Given** un vehículo sin un documento obligatorio, **When** se abre el tablero, **Then** aparece
   como «falta» junto a los vencidos.

---

### User Story 5 - Conductores básicos (Priority: P2)

Se registra a una persona como conductor y se le asignan uno o varios vehículos con fecha de inicio
y fin. Desde la ficha del vehículo se ve quién lo maneja hoy y quién lo manejó.

**Why this priority**: el dueño lo pidió en esta entrega, pero sólo el registro básico.

**Independent Test**: registrar un conductor, asignarlo a dos vehículos, terminar una asignación y
ver ambas fichas.

**Acceptance Scenarios**:

1. **Given** una persona existente, **When** se registra como conductor, **Then** queda marcada como
   conductor sin tocar sus demás datos, y la marca la da el registro de conductor, no una casilla
   editable de la persona.
2. **Given** un conductor asignado a un vehículo, **When** se termina la asignación con fecha,
   **Then** la ficha del vehículo lo muestra en la historia y no como conductor actual.
3. **Given** un propietario que también maneja su vehículo, **When** se registra como conductor del
   mismo vehículo, **Then** se admite.

---

### User Story 6 - Suscribir servicios y facturarlos cada mes (Priority: P2)

El administrador crea en Comercial los productos «Radio» y «App» con su precio. En cada vehículo
marca a qué servicios está suscrito, desde qué fecha y, si negoció otro precio, con cuál. El primer
día hábil del mes lanza la facturación del período: el sistema genera una factura por vehículo
activo, al propietario, con una línea por servicio vigente. Las de crédito quedan en Cartera como
pendientes de pago. Un día después llega un vehículo nuevo y se le hace su factura individual, que
el propietario paga en el acto.

**Why this priority**: es el puente entre el vehículo y el pago que dispara la notificación; sin
facturación no hay qué notificar.

**Independent Test**: con vehículos activos, inactivos y retirados, y suscripciones vigentes y
vencidas, lanzar la facturación del período, ver qué facturó y qué no y por qué, y relanzarla para
comprobar que no factura dos veces.

**Acceptance Scenarios**:

1. **Given** 10 vehículos activos con suscripciones vigentes y 2 inactivos, **When** se factura el
   período, **Then** salen 10 facturas, una por vehículo, cada una con sus servicios y su precio
   (el de la suscripción si lo tiene, si no el del producto vigente).
2. **Given** un período ya facturado, **When** se lanza otra vez, **Then** sólo factura los
   vehículos que faltan y el resumen dice cuáles ya estaban.
3. **Given** un vehículo nuevo a mitad de mes, **When** se le factura de forma individual, de
   contado, **Then** la factura queda pagada en el mismo proceso.
4. **Given** una facturación lanzada, **When** termina, **Then** el resumen dice cuántas facturas,
   por cuánto, qué vehículos se omitieron y por qué (inactivo, sin suscripción, sin precio vigente,
   propietario sin datos para facturar).

---

### User Story 7 - El maestro define un conector y lo habilita para una empresa (Priority: P3)

El maestro registra el conector «Proveedor de radio — asignar crédito»: la dirección base, cómo
autenticarse (iniciar sesión con usuario y clave y usar el token que devuelve), el endpoint, la
plantilla del cuerpo con variables (código radio-móvil, placa, valor, recibo, observación,
identificador único) y qué significa cada respuesta del proveedor. Lo prueba con una simulación que
muestra el cuerpo que enviaría sin enviarlo, y lo habilita para la empresa que lo usa.

**Why this priority**: es la pieza nueva más técnica y la última en el orden de construcción.

**Independent Test**: definir un conector contra un servidor de prueba, habilitarlo para una
empresa, y comprobar que otra empresa no lo ve.

**Acceptance Scenarios**:

1. **Given** un conector nuevo, **When** el maestro guarda una dirección que no es segura (sin
   cifrado) o apunta a una red interna, **Then** se rechaza.
2. **Given** un conector con plantilla, **When** el maestro pide la simulación con datos de ejemplo,
   **Then** ve el cuerpo exacto que se enviaría, sin credenciales y sin enviar nada.
3. **Given** un conector usado por envíos ya hechos, **When** el maestro cambia la plantilla,
   **Then** se crea una versión nueva y los envíos hechos siguen mostrando con qué versión salieron.

---

### User Story 8 - La empresa configura sus credenciales y liga sus servicios (Priority: P3)

El administrador de la empresa ve los conectores que el maestro le habilitó, pone el usuario y la
clave que le entregó el proveedor, prueba la conexión y liga el producto «Radio» al conector de
radio y «App» al de la app. Nunca vuelve a ver la clave; si cambia, la reemplaza.

**Why this priority**: sin esto el conector no puede enviar nada; va con la US7.

**Independent Test**: poner credenciales, probar la conexión (correcta e incorrecta), ligar un
producto y comprobar que la clave no aparece en ninguna pantalla, informe, auditoría ni registro.

**Acceptance Scenarios**:

1. **Given** un conector habilitado, **When** el administrador guarda credenciales y prueba la
   conexión, **Then** el sistema dice si el proveedor las aceptó, sin mostrar el token ni la clave.
2. **Given** credenciales guardadas, **When** se vuelve a abrir la configuración, **Then** se ve sólo
   que hay credenciales, quién y cuándo las puso, y la opción de reemplazarlas.
3. **Given** un producto ligado a un conector, **When** el maestro deshabilita el conector para esa
   empresa, **Then** los envíos nuevos quedan en espera («conector deshabilitado») y no se pierden.

---

### User Story 9 - Al pagarse un servicio, el proveedor se entera solo (Priority: P3)

Un propietario paga en la caja la factura de su taxi (radio y app). Segundos después, sin que el
cajero haga nada, el ERP avisa al proveedor de radio y al de la app, cada uno con su conector, y el
servicio queda activo. Otro propietario paga por consignación bancaria y otro por descuento de
nómina: igual. El administrador revisa el tablero de envíos: lo enviado, lo que el proveedor
rechazó y por qué, y lo que espera reintento. Uno que se rechazó porque «el vehículo no existe» lo
corrige con el proveedor y lo reintenta desde el tablero.

**Why this priority**: es el objetivo de negocio del pedido.

**Independent Test**: contra un proveedor de prueba, pagar una factura completa, una parcial, y anular
un pago ya notificado; comprobar qué se envió, cuántas veces y qué quedó marcado.

**Acceptance Scenarios**:

1. **Given** una factura de un vehículo con radio y app, **When** se paga completa por cualquier
   canal (caja, banco, convenio, pasarela, nómina o pagaduría), **Then** salen dos envíos, uno a
   cada conector, cada uno por el valor de su línea y con su identificador único.
2. **Given** un pago parcial que no salda ninguna línea, **When** se aplica, **Then** no se envía
   nada; cuando un pago posterior salda la línea, se envía esa línea.
3. **Given** un envío que el proveedor respondió con error temporal, **When** se reintenta, **Then**
   se usa el mismo identificador único, de modo que el proveedor nunca asigna el crédito dos veces.
4. **Given** un envío que el proveedor respondió «el vehículo ya tiene vigencia», **When** se
   procesa la respuesta, **Then** queda «hecho con aviso» y no se reintenta.
5. **Given** un pago ya notificado, **When** se anula o se reversa el recibo, **Then** la anulación
   procede, el envío queda «pago anulado: revisar en el sistema externo» y el administrador de la
   empresa recibe el aviso.
6. **Given** un token vencido, **When** el proveedor responde «no autorizado», **Then** el sistema
   inicia sesión otra vez y repite el envío una sola vez, sin intervención.

---

### Edge Cases

- **Vehículo sin código radio-móvil** cuando la plantilla del conector lo necesita: el envío queda
  «revisar: falta el código radio-móvil del vehículo», y al completarlo se reintenta desde el
  tablero.
- **Vehículo retirado o con otro propietario** al momento del pago de una factura ya emitida: se
  notifica igual, porque el servicio pagado es del período facturado. El tablero muestra el estado
  actual del vehículo.
- **Producto sin conector ligado**: el pago no genera ningún envío. No es un error.
- **Un mismo producto ligado a un conector que luego se desliga**: lo ya pagado conserva su envío;
  lo que se pague después ya no genera envío.
- **Proveedor caído varias horas**: los envíos esperan con reintentos espaciados; nada se pierde.
  Tras el máximo de intentos quedan «revisar».
- **Respuesta del proveedor no prevista en el mapa**: queda «revisar» con el código y el texto
  recibidos.
- **Dos personas facturan el mismo período a la vez**: un vehículo nunca recibe dos facturas del
  mismo servicio y período.
- **Vehículo suscrito a mitad de mes**: se factura según la política de la empresa (mes completo,
  por defecto, o proporcional a los días).
- **Precio del producto cambia a mitad de mes**: se usa el vigente en la fecha de la factura.
- **Propietario que es asociado y deja de serlo**: la clase de vinculación que muestra la ficha
  cambia sola a «externo»; la historia conserva lo que era en cada fecha de cambio de propietario.
- **Módulo deshabilitado con facturas pendientes de pago**: las facturas siguen en Cartera y su
  pago se aplica; los envíos se generan si el conector sigue habilitado.
- **Plantilla con la misma placa dos veces**: la revisión lo marca como error en las dos filas.
- **Persona propietaria eliminada**: no se puede eliminar una persona que es propietaria o
  conductora vigente de un vehículo vivo; el mensaje dice de cuál.

## Requirements *(mandatory)*

### Functional Requirements

**Habilitación por empresa (maestro)**

- **FR-001**: El sistema MUST permitir al maestro, y sólo al maestro, habilitar y deshabilitar el
  módulo Transporte por empresa, con motivo, quedando auditado.
- **FR-002**: Con el módulo deshabilitado, el sistema MUST ocultar su menú y responder a sus rutas
  con un error tipado de «módulo no habilitado», sin importar los permisos del usuario.
- **FR-003**: Deshabilitar un módulo MUST conservar todos sus datos; habilitarlo de nuevo los
  muestra como estaban.
- **FR-004**: La habilitación de módulos MUST servir para otros módulos en el futuro, sin cambiar
  el mecanismo.

**Vehículos**

- **FR-005**: El sistema MUST registrar vehículos con placa (única entre vehículos vivos de la
  empresa), número interno (obligatorio, único entre vivos) y código radio-móvil (opcional, único
  entre vivos).
- **FR-006**: El sistema MUST guardar la ficha técnica: marca, línea, modelo (año), clase,
  carrocería, color, cilindraje, combustible, capacidad de pasajeros, número de motor, chasis o
  VIN, número de licencia de tránsito, organismo de tránsito, fecha de matrícula, clase de servicio
  y modalidad.
- **FR-007**: El sistema MUST guardar la vinculación: fecha de ingreso, número y vigencia del
  contrato de vinculación, tarjeta de operación, grupo de rodamiento y mínimos de rodamiento diario
  y mensual (datos informativos, sin control ni cálculo).
- **FR-008**: El sistema MUST guardar los equipos del vehículo (taxímetro o registradora
  electrónica, radio, GPS, usuario de app y otros que la empresa defina), cada uno con código y
  estado.
- **FR-009**: Un vehículo MUST tener exactamente un estado: Activo, Inactivo o Retirado. Cada cambio
  lleva fecha y motivo, queda en la historia del vehículo y en la auditoría.
- **FR-010**: Un vehículo Retirado MUST liberar su placa, número interno y radio-móvil para un
  registro nuevo, y MUST seguir consultable con toda su historia. Reingresar un vehículo retirado
  es un registro nuevo.
- **FR-011**: Lo registrado de un vehículo MUST corregirse por edición, salvo el estado y el
  propietario, que sólo cambian por su operación con historia (FR-009, FR-014).
- **FR-012**: El sistema MUST permitir buscar vehículos por placa, número interno, radio-móvil,
  documento o nombre del propietario y estado, y exportar el listado.

**Propietario**

- **FR-013**: Todo vehículo vivo MUST tener un propietario, que es una persona del maestro de
  personas. Si la persona no existe, se crea desde el diálogo de persona del ERP, sin otra vía.
- **FR-014**: El cambio de propietario MUST llevar fecha efectiva y motivo, guardar propietario
  anterior y nuevo en la historia, y quedar auditado. Una fecha efectiva no puede ser anterior al
  último cambio de propietario.
- **FR-015**: Antes de confirmar un cambio de propietario o un retiro, el sistema MUST mostrar los
  saldos vencidos del propietario saliente en la cooperativa, sin bloquear la operación.
- **FR-016**: La clase de vinculación MUST deducirse: «asociado» si el propietario es asociado
  vigente, «externo» si no. No se digita.
- **FR-017**: Una persona que es propietaria o conductora vigente de un vehículo vivo MUST NOT
  poder eliminarse; el mensaje nombra el vehículo.

**Conductores**

- **FR-018**: El sistema MUST registrar a una persona como conductor y asignarle vehículos con fecha
  de inicio y, al terminar, fecha de fin. Un vehículo puede tener varios conductores vigentes.
- **FR-019**: La condición de conductor MUST nacer del registro de conductor y no de una casilla
  editable de la persona.

**Documentos del vehículo**

- **FR-020**: Cada empresa MUST tener su catálogo de tipos de documento, sembrado con SOAT, revisión
  técnico-mecánica, tarjeta de operación, póliza de responsabilidad civil contractual, póliza de
  responsabilidad civil extracontractual, todo riesgo y revisión preventiva. Cada tipo dice si es
  obligatorio y con cuántos días de anticipación avisa. La empresa agrega e inactiva tipos; no se
  borran.
- **FR-021**: Cada documento de un vehículo MUST guardar tipo, número, emisor o aseguradora, fecha
  de expedición, inicio de vigencia y vencimiento. Registrar uno nuevo del mismo tipo lo deja
  vigente y el anterior queda en la historia.
- **FR-022**: El sistema MUST ofrecer un tablero y un informe exportable de documentos vencidos, por
  vencer (según los días de aviso del tipo) y faltantes (tipos obligatorios sin documento), por
  vehículo y propietario.
- **FR-023**: Un vencimiento MUST NOT bloquear operaciones ni cambiar el estado del vehículo.

**Catálogos**

- **FR-024**: Marca, clase de vehículo, carrocería, combustible, modalidad, clase de servicio,
  organismo de tránsito, grupo de rodamiento y tipo de equipo MUST ser catálogos por empresa, con
  código de catálogo de la cooperativa (alfanumérico, en mayúsculas, sin espacios, único) y semilla
  con los valores comunes del sector.

**Carga y actualización masiva**

- **FR-025**: El sistema MUST ofrecer plantillas descargables para vehículos (llave: placa;
  propietario por tipo y número de documento), documentos del vehículo (llave: placa y tipo),
  conductores y sus asignaciones (llave: documento del conductor y placa), cambios de estado y de
  propietario (placa, acción, fecha, motivo) y suscripciones a servicios (placa y producto).
- **FR-026**: Toda plantilla MUST tener modo revisión (corre todas las reglas y no guarda nada) y
  modo aplicación (todo o nada), errores por fila y columna, y la opción de actualizar lo existente
  (sólo las celdas con valor) o conservarlo.
- **FR-027**: Una plantilla MUST aplicar las mismas reglas que la operación una a una; no existe un
  camino masivo con reglas propias.

**Servicios y facturación**

- **FR-028**: Un servicio MUST ser un producto de Comercial marcado como servicio de vehículo. Su
  precio es el precio vigente del producto.
- **FR-029**: Un vehículo MUST poder suscribirse a uno o varios servicios, cada uno con fecha de
  inicio, fecha de fin opcional y, si se negoció, precio propio con vigencia.
- **FR-030**: La facturación de un período MUST generar, para cada vehículo Activo con al menos una
  suscripción vigente en el período, una factura de Comercial al propietario con una línea por
  servicio. La factura lleva el vehículo y el período.
- **FR-031**: Un vehículo MUST NOT recibir dos facturas del mismo servicio y período, aunque la
  facturación se lance varias veces o desde dos sesiones a la vez.
- **FR-032**: La facturación MUST poder lanzarse en lote para el período o de forma individual
  para un vehículo, a crédito (queda como cuenta por cobrar en Cartera) o de contado (se paga en
  el mismo proceso).
- **FR-033**: La facturación MUST producir un resumen: facturas emitidas, valor total, y vehículos
  omitidos con su motivo.
- **FR-034**: El período MUST ser el mes calendario. La empresa MUST poder elegir, como política
  con vigencia, si una suscripción que empieza o termina dentro del mes se factura completa (por
  defecto) o proporcional a los días.

**Conectores (maestro)**

- **FR-035**: El maestro MUST poder definir conectores HTTP/JSON como datos: nombre, dirección base,
  autenticación (inicio de sesión que devuelve un token, con dónde leerlo y cuánto dura; llave fija
  en una cabecera; o usuario y clave básicos), endpoint y método de la operación, plantilla del
  cuerpo y mapa de respuestas.
- **FR-036**: La plantilla del cuerpo MUST admitir variables del vehículo (placa, número interno,
  código radio-móvil), del pago y la factura (valor de la línea, número de recibo, número de
  factura, fecha de pago, período facturado con su inicio y fin, nombre del servicio, observación)
  y el identificador único del envío, cada una con su formato (entero sin decimales, texto, fecha
  con formato).
- **FR-037**: El mapa de respuestas MUST clasificar cada combinación de código y texto de respuesta
  como hecho, hecho con aviso, reintentar, volver a autenticarse o revisar a mano. Lo no previsto es
  «revisar a mano».
- **FR-038**: El sistema MUST rechazar direcciones sin cifrado o que apunten a redes internas o
  privadas, y MUST limitar el tiempo de espera de cada llamada.
- **FR-039**: Cambiar la definición de un conector con envíos MUST crear una versión nueva; cada
  envío conserva la versión con que salió.
- **FR-040**: El maestro MUST poder simular un conector con datos de ejemplo y ver el cuerpo exacto
  que se enviaría, sin enviarlo y sin credenciales.
- **FR-041**: El maestro MUST habilitar y deshabilitar cada conector por empresa, con motivo y
  auditado. Una empresa sólo ve los conectores que tiene habilitados.

**Conectores (empresa)**

- **FR-042**: El administrador de la empresa MUST poder poner y reemplazar las credenciales de cada
  conector habilitado. Las credenciales se guardan cifradas y nunca se muestran, exportan, auditan
  ni registran; la pantalla dice sólo que existen, quién y cuándo las puso.
- **FR-043**: El administrador MUST poder probar la conexión (autenticarse contra el proveedor) y
  ver si fue aceptada, sin ver el token.
- **FR-044**: El administrador MUST poder ligar cada producto de servicio a un conector habilitado.
  Un producto se liga a lo sumo a un conector; un conector puede servir a varios productos.

**Notificación y tablero de envíos**

- **FR-045**: Cuando un pago aplicado por cualquier canal de recaudo (caja, banco, convenio,
  pasarela, nómina o pagaduría), o el pago de contado de la factura, deja saldada la línea de un
  servicio de un vehículo, el sistema MUST generar un envío a su conector por el valor de la línea.
- **FR-046**: Un pago que no salda la línea MUST NOT generar envío.
- **FR-047**: El envío MUST hacerse en segundo plano, sin demorar el recibo ni la factura, con
  reintentos espaciados ante errores temporales y renovación de la sesión ante «no autorizado».
- **FR-048**: Cada envío MUST tener un identificador único estable (por vehículo, servicio y línea de
  factura) que se repite en todos los reintentos, para que el proveedor no aplique dos veces.
- **FR-049**: El sistema MUST ofrecer un tablero de envíos por empresa con su estado (pendiente,
  enviado, hecho con aviso, reintentando, revisar, en espera por conector deshabilitado o sin
  credenciales, pago anulado), el vehículo, el servicio, la factura, el pago, la respuesta del
  proveedor y el historial de intentos, exportable.
- **FR-050**: Desde el tablero, quien tenga permiso MUST poder reintentar un envío (con el mismo
  identificador) o cerrarlo como resuelto a mano, con motivo, auditado.
- **FR-051**: Si un pago ya notificado se anula o se reversa, la anulación MUST proceder, el envío
  MUST quedar «pago anulado: revisar en el sistema externo», y el administrador de la empresa MUST
  recibir un aviso.
- **FR-052**: Cada intento de envío MUST quedar en la auditoría con el conector, su versión, el
  cuerpo enviado (sin credenciales), el código y el texto de la respuesta, y la duración.

**Transversales**

- **FR-053**: Cada acción MUST tener su permiso (consultar, crear y editar, cambiar estado, cambiar
  propietario, retirar, importar, administrar catálogos, administrar servicios, facturar, configurar
  conectores, consultar y gestionar envíos). La semilla los reparte por rol: el Operador consulta,
  crea y edita; retirar, cambiar propietario, facturar y configurar conectores son del
  administrador.
- **FR-054**: Toda pantalla del módulo MUST mostrar que está cargando por zona.
- **FR-055**: Ninguna pantalla, informe ni plantilla MUST mencionar el sistema anterior de la
  cooperativa.

**Pago parcial entre servicios (Q1)**

- **FR-056**: Cuando un pago no cubre la factura completa de un vehículo con varios servicios, en
  la caja el cajero MUST poder elegir qué servicio (línea) paga; por banco, convenio, pasarela,
  nómina o pagaduría, donde nadie elige, el pago MUST saldar las líneas en el orden de prioridad que
  la empresa configura por servicio (y, a igual prioridad, en el orden de la factura). Sin elección
  en la caja se aplica la misma prioridad.

### Key Entities *(include if feature involves data)*

- **Habilitación de módulo**: qué módulo está habilitado para qué empresa, desde cuándo, por quién y
  con qué motivo. Vive en la administración de la plataforma, no en la base de la cooperativa.
- **Vehículo**: placa, número interno, código radio-móvil, ficha técnica, vinculación, mínimos de
  rodamiento, estado vigente y propietario vigente.
- **Historia del vehículo**: cada cambio de estado y de propietario, con fecha efectiva, motivo,
  valores anterior y nuevo, y usuario.
- **Equipo del vehículo**: tipo de equipo, código y estado.
- **Tipo de documento** (catálogo por empresa) y **Documento del vehículo**: número, emisor,
  vigencia y vencimiento; uno vigente por tipo y la historia de los anteriores.
- **Conductor** y **Asignación de conductor**: la persona como conductor y su relación con
  vehículos con fecha de inicio y fin.
- **Catálogos de Transporte**: marca, clase, carrocería, combustible, modalidad, clase de servicio,
  organismo de tránsito, grupo de rodamiento, tipo de equipo.
- **Suscripción a servicio**: vehículo, producto de servicio, vigencia y precio propio opcional con
  vigencia.
- **Facturación de período**: la corrida (período, modo, quién, cuándo, resumen) y, por vehículo y
  servicio, la factura y la línea que generó, que son la marca de «ya facturado».
- **Conector** y **Versión de conector**: definición del maestro (autenticación, operación,
  plantilla, mapa de respuestas).
- **Habilitación de conector por empresa**: qué empresa puede usar qué conector.
- **Configuración del conector en la empresa**: credenciales cifradas y quién las puso; productos
  ligados.
- **Envío** e **Intento de envío**: la notificación de una línea saldada a un conector, con su
  identificador único, estado y cada intento con su respuesta.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una cooperativa carga un parque de 1.000 vehículos con sus propietarios, desde la
  revisión hasta la aplicación, en menos de 10 minutos de trabajo de pantalla.
- **SC-002**: Un operador registra un vehículo nuevo con propietario existente en menos de 3
  minutos.
- **SC-003**: La facturación mensual de 1.000 vehículos termina en menos de 5 minutos y nunca
  produce dos facturas del mismo servicio, vehículo y período.
- **SC-004**: Con el proveedor disponible, el 99 % de los servicios pagados se notifican en menos
  de 2 minutos desde el pago, sin intervención del cajero.
- **SC-005**: Ningún servicio se asigna dos veces en el proveedor por reintentos del ERP: el mismo
  pago produce siempre el mismo identificador único.
- **SC-006**: Cero credenciales visibles en pantallas, exportaciones, auditoría o registros del
  sistema (verificado por búsqueda en todos ellos tras una prueba completa).
- **SC-007**: Una empresa sin Transporte habilitado no tiene ninguna ruta ni opción de menú de
  Transporte accesible, incluso con todos los permisos.
- **SC-008**: El área de transporte identifica todos los documentos vencidos o por vencer del
  parque desde una sola pantalla, sin hojas de cálculo.
- **SC-009**: Conectar un segundo proveedor distinto, con otra forma de autenticación y otro
  cuerpo, se hace sólo con configuración, sin cambiar el programa.

## Assumptions

- **Comercial y Cartera ya existen** (features 012 y 013): la factura de servicios es una factura
  de Comercial con su numeración, impuestos y, si la empresa la tiene activa, factura electrónica.
  La venta a crédito pasa a Cartera por el camino que ya existe, y los pagos se aplican por Recaudo
  (feature 015) como hoy.
- **La bandeja de mensajes de integración existente** es el mecanismo de entrega en segundo plano;
  el conector es un destino más, con su idempotencia y su historial.
- **Personas**: propietarios y conductores son personas del maestro de personas de cada
  cooperativa (Principio V) y se crean por el diálogo único de persona (feature 008). El asociado
  vigente lo dice el registro de asociados que ya existe.
- **Plantillas**: se usa la mecánica de carga de la feature 016 (revisión, aplicación todo o nada,
  actualizar o conservar).
- **Cifrado**: las credenciales usan el mismo llavero que ya cifra los secretos del segundo factor
  y las claves de adjuntos.
- **Sólo HTTP/JSON**: un proveedor con otro protocolo (SOAP, firmas propias) exigiría un adaptador
  nuevo cuando aparezca; queda fuera.
- **Fuera de alcance**: licencias de conducción, tarjeta de control y seguridad social del
  conductor; cobro y control del rodamiento; despacho, turnos, producido o kilometraje; reversa en
  el proveedor (su API no la tiene); notificar al facturar.
- **Semillas del sector**: las clases de vehículo, combustibles, modalidades y tipos de documento
  sembrados son los comunes del transporte en Colombia (Decreto 1079 de 2015 y RUNT); el contador o
  el área de transporte los revisa antes de la salida.
- **La plantilla de suscripciones** no estaba en la lista del dueño; se agrega porque la salida en
  vivo exige cargar los servicios de todo el parque de una vez.
- **Una sola entrega antes del 01/12/2026**, construida por fases en el orden de las historias.
