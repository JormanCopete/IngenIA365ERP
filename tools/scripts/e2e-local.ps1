# =============================================================================
# e2e-local.ps1 - corre las pruebas e2e (tests/IngenIA365ERP.API.IntegrationTests)
# en esta maquina Windows, por modulo o completas, sin pisarse con otra corrida.
#
# Dos usos:
#   1. Antes de desplegar, solo los modulos tocados (10-30 min en vez de 2 h):
#        .\tools\scripts\e2e-local.ps1 -Modulos Ventas,Inventory
#   2. La suite completa de noche, desde el Programador de tareas, sobre una
#      carpeta propia que se pone al dia con develop (ver "Programarla" abajo):
#        .\tools\scripts\e2e-local.ps1 -Actualizar
#
# Que hace, en orden:
#   - No empieza si ya hay otra suite corriendo (proceso testhost): dos a la vez
#     agotan la memoria del equipo.
#   - Con -Actualizar: trae origin/<Rama> y deja la carpeta en ese commit. Solo en
#     una carpeta dedicada (HEAD suelto, sin cambios): nunca mueve una rama de nadie.
#   - Abre Docker Desktop si no responde y espera hasta 3 min.
#   - Detiene los contenedores del compose "conecta" (~2 GB que no usan las
#     pruebas) y al final enciende los mismos que estaban encendidos.
#   - Compila una vez en Release con --disable-build-servers: no quedan procesos de
#     compilacion ocupando memoria mientras corren las pruebas.
#   - dotnet test --no-build con el motor elegido; el .trx, la salida y una linea
#     de resumen quedan en TestResults\e2e-local\ (ignorado por git).
#
# Codigo de salida: el de dotnet test (0 = todo verde); 2 si no pudo empezar.
#
# Parametros:
#   -Modulos      Carpetas de tests/IngenIA365ERP.API.IntegrationTests: Accounting,
#                 Inventory, Lending, Receipts, Treasury, Ventas, Payroll, Transport,
#                 ElectronicInvoicing, Identity... Sin modulos = la suite completa.
#   -Motor        PostgreSql (por defecto) o SqlServer.
#   -Carpeta      Raiz del repositorio a probar. Por defecto, la de este guion.
#   -Actualizar   Pone la carpeta en origin/<Rama> antes de probar (ver arriba).
#   -Rama         develop por defecto.
#   -DejarConecta No toca los contenedores de Conecta.
#   -SinCompilar  Salta la compilacion (ya compilaste en Release).
#   -Simular      Revisa todo y dice lo que haria, sin detener, compilar ni probar.
#
# Programarla (una sola vez, en PowerShell; lo hace el dueno de la maquina):
#   git -C D:\PSNL\AplicacionesWeb\IngenIA365ERP worktree add --detach D:\PSNL\wt-nocturna origin/develop
#   $guion = 'D:\PSNL\wt-nocturna\tools\scripts\e2e-local.ps1'
#   $ajustes = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 4)
#   Register-ScheduledTask -TaskName 'IngenIA365ERP e2e nocturna PostgreSQL' -Settings $ajustes `
#     -Trigger (New-ScheduledTaskTrigger -Daily -At 1am) `
#     -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$guion`" -Actualizar")
#   Register-ScheduledTask -TaskName 'IngenIA365ERP e2e semanal SQL Server' -Settings $ajustes `
#     -Trigger (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday -At 4am) `
#     -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$guion`" -Actualizar -Motor SqlServer")
# El equipo tiene que quedar encendido (o en suspension, para que -WakeToRun lo despierte).
# =============================================================================
param(
    [string[]]$Modulos = @(),
    [ValidateSet('PostgreSql', 'SqlServer')][string]$Motor = 'PostgreSql',
    [string]$Carpeta = '',
    [switch]$Actualizar,
    [string]$Rama = 'develop',
    [switch]$DejarConecta,
    [switch]$SinCompilar,
    [switch]$Simular
)

# 'Continue' y no 'Stop': en Windows PowerShell 5.1 cualquier linea que un .exe escriba
# en stderr se vuelve error terminante con 'Stop'. Cada paso mira $LASTEXITCODE.
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if (-not $Carpeta) { $Carpeta = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
$Carpeta = (Resolve-Path -LiteralPath $Carpeta).Path
$proyecto = Join-Path $Carpeta 'tests\IngenIA365ERP.API.IntegrationTests\IngenIA365ERP.API.IntegrationTests.csproj'
if (-not (Test-Path -LiteralPath $proyecto)) {
    Write-Host "No encuentro el proyecto de pruebas en $Carpeta"
    exit 2
}

# Desde el Programador de tareas, "-Modulos Ventas,Inventory" llega como UNA cadena.
$Modulos = @($Modulos | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($m in $Modulos) {
    if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $proyecto -Parent) $m))) {
        Write-Host "No existe el modulo '$m' en tests\IngenIA365ERP.API.IntegrationTests."
        exit 2
    }
}
$filtro = ($Modulos | ForEach-Object { "FullyQualifiedName~IntegrationTests.$_." }) -join '|'

$sello = Get-Date -Format 'yyyyMMdd-HHmm'
$alcance = if ($Modulos.Count -gt 0) { $Modulos -join '-' } else { 'completa' }
$resultados = Join-Path $Carpeta 'TestResults\e2e-local'
$base = "$sello-$Motor-$alcance"
$trx = "$base.trx"
$script:Salida = Join-Path $resultados "$base.log"
$resumen = Join-Path $resultados 'resumen.log'
if (-not $Simular) { New-Item -ItemType Directory -Force -Path $resultados | Out-Null }

function Anotar([string]$texto) {
    $linea = '{0:HH:mm:ss} {1}' -f (Get-Date), $texto
    Write-Host $linea
    if (-not $Simular) { Add-Content -LiteralPath $script:Salida -Value $linea -Encoding UTF8 }
}

# Corre un .exe mostrando su salida y guardandola en el .log de la corrida.
function Correr([string]$programa, [string[]]$argumentos) {
    & $programa @argumentos 2>&1 | ForEach-Object {
        $linea = "$_"
        Write-Host $linea
        Add-Content -LiteralPath $script:Salida -Value $linea -Encoding UTF8
    }
    return $LASTEXITCODE
}

function DockerResponde {
    & docker info --format '{{.ServerVersion}}' *> $null
    return ($LASTEXITCODE -eq 0)
}

Anotar "e2e $Motor, $alcance, en $Carpeta"

# 1. Una sola suite a la vez.
$otra = @(Get-Process -Name testhost -ErrorAction SilentlyContinue)
if ($otra.Count -gt 0) {
    Anotar "Ya hay pruebas corriendo (testhost PID $(($otra | ForEach-Object { $_.Id }) -join ', ')). Dos a la vez agotan la memoria; no empiezo."
    exit 2
}

# 2. Poner la carpeta al dia, solo si es una carpeta dedicada.
if ($Actualizar) {
    & git -C $Carpeta symbolic-ref -q HEAD *> $null
    if ($LASTEXITCODE -eq 0) {
        Anotar "-Actualizar solo trabaja en una carpeta con HEAD suelto (git worktree add --detach ...): esta tiene una rama activa y no la muevo."
        exit 2
    }
    $cambios = & git -C $Carpeta status --porcelain
    if ($cambios) {
        Anotar "La carpeta tiene cambios sin guardar; -Actualizar no la toca."
        exit 2
    }
    if ($Simular) {
        Anotar "[simulacion] git fetch origin $Rama y checkout --detach origin/$Rama"
    } else {
        & git -C $Carpeta fetch origin $Rama --quiet
        if ($LASTEXITCODE -ne 0) { Anotar "git fetch fallo."; exit 2 }
        & git -C $Carpeta checkout --detach "origin/$Rama" --quiet
        if ($LASTEXITCODE -ne 0) { Anotar "git checkout fallo."; exit 2 }
    }
}
$commit = (& git -C $Carpeta log -1 --format='%h %s') -join ' '
Anotar "Commit: $commit"

# 3. Docker.
if (-not (DockerResponde)) {
    if ($Simular) {
        Anotar "[simulacion] Docker no responde: abriria Docker Desktop y esperaria hasta 3 min."
    } else {
        Anotar "Docker no responde: abro Docker Desktop."
        Start-Process (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe')
        $limite = (Get-Date).AddMinutes(3)
        while (-not (DockerResponde)) {
            if ((Get-Date) -gt $limite) { Anotar "Docker no respondio en 3 min."; exit 2 }
            Start-Sleep -Seconds 5
        }
        Anotar "Docker listo."
    }
}

# 4. Conecta fuera mientras corren las pruebas.
$conecta = @()
if (-not $DejarConecta) {
    $conecta = @(& docker ps -q --filter 'label=com.docker.compose.project=conecta' | Where-Object { $_ })
}

$cronometro = [System.Diagnostics.Stopwatch]::StartNew()
$codigo = 2
try {
    if ($conecta.Count -gt 0) {
        if ($Simular) {
            Anotar "[simulacion] detendria $($conecta.Count) contenedores de Conecta y los encenderia al final."
        } else {
            & docker stop $conecta *> $null
            Anotar "Conecta detenido ($($conecta.Count) contenedores)."
        }
    }

    # 5. Compilar una vez, sin servidores de compilacion que queden ocupando memoria.
    #    (Sin 'return' en este bloque: a nivel de guion, return termina el guion entero.)
    $compilo = $true
    if (-not $SinCompilar) {
        $argumentosBuild = @('build', $proyecto, '-c', 'Release', '--disable-build-servers', '-v', 'minimal')
        if ($Simular) {
            Anotar "[simulacion] dotnet $($argumentosBuild -join ' ')"
        } else {
            Anotar "Compilando en Release..."
            $codigoBuild = Correr 'dotnet' $argumentosBuild
            if ($codigoBuild -ne 0) { Anotar "La compilacion fallo (codigo $codigoBuild)."; $compilo = $false }
        }
    }

    # 6. Las pruebas.
    if ($compilo) {
        $argumentosTest = @('test', $proyecto, '-c', 'Release', '--no-build',
            '--logger', "trx;LogFileName=$trx", '--logger', 'console;verbosity=minimal',
            '--results-directory', $resultados)
        if ($filtro) { $argumentosTest += @('--filter', $filtro) }
        if ($Simular) {
            Anotar "[simulacion] DB_PROVIDER=$Motor dotnet $($argumentosTest -join ' ')"
            $codigo = 0
        } else {
            $env:DB_PROVIDER = $Motor
            Anotar "Probando ($Motor, $alcance)..."
            $codigo = Correr 'dotnet' $argumentosTest
        }
    }
}
finally {
    if ($conecta.Count -gt 0 -and -not $Simular) {
        & docker start $conecta *> $null
        Anotar "Conecta encendido de nuevo."
    }
    $cronometro.Stop()
}

if ($Simular) { exit $codigo }

# 7. Resumen.
$duracion = '{0:hh\:mm}' -f $cronometro.Elapsed
$cuenta = 'sin .trx'
$rutaTrx = Join-Path $resultados $trx
if (Test-Path -LiteralPath $rutaTrx) {
    [xml]$x = Get-Content -LiteralPath $rutaTrx -Raw -Encoding UTF8
    $c = $x.TestRun.ResultSummary.Counters
    $cuenta = "total $($c.total), pasan $($c.passed), fallan $($c.failed), no ejecutadas $($c.notExecuted)"
    $fallidas = @($x.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object { $_.testName })
    if ($fallidas.Count -gt 0) {
        Anotar "Fallaron:"
        $fallidas | Select-Object -First 30 | ForEach-Object { Anotar "  $_" }
        if ($fallidas.Count -gt 30) { Anotar "  ... y $($fallidas.Count - 30) mas (ver $trx)" }
    }
}
$lineaResumen = '{0:yyyy-MM-dd HH:mm} {1,-10} {2,-24} {3} | {4} | {5} | codigo {6}' -f (Get-Date), $Motor, $alcance, $duracion, $cuenta, $commit, $codigo
Add-Content -LiteralPath $resumen -Value $lineaResumen -Encoding UTF8
Anotar "Listo en $($duracion): $cuenta (codigo $codigo). Resumen en $resumen"
exit $codigo
