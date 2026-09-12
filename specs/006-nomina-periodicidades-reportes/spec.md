# Feature Specification: Nómina — periodicidades, reglas de recurrencia, descarte de borrador y reportes

**Feature Branch**: `006-nomina-periodicidades-reportes`
**Created**: 2026-09-12
**Status**: Draft
**Input**: Solicitud del usuario del 2026-09-12 (ocho puntos tras la primera nómina en QA):
planes decadales y semanales; que una novedad recurrente pueda aplicar en todos los períodos
del mes, sólo en el primero o sólo en el último; que el período sepa a qué sub-período y mes
pertenece; poder devolver a «Abierto» un período calculado sin aprobar-y-reversar; y
reemplazar la pantalla rota `/reportes/comprobante-nomina` por un centro de reportes de
nómina exportable a Excel, PDF y Word.

## Contexto

La feature 005 dejó un motor de cálculo puro que prorratea por la duración del período
del plan (30 días mensual, 15 quincenal) y novedades recurrentes que se generan en **todos**
los períodos del plan que cubren. Al probar en QA con una cooperativa real aparecieron cuatro
límites de uso, no de cálculo:

1. Muchas empresas liquidan **quincenal**, y algunas **decadal** o **semanal**. Hoy sólo
   existen mensual y quincenal.
2. Un préstamo o descuento recurrente en una nómina quincenal suele cobrarse **en una sola
   quincena** (o en ambas, mitad y mitad). Hoy se genera siempre en las dos.
3. Un período **Calculado** no puede volver a Abierto: si las fechas estaban mal, la única
   salida es aprobar y reversar, que deja un asiento contable espejo por un error de captura.
4. `/reportes/comprobante-nomina` es una pantalla de la Fase 1 que llama a una ruta que no
   existe; los comprobantes reales viven en Liquidación. Falta un lugar para **ver y
   exportar** la información de nómina (por corrida, por empleado, por concepto, por
   novedad, entre fechas).

## Clarifications

### Session 2026-09-12

- Q: ¿Cómo se dividen las décadas? → A: Tres fijas por mes: 1–10, 11–20 y 21–fin de mes
  (la tercera absorbe el 28/29/31). Base de proporción 10 días.
- Q: ¿Base de proporción semanal y meses partidos? → A: 7 días. La persona elige a qué
  **semana del mes (1–5)** y a qué **mes de imputación** pertenece el período cuando la
  semana cruza de mes. Los topes y valores mensuales (auxilio de transporte, tabla de
  retención en UVT, tope del IBC, umbral de exoneración) **siempre se prorratean por
  días/30**, sin importar el mes de imputación; el mes de imputación sirve para agrupar en
  reportes y para la regla «primero/último del mes» de las recurrentes.
- Q: ¿Qué reportes? → A: Cinco vistas iniciales, todas con Excel, PDF y Word: comprobante
  por empleado; resumen de la corrida por concepto; detalle por empleado y concepto;
  novedades del período con origen y estado; histórico por empleado entre fechas. Planilla
  PILA, provisiones acumuladas y costo por centro de costo quedan **fuera** de esta feature.
- Q: ¿«Descartar borrador» toca contabilidad o cuotas? → A: No hay contabilidad en un
  borrador. Las novedades generadas por recurrentes para ese período se **anulan** (la
  cuota nunca se contó: se cuenta al aprobar), las manuales quedan.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Descartar un borrador y corregir el período (Priority: P1)

Quien liquida calculó un período con fechas equivocadas (o con un plan equivocado). Necesita
volver el período a Abierto sin aprobar nada, corregir y calcular de nuevo.

**Why this priority**: es la única forma razonable de corregir un error de captura; sin esto
el camino es aprobar-y-reversar, que ensucia contabilidad por un error administrativo.

**Independent Test**: calcular un período, descartar el borrador con motivo, comprobar que el
período queda Abierto, que la corrida queda marcada como descartada con motivo y autor, que
las novedades manuales siguen y las recurrentes generadas quedaron anuladas; editar fechas,
recalcular y ver una versión nueva.

**Acceptance Scenarios**:

1. **Given** un período Calculado, **When** pulso «Descartar borrador» y escribo el motivo,
   **Then** el período pasa a Abierto, la corrida queda `Superseded` con motivo/autor/fecha, y
   la lista de versiones la muestra en gris con el motivo.
2. **Given** un período con recurrentes materializadas y novedades manuales, **When** descarto
   el borrador, **Then** las recurrentes del período quedan anuladas (motivo automático) y las
   manuales siguen activas; al recalcular, las recurrentes se regeneran una sola vez.
3. **Given** un período Aprobado, **Then** no aparece «Descartar borrador» (el camino es
   Reversar).
4. **Given** un período descartado y vuelto a Abierto, **When** edito fechas o lo elimino,
   **Then** se permite como en cualquier período Abierto.

---

### User Story 2 - Recurrentes que aplican sólo en un sub-período del mes (Priority: P1)

Una cooperativa quincenal descuenta un préstamo completo en la segunda quincena, y un auxilio
fijo sólo en la primera. Hoy la recurrente se genera en las dos.

**Independent Test**: registrar una recurrente con «Aplica en: último del mes» en un plan
quincenal; calcular la primera quincena (no aparece) y la segunda (aparece, cuota 1); con
«Primero del mes» al revés; con «Cada período» en ambas.

**Acceptance Scenarios**:

1. **Given** una recurrente «Último del mes» y un plan quincenal, **When** calculo la quincena
   1–15 de marzo, **Then** no se genera; **When** calculo la 16–31, **Then** se genera con cuota
   1/N.
2. **Given** una recurrente «Primero del mes» en un plan semanal con 5 semanas en el mes,
   **Then** sólo la semana 1 la genera.
3. **Given** una recurrente «Cada período», **Then** se comporta como hoy.
4. **Given** un plan mensual, **Then** las tres reglas dan lo mismo (un único período por mes).
5. La pantalla de recurrentes muestra la regla en el grid y la explica al crear.

---

### User Story 3 - Períodos que saben a qué sub-período y mes pertenecen (Priority: P1)

Al crear un período, el sistema propone su número dentro del mes (quincena 1/2, década 1/2/3,
semana 1–5) y su mes de imputación a partir de las fechas; la persona los puede ajustar. Son
la base de la regla de US2 y del agrupamiento de reportes.

**Acceptance Scenarios**:

1. **Given** un plan quincenal, **When** creo el período 16/03–31/03, **Then** se propone
   «Quincena 2 · Marzo 2026» y puedo guardarlo tal cual.
2. **Given** un plan semanal, **When** creo 30/03–05/04, **Then** se propone semana 5 de
   marzo (por la fecha de inicio) y puedo cambiar a «Semana 1 · Abril».
3. **Given** un plan mensual, **Then** el sub-período es 1 y no se pregunta.
4. **Given** un período con sub-período y mes, **Then** el grid los muestra y el filtro por
   mes agrupa los períodos del mes.

---

### User Story 4 - Planes decadales y semanales (Priority: P2)

Un plan puede ser mensual (30), quincenal (15), **decadal (10)** o **semanal (7)**. El motor
prorratea salario, bases, topes y tablas por la duración del período del plan.

**Independent Test**: caso dorado semanal y caso dorado decadal calculados a mano; un
empleado de $2.100.000 en un período semanal completo devenga 2.100.000 × 7/30 = 490.000;
auxilio de transporte × 7/30; salud y pensión 4 % de la base del período; tope del IBC 25
SMMLV × 7/30; tabla de retención sobre la base mensualizada (× 30/7) y el resultado
prorrateado (× 7/30), como ya hace la quincenal.

**Acceptance Scenarios**:

1. **Given** un plan semanal, **When** creo un período de 7 días y calculo, **Then** las líneas
   coinciden con el caso dorado semanal.
2. **Given** un plan decadal, **When** el tercer período del mes tiene 11 días (21–31),
   **Then** se liquidan 10 días (convención de mes de 30) igual que el mes de 31 liquida 30.
3. **Given** un período cuya duración no coincide con la periodicidad del plan (p. ej. 8 días
   en un plan semanal), **Then** la creación se rechaza con `Payroll.PeriodLengthMismatch`,
   salvo la tercera década y el último período del mes, que admiten la diferencia del
   calendario.
4. Todas las pantallas que muestran periodicidad (planes, períodos, empleados, liquidación)
   la nombran correctamente.

---

### User Story 5 - Centro de reportes de nómina exportable (Priority: P2)

`/reportes/comprobante-nomina` se convierte en **Reportes de nómina** con cinco vistas y
exportación a Excel, PDF y Word.

**Independent Test**: para una corrida aprobada, cada vista muestra datos consistentes con
Liquidación y cada exportación descarga un archivo válido con los mismos totales.

**Acceptance Scenarios**:

1. **Comprobante por empleado**: elijo corrida y empleado → devengos, deducciones, neto, con
   explicación; PDF idéntico al de Liquidación; Excel y Word con las mismas líneas.
2. **Resumen de la corrida por concepto**: totales por concepto y naturaleza, cantidad de
   empleados, total devengos/deducciones/aportes/provisiones/neto.
3. **Detalle por empleado y concepto**: matriz empleado × concepto de la corrida.
4. **Novedades del período**: todas las novedades con empleado, concepto, cantidad/valor,
   origen (manual, recurrente cuota n/N, cartera, importada), estado y quién.
5. **Histórico por empleado entre fechas**: períodos aprobados en el rango con neto y
   totales, y el detalle por concepto de cada uno.
6. Cada vista tiene botones **Excel**, **PDF**, **Word** que descargan el archivo con el
   mismo contenido de pantalla; los totales del archivo coinciden con los de la corrida.

---

### Edge Cases

- Descartar un borrador mientras otra persona lo está calculando → `Payroll.RunInProgress`.
- Recurrente «Último del mes» y el último período del mes aún no existe cuando se calcula el
  penúltimo → no se genera en el penúltimo (la regla se evalúa contra el sub-período del
  período que se calcula, no contra lo que exista).
- Cambio de plan de un empleado de quincenal a semanal a mitad de mes → la regla de
  recurrentes se evalúa con el sub-período del período del **plan nuevo**.
- Período semanal que cruza de año (29/12–04/01) → mes de imputación elegible (diciembre o
  enero); el año va con el mes.
- Exportar una corrida con 500 empleados a Word → el archivo se genera en el servidor y se
  descarga; no se limita el tamaño, sí se registra el tiempo.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir descartar una corrida en estado borrador (`Draft`) con
  motivo obligatorio, dejándola `Superseded` con `DiscardedAt/By/Reason`, devolviendo el
  período a `Open`, anulando las novedades de origen `Recurring` de ese período y conservando
  las demás. Nunca borra. Emite auditoría `PayrollRun.Discarded`.
- **FR-002**: Descartar exige el permiso `Payroll.Runs.Calculate` y respeta el candado de
  cálculo en curso.
- **FR-003**: Cada período de pago MUST registrar `SubPeriodNumber` (1 para mensual; 1–2
  quincenal; 1–3 decadal; 1–5 semanal) y `ImputationYear/ImputationMonth`. El sistema los
  propone desde las fechas y la periodicidad del plan; la persona puede ajustarlos al crear o
  editar un período Abierto.
- **FR-004**: Una novedad recurrente MUST tener una regla `ApplyOn` ∈ {`EveryPeriod`,
  `FirstOfMonth`, `LastOfMonth`}; por defecto `EveryPeriod`. La materialización sólo genera
  la novedad si el período cumple la regla: `FirstOfMonth` ⇔ `SubPeriodNumber == 1`;
  `LastOfMonth` ⇔ `SubPeriodNumber == últimoDelMes(periodicidad)` (2, 3, o —en semanal— el
  mayor número de semana con período creado para ese mes de imputación, o 5).
- **FR-005**: La periodicidad del plan MUST admitir `Monthly=30`, `Biweekly=15`,
  `TenDay=10`, `Weekly=7`. El motor MUST usar `DaysInPeriod` del plan como base de
  proporción en todo lo que hoy usa 30/15, sin ningún literal nuevo.
- **FR-006**: Al crear un período, el sistema MUST rechazar duraciones incompatibles con la
  periodicidad (`Payroll.PeriodLengthMismatch`), con las excepciones de calendario: último
  período del mes en decadal/quincenal (28–31) y mensual (28–31).
- **FR-007**: El centro de reportes MUST ofrecer las cinco vistas de US5 y exportar cada una
  a `.xlsx`, `.pdf` y `.docx` con el mismo contenido; la exportación se hace en el servidor
  (`GET /api/reports/payroll/...?format=xlsx|pdf|docx`) con los mismos permisos de
  `Payroll.Runs.View`.
- **FR-008**: La opción de menú «Comprobante Nómina» MUST pasar a «Reportes de nómina» en la
  misma posición; la ruta vieja redirige.
- **FR-009**: Todo lo anterior MUST mantener verdes los casos dorados existentes (mensual y
  quincenal) y agregar al menos un caso dorado semanal y uno decadal.

### Key Entities

- **PayrollPlan**: gana valores `TenDay` y `Weekly` en `Periodicity`.
- **PayPeriod**: `SubPeriodNumber` (byte), `ImputationYear` (short), `ImputationMonth` (byte).
- **PayrollRecurringNovelty**: `ApplyOn` (enum `RecurringApplyRule`).
- **PayrollRun**: `DiscardedAt`, `DiscardedBy`, `DiscardReason` (nullable).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Corregir un período mal capturado toma tres acciones (descartar, editar,
  calcular) y cero asientos contables.
- **SC-002**: Una cooperativa quincenal con un préstamo «último del mes» ve la cuota
  exactamente una vez por mes en 12 meses consecutivos de prueba.
- **SC-003**: Los casos dorados semanal y decadal pasan; los ocho existentes siguen pasando.
- **SC-004**: Las cinco vistas exportan a tres formatos y los totales del archivo son
  iguales a los de la corrida en la prueba e2e.

## Assumptions

- La semana del mes la decide la persona (propuesta por la fecha de inicio); no se impone
  ISO-8601.
- Los archivos Word se generan con OpenXML (sin Office instalado); Excel con ClosedXML u
  OpenXML; PDF con QuestPDF, que ya existe.
- No se cambia la forma en que se cuentan las cuotas (al aprobar) ni la inmutabilidad de las
  corridas.
