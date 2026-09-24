using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 010 (US8): archivos de dispersión bancaria y formatos de archivo plano (contracts/api.md §9).</summary>
public sealed partial class NominaClient
{
    private const string RutaDispersion = "/api/payroll/disbursements";
    private const string RutaFormatos = "/api/core/bank-file-formats";

    // ------------------------------------------------------------- formatos --

    public Task<InvitationApiResult<IReadOnlyList<FormatoBancarioResumenDto>>> ListarFormatosBancariosAsync(string? ambito = null, Guid? bancoId = null, bool soloActivos = false, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (ambito is not null) q.Add($"scope={ambito}");
        if (bancoId is { } b) q.Add($"bankId={b}");
        if (soloActivos) q.Add("onlyActive=true");
        return EnviarAsync<IReadOnlyList<FormatoBancarioResumenDto>>(HttpMethod.Get, RutaFormatos + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty), null, ct);
    }

    public Task<InvitationApiResult<FormatoBancarioDetalleDto>> FormatoBancarioAsync(Guid formatoId, CancellationToken ct = default) =>
        EnviarAsync<FormatoBancarioDetalleDto>(HttpMethod.Get, $"{RutaFormatos}/{formatoId}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<OrigenDeCampoDto>>> OrigenesDeCampoAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<OrigenDeCampoDto>>(HttpMethod.Get, $"{RutaFormatos}/sources", null, ct);

    public Task<InvitationApiResult<FormatoBancarioCreadoDto>> CrearFormatoBancarioAsync(FormatoBancarioDefinicion definicion, CancellationToken ct = default) =>
        EnviarAsync<FormatoBancarioCreadoDto>(HttpMethod.Post, RutaFormatos, definicion, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarFormatoBancarioAsync(Guid formatoId, FormatoBancarioDefinicion definicion, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{RutaFormatos}/{formatoId}", definicion, ct);

    public Task<InvitationApiResult<EmptyResponse>> EliminarFormatoBancarioAsync(Guid formatoId, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"{RutaFormatos}/{formatoId}", null, ct);

    // ------------------------------------------------------------- archivos --

    public Task<InvitationApiResult<IReadOnlyList<ArchivoDeDispersionDto>>> ListarDispersionesAsync(Guid? corridaId = null, int? anio = null, string? estado = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (corridaId is { } c) q.Add($"runId={c}");
        if (anio is { } a) q.Add($"year={a}");
        if (estado is not null) q.Add($"status={estado}");
        return EnviarAsync<IReadOnlyList<ArchivoDeDispersionDto>>(HttpMethod.Get, RutaDispersion + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty), null, ct);
    }

    public Task<InvitationApiResult<DetalleDeDispersionDto>> DispersionAsync(Guid archivoId, CancellationToken ct = default) =>
        EnviarAsync<DetalleDeDispersionDto>(HttpMethod.Get, $"{RutaDispersion}/{archivoId}", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarDispersionAsync(Guid archivoId, CancellationToken ct = default) =>
        DescargarAsync($"{RutaDispersion}/{archivoId}/file", ct);

    /// <summary>
    /// Feature 011 (§9): el enlace firmado del archivo. Con <c>direct: false</c> (guardado con el formato
    /// anterior) hay que bajarlo por <see cref="DescargarDispersionAsync"/>.
    /// </summary>
    public async Task<InvitationApiResult<Adjuntos.EnlaceDeDescargaDto>> EnlaceDeDispersionAsync(Guid archivoId, CancellationToken ct = default)
    {
        var r = await EnviarAsync<Adjuntos.EnlaceDeDescargaDto>(HttpMethod.Post, $"{RutaDispersion}/{archivoId}/download-link", null, ct);
        return r.IsSuccess && r.Value is { Url: { } url } enlace
            ? InvitationApiResult<Adjuntos.EnlaceDeDescargaDto>.Success(enlace with { Url = Adjuntos.EnlacesFirmados.Absoluta(http, url) })
            : r;
    }

    public Task<InvitationApiResult<VistaPreviaDeDispersionDto>> VistaPreviaDeDispersionAsync(VistaPreviaDeDispersionRequest request, CancellationToken ct = default) =>
        EnviarAsync<VistaPreviaDeDispersionDto>(HttpMethod.Post, $"{RutaDispersion}/preview", request, ct);

    public Task<InvitationApiResult<DispersionGeneradaDto>> GenerarDispersionAsync(GenerarDispersionRequest request, CancellationToken ct = default) =>
        EnviarAsync<DispersionGeneradaDto>(HttpMethod.Post, RutaDispersion, request, ct);

    public Task<InvitationApiResult<DispersionEnviadaDto>> MarcarDispersionEnviadaAsync(Guid archivoId, MarcarEnviadoRequest request, CancellationToken ct = default) =>
        EnviarAsync<DispersionEnviadaDto>(HttpMethod.Post, $"{RutaDispersion}/{archivoId}/mark-sent", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> AnularDispersionAsync(Guid archivoId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{RutaDispersion}/{archivoId}/cancel", new AnularDispersionRequest(motivo), ct);

    /// <summary>Las cuentas bancarias del plan (Contabilidad): la cuenta origen del archivo.</summary>
    public Task<InvitationApiResult<IReadOnlyList<CuentaBancariaDelPlanDto>>> CuentasBancariasAsync(Guid? bancoId = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CuentaBancariaDelPlanDto>>(HttpMethod.Get, "/api/accounting/accounts/bank-accounts" + (bancoId is { } b ? $"?bankId={b}" : string.Empty), null, ct);

    /// <summary>La vista <c>dispersion</c> del centro de reportes (líneas y pendientes de un archivo).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteDeDispersionAsync(Guid archivoId, string formato, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/dispersion?fileId={archivoId}&format={formato}", ct);
}

public sealed record CuentaBancariaDelPlanDto(Guid AccountPublicId, string Code, string Name, Guid BankPublicId, string BankName, string? BankAccountNumber, string? BankTransferCode, bool IsActive)
{
    public string Texto => $"{BankName} · {BankAccountNumber ?? Code} ({Name})";
}
