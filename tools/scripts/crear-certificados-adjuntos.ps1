# =============================================================================
# crear-certificados-adjuntos.ps1 - Los certificados con los que la API obtiene
# credenciales TEMPORALES de AWS para el bucket de adjuntos (feature 011, US5,
# research R3): IAM Roles Anywhere en lugar de la llave permanente.
#
#   CA propia  (ca.crt / ca.key)      -> trust anchor de Roles Anywhere (sin costo)
#   erp-api-dev.crt / .key            -> Secret erp-adjuntos-certificado en erp-dev
#   erp-api-qa.crt  / .key            -> Secret erp-adjuntos-certificado en erp-qa
#   erp-api-pdn.crt / .key            -> Secret erp-adjuntos-certificado en erp-pdn
#
# Cada certificado lleva CN erp-api-<ambiente>; el rol de cada ambiente confia
# SOLO en su CN, asi que la credencial de DEV no puede leer produccion.
#
# DONDE QUEDA CADA COSA:
#   - Todo se escribe en -Carpeta, que tiene que estar FUERA del repositorio (el
#     guion se niega si no). Esa carpeta es custodia del dueno: la llave de la
#     CA va cifrada con una frase que se pide aqui y no se guarda en ningun lado.
#   - La llave de la CA NUNCA entra al cluster. Al cluster sube solo el
#     certificado y la llave de su ambiente, por STDIN sobre SSH.
#   - Ninguna llave se imprime. Lo unico que se muestra es el sujeto y el
#     vencimiento de cada certificado, y la ruta de ca.crt (publico: es lo que
#     se pega en la plantilla de CloudFormation, T069).
#
# PRODUCCION solo si se pide con -Ambientes pdn (o dev,qa,pdn), y el guion pide
# escribir PRODUCCION antes de instalar alli.
#
# RENOVAR: los certificados duran un ano. Un certificado con mas de 30 dias de
# vida se reutiliza; en el ultimo mes (o con -Renovar) se emite otro. Si el sidecar
# ya esta activo, el guion reinicia la API del ambiente despues de instalar: el
# sidecar lee el certificado solo al arrancar.
#
# -Prueba emite un certificado desechable de 7 dias para ensayar la revocacion
# (revocar-certificado-adjuntos.ps1) sin tocar el de la API.
#
# Corre igual en Windows PowerShell 5.1 y en PowerShell 7. Necesita openssl (lo
# busca en el PATH y, si no, en la instalacion de Git para Windows) y la llave SSH
# de despliegue.
#
# Uso:
#   .\tools\scripts\crear-certificados-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos
#   .\tools\scripts\crear-certificados-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos -Ambientes pdn
#   .\tools\scripts\crear-certificados-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos -Renovar -Ambientes qa
#   .\tools\scripts\crear-certificados-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos -NoInstalar
#   .\tools\scripts\crear-certificados-adjuntos.ps1 -Carpeta D:\Custodia\erp-adjuntos -Ambientes qa -Prueba
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [string]$Carpeta,
    [ValidateSet('dev', 'qa', 'pdn')]
    [string[]]$Ambientes = @('dev', 'qa'),
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    # Vuelve a emitir el certificado del ambiente aunque el vigente no haya vencido
    # (por ejemplo, despues de revocarlo con una CRL).
    [switch]$Renovar,
    # Solo crea o renueva los archivos; no toca los clusteres.
    [switch]$NoInstalar,
    # Certificado DESECHABLE para probar la revocacion sin tocar el de la API:
    # mismo CN del ambiente (el rol lo acepta), 7 dias, archivos erp-api-<amb>-prueba.*
    # y Secret erp-adjuntos-certificado-prueba. Siempre emite uno nuevo. Solo DEV y QA.
    [switch]$Prueba
)

$ErrorActionPreference = 'Stop'

$destinos = @{
    dev = @{ Maquina = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' }
    qa  = @{ Maquina = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' }
    pdn = @{ Maquina = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' }
}

# Windows PowerShell 5.1 vuelve terminante cualquier linea que un ejecutable escriba
# en stderr con ErrorActionPreference = Stop, aunque termine en 0; openssl escribe
# avisos por ahi. Se baja la preferencia alrededor de la llamada y se decide por el
# codigo de salida.
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

# ------------------------------------------------------------------ verificaciones --

# openssl: el del PATH o, si no esta (lo normal en Windows: Git no lo agrega), el
# que trae Git para Windows en mingw64\bin. El de usr\bin es de MSYS y traduce las
# rutas a su manera; no se usa.
$script:OpenSsl = (Get-Command openssl -ErrorAction SilentlyContinue).Source
if (-not $script:OpenSsl) {
    $raices = @()
    $git = (Get-Command git -ErrorAction SilentlyContinue).Source
    if ($git) {
        # ...\Git\cmd\git.exe o ...\Git\mingw64\bin\git.exe
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

$Carpeta = [IO.Path]::GetFullPath($Carpeta)
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($Carpeta.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta $Carpeta esta dentro del repositorio ($repo). Las llaves van FUERA: elija otra."
}
if ($Prueba -and $Ambientes -contains 'pdn') {
    throw "-Prueba es para ensayar la revocacion en DEV o QA; en produccion no se emiten certificados de prueba."
}
$secretoDelCertificado = if ($Prueba) { 'erp-adjuntos-certificado-prueba' } else { 'erp-adjuntos-certificado' }
if ($Ambientes -contains 'pdn' -and -not $NoInstalar) {
    Write-Host ""
    Write-Host "  Va a instalar el certificado de PRODUCCION en el cluster erp-pdn." -ForegroundColor Yellow
    if ((Read-Host "  Escriba PRODUCCION para continuar") -cne 'PRODUCCION') { throw "Cancelado." }
}
New-Item -ItemType Directory -Force -Path $Carpeta | Out-Null

$caCrt = Join-Path $Carpeta 'ca.crt'
$caKey = Join-Path $Carpeta 'ca.key'

Write-Host ""
Write-Host "  Certificados de adjuntos del ERP (IAM Roles Anywhere)" -ForegroundColor Cyan
Write-Host "  Carpeta de custodia: $Carpeta" -ForegroundColor DarkGray
Write-Host ""

# La frase de la llave de la CA viaja a openssl por una variable de entorno de
# ESTE proceso (-passin env:), nunca como argumento: un argumento queda visible en
# la lista de procesos y en el historial.
$variableDeLaFrase = 'ERP_ADJUNTOS_CA_FRASE'
try {
    if (-not (Test-Path $caCrt) -or -not (Test-Path $caKey)) {
        Write-Host "  No hay CA en la carpeta: se crea una nueva (RSA 3072, diez anos)." -ForegroundColor Yellow
        $frase1 = Read-Host "  Frase para cifrar la llave de la CA" -AsSecureString
        $frase2 = Read-Host "  Repitala" -AsSecureString
        $texto1 = ConvertFrom-Segura $frase1
        if ($texto1.Length -lt 12) { throw "La frase tiene que tener al menos 12 caracteres." }
        if ($texto1 -cne (ConvertFrom-Segura $frase2)) { throw "Las frases no coinciden." }
        Set-Item -Path "env:$variableDeLaFrase" -Value $texto1
        $texto1 = $null
        Write-Host "  Creando la CA ... " -NoNewline
        Invoke-OpenSsl -Que 'crear la CA' -Argumentos @(
            'req', '-x509', '-newkey', 'rsa:3072', '-sha256', '-days', '3650',
            '-keyout', $caKey, '-out', $caCrt, '-passout', "env:$variableDeLaFrase",
            '-subj', '/O=INGENIA 365/CN=ingenia365-erp-adjuntos-ca',
            '-addext', 'basicConstraints=critical,CA:TRUE',
            '-addext', 'keyUsage=critical,keyCertSign,cRLSign') | Out-Null
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "  Se usa la CA existente de la carpeta." -ForegroundColor DarkGray
        $frase = Read-Host "  Frase de la llave de la CA" -AsSecureString
        Set-Item -Path "env:$variableDeLaFrase" -Value (ConvertFrom-Segura $frase)
    }

    # Extensiones de un certificado de entidad final que Roles Anywhere acepta.
    $extensiones = Join-Path $Carpeta 'entidad-final.ext'
    Set-Content -Path $extensiones -Encoding ascii -Value @(
        'basicConstraints=critical,CA:FALSE',
        'keyUsage=critical,digitalSignature',
        'extendedKeyUsage=clientAuth',
        'subjectKeyIdentifier=hash',
        'authorityKeyIdentifier=keyid')

    foreach ($ambiente in $Ambientes) {
        # El CN es el que el rol del ambiente acepta; el de prueba lleva el mismo y
        # se distingue por el archivo y por el numero de serie, que es lo que revoca la CRL.
        $cn = "erp-api-$ambiente"
        $archivo = if ($Prueba) { "$cn-prueba" } else { $cn }
        $dias = if ($Prueba) { '7' } else { '365' }
        $crt = Join-Path $Carpeta "$archivo.crt"
        $key = Join-Path $Carpeta "$archivo.key"
        $csr = Join-Path $Carpeta "$archivo.csr"

        $vigente = $false
        if ((Test-Path $crt) -and (Test-Path $key) -and -not $Renovar -and -not $Prueba) {
            Invoke-Nativo -Programa $script:OpenSsl -Argumentos @('x509', '-in', $crt, '-noout', '-checkend', '2592000') | Out-Null
            $vigente = ($LASTEXITCODE -eq 0)
        }
        if ($vigente) {
            Write-Host ("  {0}: el certificado vigente dura mas de 30 dias; se reutiliza." -f $cn) -ForegroundColor DarkGray
        } else {
            # El anterior se guarda (solo el certificado, que es publico) para poder
            # revocarlo despues con revocar-certificado-adjuntos.ps1: anteriores\<archivo>-<serie>.crt
            if (Test-Path $crt) {
                $serieAnterior = ((Invoke-OpenSsl -Que "leer el anterior $archivo" -Argumentos @('x509', '-in', $crt, '-noout', '-serial')) -join '') -replace '^serial=', ''
                $anteriores = Join-Path $Carpeta 'anteriores'
                New-Item -ItemType Directory -Force -Path $anteriores | Out-Null
                Copy-Item $crt (Join-Path $anteriores "$archivo-$serieAnterior.crt") -Force
                Write-Host ("  El certificado anterior queda en anteriores\{0}-{1}.crt" -f $archivo, $serieAnterior) -ForegroundColor DarkGray
            }
            Write-Host ("  Emitiendo {0} (EC P-256, {1} dias) ... " -f $archivo, $dias) -NoNewline
            Invoke-OpenSsl -Que "crear la llave de $archivo" -Argumentos @(
                'genpkey', '-algorithm', 'EC', '-pkeyopt', 'ec_paramgen_curve:P-256', '-out', $key) | Out-Null
            Invoke-OpenSsl -Que "crear la solicitud de $archivo" -Argumentos @(
                'req', '-new', '-key', $key, '-subj', "/O=INGENIA 365/CN=$cn", '-out', $csr) | Out-Null
            Invoke-OpenSsl -Que "firmar $archivo" -Argumentos @(
                'x509', '-req', '-in', $csr, '-CA', $caCrt, '-CAkey', $caKey, '-passin', "env:$variableDeLaFrase",
                '-CAcreateserial', '-days', $dias, '-sha256', '-extfile', $extensiones, '-out', $crt) | Out-Null
            Remove-Item $csr -ErrorAction SilentlyContinue
            Write-Host "OK" -ForegroundColor Green
        }
        $datos = Invoke-OpenSsl -Que "leer $archivo" -Argumentos @('x509', '-in', $crt, '-noout', '-subject', '-serial', '-enddate')
        Write-Host ("    {0}" -f ($datos -join '  |  ')) -ForegroundColor DarkGray
    }
} finally {
    Remove-Item -Path "env:$variableDeLaFrase" -ErrorAction SilentlyContinue
    [GC]::Collect()
}

# ------------------------------------------------------------------ instalar --

if ($NoInstalar) {
    Write-Host ""
    Write-Host "  -NoInstalar: los clusteres no se tocaron." -ForegroundColor Yellow
} else {
    Write-Host ""
    foreach ($ambiente in $Ambientes) {
        $d = $destinos[$ambiente]
        $archivo = if ($Prueba) { "erp-api-$ambiente-prueba" } else { "erp-api-$ambiente" }
        $b64Crt = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $Carpeta "$archivo.crt")))
        $b64Key = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $Carpeta "$archivo.key")))
        $yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: $secretoDelCertificado
  namespace: $($d.Namespace)
type: kubernetes.io/tls
data:
  tls.crt: $b64Crt
  tls.key: $b64Key
"@
        Write-Host ("  Instalando {0} en {1} ({2}) ... " -f $secretoDelCertificado, $d.Nombre, $d.Namespace) -NoNewline
        $salida = Invoke-Nativo -Programa 'ssh' -Entrada $yaml -Argumentos @(
            '-i', $KeyPath, '-o', 'BatchMode=yes', "root@$($d.Maquina)", 'k3s kubectl apply -f -')
        $instalado = ($LASTEXITCODE -eq 0)
        if ($instalado) { Write-Host "OK" -ForegroundColor Green }
        else { Write-Host "FALLO" -ForegroundColor Red; Write-Host ("    {0}" -f ($salida -join "`n")) -ForegroundColor Red }
        $b64Key = $null

        # El sidecar lee el certificado UNA vez, al arrancar (aws_signing_helper serve
        # crea el firmador antes de escuchar). Si ya esta activo, sin reiniciar seguiria
        # con el certificado anterior hasta que venza, y ese dia se caen los adjuntos.
        # El reinicio no corta: el Deployment va con maxUnavailable 0.
        if ($instalado -and -not $Prueba) {
            $contenedores = Invoke-Nativo -Programa 'ssh' -Argumentos @(
                '-i', $KeyPath, '-o', 'BatchMode=yes', "root@$($d.Maquina)",
                "k3s kubectl get deployment erp-api -n $($d.Namespace) -o jsonpath='{.spec.template.spec.initContainers[*].name}'")
            if (($contenedores -join ' ') -match 'credenciales-adjuntos') {
                Write-Host ("  Reiniciando la API de {0} para que el sidecar tome el certificado nuevo ... " -f $d.Nombre) -NoNewline
                $salida = Invoke-Nativo -Programa 'ssh' -Argumentos @(
                    '-i', $KeyPath, '-o', 'BatchMode=yes', "root@$($d.Maquina)",
                    "k3s kubectl rollout restart deployment/erp-api -n $($d.Namespace) && k3s kubectl rollout status deployment/erp-api -n $($d.Namespace) --timeout=600s")
                if ($LASTEXITCODE -eq 0) { Write-Host "OK" -ForegroundColor Green }
                else { Write-Host "REVISAR" -ForegroundColor Red; Write-Host ("    {0}" -f ($salida -join "`n")) -ForegroundColor Red }
            }
        }
    }
    [GC]::Collect()
}

if ($Prueba) {
    Write-Host ""
    Write-Host "  Listo el certificado de PRUEBA. Ensayo de revocacion (docs/operaciones/adjuntos-en-s3.md):" -ForegroundColor Cyan
    Write-Host "   1. probar-credencial-adjuntos.ps1 -Ambiente <amb> -Secret erp-adjuntos-certificado-prueba -Prueba ObtieneCredencial"
    Write-Host "   2. revocar-certificado-adjuntos.ps1 -Carpeta $Carpeta -Certificado erp-api-<amb>-prueba.crt, e importar la CRL"
    Write-Host "   3. probar-credencial-adjuntos.ps1 ... -Prueba SinCredencial"
    Write-Host ""
    return
}

Write-Host ""
Write-Host "  Listo. Siguiente:" -ForegroundColor Cyan
Write-Host "   1. Si la pila de Roles Anywhere todavia no existe: aplicar docs/operaciones/plantillas/adjuntos-roles-anywhere.yaml"
Write-Host "      en la cuenta 058264424927 con el contenido de $caCrt en CertificadoDeLaCa (es publico)."
Write-Host "   2. Guardar la carpeta $Carpeta fuera de esta maquina: la llave de la CA solo sirve con su frase,"
Write-Host "      pero sin ella no se emite ni se renueva ningun certificado."
Write-Host "   3. Agendar la renovacion: volver a correr este guion en el ultimo mes de vigencia (ver arriba)."
Write-Host ""
