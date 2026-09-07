# =============================================================================
# preparar-postgres-local.ps1 — deja el PostgreSQL instalado en el sistema listo
# para trabajar con el ERP.
#
# Crea el rol y las dos bases que el ERP necesita en la instancia LOCAL (5432),
# que es distinta e independiente de la del contenedor (5433).
#
# Las contrasenas NO viajan por la linea de comandos ni quedan en el historial:
# se piden por consola y se pasan a psql por la variable PGPASSWORD del proceso
# hijo, que muere con el.
#
# Uso:
#   .\tools\scripts\preparar-postgres-local.ps1
#   .\tools\scripts\preparar-postgres-local.ps1 -Puerto 5432 -Rol ingenia
# =============================================================================
param(
    [int]$Puerto = 5432,
    [string]$Rol = 'ingenia',
    [string]$Superusuario = 'postgres',
    [string]$BaseOperativa = 'ingenia365erp',
    [string]$BaseAdmin = 'ingenia365erp_admin'
)

$ErrorActionPreference = 'Stop'

$psql = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
if (-not $psql) {
    Write-Host "No encontre psql.exe en C:\Program Files\PostgreSQL\*\bin\." -ForegroundColor Red
    Write-Host "Si PostgreSQL esta en otra ruta, agregala al PATH y volve a correr." -ForegroundColor Yellow
    exit 1
}
Write-Host "Usando $($psql.FullName)" -ForegroundColor DarkGray

function Leer-Secreto([string]$mensaje) {
    $seguro = Read-Host -Prompt $mensaje -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
    try   { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# El resultado de la ultima invocacion, para poder mostrar el error REAL de psql
# en vez de un mensaje generico adivinando la causa.
$script:UltimaSalida = ''

# -----------------------------------------------------------------------------
# Ejecuta psql y devuelve SOLO el codigo de salida.
#
# El `$null =` no es cosmetico. Sin el, la funcion emite al pipeline la salida
# de psql Y el codigo, o sea un arreglo; entonces `(Invocar-Psql ...) -ne 0`
# compara un arreglo contra cero, PowerShell lo interpreta como filtro, devuelve
# los elementos distintos de cero —el texto que imprimio psql— y el `if` se
# dispara aunque el comando haya funcionado. Es exactamente lo que pasaba: la
# conexion era correcta y el script reportaba que no habia podido conectar.
# -----------------------------------------------------------------------------
function Invocar-Psql {
    param(
        [Parameter(Mandatory)][string]$Base,
        [Parameter(Mandatory)][string]$Sql,
        [Parameter(Mandatory)][string]$Clave
    )

    # Windows PowerShell 5.1 convierte cualquier escritura a stderr de un
    # ejecutable nativo en error terminante cuando ErrorActionPreference es
    # 'Stop'. psql escribe avisos por ahi. El exito se juzga por $LASTEXITCODE.
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $env:PGPASSWORD = $Clave
    try {
        $salida = & $psql.FullName `
            -h localhost -p $Puerto -U $Superusuario -d $Base `
            -v ON_ERROR_STOP=1 -c $Sql 2>&1
        $script:UltimaSalida = ($salida | ForEach-Object { "$_" }) -join [Environment]::NewLine
        return $LASTEXITCODE
    }
    finally {
        $env:PGPASSWORD = $null
        $ErrorActionPreference = $previo
    }
}

# Igual que la anterior pero devuelve el TEXTO de una consulta de un solo valor.
function Consultar-Psql {
    param(
        [Parameter(Mandatory)][string]$Base,
        [Parameter(Mandatory)][string]$Sql,
        [Parameter(Mandatory)][string]$Clave
    )
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $env:PGPASSWORD = $Clave
    try {
        $salida = & $psql.FullName `
            -h localhost -p $Puerto -U $Superusuario -d $Base -tAc $Sql 2>$null
        return ("$salida").Trim()
    }
    finally {
        $env:PGPASSWORD = $null
        $ErrorActionPreference = $previo
    }
}

function Mostrar-ErrorDePsql([string]$queIntentaba) {
    Write-Host ""
    Write-Host "Fallo al $queIntentaba. Esto respondio PostgreSQL:" -ForegroundColor Red
    if ([string]::IsNullOrWhiteSpace($script:UltimaSalida)) {
        Write-Host "  (sin salida; revisa que el servicio este escuchando en el puerto $Puerto)" -ForegroundColor Yellow
    }
    else {
        $script:UltimaSalida -split [Environment]::NewLine |
            ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    }
}

Write-Host ""
Write-Host "Contrasena del superusuario '$Superusuario' en localhost:$Puerto" -ForegroundColor Cyan
Write-Host "(la que definiste al instalar PostgreSQL; no se guarda en ningun lado)" -ForegroundColor DarkGray
$claveSuper = Leer-Secreto "Contrasena de $Superusuario"

Write-Host ""
Write-Host "Contrasena para el rol '$Rol' que voy a crear." -ForegroundColor Cyan
Write-Host "Si usas la misma que ya figura en appsettings.Development.json para el" -ForegroundColor DarkGray
Write-Host "destino Docker, no hay que tocar ninguna configuracion despues." -ForegroundColor DarkGray
$claveRol = Leer-Secreto "Contrasena para $Rol"

if ([string]::IsNullOrWhiteSpace($claveRol)) {
    Write-Host "La contrasena del rol no puede quedar vacia." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==> Verificando acceso..." -ForegroundColor Cyan
if ((Invocar-Psql -Base 'postgres' -Sql 'SELECT 1' -Clave $claveSuper) -ne 0) {
    Mostrar-ErrorDePsql "conectar como '$Superusuario'"
    exit 1
}
Write-Host "    conexion OK." -ForegroundColor Green

# El rol se crea con la contrasena escapada como literal de cadena de SQL: una
# comilla simple dentro de la contrasena romperia la sentencia.
$claveEscapada = $claveRol.Replace("'", "''")

Write-Host "==> Rol '$Rol' (si ya existe, solo se le actualiza la contrasena)..." -ForegroundColor Cyan
$sqlRol = @"
DO `$`$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$Rol') THEN
        ALTER ROLE $Rol WITH LOGIN CREATEDB PASSWORD '$claveEscapada';
    ELSE
        CREATE ROLE $Rol WITH LOGIN CREATEDB PASSWORD '$claveEscapada';
    END IF;
END
`$`$;
"@
if ((Invocar-Psql -Base 'postgres' -Sql $sqlRol -Clave $claveSuper) -ne 0) {
    Mostrar-ErrorDePsql "crear el rol '$Rol'"
    exit 1
}
Write-Host "    listo." -ForegroundColor Green

foreach ($nombreBase in @($BaseOperativa, $BaseAdmin)) {
    Write-Host "==> Base '$nombreBase'..." -ForegroundColor Cyan

    # CREATE DATABASE no admite IF NOT EXISTS: hay que consultar antes.
    $existe = Consultar-Psql -Base 'postgres' -Clave $claveSuper `
        -Sql "SELECT 1 FROM pg_database WHERE datname = '$nombreBase'"

    if ($existe -eq '1') {
        Write-Host "    ya existia; se le asigna '$Rol' como duenio." -ForegroundColor DarkGray
        if ((Invocar-Psql -Base 'postgres' -Sql "ALTER DATABASE $nombreBase OWNER TO $Rol;" -Clave $claveSuper) -ne 0) {
            Mostrar-ErrorDePsql "cambiar el duenio de '$nombreBase'"
            exit 1
        }
    }
    else {
        if ((Invocar-Psql -Base 'postgres' -Sql "CREATE DATABASE $nombreBase OWNER $Rol;" -Clave $claveSuper) -ne 0) {
            Mostrar-ErrorDePsql "crear la base '$nombreBase'"
            exit 1
        }
        Write-Host "    creada." -ForegroundColor Green
    }

    # El ERP usa el schema 'dbo' (herencia de SQL Server) y lo crea EF, pero el
    # rol necesita poder crearlo.
    if ((Invocar-Psql -Base $nombreBase -Sql "GRANT ALL ON DATABASE $nombreBase TO $Rol; GRANT CREATE ON SCHEMA public TO $Rol;" -Clave $claveSuper) -ne 0) {
        Mostrar-ErrorDePsql "otorgar permisos sobre '$nombreBase'"
        exit 1
    }
}

Write-Host ""
Write-Host "==> Comprobando que el rol '$Rol' pueda entrar de verdad..." -ForegroundColor Cyan
$previo = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$env:PGPASSWORD = $claveRol
$null = & $psql.FullName -h localhost -p $Puerto -U $Rol -d $BaseOperativa -tAc 'SELECT 1' 2>&1
$codigoRol = $LASTEXITCODE
$env:PGPASSWORD = $null
$ErrorActionPreference = $previo

if ($codigoRol -ne 0) {
    Write-Host "    El rol se creo pero NO pudo conectarse. Revisa pg_hba.conf." -ForegroundColor Red
    exit 1
}
Write-Host "    '$Rol' conecta correctamente a '$BaseOperativa'." -ForegroundColor Green

Write-Host ""
Write-Host "Listo. El PostgreSQL local quedo preparado." -ForegroundColor Green
Write-Host ""
Write-Host "Que sigue:" -ForegroundColor Cyan
Write-Host "  1. Si la contrasena del rol NO es la que figura en appsettings.Development.json,"
Write-Host "     ponela en appsettings.Development.local.json (git lo ignora):"
Write-Host ""
Write-Host '     { "Infraestructura": { "PostgreSQL": { "Local": {' -ForegroundColor DarkGray
Write-Host '         "Operativa": "Host=localhost;Port=5432;Database=ingenia365erp;Username=ingenia;Password=TU_CLAVE",' -ForegroundColor DarkGray
Write-Host '         "Admin":     "Host=localhost;Port=5432;Database=ingenia365erp_admin;Username=ingenia;Password=TU_CLAVE"' -ForegroundColor DarkGray
Write-Host '     } } } }' -ForegroundColor DarkGray
Write-Host ""
Write-Host "  2. Arranca la API. AutoMigrate crea el esquema y los seeders cargan los"
Write-Host "     parametricos. El administrador maestro NO se copia desde el contenedor:"
Write-Host "     hay que sembrarlo de nuevo con MASTER_ADMIN_EMAIL y MASTER_ADMIN_PASSWORD."
Write-Host ""
Write-Host "  Las bases del contenedor (5433) siguen intactas: son otra instalacion." -ForegroundColor DarkGray
