using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los pagos por pantalla de Cartera (feature 013, US5, T0561; contracts/api.md §8.3 y §10). <b>No hay ruta para aplicar
/// pagos</b>: todo dinero entra por el contrato de obligaciones dentro del recaudador. Por pantalla sólo se simula (por POST y sin clave: no
/// escribe), se ve la vista previa del reverso (sin clave), se reversa (con <see cref="ClaveDeOperacion"/> y la huella que se vio) y se lee o
/// cambia la preferencia del deudor para abonos a capital (el cambio con clave). Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class PagosClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeCreditos = CreditosClient.RutaDeCreditos;

    /// <summary>Lo que haría un pago con la misma función que lo distribuye al aplicar (FR-072). No escribe.</summary>
    public Task<ResultadoDeInventario<PagoSimuladoDto>> SimularAsync(Guid credito, SimulacionDePagoRequest request, CancellationToken ct = default) =>
        Enviar<PagoSimuladoDto>(HttpMethod.Post, $"{RutaDeCreditos}/{credito}/payment-simulations", request, null, ct);

    /// <summary>Lo que haría el reverso del pago: lo que restituye, las reasignaciones y los documentos que se anularían, con su huella. No escribe.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeReversoDto>> VistaPreviaDelReversoAsync(Guid credito, Guid operacion, string? motivo = null,
        CancellationToken ct = default) =>
        Enviar<VistaPreviaDeReversoDto>(HttpMethod.Post, $"{RutaDeCreditos}/{credito}/operations/{operacion}/reversal-preview",
            new VistaPreviaDeReversoRequest(string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()), null, ct);

    /// <summary>Reversa el pago con la huella de la vista previa que se vio (si el crédito cambió, <c>Lending.Reversal.PreviewChanged</c>).</summary>
    public Task<ResultadoDeInventario<PagoReversadoDto>> ReversarAsync(Guid credito, Guid operacion, ReversoDePagoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PagoReversadoDto>(HttpMethod.Post, $"{RutaDeCreditos}/{credito}/operations/{operacion}/reverse", request, clave, ct);

    /// <summary>La preferencia vigente del deudor y su historia.</summary>
    public Task<ResultadoDeInventario<PreferenciaDePrepagoDto>> PreferenciaAsync(Guid credito, CancellationToken ct = default) =>
        Enviar<PreferenciaDePrepagoDto>(HttpMethod.Get, $"{RutaDeCreditos}/{credito}/prepayment-preference", null, null, ct);

    /// <summary>Cambia (o revoca, con elección nula) la instrucción permanente del deudor para los abonos a capital (Ley 2032).</summary>
    public Task<ResultadoDeInventario<PreferenciaDePrepagoDto>> CambiarPreferenciaAsync(Guid credito, CambioDePreferenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PreferenciaDePrepagoDto>(HttpMethod.Post, $"{RutaDeCreditos}/{credito}/prepayment-preference", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
