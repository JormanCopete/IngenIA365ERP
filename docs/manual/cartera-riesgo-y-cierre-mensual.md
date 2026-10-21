# Cartera: calificación, deterioro y cierre mensual

> Feature 013 (cartera financiera), fase 12 (US9, entrega K1, Anexo 1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §12, [data-model.md](../../specs/013-cartera-financiera/data-model.md)
> §23 y [contracts/contabilidad.md](../../specs/013-cartera-financiera/contracts/contabilidad.md) §11. Aquéllos son el contrato; esto es la
> receta para **el jefe de cartera, la contadora y el consejo**. La operación del cierre (requisitos, reintentos, cuadre) está en el runbook
> [cartera-cierre-mensual.md](../operaciones/cartera-cierre-mensual.md).

Cada crédito tiene **dos** calificaciones:

- la **informativa** del día, que se calcula cada vez que se pide con el mismo motor del cierre y **no se guarda** (la pantalla la rotula
  «Informativa»); sirve para anticipar el cierre y para la cobranza;
- la **oficial**, la del último **cierre mensual** vigente: se calcula una vez al mes, se **congela** con la información registrada hasta
  ese momento (R) y es la que va a Contabilidad, a los reportes y a la categoría contable de todos los hechos del mes siguiente.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Cierres (base y mensuales), requisitos del mes, ordenar y reabrir | `/cartera/cierres` | `Lending.Ratings.Read` · `Lending.MonthlyClose.Execute` · `.Reopen` |
| Calificación informativa de la cartera a una fecha | pestaña «Calificación informativa» de `/cartera/cierres` | `Lending.Ratings.Read` |
| Detalle de un cierre: encabezado, líneas, cambios, cuadre, partes y reaperturas | `/cartera/cierres/{id}` | `Lending.Ratings.Read` (cuadre: `Lending.Reconciliation.View`) |
| Calificación de un crédito y calificación por análisis | pestaña «Calificación» de `/cartera/creditos/{id}` | `Lending.Ratings.Read` · `Lending.Ratings.Override` |
| Norma, política SARC del consejo y efectiva | pestaña «Riesgo» de `/cartera/parametros` | `Lending.Parameters.View` · `Lending.RiskPolicy.Manage` · `.Validate` |

Contabilidad remite a `/cartera/cierres` cuando responde «Cartera no ha cerrado el mes» (`Accounting.Period.LendingNotClosed`).

## 2. Cómo se califica

El motor trabaja en dos fases (todo explicado en la línea de cada crédito, botón «Por qué»):

1. **Por crédito**, se reúnen las **candidatas** y gana la **peor**:
   - **altura de mora** en días, contra los rangos de su modalidad SARC (consumo, comercial, microcrédito, vivienda) del Anexo 1;
   - **pago único**: pasado el plazo de su regla, la categoría que dice la norma;
   - **crédito nuevo**: el que nace después del último cierre entra en la categoría que la norma trae para él (A);
   - **reestructurado**: el mínimo de la norma o la categoría con que se reestructuró, y mejora por tramos de cuotas pagadas seguidas;
     si vuelve a la mora, regresa;
   - **calificación por análisis** (§4), **insolvencia** (negociación o fracaso) y, desde K3, la evaluación de cartera.
2. **Arrastre**, por deudor y modalidad SARC: si un crédito del deudor queda en B o peor, sus otros créditos **de la misma modalidad** se
   llevan a esa categoría (causa «Arrastre»). No cruza modalidades (un consumo en C no arrastra un comercial) y no alcanza las codeudas
   (salvo que la norma lo diga). Mientras rija **D19** no hay excepción de aportes en el arrastre.

La **subcategoría** (E1, E2) sale de la condición de la norma (días de mora o meses en E). La **categoría contable** es la homologada: E1 y E2
son E.

La calificación informativa aplica el arrastre según `Riesgo.CalificacionDiariaConArrastre` (encendido por defecto).

## 3. El deterioro

- **Individual** (Anexo 1): base = capital − valor admisible de las garantías según los meses de mora (hipotecaria y no hipotecaria, por
  tramos) − aportes. **Mientras rija D19, o sin indicadores de la entidad vigentes, los aportes no se descuentan** y la explicación lo dice.
  Sobre la base, el porcentaje de la categoría (y subcategoría) de la **política efectiva** (§5). Los intereses y otros conceptos de un
  crédito en suspenso se deterioran al 100 %. Redondeo por `Riesgo.RedondeoDeterioro` (centavo o peso).
- **General**: el mínimo del perfil regulatorio, nunca menor que el valor nacional `DETERIORO_GENERAL_MINIMO`, más el **adicional** de la
  política del consejo (que puede compensar el individual), por sucursal, modalidad, sub-segmento, empleado y modelo.
- Todo deterioro se reconoce **en delta** frente al cierre anterior: aumento al gasto; disminución primero contra lo llevado al gasto en el
  mismo ejercicio y el resto a recuperación (`Contabilidad.DisminucionDeDeterioro = MismoEjercicioPrimero`, o todo a recuperación si la
  contadora lo elige: pregunta C2).
- El **primer cierre tras activar** se compara con el **cierre base** (el de la carga): el deterioro que vino migrado **no se reconoce otra
  vez**.

## 4. La calificación por análisis

El analista puede llevar un crédito a una categoría **peor** que la que dan las reglas, con motivo (y soporte si lo hay). Una igual o mejor se
rechaza con la categoría de las reglas (`Lending.AnalysisRating.NotWorse`). Hay a lo sumo una vigente por crédito; levantarla pide motivo. El
cierre la toma tal como estaba vigente a R, y su arrastre alcanza a los demás créditos del deudor en la misma modalidad.

## 5. La política SARC del consejo

El consejo puede **endurecer** la norma (nunca suavizarla): inicios de categoría menores, porcentajes mayores, fracciones de garantía menores,
un deterioro general adicional. Se registra como **versión** con su fecha de inicio (hoy o después) y un JSON con sólo las celdas que cambian
(el esquema parcial del Anexo 1); una celda menos estricta que la norma se rechaza y la pantalla la lista con su ruta, lo que dice la norma y
lo propuesto. La versión queda **pendiente de validar** hasta que otra persona —nunca quien la registró— la valide con el **punto del acta**
del consejo que la aprobó. Rige celda por celda lo más estricto: si después la norma se endurece por encima de la política, gana la norma.

## 6. El cierre del mes

El cierre del mes M se ordena desde `/cartera/cierres` al empezar M+1 (o lo ordena la tarea programada si
`Riesgo.CierreProgramado` está encendido). Antes de ordenar, la pantalla muestra los **requisitos** y su detalle:

| Requisito | Qué pide |
|---|---|
| Cierre anterior vigente | el mensual de M−1 o, en el primero tras activar, el cierre base |
| Mes abierto en Contabilidad | Contabilidad todavía no cerró M |
| Causación al día hasta fin de mes | todo crédito vivo causado hasta el último día de M |
| Valores nacionales al día | la réplica de la central sincronizada (si la central no responde, el cierre se niega con motivo) |
| Ninguna corrida de cierre en curso | una sola a la vez |

Ordenar **sólo encola**: el proceso de Cartera corre el cierre por tandas (paso 1, las líneas por crédito) y una transacción final (paso 2)
que inserta el encabezado con R, los documentos normativos usados con su huella, la política, el modelo por segmento y las marcas; el
deterioro general; y los **hechos** para Contabilidad, fechados el último día de M:

- `CarteraReclasificadaPorCalificacion` para cada crédito que **cambió de letra** —todo saldo con categoría (capital, intereses, cargos) y el
  deterioro que traía—, desde la categoría con que salieron sus hechos (la del cierre anterior o, si nació después, la de crédito nuevo);
- `DeterioroCalculado` con el delta por crédito y concepto y el general;
- el informativo `CierreDeCarteraRealizado`, que es lo que deja a Contabilidad cerrar M.

Los hechos salen por **partes** que forman el lote `LendingMonthlyClose` con un comprobante **PV** resumido (si la matriz da la misma cuenta a
las dos categorías, la reclasificación queda «sin comprobante, mismas cuentas»).

Encabezado y líneas **no se editan nunca**. El **reporte de cambios** dice, por crédito, la categoría anterior, la nueva y la causa (el
primero, frente al base).

### Marcas del cierre

- **Pendiente de validar**: alguna tabla del Anexo 1 transcrita sin cotejar por la contadora (pregunta E8). No bloquea: el cierre la usa
  tal cual y la marca.
- **Modelo obligatorio no disponible**: la cooperativa está obligada al modelo de pérdida esperada (Anexo 2) por la regla vigente y su
  perfil regulatorio, y ese modelo no llega hasta K6a. Mientras tanto se aplica el **Anexo 1** y se alerta
  (`Cartera.ModeloObligatorioNoDisponible`, `Cartera.ObligacionSobrevenida`). El modelo por segmento se ve en el encabezado.
- **Historial incompleto**: créditos sin los meses de mora que pide la regla (informativo en K1).

## 7. Reabrir

Sólo el **último** cierre mensual vigente y sólo **antes de que Contabilidad cierre** su mes; nunca el base. Reabrir pide motivo (y,
si el reglamento lo exige, el punto de acta), encola la corrida de reapertura, que emite un `ReversoDeCierre` por cada parte del cierre en un
lote propio (otro PV) y el informativo `CierreDeCarteraReabierto`. El cierre queda **Reabierto** (sus líneas dejan de ser vigentes) y volver a
ordenar el cierre crea la **versión siguiente**, que lo reemplaza.

## 8. Pagos con fecha valor de un mes cerrado

Un pago con fecha valor en un mes con cierre vigente exige la atribución `Lending.Payments.BackdateBeforeClose` (y su límite de monto). Se
aplica sin reescribir el cierre: la huella de sus líneas no cambia, y su hecho contable va al **primer mes sin cierre** de Cartera.

## 9. Lo que valida la contadora

- C2: la disminución del deterioro (mismo ejercicio primero o todo a recuperación).
- C4: que la reclasificación lleve todo saldo con categoría (capital, intereses, cargos y deterioro).
- E8: el cotejo de las tablas del Anexo 1 marcadas «pendiente de validar» (la de comercial persona jurídica viene de una imagen).
- E9: el redondeo del deterioro (`Riesgo.RedondeoDeterioro`).
- El patrimonio técnico y la relación de solvencia vigentes (`/contabilidad/indicadores-regulatorios`): sin ellos el deterioro no descuenta
  aportes y la validación de concentración bloquea (`Cartera.PatrimonioTecnicoNoDisponible`).
