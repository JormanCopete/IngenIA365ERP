# Canal digital — revisión de seguridad del satélite (feature 021, T150)

Revisión del servicio del canal (`canal/api`), de la PWA (`canal/web`) y de la app (`canal/app`) hecha el 2026-11-13 sobre la rama
`021-canal-asociados-pagos`, por puntos. No reemplaza una revisión externa antes de producción.

| Punto | Estado | Dónde |
|---|---|---|
| Cabeceras: CSP `default-src 'self'` sin `unsafe-*`, `frame-ancestors 'none'`, HSTS, `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`, `Permissions-Policy`, COOP | Bien | `internal/http/middleware.go` |
| Cookies de sesión `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/`; el token sólo en la cookie (PWA) o en el almacén seguro (app) | Bien | `internal/identidad/sesion.go` |
| CSRF, segunda defensa: cuerpo JSON exigido | **Corregido**: la simulación de conceptos, los documentos y el crédito en línea no exigían `Content-Type: application/json`; ahora sí | `internal/conceptos/*.go` |
| CSRF, tercera defensa: `Origin` | **Agregado**: una escritura en `/api/` con la cookie de sesión y un `Origin` que no es el del canal responde 403 `Canal.Origen.NoPermitido` (la app, que usa su token y no cookie, no se afecta) | `verificarOrigen` en `internal/http/middleware.go`; prueba `TestLasEscriturasConCookieDeOtroSitioSeRechazan` |
| Límites: cubeta por IP para todo menos `/salud` e `/internal`; freno de ingresos por documento (5 fallos, 15 min) exista o no la cuenta; límites de códigos por documento e IP; SMS por cooperativa | Bien | `internal/http/limite.go`, `internal/identidad` |
| Enumeración: activar, recuperar e ingresar responden igual exista o no el documento (desafíos señuelo, mismo mensaje y tiempo); la atención de una cooperativa no revela si la persona está en otra | Bien | `internal/identidad`, `internal/atencion` (prueba T123) |
| Webhook de la pasarela: firma SHA-256 (o SHA-1 heredada) con el secreto del sitio, comparación en tiempo constante, 200 sin efecto y auditoría si no cuadra; el estado se confirma siempre consultando a la pasarela | Bien | `internal/pagos/webhook.go`, `internal/pasarela/placetopay` |
| Tokens de servicio ERP ↔ canal: ES256, 60 s, `aud`/`iss` fijos, `sub` = cooperativa de la ruta, `jti` de un solo uso en base compartida por las réplicas | Bien | `internal/erp/verificador.go`; ERP `CanalServicioAuthentication` |
| Secretos: credenciales de la pasarela cifradas con KEK versionada, nunca en el ERP ni en logs; tarjeta nunca guardada; llaves, VAPID, FCM y firma de la app sólo por Secrets | Bien | `internal/cifrado`, `docs/operaciones/canal-digital-llaves.md` |
| Revisión de secretos del repositorio (T149): sin llaves privadas, credenciales de pasarela, archivos de Firebase ni de firma; las URLs de prueba son `*.test` | Bien | `git grep` de patrones de llaves, `AKIA`, `AIza`, `private_key` |
| App: el puente sólo abre la pasarela por https y sólo acepta retornos del host del canal; enlaces entrantes de otro host se ignoran; la credencial del dispositivo sale de `SecureStorage` sólo tras la biometría | Bien | `canal/app/Puente/PuenteDelCanal.cs`, `PaginaPrincipal.cs` |
| Push: la suscripción de Web Push sólo con endpoint https; los avisos no llevan datos de saldos; un endpoint vencido se revoca | Bien | `internal/avisos`, `internal/mensajeria/push` |

## Pendientes fuera del código

- Revisión externa (pentest) antes de producción, con el sitio de pruebas de Avalpay.
- Confirmar con el analista de Avalpay la ventana del reverso y si `FAILED` existe (`certificacion-pasarela-avalpay.md`).
- La llave AWS `AKIA…` que figura en `docs/operaciones/estado-y-pendientes.md` ya no la usa el ERP (es anterior a la 021); conviene
  confirmar que está desactivada en IAM.
