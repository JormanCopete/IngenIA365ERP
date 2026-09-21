using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 010: calendario de festivos (contracts/api.md §10.2).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<IReadOnlyList<FestivoDto>>> ListarFestivosAsync(int anio, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<FestivoDto>>(HttpMethod.Get, $"/api/payroll/holidays?year={anio}", null, ct);

    public Task<InvitationApiResult<CreadoFestivoDto>> CrearFestivoAsync(NuevoFestivoRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoFestivoDto>(HttpMethod.Post, "/api/payroll/holidays", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> EliminarFestivoAsync(Guid festivoId, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"/api/payroll/holidays/{festivoId}", null, ct);
}

public sealed record CreadoFestivoDto(Guid HolidayPublicId);
