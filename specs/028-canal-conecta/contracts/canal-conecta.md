# Contrato: canal `CONECTA` / `CONECTA-PRUEBAS`

**Feature**: 028 | **Fecha**: 2026-11-27 | **Puerto**: `specs/012-inventario-comercial/contracts/dian.md` §3 |
**Fuente de Conecta** (repositorio `Conecta365`, leído el 2026-11-27, commit `a8092ee`):
`docs/api/README.md`, `src/Conecta.Application/Invoices/InvoiceRequest.cs`,
`src/Conecta.Application/Notes/NoteRequest.cs`, `src/Conecta.Api/Endpoints/{Document,Note,Tenant}Endpoints.cs`,
`src/Conecta.Domain/Documents/DocumentStatus.cs`, `docs/plan-canales-emision.md`, ADR-017.

Todo campo o código de Conecta citado aquí sale de esos archivos. Todo código DIAN sale del canónico del ERP, que lo
resolvió con `CatalogoDian` (§4.4 de `dian.md`). Lo marcado **por cotejar** se confirma contra el validador de
Conecta (`/preview` en habilitación) o con el contador antes de activar el modo real.

## 1. Identidad del adaptador

| | `CONECTA-PRUEBAS` | `CONECTA` |
|---|---|---|
| `Environment` del ERP admitido | `Testing` | `Production` |
| Ambiente del tenant en Conecta exigido | `Habilitation` | `Production` |
| Proceso del ERP | cualquiera | sólo producción (`CanalesExternosOptions.AmbienteDePruebas == false`) **y** con sección `ElectronicInvoicing:Channels:CONECTA` |
| Credencial | `{tenantPublicId}.CONECTA-PRUEBAS.json` | `{tenantPublicId}.CONECTA.json` |
| `Mode` de la vigencia | `TechnologyProvider` | `TechnologyProvider` |

`CapacidadesDelCanal`:

```
Tipos = { Invoice, CreditNote, DebitNote }
AceptaNumeroDelErp = true            // exige D1 en Conecta (plan §3); la verificación lo comprueba
ContingenciaDelFacturador = false    // D8
InformaContingenciaDian = true       // estado Contingency con CUFE y XML (§7)
EsAsincrono = true                   // 202 + consulta
DevuelvePdf = true                   // existe, el ERP no lo guarda (§13 de dian.md)
PuedeEnviarCorreo = true             // D9
ConsultaRangos = false               // exige tenants:admin
ConsultaAdquirente = false
AdmiteClaveDeIdempotencia = true
```

`EmitirEventoAsync`, `ConsultarRangosAsync` y `ConsultarAdquirenteAsync` responden
`ResultadoDeCanal.Sin(InvalidData, mensaje "CONECTA-NO-OFRECE")` sin llamar a la red. La guardia ya los bloquea antes
por capacidades.

## 2. Credencial (Secret `erp-fe-credenciales`, §11 de `dian.md`)

```json
{ "clientId": "…", "clientSecret": "…", "webhookSecret": "…" }
```

- `CamposDeCredencial` = `["clientId", "clientSecret"]`. `webhookSecret` es opcional y sólo se usa en P3.
- El cliente es un **cliente API de Conecta** (`POST /v1/api-clients`, rol `Operator`, scopes `documents:read`,
  `documents:write`, con el tenant en `tenant_scope`). Lo crea el administrador del tenant en Conecta. No se usa un
  cliente de plataforma con `tenants:admin`: sería la «llave maestra» que descarta §16 de `dian.md`.
- Nunca se registra ni se muestra ningún valor. `ToString` de `CredencialesDeCanal` ya los oculta.

## 3. Extremos (lista blanca de `appsettings`)

```jsonc
"ElectronicInvoicing": {
  "Channels": {
    "CONECTA-PRUEBAS": {
      "ConnectTimeoutSeconds": 5, "TotalTimeoutSeconds": 30,
      "SyncWaitSeconds": 8, "PollIntervalMs": 500,
      "Endpoints": { "QA": "https://conecta-qa.ingenia365.com", "PDN": "https://conecta.ingenia365.com" }
    },
    "CONECTA": {
      "ConnectTimeoutSeconds": 5, "TotalTimeoutSeconds": 30,
      "SyncWaitSeconds": 8, "PollIntervalMs": 500,
      "Endpoints": { "PDN": "https://conecta.ingenia365.com" }
    }
  }
}
```

- La vigencia guarda **la clave** (`QA`, `PDN`) en `ChannelEndpoint`, nunca la URL. Una clave que no está en la
  lista de ese canal da `ChannelUnavailable` («el extremo X no está habilitado en esta instalación»), **sin llamar**.
- Así el `clientSecret` sólo viaja a un host escrito por operaciones (GitOps), nunca a uno leído de la base. Lo
  vigila `ElSecretoDeConectaSoloViajaALaListaBlanca`.
- Sólo se admite `https://`, salvo en `Development` (para el stub y Conecta local, `http://localhost:*`).
- Rutas derivadas del extremo (un solo host por ambiente, `Conecta365/tools/k8s/README.md`):
  - token: `{extremo}/auth/realms/conecta/protocol/openid-connect/token`;
  - API: `{extremo}/v1/…`.

## 4. Traducción del canónico (ERP v2 → Conecta)

### 4.1 Canónico v2 (aditivo)

Lo agrega `ConstructorDelCanonico` **sólo cuando el canal sellado es `CONECTA*`** (`EsquemaDelCanonico`), desde la
copia fiscal vigente (`FirstName`, `LastName`) y DIVIPOLA (`COR_Cities`). Los campos nuevos no se escriben si son nulos,
así lo sellado a otro canal sigue en v1 con los mismos bytes:

| Campo nuevo | De dónde |
|---|---|
| `counterparty.givenNames`, `counterparty.familyNames` | `NombresDePersona.Separar`: el nombre completo de la copia fiscal partido donde empieza el primer apellido; nulos en jurídica |
| `counterparty.address.cityName`, `departmentCode`, `departmentName` | DIVIPOLA por `cityDaneCode`; el código de departamento son los 2 primeros dígitos del municipio |

El emisor no viaja a Conecta: lo toma del perfil del tenant. `tradeName` y `postalCode` no se envían porque la copia
fiscal no los guarda.

### 4.2 Factura — `POST /v1/invoices`

| Conecta (`InvoiceRequest`) | ERP (canónico) | Regla |
|---|---|---|
| `tenantId` | `ChannelTenantId` de la vigencia | |
| `prefix` / `number` | `number.prefix` / `number.consecutive` | **D1**: Conecta lo acepta desde la v0.1.4 (2026-11-28) en los prefijos con `clientAssignsNumber` |
| `issueDate` | fecha local de `issuedAt` | La hora la pone Conecta al firmar |
| `operationType` | `operationTypeCode` | |
| `invoiceType` | `dianDocumentTypeCode` | 01; la 04 la decide Conecta |
| `paymentMeans.code` | `paymentForm`: `Cash` → `"1"`, `Credit` → `"2"` | |
| `paymentMeans.method` | `payments[].dianPaymentMeansCode` del pago de mayor valor | **D2** |
| `paymentMeans.dueDate` | `dueDate` | |
| `customer.idType`, `idNumber`, `dv` | `counterparty.idTypeCode`, `taxId`, `checkDigit` | |
| `customer.legalName` | `counterparty.name` | |
| `customer.organizationType` | `counterparty.personTypeCode` (1 jurídica, 2 natural) | |
| `customer.givenNames` / `familyNames` | v2 | Factible los exige en persona natural (`InvoiceRequest.cs`) |
| `customer.taxLevelCodes` | `counterparty.responsibilities` | |
| `customer.taxScheme` | `counterparty.taxSchemeCode` | tabla 13.2.6.2 |
| `customer.regime` | `taxSchemeCode` `"01"` → `"48"`; si no → `"49"` | **por cotejar** con el contador |
| `customer.email` | `counterparty.receptionEmail` **sólo si** `EmailDeliveryBy = Channel` | **D9** |
| `customer.phone` | `counterparty.phone` | |
| `customer.address.municipalityCode` | `address.cityDaneCode` | |
| `customer.address.city`, `departmentCode`, `department`, `line`, `country` | v2 y `address.line`, `countryCode` | |
| `lines[].code`, `description`, `quantity`, `unitCode`, `unitPrice` | `lines[]` | |
| `lines[].taxes[]` | `{ code = dianTaxCode, rate = Rate × 100, exempt = (Rate == 0) }` | Conecta recibe porcentaje (`InvoiceRequestValidator`: 0, 5, 16, 19). `AmountPerUnit` no nulo → `InvalidData` (**D3**). «Tarifa 0 = exento» **por cotejar** |
| `lines[].allowances[]` | `{ isCharge = false, reason = ReasonCode ?? "Descuento", percent = Percent × 100, amount = Amount }` | La escala del porcentaje **por cotejar** con `/preview` |
| `notes` | `notes`, más «Medios de pago: …» si hay más de uno (D2) | |
| `orderReference` | `references.order` | |
| `metadata` | `{ erpDocumentPublicId, erpNumber, erpIdempotencyKey, canonicalSha256, withholdings? }` | Trazabilidad; las retenciones van aquí (**D3**) |

### 4.3 Notas — `POST /v1/credit-notes` · `POST /v1/debit-notes`

Igual que la factura, más:

| Conecta (`NoteRequest`) | ERP | Regla |
|---|---|---|
| `operationType` | `operationTypeCode` del canónico | NC 20 / ND 30 con referencia; 22 / 32 sin ella (README de Conecta) |
| `discrepancy.code` | `references.corrected.correctionConceptCode` | Tablas 13.2.4 y 13.2.5 |
| `discrepancy.description` | «{nombre del concepto en `CatalogoDian`}: {motivo del documento}» | Conecta exige de 20 a 5000 caracteres. Si no llega a 20 con el motivo, se completa con el número de la factura afectada |
| `referencedInvoice.id` | `references.corrected.number` | **por cotejar** si Conecta espera su id o el número |
| `referencedInvoice.cufe` | `references.corrected.uniqueCode` | Conecta resuelve la factura por CUFE (`IssueNote.ResolveReferencedInvoiceAsync`) |
| `referencedInvoice.issueDate` | `references.corrected.issueDate` | |
| `period` | sólo con 22 / 32 | |

### 4.4 Cabeceras

- `Authorization: Bearer {token}`.
- `X-Tenant-Id: {ChannelTenantId}`.
- `Idempotency-Key`: UUID v5 (espacio fijo del adaptador) de la clave del ERP
  `{tenantPublicId:N}:{Environment}:{Prefix}{Consecutive}:v{n}`. Es determinista, tiene 36 caracteres y cabe en el
  límite de Conecta de 64 caracteres `[A-Za-z0-9._:-]`. La clave del ERP original va en
  `metadata.erpIdempotencyKey`.

## 5. Autenticación

- `POST {extremo}/auth/realms/conecta/protocol/openid-connect/token` con `grant_type=client_credentials`,
  `client_id`, `client_secret` y `scope=documents:read documents:write`.
- `TokensDeConecta` (singleton) guarda el token por (extremo, `clientId`, SHA-256 del `clientSecret`) hasta 60 s
  antes de `expires_in`. Si se rota el secreto, cambia la huella y la entrada vieja se descarta.
- Un 401 de la API invalida el token, pide otro **una vez** y repite la misma petición (la petición no fue aceptada).
  Un segundo 401 da `ChannelUnavailable` con regla `CONECTA-AUTH`.
- Un fallo al pedir el token (400/401 de Keycloak, red) da `ChannelUnavailable` con regla `CONECTA-TOKEN`: la emisión
  no salió.

## 6. Resultado de la emisión (HTTP → `ChannelOutcome`)

| Respuesta de `POST /v1/invoices` (y notas) | Resultado | Notas |
|---|---|---|
| DNS, conexión rehusada, TLS, token imposible | `ChannelUnavailable` | No salió (regla 2) |
| Tiempo agotado o corte **después** de enviar el cuerpo | `InProcess` | Pudo llegar: se consulta (regla 2, §6.3) |
| 202 `Accepted` o 200 `replayed = true` | consulta inmediata (§7) hasta `SyncWaitSeconds`; si no decide, `InProcess` con `ExternalReference = id` | «Ya existe» no es error (regla 3) |
| `documentNumber` del 202 ≠ número del ERP | `InvalidData`, regla `CONECTA-NUMERO`, alerta crítica | Nunca se adopta otro número (D1) |
| `environment` del 202 ≠ el esperado del modo | `InvalidData`, regla `CONECTA-AMBIENTE`, alerta crítica | Mezcla de pruebas y real |
| 400 (falta la clave, cuerpo inválido) | `InvalidData` | Defecto del adaptador; `RejectedBy = Channel` |
| 401 | renovar el token una vez (§5) y después `ChannelUnavailable` | |
| 403 (fuera de `tenant_scope`, rol sin emisión, IP) | `ChannelUnavailable`, regla `CONECTA-PERMISO` | Configuración, no documento |
| 404 (tenant no existe) | `ChannelUnavailable`, regla `CONECTA-TENANT` | |
| 409 «Numeración agotada o no vigente» | `ChannelUnavailable`, regla `CONECTA-NUMERACION`, `data.prefix` | Configuración en Conecta; cuenta para el circuito |
| 409 «El canal de emisión del tenant no está listo» o prefijo sin enlace | `ChannelUnavailable`, regla `CONECTA-CANAL` | |
| 409 «Clave de idempotencia reutilizada para otro tipo de documento» | `InvalidData`, regla `CONECTA-CLAVE` | Defecto |
| 409 `…/number-conflict` (pedido D1; extensiones `documentId`, `prefix`, `number` y un segundo `status`, el del existente) | consulta por número (§7) y **adopta sólo si el total a pagar es el del ERP**; si no, `InvalidData`, regla `CONECTA-NUMERO-OCUPADO`; si todavía no se ve, `InProcess` | Regla 3. El `status` repetido no rompe la lectura: el adaptador lo lee sin tipo |
| 409 `…/numbering-out-of-range` | `ChannelUnavailable`, regla `CONECTA-FUERA-DE-RANGO` | Conecta no consume nada; el documento sigue pendiente |
| 400 `…/client-numbering` (prefijo sin la bandera) | `ChannelUnavailable`, regla `CONECTA-NUMERACION-CLIENTE` | Configuración en Conecta |
| 413 | `InvalidData` | |
| 422 validador local (`ruleIds[]`) | `InvalidData`; un `MensajeDelCanal(Rechazo)` por `ruleId` con `detail` | `RejectedBy = Channel` |
| 422 traducción al proveedor (`provider`, `errors[]`) | `InvalidData`; un mensaje por error | |
| 422 «La factura afectada no coincide» | `InvalidData`, regla `CONECTA-REFERENCIA` | |
| 429 | `ChannelUnavailable`, guardando `Retry-After` en el mensaje | No fue aceptada |
| 5xx | `InProcess` | Pudo haberse aceptado |

`ProviderCode` guarda `"HTTP {status}"` y el `type`/`title` del Problem Details. `DianStatusCode` guarda `dianCode`
si viene.

## 7. Consulta de estado

**Por id** (`ExternalReference`): `GET /v1/documents/{id}`. **Sin id**:
`GET /v1/documents?family={Invoice|CreditNote|DebitNote}&prefix={p}&number={n}&limit=2`. Si no aparece, el resultado
es `NotFound`. Si aparecen dos, es `InvalidData` con `CONECTA-DUPLICADO` y alerta crítica.

| `DocumentStatus` de Conecta | Resultado | Condición |
|---|---|---|
| `Received`, `Validated`, `Built`, `Signed`, `Queued`, `Sent`, `Pending` | `InProcess` | |
| `Accepted`, `AcceptedWithNotices`, `Rendered`, `Delivered`, `Archived` | `Validated` (o `ValidatedWithNotices` si `AcceptedWithNotices` o la respuesta de la DIAN trae notificaciones) | **sólo si** `uniqueCode` no es nulo **y** `GET /dian-response` devuelve 200 (regla 5); si falta alguno, `InProcess` |
| `Rejected` | `Rejected` (`RejectedBy = Dian`), mensajes desde `dianStatusMessage` y las reglas de la respuesta de la DIAN si se pudo descargar | Con `providerStatusCode` y sin respuesta de la DIAN: `InvalidData` (lo rechazó el proveedor) |
| `DianUnavailable`, `Contingency` | `DianUnavailable` **sólo si** hay `uniqueCode` y `GET /xml` responde 200; si no, `InProcess` | Con el proveedor caído, Conecta también usa `Contingency`: eso no es la 04 de la DIAN (§7.1 de `dian.md`: la 04 sólo la declara el canal, con el documento firmado) |
| 404 por id | `NotFound` | |
| 401/403/5xx/red | `ChannelUnavailable` (no se sabe nada; el documento sigue `Sent`) | |

Campos que se trasladan al resultado:

- `UniqueCode` ← `uniqueCode`;
- `UniqueCodeKind` ← `uniqueCodeAlgorithm` (CUFE/CUDE);
- `ValidatedAt` ← `updatedAt` del cambio a `Accepted` (línea de tiempo);
- `DianDocumentTypeCode` ← `subtypeCode`;
- `DianStatusCode` ← `dianStatusCode`;
- `ProviderCode` ← `providerStatusCode ?? status`;
- `ExternalReference` ← `id`;
- `QrContent` ← `qrUrl` si viene; si no, el `sts:QRCode` del XML firmado.

## 8. Artefactos

| `TipoDeArtefacto` | Conecta | Nombre en el ERP |
|---|---|---|
| `XmlFirmado` | `GET /v1/documents/{id}/xml` | `{Number}-v{n}-xml.xml` |
| `ApplicationResponse` | `GET /v1/documents/{id}/dian-response` | `{Number}-v{n}-respuesta-dian.xml` |
| `AttachedDocument` | `GET /v1/documents/{id}/attached` | `{Number}-v{n}-attached.xml` |

- Un 404 de un artefacto mientras el documento está decidido: el resultado de la descarga es `InProcess` y se vuelve
  a pedir.
- El PDF de Conecta (`/pdf`) no se descarga: la representación es la del ERP (§13 de `dian.md`).
- Las descargas usan el cliente de consulta (con resiliencia, regla 7).

## 9. Configuración, verificación y guardia

**`ConfigureEmissionCommand`** gana `ChannelTenantId` y `ChannelEndpoint`, obligatorios con `CONECTA*`. Rechaza
(422):

| Código | Cuándo |
|---|---|
| `ElectronicInvoicing.Settings.ChannelEnvironmentMismatch` | `CONECTA` con `Testing`, o `CONECTA-PRUEBAS` con `Production` |
| `ElectronicInvoicing.Settings.ProductionChannelNotAllowedHere` | `CONECTA` en un proceso que no es de producción, o sin la sección `CONECTA` |
| `ElectronicInvoicing.Settings.EndpointUnknown` | `ChannelEndpoint` fuera de la lista del canal |
| `ElectronicInvoicing.Settings.ProductionConfirmationRequired` | `CONECTA` sin `ConfirmProduction = "PRODUCCION"` o sin el permiso `ElectronicInvoicing.Settings.ActivateProduction` |

**`VerifyChannelCredentialCommand` → `ProbarAsync`**:

1. pide el token;
2. `GET /v1/tenants/{ChannelTenantId}/emission-channel` (admite `documents:read`, `TenantEndpoints.cs:58`) y
   `GET /v1/tenants/{id}`;
3. comprueba:
   - que el tenant esté en el token (si no, 403 → `CONECTA-PERMISO`);
   - que el ambiente del canal vigente sea el del modo;
   - que `missing` esté vacío;
   - que el NIT del tenant sea `IssuerTaxId` de la vigencia;
   - con la compuerta encendida, que cada prefijo enlazado tenga `resolutions[].clientAssignsNumber` (si no, `CONECTA-NUMERACION-CLIENTE`); el resumen
     muestra el contador de cada prefijo («SETT (va en 98597)»).

Devuelve `Validated` con un resumen («Conecta PDN · tenant 266f… · Habilitation · proveedor factible · prefijos
enlazados: SETT») que la pantalla muestra. Si algo falla, devuelve `ChannelUnavailable` con un mensaje por cada falla.
La sonda del circuito usa el mismo `ProbarAsync`.

**`GuardiaDeEmisionFiscal.Decidir`**, motivos nuevos (todos con página y permiso):

- `ElectronicInvoicing.Readiness.ChannelEnvironmentMismatch`;
- `ElectronicInvoicing.Readiness.ProductionChannelNotAllowedHere`;
- `ElectronicInvoicing.Readiness.ChannelParametersMissing` (falta el tenant o el extremo no está en la lista).

D8: abrir una contingencia 03 en un canal sin `ContingenciaDelFacturador` responde
`ElectronicInvoicing.Contingency.NotSupportedByChannel` (plan §3).

## 10. Webhook (P3)

- `POST /api/electronic-invoicing/webhooks/conecta/{tenantPublicId}`: anónimo, fuera del límite general, cuerpo hasta
  64 KB.
- Verifica `X-Conecta-Signature: t=<unix>,v1=<hex>` = HMAC-SHA256 de `"{t}.{cuerpo}"` con `webhookSecret`, con una
  ventana de 5 minutos y comparación en tiempo constante. Si falla, 401 sin detalle.
- `X-Conecta-Delivery` se deduplica 24 h.
- Eventos atendidos: `document.accepted`, `document.accepted_with_notices`, `document.rejected`,
  `document.contingency`, `document.rendered`. Buscan el documento por `document.id` = `ExternalReference` en la base
  de **esa** cooperativa (`IEjecutorEnCooperativa`) y ponen `NextAttemptAt = ahora`.
- Nada más: el procesador consulta y decide. Responde 204 aunque no encuentre el documento.
- El alta del webhook en Conecta (`POST /v1/webhooks`, sólo https y nunca a direcciones privadas) la hace el
  administrador del tenant en Conecta con la URL pública del ERP. El secreto que devuelve una sola vez va al Secret.

## 11. Registros y auditoría

- Se registran la operación, el canal, el número, el id de Conecta, el estado HTTP, el `type` del problema y la
  duración.
- **Nunca** se registran el token, el `clientSecret`, el cuerpo de la emisión ni los artefactos.
- `traceId` del Problem Details va a la transmisión, para cruzar con Conecta.
