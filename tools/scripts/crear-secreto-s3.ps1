# =============================================================================
# crear-secreto-s3.ps1 - Crea el Secret de credenciales S3 para los backups de
# PostgreSQL (CloudNativePG + Barman Cloud Plugin) en los 3 ambientes.
#
# SEGURIDAD: las credenciales se piden por teclado (ocultas), viajan por el canal
# cifrado de SSH usando STDIN (nunca como argumento de linea de comandos, asi no
# aparecen en `ps` del servidor ni en el historial del shell) y no se escriben
# en ningun archivo local.
#
# Uso:
#   .\tools\scripts\crear-secreto-s3.ps1
#
# Re-ejecutable: si el secreto ya existe, lo actualiza (util al rotar llaves).
# =============================================================================
param(
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$destinos = @(
    @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' },
    @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV'        },
    @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA'         }
)

Write-Host ""
Write-Host "  Credenciales del usuario IAM dedicado a los backups del ERP" -ForegroundColor Cyan
Write-Host "  (NO uses las llaves de tu usuario principal de AWS)" -ForegroundColor DarkGray
Write-Host ""

$accessKey = Read-Host "  AWS_ACCESS_KEY_ID"
if ([string]::IsNullOrWhiteSpace($accessKey)) { throw "El Access Key ID no puede estar vacio." }

$secretSecure = Read-Host "  AWS_SECRET_ACCESS_KEY (no se muestra)" -AsSecureString
$secretPlain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secretSecure))
if ([string]::IsNullOrWhiteSpace($secretPlain)) { throw "El Secret Access Key no puede estar vacio." }

# El Secret se arma como YAML con los valores en base64 y se envia por STDIN.
$b64Key    = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($accessKey))
$b64Secret = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($secretPlain))

Write-Host ""
foreach ($d in $destinos) {
    $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: s3-backup-creds
  namespace: $($d.Namespace)
type: Opaque
data:
  ACCESS_KEY_ID: $b64Key
  ACCESS_SECRET_KEY: $b64Secret
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

# Limpieza de la variable en memoria.
$secretPlain = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Listo. Verificacion:" -ForegroundColor Cyan
foreach ($d in $destinos) {
    $r = ssh -i $KeyPath -o BatchMode=yes "root@$($d.Host)" `
         "k3s kubectl get secret s3-backup-creds -n $($d.Namespace) -o jsonpath='{.metadata.name} ({.type}) claves={.data.ACCESS_KEY_ID}' 2>/dev/null | sed 's/claves=.*/claves=OK/'" 2>&1
    Write-Host ("    {0,-10} {1}" -f $d.Namespace, $r)
}
Write-Host ""
