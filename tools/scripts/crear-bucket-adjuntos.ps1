# =============================================================================
# crear-bucket-adjuntos.ps1 - Crea en AWS el bucket donde el ERP guarda los
# adjuntos (soportes de comprobantes, planillas, archivos de dispersion). La
# credencial del ERP NO sale de aqui: es temporal (IAM Roles Anywhere, feature
# 011) y la arman crear-certificados-adjuntos.ps1 y la plantilla
# docs/operaciones/plantillas/adjuntos-roles-anywhere.yaml. El usuario IAM con
# llave permanente que este guion creaba se retiro el 2026-09-24.
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
# Corre igual en Windows PowerShell 5.1 y en PowerShell 7 (pwsh).
#
# Uso:
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloVerificar
#   .\tools\scripts\crear-bucket-adjuntos.ps1 -Perfil ingenia365
# =============================================================================
param(
    [string]$Bucket     = 'ingenia365-erp-attachments',
    [string]$Region     = 'us-east-1',
    # Perfil del AWS CLI. Sin esto se usa el perfil por defecto, que puede
    # apuntar a OTRA cuenta de AWS.
    [string]$Perfil     = '',
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
# Los dos ultimos son los de la app (MAUI): BlazorWebView sirve la pagina desde https://0.0.0.1 en Windows
# y Android y desde app://0.0.0.1 en iOS y Mac (codigo de dotnet/maui, rama net10.0: HostAddressHelper;
# seria 0.0.0.0 solo con el interruptor BlazorWebView.AppHostAddressAlways0000, que la app no usa). El CORS
# no da acceso: sin una autorizacion firmada por el ERP, ningun origen sube nada.
Write-Host "  CORS (subidas directas desde la web y la app del ERP) ... " -NoNewline
$cors = '{"CORSRules":[{"AllowedMethods":["POST"],' +
        '"AllowedOrigins":["https://app-dev.ingenia365.com","https://app-qa.ingenia365.com","https://app.ingenia365.com","https://0.0.0.1","app://0.0.0.1"],' +
        '"AllowedHeaders":["*"],"ExposeHeaders":["ETag"],"MaxAgeSeconds":3000}]}'
Invoke-AwsConJson @('s3api', 'put-bucket-cors', '--bucket', $Bucket, '--cors-configuration') -Json $cors
Write-Host "OK" -ForegroundColor Green

Write-Host ""
Write-Host "  Listo. La API accede con credenciales temporales (docs/operaciones/adjuntos-en-s3.md)." -ForegroundColor Cyan
Write-Host ""
