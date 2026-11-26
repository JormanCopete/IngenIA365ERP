using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los cargos masivos (2026-11-25, pedido del dueño; feature 026; <c>/api/lending/bulk-charges</c>): las opciones, la lista, el detalle
/// y las líneas —consultas, sin clave—; crear, cambiar, recalcular la vista previa, descartar, confirmar y anular —escrituras, cada una con su
/// <see cref="ClaveDeOperacion"/>—. Sin <c>Authorization</c> a mano: la pone el handler de la sesión. (nuevo)
/// </summary>
public sealed class CargosMasivosClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = GobiernoDeCarteraClient.Base + "/bulk-charges";

    /// <summary>El código del 409 cuando la vista previa cambió entre lo revisado y la confirmación.</summary>
    public const string VistaPreviaCambiada = "Lending.BulkCharge.PreviewChanged";

    public Task<ResultadoDeInventario<OpcionesDeCargoMasivoDto>> OpcionesAsync(CancellationToken ct = default) =>
        Enviar<OpcionesDeCargoMasivoDto>(HttpMethod.Get, $"{Ruta}/options", null, null, ct);

    /// <summary>Las corridas, por línea, período (aaaamm) y estado (nombre del enum).</summary>
    public Task<ResultadoDeInventario<List<CargoMasivoEnListaDto>>> ListarAsync(Guid? linea = null, int? periodo = null, string? estado = null,
        int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<List<CargoMasivoEnListaDto>>(HttpMethod.Get, ConQuery(Ruta, Query(("creditLinePublicId", Texto(linea)), ("period", Texto(periodo)),
            ("status", estado), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<CargoMasivoDto>> CorridaAsync(Guid corrida, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Get, $"{Ruta}/{corrida}", null, null, ct);

    /// <summary>Las líneas de la corrida, por estado (nombre del enum).</summary>
    public Task<ResultadoDeInventario<PaginaDeLineasDeCargoMasivoDto>> LineasAsync(Guid corrida, string? estado = null, int pagina = 1, int tamano = 100,
        CancellationToken ct = default) =>
        Enviar<PaginaDeLineasDeCargoMasivoDto>(HttpMethod.Get, ConQuery($"{Ruta}/{corrida}/lines", Query(("status", estado), ("page", Texto(pagina)),
            ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Crea el borrador y calcula su vista previa (201).</summary>
    public Task<ResultadoDeInventario<CargoMasivoDto>> CrearAsync(CargoMasivoRequest pedido, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Post, Ruta, pedido, clave, ct);

    /// <summary>Cambia los datos del borrador y recalcula la vista previa.</summary>
    public Task<ResultadoDeInventario<CargoMasivoDto>> CambiarAsync(Guid corrida, CargoMasivoRequest pedido, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Put, $"{Ruta}/{corrida}", pedido, clave, ct);

    /// <summary>Recalcula la vista previa con los mismos datos.</summary>
    public Task<ResultadoDeInventario<CargoMasivoDto>> RecalcularAsync(Guid corrida, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Post, $"{Ruta}/{corrida}/preview", null, clave, ct);

    public Task<ResultadoDeInventario<CargoMasivoDto>> DescartarAsync(Guid corrida, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Post, $"{Ruta}/{corrida}/discard", null, clave, ct);

    /// <summary>Confirma la vista previa revisada (<paramref name="huella"/>); si cambió, 409 <see cref="VistaPreviaCambiada"/>.</summary>
    public Task<ResultadoDeInventario<CargoMasivoDto>> ConfirmarAsync(Guid corrida, string huella, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Post, $"{Ruta}/{corrida}/confirm", new ConfirmacionDeCargoMasivoRequest(huella), clave, ct);

    /// <summary>Anula lo vivo de la corrida en segundo plano.</summary>
    public Task<ResultadoDeInventario<CargoMasivoDto>> AnularAsync(Guid corrida, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Post, $"{Ruta}/{corrida}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Anula el cargo de un asociado.</summary>
    public Task<ResultadoDeInventario<CargoMasivoDto>> AnularLineaAsync(Guid corrida, Guid linea, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CargoMasivoDto>(HttpMethod.Post, $"{Ruta}/{corrida}/lines/{linea}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
