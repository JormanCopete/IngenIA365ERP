# T139 — Validador automatizado del quickstart de Fase 0.
#
# Requisitos previos:
#   * Docker Desktop arriba con `docker compose -f docker/dev.yml up -d`
#   * API corriendo en http://localhost:5000 (dotnet run --project src/Presentation/IngenIA365ERP.API)
#   * jq disponible (opcional, para parsear JSON; degrada gracefully)
#
# Uso:
#   pwsh scripts/quickstart-validate.ps1
#
# Salida:
#   * Códigos exit: 0 = OK, 1 = al menos un check rojo.
#   * Carpeta `specs/001-cimientos-tecnicos/quickstart-evidence/<fecha>/`
#     con logs JSON de cada paso.

$ErrorActionPreference = 'Stop'
$baseUrl = $env:INGENIA_API_BASE_URL ?? 'http://localhost:5000'
$today = Get-Date -Format 'yyyy-MM-dd'
$evidenceDir = Join-Path $PSScriptRoot ".." "specs" "001-cimientos-tecnicos" "quickstart-evidence" $today
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null

$results = @()

function Probe {
    param([string]$Name, [string]$Url, [string[]]$AcceptableStatuses = @('200', '204'))
    try {
        $resp = Invoke-WebRequest -Uri $Url -SkipHttpErrorCheck -ErrorAction Stop
        $statusOk = $AcceptableStatuses -contains "$([int]$resp.StatusCode)"
        $color = if ($statusOk) { 'Green' } else { 'Red' }
        Write-Host "  [$($resp.StatusCode)] $Name" -ForegroundColor $color
        $results += [PSCustomObject]@{ name = $Name; status = [int]$resp.StatusCode; ok = $statusOk }
        $resp.Content | Set-Content -Path (Join-Path $evidenceDir "$Name.json") -Encoding utf8
        return $statusOk
    } catch {
        Write-Host "  [ERR] $Name : $_" -ForegroundColor Red
        $results += [PSCustomObject]@{ name = $Name; status = 0; ok = $false; error = $_.Exception.Message }
        return $false
    }
}

Write-Host "`n=== US-INFRA — Health & docs ===" -ForegroundColor Cyan
Probe -Name "health-live" -Url "$baseUrl/health/live" | Out-Null
Probe -Name "health-ready" -Url "$baseUrl/health/ready" | Out-Null
Probe -Name "swagger" -Url "$baseUrl/swagger/index.html" | Out-Null

Write-Host "`n=== US1 — Auth & MFA (sin token) ===" -ForegroundColor Cyan
Probe -Name "auth-login-anon" -Url "$baseUrl/api/auth/login" `
    -AcceptableStatuses @('400', '405') | Out-Null  # GET sin body → 400/405 esperado

Write-Host "`n=== US2 — Admin (sin token → 404 indistinguible) ===" -ForegroundColor Cyan
Probe -Name "admin-users-anon" -Url "$baseUrl/api/admin/users" `
    -AcceptableStatuses @('401', '404') | Out-Null
Probe -Name "admin-roles-anon" -Url "$baseUrl/api/admin/roles" `
    -AcceptableStatuses @('401', '404') | Out-Null

Write-Host "`n=== US3 — Audit log (sin token) ===" -ForegroundColor Cyan
Probe -Name "audit-logs-anon" -Url "$baseUrl/api/audit/logs" `
    -AcceptableStatuses @('401', '404') | Out-Null

Write-Host "`n=== US5 — Attachments (sin token) ===" -ForegroundColor Cyan
Probe -Name "attachments-anon" -Url "$baseUrl/api/attachments/by-owner?ownerEntityType=User&ownerEntityPublicId=00000000-0000-0000-0000-000000000001" `
    -AcceptableStatuses @('401', '404') | Out-Null

Write-Host "`n=== US6 — Notifications (sin token) ===" -ForegroundColor Cyan
Probe -Name "notifications-anon" -Url "$baseUrl/api/notifications/" `
    -AcceptableStatuses @('401', '404') | Out-Null

Write-Host "`n=== US7 — Habeas data (sin token) ===" -ForegroundColor Cyan
Probe -Name "habeas-policies-anon" -Url "$baseUrl/api/compliance/habeas-data/policies/" `
    -AcceptableStatuses @('401', '404') | Out-Null

$summary = $results | ConvertTo-Json -Depth 4
$summary | Set-Content -Path (Join-Path $evidenceDir "_summary.json") -Encoding utf8

$failed = $results | Where-Object { -not $_.ok }
Write-Host "`n=== Resumen ===" -ForegroundColor Cyan
Write-Host "  Total: $($results.Count)"
Write-Host "  OK: $($results.Count - $failed.Count)"
Write-Host "  Falla: $($failed.Count)" -ForegroundColor ($(if ($failed.Count -gt 0) { 'Red' } else { 'Green' }))
Write-Host "  Evidencia: $evidenceDir"

if ($failed.Count -gt 0) {
    Write-Host "`nFAIL — quickstart no completado." -ForegroundColor Red
    exit 1
}
Write-Host "`nOK — quickstart pasó todos los gates." -ForegroundColor Green
exit 0
