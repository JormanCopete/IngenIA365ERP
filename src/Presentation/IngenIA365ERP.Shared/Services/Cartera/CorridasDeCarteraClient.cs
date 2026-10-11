using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de las corridas del proceso de Cartera y de la verificación del libro (feature 013, US4, T0507; contracts/api.md §16.4,
/// §16.5): la lista y el detalle con sus errores (sin datos del deudor), ordenar un ciclo diario y reintentar una corrida —las dos con su
/// <see cref="ClaveDeOperacion"/> y respuesta 202: sólo encolan—, y verificar el libro (consulta por POST, sin clave). Sin
/// <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class CorridasDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeCorridas = GobiernoDeCarteraClient.Base + "/process-runs";
    public const string RutaDeIntegridad = GobiernoDeCarteraClient.Base + "/integrity/verify";

    /// <summary>Las corridas por clase (<c>DailyCycle</c>, <c>NationalValueCorrection</c>…), estado y fechas; la más reciente primero.</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<CorridaEnListaDto>>> ListarAsync(string? clase = null, string? estado = null, DateOnly? desde = null,
        DateOnly? hasta = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<CorridaEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeCorridas, Query(
            ("kind", clase), ("status", estado), ("from", Texto(desde)), ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))),
            null, null, ct);

    public Task<ResultadoDeInventario<CorridaDto>> CorridaAsync(Guid corrida, int pagina = 1, CancellationToken ct = default) =>
        Enviar<CorridaDto>(HttpMethod.Get, ConQuery($"{RutaDeCorridas}/{corrida}", Query(("page", Texto(pagina)))), null, null, ct);

    /// <summary>Encola el ciclo diario de <paramref name="fecha"/> (hoy si no viene) a nombre de quien lo pide.</summary>
    public Task<ResultadoDeInventario<CorridaPedidaDto>> OrdenarCicloAsync(DateOnly? fecha, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CorridaPedidaDto>(HttpMethod.Post, RutaDeCorridas, new OrdenDeCorridaRequest("DailyCycle", fecha), clave, ct);

    /// <summary>Vuelve a encolar una corrida que falló o terminó con créditos sin causar; retoma desde la marca de cada crédito.</summary>
    public Task<ResultadoDeInventario<CorridaPedidaDto>> ReintentarAsync(Guid corrida, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CorridaPedidaDto>(HttpMethod.Post, $"{RutaDeCorridas}/{corrida}/retry", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Compara el libro con la reconstrucción; no corrige nada (una diferencia levanta la alerta del libro descuadrado).</summary>
    public Task<ResultadoDeInventario<VerificacionDelLibroDto>> VerificarLibroAsync(VerificacionDelLibroRequest pedido, CancellationToken ct = default) =>
        Enviar<VerificacionDelLibroDto>(HttpMethod.Post, RutaDeIntegridad, pedido, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
