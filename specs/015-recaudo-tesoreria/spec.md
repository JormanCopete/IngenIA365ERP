# Feature Specification: Recaudo y Tesorería — caja, medios de pago, bancos, pagadurías y giros

**Feature Branch**: `015-recaudo-tesoreria`

**Created**: 2026-10-22

**Status**: Draft — clarificada. El 2026-10-22 el dueño resolvió C1 a C3, confirmó D2 y, tras la revisión adversarial
de cuatro revisores, tomó ocho decisiones más (Clarifications). No quedan marcas de aclaración; lo que depende de
datos que debe aportar el dueño o su equipo está en «Pendientes del dueño», con lo que bloquea la salida marcado.
Cada requisito lleva su entrega (R0, T0, R1, T1, R2, T2; ver «Entregas»). No implementa código.

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
especificaciones y contratos de la 010, 012, 013 y 014, en el código heredado de Tesorería y en el plan de
implantación de COOFLOPAL.

### Por qué ahora

- Cartera (013) y Captaciones y aportes (014) están desplegadas en producción **sin activar** en ninguna
  cooperativa. Su activación en producción exige, en el registro de participantes de cada cooperativa, un
  **recaudador real** que haya adoptado el contrato de obligaciones (y el de cuentas, para captaciones y
  aportes) y un **destino de giros real** que haya adoptado la orden de giro y su anulación. Los dobles de
  prueba no cuentan en producción y, con esta feature, dejan de existir en todos los ambientes (FR-223).
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
mensajes de la 013, contrato de cuentas y mensajes de la 014). Esta feature **adopta la versión 1 sin cambiarla**;
lo que necesita distinto va como enmienda y, cuando cambia lo que cruza un contrato, como **versión 2** de ese
contrato (FR-164 a FR-169).

1. **Recaudo es el recaudador de clase «Recaudo»**: caja, consignaciones, cheques, débito y respuestas de
   pagadurías. Se registra como recaudador de obligaciones y de cuentas, y como destino de los mensajes de
   recaudo. Toda clave que use empieza por su código de módulo.
2. **Las aplicaciones ocurren dentro de la operación de Recaudo**: si Recaudo no confirma su recibo, ningún
   pago ni consignación queda hecho; un fallo técnico se reintenta entero.
3. **Dos formas**: interactiva (caja: el deudor ve una simulación y se aplica con ella; si la distribución
   cambió, se muestra la nueva y se vuelve a confirmar) y no interactiva (bancos, pagadurías, archivos).
4. **Modalidades de pago de Cartera**: cancelación total, vencido, cuota del ciclo, anticipada, abono
   extraordinario a capital con la elección del deudor entre reducir plazo o cuota (Ley 2032; sin elección en
   forma no interactiva rige la preferencia registrada del deudor), cuota específica y valor libre. La
   modalidad es obligatoria en cada pago, también en las formas no interactivas.
5. **Clases de medio admitidas**: efectivo, tarjetas, consignación, transferencia, bono, cheque, otro y
   descuento de pagaduría externa; las clases de crédito se rechazan. Cada ítem de un contrato lleva **una sola
   clase de medio**. La **firmeza** de una cancelación depende de la clase del medio (diez días hábiles por
   defecto; inmediata en los cruces internos).
6. **Fecha valor**: nunca futura en caja; hasta cinco días hacia atrás por defecto (parámetro de Cartera); un
   mes ya cerrado exige permiso con monto. La fecha contable la fija el período abierto. Cartera ya exime de la
   ventana el pago de un giro de pagaduría que cita la línea del archivo de descuentos; Captaciones todavía no.
7. **Lo que vuelve a Recaudo**: el **excedente** de un pago (lo que supera lo exigible) nunca entra a Cartera;
   un saldo a favor o un anticipo se **devuelve por el recaudador** o se aplica a otra obligación del mismo
   deudor.
8. **Reversos**: Recaudo reversa sólo lo suyo, por la clave con que aplicó. Cuando Cartera o Captaciones
   reversan desde su pantalla un pago o una consignación de Recaudo, le avisan, y Recaudo registra la aplicación
   reversada y resuelve el dinero según la causa (FR-059, FR-195). Desde la pantalla de Captaciones no se puede
   dejar una cuenta en sobregiro; por eso el cheque devuelto se registra siempre en Recaudo, que sí puede dejar el
   sobregiro técnico con bloqueo (FR-060).
9. **Retiros**: un retiro de ahorro en efectivo lo **autoriza Captaciones** (firmas, disponible, montos,
   atribución, GMF) y lo **entrega Recaudo**; la cancelación con entrega espera a Recaudo.
10. **Efectivo y origen de fondos**: Captaciones exige, con su propio parámetro, la declaración de origen de
    fondos en una consignación en efectivo sobre su umbral, y un aprobador presente sobre la atribución del
    usuario; lo informa en su simulación.
11. **Canje**: un medio con días de disponibilidad (cheque, tres días hábiles por defecto, parámetro de
    Captaciones) deja el valor por liberar; Captaciones lo libera sola y Recaudo puede reversarlo antes.
12. **Antes de activar**: los contratos responden «no habilitado» sin efectos. Captaciones informa qué titulares
    están en una carga aplicada sin activar; Cartera no lo informa (enmienda FR-164).
13. **Pagadurías (libranza)**: Cartera envía a Recaudo el archivo de descuentos por pagaduría (con créditos y,
    desde la 014, cuotas de aportes y ahorros por grupos de archivo y conceptos equivalentes); Recaudo confirma
    el envío, procesa la respuesta de la pagaduría con el formato de vuelta del convenio, devuelve el resultado
    y, cuando la pagaduría gira, aplica cada línea descontada por los dos contratos citando la línea.
14. **Órdenes de giro**: Cartera (desembolsos) y Captaciones (retiros por transferencia, devoluciones del
    retiro del asociado, devoluciones de aportes, entregas por fallecimiento, intereses y redención de CDAT,
    vencimiento del contractual, no reclamados, consignaciones a juzgados, excedentes pagados) emiten órdenes
    con beneficiario, cuenta destino, valor neto, fecha y forma (transferencia, cheque o pago a tercero), y su
    anulación. Tesorería responde **un resultado por orden**: girada, rechazada, cheque no cobrado o anulada, con
    transiciones fijas: desde «girada» sólo cabe «cheque no cobrado» (y sólo en la forma cheque); «anulada» sólo
    responde a una anulación del origen; el resultado lo informa una persona, nunca un proceso. Rechazada,
    anulada o no cobrada, el origen reversa o deja la orden «pendiente de regiro» y emite una orden nueva enlazada
    (regiro). Una orden que sale en un **archivo de desembolsos de la propia Cartera** no se paga por otra vía:
    Tesorería sólo informa su resultado.
15. **Un consumidor por destino**: la bandeja admite un solo consumidor por destino de mensajes; si hay dos, el
    arranque falla. Los dobles de prueba consumen hoy los destinos de recaudo y de giros.

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
- **Lo usa la implantación de COOFLOPAL**: el plan parametriza allí conceptos, bancos y cheques, capacita en sus
  cheques, cuentas por pagar y flujo de caja, los usa en el paralelo y carga sus facturas pendientes al 30/11. Por
  eso convive con esta feature hasta T2 (decisión del dueño del 2026-10-22, FR-161).

Aparte existe otro traslado heredado de **tarjeta débito y convenios con redes** (siete tablas de tarjetas,
transacciones, datáfonos y parámetros de convenio). No lo pide esta feature; queda como pendiente (P9).

### La norma

Resumen; los valores se siembran con su fuente y «pendiente de validar» (FR-009). Las citas sin verificar en la
fuente van marcadas «a validar».
- **SARLAFT** (Superintendencia de la Economía Solidaria, Circular Básica Jurídica, título V de prevención del
  lavado de activos, Anexo 2 del reporte de transacciones): conocimiento del asociado y del origen de los fondos,
  control de transacciones en efectivo, reporte periódico a la UIAF de transacciones individuales y múltiples y
  reporte de operaciones sospechosas que decide el oficial. Los umbrales del sector solidario **no son los del
  sector bancario**: un proyecto de circular de la Supersolidaria de diciembre de 2024 fija, para 2024–2025,
  **5.000.000 de pesos por transacción individual y 30.000.000 en transacciones múltiples** del mes (el cronograma
  original preveía 2.000.000 y 20.000.000 para 2025) y **suspendía la aplicación del reporte durante 2025**; una
  respuesta de la UIAF de 2025 confirma 5 y 30 millones según su resumen. El alcance del anexo («transacciones de
  clientes») puede no limitarse al efectivo. Todo eso se siembra como parámetro con vigencia, «pendiente de
  validar» por el oficial de cumplimiento (P7). La gestión SARLAFT completa es de su propia feature; ésta deja los
  datos, las alertas, los bloqueos por listas y el reporte de transacciones.
- **Listas vinculantes** (Ley 1121 de 2006, art. 20, a validar): una coincidencia con las listas del Consejo de
  Seguridad de las Naciones Unidas exige no ejecutar la operación y reportar de inmediato a la UIAF y a la
  Fiscalía. Las demás listas son de gestión de riesgo y su efecto es parametrizable. La 014 ya bloquea la apertura
  de un producto ante coincidencia y sólo la libera el oficial.
- **Fondo de liquidez** (Decreto 1068 de 2015, arts. 2.11.7.2.1 a 2.11.7.2.4, texto del Decreto 961 de 2018): se
  mantiene constante y permanente, libre de gravamen; sólo disminuye para atender a depositantes o por reducción
  de los depósitos; su uso exige aviso previo a la Supersolidaria y se informa cada mes con extractos validados
  por el revisor fiscal. La 014 cita además el Decreto 976 de 2026.
- **GMF** (Estatuto Tributario, art. 871 y siguientes): cuatro por mil sobre las disposiciones de recursos de
  cuentas, con exenciones taxativas (art. 879). El banco lo cobra sobre las cuentas propias de la cooperativa; la
  cooperativa vigilada lo practica como agente sobre los retiros de sus captaciones (eso ya lo hace Captaciones);
  y **los débitos a cuentas contables** para pagar o transferir a un tercero también lo causan (Decreto 449 de
  2003, art. 3, hoy en el DUR 1625 de 2016, a validar). La exención de los desembolsos de crédito es **por
  operación**: abono en cuenta del deudor o cheque de cruce y negociabilidad restringida; a terceros, sólo para los
  destinos que la norma admite (art. 879 num. 11; Consejo de Estado, concepto 2429 de 2020, a validar). El
  Gobierno estudia modificarlo: tarifa, exenciones y hechos generadores son configurables con vigencia, incluida
  la tarifa cero.
- **Cheques** (Código de Comercio): pagadero a la vista, la posfecha se tiene por no puesta (art. 721);
  presentación en quince días en la misma plaza y un mes en otra (art. 718); 20 % de sanción a cargo del librador
  cuando no se paga por causa imputable a él (art. 731); canje por la cámara de compensación y causales de
  devolución.
- **Libranza** (Ley 1527 de 2012, art. 6, a validar): la pagaduría gira lo descontado dentro de los tres días
  hábiles siguientes al pago al asalariado y responde solidariamente si no lo hace; los efectos sobre la mora del
  deudor ya los resuelve Cartera.
- **Prepago** (Ley 2032 de 2020): el deudor elige si el abono reduce plazo o cuota; lo pregunta Recaudo, lo aplica
  Cartera y el recibo guarda la elección como prueba.
- **Faltantes de caja** (Código Sustantivo del Trabajo, arts. 59 y 149): no se descuenta del salario sin
  autorización previa escrita del trabajador para cada caso o sin orden judicial.
- **Conservación** (Código de Comercio, art. 60, a validar): libros y papeles del comerciante por diez años.
- **Habeas Data** (Leyes 1266 de 2008 y 1581 de 2012): la información financiera de una persona circula sólo
  con su autorización o para la finalidad informada.
- **Factura** (ET arts. 615 y 616-1, Resolución DIAN 165 de 2023): un ingreso gravado (un servicio) exige factura
  electrónica, que emite Comercial.
- **Exógena**: saldos de cuentas bancarias al 31 de diciembre por NIT del banco (formato 1012) y pagos y
  retenciones por tercero, que salen de Contabilidad.

### El calendario

Hitos que fija la salida de COOFLOPAL (plan de implantación) y que esta especificación adopta:
- **Paralelo del 11/11 al 27/11 en QA**, sobre una copia de la parametrización de COOFLOPAL, con la versión de
  R0/T0 (y lo que haya de R1/T1) disponible en cada momento; ahí se registran los recaudos de cuotas, aportes y
  nómina del paralelo. **Producción queda limpia** para la carga definitiva (FR-226).
- **Ensayo general del 23/11 al 25/11 en QA**, con R0 y T0 completas; es condición del go/no-go (FR-227, SC-002).
- **R0 y T0 en producción antes del 28/11**, día de la carga definitiva; **salida en vivo el 01/12**.
- **Cuadre de apertura del 02/12 al 04/12**: la activación puede hacerse con cuadre provisional y fecha límite; el
  cierre contable de diciembre no se hace sin el cuadre definitivo (FR-228).
- **Primer reporte de transacciones** a la UIAF en enero de 2027 (si la suspensión no se prorroga): el generador
  del reporte (R1) debe estar en producción antes de su vencimiento.
- R1 y T1 siguen a la salida, sin fecha fija; R2 y T2, después. El retiro de lo heredado de Tesorería ocurre
  **después de T2**.
- Las fechas de construcción las propone el plan.

## Clarifications

### Session 2026-10-22

- Q: C1 · ¿La caja de Recaudo y la caja del punto de venta de la 012 son una sola o dos? → A: **A — dos cajas
  separadas que comparten catálogo de medios, denominaciones y método de arqueo** (FR-021).
- Q: C2 · ¿Qué canales en línea funcionan el primer día? → A: **A — sólo por archivos del banco o la pasarela
  cargados por una persona**; sin botón de pago en línea en esta feature (FR-091).
- Q: C3 · ¿Qué se hace con el módulo de Tesorería heredado? → A: **A — retirarlo entero con la guarda destructiva**
  (FR-161); el momento lo fija la decisión sobre la Tesorería heredada, abajo.
- Q: ¿Quién recauda las transferencias recibidas en cuentas de captación, que la 014 había asignado a Tesorería
  (decisión del 2026-10-18)? → A: **Recaudo, como todo ingreso de una persona** (D2); Tesorería no se registra como
  recaudadora. Enmienda la decisión de la 014 sin cambiar ningún contrato.
- Q: ¿Cuándo se retira la Tesorería heredada, si el plan de COOFLOPAL la usa para cuentas por pagar y cheques de
  proveedores? → A: **Después de T2.** COOFLOPAL sale con la heredada para cuentas por pagar y cheques de
  proveedores; se retira con la guarda (C3) cuando T2 traiga pagos a proveedores y conciliación completa, migrando
  antes sus pendientes (FR-161).
- Q: ¿Dónde corre el paralelo de COOFLOPAL del 11/11 al 27/11? → A: **En QA, con una copia de la parametrización
  de COOFLOPAL** y la versión disponible de R0/T0 y lo que haya de R1/T1; producción queda limpia para la carga definitiva del 28/11
  (FR-226).
- Q: ¿Qué pasa con los dobles de prueba TST y TES cuando existan Recaudo y Tesorería reales? → A: **Se retiran.**
  Las pruebas de punta a punta de Cartera y Captaciones migran a los módulos reales (un solo consumidor por
  destino) y, fuera de producción, los dobles dejan de valer para la activación (FR-148, FR-163, FR-223, FR-224).
- Q: ¿Qué tanto de cheques entra el primer día? → A: **Un mínimo completo.** La devolución de cheques recibidos,
  con su reverso por los contratos, sube a R1 (y entra en el recorte R0); una chequera mínima —rango, numeración,
  anulación y estados— sube a T1 (y entra en el recorte T0) (FR-060, FR-219).
- Q: ¿Cómo se cobran los servicios de COOFLOPAL (funerario, odontología y similares)? → A: **Son cuentas por
  cobrar de Comercial**: una venta con su tercero. Si se pagan a plazos, son una venta a crédito que financia
  Cartera, y Recaudo cobra esa obligación por el contrato; no se crea un tipo de deuda nuevo (FR-049, US23).
- Q: ¿Qué conciliación bancaria hay el primer mes? → A: **Una conciliación mínima en T1** (en el recorte T0):
  carga del extracto (archivo o digitado), cruce automático por valor, fecha y referencia, marca manual de las
  partidas sin cruce e informe de partidas conciliatorias. Las reglas avanzadas y los extractos de varios bancos
  quedan en T2 (FR-216, US25).
- Q: ¿Se puede activar sin el cuadre definitivo contra Contabilidad, que llega del 02/12 al 04/12? → A: **Sí, con
  cuadre provisional** aprobado por una persona con permiso, con motivo y fecha límite; mientras siga pendiente hay
  alerta diaria, y **el cierre de diciembre no se hace sin el cuadre definitivo** (FR-228).
- Q: ¿Qué se construye para el 01/12? → A: **Las entregas R0 y T0 mínimas**, con lo indispensable para activar
  COOFLOPAL: caja con efectivo y transferencia; archivo de pagaduría; giros por transferencia y cheque mínimo;
  devolución de cheque; conciliación mínima; SARLAFT básico. El resto de R1 y T1 sigue después; cada requisito
  lleva su entrega («Entregas»).

### Session 2026-10-23

Decisiones del dueño sobre los hallazgos del `/speckit-analyze` del 2026-10-23.

- Q: CO1 · ¿Cómo cumplen el Principio X las corridas no interactivas (extractos, giros de pagaduría), que aplican miles
  de líneas sin un evento por recibo? → A: **Un evento resumen por corrida que lleva la lista de cada recibo aplicado
  (su `PublicId`) con su huella, verificable recibo por recibo**, escrito por `COR_AuditOutbox` en la cadena de sellos,
  más uno por línea rechazada. Con eso el principio se cumple sin desviación (FR-014, I-3).
- Q: I3 · ¿Hasta cuándo se regeneran las migraciones de R0/T0? → A: **Se congelan al desplegarse en QA el 10/11**
  (hito del bloque 3): desde ahí no se regenera nada y cada cambio posterior de R0/T0 es una migración nueva, aditiva y
  en par. Antes del 10/11 sí se regeneran (la base de DEV del 04/11 se recrea).
- Q: I7 · ¿Conviven la conciliación bancaria de Tesorería y la de Contabilidad (009, FR-055 a FR-060)? → A: **La de
  Tesorería reemplaza a la de Contabilidad** (enmienda E-04 a la 009): un solo extracto y una sola conciliación por
  cuenta, en Tesorería. Las tablas `ACC_BankReconciliations`, `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps`
  quedan sin uso (no se borran en esta feature; su retiro, si se hace, es una migración destructiva aparte, pendiente) y
  la reapertura de un período contable mira la conciliación de Tesorería por un puerto (FR-167, FR-216).
- Q: CO3 · ¿Qué prefijo llevan las tablas nuevas, si la constitución sólo lista `TRS_` para Tesorería? → A: **`RCD_`
  (Recaudo) y `TES_` (Tesorería nueva), por enmienda de la constitución** (Estándares Técnicos Adicionales, v2.0.1 del
  2026-10-23, revisor el dueño); `TRS_` queda para lo heredado hasta su retiro.
- Q: CO2 · ¿Se aprueba que Recaudo llame directo a los contratos de Cartera y Captaciones y a los puertos entre los dos
  módulos dentro de su transacción (Principio III)? → A: **Sí, se aprueba formalmente la desviación**, como T101 de la
  013: cada puerto ejecuta su validador y escribe su evento. Queda en Complexity Tracking del plan y en la descripción
  del PR.

### Opciones que se presentaron (2026-10-22)

Las opciones con que se presentaron las tres primeras decisiones; el dueño eligió la recomendada en las tres
(arriba). Las ocho de la revisión adversarial se presentaron como propuesta única y el dueño las ajustó.

- **C1 · La caja de Recaudo y la caja del punto de venta (FR-021).** Opciones: (A) dos cajas separadas que
  comparten el catálogo de medios, las denominaciones y el método de arqueo (**recomendada**: no toca la 012 en
  producción); (B) una sola sesión de caja, de Recaudo, que el punto de venta usa para cobrar sus ventas;
  (C) separadas, con una «caja mixta» opcional por punto que recauda y vende en la misma sesión.
- **C2 · Canales en línea del primer día (FR-091).** Opciones: (A) sólo por archivos —extracto, archivo de
  recaudo del convenio, reporte de la pasarela— cargados por una persona (**recomendada**); (B) además, botón de
  pago en línea con una pasarela (PSE) para que el asociado pague desde un enlace; (C) A primero y B en una entrega
  posterior con la pasarela que el dueño elija.
- **C3 · El módulo de Tesorería heredado (FR-161).** Opciones: (A) retirarlo entero con la guarda destructiva
  y reemplazarlo por esta feature (**recomendada**); (B) conservar sus datos como consulta de sólo lectura y
  retirar rutas y pantallas; (C) migrar sus cheques pendientes y facturas por pagar a la puesta en marcha y
  después retirarlo. Con la decisión sobre el momento (después de T2), A incorpora la migración de pendientes
  de C.

## Conflictos con la constitución y con decisiones vigentes

La especificación no modifica ningún principio de la constitución. La única enmienda que pide es de los Estándares
Técnicos Adicionales: los prefijos de tabla `RCD_` y `TES_` (v2.0.1, 2026-10-23; Clarifications 2026-10-23).

### Con la constitución

- **I-1 · Principio XI.** Se lee extendido a recibos, egresos, sesiones cerradas, archivos de giro y
  conciliaciones cerradas: no se editan ni se borran; se corrigen con un documento nuevo enlazado.
- **I-2 · Principio V.** Pagadores, beneficiarios, quienes efectúan la transacción, girados de cheques,
  pagadurías, bancos-tercero y cajeros son personas del maestro. Una consignación sin identificar no tiene persona
  hasta que se identifica.
- **I-3 · Principio X.** Toda operación monetaria (recibo, egreso, giro, traslado, anulación) queda en la
  auditoría con cadena de sellos; la declaración de origen de fondos y las alertas de efectivo son datos de la
  operación. La conservación de documentos es un parámetro que nunca baja del mínimo de cinco años del principio
  (FR-178). Las corridas no interactivas (extractos, giros de pagaduría) dejan un evento resumen por corrida con la
  lista de cada recibo aplicado (su `PublicId` y su huella), verificable recibo por recibo en la cadena de sellos, y uno
  por línea rechazada (decisión del dueño del 2026-10-23).

| Principio | Cómo lo cumple |
|---|---|
| I. Spec-first | Esta especificación antes de plan, tareas y código |
| II. Clean Architecture | Reglas de arqueo, imputación de medios, identificación, conciliación y armado de lotes como cálculos puros; los demás módulos sólo por sus contratos (FR-003) |
| III. CQRS | Toda operación es comando o consulta con su validador |
| IV. Base por cooperativa | Puntos, cajas, cuentas bancarias, recibos y giros viven en la base de cada cooperativa; procesos por cooperativa (FR-019) |
| V. Persona maestra | I-2 |
| VI. PublicId | Recibos, egresos, órdenes, archivos y partidas por identificador público y número visible (FR-017) |
| VII. Borrado lógico | Catálogos se inactivan; los documentos nunca se borran (FR-002) |
| VIII. Validación dual | Mensajes en español con código por espacio (FR-015) |
| IX. Errores visibles | Rechazos de contratos por ítem, fallas de consumo en la bandeja, alertas de proceso (FR-015) |
| X. SARLAFT | I-3, sección J |
| XI. Inmutabilidad | I-1, FR-002 |
| XII. Migraciones | Retiro de lo heredado después de T2, con diagnóstico, migración de pendientes, respaldo, segundo revisor y declarado irreversible (FR-161); retiro de las filas de los dobles en los ambientes de prueba (FR-223) |

### Con decisiones vigentes de otras features

- **C1 · 014, Tesorería como recaudadora.** La 014 previó (y construyó con el doble de prueba) que Tesorería
  consigne en cuentas de captación las transferencias recibidas, con su propia clase de recaudador y su prefijo
  de clave, y que adopte también el contrato de obligaciones. **Decidido (D2, 2026-10-22)**: todo dinero que entra
  de una persona entra por Recaudo, también el identificado en el extracto; Tesorería no se registra como
  recaudadora. La clase de recaudador «Tesorería» y los destinos que la nombran se **conservan sin uso** en los
  catálogos de la 013 y la 014 (retirarlos cambiaría un contrato); ningún contrato cambia por esto.
- **C2 · 013, archivo de desembolsos de Cartera.** Cartera puede generar su propio archivo de transferencias
  de desembolsos y marcar sus órdenes como «pagadas por archivo». Habría dos lugares que generan archivos de
  pago. **Propuesta (D16)**: convivencia —Tesorería no paga esas órdenes, sólo informa su resultado y concilia el
  archivo— y enmienda para que Cartera informe el archivo por un mensaje (hoy su referencia no viaja) y, a futuro,
  todo archivo de pagos salga de Tesorería (FR-164).
- **C3 · 014, débito automático.** El débito automático de cuotas de crédito desde una cuenta de ahorro ya es
  una **operación interna** de Cartera que corre en su ciclo diario. Recaudo no lo duplica: lo muestra en el
  recaudo consolidado (D26).
- **C4 · 012, caja del punto de venta.** Decidido: dos cajas separadas (C1 de Clarifications, FR-021). El efectivo
  que reciben las dos cajas alimenta un solo registro de transacciones en efectivo (FR-204, enmienda FR-166).
- **C5 · 014, «chequeras y canje son de Recaudo».** La 014 llamó así a lo heredado de ahorros. Aquí se separa:
  el **canje** de los cheques recibidos es de Recaudo; las **chequeras** de las cuentas bancarias propias y los
  cheques girados son de Tesorería. La caja de Recaudo entrega sólo efectivo: un pago en cheque de la cooperativa
  sale por orden de pago a Tesorería. No hay chequeras de asociados (sin cuenta corriente, D4 de la 014).
- **C6 · 013, servicios a asociados.** Decidido (2026-10-22): los servicios son cuentas por cobrar de Comercial
  —una venta con su tercero—; a plazos, una venta a crédito que financia Cartera, que Recaudo cobra por el contrato
  de obligaciones. Lo vendido a crédito sin pasar a Cartera se cobra con una consulta de pendientes y un hecho con
  tercero y documento cruce (FR-049, enmiendas FR-166 y FR-167); no se cobra como concepto vario.
- **C7 · 013 y 014, dobles de prueba.** El doble de tesorería usa el código «TES» como destino y como recaudador,
  el mismo que toma la Tesorería real, y el doble recaudador «TST» consume los destinos de recaudo; la bandeja admite
  un solo consumidor por destino. **Decidido (2026-10-22)**: los dobles se retiran en todos los ambientes y las
  pruebas de Cartera y Captaciones migran a los módulos reales (FR-223, FR-224).
- **C8 · 013 y 014, ventana de fecha valor.** Una partida bancaria identificada semanas después no cabe en los
  cinco días de fecha valor del contrato; aplicarla con la fecha de identificación castiga al deudor con mora que
  no tuvo. Cartera ya exime el giro de pagaduría que cita su línea; Captaciones no. Se pide la excepción de
  partidas a las dos (versión 2 de cada contrato) y la de pagaduría sólo a Captaciones (enmiendas FR-164 y
  FR-165).
- **C9 · 014, umbral de efectivo.** Captaciones exige la declaración de origen de fondos con su propio parámetro y
  lo informa en su simulación. **Propuesta**: cada módulo conserva su parámetro; Recaudo tiene los suyos (cuatro,
  separados, FR-202), captura la declaración **una vez por recibo** cuando su umbral o cualquier aplicación la
  exija, y la hace viajar a cada aplicación (FR-094, FR-165).
- **C10 · 014, orden de giro sin forma «efectivo».** El dueño pide girar «por archivo, cheque o efectivo»; la
  orden de giro sólo admite transferencia, cheque o pago a tercero. **Propuesta (D20)**: el efectivo sale por la
  caja con el contrato (retiro, devolución de saldo a favor); la devolución de aportes al retiro sale por orden de
  giro (FR-193); una orden en efectivo exigiría una versión nueva de la orden.
- **C11 · 012, conciliación con adquirentes.** La 012 dejó la conciliación de lo que abonan las redes de
  tarjetas (comisiones y retenciones) para Tesorería; se recoge en FR-133, con los lotes del punto de venta por la
  enmienda FR-166.
- **C12 · 013, requisitos de activación fuera de producción.** La 013 decidió (2026-10-18) que fuera de producción
  sólo cuentan como recaudador y tesorería los dobles. **Decidido (2026-10-22)**: fuera de producción ya no valen
  los dobles; cuentan Recaudo y Tesorería reales, como en producción (FR-223, enmiendas FR-164 y FR-165).
- **C13 · Plan de implantación de COOFLOPAL.** El plan usa la Tesorería heredada para cuentas por pagar y
  cheques de proveedores, corre el paralelo desde el 11/11 y cuadra la apertura después del 01/12. **Decidido**:
  la heredada convive hasta T2, el paralelo corre en QA y la activación admite cuadre provisional (Clarifications).
- **C14 · 009, conciliación bancaria de Contabilidad.** La 009 especificó su propia conciliación (FR-055 a FR-060:
  extracto, mapeo de columnas, cruce, partidas, cierre y reapertura) sobre `ACC_BankReconciliations`,
  `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps`, y la reapertura de un período las marca desactualizadas.
  Habría dos extractos y dos conciliaciones por cuenta. **Decidido (2026-10-23)**: la de Tesorería **reemplaza** a la
  de Contabilidad (enmienda E-04, FR-167, FR-216); las tres tablas quedan sin uso y la reapertura del período mira la
  conciliación de Tesorería por un puerto.

## Alcance

### Dentro de esta feature

**Recaudo**
- Puntos de recaudo por oficina, cajas, cajeros y bóveda de oficina (o caja principal como bóveda).
- Sesiones de caja con base, movimientos de caja, arqueo y cierre por medio de pago, diferencias aprobadas y
  cierre del día por punto; contingencia por talonario.
- El **recibo de caja** único, numerado e inmutable, **multiproducto** (créditos, cuotas de aportes y ahorros,
  consignaciones a cuentas, ventas a crédito, cuentas por cobrar comerciales, conceptos varios) con cualquier
  combinación de medios imputados a sus aplicaciones.
- Egresos de caja en efectivo: retiros de ahorro autorizados por Captaciones, devolución de saldos a favor y
  excedentes.
- Anulación de recibos y egresos, reversos y avisos de reverso de los módulos, cheque devuelto.
- Consignaciones y transferencias recibidas: extracto y archivos de recaudo (o captura manual), identificación por
  referencia, partidas por identificar.
- Pagadurías y libranza: archivo de descuentos, confirmación de envío, respuesta, giro, no aplicados.
- Cheques recibidos, canje y devolución.
- Convenios de recaudo con referencia (bancos y corresponsales).
- Control de efectivo y SARLAFT: umbrales, origen de fondos, quien efectúa, listas, fraccionamiento, alertas y
  reporte de transacciones.

**Tesorería**
- Cuentas bancarias propias vinculadas a su cuenta contable, firmantes, chequeras, saldos y fondo de liquidez
  restringido.
- Bandeja única de órdenes de pago (Cartera, Captaciones, Recaudo, Comercial, Contabilidad y manuales).
- Lotes y archivos de pago con el motor de formatos bancarios (o el portal del banco), cheques, aprobación
  multinivel por monto y controles antifraude.
- Resultado del giro (girada, rechazada, cheque no cobrado, anulada) y cadena de regiros.
- Pagos a proveedores y de cuentas por pagar contables; traslados entre cuentas propias y remesas de efectivo.
- Conciliación bancaria (mínima y completa), notas bancarias (GMF, comisiones, intereses) y conciliación de
  adquirentes y convenios.
- Posición de liquidez y flujo de caja proyectado.

**Común**
- Hechos a Contabilidad por matriz (dos emisores nuevos), conciliación con Contabilidad.
- Registro como participantes, adopción de contratos y mensajes, requisitos de activación de Cartera y
  Captaciones, y retiro de los dobles de prueba con la migración de sus pruebas.
- Permisos, roles sembrados, alcance, segregación, aprobaciones, auditoría, informes y documentos.
- Salida en vivo: paralelo en QA, ensayo, go/no-go, puesta en marcha con plantillas y cuadre (provisional o
  definitivo); retiro de lo heredado de Tesorería después de T2.
- Enmiendas a la 009, 010, 012, 013 y 014.

### Fuera de esta feature

- **Facturación electrónica**: es de Comercial (la factura de un cargo o un servicio la emite Comercial).
- **Contabilidad general** y **nómina en sí**; la dispersión de la nómina sigue en Nómina (010).
- **La gestión SARLAFT** (segmentación, monitoreo integral, reporte de operaciones sospechosas, listas
  propias): su feature. Ésta deja datos, alertas, los bloqueos por listas y el reporte de transacciones.
- **Caja menor** (fondo fijo de gastos de oficina con reembolso), **inversiones** y **fondo de liquidez como
  portafolio**: Tesorería sólo marca y restringe las cuentas bancarias que lo constituyen (FR-139, FR-207).
- **Moneda extranjera**, cuentas en el exterior y diferencia en cambio.
- **Caja sin conexión** (más allá de la contingencia por talonario, FR-186), cajeros automáticos, tarjeta débito
  propia y banca electrónica para el asociado (portal o aplicación del asociado).
- **Integraciones en línea con bancos** (servicios web de pagos o saldos) y **botón de pago en línea**: todo es por
  archivos o por el portal del banco (C2 = A).
- **Débito automático**: ya es de Cartera (C3).
- **Crédito con tarjeta** emitida por la cooperativa y cupos rotativos: son de Cartera.

### Entregas

R0 y T0 son el **recorte mínimo** para activar COOFLOPAL el 01/12 (decisión del dueño del 2026-10-22); R1 y T1
completan lo que antes se llamaba «para activar»; R2 y T2 amplían. Cada FR lleva su entrega; un FR con dos marcas
entra por partes (la primera es la mínima). Una entrega incluye todo lo de las anteriores de su módulo.

| Entrega | Contenido | Historias |
|---|---|---|
| **R0 · Recaudo mínimo para el 01/12** | Puntos, cajas y cajeros; bóveda o caja principal como bóveda; sesión con arqueo, cierre, diferencias y contingencia por talonario; recibo multiproducto por los dos contratos con **efectivo y transferencia** (más cheque recibido si el parámetro lo admite, con canje manual); anulación y avisos de reverso de los módulos; **devolución de cheque** con reverso por los contratos; transferencias y consignaciones recibidas con extracto cargado o digitado, identificación manual y partidas por identificar; **archivo de pagaduría** (descuentos, envío, respuesta, giro, no aplicados); **SARLAFT básico** (umbrales con vigencia, declaración de origen de fondos, quien efectúa, listas vinculantes que bloquean, alertas de acumulado y fraccionamiento); hechos y matriz de Recaudo; registro como recaudador y destino; retiro de los dobles; seguridad y roles sembrados; puesta en marcha con cuadre provisional; informes de cierre y de recaudo por producto | US1 (parcial), US2, US3 (parcial), US5, US6 (parcial), US7, US8 (Recaudo), US9, US12, US13, US14 (parcial), US15 (parcial), US24 |
| **T0 · Tesorería mínima para el 01/12** | Cuentas bancarias propias con fondo de liquidez restringido; bandeja de órdenes de giro de Cartera y Captaciones y de pago de Recaudo; lotes por **transferencia** (archivo o portal del banco) y **cheque mínimo** (chequera con rango, numeración, anulación y estados); aprobación por monto; resultado del giro y regiro; controles de cuenta destino y de archivo alterado; listas vinculantes en beneficiarios; GMF en egresos por cuentas contables y exención por operación; remesas de efectivo; **conciliación mínima**; hechos y matriz de Tesorería; registro como destino de giros; puesta en marcha de bancos | US8 (Tesorería), US9, US10, US11, US12, US13, US19 (parcial), US25, US26 (parcial) |
| **R1 · Recaudo completo** | Resto de medios (tarjetas, bono, otros), pagar todo lo exigible en una acción, codeudor; egresos de caja en efectivo (retiros, saldos a favor); identificación automática por reglas; consignación reportada en ventanilla; recaudo pendiente de aplicar; enmiendas de fecha valor (E-01 y E-02 v2); cuentas por cobrar comerciales; registro único de efectivo con el punto de venta y **generador del reporte de transacciones** (antes del primer vencimiento); calendario legal de pagadurías; consolidado con Nómina y débito automático; avisos por correo; informes y tablero | US1, US3, US4, US6, US7 (calendario), US14, US21 (Recaudo), US23 |
| **T1 · Tesorería completa** | Órdenes manuales con controles; traslados entre cuentas propias; remesa del efectivo del punto de venta; firmantes y tokens; comprobante de pago y avisos por correo; cheques girados al cierre del mes; aviso al cierre contable; informes de Tesorería | US19, US21 (Tesorería), US26 |
| **R2 · Recaudo ampliado** | Canje completo de cheques, convenios de recaudo con referencia y corresponsales, archivos de pasarelas, aseguradoras, informes de canales | US15, US16, US21 (canales) |
| **T2 · Tesorería ampliada** | Conciliación bancaria completa (reglas avanzadas, varios bancos) y notas bancarias; pagos a proveedores y de cuentas por pagar; conciliación de adquirentes; liquidez y flujo de caja; exógena; y, al terminar, **retiro de lo heredado** de Tesorería con migración de sus pendientes | US17, US18, US20, US21 (resto), US22 |

Reglas de convivencia:
- **R0 y T0 van juntas**, como después R1 y T1: la activación de Cartera y Captaciones exige las dos.
- **Hasta T2**, COOFLOPAL paga proveedores y cuentas por pagar con la Tesorería heredada y por Contabilidad; esos
  egresos manuales se ven en el saldo y la posición de cada cuenta y en la conciliación mínima (FR-209).
- **Hasta R2**, un cheque recibido se acepta sólo si el parámetro lo permite (apagado por defecto) y con canje
  manual; su devolución sí está desde R0 (FR-084).
- **Hasta R1**, un pago tardío de una partida se aplica con la fecha más antigua que admite el contrato y la mora
  indebida se corrige en el módulo (FR-071).

### Glosario

**Recaudo**
- **Punto de recaudo**: lugar de una oficina donde se recibe dinero (ventanilla). Tiene cajas.
- **Caja**: puesto de un cajero dentro de un punto; recauda sólo con una **sesión** abierta.
- **Sesión de caja (turno)**: apertura con base, recibos, egresos, movimientos y cierre con arqueo.
- **Bóveda**: efectivo de la oficina fuera de las cajas; recibe y abastece cajas y envía remesas al banco. Una
  oficina sin bóveda usa su caja principal como bóveda.
- **Recibo de caja**: documento único, numerado e inmutable de un ingreso de dinero; lleva sus **medios** y sus
  **aplicaciones** (lo que cada módulo hizo con el dinero), con la imputación de cada medio a cada aplicación.
- **Aplicación**: el pago, la consignación o el recaudo que un módulo registró por su contrato al confirmar
  el recibo, con su propio comprobante; se compone de una porción por clase de medio.
- **Egreso de caja**: documento de una salida de dinero en efectivo por la caja (retiro de ahorro, devolución).
- **Canal**: caja, banco (extracto o archivo), convenio de recaudo, pagaduría, en línea (por archivo).
- **Partida por identificar**: dinero recibido en un banco cuyo pagador o destino no se conoce.
- **Arqueo**: comparación, por medio de pago, de lo esperado con lo contado o comprobado.
- **Canje**: tiempo que tarda un cheque en hacerse efectivo; mientras tanto el valor no está disponible.
- **Firmeza**: fecha desde la cual una cancelación pagada con un medio no inmediato queda en firme.
- **Fecha valor, fecha contable y fecha de registro**: el día que cuenta para intereses, el período donde se
  contabiliza y el instante en que se registró.
- **Recibo de contingencia**: recibo de un talonario prenumerado usado cuando la caja no puede operar; se digita
  después con su fecha.
- **Quien efectúa**: la persona que entrega o recibe físicamente el dinero, cuando no es el pagador o el
  beneficiario (mensajero, apoderado, autorizado).

**Tesorería**
- **Cuenta bancaria propia**: cuenta de la cooperativa en un banco, vinculada a una cuenta contable bancaria.
- **Orden de pago**: instrucción de pagar a un beneficiario que llega de un módulo (la **orden de giro** de
  Cartera y Captaciones es una de ellas) o que registra Tesorería.
- **Lote de pago**: órdenes que se pagan juntas desde una cuenta con una forma (archivo, portal del banco o
  cheques).
- **Resultado del giro**: lo que Tesorería informa al origen de cada orden.
- **Regiro**: orden nueva, enlazada a una rechazada, que el origen emite para volver a pagar.
- **Nota bancaria**: movimiento que el banco hace por su cuenta (GMF, comisión, intereses, impuestos).
- **Conciliación bancaria**: cruce del extracto con lo registrado, con partidas conciliatorias explicadas. La
  **mínima** (T0) cruza por valor, fecha y referencia y deja marcar a mano; la **completa** (T2) suma reglas,
  notas bancarias y varios bancos.
- **Uso del fondo de liquidez**: única forma de mover dinero de una cuenta del fondo, con causal y aprobación.

**Salida en vivo**
- **Cuadre provisional**: activación aceptada con diferencias o sin el cuadre contra Contabilidad, con aprobación,
  motivo y fecha límite.
- **Go/no-go**: lista de condiciones que se verifica antes de la carga definitiva y de la activación.

## Interfaces

Recaudo y Tesorería **adoptan** las interfaces que Cartera y Captaciones ya definieron (versión 1) y piden, por
enmienda, lo que les falta: la versión 2 de los dos contratos con la partida citada, una consulta de la carga de
Cartera aplicada sin activar, un mensaje del archivo de desembolsos de Cartera, el saldo del fondo de liquidez a
Captaciones y los recaudos esperados de los dos módulos (FR-164, FR-165). Las demás interfaces nuevas son con
Comercial, Contabilidad, Nómina, la plataforma y entre ellas.

**Recaudo usa**

| Interfaz | De quién | Para qué | Entrega |
|---|---|---|---|
| Contrato de obligaciones (listar, simular, aplicar, reversar, resolver saldo a favor o anticipo), v1 y, con E-01, v2 | Cartera (013) | US3, US4, US5, US6, US7 | R0 (v1), R1 (v2) |
| Contrato de cuentas (consultar, exigibles, simular, consignar, retirar, trasladar, reversar), v1 y, con E-02, v2 | Captaciones (014) | US3, US4, US5, US6, US7 | R0 (v1), R1 (v2) |
| Personas y obligaciones de una carga aplicada sin activar | Cartera (enmienda FR-164) | FR-046 | R1 |
| Consulta de cuentas por cobrar comerciales pendientes por persona | Contabilidad (enmienda FR-167) | US23 | R1 |
| Efectivo recibido en el punto de venta (para el registro único) | Comercial (enmienda FR-166) | FR-204 | R1 |
| Personas, cuentas bancarias de la persona, listas restrictivas, autorizaciones de datos, perfil tributario | Plataforma | FR-016, FR-098, FR-203 | R0 |
| Formatos de archivo plano (lectura y escritura) | Plataforma (010) | US6, US7, US16 | R0 |

**Recaudo recibe** (como destino de la bandeja): «pago reversado por Cartera», «consignación reversada por
Cartera» y «descuentos por pagaduría» (v1 y v2).

**Recaudo emite**: «envío de descuentos confirmado» y «resultado de descuentos» (v1 y v2) a Cartera; sus hechos a
Contabilidad; órdenes de pago a Tesorería (devoluciones); remesas a Tesorería.

**Tesorería recibe**: «orden de giro emitida» (v1 y v2) y «anulación de orden de giro» de Cartera y Captaciones;
«archivo de desembolsos generado o anulado» de Cartera (enmienda FR-164, T0); facturas de proveedor por pagar de
Comercial (enmienda FR-166, T2); órdenes de pago de Recaudo.

**Tesorería emite**: «resultado del giro» al origen de cada orden, siempre a nombre de una persona; sus hechos a
Contabilidad; el pago de la factura a Comercial (enmienda FR-166, T2); el saldo a la fecha de las cuentas del fondo
de liquidez a Captaciones (enmienda FR-165, T2).

**Tesorería usa**: movimientos contables de cada cuenta bancaria para conciliar (Contabilidad, enmienda FR-167,
T0); consulta de cuentas por pagar contables pendientes (enmienda FR-167, T2); archivos de dispersión de Nómina
para conciliar (enmienda FR-168, T2); lotes de datáfono y pagos con tarjeta del punto de venta (enmienda FR-166,
T2); recaudos esperados de Cartera y Captaciones (enmiendas FR-164 y FR-165, T2).

## User Scenarios & Testing *(mandatory)*

Actores: **cajero**, **supervisor de caja** (jefe de oficina) y su suplente, **analista de recaudo** (bancos,
pagadurías, convenios), **tesorero**, **aprobadores** (por monto), **conciliador** (por defecto, Contabilidad),
**contador**, **oficial de cumplimiento**, **auditor** y revisor fiscal, el **asociado o tercero** que paga o
recibe y **quien efectúa** la entrega física, las **pagadurías** y los **bancos** (por sus archivos o su portal), y
el **proceso de la cooperativa** (tareas programadas, que proponen y alertan pero nunca aplican pagos ni informan
resultados).

### User Story 1 - Configurar puntos de recaudo, cajas, cajeros y medios de pago (Priority: P1)

El jefe de operaciones configura, sin programar, los puntos de recaudo de cada oficina y sus cajas, asigna
cajeros, define la base de cada caja y elige qué medios de pago del catálogo común se ofrecen en cada punto, con
sus reglas de captura y de arqueo. Configura también los tipos de recibo y de egreso con su consecutivo y los
conceptos de ingresos y egresos varios.

**Why this priority**: sin puntos, cajas y medios no hay recibo ni arqueo; es el primer paso de la puesta en
marcha de COOFLOPAL.

**Entrega**: R0 con efectivo y transferencia (cheque recibido por parámetro); R1 con el resto de medios.

**Independent Test**: crear un punto con dos cajas, ofrecer efectivo y transferencia, asignar un cajero y comprobar
que en la otra oficina el punto no aparece para un usuario sin alcance en ella.

**Acceptance Scenarios**:

1. **Given** un medio de clase «crédito a asociado», **When** se intenta ofrecer en un punto de recaudo,
   **Then** se rechaza: pagar una obligación con crédito no es un recaudo.
2. **Given** un medio de tarjeta sin adquirente ni datáfono, **When** se ofrece en un punto, **Then** se
   rechaza indicando lo que falta, como en el punto de venta.
3. **Given** una caja con sesión abierta, **When** se cambia su base o se inactiva, **Then** el cambio rige
   desde la próxima sesión y la abierta conserva lo que selló al abrir.
4. **Given** un código de punto, caja o concepto que ya existe, **When** se crea otro igual, **Then** responde
   «código duplicado» con el nombre del existente antes de guardar.
5. **Given** un concepto de ingreso gravado, **When** se configura, **Then** queda marcado «exige factura» y no se
   puede cobrar en caja sin la factura de Comercial como documento cruce.

---

### User Story 2 - Abrir, arquear y cerrar la caja por medio de pago (Priority: P1)

El cajero abre su sesión contando la base por denominaciones. Durante el turno el supervisor puede abastecerlo
desde la bóveda o retirarle efectivo, con doble custodia, y el cajero puede reclasificar un pago registrado con el
medio equivocado con soporte y aprobación de otra persona. Al cerrar, cuenta por medio de pago (efectivo por
denominaciones, tarjetas por lote de datáfono, transferencias y consignaciones por referencia, cheques por
relación); el sistema compara con lo esperado. Las diferencias llevan motivo y, sobre la tolerancia del medio,
aprobación del supervisor o su suplente. El supervisor cierra el día del punto, que consolida las sesiones. Si la
caja no puede operar, el punto usa el talonario de contingencia.

**Why this priority**: el cuadre diario de la caja es el control básico del dinero y lo que exige la revisoría
fiscal desde el primer día.

**Entrega**: R0.

**Independent Test**: abrir con 500.000 de base, recaudar 1.200.000 en efectivo y 800.000 por transferencia,
retirar 1.000.000 a bóveda, cerrar contando 699.000 en efectivo y las referencias de la transferencia, y comprobar
el faltante de 1.000 con su motivo, su hecho contable como cuenta por cobrar al cajero, el aviso al cajero y el
informe de cierre por medio.

**Acceptance Scenarios**:

1. **Given** una caja sin sesión abierta, **When** el cajero intenta emitir un recibo, **Then** no puede y se
   le ofrece abrir la sesión.
2. **Given** una sesión abierta en una caja, **When** otro cajero intenta abrir la misma caja, **Then** se
   rechaza indicando quién la tiene abierta.
3. **Given** el arqueo ciego activado, **When** el cajero cuenta, **Then** no ve lo esperado hasta registrar
   lo contado.
4. **Given** una diferencia de 50.000 en efectivo y una tolerancia de 5.000, **When** el cajero cierra,
   **Then** la sesión queda «cerrada con diferencia por aprobar»; el supervisor, otra persona, la aprueba con
   motivo y se emite el hecho de faltante a cargo del cajero (cuenta por cobrar, con aviso al cajero) o de gasto,
   según el parámetro; su descuento por Nómina exige la autorización escrita del caso.
5. **Given** una sesión cerrada, **When** se descubre un error, **Then** no se reabre: se corrige con un
   documento de ajuste enlazado a la sesión, con aprobación.
6. **Given** el efectivo de una caja sobre el tope de aviso, **When** se emite el siguiente recibo en efectivo,
   **Then** se avisa al cajero y al supervisor que debe trasladar a bóveda; sobre el tope de bloqueo, si está
   configurado, no se reciben más pagos en efectivo hasta el traslado.
7. **Given** un pago registrado como efectivo que en realidad fue por transferencia, **When** el cajero lo
   reclasifica, **Then** exige la referencia bancaria como soporte y la aprobación de otra persona, y queda en el
   informe de excepciones.
8. **Given** el supervisor ausente, **When** hay diferencias por aprobar, **Then** las aprueba el suplente con
   delegación vigente; sin suplente el día del punto queda abierto, se alerta y el lote contable de los demás
   puntos no se detiene.
9. **Given** la caja sin sistema por una caída, **When** llega un asociado, **Then** el cajero emite un recibo del
   talonario de contingencia; al volver el sistema lo digita con su fecha, marca «contingencia» y la imagen del
   talonario, y aparece en el informe de contingencia hasta digitarse.

---

### User Story 3 - Recibo de caja multiproducto por los contratos de Cartera y Captaciones (Priority: P1)

El cajero busca a la persona (por documento, lector o número de obligación) y ve en una sola pantalla lo que puede
pagar: sus créditos (como deudor y, si lo pide, como codeudor) con lo exigible hoy, la cuota del ciclo, lo vencido
y el total para cancelar; sus cuotas de aportes y de ahorro permanente o contractual pendientes; sus cuentas de
ahorro para consignar; sus cuentas por cobrar comerciales pendientes; y conceptos varios. Elige qué paga y cómo
(modalidad por crédito; cuotas o consignación libre por cuenta), o preselecciona todo lo exigible; el sistema
simula cada aplicación con el módulo dueño y muestra la distribución, los avisos (el anticipo no reduce intereses;
el abono extraordinario reduce plazo o cuota, a elección del deudor) y el total. El cajero registra uno o varios
medios, ve cómo se imputan a las aplicaciones, confirma, y se emite **un solo recibo** con todas las aplicaciones,
o ninguna.

**Why this priority**: es el corazón de Recaudo y lo único que libera la activación de Cartera y Aportes.

**Entrega**: R0 (créditos, aportes y cuentas con efectivo y transferencia); R1 (codeudor, preselección de lo
exigible, cuentas por cobrar comerciales, demás medios).

**Independent Test**: para un asociado con dos créditos y la cuota de aportes del mes, pagar la cuota del
ciclo de uno, un abono extraordinario «reducir plazo» en el otro y la cuota de aportes, con 300.000 en efectivo
y 500.000 por transferencia; comprobar un recibo con tres aplicaciones, cada una con su comprobante del módulo
dueño y su imputación por medio, la elección del deudor impresa, y que repetir la confirmación con la misma clave
devuelve el mismo recibo.

**Acceptance Scenarios**:

1. **Given** un abono extraordinario sin elección, **When** el cajero simula, **Then** ve las dos
   alternativas (reducir plazo y reducir cuota) con su plan y no puede confirmar sin la elección del deudor, que
   queda impresa y guardada en el recibo.
2. **Given** una simulación vista por el asociado y un pago de nómina aplicado al mismo crédito un segundo
   antes, **When** el cajero confirma, **Then** Cartera responde que la distribución cambió, el sistema muestra
   la nueva y pide confirmar otra vez con la misma clave; nada queda registrado mientras tanto.
3. **Given** un recibo con tres aplicaciones donde Captaciones rechaza la tercera (p. ej. límite de
   participación en aportes), **When** se confirma, **Then** no queda ningún pago, ni recibo, ni número
   consumido; el cajero ve el motivo de la tercera y puede quitarla o cancelar.
4. **Given** un pago de 1.000.000 para cancelar un crédito cuyo total es 980.000, **When** se simula, **Then**
   el excedente de 20.000 se muestra antes de confirmar y el cajero ajusta el valor o lo destina a otro producto;
   nunca entra a Cartera.
5. **Given** un tercero que paga por el asociado, **When** se registra, **Then** el recibo sale a nombre del
   pagador con el producto enmascarado y el valor, sin saldos ni mora del beneficiario, y Cartera registra al
   pagador como tercero del pago.
6. **Given** efectivo y vueltas, **When** el asociado entrega 100.000 para un total de 82.000, **Then** el
   recibo registra 100.000 recibidos y 18.000 de vueltas; sólo el efectivo admite vueltas.
7. **Given** una cancelación pagada con cheque, **When** se emite el recibo, **Then** dice «sujeta a firmeza
   hasta» la fecha que informó Cartera.
8. **Given** la cuota de un CDAT en apertura, **When** se consigna su fondeo por un valor distinto del pactado,
   **Then** se rechaza indicando el valor exacto.
9. **Given** un deudor de otra oficina, **When** el cajero lo busca, **Then** puede cobrarle (parámetro por
   defecto); si el contrato del módulo lo rechaza por su propio alcance, se muestra su motivo.
10. **Given** un parámetro de umbral cambiado entre la simulación y la confirmación, **When** se confirma,
    **Then** rige la versión vigente al confirmar y, si cambia lo exigido, se vuelve a simular.

---

### User Story 4 - Entregar dinero por la caja: retiros, saldos a favor y excedentes (Priority: P1)

El cajero paga en efectivo un retiro de ahorro que Captaciones autoriza, un saldo a favor o un anticipo que Cartera
devuelve, o el saldo a favor que Recaudo guarda de un pagador. Verifica la identidad y las firmas del titular,
Captaciones valida y debita y devuelve la autorización; el cajero entrega, el titular (o quien recibe por él, con su
documento) firma el **egreso de caja** y todo queda en una sola operación. Lo que se pague en cheque o
transferencia sale por orden de pago a Tesorería.

**Why this priority**: sin entrega de efectivo Captaciones no puede abrir la ventanilla (su cancelación con
entrega espera a Recaudo) y los saldos a favor no tendrían salida en efectivo.

**Entrega**: R1. En R0 los saldos a favor y excedentes se devuelven por orden de pago a Tesorería (T0).

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
   resuelve «devolver por el recaudador» y el egreso entrega el valor; si el asociado prefiere transferencia o
   cheque, se emite una orden de pago a Tesorería.
5. **Given** el egreso en efectivo de un monto sobre el umbral, **When** se registra, **Then** cuenta para el
   control y el reporte de transacciones en efectivo y registra a quien recibe si no es el titular.
6. **Given** la devolución de un saldo a favor que dispone de una cuenta contable, **When** se registra, **Then**
   se evalúa el GMF de ese egreso según los parámetros vigentes y, si aplica, se emite su hecho.

---

### User Story 5 - Anular recibos, reversar aplicaciones y atender los reversos de los módulos (Priority: P1)

Un recibo mal hecho se anula entero con motivo: la anulación es un documento nuevo que reversa cada aplicación
vigente por su contrato, con la clave derivada, y deja el original marcado como anulado. En la sesión abierta y el
mismo día lo pide el cajero y lo aprueba otro; después, con permiso propio y aprobación de otro. Cuando Cartera o
Captaciones reversan desde su pantalla una aplicación de Recaudo, Recaudo recibe el aviso, registra la aplicación
reversada por el módulo y resuelve el dinero según la causa: si el dinero se recibió, queda como saldo a favor del
pagador por resolver; si nunca llegó, se da de baja el medio sin saldo a favor. El cheque devuelto se registra
siempre en Recaudo (US15).

**Why this priority**: sin anulación controlada no hay cuadre; los reversos de los módulos son parte del
contrato que ya rige en producción.

**Entrega**: R0.

**Independent Test**: anular un recibo de dos aplicaciones y comprobar los dos reversos en Cartera y
Captaciones, el documento de anulación con su número, el efectivo esperado de la sesión rebajado y los hechos
espejo; reversar desde Cartera un pago por error de digitación y comprobar el aviso, el documento de aplicación
reversada, el saldo a favor del pagador y la alerta al supervisor.

**Acceptance Scenarios**:

1. **Given** un recibo de la sesión abierta, **When** el cajero pide anularlo, **Then** queda «anulación pendiente
   de aprobación» hasta que otro usuario con atribución lo aprueba; el cajero no se aprueba a sí mismo.
2. **Given** un recibo de una sesión ya cerrada, **When** se anula con permiso y aprobación, **Then** la anulación
   no toca ninguna sesión: deja un saldo a favor del pagador por el valor del recibo, y el dinero sólo sale con el
   egreso o la orden de pago que lo devuelve.
3. **Given** un recibo cuya anulación rechaza uno de los contratos, **When** se confirma, **Then** no se anula
   nada y se muestra el motivo.
4. **Given** un pago de Nómina, **When** alguien intenta reversarlo desde Recaudo, **Then** no aparece: lo de
   Nómina sólo lo reversa Nómina.
5. **Given** un aviso de reverso de Cartera para un recibo ya anulado, **When** llega, **Then** se registra sin
   efecto y queda en la bandeja como procesado.
6. **Given** un recibo con una aplicación ya reversada por el módulo, **When** se anula, **Then** la anulación
   omite esa aplicación y reversa sólo las vigentes.
7. **Given** una anulación y la reemisión del mismo valor a otro producto, **When** el auditor consulta, **Then**
   ambas aparecen enlazadas en el informe de anulaciones con reemisión.

---

### User Story 6 - Consignaciones y transferencias recibidas: identificar, aplicar y partidas por identificar (Priority: P1)

El analista de recaudo carga cada día el extracto (archivo con formato, plantilla genérica o captura manual) de
cada cuenta bancaria de recaudo. El sistema reconoce los abonos y, desde R1, propone a quién pertenecen por la
referencia de pago, el documento del consignante o una consignación reportada en caja. El analista confirma y
aplica: cada abono identificado genera un recibo de canal «banco» con la fecha valor del movimiento, aplicado en
forma no interactiva con la modalidad por defecto del canal y a nombre del analista. Lo que no se identifica queda
como **partida por identificar**; se identifica después, en todo o en parte, o se clasifica a otro concepto con
aprobación.

**Why this priority**: muchas cuotas de COOFLOPAL llegan por consignación; sin identificación quedan sin aplicar
y los créditos entran en mora indebida.

**Entrega**: R0 (carga, identificación manual, partidas); R1 (reglas de identificación, consignación reportada,
fecha valor de partidas tardías con E-01 y E-02).

**Independent Test**: cargar un extracto con cinco abonos: tres con referencia válida, uno identificable por el
documento del consignante y uno que queda por identificar; aplicar los cuatro; identificar el quinto diez días
después y comprobar, con E-01 y E-02 adoptadas, que se aplica con su fecha valor original.

**Acceptance Scenarios**:

1. **Given** un extracto ya cargado, **When** se vuelve a cargar con movimientos repetidos, **Then** los
   movimientos con la misma huella no se cargan dos veces y el sistema dice cuántos omitió; dos movimientos
   iguales sin identificador del banco se muestran como posibles duplicados para que una persona decida.
2. **Given** un abono de 900.000 de un pagador con dos créditos, **When** el analista lo divide en 600.000 y
   300.000, **Then** se emite un recibo con dos aplicaciones; la suma debe ser exacta.
3. **Given** una partida identificada después de la ventana de fecha valor del contrato, **When** se aplica,
   **Then** conserva la fecha valor del movimiento (enmiendas FR-164 y FR-165); mientras las enmiendas no estén
   adoptadas, el sistema avisa, aplica con la fecha más antigua admitida y alerta para que el módulo corrija la
   mora indebida por su condonación con aprobación.
4. **Given** una partida con más de 30, 60 o 90 días, **When** corre el proceso diario, **Then** alerta al
   supervisor y al contador en cada escalón; no cambia nada solo.
5. **Given** un abono que resulta ser un reintegro de un proveedor, **When** el analista lo clasifica al
   concepto «reintegros», **Then** pide aprobación y emite el hecho de ese concepto; no genera recibo de
   ninguna persona sin tercero.
6. **Given** un extracto de 300 movimientos donde uno corresponde a un crédito cancelado entretanto, **When** se
   aplica, **Then** las demás líneas se aplican, la rechazada queda con su motivo y su dinero como saldo a favor
   del pagador, y reprocesarla usa la misma clave.
7. **Given** un abono que devuelve una transferencia que Tesorería ya informó «girada», **When** se identifica,
   **Then** se clasifica como giro devuelto enlazado a la orden, con alerta al origen y al tesorero; el resultado
   del giro no cambia.

---

### User Story 7 - Pagadurías y libranza (Priority: P1)

Cartera genera el archivo de descuentos de cada pagaduría y período y se lo envía a Recaudo. El analista lo ve
en la bandeja por pagaduría, lo descarga, lo envía a la pagaduría y **confirma el envío** con canal, fecha y
radicado. Cuando la pagaduría responde, **carga su archivo** con el formato de vuelta del convenio (o con la
plantilla genérica o a mano, si la pagaduría responde en otro formato), revisa la vista previa y **devuelve el
resultado** a Cartera. Cuando la pagaduría **gira**, el analista registra el giro (identificado en el extracto o
recibido en cheque) y lo aplica: cada línea descontada pendiente de giro se paga por el contrato de obligaciones o
el de cuentas, citando la línea, y cada línea es su propia unidad.

**Why this priority**: la libranza es un canal de recaudo de COOFLOPAL (sus pagadurías están por confirmar, P6) y
de toda cooperativa con convenios de nómina con empresas.

**Entrega**: R0; el calendario legal de giro y el informe de pagadurías morosas, R1.

**Independent Test**: con Cartera real en prueba, recibir un archivo de 50 líneas (créditos y aportes), confirmar el
envío, cargar una respuesta con 45 descontadas y 5 no descontadas, registrar el giro de las 45 y comprobar las
aplicaciones, el recibo de canal «pagaduría» y que las 5 siguen exigibles en Cartera con su motivo.

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
6. **Given** un giro de 5.000 líneas donde una cuenta de aportes quedó bloqueada entretanto, **When** se aplica,
   **Then** las demás líneas se aplican y la rechazada queda como saldo de la pagaduría con su motivo.
7. **Given** un giro tardío de aportes por libranza, **When** se aplica después de la ventana de fecha valor,
   **Then** con E-02 adoptada conserva la fecha del descuento; antes, sigue la regla de FR-071.

---

### User Story 8 - Contabilidad recibe los hechos de Recaudo y Tesorería por su matriz (Priority: P1)

Cada recibo, egreso, anulación, movimiento de caja, diferencia de arqueo, remesa, partida por identificar,
giro, resultado de giro, traslado, GMF y nota bancaria emite un hecho a Contabilidad, sin cuentas. Contabilidad lo
contabiliza por la matriz de cada emisor (hecho, rol y dimensiones como medio, punto, caja, oficina, cuenta
bancaria propia, concepto y módulo de destino). Los puentes con Cartera y Captaciones (recaudo por aplicar,
desembolso por girar, devolución por pagar) quedan en cero o explicados.

**Why this priority**: sin contabilidad no se cierra el mes; los puentes de Cartera y Captaciones sólo se
saldan con los hechos de Recaudo y Tesorería.

**Entrega**: R0 y T0.

**Independent Test**: con la matriz completa, emitir un recibo en efectivo y transferencia que paga un crédito y
aportes, girar un desembolso por archivo y cerrar la caja con un faltante; comprobar los comprobantes y que la
conciliación de puentes de Cartera y Captaciones da cero. Quitar la regla de la transferencia y comprobar que la
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
5. **Given** la mañana de la salida con una regla faltante, **When** una persona con permiso cambia la política a
   «advertir», **Then** el cambio exige motivo, queda auditado y alerta hasta que se vuelve a «bloquear».

---

### User Story 9 - Registrarse como participantes, retirar los dobles y liberar la activación de Cartera y Captaciones (Priority: P1)

El administrador de la cooperativa registra a Recaudo como recaudador (contrato de obligaciones y de cuentas) y
como destino de sus mensajes, y a Tesorería como destino de giros, con las versiones que este despliegue
implementa. En los ambientes de prueba, el despliegue que trae Recaudo y Tesorería reales retira las filas de los
dobles de prueba; desde entonces los dobles no existen en ningún ambiente y las pruebas de Cartera y Captaciones
corren contra los módulos reales. La activación de Cartera y de Aportes ve los requisitos en verde.

**Why this priority**: es literalmente lo que bloquea la salida de COOFLOPAL.

**Entrega**: R0 y T0.

**Independent Test**: en una cooperativa de prueba con los dobles registrados, desplegar la versión con Recaudo y
Tesorería reales, comprobar que las filas de los dobles quedaron retiradas con su historia, registrar Recaudo y
Tesorería, activarlos y comprobar que la activación de Cartera muestra sus requisitos cumplidos y que un pago por
caja y un giro por archivo recorren el ciclo completo.

**Acceptance Scenarios**:

1. **Given** cualquier ambiente, **When** se intenta registrar un doble de prueba, **Then** se rechaza: los dobles
   ya no existen.
2. **Given** una cooperativa de prueba con la fila del doble de tesorería registrada con el código «TES», **When**
   se despliega la versión con los módulos reales, **Then** la fila del doble queda retirada (con su historia) y el
   registro de la Tesorería real procede; el arranque no encuentra dos consumidores para un mismo destino.
3. **Given** Recaudo registrado sin adoptar el contrato de cuentas, **When** se intenta activar Aportes, **Then**
   la activación dice qué adopción falta.
4. **Given** Recaudo sin puntos, cajas, medios o, con Contabilidad iniciada, sin la matriz completa de los hechos
   que no estén en «no pasa», **When** se intenta activar Recaudo, **Then** se niega con la lista de lo que falta.
5. **Given** la activación de Recaudo y Tesorería, **When** se guarda, **Then** abre en el mismo guardado la
   vigencia de sus filas del registro, y sólo después puede activarse Cartera en esa cooperativa.

---

### User Story 10 - Cuentas bancarias propias y ejecución de órdenes de giro (Priority: P1)

El tesorero registra las cuentas bancarias de la cooperativa, cada una vinculada a su cuenta contable bancaria.
Las órdenes de giro de Cartera y Captaciones llegan a una **bandeja única de órdenes de pago** con su origen,
beneficiario, cuenta destino, valor, fecha y forma. El tesorero arma un lote por cuenta de origen y forma, revisa
la vista previa con el formato bancario vigente del banco (o el genérico, o la forma «portal del banco»), y lo envía
a aprobación; aprobado por quien tenga atribución para el monto, genera el archivo (inmutable, con huella), lo
descarga una persona autorizada, lo carga en el banco y lo marca enviado registrando quién lo cargó y quién lo
autorizó en el banco. Los cheques salen numerados de la chequera de la cuenta.

**Why this priority**: sin giros no hay desembolsos ni devoluciones; es el segundo requisito de activación.

**Entrega**: T0.

**Independent Test**: recibir tres órdenes (un desembolso, una devolución del retiro de un asociado y los
intereses de un CDAT a un tercero), armar el lote en la cuenta de pagos de COOFLOPAL, aprobarlo con otro usuario,
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
5. **Given** un archivo generado y no enviado, **When** se anula, **Then** sus órdenes vuelven a la bandeja, el
   archivo queda anulado con su huella para la historia y no se informa ningún resultado al origen.
6. **Given** la misma orden de giro recibida dos veces, **When** llega la segunda, **Then** no se duplica en la
   bandeja.
7. **Given** una orden con valor cero o negativo, **When** llega, **Then** se rechaza en la bandeja con su motivo.
8. **Given** un beneficiario o su cuenta destino con coincidencia en listas vinculantes, **When** se arma o aprueba
   el lote, **Then** la orden queda bloqueada fuera del lote y se alerta al oficial de cumplimiento.
9. **Given** un banco sin formato de pagos cargado, **When** se arma el lote, **Then** puede usarse el formato
   genérico o la forma «portal del banco», con el resultado registrado a mano con soporte.

---

### User Story 11 - Confirmar, rechazar y regirar (Priority: P1)

El tesorero carga la respuesta del banco, confirma el emparejamiento que propone la conciliación con el extracto,
o registra a mano con soporte, y el sistema informa a cada origen el **resultado del giro**, a nombre de esa
persona: girada, rechazada con su causal o, en los cheques, girada al entregarlo y cheque no cobrado al anularlo.
Una orden rechazada vuelve a su origen, que decide: Cartera reversa el desembolso y espera un giro nuevo;
Captaciones reintegra el saldo o deja la orden pendiente de regiro. La orden nueva que llega se enlaza a la anterior
y Tesorería muestra la cadena. Cada línea de la respuesta del banco se coteja contra el archivo generado.

**Why this priority**: el contrato con Cartera y Captaciones exige el resultado; sin él no se cierra el puente
de giros.

**Entrega**: T0.

**Independent Test**: de un archivo de tres órdenes, el banco rechaza una por cuenta inexistente; comprobar
«girada» en dos, «rechazada» en la tercera con su causal, el reverso del desembolso en Cartera y que la orden de
regiro llega enlazada y se paga en un archivo nuevo.

**Acceptance Scenarios**:

1. **Given** un archivo marcado enviado, **When** todavía no hay respuesta del banco, **Then** las órdenes quedan
   «enviadas» sin informar nada al origen; se informan con la respuesta, el emparejamiento confirmado del
   extracto o el registro manual con soporte.
2. **Given** un archivo entero rechazado por el banco por un error de forma, **When** se registra, **Then** no se
   informa rechazo al origen: el archivo queda «rechazado por el banco» y sus órdenes vuelven a la bandeja para
   generarlo otra vez.
3. **Given** un cheque entregado con constancia, **When** se entrega, **Then** se informa «girada»; si no se cobra
   en los días configurados, alerta al tesorero, y anulado con permiso se informa «cheque no cobrado».
4. **Given** una orden sin resultado en los días configurados, **When** corre el proceso, **Then** alerta al
   tesorero (además de la alerta que ya levanta el origen); el proceso nunca informa un resultado.
5. **Given** el mismo resultado informado dos veces, **When** llega al origen, **Then** no cambia nada.
6. **Given** una orden ya informada «girada», **When** el banco devuelve el abono días después, **Then** no se
   informa «rechazada»: el dinero entra por Recaudo como giro devuelto (US6) y el origen emite una orden nueva.
7. **Given** una línea de la respuesta del banco con un valor o una cuenta distinta de la del archivo generado,
   **When** se carga, **Then** se alerta la discrepancia y la línea no se informa hasta que una persona la revise.
8. **Given** un resultado que llega antes del envío del archivo, **When** se intenta registrar, **Then** se rechaza
   indicando el estado de la orden.

---

### User Story 12 - Seguridad, alcance, segregación y auditoría (Priority: P1)

Cada acción de Recaudo y Tesorería exige su permiso; los roles vienen sembrados y cada cooperativa los ajusta. El
alcance limita a cada usuario a sus oficinas, puntos y cajas (Recaudo) y a sus cuentas bancarias, convenios y
pagadurías (Tesorería y canales sin caja); las operaciones sobre la atribución del usuario piden un aprobador. Quien
registra no aprueba lo suyo. Todo queda en la auditoría con cadena de sellos, incluidas las lecturas sensibles (lo
que paga y debe una persona, saldos bancarios, cambios de cuenta de un beneficiario, exportaciones).

**Why this priority**: es dinero; la revisoría y el SARLAFT lo exigen desde el primer día.

**Entrega**: R0 y T0.

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
4. **Given** un analista con alcance en una cuenta bancaria, **When** lista partidas por identificar, **Then** ve
   sólo las de esa cuenta.

---

### User Story 13 - Puesta en marcha: saldos iniciales, pendientes y cuadre (Priority: P1)

Antes de activar, el contador y el tesorero cargan por plantilla las bóvedas y cajas con su efectivo al corte,
las cuentas bancarias propias con su saldo y chequeras, los cheques girados no cobrados, las consignaciones en
tránsito, los cheques recibidos en canje, las partidas por identificar del corte, y los cajeros con sus cajas y
alcances. El sistema cuadra cada cifra contra Contabilidad y activa Recaudo y Tesorería en la cooperativa con
fecha, con cuadre definitivo o provisional.

**Why this priority**: COOFLOPAL arranca con dinero en caja y en bancos y con partidas pendientes; sin ellas el
primer arqueo y la primera conciliación no cuadran.

**Entrega**: R0 y T0.

**Independent Test**: cargar una plantilla con un error de fila y comprobar que no guarda nada y dice fila y
columna; corregir, cargar y comprobar el cuadre en cero contra Contabilidad.

**Acceptance Scenarios**:

1. **Given** un saldo de bóveda distinto del saldo contable de su cuenta de caja, **When** se cuadra, **Then**
   la diferencia se muestra y la activación exige un acta que la acepte o un cuadre provisional con fecha límite.
2. **Given** saldos iniciales cargados, **When** se activa, **Then** no se emiten hechos contables por ellos
   (ya están en los saldos de apertura de Contabilidad).
3. **Given** cheques girados en tránsito cargados, **When** se cobran después, **Then** se marcan cobrados en la
   conciliación como cualquier cheque de la chequera.
4. **Given** la apertura contable todavía sin conciliar, **When** se activa con cuadre provisional aprobado,
   **Then** se alerta cada día hasta el cuadre definitivo y el cierre contable del mes de la activación se niega
   sin él.

---

### User Story 14 - Control de efectivo y SARLAFT (Priority: P1)

Al recibir o entregar efectivo, el sistema suma por persona la operación y lo del mes calendario, con lo que reciben
las cajas de Recaudo y las del punto de venta. Sobre el umbral de declaración exige la declaración de origen de
fondos (con su soporte adjunto) una vez por recibo y la hace viajar a cada aplicación que la exija, y registra a
quien efectúa la entrega si no es el pagador; ante señales de fraccionamiento o acumulados, alerta al oficial de
cumplimiento. Toda persona que paga, efectúa o recibe se consulta contra las listas: una coincidencia con una lista
vinculante impide la operación. Cada mes el oficial genera el **reporte de transacciones** (individuales y
múltiples) con la estructura vigente como dato, lo valida y lo marca cargado con su radicado.

**Why this priority**: es obligación legal desde la primera operación en efectivo.

**Entrega**: R0 (umbrales, declaración, quien efectúa, listas, alertas sobre lo de Recaudo); R1 (registro único con
el punto de venta y generador del reporte, en producción antes del primer vencimiento).

**Independent Test**: con el umbral de declaración en 5.000.000, recibir 6.000.000 en efectivo sin declaración y
comprobar el rechazo; con declaración, el recibo. Con el umbral de transacciones múltiples en 30.000.000 y la señal
de fraccionamiento en «tres o más operaciones bajo el umbral de declaración en siete días», recibir 4 pagos de
8.000.000 en efectivo en una semana y comprobar la alerta de fraccionamiento y su aparición en el reporte de
múltiples del mes.

**Acceptance Scenarios**:

1. **Given** un recibo con 4 millones en efectivo y 6 millones por transferencia, **When** el umbral de
   declaración es de 5 millones en efectivo, **Then** no se exige declaración: cuenta sólo el efectivo.
2. **Given** un pagador, quien efectúa o un beneficiario con coincidencia en una lista vinculante, **When** se
   intenta la operación, **Then** no se ejecuta ni se recibe el dinero, se alerta de inmediato al oficial para su
   reporte y sólo él puede liberar la persona si es un falso positivo.
3. **Given** una coincidencia en una lista no vinculante, **When** se registra el recibo, **Then** se alerta al
   oficial y, según el parámetro, se sigue o se retiene la operación con el dinero en custodia de la sesión hasta
   que él la libere.
4. **Given** el reporte del mes generado, **When** se regenera, **Then** queda una versión nueva y la anterior
   reemplazada, con su archivo.
5. **Given** una persona que paga 3 millones en el punto de venta y 3 millones en cuotas el mismo día, **When** se
   evalúa, **Then** el acumulado suma los dos.
6. **Given** el parámetro de aplicación del reporte en «suspendido» para un período, **When** llega el plazo,
   **Then** el sistema informa la suspensión y no exige el reporte; los datos se siguen registrando.

---

### User Story 15 - Cheques recibidos, canje y devolución (Priority: P1)

El cajero recibe un cheque como medio (banco, número, cuenta, girador, fecha y valor) y el sistema valida el plazo
de presentación. El cheque se consigna en la remesa del día y pasa a «en canje»; el valor queda no disponible en
captaciones y la cancelación de un crédito queda sujeta a firmeza. Cuando el extracto lo confirma, pasa a
«pagado». Si el banco lo devuelve, el analista registra la devolución con su causal y el sistema reversa por los
contratos las porciones de cada aplicación pagadas con él, da de baja el medio, avisa al pagador y alerta al
supervisor.

**Why this priority**: se reciben pocos cheques en ventanilla, pero uno devuelto sin reverso deja una deuda pagada
con dinero que no llegó.

**Entrega**: R0 (recepción por parámetro, canje manual y devolución con reverso); R2 (canje completo).

**Independent Test**: pagar con un cheque una cuota de crédito y un aporte, devolverlo por fondos insuficientes
y comprobar los dos reversos, el cheque devuelto, el aviso y el informe de cheques devueltos.

**Acceptance Scenarios**:

1. **Given** un cheque posfechado, **When** se recibe, **Then** se rechaza; si el parámetro lo admite, queda como
   «cheque en custodia» sin recibo ni aplicación hasta su fecha.
2. **Given** un cheque devuelto cuyo valor ya se retiró de la cuenta de ahorro, **When** se reversa, **Then**
   Captaciones deja el sobregiro técnico con bloqueo y alerta; Recaudo lo muestra en el informe.
3. **Given** un girador con cheques devueltos, **When** entrega otro, **Then** el cajero ve la advertencia.
4. **Given** un recibo de 300.000 en efectivo y 500.000 en cheque que pagó dos aplicaciones, **When** el cheque se
   devuelve, **Then** se reversan sólo las porciones imputadas al cheque y el efectivo sigue aplicado.
5. **Given** un cheque devuelto con fecha valor en un mes ya cerrado, **When** se registra, **Then** los reversos
   piden el permiso con monto del contrato y la devolución se fecha en el período abierto.
6. **Given** un cheque de otra plaza recibido después del plazo de presentación, **When** se registra, **Then** el
   cajero ve el aviso; no bloquea salvo que la cooperativa lo configure.

---

### User Story 16 - Convenios de recaudo y canales externos (Priority: P2)

El analista configura convenios de recaudo con bancos y corresponsales: cuenta recaudadora, estructura de la
referencia de pago y formato del archivo diario de recaudo como dato versionado. Los documentos al asociado
(extracto de crédito, cupón de pago) llevan su referencia. Cada día carga el archivo del convenio; el sistema lo
valida, identifica por referencia y aplica al cargarlo, a nombre del analista, con un recibo por pago; lo que no casa
va a partidas por identificar. Concilia lo informado por el convenio con lo abonado en la cuenta, neto de
comisiones y retenciones.

**Why this priority**: amplía los canales; COOFLOPAL sale con consignaciones identificadas por extracto (US6).

**Entrega**: R2.

**Independent Test**: configurar un convenio con referencia «número de obligación + dígito», cargar un archivo
de 100 pagos con 2 referencias inválidas y comprobar 98 aplicaciones y 2 partidas.

**Acceptance Scenarios**:

1. **Given** un archivo del convenio ya cargado, **When** se carga otra vez, **Then** se rechaza por la huella del
   archivo; un movimiento que ya llegó por el extracto no se identifica dos veces.
2. **Given** un dígito de verificación inválido, **When** se identifica, **Then** el pago va a partidas con el
   motivo.
3. **Given** lo informado de 10.000.000 y un abono de 9.985.000, **When** se concilia, **Then** la diferencia
   se explica como comisión, IVA y retenciones del convenio con su hecho y su tercero; sobre la tolerancia exige
   aprobación.

---

### User Story 17 - Conciliación bancaria completa con el extracto (Priority: P2)

El conciliador (por defecto, Contabilidad; nunca quien prepara o aprueba pagos de esa cuenta) concilia cada cuenta
bancaria por mes: el sistema empareja el extracto con lo registrado —recibos de canal banco, remesas, giros, cheques
cobrados, dispersiones de Nómina, archivos de desembolsos de Cartera y movimientos manuales de Contabilidad— con
reglas configurables; propone y el conciliador confirma lo dudoso. Las notas bancarias (GMF, comisiones, intereses,
impuestos) se registran como hechos de Tesorería con aprobación; los abonos no identificados pasan a Recaudo como
partidas por identificar. Cierra la conciliación con sus partidas conciliatorias.

**Why this priority**: amplía la conciliación mínima de T0 (US25) con reglas, notas y varios bancos.

**Entrega**: T2.

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
4. **Given** un cargo bancario no identificado (embargo, débito no autorizado), **When** aparece, **Then** levanta
   una alerta propia al tesorero y al contador.
5. **Given** intereses bancarios abonados netos de retención, **When** se registran, **Then** la nota lleva el
   interés en bruto y la retención aparte, con el banco como tercero.

---

### User Story 18 - Pagos a proveedores y de cuentas por pagar (Priority: P2)

Las facturas de proveedor confirmadas en Comercial llegan a la bandeja de órdenes con su vencimiento y su neto
(las retenciones ya las practicó la factura). Las cuentas por pagar registradas en Contabilidad (honorarios,
servicios, impuestos) se consultan por tercero y documento cruce y se convierten en órdenes. El tesorero las
programa por vencimiento y las paga en lotes como los giros. Cada pago emite su hecho con tercero y documento
cruce, e informa a Comercial que la factura quedó pagada.

**Why this priority**: COOFLOPAL paga proveedores con la Tesorería heredada hasta T2 (decisión del dueño).

**Entrega**: T2.

**Independent Test**: pagar en un lote dos facturas de proveedor y una cuenta por pagar contable, comprobar el
hecho de cada una con su documento cruce y el saldo pendiente en cero en Contabilidad.

**Acceptance Scenarios**:

1. **Given** una factura ya incluida en un lote no anulado, **When** se intenta incluir en otro, **Then** se
   rechaza indicando el lote.
2. **Given** un pago parcial de una cuenta por pagar contable, **When** se registra, **Then** el saldo pendiente
   queda con el resto (la orden de giro de Cartera y Captaciones, en cambio, no admite pago parcial).
3. **Given** el mensaje de una factura por pagar recibido dos veces, **When** llega el segundo, **Then** no se crea
   otra orden.
4. **Given** una factura anulada en Comercial después de incluirla en un lote no enviado, **When** llega la
   anulación, **Then** la orden sale del lote; si el archivo ya se envió, se alerta para gestionar el reintegro.
5. **Given** un lote pagado, **When** se reversa un pago por rechazo del banco, **Then** la factura vuelve a quedar
   por pagar en Comercial.

---

### User Story 19 - Chequeras y cheques girados; traslados entre cuentas propias (Priority: P1)

El tesorero registra las chequeras de cada cuenta (rango) y los cheques salen numerados de ella; los dañados se
anulan con su número. Un cheque girado pasa por emitido, entregado, cobrado (por conciliación), anulado o no
cobrado. Al cierre del mes, los cheques emitidos y no entregados se reclasifican y los no cobrados antiguos pasan a
cuenta por pagar. Los traslados entre cuentas propias y las remesas de efectivo entre bóvedas y bancos se registran
con aprobación y quedan en tránsito hasta que el extracto los confirma.

**Why this priority**: los cheques son una forma de giro de la orden y la chequera mínima entra el primer día (decisión del dueño); las remesas ligan la caja con los bancos.

**Entrega**: T0 (chequera mínima, cheques girados, remesas); T1 (traslados, cierre del mes de cheques).

**Independent Test**: girar un cheque de una orden, anular el siguiente por dañado y comprobar que la numeración
no tiene huecos sin explicar; trasladar 50 millones entre dos cuentas y comprobar que queda en tránsito hasta la
conciliación.

**Acceptance Scenarios**:

1. **Given** una chequera agotada, **When** se intenta girar, **Then** se pide registrar la siguiente.
2. **Given** un cheque entregado, **When** se intenta anular sin devolverlo, **Then** exige motivo, permiso y
   informa «cheque no cobrado» al origen.
3. **Given** un cheque emitido y no entregado al cierre del mes, **When** corre el cierre, **Then** se emite el
   hecho que lo devuelve al efectivo de la cuenta; un cheque no cobrado más allá del plazo configurado pasa a cuenta
   por pagar, nunca a ingreso.
4. **Given** una cuenta del fondo de liquidez, **When** se intenta trasladar desde ella sin un uso del fondo
   aprobado, **Then** se rechaza.

---

### User Story 20 - Posición de liquidez y flujo de caja (Priority: P3)

El tesorero ve cada día los saldos de bancos, bóvedas y cajas, los compromisos (órdenes aprobadas, cheques en
tránsito, giros de nómina) y lo esperado por recaudar (cuotas y exigibles por vencer que informan Cartera y
Captaciones), y proyecta el flujo de caja por semanas y meses, comparando lo real con lo proyectado. Las cuentas
que constituyen el fondo de liquidez quedan marcadas y su saldo lo consulta Captaciones.

**Why this priority**: gestión; reemplaza el flujo de caja heredado, que sólo sumaba facturas.

**Entrega**: T2.

**Independent Test**: con saldos y compromisos conocidos, comprobar que la posición del día coincide al peso
con la suma de los componentes y que el flujo real de un mes cerrado coincide con Contabilidad.

**Acceptance Scenarios**:

1. **Given** una cuenta marcada como fondo de liquidez, **When** Captaciones calcula el fondo, **Then** recibe su
   saldo a la fecha.
2. **Given** Cartera sin responder la consulta de recaudos esperados, **When** se arma la posición, **Then** se
   muestra sin ese componente y con el aviso; no se inventa un valor.
3. **Given** una orden aprobada que se anula, **When** se recalcula la posición, **Then** deja de contar como
   compromiso.

---

### User Story 21 - Informes de recaudo, caja y tesorería (Priority: P2)

Supervisores, tesorero, contador y auditor consultan y exportan: recaudo diario por punto, caja, cajero, medio,
canal, módulo y producto o línea; relación de recibos por cajero; recibos anulados con motivo y anulaciones con
reemisión; excepciones (fecha valor, reclasificación de medios, contingencia); partidas por identificar por
antigüedad; cheques en canje y devueltos; pagadurías (enviado, descontado, girado, pendiente, morosas); órdenes por
estado y antigüedad; lotes por aprobar y enviados; giros rechazados y pendientes de regiro; cheques girados;
saldos bancarios; conciliaciones. El recaudo consolidado incluye, de sólo lectura, lo recaudado por Nómina y por
débito automático.

**Why this priority**: control y revisoría; los de cierre de caja, recaudo por producto, partidas y órdenes
pendientes van desde R0 y T0 (FR-152, FR-153).

**Entrega**: por informe (FR-152, FR-153).

**Independent Test**: comprobar que el recaudo del día por medio coincide con los cierres de las sesiones y con
los hechos contabilizados.

**Acceptance Scenarios**:

1. **Given** un usuario con alcance en una oficina, **When** consulta, **Then** ve sólo esa oficina.
2. **Given** una exportación, **When** se descarga, **Then** exige su permiso y queda auditada.

---

### User Story 22 - Conciliación de adquirentes de tarjetas (Priority: P3)

El analista carga el reporte de abonos de cada red o adquirente y lo concilia con los lotes de datáfono
cerrados en las cajas de Recaudo y, por la consulta de Comercial, en las del punto de venta: lo cobrado contra lo
abonado, la comisión con su IVA y las retenciones que practicó la red. Las diferencias se registran con su hecho.

**Why this priority**: la 012 la dejó para Tesorería; es control, no operación.

**Entrega**: T2.

**Independent Test**: conciliar un día con dos lotes y un abono con comisión y retención, y comprobar el hecho
y la diferencia en cero.

**Acceptance Scenarios**:

1. **Given** un lote sin abono pasado el plazo, **When** corre el proceso, **Then** alerta.
2. **Given** el mismo reporte de la red cargado dos veces, **When** se carga, **Then** se rechaza por su huella.
3. **Given** un abono que la red reversa después, **When** se registra, **Then** la conciliación de ese día se
   reabre con motivo y el reverso queda enlazado.

---

### User Story 23 - Recaudar cuentas por cobrar comerciales (Priority: P2)

Los servicios a asociados (funerario, odontología y similares) son ventas de Comercial con su tercero. Si se pagan a
plazos, son una venta a crédito que financia Cartera y el cajero la cobra como cualquier obligación (US3). Lo
vendido a crédito que no pasa a Cartera queda como cuenta por cobrar en Contabilidad: el cajero lo ve en la
pantalla de la persona como «cuentas por cobrar comerciales» con su factura y saldo, y lo cobra en el mismo recibo;
el hecho lleva el tercero y la factura como documento cruce. Antes de activar Cartera, ese recibo es la referencia
con la que Cartera incorpora el cobro provisional.

**Why this priority**: COOFLOPAL cobra servicios así; no se crea un tipo de deuda nuevo ni se cobra como concepto
vario.

**Entrega**: R1. En R0 se cobran las ventas a plazos que financia Cartera (por el contrato de obligaciones).

**Independent Test**: vender a crédito un servicio, cobrarlo en caja junto con una cuota de crédito y comprobar
el saldo de la factura en cero en Contabilidad.

**Acceptance Scenarios**:

1. **Given** un pago mayor que el saldo de la factura, **When** se registra, **Then** se rechaza el excedente o
   se destina a otro producto.
2. **Given** la misma factura cobrada a la vez en dos cajas, **When** confirma la segunda, **Then** se rechaza
   porque el saldo ya no alcanza.
3. **Given** un recibo de cuenta por cobrar anulado, **When** se anula, **Then** el saldo de la factura vuelve en
   Contabilidad por el hecho espejo.
4. **Given** una factura de servicio con la consulta de pendientes sin respuesta, **When** el cajero intenta
   cobrarla, **Then** no la ve ni la cobra como concepto vario; se informa que la consulta no está disponible.

---

### User Story 24 - Salida en vivo de COOFLOPAL: paralelo, ensayo, go/no-go y cuadre provisional (Priority: P1)

Del 11/11 al 27/11 el equipo de COOFLOPAL registra en QA, sobre una copia de su parametrización, los recaudos de
cuotas, aportes y nómina del paralelo con la versión disponible. Del 23/11 al 25/11 corre el ensayo general con R0 y
T0 completas. El 28/11 se verifica la lista de go/no-go y se hace la carga definitiva en producción, que estaba
limpia. El 01/12 se activan Recaudo y Tesorería y después Cartera y Aportes; si la apertura contable todavía no
cuadra, la activación se aprueba con cuadre provisional y fecha límite.

**Why this priority**: es la salida en vivo; sin ensayo, lista ni salida para el cuadre tardío, el 01/12 no tiene
cómo cumplirse.

**Entrega**: R0 y T0.

**Independent Test**: en QA, recorrer la lista de go/no-go con una cooperativa de prueba: completitud de matriz al
100 %, un recibo con cada medio y concepto en cada punto, segundo factor inscrito de cada aprobador, roles y
alcances asignados, activación con cuadre provisional y alerta diaria, y comprobar que el cierre del mes se niega
sin el cuadre definitivo.

**Acceptance Scenarios**:

1. **Given** una condición de la lista en rojo, **When** se intenta la activación en producción, **Then** la
   activación muestra la condición pendiente; seguir exige la aprobación registrada del dueño.
2. **Given** el paralelo en QA, **When** termina, **Then** el informe compara lo registrado con las cifras de
   referencia por día, medio y producto, y explica cada diferencia.
3. **Given** un no-go parcial (Cartera o Aportes sin activar), **When** se decide salir con lo demás, **Then** rige
   el escenario escrito y aprobado en la lista para ese caso; la ventanilla no recibe pagos de un módulo no activo
   salvo lo que admita FR-046.
4. **Given** el cuadre provisional vencido, **When** pasa la fecha límite, **Then** la alerta escala al dueño y al
   contador; la operación diaria no se detiene.

---

### User Story 25 - Conciliación bancaria mínima (Priority: P1)

El conciliador carga el extracto de cada cuenta bancaria propia (archivo con el formato del banco, plantilla
genérica o digitado). El sistema cruza cada movimiento con lo registrado —recibos de banco, remesas, pagos y
cheques de Tesorería, y movimientos contables de la cuenta— por valor, fecha (con días de holgura) y referencia;
el conciliador marca a mano lo que no cruza y emite el informe de partidas conciliatorias del mes.

**Why this priority**: diciembre es el primer mes en vivo y también el cierre del ejercicio; sin conciliación
mínima no hay con qué responder a la revisoría (decisión del dueño).

**Entrega**: T0.

**Independent Test**: cargar un extracto de un mes con 120 movimientos: 110 cruzan solos, 6 se marcan a mano y 4
quedan como partidas (un cheque no cobrado, una consignación en tránsito, un GMF y una comisión aún no
registrados); comprobar el informe con saldo del extracto, partidas y saldo en libros cuadrados.

**Acceptance Scenarios**:

1. **Given** un movimiento ya cruzado, **When** se intenta cruzar con otro registro, **Then** se rechaza: nada se
   cruza dos veces.
2. **Given** un abono del extracto sin registro, **When** el conciliador lo marca, **Then** pasa a Recaudo como
   partida por identificar.
3. **Given** quien preparó o aprobó pagos de esa cuenta en el mes, **When** intenta conciliarla, **Then** se
   rechaza por segregación.
4. **Given** un cheque girado que aparece cobrado, **When** se cruza, **Then** el cheque pasa a «cobrado».
5. **Given** una conciliación cerrada, **When** se corrige, **Then** se reabre con motivo y permiso.

---

### User Story 26 - Controles antifraude de los pagos (Priority: P1)

Antes de que el dinero salga, el sistema protege los tres puntos donde suele desviarse: la cuenta destino del
beneficiario (su alta o cambio exige otra persona, soporte y una espera), las órdenes manuales de Tesorería
(catálogo cerrado, soporte, dos aprobadores, tope) y el archivo de pago (que se coteja contra la respuesta del
banco). Los lotes marcan las cuentas cambiadas hace poco y las cuentas destino compartidas.

**Why this priority**: el desvío por cambio de cuenta y por orden manual es el fraude de pagos más común.

**Entrega**: T0 (cuenta destino y archivo alterado); T1 (órdenes manuales).

**Independent Test**: cambiar la cuenta bancaria de un beneficiario y comprobar que queda pendiente hasta que otra
persona la aprueba con la certificación; incluir una orden suya en un lote dentro del período de espera y comprobar
la marca; cargar una respuesta del banco con una cuenta alterada y comprobar la alerta.

**Acceptance Scenarios**:

1. **Given** una cuenta destino cambiada hace menos de los días configurados, **When** se arma el lote, **Then**
   la orden aparece marcada y su aprobación exige revisarla expresamente.
2. **Given** una misma cuenta destino registrada para dos beneficiarios o igual a la de un usuario interno,
   **When** se registra o se paga, **Then** se alerta.
3. **Given** una orden manual cuyo beneficiario es quien la prepara o la aprueba, **When** se registra, **Then** se
   rechaza.
4. **Given** una orden manual sin la retención que corresponde, **When** se prepara, **Then** se exige la retención
   o se deriva a una causación previa en Contabilidad.

---

### Edge Cases

**Caja**
- **Corte de luz o red a mitad de un recibo.** Nada queda a medias: o el recibo y todas sus aplicaciones o nada;
  al volver, el cajero repite con la misma clave y recibe el recibo si ya quedó. Si la caída dura, rige la
  contingencia por talonario (FR-186).
- **Recibo confirmado y la impresora falla.** El recibo existe; se reimprime marcado «copia».
- **Dos cajeros cobran al mismo deudor a la vez.** El segundo recibe «la distribución cambió» y ve la nueva.
- **Dos recibos concurrentes que tocan créditos y cuentas de la misma persona.** No se interbloquean: los dos
  aplican primero las obligaciones y después las cuentas (FR-170).
- **Sesión olvidada abierta de un día para otro.** Alerta al supervisor; la cierra él con arqueo diferido, con un
  testigo, y la diferencia la aprueba un tercero; los recibos del día siguiente no caen en ella.
- **Cambio de medio después de confirmar** (registrado como efectivo, pagado por transferencia). Reclasificación
  entre medios con soporte cotejado y aprobación de otra persona; el recibo no cambia; queda en el informe de
  excepciones.
- **Billete falso detectado después.** Faltante del arqueo con su motivo y el acta del billete retenido.
- **Cajero que también vende en el punto de venta.** Abre dos sesiones, una en cada caja, que cuadran y cierran
  por su lado (C1 = A); su efectivo suma en el mismo registro de transacciones en efectivo (FR-204).
- **Pago con dos tarjetas y una se rechaza en el datáfono.** El recibo no se confirma hasta que el total de
  medios cuadra.
- **Persona en lista vinculante en la fila.** No se recibe su dinero ni se ejecuta la operación; alerta inmediata
  al oficial (FR-203).

**Contratos**
- **Módulo no activado** (persona de una carga aplicada sin activar). Desde R1, el valor queda como recaudo
  pendiente de aplicar y se aplica en el guardado de la activación (FR-046); en R0, el cajero ve «no habilitado».
- **Fecha valor anterior al corte de la carga.** El contrato la rechaza (ese pago ya debía estar en la carga); el
  dinero recibido queda como saldo a favor del pagador por resolver.
- **Validación previa contable sin respuesta.** En pantalla bloquea (como en Cartera); en los procesos no
  interactivos, avisa y la línea queda para reproceso.
- **Excedente en un pago no interactivo** (banco, pagaduría). Queda como saldo a favor del pagador en Recaudo
  por resolver: devolver por orden de pago (o egreso desde R1) o aplicar con un recibo nuevo.
- **Aviso de reverso de un pago que Recaudo no reconoce.** Queda en la bandeja como rechazado con su motivo.
- **Retiro autorizado cuando el titular cumple 18 años con el representante en ventanilla.** Captaciones lo
  rechaza; la caja no entrega.
- **Anular un recibo con una aplicación que el módulo ya reversó.** La anulación omite esa aplicación (FR-194).

**Bancos y pagadurías**
- **El mismo abono aparece en el extracto y en el archivo del convenio.** Se identifica una sola vez (huella del
  movimiento bancario, FR-197).
- **Un consignante paga dos personas en una consignación.** Se divide al identificar.
- **Partida identificada a una persona fallecida o retirada.** Se aplica si el módulo lo admite; si no, se
  devuelve al consignante por orden de pago.
- **La pagaduría gira antes de responder.** El giro queda como saldo de la pagaduría por aplicar hasta la
  respuesta.
- **La pagaduría gira el total de dos períodos.** Se aplica por período, en el orden de los archivos.
- **Respuesta de pagaduría con un concepto o empleado sin equivalencia.** No aplicados con motivo, sin bloquear
  lo demás.
- **Una línea de un giro de 5.000 rechazada por un contrato.** Sólo esa línea queda fuera, con su motivo (FR-171).
- **Pagaduría que responde en PDF o Excel libre.** Se carga con la plantilla genérica o a mano (FR-201).

**Tesorería**
- **Orden anulada mientras el banco procesa el archivo.** La orden sigue hasta conocer si se giró; si se giró,
  se informa «girada» y el origen la gestiona como pago.
- **El banco devuelve el abono días después de informada «girada».** No se informa «rechazada» (el contrato no lo
  admite): el dinero entra por Recaudo como giro devuelto enlazado a la orden y el origen emite una orden nueva
  (FR-199).
- **Cuenta destino del beneficiario cambia antes de girar.** La orden es del origen: Tesorería no la edita; el
  origen anula y emite otra. Un cambio de cuenta en el maestro de la persona sigue el doble control de FR-210.
- **Formato del banco cambia.** Versión nueva del formato con vigencia; los archivos ya generados conservan el
  suyo.
- **Banco sin formato de pagos.** Formato genérico o forma «portal del banco», con resultado manual con soporte
  (FR-213).
- **Cheque girado que caduca.** Alerta, anulación con permiso y «cheque no cobrado».
- **GMF real distinto del estimado.** Manda el del extracto; la diferencia se explica en la conciliación.
- **Dispersión de nómina desde una cuenta de Tesorería.** Nómina paga con su propio archivo; Tesorería la ve
  como compromiso y la concilia, no la vuelve a pagar.
- **Pago de un proveedor en diciembre.** Sale por la Tesorería heredada o por Contabilidad hasta T2 y se ve en la
  conciliación mínima como movimiento contable de la cuenta (FR-209).

**Puesta en marcha**
- **Partida por identificar heredada sin fecha.** Se rechaza la fila: la fecha es obligatoria para la
  antigüedad.
- **Cheque en tránsito heredado de una chequera no registrada.** Se carga la chequera primero.
- **Pagos recibidos entre el corte y la activación.** En producción, el plan los deja en el sistema anterior y
  entran en el ajuste de la carga; con R1, si llegan a Recaudo, quedan como recaudo pendiente de aplicar y se
  aplican en la activación con su fecha valor.
- **Apertura contable sin conciliar el día de la activación.** Cuadre provisional con fecha límite (FR-228).
- **Fila de un doble de prueba en una cooperativa de QA.** Se retira al desplegar los módulos reales (FR-223).

## Requirements *(mandatory)*

Cada requisito lleva su entrega entre paréntesis (ver «Entregas»). Los FR-170 en adelante se agregaron el
2026-10-22 tras la revisión adversarial y van en la sección que les corresponde.

### Functional Requirements

#### Transversales

- **FR-001 · Un solo camino del dinero.** *(R0, T0)* Todo dinero que entra de una persona o de un tercero entra por
  un **recibo** de Recaudo (de caja, de banco, de convenio o de pagaduría); todo dinero que sale por la caja sale
  por un **egreso** de Recaudo, y todo lo que sale de un banco, por un **pago** de Tesorería. Ningún módulo cambia
  el saldo de otro: lo pide por su contrato. Hasta T2 rige la transición de FR-209 para los pagos a proveedores y
  los egresos contables manuales. **Desde T2** (fase 42, 2026-10-26) los pagos a proveedores y de cuentas por pagar
  contables también van por Tesorería: las facturas a crédito llegan de Comercial a la bandeja y las cuentas por pagar
  se convierten en órdenes en `/tesoreria/cuentas-por-pagar`.
- **FR-002 · Inmutabilidad.** *(R0, T0)* Recibos, egresos, sesiones cerradas, lotes y archivos de pago,
  resultados de giro y conciliaciones cerradas no se editan ni se borran. Se corrigen con un documento nuevo
  enlazado al original (anulación, ajuste, reverso, reapertura con motivo). Un documento anulado sigue visible con
  su anulación.
- **FR-003 · Sólo por contratos.** *(R0, T0)* Recaudo y Tesorería no conocen las reglas internas de Cartera,
  Captaciones, Comercial, Nómina ni Contabilidad, ni leen sus datos: usan sus contratos, consultas y mensajes
  vigentes. Lo que falta se pide como enmienda (FR-164 a FR-169), nunca con un atajo.
- **FR-004 · Sin asientos ni cuentas.** *(R0, T0)* Recaudo y Tesorería no escriben asientos: emiten hechos a
  Contabilidad de forma atómica con su operación (o los dos o ninguno), y ningún hecho lleva una cuenta contable
  (sección R).
- **FR-005 · Idempotencia.** *(R0, T0)* Todo recibo, egreso, aplicación, anulación, lote, archivo, resultado de
  giro y mensaje lleva una clave de origen. Repetir con la misma clave y la misma huella (FR-172) devuelve el
  original; con otra huella se rechaza sin efectos.
- **FR-006 · Claves por módulo.** *(R0, T0)* Las claves que Recaudo y Tesorería pasan a los contratos empiezan con
  su código de módulo y respetan la gramática que fijaron los contratos (una clave por porción de cada aplicación
  de cada recibo, FR-188; para el giro de una pagaduría, el giro y la línea que cubre; derivadas para los
  reversos).
- **FR-007 · Concurrencia.** *(R0, T0)* Una sesión abierta por caja (y por cajero si es exclusivo); numeración sin
  huecos por concurrencia y asignada al final de la confirmación; dos operaciones sobre la misma persona u
  obligación siguen el orden fijo de FR-170; una orden de pago está a lo sumo en un lote no anulado.
- **FR-008 · Fechas.** *(R0, T0)* Fecha local de Colombia. Cada documento lleva fecha de registro (instante),
  fecha valor (caja: hoy; banco, convenio o pagaduría: la del movimiento o del descuento) y fecha contable (la del
  período abierto, que fija Contabilidad). En caja no se registra fecha valor distinta de hoy salvo FR-047 y
  FR-192.
- **FR-009 · Parametrización con vigencia y sin valores legales en el código.** *(R0, T0)* Umbrales de efectivo,
  plazos, tolerancias, topes, tarifas y exenciones de GMF, días de canje, días sin resultado y demás valores viven
  en parámetros por cooperativa con vigencia y fuente; lo sembrado sin validar va marcado «pendiente de validar».
  Ningún valor legal queda fijo en Recaudo ni en Tesorería, y eso se verifica de forma automática.
- **FR-010 · Plazos regulatorios informativos.** *(R0, T0)* Los plazos de reportes, de giro de pagadurías, de
  presentación de cheques y de caducidad se muestran y se alertan; no bloquean operaciones salvo que la
  cooperativa lo configure. Lo que la norma impone como bloqueo —no ejecutar una operación con una persona de una
  lista vinculante (FR-203), no disponer del fondo de liquidez fuera de sus causales (FR-207)— bloquea siempre.
- **FR-011 · Permisos y alcance.** *(R0, T0)* Permisos propios por acción (consultar, registrar, anular, aprobar,
  exportar, configurar) en Recaudo y en Tesorería, sembrados completos con la primera entrega. El alcance limita
  por **oficina, punto y caja** (Recaudo), por **cuenta bancaria propia** (Tesorería) y según FR-175 en los canales
  sin caja; falla cerrado y responde como ante lo inexistente. El alcance de Tesorería por cuenta bancaria propia es
  éste (FR-106 remite aquí).
- **FR-012 · Segregación.** *(R0, T0)* Quien registra no aprueba lo suyo: anulaciones, diferencias de arqueo,
  reclasificaciones entre medios, fechas valor anteriores, partidas clasificadas a otro concepto, lotes de pago,
  cambios de cuenta de un beneficiario, órdenes manuales, notas bancarias, traslados, usos del fondo de liquidez y
  reaperturas. Por defecto, quien prepara un lote no lo aprueba (dos personas); que una tercera registre el
  resultado es configurable. Quien prepara o aprueba pagos de una cuenta no la concilia (FR-217). La comparación es
  por persona.
- **FR-013 · Aprobaciones multinivel por monto.** *(R0, T0)* Las operaciones sobre la atribución del usuario usan
  las aprobaciones de la plataforma: límites de monto por permiso, niveles sucesivos hasta cubrir el monto,
  aprobador **presente con segundo factor** en la caja, aprobación diferida en Tesorería; la operación no mueve
  dinero hasta aprobarse.
- **FR-014 · Auditoría.** *(R0, T0)* Toda operación, rechazo, aprobación y lectura sensible (lo que debe y paga una
  persona, saldos bancarios, cambios de cuenta de un beneficiario, exportaciones, reportes de transacciones) queda
  en la auditoría con cadena de sellos, con la conservación de FR-178. Una corrida no interactiva (extracto, giro de
  pagaduría) deja un evento resumen que lista cada recibo aplicado (su `PublicId` y su huella), verificable recibo por
  recibo, y un evento por línea rechazada.
- **FR-015 · Errores visibles.** *(R0, T0)* Mensajes en español con código por módulo; cada rechazo de un contrato
  se muestra por ítem con su motivo; las fallas de consumo de mensajes quedan en la bandeja con reproceso; las
  tareas programadas alertan su falla.
- **FR-016 · Persona maestra.** *(R0, T0)* Pagadores, beneficiarios, quienes efectúan, giradores, pagadurías y
  cajeros son personas del maestro. El alta de un pagador ocasional se hace desde el recibo en un paso mínimo
  (documento, nombre y autorización de datos) con el buscador común, se completa después y, si el pago supera el
  umbral de declaración, alerta al oficial.
- **FR-017 · Identificadores.** *(R0, T0)* Documentos, órdenes, lotes y partidas se exponen por identificador
  público y número visible.
- **FR-018 · Uso.** *(R0, T0)* Un recibo de caja se completa en tres pasos o menos (persona, qué paga, medios); toda
  acción de la caja tiene atajo de teclado y la búsqueda acepta el lector; toda pantalla muestra el indicador de
  carga por zona; disponible en la aplicación web y en la instalable.
- **FR-019 · Por cooperativa.** *(R0, T0)* Todo vive en la base de cada cooperativa; las tareas programadas corren
  por cooperativa como «Proceso de la cooperativa» y sólo proponen, preparan y alertan (FR-174).
- **FR-020 · Avisos.** *(R1, T1)* El recibo, el egreso y el soporte de pago se envían por correo electrónico al
  pagador o beneficiario sólo con la autorización del canal y con el contenido de FR-177; SMS y mensajería siguen
  simulados hasta que haya proveedor.
- **FR-170 · Orden fijo de cerrojos entre contratos.** *(R0)* Dentro de una operación de Recaudo que toca los dos
  contratos, primero se aplican (o se reversan) todas las obligaciones de Cartera y después todas las cuentas de
  Captaciones, sin intercalar; dentro de cada contrato rige el orden que fija el módulo dueño (la persona antes que
  sus créditos y sus cuentas). Ninguna operación toma una persona después de una cuenta suya. Una verificación
  automática lo fija.
- **FR-171 · Unidad atómica en los procesos no interactivos.** *(R0)* En extractos, giros de pagaduría, archivos de
  convenio y aplicaciones por lote, la unidad es la línea (o el recibo que la cubre), no el archivo: una línea que
  un contrato rechaza no tumba las demás; queda en «no aplicados», como saldo de la pagaduría, como saldo a favor
  del pagador o como partida, con su motivo, y se reprocesa con la misma clave.
- **FR-172 · Huella de una operación.** *(R0, T0)* La huella es el contenido de negocio: en un recibo, pagador,
  aplicaciones (producto, modalidad, valor) y medios; en un egreso, la autorización y el valor; en un giro, la
  orden, la cuenta y el valor. La simulación, el token del aprobador y el instante no forman parte de la huella:
  reconfirmar tras «la distribución cambió» o tras una aprobación usa la misma clave. Cambiar aplicaciones o medios
  es otra operación con otra clave. Las claves se conservan mientras se conserve el documento, sin vencimiento.
- **FR-173 · Versión de los parámetros.** *(R0, T0)* Una operación usa la versión vigente al confirmar (o a la fecha
  valor, cuando el parámetro lo diga); si la versión cambió desde la simulación y cambia lo exigido, se vuelve a
  simular. No hay vigencias retroactivas sobre lo confirmado.
- **FR-174 · Todo pago a nombre de una persona.** *(R0, T0)* Ningún proceso programado llama a los contratos ni
  informa un resultado del giro: todo pago, consignación, reverso y resultado se hace a nombre de la persona que
  carga, confirma o aprueba, al cargar o confirmar. Los procesos sólo proponen, preparan y alertan.
- **FR-175 · Alcance de los canales sin caja y cobro entre oficinas.** *(R0, T0)* Recibos de banco, convenio y
  pagaduría, partidas y saldos a favor de pagadores toman el alcance de la cuenta bancaria propia, del convenio o
  de la pagaduría. Un cajero cobra a una persona de cualquier oficina, salvo que el parámetro lo restrinja a la
  suya; si el contrato del módulo rechaza por su propio alcance, se muestra su motivo.
- **FR-176 · Roles sembrados.** *(R0, T0)* Cajero, supervisor de caja, analista de recaudo, tesorero, aprobador,
  conciliador y oficial de cumplimiento vienen como roles con sus permisos de Recaudo y Tesorería y con los permisos
  de Cartera y Captaciones que los contratos verifican sobre el usuario que actúa (fecha valor en un mes cerrado,
  consignaciones, retiros), con sus montos; cada cooperativa los ajusta. Activar Recaudo exige al menos un cajero y
  un supervisor con esos permisos.
- **FR-177 · Habeas Data con pagador tercero.** *(R0)* Cuando paga un tercero, la pantalla que él ve, el recibo y
  el aviso al pagador muestran sólo lo que paga (producto enmascarado y valor), sin saldos ni mora del beneficiario,
  salvo autorización de éste. La autorización de datos informa la finalidad y la conservación de la declaración de
  origen de fondos y sus soportes.
- **FR-178 · Conservación.** *(R0, T0)* El plazo de conservación de documentos, soportes y auditoría es un parámetro
  con fuente (por defecto diez años, Código de Comercio, «pendiente de validar»), nunca menor que el mínimo de cinco
  años de la constitución; una solicitud de supresión no alcanza lo que la norma obliga a conservar.

#### A · Puntos, cajas, medios y catálogos

- **FR-021 · Puntos de recaudo y cajas.** *(R0)* Cada oficina tiene puntos de recaudo y cada punto, cajas con
  código, nombre, base y estado con vigencia. La caja de Recaudo y la del punto de venta de la 012 son **dos cajas
  separadas** que comparten el catálogo de medios de pago, las denominaciones y el método de arqueo; un cajero que
  haga las dos cosas abre dos sesiones y cada una cuadra y cierra por su lado (Clarifications, C1 = A).
- **FR-022 · Cajeros.** *(R0)* Las cajas tienen cajeros asignados (usuarios con persona); el parámetro «cajero
  exclusivo» impide que un cajero tenga dos sesiones abiertas en Recaudo.
- **FR-023 · Medios de pago.** *(R0 efectivo y transferencia, cheque por parámetro; R1 los demás)* Recaudo usa el
  **catálogo común de medios de pago** de la plataforma (el mismo del punto de venta), con su clase fija, red,
  adquirente, datáfonos, banco y cuenta destino como dato, reglas de captura, método y tolerancia de arqueo.
  Recaudo agrega **en qué puntos de recaudo** se ofrece cada medio. PSE y las pasarelas usan la clase
  transferencia; los pagos con tarjeta por pasarela, la clase de tarjeta. No se crean clases nuevas.
- **FR-024 · Medios no admitidos.** *(R0)* Las clases de crédito (a asociado, comercial) no se ofrecen en Recaudo.
  Un medio inactivo no se ofrece; un medio con pagos no se borra.
- **FR-025 · Denominaciones** *(R0)* de efectivo del catálogo común, para apertura, arqueo y remesas.
- **FR-026 · Tipos de documento.** *(R0)* Tipos de recibo (caja, banco, convenio, pagaduría, en línea, contingencia),
  de egreso, de anulación y de movimiento de caja, con consecutivo de la cooperativa (prefijo por punto opcional),
  sin huecos.
- **FR-027 · Conceptos varios.** *(R0)* Catálogo de conceptos de ingreso y egreso no ligados a productos
  (reintegros, rendimientos, devoluciones a terceros, giro devuelto, sobrantes), con código, nombre, si exige
  tercero, documento cruce o factura (FR-179), si es ingreso o egreso y aprobación; la matriz contable los lleva a
  su cuenta. Reemplaza los conceptos de tesorería heredados cuando éstos se retiren.
- **FR-028 · Base y topes de caja.** *(R0)* Modo de base (fondo fijo o base del día, sellado al abrir), tope de
  efectivo en caja con aviso de traslado a bóveda, tope de bloqueo (FR-181) y tope de egreso en efectivo por caja y
  día.
- **FR-179 · Conceptos que exigen factura.** *(R0)* Un concepto de ingreso gravado (venta de formularios, cargo por
  cheque devuelto cuando es un servicio) lleva el atributo «exige factura»: no se cobra sin la factura electrónica
  de Comercial como documento cruce, o se origina la venta en Comercial. Las cuentas por cobrar comerciales nunca se
  cobran como concepto vario (FR-049).
- **FR-180 · Caja principal como bóveda.** *(R0)* Una oficina sin bóveda configura su caja principal como bóveda,
  como modo explícito: abastece y recibe de las demás cajas y hace las remesas.
- **FR-181 · Tope de bloqueo del efectivo.** *(R0)* Además del tope con aviso, un segundo tope por caja (por ejemplo,
  el que cubre la póliza de manejo) bloquea los recibos en efectivo hasta trasladar a bóveda; sin valor
  configurado, no bloquea.

#### B · Sesión de caja y bóveda

- **FR-029 · Apertura.** *(R0)* La sesión se abre con la base contada por denominaciones (desde la bóveda o el
  cierre anterior, según el modo); sin sesión abierta la caja no recauda ni entrega.
- **FR-030 · Movimientos de caja.** *(R0)* Abastecimiento desde bóveda, retiro parcial a bóveda, traslado entre
  cajas y **reclasificación entre medios** de un pago ya confirmado, como documentos con motivo y la aprobación que
  diga su política; la reclasificación no toca el recibo y mueve lo esperado de un medio a otro. Pasar de efectivo
  a otro medio exige el soporte cotejado (comprobante del datáfono o referencia bancaria) y la aprobación de otra
  persona, y queda en el informe de excepciones.
- **FR-031 · Arqueo por medio.** *(R0)* Al cerrar, por cada medio según su método: efectivo por denominaciones o
  total, tarjetas por lote de cada datáfono (cotejo por referencia opcional), transferencias, consignaciones y en
  línea por referencias, cheques por relación. **Arqueo ciego** configurable, activado por defecto.
- **FR-032 · Diferencias.** *(R0)* Toda diferencia lleva motivo; sobre la tolerancia del medio exige además
  aprobación de otra persona. Faltante: a cargo del cajero (FR-183) o gasto; sobrante: pasivo «sobrantes de caja»
  (FR-183); según parámetro. Emite su hecho.
- **FR-033 · Cierre de sesión y del día.** *(R0)* El cierre de sesión emite el informe por medio (esperado,
  contado, diferencia); el cierre del día del punto consolida sus sesiones y sólo procede con todas cerradas y sus
  diferencias resueltas (por el supervisor o su suplente, FR-184). Un cierre no se reabre: se corrige con un ajuste
  aprobado enlazado.
- **FR-034 · Sesión olvidada.** *(R0)* Una sesión abierta al pasar el día alerta al supervisor, que la cierra con
  arqueo diferido: cuenta él con un testigo y la diferencia la aprueba un tercero; no recibe operaciones del día
  siguiente.
- **FR-035 · Bóveda de oficina.** *(R0, T0)* Cada oficina tiene una bóveda (o su caja principal, FR-180) con saldo
  de efectivo por denominación, que abastece y recibe de las cajas y envía o recibe **remesas** de un banco (con
  Tesorería, FR-105); el efectivo en tránsito se ve hasta confirmarse.
- **FR-036 · Cuadre diario.** *(R0)* El efectivo de cajas, bóveda y tránsito de cada oficina cuadra con el saldo de
  su cuenta de caja en Contabilidad **más lo emitido y pendiente de lote**; corre después del lote del día y la
  diferencia se muestra y se explica.
- **FR-182 · Doble custodia y arqueos sorpresivos.** *(R0; T0 las chequeras)* Apertura, conteo y movimientos de
  bóveda con dos personas (configurable, activo por defecto); arqueo sorpresivo de sesión y de bóveda con acta;
  remesas con acta y sello de bolsa; billete falso retenido con acta; custodia registrada de chequeras en blanco y
  sellos.
- **FR-183 · Faltantes y sobrantes.** *(R0)* Un faltante a cargo del cajero es una cuenta por cobrar a su persona,
  con su hecho y aviso al cajero; su descuento por Nómina exige la autorización escrita del cajero para ese caso
  adjunta (Código Sustantivo del Trabajo, art. 149); sin ella, se gestiona por otra vía. Un sobrante queda como
  pasivo «sobrantes de caja» hasta el plazo configurable y después pasa a ingreso sólo con aprobación.
- **FR-184 · Suplencia y lote del día.** *(R0)* El supervisor tiene un suplente o una delegación con vigencia para
  aprobar diferencias y cerrar el día. Un punto con el día abierto no detiene el lote contable de los demás: el lote
  corre con las sesiones cerradas y los recibos de las abiertas entran en el siguiente; se alerta.
- **FR-185 · Hora de corte y orden de las tareas.** *(R0)* Cada cooperativa fija la hora de corte del día de
  Recaudo (`Recaudo.HoraDeCorte`). Lo pagado en el día cuenta antes de que el ciclo diario de Cartera calcule la mora
  por una **restricción de configuración comprobada**, no por dependencias entre tareas: la hora de corte tiene que
  caer antes del inicio del ciclo de Cartera (`Lending:Process:StartTimeLocal`, 00:30 por defecto), contadas las dos en
  la línea del día de operación (si el inicio del ciclo es de madrugada, se compara contra el día siguiente). Una
  versión del parámetro que no la cumpla se rechaza, y la activación y la lista de go/no-go la verifican. El lote del
  día de Recaudo corre tras la hora de corte y el cuadre diario después del lote de su cooperativa (cada tarea mira la
  marca de corrida de la anterior); `ProgramadorDeTareas` no declara dependencias.
- **FR-186 · Contingencia de la caja.** *(R0)* Si la caja no puede operar (sin sistema o sin red), el punto usa un
  talonario prenumerado registrado. Los recibos se digitan después con la fecha valor del talonario, dentro de la
  ventana de fecha valor del contrato, con permiso propio, marca «contingencia» y la imagen del talonario; un
  informe muestra los recibos de contingencia por digitar y los números del talonario sin usar.

#### C · Recibo de caja multiproducto

- **FR-037 · Lo que una persona puede pagar.** *(R0; R1 codeudor y cuentas por cobrar comerciales)* Al buscar a una
  persona (por documento, lector o número de obligación), Recaudo muestra en una pantalla, por los contratos: sus
  obligaciones (como deudor y, a pedido, como codeudor) con exigible hoy, cuota del ciclo, vencido, total para
  cancelar, anticipo, saldo a favor, modalidades admitidas y bloqueos; sus cuotas exigibles de aportes, ahorro
  permanente y contractual; sus cuentas para consignar con su estado; sus cuentas por cobrar comerciales
  pendientes (FR-049); y los conceptos varios. Leerlo es una lectura sensible auditada.
- **FR-038 · Créditos.** *(R0)* Por obligación, el cajero elige la modalidad admitida (cancelación, vencido, cuota
  del ciclo, anticipada, abono extraordinario, cuota específica, valor libre) y el valor; el abono extraordinario
  exige la **elección del deudor** entre reducir plazo o cuota (Ley 2032), con las dos alternativas a la vista. Se
  simula con Cartera y se aplica en forma interactiva con esa simulación.
- **FR-039 · Captaciones y aportes.** *(R0)* Por cuenta, consignación libre, cuotas exigibles en orden de
  vencimiento o cuotas específicas, y fondeo de un CDAT por su valor exacto; se simula con Captaciones (disponible,
  canje, efectivo, atribución, cuotas cubiertas) y se aplica con esa simulación.
- **FR-040 · Pagador y beneficiarios.** *(R0)* El pagador puede ser la persona o un tercero; el recibo sale a nombre
  del pagador y lista, por aplicación, el beneficiario y el producto, con la restricción de FR-177. Los contratos
  reciben al pagador como tercero. Una aseguradora puede ser pagadora en el canal banco (FR-200).
- **FR-041 · Medios combinados.** *(R0)* Un recibo se paga con cualquier combinación de medios ofrecidos en el
  punto, imputados a las aplicaciones según FR-188; cada medio con valor y la referencia que exija; vueltas sólo en
  efectivo; un bono de número único no se usa dos veces. Total de medios = total aplicado + vueltas.
- **FR-042 · Todo o nada.** *(R0)* Confirmar un recibo registra de forma atómica el recibo, sus medios, todas sus
  aplicaciones (cada una por su contrato, dentro de la operación de Recaudo y en el orden de FR-170) y sus hechos.
  Si un contrato rechaza un ítem, no queda nada, no se consume número y se muestra el motivo por ítem; si una
  distribución cambió, se muestra la nueva y se pide confirmar de nuevo con la misma clave.
- **FR-043 · Excedentes.** *(R0)* Lo que un contrato no aplica (excedente) se muestra antes de confirmar en caja
  para ajustar el valor o destinarlo; en las formas no interactivas queda como **saldo a favor del pagador** en
  Recaudo, por resolver (FR-054).
- **FR-044 · El recibo.** *(R0)* Número, fecha y hora, oficina, punto, caja, cajero, pagador, quien efectúa si no es
  el pagador, canal, aplicaciones con el comprobante y el número que devolvió cada módulo, la imputación de medios,
  la elección del deudor en un abono extraordinario (FR-190), medios con referencias, vueltas, avisos de firmeza y
  la declaración de origen de fondos si la hubo. Inmutable.
- **FR-045 · Entrega del recibo.** *(R0; R1 el correo)* Impresión en tirilla o carta, reimpresión marcada «copia»,
  PDF con código de verificación y envío por correo con autorización (FR-020).
- **FR-046 · Recaudo pendiente de aplicar.** *(R1)* Si un contrato responde «no habilitado» para una persona
  incluida en una carga de migración aplicada y no activada (Captaciones lo informa; Cartera, por la consulta de la
  enmienda FR-164), el valor queda como **recaudo pendiente de aplicar** por persona y producto, por valor libre, y
  se aplica **en el guardado de la activación o ese mismo día**, por una persona con permiso que elige la modalidad,
  con su fecha valor. Fuera de ese caso el ítem se rechaza con el aviso del módulo. En R0 no existe: Recaudo y
  Tesorería se activan antes que Cartera y Aportes el mismo día y la ventanilla abre después.
- **FR-047 · Fecha valor en caja.** *(R0)* Hoy; una fecha anterior exige permiso propio, los controles de FR-192 y
  respeta las reglas de fecha del contrato (incluido el permiso con monto para meses cerrados).
- **FR-048 · Firmeza.** *(R0)* El recibo informa «sujeta a firmeza hasta» la fecha que devuelva el módulo cuando
  una cancelación se paga con un medio no inmediato.
- **FR-049 · Cuentas por cobrar comerciales.** *(R1)* Los servicios a asociados son ventas de Comercial con su
  tercero; a plazos, una venta a crédito que financia Cartera y que se cobra por el contrato de obligaciones (R0).
  Lo vendido a crédito que no pasa a Cartera se consulta como pendiente por persona y factura (enmienda FR-167) y se
  cobra en el recibo; el hecho lleva tercero y factura como documento cruce; no se cobra más que el saldo ni como
  concepto vario. Antes de activar Cartera, el recibo es la referencia contable con que Cartera incorpora el cobro
  provisional al activarse, y un informe lo entrega para esa carga.
- **FR-050 · Conceptos varios.** *(R0)* Un recibo puede incluir conceptos varios con tercero, documento cruce o
  factura cuando el concepto lo exija (FR-179).
- **FR-187 · Pagar lo exigible en una acción.** *(R1)* El cajero preselecciona todo lo exigible de todos los
  productos de la persona y el sistema corre todas las simulaciones en una sola acción; luego puede quitar ítems.
- **FR-188 · Imputación de los medios a las aplicaciones.** *(R0)* Con varios medios, cada aplicación recibe una o
  más porciones por clase de medio, y cada porción es un ítem del contrato con su clave derivada. El orden de
  imputación es configurable; por defecto, los medios inmediatos cubren primero las aplicaciones en el orden de la
  pantalla y el cheque cubre lo restante; el cajero puede cambiarla antes de confirmar y el recibo la muestra. Los
  umbrales de efectivo y la firmeza se evalúan por porción; un cheque devuelto reversa sólo las porciones pagadas con
  él.
- **FR-189 · Ciclo de vida del recibo.** *(R0)* Antes del recibo, una **operación de caja** está «en preparación»
  (simulada, sin efectos), «bloqueada» (lista vinculante: no se recibe el dinero ni hay recibo, FR-203) o «retenida»
  (lista no vinculante con retención: sin aplicaciones, con el dinero en custodia de la sesión y su hecho, hasta que
  el oficial la libere —se confirma con una simulación nueva— o se devuelva el dinero por egreso). Una
  **consignación reportada** está «por verificar» hasta que el extracto la confirma (recibo de canal banco) o vence
  y alerta. El **recibo** está «vigente», «anulación pendiente de aprobación» (vuelve a «vigente» si se rechaza) o
  «anulado». Cada **aplicación** está «aplicada», «reversada por el módulo» o «anulada». Cada transición tiene quién
  la hace, su permiso y su hecho.
- **FR-190 · Elección del deudor en el recibo.** *(R0)* El recibo imprime y guarda la elección del deudor (reducir
  plazo o cuota) y, cuando la modalidad «anticipada» no abona a capital, su aceptación expresa del aviso (Ley 2032).
- **FR-191 · Quien efectúa la transacción.** *(R0)* En efectivo sobre el umbral de declaración, se registran
  documento y nombre de quien entrega físicamente el dinero cuando no es el pagador; en los egresos, de quien recibe
  (apoderado o autorizado). Esa persona se consulta contra las listas (FR-203).
- **FR-192 · Fecha valor anterior en caja.** *(R0)* Además del permiso propio, exige aprobador presente distinto del
  cajero, motivo, soporte y un tope de días configurable; un informe de excepciones de fecha valor y de anulaciones
  con reemisión queda para la revisoría.

#### D · Egresos de caja

- **FR-051 · Retiro de ahorro.** *(R1)* El cajero verifica la identidad y las firmas, simula con Captaciones,
  obtiene el aprobador si la atribución lo pide, y el retiro autorizado se entrega **en efectivo** con un **egreso**
  firmado, en una sola operación con la autorización; sin efectivo suficiente en la caja se rechaza antes de pedir
  la autorización. Un retiro en cheque o transferencia sale por orden de pago a Tesorería, que gira el cheque de su
  chequera.
- **FR-052 · Cancelación con entrega.** *(R1)* Cuando Captaciones habilite la cancelación de una cuenta con entrega
  por el contrato, la caja la entrega con el mismo egreso; mientras responda «no disponible», se hace por la ruta de
  Captaciones y la entrega por orden de pago.
- **FR-053 · Saldos a favor y anticipos de Cartera.** *(R0 por orden de pago; R1 en efectivo)* Se resuelven
  «devolver por el recaudador»: por **orden de pago** a Tesorería (transferencia o cheque) o en efectivo por egreso,
  con el soporte que pida el parámetro.
- **FR-054 · Saldos a favor en Recaudo.** *(R0; R1 la devolución en efectivo)* Los excedentes de formas no
  interactivas, los saldos de pagadurías, los recibos anulados después del cierre de su sesión (FR-058) y los
  reversos de módulos con dinero recibido (FR-195) quedan como saldo a favor de un pagador por resolver: devolver
  (orden de pago o egreso) o aplicar con un recibo nuevo; con antigüedad y alerta.
- **FR-055 · Egresos varios.** *(R1)* Por concepto, apagados por defecto, con aprobación y tope; la caja menor queda
  fuera de alcance.
- **FR-056 · Efectivo entregado.** *(R1)* Cuenta para los topes de la caja y para el control de efectivo (sección
  J).
- **FR-193 · Devolución de aportes al retiro.** *(T0)* Véase FR-114: sale por la orden de giro de Captaciones; en
  efectivo, sólo como un retiro por el contrato de cuentas que entrega la caja (R1).

#### E · Anulación y reversos

- **FR-057 · Anulación de recibo.** *(R0)* Se anula el recibo entero, con motivo, por un **documento de anulación**
  numerado que reversa cada aplicación vigente (FR-194) por su contrato con la clave derivada, todo o nada y en el
  orden de FR-170. En la sesión abierta y el mismo día: lo pide el cajero y lo aprueba otro; después: con permiso
  propio y aprobación de otro. No hay anulación parcial: se anula y se emite otro recibo.
- **FR-058 · Dinero de un recibo anulado.** *(R0)* Si la anulación ocurre en la sesión abierta donde se recibió el
  dinero, baja lo esperado de esa sesión. Si la sesión ya cerró, la anulación **no toca ninguna sesión**: deja un
  saldo a favor del pagador (FR-054) y el dinero sólo se mueve con el egreso o la orden de pago que lo devuelve. Si
  el medio era electrónico ya abonado, la devolución va por orden de pago.
- **FR-059 · Reverso por el módulo.** *(R0)* Al recibir «pago reversado por Cartera» o «consignación reversada por
  Cartera», Recaudo registra el documento de aplicación reversada (FR-195) enlazado al recibo, que no cambia, y alerta
  al supervisor; nunca devuelve ni reaplica solo.
- **FR-060 · Cheque devuelto.** *(R0)* El cheque devuelto se registra siempre en Recaudo, nunca desde la pantalla de
  un módulo. Registrar la devolución (causal) reversa por los contratos, en una sola operación, las porciones de
  cada aplicación pagadas con ese cheque (FR-188); en captaciones, aunque deje sobregiro técnico con bloqueo; da de
  baja el medio sin saldo a favor (el dinero no llegó), avisa al pagador y alerta. Lo que ya se haya resuelto con
  ese dinero (una devolución entregada) queda como cuenta por cobrar al pagador con alerta.
- **FR-061 · Anulación de egreso.** *(R1)* Reversa el retiro o la resolución por su contrato y reingresa el dinero a
  la caja; sólo si el dinero vuelve, con aprobación.
- **FR-062 · Lo de Nómina.** *(R0)* Los descuentos de nómina propia no se anulan ni se reversan desde Recaudo.
- **FR-063 · Avisos repetidos o ajenos.** *(R0)* Un aviso de reverso repetido no cambia nada; uno de una aplicación
  que Recaudo no conoce queda rechazado en la bandeja con su motivo.
- **FR-064 · Anulación tras el cierre contable.** *(R0)* La anulación se fecha en el período abierto; el original no
  se reescribe.
- **FR-194 · Anulación con aplicaciones ya reversadas.** *(R0)* La anulación de un recibo omite las aplicaciones
  «reversadas por el módulo» o «anuladas» y reversa sólo las vigentes; si no queda ninguna vigente, la anulación
  sólo cambia el estado del recibo y resuelve el dinero restante.
- **FR-195 · Documento de aplicación reversada por el módulo.** *(R0)* Distingue la causa que informa el módulo:
  **dinero no recibido** (abono o medio que nunca llegó): sin saldo a favor, con baja del medio; **dinero recibido**
  (corrección del módulo): saldo a favor del pagador por resolver. Una persona confirma la causa cuando el aviso no
  la trae.

#### F · Bancos: consignaciones y transferencias recibidas

- **FR-065 · Extractos y archivos de movimientos.** *(R0)* Por cuenta bancaria de recaudo, se carga el extracto o el
  archivo de movimientos con el formato del banco como dato versionado (motor de archivos planos, en lectura), con
  la plantilla genérica o a mano (FR-196); se reconocen los abonos y cargos.
- **FR-066 · Sin duplicados.** *(R0)* Cada movimiento tiene una huella definida por formato (FR-197); un movimiento
  ya cargado no se carga otra vez.
- **FR-067 · Identificación.** *(R0 manual; R1 por reglas)* Reglas configurables proponen el destino de cada abono:
  referencia de pago (de producto o de persona), documento del consignante, consignación reportada en caja o valor
  exacto de un exigible. La propuesta la confirma una persona; si el parámetro lo permite, lo propuesto con
  referencia exacta y dígito válido se aplica al cargar el extracto, a nombre de quien lo carga (FR-174). Ningún
  proceso programado aplica.
- **FR-068 · Aplicación de lo identificado.** *(R0)* Cada abono identificado genera un recibo de canal «banco» con
  la fecha valor del movimiento, aplicado en forma no interactiva por los contratos con la modalidad del canal
  (FR-198); un abono puede dividirse entre varias personas y productos con suma exacta.
- **FR-069 · Consignación reportada en ventanilla.** *(R1)* El asociado que trae su soporte puede quedar aplicado de
  inmediato con medio «consignación» y pendiente de cruzar con el extracto, o quedar por verificar hasta el extracto,
  según el parámetro (por defecto, por verificar, FR-189); lo no encontrado en el extracto en los días configurados
  alerta.
- **FR-070 · Partidas por identificar.** *(R0)* Un abono no identificado queda como partida (pasivo) con banco,
  cuenta, fecha, valor y referencia, sin persona; su hecho la reconoce.
- **FR-071 · Identificación posterior.** *(R0; R1 con fecha valor del movimiento)* Una persona con permiso identifica
  la partida, entera o en partes, y la aplica con la **fecha valor del movimiento** cuando Cartera y Captaciones
  adopten las versiones 2 de sus contratos (enmiendas FR-164 y FR-165). Mientras no las adopten, se aplica con la
  fecha más antigua que admita el contrato, el recibo lo dice, y se alerta al módulo para que corrija la mora
  indebida por su condonación con aprobación.
- **FR-072 · Partidas que no son recaudo.** *(R0)* Rendimientos, reintegros, traslados propios, giros devueltos
  (FR-199) o devoluciones se clasifican a un concepto con aprobación; la devolución al consignante va por orden de
  pago.
- **FR-073 · Antigüedad y destino.** *(R0)* Las partidas se ven por antigüedad, con alertas escalonadas (por
  defecto a los 30, 60 y 90 días) y una señal para el oficial de cumplimiento cuando un mismo consignante no
  identificado se repite. Pasado el plazo, su destino es un parámetro (por defecto, siguen como pasivo); llevarlas a
  ingreso o a otra cuenta es una decisión registrada con aprobación según la política aprobada, que conserva el
  derecho del dueño a reclamar; nunca es automática.
- **FR-074 · Transferencias a cuentas de captación.** *(R0)* Entran por Recaudo como cualquier recaudo (D2);
  Tesorería no consigna en cuentas de asociados.
- **FR-196 · Extracto sin formato.** *(R0)* Cuando el banco entrega PDF o un archivo de portal sin formato cargado,
  el extracto se carga con una plantilla genérica o se captura a mano, con la misma huella y revisión previa con
  error por línea; las líneas ilegibles no se cargan y se informan. Una cuenta de recaudo sin extracto cargado en
  los días hábiles configurados alerta, y antes del cierre mensual de Cartera se alerta de las partidas abiertas
  del mes.
- **FR-197 · Huella del movimiento bancario.** *(R0)* La huella se define por formato: con el identificador o la
  secuencia del banco cuando el formato la trae; sin él, con banco, cuenta, fecha, valor y referencia, y dos
  movimientos iguales se muestran como «posibles duplicados» para que una persona decida. El mismo criterio rige
  para el extracto y para los archivos de convenio; además, un archivo ya cargado se reconoce por su propia huella.
- **FR-198 · Modalidad por canal.** *(R0)* Las formas no interactivas usan la modalidad configurada por canal y
  regla: por defecto, en créditos «cuota del ciclo» (lo que exceda, saldo a favor del pagador), y en cuentas
  «cuotas» si hay exigibles o «libre» si no.
- **FR-199 · Giro devuelto.** *(R0, T0)* Un abono que devuelve una transferencia ya informada «girada» entra por
  Recaudo como partida identificada al concepto «giro devuelto», enlazada a la orden, con alerta al origen y al
  tesorero. Tesorería no cambia el resultado informado; el origen gestiona el reintegro y, si corresponde, emite una
  orden nueva.
- **FR-200 · Aseguradoras.** *(R2)* El pago de una aseguradora (por ejemplo, de una reclamación de un seguro de
  crédito) entra por el canal banco con la aseguradora como pagadora y se aplica por el contrato de obligaciones.

#### G · Pagadurías y libranza

- **FR-075 · Archivos de descuentos.** *(R0)* Recaudo recibe de Cartera cada archivo de descuentos por pagaduría
  (las versiones que adopte: sólo créditos, y créditos con cuotas de cuentas por grupos y conceptos equivalentes) y
  lo muestra por pagaduría, convenio y período con sus totales y su archivo para descargar.
- **FR-076 · Confirmar el envío.** *(R0)* Con canal (correo, portal, físico u otro), fecha y radicado, Recaudo
  confirma a Cartera el envío de esa versión con su huella.
- **FR-077 · Respuesta de la pagaduría.** *(R0)* Se carga el archivo de respuesta con el formato de vuelta del
  convenio, la plantilla genérica o a mano (FR-201), se muestra la vista previa (por línea o por concepto de la
  pagaduría) y se devuelve el resultado a Cartera; si Cartera lo rechaza (líneas que no coinciden), se muestra para
  corregir y emitir otro.
- **FR-078 · Giro de la pagaduría.** *(R0)* Se registra el dinero girado (identificado en el extracto o recibido en
  caja) y se aplica cada línea descontada pendiente de giro por el contrato que corresponda (créditos o cuotas de
  cuentas), con medio «descuento de pagaduría externa», la referencia de la línea y la fecha valor del descuento, en
  un recibo de canal «pagaduría»; cada línea es su propia unidad (FR-171).
- **FR-079 · Giros parciales o mayores.** *(R0)* Un giro menor cubre las líneas en el orden de prelación del
  convenio o el que elija el analista y las demás siguen pendientes; un giro mayor deja el excedente como saldo a
  favor de la pagaduría (FR-054).
- **FR-080 · No aplicados.** *(R0)* Lo que la pagaduría descontó sin equivalencia, o lo que un contrato rechazó,
  queda como saldo de la pagaduría por aplicar hasta que una persona lo resuelva con soporte.
- **FR-081 · Nómina propia.** *(R0)* Los descuentos de nómina propia los aplica Nómina por los contratos; Recaudo
  sólo los muestra en el recaudo consolidado (R1).
- **FR-082 · Calendario de pagadurías.** *(R1)* Recaudo muestra por pagaduría lo enviado, respondido, girado y
  pendiente; registra la fecha de pago de la nómina que informa la pagaduría y calcula con el calendario hábil la
  fecha legal de giro (parámetro, por defecto tres días hábiles, Ley 1527 art. 6) como aviso informativo; un informe
  de pagadurías morosas soporta el cobro solidario.
- **FR-201 · Respuesta de pagaduría sin formato.** *(R0)* Si la pagaduría responde en Excel libre o PDF, la
  respuesta se captura con la plantilla genérica de Recaudo o a mano, con revisión previa y los mismos controles.

#### H · Cheques recibidos y canje

- **FR-083 · Recepción.** *(R0 por parámetro)* Banco girador, número, cuenta, girador (persona), fecha y valor;
  cheque de tercero sólo si el parámetro lo admite. Al recibir se valida el plazo de presentación (parámetro: quince
  días en la misma plaza y un mes en otra, Código de Comercio art. 718) y se avisa. Un cheque posfechado se rechaza;
  si el parámetro lo admite, queda como «cheque en custodia» sin recibo ni aplicación hasta su fecha. Por defecto, en
  R0 la clase cheque está apagada y cada cooperativa la enciende.
- **FR-084 · Canje.** *(R0 manual; R2 completo)* El cheque se incluye en la remesa del día y pasa por recibido,
  consignado, en canje, pagado (por el extracto) o devuelto; los días de canje los informa Captaciones en su
  simulación con su propio parámetro. Hasta R2, el canje se registra a mano; la devolución está desde R0.
- **FR-085 · Devolución.** *(R0)* Causal del catálogo; reverso por FR-060; lista de giradores con devoluciones para
  advertir al cajero; cargo por devolución configurable, apagado por defecto, con referencia a la sanción del 20 % del
  Código de Comercio (art. 731) y distinguiendo la indemnización de un servicio que exige factura (FR-179).
- **FR-086 · Firmeza y canje.** *(R0)* Lo pagado con cheque informa firmeza y canje; Captaciones libera el canje
  sola; Recaudo sólo reversa ante devolución.
- **FR-087 · Informe** *(R1)* de cheques recibidos, en canje, en custodia y devueltos.

#### I · Canales externos

- **FR-088 · Convenios de recaudo.** *(R2)* Por banco o corresponsal: cuenta recaudadora, estructura de la
  referencia (por producto o por persona, longitud y dígito de verificación configurables), formato del archivo
  diario como dato versionado y comisión informativa.
- **FR-089 · Archivo del convenio.** *(R2)* Carga, validación de estructura y totales, identificación por referencia
  y aplicación no interactiva al cargarlo, a nombre de quien lo carga, con un recibo por pago y la unidad de FR-171;
  los no identificados van a partidas.
- **FR-090 · Referencia de pago.** *(R2)* Cada producto (y opcionalmente cada persona) tiene una referencia de pago
  estable que los documentos al asociado pueden imprimir.
- **FR-091 · Pagos en línea.** *(R2)* Los canales en línea (PSE, pasarelas) entran **sólo por los archivos** que el
  banco o la pasarela entregan, cargados por una persona con revisión previa; no hay botón de pago en línea en esta
  feature (Clarifications, C2 = A).
- **FR-092 · Corresponsales** *(R2)* como convenios de recaudo con su archivo.
- **FR-093 · Conciliación del convenio.** *(R2)* Lo informado contra lo abonado en la cuenta, neto de comisión,
  IVA y retenciones (FR-218); diferencia explicada con su hecho y, sobre la tolerancia, aprobación.

#### J · Control de efectivo, SARLAFT y GMF

- **FR-094 · Declaración de origen de fondos.** *(R0)* Desde el umbral de declaración de Recaudo (FR-202) sobre el
  efectivo de un recibo o egreso, o cuando cualquier aplicación la exija según la simulación de su módulo, se exige
  la **declaración de origen de fondos** con soporte; se captura una vez por recibo y viaja a cada aplicación que la
  exija. Cada módulo conserva su propio parámetro.
- **FR-095 · Fraccionamiento.** *(R0)* Señales configurables (varias operaciones bajo el umbral en pocos días,
  pagos de terceros frecuentes, partidas de un mismo consignante) alertan al oficial de cumplimiento.
- **FR-096 · Reporte de transacciones.** *(R1, en producción antes del primer vencimiento)* Por período, individuales
  y múltiples, con la estructura vigente como dato versionado marcado «sin cotejar» hasta validarla y el alcance que
  fije el parámetro (sólo efectivo o todas las transacciones); generar, validar (bloqueantes y alertas), regenerar
  (la anterior queda reemplazada) y marcar cargado con radicado; el plazo legal y una suspensión vigente se
  informan; el reporte de ausencia se genera si el parámetro lo exige.
- **FR-097 · Datos para SARLAFT.** *(R0)* Cada operación deja persona, pagador, quien efectúa, medio, canal,
  oficina, montos, declaración y alertas para la feature SARLAFT; el reporte de operaciones sospechosas no es de
  esta feature.
- **FR-098 · Listas restrictivas.** *(R0)* Pagador, quien efectúa, beneficiario de un egreso y consignante
  identificado se consultan contra las listas de la plataforma antes de confirmar. Las listas vinculantes siguen
  FR-203. En las demás, una coincidencia alerta al oficial y, según el parámetro, retiene la operación (FR-189) hasta
  que él la libere o sigue (por defecto, sigue con alerta).
- **FR-099 · Egresos en efectivo** *(R1)* cuentan para umbrales, alertas y reporte.
- **FR-100 · GMF de las cuentas propias.** *(T0 estimación; T2 nota bancaria)* Tarifa, exenciones y hechos
  generadores con vigencia, incluida la tarifa cero; la exención de un desembolso se marca por operación (FR-206);
  Tesorería estima el GMF al preparar un lote y reconoce el real desde el extracto como nota bancaria. Hasta T2, el
  GMF que cobra el banco se registra en Contabilidad y aparece en la conciliación mínima.
- **FR-101 · GMF de captaciones.** *(R1)* Lo calcula Captaciones; Recaudo lo muestra en el egreso del retiro.
- **FR-202 · Parámetros SARLAFT separados.** *(R0)* Cuatro parámetros con vigencia y fuente, cada uno con su valor:
  umbral de declaración de origen de fondos (por operación), umbral de transacción individual reportable, umbral de
  transacciones múltiples (mes calendario por persona) y ventana y señales de fraccionamiento; más el estado de
  aplicación del reporte por período (vigente o suspendido), su alcance y si exige reporte de ausencia. Se siembran
  los de la Supersolidaria (5.000.000 individual y 30.000.000 múltiples, con su fuente), «pendientes de validar» por
  el oficial; nunca los del sector bancario.
- **FR-203 · Listas vinculantes bloquean.** *(R0, T0)* Una coincidencia con una lista vinculante (las del Consejo
  de Seguridad de las Naciones Unidas y las que la norma haga vinculantes) impide la operación —no se recibe el
  dinero, no se entrega, no se incluye la orden en un lote— y alerta de inmediato al oficial para su reporte; no es
  parametrizable a «sigue». Sólo el oficial libera a la persona si es un falso positivo, con soporte. Tesorería
  consulta a todo beneficiario de una orden de pago antes de aprobar el lote.
- **FR-204 · Registro único de efectivo con el punto de venta.** *(R0 sólo Recaudo; R1 con el punto de venta)* En R0
  el registro de transacciones en efectivo y sus acumulados cuentan sólo lo de Recaudo. En R1, el efectivo que reciben
  y entregan las cajas de Recaudo y las del punto de venta alimenta un solo registro por persona, con acumulados,
  señales de fraccionamiento y reporte consolidados (enmienda FR-166).
- **FR-205 · GMF en egresos por cuentas contables.** *(T0; R1 en los egresos de caja)* Cuando un egreso de caja o un
  pago de Tesorería dispone de recursos registrados en una cuenta contable para pagar o transferir a un tercero
  —devoluciones de saldos a favor y excedentes, devolución de partidas al consignante, pagos por cuenta del
  asociado—, se evalúa el hecho generador del GMF por clase de egreso u orden con los parámetros vigentes y, si
  aplica, se emite el hecho del GMF; no se evalúa lo que el origen ya liquidó (los retiros de Captaciones).
- **FR-206 · Exención de GMF por operación.** *(T0)* La exención de un desembolso se marca en la orden según su
  clase, el beneficiario (el deudor o un tercero en los destinos que la norma admite, como parámetro) y la forma
  (abono en cuenta del deudor o cheque de cruce y negociabilidad restringida). El cheque de primer beneficiario se
  liga a esa exención.

#### K · Tesorería: cuentas bancarias propias y saldos

- **FR-102 · Cuentas bancarias propias.** *(T0)* Banco (con su código de transferencia), número, tipo, oficina,
  propósitos (recaudo, pagos, nómina, convenio, o **fondo de liquidez**, que es exclusivo, FR-207), exención de GMF,
  estado con vigencia, y el vínculo **único** con la cuenta contable bancaria del plan que la representa; el vínculo
  lo usa Contabilidad para resolver la matriz; los hechos llevan la cuenta bancaria propia, nunca la contable.
- **FR-103 · Saldos.** *(T0)* Por cuenta: saldo en libros (de Contabilidad), saldo según el último extracto y
  disponible proyectado (menos lotes aprobados no enviados y cheques en tránsito, más remesas en tránsito).
- **FR-104 · Traslados entre cuentas propias** *(T1)* con aprobación por monto, hecho y tránsito hasta el extracto.
- **FR-105 · Remesas.** *(T0)* De bóveda a banco y de banco a bóveda, enlazadas con Recaudo, con acta y sello de
  bolsa (FR-182); efectivo en tránsito con alerta si no se confirma en los días configurados.
- **FR-106 · Alcance** *(T0)* Véase FR-011 (el alcance por cuenta bancaria propia es parte de él).
- **FR-207 · Fondo de liquidez restringido.** *(T0)* Una cuenta con propósito «fondo de liquidez» no admite otro
  propósito, ni lotes de pago, traslados ni remesas desde ella, salvo un **uso del fondo** con causal de la norma
  (atender a depositantes o reducción de depósitos), aprobación y alerta de aviso previo a la Supersolidaria; su
  extracto mensual se conserva como soporte del informe al supervisor. Los abonos al fondo no se restringen.
- **FR-208 · Firmantes y tokens.** *(T1)* Por cuenta, los firmantes y los tokens del portal del banco, con
  combinaciones (firma conjunta por monto) y vigencia; los cambios exigen acta y aprobación; se alerta cuando un
  firmante se retira de la cooperativa.
- **FR-209 · Egresos fuera de Tesorería hasta T2.** *(T0)* Hasta T2, los pagos a proveedores, las cuentas por pagar
  y las notas bancarias se registran en la Tesorería heredada o en Contabilidad. Tesorería los ve como movimientos
  contables de la cuenta en su saldo, en la posición y en la conciliación mínima; FR-001 rige para ellos desde T2.
  **Construido en T2 (fase 42, 2026-10-26)**: la transición terminó. Las facturas de proveedor a crédito llegan como
  órdenes (`FacturaDeProveedorPorPagar`, E-03), las cuentas por pagar contables se consultan y convierten en órdenes
  (`CuentasPorPagarAsync`, E-04) y las notas bancarias se registran en Tesorería (fase 41). Lo que se siga registrando en
  Contabilidad se ve como movimiento contable de la cuenta, como antes.

#### L · Órdenes de pago y ejecución

- **FR-107 · Bandeja única de órdenes.** *(T0 órdenes de giro y de Recaudo; T1 manuales; T2 proveedores y cuentas
  por pagar)* Recibe: órdenes de giro de Cartera y Captaciones (las versiones adoptadas, con su clase de orden) y sus
  anulaciones; órdenes de pago de Recaudo (devoluciones); facturas de proveedor por pagar de Comercial; cuentas por
  pagar contables convertidas en órdenes; y órdenes manuales de Tesorería (FR-211). Cada una con origen,
  beneficiario (siempre una persona), cuenta destino, valor, fecha y forma. Una orden repetida no se duplica y una
  orden con valor cero o negativo se rechaza.
- **FR-108 · Órdenes pagadas por archivo de Cartera.** *(T0)* No entran a lotes de Tesorería; sólo reciben su
  resultado. Tesorería conoce el archivo por el mensaje de la enmienda FR-164.
- **FR-109 · Validación.** *(T0)* Una orden sin cuenta destino, con una cuenta destino que no está registrada en el
  maestro de la persona, sin código de transferencia del banco, con beneficiario inexistente o en una lista
  vinculante queda en pendientes con su motivo.
- **FR-110 · Lote de pago.** *(T0)* Por cuenta de origen y forma (archivo, portal del banco o cheques): selección,
  vista previa con el formato bancario vigente del banco de la cuenta (o el genérico, FR-213), totales, cantidad y
  GMF estimado; la cuenta de origen debe ser del banco del formato y no del fondo de liquidez.
- **FR-111 · Aprobación del lote** *(T0)* por monto con los niveles que exija (FR-013); quien prepara no aprueba.
- **FR-112 · Archivo de pago.** *(T0)* Inmutable, conservado con una huella verificable; lo descarga una persona con
  permiso propio distinta de quien lo preparó; se marca enviado con quién lo cargó en el banco y quién lo autorizó
  allí; un archivo no enviado se anula y sus órdenes vuelven a la bandeja.
- **FR-113 · Cheques.** *(T0)* Las órdenes de forma cheque se pagan con la chequera de la cuenta (FR-219); el cheque
  se entrega con constancia de recibido.
- **FR-114 · Efectivo.** *(T0)* Una orden de giro no se paga en efectivo (su forma no lo admite: transferencia,
  cheque o pago a tercero); el efectivo sale por la caja con el contrato (FR-051, FR-053). Es la regla única (D20): la
  devolución de aportes al retiro sale por la orden de giro de Captaciones y, en efectivo, sólo como un retiro que
  Captaciones autoriza por el contrato de cuentas y entrega la caja (R1); una orden de giro en efectivo exigiría una
  versión nueva de la orden (FR-193 remite aquí).
- **FR-115 · Anulación de una orden.** *(T0)* Si la orden está en la bandeja o en un lote no enviado, sale y se
  responde «anulada»; si el archivo ya se envió, sigue hasta conocer el resultado.
- **FR-116 · Unicidad.** *(T0)* Una orden en a lo sumo un lote no anulado; el valor girado de una **orden de giro**
  es exactamente su neto: no hay pagos parciales de una orden de giro (la cuenta por pagar contable sí admite pago
  parcial, FR-125).
- **FR-117 · Comprobante de pago** *(T1)* numerado por pago, con soporte al beneficiario por correo con
  autorización.
- **FR-210 · Cuenta destino del beneficiario.** *(T0)* La cuenta destino de un pago debe estar registrada en el
  maestro de la persona. Su alta o cambio exige otra persona que lo apruebe, la certificación bancaria como soporte
  (titularidad) y un período de espera configurable antes de pagar a ella; es una escritura sensible auditada. El
  lote marca las órdenes a cuentas cambiadas en los últimos días configurados, y se alerta cuando una misma cuenta
  destino sirve a varios beneficiarios o coincide con la de un usuario interno (enmienda FR-169).
- **FR-211 · Órdenes manuales.** *(T1)* Sólo por conceptos de un catálogo cerrado, con soporte obligatorio, dos
  aprobadores, tope por concepto y beneficiario distinto de quien prepara o aprueba. Exigen la retención en la fuente
  que corresponda o se derivan a una causación previa en Contabilidad.
- **FR-212 · Archivo de pago alterado.** *(T0)* Cada línea de la respuesta del banco (o del extracto) se coteja
  contra el archivo generado (beneficiario, cuenta y valor); una discrepancia alerta y la línea no se informa al
  origen hasta que una persona la revise.
- **FR-213 · Sin formato de pagos del banco.** *(T0)* Mientras falte el formato de un banco, el lote puede usar un
  formato genérico (CSV o Excel) o la forma «portal del banco», en la que el tesorero digita los pagos en el portal;
  el resultado se registra a mano, uno a uno o en bloque, con su soporte, y con la misma segregación.
- **FR-214 · Pago a tercero.** *(T0)* Una orden de forma «pago a tercero» (por ejemplo, la consignación a un
  juzgado) se paga por transferencia o por el portal del banco a la cuenta del tercero, con su soporte (constancia
  de consignación); su resultado y su conciliación siguen las mismas reglas.

#### M · Resultado del giro, rechazo y regiro

- **FR-118 · Resultados.** *(T0)* Por orden: **girada**, **rechazada** (causal del banco), **cheque no cobrado** o
  **anulada**; se informa al origen de la orden con su clave, siempre a nombre de la persona que lo registra, desde
  la respuesta del banco (formato de vuelta), el emparejamiento del extracto que una persona confirma o el registro
  manual con soporte y permiso.
- **FR-119 · Cuándo es «girada».** *(T0)* Una transferencia es «girada» cuando el banco la confirma (respuesta,
  extracto confirmado por una persona o registro manual con soporte), nunca al marcar enviado el archivo. Un cheque
  es «girado» al entregarlo con constancia.
- **FR-120 · Rechazo técnico del archivo.** *(T0)* Un archivo rechazado entero por forma no se informa al origen: se
  marca «rechazado por el banco» y sus órdenes vuelven a la bandeja para otro archivo.
- **FR-121 · Rechazo definitivo y regiro.** *(T0)* Se informa al origen, que decide (reverso, reintegro o
  pendiente de regiro); la orden nueva que emita se enlaza a la anterior y Tesorería muestra la cadena completa.
- **FR-122 · Cheque no cobrado.** *(T0)* Pasados los días configurados desde la entrega, alerta; anulado con
  permiso, se informa «cheque no cobrado» (sólo para un cheque ya informado «girado»).
- **FR-123 · Sin resultado.** *(T0)* Alerta al tesorero a los días configurados.
- **FR-215 · Transiciones del resultado.** *(T0)* Desde «girada» sólo cabe «cheque no cobrado», y sólo en la forma
  cheque; nunca «rechazada» después de «girada» (el abono devuelto sigue FR-199). «Anulada» sólo responde a una
  anulación del origen sobre una orden en la bandeja o en un lote no enviado; lo que Tesorería anula por su cuenta
  (un archivo, un lote) no produce resultado. Ningún proceso automático informa ni cambia un resultado.

#### N · Pagos a proveedores y cuentas por pagar

- **FR-124 · Facturas de proveedor.** *(T2)* Las facturas confirmadas por pagar en Comercial llegan como órdenes con
  vencimiento y neto (enmienda FR-166); al pagarse, Comercial recibe el pago; una factura anulada en Comercial sale
  del lote no enviado.
- **FR-125 · Cuentas por pagar contables.** *(T2)* Se consultan pendientes por tercero y documento cruce en las
  cuentas que Contabilidad marque por pagar (enmienda FR-167) y se convierten en órdenes; el pago lleva tercero y
  documento cruce; admite pago parcial.
- **FR-126 · Programación** *(T2)* por vencimiento con calendario de pagos.
- **FR-127 · Sin facturas propias.** *(T0)* Tesorería no registra facturas ni causa gastos: eso es de Comercial y
  Contabilidad.

#### O · Conciliación bancaria

- **FR-128 · Por cuenta y mes.** *(T2)* Extracto contra lo registrado en la cuenta: recibos de banco, remesas,
  pagos de Tesorería, cheques, dispersiones de Nómina, archivos de desembolsos de Cartera y movimientos manuales de
  Contabilidad (consulta de movimientos de la cuenta, enmienda FR-167), con extractos de varios bancos.
- **FR-129 · Emparejamiento** *(T2)* automático por reglas configurables (referencia, valor, fecha con días de
  holgura) y manual uno a varios; nada se empareja dos veces.
- **FR-130 · Partidas conciliatorias.** *(T2)* Cheques girados no cobrados, consignaciones y remesas en tránsito,
  notas bancarias (registradas como hechos de Tesorería con aprobación) y abonos no identificados (que pasan a
  partidas por identificar de Recaudo).
- **FR-131 · Cierre.** *(T0)* Véase FR-216 (cierre de la conciliación del mes con su informe y reapertura con
  motivo y permiso).
- **FR-132 · Aviso al cierre contable.** *(T1)* Contabilidad avisa al cerrar un mes con conciliaciones abiertas o
  sesiones de caja sin cerrar; informativo por defecto, bloqueante si la cooperativa lo configura.
- **FR-133 · Adquirentes y convenios.** *(T2)* Lo cobrado por lote de datáfono (en Recaudo y, por la consulta de la
  enmienda FR-166, en el punto de venta) contra lo abonado por la red, con comisión, IVA y retenciones como hecho
  (FR-218); diferencias con aprobación.
- **FR-216 · Conciliación mínima.** *(T0)* Por cuenta y mes: carga del extracto (formato del banco, plantilla
  genérica o digitado); cruce automático por valor, fecha con días de holgura y referencia contra recibos de banco,
  remesas, pagos y cheques de Tesorería y movimientos contables de la cuenta (enmienda FR-167); marca manual de lo
  que no cruza; los abonos sin registro pasan a Recaudo como partidas; informe de partidas conciliatorias con saldo
  del extracto, partidas y saldo en libros; cierre y reapertura (FR-131). Nada se cruza dos veces. Las reglas
  configurables, las notas bancarias como hechos y los extractos de varios bancos son de T2. Es la **única**
  conciliación bancaria de la cooperativa: reemplaza a la de Contabilidad (FR-055 a FR-060 de la 009, enmienda E-04 en
  FR-167); la reapertura de un período contable marca desactualizadas las conciliaciones cerradas de ese mes.
- **FR-217 · Segregación de la conciliación.** *(T0)* Concilia una persona distinta de quien prepara o aprueba pagos
  de esa cuenta en el período (por defecto, Contabilidad); las partidas conciliatorias se ven por antigüedad con
  alerta; un cargo no identificado (embargo, débito no autorizado) levanta una alerta propia.
- **FR-218 · Comisiones y retenciones desagregadas.** *(T2, R2)* Las notas y diferencias de redes, convenios y
  bancos desagregan comisión, IVA y cada retención (en la fuente, de IVA, de ICA) con su tercero; los intereses
  bancarios se registran en bruto con su retención.

#### P · Chequeras y cheques girados

- **FR-134 · Chequeras.** *(T0)* Por cuenta, rango, estado; los cheques se numeran del rango; un cheque dañado se
  anula con su número.
- **FR-135 · Ciclo del cheque girado.** *(T0)* Emitido, entregado, cobrado (marcado en la conciliación), anulado o
  no cobrado, con beneficiario, valor, fecha y orden o pago.
- **FR-136 · Controles.** *(T0)* Cruce y «páguese al primer beneficiario» por defecto, configurables (ligados a la
  exención de FR-206); huecos de numeración sólo explicados por anulaciones.
- **FR-219 · Chequera mínima.** *(T0)* Lo indispensable del primer día: registro de la chequera con su rango,
  numeración automática del cheque desde la orden, anulación por daño con su número, estados emitido, entregado,
  cobrado y anulado, constancia de entrega (que informa «girada», FR-119) y custodia de las chequeras en blanco
  (FR-182).
- **FR-220 · Cheques girados al cierre del mes.** *(T1)* Al cierre, los cheques emitidos y no entregados se
  reclasifican al efectivo de la cuenta con su hecho; los no cobrados más allá del plazo configurado pasan a cuenta
  por pagar al beneficiario, nunca a ingreso.

#### Q · Liquidez y flujo de caja

- **FR-137 · Posición diaria.** *(T2)* Saldos de bancos, bóvedas y cajas; compromisos (lotes aprobados, órdenes en
  bandeja, cheques en tránsito, dispersiones de nómina); recaudos esperados de las consultas de Cartera y
  Captaciones (enmiendas FR-164 y FR-165); sin respuesta de una consulta, se muestra sin ese componente.
- **FR-138 · Flujo proyectado** *(T2)* por semanas y meses con categorías configurables, real contra proyectado.
- **FR-139 · Fondo de liquidez.** *(T2)* Las cuentas marcadas informan su saldo a la fecha a Captaciones (enmienda
  FR-165); las inversiones quedan fuera de alcance. Su restricción de uso rige desde T0 (FR-207).

#### R · Integración contable

- **FR-140 · Dos emisores.** *(R0, T0)* Recaudo y Tesorería emiten hechos a Contabilidad con su código de módulo, de
  forma atómica con su operación, sin cuentas, enlazados al original cuando corrigen.
- **FR-141 · Hechos de Recaudo.** *(R0; R1 los de egresos y cuentas por cobrar)* Recibo (por porción de medio,
  contra el puente de recaudo de cada módulo o el rol del concepto), egreso, anulación (espejo), movimiento de caja y
  reclasificación, diferencia de arqueo (cuenta por cobrar al cajero, gasto o sobrante), dinero retenido en custodia,
  remesa, partida por identificar reconocida, identificada o clasificada, cheque devuelto, aplicación reversada por
  el módulo, saldo a favor reconocido y resuelto, GMF de un egreso, recaudo de cuenta por cobrar comercial, recaudo
  pendiente de aplicar y su aplicación.
- **FR-142 · Hechos de Tesorería.** *(T0; T1 traslados y cierre de cheques; T2 notas y adquirentes)* Pago de una
  orden (contra el puente de giro del origen o la cuenta por pagar), resultado rechazado o cheque no cobrado
  (reverso del pago), GMF de un pago, remesa, cheque anulado, traslado, reclasificación de cheques al cierre, nota
  bancaria (GMF, comisión, intereses, impuestos), conciliación de adquirentes y convenios.
- **FR-143 · Matriz por emisor.** *(R0, T0)* Hecho × rol × dimensiones (medio, punto, caja, oficina y sucursal,
  cuenta bancaria propia, concepto, módulo y clase de recaudador de destino, clase de orden) con vigencia; plantilla
  con filas sugeridas sin cuenta; completitud; **validación previa** que bloquea en pantalla por defecto y avisa en
  procesos; modo de paso en línea o por lotes resumidos (por defecto: recibos de caja por lotes resumidos por día,
  punto y medio; lo demás en línea).
- **FR-144 · Puentes en cero.** *(R0, T0)* Lo que Recaudo y Tesorería contabilizan contra los puentes de recaudo, de
  desembolso y de devolución de Cartera y Captaciones los salda; la conciliación de puentes explica cada diferencia
  hecho por hecho.
- **FR-145 · No se reversa desde Comprobantes.** *(R0, T0)* Lo de Recaudo y Tesorería se corrige desde su módulo.
- **FR-146 · Conciliación con Contabilidad.** *(R0, T0)* Caja por oficina, bancos, partidas por identificar, saldos a
  favor de pagadores y puentes, con cero diferencias sin explicar al cierre.
- **FR-221 · Cambio de la política de validación previa.** *(R0, T0)* Pasar de «bloquear» a «advertir» exige permiso
  propio y motivo, queda auditado y alerta cada día hasta volver a «bloquear».
- **FR-222 · Exógena.** *(T2)* Cada cuenta bancaria propia lleva a su banco como tercero, de modo que Contabilidad
  obtenga los saldos por cuenta al 31 de diciembre y los pagos y retenciones por tercero para los formatos de
  información exógena.

#### S · Participantes, versiones y activación

- **FR-147 · Registro.** *(R0, T0)* Recaudo: recaudador de obligaciones y de cuentas (contratos v1, y v2 cuando se
  adopten) y destino de sus mensajes (pago reversado, consignación reversada, descuentos por pagaduría en las dos
  versiones); emite la confirmación de envío y el resultado de descuentos (dos versiones). Tesorería: destino de
  giros (orden en sus dos versiones, anulación y, con E-01, archivo de desembolsos); emite el resultado del giro.
- **FR-148 · Reemplazo de los dobles.** *(R0, T0)* Véase FR-223 (retiro de los dobles en todo ambiente); el registro
  real no depende de retirar una fila a mano.
- **FR-149 · Versiones.** *(R0, T0)* Adoptar una versión es un registro con vigencia; los mensajes conservan su
  versión; una versión no adoptada se rechaza con su motivo.
- **FR-150 · Activación de Recaudo y Tesorería** *(R0, T0)* por cooperativa con fecha. Recaudo exige puntos, cajas,
  cajeros y supervisor con permisos (FR-176), medios y tipos de documento; Tesorería, cuentas bancarias vinculadas,
  formatos (o la forma de FR-213) y chequera si ofrece cheques. Con Contabilidad iniciada, las dos exigen la matriz
  completa de los hechos que no estén en «no pasa». La activación abre, en el mismo guardado, la vigencia de sus
  filas del registro de participantes; antes de activar no reciben mensajes de negocio (quedan pendientes en la
  bandeja). Su activación precede a la de Cartera y de Captaciones en la misma cooperativa.
- **FR-151 · Consumo.** *(R0, T0)* Cada mensaje recibido tiene recibo único; su resultado (procesado o rechazado con
  motivo) se ve en la bandeja con reproceso.
- **FR-223 · Retiro de los dobles de prueba.** *(R0, T0)* Con Recaudo y Tesorería reales, los dobles de recaudador y
  de tesorería dejan de existir: no se registran en ningún ambiente, sus filas del registro en las cooperativas de
  los ambientes de prueba se retiran conservando su historia, y la bandeja tiene un solo consumidor por destino.
  Fuera de producción tampoco valen para la activación de Cartera y Captaciones: cuentan Recaudo y Tesorería reales,
  como en producción (enmiendas FR-164 y FR-165). Es la regla única de los dobles (FR-148 y FR-163 remiten aquí).
- **FR-224 · Migración de las pruebas de Cartera y Captaciones.** *(R0, T0)* Las pruebas de punta a punta de
  Cartera y Captaciones que usaban los dobles se reescriben sobre Recaudo y Tesorería reales (registro, activación,
  recibos, giros y resultados), en los dos motores de base de datos, y quedan en verde antes de retirar los dobles.

#### T · Informes y documentos

- **FR-152 · Informes de Recaudo.** *(por informe)* R0: cierre de sesión y del día, recaudo diario por punto, caja,
  cajero, medio, módulo y **producto o línea** (crédito frente a aporte), relación de recibos por cajero, anulados
  con motivo, excepciones (fecha valor, reclasificación de medios, contingencia), partidas por antigüedad, saldos a
  favor por resolver, pagadurías, cheques devueltos. R1: consolidado con Nómina y débito automático de sólo lectura,
  anulaciones con reemisión, pagadurías morosas, cheques recibidos y en canje, transacciones en efectivo. R2: canales
  y convenios.
- **FR-153 · Informes de Tesorería.** *(por informe)* T0: órdenes por estado y antigüedad, lotes por aprobar y
  enviados, rechazados y pendientes de regiro, órdenes marcadas por cambio de cuenta, cheques girados, saldos
  bancarios, conciliación mínima. T1: comprobantes de pago, traslados, cheques al cierre del mes. T2: conciliaciones
  completas, liquidez y flujo.
- **FR-154 · Exportación** *(R0, T0)* en el centro de informes con su permiso y auditada.
- **FR-155 · Tablero** *(R1, T2)* de caja y liquidez sin datos personales.
- **FR-156 · Documentos** *(R0, T0)* con número y código de verificación: recibo, egreso, anulación, informe de
  cierre, comprobante de pago, conciliación.
- **FR-225 · Manuales por entrega.** *(R0, T0)* Cada entrega trae su manual del cajero, del supervisor, del analista
  de recaudo y del tesorero, con lo que esa entrega permite; los de R0 y T0 están antes del ensayo general.

#### U · Puesta en marcha, salida en vivo y retiro de lo heredado

- **FR-157 · Plantillas** *(R0, T0)* (todo o nada, revisión previa, error por fila y columna): puntos y cajas,
  cajeros con sus cajas y alcances, medios por punto, bóvedas con su efectivo al corte, cuentas bancarias propias con
  saldo y chequeras, cheques girados en tránsito, consignaciones y remesas en tránsito, cheques recibidos en canje,
  partidas por identificar al corte, convenios (R2).
- **FR-158 · Saldos iniciales sin hechos.** *(R0, T0)* Ya están en los saldos de apertura de Contabilidad; se cuadran
  por cuenta y las diferencias exigen acta o cuadre provisional (FR-228).
- **FR-159 · Activación** *(R0, T0)* desde el día siguiente al corte, con el mes abierto.
- **FR-160 · Cifras de referencia** *(R0, T0)* para cotejar totales cargados y el paralelo, sin nombrar al sistema de
  origen.
- **FR-161 · Lo heredado de Tesorería.** *(posterior a T2)* El módulo heredado (cheques, conceptos, facturas y flujo de
  caja) convive con esta feature hasta T2: COOFLOPAL lo usa para cuentas por pagar y cheques de proveedores. Cuando
  T2 trae pagos a proveedores y conciliación completa, **se retira entero** con la guarda destructiva: diagnóstico de
  filas en cada ambiente; si hay filas, primero se exportan para archivo y sus pendientes (cheques sin cobrar,
  facturas por pagar) se cargan en Tesorería por plantilla; la guarda se niega mientras queden filas sin migrar,
  salvo el marcador de aprobación; respaldo previo, segundo revisor y migración declarada **irreversible**. Sus rutas
  y pantallas se quitan sin alias (Clarifications, C3 = A).
- **FR-162 · Menú.** *(R0, T0)* Recaudo y Tesorería tienen su grupo de menú; todo enlace apunta a una página
  existente.
- **FR-163 · Dobles de prueba.** *(R0, T0)* Véase FR-223 (no hay dobles en ningún ambiente) y FR-224 (las pruebas de
  Cartera y Captaciones usan los módulos reales).
- **FR-226 · Paralelo en QA.** *(R0, T0)* El paralelo de una cooperativa corre en QA sobre una copia de su
  parametrización, con la versión disponible de R0/T0 y lo que haya de R1/T1; al terminar, un informe compara lo registrado con las cifras de
  referencia por día, medio y producto. Producción no recibe datos del paralelo: la carga definitiva parte limpia.
- **FR-227 · Ensayo general y go/no-go.** *(R0, T0)* Antes de la carga definitiva, un ensayo general en QA y una
  lista de go/no-go verificable en el sistema: completitud de la matriz al 100 % de lo que no esté en «no pasa»; un
  recibo de prueba de cada medio, punto y concepto; un giro por cada forma y cuenta; segundo factor inscrito de cada
  aprobador y supervisor; roles y alcances asignados; cronograma del cambio (registro, activaciones, saldos y
  decisión) y los escenarios de no-go parcial con su plan de reversa, aprobados por el dueño. Una condición en rojo
  no se oculta: seguir exige la aprobación registrada del dueño.
- **FR-228 · Activación con cuadre provisional.** *(R0, T0)* Si al activar no hay cuadre definitivo contra
  Contabilidad, la activación se permite con un **cuadre provisional** aprobado por una persona con permiso propio,
  con motivo y fecha límite. Mientras siga pendiente hay alerta diaria (que escala al vencer la fecha), y el **cierre
  contable del mes de la activación** —diciembre para COOFLOPAL— no se hace sin el cuadre definitivo (enmienda
  FR-167). El cuadre definitivo cierra la alerta y queda enlazado al provisional.

#### V · Enmiendas a otras features

- **FR-164 · E-01 a la 013 (Cartera).** *(R0/T0: c y e; R1: a y b; T2: d; como la tabla E-01 de
  contracts/contratos-v2.md)* (a) Excepción de la
  ventana de fecha valor para el pago que identifica una partida bancaria, citando la partida, como **versión 2 del
  contrato de obligaciones** (el giro de pagaduría con su línea ya está exento y no se toca); Recaudo adopta v1 y v2 y
  la activación de Cartera sigue admitiendo la v1. (b) Consulta de las personas y obligaciones de una carga aplicada
  sin activar (para FR-046). (c) Mensaje «archivo de desembolsos generado o anulado» con sus órdenes, para que
  Tesorería lo concilie (convivencia, D16). (d) Recaudos esperados por fecha para la posición de liquidez (T2). (e)
  Retiro de los dobles y de la regla que, fuera de producción, sólo cuenta a los dobles como recaudador y tesorería
  (FR-223).
- **FR-165 · E-02 a la 014 (Captaciones).** *(R0: b, c y g; R1: a y d; T2: e y f; como la tabla E-02 de
  contracts/contratos-v2.md)* (a) La exención de fecha valor
  de la consignación que cita la **línea del archivo de descuentos** (como ya la tiene Cartera) y la de la partida
  bancaria, como **versión 2 del contrato de cuentas**. (b) Tesorería no se registra como recaudadora (D2); la clase
  y los destinos que la nombran se conservan sin uso. (c) Cada módulo conserva su umbral de declaración y Recaudo
  captura la declaración una vez por recibo (FR-094). (d) Habilitar la cancelación con entrega por el contrato al
  existir Recaudo. (e) Recibir de Tesorería el saldo de las cuentas del fondo de liquidez (T2). (f) Recaudos
  esperados para la posición de liquidez (T2). (g) Retiro de los dobles (FR-223).
- **FR-166 · E-03 a la 012 (Comercial).** *(R1 efectivo y cuentas por cobrar; T2 lo demás)* Efectivo recibido y
  entregado en el punto de venta por persona, para el registro único de transacciones en efectivo (FR-204); consulta
  de ventas a crédito que no pasan a Cartera; mensaje o consulta de facturas de proveedor por pagar y aviso del pago
  (T2); consulta de lotes de datáfono y pagos con tarjeta del punto de venta para la conciliación de adquirentes, que
  pasa a Tesorería (T2); remesa del efectivo del punto de venta a la bóveda o al banco por Tesorería (T1).
- **FR-167 · E-04 a la 009 (Contabilidad).** *(R0, T0; T2 cuentas por pagar)* Dos emisores nuevos en el enrutador de
  hechos con su matriz; consulta de pendientes por tercero y documento cruce (cuentas por cobrar comerciales, R1, y
  por pagar, T2); movimientos contables de una cuenta bancaria para conciliar; el vínculo cuenta bancaria propia ↔
  cuenta contable; el aviso de cierre de FR-132; y la guarda que niega el cierre del mes de la activación con un
  cuadre provisional pendiente (FR-228). Además (decisión del dueño del 2026-10-23), **la conciliación bancaria de
  Tesorería reemplaza a la de Contabilidad** (FR-055 a FR-060 de la 009): un solo extracto y una sola conciliación por
  cuenta, en Tesorería (FR-216); `ACC_BankReconciliations`, `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps`
  quedan sin uso y nadie las escribe (su retiro, si se hace, es una migración destructiva aparte, pendiente); la
  reapertura de un período contable deja de marcarlas y marca desactualizadas, por un puerto de Tesorería, las
  conciliaciones cerradas de ese mes. La marca «cuenta bancaria» de la cuenta contable (FR-055) sigue en Contabilidad y
  es la que exige el vínculo.
- **FR-168 · E-05 a la 010 (Nómina).** *(T2)* Los archivos de dispersión de nómina se informan a Tesorería como
  compromiso y para conciliar; Nómina sigue pagando con su archivo.
- **FR-169 · E-06 a la plataforma.** *(R0, T0)* Códigos de participante de Recaudo y Tesorería; ámbitos nuevos del
  motor de formatos (extracto y recaudo bancario en lectura, pagos de Tesorería en escritura —el ámbito de pagos a
  proveedores reservado en la 010—, respuesta de pagos en lectura) y plantillas genéricas de extracto, respuesta de
  pagaduría y pagos; cuentas bancarias de la persona con doble control, soporte y período de espera (FR-210); nuevos
  tipos de alerta.

### Key Entities *(include if feature involves data)*

**Recaudo — configuración**
- **Punto de recaudo**: oficina, código, nombre, cajas, medios ofrecidos, estado con vigencia.
- **Caja**: punto, código, base y modo, topes de aviso y de bloqueo, cajeros, estado; marca de caja principal
  como bóveda.
- **Bóveda**: oficina, efectivo por denominación, custodios.
- **Tipo de documento de Recaudo**: recibo por canal (incluido contingencia), egreso, anulación, movimiento;
  consecutivo.
- **Talonario de contingencia**: punto, rango, números usados y por digitar.
- **Concepto vario**: ingreso o egreso, exige tercero, cruce o factura, aprobación.
- **Convenio de recaudo**: banco o corresponsal, cuenta, referencia, formato, comisión.
- **Regla de identificación**: criterio, prioridad, aplicación al cargar o propuesta, modalidad del canal.

**Recaudo — operación**
- **Sesión de caja**: caja, cajero, apertura, base, estado, valores sellados, cierre.
- **Arqueo**: por medio, esperado, contado, diferencia, motivo, aprobación; sorpresivo con acta.
- **Movimiento de caja**: tipo (abastecimiento, retiro, traslado, reclasificación), valor, medios, soporte.
- **Operación de caja**: estado previo al recibo (en preparación, bloqueada, retenida), dinero en custodia.
- **Recibo**: número, canal, pagador, quien efectúa, sesión (si es de caja), fechas, medios, aplicaciones,
  imputación, elección del deudor, declaración, estado (vigente, anulación pendiente de aprobación, anulado),
  marca de contingencia.
- **Medio del recibo**: medio, valor, referencia, datáfono y lote, cheque.
- **Aplicación**: módulo, producto, beneficiario, modalidad, valor aplicado, porciones por clase de medio con su
  clave, comprobante del módulo, estado (aplicada, reversada por el módulo, anulada).
- **Aplicación reversada por el módulo**: aviso, causa (dinero recibido o no recibido), resolución.
- **Egreso**: tipo, beneficiario, quien recibe, autorización del módulo, valor, medio, firma.
- **Anulación**: documento, motivo, aprobador, reversos, aplicaciones omitidas.
- **Saldo a favor de pagador** y **recaudo pendiente de aplicar**: pagador, origen, valor, estado.
- **Cuenta por cobrar a cajero**: faltante, autorización de descuento adjunta, estado.
- **Movimiento bancario**: cuenta, fecha, valor, referencia, huella, estado (identificado, partida, conciliado,
  posible duplicado).
- **Partida por identificar**: movimiento, antigüedad, identificaciones, clasificación, destino.
- **Archivo de descuentos recibido**, **respuesta de pagaduría** y **giro de pagaduría**: período, versión,
  envío, resultado, líneas cubiertas, fecha de pago de la nómina.
- **Cheque recibido**: girador, banco, número, estado (incluido en custodia), causal.
- **Declaración de origen de fondos** y **alerta de efectivo**; **registro de transacciones en efectivo**;
  **reporte de transacciones** con versión.

**Tesorería**
- **Cuenta bancaria propia**: banco, número, tipo, propósitos (fondo de liquidez exclusivo), vínculo contable,
  exención, estado; **firmantes y tokens**.
- **Uso del fondo de liquidez**: cuenta, causal, valor, aprobación, aviso.
- **Chequera** y **cheque girado**: rango, número, estado, constancia de entrega.
- **Orden de pago**: origen, clase, beneficiario (persona), cuenta destino, valor, forma, fecha, estado, marca de
  cuenta cambiada, enlace a la orden que reemplaza.
- **Lote de pago** y **archivo de pago**: cuenta de origen, forma (archivo, portal, cheques), órdenes,
  aprobaciones, huella, quién descargó, cargó y autorizó, estado.
- **Resultado del giro**: orden, resultado, causal, fecha, referencia, persona que lo registró.
- **Traslado** y **remesa**: origen, destino, valor, tránsito, acta.
- **Extracto** y **conciliación** (mínima o completa): cuenta, mes, cruces, partidas conciliatorias, notas
  bancarias, conciliador, estado.
- **Flujo de caja**: categorías, proyecciones, real.

**Comunes**
- **Parámetros** con vigencia y fuente; **hechos** de dos emisores; **reglas de matriz**; **participantes** y
  adopciones; **carga de puesta en marcha**; **cuadre** (provisional o definitivo) con fecha límite; **lista de
  go/no-go**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

Las metas de tiempo se miden en QA con el volumen de las Assumptions y la concurrencia que cada una indica.

**Activación y salida en vivo**
- **SC-001**: Con R0 y T0 registrados y activados en una cooperativa, la activación de Cartera y de Aportes muestra
  cumplidos los requisitos de recaudador y de tesorería, sin ningún doble de prueba en ningún ambiente.
- **SC-002**: En el ensayo general en QA (23/11 al 25/11), el 100 % de los recorridos de la primera semana (caja,
  consignación identificada, pagaduría, cheque devuelto, desembolso girado y rechazado con regiro, conciliación
  mínima) termina con el dinero, los módulos y la contabilidad cuadrados.

**Caja**
- **SC-003**: Un cajero registra un recibo que paga tres productos de una persona, con dos medios, en menos de 2
  minutos desde que identifica a la persona.
- **SC-004**: Una sesión de hasta 200 recibos se arquea y cierra en menos de 10 minutos.
- **SC-005**: El 100 % de las sesiones cerradas tiene diferencia cero o una diferencia con motivo, y con aprobación
  cuando supera la tolerancia del medio; cero diferencias sin explicar en el cierre del día.
- **SC-006**: Ningún recibo queda con aplicaciones a medias: en la prueba de fallas inducidas a mitad de la
  confirmación, el 100 % de los recibos tiene todas sus aplicaciones o ninguna.
- **SC-007**: Una operación repetida con la misma clave nunca mueve dinero dos veces (cero duplicados en la prueba
  de reintentos con tres repeticiones por operación).

**Bancos, pagadurías y canales**
- **SC-008**: Al menos el 90 % de los abonos con referencia válida queda **propuesto correctamente** por las reglas
  de identificación (aplicado al cargar cuando el parámetro lo permite, o listo para que una persona confirme).
- **SC-009**: El 100 % de las partidas por identificar tiene antigüedad visible y ninguna supera un escalón de
  antigüedad sin su alerta.
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
- **SC-015**: Toda operación monetaria produce al menos un hecho; ningún hecho está duplicado por su clave y ninguno
  lleva cuentas contables.
- **SC-016**: La conciliación bancaria completa de un mes de una cuenta con hasta 2.000 movimientos se cierra en menos
  de una hora de trabajo, con al menos el 85 % emparejado automáticamente.

**Cumplimiento y seguridad**
- **SC-017**: El 100 % de las operaciones en efectivo sobre el umbral de declaración tiene su declaración de origen de
  fondos, y el reporte de transacciones del mes cuadra con los recibos y egresos en efectivo del mes de las dos
  cajas.
- **SC-018**: El 100 % de las rutas de Recaudo y Tesorería exige un permiso propio; un usuario fuera de alcance recibe
  la misma respuesta que ante lo inexistente.
- **SC-019**: La verificación de la cadena de auditoría detecta el 100 % de las alteraciones y borrados inducidos.
- **SC-020**: Ningún valor legal aparece fijo en Recaudo ni en Tesorería (verificación automática en verde).

**Puesta en marcha y uso**
- **SC-021**: Los saldos iniciales cargados cuadran al peso con Contabilidad o tienen su acta o su cuadre provisional
  aprobado; una carga con un error no guarda nada.
- **SC-022**: Tras la capacitación con el manual de su entrega, el 90 % de los cajeros de la cooperativa piloto
  completa su primer recibo multiproducto y su primer cierre sin pedir ayuda al soporte.

**Agregados el 2026-10-22**
- **SC-023**: Con 20 cajeros simultáneos sobre el volumen de COOFLOPAL, el percentil 95 de la consulta de lo que
  puede pagar una persona es menor de 2 segundos y el de la confirmación de un recibo, menor de 3 segundos.
- **SC-024**: El informe del paralelo en QA explica el 100 % de las diferencias por día, medio y producto entre lo
  registrado y las cifras de referencia, y producción no tiene ningún dato del paralelo el 28/11.
- **SC-025**: En la prueba con personas inducidas en una lista vinculante, cero operaciones se ejecutan, cero
  órdenes entran a un lote aprobado y el 100 % genera alerta inmediata al oficial.
- **SC-026**: El 100 % de las órdenes pagadas a una cuenta destino cambiada dentro del período configurado lleva la
  marca y su revisión expresa, y el 100 % de las discrepancias inducidas entre el archivo generado y la respuesta del
  banco se detecta antes de informar el resultado.
- **SC-027**: Tras retirar los dobles, ningún host de prueba arranca con dos consumidores para un mismo destino, y las
  pruebas de punta a punta de Cartera y Captaciones pasan sobre Recaudo y Tesorería reales en los dos motores.
- **SC-028**: Ningún movimiento sale de una cuenta del fondo de liquidez sin un uso del fondo aprobado.
- **SC-029**: El 100 % de las activaciones con cuadre provisional tiene alerta diaria hasta el cuadre definitivo, y el
  cierre contable del mes de la activación se niega en el 100 % de los intentos sin él.
- **SC-030**: En un proceso no interactivo con líneas rechazadas inducidas, el 100 % de las demás líneas se aplica y
  cada rechazada queda con su motivo y se reprocesa sin duplicar.
- **SC-031**: La conciliación mínima de una cuenta con 500 movimientos del mes cruza sola al menos el 80 % y su
  informe cuadra el saldo del extracto con el saldo en libros a través de las partidas conciliatorias.
- **SC-032**: En la prueba de concurrencia con recibos que tocan créditos y cuentas de las mismas personas, cero
  interbloqueos.

## Assumptions

- **Para quién es**: toda organización solidaria cliente del ERP; COOFLOPAL es la primera y sale con créditos y
  aportes.
- **Moneda**: pesos colombianos; sin moneda extranjera.
- **Volumen**: hasta 30 puntos y 100 cajas por cooperativa, 10.000 recibos y 2.000 órdenes de pago diarios, 50
  cuentas bancarias; en COOFLOPAL, hasta 20 cajeros simultáneos en día pico.
- **Conectividad**: la caja trabaja en línea; ante una caída rige la contingencia por talonario (FR-186), sin modo sin
  conexión.
- **Hardware**: impresora de tirilla o carta, lector y datáfonos independientes (el datáfono no se integra; el
  cajero registra el lote y la aprobación).
- **Bancos y pasarelas**: se comunican por archivos que carga y descarga una persona, o por el portal del banco; las
  estructuras son datos versionados que aporta el dueño o el banco.
- **Valores legales**: umbrales SARLAFT, GMF, plazos de cheques y de libranza, conservación y demás se siembran con su
  fuente y «pendiente de validar».
- **Contratos**: Recaudo y Tesorería adoptan la versión 1 del contrato de obligaciones y del de cuentas, la orden de
  giro v1 y v2 y los mensajes de pagadurías v1 y v2, que no cambian; las enmiendas E-01 y E-02 introducen la versión
  2 de los dos contratos sin retirar la 1.
- **Motores de base de datos**: los dos que soporta el ERP, con las mismas pruebas de punta a punta en ambos.
- **Usuarios**: cajeros, supervisores y suplentes, analistas de recaudo, tesorero, aprobadores, conciliador,
  contador, oficial de cumplimiento, auditor y revisor fiscal.
- **Ambientes**: el paralelo y el ensayo corren en QA; producción recibe sólo la carga definitiva.

### Decisiones por defecto (para revisión del dueño)

Si el dueño no responde, rige la propuesta. Las que el dueño ya decidió lo dicen.

**Alcance y frontera**
- **D1 · Una feature, dos módulos.** Recaudo y Tesorería son módulos distintos con sus permisos, códigos de
  participante y emisores; comparten medios, cuentas bancarias, bóvedas y remesas.
- **D2 · Recaudo es el único recaudador del dinero de las personas.** También el identificado en el extracto y las
  transferencias a cuentas de captación; Tesorería no se registra como recaudadora (conflicto C1; **confirmado por el
  dueño el 2026-10-22**, enmienda la decisión de la 014 del 2026-10-18).
- **D3 · El recibo es todo o nada** con todas sus aplicaciones; los procesos no interactivos, por línea (FR-171).
- **D4 · Sin anulación parcial**: se anula el recibo y se emite otro.
- **D5 · Anulación**: el mismo día en la sesión abierta, la pide el cajero y la aprueba otro; después, permiso propio y
  aprobación de otro.
- **D26 · Débito automático** sigue en Cartera; Recaudo lo muestra (conflicto C3).
- **D27 · Dispersión de nómina** sigue en Nómina; Tesorería la ve y la concilia.
- **D28 · Tesorería no registra facturas** ni causa gastos; paga lo que Comercial y Contabilidad dejan por pagar.
- **D30 · Medios**: el catálogo común, sin clases nuevas; PSE como transferencia.
- **D33 · Servicios a asociados**: cuentas por cobrar de Comercial; a plazos, venta a crédito que financia Cartera
  (**decidido por el dueño el 2026-10-22**).

**Caja**
- **D6 · Arqueo ciego** activado.
- **D7 · Diferencias**: faltante a cargo del cajero como cuenta por cobrar, descontable por Nómina sólo con la
  autorización escrita del caso; sobrante a un pasivo «sobrantes de caja» con plazo antes de ir a ingreso; ambos con
  motivo y, sobre la tolerancia, aprobación.
- **D8 · Base del día** por defecto; fondo fijo como opción por caja.
- **D9 · Tolerancias** de arqueo las del catálogo de medios (cero por defecto en efectivo).
- **D34 · Doble custodia** de bóveda activada.
- **D35 · Imputación de medios**: medios inmediatos primero, en el orden de la pantalla; el cheque al final.
- **D36 · Cobro entre oficinas** permitido.
- **D37 · Cheque recibido** apagado en R0 hasta que cada cooperativa lo encienda.

**Bancos y pagos de terceros**
- **D10 · Partidas por identificar**: alertas a los 30, 60 y 90 días; siguen como pasivo; nunca se reclasifican solas.
- **D11 · Consignación reportada en ventanilla**: queda por verificar hasta encontrarla en el extracto.
- **D12 · Excedentes no interactivos**: saldo a favor del pagador en Recaudo.
- **D13 · Saldos a favor de Cartera**: en caja o por orden de pago, a elección del deudor (en R0, por orden de pago).
- **D14 · Recaudo pendiente de aplicar**: sólo para personas de una carga aplicada sin activar, desde R1, aplicado en la
  activación.
- **D15 · Identificación**: propone y una persona confirma; con referencia exacta y dígito válido se aplica al cargar,
  a nombre de quien carga, si el parámetro lo permite; nunca la aplica un proceso.
- **D21 · Cheques recibidos**: de tercero sí, posfechados no (en custodia sólo si el parámetro lo admite); canje
  manual hasta R2; devolución desde R0.
- **D38 · Modalidad por canal**: créditos «cuota del ciclo»; cuentas «cuotas» o «libre».

**Tesorería**
- **D16 · Archivo de desembolsos de Cartera**: convive; Tesorería no paga esas órdenes y concilia el archivo que
  Cartera le informa (conflicto C2).
- **D17 · «Girada» al confirmar el banco** (respuesta, extracto confirmado o registro manual con soporte); el cheque,
  al entregarlo.
- **D18 · Segregación**: preparar y aprobar son dos personas distintas por defecto; que una tercera registre el
  resultado es configurable.
- **D19 · Aprobación de lotes** por monto con niveles sucesivos; un solo nivel si el monto cabe en la atribución.
- **D20 · Sin efectivo en órdenes**: el efectivo sale por la caja (conflicto C10; regla en FR-114).
- **D29 · Conciliaciones abiertas al cierre contable**: aviso informativo.
- **D39 · Conciliador**: Contabilidad.
- **D40 · Período de espera de una cuenta destino cambiada**: configurable, sin valor sembrado (pendiente del dueño).

**Cumplimiento e integración**
- **D22 · Umbrales SARLAFT** sembrados con los de la Supersolidaria (5 y 30 millones), «pendiente de validar» por el
  oficial; coincidencia en listas vinculantes: bloquea siempre; en las demás: sigue con alerta.
- **D23 · Reporte de transacciones** en Recaudo; el de operaciones sospechosas, en la feature SARLAFT.
- **D24 · GMF**: el del extracto manda; el configurado estima y advierte; los débitos a cuentas contables se evalúan
  por clase de egreso.
- **D25 · Matriz**: validación previa que bloquea en pantalla; recibos de caja por lotes resumidos; lo demás en
  línea; pasar a «advertir» exige permiso y queda auditado.
- **D31 · Puesta en marcha** sin hechos contables, con cuadre por cuenta y acta o cuadre provisional para las
  diferencias.
- **D32 · Lo heredado de tarjeta débito** no se toca en esta feature (P9).
- **D41 · Conservación**: diez años (Código de Comercio), pendiente de validar.

### Pendientes del dueño

No se inventan: hasta tenerlos, rige el parámetro o el defecto, y lo que **bloquea la salida** lo dice, con su fecha
límite alineada al plan de implantación.

**Datos de COOFLOPAL (dueño)**
- **P1 · Bancos y estructuras.** Qué bancos y cuentas usa el primer día y sus estructuras de extracto, de recaudo, de
  pagos y de respuesta de pagos; el formato de pagos de AV Villas sigue pendiente desde la 010 (T147). **Bloquea la
  salida** sólo si no se acepta el formato genérico o el portal del banco (FR-213); fecha límite **06/11**.
- **P6 · Pagadurías y su respuesta.** Pagadurías de COOFLOPAL, convenios, prelación y estructura de respuesta;
  confirma si la libranza es un canal principal. **Bloquea la salida** de la pagaduría (no de la caja); fecha límite
  **06/11**, porque la quincena del 15/12 exige archivo hacia el 10/12.
- **P2 · Cheques y convenios.** Si COOFLOPAL recibe cheques en ventanilla (para encender la clase en R0) y si tiene
  convenios de recaudo con referencia o corresponsales. Fecha límite **06/11**.
- **P3 · Bóveda.** Si cada oficina maneja bóveda o la caja principal hace de bóveda. Fecha límite **06/11**.
- **P4 · Personal, niveles y segregación.** Quiénes son cajeros, supervisores y suplentes, tesorero, aprobadores y
  conciliador; niveles de aprobación por monto de lotes, traslados y retiros; si basta con dos personas en Tesorería;
  período de espera de las cuentas destino cambiadas. **Bloquea la salida** (roles, alcances y segundo factor son
  condición del go/no-go); fecha límite **13/11**.
- **P13 · Servicios a asociados.** Lista de servicios (funerario, odontología y similares), cuáles se venden a plazos
  con financiación de Cartera y cuáles quedan como cuenta por cobrar de Comercial; si alguna cuenta por cobrar
  comercial sin Cartera debe cobrarse en la caja de Recaudo antes de R1. Fecha límite **13/11**.
- **P14 · Devolución de aportes en efectivo.** Si COOFLOPAL necesita devolver aportes al retiro en efectivo por caja
  (exige que Captaciones lo autorice por el contrato, R1) o basta la orden de giro. Fecha límite **13/11**.
- **P15 · Escenarios de no-go parcial.** Qué hace la ventanilla si Cartera o Aportes no se activan el 01/12 y el plan
  de reversa. **Bloquea la salida**; fecha límite **20/11**.
- **P5 · Pagos a proveedores**: confirmar que entran en T2 y, mientras tanto, siguen en la Tesorería heredada.
- **P9 · Lo heredado de tarjeta débito y convenios con redes**: si se retira con la guarda junto con lo de Tesorería.
- **P16 · Plan de implantación.** Ajustar en el plan: la capacitación de Cartera en recaudos (K19) y la de Tesorería
  (M19) a fechas con R0/T0 disponibles en QA; el paralelo (K24, K26) en QA; las tareas de la Tesorería heredada (M10,
  M19, M22, M28, M33) se conservan; el ensayo general del 23/11 al 25/11 y el go/no-go del 28/11.

**Norma y contabilidad (oficial de cumplimiento, contadora y tributarista)**
- **P7 · SARLAFT.** Umbrales vigentes del sector solidario (5 y 30 millones o los que rijan en 2026), estado de la
  suspensión del reporte, alcance del Anexo 2 (sólo efectivo o todas las transacciones), reporte de ausencia, señales
  de fraccionamiento, listas vinculantes que la plataforma consulta, y la estructura del reporte. **Bloquea la
  salida** en los umbrales y las listas (fecha límite **20/11**); la estructura del reporte, antes del primer
  vencimiento (enero de 2027).
- **P12 · Matriz de Recaudo y Tesorería** con la contadora: roles y cuentas de cada hecho. **Bloquea la salida**
  (la completitud al 100 % es condición del go/no-go); fecha límite **20/11**.
- **P8 · Faltantes, sobrantes y partidas**: cuentas, plazo del sobrante antes de ingreso, plazos de las alertas y
  destino de las partidas no identificadas. Forma parte de la matriz; fecha límite **20/11**.
- **P10 · GMF** (tributarista): hechos generadores de los débitos a cuentas contables por clase de egreso,
  exenciones de los desembolsos por operación y de las cuentas de giro. Fecha límite **20/11**.
- **P11 · Fecha valor de las partidas tardías**: confirmar las enmiendas E-01 y E-02 (versión 2 de los contratos) y,
  mientras tanto, la corrección por condonación.
- **P17 · Fondo de liquidez**: qué cuentas lo constituyen y el procedimiento de aviso previo. Fecha límite **20/11**.
- **P18 · Cuadre de apertura**: quién aprueba el cuadre provisional y la fecha límite del definitivo (del 02/12 al
  04/12 según el plan).
- **P19 · Conservación**: confirmar el plazo de diez años y su fuente.

## Dependencias

- **013 Cartera** y **014 Captaciones y aportes**: contratos, mensajes, registro de participantes y requisitos de
  activación que esta feature adopta; enmiendas E-01 y E-02 (versión 2 de los contratos para la fecha valor de las
  partidas y la línea de pagaduría en Captaciones, consulta de la carga sin activar, mensaje del archivo de
  desembolsos, recaudadora única, umbrales separados, cancelación con entrega, fondo de liquidez, recaudos
  esperados, retiro de los dobles); sus pruebas de punta a punta migran a los módulos reales.
- **012 Comercial**: catálogo de medios, denominaciones, redes, adquirentes y datáfonos, caja del punto de venta
  (separada, C1 = A), efectivo para el registro único, facturas de proveedor por pagar, ventas a crédito sin Cartera,
  plataforma de integración; enmienda E-03.
- **009 Contabilidad**: matriz, enrutador de hechos, validación previa, cuentas bancarias del plan, consultas de
  pendientes y de movimientos por cuenta, guarda de cierre y del cuadre provisional; enmienda E-04.
- **010 Nómina**: motor de archivos planos y bancos con código de transferencia; dispersión de nómina; descuento de
  faltantes con autorización; enmienda E-05.
- **011 Adjuntos**: soportes de origen de fondos, certificaciones bancarias, imágenes de talonarios, archivos de pago
  y de respuesta, extractos.
- **008 Personas**: persona maestra, buscador, alta en un paso y cuentas bancarias de la persona con doble control.
- **SARLAFT** (sin especificar): usa los datos y alertas de esta feature; decide el reporte de operaciones
  sospechosas.
- **Plan de implantación de COOFLOPAL**: paralelo, ensayo, go/no-go y convivencia con la Tesorería heredada (P16).
- **Lo que deben validar**: el dueño (D1 a D41 y los pendientes P1 a P6, P9, P13 a P16, P18), el oficial de
  cumplimiento (P7, P17), la contadora (P8, P11, P12, P18) y el tributarista (P10, P19).
