# =============================================================================
# crear-secreto-firma-auditoria.ps1 - Genera las claves propias con que el
# ambiente firma los PDF de auditoria y las anclas de la cadena de auditoria
# (feature 012, T986), y las instala como Secret erp-audit-signature.
#
# POR QUE: sin este Secret la API firma con las claves de desarrollo, cuyos
# secretos estan publicados en el repositorio (dev-v1 y dev-anclas-v1). Con
# ellas cualquiera puede fabricar un PDF o un ancla que "verifica". Con claves
# propias, las de desarrollo dejan de valer en ese ambiente
# (AuditSignatureSettings.QuitarClavesDeDesarrolloSiHayPropias): un PDF firmado
# antes con dev-v1 ya no verifica, y eso es lo correcto.
#
# SEGURIDAD:
#   - Las dos claves (32 bytes cada una) se generan AQUI con el generador
#     criptografico de .NET; nadie las escribe ni las ve.
#   - Viajan por el canal cifrado de SSH usando STDIN (nunca como argumento).
#   - Se guarda una COPIA LOCAL cifrada con DPAPI (solo tu usuario de Windows en
#     este equipo la puede abrir) en %USERPROFILE%\.ingenia365\. Es el respaldo:
#     si el Secret se pierde, sin la clave no se puede verificar un PDF ni un
#     ancla emitidos con ella (5 y 10 anos de retencion). Guardala tambien en tu
#     gestor de contrasenas con -Mostrar si lo necesitas.
#   - No sobreescribe un Secret existente: rotar exige conservar las versiones
#     anteriores (ver docs/operaciones/auditoria-cadena-de-sellos.md).
#
# Uso:
#   .\tools\scripts\crear-secreto-firma-auditoria.ps1 -Ambiente dev
#   .\tools\scripts\crear-secreto-firma-auditoria.ps1 -Ambiente qa
#   .\tools\scripts\crear-secreto-firma-auditoria.ps1 -Ambiente pdn
#   .\tools\scripts\crear-secreto-firma-auditoria.ps1 -Ambiente pdn -Mostrar   (muestra la copia local)
#
# Las versiones se llaman erp-<ambiente>-pdf-AAAAMM y erp-<ambiente>-anclas-AAAAMM
# (nunca empiezan por "dev-", que es el prefijo reservado a las de desarrollo).
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,

    [switch]$Mostrar,

    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$nombreSecreto = 'erp-audit-signature'
$carpetaLocal = Join-Path $env:USERPROFILE '.ingenia365'
$copiaLocal = Join-Path $carpetaLocal "firma-auditoria-$Ambiente.dpapi"

if ($Mostrar) {
    if (-not (Test-Path $copiaLocal)) { throw "No hay copia local para $Ambiente en $copiaLocal." }
    $seguro = Get-Content $copiaLocal | ConvertTo-SecureString
    $texto = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                 [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro))
    Write-Host ""
    Write-Host "  Claves de firma de auditoria de $Ambiente (guardalas en tu gestor de contrasenas):" -ForegroundColor Cyan
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
Write-Host ("  Claves propias de firma de auditoria para {0} (Secret {1})" -f $destino.Nombre, $nombreSecreto) -ForegroundColor Cyan
Write-Host ""

if ($Ambiente -eq 'pdn') {
    Write-Host "  ATENCION: esto es PRODUCCION." -ForegroundColor Yellow
    $ok = Read-Host "  Escribi PRODUCCION para continuar"
    if ($ok -ne 'PRODUCCION') { throw "Cancelado." }
    Write-Host ""
}

$existe = ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" `
    "k3s kubectl get secret $nombreSecreto -n $($destino.Namespace) --ignore-not-found -o name" 2>&1
if ($LASTEXITCODE -ne 0) { throw "No se pudo consultar el clúster: $existe" }
if ($existe) {
    throw "El Secret $nombreSecreto ya existe en $($destino.Nombre). Rotar exige conservar las versiones anteriores: no se sobreescribe."
}

$mes = Get-Date -Format 'yyyyMM'
$versionPdf = "erp-$Ambiente-pdf-$mes"
$versionAnclas = "erp-$Ambiente-anclas-$mes"

function Nueva-Clave {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose()
    [Convert]::ToBase64String($bytes)
}
$clavePdf = Nueva-Clave
$claveAnclas = Nueva-Clave

# Las claves del Secret son los nombres de variable de entorno que la API lee con envFrom.
$valores = [ordered]@{
    'AuditSignature__CurrentKeyVersion'    = $versionPdf
    'AuditSignature__AnchorKeyVersion'     = $versionAnclas
    'AuditSignature__Keys__0__Version'      = $versionPdf
    'AuditSignature__Keys__0__SecretBase64' = $clavePdf
    'AuditSignature__Keys__1__Version'      = $versionAnclas
    'AuditSignature__Keys__1__SecretBase64' = $claveAnclas
}

# Copia local cifrada con DPAPI, ANTES de instalar: si la instalacion falla, no se pierde nada.
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
  labels: { app.kubernetes.io/part-of: ingenia365-erp }
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

$clavePdf = $null; $claveAnclas = $null; $textoCopia = $null; $valores = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Verificacion (describe muestra nombres y tamanos, nunca valores):" -ForegroundColor Cyan
ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" `
    "k3s kubectl describe secret $nombreSecreto -n $($destino.Namespace) | sed -n '/^Data/,`$p'" 2>&1

Write-Host ""
Write-Host "  Versiones: PDF $versionPdf, anclas $versionAnclas." -ForegroundColor Green
Write-Host "  La API toma el Secret cuando GitOps lo conecta (componente auditoria-protegida) y al arrancar." -ForegroundColor Yellow
Write-Host "  Avisale al asistente." -ForegroundColor DarkGray
Write-Host ""
