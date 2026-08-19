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

Write-Host ""
Write-Host "Contrasena del superusuario '$Superusuario' en localhost:$Puerto" -ForegroundColor Cyan
Write-Host "(la que definiste al instalar PostgreSQL; no se guarda en ningun lado)" -ForegroundColor DarkGray
$claveSuper = Leer-Secreto "Contrasena de $Superusuario"

Write-Host ""
Write-Host "Contrasena para el rol '$Rol' que voy a crear." -ForegroundColor Cyan
Write-Host "Si usas la misma que ya figura en appsettings.Development.json para el" -ForegroundColor DarkGray
Write-Host "destino Docker, no hay que tocar ninguna configuracion despues." -ForegroundColor DarkGray
$claveRol = Leer-Secreto "Contrasena para $Rol"

# ErrorActionPreference vuelve a Continue alrededor de las llamadas nativas:
# en Windows PowerShell 5.1 cualquier escritura a stderr de un ejecutable se
# convierte en error terminante con 'Stop', y psql escribe avisos por ahi.
function Invocar-Psql([string]$base, [string]$sql) {
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $env:PGPASSWORD = $claveSuper
    try {
        & $psql.FullName -h localhost -p $Puerto -U $Superusuario -d $base -v ON_ERROR_STOP=1 -c $sql
        return $LASTEXITCODE
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
        $ErrorActionPreference = $previo
    }
}

Write-Host ""
Write-Host "==> Verificando acceso..." -ForegroundColor Cyan
if ((Invocar-Psql 'postgres' 'SELECT 1') -ne 0) {
    Write-Host ""
    Write-Host "No pude conectar como '$Superusuario'. Revisa la contrasena y que el" -ForegroundColor Red
    Write-Host "servicio de PostgreSQL este escuchando en el puerto $Puerto." -ForegroundColor Red
    exit 1
}

# El rol se crea con la contrasena escapada como literal de cadena de SQL: una
# comilla simple dentro de la contrasena romperia la sentencia.
$claveEscapada = $claveRol.Replace("'", "''")

Write-Host "==> Creando el rol '$Rol' (si ya existe, solo le actualiza la contrasena)..." -ForegroundColor Cyan
$sqlRol = @"
DO `$`$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$Rol') THEN
        ALTER ROLE $Rol WITH LOGIN CREATEDB PASSWORD '$claveEscapada';
        RAISE NOTICE 'rol % actualizado', '$Rol';
    ELSE
        CREATE ROLE $Rol WITH LOGIN CREATEDB PASSWORD '$claveEscapada';
        RAISE NOTICE 'rol % creado', '$Rol';
    END IF;
END
`$`$;
"@
if ((Invocar-Psql 'postgres' $sqlRol) -ne 0) { exit 1 }

foreach ($base in @($BaseOperativa, $BaseAdmin)) {
    Write-Host "==> Base '$base'..." -ForegroundColor Cyan
    # CREATE DATABASE no admite IF NOT EXISTS: se consulta antes.
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $env:PGPASSWORD = $claveSuper
    $existe = (& $psql.FullName -h localhost -p $Puerto -U $Superusuario -d postgres -tAc `
        "SELECT 1 FROM pg_database WHERE datname = '$base'") 2>$null
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    $ErrorActionPreference = $previo

    if ("$existe".Trim() -eq '1') {
        Write-Host "    ya existia; se le asigna '$Rol' como duenio." -ForegroundColor DarkGray
        if ((Invocar-Psql 'postgres' "ALTER DATABASE $base OWNER TO $Rol;") -ne 0) { exit 1 }
    }
    else {
        if ((Invocar-Psql 'postgres' "CREATE DATABASE $base OWNER $Rol;") -ne 0) { exit 1 }
        Write-Host "    creada." -ForegroundColor Green
    }
}

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
