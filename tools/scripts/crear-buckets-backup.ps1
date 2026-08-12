# =============================================================================
# crear-buckets-backup.ps1 - Crea y configura en AWS el bucket de respaldos del
# ERP y, si hay permisos, el usuario IAM con su llave instalada en los clusteres.
#
# 🚨 LO IRREVERSIBLE: Object Lock SOLO puede habilitarse AL CREAR el bucket. En
# uno existente exige abrir caso con AWS Support. Habilitarlo no obliga a
# bloquear nada; NO habilitarlo si es sin retorno.
#
# UN SOLO BUCKET alcanza: con Object Lock habilitado, el sellado regulatorio
# mensual aplica COMPLIANCE objeto por objeto mientras el resto del bucket usa
# el GOVERNANCE de 40 dias por defecto.
#
#   ingenia365-erp/pg/{pdn,dev,qa}   PostgreSQL (Barman)   GOVERNANCE 40d
#   ingenia365-erp/mongo/diario/     auditoria diaria      GOVERNANCE 40d
#   ingenia365-erp/audit/<mes>/      sellado regulatorio   COMPLIANCE 5 anos
#
# ES RE-EJECUTABLE: si el bucket ya existe, completa lo que falte sin recrearlo.
#
# SEGURIDAD: la llave secreta nunca se imprime. Se genera, viaja por STDIN sobre
# SSH a los tres clusteres y se borra de memoria.
#
# Uso:
#   .\tools\scripts\crear-buckets-backup.ps1 -Perfil ingenia365 -SoloVerificar
#   .\tools\scripts\crear-buckets-backup.ps1 -Perfil ingenia365
#   .\tools\scripts\crear-buckets-backup.ps1 -Perfil ingenia365 -OmitirIam
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
    # Salta la parte de IAM: util cuando el usuario no tiene permisos para
    # crear usuarios y la credencial se genera a mano desde la consola.
    [switch]$OmitirIam,
    [switch]$SoloVerificar
)

$ErrorActionPreference = 'Stop'
if ($Perfil) { $env:AWS_PROFILE = $Perfil }

function Paso($n, $t) { Write-Host ""; Write-Host "  [$n] $t" -ForegroundColor Cyan }
function Ok($t)       { Write-Host "      OK  $t" -ForegroundColor Green }
function Nota($t)     { Write-Host "      --  $t" -ForegroundColor DarkGray }
function Aviso($t)    { Write-Host "      !!  $t" -ForegroundColor Yellow }

# Ruta del ejecutable, resuelta una sola vez. Invocarlo por ruta y no por nombre
# es lo que evita que las funciones de abajo se llamen a si mismas: PowerShell no
# distingue mayusculas en los nombres de funcion, asi que una funcion llamada
# `Aws` intercepta la invocacion de `aws` y recursa hasta CallDepthOverflow.
$script:AwsExe = (Get-Command aws -CommandType Application | Select-Object -First 1).Source

# $ErrorActionPreference NO alcanza para comandos nativos: si `aws` falla, la
# ejecucion continua igual. Sin este envoltorio el script imprimia "OK" justo
# despues de cada error y reportaba como configurado un bucket que no lo estaba.
function EjecutarAws {
    $salida = & $script:AwsExe @args
    if ($LASTEXITCODE -ne 0) { throw "Fallo el comando: aws $($args -join ' ')" }
    return $salida
}

# Igual, pero sin abortar: para comprobaciones donde el fallo es una respuesta
# valida ("no existe", "no configurado").
function IntentarAws {
    $salida = & $script:AwsExe @args 2>$null
    return @{ Ok = ($LASTEXITCODE -eq 0); Salida = $salida }
}

# Windows PowerShell 5.1 escribe UTF-8 CON BOM y el AWS CLI rechaza el archivo
# con "Expected: '=', received: 'i'". Hay que escribirlo sin BOM explicitamente.
function JsonTemporal($contenido) {
    $ruta = [IO.Path]::GetTempFileName()
    [IO.File]::WriteAllText($ruta, $contenido, (New-Object Text.UTF8Encoding($false)))
    return $ruta
}

# --- 0. Preflight ------------------------------------------------------------
Paso 0 "Comprobaciones previas"

if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw "AWS CLI no esta instalado o no esta en el PATH. https://aws.amazon.com/cli/"
}
$identidad = EjecutarAws sts get-caller-identity --output json | ConvertFrom-Json
$perfilUsado = if ($Perfil) { $Perfil } else { '(por defecto)' }
Ok "AWS CLI autenticado como $($identidad.Arn)"
Nota "Cuenta $($identidad.Account) - perfil $perfilUsado - region $Region"

$existe = (IntentarAws s3api head-bucket --bucket $Bucket).Ok

# --- Verificacion ------------------------------------------------------------
if ($SoloVerificar) {
    Paso "V" "Estado (no se crea ni modifica nada)"
    if (-not $existe) { Nota "El bucket '$Bucket' no existe todavia"; Write-Host ""; return }

    $r = IntentarAws s3api get-object-lock-configuration --bucket $Bucket --output json
    if ($r.Ok) {
        $lock = $r.Salida | ConvertFrom-Json
        $ret = $lock.ObjectLockConfiguration.Rule.DefaultRetention
        if ($ret) { Ok "Object Lock habilitado - retencion por defecto $($ret.Mode) $($ret.Days)d" }
        else      { Aviso "Object Lock habilitado pero SIN retencion por defecto" }
    } else { Aviso "Sin Object Lock" }

    $ver = (EjecutarAws s3api get-bucket-versioning --bucket $Bucket --output json) | ConvertFrom-Json
    if ($ver.Status -eq 'Enabled') { Ok "Versionado activo" } else { Aviso "Versionado inactivo" }

    if ((IntentarAws s3api get-bucket-encryption --bucket $Bucket).Ok) { Ok "Cifrado por defecto" }
    else { Aviso "SIN cifrado por defecto" }

    $lc = IntentarAws s3api get-bucket-lifecycle-configuration --bucket $Bucket --output json
    if ($lc.Ok) {
        $reglas = ($lc.Salida | ConvertFrom-Json).Rules
        Ok "$($reglas.Count) regla(s) de ciclo de vida"
        foreach ($x in $reglas) { Nota "  $($x.ID)" }
    } else { Aviso "SIN reglas de ciclo de vida" }

    if ((IntentarAws iam get-user --user-name $UsuarioIam).Ok) { Ok "Usuario IAM '$UsuarioIam' existe" }
    else { Aviso "Usuario IAM '$UsuarioIam' NO existe" }

    Write-Host ""
    return
}

# --- 1. Bucket ---------------------------------------------------------------
Paso 1 "Bucket de respaldos: $Bucket"

if ($existe) {
    Nota "Ya existe: se completa lo que falte"
    $r = IntentarAws s3api get-object-lock-configuration --bucket $Bucket --output json
    $tieneLock = $r.Ok -and (($r.Salida | ConvertFrom-Json).ObjectLockConfiguration.ObjectLockEnabled -eq 'Enabled')
    if (-not $tieneLock) {
        throw @"
El bucket '$Bucket' existe pero NO tiene Object Lock, y no puede agregarse.
Ese es justamente el motivo de usar un bucket dedicado. Elegi otro nombre:
  .\tools\scripts\crear-buckets-backup.ps1 -Perfil $Perfil -Bucket otro-nombre
"@
    }
    Ok "Object Lock habilitado"
} else {
    # us-east-1 es la unica region que NO admite LocationConstraint.
    if ($Region -eq 'us-east-1') {
        EjecutarAws s3api create-bucket --bucket $Bucket --object-lock-enabled-for-bucket | Out-Null
    } else {
        EjecutarAws s3api create-bucket --bucket $Bucket --region $Region `
            --create-bucket-configuration "LocationConstraint=$Region" `
            --object-lock-enabled-for-bucket | Out-Null
    }
    Ok "Creado CON Object Lock habilitado (irreversible: era ahora o nunca)"
}

# El versionado es requisito de Object Lock. Ademas permite que Barman borre por
# retencion sin chocar con el bloqueo: el DELETE crea un marcador y la version
# bloqueada se retira sola al vencer.
EjecutarAws s3api put-bucket-versioning --bucket $Bucket --versioning-configuration Status=Enabled | Out-Null
Ok "Versionado activo"

EjecutarAws s3api put-public-access-block --bucket $Bucket `
    --public-access-block-configuration `
    "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true" | Out-Null
Ok "Acceso publico bloqueado"

$tmp = JsonTemporal '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'
EjecutarAws s3api put-bucket-encryption --bucket $Bucket --server-side-encryption-configuration "file://$tmp" | Out-Null
Remove-Item $tmp -Force
Ok "Cifrado en reposo por defecto"

# GOVERNANCE y no COMPLIANCE por defecto: cubre el 99% de los casos reales
# (ransomware con las credenciales del pod, borrado equivocado, bug de
# retencion) conservando una salida de emergencia con MFA. COMPLIANCE no admite
# marcha atras ni para el dueno de la cuenta, y se reserva al sellado mensual,
# donde esa ausencia de salida ES el objetivo.
# 40 dias = ventana de Barman (35d) + 5 de holgura.
$tmp = JsonTemporal '{"ObjectLockEnabled":"Enabled","Rule":{"DefaultRetention":{"Mode":"GOVERNANCE","Days":40}}}'
EjecutarAws s3api put-object-lock-configuration --bucket $Bucket --object-lock-configuration "file://$tmp" | Out-Null
Remove-Item $tmp -Force
Ok "Retencion por defecto GOVERNANCE 40 dias"

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
$tmp = JsonTemporal $lifecycle
EjecutarAws s3api put-bucket-lifecycle-configuration --bucket $Bucket --lifecycle-configuration "file://$tmp" | Out-Null
Remove-Item $tmp -Force
Ok "5 reglas aplicadas, sin expirar respaldos vivos"
Nota "El sellado mensual queda protegido: Object Lock gana sobre el ciclo de vida"

# --- 3. Usuario IAM ----------------------------------------------------------
if ($OmitirIam) {
    Paso 3 "Usuario IAM: omitido por -OmitirIam"
    Write-Host ""
    Write-Host "  Bucket listo: $Bucket" -ForegroundColor Cyan
    Write-Host "  Falta la credencial. Crea el usuario en la consola y ejecuta:" -ForegroundColor DarkGray
    Write-Host "    .\tools\scripts\crear-secreto-s3.ps1" -ForegroundColor DarkGray
    Write-Host ""
    return
}

Paso 3 "Usuario IAM: $UsuarioIam"

if (-not (IntentarAws iam get-user --user-name $UsuarioIam).Ok) {
    $r = IntentarAws iam create-user --user-name $UsuarioIam
    if (-not $r.Ok) {
        throw @"
Sin permisos de IAM: $($identidad.Arn) no puede crear usuarios.

El bucket YA quedo configurado; solo falta la credencial. Dos caminos:

  A) Crear el usuario a mano en la consola de AWS (IAM -> Users -> Create user,
     nombre: $UsuarioIam), adjuntarle esta politica en linea, generar una
     Access Key y ejecutar:
         .\tools\scripts\crear-secreto-s3.ps1

     La politica esta en docs/operaciones/politica-iam-respaldos.json

  B) Que un usuario con permisos de IAM ejecute este script.

Para no repetir lo ya hecho, volve a correrlo con -OmitirIam.
"@
    }
    Ok "Usuario creado"
} else {
    Nota "El usuario ya existe"
}

# La denegacion de BypassGovernanceRetention es el corazon de esto: si estas
# credenciales se filtran, NO pueden saltarse el bloqueo ni borrar respaldos
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
$tmp = JsonTemporal $politica
EjecutarAws iam put-user-policy --user-name $UsuarioIam --policy-name "ingenia365-erp-backup" --policy-document "file://$tmp" | Out-Null
Remove-Item $tmp -Force
Ok "Permisos minimos: sin poder saltarse el bloqueo ni borrar versiones"

# --- 4. Llave de acceso, directo al cluster ----------------------------------
Paso 4 "Llave de acceso e instalacion en los clusteres"

$previas = (EjecutarAws iam list-access-keys --user-name $UsuarioIam --output json | ConvertFrom-Json).AccessKeyMetadata
if ($previas.Count -ge 2) {
    throw "El usuario ya tiene 2 llaves (maximo de AWS). Borra una: aws iam delete-access-key --user-name $UsuarioIam --access-key-id <ID>"
}

$llave = (EjecutarAws iam create-access-key --user-name $UsuarioIam --output json | ConvertFrom-Json).AccessKey
if (-not $llave.SecretAccessKey) { throw "AWS no devolvio la llave secreta." }
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
