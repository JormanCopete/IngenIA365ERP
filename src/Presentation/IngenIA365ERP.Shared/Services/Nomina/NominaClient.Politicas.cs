using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 010: políticas por empresa con vigencia (contracts/api.md §10.1).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<IReadOnlyList<PoliticaEmpresaDto>>> ListarPoliticasAsync(DateOnly? vigenteA = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<PoliticaEmpresaDto>>(HttpMethod.Get,
            "/api/payroll/company-policies" + (vigenteA is { } d ? $"?asOf={d:yyyy-MM-dd}" : string.Empty), null, ct);

    public Task<InvitationApiResult<IReadOnlyList<VigenciaPoliticaDto>>> VigenciasDePoliticaAsync(string clave, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<VigenciaPoliticaDto>>(HttpMethod.Get, $"/api/payroll/company-policies/{Uri.EscapeDataString(clave)}/versions", null, ct);

    public Task<InvitationApiResult<VigenciaPoliticaCreadaDto>> NuevaVigenciaDePoliticaAsync(string clave, NuevaVigenciaPoliticaRequest request, CancellationToken ct = default) =>
        EnviarAsync<VigenciaPoliticaCreadaDto>(HttpMethod.Post, $"/api/payroll/company-policies/{Uri.EscapeDataString(clave)}/versions", request, ct);
}
