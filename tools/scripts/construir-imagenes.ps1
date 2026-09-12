# =============================================================================
# construir-imagenes.ps1 — publica API, Web y DbMigrator y arma sus imagenes Docker
# igual que el pipeline (.github/workflows/ci.yml): se compila UNA vez fuera y los
# Dockerfile solo empaquetan la carpeta de publicacion.
#
# Uso:
#   .\tools\scripts\construir-imagenes.ps1                 # las tres, etiqueta "local"
#   .\tools\scripts\construir-imagenes.ps1 -Imagenes web   # solo una
#   .\tools\scripts\construir-imagenes.ps1 -Etiqueta prueba-006
#
# Medir cuanto tarda el publish del Web (recorte + compresion del cliente
# WebAssembly, el paso mas largo del pipeline) es tan simple como correr esto con
# -Imagenes web y mirar el reloj.
# =============================================================================
param(
    [ValidateSet('api', 'web', 'migrator')][string[]]$Imagenes = @('api', 'web', 'migrator'),
    [string]$Etiqueta = 'local',
    [switch]$SinDocker
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$salida = Join-Path $repo 'artifacts'

$proyectos = @{
    api      = @{ Proyecto = 'src/Presentation/IngenIA365ERP.API/IngenIA365ERP.API.csproj';       Dockerfile = 'src/Presentation/IngenIA365ERP.API/Dockerfile';   Dll = 'IngenIA365ERP.API.dll' }
    web      = @{ Proyecto = 'src/Presentation/IngenIA365ERP.Web/IngenIA365ERP.Web.csproj';       Dockerfile = 'src/Presentation/IngenIA365ERP.Web/Dockerfile';   Dll = 'IngenIA365ERP.Web.dll' }
    migrator = @{ Proyecto = 'tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj';    Dockerfile = 'tools/IngenIA365ERP.DbMigrator/Dockerfile';        Dll = 'IngenIA365ERP.DbMigrator.dll' }
}

foreach ($nombre in $Imagenes) {
    $p = $proyectos[$nombre]
    $destino = Join-Path $salida $nombre
    if (Test-Path $destino) { Remove-Item -Recurse -Force $destino }

    Write-Host "==> publish $nombre" -ForegroundColor Cyan
    $reloj = [Diagnostics.Stopwatch]::StartNew()
    # Sin --no-restore a proposito: blazor.web.js llega en un paquete que el SDK
    # resuelve durante el restore del publish (ver el Dockerfile del Web).
    & dotnet publish (Join-Path $repo $p.Proyecto) -c Release -o $destino
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo para $nombre" }
    if (-not (Test-Path (Join-Path $destino $p.Dll))) { throw "el publish de $nombre no dejo $($p.Dll)" }
    if ($nombre -eq 'web') {
        $manifiesto = Join-Path $destino 'IngenIA365ERP.Web.staticwebassets.endpoints.json'
        if (-not (Test-Path $manifiesto) -or -not (Select-String -Path $manifiesto -Pattern 'blazor\.web\.js' -Quiet)) {
            throw 'el publish del Web no incluye _framework/blazor.web.js: la aplicacion arrancaria en blanco. Revisar que el SDK cumpla global.json.'
        }
    }
    Write-Host ("    publish {0}: {1:n0} s" -f $nombre, $reloj.Elapsed.TotalSeconds) -ForegroundColor DarkGray

    if ($SinDocker) { continue }
    Write-Host "==> docker build ingenia365erp/${nombre}:$Etiqueta" -ForegroundColor Cyan
    $reloj.Restart()
    & docker build -f (Join-Path $repo $p.Dockerfile) -t "ingenia365erp/${nombre}:$Etiqueta" $destino
    if ($LASTEXITCODE -ne 0) { throw "docker build fallo para $nombre" }
    Write-Host ("    imagen {0}: {1:n0} s" -f $nombre, $reloj.Elapsed.TotalSeconds) -ForegroundColor DarkGray
}
