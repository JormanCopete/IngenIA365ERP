# Contrato de API — 020

**Convenciones**:
- JSON con `PublicId`. Los enums entran por nombre o número y salen como número.
- Los errores van en el sobre de siempre (`code`, `message`, `data`).
- Las escrituras idempotentes llevan `Idempotency-Key`.
- Todo `/api/transport/**` pasa por el filtro de módulo: sin módulo responde **403
  `Module.NotEnabled`**.
- Columna Permiso: el código sin el prefijo `Transport.`, salvo que diga otra cosa.

## 1. Maestro (`/api/saas`, filtro de maestro en el grupo)

| Método | Ruta | Cuerpo / respuesta |
|---|---|---|
| GET | `/api/saas/tenants/{tenantId}/modules` | `[{ code, name, enabled, changedAt, changedBy, reason }]` |
| PUT | `/api/saas/tenants/{tenantId}/modules/{code}` | `{ enabled, reason }` → 204. Al habilitar `TRN` registra los participantes `TRN`/`CON` y siembra los catálogos (R12, R6) |
| GET | `/api/saas/connectors` | lista con versión vigente y cooperativas habilitadas |
| POST | `/api/saas/connectors` | `{ code, name, description }` → `{ publicId }` |
| PUT | `/api/saas/connectors/{id}` | `{ name, description, isActive }` |
| GET | `/api/saas/connectors/{id}/versions` | versiones |
| POST | `/api/saas/connectors/{id}/versions` | `{ baseUrl, definition, note }` → `{ number }`. Valida la URL, la definición y las variables. Publica la versión y la replica |
| POST | `/api/saas/connectors/{id}/versions/{number}/simulate` | `{ variables: { placa, radioMovil, valor, … } }` → `{ method, url, headers (sin secretos), body }`, sin red |
| GET | `/api/saas/connectors/{id}/tenants` | habilitación por cooperativa |
| PUT | `/api/saas/connectors/{id}/tenants/{tenantId}` | `{ enabled, reason }` |

### Definición de conector (cuerpo de `versions`)

```json
{
  "baseUrl": "https://proveedor.example.com:8443",
  "definition": {
    "auth": {
      "kind": "LoginToken",
      "method": "POST",
      "path": "/v1/login",
      "body": "{\"username\":\"{{cred.username}}\",\"password\":\"{{cred.password}}\",\"permanent\":false}",
      "tokenPath": "$.token",
      "expiresInSeconds": 86400,
      "header": "Authorization",
      "prefix": "Bearer "
    },
    "operation": {
      "method": "POST",
      "path": "/v1/historico_creditos/add",
      "body": "{\"hcmovil\":{{vehiculo.radioMovil:int}},\"hcplaca\":\"{{vehiculo.placa}}\",\"hcvalor\":{{pago.valor:int}},\"hcobservacion\":\"{{servicio.nombre}} {{periodo.texto}}\",\"crrecibo\":\"{{pago.recibo}}\",\"externalID\":\"{{envio.id}}\"}"
    },
    "credentialFields": [
      { "name": "username", "label": "Usuario", "secret": false },
      { "name": "password", "label": "Clave", "secret": true }
    ],
    "responses": [
      { "status": 200, "outcome": "Done" },
      { "status": 400, "contains": "ya tiene vigencia", "outcome": "DoneWithWarning" },
      { "status": 400, "contains": "no existe", "outcome": "ManualReview" },
      { "status": 400, "contains": "No hay créditos", "outcome": "ManualReview" },
      { "status": 400, "outcome": "ManualReview" },
      { "status": 401, "outcome": "ReAuth" },
      { "status": 500, "outcome": "Retry" },
      { "status": 0, "outcome": "Retry" }
    ],
    "maxAttempts": 12,
    "timeoutSeconds": 30
  }
}
```

**Variables disponibles** (catálogo fijo, `VariablesDeConector`):
- `vehiculo.placa`, `vehiculo.numeroInterno`, `vehiculo.radioMovil`
- `propietario.documento`, `propietario.nombre`
- `servicio.codigo`, `servicio.nombre`
- `periodo.inicio`, `periodo.fin`, `periodo.texto`
- `pago.valor`, `pago.recibo`, `pago.fecha`
- `factura.numero`
- `envio.id`
- `cred.<campo>`: sólo en `auth`

**Formatos**: `:int`, `:dec2`, `:date(yyyy-MM-dd)`, texto por defecto (escapado JSON).

**Reglas de la plantilla y el mapa**:
- Una variable desconocida da `Connector.UnknownVariable`.
- Una `{{cred.*}}` fuera de `auth` da `Connector.CredentialOutsideAuth`.
- `status: 0` significa error de red o tiempo agotado.
- Las reglas se evalúan en orden.

## 2. Cooperativa: conectores (`/api/core/connectors`)

| Método | Ruta | Permiso | Notas |
|---|---|---|---|
| GET | `/api/core/connectors` | `Core.Connectors.View` | los habilitados para la cooperativa: `{ publicId, code, name, version, hasCredentials, setBy, setAt, lastVerification }` |
| PUT | `/api/core/connectors/{id}/credentials` | `Core.Connectors.Configure` | `{ fields: { username, password } }` → 204; nunca devuelve el contenido |
| POST | `/api/core/connectors/{id}/test` | `Core.Connectors.Configure` | autentica contra el proveedor → `{ ok, statusCode, message }`, sin token |

## 3. Transporte (`/api/transport`)

### Vehículos

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/vehicles?q=&status=&owner=&page=` | `Vehicles.View` |
| GET | `/vehicles/{id}` | `Vehicles.View`: ficha, equipos, propietario con clase deducida, documentos vigentes, conductores, suscripciones, historia |
| POST | `/vehicles` | `Vehicles.Create` |
| PUT | `/vehicles/{id}` | `Vehicles.Update`: sin estado ni propietario |
| POST | `/vehicles/{id}/status` | `{ status: Active\|Inactive, effectiveDate, reason }`: `Vehicles.ChangeStatus` |
| POST | `/vehicles/{id}/retire` | `{ effectiveDate, reason }`: `Vehicles.Retire` |
| GET | `/vehicles/{id}/owner-debts` | `Vehicles.ChangeOwner`: saldos vencidos del propietario |
| POST | `/vehicles/{id}/owner` | `{ newOwnerPersonPublicId, effectiveDate, reason }`: `Vehicles.ChangeOwner` |
| POST | `/vehicles/with-person` | alta del vehículo con persona nueva en un paso (`Core.People.Create` + `Vehicles.Create`) |
| PUT | `/vehicles/{id}/equipment` | `[{ equipmentTypePublicId, code, status }]`: `Vehicles.Update` |
| GET | `/vehicles/template.xlsx` · POST `/vehicles/import` | `Vehicles.Import`; `transport.vehicles` |
| GET | `/vehicles/changes/template.xlsx` · POST `/vehicles/changes/import` | `Vehicles.Import`; `transport.changes` |

### Documentos

| Método | Ruta | Permiso |
|---|---|---|
| GET/POST/PUT | `/document-types` | `Catalogs.Manage` (GET con `Documents.View`) |
| POST | `/vehicles/{id}/documents` | `Documents.Manage`: reemplaza al vigente del mismo tipo |
| PUT | `/vehicles/{id}/documents/{docId}` | `Documents.Manage`: corrección |
| GET | `/documents/expirations?days=&status=` | `Documents.View`: el tablero |
| GET | `/documents/template.xlsx` · POST `/documents/import` | `Documents.Manage`; `transport.documents` |

### Conductores

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/drivers` | `Drivers.View` |
| POST | `/drivers` | `{ personPublicId }`: `Drivers.Manage` |
| POST | `/drivers/with-person` | `Core.People.Create` + `Drivers.Manage` |
| POST | `/drivers/{id}/retire` | `Drivers.Manage` |
| POST | `/drivers/{id}/assignments` | `{ vehiclePublicId, from, isPrimary }`: `Drivers.Manage` |
| POST | `/drivers/{id}/assignments/{aId}/end` | `{ to }`: `Drivers.Manage` |
| GET | `/drivers/template.xlsx` · POST `/drivers/import` | `Drivers.Manage`; `transport.drivers` |

### Catálogos

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/catalogs/{kind}` | cualquier `Transport.*.View` |
| POST/PUT | `/catalogs/{kind}` · `/catalogs/{kind}/{id}` | `Catalogs.Manage` |

El código se consulta con `GET /api/catalogos/transporte-{kind}/codigo/{codigo}`.

### Servicios, suscripciones y facturación

| Método | Ruta | Permiso |
|---|---|---|
| GET/POST/PUT | `/services` | `ServiceSetup.Manage` (GET con `Services.View`): producto, prioridad, línea de Cartera, medios, conector |
| GET/POST/PUT | `/billing-policies` | `ServiceSetup.Manage` |
| POST | `/vehicles/{id}/subscriptions` | `{ servicePublicId, from, to?, price? }`: `Services.Manage` |
| POST | `/vehicles/{id}/subscriptions/{sId}/end` | `{ to, reason }`: `Services.Manage` |
| POST | `/vehicles/{id}/subscriptions/{sId}/prices` | `{ price, from }`: `Services.Manage` |
| GET | `/subscriptions/template.xlsx` · POST `/subscriptions/import` | `Services.Manage`; `transport.subscriptions` |
| GET | `/billing/preview?period=` | `Billing.View`: qué facturaría y qué omitiría, sin guardar |
| POST | `/billing/runs` | `{ period, paymentMode }` + `Idempotency-Key` → 202 `{ runPublicId }`: `Billing.Run` |
| POST | `/billing/single` | `{ vehiclePublicId, period, paymentMode }` + `Idempotency-Key` → `{ invoicePublicId, invoiceNumber, charges[] }`: `Billing.Run` |
| GET | `/billing/runs` · `/billing/runs/{id}` | `Billing.View`: avance y resumen con omitidos |
| GET | `/charges?vehicle=&period=&status=` | `Billing.View` |

### Envíos

| Método | Ruta | Permiso |
|---|---|---|
| GET | `/notifications?status=&from=&to=&vehicle=&service=` | `Notifications.View`: con conteos por estado |
| GET | `/notifications/{id}` | `Notifications.View`: intentos, respuestas y cuerpo saneado |
| POST | `/notifications/{id}/retry` | `Notifications.Manage` + `Idempotency-Key`. `Review` reprocesa por la bandeja; `Retrying` reintenta ya |
| POST | `/notifications/{id}/resolve` | `{ reason }`: `Notifications.Manage` |

## 4. Informes (`/api/reports/transport/{vista}?format=json|xlsx|pdf`)

`Transport.Vehicles.View` en json; `Transport.Vehicles.Export` en archivo. Cada exportación queda
auditada.

| Vista | Contenido |
|---|---|
| `vehiculos` | listado con propietario, clase deducida, estado y radio-móvil |
| `historia` | eventos de estado y propietario por rango |
| `vencimientos` | vencidos, por vencer y faltantes |
| `conductores` | conductores y asignaciones vigentes |
| `facturacion` | cargos del período: facturado, pagado, abierto |
| `envios` | envíos por estado |

## 5. Lo que cambia en rutas existentes

- `GET /api/admin/permissions/mine` agrega `enabledModules: ["TRN", …]`.
- `GET /api/catalogos/{catalogo}/codigo/{codigo}` acepta los nueve `transporte-*`.
- `DELETE /api/core/people/{id}` responde `Person.HasTransportUse` si la persona es propietaria o
  conductora vigente.
