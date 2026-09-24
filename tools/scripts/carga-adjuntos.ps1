# =============================================================================
# carga-adjuntos.ps1 - Prueba de carga de adjuntos (feature 011, SC-002, T080).
#
# Veinte personas subiendo a la vez archivos del tamano maximo no tienen que
# cambiar la memoria de la API ni el tiempo de respuesta de otra pantalla, porque
# el archivo va del navegador al bucket sin pasar por el servidor. Este guion lo
# mide en DEV o QA:
#
#   1. abre un tunel SSH al Service erp-api del ambiente (DEV y QA estan detras de
#      Cloudflare Access: desde fuera del navegador no se llega a la API);
#   2. inicia sesion con SU usuario (correo, contrasena y el codigo de su app de
#      autenticacion; se piden aqui y no se guardan ni se imprimen);
#   3. mide la lista de comprobantes (la "otra pantalla") y la memoria de la API;
#   4. pide N autorizaciones de subida para el comprobante indicado, sube los N
#      archivos EN PARALELO directo al bucket y, mientras suben, vuelve a medir;
#   5. confirma cada subida, informa y, salvo -Conservar, borra los adjuntos de
#      prueba (quedan 90 dias en la papelera del bucket: unos 500 MB, centavos).
#
# Necesita: Tailscale conectado, la llave SSH de despliegue, un usuario de la
# cooperativa con Attachments.Upload/Delete y Accounting.Vouchers.Create/View, un
# segundo factor TOTP (con solo passkey no se puede desde aqui) y un comprobante EN
# BORRADOR (los soportes de uno contabilizado no se borran).
#
# Uso:
#   .\tools\scripts\carga-adjuntos.ps1 -Comprobante <PublicId del borrador>
#   .\tools\scripts\carga-adjuntos.ps1 -Ambiente qa -Comprobante <id> -Subidas 10 -TamanoMb 5 -Cooperativa demo
# =============================================================================
param(
    [ValidateSet('dev', 'qa')]
    [string]$Ambiente = 'dev',
    [Parameter(Mandatory = $true)]
    [Guid]$Comprobante,
    [ValidateRange(1, 50)]
    [int]$Subidas = 20,
    [ValidateRange(1, 25)]
    [int]$TamanoMb = 25,
    # Nombre (o parte) de la cooperativa, si su usuario tiene varias.
    [string]$Cooperativa,
    # No borrar los adjuntos de prueba al terminar.
    [switch]$Conservar,
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'
$maquina = '100.94.218.42'
$ns = "erp-$Ambiente"

# Windows PowerShell 5.1: TLS 1.2 para S3, y sin esto HttpClient abre solo DOS
# conexiones por servidor y las "20 en paralelo" serian de a dos.
Add-Type -AssemblyName System.Net.Http
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
[Net.ServicePointManager]::DefaultConnectionLimit = 128

function ConvertFrom-Segura {
    param([Security.SecureString]$Segura)
    $puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Segura)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntero) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero) }
}

function Invoke-Remoto {
    param([string]$Comando)
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $salida = & ssh -i $KeyPath -o BatchMode=yes -o ConnectTimeout=15 "root@$maquina" $Comando 2>&1 }
    finally { $ErrorActionPreference = $previo }
    return ,@($salida | ForEach-Object { "$_" })
}

# --- la API por el tunel -----------------------------------------------------------
$script:api = $null
$script:bearer = $null
$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromMinutes(2)

function Invoke-Api {
    param([string]$Metodo, [string]$Ruta, $Cuerpo, [string]$Token = $script:bearer, [switch]$SinFallar)
    $pedido = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Metodo)), "$script:api$Ruta"
    if ($Token) { $pedido.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer', $Token }
    if ($null -ne $Cuerpo) {
        $pedido.Content = New-Object System.Net.Http.StringContent (($Cuerpo | ConvertTo-Json -Depth 6 -Compress), [Text.Encoding]::UTF8, 'application/json')
    }
    $respuesta = $http.SendAsync($pedido).GetAwaiter().GetResult()
    $texto = $respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $json = $null
    if ($texto) { try { $json = $texto | ConvertFrom-Json } catch { } }
    if (-not $respuesta.IsSuccessStatusCode -and -not $SinFallar) {
        $motivo = if ($json -and $json.code) { "$($json.code): $($json.message)" } else { $texto }
        throw ("{0} {1} respondio {2}. {3}" -f $Metodo, $Ruta, [int]$respuesta.StatusCode, $motivo)
    }
    return [pscustomobject]@{ Estado = [int]$respuesta.StatusCode; Json = $json }
}

function Resolve-Sesion {
    # Sigue el mismo camino que la pantalla de ingreso: segundo factor, eleccion de cooperativa.
    param($Resultado)
    switch ($Resultado.challenge) {
        'MfaRequired' {
            $codigo = Read-Host "  Codigo de su app de autenticacion (6 digitos)"
            $r = Invoke-Api -Metodo POST -Ruta '/api/auth/mfa/verify' -Cuerpo @{ code = $codigo.Trim(); useRecoveryCode = $false } -Token $Resultado.challengeToken
            return Resolve-Sesion $r.Json
        }
        'TenantSelection' {
            $elegida = Select-Cooperativa @($Resultado.activeTenants)
            $r = Invoke-Api -Metodo POST -Ruta '/api/sessions/select-tenant' -Cuerpo @{ tenantPublicId = $elegida.tenantPublicId } -Token $Resultado.challengeToken
            return [pscustomobject]@{ Token = $r.Json.accessToken; Cooperativa = $r.Json.tenant.tenantName }
        }
        'None' {
            if (-not $Resultado.accessToken) { throw "El ingreso no devolvio una sesion." }
            $sesion = [pscustomobject]@{ Token = $Resultado.accessToken; Cooperativa = $Resultado.activeTenantName }
            if ($Cooperativa -and $sesion.Cooperativa -notlike "*$Cooperativa*") {
                $elegida = Select-Cooperativa @($Resultado.activeTenants)
                $r = Invoke-Api -Metodo POST -Ruta '/api/sessions/switch-tenant' -Cuerpo @{ tenantPublicId = $elegida.tenantPublicId } -Token $sesion.Token
                $sesion = [pscustomobject]@{ Token = $r.Json.accessToken; Cooperativa = $elegida.tenantName }
            }
            return $sesion
        }
        'MfaEnrollmentRequired' { throw "Su usuario todavia no tiene segundo factor: inscribalo desde la aplicacion." }
        'NoActiveMembership' { throw "Su usuario no pertenece a ninguna cooperativa activa." }
        default { throw "El ingreso pidio '$($Resultado.challenge)', que este guion no sabe resolver." }
    }
}

function Select-Cooperativa {
    param([object[]]$Opciones)
    $candidatas = if ($Cooperativa) { @($Opciones | Where-Object { $_.tenantName -like "*$Cooperativa*" }) } else { @($Opciones) }
    if ($candidatas.Count -eq 1) { return $candidatas[0] }
    throw ("Hay que elegir cooperativa con -Cooperativa. Opciones: {0}" -f (($Opciones | ForEach-Object { $_.tenantName }) -join ', '))
}

function Measure-Pantalla {
    # La "otra pantalla": la lista de comprobantes del mes.
    $desde = (Get-Date -Day 1).ToString('yyyy-MM-dd')
    $hasta = (Get-Date).ToString('yyyy-MM-dd')
    $reloj = [Diagnostics.Stopwatch]::StartNew()
    $r = Invoke-Api -Metodo GET -Ruta "/api/accounting/documents?from=$desde&to=$hasta" -SinFallar
    $reloj.Stop()
    if ($r.Estado -ne 200) { throw "La lista de comprobantes respondio $($r.Estado)." }
    return $reloj.Elapsed.TotalMilliseconds
}

function Get-Mediana { param([double[]]$v) $o = @($v | Sort-Object); if (-not $o) { return 0 }; return $o[[int][Math]::Floor(($o.Count - 1) / 2)] }

$tunel = $null
$archivo = $null
$memoria = $null
$adjuntos = New-Object System.Collections.Generic.List[string]
try {
    Write-Host ""
    Write-Host ("  Carga de adjuntos en {0}: {1} subidas de {2} MB en paralelo" -f $ns, $Subidas, $TamanoMb) -ForegroundColor Cyan

    # --- tunel ------------------------------------------------------------------
    $ipDelServicio = ((Invoke-Remoto "k3s kubectl get svc erp-api -n $ns -o jsonpath='{.spec.clusterIP}'") -join '').Trim()
    if ($ipDelServicio -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "No se pudo leer el Service erp-api de $ns ($ipDelServicio)." }
    $puerto = 18080..18099 | Where-Object { -not (Get-NetTCPConnection -LocalPort $_ -ErrorAction SilentlyContinue) } | Select-Object -First 1
    $tunel = Start-Process ssh -PassThru -WindowStyle Hidden -ArgumentList @(
        '-i', "`"$KeyPath`"", '-o', 'BatchMode=yes', '-o', 'ExitOnForwardFailure=yes', '-N',
        '-L', "$($puerto):$($ipDelServicio):80", "root@$maquina")
    $script:api = "http://127.0.0.1:$puerto"
    $listo = $false
    for ($i = 0; $i -lt 30 -and -not $listo; $i++) {
        Start-Sleep -Milliseconds 500
        try { $listo = (Invoke-Api -Metodo GET -Ruta '/api/auth/session-policy' -Token $null -SinFallar).Estado -eq 200 } catch { }
    }
    if (-not $listo) { throw "El tunel a la API de $ns no respondio." }
    Write-Host "  Tunel a la API de $ns abierto." -ForegroundColor DarkGray

    # --- sesion -------------------------------------------------------------------
    $correo = Read-Host "  Correo"
    $clave = Read-Host "  Contrasena" -AsSecureString
    $login = Invoke-Api -Metodo POST -Ruta '/api/auth/login' -Cuerpo @{ email = $correo.Trim(); password = (ConvertFrom-Segura $clave) } -Token $null
    $clave = $null
    $sesion = Resolve-Sesion $login.Json
    $script:bearer = $sesion.Token
    Write-Host ("  Sesion iniciada en {0}." -f $sesion.Cooperativa) -ForegroundColor DarkGray

    # --- el archivo -----------------------------------------------------------------
    $archivo = Join-Path $env:TEMP ("carga-adjuntos-{0}.pdf" -f [guid]::NewGuid().ToString('N'))
    $tamano = [long]$TamanoMb * 1MB
    $azar = [Security.Cryptography.RandomNumberGenerator]::Create()
    $escritura = [IO.File]::Create($archivo)
    try {
        $cabecera = [Text.Encoding]::ASCII.GetBytes("%PDF-1.7`n")
        $escritura.Write($cabecera, 0, $cabecera.Length)
        $bloque = New-Object byte[] (1MB)
        $restante = $tamano - $cabecera.Length
        while ($restante -gt 0) {
            $azar.GetBytes($bloque)
            $n = [int][Math]::Min($bloque.Length, $restante)
            $escritura.Write($bloque, 0, $n)
            $restante -= $n
        }
    } finally { $escritura.Dispose() }
    $lectura = [IO.File]::OpenRead($archivo)
    try { $huella = [Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash($lectura)) } finally { $lectura.Dispose() }

    # --- antes ------------------------------------------------------------------------
    $memoria = Start-Job -ArgumentList $KeyPath, $maquina, $ns -ScriptBlock {
        param($k, $m, $n)
        while ($true) {
            $linea = & ssh -i $k -o BatchMode=yes -o ConnectTimeout=10 "root@$m" "k3s kubectl top pod -n $n -l app.kubernetes.io/name=erp-api --no-headers" 2>$null
            foreach ($l in @($linea)) { if ($l) { "{0}|{1}" -f (Get-Date).ToString('o'), $l } }
            Start-Sleep -Seconds 5
        }
    }
    Write-Host "  Midiendo la lista de comprobantes sin carga ..." -ForegroundColor DarkGray
    $sinCarga = @(1..8 | ForEach-Object { Measure-Pantalla; Start-Sleep -Milliseconds 500 })
    Start-Sleep -Seconds 20   # que el muestreo de memoria tenga lineas de antes
    $inicioCarga = Get-Date

    # --- autorizaciones ---------------------------------------------------------------
    $autorizaciones = @()
    for ($i = 1; $i -le $Subidas; $i++) {
        $r = Invoke-Api -Metodo POST -Ruta '/api/attachments/uploads' -Cuerpo @{
            ownerEntityType = 'AccountingDocument'; ownerEntityPublicId = $Comprobante.ToString()
            fileName = ("carga-{0:00}.pdf" -f $i); contentType = 'application/pdf'; sizeBytes = $tamano; sha256Base64 = $huella
        }
        $adjuntos.Add($r.Json.attachmentPublicId)
        $autorizaciones += $r.Json
    }
    Write-Host ("  {0} autorizaciones de subida firmadas." -f $autorizaciones.Count) -ForegroundColor DarkGray

    # --- las subidas, en paralelo, directo al bucket -------------------------------------
    $s3 = New-Object System.Net.Http.HttpClient
    $s3.Timeout = [TimeSpan]::FromMinutes(30)
    $envios = @()
    $flujos = @()
    foreach ($a in $autorizaciones) {
        $formulario = New-Object System.Net.Http.MultipartFormDataContent
        # Todos los campos en el orden recibido y el archivo al final (contracts/api.md §1).
        foreach ($campo in $a.upload.fields.PSObject.Properties) {
            # Como un navegador: los campos van sin Content-Type propio.
            $valor = New-Object System.Net.Http.StringContent ([string]$campo.Value)
            $valor.Headers.ContentType = $null
            $formulario.Add($valor, "`"$($campo.Name)`"")
        }
        $flujo = [IO.File]::Open($archivo, 'Open', 'Read', 'Read')
        $flujos += $flujo
        $parte = New-Object System.Net.Http.StreamContent $flujo
        $parte.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/pdf')
        # Sin filename*: algunos servidores rechazan la forma extendida.
        $disposicion = New-Object System.Net.Http.Headers.ContentDispositionHeaderValue 'form-data'
        $disposicion.Name = "`"$($a.upload.fileField)`""
        $disposicion.FileName = '"carga.pdf"'
        $parte.Headers.ContentDisposition = $disposicion
        $formulario.Add($parte)
        $envios += $s3.PostAsync($a.upload.url, $formulario)
    }
    Write-Host "  Subiendo ..." -ForegroundColor DarkGray
    $conCarga = @()
    while (@($envios | Where-Object { -not $_.IsCompleted }).Count -gt 0) {
        $conCarga += Measure-Pantalla
        Start-Sleep -Seconds 2
    }
    $finCarga = Get-Date
    $fallidas = @()
    for ($i = 0; $i -lt $envios.Count; $i++) {
        $t = $envios[$i]
        if ($t.IsFaulted) { $fallidas += "subida $($i + 1): $($t.Exception.InnerException.Message)" }
        elseif ([int]$t.Result.StatusCode -ne 204) {
            $fallidas += "subida $($i + 1): el bucket respondio $([int]$t.Result.StatusCode) $($t.Result.Content.ReadAsStringAsync().GetAwaiter().GetResult())"
        }
    }
    $flujos | ForEach-Object { $_.Dispose() }

    # --- confirmar ------------------------------------------------------------------------
    $disponibles = 0
    foreach ($id in $adjuntos) {
        $c = Invoke-Api -Metodo POST -Ruta "/api/attachments/$id/confirm" -SinFallar
        if ($c.Estado -eq 200 -and $c.Json.status -eq 2) { $disponibles++ }
    }
    Start-Sleep -Seconds 20   # lineas de memoria de despues

    # --- informe --------------------------------------------------------------------------
    $muestras = @(Receive-Job $memoria | ForEach-Object {
            $partes = $_ -split '\|', 2
            if ($partes[1] -match '\s(\d+)Mi\s*$') { [pscustomobject]@{ Hora = [datetime]$partes[0]; Mi = [int]$Matches[1] } }
        })
    $antes = @($muestras | Where-Object { $_.Hora -lt $inicioCarga } | ForEach-Object { $_.Mi })
    $durante = @($muestras | Where-Object { $_.Hora -ge $inicioCarga -and $_.Hora -le $finCarga.AddSeconds(15) } | ForEach-Object { $_.Mi })
    $segundos = ($finCarga - $inicioCarga).TotalSeconds
    $mb = $Subidas * $TamanoMb

    Write-Host ""
    Write-Host ("  Subidas: {0} de {1} llegaron al bucket y {2} quedaron disponibles ({3} MB en {4:N0} s, {5:N1} MB/s)." -f
        ($Subidas - $fallidas.Count), $Subidas, $disponibles, $mb, $segundos, ($mb / [Math]::Max($segundos, 1))) -ForegroundColor Cyan
    $fallidas | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    if ($antes -and $durante) {
        $maxAntes = ($antes | Measure-Object -Maximum).Maximum
        $maxDurante = ($durante | Measure-Object -Maximum).Maximum
        Write-Host ("  Memoria de la API: {0} Mi antes, {1} Mi como maximo durante la carga ({2:+0;-0;0} Mi)." -f $maxAntes, $maxDurante, ($maxDurante - $maxAntes))
    } else {
        Write-Host "  Memoria de la API: sin muestras suficientes (kubectl top tarda ~15 s en actualizar)." -ForegroundColor Yellow
    }
    Write-Host ("  Lista de comprobantes: mediana {0:N0} ms sin carga, {1:N0} ms durante ({2} mediciones; maximo {3:N0} ms)." -f
        (Get-Mediana $sinCarga), (Get-Mediana $conCarga), $conCarga.Count, (($conCarga + 0) | Measure-Object -Maximum).Maximum)
    Write-Host "  SC-002 pide que la memoria no se mueva y que la otra pantalla responda como sin carga." -ForegroundColor DarkGray
} finally {
    if ($memoria) { Stop-Job $memoria -ErrorAction SilentlyContinue; Remove-Job $memoria -Force -ErrorAction SilentlyContinue }
    if (-not $Conservar -and $adjuntos.Count -gt 0 -and $script:bearer) {
        $borrados = 0
        foreach ($id in $adjuntos) {
            try { if ((Invoke-Api -Metodo DELETE -Ruta "/api/attachments/$id" -SinFallar).Estado -in 200, 204) { $borrados++ } } catch { }
        }
        Write-Host ("  Adjuntos de prueba borrados: {0} de {1} (quedan 90 dias en la papelera del bucket)." -f $borrados, $adjuntos.Count) -ForegroundColor DarkGray
    }
    $script:bearer = $null
    if ($archivo -and (Test-Path $archivo)) { Remove-Item $archivo -ErrorAction SilentlyContinue }
    if ($tunel -and -not $tunel.HasExited) { Stop-Process -Id $tunel.Id -ErrorAction SilentlyContinue }
    Write-Host ""
}
