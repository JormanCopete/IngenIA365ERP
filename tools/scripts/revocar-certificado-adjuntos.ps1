# =============================================================================
# revocar-certificado-adjuntos.ps1 - Revoca un certificado de adjuntos (feature 011,
# US5, research R3) y deja la lista de revocados (CRL) lista para importar en IAM
# Roles Anywhere.
#
# Se usa cuando un certificado se filtro o se retira (y en el ensayo de T075, con
# el de prueba que emite crear-certificados-adjuntos.ps1 -Prueba). Roles Anywhere
# no consulta ningun servidor de revocacion: solo conoce la CRL que se le importa.
#
# QUE HACE:
#   - Lleva la base de la CA en -Carpeta\revocacion (index.txt, crlnumber): cada
#     revocacion se SUMA a las anteriores, y la CRL que sale las lista todas.
#   - Firma la CRL con la llave de la CA (pide su frase; no se guarda) y la deja en
#     -Carpeta\revocados.crl.pem. Es publica: lleva numeros de serie, no llaves.
#   - Muestra los comandos para importarla en CloudShell. Importarla lo hace un
#     administrador de la cuenta 058264424927: el usuario del perfil ingenia365 no
#     tiene permisos sobre Roles Anywhere.
#
# ORDEN SI SE FILTRO EL CERTIFICADO DE UN AMBIENTE (para no cortar los adjuntos):
#   1. crear-certificados-adjuntos.ps1 -Renovar -Ambientes <amb>: emite otro,
#      lo instala y reinicia la API, que pasa a usar el nuevo; el viejo queda en
#      anteriores\erp-api-<amb>-<serie>.crt;
#   2. este guion con el certificado VIEJO, e importar la CRL.
#   Al reves, la API se queda sin credencial hasta que se renueve. Las sesiones ya
#   emitidas con el certificado viejo siguen valiendo hasta su vencimiento, una hora
#   como mucho: la CRL corta las sesiones NUEVAS.
#
# Uso:
#   .\tools\scripts\revocar-certificado-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos -Certificado erp-api-qa-prueba.crt
#   .\tools\scripts\revocar-certificado-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos -Certificado anteriores\erp-api-dev-<serie>.crt -Motivo superseded
#   .\tools\scripts\revocar-certificado-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos          # solo vuelve a firmar la CRL
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [string]$Carpeta,
    # El certificado a revocar, relativo a -Carpeta o ruta completa. Sin el, solo se
    # vuelve a firmar la CRL con lo ya revocado.
    [string]$Certificado,
    [ValidateSet('keyCompromise', 'superseded', 'cessationOfOperation')]
    [string]$Motivo = 'keyCompromise'
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 vuelve terminante cualquier linea que un ejecutable escriba
# en stderr con ErrorActionPreference = Stop; se decide por el codigo de salida.
function Invoke-Nativo {
    param([string]$Programa, [string[]]$Argumentos)
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $salida = & $Programa $Argumentos 2>&1 }
    finally { $ErrorActionPreference = $previo }
    return ,@($salida | ForEach-Object { "$_" })
}

function Invoke-OpenSsl {
    param([string[]]$Argumentos, [string]$Que)
    $salida = Invoke-Nativo -Programa $script:OpenSsl -Argumentos $Argumentos
    if ($LASTEXITCODE -ne 0) { throw ("openssl fallo al {0}:`n{1}" -f $Que, ($salida -join "`n")) }
    return $salida
}

function ConvertFrom-Segura {
    param([Security.SecureString]$Segura)
    $puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Segura)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntero) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero) }
}

# openssl: el del PATH o el que trae Git para Windows (mingw64\bin); el de usr\bin
# es de MSYS y traduce las rutas a su manera.
$script:OpenSsl = (Get-Command openssl -ErrorAction SilentlyContinue).Source
if (-not $script:OpenSsl) {
    $raices = @()
    $git = (Get-Command git -ErrorAction SilentlyContinue).Source
    if ($git) {
        $raices += Split-Path (Split-Path $git -Parent) -Parent
        $raices += Split-Path (Split-Path (Split-Path $git -Parent) -Parent) -Parent
    }
    $raices += Join-Path $env:ProgramFiles 'Git'
    $script:OpenSsl = $raices |
        ForEach-Object { Join-Path $_ 'mingw64\bin\openssl.exe' } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
}
if (-not $script:OpenSsl) {
    throw "No se encontro openssl. Viene con Git para Windows (C:\Program Files\Git\mingw64\bin\openssl.exe)."
}

# ------------------------------------------------------------------ verificaciones --

$Carpeta = [IO.Path]::GetFullPath($Carpeta)
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($Carpeta.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta $Carpeta esta dentro del repositorio ($repo). La CA vive FUERA."
}
$caCrt = Join-Path $Carpeta 'ca.crt'
$caKey = Join-Path $Carpeta 'ca.key'
if (-not (Test-Path $caCrt) -or -not (Test-Path $caKey)) {
    throw "No estan ca.crt y ca.key en $Carpeta. Es la carpeta de custodia de crear-certificados-adjuntos.ps1."
}

$crt = $null
if ($Certificado) {
    $crt = if ([IO.Path]::IsPathRooted($Certificado)) { $Certificado } else { Join-Path $Carpeta $Certificado }
    if (-not (Test-Path $crt)) { throw "No existe $crt." }
    # Solo se revoca lo que emitio ESTA CA (-no_check_time: un vencido tambien se puede revocar).
    Invoke-Nativo -Programa $script:OpenSsl -Argumentos @('verify', '-no_check_time', '-CAfile', $caCrt, $crt) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$crt no lo emitio la CA de $Carpeta." }
}

# Base de la CA. openssl lee rutas con barras normales; con barras invertidas las
# tomaria como escapes.
$base = Join-Path $Carpeta 'revocacion'
New-Item -ItemType Directory -Force -Path $base | Out-Null
$indice = Join-Path $base 'index.txt'
$numero = Join-Path $base 'crlnumber'
# Sin CRLF: openssl los lee tal cual.
if (-not (Test-Path $indice)) { [IO.File]::WriteAllText($indice, '') }
if (-not (Test-Path $numero)) { [IO.File]::WriteAllText($numero, "01`n") }
$barras = { param($ruta) $ruta -replace '\\', '/' }
$config = Join-Path $base 'openssl-ca.cnf'
Set-Content -Path $config -Encoding ascii -Value @(
    '[ ca ]',
    'default_ca = adjuntos',
    '',
    '[ adjuntos ]',
    ('database = {0}' -f (& $barras $indice)),
    ('crlnumber = {0}' -f (& $barras $numero)),
    ('certificate = {0}' -f (& $barras $caCrt)),
    ('private_key = {0}' -f (& $barras $caKey)),
    'default_md = sha256',
    # Diez anos, como la CA: la CRL se importa a mano y no debe vencer sola.
    'default_crl_days = 3650',
    'unique_subject = no',
    'crl_extensions = extensiones_crl',
    '',
    '[ extensiones_crl ]',
    'authorityKeyIdentifier = keyid:always')
$crl = Join-Path $Carpeta 'revocados.crl.pem'

Write-Host ""
Write-Host "  Revocacion de certificados de adjuntos (IAM Roles Anywhere)" -ForegroundColor Cyan
Write-Host "  Carpeta de custodia: $Carpeta" -ForegroundColor DarkGray

$yaRevocado = $false
if ($crt) {
    $serie = ((Invoke-OpenSsl -Que 'leer el certificado' -Argumentos @('x509', '-in', $crt, '-noout', '-serial')) -join '') -replace '^serial=', ''
    $sujeto = (Invoke-OpenSsl -Que 'leer el certificado' -Argumentos @('x509', '-in', $crt, '-noout', '-subject', '-enddate')) -join '  |  '
    Write-Host ("  Certificado: {0}  |  serie {1}" -f $sujeto, $serie) -ForegroundColor DarkGray
    $yaRevocado = [bool](Get-Content $indice | Where-Object { $_ -match "^R\t.*\t$serie\t" })
    if ($yaRevocado) { Write-Host "  Ese certificado ya estaba revocado; solo se vuelve a firmar la CRL." -ForegroundColor Yellow }
}
Write-Host ""

$variableDeLaFrase = 'ERP_ADJUNTOS_CA_FRASE'
try {
    $frase = Read-Host "  Frase de la llave de la CA" -AsSecureString
    Set-Item -Path "env:$variableDeLaFrase" -Value (ConvertFrom-Segura $frase)

    if ($crt -and -not $yaRevocado) {
        Write-Host ("  Revocando (motivo {0}) ... " -f $Motivo) -NoNewline
        Invoke-OpenSsl -Que 'revocar' -Argumentos @(
            'ca', '-config', $config, '-revoke', $crt, '-crl_reason', $Motivo, '-passin', "env:$variableDeLaFrase") | Out-Null
        Write-Host "OK" -ForegroundColor Green
    }
    Write-Host "  Firmando la CRL ... " -NoNewline
    Invoke-OpenSsl -Que 'firmar la CRL' -Argumentos @(
        'ca', '-config', $config, '-gencrl', '-out', $crl, '-passin', "env:$variableDeLaFrase") | Out-Null
    Write-Host "OK" -ForegroundColor Green
} finally {
    Remove-Item -Path "env:$variableDeLaFrase" -ErrorAction SilentlyContinue
    [GC]::Collect()
}

# La CRL tiene que verificar contra la CA, y el certificado revocado tiene que salir
# como tal: si no, lo que se importa no sirve de nada.
Invoke-OpenSsl -Que 'verificar la firma de la CRL' -Argumentos @('crl', '-in', $crl, '-CAfile', $caCrt, '-noout') | Out-Null
if ($crt) {
    $veredicto = Invoke-Nativo -Programa $script:OpenSsl -Argumentos @(
        'verify', '-crl_check', '-no_check_time', '-CAfile', $caCrt, '-CRLfile', $crl, $crt)
    if (($veredicto -join ' ') -notmatch 'revoked') { throw ("La CRL no marca el certificado como revocado:`n{0}" -f ($veredicto -join "`n")) }
}
$revocados = @((Invoke-OpenSsl -Que 'leer la CRL' -Argumentos @('crl', '-in', $crl, '-noout', '-text')) | Where-Object { $_ -match 'Serial Number:' }).Count
Write-Host ("  CRL: {0} certificado(s) revocado(s) en {1}" -f $revocados, $crl) -ForegroundColor DarkGray

$anclaDeConfianza = '"$(aws cloudformation describe-stacks --region us-east-1 --stack-name ingenia365-erp-adjuntos-roles-anywhere --query "Stacks[0].Outputs[?OutputKey==''TrustAnchorArn''].OutputValue" --output text)"'
Write-Host ""
Write-Host "  Siguiente (un administrador de la cuenta 058264424927, en AWS CloudShell, us-east-1):" -ForegroundColor Cyan
Write-Host "   1. Acciones > Cargar archivo: $crl"
Write-Host "   2. Ver si ya hay una CRL importada:"
Write-Host '        aws rolesanywhere list-crls --region us-east-1 --query "crls[].[name,crlId,enabled]" --output table'
Write-Host "   3a. Si no hay ninguna, importarla:"
Write-Host ("        aws rolesanywhere import-crl --region us-east-1 --name ingenia365-erp-adjuntos --enabled --crl-data fileb://revocados.crl.pem --trust-anchor-arn {0}" -f $anclaDeConfianza)
Write-Host "   3b. Si ya hay una, reemplazar su contenido (la nueva lista trae tambien lo revocado antes):"
Write-Host "        aws rolesanywhere update-crl --region us-east-1 --crl-id <crlId del paso 2> --crl-data fileb://revocados.crl.pem"
Write-Host ""
Write-Host "  Desde ese momento el certificado ya no obtiene sesiones nuevas; las ya emitidas vencen en una hora." -ForegroundColor DarkGray
Write-Host ""
