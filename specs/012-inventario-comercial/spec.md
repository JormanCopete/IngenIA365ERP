# Feature Specification: Inventario comercial renovado — catálogo, existencias, compras, ventas y facturación

**Feature Branch**: `012-inventario-comercial`
**Created**: 2026-09-24
**Status**: Draft — decisiones del dueño del 2026-09-24 incorporadas (ver Clarifications) y revisión
de consistencia aplicada (ver Supuestos, «Decisiones por defecto»)
**Input**: Solicitud del dueño: renovar desde cero el módulo base de inventario comercial de una
organización que vende a clientes y a asociados, de contado y a crédito. Debe ser fácil, funcional,
dinámico, seguro, auditable y parametrizable, a la altura de Odoo, SAP Business One, Siigo, Alegra y
Zoho Inventory, y quedar integrado con Contabilidad y con Cartera sin acoplarse a ellas. La solicitud
trae:
- diez requisitos transversales: R1 integridad, R2 documentos inmutables, R3 auditoría verificable,
  R4 seguridad y segregación, R5 parametrización con vigencia, R6 desacoplamiento por mensajes,
  R7 idempotencia, R8 precisión decimal, R9 usabilidad y R10 normativa colombiana;
- diez capacidades, de A (catálogo) a J (migración), y cinco criterios de aceptación clave;
- un fuera de alcance: la lógica interna de Contabilidad y Cartera, la nómina y el MRP.

Pide expresamente no modificar la constitución y marcar como pendiente de aclaración todo conflicto
con ella. Las tres aclaraciones que salieron de esa revisión ya las respondió el dueño (ver
Clarifications).

La solicitud dejó sin llenar «[NOMBRE DE LA ORGANIZACIÓN]» y «[cooperativa / empresa comercial]».
Se lee como un módulo para **toda** cooperativa o empresa comercial cliente del ERP, con COOFLOPAL
como la primera que lo usará (ver Supuestos).

## Contexto

Estado verificado el 2026-09-24 en el código, las especificaciones vigentes y el plan de implantación.

### El módulo actual

1. **Es un traslado tabla por tabla de SOLIDO que nunca se terminó.** Tiene 24 tablas, creadas en la
   base de toda cooperativa y en los dos motores. Muy probablemente están vacías: ninguna semilla ni
   migración las llena.
2. **La existencia no es confiable.**
   - Es un número entero guardado en dos sitios que nadie reconcilia: un campo del producto y la
     suma de los movimientos.
   - No hay existencia por bodega ni método de costeo: el costo del producto no se actualiza nunca.
   - Las cantidades no admiten decimales.
3. **Los consecutivos se toman como «el último + 1»**, sin protección ante concurrencia: dos cajeros
   pueden obtener el mismo número.
4. **Sólo funcionan los maestros** (15 catálogos). Movimientos, factura y anulación corren, pero sin
   punto de venta, turno, vendedor, lista de precios, forma de pago, retención ni crédito. No existen
   precios, descuentos, pedidos, conteo físico, compras ni factura electrónica.
5. **Ninguna operación de inventario produce contabilidad.** Sus tres escritores contables están
   marcados como pendientes de la entrega E3 de la feature 009.
6. **Ninguna de sus 87 rutas exige permiso**, sólo sesión. Los permisos de inventario existen
   únicamente en una semilla retirada que nadie ejecuta.
7. **Varias pantallas no funcionan.** De las 20:
   - Facturación es un «módulo en construcción», aunque el manual en línea la describe como si
     funcionara.
   - Nuevo movimiento, kardex, inventario valorizado y vendedores fallan porque sus rutas o
     contratos no coinciden con los del servidor.
   - Ninguna cumple las convenciones de permisos ni del indicador de carga.

   La única operación que hoy crea y retira el rol de vendedor, y mantiene la marca «Vendedor» de la
   persona, vive en este módulo.
8. **No hay camino utilizable para migrar datos de SOLIDO.** El guion está congelado y sólo corre en
   un motor de base de datos que producción no usa. Además inserta 18 de 24 tablas y no coincide con
   la definición de las tablas. La tabla de control de costos por período de SOLIDO no se mapeó a
   ningún sitio.

### La plataforma alrededor

9. **Contabilidad (feature 009) recibe a los módulos por un contrato único y sincrónico.**
   - El comprobante y la operación del módulo se guardan juntos o no se guarda ninguno (FR-036 de la
     009). Ningún módulo queda con operaciones sin comprobante (SC-004 de la 009).
   - La alternativa de eventos procesados después se evaluó y se descartó (decisión R2 de la 009):
     «el módulo quedaría aprobado sin comprobante si el evento falla».
   - Inventario ya tiene sus tipos de comprobante asignados en ese contrato.
   - Corregir es reversar el comprobante entero, una sola vez (FR-030 de la 009), y sólo el módulo
     dueño puede reversar lo suyo (FR-038 de la 009).
   - Las tarifas de impuesto viven en la cuenta de impuesto, con vigencia, y cada línea se valida
     contra ellas.
   - Cada línea lleva la sucursal contable de la cooperativa (la de la 009).
10. **No hay infraestructura de mensajes.**
    - No hay bandeja de salida, despachador de eventos ni bus.
    - Los trabajos de fondo recorren las cooperativas una por una; el despachador de correos es el
      precedente, y abre la base de cada cooperativa sin pasar por el camino común de las
      operaciones.
    - Hoy no hay forma de ejecutar, en segundo plano, una operación del ERP en nombre de una
      cooperativa. Si se intentara sin fijar la cooperativa, la auditoría caería **en silencio** en
      la base de auditoría global.
11. **Cartera no ofrece consultas.** No expone el cupo disponible ni el estado del asociado (activo,
    en mora, bloqueado). Nómina la integra dentro del mismo proceso y lee sus datos directamente. En
    el maestro, el asociado tiene cupo, clase y estado heredados de SOLIDO.
12. **La auditoría no cumple todavía lo que pide el Principio X.** Lo que ya hace:
    - cada operación queda en la base de auditoría de su cooperativa;
    - esa base no admite modificar ni borrar.

    Lo que le falta:
    - no registra la IP ni la diferencia antes/después, que el Principio X exige;
    - la entrega a la base de auditoría no está garantizada;
    - los eventos no llevan sello de integridad: sólo los PDF exportados tienen firma verificable.
13. **Permisos**: se asignan por capacidad y cooperativa, y sin permiso la respuesta es «no existe».
    - El alcance por sucursal sólo aplica a la digitación contable.
    - No hay aprobación por montos ni en varios niveles. Contabilidad tiene «cuatro ojos» opcional y
      Nómina, segregación por política.
14. **No hay varias empresas dentro de una cooperativa.** Contabilidad trabaja con una sola, y
    «multiempresa» en el ERP significa varias cooperativas, cada una en su base (Principio IV). Hay
    dos nociones de sucursal:
    - las oficinas, en la base administrativa;
    - las sucursales contables, en la base de la cooperativa, que son las que usa Contabilidad.

    También hay centros de costo.
15. **No hay catálogo de impuestos.** Los impuestos son cuentas contables con tarifa y vigencia. No
    existe el impuesto nacional al consumo (INC), ni ReteIVA y ReteICA como tipos propios.
16. **El patrón de parámetros con vigencia existe en Nómina**: políticas de la empresa y parámetros
    legales con vigencia, un único lector y ningún valor legal en el código. La configuración general
    del sistema no tiene vigencia ni historial.
17. **No existe nada para la factura electrónica.** Para la nómina electrónica (feature 010, entrega
    N3, pendiente) se decidió un servicio central **sin estado**: el ERP arma el documento y guarda
    todo, y el servicio calcula, firma y transmite. Además:
    - allí «proveedor tecnológico» significa **Ingenia365 habilitado como proveedor**, con el mismo
      servicio central;
    - la 010 **descartó** contratar a un proveedor tecnológico externo por API;
    - los secretos ante la DIAN no se guardan en el ERP ni en el repositorio.
18. **Dinero**: dos decimales por convención general. Los documentos no llevan moneda: todo es en
    pesos.
19. **Clientes, proveedores y vendedores son personas del maestro único** (Principio V). Las personas
    se escriben desde un solo diálogo (feature 008). Los vendedores tienen su tabla de rol, que la
    constitución nombra expresamente. La plataforma ya tiene un registro de consentimientos de
    tratamiento de datos.

### El calendario

20. **COOFLOPAL usa comercio, y su plan de implantación choca con renovar desde cero** (ver FR-095).
    El plan dice:
    - parametrizar **este** módulo desde el 2026-09-26: grupos, bodegas, puntos de venta, turnos,
      vendedores y comisiones, listas de precios, tipos de movimiento y catálogo;
    - marcha en paralelo con SOLIDO en noviembre: ventas por punto de venta y turno, contado y
      crédito, compras, traslados, devoluciones y cierres de turno, comparando kardex y valorizado
      con el sistema anterior;
    - conteo físico el 30/11, carga del inventario inicial del 30/11 al 02/12 y **salida en vivo el
      2026-12-01**;
    - la factura electrónica figura «por confirmar».

    Las fechas de ese plan ya no rigen para este módulo: el dueño las define cuando la aplicación esté
    lista. COOFLOPAL sí está obligada a facturar electrónicamente (Clarifications).

## Clarifications

### Session 2026-09-24

- Q: ¿Con qué sale COOFLOPAL el 2026-12-01 y qué pasa con el módulo actual? → A: **A**. Sale con el
  módulo nuevo en su alcance mínimo: entregas I1 a I3, más I4 si está obligada a facturar
  electrónicamente. Su parametrización se prepara en las plantillas de importación del módulo nuevo,
  no en las pantallas actuales, y el módulo actual se retira sin migrar datos (FR-095).
- Q: ¿Cómo se integra Inventario con Contabilidad, si la feature 009 exige que la operación y su
  comprobante sean atómicos? → A: **B, con un parámetro que indica si el comprobante pasa en línea, por
  lotes o no pasa a contabilidad**. Son mensajes asíncronos, con una validación previa «¿es
  contabilizable?» antes de confirmar. La 009 se enmienda para Inventario (FR-074 a FR-079 y FR-082).
- Q: ¿Por dónde se emiten los documentos electrónicos ante la DIAN? → A: **C, configurable**. Van por
  un proveedor tecnológico autorizado y por software propio a través del servicio central de la 010,
  detrás del mismo adaptador. Primero el proveedor, y el software propio cuando exista ese servicio.
  Cada cooperativa elige su modo (FR-064).
- Q: ¿Qué fechas rigen el ensayo, el conteo, la carga y la salida? → A: **Ninguna fija en esta
  especificación**. El dueño las define cuando la aplicación esté lista (FR-095).
- Q: ¿Qué se hace con lo que depende de Cartera y con las decisiones por defecto de la revisión? → A:
  **Queda pendiente sólo el proceso que requiere comunicación con Cartera** (entrega IC). Lo demás
  avanza, incluidas las decisiones por defecto de los Supuestos.
- Q: ¿COOFLOPAL está obligada a facturar electrónicamente? → A: **Sí**, y por eso I4 entra en su
  salida. La obligación es un **parámetro de cada cooperativa**, porque pueden llegar empresas que no
  lo estén (FR-063).
- Q: ¿Cómo se vende a crédito mientras el proceso con Cartera (IC) está pendiente? → A: **B, crédito
  provisional**:
  - se vende a crédito sin consultar cupo, con la política «permitir con aprobación» de FR-061;
  - la cuenta por cobrar la registra Contabilidad desde «VentaFacturada»;
  - los mensajes para Cartera se acumulan como pendientes de validar. Cuando exista Cartera, los
    recibe, crea las obligaciones y valida el cupo a la fecha de cada venta (F2).
- Q: ¿Cómo son los medios de pago? (pedido del dueño al iniciar el plan) → A: **Dinámicos y
  múltiples, todos configurables**:
  - varias tarjetas de crédito y varias de débito, efectivo, créditos de asociados, crédito comercial
    a clientes, consignación, transferencias, bonos y los que la cooperativa defina;
  - cada uno llega automáticamente a la cuenta contable que le corresponde;
  - el cuadre y el cierre de caja por punto de venta se hacen por medio de pago (sección K, FR-096 a
    FR-101).

## Conflictos con la constitución y con decisiones vigentes

La solicitud pide marcar todo conflicto con la constitución. Ésta es la revisión.

### Con la constitución

**No hay conflicto directo** si los requisitos se leen como se indica abajo. Hay dos lecturas
posibles que sí chocarían, y esta especificación las descarta:

- **«Multiempresa» (capacidad B).** Leída como varias empresas que comparten existencias o se
  trasladan mercancía **entre bases de cooperativa**, violaría el Principio IV, que prohíbe todo
  cruce entre cooperativas. Se lee así: cada empresa con NIT propio es una cooperativa del ERP con su
  propia base, y dentro de ella hay un solo emisor. Mercancía entre dos de ellas es una venta de una
  y una compra de la otra.
- **R6.** Leído como «un proceso aparte escribe la contabilidad por fuera de las operaciones del
  ERP», violaría los Principios III y X: toda escritura pasa por el camino común, con validación,
  auditoría y actor. Se lee así: quien consume los mensajes ejecuta operaciones normales del ERP, por
  cooperativa y por el camino común. **El actor queda registrado como corresponde** (FR-083):
  - el procesamiento automático (en línea, o un lote a su hora programada) tiene como actor al
    **proceso de integración de la cooperativa**, identificado como tal, con el mensaje o el lote
    como origen y sin IP de persona;
  - un lote ordenado a mano, un reproceso o un envío posterior tienen como actor a la persona que lo
    ordenó;
  - el usuario que originó cada documento viaja en el mensaje y queda como dato, nunca como actor;
  - el procesador no usa los permisos de ese usuario.

| Principio | Cómo lo cumple esta especificación |
|---|---|
| I · Spec first | Esta especificación, antes de plan y tareas. También especifica las piezas del lado de Contabilidad que la integración exige (FR-073 a FR-083), que se construyen en esta feature como enmienda de la 009. Lo que Cartera debe construir necesita su propia especificación (D-02). |
| II · Capas | Nada de esta especificación lo altera; lo verifica el plan. |
| III · Operaciones por el camino común | Toda escritura es una operación del ERP con su validación y su auditoría, incluida la que dispara un mensaje recibido o un lote de contabilización. |
| IV · Una base por cooperativa | Existencias, documentos, mensajes y parámetros viven en la base de cada cooperativa. El procesamiento de mensajes recorre las cooperativas una por una y fija, para cada una, el mismo contexto que una petición: su base de datos y su base de auditoría. Sin cooperativa resuelta, falla; nunca escribe en la auditoría global (D-03). Nada cruza entre bases. Un servicio central de documentos electrónicos, si lo hay, no guarda estado (como en la 010). |
| V · Persona maestra | Clientes, proveedores, asociados y vendedores son personas del maestro, que se crean y modifican sólo desde el diálogo único de personas. Un documento comercial o fiscal confirmado guarda una **copia inmutable de la identificación fiscal de la contraparte tal como se emitió** (nombre o razón social, documento, dirección y régimen). No es una duplicación de las que prohíbe el Principio V, que se refiere a las tablas de rol: un documento no es una tabla de rol. Reimpresiones y representaciones gráficas usan esa copia, o el archivo firmado si existe. Esa copia sólo cambia en la corrección del caso a de FR-066 (FR-011). La tabla de rol de vendedores se conserva. |
| VI · Identificador público | Rutas, pantallas, mensajes y auditoría usan sólo el identificador público. |
| VII · Borrado lógico y auditoría | Toda entidad hereda la base auditable del Principio VII. El kardex, los documentos confirmados y los mensajes emitidos no se borran ni se modifican por ninguna vía de la aplicación (SC-013). El mantenimiento técnico autorizado que admite el Principio XI les aplica igual que a los movimientos contables. La única excepción de la aplicación es corregir un documento electrónico rechazado por la DIAN (FR-066, caso a), que agrega una versión auditada. |
| VIII · Validación doble | Toda regla se valida en pantalla y en el servidor, y el servidor es la fuente de verdad. Los mensajes van en español y dicen qué hacer. |
| IX · Errores visibles | Un mensaje rechazado, un lote que no corrió, una consulta a Cartera sin respuesta y un documento que la DIAN rechaza quedan visibles para quien debe actuar (FR-022). Nada falla en silencio. |
| X · Trazabilidad | Toda acción queda con actor, fecha, origen, antes/después y motivo. El actor del procesamiento sigue la regla de arriba (FR-083). Un comprobante resumido por lote conserva la lista de documentos y usuarios de origen. **Hoy la plataforma no registra IP ni diferencias**, y este módulo lo necesita (D-03). |
| XI · Inmutabilidad | Se extiende a inventario. Son **hechos inmutables** las líneas del kardex y los documentos confirmados: sólo se corrigen con un documento contrario, con el documento de corrección fiscal (nota crédito o nota de ajuste, FR-066) o con una línea de ajuste, nunca reescribiendo (salvo el caso a de FR-066). Son **proyecciones reconstruibles** desde el kardex, fuera del Principio XI, el saldo acumulado, la existencia por ubicación y lote y las capas PEPS (FR-002). |
| XII · Migraciones | Retirar las tablas del módulo actual es una migración destructiva: exige respaldo, segundo revisor y el marcador de aprobación. Aplica a la base administrativa y a todas las de cooperativa. |

### Con decisiones vigentes de otras features

Aquí **sí hay conflictos**. Los tres que cambiaban el alcance los decidió el dueño el 2026-09-24 (ver
Clarifications). Los demás se resuelven como se indica, y la enmienda de la 009 los recoge (D-01).

- **C1 · R6 frente al contrato de la 009 (FR-036, decisión R2 y SC-004).** La 009 exige que la
  operación y su comprobante sean atómicos, ya descartó los eventos posteriores y no admite
  operaciones sin comprobante. R6 pide que Inventario siga operando aunque Contabilidad no reciba.
  **Decidido**: mensajes asíncronos con validación previa y un modo de paso parametrizable (FR-074 a
  FR-078). Lo pendiente, rechazado o que no pasa queda visible en la bandeja.
- **C2 · Inventario ya está asignado al contrato sincrónico de la 009** (FR-039 y la tabla de su
  contrato). Esta feature reemplaza las tareas de la 009 que lo iban a conectar (la validación de las
  cuentas de producto e IVA y la contabilización de factura, entradas y salidas). Las pantallas de
  cuentas de producto e IVA se reemplazan por la matriz de reglas, que cumple las mismas validaciones
  de cuenta (FR-016 y FR-017 de la 009).
- **C3 · Sólo el módulo dueño corrige lo suyo (FR-038 de la 009).** Se conserva: Contabilidad nunca
  corrige por su cuenta un comprobante nacido de Inventario. Lo que cambia es el camino (C7).
- **C4 · Cartera y Nómina se integran hoy dentro del mismo proceso**, y Nómina lee datos de Cartera
  directamente. R6 lo prohíbe para Inventario, así que Cartera debe ofrecer consultas y recibir
  mensajes (D-02).
- **C5 · El calendario de COOFLOPAL** (contexto 20). **Decidido por el dueño**: sale con el módulo
  nuevo en su alcance mínimo, en las fechas que él defina (FR-095). **Por defecto** (decisión 1 de los
  Supuestos, aceptada): el ensayo previo corre en una cooperativa de ensayo, fuera de producción.
- **C6 · Factura electrónica frente al diseño de la 010.** En la 010, «proveedor tecnológico»
  significa Ingenia365 habilitado como proveedor, y contratar a un proveedor externo por API fue una
  alternativa descartada. **Decidido por el dueño** para los documentos comerciales:
  - un **proveedor tecnológico externo** primero;
  - el software propio por el servicio central de la 010 cuando exista;
  - los dos detrás del mismo adaptador y configurables por cooperativa (FR-064).

  Que Ingenia365 actúe como proveedor tecnológico no es parte de esta feature. Podría sumarse después
  detrás del mismo adaptador.
- **C7 · Corregir un documento de Inventario frente a la reversión de la 009 (FR-030 y FR-038).** En la
  009, corregir es reversar el comprobante entero, una sola vez. Aquí hay dos cosas que no caben en
  eso:
  - la anulación es una operación nueva, con fecha propia (FR-006), y fiscalmente afecta el período
    en que ocurre;
  - un comprobante resumido reúne muchos documentos, y anular uno no puede reversar a los demás.

  **Resuelto así**: Contabilidad registra cada anulación, nota o ajuste como un **comprobante nuevo**
  de su propio mensaje, enlazado al documento original y a su comprobante. El comprobante original
  nunca se marca reversado (FR-079).
- **C8 · Dos fuentes de tarifas de impuesto.** Inventario calcula con su catálogo de impuestos
  (FR-013), y la 009 valida cada línea contra la tarifa de la cuenta de impuesto. Si se desalinean, el
  mensaje se rechaza después de confirmar. **Resuelto así**, sin que Inventario lea datos contables:
  - la matriz asigna a cada impuesto y tarifa su cuenta;
  - la validación previa (FR-074) y el reporte de completitud (FR-082) señalan todo impuesto cuya
    cuenta tenga otra tarifa en esa fecha.

## Alcance

### Dentro de esta feature

- **Catálogo**: productos, servicios, combos, kits ensamblados y productos con variantes;
  categorías, marcas, códigos de barras, unidades con conversión, lote, serie y vencimiento,
  impuestos y conceptos de retención, grupo contable e imágenes; importación y exportación con
  revisión previa.
- **Bodegas, ubicaciones y existencias**: existencia física, reservada, disponible y en tránsito
  (con bodegas de tránsito), y mínimos, máximos y punto de reorden.
- **Kardex inmutable y documentos** para todos los movimientos, con clases fijas y tipos de
  documento parametrizables.
- **Costeo**: promedio ponderado, y PEPS como opción; retroactivos por ajuste, prorrateo de costos
  adicionales y cierre y reapertura de período.
- **Compras**: solicitud, orden, recepción, factura del proveedor con cruce a tres vías y devolución;
  acuse de recibo y recibo del bien sobre las facturas de proveedor a crédito.
- **Ventas**: cotización, pedido con reserva, remisión, factura y notas crédito y débito; POS con
  cajas y turnos; listas de precios, descuentos con topes y promociones.
- **Formas de pago y crédito**: contado, crédito a cliente, crédito a asociado y pagos mixtos, con
  consulta de cupo y estado a Cartera.
- **Documentos electrónicos DIAN**: factura, notas crédito y débito, documento equivalente POS y su
  nota de ajuste, y documento soporte y su nota de ajuste, por proveedor tecnológico o por software
  propio, según la cooperativa, con sus dos contingencias.
- **Integración con Contabilidad y Cartera por mensajes**, con validación previa, un modo de paso a
  contabilidad (en línea, por lotes o no pasa), bandeja y reporte de conciliación.
- **Las piezas del lado de Contabilidad que esa integración exige**, como enmienda de la 009:
  - la matriz de reglas, con sus pantallas y su plantilla;
  - el procesamiento en línea y por lotes, y los comprobantes por documento o resumidos;
  - el registro de anulaciones y notas;
  - las consultas y el aviso antes del cierre contable.
- **Reportes, tablero, alertas y sugerido de compra.**
- **Transversales**: permisos por capacidad, bodega y monto; aprobaciones en varios niveles;
  auditoría verificable; parámetros con vigencia.
- **Puesta en marcha**: plantillas de parametrización, ensayo, saldo inicial con cuadre contra
  contabilidad, activación por bodega y retiro del módulo actual.

### Fuera de esta feature

- **El resto de la lógica interna de Contabilidad** (todo lo que no está en la lista de arriba) y
  **toda la de Cartera**. Lo que Cartera debe construir para esta integración va en una
  especificación propia (D-02).
- **Nómina y MRP de manufactura.** El ensamble de kits es una transformación simple, no
  planeación de producción.
- **Cálculo y liquidación de comisiones de vendedores.** Sí están el vendedor en cada venta y el
  margen por vendedor.
- **Tesorería**: pagos a proveedores, consignaciones y conciliación bancaria. El arqueo de caja del
  POS sí está.
- **Comercio electrónico**, tienda web y POS sin conexión.
- **Los demás eventos RADIAN** sobre facturas recibidas: aceptación expresa o tácita, endoso y los
  demás. El acuse de recibo y el recibo del bien sí están (FR-050). Importar el archivo de la factura
  electrónica del proveedor para prellenar su registro está, como opcional; el archivo se lee, no se
  guarda.
- **Ingenia365 como proveedor tecnológico** (C6).
- **Multimoneda operativa**: importaciones en moneda extranjera y diferencia en cambio. La
  estructura queda lista.
- **Movimientos históricos de SOLIDO**: no se migran. Se entra con **saldos**, como en la 009.

### Entregas

El corte lo decidió el dueño (FR-095). Las fechas también las define él, cuando la aplicación esté
lista:
- **Salida de COOFLOPAL**: I1 a I4, porque está obligada a facturar electrónicamente. Primero un
  ensayo fuera de producción, y después la salida bodega por bodega.
- **Pendiente**: la entrega IC, el proceso que se comunica con Cartera, hasta que exista la
  especificación de Cartera (D-02). Mientras tanto se vende a crédito provisional (F2).
- **Después de la salida**: I5 e I6.

La obligación de facturar electrónicamente es un parámetro de cada cooperativa (FR-063). Una
cooperativa obligada no confirma ventas fiscales hasta que exista I4.

| Entrega | Contenido |
|---|---|
| **I1 · Núcleo** | **Primero, las plantillas de importación de la parametrización** (FR-095), para que COOFLOPAL prepare sus datos. Después: catálogo básico con impuestos y conceptos de retención, bodegas, ubicaciones y bodegas de tránsito, kardex y costo promedio, ajustes, consumos y bajas con aprobación, traslados, conteos (totales y cíclicos, salvo por clase ABC), cierre de período, compra directa (recepción y factura del proveedor), devolución a proveedor, carga y validación del saldo inicial, importación de cifras de SOLIDO y comparativos de kardex y valorizado, rol de vendedor, alertas, permisos, auditoría y parámetros. Retiro del módulo actual. El retiro gravado de consumo interno necesita la lista general de precios, que llega con I3. |
| **I2 · Integración** | Mensajes a Contabilidad y a Cartera, validación previa y modo de paso a contabilidad. Del lado de Contabilidad, como enmienda de la 009 y en la ruta crítica de la salida: matriz de reglas con sus pantallas y su plantilla, procesamiento en línea y por lotes, comprobantes por documento y resumidos, registro de anulaciones, consultas, aviso antes del cierre contable y actor de proceso. Además: bandeja, conciliación, cuadre del saldo inicial y activación de bodegas. |
| **I3 · Ventas y POS** | POS con cajas y turnos, medios de pago configurables y múltiples, cierre de caja por punto y por medio con movimientos de caja, factura (o comprobante no electrónico para una cooperativa no obligada), nota crédito (o nota no electrónica) y devoluciones, contado y mixto, crédito provisional mientras IC está pendiente (F2), listas de precios y descuentos con topes. Las plantillas de cajas, precios y topes se entregan en I1 y se cargan cuando I3 existe. |
| **IC · Crédito con Cartera (pendiente)** | Consulta de estado, cupo y líneas de crédito antes de vender a crédito. Entrega a Cartera de «VentaACreditoRegistrada» y sus ajustes, incluidos los acumulados durante el crédito provisional, y validación de las ventas pendientes. Espera la especificación de Cartera (D-02). |
| **I4 · Documentos electrónicos DIAN** | Factura, nota crédito, documento equivalente POS y su nota de ajuste, documento soporte y su nota de ajuste, resoluciones de numeración y las dos contingencias. Modo proveedor tecnológico externo; el de software propio, cuando exista el servicio central de la 010. |
| **I5 · Compras completas y costeo avanzado** | Solicitud, orden, recepción contra orden y parcial, cruce a tres vías, prorrateo, emisión desde el ERP del acuse de recibo y el recibo del bien, PEPS y retroactivos. |
| **I6 · Comercio ampliado y analítica** | Cotización, pedido con reserva, remisión, nota débito (y su versión electrónica), promociones, variantes, combos y ensamble, lote, serie y vencimiento, conteo por clase ABC, reposición, reportes avanzados y tablero. |

### Glosario

| Término | Significado en esta especificación |
|---|---|
| Sistema anterior | SOLIDO, el sistema de escritorio que la cooperativa usa hoy. |
| Módulo actual | El inventario que hoy tiene el ERP, a medio trasladar de SOLIDO, que esta feature retira. |
| Producto inventariable | Ítem con existencia y kardex. |
| Servicio | Ítem que se vende o se compra sin existencia ni kardex. |
| Combo | Ítem virtual que se vende como uno y descuenta sus componentes. |
| Kit ensamblado | Ítem inventariable que se produce con un documento de ensamble, consumiendo componentes. |
| Variante | Producto concreto que resulta de combinar los atributos de una plantilla (p. ej. talla M, color azul), con código, códigos de barras y existencia propios. |
| Unidad base | Unidad en que se lleva el kardex del producto. Las de compra y venta se convierten a ella con un factor exacto. |
| Kardex | Registro cronológico, sólo de adición, de cada entrada, salida y ajuste de costo por producto, bodega, ubicación y lote. Cada línea es un hecho que no cambia; saldos y capas se calculan a partir de él. |
| Existencia física | Suma del kardex de una bodega operativa. Las bodegas de tránsito no cuentan como existencia física de ninguna bodega operativa. |
| Reservada | Comprometida por pedidos vigentes. |
| Disponible | Física menos reservada. |
| En tránsito | Lo despachado hacia una bodega y aún no recibido. Vive en una bodega de tránsito. |
| Bodega de tránsito | Bodega virtual donde queda la mercancía despachada y no recibida. No se vende ni se despacha desde ella. |
| Posición | Disponible + en tránsito + por recibir (órdenes aprobadas no recibidas). Es lo que se compara con el punto de reorden. |
| Quiebre | Disponible por debajo del mínimo. |
| Documento | Operación con tipo, consecutivo, fecha, líneas y estado: borrador, en aprobación, confirmado o anulado. |
| Clase de documento | Categoría fija del sistema que define el efecto sobre el inventario, si es fiscal y qué mensajes emite (FR-036). |
| Tipo de documento | Variante parametrizable de una clase: consecutivo, prefijo, aprobación, modo de paso y campos obligatorios. |
| Confirmar | Volver definitivo un documento: recibe número, produce efectos y ya no se edita. |
| Anular | Deshacer un documento confirmado. Según el caso: en los no fiscales y en el registro de documentos recibidos de proveedores, con un documento contrario enlazado y motivo (FR-006); en los fiscales que emite la cooperativa, validados o expedidos en contingencia, con el documento de corrección que la norma exige; en los rechazados por la DIAN, según FR-066 (a) a (c). |
| Grupo contable | Clasificación del producto que la contabilidad usa para decidir sus cuentas. Inventario no conoce cuentas. |
| Matriz de reglas contables | Tabla parametrizable, del lado de Contabilidad: tipo de operación + grupo contable + bodega → cuentas, incluidas las de cada impuesto y tarifa. |
| Mensaje de negocio | Hecho versionado que Inventario emite al confirmar o anular (p. ej. «VentaFacturada»), con todo lo necesario para que el destino actúe sin leer datos de Inventario. |
| Mensaje informativo | Mensaje que Contabilidad recibe y registra sin generar comprobante, como el cierre de período o el saldo inicial. |
| Bandeja de mensajes | Consulta de los mensajes emitidos y su estado (pendiente, en lote, procesado, rechazado, no aplica o validación fallida) con el motivo, y la opción de reprocesar. |
| Modo de paso a contabilidad | Parámetro que dice cómo llega un documento a Contabilidad: **en línea** (se contabiliza apenas se confirma), **por lotes** (se acumula y se contabiliza junto con otros) o **no pasa** (no llega). Se sella en el documento al confirmarlo. |
| Lote de contabilización | Conjunto de mensajes por lotes que se contabilizan juntos, a la hora programada o cuando alguien lo ordena. |
| Comprobante resumido | Comprobante que un lote genera para varios documentos de la misma fecha, tipo y sucursal, en lugar de uno por documento. |
| Validación previa | Pregunta que Inventario hace a Contabilidad antes de confirmar: ¿las líneas que generaría este documento cumplen las reglas contables, y está abierto el período? |
| Proceso de integración | Actor de sistema de cada cooperativa que figura en la auditoría cuando los mensajes se procesan solos. |
| Cupo | Crédito disponible que Cartera informa para una persona. Inventario lo consulta y nunca lo guarda como dato de operación; la respuesta sólo queda como evidencia en la auditoría (FR-060). |
| Segmento | La clase del asociado en el maestro. Un cliente que no es asociado no tiene segmento. |
| Canal | Vía de venta (mostrador, POS, televenta u otras que la cooperativa defina). La fija el punto de venta o el tipo de documento. |
| Cruce a tres vías | Comparación, línea por línea, de lo ordenado, lo recibido y lo facturado por el proveedor, en cantidad y precio, contra tolerancias. |
| Prorrateo | Reparto de costos adicionales de compra (flete, seguro) entre los productos recibidos. |
| Período de inventario | Mes de la cooperativa que se cierra: bloquea documentos con fecha en él y fija el valorizado. Es uno para toda la cooperativa. |
| Foto del conteo | Existencia teórica congelada al abrir un conteo físico, contra la que se comparan las cantidades contadas. |
| Sesión de caja (turno) | Apertura con base, ventas, movimientos de caja y cierre con arqueo de una caja del POS por un cajero. |
| Medio de pago | Forma concreta de pagar que la cooperativa configura (p. ej. «Visa Redeban», «Bono de mercado», «Consignación Bancolombia»), de una clase fija: efectivo, tarjeta de crédito, tarjeta de débito, crédito a asociado, crédito comercial, consignación, transferencia, bono o vale, cheque u otro. |
| Adquirente y datáfono | La red que procesa los pagos con tarjeta (p. ej. Redeban o Credibanco) y el terminal con que se cobra. |
| Arqueo | Comparación, por medio de pago, de lo esperado en una sesión con lo contado o comprobado al cerrarla. |
| Movimiento de caja | Retiro parcial o ingreso de base dentro de una sesión de caja, documentado. |
| Paso | Cambio de pantalla o confirmación explícita del usuario. Las lecturas del lector y las teclas de cantidad no cuentan como pasos. |
| Resolución de numeración | Autorización de la DIAN: prefijo, rango y vigencia para numerar facturas, documentos equivalentes POS, documentos soporte y la numeración de contingencia. Las notas no tienen resolución. |
| Validado por la DIAN | Documento electrónico que la DIAN aprobó en la validación de la DIAN, que es distinta de la validación previa contable. Es distinto de la «aceptación», que es un acto del comprador sobre una factura a crédito. |
| CUFE, CUDE, CUDS | Códigos únicos: CUFE de la factura; CUDE de las notas crédito y débito, del documento equivalente POS y de su nota de ajuste; CUDS del documento soporte y de su nota de ajuste. |
| Documento equivalente POS | Documento electrónico que la DIAN admite en ventas de mostrador en lugar de la factura, salvo que el comprador la pida. Se corrige con una nota de ajuste. |
| Documento soporte | Documento electrónico que el comprador emite en compras a quien no está obligado a facturar. Se corrige con una nota de ajuste. |
| Contingencia del facturador | Falla del lado de la cooperativa (su conexión, su proveedor o su software). Se factura con la numeración de contingencia y se transmite después. |
| Contingencia de la DIAN | Falla de la DIAN. Se entrega la factura sin la validación de la DIAN y se transmite cuando vuelve. |
| Consumidor final | Persona genérica del maestro para ventas sin identificar al comprador, dentro de lo que la norma permite. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Preparar el catálogo y las bodegas para operar (Priority: P1)

El jefe de inventario deja listo el catálogo antes de la primera venta:
- **Productos**, a mano o importándolos, con su unidad base, las unidades de compra y venta con
  conversión, uno o varios códigos de barras, categoría, marca, impuestos, concepto de retención y
  grupo contable.
- **Bodegas y ubicaciones** de cada sucursal.

Importar muestra antes de guardar qué se va a crear o actualizar y todos los errores.

**Why this priority**: sin catálogo ni bodegas no hay ninguna otra operación, y es lo primero que
COOFLOPAL tiene que preparar (FR-095).

**Independent Test**: importar una plantilla de productos con errores y otra corregida, crear dos
bodegas con ubicaciones, recibir mercancía en cajas y ver el kardex en unidades.

**Acceptance Scenarios**:

1. **Given** una plantilla con 500 productos y tres errores (un código de barras repetido, una unidad
   inexistente y un impuesto inexistente), **When** la importan, **Then** no se guarda nada y la
   pantalla lista los tres errores con fila, columna y qué hacer. Corregida y reimportada, **Then**
   quedan los 500.
2. **Given** un producto con unidad base «unidad» y compra por «caja × 12», **When** reciben 3 cajas,
   **Then** el kardex registra 36 unidades, y el documento muestra 3 cajas.
3. **Given** un producto con movimientos, **When** intentan borrarlo, **Then** el sistema no lo permite
   y ofrece inactivarlo o bloquearlo:
   - inactivo, no se ofrece en documentos nuevos pero conserva su historia y se puede consultar;
   - bloqueado, no admite ningún movimiento salvo el conteo y la recepción de lo que ya estaba en
     tránsito.
4. **Given** un código de barras que ya tiene otro producto, **When** lo asignan, **Then** se rechaza
   nombrando al producto que lo tiene.
5. **Given** un escáner en modo teclado, **When** el bodeguero lee un código de barras en cualquier
   campo de producto, **Then** el producto (y su unidad de empaque, si el código es de la caja) queda
   elegido sin más pasos.
6. **Given** las plantillas de parametrización diligenciadas por la cooperativa antes de tener el
   módulo (FR-095), **When** las importa en orden, **Then** pasan por la misma revisión previa de
   la importación, todo o nada. Además:
   - los vendedores deben ser personas ya registradas en el maestro, citadas por su documento; la fila
     de uno que no existe se rechaza nombrándola;
   - las plantillas de cajas, listas de precios y topes de descuento se cargan cuando exista I3.

---

### User Story 2 - Existencias confiables sobre un kardex que sólo crece (Priority: P1)

Toda entrada y salida nace de un documento confirmado y deja líneas en el kardex, que nadie puede
modificar ni borrar. Esto incluye ajustes, consumo interno y bajas por daño o vencimiento. Quien
consulta ve, por producto y bodega, la existencia física, la reservada, la disponible y la que está
en tránsito. Con el stock negativo prohibido, ninguna combinación de operaciones simultáneas deja
una existencia disponible por debajo de cero.

**Why this priority**: es el requisito R1 y el primer criterio de aceptación del dueño. El módulo
actual falla aquí: guarda la existencia en dos sitios que no cuadran y numera sin protección.

**Independent Test**: lanzar ventas simultáneas del mismo producto con existencia limitada, anular
un ajuste confirmado y correr la verificación de integridad del kardex.

**Acceptance Scenarios**:

1. **Given** 5 unidades disponibles y el stock negativo prohibido, **When** dos cajeros confirman al
   mismo tiempo ventas de 4 unidades cada uno, **Then** se confirma una. La otra se rechaza diciendo
   que queda 1 disponible, y la existencia nunca queda negativa.
2. **Given** un ajuste confirmado, **When** alguien intenta editarlo o borrarlo, **Then** no hay forma.
   **When** lo anulan con motivo, **Then**:
   - aparece un documento contrario enlazado, con su propia fecha y al costo del original;
   - el kardex muestra las dos líneas;
   - la existencia vuelve a la de antes.
3. **Given** cualquier producto y bodega, **When** corren la verificación de integridad, **Then** la
   existencia y los saldos coinciden con lo que resulta de sumar el kardex. Cualquier diferencia se
   informa como incidente.
4. **Given** un ajuste negativo cuyo monto supera el umbral de aprobación de su tipo, **When** lo
   envían, **Then** queda «en aprobación» sin afectar la existencia. Sólo la afecta cuando lo aprueba
   otra persona con permiso para ese monto y esa bodega. El creador no puede aprobarlo.
5. **Given** un usuario sin alcance sobre la bodega B, **When** busca o intenta operar documentos de
   B, **Then** no los ve ni puede hacerlo, tampoco llamando directamente al servidor.

---

### User Story 3 - Costo correcto y períodos cerrados (Priority: P1)

Cada entrada recalcula el costo promedio ponderado del producto. Cada salida sale a ese costo, y las
devoluciones regresan al costo con que salieron. Al fin de mes, el jefe de inventario cierra el
período:
- ya no entra ningún documento con fecha en él;
- queda fijado su valorizado.

Reabrirlo exige un permiso especial y queda auditado.

**Why this priority**: sin costo correcto no hay margen, ni costo de venta, ni cuadre con
contabilidad. El módulo actual no calcula costo.

**Independent Test**: una secuencia de compras, ventas, devoluciones y traslados cuyo costo se
calculó a mano (casos dorados); cerrar el mes; intentar un documento con fecha en él; reabrir con
permiso.

**Acceptance Scenarios**:

1. **Given** 10 unidades a $1.000 **When** entran 10 a $1.300, **Then** el costo promedio es $1.150.
   Una venta de 5 sale a $1.150, y su devolución entra a $1.150 aunque el promedio haya cambiado
   después.
2. **Given** un traslado, **Then** la mercancía viaja al costo de origen. Mientras está en tránsito
   sigue en el valorizado, en la bodega de tránsito, así que el traslado no cambia el valor total del
   inventario.
3. **Given** el período de septiembre cerrado, **When** confirman un documento con fecha en
   septiembre, **Then** se rechaza nombrando el período.
4. **Given** un conteo abierto con foto en septiembre, **When** intentan cerrar septiembre, **Then** el
   cierre no se completa hasta cerrar o anular el conteo.
5. **Given** un usuario con el permiso especial, **When** reabre septiembre con motivo, **Then**:
   - queda abierto;
   - la acción queda auditada;
   - se emite «PeriodoInventarioReabierto».

   Sólo se reabre el último período cerrado.
6. **Given** una compra cuyo IVA no es descontable según la regla de FR-044 (p. ej. destinada a
   consumo interno), **When** la reciben, **Then** ese IVA forma parte del costo. El impuesto al
   consumo pagado en compras siempre forma parte del costo.

---

### User Story 4 - Arrancar con saldo inicial cuadrado, bodega por bodega (Priority: P1)

La cooperativa carga su inventario inicial por plantilla: producto, bodega, ubicación, cantidad y
costo unitario, y lote y serie cuando el producto los controla. El sistema lo valida completo antes
de guardar.

El saldo inicial **no genera comprobante**: el valor de ese inventario ya está en los libros, por la
apertura contable de la 009 o por los registros que la contabilidad lleva. Antes de activar una
bodega, el sistema compara esos saldos contables, a su fecha de corte y por grupo contable y
conjunto de cuentas mapeadas, con el valorizado de todas las bodegas que usan esas cuentas: las
activas y la que se activa, con el del módulo nuevo; las demás, con las cifras importadas de SOLIDO.
Entonces:
- con cuadre, se activa;
- con diferencia, sólo se activa si alguien con permiso la acepta con motivo.

Cada bodega sale en vivo cuando se activa, y su saldo inicial se fecha en su fecha de corte, la
víspera de la activación. Hasta entonces, el sistema anterior sigue registrando sus ventas.

**Why this priority**: es la capacidad J y la condición para salir en vivo. COOFLOPAL necesita contar
y cargar su inventario inicial antes de su salida.

**Independent Test**: cargar un saldo con errores y luego corregido, compararlo con saldos contables
que difieren en un grupo, aceptar la diferencia con motivo y activar una bodega.

**Acceptance Scenarios**:

1. **Given** una plantilla de 20.000 líneas con un producto inexistente y una bodega inexistente,
   **When** la cargan, **Then** no se guarda nada y se listan los dos errores con fila y columna.
2. **Given** un saldo inicial válido, **When** piden activar, **Then** el sistema muestra, por grupo
   contable y conjunto de cuentas mapeadas, el saldo contable, la diferencia y el valorizado de todas
   las bodegas que usan esas cuentas:
   - las activas y la que se activa, con el valorizado del módulo nuevo;
   - las demás, con las cifras importadas de SOLIDO.
3. **Given** una diferencia, **When** alguien sin el permiso especial intenta activar, **Then** no
   puede. **When** lo hace quien lo tiene, con motivo, **Then** se activa y queda auditado.
4. **Given** una bodega activa, **Then** sólo la opera el módulo nuevo. **Given** una bodega no
   activa, **Then** el módulo nuevo sólo admite en ella su documento de saldo inicial (y su
   anulación).
5. **Given** el saldo inicial confirmado, **Then** su mensaje es informativo: Contabilidad lo registra
   sin generar comprobante, y la conciliación lo cuenta como incluido en el saldo contable.
6. **Given** cifras del sistema anterior importadas para una fecha, **When** piden el comparativo,
   **Then** se ve, por producto y bodega, la existencia y el valor de cada sistema y la diferencia.

---

### User Story 5 - Vender en el punto de venta con lector, rápido (Priority: P1)

El cajero abre su turno con una base, escanea los productos y cobra con cualquier combinación de los
medios de pago que la cooperativa configuró: efectivo, varias tarjetas, bonos, transferencia,
consignación o crédito. Entrega el documento que corresponda. Al final del turno cuenta la caja: el
sistema compara el arqueo con lo esperado por cada medio de pago. Cada pago llega solo a su cuenta
contable.

Precios:
- el precio sale de la lista vigente más específica que aplica al cliente, a su segmento, al canal y
  a la sucursal (FR-053);
- los descuentos tienen tope por rol, y por encima del tope se necesita la aprobación de alguien con
  tope suficiente.

**Why this priority**: es el uso diario de COOFLOPAL y el criterio del dueño «5 ítems con lector en
menos de 30 segundos».

**Independent Test**: abrir turno, vender 5 ítems con lector de contado y con pago mixto, devolver
uno, cerrar turno con una diferencia y cronometrar la venta.

**Acceptance Scenarios**:

1. **Given** un turno abierto, **When** el cajero hace 6 lecturas de 5 productos distintos (uno leído
   dos veces) y cobra en efectivo, **Then**:
   - la venta queda confirmada, con su representación gráfica lista para entregar, en menos de 30
     segundos desde la primera lectura (SC-004);
   - el producto repetido suma cantidad;
   - el sistema calcula las vueltas.
2. **Given** un pago mixto, **When** la suma de los pagos no iguala el total, **Then** no se
   puede confirmar, y la pantalla dice cuánto falta o sobra.
3. **Given** un descuento del 12 % y un tope del cajero del 5 %, **When** lo aplica, **Then** se pide
   la aprobación de un usuario con tope suficiente, con su propia identidad, y queda registrado quién
   aprobó.
4. **Given** el cierre de turno con $20.000 menos de lo esperado en efectivo, **Then** la diferencia
   exige motivo y, por encima de la tolerancia, aprobación. El informe de cierre muestra ventas,
   devoluciones y retiros por medio de pago.
5. **Given** una caja sin turno abierto, **Then** no se puede vender en ella. **Given** un turno
   abierto, **Then** no se abre otro en la misma caja.
6. **Given** el cajero trabajando sólo con teclado y lector, **Then** puede completar la venta sin
   usar el ratón, con atajos visibles en pantalla.
7. **Given** tres listas vigentes (una general, una para el segmento del asociado y una para ese mismo
   asociado), **When** le venden a ese asociado, **Then** aplica la del asociado, y la pantalla dice
   qué lista usó. **Given** otro asociado del mismo segmento, **Then** aplica la del segmento.
8. **Given** una venta de mostrador ya entregada, **When** el cliente devuelve un producto, **Then**:
   - se emite el documento de corrección que corresponde: la nota de ajuste del documento
     equivalente POS, la nota crédito si se había facturado, o la nota no electrónica si la venta se
     hizo con comprobante no electrónico;
   - el dinero se devuelve por el mismo medio de la venta, salvo permiso;
   - la mercancía vuelve al costo con que salió.
9. **Given** una venta de $500.000, **When** el cliente paga $200.000 con una tarjeta de crédito
   Visa, $150.000 con una Mastercard de otro adquirente, $100.000 con un bono y el resto en efectivo,
   **Then**:
   - cada pago queda con su medio, valor y referencia (aprobación de cada tarjeta, número del bono);
   - el bono no puede volver a usarse;
   - en Contabilidad, cada pago llega a la cuenta de su medio sin digitar nada.
10. **Given** el cierre de un turno con ventas en efectivo, dos tarjetas, transferencias y un retiro
    parcial a caja fuerte, **When** el cajero cierra, **Then**:
    - el sistema muestra lo esperado por cada medio y pide lo contado según cómo se arquea;
    - una diferencia en efectivo exige motivo y, sobre la tolerancia, aprobación, y al aprobarse se
      contabiliza sola;
    - el informe de cierre sale por medio de pago, con el detalle de tarjetas por adquirente y
      datáfono.
11. **Given** un pago de $80.000 registrado como Visa que en realidad pasó por Mastercard, **When** el
    cajero registra la reclasificación entre medios con motivo, **Then**:
    - la venta y su pago original no cambian;
    - si la política lo exige, queda pendiente hasta que otro usuario la aprueba;
    - al confirmarse, el esperado de la sesión baja $80.000 en Visa y sube $80.000 en Mastercard;
    - Contabilidad recibe «MovimientoDeCajaRegistrado» y pasa el valor de la cuenta de Visa a la de
      Mastercard sin digitar nada.

---

### User Story 6 - Vender a crédito a asociados y clientes con el cupo de Cartera (Priority: P1)

Antes de confirmar una venta a crédito, o la parte a crédito de un pago mixto, el sistema pregunta a
Cartera por el estado de la persona (activo, en mora, bloqueado), su cupo disponible y las líneas de
crédito aplicables.
- Si todo está bien, se eligen plazo, cuotas y línea dentro de lo que Cartera permite, y al confirmar
  se avisa a Cartera para que cree la obligación.
- Si Cartera no responde, se aplica la política de la cooperativa: bloquear, o permitir con
  aprobación y la marca «pendiente de validar».

**Why this priority**: la cooperativa vende a crédito a sus asociados, es la integración que la
solicitud detalla.

**Estado**: la comunicación con Cartera está **pendiente** (entrega IC, que depende de D-02).
Mientras tanto rige el **crédito provisional** (F2), y por eso la venta a crédito sí está en la
salida. Los escenarios 1 a 5 son para cuando exista IC; el 6 es el del crédito provisional.

**Independent Test**: vender a crédito a un asociado activo con cupo, a uno en mora y a uno sin cupo
suficiente; simular que Cartera no responde con cada política; anular la venta.

**Acceptance Scenarios**:

1. **Given** un asociado activo con cupo de $500.000, **When** le venden $300.000 a 6 cuotas en una
   línea permitida, **Then** la venta se confirma y Cartera recibe la venta a crédito con persona,
   valor, plazo, cuotas, línea y documento.
2. **Given** un asociado en mora o bloqueado, **When** intentan venderle a crédito, **Then** no se
   confirma y la pantalla dice el estado que informó Cartera.
3. **Given** cupo de $200.000, **When** la venta a crédito es de $300.000, **Then** no se confirma, y
   la pantalla ofrece pagar la diferencia de contado (pago mixto).
4. **Given** que Cartera no responde en el tiempo parametrizado:
   - con la política «bloquear», la venta a crédito no se confirma y el cajero lo ve claramente;
   - con la política «permitir con aprobación», la venta se confirma sólo con la aprobación de un
     usuario autorizado para ese monto, y queda «pendiente de validar».

   Al recibirla, Cartera evalúa estado y cupo **a la fecha de la venta**, sin contar esa misma venta, y
   deja el resultado disponible para la consulta de Inventario. **When** el resultado es negativo,
   **Then**:
   - la venta sigue confirmada;
   - queda «validación fallida» visible en la bandeja;
   - se genera una alerta (FR-022).

   Nada se anula solo.
5. **Given** una venta a crédito confirmada, **When** la anulan o le emiten nota crédito, **Then**
   Cartera recibe el ajuste enlazado al documento original. La venta nunca muestra saldos de cartera.
6. **Given** la entrega IC pendiente, **When** venden $300.000 a crédito a un asociado, **Then**:
   - no se consulta cupo;
   - la venta exige la aprobación de un usuario autorizado para ese monto y queda «pendiente de
     validar»;
   - Contabilidad registra la cuenta por cobrar desde «VentaFacturada»;
   - el mensaje para Cartera queda guardado y visible en la bandeja como pendiente.

   **When** exista Cartera, **Then** recibe en orden los mensajes acumulados (ventas, notas y
   anulaciones), crea las obligaciones y valida el cupo a la fecha de cada venta. Las que no pasen
   quedan en «validación fallida» con alerta, y nada se anula solo.

---

### User Story 7 - Contabilidad recibe cada operación sin que Inventario escriba asientos (Priority: P1)

Al confirmar o anular un documento, Inventario emite los mensajes de negocio con todo lo que
Contabilidad necesita: tercero, sucursal, centro de costo, bodega, grupo contable, impuestos y
retenciones desglosados, costo y documento origen. Contabilidad los convierte en comprobantes con su
matriz de reglas.

Cada documento pasa a contabilidad según el modo de paso sellado al confirmarlo:
- **en línea**: se contabiliza apenas se confirma;
- **por lotes**: los mensajes se acumulan y se contabilizan juntos a la hora programada o cuando
  alguien lo ordena, un comprobante por documento o uno resumido;
- **no pasa**: el documento no llega a Contabilidad, y la conciliación lo muestra aparte.

Antes de confirmar un documento que sí pasa, Inventario pregunta a Contabilidad si es contabilizable:
- ¿las líneas que generaría cumplen las reglas de sus cuentas?
- ¿está abierto el período?

Si algo falla, no confirma y dice qué falta. La bandeja muestra cada mensaje y su estado, y el reporte
de conciliación compara el inventario valorizado con el saldo contable. Procesar dos veces el mismo
mensaje no duplica nada.

**Why this priority**: sin contabilidad no se sale en vivo, y es el requisito R6 del dueño. El dueño
decidió mensajes asíncronos con validación previa y un modo de paso parametrizable
(Clarifications).

**Independent Test**: configurar las compras en línea, las ventas POS por lotes resumidos y los
traslados sin paso a contabilidad. Luego:
- confirmar un documento de cada uno;
- intentar confirmar uno cuya línea no cumple una regla de su cuenta;
- procesar el lote;
- reenviar un mensaje ya procesado;
- anular una factura;
- conciliar.

**Acceptance Scenarios**:

1. **Given** las compras en línea, **When** confirman una compra, **Then** existe su «CompraRecibida»
   y, en menos de un minuto, Contabilidad la procesó en un comprobante con el origen enlazado.
2. **Given** una operación cuyo grupo contable no tiene regla en la matriz, cuya cuenta exige un centro
   de costo que el documento no trae, cuyo impuesto tiene otra tarifa en su cuenta, o con fecha en un
   período contable cerrado, **When** intentan confirmarla, **Then** no se confirma. La pantalla dice
   la línea, la cuenta y la regla que falla, y quién puede corregirla.
3. **Given** las ventas POS por lotes resumidos por día, tipo y sucursal, **When** corre el lote del
   día, **Then**:
   - sale un comprobante por día, tipo y sucursal, fechado en la fecha de las ventas y no en la del
     lote;
   - las líneas cuya cuenta exige tercero, documento cruce o base gravable conservan su detalle;
   - el lote muestra qué documentos contiene;
   - el actor registrado es el proceso de integración, y los usuarios de origen quedan como dato.
4. **Given** los traslados sin paso a contabilidad, **When** confirman uno, **Then** no llega a
   Contabilidad, su mensaje queda «no aplica» y la conciliación lo muestra como partida aparte.
5. **Given** un mensaje ya procesado, **When** llega otra vez (por un reintento o al repetir el lote),
   **Then** no se crea un segundo comprobante.
6. **Given** un mensaje de un documento ya confirmado que Contabilidad rechaza porque el período
   contable se cerró antes de correr el lote, **Then**:
   - el documento sigue confirmado;
   - el mensaje aparece en la bandeja con el motivo.

   **When** reabren ese mes contable (y el ejercicio, si ya se cerró) según las reglas de la 009 y lo
   reprocesan, **Then** se contabiliza con su fecha original.
7. **Given** cero mensajes pendientes, en lote o rechazados, **When** piden la conciliación a una fecha
   de corte, **Then** la diferencia entre valorizado y saldo contable por grupo es cero:
   - el saldo inicial cuenta como incluido en el saldo contable;
   - las partidas de los tipos que no pasan se muestran aparte;
   - las bodegas que siguen en SOLIDO se cuentan con sus cifras importadas.
8. **Given** una factura validada por la DIAN que iba en un comprobante resumido, **When** la anulan,
   **Then**:
   - se emite su nota crédito total, con su propia fecha;
   - Contabilidad registra un comprobante nuevo con esa fecha, enlazado a la factura y al comprobante
     resumido;
   - el comprobante resumido no se toca.
9. **Given** una venta que no pasó a contabilidad, **When** la anulan, **Then** la anulación tampoco
   pasa, aunque el tipo haya cambiado de modo después.
10. **Given** documentos «no aplica» de octubre, **When** alguien con el permiso especial los envía a
    Contabilidad (FR-078), **Then** se envían con sus anulaciones, notas, ajustes y documentos
    derivados, en orden. Si uno
    de ellos se anula después, la anulación también llega.

---

### User Story 8 - Documentos electrónicos ante la DIAN (Priority: P1)

Toda factura, nota crédito, documento equivalente POS con su nota de ajuste, y documento soporte con
su nota de ajuste se genera, numera, firma y transmite según la norma vigente. La nota débito llega
con I6.
- Se numera dentro de una resolución vigente, cuando el tipo la exige. Las notas llevan su propio
  consecutivo, sin resolución.
- Lleva su código único (CUFE, CUDE o CUDS), código QR y representación gráfica.
- En operación normal se entrega al comprador **después** de la validación de la DIAN.
- El sistema sigue su estado (validado, validado con notificaciones o rechazado con motivos) y
  reintenta sin duplicar.

Si falla el lado de la cooperativa, se factura con la numeración de contingencia. Si falla la DIAN, se
entrega sin su validación. En los dos casos la venta no se detiene, y el documento se transmite
después, con alerta antes de que venza el plazo.

Cada cooperativa elige su modo de emisión: proveedor tecnológico externo o, cuando exista, software
propio.

**Why this priority**: es obligación legal para quien vende. El dueño decidió los dos modos detrás del
mismo adaptador, configurables por cooperativa (FR-064). COOFLOPAL está obligada, así que entra en su
salida (FR-095). Para cada cooperativa, la obligación es un parámetro (FR-063).

**Independent Test**: en el ambiente de pruebas de la DIAN:
- emitir una factura, la nota crédito que la anula, un documento equivalente POS con su nota de ajuste
  y un documento soporte;
- simular un rechazo y corregirlo;
- simular cada contingencia y ver la cola y la alerta.

**Acceptance Scenarios**:

1. **Given** una resolución vigente, **When** confirman una factura, **Then**:
   - recibe el número siguiente del rango, su CUFE, QR y PDF;
   - queda «validada» al responder la DIAN;
   - sólo entonces su representación gráfica se entrega al comprador.
2. **Given** una factura validada, **When** quieren anularla, **Then** el sistema no la anula: emite una
   nota crédito total enlazada, con su propia fecha y su CUDE. **Given** un documento equivalente POS
   validado, **Then** su corrección es la nota de ajuste.
3. **Given** un documento rechazado por la DIAN, **Then** se ven los motivos.
   - **When** la corrección no cambia tercero, cantidades, bases, impuestos ni total, **Then** se
     regenera y se reenvía con el mismo número, sin mensajes nuevos.
   - **When** sí los cambia, **Then** el documento del ERP se anula sin efecto fiscal y un documento de
     reemplazo, enlazado, se confirma con el mismo número.

   En ningún caso queda un número sin explicación.
4. **Given** que falla la conexión de la cooperativa o su proveedor, **When** confirman ventas, **Then**:
   - se emiten con la numeración de contingencia;
   - quedan en cola con aviso visible;
   - al restablecerse, se transmiten con la referencia a su número de contingencia.

   Si se acerca el plazo legal sin transmitir, sale una alerta.
5. **Given** que la DIAN no está disponible, **When** confirman ventas, **Then** la factura se entrega
   sin la validación de la DIAN, marcada como tal, y se transmite cuando la DIAN vuelve.
6. **Given** una resolución al 90 % de su rango o a 30 días de vencer, **Then** se avisa. **Given** una
   resolución vencida o agotada, **Then** no se emite con ella. Una nota crédito sí puede referirse a
   una factura cuya resolución ya venció.
7. **Given** una compra a un proveedor no obligado a facturar, **When** la registran, **Then** se
   emite el documento soporte con su CUDS. **When** se le devuelve mercancía, **Then** se emite su nota
   de ajuste.
8. **Given** una cooperativa que emite por un proveedor tecnológico, **When** cambian su configuración
   a otro proveedor, con vigencia, **Then**:
   - los documentos numerados desde esa vigencia salen por el nuevo canal, sin cambiar documentos ni
     pantallas;
   - los que estaban pendientes, rechazados o en contingencia se reenvían por el canal con que se
     numeraron.
9. **Given** una cooperativa con el parámetro «obligada a facturar electrónicamente» apagado, **When**
   vende, **Then** emite el comprobante de venta no electrónico (FR-036) y ningún documento
   electrónico de venta. **When** una cooperativa obligada no tiene I4 activo o no tiene resolución
   vigente, **Then** no se confirma ninguna venta fiscal.

---

### User Story 9 - Comprar directo, recibir y devolver mercancía (Priority: P1)

El comprador registra la compra directa y el bodeguero recibe la mercancía. Después se registra la
factura del proveedor, con su prefijo, número y CUFE, y con impuestos y retenciones calculados por las
reglas vigentes: el concepto de retención del producto, el régimen de las partes y el municipio de
la operación. Si algo llegó mal, se devuelve al proveedor al costo con que entró.

La recepción contra una orden de compra, total o parcial, llega con I5 (US13).

**Why this priority**: sin entradas no hay existencias que vender, y COOFLOPAL las necesita desde su
salida.

**Independent Test**: una compra directa con factura, una devolución a proveedor y una factura a
crédito sin sus eventos, verificando el kardex, el costo, los mensajes y la alerta.

**Acceptance Scenarios**:

1. **Given** una compra directa confirmada, **Then** entra al kardex al costo neto y se emite
   «CompraRecibida». **When** se registra la factura del proveedor, **Then** se emite
   «FacturaProveedorRegistrada» con impuestos y retenciones desglosados.
2. **Given** un proveedor gran contribuyente o autorretenedor, **Then** las retenciones se calculan
   según su régimen y el concepto de cada producto. Sólo se retiene si la base es igual o superior al
   mínimo vigente. La ReteICA usa la tarifa del municipio de la operación.
3. **Given** una devolución a proveedor, **Then** sale al costo con que entró, enlazada a la recepción,
   y la diferencia con el promedio se registra como ajuste de costo.
4. **Given** una factura de proveedor a crédito sin acuse de recibo ni recibo del bien registrados,
   **Then** aparece en alertas, porque sin esos eventos no soporta costo ni IVA descontable. Hasta I5,
   la cooperativa los emite en el portal de su proveedor o de la DIAN, y el ERP registra que los
   emitió.

---

### User Story 10 - Trasladar entre bodegas en dos pasos (Priority: P1)

El bodeguero de origen despacha: la mercancía sale de su bodega y queda «en tránsito», en una bodega de
tránsito, visible para el destino. El bodeguero de destino recibe. Las diferencias quedan pendientes
hasta que alguien con permiso las resuelva (FR-039):
- si llega menos, el faltante queda en tránsito, y se resuelve con devolución al origen, baja desde
  tránsito con su causa (daño, hurto o reclamación al transportador) o recepción tardía en destino;
- si llega más, el sobrante queda registrado aparte, fuera de la existencia, hasta que se apruebe su
  ajuste.

**Why this priority**: COOFLOPAL traslada entre bodegas y puntos de venta en su operación diaria.

**Independent Test**: despachar 10 unidades, ver 10 en tránsito, recibir 9 y resolver el faltante.

**Acceptance Scenarios**:

1. **Given** un despacho de 10 unidades, **Then**:
   - el origen baja 10;
   - la bodega de tránsito sube 10, que el destino ve como «en tránsito» y no puede vender;
   - el valor total del inventario no cambia;
   - se emite «TrasladoDespachado».
2. **When** el destino recibe 9, **Then**:
   - entran 9 y se emite «TrasladoRecibido»;
   - la unidad que falta queda en tránsito como faltante pendiente, que no desaparece hasta
     resolverse con aprobación (FR-039).
3. **Given** un traslado despachado y no recibido, **When** lo anulan, **Then** la mercancía vuelve al
   origen. **Given** uno ya recibido, **Then** sólo se corrige con un traslado contrario.
4. **Given** un movimiento entre ubicaciones de la misma bodega, **Then** es de un solo paso y no
   cambia el costo ni genera mensajes contables.

---

### User Story 11 - Contar físicamente y ajustar sólo lo aprobado (Priority: P1)

El jefe de inventario abre un conteo, total o cíclico (por categoría, ubicación o selección; por clase
ABC cuando exista, en I6). El sistema congela la foto de la existencia teórica. Los contadores
capturan con lector, y el conteo puede ser ciego. Las diferencias por encima de la tolerancia se
recuentan. El ajuste se genera sólo después de aprobado.

**Why this priority**: COOFLOPAL cuenta su inventario para el saldo inicial, y el conteo es el control
básico de un inventario.

**Independent Test**: abrir un conteo cíclico de una ubicación, capturar con lector con una
diferencia, recontar y aprobar el ajuste.

**Acceptance Scenarios**:

1. **Given** un conteo abierto, **Then** la foto queda fija. Según el parámetro, los productos incluidos:
   - no admiten movimientos mientras dure el conteo (valor por defecto);
   - o sí los admiten, y sus movimientos posteriores a la foto se suman al teórico al comparar.
2. **Given** tres lecturas del mismo código, **Then** cuentan 3 unidades.
3. **Given** una diferencia por encima de la tolerancia, **Then** exige reconteo antes de poder
   proponer el ajuste.
4. **Given** el ajuste propuesto, **Then** no afecta la existencia hasta que lo apruebe alguien que no
   abrió el conteo ni contó en él, con el monto dentro de su rango. Se fecha y se valoriza según
   FR-041, y se emite «AjusteInventarioAprobado».

---

### User Story 12 - Seguridad, aprobaciones, parámetros y auditoría verificables (Priority: P1)

El administrador arma los roles de la cooperativa sobre los permisos del módulo. El módulo trae
perfiles sugeridos para: administrador, jefe de inventario, bodeguero, comprador, vendedor o cajero,
aprobador, contador (sólo consulta) y auditor (sólo lectura). Los permisos se limitan por bodega, por
punto de venta y, en las acciones con valor, por monto.

Cada tipo de documento puede exigir aprobación en uno o varios niveles, cada nivel con su umbral.
Quien crea nunca aprueba.

Todo parámetro tiene:
- un valor por defecto seguro;
- vigencia e historial;
- un cambio que exige permiso y motivo.

El auditor verifica que la bitácora no fue alterada.

**Why this priority**: son los requisitos R3, R4 y R5, y hoy el módulo no exige ningún permiso.

**Independent Test**:
- un bodeguero sin alcance sobre una bodega;
- un ajuste que pasa por dos niveles de aprobación;
- una compra directa por encima del monto máximo del comprador;
- cambiar el parámetro de stock negativo con vigencia futura;
- alterar a propósito un registro de auditoría de prueba para ver que la verificación lo detecta.

**Acceptance Scenarios**:

1. **Given** una ruta del módulo, **Then** exige un permiso propio. Sin él, responde igual que una ruta
   inexistente.
2. **Given** una política de dos niveles para ajustes (primer umbral $0, segundo $1.000.000), **When**
   un ajuste de $3.000.000 lo aprueba un aprobador, **Then** sigue «en aprobación» hasta que lo
   apruebe además el jefe de inventario. El jefe debe ser una persona distinta del creador y del
   primer aprobador.
3. **Given** un cambio del parámetro de stock negativo con vigencia desde el mes próximo, **Then**:
   - los documentos de este mes siguen con el valor anterior;
   - el historial muestra ambos valores, quién los cambió y el motivo.
4. **Given** cualquier acción del módulo, **Then** la auditoría tiene actor, fecha y hora, origen,
   antes/después y, cuando la acción lo exige, motivo (FR-007). Esto incluye crear, modificar, aprobar,
   anular, cambiar un parámetro, exportar e ingresar a una opción.
5. **Given** un registro de auditoría alterado o faltante, **When** el auditor corre la verificación de
   integridad sobre ese rango, **Then** la verificación lo señala.
6. **Given** un comprador cuyo monto máximo es $5.000.000, **When** confirma una compra de $8.000.000,
   **Then** la compra exige al menos el primer nivel de la política de aprobación de su tipo, aunque no
   alcance su umbral. Si el tipo no tiene política, se rechaza diciendo el monto máximo.

---

### User Story 13 - Ciclo completo de compras con cruce a tres vías (Priority: P2)

El comprador sigue un ciclo completo:
- solicitud de compra, aprobada;
- orden de compra, aprobada por montos y enviada al proveedor;
- una o varias recepciones parciales contra la orden;
- la factura del proveedor, que se cruza línea por línea contra lo ordenado y lo recibido, en
  cantidad y precio, con tolerancias parametrizables.

Lo que excede la tolerancia queda retenido para aprobación. Los costos adicionales (flete, seguro)
se prorratean entre lo recibido. El acuse de recibo y el recibo del bien se emiten desde el ERP.

**Why this priority**: da control sobre las compras, pero una cooperativa puede operar con compra
directa (US9) mientras llega.

**Independent Test**: orden de 100 unidades, dos recepciones (60 y 38), factura por 100 a un precio
2 % mayor con tolerancia de 1 %, y un flete prorrateado por valor.

**Acceptance Scenarios**:

1. **Given** una orden de 100 unidades y recepciones por 98, **When** la factura dice 100, **Then** la
   línea queda retenida por cantidad: se factura más de lo recibido.
2. **Given** un precio facturado 2 % mayor y una tolerancia de 1 %, **Then** queda retenida por precio.
   **When** la aprueban, **Then** la diferencia ajusta el costo de lo que sigue en existencia y el
   costo de venta de lo ya vendido, con sus mensajes.
3. **Given** un flete de $100.000 sobre una recepción de dos productos por valores de $600.000 y
   $400.000, **When** lo prorratean por valor, **Then** reciben $60.000 y $40.000 de mayor costo.
4. **Given** una factura de proveedor a crédito recibida y su recepción confirmada, **When** el
   comprador confirma, **Then** el ERP emite el acuse de recibo y el recibo del bien por el mismo canal
   de FR-064, y la alerta de US9 se cierra.

---

### User Story 14 - Ciclo comercial completo: cotización, pedido, remisión, notas y promociones (Priority: P2)

El vendedor sigue el ciclo:
- cotiza, con vigencia;
- convierte la cotización en pedido, que **reserva** existencias;
- despacha con remisión;
- factura una o varias remisiones.

Emite notas crédito (con o sin devolución de mercancía) y notas débito. Aplica promociones con
vigencia («lleve 3 pague 2», precio por cantidad, porcentaje por categoría y segmento) sin acumular
las que no son acumulables.

**Why this priority**: amplía la venta de mostrador (US5) a la venta con despacho, pero no es
necesaria para operar un almacén de mostrador.

**Independent Test**: cotización → pedido que reserva → remisión → factura; una nota crédito sin
devolución; una nota débito sobre una venta a crédito; una promoción 3×2.

**Acceptance Scenarios**:

1. **Given** 10 disponibles y un pedido de 4, **Then** quedan 4 reservadas y 6 disponibles. **When**
   vence el pedido sin despacharse, **Then** la reserva se libera sola.
2. **Given** una remisión, **Then** la mercancía sale y el costo se reconoce en ese momento. **When** se
   factura, **Then** no vuelve a descontar existencia ni costo. **Given** una remisión que supera los
   días parametrizados sin facturar, **Then** sale una alerta.
3. **Given** una nota crédito por descuento posterior, sin devolución, **Then** no afecta la existencia.
   **Given** una con devolución, **Then** la mercancía vuelve al costo con que salió.
4. **Given** una promoción 3×2 vigente, **When** venden 3 unidades, **Then** el documento muestra las 3
   unidades con un descuento no condicionado igual al precio de una. Ese descuento reduce la base
   gravable, y la promoción queda visible.
5. **Given** una venta a crédito, **When** le emiten una nota débito, **Then** antes se consulta estado
   y cupo en Cartera, como en una venta a crédito. Cartera recibe el mensaje enlazado a la venta
   original.

---

### User Story 15 - Catálogo avanzado: variantes, combos, kits, lotes, series y vencimientos (Priority: P2)

El jefe de inventario define plantillas con atributos propios (talla, color, presentación) y genera
sus variantes. Combos y kits funcionan así:
- los combos se venden como uno y descuentan sus componentes;
- los kits se ensamblan con un documento que consume componentes y produce el kit.

Para los productos que lo requieren, controla lote, vencimiento o serie. Las salidas sugieren el lote
que vence primero, y un lote vencido no se vende.

**Why this priority**: necesario para ciertos negocios (alimentos, droguería, confección), no para
empezar.

**Independent Test**: plantilla con 2 tallas × 2 colores; combo de dos productos; ensamble de un kit;
recepción por lote y venta que sugiere el más próximo a vencer; serie repetida.

**Acceptance Scenarios**:

1. **Given** una plantilla con talla (S, M) y color (azul, rojo), **Then** genera 4 variantes, cada una
   con su código, código de barras y existencia.
2. **Given** un combo de A + B, **When** se vende, **Then** bajan A y B, y el costo de venta es la suma
   de sus costos.
3. **Given** un ensamble de 5 kits, **Then** salen sus componentes, entran 5 kits al costo de lo
   consumido y se emite el mensaje de ajuste correspondiente.
4. **Given** dos lotes que vencen en 10 y en 60 días, **When** venden, **Then** se sugiere el de 10.
   **Given** un lote vencido y la política «bloquear», **Then** no se vende.
5. **Given** un producto por serie, **When** reciben una serie que ya está en existencia, **Then** se
   rechaza.

---

### User Story 16 - Costeo avanzado: PEPS, retroactivos y cambio de método (Priority: P2)

Una cooperativa puede elegir PEPS en lugar del promedio ponderado. Cambiar de método es un cambio de
política contable: sólo se hace al inicio de un período, con un permiso especial y una justificación.
El módulo entrega a Contabilidad la valoración por los dos métodos.

Si el parámetro lo permite, se registran documentos con fecha anterior dentro de un período abierto.
El sistema calcula la diferencia de costo de los movimientos posteriores y la registra como ajuste,
sin reescribir nada, y muestra el impacto antes de confirmar.

**Why this priority**: da flexibilidad, pero el promedio ponderado (P1) cubre la mayoría de los
casos.

**Independent Test**: casos dorados de PEPS; una compra con fecha anterior a tres ventas, verificando
los ajustes emitidos; un cambio de método con su reporte.

**Acceptance Scenarios**:

1. **Given** PEPS y dos capas (10 a $1.000 y 10 a $1.300), **When** venden 15, **Then** el costo es
   $16.500.
2. **Given** una compra con fecha anterior a tres ventas ya confirmadas, **When** la registran, **Then**:
   - antes de confirmar se ven los movimientos afectados y la diferencia de costo;
   - al confirmar, el kardex agrega líneas de ajuste de costo, sin cambiar las de las ventas;
   - se emite «AjusteDeCostoReconocido» por esa diferencia.
3. **Given** el parámetro de retroactivos apagado, o una fecha en un período cerrado, **Then** no se
   admite.
4. **Given** un cambio de promedio a PEPS, **When** lo registran, **Then**:
   - exige la justificación;
   - el módulo produce, por grupo contable, el valorizado por los dos métodos a la fecha del cambio y
     al inicio del período comparativo más antiguo que la cooperativa presente, o deja constancia de
     por qué no puede calcularse.

---

### User Story 17 - Reportes, tablero y reposición (Priority: P3)

Quien tiene permiso consulta y exporta a Excel y PDF:
- el kardex;
- el inventario valorizado a cualquier fecha de corte;
- la rotación y el análisis ABC;
- los productos sin movimiento y los próximos a vencer;
- el margen por producto, vendedor, cliente y punto de venta;
- las diferencias de conteo;
- el sugerido de compras.

Un tablero muestra los indicadores clave. Las alertas avisan cuando un producto llega a su punto de
reorden.

**Why this priority**: da mucho valor para decidir, pero no es condición para operar. El kardex y el
valorizado básicos van con US2 y US4.

**Independent Test**: generar cada reporte con datos conocidos y contrastar sus cifras; exportar uno
con datos personales sin el permiso.

**Acceptance Scenarios**:

1. **Given** una fecha de corte pasada, **Then** el valorizado se reconstruye desde el kardex y coincide
   con el que se cerró en ese período.
2. **Given** mínimo 10, máximo 50, punto de reorden 15 y nada por recibir:
   - con 12 disponibles y 5 en tránsito (posición 17), **Then** el producto no aparece en alertas ni en
     el sugerido;
   - con 8 disponibles y 5 en tránsito (posición 13), **Then** aparece en alertas con un sugerido de 37
     (50 − 13), y como quiebre, porque 8 está por debajo del mínimo.
3. **Given** una exportación, **Then** queda auditada. Si trae datos personales, exige el permiso
   correspondiente.

### Edge Cases

- **Concurrencia**: dos usuarios confirman salidas del último disponible. Dos cajeros piden el
  siguiente consecutivo al mismo tiempo. Se hace doble clic en «Confirmar». Hay un reintento tras
  perder la conexión justo después de confirmar. **Nada se duplica, nada queda negativo** (FR-004,
  FR-016).
- **Anulaciones encadenadas**:
  - anular una recepción cuya factura del proveedor ya se registró exige anular primero la factura;
  - anular una factura con notas crédito exige que las notas no excedan lo que queda;
  - el sistema dice qué hay que anular antes.
- **Anular una entrada cuyas unidades ya salieron**: si dejaría disponible negativo con el stock
  negativo prohibido, se rechaza con la cantidad que falta y la alternativa (devolución o ajuste). Si
  se admite, la diferencia entre su costo y el promedio vigente se registra como ajuste de costo
  (FR-006).
- **Stock negativo permitido** (en bodegas donde el parámetro lo habilita): la salida toma el último
  costo conocido. Cuando llega la entrada, la diferencia de costo se registra como ajuste y se avisa a
  Contabilidad.
- **Costo con existencia cero**: el promedio conserva el último costo, y la siguiente entrada lo
  reemplaza.
- **Conversión de unidades que no da exacta**: la diferencia queda en la misma línea como cantidad de
  redondeo visible. Si la unidad no admite los decimales necesarios, la conversión se rechaza
  (FR-017).
- **Documento con fecha en un período cerrado o posterior a hoy cuando el tipo no lo admite**: se
  rechaza nombrando la regla. Esto vale para el período de inventario, y para el período contable
  cuando el documento pasa a contabilidad y la validación previa lo informa.
- **La validación previa no responde**: se aplica el parámetro de la cooperativa (FR-074). Por
  defecto, el documento se confirma y su mensaje queda pendiente y visible; con «bloquear», no se
  confirma.
- **Contabilidad rechaza un mensaje de un documento ya confirmado.** Puede pasar si la regla o la
  cuenta cambió entre la confirmación y el lote, o si el período contable se cerró antes. El documento
  sigue confirmado (FR-074), el mensaje queda en la bandeja con motivo y la conciliación lo explica.
  Se corrige la causa (la regla, o reabriendo el mes contable según la 009) y se reprocesa con su
  fecha original: la fecha nunca se cambia.
- **Contabilidad quiere cerrar un período con mensajes de Inventario todavía sin procesar, fechados en
  él**: Contabilidad avisa antes de cerrar (D-01). Si cierra de todos modos, esos mensajes se rechazan
  a la bandeja y se recuperan como en el punto anterior.
- **Un tipo de documento cambia de modo de paso a mitad de mes**: cada documento conserva el modo
  sellado al confirmarlo. Su anulación, sus notas y sus ajustes siguen el destino del mensaje del
  original (FR-079).
- **Se anula un documento que iba en un comprobante resumido**: Contabilidad registra un comprobante
  nuevo de la anulación, con su propia fecha. El comprobante resumido no se toca (FR-079).
- **Un tipo fiscal queda sin paso a contabilidad**, por su propio valor o porque hereda el valor
  general de la cooperativa: el cambio exigió permiso especial y una confirmación explícita que
  nombra esos tipos (FR-075). La conciliación lo muestra aparte, y el tablero lo señala.
- **La tarifa de un impuesto en su cuenta contable no coincide con la del catálogo de Inventario**: la
  validación previa lo detiene antes de confirmar, y el reporte de completitud lo lista (C8).
- **Cartera responde tarde**, después del tiempo máximo: la respuesta tardía no cambia una venta ya
  resuelta por la política. Queda en auditoría.
- **Crédito provisional acumulado cuando llega Cartera**: las ventas cuyo cupo, evaluado a su fecha, no
  alcanzaba quedan en «validación fallida» con alerta. La venta sigue confirmada, y el responsable
  decide si la anula o la documenta (FR-061).
- **Cambio de grupo contable de un producto con existencia**: afecta sólo los movimientos futuros, y
  emite «GrupoContableReclasificado» con la cantidad y el valor que cambian de grupo (FR-027).
- **Precio de venta menor que el costo**: alerta o bloqueo según el parámetro.
- **Dos listas de precios con el mismo ámbito exacto y vigencias que se cruzan**: no se admite.
- **Traslado despachado en un período ya cerrado y recibido en el siguiente**: la recepción se fecha en
  el período abierto, y el tránsito queda visible en el valorizado de los dos cortes.
- **Conteo abierto al cerrar un período**: el cierre no se completa hasta cerrar o anular el conteo
  (FR-047).
- **Remisiones sin facturar al cerrar un período**: el cierre las lista y sólo se completa si alguien
  con permiso las acepta con motivo (FR-047).
- **Resolución agotada o vencida en medio de un turno**: la venta se detiene con un mensaje claro y una
  alerta a quien administra resoluciones. Nunca se numera fuera de resolución. Las notas no se
  afectan, porque no usan resolución.
- **La DIAN valida tarde un documento pendiente**: queda validado y en auditoría. Mientras no hubo
  respuesta, no se corrigió (FR-066), y los casos de rechazo sólo se aplican a un rechazo confirmado.
- **Cambia la configuración de emisión con documentos pendientes, rechazados o en contingencia**:
  esos documentos siguen por el canal con que se numeraron (FR-064).
- **Retiro gravado de consumo interno de un producto sin precio en la lista general vigente**: el
  documento no se confirma y dice qué precio falta.
- **Mensaje de una versión que el destino ya no acepta**: se rechaza con motivo visible. Nunca se
  reescribe el mensaje emitido.
- **Persona inactiva o retirada como cliente**: no se le vende a crédito. De contado, según el
  parámetro.
- **Producto bloqueado con existencia en tránsito**: la recepción del traslado sí se admite, para no
  dejar la mercancía en el aire.

## Requirements *(mandatory)*

### Functional Requirements

#### Transversales (R1 a R10)

- **FR-001** (R1) Toda variación de existencia o de costo MUST nacer de un documento confirmado.
  Ninguna pantalla, importación ni proceso edita saldos directamente.
- **FR-002** (R1) El kardex MUST ser sólo de adición. Cada línea es un **hecho** que no cambia:
  - producto, bodega, ubicación y lote o serie;
  - fecha de operación y fecha de registro;
  - documento origen;
  - cantidad en unidad base con signo (cero en las líneas de ajuste de costo);
  - costo unitario y total con que se registró;
  - usuario.

  Ningún usuario ni proceso lo modifica ni lo borra. Las anulaciones, los retroactivos, los
  prorrateos, las diferencias de precio y los negativos regularizados agregan líneas.

  El saldo acumulado en cantidad y valor, la existencia por ubicación y lote y las capas PEPS son
  **proyecciones**: se calculan desde el kardex y se pueden reconstruir.
- **FR-003** (R1) La existencia y los saldos de cada producto, bodega (incluidas las de tránsito),
  ubicación y lote MUST coincidir siempre con lo que resulta del kardex. El sistema MUST ofrecer una
  verificación que informe cualquier diferencia como incidente.
- **FR-004** (R1) Con el stock negativo prohibido, ninguna combinación de operaciones concurrentes,
  incluidas las anulaciones, MUST dejar disponible negativo. La operación que no cabe se rechaza con
  la cantidad realmente disponible.
- **FR-005** (R2) Los documentos MUST tener los estados borrador, en aprobación, confirmado y
  anulado.
  - El borrador se edita o se descarta (y el descarte queda registrado).
  - El confirmado no se edita ni se borra. La única excepción es la corrección de un documento
    electrónico rechazado por la DIAN (FR-066).
- **FR-006** (R2) Anular un documento confirmado que no es fiscal MUST generar un documento contrario,
  referenciado en ambos sentidos y con motivo obligatorio.
  - **Fecha**: se fecha en su propia fecha de operación (hoy, o la que admita el tipo). Cae siempre en
    un período de inventario abierto y, si el documento pasa a contabilidad, en un período contable
    abierto según la validación previa. Nunca hereda la fecha del original.
  - **Efecto**: revierte las cantidades al costo del original. Si la entrada anulada ya entró al
    promedio, la diferencia entre su costo y el promedio vigente se registra como ajuste de costo, como
    en FR-044.
  - **Límites**: un documento se anula una sola vez. Si tiene dependientes vigentes, el sistema MUST
    nombrarlos y exigir anularlos primero.

  Los documentos fiscales que emite la cooperativa se corrigen como dice FR-066. El registro de una
  factura o nota recibida de un proveedor se anula con un documento contrario y emite
  «DocumentoAnulado»: corrige el registro del ERP, no el documento del proveedor.
- **FR-007** (R3) Cada acción MUST quedar auditada con:
  - actor (FR-083), fecha y hora;
  - origen: IP y canal (pantalla web, app, POS o proceso);
  - entidad, y valores antes y después;
  - motivo, cuando la acción lo exige (anular, rechazar, aceptar una diferencia, cambiar un
    parámetro, reabrir un período).

  Las acciones son: crear, modificar un borrador, confirmar, aprobar, rechazar, anular, cambiar un
  parámetro, importar, exportar, imprimir o reimprimir, e ingresar a una opción del módulo.
- **FR-008** (R3) Nadie, tampoco un administrador desde la aplicación, MUST poder modificar o borrar
  un registro de auditoría. Un auditor MUST poder verificar la integridad de un rango y detectar
  cualquier registro alterado, eliminado o intercalado; la verificación misma queda registrada. La
  auditoría del módulo se conserva al menos 10 años.
- **FR-009** (R4) Cada capacidad (acción sobre un tipo de documento, consulta o parámetro) MUST
  exigir un permiso propio. Los permisos MUST poder limitarse:
  - por bodega y por punto de venta;
  - en las acciones con valor (confirmar compras, órdenes, ajustes, notas y ventas a crédito), por
    monto máximo. Por encima de ese monto, el documento exige al menos el primer nivel de la política
    de aprobación del tipo (FR-010), aunque no alcance su umbral, y además los niveles cuyo umbral
    alcance. Si el tipo no tiene política, se rechaza diciendo el monto máximo.

  El servidor lo exige; la pantalla sólo refleja lo que el servidor permitiría. Sin permiso, la
  respuesta es la misma que para algo inexistente. La excepción es un permiso que depende del contenido
  de una petición sobre algo que el usuario ya ve (fijar un costo, aceptar una diferencia, dejar sin
  paso un tipo fiscal, cambiar un parámetro concreto): ahí la respuesta es un error con código propio,
  porque no revela nada que no se vea.
- **FR-010** (R4) Cada tipo de documento que exige aprobación MUST tener una política de uno o varios
  niveles, cada uno con un umbral y un permiso.
  - Un documento exige, en orden, todos los niveles cuyo umbral alcanza. Por debajo del primero se
    confirma sin aprobación.
  - No puede aprobar el creador del documento, **y esto no es parametrizable**. Tampoco quien abrió o
    capturó un conteo, ni quien ya aprobó otro nivel del mismo documento.
  - Rechazar exige motivo y devuelve el documento a borrador.
- **FR-011** (R4, Ley 1581 de 2012) Las personas se crean y modifican sólo en el maestro, desde el
  diálogo único de personas.
  - **Copia de lo emitido**: un documento comercial o fiscal confirmado MUST guardar una copia
    inmutable de la identificación fiscal de la contraparte tal como se emitió (nombre o razón
    social, documento, dirección y régimen). Reimpresiones y representaciones gráficas usan esa copia,
    o el archivo firmado si existe. La copia sólo cambia en la corrección del caso a de FR-066, con
    antes y después auditados.
  - **Autorización**: al dar de alta una persona desde el POS o desde Compras, el diálogo muestra el
    aviso de privacidad. Registra en el registro de consentimientos de la plataforma la autorización
    del titular, o deja constancia de que no la dio. Sin autorización, sus datos sólo se usan para
    emitir y entregar el documento que la ley exige, nunca para promociones ni contacto comercial.
  - **Exportación**: exportar datos personales exige permiso y queda auditado.
- **FR-012** (R5) Cada parámetro del módulo MUST tener valor por defecto seguro, vigencia (desde y
  hasta) e historial. Cambiarlo exige permiso y motivo, y queda auditado. Un documento usa el valor
  vigente en su fecha de operación, salvo el modo de paso, el modo de emisión electrónica y la
  numeración, que se fijan al confirmar (FR-075, FR-064 y FR-038). Los
  parámetros son, al menos:
  - método y ámbito de costeo;
  - stock negativo por bodega;
  - consecutivos y prefijos;
  - políticas de aprobación y montos máximos;
  - impuestos y retenciones (FR-013);
  - listas de precios;
  - topes de descuento por rol;
  - tolerancias (cruce a tres vías, arqueo, conteo);
  - redondeo;
  - retroactividad;
  - vencimiento de reservas;
  - días máximos entre remisión y factura;
  - política ante Cartera sin respuesta;
  - política ante la validación previa sin respuesta;
  - modo de paso a contabilidad, con su granularidad y el horario de los lotes;
  - obligación de facturar electrónicamente (sí o no), y modo de emisión electrónica;
  - plazo de transmisión en contingencia;
  - bloqueo durante conteos;
  - venta de lotes vencidos;
  - venta bajo costo;
  - porcentaje de gastos de venta para los indicios de deterioro;
  - umbrales y destinatarios de las alertas.
- **FR-013** (R5, R10) El catálogo de impuestos y retenciones MUST ser del módulo (o de Core), nunca
  leído de Contabilidad (FR-014), y entregarse en I1. Registra:
  - tipo: IVA, INC, ReteFuente, ReteIVA, ReteICA y otros;
  - forma de cálculo: porcentaje sobre la base, porcentaje sobre el IVA (ReteIVA) o valor fijo por
    unidad (p. ej. el impuesto al consumo de bolsas plásticas y los impuestos saludables ICUI e IBUA de
    la Ley 2277 de 2022);
  - concepto de retención (compras, servicios, honorarios, arrendamientos, transporte y otros);
  - municipio y actividad, para ICA y ReteICA;
  - base mínima, expresada en UVT;
  - condiciones por régimen de las partes;
  - vigencia y norma que lo respalda.

  Se aplica en compras y en ventas. En ventas se aplican también las retenciones que practica un
  comprador agente retenedor. Una retención procede cuando la base es **igual o superior** al mínimo
  vigente. Ningún valor legal MUST estar escrito en el programa.
- **FR-014** (R6) Inventario MUST NOT leer ni escribir datos de Contabilidad ni de Cartera. Se
  comunica con ellos sólo por mensajes de negocio versionados con entrega garantizada y por estas
  consultas definidas:
  - **Cartera**: estado, cupo y líneas de crédito de una persona (FR-060), y estado de validación de
    una venta pendiente (FR-061).
  - **Contabilidad**:
    - «¿es contabilizable?», cuya respuesta dice qué falla y quién puede corregirlo (FR-074);
    - saldos de las cuentas mapeadas a una fecha (FR-081, FR-090);
    - completitud de la matriz (FR-082);
    - vista previa de los comprobantes de un lote (FR-077).

  Una comprobación automática MUST vigilar esa lista.
- **FR-015** (R6) Inventario MUST seguir operando cuando Contabilidad o Cartera no estén disponibles,
  según las políticas de FR-061 y FR-074 y el modo de paso de FR-075. Lo pendiente queda visible,
  nunca oculto.
- **FR-016** (R7) Cada operación iniciada desde una pantalla o una integración MUST llevar una clave
  única. Repetirla (doble clic, reintento o reenvío de la red) devuelve el mismo resultado sin
  duplicar efectos. Cada mensaje MUST tener un identificador único, y el destino lo procesa una sola
  vez.
- **FR-017** (R8) Cantidades, costos y montos MUST calcularse en decimal exacto, nunca en punto
  flotante.
  - Cantidades: hasta 4 decimales, con los decimales permitidos definidos por unidad.
  - Costos unitarios: hasta 6 decimales.
  - Montos en pesos: 2 decimales, redondeados según el parámetro.
  - La suma de las líneas iguala el total exactamente, y la diferencia de redondeo se asigna por regla
    y queda visible.
  - En una conversión de unidades que no da exacta, la diferencia queda en la misma línea del
    documento, en unidad base, como cantidad de redondeo visible. Si la unidad no admite los decimales
    necesarios, la conversión se rechaza.
- **FR-018** (R8) Todo documento y todo mensaje MUST llevar moneda y tasa de cambio. En esta feature
  sólo se opera en pesos (tasa 1).
- **FR-019** (R9) Las operaciones frecuentes MUST completarse en 3 pasos o menos: vender en el POS,
  recibir una compra, despachar o recibir un traslado, capturar un conteo y consultar una existencia.
  Un paso es un cambio de pantalla o una confirmación explícita; las lecturas del lector y las teclas
  de cantidad no cuentan.
- **FR-020** (R9) Todo campo de producto MUST aceptar lector de código de barras en modo teclado.
  - Las pantallas operativas tienen atajos de teclado visibles.
  - La búsqueda de productos es instantánea: por código, código de barras, nombre o referencia,
    mientras se escribe.
  - Todo error dice en español qué pasó y qué hacer.
  - Toda zona que espera datos muestra que está cargando.
- **FR-021** (R10) El kardex MUST medir el costo por promedio ponderado o PEPS; UEPS no se ofrece. El
  marco es la NIC 2 o la Sección 13 de la NIIF para PYMES, según el grupo de la cooperativa (por
  defecto el Grupo 2, como en la 009). El inventario se presenta al menor entre el costo y el valor
  neto realizable. El módulo MUST ofrecer el reporte de indicios de deterioro:
  - productos cuyo costo supera el precio de venta vigente menos el porcentaje parametrizado de gastos
    de venta;
  - productos vencidos o próximos a vencer;
  - productos sin movimiento.

  El deterioro y su reversión los registra Contabilidad.
- **FR-022** (R9, Principio IX) Cada tipo de alerta del módulo MUST tener:
  - destinatarios definidos por permiso, parametrizables;
  - un canal: notificación en la aplicación y, opcionalmente, correo.

  Las alertas quedan en una bandeja con fecha, estado (pendiente o atendida) y quién la atendió. Todo
  requisito que dice «alerta» o «avisa al responsable» usa este mecanismo.

#### A · Catálogo

- **FR-023** El catálogo MUST admitir producto inventariable, servicio, combo, kit ensamblado y
  plantilla con variantes (atributos definidos por el usuario; cada combinación es un producto con
  código, códigos de barras y existencia propios).
- **FR-024** MUST haber categorías jerárquicas de hasta 5 niveles, marcas y varios códigos de barras
  por producto. Un código de barras puede identificar una unidad de empaque. Todo código de barras es
  único en la cooperativa, y un duplicado se rechaza nombrando al producto que lo tiene.
- **FR-025** Unidades de medida:
  - un catálogo con los decimales que admite cada unidad;
  - cada producto con su unidad base y unidades de compra y venta con factor exacto;
  - el kardex siempre en unidad base.

  La unidad base de un producto con movimientos MUST NOT cambiar.
- **FR-026** El control por lote, serie y vencimiento MUST ser opcional por producto:
  - con lote, toda entrada exige lote (y vencimiento si aplica) y toda salida elige lote, con
    sugerencia del que vence primero;
  - con serie, cada unidad tiene una serie única que no puede estar dos veces en existencia;
  - vender un lote vencido se bloquea o se advierte, según el parámetro.
- **FR-027** Cada producto MUST llevar:
  - su tratamiento de impuestos (IVA con su tarifa, exento o excluido; INC; otros del catálogo
    vigente);
  - su concepto de retención en compras;
  - si es inventariable, su grupo contable obligatorio.

  Cambiar el grupo contable de un producto con existencia exige permiso y motivo. Afecta sólo los
  movimientos futuros, y emite «GrupoContableReclasificado» con la cantidad y el valor que pasan de un
  grupo al otro a la fecha efectiva.
- **FR-028** Estados del producto:
  - activo;
  - inactivo: no se ofrece en documentos nuevos;
  - bloqueado: ningún movimiento, salvo el conteo y la recepción de lo que ya estaba en tránsito.

  Un producto con historia MUST NOT borrarse.
- **FR-029** Cada producto MUST admitir varias imágenes, guardadas como adjuntos del ERP.
- **FR-030** La importación y exportación masiva MUST usar una plantilla.
  - Hay revisión previa completa sin guardar, con la vista de lo que se creará o actualizará.
  - Es todo o nada, y los errores salen con fila y columna.
  - Cambiar por importación el grupo contable de un producto con existencia exige permiso y motivo, y
    sigue FR-027.
- **FR-031** El rol de vendedor MUST crearse y retirarse, desde su pantalla en el módulo nuevo y desde
  la plantilla, sólo por la operación que escribe juntas la fila del rol y la marca «Vendedor» de la
  persona, como exige la feature 008. La importación no crea ni modifica personas.

#### B · Bodegas y existencias

- **FR-032** La estructura MUST ser cooperativa → sucursal contable (la de la 009) → bodega →
  ubicación interna.
  - Una bodega pertenece a una sucursal. Su tipo es parametrizable: principal, punto de venta,
    averías, cuarentena, tránsito u otros.
  - Cada sucursal tiene al menos una bodega de tránsito. Desde ella no se vende ni se despacha, y su
    contenido no cuenta como existencia física de ninguna bodega operativa.
  - El alcance de los usuarios es por bodega y punto de venta (FR-009), no por las oficinas que
    tengan asignadas.
- **FR-033** Por producto, bodega, ubicación y lote, el sistema MUST ofrecer la existencia física, la
  reservada, la disponible y la que está en tránsito. La existencia en tránsito de una bodega es la de
  los despachos abiertos hacia ella. Un documento confirmado se refleja en la consulta siguiente.
- **FR-034** El stock negativo MUST ser un parámetro por cooperativa con excepción por bodega. Por
  defecto está prohibido.
- **FR-035** Por producto y bodega MUST poder definirse mínimo, máximo y punto de reorden.
  - **Posición**: disponible + en tránsito + por recibir. «Por recibir» son las órdenes aprobadas no
    recibidas, que valen 0 hasta I5.
  - **Alerta**: salta cuando la posición es igual o menor que el punto de reorden.
  - **Sugerido de compra**: sólo para esos productos, y vale máximo − posición.
  - **Quiebre**: disponible por debajo del mínimo.

#### C · Movimientos y documentos

- **FR-036** Las clases de documento MUST ser fijas del sistema. Cada una fija su efecto sobre el
  inventario, si es fiscal y qué mensajes emite. Los tipos eligen su clase; ése es el «efecto sobre el
  inventario» parametrizable que pide la solicitud, sin que un tipo pueda inventar un efecto que rompa
  los invariantes.

  | Clase | Efecto en inventario | Fiscal | Mensajes (FR-069) |
  |---|---|---|---|
  | Recepción de compra | Entrada | No | CompraRecibida |
  | Factura del proveedor | Ninguno; ajusta costo si el precio difiere | Recibida | FacturaProveedorRegistrada (+ AjusteDeCostoReconocido) |
  | Documento soporte | Ninguno; acompaña la recepción | Emitido (CUDS) | FacturaProveedorRegistrada |
  | Costos adicionales (prorrateo) | Sólo costo | No (el flete trae su propia factura de proveedor) | AjusteDeCostoReconocido |
  | Devolución a proveedor | Salida | No; su nota la envía el proveedor, o se emite la nota de ajuste al documento soporte | DevoluciónRegistrada (+ AjusteDeCostoReconocido por la diferencia con el promedio) |
  | Nota del proveedor, o nota de ajuste al documento soporte | Ninguno; ajusta costo si cambia el precio | Recibida / emitida (CUDS) | FacturaProveedorRegistrada con su signo (+ AjusteDeCostoReconocido) |
  | Ajuste positivo / ajuste negativo | Entrada / salida | No | AjusteInventarioAprobado |
  | Consumo interno | Salida | No; si es retiro gravado, lleva base e IVA | AjusteInventarioAprobado |
  | Baja (daño, vencimiento, hurto, destrucción) | Salida | No | AjusteInventarioAprobado |
  | Ensamble | Salida de componentes y entrada del kit | No | AjusteInventarioAprobado |
  | Saldo inicial | Entrada | No | SaldoInicialCargado (informativo) |
  | Despacho de traslado | Salida del origen y entrada a la bodega de tránsito | No | TrasladoDespachado |
  | Recepción de traslado | Salida de tránsito y entrada al destino | No | TrasladoRecibido |
  | Movimiento entre ubicaciones | Dentro de una bodega | No | Ninguno |
  | Movimiento de caja (retiro parcial o ingreso de base) | Ninguno | No | MovimientoDeCajaRegistrado |
  | Arqueo con diferencia aprobada | Ninguno | No | DiferenciaDeArqueoAprobada |
  | Conteo | Ninguno; su ajuste es un ajuste | No | Ninguno |
  | Ajuste de costo (retroactivo, diferencia de precio, negativo regularizado) | Sólo costo | No | AjusteDeCostoReconocido |
  | Solicitud de compra, orden de compra, cotización | Ninguno | No | Ninguno |
  | Pedido | Reserva | No | Ninguno |
  | Remisión | Salida | No | CostoDeVentaReconocido |
  | Factura de venta directa o desde pedido | Salida (libera la reserva del pedido) | Sí (CUFE) | VentaFacturada + CostoDeVentaReconocido |
  | Factura desde remisiones | Ninguno | Sí (CUFE) | VentaFacturada |
  | Documento equivalente POS | Salida | Sí (CUDE) | VentaFacturada + CostoDeVentaReconocido |
  | Comprobante de venta no electrónico (sólo en una cooperativa no obligada a facturar electrónicamente, FR-063) | Salida | No | VentaFacturada + CostoDeVentaReconocido |
  | Nota sobre un comprobante no electrónico, con o sin devolución (misma restricción) | Entrada al costo con que salió, o ninguno | No | NotaCréditoEmitida (+ DevoluciónRegistrada) |
  | Nota crédito o nota de ajuste POS sin devolución | Ninguno | Sí (CUDE) | NotaCréditoEmitida |
  | Nota crédito o nota de ajuste POS con devolución | Entrada, al costo con que salió | Sí (CUDE) | NotaCréditoEmitida + DevoluciónRegistrada |
  | Nota débito | Ninguno | Sí (CUDE) | NotaDébitoEmitida |

  Toda venta, nota o anulación con parte a crédito emite además su mensaje a Cartera (FR-062).

  Toda anulación hecha con un documento contrario (FR-006, y FR-066 casos b y c) emite
  «DocumentoAnulado». Si la entrada anulada ya había entrado al promedio, emite además
  «AjusteDeCostoReconocido» por la diferencia de costo. La anulación del saldo inicial es informativa,
  como el saldo inicial (FR-089).

  Cuando una clase emite dos mensajes a Contabilidad, su contenido no se superpone:
  - «VentaFacturada», «NotaCréditoEmitida», «NotaDébitoEmitida» y «FacturaProveedorRegistrada»
    llevan base, descuentos, impuestos, retenciones y pagos por medio, sin costo;
  - «CostoDeVentaReconocido» y «DevoluciónRegistrada» llevan sólo cantidades y costo;
  - «AjusteDeCostoReconocido» lleva sólo la diferencia de costo.

  «DocumentoAnulado» lleva, con signo contrario, el contenido de los mensajes del documento que anula:
  base, descuentos, impuestos, retenciones y pagos por medio; cantidades y costo; o ambos. Los demás
  mensajes a Contabilidad llevan cantidades y costo, y el del retiro gravado lleva además base e IVA
  (FR-037).
- **FR-037** Dentro de cada clase, los **tipos** de documento MUST ser parametrizables:
  - consecutivo y prefijo (FR-038);
  - política de aprobación;
  - modo de paso a contabilidad (FR-075);
  - campos obligatorios: tercero, centro de costo, motivo, referencia externa;
  - bodegas permitidas y canal.

  Los mensajes los fija la clase, no el tipo. Además:
  - el consumo interno exige centro de costo, y su tipo indica si es **retiro gravado**: la base es el
    precio de la lista general vigente y el IVA el del producto, y su mensaje lleva costo, base e IVA;
  - las bajas y los ajustes negativos llevan una causa de un catálogo (merma o faltante, daño,
    vencimiento, hurto, diferencia de conteo, destrucción) y admiten adjuntos de soporte, como el acta
    de destrucción o la denuncia.
- **FR-038** Los consecutivos MUST ser por tipo de documento y prefijo, compartidos por todas las
  cajas que usan ese tipo.
  - No tienen huecos ni repetidos.
  - Se asignan al confirmar: el borrador no consume número.
  - Son seguros ante concurrencia.

  Los tipos fiscales numeran dentro de su resolución (FR-065), y las notas con su propio consecutivo.
  La unicidad del número fiscal rige entre documentos fiscales vigentes: un documento rechazado por la
  DIAN y su reemplazo comparten número (FR-066). Un número rechazado sin reemplazo queda ligado a su
  documento anulado y no cuenta como hueco.
- **FR-039** Los traslados entre bodegas MUST hacerse en dos pasos.
  - **Despacho**: saca la mercancía del origen y la deja en la bodega de tránsito de la sucursal de
    origen, al costo de origen.
  - **Recepción**: saca de tránsito, como máximo, lo despachado, y lo entra al destino. Sigue el
    destino del mensaje de su despacho (FR-075).
  - **Faltante**: lo que no llega queda en tránsito como faltante pendiente. Se resuelve con
    aprobación de una de tres formas:
    - devolución al origen;
    - baja desde tránsito con su causa (daño, hurto o reclamación al transportador);
    - recepción tardía en destino, que lo saca de tránsito.
  - **Sobrante**: lo recibido de más queda registrado en la recepción como sobrante pendiente, fuera
    de la existencia. Al aprobarse, un ajuste positivo lo entra al destino con su causa, al costo
    vigente, y emite «AjusteInventarioAprobado».
  - **Anulación**: anular un despacho no recibido devuelve la mercancía al origen.
  - **Dentro de una bodega**: el movimiento entre ubicaciones es de un paso y no afecta costo.
- **FR-040** Los conteos físicos MUST ser totales o cíclicos: por categoría, ubicación o selección, y
  por clase ABC desde I6.
  - Al abrir, se congela la foto de la existencia teórica.
  - La captura admite lector (cada lectura suma), varios contadores y conteo ciego.
  - Una diferencia por encima de la tolerancia exige reconteo.
  - Mientras el conteo esté abierto, sus productos no admiten movimientos (por defecto) o los admiten
    y se suman al teórico, según el parámetro.
- **FR-041** El ajuste de un conteo MUST generarse sólo después de aprobado (FR-010), como documento
  de ajuste enlazado al conteo.
  - **Fecha**: la de la foto, o la de la aprobación según el parámetro. Si ese período ya se cerró, el
    primer día abierto.
  - **Valor**: el costo promedio vigente en esa fecha.

#### D · Costeo

- **FR-042** El costo MUST ser promedio ponderado por defecto. Su ámbito es por producto en toda la
  cooperativa (por defecto) o por producto y bodega. Los traslados viajan al costo de origen.
- **FR-043** PEPS MUST poder elegirse como método por capas. Cambiar de método:
  - sólo se admite al inicio de un período abierto sin movimientos posteriores, con permiso especial;
  - registra la justificación (por qué da información más fiable y relevante) y, si la cooperativa lo
    exige, la referencia al acta que lo aprueba;
  - produce, por grupo contable, el valorizado por los dos métodos a la fecha del cambio y al inicio
    del período comparativo más antiguo que la cooperativa presente, o deja constancia de por qué no
    puede calcularse.

  Ese reporte se entrega a Contabilidad para la aplicación retroactiva o la revelación que exigen la
  NIC 8 o la Sección 10 de la NIIF para PYMES.
- **FR-044** Reglas de costo de las entradas y devoluciones:
  - **compras**: entran al costo neto de descuentos y de impuestos descontables, más los costos
    adicionales prorrateados. El carácter descontable del IVA se decide en este orden:
    1. un parámetro general con vigencia dice si la cooperativa es responsable de IVA; si no lo es,
       todo IVA va al costo;
    2. el tipo de compra marca como no descontables las destinadas a consumo interno o a actividades
       excluidas;
    3. el tratamiento del producto, cuando su venta es excluida.

    El INC pagado en compras siempre va al costo. El prorrateo del IVA en costos comunes lo hace
    Contabilidad, no el kardex;
  - **devoluciones de cliente**: entran al costo con que salieron;
  - **devoluciones a proveedor**: salen al costo con que entraron, y la diferencia con el promedio es
    ajuste de costo;
  - **ajustes positivos**: entran al costo vigente, o al indicado si se tiene permiso;
  - **saldo inicial**: entra al costo cargado.
- **FR-045** Los documentos retroactivos MUST admitirse sólo si el parámetro lo permite, dentro de
  un período abierto y hasta los días parametrizados. Antes de confirmar, el sistema muestra el
  impacto. Al confirmar:
  - calcula la diferencia de costo de los movimientos posteriores y la registra como líneas de
    ajuste, sin reescribir nada;
  - emite «AjusteDeCostoReconocido», dividido por documento afectado (FR-075).

  Dos casos no dependen del parámetro, porque los genera la puesta en marcha o un control, no una
  persona que fecha hacia atrás: el saldo inicial de una bodega todavía no activa (y su anulación), y
  los ajustes que salen de un conteo aprobado. Ambos entran en el orden de su fecha, con los ajustes de
  costo que produzcan, desde I1.
- **FR-046** Los costos adicionales de una o varias compras MUST poder prorratearse por valor,
  cantidad, peso, volumen o a mano. La porción de lo ya vendido va a costo de venta, y la de lo
  existente, al inventario, con «AjusteDeCostoReconocido» dividido por documento afectado (FR-075).
- **FR-047** El cierre mensual de período MUST hacerse para toda la cooperativa. El cierre:
  - **se bloquea** si hay conteos abiertos con foto en el período;
  - **avisa**, sin bloquear, de borradores, tránsitos sin resolver y mensajes pendientes, en lote o
    rechazados con fecha en el período;
  - **lista las remisiones sin facturar** del período, con el valor por facturar de cada una (al
    precio de su pedido o de la lista vigente), y sólo se completa si alguien con permiso las acepta
    con motivo;
  - bloquea los documentos con fecha en el período y fija el valorizado por grupo contable y bodega;
  - emite «PeriodoInventarioCerrado».

  Reabrir exige un permiso especial y motivo, sólo alcanza al último período cerrado, queda auditado
  y emite «PeriodoInventarioReabierto».

#### E · Compras

- **FR-048** El módulo MUST ofrecer (I5):
  - solicitud de compra (quién pide, qué, para cuándo, qué bodega), con aprobación;
  - orden de compra (proveedor, precios, condiciones y entrega), con aprobación por montos y envío
    al proveedor en PDF o por correo.
- **FR-049** La recepción MUST poder ser una compra directa (I1) o una recepción contra una o varias
  órdenes, total o parcial (I5). Lo recibido de más sólo se acepta dentro de la tolerancia. La
  recepción entra al kardex y emite «CompraRecibida».
- **FR-050** La factura del proveedor MUST registrarse contra las recepciones, con su prefijo, número
  y CUFE.
  - **Cruce a tres vías** (I5): por línea, lo ordenado, lo recibido y lo facturado, en cantidad y en
    precio. Las tolerancias van en porcentaje y en valor, y lo que las excede queda retenido para
    aprobación. Una diferencia de precio aprobada ajusta el costo de lo existente y el costo de venta
    de lo vendido.
  - **Impuestos y retenciones**: se calculan por el catálogo vigente (FR-013), según el concepto de
    cada producto, el régimen del proveedor y del comprador, y las bases mínimas. La ReteICA usa la
    tarifa del municipio donde se realiza la operación; por defecto, el de la sucursal de la bodega
    que recibe.
  - Emite «FacturaProveedorRegistrada».
  - Las notas crédito y débito que envía el proveedor se registran contra su factura y emiten
    «FacturaProveedorRegistrada» con su signo. Si cambian el precio de lo recibido, emiten además
    «AjusteDeCostoReconocido».
  - Opcionalmente se prellena leyendo el archivo de la factura electrónica del proveedor, que no se
    guarda.
  - **Eventos de una factura a crédito**: se muestra el estado del acuse de recibo y del recibo del
    bien, y se alerta cuando faltan, porque sin ellos la factura no soporta costo ni IVA descontable.
    El ERP los emite por el canal de FR-064 desde I5. Hasta entonces, la cooperativa los emite en el
    portal de su proveedor o de la DIAN, y el ERP registra que lo hizo.
- **FR-051** La devolución a proveedor MUST salir al costo de entrada, enlazada a la recepción o la
  factura, y emitir «DevoluciónRegistrada». Si la compra se soportó con documento soporte, emite su
  nota de ajuste.

#### F · Ventas y facturación

- **FR-052** Los documentos de venta MUST ser:
  - **cotización**: sin efecto, con vigencia;
  - **pedido**: reserva existencias hasta su vencimiento parametrizado, y después la libera sola;
  - **remisión**: descarga existencias y reconoce el costo; si pasa los días máximos sin facturar,
    sale una alerta;
  - **factura**: directa, o desde pedido o remisiones, sin volver a descargar lo remisionado;
  - **nota crédito**: con o sin devolución de mercancía, total o parcial;
  - **nota débito**.
- **FR-053** Las listas de precios MUST tener vigencia y un ámbito, que es una combinación de
  dimensiones opcionales: cliente, segmento del asociado, canal y sucursal.
  - Gana la lista vigente más específica, la que coincide en más dimensiones.
  - En empate, el orden es cliente, luego segmento, luego canal, luego sucursal. Si ninguna aplica, la
    general.
  - Cada lista indica si el precio incluye impuestos.
  - Dos listas con el mismo ámbito exacto no pueden tener vigencias que se crucen.
  - El documento muestra qué lista se aplicó.
- **FR-054** Los descuentos por línea y por total MUST tener tope por rol. Por encima del tope exigen
  la aprobación de un usuario con tope suficiente, con su propia identidad, y registrada.
- **FR-055** Las promociones MUST tener vigencia y ámbito (producto, categoría, segmento o canal).
  - Tipos: porcentaje, valor, «lleve N pague M», precio por cantidad o precio de paquete.
  - Se aplican como descuentos no condicionados en el documento, que reducen la base gravable, y no
    como líneas a precio cero.
  - Por defecto no son acumulables, y el documento muestra cuál se aplicó.
- **FR-056** Formas de pago: todo documento de venta MUST cobrarse con uno o varios **medios de pago**
  del catálogo configurable de la sección K (FR-096 y FR-097). Los créditos a asociado y a cliente son
  medios de pago de clase crédito; mientras la entrega IC está pendiente, funcionan como crédito
  provisional (F2). La suma de los pagos MUST igualar exactamente el **total a pagar**: el total menos
  las retenciones que practica el comprador cuando es agente retenedor. El sistema calcula las vueltas
  en los medios que las admiten.
- **FR-057** Cada venta MUST poder llevar vendedor, que es una persona con rol de vendedor. Vender
  bajo costo se alerta o se bloquea según el parámetro.
- **FR-058** El POS MUST ser opcional por punto de venta. El punto de venta fija el canal. Cada caja
  tiene su bodega y sus tipos de documento, cada uno con su prefijo y, cuando aplica, su resolución:
  - el de venta POS;
  - el de factura, para cuando el comprador la pide;
  - sus notas;
  - el de contingencia.

  Además:
  - **Sesión de caja**: apertura con base, ventas, movimientos de caja (FR-100) y cierre con arqueo
    por medio de pago (FR-099).
  - **Diferencias**: exigen motivo y, por encima de la tolerancia, aprobación.
  - **Límites**: una sesión por caja a la vez, y sin sesión abierta no se vende.
  - **Cierre**: hay informe de cierre de turno y de cierre del día por punto (FR-099).
- **FR-059** El POS MUST admitir:
  - lector (cada lectura suma) y multiplicador de cantidad;
  - búsqueda;
  - consumidor final por defecto o cliente identificado, dado de alta en el diálogo único de personas
    con su autorización (FR-011);
  - suspender y recuperar una venta;
  - quitar una línea antes de cobrar (auditado);
  - reimprimir (auditado), con la copia de lo emitido.

  Todo esto se hace sin ratón.

#### F2 · Crédito con Cartera (entrega IC pendiente; crédito provisional mientras tanto)

La comunicación con Cartera queda **pendiente** hasta que exista la especificación de Cartera (D-02),
por decisión del dueño. Esa comunicación es la consulta de FR-060 y la entrega de los mensajes de
FR-062. Mientras tanto rige el **crédito provisional**, también decidido por el dueño (Clarifications):
- la venta a crédito se trata como si Cartera no respondiera: se aplica la política «permitir con
  aprobación» de FR-061, sin consultar cupo, y la venta queda «pendiente de validar»;
- la cuenta por cobrar la registra Contabilidad desde «VentaFacturada», por la cuenta que indique la
  matriz (primera opción de FR-062);
- los mensajes para Cartera se guardan como pendientes, con entrega garantizada. Cuando Cartera exista,
  los recibe en orden, crea las obligaciones y valida el cupo a la fecha de cada venta (FR-061).

Lo que sigue queda especificado para cuando exista la entrega IC.

- **FR-060** Antes de confirmar una venta a crédito, la parte a crédito de un pago mixto o una nota
  débito sobre una venta a crédito, el sistema MUST consultar a Cartera el estado de la persona, su
  cupo disponible y las líneas de crédito aplicables (plazo y cuotas permitidos). Sólo confirma si el
  estado lo permite y el cupo alcanza. La respuesta queda en la auditoría como evidencia; Inventario
  no la guarda como dato de operación. Mientras IC está pendiente, esta consulta no existe y rige el
  crédito provisional.
- **FR-061** Si Cartera no responde en el tiempo parametrizado, se aplica la política de la
  cooperativa, que puede variar por tipo de tercero (asociado o cliente):
  - **bloquear**, que es el valor por defecto;
  - **permitir con aprobación** de un usuario autorizado para ese monto, marcando la venta «pendiente
    de validar».

  Una venta pendiente de validar sigue este camino:
  - al recibirla, Cartera crea la obligación y evalúa estado y cupo **a la fecha de la venta**, sin
    contar esa misma venta;
  - Inventario conoce el resultado por la consulta de estado de validación (FR-014);
  - si el resultado es negativo, la venta sigue confirmada, queda «validación fallida» visible en la
    bandeja y se genera una alerta (FR-022). Nada se anula solo.
- **FR-062** Al confirmar, MUST emitirse «VentaACreditoRegistrada» con persona, tipo (asociado o
  cliente), valor, plazo, cuotas, línea de crédito, fechas, marca de pendiente de validar y documento.
  - Las notas crédito, las notas débito, las devoluciones y las anulaciones de una venta a crédito
    emiten sus mensajes enlazados al original.
  - Inventario MUST NOT guardar saldos, cuotas ni obligaciones de Cartera.
  - La cuenta por cobrar de una venta a crédito la registra **un solo lado**. O la registra
    Contabilidad desde «VentaFacturada», y Cartera no genera comprobante al crear la obligación. O la
    registra Cartera al crear la obligación, y «VentaFacturada» lleva la parte a crédito contra la
    cuenta puente que indique la matriz. Durante el crédito provisional aplica la primera opción. Si
    D-02 elige después la segunda, la especificación de Cartera define cómo pasan a Cartera las
    cuentas por cobrar provisionales.

#### F3 · Documentos electrónicos DIAN

- **FR-063** El módulo MUST generar, numerar, firmar, transmitir y conservar, según la norma DIAN
  vigente, estos documentos. La norma es la Resolución Única 000227 de 2025, Título 5, que compila la
  000165 de 2023 y la 000167 de 2021, con sus anexos técnicos vigentes.
  - factura electrónica de venta;
  - notas crédito y débito (la débito, con I6);
  - documento equivalente electrónico POS (o factura, si el comprador la pide) y su nota de ajuste. El
    documento equivalente no tiene tope de valor. Si el comprador pide factura después de emitido el
    documento equivalente, se emite la nota de ajuste que lo anula y después la factura;
  - documento soporte en adquisiciones a no obligados a facturar y su nota de ajuste.

  Cada documento lleva:
  - su código único: CUFE, CUDE o CUDS según el tipo;
  - QR y representación gráfica;
  - envío al comprador. En operación normal, la representación gráfica se entrega **después** de la
    validación de la DIAN;
  - seguimiento de estado: pendiente, enviado, validado, validado con notificaciones, rechazado con
    motivos, o en contingencia;
  - reintentos automáticos con espera creciente, sin duplicar.

  Esto aplica a toda cooperativa con el parámetro «obligada a facturar electrónicamente» encendido
  (FR-012), como COOFLOPAL. Si el parámetro está apagado, sus ventas usan el comprobante de venta no
  electrónico y su nota (FR-036). El parámetro sólo gobierna los documentos de venta: el documento
  soporte sigue su propia norma.
- **FR-064** **Modo de emisión** (decisión del dueño). Hay dos modos detrás del mismo adaptador, y
  cada cooperativa MUST poder elegir el suyo por configuración, con vigencia:
  - **proveedor tecnológico externo**, autorizado por la DIAN e intercambiable por otro, por API
    (entrega I4);
  - **software propio** de la cooperativa, por el servicio central sin estado de la feature 010,
    ampliado a los documentos de FR-063, cuando ese servicio exista.

  Reglas del cambio:
  - Cada documento registra el modo y el proveedor o software con que se numeró. Sus reenvíos, las
    correcciones de FR-066 (a) y (b), que conservan su número, y sus transmisiones desde contingencia
    salen por ese mismo canal, aunque la configuración cambie. Si ese canal se retira, quien tenga
    el permiso especial puede transmitirlos por el canal vigente, cuando la resolución con que se
    numeraron esté asociada a ese software; queda auditado.
  - Una nota crédito, una nota débito o una nota de ajuste es un documento nuevo: sale por el modo
    vigente al confirmarla, aunque se refiera a un documento numerado por otro canal.
  - El modo nuevo aplica a los documentos numerados desde su vigencia, y sólo con resoluciones cuyo
    prefijo esté asociado a ese software.
  - Cambiar de modo o de proveedor MUST NOT cambiar documentos ni pantallas. La habilitación del
    software nuevo y la asociación de prefijos ante la DIAN son trámites de la cooperativa.
  - Las credenciales de cada cooperativa ante el proveedor se guardan fuera de su base y del
    repositorio, como los secretos DIAN de la 010.
- **FR-065** Las resoluciones de numeración MUST registrarse para los tipos que la norma exige:
  factura, documento equivalente POS, documento soporte y numeración de contingencia.
  - **Datos de cada resolución**: prefijo, rango, vigencia, ambiente, tipo de documento, y software o
    modo asociado.
  - **Clave técnica**: sólo en las de factura, con vigencia y ligada a la resolución y al software con
    que se obtuvo.
  - **Avisos**: al consumir el porcentaje o al faltar los días parametrizados (por defecto 90 % y 30
    días).
  - Nunca se numera fuera de una resolución vigente.

  Las notas llevan su propio consecutivo, sin resolución, y pueden referirse a una factura cuya
  resolución ya venció o se agotó.
- **FR-066** Un documento fiscal que emite la cooperativa, ya validado por la DIAN, MUST NOT anularse
  con un documento contrario ni emitir «DocumentoAnulado». «Anular» emite el documento de corrección
  que corresponde a su tipo:
  - **factura**: nota crédito total;
  - **documento equivalente POS**: nota de ajuste;
  - **documento soporte**: nota de ajuste.

  El documento de corrección lleva su propio número, código único y fecha. Lo que emite depende del
  caso:
  - **En ventas** emite «NotaCréditoEmitida», marcada como anulación total cuando lo es, y devuelve a
    la existencia lo que el documento descargó, al costo con que salió («DevoluciónRegistrada»).
  - **La nota de una factura desde remisiones** no mueve existencia y deja sus remisiones sin facturar.
  - **La nota de servicios** sólo emite «NotaCréditoEmitida».
  - **En compras** emite los mensajes de su clase (FR-036).

  Un comprobante de venta no electrónico (FR-036) se anula con un documento contrario (FR-006).

  **Un documento enviado y sin respuesta de la DIAN**, fuera de contingencia, no se corrige hasta
  conocerla. Si lo valida, «anular» emite su documento de corrección; si lo rechaza, rigen los casos de
  abajo.

  **Un documento expedido en contingencia** ya se entregó. Se corrige con su documento de corrección,
  que se transmite después del original y referido a él. Si al transmitirlo la DIAN lo rechaza, se
  corrige como un rechazado (casos a a c). Su documento de corrección en cola se transmite referido al
  documento que quede validado con ese número.

  **Un documento rechazado por la DIAN** no está expedido y su número no se consume. Los casos (b) y
  (c) sólo se aplican a un rechazo confirmado por la consulta de estado a la DIAN:
  - **(a)** Si la corrección no cambia tercero, cantidades, bases, impuestos ni total (formato,
    códigos, datos de contacto corregidos en el maestro), se genera una versión nueva del archivo
    firmado y se reenvía con el mismo número, sin mensajes nuevos. Si la corrección toca la copia de
    la identificación fiscal de la contraparte (FR-011), esa copia se actualiza. Todo queda con antes y
    después auditados; es la excepción de FR-005.
  - **(b)** Si los cambia, el documento del ERP se anula con un documento contrario sin efecto fiscal,
    que emite sus mensajes de anulación a existencia, Contabilidad y Cartera. Un documento de
    reemplazo, enlazado, se confirma con el mismo número fiscal y emite los suyos.
  - **(c)** Si la venta se cancela, el documento del ERP se anula como en (b), pero sin reemplazo. El
    número queda registrado como rechazado sin reemplazo, con motivo y responsable.
- **FR-067** Las contingencias MUST tratarse según quién falla:
  - **Contingencia del facturador** (falla la conexión, el proveedor o el software de la cooperativa):
    se emite el documento con la numeración de contingencia (FR-065). Al restablecerse, se transmite
    con la referencia a su número de contingencia, como exige el anexo técnico.
  - **Contingencia de la DIAN**: se entrega la factura electrónica sin la validación de la DIAN,
    marcada como tal, y se transmite cuando la DIAN vuelve.

  En los dos casos la operación se confirma y queda en cola con aviso visible. El plazo de transmisión
  es un parámetro con norma y vigencia, y la alerta sale las horas parametrizadas antes de que venza.
- **FR-068** El módulo MUST guardar como adjuntos que no se borran ni reciben subidas:
  - el documento firmado, con cada una de sus versiones;
  - las respuestas de la DIAN;
  - la representación gráfica.

  Se leen con el permiso de consulta de ventas o de compras, durante el plazo legal de conservación.

#### G · Integración con Contabilidad

- **FR-069** Inventario MUST NOT registrar comprobantes contables ni conocer cuentas. Emite estos
  mensajes, según la clase de cada documento (FR-036). Los tipos de mensaje se guardan sin tildes
  (`DevolucionRegistrada`, `NotaCreditoEmitida`, `NotaDebitoEmitida`); la pantalla los muestra con
  tilde:
  - **Los diez que pide la solicitud**: «VentaFacturada», «CostoDeVentaReconocido»,
    «CompraRecibida», «FacturaProveedorRegistrada», «AjusteInventarioAprobado»,
    «TrasladoDespachado», «TrasladoRecibido», «DevoluciónRegistrada», «DocumentoAnulado» y
    «PeriodoInventarioCerrado».
  - **Los que la contabilidad necesita para cuadrar**: «AjusteDeCostoReconocido», «NotaCréditoEmitida»,
    «NotaDébitoEmitida», «GrupoContableReclasificado», «MovimientoDeCajaRegistrado» y
    «DiferenciaDeArqueoAprobada».
  - **Informativos**: «SaldoInicialCargado» y su anulación, «PeriodoInventarioCerrado» y
    «PeriodoInventarioReabierto». Contabilidad los registra sin generar comprobante, se entregan
    siempre y no dependen del modo de paso.
  - **A Cartera**: «VentaACreditoRegistrada» y los ajustes de FR-062, que se entregan siempre.

  «GrupoContableReclasificado» no pertenece a un documento y sigue el valor general del modo de paso.
- **FR-070** Cada mensaje MUST incluir:
  - identificador único, tipo y versión;
  - sucursal contable, grupo contable y tipo de operación, y persona tercero, centro de costo y bodega
    cuando aplican;
  - según el tipo de mensaje (FR-036): base, descuentos, impuestos y retenciones desglosados por
    impuesto y tarifa, y pagos por medio (FR-098); o cantidades y costo; o ambos, en el retiro
    gravado (FR-037)
    y en «DocumentoAnulado» (FR-036);
  - cuando el mensaje nace de un documento: documento origen (clase, tipo, número, identificador, fecha
    de operación y, si es fiscal, su código único) y documento relacionado (anulado, devuelto o
    corregido). Si no, la operación que lo origina: cierre, reapertura o reclasificación;
  - usuario que originó, moneda y tasa.
- **FR-071** Entrega garantizada:
  - el mensaje MUST nacer junto con el documento, de modo que no hay documento confirmado sin sus
    mensajes ni mensaje sin documento. Los de cierre, reapertura y reclasificación nacen junto con esa
    operación;
  - se entrega hasta que el destino lo procese o lo rechace;
  - los mensajes de un mismo documento se procesan en el orden en que ocurrieron, y la anulación, las
    notas y los ajustes siempre después de su original. Entre documentos distintos no se exige orden,
    porque cada comprobante lleva la fecha de operación de su documento;
  - un mensaje que falla sólo detiene los posteriores de su mismo documento y los de sus relacionados
    (anulación, notas, ajustes y documentos derivados; FR-075 y FR-079). Un mensaje «no aplica», en
    lote o rechazado no detiene los de documentos no relacionados.

  Un mensaje emitido no se modifica nunca.
- **FR-072** Cada tipo de mensaje MUST tener versión. Un cambio incompatible crea una versión nueva,
  y el destino declara qué versiones acepta.
- **FR-073** La matriz de reglas contables MUST ser parametrizable sin programar y con vigencia. Asigna,
  por tipo de operación + grupo contable + bodega (y opcionalmente sucursal o centro de costo), las
  cuentas de:
  - inventario, costo, ingreso y devoluciones;
  - cada impuesto y tarifa, y cada retención;
  - cada medio de pago (FR-098), y opcionalmente por punto de venta;
  - diferencias de arqueo y movimientos de caja (FR-099, FR-100);
  - tránsito, mercancía por facturar y las cuentas puente.

  Vive del lado de Contabilidad, la administra quien tiene permiso contable y se construye en esta
  feature (I2) con sus pantallas y su plantilla. Guardar una regla valida que sus cuentas sean de
  movimiento, estén activas y habilitadas para Inventario, como exige la 009 para toda
  parametrización. Reemplaza a las cuentas de producto e IVA del módulo actual, que guardaban códigos
  de cuenta como texto sin validar.
- **FR-074** Inventario MUST integrarse con Contabilidad por mensajes asíncronos. Es decisión del dueño,
  y enmienda para Inventario la 009 (D-01).
  - **Validación previa**: antes de confirmar un documento cuyo modo pasa a contabilidad, Inventario
    MUST preguntar si es contabilizable. Contabilidad arma las líneas que la matriz produciría y las
    somete a las mismas reglas de cuenta de la 009 (cuenta elegible, tercero, documento cruce, centro
    de costo, sucursal, base y tarifa de impuesto) y a la del período abierto. Si algo falla, no se
    confirma, y la respuesta dice la línea, la cuenta, la regla y quién puede corregirla.
  - **Sin respuesta**: un parámetro de la cooperativa decide entre confirmar con el mensaje pendiente
    (por defecto, por R6) o bloquear.
  - **Rechazo después de confirmar**: el documento sigue confirmado, y el mensaje queda pendiente o
    rechazado, visible en la bandeja y en la conciliación, hasta que alguien corrija la causa y lo
    reprocese.
- **FR-075** El **modo de paso a contabilidad** MUST ser: en línea, por lotes o no pasa.
  - **Valor**: la cooperativa tiene un valor general, que cada tipo de documento puede cambiar. Es un
    parámetro con vigencia (FR-012), y por defecto es «en línea».
  - **Sello**: el modo se sella en el documento al confirmarlo, y un cambio posterior no lo altera,
    aunque se registre con vigencia anterior.
  - **Tipos encadenados**: comparten modo. Son tres cadenas, y cambiar un tipo sin los demás de su
    cadena se rechaza:
    - la recepción, la factura del proveedor y sus notas, el documento soporte y su nota de ajuste, y
      la devolución a proveedor;
    - la remisión, la factura y las notas;
    - el despacho y la recepción de traslado.

    La excepción por tipo se declara por cadena: todos los tipos de una cadena heredan el valor
    general, o todos lo cambian. Cambiar el valor general cuando una cadena lo hereda cuenta como
    cambiar sus tipos, y si alguno es fiscal exige la confirmación de abajo.
  - **Documentos derivados**: un documento que nace de otro de su cadena no sella modo propio: sigue el
    destino del mensaje de su documento de origen (FR-079). Son:
    - la factura desde remisiones;
    - la factura del proveedor, el documento soporte o la devolución contra una recepción;
    - la recepción de traslado.

    Un documento derivado no puede reunir orígenes con destinos distintos: ni una factura, remisiones;
    ni una factura del proveedor, un documento soporte o una devolución, recepciones. La factura desde
    pedido sella su propio modo, como la directa, porque el pedido no emite mensajes.
  - **Ajustes de costo**: «AjusteDeCostoReconocido» se divide por documento afectado, y cada parte
    sigue el destino del mensaje de ese documento (FR-079). Vale para los ajustes que vienen de:
    - un retroactivo;
    - un prorrateo;
    - una diferencia de precio;
    - un negativo regularizado;
    - la anulación o devolución de una entrada que ya había entrado al promedio (FR-006 y FR-044).
  - **Alcance**: el modo sólo gobierna el paso a Contabilidad. Los mensajes a Cartera y los
    informativos se entregan siempre.
  - **Tipos fiscales**: dejar sin paso un tipo fiscal (factura, notas, documento equivalente POS,
    documento soporte o factura del proveedor) exige el permiso especial y una confirmación explícita
    que nombra esos tipos. Rige también al cambiar el valor general cuando un tipo fiscal lo hereda.
    Queda auditado.
- **FR-076** Un documento **en línea** MUST contabilizarse apenas se confirma, sin que nadie lo ordene.
- **FR-077** En modo **por lotes**, los mensajes MUST acumularse hasta que se procese su lote.
  - **Cuándo**: a la hora programada (diaria, al cierre de cada turno o al cierre del período de
    inventario, según el parámetro) o cuando lo ordene alguien con permiso. Antes de procesarlo, esa
    persona puede ver qué documentos contiene y qué comprobantes va a generar.
  - **Qué registra**: cada lote tiene número, rango de fechas, documentos, totales, actor (FR-083) y
    resultado.
  - **Granularidad**, parámetro del tipo: un comprobante por documento, o uno **resumido** por fecha de
    operación, tipo de documento y sucursal (y centro de costo, si aplica). Resumir nunca junta líneas
    cuya cuenta exige tercero, documento cruce o base gravable: ésas conservan su detalle. El origen
    de un comprobante resumido es el lote, que lista sus documentos.
  - **Fecha**: todo comprobante lleva la fecha de operación de sus documentos, nunca la del lote.
  - **Fallas**: un mensaje que falla no detiene el resto del lote, salvo los posteriores de su mismo
    documento y de sus relacionados (FR-079). Queda en la bandeja con su motivo.
  - **Repetición**: procesar otra vez un lote o un mensaje no duplica nada (R7).
- **FR-078** En modo **no pasa**, los mensajes para Contabilidad MUST registrarse con estado «no
  aplica», sin entregarse, para conservar la trazabilidad y la conciliación.

  Si después el tipo pasa a «en línea» o «por lotes», quien tenga el permiso especial puede enviar a
  Contabilidad, con motivo, los mensajes «no aplica» de un rango de fechas:
  - el envío incluye, en orden y en la misma operación, todos los mensajes relacionados de esos
    documentos, sean de la fecha que sean: anulaciones, notas, ajustes y sus documentos derivados de
    FR-075, con los relacionados de éstos;
  - se procesan con su fecha de operación original y con las mismas reglas;
  - si alguno cae en un período contable cerrado, el envío de ese documento se rechaza completo a la
    bandeja.
- **FR-079** La anulación, las notas y los ajustes de un documento MUST seguir el destino del mensaje
  de su original:
  - si el original pasa a contabilidad, en cualquier estado (pendiente, en lote, procesado o
    rechazado), ellos también pasan, detrás de él y en orden;
  - si el original quedó «no aplica», ellos también, y FR-078 los envía siempre juntos;
  - si el original está rechazado, sus relacionados esperan con él y se reprocesan juntos;
  - si el mensaje del original es informativo (el saldo inicial), los de sus relacionados también lo
    son.

  Lo mismo vale para los documentos derivados del original (FR-075).

  Contabilidad registra cada uno como un **comprobante nuevo** de su propio mensaje, con su propia
  fecha, enlazado al documento original y a su comprobante, por documento o resumido. El comprobante
  original nunca se marca reversado ni se toca. Contabilidad nunca corrige por su cuenta un comprobante
  nacido de Inventario (FR-038 de la 009).
- **FR-080** La bandeja de mensajes MUST permitir:
  - consultar por estado (pendiente, en lote, procesado, rechazado con motivo, no aplica o validación
    fallida), tipo, documento, lote y fecha;
  - reprocesar después de corregir la causa, con permiso propio.

  Los rechazados y los lotes que no corrieron a su hora generan alertas (FR-022) para los responsables
  de Inventario y de Contabilidad.
- **FR-081** El reporte de conciliación MUST mostrar, a una fecha de corte y por grupo contable y
  conjunto de cuentas mapeadas, con el detalle por bodega sólo como información:
  - el inventario valorizado, con lo que está en tránsito como columna propia;
  - el saldo contable de las cuentas mapeadas, obtenido por la consulta definida;
  - la diferencia;
  - los mensajes pendientes, en lote o rechazados que la explican.

  El saldo inicial cuenta como incluido en el saldo contable. Aparte, lista:
  - lo movido por tipos que no pasan a contabilidad;
  - las ventas a crédito de esos tipos, cuya obligación en Cartera no tiene ingreso en los libros;
  - mientras haya bodegas no activas que usan las mismas cuentas, esas bodegas con las cifras
    importadas de SOLIDO, como en FR-090: entran al valorizado del conjunto y se muestran aparte para
    identificarlas.
- **FR-082** El sistema MUST mostrar:
  - las combinaciones de operación, grupo contable y bodega en uso, de tipos que pasan a contabilidad,
    que no tienen regla vigente en la matriz;
  - los medios de pago activos sin cuenta vigente;
  - las reglas cuyas cuentas dejaron de ser elegibles;
  - los impuestos cuya cuenta tiene, en alguna fecha vigente, una tarifa distinta de la del catálogo.

  Así se corrigen antes de que la validación previa detenga una venta.
- **FR-083** El procesamiento de mensajes MUST registrar su actor así:
  - el procesamiento automático (en línea, o un lote a su hora) tiene como actor al proceso de
    integración de la cooperativa, identificado como tal, con canal «proceso», el mensaje o el lote
    como origen y sin IP de persona;
  - un lote ordenado a mano, un reproceso o un envío de FR-078 tienen como actor a la persona que lo
    ordenó;
  - el usuario que originó cada documento queda en el comprobante y en el lote como dato, nunca como
    actor;
  - el procesador no usa los permisos de ese usuario.

  Por cada cooperativa, el procesador MUST fijar el mismo contexto que una petición: su base de datos
  y su base de auditoría. Sin cooperativa resuelta falla, y nunca escribe en la auditoría global.

#### H · Integración con Cartera (pendiente, entrega IC)

Igual que F2: especificados y pendientes hasta que exista la especificación de Cartera.

- **FR-084** Con «VentaACreditoRegistrada», Cartera crea la obligación. Las notas crédito, las notas
  débito, las devoluciones y las anulaciones de una venta a crédito MUST informarse con su valor y la
  referencia al original, y Cartera ajusta la obligación o crea otra según sus reglas.
- **FR-085** Las consultas a Cartera MUST ser una interfaz definida, con tiempo máximo de respuesta
  parametrizable: estado, cupo y líneas de crédito de una persona, y estado de validación de una venta
  pendiente.

#### I · Reportes y tablero

- **FR-086** El módulo MUST ofrecer estos reportes:
  - kardex por producto, bodega, lote y rango, con saldo en cantidad y valor y enlace al documento;
  - inventario valorizado a cualquier fecha de corte, reconstruido desde el kardex, con lo que está en
    tránsito;
  - rotación y días de inventario;
  - análisis ABC, con umbrales parametrizables;
  - productos sin movimiento en N días y próximos a vencer en N días;
  - margen por producto, categoría, vendedor, cliente y punto de venta;
  - sugerido de compras, alertas de reorden y quiebres, y diferencias de conteo;
  - ventas por turno, caja y medio de pago;
  - documentos DIAN por estado, mensajes por estado y lotes;
  - indicios de deterioro (FR-021);
  - opcional, para empresas del régimen ordinario de renta: faltantes y mermas del año frente al tope
    legal sobre inventario inicial más compras.
- **FR-087** Todo reporte MUST poder exportarse a Excel y PDF, respetando permisos y alcance por
  bodega. Cada exportación queda auditada, y los datos personales exigen su permiso.
- **FR-088** El tablero MUST mostrar, por sucursal y bodega, y llegar al detalle con un clic:
  - valor del inventario, rotación, días de inventario y margen bruto;
  - ventas del día y del mes frente al período anterior;
  - productos bajo punto de reorden, quiebres y próximos a vencer;
  - mensajes pendientes o rechazados, lotes que no corrieron a su hora y documentos DIAN pendientes o
    rechazados;
  - tipos de documento fiscales configurados para no pasar a contabilidad;
  - alertas pendientes.

#### J · Puesta en marcha, convivencia y retiro del módulo actual

- **FR-089** El saldo inicial MUST cargarse por plantilla: producto, bodega, ubicación, cantidad y
  costo unitario, y lote, serie y vencimiento cuando el producto los controla (I6).
  - Tiene revisión previa completa de la plantilla, todo o nada.
  - Genera un documento de saldo inicial por bodega, fechado en su fecha de corte (la víspera de su
    activación), que se confirma con aprobación.
  - La cantidad cargada es la del conteo más o menos los movimientos que la bodega tuvo en SOLIDO entre
    el conteo y el corte. La otra opción es que la bodega deje de operar en SOLIDO desde el conteo
    hasta su activación.
  - Emite «SaldoInicialCargado», **informativo**: no genera comprobante, porque el valor de ese
    inventario ya está en los libros (apertura contable de la 009 o registros previos). No depende
    del modo de paso ni exige validación previa. Su anulación también es informativa.
- **FR-090** Antes de activar una bodega, el sistema MUST comparar, a su fecha de corte y por grupo
  contable y conjunto de cuentas mapeadas, el valorizado de todas las bodegas que usan esas cuentas
  contra su saldo contable, tal como Contabilidad lo lleva:
  - las bodegas activas y la que se activa entran con el valorizado del módulo nuevo;
  - las no activas, con las cifras importadas de SOLIDO a esa misma fecha (FR-091).

  Así, la primera bodega que se activa no muestra como diferencia el inventario de las que siguen en
  SOLIDO. Para esta comparación hacen falta la matriz y la consulta de saldos (I2). Activar exige
  cuadre, o aceptar la diferencia con motivo y permiso especial, y queda auditado.
- **FR-091** Durante la transición:
  - la activación es por bodega, con fecha de corte;
  - una bodega activa sólo la opera el módulo nuevo;
  - una bodega no activa sólo admite su documento de saldo inicial (y su anulación);
  - el sistema anterior MUST seguir siendo el de registro de las bodegas no activas;
  - no hay sincronización automática de transacciones entre los dos;
  - hay reportes comparativos de kardex y valorizado contra cifras importadas del sistema anterior.
- **FR-092** Las rutas, pantallas y tablas del módulo actual MUST retirarse. Retirar tablas es una
  migración destructiva con guarda: falla si hay filas, salvo el marcador de aprobación con
  referencia al respaldo y un segundo revisor. La tabla de rol de vendedores se conserva (Principio
  V), y la operación actual de crear y retirar vendedores se reemplaza por la de FR-031 antes de
  retirarla.
- **FR-093** El módulo MUST ofrecer el catálogo de permisos y los perfiles sugeridos para los ocho
  actores de la solicitud. El contador y el auditor sólo consultan; el auditor además verifica la
  integridad de la auditoría.
- **FR-094** El módulo MUST estar disponible en el navegador y en la app, igual que el resto del ERP.

#### Salida en vivo de COOFLOPAL

- **FR-095** COOFLOPAL MUST salir en vivo con el módulo nuevo en su alcance mínimo: entregas I1 a I4,
  porque está obligada a facturar electrónicamente.
  - **Decisiones del dueño**: el alcance mínimo, las plantillas, el retiro sin migrar, y que el
    proceso con Cartera (IC) quede pendiente, con crédito provisional mientras tanto (F2). Todas las
    fechas (ensayo, conteo, carga y salida) las define el dueño cuando la aplicación esté lista.
  - **Por defecto**: el ensayo fuera de producción es la decisión 1 de los Supuestos, aceptada por el
    dueño.

  De todo ello se sigue:
  - **Plantillas primero**. Lo primero que se entrega son las plantillas de importación de la
    parametrización: impuestos, retenciones y conceptos de retención (FR-013), grupos contables,
    unidades de medida, marcas, categorías, productos, bodegas y ubicaciones, tipos de documento
    (prefijo, consecutivo, aprobación, modo de paso y granularidad) y vendedores (personas ya
    registradas en el maestro, citadas por su documento).
    - Las de cajas, medios de pago, listas de precios y topes de descuento se entregan con ellas y se
      cargan cuando exista I3.
    - La matriz de reglas contables tiene su propia plantilla (I2), que COOFLOPAL diligencia con su
      contadora.
    - La de cifras de SOLIDO sirve para el ensayo, la conciliación y la activación.
  - **COOFLOPAL diligencia esas plantillas desde ya**, en lugar de parametrizar en las pantallas del
    módulo actual, y se cargan con la revisión previa de FR-030.
  - **Ensayo previo en una cooperativa de ensayo**, fuera de producción, con la parametrización
    cargada desde las plantillas y un saldo inicial provisional. Allí los tipos de documento pasan a una
    contabilidad de ensayo o quedan sin paso, y nada del ensayo llega a la cooperativa de producción.
    I1 a I4 MUST estar disponibles para el ensayo antes de la salida.
  - **Salida bodega por bodega**: en producción, cada bodega sale en vivo cuando su saldo inicial queda
    cargado y cuadrado (FR-090). Cada saldo inicial se fecha en la fecha de corte de su bodega
    (FR-089). Hasta su activación, SOLIDO registra sus ventas (FR-091).
  - **Módulo actual**: se retira sin migrar datos (FR-092).
  - **Después**: I5 e I6 van después de la salida. IC, cuando exista la especificación de Cartera.

#### K · Medios de pago y cierre de caja

- **FR-096** Los medios de pago MUST ser un **catálogo configurable** de cada cooperativa, sin
  programar. Cada medio de pago tiene:
  - código (la nomenclatura de la cooperativa, como en todo catálogo del ERP), nombre y orden en
    pantalla, con tecla rápida opcional en el POS;
  - **clase**, fija del sistema: efectivo, tarjeta de crédito, tarjeta de débito, crédito a asociado,
    crédito comercial a cliente, consignación, transferencia, bono o vale, cheque u otro;
  - para las tarjetas: la **franquicia o red** (Visa, Mastercard, American Express, débito de cada red…)
    y el **adquirente** y los **datáfonos** con que se cobra. Puede haber tantos medios de tarjeta como
    combinaciones use la cooperativa;
  - para consignación y transferencia: el banco y la cuenta de destino de la cooperativa, como dato del
    medio (no como cuenta contable);
  - reglas de captura: si exige referencia (número de aprobación, comprobante, consignación o bono) y
    cuál; si admite vueltas (sólo efectivo); si admite pago parcial; y si un número de bono puede
    usarse más de una vez (por defecto, no);
  - cómo se arquea (FR-099): contado físico (efectivo y cheques), por total de comprobantes o lote del
    datáfono (tarjetas), por referencias (consignaciones, transferencias y bonos) o sin arqueo
    (créditos);
  - en qué puntos de venta, canales y tipos de documento se ofrece;
  - estado activo o inactivo, con vigencia e historial (FR-012). Un medio con pagos no se borra: se
    inactiva.
- **FR-097** Una venta MUST poder pagarse con **cualquier combinación** de medios activos en su punto:
  - varias tarjetas distintas, o el mismo medio varias veces con referencias distintas;
  - efectivo con vueltas, bonos, créditos y los demás medios.

  Cada pago registra medio, valor y referencia. Además:
  - los créditos siguen F2;
  - un bono cuyo medio exige número único se rechaza si ese número ya se usó, nombrando la venta que
    lo usó;
  - una devolución de dinero sale por los medios que su nota permita, por defecto el mismo medio de
    la venta; otro medio exige permiso y queda auditado.
- **FR-098** Cada pago MUST llegar **automáticamente** a la cuenta contable que le corresponde, sin
  digitación. El mensaje de la venta o de la nota lleva cada pago con su medio, valor y referencia
  (FR-070), y la matriz de reglas de Contabilidad (FR-073) asigna a cada medio de pago, y
  opcionalmente al punto de venta, su cuenta:
  - la caja del punto, para el efectivo;
  - la cuenta por cobrar a la red o al adquirente, para las tarjetas;
  - el banco, para consignaciones y transferencias;
  - la cuenta de cartera o la de la cuenta por cobrar provisional, para los créditos;
  - la cuenta del bono.

  Un medio de pago sin cuenta vigente aparece en el reporte de completitud (FR-082) y detiene la
  validación previa (FR-074).
- **FR-099** El **cierre de caja** MUST hacerse por punto de venta y por medio de pago:
  - **por sesión**: al cerrar, el sistema muestra por medio de pago lo esperado (base + ventas −
    devoluciones ± movimientos de caja) y pide lo contado, según cómo se arquea cada medio:
    - el efectivo, por denominaciones o por total;
    - las tarjetas, por total de comprobantes o del lote de cada datáfono, con opción de cotejar
      referencia por referencia;
    - consignaciones, transferencias y bonos, por referencias;
  - **diferencias por medio**: exigen motivo y, por encima de la tolerancia del medio, aprobación
    (FR-010). Una diferencia aprobada emite «DiferenciaDeArqueoAprobada», que la matriz lleva a su
    cuenta: sobrante, faltante a cargo del cajero (como persona) o gasto, según el parámetro;
  - **cierre del día por punto**: consolida las sesiones del día con el mismo detalle por medio;
  - **informes**: el informe de cierre de sesión y el del día se imprimen y se exportan (FR-087). Los
    de tarjetas muestran el detalle por adquirente y datáfono, para cotejar después con los abonos de
    la red.
- **FR-100** Los **movimientos de caja** de una sesión MUST registrarse como documentos, con motivo y
  aprobación según su política: retiros parciales (a caja fuerte, a otra caja o para consignar),
  reclasificaciones entre medios de pago e
  ingresos de base. Cada uno emite «MovimientoDeCajaRegistrado», que la matriz lleva entre la caja del
  punto y la cuenta de destino. La **reclasificación entre medios de pago** corrige un pago de una venta
  confirmada que quedó con el medio equivocado (p. ej. registrado como Visa y pasado por Mastercard)
  sin tocar la venta ni el pago original: exige motivo y la aprobación que diga su política; en el
  arqueo baja el esperado del medio de origen y sube el del medio correcto por el mismo valor; emite
  «MovimientoDeCajaRegistrado» con el tipo `ReclassificationBetweenMeans`, que la matriz lleva de la
  cuenta del medio de origen (crédito) a la del medio correcto (débito).
- **FR-101** La conciliación de lo que pagan los adquirentes (comisiones y retenciones que practica la
  red) es de Tesorería y queda fuera de esta feature. El módulo MUST dejarla facilitada: por medio de
  tarjeta guarda adquirente, datáfono, lote y referencia de cada pago, y registra la comisión esperada
  como dato informativo del medio.

### Key Entities

- **Producto**: código, nombre, tipo (inventariable, servicio, combo, kit, plantilla o variante),
  categoría, marca, unidad base, unidades alternas con factor, códigos de barras, control de lote,
  serie o vencimiento, tratamiento de impuestos, concepto de retención, grupo contable, estado e
  imágenes. Se relaciona con sus variantes, sus componentes (combo o kit) y sus precios.
- **Categoría** (jerárquica) y **Marca**.
- **Unidad de medida**: decimales admitidos, y conversiones por producto.
- **Lote** (vencimiento) y **Serie**: por producto.
- **Catálogo de impuestos y retenciones**: tipo, forma de cálculo, tarifa o valor por unidad, concepto
  de retención, municipio y actividad (ICA), base mínima en UVT, condiciones por régimen, vigencia y
  norma.
- **Grupo contable**: la clasificación que Inventario envía y que la matriz de Contabilidad traduce a
  cuentas.
- **Bodega** (de una sucursal contable; operativa o de tránsito) y **Ubicación**.
- **Línea de kardex**: hecho sólo de adición, con cantidad (o cero, si es ajuste de costo) y costo con
  que se registró.
- **Proyecciones del kardex**: existencia por bodega, ubicación y lote, saldo acumulado y capas PEPS.
  Se calculan desde el kardex, se pueden reconstruir y se verifican contra él.
- **Reserva**: cantidad comprometida por un pedido vigente.
- **Clase de documento** (fija: efecto en inventario, si es fiscal, mensajes) y **Tipo de documento**
  (parametrizable: consecutivo, prefijo, aprobación, modo de paso y su granularidad, campos
  obligatorios, bodegas, canal).
- **Documento** con **líneas**: estado, fechas, tercero y copia inmutable de su identificación fiscal
  tal como se emitió, sucursal, centro de costo, bodegas, totales, impuestos, pagos, vendedor,
  modo de paso sellado, documento relacionado y motivo. Son documentos:
  - los de inventario, incluida la causa de bajas y ajustes, con sus soportes;
  - los de compra: solicitud, orden, recepción, factura del proveedor con su CUFE y el estado de sus
    eventos, documento soporte, devolución, y costos adicionales con su prorrateo;
  - los de venta: cotización, pedido, remisión, factura, documento equivalente POS y notas;
  - los traslados y los conteos, con su foto, sus capturas y su ajuste.
- **Período de inventario**: uno por cooperativa, abierto o cerrado, con su valorizado fijado.
- **Lista de precios** (ámbito y vigencia), **Promoción**, **Tope de descuento por rol** y **Canal**.
- **Punto de venta y caja** (bodega, canal y tipos de documento: venta POS, factura a pedido del
  comprador, notas y contingencia, cada uno con su prefijo y, cuando aplica, su resolución),
  **Sesión de caja** (apertura, movimientos de caja y cierre) y **Cierre del día** por punto.
- **Medio de pago**: código, nombre, clase, franquicia o red, adquirente y datáfonos, banco y cuenta
  de destino, reglas de captura y de arqueo, puntos, canales y tipos donde se ofrece, comisión esperada,
  estado y vigencia.
- **Pago**: medio, valor y referencia de cada pago de un documento (adquirente, datáfono y lote en las
  tarjetas; número en los bonos).
- **Movimiento de caja**, y **Arqueo**: por medio de pago, lo esperado, lo contado (con denominaciones
  o referencias), la diferencia, el motivo y la aprobación.
- **Documento electrónico DIAN**:
  - tipo, número, resolución, código único (CUFE, CUDE o CUDS), estado y transmisiones;
  - modo y proveedor o software con que se numeró;
  - versiones del archivo firmado, respuestas y representación gráfica.
- **Resolución de numeración**: prefijo, rango, vigencia, ambiente, tipo de documento, software o
  modo asociado, y clave técnica en las de factura.
- **Configuración de emisión electrónica**: modo, proveedor, vigencia, referencia al certificado y a
  las credenciales (que se guardan fuera de la base) e historial, por cooperativa.
- **Parámetro con vigencia**: clave, valor, desde, hasta, motivo e historial.
- **Política de aprobación** (niveles con umbral y permiso) y **Aprobación** (quién, cuándo, decisión
  y motivo).
- **Alerta**: tipo, destinatarios, canal, fecha, estado y quién la atendió.
- **Mensaje de negocio**: identificador, tipo, versión, contenido, documento, estado de entrega por
  destino (pendiente, en lote, procesado, rechazado, no aplica o validación fallida), lote, intentos y
  motivo de rechazo.
- **Lote de contabilización**: número, rango de fechas, mensajes y documentos que contiene,
  granularidad, totales, estado, actor (proceso de integración o persona que lo ordenó) y resultado.
- **Matriz de reglas contables**, en Contabilidad: operación + grupo + bodega → cuentas, incluidas las
  de cada impuesto y tarifa, con vigencia.
- **Registro de auditoría**: actor, fecha, origen, entidad, antes y después, motivo, y sello de
  integridad.
- Existentes que se usan sin duplicar: **Persona** (cliente, proveedor o asociado, con su
  autorización de tratamiento de datos; consumidor final genérico), **Vendedor** (su tabla de rol),
  **Sucursal contable**, **Centro de costo** y **Adjunto**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con el stock negativo prohibido, en 1.000 repeticiones de 50 ventas simultáneas de 1
  unidad del mismo producto con existencia 10, se confirman exactamente 10, se rechazan 40 y la
  existencia nunca queda negativa (0 casos).
- **SC-002**: Repetir cualquier operación o reprocesar cualquier mensaje al menos 3 veces produce
  exactamente un efecto: 0 documentos, comprobantes u obligaciones duplicados.
- **SC-003**: Anular una factura validada, con una sola acción del usuario, emite su nota crédito total
  y deja:
  - existencia y costo revertidos, si la factura había descargado existencia (FR-066);
  - si la factura pasó a contabilidad, el comprobante de la nota en Contabilidad, enlazado al
    original. Que haya pasado depende del destino de su mensaje: el modo sellado al confirmarla o, si
    es desde remisiones, el de éstas (FR-075). Si no pasó, ni la factura ni la nota generan
    comprobante desde Inventario, y lo que registre Cartera depende de D-02;
  - si tenía parte a crédito, la obligación de cartera ajustada;
  - el enlace al original visible desde Inventario y, donde haya registro, desde Contabilidad y
    Cartera.
- **SC-004**: Un cajero entrenado registra una venta de 5 productos distintos con 6 lecturas, de
  contado y a consumidor final, en menos de 30 segundos. El tiempo va desde la primera lectura hasta que
  la caja queda libre con uno de tres resultados:
  - **validado**: la DIAN respondió normalmente, es decir, dentro de la espera máxima del punto de venta
    (`Dian.EsperaMaximaPosSegundos`, 15 s por defecto), y la representación gráfica está lista para
    entregar;
  - **contingencia**: el documento de contingencia (tipo 03 o 04) con su representación gráfica lista;
  - **pendiente de entrega**: agotada la espera sin respuesta, la venta queda confirmada y cobrada, sin
    tirilla; la tirilla sale cuando el documento se valida.

  Los tres cuentan como éxito del cajero, porque la venta está cobrada y la caja libre; la proporción
  de ventas pendientes de entrega se mide y se informa aparte. Dentro de los 30 segundos, la emisión
  electrónica tiene su propio presupuesto: percentil 95 de 5 segundos o menos con 30 cajas (SC-019).
- **SC-005**: Con cero mensajes pendientes, en lote o rechazados, la conciliación muestra diferencia
  cero entre inventario valorizado y saldo contable, por grupo contable y a cualquier fecha de corte:
  - el saldo inicial cuenta como incluido en el saldo contable;
  - las partidas de los tipos que no pasan a contabilidad se muestran aparte;
  - las bodegas que siguen en SOLIDO se cuentan con sus cifras importadas, como en FR-090.
- **SC-006**: La verificación de integridad del kardex reporta 0 diferencias entre existencias,
  saldos y lo que resulta del kardex, en cualquier momento.
- **SC-007**: El costo calculado coincide al peso con casos calculados a mano para compras, ventas,
  devoluciones, traslados, prorrateos, retroactivos, PEPS, ensamble y anulaciones de entradas ya
  consumidas.
- **SC-008**: Vender, recibir, trasladar, contar y consultar existencia se completan en 3 pasos o
  menos (según la definición de FR-019) en el guion de prueba.
- **SC-009**: La búsqueda de productos muestra resultados mientras se escribe en menos de 1 segundo,
  con un catálogo de 50.000 productos.
- **SC-010**: Con Contabilidad o Cartera no disponibles, y con la política por defecto ante la
  validación previa sin respuesta (FR-074), las ventas de contado se siguen registrando sin error.
  Al volver, sin intervención manual:
  - lo acumulado de tipos en línea se procesa;
  - lo de tipos por lotes se procesa en su lote siguiente.

  Sólo lo rechazado por reglas queda en la bandeja, con su motivo.
- **SC-011**: Con ambos módulos disponibles, el 95 % de los mensajes de tipos en línea se procesan
  antes de 1 minuto desde la confirmación.
- **SC-012**: El 100 % de las acciones del módulo aparece en la auditoría con actor, fecha, origen,
  antes y después y, cuando la acción lo exige, motivo. La verificación de integridad detecta el
  100 % de las alteraciones simuladas.
- **SC-013**: Ninguna vía de la aplicación permite modificar o borrar un documento confirmado, una
  línea de kardex o un mensaje emitido (comprobación automática). La única excepción es la corrección
  del caso a de FR-066, que agrega una versión auditada.
- **SC-014**: El 100 % de las rutas del módulo exige permiso, y ningún usuario ve ni opera documentos
  de bodegas fuera de su alcance (comprobación automática).
- **SC-015**: En operación normal, es decir, sin contingencia declarada y con el canal disponible (su
  circuito de protección cerrado), medida sobre un mes calendario, extendido hasta al menos 1.000 documentos electrónicos si no
  los alcanza, y sólo en el sandbox del proveedor o en producción (nunca con el canal
  simulado, cuyo resultado lo decide el propio simulador):
  - el 99 % de los documentos electrónicos de la ventana queda validado por la DIAN en el primer envío;
  - ningún documento se entrega al comprador antes de su validación;
  - ninguno supera el plazo legal de transmisión sin alerta previa.
- **SC-016**: Una cooperativa carga un saldo inicial de hasta 20.000 líneas, lo concilia contra
  contabilidad y activa sus bodegas en una jornada de trabajo.
- **SC-017**: El inventario valorizado a una fecha pasada, con 50.000 productos en 50 bodegas, está
  listo en menos de 30 segundos.
- **SC-018**: En la marcha paralela de la cooperativa de ensayo, cada diferencia de kardex y valorizado
  contra SOLIDO tiene su causa documentada y aprobada por el jefe de inventario antes de la salida en
  vivo.
- **SC-019**: Con 30 cajas vendiendo a la vez en una cooperativa, el percentil 95 del tiempo de
  SC-004 sigue por debajo de 30 segundos, y el percentil 95 de la emisión electrónica (desde el envío
  al canal hasta su respuesta o el fin de la espera máxima del punto de venta) no pasa de 5 segundos.
  La emisión se mide en el sandbox del proveedor, no contra el canal simulado.
- **SC-020**: Un lote con las ventas de un día (5.000 documentos) se contabiliza en menos de 10
  minutos, y procesarlo otra vez no crea nada nuevo.
- **SC-021**: Ningún mensaje de un documento cuya validación previa respondió «contabilizable» se
  rechaza por una causa que esa validación podía detectar, salvo que la regla o la cuenta cambie, o que
  el período contable se cierre, después de confirmar. Lo confirmado sin respuesta de la validación y
  lo enviado por FR-078 se mide aparte.
- **SC-022**: Toda alerta llega a su bandeja con al menos un destinatario: 0 alertas sin destinatario.
- **SC-023**: Durante el crédito provisional, el 100 % de las ventas a crédito queda con su mensaje
  para Cartera guardado y visible como pendiente. Cuando exista Cartera, todos se entregan en orden y
  sin duplicar: 0 perdidos, 0 repetidos.
- **SC-024**: El 100 % de los pagos llega a Contabilidad en la cuenta de su medio de pago sin
  digitación: 0 pagos sin cuenta, porque la validación previa detiene al medio que no la tiene.
- **SC-025**: El cierre de una sesión de caja con 300 ventas y 6 medios de pago se completa en menos de
  5 minutos, con lo esperado calculado por medio y el informe listo para imprimir.

## Assumptions

- **Organización**: el módulo sirve a toda cooperativa o empresa comercial cliente del ERP. COOFLOPAL
  es la primera en usarlo.
- **Multiempresa**: significa varias cooperativas, cada una en su base (Principio IV).
  - Dentro de una cooperativa hay un solo emisor, con varias sucursales y bodegas.
  - Operaciones entre dos cooperativas del ERP son una venta de una y una compra de la otra.
- **Moneda**: sólo pesos colombianos. Documentos y mensajes llevan moneda y tasa para crecer a
  multimoneda.
- **Personas**:
  - clientes, proveedores y vendedores son personas del maestro;
  - la venta sin identificar al comprador usa una persona genérica «consumidor final», dentro de los
    límites de la norma;
  - el régimen tributario de cada persona (responsable de IVA, gran contribuyente, autorretenedor,
    exenciones) ya vive en el maestro.
- **Segmento de asociado**: para listas de precios es la clase del asociado que ya existe en el
  maestro.
- **Aprobaciones**: quien aprueba lo hace con su propia identidad desde su sesión. El sistema nunca
  pide ni guarda la contraseña de otra persona.
- **Crédito a clientes que no son asociados**: lo administra Cartera, igual que el de asociados.
  Inventario sólo emite la venta a crédito.
- **Cobros de contado**: se registran como pagos del documento y en el arqueo del POS. La
  consignación del efectivo es de Tesorería.
- **Hardware**: lectores en modo teclado. La impresión de tirillas y facturas sale del navegador o de
  la app, sin controladores propios.
- **Escala de referencia por cooperativa**:
  - hasta 50.000 productos, 50 bodegas y 30 cajas simultáneas;
  - 5.000 documentos al día.
- **Retención**: la auditoría del módulo se conserva 10 años, como la contable, por encima del mínimo
  de 5 del Principio X, porque respalda documentos comerciales y fiscales.
- **Marco contable**: por defecto, el Grupo 2 (NIIF para PYMES), como en la 009.
- **Recaudo del crédito provisional**: mientras IC está pendiente, los pagos de estos créditos se
  registran por fuera del módulo, en Contabilidad o Tesorería. La especificación de Cartera define cómo
  se incorporan a las obligaciones.
- **Fechas y cifras de ejemplo** (tolerancias, 90 %, 30 días, tiempos): son valores por defecto
  parametrizables, no reglas fijas.

### Decisiones por defecto de la revisión de consistencia

La revisión del 2026-09-24 encontró huecos que las tres respuestas del dueño no cubrían. Se
resolvieron así, y el dueño las **aceptó** ese mismo día (Clarifications). Puede cambiar cualquiera más
adelante.

1. **Ensayo y salida**: el ensayo previo corre en una cooperativa de ensayo, fuera de
   producción, porque en producción una bodega no activa sólo admite su saldo inicial. La salida en
   producción es bodega por bodega, cuando cada una queda cuadrada, con el saldo inicial fechado en su
   corte. El cuadre se hace por conjunto de cuentas, sumando las bodegas que siguen en SOLIDO con sus
   cifras importadas. Las fechas las define el dueño.
2. **El saldo inicial no contabiliza**: su valor ya está en los libros por la apertura de la 009.
   Contabilizarlo lo contaría dos veces.
3. **Granularidad del modo de paso**: un valor general por cooperativa, con excepción por tipo de
   documento. Los tipos encadenados comparten modo, y el modo se sella al confirmar. El dueño pidió
   el parámetro, no su granularidad.
4. **Por lotes**: acumular y contabilizar juntos, con la opción de un comprobante resumido por fecha,
   tipo y sucursal que respeta las reglas de cada cuenta (tercero, documento cruce, base gravable).
5. **Correcciones en Contabilidad**: toda anulación, nota o ajuste es un comprobante nuevo con fecha
   propia, enlazado al original. Nunca se reversa el comprobante original, y así se evita tocar
   períodos declarados (C7).
6. **Validación previa**: aplica las mismas reglas de cuenta de la 009, no sólo «¿hay regla?». Sin
   respuesta, un parámetro decide, y por defecto se confirma (R6).
7. **Actor del procesamiento**: el proceso de integración de la cooperativa, o la persona que ordena
   un lote o un reproceso. El usuario de origen queda como dato.
8. **Documentos fiscales que emite la cooperativa**: se corrigen con el documento que exige la norma
   (nota crédito o nota de ajuste), nunca con un documento contrario, salvo los rechazados por la
   DIAN (FR-066, casos b y c). El registro de una factura o nota recibida de un proveedor se anula con
   un documento contrario (FR-006).
9. **Dos contingencias**, la del facturador y la de la DIAN, como las separa la norma.
10. **«Configurable»** (Q3): por cooperativa y con vigencia, no por tipo de documento electrónico.
11. **Acuse de recibo y recibo del bien** de facturas de proveedor a crédito: sin ellos se pierde la
    deducibilidad. El ERP los emite desde I5, y mientras tanto alerta y registra los que la
    cooperativa emite por fuera.
12. **Remisiones sin facturar**: el cierre las lista y exige aceptarlas con motivo. Se supone que la
    cooperativa factura cada remisión dentro del mismo mes. Si no, el contador causa el ingreso por
    facturar con un comprobante de la 009, usando la lista con los valores que entrega el cierre.
13. **Promociones**: se aplican como descuentos no condicionados, no como líneas a precio cero.
14. **Consumo interno**: su tipo dice si es retiro gravado, con base en el valor comercial.
15. **Bodegas de tránsito** por sucursal (la de origen del traslado), para que la mercancía en camino
    siga en el kardex y en el valorizado.
16. **Pendiente de validar**: Cartera evalúa el cupo a la fecha de la venta, sin contarla, e
    Inventario consulta el resultado.
17. **Catálogo de impuestos**: es del módulo (o de Core), no de Contabilidad. Las tarifas se alinean
    por la matriz (C8).
18. **Enviar después lo que no pasó** (FR-078) se deriva de conservar los mensajes «no aplica», para
    una cooperativa que empieza a usar la contabilidad del ERP más tarde. No lo pidió el dueño y puede
    quitarse sin afectar lo demás.
19. **Documentos derivados y ajustes de costo** siguen el destino del mensaje de su documento de
    origen o del documento afectado, no un modo propio. Así una factura nunca pasa sin el costo de su
    remisión.
20. **Comprobante de venta no electrónico**, sólo para una cooperativa con el parámetro «obligada a
    facturar electrónicamente» apagado (FR-063). Una cooperativa obligada no confirma ventas fiscales
    hasta que exista I4.

## Dependencias

- **D-01 · Contabilidad (enmienda de la 009)**. Esta feature construye en I2 las piezas del lado de
  Contabilidad:
  - la matriz de reglas, con sus pantallas y su plantilla (FR-073);
  - el procesamiento en línea y por lotes, con comprobantes por documento o resumidos (FR-076 y
    FR-077);
  - el registro de anulaciones, notas y ajustes como comprobantes nuevos (FR-079);
  - las consultas de FR-014;
  - el aviso antes de cerrar un período contable con mensajes de Inventario sin procesar, fechados en
    él. Un mensaje rechazado por período cerrado se recupera reabriendo el mes (y el ejercicio, si ya
    se cerró) según las reglas de la 009 (FR-022 y FR-023 de la 009).

  La especificación de la 009 se enmienda para Inventario en:
  - FR-030 y FR-038: la corrección de lo de Inventario es un comprobante nuevo, no la reversión total;
  - FR-036 y FR-039: Inventario no usa el contrato sincrónico;
  - FR-037: el origen de un comprobante resumido es el lote;
  - SC-004: para Inventario pasa a ser «cero documentos confirmados sin mensaje y cero comprobantes sin
    mensaje; lo pendiente, rechazado o que no pasa se ve en la bandeja»;
  - la frase de su US4 «si rechaza, la operación del módulo falla completa» y su escenario 5, que no
    aplican a Inventario;
  - las filas de Inventario en la tabla de su contrato;
  - la entrada de Inventario en su decisión R16, porque las cuentas de producto e IVA se reemplazan
    por la matriz;
  - una nota en su decisión R2.

  La matriz cumple las validaciones de cuenta de la 009 (FR-016 y FR-017 de la 009) como cualquier
  parametrización.
- **D-02 · Cartera**: necesita **su propia especificación** (especificación, plan y tareas). Hoy no
  existe nada de esto. Debe cubrir:
  - la consulta de estado, cupo y líneas de crédito, y la de estado de validación de una venta
    pendiente;
  - la creación y el ajuste de obligaciones desde los mensajes de Inventario, incluidas las notas
    débito;
  - el crédito comercial a clientes que no son asociados;
  - cuál lado registra la cuenta por cobrar (FR-062);
  - la recepción de los mensajes acumulados durante el crédito provisional y la validación de esas
    ventas;
  - el recaudo de esos créditos.

  Es condición de la entrega IC, que queda pendiente por decisión del dueño (Clarifications).
- **D-03 · Plataforma**, para las piezas que no existen:
  - mensajes que nacen con el documento;
  - un procesador por cooperativa que fije el mismo contexto que una petición (su base de datos y su
    base de auditoría), ejecute por el camino común con el actor de FR-083, falle sin cooperativa y
    nunca escriba en la auditoría global. Una prueba debe comprobar que una operación en segundo
    plano se audita en la base de su cooperativa;
  - claves de idempotencia de operación;
  - auditoría con IP, diferencias, entrega garantizada y sello de integridad verificable (hoy falta
    todo esto, y el Principio X ya lo exige);
  - aprobaciones en varios niveles y por montos, reutilizables;
  - parámetros con vigencia, generalizados desde el patrón de Nómina;
  - bandeja de alertas y notificaciones.
- **D-04 · Adjuntos (feature 011)**, con dueños nuevos en las reglas de adjuntos por módulo:
  1. **imágenes de producto**: subida directa por quien puede editar el producto, y borrado auditado;
  2. **documentos electrónicos DIAN** (firmados, respuestas, representación gráfica): los genera el
     módulo y se guardan desde el servidor. No se borran, no admiten subidas y se leen con el permiso
     de consulta de ventas o compras;
  3. **soportes de bajas y ajustes negativos**: subida directa, y no se borran una vez confirmado el
     documento.

  El archivo de la factura del proveedor sólo se lee para prellenar; no se guarda.
- **D-05 · DIAN**:
  - habilitación de cada cooperativa emisora, resoluciones de numeración (también la de
    contingencia) y certificado de firma;
  - para el modo proveedor, el contrato con un proveedor tecnológico externo para toda cooperativa
    obligada, como COOFLOPAL;
  - sus credenciales, fuera de la base y del repositorio;
  - para el modo software propio, el servicio central de la 010 (entrega N3, pendiente), ampliado a
    los documentos de FR-063.
- **D-06 · Personas (feature 008)**: el alta de clientes y proveedores desde el diálogo único (la fase
  2 pendiente de esa feature), con el aviso de privacidad y el registro de la autorización (FR-011).
  También la persona genérica «consumidor final».
- **D-07 · COOFLOPAL**, para cumplir FR-095:
  - diligenciar desde ya las plantillas de parametrización, y no parametrizar en las pantallas del
    módulo actual;
  - definir con su contadora el modo de paso de cada tipo de documento y diligenciar la matriz,
    dejando vacío el reporte de FR-082 antes del ensayo;
  - elegir su proveedor tecnológico, porque está obligada a facturar electrónicamente, y registrar
    resoluciones (también la de contingencia), certificado y configuración del modo;
  - hacer el conteo físico antes de la carga del saldo inicial;
  - entregar las cifras de SOLIDO para el ensayo, la conciliación y la marcha paralela.

  Las fechas de todo esto las define el dueño.
