# Feature Specification: Recaudo y Tesorería — caja, medios de pago, bancos, pagadurías y giros

**Feature Branch**: `015-recaudo-tesoreria`

**Created**: 2026-10-22

**Status**: Draft — sin clarificar. Quedan **tres preguntas al dueño** que cambian el alcance (marcadas en
FR-021, FR-091 y FR-161 y resumidas en Clarifications); todo lo demás se decidió con defectos razonables que
el dueño puede cambiar (D1 a D32, en Assumptions). No implementa código.

**Input**: User description: «Recaudo y Tesorería (feature 015) para IngenIA365ERP, ERP SaaS multi-cooperativa
del sector solidario colombiano. Dos módulos en una sola feature con dos entregas, porque comparten caja, medios
de pago y cuentas bancarias.
RECAUDO es el módulo central de la plata que ENTRA: el dueño de la caja, los medios de pago y el recibo de caja.
Se comunica con todos los módulos por contratos ya definidos, sin conocer sus reglas internas: Cartera de
créditos (contrato de obligaciones de la 013: listar, simular, aplicar, reversar, resolver saldos a favor y
anticipos, con modalidades de pago total, cuota del ciclo, anticipada y cuota específica, y la pregunta de
imputación del prepago de la Ley 2032), Captaciones y aportes (contrato de cuentas de la 014: consignar,
retirar, trasladar, reversar, exigibles de aportes y cuotas), Comercial/Inventario de la 012 (ventas a crédito y
cartera comercial, y su caja POS con medios de pago dinámicos y cierre por medio, que se reutiliza o se
integra) y Nómina (descuentos de nómina propia ya implementados). Cada pago genera un recibo de caja único,
numerado e inmutable, del que cuelgan las aplicaciones de cada módulo; un recibo puede pagar varias obligaciones
y productos de una persona a la vez (créditos, aportes, ahorro, ventas). Debe cubrir: ventanilla/caja por punto
y por cajero con apertura, arqueo, cuadre y cierre por medio de pago y diferencias aprobadas; medios de pago
dinámicos y configurables (efectivo, cheque con canje, tarjetas débito y crédito, transferencia, consignación
bancaria, PSE/pasarela, bonos) que van solos a las cuentas contables; consignaciones y transferencias recibidas
en bancos con su identificación y conciliación (archivos de extracto o de recaudo bancario, referencias); recaudo
por pagadurías y libranza (recibir de Cartera el archivo de descuentos por pagaduría, confirmar el envío,
procesar la respuesta de descuentos, aplicar giros de la pagaduría y no aplicados); débito automático desde
cuentas de ahorro; recaudos por canales externos (bancos, corresponsales, convenios de recaudo con referencia);
pagos sin identificar en partida pendiente hasta identificarlos; excedentes y saldos a favor que devuelve
Cartera; reversos de pagos y pagos reversados por Cartera; chequeras y canje; recibos anulados con motivo y
auditoría; reportes de recaudo y de caja.
TESORERÍA es el módulo de la plata que SALE y de los bancos: cuentas bancarias propias (vinculadas a las cuentas
contables bancarias del plan), saldos y flujo de caja; ejecución de las órdenes de giro de Cartera y Captaciones
(desembolsos, devoluciones de saldos a favor, excedentes de asamblea, entregas por fallecimiento, CDAT y
retiros) por archivo bancario plano (reusando el motor de formatos bancarios de Core de la 010), cheque o
efectivo, con su confirmación o rechazo (resultado del giro, regiro), pagos a proveedores de Comercial y de
Contabilidad, traslados entre cuentas propias, conciliación bancaria con el extracto, chequera y cheques
girados, y aprobaciones multinivel por monto. El módulo de Tesorería heredado (cheques, facturas de tesorería,
conceptos y flujo de caja traídos del sistema anterior) debe evaluarse para reemplazarse o retirarse.
Reglas del proyecto que no se negocian: los módulos no escriben asientos (emiten hechos a Contabilidad por la
matriz contable, como Cartera e Inventario); bandeja de mensajes y registro de participantes de la plataforma
(REC y TES reemplazan a los dobles de prueba TST y TES); idempotencia en todo pago, giro y mensaje; segregación
de funciones y aprobaciones multinivel con montos por permiso; alcance por oficina, punto y caja; auditoría con
cadena de sellos; todo parametrizable por cooperativa y los plazos regulatorios se informan sin imponerse;
SARLAFT (reportes de efectivo y operaciones sospechosas por umbrales configurables); GMF donde aplique; sin
valores legales escritos en el código; una base de datos por cooperativa; PostgreSQL y SQL Server; pantallas
Web y MAUI con indicador de carga. Contexto de negocio: Cartera y Captaciones ya están en producción pero
ninguna cooperativa puede activarlas sin un recaudador y una tesorería reales; COOFLOPAL sale en vivo el
01/12/2026 y depende de esta feature. Fuera de alcance: facturación electrónica (ya existe en Comercial), la
contabilidad general y la nómina en sí.»

Ninguna pantalla, informe, plantilla ni documento de esta feature nombra al sistema anterior: se habla de «lo
heredado» y de «cifras de referencia».

## Contexto

Estado verificado el 2026-10-22 en la rama `develop` (features 009 a 014 en producción), en las
especificaciones y contratos de la 010, 012, 013 y 014, y en el código heredado de Tesorería.

### Por qué ahora

- Cartera (013) y Captaciones y aportes (014) están desplegadas en producción **sin activar** en ninguna
  cooperativa. Su activación en producción exige, en el registro de participantes de cada cooperativa, un
  **recaudador real** que haya adoptado el contrato de obligaciones (y el de cuentas, para captaciones y
  aportes) y un **destino de giros real** que haya adoptado la orden de giro y su anulación. Los dobles de
  prueba no cuentan en producción.
- COOFLOPAL sale en vivo el **01/12/2026** con créditos y aportes (decisiones del dueño en la 013 y la 014: sin
  ventanilla provisional, sin recaudo ni giros provisionales). Sin esta feature no sale ninguna línea.

### Lo que ya existe y se reutiliza

- **Plataforma de la 012**: bandeja de mensajes con entrega garantizada y un consumidor por destino; registro
  de participantes por cooperativa con adopción de versiones de contrato y de mensaje; idempotencia por clave;
  parámetros con vigencia; aprobaciones multinivel con montos por permiso y aprobador presente con segundo
  factor; alertas; alcance; plantillas de importación con revisión previa; auditoría con cadena de sellos.
- **Catálogo de medios de pago de la plataforma** (creado por la 012): medios configurables de clase fija
  (efectivo, tarjetas de crédito y de débito, crédito a asociado, crédito comercial, consignación,
  transferencia, bono, cheque, otro), con red, adquirente y datáfonos, banco y cuenta de destino como dato,
  reglas de captura, método y tolerancia de arqueo, y denominaciones de efectivo.
- **La caja del punto de venta de la 012**: puntos de venta, cajas, sesiones con base, movimientos de caja,
  arqueo por medio, diferencias aprobadas y cierre del día. Es de Comercial y cobra **ventas**; la 013 y la
  014 decidieron no ampliarla para recaudar créditos ni captaciones.
- **Motor de archivos planos de la 010** (formatos bancarios versionados por banco, ámbito y vigencia, que
  escribe y, desde la 013, lee archivos), con el código de banco para transferencias electrónicas.
- **Contratos que Cartera y Captaciones ya definieron para Recaudo y Tesorería** (abajo).
- **Contabilidad (009)**: cuentas bancarias del plan, matriz por emisor, validación previa, modo de paso,
  conciliación de puentes, guarda de cierre, consulta de saldos pendientes por tercero y documento cruce.

### Lo que Cartera y Captaciones ya exigen a Recaudo y Tesorería

Resumen de los contratos vigentes (contrato de obligaciones e interfaz de órdenes de giro de la 013, catálogo de
mensajes de la 013, contrato de cuentas y mensajes de la 014). Esta feature los **adopta sin cambiarlos**; lo que
necesita distinto va como enmienda (FR-164 a FR-169).

1. **Recaudo es el recaudador de clase «Recaudo»**: caja, consignaciones, cheques, débito y respuestas de
   pagadurías. Se registra como recaudador de obligaciones y de cuentas, y como destino de los mensajes de
   recaudo. Toda clave que use empieza por su código de módulo.
2. **Las aplicaciones ocurren dentro de la transacción de Recaudo**: si Recaudo no confirma su recibo, ningún
   pago ni consignación queda hecho; un fallo técnico se reintenta entero.
3. **Dos formas**: interactiva (caja: el deudor ve una simulación y se aplica con ella; si la distribución
   cambió, se muestra la nueva y se vuelve a confirmar) y no interactiva (bancos, pagadurías, archivos).
4. **Modalidades de pago de Cartera**: cancelación total, vencido, cuota del ciclo, anticipada, abono
   extraordinario a capital con la elección del deudor entre reducir plazo o cuota (Ley 2032; sin elección en
   forma no interactiva rige la preferencia registrada del deudor), cuota específica y valor libre.
5. **Clases de medio admitidas**: efectivo, tarjetas, consignación, transferencia, bono, cheque, otro y
   descuento de pagaduría externa; las clases de crédito se rechazan. La **firmeza** de una cancelación depende
   de la clase del medio (diez días hábiles por defecto; inmediata en los cruces internos).
6. **Fecha valor**: nunca futura en caja; hasta cinco días hacia atrás por defecto (parámetro de Cartera); un
   mes ya cerrado exige permiso con monto. La fecha contable la fija el período abierto.
7. **Lo que vuelve a Recaudo**: el **excedente** de un pago (lo que supera lo exigible) nunca entra a Cartera;
   un saldo a favor o un anticipo se **devuelve por el recaudador** o se aplica a otra obligación del mismo
   deudor.
8. **Reversos**: Recaudo reversa sólo lo suyo, por la clave con que aplicó; cuando Cartera o Captaciones
   reversan desde su pantalla un pago o una consignación de Recaudo (p. ej. cheque devuelto), le avisan para
   que anule su recibo. Un cheque devuelto ya usado en una cuenta de ahorro se reversa igual y deja un
   sobregiro técnico con bloqueo.
9. **Retiros**: un retiro de ahorro en efectivo o cheque lo **autoriza Captaciones** (firmas, disponible,
   montos, atribución, GMF) y lo **entrega Recaudo**; la cancelación con entrega espera a Recaudo.
10. **Efectivo y origen de fondos**: una consignación en efectivo sobre el umbral exige la declaración de
    origen de fondos; sobre la atribución del usuario exige un aprobador presente.
11. **Canje**: un medio con días de disponibilidad (cheque, tres días hábiles por defecto) deja el valor por
    liberar; Captaciones lo libera sola y Recaudo puede reversarlo antes.
12. **Antes de activar**: los contratos responden «no habilitado» sin efectos; Recaudo **conserva** esos pagos
    y los aplica al activar con su fecha valor.
13. **Pagadurías (libranza)**: Cartera envía a Recaudo el archivo de descuentos por pagaduría (con créditos y,
    desde la 014, cuotas de aportes y ahorros por grupos de archivo y conceptos equivalentes); Recaudo confirma
    el envío, procesa la respuesta de la pagaduría con el formato de vuelta del convenio, devuelve el resultado
    y, cuando la pagaduría gira, aplica cada línea descontada por los dos contratos citando la línea.
14. **Órdenes de giro**: Cartera (desembolsos) y Captaciones (retiros por transferencia, devoluciones del
    retiro del asociado, devoluciones de aportes, entregas por fallecimiento, intereses y redención de CDAT,
    vencimiento del contractual, no reclamados, consignaciones a juzgados, excedentes pagados) emiten órdenes
    con beneficiario, cuenta destino, valor neto, fecha y forma (transferencia, cheque o pago a tercero), y su
    anulación. Tesorería responde **un resultado por orden**: girada, rechazada, cheque no cobrado o anulada.
    Rechazada, anulada o no cobrada, el origen reversa o deja la orden «pendiente de regiro» y emite una
    orden nueva enlazada (regiro). Una orden que sale en un **archivo de desembolsos de la propia Cartera** no
    se paga por otra vía: Tesorería sólo informa su resultado.

### Lo heredado de Tesorería

Un traslado tabla por tabla del sistema anterior, sin modelo de negocio:
- **Tres tablas**: cheques, conceptos de tesorería y facturas de tesorería (cuentas por pagar y por cobrar).
  Estados de una letra, número de cheque digitado a mano, banco como texto en las facturas, la persona por
  identificador interno.
- **Cuatro pantallas** (cheques, conceptos, facturas y flujo de caja) y **tres grupos de rutas** (unas diez
  rutas) que sólo exigen sesión, sin permisos propios ni alcance.
- **No contabiliza**: cobrar, anular o devolver un cheque y registrar una factura sólo cambian un estado; el
  código lleva la marca de que la contabilización quedó pendiente desde la feature 009. El flujo de caja suma
  facturas por mes, no movimientos reales.
- **No se integra** con Cartera, Captaciones, Comercial ni Nómina, ni con las órdenes de giro.

Aparte existe otro traslado heredado de **tarjeta débito y convenios con redes** (siete tablas de tarjetas,
transacciones, datáfonos y parámetros de convenio). No lo pide esta feature; queda como pregunta abierta (P9).

### La norma

Resumen; los valores se siembran con su fuente y «pendiente de validar» (FR-009).
- **SARLAFT** (Superintendencia de la Economía Solidaria, Circular Básica Jurídica, título de prevención del
  lavado de activos): conocimiento del asociado y del origen de los fondos, control de transacciones en
  efectivo, reporte periódico de transacciones en efectivo individuales y múltiples a la UIAF con los umbrales
  vigentes (de referencia, desde 10 millones de pesos en una operación y desde 50 millones acumulados en el
  mes por persona, a validar por el oficial de cumplimiento), reporte de operaciones sospechosas que decide el
  oficial. La gestión SARLAFT completa es de su propia feature; ésta deja los datos, las alertas y el reporte de
  efectivo.
- **GMF** (Estatuto Tributario, art. 871 y siguientes): cuatro por mil sobre las disposiciones de recursos de
  cuentas, con exenciones taxativas (art. 879); el banco lo cobra sobre las cuentas propias de la cooperativa;
  la cooperativa vigilada lo practica como agente sobre los retiros de sus captaciones (eso ya lo hace
  Captaciones). El Gobierno estudia modificarlo: tarifa, exenciones y hechos generadores son configurables con
  vigencia, incluida la tarifa cero.
- **Cheques** (Código de Comercio): presentación para el pago, canje por la cámara de compensación, causales
  de devolución y caducidad de presentación.
- **Libranza** (Ley 1527 de 2012 y sus modificaciones): el empleador o pagaduría gira lo descontado en el plazo
  legal; los efectos sobre la mora del deudor ya los resuelve Cartera.
- **Prepago** (Ley 2032 de 2020): el deudor elige si el abono reduce plazo o cuota; lo pregunta Recaudo y lo
  aplica Cartera.

### El calendario

- **R1 y T1 son obligatorias para la salida de COOFLOPAL el 01/12/2026** (activación de Cartera y de Aportes
  en producción).
- R2 y T2 no llevan fecha; conviene tener la conciliación bancaria (T2) antes del primer cierre mensual en vivo
  (diciembre de 2026).
- Esta especificación no fija fechas de construcción; el plan las propone.

## Clarifications

### Session 2026-10-22

- Q: C1 · ¿La caja de Recaudo y la caja del punto de venta de la 012 son una sola o dos? → A: **A — dos cajas
  separadas que comparten catálogo de medios, denominaciones y método de arqueo** (FR-021).
- Q: C2 · ¿Qué canales en línea funcionan el primer día? → A: **A — sólo por archivos del banco o la pasarela
  cargados por una persona**; sin botón de pago en línea en esta feature (FR-091).
- Q: C3 · ¿Qué se hace con el módulo de Tesorería heredado? → A: **A — retirarlo entero con la guarda destructiva**
  (FR-161).
- Q: ¿Quién recauda las transferencias recibidas en cuentas de captación, que la 014 había asignado a Tesorería
  (decisión del 2026-10-18)? → A: **Recaudo, como todo ingreso de una persona** (D2); Tesorería no se registra como
  recaudadora. Enmienda la decisión de la 014 sin cambiar ningún contrato.

### Opciones que se presentaron (2026-10-22)

Las opciones con que se presentaron las tres decisiones; el dueño eligió la recomendada en las tres (arriba).

- **C1 · La caja de Recaudo y la caja del punto de venta (FR-021).** Opciones: (A) dos cajas separadas que
  comparten el catálogo de medios, las denominaciones y el método de arqueo (**recomendada**: no toca la 012 en
  producción); (B) una sola sesión de caja, de Recaudo, que el punto de venta usa para cobrar sus ventas;
  (C) separadas, con una «caja mixta» opcional por punto que recauda y vende en la misma sesión.
- **C2 · Canales en línea del primer día (FR-091).** Opciones: (A) sólo por archivos —extracto, archivo de
  recaudo del convenio, reporte de la pasarela— cargados por una persona (**recomendada** para R1 y R2);
  (B) además, botón de pago en línea con una pasarela (PSE) para que el asociado pague desde un enlace; (C) A
  en R1 y B en una entrega posterior con la pasarela que el dueño elija.
- **C3 · El módulo de Tesorería heredado (FR-161).** Opciones: (A) retirarlo entero con la guarda destructiva
  y reemplazarlo por esta feature (**recomendada**); (B) conservar sus datos como consulta de sólo lectura y
  retirar rutas y pantallas; (C) migrar sus cheques pendientes y facturas por pagar a la puesta en marcha y
  después retirarlo.

## Conflictos con la constitución y con decisiones vigentes

La especificación no modifica la constitución.

### Con la constitución

- **I-1 · Principio XI.** Se lee extendido a recibos, egresos, sesiones cerradas, archivos de giro y
  conciliaciones cerradas: no se editan ni se borran; se corrigen con un documento nuevo enlazado.
- **I-2 · Principio V.** Pagadores, beneficiarios, girados de cheques, pagadurías, bancos-tercero y cajeros
  son personas del maestro. Una consignación sin identificar no tiene persona hasta que se identifica.
- **I-3 · Principio X.** Toda operación monetaria (recibo, egreso, giro, traslado, anulación) queda en la
  auditoría con cadena de sellos; la declaración de origen de fondos y las alertas de efectivo son datos de la
  operación.

| Principio | Cómo lo cumple |
|---|---|
| I. Spec-first | Esta especificación antes de plan, tareas y código |
| II. Clean Architecture | Reglas de arqueo, identificación, conciliación y armado de lotes como cálculos puros; los demás módulos sólo por sus contratos (FR-003) |
| III. CQRS | Toda operación es comando o consulta con su validador |
| IV. Base por cooperativa | Puntos, cajas, cuentas bancarias, recibos y giros viven en la base de cada cooperativa; procesos por cooperativa (FR-019) |
| V. Persona maestra | I-2 |
| VI. PublicId | Recibos, egresos, órdenes, archivos y partidas por identificador público y número visible (FR-017) |
| VII. Borrado lógico | Catálogos se inactivan; los documentos nunca se borran (FR-002) |
| VIII. Validación dual | Mensajes en español con código por espacio (FR-015) |
| IX. Errores visibles | Rechazos de contratos por ítem, fallas de consumo en la bandeja, alertas de proceso (FR-015) |
| X. SARLAFT | I-3, sección J |
| XI. Inmutabilidad | I-1, FR-002 |
| XII. Migraciones | Retiro de lo heredado con diagnóstico, respaldo y segundo revisor (FR-161) |

### Con decisiones vigentes de otras features

- **C1 · 014, Tesorería como recaudadora.** La 014 previó (y construyó con el doble de prueba) que Tesorería
  consigne en cuentas de captación las transferencias recibidas, con su propia clase de recaudador y su prefijo
  de clave, y que adopte también el contrato de obligaciones. El dueño pide ahora que las consignaciones y
  transferencias recibidas sean de **Recaudo**. **Propuesta (D2)**: todo dinero que entra de una persona entra
  por Recaudo, también el identificado en el extracto; Tesorería no se registra como recaudadora y esa clase
  queda disponible sin uso. No exige cambiar ningún contrato.
- **C2 · 013, archivo de desembolsos de Cartera.** Cartera puede generar su propio archivo de transferencias
  de desembolsos y marcar sus órdenes como «pagadas por archivo». Habría dos lugares que generan archivos de
  pago. **Propuesta (D16)**: convivencia —Tesorería no paga esas órdenes, sólo informa su resultado y concilia el
  archivo— y enmienda futura para que todo archivo de pagos salga de Tesorería (FR-164).
- **C3 · 014, débito automático.** El débito automático de cuotas de crédito desde una cuenta de ahorro ya es
  una **operación interna** de Cartera que corre en su ciclo diario. Recaudo no lo duplica: lo muestra en el
  recaudo consolidado (D26).
- **C4 · 012, caja del punto de venta.** Ver C1 de Clarifications (FR-021).
- **C5 · 014, «chequeras y canje son de Recaudo».** La 014 llamó así a lo heredado de ahorros. Aquí se separa:
  el **canje** de los cheques recibidos es de Recaudo; las **chequeras** de las cuentas bancarias propias y los
  cheques girados son de Tesorería. No hay chequeras de asociados (sin cuenta corriente, D4 de la 014).
- **C6 · 013, servicios a asociados.** Lo vendido a crédito sin pasar a Cartera queda como cuenta por cobrar en
  Contabilidad (D29 de la 013) y nadie ofrece hoy cómo recaudarlo. Se resuelve con una consulta de pendientes
  y un hecho con tercero y documento cruce (FR-049, enmiendas FR-166 y FR-167).
- **C7 · 013 y 014, dobles de prueba.** El doble de tesorería usa el código «TES» como destino y como
  recaudador, el mismo que tomaría la Tesorería real; el registro no admite dos filas iguales. En los ambientes
  de prueba hay que retirar la fila del doble antes de registrar la real (FR-148).
- **C8 · 013 y 014, ventana de fecha valor.** Una partida bancaria identificada semanas después no cabe en los
  cinco días de fecha valor del contrato; aplicarla con la fecha de identificación castiga al deudor con mora que
  no tuvo. Se pide una excepción como la que ya tienen Nómina y la pagaduría con referencia (enmiendas FR-164 y
  FR-165).
- **C9 · 014, umbral de efectivo.** Captaciones exige la declaración de origen de fondos por operación; el
  dueño pide que Recaudo controle el efectivo con umbrales. Un solo parámetro de umbral, evaluado por Recaudo por
  recibo y por persona en el período, y la declaración viaja a cada aplicación que la exija (FR-094, FR-165).
- **C10 · 014, orden de giro sin forma «efectivo».** El dueño pide girar «por archivo, cheque o efectivo»; la
  orden de giro sólo admite transferencia, cheque o pago a tercero. **Propuesta (D20)**: el efectivo sale por la
  caja con el contrato (retiro, devolución de saldo a favor); una orden en efectivo exigiría una versión nueva de
  la orden.
- **C11 · 012, conciliación con adquirentes.** La 012 dejó la conciliación de lo que abonan las redes de
  tarjetas (comisiones y retenciones) para Tesorería; se recoge en FR-133.

## Alcance

### Dentro de esta feature

**Recaudo**
- Puntos de recaudo por oficina, cajas, cajeros y bóveda de oficina.
- Sesiones de caja con base, movimientos de caja, arqueo y cierre por medio de pago, diferencias aprobadas y
  cierre del día por punto.
- El **recibo de caja** único, numerado e inmutable, **multiproducto** (créditos, cuotas de aportes y ahorros,
  consignaciones a cuentas, ventas a crédito, cuentas por cobrar comerciales, conceptos varios) con cualquier
  combinación de medios.
- Egresos de caja: retiros de ahorro autorizados por Captaciones, devolución de saldos a favor y excedentes.
- Anulación de recibos y egresos, reversos y avisos de reverso de los módulos, cheque devuelto.
- Consignaciones y transferencias recibidas: extracto y archivos de recaudo, identificación por referencia,
  partidas por identificar.
- Pagadurías y libranza: archivo de descuentos, confirmación de envío, respuesta, giro, no aplicados.
- Cheques recibidos y canje.
- Convenios de recaudo con referencia (bancos y corresponsales).
- Control de efectivo y SARLAFT: umbrales, origen de fondos, fraccionamiento, alertas y reporte de
  transacciones en efectivo.

**Tesorería**
- Cuentas bancarias propias vinculadas a su cuenta contable, chequeras, saldos y bóvedas.
- Bandeja única de órdenes de pago (Cartera, Captaciones, Recaudo, Comercial, Contabilidad y manuales).
- Lotes y archivos de pago con el motor de formatos bancarios, cheques, aprobación multinivel por monto.
- Resultado del giro (girada, rechazada, cheque no cobrado, anulada) y cadena de regiros.
- Pagos a proveedores y de cuentas por pagar contables; traslados entre cuentas propias y remesas de efectivo.
- Conciliación bancaria con el extracto, notas bancarias (GMF, comisiones, intereses) y conciliación de
  adquirentes y convenios.
- Posición de liquidez y flujo de caja proyectado.

**Común**
- Hechos a Contabilidad por matriz (dos emisores nuevos), conciliación con Contabilidad.
- Registro como participantes, adopción de contratos y mensajes, y requisitos de activación de Cartera y
  Captaciones.
- Permisos, alcance, segregación, aprobaciones, auditoría, informes y documentos.
- Puesta en marcha con plantillas y cuadre; retiro de lo heredado de Tesorería.
- Enmiendas a la 009, 010, 012, 013 y 014.

### Fuera de esta feature

- **Facturación electrónica**: es de Comercial (la factura de un cargo o un servicio la emite Comercial).
- **Contabilidad general** y **nómina en sí**; la dispersión de la nómina sigue en Nómina (010).
- **La gestión SARLAFT** (segmentación, monitoreo integral, reporte de operaciones sospechosas, listas
  propias): su feature. Ésta deja datos, alertas y el reporte de transacciones en efectivo.
- **Caja menor** (fondo fijo de gastos de oficina con reembolso), **inversiones** y **fondo de liquidez como
  portafolio**: Tesorería sólo marca las cuentas bancarias que lo constituyen (FR-139).
- **Moneda extranjera**, cuentas en el exterior y diferencia en cambio.
- **Caja sin conexión**, cajeros automáticos, tarjeta débito propia y banca electrónica para el asociado
  (portal o aplicación del asociado).
- **Integraciones en línea con bancos** (servicios web de pagos o saldos): todo es por archivos salvo lo que
  decida C2.
- **Débito automático**: ya es de Cartera (C3).
- **Crédito con tarjeta** emitida por la cooperativa y cupos rotativos: son de Cartera.

### Entregas sugeridas

| Entrega | Contenido | Historias |
|---|---|---|
| **R1 · Recaudo para activar** | Puntos, cajas, medios, sesión con arqueo y cierre, recibo multiproducto por los contratos, egresos de caja, anulaciones y reversos, consignaciones identificadas y partidas por identificar, pagadurías y libranza, control de efectivo y reporte, hechos y matriz de Recaudo, registro como recaudador y destino, seguridad, puesta en marcha de cajas | US1 a US9, US12 (Recaudo), US13 (Recaudo), US14 |
| **T1 · Tesorería de giros** | Cuentas bancarias propias, bandeja de órdenes de Cartera y Captaciones, lotes y archivos, cheques, aprobación por monto, resultado y regiro, hechos y matriz de Tesorería, registro como destino de giros, puesta en marcha de bancos, retiro de lo heredado | US8 (Tesorería), US9 (Tesorería), US10, US11, US12, US13 |
| **R2 · Recaudo ampliado** | Cheques y canje completos, convenios de recaudo con referencia y corresponsales, canal en línea si C2 lo decide, cuentas por cobrar comerciales, informes completos | US15, US16, US21 (Recaudo), US23 |
| **T2 · Tesorería ampliada** | Conciliación bancaria, notas bancarias y GMF, pagos a proveedores y de cuentas por pagar, traslados, chequeras completas, conciliación de adquirentes, liquidez y flujo de caja | US17 a US20, US21 (Tesorería), US22 |

Reglas de convivencia:
- **R1 y T1 van juntas**: la activación de Cartera y Captaciones exige las dos.
- **Hasta T2**, la conciliación bancaria del mes se hace en Contabilidad como hoy; las partidas por identificar
  de R1 ya llegan del extracto.
- **Hasta R2**, un cheque recibido se acepta sólo si el parámetro lo permite y con canje manual (FR-084).

### Glosario

**Recaudo**
- **Punto de recaudo**: lugar de una oficina donde se recibe dinero (ventanilla). Tiene cajas.
- **Caja**: puesto de un cajero dentro de un punto; recauda sólo con una **sesión** abierta.
- **Sesión de caja (turno)**: apertura con base, recibos, egresos, movimientos y cierre con arqueo.
- **Bóveda**: efectivo de la oficina fuera de las cajas; recibe y abastece cajas y envía remesas al banco.
- **Recibo de caja**: documento único, numerado e inmutable de un ingreso de dinero; lleva sus **medios** y sus
  **aplicaciones** (lo que cada módulo hizo con el dinero).
- **Aplicación**: el pago, la consignación o el recaudo que un módulo registró por su contrato al confirmar
  el recibo, con su propio comprobante.
- **Egreso de caja**: documento de una salida de dinero por la caja (retiro de ahorro, devolución).
- **Canal**: caja, banco (extracto o archivo), convenio de recaudo, pagaduría, en línea.
- **Partida por identificar**: dinero recibido en un banco cuyo pagador o destino no se conoce.
- **Arqueo**: comparación, por medio de pago, de lo esperado con lo contado o comprobado.
- **Canje**: tiempo que tarda un cheque en hacerse efectivo; mientras tanto el valor no está disponible.
- **Firmeza**: fecha desde la cual una cancelación pagada con un medio no inmediato queda en firme.
- **Fecha valor, fecha contable y fecha de registro**: el día que cuenta para intereses, el período donde se
  contabiliza y el instante en que se registró.

**Tesorería**
- **Cuenta bancaria propia**: cuenta de la cooperativa en un banco, vinculada a una cuenta contable bancaria.
- **Orden de pago**: instrucción de pagar a un beneficiario que llega de un módulo (la **orden de giro** de
  Cartera y Captaciones es una de ellas) o que registra Tesorería.
- **Lote de pago**: órdenes que se pagan juntas desde una cuenta con una forma (archivo o cheques).
- **Resultado del giro**: lo que Tesorería informa al origen de cada orden.
- **Regiro**: orden nueva, enlazada a una rechazada, que el origen emite para volver a pagar.
- **Nota bancaria**: movimiento que el banco hace por su cuenta (GMF, comisión, intereses, impuestos).
- **Conciliación bancaria**: cruce del extracto con lo registrado, con partidas conciliatorias explicadas.

## Interfaces

Recaudo y Tesorería no tienen interfaces nuevas con Cartera ni con Captaciones: **adoptan** las que esas
features ya definieron. Las nuevas son con Comercial, Contabilidad y entre ellas.

**Recaudo usa**

| Interfaz | De quién | Para qué |
|---|---|---|
| Contrato de obligaciones (listar, simular, aplicar, reversar, resolver saldo a favor o anticipo), v1 | Cartera (013) | US3, US4, US5, US6, US7 |
| Contrato de cuentas (consultar, exigibles, simular, consignar, retirar, trasladar, reversar), v1 | Captaciones (014) | US3, US4, US5, US6, US7 |
| Consulta de cuentas por cobrar comerciales pendientes por persona | Contabilidad (enmienda FR-167) | US23 |
| Personas, listas restrictivas, autorizaciones de datos, perfil tributario | Plataforma | FR-016, FR-098 |
| Formatos de archivo plano (lectura y escritura) | Plataforma (010) | US6, US7, US16 |

**Recaudo recibe** (como destino de la bandeja): «pago reversado por Cartera», «consignación reversada por
Cartera» y «descuentos por pagaduría» (v1 y v2).

**Recaudo emite**: «envío de descuentos confirmado» y «resultado de descuentos» (v1 y v2) a Cartera; sus hechos a
Contabilidad; órdenes de pago a Tesorería (devoluciones); remesas a Tesorería.

**Tesorería recibe**: «orden de giro emitida» (v1 y v2) y «anulación de orden de giro» de Cartera; facturas de
proveedor por pagar de Comercial (enmienda FR-166); órdenes de pago de Recaudo.

**Tesorería emite**: «resultado del giro» al origen de cada orden; sus hechos a Contabilidad; el pago de la
factura a Comercial (enmienda FR-166).

**Tesorería usa**: consulta de cuentas por pagar contables pendientes y movimientos contables de cada cuenta
bancaria para conciliar (Contabilidad, enmienda FR-167); archivos de dispersión de Nómina para conciliar
(enmienda FR-168).

## User Scenarios & Testing *(mandatory)*

Actores: **cajero**, **supervisor de caja** (jefe de oficina), **analista de recaudo** (bancos, pagadurías,
convenios), **tesorero**, **aprobadores** (por monto), **contador**, **oficial de cumplimiento**, **auditor** y
revisor fiscal, el **asociado o tercero** que paga o recibe, las **pagadurías** y los **bancos** (por sus
archivos), y el **proceso de la cooperativa** (tareas programadas).

### User Story 1 - Configurar puntos de recaudo, cajas, cajeros y medios de pago (Priority: P1)

El jefe de operaciones configura, sin programar, los puntos de recaudo de cada oficina y sus cajas, asigna
cajeros, define la base de cada caja y elige qué medios de pago del catálogo común se ofrecen en cada punto, con
sus reglas de captura y de arqueo. Configura también los tipos de recibo y de egreso con su consecutivo y los
conceptos de ingresos y egresos varios.

**Why this priority**: sin puntos, cajas y medios no hay recibo ni arqueo; es el primer paso de la puesta en
marcha de COOFLOPAL.

**Independent Test**: crear un punto con dos cajas, ofrecer efectivo, dos tarjetas y transferencia, asignar un
cajero y comprobar que en la otra oficina el punto no aparece para un usuario sin alcance en ella.

**Acceptance Scenarios**:

1. **Given** un medio de clase «crédito a asociado», **When** se intenta ofrecer en un punto de recaudo,
   **Then** se rechaza: pagar una obligación con crédito no es un recaudo.
2. **Given** un medio de tarjeta sin adquirente ni datáfono, **When** se ofrece en un punto, **Then** se
   rechaza indicando lo que falta, como en el punto de venta.
3. **Given** una caja con sesión abierta, **When** se cambia su base o se inactiva, **Then** el cambio rige
   desde la próxima sesión y la abierta conserva lo que selló al abrir.
4. **Given** un código de punto, caja o concepto que ya existe, **When** se crea otro igual, **Then** responde
   «código duplicado» con el nombre del existente antes de guardar.

---

### User Story 2 - Abrir, arquear y cerrar la caja por medio de pago (Priority: P1)

El cajero abre su sesión contando la base por denominaciones. Durante el turno el supervisor puede abastecerlo
desde la bóveda o retirarle efectivo, y el cajero puede consignar el excedente o reclasificar un pago registrado
con el medio equivocado. Al cerrar, cuenta por medio de pago (efectivo por denominaciones, tarjetas por lote de
datáfono, transferencias y consignaciones por referencia, cheques por relación); el sistema compara con lo
esperado. Las diferencias llevan motivo y, sobre la tolerancia del medio, aprobación del supervisor. El
supervisor cierra el día del punto, que consolida las sesiones.

**Why this priority**: el cuadre diario de la caja es el control básico del dinero y lo que exige la revisoría
fiscal desde el primer día.

**Independent Test**: abrir con 500.000 de base, recaudar 1.200.000 en efectivo y 800.000 en tarjeta, retirar
1.000.000 a bóveda, cerrar contando 699.000 en efectivo y el lote completo de la tarjeta, y comprobar el
faltante de 1.000 con su aprobación, su hecho contable y el informe de cierre por medio.

**Acceptance Scenarios**:

1. **Given** una caja sin sesión abierta, **When** el cajero intenta emitir un recibo, **Then** no puede y se
   le ofrece abrir la sesión.
2. **Given** una sesión abierta en una caja, **When** otro cajero intenta abrir la misma caja, **Then** se
   rechaza indicando quién la tiene abierta.
3. **Given** el arqueo ciego activado, **When** el cajero cuenta, **Then** no ve lo esperado hasta registrar
   lo contado.
4. **Given** una diferencia de 50.000 en efectivo y una tolerancia de 5.000, **When** el cajero cierra,
   **Then** la sesión queda «cerrada con diferencia por aprobar»; el supervisor, otra persona, la aprueba con
   motivo y se emite el hecho de faltante a cargo del cajero o de gasto, según el parámetro.
5. **Given** una sesión cerrada, **When** se descubre un error, **Then** no se reabre: se corrige con un
   documento de ajuste enlazado a la sesión, con aprobación.
6. **Given** el efectivo de una caja sobre el tope configurado, **When** se emite el siguiente recibo en
   efectivo, **Then** se avisa al cajero y al supervisor que debe trasladar a bóveda; no bloquea.

---

### User Story 3 - Recibo de caja multiproducto por los contratos de Cartera y Captaciones (Priority: P1)

El cajero busca a la persona y ve en una sola pantalla lo que puede pagar: sus créditos (como deudor y, si lo
pide, como codeudor) con lo exigible hoy, la cuota del ciclo, lo vencido y el total para cancelar; sus cuotas de
aportes y de ahorro permanente o contractual pendientes; sus cuentas de ahorro para consignar; sus ventas a
crédito y cuentas por cobrar comerciales pendientes; y conceptos varios. Elige qué paga y cómo (modalidad por
crédito; cuotas o consignación libre por cuenta); el sistema simula cada aplicación con el módulo dueño y
muestra la distribución, los avisos (el anticipo no reduce intereses; el abono extraordinario reduce plazo o
cuota, a elección del deudor) y el total. El cajero registra uno o varios medios, confirma, y se emite **un solo
recibo** con todas las aplicaciones, o ninguna.

**Why this priority**: es el corazón de Recaudo y lo único que libera la activación de Cartera y Aportes.

**Independent Test**: para un asociado con dos créditos y la cuota de aportes del mes, pagar la cuota del
ciclo de uno, un abono extraordinario «reducir plazo» en el otro y la cuota de aportes, con 300.000 en efectivo
y 500.000 en tarjeta; comprobar un recibo con tres aplicaciones, cada una con su comprobante del módulo dueño,
y que repetir la confirmación con la misma clave devuelve el mismo recibo.

**Acceptance Scenarios**:

1. **Given** un abono extraordinario sin elección, **When** el cajero simula, **Then** ve las dos
   alternativas (reducir plazo y reducir cuota) con su plan y no puede confirmar sin la elección del deudor.
2. **Given** una simulación vista por el asociado y un pago de nómina aplicado al mismo crédito un segundo
   antes, **When** el cajero confirma, **Then** Cartera responde que la distribución cambió, el sistema muestra
   la nueva y pide confirmar otra vez; nada queda registrado mientras tanto.
3. **Given** un recibo con tres aplicaciones donde Captaciones rechaza la tercera (p. ej. límite de
   participación en aportes), **When** se confirma, **Then** no queda ningún pago, ni recibo, ni número
   consumido; el cajero ve el motivo de la tercera y puede quitarla o cancelar.
4. **Given** un pago de 1.000.000 para cancelar un crédito cuyo total es 980.000, **When** se simula, **Then**
   el excedente de 20.000 se muestra antes de confirmar y el cajero ajusta el valor o lo destina a otro producto;
   nunca entra a Cartera.
5. **Given** un tercero que paga por el asociado, **When** se registra, **Then** el recibo sale a nombre del
   pagador, lista al beneficiario de cada aplicación y Cartera registra al pagador como tercero del pago.
6. **Given** efectivo y vueltas, **When** el asociado entrega 100.000 para un total de 82.000, **Then** el
   recibo registra 100.000 recibidos y 18.000 de vueltas; sólo el efectivo admite vueltas.
7. **Given** una cancelación pagada con cheque, **When** se emite el recibo, **Then** dice «sujeta a firmeza
   hasta» la fecha que informó Cartera.
8. **Given** la cuota de un CDAT en apertura, **When** se consigna su fondeo por un valor distinto del pactado,
   **Then** se rechaza indicando el valor exacto.

---

### User Story 4 - Entregar dinero por la caja: retiros, saldos a favor y excedentes (Priority: P1)

El cajero paga en efectivo (o con cheque de la cooperativa, cuando haya chequera) un retiro de ahorro que
Captaciones autoriza, un saldo a favor o un anticipo que Cartera devuelve, o el saldo a favor que Recaudo guarda
de un pagador. Verifica la identidad y las firmas del titular, Captaciones valida y debita y devuelve la
autorización; el cajero entrega, el titular firma el **egreso de caja** y todo queda en una sola operación.

**Why this priority**: sin entrega de efectivo Captaciones no puede abrir la ventanilla (su cancelación con
entrega espera a Recaudo) y los excedentes de los pagos no tendrían salida.

**Independent Test**: retirar 400.000 en efectivo de una cuenta no exenta: comprobar el GMF de 1.600 que
calcula Captaciones, la entrega de 400.000, el egreso firmado y la baja del efectivo esperado de la caja.

**Acceptance Scenarios**:

1. **Given** una cuenta de manejo conjunto, **When** se presenta un solo titular, **Then** Captaciones rechaza
   indicando las firmas que faltan y la caja no entrega nada.
2. **Given** un retiro mayor que la atribución del cajero, **When** se simula, **Then** se pide un aprobador
   presente con segundo factor y, aprobado, se repite con la misma clave sin duplicar.
3. **Given** un retiro autorizado y una caja sin efectivo suficiente, **When** el cajero intenta entregar,
   **Then** se rechaza antes de pedir la autorización; no queda débito.
4. **Given** un saldo a favor de un crédito, **When** el deudor pide su devolución en caja, **Then** Cartera lo
   resuelve «devolver por el recaudador» y el egreso entrega el valor; si el asociado prefiere transferencia, se
   emite una orden de pago a Tesorería.
5. **Given** el egreso en efectivo de un monto sobre el umbral, **When** se registra, **Then** cuenta para el
   control y el reporte de transacciones en efectivo.

---

### User Story 5 - Anular recibos, reversar aplicaciones y atender los reversos de los módulos (Priority: P1)

Un recibo mal hecho se anula entero con motivo: la anulación es un documento nuevo que reversa cada aplicación
por su contrato, con la misma clave, y deja el original marcado como anulado. En la sesión abierta y el mismo
día lo pide el cajero y lo aprueba el supervisor; después, sólo con permiso propio. Cuando Cartera o Captaciones
reversan desde su pantalla una aplicación de Recaudo (por ejemplo, cheque devuelto), Recaudo recibe el aviso,
marca la aplicación «reversada por el módulo» y deja el dinero como saldo a favor del pagador por resolver.

**Why this priority**: sin anulación controlada no hay cuadre; los reversos de los módulos son parte del
contrato que ya rige en producción.

**Independent Test**: anular un recibo de dos aplicaciones y comprobar los dos reversos en Cartera y
Captaciones, el documento de anulación con su número, el efectivo esperado de la sesión rebajado y los hechos
espejo; reversar desde Cartera un pago con cheque y comprobar el aviso, la marca y la alerta al supervisor.

**Acceptance Scenarios**:

1. **Given** un recibo de la sesión abierta, **When** el cajero pide anularlo, **Then** queda pendiente hasta
   que otro usuario con atribución lo aprueba; el cajero no se aprueba a sí mismo.
2. **Given** un recibo de una sesión ya cerrada, **When** se anula con permiso, **Then** la anulación cae en
   la sesión abierta de quien anula, o en la bóveda si no hay sesión, y el reintegro del dinero se gestiona
   aparte (egreso u orden de pago).
3. **Given** un recibo cuya anulación rechaza uno de los contratos, **When** se confirma, **Then** no se anula
   nada y se muestra el motivo.
4. **Given** un pago de Nómina, **When** alguien intenta reversarlo desde Recaudo, **Then** no aparece: lo de
   Nómina sólo lo reversa Nómina.
5. **Given** un aviso de reverso de Cartera para un recibo ya anulado, **When** llega, **Then** se registra sin
   efecto y queda en la bandeja como procesado.

---

### User Story 6 - Consignaciones y transferencias recibidas: identificar, aplicar y partidas por identificar (Priority: P1)

El analista de recaudo carga cada día el extracto (o el archivo de movimientos) de cada cuenta bancaria de
recaudo. El sistema reconoce los abonos y propone a quién pertenecen por la referencia de pago, el documento del
consignante o una consignación reportada en caja. El analista confirma y aplica: cada abono identificado genera
un recibo de canal «banco» con la fecha valor del movimiento, aplicado en forma no interactiva. Lo que no se
identifica queda como **partida por identificar**; se identifica después, en todo o en parte, o se clasifica a
otro concepto con aprobación.

**Why this priority**: muchas cuotas de COOFLOPAL llegan por consignación; sin identificación quedan sin aplicar
y los créditos entran en mora indebida.

**Independent Test**: cargar un extracto con cinco abonos: tres con referencia válida se aplican, uno se
identifica por el documento del consignante y uno queda por identificar; identificarlo diez días después y
comprobar que se aplica con su fecha valor original.

**Acceptance Scenarios**:

1. **Given** un extracto ya cargado, **When** se vuelve a cargar con movimientos repetidos, **Then** los
   movimientos ya conocidos no se cargan dos veces y el sistema dice cuántos omitió.
2. **Given** un abono de 900.000 de un pagador con dos créditos, **When** el analista lo divide en 600.000 y
   300.000, **Then** se emite un recibo con dos aplicaciones; la suma debe ser exacta.
3. **Given** una partida identificada después de la ventana de fecha valor del contrato, **When** se aplica,
   **Then** conserva la fecha valor del movimiento (enmienda FR-164); mientras la enmienda no exista, el sistema
   avisa y aplica con la fecha más antigua admitida, dejando la diferencia explicada.
4. **Given** una partida con más de 90 días, **When** corre el proceso diario, **Then** alerta al supervisor y
   al contador; no cambia nada solo.
5. **Given** un abono que resulta ser un reintegro de un proveedor, **When** el analista lo clasifica al
   concepto «reintegros», **Then** pide aprobación y emite el hecho de ese concepto; no genera recibo de
   ninguna persona sin tercero.

---

### User Story 7 - Pagadurías y libranza (Priority: P1)

Cartera genera el archivo de descuentos de cada pagaduría y período y se lo envía a Recaudo. El analista lo ve
en la bandeja por pagaduría, lo descarga, lo envía a la pagaduría y **confirma el envío** con canal, fecha y
radicado. Cuando la pagaduría responde, **carga su archivo** con el formato de vuelta del convenio, revisa la
vista previa y **devuelve el resultado** a Cartera. Cuando la pagaduría **gira**, el analista registra el giro
(identificado en el extracto o recibido en cheque) y lo aplica: cada línea descontada pendiente de giro se paga
por el contrato de obligaciones o el de cuentas, citando la línea, en una sola operación.

**Why this priority**: la libranza es el recaudo principal de COOFLOPAL y de toda cooperativa con convenios de
nómina con empresas.

**Independent Test**: con el doble de Cartera reemplazado por Cartera real en prueba, recibir un archivo de 50
líneas (créditos y aportes), confirmar el envío, cargar una respuesta con 45 descontadas y 5 no descontadas,
registrar el giro de las 45 y comprobar las aplicaciones, el recibo de canal «pagaduría» y que las 5 siguen
exigibles en Cartera con su motivo.

**Acceptance Scenarios**:

1. **Given** una respuesta de la pagaduría con un empleado sin equivalencia, **When** se procesa, **Then** esa
   fila queda en «no aplicados» con su motivo y el resto sigue; el dinero de esa fila, si llega, queda como saldo
   de la pagaduría por aplicar.
2. **Given** un giro menor que lo descontado, **When** se aplica, **Then** se aplican las líneas que el giro
   cubre en el orden que fije el analista o la prelación del convenio, y las demás siguen «pendientes de giro».
3. **Given** un giro mayor que lo descontado, **When** se aplica, **Then** el excedente queda como saldo a favor
   de la pagaduría por resolver, nunca en un crédito.
4. **Given** un archivo de descuentos ya confirmado como enviado, **When** se intenta confirmar otra vez,
   **Then** no cambia nada; si la huella del archivo no coincide, Cartera lo rechaza y se muestra el motivo.
5. **Given** un convenio con cuotas de aportes y ahorros, **When** Recaudo no adoptó la versión del archivo que
   las admite, **Then** Cartera no genera ese archivo y la pantalla de Recaudo indica qué versión adoptar.

---

### User Story 8 - Contabilidad recibe los hechos de Recaudo y Tesorería por su matriz (Priority: P1)

Cada recibo, egreso, anulación, movimiento de caja, diferencia de arqueo, remesa, partida por identificar,
giro, resultado de giro, traslado y nota bancaria emite un hecho a Contabilidad, sin cuentas. Contabilidad lo
contabiliza por la matriz de cada emisor (hecho, rol y dimensiones como medio, punto, caja, oficina, cuenta
bancaria propia, concepto y módulo de destino). Los puentes con Cartera y Captaciones (recaudo por aplicar,
desembolso por girar, devolución por pagar) quedan en cero o explicados.

**Why this priority**: sin contabilidad no se cierra el mes; los puentes de Cartera y Captaciones sólo se
saldan con los hechos de Recaudo y Tesorería.

**Independent Test**: con la matriz completa, emitir un recibo en efectivo y tarjeta que paga un crédito y
aportes, girar un desembolso por archivo y cerrar la caja con un faltante; comprobar los comprobantes y que la
conciliación de puentes de Cartera y Captaciones da cero. Quitar la regla de la tarjeta y comprobar que la
pantalla de caja no confirma con ese medio y dice qué falta.

**Acceptance Scenarios**:

1. **Given** un medio de pago sin regla contable vigente y la política «bloquear», **When** el cajero lo
   elige, **Then** el recibo no se confirma y se indica el medio, el rol y quién debe completarlo.
2. **Given** los recibos del día en modo «por lotes», **When** corre el lote, **Then** se genera un
   comprobante resumido por día, punto y medio, con tercero o documento cruce sólo donde la cuenta lo exige.
3. **Given** un recibo anulado, **When** se contabiliza, **Then** su anulación es un hecho espejo con la fecha
   contable del período abierto; el original no se toca.
4. **Given** un comprobante de Recaudo, **When** alguien lo intenta reversar desde Comprobantes, **Then** se
   rechaza indicando que se corrige desde Recaudo.

---

### User Story 9 - Registrarse como participantes y liberar la activación de Cartera y Captaciones (Priority: P1)

El administrador de la cooperativa registra a Recaudo como recaudador (contrato de obligaciones y de cuentas) y
como destino de sus mensajes, y a Tesorería como destino de giros, con las versiones que este despliegue
implementa. La activación de Cartera y de Aportes en producción ve los requisitos en verde. En los ambientes de
prueba, el doble de prueba se retira del registro antes de registrar al participante real con el mismo código.

**Why this priority**: es literalmente lo que bloquea la salida de COOFLOPAL.

**Independent Test**: en una cooperativa de prueba con los dobles registrados, retirar los dobles, registrar
Recaudo y Tesorería reales y comprobar que la activación de Cartera muestra sus requisitos cumplidos y que un
pago por caja y un giro por archivo recorren el ciclo completo.

**Acceptance Scenarios**:

1. **Given** un despliegue de producción, **When** se intenta registrar un doble de prueba, **Then** se rechaza.
2. **Given** el doble de tesorería registrado con su código, **When** se registra la Tesorería real con el
   mismo código, **Then** se rechaza indicando que primero se retire el doble.
3. **Given** Recaudo registrado sin adoptar el contrato de cuentas, **When** se intenta activar Aportes, **Then**
   la activación dice qué adopción falta.
4. **Given** Recaudo sin puntos, cajas, medios ni matriz contable completa, **When** se intenta activar
   Recaudo, **Then** se niega con la lista de lo que falta.

---

### User Story 10 - Cuentas bancarias propias y ejecución de órdenes de giro (Priority: P1)

El tesorero registra las cuentas bancarias de la cooperativa, cada una vinculada a su cuenta contable bancaria.
Las órdenes de giro de Cartera y Captaciones llegan a una **bandeja única de órdenes de pago** con su origen,
beneficiario, cuenta destino, valor, fecha y forma. El tesorero arma un lote por cuenta de origen y forma, revisa
la vista previa con el formato bancario vigente del banco, y lo envía a aprobación; aprobado por quien tenga
atribución para el monto, genera el archivo (inmutable, con huella), lo descarga, lo carga en el banco y lo marca
enviado. Los cheques se imprimen de la chequera de la cuenta.

**Why this priority**: sin giros no hay desembolsos ni devoluciones; es el segundo requisito de activación.

**Independent Test**: recibir tres órdenes (un desembolso, una devolución del retiro de un asociado y los
intereses de un CDAT a un tercero), armar el lote en la cuenta del banco de COOFLOPAL, aprobarlo con otro usuario,
generar el archivo con el formato del banco y comprobar que el total, la cantidad y cada línea coinciden con las
órdenes.

**Acceptance Scenarios**:

1. **Given** una orden cuyo beneficiario no tiene cuenta o cuyo banco no tiene código de transferencia, **When**
   se arma el lote, **Then** queda fuera, en pendientes, con el motivo.
2. **Given** una orden «pagada por archivo de Cartera», **When** se arma un lote, **Then** no se puede incluir;
   sólo se le registra el resultado.
3. **Given** un lote preparado por el tesorero, **When** el mismo tesorero intenta aprobarlo, **Then** se
   rechaza; lo aprueba otra persona con atribución suficiente para el total, o varios niveles si el monto lo
   exige.
4. **Given** la anulación de una orden que está en un lote no enviado, **When** llega, **Then** la orden sale
   del lote y se responde «anulada»; si el archivo ya se envió, la orden sigue hasta conocer si se giró.
5. **Given** un archivo generado y no enviado, **When** se anula, **Then** sus órdenes vuelven a la bandeja y el
   archivo queda anulado con su huella para la historia.

---

### User Story 11 - Confirmar, rechazar y regirar (Priority: P1)

El tesorero carga la respuesta del banco (o confirma con el extracto, o registra a mano con soporte) y el
sistema informa a cada origen el **resultado del giro**: girada, rechazada con su causal, cheque no cobrado o
anulada. Una orden rechazada vuelve a su origen, que decide: Cartera reversa el desembolso y espera un giro
nuevo; Captaciones reintegra el saldo o deja la orden pendiente de regiro. La orden nueva que llega se enlaza a
la anterior y Tesorería muestra la cadena.

**Why this priority**: el contrato con Cartera y Captaciones exige el resultado; sin él no se cierra el puente
de giros.

**Independent Test**: de un archivo de tres órdenes, el banco rechaza una por cuenta inexistente; comprobar
«girada» en dos, «rechazada» en la tercera con su causal, el reverso del desembolso en Cartera y que la orden de
regiro llega enlazada y se paga en un archivo nuevo.

**Acceptance Scenarios**:

1. **Given** el parámetro «girada al confirmar el banco», **When** se marca enviado el archivo, **Then** las
   órdenes quedan «enviadas» sin informar todavía; se informan con la respuesta o la conciliación.
2. **Given** un archivo entero rechazado por el banco por un error de forma, **When** se registra, **Then** no se
   informa rechazo al origen: el archivo queda «rechazado por el banco» y sus órdenes vuelven al lote para
   generarlo otra vez.
3. **Given** un cheque entregado y no cobrado en los días configurados, **When** corre el proceso, **Then**
   alerta al tesorero; anulado el cheque con permiso, se informa «cheque no cobrado».
4. **Given** una orden sin resultado en los días configurados, **When** corre el proceso, **Then** alerta al
   tesorero (además de la alerta que ya levanta el origen).
5. **Given** el mismo resultado informado dos veces, **When** llega al origen, **Then** no cambia nada.

---

### User Story 12 - Seguridad, alcance, segregación y auditoría (Priority: P1)

Cada acción de Recaudo y Tesorería exige su permiso; el alcance limita a cada usuario a sus oficinas, puntos y
cajas (Recaudo) y a sus cuentas bancarias (Tesorería); las operaciones sobre la atribución del usuario piden un
aprobador. Quien registra no aprueba lo suyo. Todo queda en la auditoría con cadena de sellos, incluidas las
lecturas sensibles (lo que paga y debe una persona, saldos bancarios, exportaciones).

**Why this priority**: es dinero; la revisoría y el SARLAFT lo exigen desde el primer día.

**Independent Test**: con un cajero de la oficina A, pedir un recibo de la oficina B y comprobar que responde
igual que ante uno inexistente; anular con el mismo usuario que registró y comprobar el rechazo; alterar un
evento de auditoría y comprobar que la verificación lo detecta.

**Acceptance Scenarios**:

1. **Given** un usuario sin el permiso de anular, **When** intenta anular, **Then** se rechaza y el intento
   queda auditado.
2. **Given** un supervisor con alcance en un solo punto, **When** lista sesiones, **Then** ve sólo las de su
   punto.
3. **Given** un tesorero con atribución de 50 millones, **When** aprueba un lote de 80 millones, **Then** su
   aprobación cuenta como un nivel y el lote espera a quien complete el monto.

---

### User Story 13 - Puesta en marcha: saldos iniciales, pendientes y cuadre (Priority: P1)

Antes de activar, el contador y el tesorero cargan por plantilla las bóvedas y cajas con su efectivo al corte,
las cuentas bancarias propias con su saldo y chequeras, los cheques girados no cobrados, las consignaciones en
tránsito, los cheques recibidos en canje y las partidas por identificar del corte. El sistema cuadra cada cifra
contra Contabilidad y activa Recaudo y Tesorería en la cooperativa con fecha.

**Why this priority**: COOFLOPAL arranca con dinero en caja y en bancos y con partidas pendientes; sin ellas el
primer arqueo y la primera conciliación no cuadran.

**Independent Test**: cargar una plantilla con un error de fila y comprobar que no guarda nada y dice fila y
columna; corregir, cargar y comprobar el cuadre en cero contra Contabilidad.

**Acceptance Scenarios**:

1. **Given** un saldo de bóveda distinto del saldo contable de su cuenta de caja, **When** se cuadra, **Then**
   la diferencia se muestra y la activación exige un acta que la acepte.
2. **Given** saldos iniciales cargados, **When** se activa, **Then** no se emiten hechos contables por ellos
   (ya están en los saldos de apertura de Contabilidad).
3. **Given** cheques girados en tránsito cargados, **When** se cobran después, **Then** se concilian como
   cualquier cheque de Tesorería.

---

### User Story 14 - Control de efectivo y SARLAFT (Priority: P1)

Al recibir o entregar efectivo, el sistema suma por persona la operación y lo del período. Sobre el umbral de la
operación exige la declaración de origen de fondos (con su soporte adjunto) una vez por recibo y la hace viajar
a cada aplicación que la exija; sobre el acumulado, o ante señales de fraccionamiento, alerta al oficial de
cumplimiento. Cada mes el oficial genera el **reporte de transacciones en efectivo** (individuales y múltiples)
con la estructura vigente como dato, lo valida y lo marca cargado con su radicado.

**Why this priority**: es obligación legal desde la primera operación en efectivo; el primer reporte vence en
enero de 2027.

**Independent Test**: con umbral individual de 10 millones, recibir 12 millones en efectivo sin declaración y
comprobar el rechazo; con declaración, el recibo; recibir 3 pagos de 9 millones en el mes y comprobar la alerta
de fraccionamiento y su aparición en el reporte de múltiples.

**Acceptance Scenarios**:

1. **Given** un recibo con 6 millones en efectivo y 6 millones en transferencia, **When** el umbral es de 10
   millones en efectivo, **Then** no se exige declaración: cuenta sólo el efectivo.
2. **Given** un pagador tercero no asociado con coincidencia en listas restrictivas, **When** se registra el
   recibo, **Then** se alerta al oficial y se sigue o se retiene según el parámetro de la cooperativa.
3. **Given** el reporte del mes generado, **When** se regenera, **Then** queda una versión nueva y la anterior
   reemplazada, con su archivo.

---

### User Story 15 - Cheques recibidos y canje (Priority: P2)

El cajero recibe un cheque como medio (banco, número, cuenta, girador, fecha y valor). El cheque se consigna en
la remesa del día y pasa a «en canje»; el valor queda no disponible en captaciones y la cancelación de un
crédito queda sujeta a firmeza. Cuando el extracto lo confirma, pasa a «pagado». Si el banco lo devuelve, el
analista registra la devolución con su causal y el sistema reversa por los contratos cada aplicación que se pagó
con él, avisa al pagador y alerta al supervisor.

**Why this priority**: se reciben pocos cheques en ventanilla; puede arrancar con canje manual (D21).

**Independent Test**: pagar con un cheque una cuota de crédito y un aporte, devolverlo por fondos insuficientes
y comprobar los dos reversos, el cheque devuelto, el aviso y el informe de cheques devueltos.

**Acceptance Scenarios**:

1. **Given** un cheque posfechado, **When** se recibe, **Then** se rechaza salvo que el parámetro lo admita.
2. **Given** un cheque devuelto cuyo valor ya se retiró de la cuenta de ahorro, **When** se reversa, **Then**
   Captaciones deja el sobregiro técnico con bloqueo y alerta; Recaudo lo muestra en el informe.
3. **Given** un girador con cheques devueltos, **When** entrega otro, **Then** el cajero ve la advertencia.

---

### User Story 16 - Convenios de recaudo y canales externos (Priority: P2)

El analista configura convenios de recaudo con bancos y corresponsales: cuenta recaudadora, estructura de la
referencia de pago y formato del archivo diario de recaudo como dato versionado. Los documentos al asociado
(extracto de crédito, cupón de pago) llevan su referencia. Cada día carga el archivo del convenio; el sistema lo
valida, identifica por referencia y aplica por lote, con un recibo por pago; lo que no casa va a partidas por
identificar. Concilia lo informado por el convenio con lo abonado en la cuenta, neto de comisiones.

**Why this priority**: amplía los canales; COOFLOPAL puede salir con consignaciones identificadas por extracto
(US6).

**Independent Test**: configurar un convenio con referencia «número de obligación + dígito», cargar un archivo
de 100 pagos con 2 referencias inválidas y comprobar 98 aplicaciones y 2 partidas.

**Acceptance Scenarios**:

1. **Given** un archivo del convenio ya cargado, **When** se carga otra vez, **Then** se rechaza por su huella.
2. **Given** un dígito de verificación inválido, **When** se identifica, **Then** el pago va a partidas con el
   motivo.
3. **Given** lo informado de 10.000.000 y un abono de 9.985.000, **When** se concilia, **Then** la diferencia
   se explica como comisión del convenio con su hecho, sobre la tolerancia exige aprobación.

---

### User Story 17 - Conciliación bancaria con el extracto (Priority: P2)

El tesorero concilia cada cuenta bancaria por mes: el sistema empareja el extracto con lo registrado —recibos de
canal banco, remesas, giros, cheques cobrados, dispersiones de Nómina, archivos de desembolsos de Cartera y
movimientos manuales de Contabilidad— por referencia, valor y fecha; propone y el tesorero confirma lo dudoso.
Las notas bancarias (GMF, comisiones, intereses, impuestos) se registran como hechos de Tesorería con
aprobación; los abonos no identificados pasan a Recaudo como partidas por identificar. Cierra la conciliación
con sus partidas conciliatorias.

**Why this priority**: necesaria para el primer cierre mensual; R1 ya identifica los abonos de recaudo.

**Independent Test**: conciliar un mes con 200 movimientos: 190 automáticos, 6 manuales, 2 notas de GMF y
comisión registradas, 1 cheque girado no cobrado y 1 consignación en tránsito como partidas; comprobar el saldo
conciliado igual al contable.

**Acceptance Scenarios**:

1. **Given** una conciliación cerrada, **When** se necesita corregir, **Then** se reabre con motivo y permiso,
   y queda en la auditoría.
2. **Given** el mes contable por cerrar con conciliaciones abiertas, **When** el contador cierra, **Then** ve el
   aviso por cuenta (informativo por defecto, bloqueante si la cooperativa lo configura).
3. **Given** el GMF cobrado por el banco, **When** se registra, **Then** la nota toma la tarifa y la base que
   muestra el extracto; la tarifa configurada sólo se usa para estimar y advertir diferencias.

---

### User Story 18 - Pagos a proveedores y de cuentas por pagar (Priority: P2)

Las facturas de proveedor confirmadas en Comercial llegan a la bandeja de órdenes con su vencimiento y su neto
(las retenciones ya las practicó la factura). Las cuentas por pagar registradas en Contabilidad (honorarios,
servicios, impuestos) se consultan por tercero y documento cruce y se convierten en órdenes. El tesorero las
programa por vencimiento y las paga en lotes como los giros. Cada pago emite su hecho con tercero y documento
cruce, e informa a Comercial que la factura quedó pagada.

**Why this priority**: COOFLOPAL puede pagar proveedores como hoy durante el primer mes; es T2.

**Independent Test**: pagar en un lote dos facturas de proveedor y una cuenta por pagar contable, comprobar el
hecho de cada una con su documento cruce y el saldo pendiente en cero en Contabilidad.

**Acceptance Scenarios**:

1. **Given** una factura ya incluida en un lote no anulado, **When** se intenta incluir en otro, **Then** se
   rechaza indicando el lote.
2. **Given** un pago parcial de una cuenta por pagar, **When** se registra, **Then** el saldo pendiente queda
   con el resto.

---

### User Story 19 - Chequeras y cheques girados; traslados entre cuentas propias (Priority: P2)

El tesorero registra las chequeras de cada cuenta (rango) y los cheques salen numerados de ella; los dañados se
anulan con su número. Un cheque girado pasa por emitido, entregado, cobrado (por conciliación), anulado o no
cobrado. Los traslados entre cuentas propias y las remesas de efectivo entre bóvedas y bancos se registran con
aprobación y quedan en tránsito hasta que el extracto los confirma.

**Why this priority**: los cheques son una forma de giro de la orden; las remesas ligan la caja con los bancos.

**Independent Test**: girar un cheque de una orden, anular el siguiente por dañado y comprobar que la numeración
no tiene huecos sin explicar; trasladar 50 millones entre dos cuentas y comprobar que queda en tránsito hasta la
conciliación.

**Acceptance Scenarios**:

1. **Given** una chequera agotada, **When** se intenta girar, **Then** se pide registrar la siguiente.
2. **Given** un cheque entregado, **When** se intenta anular sin devolverlo, **Then** exige motivo, permiso y,
   si ya se informó «girada», informa «cheque no cobrado» al origen.

---

### User Story 20 - Posición de liquidez y flujo de caja (Priority: P3)

El tesorero ve cada día los saldos de bancos, bóvedas y cajas, los compromisos (órdenes aprobadas, cheques en
tránsito, giros de nómina) y lo esperado por recaudar (cuotas y exigibles por vencer que informan Cartera y
Captaciones), y proyecta el flujo de caja por semanas y meses, comparando lo real con lo proyectado. Las cuentas
que constituyen el fondo de liquidez quedan marcadas y su saldo lo consulta Captaciones.

**Why this priority**: gestión; reemplaza el flujo de caja heredado, que sólo sumaba facturas.

**Independent Test**: con saldos y compromisos conocidos, comprobar que la posición del día coincide al peso
con la suma de los componentes y que el flujo real de un mes cerrado coincide con Contabilidad.

**Acceptance Scenarios**:

1. **Given** una cuenta marcada como fondo de liquidez, **When** Captaciones calcula el fondo, **Then** recibe su
   saldo a la fecha.

---

### User Story 21 - Informes de recaudo, caja y tesorería (Priority: P2)

Supervisores, tesorero, contador y auditor consultan y exportan: recaudo diario por punto, caja, cajero, medio,
canal y módulo; recibos anulados con motivo; partidas por identificar por antigüedad; cheques en canje y
devueltos; pagadurías (enviado, descontado, girado, pendiente); órdenes por estado y antigüedad; giros
rechazados y pendientes de regiro; cheques girados; saldos bancarios; conciliaciones. El recaudo consolidado
incluye, de sólo lectura, lo recaudado por Nómina y por débito automático.

**Why this priority**: control y revisoría; los de cierre de caja y partidas van en R1 (FR-152).

**Independent Test**: comprobar que el recaudo del día por medio coincide con los cierres de las sesiones y con
los hechos contabilizados.

**Acceptance Scenarios**:

1. **Given** un usuario con alcance en una oficina, **When** consulta, **Then** ve sólo esa oficina.
2. **Given** una exportación, **When** se descarga, **Then** exige su permiso y queda auditada.

---

### User Story 22 - Conciliación de adquirentes de tarjetas (Priority: P3)

El analista carga el reporte de abonos de cada red o adquirente y lo concilia con los lotes de datáfono
cerrados en las cajas de Recaudo y del punto de venta: lo cobrado contra lo abonado, la comisión y las
retenciones que practicó la red. Las diferencias se registran con su hecho.

**Why this priority**: la 012 la dejó para Tesorería; es control, no operación.

**Independent Test**: conciliar un día con dos lotes y un abono con comisión y retención, y comprobar el hecho
y la diferencia en cero.

**Acceptance Scenarios**:

1. **Given** un lote sin abono pasado el plazo, **When** corre el proceso, **Then** alerta.

---

### User Story 23 - Recaudar cuentas por cobrar comerciales que no pasan a Cartera (Priority: P2)

Lo vendido a crédito por Comercial que no pasa a Cartera (servicios a asociados) queda como cuenta por cobrar en
Contabilidad. El cajero lo ve en la pantalla de la persona como «cuentas por cobrar comerciales» con su factura y
saldo, y lo cobra en el mismo recibo; el hecho lleva el tercero y la factura como documento cruce.

**Why this priority**: COOFLOPAL cobra servicios así; hoy nadie tiene cómo recaudarlo, aunque al principio se
pueden cobrar como concepto vario con documento cruce.

**Independent Test**: vender a crédito un servicio, cobrarlo en caja junto con una cuota de crédito y comprobar
el saldo de la factura en cero en Contabilidad.

**Acceptance Scenarios**:

1. **Given** un pago mayor que el saldo de la factura, **When** se registra, **Then** se rechaza el excedente o
   se destina a otro producto.

---

### Edge Cases

**Caja**
- **Corte de luz o red a mitad de un recibo.** Nada queda a medias: o el recibo y todas sus aplicaciones o nada;
  al volver, el cajero repite con la misma clave y recibe el recibo si ya quedó.
- **Recibo confirmado y la impresora falla.** El recibo existe; se reimprime marcado «copia».
- **Dos cajeros cobran al mismo deudor a la vez.** El segundo recibe «la distribución cambió» y ve la nueva.
- **Sesión olvidada abierta de un día para otro.** Alerta al supervisor; la cierra él con arqueo diferido; los
  recibos del día siguiente no caen en ella.
- **Cambio de medio después de confirmar** (registrado como Visa, pasado por Mastercard). Reclasificación entre
  medios con motivo y aprobación; el recibo no cambia.
- **Billete falso detectado después.** Faltante del arqueo con su motivo.
- **Cajero que también vende en el punto de venta.** Según C1 (FR-021).
- **Pago con dos tarjetas y una se rechaza en el datáfono.** El recibo no se confirma hasta que el total de
  medios cuadra.

**Contratos**
- **Módulo no activado** (persona de una carga aplicada sin activar). El valor queda como recaudo pendiente de
  aplicar y se aplica al activar con su fecha valor; fuera de ese caso, el cajero ve «no habilitado».
- **Fecha valor anterior al corte de la carga.** El contrato la rechaza; ese pago ya debía estar en la carga.
- **Validación previa contable sin respuesta.** Rige la política de cada módulo (aviso, nunca bloqueo en el
  contrato).
- **Excedente en un pago no interactivo** (banco, pagaduría). Queda como saldo a favor del pagador en Recaudo
  por resolver: devolver por orden de pago o aplicar con un recibo nuevo.
- **Aviso de reverso de un pago que Recaudo no reconoce.** Queda en la bandeja como rechazado con su motivo.
- **Retiro autorizado cuando el titular cumple 18 años con el representante en ventanilla.** Captaciones lo
  rechaza; la caja no entrega.

**Bancos y pagadurías**
- **El mismo abono aparece en el extracto y en el archivo del convenio.** Se identifica una sola vez (huella del
  movimiento bancario).
- **Un consignante paga dos personas en una consignación.** Se divide al identificar.
- **Partida identificada a una persona fallecida o retirada.** Se aplica si el módulo lo admite; si no, se
  devuelve al consignante por orden de pago.
- **La pagaduría gira antes de responder.** El giro queda como saldo de la pagaduría por aplicar hasta la
  respuesta.
- **La pagaduría gira el total de dos períodos.** Se aplica por período, en el orden de los archivos.
- **Respuesta de pagaduría con un concepto o empleado sin equivalencia.** No aplicados con motivo, sin bloquear
  lo demás.

**Tesorería**
- **Orden anulada mientras el banco procesa el archivo.** La orden sigue hasta conocer si se giró; si se giró,
  se informa «girada» y el origen la gestiona como pago.
- **El banco rechaza una línea días después de marcarla girada** (abono devuelto). Se informa «rechazada» como
  un resultado posterior; el origen decide.
- **Cuenta destino del beneficiario cambia antes de girar.** La orden es del origen: Tesorería no la edita; el
  origen anula y emite otra.
- **Formato del banco cambia.** Versión nueva del formato con vigencia; los archivos ya generados conservan el
  suyo.
- **Cheque girado que caduca.** Alerta, anulación con permiso y «cheque no cobrado».
- **GMF real distinto del estimado.** Manda el del extracto; la diferencia se explica en la conciliación.
- **Dispersión de nómina desde una cuenta de Tesorería.** Nómina paga con su propio archivo; Tesorería la ve
  como compromiso y la concilia, no la vuelve a pagar.

**Puesta en marcha**
- **Partida por identificar heredada sin fecha.** Se rechaza la fila: la fecha es obligatoria para la
  antigüedad.
- **Cheque en tránsito heredado de una chequera no registrada.** Se carga la chequera primero.
- **Pagos recibidos entre el corte y la activación.** Se conservan como recaudo pendiente de aplicar y se aplican
  al activar con su fecha valor.

## Requirements *(mandatory)*

### Functional Requirements

#### Transversales

- **FR-001 · Un solo camino del dinero.** Todo dinero que entra de una persona o de un tercero entra por un
  **recibo** de Recaudo (de caja, de banco, de convenio o de pagaduría); todo dinero que sale por la caja sale por
  un **egreso** de Recaudo, y todo lo que sale de un banco, por un **pago** de Tesorería. Ningún módulo cambia el
  saldo de otro: lo pide por su contrato.
- **FR-002 · Inmutabilidad.** Recibos, egresos, sesiones cerradas, lotes y archivos de pago, resultados de giro y
  conciliaciones cerradas no se editan ni se borran. Se corrigen con un documento nuevo enlazado al original
  (anulación, ajuste, reverso, reapertura con motivo). Un documento anulado sigue visible con su anulación.
- **FR-003 · Sólo por contratos.** Recaudo y Tesorería no conocen las reglas internas de Cartera, Captaciones,
  Comercial, Nómina ni Contabilidad, ni leen sus datos: usan sus contratos, consultas y mensajes vigentes. Lo que
  falta se pide como enmienda (FR-164 a FR-169), nunca con un atajo.
- **FR-004 · Sin asientos ni cuentas.** Recaudo y Tesorería no escriben asientos: emiten hechos a Contabilidad,
  en la misma transacción que su operación, y ningún hecho lleva una cuenta contable (sección R).
- **FR-005 · Idempotencia.** Todo recibo, egreso, aplicación, anulación, lote, archivo, resultado de giro y
  mensaje lleva una clave de origen. Repetir con la misma clave y el mismo contenido devuelve el original;
  con otro contenido se rechaza sin efectos.
- **FR-006 · Claves por módulo.** Las claves que Recaudo y Tesorería pasan a los contratos empiezan con su código
  de módulo y respetan la gramática que fijaron los contratos (una clave por aplicación de cada recibo; para el
  giro de una pagaduría, el giro y la línea que cubre; derivadas para los reversos).
- **FR-007 · Concurrencia.** Una sesión abierta por caja (y por cajero si es exclusivo); numeración sin huecos
  por concurrencia y asignada al final de la confirmación; dos operaciones sobre la misma persona u obligación las
  ordena el módulo dueño; una orden de pago está a lo sumo en un lote no anulado.
- **FR-008 · Fechas.** Fecha local de Colombia. Cada documento lleva fecha de registro (instante), fecha valor
  (caja: hoy; banco, convenio o pagaduría: la del movimiento o del descuento) y fecha contable (la del período
  abierto, que fija Contabilidad). En caja no se registra fecha valor distinta de hoy salvo permiso propio.
- **FR-009 · Parametrización con vigencia y sin valores legales en el código.** Umbrales de efectivo, plazos,
  tolerancias, topes, tarifas y exenciones de GMF, días de canje, días sin resultado y demás valores viven en
  parámetros por cooperativa con vigencia y fuente; lo sembrado sin validar va marcado «pendiente de validar». Una
  verificación automática impide literales legales en el código de estos módulos.
- **FR-010 · Plazos regulatorios informativos.** Los plazos de reportes, de giro de pagadurías y de caducidad se
  muestran y se alertan; no bloquean operaciones salvo que la cooperativa lo configure.
- **FR-011 · Permisos y alcance.** Permisos propios por acción (consultar, registrar, anular, aprobar, exportar,
  configurar) en Recaudo y en Tesorería, sembrados completos con la primera entrega. El alcance limita por
  **oficina, punto y caja** (Recaudo) y por **cuenta bancaria propia** (Tesorería); falla cerrado y responde como
  ante lo inexistente.
- **FR-012 · Segregación.** Quien registra no aprueba lo suyo: anulaciones, diferencias de arqueo, partidas
  clasificadas a otro concepto, lotes de pago, notas bancarias, traslados y reaperturas. Por defecto, quien
  prepara un lote no lo aprueba ni registra su resultado. La comparación es por persona.
- **FR-013 · Aprobaciones multinivel por monto.** Las operaciones sobre la atribución del usuario usan las
  aprobaciones de la plataforma: límites de monto por permiso, niveles sucesivos hasta cubrir el monto, aprobador
  **presente con segundo factor** en la caja, aprobación diferida en Tesorería; la operación no mueve dinero hasta
  aprobarse.
- **FR-014 · Auditoría.** Toda operación, rechazo, aprobación y lectura sensible (lo que debe y paga una persona,
  saldos bancarios, exportaciones, reportes de efectivo) queda en la auditoría con cadena de sellos, con diez años
  de retención.
- **FR-015 · Errores visibles.** Mensajes en español con código por módulo; cada rechazo de un contrato se
  muestra por ítem con su motivo; las fallas de consumo de mensajes quedan en la bandeja con reproceso; las tareas
  programadas alertan su falla.
- **FR-016 · Persona maestra.** Pagadores, beneficiarios, giradores, pagadurías y cajeros son personas del
  maestro; el alta de un pagador nuevo se hace en un paso con el buscador común, con la autorización de datos.
- **FR-017 · Identificadores.** Documentos, órdenes, lotes y partidas se exponen por identificador público y
  número visible.
- **FR-018 · Uso.** Un recibo de caja se completa en tres pasos o menos (persona, qué paga, medios); toda pantalla
  muestra el indicador de carga por zona; disponible en la aplicación web y en la instalable; la caja se opera
  con teclado y lector.
- **FR-019 · Por cooperativa.** Todo vive en la base de cada cooperativa; las tareas programadas corren por
  cooperativa como «Proceso de la cooperativa» y sólo originan lo marcado para el proceso.
- **FR-020 · Avisos.** El recibo, el egreso y el soporte de pago se envían por correo electrónico al pagador o
  beneficiario sólo con la autorización del canal; SMS y mensajería siguen simulados hasta que haya proveedor.

#### A · Puntos, cajas, medios y catálogos

- **FR-021 · Puntos de recaudo y cajas.** Cada oficina tiene puntos de recaudo y cada punto, cajas con código,
  nombre, base y estado con vigencia. La caja de Recaudo y la del punto de venta de la 012 son **dos cajas separadas** que comparten el catálogo de
  medios de pago, las denominaciones y el método de arqueo; un cajero que haga las dos cosas abre dos sesiones y
  cada una cuadra y cierra por su lado (Clarifications, C1 = A).
- **FR-022 · Cajeros.** Las cajas tienen cajeros asignados (usuarios con persona); el parámetro «cajero
  exclusivo» impide que un cajero tenga dos sesiones abiertas.
- **FR-023 · Medios de pago.** Recaudo usa el **catálogo común de medios de pago** de la plataforma (el mismo del
  punto de venta), con su clase fija, red, adquirente, datáfonos, banco y cuenta destino como dato, reglas de
  captura, método y tolerancia de arqueo. Recaudo agrega **en qué puntos de recaudo** se ofrece cada medio. PSE y
  las pasarelas usan la clase transferencia; los pagos con tarjeta por pasarela, la clase de tarjeta. No se crean
  clases nuevas.
- **FR-024 · Medios no admitidos.** Las clases de crédito (a asociado, comercial) no se ofrecen en Recaudo. Un
  medio inactivo no se ofrece; un medio con pagos no se borra.
- **FR-025 · Denominaciones** de efectivo del catálogo común, para apertura, arqueo y remesas.
- **FR-026 · Tipos de documento.** Tipos de recibo (caja, banco, convenio, pagaduría, en línea), de egreso, de
  anulación y de movimiento de caja, con consecutivo de la cooperativa (prefijo por punto opcional), sin huecos.
- **FR-027 · Conceptos varios.** Catálogo de conceptos de ingreso y egreso no ligados a productos (reintegros,
  venta de formularios, rendimientos, devoluciones a terceros), con código, nombre, si exige tercero o documento
  cruce, si es ingreso o egreso y aprobación; la matriz contable los lleva a su cuenta. Reemplaza los conceptos de
  tesorería heredados.
- **FR-028 · Base y topes de caja.** Modo de base (fondo fijo o base del día, sellado al abrir), tope de efectivo
  en caja con aviso de traslado a bóveda y tope de egreso en efectivo por caja y día.

#### B · Sesión de caja y bóveda

- **FR-029 · Apertura.** La sesión se abre con la base contada por denominaciones (desde la bóveda o el cierre
  anterior, según el modo); sin sesión abierta la caja no recauda ni entrega.
- **FR-030 · Movimientos de caja.** Abastecimiento desde bóveda, retiro parcial a bóveda, traslado entre cajas y
  **reclasificación entre medios** de un pago ya confirmado, como documentos con motivo y la aprobación que diga
  su política; la reclasificación no toca el recibo y mueve lo esperado de un medio a otro.
- **FR-031 · Arqueo por medio.** Al cerrar, por cada medio según su método: efectivo por denominaciones o total,
  tarjetas por lote de cada datáfono (cotejo por referencia opcional), transferencias, consignaciones y en línea
  por referencias, cheques por relación. **Arqueo ciego** configurable, activado por defecto.
- **FR-032 · Diferencias.** Toda diferencia lleva motivo; sobre la tolerancia del medio exige aprobación de otra
  persona. Faltante: a cargo del cajero (como persona) o gasto; sobrante: ingreso o cuenta por pagar; según
  parámetro. Emite su hecho.
- **FR-033 · Cierre de sesión y del día.** El cierre de sesión emite el informe por medio (esperado, contado,
  diferencia); el cierre del día del punto consolida sus sesiones y sólo procede con todas cerradas y sus
  diferencias resueltas. Un cierre no se reabre: se corrige con un ajuste aprobado enlazado.
- **FR-034 · Sesión olvidada.** Una sesión abierta al pasar el día alerta al supervisor, que la cierra con arqueo
  diferido; no recibe operaciones del día siguiente.
- **FR-035 · Bóveda de oficina.** Cada oficina tiene una bóveda con saldo de efectivo por denominación, que
  abastece y recibe de las cajas y envía o recibe **remesas** de un banco (con Tesorería, FR-105); el efectivo en
  tránsito se ve hasta confirmarse.
- **FR-036 · Cuadre diario.** El efectivo de cajas, bóveda y tránsito de cada oficina cuadra con el saldo de su
  cuenta de caja en Contabilidad; la diferencia se muestra y se explica.

#### C · Recibo de caja multiproducto

- **FR-037 · Lo que una persona puede pagar.** Al buscar a una persona, Recaudo muestra en una pantalla, por los
  contratos: sus obligaciones (como deudor y, a pedido, como codeudor) con exigible hoy, cuota del ciclo, vencido,
  total para cancelar, anticipo, saldo a favor, modalidades admitidas y bloqueos; sus cuotas exigibles de aportes,
  ahorro permanente y contractual; sus cuentas para consignar con su estado; sus cuentas por cobrar comerciales
  pendientes (FR-049); y los conceptos varios. Leerlo es una lectura sensible auditada.
- **FR-038 · Créditos.** Por obligación, el cajero elige la modalidad admitida (cancelación, vencido, cuota del
  ciclo, anticipada, abono extraordinario, cuota específica, valor libre) y el valor; el abono extraordinario exige
  la **elección del deudor** entre reducir plazo o cuota (Ley 2032), con las dos alternativas a la vista. Se simula
  con Cartera y se aplica en forma interactiva con esa simulación.
- **FR-039 · Captaciones y aportes.** Por cuenta, consignación libre, cuotas exigibles en orden de vencimiento o
  cuotas específicas, y fondeo de un CDAT por su valor exacto; se simula con Captaciones (disponible, canje,
  efectivo, atribución, cuotas cubiertas) y se aplica con esa simulación.
- **FR-040 · Pagador y beneficiarios.** El pagador puede ser la persona o un tercero; el recibo sale a nombre del
  pagador y lista, por aplicación, el beneficiario y el producto. Los contratos reciben al pagador como tercero.
- **FR-041 · Medios combinados.** Un recibo se paga con cualquier combinación de medios ofrecidos en el punto;
  cada medio con valor y la referencia que exija; vueltas sólo en efectivo; un bono de número único no se usa dos
  veces. Total de medios = total aplicado + vueltas.
- **FR-042 · Todo o nada.** Confirmar un recibo registra en una sola operación el recibo, sus medios, todas sus
  aplicaciones (cada una por su contrato, dentro de la transacción de Recaudo) y sus hechos. Si un contrato rechaza
  un ítem, no queda nada, no se consume número y se muestra el motivo por ítem; si una distribución cambió, se
  muestra la nueva y se pide confirmar de nuevo.
- **FR-043 · Excedentes.** Lo que un contrato no aplica (excedente) se muestra antes de confirmar en caja para
  ajustar el valor o destinarlo; en las formas no interactivas queda como **saldo a favor del pagador** en Recaudo,
  por resolver (FR-054).
- **FR-044 · El recibo.** Número, fecha y hora, oficina, punto, caja, cajero, pagador, canal, aplicaciones con el
  comprobante y el número que devolvió cada módulo, medios con referencias, vueltas, avisos de firmeza y la
  declaración de origen de fondos si la hubo. Inmutable.
- **FR-045 · Entrega del recibo.** Impresión en tirilla o carta, reimpresión marcada «copia», PDF con código de
  verificación y envío por correo con autorización (FR-020).
- **FR-046 · Módulo no activado.** Si un contrato responde «no habilitado» para una persona incluida en una carga
  de migración aplicada y no activada, el valor queda como **recaudo pendiente de aplicar** (por persona y
  producto) y se aplica al activar, por lote y con su fecha valor, por una persona con permiso. Fuera de ese caso
  el ítem se rechaza con el aviso del módulo.
- **FR-047 · Fecha valor en caja.** Hoy; una fecha anterior exige permiso propio y respeta las reglas de fecha
  del contrato (incluido el permiso con monto para meses cerrados).
- **FR-048 · Firmeza.** El recibo informa «sujeta a firmeza hasta» la fecha que devuelva el módulo cuando una
  cancelación se paga con un medio no inmediato.
- **FR-049 · Cuentas por cobrar comerciales.** Lo vendido a crédito que no pasa a Cartera se consulta como
  pendiente por persona y factura (enmienda FR-167) y se cobra en el recibo; el hecho lleva tercero y factura como
  documento cruce; no se cobra más que el saldo.
- **FR-050 · Conceptos varios.** Un recibo puede incluir conceptos varios con tercero y documento cruce cuando el
  concepto lo exija.

#### D · Egresos de caja

- **FR-051 · Retiro de ahorro.** El cajero verifica la identidad y las firmas, simula con Captaciones, obtiene el
  aprobador si la atribución lo pide, y el retiro autorizado se entrega en efectivo (o cheque) con un **egreso**
  firmado, en una sola operación con la autorización; sin efectivo suficiente en la caja se rechaza antes de pedir
  la autorización.
- **FR-052 · Cancelación con entrega.** Cuando Captaciones habilite la cancelación de una cuenta con entrega por el
  contrato, la caja la entrega con el mismo egreso; mientras responda «no disponible», se hace por la ruta de
  Captaciones y la entrega por orden de pago.
- **FR-053 · Saldos a favor y anticipos de Cartera.** Se resuelven «devolver por el recaudador»: en efectivo por
  egreso o por **orden de pago** a Tesorería (transferencia o cheque), con el soporte que pida el parámetro.
- **FR-054 · Saldos a favor en Recaudo.** Los excedentes de formas no interactivas, los saldos de pagadurías y los
  reversos de módulos quedan como saldo a favor de un pagador por resolver: devolver (egreso u orden de pago) o
  aplicar con un recibo nuevo; con antigüedad y alerta.
- **FR-055 · Egresos varios.** Por concepto, apagados por defecto, con aprobación y tope; la caja menor queda
  fuera de alcance.
- **FR-056 · Efectivo entregado.** Cuenta para los topes de la caja y para el control de efectivo (sección J).

#### E · Anulación y reversos

- **FR-057 · Anulación de recibo.** Se anula el recibo entero, con motivo, por un **documento de anulación**
  numerado que reversa cada aplicación por su contrato con la clave derivada, todo o nada. En la sesión abierta y
  el mismo día: lo pide el cajero y lo aprueba otro; después: sólo con permiso propio y aprobación. No hay anulación
  parcial: se anula y se emite otro recibo.
- **FR-058 · Dinero de un recibo anulado.** La anulación baja lo esperado de la sesión donde cae (la abierta de
  quien anula o la bóveda); si el dinero ya no está, la devolución se hace por egreso u orden de pago; si el medio
  era electrónico ya abonado, por orden de pago.
- **FR-059 · Reverso por el módulo.** Al recibir «pago reversado por Cartera» o «consignación reversada por
  Cartera», Recaudo marca la aplicación reversada (el recibo no cambia), deja el valor como saldo a favor del
  pagador por resolver y alerta al supervisor; nunca devuelve ni reaplica solo.
- **FR-060 · Cheque devuelto.** Registrar la devolución (causal) reversa por los contratos cada aplicación pagada
  con ese cheque, en una sola operación; en captaciones, aunque deje sobregiro técnico; avisa al pagador y alerta.
- **FR-061 · Anulación de egreso.** Reversa el retiro o la resolución por su contrato y reingresa el dinero a la
  caja; sólo si el dinero vuelve, con aprobación.
- **FR-062 · Lo de Nómina.** Los descuentos de nómina propia no se anulan ni se reversan desde Recaudo.
- **FR-063 · Avisos repetidos o ajenos.** Un aviso de reverso repetido no cambia nada; uno de una aplicación que
  Recaudo no conoce queda rechazado en la bandeja con su motivo.
- **FR-064 · Anulación tras el cierre contable.** La anulación se fecha en el período abierto; el original no se
  reescribe.

#### F · Bancos: consignaciones y transferencias recibidas

- **FR-065 · Extractos y archivos de movimientos.** Por cuenta bancaria de recaudo, se carga el extracto o el
  archivo de movimientos con el formato del banco como dato versionado (motor de archivos planos, en lectura); se
  reconocen los abonos y cargos.
- **FR-066 · Sin duplicados.** Cada movimiento tiene una huella (banco, cuenta, fecha, valor, referencia,
  secuencia); un movimiento ya cargado no se carga otra vez.
- **FR-067 · Identificación.** Reglas configurables proponen el destino de cada abono: referencia de pago (de
  producto o de persona), documento del consignante, consignación reportada en caja o valor exacto de un
  exigible. La identificación automática se confirma por una persona, o se aplica sola si la regla lo permite.
- **FR-068 · Aplicación de lo identificado.** Cada abono identificado genera un recibo de canal «banco» con la
  fecha valor del movimiento, aplicado en forma no interactiva por los contratos; un abono puede dividirse entre
  varias personas y productos con suma exacta.
- **FR-069 · Consignación reportada en ventanilla.** El asociado que trae su soporte puede quedar aplicado de
  inmediato con medio «consignación» y pendiente de cruzar con el extracto, o quedar por verificar hasta el
  extracto, según el parámetro (por defecto, por verificar); lo no encontrado en el extracto en los días
  configurados alerta.
- **FR-070 · Partidas por identificar.** Un abono no identificado queda como partida (pasivo) con banco, cuenta,
  fecha, valor y referencia, sin persona; su hecho la reconoce.
- **FR-071 · Identificación posterior.** Una persona con permiso identifica la partida, entera o en partes, y la
  aplica con la **fecha valor del movimiento** (enmienda FR-164; hasta tenerla, con la más antigua admitida y la
  diferencia explicada).
- **FR-072 · Partidas que no son recaudo.** Rendimientos, reintegros, traslados propios o devoluciones se
  clasifican a un concepto con aprobación; la devolución al consignante va por orden de pago.
- **FR-073 · Antigüedad.** Las partidas se ven por antigüedad; pasado el plazo configurable (90 días por defecto)
  alertan; reclasificarlas a ingreso o a otra cuenta es una decisión registrada con aprobación, nunca automática.
- **FR-074 · Transferencias a cuentas de captación.** Entran por Recaudo como cualquier recaudo (D2); Tesorería no
  consigna en cuentas de asociados.

#### G · Pagadurías y libranza

- **FR-075 · Archivos de descuentos.** Recaudo recibe de Cartera cada archivo de descuentos por pagaduría (las
  versiones que adopte: sólo créditos, y créditos con cuotas de cuentas por grupos y conceptos equivalentes) y lo
  muestra por pagaduría, convenio y período con sus totales y su archivo para descargar.
- **FR-076 · Confirmar el envío.** Con canal (correo, portal, físico u otro), fecha y radicado, Recaudo confirma
  a Cartera el envío de esa versión con su huella.
- **FR-077 · Respuesta de la pagaduría.** Se carga el archivo de respuesta con el formato de vuelta del convenio,
  se muestra la vista previa (por línea o por concepto de la pagaduría) y se devuelve el resultado a Cartera; si
  Cartera lo rechaza (líneas que no coinciden), se muestra para corregir y emitir otro.
- **FR-078 · Giro de la pagaduría.** Se registra el dinero girado (identificado en el extracto o recibido en
  caja) y se aplica, en una sola operación, cada línea descontada pendiente de giro por el contrato que
  corresponda (créditos o cuotas de cuentas), con medio «descuento de pagaduría externa», la referencia de la línea
  y la fecha valor del descuento; un recibo de canal «pagaduría».
- **FR-079 · Giros parciales o mayores.** Un giro menor cubre las líneas en el orden de prelación del convenio o el
  que elija el analista y las demás siguen pendientes; un giro mayor deja el excedente como saldo a favor de la
  pagaduría (FR-054).
- **FR-080 · No aplicados.** Lo que la pagaduría descontó sin equivalencia queda como saldo de la pagaduría por
  aplicar hasta que una persona lo resuelva con soporte.
- **FR-081 · Nómina propia.** Los descuentos de nómina propia los aplica Nómina por los contratos; Recaudo sólo los
  muestra en el recaudo consolidado.
- **FR-082 · Calendario de pagadurías.** Recaudo muestra por pagaduría lo enviado, respondido, girado y pendiente,
  con la fecha legal de giro como aviso informativo.

#### H · Cheques recibidos y canje

- **FR-083 · Recepción.** Banco girador, número, cuenta, girador (persona), fecha y valor; cheque de tercero y
  posfechado sólo si el parámetro lo admite (por defecto: de tercero sí, posfechado no).
- **FR-084 · Canje.** El cheque se incluye en la remesa del día y pasa por recibido, consignado, en canje, pagado
  (por el extracto) o devuelto; los días de canje son el parámetro único que ya usa Captaciones. Hasta R2, el canje
  se registra a mano.
- **FR-085 · Devolución.** Causal del catálogo; reverso por FR-060; lista de giradores con devoluciones para
  advertir al cajero; cargo por devolución configurable, apagado por defecto.
- **FR-086 · Firmeza y canje.** Lo pagado con cheque informa firmeza y canje; Captaciones libera el canje sola;
  Recaudo sólo reversa ante devolución.
- **FR-087 · Informe** de cheques recibidos, en canje y devueltos.

#### I · Canales externos

- **FR-088 · Convenios de recaudo.** Por banco o corresponsal: cuenta recaudadora, estructura de la referencia
  (por producto o por persona, longitud y dígito de verificación configurables), formato del archivo diario como
  dato versionado y comisión informativa.
- **FR-089 · Archivo del convenio.** Carga, validación de estructura y totales, identificación por referencia y
  aplicación no interactiva por lote con un recibo por pago; los no identificados van a partidas.
- **FR-090 · Referencia de pago.** Cada producto (y opcionalmente cada persona) tiene una referencia de pago
  estable que los documentos al asociado pueden imprimir.
- **FR-091 · Pagos en línea.** En R1 y R2 los canales en línea (PSE, pasarelas) entran **sólo por los archivos** que el banco o la pasarela
  entregan, cargados por una persona con revisión previa; no hay botón de pago en línea en esta feature
  (Clarifications, C2 = A).
- **FR-092 · Corresponsales** como convenios de recaudo con su archivo.
- **FR-093 · Conciliación del convenio.** Lo informado contra lo abonado en la cuenta, neto de comisión;
  diferencia explicada con su hecho y, sobre la tolerancia, aprobación.

#### J · Control de efectivo, SARLAFT y GMF

- **FR-094 · Umbrales de efectivo.** Con vigencia y fuente: umbral por operación (sobre el efectivo de un recibo o
  egreso) desde el cual se exige la **declaración de origen de fondos** con soporte; umbral acumulado por persona en
  el período. Un solo parámetro, compartido con Captaciones: la declaración se captura una vez por recibo y viaja a
  cada aplicación que la exija.
- **FR-095 · Fraccionamiento.** Señales configurables (varias operaciones bajo el umbral en pocos días, pagos de
  terceros frecuentes, partidas de un mismo consignante) alertan al oficial de cumplimiento.
- **FR-096 · Reporte de transacciones en efectivo.** Por período, individuales y múltiples, con la estructura
  vigente como dato versionado marcado «sin cotejar» hasta validarla; generar, validar (bloqueantes y alertas),
  regenerar (la anterior queda reemplazada) y marcar cargado con radicado; el plazo legal se informa.
- **FR-097 · Datos para SARLAFT.** Cada operación deja persona, pagador, medio, canal, oficina, montos, declaración
  y alertas para la feature SARLAFT; el reporte de operaciones sospechosas no es de esta feature.
- **FR-098 · Listas restrictivas.** Un pagador tercero se consulta contra las listas de la plataforma; una
  coincidencia alerta al oficial y, según el parámetro, retiene el recibo hasta que él lo libere o sigue (por
  defecto, sigue con alerta).
- **FR-099 · Egresos en efectivo** cuentan para umbrales, alertas y reporte.
- **FR-100 · GMF de las cuentas propias.** Tarifa, exenciones (cuentas marcadas) y hechos generadores con
  vigencia, incluida la tarifa cero; Tesorería estima el GMF al preparar un lote y reconoce el real desde el
  extracto como nota bancaria.
- **FR-101 · GMF de captaciones.** Lo calcula Captaciones; Recaudo lo muestra en el egreso del retiro.

#### K · Tesorería: cuentas bancarias propias y saldos

- **FR-102 · Cuentas bancarias propias.** Banco (con su código de transferencia), número, tipo, oficina,
  propósitos (recaudo, pagos, nómina, convenio, fondo de liquidez), exención de GMF, estado con vigencia, y el
  vínculo **único** con la cuenta contable bancaria del plan que la representa; el vínculo lo usa Contabilidad para
  resolver la matriz; los hechos llevan la cuenta bancaria propia, nunca la contable.
- **FR-103 · Saldos.** Por cuenta: saldo en libros (de Contabilidad), saldo según el último extracto y disponible
  proyectado (menos lotes aprobados no enviados y cheques en tránsito, más remesas en tránsito).
- **FR-104 · Traslados entre cuentas propias** con aprobación por monto, hecho y tránsito hasta el extracto.
- **FR-105 · Remesas.** De bóveda a banco y de banco a bóveda, enlazadas con Recaudo; efectivo en tránsito con
  alerta si no se confirma en los días configurados.
- **FR-106 · Alcance** por cuenta bancaria propia.

#### L · Órdenes de pago y ejecución

- **FR-107 · Bandeja única de órdenes.** Recibe: órdenes de giro de Cartera y Captaciones (las versiones
  adoptadas, con su clase de orden) y sus anulaciones; órdenes de pago de Recaudo (devoluciones); facturas de
  proveedor por pagar de Comercial; cuentas por pagar contables convertidas en órdenes; y órdenes manuales de
  Tesorería por concepto, con aprobación. Cada una con origen, beneficiario, cuenta destino, valor, fecha y forma.
- **FR-108 · Órdenes pagadas por archivo de Cartera.** No entran a lotes de Tesorería; sólo reciben su resultado.
- **FR-109 · Validación.** Una orden sin cuenta destino, sin código de transferencia del banco o con beneficiario
  inexistente queda en pendientes con su motivo.
- **FR-110 · Lote de pago.** Por cuenta de origen y forma (archivo o cheques): selección, vista previa con el
  formato bancario vigente del banco de la cuenta, totales, cantidad y GMF estimado; la cuenta de origen debe ser
  del banco del formato.
- **FR-111 · Aprobación del lote** por monto con los niveles que exija (FR-013); quien prepara no aprueba.
- **FR-112 · Archivo de pago.** Inmutable, guardado como adjunto con huella, con el ámbito de pagos de Tesorería del
  motor de formatos; se descarga, se marca enviado (con quién y cuándo); un archivo no enviado se anula y sus órdenes
  vuelven a la bandeja.
- **FR-113 · Cheques.** Las órdenes de forma cheque se pagan con la chequera de la cuenta (sección P); el cheque se
  entrega con constancia de recibido.
- **FR-114 · Efectivo.** Una orden de giro no se paga en efectivo (su forma no lo admite); el efectivo sale por la
  caja con el contrato (FR-051, FR-053).
- **FR-115 · Anulación de una orden.** Si la orden está en la bandeja o en un lote no enviado, sale y se responde
  «anulada»; si el archivo ya se envió, sigue hasta conocer el resultado.
- **FR-116 · Unicidad.** Una orden en a lo sumo un lote no anulado; el valor girado es exactamente el neto de la
  orden: no hay pagos parciales de una orden de giro.
- **FR-117 · Comprobante de pago** numerado por pago, con soporte al beneficiario por correo con autorización.

#### M · Resultado del giro, rechazo y regiro

- **FR-118 · Resultados.** Por orden: girada, rechazada (causal del banco), cheque no cobrado o anulada; desde la
  respuesta del banco (formato de vuelta), la conciliación o a mano con soporte y permiso; se informa al origen de
  la orden con su clave.
- **FR-119 · Cuándo es «girada».** Parámetro: al marcar enviado o al confirmar el banco; por defecto, al confirmar.
- **FR-120 · Rechazo técnico del archivo.** Un archivo rechazado entero por forma no se informa al origen: se
  marca «rechazado por el banco» y sus órdenes vuelven a la bandeja para otro archivo.
- **FR-121 · Rechazo definitivo y regiro.** Se informa al origen, que decide (reverso, reintegro o pendiente de
  regiro); la orden nueva que emita se enlaza a la anterior y Tesorería muestra la cadena completa.
- **FR-122 · Cheque no cobrado.** Pasados los días configurados desde la entrega, alerta; anulado con permiso, se
  informa «cheque no cobrado».
- **FR-123 · Sin resultado.** Alerta al tesorero a los días configurados.

#### N · Pagos a proveedores y cuentas por pagar

- **FR-124 · Facturas de proveedor.** Las facturas confirmadas por pagar en Comercial llegan como órdenes con
  vencimiento y neto (enmienda FR-166); al pagarse, Comercial recibe el pago.
- **FR-125 · Cuentas por pagar contables.** Se consultan pendientes por tercero y documento cruce en las cuentas que
  Contabilidad marque por pagar (enmienda FR-167) y se convierten en órdenes; el pago lleva tercero y documento
  cruce; admite pago parcial.
- **FR-126 · Programación** por vencimiento con calendario de pagos.
- **FR-127 · Sin facturas propias.** Tesorería no registra facturas ni causa gastos: eso es de Comercial y
  Contabilidad.

#### O · Conciliación bancaria

- **FR-128 · Por cuenta y mes.** Extracto contra lo registrado en la cuenta: recibos de banco, remesas, pagos de
  Tesorería, cheques, dispersiones de Nómina, archivos de desembolsos de Cartera y movimientos manuales de
  Contabilidad (consulta de movimientos de la cuenta, enmienda FR-167).
- **FR-129 · Emparejamiento** automático por reglas (referencia, valor, fecha con días de holgura) y manual uno a
  varios; nada se empareja dos veces.
- **FR-130 · Partidas conciliatorias.** Cheques girados no cobrados, consignaciones y remesas en tránsito, notas
  bancarias (registradas como hechos de Tesorería con aprobación) y abonos no identificados (que pasan a partidas por
  identificar de Recaudo).
- **FR-131 · Cierre.** La conciliación del mes se cierra con su informe (saldo extracto, partidas, saldo libros); se
  reabre con motivo y permiso.
- **FR-132 · Aviso al cierre contable.** Contabilidad avisa al cerrar un mes con conciliaciones abiertas o sesiones
  de caja sin cerrar; informativo por defecto, bloqueante si la cooperativa lo configura.
- **FR-133 · Adquirentes y convenios.** Lo cobrado por lote de datáfono (en Recaudo y en el punto de venta) contra lo
  abonado por la red, con comisión y retenciones de la red como hecho; diferencias con aprobación.

#### P · Chequeras y cheques girados

- **FR-134 · Chequeras.** Por cuenta, rango, estado; los cheques se numeran del rango; un cheque dañado se anula con
  su número.
- **FR-135 · Ciclo del cheque girado.** Emitido, entregado, cobrado (por conciliación), anulado o no cobrado, con
  beneficiario, valor, fecha y orden o pago.
- **FR-136 · Controles.** Cruce y «páguese al primer beneficiario» por defecto configurables; huecos de numeración
  sólo explicados por anulaciones.

#### Q · Liquidez y flujo de caja

- **FR-137 · Posición diaria.** Saldos de bancos, bóvedas y cajas; compromisos (lotes aprobados, órdenes en bandeja,
  cheques en tránsito, dispersiones de nómina); recaudos esperados de las consultas de Cartera y Captaciones.
- **FR-138 · Flujo proyectado** por semanas y meses con categorías configurables, real contra proyectado.
- **FR-139 · Fondo de liquidez.** Las cuentas marcadas informan su saldo a la fecha a Captaciones; las inversiones
  quedan fuera de alcance.

#### R · Integración contable

- **FR-140 · Dos emisores.** Recaudo y Tesorería emiten hechos a Contabilidad con su código de módulo, en la misma
  transacción que su operación, sin cuentas, enlazados al original cuando corrigen.
- **FR-141 · Hechos de Recaudo.** Recibo (por medio, contra el puente de recaudo de cada módulo o el rol del
  concepto), egreso, anulación (espejo), movimiento de caja y reclasificación, diferencia de arqueo, remesa, partida
  por identificar reconocida, identificada o clasificada, cheque devuelto, saldo a favor reconocido y resuelto,
  recaudo de cuenta por cobrar comercial, recaudo pendiente de aplicar y su aplicación.
- **FR-142 · Hechos de Tesorería.** Pago de una orden (contra el puente de giro del origen o la cuenta por pagar),
  resultado rechazado o cheque no cobrado (reverso del pago), traslado, remesa, nota bancaria (GMF, comisión,
  intereses, impuestos), conciliación de adquirentes y convenios, cheque anulado.
- **FR-143 · Matriz por emisor.** Hecho × rol × dimensiones (medio, punto, caja, oficina y sucursal, cuenta bancaria
  propia, concepto, módulo y clase de recaudador de destino, clase de orden) con vigencia; plantilla con filas
  sugeridas sin cuenta; completitud; **validación previa** que bloquea en pantalla por defecto y avisa en procesos;
  modo de paso en línea o por lotes resumidos (por defecto: recibos de caja por lotes resumidos por día, punto y
  medio; lo demás en línea).
- **FR-144 · Puentes en cero.** Lo que Recaudo y Tesorería contabilizan contra los puentes de recaudo, de desembolso
  y de devolución de Cartera y Captaciones los salda; la conciliación de puentes explica cada diferencia hecho por
  hecho.
- **FR-145 · No se reversa desde Comprobantes.** Lo de Recaudo y Tesorería se corrige desde su módulo.
- **FR-146 · Conciliación con Contabilidad.** Caja por oficina, bancos, partidas por identificar, saldos a favor de
  pagadores y puentes, con cero diferencias sin explicar al cierre.

#### S · Participantes, versiones y activación

- **FR-147 · Registro.** Recaudo: recaudador de obligaciones y de cuentas (contratos v1) y destino de sus mensajes
  (pago reversado, consignación reversada, descuentos por pagaduría en las dos versiones); emite la confirmación de
  envío y el resultado de descuentos (dos versiones). Tesorería: destino de giros (orden en sus dos versiones y
  anulación); emite el resultado del giro.
- **FR-148 · Reemplazo de los dobles.** En producción los dobles no se registran; en los ambientes de prueba, el
  registro real exige retirar antes la fila del doble con el mismo código y rol, y la pantalla lo indica.
- **FR-149 · Versiones.** Adoptar una versión es un registro con vigencia; los mensajes conservan su versión; una
  versión no adoptada se rechaza con su motivo.
- **FR-150 · Activación de Recaudo y Tesorería** por cooperativa con fecha. Recaudo exige puntos, cajas, medios,
  tipos de documento y matriz completa; Tesorería, cuentas bancarias vinculadas, formatos y matriz completa. Antes de
  activar no reciben mensajes de negocio (quedan pendientes en la bandeja). Su registro como participantes y su
  activación preceden a la activación de Cartera y de Captaciones en la misma cooperativa.
- **FR-151 · Consumo.** Cada mensaje recibido tiene recibo único; su resultado (procesado o rechazado con motivo) se
  ve en la bandeja con reproceso.

#### T · Informes y documentos

- **FR-152 · Informes de Recaudo** (R1: cierre de sesión y del día, recaudo diario por punto, caja, cajero, medio y
  módulo, anulados con motivo, partidas por antigüedad, saldos a favor por resolver, pagadurías; R2: canales,
  convenios, cheques, consolidado con Nómina y débito automático de sólo lectura).
- **FR-153 · Informes de Tesorería**: órdenes por estado y antigüedad, lotes y archivos, rechazados y pendientes de
  regiro, cheques girados, saldos bancarios, conciliaciones, liquidez y flujo.
- **FR-154 · Exportación** en el centro de informes con su permiso y auditada.
- **FR-155 · Tablero** de caja y liquidez sin datos personales.
- **FR-156 · Documentos** con número y código de verificación: recibo, egreso, anulación, informe de cierre,
  comprobante de pago, conciliación.

#### U · Puesta en marcha y retiro de lo heredado

- **FR-157 · Plantillas** (todo o nada, revisión previa, error por fila y columna): puntos y cajas, medios por punto,
  bóvedas con su efectivo al corte, cuentas bancarias propias con saldo y chequeras, cheques girados en tránsito,
  consignaciones y remesas en tránsito, cheques recibidos en canje, partidas por identificar al corte, convenios.
- **FR-158 · Saldos iniciales sin hechos.** Ya están en los saldos de apertura de Contabilidad; se cuadran por cuenta
  y las diferencias exigen acta.
- **FR-159 · Activación** desde el día siguiente al corte, con el mes abierto.
- **FR-160 · Cifras de referencia** para cotejar totales cargados, sin nombrar al sistema de origen.
- **FR-161 · Lo heredado de Tesorería.** El módulo heredado (cheques, conceptos, facturas y flujo de caja) **se retira entero** con la guarda destructiva
  —diagnóstico de filas en cada ambiente, respaldo, marcador de aprobación y segundo revisor—, y sus rutas y
  pantallas se quitan sin alias; lo reemplazan las funciones de esta feature (Clarifications, C3 = A).
- **FR-162 · Menú.** Recaudo y Tesorería tienen su grupo de menú; todo enlace apunta a una página existente.
- **FR-163 · Dobles de prueba.** Siguen disponibles fuera de producción para las pruebas de Cartera y Captaciones con
  su propio código, sin chocar con los reales (FR-148).

#### V · Enmiendas a otras features

- **FR-164 · E-01 a la 013 (Cartera).** Excepción de la ventana de fecha valor para los pagos que identifican una
  partida bancaria o un giro de pagaduría tardío, citando la partida; convivencia del archivo de desembolsos de
  Cartera con los lotes de Tesorería (D16) y su conciliación.
- **FR-165 · E-02 a la 014 (Captaciones).** La misma excepción de fecha valor; Tesorería no se registra como
  recaudadora por defecto (D2); el umbral de efectivo es único y lo evalúa Recaudo por recibo (FR-094); habilitar la
  cancelación con entrega por el contrato al existir Recaudo.
- **FR-166 · E-03 a la 012 (Comercial).** Mensaje o consulta de facturas de proveedor por pagar y aviso del pago;
  consulta de ventas a crédito que no pasan a Cartera; la conciliación de adquirentes pasa a Tesorería; lo que
  decida C1 sobre la caja.
- **FR-167 · E-04 a la 009 (Contabilidad).** Dos emisores nuevos en el enrutador de hechos con su matriz; consulta de
  pendientes por tercero y documento cruce (cuentas por cobrar comerciales y por pagar); movimientos contables de
  una cuenta bancaria para conciliar; el vínculo cuenta bancaria propia ↔ cuenta contable; el aviso de cierre de
  FR-132.
- **FR-168 · E-05 a la 010 (Nómina).** Los archivos de dispersión de nómina se informan a Tesorería como compromiso y
  para conciliar; Nómina sigue pagando con su archivo.
- **FR-169 · E-06 a la plataforma.** Códigos de participante de Recaudo y Tesorería; ámbitos nuevos del motor de
  formatos (extracto y recaudo bancario en lectura, pagos de Tesorería en escritura —el ámbito de pagos a
  proveedores reservado en la 010—, respuesta de pagos en lectura); nuevos tipos de alerta.

### Key Entities *(include if feature involves data)*

**Recaudo — configuración**
- **Punto de recaudo**: oficina, código, nombre, cajas, medios ofrecidos, estado con vigencia.
- **Caja**: punto, código, base y modo, topes, cajeros, estado.
- **Bóveda**: oficina, efectivo por denominación.
- **Tipo de documento de Recaudo**: recibo por canal, egreso, anulación, movimiento; consecutivo.
- **Concepto vario**: ingreso o egreso, exige tercero o cruce, aprobación.
- **Convenio de recaudo**: banco o corresponsal, cuenta, referencia, formato, comisión.
- **Regla de identificación**: criterio, prioridad, aplicación automática o propuesta.

**Recaudo — operación**
- **Sesión de caja**: caja, cajero, apertura, base, estado, valores sellados, cierre.
- **Arqueo**: por medio, esperado, contado, diferencia, motivo, aprobación.
- **Movimiento de caja**: tipo (abastecimiento, retiro, traslado, reclasificación), valor, medios.
- **Recibo**: número, canal, pagador, sesión (si es de caja), fechas, medios, aplicaciones, declaración, estado
  (vigente o anulado).
- **Medio del recibo**: medio, valor, referencia, datáfono y lote, cheque.
- **Aplicación**: módulo, producto, beneficiario, modalidad, valor aplicado, clave, comprobante del módulo, estado
  (aplicada, reversada por el módulo, anulada).
- **Egreso**: tipo, beneficiario, autorización del módulo, valor, medio, firma.
- **Anulación**: documento, motivo, aprobador, reversos.
- **Saldo a favor de pagador** y **recaudo pendiente de aplicar**: pagador, origen, valor, estado.
- **Movimiento bancario**: cuenta, fecha, valor, referencia, huella, estado (identificado, partida, conciliado).
- **Partida por identificar**: movimiento, antigüedad, identificaciones, clasificación.
- **Archivo de descuentos recibido**, **respuesta de pagaduría** y **giro de pagaduría**: período, versión,
  envío, resultado, líneas cubiertas.
- **Cheque recibido**: girador, banco, número, estado, causal.
- **Declaración de origen de fondos** y **alerta de efectivo**; **reporte de transacciones en efectivo** con
  versión.

**Tesorería**
- **Cuenta bancaria propia**: banco, número, tipo, propósitos, vínculo contable, exención, estado.
- **Chequera** y **cheque girado**.
- **Orden de pago**: origen, clase, beneficiario, cuenta destino, valor, forma, fecha, estado, enlace a la orden que
  reemplaza.
- **Lote de pago** y **archivo de pago**: cuenta de origen, forma, órdenes, aprobaciones, huella, estado.
- **Resultado del giro**: orden, resultado, causal, fecha, referencia.
- **Traslado** y **remesa**: origen, destino, valor, tránsito.
- **Extracto** y **conciliación**: cuenta, mes, emparejamientos, partidas conciliatorias, notas bancarias, estado.
- **Flujo de caja**: categorías, proyecciones, real.

**Comunes**
- **Parámetros** con vigencia y fuente; **hechos** de dos emisores; **reglas de matriz**; **participantes** y
  adopciones; **carga de puesta en marcha**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

**Activación y salida en vivo**
- **SC-001**: Con R1 y T1 registrados en una cooperativa de producción, la activación de Cartera y de Aportes
  muestra cumplidos los requisitos de recaudador y de tesorería, sin ningún doble de prueba registrado.
- **SC-002**: En el ensayo general previo a la salida de COOFLOPAL, el 100 % de los recorridos de la primera semana
  (caja, consignación identificada, pagaduría, retiro, desembolso girado y rechazado con regiro) termina con el
  dinero, los módulos y la contabilidad cuadrados.

**Caja**
- **SC-003**: Un cajero registra un recibo que paga tres productos de una persona, con dos medios, en menos de 2
  minutos desde que identifica a la persona.
- **SC-004**: Una sesión de hasta 200 recibos se arquea y cierra en menos de 10 minutos.
- **SC-005**: El 100 % de las sesiones cerradas tiene diferencia cero o una diferencia con motivo y aprobación; cero
  diferencias sin explicar en el cierre del día.
- **SC-006**: Ningún recibo queda con aplicaciones a medias: en la prueba de fallas inducidas a mitad de la
  confirmación, el 100 % de los recibos tiene todas sus aplicaciones o ninguna.
- **SC-007**: Una operación repetida con la misma clave nunca mueve dinero dos veces (cero duplicados en la prueba
  de reintentos con tres repeticiones por operación).

**Bancos, pagadurías y canales**
- **SC-008**: Al menos el 90 % de los abonos con referencia válida se identifica sin intervención manual.
- **SC-009**: El 100 % de las partidas por identificar tiene antigüedad visible y ninguna supera el plazo sin alerta.
- **SC-010**: La respuesta de una pagaduría de 5.000 líneas se procesa y su giro se aplica en menos de 5 minutos.

**Tesorería**
- **SC-011**: Un lote de 1.000 órdenes genera su archivo en menos de 1 minuto y su total coincide al peso con la
  suma de las órdenes.
- **SC-012**: El 100 % de las órdenes de giro recibidas tiene un resultado informado o una alerta vigente a los días
  configurados.
- **SC-013**: Ninguna orden se paga dos veces (cero órdenes en dos lotes no anulados).

**Contabilidad y conciliación**
- **SC-014**: Al cierre de cada mes, la caja de cada oficina, cada cuenta bancaria, las partidas por identificar y los
  puentes con Cartera y Captaciones concilian con Contabilidad con cero diferencias sin explicar.
- **SC-015**: El 100 % de las operaciones produce exactamente un hecho (o un lote que lo incluye) y ninguno lleva
  cuentas contables.
- **SC-016**: La conciliación bancaria de un mes de una cuenta con hasta 2.000 movimientos se cierra en menos de una
  hora de trabajo, con al menos el 85 % emparejado automáticamente.

**Cumplimiento y seguridad**
- **SC-017**: El 100 % de las operaciones en efectivo sobre el umbral tiene su declaración de origen de fondos, y el
  reporte de transacciones en efectivo del mes cuadra con los recibos y egresos en efectivo del mes.
- **SC-018**: El 100 % de las rutas de Recaudo y Tesorería exige un permiso propio; un usuario fuera de alcance recibe
  la misma respuesta que ante lo inexistente.
- **SC-019**: La verificación de la cadena de auditoría detecta el 100 % de las alteraciones y borrados inducidos.
- **SC-020**: Ningún valor legal aparece fijo en Recaudo ni en Tesorería (verificación automática en verde).

**Puesta en marcha y uso**
- **SC-021**: Los saldos iniciales cargados cuadran al peso con Contabilidad o tienen su acta de aceptación; una
  carga con un error no guarda nada.
- **SC-022**: El 90 % de los cajeros de la cooperativa piloto completa su primer recibo multiproducto y su primer
  cierre sin ayuda.

## Assumptions

- **Para quién es**: toda organización solidaria cliente del ERP; COOFLOPAL es la primera y sale con créditos y
  aportes.
- **Moneda**: pesos colombianos; sin moneda extranjera.
- **Volumen**: hasta 30 puntos y 100 cajas por cooperativa, 10.000 recibos y 2.000 órdenes de pago diarios, 50
  cuentas bancarias.
- **Conectividad**: la caja trabaja en línea; sin modo sin conexión.
- **Hardware**: impresora de tirilla o carta, lector y datáfonos independientes (el datáfono no se integra; el
  cajero registra el lote y la aprobación).
- **Bancos y pasarelas**: se comunican por archivos que carga y descarga una persona; las estructuras son datos
  versionados que aporta el dueño o el banco.
- **Valores legales**: umbrales SARLAFT, GMF, caducidad de cheques y plazos se siembran con su fuente y
  «pendiente de validar».
- **Contratos estables**: el contrato de obligaciones v1, el de cuentas v1, la orden de giro v1 y v2 y los mensajes de
  pagadurías v1 y v2 no cambian durante esta feature; lo nuevo va como enmienda o como versión nueva.
- **Motores de base de datos**: los dos que soporta el ERP, con las mismas pruebas de punta a punta en ambos.
- **Usuarios**: cajeros, supervisores, analistas de recaudo, tesorero, aprobadores, contador, oficial de
  cumplimiento, auditor y revisor fiscal.

### Decisiones por defecto (para revisión del dueño)

Si el dueño no responde, rige la propuesta.

**Alcance y frontera**
- **D1 · Una feature, dos módulos.** Recaudo y Tesorería son módulos distintos con sus permisos, códigos de
  participante y emisores; comparten medios, cuentas bancarias, bóvedas y remesas.
- **D2 · Recaudo es el único recaudador del dinero de las personas.** También el identificado en el extracto y las
  transferencias a cuentas de captación; Tesorería no se registra como recaudadora (conflicto C1; **confirmado por el
  dueño el 2026-10-22**, enmienda la decisión de la 014 del 2026-10-18).
- **D3 · El recibo es todo o nada** con todas sus aplicaciones.
- **D4 · Sin anulación parcial**: se anula el recibo y se emite otro.
- **D5 · Anulación**: el mismo día en la sesión abierta, la pide el cajero y la aprueba otro; después, permiso propio.
- **D26 · Débito automático** sigue en Cartera; Recaudo lo muestra (conflicto C3).
- **D27 · Dispersión de nómina** sigue en Nómina; Tesorería la ve y la concilia.
- **D28 · Tesorería no registra facturas** ni causa gastos; paga lo que Comercial y Contabilidad dejan por pagar.
- **D30 · Medios**: el catálogo común, sin clases nuevas; PSE como transferencia.

**Caja**
- **D6 · Arqueo ciego** activado.
- **D7 · Diferencias**: faltante a cargo del cajero; sobrante a ingresos diversos; ambos con motivo y, sobre la
  tolerancia, aprobación.
- **D8 · Base del día** por defecto; fondo fijo como opción por caja.
- **D9 · Tolerancias** de arqueo las del catálogo de medios (cero por defecto en efectivo).

**Bancos y pagos de terceros**
- **D10 · Partidas por identificar**: alerta a los 90 días; nunca se reclasifican solas.
- **D11 · Consignación reportada en ventanilla**: queda por verificar hasta encontrarla en el extracto.
- **D12 · Excedentes no interactivos**: saldo a favor del pagador en Recaudo.
- **D13 · Saldos a favor de Cartera**: en caja o por orden de pago, a elección del deudor.
- **D14 · Recaudo pendiente de aplicar**: sólo para personas de una carga aplicada sin activar.
- **D15 · Identificación automática**: propone y una persona confirma; aplica sola sólo con referencia exacta y
  dígito válido si el parámetro lo permite.
- **D21 · Cheques recibidos**: de tercero sí, posfechados no; canje manual hasta R2.

**Tesorería**
- **D16 · Archivo de desembolsos de Cartera**: convive; Tesorería no paga esas órdenes y concilia el archivo
  (conflicto C2).
- **D17 · «Girada» al confirmar el banco** (respuesta o extracto), no al enviar.
- **D18 · Segregación**: preparar, aprobar y registrar el resultado son tres personas distintas por defecto.
- **D19 · Aprobación de lotes** por monto con niveles sucesivos; un solo nivel si el monto cabe en la atribución.
- **D20 · Sin efectivo en órdenes**: el efectivo sale por la caja (conflicto C10).
- **D29 · Conciliaciones abiertas al cierre contable**: aviso informativo.

**Cumplimiento e integración**
- **D22 · Umbrales SARLAFT** sembrados con los de referencia, «pendiente de validar» por el oficial; coincidencia en
  listas de un pagador tercero: sigue con alerta.
- **D23 · Reporte de efectivo** en Recaudo; el de operaciones sospechosas, en la feature SARLAFT.
- **D24 · GMF**: el del extracto manda; el configurado estima y advierte.
- **D25 · Matriz**: validación previa que bloquea en pantalla; recibos de caja por lotes resumidos; lo demás en
  línea.
- **D31 · Puesta en marcha** sin hechos contables, con cuadre por cuenta y acta para las diferencias.
- **D32 · Lo heredado de tarjeta débito** no se toca en esta feature (P9).

### Preguntas abiertas para el dueño (no bloquean)

Rigen como parámetro o defecto mientras no se respondan.

**Producto (dueño)**
- **P1 · Bancos de COOFLOPAL**: ¿qué bancos y cuentas usará el primer día y con qué estructuras de extracto, de
  recaudo y de pagos? (El formato de pagos de AV Villas sigue pendiente desde la 010.)
- **P2 · Cheques y convenios**: ¿COOFLOPAL recibe cheques en ventanilla? ¿Tiene convenios de recaudo con referencia o
  corresponsales?
- **P3 · Bóveda**: ¿cada oficina maneja bóveda o la caja principal hace de bóveda?
- **P4 · Niveles de aprobación** por monto de lotes de pago, traslados y retiros en caja.
- **P5 · Pagos a proveedores**: ¿entran en T2 o se espera a que Comercial complete compras?
- **P6 · Pagadurías de COOFLOPAL** y su estructura de respuesta.
- **P9 · Lo heredado de tarjeta débito y convenios con redes**: ¿se retira con la guarda junto con lo de Tesorería?

**Norma y contabilidad (oficial de cumplimiento, contadora y tributarista)**
- **P7 · Umbrales y estructura** del reporte de transacciones en efectivo vigentes en 2026.
- **P8 · Faltantes, sobrantes y partidas**: cuentas, plazo y destino de las partidas no identificadas.
- **P10 · GMF** de los giros de desembolsos y devoluciones (exenciones aplicables a las cuentas de giro).
- **P11 · Fecha valor de las partidas tardías**: confirmar las enmiendas E-01 y E-02.
- **P12 · Matriz de Recaudo y Tesorería**: roles y cuentas sugeridas.

## Dependencias

- **013 Cartera** y **014 Captaciones y aportes**: contratos, mensajes, registro de participantes y requisitos de
  activación que esta feature adopta; enmiendas E-01 y E-02 (fecha valor, convivencia de archivos, recaudadora
  única, umbral de efectivo, cancelación con entrega).
- **012 Comercial**: catálogo de medios, denominaciones, redes, adquirentes y datáfonos, caja del punto de venta
  (según C1), facturas de proveedor por pagar, ventas a crédito sin Cartera, plataforma de integración; enmienda
  E-03.
- **009 Contabilidad**: matriz, enrutador de hechos, validación previa, cuentas bancarias del plan, consultas de
  pendientes y de movimientos por cuenta, guarda de cierre; enmienda E-04.
- **010 Nómina**: motor de archivos planos y bancos con código de transferencia; dispersión de nómina; enmienda E-05.
- **011 Adjuntos**: soportes de origen de fondos, archivos de pago y de respuesta, extractos.
- **008 Personas**: persona maestra, buscador y alta en un paso.
- **SARLAFT** (sin especificar): usa los datos y alertas de esta feature; decide el reporte de operaciones
  sospechosas.
- **Lo que deben validar**: el dueño (C1 a C3, D1 a D32, P1 a P6 y P9), el oficial de cumplimiento (P7), la contadora
  (P8, P11, P12) y el tributarista (P10).

