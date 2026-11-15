#!/usr/bin/env bash
# Genera los pares de llaves ES256 de DESARROLLO para los tokens de servicio entre el canal y el ERP, y una KEK de
# desarrollo para cifrar secretos de pasarela. Todo queda FUERA del repositorio, en $HOME/.ingenia365/canal-dev/.
# En DEV, QA y producción las llaves son Secrets de Kubernetes que carga el dueño (docs/operaciones/canal-digital-llaves.md).
set -euo pipefail

destino="${CANAL_LLAVES_DIR:-$HOME/.ingenia365/canal-dev}"
mkdir -p "$destino"
chmod 700 "$destino" 2>/dev/null || true

generar_par() {
  local nombre="$1"
  if [[ -f "$destino/$nombre-privada.pem" ]]; then
    echo "Ya existe $destino/$nombre-privada.pem; no se sobrescribe."
    return
  fi
  openssl ecparam -name prime256v1 -genkey -noout -out "$destino/$nombre-privada.pem"
  openssl ec -in "$destino/$nombre-privada.pem" -pubout -out "$destino/$nombre-publica.pem" 2>/dev/null
  chmod 600 "$destino/$nombre-privada.pem" 2>/dev/null || true
  echo "Par ES256 '$nombre' en $destino"
}

generar_par canal   # firma el canal, lo verifica el ERP
generar_par erp     # firma el ERP, lo verifica el canal

if [[ ! -f "$destino/kek-v1.b64" ]]; then
  openssl rand -base64 32 > "$destino/kek-v1.b64"
  chmod 600 "$destino/kek-v1.b64" 2>/dev/null || true
  echo "KEK de desarrollo v1 en $destino/kek-v1.b64"
fi

cat <<EOF

Variables para el canal (canal/api/.env, que git ignora):
  CANAL_LLAVE_PRIVADA_FILE=$destino/canal-privada.pem
  CANAL_ERP_LLAVE_PUBLICA_FILE=$destino/erp-publica.pem
  CANAL_KEK_V1_FILE=$destino/kek-v1.b64

Variables para el ERP (por entorno, sin crear appsettings.Development.json):
  CanalServicio__LlavesPublicas__dev1=<contenido de $destino/canal-publica.pem>
  CanalServicio__LlavePrivadaErp=<contenido de $destino/erp-privada.pem>
EOF
