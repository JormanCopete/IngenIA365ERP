using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de captaciones y aportes (feature 014, T057; contracts/api.md de la 014): nace con la activación <b>sin carga</b> de aportes o de
/// captaciones (§16, <c>Lending.Deposits.Activate</c>) y cada historia le suma sus rutas (líneas, cuentas, CDAT, aportes, retiro del asociado…). Sin
/// <c>Authorization</c> a mano: la pone el handler; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class CaptacionesClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = GobiernoDeCarteraClient.Base;
    public const string RutaDeActivacion = Base + "/deposits/activation";

    /// <summary>Las naturalezas que se activan aparte de los créditos (<c>LineNature</c>).</summary>
    public const string Captaciones = "Deposit";
    public const string Aportes = "Contribution";

    /// <summary>
    /// Activa aportes o captaciones sin carga (cooperativa sin historia, FR-117) desde <see cref="ActivarCaptacionesRequest.ActivationDate"/>.
    /// Responde 422 con <c>Lending.Activation.LoansFirst</c>, <c>.RequirementsMissing</c> (<c>data.missing</c>), <c>.AlreadyActivated</c> o
    /// <c>Lending.Savings.DeliveryNotAvailable</c>.
    /// </summary>
    public Task<ResultadoDeInventario<CaptacionesActivadasDto>> ActivarAsync(ActivarCaptacionesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CaptacionesActivadasDto>(HttpMethod.Post, RutaDeActivacion, request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}

/// <summary>El cuerpo de <c>POST /api/lending/deposits/activation</c>: <c>nature</c> es <c>Deposit</c> o <c>Contribution</c>. (nuevo)</summary>
public sealed record ActivarCaptacionesRequest(
    string Nature,
    DateOnly ActivationDate,
    string Reason,
    bool AcknowledgeWarnings = false,
    Guid? MigrationLoadPublicId = null,
    Guid? SessionItemPublicId = null);

/// <summary>Lo que responde la activación de aportes o captaciones. (nuevo)</summary>
public sealed record CaptacionesActivadasDto(
    Guid ActivationPublicId,
    string Nature,
    DateOnly ActivationDate,
    int Accounts,
    int OpeningTransactions,
    IReadOnlyList<MensajeEmitidoDto> Messages,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto> Warnings);
