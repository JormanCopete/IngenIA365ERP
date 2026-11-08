# Feature Specification: Canal digital del asociado y pagos en línea

**Feature Branch**: `021-canal-asociados-pagos`

**Created**: 2026-11-08

**Status**: Draft

**Input**: User description: "Complemento del ERP: una PWA y una app de tiendas para que los asociados de cada
cooperativa ingresen con documento de identidad y clave, vean sus conceptos de cartera y los paguen por una
pasarela; el pago aprobado notifica al ERP, que hace el asiento contable y los demás procesos internos. Primero un
proceso por entidad que decide qué información y qué conceptos ve cada asociado y, según la configuración de la
empresa, a qué pasarela y cuentas lleva cada concepto." (descripción completa y decisiones del dueño del 2026-11-08
en la sección **Decisiones del dueño**).

## Contexto

Hasta hoy el asociado no tiene canal propio: la 013 (D1) y la 014 (D5) decidieron «sin portal del asociado» y la 015
(C2, FR-091) dejó los pagos en línea **sólo por archivo** cargado por una persona, con la opción explícita de «un
botón de pago con la pasarela que el dueño elija en una entrega posterior». Esta feature es esa entrega y **revoca
D1 de la 013 y D5 de la 014** para lo que aquí se define; no cambia ningún contrato existente de Cartera,
Captaciones, Recaudo ni Tesorería: los usa.

Piezas que ya existen y esta feature reutiliza:

- **Recaudo (015)**: convenios de recaudo de clase pasarela, referencias de pago, recibos por canal, archivos de
  pasarela, partidas por identificar y conciliación del convenio contra el extracto.
- **Contrato de obligaciones (Cartera, 013)** y **contrato de cuentas (Captaciones, 014)**: la única forma de
  aplicar un pago a una deuda o de depositar en una cuenta, con su prelación y su asiento.
- **Contabilidad (009)**: un solo camino al libro; lo contabilizado no se edita, se reversa.
- **Servicios por vehículo (020)**: cobros mensuales que la entidad puede ofrecer como «otro cobro» pagable.

## Glosario

- **Entidad**: una cooperativa cliente de la plataforma (una base de datos propia).
- **Canal digital**: la PWA y la app de tiendas para asociados, con su propio servicio y su propia base, separados
  del ERP.
- **Cuenta del canal**: la identidad de una persona en el canal digital, única en toda la plataforma y anclada a
  su tipo y número de documento. No es un usuario del ERP y no tiene roles del ERP.
- **Habilitación**: la decisión de una entidad de que una persona suya pueda usar el canal. Es por entidad y
  tiene estado (habilitada / inhabilitada) y fecha.
- **Entidad validadora**: la **primera entidad que habilitó** a la persona y que sigue habilitándola; sus datos de
  contacto reciben el código de activación.
- **Concepto pagable**: lo que la entidad publica en el canal para ver y pagar: una obligación de crédito, la
  cuota de aportes, la cuota de un ahorro contractual o programado, u otro cobro o convenio.
- **Destino de pago**: el sitio de pasarela, el convenio de recaudo y la cuenta contable/bancaria a la que va un
  concepto.
- **Intento de pago**: una solicitud de pago del asociado ante la pasarela, con su referencia única y su estado
  (pendiente, aprobado, rechazado, fallido, vencido).
- **Aviso de pago**: la notificación del canal al ERP de que un intento quedó aprobado.

## Clarifications

### Session 2026-11-08

- Q: ¿Cuánto tiempo sin actividad cierra la sesión del asociado y cuál es la duración máxima? → A: 5 minutos de
  inactividad (aviso al minuto 4) y 8 horas de duración máxima; cada entidad puede subir la inactividad hasta 15.
- Q: En un abono anticipado a un crédito, ¿quién decide si reduce el plazo o la cuota? → A: el asociado elige al
  pagar, con la opción por defecto de la entidad preseleccionada; la elección viaja con el aviso y Cartera la aplica.
- Q: ¿Qué hace el ERP por defecto con un pago aprobado que excede lo debido o cuya obligación ya se canceló? → A: si
  la obligación sigue viva, el excedente queda como anticipo a sus próximas cuotas; si no hay deuda, queda como saldo
  a favor del asociado para que Recaudo lo devuelva o reubique con su autorización. Cada entidad puede cambiarlo.
  (El dueño respondió «partida por identificar» y el 2026-11-08 confirmó «saldo a favor del asociado»: el pagador
  está identificado y es la figura del ERP para ese caso.)
- Q: ¿Por dónde llegan los códigos de un solo uso? → A: correo siempre; SMS opcional por un proveedor que contrata
  la plataforma, que cada entidad enciende y se le factura por uso; el asociado elige entre sus datos verificados.
- Q: Si el asociado cierra su cuenta o pide suprimir sus datos, ¿qué se borra y qué se conserva? → A: por ahora no
  hay opción de cerrar ni inactivar la cuenta desde el canal; queda fuera del alcance de esta feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - La entidad configura su canal (Priority: P1)

El administrador de la entidad, desde el ERP, enciende el canal digital y decide qué ve y qué paga cada asociado:
qué conceptos se publican (por línea de crédito, aportes, cada modalidad de ahorro contractual o programado, cada
otro cobro o convenio), qué montos admite cada concepto (exigible, parcial con mínimo, anticipado o total, varios
conceptos en un solo pago), a qué destino de pago va cada concepto, qué medios ofrece (PSE, tarjeta de crédito),
su marca (nombre, logo y colores), sus textos legales (términos y condiciones, política de tratamiento de datos,
preguntas frecuentes, contacto de soporte), cuánto dura la sesión de pago, si exige código al pagar y qué personas
quedan habilitadas.

**Why this priority**: sin configuración por entidad no hay nada que mostrar ni a dónde llevar el dinero; es el
«proceso primero» que pidió el dueño.

**Independent Test**: con una entidad de prueba, configurar dos conceptos con destinos distintos, habilitar a tres
personas e inhabilitar a una; consultar desde el canal qué ve cada una.

**Acceptance Scenarios**:

1. **Given** una entidad sin canal, **When** el administrador lo enciende sin destino de pago configurado,
   **Then** el canal queda en modo «sólo consulta» y lo informa: no se ofrece ningún botón de pago.
2. **Given** un concepto de aportes publicado con «pago parcial, mínimo $20.000», **When** el administrador lo
   guarda, **Then** el canal lo ofrece con ese mínimo y sin las otras modalidades.
3. **Given** credenciales de pasarela capturadas para la entidad, **When** el administrador las guarda, **Then**
   quedan cifradas, nunca se vuelven a mostrar completas y se verifica la conexión con la pasarela antes de activarlas.
4. **Given** la política «habilitar a todo asociado activo», **When** un asociado se retira en el ERP, **Then**
   su habilitación pasa a inhabilitada automáticamente con el motivo «retiro».
5. **Given** una persona habilitada, **When** el administrador la inhabilita con motivo, **Then** la persona deja de
   ver toda la información de esa entidad en su próxima petición, sin afectar las demás entidades.

---

### User Story 2 - El asociado activa su cuenta e ingresa (Priority: P1)

La primera vez, el asociado digita su tipo y número de documento. Si alguna entidad lo tiene habilitado, el canal
envía un código al correo o celular que la **entidad validadora** tiene registrados, el asociado lo digita, crea su
clave y acepta los términos y la política de datos. Después ingresa con documento y clave; en un dispositivo nuevo
se le pide además un código; en la app puede desbloquear con huella o rostro. Si está habilitado en varias
entidades, elige con cuál continuar; con una sola, entra directo. Ya activo, cambia su correo y su celular desde el
canal.

**Why this priority**: es la puerta; nada más funciona sin una identidad confiable y aislada del ERP.

**Independent Test**: una persona habilitada en dos entidades activa su cuenta con el código enviado a los datos de
la primera, ingresa, ve el selector, elige; luego la segunda la inhabilita y el selector desaparece.

**Acceptance Scenarios**:

1. **Given** un documento no habilitado en ninguna entidad, **When** se intenta activar, **Then** el canal muestra
   el mismo mensaje que para uno habilitado («si el documento está habilitado, enviamos un código a los datos que
   tu entidad tiene registrados») y no envía nada: no revela si la persona existe.
2. **Given** una persona habilitada cuya entidad validadora no tiene correo ni celular, **When** intenta activar,
   **Then** el canal le indica que se acerque a su entidad para actualizar sus datos de contacto, con el contacto de
   soporte de esa entidad.
3. **Given** una cuenta activa, **When** el asociado falla la clave el número de veces configurado, **Then** la
   cuenta se bloquea temporalmente y se le avisa por los datos de la cuenta.
4. **Given** una cuenta activa habilitada en dos entidades, **When** ingresa, **Then** ve el selector con las dos
   y la marca de cada una; al elegir, toda la sesión queda acotada a esa entidad.
5. **Given** una cuenta activa, **When** cambia su celular, **Then** el canal verifica el nuevo con un código,
   avisa al anterior, y el ERP de cada entidad ve la novedad sin que se sobrescriba la persona.
6. **Given** una persona inhabilitada en todas sus entidades, **When** ingresa, **Then** puede entrar pero ve
   «no tienes entidades activas en el canal» y no ve información de ninguna.

---

### User Story 3 - El asociado consulta sus conceptos (Priority: P1)

Dentro de la entidad elegida, el asociado ve sus conceptos publicados: por cada crédito, el saldo, lo vencido, la
cuota del período, la próxima fecha de pago y el valor exigible a hoy; sus aportes y su cuota; sus ahorros
contractuales o programados con la cuota pactada; y los otros cobros pendientes. Todos los valores los calcula el
ERP a la fecha de la consulta.

**Why this priority**: es lo que el asociado más pide y la base del pago; entrega valor aunque el pago no esté
listo (modo sólo consulta).

**Independent Test**: comparar lo que muestra el canal para un asociado con lo que muestra el ERP en sus pantallas
de Cartera y Captaciones a la misma fecha.

**Acceptance Scenarios**:

1. **Given** un crédito en mora, **When** el asociado lo consulta, **Then** ve el vencido, los intereses de mora y
   el exigible a hoy iguales a los del ERP, y la fecha a la que están calculados.
2. **Given** una línea de crédito no publicada por la entidad, **When** el asociado tiene un crédito de esa línea,
   **Then** el canal no lo muestra.
3. **Given** el ERP de la entidad no disponible, **When** el asociado consulta, **Then** el canal informa que la
   información de esa entidad no está disponible en este momento y no muestra cifras viejas como actuales.

---

### User Story 4 - El asociado paga en línea (Priority: P1)

El asociado elige uno o varios conceptos, decide el valor de cada uno dentro de lo que la entidad permite, ve un
resumen con el total, el desglose y el logo de la pasarela, acepta los términos y la política de datos, confirma
(con código si la entidad lo exige) y es llevado a la pasarela, donde paga por PSE o tarjeta. Al volver, el canal
consulta el estado y le muestra el resultado: aprobado, rechazado, pendiente o fallido, con referencia, fecha, valor
y autorización.

**Why this priority**: es el objetivo del dueño y lo que habilita el recaudo en línea de todas las entidades.

**Independent Test**: en el ambiente de pruebas de la pasarela, pagar una cuota por PSE con cada estado simulado
(aprobada, pendiente, declinada, fallida) y verificar lo que ve el asociado y lo que queda registrado.

**Acceptance Scenarios**:

1. **Given** un carrito con una cuota de crédito y la cuota de aportes del mismo destino, **When** el asociado
   confirma, **Then** se crea **un** intento con una referencia única y el total enviado a la pasarela es igual al
   total del resumen.
2. **Given** un carrito con conceptos de **dos** destinos distintos, **When** el asociado confirma, **Then** el
   canal le explica que se harán dos pagos separados, uno por destino, y los lleva uno a la vez.
3. **Given** el asociado oprime «Pagar» varias veces, **When** se procesa, **Then** se crea un solo intento.
4. **Given** un intento pendiente sobre un concepto, **When** el asociado intenta pagarlo otra vez, **Then** el
   canal le muestra el aviso de pago pendiente con referencia, valor, autorización y el contacto de la entidad, y
   no crea otro intento hasta que el primero tenga estado final.
5. **Given** un pago parcial por debajo del mínimo configurado, **When** se intenta confirmar, **Then** el canal lo
   rechaza antes de ir a la pasarela con el mínimo en el mensaje.
6. **Given** un intento creado, **When** pasa el tiempo de sesión configurado (entre 10 y 30 minutos) sin pago,
   **Then** el intento queda vencido y el asociado puede iniciar otro.
7. **Given** el asociado cierra la app en la pasarela, **When** la pasarela aprueba el pago, **Then** el estado
   final llega por notificación de la pasarela o, si no llega, por la consulta periódica de pendientes, y el
   asociado lo ve al volver.

---

### User Story 5 - El ERP aplica el pago aprobado (Priority: P1)

Cuando un intento queda aprobado, el canal avisa al ERP de la entidad. El ERP lo registra como un recibo de canal
«en línea» en Recaudo, contra el convenio de pasarela del destino, y lo aplica: a la obligación por el contrato de
obligaciones (con su prelación), a la cuenta de ahorro por el contrato de cuentas, o al otro cobro por su módulo
dueño. Contabiliza contra la cuenta puente de la pasarela y deja el comprobante disponible para el asociado.

**Why this priority**: sin aplicación el pago no existe para la cooperativa; es la otra mitad del objetivo.

**Independent Test**: aprobar un pago en el ambiente de pruebas y verificar en el ERP el recibo, la aplicación por
prelación, el asiento y el comprobante; repetir el mismo aviso y verificar que no cambia nada.

**Acceptance Scenarios**:

1. **Given** un aviso de pago aprobado, **When** el ERP lo recibe dos veces (o más), **Then** lo aplica una sola
   vez y responde lo mismo a cada repetición.
2. **Given** un aviso cuya firma no corresponde, **When** llega al ERP, **Then** se rechaza, se audita y no se
   aplica nada.
3. **Given** una obligación que se canceló por otro canal entre la consulta y el pago, **When** llega el aviso,
   **Then** el valor no se pierde: queda como saldo a favor del asociado con el motivo «obligación cancelada»
   (política por defecto, FR-013) y el asociado ve «pago recibido, en revisión».
4. **Given** un intento rechazado o fallido, **When** termina, **Then** no se crea nada en el ERP.
5. **Given** un pago aplicado, **When** el asociado abre su historial, **Then** descarga el comprobante emitido por
   el ERP con la referencia de la pasarela.

---

### User Story 6 - Recaudo concilia el canal cada día (Priority: P2)

Cada día, el analista de Recaudo carga (u obtiene) el reporte de la pasarela y el extracto bancario del destino; el
ERP cuadra los pagos aplicados en línea contra lo reportado por la pasarela y contra el abono en el banco
(descontando comisiones), aplica los aprobados que no llegaron a avisarse y deja cada diferencia como partida por
identificar con su motivo.

**Why this priority**: el canal en línea no puede quedar fuera del cuadre diario; es la contrapartida de aplicar
sin revisión previa.

**Independent Test**: preparar un día con un pago aplicado, uno aprobado no avisado y un abono con comisión;
conciliar y verificar el resultado y las partidas.

**Acceptance Scenarios**:

1. **Given** un pago aprobado en la pasarela que nunca se avisó, **When** se concilia el día, **Then** se aplica
   con su referencia, una sola vez, y queda marcado como «aplicado por conciliación».
2. **Given** un abono bancario menor que la suma de los pagos por la comisión de la pasarela, **When** se
   concilia, **Then** la comisión se reconoce según la configuración del destino y no queda como diferencia.
3. **Given** una diferencia sin explicación, **When** se concilia, **Then** queda como partida por identificar y
   el día no se marca como cuadrado.

---

### User Story 7 - Reverso de un pago del canal (Priority: P2)

Un funcionario autorizado reversa un pago del canal (error del asociado, pago doble, solicitud de devolución). Antes
del corte de conciliación de la pasarela del día, el reverso va por la pasarela y el ERP reversa la aplicación por
el módulo dueño; después del corte, se registra como devolución por Tesorería.

**Why this priority**: la certificación lo exige definido y los pagos dobles ocurren.

**Independent Test**: reversar un pago aprobado antes y después del corte y verificar los dos caminos en el ERP y
el estado que ve el asociado.

**Acceptance Scenarios**:

1. **Given** un pago aplicado del día, **When** se reversa antes del corte, **Then** la pasarela lo reversa, el ERP
   reversa la aplicación con su asiento espejo y el asociado ve el pago como «reversado».
2. **Given** un pago de un día anterior, **When** se pide reversarlo, **Then** el ERP indica que ya pasó el corte y
   lo lleva a una devolución por Tesorería con motivo.

---

### User Story 8 - Historial y comprobantes (Priority: P2)

El asociado ve el historial de sus pagos por entidad, del más reciente al más antiguo: fecha y hora, referencia,
autorización o CUS, estado y valor con su moneda; y descarga el comprobante de cada pago aplicado.

**Why this priority**: lo exige la certificación (al menos las últimas cinco transacciones) y evita llamadas a la
entidad.

**Independent Test**: tras pagos con distintos estados, verificar el orden, los campos y la descarga.

**Acceptance Scenarios**:

1. **Given** seis intentos con estados distintos, **When** el asociado abre el historial, **Then** ve los seis en
   orden descendente con todos los campos.

---

### User Story 9 - Extractos y certificados (Priority: P2)

El asociado descarga, para la entidad elegida, el extracto de cada crédito, el extracto de sus ahorros y aportes,
el certificado de aportes y el certificado para la declaración de renta, generados por el ERP con los mismos
datos que la entidad entrega en oficina.

**Why this priority**: es la segunda razón por la que el asociado va a la oficina.

**Independent Test**: comparar el PDF descargado desde el canal con el generado desde el ERP para la misma persona
y fecha.

**Acceptance Scenarios**:

1. **Given** un asociado con dos créditos, **When** pide el extracto de uno, **Then** recibe sólo ese y la descarga
   queda auditada en la entidad.
2. **Given** un certificado que la entidad no publica, **When** el asociado lo busca, **Then** no aparece.

---

### User Story 10 - Notificaciones (Priority: P3)

El canal avisa al asociado por notificación en la app y por correo: cuota próxima a vencer (con la anticipación que
configure la entidad), pago aplicado, pago rechazado y pago pendiente resuelto. El asociado puede apagar los avisos
que no son de seguridad.

**Why this priority**: mejora el recaudo oportuno, pero el canal funciona sin ella.

**Independent Test**: con una cuota que vence en la anticipación configurada, verificar el aviso; apagarlo y
verificar que no llega.

**Acceptance Scenarios**:

1. **Given** una cuota que vence en 3 días y la entidad configuró 3, **When** corre el aviso diario, **Then** el
   asociado recibe un aviso con el valor y un acceso directo a pagarla.
2. **Given** un aviso de seguridad (cambio de clave, dispositivo nuevo), **When** el asociado apagó los avisos,
   **Then** el aviso de seguridad igual llega.

---

### User Story 11 - App de tiendas (Priority: P3)

El asociado instala una sola app desde Play Store o App Store, con la misma experiencia que la PWA, y suma el
desbloqueo con huella o rostro, las notificaciones en el teléfono y el almacenamiento seguro de su sesión.

**Why this priority**: la PWA ya cubre el uso; la app suma confianza y avisos.

**Independent Test**: instalar la app, ingresar, activar biometría, pagar y recibir el aviso de pago aplicado.

**Acceptance Scenarios**:

1. **Given** la app instalada con biometría activa, **When** el asociado la abre, **Then** desbloquea con huella o
   rostro sin digitar la clave, mientras su sesión siga vigente.
2. **Given** la pasarela abre la interfaz del banco, **When** el asociado termina, **Then** vuelve a la app con el
   resultado consultado.

---

### User Story 12 - La entidad atiende y supervisa el canal (Priority: P2)

Un funcionario de la entidad, desde el ERP, consulta las cuentas del canal de sus asociados (estado, último
ingreso, dispositivos), sus intentos de pago y su estado, desbloquea una cuenta, cierra sesiones, y ve los
indicadores del canal (activaciones, pagos, valor recaudado, rechazos).

**Why this priority**: sin soporte la entidad no puede responder la llamada «pagué y no aparece».

**Independent Test**: con un intento pendiente, el funcionario lo encuentra por documento o referencia y ve su
estado en la pasarela.

**Acceptance Scenarios**:

1. **Given** un asociado que llama por un pago, **When** el funcionario busca por referencia o documento, **Then**
   ve el intento, su estado en la pasarela y, si se aplicó, el recibo del ERP.
2. **Given** un funcionario de la entidad A, **When** consulta, **Then** nunca ve datos de la persona en la
   entidad B, ni sabe si existe allí.

---

### User Story 13 - Simulador y solicitud de crédito (Priority: P4)

El asociado simula un crédito con las líneas que la entidad publica (monto, plazo, cuota estimada) y envía una
solicitud que llega al ERP como solicitud de crédito para que la entidad la estudie; luego ve su estado.

**Why this priority**: lo pidió el dueño para la feature, en una entrega posterior a pagar.

**Independent Test**: simular, enviar, y verificar en el ERP la solicitud con los datos y el canal de origen.

**Acceptance Scenarios**:

1. **Given** una línea publicada con tasa y plazos, **When** el asociado simula, **Then** la cuota coincide con la
   del simulador del ERP para los mismos datos.
2. **Given** una solicitud enviada, **When** la entidad la estudia, **Then** el asociado ve el estado (radicada, en
   estudio, aprobada, negada) sin ver las notas internas.

---

### Edge Cases

- **Persona en varias entidades con la validadora inhabilitada antes de activar**: valida la siguiente entidad
  por fecha de habilitación. Después de activada, la cuenta no depende de ninguna entidad en particular.
- **Persona sin entidades activas**: ingresa y no ve información; puede cambiar sus datos de contacto y su clave. No
  puede cerrar ni inactivar su cuenta desde el canal (fuera de alcance).
- **Mismo documento con tipos distintos** (CC y CE): son cuentas distintas; el canal no las fusiona solo.
- **Cambio de documento en el ERP de una entidad**: la habilitación queda huérfana hasta que la entidad corrija; el
  canal no muestra información de una persona cuyo documento ya no coincide.
- **Aviso que llega antes de que el asociado vuelva de la pasarela**: el estado del intento lo decide la consulta a
  la pasarela; notificación, consulta al volver y sonda convergen en el mismo estado final sin duplicar.
- **Pasarela aprueba después de que el intento venció en el canal**: prima el estado de la pasarela; el intento
  pasa a aprobado y se aplica.
- **Reintento dentro de la misma sesión de pasarela con un estado distinto**: el canal muestra el estado del último
  pago de la sesión.
- **Pago aprobado parcialmente por la pasarela**: no aplica a esta feature (no se usa pago mixto); si llega, se trata
  como diferencia de conciliación.
- **Obligación que cambia entre la consulta y el pago** (otro pago, causación del día): el ERP aplica con los
  valores del momento de la aplicación; el excedente sigue la política de la entidad.
- **Concepto con IVA** (servicios de otros cobros gravados): el pago lleva el desglose de base, tipo y valor del
  impuesto, y se muestra al asociado antes de pagar.
- **ERP no disponible al llegar el aviso**: el canal reintenta con espera creciente; el pago nunca se da por perdido
  y la conciliación diaria es la red final.
- **Credenciales de pasarela vencidas o revocadas**: la entidad pasa a «sólo consulta» con alerta al administrador;
  los intentos pendientes se siguen consultando.
- **Entidad que apaga el canal con intentos pendientes**: los pendientes se resuelven y aplican; no se crean nuevos.
- **Comprador distinto del titular del medio de pago**: el canal envía los datos del asociado como comprador; la
  pasarela pide los del titular.

## Requirements *(mandatory)*

### Functional Requirements

#### Configuración del canal por entidad

- **FR-001**: Cada entidad MUST poder encender y apagar el canal digital; apagado, ninguna persona de la entidad ve
  su información en el canal y los pendientes se siguen resolviendo.
- **FR-002**: La entidad MUST poder publicar conceptos pagables por tipo y subtipo: por línea de crédito, aportes
  sociales, cada modalidad de ahorro contractual o programado, y cada otro cobro o convenio (incluidos los servicios
  por vehículo de la 020). Lo no publicado no se muestra.
- **FR-003**: Por cada concepto, la entidad MUST elegir las modalidades de monto que admite: valor exigible, pago
  parcial con mínimo, abono anticipado o pago total, y si puede ir con otros conceptos en un solo pago.
- **FR-004**: Por cada concepto, la entidad MUST asignar un destino de pago (sitio de pasarela, convenio de recaudo
  de clase pasarela y cuenta contable puente). Un concepto sin destino se muestra sólo para consulta.
- **FR-005**: La entidad MUST registrar uno o varios sitios de pasarela con sus credenciales; las credenciales se
  guardan cifradas, nunca se muestran completas tras guardarse, nunca salen en registros ni auditoría, y se
  verifican contra la pasarela antes de quedar activas.
- **FR-006**: La entidad MUST elegir los medios de pago ofrecidos (PSE, tarjeta de crédito) por sitio.
- **FR-007**: La entidad MUST configurar su marca (nombre visible, logo, colores) y sus textos: términos y
  condiciones, política de tratamiento de datos, preguntas frecuentes (incluidas las de la pasarela, con su nombre
  y datos de contacto) y contacto de soporte (teléfono, correo y horario).
- **FR-008**: La entidad MUST configurar el tiempo de vencimiento de la sesión de pago, entre 10 y 30 minutos
  (valor por defecto 15).
- **FR-009**: La entidad MUST poder exigir un código de verificación al confirmar un pago (por defecto: no).
- **FR-010**: La entidad MUST elegir la política de habilitación: (a) todo asociado activo queda habilitado y se
  inhabilita al retirarse; (b) sólo las personas que habilite un funcionario, una a una o por carga masiva; o (c)
  las dos. Puede además habilitar a no asociados con conceptos pagables (ex asociados con saldo, deudores de otros
  cobros).
- **FR-011**: Un funcionario con permiso MUST poder inhabilitar y volver a habilitar a una persona con motivo; cada
  cambio queda auditado con fecha y autor.
- **FR-011a**: Los códigos de un solo uso MUST poder enviarse siempre por correo; el envío por SMS usa un proveedor
  que contrata la plataforma, cada entidad decide si lo enciende y su uso se registra por entidad para facturárselo.
  Cuando hay más de un canal verificado, el asociado elige por cuál recibir el código.
- **FR-012**: La entidad MUST configurar la anticipación de los avisos de cuota próxima y qué extractos y
  certificados publica.
- **FR-013**: La entidad MUST configurar qué hacer con un valor que excede lo exigible al aplicarse. Por defecto: si
  la obligación sigue viva, el excedente queda como anticipo a sus próximas cuotas; si ya no hay deuda (cancelada por
  otro canal) o el concepto no admite anticipo, queda como saldo a favor del asociado, y Recaudo lo devuelve o lo
  reubica con su autorización. Ninguna opción mueve el dinero a otro producto sin esa autorización. La entidad configura
  también cómo reconocer la comisión de la pasarela en la conciliación.
- **FR-014**: Toda la configuración MUST tener valores por defecto razonables, quedar versionada con fecha de
  vigencia y auditada.

#### Identidad, activación y sesión

- **FR-015**: La cuenta del canal MUST ser única por tipo y número de documento en toda la plataforma,
  independiente de los usuarios del ERP y sin acceso a ninguna función del ERP.
- **FR-016**: La activación MUST iniciar con tipo y número de documento y enviar un código de un solo uso al correo
  o al celular que la entidad validadora tiene registrados, enmascarados en pantalla (p. ej. «j***@g***.com»).
- **FR-017**: La entidad validadora MUST ser la primera entidad que habilitó a la persona y que la mantiene
  habilitada; si ninguna la tiene habilitada, no hay activación.
- **FR-018**: El canal MUST responder igual a documentos habilitados y no habilitados en activación, ingreso y
  recuperación de clave, para no revelar si una persona existe ni en qué entidad.
- **FR-019**: Si la entidad validadora no tiene correo ni celular de la persona, el canal MUST indicarle que se
  acerque a esa entidad, con su contacto de soporte, y no ofrecer otro camino.
- **FR-020**: Al activar, el asociado MUST crear una clave que cumpla la política del canal y aceptar términos y
  política de datos; la aceptación queda registrada con fecha, versión del texto, IP y dispositivo.
- **FR-021**: El ingreso MUST ser con tipo y número de documento y clave; en un dispositivo no reconocido se exige
  además un código de un solo uso; en la app, la persona puede activar desbloqueo biométrico o llave de acceso
  (passkey) para los ingresos siguientes en ese dispositivo.
- **FR-022**: Tras un número configurable de fallos, la cuenta MUST bloquearse temporalmente y avisarse a la
  persona por sus datos de contacto; los códigos de un solo uso vencen y tienen intentos limitados.
- **FR-023**: El asociado MUST poder recuperar su clave con un código enviado a los datos de contacto de su cuenta
  del canal.
- **FR-024**: El asociado activo MUST poder cambiar su correo y su celular verificando el nuevo con un código; el
  canal avisa al dato anterior. El cambio vale sólo para la cuenta del canal y cada entidad lo ve como novedad de
  datos de contacto, sin que se sobrescriba la persona del ERP.
- **FR-025**: Tras autenticarse, si la persona tiene más de una entidad habilitada con el canal encendido, MUST
  elegir con cuál continuar; con una sola, entra directo; puede cambiar de entidad sin volver a autenticarse.
- **FR-026**: La inhabilitación por una entidad MUST quitar el acceso a toda la información de esa entidad desde la
  siguiente petición, sin impedir el ingreso ni afectar las demás entidades.
- **FR-027**: La sesión del canal MUST cerrarse a los 5 minutos sin actividad, con aviso al minuto 4, y durar como
  máximo 8 horas desde el ingreso; cada entidad puede subir la inactividad hasta 15 minutos, nunca más. La sesión
  pausada en la pasarela no cuenta como inactividad mientras el intento esté vigente. El asociado MUST poder ver sus
  dispositivos y cerrar la sesión en cualquiera de ellos.
- **FR-028**: Una sesión acotada a una entidad MUST poder leer y pagar sólo lo de esa persona en esa entidad.

#### Consulta

- **FR-029**: El canal MUST mostrar los conceptos publicados de la persona en la entidad elegida con los valores que
  calcula el ERP a la fecha de la consulta: saldo, vencido, intereses de mora, cuota del período, próxima fecha y
  valor exigible; la fecha de cálculo se muestra.
- **FR-030**: El canal MUST NOT guardar saldos ni movimientos de la persona más allá de lo necesario para la sesión
  en curso; la fuente es siempre el ERP.
- **FR-031**: Si el ERP de la entidad no responde, el canal MUST decirlo y no mostrar cifras anteriores como
  actuales.

#### Pago

- **FR-032**: El asociado MUST poder armar un pago con uno o varios conceptos del **mismo destino** y elegir el
  valor de cada uno dentro de las modalidades configuradas; conceptos de destinos distintos se pagan por separado.
- **FR-032a**: En un abono anticipado a un crédito, el canal MUST preguntar al asociado si reduce el plazo o el
  valor de la cuota, con la opción por defecto de la entidad preseleccionada; la elección viaja con el aviso de pago y
  Cartera la aplica por el contrato de obligaciones. Si el crédito no admite una de las dos, sólo se ofrece la otra.
- **FR-033**: Antes de redirigir, el canal MUST mostrar un resumen con el detalle por concepto, el impuesto si lo
  hay (base, tipo y valor), el total, la moneda y el logo de la pasarela; el total enviado a la pasarela MUST ser
  igual al mostrado.
- **FR-034**: El asociado MUST aceptar explícitamente términos y política de datos de la entidad antes de cada pago
  si su versión cambió desde la última aceptación.
- **FR-035**: Cada intento MUST tener una referencia única, alfanumérica, de hasta 32 caracteres, que nunca se
  reutiliza.
- **FR-036**: Confirmar varias veces MUST crear un solo intento.
- **FR-037**: El intento MUST enviar a la pasarela los datos del asociado como comprador (tipo y número de
  documento, nombres, apellidos, correo y celular), validados según las reglas de la pasarela, más la IP y el agente
  de navegación del dispositivo del asociado, la fecha de vencimiento y la dirección de retorno.
- **FR-038**: Al volver de la pasarela, el canal MUST consultar el estado del intento a la pasarela y mostrar el
  resultado (referencia, estado, fecha y hora, valor, moneda, autorización o CUS); nunca debe confiar en los datos
  de la dirección de retorno.
- **FR-039**: El canal MUST recibir las notificaciones de la pasarela, verificar su firma y, si la firma no
  corresponde, descartarlas y auditarlas.
- **FR-040**: El canal MUST consultar periódicamente los intentos pendientes (cada 15 minutos durante la
  certificación; la frecuencia en producción es configurable, como mínimo una vez al día) y actualizar su estado.
  La notificación y la consulta periódica sólo actúan sobre intentos pendientes.
- **FR-041**: Mientras un concepto tenga un intento pendiente, el canal MUST impedir pagarlo de nuevo y mostrar el
  aviso de pago pendiente con referencia, valor, autorización y el contacto de la entidad.
- **FR-042**: Los datos de tarjeta MUST NOT pasar por el canal ni por el ERP ni guardarse en ningún sistema de la
  plataforma, tampoco como token; sólo se guardan la referencia, la autorización, la franquicia y los últimos
  dígitos si la pasarela los devuelve.
- **FR-043**: Cada cambio de estado de un intento MUST quedar registrado con su fuente (retorno, notificación,
  consulta periódica, conciliación), fecha y respuesta de la pasarela.

#### Aplicación en el ERP

- **FR-044**: Al quedar aprobado un intento, el canal MUST avisar al ERP de la entidad; el aviso va firmado y es
  idempotente por referencia: repetirlo no cambia nada y devuelve el mismo resultado.
- **FR-045**: El ERP MUST registrar el pago como recibo de canal «en línea» en Recaudo contra el convenio del
  destino, con la referencia y la autorización de la pasarela, y aplicarlo por el contrato de obligaciones, el de
  cuentas o el módulo dueño del otro cobro, en la misma unidad de trabajo, con su asiento contra la cuenta puente.
- **FR-046**: Si el ERP no puede aplicar todo o parte del valor, MUST tratarlo según FR-013 y nunca rechazar ni
  perder un pago aprobado; el asociado ve «pago recibido, en revisión» hasta que se resuelva.
- **FR-047**: Si el aviso no llega a aplicarse (ERP caído), el canal MUST reintentar con espera creciente y alertar
  a la entidad después de un umbral configurable.
- **FR-048**: Cada pago aplicado MUST generar un comprobante del ERP disponible para el asociado en el canal.
- **FR-049**: Esta feature enmienda FR-091 de la 015: los pagos por pasarela entran también en línea por el canal,
  además de por archivo.

#### Conciliación y reverso

- **FR-050**: Recaudo MUST conciliar a diario, por destino, los pagos del canal contra el reporte de la pasarela y
  contra el abono bancario, reconociendo la comisión según FR-013.
- **FR-051**: La conciliación MUST aplicar, una sola vez, los aprobados que la pasarela reporte y el ERP no tenga, y
  dejar como partida por identificar toda diferencia sin explicación.
- **FR-052**: Un funcionario con permiso MUST poder reversar un pago del canal: antes del corte diario de la pasarela,
  por la pasarela, reversando la aplicación por el módulo dueño; después del corte, como devolución por Tesorería con
  motivo.
- **FR-053**: Un reverso MUST quedar visible para el asociado en su historial.

#### Historial, documentos y avisos

- **FR-054**: El canal MUST mostrar el historial de intentos por entidad en orden descendente, con al menos las
  últimas cinco y con fecha y hora, referencia, autorización o CUS, estado y valor con su moneda.
- **FR-055**: El canal MUST ofrecer los extractos y certificados que la entidad publica, generados por el ERP en el
  momento de pedirlos; cada descarga queda auditada en la entidad.
- **FR-056**: El canal MUST enviar avisos por notificación en la app y por correo: cuota próxima, pago aplicado,
  pago rechazado, pendiente resuelto y avisos de seguridad; la persona puede apagar todos menos los de seguridad.
- **FR-057**: El canal MUST incluir una sección de preguntas frecuentes, términos y condiciones y política de datos
  de la entidad, accesible desde cualquier pantalla.

#### Atención y supervisión

- **FR-058**: Un funcionario de la entidad con permiso MUST poder buscar por documento o referencia y ver la cuenta
  del canal de la persona (estado, último ingreso, dispositivos), sus intentos y su estado en la pasarela, y
  desbloquear la cuenta o cerrar sus sesiones.
- **FR-059**: Ninguna consulta de una entidad MUST revelar datos de la persona en otra entidad ni si existe allí.
- **FR-060**: La entidad MUST ver indicadores del canal: personas habilitadas y activas, ingresos, intentos por
  estado, valor recaudado por destino y por medio, y rechazos por motivo.
- **FR-061**: Toda acción del asociado y de los funcionarios sobre el canal MUST quedar auditada (ingreso, cambio de
  datos, intento, pago, descarga, inhabilitación, reverso).

#### App de tiendas

- **FR-062**: La experiencia MUST ser la misma en la PWA y en la app de tiendas; la app suma biometría o passkey,
  notificaciones del teléfono y almacenamiento seguro de la sesión.
- **FR-063**: Una sola app y una sola PWA sirven a todas las entidades; tras elegir la entidad, el canal muestra su
  marca.

#### Simulador y solicitud

- **FR-064**: El asociado MUST poder simular un crédito con las líneas que la entidad publique para el canal, con la
  misma cuota que calcula el ERP.
- **FR-065**: El asociado MUST poder enviar una solicitud de crédito que llega al ERP como solicitud con canal de
  origen «canal digital», y ver su estado sin notas internas.

### Key Entities *(include if feature involves data)*

- **Cuenta del canal**: la persona en el canal; tipo y número de documento, nombres, correo y celular verificados,
  estado (pendiente de activar, activa, bloqueada temporalmente), aceptaciones de textos legales, dispositivos de
  confianza. Vive en la base del canal. No hay estado «cerrada» en esta feature.
- **Habilitación**: la relación persona–entidad para el canal; entidad, identificador público de la persona en la
  entidad, estado, fecha de habilitación, motivo de inhabilitación. Su fecha decide la entidad validadora.
- **Configuración del canal de la entidad**: encendido, marca, textos legales y sus versiones, vencimiento de la
  sesión de pago, código al pagar, política de habilitación, avisos, documentos publicados, política de excedentes y
  comisiones. Vive en la base de la entidad.
- **Concepto pagable**: tipo y subtipo, modalidades de monto, mínimo, si admite ir con otros, destino de pago y,
  en créditos, la opción de prepago por defecto (reducir plazo o cuota).
- **Destino de pago / sitio de pasarela**: pasarela, sitio, credenciales cifradas, medios ofrecidos, convenio de
  recaudo, cuenta puente, corte diario.
- **Intento de pago**: referencia, entidad, persona, destino, conceptos con su valor (y la elección de prepago en
  créditos), impuestos, total, estado, fechas,
  respuestas de la pasarela, fuente de cada cambio, autorización/CUS, IP y agente del asociado.
- **Aviso de pago**: referencia, firma, resultado de la aplicación en el ERP (aplicado, en revisión), recibo y
  comprobante del ERP.
- **Novedad de contacto**: el cambio de correo o celular hecho por el asociado en el canal, visible para cada
  entidad donde está habilitado.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un asociado habilitado activa su cuenta en menos de 3 minutos desde que digita su documento.
- **SC-002**: Un asociado paga una cuota en menos de 2 minutos desde que ingresa hasta que llega a la pasarela.
- **SC-003**: El 100 % de los pagos aprobados por la pasarela queda aplicado en el ERP o en revisión, sin pérdidas
  ni duplicados; el 99 % se aplica en menos de 5 minutos desde la aprobación.
- **SC-004**: Ningún pago se aplica dos veces, aunque el aviso se repita o llegue por notificación, retorno, consulta
  periódica y conciliación a la vez (verificado con pruebas de repetición).
- **SC-005**: Los valores que ve el asociado coinciden con los del ERP a la misma fecha en el 100 % de los casos
  probados.
- **SC-006**: Ninguna prueba logra ver información de una entidad donde la persona está inhabilitada ni saber si una
  persona existe en una entidad o en el canal.
- **SC-007**: La conciliación diaria deja explicado el 100 % del valor reportado por la pasarela y abonado en el
  banco, o como partida por identificar.
- **SC-008**: El sitio de COOFLOPAL aprueba la certificación de la pasarela a más tardar en el segundo intento.
- **SC-009**: El 95 % de las consultas de conceptos responde en menos de 3 segundos.
- **SC-010**: A tres meses de salir en vivo, al menos el 30 % de los asociados habilitados de la primera entidad ha
  activado su cuenta y las llamadas por «¿cuánto debo?» bajan (la entidad fija la línea base).

## Decisiones del dueño (2026-11-08)

Tomadas en formulario en la sesión «Plan App y PSE»; todas eligieron la opción recomendada.

- **D1 · Arquitectura**: el canal es un **servicio satélite** con base propia (cuentas del canal, sesiones, intentos
  de pago, integración con la pasarela). **No accede a las bases de las entidades**: lee por una interfaz de canal
  que expone el ERP de cada entidad y avisa los pagos aprobados por una entrada firmada e idempotente. El ERP sigue
  siendo el único que escribe en sus libros.
- **D2 · Tecnología** (restricción del dueño, no de negocio): backend del canal en **Go**; interfaz en **React**
  como PWA, empaquetada con **MAUI HybridWebView** para las tiendas. Exige **enmendar la constitución**, que hoy
  declara .NET y Blazor como stack (ver Dependencias).
- **D3 · Identidad**: cuenta del canal aparte de los usuarios del ERP, anclada al documento, sin roles del ERP.
- **D4 · Comercio**: un sitio de pasarela **por entidad**, con sus credenciales; el dinero llega directo a la
  entidad. Primera pasarela: **Avalpay** (WebCheckout de Placetopay/Evertec); el modelo admite otras.
- **D5 · Entidad validadora**: la primera que habilitó a la persona (dato estable).
- **D6 · Activación**: autoactivación con código a los datos de la entidad validadora.
- **D7 · Datos de contacto**: cambiados en el canal valen sólo para la cuenta del canal; el ERP los ve como novedad.
- **D8 · Segundo factor**: código en dispositivo nuevo y biometría o passkey en la app; la entidad puede exigir
  código al pagar.
- **D9 · Conceptos**: créditos, aportes, ahorro contractual o programado y otros cobros, todos en la primera
  entrega.
- **D10 · Montos**: exigible, parcial con mínimo, anticipado o total y varios conceptos juntos, por entidad y concepto.
- **D11 · Aplicación**: en línea y conciliación diaria.
- **D12 · Medios**: PSE y tarjeta según la entidad; sin tokenización ni recurrencia.
- **D13 · Código**: en el mismo repositorio, mismo clúster, base propia del canal.
- **D14 · Marca**: una app multientidad con la marca de la entidad elegida.
- **D15 · Alcance**: además de ver y pagar, historial y comprobantes, extractos y certificados, avisos, simulador y
  solicitud.
- **D16 · Calendario**: después de la salida en vivo de COOFLOPAL (01/12/2026); se construye en paralelo.

## Entregas

| Entrega | Contenido | Historias |
|---|---|---|
| **C0 · Fundamentos** | Enmienda de la constitución (stack del canal), interfaz de canal del ERP, firma de avisos | — |
| **C1 · Identidad y consulta** | Configuración por entidad, habilitación, activación, ingreso, selector, consulta, PWA en modo sólo consulta | US1, US2, US3, US12 (consulta) |
| **C2 · Pago** | Intento de pago, pasarela Avalpay, aplicación en el ERP, historial y comprobantes, certificación de COOFLOPAL | US4, US5, US8 |
| **C3 · Operación** | Conciliación diaria, reverso, atención y supervisión, indicadores | US6, US7, US12 |
| **C4 · Documentos y avisos** | Extractos, certificados, notificaciones por correo y en la app | US9, US10 |
| **C5 · App de tiendas** | App empaquetada, biometría o passkey, notificaciones del teléfono, publicación en tiendas | US11 |
| **C6 · Crédito en línea** | Simulador y solicitud | US13 |

## Assumptions

- **Tercero que paga por otro**: fuera del alcance; cada asociado paga sólo lo suyo.
- **Costo de la pasarela**: la comisión la asume la entidad (la pasarela no cobra al comprador) y se reconoce en
  la conciliación según la configuración del destino.
- **Proveedor de SMS**: lo elige la plataforma en el plan; sin SMS encendido en la entidad, todo código va por
  correo (FR-011a).
- **Personas habilitables**: además de asociados activos, la entidad puede habilitar ex asociados con saldo y
  deudores de otros cobros (FR-010); esas personas usan el mismo canal.
- **Cuentas a la vista**: depositar libremente en una cuenta de ahorro a la vista no es un concepto de la primera
  entrega; sí las cuotas de ahorro contractual o programado.
- **Pago mixto, dispersión y recurrencia de la pasarela**: no se usan.
- **Lightbox**: no se usa; el pago va siempre por redirección completa, que funciona igual en todos los navegadores
  y en la app.
- **Tablas `WEB_*` heredadas** (solicitudes web, actualización de datos, pagos extra): no se usan; su retiro se
  decide en el plan.
- **Habeas data**: el canal guarda sólo los datos de identidad y contacto de la persona y el registro de sus pagos;
  la entidad es responsable del tratamiento y la plataforma es encargada.
- **Moneda**: sólo pesos colombianos.
- **Idioma**: sólo español.

## Dependencias

- **Enmienda de la constitución** (Principio I y estándares técnicos): admitir un servicio satélite en Go y una
  interfaz en React fuera de la arquitectura en capas del ERP, con reglas propias (no accede a bases de entidades,
  idempotencia, firma). Requiere dos revisores según su gobierno.
- **Cartera (013), Captaciones (014), Recaudo y Tesorería (015)** activos en la entidad: sin ellos el canal sólo
  muestra lo que exista y no aplica pagos.
- **Servicios por vehículo (020)**: sus cobros entran como «otro cobro» cuando estén disponibles.
- **Credenciales de producción de la pasarela**: las entrega la pasarela tras la certificación; las de prueba ya
  existen para COOFLOPAL. Nunca entran al repositorio.
- **Cuentas de desarrollador** en Play Store y App Store a nombre de la plataforma.
- **Dominio** del canal (p. ej. un subdominio de la plataforma) con certificado.

## Fuera de alcance

- Abrir productos en línea (afiliación, apertura de cuentas o CDT).
- Transferencias entre cuentas, retiros o giros desde el canal.
- Billetera, tarjetas guardadas o pago a un clic.
- Atención por chat.
- Cerrar o inactivar la cuenta del canal, ni por el asociado ni desde el canal. Una solicitud de supresión de datos
  (Habeas data) se atiende como trámite de soporte de la plataforma; su procedimiento y los plazos de conservación
  quedan para una entrega posterior.
