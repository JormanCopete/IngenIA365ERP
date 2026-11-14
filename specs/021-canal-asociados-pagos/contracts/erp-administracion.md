# Contrato — Administración del canal en el ERP (usuarios de la cooperativa)

JWT de usuario, cooperativa por `active_tenant_id`, Carter → MediatR, cada comando con validador (Principios III y
VIII), escrituras idempotentes. Pantallas en `IngenIA365ERP.Shared/Pages/Canal/`, todas con `IndicadorDeCarga`.

## Permisos (sembrados por la API al arrancar, catálogo `ChannelPermissionCatalogSeeder`)

| Permiso | Plantilla sugerida |
|---|---|
| `Channel.Settings.View` | Administrador, Operador (lectura) |
| `Channel.Settings.Manage` | Administrador |
| `Channel.Destinations.Manage` | Administrador (credenciales de pasarela) |
| `Channel.Enablements.View` | Administrador, Operador |
| `Channel.Enablements.Manage` | Administrador, Operador (habilitar, inhabilitar con motivo, carga masiva) |
| `Channel.Support.View` | Administrador, Operador |
| `Channel.Support.Manage` | Administrador (desbloquear, cerrar sesiones) |
| `Channel.Payments.Reverse` | Administrador (con motivo) |
| `Channel.Metrics.View` | Administrador |

## Rutas

| Método y ruta | Comando / consulta | Permiso |
|---|---|---|
| `GET /api/channel/settings` | `GetChannelSettingsQuery` | Settings.View |
| `PUT /api/channel/settings` | `UpdateChannelSettingsCommand` (crea versión) | Settings.Manage |
| `POST /api/channel/settings/enable` · `/disable` | `EnableChannelCommand` (asigna `ChannelCode`, siembra la regla de modalidad `Online`, materializa la política de habilitación) · `DisableChannelCommand` | Settings.Manage |
| `GET/POST /api/channel/legal-texts` | `PublishLegalTextCommand` (versión nueva; nunca edita) | Settings.Manage |
| `GET/POST/PUT /api/channel/destinations` | `CreatePaymentDestinationCommand` (exige convenio `Gateway` activo) | Destinations.Manage |
| `PUT /api/channel/destinations/{id}/credentials` | `SetGatewayCredentialsCommand` → reenvía al canal, **no guarda el secreto** (R10) | Destinations.Manage |
| `GET/POST/PUT /api/channel/concepts` | `PublishConceptCommand` (validaciones de FR-003/FR-004) | Settings.Manage |
| `GET /api/channel/enablements?status=&q=` | `ListChannelEnablementsQuery` | Enablements.View |
| `POST /api/channel/enablements` | `EnablePersonCommand` | Enablements.Manage |
| `POST /api/channel/enablements/{id}/disable` · `/enable` | con motivo (FR-011) | Enablements.Manage |
| `POST /api/channel/enablements/import` · `GET …/template.xlsx` | carga masiva con la mecánica de la 016 (todo o nada, `row`/`column`) | Enablements.Manage |
| `GET /api/channel/contact-updates?pending=` · `GET …/{id}` · `POST …/{id}/acknowledge` | novedades (FR-024): la lista enmascara el valor; el detalle lo muestra completo y queda auditado (`Channel.ContactUpdate.Read`) | Enablements.View o Manage |
| `GET /api/channel/support?document=&reference=` | proxy a `/internal/v1/…/accounts` y `…/attempts` + avisos y recibos del ERP | Support.View |
| `POST /api/channel/support/accounts/{id}/unlock` · `/revoke-sessions` | | Support.Manage |
| `POST /api/channel/payments/{reference}/reverse` | `ReverseChannelPaymentCommand` `{ reason, personBankAccountPublicId? }` → `{ reference, mode (Gateway \| Treasury), reversedAt, reason, receiptPublicId, receiptNumber, voidingPublicId, voidingNumber, refundOrders[{ paymentOrderPublicId, number, amount }], channelUpdated }`. Antes del corte del destino → la pasarela por el canal y el recibo se anula con su espejo; después (o si el canal responde `Canal.Reverso.FueraDeCorte`, o si el pago dejó saldo a favor) → devolución por Tesorería: anulación con saldo a favor y orden de pago por cada saldo (FR-052). Recibo ya anulado en Recaudo → 422 `Channel.Reversal.ReceiptNotValid`; aviso sin recibo y por Tesorería → `Channel.Reversal.NoReceipt`; la pasarela no lo reversa → `Channel.Reversal.GatewayRejected` sin cambios. Idempotente por la referencia | Payments.Reverse |
| `GET /api/channel/metrics` | `GetChannelMetricsQuery` (cifras del ERP + del canal) | Metrics.View |

## Pantallas

| Ruta | Contenido |
|---|---|
| `/canal/configuracion` | encender/apagar, marca, sesión, código al pagar, política de habilitación, SMS, avisos, excedentes; textos legales con historial de versiones |
| `/canal/destinos` | destinos con su convenio de pasarela, medios, corte; credenciales (campo de sólo escritura, estado de la verificación) |
| `/canal/conceptos` | conceptos publicados por tipo y línea, modalidades, mínimo, destino, documentos |
| `/canal/habilitaciones` | buscador con `PersonSearchPicker`, estado, motivo, carga masiva, novedades de contacto |
| `/canal/atencion` | búsqueda por documento o referencia: cuenta, intentos, estado en la pasarela, recibo, reverso |
| `/canal/indicadores` | FR-060 |

Enlaces del menú sólo a páginas con `@page` (`TodoEnlaceDelMenuTieneSuPagina`).

## Auditoría

`ModuloDeAuditoria.Channel` con un emisor `AuditoriaDelCanal`: cambios de configuración (antes/después, sin
secretos), habilitaciones, lecturas sensibles de la API de servicio, avisos aplicados, reversos y descargas.
