using Microsoft.JSInterop;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US4: comparativo, cuadre y exportación (contracts/api.md §4).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<ComparativoDto>> ComparativoAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<ComparativoDto>(HttpMethod.Get, $"/api/payroll/runs/{corridaId}/comparison", null, ct);

    public Task<InvitationApiResult<CuadreDto>> CuadreAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<CuadreDto>(HttpMethod.Get, $"/api/payroll/runs/{corridaId}/balance-check", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> ExportarAsync(Guid corridaId, CancellationToken ct = default) =>
        DescargarAsync($"/api/payroll/runs/{corridaId}/export", ct);
}

public sealed record FilaComparativoDto(Guid EmployeePublicId, string EmployeeName, decimal? PreviousNet, decimal CurrentNet, decimal? VariationPercent, bool OverThreshold, bool NewEmployee, bool LeftEmployee);

public sealed record ComparativoDto(Guid RunPublicId, Guid? PreviousRunPublicId, Guid? PreviousPeriodPublicId, string? PreviousPeriodLabel, decimal ThresholdPercent, IReadOnlyList<FilaComparativoDto> Rows);

public sealed record CuadreDto(bool EarningsMinusDeductionsEqualsNet, bool EmployerAndProvisionsOutsideNet, bool? AccountingDocumentBalanced, bool LinesMatchEmployeeTotals, IReadOnlyList<string> Details);

/// <summary>
/// Entrega un archivo al navegador desde Blazor (WASM o servidor interactivo): crea un
/// blob y dispara la descarga. En prerenderizado no hay JS y el botón no se muestra.
/// </summary>
public sealed class DescargaDeArchivos(IJSRuntime js)
{
    public async Task GuardarAsync(string nombre, string tipoContenido, byte[] contenido)
    {
        var base64 = Convert.ToBase64String(contenido);
        await js.InvokeVoidAsync("eval",
            $"(function(){{var a=document.createElement('a');a.href='data:{tipoContenido};base64,{base64}';a.download='{nombre.Replace("'", string.Empty)}';document.body.appendChild(a);a.click();a.remove();}})();");
    }
}
