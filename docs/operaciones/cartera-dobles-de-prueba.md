# Cartera: los dobles de prueba (Tesorería y Recaudo)

> Feature 013 (cartera financiera), fases 7 y 9 (US3 y US5, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §20 y §21.1 y
> [contracts/interfaces.md](../../specs/013-cartera-financiera/contracts/interfaces.md) §3. Para el uso funcional de los pagos ver
> [cartera-pagos-y-contrato-de-obligaciones.md](../manual/cartera-pagos-y-contrato-de-obligaciones.md).

Cartera habla con dos módulos que todavía no existen: **Tesorería** (gira los desembolsos) y **Recaudo** (la caja que recibe
pagos). Para probar el ciclo completo fuera de producción hay dos **dobles de prueba** que hacen su papel:

| Doble | Código | Hace de | Qué hace |
|---|---|---|---|
| Tesorería de prueba | `TesoreriaDePrueba` (participante `TES`) | Tesorería | recibe las órdenes de giro y sus anulaciones (destino `Treasury`) y emite el `ResultadoDelGiro` (girada, rechazada, cheque no cobrado, anulada) |
| Recaudador de prueba | `RecaudadorDePrueba` (participante `TST`, clase `Test`) | Recaudo | llama al contrato de obligaciones (listar, simular, aplicar, reversar, resolver saldos) y recibe los `PagoReversadoPorCartera` (destino `Receipts`) |

No son canales simulados ni dobles de pruebas unitarias: son módulos de mentira dentro de la API, con permisos, auditoría e
idempotencia reales, que dejan como usuario de origen a la persona que prueba.

## 1. Cómo se habilitan

Hacen falta **las dos cosas**:

1. **Configuración del despliegue**: `Lending:TestDoubles:Enabled = true` en la configuración de la API (está en
   `appsettings.Development.json`; en DEV y QA va en la configuración del ambiente). Con eso y un host que **no** es de
   producción (`IHostEnvironment.IsProduction()` falso), el arranque registra los dos dobles
   (`DoblesDePruebaDeCarteraEnDi.Registrar`) y la API publica las rutas `/api/lending/test-doubles/*`.
2. **Registro en cada cooperativa**: filas de participante en `/admin/participantes-de-integracion` (permiso
   `Core.IntegrationParticipants.Manage`), marcadas como doble de prueba:

   | Código | Clase | Rol | Destino | Contratos adoptados |
   |---|---|---|---|---|
   | `TST` | `Test` | recaudador de obligaciones | — | `ProveedorDeObligaciones` v1 |
   | `TST` | `Test` | destino de mensajes | `Receipts` | ninguno (es el destino de los avisos `PagoReversadoPorCartera`) |
   | `TES` | `Test` | destino de mensajes | `Treasury` | `OrdenDeGiroEmitida` v1, `AnulacionDeOrdenDeGiro` v1 |

   Sin la fila de recaudador, el contrato rechaza los pagos del recaudador de prueba con
   `Lending.Contract.CollectorNotRegistered`; sin las de destino, los mensajes no se le entregan.

Quien opera la pantalla necesita además `Lending.TestDoubles.Operate`, y Cartera tiene que estar **activada** en la
cooperativa (antes, el contrato responde `Lending.NotActivated`; ver §11 del manual de pagos).

## 2. Qué no existe en producción, y por qué el arranque falla si se piden

En producción los dobles **no existen**:

- no se registran, y las rutas `/api/lending/test-doubles/*` no se publican: responden **404** como cualquier ruta
  inexistente. La pantalla `/cartera/dobles-de-prueba` lo dice («Los dobles de prueba no están disponibles en este
  ambiente») y no ofrece nada;
- el registro de integración no admite una fila marcada como doble de prueba (`Integration.Participant.TestDoubleInProduction`),
  y el contrato no cuenta a un doble aunque la encontrara.

Si la configuración **pide** los dobles en un despliegue de producción, la API **no arranca**: `DoblesDePruebaDeCarteraEnDi`
lanza una excepción con el mensaje «La configuración pide los dobles de prueba de Cartera (Lending:TestDoubles:Enabled = true)
en un despliegue de producción…». Es a propósito: un recaudador de mentira en producción podría bajar saldos reales sin que
entre un peso, y una tesorería de mentira podría marcar girado lo que nunca salió. Que falle el arranque se ve en el primer
minuto; que funcione en silencio, no. La prueba `LosDoblesDePruebaNoSeRegistranEnProduccion` lo fija. Para corregirlo, quitar la
clave o ponerla en `false` en la configuración del ambiente de producción.

## 3. Las rutas

Todas bajo `/api/lending/test-doubles`, con `Lending.TestDoubles.Operate`. Las escrituras llevan `Idempotency-Key` de pantalla;
el recaudador lleva además su **clave de negocio** `TST:{guid:N}`, que la pantalla propone y deja repetir para probar la
idempotencia del contrato (`AlreadyApplied`).

| Ruta | Qué hace |
|---|---|
| `GET /` | qué dobles hay: `{ treasury, collector }` (la pantalla y el menú lo preguntan) |
| `GET /treasury/orders?status=` | las órdenes y anulaciones que recibió la tesorería de prueba |
| `POST /treasury/orders/{orden}/results` | emite el resultado del giro: `{ outcome: Disbursed \| Rejected \| CheckNotCashed \| Voided, reference, date?, amount?, reason?, checkNumber? }` |
| `GET /collector/obligations?personPublicId=&includeCoDebtor=` | `ListarAsync` del contrato |
| `POST /collector/simulations` | `SimularAsync`: `{ obligationPublicId, modality, amount, valueDate?, prepaymentChoice?, installmentNumber?, paymentIdempotencyKeyToReverse? }`; con la clave de un pago, la redistribución de su reverso |
| `POST /collector/payments` | `AplicarAsync`: `{ items: [PagoAAplicar], interactive }`; en forma interactiva cada ítem lleva el `simulationId` |
| `POST /collector/payments/{clave}/reverse` | `ReversarAsync` por la clave del pago: `{ reason }` |
| `POST /collector/credit-balances/resolve` | `ResolverSaldoAFavorAsync`: `{ obligationPublicId, kind: RefundThroughCollector \| ApplyToOtherObligation \| ApplyAsExtraordinaryPrincipal, idempotencyKey?, amount?, concept?: CreditBalance \| Advance, targetObligationPublicId?, targetModality?, prepaymentChoice? }` |
| `GET /collector/reversals` | los `PagoReversadoPorCartera` que recibió el recaudador de prueba |

Los enums entran por nombre o por número y salen como número. Si el recaudador no está registrado en el despliegue, sus rutas
responden 404 («El recaudador de prueba no está habilitado en este ambiente»).

## 4. La pantalla

`/cartera/dobles-de-prueba` (menú Cartera, `Lending.TestDoubles.Operate`), rotulada «Doble de prueba: no existe en
producción», con dos pestañas:

- **Tesorería**: las órdenes recibidas y el botón «Resultado…» para emitir girada, rechazada, cheque no cobrado o anulada.
- **Recaudador**: buscar la persona, listar sus obligaciones (como deudor y, si se marca, como codeudor), elegir una, simular,
  aplicar en forma interactiva (con la simulación vista; en un abono extraordinario se elige una de las dos alternativas) o no
  interactiva con la clave `TST:` propuesta —«Clave nueva» la cambia; dejarla igual y volver a aplicar prueba la
  idempotencia—, reversar por clave (con la redistribución previa), resolver saldos a favor y anticipos, y ver los reversos
  que Cartera le avisó.

## 5. Problemas frecuentes

| Síntoma | Causa | Qué hacer |
|---|---|---|
| «Los dobles de prueba no están disponibles en este ambiente» | `Lending:TestDoubles:Enabled` no está en `true` o el host es de producción | revisar la configuración del ambiente |
| El pago responde «el recaudador no está registrado» | falta la fila `TST` de recaudador vigente | registrarla en `/admin/participantes-de-integracion` |
| Todo responde «Cartera no activada» | la cooperativa no activó Cartera | activar (carga inicial aplicada) |
| La API no arranca en producción | la configuración pide los dobles allí | quitar la clave o ponerla en `false` |
| «Reversos que Cartera le avisó» no muestra nada tras reversar por pantalla | falta la fila `TST` de destino `Receipts`, o la bandeja todavía no entregó | registrar la fila; esperar al despachador |
