# Feature Specification: Cartera financiera — líneas, originación, causación, pagos, riesgo y cobranza de créditos

**Feature Branch**: `013-cartera-financiera`
**Created**: 2026-10-04
**Status**: Draft — clarificada con el dueño el 2026-10-05 (Q1 a Q3, ver Clarifications). Antes pasó tres
verificaciones independientes: cobertura y constitución, norma y lógica financiera, y coherencia final.
Quedan para su revisión las decisiones por defecto (D1 a D28; D29 ya la decidió el dueño) y las interpretaciones
constitucionales (I-1 a I-6).
**Input**: Solicitud del dueño: crear desde cero el módulo de Cartera financiera de Ingenia365ERP
para organizaciones del sector solidario colombiano vigiladas por la Supersolidaria. Debe ser fácil,
funcional, dinámico, seguro, auditable y parametrizable, a la altura de Linix, SIIF/Opa, Bantotal,
Temenos y Mambu, e integrarse sin acoplarse con Contabilidad, Comercial (Inventario) y Recaudo. La
solicitud trae:
- doce actores internos y seis sistemas externos;
- diez requisitos transversales: R1 saldos sólo por transacciones, R2 reverso en vez de borrado,
  R3 auditoría verificable, R4 permisos y atribuciones con habeas data, R5 parametrización con
  vigencia, R6 desacoplamiento por mensajes y consultas, R7 idempotencia, R8 decimal exacto y
  reproducibilidad, R9 tres pasos o menos y R10 normativa;
- catorce capacidades, de A (líneas) a N (migración), y seis criterios de aceptación clave;
- un fuera de alcance: ahorros, aportes, la caja y los medios de pago (de Recaudo) y la contabilidad
  general.

Pide expresamente no modificar la constitución y marcar como pendiente de aclaración todo conflicto
con ella. La revisión está en «Conflictos con la constitución y con decisiones vigentes».

La solicitud dejó sin elegir «[cooperativa multiactiva / con sección de ahorro y crédito / fondo de
empleados]». Se lee como un módulo para **toda** organización solidaria cliente del ERP. El tipo de
entidad es un dato de cada cooperativa, y decide qué le permite la norma y qué le exige. COOFLOPAL es
la primera que lo usará. **No recibe ahorros** de sus asociados (confirmado por el dueño el 2026-10-05),
así que no está obligada a la pérdida esperada y puede salir con el Anexo 1. Aun así, la implementación
queda completa y configurable para todos los tipos de entidad (ver Clarifications).

## Contexto

Estado verificado el 2026-10-04 en el código, las especificaciones vigentes (009, 010 y 012), el plan
de implantación de COOFLOPAL y la norma publicada.

### La cartera actual

1. **Es un traslado tabla por tabla del sistema anterior, mezclado y sin terminar.**
   - Son 93 tablas, creadas en la base de toda cooperativa y en los dos motores.
   - Mezclan crédito con ahorros, depósitos, aportes, retiros, caja, chequeras, zonas, actas y SIPLA.
   - Ninguna semilla las llena, así que probablemente están vacías. Hay que confirmarlo con un
     diagnóstico antes de retirarlas.
2. **Tiene pantallas y rutas, pero no un ciclo de crédito utilizable.**
   - Tiene 115 rutas y 39 pantallas bajo «Cartera financiera». Ninguna ruta exige un permiso: basta
     con haber iniciado sesión.
   - No hay operaciones para garantías, codeudores, cobranza, reestructuraciones, seguros ni
     evaluación de riesgo, aunque sus tablas existen.
   - «Desembolsos» es una pantalla «en construcción», y dos pantallas llaman a una ruta que no existe.
   - El catálogo de permisos no tiene ningún código de Cartera.
3. **La contabilización está a medias.**
   - El desembolso y el recaudo contabilizan.
   - La causación, la clasificación y las deducciones por nómina tienen el comprobante pendiente
     desde la 009.
   - Las cuentas viven como texto dentro de los parámetros de la línea.
4. **No hay recaudo propio.** El único camino para un pago es la pantalla de recaudos de la cartera
   actual. La caja real del ERP es la del punto de venta de la 012.

### Quién depende de ella

- **Nómina** (la dependencia más fuerte):
  - la liquidación definitiva lee saldos, cuotas y mora de los créditos del empleado y propone los
    descuentos;
  - al aprobarla, **escribe** el recaudo en Cartera dentro de su misma transacción, obligación por
    obligación;
  - la nómina ordinaria lee las deducciones de cartera del período.
- **Personas**: no deja borrar a una persona con créditos vivos, y su ficha muestra un resumen de
  créditos y ahorros.
- **Comercial (012)**:
  - vende a crédito con **crédito provisional**. Cartera no responde, la venta exige una aprobación
    del límite de venta a crédito de Comercial, y la cuenta por cobrar la registra Contabilidad desde
    la factura;
  - emite y **acumula** dos mensajes para Cartera: «venta a crédito registrada» y «ajuste de venta a
    crédito» (nota crédito, devolución, nota débito, anulación, anulación por rechazo de la DIAN,
    reemplazo);
  - dejó definido que Cartera **crea siempre la obligación** de una venta confirmada y evalúa después.
    Una evaluación negativa deja la entrega en «validación fallida», con alerta, sin anular nada;
  - espera de Cartera dos consultas: el **estado de validación** de cada mensaje y el **cupo y
    estado** del comprador, con las líneas aplicables. Hoy las dos responden «Cartera no habilitada»;
  - guarda en sus parámetros la fecha de habilitación de Cartera, la política cuando Cartera no
    responde y **quién registra la cuenta por cobrar** de cada venta, sellado en la venta.
- **Contabilidad (009)**: su contrato de contabilización ya asigna a Cartera cinco tipos de comprobante
  (desembolso, recaudo, causación, deterioro y deducción de nómina), con el pagaré como documento
  cruce. Supone que Cartera llama al contrato directamente, como Nómina.

### La plataforma alrededor

Construido por la 012 y disponible:
- una **bandeja de mensajes de negocio** con sobre común, entrega por destino, reintentos, orden por
  cadena de documentos e idempotencia. Sólo conoce a Inventario como emisor, y a Contabilidad y
  Cartera como destinos;
- el **modo de paso** contable (en línea, por lotes con comprobante resumido, o no pasa) y la **matriz
  de reglas contables** del lado de Contabilidad, con reporte de completitud y conciliación;
- un **motor de aprobaciones** por niveles, con **límites de monto** por permiso y rol y segregación
  por persona;
- **parámetros con vigencia** leídos en un solo sitio. Sus ámbitos no incluyen la línea de crédito;
- **auditoría encadenada y verificable**: actor (persona o proceso), IP, canal, antes/después, motivo y
  clave de idempotencia. Se conserva diez años. Cartera **no** está entre los módulos encadenados;
- un **programador de tareas por cooperativa**, sin trabajos que barran todas las bases desde una sola
  conexión;
- **plantillas de importación** con revisión, todo o nada, y errores por fila y columna;
- **notificaciones** por correo y dentro de la aplicación. **No hay SMS ni WhatsApp**;
- **formatos de archivos planos como dato versionado** (bancos y PILA), con un motor común;
- el **catálogo de cuentas CUIF** sembrado. Separa cartera, intereses, pagos por cuenta de asociados y
  deterioro por modalidad, con y sin libranza, y por categoría A a E. Los créditos a empleados van en
  su propio grupo, y la cartera productiva en el suyo;
- los **festivos** viven hoy en Nómina, por cooperativa.

### La norma (verificada en la fuente)

- **SARC**: Circular Básica Contable y Financiera (CBCF), Título IV, Capítulo II, y sus Anexos 1 y 2.
  - La **Circular Externa 100 del 12/05/2026** dejó el **modelo de referencia de pérdida esperada**
    (Anexo 2) obligatorio sólo para:
    - cooperativas de ahorro y crédito;
    - multiactivas e integrales **con sección de ahorro y crédito**;
    - fondos de empleados de **categoría plena**;
    - asociaciones mutuales con ahorro y crédito.
  - Las demás califican y deterioran **por altura de mora con factor de riesgo** (Anexo 1). Pueden
    adoptar la pérdida esperada **voluntariamente**, y también desistir de ella, con acta del consejo y
    aviso previo a la Superintendencia.
  - El modelo de referencia cubre sólo **consumo con libranza, consumo sin libranza y comercial de
    persona natural**. Los demás segmentos siguen el Anexo 1 aun en las entidades obligadas.
  - El **arrastre** alcanza los créditos **de la misma modalidad** del deudor.
  - La causación se suspende desde una altura de mora por modalidad: comercial 90 días, consumo 60,
    vivienda 60, microcrédito 30. El deterioro general mínimo es el 1 %.
- **Modalidades**: consumo, comercial, vivienda y microcrédito, y desde el **Decreto 455 de 2023** las
  modalidades productivas (popular productivo rural y urbano, productivo rural y urbano, productivo
  de mayor monto). El microcrédito no se eliminó. La usura se certifica por modalidad, con vigencias
  que cambian (en octubre de 2026, mensual para todas).
- **Prepago**: la **Ley 2032 de 2020** extiende al sector cooperativo el pago anticipado sin penalidad
  de la **Ley 1555 de 2012**:
  - el **deudor** decide si un abono reduce el plazo o la cuota; la entidad **nunca** lo decide y debe
    preguntarle;
  - aplica a créditos de hasta 880 SMMLV.
- **Habeas data**: la **Ley 1266 de 2008**, modificada por la **Ley 2157 de 2021**:
  - el reporte negativo procede 20 días calendario después de **enviar** la comunicación previa;
  - las obligaciones pequeñas exigen dos comunicaciones;
  - un reporte sin aviso se retira.
- **Cobranza**: la **Ley 2300 de 2023**:
  - horario de lunes a viernes de 7:00 a 19:00 y sábados de 8:00 a 15:00, nunca domingos ni festivos;
  - tras un contacto directo, un solo canal por semana y una sola vez al día;
  - sólo por los canales que el deudor autorizó;
  - nunca a referencias personales.
- **Libranza**: la **Ley 1527 de 2012** pone un tope de descuento sobre el salario o la pensión, una
  prelación de descuentos, el giro del empleador y el traslado de la libranza al cambiar de empleador.
  La **Ley 79 de 1988** da preferencia a las cooperativas en los descuentos.
- **Personas vinculadas**: la **Ley 454 de 1998, art. 61**, exige, en entidades con actividad
  financiera, 4/5 de la composición del consejo para operaciones con administradores, órganos de
  control, grandes aportantes y sus parientes.
- **Vivienda**: la **Ley 546 de 1999** fija para vivienda tasa fija, el tope de la Junta del Banco de la
  República, plazo, porcentaje financiable e hipoteca de primer grado.
- **NIIF**: en estados financieros individuales, las organizaciones solidarias tratan la cartera y su
  deterioro con las reglas de la Supersolidaria y no con la NIIF 9 (salvedad del Decreto 2496 de 2015,
  a confirmar por la contadora).

### El calendario

El plan de implantación de COOFLOPAL incluye Cartera financiera en su salida en vivo, con la
parametrización en noviembre y saldos iniciales digitados al corte.
- La parametrización puede hacerse en noviembre.
- Los saldos se cargan con corte a la víspera de la activación, que espera a Recaudo y Tesorería (Q1).
- Una carga anterior es un ensayo (FR-142).

Su cartera hoy tiene:
- **créditos y aportes**; ahorros y CDT quedaron fuera de su implantación;
- **cuotas de servicios a asociados** (radio, aplicación y combo), con pagos anticipados, activación y
  suspensión por mora. **No son cartera**: son un servicio opcional que el asociado toma cuando quiere,
  que Comercial le factura electrónicamente y que normalmente se paga de contado (Clarifications, Q3).

La salida en vivo de Cartera depende además de **Recaudo y Tesorería**, que hoy no están especificadas
(Clarifications, Q1).

Como en la 012, esta especificación **no fija fechas**: el dueño las define cuando el módulo esté
listo. Sí fija qué debe estar para salir en vivo, porque el primer cierre mensual llega a los 30 días
(ver «Entregas»).

## Clarifications

### Session 2026-10-05

- Q: Recaudo y Tesorería no existen como módulos para Cartera. Mientras no existan, ¿quién recibe los
  pagos, procesa el débito automático y las respuestas de pagaduría, devuelve excedentes y saldos a
  favor, y confirma los giros de los desembolsos?
  → A: **B — se espera a las features de Recaudo y Tesorería.**
  - Esta feature no construye un recaudo ni una confirmación de giros provisionales, y no amplía la caja
    del punto de venta de la 012.
  - Cartera no se activa en producción sin Recaudo y Tesorería (FR-089, FR-142).
- Q: ¿Qué modelos de deterioro entran en esta feature, y cuándo?
  → A: **A — el Anexo 1 (altura de mora) en K1 y la pérdida esperada (Anexo 2) en K6 de esta misma
  feature.**
  - En el texto, K6 quedó dividida en K6a (pérdida esperada) y K6b (vivienda), para que K6a pueda
    adelantarse sola si la primera cooperativa obligada lo necesita.
  - Los dos quedan **parametrizables** por cooperativa y por segmento.
  - La pérdida esperada se aplica porque el tipo de entidad la exige, o porque la entidad la adopta
    voluntariamente (FR-105, FR-106).
  - **COOFLOPAL no recibe ahorros** (precisión del dueño del mismo día): no está obligada y sale con el
    Anexo 1.
  - El dueño pide que la implementación quede **completa y configurable para todos los tipos de
    entidad**: K6a se construye entera dentro de esta feature, aunque COOFLOPAL no la use al salir.
- Q: ¿Dónde viven las cuotas de servicios a asociados de COOFLOPAL (radio, aplicación y combo)?
  → A: **No son cartera.**
  - Son un servicio opcional: cada mes el asociado decide si lo toma y, si lo toma, se le factura
    electrónicamente. Lo factura Comercial, y normalmente se paga de contado.
  - Si se vende a crédito, queda como saldo por cobrar en Contabilidad, registrado desde Comercial, y
    **no llega a Cartera** (precisión del dueño del mismo día).
  - Lo que los asociados deban por servicios al corte tampoco entra a Cartera (D29).
  - Queda fuera de esta feature (Alcance).

## Conflictos con la constitución y con decisiones vigentes

La solicitud pide marcar todo conflicto con la constitución. Ésta es la revisión.

### Con la constitución

**No queda ningún conflicto abierto.**
- El único conflicto real era una verificación pública y anónima de documentos (ver D21). Una ruta sin
  sesión ni cooperativa resuelta choca con el estándar de seguridad y con el Principio IV, así que se
  deja **fuera de alcance** hasta que una enmienda la admita.
- Otras lecturas de la solicitud sí chocarían con la constitución. Esta especificación las descarta
  con las seis interpretaciones de abajo, que se someten a la confirmación del dueño.

- **I-1 · R6 frente al Principio V (persona maestra).** «Cartera nunca toca datos de otro módulo»,
  leído como que Cartera guarda su propia copia del deudor, violaría el Principio V. Se lee así:
  - deudores, codeudores, pagadurías, aseguradoras y abogados son **personas del maestro**, que
    Cartera referencia y nunca duplica;
  - un documento emitido (pagaré, acta, certificado, extracto) conserva **una copia de la
    identificación tal como se emitió**, que no es una tabla de rol (mismo criterio que la 012);
  - un mensaje enviado al deudor guarda el destino (teléfono o correo) al que se envió, por la misma
    razón.
- **I-2 · R6 frente a los Principios III y X.** Leído como «un proceso aparte escribe por fuera del
  ERP», chocaría con el camino común de validación y auditoría. Se lee igual que en la 012:
  - quien consume un mensaje ejecuta operaciones normales del ERP, por cooperativa;
  - el actor de un proceso automático es el **proceso** de la cooperativa, identificado como tal;
  - el usuario que originó el hecho viaja como dato.
- **I-3 · Valores nacionales frente al Principio IV.** La usura certificada, los índices (IBR, IPC), el
  tope de vivienda, el salario mínimo y los festivos son los mismos para todas las cooperativas.
  Leerlos de una base compartida durante un cálculo sería un cruce entre bases. Se lee así:
  - se registran **una vez** fuera de las bases de cooperativa;
  - **se replican** con su vigencia a la base de cada cooperativa, también en el alta de una
    cooperativa nueva;
  - cada cálculo lee la copia de su propia base, y la réplica queda auditada.
- **I-4 · R6 frente a una garantía «todo o nada» entre módulos.** El contrato de obligaciones se
  ejecuta **sincrónicamente dentro de la unidad de trabajo del recaudador**, en la base de la misma
  cooperativa.
  - Nómina no toca datos de Cartera, pero su aprobación y los recaudos ocurren juntos (FR-088).
  - Cada llamada pasa por el camino común de validación y auditoría.
  - La auditoría registra el intento y su desenlace final: confirmado, o revertido junto con el
    recaudador.
- **I-5 · Principio XI.** Nombra una tabla de movimientos de crédito que hoy no existe con ese nombre.
  Se lee así:
  - la **transacción de cartera** nueva es el objeto del Principio XI, junto con los comprobantes de
    aplicación, los cierres mensuales, las actas y los mensajes emitidos;
  - corregir el nombre en la constitución queda para su propio procedimiento de enmienda.
- **I-6 · Proyecciones frente al Principio VII.** Saldos por concepto, cuotas pendientes, días de mora
  y calificación diaria se **calculan al leer** a partir de las transacciones. Si se guardan para
  rendimiento, es como **instantáneas versionadas** que nunca se borran ni se sobrescriben.

| Principio | Cómo lo cumple esta especificación |
|---|---|
| I · Spec first | Esta especificación, antes de plan y tareas. Incluye las piezas del lado de Comercial, Contabilidad, Nómina, Personas y la plataforma que el desacoplamiento exige (enmiendas E-01 a E-05). |
| II · Capas | No lo altera; lo verifica el plan. |
| III · Camino común | Toda escritura es una operación del ERP con validación y auditoría: también la que dispara un mensaje, un lote nocturno o el contrato de obligaciones. Ninguna consulta cambia el estado del negocio; las lecturas sensibles sólo dejan su registro de auditoría. Reservar un número de crédito es una operación aparte (FR-057). |
| IV · Una base por cooperativa | Créditos, parámetros, mensajes y cierres viven en la base de cada cooperativa. Los procesos diarios recorren las cooperativas una por una. Los valores nacionales se replican, no se comparten (I-3). |
| V · Persona maestra | I-1. El alta de un deudor nuevo usa el diálogo único de personas. |
| VI · Identificador público | Rutas, pantallas, mensajes y auditoría identifican el crédito por su identificador público. El número del crédito es un dato visible y un criterio de búsqueda, nunca un identificador en una ruta. |
| VII · Borrado lógico | Toda entidad es auditable. Lo inmutable no se borra por ninguna vía de la aplicación (I-6). |
| VIII · Validación doble | Toda regla se valida en pantalla y en el servidor. Los mensajes van en español y dicen qué hacer. |
| IX · Errores visibles | Un mensaje rechazado, un proceso que no corrió, un descuadre o un no aplicado de pagaduría quedan como alerta para quien debe actuar (FR-016). |
| X · Trazabilidad | Toda acción queda con actor, fecha, origen (IP, canal, oficina), antes/después y motivo. Cartera entra a la auditoría encadenada, que se conserva diez años. |
| XI · Inmutabilidad | I-5. |
| XII · Migraciones | Retirar las tablas de crédito heredadas es destructivo. Exige diagnóstico, respaldo, segundo revisor y el marcador de aprobación, en la base administrativa y en todas las de cooperativa (FR-144). |

### Con decisiones vigentes de otras features

Aquí **sí hay conflictos**. Se resuelven así, y las enmiendas E-01 a E-05 los recogen.

- **C1 · «Obligación rechazada» frente a la 012.** La solicitud pide que Cartera rechace una venta, y
  la 012 dejó definido que Cartera crea siempre la obligación de una venta confirmada: la factura ya
  existe y la deuda también. **Resuelto** (decisión D9):
  - una evaluación negativa **crea la obligación marcada «validación fallida»** y bloquea crédito
    nuevo al deudor;
  - «obligación rechazada» queda para los mensajes que no se pueden convertir en obligación, que
    quedan para reproceso (FR-052, FR-053).
- **C2 · «La factura muestra el número del crédito».** La factura electrónica se firma al confirmar la
  venta, antes de que el crédito exista, y no se modifica después. Además, la consulta de cupo de la
  012 se llama varias veces y no puede escribir. **Resuelto** con una operación aparte, **reservar
  número de crédito**, ligada al documento de venta (FR-057, decisión D3). Exige estos cambios en la
  012 (E-02):
  - una operación más en su interfaz con Cartera;
  - una versión 2 del mensaje de venta con el número reservado;
  - ajustar los requisitos de la 012 que no guardan respuesta de Cartera.
- **C3 · Contabilidad (009) supone que Cartera llama al contrato directamente.** R6 lo prohíbe.
  **Resuelto** como en la 012:
  - Cartera emite hechos;
  - Contabilidad los consume con validación previa, modo de paso y una matriz de Cartera de su lado;
  - se reemplazan los cinco tipos de comprobante asignados y los escritores pendientes (E-01).
- **C4 · Nómina lee y escribe en Cartera directamente.** **Resuelto**:
  - lee por la consulta de obligaciones;
  - recauda por el contrato de obligaciones, con la garantía «todo o nada» de I-4 (E-03);
  - la nómina ordinaria pasa a ser una pagaduría interna (FR-112).
- **C5 · Personas consulta créditos directamente.** **Resuelto**: por la consulta de obligaciones de la
  persona (E-04).
- **C6 · Las tablas heredadas mezclan módulos.** **Se retiran sólo las de crédito.** Las demás quedan
  hasta su propia feature, sin llaves que apunten a lo retirado (FR-144).
- **C7 · Recaudo no existe.** La solicitud dice que Recaudo define el contrato «Proveedor de
  Obligaciones», pero no hay módulo ni especificación de Recaudo. **Resuelto** así:
  - esta especificación define el contrato, para que Recaudo lo adopte (decisión D15);
  - mientras Recaudo y Tesorería no existan, Cartera no se activa en producción, y esta feature no trae
    recaudo ni giros provisionales (decidido por el dueño, Q1; FR-089).
- **C8 · Los festivos viven en Nómina.** Cartera los necesita y no puede leer datos de Nómina.
  **Resuelto**: los festivos pasan a ser un valor nacional de la plataforma (I-3), y Nómina los toma
  de ahí (E-05).

## Alcance

### Dentro de esta feature

- Líneas de crédito parametrizables con vigencia, tasas fijas y variables, y control diario de usura.
- Originación:
  - simulador y radicación con lista de documentos;
  - validaciones: listas restrictivas, centrales, capacidad de pago y de descuento, endeudamiento,
    cupo y concentración;
  - puntaje, atribuciones, actas y reglas de personas vinculadas.
- Garantías, codeudores y pagaré (físico o desmaterializado por adaptador).
- Desembolso por transferencia, cheque, giro a tercero o en especie, con recogida de créditos propios
  y reverso de un desembolso no girado.
- Créditos nacidos de ventas de Comercial sobre un cupo otorgado, consulta de cupo y estado, y reserva
  del número de crédito.
- Causación diaria, suspensión y reanudación, reajuste de tasas variables y liquidación a cualquier
  fecha.
- El **contrato de obligaciones** (listar, simular, aplicar y reversar pagos) con siete modalidades,
  prelación por línea, anticipos y saldos a favor.
- Novedades clasificadas por su causa: reestructuración, modificación, refinanciación, novación,
  prórroga, cambio de fecha y de tasa. También condonaciones, insolvencia, castigo y su seguimiento.
- SARC:
  - calificación con arrastre por modalidad;
  - evaluación periódica de cartera;
  - deterioro individual y general;
  - cierre mensual con reclasificación por categoría.
- Cobranza por etapas, gestores, acuerdos, mensajes con la Ley 2300 y aviso previo de la Ley 1266.
- Libranza:
  - pagadurías, prelación de descuentos, archivo de descuentos y no aplicados;
  - el estado «descontado por la pagaduría, pendiente de giro» y el traslado de la libranza;
  - la nómina propia como pagaduría.
- Hechos para Contabilidad, matriz contable y conciliación.
- Informes:
  - documentos al deudor;
  - reportes de gestión, cosechas, concentración y tablero;
  - formato de cartera del SICSES y archivos para centrales de riesgo.
- Migración de la cartera vigente con cuadre contra Contabilidad, y retiro de la cartera de crédito
  heredada.
- Las adaptaciones de Comercial, Contabilidad, Nómina, Personas y la plataforma que el desacoplamiento
  exige.

### Fuera de esta feature

- **Ahorros, depósitos, CDT y aportes** como productos. Cartera sólo **consulta** el saldo de aportes y
  de ahorro permanente de un asociado (ver «Interfaces» y D19).
- **La caja y los medios de pago**: son de Recaudo. El **cajero** es actor de Recaudo y llega a Cartera
  por el contrato de obligaciones.
- **Un recaudo o una confirmación de giros provisionales**: no se construyen (Q1). Cartera espera a
  Recaudo y Tesorería para salir en vivo (FR-089).
- **Las cuotas de servicios a asociados** de COOFLOPAL (radio, aplicación y combo), por decisión del
  dueño (Q3):
  - son un servicio opcional, que el asociado toma cuando quiere, que Comercial le factura
    electrónicamente y que normalmente se paga de contado;
  - si se vende a crédito, queda como saldo por cobrar en Contabilidad, registrado desde Comercial, y no
    llega a Cartera;
  - lo que los asociados deban por servicios al corte tampoco entra a Cartera (D29);
  - si COOFLOPAL quisiera conservar los pagos por adelantado de servicios, sería un asunto de Comercial
    (ver Dependencias).
- **La contabilidad general**: Cartera emite hechos; los comprobantes son de Contabilidad.
- **El giro del dinero del desembolso**: Cartera registra la orden y su forma, y genera el archivo de
  transferencia con los formatos bancarios de la plataforma. Pagar es de Tesorería.
- **Un portal o aplicación del asociado** (decisión D1): el asociado es actor de consulta por medio de
  los documentos que la oficina le entrega o le envía.
- **La verificación pública y anónima de documentos** (D21).
- **La gestión del sistema SARLAFT**: segmentación, monitoreo y reportes a la UIAF.
  - Cartera **consulta** las listas restrictivas por un adaptador y avisa al oficial de cumplimiento.
  - Mientras no haya proveedor de listas, esta feature incluye una **carga de listas con vigencia**
    que mantiene el oficial de cumplimiento y que el adaptador consulta. No es un módulo SARLAFT.
- **Integraciones reales** con el depósito centralizado de valores (Deceval), centrales de riesgo y
  proveedores de SMS y WhatsApp:
  - cada una entra por un adaptador con **canal simulado** hasta que la cooperativa tenga contrato;
  - los formatos regulatorios se generan como archivo y los carga una persona.
- **La facturación del interés de financiación**, si la contadora lo declara gravado con IVA: es de
  Comercial (D20).

### Entregas sugeridas

El orden es una propuesta para el plan; ninguna lleva fecha. **K1 es lo mínimo para salir en vivo**:
sin calificación, deterioro ni cierre mensual no se puede cerrar el primer mes. Además de K1, salir en
vivo exige:
- Recaudo y Tesorería (Q1);
- en una cooperativa obligada al modelo de referencia, la pérdida esperada (K6a).

| Entrega | Contenido | Historias |
|---|---|---|
| K1 · Núcleo y riesgo básico | Retiro de lo heredado. Plataforma completa (E-05: emisor Cartera, ámbito de línea, auditoría encadenada, valores nacionales). Enmiendas E-01 a E-04 completas, incluida la nómina ordinaria por el contrato (FR-112, sin archivo de pagaduría). Líneas, simulador, originación, desembolso, causación, liquidación, contrato de obligaciones, Comercial y cupo. Hechos contables y matriz, calificación y deterioro por altura de mora, cierre mensual, conciliación, migración y seguridad | US1–US9 (salvo US8-10, US9-6, US9-7 y US9-9), US17 |
| K2 · Libranza | Pagadurías, prelación, archivos, pendientes de giro, no aplicados, traslado, convenio de la nómina propia y gestión de sus no aplicados | US10 |
| K3 · Novedades | Novedades por causa, consentimiento de garantes, condonación, insolvencia, castigo, evaluación periódica de cartera | US11, US14 |
| K4 · Cobranza | Etapas, gestores, acuerdos, mensajería, aviso previo | US12 |
| K5 · Informes | Documentos, reportes de gestión, SICSES, centrales y tablero | US13, US15, US16 |
| K6a · Pérdida esperada | Pérdida esperada (FR-106), parametrizable por tipo de entidad o por adopción voluntaria (Q2) | US9-6, US9-7, US9-9, US8-10 |
| K6b · Vivienda | Vivienda con la Ley 546 y UVR (D18) | — |

Todas las entregas, de K1 a K6b, son parte de esta feature y se construyen completas y configurables para
todos los tipos de entidad, por pedido del dueño: ninguna queda para otra feature.

Reglas de convivencia entre entregas. El plan puede reordenar K2 a K6b, y debe adelantar K6a a antes de
la salida de la primera cooperativa obligada al modelo de referencia.
- **Recogida con mora.** Hasta K3, la recogida de un crédito en mora se rechaza con «requiere
  reestructuración».
- **Datos migrados de entregas posteriores.** Lo migrado que corresponde a K2–K4 (pagaduría, etapa de
  cobranza, conteo de reestructuraciones) se conserva como dato desde K1. Esos créditos se cobran por
  el contrato, y la recuperación de cartera castigada funciona desde K1.
- **Vivienda.** Hasta K6b, las cuentas de vivienda quedan fuera del cuadre de activación (FR-141), y así
  se declara en el acta de activación.
- **Pérdida esperada.** Hasta K6a, ninguna cooperativa puede adoptar el modelo de referencia
  voluntariamente, y una cooperativa obligada no puede activar Cartera en producción (FR-105, FR-142).
  En los ambientes de prueba se activa con el Anexo 1 y la marca «modelo obligatorio no disponible».
- **No aplicados de la nómina propia.** Hasta K2 no hay convenio de la nómina propia. Lo que Nómina no
  pudo descontar (FR-112) queda marcado en el crédito con su motivo, la cuota sigue exigible y se alerta
  al jefe de cartera. El escenario US10-6 se verifica desde K1.

### Glosario

**El crédito y su plan**
- **Crédito u obligación**: la deuda de un deudor en una línea, con número propio, condiciones
  pactadas y plan de pagos.
- **Línea de crédito**: el producto parametrizado (modalidad, tasas, plazos, garantías, prelación).
  Tiene versiones con vigencia.
- **Modalidad**: la clasificación regulatoria del crédito, tomada de un catálogo con vigencia
  (consumo, comercial, vivienda, microcrédito y las productivas). Tiene sub-segmentos cuando la norma
  los distingue: con o sin libranza, persona natural o jurídica.
- **Condiciones pactadas**: lo que el crédito **sella** al desembolsar:
  - tasa, o índice y margen;
  - sistema de amortización, periodicidad y plazo;
  - seguros y prelación;
  - convención de días y redondeo;
  - la preferencia del deudor para imputar abonos a capital.
- **Plan de pagos**: las cuotas con su fecha y su capital, interés, seguros y otros. Cada novedad crea
  una **versión** nueva; las anteriores quedan en el historial.
- **Transacción de cartera**: el hecho inmutable que cambia un saldo. Tiene concepto, valor, actor,
  origen, enlace a su reverso y **tres fechas**:
  - **fecha valor**: desde cuándo cuenta para intereses y mora;
  - **fecha contable**: el período en que se reconoce, que es el primer mes sin cierre de Cartera que
    Contabilidad tenga abierto;
  - **fecha de registro**: cuándo se supo.
- **Concepto**: capital, interés corriente, interés de mora, interés en suspenso, seguros, gastos de
  cobranza y otros cargos.

**Causación, mora y riesgo**
- **Causación**: el reconocimiento diario del interés corriente (sobre el capital no vencido), de la
  mora (sobre el capital vencido) y de los seguros.
- **Suspendida**: desde la altura de mora que fija la norma, el interés se lleva **en suspenso**. Sigue
  debiéndose, pero no es ingreso hasta que se cobra.
- **Altura de mora**: los días transcurridos desde la cuota impaga más antigua. Se cuentan desde el
  día siguiente al vencimiento o, si éste cayó en día no hábil, desde el día siguiente al primer día
  hábil posterior.
- **Calificación**: la categoría de riesgo (A a E, con subcategorías cuando la norma las fija).
- **Arrastre**: llevar los créditos **de la misma modalidad** de un deudor a la peor categoría entre
  ellos.
- **Deterioro**: la pérdida estimada que se reconoce. Es **individual** por crédito y **general** sobre
  la cartera.
- **Cierre mensual**: el corte oficial de calificación y deterioro al último día del mes. Una vez hecho,
  no se reescribe.
- **Evaluación de cartera**: la revisión periódica que exige la norma, aprobada por el consejo, que
  puede recalificar créditos hasta la siguiente evaluación.

**Pagos**
- **Prelación**: el orden en que un pago cubre cuotas y conceptos.
- **Anticipo**: el dinero que el deudor entrega para cubrir cuotas futuras en su vencimiento. Es un
  pasivo de Cartera con el deudor.
- **Excedente**: lo que sobra de un pago. Nunca entra a Cartera: vuelve al recaudador, que lo resuelve.
- **Saldo a favor**: lo que Cartera le debe al deudor por una nota o una anulación de Comercial, o por
  una corrección de usura. Se devuelve o se aplica por el contrato (FR-083).
- **Contrato de obligaciones**: lo que Cartera ofrece a quien recauda (Recaudo, Nómina, Aportes, la
  pagaduría interna): listar las obligaciones de un tercero, simular, aplicar y reversar un pago.
- **Comprobante de aplicación**: el documento de Cartera que registra cómo se distribuyó un pago,
  enlazado al recibo del recaudador. No es un comprobante contable.

**Decisiones y garantías**
- **Atribución**: la facultad de un rol o nivel para aprobar hasta cierto monto, línea o condición.
- **Acta**: el registro de una decisión colegiada (comité, gerencia, consejo) con número, fecha,
  asistentes, quórum, votos y condiciones.
- **Cupo**: el monto máximo que un deudor puede deber, en total y por línea. El **cupo rotativo** se
  otorga por originación y se consume con ventas de Comercial.

**Libranza**
- **Pagaduría**: el empleador o entidad que descuenta las cuotas por libranza. **Convenio**: sus
  condiciones con la cooperativa.
- **Descontado pendiente de giro**: la cuota que la pagaduría ya descontó al empleado y todavía no ha
  girado.

**Otros**
- **Liquidación a la fecha**: el valor exacto para cancelar un crédito en una fecha dada, desglosado
  por concepto.
- **Usura**: el tope legal de tasa. La certifica la Superintendencia Financiera por modalidad y
  período. Para vivienda el tope lo fija la Junta del Banco de la República.
- **Proceso de la cooperativa**: el actor de lo que corre solo (causación, lotes, mensajes), distinto
  de una persona.
- **Paso**: una pantalla o diálogo con una acción de confirmación.

## Interfaces que Cartera ofrece y consume

Ésta es la lista **cerrada** de puntos de contacto de Cartera con el resto del ERP. Una verificación
automática fija esta lista (FR-008). Cualquier otro acceso entre Cartera y otro módulo es un defecto. Un
consumidor nuevo se agrega por enmienda de esta lista; «y otros» no es un consumidor.

**Ofrece**

| Interfaz | A quién | Qué hace |
|---|---|---|
| Consulta de cupo y estado | Comercial (ventas a crédito) | FR-059 |
| Reservar número de crédito y liberar la reserva | Comercial | FR-057; son operaciones idempotentes, no consultas |
| Consulta de estado de validación de un mensaje | Comercial | FR-053 |
| Consulta de obligaciones de una persona | Personas, Nómina, Aportes | FR-061 |
| Contrato de obligaciones (listar, simular, aplicar, reversar y resolver saldos a favor o anticipos) | Recaudo, Nómina, Aportes, recogida de créditos y, en prueba, el recaudador de prueba | FR-083 a FR-089 |
| Mensaje «pago reversado por Cartera» | El recaudador del pago | FR-080 |
| Orden de giro de un desembolso y su anulación (mensajes; los define esta especificación, D27) | Tesorería y, en prueba, la tesorería de prueba | FR-045, FR-047 |
| Mensajes «obligación creada» y «obligación rechazada» | Comercial | FR-053 |
| Hechos de cartera (FR-125) | Contabilidad | FR-125 a FR-129 |
| Mensaje «descuentos por pagaduría» | Recaudo | FR-113 |
| No descontados de la nómina propia, en la misma llamada del contrato | Nómina | FR-112 |

**Consume**

| Interfaz | De quién | Para qué |
|---|---|---|
| Mensajes «venta a crédito registrada» y «ajuste de venta a crédito» | Comercial | FR-050 a FR-055 |
| ¿Es contabilizable? | Contabilidad | FR-127 |
| Saldos de las cuentas mapeadas a una fecha | Contabilidad | FR-130, FR-141 |
| Estado de un período contable | Contabilidad | FR-012 (fecha contable), FR-107 (reabrir un cierre) |
| Resultado del giro (girada, rechazada, cheque no cobrado, anulada) | Tesorería | FR-045, FR-048 |
| Cobros provisionales por plantilla, al registrar la activación | Plantilla | FR-142 |
| Confirmación del envío del archivo de descuentos y mensaje «resultado de descuentos» | Recaudo | FR-113 a FR-115 |
| Registro de recaudadores del contrato y de destinos activos de la bandeja, por cooperativa | Plataforma | FR-142 |
| Indicadores de la entidad (pérdidas, solvencia, patrimonio técnico) | Contabilidad | FR-041, FR-100, FR-103 |
| Cuentas bancarias de origen | Contabilidad | FR-047 |
| Saldo de aportes y de ahorro permanente de un asociado | Aportes y Ahorros | FR-029, FR-100, FR-103 |
| Saldo de aportes por plantilla, con fecha (provisional mientras no exista la consulta anterior; lo carga el jefe de cartera) | Plantilla | D19 |
| Personas, alta de persona y buscador | Maestro de personas | I-1 |
| Valores nacionales (usura, índices, tope de vivienda, salario mínimo, festivos, pisos y techos legales) | Plataforma | FR-015 |
| Adjuntos, aprobaciones, parámetros, archivos planos, bancos, notificaciones, auditoría | Plataforma | Varios |
| Adaptadores: listas restrictivas (o su carga provisional), centrales, depósito de valores, SMS, WhatsApp | Externos | FR-036, FR-044, FR-122, FR-137 |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parametrizar líneas de crédito que nunca superan la usura (Priority: P1)

El jefe de cartera crea y ajusta las líneas de crédito sin programar:
- la modalidad, la tasa fija o variable y la tasa de mora;
- montos, plazos y periodicidades;
- el sistema de amortización, los seguros, las garantías y los codeudores exigidos;
- la forma de pago y la prelación de imputación.

Cada cambio crea una versión con fecha desde. El sistema compara las tasas, con todos los cargos que
computan, contra la usura certificada vigente. No deja crear, aprobar ni desembolsar nada por encima,
y ningún crédito vivo cobra un día por encima.

**Why this priority**: sin líneas no hay originación, ni créditos de Comercial, ni migración. La usura
es un límite legal cuyo incumplimiento es sancionable.

**Independent Test**: crear una línea de consumo con tasa fija y otra variable sobre un índice,
registrar la usura del mes y comprobar que una tasa por encima se rechaza con un mensaje claro.

**Acceptance Scenarios**:

1. **Given** una usura de consumo vigente registrada, **When** el jefe de cartera crea una línea cuya
   tasa efectiva anual (con los cargos que computan) es mayor, **Then** el sistema la rechaza. El
   mensaje indica la usura vigente, la modalidad y la diferencia, y no se guarda nada.
2. **Given** una línea vigente con créditos vivos, **When** se cambia su tasa desde el mes siguiente,
   **Then** se crea una versión nueva y los créditos vivos conservan su tasa pactada.
3. **Given** que no hay usura registrada para el día, **When** alguien intenta simular o aprobar un
   crédito, **Then** el sistema lo bloquea, dice qué falta y alerta a quien registra la usura.
4. **Given** créditos vivos con tasa pactada mayor que una usura nueva, **When** empieza a regir esa
   usura, **Then** desde ese mismo día causan con la usura como tope, y se alerta.
5. **Given** una línea cuya tasa supera una usura nueva, **When** se registra esa usura, **Then** la
   línea queda cerrada a nuevos créditos hasta ajustarla, y se alerta.
6. **Given** la plantilla de líneas diligenciada, **When** se importa con un error en una fila, **Then**
   no se guarda ninguna y se informa fila, columna y motivo.

---

### User Story 2 - Simular, radicar y aprobar con atribuciones (Priority: P1)

El asesor simula con el asociado el plan de pagos y radica la solicitud con:
- sus documentos, codeudores y garantías;
- la preferencia del deudor para imputar abonos a capital (plazo o cuota).

Antes de analizarla, el sistema la valida sola:
- listas restrictivas;
- centrales de riesgo, con autorización registrada;
- capacidad de pago y de descuento;
- endeudamiento consolidado, cupo y concentración;
- puntaje.

La solicitud llega al nivel que le corresponde según línea, monto y condición. Analista, gerente,
comité o consejo deciden con acta, y quien radica no aprueba.

**Why this priority**: es el núcleo del negocio de crédito y donde se concentra el riesgo operativo y
regulatorio.

**Independent Test**: radicar tres solicitudes de montos distintos y comprobar que:
- cada una va al nivel correcto;
- el asesor que radicó no puede aprobar;
- el comité deja acta.

**Acceptance Scenarios**:

1. **Given** un asociado activo, **When** el asesor simula monto, plazo y periodicidad, **Then** ve, sin
   que se registre nada:
   - el plan cuota a cuota;
   - la tasa efectiva y la nominal;
   - el costo total;
   - la validación de usura.
2. **Given** una solicitud sin un documento obligatorio de la línea, **When** el asesor intenta enviarla a
   análisis, **Then** el sistema lo impide y lista lo que falta.
3. **Given** un solicitante que coincide con una lista restrictiva, **When** se radica, **Then** la
   solicitud queda bloqueada y el oficial de cumplimiento recibe la alerta. Sólo él la libera, con
   motivo.
4. **Given** un solicitante sin autorización de consulta a centrales registrada, **When** el analista pide
   la consulta, **Then** el sistema la impide y explica cómo registrar la autorización.
5. **Given** una solicitud cuyo monto consolidado supera la atribución del gerente, **When** se envía a
   aprobación, **Then** va al comité. El acta guarda asistentes, quórum, votos, decisión y condiciones.
6. **Given** que el asesor radicó una solicitud y también es miembro del comité, **When** el comité la
   decide, **Then** su voto no se admite.
7. **Given** una solicitud de un miembro del consejo en una entidad con actividad financiera, **When** se
   decide, **Then** exige el voto favorable de al menos 4/5 de la composición del consejo. El interesado
   no vota, pero la base de cálculo sigue siendo la composición.
8. **Given** un desembolso que llevaría al deudor por encima del límite individual de concentración,
   **When** se intenta aprobar, **Then** el sistema lo bloquea indicando el límite y el exceso.

---

### User Story 3 - Desembolsar y nacer con condiciones selladas (Priority: P1)

Aprobado el crédito y perfeccionadas sus garantías, se desembolsa:
- por transferencia, cheque o giro a un tercero;
- si el asociado lo pide, descontando del desembolso sus créditos abiertos.

El crédito nace con su número, sus condiciones pactadas y su plan de pagos sellados, el comprobante de
desembolso y el hecho para Contabilidad.

**Why this priority**: sin desembolso no hay cartera. Sellar las condiciones es lo que hace
reproducibles todas las liquidaciones posteriores.

**Independent Test**: desembolsar un crédito que recoge otro del mismo asociado y comprobar que:
- el recogido queda cancelado con su propio comprobante;
- el neto girado es el correcto;
- Contabilidad recibe los dos hechos.

**Acceptance Scenarios**:

1. **Given** un crédito aprobado con pagaré pendiente de firma, **When** se intenta desembolsar, **Then**
   el sistema lo impide hasta que el pagaré esté firmado y custodiado.
2. **Given** una aprobación vencida por plazo, **When** se intenta desembolsar, **Then** el sistema exige
   una nueva aprobación.
3. **Given** un desembolso que recoge un crédito vigente y al día del mismo asociado, **When** se
   confirma, **Then**:
   - el recogido queda cancelado con su comprobante de aplicación;
   - se informa el neto a girar;
   - los dos créditos quedan enlazados.
4. **Given** un desembolso que recoge un crédito en mora, **When** se simula, **Then** el sistema advierte
   que es una reestructuración y lo tramita como tal.
5. **Given** que la usura del día del desembolso es menor que la del día de aprobación, **When** la tasa
   aprobada la supera, **Then** el desembolso se bloquea con el mensaje de usura.
6. **Given** una transferencia de desembolso rechazada por el banco, **When** se registra el rechazo,
   **Then**:
   - el desembolso se reversa desde su origen, sin intereses causados;
   - se emite el hecho de reverso;
   - el crédito queda pendiente de un nuevo giro, o anulado si así se decide.

---

### User Story 4 - Causación diaria y liquidación a cualquier fecha (Priority: P1)

Cada día, sin que nadie lo ejecute, el sistema causa en cada crédito vivo:
- el interés corriente sobre el capital no vencido;
- la mora sobre el capital vencido;
- los seguros.

Al llegar a la altura de mora que fija la norma para la modalidad, lleva el interés en suspenso.
Cualquier usuario autorizado obtiene la liquidación de un crédito a una fecha pasada o futura,
desglosada por concepto. La misma liquidación, con la misma información registrada, da siempre el mismo
resultado.

**Why this priority**: es el corazón del cálculo financiero; de él dependen pagos, calificación,
deterioro, contabilidad y documentos.

**Independent Test**: liquidar un crédito a tres fechas (pasada, hoy, futura), repetir las
liquidaciones y compararlas al centavo. Dejar un día sin correr la causación y comprobar que se pone
al día sin duplicar.

**Acceptance Scenarios**:

1. **Given** un crédito al día, **When** corre la causación del día, **Then** se registra el interés
   corriente del día con la tasa vigente y la convención pactada, una sola vez aunque el proceso se
   repita.
2. **Given** un crédito con una cuota vencida, **When** corre la causación, **Then** la porción vencida
   causa sólo mora y la no vencida sólo interés corriente. La suma no supera la usura del día.
3. **Given** un crédito de consumo que alcanza la altura de mora de suspensión, **When** corre la
   causación, **Then**:
   - el interés pasa a suspenso;
   - Contabilidad recibe «causación suspendida»;
   - la liquidación a la fecha sigue incluyendo ese interés.
4. **Given** un día en que la causación no corrió, **When** corre al día siguiente, **Then** causa los dos
   días con la tasa de cada uno y sin duplicar.
5. **Given** un crédito, **When** se liquida a una fecha futura, **Then** se proyecta sin pagos
   intermedios y con la tasa conocida, y el documento dice hasta qué fecha es válido.
6. **Given** una liquidación a una fecha pasada hecha ayer, **When** se repite hoy indicando «con la
   información registrada hasta ayer», **Then** el resultado es idéntico al centavo, aunque hoy se haya
   registrado un pago con fecha valor anterior.

---

### User Story 5 - Aplicar y reversar pagos con prelación y simulación previa (Priority: P1)

Quien recauda (Recaudo, Nómina, Aportes, la pagaduría interna) lista las obligaciones del deudor y
elige una modalidad:
- pago total o cancelación;
- abono a cuotas vencidas;
- cuota del ciclo;
- pago anticipado de cuotas futuras;
- abono extraordinario a capital;
- pago a una cuota específica;
- valor libre.

Antes de confirmar ve la distribución exacta y el nuevo plan. Cartera aplica con la prelación de la
línea, devuelve el excedente al recaudador, que lo resuelve (FR-076), y deja su comprobante enlazado al
recibo. En un abono a
capital decide siempre el deudor, nunca la entidad. Un pago se reversa dejando el crédito exactamente
como si no hubiera existido.

**Why this priority**: sin pagos no hay recuperación de la cartera. Es la operación más frecuente y la
que más reclamos genera.

**Independent Test**: aplicar un abono extraordinario eligiendo reducir plazo, reversarlo y comprobar
que saldos, mora y calificación son idénticos a los de antes.

**Acceptance Scenarios**:

1. **Given** un crédito con dos cuotas vencidas, **When** se simula un valor libre, **Then** cubre
   completa la cuota más antigua antes de la siguiente. Dentro de cada cuota sigue el orden de la
   línea: gastos de cobranza informados, seguros, mora, corriente y capital.
2. **Given** un abono extraordinario, **When** se simula, **Then**:
   - se pregunta al deudor si reduce plazo o cuota, mostrando los dos planes;
   - su respuesta se aplica sin penalidad;
   - si el abono llega por un canal sin interacción, se usa la preferencia que registró, nunca una
     regla de la entidad.
3. **Given** un pago mayor que el total para cancelar, **When** se aplica, **Then** el crédito queda
   cancelado y el excedente vuelve al recaudador.
4. **Given** un pago ya aplicado, **When** el recaudador repite la misma operación con la misma clave,
   **Then** recibe el comprobante original y no se aplica dos veces.
5. **Given** un pago aplicado y otro posterior, **When** se reversa el primero, **Then** se muestra la
   nueva distribución del posterior antes de confirmar. Al confirmar, saldos, cuotas, días de mora,
   causación y calificación vigente quedan como si el primero no hubiera existido.
6. **Given** que entre simular y aplicar corrió la causación o entró otro pago, **When** se confirma,
   **Then** el sistema avisa que la distribución cambió y muestra la nueva antes de aplicar.
7. **Given** un pago anticipado de tres cuotas futuras, **When** se simula, **Then**:
   - se informa que el anticipo cubre cada cuota en su vencimiento y no reduce los intereses;
   - se ofrece el abono a capital como alternativa.
8. **Given** un ambiente de prueba sin Recaudo, **When** se ejercita el contrato de obligaciones,
   **Then** un recaudador de prueba, claramente identificado, lista, simula, aplica, reversa y resuelve
   saldos a favor. Ese recaudador no se puede registrar en un despliegue de producción (FR-089).

---

### User Story 6 - Ventas a crédito de Comercial sobre un cupo otorgado (Priority: P1)

El asociado o cliente tiene un **cupo rotativo** otorgado por originación, con su evaluación y sus
autorizaciones. El flujo es:
1. Antes de vender a crédito, Comercial consulta el cupo, el estado y las líneas aplicables.
2. Al confirmar, reserva el número del crédito, que la factura lleva como referencia.
3. Envía «venta a crédito registrada».
4. Cartera crea el crédito sin salida de dinero (la factura es el desembolso) y responde «obligación
   creada» con el número y su estado.

Cada nota de Comercial ajusta el saldo y recalcula el plan.

**Why this priority**: Comercial (012) ya vende a crédito con un crédito provisional y acumula mensajes
que esperan a Cartera. Sin esta historia, las ventas a crédito de COOFLOPAL no tienen cartera.

**Independent Test**: vender a crédito a un asociado con cupo y comprobar:
- el crédito existe en Cartera en menos de cinco segundos, con la referencia de la factura;
- la factura lleva el número reservado (si hubo reserva, D26);
- tras una nota crédito, el saldo y el plan son los nuevos.

**Acceptance Scenarios**:

1. **Given** un asociado con cupo rotativo disponible, **When** Comercial consulta cupo y estado por un
   valor, **Then** recibe en menos de un segundo el disponible, el estado y las líneas aplicables, con
   su plazo máximo, cuotas máximas y periodicidades.
2. **Given** una venta a crédito que se va a confirmar, **When** Comercial reserva el número, **Then**
   recibe el número ligado a ese documento de venta. Repetir la reserva del mismo documento devuelve el
   mismo número.
3. **Given** una venta a crédito confirmada en modo en línea, **When** Cartera recibe el mensaje,
   **Then** el crédito existe en menos de cinco segundos con el número reservado, la fecha y la
   referencia de la factura, y Comercial recibe «obligación creada».
4. **Given** una venta que llegó pendiente de validar y no pasa la evaluación a la fecha de la venta,
   **When** Cartera la procesa, **Then**:
   - la obligación se crea marcada «validación fallida»;
   - se bloquea crédito nuevo al deudor;
   - queda una alerta en Comercial y en Cartera;
   - la venta no se anula.
5. **Given** una nota crédito por devolución parcial, **When** llega el ajuste, **Then** baja el capital,
   se reversa el interés causado sobre la parte devuelta y el plan se recalcula según la línea.
6. **Given** ventas a crédito confirmadas hasta el corte, **When** se aplica la carga de migración,
   **Then**:
   - se concilian por factura como migradas, canceladas antes del corte o no migradas con saldo;
   - las no migradas con saldo impiden activar hasta incorporarlas con una carga nueva o aceptarlas con
     acta;
   - al activar, Comercial recibe:
     - «obligación creada», con su número y su estado, de cada venta migrada o cancelada antes del corte;
     - «obligación rechazada» de cada venta aceptada sin migrar;
   - ninguna se procesa como mensaje en vivo.
7. **Given** el mismo mensaje entregado dos veces, **When** llega de nuevo, **Then** Cartera responde lo
   mismo que la primera vez y no crea otro crédito.

---

### User Story 7 - Contabilidad recibe cada hecho de Cartera sin que Cartera escriba asientos (Priority: P1)

Cada operación que cambia un saldo, o la categoría de un saldo, emite un hecho de negocio. El hecho
lleva:
- línea, modalidad y sub-segmento;
- calificación y concepto;
- oficina, tercero y crédito.

Contabilidad lo convierte en comprobante con la matriz de Cartera de su lado y el modo de paso de cada
hecho: en línea, por lotes o no pasa. Una conciliación compara los saldos de Cartera con los contables
a cualquier fecha.

**Why this priority**: la cartera es el activo principal de una cooperativa de crédito. Sin cuadre
contable no hay estados financieros ni cierre.

**Independent Test**: desembolsar, causar, pagar y reversar en un día, cerrar el mes y comprobar:
- los comprobantes generados según la matriz;
- la reclasificación por categoría;
- una conciliación sin diferencias.

**Acceptance Scenarios**:

1. **Given** la matriz completa para una línea, **When** se desembolsa, **Then** Contabilidad genera el
   comprobante con las cuentas de esa línea, modalidad, sub-segmento y concepto, y el crédito como
   documento cruce.
2. **Given** una combinación sin regla en la matriz, **When** se va a confirmar una operación, **Then** la
   validación previa lo advierte o lo bloquea según el parámetro, diciendo qué regla falta.
3. **Given** la causación diaria en modo por lotes con comprobante resumido, **When** corre el lote,
   **Then** Contabilidad recibe un comprobante por día que conserva todas las dimensiones de la matriz y
   la lista de créditos.
4. **Given** un crédito que pasa de A a C en el cierre del mes, **When** se cierra, **Then** Contabilidad
   recibe la reclasificación de su capital y de sus intereses de la categoría A a la C.
5. **Given** mensajes pendientes o rechazados, **When** se concilia, **Then** cada diferencia se explica
   con los mensajes que la causan.
6. **Given** una venta en la que Contabilidad registró la cuenta por cobrar, **When** se crea su crédito,
   **Then** esa cuenta por cobrar se reclasifica a cartera de créditos, sin duplicarla.

---

### User Story 8 - Migrar la cartera vigente y cuadrar antes de activar (Priority: P1)

La cooperativa carga sus créditos vigentes con una plantilla que incluye:
- condiciones pactadas y cuotas pendientes;
- saldos por concepto al corte, incluido el interés en suspenso;
- días de mora, calificación, deterioro y datos de reestructuración;
- historial de mora;
- garantías, codeudores, pagaré, pagaduría y cartera castigada.

Cartera valida fila a fila, cuadra contra Contabilidad a la fecha de corte, crea un cierre base y sólo
entonces se activa.

**Why this priority**: sin migración no hay salida en vivo. Un descuadre inicial contamina todos los
cierres siguientes.

**Independent Test**: cargar 100 créditos con un descuadre deliberado y comprobar:
- no se puede activar hasta corregirlo o aceptarlo con acta;
- tras activar, la liquidación de cada uno coincide con los saldos cargados;
- el primer cierre no vuelve a reconocer el deterioro migrado.

**Acceptance Scenarios**:

1. **Given** la plantilla con una fila cuyo plan no suma el saldo de capital, **When** se revisa, **Then**
   se marca esa fila y columna y no se aplica nada.
2. **Given** la carga aplicada, **When** se compara con Contabilidad a la fecha de corte, **Then** se ve
   el cuadre por concepto, modalidad, sub-segmento, categoría y cuenta, con sus diferencias.
3. **Given** diferencias sin aceptar, **When** se intenta activar, **Then** el sistema lo impide.
4. **Given** la activación, **When** queda hecha, **Then**:
   - cada crédito nace con una transacción de saldo inicial por concepto, sin hechos contables;
   - existe un cierre base con la calificación y el deterioro migrados.
5. **Given** el primer cierre mensual después de activar, **When** se calcula el deterioro, **Then** el
   hecho emitido es sólo la diferencia frente al cierre base.
6. **Given** las tablas de crédito heredadas, **When** se despliega el módulo, **Then** se retiran tras
   un diagnóstico que confirma que no tienen datos, sin tocar ahorros, aportes ni caja.
7. **Given** una cooperativa de producción sin un recaudador Recaudo o sin una Tesorería registrados con la
   versión vigente del contrato y de la orden de giro, **When** se intenta activar Cartera, **Then** la
   activación se niega y dice qué falta (FR-142).
8. **Given** una cooperativa de producción obligada a la pérdida esperada según su perfil (FR-018), sin
   K6a desplegada, **When** se intenta activar Cartera, **Then** la activación se niega y dice que falta
   el modelo de pérdida esperada (FR-142).
9. **Given** una carga aplicada con corte en una fecha, **When** se intenta activar con una fecha que no es
   el día siguiente al corte, **Then** se niega con el motivo. Mientras no se active, una carga nueva, con
   el mismo corte o con otro, reemplaza a la anterior, que queda «reemplazada» como ensayo.
10. **Given** una cooperativa obligada (desde K6a), sin parámetros de FR-106 vigentes o con créditos
    migrados de segmentos cubiertos sin historial de mora completo, **When** se intenta activar, **Then**
    se niega y lista lo que falta.

---

### User Story 9 - Calificación, arrastre y deterioro con cierre mensual (Priority: P1)

Cada día el sistema muestra la calificación informativa de cada crédito. Al cerrar el mes la calcula
oficialmente y la congela, junto con:
- el arrastre por modalidad;
- las reglas de reestructurados y de pago único;
- las subcategorías;
- el deterioro individual y general;
- los hechos de deterioro y de reclasificación;
- el reporte de cambios.

El cierre debe cuadrar con Contabilidad. El modelo de deterioro depende del tipo de entidad y del
segmento (FR-105): el Anexo 1 desde K1 y la pérdida esperada desde K6a, porque el tipo de entidad la
exige o por adopción voluntaria.

**Why this priority**: el primer cierre mensual llega a los 30 días de salir en vivo y es obligatorio.

**Independent Test**: preparar créditos en cada rango de mora de cada modalidad, cerrar el mes y
comparar calificación y deterioro con un cálculo hecho a mano. Desde K6a, repetir el cierre con un
segmento en pérdida esperada, comparado también con un cálculo a mano.

**Acceptance Scenarios**:

1. **Given** un deudor con un crédito de consumo en C, otro de consumo en A y uno comercial en A,
   **When** se cierra el mes, sin excepción de aportes aplicable, **Then**:
   - los dos de consumo quedan en C por arrastre;
   - el comercial sigue en A.
2. **Given** el cierre de septiembre hecho, **When** se registra después un pago con fecha valor de
   septiembre, **Then** el cierre de septiembre no cambia y el efecto se reconoce en octubre.
3. **Given** el cierre del mes, **When** termina, **Then** el reporte lista los créditos que cambiaron de
   categoría, la categoría anterior, la nueva y la causa.
4. **Given** un analista que califica un crédito a una categoría peor por análisis, **When** guarda,
   **Then** se exige motivo. Ninguna calificación por análisis puede ser mejor que la que resulta de las
   reglas.
5. **Given** un crédito de pago único, **When** pasa de los días de mora que la norma fija para ese
   tipo, **Then** se califica en la categoría correspondiente aunque la tabla general dijera otra.
6. **Given** una cooperativa de un tipo obligado al modelo de referencia (desde K6a), **When** se cierra
   el mes, **Then**:
   - consumo con libranza, consumo sin libranza y comercial de persona natural se deterioran por pérdida
     esperada;
   - los demás segmentos, por el Anexo 1;
   - intentar dejar uno de esos tres segmentos en el Anexo 1 se rechaza citando la norma.
7. **Given** una cooperativa no obligada que registra la adopción voluntaria con acta, estudio de impacto
   y aviso a la Superintendencia (desde K6a), **When** se cierra el primer mes de su vigencia, **Then**:
   - los segmentos cubiertos usan la pérdida esperada y los demás el Anexo 1;
   - el cierre anterior no cambia;
   - el hecho de deterioro es la diferencia frente a él.

   Si después desiste con acta y aviso, vuelve al Anexo 1 desde la fecha registrada, sin recalcular
   cierres.
8. **Given** una cooperativa activa a la que se le registra una vigencia del perfil que la obliga, sin K6a
   (desde K1), **When** se cierra el mes, **Then** cierra con el Anexo 1, la marca «modelo obligatorio no
   disponible» y la alerta (D28).
9. **Given** la misma situación con K6a, **When** se cierra el primer mes de la vigencia, **Then**:
   - los segmentos cubiertos usan la pérdida esperada;
   - los cierres anteriores no cambian;
   - se listan los créditos sin historial de mora completo, y su deterioro aplica la regla de meses
     faltantes (FR-105).

---

### User Story 10 - Libranza: pagadurías, archivo de descuentos y no aplicados (Priority: P2)

Cartera mantiene las pagadurías con convenio y valida al originar la capacidad de descuento y la
prelación. Cada período, con la antelación que fija el convenio, genera para cada pagaduría el archivo de
descuentos en su formato.

Lo que la pagaduría ya descontó y aún no gira no se le imputa al deudor como mora. Cuando la pagaduría
responde, Recaudo aplica lo descontado. Cartera gestiona lo no aplicado y traslada la libranza si el
deudor cambia de empleador. La nómina propia de la cooperativa funciona como una pagaduría más.

**Why this priority**: la libranza es la forma de pago dominante en fondos de empleados y en muchas
cooperativas; sin ella la mora se dispara el primer mes.

**Independent Test**: generar el archivo de una pagaduría con 20 obligaciones, procesar una respuesta
con tres no aplicados y comprobar que esos tres quedan identificados, con motivo y en gestión.

**Acceptance Scenarios**:

1. **Given** una solicitud por libranza cuyo descuento total supera el tope legal sobre el salario o la
   pensión después de los descuentos de ley, **When** se valida, **Then** se rechaza indicando la
   capacidad disponible.
2. **Given** el período de una pagaduría, **When** se genera el archivo, **Then** lleva las cuotas
   exigibles del período en el formato vigente del convenio, queda inmutable con su huella y se entrega
   a Recaudo. Queda «enviado» cuando Recaudo confirma el envío (FR-113).
3. **Given** una cuota que la pagaduría informó como descontada y aún no ha girado, **When** pasa el
   vencimiento, **Then**:
   - el deudor no queda en mora ni cambia su calificación mientras la mora de la pagaduría no supere el
     umbral configurado;
   - la mora se gestiona ante la pagaduría.
4. **Given** la respuesta con no aplicados, **When** se procesa, **Then** cada uno queda con su motivo
   (retiro, saldo insuficiente, prelación de otro descuento, incapacidad u otro) y la acción siguiente.
5. **Given** un deudor que cambia de empleador, **When** se registra la nueva pagaduría, **Then** la
   libranza se traslada conservando su fecha original para la prelación.
6. **Given** un empleado de la cooperativa con crédito, **When** se liquida la nómina del período,
   **Then** Nómina obtiene la cuota a descontar por la consulta, y al aprobar la nómina el descuento se
   aplica por el contrato.

---

### User Story 11 - Novedades y condonaciones con aprobación (Priority: P2)

El analista tramita estas novedades:
- reestructuración;
- modificación;
- refinanciación;
- novación;
- prórroga;
- cambio de fecha de pago;
- cambio de tasa;
- condonación de intereses o cargos.

El sistema clasifica cada novedad por su **causa**: si la motiva el deterioro de la capacidad de pago, es
una reestructuración, cualquiera sea el mecanismo. Cada novedad tiene simulación del nuevo plan,
consentimiento de codeudores y garantes, aprobación por atribución con acta cuando el nivel lo exige, y
el efecto en la calificación que fija la norma.

**Why this priority**: son frecuentes en mora y, sin control, generan maquillaje de cartera, que la
Superintendencia vigila.

**Independent Test**: reestructurar un crédito en C y comprobar:
- la marca de reestructurado y el nuevo plan;
- que la calificación no mejora;
- el consentimiento del codeudor y el hecho para Contabilidad.

**Acceptance Scenarios**:

1. **Given** un crédito en mora, **When** se simula una prórroga solicitada por dificultades de pago,
   **Then** el sistema la clasifica como reestructuración y muestra el nuevo plan y la calificación
   resultante.
2. **Given** una reestructuración aprobada sin la aceptación firmada del codeudor, **When** el
   aprobador decide, con motivo, **Then** hay dos salidas:
   - no aplicarla;
   - aplicarla con esa garantía extinguida, sólo si la cobertura restante cumple la línea o se aprueba
     la excepción. Cobertura y deterioro se recalculan.
3. **Given** una reestructuración aplicada, **When** queda hecha, **Then**:
   - **por prórroga, cambio de fecha o de tasa**: el crédito conserva su número e historial, y el plan
     anterior queda como versión anterior;
   - **por novación o refinanciación**: nace un crédito enlazado que hereda la marca, el conteo, la
     calificación y el historial de mora;
   - en los dos casos queda marcado como reestructurado con su conteo.
4. **Given** un crédito reestructurado, **When** el deudor paga cumplidamente, **Then** la calificación
   mejora sólo una categoría por cada tramo de cuotas consecutivas que fije la norma. Si vuelve a caer
   en mora, regresa a la calificación que tenía al reestructurarse.
5. **Given** una condonación de intereses de mora que supera la atribución del gerente, **When** se envía
   a aprobación, **Then** va al nivel siguiente y se exige motivo.

---

### User Story 12 - Cobranza por etapas que respeta la ley (Priority: P2)

Los créditos avanzan por etapas según sus días de mora: preventiva, administrativa, prejurídica y
jurídica. Se asignan a gestores, que tienen agenda, registran gestiones y promesas, y pactan acuerdos de
pago. Las comunicaciones cumplen tres reglas:
- los mensajes salen sólo por los canales que el deudor autorizó, dentro de los horarios y
  frecuencias de la Ley 2300;
- nunca se contacta a referencias personales;
- antes de cada reporte negativo a centrales sale el aviso previo de la Ley 1266.

**Why this priority**: la recuperación depende de la gestión, y el incumplimiento de la Ley 2300 o de la
1266 genera sanciones y reclamos.

**Independent Test**: programar un mensaje para un domingo y comprobar que sale en el siguiente día y
hora permitidos.
Dar un crédito por llegar a reporte negativo sin aviso con constancia y comprobar que no se reporta.

**Acceptance Scenarios**:

1. **Given** un crédito que pasa de 30 días de mora, **When** corre el proceso diario, **Then** pasa a la
   etapa configurada y se asigna a un gestor según las reglas.
2. **Given** un contacto directo con el deudor por teléfono el lunes, **When** el gestor intenta enviarle
   un WhatsApp el miércoles de la misma semana calendario, **Then** el sistema lo impide porque ya se
   usó otro canal esa semana.
3. **Given** una promesa de pago para el viernes, **When** llega el pago, **Then** la promesa queda
   cumplida sola. Si no llega, queda incumplida y vuelve a la agenda.
4. **Given** un deudor que no autorizó WhatsApp, **When** se programa una campaña por ese canal, **Then**
   se excluye al deudor y queda constancia del motivo.
5. **Given** un deudor que ya fue reportado negativo, se puso al día y vuelve a caer en mora, **When** se
   acerca el nuevo reporte, **Then** se exige un nuevo aviso previo para este episodio.

---

### User Story 13 - Documentos al deudor (Priority: P2)

El asesor entrega o envía, en un paso:
- el plan de pagos;
- el extracto de un período;
- el paz y salvo;
- el certificado de saldo a una fecha;
- el certificado tributario del año.

Cada documento lleva número, fecha, firma y un código de verificación.

**Why this priority**: son las solicitudes más frecuentes de los asociados en oficina.

**Independent Test**: pedir un paz y salvo para un deudor con saldo y comprobar que se niega; pedirlo
para uno sin saldo y comprobar que se emite con su código.

**Acceptance Scenarios**:

1. **Given** un deudor con una obligación viva, **When** se pide paz y salvo general, **Then** se niega y
   se indica qué obligación tiene saldo.
2. **Given** un certificado emitido, **When** un usuario de la cooperativa verifica su código, **Then**
   obtiene si es auténtico o está anulado, y sus datos esenciales.
3. **Given** el certificado tributario, **When** se emite, **Then** muestra:
   - los intereses pagados en el año por crédito, con los de vivienda separados;
   - el saldo de la deuda al 31 de diciembre.
4. **Given** un deudor sin obligaciones vivas que tiene un mensaje de venta en reproceso, **When** se pide
   paz y salvo general, **Then** se niega e indica la factura. Si en cambio tiene una venta aceptada sin
   migrar en la activación, el paz y salvo se emite y la menciona como fuera de Cartera.

---

### User Story 14 - Castigo y seguimiento de la cartera castigada (Priority: P3)

El jefe de cartera propone castigar los créditos que cumplen los requisitos de la norma y de la
política de la cooperativa:
- el deudor ya no es asociado y sus aportes ya se cruzaron;
- hay informes motivados y un concepto de irrecuperabilidad;
- las gestiones están agotadas.

El consejo aprueba el castigo con acta. La cartera castigada sigue en cobranza, y sus recuperaciones se
registran y se ven por separado.

**Why this priority**: se hace pocas veces al año y requiere aprobación del consejo, pero sin él la
cartera queda inflada.

**Independent Test**: proponer un lote de castigo, aprobarlo con acta y aplicar un pago de recuperación
sobre un castigado.

**Acceptance Scenarios**:

1. **Given** un deudor que sigue siendo asociado, **When** se intenta incluir su crédito en un castigo,
   **Then** se rechaza y se dice qué requisito falta.
2. **Given** el lote aprobado por el consejo, **When** se aplica, **Then**:
   - cada crédito queda castigado con referencia al acta;
   - Contabilidad recibe «crédito castigado»;
   - si la entidad está obligada a reportar castigos, se alerta el plazo de reporte a la
     Superintendencia.
3. **Given** un pago a un crédito castigado, **When** se aplica, **Then** queda como recuperación de
   cartera castigada y se ve en el seguimiento.

---

### User Story 15 - Reportes de gestión, cosechas y tablero (Priority: P3)

Gerencia y jefatura de cartera consultan:
- la cartera por edades;
- la colocación y el recaudo por línea;
- los indicadores de calidad, las cosechas y la concentración.

Tienen además un tablero de indicadores por oficina y línea, limitado a las oficinas que cada usuario
puede ver.

**Why this priority**: son de gestión y no bloquean la operación.

**Independent Test**: generar la cosecha de los créditos de seis meses y comparar la mora a 30, 60 y 90
días con un cálculo manual.

**Acceptance Scenarios**:

1. **Given** un usuario con alcance de una oficina, **When** abre el tablero, **Then** sólo ve cifras de su
   oficina.
2. **Given** un deudor cerca del límite de concentración individual, **When** se genera el reporte de
   concentración, **Then** aparece señalado con su porcentaje del límite.

---

### User Story 16 - Reportes regulatorios y centrales de riesgo (Priority: P3)

Al cierre de cada período, el contador genera:
- el formato de cartera de créditos del SICSES, validado antes de entregarlo;
- los archivos para las centrales de riesgo, que incluyen sólo a quien autorizó el reporte y, si el
  dato es negativo, a quien recibió el aviso previo de ese episodio.

**Why this priority**: es obligatorio pero periódico, y se carga a mano en los portales.

**Independent Test**: generar el formato con un crédito con un dato inválido y comprobar que la
validación lo señala antes de generar.

**Acceptance Scenarios**:

1. **Given** el cierre del mes hecho, **When** se genera el formato de cartera, **Then** sale en el layout
   vigente, con las validaciones de forma aplicadas. Si el layout no se ha cotejado, sale con la
   advertencia.
2. **Given** un deudor con dato negativo sin aviso previo con constancia para ese episodio, **When** se
   genera el archivo de centrales, **Then** ese dato negativo se excluye y queda en una lista para
   gestión.

---

### User Story 17 - Seguridad, atribuciones, habeas data y auditoría verificable (Priority: P1)

Cada acción exige su permiso y respeta el alcance del usuario: oficina, línea y rango de montos. Las
aprobaciones siguen las atribuciones por niveles, y quien radica no aprueba. Ninguna consulta ni reporte
a centrales ocurre sin la autorización del titular registrada. Todo queda en una auditoría inalterable y
verificable, que auditor y revisor fiscal consultan sólo en lectura.

**Why this priority**: es transversal y condición para cualquier salida en vivo de un módulo financiero
vigilado.

**Independent Test**: con un usuario de una oficina y atribución hasta un monto, intentar ver un
crédito de otra oficina y aprobar uno por encima de su monto. Los dos intentos deben fallar y quedar
auditados.

**Acceptance Scenarios**:

1. **Given** un usuario sin permiso de desembolso, **When** intenta desembolsar por pantalla o por
   llamada directa, **Then** la operación no existe para él y el intento queda auditado.
2. **Given** un revisor fiscal, **When** consulta créditos, actas y auditoría, **Then** ve todo y no puede
   modificar nada.
3. **Given** la auditoría de un mes, **When** se verifica su integridad, **Then** cualquier alteración o
   borrado se detecta y se informa.
4. **Given** un deudor sin autorización de reporte a centrales registrada, **When** se genera el archivo
   de centrales, **Then** no se incluye y queda en una lista para gestión.
5. **Given** un titular que pide revocar su autorización mientras tiene una obligación vigente, **When**
   se registra la solicitud, **Then** se le responde, según la regla configurada, que la revocación no
   procede mientras exista el deber contractual. La solicitud y la respuesta quedan registradas.

### Edge Cases

**Tasas y usura**
- **La usura baja por debajo de la tasa de un crédito vivo.** Desde ese día la causación usa la menor
  entre la pactada y la usura vigente. Fija o variable, nunca se cobra por encima. La línea queda
  cerrada a nuevos créditos hasta ajustarla.
- **Se corrige una usura ya registrada.** La corrección es una versión nueva con su fecha de registro.
  Las liquidaciones «con la información hasta R» siguen siendo reproducibles. Si la corregida es menor,
  lo cobrado de más se devuelve como saldo a favor.
- **La convención real/360 aplicada a una tasa efectiva anual** sube el costo efectivo. El control de
  usura compara la tasa efectiva que resulta de la convención y de los cargos, no la tasa nominal
  pactada.
- **Índice de un crédito variable.** El reajuste usa el último índice publicado a la fecha de reajuste,
  y es definitivo. No hay reajustes provisionales ni cobros retroactivos.

**Fechas y cálculo**
- **Una cuota vence en día no hábil.** Se puede pagar el día hábil siguiente sin mora (Código de
  Comercio). El interés corriente de esos días se causa normalmente y se cobra con la cuota siguiente.
- **Febrero, meses de 31 días y años bisiestos.** Se resuelven con la convención sellada en el crédito.
  Con 30/360:
  - el día 31 no causa;
  - el último día de febrero causa los días que faltan para completar 30.
- **Diferencias entre el plan y la causación diaria.** El interés de cada cuota es el causado entre
  vencimientos, y su capital es la cuota menos ese interés. La última cuota absorbe la diferencia. La
  suma del capital del plan es igual al monto desembolsado, al centavo.
- **Período de gracia.** Los intereses de la gracia no se capitalizan (no hay anatocismo). Se pagan en
  la primera cuota o se distribuyen en las siguientes, según la línea.
- **Saldos residuales.** Un saldo vencido menor que la tolerancia configurada (por ejemplo, centavos)
  no cuenta días de mora.

**Pagos y reversos**
- **Pago con fecha valor anterior, incluso de un mes cerrado.** Por ejemplo, una libranza consignada el
  30 y aplicada el 2. Cuenta para intereses y mora desde su fecha valor, con el límite de días y la
  atribución configurados. Su fecha contable es la del período abierto. El cierre de ese mes no se
  reescribe, y la diferencia se reconoce en el mes abierto.
- **Reverso de un pago de un mes cerrado.** El crédito vuelve a su estado sin ese pago, también para
  liquidaciones a fechas de ese mes con la información posterior. Lo cerrado no se reescribe.
- **Reverso de un pago que canceló el crédito.**
  - El crédito se reabre con sus cuotas, su mora y su calificación.
  - Pagaré y garantías no se liberaron, porque la liberación espera el plazo de firmeza.
  - El paz y salvo y los certificados emitidos después se verifican como anulados.
- **Reverso de un pago cuyo excedente ya se devolvió.** Cartera reversa lo que aplicó. El excedente lo
  resuelve el recaudador que lo devolvió.
- **Un pago llega mientras corre la causación del mismo crédito.** Una operación espera a la otra.
  Ninguna se pierde ni se duplica.
- **Dos recaudadores aplican a la vez a la misma obligación.** Una se aplica. La otra recibe «la
  distribución cambió» y se re-simula.
- **Abono extraordinario mayor que el saldo de capital.** Se trata como cancelación, y el excedente
  vuelve al recaudador.
- **Abono con cuotas vencidas.** Primero cubre lo vencido y luego se aplica a capital.

**Créditos que nacen en Comercial**
- **Nota crédito mayor que el saldo**, porque el deudor ya pagó. El saldo queda en cero y la diferencia
  es un saldo a favor del deudor. Queda visible en el listado del contrato (FR-084) para que Recaudo lo
  devuelva o lo aplique (FR-083).
- **Ajuste que llega antes que su venta.** Espera a su venta, según el orden de la cadena.
- **Venta anterior a la activación.** Entra sólo por la migración, identificada por su factura. Su
  mensaje acumulado se da por procesado; nunca se crea dos veces. Si al corte ya tenía saldo cero
  (pagada por fuera, anulada o cubierta por notas), se carga o se marca como «cancelada antes del
  corte», sin obligación viva ni hechos (FR-143).
- **Número de crédito reservado y venta descartada.**
  - Comercial libera la reserva. Si no lo hace en los días configurados, se libera sola, con alerta.
  - El número queda anulado y visible, y el cupo reservado vuelve al disponible.
  - Los números de crédito no tienen que ser continuos.
- **La consulta de cupo no responde a tiempo.** Comercial aplica su política «sin respuesta» (012). La
  venta llega después como mensaje pendiente de validar y se evalúa a su fecha.

**Deudores, codeudores y garantías**
- **El deudor se retira de la cooperativa.** Sus créditos siguen vivos y se bloquea su cupo. El proceso
  de retiro cruza aportes contra deudas por el contrato de obligaciones.
- **El deudor fallece.**
  - Se bloquean cobranza, mensajes y reporte negativo a centrales mientras la reclamación al seguro de
    vida deudores está en trámite.
  - La causación continúa, salvo que la regla configurada diga otra cosa.
  - El pago de la aseguradora entra por el contrato.
- **Un codeudor es a la vez deudor en mora.** El endeudamiento consolidado lo incluye como deudor y
  como codeudor. El arrastre no alcanza a sus codeudas.
- **Un garante no consiente una prórroga o reestructuración.** El aprobador decide con motivo:
  - no aplicarla;
  - o aplicarla con esa garantía extinguida, si la cobertura restante cumple la línea o se aprueba la
    excepción. Cobertura y deterioro se recalculan.
- **Una garantía vence** (póliza, avalúo): se alerta a cartera antes del vencimiento, según los días
  configurados.

**Procesos y cierres**
- **El proceso diario no corrió en varios días.** Se pone al día en orden, un día a la vez, sin duplicar,
  y recalcula la calificación diaria.
- **Reabrir un cierre que no es el último, o cuyo período ya cerró Contabilidad.** Se rechaza.
- **Un mensaje a Contabilidad falla.** Queda visible en la bandeja con su motivo y se reintenta. La
  operación de Cartera no se deshace.

**Cobranza**
- **Mensaje programado fuera de horario o tras el tope de contactos.** Se corre a la siguiente ventana
  permitida y queda constancia.
- **Un deudor pide no ser contactado por un canal.** Se retira esa autorización y la campaña lo excluye.

**Habeas data**
- **Reclamo del titular.** Se marca «reclamo en trámite». El archivo de centrales lo reporta con esa
  marca y el plazo legal corre.
- **Reclamo vencido sin respuesta.** Se trata como aceptado, según la ley.
- **Suplantación alegada.** Sigue su propio trámite legal, con su leyenda en el reporte.

## Requirements *(mandatory)*

### Functional Requirements

#### Transversales

- **FR-001 · Saldos sólo por transacciones (R1).** El saldo de cada concepto de cada crédito debe ser
  siempre el resultado de sus transacciones:
  - ninguna pantalla, importación ni proceso cambia un saldo sin una transacción documentada;
  - cada transacción registra tipo, sus tres fechas (valor, contable y registro), concepto, valor,
    actor, origen, documento y, cuando aplica, motivo;
  - el historial de un crédito sólo crece.
- **FR-002 · Reverso en vez de borrado (R2).**
  - Una transacción confirmada no se edita ni se borra por ninguna vía de la aplicación.
  - Se corrige con una transacción contraria enlazada a la original.
  - Un reverso no se reversa: para rehacer, se registra una operación nueva.
  - La cadena original → reverso es visible desde los dos lados.
- **FR-003 · Auditoría inalterable y verificable (R3).** Toda acción debe quedar en la auditoría
  encadenada de la cooperativa, incluidas las fallidas y rechazadas, con:
  - actor (persona, o el proceso de la cooperativa), fecha UTC y oficina;
  - origen: IP, canal y dispositivo;
  - entidad, valores antes y después, motivo y clave de idempotencia.

  También se auditan las lecturas sensibles: consulta a centrales, liquidaciones y documentos emitidos,
  y exportaciones. La auditoría se conserva diez años (el mínimo constitucional es cinco). Su
  integridad se puede verificar en cualquier momento, y una alteración o un borrado se detectan.
- **FR-004 · Permisos con alcance (R4).**
  - Cada acción exige un permiso propio, y el catálogo de permisos de Cartera queda sembrado.
  - Cada usuario tiene, además, un alcance:
    - las **oficinas** cuyos créditos ve y opera;
    - las **líneas** que opera;
    - el **rango de montos** de sus atribuciones.
  - Fuera de su permiso o alcance, la operación **no existe** para él, igual que en el resto del ERP.
  - Ese alcance rige para las pantallas y rutas propias de Cartera. Las interfaces que usan otros
    módulos (consultas y contrato) se autorizan con el permiso del módulo que llama, y no las limita el
    alcance de oficina, línea ni monto de Cartera.
  - Contador, auditor y revisor fiscal leen todo. El contador además genera los reportes regulatorios y
    los archivos de centrales.
- **FR-005 · Segregación de funciones (R4).**
  - Quien radica una solicitud no la aprueba ni vota sobre ella.
  - La cooperativa configura otras incompatibilidades, por ejemplo aprobar y desembolsar el mismo
    crédito.
  - La comparación es por **persona**, aunque tenga varios roles o sesiones.
- **FR-006 · Habeas data (R4: Leyes 1581, 1266 y 2157).**
  - **Autorización previa.** Cartera exige la autorización del titular (deudor y codeudores) antes de
    consultar o reportar a centrales y antes de tratar sus datos para el crédito. Se registra con
    fecha, canal, evidencia y la versión del texto autorizado. Sin ella, la operación se bloquea y se
    explica cómo obtenerla.
  - **Revocación.** Se registra la solicitud. No procede mientras exista un deber legal o contractual de
    conservar o reportar el dato, y se responde así con constancia.
  - **Autorizaciones migradas.** Llevan la evidencia que tenga la cooperativa. Si no hay evidencia, el
    crédito sigue vivo y se cobra; sólo se bloquean la consulta y el reporte a centrales y los mensajes
    de cobranza, hasta obtenerla.
  - **Derechos del titular.** Se atienden y registran las solicitudes de conocer, actualizar, rectificar
    y suprimir, con su plazo configurado.
  - **Reclamos sobre el reporte.**
    - Llevan la marca «reclamo en trámite» y los plazos legales: 15 días hábiles prorrogables 8.
    - Un reclamo vencido sin respuesta se tiene por aceptado.
    - La suplantación sigue su trámite propio.
  - **Permanencia del dato negativo.** Su permanencia y su actualización tras el pago siguen los plazos
    legales, que son parámetros con fuente.
- **FR-007 · Parametrización con vigencia (R5).** Todo parámetro de Cartera se versiona con fecha desde
  y conserva su historial: líneas, tasas, índices, usura, plazos, prelación, calificación, deterioro,
  cobranza, plantillas y formatos.
  - Cada versión guarda también **cuándo se registró**, para reproducir resultados «con la información
    hasta R».
  - Un cambio nunca altera lo ya liquidado o cerrado.
  - Un crédito usa sus **condiciones pactadas** selladas al desembolsar.
  - Para las reglas regulatorias (calificación, deterioro, suspensión, usura), usa el valor vigente en
    cada fecha valor.
- **FR-008 · Desacoplamiento (R6).**
  - Cartera se comunica con otros módulos **sólo** por las interfaces de la sección «Interfaces que
    Cartera ofrece y consume»: mensajes de negocio versionados y consultas y contratos definidos.
  - No lee ni escribe datos de otro módulo, y ningún módulo lee ni escribe los de Cartera.
  - Una verificación automática fija la lista exacta de interfaces y falla ante cualquier otro acceso.
- **FR-009 · Idempotencia (R7).**
  - Todo desembolso, pago, reverso, reserva de número, mensaje recibido y operación del contrato de
    obligaciones lleva una clave única de su origen.
  - Repetir la clave devuelve el resultado original sin duplicar nada.
  - La misma clave con contenido distinto se rechaza con un mensaje claro.
- **FR-010 · Precisión y convenciones (R8).**
  - Todo cálculo financiero se hace en decimal exacto, sin aproximaciones intermedias.
  - Son parámetros de la línea y se sellan en el crédito:
    - la convención de días (30/360, real/365, real/360);
    - la equivalencia entre la tasa efectiva anual y la del período o del día (compuesta por defecto);
    - el redondeo: unidad, modo y momento (por concepto, por cuota o por total).
- **FR-011 · Reproducibilidad (R8).**
  - La liquidación de un crédito a una fecha D, con la información registrada hasta un instante R, da
    siempre el mismo resultado al centavo, desglosado por concepto, para cualquier D pasada o futura.
  - Por defecto R es el momento de la consulta.
  - Consultar una liquidación no cambia nada.
- **FR-012 · Tres fechas.** Toda transacción lleva fecha valor, fecha contable y fecha de registro.
  - **La fecha valor** puede ser anterior a hoy, aun dentro de un mes ya cerrado, hasta los días
    configurados. Si cae antes de un cierre, exige la atribución configurada.
  - **La fecha contable** es el primer mes sin cierre de Cartera que Contabilidad tenga abierto, según
    la consulta del estado del período.
  - **Los cierres mensuales** no se reescriben: los efectos posteriores se reconocen en el período
    abierto, con transacciones nuevas.
- **FR-013 · Pocos pasos y mensajes claros (R9).**
  - Un **paso** es una pantalla o diálogo con una acción de confirmación.
  - Estas operaciones se hacen en tres pasos o menos, y la lista se verifica en las pruebas: simular,
    radicar con los datos ya capturados, aprobar, desembolsar, simular un pago, consultar estado y
    liquidación, y emitir un documento.
  - Aplicar un pago es una pantalla de Recaudo, que hereda esta regla. Aquí se verifica que el contrato
    lo resuelve con una simulación y una aplicación.
  - El simulador está visible en toda pantalla de originación.
  - Toda pantalla que carga o guarda muestra que está cargando.
  - Los mensajes van en español claro, dicen qué hacer y nunca muestran códigos técnicos.
- **FR-014 · Normativa como dato, con límites legales (R10).**
  - Ningún valor legal o regulatorio está fijo en el producto:
    - usura e índices;
    - rangos de calificación, porcentajes y modelos de deterioro;
    - días de suspensión;
    - horarios y frecuencias de cobranza;
    - plazos de aviso y de reclamos;
    - capacidad de descuento;
    - mayorías para vinculados;
    - límites de concentración.
  - Todos son parámetros con vigencia y fuente, sembrados como **«pendiente de validar»** hasta que el
    contador o el oficial de cumplimiento los confirme.
  - Cada parámetro legal lleva su **piso o techo normativo**. Los pisos y techos son valores nacionales
    (FR-015), de sólo lectura para la cooperativa, que sólo puede configurar valores más estrictos.
  - Una verificación automática impide valores legales escritos en el código.
- **FR-015 · Valores nacionales replicados (I-3).**
  - Se registran **una vez**, fuera de las bases de cooperativa, con su vigencia, su fuente y su fecha
    de registro:
    - la usura certificada por modalidad;
    - los índices (IBR, IPC y otros) y el tope de tasa de vivienda;
    - el salario mínimo y los festivos;
    - los pisos y techos de los parámetros legales (FR-014);
    - la regla de obligación del modelo de referencia: los perfiles obligados y los segmentos que cubre;
    - los meses de historial de mora que pide ese modelo (FR-139).
  - Se replican a la base de cada cooperativa, también en el alta de una nueva. Cada cálculo lee su
    propia copia.
  - Las vigencias son arbitrarias: no se asume periodicidad mensual ni trimestral.
  - Si falta el valor de una fecha, lo que lo necesita se bloquea y se alerta a quien lo registra.
- **FR-016 · Alertas visibles (Principio IX).** Quedan como alerta para el rol responsable, con enlace
  al caso, entre otros:
  - un mensaje rechazado o un proceso diario que no corrió;
  - una usura o índice faltante;
  - un descuadre con Contabilidad;
  - un no aplicado o un giro atrasado de pagaduría;
  - una garantía por vencer;
  - una coincidencia en listas restrictivas;
  - un plazo regulatorio por vencer.

  Nada falla en silencio.
- **FR-017 · NIIF.**
  - El grupo NIIF de la cooperativa es un dato con vigencia.
  - En estados financieros individuales, la cartera y su deterioro siguen las reglas de la
    Supersolidaria (salvedad del Decreto 2496 de 2015), lo que debe confirmar la contadora.
  - Si las comisiones y costos de originación se difieren por tasa efectiva es un parámetro validado
    por la contadora. Por defecto no se difieren.
- **FR-018 · Tipo de entidad.** Son datos de la cooperativa con vigencia y soporte:
  - el tipo de organización solidaria;
  - si está autorizada para ejercer actividad financiera: sección de ahorro y crédito en multiactivas e
    integrales, o servicio de ahorro y crédito en mutuales;
  - su nivel de supervisión;
  - y, si aplica, su categoría (fondos de empleados).

  Deciden lo que la norma le permite y le exige:
  - si puede prestar a no asociados;
  - el modelo de deterioro por segmento (FR-105);
  - las mayorías para personas vinculadas (FR-040);
  - el reporte de castigos (FR-095);
  - la periodicidad de los reportes regulatorios (FR-136).

#### A · Líneas de crédito

- **FR-019 · Definición de la línea.** Cada línea tiene:
  - código con la nomenclatura de la cooperativa, nombre y destino;
  - **modalidad**, tomada del catálogo de modalidades con vigencia (consumo, comercial, vivienda,
    microcrédito y productivas), con su sub-segmento: con o sin libranza, persona natural o jurídica,
    y si es para empleados de la cooperativa;
  - estado y vigencia;
  - si admite solicitantes no asociados, sólo si el tipo de entidad lo permite (FR-018);
  - si admite **origen comercial**, es decir, ventas de Comercial.
- **FR-020 · Tasas.**
  - **Corriente**:
    - **fija**, o **variable**: un índice registrado (IBR, IPC u otro) más un margen, con periodicidad
      de reajuste, piso y techo;
    - se expresa en efectiva anual y se informa su equivalente nominal del período;
    - admite tasas diferenciadas por rangos de monto, plazo o segmento del solicitante.
  - **De mora**: fija, o relativa a la corriente.
  - Cada tasa declara qué **certificación de usura** le aplica: la modalidad certificada que corresponde
    a la línea, o el tope de vivienda.
- **FR-021 · Control de usura.**
  - **Qué se compara**: la tasa efectiva anual que resulta de la tasa pactada, la convención de días y
    **todos los cargos**, contra la usura vigente de su modalidad certificada.
  - **Qué cargos computan**: todos, salvo los que tengan una contraprestación distinta al crédito
    documentada. Un seguro computa si la cooperativa retiene parte de la prima o se autoasegura.
  - **Cuándo**: al crear o modificar la línea, al simular, al aprobar, al desembolsar y en cada causación
    diaria.
  - **Qué pasa si la supera**:
    - en una línea o en la originación, **bloquea** y alerta;
    - en un crédito vivo, **topa** la tasa del día en la usura y alerta.
  - **Mora**: la tasa de mora nunca supera la usura del día (cada porción de capital causa sólo
    corriente o sólo mora, FR-063).
  - **Usura nueva**: toda línea que la supere queda cerrada a nuevos créditos, con alerta, hasta
    ajustarla.
- **FR-022 · Montos, plazos y periodicidades.**
  - Monto y plazo mínimos y máximos, por línea y opcionalmente por segmento.
  - Periodicidades: semanal, decadal, quincenal, mensual, bimestral, trimestral, semestral, anual y pago
    único.
  - Regla del día de pago o de corte.
  - Las periodicidades y plazos que llegan de Comercial se traducen a las de Cartera con una tabla de
    equivalencias. Una periodicidad sin equivalencia se informa como error del mensaje.
- **FR-023 · Sistemas de amortización.**
  - Cuota fija.
  - Abono constante a capital.
  - Cuotas extraordinarias (primas): meses y valor o porcentaje, combinables con los dos anteriores.
  - Pago único al vencimiento.
  - Período de gracia, a capital o total, sin capitalizar intereses.

  En todos, el interés de cada cuota es el causado entre vencimientos, y la última cuota absorbe las
  diferencias (ver Edge Cases).
- **FR-024 · Vivienda.** Una línea de vivienda (Ley 546 de 1999) debe cumplir las reglas legales, que
  son parámetros con fuente:
  - tasa fija salvo pacto de rebaja, cobrada vencida y sin capitalizar;
  - tope de la Junta del Banco de la República;
  - plazo, porcentaje financiable (general y de vivienda de interés social) y relación cuota-ingreso;
  - hipoteca de primer grado.

  Las operaciones en UVR también son de vivienda. Su entrega es la K6b (D18).
- **FR-025 · Requisitos y documentos.**
  - Lista de documentos por línea, obligatorios u opcionales, con condiciones por monto, tipo de
    solicitante o forma de pago.
  - Requisitos del solicitante: antigüedad como asociado, edad y vínculo laboral. El puntaje sigue
    FR-037.
- **FR-026 · Codeudores y garantías exigidos.** Por rangos de monto y plazo:
  - número de codeudores;
  - tipos de garantía admitidos;
  - cobertura mínima;
  - si los aportes cuentan como garantía.
- **FR-027 · Formas de pago.** La línea indica cuáles admite (caja, libranza o nómina, débito
  automático) y la forma por defecto.
- **FR-028 · Seguros.** Seguros asociados (vida deudores y otros), cada uno con:
  - aseguradora, que es una persona del maestro;
  - base (saldo o monto inicial), tarifa y periodicidad;
  - si es obligatorio u opcional;
  - si la cooperativa retiene parte de la prima, para el control de usura.

  Se cobran con la cuota y se causan según su periodicidad.
- **FR-029 · Cupo y aportes.**
  - Relación cupo/aportes (por ejemplo, N veces los aportes).
  - Endeudamiento máximo con la entidad.
  - Saldo mínimo de aportes exigido.

  El saldo de aportes se consulta a Aportes (D19).
- **FR-030 · Prelación de imputación.**
  - Orden de cuotas y de conceptos por línea. Por defecto, **cuota más antigua completa** primero; dentro
    de cada cuota: gastos de cobranza, seguros, interés de mora, interés corriente y capital.
  - Los gastos de cobranza sólo van primero si se informaron al deudor (FR-121).
  - El interés en suspenso va, por defecto, junto con el interés corriente de su cuota.
  - Se sella en el crédito al desembolsar.
- **FR-031 · Recálculo por notas de Comercial.** La línea fija cómo se recalcula el plan tras una nota de
  Comercial: reducir cuota o reducir plazo. Esta regla **no aplica** a los abonos del deudor a capital,
  donde decide siempre el deudor (FR-075).
- **FR-032 · Versiones de línea.**
  - Modificar una línea crea una versión con fecha desde, y los créditos vivos conservan lo pactado.
  - Una línea con créditos vivos no se borra: se cierra a nuevos créditos.
- **FR-033 · Plantillas.** Líneas, tasas por rango, documentos y seguros se pueden cargar por plantilla,
  con revisión previa, todo o nada y errores por fila y columna.

#### B · Originación

- **FR-034 · Simulador.**
  - Entradas: línea, monto, plazo, periodicidad, fecha de desembolso y de primera cuota, sistema de
    amortización, extraordinarias, gracia y seguros.
  - Resultado:
    - el plan cuota a cuota: capital, interés, seguros, otros y saldo;
    - la tasa efectiva y la nominal;
    - el valor total a pagar;
    - la validación de usura.
  - No registra nada. Se puede imprimir o enviar al asociado.
- **FR-035 · Radicación.** Una solicitud registra:
  - el solicitante, que es una persona del maestro y debe ser asociado activo, salvo que la línea y el
    tipo de entidad admitan no asociados;
  - la línea y las condiciones simuladas;
  - el destino, la forma de pago y la pagaduría si es libranza;
  - los codeudores y garantías;
  - la **preferencia del deudor** para imputar abonos a capital (reducir plazo o cuota), que puede
    cambiar después en cualquier momento;
  - los documentos de la lista, adjuntos en el almacén de adjuntos de la plataforma.

  Sin los documentos obligatorios, la solicitud no pasa a análisis. El alta de una persona nueva usa el
  diálogo único de personas.
- **FR-036 · Validaciones automáticas.** Al radicar y antes de aprobar, para el solicitante y sus
  codeudores:
  - **listas restrictivas**: una coincidencia bloquea y alerta al oficial de cumplimiento, que es el
    único que la libera, con motivo;
  - **centrales de riesgo**: sólo con autorización (FR-006). El resultado se guarda como evidencia con su
    fecha;
  - **capacidad de pago**: ingresos menos egresos y cuotas, propias y externas;
  - **capacidad de descuento** por libranza (FR-110);
  - **endeudamiento consolidado** con la entidad, como deudor y como codeudor;
  - **cupo** (FR-058);
  - **concentración** (FR-041);
  - **usura** (FR-021);
  - **requisitos** de la línea.

  Cada validación queda con su resultado y su evidencia. Las que no bloquean llegan al aprobador como
  alertas.
- **FR-037 · Puntaje (scoring) parametrizable.**
  - Variables, pesos, rangos y puntaje mínimo por línea, con vigencia.
  - El resultado y su explicación (cuánto aportó cada variable) se guardan en la solicitud.
  - Un puntaje bajo el mínimo no rechaza solo: sube la solicitud al nivel de excepción configurado.
- **FR-038 · Atribuciones por niveles.**
  - La cooperativa define niveles (analista, gerente, comité, consejo u otros) y las condiciones que
    llevan una solicitud a cada uno: línea, monto consolidado del solicitante, plazo, garantía,
    puntaje, alertas y excepciones a la política.
  - La solicitud va sola al nivel que le corresponde.
  - Un nivel puede:
    - aprobar;
    - aprobar con condiciones: monto o plazo menores, o garantía adicional;
    - negar con motivo;
    - devolver para corrección.
  - **Sin excepción posible**: la falta de capacidad de pago, de autorización del titular, una
    coincidencia en listas restrictivas y la usura nunca se pueden aprobar por excepción.
- **FR-039 · Decisiones colegiadas con acta.**
  - Comité y consejo deciden en una **sesión con acta**: número, fecha, asistentes, quórum, votos por
    solicitud, decisión y condiciones.
  - El voto de quien radicó la solicitud no se admite.
  - El acta queda inmutable una vez cerrada.
- **FR-040 · Personas vinculadas (Ley 454 de 1998, art. 61).**
  - La cooperativa mantiene la lista de vinculados con su fundamento:
    - asociados con el porcentaje de aportes que fije la norma;
    - miembros del consejo y de la junta de vigilancia;
    - representantes legales;
    - sus cónyuges y parientes en los grados legales.
  - Sus operaciones de crédito, novedades y condonaciones van al consejo.
  - En las entidades con actividad financiera exigen el voto favorable de al menos **4/5 de la
    composición** del consejo, como piso legal. El interesado no vota, pero la base sigue siendo la
    composición.
  - El acta deja constancia de la decisión y de los límites.
  - Sus condiciones no pueden ser distintas a las generales, salvo las excepciones que la ley y el
    reglamento admiten.
- **FR-041 · Límites de concentración.**
  - Ningún desembolso, cupo ni novedad puede llevar el endeudamiento de una persona, o de un grupo
    conectado, por encima del límite individual que fije la norma para el tipo de entidad, con o sin
    garantía admisible.
  - El patrimonio técnico se toma de una consulta definida a Contabilidad.
  - Exceder el límite **bloquea**, no sólo se reporta.
- **FR-042 · Vigencia de la aprobación.**
  - La aprobación vale los días configurados. Vencida, exige aprobar de nuevo.
  - Las condiciones se vuelven a validar contra la usura del día del desembolso.
- **FR-043 · Garantías.** Cada garantía tiene tipo, valor, fecha, documentos, vigencia y estado
  (constituida, vigente, liberada, extinguida, ejecutada). Los tipos son:
  - **pagaré**, físico o desmaterializado;
  - **codeudor**, que es una persona del maestro con sus propias validaciones y autorización;
  - **prenda** e **hipoteca**, con avalúo, póliza y sus fechas;
  - **fondo de garantías**, con cobertura y comisión;
  - **aportes**.

  Las que vencen alertan con la anticipación configurada. Pagaré y garantías se liberan sólo cuando la
  cancelación queda en firme (FR-082).
- **FR-044 · Pagaré desmaterializado.**
  - Por un adaptador del depósito centralizado de valores: creación, firma electrónica del deudor y
    codeudores, custodia, endoso y anulación.
  - Hasta que la cooperativa tenga contrato, el adaptador trabaja en **canal simulado**, claramente
    identificado.
  - El documento cruce del crédito ante Contabilidad es el **número del crédito**. El pagaré se
    referencia en el crédito.
- **FR-045 · Desembolso.**
  - **Requisitos**: la aprobación vigente y las garantías perfeccionadas que exija la línea.
  - **Formas**:
    - **transferencia** a la cuenta del deudor o de un tercero;
    - **cheque**;
    - **giro a tercero**, por ejemplo a un proveedor o a un vendedor;
    - **en especie**, desde Comercial y sin salida de dinero.
  - **Tramos**: puede ser único o por tramos cuando la línea lo admite. Cada tramo es una transacción.
  - **Efectos**: el desembolso sella las condiciones pactadas y el plan, emite el comprobante de
    desembolso y la **orden de giro** para Tesorería, y emite «crédito desembolsado».
  - **Orden de giro** (la define esta especificación y Tesorería la adopta, D27):
    - es un mensaje versionado de la bandeja, con clave única;
    - lleva crédito, tramo, forma (transferencia, cheque o giro a tercero), beneficiario (persona del
      maestro), cuenta destino, valor neto tras la recogida y fecha;
    - pasa de emitida a girada, rechazada, cheque no cobrado o anulada;
    - Tesorería responde «resultado del giro», con su propia clave y referido a la clave de la orden. Una
      orden puede recibir más de un resultado, por ejemplo girada y luego cheque no cobrado;
    - sin resultado en los días configurados, alerta (FR-016);
    - un usuario con permiso anula, con motivo, una orden emitida con el mensaje «anulación de orden de
      giro». El mensaje lleva su propia clave única y la clave de la orden como enlace al original.
      Tesorería responde «resultado del giro» referido a la orden: «anulada» o, si ya giró, «girada». En
      este último caso el desembolso no se reversa y se gestiona como un pago (FR-048);
    - un nuevo giro es una orden nueva enlazada a la anterior;
    - si la transferencia sale en el archivo de FR-047, la orden lleva la referencia del archivo, y
      Tesorería no la paga por otra vía.
- **FR-046 · Recogida de créditos propios.**
  - A pedido del solicitante y si la aprobación lo autoriza, el desembolso cancela total o parcialmente
    otros créditos del mismo deudor.
  - Cada crédito recogido se paga por el contrato de obligaciones con su comprobante. Se informa el neto
    a girar, y los créditos quedan enlazados.
  - Si un crédito recogido tiene mora o el deudor perdió capacidad de pago, la operación es una
    **reestructuración** (FR-090) y se tramita como tal.
- **FR-047 · Archivo de transferencia y anulación de solicitud.**
  - Los desembolsos por transferencia pueden salir en un archivo plano con el formato bancario vigente
    de la plataforma, en el ámbito «desembolso de crédito» (FR-149). La cuenta de origen se elige entre
    las cuentas bancarias que informa Contabilidad.
  - Una solicitud no desembolsada se puede anular o desistir con motivo, y no genera cartera.
- **FR-048 · Reverso de un desembolso.**
  - Tesorería informa el resultado de cada giro: girada, rechazada, cheque no cobrado o anulada.
  - Si es rechazada, cheque no cobrado o anulada, el desembolso se reversa **desde su origen**: los
    intereses causados se anulan y se emite «desembolso reversado».
  - El crédito queda pendiente de un nuevo giro, o anulado si así se decide con motivo.
  - Un desembolso cuyo dinero ya salió no se reversa: se gestiona como un pago.

#### C · Créditos originados en Comercial

- **FR-049 · Cupo rotativo para Comercial.**
  - Vender a crédito exige un **cupo rotativo** otorgado por originación. La originación incluye la
    evaluación (capacidad de pago, centrales y listas), las autorizaciones del titular y la aprobación
    por atribución.
  - Los clientes no asociados reciben cupo por atribución, sólo en las líneas que los admiten y si el
    tipo de entidad lo permite.
  - Al otorgar el cupo se registra la **preferencia del deudor** para imputar abonos a capital (FR-035),
    que heredan los créditos que nacen de sus compras.
  - Al consumir el cupo con una venta se vuelven a verificar las listas restrictivas y la vigencia de la
    autorización.
- **FR-050 · Crédito desde una venta.** Al recibir «venta a crédito registrada», Cartera **crea siempre**
  la obligación de la venta confirmada, con:
  - **Línea**: la que indica el mensaje; si no la trae, la sugerida por el medio de pago; si no, la línea
    por defecto para el tipo de tercero.
  - **Fecha de origen**: la de la venta.
  - **Valor**: el del pago a crédito.
  - **Número**: el reservado, si lo hay (FR-057).
  - **Plan**: las condiciones del mensaje, traducidas (FR-022), o las de la línea.
  - **Desembolso**: en especie, sin salida de dinero, con la factura como referencia.
- **FR-051 · Validación de la venta.**
  - Las ventas que Comercial ya validó en línea se crean sin reevaluarlas.
  - Las que llegan **pendientes de validar** se evalúan con lo vigente **a la fecha de la venta**:
    estado, cupo sin contar esa misma venta, concentración (FR-041), usura, que la línea admita origen
    comercial, listas restrictivas y autorización.
  - Se evalúan en el orden de llegada, contando las anteriores ya creadas.
  - Una aprobación de Comercial (su límite de venta a crédito) **no sustituye** una atribución de
    Cartera.
- **FR-052 · Validación fallida.** Si la evaluación es negativa:
  - la obligación se crea igual, marcada **«validación fallida»** con el motivo;
  - se bloquea crédito nuevo al deudor;
  - la entrega queda en «validación fallida», que no bloquea los ajustes de esa venta;
  - se alerta en Comercial y en Cartera. Una coincidencia en listas restrictivas alerta además al
    oficial de cumplimiento;
  - la marca no altera la causación, el cobro ni la calificación del crédito.

  Un usuario con atribución puede confirmar la obligación por excepción, con motivo, salvo en los casos
  sin excepción posible de FR-038. Si no, sigue en cobro normal.
- **FR-053 · Respuestas a Comercial.**
  - **«Obligación creada»**: número, línea, valor, resumen del plan y estado (validada, validación
    fallida, migrada o cancelada antes del corte).
  - **«Obligación rechazada»**: para mensajes que no se pueden convertir en obligación (persona
    inexistente, línea inválida sin alterna, versión no aceptada). Lleva código y motivo en español y
    queda para reproceso. La excepción es la venta aceptada sin migrar en la activación (FR-143), que no
    queda para reproceso.
  - Las dos llevan siempre la referencia a la factura y al mensaje original.
  - Además, Cartera responde la consulta de **estado de validación** que Comercial ya usa: pendiente,
    validado, fallido o no incorporado.
- **FR-054 · Ajustes desde Comercial.** Cada ajuste se registra enlazado a la factura y al ajuste. Manda
  el signo del valor. Por clase:
  - **Nota crédito y devolución parcial**: bajan el capital, reversan el interés causado sobre la parte
    devuelta y recalculan el plan según la línea (FR-031). Si superan lo adeudado, el excedente es un
    **saldo a favor del deudor**, visible en el listado del contrato (FR-084) para que Recaudo lo
    devuelva o lo aplique (FR-083).
  - **Anulación y anulación por rechazo de la DIAN**: dejan el crédito **sin efecto desde su origen**.
    - Se reversan intereses, mora y cargos.
    - Se corrige lo reportado a centrales.
    - Lo pagado queda como saldo a favor.
  - **Reemplazo**: crea un crédito nuevo enlazado al anulado.
  - **Nota débito**: sigue la regla de la venta:
    - aumenta siempre el saldo y recalcula el plan;
    - se evalúa como una venta a su fecha (cupo, concentración, usura, autorización);
    - si la evaluación es negativa, marca el crédito «validación fallida» (FR-052).
- **FR-055 · Orden, versiones y sobre.**
  - Los mensajes de una misma cadena de documentos se procesan en orden: un ajuste espera a su venta.
  - Cartera acepta a la vez todas las versiones vigentes de cada mensaje, y usa el mismo sobre de la
    bandeja de la plataforma.
  - La identidad de una venta es su factura: ninguna factura origina dos obligaciones, venga por mensaje
    o por migración.
- **FR-056 · Una sola cuenta por cobrar.** Cartera respeta lo que Comercial selló en cada venta sobre quién
  registra la cuenta por cobrar:
  - **Cartera**: la venta llevó la cuenta por cobrar a la cuenta puente, y el hecho de desembolso en
    especie la traslada a cartera de créditos;
  - **Contabilidad**: el hecho de desembolso en especie **reclasifica** esa cuenta por cobrar a cartera
    de créditos.

  En ningún caso queda registrada dos veces. Tras la activación, el valor por defecto que Comercial
  sella es «Cartera»; lo cambia el administrador de Comercial (D16).
- **FR-057 · Reservar número de crédito.**
  - Es una **operación** con clave de idempotencia ligada al documento de venta, no una consulta.
  - Repetirla para el mismo documento devuelve el mismo número.
  - La reserva lleva el valor de la venta y **descuenta cupo**.
  - Vale hasta que la venta se confirma o Comercial la **libera** al descartarla. Liberar es otra
    operación idempotente.
  - Si no se confirma ni se libera en los días configurados, se libera sola, con alerta.
  - Comercial imprime el número en la factura como referencia y lo envía en la versión 2 del mensaje de
    venta.
  - Si no hubo reserva, porque Cartera no respondió, Comercial guarda el número cuando llega «obligación
    creada» y lo muestra en la venta y en sus consultas.
  - Un número reservado y no usado queda anulado y visible. Los números de crédito no tienen que ser
    continuos.

#### D · Cupos y consultas para otros módulos

- **FR-058 · Cupo.**
  - Cada asociado tiene un cupo **consolidado** y, si se configura, **por línea**. Un cliente no asociado
    tiene cupo sólo si se le otorgó (FR-049).
  - El cupo se **calcula** (relación con aportes, capacidad, puntaje) o se **asigna** por atribución, con
    vigencia y motivo.
  - Disponible = cupo − saldos vivos − reservas vigentes − solicitudes aprobadas sin desembolsar.
- **FR-059 · Consulta de cupo y estado.**
  - **Entradas**: persona, tipo de tercero (asociado o cliente), valor, fecha y canal.
  - **Respuesta**:
    - **estado**: activo, en mora (con días), bloqueado (con motivo) o sin vínculo;
    - cupo total y disponible;
    - las **líneas de origen comercial aplicables**, con plazo máximo, cuotas máximas y periodicidades;
    - una referencia de evidencia: la huella de las entradas y la respuesta, que queda en la auditoría.
  - Para cada estado, un parámetro dice si permite vender. Por ejemplo, en mora sólo hasta N días.
  - Es una consulta: **no cambia el estado del negocio**; sólo deja su registro de auditoría. Su tiempo de
    respuesta está en SC-008.
- **FR-060 · Bloqueos.**
  - **Manuales**, con motivo y atribución.
  - **Automáticos**: mora por encima de los días configurados, validación fallida de una venta, crédito
    castigado, alerta de listas restrictivas, retiro del asociado, fallecimiento, insolvencia.
  - Un bloqueo impide originar y vender a crédito, y lo informa la consulta de estado.
- **FR-061 · Consulta de obligaciones de una persona.** Para otros módulos, por persona y por obligación,
  como deudor o codeudor:
  - número, línea, clase (crédito o libranza) y pagaduría;
  - estado, incluido «migrada, sin activar» para los deudores de la carga vigente antes de la activación
    (FR-142);
  - saldo total, exigible y cuotas causadas a una fecha;
  - días de mora.

  La usan:
  - **Personas**, para no permitir el borrado de una persona con obligaciones vivas y para su ficha;
  - **Nómina**, para la cuota del período (FR-112) y la definitiva (FR-088);
  - **Aportes**, en el retiro.

#### E · Causación y liquidación

- **FR-062 · Interés corriente.**
  - Cada día calendario, cada crédito vivo causa interés corriente sobre su **capital no vencido**.
  - Usa la tasa vigente ese día, topada en la usura, con la convención y la equivalencia pactadas.
  - Corre sola por cooperativa; nadie tiene que ejecutarla.
- **FR-063 · Interés de mora.**
  - Se causa diariamente sobre el **capital vencido**, con la tasa de mora vigente y topada en la usura
    del día.
  - La porción vencida no causa también interés corriente.
  - No se causa mora sobre intereses, salvo lo que la ley permita y la cooperativa configure. En
    vivienda, nunca.
- **FR-064 · Seguros y otros cargos.**
  - Los seguros se causan según su periodicidad y su base.
  - Los gastos de cobranza y otros cargos se causan sólo cuando una regla o una persona los origina, con
    su soporte y, si la regla lo exige, informados antes al deudor.
- **FR-065 · Suspensión y reanudación.**
  - **Cuándo se suspende**:
    - al superar la altura de mora que fija la norma para la modalidad;
    - en un crédito reestructurado más de una vez, que reconoce sus intereses sólo cuando los cobra;
    - al iniciar un proceso de insolvencia;
    - al castigar.
  - **Qué pasa al suspender**: el interés pasa a **suspenso**, sigue debiéndose y se incluye en la
    liquidación y en la prelación. Se emite «causación suspendida».
  - **Cobrar lo suspendido** es ingreso directo, con su propio hecho.
  - **Reanudación**: según una regla medible configurada. Por defecto, cuando la altura de mora baja del
    umbral de la modalidad y no hay otro disparador vigente. Se emite «causación reanudada».
- **FR-066 · Proceso diario confiable.**
  - La causación de un crédito y una fecha ocurre **una sola vez**, aunque el proceso se repita.
  - Si un día no corrió, el siguiente se pone al día, una fecha a la vez y en orden.
  - El proceso recorre las cooperativas una por una, sin afectar a las demás si una falla. Deja alerta de
    lo que no pudo causar.
- **FR-067 · Reajuste de tasa variable.** En cada fecha de reajuste:
  - toma el **último índice publicado** a esa fecha más el margen, con su piso, su techo y el tope de
    usura;
  - el reajuste es definitivo;
  - recalcula la cuota o el plazo según lo pactado;
  - registra la nueva tasa como transacción y versión del plan;
  - lo informa en el siguiente extracto.
- **FR-068 · Liquidación a la fecha.**
  - Para una fecha D pasada, de hoy o futura, da el valor para cancelar, desglosado por concepto y por
    cuota: capital, corriente, mora, en suspenso, seguros, gastos de cobranza y otros.
  - Explica cómo se calculó: días, tasas y bases.
  - A fecha futura proyecta sin pagos intermedios y con las tasas conocidas, y dice hasta qué fecha es
    válida.
  - Cumple FR-011. Se puede imprimir o enviar.
- **FR-069 · Estado de cuenta del crédito.** A cualquier fecha muestra:
  - cuotas pagadas, vencidas, cubiertas por anticipo y por vencer;
  - **días de mora**, desde la cuota impaga más antigua cuyo saldo vencido supere la tolerancia
    configurada;
  - saldos por concepto;
  - calificación, etapa de cobranza, garantías, bloqueos y estado de libranza;
  - el historial completo de transacciones y versiones del plan.
- **FR-070 · Día no hábil.**
  - Si una cuota vence en día no hábil, se puede pagar el día hábil siguiente sin mora, como manda el
    Código de Comercio. Esta regla no se puede desactivar.
  - El interés corriente de esos días se causa y se cobra con la cuota siguiente.

#### F · Aplicación de pagos

- **FR-071 · Modalidades de pago.** Cartera ofrece al recaudador:
  1. **Pago total o cancelación**: el valor de la liquidación a la fecha valor.
  2. **Abono a cuotas vencidas**: cubre lo vencido en orden de prelación.
  3. **Cuota del ciclo**: lo exigible al corte vigente, es decir, lo vencido más la cuota del período.
  4. **Pago anticipado de cuotas futuras** (FR-074).
  5. **Abono extraordinario a capital** (FR-075).
  6. **Pago a una cuota específica**: sólo si las anteriores están cubiertas, salvo que la línea lo
     permita.
  7. **Valor libre**: distribución automática según la prelación.
- **FR-072 · Prelación aplicada.**
  - La distribución por cuota y concepto sigue la prelación sellada en el crédito (FR-030), y es idéntica
    en la simulación y en la aplicación.
  - Todo abono o anticipo cubre primero lo vencido.
- **FR-073 · Simulación previa y dos formas de aplicar.**
  - **Interactiva** (oficina, caja):
    - aplicar exige una simulación previa que muestre la distribución exacta, los saldos resultantes, el
      nuevo plan si cambia y el excedente;
    - la aplicación lleva el identificador de esa simulación;
    - si entre simular y aplicar cambió el crédito (causación, otro pago, una novedad), responde «la
      distribución cambió» con la nueva, antes de aplicar.
  - **Sin interacción** (Nómina, pagadurías, débito automático, pagos de terceros): aplicar lleva
    modalidad, valor y fecha valor. Cartera distribuye con lo vigente al aplicar, nunca responde «la
    distribución cambió» y devuelve el comprobante con la distribución hecha.
- **FR-074 · Pago anticipado de cuotas futuras.**
  - El valor queda como **anticipo**: un pasivo de Cartera con el deudor que cubre cada cuota completa en
    su vencimiento, sin cobrar intereses aún no causados.
  - Emite «anticipo recibido» y, en cada vencimiento, «anticipo aplicado».
  - La simulación informa que el anticipo no reduce los intereses y ofrece el abono a capital (FR-075)
    como alternativa. **El deudor elige en cada pago**; la línea sólo fija qué opción se muestra primero.
  - Las cuotas cubiertas se ven como tales.
- **FR-075 · Abono extraordinario a capital (Leyes 1555 de 2012 y 2032 de 2020).**
  - Se aplica a capital **sin penalidad, comisión ni cargo**, después de cubrir lo vencido.
  - **El deudor decide siempre** si reduce el plazo o la cuota, y la entidad **nunca** decide por él:
    - Cartera pregunta en cada pago y muestra los dos planes antes de confirmar;
    - si el abono llega por un canal sin interacción, se aplica la **preferencia que el deudor registró**
      (FR-035, FR-049) como instrucción permanente y revocable. Cada aplicación se le informa con la
      opción de cambiarla (D24);
    - sin preferencia registrada, el valor queda como saldo a favor hasta que el deudor elija.
  - El límite de monto de crédito que fija la ley para este beneficio es un parámetro legal con fuente.
- **FR-076 · Excedentes, anticipos y saldos a favor.**
  - Lo que supera lo exigible de la modalidad elegida, o el total en una cancelación, es un
    **excedente**: vuelve al recaudador y nunca entra a Cartera.
  - Cartera sólo conserva dinero del deudor en estos casos, todos pasivos con sus hechos:
    - los **anticipos** (FR-074);
    - los **saldos a favor** por notas o anulaciones de Comercial, por correcciones de usura, o por un
      abono sin preferencia registrada (FR-075).
  - Se devuelven o se aplican a otra obligación por la quinta operación del contrato (FR-083).
- **FR-077 · Comprobante de aplicación.**
  - Cada aplicación genera un comprobante propio, numerado, con la distribución por cuota y concepto.
  - Queda enlazado al recibo del recaudador (número, fecha, oficina y referencia del medio), sólo como
    referencia.
  - Emite «pago aplicado», desglosado por concepto.
- **FR-078 · Pagos con fecha valor anterior.** Siguen FR-012:
  - mora y causación se recalculan desde la fecha valor con transacciones de ajuste, que llevan fecha
    contable del período abierto;
  - nunca se edita una transacción anterior.
- **FR-079 · Concurrencia.**
  - Dos operaciones sobre el mismo crédito (pago, causación, novedad) nunca se pisan: una espera a la
    otra.
  - La que llega con una distribución desactualizada recibe «la distribución cambió», con la nueva.
- **FR-080 · Reverso de un pago.**
  - Lo pide el recaudador por el contrato, o un usuario con atribución (por ejemplo, por un cheque
    devuelto), siempre con motivo.
  - Un pago de Nómina sólo lo reversa Nómina.
  - Un reverso que inicia Cartera se informa al recaudador con el mensaje «pago reversado por Cartera».
  - El crédito queda **exactamente** como si el pago no hubiera existido: saldos por concepto, cuotas,
    días de mora, causación y calificación vigente.
  - Lo ya cerrado no se reescribe: la calificación y el deterioro de meses cerrados y los comprobantes
    contables de períodos cerrados quedan como están. La diferencia se reconoce en el período abierto.
  - Emite «pago reversado».
- **FR-081 · Reverso con pagos posteriores.**
  - Si después del pago reversado hay otros pagos, el reverso reconstruye su distribución con su misma
    fecha valor y prelación.
  - Muestra la nueva distribución antes de confirmar, y registra los ajustes como transacciones nuevas
    enlazadas.
  - Un excedente ya devuelto por el recaudador lo resuelve el recaudador.
- **FR-082 · Plazo de firmeza.**
  - Tras una cancelación, pagaré y garantías se liberan sólo cuando el pago que canceló queda en firme:
    pasados los días configurados para su **clase de medio**, sin reverso.
  - Las clases son una lista fija de Cartera:
    - las clases fijas de medios de pago de la 012, cada una con sus días;
    - las propias de Cartera: **descuento de la nómina propia**, **libranza de pagaduría externa** y
      **cruce interno** (recogida, aportes y saldo a favor).
  - Cada clase tiene sus días por defecto, y el cruce interno queda en firme al aplicarse.
  - El contrato lleva la clase, no el medio de pago de la cooperativa, y una clase desconocida se
    rechaza.
  - Hasta entonces, el paz y salvo indica que la cancelación está sujeta a firmeza.

#### L · Contrato de obligaciones (para Recaudo y demás recaudadores)

- **FR-083 · El contrato.**
  - Cartera implementa el contrato «Proveedor de Obligaciones», con cinco operaciones:
    - **listar** las obligaciones de un tercero;
    - **simular** un pago;
    - **aplicar** un pago;
    - **reversar** un pago;
    - **resolver un saldo a favor o un anticipo**: devolverlo al deudor por el recaudador, aplicarlo a
      otra obligación suya o, si nació de un abono sin preferencia (FR-075), aplicarlo como abono
      extraordinario a la misma obligación con la opción que el deudor elija.
  - Esta especificación lo define para que Recaudo lo adopte (D15). Si Recaudo necesita otro detalle, se
    ajusta con una versión nueva del contrato.
  - El **cajero** de Recaudo es su usuario más frecuente.
- **FR-084 · Listar.** Para un tercero, como deudor y opcionalmente como codeudor, entrega por cada
  obligación:
  - número, línea, estado y días de mora;
  - exigible hoy, cuota del ciclo, vencido y total para cancelar;
  - anticipo y saldo a favor;
  - modalidades admitidas;
  - si tiene bloqueos o acuerdos de pago vigentes.
- **FR-085 · Simular, aplicar y reversar.**
  - **Simular** recibe obligación, modalidad, valor, fecha valor y, si aplica, la elección del deudor
    entre plazo y cuota. Devuelve la distribución, el nuevo plan y un identificador de simulación.
  - **Aplicar** recibe además:
    - la clave de idempotencia y la referencia del recibo;
    - la **clase de medio** (FR-082);
    - el **pagador**, que es una persona del maestro: el deudor, un codeudor, la pagaduría, la aseguradora
      u otro tercero;
    - en el pago de una pagaduría, desde K2 y sólo si la obligación tiene una línea «descontado
      pendiente de giro» de esa pagaduría (FR-114): la referencia de la línea que cubre. Sin ella, se
      rechaza. Con ella, Cartera levanta el pendiente de giro y emite el hecho con la pagaduría como
      tercero;
    - los demás pagos de una pagaduría se aplican con la pagaduría como pagador y la clase «libranza de
      pagaduría externa», sin levantar pendientes. Son los de períodos cuyo descuento no salió en un
      archivo de Cartera, las cancelaciones al retiro y los abonos o descuentos atrasados. Los descuentos
      de la nómina propia no llevan referencia (FR-088, FR-112);
    - el identificador de la simulación, en la forma interactiva (FR-073).

    Devuelve el comprobante y el excedente. Con la aseguradora como pagadora, Cartera cierra la
    reclamación del siniestro (FR-097).
  - **Reversar** recibe la clave del pago y el motivo. Devuelve el comprobante de reverso.
- **FR-086 · Ejecución y respuestas (I-4).**
  - El contrato se ejecuta **sincrónicamente dentro de la unidad de trabajo del recaudador**, en la base
    de la misma cooperativa: si el recaudador no confirma, la aplicación tampoco queda.
  - Tiene versión.
  - Cada respuesta lleva un resultado (aplicado, ya aplicado, rechazado), un código y un mensaje en
    español que dice qué hacer.
- **FR-087 · Quiénes recaudan.** Todo dinero que reduce una obligación entra por este contrato, salvo
  los cobros provisionales incorporados al activar (FR-142):
  - **Recaudo**: caja, consignaciones, débito automático y respuestas de pagadurías;
  - **Nómina**: la definitiva (FR-088) y la nómina ordinaria (FR-112);
  - **Aportes**, al cruzar aportes contra deudas en un retiro;
  - **el pago de una aseguradora** por un siniestro, que lo recibe Recaudo con la aseguradora como
    pagadora;
  - **la recogida de créditos** en un desembolso (FR-046).

  Cada uno se identifica como recaudador con su propia clave de idempotencia.
- **FR-088 · Liquidación definitiva de Nómina.**
  - Nómina consulta las obligaciones del empleado (FR-061) y propone los descuentos según su política.
  - Al aprobar la liquidación, aplica cada descuento por el contrato, en la forma sin interacción
    (FR-073), con la clave «identificador de la versión aprobada de la liquidación + obligación».
  - La aprobación y los recaudos ocurren **todo o nada**: si Cartera rechaza uno, la aprobación no queda
    hecha y se ve el motivo.
  - Reversar la liquidación reversa sus recaudos por el contrato.
- **FR-089 · Sin Recaudo ni Tesorería no hay salida en vivo** (decidido por el dueño, Q1).
  - Esta feature no construye un recaudo ni una confirmación de giros provisionales, y no amplía la caja
    del punto de venta de la 012.
  - Cartera no se activa en producción en una cooperativa mientras Recaudo y Tesorería no estén
    disponibles allí (FR-142).
  - Hasta entonces, Comercial sigue vendiendo con su crédito provisional y acumulando mensajes, como en
    la 012.
  - En los ambientes de prueba, Cartera se ejercita con dos dobles. Están claramente identificados, no
    son una forma de operar sin Recaudo, y **no se pueden registrar en un despliegue de producción**: lo
    fija una prueba automática. Son:
    - un **recaudador de prueba**, que:
      - lista, simula, aplica, reversa y resuelve saldos a favor por el contrato;
      - recibe «pago reversado por Cartera» (FR-080);
      - desde K2, recibe «descuentos por pagaduría», confirma su envío (FR-113) y emite «resultado de
        descuentos» (FR-115) a partir de un archivo de respuesta que carga quien prueba;
    - una **tesorería de prueba**, que recibe la orden de giro y su anulación, y emite «resultado del
      giro»: girada, rechazada, cheque no cobrado o anulada (FR-045, FR-048).

#### G · Novedades, condonaciones, insolvencia y castigo

- **FR-090 · Clasificación por causa.**
  - **Reestructuración**: cualquier modificación de las condiciones motivada por el deterioro de la
    capacidad de pago del deudor, cualquiera sea su mecanismo. Incluye:
    - prórroga, novación, recogida con mora o acuerdo de pago que cambie condiciones;
    - los acuerdos de los procesos de insolvencia.

    Lleva la marca de reestructurado con su conteo:
    - por prórroga, cambio de fecha o de tasa, el crédito **conserva su número**;
    - por novación o refinanciación, nace un **crédito enlazado** que hereda la marca, el conteo, la
      calificación y el historial de mora.
  - **Modificación**: un cambio de condiciones a petición del deudor sin deterioro de su capacidad de
    pago. Se admite sólo si su historial de mora reciente está dentro del límite que fija la norma.
    Vuelve a ser reestructuración si cae en mora por encima de los días que la norma fija bajo las
    nuevas condiciones.
  - **Mecanismos**: refinanciación (un crédito nuevo que recoge otros), novación (cancela la obligación
    y crea otra enlazada), prórroga, cambio de fecha de pago y cambio de tasa dentro de la usura.
- **FR-091 · Trámite de una novedad.**
  - Ciclo: solicitud con motivo, simulación del nuevo plan y de la calificación resultante, aprobación
    por atribución (con acta cuando el nivel la exija) y aplicación.
  - Aplicarla crea una **versión nueva del plan**; la anterior queda en el historial.
  - Emite «crédito reestructurado», «crédito modificado» o «crédito novado», según corresponda.
  - Los costos de una reestructuración no se trasladan al deudor.
- **FR-092 · Consentimiento de garantes.**
  - Antes de aplicar una prórroga, reestructuración o novación, se exige la aceptación firmada de
    codeudores y garantes, y el nuevo pagaré u otrosí.
  - Si un garante no consiente, el aprobador decide con motivo:
    - **no aplicar** la novedad;
    - o **aplicarla con esa garantía extinguida**, sólo si la cobertura restante cumple la línea o se
      aprueba la excepción. Cobertura y deterioro se recalculan.
  - Una novación exige constituir sus propias garantías.
- **FR-093 · Efecto en la calificación.** Las reglas que fija la norma, sembradas como parámetros:
  - un reestructurado nunca queda mejor calificado que al pedir la reestructuración;
  - mejora una categoría por cada tramo de cuotas consecutivas pagadas que fije la norma;
  - si vuelve a caer en mora, regresa de inmediato a la calificación que tenía al reestructurarse;
  - reestructurado más de una vez, reconoce sus intereses sólo al cobrarlos (FR-065);
  - la marca se levanta tras el tiempo que fija la norma por modalidad.
- **FR-094 · Condonaciones.**
  - Se pueden condonar intereses de mora, corrientes o en suspenso, gastos de cobranza, seguros y,
    excepcionalmente, capital.
  - Cada nivel de atribución tiene sus condiciones: concepto, monto, días de mora y si el deudor es
    persona vinculada (FR-040).
  - Siempre llevan motivo, y acta cuando el nivel la exige.
  - Emiten «condonación aprobada» por concepto.
- **FR-095 · Castigo.**
  - El jefe de cartera arma un **lote de castigo**. Cada crédito debe cumplir:
    - **lo que exige la norma**:
      - el deudor ya no es asociado;
      - sus aportes ya se cruzaron;
      - hay informes motivados y concepto de irrecuperabilidad.
    - **lo que fija la política de la cooperativa**: deterioro total, altura de mora mínima y gestiones
      agotadas.
  - El **consejo de administración** aprueba el lote con acta.
  - Aplicarlo deja cada crédito castigado con referencia al acta y emite «crédito castigado».
  - Si el tipo de entidad está obligado a reportar castigos a la Superintendencia, se alerta el plazo
    con sus soportes.
  - Un crédito que no cumple se rechaza del lote, diciendo qué falta.
- **FR-096 · Cartera castigada.**
  - Sigue en cobranza y en los reportes a centrales mientras la ley lo permita.
  - Su interés se lleva en control.
  - Sus pagos son **recuperaciones**: entran por el contrato y emiten «recuperación de castigada».
  - Un reporte de seguimiento muestra lo castigado, lo recuperado y las gestiones.
- **FR-097 · Novedades del deudor.**
  - **Fallecimiento**: bloquea cobranza, mensajes y reporte negativo mientras la reclamación al seguro
    de vida deudores está en trámite, y activa esa reclamación.
  - **Retiro de la cooperativa**: bloquea el cupo y avisa al proceso de retiro.
  - **Embargo**: bloquea el cupo y se registra con su soporte.
- **FR-098 · Insolvencia (Leyes 1116 de 2006 y 1564 de 2012).**
  - Desde el inicio de la negociación se suspenden la causación y el cobro, y la calificación se
    mantiene, según la norma.
  - Un acuerdo es una reestructuración (FR-090).
  - Si la negociación fracasa, el crédito pasa a la calificación que fija la norma.

#### H · Riesgo de crédito (SARC)

- **FR-099 · Calificación.**
  - Cada crédito tiene una calificación de A a E, con las subcategorías que fije la norma (por ejemplo
    E1 y E2 según el tiempo en E y la modalidad). Sale de:
    - los rangos de altura de mora vigentes de su modalidad;
    - las reglas especiales de pago único y de créditos nuevos;
    - los **factores de riesgo** de la norma vigente (Anexo 1).
  - En los segmentos con pérdida esperada (FR-105), la calificación sale de la regla del Anexo 2 vigente,
    que es un parámetro con vigencia y fuente sembrado «pendiente de validar».
  - «La del modelo» es la que resulta del modelo vigente del segmento: el factor de riesgo del Anexo 1 o
    la regla del Anexo 2.
  - La calificación final es la **peor** entre la del modelo, la altura de mora, la evaluación de cartera,
    la calificación por análisis (FR-101), el arrastre y la regla de reestructurados. Arrastre,
    reestructurados, evaluación y análisis se aplican igual en los dos modelos, salvo que la norma diga
    otra cosa.
  - La calificación diaria es **informativa**; la oficial es la del cierre mensual (FR-107).
- **FR-100 · Arrastre.**
  - Es mensual. Cuando un crédito del deudor queda en B o peor, lleva a esa categoría los demás créditos
    **de la misma modalidad** del deudor.
  - No alcanza a sus codeudas.
  - **Excepción de aportes**: aplica cuando la suma de los saldos del deudor está cubierta por sus
    aportes, ponderados como garantía admisible, y sólo si la entidad no tiene pérdidas y cumple
    solvencia. Esos datos vienen de una consulta definida a Contabilidad (D19).
- **FR-101 · Calificación por análisis.**
  - El analista puede llevar un crédito a una categoría **peor** que la que resulta de las reglas, con
    motivo y soporte. Nunca a una mejor.
  - Queda vigente hasta que se levante, también con motivo.
- **FR-102 · Evaluación de cartera.**
  - La evaluación periódica que exige la norma (semestral o anual según el tipo de entidad, con sus meses
    de corte y de efecto) se hace como un **lote**: lo recomienda el comité de riesgos, lo aprueba el
    consejo con acta y aplica los criterios de la norma.
  - La recalificación que resulta se refleja en el cierre del mes que corresponda y se mantiene hasta la
    siguiente evaluación.
- **FR-103 · Deterioro individual por altura de mora (Anexo 1).**
  - **Porcentaje**: el vigente para la categoría y los días de mora de cada modalidad.
  - **Base**: el saldo de capital, menos el valor admisible de las garantías. Ese valor se descuenta según
    el **tiempo de mora**, con tablas distintas para garantías hipotecarias y no hipotecarias.
  - **Aportes**: se descuentan, repartidos a prorrata entre los créditos del deudor, sólo en las
    condiciones de la norma (sin pérdidas, con los requisitos de solvencia del tipo de entidad). En los
    fondos de empleados, también el ahorro permanente.
  - **Créditos suspendidos**: los intereses, seguros y otros conceptos causados y no pagados se deterioran
    al cien por ciento.
- **FR-104 · Deterioro general.**
  - El porcentaje mínimo que fije la norma sobre la cartera bruta, con sus ajustes por tipo de entidad.
  - La cooperativa puede configurar un **deterioro general adicional** mayor, y su uso para compensar el
    individual en los términos de la norma.
  - Si el deterioro general se aplica a los segmentos con pérdida esperada, y con qué porcentaje, es un
    parámetro por modelo y tipo de entidad, con fuente.
- **FR-105 · Modelo de deterioro por cooperativa y por segmento.**
  - El modelo se elige **por cooperativa y por segmento**:
    - **pérdida esperada** (Anexo 2) para consumo con libranza, consumo sin libranza y comercial de
      persona natural, en las entidades obligadas (FR-018);
    - **altura de mora** (Anexo 1) para los demás segmentos y para las entidades no obligadas.
  - Una entidad no obligada puede **adoptar** voluntariamente la pérdida esperada, y luego **desistir**:
    - con acta del consejo y estudio de impacto adjunto;
    - con la fecha y el soporte del aviso a la Superintendencia;
    - con las tablas de probabilidad propias de quien la adopta voluntariamente.

    La fecha desde no puede ser anterior a la del aviso más la antelación que fije la norma, que es un
    parámetro con vigencia y fuente (FR-014).
  - El cambio de modelo tiene fecha desde, acta y motivo, y nunca recalcula cierres anteriores.
  - **Historial de mora**:
    - un crédito tiene el historial **completo** a una fecha si cubre, hacia atrás desde ella, los meses
      que pide el modelo según la vigencia de FR-015 a esa fecha o, si es más joven, todo el tiempo desde
      su desembolso. Los meses posteriores al corte de la migración se dan por cubiertos por las
      transacciones. La fecha de referencia es la del primer cierre mensual en pérdida esperada;
    - **adopción voluntaria**: se niega si un crédito vivo de un segmento cubierto no tiene el historial
      completo, y lista esos créditos;
    - **obligación**: registrar la vigencia que obliga nunca se niega por falta de historial (la
      activación sí, FR-142). Registrarla lista los créditos sin historial completo y alerta al contador
      y al jefe de cartera. El historial se completa por la carga complementaria (FR-140) hasta el
      primer cierre en pérdida esperada;
    - si en ese cierre todavía falta, se aplica la pérdida esperada con el historial disponible, y esos
      créditos quedan listados en el cierre y en el reporte de cambios. Los meses faltantes siguen la
      **regla de meses faltantes**, con vigencia y fuente: por cada mes que falta, qué altura de mora se
      toma (la mayor observada en el historial disponible, la del corte o la que fije la contadora). Se
      siembra «la mayor observada», «pendiente de validar».
  - **Obligación sobrevenida**: una vigencia nueva del perfil (FR-018) o de la regla de obligación
    (FR-015), registrada después de registrar la activación (no de la fecha de activación), puede dejar
    obligada a una cooperativa que ya tiene Cartera activa.
    - Los segmentos cubiertos pasan a pérdida esperada desde el primer cierre mensual que cae dentro de
      esa vigencia.
    - El soporte es el acto o la norma que la impone. No es una adopción voluntaria y no admite
      desistimiento.
    - Mientras K6a no esté, registrar esa vigencia alerta al contador y al jefe de cartera. Los cierres
      siguen con el Anexo 1 y llevan la marca «modelo obligatorio no disponible» en el cierre y en el
      reporte de cambios, y como advertencia al generar el formato del SICSES, **nunca dentro del
      archivo** (FR-136), hasta que K6a esté (D28).
  - **Entregas** (decidido por el dueño, Q2): el Anexo 1 entra en K1 y la pérdida esperada en K6a de esta
    misma feature. Los dos quedan parametrizables como se describe arriba.
  - **Mientras K6a no esté**:
    - una cooperativa obligada al modelo de referencia no puede activar Cartera en producción (FR-142);
    - en los ambientes de prueba se activa con el Anexo 1 y la marca «modelo obligatorio no disponible»;
    - ninguna cooperativa puede adoptar el modelo voluntariamente.
- **FR-106 · Pérdida esperada.** Cuando el modelo aplique a un segmento, sus componentes son parámetros
  con vigencia y fuente:
  - probabilidad de incumplimiento por segmento, tipo de organización y variables del modelo de
    referencia, incluido el historial de mora;
  - pérdida dado el incumplimiento, por tipo de garantía y tiempo en incumplimiento;
  - exposición.

  El resultado se explica crédito por crédito.
- **FR-107 · Cierre mensual.** Al cierre del mes, ordenado por un usuario con permiso o programado:
  - calcula la calificación oficial, el deterioro de cada crédito y el general al último día del mes;
  - **congela** los resultados, que no se reescriben;
  - emite «deterioro calculado»: el ajuste frente al cierre anterior, por crédito o agregado según el
    modo de paso. Incluye reintegros y traslados entre general e individual;
  - emite **«cartera reclasificada por calificación»**: el capital y los intereses que cambian de
    categoría, con la anterior y la nueva;
  - entrega el **reporte de cambios**: créditos que cambian de categoría, la anterior, la nueva y la causa;
  - muestra el **cuadre** con Contabilidad (FR-130).

  Sólo se puede reabrir el último cierre, con motivo y atribución y antes de que Contabilidad cierre ese
  período (lo informa la consulta del estado del período). Reabrir emite los reversos de sus hechos. El
  primer cierre tras la migración se compara con el **cierre base** (FR-140).

#### J · Libranza y nómina

- **FR-108 · Pagadurías y convenios.**
  - La pagaduría es una persona jurídica del maestro. Su convenio tiene:
    - vigencia y fechas de corte, de envío y de pago;
    - el formato de archivo de ida y de vuelta;
    - los contactos;
    - el tope de descuento y las reglas de novedades;
    - la ventana de giro (FR-114).
  - El registro de la cooperativa como operador de libranza, cuando la ley lo exige, es un dato de la
    cooperativa que se valida antes de originar por libranza.
- **FR-109 · Vínculo deudor–pagaduría.**
  - Cada crédito por libranza indica la pagaduría, la identificación del empleado ante ella y la
    certificación laboral y de descuento (adjunta) con su fecha.
  - Un cambio de pagaduría es una novedad del crédito.
- **FR-110 · Capacidad de descuento (Ley 1527 de 2012).**
  - Al originar por libranza, el total de descuentos (los vigentes más la cuota nueva) no puede superar
    el tope legal sobre el **salario o la pensión después de los descuentos de ley**. Es un parámetro con
    piso legal (D13).
  - Se verifica contra la certificación de la pagaduría. Si no alcanza, se rechaza mostrando la capacidad
    disponible.
- **FR-111 · Prelación de descuentos.**
  - Las libranzas se descuentan en orden cronológico de su fecha.
  - La cooperativa conserva la preferencia que le da la ley sobre otros descuentos, salvo los de
    alimentos. Son reglas con fuente.
- **FR-112 · La nómina propia como pagaduría.**
  - La cooperativa es pagaduría de sus propios empleados.
  - Nómina obtiene por la consulta (FR-061) la cuota a descontar de cada empleado en el período.
  - Al aprobar la nómina, aplica los descuentos por el contrato, en la forma sin interacción, con la clave
    «identificador de la versión aprobada de la nómina + obligación».
  - En la misma llamada informa lo que **no se pudo descontar**, con su motivo, y queda como no aplicado.
    Desde K2 sigue la regla de su convenio (FR-115). Hasta K2 queda marcado en el crédito, la cuota sigue
    exigible y se alerta al jefe de cartera (FR-016).
  - Un descuento que Cartera rechaza no impide aprobar la nómina: queda como no aplicado con su motivo.
  - Un excedente, por ejemplo una cuota que ya se pagó por caja, vuelve a Nómina como excedente del
    empleado.
- **FR-113 · Archivo de descuentos.**
  - Por pagaduría y período, con la antelación que fija el convenio, Cartera genera el archivo con las
    cuotas a descontar: exigible del período y, si el convenio lo permite, lo vencido.
  - Usa el formato vigente del convenio, como dato versionado, e incluye las novedades: altas, cambios de
    valor y cancelaciones.
  - Queda **inmutable** con su huella. Regenerarlo crea una versión nueva y deja la anterior
    reemplazada.
  - Se entrega a Recaudo como mensaje «descuentos por pagaduría». Recaudo confirma el envío a la
    pagaduría, y entonces queda marcado enviado.
- **FR-114 · Descontado pendiente de giro.**
  - El mensaje «resultado de descuentos» de Recaudo marca lo descontado como **pendiente de giro**.
    Recaudo lo aplica por el contrato al recibir el giro, con fecha valor la del descuento.
  - **Ventana de giro**: es un parámetro con fuente. Por defecto, tres días tras el descuento (Ley 1527,
    art. 6). Vencida, la pagaduría queda en mora, con alerta y gestión ante ella.
  - **Umbral de mora de la pagaduría**: también es un parámetro con fuente. Por defecto, los días que fija
    la CBCF para informar al deudor e iniciar su calificación individual.
  - Mientras la mora de la pagaduría no supere ese umbral, **no** se imputan al deudor días de mora,
    cambio de calificación, gastos de cobranza ni reporte negativo.
  - Superado el umbral, se informa al deudor y su crédito se califica individualmente.
- **FR-115 · Respuesta y no aplicados.**
  - La respuesta de la pagaduría la procesa **Recaudo**, que devuelve a Cartera el mensaje «resultado de
    descuentos»: descontado (pendiente de giro, FR-114) y no aplicado.
  - Por cada **no aplicado** y su motivo (retiro, saldo insuficiente, prelación de otro descuento o de
    alimentos, incapacidad, licencia u otro), Cartera:
    - lo marca en el crédito;
    - avisa al deudor por sus canales autorizados;
    - lo pasa a pago por caja o a cobranza, según la regla del convenio.
  - Un reporte por pagaduría y período cuadra lo enviado, lo aplicado, lo pendiente de giro y lo no
    aplicado.
- **FR-116 · Cambio de empleador o retiro.**
  - Si el deudor pasa a otro empleador con convenio, la libranza se **traslada** a la nueva pagaduría
    conservando su fecha original para la prelación.
  - Si se retira sin nuevo empleador, el crédito pasa a la forma de pago alterna configurada (por defecto
    caja), con aviso al deudor.
  - Un cambio entre con y sin libranza emite «dimensión contable cambiada» (FR-125), porque cambia la
    cuenta contable.

#### I · Cobranza

- **FR-117 · Etapas.**
  - Etapas configurables por días de mora, con acciones sugeridas, plantillas de mensajes y gastos de
    cobranza aplicables a cada una.
  - Por defecto: preventiva (antes del vencimiento), administrativa, prejurídica y jurídica.
  - El proceso diario mueve cada crédito a la etapa que le corresponde.
- **FR-118 · Gestores y agenda.**
  - Créditos y deudores se asignan a gestores por reglas (oficina, etapa, saldo, línea, pagaduría) o a
    mano. El reparto se hace por número de créditos o por saldo, según la regla, y la reasignación queda
    auditada.
  - Cada gestor tiene una agenda con sus compromisos del día.
- **FR-119 · Gestiones y promesas.**
  - Cada gestión registra fecha, canal, persona contactada (deudor o codeudor), si fue un **contacto
    directo** (comunicación efectiva con el titular), resultado y, si la hay, **promesa de pago** con
    fecha y valor.
  - Una promesa se marca cumplida sola cuando entra el pago, o incumplida al vencer, y entonces vuelve a
    la agenda.
- **FR-120 · Acuerdos de pago.**
  - Un acuerdo sobre lo vencido, **sin cambiar las condiciones del crédito**, tiene su propio
    calendario, se sigue solo contra los pagos y, si se incumple, vuelve a la etapa que corresponda.
  - Si cambia las condiciones, es una novedad (FR-090) y se tramita como tal.
- **FR-121 · Gastos de cobranza.**
  - Se causan sólo según las reglas configuradas por etapa: base, tarifa, tope y momento.
  - Siempre con su soporte y **informados al deudor antes** de cobrarlos.
- **FR-122 · Mensajería con la Ley 2300 de 2023.** Los mensajes por SMS, correo y WhatsApp a deudores y
  codeudores cumplen estas reglas:
  - salen **sólo por los canales que el titular autorizó**, con la evidencia de la autorización;
  - respetan los **horarios** legales como techo (lunes a viernes de 7:00 a 19:00, sábados de 8:00 a
    15:00, nunca domingos ni festivos). La cooperativa sólo puede restringirlos;
  - tras un contacto directo, se usa **un solo canal por semana calendario** y no se contacta más de una
    vez al día;
  - **nunca** se contacta a referencias personales;
  - lo programado fuera de ventana o por encima del tope se corre a la siguiente ventana permitida, con
    constancia;
  - cada envío registra plantilla, canal, destinatario, fecha y estado de entrega.

  El correo usa el envío de la plataforma. SMS y WhatsApp entran por adaptadores con canal simulado
  hasta que la cooperativa tenga proveedor.
- **FR-123 · Aviso previo al reporte negativo (Leyes 1266 de 2008 y 2157 de 2021).**
  - Antes de **cada** reporte negativo, por obligación y por episodio de mora, se envía la comunicación
    previa con la anticipación legal como piso (20 días calendario desde el envío).
  - Las obligaciones bajo el umbral legal exigen dos comunicaciones en días distintos.
  - Queda constancia de cada envío. Sin constancia, ese dato negativo no se incluye en el archivo de
    centrales (FR-137).
  - Un reporte que se haya hecho sin aviso se retira en el siguiente archivo.
- **FR-124 · Cobro jurídico.**
  - Un crédito en etapa jurídica se entrega a un abogado, que es una persona del maestro.
  - Se registran el estado del proceso, sus actuaciones, documentos, honorarios y gastos.
  - Lo que se cobre al deudor sigue la regla de gastos de cobranza.
  - Un proceso de insolvencia suspende el cobro (FR-098).

#### K · Integración contable

- **FR-125 · Hechos que emite Cartera.** Cartera **no registra asientos**. Emite como mensajes de negocio
  versionados todo cambio de saldo o de la categoría contable de un saldo:
  - **los que pide la solicitud**: crédito desembolsado, interés causado, mora causada, causación
    suspendida, pago aplicado (por concepto), pago reversado, crédito reestructurado, deterioro
    calculado, crédito castigado, condonación aprobada y seguro causado;
  - **los que esta especificación agrega** para que ningún cambio quede sin hecho:
    - desembolso reversado y crédito ajustado desde Comercial;
    - crédito modificado y crédito novado;
    - causación reanudada y cobro de interés en suspenso;
    - gasto de cobranza causado y ajuste de causación por fecha valor;
    - anticipo recibido y anticipo aplicado;
    - saldo a favor reconocido y saldo a favor devuelto o aplicado;
    - cartera reclasificada por calificación;
    - **dimensión contable cambiada**: cuando cambia cualquier dimensión de FR-126 de un saldo vivo, con
      la anterior y la nueva. Por ejemplo, con o sin libranza, fin de la gracia, deudor que pasa a ser o
      deja de ser empleado, cambio de oficina, modalidad o sub-segmento;
    - recuperación de castigada;
    - cobro provisional incorporado (FR-142);
    - reverso de cierre.
- **FR-126 · Contenido de cada hecho.** Cada hecho lleva:
  - tipo, versión, clave única y el **enlace al hecho original** cuando depende de otro: un reverso, de
    su pago; un ajuste, de su desembolso;
  - todas las dimensiones que la matriz necesita:
    - línea, modalidad y sub-segmento;
    - calificación y concepto;
    - si el deudor es empleado, si está en período de gracia y si es un pago por cuenta del asociado;
    - oficina;
    - tercero: el deudor, o la pagaduría o la aseguradora cuando aplique;
    - número del crédito como documento cruce;
  - fecha contable, valores por concepto y el documento de Cartera que lo originó.

  Usa el sobre común de la bandeja y **nunca lleva cuentas contables**.
- **FR-127 · Validación previa.**
  - Antes de confirmar una operación, Cartera pregunta a Contabilidad si es contabilizable: si la matriz
    tiene regla para cada combinación.
  - Según el parámetro, la falta **advierte** o **bloquea**, y dice qué regla falta.
  - Sin respuesta de Contabilidad se aplica la política configurada (D7).
- **FR-128 · Modo de paso por hecho.**
  - Cada tipo de hecho tiene su modo con vigencia, el mismo de la 012:
    - **en línea**;
    - **por lotes**, con comprobante por hecho o **resumido**. Por ejemplo, la causación diaria en un
      comprobante por día que **conserva todas las dimensiones** de la matriz y la lista de créditos;
    - **no pasa**.
- **FR-129 · Matriz contable de Cartera.**
  - Vive del lado de Contabilidad: hecho y rol, más las dimensiones de FR-126, dan la cuenta débito y
    la crédito, con vigencia y especificidad.
  - Tiene reporte de completitud y plantilla de importación.
  - Ninguna cuenta contable vive en Cartera.
- **FR-130 · Conciliación.** A cualquier fecha compara los saldos de Cartera con los saldos contables de
  las cuentas mapeadas:
  - capital e intereses por modalidad, sub-segmento y categoría;
  - deterioro individual y general;
  - anticipos y saldos a favor;
  - suspenso y castigada en control.

  Cada diferencia se explica con los mensajes pendientes, rechazados o que no pasan. Se presenta en cada
  cierre mensual. Un descuadre sin explicar alerta al contador y a cartera.

#### M · Documentos y reportes

- **FR-131 · Documentos al deudor.** Cartera emite:
  - plan de pagos;
  - extracto de un período;
  - paz y salvo de una obligación o general, sólo si el saldo es cero y sujeto a firmeza (FR-082). El
    general:
    - certifica sólo las obligaciones de Cartera, y así lo dice el documento;
    - se niega también si la persona tiene un mensaje de venta en reproceso (FR-053), y la negativa dice
      cuál. Las ventas aceptadas sin migrar (FR-143) no lo niegan: el documento las menciona como fuera
      de Cartera;
    - incluye o no las codeudas según un parámetro de la cooperativa, y el documento dice cuál se
      aplicó;
  - certificado de saldo a una fecha;
  - certificado tributario del año: intereses pagados por crédito, con los de vivienda separados, y el
    saldo de la deuda al 31 de diciembre.

  Cada documento lleva número, fecha, firma de la cooperativa y la copia de la identificación del deudor
  tal como se emitió. Se imprime o se envía por correo en un paso, y su emisión queda auditada.
- **FR-132 · Verificación de autenticidad.**
  - Cada documento lleva un código de verificación. Un usuario de la cooperativa lo verifica y obtiene si
    es auténtico o está **anulado**, más sus datos esenciales.
  - Un documento pierde validez, por ejemplo, cuando se reversa el pago que canceló el crédito.
  - La verificación pública y anónima queda fuera de alcance (D21).
- **FR-133 · Reportes de gestión.**
  - Cartera por edades, con rangos configurables.
  - Colocación por período, línea, oficina y asesor.
  - Recaudo por línea, recaudador y modalidad.
  - Créditos por etapa de cobranza, promesas y acuerdos.
  - Libranza por pagaduría, con pendientes de giro.
  - Usura: créditos topados y líneas a menos de los puntos configurados de la usura.
- **FR-134 · Calidad, cosechas y concentración.**
  - **Indicadores de calidad**: cartera vencida sobre bruta, cartera en riesgo (B a E) sobre bruta y
    cobertura del deterioro.
  - **Cosechas**: por mes de desembolso, la mora a 30, 60 y 90 días en cada mes de maduración.
  - **Concentración**: mayores deudores y grupos conectados, por línea, sector y pagaduría, con su
    porcentaje del límite individual (FR-041).
- **FR-135 · Tablero.**
  - Indicadores de saldo, colocación del mes, calidad, deterioro, recaudo frente al esperado, cosechas y
    cumplimiento de promesas.
  - Se ven por oficina y línea, limitados al alcance del usuario.
- **FR-136 · Formato de cartera del SICSES.**
  - En cada período de reporte, que según el nivel de supervisión fija el tipo de entidad, Cartera
    genera el **formato de cartera de créditos** del reporte a la Superintendencia (SICSES).
  - El layout es un **dato versionado** con vigencia. Mientras no se haya cotejado contra el instructivo
    vigente, se genera con la advertencia «layout sin cotejar».
  - Antes de generar aplica las validaciones de forma. El archivo queda inmutable con su huella, y la
    carga en el portal la hace una persona.
  - Si el cierre lleva la marca «modelo obligatorio no disponible» (D28), la generación la muestra como
    advertencia, igual que «layout sin cotejar», y la deja registrada con el archivo. El archivo sigue el
    layout vigente con los datos del modelo efectivamente aplicado.
- **FR-137 · Archivos para centrales de riesgo.**
  - Por central y período, Cartera genera el archivo en su layout versionado. Incluye las novedades
    (desembolsos, pagos, mora, cancelaciones, castigos, reclamos en trámite y retiros de reportes sin
    aviso).
  - Incluye sólo a quienes tienen autorización de reporte.
  - Un dato negativo sólo se incluye si hay aviso previo con constancia para ese episodio (FR-123). Lo
    excluido queda listado para gestión.
- **FR-138 · Exportación.**
  - Todo reporte se puede exportar con el permiso de exportación propio, y cada exportación queda
    auditada.
  - Los reportes respetan el alcance de oficina y línea del usuario.

#### N · Migración, activación y retiro de lo heredado

- **FR-139 · Plantilla de migración.** Carga los créditos vigentes al corte y, por factura, las ventas a
  crédito anteriores a la activación ya canceladas al corte (FR-143), con revisión previa, todo o nada y
  errores por fila y columna:
  - deudor, línea, número del crédito de origen, factura (si nació de una venta) y condiciones pactadas,
    incluida la preferencia de imputación de abonos;
  - cuotas pendientes con su capital, interés y seguros. El plan pendiente debe sumar el saldo de
    capital;
  - saldos por concepto al corte, incluidos el interés en suspenso, los anticipos y los saldos a favor;
  - días de mora y calificación al corte, deterioro individual y general, y tiempo en E;
  - datos de reestructuración: conteo, fecha, calificación al reestructurar y cuotas consecutivas
    pagadas;
  - historial de mora mensual por crédito, de los meses que pide el modelo de referencia (FR-015).
    **Opcional**: que falte no rechaza la carga, salvo en los créditos de los segmentos cubiertos de una
    cooperativa obligada a la fecha de activación o con una obligación ya registrada desde una fecha
    posterior (FR-142);
  - garantías, codeudores, pagaré, pagaduría y etapa de cobranza;
  - la **cartera castigada**, con su saldo, para registrar sus recuperaciones;
  - los cobros recibidos por fuera durante el crédito provisional de la 012, por factura;
  - **cupos** (consolidado, por línea y rotativo) y **bloqueos o embargos** vigentes;
  - **autorizaciones del titular**: tratamiento, consulta y reporte a centrales y canales de contacto,
    con la evidencia y la versión del texto que tenga la cooperativa (FR-006);
  - **avisos previos** ya enviados y reportes vigentes en centrales por episodio, y **reclamos en
    trámite**;
  - **personas vinculadas** con su fundamento.
- **FR-140 · Saldo inicial y cierre base.**
  - Cada crédito migrado nace con una **transacción de saldo inicial por concepto**, fechada al corte.
    La historia anterior no se reconstruye, aunque el historial de pagos previo puede cargarse como
    información.
  - El saldo inicial **no** genera hechos contables: Contabilidad ya tiene sus saldos de apertura.
  - Se crea un **cierre base** con la calificación y el deterioro migrados, sin hechos. El primer cierre
    mensual emite sólo la diferencia frente a él.
  - Desde que hay una carga aplicada, también antes de activar y antes de registrar una adopción
    voluntaria, y hasta el primer cierre en pérdida esperada de los segmentos cubiertos, se admite una
    carga complementaria del historial de mora de los créditos de la carga, como información, sin
    transacciones ni hechos (FR-105).
- **FR-141 · Cuadre antes de activar.**
  - Cartera compara los saldos migrados con los saldos contables de las cuentas mapeadas a la fecha de
    corte: por concepto, modalidad, sub-segmento, categoría y cuenta.
  - No se puede activar con diferencias, salvo aceptarlas con motivo y acta (D17).
- **FR-142 · Activación.**
  - Cartera opera en una cooperativa desde la fecha de activación que registra un usuario con permiso.
  - **La fecha de activación es el día siguiente a la fecha de corte de la carga aplicada**, aunque se
    registre después:
    - la causación se pone al día desde ella (FR-066);
    - los pagos de esos días entran por el contrato con su fecha valor (FR-012);
    - activar con un corte que no cumpla esta regla se niega con el motivo;
    - mientras no se active, una carga aplicada se reemplaza entera por otra, con el mismo corte o con
      uno nuevo. La anterior queda «reemplazada», no se borra, y sirve de ensayo;
    - con un corte nuevo posterior, los pagos que Recaudo conserva con fecha valor hasta ese corte deben
      estar reflejados en la carga. Al activar, el contrato los rechaza con el motivo «anterior al corte»;
    - al registrar la activación, Cartera recalcula el cuadre (FR-141) y la conciliación de ventas
      (FR-143) con los saldos contables de ese momento. Si difieren de los guardados al aplicar la carga,
      la aceptación anterior (D17) deja de valer y hay que volver a aceptar las diferencias.
  - **Plazo para registrar la activación**:
    - se registra, a más tardar, dentro de los días de fecha valor de FR-012 contados desde el corte;
    - sólo si Contabilidad tiene abierto el mes que contiene el día siguiente al corte;
    - si pasó el plazo o ese mes ya está cerrado, la activación se niega y pide una carga con corte nuevo.
  - **Qué guarda la carga y qué nace al activar**:
    - aplicar una carga guarda sólo la carga, su revisión, su cuadre y su conciliación de ventas
      (FR-143);
    - al activar nacen, desde la carga vigente, los créditos, las transacciones de saldo inicial, el
      cierre base, los cupos, los bloqueos, las autorizaciones, los avisos, los reclamos y los
      vinculados;
    - lo registrado a mano antes de activar prevalece sobre la carga para la misma persona y concepto, y
      la revisión lo informa;
    - la aceptación de diferencias (D17) es de cada carga;
    - Personas sólo considera a los deudores de la carga vigente.
  - **Entre el corte y el registro de la activación**:
    - Recaudo conserva los pagos que el contrato rechazó con «Cartera no activada» y los aplica al
      activar, con su fecha valor;
    - **cobros provisionales**: los cobros por fuera de ventas confirmadas después del corte, que se
      registraron en Contabilidad o en Tesorería durante el crédito provisional, se cargan al registrar la
      activación con la **plantilla de cobros provisionales**:
      - lleva factura, valor, fecha valor y referencia del registro contable, y se aplica con revisión
        previa y todo o nada;
      - creada la obligación de su venta, cada cobro se registra como transacción de pago con su fecha
        valor, sin recaudador y en firme desde que se aplica;
      - emite el hecho «cobro provisional incorporado», que la matriz registra contra la cuenta por cobrar
        provisional que Contabilidad redujo al recibir el cobro, nunca contra caja ni bancos;
    - aprobar una nómina ordinaria o una definitiva que incluya a deudores de la carga aplicada exige
      reconocer la alerta «Cartera por activar» (E-03);
    - al activar, Cartera marca como no aplicada, con el motivo «Cartera sin activar», cada cuota de un
      crédito migrado con forma de pago nómina propia que venció entre el día siguiente al corte y el
      registro de la activación y sigue impaga. La cuota sigue exigible y se alerta al jefe de cartera
      (FR-112). La marca no depende de qué nóminas se aprobaron ni de lo que Nómina informe;
    - las cuotas vencidas en ese intervalo no generan gastos de cobranza, mensajes ni avisos previos con
      fechas anteriores al registro.
  - Activar habilita los procesos diarios y la recepción de mensajes de Comercial.
  - La activación muestra lo que Comercial debe hacer en sus parámetros, porque Cartera no los escribe:
    - fijar su fecha de habilitación de Cartera igual a la de activación;
    - revisar su política «sin respuesta»;
    - fijar el valor de «quién registra la cuenta por cobrar» (FR-056).
  - Antes de activar sólo se puede parametrizar, simular, migrar, otorgar cupos y registrar
    autorizaciones y personas vinculadas.
  - **Cupos antes de activar**: otorgar un cupo evalúa endeudamiento consolidado (como deudor y como
    codeudor), grupo conectado, concentración y personas vinculadas contra la carga aplicada vigente,
    como si ya hubiera nacido.
    - Al activar, y al reemplazar la carga, Cartera revalida cada cupo otorgado a mano contra lo que nace
      de la carga.
    - El cupo que exceda un límite, o el de un vinculado sin decisión del consejo, queda bloqueado
      (FR-060) con el motivo «cupo por revalidar» y alerta al jefe de cartera, hasta que se ajuste o el
      consejo decida.
  - **Antes de la activación**, falten o no Recaudo y Tesorería, las interfaces que Cartera ofrece están
    desplegadas y responden sin error:
    - **Consulta de obligaciones** (FR-061): no informa obligaciones exigibles, porque una carga aplicada
      no es obligación hasta activar. Nómina aprueba la definitiva y la ordinaria sin descuentos de
      Cartera. Personas ve a los deudores de una carga aplicada como «migrados, sin activar» y no deja
      borrarlos.
    - **Contrato de obligaciones**: listar y simular responden sin obligaciones, con el motivo «Cartera no
      activada»; aplicar, reversar y resolver saldos se rechazan con ese mismo motivo.
    - **Consultas de Comercial**: la de cupo y estado, la de estado de validación y la reserva y
      liberación de número responden «Cartera no habilitada», como hoy en la 012. Comercial lo trata como
      crédito provisional, no como «Cartera no responde».
    - **Mensajes de Comercial**: se acumulan sin consumirse.

    Una prueba de integración recorre, con Cartera desplegada y sin activar, la definitiva, la nómina
    ordinaria, el borrado de una persona y una venta a crédito.
  - **Requisitos para activar en producción**:
    - **Recaudo y Tesorería disponibles en la cooperativa** (FR-089). «Disponible» significa que la
      plataforma tiene registrados allí:
      - un recaudador de clase Recaudo que adopta una versión del contrato de obligaciones que Cartera
        tenga vigente (FR-086);
      - un destino Tesorería que adopta una versión de la orden de giro que Cartera tenga vigente (D27).

      Cartera lo lee de la plataforma. Un recaudador o una tesorería de prueba no cuentan. El carácter de
      producción lo fija la configuración del despliegue, no la cooperativa.
    - si la cooperativa es **obligada** (abajo), K6a desplegada.

    Si falta alguno, la activación se niega diciendo qué falta.
  - **Cooperativa obligada**: lo es si, al registrar la activación, está obligada a la pérdida esperada a
    la fecha de activación, o tiene registrada una vigencia del perfil (FR-018) o de la regla de
    obligación (FR-015) que la obliga desde una fecha posterior.
    - En todo ambiente donde K6a esté desplegada se exigen además los parámetros de FR-106 vigentes a la
      fecha del primer cierre mensual que los use, y el historial de mora completo a esa fecha (FR-105)
      de los créditos migrados de los segmentos cubiertos. Si falta algo, la activación se niega y lista
      lo que falta.
    - En los ambientes de prueba sin K6a se activa con el Anexo 1 y la marca «modelo obligatorio no
      disponible» (FR-105).
- **FR-143 · Ventas anteriores a la activación.**
  - Lo vendido a crédito hasta la fecha de corte entra **sólo por la migración** (FR-139): saldo neto al
    corte, cobros externos incluidos y sin hechos contables. Las ventas a crédito de servicios a
    asociados no vienen a Cartera (Q3).
  - **Al aplicar la carga**, Cartera lista sin consumirlos los mensajes acumulados de ventas hasta el corte
    y los concilia por factura con la carga:
    - **migrada**;
    - **cancelada antes del corte**: con saldo cero al corte (pagada por fuera, anulada o cubierta por
      notas), cargada en la plantilla con factura, valor, cobros externos y ajustes, o marcada así desde
      la lista con motivo y atribución;
    - **no migrada con saldo**.
  - La conciliación forma parte del cuadre de FR-141. La activación se niega mientras haya ventas no
    migradas con saldo, salvo que se acepten con motivo y acta (D17).
  - **Al activar**, Cartera responde a cada venta conciliada:
    - a la **migrada**: «obligación creada», con el número migrado y el estado «migrada»;
    - a la **cancelada antes del corte**, cargada o marcada desde la lista: le crea una obligación en
      cero, con número, en la línea que trae la carga o, si se marcó desde la lista, en la línea por
      defecto de origen comercial. Responde «obligación creada» con ese número y el estado «cancelada
      antes del corte»;
    - a la **no migrada con saldo aceptada con acta** (D17): la aceptación la declara fuera de Cartera,
      venta por venta y con motivo. Responde «obligación rechazada» con el motivo «aceptada sin migrar en
      la activación», que no queda para reproceso (FR-053). Su cobro sigue fuera de Cartera.
  - La consulta de estado de validación informa las migradas y las canceladas como validadas, y las
    aceptadas sin migrar como «no incorporadas».
  - **Ajustes acumulados de esas ventas**: se concilian por fecha.
    - Los fechados hasta el corte se dan por procesados, porque ya están en el saldo neto.
    - Los posteriores siguen FR-054 sobre la obligación de la venta, migrada o en cero: una nota crédito o
      una anulación dejan saldo a favor, y una nota débito crea saldo y plan.
    - Los de una venta aceptada sin migrar se le informan a Comercial como «sin obligación».
  - Ninguna venta anterior a la activación se procesa como mensaje en vivo.
- **FR-144 · Retiro de la cartera de crédito heredada.**
  - Un diagnóstico por ambiente **clasifica cada tabla heredada** como de crédito (se retira) o de otro
    módulo (se conserva), con las llaves que las unen. Si alguna de crédito tiene datos, el retiro se
    detiene y se decide con el dueño.
  - **Se retiran**: solicitudes, créditos, transacciones, cuotas, causación, clasificación, provisiones,
    cobranza, deducciones, garantías, codeudores, reestructuraciones, tasas, scoring, seguros, cuotas
    extraordinarias, facturas de cartera y parámetros de línea, con sus rutas y pantallas.
  - **Quedan intactos**: ahorros, depósitos, aportes, retiros, caja y SIPLA. Ninguna tabla que se
    conserva queda apuntando a una retirada. Lo compartido (actas, listas) se decide en el diagnóstico.
  - El retiro es una migración destructiva con respaldo, segundo revisor y marcador de aprobación.
  - El menú queda con las pantallas nuevas y conserva las de ahorros y aportes. Ninguna dice el nombre
    del sistema anterior.

#### Enmiendas a otras features

- **FR-145 · E-02 · Comercial.**
  - Sus dos consultas a Cartera pasan a la implementación real (FR-053, FR-059), y suma las operaciones
    de reservar y liberar número (FR-057).
  - Su mensaje de venta pasa a la versión 2 con el número reservado.
  - Suma el consumidor de «obligación creada» y «obligación rechazada», que guarda el número del crédito
    en la venta.
  - Registra el destino Cartera en la bandeja.
  - Se ajustan los requisitos de la 012 que hoy no guardan respuesta de Cartera, y se cierran las tareas
    de su entrega IC.
- **FR-146 · E-01 · Contabilidad.** Es una enmienda de la 009, como la que hizo la 012 para Inventario:
  - consume los hechos de Cartera con la matriz de FR-129, el modo de paso y la validación previa;
  - ofrece a Cartera las consultas de la sección «Interfaces»: saldos mapeados, estado del período,
    indicadores de la entidad y cuentas bancarias de origen;
  - su contrato deja de asignar a Cartera comprobantes por llamada directa, y se retiran los escritores
    pendientes de la cartera heredada;
  - no cierra un mes que Cartera no haya cerrado, a partir del mes de la fecha de activación de Cartera
    en esa cooperativa (FR-142). Mientras Cartera no esté activada, Contabilidad cierra sus meses como
    hoy, y una prueba lo cubre con Cartera desplegada y sin activar;
  - mientras haya una carga aplicada sin activar, cerrar el mes que contiene el día siguiente a su corte
    advierte que esa carga ya no podrá activarse.
- **FR-147 · E-03 · Nómina.**
  - La liquidación definitiva y la nómina ordinaria dejan de leer y escribir datos de Cartera. Usan la
    consulta de FR-061 y el contrato de FR-083.
  - Se conservan la política de descuento al retiro, el ajuste de descuentos con motivo y la garantía
    «todo o nada» (FR-088).
  - Aprobar una nómina con deudores de una carga aplicada y sin activar exige reconocer la alerta «Cartera
    por activar» (FR-142).
  - Los festivos y el salario mínimo pasan a leerse de los valores nacionales de la plataforma, para que
    no haya dos fuentes.
- **FR-148 · E-04 · Personas.** El bloqueo de borrado y el resumen de créditos de la ficha usan la
  consulta de FR-061.
- **FR-149 · E-05 · Plataforma.**
  - La bandeja de mensajes admite a Cartera como **emisor**, con sus hechos y respuestas, y a Comercial,
    Recaudo y Tesorería como destinos (Tesorería recibe la orden de giro, FR-045).
  - A Recaudo y a Tesorería como emisores hacia Cartera («resultado de descuentos», «resultado del
    giro») los registra la feature de cada uno. Mientras no existan, en los ambientes de prueba los
    registran sus dobles (FR-089).
  - La plataforma lleva, por cooperativa, el **registro de recaudadores** del contrato y de **destinos
    activos** de la bandeja, que Cartera consulta para activar (FR-142). Por cada recaudador y destino
    guarda:
    - su clase: Recaudo, Nómina, Aportes, Tesorería o prueba;
    - las versiones del contrato o del mensaje que adopta.
  - Los formatos bancarios admiten el ámbito «desembolso de crédito» (FR-047).
  - Los parámetros admiten el **ámbito de línea de crédito**.
  - La auditoría encadenada incluye a Cartera.
  - Las notificaciones admiten **SMS y WhatsApp** por adaptador, con canal simulado.
  - Los **valores nacionales** (FR-015) tienen su registro central y su réplica, incluidos los festivos.

### Key Entities *(include if feature involves data)*

**Configuración**
- **Línea de crédito y su versión**: producto con modalidad y sub-segmento, tasas, montos, plazos,
  periodicidades, sistema de amortización, seguros, garantías, documentos, prelación, reglas de recálculo
  y vigencia.
- **Catálogo de modalidades**: modalidades y sub-segmentos regulatorios, con vigencia.
- **Valor nacional**: usura certificada por modalidad, índice, tope de vivienda, salario mínimo o
  festivo, con vigencia, fuente y fecha de registro. Se registra una vez y se replica.
- **Perfil regulatorio de la cooperativa**: tipo de entidad, actividad financiera autorizada, nivel de
  supervisión, categoría, grupo NIIF y modelo de deterioro por segmento. Tiene vigencia y soporte. La
  adopción y el desistimiento voluntarios llevan además acta, estudio de impacto y aviso a la
  Superintendencia.
- **Cupo y bloqueo**: cupo consolidado o por línea (rotativo para Comercial), con vigencia; reservas;
  bloqueos con motivo.

**Originación**
- **Solicitud de crédito**: solicitante, línea, condiciones simuladas, preferencia de imputación,
  destino, forma de pago, documentos, validaciones con evidencia, puntaje con explicación, nivel de
  aprobación y estado.
- **Persona vinculada**: el vínculo de una persona con la cooperativa y su fundamento legal.
- **Sesión y acta**: decisión colegiada con asistentes, quórum, votos por solicitud o lote y condiciones.
- **Garantía y pagaré**: tipo, valor, avalúo o póliza, vigencia, estado y documento. El pagaré, físico o
  desmaterializado, con su firma y custodia.
- **Reserva de número**: número reservado ligado a un documento de venta, con su estado.

**El crédito**
- **Crédito (obligación)**:
  - número, deudor, codeudores, línea, oficina y condiciones pactadas selladas;
  - estado: pendiente de giro, vigente, cancelado, castigado, anulado o novado;
  - marcas: validación fallida, reestructurado con su conteo, modificado, en jurídico, en insolvencia.
    Las marcas no alteran la causación ni el cobro;
  - origen: originación, Comercial o migración.
- **Plan de pagos y su versión**: cuotas con fecha, capital, interés, seguros y otros. Cada novedad crea
  una versión.
- **Transacción de cartera**: el hecho inmutable que cambia saldos.
  - Tiene tipo, fecha valor, fecha contable, fecha de registro, concepto, valor, cuota, actor, origen,
    documento, clave de idempotencia y enlace al reverso.
  - De ella se derivan saldos, cuotas pendientes, anticipos, saldos a favor, días de mora y calificación
    diaria.
- **Comprobante de aplicación**: la distribución de un pago por cuota y concepto, con el recaudador y la
  referencia de su recibo.
- **Novedad**: reestructuración, modificación, refinanciación, novación, prórroga, cambio de fecha o de
  tasa. Lleva causa, simulación, consentimientos, aprobación y efecto.
- **Condonación**, **lote de castigo** y **evaluación de cartera**: con conceptos, valores, nivel, acta y
  motivo.

**Riesgo y libranza**
- **Cierre mensual y cierre base**: calificación oficial, deterioro individual y general por crédito,
  reclasificaciones, reporte de cambios y cuadre. Son inmutables.
- **Pagaduría, convenio y archivo de descuentos**: con formato versionado, período, líneas enviadas,
  aplicadas, pendientes de giro y no aplicadas.

**Cobranza y habeas data**
- **Cobranza**: etapa de cobranza, asignación, gestión, promesa, acuerdo de pago y proceso jurídico.
- **Autorización y solicitud del titular**: tratamiento de datos, consulta y reporte a centrales, canales
  de contacto, revocaciones y reclamos, con evidencia, versión del texto, fechas y plazos.
- **Aviso previo**: obligación, episodio de mora, comunicaciones enviadas y su constancia.
- **Mensaje al deudor**: plantilla, canal, destinatario, ventana y estado de entrega.

**Documentos e integración**
- **Documento emitido**: tipo, número, código de verificación y estado (vigente, sujeto a firmeza o
  anulado).
- **Mensaje de negocio** recibido o emitido: tipo, versión, clave, origen, enlace al original, estado de
  procesamiento y respuesta.
- **Reporte regulatorio y archivo de centrales**: layout versionado, período, huella y estado de cotejo.
- **Carga de migración**: archivo, revisión, cuadre y aceptación de diferencias con acta.

## Success Criteria *(mandatory)*

### Measurable Outcomes

**Cálculo**
- **SC-001**: Liquidar un mismo crédito a la misma fecha, con la misma información registrada, da un
  resultado idéntico al centavo en el 100 % de las repeticiones. Se prueba con créditos de todos los
  sistemas de amortización, a fechas pasadas, de hoy y futuras.
- **SC-002**: El 100 % de los intentos de crear, simular, aprobar o desembolsar con una tasa efectiva por
  encima de la usura vigente se bloquea. Ningún crédito vivo causa un solo día interés corriente ni de
  mora por encima de ella.
- **SC-017**: En todos los sistemas de amortización, la suma del capital del plan es igual al monto
  desembolsado, al centavo.
- **SC-023**: En los **casos dorados** calculados a mano, la causación, la mora, la liquidación y el
  deterioro coinciden al centavo en el 100 %. Los casos cubren:
  - sistema de amortización, convención de días, modalidad y novedad;
  - el modelo de deterioro: el Anexo 1 y la pérdida esperada, con probabilidad de incumplimiento, pérdida
    dado el incumplimiento, exposición y deterioro crédito por crédito;
  - un crédito con historial de mora parcial tratado según la regla de meses faltantes.

**Pagos**
- **SC-003**: En el 100 % de los abonos extraordinarios, la elección entre reducir plazo o cuota es la
  del deudor: la que respondió o la preferencia que registró. Una sola simulación del contrato entrega
  los dos planes, y una sola llamada aplica la elección. Los tres pasos y los dos minutos de la pantalla
  de caja los hereda Recaudo.
- **SC-004**: Tras reversar un pago, con o sin pagos posteriores, los saldos por concepto, las cuotas, los
  días de mora y la calificación vigente son idénticos a los que habría si el pago no hubiera existido,
  en el 100 % de los casos de prueba.
- **SC-010**: Repetir tres veces cada desembolso, pago, reverso, reserva y mensaje con la misma clave
  produce cero duplicados.

**Integración**
- **SC-005**: En modo en línea y sin mensajes en cola, el 100 % de las ventas a crédito de la muestra de
  prueba aparece como crédito en Cartera en menos de cinco segundos desde la confirmación. La venta
  muestra el número del crédito, y la factura lo lleva cuando hubo reserva.
- **SC-006**: En cada cierre mensual de prueba, la diferencia entre Cartera y Contabilidad es **cero**
  por modalidad, sub-segmento, categoría y concepto, después de descontar los mensajes pendientes.
- **SC-018**:
  - una verificación automática encuentra cero accesos entre Cartera y otros módulos fuera de la lista
    de interfaces;
  - cero valores legales escritos en el código;
  - cero parámetros legales configurados por debajo de su piso o por encima de su techo.

**Rendimiento**
- **SC-007**: La causación diaria de una cooperativa con 50.000 créditos vivos termina en menos de
  30 minutos, sin afectar la operación de las demás cooperativas.
- **SC-008**: El 95 % de las consultas de cupo y estado responde en menos de un segundo, y el 95 % de las
  simulaciones de crédito o de pago, en menos de dos segundos.
- **SC-016**: El archivo de descuentos de una pagaduría con 5.000 obligaciones se genera en menos de un
  minuto. En la prueba:
  - el 100 % de los no aplicados queda identificado con su motivo;
  - ningún deudor recibe mora imputada mientras su pagaduría está dentro de la ventana de giro.

**Uso**
- **SC-009**: Cada operación frecuente de FR-013 se completa en tres pasos o menos, verificado una por una.
  Un asesor con la capacitación básica y sin experiencia previa en el módulo radica su primera solicitud
  completa en menos de diez minutos.

**Cumplimiento**
- **SC-011**: Cero mensajes de cobranza enviados fuera del horario, por encima de la frecuencia, por un
  canal no autorizado o a una referencia personal, verificado sobre el registro de envíos.
- **SC-012**: Cero datos negativos reportados a centrales sin aviso previo con constancia para su
  episodio, y cero consultas a centrales sin autorización registrada.
- **SC-015**: En la prueba de seguridad:
  - cero accesos exitosos fuera del permiso, la oficina, la línea o el rango de montos del usuario;
  - cero aprobaciones de la misma persona que radicó.
- **SC-022**: Cero desembolsos, cupos o novedades que lleven a una persona o grupo conectado por encima
  del límite individual de concentración. Las ventas de Comercial que lo excedan quedan con
  validación fallida y listadas como exceso.

**Riesgo**
- **SC-021**: En el conjunto de prueba de calificación, el 100 % de las calificaciones coincide con el
  cálculo manual: rangos por modalidad, arrastre por modalidad, reestructurados, pago único y
  subcategorías.

**Migración**
- **SC-013**: En la migración de prueba la diferencia con Contabilidad es cero por concepto, modalidad,
  sub-segmento, categoría y cuenta, y el primer cierre emite sólo el deterioro nuevo frente al cierre
  base.

**Auditoría, regulatorio y operación**
- **SC-014**: El 100 % de las operaciones queda auditado con actor, origen, antes/después y motivo. La
  verificación de integridad detecta el 100 % de las alteraciones introducidas en la prueba.
- **SC-019**: El formato de cartera de un cierre de prueba lo acepta el validador de la Superintendencia
  sin errores de estructura. Lo verifica el dueño, como en la PILA.
- **SC-020**: Ningún proceso diario deja de correr sin alerta. Un día sin causación se pone al día solo en
  la corrida siguiente, sin duplicados.

## Assumptions

- **Para quién es**: toda organización solidaria cliente del ERP (FR-018).
- **Moneda**: pesos colombianos. La UVR entra con vivienda (D18).
- **Volumen de referencia**: hasta 50.000 créditos vivos y 500 pagos por hora en la hora pico, por
  cooperativa.
- **Usuarios**:
  - el personal de la cooperativa, con roles nuevos sembrados: asesor, analista, jefe de cartera,
    comité, comité de riesgos, gerente, consejo, gestor de cobranza y oficial de cumplimiento;
  - el contador lee todo y genera reportes regulatorios; auditor y revisor fiscal sólo leen;
  - cada cooperativa ajusta los roles.
- **Los procesos diarios** corren todos los días calendario.
- **Los documentos** se generan en PDF con la identidad de la cooperativa. Los adjuntos van al almacén de
  adjuntos de la plataforma.
- **Las integraciones externas** entran por adaptador con canal simulado, como la DIAN en la 012:
  pagaré desmaterializado, centrales, listas restrictivas, SMS y WhatsApp. Pasar a canal real es
  configuración más contrato, no código nuevo en Cartera.
- **Listas restrictivas**: mientras no haya proveedor, el oficial de cumplimiento mantiene la carga de
  listas con vigencia que incluye esta feature, y el adaptador la consulta. Cartera no guarda listas
  propias aparte de esa carga.
- **Valores legales citados**: los porcentajes, días, plazos y tablas que aparecen en esta
  especificación son los de la norma consultada el 2026-10-04. Se siembran como «pendiente de validar»
  y mandan los vigentes que confirmen la contadora y el oficial de cumplimiento.

### Decisiones por defecto (para revisión del dueño)

**Alcance y actores**
- **D1 · Sin portal del asociado.** El asociado consulta por documentos impresos o enviados por correo.
  Un portal o aplicación propia es otra feature, junto con el antivirus para archivos que suban externos.
- **D15 · El contrato de obligaciones lo define esta especificación** y Recaudo lo adopta. La solicitud
  decía lo contrario, pero Recaudo no existe.
- **D18 · Vivienda y UVR** en la entrega K6b, con la Ley 546. Hasta entonces, la modalidad vivienda no se
  puede originar ni migrar.
- **D21 · Verificación de documentos** sólo por usuarios de la cooperativa. La pública y anónima exige
  una excepción constitucional.

**Créditos y fechas**
- **D2 · Condiciones selladas, norma vigente.**
  - El crédito sella tasa, sistema, plazo, seguros, prelación, convención de días, redondeo y la
    preferencia de imputación.
  - Calificación, deterioro, suspensión y usura usan siempre la norma vigente de cada fecha.
- **D4 · Fecha valor anterior**: hasta cinco días por defecto. Antes de un cierre mensual exige
  atribución, y su fecha contable es siempre la del período abierto.
- **D23 · Plazo de firmeza** para liberar pagaré y garantías tras una cancelación: diez días hábiles por
  defecto, configurable por clase de medio (FR-082). El cruce interno queda en firme al aplicarse.

**Pagos y saldos**
- **D5 · Excedentes**: vuelven al recaudador. Cartera sólo conserva anticipos y saldos a favor, como
  pasivos con sus hechos.
- **D6 · Pago anticipado de cuotas futuras**: anticipo que cubre cada cuota en su vencimiento, con aviso
  de que no reduce intereses y la alternativa de abonar a capital.

**Comercial**
- **D3 · Número del crédito en la factura**: por la operación de reserva, con la enmienda E-02 de la 012.
- **D9 · Venta con evaluación negativa**: la obligación se crea marcada «validación fallida», no se
  rechaza.
- **D16 · Cuenta por cobrar tras activar**: «Cartera» por defecto, fijado por el administrador de
  Comercial.
- **D20 · IVA sobre el interés de financiación** de ventas a crédito: no hay valor por defecto. Una línea
  de origen comercial con interés no se activa sin el concepto de la contadora. Si el interés es
  gravado, lo factura Comercial.

**Contabilidad y cierres**
- **D7 · Sin respuesta de Contabilidad** en la validación previa: advierte y permite.
- **D8 · Causación diaria** en modo por lotes, con comprobante resumido por día que conserva las
  dimensiones. Desembolsos, pagos y reversos, en línea.
- **D12 · Reabrir un cierre mensual**: sólo el último, y sólo antes del cierre contable de ese período.

**Migración**
- **D10 · Retiro de lo heredado** sin migrar datos, como en la 009 y la 012, y sólo de las tablas de
  crédito.
- **D17 · Activar con diferencias de cuadre** sólo si se aceptan con motivo y acta. Es más laxo que el
  «cuadre antes de activar» literal de la solicitud.

**Nómina, libranza y cobranza**
- **D11 · Nómina conserva el «todo o nada»** de la definitiva, por el contrato síncrono (I-4).
- **D13 · Capacidad de descuento por libranza**: el tope legal sobre el salario o la pensión después de
  los descuentos de ley, como piso.
- **D14 · Aviso previo**: 20 días calendario desde el envío, por episodio y obligación, como piso legal.

**Aportes**
- **D19 · Aportes y ahorro permanente**: mientras Aportes y Ahorros no ofrezcan su consulta:
  - el cupo usa el saldo de aportes cargado por plantilla, con fecha y alerta;
  - el deterioro **no** descuenta aportes;
  - el arrastre **no** aplica su excepción.

  Es la opción más conservadora.

**Interpretaciones de la constitución**
- **D22 · I-1 a I-6**: se aplican mientras el dueño no diga otra cosa.

**Pagos sin interacción y criterios del dueño**
- **D24 · Abono a capital sin interacción.**
  - Se aplica la preferencia registrada por el deudor, presentada como instrucción permanente y
    revocable, y cada aplicación se le informa con la opción de cambiarla.
  - Sin preferencia, el valor queda como saldo a favor hasta que el deudor elija.
  - Es una lectura de la Ley 2032, que obliga a preguntar en cada pago. Debe validarla el abogado o la
    contadora.
- **D25 · Reproducibilidad «con la misma información registrada».** El criterio del dueño, «da siempre
  el mismo resultado», se cumple para una misma fecha y un mismo instante de información (FR-011). Un
  pago registrado después con fecha valor anterior cambia, como debe, la liquidación hecha con la
  información nueva.
- **D26 · Número en la factura sólo cuando hubo reserva.** Si Cartera no respondió al reservar, la
  factura sale sin número, y éste se ve en la venta en cuanto llega «obligación creada».

**Integración, modelos y servicios**
- **D27 · La orden de giro y su resultado los define esta especificación**, y Tesorería los adopta, como
  D15 con Recaudo.
- **D28 · Obligación sobrevenida sin K6a.** Aplica sólo a vigencias del perfil, o de la regla de
  obligación, registradas después de registrar la activación (no de la fecha de activación). Si una cooperativa activa queda obligada a la
  pérdida esperada antes de que exista K6a:
  - sigue cerrando con el Anexo 1, con alerta;
  - lleva la marca «modelo obligatorio no disponible» en el cierre y en el reporte de cambios, y como
    advertencia al generar el SICSES, nunca dentro del archivo.

  Es lo único posible sin el modelo; el dueño decide si en ese caso se adelanta K6a.
- **D29 · Servicios a asociados** (decidido por el dueño el 2026-10-05, Q3).
  - Se facturan en Comercial y normalmente se pagan de contado.
  - Vendidos a crédito, quedan como saldo por cobrar en Contabilidad, registrado desde Comercial, y no
    llegan a Cartera.
  - Lo que los asociados deban por servicios al corte no entra a Cartera: queda en los saldos de
    apertura de Contabilidad.

## Dependencias

- **012 · Comercial**: mensajes de venta y ajuste, crédito provisional, consulta de cupo, versión 2 del
  mensaje y consumidor de respuestas. Las tareas de su entrega IC se cierran aquí (E-02).
- **009 · Contabilidad**:
  - modo de paso, matriz, validación previa y conciliación;
  - consultas de saldos, estado del período, indicadores de la entidad y cuentas bancarias;
  - saldos de apertura para el cuadre de la migración (E-01).
- **010 · Nómina**: la definitiva y la nómina ordinaria pasan al contrato; los festivos pasan a la
  plataforma (E-03).
- **008 · Personas**: diálogo único de alta, buscador de personas y consulta de obligaciones (E-04).
- **011 · Adjuntos**: documentos de solicitudes, garantías y certificaciones.
- **Plataforma (E-05)**: bandeja de mensajes con emisor Cartera, auditoría encadenada, motor de
  aprobaciones con límites de monto, parámetros con ámbito de línea, programador de tareas por
  cooperativa, motor de archivos planos, notificaciones por SMS y WhatsApp, y valores nacionales.
- **Recaudo** (sin especificar):
  - adopta el contrato de obligaciones (FR-083) y procesa las pagadurías (FR-113 a FR-115);
  - recibe «pago reversado por Cartera» (FR-080);
  - conserva los pagos que el contrato rechazó con «Cartera no activada» entre el corte y la activación,
    y los aplica al activar (FR-142);
  - hereda para su pantalla de caja los tres pasos y los dos minutos de FR-013, SC-003 y SC-009.

  **Cartera no sale en vivo sin ella** (FR-089). Hay que especificarla.
- **Aportes y Ahorros** (fuera de alcance): deben ofrecer la consulta del saldo de aportes y de ahorro
  permanente de un asociado, y Aportes recaudar por el contrato al cruzar aportes en un retiro. Mientras
  no exista, aplica D19.
  - **Riesgo para COOFLOPAL**: su cartera incluye cuotas de aportes. Su cobro al salir en vivo depende de
    Aportes y de Recaudo, no de esta feature.
- **Tesorería** (sin módulo para Cartera): adopta la orden de giro (D27), y la recibe y confirma su
  resultado (FR-045, FR-048). **Cartera no sale en vivo sin ella** (FR-089). Hay que especificarla, o
  especificar su parte junto con Recaudo.
- **Comercial · servicios a asociados** (Q3, D29):
  - la 012 ya factura electrónicamente un servicio como ítem del catálogo;
  - para venderlo a crédito sin pasar por Cartera, Comercial necesita un medio de pago a crédito cuya
    cuenta por cobrar quede en Contabilidad, sin destino Cartera. Si la 012 no lo ofrece así, es un
    ajuste de Comercial, fuera de esta feature;
  - si COOFLOPAL quisiera conservar los pagos por adelantado de servicios, también sería un asunto de
    Comercial (con Recaudo para el dinero), fuera de esta feature.
- **Riesgo para el calendario de COOFLOPAL**: su salida con Cartera depende de dos features que todavía
  no tienen especificación (Recaudo y Tesorería).
- **El dueño, la contadora y el oficial de cumplimiento** validan los parámetros sembrados como
  «pendiente de validar»: usura, rangos, porcentajes, días, horarios, capacidad de descuento, límites,
  tablas del Anexo 2 y layouts. La contadora da además su concepto sobre NIIF (FR-017) e IVA (D20).
- **El dueño** aporta el layout real de cada pagaduría y central y coteja el formato del SICSES.
  COOFLOPAL no recibe ahorros: su perfil se registra así al implantarla, y no necesita K6a para salir
  (FR-105).
