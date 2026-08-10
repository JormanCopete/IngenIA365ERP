# =============================================================================
# crear-secreto-ghcr.ps1 - Crea el Secret que permite a los clusteres descargar
# las imagenes del ERP desde GitHub Container Registry (GHCR).
#
# POR QUE HACE FALTA: las imagenes se publican como paquetes PRIVADOS (contienen
# el codigo compilado del producto). Sin este secreto los pods quedan en
# ImagePullBackOff con "unauthorized", aunque la imagen exista.
#
# TOKEN A USAR: un Personal Access Token *classic* de GitHub con UN SOLO permiso,
# `read:packages`. No uses un token con permisos de escritura ni tu contrasena:
# este token vive dentro de los servidores y solo debe poder leer paquetes.
#   GitHub -> Settings -> Developer settings -> Personal access tokens (classic)
#
# SEGURIDAD: el token se pide por teclado (oculto), viaja por el canal cifrado de
# SSH usando STDIN (nunca como argumento de linea de comandos, asi no aparece en
# `ps` del servidor ni en el historial del shell) y no se escribe en ningun
# archivo local.
#
# Uso:
#   .\tools\scripts\crear-secreto-ghcr.ps1
#
# Re-ejecutable: si el secreto ya existe, lo reemplaza (util al rotar el token).
# =============================================================================
param(
    [string]$KeyPath  = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    [string]$Usuario  = 'JormanCopete'
)

$ErrorActionPreference = 'Stop'

$destinos = @(
    @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' },
    @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV'        },
    @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA'         }
)

Write-Host ""
Write-Host "  Token de GitHub para DESCARGAR imagenes (solo read:packages)" -ForegroundColor Cyan
Write-Host "  Usuario: $Usuario" -ForegroundColor DarkGray
Write-Host ""

$tokenSecure = Read-Host "  GITHUB_TOKEN (no se muestra)" -AsSecureString
$tokenPlain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                   [Runtime.InteropServices.Marshal]::SecureStringToBSTR($tokenSecure))
if ([string]::IsNullOrWhiteSpace($tokenPlain)) { throw "El token no puede estar vacio." }

# Formato dockerconfigjson: el par usuario:token va en base64 dentro de "auth",
# y el JSON completo se vuelve a codificar en base64 para el campo .dockerconfigjson.
$auth       = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${Usuario}:${tokenPlain}"))
$dockerJson = "{`"auths`":{`"ghcr.io`":{`"auth`":`"$auth`"}}}"
$b64Config  = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($dockerJson))

Write-Host ""
foreach ($d in $destinos) {
    $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: ghcr-pull
  namespace: $($d.Namespace)
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: $b64Config
"@
    Write-Host ("  {0,-12} ({1}) ... " -f $d.Nombre, $d.Namespace) -NoNewline
    $salida = $yaml | ssh -i $KeyPath -o BatchMode=yes "root@$($d.Host)" "k3s kubectl apply -f -" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "FALLO" -ForegroundColor Red
        Write-Host "     $salida" -ForegroundColor DarkRed
    }
}

# Limpieza de las variables en memoria.
$tokenPlain = $null; $auth = $null; $dockerJson = $null; $b64Config = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Listo. Verificacion:" -ForegroundColor Cyan
foreach ($d in $destinos) {
    $r = ssh -i $KeyPath -o BatchMode=yes "root@$($d.Host)" `
         "k3s kubectl get secret ghcr-pull -n $($d.Namespace) -o jsonpath='{.metadata.name} ({.type})' 2>/dev/null" 2>&1
    Write-Host ("    {0,-10} {1}" -f $d.Namespace, $r)
}
Write-Host ""
Write-Host "  Si un pod sigue en ImagePullBackOff, el motivo exacto esta en:" -ForegroundColor DarkGray
Write-Host "    k3s kubectl describe pod -n erp-dev -l app.kubernetes.io/name=erp-api" -ForegroundColor DarkGray
Write-Host ""
