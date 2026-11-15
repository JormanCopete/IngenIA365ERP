#!/usr/bin/env bash
# Feature 021, US11 (T135): copia el build de la PWA en modo «app» (rutas por hash, sin service worker, API absoluta) a la app MAUI.
# Uso: CANAL_HOST=asociados.ejemplo.test canal/scripts/empaquetar-app.sh
# Después: dotnet build canal/app -f net10.0-android -p:CanalHost=$CANAL_HOST (la firma, fuera del CI: docs/operaciones/canal-digital-app-tiendas.md).
set -euo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
host="${CANAL_HOST:-asociados.ingenia365.com}"
destino="$raiz/app/Resources/Raw/wwwroot"

cd "$raiz/web"
npm ci --no-audit --no-fund
VITE_CANAL_API_BASE="https://$host/api/v1" npm run build:app -- --outDir "$destino" --emptyOutDir

# Con huella en el nombre: el HybridWebView sirve los archivos locales y una versión nueva de la app nunca muestra los viejos.
if ! ls "$destino"/assets/*-*.js >/dev/null 2>&1; then
  echo "El build de la app no trae archivos con huella en assets/: revisar vite.config.ts." >&2
  exit 1
fi
echo "PWA empaquetada para la app en $destino (host $host)."
