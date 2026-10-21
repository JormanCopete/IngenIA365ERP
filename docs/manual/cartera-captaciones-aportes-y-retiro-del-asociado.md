# Aportes sociales y retiro del asociado

> Feature 014, US3 y US10 (entrega K7). Para el jefe de captaciones, el analista de aportes y la contadora. Contrato:
> `specs/014-ahorros-aportes-cdt/contracts/api.md` §7 y §11 y `contrato-de-cuentas.md`, con las enmiendas de `research.md` §10, §11 y §13
> (D36 a D46). Índice y pendientes: [cartera-captaciones.md](cartera-captaciones.md).

Los aportes son **capital social** del asociado: no causan intereses, no se retiran por el contrato y no se embargan (Ley 79 de 1988, art. 49). La
cuota de aportes **no es un crédito** (D12): es un exigible de la línea que los recaudadores pagan por el contrato de cuentas; no tiene mora,
calificación ni deterioro.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Resumen de aportes de un asociado, devoluciones y cesiones por aprobar | `/cartera/aportes` (menú «Aportes sociales») | `Lending.Accounts.Read` |
| Ficha de aportes de la persona (saldo por clase, cuotas, constancia, paz y salvo) | `/cartera/aportes/{persona}` | `Lending.Accounts.Read` |
| Devoluciones sin retiro y cesiones: registrar, aprobar, negar | `/cartera/aportes` | `Lending.Contributions.Manage` |
| Retiro del asociado: lista y ficha | `/cartera/retiros-asociado`, `/cartera/retiros-asociado/{id}` | `Lending.MembershipWithdrawals.File` · `.Approve` · `.Settle` |
| Reversar un pago del cruce con el retiro ya pagado | ficha del crédito | `Lending.MembershipWithdrawals.ReverseAfterPayment` |
| Grupos de archivo y equivalencias de una pagaduría | pestaña «Grupos y equivalencias» de `/cartera/pagadurias/{id}` | los de la 013 |
| Descuentos de cuentas de una corrida de Nómina | `/api/payroll/runs/{runId}/account-deductions` | los de Nómina |

## 2. La cuota y su recaudo

- **Cálculo**: por la versión de la línea de aportes vigente: valor fijo, fracción del ingreso registrado del asociado (sin ingreso registrado la
  cuota no se genera y el ciclo lo informa) o fracción del salario mínimo (valor nacional `SMMLV`). La cuota guarda dos partes: la de **aportes**
  y la de **ahorro permanente** (otra cuota de la cuenta permanente, enlazada); si el asociado no tiene cuenta permanente viva sólo se exige la de
  aportes.
- **Generación**: el paso de captaciones del ciclo diario genera las cuotas al vencer su período.
- **Recaudo**: sólo por el **contrato de cuentas** (`ConsignarAsync`), en la transacción del recaudador: Recaudo, Tesorería, Nómina o la pagaduría.
  Lo libre en aportes es un **extraordinario** (si la línea lo admite); un sobrepago sin extraordinarios se rechaza: no hay saldo a favor. El límite
  de participación sólo se aplica cuando el capital social ya es mayor que cero.
- **Nómina** descuenta con dos conceptos sembrados, `DESC_APORTES` y `DESC_AHORROS`, cuyo valor sale de los exigibles del contrato, y consigna
  al aprobar la corrida con fecha valor el fin del período. La **prelación** de los descuentos de Cartera en Nómina es la política de empresa
  `PrelacionDescuentosDeCartera` (defecto aportes → créditos → ahorros, D37). Lo no descontado queda exigible con su motivo; reversar la nómina
  reversa cada consignación, todo o nada. La liquidación definitiva de Nómina no recauda cuentas (D38).
- **Pagadurías** (D36): toda cuota que se paga por nómina se puede descontar por pagaduría. La cuenta va al archivo de la pagaduría que es la
  **empresa empleadora de la ficha del asociado**; el convenio define **grupos de archivo** (cada uno con sus formatos) y **equivalencias** (qué
  líneas agrupa cada código de la pagaduría y si reparte por prioridad o a prorrata). Lo que la respuesta no explica queda en no aplicados. Receta:
  [cartera-libranza-y-pagadurias.md](cartera-libranza-y-pagadurias.md) §9a.
- **Hecho contable**: `AporteRecibido` (comprobante `AO`; `DN` si consigna Nómina).

## 3. La consulta de aportes para créditos

Los créditos leen los aportes por la consulta de aportes (versión 2, con el mínimo y las cuotas atrasadas): el saldo es la suma del libro a la fecha
y es el mismo en la consulta, el resumen, los informes, la constancia y el paz y salvo. Con aportes activados, la excepción de arrastre (capital ≤
aportes) y el descuento de aportes en el deterioro se leen de `Riesgo.ExcepcionAportesArrastre` y `Riesgo.DescuentoAportesDeterioro` (defecto
falso). La pérdida esperada (Anexo 2, num. 4.2) también descuenta aportes cuando el parámetro está activo y la entidad cumple el num. 5.1 del
Anexo 1 (D39; ver [cartera-perdida-esperada.md](cartera-perdida-esperada.md)).

## 4. Devoluciones sin retiro y cesiones

En `/cartera/aportes`: la **devolución sin retiro** (por exceso sobre el límite de participación u otra causa) se paga por orden de giro; la
**cesión** pasa aportes a otro asociado si la línea la admite. Las aprueba o niega **otra persona** (con motivo). Constancia de aportes y paz y
salvo se emiten desde la ficha; reversar una operación de aportes anula los documentos que la citaban.

## 5. El retiro del asociado: de la radicación al pago

El trámite **nace en Cartera** (D16) y Asociados sólo cambia el estado cuando Cartera le avisa.

| Paso | Quién | Qué pasa |
|---|---|---|
| Radicar | `.File` | causa (voluntario, exclusión, fallecimiento, otra), fecha efectiva no anterior a la radicación; se puede desistir |
| Aprobar o negar | `.Approve`, otra persona | con el **punto de un acta cerrada** cuyo asunto es este trámite (el asunto `AsuntoDeRetiroDeAsociado`: quien radicó no vota). Al aprobar sale `RetiroDeAsociadoAprobado` a Asociados, que deja el estado «R», la fecha y el motivo traducido por `Captaciones.MotivosDeRetiroDeAsociados` |
| Vista previa y liquidar | `.Settle` (atribución con monto) | ver §6; la liquidación queda emitida como documento |
| Aprobar la devolución | `.Approve`, otra persona que quien liquidó | ordena el giro de lo que quedó por devolver |
| Pagado | Tesorería | cuando todas las órdenes están giradas y no queda saldo, las cuentas se cancelan |

Con un retiro aprobado, liquidado o por rehacer la persona no abre cuentas y sus consignaciones se rechazan. El reingreso abre cuentas nuevas.

## 6. La liquidación

En una sola transacción, bajo el cerrojo de la persona, sus créditos y sus cuentas:

1. **Intereses a la fecha**: cada ahorro que gana interés se liquida a la fecha (bruto menos retención) antes de cruzar.
2. **Pérdidas** (sólo si la línea lo admite, D15): la proporción del acta sobre el total de aportes, en su propia operación (`PerdidaAplicadaAlRetiro`).
3. **Cruce** contra los créditos donde la persona es deudora: la prelación es **en mora primero** (más días primero), luego **mayor tasa**, luego el
   número (D14). Los fondos salen según `Captaciones.OrdenDeFondosAlRetiro` (defecto `AportesPrimero`; dentro de aportes: extraordinarios,
   revalorizados, ordinarios, mínimo) y luego de los ahorros vivos que **no** son CDAT. El pago va por el contrato de obligaciones como pago interno y
   el cruce y el pago salen en **un solo comprobante `RA`** (D45). Si el asociado queda debiendo, quien liquida elige qué pasa con el saldo: pasar
   a caja o seguir con su forma de pago.
4. **Devolución**: lo que sobra, por orden de giro, con plazo = fecha efectiva + días de la línea (o `Captaciones.DiasPlazoDevolucionAportes`).
5. **Mínimo irreducible**: si el capital social después de todo quedaría bajo `Aportes.MinimoIrreducibleEntidad`, se **retiene la parte de
   aportes** de la devolución (los ahorros sí se devuelven, D43) y sale la alerta `Cartera.DevolucionRetenidaPorMinimo`.

Los CDAT y los contractuales vivos van como líneas **informativas**: siguen hasta su vencimiento y no impiden el pago.

**Si se reversa un pago del cruce**: se reversan las operaciones de cuenta que lo pagaron y el retiro queda **por rehacer** (se vuelve a liquidar, en
una versión nueva). Si el retiro ya estaba pagado y las cuentas canceladas (D44), la cuenta se **reabre**, el retiro vuelve a liquidado, el saldo
queda a favor del exasociado, sale la alerta `Cartera.ReversoTardioDeCruce` y un correo a gerencia, revisoría fiscal y
`Captaciones.DestinatariosReversoTardio` (después de confirmar la transacción).

## 7. Ejemplo

Asociado con 3.000.000 de aportes (1.000.000 mínimos y 2.000.000 ordinarios), 500.000 de ahorro a la vista y un crédito en mora de 800.000. Con el
defecto `AportesPrimero` el cruce toma 800.000 de los aportes ordinarios y no toca el ahorro; quedan 2.200.000 de aportes y 500.000 de ahorro por
devolver. Si el capital social de la cooperativa quedara bajo el mínimo irreducible, se retienen los 2.200.000 de aportes y se giran los 500.000 del
ahorro. (Ejemplo ilustrativo; la prueba de aplicación lo cubre con sus propias cifras.)

## 8. Hechos contables

| Hecho | Comprobante |
|---|---|
| `AporteRecibido` | `AO` (`DN` por Nómina) |
| `CruceAlRetiroAplicado` (con el `PagoAplicado` del crédito) | `RA` |
| `PerdidaAplicadaAlRetiro` | `RA` |
| `DevolucionOrdenada` (con la orden de giro `OrdenDeGiroEmitida` v2 a Tesorería) | `RA` |
| `DevolucionGirada` | informativo |
| `OperacionDeCuentaReversada` | el del original |

## 9. Pendiente de validar

Mínimo irreducible, plazo de devolución, orden de fondos, motivos, tope de la cuota de fondos de empleados, fracción mínima, límites de
participación (P12) y aportes en el deterioro: [cartera-captaciones.md](cartera-captaciones.md), «Aportes y retiro del asociado».
