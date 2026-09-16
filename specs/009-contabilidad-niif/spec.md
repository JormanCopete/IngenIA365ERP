# Feature Specification: Contabilidad NIIF — plan de cuentas, comprobantes, integración e informes

**Feature Branch**: `009-contabilidad-niif`
**Created**: 2026-09-14
**Status**: Draft con las decisiones del dueño incorporadas (2026-09-14)
**Input**: Solicitud del usuario: reestructurar por completo el módulo de contabilidad. (1) Pensado
en NIIF/NIC. (2) Recibe transacciones **en línea** desde Nómina, Cartera financiera, Inventario
comercial y cualquier otro módulo que integre con contabilidad. (3) Los documentos que vienen de
otros módulos sólo se modifican desde su módulo, para conservar la integridad. (4) Dos planes de
cuentas —**PUC Solidario** y **PUC Comercial**— cargados hasta el **nivel 4**; al iniciar una
empresa el administrador elige cuál, y define si las **cuentas de movimiento** (auxiliares) son las
de nivel 5 o las de nivel 6; ningún módulo puede parametrizar ni registrar movimientos en cuentas
que no sean de movimiento. (5) La ventana de parametrización del plan y de las auxiliares con
integridad fuerte: sin cambios de configuración si la cuenta ya tiene movimientos en el año
contable; parámetros «habilitada para módulos», «exige tercero», «exige documento cruce», «exige
centro de costo», «exige sucursal»; y toda transacción se comporta y exige datos según esa
configuración. (6) La ventana de digitación de comprobantes: secuencial e intuitiva, con todas las
validaciones antes y al registrar, y con ayuda en cada campo y guía en la ventana. (7) Todos los
informes de una contabilidad completa: exportables a Excel, PDF y Word; consultas en pantalla que
desde una cuenta o auxiliar profundicen hasta documentos y terceros de forma auditable; filtros
dinámicos múltiples. (8) Auditoría de todos los procesos, incluyendo todos los registros y el
ingreso a todas las opciones. (9) Todo lo demás que exija la norma colombiana e internacional.

## Contexto

El módulo de contabilidad actual es la transcripción del de SOLIDO (37 tablas `cnt_`), y al
revisarlo se encuentra esto:

1. **El plan de cuentas no tiene catálogo de referencia**: la semilla deja sólo las nueve clases;
   grupos, cuentas y subcuentas los digitaría cada cooperativa a mano. La cuenta arrastra más de
   cincuenta atributos heredados que ninguna pantalla usa.
2. **Las reglas de comportamiento existen pero nadie las hace cumplir**: hay banderas «exige
   tercero», «maneja centro de costo», «exige documento» y «aplica a» siete módulos, y la
   digitación de comprobantes sólo comprueba que la cuenta exista y que débitos y créditos cuadren.
   Tampoco se distingue una cuenta de agrupación de una de movimiento.
3. **Quince operaciones de cinco módulos** (Nómina, Cartera, Inventario, Tesorería, CDT) escriben
   movimientos contables directamente, cada una con sus propias comprobaciones o sin ninguna. No
   hay un contrato único ni una validación común, y un comprobante nacido en un módulo se puede
   tocar desde Contabilidad.
4. **Los saldos se guardan aparte de los movimientos** (tablas con una columna por mes para
   cuentas, terceros, documentos y presupuesto; una columna por día para saldos promedio): un
   informe puede no cuadrar con el libro y nada lo detecta.
5. **Ninguna de las veinte rutas de contabilidad exige permiso**: cualquier sesión autenticada
   puede crear, contabilizar o anular comprobantes y cambiar el plan de cuentas.
6. **Informes**: balance general, estado de resultados y libro mayor sólo en PDF; consultas de
   saldos y movimientos sin poder profundizar de la cuenta al tercero, al documento ni al
   comprobante.
7. **Los satélites** —conciliación bancaria, presupuestos, líneas de impuestos, formatos DIAN,
   certificados de retención, «categorías de riesgo» (en realidad saldos diarios promedio),
   amortizaciones y depreciaciones— son pantallas de mantenimiento de tablas heredadas, sin flujo
   de trabajo, sin generación de comprobantes y sin exportación.
8. **No hay registro de qué opciones abre cada usuario**; sí quedan auditados los comandos.

**Dato verificado el 2026-09-14**: los libros están **vacíos** en todos los ambientes (DEV, QA y
producción: 9 cuentas de semilla, 0 comprobantes, 0 movimientos, un solo tipo de comprobante). No
hay datos históricos que migrar: el rediseño puede reemplazar la estructura sin conversión.

## Clarifications

### Session 2026-09-14

- Q: ¿Los satélites heredados (conciliación bancaria, presupuestos, impuestos y formatos DIAN,
  certificados de retención, saldos diarios promedio, amortizaciones y depreciaciones) quedan fuera
  o se reconstruyen aquí? → A: **Se reconstruyen en esta feature**, como historias P3 después del
  núcleo, todos sobre el plan nuevo y el contrato único de contabilización.
- Q: ¿De dónde salen los catálogos PUC? → A: **El equipo transcribe los dos catálogos de la norma
  pública** (Comercial desde el Decreto 2650; Solidario desde el catálogo de la Supersolidaria bajo
  NIIF), **el contador de la cooperativa los valida en QA antes de producción, y además existe un
  importador** para que una cooperativa cargue su propio catálogo como tercera plantilla.
- Q: ¿La digitación manual contabiliza al guardar o pasa por borrador? → A: **Borrador y
  «Contabilizar» son permisos distintos** (la misma persona puede tener ambos) **y cada empresa puede
  exigir cuatro ojos**: quien contabiliza debe ser distinto de quien registró.
- Q: ¿Cómo entran los saldos de una cooperativa que ya opera (años de contabilidad en SOLIDO)?
  → A: **Comprobante de apertura** con tipo reservado «Apertura», digitado o importado desde
  archivo (cuenta, tercero, documento cruce, centro, sucursal, débito/crédito), cuadrado, fechado el
  día anterior al primer período, único por empresa (corregir = reversar y cargar otro); pasa por el
  contrato y las reglas de cuenta como cualquier comprobante. No se migran movimientos de SOLIDO.
- Q: ¿Toda línea lleva sucursal? → A: **Sí, siempre**: la empresa define una **sucursal principal**
  que se propone sola (a un usuario con sucursales asignadas se le propone la suya); la regla
  «exige sucursal» de la cuenta significa que el usuario debe elegirla explícitamente y no vale la
  propuesta. El centro de costo sigue siendo opcional según la cuenta.
- Q: ¿Quién es el tercero cuando la contraparte es una EPS, ARL, fondo, caja o banco? → A:
  **Vínculo explícito entidad → persona** en cada catálogo institucional (EPS, ARL, fondos de
  pensiones y cesantías, cajas de compensación, bancos); el vínculo es obligatorio en cuanto alguna
  cuenta parametrizada para ese módulo exige tercero, y la parametrización lo valida al guardar. La
  persona del maestro es la única fuente del tercero (Principio V).
- Q: ¿En qué formato llega el extracto bancario? → A: **Mapeo de columnas por cuenta bancaria**:
  el tesorero define una vez qué columna es fecha, referencia, descripción y valor (uno con signo,
  o débito y crédito separados), el formato de fecha y el signo; se reutiliza cada mes; la
  plantilla propia del ERP es el mapeo por defecto.
- Q: ¿Qué formatos de exógena trae el ERP de fábrica? → A: **Todos los formatos y conceptos de
  la resolución vigente** para el año gravable 2026, como semilla editable, con las cuentas del
  catálogo propuestas en los conceptos donde el vínculo es natural; los que no aplican a la
  cooperativa se marcan «no aplica» y no generan. Un año nuevo trae su semilla sin tocar lo ya
  parametrizado.
- Q: ¿El alcance de sucursales de un usuario limita lo que contabilizan los módulos? → A: **No**:
  limita la digitación manual y las consultas; los módulos contabilizan con la sucursal de la
  operación (la del empleado, del crédito, de la factura), sin importar el alcance de quien la
  ejecuta.

## Alcance

### Dentro de esta feature

- Configuración inicial de la contabilidad de cada empresa (PUC, nivel de movimiento, ejercicio,
  cuatro ojos).
- Catálogos PUC Solidario y PUC Comercial hasta nivel 4, versionados, con su clasificación NIIF, e
  importador de catálogo propio de la cooperativa.
- Plan de cuentas de la empresa: cuentas de agrupación (catálogo) y auxiliares (nivel 5 y 6) con
  sus reglas de comportamiento y su protección.
- Tipos de comprobante, períodos contables, cierre mensual, cierre y reapertura del ejercicio.
- Ventana de digitación de comprobantes manuales, borradores y lista de comprobantes.
- Contrato único de contabilización para los módulos, con Nómina primero (es el único módulo en
  producción) y después Cartera, Inventario, Tesorería y CDT/Ahorros, que hoy escriben directo.
- Vínculo de las entidades de los catálogos institucionales (EPS, ARL, fondos de pensiones y
  cesantías, cajas de compensación, bancos) con una persona del maestro, para que sean terceros
  contables.
- Consultas con profundización (cuenta → tercero → documento → comprobante → línea), informes
  contables, estados financieros NIIF y saldos diarios promedio, exportables a Excel, PDF y Word.
- Satélites reconstruidos: conciliación bancaria, presupuesto y ejecución, impuestos (retenciones,
  IVA, ICA, GMF) con certificados de retención, información exógena (medios magnéticos DIAN), y
  activos fijos y diferidos con depreciación y amortización automáticas.
- Permisos por capacidad para todo el módulo y auditoría de cada acción, de cada exportación y del
  ingreso a cada opción (este último mecanismo es general para todo el ERP).

### Fuera de esta feature

- Multimoneda y diferencia en cambio; consolidación de varias empresas.
- Notas a los estados financieros y políticas contables como texto.
- Conciliación fiscal (formato 2516) y reportes a la Supersolidaria (SICSES): el catálogo debe
  dejar los códigos compatibles, pero los formatos de envío no se construyen aquí.
- Conexión directa con los bancos: el extracto llega por archivo o digitación.
- Presupuesto con control previo de compromisos (estilo sector público): sólo presupuesto y
  ejecución.
- Deterioro y revaluación automáticos de activos fijos (se registran con comprobante manual);
  impuesto de timbre por grados (tabla heredada sin uso).
- Migración de los movimientos históricos de SOLIDO: no se hace; una cooperativa que ya opera
  entra con sus **saldos** por el comprobante de apertura (US13), no con su historia.

### Glosario

| Término | Significado en esta especificación |
|---|---|
| Nivel 1 · clase | 1 dígito (1 Activo, 2 Pasivo, 3 Patrimonio, 4 Ingresos, 5 Gastos, 6 Costos de ventas, 7 Costos de producción, 8 y 9 Cuentas de orden). |
| Nivel 2 · grupo | 2 dígitos (p. ej. 11 Efectivo y equivalentes). |
| Nivel 3 · cuenta | 4 dígitos (1105 Caja). |
| Nivel 4 · subcuenta | 6 dígitos (110505 Caja general). Es hasta donde llegan los catálogos. |
| Nivel 5 · auxiliar | Cuenta creada por la empresa bajo una subcuenta; longitud configurable (8 dígitos por defecto). |
| Nivel 6 · sub-auxiliar | Cuenta creada por la empresa bajo una auxiliar; longitud configurable (10 dígitos por defecto). |
| Cuenta de movimiento | La del **nivel de movimiento** que la empresa configuró (5 o 6). Es la única que recibe movimientos y la única que otros módulos pueden parametrizar. |
| Cuenta de agrupación | Toda cuenta que no es de movimiento: niveles 1 a 4 siempre; nivel 5 cuando el nivel de movimiento es 6. |
| Catálogo | Plantilla de cuentas hasta nivel 4: Solidario, Comercial o el propio de la cooperativa (importado). |
| Comprobante | Documento contable con tipo, número consecutivo, fecha, descripción y dos o más líneas cuadradas. |
| Movimiento / línea | Cuenta, tercero, documento cruce, centro de costo, sucursal, débito o crédito, detalle, base gravable. |
| Documento cruce | Referencia (tipo y número) al documento que la línea afecta —factura, cuenta de cobro, contrato— para llevar saldo por documento. |
| Módulo origen | Módulo que generó el comprobante: Contabilidad (manual), Nómina, Cartera, Inventario, Tesorería, CDT/Ahorros, Activos, Cierre. |
| Contabilizar | Volver definitivo un comprobante: recibe número, afecta saldos y ya no se edita. |
| Cuatro ojos | Regla opcional por empresa: quien contabiliza un comprobante manual debe ser distinto de quien lo registró. |
| Reversión | Comprobante que anula a otro con las mismas líneas invertidas, referenciado en ambos sentidos. Es la única corrección posible de un comprobante contabilizado. |
| Apertura | Comprobante único por empresa, con tipo reservado, que carga los saldos con que la cooperativa arranca en el ERP; se fecha el día anterior al primer período y los informes lo tratan como saldo inicial, no como movimiento del ejercicio. |
| Sucursal principal | La sucursal que la empresa define en la configuración contable; toda línea lleva sucursal, y ésta es la que se propone cuando la cuenta no obliga a elegirla explícitamente. |
| Período | Mes de un ejercicio (año fiscal), abierto o cerrado. |
| Extracto | Movimientos del banco para una cuenta bancaria y un mes, cargados por archivo o digitados. |
| Partida conciliatoria | Movimiento que está en libros y no en el extracto, o al revés, y explica la diferencia entre ambos saldos. |
| Exógena | Información que la DIAN exige por medios magnéticos, organizada en formatos y conceptos por año gravable. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iniciar la contabilidad de una empresa con el PUC elegido (Priority: P1)

El administrador de una cooperativa recién creada abre «Contabilidad › Configuración inicial»,
elige el catálogo (Solidario, Comercial o uno propio importado), el nivel de las cuentas de
movimiento (5 o 6), la longitud de los códigos de esos niveles, el grupo NIIF al que pertenece la
empresa, el primer ejercicio y si exige cuatro ojos, y confirma. El sistema crea el plan de cuentas
completo hasta nivel 4 y deja el módulo listo para crear auxiliares.

**Why this priority**: sin plan de cuentas no hay contabilidad; hoy cada cooperativa tendría que
digitar cientos de cuentas antes del primer comprobante.

**Independent Test**: crear una cooperativa de prueba, iniciar con PUC Solidario y nivel de
movimiento 6; verificar que el plan contiene todas las cuentas del catálogo hasta nivel 4, ninguna
marcada como de movimiento, y que la elección queda bloqueada en cuanto exista una auxiliar.

**Acceptance Scenarios**:

1. **Given** una empresa sin contabilidad iniciada, **When** el administrador elige PUC Comercial
   y nivel de movimiento 5 y confirma, **Then** existen todas las cuentas del catálogo hasta nivel
   4 con su código, nombre, naturaleza y rubro NIIF; ninguna es de movimiento; la configuración
   muestra «Iniciada con PUC Comercial, movimiento en nivel 5» con fecha y responsable.
2. **Given** una contabilidad iniciada **sin** auxiliares ni movimientos, **When** el
   administrador cambia el catálogo o el nivel de movimiento, **Then** el sistema reemplaza el plan
   por el nuevo catálogo y deja registro de auditoría con la configuración anterior y la nueva.
3. **Given** una contabilidad con al menos una auxiliar o un movimiento, **When** intenta cambiar
   el catálogo, el nivel de movimiento o las longitudes, **Then** el sistema lo rechaza indicando
   qué lo impide (cuántas auxiliares, desde qué fecha hay movimientos).
4. **Given** una empresa sin contabilidad iniciada, **When** alguien intenta digitar un comprobante
   o parametrizar cuentas en Nómina, Cartera o cualquier módulo, **Then** el sistema le dice que la
   contabilidad no está iniciada y quién puede iniciarla; nada se guarda.
5. **Given** una versión nueva de un catálogo, **When** se despliega, **Then** las empresas ya
   iniciadas conservan su plan tal cual y se les ofrecen las cuentas nuevas para incorporarlas;
   ninguna cuenta se cambia ni se elimina sin que la empresa lo pida.
6. **Given** un archivo con el catálogo propio de la cooperativa (código, nombre, nivel, naturaleza,
   rubro NIIF), **When** el administrador lo importa, **Then** el sistema valida jerarquía,
   longitudes, unicidad, naturaleza y rubro, muestra los errores fila por fila, y sólo si no hay
   errores lo deja disponible como tercera plantilla para iniciar la contabilidad.

---

### User Story 2 - Parametrizar las cuentas auxiliares y sus reglas con integridad (Priority: P1)

El contador crea las auxiliares (nivel 5 y, si la empresa lo configuró, nivel 6) bajo una
subcuenta del catálogo y define cómo se comporta cada una: módulos habilitados, si exige tercero,
documento cruce, centro de costo o sucursal, y si está activa. Esas reglas gobiernan cualquier
movimiento que llegue a la cuenta, venga de la digitación o de un módulo. Una cuenta con
movimientos en el ejercicio no cambia de reglas.

**Why this priority**: es la garantía de integridad del módulo; sin reglas exigidas, los informes
por tercero, documento, centro de costo y sucursal no son confiables.

**Independent Test**: con la contabilidad iniciada, crear una auxiliar que exija tercero y esté
habilitada sólo para Nómina; comprobar que Cartera no puede parametrizarla, que Nómina sí, y que
tras registrarle un movimiento sus reglas quedan bloqueadas.

**Acceptance Scenarios**:

1. **Given** nivel de movimiento 6, **When** crea una cuenta de nivel 5 bajo una subcuenta,
   **Then** queda como cuenta de agrupación; **When** crea una de nivel 6 bajo ella, **Then** queda
   como cuenta de movimiento con sus reglas.
2. **Given** nivel de movimiento 5, **When** intenta crear una cuenta de nivel 6, **Then** el
   sistema lo rechaza explicando que la empresa trabaja con movimiento en nivel 5.
3. **Given** una auxiliar sin movimientos, **When** cambia «exige tercero» o sus módulos, **Then**
   el cambio se guarda y queda auditado con valores antes y después.
4. **Given** una auxiliar con movimientos en el ejercicio, **When** intenta cambiar cualquier
   regla, módulo, naturaleza o nivel, **Then** el sistema lo rechaza diciendo desde qué fecha tiene
   movimientos; sólo puede corregir el nombre e inactivarla o reactivarla.
5. **Given** una auxiliar habilitada sólo para Nómina, **When** alguien la elige en la
   parametrización de Cartera, **Then** es rechazada; **When** la elige en Nómina, **Then** es
   aceptada. **Given** una cuenta de agrupación, **When** cualquier módulo intenta parametrizarla,
   **Then** es rechazada con «no es de movimiento; use una de sus auxiliares».
6. **Given** una auxiliar referenciada por una parametrización de otro módulo o con movimientos,
   **When** intenta eliminarla, **Then** el sistema lo rechaza y muestra dónde está referenciada;
   sólo puede inactivarla.
7. **Given** un código que no empieza por el código de su cuenta padre, que no tiene la longitud
   configurada para su nivel o que ya existe, **When** guarda, **Then** el sistema lo rechaza
   señalando la regla incumplida.
8. **Given** una cuenta inactiva, **When** se busca desde la digitación o desde un módulo,
   **Then** no se ofrece; sus saldos históricos siguen apareciendo en los informes.

---

### User Story 3 - Digitar comprobantes manuales en una ventana guiada y validada (Priority: P1)

Un auxiliar contable abre «Comprobantes › Nuevo», elige el tipo, la fecha y la descripción, y
digita línea a línea: al elegir la cuenta, la ventana habilita o deshabilita tercero, documento
cruce, centro de costo y sucursal según las reglas de esa cuenta, explica en cada campo qué se
espera y por qué, y muestra en todo momento débitos, créditos y diferencia. Guarda un borrador y,
cuando cuadra, quien tiene el permiso de contabilizar lo contabiliza: recibe número y queda
inmutable. Si la empresa exige cuatro ojos, esa persona debe ser distinta de quien lo registró.

**Why this priority**: es la operación diaria del módulo y donde se decide la calidad del dato.

**Independent Test**: registrar un comprobante de 20 líneas usando sólo el teclado, con cuentas que
exigen tercero, documento y centro de costo; verificar que ninguna línea incompleta pasa, que la
diferencia se ve en vivo y que al contabilizar el comprobante ya no se puede editar.

**Acceptance Scenarios**:

1. **Given** una línea con una cuenta que exige tercero, **When** el usuario sale del campo tercero
   vacío, **Then** la línea marca el error con el motivo y «Contabilizar» permanece deshabilitado.
2. **Given** una cuenta que no maneja centro de costo, **When** se elige, **Then** ese campo queda
   deshabilitado y no se guarda valor en él; la sucursal viene siempre propuesta (la principal, o
   la del usuario si tiene sucursales asignadas) y, si la cuenta «exige sucursal», el usuario debe
   confirmarla o cambiarla a mano antes de salir de la línea.
3. **Given** débitos distintos de créditos, **Then** el pie muestra la diferencia y no se puede
   contabilizar; sí se puede guardar como borrador.
4. **Given** una cuenta de agrupación, **When** el usuario la escribe, **Then** el buscador no la
   ofrece y el sistema la rechaza con «1105 no es de movimiento; use una de sus auxiliares».
5. **Given** una fecha en período cerrado, fuera del ejercicio o posterior a hoy, **When** guarda,
   **Then** el sistema lo rechaza señalando el período.
6. **Given** un comprobante contabilizado, **When** alguien intenta editarlo o eliminarlo, **Then**
   no existe tal acción; sólo «Anular», que crea el comprobante de reversión con referencia cruzada
   y motivo obligatorio, dentro de un período abierto.
7. **Given** la grilla de líneas, **When** el usuario avanza con Tab/Enter, **Then** recorre los
   campos en orden, puede repetir tercero, centro y sucursal de la línea anterior, duplicar una
   línea y pedir que la última línea tome la diferencia, sin usar el ratón.
8. **Given** un tercero inactivo o eliminado, **When** se digita, **Then** no se admite; si el
   usuario tiene permiso de crear personas, puede crear el tercero desde el buscador sin salir del
   comprobante.
9. **Given** dos usuarios que contabilizan al tiempo con el mismo tipo, **Then** reciben números
   consecutivos distintos; no hay duplicados ni huecos.
10. **Given** un tipo de comprobante reservado a un módulo (p. ej. el de Nómina), **When** alguien
    lo elige en la digitación manual, **Then** no está disponible.
11. **Given** errores en varias líneas, **When** intenta contabilizar, **Then** ve la lista de
    errores y cada uno lo lleva a la línea y al campo.
12. **Given** una empresa con cuatro ojos activo, **When** quien registró el borrador intenta
    contabilizarlo aunque tenga el permiso, **Then** el sistema lo rechaza indicando que debe
    hacerlo otra persona; **When** otra persona con el permiso lo contabiliza, **Then** el
    comprobante guarda quién registró y quién contabilizó.

---

### User Story 4 - Los módulos contabilizan en línea por un solo contrato (Priority: P1)

Cuando Nómina aprueba una liquidación, Cartera desembolsa o recauda, Inventario factura,
Tesorería gira un cheque o CDT liquida intereses, el módulo entrega a Contabilidad un comprobante
completo (tipo del módulo, fecha, líneas con cuenta, tercero, documento, centro y sucursal, y la
referencia al documento de origen). Contabilidad aplica **exactamente las mismas reglas** que a la
digitación; si rechaza, la operación del módulo falla completa; si acepta, el comprobante queda
contabilizado de inmediato con su origen, y en Contabilidad sólo se consulta. Corregirlo es
reversarlo **desde el módulo**.

**Why this priority**: es el punto 2 y 3 de la solicitud, y hoy es donde se rompe la integridad
(cinco módulos, quince caminos, ninguna regla común). Nómina va primero porque es el único módulo
en producción.

**Independent Test**: aprobar una nómina con un concepto parametrizado a una cuenta que exige
tercero; verificar que el comprobante NM queda contabilizado con el empleado como tercero en cada
línea, que en Contabilidad se ve de sólo lectura con «Ver en Nómina», y que reversar la nómina
genera la reversión referenciada.

**Acceptance Scenarios**:

1. **Given** una liquidación de nómina aprobada, **Then** existe un comprobante NM contabilizado,
   cuadrado, con origen «Nómina», referencia a la corrida, y visible en Contabilidad de sólo lectura
   con enlace al origen.
2. **Given** un concepto de nómina parametrizado a una cuenta de agrupación, inactiva o no
   habilitada para Nómina, **When** aprueban la liquidación, **Then** la aprobación falla nombrando
   el concepto y la cuenta; no queda corrida aprobada ni comprobante a medias.
3. **Given** una cuenta que exige tercero (o documento, centro o sucursal), **When** el módulo
   envía una línea sin ese dato, **Then** Contabilidad rechaza y el módulo muestra qué línea y qué
   regla; nada queda guardado.
4. **Given** un comprobante con origen Nómina, **When** alguien lo abre en Contabilidad, **Then** no
   tiene «Anular» ni edición; **When** reversan la liquidación desde Nómina, **Then** aparece el
   comprobante de reversión referenciado al original y el saldo neto de esas cuentas queda en cero.
5. **Given** un período contable cerrado, **When** un módulo intenta contabilizar con fecha en ese
   período, **Then** el rechazo llega al usuario del módulo con el período señalado.
6. **Given** el código del ERP, **Then** no existe ningún camino que escriba movimientos contables
   fuera del contrato único (lo comprueba una prueba automática).
7. **Given** Cartera, Inventario, Tesorería y CDT, **When** ejecutan cada operación que hoy
   contabiliza, **Then** producen su comprobante por el contrato, con su tipo de comprobante y con
   el tercero de la operación (asociado, cliente, proveedor, banco) en cada línea que lo exija.
8. **Given** una cuenta de aportes parametrizada para Nómina que exige tercero, **When** una EPS
   del catálogo no tiene persona vinculada, **Then** la parametrización lo rechaza nombrando la EPS;
   **When** la EPS queda vinculada y se aprueba la nómina, **Then** las líneas de aportes llevan a
   la EPS como tercero y su estado de cuenta muestra lo causado y lo pagado.

---

### User Story 5 - Consultar y profundizar: del saldo a la línea del comprobante (Priority: P2)

El contador o el revisor fiscal abre el libro auxiliar interactivo, filtra por rango de fechas,
cuenta o rama del plan, tercero, documento, centro de costo, sucursal, tipo de comprobante, módulo
origen o usuario, y ve saldo inicial, débitos, créditos y saldo final por nivel; abre una clase
hasta su auxiliar, de la auxiliar a los terceros, del tercero a los documentos, del documento a los
comprobantes y de ahí a la línea. Cualquier vista se exporta a Excel, PDF o Word con los filtros
aplicados. Los informes formales (balance de prueba, libros oficiales, estados financieros NIIF,
saldos diarios promedio) salen de los mismos movimientos.

**Why this priority**: es lo que hace auditable la contabilidad; sin profundización, cada duda
termina en una consulta directa a la base.

**Independent Test**: con movimientos de dos módulos y de digitación, partir del balance de prueba
en 1105 y llegar a la línea de un comprobante concreto en cuatro clics; exportar la vista
intermedia por terceros a Word y comprobar que trae los mismos totales.

**Acceptance Scenarios**:

1. **Given** movimientos del mes, **When** abre el libro auxiliar por la cuenta 1105, **Then** ve
   saldo inicial, débitos, créditos y saldo final, y puede abrir 110505 → auxiliares → terceros →
   documentos → comprobantes → líneas, en cuatro clics o menos.
2. **Given** cualquier informe o consulta en pantalla, **When** exporta, **Then** obtiene el archivo
   en Excel, PDF o Word con la empresa, el NIT, los filtros aplicados, el período, quién lo generó
   y cuándo, y los mismos totales que la pantalla.
3. **Given** el saldo de una cuenta en el balance de prueba, **Then** es exactamente la suma de sus
   movimientos en el libro auxiliar; la suma de débitos y de créditos de todo el balance es igual.
4. **Given** los filtros combinados (p. ej. tercero + centro de costo + módulo origen + rango),
   **Then** todos aplican a la vez sobre la misma vista y sobre su exportación.
5. **Given** un usuario con acceso restringido a ciertas sucursales, **Then** consultas e informes
   sólo muestran movimientos de esas sucursales.
6. **Given** el cierre del ejercicio contabilizado, **When** consulta el estado de resultados del
   año, **Then** el comprobante de cierre no se incluye salvo que lo pida explícitamente.
7. **Given** un tercero, **When** abre su estado de cuenta, **Then** ve sus movimientos por cuenta y
   los documentos cruce con saldo pendiente.
8. **Given** una cuenta y un mes, **When** pide el saldo diario promedio, **Then** lo obtiene
   calculado desde los movimientos, día por día, sin mantener ninguna tabla aparte.

---

### User Story 6 - Cerrar períodos y el ejercicio (Priority: P2)

Cada mes, el contador cierra el período: el sistema comprueba que no queden borradores y bloquea
nuevos movimientos con fecha en ese mes, venga de donde venga. Con motivo y permiso propio se puede
reabrir. Al terminar el año, con los doce meses cerrados, el cierre del ejercicio cancela ingresos,
gastos y costos contra la cuenta de resultado del ejercicio y deja el año siguiente listo con los
saldos de balance.

**Why this priority**: sin cierres no hay estados financieros firmes ni control sobre
modificaciones posteriores; después de US3 y US4 porque necesita movimientos.

**Independent Test**: cerrar un mes con un borrador pendiente (debe rechazar), contabilizar el
borrador, cerrar, intentar una nómina con fecha en ese mes (debe rechazar), reabrir con motivo y
verificar el registro de auditoría.

**Acceptance Scenarios**:

1. **Given** un mes con comprobantes en borrador, **When** intenta cerrarlo, **Then** el sistema lo
   rechaza y lista los borradores.
2. **Given** un mes cerrado, **When** la digitación o un módulo intentan un comprobante con fecha
   en él, **Then** rechazo con el período señalado.
3. **Given** un mes cerrado, **When** alguien con el permiso de reabrir indica el motivo, **Then**
   el período vuelve a abierto y queda auditado quién, cuándo y por qué; sin permiso o sin motivo,
   no se reabre.
4. **Given** los doce meses cerrados, **When** cierra el ejercicio, **Then** existe un comprobante
   de cierre con el resultado del ejercicio llevado a la cuenta configurada, y el balance de prueba
   del primer día del año siguiente muestra sólo saldos de balance y cero en resultados.
5. **Given** el ejercicio anterior abierto, **When** intenta cerrar el actual, **Then** rechazo.
6. **Given** un ejercicio cerrado, **When** se reabre con permiso y motivo, **Then** el comprobante
   de cierre queda reversado y los meses vuelven a poder reabrirse uno a uno.

---

### User Story 7 - Todo queda auditado y cada acción tiene su permiso (Priority: P2)

El administrador asigna por rol qué puede hacer cada quien: consultar, parametrizar el plan,
iniciar la contabilidad, registrar borradores, contabilizar, anular, cerrar y reabrir períodos,
exportar informes, operar cada satélite y consultar la auditoría. Cada acción de escritura guarda
quién, cuándo, qué y los valores antes y después; cada exportación guarda informe, filtros y
formato; y cada vez que cualquier usuario abre cualquier opción del ERP queda un registro. El
administrador y el auditor lo consultan con filtros y lo exportan.

**Why this priority**: es el punto 8 de la solicitud y la exigencia regulatoria; después de las
historias que generan los eventos.

**Independent Test**: con un rol que sólo registra borradores, intentar contabilizar (botón ausente
y servidor rechaza); abrir cinco opciones distintas y verlas en la consulta de auditoría de
accesos; cambiar una regla de cuenta y ver antes/después.

**Acceptance Scenarios**:

1. **Given** cualquier usuario, **When** abre cualquier opción del ERP (no sólo de contabilidad),
   **Then** queda un registro con usuario, cooperativa, opción, momento y origen, visible para el
   administrador y el auditor.
2. **Given** un cambio de configuración de una cuenta, del plan o de un tipo de comprobante,
   **Then** el registro guarda los valores antes y después.
3. **Given** un rol sin permiso de contabilizar, **Then** puede guardar borradores; el botón
   «Contabilizar» no aparece y, si lo intenta por otro medio, el servidor lo rechaza.
4. **Given** un rol de sólo lectura (p. ej. revisor fiscal), **Then** consulta y exporta todo y no
   puede escribir nada.
5. **Given** una exportación de informe, **Then** el registro incluye el informe, los filtros y el
   formato.
6. **Given** la auditoría, **When** se consulta por usuario, opción, entidad o rango de fechas,
   **Then** responde con los eventos y permite exportarlos.
7. **Given** una empresa que activa o desactiva cuatro ojos, **Then** el cambio queda auditado y
   aplica a los comprobantes contabilizados desde ese momento.

---

### User Story 8 - Conciliar las cuentas bancarias (Priority: P3)

El tesorero marca qué cuentas de movimiento son bancarias (banco y número de cuenta), carga cada
mes el extracto por archivo o lo digita, pide al sistema que proponga coincidencias con los
movimientos contables de esa cuenta, confirma o rechaza, deja identificadas las partidas
conciliatorias en ambos sentidos, lleva a un borrador de comprobante las notas del banco que
faltan en libros, obtiene el informe que explica la diferencia entre saldo en libros y saldo en
extracto hasta cero, y cierra la conciliación del mes.

**Why this priority**: es control interno obligatorio sobre el efectivo; necesita movimientos
contabilizados y períodos (US3–US6).

**Independent Test**: con diez movimientos en una cuenta bancaria y un extracto con ocho
coincidencias, una nota bancaria sin contabilizar y un cheque no cobrado, conciliar y obtener el
informe con diferencia cero explicada.

**Acceptance Scenarios**:

1. **Given** el extracto cargado y los movimientos del mes, **When** pide conciliar
   automáticamente, **Then** las coincidencias por valor y referencia, o por valor y fecha dentro
   de la tolerancia de días configurada, quedan propuestas y el usuario las confirma o rechaza una
   a una o en bloque.
2. **Given** partidas sin pareja, **Then** aparecen clasificadas («en libros, no en banco» y «en
   banco, no en libros») y el informe de conciliación muestra saldo en libros, saldo en extracto y
   las partidas que explican la diferencia hasta llegar a cero.
3. **Given** una nota bancaria sin contabilizar, **When** elige «Contabilizar», **Then** se abre un
   borrador de comprobante prellenado (cuenta bancaria, fecha, valor, detalle del banco) para
   completar la contrapartida por la digitación normal.
4. **Given** una conciliación cerrada, **When** aparece un movimiento nuevo con fecha de ese mes
   (por reapertura del período), **Then** la conciliación se marca desactualizada y debe reabrirse
   con permiso y motivo.
5. **Given** un movimiento ya conciliado, **When** alguien lo desconcilia, **Then** exige motivo y
   queda auditado.
6. **Given** un extracto cargado dos veces, **Then** las líneas repetidas se detectan y no se
   duplican.
7. **Given** una cuenta bancaria nueva, **When** el tesorero carga el primer extracto del banco,
   **Then** define el mapeo (columna de fecha y su formato, referencia, descripción, valor con signo
   o débito/crédito) viendo una vista previa de las primeras filas ya interpretadas, lo guarda con
   la cuenta, y los meses siguientes el archivo se interpreta solo; un archivo que no encaja con el
   mapeo (columna faltante, fecha ilegible) se rechaza señalando la fila y la columna.

---

### User Story 9 - Presupuestar y seguir la ejecución (Priority: P3)

El contador registra el presupuesto del ejercicio por cuenta de movimiento (y, si quiere, por
sucursal y centro de costo), mes a mes o por distribución, puede copiarlo del año anterior con un
porcentaje de ajuste, lo aprueba, y consulta la ejecución: presupuestado, ejecutado, variación y
porcentaje, por cuenta y agregado hacia arriba por niveles, con profundización al libro auxiliar y
exportación. Las modificaciones después de aprobado quedan como versiones con motivo.

**Why this priority**: gestión, no registro; necesita el plan y los movimientos.

**Independent Test**: presupuestar dos cuentas de gasto, contabilizar movimientos en ellas y
obtener la ejecución mensual y acumulada con la variación correcta y la profundización a las
líneas.

**Acceptance Scenarios**:

1. **Given** el presupuesto del año, **When** consulta la ejecución a un mes, **Then** ve por cuenta
   y por nivel presupuestado, ejecutado, variación y % de ejecución, del mes y acumulado, y puede
   abrir cualquier valor ejecutado hasta el libro auxiliar.
2. **Given** una cuenta de agrupación, **When** intenta presupuestarla, **Then** rechazo: el
   presupuesto se registra en cuentas de movimiento y se agrega hacia arriba.
3. **Given** un presupuesto aprobado, **When** modifica un valor, **Then** queda una versión con
   motivo y responsable; el informe puede mostrar el presupuesto inicial y el vigente.
4. **Given** «copiar del año anterior con +5 %», **Then** los valores quedan redondeados a pesos y
   el usuario puede ajustarlos antes de aprobar.
5. **Given** distribución «igual en doce meses» de un total anual, **Then** la diferencia de
   redondeo cae en el último mes.

---

### User Story 10 - Impuestos y certificados de retención (Priority: P3)

El contador parametriza las cuentas de impuestos: a cada cuenta de movimiento de retención en la
fuente, IVA, ICA o GMF le asocia tipo de impuesto, concepto, tarifa y la exigencia de base
gravable. Desde entonces toda línea en esas cuentas —digitada o enviada por un módulo— trae base y
el sistema comprueba la tarifa. Con eso obtiene los informes de retenciones practicadas, IVA
generado y descontable, ICA y GMF por período, el resumen por renglón de cada formulario, y genera
los certificados de retención (fuente, IVA, ICA) por tercero y año con consecutivo, fecha de
expedición, impresión y envío por correo.

**Why this priority**: obligación fiscal mensual y anual; depende de bases por línea (US3, US4).

**Independent Test**: parametrizar una cuenta de retefuente al 4 %, contabilizar tres líneas de
dos terceros con base, generar los certificados del año y verificar valores, consecutivos y PDF.

**Acceptance Scenarios**:

1. **Given** una cuenta de retefuente parametrizada al 4 %, **When** digita una línea con base
   1.000.000 y valor 40.000, **Then** la acepta; con valor 35.000, **Then** avisa la diferencia y la
   rechaza si supera la tolerancia configurada; sin base, **Then** la rechaza.
2. **Given** un año gravable, **When** genera certificados de retención en la fuente, **Then**
   obtiene uno por tercero con retenciones, con concepto, base, tarifa, valor retenido,
   consecutivo y fecha de expedición; reimprimir no cambia el consecutivo.
3. **Given** un certificado ya expedido, **When** la contabilidad de ese año cambia (reversión),
   **Then** el certificado aparece desactualizado y se puede reexpedir con número nuevo dejando el
   anterior anulado.
4. **Given** el resumen por formulario (p. ej. retención mensual), **Then** los valores por renglón
   salen de las cuentas y bases del período y se exportan.
5. **Given** Nómina (retención a empleados) o Inventario (IVA de facturas), **When** envían líneas
   a cuentas de impuesto, **Then** aplican las mismas reglas de base y tarifa.
6. **Given** un certificado, **When** pide enviarlo por correo, **Then** llega al correo del tercero
   registrado en Personas y queda auditado el envío.

---

### User Story 11 - Información exógena para la DIAN (Priority: P3)

El contador parametriza por año gravable los formatos de medios magnéticos que la cooperativa
debe reportar (pagos, retenciones, IVA, ingresos, cuentas por cobrar y por pagar, entre otros),
con los conceptos y las cuentas que alimentan cada uno, la cuantía mínima y la regla de menores
cuantías; genera la información por tercero con sus datos de identificación, revisa las
inconsistencias, corrige los terceros en Personas, regenera, y exporta en el formato que acepta el
prevalidador de la DIAN y en Excel para revisión.

**Why this priority**: obligación anual con sanción; depende de todo el año contabilizado con
terceros correctos.

**Independent Test**: parametrizar el formato de pagos con dos conceptos, contabilizar movimientos
a cinco terceros (uno bajo la cuantía mínima y uno sin dirección), generar, ver las
inconsistencias, y exportar el archivo del prevalidador y el Excel.

**Acceptance Scenarios**:

1. **Given** un formato parametrizado, **When** genera el año, **Then** por cada tercero y concepto
   obtiene los valores desde las cuentas indicadas; los terceros bajo la cuantía mínima se agrupan
   bajo la identificación de menores cuantías.
2. **Given** terceros con datos incompletos o inválidos, **Then** la generación entrega la lista de
   inconsistencias con enlace a cada persona; las bloqueantes (documento inválido) impiden la
   exportación al formato DIAN y las demás (dirección, municipio) se avisan.
3. **Given** una generación previa, **When** regenera, **Then** reemplaza la versión de trabajo; las
   versiones exportadas se conservan con fecha, usuario y archivo.
4. **Given** un año nuevo con formatos o conceptos distintos, **Then** se parametrizan sin cambios
   de programa: formatos y conceptos son configuración por año.
5. **Given** el archivo exportado, **When** se carga en el prevalidador de la DIAN en QA, **Then**
   pasa la validación de estructura.
6. **Given** una empresa recién iniciada, **When** el contador abre exógena para 2026, **Then**
   encuentra todos los formatos y conceptos de la resolución vigente ya cargados, con las cuentas
   propuestas donde aplica; marca «no aplica» los que no le corresponden y sólo los demás generan.
7. **Given** un año gravable cuya semilla aún no existe, **When** el contador lo abre, **Then**
   puede copiar la parametrización del año anterior y ajustarla; cuando la semilla del año llega,
   se ofrece sin pisar lo que ya parametrizó.

---

### User Story 12 - Activos fijos y diferidos: depreciación y amortización (Priority: P3)

El contador registra cada activo fijo (código, nombre, cuentas de activo, depreciación acumulada
y gasto, centro de costo, sucursal, proveedor, fecha y documento de compra, valor, valor residual,
vida útil en meses) y cada cargo diferido o intangible (amortización), y cada mes ejecuta el
cálculo: el sistema genera un solo comprobante por período con origen «Activos» por el contrato
único, reversable desde la misma opción. Consulta el inventario de activos con valor original,
depreciación acumulada, valor neto en libros y proyección, revisa vida útil o valor residual con
efecto prospectivo, y da de baja activos con su comprobante.

**Why this priority**: cierre mensual completo bajo NIIF; usa el contrato y los períodos.

**Independent Test**: registrar un activo de 60 meses con valor residual, ejecutar dos meses de
depreciación, reversar el segundo, cambiar la vida útil y verificar que las cuotas futuras se
recalculan sin tocar las pasadas.

**Acceptance Scenarios**:

1. **Given** un activo con vida útil de 60 meses y valor residual, **When** ejecuta la depreciación
   del mes, **Then** existe un comprobante cuadrado (gasto contra depreciación acumulada) con el
   activo como referencia; ejecutar de nuevo el mismo mes no genera otro.
2. **Given** un período cerrado, **When** intenta ejecutar, **Then** rechazo; **Given** una
   ejecución reversada, **Then** el comprobante de reversión existe y el período vuelve a estar
   pendiente para esos activos.
3. **Given** un cambio de vida útil o valor residual, **Then** las cuotas futuras se recalculan
   sobre el valor neto en libros; las ya contabilizadas no cambian; el cambio queda auditado.
4. **Given** la baja o venta de un activo, **Then** se genera el comprobante que cancela costo y
   depreciación acumulada contra la cuenta indicada, y el activo queda retirado con fecha y motivo.
5. **Given** un activo totalmente depreciado, **Then** no genera más cuotas y sigue en el
   inventario hasta su baja.
6. **Given** un cargo diferido con plazo en meses, **Then** la amortización mensual sigue las mismas
   reglas de ejecución única por período y reversión.

---

### User Story 13 - Cargar los saldos de apertura de una cooperativa que ya opera (Priority: P2)

El contador de una cooperativa que llega desde SOLIDO carga, una sola vez, los saldos con que
arranca en el ERP: importa un archivo (o digita) con cuenta, tercero, documento cruce, centro de
costo, sucursal y débito o crédito, el sistema valida cada fila con las mismas reglas de cuenta que
cualquier comprobante, muestra los errores fila por fila, exige que cuadre, y al contabilizarlo el
primer balance de prueba refleja los activos, pasivos y patrimonio reales, con el detalle por
tercero y por documento en cartera y proveedores.

**Why this priority**: sin esto ninguna cooperativa existente puede arrancar; va después del
núcleo porque usa el plan, las reglas de cuenta, la digitación y el contrato.

**Independent Test**: con la contabilidad iniciada y las auxiliares creadas, importar un archivo
de apertura de 200 filas con dos errores (cuenta de agrupación y tercero inexistente), corregirlo,
contabilizarlo y comprobar que el balance de prueba del primer día del ejercicio coincide con los
totales del archivo y que el estado de cuenta de un tercero muestra sus documentos pendientes.

**Acceptance Scenarios**:

1. **Given** un archivo de apertura con filas válidas, **When** lo importa, **Then** queda un
   borrador de comprobante de tipo «Apertura» con esas líneas, fechado el día anterior al primer
   período, listo para revisar y contabilizar con los permisos normales.
2. **Given** filas con cuenta de agrupación, tercero inexistente, documento faltante donde la cuenta
   lo exige o valor con más de dos decimales, **When** importa, **Then** ve cada error con su fila y
   nada queda a medias; corrige el archivo y vuelve a importar.
3. **Given** un archivo que no cuadra, **When** intenta contabilizar, **Then** rechazo con la
   diferencia; sí puede guardarlo como borrador.
4. **Given** la apertura contabilizada, **When** intenta cargar otra, **Then** rechazo: primero debe
   reversar la existente; **When** la reversa y carga otra, **Then** ambas quedan referenciadas y
   auditadas.
5. **Given** la apertura contabilizada, **When** consulta el balance de prueba del primer mes,
   **Then** los saldos de apertura aparecen como saldo inicial y no como movimiento del mes, y el
   estado de cuenta de cada tercero muestra sus documentos con saldo pendiente.

---

### Edge Cases

- Un comprobante con una sola línea, con líneas de valor cero, o con débito y crédito en la misma
  línea: se rechaza antes de guardar.
- Un módulo envía un comprobante descuadrado por redondeo: se rechaza; el módulo debe cuadrar.
- Dos usuarios editan el mismo borrador: gana el primero en guardar; el segundo ve que cambió y
  recarga.
- Se intenta anular un comprobante de reversión: no se permite (se reversa el original una sola
  vez); tampoco se puede reversar dos veces el mismo comprobante.
- La fecha de la reversión cae en un período cerrado: la reversión se fecha en el primer período
  abierto y lo dice.
- Un tercero se elimina o inactiva después de tener movimientos: los movimientos y los informes lo
  siguen mostrando; sólo no se admite en movimientos nuevos.
- La empresa tiene un solo centro de costo: la digitación lo propone automáticamente en las cuentas
  que lo exigen; la sucursal principal se propone siempre, tenga la empresa una o varias.
- La sucursal principal se inactiva: el sistema exige elegir otra principal antes de permitirlo.
- Un usuario tiene sucursales asignadas: sólo puede mover y consultar esas sucursales.
- Se cambia el nombre de una cuenta con movimientos: se permite y queda auditado; los informes
  muestran el nombre vigente.
- Se inactiva una cuenta con saldo: se permite (deja de recibir movimientos); el saldo sigue en los
  informes hasta que se reclasifique con un comprobante.
- Una parametrización de otro módulo apunta a una cuenta que luego se inactiva: el módulo falla al
  contabilizar nombrando la cuenta; hay una consulta que lista parametrizaciones inválidas.
- El almacén de auditoría está momentáneamente indisponible: el evento no se descarta en silencio
  (se reintenta y el sistema avisa); ninguna operación contable se da por auditada sin serlo.
- El catálogo trae una subcuenta con naturaleza contraria a su clase (p. ej. depreciación acumulada
  dentro de Activo): la naturaleza es la del catálogo, y las auxiliares la heredan.
- Un ejercicio se inicia sin haber creado sus períodos: los períodos se crean con el ejercicio;
  nunca se puede contabilizar en un mes sin período.
- Se intenta iniciar la contabilidad con un ejercicio anterior al año actual con meses ya
  vencidos: se permite (arranque tardío), y los meses se crean abiertos.
- Cuatro ojos activo y una sola persona con permiso de contabilizar: el borrador queda sin poder
  contabilizarse y el sistema lo dice; la salida es dar el permiso a otra persona o desactivar la
  regla (auditado), nunca saltarla.
- Un catálogo propio importado con un código de nivel 4 que no tiene padre de nivel 3: la
  importación falla señalando la fila; nada queda a medias.
- El extracto trae dos movimientos idénticos (mismo valor, fecha y referencia): se cargan ambos y
  cada uno se concilia contra un movimiento contable distinto; ninguno se empareja dos veces.
- Se reversa un comprobante ya conciliado: el movimiento original y su reversión quedan como
  partidas conciliatorias hasta que se concilien entre sí.
- Un activo se registra con fecha de compra en un período cerrado: se admite (el activo existía) y
  la depreciación empieza en el primer período abierto, acumulando lo pendiente en la primera cuota
  con aviso.
- La tarifa de una cuenta de impuesto cambia por norma: se registra una vigencia nueva; las líneas
  anteriores conservan la tarifa que tenían.
- Un tercero de exógena cambia de documento durante el año: la información se reporta con el
  documento vigente al generar, y el cambio consta en la auditoría de Personas.
- La apertura trae saldos de resultados (ingresos o gastos acumulados) porque la cooperativa arranca
  a mitad de año: se admiten, y el cierre del ejercicio los cancela junto con los del año.
- Ya hay movimientos contabilizados cuando se intenta cargar la apertura: se admite, porque la
  apertura se fecha antes de todos; los saldos iniciales de los informes se recalculan solos.
- Una entidad institucional (EPS, banco) cambia de persona vinculada: los movimientos ya
  contabilizados conservan el tercero con que nacieron; sólo los nuevos usan el vínculo vigente, y
  el cambio queda auditado.
- Dos entidades de catálogo vinculan a la misma persona (p. ej. una caja que también es banco): se
  admite; el estado de cuenta del tercero muestra todo junto.

## Requirements *(mandatory)*

### Functional Requirements

**Catálogos e inicialización**

- **FR-001**: El sistema MUST traer dos catálogos —PUC Solidario (desde el catálogo de la
  Supersolidaria bajo NIIF) y PUC Comercial (desde el Decreto 2650)— con todas las cuentas hasta
  nivel 4, cada una con código, nombre, naturaleza y rubro de estado financiero NIIF, versionados
  y de sólo lectura para las empresas; su contenido MUST quedar validado por el contador de la
  cooperativa en QA antes de usarse en producción, y esa validación queda registrada.
- **FR-002**: El administrador MUST poder importar un catálogo propio (archivo con código, nombre,
  nivel, naturaleza y rubro NIIF hasta nivel 4); el sistema MUST validar jerarquía, longitudes,
  unicidad, naturaleza y rubro, reportar los errores fila por fila, no dejar nada a medias, y
  ofrecerlo como plantilla adicional sólo cuando está sin errores.
- **FR-003**: El administrador MUST poder iniciar la contabilidad de la empresa eligiendo
  catálogo, nivel de movimiento (5 o 6), longitud de los códigos de nivel 5 y 6, grupo NIIF (1, 2
  o 3), primer ejercicio, cuenta de resultado del ejercicio, sucursal principal y si exige cuatro
  ojos; al confirmar, el sistema crea el plan completo hasta nivel 4 y los períodos del ejercicio.
- **FR-004**: El sistema MUST permitir cambiar catálogo, nivel de movimiento o longitudes sólo
  mientras no exista ninguna auxiliar ni ningún movimiento, y MUST auditar el cambio con los
  valores anteriores; la regla de cuatro ojos MUST poder cambiarse en cualquier momento, auditada,
  con efecto hacia adelante.
- **FR-005**: Mientras la contabilidad no esté iniciada, el sistema MUST impedir digitar
  comprobantes, contabilizar desde módulos y parametrizar cuentas en cualquier módulo, indicando
  quién puede iniciarla.
- **FR-006**: Una versión nueva de un catálogo MUST NOT alterar el plan de las empresas ya
  iniciadas; el sistema MUST ofrecerles las cuentas nuevas para incorporarlas a voluntad.
- **FR-007**: Las cuentas del catálogo (niveles 1 a 4) MUST NOT poder editarse ni eliminarse por la
  empresa; sí inactivarse y reactivarse para no ofrecerlas en los buscadores.

**Plan de cuentas y auxiliares**

- **FR-008**: Una cuenta de nivel N MUST crearse bajo una cuenta existente de nivel N−1 cuyo código
  sea prefijo del suyo, con la longitud configurada para su nivel y código único en la empresa.
- **FR-009**: El sistema MUST tratar como cuenta de movimiento sólo las del nivel de movimiento
  configurado, y MUST rechazar movimientos y parametrizaciones sobre cualquier otra.
- **FR-010**: Cada auxiliar MUST tener: nombre, módulos habilitados (Contabilidad, Nómina, Cartera,
  Inventario, Tesorería, CDT/Ahorros, Activos), exige tercero, exige documento cruce, exige centro
  de costo, exige sucursal (= la sucursal debe elegirse explícitamente; sin esa regla se toma la
  propuesta), y estado activa/inactiva; opcionalmente MUST poder marcarse como cuenta bancaria
  (banco y número de cuenta) o como cuenta de impuesto (ver FR-065).
- **FR-011**: Naturaleza y rubro NIIF MUST heredarse de la cuenta padre y no ser editables en las
  auxiliares.
- **FR-012**: Si una cuenta tiene movimientos en el ejercicio en curso, el sistema MUST bloquear
  cambios en sus reglas, módulos, naturaleza y nivel, e indicar desde qué fecha tiene movimientos;
  MUST seguir permitiendo corregir el nombre e inactivar/reactivar.
- **FR-013**: El sistema MUST rechazar la eliminación de una cuenta con movimientos (de cualquier
  ejercicio) o referenciada por una parametrización, mostrando dónde está referenciada.
- **FR-014**: Un único conjunto de reglas MUST aplicarse a toda línea contable, provenga de la
  digitación o de cualquier módulo: cuenta de movimiento, activa, habilitada para el módulo origen,
  sucursal siempre presente y vigente, tercero/documento/centro presentes cuando la cuenta los
  exige y ausentes cuando no los admite, base gravable cuando la cuenta es de impuesto, tercero y
  centro vigentes.
- **FR-015**: Los buscadores de cuentas MUST buscar por código o nombre y ofrecer sólo cuentas de
  movimiento activas habilitadas para el contexto (módulo) en que se usan.
- **FR-016**: Las pantallas de parametrización de los módulos (cuentas por concepto de nómina,
  cuentas de producto e IVA, cuentas de cartera, ahorros y CDT, tesorería, activos) MUST validar la
  cuenta al guardar según FR-009 y FR-014, y MUST mostrar las reglas de la cuenta elegida.
- **FR-017**: El sistema MUST ofrecer una consulta de parametrizaciones inválidas (cuentas de
  agrupación, inactivas o no habilitadas para el módulo) para las bases ya sembradas.
- **FR-018**: La ficha de cada cuenta MUST mostrar su historial de cambios (quién, cuándo, antes y
  después).

**Tipos de comprobante y períodos**

- **FR-019**: Cada tipo de comprobante MUST tener código de catálogo, nombre, uso (manual,
  reservado a un módulo, cierre, activos o apertura) y estado activo; los tipos reservados MUST
  venir sembrados y MUST NOT poder usarse en la digitación manual corriente.
- **FR-020**: El número del comprobante MUST asignarse al contabilizar, ser consecutivo por tipo,
  sin duplicados ni huecos aun con usuarios concurrentes; un comprobante anulado MUST conservar su
  número.
- **FR-021**: El ejercicio MUST dividirse en períodos mensuales, cada uno abierto o cerrado; la
  fecha del comprobante MUST determinar su período y MUST caer en un período abierto y no ser
  posterior al día actual; la única excepción es el comprobante de apertura (FR-084).
- **FR-022**: El cierre mensual MUST exigir que no queden borradores del período; la reapertura
  MUST exigir permiso propio y motivo, quedar auditada y marcar como desactualizadas las
  conciliaciones bancarias cerradas de ese mes.
- **FR-023**: El cierre del ejercicio MUST exigir los doce meses cerrados y el ejercicio anterior
  cerrado, y MUST generar el comprobante de cierre (ingresos, gastos y costos contra la cuenta de
  resultado del ejercicio) con un tipo de comprobante reservado; la reapertura del ejercicio MUST
  reversarlo y quedar auditada.
- **FR-024**: Los estados de resultados de un ejercicio MUST excluir por defecto el comprobante de
  cierre, con opción de incluirlo.

**Comprobantes manuales**

- **FR-025**: Un comprobante MUST tener tipo, fecha, descripción, número (automático), origen y al
  menos dos líneas; cada línea MUST tener cuenta, sucursal, débito **o** crédito mayor que cero
  (nunca ambos ni ninguno), y opcionalmente tercero, documento cruce (tipo y número), centro de
  costo, detalle y base gravable, según lo que exija la cuenta.
- **FR-026**: La digitación MUST habilitar o deshabilitar tercero, documento, centro y base según
  la cuenta de cada línea, MUST proponer la sucursal en toda línea (la principal o la del usuario)
  y exigir confirmarla a mano cuando la cuenta lo pide, MUST mostrar ayuda por campo con la regla
  que aplica, una guía de pasos en la ventana, y débitos, créditos y diferencia en tiempo real.
- **FR-027**: La digitación MUST validar al salir de cada campo y de nuevo al guardar y al
  contabilizar, listando todos los errores con enlace a la línea y al campo; el servidor MUST
  repetir toda validación.
- **FR-028**: Un comprobante MUST poder guardarse como borrador (sin número, editable, ausente de
  informes) con el permiso de registrar, y contabilizarse con el permiso de contabilizar, que es
  distinto; una vez contabilizado MUST ser inmutable y MUST guardar quién registró y quién
  contabilizó.
- **FR-029**: Cuando la empresa exige cuatro ojos, el sistema MUST rechazar que contabilice la
  misma persona que registró el borrador, aunque tenga el permiso, y MUST decir por qué.
- **FR-030**: La única corrección de un comprobante contabilizado MUST ser la anulación por
  reversión: un comprobante nuevo con las líneas invertidas, motivo obligatorio, referencia en
  ambos sentidos y fecha en un período abierto; MUST NOT poderse reversar dos veces ni reversar una
  reversión.
- **FR-031**: La digitación MUST poder operarse por teclado de principio a fin, con acciones para
  repetir tercero, centro y sucursal de la línea anterior, duplicar línea y llevar la diferencia a
  la última línea.
- **FR-032**: El buscador de terceros MUST ser el del maestro de personas, con creación en línea si
  el usuario tiene permiso de crear personas; centros de costo y sucursales MUST ofrecerse sólo
  activos; el centro MUST proponerse automáticamente cuando la empresa tiene uno solo y la sucursal
  siempre (la principal, o la del usuario con sucursales asignadas).
- **FR-033**: Un comprobante MUST poder llevar soportes adjuntos, que se conservan con él.
- **FR-034**: Cada comprobante MUST poder imprimirse con fecha, número, tipo, origen, descripción,
  líneas, totales, quién lo registró y quién lo contabilizó.
- **FR-035**: Un usuario con sucursales asignadas MUST poder digitar y consultar sólo esas
  sucursales; las líneas que envía un módulo (nómina, cartera, inventario, tesorería, CDT) llevan
  la sucursal de la operación y no se limitan por el alcance de quien la ejecuta.

**Integración con los módulos**

- **FR-036**: Los módulos MUST contabilizar a través de un único contrato que recibe el comprobante
  completo y aplica FR-014, FR-020 y FR-021; el comprobante y la operación del módulo MUST quedar
  registrados juntos o no quedar ninguno.
- **FR-037**: Todo comprobante MUST guardar su origen (módulo, tipo de documento origen e
  identificador) y, cuando el origen es un módulo, MUST verse en Contabilidad de sólo lectura con
  enlace al documento de origen y sin acciones de edición ni anulación.
- **FR-038**: La corrección de un comprobante de origen módulo MUST hacerse sólo desde ese módulo,
  mediante el mismo contrato de reversión (FR-030).
- **FR-039**: Nómina, Cartera, Inventario, Tesorería, CDT/Ahorros y los procesos propios de
  Contabilidad que generan comprobantes (cierre, activos, conciliación) MUST contabilizar por el
  contrato; MUST NOT quedar ningún camino que escriba movimientos fuera de él (comprobado por una
  prueba automática).
- **FR-040**: Cada módulo MUST tener sus tipos de comprobante reservados, MUST enviar como tercero
  de cada línea la persona de la operación (empleado, asociado, cliente, proveedor, banco) cuando la
  cuenta lo exige, y MUST enviar en toda línea la sucursal de la operación (la del empleado, del
  asociado, de la factura) o, si no la conoce, la principal; cuando la contraparte es una entidad
  de catálogo, el tercero es la persona vinculada a esa entidad (FR-088).
- **FR-041**: El mensaje de rechazo hacia el módulo MUST nombrar la línea, el concepto o producto y
  la regla incumplida.

**Consultas e informes**

- **FR-042**: El libro auxiliar interactivo MUST permitir profundizar clase → grupo → cuenta →
  subcuenta → auxiliar → tercero → documento cruce → comprobante → línea, mostrando en cada nivel
  saldo inicial, débitos, créditos y saldo final, y exportar cualquier nivel.
- **FR-043**: Consultas e informes MUST aceptar filtros combinables: rango de fechas, cuenta o rama
  del plan, tercero, documento cruce, centro de costo, sucursal, tipo de comprobante, módulo origen,
  usuario y nivel de detalle.
- **FR-044**: El sistema MUST ofrecer al menos: balance de prueba (por nivel, con o sin terceros,
  con o sin cierre), libro diario, libro mayor y balances, libro auxiliar por cuenta, estado de
  cuenta por tercero, documentos cruce con saldo pendiente, relación de comprobantes, estado de
  situación financiera, estado de resultados integral, estado de cambios en el patrimonio y estado
  de flujos de efectivo (método indirecto), con comparativo del período anterior.
- **FR-045**: Todo informe y toda consulta MUST exportarse a Excel, PDF y Word con encabezado de
  empresa, NIT, filtros aplicados, período, usuario y fecha de generación, y con los mismos totales
  que la pantalla.
- **FR-046**: Los saldos MUST ser siempre la suma de los movimientos contabilizados; MUST NOT
  existir un saldo almacenado que pueda divergir del libro, y el balance de prueba MUST cuadrar
  siempre (suma de débitos = suma de créditos).
- **FR-047**: Los estados financieros MUST construirse a partir del rubro NIIF de cada cuenta del
  catálogo y presentarse según el grupo NIIF configurado.
- **FR-048**: El saldo diario promedio de una cuenta en un período MUST calcularse desde los
  movimientos y ofrecerse como informe (reemplaza la tabla y la pantalla heredadas de «categorías
  de riesgo»).

**Auditoría, permisos y norma**

- **FR-049**: El sistema MUST distinguir permisos para: consultar, iniciar la contabilidad,
  parametrizar el plan, administrar tipos de comprobante, registrar borradores, contabilizar,
  anular, cerrar período, reabrir período, cerrar ejercicio, exportar, consultar la auditoría, y
  operar cada satélite (conciliar, presupuestar, impuestos y certificados, exógena, activos); el
  servidor MUST hacerlos cumplir y la interfaz MUST ocultar lo que no está permitido.
- **FR-050**: Toda escritura del módulo MUST quedar auditada con usuario, cooperativa, momento,
  operación, entidad y valores antes y después; toda exportación y todo envío de certificado MUST
  quedar auditado con informe, filtros y formato o destinatario.
- **FR-051**: El sistema MUST registrar cada apertura de cualquier opción del ERP (usuario,
  cooperativa, opción, momento, origen) y ofrecer una consulta filtrable y exportable de esos
  accesos al administrador y al auditor.
- **FR-052**: Los registros contables y su auditoría MUST conservarse al menos diez años y MUST NOT
  borrarse ni física ni lógicamente como flujo de usuario.
- **FR-053**: Los ejercicios cerrados MUST seguir consultables y exportables sin restricción de
  tiempo.
- **FR-054**: La moneda funcional MUST ser el peso colombiano con dos decimales; el sistema MUST
  rechazar montos con más decimales.

**Conciliación bancaria**

- **FR-055**: Una cuenta de movimiento MUST poder marcarse como bancaria con banco y número de
  cuenta; sólo esas cuentas se concilian.
- **FR-056**: El extracto de un mes MUST poder cargarse por archivo o digitarse, con fecha,
  referencia, descripción y valor; cada cuenta bancaria MUST tener un mapeo de columnas (fecha y su
  formato, referencia, descripción, valor con signo o débito y crédito separados) que el tesorero
  define una vez con vista previa y que se reutiliza; la plantilla propia del ERP MUST ser el mapeo
  por defecto; un archivo que no encaja MUST rechazarse señalando fila y columna, y las líneas
  repetidas MUST detectarse y no duplicarse.
- **FR-057**: El sistema MUST proponer coincidencias entre extracto y movimientos contables por
  valor y referencia, o por valor y fecha dentro de una tolerancia de días configurable, y el
  usuario MUST poder confirmarlas o rechazarlas una a una o en bloque; conciliar a mano MUST seguir
  siendo posible.
- **FR-058**: Las partidas sin pareja MUST clasificarse en «en libros, no en banco» y «en banco, no
  en libros», y el informe de conciliación MUST mostrar saldo en libros, saldo en extracto y las
  partidas que explican la diferencia hasta cero.
- **FR-059**: Desde una partida del extracto sin pareja MUST poder abrirse un borrador de
  comprobante prellenado para contabilizarla por la digitación normal.
- **FR-060**: La conciliación de un mes MUST poder cerrarse; desconciliar o reabrir MUST exigir
  permiso y motivo; una reapertura del período contable MUST marcarla desactualizada.

**Presupuesto**

- **FR-061**: El presupuesto MUST registrarse por ejercicio y cuenta de movimiento, opcionalmente
  por sucursal y centro de costo, con valores mensuales, y MUST poder cargarse por distribución de
  un total (igual, manual, porcentual) o copiando el ejercicio anterior con un porcentaje de ajuste.
- **FR-062**: Un presupuesto MUST aprobarse; después de aprobado, toda modificación MUST quedar como
  versión con motivo y responsable, y el informe MUST poder mostrar inicial y vigente.
- **FR-063**: La ejecución MUST mostrar presupuestado, ejecutado, variación y porcentaje, del mes y
  acumulado, por cuenta y agregado hacia arriba por niveles, con profundización al libro auxiliar y
  exportación.
- **FR-064**: Presupuestar una cuenta de agrupación MUST rechazarse.

**Impuestos y certificados de retención**

- **FR-065**: Una cuenta de movimiento MUST poder marcarse como cuenta de impuesto con tipo
  (retención en la fuente, IVA, ICA, GMF, renta), concepto, tarifa con vigencias y exigencia de base
  gravable; toda línea en esa cuenta MUST traer base y el sistema MUST comprobar valor ≈ base ×
  tarifa con una tolerancia configurable, avisando o rechazando según la exceda.
- **FR-066**: El sistema MUST ofrecer informes de retenciones practicadas (por tercero, concepto y
  período), IVA generado y descontable, ICA y GMF, y un resumen por renglón de cada formulario
  configurado, exportables.
- **FR-067**: Los certificados de retención (fuente, IVA, ICA) MUST generarse por tercero y año o
  período desde los movimientos, con concepto, base, tarifa, valor retenido, consecutivo y fecha de
  expedición; reimprimir MUST NOT cambiar el consecutivo.
- **FR-068**: Si la contabilidad de un año cambia después de expedidos los certificados, el sistema
  MUST marcarlos desactualizados y permitir reexpedirlos con número nuevo dejando el anterior
  anulado.
- **FR-069**: Los certificados MUST poder imprimirse y enviarse por correo al tercero, con registro
  del envío.
- **FR-070**: Las líneas de impuesto que envían los módulos (retención en Nómina, IVA en Inventario)
  MUST cumplir FR-065.

**Información exógena (medios magnéticos DIAN)**

- **FR-071**: Los formatos y conceptos de exógena MUST parametrizarse por año gravable, cada
  concepto con las cuentas que lo alimentan, la cuantía mínima y la regla de menores cuantías, sin
  cambios de programa entre años; el ERP MUST traer como semilla editable **todos** los formatos y
  conceptos de la resolución vigente del año gravable 2026, con las cuentas del catálogo propuestas
  en los conceptos donde el vínculo es natural, y cada formato MUST poder marcarse «no aplica» para
  que no genere; la semilla de un año nuevo MUST ofrecerse sin alterar lo ya parametrizado, y el
  contador MUST poder copiar la parametrización del año anterior.
- **FR-072**: La generación MUST producir, por formato, los valores por tercero y concepto desde
  los movimientos del año, con los datos de identificación del tercero (tipo y número de documento,
  dígito de verificación, nombres o razón social, dirección, municipio), y agrupar bajo la
  identificación de menores cuantías a quienes no alcancen la cuantía mínima.
- **FR-073**: La generación MUST entregar la lista de inconsistencias con enlace a cada persona;
  las bloqueantes (documento inválido) MUST impedir la exportación al formato DIAN y las demás
  MUST avisarse.
- **FR-074**: La información MUST exportarse en el formato que acepta el prevalidador de la DIAN y
  en Excel; cada exportación MUST conservarse como versión con fecha, usuario y archivo.
- **FR-075**: Regenerar MUST reemplazar sólo la versión de trabajo, nunca las exportadas.

**Activos fijos y diferidos**

- **FR-076**: Cada activo fijo MUST registrarse con código, nombre, cuentas de activo, depreciación
  acumulada y gasto (de movimiento y habilitadas para Activos), centro de costo, sucursal,
  proveedor, fecha y documento de compra, valor, valor residual, vida útil en meses y método lineal;
  cada diferido o intangible, con sus cuentas, valor y plazo en meses.
- **FR-077**: La ejecución mensual de depreciación y amortización MUST generar un solo comprobante
  por período con origen «Activos» a través del contrato único, MUST ser única por período (repetir
  no duplica) y MUST poder reversarse desde la misma opción.
- **FR-078**: Cambiar vida útil o valor residual MUST recalcular sólo las cuotas futuras sobre el
  valor neto en libros y quedar auditado; las cuotas contabilizadas MUST NOT cambiar.
- **FR-079**: La baja o venta de un activo MUST generar el comprobante que cancela costo y
  depreciación acumulada contra la cuenta indicada, y dejar el activo retirado con fecha y motivo.
- **FR-080**: Un activo totalmente depreciado MUST NOT generar más cuotas y MUST seguir en el
  inventario hasta su baja.
- **FR-081**: El inventario de activos MUST mostrar valor original, depreciación acumulada, valor
  neto en libros y proyección de cuotas, con exportación.
- **FR-082**: Un activo con fecha de compra en período cerrado MUST admitirse; la depreciación
  pendiente MUST acumularse en la primera cuota del primer período abierto, con aviso.

**Retiro de lo heredado**

- **FR-083**: Las opciones heredadas que el rediseño reemplaza (grupos y subgrupos de cuenta,
  categorías de riesgo, saldos, balance de prueba, movimientos, comprobantes, plan de cuentas,
  períodos, cierre, tipos de comprobante, y los satélites reconstruidos) MUST desaparecer del menú
  y de las rutas, sin dejar tablas ni pantallas huérfanas; la única opción de cada función es la
  nueva.

**Saldos de apertura**

- **FR-084**: El sistema MUST ofrecer un comprobante de apertura con tipo reservado, fechado el día
  anterior al primer período del primer ejercicio, admitido fuera de un período abierto sólo por
  ser de ese tipo, y tratado en consultas e informes como saldo inicial y no como movimiento del
  ejercicio.
- **FR-085**: La apertura MUST poder importarse desde un archivo con cuenta, tercero, documento
  cruce (tipo y número), centro de costo, sucursal, débito o crédito y detalle, y también digitarse;
  la importación MUST validar cada fila con FR-014, reportar los errores fila por fila y no dejar
  nada a medias.
- **FR-086**: La apertura MUST cuadrar para contabilizarse y MUST seguir el mismo flujo de borrador,
  contabilizar, cuatro ojos y reversión que un comprobante manual.
- **FR-087**: MUST existir a lo sumo una apertura contabilizada y no reversada por empresa; cargar
  otra MUST exigir reversar la anterior, y ambas MUST quedar referenciadas y auditadas.

**Terceros institucionales**

- **FR-088**: Cada entidad de los catálogos institucionales (EPS, ARL, fondos de pensiones, fondos
  de cesantías, cajas de compensación, bancos) MUST poder vincularse a una persona del maestro; el
  vínculo MUST ser obligatorio en cuanto alguna cuenta parametrizada para ese módulo exige tercero,
  la parametrización MUST validarlo al guardar nombrando la entidad sin vínculo, y la consulta de
  parametrizaciones inválidas (FR-017) MUST listar los vínculos que faltan.

### Key Entities

- **Catálogo**: plantilla versionada (Solidario, Comercial, o propio importado) con sus cuentas
  hasta nivel 4: código, nombre, naturaleza, rubro NIIF, nivel; y su validación (quién, cuándo).
- **Configuración contable de la empresa**: catálogo elegido, nivel de movimiento, longitudes de
  nivel 5 y 6, grupo NIIF, cuenta de resultado del ejercicio, sucursal principal, cuatro ojos,
  tolerancia de conciliación y de impuestos, fecha y responsable de la inicialización, bloqueada o
  no.
- **Cuenta**: nodo del plan de la empresa; código, nombre, nivel, naturaleza, rubro NIIF, padre,
  origen (catálogo o empresa), activa; si es de movimiento, sus **reglas** (módulos habilitados,
  exige tercero, documento, centro, sucursal), la fecha de su primer movimiento, y opcionalmente su
  condición de **cuenta bancaria** (banco, número) o de **cuenta de impuesto** (tipo, concepto,
  tarifas con vigencia, exige base).
- **Tipo de comprobante**: código, nombre, uso (manual / reservado a un módulo / cierre / activos /
  apertura), consecutivo, activo.
- **Ejercicio y período**: año fiscal con doce períodos mensuales; estado de cada uno; cierre del
  ejercicio con su comprobante.
- **Comprobante**: tipo, número, fecha, descripción, estado (borrador / contabilizado / anulado),
  origen (módulo, documento origen), totales, quién registró y quién contabilizó, referencia a su
  reversión o al comprobante que reversa, soportes.
- **Línea**: cuenta, sucursal (siempre), tercero, documento cruce (tipo, número), centro de costo,
  débito o crédito, detalle, base gravable, y su estado de conciliación cuando la cuenta es
  bancaria.
- **Parametrización de módulo**: vínculo de un concepto, producto, línea de crédito, instrumento o
  activo con cuentas de movimiento habilitadas para ese módulo.
- **Entidad institucional**: EPS, ARL, fondo de pensiones, fondo de cesantías, caja de
  compensación o banco de su catálogo, con la persona del maestro que la representa como tercero.
- **Extracto y partida**: líneas del banco para una cuenta bancaria y un mes (fecha, referencia,
  descripción, valor), su pareja contable si la tiene, y la conciliación del mes (saldos, estado,
  desactualizada); el **mapeo de columnas** de la cuenta bancaria (columnas, formato de fecha,
  signo) con el que se interpretan sus archivos.
- **Presupuesto**: ejercicio, cuenta, sucursal y centro opcionales, doce valores, versión
  (inicial, vigente), motivo de cada cambio, aprobación.
- **Certificado de retención**: tercero, tipo de impuesto, año o período, consecutivo, fecha de
  expedición, líneas (concepto, base, tarifa, valor), estado (vigente, desactualizado, anulado),
  envíos.
- **Formato de exógena**: año gravable, formato, aplica o no, origen (semilla o propio), conceptos
  con sus cuentas, cuantía mínima, regla de menores cuantías; **versión generada** (trabajo o
  exportada, fecha, usuario, archivo, inconsistencias).
- **Activo fijo / diferido**: datos de identificación y compra, cuentas, centro, sucursal,
  proveedor, valor, residual, vida útil o plazo, cuotas (período, valor, comprobante), estado
  (activo, totalmente depreciado, retirado).
- **Evento de acceso**: usuario, cooperativa, opción, momento, origen.
- **Permiso de contabilidad**: capacidad concedida a un rol.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Iniciar la contabilidad de una empresa nueva toma menos de 2 minutos de principio a
  fin y deja el 100 % de las cuentas del catálogo elegido hasta nivel 4, sin ninguna de movimiento;
  importar un catálogo propio de hasta 2 000 cuentas termina en menos de 1 minuto con cada error
  señalado por fila.
- **SC-002**: El 100 % de los intentos de mover o parametrizar una cuenta de agrupación, inactiva o
  no habilitada para el módulo son rechazados con un mensaje que nombra la cuenta y la regla, en
  digitación, en los cinco módulos y en los procesos propios (cierre, activos, conciliación).
- **SC-003**: Un auxiliar contable registra un comprobante de 20 líneas con cuentas que exigen
  tercero, documento y centro de costo en menos de 5 minutos usando sólo el teclado, y ninguna
  línea incompleta llega a contabilizarse; con cuatro ojos activo, el 100 % de los intentos de
  contabilizar el propio borrador son rechazados.
- **SC-004**: Toda liquidación de nómina aprobada produce su comprobante contabilizado en la misma
  operación; en las pruebas automáticas no queda ni una corrida aprobada sin comprobante ni un
  comprobante sin corrida (cero huérfanos en ambos sentidos), y lo mismo para cada operación de
  Cartera, Inventario, Tesorería, CDT y Activos.
- **SC-005**: Desde el balance de prueba se llega a la línea de un comprobante concreto en 4 clics
  o menos, y toda vista intermedia se exporta a Excel, PDF y Word con totales idénticos.
- **SC-006**: El balance de prueba cuadra (débitos = créditos) el 100 % de las veces y cada saldo
  coincide con la suma de sus movimientos en el libro auxiliar; las pruebas automáticas lo
  comprueban tras cada operación del ciclo (digitar, contabilizar, reversar, cerrar, reabrir,
  depreciar).
- **SC-007**: El 100 % de las escrituras, exportaciones, envíos y aperturas de opción aparecen en
  la consulta de auditoría en menos de 30 segundos, con usuario, cooperativa y momento; ninguna
  ruta de escritura del módulo responde sin permiso.
- **SC-008**: Con un libro de un millón de líneas, el libro auxiliar de una cuenta por un mes, el
  balance de prueba y la ejecución presupuestal responden en menos de 5 segundos; la exportación de
  un informe de hasta 50 000 líneas termina en menos de 60 segundos.
- **SC-009**: Un revisor fiscal con rol de sólo lectura obtiene los libros oficiales (diario, mayor
  y balances, auxiliares), los estados financieros y la conciliación bancaria de un ejercicio
  cerrado sin pedir nada al equipo técnico.
- **SC-010**: Cero caminos de escritura contable fuera del contrato único: la prueba automática que
  lo vigila falla si alguien agrega uno.
- **SC-011**: En la conciliación, el 100 % de las coincidencias exactas (valor y referencia) se
  proponen solas, y el informe explica la diferencia entre libros y extracto hasta cero en todos
  los casos de prueba.
- **SC-012**: Los certificados de retención de un año con 1 000 terceros se generan en menos de 2
  minutos, y el archivo de exógena exportado pasa la validación de estructura del prevalidador de
  la DIAN en QA.
- **SC-013**: La ejecución mensual de depreciación produce exactamente un comprobante por período
  aunque se lance varias veces, y las cuotas contabilizadas nunca cambian tras revisar vida útil o
  valor residual (comprobado por pruebas automáticas).
- **SC-014**: Importar un archivo de apertura de 5 000 filas termina en menos de 2 minutos con cada
  error señalado por fila, y tras contabilizarlo el balance de prueba del primer día coincide al
  centavo con los totales del archivo.

## Assumptions

- **Un solo juego de libros bajo NIIF**, por defecto Grupo 2 (NIIF para PYMES), configurable a
  Grupo 1 o 3 en la inicialización; la conciliación fiscal se hace fuera del módulo con los
  informes exportados. Las bases gravables se guardan por línea y alimentan impuestos, certificados
  y exógena.
- **Catálogos**: el equipo transcribe el Comercial desde el Decreto 2650 y el Solidario desde el
  catálogo de la Supersolidaria bajo NIIF, con el rubro NIIF asignado por el equipo; el contador de
  la cooperativa los valida en QA y esa validación es una tarea explícita antes de producción.
- **Niveles y longitudes**: catálogo hasta nivel 4 (6 dígitos); nivel 5 con 8 dígitos y nivel 6
  con 10 por defecto, configurables en la inicialización hasta un máximo de 12 dígitos en total.
- **Naturaleza y rubro NIIF vienen del catálogo** en cada subcuenta y se heredan hacia abajo; no
  se editan en la empresa.
- **Nómina es el primer módulo** integrado por el contrato porque es el único en producción; los
  otros cuatro se integran en esta misma feature con prioridad posterior, adaptando sus
  parametrizaciones existentes.
- **Comprobante de cierre**: se fecha el último día del ejercicio con un tipo reservado «Cierre»;
  no hay «período 13».
- **Numeración**: consecutivo por tipo de comprobante, continuo entre ejercicios, asignado al
  contabilizar.
- **Cuatro ojos**: desactivado por defecto; lo activa el administrador por empresa.
- **Sucursal**: toda línea la lleva; la principal la fija la configuración contable y se propone
  sola; a un usuario con sucursales asignadas se le propone la primera de las suyas; los informes
  por sucursal siempre suman el total de la empresa.
- **Roles integrados**: Administrador de cooperativa tiene todo; Operador registra borradores,
  consulta y exporta; Sólo lectura consulta y exporta; Auditor consulta, exporta y ve la auditoría.
  Contabilizar, anular, cerrar, reabrir, parametrizar y operar los satélites los asigna el
  administrador (a sí mismo o a un rol «Contador» que crea).
- **Retención**: diez años para registros contables y su auditoría (norma colombiana), por encima
  de los cinco de la auditoría general.
- **Exportación**: se reutiliza el exportador existente (Excel, PDF y Word) y el mismo formato de
  encabezado de los informes de nómina.
- **Terceros**: el tercero de una línea es siempre una persona del maestro centralizado; el
  buscador y la creación en línea son los de la feature 008; los datos de exógena (documento,
  dirección, municipio) salen de Personas y se corrigen allí. Las entidades institucionales
  (EPS, ARL, fondos, cajas, bancos) llegan al libro por la persona a la que están vinculadas.
- **Soportes adjuntos**: se reutiliza la capacidad de adjuntos cifrados existente; el envío de
  certificados usa el correo ya configurado por cooperativa.
- **Auditoría de accesos**: el mecanismo es general para todo el ERP (una opción abierta = un
  evento), no sólo para contabilidad; los eventos van al mismo almacén de auditoría.
- **Conciliación**: el extracto llega por archivo tabular (hoja de cálculo o texto separado)
  interpretado con el mapeo de columnas de la cuenta bancaria, o digitado; no hay conexión con los
  bancos; tolerancia de días por defecto 3.
- **Depreciación y amortización**: sólo método de línea recta con valor residual; deterioro y
  revaluación se registran con comprobantes manuales.
- **Exógena**: formatos y conceptos son configuración por año; la semilla 2026 trae todos los de
  la resolución vigente (los que no aplican se marcan y no generan); el formato de salida es el que
  publica la DIAN para su prevalidador en ese año gravable; la validación final la hace el
  prevalidador, no el ERP.
- **Documento cruce**: los tipos vienen de un catálogo sembrado (factura de venta, factura de
  compra, cuenta de cobro, nota crédito, nota débito, contrato, pagaré, otro) que el administrador
  amplía; los módulos usan el tipo de su documento.
- **Anulación**: la reversión se contabiliza en el acto con el permiso de anular; la regla de
  cuatro ojos aplica al comprobante original, no a su reversión.
- **Ejercicio**: coincide con el año calendario (enero a diciembre).
- **Sin datos históricos en el ERP**: verificado el 2026-09-14 en DEV, QA y producción; no se
  construye conversión de movimientos. Una cooperativa que viene de SOLIDO arranca con el
  comprobante de apertura (US13); el archivo lo prepara su contador desde SOLIDO con la plantilla
  que el ERP publica.

## Dependencias

- Maestro de personas, buscador compartido y permisos del cliente (feature 008).
- Exportador de tablas (Excel, PDF, Word) e indicador de carga por zona (features 006 y 007).
- Pipeline de auditoría existente (comandos con valores antes/después).
- Catálogo de permisos y roles integrados (seguridad fase 0).
- Nómina: aprobación, reversión y cuentas por concepto (features 005 y 006).
- Correo por cooperativa (envío de certificados) y adjuntos cifrados (soportes).
