# =============================================================================
# crear-secreto-tunel.ps1 - Instala el token del Cloudflare Tunnel como Secret
# de Kubernetes en el cluster indicado.
#
# SEGURIDAD: el token se pide por teclado (oculto), viaja por el canal cifrado
# de SSH usando STDIN (nunca como argumento, asi no aparece en `ps` ni en el
# historial del shell) y no se escribe en ningun archivo local.
#
# El token se obtiene en: Cloudflare -> Zero Trust -> Networks -> Tunnels ->
# (tu tunel) -> Configure. Es la cadena que empieza con "eyJ...".
#
# Uso:
#   .\tools\scripts\crear-secreto-tunel.ps1                    # produccion
#   .\tools\scripts\crear-secreto-tunel.ps1 -Ambiente nonprod  # DEV/QA
# =============================================================================
param(
    [ValidateSet('pdn', 'nonprod')]
    [string]$Ambiente = 'pdn',
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$destino = if ($Ambiente -eq 'pdn') {
    @{ Host = '100.104.190.76'; Namespace = 'cloudflare'; Nombre = 'PRODUCCION' }
} else {
    @{ Host = '100.94.218.42';  Namespace = 'cloudflare'; Nombre = 'DEV/QA' }
}

Write-Host ""
Write-Host "  Token del Cloudflare Tunnel para $($destino.Nombre)" -ForegroundColor Cyan
Write-Host "  (Zero Trust -> Networks -> Tunnels -> Configure; empieza con eyJ...)" -ForegroundColor DarkGray
Write-Host ""

$tokenSecure = Read-Host "  TUNNEL_TOKEN (no se muestra)" -AsSecureString
$token = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
             [Runtime.InteropServices.Marshal]::SecureStringToBSTR($tokenSecure))

if ([string]::IsNullOrWhiteSpace($token)) { throw "El token no puede estar vacio." }
if (-not $token.StartsWith('eyJ')) {
    Write-Host "  AVISO: el token no empieza con 'eyJ'. Verifica que copiaste el token" -ForegroundColor Yellow
    Write-Host "         completo y no el comando entero de instalacion." -ForegroundColor Yellow
    $seguir = Read-Host "  Continuar de todas formas? (s/N)"
    if ($seguir -ne 's') { throw "Cancelado." }
}

$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($token))

$yaml = @"
apiVersion: v1
kind: Namespace
metadata:
  name: $($destino.Namespace)
---
apiVersion: v1
kind: Secret
metadata:
  name: cloudflared-token
  namespace: $($destino.Namespace)
type: Opaque
data:
  token: $b64
"@

Write-Host ""
Write-Host ("  Instalando en {0} ... " -f $destino.Nombre) -NoNewline
$salida = $yaml | ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" "k3s kubectl apply -f -" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "FALLO" -ForegroundColor Red
    Write-Host "     $salida" -ForegroundColor DarkRed
    exit 1
}

$token = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Verificacion:" -ForegroundColor Cyan
ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" `
    "k3s kubectl get secret cloudflared-token -n $($destino.Namespace) -o jsonpath='    secreto {.metadata.name} creado ({.type})'; echo" 2>&1
Write-Host ""
Write-Host "  Avisale al asistente para que despliegue el conector." -ForegroundColor DarkGray
Write-Host ""
