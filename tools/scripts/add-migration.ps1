# =============================================================================
# add-migration.ps1 — genera una migracion EF Core EN PAR (feature 004, D-02)
#
# Toda migracion debe existir en AMBOS ensamblados de proveedor
# (Migrations.SqlServer y Migrations.PostgreSql). Este script elimina el error
# humano de generar solo una; la omision la caza el check de paridad
# (check-migration-parity.ps1 / Architecture.Tests).
#
# Uso:
#   .\tools\scripts\add-migration.ps1 -Name AddLoanRestructuring -Context Application
#   .\tools\scripts\add-migration.ps1 -Name AddSeedRegistry     -Context Admin
# =============================================================================
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][ValidateSet('Application', 'Admin')][string]$Context
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$startup = Join-Path $repo 'src/Presentation/IngenIA365ERP.API'
$contextClass = "$($Context)DbContext"

$targets = @(
    @{ Project = Join-Path $repo 'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer';  Label = 'SqlServer';  Provider = 'SqlServer' },
    @{ Project = Join-Path $repo 'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql'; Label = 'PostgreSql'; Provider = 'PostgreSQL' }
)

# dotnet-ef arranca el proyecto de la API para resolver el DbContext, y ese
# arranque elige el proveedor por configuracion. En una maquina de desarrollo
# appsettings.Development.json dice PostgreSQL, asi que el lado de SQL Server
# fallaba siempre con "target project doesn't match your migrations assembly".
# Se fija el proveedor por variable de entorno en cada vuelta.
$proveedorPrevio = $env:Database__Provider

foreach ($t in $targets) {
    Write-Host "==> [$($t.Label)] dotnet ef migrations add $Name --context $contextClass" -ForegroundColor Cyan
    $env:Database__Provider = $t.Provider

    # Windows PowerShell 5.1 convierte CUALQUIER escritura a stderr de un
    # ejecutable nativo en error terminante cuando ErrorActionPreference es
    # 'Stop'. Basta una advertencia de NuGet para abortar una migracion que en
    # realidad iba a funcionar. El exito se juzga por $LASTEXITCODE, que es lo
    # unico que dotnet-ef promete.
    $previo = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    dotnet ef migrations add $Name `
        --context $contextClass `
        --project $t.Project `
        --startup-project $startup `
        --output-dir $Context
    $ErrorActionPreference = $previo

    if ($LASTEXITCODE -ne 0) {
        $env:Database__Provider = $proveedorPrevio
        Write-Host "FALLO en $($t.Label) — corrige y re-ejecuta. NO dejes la migracion en un solo proveedor." -ForegroundColor Red
        exit 1
    }
}

$env:Database__Provider = $proveedorPrevio
Write-Host "OK: migracion '$Name' generada en ambos proveedores." -ForegroundColor Green
