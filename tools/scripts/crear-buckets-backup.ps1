# =============================================================================
# crear-buckets-backup.ps1 - Prepara en AWS la infraestructura de respaldos del
# ERP sobre un bucket S3 YA EXISTENTE y compartido con otra aplicacion, y deja
# la credencial instalada en los tres clusteres.
#
# CONTEXTO: se reutiliza `polly-carteravirtual-pdn`, que ya guarda archivos de
# otra aplicacion. Todo lo del ERP vive bajo el prefijo `ingenia365-erp/`.
#
# 🚨 DOS LIMITES QUE TRAE REUTILIZAR UN BUCKET EXISTENTE
#
#   1. Object Lock NO se puede habilitar despues de crear el bucket (exige caso
#      con AWS Support). Sin el, quien tenga las credenciales puede borrar los
#      respaldos: justo el escenario de ransomware que el diseno cubria. Por eso
#      el sellado regulatorio mensual sigue apuntando a un bucket NUEVO y
#      pequeno (~11 GB en 5 anos), que es la unica parte que no admite arreglo
#      retroactivo.
#
#   2. Las reglas de ciclo de vida son POR BUCKET y `put-bucket-lifecycle-
#      configuration` REEMPLAZA la configuracion completa. Aplicarlas a ciegas
#      borraria las reglas de la otra aplicacion. Este script LEE las existentes
#      y las conserva; solo agrega o actualiza las suyas, todas con filtro de
#      prefijo.
#
# QUE NO TOCA (a proposito, por ser un bucket compartido): versionado, cifrado
# por defecto y bloqueo de acceso publico. Cambiarlos afectaria a la otra
# aplicacion. Solo se informa su estado.
#
# SEGURIDAD: la llave secreta nunca se imprime. Se genera, viaja por STDIN sobre
# SSH a los tres clusteres y se borra de memoria. Solo se muestra el Access Key
# ID, que por si solo no sirve para nada.
#
# REQUISITOS: AWS CLI ya configurado. Este script NO pide ni maneja tus
# credenciales de AWS: usa las que ya tenes.
#
# Uso:
#   .\tools\scripts\crear-buckets-backup.ps1 -SoloVerificar   # diagnostico
#   .\tools\scripts\crear-buckets-backup.ps1                  # aplica
# =============================================================================
param(
    [string]$Bucket     = 'polly-carteravirtual-pdn',
    [string]$Prefijo    = 'ingenia365-erp',
    [string]$Region     = 'us-east-1',
    [string]$UsuarioIam = 'ingenia365-erp-backup',
    [string]$KeyPath    = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    [switch]$SoloVerificar
)

$ErrorActionPreference = 'Stop'

function Paso($n, $t) { Write-Host ""; Write-Host "  [$n] $t" -ForegroundColor Cyan }
function Ok($t)       { Write-Host "      OK  $t" -ForegroundColor Green }
function Nota($t)     { Write-Host "      --  $t" -ForegroundColor DarkGray }
function Aviso($t)    { Write-Host "      !!  $t" -ForegroundColor Yellow }
function Alerta($t)   { Write-Host "      XX  $t" -ForegroundColor Red }

# --- 0. Preflight ------------------------------------------------------------
Paso 0 "Comprobaciones previas"

if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw "AWS CLI no esta instalado o no esta en el PATH. https://aws.amazon.com/cli/"
}
$identidad = aws sts get-caller-identity --output json | ConvertFrom-Json
Ok "AWS CLI autenticado como $($identidad.Arn)"

try { aws s3api head-bucket --bucket $Bucket 2>$null | Out-Null }
catch { throw "El bucket '$Bucket' no existe o no tenes acceso con estas credenciales." }
Ok "Bucket '$Bucket' accesible"

# --- 1. Diagnostico del bucket compartido ------------------------------------
Paso 1 "Estado del bucket (compartido con otra aplicacion)"

$lockOk = $false
try {
    $lock = aws s3api get-object-lock-configuration --bucket $Bucket --output json 2>$null | ConvertFrom-Json
    if ($lock.ObjectLockConfiguration.ObjectLockEnabled -eq 'Enabled') { $lockOk = $true }
} catch { }

if ($lockOk) {
    Ok "Object Lock HABILITADO - la inmutabilidad si es posible en este bucket"
} else {
    Alerta "Object Lock NO habilitado, y no puede agregarse a un bucket existente"
    Nota "Consecuencia: quien tenga las credenciales puede borrar los respaldos."
    Nota "El sellado regulatorio de 5 anos requiere un bucket nuevo (ver paso 4)."
}

$ver = aws s3api get-bucket-versioning --bucket $Bucket --output json | ConvertFrom-Json
if ($ver.Status -eq 'Enabled') { Ok "Versionado activo" }
else { Aviso "Versionado NO activo. No se cambia: es un bucket compartido." }

try {
    aws s3api get-bucket-encryption --bucket $Bucket --output json 2>$null | Out-Null
    Ok "Cifrado en reposo por defecto configurado"
} catch { Aviso "Sin cifrado por defecto. Los respaldos igual se suben con --sse AES256." }

$objetos = aws s3api list-objects-v2 --bucket $Bucket --prefix "$Prefijo/" --max-items 1 --output json 2>$null | ConvertFrom-Json
if ($objetos.Contents) { Nota "Ya hay objetos bajo '$Prefijo/'" }
else { Nota "El prefijo '$Prefijo/' esta vacio (primera instalacion)" }

if ($SoloVerificar) {
    Paso "V" "Reglas de ciclo de vida existentes (no se modifica nada)"
    try {
        $lc = aws s3api get-bucket-lifecycle-configuration --bucket $Bucket --output json 2>$null | ConvertFrom-Json
        foreach ($r in $lc.Rules) {
            $pref = if ($r.Filter.Prefix) { $r.Filter.Prefix } else { '(TODO EL BUCKET)' }
            Nota "$($r.ID) [$($r.Status)] prefijo=$pref"
        }
    } catch { Nota "El bucket no tiene reglas de ciclo de vida" }
    Write-Host ""
    return
}

# --- 2. Ciclo de vida: agregar sin destruir lo ajeno --------------------------
Paso 2 "Reglas de ciclo de vida (conservando las existentes)"

$reglasExistentes = @()
try {
    $lc = aws s3api get-bucket-lifecycle-configuration --bucket $Bucket --output json 2>$null | ConvertFrom-Json
    $reglasExistentes = @($lc.Rules)
    Nota "$($reglasExistentes.Count) regla(s) preexistente(s) detectada(s)"
} catch { Nota "El bucket no tenia reglas de ciclo de vida" }

# Nuestras reglas van con un ID reconocible y SIEMPRE con filtro de prefijo.
# Sin el filtro, `NoncurrentVersionExpiration` borraria el historial de
# versiones de la otra aplicacion.
$nuestras = @(
    @{ ID = "ingenia365-abortar-multipart"; Status = "Enabled"
       Filter = @{ Prefix = "$Prefijo/" }
       AbortIncompleteMultipartUpload = @{ DaysAfterInitiation = 7 } },
    @{ ID = "ingenia365-versiones-no-actuales"; Status = "Enabled"
       Filter = @{ Prefix = "$Prefijo/" }
       NoncurrentVersionExpiration = @{ NoncurrentDays = 1 } },
    @{ ID = "ingenia365-mongo-diario"; Status = "Enabled"
       Filter = @{ Prefix = "$Prefijo/mongo/diario/" }
       Expiration = @{ Days = 35 } },
    @{ ID = "ingenia365-mongo-dev"; Status = "Enabled"
       Filter = @{ Prefix = "$Prefijo/mongo/dev/" }
       Expiration = @{ Days = 7 } }
)

# Se descartan versiones anteriores de NUESTRAS reglas (idempotencia) y se
# conservan intactas todas las demas.
$idsNuestros = $nuestras | ForEach-Object { $_.ID }
$conservadas = @($reglasExistentes | Where-Object { $idsNuestros -notcontains $_.ID })
foreach ($r in $conservadas) { Nota "se conserva: $($r.ID)" }

# OJO: ninguna regla nuestra expira los respaldos de PostgreSQL. Barman es la
# UNICA autoridad de borrado sobre ellos. Si S3 expirara tarballs y dejara el
# catalogo, `barman-cloud-backup-list` seguiria reportando el respaldo como
# valido y el fallo se descubriria durante una restauracion, en plena crisis.
$config = @{ Rules = @($conservadas + $nuestras) } | ConvertTo-Json -Depth 10
$tmp = New-TemporaryFile
Set-Content -Path $tmp -Value $config -Encoding utf8
aws s3api put-bucket-lifecycle-configuration --bucket $Bucket --lifecycle-configuration "file://$tmp" | Out-Null
Remove-Item $tmp -Force
Ok "$($nuestras.Count) regla(s) del ERP aplicadas, $($conservadas.Count) ajena(s) conservada(s)"

# --- 3. Usuario IAM con permisos acotados al prefijo -------------------------
Paso 3 "Usuario IAM: $UsuarioIam"

$existeUsr = $true
try { aws iam get-user --user-name $UsuarioIam 2>$null | Out-Null } catch { $existeUsr = $false }
if (-not $existeUsr) { aws iam create-user --user-name $UsuarioIam | Out-Null; Ok "Usuario creado" }
else { Aviso "El usuario ya existe" }

# Acotado al prefijo del ERP: estas credenciales viven dentro de los clusteres y
# NO deben poder leer ni tocar los archivos de la otra aplicacion.
# La denegacion de BypassGovernanceRetention es preventiva: si algun dia el
# bucket gana Object Lock, estas llaves no podran saltarselo.
$politica = @"
{"Version":"2012-10-17","Statement":[
 {"Sid":"ListarSoloNuestroPrefijo","Effect":"Allow",
  "Action":["s3:ListBucket","s3:ListBucketMultipartUploads"],
  "Resource":"arn:aws:s3:::$Bucket",
  "Condition":{"StringLike":{"s3:prefix":["$Prefijo/*","$Prefijo/"]}}},
 {"Sid":"UbicacionDelBucket","Effect":"Allow",
  "Action":["s3:GetBucketLocation"],"Resource":"arn:aws:s3:::$Bucket"},
 {"Sid":"ObjetosSoloBajoNuestroPrefijo","Effect":"Allow",
  "Action":["s3:PutObject","s3:GetObject","s3:DeleteObject","s3:AbortMultipartUpload","s3:ListMultipartUploadParts"],
  "Resource":"arn:aws:s3:::$Bucket/$Prefijo/*"},
 {"Sid":"NuncaSaltarseElBloqueo","Effect":"Deny",
  "Action":["s3:BypassGovernanceRetention","s3:PutBucketObjectLockConfiguration","s3:PutBucketVersioning","s3:PutBucketLifecycleConfiguration","s3:DeleteBucket"],
  "Resource":"*"}
]}
"@
$tmpPol = New-TemporaryFile
Set-Content -Path $tmpPol -Value $politica -Encoding utf8
aws iam put-user-policy --user-name $UsuarioIam --policy-name "ingenia365-erp-backup" --policy-document "file://$tmpPol" | Out-Null
Remove-Item $tmpPol -Force
Ok "Permisos acotados a $Bucket/$Prefijo/ - sin acceso al resto del bucket"
Nota "Si barman-cloud-backup-list fallara con AccessDenied, quitar la condicion"
Nota "de prefijo en ListarSoloNuestroPrefijo es el ajuste (documentado a proposito)."

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
Write-Host "  Listo. Falta el ensayo real: un respaldo que nunca se probo" -ForegroundColor Cyan
Write-Host "  no es un respaldo, es una intencion." -ForegroundColor Cyan
Write-Host ""
