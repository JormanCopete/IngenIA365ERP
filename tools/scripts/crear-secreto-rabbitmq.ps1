# =============================================================================
# crear-secreto-rabbitmq.ps1 - Genera el usuario y la clave de RabbitMQ de un
# ambiente (feature 023, emision electronica asincrona) y los instala como Secret
# erp-rabbitmq. Lo leen el StatefulSet erp-rabbitmq (usuario por defecto del
# broker) y la API (Mensajeria__Usuario, Mensajeria__Clave): componente
# workloads/erp/components/mensajeria del repositorio GitOps.
#
# SEGURIDAD:
#   - La clave (32 bytes) se genera AQUI con el generador criptografico de .NET;
#     nadie la escribe ni la ve.
#   - Viaja por el canal cifrado de SSH usando STDIN (nunca como argumento).
#   - Se guarda una COPIA LOCAL cifrada con DPAPI en %USERPROFILE%\.ingenia365\.
#     No es imprescindible: perderla solo obliga a rotar (borrar el Secret y el
#     volumen del broker, que no guarda nada que no este en la base).
#   - No sobreescribe un Secret existente: rotar la clave exige recrear el
#     usuario en el broker (ver docs/operaciones/mensajeria-rabbitmq.md).
#
# Uso:
#   .\tools\scripts\crear-secreto-rabbitmq.ps1 -Ambiente dev
#   .\tools\scripts\crear-secreto-rabbitmq.ps1 -Ambiente qa
#   .\tools\scripts\crear-secreto-rabbitmq.ps1 -Ambiente pdn
#   .\tools\scripts\crear-secreto-rabbitmq.ps1 -Ambiente dev -Mostrar   (muestra la copia local)
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,

    [switch]$Mostrar,

    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$nombreSecreto = 'erp-rabbitmq'
$usuario = 'erp'
$carpetaLocal = Join-Path $env:USERPROFILE '.ingenia365'
$copiaLocal = Join-Path $carpetaLocal "rabbitmq-$Ambiente.dpapi"

if ($Mostrar) {
    if (-not (Test-Path $copiaLocal)) { throw "No hay copia local para $Ambiente en $copiaLocal." }
    $seguro = Get-Content $copiaLocal | ConvertTo-SecureString
    $texto = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                 [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro))
    Write-Host ""
    Write-Host "  RabbitMQ de ${Ambiente}:" -ForegroundColor Cyan
    Write-Host $texto
    Write-Host ""
    return
}

$destino = switch ($Ambiente) {
    'dev' { @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' } }
    'qa'  { @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' } }
    'pdn' { @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' } }
}

Write-Host ""
Write-Host ("  Usuario y clave de RabbitMQ para {0} (Secret {1})" -f $destino.Nombre, $nombreSecreto) -ForegroundColor Cyan
Write-Host ""

if ($Ambiente -eq 'pdn') {
    Write-Host "  ATENCION: esto es PRODUCCION." -ForegroundColor Yellow
    $ok = Read-Host "  Escribi PRODUCCION para continuar"
    if ($ok -ne 'PRODUCCION') { throw "Cancelado." }
    Write-Host ""
}

# En Windows PowerShell 5.1, con 'Stop', cualquier linea de stderr de ssh (un aviso de known_hosts)
# se vuelve error terminante: los comandos nativos corren con 'Continue' y se juzgan por $LASTEXITCODE.
$ErrorActionPreference = 'Continue'
$existe = ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" `
    "k3s kubectl get secret $nombreSecreto -n $($destino.Namespace) --ignore-not-found -o name" 2>&1
if ($LASTEXITCODE -ne 0) { throw "No se pudo consultar el cluster: $existe" }
if ($existe) {
    throw "El Secret $nombreSecreto ya existe en $($destino.Nombre). Rotar exige recrear el usuario en el broker: no se sobreescribe."
}

# Solo letras y numeros: la clave viaja tambien en la URI de conexion de las herramientas de diagnostico.
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose()
$clave = ([Convert]::ToBase64String($bytes) -replace '[^A-Za-z0-9]', '')

$valores = [ordered]@{ 'usuario' = $usuario; 'clave' = $clave }

New-Item -ItemType Directory -Force -Path $carpetaLocal | Out-Null
$textoCopia = ($valores.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n"
ConvertTo-SecureString $textoCopia -AsPlainText -Force | ConvertFrom-SecureString | Set-Content -Path $copiaLocal
Write-Host "  Copia local cifrada (DPAPI): $copiaLocal" -ForegroundColor DarkGray

$lineas = ($valores.GetEnumerator() | ForEach-Object {
    "  $($_.Key): $([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_.Value)))"
}) -join "`n"

$yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: $nombreSecreto
  namespace: $($destino.Namespace)
  labels: { app.kubernetes.io/part-of: ingenia365erp }
type: Opaque
data:
$lineas
"@

Write-Host ""
Write-Host ("  Instalando en {0} ... " -f $destino.Nombre) -NoNewline
$salida = $yaml | ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" "k3s kubectl apply -f -" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "FALLO" -ForegroundColor Red
    Write-Host "     $salida" -ForegroundColor DarkRed
    exit 1
}

$clave = $null; $textoCopia = $null; $valores = $null
[GC]::Collect()
Write-Host ""
Write-Host "  Listo. Activa el componente mensajeria en el overlay de $($destino.Nombre) (repositorio GitOps)." -ForegroundColor Cyan
Write-Host ""
