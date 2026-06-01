# =============================================================================
# convertir-manual-a-word.ps1
#
# Convierte los manuales .md de identidad central a .docx (Word) usando pandoc.
#
# Requisito previo: instalar pandoc (una sola vez):
#   winget install --id JohnMacFarlane.Pandoc
# o:
#   choco install pandoc
# o descarga directa: https://pandoc.org/installing.html
#
# Uso:
#   .\convertir-manual-a-word.ps1
# =============================================================================

$ErrorActionPreference = "Stop"

# Ubicarse en la raíz del repo (un nivel arriba de docs/operaciones).
$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
Set-Location $repoRoot

if (-not (Get-Command pandoc -ErrorAction SilentlyContinue)) {
    Write-Host "❌ pandoc no está instalado." -ForegroundColor Red
    Write-Host ""
    Write-Host "Instalalo con:" -ForegroundColor Yellow
    Write-Host "  winget install --id JohnMacFarlane.Pandoc" -ForegroundColor Yellow
    Write-Host "  o: choco install pandoc" -ForegroundColor Yellow
    Write-Host "  o: descarga manual en https://pandoc.org/installing.html" -ForegroundColor Yellow
    exit 1
}

$inputs = @(
    @{
        Source = "docs\operaciones\manual-funcional-usuario-final.md"
        Output = "docs\operaciones\Manual-Usuario-Identidad-Central.docx"
        Title  = "Manual de Usuario — Identidad Central"
    },
    @{
        Source = "docs\operaciones\manual-pruebas-identidad-central.md"
        Output = "docs\operaciones\Manual-Pruebas-Identidad-Central.docx"
        Title  = "Manual de Pruebas Técnico — Identidad Central"
    }
)

foreach ($item in $inputs) {
    if (-not (Test-Path $item.Source)) {
        Write-Host "⚠️  Archivo fuente no encontrado: $($item.Source)" -ForegroundColor Yellow
        continue
    }

    Write-Host "Convirtiendo $($item.Source) → $($item.Output)..." -ForegroundColor Cyan

    & pandoc $item.Source `
        -o $item.Output `
        --from markdown `
        --to docx `
        --standalone `
        --toc `
        --toc-depth=3 `
        --metadata title="$($item.Title)"

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ $($item.Output) generado." -ForegroundColor Green
    } else {
        Write-Host "❌ Falló la conversión de $($item.Source)." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Listo. Abrí los .docx con Word, LibreOffice o Google Docs." -ForegroundColor Green
