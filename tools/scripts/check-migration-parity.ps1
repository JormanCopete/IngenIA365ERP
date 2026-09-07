# =============================================================================
# check-migration-parity.ps1 — verifica que cada migracion EF exista en AMBOS
# ensamblados de proveedor (feature 004, SC-008). Compara los NOMBRES LOGICOS
# (sufijo tras el timestamp) por contexto (carpetas Admin/ y Application/).
# Exit 1 si hay diferencia — usable como gate de CI.
# =============================================================================
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$roots = @{
    SqlServer  = Join-Path $repo 'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer'
    PostgreSql = Join-Path $repo 'src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql'
}

$fail = $false
foreach ($context in @('Admin', 'Application')) {
    $sets = @{}
    foreach ($p in $roots.Keys) {
        $dir = Join-Path $roots[$p] $context
        $sets[$p] = @(Get-ChildItem $dir -Filter '*.cs' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d{14}_(.+)\.cs$' -and $_.Name -notmatch '\.Designer\.cs$' } |
            ForEach-Object { [regex]::Match($_.Name, '^\d{14}_(.+)\.cs$').Groups[1].Value } |
            Sort-Object)
    }
    $onlySql = $sets['SqlServer']  | Where-Object { $_ -notin $sets['PostgreSql'] }
    $onlyPg  = $sets['PostgreSql'] | Where-Object { $_ -notin $sets['SqlServer'] }
    if ($onlySql -or $onlyPg) {
        $fail = $true
        Write-Host "PARIDAD ROTA en contexto ${context}:" -ForegroundColor Red
        if ($onlySql) { Write-Host "  Solo en SqlServer : $($onlySql -join ', ')" -ForegroundColor Red }
        if ($onlyPg)  { Write-Host "  Solo en PostgreSql: $($onlyPg -join ', ')" -ForegroundColor Red }
    }
    else {
        Write-Host "OK ${context}: $($sets['SqlServer'].Count) migracion(es) en par." -ForegroundColor Green
    }
}

if ($fail) {
    Write-Host "Genera la migracion faltante con tools/scripts/add-migration.ps1 (SIEMPRE en par)." -ForegroundColor Yellow
    exit 1
}
exit 0
