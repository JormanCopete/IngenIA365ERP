using System.Net;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los dobles de prueba de Cartera (feature 013, US3, T0458; contracts/api.md §20): sólo existen fuera de producción, así que
/// <see cref="DisponiblesAsync"/> responde nulo cuando el servidor no los publica (el 404 de la ruta), y la pantalla y el menú no se dibujan. La
/// tesorería de prueba lista las órdenes recibidas y emite el resultado de una orden (con <see cref="ClaveDeOperacion"/>). US5 (T0561) suma el
/// recaudador de prueba sobre el contrato de obligaciones (§20): listar y simular sin clave; aplicar, reversar por la clave del pago y resolver
/// saldos con su <see cref="ClaveDeOperacion"/> de pantalla, aparte de la clave de negocio <c>TST:{guid:N}</c> que la pantalla propone y deja
/// repetir (C35). Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class DoblesDePruebaClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeDobles = GobiernoDeCarteraClient.Base + "/test-doubles";

    /// <summary>Qué dobles hay; nulo si el servidor no los tiene (producción) o quien pregunta no los opera.</summary>
    public async Task<DoblesDisponiblesDto?> DisponiblesAsync(CancellationToken ct = default)
    {
        var r = await Enviar<DoblesDisponiblesDto>(HttpMethod.Get, RutaDeDobles, null, null, ct);
        return r.IsSuccess ? r.Value : null;
    }

    /// <summary>Qué dobles hay, con el resultado completo: un 404 es «este ambiente no los tiene».</summary>
    public Task<ResultadoDeInventario<DoblesDisponiblesDto>> ConsultarAsync(CancellationToken ct = default) =>
        Enviar<DoblesDisponiblesDto>(HttpMethod.Get, RutaDeDobles, null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<OrdenEnTesoreriaDePruebaDto>>> OrdenesAsync(string? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<OrdenEnTesoreriaDePruebaDto>>(HttpMethod.Get, ConQuery(RutaDeDobles + "/treasury/orders", Query(("status", estado))), null, null, ct);

    /// <summary>Emite el <c>ResultadoDelGiro</c> de la orden como lo haría Tesorería (<c>Disbursed</c>, <c>Rejected</c>, <c>CheckNotCashed</c>, <c>Voided</c>).</summary>
    public Task<ResultadoDeInventario<ResultadoDePruebaEmitidoDto>> ResultadoAsync(Guid orden, ResultadoDePruebaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDePruebaEmitidoDto>(HttpMethod.Post, $"{RutaDeDobles}/treasury/orders/{orden}/results", request, clave, ct);

    // ---------------------------------------------------------------------------------------- recaudador de prueba (US5) --

    /// <summary>La clave de negocio que propone la pantalla para un pago o una resolución del recaudador de prueba.</summary>
    public static string ClaveNueva() => $"TST:{Guid.NewGuid():N}";

    /// <summary>Las obligaciones de la persona como las ve el recaudador (resultado <c>Answered</c>; antes de activar Cartera, la lista vacía con motivo).</summary>
    public Task<ResultadoDeInventario<RespuestaDelContratoDto<ListadoDeObligacionesDto>>> ObligacionesAsync(Guid persona, bool comoCodeudor = false,
        CancellationToken ct = default) =>
        Enviar<RespuestaDelContratoDto<ListadoDeObligacionesDto>>(HttpMethod.Get, ConQuery(RutaDeDobles + "/collector/obligations", Query(
            ("personPublicId", Texto(persona)), ("includeCoDebtor", Texto(comoCodeudor)))), null, null, ct);

    /// <summary>La distribución de un pago por el contrato, sin escribir; con la clave de un pago, la redistribución de su reverso (C38).</summary>
    public Task<ResultadoDeInventario<RespuestaDelContratoDto<SimulacionDelContratoDto>>> SimularPagoAsync(SimulacionDelRecaudadorRequest request,
        CancellationToken ct = default) =>
        Enviar<RespuestaDelContratoDto<SimulacionDelContratoDto>>(HttpMethod.Post, RutaDeDobles + "/collector/simulations", request, null, ct);

    /// <summary>Aplica un lote en forma interactiva (con el <c>simulationId</c> de la simulación) o no interactiva.</summary>
    public Task<ResultadoDeInventario<RespuestaDeAplicacionDto>> AplicarPagosAsync(LoteDePagosDePruebaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<RespuestaDeAplicacionDto>(HttpMethod.Post, RutaDeDobles + "/collector/payments", request, clave, ct);

    /// <summary>Reversa por el contrato el pago aplicado con <paramref name="clavePago"/>.</summary>
    public Task<ResultadoDeInventario<RespuestaDelContratoDto<ComprobanteDeReversoDelContratoDto>>> ReversarPagoAsync(string clavePago, string motivo,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RespuestaDelContratoDto<ComprobanteDeReversoDelContratoDto>>(HttpMethod.Post,
            $"{RutaDeDobles}/collector/payments/{Uri.EscapeDataString(clavePago.Trim())}/reverse", new ReversoDePruebaRequest(motivo), clave, ct);

    /// <summary>Devuelve o aplica un saldo a favor o un anticipo.</summary>
    public Task<ResultadoDeInventario<RespuestaDelContratoDto<ComprobanteDeResolucionDto>>> ResolverSaldoAsync(ResolucionDePruebaRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RespuestaDelContratoDto<ComprobanteDeResolucionDto>>(HttpMethod.Post, RutaDeDobles + "/collector/credit-balances/resolve", request, clave, ct);

    /// <summary>Los <c>PagoReversadoPorCartera</c> que recibió el recaudador de prueba.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PagoReversadoRecibidoDto>>> ReversosRecibidosAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PagoReversadoRecibidoDto>>(HttpMethod.Get, RutaDeDobles + "/collector/reversals", null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
