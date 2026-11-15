param(
    [Parameter(Mandatory)] [string] $Proyecto,
    [string] $Etiqueta = (Split-Path $Proyecto -Leaf),
    [string] $Salida = "$env:TEMP\mediciones-migraciones.csv",
    [string[]] $ArgsExtra = @()
)
# Compila un proyecto en Release sin recompilar sus dependencias y mide tiempo de pared y memoria
# pico del compilador (proceso csc propio: UseSharedCompilation=false) y de todo lo que lanza el build.
#
# Uso (Windows PowerShell 5.1, con las dependencias ya compiladas en Release):
#   .\tools\scripts\consolidar-migraciones\medir-compilacion.ps1 `
#       -Proyecto src\Infrastructure\IngenIA365ERP.Persistence.Migrations.PostgreSql\IngenIA365ERP.Persistence.Migrations.PostgreSql.csproj `
#       -Etiqueta despues-PostgreSql -ArgsExtra '--no-incremental'
# Muestrea cada 200 ms los procesos dotnet/csc nacidos despues de empezar; el pico es PeakWorkingSet64.
$ErrorActionPreference = 'Stop'
$t0 = Get-Date
$args2 = @('build', $Proyecto, '-c', 'Release', '--no-restore', '-p:BuildProjectReferences=false', '-nodeReuse:false', '-v:q', '-nologo', '-clp:ErrorsOnly') + $ArgsExtra
$p = Start-Process -FilePath dotnet -ArgumentList $args2 -PassThru -NoNewWindow -RedirectStandardOutput "$env:TEMP\medir-build-out.txt" -RedirectStandardError "$env:TEMP\medir-build-err.txt"
$null = $p.Handle
$cmd = @{}
$picoPorPid = @{}
$picoTotal = 0
$picoCsc = 0
while (-not $p.HasExited) {
    $suma = 0
    foreach ($q in Get-Process -Name dotnet, csc, VBCSCompiler -ErrorAction SilentlyContinue) {
        try { if ($q.StartTime -lt $t0) { continue } } catch { continue }
        if (-not $cmd.ContainsKey($q.Id)) {
            $c = (Get-CimInstance Win32_Process -Filter "ProcessId=$($q.Id)" -ErrorAction SilentlyContinue).CommandLine
            $cmd[$q.Id] = "$c"
        }
        $suma += $q.WorkingSet64
        $pk = $q.PeakWorkingSet64
        if (-not $picoPorPid.ContainsKey($q.Id) -or $picoPorPid[$q.Id] -lt $pk) { $picoPorPid[$q.Id] = $pk }
        if (($cmd[$q.Id] -match 'csc\.(dll|exe)|VBCSCompiler' -or $q.Name -eq 'csc') -and $pk -gt $picoCsc) { $picoCsc = $pk }
    }
    if ($suma -gt $picoTotal) { $picoTotal = $suma }
    Start-Sleep -Milliseconds 200
}
$p.WaitForExit()
$dur = (Get-Date) - $t0
$fila = [pscustomobject]@{
    Fecha = $t0.ToString('s'); Etiqueta = $Etiqueta; Codigo = $p.ExitCode
    Segundos = [math]::Round($dur.TotalSeconds, 1)
    PicoCscGB = [math]::Round($picoCsc / 1GB, 2)
    PicoTotalGB = [math]::Round($picoTotal / 1GB, 2)
}
$fila | Export-Csv -Path $Salida -Append -NoTypeInformation -Encoding utf8
$fila | Format-List
$picoPorPid.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 4 | ForEach-Object {
    '{0,7} {1,6:N2} GB  {2}' -f $_.Key, ($_.Value/1GB), ($cmd[$_.Key] -replace '^(.{0,160}).*','$1') }
Get-Content "$env:TEMP\medir-build-err.txt", "$env:TEMP\medir-build-out.txt" -ErrorAction SilentlyContinue | Select-Object -Last 15
