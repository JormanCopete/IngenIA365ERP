# Feature Specification: Agilidad por teclado — Caja, POS, Factura y toda la aplicación

**Feature Branch**: `claude/agilidad-teclado` (carpeta `specs/022-agilidad-teclado`)

**Created**: 2026-11-08

**Status**: Draft — decisiones del dueño tomadas el 2026-11-08

**Input**: pedido del dueño: «/recaudo/caja, /pos y /ventas/facturas/nueva son de alto uso y hoy no permiten operar de forma
ágil y rápida. Estas opciones y todas las de la aplicación deben permitir un manejo ágil del teclado: confirmar y pasar de
campo con Enter, también con Tab. (1) La búsqueda de clientes ágil y rápida, como la de productos del POS, y mejorar también
la de productos. (2) Mejorar la experiencia de los medios de pago y unificarla: que se comporten igual en todas y permitan
múltiples medios de forma ágil, dinámica y fácil. (3) Estas ventanas deben ser ágiles como las de escritorio.»

## Contexto: lo que se revisó (develop `772195c4`)

- **Buscador de personas** (`PersonSearchPicker`, 60 pantallas): no busca mientras se escribe (el campo de Syncfusion sólo
  avisa al salir o con Enter) y espera 300 ms; los resultados no se recorren con el teclado; el servidor busca la frase entera
  dentro de cada columna («juan perez» no encuentra a Juan Pérez), distingue tildes, no pone primero el documento exacto y
  ordena por apellido. La búsqueda de productos ya resuelve todo eso.
- **Enter** no avanza de campo ni aplica diálogos en ninguna pantalla.
- **Valores** en `type=number`: sin separador de miles; «50.000» puede leerse como 50.
- **Medios de pago**: el POS y la factura usan `PanelDeCobro`; la Caja de Recaudo tiene su propio formulario, con otros
  nombres, otro orden y otros datos (cuenta propia de la transferencia, cheque completo, sin «Entregado»).
- **Caja de Recaudo**: Esc cancela y abandona la operación sin preguntar; cobrar exige cinco clics obligatorios (persona con
  ratón, «Pagar», «Simular», «Agregar medio», «Imputar»); dos buscadores (persona y número); el soporte de la declaración de
  origen de fondos pide escribir el identificador del adjunto.
- **POS**: la base más ágil (campo de lectura, ráfaga del lector, F2…F10), pero los diálogos no aplican con Enter, un código
  desconocido abre la búsqueda vacía, los resultados no traen precio y el efectivo con vueltas o el pago partido piden ratón.
- **Factura de oficina**: nada se cobra ni se ve el total hasta «Guardar»; tras elegir un producto la cantidad se digita con
  clic; las teclas de los medios no existen; no recuerda tipo, bodega ni vendedor; al confirmar sale al detalle.

## Clarifications

### Session 2026-11-08 (formulario del dueño)

- Q1: ¿Alcance? → A: **todo de una vez**: las tres pantallas, los componentes compartidos y el Enter en toda la aplicación.
- Q2: ¿Qué sale primero a producción? → A: **las tres juntas**, en un solo despliegue.
- Q3: ¿El Enter que avanza? → A: **encendido por defecto**; cada usuario puede apagarlo en Preferencias.
- Q4: ¿Enter en el último campo? → A: **lleva al botón principal**; otro Enter lo ejecuta (nadie confirma por accidente).
- Q5: ¿Teclas de función? → A: **base común** — F2 buscar, F10 cobrar y confirmar, Esc volver, Supr quitar —; cada pantalla
  conserva sus demás teclas.
- Q6: ¿Búsqueda de productos en el POS? → A: **en el diálogo F2 mejorado**: con lo escrito ya puesto, el foco en la búsqueda
  y la lista recorrible con flechas.
- Q7: ¿Qué muestra cada producto en la lista? → A: **precio que aplica, disponible en la bodega, unidad y empaques**.
- Q8: ¿Extras del buscador de personas? → A: **personas recientes al entrar al campo; en la Caja, un solo buscador para
  documento, nombre o número del crédito o de la cuenta; crear la persona con una tecla** — y **sin importar mayúsculas ni
  minúsculas** (además de tildes).
- Q9: ¿Hasta dónde se unifican los medios de pago? → A: **experiencia y datos juntos**: ventas pide también la cuenta propia
  de la transferencia o consignación y el cheque completo, como Recaudo.
- Q10: ¿Efectivo? → A: **campo «Recibido» y billetes rápidos** («Exacto» y los billetes del catálogo de denominaciones, con
  tecla).
- Q11: ¿Caja de Recaudo a pantalla completa? → A: **sí, como el POS**.
- Q12: ¿Simular e Imputar en la Caja? → A: **automáticos**: simula al cambiar lo que se paga e imputa al confirmar.
- Q13: ¿Guardado de la factura de oficina? → A: **borrador automático** al agregar o cambiar líneas; total y pagos siempre a
  la vista.
- Q14: ¿Después de confirmar? → A: **quedarse en la pantalla**: Enter abre la siguiente y una tecla imprime.
- Q15 (comentario): **cada campo debe tener una tecla que muestre la explicación de cómo se usa**.
- Q16: La revisión del servidor mostró que unificar los datos de la transferencia toca la contabilidad de ventas. ¿Hasta
  dónde? → A: **completo, como Recaudo**: un solo medio de transferencia o consignación; la cuenta propia se elige entre las
  habilitadas para el punto de venta y la venta se contabiliza contra esa cuenta (con los vínculos de cuenta propia que ya
  usa Recaudo).
- Q17: La custodia de cheques recibidos es de **Recaudo** (no de Tesorería). ¿Cheques en ventas? → A: **datos ahora,
  custodia después**: el panel pide los mismos datos que en Recaudo, se guardan en el pago y exigen cliente identificado; la
  custodia en Recaudo, la anulación y la devolución con deuda del cliente quedan para una fase 2.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Enter avanza en toda la aplicación (Priority: P1)

Quien digita pasa de campo en campo con Enter, como en un programa de escritorio, en cualquier pantalla y diálogo. Tab sigue
funcionando igual.

**Why this priority**: es lo que el dueño pide para «todas las de la aplicación» y lo que más tiempo ahorra en el día a día.

**Independent Test**: en cualquier formulario (p. ej. Maestros › Personas, un diálogo de catálogo), digitar con Enter
recorre los campos en orden; en el último, Enter lleva al botón principal y otro Enter guarda.

**Acceptance Scenarios**:

1. **Given** el foco en un campo de texto, número, fecha o lista, **When** se pulsa Enter, **Then** el foco pasa al siguiente
   campo editable de la misma zona (diálogo o página), saltando los deshabilitados, ocultos y de sólo lectura.
2. **Given** el foco en el último campo de la zona, **When** se pulsa Enter, **Then** el foco va al botón principal; **When**
   se pulsa Enter otra vez, **Then** se ejecuta.
3. **Given** una lista desplegable abierta, **When** se pulsa Enter, **Then** se elige la opción marcada (y, si la preferencia
   lo permite, el foco avanza), sin saltarse la elección.
4. **Given** el foco en un área de texto de varias líneas, una grilla en edición o un buscador con lista abierta, **When** se
   pulsa Enter, **Then** el campo conserva su comportamiento propio (salto de línea, confirmar la fila, elegir el resultado).
5. **Given** Shift+Enter, **Then** el foco vuelve al campo anterior.
6. **Given** un usuario que apagó «Enter avanza» en Preferencias, **Then** Enter vuelve al comportamiento del navegador.
7. **Given** cualquier pantalla, **When** se pulsa Esc, **Then** se cierra lo que esté abierto (lista, ayuda, diálogo) y
   nunca se cancela ni se pierde una operación.

---

### User Story 2 — Ayuda por campo con una tecla (Priority: P1)

Sobre cualquier campo, una tecla muestra la explicación de cómo se usa: qué se escribe, en qué formato, qué teclas sirven ahí.

**Why this priority**: pedido expreso del dueño (Q15); baja la curva de aprendizaje de las cajas.

**Independent Test**: en las tres pantallas, F1 sobre cada campo muestra su texto propio; en otra pantalla cualquiera, F1
muestra la ayuda genérica del campo (etiqueta, formato, teclas).

**Acceptance Scenarios**:

1. **Given** el foco en un campo, **When** se pulsa F1, **Then** aparece junto al campo su explicación, sin mover el foco.
2. **When** se pulsa Esc o F1 otra vez o se sale del campo, **Then** la explicación se cierra.
3. **Given** una pantalla sin texto propio para ese campo, **Then** la ayuda muestra la etiqueta, el formato esperado y las
   teclas que aplican a ese tipo de campo.

---

### User Story 3 — Buscador de personas ágil (Priority: P1)

Buscar al cliente, asociado o pagador se hace como en el POS con los productos: se escribe y la lista aparece; flechas y Enter
eligen.

**Why this priority**: es el primer paso de la caja, del POS (F4) y de la factura; hoy obliga a usar el ratón.

**Independent Test**: en cualquier pantalla con el buscador, escribir «perez juan» (minúsculas, sin tilde) muestra a Juan
Pérez sin salir del campo; ↓ y Enter lo eligen; el número de documento completo + Enter lo elige de una.

**Acceptance Scenarios**:

1. **Given** el foco en el buscador, **When** se escriben 2 o más caracteres, **Then** la lista aparece mientras se escribe,
   sin importar mayúsculas, minúsculas ni tildes, con varias palabras en cualquier orden.
2. **Given** un documento exacto, **Then** esa persona sale primera; **When** se pulsa Enter, **Then** queda elegida.
3. **Given** la lista abierta, **When** se usan ↑ ↓ y Enter, **Then** se elige la marcada; Esc cierra la lista.
4. **Given** el foco en el buscador vacío, **When** se pulsa ↓, **Then** aparecen las personas recientes de ese usuario en
   ese equipo.
5. **Given** una búsqueda sin resultados y permiso para crear, **When** se pulsa la tecla de crear, **Then** se abre el alta
   de persona con el documento ya puesto; al guardar queda elegida.
6. **Given** la Caja de Recaudo, **When** se escribe un número de crédito o de cuenta, **Then** el mismo buscador ofrece ese
   producto con su titular y elegirlo carga lo que puede pagar.

---

### User Story 4 — Un solo panel de medios de pago, igual en todas (Priority: P1)

Cobrar con uno o varios medios se hace igual en el POS, la factura (y la nota crédito y los demás documentos del ciclo) y la
Caja de Recaudo: mismas teclas, mismos nombres, mismo orden y los mismos datos.

**Why this priority**: es el paso donde hoy más se usa el ratón; el dueño pidió unificar experiencia y datos (Q9).

**Independent Test**: cobrar 100.000 con 50.000 en efectivo (recibe 100.000) y 50.000 por transferencia, sin ratón, en las
tres pantallas, con el mismo recorrido de teclas.

**Acceptance Scenarios**:

1. **Given** el panel abierto, **When** se pulsa la tecla rápida de un medio, **Then** se agrega un pago por lo que falta y
   el foco queda en su valor, seleccionado, para partir el pago escribiendo otro valor.
2. **When** se pulsa Enter, **Then** el foco recorre los datos de ese medio en orden fijo (efectivo: recibido; tarjeta:
   datáfono, aprobación, lote, últimos 4; transferencia y consignación: cuenta propia, referencia; cheque: banco, número,
   cuenta, fecha, plaza, girador; bono: número; crédito: cuotas, plazo, periodicidad, primer vencimiento, línea).
3. **Given** el último dato del medio, **When** se pulsa Enter, **Then** si falta dinero el foco vuelve a la barra de medios;
   si no falta, va al botón de confirmar.
4. **Given** efectivo, **When** se escribe lo recibido o se pulsa la tecla de un billete o de «Exacto», **Then** las vueltas
   se ven al instante.
5. **Given** varios pagos, **When** se usan ↑ ↓, **Then** se pasa de uno a otro; **When** se pulsa Supr, **Then** se quita el
   elegido.
6. **Given** una tarjeta, **Then** el datáfono propuesto es el último usado en ese equipo (o el de la caja).
7. **Given** una transferencia o consignación en ventas en un punto con cuentas habilitadas, **Then** se pide la cuenta
   propia donde entró, elegida entre las habilitadas (la única se propone sola), y la venta se contabiliza contra esa cuenta;
   **Given** un cheque en ventas, **Then** se piden los mismos datos que en Recaudo y el cliente debe estar identificado.

---

### User Story 5 — Caja de Recaudo como estación de trabajo (Priority: P1)

La caja trabaja a pantalla completa, como el POS: persona → qué paga → medios → confirmar, todo con teclado.

**Why this priority**: es la pantalla de más uso en las cooperativas y la que hoy tiene más pasos y un Esc peligroso.

**Independent Test**: recaudar la cuota de un crédito y una consignación a una cuenta de ahorro con pago mixto, sin ratón y sin
botones de Simular ni Imputar.

**Acceptance Scenarios**:

1. **Given** la caja abierta, **Then** el foco está en el buscador único (documento, nombre o número).
2. **Given** la persona elegida, **Then** lo que puede pagar aparece como una lista que se recorre con ↑ ↓; Espacio o Enter
   agrega el producto con su valor propuesto; F4 agrega todo lo exigible.
3. **When** cambia lo que se paga (agregar, quitar, valor, modalidad), **Then** la simulación se actualiza sola.
4. **When** se pulsa F10, **Then** el foco va a los medios; **When** se pulsa F10 con los medios completos, **Then** se imputa
   y se confirma en un paso.
5. **When** se pulsa Esc, **Then** se cierra lo abierto; cancelar la operación es otra tecla y siempre pide confirmación.
6. **Given** el recibo confirmado, **Then** Enter abre la siguiente operación y una tecla imprime la tirilla (otra, la carta).
7. **Given** que la operación exige declaración de origen de fondos, **Then** el soporte se adjunta como archivo.

---

### User Story 6 — POS sin ratón de punta a punta (Priority: P1)

El POS conserva su campo de lectura y sus teclas, y ya no pide ratón en diálogos, búsqueda ni cobro.

**Independent Test**: vender tres productos (uno buscado por nombre), cambiar una cantidad, cobrar en efectivo con vueltas y
abrir la venta siguiente, sin ratón.

**Acceptance Scenarios**:

1. **Given** un diálogo (cantidad, descuento, suspender, motivo), **When** se pulsa Enter en su último campo, **Then** lleva
   a «Aplicar»; otro Enter aplica.
2. **Given** un código desconocido o texto escrito, **When** se abre F2, **Then** la búsqueda trae lo escrito y el foco está
   en ella.
3. **Then** cada resultado muestra el precio que aplica a esa venta, el disponible en la bodega, la unidad y los empaques.
4. **Given** F9 (recuperar), **Then** la lista de suspendidas se recorre con ↑ ↓ y Enter recupera.

---

### User Story 7 — Factura de oficina al estilo escritorio (Priority: P1)

La factura se digita en línea, se guarda sola como borrador y se cobra y confirma con teclas.

**Independent Test**: una factura de cinco líneas con pago con tarjeta, sin ratón y sin pulsar «Guardar».

**Acceptance Scenarios**:

1. **Given** un producto elegido, **Then** se agrega la línea y el foco va a su cantidad; Enter pasa a precio, a descuento y
   vuelve al buscador de productos para la siguiente.
2. **When** se agrega o cambia una línea, **Then** el borrador se guarda solo y el total, los impuestos y los pagos se ven sin
   pulsar «Guardar».
3. **Then** F2 busca producto, F4 elige cliente, F10 va a los pagos y luego confirma; Supr quita la línea elegida.
4. **Given** una factura nueva, **Then** tipo, bodega y vendedor vienen como en la última del usuario en ese equipo.
5. **Given** la factura confirmada, **Then** la pantalla se queda: Enter abre una nueva y una tecla la imprime.

### Edge Cases

- El lector de códigos en un buscador: la ráfaga termina en Enter y se trata como lectura exacta (documento o código).
- Listas de Syncfusion abiertas, autocompletados, grillas en edición, editores enriquecidos y áreas de texto: Enter no se
  intercepta mientras la lista está abierta o el campo es de varias líneas.
- La tecla rápida de un medio es una letra o un número (el servidor ya no admite otra cosa): sólo actúa con la barra de
  medios enfocada, así no choca con lo que se digita en los campos ni con las teclas de función.
- Un punto de venta sin cuentas habilitadas para un medio de transferencia: la venta funciona como hoy (contabiliza por el
  medio); con cuentas habilitadas, la cuenta es obligatoria.
- Una cuenta propia habilitada sin vínculo contable: la venta no se confirma y lo dice, igual que Recaudo.
- Un cheque en una venta al consumidor final: se rechaza; el cheque exige cliente identificado.
- Una venta con cheque que se anula: en esta entrega el cheque no está en custodia (fase 2); la anulación sigue como hoy.
- El borrador automático de la factura choca con otra edición (versión de fila): se recarga y se avisa, sin perder lo digitado.
- Latencia: una escritura del borrador en curso no bloquea la digitación; la siguiente se encola.
- MAUI: el mismo comportamiento (el Shared y su JavaScript son los mismos).
- Teclas del navegador que no se tocan: F5 (recargar), F11 (pantalla completa), F12 (herramientas).

## Requirements *(mandatory)*

### Functional Requirements

**Teclado en toda la aplicación**

- **FR-001**: Enter MUST mover el foco al siguiente campo editable de la misma zona (diálogo abierto o página) y Shift+Enter
  al anterior, saltando deshabilitados, ocultos y de sólo lectura. Tab no cambia.
- **FR-002**: Enter en el último campo de la zona MUST llevar el foco al botón principal de esa zona; Enter sobre un botón lo
  ejecuta (Q4).
- **FR-003**: Enter MUST NOT interceptarse en áreas de texto, editores enriquecidos, grillas en edición, listas desplegables o
  autocompletados abiertos y buscadores con lista propia, ni en campos marcados como de comportamiento propio.
- **FR-004**: La preferencia «Enter pasa al siguiente campo» MUST existir por usuario, encendida por defecto, en Preferencias
  (Q3), y aplicarse en web y MAUI.
- **FR-005**: Al entrar a un campo de texto o de valor por teclado, su contenido MUST quedar seleccionado.
- **FR-006**: Esc MUST cerrar lo abierto (lista, ayuda, diálogo) y MUST NOT cancelar ni abandonar una operación.
- **FR-007**: Los valores en pesos MUST digitarse con separador de miles visible y sin ambigüedad entre separador de miles y
  decimal.

**Ayuda por campo**

- **FR-010**: F1 sobre un campo MUST mostrar su explicación junto al campo sin mover el foco; Esc, F1 o salir la cierran.
- **FR-011**: Los campos de las tres pantallas y de los componentes compartidos (buscadores, panel de medios) MUST tener texto
  de ayuda propio; los demás MUST mostrar una ayuda genérica (etiqueta, formato y teclas del tipo de campo).
- **FR-012**: Las tres pantallas MUST mostrar siempre su barra de atajos.

**Buscador de personas**

- **FR-020**: MUST buscar mientras se escribe (desde 2 caracteres), cancelando la búsqueda anterior.
- **FR-021**: MUST ignorar mayúsculas, minúsculas y tildes, y aceptar varias palabras en cualquier orden (cada palabra debe
  estar en el documento, los nombres, los apellidos, la razón social o el código).
- **FR-022**: MUST ordenar: documento exacto, documento que empieza por lo escrito, nombre que empieza por lo escrito, resto.
- **FR-023**: MUST recorrerse con ↑ ↓, elegir con Enter y cerrar con Esc; un documento exacto + Enter elige sin pasar por la
  lista.
- **FR-024**: MUST ofrecer las personas recientes del usuario en ese equipo al pulsar ↓ con el campo vacío.
- **FR-025**: Sin resultados y con `Core.People.Create`, una tecla MUST abrir el alta con el documento escrito; al guardar
  queda elegida. Sin el permiso, dice a quién pedirla.
- **FR-026**: En la Caja de Recaudo el mismo buscador MUST aceptar el número de un crédito o de una cuenta y ofrecer ese
  producto con su titular.
- **FR-027**: El componente MUST seguir siendo el único buscador de personas (constitución) y conservar su contrato para las
  60 pantallas que lo usan.

**Buscador de productos**

- **FR-030**: MUST mostrar en cada resultado el precio que aplica en el contexto (cliente, canal, punto o sucursal, fecha), el
  disponible en la bodega, la unidad base y los empaques (Q7).
- **FR-031**: Abrir la búsqueda desde un código desconocido o con texto escrito MUST traer ese texto y dejar el foco en ella.
- **FR-032**: En la factura, elegir un producto MUST agregar la línea y llevar el foco a su cantidad.

**Panel único de medios de pago**

- **FR-040**: POS, factura de oficina, nota crédito, documentos del ciclo y Caja de Recaudo MUST usar el mismo panel, con los
  mismos nombres de campo y el mismo orden por clase de medio.
- **FR-041**: La tecla rápida de un medio MUST agregar un pago por lo que falta y dejar su valor seleccionado.
- **FR-042**: Enter MUST recorrer los datos del medio en orden; al terminar, MUST volver a la barra de medios si falta dinero o
  ir a confirmar si no.
- **FR-043**: El efectivo MUST tener «Recibido», «Exacto» y los billetes activos del catálogo de denominaciones con tecla, y
  mostrar las vueltas al instante.
- **FR-044**: ↑ ↓ MUST mover entre pagos y Supr quitar el elegido.
- **FR-045**: El datáfono propuesto MUST ser el último usado en el equipo (si sigue disponible) o el de la caja.
- **FR-046**: En ventas, las cuentas propias de recaudo de Tesorería MUST poder habilitarse por punto de venta (y,
  opcionalmente, por caja) para cada medio de transferencia o consignación, como en Recaudo.
- **FR-047**: En ventas, si el punto tiene cuentas habilitadas para el medio, el pago MUST registrar la cuenta propia donde
  entró (la única se propone sola), validarse al guardar y al confirmar, y la venta MUST contabilizarse contra esa cuenta por
  su vínculo contable; sin cuentas habilitadas, la venta sigue como hoy (contabiliza por el medio) (Q16).
- **FR-048**: En ventas, un cheque MUST registrar banco librado, número, cuenta, fecha, misma plaza y girador, y exigir
  cliente identificado; la custodia en Recaudo queda para la fase 2 (Q17).
- **FR-049**: La tecla rápida de un medio MUST ser una letra o un número y actuar sólo con la barra de medios enfocada; el
  catálogo de medios pide un solo carácter.
- **FR-04A**: En Recaudo, la tarjeta MUST pedir también los últimos cuatro dígitos (opcional), como en ventas.
- **FR-04B**: El catálogo de billetes MUST poder leerlo quien cobra (POS, factura o caja), no sólo quien administra medios.

**Caja de Recaudo**

- **FR-050**: MUST trabajar a pantalla completa, con su barra de contexto (punto, caja, cajero, fecha) y de atajos (Q11).
- **FR-051**: Lo que la persona puede pagar MUST ser una lista recorrible; Espacio o Enter agrega; F4 agrega todo lo exigible.
- **FR-052**: La simulación MUST actualizarse sola al cambiar lo que se paga (Q12); la imputación MUST hacerse al confirmar.
- **FR-053**: F10 MUST llevar a los medios y, con los medios completos, confirmar.
- **FR-054**: Cancelar la operación MUST ser una tecla propia con confirmación.
- **FR-055**: Tras el recibo, Enter MUST abrir la siguiente operación; una tecla imprime la tirilla y otra la carta.
- **FR-056**: El soporte de la declaración de origen de fondos MUST adjuntarse como archivo (los adjuntos admiten ese dueño,
  con el permiso de quien registra recibos).

**POS**

- **FR-060**: Base común de teclas (Q5) más las propias que ya tiene (F3, F4, F6, F7, F8, F9, Ctrl+Alt+M/C/R/D).
- **FR-061**: Los diálogos MUST aplicarse con Enter según FR-002.
- **FR-062**: La lista de suspendidas MUST recorrerse con ↑ ↓ y Enter.

**Factura de oficina**

- **FR-070**: El borrador MUST guardarse solo al agregar o cambiar líneas o cabecera, sin bloquear la digitación (Q13).
- **FR-071**: Total, impuestos y pagos MUST verse siempre que haya borrador.
- **FR-072**: F2 producto, F4 cliente, F10 pagos y confirmar, Supr quitar línea.
- **FR-073**: Tipo, bodega y vendedor MUST proponerse como en la última factura del usuario en ese equipo.
- **FR-074**: Tras confirmar, la pantalla MUST quedarse con el resultado: Enter abre una nueva, una tecla imprime (Q14).

### Key Entities

- **Preferencia del usuario**: gana «Enter pasa al siguiente campo» (sí/no).
- **Persona**: gana un texto de búsqueda normalizado (sin tildes, en mayúsculas) con el documento, nombres, apellidos, razón
  social y código, mantenido al crear y editar.
- **Cuenta habilitada para transferencias en un punto de venta**: punto, caja opcional, medio y cuenta propia de recaudo
  (con su rótulo), como la de Recaudo para sus puntos.
- **Pago de un documento de venta**: gana la cuenta propia (transferencia, consignación) y los datos del cheque.
- **Mensaje de venta a Contabilidad**: una versión nueva cuyo pago lleva la cuenta propia.
- **Medio de un recibo de Recaudo**: gana los últimos cuatro dígitos de la tarjeta.

## Success Criteria *(mandatory)*

- **SC-001**: Una venta POS de tres productos (uno buscado por nombre) con efectivo y vueltas se completa sin ratón.
- **SC-002**: Un recaudo de una cuota con pago mixto (efectivo + transferencia) se completa sin ratón y sin botones de simular
  ni imputar.
- **SC-003**: Una factura de oficina de cinco líneas con tarjeta se completa sin ratón y sin «Guardar».
- **SC-004**: «perez juan», «PÉREZ» o «Perez» encuentran a Juan Pérez; el documento exacto sale primero.
- **SC-005**: La búsqueda de personas responde en menos de 1 segundo con 100.000 personas.
- **SC-006**: F1 muestra la ayuda en todos los campos de las tres pantallas.
- **SC-007**: Ninguna operación de caja se pierde por pulsar Esc.
- **SC-008**: Las 60 pantallas con buscador de personas y las 20 con buscador de productos siguen funcionando sin cambios en
  su código.

## Assumptions

- No hay pruebas de navegador en el repositorio: el teclado y el interop se prueban a mano en DEV y QA con una lista de
  comprobación; la lógica (cobro, normalización, orden de búsqueda) con pruebas unitarias.
- F5, F11 y F12 quedan para el navegador.
- Las pantallas fuera de las tres reciben el Enter y la ayuda genérica; sus textos propios se escriben después.
- El despliegue es uno solo (Q2) e incluye una migración aditiva por proveedor.
