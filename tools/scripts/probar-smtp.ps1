# =============================================================================
# probar-smtp.ps1 — comprueba el correo saliente ANTES de que lo necesite una
# invitacion real.
#
# Hace cuatro comprobaciones y se detiene en la primera que falle:
#   1. DNS y puerto          -> descarta firewall y nombre mal escrito
#   2. STARTTLS y certificado -> descarta TLS, y DICE la huella si no valida
#   3. Autenticacion          -> descarta credenciales
#   4. Envio (opcional)       -> descarta permisos de "enviar como"
#
# Habla SMTP directamente sobre TcpClient en vez de usar MailKit: los paquetes de
# MailKit apuntan a .NET moderno y Windows PowerShell 5.1 no puede cargarlos.
#
# La contrasena se pide por consola y NO se guarda: ni en el historial, ni en la
# linea de comandos, ni en ningun archivo.
#
# Uso:
#   .\tools\scripts\probar-smtp.ps1
#   .\tools\scripts\probar-smtp.ps1 -Destinatario alguien@ejemplo.com
#   .\tools\scripts\probar-smtp.ps1 -SoloDiagnostico     (ni pide contrasena)
# =============================================================================
param(
    [string]$Servidor = 'mail.notifica365.com',
    [int]$Puerto = 587,
    [string]$Usuario = 'noresponder.ingenia365erp@notifica365.com',
    [string]$Remitente = 'noresponder.ingenia365erp@notifica365.com',
    [string]$NombreRemitente = 'No Responder IngenIA365 ERP',
    [string]$Destinatario,
    [switch]$SoloDiagnostico
)

$ErrorActionPreference = 'Stop'
$script:huellaServidor = $null

function Leer-Secreto([string]$mensaje) {
    $seguro = Read-Host -Prompt $mensaje -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
    try   { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Leer-Respuesta($lector) {
    $lineas = @()
    do {
        $l = $lector.ReadLine()
        if ($null -eq $l) { break }
        $lineas += $l
        $continua = ($l.Length -ge 4 -and $l.Substring(3, 1) -eq '-')
    } while ($continua)
    return $lineas
}

Write-Host ""
Write-Host "Probando ${Servidor}:$Puerto" -ForegroundColor Cyan
Write-Host ""

# --- 1. DNS y puerto ---------------------------------------------------------
Write-Host "==> 1/4  DNS y puerto" -ForegroundColor Cyan
try {
    $ip = [System.Net.Dns]::GetHostAddresses($Servidor) | Select-Object -First 1
    Write-Host "    $Servidor -> $ip" -ForegroundColor DarkGray
}
catch {
    Write-Host "    No resuelve '$Servidor'. Revisa el nombre." -ForegroundColor Red
    exit 1
}

$tcp = New-Object System.Net.Sockets.TcpClient
$tcp.ReceiveTimeout = 15000
$tcp.SendTimeout = 15000
try {
    $espera = $tcp.BeginConnect($Servidor, $Puerto, $null, $null)
    if (-not $espera.AsyncWaitHandle.WaitOne(10000)) {
        Write-Host "    El puerto $Puerto no responde en 10s." -ForegroundColor Red
        Write-Host "    Suele ser el firewall de salida, o el proveedor de internet" -ForegroundColor Yellow
        Write-Host "    bloqueando el 587." -ForegroundColor Yellow
        exit 1
    }
    $tcp.EndConnect($espera)
    Write-Host "    puerto $Puerto abierto." -ForegroundColor Green
}
catch {
    Write-Host "    No se pudo conectar: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# --- 2. STARTTLS y certificado ----------------------------------------------
Write-Host ""
Write-Host "==> 2/4  STARTTLS y certificado" -ForegroundColor Cyan

$flujo = $tcp.GetStream()
$lector = New-Object System.IO.StreamReader($flujo)
$escritor = New-Object System.IO.StreamWriter($flujo)
$escritor.AutoFlush = $true

$saludo = $lector.ReadLine()
Write-Host "    $saludo" -ForegroundColor DarkGray

$escritor.WriteLine("EHLO ingenia365.local")
$caps = Leer-Respuesta $lector
if (-not (($caps -join ' ') -match 'STARTTLS')) {
    Write-Host "    El servidor NO ofrece STARTTLS en el puerto $Puerto." -ForegroundColor Red
    Write-Host "    Si exige SSL implicito, es el puerto 465 — y ojo: el ERP hoy" -ForegroundColor Yellow
    Write-Host "    no lo soporta." -ForegroundColor Yellow
    $tcp.Close(); exit 1
}

$escritor.WriteLine("STARTTLS")
$null = $lector.ReadLine()

$global:certServidor = $null
$global:erroresTls = $null
$callback = [System.Net.Security.RemoteCertificateValidationCallback] {
    param($remitente, $certificado, $cadena, $errores)
    $global:certServidor = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certificado)
    $global:erroresTls = $errores
    return $true   # se acepta para poder DIAGNOSTICAR; el veredicto se da abajo
}

$ssl = New-Object System.Net.Security.SslStream($flujo, $false, $callback)
try {
    $ssl.AuthenticateAsClient($Servidor)
}
catch {
    Write-Host "    Fallo la negociacion TLS: $($_.Exception.Message)" -ForegroundColor Red
    $tcp.Close(); exit 1
}

Write-Host "    TLS $($ssl.SslProtocol) negociado." -ForegroundColor Green

$certValido = ($global:erroresTls -eq [System.Net.Security.SslPolicyErrors]::None)
if ($certValido) {
    Write-Host "    certificado valido." -ForegroundColor Green
}
else {
    $c = $global:certServidor
    $sha = [System.Security.Cryptography.SHA256]::Create().ComputeHash($c.RawData)
    $script:huellaServidor = (($sha | ForEach-Object { $_.ToString('X2') }) -join '')

    Write-Host ""
    Write-Host "    EL CERTIFICADO NO VALIDA ($global:erroresTls)" -ForegroundColor Red
    Write-Host "      sujeto      : $($c.Subject)"                 -ForegroundColor Yellow
    Write-Host "      emisor      : $($c.Issuer)"                  -ForegroundColor Yellow
    Write-Host "      vigencia    : $($c.NotBefore.ToString('yyyy-MM-dd')) a $($c.NotAfter.ToString('yyyy-MM-dd'))" -ForegroundColor Yellow
    Write-Host "      autofirmado : $($c.Subject -eq $c.Issuer)"   -ForegroundColor Yellow
    Write-Host ""
    Write-Host "    Lo correcto es instalar un certificado valido en ese servidor." -ForegroundColor Yellow
    Write-Host "    Como parche, el ERP puede aceptar ESE certificado y ninguno mas:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "      Smtp__HuellaCertificadoAceptada=$script:huellaServidor" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "    (Fijar la huella NO es lo mismo que desactivar la validacion:" -ForegroundColor DarkGray
    Write-Host "     esto acepta uno solo, y si el servidor cambia deja de conectar.)" -ForegroundColor DarkGray
}

if ($SoloDiagnostico) {
    Write-Host ""
    Write-Host "==> 3/4 y 4/4 omitidos (-SoloDiagnostico)." -ForegroundColor DarkGray
    $ssl.Dispose(); $tcp.Close()
    exit $(if ($certValido) { 0 } else { 2 })
}

# --- 3. Autenticacion --------------------------------------------------------
Write-Host ""
Write-Host "Contrasena del buzon $Usuario" -ForegroundColor Cyan
Write-Host "(no se guarda; solo vive en este proceso)" -ForegroundColor DarkGray
$clave = Leer-Secreto "Contrasena"
if ([string]::IsNullOrWhiteSpace($clave)) {
    Write-Host "Sin contrasena no hay nada mas que probar." -ForegroundColor Red
    $ssl.Dispose(); $tcp.Close(); exit 1
}

Write-Host ""
Write-Host "==> 3/4  Autenticacion" -ForegroundColor Cyan

$lectorSsl = New-Object System.IO.StreamReader($ssl)
$escritorSsl = New-Object System.IO.StreamWriter($ssl)
$escritorSsl.AutoFlush = $true

$escritorSsl.WriteLine("EHLO ingenia365.local")
$capsTls = Leer-Respuesta $lectorSsl

if (-not (($capsTls -join ' ') -match 'AUTH')) {
    Write-Host "    El servidor no ofrece AUTH tras STARTTLS." -ForegroundColor Red
    $ssl.Dispose(); $tcp.Close(); exit 1
}

$b64 = { param($s) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($s)) }
$escritorSsl.WriteLine("AUTH LOGIN")
$null = $lectorSsl.ReadLine()
$escritorSsl.WriteLine((& $b64 $Usuario))
$null = $lectorSsl.ReadLine()
$escritorSsl.WriteLine((& $b64 $clave))
$respAuth = $lectorSsl.ReadLine()

if ($respAuth -notmatch '^235') {
    Write-Host "    Rechazado: $respAuth" -ForegroundColor Red
    Write-Host ""
    Write-Host "    Causas habituales:" -ForegroundColor Yellow
    Write-Host "      - La contrasena no es la del buzon." -ForegroundColor Yellow
    Write-Host "      - El buzon exige contrasena de aplicacion (MFA activo)." -ForegroundColor Yellow
    Write-Host "      - El servidor tiene desactivada la autenticacion basica." -ForegroundColor Yellow
    $ssl.Dispose(); $tcp.Close(); exit 1
}
Write-Host "    autenticacion correcta." -ForegroundColor Green

# --- 4. Envio ----------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($Destinatario)) {
    Write-Host ""
    $Destinatario = Read-Host "Destinatario del correo de prueba (vacio para omitir)"
}

if ([string]::IsNullOrWhiteSpace($Destinatario)) {
    Write-Host "==> 4/4  Omitido: sin destinatario." -ForegroundColor DarkGray
    $escritorSsl.WriteLine("QUIT"); $ssl.Dispose(); $tcp.Close()
}
else {
    Write-Host ""
    Write-Host "==> 4/4  Enviando a $Destinatario" -ForegroundColor Cyan

    $escritorSsl.WriteLine("MAIL FROM:<$Remitente>")
    $rMail = $lectorSsl.ReadLine()
    if ($rMail -notmatch '^250') {
        Write-Host "    Rechazo el remitente: $rMail" -ForegroundColor Red
        Write-Host "    El buzon probablemente no tiene permiso de 'enviar como'" -ForegroundColor Yellow
        Write-Host "    la direccion $Remitente." -ForegroundColor Yellow
        $ssl.Dispose(); $tcp.Close(); exit 1
    }

    $escritorSsl.WriteLine("RCPT TO:<$Destinatario>")
    $rRcpt = $lectorSsl.ReadLine()
    if ($rRcpt -notmatch '^250|^251') {
        Write-Host "    Rechazo el destinatario: $rRcpt" -ForegroundColor Red
        $ssl.Dispose(); $tcp.Close(); exit 1
    }

    $escritorSsl.WriteLine("DATA")
    $null = $lectorSsl.ReadLine()

    $fecha = (Get-Date).ToString('r')
    $cuerpo = @"
From: $NombreRemitente <$Remitente>
To: <$Destinatario>
Subject: Prueba de correo saliente - IngenIA365 ERP
Date: $fecha
MIME-Version: 1.0
Content-Type: text/plain; charset=utf-8

Si estas leyendo esto, el correo saliente del ERP funciona.

Servidor: ${Servidor}:$Puerto (STARTTLS)
Autenticado como: $Usuario

Generado por tools/scripts/probar-smtp.ps1
.
"@
    foreach ($linea in $cuerpo -split "`r?`n") { $escritorSsl.WriteLine($linea) }
    $rData = $lectorSsl.ReadLine()

    if ($rData -match '^250') {
        Write-Host "    enviado ($rData)" -ForegroundColor Green
    }
    else {
        Write-Host "    Rechazado al entregar: $rData" -ForegroundColor Red
        $escritorSsl.WriteLine("QUIT"); $ssl.Dispose(); $tcp.Close(); exit 1
    }

    $escritorSsl.WriteLine("QUIT")
    $ssl.Dispose(); $tcp.Close()
}

# --- Resumen -----------------------------------------------------------------
Write-Host ""
Write-Host "Correo saliente operativo." -ForegroundColor Green
Write-Host ""
Write-Host "Para que el ERP lo use en local:" -ForegroundColor Cyan
Write-Host '  1. En appsettings.Development.json, dentro de Infraestructura:Destinos,'
Write-Host '     pone   "Smtp": "Microsoft365"'
Write-Host '  2. Arranca la API con las credenciales por variable de entorno:'
Write-Host ''
Write-Host "     `$env:Smtp__Username = '$Usuario'" -ForegroundColor DarkGray
Write-Host "     `$env:Smtp__Password = '<la contrasena>'" -ForegroundColor DarkGray
if ($script:huellaServidor) {
Write-Host "     `$env:Smtp__HuellaCertificadoAceptada = '$script:huellaServidor'" -ForegroundColor DarkGray
}
Write-Host '     dotnet run --project src/Presentation/IngenIA365ERP.API' -ForegroundColor DarkGray
Write-Host ''
Write-Host "  Con el destino en 'Docker' los correos quedan en smtp4dev" -ForegroundColor DarkGray
Write-Host "  (http://localhost:8025) sin salir a internet, que es lo que conviene" -ForegroundColor DarkGray
Write-Host "  para el dia a dia." -ForegroundColor DarkGray
