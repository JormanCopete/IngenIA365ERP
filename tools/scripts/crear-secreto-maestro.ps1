# =============================================================================
# crear-secreto-maestro.ps1 - Instala las credenciales del administrador maestro
# como Secret de Kubernetes en el ambiente indicado.
#
# SEGURIDAD: la contrasena se pide por teclado (oculta), viaja por el canal
# cifrado de SSH usando STDIN (nunca como argumento, asi no aparece en `ps` ni
# en el historial del shell) y no se escribe en ningun archivo local.
#
# OJO CON EL SEMBRADOR. MasterAdminSeeder es IDEMPOTENTE: si ya existe un
# maestro vivo (IsGlobalMasterAdmin y no borrado), NO HACE NADA y la contrasena
# de este Secret se ignora. Para cambiarle la clave a un maestro existente hay
# que retirarlo primero (baja logica) y reiniciar la API, que es cuando el
# sembrador lo vuelve a crear con lo que diga este Secret.
#
# Y AL REVES, cuidado: sobre una base SIN maestro y sin este Secret, la API
# hace fallo duro al arrancar y no levanta. Es deliberado — una instalacion sin
# gobierno y sin error visible es el peor modo de fallo. Asi que este Secret va
# ANTES del reinicio, nunca despues.
#
# Uso:
#   .\tools\scripts\crear-secreto-maestro.ps1 -Ambiente qa
#   .\tools\scripts\crear-secreto-maestro.ps1 -Ambiente dev
#   .\tools\scripts\crear-secreto-maestro.ps1 -Ambiente pdn
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,

    [string]$Email = 'master@ingenia365.com',
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$destino = switch ($Ambiente) {
    'dev' { @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' } }
    'qa'  { @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' } }
    'pdn' { @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' } }
}

Write-Host ""
Write-Host ("  Credenciales del administrador maestro para {0}" -f $destino.Nombre) -ForegroundColor Cyan
Write-Host ("  Correo: {0}" -f $Email) -ForegroundColor DarkGray
Write-Host ""

if ($Ambiente -eq 'pdn') {
    Write-Host "  ATENCION: esto es PRODUCCION." -ForegroundColor Yellow
    $ok = Read-Host "  Escribi PRODUCCION para continuar"
    if ($ok -ne 'PRODUCCION') { throw "Cancelado." }
    Write-Host ""
}

$claveSecure = Read-Host "  Contrasena nueva (no se muestra)" -AsSecureString
$claveSecure2 = Read-Host "  Repetila" -AsSecureString

$clave = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
             [Runtime.InteropServices.Marshal]::SecureStringToBSTR($claveSecure))
$clave2 = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
             [Runtime.InteropServices.Marshal]::SecureStringToBSTR($claveSecure2))

if ([string]::IsNullOrWhiteSpace($clave)) { throw "La contrasena no puede estar vacia." }
if ($clave -ne $clave2) { throw "Las dos contrasenas no coinciden." }

# La politica del producto exige 12 caracteres (CentralIdentity:PasswordMinimumLength).
# El sembrador NO la valida —siembra lo que le den— asi que el aviso va aca: una
# clave corta entraria igual y despues fallaria al intentar cambiarla.
if ($clave.Length -lt 12) {
    Write-Host ""
    Write-Host "  AVISO: la politica del producto pide 12 caracteres o mas." -ForegroundColor Yellow
    Write-Host "         El sembrador no valida, asi que esta clave se instalaria igual," -ForegroundColor DarkGray
    Write-Host "         pero no vas a poder cambiarla despues sin romper la politica." -ForegroundColor DarkGray
    $seguir = Read-Host "  Continuar de todas formas? (s/N)"
    if ($seguir -ne 's') { throw "Cancelado." }
}

$b64Email = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Email))
$b64Clave = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($clave))

$yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: erp-master-admin
  namespace: $($destino.Namespace)
type: Opaque
data:
  email: $b64Email
  password: $b64Clave
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

$clave = $null; $clave2 = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Verificacion:" -ForegroundColor Cyan
ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" `
    "k3s kubectl get secret erp-master-admin -n $($destino.Namespace) -o jsonpath='    secreto {.metadata.name} con claves: {range .data}{@}{end}'; echo" 2>&1

Write-Host ""
Write-Host "  El Secret ya esta. Pero si YA EXISTE un maestro vivo, el sembrador" -ForegroundColor Yellow
Write-Host "  lo ignora: hay que retirarlo y reiniciar la API para que lo recree." -ForegroundColor Yellow
Write-Host "  Avisale al asistente." -ForegroundColor DarkGray
Write-Host ""
