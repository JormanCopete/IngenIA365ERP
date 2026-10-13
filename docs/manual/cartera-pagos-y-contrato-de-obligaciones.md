# Cartera: pagos y contrato de obligaciones

> Feature 013 (cartera financiera), fase 9 (US5, entrega K1). Complementa
> [contracts/interfaces.md](../../specs/013-cartera-financiera/contracts/interfaces.md) §3 (el contrato de obligaciones),
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §8.3 (preferencia del deudor), §10 (pagos por pantalla),
> §20 (dobles de prueba) y §24 (auditoría por clave), y
> [quickstart.md](../../specs/013-cartera-financiera/quickstart.md) §3.9. Aquellos son el contrato; esto es la receta para quien
> opera pagos en Cartera (jefe de cartera, analista), para quien construye un recaudador (Recaudo, Nómina, Aportes) y para quien
> audita.

Cartera **no tiene una pantalla para aplicar pagos**. Todo dinero que reduce una obligación entra por el **contrato de
obligaciones** (`IContratoDeObligaciones`), que llama el módulo que recibió la plata —la caja de Recaudo, la nómina
aprobada, la recogida de un desembolso, Aportes— dentro de **su propia transacción**: si el recaudador deshace la suya, el
pago tampoco existe. Por pantalla, Cartera sólo **simula** un pago, **reversa** uno (un cheque devuelto, un error de caja)
y registra la **preferencia del deudor** para sus abonos a capital.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Simular un pago (siete modalidades, no registra nada) | pestaña «Simular pago» de `/cartera/creditos/{id}` | `Lending.Payments.Simulate` |
| Comprobante de aplicación de una operación (distribución, recibo, medio, firmeza) | pestaña «Operaciones» del crédito | `Lending.Loans.Read` |
| Reversar un pago con su vista previa | pestaña «Operaciones», botón «Reversar…» o el comprobante del pago | `Lending.Payments.Reverse` |
| Preferencia del deudor: ver la vigente y su historia | pestaña «Preferencia de abono» | `Lending.Loans.Read` |
| Cambiar o revocar la preferencia | misma pestaña | `Lending.PrepaymentPreferences.Manage` |
| Aplicar con fecha valor dentro de un mes con cierre de Cartera | lo verifica el contrato sobre quien actúa | `Lending.Payments.BackdateBeforeClose` (con límite de monto) |
| Recaudador de prueba (fuera de producción) | pestaña «Recaudador» de `/cartera/dobles-de-prueba` | `Lending.TestDoubles.Operate` |
| Intento y hecho de una clave del contrato | `GET /api/audit/logs?operationKey=` (la consola `/admin/auditoria` todavía no tiene ese filtro) | el de la consola de auditoría |

Como en todo Cartera: fuera del alcance de oficina y línea el crédito no existe (el mismo 404). Simular y la vista previa
del reverso no escriben y van sin clave; reversar y cambiar la preferencia llevan `Idempotency-Key` (dos clics son una sola
operación).

## 2. Las cinco operaciones del contrato

| Operación | Qué hace | Escribe | Respuesta de cada ítem |
|---|---|---|---|
| `ListarAsync` | las obligaciones de una persona a la fecha, como deudor (y como codeudor si se pide), con lo que se puede pagar: exigible hoy, cuota del ciclo, vencido, para cancelar, anticipo, saldo a favor, modalidades admitidas y bloqueos | no | `Answered` |
| `SimularAsync` | la distribución de un pago (o, con la clave de un pago, la redistribución de su reverso) con la **misma** función que aplica | no | `Answered` |
| `AplicarAsync` | un lote de pagos y, sólo para Nómina, sus no descontados | sí | `Applied`, `AlreadyApplied` o `Rejected` |
| `ReversarAsync` | reversa un pago del mismo recaudador por la clave con que lo aplicó | sí | ídem |
| `ResolverSaldoAFavorAsync` | devuelve o aplica un saldo a favor o un anticipo | sí | ídem |

Cada respuesta trae el **resultado**, un **código** (el del hecho o el del error), un **mensaje en español** que el
recaudador puede mostrar tal cual y el valor (el comprobante). Un rechazo de un ítem no rechaza a los demás del lote. El
contrato nunca abre ni confirma una transacción: si no hay una en curso responde `Lending.Contract.TransactionRequired`.

**Idempotencia.** Cada pago lleva una **clave de negocio** del recaudador, única por recaudador: `NOM:{nómina}:{obligación}`
para Nómina, `CAR:{desembolso}:{obligación}` para la recogida, `REC:…` para Recaudo, `TST:{guid}` para el recaudador de
prueba. Repetir la clave con el mismo contenido devuelve el comprobante original (`AlreadyApplied`); con otro contenido,
`Lending.Contract.KeyReused`. Las operaciones derivadas usan `{clave}:reverso` y `{clave}:destino`.

**El recaudador tiene que contar.** Sólo aplica quien tiene una fila vigente en `/admin/participantes-de-integracion` con
rol de recaudador de obligaciones y una versión del contrato soportada (si no, `Lending.Contract.CollectorNotRegistered`).
La recogida de un desembolso es Cartera misma y no necesita fila.

## 3. Las dos formas: interactiva y no interactiva

| | Interactiva (caja, oficina) | No interactiva (Nómina, débito automático, pagadurías, terceros) |
|---|---|---|
| Qué trae cada pago | el `simulationId` de la simulación que vio el deudor | modalidad, valor y fecha valor, sin simulación |
| Si el crédito cambió entre simular y aplicar | `Rejected` con `Lending.Payment.DistributionChanged` y la simulación nueva en `data.simulation`; no escribe | nunca: distribuye con lo vigente |
| Abono extraordinario a capital | la elección del deudor es obligatoria | se aplica la **preferencia** vigente del deudor; sin ella, el valor queda como saldo a favor con el aviso `Lending.Payment.PreferenceMissing` |

## 4. Las siete modalidades y la prelación

Todo pago cubre **primero lo exigible** (las cuotas vencidas hasta la fecha valor, con todos sus conceptos) según la
**prelación sellada** en el crédito: por defecto, gastos de cobranza informados, seguros, interés de mora, interés corriente
y capital, cuota por cuota desde la más antigua (o concepto por concepto, si la línea lo dice).

| Modalidad | Qué cubre | Lo que sobra |
|---|---|---|
| Cancelación total | todo lo cobrable de todas las cuotas; usa primero el anticipo del crédito, nunca el saldo a favor. Sin valor, la simulación dice lo que cuesta | excedente, vuelve al recaudador |
| Lo vencido | sólo lo exigible. Sin valor, la simulación dice cuánto es | excedente |
| Cuota del ciclo | lo exigible y la cuota del período; si todavía no vence, lo que le toca queda como **anticipo de esa cuota** y no rebaja intereses | excedente |
| Anticipo de cuotas | lo exigible y el resto como anticipo general, hasta lo que falta por vencer | excedente |
| Abono extraordinario a capital | lo exigible y el resto a capital con la elección del deudor (plan regenerado); sin elección ni preferencia, **saldo a favor**; si alcanza el capital por vencer, es una cancelación | excedente |
| Cuota específica | sólo esa cuota, si las anteriores exigibles están cubiertas (si no, `Lending.Payment.SpecificInstallmentBlocked` con las cuotas pendientes) | excedente |
| Valor libre | lo exigible por la prelación y el resto como anticipo general | excedente |

Un **anticipo** cubre cada cuota cuando vence y **no rebaja intereses**: la simulación lo avisa
(`Lending.Payment.AdvanceDoesNotReduceInterest`). Quien quiera pagar menos intereses abona a capital. El **excedente** nunca
entra a Cartera: vuelve al recaudador, que lo devuelve por su proceso.

En la pantalla «Simular pago», el abono extraordinario sin elección muestra **los dos planes lado a lado** (reducir el plazo,
reducir la cuota) para que el deudor decida.

## 5. Anticipos y saldos a favor

- **Anticipo**: plata recibida para cuotas que todavía no vencen. Cubre cada cuota en su vencimiento (el proceso diario lo
  aplica). Se puede devolver o aplicar a otra obligación.
- **Saldo a favor**: plata que el crédito no necesita, por ejemplo un abono sin elección ni preferencia, o lo que queda de un
  crédito cancelado o anulado. **Nunca** se usa solo para cancelar: alguien tiene que resolverlo.

`ResolverSaldoAFavorAsync` lo resuelve de tres formas:

| Forma | Qué pasa |
|---|---|
| Devolver por el recaudador | baja el saldo; el recaudador entrega el dinero por su proceso (egreso) |
| Aplicar a otra obligación | otra obligación **del mismo deudor** (si no, `Lending.CreditBalance.TargetNotOwned`) recibe un pago de clase compensación interna con la clave `{clave}:destino` |
| Aplicar como abono a capital | sólo para un saldo a favor que nació de un abono sin preferencia, con la elección del deudor y el plan nuevo |

Más de lo disponible responde `Lending.CreditBalance.Insufficient`. Un saldo a favor en un crédito ya cancelado también se
resuelve aquí.

## 6. La preferencia del deudor (Ley 2032)

La ley le da al deudor el derecho a decidir si un abono a capital **reduce el plazo o reduce la cuota**; la cooperativa
nunca decide por él. La preferencia es su **instrucción permanente y revocable** para los abonos que llegan sin que él esté
presente (forma no interactiva):

- nace sellada en la solicitud o en el cupo, y se cambia o revoca en la pestaña «Preferencia de abono» con un **motivo** (la
  instrucción del deudor) y, si se tiene, la constancia;
- cada cambio es una **versión nueva**; la pantalla muestra la vigente y la historia, nada se borra;
- cada vez que un abono usa la preferencia, el deudor recibe el aviso informativo `ABONOCAP` por correo (cuánto, cómo, la
  cuota y el plazo nuevos, y que puede cambiarla). Un aviso que no sale nunca tumba el pago.

Sin preferencia, el abono queda como saldo a favor hasta que el deudor decida (§5).

## 7. Fecha valor y atribución antes de un cierre

La **fecha valor** es el día en que el pago cuenta para los intereses; la **fecha contable** la pone Cartera según el período
contable abierto, nunca el recaudador.

| Regla | Si falla |
|---|---|
| nunca futura | `Lending.Payment.ValueDateOutOfRange` |
| hasta `Pagos.DiasMaximosFechaValor` días atrás (5 por defecto, parámetro con vigencia en `/cartera/parametros`). **Nómina está exenta**: su fecha la fija la norma | `Lending.Payment.ValueDateOutOfRange` (con la fecha mínima) |
| posterior al corte de la carga inicial: lo de antes ya debía venir en los saldos de apertura | `Lending.Payment.BeforeCutoff` |
| si cae en un mes con **cierre de Cartera** vigente, quien actúa necesita `Lending.Payments.BackdateBeforeClose` y que el valor no supere su límite de monto. Se mira a la persona —sus permisos y su límite—, no su alcance de oficina o línea. Nómina está exenta | `Lending.Payment.ValueDateBeforeClosing` |

Un pago con fecha valor anterior a días ya causados corrige la causación de esos días (`AjusteDeCausacionPorFechaValor`), con
la fecha contable del período abierto: lo cerrado no se reescribe. Mientras no existan cierres de Cartera (llegan con la
entrega de riesgo), la regla del cierre no muerde.

## 8. Clase de medio y firmeza

El recaudador declara la **clase** del medio con que recibió el pago (nunca su medio de pago interno). Cada clase tiene sus
recaudadores:

| Clase | Admitida para |
|---|---|
| Efectivo, tarjetas, consignación, transferencia, bono, cheque, otro | Recaudo y el recaudador de prueba |
| Descuento de pagaduría externa | Recaudo y el recaudador de prueba |
| Descuento de nómina propia | sólo Nómina |
| Compensación interna | la recogida, Aportes, las resoluciones de saldo a favor y el recaudador de prueba |
| Crédito de asociado, crédito de cliente | **nadie**: pagar un crédito con crédito es recogida o novación (`Lending.Payment.MeansClassNotAllowed`) |

La **firmeza** de un pago es su fecha valor más los días hábiles de su clase en `Pagos.FirmezaDiasPorClaseDeMedio` (10 hábiles
por defecto; compensación interna, 0). En una cancelación, hasta esa fecha el paz y salvo dice «sujeta a firmeza» y el pagaré
y las garantías no se liberan. El comprobante la muestra.

## 9. El reverso y quién reversa qué

Un reverso deja el crédito **exactamente como si el pago no hubiera existido**:

- los **pagos posteriores se redistribuyen** con su fecha valor y su elección (la vista previa muestra lo que cada uno cubría
  antes y lo que cubriría después);
- la causación de los días afectados se corrige con la fecha contable del período abierto;
- si el pago había cancelado el crédito, vuelve a quedar vigente; si había regenerado el plan, vuelve el plan que corresponde;
- los documentos cuyo corte es igual o posterior al pago (paz y salvo, certificados) se anulan;
- un reverso no se reversa (`Lending.Reversal.OfReversal`) y un pago tiene a lo sumo un reverso.

| Quién lo aplicó | Quién lo reversa |
|---|---|
| Recaudo, el recaudador de prueba | el recaudador por el contrato (`ReversarAsync` con su clave), o Cartera por pantalla. Si lo reversa Cartera, emite `PagoReversadoPorCartera` al recaudador para que anule su recibo |
| Nómina | **sólo Nómina**, al reversar su nómina o su liquidación. La pantalla no ofrece el reverso: dice «Sólo Nómina lo reversa» |
| Aportes | sólo Aportes |
| La recogida de un desembolso | se reversa al anular el crédito que la recogió |

Por pantalla son **tres pasos**: el motivo, la vista previa (lo que se restituye por concepto, las reasignaciones antes y
después, los documentos que se anularían y si se avisa al recaudador) y confirmar. Al confirmar viaja la **huella** de la
vista previa: si el crédito cambió entretanto (`Lending.Reversal.PreviewChanged`), la pantalla muestra la vista nueva y pide
confirmar otra vez, en vez de reversar algo distinto de lo que se vio.

## 10. Nómina como recaudador y sus no descontados

La nómina propia aplica las cuotas al **aprobar** la nómina o la liquidación definitiva, en forma no interactiva, con la clase
descuento de nómina propia y la clave `NOM:{versión aprobada}:{obligación}`. Recalcular una nómina crea otra versión y por
tanto otra clave; volver a aprobar la misma responde `AlreadyApplied`.

Lo que la nómina **no pudo descontar** (tope de descuentos, neto insuficiente, retiro, licencia, incapacidad, prioridad de
descuentos, otro) viaja en el mismo lote como **no descontado**: Cartera lo guarda con su motivo, la cuota **sigue exigible**,
no mueve el libro ni emite hecho contable, y alerta `Cartera.CuotaNoDescontadaPorNomina` a quien lee créditos en la oficina
del crédito. El estado de cuenta lo muestra en «Cuotas no aplicadas por la pagaduría o la nómina».

## 11. Antes de activar Cartera

Mientras la cooperativa no active Cartera:

| Operación | Responde |
|---|---|
| listar, simular | `Answered` con la lista o el valor vacío y el código `Lending.NotActivated` |
| aplicar, reversar, resolver | `Rejected` con `Lending.NotActivated` en cada ítem; no escribe nada |

Recaudo conserva esos pagos y los aplica al activar con su fecha valor; los de fecha valor hasta el corte ya deben estar en
la carga inicial y responden `Lending.Payment.BeforeCutoff`.

## 12. Auditoría: el intento y el hecho

Cada escritura del contrato deja **dos** eventos:

- el **intento** (`Lending.ObligationContract.Attempted`), al entrar, en un contexto aparte que sobrevive aunque el recaudador
  deshaga su transacción;
- el **hecho** (`.Applied`, `.Reversed`, `.CreditBalanceResolved` o `.Rejected`), dentro de la transacción, con la
  distribución completa por cuota y concepto.

La consulta `GET /api/audit/logs?operationKey={clave}` trae **los dos juntos** (la consola `/admin/auditoria` todavía no
ofrece ese filtro). Un intento sin hecho pasados **5 minutos** (el tiempo máximo de una transacción) se rotula **«Revertido con el
recaudador»**: el recaudador deshizo su transacción y con ella el pago.

## 13. Cómo se prueba

- Pruebas sin contenedores: el motor de pagos (casos dorados de Domain), la aplicación, el reverso y las resoluciones
  (Application), y las de arquitectura que fijan que nadie escribe el libro fuera del registro, que los comandos llevan clave
  y que las escrituras del contrato validan y auditan.
- E2E: el recaudador de prueba ejercita el contrato por HTTP (ver
  [cartera-dobles-de-prueba.md](../operaciones/cartera-dobles-de-prueba.md)).
- A mano: `/cartera/dobles-de-prueba`, pestaña «Recaudador»: listar, simular, aplicar en las dos formas, repetir la clave
  (`AlreadyApplied`), reversar por clave y ver el aviso que Cartera le manda al recaudador.
