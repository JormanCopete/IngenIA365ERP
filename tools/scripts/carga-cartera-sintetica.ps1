# =============================================================================
# carga-cartera-sintetica.ps1 - Volumen de Cartera en DEV y QA (feature 013, fase 24, T1070).
#
# Mide la Cartera con volumen real en una cooperativa de ensayo de DEV o QA, sembrando
# TODO por las rutas de la API, nunca por SQL (molde: carga-inventario-valorizado.ps1):
#
#   1. abre un tunel SSH al Service erp-api del ambiente (DEV y QA estan detras de
#      Cloudflare Access: desde fuera del navegador no se llega a la API), o usa -Api;
#   2. inicia sesion con SU usuario (correo, contrasena y el codigo de su app de
#      autenticacion; se piden aqui y no se guardan ni se imprimen);
#   3. lee personas EXISTENTES de la cooperativa (GET /api/core/people): no crea personas,
#      igual que la carga real (la migracion no tiene plantilla de personas);
#   4. arma las cuatro partes obligatorias de la carga de migracion -creditos, cuotas,
#      saldos y riesgo- con N creditos sinteticos repartidos entre esas personas, y las
#      sube en varios archivos por parte (CSV con encabezados en la fila 1), con la
#      misma mecanica que la pantalla /cartera/migracion (revisar y aplicar cada archivo);
#   5. aplica la carga (POST /api/lending/migration/loads/{id}/apply) e informa los tiempos;
#   6. con -MedirInformes (Cartera ya activada), mide las vistas aging, quality y
#      vintages del centro de informes y el tablero.
#
# Activar NO lo hace este guion: la activacion exige el cuadre aceptado con el punto de
# un acta y la decide una persona (docs/operaciones/cartera-puesta-en-marcha.md). Con la
# carga activada, la causacion y el cierre se miden desde /cartera/cierres y las corridas
# del proceso (GET /api/lending/process-runs), con el volumen que este guion dejo.
#
# Lo que siembra SE QUEDA en la cooperativa: una carga aplicada sin activar exige en Nomina
# reconocer "Cartera por activar" y Personas no deja borrar a esos deudores; otra carga con el
# mismo corte la reemplaza. Uselo sobre una cooperativa de ensayo, NUNCA en produccion ni en la
# de COOFLOPAL: el guion solo admite -Ambiente dev o qa.
#
# Necesita: Tailscale conectado, la llave SSH de despliegue, un usuario de la cooperativa con
# Lending.Migration.Load y Core.People.View (y Lending.Reports.View, Lending.Loans.Read y
# Lending.Dashboard.View para -MedirInformes), la linea de credito y la oficina vinculada a su
# sucursal contable ya creadas, la contabilidad iniciada con su apertura al corte, y un segundo
# factor TOTP (con solo passkey no se puede desde aqui). Ninguna credencial vive en el guion.
#
# Uso:
#   .\tools\scripts\carga-cartera-sintetica.ps1 -Cooperativa ensayo -Linea CONSUMO -Oficina "Agencia principal"
#   .\tools\scripts\carga-cartera-sintetica.ps1 -Ambiente dev -Creditos 5000 -Cuotas 24 -Linea CONSUMO -Oficina Principal
#   .\tools\scripts\carga-cartera-sintetica.ps1 -Cooperativa ensayo -SoloMedir -MedirInformes
#   .\tools\scripts\carga-cartera-sintetica.ps1 -Api http://localhost:5000 -Creditos 500 -Linea CONSUMO -Oficina Principal
# =============================================================================
param(
    [ValidateSet('dev', 'qa')]
    [string]$Ambiente = 'qa',
    # Nombre (o parte) de la cooperativa, si su usuario tiene varias.
    [string]$Cooperativa,
    [ValidateRange(1, 200000)]
    [int]$Creditos = 50000,
    # Cuotas pendientes de cada credito (50.000 x 30 = 1,5 millones de cuotas, el volumen de C60).
    [ValidateRange(1, 360)]
    [int]$Cuotas = 30,
    # Codigo de la linea de credito de los creditos (tiene que existir en la cooperativa).
    [string]$Linea,
    # Nombre de la agencia contable vinculada a la oficina (lo que lleva la plantilla de creditos).
    [string]$Oficina,
    # Cuantas personas existentes se usan como deudores (los creditos se reparten entre ellas).
    [ValidateRange(1, 50000)]
    [int]$Deudores = 500,
    # Prefijo del numero de credito: PREFIJO-000001.
    [ValidatePattern('^[A-Z0-9]{1,6}$')]
    [string]$Prefijo = 'VOL',
    # Corte de la carga; por defecto el ultimo dia del mes anterior.
    [Nullable[datetime]]$FechaDeCorte,
    [ValidateRange(1, 20000)]
    [int]$CreditosPorArchivo = 10000,
    [switch]$SoloMedir,
    [switch]$MedirInformes,
    [ValidateRange(1, 20)]
    [int]$Mediciones = 3,
    # Una API ya alcanzable (p. ej. http://localhost:5000 en desarrollo): sin tunel ni Kubernetes.
    [string]$Api,
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'
$maquina = '100.94.218.42'
$ns = "erp-$Ambiente"
$motivo = 'Carga de volumen de Cartera (carga-cartera-sintetica.ps1)'
$hoy = Get-Date
if (-not $FechaDeCorte) { $FechaDeCorte = (Get-Date -Year $hoy.Year -Month $hoy.Month -Day 1).AddDays(-1) }
$corte = $FechaDeCorte.ToString('yyyy-MM-dd')
$desembolso = $FechaDeCorte.AddMonths(-2).ToString('yyyy-MM-dd')
if (-not $SoloMedir -and (-not $Linea -or -not $Oficina)) { throw "Indique -Linea (codigo de la linea) y -Oficina (agencia contable vinculada) para armar la carga." }
$cultura = [Globalization.CultureInfo]::InvariantCulture

Add-Type -AssemblyName System.Net.Http
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

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
$script:refresh = $null
$script:renovada = Get-Date
$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromMinutes(60)

function Send-Pedido {
    # Envia y, ante un 429 del limitador, espera y reintenta el MISMO pedido: la clave de operacion se conserva,
    # asi que un reintento nunca hace dos veces lo mismo.
    param([scriptblock]$Crear)
    for ($intento = 1; ; $intento++) {
        $respuesta = $http.SendAsync((& $Crear)).GetAwaiter().GetResult()
        if ([int]$respuesta.StatusCode -ne 429 -or $intento -ge 5) { return $respuesta }
        Write-Host "  El limitador de la API pidio esperar; reintento en 60 s ..." -ForegroundColor DarkYellow
        Start-Sleep -Seconds 60
    }
}

function Read-Respuesta {
    param($Respuesta, [string]$Descripcion, [switch]$SinFallar)
    $texto = $Respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $json = $null
    if ($texto) { try { $json = $texto | ConvertFrom-Json } catch { } }
    if (-not $Respuesta.IsSuccessStatusCode -and -not $SinFallar) {
        $motivoError = if ($json -and $json.code) { "$($json.code): $($json.message)" } else { $texto }
        throw ("{0} respondio {1}. {2}" -f $Descripcion, [int]$Respuesta.StatusCode, $motivoError)
    }
    return [pscustomobject]@{ Estado = [int]$Respuesta.StatusCode; Json = $json }
}

function Invoke-Api {
    param([string]$Metodo, [string]$Ruta, $Cuerpo, [string]$Token = $script:bearer, [switch]$SinFallar)
    $clave = [guid]::NewGuid().ToString()
    $json = if ($null -ne $Cuerpo) { $Cuerpo | ConvertTo-Json -Depth 6 -Compress } else { $null }
    $respuesta = Send-Pedido {
        $pedido = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Metodo)), "$script:api$Ruta"
        if ($Token) { $pedido.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer', $Token }
        if ($Metodo -ne 'GET') { $pedido.Headers.Add('Idempotency-Key', $clave) }
        if ($null -ne $json) { $pedido.Content = New-Object System.Net.Http.StringContent ($json, [Text.Encoding]::UTF8, 'application/json') }
        $pedido
    }
    return Read-Respuesta $respuesta "$Metodo $Ruta" -SinFallar:$SinFallar
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
            return [pscustomobject]@{ Token = $r.Json.accessToken; Refresh = $r.Json.refreshToken; Cooperativa = $r.Json.tenant.tenantName }
        }
        'None' {
            if (-not $Resultado.accessToken) { throw "El ingreso no devolvio una sesion." }
            $sesion = [pscustomobject]@{ Token = $Resultado.accessToken; Refresh = $Resultado.refreshToken; Cooperativa = $Resultado.activeTenantName }
            if ($Cooperativa -and $sesion.Cooperativa -notlike "*$Cooperativa*") {
                $elegida = Select-Cooperativa @($Resultado.activeTenants)
                $r = Invoke-Api -Metodo POST -Ruta '/api/sessions/switch-tenant' -Cuerpo @{ tenantPublicId = $elegida.tenantPublicId } -Token $sesion.Token
                $sesion = [pscustomobject]@{ Token = $r.Json.accessToken; Refresh = $(if ($r.Json.refreshToken) { $r.Json.refreshToken } else { $sesion.Refresh }); Cooperativa = $elegida.tenantName }
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

function Update-Sesion {
    # El access dura 15 minutos y la siembra tarda mas: se rota por /api/auth/refresh cada diez, como hace la pestana activa.
    if (-not $script:refresh -or ((Get-Date) - $script:renovada).TotalMinutes -lt 10) { return }
    $r = Invoke-Api -Metodo POST -Ruta '/api/auth/refresh' -Cuerpo @{ refreshToken = $script:refresh } -Token $null
    $script:bearer = $r.Json.accessToken
    $script:refresh = $r.Json.refreshToken
    $script:renovada = Get-Date
}

# --- las partes de la carga, en CSV --------------------------------------------------
# Encabezados en la fila 1 con los nombres de contracts/plantillas.md 5 (los de DefinicionesDeMigracion.Columnas);
# separador ';'. Solo van las columnas obligatorias y las que el guion llena: una columna opcional ausente no es error.
function New-Csv {
    param([string[]]$Encabezados, [System.Collections.Generic.List[string[]]]$Filas)
    $sb = New-Object Text.StringBuilder
    [void]$sb.AppendLine(($Encabezados -join ';'))
    foreach ($f in $Filas) { [void]$sb.AppendLine(($f -join ';')) }
    return ,(New-Object Text.UTF8Encoding $false).GetBytes($sb.ToString())
}

function Import-Parte {
    # POST /api/lending/migration/loads/{id}/parts/{parte}/import?mode=review|apply: cada archivo es una tanda de la parte.
    param([string]$Carga, [string]$Parte, [byte[]]$Contenido, [string]$Archivo)
    foreach ($modo in 'review', 'apply') {
        Update-Sesion
        $clave = [guid]::NewGuid().ToString()
        $reloj = [Diagnostics.Stopwatch]::StartNew()
        $respuesta = Send-Pedido {
            $formulario = New-Object System.Net.Http.MultipartFormDataContent
            $contenidoDelArchivo = New-Object System.Net.Http.ByteArrayContent (, $Contenido)
            $contenidoDelArchivo.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/csv')
            $formulario.Add($contenidoDelArchivo, 'file', $Archivo)
            $formulario.Add((New-Object System.Net.Http.StringContent $motivo), 'reason')
            $pedido = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::Post), "$script:api/api/lending/migration/loads/$Carga/parts/$Parte/import?mode=$modo"
            $pedido.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer', $script:bearer
            $pedido.Headers.Add('Idempotency-Key', $clave)
            $pedido.Content = $formulario
            $pedido
        }
        $r = Read-Respuesta $respuesta "Parte $Parte ($Archivo, $modo)"
        if ($modo -eq 'review' -and $r.Json.PSObject.Properties['valid'] -and -not $r.Json.valid) {
            $errores = @($r.Json.errors | Select-Object -First 10 | ForEach-Object { "fila $($_.row), $($_.column): $($_.code) $($_.message)" })
            throw ("La revision de {0} ({1}) trae errores:`n    {2}" -f $Parte, $Archivo, ($errores -join "`n    "))
        }
        Write-Host ("  {0} {1}: {2} en {3:N1} s." -f $Parte, $Archivo, $(if ($modo -eq 'review') { 'revisada' } else { 'aplicada' }), $reloj.Elapsed.TotalSeconds) -ForegroundColor DarkGray
    }
}

function Get-Mediana { param([double[]]$v) $o = @($v | Sort-Object); if (-not $o) { return 0 }; return $o[[int][Math]::Floor(($o.Count - 1) / 2)] }

$tunel = $null
try {
    Write-Host ""
    Write-Host ("  Cartera sintetica en {0}: {1:N0} creditos de {2} cuotas sobre {3:N0} deudores existentes; corte {4}" -f
        $(if ($Api) { $Api } else { $ns }), $Creditos, $Cuotas, $Deudores, $corte) -ForegroundColor Cyan

    # --- tunel ------------------------------------------------------------------
    if ($Api) { $script:api = $Api.TrimEnd('/') } else {
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
    }

    # --- sesion -------------------------------------------------------------------
    $correo = Read-Host "  Correo"
    $clave = Read-Host "  Contrasena" -AsSecureString
    $login = Invoke-Api -Metodo POST -Ruta '/api/auth/login' -Cuerpo @{ email = $correo.Trim(); password = (ConvertFrom-Segura $clave) } -Token $null
    $clave = $null
    $sesion = Resolve-Sesion $login.Json
    $script:bearer = $sesion.Token
    Write-Host ("  Sesion iniciada en {0}." -f $sesion.Cooperativa) -ForegroundColor DarkGray
    $script:refresh = $sesion.Refresh
    $script:renovada = Get-Date

    if (-not $SoloMedir) {
        $inicio = Get-Date

        # --- deudores: personas que ya existen en la cooperativa ---------------------------
        $documentos = New-Object 'System.Collections.Generic.List[string]'
        for ($pagina = 1; $documentos.Count -lt $Deudores; $pagina++) {
            Update-Sesion
            $lote = (Invoke-Api -Metodo GET -Ruta "/api/core/people?PageNumber=$pagina&PageSize=200").Json
            foreach ($p in @($lote.items)) { if ($p.taxId -and $documentos.Count -lt $Deudores) { $documentos.Add([string]$p.taxId) } }
            if (@($lote.items).Count -lt 200) { break }
        }
        if ($documentos.Count -eq 0) { throw "La cooperativa no tiene personas: registrelas antes (la migracion no crea personas)." }
        Write-Host ("  {0:N0} deudores existentes." -f $documentos.Count) -ForegroundColor DarkGray

        # --- la carga en borrador ------------------------------------------------------------
        $carga = (Invoke-Api -Metodo POST -Ruta '/api/lending/migration/loads' -Cuerpo @{ cutoffDate = $corte; notes = $motivo }).Json.publicId
        Write-Host "  Carga $carga creada con corte $corte." -ForegroundColor DarkGray

        $capitalPorCuota = 100000
        $numeros = @(1..$Creditos | ForEach-Object { '{0}-{1:000000}' -f $Prefijo, $_ })
        $archivo = 0
        for ($desde = 0; $desde -lt $Creditos; $desde += $CreditosPorArchivo) {
            $archivo++
            $hasta = [Math]::Min($Creditos, $desde + $CreditosPorArchivo) - 1
            $creditosCsv = New-Object 'System.Collections.Generic.List[string[]]'
            $saldosCsv = New-Object 'System.Collections.Generic.List[string[]]'
            $riesgoCsv = New-Object 'System.Collections.Generic.List[string[]]'
            for ($k = $desde; $k -le $hasta; $k++) {
                $n = $numeros[$k]
                $deudor = $documentos[$k % $documentos.Count]
                $creditosCsv.Add([string[]]@($n, $deudor, $Oficina, $Linea, $desembolso, ($capitalPorCuota * ($Cuotas + 2)).ToString($cultura),
                    'Fixed', '22', 'RelativeToCurrent', '1,5', 'Monthly', ($Cuotas + 2).ToString($cultura), 'Cashier', 'no'))
                $saldosCsv.Add([string[]]@($n, 'Principal', ($capitalPorCuota * $Cuotas).ToString($cultura)))
                $riesgoCsv.Add([string[]]@($n, '0', 'A', '0', 'no'))
            }
            Import-Parte $carga 'loans' (New-Csv @('numero', 'deudor', 'oficina', 'linea', 'fechaDeDesembolso', 'montoDesembolsado', 'tipoDeTasa',
                    'tasaCorrienteEA', 'tipoDeTasaDeMora', 'factorDeMora', 'periodicidad', 'plazo', 'formaDePago', 'enJuridico') $creditosCsv) "creditos-$archivo.csv"
            Import-Parte $carga 'balances' (New-Csv @('numeroDeCredito', 'concepto', 'valor') $saldosCsv) "saldos-$archivo.csv"
            Import-Parte $carga 'risk-data' (New-Csv @('numeroDeCredito', 'diasDeMora', 'calificacion', 'deterioroCapital', 'enSuspenso') $riesgoCsv) "riesgo-$archivo.csv"
        }
        # Las cuotas, en archivos de hasta 60.000 filas (el tope del lector).
        $creditosPorArchivoDeCuotas = [Math]::Max(1, [int][Math]::Floor(60000 / $Cuotas))
        $archivo = 0
        for ($desde = 0; $desde -lt $Creditos; $desde += $creditosPorArchivoDeCuotas) {
            $archivo++
            $hasta = [Math]::Min($Creditos, $desde + $creditosPorArchivoDeCuotas) - 1
            $cuotasCsv = New-Object 'System.Collections.Generic.List[string[]]'
            for ($k = $desde; $k -le $hasta; $k++) {
                for ($c = 1; $c -le $Cuotas; $c++) {
                    $cuotasCsv.Add([string[]]@($numeros[$k], $c.ToString($cultura), $FechaDeCorte.AddDays(15).AddMonths($c - 1).ToString('yyyy-MM-dd'),
                        $capitalPorCuota.ToString($cultura)))
                }
            }
            Import-Parte $carga 'installments' (New-Csv @('numeroDeCredito', 'cuota', 'fechaDeVencimiento', 'capital') $cuotasCsv) "cuotas-$archivo.csv"
        }
        Write-Host ("  Partes subidas en {0:N1} min." -f ((Get-Date) - $inicio).TotalMinutes) -ForegroundColor Cyan

        # --- aplicar la carga --------------------------------------------------------------
        Update-Sesion
        $reloj = [Diagnostics.Stopwatch]::StartNew()
        $aplicada = Invoke-Api -Metodo POST -Ruta "/api/lending/migration/loads/$carga/apply" -Cuerpo @{ reason = $motivo }
        $reloj.Stop()
        Write-Host ("  Carga aplicada en {0:N1} s (estado {1}). Revise el cuadre y las diferencias en /cartera/migracion antes de activar." -f
            $reloj.Elapsed.TotalSeconds, $aplicada.Json.status) -ForegroundColor Green
    }

    # --- las mediciones de informes (Cartera activada) ------------------------------------
    if ($MedirInformes) {
        foreach ($ruta in '/api/reports/lending/aging', '/api/reports/lending/quality', '/api/reports/lending/vintages', '/api/lending/dashboard') {
            $tiempos = @()
            for ($m = 1; $m -le $Mediciones; $m++) {
                Update-Sesion
                $reloj = [Diagnostics.Stopwatch]::StartNew()
                [void](Invoke-Api -Metodo GET -Ruta $ruta)
                $reloj.Stop()
                $tiempos += $reloj.Elapsed.TotalSeconds
            }
            Write-Host ("  {0}: mediana {1:N1} s, maximo {2:N1} s de {3} mediciones." -f $ruta, (Get-Mediana $tiempos),
                ($tiempos | Measure-Object -Maximum).Maximum, $Mediciones)
        }
    }
    Write-Host "  Anote la medicion en las notas de la entrega y en T1071 de specs/013-cartera-financiera/tasks.md (ambiente, fecha y volumen)." -ForegroundColor DarkGray
} finally {
    $script:bearer = $null
    if ($tunel -and -not $tunel.HasExited) { Stop-Process -Id $tunel.Id -ErrorAction SilentlyContinue }
    Write-Host ""
}
