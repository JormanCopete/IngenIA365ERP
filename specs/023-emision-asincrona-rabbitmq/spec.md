# Feature Specification: Emisión electrónica asíncrona — el cobro no espera a la DIAN

**Feature Branch**: `claude/dian-asincrona-rabbitmq-798014` (carpeta `specs/023-emision-asincrona-rabbitmq`)

**Created**: 2026-11-11

**Status**: Draft — decisiones de fondo del dueño tomadas el 2026-11-11 (D1–D4)

**Input**: pedido del dueño tras el diagnóstico del VPS de producción (2026-11-11): «separar del cobro del POS (y de la factura de
Ventas) la emisión del documento electrónico ante la DIAN y todo lo que no tenga que ocurrir mientras el cajero espera, y llevar
esa emisión por RabbitMQ de forma que ninguna factura se pierda», con cero pérdida, reintentos con tope, mensajes muertos
visibles, idempotencia, un solo consumidor por documento, contingencia parametrizable, estado visible, observabilidad y
despliegue en el clúster por GitOps.

## Contexto: lo que se midió y lo que se revisó (develop `b3934111`)

- **Medición en producción (2026-11-11)**: el cobro del POS promedia 2,9 s y llega a 15,8 s. El servidor está holgado (85 % de
  CPU libre, 6 GB de memoria libres): la lentitud no es de capacidad sino de lo que el cobro hace antes de responder.
- **El cobro espera a propósito.** Tras confirmar la venta, el cobro intenta la emisión ante el canal y, si queda «enviado sin
  respuesta», consulta hasta cumplir `Dian.EsperaMaximaPosSegundos` (15 s por defecto, por punto de venta). La tirilla sólo sale
  si la DIAN validó (o si hay contingencia). Antes de responder también sube el documento canónico como adjunto.
- **La orden de emitir ya nace con la venta.** El documento electrónico se registra pendiente en la misma transacción que
  confirma la venta, con su número; un procesador de fondo lo retoma cada 15 s con arrendamiento por cooperativa y por
  documento, consulta antes de reenviar lo ambiguo, usa una clave de idempotencia por cooperativa, ambiente, número y versión,
  reintenta con esperas crecientes **sin tope**, alerta los rechazos y abre contingencia por circuito. Es, en la práctica, una
  bandeja de salida transaccional: lo que falta no es la garantía sino que el cajero deje de esperar.
- **Los mensajes a Contabilidad** ya salen en segundo plano por la bandeja de integración, despertados por un aviso en proceso
  (uno por réplica) y con sondeo de respaldo; reintentan sin tope y alertan a los tres intentos o quince minutos.

## Clarifications

### Session 2026-11-11 (formulario del dueño)

- D1 ¿Qué hace el cobro? → **Imprimir ya**: la tirilla sale de inmediato con el código único (CUDE/CUFE) y el QR calculados
  localmente y la leyenda «pendiente de validación DIAN»; la emisión sigue detrás. La impresión nunca espera a la DIAN. Requiere
  el aval de la contadora (entrega antes de la validación).
- D2 ¿Qué papel cumple RabbitMQ? → **Despertador y reparto**: la base de datos sigue siendo la fuente de verdad (la orden nace
  en la transacción del documento); RabbitMQ lleva cada orden a quien la procesa en el acto y reparte la carga entre réplicas; el
  sondeo actual queda como red de seguridad. Si RabbitMQ no está o pierde algo, nada se pierde ni se detiene.
- D3 ¿Tope de reintentos? → **12 intentos por defecto (~7 h)**, parametrizable por cooperativa; agotados, el trabajo pasa a
  **mensajes muertos**, visibles en pantalla, con reproceso manual auditado y alerta.
- D4 ¿Alcance? → la emisión del POS **y** de la factura de Ventas en oficina, los **adjuntos** de la emisión (canónico, XML
  firmado, representación gráfica), la **entrega al comprador** por correo y los **mensajes a Contabilidad** de la bandeja de
  integración.

### Session 2026-11-11 (segunda ronda)

- D5 ¿Cómo se activa «imprimir ya»? → **Siempre, sin parámetro**: desde el despliegue todas las cooperativas imprimen sin esperar
  y la espera se retira por completo. Pedido adicional del dueño: en la configuración de la caja, junto a las copias a imprimir,
  poder indicar que la tirilla **vaya directo a la impresora por defecto** sin mostrar la vista previa en pantalla.
- D7 (corrige D1 y D5) El CUDE y el QR no los calcula el ERP: los genera el canal al emitir. ¿Qué lleva la tirilla? → **Espera
  corta y luego sin CUDE**: el cobro intenta emitir con un tope corto por punto de venta (`Dian.EsperaMaximaPosSegundos`, que se
  conserva con defecto **2 s** y admite 0); si el canal responde a tiempo, la tirilla sale completa con CUDE y QR; si no, sale **igual**,
  sin CUDE ni QR, con número, totales y la leyenda «pendiente de validación DIAN», y el comprador recibe la representación completa
  por correo cuando el documento queda validado. Nunca hay cobro sin tirilla. COOFLOPAL usa hoy el canal **simulado** en producción.
- D6 ¿Quién ve y reprocesa los mensajes muertos? → **Dos permisos nuevos**: ver (supervisor y administrador) y reprocesar
  (administrador), por cooperativa; el maestro ve todas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El cajero cobra y entrega la tirilla sin quedar atado a la DIAN (Priority: P1)

El cajero del POS cobra una venta con factura o documento equivalente electrónico. En cuanto la venta queda confirmada (kardex,
número, recaudo), el ERP espera al canal a lo sumo la espera corta del punto de venta: si validó, la tirilla sale con CUDE y
QR; si no, sale igual sin ellos y con la leyenda «pendiente de validación DIAN». La caja queda libre para la venta siguiente y
la emisión ante la DIAN sigue detrás.

**Why this priority**: es la lentitud que se siente en producción; COOFLOPAL cobra todo el día por el POS.

**Independent Test**: con el canal simulado configurado para tardar 10 s y la espera en 2 s, cobrar una venta y medir que la
respuesta llega en menos de 2,5 s con tirilla, y que minutos después el documento aparece validado sin que nadie haga
nada.

**Acceptance Scenarios**:

1. **Given** una caja abierta, espera de 2 s y un canal que tarda 10 s, **When** el cajero cobra, **Then** la respuesta llega en
   menos de 2,5 s con el número, las vueltas y la tirilla sin CUDE con la leyenda «pendiente de validación DIAN».
5. **Given** un canal que valida en 0,4 s, **When** el cajero cobra, **Then** la tirilla sale completa con CUDE y QR.
2. **Given** un canal caído, **When** el cajero cobra, **Then** cobra igual de rápido, la tirilla sale con la misma leyenda y el
   documento queda «en reintento».
3. **Given** una venta cobrada cuyo documento quedó validado después, **When** se reimprime la tirilla, **Then** sale sin la
   leyenda de pendiente.
4. **Given** el mismo cobro repetido con la misma clave de operación (doble clic, red que corta), **When** llega la repetición,
   **Then** se devuelve el mismo resultado y no nace una segunda venta ni un segundo documento electrónico.

---

### User Story 2 - Ninguna factura se pierde (Priority: P1)

Toda venta confirmada que debe emitirse llega a la DIAN, aunque caiga la API, se reinicie un nodo, RabbitMQ no esté o el
proveedor falle por horas. Un mismo documento nunca se emite dos veces.

**Why this priority**: es la exigencia no negociable del dueño; una factura perdida es un problema tributario.

**Independent Test**: confirmar ventas con RabbitMQ detenido y con la API reiniciándose a mitad de una emisión; al volver todo,
cada venta tiene exactamente un documento electrónico validado y ningún envío duplicado ante el canal.

**Acceptance Scenarios**:

1. **Given** RabbitMQ detenido, **When** se confirman ventas, **Then** las ventas se confirman igual, sus órdenes quedan
   retenidas y salen solas al volver RabbitMQ; mientras tanto el sondeo de respaldo las sigue emitiendo.
2. **Given** una réplica que cae después de enviar al canal y antes de registrar la respuesta, **When** el documento se retoma,
   **Then** primero se consulta su estado ante el canal y sólo se reenvía si el canal no lo conoce.
3. **Given** la misma orden entregada dos veces (redelivery) o tomada por dos réplicas, **When** ambas intentan, **Then** una
   sola procesa el documento y la otra lo suelta sin llamar al canal.
4. **Given** una venta que se revierte antes del commit, **When** termina la transacción, **Then** no queda ninguna orden de
   emitir ni nada publicado.

---

### User Story 3 - Reintentos con tope, mensajes muertos y reproceso (Priority: P2)

Un error transitorio (red, falla del proveedor, tiempo agotado) se reintenta solo con esperas crecientes hasta el tope de la
cooperativa (12 por defecto). Agotado el tope, el trabajo pasa a «mensajes muertos», se levanta una alerta y aparece en una
pantalla donde una persona con permiso ve el motivo de cada intento y lo reprocesa; el reproceso queda auditado. Un rechazo de
validación de la DIAN no se reintenta: queda «rechazado», alerta y sigue el flujo de corrección que ya existe.

**Why this priority**: sin tope, una falla larga se esconde; con tope, alguien se entera y actúa.

**Independent Test**: con el canal devolviendo error transitorio siempre y el tope en 3, ver tres intentos, el paso a mensajes
muertos, la alerta, y que «Reprocesar» lo devuelve a la cola, lo emite al sanar el canal y deja la marca en la auditoría.

**Acceptance Scenarios**:

1. **Given** un canal que falla transitoriamente, **When** se agotan los intentos de la cooperativa, **Then** el documento queda
   «muerto», con la alerta levantada y visible en la pantalla de mensajes muertos.
2. **Given** un documento muerto, **When** una persona con permiso lo reprocesa con un motivo, **Then** vuelve a intentarse de
   inmediato con su contador reiniciado y la auditoría registra quién, cuándo y por qué.
3. **Given** un rechazo de validación de la DIAN, **When** llega la respuesta, **Then** no se reintenta, queda «rechazado» y se
   alerta; no pasa por mensajes muertos.
4. **Given** una persona sin el permiso, **When** intenta reprocesar, **Then** se le niega.

---

### User Story 4 - Estado visible para quien vende y quien supervisa (Priority: P2)

En la venta, en la factura de Ventas y en la bandeja de documentos electrónicos se ve el estado de cada documento: pendiente de
envío, enviado, aceptado (validado), rechazado, en reintento (con el número de intento y cuándo toca el siguiente) o muerto.

**Why this priority**: al no esperar, el cajero y el supervisor necesitan saber qué pasó después.

**Independent Test**: seguir una venta desde el cobro hasta validada y ver cada estado en la pantalla de la venta y en la
bandeja.

**Acceptance Scenarios**:

1. **Given** un documento en reintento, **When** se abre la venta, **Then** se ve «en reintento», el intento actual de cuántos y
   la hora del próximo.
2. **Given** un documento rechazado después de entregada la tirilla, **When** el supervisor abre la bandeja, **Then** lo ve
   destacado con los motivos traducidos y la alerta correspondiente.

---

### User Story 5 - La factura de Ventas en oficina tampoco espera (Priority: P2)

Al confirmar una factura electrónica desde Ventas, la confirmación responde en cuanto el documento queda confirmado; la emisión,
los adjuntos y el envío al comprador siguen por la misma vía asíncrona.

**Why this priority**: misma causa, menos frecuencia que el POS.

**Independent Test**: confirmar una factura de oficina con canal lento y medir que responde sin esperar al canal; luego verla
validada y enviada al comprador.

**Acceptance Scenarios**:

1. **Given** un canal que tarda 10 s, **When** se confirma una factura en oficina, **Then** la confirmación responde en menos de
   1 s con el documento «pendiente de envío».

---

### User Story 6 - Adjuntos y entrega al comprador detrás (Priority: P3)

El canónico, el XML firmado y la representación gráfica se guardan detrás, nunca dentro de la petición del cajero; el correo al
comprador sale cuando el documento queda validado (o en contingencia, según la regla vigente), por la misma vía asíncrona y con
los mismos reintentos, tope y mensajes muertos.

**Why this priority**: hoy suben dentro del cobro; con US1 dejan de bloquear, pero necesitan garantía propia.

**Independent Test**: con el almacén de adjuntos caído, cobrar; la venta responde; al volver el almacén los adjuntos aparecen y
el correo sale una sola vez.

**Acceptance Scenarios**:

1. **Given** el almacén de adjuntos caído, **When** se cobra, **Then** el cobro responde igual y los adjuntos se guardan cuando el
   almacén vuelve.
2. **Given** un correo ya enviado, **When** la tarea se repite, **Then** el comprador no recibe un segundo correo.

---

### User Story 7 - Mensajes a Contabilidad por la misma vía (Priority: P3)

Los mensajes de la bandeja de integración hacia Contabilidad (y los demás destinos en línea) se despiertan y reparten por la
misma mensajería, con la base como fuente de verdad, el sondeo de respaldo, tope de reintentos transitorios por cooperativa y
mensajes muertos con reproceso auditado.

**Why this priority**: ya salen en segundo plano; la ganancia es repartir entre réplicas y despertar sin depender del aviso en
proceso de cada réplica.

**Independent Test**: confirmar una venta con dos réplicas y ver que su mensaje a Contabilidad lo procesa una sola, en segundos,
y que un fallo transitorio repetido termina en mensajes muertos con alerta.

**Acceptance Scenarios**:

1. **Given** dos réplicas, **When** se confirma una venta en una, **Then** su mensaje a Contabilidad se procesa una sola vez,
   por cualquiera de las dos.
2. **Given** un rechazo de negocio de Contabilidad (sin regla, cuenta inválida), **When** se procesa, **Then** queda rechazado como
   hoy y no consume reintentos.

---

### User Story 8 - Operación y observabilidad (Priority: P3)

Quien opera la plataforma ve la profundidad de cada cola, la edad del trabajo más viejo y la cantidad de mensajes muertos, por
cooperativa, y encuentra en la bitácora todo lo de un documento por su identificador. La mensajería corre en el clúster de
cada ambiente con almacenamiento persistente, recursos acotados y credenciales fuera del repositorio.

**Why this priority**: sin esto, la cola es una caja negra.

**Independent Test**: detener el consumidor, confirmar ventas y ver crecer la profundidad y la edad; reanudar y verlas bajar.

**Acceptance Scenarios**:

1. **Given** trabajo acumulado, **When** se consultan las métricas, **Then** muestran profundidad, edad del más viejo y muertos
   por cola y cooperativa.
2. **Given** un documento, **When** se busca su identificador en la bitácora, **Then** aparecen todos sus intentos, réplicas y
   resultados.

### User Story 9 - La tirilla va directo a la impresora (Priority: P2)

Quien configura la caja, junto al ancho del papel y las copias a imprimir, marca «Imprimir directo a la impresora por defecto».
Con la marca, al cobrar la tirilla sale a la impresora predeterminada del equipo sin mostrar la vista previa en pantalla ni pedir
confirmación, tantas copias como diga la caja; sin la marca, se comporta como hoy.

**Why this priority**: es el último clic que queda entre el cobro y la venta siguiente.

**Independent Test**: en la app instalada (MAUI) con una caja marcada, cobrar y ver que la tirilla sale impresa sin ningún
diálogo; en el navegador, ver que se omite la vista previa propia del ERP.

**Acceptance Scenarios**:

1. **Given** una caja marcada para imprimir directo con 2 copias, **When** el cajero cobra en la app instalada, **Then** salen dos
   tirillas en la impresora predeterminada sin vista previa ni diálogo.
2. **Given** la misma caja usada desde el navegador, **When** el cajero cobra, **Then** el ERP no muestra su vista previa y manda
   la tirilla a imprimir; si el navegador muestra su propio diálogo de impresión, el manual explica cómo configurar el equipo
   para que no lo haga.
3. **Given** una caja sin la marca, **When** el cajero cobra, **Then** todo sigue como hoy.
4. **Given** una impresión que falla (sin impresora, sin papel), **When** ocurre, **Then** el cajero ve un aviso y puede reimprimir;
   la venta ya está cobrada.

### Edge Cases

- **Contingencia abierta** (03 del facturador o 04 de la DIAN): la venta se numera y entrega como hoy en contingencia; la
  transmisión diferida va por la misma vía y el plazo legal (`Dian.PlazoContingenciaHoras`) se **informa** (alerta antes del
  plazo), no se impone. Un documento en contingencia no pasa a muertos antes de su plazo: el tope de intentos aplica a fallas
  transitorias, y la alerta del plazo sigue siendo la de hoy.
- **El canal no responde y el circuito se abre**: el comportamiento del circuito (umbral, contingencia automática o manual) no
  cambia; ahora cuenta las fallas del trabajo de fondo en vez de las esperas vencidas del cobro.
- **Rechazo después de entregar la tirilla**: el comprador ya tiene una representación «pendiente»; el documento queda rechazado,
  se alerta al supervisor y se corrige por el flujo existente (nota o reemplazo), que también va por la vía asíncrona.
- **Notas que esperan a su original** (`WaitsForDocument`): no se transmiten hasta que el original esté validado, sin importar en
  qué orden lleguen a la cola.
- **Orden y numeración**: nunca dependen de la cola; el número se asigna en la transacción de confirmación y la cola puede
  entregar en cualquier orden.
- **Mensaje en la cola de un documento que ya terminó** (validado por el sondeo de respaldo antes de que llegue el mensaje): se
  descarta sin llamar al canal.
- **Mensaje de una cooperativa inactiva o eliminada**: se reconoce y se descarta con registro; no bloquea la cola.
- **Mensaje veneno** (no se puede leer): va a muertos de la mensajería con su contenido, sin bloquear a los demás.
- **RabbitMQ lleno o lento**: la confirmación de la venta nunca espera a la mensajería; lo no publicado se publica después.
- **Reproceso de un documento que el sondeo ya está procesando**: el arrendamiento por documento impide el doble intento.
- **Tope cambiado por la cooperativa**: rige para los intentos siguientes; un documento muerto no revive solo al subir el tope.

## Requirements *(mandatory)*

### Functional Requirements

**Cobro y confirmación sin espera**

- **FR-001**: El cobro del POS MUST esperar al canal como máximo `Dian.EsperaMaximaPosSegundos` del punto de venta (defecto 2 s,
  0 = no esperar) después de confirmar la venta; nunca más, aunque el canal siga sin responder.
- **FR-002**: La respuesta del cobro MUST traer **siempre** la tirilla: con CUDE y QR si el canal validó dentro de la espera; sin
  ellos y con la leyenda «pendiente de validación DIAN» si no; en contingencia, la leyenda de contingencia que ya existe.
- **FR-003**: La reimpresión de una tirilla MUST reflejar el estado del documento en ese momento (sin leyenda de pendiente si ya
  está validado).
- **FR-004**: La confirmación de una factura electrónica desde Ventas en oficina MUST responder sin intentar la emisión.
- **FR-005**: Fuera del único intento acotado de FR-001, ninguna tarea posterior al commit que hable con el canal electrónico, el
  almacén de adjuntos o el correo MUST correr dentro de la petición del usuario; si el canal no respondió a tiempo, el intento en
  curso se abandona sin perderse (lo retoma el trabajo de fondo consultando primero).
- **FR-006**: El parámetro `Dian.EsperaMaximaPosSegundos` MUST conservarse con defecto 2 s (D7) y su descripción cambia a «segundos
  que el POS espera la validación antes de entregar la tirilla sin código único»; las versiones guardadas por una cooperativa
  siguen rigiendo.

**Impresión directa (D5)**

- **FR-033**: La configuración de la caja MUST ofrecer «Imprimir directo a la impresora por defecto», apagado por defecto, junto a
  las copias a imprimir; se audita como cualquier cambio de la caja.
- **FR-034**: Con la marca, la app instalada MUST imprimir la tirilla en la impresora predeterminada del equipo sin vista previa ni
  diálogo, tantas copias como diga la caja; en el navegador, el ERP MUST omitir su vista previa y mandar a imprimir directamente.
- **FR-035**: Una impresión fallida MUST avisar al cajero y dejar la reimpresión a un atajo de teclado, sin afectar la venta.

**Permisos (D6)**

- **FR-036**: Dos permisos nuevos MUST gobernar los mensajes muertos: verlos (supervisor y administrador) y reprocesarlos
  (administrador); los siembra la API al arrancar, como los demás.

**Bandeja de salida y mensajería (cero pérdida)**

- **FR-007**: Toda orden de trabajo asíncrono (emitir, consultar, transmitir contingencia, guardar adjuntos, entregar al
  comprador, mensajes de integración) MUST nacer en la misma transacción que el hecho que la origina, reutilizando las tablas y
  el escritor existentes (documentos electrónicos y bandeja de integración) o extendiéndolos; no se crea un mecanismo paralelo.
- **FR-008**: Un publicador MUST llevar las órdenes nuevas a la mensajería con confirmación del broker y marcar cada orden como
  publicada sólo tras esa confirmación; sin confirmación, la orden sigue pendiente de publicar y se reintenta.
- **FR-009**: La mensajería MUST usar colas durables replicables (quorum), mensajes persistentes, confirmación manual del
  consumidor **sólo después** de registrar el resultado en la base y un número acotado de mensajes en vuelo por consumidor.
- **FR-010**: El sondeo de respaldo MUST seguir procesando lo elegible aunque la mensajería no esté disponible, de modo que la
  ausencia de la mensajería retrase pero nunca detenga ni pierda una emisión.
- **FR-011**: La base de datos MUST ser la única fuente de verdad del estado; un mensaje sólo despierta el trabajo y el
  consumidor relee el estado antes de actuar.
- **FR-012**: La API MUST arrancar y confirmar ventas aunque la mensajería no esté disponible, y el estado de salud MUST
  informarlo como degradado, no como caído.

**Idempotencia y concurrencia**

- **FR-013**: Un mismo documento MUST NOT emitirse dos veces: se conserva la clave de idempotencia por documento y versión, y ante
  un intento ambiguo (tiempo agotado, caída entre la llamada y su registro) se consulta el estado ante el canal antes de reenviar.
- **FR-014**: Un solo trabajador MUST procesar un documento a la vez, en cualquier número de réplicas (arrendamiento por
  documento); un mensaje de un documento tomado se suelta sin llamar al canal y se vuelve a intentar después.
- **FR-015**: La numeración y el orden fiscal MUST NOT depender de la mensajería; las dependencias entre documentos (nota que espera
  a su original) se respetan aunque los mensajes lleguen en otro orden.
- **FR-016**: Un mensaje repetido o tardío de un trabajo ya terminado MUST reconocerse sin efecto.
- **FR-017**: La entrega al comprador MUST ocurrir a lo sumo una vez por documento y versión.

**Reintentos, mensajes muertos y alertas**

- **FR-018**: El sistema MUST clasificar cada falla como transitoria (red, 5xx, tiempo agotado, canal no disponible) o definitiva
  (rechazo de validación de la DIAN, rechazo de negocio del destino) y sólo reintentar las transitorias.
- **FR-019**: Los reintentos transitorios MUST seguir esperas crecientes (exponenciales con dispersión) hasta un tope de intentos
  parametrizable por cooperativa con vigencia, 12 por defecto, para documentos electrónicos y para mensajes de integración por
  separado.
- **FR-020**: Agotado el tope, el trabajo MUST pasar a estado «muerto», levantar una alerta en el sistema de alertas de la
  cooperativa y aparecer en una pantalla de mensajes muertos con su historia de intentos y el último motivo.
- **FR-021**: Una persona con permiso MUST poder reprocesar uno o varios trabajos muertos con un motivo obligatorio; el reproceso
  reinicia el contador, se intenta de inmediato y queda en la auditoría encadenada.
- **FR-022**: Un rechazo de validación de la DIAN MUST quedar «rechazado», alertarse como hoy y seguir el flujo de corrección
  existente, sin pasar por mensajes muertos.
- **FR-023**: Los mensajes de la mensajería que no se pueden leer MUST ir a una cola de muertos de la mensajería y registrarse; no
  bloquean a los demás.

**Contingencia**

- **FR-024**: El comportamiento de contingencia (03 y 04, circuito, contingencia automática o manual, transmisión diferida) MUST
  conservarse; el plazo de transmisión y la anticipación de la alerta siguen siendo parámetros por cooperativa, informativos.
- **FR-025**: Un documento en contingencia MUST NOT pasar a muertos antes de vencer su plazo de transmisión; vencido el plazo, se
  alerta y sigue reintentando hasta su tope.

**Estado visible**

- **FR-026**: La venta, la factura y la bandeja de documentos electrónicos MUST mostrar el estado de cada documento: pendiente de
  envío, enviado, aceptado, aceptado con notificaciones, rechazado, en reintento (intento actual, tope y próxima hora), muerto y
  en contingencia.
- **FR-027**: La bandeja MUST permitir filtrar por estado, incluidos «en reintento» y «muerto».

**Observabilidad**

- **FR-028**: El sistema MUST exponer, por cola y por cooperativa, la profundidad, la edad del trabajo más viejo pendiente y la
  cantidad de muertos, en el formato de métricas que ya consume la plataforma.
- **FR-029**: Toda línea de bitácora del trabajo asíncrono MUST llevar la cooperativa y el identificador del documento o mensaje.

**Despliegue y pruebas**

- **FR-030**: La mensajería MUST desplegarse en el clúster de cada ambiente (DEV, QA, PDN) por GitOps, con almacenamiento
  persistente, límites de CPU y memoria acotados al VPS y credenciales por secreto del clúster, nunca en el repositorio.
- **FR-031**: Las pruebas de integración MUST levantar su propia mensajería en contenedor junto a la base, como las demás
  dependencias.
- **FR-032**: Pruebas de arquitectura MUST vigilar que sólo el adaptador de mensajería conozca la librería del broker y que
  ningún handler de petición llame al canal electrónico.

### Key Entities *(include if feature involves data)*

- **Documento electrónico** (existente): gana el estado «muerto» (o una marca equivalente), el tope vigente al morir, la fecha de
  muerte y la marca de publicado en la mensajería. Sigue siendo la orden de emitir.
- **Mensaje de integración y su entrega** (existentes): ganan la marca de publicado, el contador de intentos transitorios y el
  estado «muerto».
- **Tarea de entrega asíncrona**: guardar adjuntos y entregar al comprador como trabajos con su propio estado, intentos y
  marca de hecho, nacidos de la emisión.
- **Reproceso**: quién, cuándo, motivo y qué trabajos; queda en la auditoría.
- **Parámetros nuevos por cooperativa**: tope de intentos de documentos electrónicos y tope de intentos de mensajes de integración,
  con vigencia.
- **Alertas nuevas**: documento electrónico muerto, mensaje de integración muerto.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ningún cobro del POS tarda más que la espera del punto de venta más 0,5 s; con la espera en 0, el 95 % responde en
  menos de 1 s. Medido en producción durante una semana, con el canal electrónico en cualquier estado.
- **SC-002**: Cero documentos perdidos: en las pruebas de caída (broker detenido, réplica reiniciada a mitad de la emisión,
  almacén caído), toda venta confirmada termina con exactamente un documento electrónico emitido.
- **SC-003**: Cero envíos duplicados al canal por un mismo documento y versión en las mismas pruebas, con dos réplicas.
- **SC-004**: Con el canal sano, el 95 % de los documentos queda validado en menos de 10 s desde la confirmación.
- **SC-005**: Un trabajo que agota su tope aparece en mensajes muertos y alertado en menos de un minuto desde el último intento.
- **SC-006**: Con la mensajería detenida, la emisión sigue avanzando por el respaldo con un retraso máximo de un intervalo de
  sondeo.
- **SC-007**: La mensajería en producción no supera los límites de recursos asignados en una semana de operación normal.

## Assumptions

- Imprimir antes de la validación rige siempre desde el despliegue (D5); el aval de la contadora se pide antes de promover a
  producción y queda anotado en el runbook. El QR apunta a la consulta pública con el código único, que mostrará el
  documento cuando quede validado.
- El correo al comprador sigue saliendo sólo cuando el documento es entregable según la regla vigente (validado o contingencia),
  no al imprimir la tirilla.
- La mensajería es una sola instancia por ambiente (sin clúster de varios nodos): el VPS es uno; las colas quorum se declaran
  para poder crecer sin cambiar el código.
- Las cooperativas comparten la mensajería; el aislamiento lo da la cooperativa dentro de cada mensaje y el ejecutor por
  cooperativa existente, no una mensajería por cooperativa.
- El sondeo de respaldo puede espaciarse (por ejemplo, de 15 a 60 s) una vez que la mensajería despierta el trabajo; el valor es
  técnico, no de la cooperativa.
- Un navegador no puede imprimir sin su diálogo por decisión de la página; la impresión verdaderamente silenciosa en web depende de
  configurar el equipo (por ejemplo, Chrome con impresión en modo quiosco), que el manual documenta. En la app instalada el ERP la
  hace por la implementación nativa de impresión que ya existe.
- El precalentamiento de la API lo resuelve otra sesión y queda fuera de esta feature.
- El tope de 12 intentos con las esperas actuales cubre unas 7 horas, dentro del plazo de contingencia de 48 h.
- Fuera de alcance: los demás trabajos de fondo (cartera, nómina, correos de notificaciones generales), que siguen como están.
