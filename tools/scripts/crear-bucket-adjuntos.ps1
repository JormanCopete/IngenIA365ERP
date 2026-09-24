# =============================================================================
# crear-bucket-adjuntos.ps1 - Crea en AWS el bucket donde el ERP guarda los
# adjuntos (soportes de comprobantes, planillas, archivos de dispersion) y, si
# hay permisos, el usuario IAM con su llave instalada en los tres clusteres.
#
# ES OTRO BUCKET que el de respaldos, a proposito: aquel tiene Object Lock a 40
# dias porque un respaldo no debe poder borrarse; un adjunto se borra cuando una
# persona con permiso lo borra desde la aplicacion. Mezclarlos obligaria a elegir
# una sola retencion para las dos cosas.
#
# NADA SE BORRA SOLO (feature 011). Borrar desde el ERP deja una marca de borrado
# y la version queda 90 dias en la papelera, de donde soporte la recupera; pasado
# ese plazo el ciclo de vida la purga, y es la unica purga automatica que existe:
# solo alcanza a lo que alguien ya borro. La credencial del ERP no puede borrar
# versiones. Recetas de recuperacion y de supresion definitiva (Habeas Data) en
# docs/operaciones/adjuntos-en-s3.md.
#
#   ingenia365-erp-attachments/{pdn,qa,dev}/<cooperativa>/<aaaa>/<mm>/<guid>.bin
#
# Lo que SI lleva: versionado (un borrado accidental se recupera), cifrado
# AES256 del lado del servidor, bloqueo total de acceso publico, politica que
# rechaza toda peticion sin TLS, y ciclo de vida que purga las versiones borradas
# a los 90 dias, las marcas de borrado que quedan sin version y los multipart a
# medias.
#
# ES RE-EJECUTABLE: si el bucket ya existe, completa lo que falte sin recrearlo.
#
# SEGURIDAD: la llave secreta nunca se imprime ni se escribe en un archivo. Se
# genera, viaja por STDIN sobre SSH a los clusteres y se borra de memoria.
#
# Corre igual en Windows PowerShell 5.1 y en PowerShell 7 (pwsh).
#
# Uso:
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloVerificar
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365 -OmitirIam
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloSecreto
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
    # Instala en los tres clusteres una llave creada A MANO en la consola de AWS. Es
    # el complemento de -OmitirIam: quien ejecuta no puede crear usuarios IAM, la
    # credencial la crea otro, y aqui solo se pide y se instala.
    [switch]$SoloSecreto,
    [switch]$SoloVerificar
)

$ErrorActionPreference = 'Stop'

$awsExtras = @()
if ($Perfil) { $awsExtras = @('--profile', $Perfil) }

# Windows PowerShell 5.1 convierte en error TERMINANTE cualquier linea que un
# ejecutable nativo escriba en stderr cuando se la redirige y ErrorActionPreference
# vale 'Stop', aunque el comando haya terminado en 0. aws escribe avisos por ahi, y
# head-bucket sobre un bucket que todavia no existe escribe el 404 que este guion
# necesita tolerar. Por eso la preferencia se baja alrededor de la llamada y se
# decide por $LASTEXITCODE, que es lo unico que dice de verdad si el comando fallo.
function Invoke-Nativo {
    param([string]$Programa, [string[]]$Argumentos, [string]$Entrada)
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($PSBoundParameters.ContainsKey('Entrada')) { $salida = $Entrada | & $Programa $Argumentos 2>&1 }
        else { $salida = & $Programa $Argumentos 2>&1 }
    } finally {
        $ErrorActionPreference = $previo
    }
    return ,@($salida | ForEach-Object { "$_" })
}

function Invoke-Aws {
    param([string[]]$Argumentos, [switch]$TolerarError)
    $salida = Invoke-Nativo -Programa 'aws' -Argumentos ($awsExtras + $Argumentos)
    if ($LASTEXITCODE -ne 0 -and -not $TolerarError) {
        throw ("aws {0} fallo: {1}" -f ($Argumentos -join ' '), ($salida -join "`n"))
    }
    return $salida
}

# Windows PowerShell 5.1 se come las comillas dobles al pasar un argumento a un
# ejecutable nativo, asi que un JSON en linea le llega a aws como {Rules:[...]} y lo
# rechaza por invalido. Se escribe a un archivo temporal -en %TEMP%, que no tiene
# espacios en la ruta- y se pasa por file://, que es ademas como aws espera recibir
# los documentos largos.
function Invoke-AwsConJson {
    param([string[]]$Argumentos, [string]$Json)
    $archivo = Join-Path ([IO.Path]::GetTempPath()) ("ingenia-" + [Guid]::NewGuid().ToString('N') + ".json")
    [IO.File]::WriteAllText($archivo, $Json, (New-Object Text.UTF8Encoding($false)))
    try { Invoke-Aws ($Argumentos + @("file://$archivo")) | Out-Null }
    finally { Remove-Item $archivo -Force -ErrorAction SilentlyContinue }
}

function ConvertFrom-Segura {
    param([Security.SecureString]$Segura)
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Segura)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

# Escribe y borra un objeto con LA llave que se va a instalar, no con la del operador:
# que el bucket exista no dice nada sobre si esa credencial puede escribir en el, y es
# exactamente lo que el health check de la API intenta al arrancar. Vale la pena
# descubrirlo aqui y no en /health/ready.
function Test-Llave {
    param([string]$IdLlave, [string]$Secreta)
    Write-Host "  Probando la llave contra el bucket (escribe y borra) ... " -NoNewline
    $previos = @{
        Id = $env:AWS_ACCESS_KEY_ID; Secreta = $env:AWS_SECRET_ACCESS_KEY
        Token = $env:AWS_SESSION_TOKEN; Perfil = $env:AWS_PROFILE
    }
    $archivo = Join-Path ([IO.Path]::GetTempPath()) ("ingenia-prueba-" + [Guid]::NewGuid().ToString('N') + ".bin")
    $clave = ".healthcheck/credencial-" + [Guid]::NewGuid().ToString('N') + ".bin"
    $env:AWS_ACCESS_KEY_ID = $IdLlave
    $env:AWS_SECRET_ACCESS_KEY = $Secreta
    $env:AWS_SESSION_TOKEN = $null
    $env:AWS_PROFILE = $null
    try {
        [IO.File]::WriteAllText($archivo, 'ok')
        $salida = Invoke-Nativo -Programa 'aws' -Argumentos @(
            's3api', 'put-object', '--bucket', $Bucket, '--key', $clave, '--body', $archivo, '--region', $Region)
        if ($LASTEXITCODE -ne 0) { throw ("la llave no puede escribir en el bucket: {0}" -f ($salida -join "`n")) }
        $salida = Invoke-Nativo -Programa 'aws' -Argumentos @(
            's3api', 'delete-object', '--bucket', $Bucket, '--key', $clave, '--region', $Region)
        if ($LASTEXITCODE -ne 0) { throw ("la llave escribe pero no borra: {0}" -f ($salida -join "`n")) }
    } finally {
        Remove-Item $archivo -Force -ErrorAction SilentlyContinue
        $env:AWS_ACCESS_KEY_ID     = $previos.Id
        $env:AWS_SECRET_ACCESS_KEY = $previos.Secreta
        $env:AWS_SESSION_TOKEN     = $previos.Token
        $env:AWS_PROFILE           = $previos.Perfil
    }
    Write-Host "OK" -ForegroundColor Green
}

function Install-Secret {
    param([string]$IdLlave, [string]$Secreta)
    $destinos = @(
        @{ Maquina = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' },
        @{ Maquina = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' },
        @{ Maquina = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' }
    )
    $b64Id      = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($IdLlave))
    $b64Secreta = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Secreta))
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
        $salida = Invoke-Nativo -Programa 'ssh' -Entrada $yaml -Argumentos @(
            '-i', $KeyPath, '-o', 'BatchMode=yes', "root@$($d.Maquina)", 'k3s kubectl apply -f -')
        if ($LASTEXITCODE -eq 0) { Write-Host "OK" -ForegroundColor Green }
        else { Write-Host "FALLO" -ForegroundColor Red; Write-Host ("    {0}" -f ($salida -join "`n")) -ForegroundColor Red }
    }
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
        Write-Host ("  Ciclo:      {0}" -f (Invoke-Aws @('s3api', 'get-bucket-lifecycle-configuration', '--bucket', $Bucket, '--query', 'Rules[].ID', '--output', 'text') -TolerarError))
        Write-Host ("  Politica:   {0}" -f (Invoke-Aws @('s3api', 'get-bucket-policy', '--bucket', $Bucket, '--query', 'Policy', '--output', 'text') -TolerarError))
        Write-Host ("  CORS:       {0}" -f (Invoke-Aws @('s3api', 'get-bucket-cors', '--bucket', $Bucket, '--query', 'CORSRules[].AllowedOrigins[]', '--output', 'text') -TolerarError))
    }
    return
}

if ($SoloSecreto) {
    if (-not $existe) { throw "El bucket $Bucket no existe todavia: corra el guion sin -SoloSecreto (o con -OmitirIam) primero." }
    Write-Host ""
    Write-Host ("  Llave del usuario {0}, creada a mano en la consola de AWS." -f $UsuarioIam) -ForegroundColor Cyan
    Write-Host "  No se imprime, no se guarda en ningun archivo y no queda en el historial." -ForegroundColor DarkGray
    Write-Host ""
    $idLlave = (Read-Host "  AccessKeyId").Trim()
    $secretaSegura = Read-Host "  SecretAccessKey" -AsSecureString
    if (-not $idLlave -or -not $secretaSegura -or $secretaSegura.Length -eq 0) { throw "Falta la llave." }
    $secreta = ConvertFrom-Segura $secretaSegura
    try {
        Write-Host ""
        Test-Llave -IdLlave $idLlave -Secreta $secreta
        Install-Secret -IdLlave $idLlave -Secreta $secreta
    } finally {
        $secreta = $null
        $secretaSegura.Dispose()
        [GC]::Collect()
    }
    Write-Host ""
    Write-Host "  Listo. Falta el overlay de GitOps y relevar los pods de la API." -ForegroundColor Cyan
    Write-Host ""
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
Invoke-AwsConJson @('s3api', 'put-bucket-encryption', '--bucket', $Bucket,
                    '--server-side-encryption-configuration') -Json $cifrado
Write-Host "OK" -ForegroundColor Green

# La papelera (contracts/almacen.md de la feature 011, seccion 1). Reemplaza la
# configuracion entera: la regla vieja se llamaba "limpieza" y no purgaba las
# marcas de borrado que quedan solas cuando su version ya se fue.
Write-Host "  Ciclo de vida (papelera de 90 d, marcas huerfanas, multipart a medias 7 d) ... " -NoNewline
$ciclo = '{"Rules":[{"ID":"papelera-90-dias","Status":"Enabled","Filter":{},' +
         '"NoncurrentVersionExpiration":{"NoncurrentDays":90},' +
         '"Expiration":{"ExpiredObjectDeleteMarker":true},' +
         '"AbortIncompleteMultipartUpload":{"DaysAfterInitiation":7}}]}'
Invoke-AwsConJson @('s3api', 'put-bucket-lifecycle-configuration', '--bucket', $Bucket,
                    '--lifecycle-configuration') -Json $ciclo
Write-Host "OK" -ForegroundColor Green

# Solo TLS. Es un Deny, asi que no choca con BlockPublicPolicy (que solo mira los
# Allow). put-bucket-policy REEMPLAZA la politica: este guion es su unica fuente, y
# si alguien le agrego algo a mano desde la consola, aqui se pierde.
Write-Host "  Politica del bucket (solo TLS) ... " -NoNewline
$politicaDelBucket = '{"Version":"2012-10-17","Statement":[{"Sid":"SoloTls","Effect":"Deny","Principal":"*",' +
                     '"Action":"s3:*","Resource":["arn:aws:s3:::' + $Bucket + '","arn:aws:s3:::' + $Bucket + '/*"],' +
                     '"Condition":{"Bool":{"aws:SecureTransport":"false"}}}]}'
Invoke-AwsConJson @('s3api', 'put-bucket-policy', '--bucket', $Bucket, '--policy') -Json $politicaDelBucket
Write-Host "OK" -ForegroundColor Green

# CORS (feature 011, research R14): el navegador sube directo al bucket con una autorizacion firmada, asi
# que el bucket tiene que aceptar el POST desde los origenes del ERP. Bajar no lo necesita: es navegacion.
# Los origenes de la app (BlazorWebView, https://0.0.0.1 y app://0.0.0.1) se agregan cuando la espiga T003
# los confirme en cada plataforma; hasta entonces la app pide subir los soportes desde la web.
Write-Host "  CORS (subidas directas desde la web del ERP) ... " -NoNewline
$cors = '{"CORSRules":[{"AllowedMethods":["POST"],' +
        '"AllowedOrigins":["https://app-dev.ingenia365.com","https://app-qa.ingenia365.com","https://app.ingenia365.com"],' +
        '"AllowedHeaders":["*"],"ExposeHeaders":["ETag"],"MaxAgeSeconds":3000}]}'
Invoke-AwsConJson @('s3api', 'put-bucket-cors', '--bucket', $Bucket, '--cors-configuration') -Json $cors
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
Invoke-AwsConJson @('iam', 'put-user-policy', '--user-name', $UsuarioIam,
                    '--policy-name', 'AdjuntosDelErp',
                    '--policy-document') -Json ([IO.File]::ReadAllText((Resolve-Path $politica).Path))
Write-Host "OK" -ForegroundColor Green

Write-Host ""
Write-Host "  Generando la llave de acceso (no se imprime) ... " -NoNewline
$llaveJson = Invoke-Aws @('iam', 'create-access-key', '--user-name', $UsuarioIam, '--output', 'json') | Out-String
$llave = $llaveJson | ConvertFrom-Json
$idLlave = $llave.AccessKey.AccessKeyId
$secreta = $llave.AccessKey.SecretAccessKey
Write-Host "OK" -ForegroundColor Green
Write-Host ("  AccessKeyId: {0}" -f $idLlave) -ForegroundColor DarkGray

Write-Host ""
try {
    Test-Llave -IdLlave $idLlave -Secreta $secreta
    Install-Secret -IdLlave $idLlave -Secreta $secreta
} finally {
    $secreta = $null
    [GC]::Collect()
}

Write-Host ""
Write-Host "  Listo. Falta:" -ForegroundColor Cyan
Write-Host "   1. Que el overlay de cada ambiente ponga AttachmentStorage__Provider=S3 y el bucket (GitOps)."
Write-Host "   2. Relevar los pods de la API: el Secret se lee al arrancar."
Write-Host "   3. Verificar /health/ready: el check de adjuntos dice «S3: s3://<bucket>/<prefijo>»."
Write-Host ""
