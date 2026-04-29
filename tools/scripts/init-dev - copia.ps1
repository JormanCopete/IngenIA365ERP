#!/usr/bin/env pwsh
# ============================================================
# IngenIA365ERP - Script de inicializacion para desarrollo
# Orquesta la creacion de BD, schemas y seed data
# ============================================================

param(
    [string]$Server = "localhost",
    [string]$User = "erp",
    [string]$Password = "IngenIA365_Dev2026!",
    [int]$MaxRetries = 30,
    [int]$RetryDelay = 2
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent (Split-Path -Parent $ScriptDir)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " IngenIA365ERP - Inicializacion de entorno de desarrollo" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ── Paso 1: Esperar SQL Server ──
Write-Host "[1/5] Esperando que SQL Server este listo..." -ForegroundColor Yellow
$retry = 0
$connected = $false
while ($retry -lt $MaxRetries) {
    try {
        $result = sqlcmd -S $Server -U $User -P $Password -Q "SELECT 1" -C -h -1 2>&1
        if ($LASTEXITCODE -eq 0) {
            $connected = $true
            break
        }
    } catch {
        # Ignorar errores de conexion
    }
    $retry++
    Write-Host "  Intento $retry de $MaxRetries..." -ForegroundColor Gray
    Start-Sleep -Seconds $RetryDelay
}

if (-not $connected) {
    Write-Host "ERROR: No se pudo conectar a SQL Server despues de $MaxRetries intentos." -ForegroundColor Red
    Write-Host "Verifique que SQL Server este ejecutandose en $Server" -ForegroundColor Red
    exit 1
}
Write-Host "  SQL Server listo." -ForegroundColor Green

# ── Paso 2: Crear bases de datos ──
Write-Host "[2/5] Creando bases de datos..." -ForegroundColor Yellow
sqlcmd -S $Server -U $User -P $Password -i "$ScriptDir\init-database.sql" -C
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Fallo la creacion de bases de datos." -ForegroundColor Red
    exit 1
}
Write-Host "  Bases de datos listas." -ForegroundColor Green

# ── Paso 3: Ejecutar DDL del schema nuevo ──
Write-Host "[3/5] Ejecutando DDL del esquema..." -ForegroundColor Yellow
$ddlFiles = @(
    "IngenIA365ERP_Schema_01_COR.sql",
    "IngenIA365ERP_Schema_02_ACC.sql",
    "IngenIA365ERP_Schema_03_LND.sql",
    "IngenIA365ERP_Schema_04_PAY.sql",
    "IngenIA365ERP_Schema_05_INV.sql",
    "IngenIA365ERP_Schema_06_CDT_DEB_TRS.sql",
    "IngenIA365ERP_Schema_07_SEC_AUD_WEB_ADM.sql",
    "IngenIA365ERP_Schema_08_ForeignKeys.sql",
    "IngenIA365ERP_Schema_09_Indexes.sql",
    "IngenIA365ERP_Schema_10_Views.sql",
    "IngenIA365ERP_Schema_11_Functions.sql",
    "IngenIA365ERP_Schema_12_SeedData.sql"
)

# Buscar DDL en el directorio raiz del proyecto o en PluginsComplete
$ddlDir = $null
$searchPaths = @(
    $RootDir,
    (Join-Path $RootDir "Plugins\PluginsComplete")
)
foreach ($searchPath in $searchPaths) {
    $testFile = Join-Path $searchPath $ddlFiles[0]
    if (Test-Path $testFile) {
        $ddlDir = $searchPath
        break
    }
}

if ($null -eq $ddlDir) {
    Write-Host "  ADVERTENCIA: No se encontraron archivos DDL. Omitiendo paso 3." -ForegroundColor DarkYellow
    Write-Host "  Copie los archivos IngenIA365ERP_Schema_*.sql al directorio raiz." -ForegroundColor DarkYellow
} else {
    foreach ($ddl in $ddlFiles) {
        $ddlPath = Join-Path $ddlDir $ddl
        if (Test-Path $ddlPath) {
            Write-Host "  Ejecutando $ddl..." -ForegroundColor Gray
            sqlcmd -S $Server -U $User -P $Password -d IngenIA365ERP -i $ddlPath -C 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Host "  ADVERTENCIA: Error ejecutando $ddl (puede ya existir)." -ForegroundColor DarkYellow
            }
        } else {
            Write-Host "  ADVERTENCIA: No se encontro $ddl" -ForegroundColor DarkYellow
        }
    }
    Write-Host "  DDL ejecutado." -ForegroundColor Green
}

# ── Paso 4: Crear tenant de desarrollo ──
Write-Host "[4/5] Creando tenant de desarrollo..." -ForegroundColor Yellow
sqlcmd -S $Server -U $User -P $Password -i "$ScriptDir\init-tenant.sql" -C
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Fallo la creacion del tenant." -ForegroundColor Red
    exit 1
}
Write-Host "  Tenant de desarrollo listo." -ForegroundColor Green

# ── Paso 5: Verificar ──
Write-Host "[5/5] Verificando..." -ForegroundColor Yellow
$dbCount = sqlcmd -S $Server -U $User -P $Password -Q "SELECT COUNT(*) FROM sys.databases WHERE name LIKE 'IngenIA365ERP%'" -C -h -1 2>&1
Write-Host "  Bases de datos IngenIA365ERP: $($dbCount.Trim())" -ForegroundColor Gray

$tenantCount = sqlcmd -S $Server -U $User -P $Password -d IngenIA365ERP_Admin -Q "SELECT COUNT(*) FROM dbo.ADM_Tenants WHERE IsDeleted = 0" -C -h -1 2>&1
Write-Host "  Tenants activos: $($tenantCount.Trim())" -ForegroundColor Gray

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Entorno de desarrollo listo!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Siguiente paso: ejecutar la API" -ForegroundColor Cyan
Write-Host "  cd src/Presentation/IngenIA365ERP.API" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""
