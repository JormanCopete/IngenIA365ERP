# Vivienda con la Ley 546 y créditos en UVR

> Feature 013, entrega K6b (fase 23). Para el jefe de cartera y la contadora. Cómo se opera: tema «Vivienda (Ley 546) y créditos en UVR» del
> manual en línea; cómo se registran la UVR y el tope: [runbook de valores nacionales](../operaciones/valores-nacionales.md) §3a. Todo lo marcado
> **pendiente de validar** es una lectura del equipo que el dueño o la contadora confirman o corrigen (quickstart §12; preguntas E8 y E12).

## 1. Qué cambia con K6b

Hasta K6a la modalidad **Vivienda** estaba cerrada en las líneas, las solicitudes y la migración (`Lending.CreditLine.HousingNotAvailable`,
`Lending.Migration.HousingNotAvailable`). Con K6b se abre, con las reglas de la Ley 546 de 1999 como **valores con fuente**, nunca escritas en el
código:

| Regla | De dónde sale | Qué pasa si no se cumple |
|---|---|---|
| Tasa **fija** (la rebaja pactada se registra como una versión nueva) | la versión de la línea | `Lending.Housing.VariableRateNotAllowed` |
| Tasa no mayor que el **tope del día**: el menor entre `TOPE_TASA_VIVIENDA` y la usura de la modalidad, si la hay | valor nacional `TOPE_TASA_VIVIENDA` (EA) | `Lending.Housing.RateCapExceeded`, con el tope, su fecha y su fuente |
| Plazo entre el mínimo y el máximo | `VIVIENDA_PLAZO_MINIMO_MESES` (60) y `VIVIENDA_PLAZO_MAXIMO_MESES` (360) | `Lending.Housing.TermOutOfLimits`, nombrando el límite |
| Monto frente al valor del inmueble | `VIVIENDA_FINANCIABLE` (0,70) o, si la solicitud es VIS, `VIVIENDA_FINANCIABLE_VIS` (0,80) | `Lending.Housing.FinancingExceeded` |
| Primera cuota (llevada a un mes) frente a los ingresos familiares | `VIVIENDA_CUOTA_INGRESO` (0,30) | `Lending.Housing.InstallmentToIncomeExceeded` |
| Hipoteca de **primer grado** para desembolsar | la garantía de la solicitud (`MortgageRank = 1`) | `Lending.Housing.FirstRankMortgageRequired` |
| Interés vencido y sin capitalizar; **sin mora sobre intereses** aunque `Mora.SobreIntereses` esté encendido; prepago sin penalidad | la versión de reglas de vivienda `LND-2026.2` | — (lo hace el motor) |

Los cinco límites vienen sembrados como **valor semilla, pendiente de validar**, con la fuente «Ley 546 de 1999, art. 17; Decreto 145 de 2000
(compilado en el Decreto 2555 de 2010)» por cotejar. Si falta uno vigente, la línea, la solicitud y el desembolso de vivienda responden
`NationalValues.Missing`. El tope y la UVR no se siembran: los registra el maestro (runbook §3a).

**La versión de reglas.** Todo crédito de vivienda sella `LND-2026.2` (la de K1 es `LND-2026.1` y la conservan los créditos que ya la tienen).
La simulación, la solicitud, el desembolso y la migración la eligen solos por la modalidad.

## 2. Líneas en pesos y en UVR

En **Cartera → Líneas → Nueva versión**, con modalidad Vivienda:

- **Unidad del capital**: Pesos (por defecto) o **UVR**, sólo si el catálogo de modalidades vigente la marca «admite UVR» para la combinación.
- Una línea **en UVR no admite desembolso por tramos** (`Lending.Housing.UvrTranchesNotSupported`): limitación de K6b, pendiente del dueño.
- El detalle de la línea muestra el **tope y los cinco límites vigentes con su fuente**, y lo que falte registrar.

## 3. Cómo se calcula un crédito en UVR

- El **plan** se calcula en UVR: el monto se convierte a UVR con la **UVR publicada del día del desembolso** (a la diezmilésima) y las cuotas se
  pactan en UVR. El plan en pesos que se ve en la simulación y en el crédito es su **equivalencia** a esa UVR, no el valor a pagar de cada fecha.
- **Cada día** el saldo se valora en pesos con la UVR de ese día. La diferencia que produce el cambio de la UVR es la **corrección monetaria**, una
  fila `MonetaryCorrection` del libro por día (además de la causación del interés, que se calcula en UVR y se valora en pesos).
- **Un pago** en pesos se convierte a UVR con la UVR de su **fecha valor** y se distribuye en UVR por la prelación de la línea.
- **Sin UVR publicada del día** el crédito en UVR no se causa ese día ni los siguientes; la corrida lo dice como error de grupo
  (`NationalValues.Missing`) y alerta `Cartera.ValorNacionalFaltante`; **los créditos en pesos siguen**. Cuando se registra la UVR que faltaba, la
  corrida siguiente lo pone al día.
- Una **liquidación a una fecha futura** sin UVR publicada usa una UVR **proyectada** desde la última publicada (con el factor diario de sus dos
  últimos días): se muestra con aviso y **nunca** se causa ni se contabiliza.
- Redondeo: UVR a la diezmilésima, pesos al centavo. Un pago que no cancela puede dejar un residuo de 0,0001 UVR (limitación conocida).

**Qué se ve en pantalla** (`/cartera/creditos/{id}`): la unidad en el resumen; en la liquidación y el estado de cuenta, la UVR del día (y si es
proyectada), el capital y lo que cuesta cancelar en UVR y en pesos y los saldos por concepto en UVR; en el plan, la cuota y el saldo en UVR; en el
libro, el valor en UVR y la UVR de cada movimiento.

## 4. Solicitud, garantía y desembolso

1. **Simular**: el simulador de una línea en UVR trae el plan en UVR, la UVR del desembolso y la leyenda de la equivalencia.
2. **Borrador**: marcar si el inmueble es **de interés social (VIS)**; elige el porcentaje financiable.
3. **Hipoteca**: registrarla sobre la solicitud **antes de radicar** (detalle de la solicitud, zona «Garantías y pagaré», clase Hipoteca):
   su valor es el del inmueble, con la matrícula y el **grado** (1 = primer grado). Los ingresos familiares son los del solicitante y sus codeudores.
4. **Radicar**: se controlan plazo, porcentaje financiable y cuota-ingreso con los límites vigentes ese día.
5. **Desembolsar**: además de lo de siempre, una hipoteca de **primer grado** constituida o vigente, y de nuevo las reglas de la Ley 546 al monto
   desembolsado. En UVR, la UVR publicada del día del desembolso.

## 5. Contabilidad: la corrección monetaria (E12)

Rige la **propuesta por defecto de la pregunta E12** mientras la contadora no responda: un hecho propio, `CorreccionMonetariaUvrCausada`
(código `CMU`), que el ciclo diario emite junto con la causación, por lotes y resumido en el comprobante `CA` del día. En la matriz contable
(`/contabilidad/cartera/matriz`) la contadora crea la regla de `CMU` sobre el rol **Ingreso** con la cuenta que elija para la corrección monetaria;
el otro lado es la cuenta del concepto cuyo valor cambió (capital, intereses, seguros…). Con `Contabilidad.FaltaDeRegla = Bloquear` y sin esa
regla, el ciclo detiene su tanda y alerta. Si la UVR baja, el hecho viene con signo negativo y los lados se invierten.

**Pendiente de la contadora** (E12): confirmar el hecho propio y su cuenta, y si el certificado tributario incluye la corrección monetaria pagada
junto a los intereses de vivienda (hoy **no** la incluye).

## 6. Certificado tributario

Los intereses de un crédito de vivienda van en su **sección de vivienda** del certificado. En un crédito en UVR, los intereses pagados se
expresan en pesos a la UVR de la **fecha valor de cada pago**, y el saldo a la UVR del 31 de diciembre.

## 7. Riesgo: E1 y E2 de vivienda, garantía hipotecaria

El Anexo 1 sembrado ya trae las subcategorías **E1/E2 de vivienda por meses en E** y la tabla de la garantía hipotecaria por meses de mora, con
`verified = false` hasta el cotejo del dueño (E8). El cierre mensual las aplica sin pasos adicionales.

## 8. Migración de créditos de vivienda

Con K6b la plantilla `loans` admite modalidad Vivienda; el cuadre de la activación la incluye por concepto, modalidad, sub-segmento, categoría y
cuenta, y el acta ya no la declara fuera. Un crédito migrado en UVR trae los **montos en pesos**, que se convierten a la UVR del corte
(limitación de K6b: cómo se migran los saldos en UVR del sistema anterior lo decide el dueño).

## 9. Lo que queda para el dueño y la contadora

- Valores y fuentes de los cinco límites; la clave del tope (hoy una sola: no distingue VIS/no VIS ni pesos/UVR).
- E12: el hecho propio de la corrección monetaria y su cuenta; la corrección monetaria en el certificado.
- Tramos en UVR; la migración de los créditos en UVR del sistema anterior; los campos de vivienda y UVR del SICSES (T1057).
- El cotejo del Anexo 1 de vivienda (E8).
- Corregir una UVR ya causada no encola todavía una corrida de corrección (sólo la usura lo hace): ver el runbook.
