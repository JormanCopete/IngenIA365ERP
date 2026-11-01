using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>Activos fijos y diferidos (feature 009 E4, US12) sobre <c>/api/accounting/assets</c>. El inventario exportable es la vista <c>assets</c> de <see cref="InformeAsync"/>.</summary>
public sealed partial class ContabilidadClient
{
    private const string BaseActivos = "/api/accounting/assets";

    public Task<InvitationApiResult<IReadOnlyList<ActivoDto>>> ListarActivosAsync(string? kind = null, string? status = null, string? search = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(kind)) q.Add($"kind={Uri.EscapeDataString(kind)}");
        if (!string.IsNullOrWhiteSpace(status)) q.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search)}");
        return EnviarAsync<IReadOnlyList<ActivoDto>>(HttpMethod.Get, q.Count == 0 ? BaseActivos : $"{BaseActivos}?{string.Join('&', q)}", null, ct);
    }

    public Task<InvitationApiResult<FichaDeActivoDto>> ObtenerActivoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<FichaDeActivoDto>(HttpMethod.Get, $"{BaseActivos}/{id}", null, ct);

    public Task<InvitationApiResult<ActivoCreadoDto>> RegistrarActivoAsync(ActivoInput input, CancellationToken ct = default) =>
        EnviarAsync<ActivoCreadoDto>(HttpMethod.Post, BaseActivos, input, ct);

    public Task<InvitationApiResult<EmptyResponse>> CorregirActivoAsync(Guid id, ActivoInput input, string? motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{BaseActivos}/{id}", new { Asset = input, Reason = motivo }, ct);

    public Task<InvitationApiResult<BajaDeActivoDto>> DarDeBajaActivoAsync(Guid id, DateOnly fecha, Guid cuentaContrapartida, string motivo, CancellationToken ct = default) =>
        EnviarAsync<BajaDeActivoDto>(HttpMethod.Post, $"{BaseActivos}/{id}/retire", new { Date = fecha, CounterAccountPublicId = cuentaContrapartida, Reason = motivo }, ct);

    public Task<InvitationApiResult<IReadOnlyList<CorridaDeActivosDto>>> ListarCorridasDeActivosAsync(int? year = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CorridaDeActivosDto>>(HttpMethod.Get, year is { } y ? $"{BaseActivos}/runs?year={y}" : $"{BaseActivos}/runs", null, ct);

    public Task<InvitationApiResult<VistaPreviaDeCorridaDto>> VistaPreviaDeCorridaAsync(int year, int month, CancellationToken ct = default) =>
        EnviarAsync<VistaPreviaDeCorridaDto>(HttpMethod.Get, $"{BaseActivos}/runs/preview?year={year}&month={month}", null, ct);

    public Task<InvitationApiResult<CorridaDeActivosDto>> CorrerDepreciacionAsync(int year, int month, CancellationToken ct = default) =>
        EnviarAsync<CorridaDeActivosDto>(HttpMethod.Post, $"{BaseActivos}/runs", new { Year = year, Month = month }, ct);

    public Task<InvitationApiResult<CorridaDeActivosDto>> ReversarCorridaDeActivosAsync(Guid id, string motivo, CancellationToken ct = default) =>
        EnviarAsync<CorridaDeActivosDto>(HttpMethod.Post, $"{BaseActivos}/runs/{id}/reverse", new { Reason = motivo }, ct);
}

/// <summary>Espejo de <c>FixedAssetInput</c>.</summary>
public sealed record ActivoInput(
    string Code, string Name, string Kind, Guid AssetAccountPublicId, Guid? AccumulatedAccountPublicId, Guid ExpenseAccountPublicId,
    Guid BranchPublicId, Guid? CostCenterPublicId, Guid? SupplierPersonPublicId, DateOnly PurchaseDate, string? PurchaseDocument,
    decimal Cost, decimal ResidualValue, int LifeMonths, DateOnly StartDate, decimal OpeningAccumulated, int OpeningElapsedMonths);

public sealed record CuentaDeActivoDto(Guid PublicId, string Code, string Name)
{
    public string Texto => $"{Code} - {Name}";
}

public sealed record ActivoDto(
    Guid PublicId, string Code, string Name, string Kind, string Status,
    CuentaDeActivoDto AssetAccount, CuentaDeActivoDto? AccumulatedAccount, CuentaDeActivoDto ExpenseAccount,
    Guid BranchPublicId, string BranchName, Guid? CostCenterPublicId, string? CostCenterName, Guid? SupplierPersonPublicId, string? SupplierName,
    DateOnly PurchaseDate, string? PurchaseDocument, decimal Cost, decimal ResidualValue, int LifeMonths, DateOnly StartDate,
    decimal OpeningAccumulated, int OpeningElapsedMonths, decimal Accumulated, decimal NetBookValue, int RemainingMonths,
    DateOnly? RetiredAt, string? RetireReason, Guid? RetireDocumentPublicId, bool HasPostedInstallments)
{
    public bool EsDiferido => Kind == "Deferred";
    public string ClaseTexto => EsDiferido ? "Diferido" : "Activo fijo";
    public string EstadoTexto => Status switch
    {
        "Active" => "Vigente",
        "FullyDepreciated" => "Totalmente depreciado",
        "Retired" => "Dado de baja",
        _ => Status,
    };
}

public sealed record CuotaDeActivoDto(int Year, int Month, decimal Amount, string Status, Guid? DocumentPublicId, string? DocumentNumber);

public sealed record CuotaProyectadaDto(int Year, int Month, decimal Amount, decimal Accumulated, decimal NetBookValue);

public sealed record FichaDeActivoDto(ActivoDto Asset, IReadOnlyList<CuotaDeActivoDto> Installments, IReadOnlyList<CuotaProyectadaDto> Projection);

public sealed record ActivoCreadoDto(Guid PublicId);

public sealed record BajaDeActivoDto(Guid? DocumentPublicId);

public sealed record CorridaDeActivosDto(
    Guid PublicId, int Year, int Month, string Status, Guid DocumentPublicId, string DocumentNumber, int Assets, decimal Total,
    DateTime ExecutedAt, string ExecutedBy, Guid? ReversalDocumentPublicId, string? ReversalDocumentNumber)
{
    public bool Vigente => Status == "Posted";
}

public sealed record CuotaDeLaCorridaDto(Guid AssetPublicId, string Code, string Name, string Kind, decimal Amount, int Months);

public sealed record VistaPreviaDeCorridaDto(int Year, int Month, bool AlreadyRun, IReadOnlyList<CuotaDeLaCorridaDto> Lines, decimal Total);
