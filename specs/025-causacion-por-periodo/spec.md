# 025 — Causación por período y tasa mensual en Cartera

**Rama**: `025-causacion-por-periodo` · **Creada**: 2026-11-24 · **Estado**: borrador con las decisiones D1–D3 del dueño (2026-11-24)
**Base**: la 013 (motor de Cartera), la 024 (causación configurable) y la carga de cartera de la 016.

## Problema

Casi todas las cooperativas y fondos de empleados que se implantan vienen de herramientas que causan **por período del calendario**:

- El día 1 se corre la causación del 1 al 30. Cada crédito recibe la cuota que vence en el mes, con **30 días** de interés sobre su saldo.
- En quincenal se corre del 1 al 15 y del 16 al 30, con **15 días**. En decadal se toma un tercio del mes y en semanal 7/30.
- La tasa se pacta y se piensa **mensual vencida** (1,8 % MV), no efectiva anual.

El ERP calcula bien la cuota normal: con 30/360 y vencimientos mensuales, cada cuota lleva 30 días sobre el saldo, igual que esas herramientas. Pero se aparta en cinco puntos.

1. **La carga.** La primera cuota después del corte lleva el interés **desde la fecha de corte**, no el de su período (D5 de la 013).
   - La herramienta anterior todavía no la había causado, así que nadie cobra los días entre el vencimiento anterior y el corte.
   - Ejemplo: saldo 10.000.000 a 1,8 % MV, corte 31/10, vencimiento el 9. La cuota de noviembre lleva 9 días (≈ 54.000) en vez de 30 (180.000).
2. **La tasa.** Sólo se pacta en E.A. con seis decimales de fracción. Si se redondea al traerla, como pasó con 23,87 %, la mensual deja de ser 1,8 %.
   - Aun exacta, la quincena sale de convertir la E.A. y no de dividir la mensual: 0,896 % en vez de 0,9 %.
3. **Qué cuota es «la del período».** El ERP la define por el ciclo de cada cuota, del vencimiento anterior al suyo.
   - El 24/11, para un crédito que vence el 9, la cuota del período es la de diciembre.
   - Para la cooperativa, la del período es la que **vence en el mes**: la de noviembre, ya pagada. Pagar la de diciembre ese día es un anticipo.
   - El proceso de causación de la 024 con rango 1–30 de noviembre toma también la cuota de diciembre.
4. **Cancelar o abonar a mitad de período.** La herramienta anterior deja causado el período completo desde el día 1. El ERP cobra lo corrido o la cuota completa (`Pagos.InteresesAlPagarAntesDelVencimiento`), siempre por el ciclo de la cuota.
5. **El estado de cuenta.** La columna «Cuota» muestra la primera cuota del plan. En un crédito cargado, esa es justo la recortada por el corte.

## Principio

El **cálculo** sigue siendo día a día (la 013 y la 024 no cambian en eso): así quedan exactas la mora, la usura y los abonos. Lo que se vuelve configurable es:

- en qué se pacta la tasa;
- qué cuota pertenece a cada período y cuándo queda causada para cobrar;
- qué se cobra al cancelar o abonar dentro del período;
- cómo nace el interés de la primera cuota de una carga.

**De fábrica nada cambia**: una cooperativa sin parámetros nuevos sigue exactamente como hoy.

## Historias de usuario

### HU1 — Recálculo de la carga desde el corte en créditos ya cargados (P1)

La cooperativa que ya activó su cartera la recausa desde el corte, sin borrar nada, con los mismos pagos reales (D1, 2026-11-24):
- La tasa queda corregida desde el corte (HU3).
- El interés que la carga dejó por fuera en la primera cuota posterior al corte se cobra en la **primera cuota sin vencer, sin mora**.

**Por qué primero**: es plata que hoy no se está cobrando y que se pierde cuota a cuota.

**Prueba independiente**: sobre una cartera cargada y activa, el borrador del ajuste lista por crédito el interés que faltó. Puesto en firme, cada crédito afectado lo debe en su primera cuota sin vencer, sin mora, y la contabilidad lo refleja.

**Escenarios**:
1. **Dado** un crédito cargado con corte 31/10, vencimiento el día 9 y 1,8 % MV sobre 10.000.000, **cuando** se arma el borrador, **entonces** propone ≈ 126.000: 21 días de interés, del 10/10 al 31/10 en 30/360. Son 125.664 si el crédito sigue sellado en E.A. compuesta, o 126.000,00 si ya pasó a MV.
2. **Dado** un crédito que vence el día 30, **cuando** se arma el borrador, **entonces** no propone nada, porque su primera cuota ya tenía el período completo.
3. **Dado** un crédito cuya primera cuota posterior al corte trajo interés en la carga, **cuando** se arma el borrador, **entonces** sólo propone lo que le falte para el período completo. Si no le falta, no propone nada.
4. **Dado** un borrador, **cuando** se pone en firme, **entonces** exige aprobación de otra persona, asienta una sola vez por crédito y no se puede repetir para el mismo crédito.
5. **Dado** un crédito cancelado, castigado o en UVR, **cuando** se arma el borrador, **entonces** queda fuera con su motivo.

### HU2 — Tasa pactada en mensual vencida (P1)

La cooperativa pacta y digita la tasa como la conoce, 1,8 % MV. El ERP la guarda exacta y calcula con ella.

**Prueba independiente**: una línea en 1,8 % MV genera cuotas cuyo interés es exactamente saldo × 1,8 % en mensual y saldo × 0,9 % en quincenal. Los documentos muestran «1,8 % MV = 23,8721 % E.A.».

**Escenarios**:
1. **Dado** una versión de línea, **cuando** se elige «mensual vencida» y se digita 1,8, **entonces** se guarda 0,018 exacto y se muestra su E.A. (23,8721 %).
2. **Dado** un crédito en 1,8 % MV con saldo 10.000.000, **cuando** vence una cuota mensual, **entonces** su interés es 180.000,00; en quincenal, 90.000,00.
3. **Dado** una tasa MV cuya E.A. pasa la usura del día, **cuando** se valida o se causa, **entonces** se rechaza o se topa igual que una E.A.
4. **Dado** extracto, certificado, simulador, estado de cuenta, SICSES o centrales, **cuando** muestran la tasa, **entonces** informan la E.A. (como exige la norma) y, si se pactó en MV, también la mensual.
5. **Dado** una tasa MV, **cuando** se elige otra convención de días que no sea 30/360, **entonces** la versión se rechaza.
6. **Dado** la plantilla de líneas o la de migración, **cuando** trae `tasaCorrienteMV` en lugar de `tasaCorrienteEA`, **entonces** se acepta. Traer las dos es un error.

### HU3 — Corrección de la tasa de los créditos cargados (P1, parte del recálculo de la HU1)

La cooperativa corrige de una vez la tasa de los créditos que entraron con la E.A. redondeada, sin tocar el capital del plan.

**Escenarios**:
1. **Dado** créditos cuya E.A. sellada corresponde a una MV redonda con una diferencia menor a 0,005 puntos, **cuando** se arma el borrador, **entonces** propone pasarlos a esa MV, con el valor actual y el nuevo.
2. **Dado** el borrador en firme, con aprobación de otra persona, **entonces** sella una versión de condiciones nueva por crédito, con la causa «Corrección de la carga» y vigencia **desde el corte**. El recálculo desde ahí usa los mismos pagos y asienta las diferencias como movimientos nuevos.

### HU4 — La carga causa completo el período en curso (P2)

Una cooperativa nueva que carga su cartera no repite el problema de la HU1.

**Escenarios**:
1. **Dado** una carga con la opción «el período en curso al corte se causa completo» (por defecto **sí**), **cuando** se activa, **entonces** la primera cuota posterior al corte lleva el interés de su período completo.
   - Lo anterior al corte se causa el día de la activación, como ingreso de ese día.
   - Lo que la cuota ya traía en la carga cuenta como causado.
2. **Dado** la opción en «no», **entonces** el comportamiento es el de hoy (D5).
3. **Dado** la pantalla de la carga, **cuando** se revisa antes de activar, **entonces** muestra cuánto interés suma la opción, por crédito y en total.

### HU5 — Causación por período calendario (P2)

La cooperativa trabaja como siempre: el día 1 (o el 16) queda causada la cuota del período y la caja la cobra completa.

**Escenarios**:
1. **Dado** `Causacion.Periodo = PeriodoCalendario`, **cuando** empieza noviembre, **entonces**:
   - la cuota del período de cada crédito mensual es la que **vence en noviembre**;
   - con `Causacion.Momento = InicioDelCiclo` queda reconocida el 1/11 y la caja la ofrece como lo exigible del período.
2. **Dado** periodicidad quincenal, **cuando** empieza el 16, **entonces** el período es del 16 al fin de mes y su cuota es la que vence en ese tramo. Igual en decadal (1–10, 11–20, 21–fin) y en semanal.
3. **Dado** el proceso manual de la 024 con rango del 1 al 30 de noviembre, **cuando** está en `PeriodoCalendario`, **entonces** toma las cuotas que **vencen** en el rango, no las cuyo ciclo se cruza con él.
4. **Dado** un pago el 24/11 de un crédito que vence el día 9 y ya pagó la de noviembre, **cuando** se paga en caja, **entonces** la cuota de diciembre entra como anticipo, igual que en la herramienta anterior.
5. **Dado** una cancelación o un abono a mitad del período, **entonces** el interés que se cobra lo decide D3 (ver FR-017).
6. **Dado** `Causacion.Periodo = CicloDeLaCuota` (de fábrica), **entonces** nada cambia frente a hoy.
7. Los filtros de la corrida son los de la herramienta anterior: forma de pago (nómina, caja, todos), qué causa (todo, sin extras, sólo extras) y ciclo de descuento.

### HU6 — «Cuota» del estado de cuenta (P1, independiente y pequeña)

**Escenario**:
1. **Dado** un crédito cargado, **cuando** se abre el estado de cuenta, **entonces** «Cuota» muestra la cuota regular del plan, la más frecuente entre las no extraordinarias ni de gracia. No muestra la primera.

## Casos límite

- **Febrero y meses de 31:** en 30/360 son de 30 días; la quincena del 16 al 28 o al 31 es de 15 días.
- **Primera cuota de un desembolso nuevo:** días 30/360 del desembolso al primer vencimiento, prorrateados linealmente en MV (como la herramienta anterior). Sin cambios en E.A.
- **Última cuota:** interés del período sobre el saldo que queda; el capital se topa al saldo. No hay regla de días especial.
- **Créditos en mora:** el interés corriente sigue sobre el capital y la mora corre sobre lo vencido, como hoy. La HU1 no los excluye, pero si su primera cuota posterior al corte ya está vencida, el ajuste va a la primera cuota sin vencer, sin mora.
- **Vivienda, UVR, castigados y suspendidos:** fuera de la HU1 y de la HU3. La HU2 no aplica a vivienda en UVR.
- **Créditos de pago único (servicios, ventas a crédito):** la HU1 sólo considera créditos con plan periódico.
- **Doble cobro:**
  - El ajuste del corte se sella por crédito y no se repite.
  - La opción de la HU4 y la HU1 no se suman: un crédito nacido con la opción no entra al ajuste.

## Requisitos funcionales

### Ajuste del corte (HU1)
- **FR-001** Una clase nueva del proceso de causación de la 024, «Ajuste del período al corte», con su borrador, firme a cuatro ojos y lote contable. No hace falta otra pantalla ni otra aprobación.
- **FR-002** Considera sólo los créditos nacidos de una carga de migración, con plan periódico, vivos, sin UVR, sin castigo y sin causación suspendida.
- **FR-003** Por crédito calcula:
  - **interés del período completo** de su primera cuota con vencimiento posterior al corte: saldo de capital al corte × tasa del período, con la convención y la equivalencia selladas;
  - **lo que ya tiene**: lo causado desde el corte hasta ese vencimiento, más el interés que traía la cuota en la carga;
  - **el ajuste**: la diferencia, si es positiva y mayor a un peso.
- **FR-004** El ajuste se registra en el libro como interés corriente (transacción de clase nueva `CutoverInterestAdjustment`) en la **primera cuota sin vencer** a la fecha de firma, sin mora ni efecto en la altura de mora ni en la calificación (D1). Emite un hecho contable propio (`InteresDelCorte`) con los roles de la causación de interés corriente: cuenta por cobrar e ingreso.
- **FR-005** Un crédito ajustado queda marcado y no vuelve a entrar.
- **FR-006** El reproceso de la 024 aplica igual: lo puesto en firme no se toca ni se reversa.

### Tasa MV (HU2, HU3)
- **FR-007** Versión de línea y condiciones del crédito ganan `RateBasis` (`EffectiveAnnual` de fábrica, `MonthlyNominal`) y `MonthlyRate`: fracción con ocho decimales, obligatoria si la base es MV.
  - `AnnualRate` se sigue guardando: con MV es la E.A. derivada, (1 + MV)^12 − 1, sin redondear más allá de su precisión.
  - Toda lectura que hoy usa `AnnualRate` para comparar con la usura o para informar sigue funcionando.
- **FR-008** Con base MV, la tasa diaria es MV / 30 y la del período es lineal: los días 30/360 por MV / 30. Exige `DayCount = Thirty360` y `RateEquivalence = Linear` (`Lending.Rate.MonthlyRequiresThirty360`).
  - Mensual = MV, quincenal = MV/2, decadal = MV/3, semanal = 7·MV/30, diario = MV/30.
- **FR-009** El tope de usura compara la E.A. derivada con la usura del día. Si la pasa, se topa como hoy, convirtiendo la usura a mensual.
- **FR-010** Editor de la versión de línea:
  - selector «Tasa en: E.A. / Mensual vencida»;
  - con MV, un campo «Tasa corriente (% MV)» de cuatro decimales y, a su lado, la E.A. calculada (y al revés con E.A.).
- **FR-011** Plantillas de líneas y de migración: columna `tasaCorrienteMV`, excluyente con `tasaCorrienteEA`.
- **FR-012** Documentos e informes: siempre la E.A. y, si la base es MV, también la mensual.
- **FR-013** Corrección de la carga (HU3, D2): parte del mismo recálculo de la HU1.
  - Propone la MV redonda más cercana cuando la diferencia en E.A. es menor a 0,005 puntos.
  - Sella condiciones nuevas con la causa `CutoverCorrection` y **vigencia desde el corte**.
  - Recalcula desde ahí con los mismos pagos: las diferencias se asientan como movimientos nuevos (ajuste de causación y redistribución de pagos, como la corrección por valor nacional).
  - No regenera el capital del plan.
  - **La tasa de mora no cambia de base:** sigue en E.A., topada por la usura.

### Carga (HU4)
- **FR-014** La carga de migración gana la opción «El período en curso al corte se causa completo» (por defecto sí).
  - Con ella, el interés de la primera cuota posterior al corte se calcula desde el vencimiento anterior, o desde el desembolso si es la primera del crédito.
  - Lo anterior al corte se causa el día de la activación, como ingreso de ese día, con el hecho `InteresDelCorte`.
  - Lo que la cuota ya traía en la carga cuenta como causado.
- **FR-015** El cuadre de la activación y la vista previa informan el interés que suma la opción.

### Período calendario (HU5)
- **FR-016** `Causacion.Periodo`: `CicloDeLaCuota` (de fábrica) o `PeriodoCalendario`, general con excepción por línea, como `Causacion.Momento`.
  - Con `PeriodoCalendario`, el período de una cuota es el tramo del calendario (mes, quincena 1–15 / 16–fin, década 1–10 / 11–20 / 21–fin, semana) que contiene su vencimiento.
  - De ahí salen:
    - qué cuota reconoce el ciclo diario el primer día del tramo;
    - qué toma el proceso manual por rango;
    - qué muestra la caja como cuota del período;
    - la cuota del período que usan los descuentos por nómina y pagaduría.
  - El **monto** de la cuota no cambia: es el que da el plan.
- **FR-017** Interés al pagar dentro del período (D3, decidido el 2026-11-24).
  - **Abono** (todo pago que no cancela la deuda): primero cubre **todo lo causado** del período, el reconocido completo, y después lo que diga su modalidad.
  - **Cancelación total con interés ya causado:** lo decide `Pagos.InteresesAlPagarAntesDelVencimiento`, el parámetro que ya existe (general y por línea). No se crea otro para no tener dos que se contradigan. En pantalla se presenta como «¿Devuelve los días no corridos al cancelar?»:
    - **Sí** (`Corrido`, de fábrica): se devuelven los días del período causado que no corrieron.
    - **No** (`CuotaCompleta`): se cobra el período causado completo.
  - Lo causado día a día de un período sin empezar nunca se cobra: se devuelve con el mecanismo de devolución de la 024.
  - Ley 1555 y vivienda: como hoy, «No» sólo aplica fuera del tope legal del prepago.
- **FR-018** Filtros del proceso manual: forma de pago (nómina, caja, todos), qué causa (todo, sin extras, sólo extras) y ciclo de descuento (primera o segunda quincena, década, semana del mes).

### Estado de cuenta (HU6)
- **FR-019** «Cuota» = el total más frecuente entre las cuotas regulares del plan vigente (no extraordinarias ni de gracia). Si hay empate, gana la siguiente por vencer.
- **FR-020** «Valor próxima cuota» no cambia.

## Decisiones del dueño

| # | Pregunta | Recomendación | Decisión |
|---|---|---|---|
| D1 | ¿Dónde se cobra el ajuste del corte (HU1)? | En la **siguiente cuota sin vencer**. Alternativa: que lo asuma la cooperativa. | **2026-11-24: recausar desde el 1/11** («borrando los registros ya realizados de pagos, causaciones, etc.»). Directo en producción, con respaldo previo; **sin borrar**: se recalcula con los mismos pagos reales y las diferencias van como movimientos nuevos; lo que falte de la cuota ya vencida va **sin mora** en la próxima cuota. |
| D2 | ¿Se corrige la tasa de los créditos cargados (HU3)? | Sí, desde la fecha en que se ponga en firme. | **2026-11-24: sí.** Con D1, la tasa corregida rige desde el corte. |
| D3 | ¿Qué se cobra al cancelar o abonar a mitad del período? | `Corrido` de fábrica; `PeriodoCausado` como opción. | **2026-11-24:** un abono, que no cancela, cobra **todo lo causado**. La cancelación total, con interés ya causado, devuelve o no los días según **un parámetro de la empresa en Cartera** (FR-017). |
| D4 | ¿La tasa de mora también en MV? | No: la mora se topa con la usura, que es E.A. | propuesta |
| D5 | Opción de la HU4 por defecto | Sí: es lo que corresponde a casi todas las herramientas de origen. | propuesta |

## Entidades

- **Versión de línea y condiciones del crédito**: `RateBasis` y `MonthlyRate` nuevos; `AnnualRate` sigue siendo la E.A., propia o derivada.
- **Transacción del libro**: clase nueva `CutoverInterestAdjustment` (HU1, HU4) y causa de condiciones `CutoverCorrection` (HU3).
- **Hecho contable**: `InteresDelCorte`, con los roles del interés causado.
- **Proceso de causación (024)**: dos clases nuevas, `CutoverAdjustment` y `CutoverRateCorrection`, y los filtros de FR-018.
- **Carga de migración**: la opción de FR-014.
- **Parámetros de Cartera**: `Causacion.Periodo` y el valor `PeriodoCausado` de `Pagos.InteresesAlPagarAntesDelVencimiento`.

## Criterios de éxito

- **SC-001** Casos dorados de la herramienta anterior —mensual, quincenal, primera cuota, última cuota y carga— dan **igual al centavo**: saldo × MV × fracción, redondeo de la herramienta anterior anotado en cada caso.
- **SC-002** El borrador del ajuste del corte sobre la cartera piloto cuadra con el cálculo independiente (hoja de la contadora) crédito por crédito.
- **SC-003** Con todos los parámetros de fábrica, las suites existentes de Cartera y los casos dorados de la 013 y la 024 siguen pasando sin cambios.
- **SC-004** Una cooperativa nueva cargada con la opción de la HU4 no necesita la HU1: la vista previa de la carga muestra el interés completo.

## Supuestos y límites

- La herramienta anterior no contabilizaba la causación. Aquí lo causado sigue el contrato de contabilización de la 013 y la 024.
- La mora no cambia: corre día a día sobre lo vencido desde el vencimiento.
- No se regenera el capital de ningún plan.
- No se toca la carga de captaciones.
- Fuera de alcance: tasas variables en MV (DTF más puntos). La herramienta anterior las convertía; aquí siguen en E.A.
