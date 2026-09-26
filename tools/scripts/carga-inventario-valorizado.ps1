# =============================================================================
# carga-inventario-valorizado.ps1 - Volumen del inventario valorizado (feature 012, SC-017, T995).
#
# SC-017: con 50.000 productos en 50 bodegas, el inventario valorizado a una fecha
# pasada esta listo en menos de 30 segundos. Este guion lo mide en QA (o DEV) sobre
# una cooperativa de ensayo, sembrando TODO por las rutas de la API, nunca por SQL:
#
#   1. abre un tunel SSH al Service erp-api del ambiente (DEV y QA estan detras de
#      Cloudflare Access: desde fuera del navegador no se llega a la API);
#   2. inicia sesion con SU usuario (correo, contrasena y el codigo de su app de
#      autenticacion; se piden aqui y no se guardan ni se imprimen);
#   3. carga por las plantillas 2, 5 y 6 un grupo contable, una categoria y los
#      productos (revision y aplicacion, como la pantalla);
#   4. crea las bodegas (POST /warehouses; la primera de la sucursal trae su transito)
#      y las activa a la fecha de corte aceptando la diferencia con motivo —fuera de
#      produccion se puede: sin la consulta contable de I2, produccion responde
#      Inventory.Activation.AccountingUnavailable—;
#   5. deja kardex en cada bodega con un ajuste positivo de hasta 4.000 lineas fechado
#      el dia siguiente al corte (un producto cae en varias bodegas: 50 x 4.000 =
#      200.000 lineas de kardex con los valores por defecto);
#   6. mide GET /api/reports/inventory/valuation?asOf=<fecha pasada> varias veces
#      (json, sin y con transito) e informa cada tiempo, la mediana y el maximo.
#
# Lo que siembra SE QUEDA en la cooperativa (el inventario no se borra: se anula con
# documentos contrarios). Uselo sobre una cooperativa de ensayo, nunca la de COOFLOPAL.
# Repetirlo no duplica: las plantillas actualizan lo que ya existe, una bodega que ya
# existe no se vuelve a crear ni a activar, y una bodega que ya tiene ajustes de la
# carga no recibe otro. -SoloMedir se salta la siembra.
#
# Necesita: Tailscale conectado, la llave SSH de despliegue, un usuario de la
# cooperativa con los permisos de administracion de Inventario (Catalog.Manage,
# Warehouses.Manage/Activate/AcceptActivationDifference, Adjustments.Create,
# Costs.Read, Reports.View y alcance total de bodegas), y un segundo factor TOTP
# (con solo passkey no se puede desde aqui).
#
# Uso:
#   .\tools\scripts\carga-inventario-valorizado.ps1 -Cooperativa ensayo
#   .\tools\scripts\carga-inventario-valorizado.ps1 -Ambiente dev -Productos 5000 -Bodegas 5 -LineasPorBodega 1000
#   .\tools\scripts\carga-inventario-valorizado.ps1 -Cooperativa ensayo -SoloMedir -Mediciones 5
#   .\tools\scripts\carga-inventario-valorizado.ps1 -Api http://localhost:5000 -Productos 2000 -Bodegas 2 -LineasPorBodega 500
# =============================================================================
param(
    [ValidateSet('dev', 'qa')]
    [string]$Ambiente = 'qa',
    # Nombre (o parte) de la cooperativa, si su usuario tiene varias.
    [string]$Cooperativa,
    [ValidateRange(1, 200000)]
    [int]$Productos = 50000,
    [ValidateRange(1, 99)]
    [int]$Bodegas = 50,
    # Lineas del ajuste de cada bodega (el documento admite hasta 4.000, InventoryDocument.MaxLineas).
    [ValidateRange(1, 4000)]
    [int]$LineasPorBodega = 4000,
    # Prefijo de todo lo sembrado: productos PREFIJO00001, bodegas PREFIJOB01, grupo y categoria PREFIJO.
    [ValidatePattern('^[A-Z0-9]{1,5}$')]
    [string]$Prefijo = 'CARGA',
    # Nombre o codigo de la sucursal de las bodegas; vacio = la primera que devuelva la API.
    [string]$Sucursal,
    # Corte de las bodegas; por defecto el ultimo dia de hace tres meses.
    [Nullable[datetime]]$FechaDeCorte,
    # La fecha pasada del valorizado; por defecto el ultimo dia del mes anterior.
    [Nullable[datetime]]$FechaDeValorizado,
    [ValidateRange(1, 20)]
    [int]$Mediciones = 3,
    [switch]$SoloMedir,
    # Una API ya alcanzable (p. ej. http://localhost:5000 en desarrollo): sin tunel ni Kubernetes.
    [string]$Api,
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'
$maquina = '100.94.218.42'
$ns = "erp-$Ambiente"
$motivo = 'Carga de volumen SC-017 (carga-inventario-valorizado.ps1)'
$hoy = Get-Date
if (-not $FechaDeCorte) { $FechaDeCorte = (Get-Date -Year $hoy.Year -Month $hoy.Month -Day 1).AddMonths(-2).AddDays(-1) }
if (-not $FechaDeValorizado) { $FechaDeValorizado = (Get-Date -Year $hoy.Year -Month $hoy.Month -Day 1).AddDays(-1) }
$corte = $FechaDeCorte.ToString('yyyy-MM-dd')
$entrada = $FechaDeCorte.AddDays(1).ToString('yyyy-MM-dd')
$asOf = $FechaDeValorizado.ToString('yyyy-MM-dd')
if ($FechaDeValorizado -le $FechaDeCorte.AddDays(1)) { throw "La fecha del valorizado ($asOf) tiene que ser posterior a la de las entradas ($entrada)." }

Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression
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
$http.Timeout = [TimeSpan]::FromMinutes(30)

function Send-Pedido {
    # Envia y, ante un 429 del limitador (1.000 por minuto y por IP), espera y reintenta el MISMO pedido: la clave
    # de operacion se conserva, asi que un reintento nunca hace dos veces lo mismo.
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

# --- libros .xlsx sin dependencias -------------------------------------------------
# Un .xlsx minimo (celdas de texto en linea) que lee ClosedXML: las plantillas llevan los encabezados en la fila 1
# (contracts/plantillas.md §0.3) y todo valor viaja como texto, igual que al diligenciarlas a mano.
function Get-Columna { param([int]$Indice) $letras = ''; $n = $Indice + 1; while ($n -gt 0) { $r = ($n - 1) % 26; $letras = [char](65 + $r) + $letras; $n = [int][Math]::Floor(($n - 1) / 26) }; return $letras }

function New-Libro {
    # $Hojas: lista ordenada de @{ Nombre = 'Datos'; Encabezados = @(...); Filas = List[string[]] }
    param([object[]]$Hojas)
    $memoria = New-Object IO.MemoryStream
    $zip = New-Object IO.Compression.ZipArchive ($memoria, [IO.Compression.ZipArchiveMode]::Create, $true)
    function Add-Parte([string]$Ruta, [string]$Texto) {
        $entrada = $zip.CreateEntry($Ruta)
        $flujo = $entrada.Open()
        try { $bytes = (New-Object Text.UTF8Encoding $false).GetBytes($Texto); $flujo.Write($bytes, 0, $bytes.Length) } finally { $flujo.Dispose() }
    }
    $hojasXml = ''; $relsXml = ''; $tipos = ''
    for ($h = 0; $h -lt $Hojas.Count; $h++) {
        $id = $h + 1
        $hojasXml += "<sheet name=`"$([Security.SecurityElement]::Escape($Hojas[$h].Nombre))`" sheetId=`"$id`" r:id=`"rId$id`"/>"
        $relsXml += "<Relationship Id=`"rId$id`" Type=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet`" Target=`"worksheets/sheet$id.xml`"/>"
        $tipos += "<Override PartName=`"/xl/worksheets/sheet$id.xml`" ContentType=`"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml`"/>"
        $sb = New-Object Text.StringBuilder
        [void]$sb.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>')
        $todas = @(, [string[]]$Hojas[$h].Encabezados) + @($Hojas[$h].Filas)
        for ($f = 0; $f -lt $todas.Count; $f++) {
            $fila = $f + 1
            [void]$sb.Append("<row r=`"$fila`">")
            $celdas = $todas[$f]
            for ($c = 0; $c -lt $celdas.Count; $c++) {
                if ($null -eq $celdas[$c] -or $celdas[$c] -eq '') { continue }
                [void]$sb.Append("<c r=`"$(Get-Columna $c)$fila`" t=`"inlineStr`"><is><t>$([Security.SecurityElement]::Escape([string]$celdas[$c]))</t></is></c>")
            }
            [void]$sb.Append('</row>')
        }
        [void]$sb.Append('</sheetData></worksheet>')
        Add-Parte "xl/worksheets/sheet$id.xml" $sb.ToString()
    }
    Add-Parte '[Content_Types].xml' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' + $tipos + '</Types>')
    Add-Parte '_rels/.rels' '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
    Add-Parte 'xl/workbook.xml' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>' + $hojasXml + '</sheets></workbook>')
    Add-Parte 'xl/_rels/workbook.xml.rels' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' + $relsXml + '<Relationship Id="rIdS" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>')
    Add-Parte 'xl/styles.xml' '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs></styleSheet>'
    $zip.Dispose()
    return ,$memoria.ToArray()
}

function Import-Plantilla {
    # Revisar y, sin errores, aplicar (POST {ruta}/import?mode=review|apply), cada vez con su clave (plantillas.md §0.5).
    param([string]$Ruta, [byte[]]$Libro, [string]$Descripcion)
    foreach ($modo in 'review', 'apply') {
        $clave = [guid]::NewGuid().ToString()
        $reloj = [Diagnostics.Stopwatch]::StartNew()
        $respuesta = Send-Pedido {
            $formulario = New-Object System.Net.Http.MultipartFormDataContent
            $archivo = New-Object System.Net.Http.ByteArrayContent (, $Libro)
            $archivo.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
            $formulario.Add($archivo, 'file', 'plantilla.xlsx')
            $formulario.Add((New-Object System.Net.Http.StringContent $motivo), 'reason')
            $pedido = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::Post), "$script:api$Ruta/import?mode=$modo"
            $pedido.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer', $script:bearer
            $pedido.Headers.Add('Idempotency-Key', $clave)
            $pedido.Content = $formulario
            $pedido
        }
        $r = Read-Respuesta $respuesta "Plantilla $Descripcion ($modo)"
        if ($modo -eq 'review' -and -not $r.Json.valid) {
            $errores = @($r.Json.errors | Select-Object -First 10 | ForEach-Object { "fila $($_.row), $($_.column): $($_.code) $($_.message)" })
            throw ("La revision de {0} trae errores:`n    {1}" -f $Descripcion, ($errores -join "`n    "))
        }
        Write-Host ("  Plantilla {0}: {1} en {2:N1} s." -f $Descripcion, $(if ($modo -eq 'review') { 'revisada' } else { 'aplicada' }), $reloj.Elapsed.TotalSeconds) -ForegroundColor DarkGray
    }
}

function Update-Sesion {
    # El access dura 15 minutos y la siembra tarda mas: se rota por /api/auth/refresh cada diez, como hace la pestana
    # activa (una rotacion no cuenta como actividad, pero el guion esta trabajando de verdad).
    if (-not $script:refresh -or ((Get-Date) - $script:renovada).TotalMinutes -lt 10) { return }
    $r = Invoke-Api -Metodo POST -Ruta '/api/auth/refresh' -Cuerpo @{ refreshToken = $script:refresh } -Token $null
    $script:bearer = $r.Json.accessToken
    $script:refresh = $r.Json.refreshToken
    $script:renovada = Get-Date
}

function Get-Mediana { param([double[]]$v) $o = @($v | Sort-Object); if (-not $o) { return 0 }; return $o[[int][Math]::Floor(($o.Count - 1) / 2)] }

$tunel = $null
try {
    Write-Host ""
    Write-Host ("  Inventario valorizado en {0}: {1:N0} productos, {2} bodegas, {3:N0} lineas por bodega; corte {4}, valorizado al {5}" -f
        $ns, $Productos, $Bodegas, $LineasPorBodega, $corte, $asOf) -ForegroundColor Cyan

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
        $inicioSiembra = Get-Date

        # --- catalogo por las plantillas 2, 5 y 6 ---------------------------------------
        Import-Plantilla '/api/inventory/accounting-groups' (New-Libro @(@{ Nombre = 'Datos'; Encabezados = @('codigo', 'nombre'); Filas = @(, [string[]]@($Prefijo, "Carga de volumen $Prefijo")) })) 'grupos contables'
        Import-Plantilla '/api/inventory/product-categories' (New-Libro @(@{ Nombre = 'Datos'; Encabezados = @('codigo', 'nombre'); Filas = @(, [string[]]@($Prefijo, "Carga de volumen $Prefijo")) })) 'categorias'
        $filas = New-Object 'System.Collections.Generic.List[string[]]'
        for ($i = 1; $i -le $Productos; $i++) {
            $filas.Add([string[]]@(('{0}{1:00000}' -f $Prefijo, $i), ('ARTICULO DE CARGA {0:00000}' -f $i), 'Inventoriable', $Prefijo, 'UND', $Prefijo, 'Excluded', 'COMPRAS'))
        }
        Import-Plantilla '/api/inventory/products' (New-Libro @(@{ Nombre = 'Productos'; Encabezados = @('codigo', 'nombre', 'tipo', 'categoria', 'unidadBase', 'grupoContable', 'tratamientoIva', 'conceptoRetencion'); Filas = $filas })) ("productos ({0:N0})" -f $Productos)

        # Los PublicId de los productos, por el grupo contable de la carga (paginas de 200, el maximo).
        Update-Sesion
        $grupo = @((Invoke-Api -Metodo GET -Ruta '/api/inventory/accounting-groups').Json | Where-Object { $_.code -eq $Prefijo })[0]
        $und = @((Invoke-Api -Metodo GET -Ruta '/api/inventory/units').Json | Where-Object { $_.code -eq 'UND' })[0]
        if (-not $grupo -or -not $und) { throw "No aparecen el grupo contable $Prefijo o la unidad UND despues de la carga." }
        $idsDeProducto = New-Object 'System.Collections.Generic.Dictionary[string,string]'
        for ($pagina = 1; ; $pagina++) {
            $lote = (Invoke-Api -Metodo GET -Ruta "/api/inventory/products?accountingGroupPublicId=$($grupo.publicId)&page=$pagina&pageSize=200").Json
            foreach ($p in @($lote.items)) { $idsDeProducto[$p.code] = $p.publicId }
            if (@($lote.items).Count -lt 200) { break }
        }
        if ($idsDeProducto.Count -lt $Productos) { throw ("Se esperaban {0:N0} productos {1}* y la API devuelve {2:N0}." -f $Productos, $Prefijo, $idsDeProducto.Count) }
        Write-Host ("  {0:N0} productos listos." -f $idsDeProducto.Count) -ForegroundColor DarkGray

        # --- bodegas: crear, activar, kardex ------------------------------------------------
        $sucursales = @((Invoke-Api -Metodo GET -Ruta '/api/core/branches?PageNumber=1&PageSize=100').Json.items)
        $laSucursal = if ($Sucursal) { @($sucursales | Where-Object { $_.name -like "*$Sucursal*" -or $_.code -eq $Sucursal })[0] } else { $sucursales[0] }
        if (-not $laSucursal) { throw "No se encontro la sucursal '$Sucursal'." }
        $tipoPrincipal = @((Invoke-Api -Metodo GET -Ruta '/api/inventory/warehouse-types').Json | Where-Object { $_.code -eq 'PRINCIPAL' })[0]
        $tipoAjuste = @((Invoke-Api -Metodo GET -Ruta '/api/inventory/document-types').Json | Where-Object { $_.code -eq 'AJP' })[0]
        if (-not $tipoPrincipal -or -not $tipoAjuste) { throw "Faltan el tipo de bodega PRINCIPAL o el tipo de documento AJP de la semilla." }

        for ($b = 1; $b -le $Bodegas; $b++) {
            Update-Sesion
            $codigo = '{0}B{1:00}' -f $Prefijo, $b
            $todas = @((Invoke-Api -Metodo GET -Ruta '/api/inventory/warehouses?includeTransit=true&includeInactive=true').Json)
            $bodega = @($todas | Where-Object { $_.code -eq $codigo })[0]
            if (-not $bodega) {
                $conTransito = @($todas | Where-Object { $_.isTransit -and $_.branch.publicId -eq $laSucursal.publicId }).Count -gt 0
                $cuerpo = @{ code = $codigo; name = "Bodega de carga $b"; branchPublicId = $laSucursal.publicId; warehouseTypePublicId = $tipoPrincipal.publicId }
                if (-not $conTransito) { $cuerpo.transitWarehouse = @{ code = "$($Prefijo)TR"; name = "Transito de carga $Prefijo" } }
                $bodega = (Invoke-Api -Metodo POST -Ruta '/api/inventory/warehouses' -Cuerpo $cuerpo).Json.warehouse
            }
            if ($bodega.activationStatus -ne 1) {
                [void](Invoke-Api -Metodo POST -Ruta "/api/inventory/warehouses/$($bodega.publicId)/activation" -Cuerpo @{ cutoffDate = $corte; acceptDifference = $true; reason = $motivo })
            }
            $previos = (Invoke-Api -Metodo GET -Ruta "/api/inventory/adjustments?warehousePublicId=$($bodega.publicId)&documentTypePublicId=$($tipoAjuste.publicId)&pageSize=1").Json
            $cuantos = if ($previos.PSObject.Properties['totalCount']) { [int]$previos.totalCount } else { @($previos.items).Count }
            if ($cuantos -gt 0) { Write-Host "  $codigo ya tiene su ajuste de carga: se deja como esta." -ForegroundColor DarkGray; continue }

            $lineas = New-Object 'System.Collections.Generic.List[object]'
            for ($k = 0; $k -lt $LineasPorBodega; $k++) {
                $indice = ((($b - 1) * $LineasPorBodega + $k) % $Productos) + 1
                $lineas.Add(@{
                        productPublicId = $idsDeProducto[('{0}{1:00000}' -f $Prefijo, $indice)]; unitPublicId = $und.publicId
                        quantity = 1 + ($indice % 50); unitCost = 1000 + ($indice % 97) * 10
                    })
            }
            $reloj = [Diagnostics.Stopwatch]::StartNew()
            $borrador = (Invoke-Api -Metodo POST -Ruta '/api/inventory/adjustments' -Cuerpo @{
                    documentTypePublicId = $tipoAjuste.publicId; warehousePublicId = $bodega.publicId; operationDate = $entrada; reason = $motivo; lines = $lineas
                }).Json
            $confirmado = (Invoke-Api -Metodo POST -Ruta "/api/inventory/adjustments/$($borrador.publicId)/confirm" -Cuerpo @{}).Json
            if ($confirmado.status -ne 2) {
                throw "El ajuste de $codigo quedo en estado $($confirmado.status) (1 = en aprobacion): la politica o el monto maximo de su rol piden aprobarlo. Use un usuario sin limite o apruebelo y vuelva a correr el guion."
            }
            Write-Host ("  {0}: activa al {1}, {2:N0} lineas de kardex en {3:N1} s ({4})." -f $codigo, $corte, $LineasPorBodega, $reloj.Elapsed.TotalSeconds, $confirmado.displayNumber) -ForegroundColor DarkGray
        }
        Write-Host ("  Siembra terminada en {0:N1} min." -f ((Get-Date) - $inicioSiembra).TotalMinutes) -ForegroundColor Cyan
    }

    # --- la medicion ---------------------------------------------------------------------
    $resultados = @()
    foreach ($transito in $false, $true) {
        $tiempos = @()
        for ($m = 1; $m -le $Mediciones; $m++) {
            Update-Sesion
            $ruta = "/api/reports/inventory/valuation?format=json&asOf=$asOf" + $(if ($transito) { '&includeTransit=true' } else { '' })
            $reloj = [Diagnostics.Stopwatch]::StartNew()
            $r = Invoke-Api -Metodo GET -Ruta $ruta
            $reloj.Stop()
            $tiempos += $reloj.Elapsed.TotalSeconds
            $filasDelInforme = @($r.Json.filas).Count
            if ($filasDelInforme -eq 0 -and $r.Json.PSObject.Properties['rows']) { $filasDelInforme = @($r.Json.rows).Count }
            Write-Host ("  Valorizado al {0}{1}, medicion {2}: {3:N1} s ({4:N0} filas)." -f $asOf, $(if ($transito) { ' con transito' } else { '' }), $m, $reloj.Elapsed.TotalSeconds, $filasDelInforme)
        }
        $resultados += [pscustomobject]@{ Transito = $transito; Mediana = (Get-Mediana $tiempos); Maximo = ($tiempos | Measure-Object -Maximum).Maximum }
    }

    Write-Host ""
    foreach ($r in $resultados) {
        $cumple = $r.Maximo -lt 30
        Write-Host ("  Valorizado {0}: mediana {1:N1} s, maximo {2:N1} s de {3} mediciones -> {4}" -f
            $(if ($r.Transito) { 'con transito' } else { 'sin transito' }), $r.Mediana, $r.Maximo, $Mediciones,
            $(if ($cumple) { 'cumple SC-017 (< 30 s)' } else { 'NO cumple SC-017 (< 30 s)' })) -ForegroundColor $(if ($cumple) { 'Green' } else { 'Red' })
    }
    Write-Host "  Anote la medicion en la T995 de specs/012-inventario-comercial/tasks.md (ambiente, fecha y volumen)." -ForegroundColor DarkGray
} finally {
    $script:bearer = $null
    if ($tunel -and -not $tunel.HasExited) { Stop-Process -Id $tunel.Id -ErrorAction SilentlyContinue }
    Write-Host ""
}
