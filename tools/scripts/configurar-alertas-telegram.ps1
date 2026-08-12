# =============================================================================
# configurar-alertas-telegram.ps1 - Hace que las alertas de Prometheus lleguen
# efectivamente a alguien.
#
# EL PROBLEMA QUE RESUELVE: kube-prometheus-stack se instala con
# `route.receiver: "null"`, es decir, Alertmanager DESCARTA TODAS LAS ALERTAS.
# El sistema parece monitoreado —hay reglas, hay paneles, hay alertas
# disparandose— pero nadie se entera de nada. Es peor que no tener monitoreo,
# porque genera confianza infundada.
#
# COMO CREAR EL BOT (2 minutos, desde Telegram):
#   1. Hablar con @BotFather -> /newbot -> nombre y usuario del bot
#   2. Copiar el token que devuelve (formato 1234567890:AA...)
#   3. Enviarle CUALQUIER mensaje al bot reci�n creado (o agregarlo a un grupo)
#   4. Obtener el chat_id abriendo en el navegador:
#        https://api.telegram.org/bot<TOKEN>/getUpdates
#      y buscando "chat":{"id":NNNNN
#
# SEGURIDAD: el token se pide oculto, se valida enviando un mensaje real y viaja
# por STDIN sobre SSH hasta el cluster. No se escribe en ningun archivo local ni
# aparece en el historial del shell.
#
# Uso:
#   .\tools\scripts\configurar-alertas-telegram.ps1
# =============================================================================
param(
    [string]$KeyPath   = "$env:USERPROFILE\.ssh\ingenia365_deploy",
    [string]$Host_     = '100.94.218.42',
    [string]$Namespace = 'monitoring'
)

$ErrorActionPreference = 'Stop'

function Paso($n, $t) { Write-Host ""; Write-Host "  [$n] $t" -ForegroundColor Cyan }
function Ok($t)       { Write-Host "      OK  $t" -ForegroundColor Green }
function Nota($t)     { Write-Host "      --  $t" -ForegroundColor DarkGray }

Write-Host ""
Write-Host "  Canal de alertas por Telegram" -ForegroundColor Cyan
Write-Host "  Hoy Alertmanager descarta TODAS las alertas (receiver: null)." -ForegroundColor DarkGray
Write-Host ""

$tokenSeguro = Read-Host "  Token del bot (no se muestra)" -AsSecureString
$token = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
             [Runtime.InteropServices.Marshal]::SecureStringToBSTR($tokenSeguro))
if ([string]::IsNullOrWhiteSpace($token)) { throw "El token no puede estar vacio." }

$chatId = Read-Host "  chat_id de destino"
if ($chatId -notmatch '^-?\d+$') { throw "El chat_id debe ser un numero entero (puede ser negativo en grupos)." }

# --- 1. Validacion REAL antes de tocar el cluster ----------------------------
Paso 1 "Comprobando el bot con un mensaje de prueba"

$fecha = Get-Date -Format 'yyyy-MM-dd HH:mm'
$texto = "IngenIA365 ERP - prueba de canal de alertas ($fecha). Si lees esto, las alertas de produccion van a llegar aca."
try {
    $r = Invoke-RestMethod -Method Post -Uri "https://api.telegram.org/bot$token/sendMessage" `
        -Body @{ chat_id = $chatId; text = $texto } -TimeoutSec 20
} catch {
    throw @"
Telegram rechazo el envio: $($_.Exception.Message)

Causas habituales:
  - El token esta mal copiado (debe incluir los digitos, los dos puntos y el resto).
  - Nunca le escribiste al bot: Telegram no permite que un bot inicie la
    conversacion. Mandale cualquier mensaje y volve a intentar.
  - El chat_id no corresponde. Verificalo en:
      https://api.telegram.org/bot<TOKEN>/getUpdates
"@
}
if (-not $r.ok) { throw "Telegram respondio sin exito: $($r | ConvertTo-Json -Compress)" }
Ok "Mensaje entregado - revisa tu Telegram antes de seguir"

# --- 2. Secret con el token --------------------------------------------------
Paso 2 "Instalando el token en el cluster"

$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($token))
$secreto = @"
apiVersion: v1
kind: Secret
metadata:
  name: telegram-alertas
  namespace: $Namespace
type: Opaque
data:
  token: $b64
"@
$salida = $secreto | ssh -i $KeyPath -o BatchMode=yes "root@$Host_" "k3s kubectl apply -f -" 2>&1
if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el Secret: $salida" }
Ok "Secret telegram-alertas"

# --- 3. Configuracion de Alertmanager ----------------------------------------
Paso 3 "Configurando el enrutamiento de alertas"

# El chat_id NO es una credencial (sin el token no sirve para nada), pero igual
# se aplica desde aqui y no desde el repositorio, para no versionar destinos.
#
# repeatInterval 4h y no 12h: una alerta de respaldo roto que se repite dos
# veces al dia es facil de ignorar; cada 4 horas, no tanto.
$config = @"
apiVersion: monitoring.coreos.com/v1alpha1
kind: AlertmanagerConfig
metadata:
  name: alertas-ingenia365
  namespace: $Namespace
spec:
  route:
    receiver: telegram
    groupBy: ["alertname", "namespace"]
    groupWait: 30s
    groupInterval: 5m
    repeatInterval: 4h
    routes:
      # Watchdog es el latido del propio Prometheus: dispara SIEMPRE, a
      # proposito. Notificarlo seria ruido constante; su ausencia es lo que
      # importa, y eso se vigila desde fuera.
      - receiver: descartar
        matchers:
          - name: alertname
            value: Watchdog
  receivers:
    - name: descartar
    - name: telegram
      telegramConfigs:
        - botToken:
            name: telegram-alertas
            key: token
          chatID: $chatId
          parseMode: HTML
          sendResolved: true
          message: |
            <b>{{ .Status | toUpper }}</b> {{ .CommonLabels.alertname }}
            {{ if .CommonLabels.severity }}severidad: {{ .CommonLabels.severity }}{{ end }}
            {{ if .CommonLabels.namespace }}ambiente: {{ .CommonLabels.namespace }}{{ end }}
            {{ range .Alerts }}{{ if .Annotations.summary }}
            {{ .Annotations.summary }}{{ end }}{{ if .Annotations.description }}
            {{ .Annotations.description }}{{ end }}{{ end }}
  inhibitRules:
    # Una alerta critica silencia las de menor severidad del mismo problema:
    # evita recibir tres mensajes por un unico incidente.
    - sourceMatch:
        - name: severity
          value: critical
      targetMatch:
        - name: severity
          value: warning
      equal: ["alertname", "namespace"]
"@
$salida = $config | ssh -i $KeyPath -o BatchMode=yes "root@$Host_" "k3s kubectl apply -f -" 2>&1
if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el AlertmanagerConfig: $salida" }
Ok "AlertmanagerConfig alertas-ingenia365"

# --- 4. Conectarlo como configuracion principal ------------------------------
Paso 4 "Reemplazando la configuracion que descartaba todo"

$parche = '{"spec":{"alertmanagerConfiguration":{"name":"alertas-ingenia365"}}}'
$salida = ssh -i $KeyPath -o BatchMode=yes "root@$Host_" `
    "k3s kubectl patch alertmanager monitoring-kube-prometheus-alertmanager -n $Namespace --type=merge --patch '$parche'" 2>&1
if ($LASTEXITCODE -ne 0) { throw "No se pudo conectar la configuracion: $salida" }
Ok "Alertmanager apunta a la nueva configuracion"

$token = $null; $b64 = $null
[GC]::Collect()

Nota "El operador regenera la configuracion en ~30 s."

# Comprobacion final: que la configuracion generada ya NO enrute a "null".
Paso 5 "Comprobando la configuracion efectiva"
Start-Sleep -Seconds 35
$leer = "k3s kubectl get secret alertmanager-monitoring-kube-prometheus-alertmanager-generated -n $Namespace -o jsonpath='{.data.alertmanager\.yaml\.gz}' | base64 -d | gunzip | head -6"
$efectiva = ssh -i $KeyPath -o BatchMode=yes "root@$Host_" $leer 2>&1
Write-Host ($efectiva | Out-String)
if ($efectiva -match 'telegram') {
    Ok "Las alertas ya se enrutan a Telegram"
} else {
    Nota "Todavia no se refleja: volve a mirar en un minuto con el comando de arriba."
}
Write-Host ""
