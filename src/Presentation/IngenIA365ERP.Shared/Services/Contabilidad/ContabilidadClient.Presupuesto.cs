using System.Globalization;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Presupuesto y ejecución presupuestal (feature 009 E2, US9, FR-061..FR-064) sobre
/// <c>/api/accounting/budgets</c>. La ejecución no está aquí: es la vista
/// <c>budget-execution</c> de <see cref="InformeAsync"/>, porque es un informe más y se exporta
/// como los demás.
/// </summary>
public sealed partial class ContabilidadClient
{
    private const string BasePresupuesto = "/api/accounting/budgets";

    /// <summary>El presupuesto vigente del año, o la versión pedida. Sin presupuesto responde éxito con <c>Status = "None"</c>, no 404.</summary>
    public Task<InvitationApiResult<PresupuestoDto>> ObtenerPresupuestoAsync(int year, int? version = null, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Get,
            $"{BasePresupuesto}?year={year}{(version is { } v ? $"&version={v}" : string.Empty)}", null, ct);

    public Task<InvitationApiResult<PresupuestoDto>> CrearPresupuestoAsync(int year, IReadOnlyList<LineaPresupuestoInput> lineas, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post, BasePresupuesto, new CrearPresupuestoRequest(year, lineas), ct);

    /// <summary>Sobre un borrador reemplaza las líneas; sobre un aprobado exige <paramref name="motivo"/> y crea la versión siguiente.</summary>
    public Task<InvitationApiResult<PresupuestoDto>> ActualizarPresupuestoAsync(int year, IReadOnlyList<LineaPresupuestoInput> lineas, string? motivo, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Put, $"{BasePresupuesto}/{year}", new ActualizarPresupuestoRequest(lineas, motivo), ct);

    public Task<InvitationApiResult<PresupuestoDto>> AprobarPresupuestoAsync(int year, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post, $"{BasePresupuesto}/{year}/approve", null, ct);

    /// <summary>Copia la versión vigente de <paramref name="anterior"/> al borrador de <paramref name="year"/>, ajustada en <paramref name="porcentaje"/> % y redondeada a pesos.</summary>
    public Task<InvitationApiResult<PresupuestoDto>> CopiarPresupuestoAsync(int year, int anterior, decimal porcentaje, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post,
            $"{BasePresupuesto}/{year}/copy-from/{anterior}?adjustPercent={porcentaje.ToString(CultureInfo.InvariantCulture)}", null, ct);

    public Task<InvitationApiResult<PresupuestoDto>> DistribuirPresupuestoAsync(int year, DistribucionInput input, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post, $"{BasePresupuesto}/{year}/distribute", input, ct);
}
