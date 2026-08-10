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
    @{ Project = Join-Path $repo 'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer';  Label = 'SqlServer' },
    @{ Project = Join-Path $repo 'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql'; Label = 'PostgreSql' }
)

foreach ($t in $targets) {
    Write-Host "==> [$($t.Label)] dotnet ef migrations add $Name --context $contextClass" -ForegroundColor Cyan
    dotnet ef migrations add $Name `
        --context $contextClass `
        --project $t.Project `
        --startup-project $startup `
        --output-dir $Context
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FALLO en $($t.Label) — corrige y re-ejecuta. NO dejes la migracion en un solo proveedor." -ForegroundColor Red
        exit 1
    }
}

Write-Host "OK: migracion '$Name' generada en ambos proveedores." -ForegroundColor Green
