# Feature Specification: Nómina completa — prestaciones, retiro, procedimiento 2, PILA, nómina electrónica y dispersión

**Feature Branch**: `010-nomina-prestaciones-pila-dian`

**Created**: 2026-09-20

**Status**: Draft

**Input**: User description: "Completar el módulo de nómina con los procesos que la feature 005 dejó explícitamente para después y que la cooperativa necesita para operar el año completo: prima de servicios, consignación anual de cesantías e intereses, liquidación de vacaciones, liquidación definitiva por retiro, retención procedimiento 2 automático, planilla PILA, nómina electrónica ante la DIAN e integración de la relación de pago con tesorería (dispersión bancaria). Todos consumen la nómina ordinaria ya en producción y contabilizan por el contrato único; ningún valor legal fijo en el código; corridas inmutables; cada proceso auditado y con permiso propio; exportación al centro de reportes; la constitución manda."

## Contexto

La nómina ordinaria (features 005 y 006) está en producción: períodos, novedades, cálculo con
motor puro y explicación por línea, aprobación que contabiliza el comprobante `NM`, relación de
pago, comprobante del empleado, reversión controlada, reportes. Deja cada mes las **provisiones**
de prima, cesantías, intereses y vacaciones y las **bases** (salarial, prestacional, de aportes,
de retención) que los procesos de esta feature consumen. La 005 dejó escrito lo que quedaba fuera
(Supuestos): liquidación definitiva por retiro, prima de servicios, consignación de cesantías e
intereses, liquidación de vacaciones como proceso aparte, planilla PILA, nómina electrónica ante
la DIAN, el porcentaje del procedimiento 2 calculado con los doce meses anteriores, y la ejecución
del pago con tesorería. Esta feature los completa.

Quien opera es la responsable de nómina de la cooperativa (COOFLOPAL: once empleados, nómina
quincenal, salida en vivo el 1 de diciembre de 2026) con la contadora. El calendario manda: la
prima del segundo semestre se paga antes del 20 de diciembre, los intereses a las cesantías antes
del 31 de enero, las cesantías se consignan antes del 15 de febrero, la PILA se paga cada mes
según el NIT, el documento de nómina electrónica se transmite en los diez primeros días del mes
siguiente (calendario según la DIAN; la contadora confirma), y un retiro exige la liquidación al
terminar el contrato. Todo esto la cooperativa lo hace hoy a mano o en otros programas.

## Clarifications

### Session 2026-09-20

- Q: ¿Alcance? → A: **Todo lo que la 005 dejó pendiente**, incluida la nómina electrónica ante
  la DIAN y la PILA (decisión del dueño). Se organiza en ocho historias con prioridad por
  calendario legal: primero lo que vence antes del 1 de diciembre de 2026 y en el arranque de 2027
  (retiro, prima, cesantías e intereses), luego vacaciones, PILA y nómina electrónica, y por
  último procedimiento 2 automático y dispersión bancaria.
- Q: ¿Cómo firma y transmite la cooperativa la nómina electrónica a la DIAN? → A: **Por un
  servicio centralizado de Ingenia365**, uno solo para todas las cooperativas, que **no guarda
  datos de ningún cliente**: sólo completa, firma y transmite. La habilitación de cada cooperativa
  (NIT, modo, ambiente, identificación del software, set de pruebas y su estado) y todos sus
  documentos (XML, respuesta de la DIAN, CUNE, estados, intentos, notas de ajuste) viven **en la
  base de esa cooperativa** (Principio IV), escritos por el ERP; el ERP construye el documento, lo
  entrega al servicio identificándose como esa cooperativa, y guarda lo que el servicio devuelve.
  El servicio **arranca en modo «software propio de cada cooperativa»**: cada cooperativa registra
  el ERP como su software en el catálogo de la DIAN, aporta su propio certificado de firma y el
  servicio firma en su nombre como empleador. El modo **«proveedor tecnológico de Ingenia365»**
  (certificado y software de Ingenia365, firma como tercero) se activa por configuración, por
  cooperativa, cuando exista la habilitación de Ingenia365 como PT, que exige ser ya PT de factura
  electrónica, patrimonio de al menos 20.000 UVT, ISO 27001 (o compromiso) y visita de la DIAN. Los
  secretos (certificados y sus contraseñas, PIN de cada cooperativa, credenciales) los monta el
  dueño en el servicio y la habilitación los referencia **por nombre**; nunca están en el ERP ni
  en el repositorio. Los trámites ante la DIAN —el registro de cada cooperativa y su software, y el
  de Ingenia365 como PT cuando se decida— son del dueño y quedan fuera del programa.
- Q: ¿Operador de PILA y planillas? → A: **Aportes en Línea, planilla E** (empleados). Las
  correcciones (planilla N) se digitan en el operador. La prueba de aceptación es el validador de
  archivos de Aportes en Línea.
- Q: ¿Banco pagador para la dispersión? → A: **Banco AV Villas**. El primer formato se construye
  con la estructura del archivo plano de pagos de AV Villas Empresas que publique el banco (el dueño
  la aporta); el formato queda parametrizable para otros bancos.
- Q: ¿Cómo se aplica la retención en la fuente a las liquidaciones especiales? → A: **Automática
  según la norma tributaria**, con topes y tarifas como parámetros con vigencia y explicación por
  valor: prima con retención independiente en procedimiento 1 (sumada al ingreso en el 2);
  cesantías e intereses exentas cuando el ingreso mensual promedio del empleado está bajo el tope
  del art. 206 ET y gravadas en la parte que la norma indique; indemnización con la tarifa del
  art. 401-3 ET cuando el ingreso supera el tope; vacaciones y salario pendiente con la retención
  ordinaria del procedimiento del empleado.
- Q: ¿Qué se descuenta en la liquidación definitiva por deudas del empleado? → A: **El saldo
  total de los préstamos de la cooperativa**, hasta donde alcance la liquidación, propuesto
  automáticamente desde Cartera con el detalle de cada obligación para que la responsable **valide
  los saldos** antes de aprobar; ella puede **modificar** el valor hacia abajo con motivo, y todo
  (saldo propuesto, valor aplicado, motivo, quién y cuándo) queda **auditado**. Las libranzas con
  terceros sólo las cuotas ya causadas y no descontadas; el resto lo cobra el tercero.
- Q: ¿La prima y los intereses a las cesantías se pagan aparte o dentro de la quincena? → A:
  **Pago aparte**: cada liquidación especial tiene su relación de pago, su archivo de dispersión y
  su comprobante para el empleado, con fecha propia; la nómina ordinaria no la incluye. En el
  documento de nómina electrónica del mes se suma con lo ordinario pagado en ese mes.
- Q: ¿La empresa está exonerada de aportes a SENA, ICBF y salud del empleador (art. 114-1 ET)? →
  A: **COOFLOPAL sí** (cooperativa contribuyente del régimen tributario especial), pero la
  exoneración es un **parámetro por empresa con vigencia** —«exonerada: sí/no» y el umbral de
  salarios mínimos—, porque no todas las entidades que usarán el aplicativo son cooperativas ni
  están exoneradas; la PILA y la nómina ordinaria leen el mismo parámetro. La norma respalda la
  respuesta: la Ley 1955 de 2019 (art. 204) añadió al parágrafo 2 del art. 114-1 ET que las
  entidades del art. 19-4 ET —las cooperativas— conservan el derecho a la exoneración; el
  comentario que quedó sembrado en el código de la nómina ordinaria dice lo contrario y está
  desactualizado. **La contadora confirma** el valor antes de fijarlo para COOFLOPAL.
- Q: ¿El sábado cuenta como día hábil para las vacaciones? → A: **Sí por defecto** (lunes a
  sábado; sólo domingos y festivos no cuentan), pero es un **parámetro por empresa con vigencia**
  («semana laboral: lunes a viernes / lunes a sábado»), porque hay empresas que trabajan de lunes a
  viernes y otras de lunes a sábado; el calendario de festivos también es parámetro.
- Q: ¿Reglas laborales cuando la ley admite variantes? → A (por defecto, ver Supuestos): se
  aplica la norma colombiana general (Código Sustantivo del Trabajo, Ley 50 de 1990, Ley 52 de
  1975, Estatuto Tributario art. 385–386, Ley 1607 de 2012 para exoneraciones); toda variante que
  la cooperativa pacte (convención, reglamento) entra como **parámetro con vigencia**, nunca como
  cambio del programa.
- Q: ¿Qué corrigió la investigación (Fase 0, `research.md`)? → A: ocho puntos, ya incorporados
  en este documento: **(1) aprendices por etapa** —la Ley 2466 de 2025 volvió el contrato de
  aprendizaje un contrato laboral especial: en etapa lectiva sin prestaciones y con cotizante PILA
  19; en etapa práctica con prestaciones y aportes completos, cotizante 1—, así que la exclusión
  en bloque sólo vale para la etapa lectiva; **(2) procedimiento 2**: el art. 386 ET manda «dividir
  por 13» la suma de los doce meses (o por los meses de vinculación si son menos), no promediar
  por doce; **(3) plazo de la nómina electrónica**: «diez primeros días del mes siguiente», sin
  «hábiles» (calendario según la DIAN; la contadora confirma); **(4) marco normativo DIAN**: la
  Res. 013/2021 quedó compilada en la Res. Única 000227 de 2025 y así se cita; **(5) servicio
  central**: arranca en modo «software propio de cada cooperativa», el modo «proveedor tecnológico
  de Ingenia365» se activa después, y ni la habilitación ni los documentos viven en el servicio
  (cada cooperativa los guarda en su base); **(6) fondo de solidaridad pensional**: dos tablas con
  vigencia (Ley 797/2003 hasta el 31-03-2027; Ley 2381/2024 desde el 01-04-2027) y bandera de
  régimen de transición por empleado; **(7) redondeo PILA** (IBC al peso superior, aportes al
  múltiplo de 100 superior) como parámetro con vigencia; **(8) exoneración art. 114-1 ET**: la
  norma respalda que la cooperativa esté exonerada; la contadora confirma.

## Alcance

### Dentro de esta feature

- Liquidación de **prima de servicios** por semestre, con revisión, aprobación, contabilización
  contra la provisión, relación de pago y comprobante del empleado.
- **Cesantías e intereses** a 31 de diciembre: liquidación, pago de intereses al empleado,
  relación de consignación por fondo, contabilización contra la provisión.
- **Vacaciones**: registro del disfrute y de la compensación en dinero, liquidación,
  contabilización contra la provisión y la novedad de ausencia en el período de nómina.
- **Liquidación definitiva** por terminación del contrato, con indemnización parametrizable,
  documento para firma y cierre de la ficha.
- **Procedimiento 2**: cálculo semestral automático del porcentaje fijo de retención con su
  explicación, guardado con vigencia en la ficha.
- **Planilla PILA**: archivo plano de autoliquidación por período con validación previa.
- **Nómina electrónica**: documento soporte y notas de ajuste por empleado y período, firma,
  transmisión, CUNE y estado; parametrización de habilitación.
- **Dispersión bancaria**: archivo plano por banco con formato parametrizable y marca de pagado
  desde el archivo.
- Permisos propios, auditoría, explicación de cada valor, exportación por el centro de reportes,
  parámetros legales nuevos con vigencia.

### Fuera de esta feature

- Contratos y prestaciones que no existen en la cooperativa: trabajadores del servicio doméstico,
  salario por días o destajo, trabajadores en misión, pensionados por la cooperativa.
- Cálculo de la retención procedimiento 1 (ya existe) y las deducciones/rentas exentas nuevas
  (se parametrizan con lo que ya hay).
- Conexión directa con los bancos, con el operador de PILA o con el fondo de cesantías: los
  archivos se generan y la persona los carga; sólo la DIAN, a través del servicio centralizado de
  Ingenia365, recibe transmisión directa.
- Recibir y contabilizar respuestas de los bancos (rechazos de transferencia): la marca de pagado
  se corrige a mano como hoy.
- Historia previa al arranque: las provisiones acumuladas antes del 1 de diciembre de 2026 entran
  por el comprobante de apertura contable y por los **saldos iniciales de prestaciones** por
  empleado que esta feature deja digitar (días de vacaciones pendientes, cesantías acumuladas del
  año, prima acumulada del semestre) para que las primeras liquidaciones no salgan cortas.

### Glosario

- **Base prestacional**: salario más todo lo que constituye salario (horas extras, recargos,
  comisiones, bonificaciones salariales) más el auxilio de transporte cuando la ley lo incluye
  (prima y cesantías sí; vacaciones no). La nómina ordinaria ya la marca concepto por concepto.
- **Provisión**: lo que la nómina ordinaria acumula cada período por prima, cesantías, intereses y
  vacaciones. Liquidar una prestación **consume** la provisión y contabiliza la diferencia.
- **Liquidación especial**: una corrida de nómina que no es ordinaria (prima, cesantías e
  intereses, vacaciones, definitiva), con las mismas propiedades: borrador, versiones,
  explicación, aprobación que contabiliza, reversión.
- **Procedimiento 2**: método de retención en la fuente por salarios con un porcentaje fijo que se
  calcula cada semestre con los ingresos de los doce meses anteriores, cuya suma se **divide por
  13** (la prima es el mes trece) o por los meses de vinculación cuando son menos de doce
  (art. 386 ET).
- **PILA**: Planilla Integrada de Liquidación de Aportes; archivo plano que se carga en un operador
  de información (Aportes en Línea, SOI, Asopagos…) para pagar salud, pensión, riesgos laborales,
  caja de compensación, SENA e ICBF.
- **Documento soporte de nómina electrónica**: el XML mensual por empleado que se transmite a la
  DIAN con el pago de nómina; la **nota de ajuste** lo corrige o lo elimina; el **CUNE** es el
  código único que la DIAN devuelve al validarlo.
- **Dispersión bancaria**: archivo plano con las transferencias de la nómina que se carga en el
  portal del banco de la cooperativa.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Liquidar la prima de servicios del semestre (Priority: P1)

En junio y en diciembre la responsable abre «Prima de servicios», elige el semestre, y el
sistema calcula para cada empleado con derecho la prima proporcional a los días trabajados del
semestre sobre la base prestacional (el salario del momento, o el promedio del semestre cuando
varió), descontando lo ya pagado del mismo semestre (por ejemplo, la prima proporcional de una
liquidación definitiva). Revisa cada valor con su explicación, corrige novedades si hace falta,
recalcula y aprueba; al aprobar se contabiliza contra la provisión acumulada y la diferencia va
al gasto; la prima queda en la relación de pago del semestre y en el comprobante del empleado.

**Why this priority**: es la primera obligación que vence tras la salida en vivo (20 de
diciembre de 2026) y la de mayor valor por empleado.

**Independent Test**: con dos empleados —uno todo el semestre con salario fijo, otro con ingreso a
mitad de semestre y un cambio de salario— liquidar la prima de diciembre y comprobar los valores
a mano, el comprobante contable (provisión cancelada, diferencia al gasto) y el comprobante de
pago.

**Acceptance Scenarios**:

1. **Given** un empleado con salario fijo todo el semestre, **When** liquida la prima, **Then** el
   valor es la base prestacional × días del semestre / 360 y la explicación muestra base, días y
   fórmula.
2. **Given** un empleado con dos salarios en el semestre, **When** liquida, **Then** la base es el
   promedio ponderado del semestre y la explicación lo dice mes a mes.
3. **Given** un empleado con salario integral o un aprendiz en etapa lectiva, **When** liquida,
   **Then** no aparece y la lista dice por qué; **Given** un aprendiz en etapa práctica, **Then**
   sí aparece, con prima completa sobre su apoyo de sostenimiento.
4. **Given** un empleado que ya recibió prima proporcional en una liquidación definitiva del
   mismo semestre, **When** liquida, **Then** no se le vuelve a pagar.
5. **Given** la prima aprobada, **Then** existe un comprobante contable con la provisión cancelada,
   la diferencia en el gasto y la cuenta por pagar al empleado, y el período ordinario no se
   afecta.
6. **Given** la prima aprobada y aún no pagada, **When** la reversa con motivo, **Then** queda el
   asiento espejo y se puede liquidar de nuevo.

---

### User Story 2 - Liquidar cesantías e intereses del año y armar la consignación (Priority: P1)

A 31 de diciembre la responsable liquida las cesantías del año de cada empleado (base
prestacional × días / 360, con la base del último salario si no cambió en los últimos tres meses
o el promedio del año si cambió) y los intereses (12 % anual sobre las cesantías, proporcional a
los días). Los intereses se pagan al empleado en enero con su comprobante; las cesantías se
**consignan** al fondo de cada uno antes del 15 de febrero: el sistema arma la relación por fondo
(fondo, empleado, documento, valor) y el archivo que el fondo acepte. Todo contabiliza contra las
provisiones.

**Why this priority**: obligación legal de enero y febrero de 2027 con sanción por día de
retraso; primera vez que la cooperativa lo hará con el sistema.

**Independent Test**: dos empleados (uno con salario estable, otro con aumento en noviembre) y
un tercero que ingresó en septiembre; liquidar a 31/12, comprobar cesantías e intereses a mano,
la relación por fondo con los dos fondos distintos, el pago de intereses en la relación de pago de
enero y los comprobantes contables.

**Acceptance Scenarios**:

1. **Given** un empleado con salario estable en los últimos tres meses, **When** liquida, **Then**
   la base es el último salario más auxilio de transporte; **Given** un cambio de salario en los
   últimos tres meses, **Then** la base es el promedio del año trabajado, y la explicación lo dice.
2. **Given** un empleado que ingresó en septiembre, **When** liquida, **Then** cesantías e
   intereses son proporcionales a los días trabajados del año.
3. **Given** cesantías liquidadas, **Then** los intereses son el 12 % anual proporcional (parámetro
   con vigencia) y aparecen como concepto a pagar del empleado en la relación de pago de enero.
4. **Given** dos fondos de cesantías distintos entre los empleados, **When** genera la
   consignación, **Then** hay una relación por fondo con los totales que cuadran con la
   liquidación, exportable.
5. **Given** un empleado con salario integral, **Then** no acumula cesantías ni intereses y la
   lista lo dice.
6. **Given** la liquidación aprobada, **Then** el comprobante contable cancela las provisiones de
   cesantías e intereses y deja la cuenta por pagar al fondo y al empleado.
7. **Given** un empleado retirado durante el año con liquidación definitiva, **Then** no aparece:
   sus cesantías ya se pagaron con el retiro.

---

### User Story 3 - Liquidación definitiva por terminación del contrato (Priority: P1)

Cuando un empleado se retira, la responsable registra la terminación (fecha, motivo, tipo de
contrato) y el sistema liquida en un solo documento: salario de los días pendientes, cesantías e
intereses del período causado, prima proporcional del semestre, vacaciones causadas y no
disfrutadas, indemnización cuando el motivo y el tipo de contrato la generan (tabla
parametrizable), y las deducciones pendientes (préstamos de la cooperativa, libranzas
autorizadas). Cada valor trae su explicación. Al aprobar se contabiliza contra las provisiones y
se imprime el documento de liquidación para la firma del empleado; la ficha queda cerrada y no
entra en más nóminas.

**Why this priority**: sin esto un retiro obliga a liquidar a mano y a registrar el pago fuera del
sistema; puede ocurrir en cualquier momento desde el 1 de diciembre.

**Independent Test**: retirar a un empleado con contrato indefinido, dos años y tres meses de
antigüedad, sin justa causa, con seis días de vacaciones tomados y un préstamo pendiente;
comprobar cada rubro a mano (incluida la indemnización), el documento impreso, el comprobante
contable y que la siguiente nómina ordinaria no lo incluya.

**Acceptance Scenarios**:

1. **Given** la fecha de retiro a mitad de un período abierto, **When** liquida, **Then** el salario
   cubre sólo los días trabajados del período y ese empleado no se liquida en la nómina ordinaria
   del período.
2. **Given** vacaciones causadas y parcialmente disfrutadas, **Then** se pagan los días pendientes
   sobre el salario base del retiro.
3. **Given** contrato indefinido terminado sin justa causa por el empleador, **Then** la
   indemnización sigue la tabla parametrizada por antigüedad y rango salarial; **Given** renuncia
   o justa causa, **Then** no hay indemnización y la explicación lo dice; **Given** contrato a
   término fijo, **Then** la indemnización es el tiempo que faltaba.
4. **Given** un préstamo de la cooperativa con saldo, **When** liquida, **Then** el sistema
   propone descontar el saldo total con el detalle de la obligación (capital, intereses, cuotas)
   para validarlo; la responsable puede bajarlo con motivo y el descuento aplicado, el propuesto y
   el motivo quedan en la explicación y en la auditoría; una libranza con un tercero sólo descuenta
   las cuotas causadas y no descontadas.
5. **Given** la liquidación aprobada, **Then** existe el comprobante contable, el documento para
   firma (empresa, empleado, fechas, cada rubro, total, espacio de firmas) y la ficha marca la
   terminación; una nómina ordinaria posterior no lo incluye.
6. **Given** un error después de aprobar, **When** reversa con motivo, **Then** asiento espejo, la
   ficha vuelve a estar vigente y se puede liquidar de nuevo.

---

### User Story 4 - Registrar y liquidar vacaciones (Priority: P2)

La responsable consulta los días de vacaciones causados y pendientes de cada empleado (15 días
hábiles por año, proporcionales), registra un **disfrute** con fechas (el sistema cuenta los días
hábiles según el calendario parametrizado y los descuenta) o una **compensación en dinero** hasta
lo que la ley permite, y liquida: el valor sale del salario ordinario del momento sin auxilio de
transporte ni horas extras (o del promedio del último año cuando el salario es variable). Al
aprobar contabiliza contra la provisión y deja la novedad de ausencia en el período de nómina que
cubre las fechas, para que la nómina ordinaria pague esos días como vacaciones y no como salario.

**Why this priority**: necesaria para el ciclo anual y para que la definitiva tenga de dónde leer
lo disfrutado; después de las obligaciones con fecha fija.

**Independent Test**: empleado con dos años de antigüedad y sin vacaciones tomadas; registrar un
disfrute de 10 días hábiles que cruza fin de mes; comprobar el saldo de días, el valor, la novedad
en los dos períodos de nómina y el comprobante contable.

**Acceptance Scenarios**:

1. **Given** un empleado con 18 meses de antigüedad, **When** consulta, **Then** ve 22,5 días
   causados, menos los disfrutados y compensados.
2. **Given** un disfrute del 28 de octubre al 10 de noviembre, **When** liquida, **Then** los días
   hábiles se cuentan con el calendario (festivos de Colombia y la semana laboral parametrizada:
   con «lunes a sábado» son 11 hábiles y con «lunes a viernes» 9, descontando el festivo del 2 de
   noviembre), la novedad de vacaciones queda en los dos períodos que cubre y la nómina ordinaria
   de esos períodos no paga esos días como salario.
3. **Given** una compensación en dinero que supera la mitad de las vacaciones causadas, **When**
   registra, **Then** rechazo con el máximo permitido.
4. **Given** la liquidación aprobada, **Then** el comprobante contable cancela la provisión y
   registra la diferencia y la cuenta por pagar.

---

### User Story 5 - Generar la planilla PILA del período (Priority: P2)

Cada mes la responsable genera la PILA desde las nóminas aprobadas del mes: el sistema arma por
cotizante los días, novedades (ingreso, retiro, variación de salario, incapacidad, licencia,
vacaciones), el ingreso base de cotización, y los aportes de salud, pensión, fondo de
solidaridad, riesgos laborales, caja, SENA e ICBF con las tarifas y exoneraciones vigentes;
valida antes (documento, afiliaciones, tarifas, topes) y entrega la lista de inconsistencias con
enlace a la ficha; genera el archivo plano en el formato del operador y lo registra (quién,
cuándo, qué totales) para cargarlo en el operador. Una regeneración deja la anterior como
versión.

**Why this priority**: obligación mensual con intereses de mora; hoy se digita a mano en el
operador.

**Independent Test**: un mes con once empleados, uno con ingreso el 10, uno con incapacidad de
tres días y uno retirado el 20; generar, comprobar contra el cálculo manual las bases y aportes
por cotizante, y que el archivo lo acepte el validador del operador. El operador es **Aportes en Línea** y la planilla es la **E**; su validador de archivos es la prueba de aceptación real.

**Acceptance Scenarios**:

1. **Given** las nóminas del mes aprobadas, **When** genera la PILA, **Then** cada cotizante tiene
   días cotizados, IBC y aportes que coinciden con lo liquidado, y los totales del archivo cuadran
   con la suma de los comprobantes `NM` del mes.
2. **Given** un empleado sin EPS afiliada en la ficha, **When** valida, **Then** la inconsistencia
   bloquea la generación y enlaza a la ficha.
3. **Given** una empresa marcada como exonerada (art. 114-1 ET) para empleados que ganan menos del
   umbral de salarios mínimos parametrizado, **When** genera, **Then** esos aportes de salud del
   empleador, SENA e ICBF van en cero con la marca de exoneración y quien gana el umbral o más sí
   aporta; **Given** una empresa no exonerada, **Then** todos aportan.
4. **Given** una PILA ya generada, **When** regenera tras corregir una nómina, **Then** la anterior
   queda como versión consultable y la nueva es la vigente.

---

### User Story 6 - Transmitir la nómina electrónica a la DIAN (Priority: P2)

Cada mes la responsable genera el documento soporte de pago de nómina electrónica por empleado
con lo devengado y deducido del mes (sumando las nóminas ordinarias y las liquidaciones
especiales pagadas), lo revisa, lo **firma y transmite** con una acción explícita, y ve el
estado que la DIAN devuelve (aceptado con CUNE, rechazado con los errores, en proceso); puede
reintentar, consultar el histórico y descargar el XML y su representación gráfica. Si un mes ya
transmitido cambia (reversión, corrección), genera la **nota de ajuste** de reemplazo o
eliminación. La cooperativa parametriza una vez su habilitación: NIT, rangos de numeración,
identificación del software, ambiente (habilitación o producción), modo de firma («software propio»,
con su certificado, o «proveedor tecnológico de Ingenia365» cuando exista). La firma y la
transmisión las hace el **servicio centralizado de nómina electrónica de Ingenia365**, que atiende
a varias cooperativas sin guardar los datos de ninguna: la habilitación y los documentos quedan en
la base de cada cooperativa.

**Why this priority**: obligación mensual (Res. 013/2021, compilada en la Res. Única 000227 de
2025) con sanción por no transmitir; la cooperativa debe cumplirla desde el primer mes de nómina
en el sistema, dentro de los diez primeros días del mes siguiente.

**Independent Test**: un mes con dos empleados (uno con horas extras y una deducción de
libranza); generar los documentos, validarlos contra el anexo técnico vigente y transmitirlos al
ambiente de habilitación de la DIAN a través del servicio central en modo software propio,
recibir CUNE; reversar una nómina y generar la nota de ajuste de eliminación.

**Acceptance Scenarios**:

1. **Given** las nóminas del mes aprobadas y pagadas, **When** genera, **Then** hay un documento
   por empleado con devengados, deducciones, total y fechas conforme al anexo técnico, con
   numeración consecutiva del rango parametrizado.
2. **Given** los documentos generados, **When** la responsable transmite, **Then** cada documento
   queda con su estado y, si fue aceptado, con el CUNE y la fecha; nada se transmite sin la acción
   explícita.
3. **Given** un rechazo de la DIAN, **Then** la responsable ve los errores en lenguaje llano,
   corrige y reintenta; el documento rechazado conserva su historial.
4. **Given** una nómina del mes ya transmitido que se reversa, **When** genera la nota de ajuste,
   **Then** la nota referencia el documento original y la DIAN la acepta.
5. **Given** la parametrización de habilitación incompleta, **When** intenta transmitir, **Then**
   rechazo con lo que falta.

---

### User Story 7 - Calcular el porcentaje fijo de retención (procedimiento 2) (Priority: P3)

En junio y en diciembre la responsable pide el cálculo del porcentaje fijo para los empleados
que están en procedimiento 2: el sistema toma los ingresos laborales de los doce meses
anteriores (o los meses trabajados), los depura con las mismas deducciones y rentas exentas que
la nómina ordinaria aplica, divide la suma por 13 (o por los meses de vinculación cuando son
menos de doce) para obtener el ingreso mensual en UVT, busca la retención teórica en la tabla y
calcula el porcentaje; lo muestra con su explicación mes a mes y, al
aprobar, lo guarda en la ficha con vigencia del semestre siguiente. La nómina ordinaria lo aplica
como hoy.

**Why this priority**: sólo aplica a empleados con procedimiento 2 (pocos en una cooperativa) y
la ficha ya admite el porcentaje digitado.

**Independent Test**: un empleado con doce meses de historia liquidada en el sistema, con un
mes de comisiones altas; calcular y comprobar el porcentaje a mano con la tabla vigente; aprobar y
ver que la siguiente nómina lo aplica.

**Acceptance Scenarios**:

1. **Given** doce meses liquidados, **When** calcula, **Then** el porcentaje coincide con el
   cálculo manual y la explicación lista los doce meses, la depuración y la tabla usada.
2. **Given** doce meses completos, **Then** la suma se divide por 13, y la explicación lo dice;
   **Given** menos de doce meses, **Then** se divide por los meses de vinculación y lo dice.
3. **Given** el porcentaje aprobado, **Then** queda en la ficha con vigencia julio–diciembre o
   enero–junio y el anterior se cierra; la nómina siguiente lo aplica.

---

### User Story 8 - Pagar por archivo de dispersión bancaria (Priority: P3)

Desde la relación de pago, la responsable genera el archivo plano de transferencias para el
banco de la cooperativa con el formato parametrizado (columnas, separador, longitudes, cuenta
origen), lo descarga y lo carga en el portal del banco; al confirmar que el banco lo procesó,
marca el archivo como enviado y todos los empleados del archivo quedan pagados con la fecha, el
medio y la referencia del archivo, en vez de marcarlos uno a uno. Un empleado sin cuenta bancaria
en la ficha queda fuera del archivo y en la lista de pendientes para pagar por otro medio.
El banco es **AV Villas**: el primer formato es el de su archivo plano de pagos empresariales, y el formato queda parametrizable para otros bancos.

**Why this priority**: ahorra la digitación del pago pero hoy ya existe la marca manual; depende
del banco y su formato.

**Independent Test**: relación de pago con tres empleados, uno sin cuenta; generar el archivo,
verificar estructura y totales, marcar enviado y comprobar que dos quedan pagados con la misma
referencia y el tercero aparece en pendientes.

**Acceptance Scenarios**:

1. **Given** la relación de pago aprobada, **When** genera el archivo, **Then** contiene una línea
   por empleado con cuenta, en el formato parametrizado, con totales que cuadran con la relación.
2. **Given** el archivo enviado, **When** lo marca como enviado, **Then** los empleados del archivo
   quedan pagados con fecha, medio «transferencia» y la referencia del archivo, y la reversión de
   esa nómina queda bloqueada como hoy.
3. **Given** un empleado sin cuenta bancaria, **Then** no va en el archivo y aparece en pendientes.

---

### Edge Cases

- Empleado que ingresó y se retiró dentro del mismo semestre: prima, cesantías, intereses y
  vacaciones proporcionales en la definitiva; el semestre no lo vuelve a liquidar.
- Prima o cesantías con la provisión acumulada **menor** que lo liquidado (aumento de salario
  reciente): la diferencia va al gasto del período de la liquidación; **mayor**: se libera.
- Retiro con fecha en un período ya aprobado: rechazo; se reversa el período o se liquida con
  fecha en el período abierto, y la explicación lo dice.
- Vacaciones que cruzan un período cerrado o aprobado: la novedad se rechaza para ese período y se
  ofrece el ajuste retroactivo, como la nómina ordinaria.
- Dos liquidaciones especiales del mismo tipo y período para el mismo empleado: la segunda se
  rechaza salvo que la primera esté reversada.
- Empleado con salario integral: sin prima, cesantías ni intereses; vacaciones sí; procedimiento
  2 y PILA sí (IBC al 70 %).
- Aprendiz (Ley 2466 de 2025, contrato laboral especial a término fijo) **según su etapa**, que
  la ficha registra con vigencia: en **etapa lectiva** apoyo de sostenimiento del porcentaje
  parametrizado del salario mínimo, salud y riesgos a cargo del empleador, sin prestaciones ni
  aportes a pensión ni caja, PILA con cotizante **19**; en **etapa práctica** apoyo del 100 % del
  mínimo y prestaciones y aportes **completos** (prima, cesantías, intereses, vacaciones, pensión),
  PILA con cotizante **1**. Sólo la etapa lectiva queda fuera de las liquidaciones especiales; si el
  aprendiz en práctica causa caja, SENA e ICBF lo confirma la contadora y entra como parámetro.
  Pasante sin contrato de aprendizaje: sin prestaciones, como hoy.
- Cambio de tarifa de riesgos laborales a mitad de mes: PILA con la tarifa vigente al primer día
  del período (parámetro con vigencia).
- PILA con un empleado con dos novedades excluyentes (ingreso y retiro el mismo mes): ambas
  marcas y días exactos.
- Documento de nómina electrónica de un empleado sin correo o sin dirección: se genera igual (la
  DIAN no los exige) pero la representación gráfica lo dice.
- La DIAN o el servicio central no responden: el documento queda «en proceso» con reintento manual;
  nunca se marca aceptado sin CUNE.
- Cambio de banco o de formato a mitad de año: el formato tiene vigencia; los archivos anteriores
  conservan el suyo.
- Provisiones anteriores al arranque (noviembre de 2026 hacia atrás): sin saldos iniciales de
  prestaciones cargados, la prima de diciembre y las cesantías de 2026 saldrían cortas; el sistema
  lo advierte en la liquidación cuando el empleado tiene ingreso anterior al arranque y sin saldo
  inicial registrado.

## Requirements *(mandatory)*

### Functional Requirements

**Liquidaciones especiales (común)**

- **FR-001**: El sistema MUST ofrecer cuatro liquidaciones especiales —prima de servicios,
  cesantías e intereses, vacaciones y definitiva— con el mismo ciclo que la nómina ordinaria:
  borrador calculado, explicación por valor, recálculo con versión, aprobación que contabiliza en
  la misma transacción, relación de pago, comprobante del empleado y reversión con asiento espejo.
- **FR-002**: Cada valor de una liquidación especial MUST traer su explicación (base, días,
  parámetro con vigencia, fórmula), con el mismo detalle que las líneas de la nómina ordinaria.
- **FR-003**: Ningún valor legal (días de prima, porcentaje de intereses, días de vacaciones,
  tablas de indemnización, tarifas de aportes, topes, exoneraciones, UVT) MUST estar escrito en el
  programa: todo es parámetro con vigencia que la cooperativa o quien implementa carga.
- **FR-004**: Al aprobar, el comprobante contable MUST cancelar la provisión acumulada del
  concepto para ese empleado y llevar la diferencia al gasto (o liberarla), usando las cuentas
  parametrizadas por concepto y el tercero del empleado, por el contrato único de contabilidad.
- **FR-005**: El sistema MUST rechazar una segunda liquidación especial del mismo tipo, período y
  empleado mientras la anterior no esté reversada, y MUST impedir liquidar a un empleado en una
  nómina ordinaria de un período posterior a su retiro.
- **FR-006**: Toda liquidación especial MUST respetar permisos propios (registrar, aprobar,
  reversar) y quedar auditada; la segregación de funciones de la nómina ordinaria aplica igual.
- **FR-006a**: Cada liquidación especial MUST calcular la retención en la fuente que le
  corresponde según la norma, con topes y tarifas parametrizados con vigencia y explicación: la
  prima con retención independiente sobre ella sola en procedimiento 1 y sumada al ingreso del mes
  en procedimiento 2; las cesantías y sus intereses exentas cuando el ingreso mensual promedio de
  los últimos seis meses no supera el tope parametrizado (art. 206 ET) y gravadas en la parte que
  la norma indique cuando lo supera; la indemnización con la tarifa parametrizada (art. 401-3 ET)
  cuando el ingreso mensual del empleado supera el tope; las vacaciones y el salario pendiente con
  la retención ordinaria del procedimiento del empleado. La responsable MUST poder ver el detalle y
  MUST NOT tener que digitarla.
- **FR-007**: El sistema MUST permitir digitar, por empleado, los saldos iniciales de
  prestaciones a la fecha de arranque (días de vacaciones pendientes, cesantías e intereses
  acumulados del año, prima acumulada del semestre), auditados, y usarlos en las liquidaciones.

**Prima de servicios**

- **FR-008**: La prima MUST calcularse por semestre calendario como base prestacional × días
  trabajados del semestre / 360, con la base del salario vigente o el promedio ponderado del
  semestre cuando hubo cambios, incluyendo el auxilio de transporte cuando el empleado lo devengó.
- **FR-009**: MUST excluir a quien no tiene derecho (salario integral, aprendices en **etapa
  lectiva**, pasantes) e incluir al aprendiz en **etapa práctica** con prima completa (Ley 2466 de
  2025; la etapa la lleva la ficha con vigencia), y descontar la prima proporcional ya pagada en
  una liquidación definitiva del mismo semestre.

**Cesantías e intereses**

- **FR-010**: Las cesantías MUST calcularse a 31 de diciembre (o a la fecha de retiro) como base
  prestacional × días trabajados del año / 360, con la base del último salario si no varió en los
  últimos tres meses o el promedio del año trabajado si varió.
- **FR-011**: Los intereses MUST calcularse como cesantías × porcentaje anual vigente × días /
  360, pagarse al empleado con la relación de pago **propia** de la liquidación (fecha dentro del
  plazo legal) y contabilizarse contra su provisión.
- **FR-011a**: Toda liquidación especial que pague algo al empleado (prima, intereses,
  vacaciones compensadas o pagadas, definitiva) MUST tener su propia relación de pago, su propio
  comprobante para el empleado y su propio archivo de dispersión, con fecha de pago propia; la
  nómina ordinaria MUST NOT incluir esos valores, y el documento de nómina electrónica del mes MUST
  sumarlos con lo ordinario pagado en el mes.
- **FR-012**: El sistema MUST producir la relación de consignación por fondo de cesantías
  (fondo, empleado, documento, valor, total) exportable, y registrar la fecha en que se consignó
  cada fondo.
- **FR-013**: Quien tiene salario integral y el aprendiz en etapa lectiva MUST quedar fuera de
  cesantías e intereses; el aprendiz en etapa práctica MUST entrar con cesantías e intereses
  completos por los días de esa etapa; y quien se retiró con definitiva en el año MUST quedar
  fuera de la anual.

**Vacaciones**

- **FR-014**: El sistema MUST llevar por empleado los días de vacaciones causados (parámetro de
  días por año, proporcional a los días trabajados), disfrutados y compensados, y mostrar el
  saldo con su detalle.
- **FR-015**: Un disfrute MUST registrarse con fechas y contar los días hábiles con el calendario
  parametrizado: festivos de Colombia con vigencia y la **semana laboral de la empresa** («lunes a
  sábado» por defecto, o «lunes a viernes»), parámetro por empresa con vigencia; MUST mostrar los
  días hábiles y calendario que consume la solicitud antes de guardarla; y MUST dejar la novedad de
  vacaciones en cada período de nómina que cubre para que esos días se paguen como vacaciones y no
  como salario.
- **FR-016**: La compensación en dinero MUST limitarse al máximo legal parametrizado sobre lo
  causado y MUST rechazar lo que lo supere con el máximo permitido.
- **FR-017**: El valor de las vacaciones MUST calcularse sobre el salario ordinario del momento
  (sin auxilio de transporte ni horas extras) o el promedio del último año cuando el salario es
  variable, y contabilizarse contra la provisión.

**Liquidación definitiva**

- **FR-018**: Al registrar la terminación (fecha, motivo de una lista parametrizable, tipo de
  contrato), el sistema MUST liquidar en un solo documento: salario de los días pendientes,
  cesantías e intereses del período causado, prima proporcional, vacaciones pendientes,
  indemnización si aplica, y las deducciones pendientes, cada una con explicación.
- **FR-018a**: Las deducciones de la definitiva MUST proponerse solas: el **saldo total** de cada
  préstamo de la cooperativa (con capital, intereses y cuotas pendientes leídos de Cartera) hasta
  donde alcance el neto de la liquidación, y sólo las **cuotas causadas y no descontadas** de las
  libranzas con terceros. La responsable MUST poder validar cada saldo y **modificar el valor hacia
  abajo con motivo** antes de aprobar; el sistema MUST guardar y auditar el valor propuesto, el
  aplicado, el motivo, quién y cuándo, y mostrar el saldo que queda en Cartera después del
  descuento.
- **FR-019**: La indemnización MUST calcularse con una tabla parametrizada por tipo de contrato,
  antigüedad y rango salarial (contrato indefinido: días por el primer año y por cada año
  siguiente, según el rango de salarios mínimos; término fijo: el tiempo faltante), y MUST ser
  cero cuando el motivo no la genera; la explicación lo dice.
- **FR-020**: El sistema MUST producir el documento de liquidación para firma (empresa, empleado,
  cargo, fechas de ingreso y retiro, motivo, cada rubro con base y días, deducciones, total,
  firmas) descargable, y al aprobar MUST cerrar la ficha del empleado (retiro) y excluirlo de las
  nóminas posteriores; la reversión MUST reabrir la ficha.
- **FR-021**: Un retiro con fecha dentro de un período ya aprobado MUST rechazarse con la
  indicación de reversar el período o liquidar con fecha en el abierto.

**Procedimiento 2**

- **FR-022**: El sistema MUST calcular, para los empleados marcados en procedimiento 2, el
  porcentaje fijo semestral: ingresos laborales de los doce meses anteriores (o los trabajados),
  depurados con las deducciones y rentas exentas parametrizadas, la suma **dividida por 13** (o
  por los meses de vinculación cuando son menos de doce; el divisor es parámetro con vigencia,
  art. 386 ET) expresada en UVT, retención teórica con la tabla vigente y porcentaje resultante,
  con explicación mes a mes.
- **FR-023**: Al aprobar, el porcentaje MUST guardarse en la ficha con vigencia del semestre
  siguiente cerrando el anterior; la nómina ordinaria lo aplica sin cambios.

**PILA**

- **FR-024**: El sistema MUST generar por período mensual el archivo plano de autoliquidación en
  el formato vigente (encabezado y registro por cotizante) con tipo de cotizante, días, novedades
  (ingreso, retiro, variación de salario, incapacidades, licencias, vacaciones), IBC por
  subsistema y aportes de salud, pensión, fondo de solidaridad, riesgos, caja, SENA e ICBF, con
  tarifas y exoneraciones vigentes por parámetro. El **fondo de solidaridad pensional** MUST
  resolverse con **dos tablas con vigencia** —la de la Ley 797 de 2003 hasta el 31 de marzo de
  2027 y la de la Ley 2381 de 2024 desde el 1 de abril de 2027— y con una **bandera de régimen de
  transición** por empleado en la ficha: quien está en transición sigue con la tabla anterior; la
  explicación dice cuál tabla se usó y por qué. El **redondeo** MUST ser parámetro con vigencia:
  IBC al peso superior y aportes al múltiplo de 100 superior por defecto (Decreto 780 de 2016).
- **FR-024a**: La exoneración del art. 114-1 ET MUST ser un parámetro por empresa con vigencia
  («exonerada: sí/no» y umbral en salarios mínimos), que la PILA y la nómina ordinaria (aportes
  del empleador) leen por igual; el aplicativo MUST servir a entidades exoneradas y no exoneradas
  sin cambio del programa.
- **FR-025**: Antes de generar, MUST validar afiliaciones, documentos, tarifas y topes y entregar
  la lista de inconsistencias con enlace a la ficha; las bloqueantes impiden generar.
- **FR-026**: Cada generación MUST quedar registrada (quién, cuándo, período, totales, archivo) y
  las regeneraciones MUST conservar las anteriores como versiones.
- **FR-027**: Los totales del archivo MUST cuadrar con la suma de los aportes de las nóminas
  aprobadas del mes, y la diferencia, si la hay, MUST mostrarse antes de descargar.

**Nómina electrónica**

- **FR-028**: El sistema MUST generar por empleado y mes el documento soporte de pago de nómina
  electrónica conforme al anexo técnico vigente, con numeración consecutiva del rango
  parametrizado, sumando lo devengado y deducido pagado en el mes (nóminas ordinarias y
  liquidaciones especiales).
- **FR-029**: La firma y la transmisión MUST ser una acción explícita de la responsable; nada se
  transmite automáticamente. El sistema MUST registrar el estado devuelto (aceptado con CUNE y
  fecha, rechazado con errores, en proceso), permitir reintentar y consultar el histórico, y
  descargar el XML y la representación gráfica.
- **FR-030**: Cuando un mes transmitido cambia, el sistema MUST generar la nota de ajuste
  (reemplazo o eliminación) que referencia el documento original.
- **FR-031**: La cooperativa MUST parametrizar su habilitación (NIT, rangos de numeración,
  identificación del software, set de pruebas y su estado, ambiente de habilitación o producción,
  modo de firma «software propio» o «proveedor tecnológico», y el nombre del secreto que el
  servicio central usa para firmar en su nombre) y el sistema MUST rechazar transmitir con la
  parametrización incompleta. Esa habilitación, como todo dato de la cooperativa, vive **en su
  propia base** (Principio IV).
- **FR-031a**: La firma y la transmisión MUST hacerlas un **servicio centralizado de Ingenia365**
  (un solo servicio para todas las cooperativas, sin acceso público) que **no guarda datos de
  ningún cliente**: recibe del ERP el documento sin firmar junto con la identidad de la
  cooperativa, lo completa (CUNE y código de seguridad del software, que necesitan el PIN), lo
  valida contra el esquema, lo firma, lo transmite al servicio de recepción de la DIAN y devuelve
  al ERP el documento firmado, el paquete enviado, la respuesta y el estado con el CUNE; el ERP los
  guarda **en la base de esa cooperativa** junto con sus intentos y errores traducidos. El servicio
  MUST rechazar toda petición cuya identidad no coincida con la habilitación pedida: ninguna
  cooperativa MUST poder firmar, transmitir ni consultar en nombre de otra. Los certificados con
  sus contraseñas, el PIN de cada cooperativa y las credenciales MUST vivir sólo como secretos
  montados en ese servicio por el dueño de la plataforma, referenciados por nombre desde la
  habilitación, nunca en el ERP ni en el repositorio. Un documento MUST NOT transmitirse dos veces
  por reintento: su número es único por cooperativa y el ERP lo garantiza.
- **FR-031b**: El servicio MUST arrancar en modo **«software propio de cada cooperativa»**: la
  cooperativa registra el ERP como su software en el catálogo de la DIAN, aporta su certificado de
  firma y el servicio firma con él como empleador. El modo **«proveedor tecnológico de
  Ingenia365»** (certificado y software de Ingenia365, firma como tercero) MUST poder activarse
  por configuración, cooperativa por cooperativa y sin cambiar lo que el ERP construye ni el
  contrato entre ERP y servicio, cuando Ingenia365 tenga la habilitación como PT (ser PT de factura
  electrónica, patrimonio de al menos 20.000 UVT, ISO 27001 o compromiso, visita de la DIAN). En
  cualquiera de los dos modos el servicio MUST operar contra el ambiente de habilitación de la DIAN
  para el set de pruebas de cada cooperativa y contra producción una vez habilitada, y el ERP MUST
  mostrar en cuál ambiente y en cuál modo está cada una.

**Dispersión bancaria**

- **FR-032**: El sistema MUST generar desde la relación de pago un archivo plano por banco con
  formato parametrizable (columnas, orden, separador, longitudes, cuenta origen, vigencia) con una
  línea por empleado con cuenta bancaria, y dejar en pendientes a quien no la tenga.
- **FR-033**: Marcar el archivo como enviado MUST dejar pagados a todos sus empleados con fecha,
  medio transferencia y la referencia del archivo, con el mismo efecto que la marca manual
  (bloquea la reversión).

**Reportes, permisos y auditoría**

- **FR-034**: Cada liquidación especial, la consignación de cesantías, la PILA, la nómina
  electrónica y la dispersión MUST exportarse por el centro de reportes a Excel, PDF y Word con
  el encabezado de la cooperativa.
- **FR-035**: Cada proceso MUST tener permisos propios (ver, registrar, aprobar, reversar,
  generar, transmitir) y toda escritura, generación y transmisión MUST quedar en la auditoría con
  usuario, cooperativa y momento.

### Key Entities *(include if feature involves data)*

- **Liquidación especial**: tipo (prima, cesantías e intereses, vacaciones, definitiva), período
  o fecha de corte, versión, estado (borrador, aprobada, reversada, reemplazada), empleados
  incluidos con sus líneas explicadas, comprobante contable, relación de pago; inmutable una vez
  aprobada.
- **Saldo inicial de prestaciones**: por empleado a la fecha de arranque: días de vacaciones
  pendientes, cesantías e intereses acumulados del año, prima acumulada del semestre; quién y
  cuándo lo digitó.
- **Movimiento de vacaciones**: causación, disfrute (fechas, días hábiles), compensación, ajuste;
  saldo derivado, nunca almacenado como verdad.
- **Terminación del contrato**: fecha, motivo (catálogo parametrizable con marca «genera
  indemnización»), tipo de contrato, liquidación definitiva asociada, documento para firma.
- **Parámetros legales nuevos**: días de prima por semestre, porcentaje de intereses a las
  cesantías, días de vacaciones por año y máximo compensable, tabla de indemnización por tipo de
  contrato/antigüedad/rango salarial, calendario de festivos, sábado hábil o no, tarifas y
  exoneraciones de aportes por subsistema y tipo de cotizante, topes de IBC, **dos tablas del
  fondo de solidaridad pensional** (Ley 797 de 2003 hasta el 31-03-2027; Ley 2381 de 2024 desde el
  01-04-2027), **redondeo PILA** (IBC al peso superior, aportes al múltiplo de 100 superior),
  divisor del procedimiento 2 (13), porcentajes de apoyo del aprendiz por etapa, tope de ingreso
  para la exención de cesantías e intereses y tarifa y tope de retención sobre indemnizaciones;
  todos con vigencia. En la ficha del empleado: etapa del aprendiz (lectiva/práctica, con
  vigencia) y bandera de régimen de transición pensional.
- **Cálculo de porcentaje fijo (procedimiento 2)**: empleado, semestre, doce meses con ingresos y
  depuración, promedio, retención teórica, porcentaje, estado (calculado, aprobado), vigencia
  resultante en la ficha.
- **Generación PILA**: período, versión, estado (validada con inconsistencias, generada,
  cargada), totales por subsistema, archivo, inconsistencias con enlace.
- **Documento de nómina electrónica**: empleado, mes, tipo (soporte, nota de ajuste de reemplazo
  o eliminación), número, contenido sin firmar y firmado, paquete enviado, respuesta de la DIAN,
  estado (generado, transmitido, aceptado, rechazado, en proceso), CUNE, intentos, errores
  traducidos; **Habilitación**: NIT, rangos, identificación del software, set de pruebas y su
  estado, ambiente, modo de firma (software propio / proveedor tecnológico) y nombre del secreto
  con que el servicio central firma. Ambas viven **en la base de la cooperativa**; el servicio
  central no conserva ninguna.
- **Formato de dispersión**: banco, vigencia, estructura de columnas; **Archivo de dispersión**:
  relación de pago, banco, líneas, totales, estado (generado, enviado), referencia, quién y cuándo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: La prima de diciembre de 2026, los intereses de enero de 2027 y la consignación de
  cesantías de febrero de 2027 de COOFLOPAL se liquidan en el sistema con valores que coinciden al
  peso con el cálculo manual de la contadora en el 100 % de los empleados, sin ninguna hoja de
  cálculo aparte.
- **SC-002**: Una liquidación definitiva completa (retiro, todos los rubros, documento para
  firma, comprobante contable) se termina en menos de 15 minutos desde que se conoce la fecha de
  retiro, y la nómina ordinaria siguiente excluye al empleado sin intervención.
- **SC-003**: El 100 % de las liquidaciones especiales aprobadas tienen comprobante contable y
  ninguna provisión queda con saldo distinto del que explican las liquidaciones pendientes (las
  pruebas automáticas lo comprueban tras cada operación).
- **SC-004**: El archivo PILA generado lo acepta el validador del operador de la cooperativa sin
  correcciones manuales en el 100 % de los meses de prueba, y sus totales cuadran con los
  comprobantes `NM` del mes.
- **SC-005**: El 100 % de los documentos de nómina electrónica generados en el ambiente de
  habilitación reciben CUNE o un rechazo con causa entendible; ninguno se transmite sin acción
  explícita.
- **SC-006**: El porcentaje del procedimiento 2 calculado coincide con el manual para todos los
  casos de prueba, y su explicación permite a la contadora reconstruirlo mes a mes.
- **SC-007**: El archivo de dispersión lo acepta el portal del banco sin edición manual y marcar
  «enviado» deja pagados a todos los empleados del archivo en una acción.
- **SC-008**: Ningún valor legal está en el código: la prueba de arquitectura que hoy vigila la
  nómina sigue verde con los procesos nuevos.

## Assumptions

- **Norma laboral colombiana general** como regla por defecto, con todo valor como parámetro con
  vigencia: prima = 15 días de salario por semestre (art. 306 CST) sobre base prestacional con
  auxilio de transporte; cesantías = un mes de salario por año (Ley 50 de 1990), base del último
  salario si no varió en los últimos tres meses; intereses del 12 % anual (Ley 52 de 1975);
  vacaciones = 15 días hábiles por año (art. 186 CST), compensables hasta la mitad (art. 189
  CST), pagadas con el salario ordinario sin auxilio ni extras (art. 192 CST); indemnización del
  art. 64 CST (indefinido: 30 días el primer año y 20 por cada uno siguiente para quien gana menos
  de 10 salarios mínimos; 20 y 15 para quien gana 10 o más; término fijo: el tiempo faltante);
  salario integral sin prima ni cesantías; aprendices **según su etapa** (Ley 2466 de 2025): en
  etapa lectiva sin prestaciones, en etapa práctica con prestaciones completas. Cualquier pacto
  distinto de la cooperativa se carga como parámetro.
- **Procedimiento 2** según el art. 386 del Estatuto Tributario: suma de los ingresos laborales
  de los doce meses anteriores al mes del cálculo, depurada con las mismas reglas que la nómina
  ordinaria usa para el procedimiento 1 y **dividida por 13** (o por los meses de vinculación si son
  menos de doce), en UVT del año, retención teórica con la tabla y porcentaje fijo que rige el
  semestre siguiente; el orden entre depurar y dividir es una política por empresa que la contadora
  elige.
- **PILA** según el formato de la Resolución 2388 de 2016 y sus modificaciones vigentes, planilla
  E de empleados para **Aportes en Línea**; exoneraciones de la Ley 1607 de 2012 por parámetro
  (cooperativa exonerada o no, umbral de salarios mínimos); fondo de solidaridad pensional con la
  tabla de la Ley 797 de 2003 hasta el 31 de marzo de 2027 y la de la Ley 2381 de 2024 desde el 1
  de abril de 2027 (bandera de transición por empleado); redondeos del Decreto 780 de 2016 (IBC al
  peso superior, aportes al múltiplo de 100 superior) como parámetro.
- **Nómina electrónica** según la Res. 013/2021, compilada en la Res. Única 000227 de 2025, y el
  anexo técnico vigente (documento mensual por empleado, notas de ajuste, CUNE), con plazo de
  transmisión en los diez primeros días del mes siguiente (calendario según la DIAN; la contadora
  confirma), firmada y transmitida por el servicio centralizado de Ingenia365 que arranca en modo
  **software propio de cada cooperativa** (certificado propio, firma como empleador) y pasa a
  **proveedor tecnológico** cuando Ingenia365 tenga esa habilitación; el registro de cada
  cooperativa y su software en el catálogo de la DIAN, su certificado de firma y, después, el
  registro de Ingenia365 como PT son trámites del dueño y condicionan cuándo se puede transmitir en
  producción, no lo que el programa hace. La habilitación y los documentos de cada cooperativa
  viven en su base; el servicio central sólo firma y transmite.
- **Dispersión**: un banco a la vez por cooperativa, formato parametrizable; el primer formato es
  el del archivo plano de pagos de **Banco AV Villas** con la estructura que publique el banco.
- **Datos existentes**: ficha del empleado (clase, tipo de contrato, fecha de ingreso, fondo de
  cesantías, EPS, pensión, ARL, caja, cuenta bancaria), nóminas aprobadas con sus líneas y bases,
  provisiones por concepto, conceptos parametrizados con cuentas, parámetros legales con
  vigencia, relación de pago y comprobante del empleado. Se reutiliza todo; lo que falte en la
  ficha (cuenta bancaria, procedimiento de retención) se agrega.
- **Arranque el 1 de diciembre de 2026**: las provisiones y prestaciones causadas antes entran por
  saldos iniciales digitados (FR-007) y por el comprobante de apertura contable; sin ellos, las
  primeras liquidaciones advierten que pueden salir cortas.
- **Segregación y permisos**: como la nómina ordinaria (registrar y aprobar son permisos
  distintos; misma persona sólo si la cooperativa lo permite por parámetro).
- **Tesorería**: sigue fuera; la dispersión es un archivo que la persona carga en el banco y la
  respuesta del banco no se procesa.

## Dependencias

- Nómina ordinaria en producción (features 005 y 006) con provisiones y bases por línea.
- Contrato único de contabilidad y cuentas por concepto (feature 009 E1) para contabilizar cada
  liquidación; el módulo contable iniciado en la cooperativa.
- Fondos de cesantías, EPS, ARL, cajas y bancos vinculados a su persona (FR-088 de la 009) para
  que sean terceros contables y para la PILA.
- Para la nómina electrónica, en modo software propio: el registro de cada cooperativa en el
  catálogo de la DIAN como emisor de nómina electrónica, el registro del ERP como su software
  (identificación, PIN y set de pruebas), su certificado de firma de una entidad acreditada y el
  acceso al ambiente de habilitación; el registro de Ingenia365 como proveedor tecnológico sólo
  cuando se decida activar ese modo.
- Para la PILA y la dispersión: el validador de archivos de Aportes en Línea y el portal
  empresarial de AV Villas (con la estructura publicada del archivo) para la prueba de aceptación.
