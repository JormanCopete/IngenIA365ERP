# 026 — Cargos masivos de Cartera

**Rama**: `026-cargos-masivos` · **Creada**: 2026-11-25 · **Estado**: borrador con las decisiones D1–D5 del dueño (2026-11-25)
**Base**: la 013 (motor de Cartera y contrato de contabilización), la 014 (retiro del asociado y fallecimiento), la 016 (plantillas de cargue)
y la 024/025 (causación).

## Problema

Hay conceptos que la cooperativa carga **cada mes a todos sus asociados activos, por el mismo valor**, y que se cobran como una obligación de
Cartera: por caja, por nómina o por pagaduría. El caso que lo origina es el **fondo de solidaridad** de COOFLOPAL, la línea `FDOSOL`.

- En la herramienta anterior se generaba **una obligación por asociado y por mes**. Hoy hay en producción 1.229 obligaciones vivas de `FDOSOL`,
  de 111 asociados, todas traídas por la migración al 31/10/2026. La cooperativa tiene 459 asociados activos.
- Desde la migración no hay cómo seguir cargándola. El ERP hace nacer una obligación sólo de tres maneras, y ninguna sirve:
  - **Originación**: solicitud, decisión y desembolso, asociado por asociado. No escala a 459 por mes, y el desembolso deja el dinero «por girar»
    a Tesorería, cuando aquí no se gira nada.
  - **Venta a crédito de Comercial**: exige un documento de venta, que es factura electrónica. El fondo de solidaridad no es una venta.
  - **Migración**: sólo al activar la cartera, con fecha de corte.

## Principio

- La obligación nace **como cualquier crédito de su línea**: número, condiciones selladas, plan y libro. Desde ahí vive igual que los demás: se
  paga en caja, se descuenta por nómina o pagaduría, sale en el estado de cuenta y cae en mora.
- **No hay giro**. El crédito no sale para Tesorería: va a una **cuenta de contrapartida** que pone la matriz contable de Cartera, como todas sus
  cuentas. Cartera sigue sin conocer cuentas.
- **Una corrida, un comprobante.**
- **Repetir no duplica**: un asociado recibe a lo sumo un cargo de la misma línea en el mismo período.
- **De fábrica nada cambia**: ninguna línea admite cargos masivos hasta que alguien la marca.

## Historias de usuario

### HU1 — Preparar la línea para cargos masivos (P1)

El administrador de Cartera marca en una versión de la línea que **admite cargo masivo** y define sus valores por defecto: valor, cuotas,
periodicidad y formas de pago. La contadora pone en la matriz la cuenta de **contrapartida del cargo** de esa línea.

**Por qué primero**: sin la línea marcada y la regla contable, ninguna corrida se puede confirmar.

**Prueba independiente**: se marca `FDOSOL`, se pone su regla en la matriz y la completitud de la matriz deja de pedirla. Una línea sin marcar
no aparece en el proceso.

**Escenarios**:
1. **Dado** una versión de línea que exige garantía o codeudor, **cuando** se intenta marcar «Admite cargo masivo», **entonces** se rechaza con
   el motivo.
2. **Dado** una línea marcada sin la regla de contrapartida en la matriz, **cuando** se consulta la completitud de la matriz, **entonces** pide
   esa regla.
3. **Dado** una línea marcada, **cuando** se abre una versión nueva, **entonces** conserva la marca y sus valores por defecto, como el resto de
   la versión.

### HU2 — Cargar el mes a los asociados activos (P1)

El analista de Cartera abre **Cartera → Procesos → Cargos masivos**, elige la línea y el período, confirma o cambia los datos del cargo, filtra
a quién se carga, revisa la vista previa y confirma. Cada asociado incluido queda con su obligación.

**Por qué primero**: es lo que hoy no se puede hacer y la cooperativa necesita cada mes.

**Prueba independiente**: con `FDOSOL` marcada y su regla puesta, una corrida de diciembre crea una obligación por cada asociado activo que no la
tenga, y cada una se puede pagar en caja ese mismo día.

**Escenarios**:
1. **Dado** 459 asociados activos, 3 de ellos con retiro radicado y 1 marcado fallecido, **cuando** se prepara la corrida de diciembre, **entonces**
   la vista previa incluye 455 y lista los 4 omitidos con su motivo.
2. **Dado** un asociado en cobro jurídico, **cuando** se prepara la corrida, **entonces** se incluye.
3. **Dado** una corrida de diciembre ya terminada, **cuando** se prepara otra de diciembre de la misma línea, **entonces** todos los que ya tienen
   su cargo salen omitidos por «ya tiene el cargo de este período» y la corrida sólo crea los que faltan.
4. **Dado** un valor fuera de los rangos de la versión de la línea, **cuando** se intenta confirmar, **entonces** se rechaza y no se crea nada.
5. **Dado** una corrida confirmada que se interrumpe, **cuando** se retoma, **entonces** sigue donde iba y el resultado es el mismo que sin
   interrupción.
6. **Dado** un asociado retirado entre la vista previa y la ejecución, **cuando** la corrida llega a él, **entonces** lo omite con su motivo.
7. **Dado** la forma de pago «la de cada asociado» y un asociado cuya forma de pago la línea no admite, **cuando** se prepara la corrida,
   **entonces** le pone la forma por defecto de la línea y la vista previa lo avisa.
8. **Dado** la forma de pago «pagaduría» y un asociado sin pagaduría, **cuando** se prepara la corrida, **entonces** lo omite con su motivo.

### HU3 — Un solo comprobante por corrida (P1)

Al terminar la corrida, Contabilidad recibe **un solo comprobante** de tipo `CM` («Cargos masivos»):
- **débito** a la cuenta por cobrar de capital de la línea, una fila por asociado con su tercero;
- **crédito** a la cuenta de contrapartida del cargo, con el tercero elegido en la ventana (el mismo asociado o un proveedor). Si la cuenta no
  exige tercero, va una sola fila por el total.

**Por qué primero**: con `Contabilidad.FaltaDeRegla` en «Bloquear», sin esto no se confirma ninguna corrida en producción.

**Prueba independiente**: una corrida de 455 cargos de 13.557 deja un comprobante `CM` que cuadra al peso con 6.168.435 y la conciliación
contable de Cartera no muestra diferencia en la línea.

**Escenarios**:
1. **Dado** una cuenta de contrapartida que exige tercero y el tercero «el mismo asociado», **cuando** termina la corrida, **entonces** el
   comprobante lleva una fila crédito por asociado.
2. **Dado** el tercero «un proveedor», **cuando** termina la corrida, **entonces** las filas crédito llevan a ese proveedor.
3. **Dado** una cuenta de contrapartida que no exige tercero, **cuando** termina la corrida, **entonces** el crédito va en una sola fila por
   sucursal.
4. **Dado** que falta la regla de contrapartida y `Contabilidad.FaltaDeRegla` está en «Bloquear», **cuando** se intenta confirmar, **entonces**
   la ventana no deja y dice qué regla falta.
5. **Dado** una cooperativa sin contabilidad iniciada, **cuando** termina la corrida, **entonces** las obligaciones quedan creadas y no hay
   comprobante, como en el resto de Cartera.
6. **Dado** el comprobante de una corrida, **cuando** alguien intenta reversarlo desde Comprobantes, **entonces** se rechaza: se corrige
   anulando desde Cartera.

### HU4 — Cargar desde un plano (P2)

Cuando el valor o las condiciones no son iguales para todos, la corrida toma a los asociados de un archivo: documento y, si se quiere, valor,
cuotas, fecha del primer pago y forma de pago propios por fila. Lo que no viene en la fila sale de los datos de la corrida.

**Por qué segundo**: el caso de origen es un valor único; el plano cubre excepciones y otros conceptos.

**Prueba independiente**: un plano de 10 filas con valores distintos crea 10 obligaciones con esos valores y un solo comprobante.

**Escenarios**:
1. **Dado** un plano con un documento que no existe o que es ambiguo entre dos tipos, **cuando** se revisa, **entonces** marca la fila con su
   error y no se aplica nada hasta corregirlo.
2. **Dado** un plano con un asociado retirado, **cuando** se revisa, **entonces** la fila sale omitida con su motivo y no impide aplicar las
   demás.
3. **Dado** un plano con una persona que no es asociada, **cuando** se revisa, **entonces** marca la fila con su error.

### HU5 — Anular una corrida o un cargo (P2)

Quien tiene permiso anula, con motivo, **la corrida entera** o **el cargo de un asociado**, mientras la obligación no tenga pagos ni novedades.
La obligación queda anulada, Contabilidad recibe el reverso y el período queda libre para volver a cargarlo.

**Prueba independiente**: se anula el cargo de un asociado y se vuelve a cargar en otra corrida del mismo período sin que lo omita.

**Escenarios**:
1. **Dado** una corrida donde un asociado ya pagó su cargo, **cuando** se anula la corrida entera, **entonces** se anulan todas las demás y esa
   queda con el motivo «tiene pagos».
2. **Dado** la anulación de una corrida entera, **cuando** termina, **entonces** Contabilidad recibe un solo comprobante que la reversa.
3. **Dado** la anulación del cargo de un asociado, **cuando** termina, **entonces** Contabilidad recibe el reverso de esa sola obligación.

### HU6 — Consultar las corridas (P3)

La lista de corridas, con filtros por línea, período y estado, muestra cada una con su total, cuántos se cargaron, omitieron o fallaron, quién la
preparó y confirmó, y su comprobante. El detalle muestra asociado por asociado y se exporta a Excel y PDF.

## Casos límite

- **Fecha del cargo en un mes cerrado** por Cartera o por Contabilidad: la corrida no se confirma, igual que un desembolso.
- **Asociado con una obligación anterior del mismo concepto en mora**: se le carga igual. El cargo no evalúa mora ni cupo.
- **El mismo asociado dos veces en un plano**: la segunda fila es un error.
- **Dos personas preparan a la vez la corrida del mismo período y línea**: la segunda que confirma omite a los que ya cargó la primera.
- **Fallo en un asociado** (por ejemplo, una condición de la línea que su caso no cumple): ese queda «fallido» con el motivo y la corrida sigue.
  Ninguna obligación queda a medias.
- **La contabilidad rechaza el comprobante** después de confirmar (una regla retirada a mitad de camino): las obligaciones quedan y el
  comprobante espera en reproceso con su alerta, como cualquier lote de Cartera.
- **Una versión nueva de la línea entra en vigencia** entre la vista previa y la confirmación: confirmar vuelve a leer la versión vigente a la
  fecha del cargo y vuelve a validar.
- **Valor cero o negativo**: se rechaza.

## Requisitos funcionales

### La línea (HU1)

- **FR-001** La versión de línea gana la marca **«Admite cargo masivo»** (no de fábrica). Sólo las líneas con esa marca vigente a la fecha del
  cargo aparecen en el proceso.
- **FR-002** Una versión con la marca no puede exigir garantía, codeudor, consulta a centrales, vínculo laboral ni documentos de originación;
  intentarlo se rechaza con el motivo.
- **FR-003** La versión con la marca define los valores por defecto del cargo: valor, cuotas, periodicidad y forma de pago (entre las que la
  línea admite). Los rangos de monto y plazo de la versión siguen rigiendo.
- **FR-004** La versión con la marca tiene dos opciones para las obligaciones que nacen de un cargo masivo: **«Se reporta a centrales»** y
  **«Entra a cobranza»**, las dos en **No** de fábrica (D4). Con No, esas obligaciones quedan fuera de los archivos de centrales (con su motivo
  en las exclusiones) y fuera del reparto de cobranza. La mora, la calificación y el deterioro siguen las reglas de la línea.

### La corrida (HU2)

- **FR-005** Los datos de la corrida, con los valores por defecto de la versión de la línea:
  - línea y **período** (año y mes) del cargo;
  - **fecha del cargo**: la del comprobante y la fecha valor de las obligaciones; por defecto, hoy;
  - **valor** de la obligación;
  - **plazo** en cuotas (por defecto 1) y **periodicidad**;
  - **forma de pago**: caja, nómina, pagaduría o débito automático, o **«la de cada asociado»** (la de su ficha de asociado; si la línea no la
    admite o no la tiene, la de defecto de la línea, con aviso);
  - **ciclo de pago** (opcional): primera o segunda quincena, década o semana del mes. Si se da, la fecha del primer pago tiene que caer en él;
  - **fecha del primer pago**;
  - **tercero de la contrapartida**: «el mismo asociado» (por defecto) o un proveedor, buscado en Personas;
  - lo demás que la versión de la línea pida para simular el plan (sistema de amortización y seguros, si los tiene).
- **FR-006** A quién se carga, por **filtros**: asociados con la ficha de asociado activa, con filtros opcionales de agencia, pagaduría, forma
  de pago de la ficha y ciclo de descuento. Siempre se excluyen (D3):
  - los retirados;
  - las personas marcadas fallecidas o con un caso de fallecimiento abierto;
  - quien tiene un retiro de asociado vivo (radicado, aprobado, liquidado o por rehacer).
  Quien está en cobro jurídico se incluye.
- **FR-007** **Unicidad**: un asociado no recibe dos cargos de la misma línea en el mismo período mientras el anterior no esté anulado. Los ya
  cargados salen omitidos con el motivo «ya tiene el cargo de este período».
- **FR-008** **Vista previa**, antes de confirmar y sin crear obligaciones:
  - incluidos, con su forma de pago, primer pago y valor;
  - omitidos, con su motivo;
  - total y número de obligaciones;
  - las dos cuentas del comprobante, como las resuelve la matriz, y el resultado de la validación contable previa.
- **FR-009** La validación contable previa sigue `Contabilidad.FaltaDeRegla`: en «Bloquear», sin la regla no se confirma; en «Advertir», se
  confirma y queda la alerta; sin contabilidad iniciada o con el hecho en «No pasa», no se bloquea.
- **FR-010** **Confirmar** vuelve a evaluar todo a la fecha del cargo (versión de la línea, estado de cada asociado, unicidad) y ejecuta en
  segundo plano por tandas. Si se interrumpe, se retoma donde iba. Cada obligación se crea completa o no se crea.
- **FR-011** Cada obligación nace:
  - con el origen nuevo **«Cargo masivo»** y la referencia a su corrida;
  - con su número del numerador de créditos;
  - con las condiciones de la versión vigente a la fecha del cargo más lo elegido en la corrida, selladas;
  - con el plan del simulador de la línea, dentro de los rangos de la versión;
  - con un desembolso de forma nueva **«Cargo»**, sin giro: no crea orden de pago en Tesorería ni documentos para firma;
  - con fecha valor = fecha del cargo, en la sucursal de la agencia del asociado;
  - con el asociado como único deudor.
- **FR-012** Desde que nace, la obligación se comporta como cualquier crédito de la línea: caja, contrato de obligaciones, nómina, pagaduría,
  estado de cuenta, causación, mora, cierre mensual e informes.
- **FR-013** Cada asociado de la corrida queda con su resultado: **creado** (con su obligación), **omitido** o **fallido**, con el motivo.

### La contabilidad (HU3)

- **FR-014** La matriz de Cartera gana el rol **«Contrapartida del cargo»** (naturaleza crédito), que se resuelve por línea y sucursal como los
  demás roles. La plantilla de la matriz lo sugiere sin cuenta para cada línea marcada y la completitud lo pide.
- **FR-015** El hecho de cada obligación debita la cuenta por cobrar de capital de la línea (su regla de saldo de siempre), con el asociado como
  tercero, y acredita la contrapartida del cargo, con el tercero elegido en la corrida. Ningún hecho lleva una cuenta.
- **FR-016** Todos los hechos de una corrida se asientan en **un solo comprobante** al terminarla, con una fila por asociado donde la cuenta
  exige tercero (o documento cruce) y una fila por sucursal y centro de costo donde no.
- **FR-017** Se siembra el tipo de comprobante **`CM` «Cargos masivos»** con su consecutivo (D2), configurable como los demás tipos de Cartera.
- **FR-018** El comprobante se fecha en la fecha del cargo. Si contabilidad lo rechaza, queda en reproceso con su alerta.
- **FR-019** Lo contabilizado por una corrida no se reversa desde Comprobantes: se corrige anulando desde Cartera (HU5).

### El plano (HU4)

- **FR-020** Plantilla de cargue **«Cargos masivos»**: tipo y número de documento obligatorios; valor, cuotas, fecha del primer pago y forma de
  pago opcionales. Se baja desde la ventana del proceso.
- **FR-021** Revisión sin guardar con la mecánica de las demás plantillas: los errores de fila (persona inexistente, ambigua, no asociada,
  repetida, valor o cuotas fuera de rango) impiden aplicar; las exclusiones de FR-006 y la unicidad de FR-007 salen como omitidos y no lo
  impiden.
- **FR-022** Con plano, los filtros de FR-006 no aplican; las exclusiones sí.

### La anulación (HU5)

- **FR-023** Se anula la corrida entera o el cargo de un asociado, con motivo obligatorio, mientras la obligación no tenga pagos, novedades,
  condonaciones ni operaciones distintas de su desembolso y su causación.
- **FR-024** Anular reversa el desembolso de la obligación con el mecanismo de siempre y la deja anulada. La de la corrida entera va en un solo
  comprobante de reverso; la de un asociado, en el suyo.
- **FR-025** Una obligación anulada libera su período: el asociado puede volver a cargarse.

### Gobierno y pantallas (transversal)

- **FR-026** Permisos nuevos `Lending.BulkCharges.View` (consultar), `.Run` (preparar y confirmar) y `.Void` (anular), sembrados por la API.
- **FR-027** Parámetro `Segregacion.CargoMasivoPorOtraPersona` (No de fábrica): con Sí, quien prepara la corrida no la puede confirmar.
- **FR-028** Auditoría de la corrida: preparada, confirmada, terminada, anulada (entera o por asociado), con sus datos y totales.
- **FR-029** Confirmar y anular son idempotentes por su clave: repetir la petición no repite el efecto.
- **FR-030** Pantallas en **Cartera → Procesos → Cargos masivos**: la corrida con sus pasos (datos, a quién, vista previa, confirmar), la lista de
  corridas (HU6) y su detalle, con indicador de carga en cada zona que espera datos.
- **FR-031** La lista y el detalle de corridas se exportan a Excel y PDF.

## Decisiones del dueño (2026-11-25)

- **D1** La contrapartida («acreditando a la cuota correspondiente») es la **cuenta de contrapartida de la línea**, que pone la matriz contable,
  con el tercero que se elija en la ventana: el mismo asociado o un proveedor.
- **D2** Tipo de comprobante nuevo **`CM` «Cargos masivos»**.
- **D3** Se excluyen retirados, fallecidos y quien tiene el retiro en trámite. Quien está en cobro jurídico se incluye.
- **D4** La línea decide si sus obligaciones de cargo masivo se reportan a centrales y entran a cobranza; de fábrica, **no**.
- **D5** Por defecto, **una obligación por asociado y por mes** (una cuota), como en la herramienta anterior; el proceso admite varias cuotas.

## Entidades

- **Corrida de cargo masivo**: línea y versión, período, fecha del cargo, datos sellados del cargo (valor, cuotas, periodicidad, forma y ciclo de
  pago, primer pago, tercero de la contrapartida), origen (filtros o plano) y filtros usados, estado (borrador, en curso, terminada, anulada),
  totales (creados, omitidos, fallidos, valor), comprobante, quién preparó y quién confirmó.
- **Cargo de la corrida**: el asociado, su resultado (creado, omitido, fallido, anulado), el motivo, la obligación creada, el valor y la fecha
  del primer pago. Uno por asociado y corrida.
- **Versión de línea**: gana «Admite cargo masivo», los valores por defecto del cargo y las opciones de centrales y cobranza.
- **Obligación**: gana el origen «Cargo masivo» y la referencia a su corrida; su desembolso, la forma «Cargo».
- **Matriz de Cartera**: gana el rol «Contrapartida del cargo».
- **Tipo de comprobante** `CM`.

## Criterios de éxito

- **SC-001** El cargo mensual de `FDOSOL` a los asociados activos de COOFLOPAL se hace de punta a punta (datos, vista previa, confirmación y
  comprobante) en menos de 10 minutos, sin intervención técnica.
- **SC-002** Una corrida de 2.000 asociados termina en menos de 15 minutos.
- **SC-003** El comprobante de cada corrida cuadra al peso con la suma de las obligaciones creadas, y la conciliación contable de Cartera no
  muestra diferencias en la línea.
- **SC-004** Repetir la corrida del mismo período y línea no crea ninguna obligación nueva.
- **SC-005** Una corrida interrumpida y retomada deja el mismo resultado que una sin interrupción.
- **SC-006** Las obligaciones creadas se pueden pagar en caja y aparecen en el estado de cuenta y en los descuentos de nómina y pagaduría
  desde el mismo día.
- **SC-007** Con los valores de fábrica (ninguna línea marcada), las suites existentes de Cartera y Contabilidad siguen pasando sin cambios.

## Supuestos y límites

- **No es una solicitud de crédito**: no se evalúan cupo, capacidad de pago, centrales, documentos ni decisión de crédito.
- «Asociado activo» es la ficha de asociado en estado activo.
- La cuenta de contrapartida de `FDOSOL` la define la contadora en la matriz; queda pendiente de validar.
- No factura electrónicamente. Si el concepto es un ingreso que debe facturarse, va por Comercial (como los servicios de Transporte).
- No aplica a aportes ni a captaciones: tienen su propia cuota.
- No hay programación automática: cada mes una persona prepara y confirma la corrida. Programarla es una mejora posterior.
- No se cargan períodos atrasados en bloque: cada corrida es de un período y su fecha de cargo no puede caer en un mes cerrado.
- Las 1.229 obligaciones migradas de `FDOSOL` no cambian; la unicidad de FR-007 sólo mira cargos hechos por este proceso.
