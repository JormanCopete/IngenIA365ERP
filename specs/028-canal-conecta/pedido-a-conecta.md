# Pedido a IngenIA Conecta — lo que el ERP necesita para emitir por Conecta

**Desde**: IngenIA365ERP, feature 028 (`plan.md` §3, decisiones D1, D2 y D7, aprobadas por el dueño el 2026-11-27) |
**Para**: el repositorio `Conecta365` (este repositorio no lo modifica) | **Prioridad**: D1 bloquea activar el canal;
D2 y D7 tienen un puente en el ERP mientras llegan.

## D1 — Numeración del cliente (bloqueante)

**Por qué.** El ERP numera al confirmar la venta, dentro de su transacción y de una resolución vigente. Así:

- el POS imprime con número aunque el canal no conteste;
- la contingencia 03 numera con la resolución de contingencia;
- un reintento nunca duplica;
- un documento **rechazado** se corrige o se reemplaza con **el mismo número**: la DIAN no consume un número rechazado
  (Res. 165/2023; Res. 167/2021 art. 13).

Hoy `IssueInvoiceCommandValidator` (`IssueInvoice.cs:54`) e `IssueNoteCommandValidator` (`IssueNote.cs:63`) exigen
`number == null`.

**Lo que se pide.** Un modo **«numeración del cliente»** por tenant, familia y prefijo (por ejemplo, una bandera en la
numeración registrada: `ClientAssignsNumber`), sin cambiar el comportamiento de los prefijos que no lo tengan.

1. `POST /v1/invoices`, `/v1/credit-notes` y `/v1/debit-notes` aceptan `number` (> 0) **sólo** en un prefijo con
   numeración del cliente; en los demás, la regla de hoy (`number = null`).
2. El número debe caer dentro de una numeración vigente del prefijo y del ambiente del tenant, en `issueDate`. Si no:
   409 `numbering-out-of-range` con `prefix` y `number`. No se consume nada.
3. **Idempotencia igual que hoy**: la misma `Idempotency-Key` devuelve la misma respuesta (200 `replayed = true`).
4. **Unicidad por (tenant, ambiente, familia, prefijo, número)** entre los documentos no rechazados:
   - si ya existe uno **en curso o validado** con ese número y **otra clave**: 409 `number-conflict`, con
     `documentId` y `status` del existente en las extensiones (el ERP lo consulta);
   - si el existente quedó **`Rejected`**: se admite el nuevo documento con ese número y la clave nueva. Es la
     corrección (caso a) o el reemplazo (caso b) del ERP. El rechazado queda en la custodia como versión anterior (o
     enlazado al nuevo).
5. En el canal de proveedor (Factible), el número que se envía al proveedor es el del cliente. La sincronización de
   numeraciones (`provider-numberings/sync`) debe tolerar huecos: el ERP puede dejar números cancelados sin reemplazo
   (caso c), que nunca llegan a Conecta.
6. El contador de la plataforma para ese prefijo deja de usarse (o se adelanta al mayor número recibido, sin
   retroceder), para no chocar si alguien vuelve a la numeración de la plataforma.
7. `GET /v1/tenants/{id}/emission-channel` (o `GET /v1/tenants/{id}`) informa por prefijo si la numeración es del
   cliente. El ERP lo usa en su verificación.
8. La búsqueda `GET /v1/documents?family=&prefix=&number=` debe devolver **todos** los documentos con ese número,
   incluido el rechazado, con su estado. El ERP ya lo usa para consultar sin id.

**Aceptación.** Con un prefijo con numeración del cliente:

- `POST` con `number = 98594` → 202 con `documentNumber = "SETT98594"`;
- el mismo `POST` con la misma clave → 200 `replayed`;
- otra clave con el mismo número mientras el anterior está en curso → 409 `number-conflict`;
- el anterior `Rejected` → 202 con el mismo número;
- un número fuera de rango → 409 sin consumir;
- un prefijo sin la bandera, con `number` → 400 como hoy.

## D2 — Varios medios de pago

`paymentMeans` es un solo objeto (`PaymentMeansRequest(Code, Method, DueDate)`). Una venta de POS puede pagarse en
efectivo y tarjeta, y el UBL admite `cac:PaymentMeans` 1..n (anexo 1.9).

**Se pide**: aceptar `paymentMeans` como **lista** (o un campo nuevo `payments[]` con `method`, `amount`, `reference`)
y conservar el objeto actual por compatibilidad. **Puente del ERP mientras tanto**: envía el medio del pago de mayor
valor y deja todos en `notes`.

## D7 — Totales esperados

Conecta recalcula los totales (`InvoiceTotalsCalculator`). Si difieren de los del ERP, el XML legal diría otra cosa
que la representación impresa del ERP.

**Se pide**: un campo opcional `expectedTotals` (`payable`, `taxInclusive`, `taxExclusive`). Si alguno difiere del
cálculo de Conecta, la respuesta es **422 `totals-mismatch`** con los dos valores, **antes** de consumir el número y de
transmitir. **Puente del ERP mientras tanto**: compara los `totals` del 202 y levanta una alerta crítica.

## Lo que el ERP no pide (para que no se haga en vano)

- Webhooks nuevos: los actuales le bastan.
- Que Conecta guarde datos del ERP: el ERP manda `metadata` (`erpDocumentPublicId`, `erpNumber`,
  `erpIdempotencyKey`, `canonicalSha256`) sólo para trazabilidad.
- Documento soporte, DEE POS y eventos RADIAN por proveedor: se tratarán en una entrega posterior.
