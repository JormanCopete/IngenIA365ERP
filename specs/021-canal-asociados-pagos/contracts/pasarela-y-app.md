# Contrato — Adaptador de pasarela y puente de la app

## Interfaz de pasarela (Go, `canal/api/internal/pasarela`)

```go
type Pasarela interface {
    CrearSesion(ctx context.Context, sitio Sitio, s SolicitudDeSesion) (SesionCreada, error)
    ConsultarSesion(ctx context.Context, sitio Sitio, requestID int64) (EstadoDeSesion, error)
    Reversar(ctx context.Context, sitio Sitio, internalReference int64) (ResultadoDeReverso, error)
    VerificarNotificacion(sitio Sitio, cuerpo []byte) (Notificacion, error) // firma SHA-256 con prefijo o SHA-1
}
```

- `SolicitudDeSesion`: referencia, descripción, total, impuestos, comprador (tipo y número de documento, nombres,
  apellidos, correo, celular), IP, agente, vencimiento, URL de retorno, medios.
- `EstadoDeSesion`: estado de la sesión normalizado (`pending`, `approved`, `rejected`, `failed`, `expired`) + el
  intento aprobado (`internalReference`, `authorization`, `receipt`, `franchise`, `paymentMethodName`,
  `issuerName`, `date`) + la respuesta cruda **sin** datos de tarjeta (FR-042).
- Implementaciones: `placetopay` (Avalpay) y `simulada` (pruebas y DEV: estados elegibles, como el mock de la
  pasarela). Una pasarela nueva es otra implementación; nada fuera del paquete conoce a Placetopay.

### Normalización de estados

| Pasarela (sesión) | Canal |
|---|---|
| `PENDING` | `pending` |
| `APPROVED` | `approved` |
| `REJECTED` | `rejected` (o `expired` si el motivo es vencimiento) |
| `FAILED` (si existe) / error de la pasarela | `failed` |
| `APPROVED_PARTIAL`, `PARTIAL_EXPIRED` | no se esperan (`allowPartial = false`); se registran y alertan como diferencia |

### Validación del comprador antes de crear la sesión (FR-037)

- Nombres y apellidos: letras, espacios, tildes y Ñ; sin números ni símbolos.
- Documento según el tipo (CC `^[1-9][0-9]{3,9}$`, CE `^([a-zA-Z]{1,5})?[1-9][0-9]{3,7}$`, TI `^[1-9][0-9]{4,11}$`,
  NIT `^[1-9]\d{6,9}$`; otros como `PPN` `^[a-zA-Z0-9_]{4,16}$`).
- Correo con estructura válida; celular sólo dígitos.
- Referencia alfanumérica de 18 caracteres (R12).

## Puente de la app (MAUI HybridWebView ↔ React)

React detecta la app por `window.HybridWebView`. Métodos que C# expone con `SetInvokeJavaScriptTarget`:

| Método | Respuesta | Uso |
|---|---|---|
| `Plataforma()` | `{ os, version, appVersion }` | registro de dispositivo |
| `GuardarCredencial(valor)` / `LeerCredencialConBiometria(motivo)` / `BorrarCredencial()` | `string?` | credencial de dispositivo en `SecureStorage`, liberada sólo tras `BiometricPrompt` / `LAContext` (R13) |
| `BiometriaDisponible()` | `{ disponible, tipo }` | ofrecer «entrar con huella/rostro» |
| `AbrirPasarela(url, retorno)` | `{ resultado: "returned" \| "cancelled", url? }` | `WebAuthenticator` con Custom Tabs / `ASWebAuthenticationSession`; vuelve por App Link (R14) |
| `TokenPush()` | `string?` | token FCM para `POST /devices/push-token` |
| `AbrirDocumento(bytesBase64, nombre, tipo)` | | guardar o compartir el PDF |

C# → JS: `window.canal.alAbrirEnlace(url)` (App Link entrante, p. ej. el retorno o una notificación) y
`window.canal.alNotificacion(datos)`.

En la PWA, los mismos servicios tienen su versión web: passkeys (WebAuthn), `window.location` hacia la pasarela,
Web Push. Un módulo `plataforma.ts` decide cuál usar; ninguna pantalla pregunta por la plataforma.
