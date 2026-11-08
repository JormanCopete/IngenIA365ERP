# Contrato — API del canal (servicio Go)

Tres superficies en el mismo servicio, separadas por prefijo y por autenticación:

| Prefijo | Quién llama | Autenticación | Publicado a Internet |
|---|---|---|---|
| `/api/v1/…` | la PWA y la app | sesión del canal (cookie o token de la app) | sí |
| `/webhooks/…` | la pasarela | firma de la notificación | sí |
| `/internal/v1/…` | el ERP | JWT ES256 del ERP (`iss = ingenia365-erp`) | **no** (NetworkPolicy) |

Errores: `{ "code": "Canal.Pago.Pendiente", "message": "…", "data": {…} }`, mensajes en español (enmienda, regla 6).

## A. API pública (`/api/v1`)

### Identidad

| Método y ruta | Cuerpo / respuesta | Reglas |
|---|---|---|
| `POST /activation/start` | `{ documentType, documentNumber }` → `{ challengeId, channels:[{ kind, masked }] }` **o** el mismo cuerpo vacío de canales | Respuesta y tiempo idénticos exista o no (FR-018). Sin datos de contacto en la validadora → `{ needsEntityUpdate: true, support{…} }` sólo si el documento está habilitado y tras pasar el límite de intentos (FR-019). |
| `POST /activation/send` | `{ challengeId, channel }` | envía el código (FR-016). |
| `POST /activation/verify` | `{ challengeId, code }` → `{ activationToken }` | 3 intentos, 5 min. |
| `POST /activation/complete` | `{ activationToken, password, acceptances:[{kind, version}] }` | crea la cuenta `active`, registra aceptaciones (FR-020), abre sesión. |
| `POST /auth/login` | `{ documentType, documentNumber, password, deviceCredential? }` → `{ session }` o `{ challengeId }` si el dispositivo es nuevo | FR-021, FR-022. |
| `POST /auth/login/verify` | `{ challengeId, code, trustDevice }` → `{ session, deviceCredential? }` | |
| `POST /auth/passkey/options` · `/auth/passkey/verify` | WebAuthn (PWA) | RP ID = host del canal. |
| `POST /auth/device-unlock` | `{ deviceCredential }` (app, tras biometría) | R13. |
| `POST /auth/recovery/start` · `/verify` · `/complete` | igual que activación, a los datos de la cuenta | FR-023. |
| `POST /auth/logout` | | |
| `GET /me` | `{ account{ names, emailMasked, mobileMasked }, tenants:[{ tenantPublicId, displayName, brand }] }` | sólo cooperativas con habilitación `enabled` (FR-025). |
| `POST /me/contact` · `/me/contact/verify` | `{ field, value }` / `{ challengeId, code }` | código al nuevo, aviso al anterior, novedad a cada cooperativa (FR-024). |
| `POST /me/password` | | |
| `GET /me/devices` · `DELETE /me/devices/{id}` · `DELETE /me/sessions/{id}` | | FR-027 (revocar = estado, no borrado). |
| `PUT /me/notifications` | preferencias | los de seguridad no se apagan (FR-056). |
| `POST /session/tenant` | `{ tenantPublicId }` | acota la sesión (FR-025, FR-028). |

### Dentro de la cooperativa elegida

| Método y ruta | Descripción |
|---|---|
| `GET /payables` | conceptos (proxy de `…/payables` del ERP, sin caché persistente; FR-029–FR-031). |
| `POST /payables/simulate` | simulación de abono (R7). |
| `GET /legal/{kind}` | términos, política, preguntas frecuentes (FR-057). |
| `POST /payments` | crea el intento. Cabecera `Idempotency-Key` del cliente (doble clic, FR-036). Cuerpo `{ items:[{ conceptKey, modality, amount, prepaymentChoice? }], acceptances:[…], otpChallengeId? }`. Valida: mismo destino (FR-032), modalidades y mínimos, sin pendiente sobre el concepto (FR-041 → 409 `Canal.Pago.Pendiente` con referencia, valor, autorización y contacto), total = suma. Respuesta `{ attemptId, reference, total, processUrl, expiresAt }`. |
| `GET /payments/{id}` | estado; si está `pending` o recién vuelve, consulta la pasarela antes de responder (FR-038). |
| `GET /payments?limit=` | historial descendente (FR-054). |
| `GET /payments/{id}/receipt` | PDF del ERP (FR-048). |
| `GET /documents` · `POST /documents` | documentos publicados y su PDF (FR-055). |
| `POST /devices/push-token` | registro de push (app o Web Push). |

### Retorno de la pasarela
`GET /pagos/{attemptId}/retorno` es una **ruta de la PWA** (no de la API): muestra el resultado llamando a
`GET /api/v1/payments/{id}`. En la app, la misma URL es App Link / Universal Link.

## B. Webhook de la pasarela

`POST /webhooks/placetopay/{siteId}` — cuerpo `{ status{ status, reason, message, date }, requestId, reference,
signature }`. Verifica la firma con el secreto del sitio (R11), responde 200 enseguida, encola la consulta del intento
y sólo actúa si está `pending` (FR-039, FR-040). Firma inválida → 200 sin efecto + auditoría (no se le da pista al
emisor) y alerta si se repite.

## C. API interna para el ERP (`/internal/v1`)

| Método y ruta | Descripción |
|---|---|
| `PUT /tenants/{t}/sites/{siteId}/credentials` | `{ gateway, baseUrl, login, secretKey, paymentMethods }` → `{ activa, verificadaEn (nulo si quedó pendiente), loginLast4, motivo }` (R10). El secreto no vuelve nunca; sin verificador de la pasarela queda `pending_verification`. Errores 400 `Canal.Sitio.*` con `data.errores[{campo, code, message}]`; sitio de otra cooperativa → 409 `Canal.Sitio.DeOtraCooperativa`. |
| `POST /tenants/{t}/channel-code` | `{ suggested }` → `{ code }`: el código de 3 letras de la cooperativa, único en la plataforma (R12); el mismo si ya tenía uno. 409 `Canal.Codigo.Agotados` / `.EnConflicto`. |
| `GET /tenants/{t}/attempts?document=&reference=` | atención (FR-058): intentos y estado en la pasarela. |
| `POST /tenants/{t}/attempts/{reference}/reverse` | `{ requestedBy, reason }` → reversa en la pasarela si es antes del corte y luego llama `…/payment-notices/{reference}/reversal` del ERP (FR-052). Después del corte → 409 `Canal.Reverso.FueraDeCorte`. |
| `GET /tenants/{t}/accounts?document=` | cuenta del canal de una persona habilitada: estado, último ingreso, dispositivos (FR-058). |
| `POST /tenants/{t}/accounts/{id}/unlock` · `/sessions/revoke` | FR-058. |
| `GET /tenants/{t}/metrics?from=&to=` | indicadores (FR-060). |
| `POST /tenants/{t}/sync` | encola el arrastre de habilitaciones de la cooperativa (tras una carga masiva) → 202 `{ enqueued: true }`. |

Toda ruta interna verifica que la cuenta o el intento pertenezcan a esa cooperativa: nunca devuelve datos de la
persona en otra (FR-059).
