# 024 — Causación configurable de Cartera

Pedido del dueño del 2026-11-16, sobre la rama `mejoras-estado-cuenta-recaudo`. Plan de trabajo y decisiones; las recetas de uso quedan en
`docs/manual/` al cerrar cada fase.

## Lo que pidió el dueño

1. La tasa de mora puede variar cada mes: debe regir una **tasa general**, no la de cada línea.
2. Un parámetro para contabilizar la causación **en el mismo comprobante o separada** (corriente, mora y demás rubros), para saber qué
   corresponde a cada uno.
3. Un parámetro por empresa que elija, **por separado**, si la causación es **automática o manual** para: interés corriente, interés de mora y
   otros rubros (administración, seguro, otros); y si la causación es **diaria o al inicio del ciclo** según la periodicidad.
4. Un **proceso de causación** (manual o automático) con sus parámetros de ejecución:
   - Manual al inicio del ciclo: se elige de qué fecha a qué fecha va, si es la primera o segunda quincena (en quincenal), y queda causada la
     cuota completa —capital, interés y demás rubros— lista para que el asociado la pague completa. Igual en mensual (sin elegir quincena),
     semanal, decadal o diaria.
   - Dinámico, seguro y simple, con **borrador** para validar antes de dejarlo en firme y contabilizar.
   - Se puede repetir, pero los créditos con abonos no se tocan al recalcular; con parámetros de seguridad.
   - Todas las validaciones de fechas, ciclos cerrados, etc.

## Decisiones del dueño (formularios del 2026-11-16)

| # | Pregunta | Decisión |
|---|---|---|
| 1 | Tasa de mora general | La digita la empresa con vigencia (efectiva anual), topada siempre en la usura. |
| 2 | Comprobantes separados | Un **tipo de comprobante por concepto** (p. ej. CA corriente, CM mora, CS seguro…), configurable. |
| 3 | La mora | Día a día sobre lo vencido; su contabilización automática (cada noche) o manual (por rango). |
| 4 | Ámbito de «diaria / inicio del ciclo» | Por empresa, con excepción por línea. |
| 5 | Inicio del ciclo | **Parametrizable**: reconocimiento contra **ingreso diferido** (que pasa a ingreso día a día, NIIF) o **todo a ingreso** el primer día. |
| 6 | Reproceso y seguridad | Borrador libre; poner en firme y reprocesar exige permiso, motivo y aprobación de **otra persona**; al reprocesar, los créditos con abonos posteriores quedan intactos y se informan. |
| 7 | Mora general en créditos existentes | A **todos**, desde su vigencia; no se admite vigencia con fecha pasada. Sin tasa general vigente, la de la línea. |
| 8 | Administración | **Rubro propio** (en la línea, el libro, la prelación y la contabilidad), con su causación separada del seguro. |

## Principio de diseño: el motor sigue calculando día a día

Las decisiones de la 013 (FR-062, FR-063, D8, R33) fijan que el **cálculo** de intereses es diario; así quedan exactos la usura, la mora, los
abonos y las cancelaciones, y el libro siempre es igual a la reconstrucción. Esta feature **no cambia eso**. Lo que se vuelve configurable es:

- **cuándo y cómo se contabiliza** lo causado (cada noche o por un proceso manual; un comprobante o uno por concepto), y
- **el reconocimiento al inicio del ciclo**: el primer día del período (o cuando la persona corre el proceso) se reconoce la cuota completa por
  cobrar —contra ingreso diferido o contra ingreso, según el parámetro— y la caja la ofrece como lo causado para pagar completo. Cada día la
  causación real pasa el diferido a ingreso (o, en «todo a ingreso», no vuelve a reconocer). Al vencer, cancelar o abonar se **ajusta** la
  diferencia entre lo reconocido y lo realmente causado; la mora sigue corriendo sólo desde el vencimiento.

Es una enmienda a D8 en cuanto a la **contabilización** (no al cálculo), que se anota en `specs/013-cartera-financiera/` al cerrar la fase 4.

## Parámetros nuevos (Cartera, con vigencia)

| Clave | Valores | De fábrica | Ámbito |
|---|---|---|---|
| `Mora.TasaGeneral` | EA en fracción; vacío = la de la línea | vacío | General; **sin vigencia hacia atrás** |
| `Contabilidad.CausacionSeparadaPorConcepto` | Sí / No | No (un CA como hoy) | General |
| `Causacion.ModoInteresCorriente` | Automatica / Manual | Automatica | General |
| `Causacion.ModoInteresDeMora` | Automatica / Manual | Automatica | General |
| `Causacion.ModoSeguro` | Automatica / Manual | Automatica | General |
| `Causacion.ModoAdministracion` | Automatica / Manual | Automatica | General |
| `Causacion.ModoOtrosRubros` | Automatica / Manual | Automatica | General |
| `Causacion.Momento` | Diaria / InicioDelCiclo | Diaria | General y línea |
| `Causacion.InicioDelCiclo.Reconocimiento` | IngresoDiferido / IngresoDirecto | IngresoDiferido | General |

De fábrica nada cambia frente a hoy.

## Fases (en orden; cada una compila, prueba, se sube y se documenta)

### Fase 1 — Mora general, comprobantes por concepto y modo manual de contabilización

- **Mora general**: serie con vigencia leída completa (no sólo en los extremos) y llevada a la entrada de la reconstrucción como la usura
  (`TasaDelDia`, la llave de su memoria, los cortes de ventana); topada en la usura; sin vigencia hacia atrás (validación del escritor de
  parámetros para esa clave); casos dorados.
- **Comprobantes por concepto**: con el parámetro en Sí, cada hecho de causación (ICA, MCA, SCA y los nuevos) va en su propio documento, de
  manera independiente (la clave de agrupación incluye el hecho), con el tipo que la empresa mapee en «Tipos de comprobante de Cartera»; semilla
  de los tipos sugeridos (inactivos hasta que la empresa los mapee).
- **Modo manual de contabilización por concepto**: un disparador «Manual» para el hecho de cada concepto: lo causado queda esperando y no lo
  toma ni el ciclo nocturno ni el programador de lotes; lo toma el proceso de causación (fase 3). Ámbito por tipo de hecho visible en
  `/cartera/parametros`.

### Fase 2 — Administración como rubro propio

- Clase en los seguros de la línea (Seguro / Administración), concepto propio en el libro, su hecho de causación (administración causada) con
  sus roles en la matriz, su lugar en la prelación por rubros, sus columnas en el estado de cuenta y en la caja. Migración nueva y aditiva.
  Los créditos ya desembolsados siguen como están.

### Fase 3 — Proceso de causación (manual) con borrador

- Pantalla `/cartera/causacion`: periodicidad, rango (propuesto por el período; en quincenal, primera o segunda quincena), conceptos (los que
  estén en modo manual), filtros opcionales (línea, oficina, pagaduría).
- **Calcular → borrador**: por crédito y concepto lo que se causa o reconoce, totales, y lo que queda fuera con su motivo (abonos posteriores,
  período contable cerrado, mes de Cartera cerrado, falta de regla contable, falta de usura…). Se recalcula cuantas veces se quiera.
- **Poner en firme**: permiso propio, motivo y **aprobación de otra persona** (plataforma de aprobaciones existente); emite los hechos y ordena
  el lote contable (uno o varios comprobantes según el parámetro).
- **Reprocesar en firme**: mismo control; los créditos con abonos posteriores no se tocan y se listan; el resto se ajusta por diferencia.
- Validaciones: Cartera activada, rango dentro de períodos contables abiertos y meses de Cartera sin cerrar, sin solaparse con otro proceso en
  firme del mismo concepto y período, fecha no futura en diaria, usura disponible, mora general vigente si aplica.

### Fase 4 — Reconocimiento al inicio del ciclo

- Hecho nuevo «cuota reconocida del período» (interés, seguro, administración y otros de la cuota), automático el primer día del período o por
  el proceso manual de la fase 3; contra ingreso diferido o contra ingreso según el parámetro.
- La causación diaria de esos créditos pasa el diferido a ingreso (o no vuelve a reconocer); ajuste al vencer, cancelar o abonar (con
  `Pagos.InteresesAlPagarAntesDelVencimiento`: con «corrido» se devuelve lo no corrido; con «cuota completa» no).
- La caja y el estado de cuenta muestran la cuota reconocida como lo causado para pagar completo; la mora corre desde el vencimiento; el pago
  de esa cuota no pide el aviso de «anticipo que no abona a capital».
- Enmienda de D8 en los artefactos de la 013.

## Fuera de alcance

Cambiar el cálculo diario del motor; novación y refinanciación; contabilizar un comprobante mensual único (sigue uno por día y concepto, o por
proceso manual).
