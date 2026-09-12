# =============================================================================
# crear-secreto-smtp.ps1 - Instala las credenciales del relay de correo como
# Secret de Kubernetes en el ambiente indicado.
#
# SEGURIDAD: la contrasena se pide por teclado (oculta), viaja por el canal
# cifrado de SSH usando STDIN (nunca como argumento, asi no aparece en `ps` ni
# en el historial del shell) y no se escribe en ningun archivo local.
#
# Lo NO secreto (host, puerto, TLS, remitente, huella del certificado) va en el
# overlay de Kustomize del ambiente, no aqui. Este Secret solo lleva usuario y
# contrasena, que son lo unico que no puede estar en git.
#
# Uso:
#   .\tools\scripts\crear-secreto-smtp.ps1 -Ambiente qa
#   .\tools\scripts\crear-secreto-smtp.ps1 -Ambiente dev
#   .\tools\scripts\crear-secreto-smtp.ps1 -Ambiente pdn
#   .\tools\scripts\crear-secreto-smtp.ps1 -Ambiente pdn -Respaldo   (segunda cuenta)
#
# -Respaldo instala el Secret erp-smtp-respaldo: las credenciales de la cuenta
# por la que sale el correo cuando la principal agota sus reintentos
# (Smtp:Respaldo). El overlay del ambiente decide host y remitente de esa
# cuenta; aqui solo van usuario y contrasena.
#
# OJO: el usuario tiene que ser el mismo buzon que el remitente configurado
# (Smtp__FromAddress o Smtp__Respaldo__FromAddress): el servidor rechaza enviar
# «como» otra direccion («You are not allowed to send emails as X while logged
# as Y»). Es lo que paso en produccion el 2026-09-11.
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,

    [switch]$Respaldo,

    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$nombreSecreto = if ($Respaldo) { 'erp-smtp-respaldo' } else { 'erp-smtp' }
$rol = if ($Respaldo) { 'de RESPALDO' } else { 'principal' }

$destino = switch ($Ambiente) {
    'dev' { @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' } }
    'qa'  { @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' } }
    'pdn' { @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' } }
}

Write-Host ""
Write-Host ("  Credenciales de la cuenta de correo {0} para {1} (Secret {2})" -f $rol, $destino.Nombre, $nombreSecreto) -ForegroundColor Cyan
Write-Host "  (las del servidor SMTP, p. ej. mail.notifica365.com)" -ForegroundColor DarkGray
Write-Host ""

if ($Ambiente -eq 'pdn') {
    Write-Host "  ATENCION: esto es PRODUCCION." -ForegroundColor Yellow
    $ok = Read-Host "  Escribi PRODUCCION para continuar"
    if ($ok -ne 'PRODUCCION') { throw "Cancelado." }
    Write-Host ""
}

$usuario = Read-Host "  Usuario SMTP"
if ([string]::IsNullOrWhiteSpace($usuario)) { throw "El usuario no puede estar vacio." }

$claveSecure = Read-Host "  Contrasena SMTP (no se muestra)" -AsSecureString
$clave = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
             [Runtime.InteropServices.Marshal]::SecureStringToBSTR($claveSecure))
if ([string]::IsNullOrWhiteSpace($clave)) { throw "La contrasena no puede estar vacia." }

$b64Usuario = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($usuario))
$b64Clave   = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($clave))

$yaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: $nombreSecreto
  namespace: $($destino.Namespace)
type: Opaque
data:
  username: $b64Usuario
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

$clave = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Verificacion:" -ForegroundColor Cyan
ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" `
    "k3s kubectl get secret $nombreSecreto -n $($destino.Namespace) -o jsonpath='    secreto {.metadata.name} con claves: {range .data}{@}{end}'; echo" 2>&1

Write-Host ""
Write-Host "  La API lee el Secret al ARRANCAR: hay que reiniciarla para que lo tome." -ForegroundColor Yellow
Write-Host "  Avisale al asistente." -ForegroundColor DarkGray
Write-Host ""
