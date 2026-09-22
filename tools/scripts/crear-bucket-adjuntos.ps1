# =============================================================================
# crear-bucket-adjuntos.ps1 - Crea en AWS el bucket donde el ERP guarda los
# adjuntos (soportes de comprobantes, planillas, archivos de dispersion) y, si
# hay permisos, el usuario IAM con su llave instalada en los tres clusteres.
#
# ES OTRO BUCKET que el de respaldos, a proposito: aquel tiene Object Lock a 40
# dias porque un respaldo no debe poder borrarse; un adjunto SI se borra cuando
# su dueno lo borra desde la aplicacion. Mezclarlos obligaria a elegir una sola
# retencion para las dos cosas.
#
#   ingenia365-erp-attachments/{pdn,qa,dev}/<cooperativa>/<aaaa>/<mm>/<guid>.bin
#
# Lo que SI lleva: versionado (un borrado accidental se recupera), cifrado
# AES256 del lado del servidor -encima del nuestro, que ya cifra cada archivo
# con AES-256-GCM antes de subirlo-, bloqueo total de acceso publico y ciclo de
# vida que limpia las versiones viejas a los 90 dias y los multipart a medias.
#
# ES RE-EJECUTABLE: si el bucket ya existe, completa lo que falte sin recrearlo.
#
# SEGURIDAD: la llave secreta nunca se imprime ni se escribe en un archivo. Se
# genera, viaja por STDIN sobre SSH a los clusteres y se borra de memoria.
#
# Uso:
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloVerificar
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365 -OmitirIam
# =============================================================================
param(
    [string]$Bucket     = 'ingenia365-erp-attachments',
    [string]$Region     = 'us-east-1',
    [string]$UsuarioIam = 'ingenia365-erp-adjuntos',
    [string]$KeyPath    = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    # Perfil del AWS CLI. Sin esto se usa el perfil por defecto, que puede
    # apuntar a OTRA cuenta de AWS.
    [string]$Perfil     = '',
    # Salta la parte de IAM: util cuando quien ejecuta no puede crear usuarios y
    # la credencial se genera a mano desde la consola.
    [switch]$OmitirIam,
    [switch]$SoloVerificar
)

$ErrorActionPreference = 'Stop'

$aws = @('aws')
if ($Perfil) { $aws += @('--profile', $Perfil) }

function Invoke-Aws {
    param([string[]]$Argumentos, [switch]$TolerarError)
    $salida = & $aws[0] ($aws[1..($aws.Length - 1)] + $Argumentos) 2>&1
    if ($LASTEXITCODE -ne 0 -and -not $TolerarError) {
        throw ("aws {0} fallo: {1}" -f ($Argumentos -join ' '), ($salida -join "`n"))
    }
    return $salida
}

Write-Host ""
Write-Host "  Bucket de adjuntos del ERP" -ForegroundColor Cyan
Write-Host ("  {0} ({1})" -f $Bucket, $Region) -ForegroundColor DarkGray
Write-Host ""

$identidad = Invoke-Aws @('sts', 'get-caller-identity', '--output', 'text', '--query', 'Account')
Write-Host ("  Cuenta AWS: {0}" -f $identidad) -ForegroundColor DarkGray

$existe = $true
Invoke-Aws @('s3api', 'head-bucket', '--bucket', $Bucket) -TolerarError | Out-Null
if ($LASTEXITCODE -ne 0) { $existe = $false }

if ($SoloVerificar) {
    Write-Host ("  Existe: {0}" -f $(if ($existe) { 'si' } else { 'NO' }))
    if ($existe) {
        Write-Host ("  Versionado: {0}" -f (Invoke-Aws @('s3api', 'get-bucket-versioning', '--bucket', $Bucket, '--output', 'text') -TolerarError))
        Write-Host ("  Cifrado:    {0}" -f (Invoke-Aws @('s3api', 'get-bucket-encryption', '--bucket', $Bucket, '--output', 'text') -TolerarError))
        Write-Host ("  Objetos:    {0}" -f (Invoke-Aws @('s3api', 'list-objects-v2', '--bucket', $Bucket, '--max-items', '1', '--output', 'text') -TolerarError))
    }
    return
}

if (-not $existe) {
    Write-Host "  Creando el bucket ... " -NoNewline
    if ($Region -eq 'us-east-1') {
        Invoke-Aws @('s3api', 'create-bucket', '--bucket', $Bucket, '--region', $Region) | Out-Null
    } else {
        Invoke-Aws @('s3api', 'create-bucket', '--bucket', $Bucket, '--region', $Region,
                     '--create-bucket-configuration', "LocationConstraint=$Region") | Out-Null
    }
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "  El bucket ya existe: se completa lo que falte." -ForegroundColor DarkGray
}

Write-Host "  Bloqueando el acceso publico ... " -NoNewline
Invoke-Aws @('s3api', 'put-public-access-block', '--bucket', $Bucket,
             '--public-access-block-configuration',
             'BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true') | Out-Null
Write-Host "OK" -ForegroundColor Green

Write-Host "  Versionado ... " -NoNewline
Invoke-Aws @('s3api', 'put-bucket-versioning', '--bucket', $Bucket,
             '--versioning-configuration', 'Status=Enabled') | Out-Null
Write-Host "OK" -ForegroundColor Green

Write-Host "  Cifrado del lado del servidor (AES256) ... " -NoNewline
$cifrado = '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'
Invoke-Aws @('s3api', 'put-bucket-encryption', '--bucket', $Bucket,
             '--server-side-encryption-configuration', $cifrado) | Out-Null
Write-Host "OK" -ForegroundColor Green

Write-Host "  Ciclo de vida (versiones viejas 90 d, multipart a medias 7 d) ... " -NoNewline
$ciclo = '{"Rules":[{"ID":"limpieza","Status":"Enabled","Filter":{},' +
         '"NoncurrentVersionExpiration":{"NoncurrentDays":90},' +
         '"AbortIncompleteMultipartUpload":{"DaysAfterInitiation":7}}]}'
Invoke-Aws @('s3api', 'put-bucket-lifecycle-configuration', '--bucket', $Bucket,
             '--lifecycle-configuration', $ciclo) | Out-Null
Write-Host "OK" -ForegroundColor Green

if ($OmitirIam) {
    Write-Host ""
    Write-Host "  IAM omitido. La politica minima esta en docs/operaciones/politica-iam-adjuntos.json" -ForegroundColor Yellow
    Write-Host "  Cree el usuario, adjuntesela y luego instale la llave con -SoloSecreto." -ForegroundColor Yellow
    return
}

$politica = Join-Path $PSScriptRoot '..\..\docs\operaciones\politica-iam-adjuntos.json'
if (-not (Test-Path $politica)) { throw "No se encontro la politica: $politica" }

Write-Host ""
Write-Host ("  Usuario IAM {0} ... " -f $UsuarioIam) -NoNewline
Invoke-Aws @('iam', 'get-user', '--user-name', $UsuarioIam) -TolerarError | Out-Null
if ($LASTEXITCODE -ne 0) {
    Invoke-Aws @('iam', 'create-user', '--user-name', $UsuarioIam) | Out-Null
    Write-Host "creado" -ForegroundColor Green
} else {
    Write-Host "ya existia" -ForegroundColor DarkGray
}

Write-Host "  Politica minima ... " -NoNewline
Invoke-Aws @('iam', 'put-user-policy', '--user-name', $UsuarioIam,
             '--policy-name', 'AdjuntosDelErp',
             '--policy-document', "file://$((Resolve-Path $politica).Path)") | Out-Null
Write-Host "OK" -ForegroundColor Green

Write-Host ""
Write-Host "  Generando la llave de acceso (no se imprime) ... " -NoNewline
$llaveJson = Invoke-Aws @('iam', 'create-access-key', '--user-name', $UsuarioIam, '--output', 'json') | Out-String
$llave = $llaveJson | ConvertFrom-Json
$idLlave = $llave.AccessKey.AccessKeyId
$secreta = $llave.AccessKey.SecretAccessKey
Write-Host "OK" -ForegroundColor Green
Write-Host ("  AccessKeyId: {0}" -f $idLlave) -ForegroundColor DarkGray

$destinos = @(
    @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' },
    @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' },
    @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' }
)

$b64Id      = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($idLlave))
$b64Secreta = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($secreta))

foreach ($d in $destinos) {
    $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: erp-adjuntos-s3
  namespace: $($d.Namespace)
type: Opaque
data:
  accessKeyId: $b64Id
  secretAccessKey: $b64Secreta
"@
    Write-Host ("  Instalando el Secret en {0} ... " -f $d.Nombre) -NoNewline
    $salida = $yaml | ssh -i $KeyPath -o BatchMode=yes "root@$($d.Host)" "k3s kubectl apply -f -" 2>&1
    if ($LASTEXITCODE -eq 0) { Write-Host "OK" -ForegroundColor Green }
    else { Write-Host "FALLO" -ForegroundColor Red; Write-Host ("    {0}" -f ($salida -join "`n")) -ForegroundColor Red }
}

$secreta = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Listo. Falta:" -ForegroundColor Cyan
Write-Host "   1. Que el overlay de cada ambiente ponga AttachmentStorage__Provider=S3 y el bucket (GitOps)."
Write-Host "   2. Relevar los pods de la API: el Secret se lee al arrancar."
Write-Host "   3. Verificar /health/ready: el check de adjuntos dice «S3: s3://<bucket>/<prefijo>»."
Write-Host ""
