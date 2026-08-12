# =============================================================================
# crear-buckets-backup.ps1 - Crea y configura en AWS el bucket de respaldos del
# ERP, el usuario IAM con permisos minimos, y deja la credencial instalada en
# los tres clusteres.
#
# 🚨 LO IRREVERSIBLE: Object Lock SOLO puede habilitarse AL CREAR el bucket. En
# uno existente exige abrir caso con AWS Support. Por eso este script crea un
# bucket dedicado: `polly-carteravirtual-pdn` no lo tiene y ya no puede tenerlo.
# Habilitarlo no obliga a bloquear nada; NO habilitarlo si es sin retorno.
#
# UN SOLO BUCKET alcanza: con Object Lock habilitado, el sellado regulatorio
# mensual aplica COMPLIANCE objeto por objeto mientras el resto del bucket usa
# el GOVERNANCE de 40 dias por defecto.
#
#   ingenia365-erp/pg/{pdn,dev,qa}   PostgreSQL (Barman)   GOVERNANCE 40d
#   ingenia365-erp/mongo/diario/     auditoria diaria      GOVERNANCE 40d
#   ingenia365-erp/audit/<mes>/      sellado regulatorio   COMPLIANCE 5 anos
#
# SEGURIDAD: la llave secreta nunca se imprime. Se genera, viaja por STDIN sobre
# SSH a los tres clusteres y se borra de memoria. Solo se muestra el Access Key
# ID, que por si solo no sirve para nada.
#
# REQUISITOS: AWS CLI ya configurado. Este script NO pide ni maneja tus
# credenciales de AWS: usa el perfil que le indiques.
#
# Uso:
#   .\tools\scripts\crear-buckets-backup.ps1 -Perfil ingenia365 -SoloVerificar
#   .\tools\scripts\crear-buckets-backup.ps1 -Perfil ingenia365
# =============================================================================
param(
    [string]$Bucket     = 'ingenia365-erp-backups',
    [string]$Prefijo    = 'ingenia365-erp',
    [string]$Region     = 'us-east-1',
    [string]$UsuarioIam = 'ingenia365-erp-backup',
    [string]$KeyPath    = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    # Perfil del AWS CLI. Sin esto se usa el perfil por defecto, que puede
    # apuntar a OTRA cuenta de AWS.
    [string]$Perfil     = '',
    [switch]$SoloVerificar
)

$ErrorActionPreference = 'Stop'
if ($Perfil) { $env:AWS_PROFILE = $Perfil }

function Paso($n, $t) { Write-Host ""; Write-Host "  [$n] $t" -ForegroundColor Cyan }
function Ok($t)       { Write-Host "      OK  $t" -ForegroundColor Green }
function Nota($t)     { Write-Host "      --  $t" -ForegroundColor DarkGray }
function Aviso($t)    { Write-Host "      !!  $t" -ForegroundColor Yellow }

# --- 0. Preflight ------------------------------------------------------------
Paso 0 "Comprobaciones previas"

if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw "AWS CLI no esta instalado o no esta en el PATH. https://aws.amazon.com/cli/"
}
$identidad = aws sts get-caller-identity --output json | ConvertFrom-Json
$perfilUsado = if ($Perfil) { $Perfil } else { '(por defecto)' }
Ok "AWS CLI autenticado como $($identidad.Arn)"
Nota "Cuenta $($identidad.Account) - perfil $perfilUsado - region $Region"

$existe = $true
try { aws s3api head-bucket --bucket $Bucket 2>$null | Out-Null } catch { $existe = $false }

if ($SoloVerificar) {
    Paso "V" "Estado (no se crea ni modifica nada)"
    if (-not $existe) { Nota "El bucket '$Bucket' no existe todavia"; Write-Host ""; return }
    try {
        $lock = aws s3api get-object-lock-configuration --bucket $Bucket --output json 2>$null | ConvertFrom-Json
        $r = $lock.ObjectLockConfiguration.Rule.DefaultRetention
        Ok "Object Lock: $($lock.ObjectLockConfiguration.ObjectLockEnabled) - $($r.Mode) $($r.Days)d"
    } catch { Aviso "Sin Object Lock" }
    $ver = aws s3api get-bucket-versioning --bucket $Bucket --output json | ConvertFrom-Json
    if ($ver.Status -eq 'Enabled') { Ok "Versionado activo" } else { Aviso "Versionado inactivo" }
    Write-Host ""
    return
}

# --- 1. Bucket ---------------------------------------------------------------
Paso 1 "Bucket de respaldos: $Bucket"

if ($existe) {
    Aviso "Ya existe: no se recrea"
    $tieneLock = $false
    try {
        $lock = aws s3api get-object-lock-configuration --bucket $Bucket --output json 2>$null | ConvertFrom-Json
        if ($lock.ObjectLockConfiguration.ObjectLockEnabled -eq 'Enabled') { $tieneLock = $true }
    } catch { }
    if (-not $tieneLock) {
        throw @"
El bucket '$Bucket' existe pero NO tiene Object Lock, y no puede agregarse.
Ese es justamente el motivo de crear un bucket dedicado.

Elegi otro nombre (deben ser unicos en todo AWS) y volve a ejecutar:
  .\tools\scripts\crear-buckets-backup.ps1 -Perfil $Perfil -Bucket ingenia365-erp-backups-co
"@
    }
} else {
    # us-east-1 es la unica region que NO admite LocationConstraint.
    try {
        if ($Region -eq 'us-east-1') {
            aws s3api create-bucket --bucket $Bucket --object-lock-enabled-for-bucket | Out-Null
        } else {
            aws s3api create-bucket --bucket $Bucket --region $Region `
                --create-bucket-configuration "LocationConstraint=$Region" `
                --object-lock-enabled-for-bucket | Out-Null
        }
    } catch {
        throw @"
No se pudo crear '$Bucket'. Causa habitual: el nombre ya esta tomado por otra
cuenta (los nombres de bucket son unicos en TODO AWS, no por cuenta).

Proba con otro y volve a ejecutar:
  .\tools\scripts\crear-buckets-backup.ps1 -Perfil $Perfil -Bucket ingenia365-erp-backups-co

Detalle: $($_.Exception.Message)
"@
    }
    Ok "Creado CON Object Lock habilitado (irreversible: era ahora o nunca)"
}

# El versionado es requisito de Object Lock. Ademas es lo que permite que Barman
# borre por retencion sin pelearse con el bloqueo: un DELETE crea un marcador y
# la version bloqueada se retira sola cuando vence.
aws s3api put-bucket-versioning --bucket $Bucket --versioning-configuration Status=Enabled | Out-Null
Ok "Versionado activo"

aws s3api put-public-access-block --bucket $Bucket `
    --public-access-block-configuration `
    "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true" | Out-Null
Ok "Acceso publico bloqueado"

$cifrado = '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'
$tmpEnc = New-TemporaryFile
Set-Content -Path $tmpEnc -Value $cifrado -Encoding utf8
aws s3api put-bucket-encryption --bucket $Bucket --server-side-encryption-configuration "file://$tmpEnc" | Out-Null
Remove-Item $tmpEnc -Force
Ok "Cifrado en reposo por defecto"

# GOVERNANCE y no COMPLIANCE por defecto: cubre el 99% de los casos reales
# (ransomware con las credenciales del pod, borrado equivocado, bug de
# retencion) conservando una salida de emergencia con MFA. COMPLIANCE no admite
# marcha atras ni para el dueno de la cuenta, y se reserva al sellado mensual,
# donde esa ausencia de salida ES el objetivo.
# 40 dias = ventana de Barman (35d) + 5 de holgura.
$lockCfg = '{"ObjectLockEnabled":"Enabled","Rule":{"DefaultRetention":{"Mode":"GOVERNANCE","Days":40}}}'
$tmpLock = New-TemporaryFile
Set-Content -Path $tmpLock -Value $lockCfg -Encoding utf8
aws s3api put-object-lock-configuration --bucket $Bucket --object-lock-configuration "file://$tmpLock" | Out-Null
Remove-Item $tmpLock -Force
Ok "Object Lock GOVERNANCE 40 dias por defecto"

# --- 2. Ciclo de vida --------------------------------------------------------
Paso 2 "Reglas de ciclo de vida"

# Notese lo que NO esta: ninguna expiracion sobre los respaldos de PostgreSQL.
# Barman es la UNICA autoridad de borrado sobre ellos. Si S3 expirara tarballs y
# dejara el catalogo, `barman-cloud-backup-list` seguiria reportando el respaldo
# como valido y el fallo se descubriria durante una restauracion, en plena
# crisis. Tampoco hay transiciones a Glacier: un WAL de 40 KB se factura como
# 128 KB en IA y un PITR pasaria de minutos a horas.
$lifecycle = @"
{"Rules":[
 {"ID":"abortar-multipart-huerfanos","Status":"Enabled","Filter":{},
  "AbortIncompleteMultipartUpload":{"DaysAfterInitiation":7}},
 {"ID":"expirar-versiones-no-actuales","Status":"Enabled","Filter":{},
  "NoncurrentVersionExpiration":{"NoncurrentDays":1}},
 {"ID":"limpiar-delete-markers","Status":"Enabled","Filter":{},
  "Expiration":{"ExpiredObjectDeleteMarker":true}},
 {"ID":"expirar-dumps-mongo-diarios","Status":"Enabled",
  "Filter":{"Prefix":"$Prefijo/mongo/diario/"},"Expiration":{"Days":35}},
 {"ID":"expirar-dumps-mongo-dev","Status":"Enabled",
  "Filter":{"Prefix":"$Prefijo/mongo/dev/"},"Expiration":{"Days":7}}
]}
"@
$tmpLc = New-TemporaryFile
Set-Content -Path $tmpLc -Value $lifecycle -Encoding utf8
aws s3api put-bucket-lifecycle-configuration --bucket $Bucket --lifecycle-configuration "file://$tmpLc" | Out-Null
Remove-Item $tmpLc -Force
Ok "Limpieza de basura invisible, sin expirar respaldos vivos"
Nota "El sellado mensual queda protegido: Object Lock gana sobre el ciclo de vida"

# --- 3. Usuario IAM ----------------------------------------------------------
Paso 3 "Usuario IAM: $UsuarioIam"

$existeUsr = $true
try { aws iam get-user --user-name $UsuarioIam 2>$null | Out-Null } catch { $existeUsr = $false }
if (-not $existeUsr) { aws iam create-user --user-name $UsuarioIam | Out-Null; Ok "Usuario creado" }
else { Aviso "El usuario ya existe" }

# La denegacion de BypassGovernanceRetention es el corazon de esto: si estas
# credenciales se filtran, NO pueden saltarse el bloqueo ni borrar los respaldos
# antes de tiempo. La salida de emergencia vive en un rol aparte, fuera del
# cluster y con MFA.
$politica = @"
{"Version":"2012-10-17","Statement":[
 {"Sid":"Bucket","Effect":"Allow",
  "Action":["s3:ListBucket","s3:GetBucketLocation","s3:ListBucketMultipartUploads","s3:ListBucketVersions"],
  "Resource":"arn:aws:s3:::$Bucket"},
 {"Sid":"Objetos","Effect":"Allow",
  "Action":["s3:PutObject","s3:GetObject","s3:DeleteObject","s3:AbortMultipartUpload",
            "s3:ListMultipartUploadParts","s3:PutObjectRetention","s3:GetObjectRetention"],
  "Resource":"arn:aws:s3:::$Bucket/*"},
 {"Sid":"NuncaSaltarseElBloqueo","Effect":"Deny",
  "Action":["s3:BypassGovernanceRetention","s3:PutBucketObjectLockConfiguration",
            "s3:PutBucketVersioning","s3:PutBucketLifecycleConfiguration","s3:DeleteBucket",
            "s3:DeleteObjectVersion"],
  "Resource":"*"}
]}
"@
$tmpPol = New-TemporaryFile
Set-Content -Path $tmpPol -Value $politica -Encoding utf8
aws iam put-user-policy --user-name $UsuarioIam --policy-name "ingenia365-erp-backup" --policy-document "file://$tmpPol" | Out-Null
Remove-Item $tmpPol -Force
Ok "Permisos minimos: sin poder saltarse el bloqueo ni borrar versiones"

# --- 4. Llave de acceso, directo al cluster ----------------------------------
Paso 4 "Llave de acceso e instalacion en los clusteres"

$previas = (aws iam list-access-keys --user-name $UsuarioIam --output json | ConvertFrom-Json).AccessKeyMetadata
if ($previas.Count -ge 2) {
    throw "El usuario ya tiene 2 llaves (maximo de AWS). Borra una: aws iam delete-access-key --user-name $UsuarioIam --access-key-id <ID>"
}

$llave = (aws iam create-access-key --user-name $UsuarioIam --output json | ConvertFrom-Json).AccessKey
Ok "Llave creada: $($llave.AccessKeyId)"
Nota "La llave secreta no se muestra: va directo al cluster por SSH"

Start-Sleep -Seconds 10   # AWS tarda unos segundos en propagar una llave nueva

$b64Key    = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($llave.AccessKeyId))
$b64Secret = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($llave.SecretAccessKey))
$b64Region = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Region))

$destinos = @(
    @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' },
    @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV'        },
    @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA'         }
)

foreach ($d in $destinos) {
    $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: s3-backup-creds
  namespace: $($d.Namespace)
  labels:
    cnpg.io/reload: ""
type: Opaque
data:
  ACCESS_KEY_ID: $b64Key
  ACCESS_SECRET_KEY: $b64Secret
  AWS_REGION: $b64Region
"@
    Write-Host ("      {0,-12} ({1}) ... " -f $d.Nombre, $d.Namespace) -NoNewline
    $salida = $yaml | ssh -i $KeyPath -o BatchMode=yes "root@$($d.Host)" "k3s kubectl apply -f -" 2>&1
    if ($LASTEXITCODE -eq 0) { Write-Host "OK" -ForegroundColor Green }
    else { Write-Host "FALLO" -ForegroundColor Red; Write-Host "         $salida" -ForegroundColor DarkRed }
}

$llave = $null; $b64Secret = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Listo. Bucket: $Bucket" -ForegroundColor Cyan
Write-Host "  Falta el ensayo real: un respaldo que nunca se probo no es un" -ForegroundColor Cyan
Write-Host "  respaldo, es una intencion." -ForegroundColor Cyan
Write-Host ""
