# =============================================================================
# Prueba la credencial temporal de adjuntos de un ambiente (feature 011, T075).
#
# Levanta en el namespace del ambiente un pod de UN SOLO USO con el mismo sidecar
# (aws_signing_helper serve, la imagen que usa la API), el mismo ConfigMap de ARN y
# un certificado (por defecto el Secret de la API), corre las comprobaciones con
# la AWS CLI y lo borra. La API no se toca. Ninguna credencial se imprime: lo unico
# que sale del pod es "permitido" o "denegado" por comprobacion.
#
# Tres pruebas:
#   Permisos           (por defecto) lo que la credencial puede y no puede hacer:
#                      escribir, leer y borrar lo suyo; NO listar el bucket ni su
#                      prefijo, NO ver versiones ni vaciar la papelera, NO tocar el
#                      prefijo de otro ambiente, NO tocar el bucket de respaldos, y
#                      su certificado NO obtiene el rol de otro ambiente.
#   ObtieneCredencial  el certificado obtiene credencial (antes de revocarlo).
#   SinCredencial      el certificado YA NO obtiene credencial (despues de importar
#                      la CRL que lo revoca).
#
# Deja en la papelera del bucket un objeto de pocos bytes bajo
# {ambiente}/.prueba-permisos/, que se purga solo a los 90 dias.
#
# Uso (Tailscale conectado y la llave SSH de despliegue):
#   .\tools\scripts\probar-credencial-adjuntos.ps1 -Ambiente qa
#   .\tools\scripts\probar-credencial-adjuntos.ps1 -Ambiente qa -Secret erp-adjuntos-certificado-prueba -Prueba ObtieneCredencial
#   .\tools\scripts\probar-credencial-adjuntos.ps1 -Ambiente qa -Secret erp-adjuntos-certificado-prueba -Prueba SinCredencial
# PRODUCCION pide escribir PRODUCCION antes de crear el pod.
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,
    [ValidateSet('Permisos', 'ObtieneCredencial', 'SinCredencial')]
    [string]$Prueba = 'Permisos',
    # Secret (tipo tls) con el certificado que presenta el pod de prueba.
    [string]$Secret = 'erp-adjuntos-certificado',
    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$destinos = @{
    dev = @{ Maquina = '100.94.218.42';  Namespace = 'erp-dev'; OtroRol = 'qa' }
    qa  = @{ Maquina = '100.94.218.42';  Namespace = 'erp-qa';  OtroRol = 'dev' }
    pdn = @{ Maquina = '100.104.190.76'; Namespace = 'erp-pdn'; OtroRol = 'qa' }
}
$d = $destinos[$Ambiente]
$ns = $d.Namespace
$pod = 'prueba-credencial-adjuntos'

# Windows PowerShell 5.1 vuelve terminante cualquier linea que un ejecutable escriba
# en stderr con ErrorActionPreference = Stop; se decide por el codigo de salida.
function Invoke-Remoto {
    param([string]$Comando, [string]$Entrada)
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $argumentos = @('-i', $KeyPath, '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=15', "root@$($d.Maquina)", $Comando)
        if ($PSBoundParameters.ContainsKey('Entrada')) { $salida = $Entrada | & ssh $argumentos 2>&1 }
        else { $salida = & ssh $argumentos 2>&1 }
    } finally {
        $ErrorActionPreference = $previo
    }
    return ,@($salida | ForEach-Object { "$_" })
}

function Get-Json {
    param([string]$Comando, [string]$Que)
    $salida = Invoke-Remoto -Comando $Comando
    if ($LASTEXITCODE -ne 0) { throw ("No se pudo leer {0}:`n{1}" -f $Que, ($salida -join "`n")) }
    return ($salida -join "`n") | ConvertFrom-Json
}

if ($Ambiente -eq 'pdn') {
    Write-Host ""
    Write-Host "  Va a crear un pod de prueba en el namespace erp-pdn." -ForegroundColor Yellow
    if ((Read-Host "  Escriba PRODUCCION para continuar") -cne 'PRODUCCION') { throw "Cancelado." }
}

Write-Host ""
Write-Host ("  Prueba de la credencial de adjuntos: {0} en {1} con el Secret {2}" -f $Prueba, $ns, $Secret) -ForegroundColor Cyan

# --- lo que usa la API: imagen del sidecar, ARN y securityContext del pod ---------
$deploy = Get-Json -Comando "k3s kubectl get deployment erp-api -n $ns -o json" -Que 'el Deployment erp-api'
$sidecar = @($deploy.spec.template.spec.initContainers) | Where-Object { $_.name -eq 'credenciales-adjuntos' } | Select-Object -First 1
if (-not $sidecar) {
    throw "El sidecar credenciales-adjuntos no esta activo en $ns (componente adjuntos-roles-anywhere de GitOps)."
}
$nombreConfigMap = @($sidecar.env | Where-Object { $_.valueFrom.configMapKeyRef } | ForEach-Object { $_.valueFrom.configMapKeyRef.name }) | Select-Object -First 1
$arn = (Get-Json -Comando "k3s kubectl get configmap $nombreConfigMap -n $ns -o json" -Que "el ConfigMap $nombreConfigMap").data
$rolOtro = $arn.roleArn -replace '-[a-z]+$', "-$($d.OtroRol)"
# El sidecar corre sin privilegios y el Secret se monta 0400: lo que lo vuelve
# legible es el fsGroup del pod. Se copia el de la API tal cual.
$contexto = $deploy.spec.template.spec.securityContext | ConvertTo-Json -Compress -Depth 5

Invoke-Remoto -Comando "k3s kubectl get secret $Secret -n $ns -o name" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "No existe el Secret $Secret en $ns." }

$otros = (@('dev', 'qa', 'pdn') | Where-Object { $_ -ne $Ambiente }) -join ' '

# --- el guion que corre en el pod (sh de la imagen de la AWS CLI) --------------
$guion = @'
sleep 3
B=ingenia365-erp-attachments
fallas=0
res() {
  if [ "$1" = "$2" ]; then echo "OK    $3 -> $2"; else echo "FALLA $3 -> $2 (se esperaba $1)"; fallas=$((fallas+1)); fi
}
probar() {
  esperado="$1"; desc="$2"; shift 2
  salida=$("$@" 2>&1); rc=$?
  if [ $rc -eq 0 ]; then r=permitido
  elif echo "$salida" | grep -q -E "AccessDenied|Forbidden|\(403\)"; then r=denegado
  else r="otro error: $(echo "$salida" | tail -1 | cut -c1-160)"; fi
  res "$esperado" "$r" "$desc"
}
identidad=$(aws sts get-caller-identity --query Arn --output text 2>&1) && obtiene=obtiene || obtiene=rechazada
case "$PRUEBA" in
  ObtieneCredencial)
    res obtiene "$obtiene" "el certificado obtiene credencial"
    [ "$obtiene" = obtiene ] && echo "identidad: $identidad" ;;
  SinCredencial)
    res rechazada "$obtiene" "el certificado ya no obtiene credencial" ;;
  *)
    echo "identidad: $(echo "$identidad" | tail -1 | cut -c1-200)"
    K="$PROPIO/.prueba-permisos/$(date +%s).txt"
    echo prueba > /tmp/p.txt
    if V=$(aws s3api put-object --bucket $B --key "$K" --body /tmp/p.txt --query VersionId --output text 2>&1); then r=permitido
    else r="otro error: $(echo "$V" | tail -1 | cut -c1-160)"; fi
    res permitido "$r" "escribir en $PROPIO/"
    probar permitido "leer lo suyo en $PROPIO/" aws s3api get-object --bucket $B --key "$K" /tmp/o.txt
    probar denegado "listar el bucket" aws s3api list-objects-v2 --bucket $B --max-keys 1
    probar denegado "listar su propio prefijo $PROPIO/" aws s3api list-objects-v2 --bucket $B --prefix "$PROPIO/" --max-keys 1
    probar denegado "listar versiones" aws s3api list-object-versions --bucket $B --prefix "$K"
    for o in $OTROS; do
      probar denegado "escribir en $o/" aws s3api put-object --bucket $B --key "$o/.prueba-permisos/x.txt" --body /tmp/p.txt
      probar denegado "leer en $o/" aws s3api get-object --bucket $B --key "$o/.prueba-permisos/x.txt" /tmp/o2.txt
    done
    probar denegado "listar el bucket de respaldos" aws s3api list-objects-v2 --bucket ingenia365-erp-backups --max-keys 1
    probar denegado "leer del bucket de respaldos" aws s3api get-object --bucket ingenia365-erp-backups --key x /tmp/o3.txt
    probar permitido "borrar lo suyo (queda en la papelera)" aws s3api delete-object --bucket $B --key "$K"
    probar denegado "borrar la version (vaciar la papelera)" aws s3api delete-object --bucket $B --key "$K" --version-id "$V"
    AWS_EC2_METADATA_SERVICE_ENDPOINT=http://127.0.0.1:9912 aws sts get-caller-identity > /dev/null 2>&1 && r=obtiene || r=rechazada
    res rechazada "$r" "su certificado pidiendo el rol de $ROL_OTRO" ;;
esac
echo "fallas: $fallas"
'@
$guionIndentado = ($guion -split "`r?`n" | ForEach-Object { "          $_" }) -join "`n"

function Get-Sidecar {
    param([string]$Nombre, [string]$Rol, [int]$Puerto)
    return @"
    - name: $Nombre
      image: $($sidecar.image)
      restartPolicy: Always
      args:
        - serve
        - --certificate=/certificado/tls.crt
        - --private-key=/certificado/tls.key
        - --trust-anchor-arn=$($arn.trustAnchorArn)
        - --profile-arn=$($arn.profileArn)
        - --role-arn=$Rol
        - --port=$Puerto
        - --hop-limit=1
      resources: { requests: { cpu: 10m, memory: 16Mi }, limits: { cpu: 200m, memory: 64Mi } }
      volumeMounts: [{ name: certificado, mountPath: /certificado, readOnly: true }]
"@
}

$manifiesto = @"
apiVersion: v1
kind: Pod
metadata:
  name: $pod
  namespace: $ns
  labels: { app.kubernetes.io/name: $pod }
spec:
  restartPolicy: Never
  imagePullSecrets: [{ name: ghcr-pull }]
  securityContext: $contexto
  initContainers:
$(Get-Sidecar -Nombre 'credenciales' -Rol $arn.roleArn -Puerto 9911)
$(Get-Sidecar -Nombre 'credenciales-otro-rol' -Rol $rolOtro -Puerto 9912)
  containers:
    - name: aws
      image: public.ecr.aws/aws-cli/aws-cli:latest
      command: ["/bin/sh", "-c"]
      env:
        - { name: AWS_EC2_METADATA_SERVICE_ENDPOINT, value: "http://127.0.0.1:9911" }
        - { name: AWS_DEFAULT_REGION, value: us-east-1 }
        - { name: AWS_PAGER, value: "" }
        - { name: HOME, value: /tmp }
        - { name: PRUEBA, value: $Prueba }
        - { name: PROPIO, value: $Ambiente }
        - { name: OTROS, value: "$otros" }
        - { name: ROL_OTRO, value: $($d.OtroRol) }
      resources: { requests: { cpu: 50m, memory: 128Mi }, limits: { cpu: 500m, memory: 256Mi } }
      args:
        - |
$guionIndentado
  volumes:
    - name: certificado
      secret: { secretName: $Secret, defaultMode: 0400 }
"@

# --- correr, leer y borrar -----------------------------------------------------
$fallas = -1
try {
    Invoke-Remoto -Comando "k3s kubectl delete pod $pod -n $ns --ignore-not-found --wait=true" | Out-Null
    $salida = Invoke-Remoto -Comando 'k3s kubectl apply -f -' -Entrada $manifiesto
    if ($LASTEXITCODE -ne 0) { throw ("No se pudo crear el pod:`n{0}" -f ($salida -join "`n")) }
    Write-Host "  Pod de prueba creado; esperando que termine (hasta 4 minutos) ..." -ForegroundColor DarkGray
    $fase = Invoke-Remoto -Comando "for i in `$(seq 1 60); do f=`$(k3s kubectl get pod $pod -n $ns -o jsonpath='{.status.phase}'); case `$f in Succeeded|Failed) break;; esac; sleep 4; done; echo `$f"
    Write-Host ("  Fase: {0}" -f ($fase -join ' ')) -ForegroundColor DarkGray
    Write-Host ""
    $log = Invoke-Remoto -Comando "k3s kubectl logs $pod -n $ns -c aws"
    # -clike: -like no distingue mayusculas y "fallas: 0" pasaria por "FALLA".
    foreach ($linea in $log) {
        if ($linea -clike 'fallas:*') { $fallas = [int]($linea -replace '\D', '') }
        elseif ($linea -clike 'OK*') { Write-Host "  $linea" -ForegroundColor Green }
        elseif ($linea -clike 'FALLA*') { Write-Host "  $linea" -ForegroundColor Red }
        else { Write-Host "  $linea" -ForegroundColor DarkGray }
    }
    if ($Prueba -eq 'SinCredencial') {
        # El motivo del rechazo lo dice el sidecar (sin credenciales: solo el error).
        $motivo = Invoke-Remoto -Comando "k3s kubectl logs $pod -n $ns -c credenciales | grep 'Error generating credentials' | tail -1 | cut -c1-300"
        if ($motivo) { Write-Host ("  sidecar: {0}" -f ($motivo -join ' ')) -ForegroundColor DarkGray }
    }
} finally {
    Invoke-Remoto -Comando "k3s kubectl delete pod $pod -n $ns --ignore-not-found --wait=false" | Out-Null
    Write-Host "  Pod de prueba borrado." -ForegroundColor DarkGray
}

Write-Host ""
if ($fallas -eq 0) { Write-Host "  Todo como se esperaba." -ForegroundColor Green; exit 0 }
elseif ($fallas -lt 0) { Write-Host "  La prueba no llego a terminar: revise el pod y sus eventos." -ForegroundColor Red; exit 1 }
else { Write-Host ("  {0} comprobacion(es) no dieron lo esperado." -f $fallas) -ForegroundColor Red; exit 1 }
