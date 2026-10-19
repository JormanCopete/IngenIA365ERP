# CDAT, tasa negociada y ahorro contractual

> Feature 014, US6 y US7 (entrega K9). Para el jefe de captaciones y la contadora. Contrato: `specs/014-ahorros-aportes-cdt/contracts/api.md` §10, con
> las enmiendas de `research.md` §18 y §19. Fundamento: `investigacion-financiera.md` §3.2 y §3.4. Índice y pendientes:
> [cartera-captaciones.md](cartera-captaciones.md).

El CDAT es un **título de legitimación, no un título valor** (C. de Co. art. 645): plazo, redención, prórroga y su tasa son del reglamento de cada
cooperativa, por eso todo es condición de la línea con vigencia. En la interfaz se llama «CDAT», no «CDT» (D1).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Lista de CDAT, calendario de vencimientos, tasas negociadas por decidir | `/cartera/cdat` | `Lending.Accounts.Read` |
| Simular y constituir | `/cartera/cdat/nuevo` | `Lending.TermDeposits.Constitute` |
| Ficha: plan, títulos, instrucción, renovación, redención, documentos | `/cartera/cdat/{id}` | `Lending.Accounts.Read` · `Lending.TermDeposits.Renew` · `.Redeem` (con monto) |
| Pedir y decidir una tasa negociada | ficha `/cartera/cdat/{id}`; las por decidir, en `/cartera/cdat` | `Lending.NegotiatedRates.Request` · `.Approve` |
| Reglas de tasa negociada de una línea | detalle de la línea de CDAT en `/cartera/lineas` | `Lending.DepositLines.Manage` |
| Contrato del ahorro contractual | pestaña de la ficha `/cartera/ahorros/{id}` | `Lending.Accounts.Read` · `Lending.Accounts.Parties.Manage` (instrucción) |
| Informes `term-deposit-maturities`, `negotiated-rates` | `/cartera/informes` | `Lending.Reports.View` |

## 2. Constituir un CDAT

1. **Simular**: monto, plazo en días, periodicidad de intereses (al vencimiento, mensual, trimestral…) y modalidad (vencido o anticipado), dentro de
   lo que admite la línea.
2. **Constituir**: la cuenta nace **en apertura** con su propuesta; las condiciones (versión 1) y el **plan de intereses** se **sellan al fondear**,
   con inicio el día del fondeo. Se fondea por traslado desde una cuenta de ahorro del titular o por consignación del recaudador por el **monto
   exacto**. Un CDAT constituido no recibe más consignaciones.
3. **Destinos**: los intereses y el vencimiento van a una cuenta a la vista viva del mismo titular o por giro a la cuenta bancaria registrada.
4. Al sellarse nace el **título** (§5).

## 3. El plan de intereses

- El plazo se cuenta en días calendario y se parte en períodos de la periodicidad (mensual 30, trimestral 90…; el último absorbe lo que sobre).
- Interés de un período de n días: `C × ((1 + i)^(n/B) − 1)` con la base de la convención; anticipado: el vencido ÷ (1 + tasa del período).
- Pago vencido al fin del período, anticipado al inicio; en día no hábil (sábado, domingo o festivo) se paga el siguiente hábil **sin interés
  adicional** (D19). La causación diaria cuadra con el plan: el último día absorbe el redondeo.
- **Plazo de emisión** por días con meses de 30: menos de 180; 180 a 360; más de 360 y menos de 540; 540 o más (cuentas CUIF 211005 a 211020,
  pendiente de validar).

Ejemplos (casos dorados `cdat-*`, investigación §3.2), 10.000.000 a 180 días al 10 % EA:

| Caso | Interés |
|---|---|
| Al vencimiento, base 365 | **481.244** |
| Al vencimiento, base 360 | **488.088** |
| Mensual vencido, base 360 (0,79741 % por mes) | 6 × **79.741** = 478.446 |
| Mensual anticipado | **79.111** al inicio de cada período (79.741 / 1,0079741) |
| Vence el 20 de julio de 2027 (festivo) | se paga el 21; tres días hábiles de gracia hasta el 23 |
| 200 días con pago trimestral, base 365 | períodos de 90, 90 y 20 días; plazo de emisión de 6 a 12 meses |

## 4. Vencimiento, gracia, prórroga, renovación y redención

- **Pago de períodos**: el ciclo diario paga cada período neto de retención (a la cuenta, con su GMF, o por giro). Hecho `InteresesDeCdatPagados`.
- **Al vencer**: se aplica la instrucción (cambiable hasta el fin de la gracia) o la sellada: **cancelar y pagar**, o **prorrogar** si la línea lo hace,
  no se agotó el máximo de prórrogas y hay tasa. Si no, el tratamiento de la línea: deja de causar (vencido, a disposición) o pasa a la vista.
- **Gracia** en días hábiles desde el vencimiento; si causa o no lo dice la línea.
- **Prórroga**: misma cuenta, versión nueva de condiciones y **título nuevo**; tasa por la regla de la línea (la misma, la vigente de la tabla; la
  pactada cae a la de la tabla); el margen negociado sólo se conserva si la línea lo dice (D34: por defecto no); si capitaliza, capitaliza el interés
  **neto de retención**. Ejemplo (`cdat-05`): 488.088 − 4 % (19.524) = 468.564; nuevo capital **10.468.564** por el mismo plazo y tasa. Si cambia el
  plazo de emisión, sale `CaptacionReclasificada`.
- **CDAT embargado o de titular fallecido al vencer**: queda vencido, a disposición, sin pago ni prórroga.
- **Renovación** (`.Renew`): sólo entre el vencimiento y el fin de la gracia, con monto, plazo o tasa nuevos; paga antes lo pendiente.
- **Redención anticipada** (`.Redeem`, atribución con monto): sólo si la línea la admite (D18: por defecto no), con penalidad por **reliquidación** a la
  tasa de penalidad (descuenta lo ya pagado; lo pagado de más sale del capital) o por **descuento** de lo causado del período. En la gracia es una
  cancelación sin penalidad. Ejemplo (`cdat-06`): redimido a los 90 días en base 360, lo causado (241.136,89) baja a lo reliquidado al 2 % EA
  (**49.629**). Con giro la cuenta queda en cancelación hasta que Tesorería gire.

Hechos (comprobante `TD`): `CdatConstituido`, `InteresesDeCdatPagados`, `CdatProrrogado`, `CdatRenovado`, `CdatRedimido` con `PenalidadAplicada`,
`CdatCancelado`; con `RetencionPracticada` (4 %, sin umbral por defecto) y, si se abona en cuenta, `GmfRetenido` (redención abonada: gravada sobre
capital más intereses; también los intereses abonados en cuenta, pendiente de validar).

## 5. El título

«CDAT, no negociable», número `TIT-{consecutivo}-{versión}`: lo emite **sola** la operación que sella una versión (constitución, prórroga del proceso
diario, renovación). El título anterior queda **reemplazado** y la redención o la cancelación **liquida** el vigente; el reverso de la operación que lo
selló lo anula. Si falta el firmante o el almacén de archivos, la operación sigue y el título pendiente se emite desde la ficha («Emitir el título
pendiente»). Los CDAT migrados reciben su título por esa misma vía.

## 6. Tasa negociada

Cada línea de CDAT que la admite tiene **reglas** versionadas (antigüedad mínima, rangos de monto y plazo, tasa base, saldo total del titular,
oficina, canal) con un **margen máximo** en puntos sobre la tabla; casa la más específica y, a igual especificidad, la de mayor prioridad. Quien
pide dentro de su atribución la obtiene; por encima, la decide una persona con el nivel (sesión con segundo factor) o un órgano con punto de acta.
Se sellan la tasa de tabla, el margen, la regla, el aprobador y el motivo. El administrador de la empresa no aprueba tasas negociadas (D17).

Ejemplo (`tasa-negociada-01`): asociado de cinco años, 200.000.000 a 360 días, la regla de antigüedad admite 1,5 puntos; pide 1,2 con atribución de
0,5 → espera aprobador; pide 2 → se rechaza. Sin regla que case, no se negocia.

## 7. Ahorro contractual: el contrato

Al abrir la cuenta en una línea contractual se piden **cuota** y **plazo en meses** (y la instrucción al vencimiento). Antes de abrir se revisan la cuota
mínima, el rango de plazo, que quepa al menos una cuota y la tasa. Se sella la versión 1: lo pactado, el valor final proyectado cumpliendo, la tasa
base de la línea, las tasas **bonificada** y **de castigo**, la tolerancia de cuotas atrasadas, el efecto del incumplimiento y cuándo se reconoce la
bonificación. **El contrato no cambia aunque la línea cambie después.**

## 8. Cuotas y movimientos

- Se consigna **sólo por cuotas** (lo libre se rechaza; lo que excede la cuota pendiente, también); el traslado a un contractual paga cuotas.
- **No admite retiros** antes de vencer, salvo su propia entrega.
- Cada cuota se genera el día que vence la anterior; una cuota cuenta **atrasada** pasados `Captaciones.DiasDeGraciaDeCuotaContractual` (5 días)
  de su vencimiento (Nómina y las pagadurías descuentan unos días después).
- Causa y liquida como el ahorro a la vista, con la tasa sellada del día (la base o, desde el incumplimiento, la de castigo).

## 9. Cumplimiento, bonificación e incumplimiento

- **Cumple** si al vencer todas las cuotas están pagadas: se abona la **bonificación** al capital con su retención (`Retencion.TarifaContractual`;
  vacía = la del ahorro) y se entrega según la instrucción (traslado o giro); sin instrucción queda vencido a disposición.
- La bonificación y la penalidad se calculan **sobre los aportes** en su fecha valor real, no sobre el saldo: bonificación = interés a la tasa
  bonificada − interés a la base.
- **Incumple** si antes de vencer las atrasadas superan la tolerancia, si vence con cuotas sin pagar (pasada la gracia de la última) o si se cancela
  antes de vencer. Efecto de la línea: seguir a la tasa de castigo hasta el vencimiento, pasar a una cuenta a la vista del titular o cancelar y
  entregar. La reliquidación al castigo es la **penalidad**, que **sale del capital**; se avisa al titular con `CONTRATINC`.
- **P26**: la línea elige si la bonificación se reconoce al cumplir o se **provisiona** el día de cada cuota (y se reversa al incumplir).

Ejemplos (casos dorados `contractual-*`, investigación §3.4), 12 cuotas mensuales de 200.000 al 6 % EA (30/360):

| Caso | Resultado |
|---|---|
| Cumplido a la tasa base | valor final **2.465.306** (interés 65.306) |
| Con bonificación al 8 % EA | valor final 2.486.777; bonificación **21.471** |
| Paga 10 de 12 con 1 tolerada: el día después del vencimiento hay 2 atrasadas | incumplido; base 64.332, castigo al 3 % 32.331, penalidad **32.001**; sin bonificación |
| 1 tolerada y 5 días de gracia: la cuota 5 atrasada, la 6 vencida hace 3 días todavía no | no supera la tolerancia |
| Quincenal de 50.000 durante 18 meses | 36 cuotas; plazo de emisión de 18 meses o más |

El libro causa día a día y liquida cada mes al peso; en 30/360 lo abonado puede diferir del valor del reglamento (~0,4 % en el ejemplo): es de la
convención, no un error. La bonificación y la penalidad sí son las del motor.

Hechos: `BonificacionReconocida` (comprobante `CC`, con `RetencionPracticada`), `PenalidadAplicada` (con su principal), los de causación y
liquidación de siempre y, al entregar, el traslado o la devolución.

## 10. Pendiente de validar

Plazo de emisión, GMF de los intereses abonados, penalidad del CDAT, interés anticipado, defectos D17 y D18; gracia de las cuotas, bonificación
como gasto con su retención, penalidad contra el capital, P26 y la convención 30/360: [cartera-captaciones.md](cartera-captaciones.md), «CDAT» y
«Ahorro contractual».
