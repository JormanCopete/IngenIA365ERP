# =============================================================================
# crear-secreto-facturacion-electronica.ps1 - Instala la credencial de
# facturacion electronica de UNA cooperativa en UN canal, como una clave del
# Secret de Kubernetes erp-fe-credenciales del ambiente indicado.
#
# Feature 012, entrega I4 (T760; contracts/dian.md seccion 11). Patron de
# crear-secreto-smtp.ps1:
#
#   - Los valores se piden por teclado (ocultos) o llegan por STDIN; viajan por
#     el canal cifrado de SSH usando STDIN (nunca como argumento, asi no
#     aparecen en `ps` ni en el historial del shell) y no se escriben en ningun
#     archivo local, ni en appsettings*, ni en GitOps.
#   - `kubectl patch secret` agrega o reemplaza SOLO la clave
#     {tenantPublicId}.{canal}.json: las credenciales de las demas cooperativas
#     y canales del mismo Secret no se tocan.
#   - En produccion exige escribir PRODUCCION.
#
# El Secret se monta como volumen de directorio (sin subPath) en
# /secrets/facturacion-electronica/: el kubelet refresca el archivo solo, sin
# reiniciar la API (a diferencia de SMTP). La API lo relee al vencer su cache
# de 5 minutos. Despues hay que VERIFICAR la credencial desde la pantalla
# Administracion > Facturacion electronica ("Verificar credencial"), que es lo
# que sella CredentialVerifiedAt.
#
# Que campos lleva el JSON lo decide el adaptador del canal (lo que pida el
# proveedor, p. ej. tokenEmpresa y tokenPassword). Se pasan con -Campos; sin
# -Campos se preguntan uno por uno hasta dejar el nombre vacio. El canal
# SIMULADO acepta cualquier par.
#
# Uso:
#   .\tools\scripts\crear-secreto-facturacion-electronica.ps1 -Ambiente qa -Tenant <guid> -Canal SIMULADO -Campos token
#   .\tools\scripts\crear-secreto-facturacion-electronica.ps1 -Ambiente pdn -Tenant <guid> -Canal PROVEEDOR -Campos tokenEmpresa,tokenPassword
#
# Por STDIN (sin teclado; una linea por campo, en el orden de -Campos):
#   Get-Content valores.txt | .\tools\scripts\crear-secreto-facturacion-electronica.ps1 -Ambiente dev -Tenant <guid> -Canal SIMULADO -Campos token -DesdeStdin
#   (y borrar valores.txt: el guion no deja nada en disco, pero no puede borrar lo que otro escribio)
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,

    # El PublicId de la cooperativa (el de ADM_Tenants), no su codigo ni su Id interno.
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')]
    [string]$Tenant,

    # El codigo del canal tal como lo registra la API (SIMULADO, el del proveedor...). Se guarda en mayusculas.
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_-]{1,20}$')]
    [string]$Canal,

    [string[]]$Campos,

    [switch]$DesdeStdin,

    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$nombreSecreto = 'erp-fe-credenciales'
$tenantId = ([Guid]$Tenant).ToString('D')
$canalNormalizado = $Canal.ToUpperInvariant()
$clave = "$tenantId.$canalNormalizado.json"

$destino = switch ($Ambiente) {
    'dev' { @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' } }
    'qa'  { @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' } }
    'pdn' { @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' } }
}

Write-Host ""
Write-Host ("  Credencial de facturacion electronica para {0} (Secret {1}, clave {2})" -f $destino.Nombre, $nombreSecreto, $clave) -ForegroundColor Cyan
Write-Host ""

if ($Ambiente -eq 'pdn') {
    if ($canalNormalizado -eq 'SIMULADO') { throw "El canal SIMULADO no se usa en produccion." }
    Write-Host "  ATENCION: esto es PRODUCCION." -ForegroundColor Yellow
    # Con -DesdeStdin la confirmacion tambien llega por la entrada: es la primera linea.
    $ok = if ($DesdeStdin) { [Console]::In.ReadLine() } else { Read-Host "  Escribi PRODUCCION para continuar" }
    if ($ok -ne 'PRODUCCION') { throw "Cancelado." }
    Write-Host ""
}

# Los nombres de los campos (no son secretos).
$nombres = New-Object System.Collections.Generic.List[string]
if ($Campos) {
    foreach ($c in $Campos) { foreach ($p in ($c -split ',')) { if (-not [string]::IsNullOrWhiteSpace($p)) { $nombres.Add($p.Trim()) } } }
} elseif ($DesdeStdin) {
    throw "Con -DesdeStdin hay que declarar -Campos: la entrada lleva solo los valores."
} else {
    while ($true) {
        $n = Read-Host "  Nombre del campo (vacio para terminar)"
        if ([string]::IsNullOrWhiteSpace($n)) { break }
        $nombres.Add($n.Trim())
    }
}
if ($nombres.Count -eq 0) { throw "La credencial necesita al menos un campo." }
foreach ($n in $nombres) {
    if ($n -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') { throw "Nombre de campo invalido: '$n' (letras, digitos y _)." }
}
if (($nombres | Sort-Object -Unique).Count -ne $nombres.Count) { throw "Hay campos repetidos." }

# Los valores (secretos): por teclado, ocultos, o una linea por campo desde STDIN. Solo en memoria.
$valores = [ordered]@{}
foreach ($n in $nombres) {
    if ($DesdeStdin) {
        $valor = [Console]::In.ReadLine()
    } else {
        $seguro = Read-Host ("  Valor de {0} (no se muestra)" -f $n) -AsSecureString
        $puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
        try { $valor = [Runtime.InteropServices.Marshal]::PtrToStringAuto($puntero) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero) }
    }
    if ([string]::IsNullOrEmpty($valor)) { throw "El valor de '$n' no puede estar vacio." }
    $valores[$n] = $valor
}

$json = $valores | ConvertTo-Json -Compress
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
$json = $null
$valores = $null

# El parche fusiona SOLO esta clave en .data; las demas claves del Secret quedan como estaban.
$parche = @{ data = @{ $clave = $b64 } } | ConvertTo-Json -Compress
$b64 = $null

$ns = $destino.Namespace
# Si el Secret no existe todavia se crea vacio (sin claves) y despues se parcha; nunca se reemplaza entero.
$remoto = "k3s kubectl get secret $nombreSecreto -n $ns >/dev/null 2>&1 || k3s kubectl create secret generic $nombreSecreto -n $ns >/dev/null; " +
          "k3s kubectl patch secret $nombreSecreto -n $ns --type=merge --patch-file=/dev/stdin"

Write-Host ""
Write-Host ("  Instalando en {0} ... " -f $destino.Nombre) -NoNewline
$salida = $parche | ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" $remoto 2>&1
$codigo = $LASTEXITCODE
$parche = $null
[GC]::Collect()

if ($codigo -eq 0) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "FALLO" -ForegroundColor Red
    Write-Host "     $salida" -ForegroundColor DarkRed
    exit 1
}

Write-Host ""
Write-Host "  Verificacion (describe muestra las claves y su tamano, nunca sus valores):" -ForegroundColor Cyan
ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" "k3s kubectl describe secret $nombreSecreto -n $ns" 2>&1

Write-Host ""
Write-Host "  El kubelet refresca el volumen solo (unos segundos a un minuto) y la API relee la" -ForegroundColor Yellow
Write-Host "  credencial al vencer su cache de 5 minutos: no hace falta reiniciarla." -ForegroundColor Yellow
Write-Host "  Despues, en Administracion > Facturacion electronica, "Verificar credencial"." -ForegroundColor DarkGray
Write-Host ""
