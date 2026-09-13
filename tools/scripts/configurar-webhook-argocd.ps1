# =============================================================================
# configurar-webhook-argocd.ps1 - Conecta el repositorio GitOps con Argo CD por
# webhook: cada push a JormanCopete/ingenia365-gitops avisa a Argo CD de NONPROD
# al instante, en vez de esperar su sondeo (hasta 3 minutos, y en la practica
# hubo que refrescar a mano).
#
# Hace DOS cosas con UN secreto compartido que se pide por teclado (oculto):
#   1. Lo deja en el Secret argocd-secret del cluster (clave webhook.github.secret),
#      viajando por STDIN sobre SSH: nunca como argumento ni a un archivo.
#   2. Crea el webhook en GitHub con `gh api`, mandando el JSON por STDIN.
# Antes hay que haber publicado el hostname en el tunel de Cloudflare (ver
# docs/operaciones/despliegue-infraestructura.md, "Webhook GitHub -> Argo CD").
#
# Requiere: llave SSH ~/.ssh/ingenia365_deploy y `gh` autenticado como el dueno
# del repo GitOps (cuenta JormanCopete).
#
# Uso:
#   .\tools\scripts\configurar-webhook-argocd.ps1
#   .\tools\scripts\configurar-webhook-argocd.ps1 -Hostname hooks.ingenia365.com
# =============================================================================
param(
    [string]$Hostname = 'argocd-webhook.ingenia365.com',
    [string]$Repo = 'JormanCopete/ingenia365-gitops',
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    [string]$NodoNonprod = '100.94.218.42'
)

$ErrorActionPreference = 'Stop'
$url = "https://$Hostname/api/webhook"

Write-Host ""
Write-Host "  Webhook GitHub -> Argo CD (nonprod)" -ForegroundColor Cyan
Write-Host "  Repo: $Repo   URL: $url" -ForegroundColor DarkGray
Write-Host "  Secreto compartido: cualquier cadena larga y aleatoria (40+ caracteres)." -ForegroundColor DarkGray
Write-Host ""

$secure = Read-Host "  WEBHOOK_SECRET (no se muestra)" -AsSecureString
$secreto = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
              [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
if ($secreto.Length -lt 20) { throw "El secreto es muy corto: usa al menos 20 caracteres." }

# --- 1. Cluster: la clave webhook.github.secret en argocd-secret -----------------
# Se manda como parche JSON por STDIN; kubectl lo lee de /dev/stdin.
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($secreto))
$parche = '{"data":{"webhook.github.secret":"' + $b64 + '"}}'
Write-Host "  Guardando el secreto en argocd-secret ... " -NoNewline
$salida = $parche | ssh -i $KeyPath -o BatchMode=yes "root@$NodoNonprod" `
    "k3s kubectl -n argocd patch secret argocd-secret --type merge --patch-file /dev/stdin" 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "FALLO" -ForegroundColor Red; Write-Host "     $salida" -ForegroundColor DarkRed; exit 1 }
Write-Host "OK" -ForegroundColor Green

# --- 2. GitHub: el webhook con el mismo secreto ------------------------------------
$json = @{
    name   = 'web'
    active = $true
    events = @('push')
    config = @{ url = $url; content_type = 'json'; secret = $secreto; insecure_ssl = '0' }
} | ConvertTo-Json -Compress
Write-Host "  Creando el webhook en GitHub ... " -NoNewline
$salida = $json | gh api "repos/$Repo/hooks" --method POST --input - 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "FALLO" -ForegroundColor Red
    Write-Host "     $salida" -ForegroundColor DarkRed
    Write-Host "     Si ya existia un webhook a esa URL, borralo en GitHub (Settings -> Webhooks) y vuelve a correr." -ForegroundColor Yellow
    exit 1
}
Write-Host "OK" -ForegroundColor Green

$secreto = $null; $json = $null; $parche = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Verificacion:" -ForegroundColor Cyan
Write-Host "   - GitHub -> Settings -> Webhooks -> Recent Deliveries: el ping debe responder 200." -ForegroundColor DarkGray
Write-Host "   - Tras el proximo push al GitOps, Argo CD de nonprod pasa a OutOfSync/Syncing en segundos." -ForegroundColor DarkGray
Write-Host ""
