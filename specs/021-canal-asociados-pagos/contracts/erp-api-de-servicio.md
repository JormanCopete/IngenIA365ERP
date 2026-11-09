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
support{phone, email, hours}, inactivityMinutes, smsEnabled }]`. Recorre el directorio abriendo cada base
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
  }]
}
```
Aportes y ahorros: `kind = Contribution | ContractualSaving | ProgrammedSaving`, con `installment{ period, dueDate,
amount, pending }`. Audita la lectura como `Receipts.Person.PayablesRead` con canal digital.

### `POST /tenants/{t}/people/{p}/payables/simulate`
`{ conceptKey, modality, amount, prepaymentChoice? }` → `{ distribution[], balancesAfter, newPlan{ installment,
remainingInstallments, nextDueDate }, alternatives[] }`. Envuelve `SimularAsync` (R7). No escribe.

### `POST /tenants/{t}/payment-notices` — aviso de pago aprobado (FR-044, FR-045)
Cabecera `Idempotency-Key: {reference}`. Cuerpo:

```json
{
  "reference": "CFP261205A1B2C3D47",
  "personPublicId": "…", "destinationPublicId": "…",
  "total": 332500, "currency": "COP", "approvedAt": "2026-12-05T10:41:22-05:00",
  "gateway": { "requestId": 733112, "internalReference": 1598634446, "authorization": "123",
               "receipt": "1597317690", "franchise": "PS_PSE", "paymentMethodName": "PSE", "issuerName": "…" },
  "items": [{ "conceptKey": "LOAN:…", "modality": "PastDue", "amount": 332500, "prepaymentChoice": null }],
  "payer": { "ip": "186.80.28.28", "userAgent": "…", "accountId": "…" }
}
```
Lo atiende `RegisterChannelPaymentCommand` (`IOperacionIdempotente`, `IEscrituraDeRecaudo`, actor
`Actor.AsociadoPorCanal`): arma una `LineaNoInteractiva` con `Channel = Online`, `Key = GW:{convenio}:{referencia}`,
`MeansClass = Transfer` (PSE) o la de tarjeta, la cuenta propia del convenio y las partes por concepto; la aplica con
`AplicacionNoInteractiva` en una transacción; guarda `CDG_PaymentNotices`. Respuestas:

| HTTP | Cuándo | Cuerpo |
|---|---|---|
| 200 | aplicado (o ya aplicado: misma respuesta) | `{ outcome: "Applied", receiptPublicId, receiptNumber, documentUrl }` |
| 200 | aplicado con excedente a saldo a favor o parte rechazada | `{ outcome: "InReview", receiptPublicId, reason }` |
| 409 | `Channel.Notice.TotalMismatch` (la suma de ítems ≠ total) | no se aplica; el canal alerta |
| 422 | regla del ERP que impide aplicar todo (p. ej. Recaudo inactivo) | el canal reintenta y alerta (FR-047) |

Nunca responde «rechazado» a un pago aprobado: lo que no se aplica queda como saldo a favor (FR-046, R6).

### `POST /tenants/{t}/payment-notices/{reference}/reversal`
Lo llama el canal **después** de que la pasarela confirmó el reverso pedido desde el ERP. Reversa el recibo por el
contrato (`ReversarAsync`, módulo dueño) y marca el aviso. Idempotente.

### `GET /tenants/{t}/people/{p}/receipts/{receiptPublicId}/document`
PDF carta del recibo (`GetReceiptDocumentQuery`); sólo recibos del canal de esa persona.

### `POST /tenants/{t}/people/{p}/documents`
`{ kind: "Statement" | "AccountStatement" | "ContributionCertificate" | "TaxCertificate" | "DepositTaxCertificate",
productPublicId?, year?, cutoffDate? }` → PDF. Envuelve `IssueLendingDocumentCommand` en modo `Download` y sólo para
documentos publicados (FR-055). Audita la descarga.

### `POST /tenants/{t}/people/{p}/contact-updates`
`{ field: "Email" | "Mobile", newValue, reportedAt }` → `{ publicId, created }` y crea `CDG_ContactUpdates` (FR-024).
Idempotente por (`personPublicId`, `field`, `reportedAt` al microsegundo): el reintento devuelve la misma con `created = false`.
Valor sin forma de correo o de celular → 422 `Channel.ContactUpdate.ValueInvalid`.

### C6 — crédito en línea
`GET /tenants/{t}/credit-lines/published`, `POST /tenants/{t}/people/{p}/credit-simulations` (envuelve
`SimuladorDeCredito`), `POST /tenants/{t}/people/{p}/credit-applications`, `GET …/credit-applications`.
