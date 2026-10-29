using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Un requisito de la activación de Recaudo o Tesorería. (nuevo)</summary>
public sealed record RequisitoDeActivacionDeModuloDto(string Code, bool Met, string Description);

/// <summary>Los requisitos de la activación, si se puede activar y el cuadre que tomaría (o tomó). (nuevo)</summary>
public sealed record EstadoDeActivacionDeModuloDto(string Status, DateOnly? ActivatedOn, bool IsMinimal, bool CanActivate, IReadOnlyList<RequisitoDeActivacionDeModuloDto> Requirements,
    Guid? OpeningSquarePublicId, string? OpeningSquareKind, DateOnly? CutoffDate);

public sealed record ActivacionDeModuloRequest(DateOnly? CutoffDate, bool? Minimal = null, bool? OffersChecks = null);

/// <summary>Feature 015, R1 (T0352): las versiones de los contratos de Cartera y Captaciones que Recaudo usa. (nuevo)</summary>
public sealed record VersionesDeContratosDeRecaudoDto(int Obligations, int Accounts, DateOnly? AdoptedFrom);

/// <summary>
/// Cliente tipado de la activación de Recaudo y de Tesorería (feature 015, US9, T0304; api.md §14): los requisitos completos (<c>GET …/activation/requirements</c>) y
/// la activación (<c>POST …/activation</c>). <c>modulo</c> es <c>receipts</c> o <c>treasury</c>, como <see cref="PuestaEnMarchaClient"/>. (nuevo)
/// </summary>
public sealed class ActivacionDeModuloClient(HttpClient http, CentralAuthClient auth)
{
    public Task<ResultadoDeInventario<EstadoDeActivacionDeModuloDto>> RequisitosAsync(string modulo, DateOnly? corte, bool ofreceCheques, CancellationToken ct = default)
    {
        var parametros = new List<string>();
        if (corte is { } c) parametros.Add($"cutoffDate={c:yyyy-MM-dd}");
        if (ofreceCheques) parametros.Add("offersChecks=true");
        var url = $"/api/{modulo}/activation/requirements" + (parametros.Count == 0 ? string.Empty : "?" + string.Join("&", parametros));
        return EnvioDeRecaudo.EnviarAsync<EstadoDeActivacionDeModuloDto>(http, auth, HttpMethod.Get, url, null, null, null, ct);
    }

    public Task<ResultadoDeInventario<EstadoDeActivacionDeModuloDto>> ActivarAsync(string modulo, ActivacionDeModuloRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<EstadoDeActivacionDeModuloDto>(http, auth, HttpMethod.Post, $"/api/{modulo}/activation", r, clave, null, ct);

    /// <summary>Feature 015, R1 (T0352): Recaudo adopta la versión 2 de los contratos (<c>POST /api/receipts/activation/contract-versions</c>).</summary>
    public Task<ResultadoDeInventario<VersionesDeContratosDeRecaudoDto>> AdoptarVersion2Async(ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<VersionesDeContratosDeRecaudoDto>(http, auth, HttpMethod.Post, "/api/receipts/activation/contract-versions",
            new { obligations = 2, accounts = 2 }, clave, null, ct);
}
