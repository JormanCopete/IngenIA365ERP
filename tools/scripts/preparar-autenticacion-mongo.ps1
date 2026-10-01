# =============================================================================
# preparar-autenticacion-mongo.ps1 - Deja listo MongoDB (auditoria) para exigir
# usuario y clave (feature 012, T986). PASO 1 de 2: crea los usuarios y los
# Secrets MIENTRAS Mongo sigue abierto; no corta nada. El PASO 2 lo hace GitOps
# (componente auditoria-protegida): Mongo arranca con --keyFile (que activa la
# autenticacion) y la API, los respaldos y las sondas usan sus usuarios.
#
# Usuarios (base admin):
#   erp-admin     root. Solo lo usan las sondas y el arranque del replica set
#                 del propio pod de Mongo, y soporte.
#   erp-api       rol erp_auditoria_api: INSERTAR y LEER en las bases de
#                 auditoria, crear colecciones e indices, listar bases. NUNCA
#                 update ni remove: la auditoria es solo de agregar (FR-024).
#   erp-respaldo  rol backup: mongodump --oplog de los respaldos diarios.
#
# Secrets (namespace del ambiente):
#   erp-mongo-keyfile   keyfile  (autenticacion interna del replica set)
#   erp-mongo-admin     username, password
#   erp-mongo-api       uri      (cadena de conexion completa de la API)
#   erp-mongo-respaldo  uri      (cadena de conexion de los respaldos)
#
# SEGURIDAD: todas las claves se generan AQUI con el generador criptografico
# de .NET, viajan por SSH usando STDIN (nunca como argumento) y no se escriben
# en ningun archivo local. Nadie las lee: si hicieran falta, estan en el
# Secret del ambiente.
#
# Es re-ejecutable mientras NO existan los Secrets: si una corrida anterior
# alcanzo a crear usuarios, les cambia la clave. Con los Secrets ya creados se
# niega (cambiar claves con Mongo protegido es otro procedimiento).
#
# Uso:
#   .\tools\scripts\preparar-autenticacion-mongo.ps1 -Ambiente dev
#   .\tools\scripts\preparar-autenticacion-mongo.ps1 -Ambiente qa
#   .\tools\scripts\preparar-autenticacion-mongo.ps1 -Ambiente pdn
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'qa', 'pdn')]
    [string]$Ambiente,

    [string]$KeyPath = "$env:USERPROFILE\.ssh\ingenia365_deploy"
)

$ErrorActionPreference = 'Stop'

$destino = switch ($Ambiente) {
    'dev' { @{ Host = '100.94.218.42';  Namespace = 'erp-dev'; Nombre = 'DEV' } }
    'qa'  { @{ Host = '100.94.218.42';  Namespace = 'erp-qa';  Nombre = 'QA' } }
    'pdn' { @{ Host = '100.104.190.76'; Namespace = 'erp-pdn'; Nombre = 'PRODUCCION' } }
}
$ns = $destino.Namespace
$secretos = @('erp-mongo-keyfile', 'erp-mongo-admin', 'erp-mongo-api', 'erp-mongo-respaldo')

function En-Nodo([string]$comando, [string]$entrada = $null) {
    if ($null -ne $entrada) {
        $r = $entrada | ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" $comando 2>&1
    } else {
        $r = ssh -i $KeyPath -o BatchMode=yes "root@$($destino.Host)" $comando 2>&1
    }
    return @{ Salida = ($r | Out-String); Codigo = $LASTEXITCODE }
}

Write-Host ""
Write-Host ("  Autenticacion de MongoDB en {0} - paso 1: usuarios y Secrets" -f $destino.Nombre) -ForegroundColor Cyan
Write-Host ""

if ($Ambiente -eq 'pdn') {
    Write-Host "  ATENCION: esto es PRODUCCION." -ForegroundColor Yellow
    $ok = Read-Host "  Escribi PRODUCCION para continuar"
    if ($ok -ne 'PRODUCCION') { throw "Cancelado." }
    Write-Host ""
}

# 1. Nada de esto puede existir ya.
$r = En-Nodo "k3s kubectl get secret $($secretos -join ' ') -n $ns --ignore-not-found -o name"
if ($r.Codigo -ne 0) { throw "No se pudo consultar el clúster: $($r.Salida)" }
if ($r.Salida.Trim()) {
    throw "Ya existen Secrets de Mongo en $($destino.Nombre):`n$($r.Salida)No se sobreescriben."
}

# 2. Mongo tiene que estar sano y todavia abierto (sin autenticacion).
# mongosh --file /dev/stdin evalua el guion ENTERO (leido por STDIN sin --file lo evalua linea
# por linea, como la consola: un else en su propia linea queda suelto).
$r = En-Nodo "k3s kubectl exec -i -n $ns erp-mongo-0 -c mongo -- mongosh --quiet --file /dev/stdin" "print('ESTADO=' + rs.status().myState)"
if ($r.Codigo -ne 0 -or $r.Salida -notmatch 'ESTADO=1') {
    throw "Mongo no responde como PRIMARY sin autenticacion en $($destino.Nombre) (¿ya esta protegido?): $($r.Salida)"
}

# 3. Claves nuevas.
function Nueva-Clave([int]$largo) {
    $alfabeto = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = New-Object byte[] $largo
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose()
    -join ($bytes | ForEach-Object { $alfabeto[$_ % $alfabeto.Length] })
}
$claveAdmin = Nueva-Clave 40
$claveApi = Nueva-Clave 40
$claveRespaldo = Nueva-Clave 40
$bytesKeyfile = New-Object byte[] 756
$rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytesKeyfile); $rng.Dispose()
$keyfile = [Convert]::ToBase64String($bytesKeyfile).Substring(0, 1000)

# 4. Rol y usuarios (idempotente: crea o cambia la clave).
$js = @"
const admin = db.getSiblingDB('admin');
const privilegios = [
  { resource: { db: '', collection: '' },
    actions: ['find', 'insert', 'createCollection', 'createIndex', 'dropIndex', 'listCollections', 'listIndexes', 'collStats', 'dbStats'] },
  { resource: { cluster: true }, actions: ['listDatabases'] }
];
if (admin.getRole('erp_auditoria_api')) { admin.updateRole('erp_auditoria_api', { privileges: privilegios, roles: [] }); }
else { admin.createRole({ role: 'erp_auditoria_api', privileges: privilegios, roles: [] }); }
function usuario(nombre, clave, roles) {
  if (admin.getUser(nombre)) { admin.updateUser(nombre, { pwd: clave, roles: roles }); print('actualizado ' + nombre); }
  else { admin.createUser({ user: nombre, pwd: clave, roles: roles }); print('creado ' + nombre); }
}
usuario('erp-admin', '$claveAdmin', [{ role: 'root', db: 'admin' }]);
usuario('erp-api', '$claveApi', [{ role: 'erp_auditoria_api', db: 'admin' }]);
usuario('erp-respaldo', '$claveRespaldo', [{ role: 'backup', db: 'admin' }]);
const prueba = new Mongo('mongodb://erp-api:$claveApi@localhost:27017/?authSource=admin&directConnection=true');
const r = prueba.getDB('admin').runCommand({ connectionStatus: 1 });
print('erp-api autentica: ' + (r.authInfo.authenticatedUsers.length === 1));
"@
Write-Host "  Creando rol y usuarios ... " -NoNewline
$r = En-Nodo "k3s kubectl exec -i -n $ns erp-mongo-0 -c mongo -- mongosh --quiet --file /dev/stdin" $js
if ($r.Codigo -ne 0 -or $r.Salida -notmatch 'erp-api autentica: true') {
    Write-Host "FALLO" -ForegroundColor Red
    Write-Host "     $($r.Salida)" -ForegroundColor DarkRed
    exit 1
}
Write-Host "OK" -ForegroundColor Green
Write-Host (($r.Salida -split "`n" | Where-Object { $_ -match '^(creado|actualizado|erp-api autentica)' -or $_ -match '> (creado|actualizado|erp-api)' } |
    ForEach-Object { "     " + ($_ -replace '^.*> ', '') }) | Out-String) -ForegroundColor DarkGray

# 5. Secrets.
function B64([string]$s) { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($s)) }
$uriApi = "mongodb://erp-api:$claveApi@erp-mongo:27017/?replicaSet=rs0&authSource=admin"
$uriRespaldo = "mongodb://erp-respaldo:$claveRespaldo@erp-mongo:27017/?replicaSet=rs0&authSource=admin"
$yaml = @"
apiVersion: v1
kind: Secret
metadata: { name: erp-mongo-keyfile, namespace: $ns, labels: { app.kubernetes.io/part-of: ingenia365-erp } }
type: Opaque
data:
  keyfile: $(B64 $keyfile)
---
apiVersion: v1
kind: Secret
metadata: { name: erp-mongo-admin, namespace: $ns, labels: { app.kubernetes.io/part-of: ingenia365-erp } }
type: Opaque
data:
  username: $(B64 'erp-admin')
  password: $(B64 $claveAdmin)
---
apiVersion: v1
kind: Secret
metadata: { name: erp-mongo-api, namespace: $ns, labels: { app.kubernetes.io/part-of: ingenia365-erp } }
type: Opaque
data:
  uri: $(B64 $uriApi)
---
apiVersion: v1
kind: Secret
metadata: { name: erp-mongo-respaldo, namespace: $ns, labels: { app.kubernetes.io/part-of: ingenia365-erp } }
type: Opaque
data:
  uri: $(B64 $uriRespaldo)
"@
Write-Host "  Instalando los Secrets ... " -NoNewline
$r = En-Nodo "k3s kubectl apply -f -" $yaml
if ($r.Codigo -ne 0) {
    Write-Host "FALLO" -ForegroundColor Red
    Write-Host "     $($r.Salida)" -ForegroundColor DarkRed
    exit 1
}
Write-Host "OK" -ForegroundColor Green

$claveAdmin = $null; $claveApi = $null; $claveRespaldo = $null; $keyfile = $null; $js = $null; $yaml = $null
$uriApi = $null; $uriRespaldo = $null
[GC]::Collect()

Write-Host ""
Write-Host "  Verificacion (describe muestra nombres y tamanos, nunca valores):" -ForegroundColor Cyan
$r = En-Nodo "k3s kubectl describe secret $($secretos -join ' ') -n $ns | grep -E '^Name:|bytes'"
Write-Host $r.Salida

Write-Host "  Mongo SIGUE ABIERTO: nada cambia hasta que GitOps active el componente" -ForegroundColor Yellow
Write-Host "  auditoria-protegida en $($destino.Nombre). Avisale al asistente." -ForegroundColor Yellow
Write-Host ""
