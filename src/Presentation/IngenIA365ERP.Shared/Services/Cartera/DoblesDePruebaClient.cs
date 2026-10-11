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
/// tesorería de prueba lista las órdenes recibidas y emite el resultado de una orden (con <see cref="ClaveDeOperacion"/>). US5 suma el recaudador.
/// Sin <c>Authorization</c> a mano. (nuevo)
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

    public Task<ResultadoDeInventario<IReadOnlyList<OrdenEnTesoreriaDePruebaDto>>> OrdenesAsync(string? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<OrdenEnTesoreriaDePruebaDto>>(HttpMethod.Get, ConQuery(RutaDeDobles + "/treasury/orders", Query(("status", estado))), null, null, ct);

    /// <summary>Emite el <c>ResultadoDelGiro</c> de la orden como lo haría Tesorería (<c>Disbursed</c>, <c>Rejected</c>, <c>CheckNotCashed</c>, <c>Voided</c>).</summary>
    public Task<ResultadoDeInventario<ResultadoDePruebaEmitidoDto>> ResultadoAsync(Guid orden, ResultadoDePruebaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDePruebaEmitidoDto>(HttpMethod.Post, $"{RutaDeDobles}/treasury/orders/{orden}/results", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
