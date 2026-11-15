# Canal digital del asociado

Sistema satélite del ERP (constitución v2.1.0, sección «Sistemas satélite»; feature 021). Le da al asociado de cada
cooperativa ingreso con documento y clave, consulta de sus conceptos y pago por la pasarela de su cooperativa.

| Carpeta | Qué es |
|---|---|
| `api/` | Servicio en Go con su propia base PostgreSQL (`ingenia365_canal`). Sirve también la PWA embebida. |
| `web/` | Interfaz React (PWA). El mismo build se empaqueta en la app. |
| `app/` | Envoltura MAUI HybridWebView para Play Store y App Store (entrega C5). |
| `scripts/` | Utilidades de desarrollo (llaves locales, empaquetado de la app). |

## Reglas que no se negocian

1. El canal **nunca** abre una conexión a una base del ERP, a su Mongo ni a su Redis. Todo lo pide por la API de
   servicio del ERP (`/api/canal/v1`), y el ERP es el único que escribe en sus libros.
2. Toda llamada canal ↔ ERP lleva un token de servicio ES256 de 60 s con `jti` de un solo uso; la cooperativa va en
   la ruta y en el token.
3. Todo aviso al ERP es idempotente por la referencia del intento.
4. El canal no guarda saldos, movimientos ni datos contables; de cada cooperativa sólo el `PublicId` de la persona,
   su documento y el estado de la habilitación.
5. Sólo `PublicId` del ERP y UUID propios hacia afuera.
6. Nada se borra en flujo de usuario (estados y fechas); validación en cliente y servidor con mensajes en español;
   ningún error silenciado; migraciones `goose` con Up y Down; auditoría sólo de anexar.
7. Los secretos de las pasarelas se guardan cifrados con una llave fuera de la base, nunca en el repositorio ni en
   logs, nunca devueltos completos.
8. Claves de personas con bcrypt costo 12.
9. Las pruebas viven con el código y el CI (`.github/workflows/canal.yml`) las corre en cada cambio de `canal/`.

El repositorio es público: **ninguna** credencial, URL de sitio de una cooperativa, dato de persona ni texto de las
guías confidenciales de la pasarela entra aquí.

## Levantarlo en local

```bash
docker compose -f canal/docker-compose.yml up -d     # PostgreSQL del canal en el puerto 5434
canal/scripts/llaves-dev.sh                          # llaves ES256 de desarrollo, fuera del repositorio
cd canal/web && npm ci && npm run build              # build de la PWA
cd canal/api && go run ./cmd/canal                   # con las variables de entorno de canal/api/.env.ejemplo
```

La pasarela por defecto en desarrollo es la **simulada** (`CANAL_PASARELA=simulada`). Las credenciales de prueba de
una pasarela real se cargan por la pantalla del ERP, nunca en archivos.

Documentación: `specs/021-canal-asociados-pagos/` (spec, plan, contratos) y `docs/operaciones/canal-digital-*.md`.
