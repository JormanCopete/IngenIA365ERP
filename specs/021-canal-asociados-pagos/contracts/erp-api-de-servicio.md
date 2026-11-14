# Contrato — API de servicio del ERP para el canal (`/api/canal/v1`)

Código del ERP (cumple los doce principios). La consume **sólo** el satélite.

## Reglas comunes

- **Autenticación**: esquema `CanalServicio`, JWT ES256 firmado por el canal, 60 s, `iss = ingenia365-canal`,
  `aud = ingenia365-erp`, `sub = {tenantPublicId}` (o `platform` en las rutas sin cooperativa), `jti` de un solo uso
  (R2). Ninguna otra ruta del ERP acepta este esquema y ninguna de estas acepta el JWT de usuarios.
- **Cooperativa**: va en la ruta (`/tenants/{tenantPublicId}/…`) y debe coincidir con `sub`; si no, 403
  `Channel.Service.TenantMismatch`. Cooperativa con el canal apagado: 409 `Channel.Disabled` (salvo avisos de pago de
  intentos ya creados, FR-001).
- **Persona**: toda ruta con `{personPublicId}` verifica en vivo que la habilitación esté `Enabled` (FR-026); si no,
  404 `Channel.Person.NotEnabled` (mismo cuerpo que una persona inexistente, FR-059).
- **Contexto del asociado**: cabeceras `X-Canal-Ip`, `X-Canal-UserAgent`, `X-Canal-Account` (UUID de la cuenta del
  canal) para la auditoría (Principio X).
- **Errores**: el sobre estándar del ERP (`code`, `message` en español, `data`).
- **Versionado**: `v1` en la ruta; un cambio incompatible es `v2` y conviven.

## Plataforma

### `GET /api/canal/v1/tenants`
Cooperativas con el canal encendido: `[{ tenantPublicId, channelCode, displayName, brand{logoUrl, primary, secondary},
support{phone, email, hours}, inactivityMinutes, smsEnabled, paymentSessionMinutes, requireOtpToPay }]`
(`paymentSessionMinutes` 10–30 es el vencimiento de la sesión en la pasarela, FR-008; `requireOtpToPay`, FR-009; desde T090). Recorre el directorio abriendo cada base
(Principio IV).

## Por cooperativa

### `GET /tenants/{t}/enablements?since={sequence}&limit=500`
Cambios de habilitación desde el cursor (R8): `{ items:[{ personPublicId, documentType, documentNumber, status ("Enabled" |
"Disabled", texto), enabledAt, sequence }], next, hasMore }`. `sequence` son ticks UTC; el canal pide con 5 min de solape. El documento sale de `COR_People` (tipo del catálogo fijo y número normalizado).

### `POST /tenants/{t}/enablements/reconcile`
Concilia la política de habilitación (idempotente, R8): habilita a los asociados activos sin habilitación e inhabilita las que puso la
política cuyo asociado se retiró; no toca las manuales ni las masivas. Cuerpo `{}`; respuesta `{ enabled, disabled }`. El canal la pide
antes de arrastrar, a lo sumo una vez cada `CANAL_FRECUENCIA_COOPERATIVAS` por cooperativa.

### `GET /tenants/{t}/legal-texts`
La versión vigente de cada texto legal: `[{ kind ("Terms" | "DataPolicy" | "Faq" | "GatewayFaq"), version, content, publishedAt }]`.
El canal la muestra al activar y registra la aceptación con ese número (FR-020).

### `GET /tenants/{t}/people/{p}/activation-contact`
`{ firstNames, lastNames, email: { masked, available }, mobile: { masked, available }, contactConsent: { email, sms }, delivery: { email, mobile } }`
(`contactConsent` por canal: `true`/`false` o `null` si el titular nunca decidió). Correo con forma de correo y celular colombiano
de 10 dígitos que empieza por 3; si no, `available = false`. Los valores
completos van en un segundo campo `delivery` sólo a esta ruta de servicio, nunca a la PWA (R9). Audita
`Channel.ActivationContactRead`.

### `GET /tenants/{t}/people/{p}/payables?date=`
Respuesta de `GetChannelPayablesQuery` (R5):

```json
{
  "asOf": "2026-12-05",
  "items": [{
    "conceptKey": "LOAN:{obligationPublicId}",
    "kind": "Loan", "lineCode": "CONSUMO", "description": "Crédito CR-000123 · Consumo",
    "productPublicId": "…", "productNumber": "CR-000123",
    "balance": 5400000, "pastDue": 320000, "defaultInterest": 12500, "cycleInstallment": 280000,
    "dueToday": 332500, "payoffAmount": 5450000, "nextDueDate": "2026-12-15", "daysPastDue": 12,
    "destinationPublicId": "…", "payable": true,
    "modes": { "due": true, "partial": { "minimum": 20000 }, "prepayment": { "defaultChoice": "ReduceTerm" }, "payoff": true },
    "canCombine": true, "taxes": []
  }],
  "notices": [],
  "destinations": [{ "destinationPublicId": "…", "sitePublicId": "…", "name": "Avalpay", "offerPse": true, "offerCard": false }]
}
```
`destinations` lista los destinos activos que usan los ítems, con el sitio de pasarela (R10) con que el canal crea la sesión de
pago (T090).
Aportes y ahorros: `kind = Contribution | ContractualSaving | ProgrammedSaving`, `conceptKey = "ACCOUNT:{accountPublicId}"`,
con `installment{ period, dueDate, amount, pending }` (la cuota pendiente más antigua), `balance` = saldo de la cuenta,
`dueToday` = lo pendiente ya vencido y `payoffAmount = null`. Cada ítem lleva además `notices[]` a nivel de respuesta (un
módulo que no respondió) y `blockReasons[]`. `modes` es la intersección de lo publicado con lo que admite el módulo dueño;
`payable` exige destino activo, producto activo, sin bloqueos y alguna modalidad. Sin conceptos publicados no se pregunta
a Cartera ni a Captaciones. Audita la lectura como `Receipts.Person.PayablesRead` (módulo Channel) con `channel = "Digital"`.

**Modalidades del canal** (T078, 2026-11-09): el canal habla `Due`, `Partial`, `Prepayment`, `Payoff` y el ERP las
traduce (`ModosDelCanal`): en créditos `PastDue`, `FreeAmount`, `ExtraordinaryPrincipal`, `FullPayoff`; en cuentas
`Installments` y `Free` de la consignación. Lo mismo vale para los ítems del aviso de pago.

### `POST /tenants/{t}/people/{p}/payables/simulate`
`{ conceptKey, modality, amount, prepaymentChoice? }` → `{ conceptKey, modality, amount, allowed, surplus,
distribution[{ concept, installmentNumber, period, amount }], balancesAfter{ principal, pastDue, payoffAmount, balance },
newPlan{ installment, remainingInstallments, nextDueDate }, prepaymentChoice, alternatives[{ choice, newPlan, surplus }],
notices[] }`. Envuelve `SimularAsync` de obligaciones o de cuentas (R7) a la fecha de hoy; no escribe. Sólo sobre un
concepto que la persona ve en `payables`: si no, 404 `Channel.Payable.NotFound`; modalidad no admitida 422
`Channel.Payable.ModalityNotAllowed`; valor no entero, ≤ 0 o bajo el mínimo parcial 422 `Channel.Payable.AmountInvalid`
(`data.minimum`); rechazo del módulo 422 `Channel.Payable.SimulationRejected` con su mensaje.

### `POST /tenants/{t}/people/{p}/payment-notices` — aviso de pago aprobado (FR-044, FR-045)
La persona va en la ruta (así `ActorDeLaPeticion` da el actor `AsociadoPorCanal`, R3) y la clave de operación la deriva el ERP de la
referencia (`RegisterChannelPaymentCommand.ClaveDeOperacion`): el canal no manda `Idempotency-Key` y reintentar es la misma operación.
Responde aunque la cooperativa haya apagado el canal (el pago ya se cobró). Cuerpo:

```json
{
  "reference": "CFP261205A1B2C3D47",
  "destinationPublicId": "…",
  "total": 332500, "currency": "COP", "approvedAt": "2026-12-05T10:41:22-05:00",
  "gateway": { "requestId": 733112, "internalReference": 1598634446, "authorization": "123",
               "receipt": "1597317690", "franchise": "PS_PSE", "paymentMethodName": "PSE", "issuerName": "…" },
  "items": [{ "conceptKey": "LOAN:…", "modality": "Due", "amount": 332500, "prepaymentChoice": null }],
  "payer": { "ip": "186.80.28.28", "userAgent": "…", "accountId": "…" }
}
```
Lo atiende `RegisterChannelPaymentCommand` (`IOperacionIdempotente`, `IEscrituraDeRecaudo`, actor
`Actor.AsociadoPorCanal`): arma una `LineaNoInteractiva` con `Channel = Online`, `Key = GW:{convenio}:{referencia}`,
`MeansClass = Transfer` (PSE) o la de tarjeta, la cuenta propia del convenio y las partes por concepto; la aplica con
`AplicacionNoInteractiva` en una transacción; guarda `CDG_PaymentNotices`. Respuestas:

| HTTP | Cuándo | Cuerpo |
|---|---|---|
| 200 | aplicado (o ya aplicado: misma respuesta) | `{ outcome: "Applied", receiptPublicId, receiptNumber, reason }` (`reason` informa un anticipo) |
| 200 | aplicado con saldo a favor o parte rechazada, o sin recibo porque ningún producto lo admitió | `{ outcome: "InReview", receiptPublicId?, receiptNumber?, reason }` y alerta `Canal.PagoEnRevision` a Recaudo |
| 409 | `Channel.Notice.TotalMismatch` (la suma de ítems ≠ total) | no se aplica; el canal alerta |
| 422 | regla del ERP que impide aplicar todo (p. ej. Recaudo inactivo) | el canal reintenta y alerta (FR-047) |

Nunca responde «rechazado» a un pago aprobado: lo que no se aplica queda como saldo a favor (FR-046, R6). Antes de aplicar, el ERP
simula cada concepto (`ExcedenteDelAviso`): el que ya no admite el pago se retira (su valor queda a favor) y lo que sobra de un
«exigible» o «parcial» va como anticipo si la política de la cooperativa es `AdvanceElseCreditBalance`. La línea entra con la
llave `GW:{convenio:N}:{referencia}` (R4), fecha valor la de la aprobación en hora de Colombia, medio transferencia (PSE) o tarjeta.
El mensaje contable lo origina el asociado (`OriginActorKind = Member`, sin identidad central). Recaudo inactivo → 422
`Receipts.NotActivated` (el canal reintenta).

### ~~`POST /tenants/{t}/payment-notices/{reference}/reversal`~~ (no se construyó, T116)
El plan la ponía para que el canal avisara el reverso al ERP. **No existe**: la anulación del recibo origina un mensaje
contable, y ese mensaje lo origina la persona que pidió el reverso (T11 de la 012); el actor de servicio del canal no
puede. El ERP termina el reverso él mismo (`ReverseChannelPaymentNoticeCommand`, con la identidad del funcionario) en
cuanto el canal responde; si la llamada se corta después de que la pasarela reversó, repetir el reverso lo termina,
porque el canal responde «ya reversado» y el ERP lo ve en `attempts` (`reversedAt`, `reversalKind = gateway`).

### `GET /tenants/{t}/people/{p}/receipts/{receiptPublicId}/document`
PDF carta del recibo (el mismo modelo de `GetReceiptDocumentQuery`); sólo un recibo que salió de un aviso de esa persona, si no 404
`Channel.Receipt.NotFound`. Audita `Channel.Receipt.Downloaded`.

### `GET /tenants/{t}/people/{p}/documents`
`[{ kind, productPublicId?, productNumber?, description, parameter: "period" | "year" | "none" }]`: lo publicado en los
conceptos del asociado (T124): crédito con extracto → `Statement` por crédito; crédito con certificado tributario →
`TaxCertificate`; aportes con certificado → `ContributionCertificate`; ahorro contractual o programado con extracto →
`AccountStatement` por cuenta; aportes o ahorros con certificado tributario → `DepositTaxCertificate`.

### `POST /tenants/{t}/people/{p}/documents`
`{ kind, productPublicId?, year?, from?, to? }` → PDF (`application/pdf`). Sólo lo que lista el GET (si no, 404
`Channel.Document.NotAvailable`: no publicado o producto de otra persona). Envuelve `IssueLendingDocumentCommand` en modo
`Download` marcado `PorElCanal` (sin usuario del ERP ni alcance de oficina; no viaja en el JSON); sin período, el
extracto cubre los últimos tres meses. Los rechazos de Cartera pasan con su código (`Lending.Document.*`). Audita la
emisión (Cartera) y la descarga (`Channel.Document.Downloaded`).

### `POST /tenants/{t}/people/{p}/contact-updates`
`{ field: "Email" | "Mobile", newValue, reportedAt }` → `{ publicId, created }` y crea `CDG_ContactUpdates` (FR-024).
Idempotente por (`personPublicId`, `field`, `reportedAt` al microsegundo): el reintento devuelve la misma con `created = false`.
Valor sin forma de correo o de celular → 422 `Channel.ContactUpdate.ValueInvalid`.

### C6 — crédito en línea
`GET /tenants/{t}/credit-lines/published`, `POST /tenants/{t}/people/{p}/credit-simulations` (envuelve
`SimuladorDeCredito`), `POST /tenants/{t}/people/{p}/credit-applications`, `GET …/credit-applications`.
