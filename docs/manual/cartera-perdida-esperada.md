# La pérdida esperada en Cartera (Anexo 2): guía para la contadora

> Feature 013, entrega K6a. Para la contadora y el jefe de cartera. Cómo se opera: tema «Pérdida esperada (Anexo 2) y modelo de deterioro» del
> manual en línea; cómo se despliega y se registra la norma: [runbook](../operaciones/cartera-perdida-esperada.md). Todo lo marcado
> **pendiente de validar** es una lectura del equipo que usted confirma o corrige (quickstart §12; preguntas E2, E8, E9).

## 1. Qué cambia y qué no

- **Qué segmentos**: consumo con libranza, consumo sin libranza y comercial de persona natural. El resto (microcrédito, comercial de persona
  jurídica, vivienda cuando llegue) sigue por el Anexo 1.
- **A quién**: a las cooperativas **obligadas** por la Circular Externa 100 (ahorro y crédito, multiactivas e integrales con sección de ahorro,
  fondos de empleados de categoría plena) desde su primer cierre con la entrega, y a las demás si el consejo lo **adopta** voluntariamente.
- **Qué no cambia**: el cierre mensual sigue siendo el mismo proceso, con los mismos hechos para Contabilidad (`DeterioroCalculado` por la
  diferencia frente al cierre anterior y la reclasificación por categoría). Las cuentas, la matriz contable y el cuadre no cambian.

## 2. El cálculo, crédito por crédito

1. **Puntaje**: Z = suma de coeficientes × variables del segmento (mora de los últimos 3, 12, 24 y 36 meses; asociado activo; para fondos de
   empleados, la cuota frente al salario mínimo y el plazo). Puntaje = 1 / (1 + e^(−Z)), **redondeado a cuatro decimales** (pendiente de validar,
   E9: en el borde decide la calificación).
2. **Calificación del modelo**: por los cortes del puntaje del Anexo 2 (inclusive, pendiente de validar). Entra al motor de calificación como una
   candidata más: la peor manda, con arrastre, reestructurados, evaluación de cartera y calificación por análisis igual que en el Anexo 1.
3. **Incumplimiento**: más de 90 días en consumo y de 120 en comercial, o el pago único vencido. Se alinea por deudor (num. 4.1).
4. **PI**: de la tabla del tipo de organización de la cooperativa (o de la aplicación **voluntaria** si la adoptó) para la calificación final; 100 %
   si está en incumplimiento.
5. **PDI**: sin incumplimiento, la del segmento (45 % / 50 %); en incumplimiento, la del tipo de garantía según los días en incumplimiento. Mapeo de
   garantías (**pendiente de validar**): prenda, hipoteca y fondo de garantías **perfeccionados** a prendaria, hipotecaria y fondo de garantías;
   sin perfeccionar, codeudor y avalista a «no idónea»; los aportes no son garantía para la PDI (Anexo 2, num. 4.3) y van a «sin garantía»; pagaré y sin garantías a «sin garantía». Con **varias garantías**, la PDI más
   conservadora (la mayor) — decisión del equipo a falta de norma explícita.
6. **Exposición** (VEA): capital, intereses y otros conceptos, por concepto, **menos los aportes del deudor a prorrata** (Anexo 2, num. 4.2;
   decisión del dueño del 2026-10-18 tras revisar la norma, feature 014 D39): se descuentan con las mismas condiciones del deterioro individual
   (Anexo 1, num. 5.1) —el parámetro `Riesgo.DescuentoAportesDeterioro`, la norma que los admite y la entidad sin pérdidas acumuladas y con
   solvencia—, primero del capital, después de los intereses y al final de los otros conceptos (orden **pendiente de validar**). Si cubren todo el
   VEA, el deterioro es cero. El ahorro permanente, que el num. 5.1 también permite descontar, **todavía no** se descuenta (pendiente).
7. **Deterioro** = PI × PDI × exposición, por concepto, redondeado con `Riesgo.RedondeoDeterioro`. Lo causado de un crédito en suspenso se
   deteriora al 100 % si el documento lo dice (**pendiente de validar** si también en pérdida esperada). La suspensión de la causación sigue la
   altura de mora del Anexo 1: una calificación C del modelo con el crédito al día no suspende (**pendiente de validar**).
8. **Calificación contable**: la homologada del Anexo 2 (causa «Homologación»).
9. **Deterioro general**: el documento dice si alcanza a los segmentos en pérdida esperada (la semilla: sí, **pendiente de validar**).

Cada línea del cierre guarda Z, puntaje, calificación del modelo, incumplimiento y días, la clave de la tabla de PI, la PI, el tipo de garantía, la
PDI, la exposición y una explicación por componente («Por qué» en el detalle del cierre).

## 3. El historial de mora y los meses que faltan

La PI necesita la mora máxima de cada mes de los últimos 36 (o desde el desembolso). Los meses que el sistema anterior no trajo se cargan con la
plantilla «Historial de mora complementario», con la fuente (p. ej. «reporte de mora mensual del sistema anterior»). Si aun así faltan meses en
el primer cierre en pérdida esperada, se completan con `Riesgo.ReglaMesesFaltantes`:

| Regla | Qué pone en el mes que falta |
|---|---|
| `MayorObservada` (sembrada, **pendiente de validar**) | la mayor mora observada en los meses que sí hay |
| `DelCorte` | la mora del corte de la migración |
| `Fija` | los días de `Riesgo.MesesFaltantesDiasFijos` |

El cierre deja esos créditos marcados y los lista en el reporte de cambios: conviene revisarlos uno a uno.

## 4. La adopción voluntaria

Requiere decisión del **consejo** (punto del acta), **estudio de impacto** y **aviso** a la Superintendencia con la antelación que diga la norma (15
días en la regla sembrada, contados contra la fecha desde la que rige: pregunta E2, **pendiente de validar**). Rige desde el primer cierre de su
fecha y nunca recalcula un cierre anterior: el primero en pérdida esperada contabiliza sólo la diferencia frente al último del Anexo 1. El
desistimiento sigue los mismos pasos. Mientras la plataforma no registre la PI de la aplicación voluntaria (CE 100), **no se puede adoptar**.

## 5. Lo que la contadora valida (T1041)

- [ ] Los casos dorados pe01–pe10 (`tests/IngenIA365ERP.Domain.Tests/Lending/Risk/Casos/`), calculados a mano por el equipo: PI, PDI, exposición,
      deterioro por crédito, calificación homologada y explicación.
- [ ] La transcripción del Anexo 2 (ecuaciones, cortes, tablas de PI, PDI y homologación), en especial el «00» del tercer tramo de «sin garantía»
      (se puso 90) y que la PI de libranza de mutuales e integrales sin ahorro sea la misma.
- [ ] Que «para moras mayores a 30 días la PDI no se modifica» quiere decir que la PDI sin incumplimiento rige hasta el incumplimiento.
- [ ] Si las ventanas de 3, 12, 24 y 36 meses incluyen el mes de evaluación.
- [ ] El corte inclusive y el redondeo del puntaje a cuatro decimales (E9).
- [ ] La regla de meses faltantes sembrada «la mayor observada».
- [ ] La PDI con varias garantías (la más conservadora) y el mapeo de garantías.
- [ ] Si basta la ficha de asociado para «asociado activo» (sin ficha, no activo) y si la cuota de VALCUOTA es la total o sólo capital e intereses.
- [ ] Si lo causado de un suspendido se deteriora al 100 % también en pérdida esperada, y si el deterioro general alcanza a esos segmentos.
- [ ] La antelación del aviso y desde qué fecha se cuenta (E2).

Con cada punto confirmado, el equipo marca la tabla `verified = true` en una versión nueva del documento (nunca cambia un cierre hecho).
