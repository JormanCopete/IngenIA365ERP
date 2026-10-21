# Captaciones: movimientos, cargos, intereses, retención en la fuente y GMF

> Feature 014, US4 y US5 (entrega K8). Para el jefe de captaciones y la contadora. Contrato: `specs/014-ahorros-aportes-cdt/contracts/contrato-de-cuentas.md`
> y `api.md` §6 y §9, con las enmiendas de `research.md` §12 a §14. Fundamento: `investigacion-financiera.md` §3.3 y §3.6. Índice y pendientes:
> [cartera-captaciones.md](cartera-captaciones.md).

## 1. Pantallas y permisos

Todo vive en la ficha de la cuenta (`/cartera/ahorros/{id}`), con sus pestañas de movimientos, intereses y documentos.

| Qué | Permiso |
|---|---|
| Ver operaciones, libro, saldo a una fecha y extracto (lecturas auditadas) | `Lending.Accounts.Read` |
| Retirar por orden de giro | `Lending.Accounts.Withdraw` (con monto) |
| Trasladar a otra cuenta del titular (con simulación) | `Lending.Accounts.Transfer` (con el monto de `.Withdraw`) |
| Reversar una operación registrada por Cartera | `Lending.Accounts.Reverse` |
| Registrar y reversar cargos; condonar y exonerar | `Lending.Charges.Manage`; `Lending.Charges.Waive` |
| Autorizaciones de débito automático | `Lending.DirectDebits.Manage` |
| Simular y liquidar a mano un período de intereses | `Lending.Interest.Settle` |
| Cancelar la cuenta | `Lending.Accounts.Close` |
| Informes `deposit-balances`, `deposit-transactions`, `interest-and-withholding`, `gmf`, `charges` | `/cartera/informes` |

## 2. Movimientos: quién mueve la cuenta

- **Consignar** sólo entra por el **contrato de cuentas**: Recaudo (caja), Tesorería (transferencias y consignaciones bancarias), Nómina y las
  pagadurías, en su propia transacción y con su clave (repetir la clave devuelve la misma operación, sin duplicar). En la cuenta a la vista lo libre va
  al capital; en aportes, permanente y contractual se paga por cuotas.
- **Retirar**: por el contrato (efectivo o cheque) o desde la pantalla **por orden de giro** a la cuenta bancaria registrada del titular.
- **Trasladar**: una sola operación con las dos cuentas bloqueadas en orden; el destino puede ser de otro titular o de aportes (por cuotas).
- **Firmas**: según el manejo (indistinto o conjunto), los límites de firma y el representante del menor; si falta una, se rechaza con las personas
  que faltan.
- **Topes**: retiro por operación y por día y efectivo del día, de la versión de la línea.
- **Atribución con monto**: por encima del monto del rol, queda **en espera de aprobación** y no mueve saldo (ver
  [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) §6).
- **Disponible** = saldo del titular − lo en canje − bloqueos − embargos − el mínimo de la línea; es el mismo número en la consulta, el retiro, el
  traslado y la cancelación. Lo en **canje** suma al saldo pero no al disponible hasta cumplir `Captaciones.DiasDeCanjePorClaseDeMedio`
  (`{"Check": 3, "*": 0}` días hábiles, sin sábados, domingos ni festivos).
- **Fecha valor** hacia atrás hasta `Captaciones.DiasMaximosFechaValor` (5 días, D11): si cae en días ya causados, la misma operación lleva el
  **ajuste de causación** y sale el hecho `AjusteDeCausacionDeCaptacion` aparte. Si cae antes de la última liquidación, el período liquidado **no
  se reliquida** (pregunta abierta al dueño).

## 3. Reversos

- Desde la pantalla nunca queda un sobregiro (`ReversalWouldOverdraw`).
- Lo de Nómina o de un recaudador que no sea consignación lo reversa **él**; lo de un proceso (cruce del retiro, débito automático, devoluciones,
  cargos, liquidación, cancelación) se reversa **desde su origen**.
- Reversar desde Cartera la consignación de un recaudador le avisa (`ConsignacionReversadaPorCartera`) para que anule su recibo.
- Por el contrato, el reverso de una consignación ya usada sí se aplica y deja **sobregiro técnico** con la alerta `Cartera.SobregiroTecnico`.
- No hay ventana de días para reversar.

## 4. Cargos de cuenta

El catálogo vive en la versión de la línea y está **apagado por defecto** (D35). Cada cargo tiene un disparador (periódico, retiro o consignación
sobre los gratuitos, documento adicional, reposición del medio, inactividad, manual), un valor (fijo o porcentaje con mínimo y máximo), gratuitos por
período (por cuenta, no por titular) y exoneraciones (saldo promedio, empleado, cuenta exenta de GMF, menor, oficina, exoneración individual).

- Se **cobra** si el capital alcanza sin bajar del mínimo; si no, queda **por cobrar** y lo cobra la siguiente consignación en su misma operación.
- Los periódicos se causan el último día de su período por el ciclo diario; los de inactividad, sólo en cuentas inactivas.
- **Condonar** o **exonerar** lo hace otra persona que quien registró el cargo. Un cargo cuya deuda ya cobró una consignación posterior no se
  reversa: se condona.
- Si el cargo está **gravado**, Cartera avisa a Comercial (`CargoDeCaptacionCausado`), que deja lista la factura de venta del servicio (tipo y bodega
  en `Ventas.TipoDeFacturaDeCargosDeCartera` y `Ventas.BodegaDeCargosDeCartera`); el reverso deja la nota crédito (D51).

Ejemplos (casos dorados `cargo-*`): un retiro adicional de 2.000 con 4 gratuitos al mes no se causa en la cuarta operación y se cobra en la quinta;
un cargo del 0,1 % con mínimo 1.000 y máximo 5.000 cobra 1.000 sobre 500.000, 2.000 sobre 2.000.000 y 5.000 sobre 10.000.000.

Hechos: `CargoDeCuentaCausado`, `CargoPendienteCobrado`, `CargoDeCuentaCondonado` (comprobante `MA`).

## 5. Débito automático

Una autorización (cuenta, crédito o todos los del titular, débito parcial sí o no, vigencia, soporte) hace que el ciclo diario, el día en que una
cuota de crédito es exigible, debite la cuenta y aplique el pago por el contrato de obligaciones como pago interno. El reverso del pago arrastra el
del débito. El débito automático **genera GMF como un retiro** (D54, pendiente de validar). Hecho: `RetiroDeCuentaAplicado` (`MA`) con el pago del
crédito.

**Libranza o débito automático, no las dos** (decisión del dueño del 2026-10-21): un crédito que se paga por libranza o nómina propia (vínculo de
pagaduría vigente) **no admite** una autorización de débito automático de ese crédito (`Lending.DirectDebit.LoanOnPayroll`), y un crédito con
autorización vigente de débito automático **no pasa** a libranza o nómina (`Lending.Payroll.LoanHasDirectDebit`): se revoca primero la autorización. La
autorización «todos los créditos del titular» sí se registra, pero el ciclo **salta** los créditos que van por libranza, para no pagar la misma cuota
dos veces.

## 6. Causación y liquidación de intereses

- **Causación diaria siempre** (D6), por el paso de captaciones del ciclo diario, al final de los subpasos del día: lo causado el día d es la
  diferencia entre lo acumulado a d y a d − 1, con seis decimales; la liquidación redondea al peso y la diferencia se ajusta en la misma operación (D9).
- **Base**: el capital (el interés por pagar no capitaliza hasta abonarse; lo en canje sí gana interés). El día de la apertura no causa.
- **Tasa diaria**: equivalente compuesta de la tasa efectiva anual con la base de la convención (real/365 por defecto); la tasa puede cambiar
  cualquier día y cada día causa con la que rige; tasa variable con el último índice publicado a la fecha de su reajuste.
- **Método de saldo**: diario (defecto), promedio (interés simple sobre el promedio de lo corrido) o mínimo del período (un día que baja el mínimo
  puede causar en negativo: es correcto).
- **Suspensión**: con el titular fallecido o la cuenta totalmente embargada, si la línea lo dice; una cuenta inactiva cuya línea no causa en la
  inactividad no causa.
- **Liquidación**: al fin de cada período de la línea (o el día de liquidación), por el proceso o a mano con `Lending.Interest.Settle`: abona el
  interés al capital y practica la retención en la misma operación. Liquidar dos veces el mismo período devuelve la primera. La liquidación y la
  cancelación no se reversan desde la pantalla.
- La puesta al día de varios días avisa `Cartera.CausacionDeCaptacionesAtrasada`.

Ejemplos (casos dorados `interes-*`, investigación §3.3): un mes de 30 días al 3 % EA en base 365 con saldos de 2.000.000, 5.000.000 y
1.500.000 (diez días cada uno) abona **6.884** por saldo diario y lo mismo por saldo promedio (2.833.333); por saldo mínimo
(1.500.000 × (1,03^(30/365) − 1)) abona **3.649**. Con un saldo mínimo para interés de 2.000.000, los días con 1.500.000 causan cero. Una tasa que
baja del 3 % al 2 % EA el 16 de abril causa cada día con la suya.

Hechos: `InteresDeCaptacionCausado` (comprobante `CC`, por lotes resumido por día), `InteresDeCaptacionLiquidado` con `RetencionPracticada`
(`CC`), `AjusteDeCausacionDeCaptacion` (`CC`).

## 7. Retención en la fuente

Se practica en cada abono o pago de intereses, **antes de capitalizar** (FR-063), en este orden:

1. **Perfil tributario** de la persona: no contribuyente o autorretenedor → cero.
2. **Umbral diario** `Retencion.UmbralDiarioUvt` (0,055 UVT × UVT vigente), medido **por cuenta y por día** con lo causado ese día: si ningún día lo
   alcanza → cero; si alguno lo alcanza → la base es **todo** el interés del período. A qué clases aplica: `Retencion.AlcanceDelUmbral`
   (`ExceptTermDeposit`: el CDAT retiene sin umbral; P2).
3. **Componente inflacionario** (`Retencion.ComponenteInflacionarioAplica`, valor nacional `COMPONENTE_INFLACIONARIO`): se descuenta de la base a
   la persona natural con el último porcentaje publicado (el del año anterior; si no está, el de dos años antes).
4. **Tarifa** de la clase: `Retencion.TarifaAhorro` 7 %, `Retencion.TarifaCdat` 4 %; contractual y permanente, si están vacías, la del ahorro.
   Redondeo `Retencion.Redondeo` (al peso).

Los rendimientos, el componente y la retención se acumulan **por titular y cotitulares** según su proporción (el titular absorbe el residuo): de
ahí salen los certificados.

Ejemplos (casos dorados `retencion-*`, UVT de prueba 52.374, umbral 2.880,57):

| Caso | Resultado |
|---|---|
| El mes del 6.884: el mayor interés diario es 404,94 | no alcanza el umbral: sin retención |
| Ahorro de 400.000.000 al 3 % EA un mes: 32.394 diarios, 971.812 en el mes, persona jurídica | 7 % de 971.812 = **68.027** |
| El mismo, persona natural, componente de 2025 (55,43 %) | componente 538.675; base 433.137; retención **30.320** |
| CDAT de 488.088 de interés, persona natural | base 217.541; retención **8.702** |
| El mismo CDAT sin componente | 4 % = **19.524** |
| El mismo CDAT si el umbral alcanzara al CDAT (P2): 2.711,6 diarios | no alcanza: sin retención |
| Fondo de empleados no contribuyente o autorretenedor | sin retención |

## 8. GMF

El motor aplica la **tarifa** `Gmf.Tarifa` (4 por mil; admite cero desde una fecha, y entonces no se calcula desde ahí), el **tope exento**
`Gmf.TopeExencionUvt` (350 UVT por persona y mes) según la **política** `Gmf.PoliticaDeExencion` (una cuenta marcada por persona, un tope por titular
sumando cuentas, o no aplica) y los **hechos generadores** de `Gmf.HechosGeneradores` (gravados por defecto: retiro, traslado a otro titular,
traslado entre ahorro y CDAT, redención de CDAT abonada en cuenta, cancelación, débito automático; exento: traslado entre cuentas de ahorro del mismo
titular). Redondeo `Gmf.Redondeo` (al peso). El GMF va en pareja con la operación que lo genera (`GmfRetenido` acompaña a su principal); un reverso
devuelve el tope al mes de la operación original.

Ejemplos (casos dorados `gmf-*`, UVT de prueba 52.374, tope 18.330.900):

| Caso | GMF |
|---|---|
| Dos retiros de la cuenta marcada en marzo: 12.000.000 (todo exento) y 8.000.000 (6.330.900 exento, 1.669.100 gravado) | **6.676** |
| Redención de un CDAT abonada en cuenta por 10.488.088 (capital más intereses) | **41.952** |
| Traslado de 5.000.000 entre cuentas del mismo titular | 0 |
| Traslado de 5.000.000 a otro titular | **20.000** |

## 9. Cancelar una cuenta

Sólo el ahorro a la vista y el contractual (éste como incumplimiento si no ha vencido) se cancelan por aquí, con la fecha de hoy; aportes y permanente
se cancelan con el retiro del asociado y el CDAT por su propia ruta. La **vista previa** muestra la liquidación a la fecha, la retención, el GMF de
la entrega y, en el contractual, el costo de cancelar el contrato. Bloquean la cancelación: embargo, bloqueo, canje, sobregiro técnico, titular
fallecido y **cargos por cobrar** (pregunta abierta: cobrarlos del saldo). La entrega es una operación aparte: **orden de giro** (la cuenta queda en
cancelación hasta que Tesorería informe el giro; si lo rechaza, vuelve a activa) o **traslado** a otra cuenta (con el GMF del traslado).

## 10. Pendiente de validar

Tarifas, umbral (P2), componente inflacionario y redondeo de la retención; tarifa, tope, política (P3), hechos generadores y redondeo del GMF; el
canje; que lo en canje gane interés; la fecha valor en un período liquidado: [cartera-captaciones.md](cartera-captaciones.md), «Retención en la
fuente» y «GMF».
