# Feature Specification: Novedades y Liquidación Periódica de Nómina

**Feature Branch**: `005-nomina-novedades-liquidacion`

**Created**: 2026-09-05

**Status**: Draft

**Input**: User description: "requiero la construcción de las funcionalidades de nomina/novedades y nomina/liquidacion, que corresponden a las novedades de nómina de empleados que se presentan cada período, y a la liquidación periódica que se realiza a todos los empleados. Se requiere una implementación dinámica, flexible y parametrizable para tener una liquidación segura, comprobable y precisa para cada uno de los empleados con base en la información de salarios, conceptos, novedades, etc. Este proceso debe ser sencillo, claro y seguro."

## Contexto

Las opciones **Nómina → Novedades** y **Nómina → Liquidación** existen hoy en el menú
pero son pantallas vacías («módulo en construcción»). Detrás hay un proceso de
liquidación preliminar que **no cumple lo que se pide**: calcula con porcentajes fijos
escritos en el programa (salud 4 %, pensión 4 %, aportes del empleador 8,5 % y 12 %),
divide el salario entre 30 sin mirar ingresos ni retiros dentro del período, ignora
el auxilio de transporte, las provisiones de prestaciones y la tabla de retención en
la fuente, y no deja explicación de ningún valor. Un cambio legal exige recompilar, y
un contador no puede reconstruir de dónde salió un número.

Lo que ya existe y esta feature aprovecha: la ficha del empleado (salario, tipo de
contrato, fechas de ingreso y retiro, afiliaciones a EPS, fondo de pensiones, ARL y
cesantías, centro de costo, clase de nómina), el catálogo de **conceptos de nómina**
heredado del sistema anterior (naturaleza, base, factor, qué bases afecta, si es
prestacional, cuentas contables por concepto), los **períodos de pago**, los
parámetros de retención en la fuente y de autoliquidación de aportes, y el
comprobante de pago del empleado como reporte.

La cooperativa tiene, por lo general, entre diez y doscientos empleados y paga
mensual o quincenalmente. Quien liquida es una persona del área administrativa, no
un especialista en sistemas: necesita ver **qué** se va a pagar, **por qué** cada
valor da lo que da, corregir antes de aprobar, y quedarse tranquila de que lo
aprobado no cambia.

## Clarifications

### Session 2026-09-05

- Q: ¿Alcance de la liquidación periódica frente a los demás procesos de nómina? →
  A (por defecto, ver Supuestos): esta feature cubre la **nómina ordinaria de cada
  período** (devengos, deducciones, aportes del empleador y provisiones mensuales de
  prestaciones). La liquidación definitiva por retiro, la prima semestral, la
  consignación anual de cesantías e intereses, la liquidación de vacaciones, la
  planilla PILA y la nómina electrónica ante la DIAN son procesos aparte que **consumen**
  lo que esta feature produce y quedan para features posteriores.
- Q: ¿Cómo define la administradora el cálculo de un concepto: formas de cálculo
  predefinidas y parametrizadas, un lenguaje de fórmulas libre, o ambas? → A: **Formas
  de cálculo predefinidas y parametrizadas** (valor fijo, porcentaje sobre base,
  cantidad por unidad derivada del salario, tabla por rangos, suma o porcentaje de
  otros conceptos). Sin lenguaje de fórmulas libre: cada forma se valida y se explica
  sola, y un caso que ninguna cubra se resuelve agregando una forma en una versión,
  no escribiendo una expresión en producción. Ver FR-009.
- Q: ¿Una cooperativa tiene una sola nómina con un solo calendario, varios planes de
  nómina dentro de una única empresa pagadora, o varias empresas pagadoras? → A:
  **Varios planes de nómina dentro de una única empresa pagadora**. Cada plan tiene su
  periodicidad y su calendario de períodos; cada empleado pertenece a exactamente un
  plan; se liquida por plan y período. El sistema crea un plan por defecto, de modo que
  una cooperativa con una sola nómina nunca ve el concepto. Varias empresas pagadoras
  quedan fuera de alcance. Ver FR-037 y la entidad «Plan de nómina».
- Q: ¿Cómo llegan los conceptos del catálogo heredado a las formas de cálculo nuevas:
  semilla curada más creación propia, traducción automática del catálogo heredado, o
  ambas? → A: **Semilla curada más creación propia**. El sistema trae los conceptos
  legales colombianos estándar ya parametrizados con su forma de cálculo y sus bases;
  la cooperativa crea los suyos. El catálogo heredado se conserva sólo como referencia
  de consulta y no participa en ninguna liquidación; un concepto heredado se traduce a
  mano, uno a uno, cuando haga falta. No hay traducción automática. Ver FR-038.
- Q: Retención en la fuente: ¿procedimiento 1 completo con el 2 como porcentaje del
  empleado, ambos procedimientos completos, o sólo el 1? → A: **Procedimiento 1
  completo; procedimiento 2 como porcentaje del empleado**. La tabla por rangos en UVT
  se aplica cada período sobre la base depurada, con las deducciones y rentas exentas
  parametrizadas. Para quien esté en procedimiento 2, la cooperativa registra el
  porcentaje fijo con vigencia semestral y el sistema lo aplica y lo explica; calcular
  ese porcentaje a partir de los doce meses anteriores queda para una feature
  posterior. Ver FR-039.
- Q: ¿Quién marca la nómina como pagada, para que la reversión tenga de dónde leer «ya
  se pagó»? → A: **La nómina marca su propio pago**. En la relación de pago del período,
  la responsable registra «pagado» —total o por empleado— con fecha, medio, referencia
  y usuario; esa marca bloquea la reversión. Tesorería no interviene en esta feature;
  la ejecución bancaria sigue fuera del sistema. Ver FR-040.
- Q: ¿El sistema envía el comprobante de pago al empleado: sólo descarga e impresión,
  envío manual por correo, o envío automático al aprobar? → A: **Envío manual por
  correo**, además de la descarga e impresión. La responsable envía el comprobante a un
  empleado o a todo el período con una acción explícita; el sistema exige correo en la
  ficha, registra cada envío con su resultado, y nunca envía nada automáticamente. Ver
  FR-026.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar las novedades del período (Priority: P1)

La analista de nómina abre el período de pago en curso y registra lo que cambió para
cada empleado respecto de su salario fijo: horas extra y recargos, comisiones y
bonificaciones, incapacidades, licencias, vacaciones, permisos no remunerados,
préstamos y descuentos autorizados, y cambios de salario con fecha de efecto. Cada
novedad dice a qué empleado, con qué concepto, cuánto (horas, días o valor) y desde y
hasta cuándo. Antes de liquidar puede revisarlas todas juntas, corregirlas o anularlas.

**Why this priority**: las novedades son la materia prima de la liquidación; sin ellas
la nómina de cada período sería idéntica a la anterior y el proceso no serviría para
nada real. Es además lo que la cooperativa hace **todos los períodos** durante varios
días, mientras que liquidar es un acto de una hora.

**Independent Test**: con un período abierto y dos empleados activos, registrar cinco
novedades de tipos distintos (horas extra, incapacidad de tres días, un préstamo, una
comisión y un cambio de salario a mitad de período), verificar que la lista del
período las muestra con sus cantidades y valores previstos, corregir una, anular otra
y comprobar que el historial conserva quién hizo cada cambio y cuándo. El valor lo
aporta por sí solo: la cooperativa deja de llevar las novedades en hojas de cálculo.

**Acceptance Scenarios**:

1. **Given** un período de pago abierto y un empleado activo, **When** la analista
   registra una novedad de horas extra nocturnas con 6 horas, **Then** la novedad
   queda asociada al período y al empleado, muestra el valor que aportará a la
   liquidación según el concepto y el salario vigente, y registra quién la creó.
2. **Given** una novedad de incapacidad con fecha inicial y final, **When** se guarda,
   **Then** el sistema calcula los días que caen dentro del período, indica cuántos
   quedan para el período siguiente si la incapacidad lo cruza, y aplica el
   tratamiento del concepto (por ejemplo, los dos primeros días a cargo del empleador
   al 66,67 %) sin que la analista teclee porcentajes.
3. **Given** un empleado retirado antes de iniciar el período, **When** se intenta
   registrarle una novedad, **Then** el sistema lo impide y explica que el empleado no
   está vigente en ese período.
4. **Given** un período ya liquidado y aprobado, **When** se intenta registrar,
   corregir o anular una novedad en él, **Then** el sistema lo impide y ofrece
   registrarla como **ajuste retroactivo** en el período abierto siguiente, con la
   referencia al período original.
5. **Given** dos novedades del mismo concepto para el mismo empleado y período, cuando
   el concepto no admite repetirse, **When** se intenta guardar la segunda, **Then** el
   sistema lo impide y muestra la novedad ya existente.
6. **Given** una novedad de cambio de salario con fecha de efecto el día 16 de un
   período mensual, **When** se guarda, **Then** el salario del empleado queda con dos
   vigencias y la liquidación del período usa cada una para los días que le
   corresponden.
7. **Given** una novedad guardada, **When** la analista la corrige o la anula, **Then**
   no se borra nada: la versión anterior queda visible en el historial con motivo,
   usuario y fecha, y la liquidación en borrador, si existe, queda marcada como
   «desactualizada» hasta recalcular.

---

### User Story 2 - Liquidar el período: calcular, revisar, aprobar (Priority: P1)

La responsable de nómina, con las novedades registradas, ordena **calcular** el
período de un plan de nómina (si la cooperativa tiene uno solo, no se le pregunta). El
sistema liquida a todos los empleados del plan vigentes en el período: salario por los días
efectivamente laborados del período, auxilio de transporte si aplica, las novedades,
las deducciones de ley (salud, pensión, fondo de solidaridad, retención en la
fuente), las deducciones autorizadas, los aportes del empleador y las provisiones de
prestaciones. El resultado es un **borrador** que se puede revisar empleado por
empleado, comparar con el período anterior y recalcular tantas veces como haga falta.
Cuando está conforme, **aprueba**: el período queda cerrado, los valores ya no
cambian, y se generan el comprobante contable y la relación de pago.

**Why this priority**: es la razón de ser del módulo. Sin la liquidación, registrar
novedades no produce nada. Se prioriza igual que la US1 porque son inseparables: la
primera versión entregable necesita las dos para que una cooperativa pague una nómina
con el sistema.

**Independent Test**: sobre un período con tres empleados (uno de tiempo completo, uno
que ingresó el día 10 y uno con salario superior a dos salarios mínimos) y las
novedades de la US1, calcular el borrador y verificar cada línea contra una
liquidación hecha a mano: días, salario proporcional, auxilio de transporte sólo a
quien corresponde, deducciones sobre la base correcta, retención según tabla. Cambiar
una novedad, recalcular y ver que sólo cambian las líneas afectadas. Aprobar y
verificar que el período queda cerrado, que un segundo intento de calcular es
rechazado, y que el comprobante contable cuadra.

**Acceptance Scenarios**:

1. **Given** un período abierto con novedades registradas y los parámetros legales del
   año vigentes, **When** la responsable ordena calcular, **Then** en menos de un
   minuto obtiene un borrador con una liquidación por cada empleado del plan vigente
   en el período, el total devengado, deducido, neto, aportes del empleador y
   provisiones, y ningún empleado vigente del plan queda sin liquidar.
2. **Given** un empleado que ingresó el día 10 de un período mensual de 30 días,
   **When** se calcula, **Then** su salario se liquida por 21 días y todas las bases
   (salud, pensión, prestaciones) se proporcionan igual.
3. **Given** un empleado con salario hasta dos salarios mínimos legales, **When** se
   calcula, **Then** recibe auxilio de transporte proporcional a los días laborados;
   otro con salario mayor no lo recibe, sin que nadie lo marque a mano.
4. **Given** un borrador calculado, **When** la responsable cambia una novedad y
   recalcula, **Then** el borrador anterior se reemplaza completo, el resultado es el
   mismo que si se hubiera calculado por primera vez con esos datos, y el sistema
   señala qué empleados cambiaron respecto del borrador anterior.
5. **Given** un borrador en el que un empleado queda con neto negativo o con una
   deducción mayor al máximo permitido por ley, **When** la responsable intenta
   aprobar, **Then** el sistema lo impide, señala al empleado y la causa, y sólo
   permite continuar si una persona con permiso de autorización acepta la excepción
   dejando motivo.
6. **Given** un borrador revisado, **When** la responsable aprueba y confirma, **Then**
   el período pasa a «aprobado», las novedades y la liquidación quedan inmutables, se
   genera un comprobante contable cuadrado con las cuentas de cada concepto y centro
   de costo, y queda disponible la relación de pago con el neto de cada empleado y su
   cuenta bancaria.
7. **Given** un período aprobado, **When** alguien intenta calcular de nuevo, cambiar
   una novedad o editar una línea, **Then** el sistema lo rechaza; el único camino es
   la reversión controlada de la US7 o un ajuste en el período siguiente.
8. **Given** que faltan los parámetros legales del año (salario mínimo, auxilio de
   transporte, UVT o tabla de retención), **When** se ordena calcular, **Then** el
   sistema se niega y dice exactamente qué parámetro falta y dónde se registra, en vez
   de liquidar con valores del año anterior.
9. **Given** dos personas que ordenan calcular el mismo período a la vez, **When**
   ambas peticiones llegan, **Then** una sola se ejecuta y la otra recibe el aviso de
   que ya hay un cálculo en curso; nunca quedan dos borradores del mismo período.

---

### User Story 3 - Parametrizar conceptos y reglas de cálculo sin tocar el programa (Priority: P2)

La administradora de nómina define y mantiene los conceptos (qué se paga, qué se
descuenta, qué aporta el empleador, qué se provisiona) y **cómo se calcula cada uno**:
un valor fijo, un porcentaje sobre una base, una cantidad por un valor unitario
derivado del salario, una tabla por rangos, o una combinación de otros conceptos.
Registra también los **parámetros legales con vigencia por año**: salario mínimo,
auxilio de transporte, UVT, porcentajes de salud, pensión, fondo de solidaridad,
ARL por clase de riesgo, parafiscales, provisiones, y la tabla de retención en la
fuente. Cuando cambia la ley, cambia un parámetro con fecha de vigencia, y la próxima
liquidación lo usa. No parte de cero: el sistema entrega los conceptos legales
estándar ya parametrizados (la semilla), y ella los ajusta, los desactiva o agrega los
propios; el catálogo heredado del sistema anterior queda a la mano como referencia.

**Why this priority**: es lo que hace «dinámica, flexible y parametrizable» a la
liquidación, y lo que evita que cada enero haya que esperar una versión nueva. Va
después de la US1 y la US2 porque la primera entrega puede salir con los conceptos y
parámetros cargados por quien implementa; pero sin esta historia la cooperativa no es
autónoma.

**Independent Test**: crear un concepto nuevo «Bonificación por antigüedad» como 2 %
del salario básico, no prestacional, que afecta la base de retención; registrar el
valor del auxilio de transporte del año siguiente con vigencia 1 de enero; probar el
concepto «en seco» sobre un empleado y ver el valor esperado; calcular un período de
diciembre y otro de enero y verificar que cada uno usa el auxilio de su vigencia.

**Acceptance Scenarios**:

1. **Given** el catálogo de conceptos, **When** la administradora crea un concepto
   indicando su naturaleza (devengo, deducción, aporte del empleador, provisión o
   informativo), su forma de cálculo y las bases que afecta, **Then** el concepto queda
   disponible para novedades y para la liquidación sin ninguna intervención técnica.
2. **Given** un concepto definido como porcentaje sobre la base «salario básico más
   devengos salariales», **When** se liquida, **Then** el valor resulta de aplicar el
   porcentaje a esa base tal como quedó en el mismo período, y la explicación de la
   línea muestra la base, el porcentaje y el resultado.
3. **Given** un parámetro legal con dos vigencias (2026 y 2027), **When** se calcula un
   período de cada año, **Then** cada liquidación usa el valor vigente en la fecha de
   fin de su período.
4. **Given** una definición de concepto que se refiere a otro concepto que a su vez
   depende del primero (referencia circular), **When** se intenta guardar, **Then** el
   sistema lo rechaza y muestra el ciclo.
5. **Given** un concepto ya usado en una liquidación aprobada, **When** se modifica su
   forma de cálculo, **Then** las liquidaciones aprobadas no cambian; la modificación
   rige desde el siguiente cálculo, y el historial del concepto conserva la definición
   anterior con su vigencia.
6. **Given** un concepto en edición, **When** la administradora pide probarlo sobre un
   empleado y un período, **Then** ve el valor que daría y su explicación sin crear
   ninguna novedad ni liquidación.
7. **Given** una cooperativa recién creada, **When** la administradora abre el catálogo
   de conceptos, **Then** encuentra la semilla de conceptos legales estándar ya
   parametrizados y marcados como tales; puede ajustarlos o desactivarlos pero no
   borrarlos, y aplicar la semilla de nuevo no duplica ninguno.

---

### User Story 4 - Comprobar y explicar cada valor (Priority: P2)

La responsable de nómina, o la auditora, abre la liquidación de un empleado y ve
cada línea con su **explicación**: qué concepto, qué base, qué factor o tabla, qué
parámetro legal y con qué vigencia, y qué novedad la originó. Ve el comparativo con
el período anterior con las variaciones resaltadas, los totales por concepto de toda
la nómina, y la verificación de cuadre. Puede exportarlo para la revisoría fiscal.

**Why this priority**: «comprobable» es un requisito explícito. Una liquidación que
no se puede explicar no se puede defender ante el empleado, la revisoría ni la UGPP.
Va como P2 porque el borrador de la US2 ya muestra los valores; esta historia añade el
**por qué** y las herramientas de revisión.

**Independent Test**: abrir la liquidación de un empleado con una incapacidad y una
retención en la fuente, y reconstruir a mano cada línea a partir de la explicación
que muestra el sistema; abrir el comparativo con el período anterior y verificar que
las variaciones superiores al umbral aparecen resaltadas; exportar el detalle y
comprobar que contiene las mismas líneas y explicaciones.

**Acceptance Scenarios**:

1. **Given** una liquidación calculada, **When** se abre el detalle de un empleado,
   **Then** cada línea muestra concepto, cantidad, base, factor o tabla aplicada,
   parámetro legal con su vigencia, novedad de origen si la hay, y el valor; la suma
   de las líneas coincide con los totales del empleado.
2. **Given** un período con período anterior liquidado, **When** se abre el
   comparativo, **Then** se ven por empleado el neto anterior, el actual y la
   variación; las variaciones por encima del umbral definido por la cooperativa
   quedan resaltadas y se pueden filtrar.
3. **Given** cualquier liquidación, **When** se pide la verificación de cuadre,
   **Then** el sistema muestra que devengado menos deducciones es igual al neto, que
   aportes del empleador y provisiones están separados del neto, y que el comprobante
   contable (si ya existe) suma igual en débitos y créditos.
4. **Given** una liquidación aprobada, **When** la auditora exporta el detalle,
   **Then** obtiene un archivo con todas las líneas y sus explicaciones, y la
   exportación queda registrada en auditoría.

---

### User Story 5 - Contabilizar y disponer el pago (Priority: P2)

Al aprobar la liquidación, el sistema produce en el mismo acto el **comprobante
contable** de la nómina —una línea por concepto y centro de costo, con las cuentas
definidas para cada concepto— y la **relación de pago** con el neto de cada empleado,
su banco, tipo y número de cuenta, lista para el proceso de pago de la cooperativa. Los
comprobantes de pago de cada empleado se descargan e imprimen de a uno o todos
juntos, y la responsable puede enviarlos por correo a un empleado o a todo el período
con una acción explícita; nada se envía solo.

**Why this priority**: sin contabilización la nómina no llega a los estados
financieros y sin relación de pago no se paga. Es P2 y no P1 porque la aprobación de
la US2 ya deja los datos listos; esta historia es su salida hacia contabilidad y
tesorería.

**Independent Test**: aprobar un período y verificar que existe un comprobante
contable de tipo nómina cuadrado cuyo total coincide con la nómina, que cada concepto
llegó a la cuenta configurada, que un concepto sin cuenta configurada impidió la
aprobación con mensaje claro, y que la relación de pago lista a cada empleado con su
neto y cuenta.

**Acceptance Scenarios**:

1. **Given** todos los conceptos con cuentas contables asignadas, **When** se aprueba
   el período, **Then** se crea un comprobante contable de nómina, cuadrado, con una
   línea por concepto y centro de costo, referenciado desde la liquidación, y no se
   puede editar ni borrar.
2. **Given** un concepto usado en el borrador sin cuentas contables asignadas,
   **When** se intenta aprobar, **Then** el sistema lo impide y nombra el concepto y
   los empleados afectados.
3. **Given** un período aprobado, **When** se abre la relación de pago, **Then** lista
   a cada empleado con neto a pagar, medio de pago, banco, tipo y número de cuenta, y
   el total coincide con el neto de la nómina.
4. **Given** un período aprobado, **When** se pide el comprobante de pago de un
   empleado, **Then** muestra devengos, deducciones y neto de ese período con los
   mismos valores de la liquidación.
5. **Given** la relación de pago de un período aprobado, **When** la responsable marca
   como pagados a todos los empleados (o a algunos) con fecha, medio y referencia,
   **Then** cada uno queda «pagado» con esos datos y el usuario, el período ya no se
   puede reversar, y retirar la marca exige permiso y motivo.
6. **Given** un período aprobado en el que dos empleados no tienen correo en su ficha,
   **When** la responsable ordena enviar los comprobantes de todo el período, **Then**
   el sistema envía a los que tienen correo, registra cada envío con su resultado,
   lista a los dos sin correo para entrega manual, y ofrece reintentar los que
   fallaron; nada de esto altera la liquidación ni la marca de pago.

---

### User Story 6 - Novedades recurrentes y carga masiva (Priority: P3)

La analista registra una vez las novedades que se repiten por varios períodos —una
libranza a 24 cuotas, un auxilio permanente— y el sistema las incluye en cada período
hasta cumplir el número de cuotas o la fecha final. Para las novedades variables de
muchos empleados (horas extra del mes, comisiones), carga un archivo con una fila por
novedad; el sistema valida fila por fila, muestra los errores con su número de fila y
sólo aplica el lote cuando todo es válido.

**Why this priority**: reduce el trabajo repetitivo y los errores de transcripción,
pero la cooperativa puede operar sin esto registrando una a una. Es una mejora de
productividad sobre la US1.

**Independent Test**: registrar una libranza a 3 cuotas y verificar que aparece en
tres períodos consecutivos y no en el cuarto; cargar un archivo con 20 filas, dos de
ellas erróneas, y verificar que el sistema rechaza el lote señalando las dos filas;
corregirlas y cargar de nuevo con éxito.

**Acceptance Scenarios**:

1. **Given** una novedad recurrente con 3 cuotas, **When** se calculan tres períodos
   seguidos, **Then** aparece en los tres con el número de cuota, y en el cuarto ya no.
2. **Given** un archivo de novedades con filas inválidas, **When** se carga, **Then**
   ninguna fila se aplica y el resultado enumera cada error con su fila y causa.
3. **Given** un archivo válido, **When** se carga, **Then** todas las novedades quedan
   registradas como si se hubieran creado una a una, con el mismo historial.

---

### User Story 7 - Reversión controlada de un período aprobado (Priority: P3)

Cuando se descubre un error después de aprobar, la responsable, con un permiso
específico, **reversa** el período dejando motivo. El sistema genera el comprobante
contable de reversión, reabre el período para corregir y volver a calcular, y
conserva la liquidación reversada como historial. No se puede reversar si algún
empleado ya está marcado como pagado en la relación de pago o si el período contable
está cerrado.

**Why this priority**: es la válvula de seguridad. Sin ella, un error tras aprobar
obliga a ajustes manuales fuera del sistema, que es exactamente lo que se quiere
evitar. Es P3 porque los controles de la US2 (borrador, comparativo, bloqueos) hacen
que el caso sea raro.

**Independent Test**: aprobar un período, reversarlo con motivo, verificar que existe
el comprobante de reversión, que el período volvió a «abierto» con sus novedades
intactas, que la liquidación reversada sigue consultable, y que un período con
empleados marcados como pagados no se deja reversar.

**Acceptance Scenarios**:

1. **Given** un período aprobado sin pago aplicado, **When** una persona con permiso
   de reversión la ordena con motivo y confirma, **Then** se genera el comprobante
   contable de reversión, el período vuelve a «abierto», la liquidación reversada
   queda en historial marcada como tal, y todo queda en auditoría.
2. **Given** un período aprobado con al menos un empleado marcado como pagado en la
   relación de pago, **When** se intenta reversar, **Then** el sistema lo impide y
   muestra qué marcas de pago lo bloquean (empleado, fecha, referencia).
3. **Given** un período aprobado cuyo período contable ya está cerrado, **When** se
   intenta reversar, **Then** el sistema lo impide y remite al cierre contable.

---

### Edge Cases

- Empleado que ingresa y se retira dentro del mismo período: se liquidan sólo los días
  entre ambas fechas; los días posteriores al retiro no generan salario ni aportes.
- Incapacidad o licencia que cruza el fin del período: los días del período se
  liquidan; el resto queda registrado como pendiente y aparece automáticamente en la
  novedad del período siguiente.
- Dos cambios de salario dentro de un período: cada tramo de días usa su salario.
- Cambio de plan de nómina (de mensual a quincenal, por ejemplo) con fecha de efecto a
  mitad de un período: el empleado termina el período abierto en su plan actual y
  entra al plan nuevo en el primer período de ese plan que empiece después de la
  fecha; nunca aparece en dos períodos abiertos a la vez ni se le liquidan días dos
  veces.
- Empleado sin afiliación a salud o pensión registrada: el cálculo lo señala y la
  aprobación se bloquea hasta completar la ficha, porque los aportes no tendrían
  destinatario.
- Salario integral: no se provisionan prestaciones y las bases de aportes se toman
  sobre el 70 % según el parámetro; el concepto lo define, no el programa.
- Aprendices y contratos con reglas propias: pertenecen a una clase de empleado cuyos
  conceptos aplicables y exclusiones se parametrizan; no hay casos especiales escritos
  en el programa.
- Deducciones que superan el máximo legal sobre el salario (embargos, libranzas): el
  sistema aplica el tope, deja el saldo pendiente para el siguiente período y lo
  señala en la revisión.
- Neto negativo: bloquea la aprobación salvo excepción autorizada con motivo.
- Redondeo: los valores se redondean al peso según el parámetro de la cooperativa; la
  suma de las líneas redondeadas debe coincidir con los totales mostrados, y la
  diferencia por redondeo, si existe, se imputa a un concepto de ajuste explícito.
- Período sin empleados vigentes: el cálculo termina sin liquidaciones y lo dice; no
  aprueba un período vacío sin confirmación explícita.
- Parámetros legales sin vigencia para la fecha del período: el cálculo se niega y
  nombra el parámetro (nunca liquida con el año anterior en silencio).
- Empleado en procedimiento 2 de retención sin porcentaje vigente para la fecha del
  período (venció el semestre y nadie registró el nuevo): el cálculo se niega para ese
  empleado y lo nombra; no aplica el porcentaje vencido ni cae al procedimiento 1.
- Empleado sin deducciones ni rentas exentas declaradas: la base de retención se
  depura sólo con los aportes obligatorios y se aplica la tabla; la explicación dice
  que no hay depuraciones declaradas, para que se note si faltó registrarlas.
- Envío de comprobantes con empleados sin correo en la ficha: se envía a los demás y
  los que faltan quedan listados para entrega manual; nunca se envía a un correo
  «parecido» ni al del empleador.
- Fallo del correo saliente al enviar comprobantes: cada intento queda registrado con
  su resultado y se puede reintentar; la aprobación y la marca de pago no dependen de
  que el correo salga.
- Dos personas calculando el mismo período a la vez: una sola ejecución; la otra
  recibe aviso.
- Un concepto se modifica mientras hay un borrador: el borrador se marca
  «desactualizado» y debe recalcularse antes de aprobar.
- Un concepto heredado que ninguna forma de cálculo cubre: no se traduce «a lo más
  parecido»; queda como referencia con la marca «sin forma equivalente» y se registra
  la forma que falta para una versión posterior. Ningún concepto liquida con una regla
  que no sea exactamente la suya.
- Corte de la sesión o falla a mitad del cálculo: no queda un borrador a medias; o se
  completa o no existe.
- Novedad registrada por error en el empleado equivocado, ya aprobado el período: se
  corrige con un ajuste retroactivo en el siguiente período, que referencia al
  original y aparece en su comprobante de pago como tal.

## Requirements *(mandatory)*

### Functional Requirements

**Novedades**

- **FR-001**: El sistema MUST permitir registrar novedades por empleado dentro de un
  período de pago abierto, indicando concepto, cantidad (horas o días) o valor, fecha
  inicial y final cuando aplique, y una observación.
- **FR-002**: El sistema MUST validar al guardar una novedad que el período esté
  abierto, que el empleado esté vigente en alguna parte del período, que el concepto
  esté vigente y sea aplicable a la clase del empleado, y que se cumplan las reglas de
  repetición y topes definidas en el concepto; cada rechazo MUST decir la causa.
- **FR-003**: El sistema MUST calcular, al registrar una novedad con fechas, los días
  que caen dentro del período y dejar constancia de los que corresponden a períodos
  siguientes, incluyéndolos automáticamente en ellos.
- **FR-004**: El sistema MUST registrar los cambios de salario como novedades con
  fecha de efecto, conservando el historial de salarios del empleado, y MUST liquidar
  cada tramo de días con el salario vigente en él.
- **FR-005**: El sistema MUST permitir corregir y anular novedades de un período
  abierto conservando la versión anterior, el motivo, el usuario y la fecha; MUST
  impedirlo en períodos aprobados y ofrecer en su lugar un ajuste retroactivo en el
  período abierto siguiente, con referencia al período original.
- **FR-006**: El sistema MUST mostrar la lista de novedades del período con filtros
  por empleado, concepto, tipo y estado, y el valor previsto de cada una según el
  salario vigente.

**Cálculo**

- **FR-007**: El sistema MUST liquidar, a solicitud, a todos los empleados del plan de
  nómina vigentes en el período en un solo proceso, produciendo por cada uno las líneas
  de devengos, deducciones, aportes del empleador y provisiones, con sus totales y el
  neto a pagar.
- **FR-008**: El sistema MUST proporcionar el salario y todas las bases a los días
  efectivamente vinculados dentro del período (ingresos, retiros y cambios de salario
  intermedios), según la duración de período que define la periodicidad del plan de
  nómina (30 días para mensual, 15 para quincenal).
- **FR-009**: El sistema MUST calcular cada concepto según una de las **formas de
  cálculo predefinidas y parametrizadas**: valor fijo; porcentaje sobre una base
  (salario básico, devengos salariales, base de aportes, base prestacional, base de
  retención u otra base definida); cantidad por un valor unitario derivado del
  salario (hora ordinaria, día, hora con recargo); tabla por rangos sobre una base
  (retención en la fuente, fondo de solidaridad); y suma o porcentaje de otros
  conceptos del mismo período. Cada forma MUST producir su explicación en términos de
  esa forma («base × factor», «tramo de tabla», «cantidad × unidad»). No existe un
  lenguaje de fórmulas libre: un cálculo que ninguna forma cubra es una forma nueva,
  no una expresión escrita por la administradora.
- **FR-010**: El sistema MUST NOT contener ningún porcentaje, tope, valor legal ni
  regla de negocio de nómina fijo en el programa: salario mínimo, auxilio de
  transporte y su tope, UVT, porcentajes de salud, pensión, fondo de solidaridad y sus
  rangos, ARL por clase de riesgo, parafiscales, provisiones, tabla de retención en la
  fuente y máximos de deducción MUST ser parámetros con vigencia.
- **FR-011**: El sistema MUST resolver cada parámetro legal por la vigencia que
  corresponde a la fecha final del período liquidado, y MUST negarse a calcular si
  algún parámetro requerido no tiene vigencia para esa fecha, nombrándolo.
- **FR-012**: El sistema MUST aplicar el auxilio de transporte, las provisiones de
  prestaciones, el fondo de solidaridad y la retención en la fuente únicamente cuando
  las condiciones parametrizadas se cumplan para cada empleado, sin marcas manuales.
- **FR-039**: La retención en la fuente por salarios MUST calcularse por
  **procedimiento 1**: base depurada del período (devengos gravados menos aportes
  obligatorios, deducciones y rentas exentas parametrizadas con sus topes) convertida a
  UVT y aplicada a la tabla por rangos vigente; la explicación MUST mostrar la base
  bruta, cada depuración, la base en UVT, el tramo y el resultado. Para un empleado
  marcado en **procedimiento 2**, el sistema MUST aplicar el porcentaje fijo registrado
  en su ficha con vigencia semestral sobre la misma base depurada, MUST explicarlo
  como tal, y MUST negarse a liquidarlo si no hay porcentaje vigente para la fecha del
  período, nombrándolo. Calcular ese porcentaje a partir de los doce meses anteriores
  MUST NOT hacer parte de esta feature.
- **FR-013**: El sistema MUST producir, para cada línea de liquidación, una
  explicación que identifique el concepto, la cantidad, la base y su valor, el factor
  o el tramo de tabla aplicado, el parámetro legal y su vigencia, y la novedad de
  origen cuando la haya.
- **FR-014**: El cálculo MUST ser repetible: calcular dos veces con los mismos datos
  MUST producir exactamente el mismo resultado, y cada recálculo MUST reemplazar por
  completo el borrador anterior indicando qué empleados cambiaron.
- **FR-015**: El sistema MUST garantizar que nunca existan dos cálculos simultáneos
  del mismo período ni un borrador incompleto: un cálculo o termina completo o no
  deja rastro.
- **FR-016**: El sistema MUST marcar un borrador como desactualizado cuando cambie
  cualquier novedad, salario, concepto o parámetro que influya en él, e impedir su
  aprobación hasta recalcular.
- **FR-017**: El sistema MUST redondear según el parámetro de la cooperativa, MUST
  garantizar que la suma de líneas coincide con los totales, y MUST imputar cualquier
  diferencia de redondeo a un concepto de ajuste explícito y visible.

**Revisión y aprobación**

- **FR-018**: El sistema MUST mostrar el borrador por empleado con sus líneas y
  explicaciones, los totales de la nómina por concepto, y un comparativo con el
  período anterior resaltando variaciones por encima de un umbral parametrizable.
- **FR-019**: El sistema MUST impedir la aprobación cuando algún empleado tenga neto
  negativo, deducciones por encima del máximo legal parametrizado, afiliaciones
  faltantes o conceptos sin cuenta contable, salvo excepción autorizada por una
  persona con el permiso correspondiente y con motivo registrado.
- **FR-020**: La aprobación MUST exigir confirmación explícita, MUST ser realizada por
  una persona con permiso de aprobación distinto del permiso de registrar novedades, y
  MUST dejar el período, sus novedades y su liquidación inmutables.
- **FR-021**: La cooperativa MUST poder decidir, por parámetro, si la misma persona
  puede registrar novedades y aprobar (cooperativas pequeñas); cuando lo permita, la
  aprobación MUST pedir una segunda confirmación y quedar marcada como aprobación sin
  segregación.
- **FR-022**: El sistema MUST rechazar cualquier intento de calcular, editar o
  registrar novedades sobre un período aprobado, y MUST ofrecer el camino correcto
  (ajuste en el siguiente período o reversión controlada).

**Contabilidad y pago**

- **FR-023**: Al aprobar, el sistema MUST generar en el mismo acto un comprobante
  contable de nómina cuadrado, con una línea por concepto y centro de costo según las
  cuentas asignadas a cada concepto, referenciado desde la liquidación; si la
  generación falla, la aprobación MUST NOT quedar registrada.
- **FR-024**: El comprobante contable de nómina MUST ser inmutable; cualquier
  corrección posterior MUST hacerse mediante comprobante de reversión.
- **FR-025**: El sistema MUST producir la relación de pago del período con neto,
  medio de pago, banco, tipo y número de cuenta por empleado, cuyo total coincida con
  el neto de la nómina.
- **FR-026**: El sistema MUST ofrecer el comprobante de pago de cada empleado con los
  valores exactos de su liquidación aprobada, descargable e imprimible de a uno o
  todos los del período en un solo documento. MUST permitir además el **envío manual
  por correo** a un empleado o a todo el período mediante una acción explícita de la
  responsable: MUST exigir que el empleado tenga correo en su ficha y listar a quienes
  no lo tienen para entrega manual, MUST registrar cada envío con destinatario, fecha,
  usuario y resultado, MUST permitir reintentar los fallidos, y MUST NOT enviar nada
  automáticamente al aprobar. Un envío fallido MUST NOT afectar la aprobación ni la
  marca de pago.
- **FR-040**: La relación de pago MUST permitir marcar como **pagado** al período
  completo o a empleados individuales, registrando fecha de pago, medio, referencia
  (número de transferencia o lote) y usuario; la marca MUST poder retirarse sólo con
  permiso y motivo; toda marca y su retiro MUST quedar en auditoría; y mientras exista
  al menos un empleado marcado como pagado, el período MUST NOT poder reversarse. La
  ejecución del pago (archivo bancario, cheques, transferencias) MUST NOT hacer parte de
  esta feature.

**Parametrización**

- **FR-027**: La administradora MUST poder crear y mantener conceptos indicando
  naturaleza, forma de cálculo, bases que afecta (salarial, prestacional, aportes,
  retención), si es prestacional, si admite repetirse en un período, topes, clases de
  empleado a las que aplica, y cuentas contables por centro de costo.
- **FR-028**: El sistema MUST validar la coherencia de las definiciones de conceptos
  (referencias existentes, sin ciclos, bases definidas) y MUST permitir probar un
  concepto sobre un empleado y período sin crear novedades ni liquidaciones.
- **FR-029**: Toda definición de concepto y todo parámetro legal MUST tener vigencia;
  modificarlos MUST crear una vigencia nueva y conservar la anterior, y las
  liquidaciones aprobadas MUST seguir mostrando los valores con los que se calcularon.
- **FR-038**: El sistema MUST entregar a cada cooperativa una **semilla curada** de
  conceptos legales colombianos estándar (salario básico, auxilio de transporte, horas
  extra y recargos, incapacidades, licencias, vacaciones, salud y pensión del
  empleado, fondo de solidaridad, retención en la fuente, aportes del empleador,
  parafiscales y provisiones de prestaciones) ya parametrizados con su forma de
  cálculo, sus bases y su naturaleza; la semilla MUST poder aplicarse más de una vez sin
  duplicar nada, MUST poder ajustarse y desactivarse por la cooperativa pero no
  borrarse, y MUST distinguirse en el catálogo de los conceptos propios. El catálogo de
  conceptos heredado del sistema anterior MUST conservarse como referencia de consulta
  y MUST NOT participar en ninguna liquidación; traducirlo a formas de cálculo es una
  acción manual, concepto a concepto, y nunca automática.

**Recurrencia, carga y reversión**

- **FR-030**: El sistema MUST permitir novedades recurrentes con número de cuotas o
  fecha final, incluirlas automáticamente en cada período hasta agotarse, y mostrar el
  número de cuota en cada liquidación.
- **FR-031**: El sistema MUST permitir cargar novedades desde un archivo con
  validación fila a fila; MUST rechazar el lote completo si alguna fila es inválida,
  enumerando fila y causa, y al aplicarlo MUST dejar el mismo historial que el
  registro individual.
- **FR-032**: El sistema MUST permitir reversar un período aprobado sólo a personas
  con permiso de reversión, con motivo, generando el comprobante contable de
  reversión, reabriendo el período y conservando la liquidación reversada como
  historial; MUST impedirlo si algún empleado del período está marcado como pagado en
  la relación de pago (FR-040) o si el período contable está cerrado.

**Transversales**

- **FR-033**: Toda escritura (novedad, cálculo, aprobación, reversión, cambio de
  concepto o parámetro, exportación) MUST quedar en el registro de auditoría con
  usuario, fecha, valores anteriores y nuevos.
- **FR-034**: Todo error MUST ser visible para quien opera, con causa y acción
  sugerida; ningún fallo de cálculo o contabilización MUST resolverse en silencio.
- **FR-035**: Todos los datos de nómina MUST pertenecer a la cooperativa activa y ser
  invisibles desde cualquier otra.
- **FR-036**: Las pantallas MUST validar antes de enviar y el servidor MUST validar de
  nuevo; los mensajes MUST coincidir.

**Planes de nómina**

- **FR-037**: La cooperativa MUST poder tener varios planes de nómina, cada uno con su
  periodicidad y su calendario de períodos; cada empleado MUST pertenecer a exactamente
  un plan; los períodos de un plan MUST NOT superponerse; toda liquidación MUST ser de
  un plan y un período. El sistema MUST crear un plan por defecto por cooperativa, y
  mientras exista un solo plan las pantallas MUST NOT pedir elegirlo. El cambio de plan
  de un empleado MUST tener fecha de efecto y regir desde el primer período del plan
  nuevo posterior a ella, sin dejarlo en dos períodos abiertos a la vez.

### Key Entities *(include if feature involves data)*

- **Plan de nómina**: grupo de empleados que se liquida junto, con su periodicidad
  (mensual o quincenal) y su calendario de períodos; pertenece a la única empresa
  pagadora de la cooperativa. Toda cooperativa tiene al menos uno (el plan por
  defecto, creado por el sistema); cada empleado pertenece a exactamente uno.
- **Período de pago**: intervalo con fecha inicial y final dentro del calendario de un
  plan de nómina, estado (abierto, calculado, aprobado, reversado); contiene sus
  novedades y, a lo sumo, una liquidación vigente. Dos períodos del mismo plan no se
  superponen.
- **Empleado (vigencias)**: la persona vinculada, con su plan de nómina, su historial
  de salario por fecha de efecto, tipo de contrato, fechas de ingreso y retiro, clase
  de empleado, afiliaciones (salud, pensión, riesgos laborales, cesantías), datos de
  pago, procedimiento de retención en la fuente (1 o 2) y, si es 2, su porcentaje fijo
  con vigencia semestral; además, sus deducciones y rentas exentas declaradas para la
  depuración de la base de retención, con vigencia. El cambio de plan tiene fecha de
  efecto y rige desde el primer período del plan nuevo que empiece después de ella.
- **Concepto de nómina**: qué se paga, descuenta, aporta o provisiona; naturaleza,
  forma de cálculo, bases que afecta, prestacional o no, reglas de repetición y
  topes, clases de empleado aplicables, cuentas contables; con vigencias. Tiene
  origen: **de la semilla estándar** (ajustable, desactivable, no borrable), **propio**
  de la cooperativa, o **traducido a mano** de un concepto heredado, que queda
  referenciado.
- **Concepto heredado**: fila del catálogo del sistema anterior, de sólo consulta; no
  participa en ninguna liquidación.
- **Parámetro legal**: valor con vigencia por fecha (salario mínimo, auxilio de
  transporte, UVT, porcentajes y rangos de aportes, tabla de retención por rangos en
  UVT, reglas y topes de depuración de la base de retención —aportes obligatorios,
  deducciones, rentas exentas—, máximos de deducción, clases de riesgo ARL).
- **Novedad**: hecho de un período para un empleado y concepto: cantidad o valor,
  fechas, observación, estado (vigente, corregida, anulada), origen (manual, archivo,
  recurrente, retroactivo), y su historial de versiones.
- **Novedad recurrente**: plantilla que genera novedades por período hasta un número
  de cuotas o fecha final.
- **Liquidación del período**: resultado de un cálculo: estado (borrador,
  desactualizado, aprobado, reversado), fecha, quién la calculó y quién la aprobó,
  totales, referencia al comprobante contable.
- **Liquidación del empleado**: por cada empleado en la liquidación: días liquidados,
  salario aplicado por tramo, líneas, totales y neto.
- **Línea de liquidación**: concepto, cantidad, base, factor o tramo, parámetro y
  vigencia usados, novedad de origen, valor; es la unidad de la explicación.
- **Comprobante contable de nómina**: asiento cuadrado por concepto y centro de
  costo, inmutable, referenciado desde la liquidación; y su reversión.
- **Relación de pago**: lista de netos por empleado con datos bancarios para un
  período aprobado; cada empleado lleva su estado de pago (pendiente o pagado) con
  fecha, medio, referencia y usuario. Cualquier empleado pagado bloquea la reversión
  del período.
- **Envío de comprobante**: registro de cada envío manual del comprobante de pago:
  empleado, correo destinatario, fecha, usuario y resultado (enviado, fallido con
  motivo, reintentado). No existe envío automático.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una liquidación de prueba con veinte empleados y casos representativos
  (ingreso a mitad de período, incapacidad, cambio de salario, salario integral,
  retención en la fuente) coincide al peso, en el 100 % de las líneas, con la
  liquidación hecha a mano por la contadora de la cooperativa.
- **SC-002**: El 100 % de las líneas de cualquier liquidación muestran una
  explicación con la que una persona ajena al cálculo puede reconstruir el valor sin
  preguntar a nadie.
- **SC-003**: Ningún valor legal ni porcentaje de nómina existe fijo en el programa;
  el cambio de salario mínimo, auxilio de transporte y tabla de retención de un año
  nuevo se hace sólo con parámetros, en menos de treinta minutos y sin despliegue.
- **SC-004**: Calcular un período de doscientos empleados toma menos de un minuto, y
  calcularlo dos veces con los mismos datos da el mismo resultado en el 100 % de los
  casos.
- **SC-005**: El 100 % de los períodos aprobados tienen un comprobante contable
  cuadrado cuyo total coincide con la nómina, y ninguno admite cambios después.
- **SC-006**: Una analista registra veinte novedades variadas en menos de quince
  minutos, y el 95 % de los intentos inválidos reciben un mensaje que le permite
  corregir sin ayuda.
- **SC-007**: Ninguna aprobación con neto negativo, deducción excesiva, afiliación
  faltante o concepto sin cuenta pasa sin una excepción autorizada y con motivo.
- **SC-008**: El 100 % de las novedades, cálculos, aprobaciones, reversiones y
  cambios de parámetros quedan en auditoría con antes y después, y el comparativo
  resalta el 100 % de las variaciones por encima del umbral.
- **SC-009**: La responsable de nómina de la cooperativa de prueba completa un ciclo
  completo (novedades → cálculo → revisión → aprobación → relación de pago) sin
  soporte técnico en su segundo período de uso.

## Assumptions

- **Alcance**: nómina ordinaria de cada período de pago. Quedan fuera y para features
  posteriores: liquidación definitiva por retiro, prima de servicios, consignación de
  cesantías e intereses, liquidación de vacaciones como proceso independiente,
  planilla PILA y nómina electrónica ante la DIAN. Esta feature deja las provisiones
  mensuales y las bases que esos procesos necesitan.
- **Legislación**: Colombia. Los valores concretos (porcentajes, topes, tablas) son
  datos con vigencia que carga la cooperativa o quien implementa; esta especificación
  no los fija.
- **Retención en la fuente**: procedimiento 1 completo (tabla por rangos en UVT sobre
  la base depurada). El procedimiento 2 se aplica con el porcentaje fijo que la
  cooperativa registra en la ficha del empleado con vigencia semestral; calcular ese
  porcentaje con los doce meses anteriores queda para una feature posterior, cuando
  exista historia liquidada en el sistema.
- **Periodicidad y planes**: una única empresa pagadora por cooperativa, con uno o
  varios planes de nómina; la periodicidad (mensual o quincenal) es de cada plan; base
  de 30 días por mes para proporcionar salario, como es práctica en el país. Liquidar
  nóminas de varias entidades pagadoras desde una misma cooperativa queda fuera de
  alcance.
- **Empleados**: se liquida a toda persona con vínculo laboral vigente en el período
  con la empresa pagadora de la cooperativa activa; contratistas por honorarios no
  hacen parte de la nómina. Las clases de empleado con reglas propias (aprendices,
  salario integral) se manejan por parametrización de conceptos, no por casos en el
  programa.
- **Datos existentes**: se reutilizan la ficha de empleado, los períodos de pago, los
  proveedores de salud, pensión, riesgos y cesantías, los parámetros de retención y de
  autoliquidación, y el comprobante de pago del empleado. El **catálogo de conceptos
  heredado** se conserva como referencia de consulta, no liquida: la primera nómina
  arranca con la semilla curada de conceptos estándar (FR-038) y con los conceptos
  propios que la cooperativa cree; lo heredado se traduce a mano sólo cuando haga
  falta. El cálculo preliminar con porcentajes fijos se retira; sus resultados no se
  conservan porque nunca se usaron en una nómina real.
- **Segregación de funciones**: por defecto, registrar novedades y aprobar son
  permisos distintos; la cooperativa puede permitir la misma persona por parámetro,
  con doble confirmación y marca en la auditoría.
- **Aprobación y contabilización** son un solo acto: no existe una nómina aprobada
  sin comprobante contable.
- **Pago**: la relación de pago registra dentro de nómina la marca de «pagado» por
  empleado (fecha, medio, referencia, usuario), y esa marca es lo que bloquea la
  reversión. La ejecución del pago (archivo bancario, cheques, transferencias) queda
  fuera del sistema en esta feature; tesorería no interviene. Integrar la relación de
  pago con tesorería es una feature posterior.
- **Comprobante de pago**: se descarga e imprime siempre; el envío por correo es una
  acción manual de la responsable, por empleado o por período, que depende de que el
  ambiente tenga correo saliente configurado y de que la ficha de la persona tenga
  correo. No hay envío automático. El correo del empleado es dato personal: se usa
  sólo para esto y cada envío queda registrado.
- **Retroactivos**: lo que se descubre después de aprobar se corrige en el período
  abierto siguiente con referencia al original; la reversión completa es la
  excepción, no la regla.
- **Redondeo**: al peso, con concepto de ajuste explícito; parametrizable.
- **Datos personales**: la información de nómina es sensible; sólo la ven quienes
  tienen permiso del módulo, y toda consulta de detalle queda en auditoría.
- **Auditoría, aislamiento por cooperativa, inmutabilidad contable, validación dual
  y errores visibles** se rigen por los principios de la constitución del proyecto
  (IV, VII, VIII, IX, X, XI); esta especificación los asume, no los redefine.
