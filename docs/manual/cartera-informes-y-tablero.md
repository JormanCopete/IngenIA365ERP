# Cartera: informes de gestión y tablero

> Feature 013 (cartera financiera), fase 14 (US15, base del centro de informes y vistas de K1), fases 15 a 18 (vistas de K2 a K4) y fase 20
> (US15, entrega K5: calidad, cosechas, concentración, documentos emitidos y el tablero). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §17 (informes y tablero) y
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §12.1 (parámetros `Informes.*`). Aquellos son el contrato; esto es la receta
> para gerencia, la jefatura de cartera y la contadora.

Cartera ofrece dos formas de mirar la cartera sin tocarla:

- el **centro de informes** (`/cartera/informes`), donde cada informe es una tabla que se filtra, se abre fila por fila y se exporta a Excel,
  PDF o Word;
- el **tablero** (`/cartera/tablero`), con nueve fichas que resumen la cartera a una fecha y llevan, con un clic, al informe o la pantalla que
  las explica.

Los dos muestran **sólo** las oficinas y líneas del alcance de quien consulta: pedir una oficina o una línea que no es suya da cifras en cero,
nunca las de otra.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Ver un informe en pantalla | Cartera → Informes de cartera (`/cartera/informes`) | `Lending.Reports.View` |
| Ver un informe que trae datos del deudor (nombre, crédito) | ídem | además `Lending.Loans.Read` |
| Exportar un informe (Excel, PDF, Word) | botones de exportar | `Lending.Reports.Export` |
| Exportar un informe con datos del deudor | ídem | además `Lending.Reports.ExportPersonalData` |
| Ver el tablero | Cartera → Tablero de cartera (`/cartera/tablero`) | `Lending.Dashboard.View` |

Sin el permiso, la ruta responde «no encontrado», igual que si no existiera, y no se audita nada (C84). **Cada exportación a archivo queda en
la auditoría** (`Lending.Report.Exported`) con el informe, los filtros, el formato y el número de filas; consultar en pantalla no se audita.

El tablero no trae ningún dato personal: sólo cifras. Por eso basta un permiso de los «View».

## 2. Los informes por entrega

Cada informe declara desde qué entrega existe; el selector de la pantalla sólo ofrece los de la entrega vigente y, a quien no tiene
`Lending.Loans.Read`, sólo los que no traen datos del deudor.

| Entrega | Informe | Filtros propios | Datos del deudor |
|---|---|---|---|
| K1 | Cartera por edades (`aging`) | corte, rangos | sí |
| K1 | Colocación (`placement`) | rango, por línea, oficina, asesor o mes | no |
| K1 | Recaudo (`receipts`) | rango, por línea, recaudador, modalidad o medio | no |
| K1 | Movimientos (`transactions`) | rango, crédito, clase, tipo de fecha | sí |
| K1 | Usura (`usury`) | corte, puntos de alerta | sí |
| K1 | Mensajes al deudor (`debtor-messages`) | rango, propósito, canal, estado | sí |
| K1 | Conciliación de la migración (`migration-reconciliation`) | carga | sí |
| K2 | Pagadurías (`payroll-agreements`) · cuadre del archivo de descuentos (`payroll-deduction-reconciliation`) | corte · convenio y período | no · sí |
| K3 | Reestructurados (`restructured`) · cartera castigada (`charge-offs`) | corte y mecanismo · rango | sí |
| K4 | Cartera por etapa, promesas, acuerdos de pago, avisos previos | corte, etapa, gestor, estado | sí |
| **K5** | **Calidad de la cartera** (`quality`) | corte; total o por oficina, línea o modalidad | no |
| **K5** | **Cosechas** (`vintages`) | meses de desembolso (desde, hasta) | no |
| **K5** | **Concentración** (`concentration`) | corte; por deudor, grupo conectado, línea, sector o pagaduría; mayores; señal desde | **sólo por deudor y por grupo** |
| **K5** | **Documentos emitidos** (`issued-documents`) | rango de emisión, clase, estado | sí |

Filtros comunes a todos: desde y hasta (hasta cinco años), fecha de corte, oficina, línea, modalidad, sub-segmento y asesor. Un filtro propio
con un valor que el informe no admite responde con la lista de los que sí admite.

## 3. Definiciones de K5 — a validar por la contadora

Las cuatro definiciones que siguen las fijó el desarrollo para poder construir los informes. **Están pendientes de validar por la contadora**:
si alguna no es la que la cooperativa usa, se cambia la regla y se vuelven a pedir los informes (nada se guarda).

### 3.1 Calidad de la cartera

Sale del **último cierre de cartera vigente** a la fecha de corte —el cierre mensual no reabierto más reciente o, antes del primero, el cierre
base de la activación—, con la calificación y el deterioro **oficiales** de ese cierre, nunca la calificación diaria. La nota del informe dice
de qué cierre salen las cifras.

| Indicador | Definición |
|---|---|
| Cartera bruta | Saldo de capital de los créditos del cierre que **no** están castigados. |
| Vencida | Capital de los créditos con días de mora al cierre **mayores** que el parámetro `Informes.DiasCarteraVencida` (30 por defecto). |
| Vencida/bruta | Vencida sobre bruta, en %. |
| En riesgo (B–E) | Capital de los créditos calificados B, C, D o E en el cierre. |
| Riesgo/bruta | En riesgo sobre bruta, en %. |
| Deterioro | El individual de cada crédito más el general del cierre de las oficinas y modalidades del alcance. |
| Cobertura | Deterioro sobre vencida, en %. |

El deterioro **general** se calcula por oficina y modalidad, no por línea de crédito: si el informe se pide por una línea, o quien lo pide sólo
ve algunas líneas, se suma sólo el individual (y la nota lo dice); abierto por línea, el general va en una fila aparte.

### 3.2 Cosechas

Por **mes de desembolso** (la cosecha) y por **mes de maduración** k (el cierre del mes k meses después), «Mora 30», «Mora 60» y «Mora 90»
son el saldo de capital de los créditos de la cosecha con **30, 60 o 90 días de mora o más** al cierre de ese mes, sobre el monto desembolsado
en el mes de la cosecha, en %.

- El mes de desembolso de un crédito nacido en el ERP es el de su primer desembolso sin reversar, y su monto suma todos los tramos; el de un
  crédito migrado, la fecha y el monto original de su fila en la carga de migración.
- Los cierres son los mensuales vigentes y, para los meses anteriores a la activación, el cierre base.
- Un crédito castigado sigue contando con el saldo que traiga su línea del cierre.
- Una cosecha sin cierres posteriores sale con su monto y sin mora.

### 3.3 Concentración

- **Saldo**: capital de la posición diaria al corte de los créditos vivos no castigados, como deudor.
- **Participación**: el saldo sobre el total del alcance, en %.
- **% del límite individual** (sólo por deudor y por grupo): el saldo sobre el límite individual, que es el porcentaje vigente de
  `Concentracion.LimiteIndividual` (acotado por el límite legal) por el **patrimonio técnico vigente** registrado en Contabilidad. Es el mismo
  límite que usan las validaciones de la originación y el desembolso, que hoy miden sin garantía admisible.
- **Señal**: «Cerca del límite» desde el porcentaje del filtro «Señal desde» (80 % por defecto) y «Supera el límite» por encima del 100 %.
- **Grupo conectado**: el mismo cálculo que usa la validación de concentración de la originación (grupos registrados, vinculados y codeudores
  comunes); su saldo suma el de sus miembros dentro del alcance. Los grupos de una sola persona no se listan.
- **Sector**: la actividad económica (CIIU) de la persona en el maestro; «Sin sector» si falta.
- **Pagaduría**: la del vínculo de libranza vigente al corte; «Sin pagaduría» si no tiene.

Sin patrimonio técnico vigente (o más viejo que `Contabilidad.IndicadoresVigenciaMeses`), el informe sale igual, sin el porcentaje del límite y
con una advertencia en la nota.

### 3.4 Documentos emitidos

Los documentos al deudor emitidos en el rango, con su estado **a hoy**, calculado: vigente, sujeto a firmeza o anulado (a mano o por el reverso
de lo que respaldaba). Ver [cartera-documentos-al-deudor.md](cartera-documentos-al-deudor.md).

## 4. El tablero

Cartera → **Tablero de cartera**. Se elige la oficina y la línea (sólo las de su alcance) y la fecha de corte (hoy por defecto). Cada ficha
muestra la cifra al corte y, debajo, la del mes anterior; un clic abre su informe.

| Ficha | Qué es | Mes anterior | Lleva a |
|---|---|---|---|
| Saldo de capital | Capital de las posiciones diarias al corte (créditos vivos no castigados). | Al último día del mes anterior. | Cartera por edades |
| Colocación del mes | Desembolsos sin reversar del primero del mes al corte. | El mismo tramo del mes anterior. | Colocación |
| Cartera vencida sobre bruta | Como la calidad (§3.1), pero con las posiciones diarias al corte. | Al último día del mes anterior. | Calidad |
| Deterioro del último cierre | Individual más general del último cierre vigente. | El del cierre anterior. | Calidad |
| Recaudo frente al esperado | Pagos sin reversar del mes hasta el corte sobre lo exigible del mes hasta el corte según el plan vigente de cada crédito, en %. | El mes anterior completo. | Recaudo |
| Mora 30 de las cosechas del año | Capital con 30 días de mora o más de los créditos desembolsados en los últimos doce meses, sobre lo desembolsado. | Al último día del mes anterior. | Cosechas |
| Cumplimiento de promesas | Promesas cumplidas sobre las resueltas (cumplidas más incumplidas) de las que vencían en el mes. Antes de la entrega de cobranza, «Disponible con cobranza». | El mes anterior. | Promesas |
| Mensajes pendientes y rechazados | Entregas de la bandeja de Cartera pendientes, en lote, rechazadas o con la validación fallida. Se marca si hay rechazos. | — | Bandeja de mensajes |
| Alertas pendientes | Alertas de Cartera sin atender que usted puede ver. Se marca si hay alguna. | — | Alertas |

Sin un cierre vigente, la ficha de deterioro sale sin dato y lo dice. La calidad y las cosechas del tablero siguen las definiciones de §3,
pendientes de validar por la contadora.

## 5. Parámetros

| Parámetro | Para qué | Defecto | Entrega |
|---|---|---|---|
| `Informes.RangosDeEdad` | Los rangos de la cartera por edades. | 30, 60, 90, 180, 360 | K1 |
| `Informes.PuntosAlertaUsura` | Desde cuántos puntos bajo la usura alerta el informe de usura. | 2,0 | K1 |
| `Informes.DiasCarteraVencida` | Desde cuántos días de mora (más de este número) la cartera cuenta como vencida en la calidad y el tablero. | 30, pendiente de validar | K5 |

Se cambian en Cartera → Parámetros con vigencia y motivo; un cambio vale para los informes desde su fecha.

## 6. Lo que queda para el dueño

- Validar con la contadora las definiciones de §3 (calidad, cosechas y concentración) y el defecto de `Informes.DiasCarteraVencida`.
- Confirmar si la concentración debe medir también con el límite con garantía admisible para los deudores que la tengan (hoy, como las
  validaciones, mide sin garantía) y si el 80 % de la señal es el umbral que quiere ver.
