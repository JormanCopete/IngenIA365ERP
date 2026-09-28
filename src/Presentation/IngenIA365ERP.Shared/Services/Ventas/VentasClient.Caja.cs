using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>La caja: sesiones, esperado, cierre y recuento, movimientos y cierre del día (contracts/api.md §21). (nuevo)</summary>
public sealed partial class VentasClient
{
    // ---------------------------------------------------------------------------------- sesiones --

    /// <summary>Las sesiones; <paramref name="estado"/> es el número de <c>CashSessionStatus</c> y viaja por nombre.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SesionDeCajaDto>>> ListarSesionesAsync(bool? mias = null, Guid? punto = null, Guid? caja = null,
        int? estado = null, DateOnly? desde = null, DateOnly? hasta = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<SesionDeCajaDto>>(HttpMethod.Get, Q(Rutas.Sesiones,
            ("mine", Texto(mias)), ("pointOfSale", Texto(punto)), ("cashRegister", Texto(caja)),
            ("status", estado is { } e ? TextosDeVentas.NombreDeEstadoDeSesion(e) : null), ("from", Texto(desde)), ("to", Texto(hasta)),
            ("page", Texto(pagina)), ("pageSize", Texto(tamano))), null, null, ct);

    /// <summary>La sesión abierta del usuario, o nula si no tiene (el POS y el menú la consultan al arrancar).</summary>
    public async Task<ResultadoDeInventario<SesionDeCajaDto?>> MiSesionAbiertaAsync(CancellationToken ct = default)
    {
        var r = await ListarSesionesAsync(mias: true, estado: TextosDeVentas.SesionAbierta, pagina: 1, tamano: 1, ct: ct);
        return new(r.IsSuccess, r.Value?.Items.FirstOrDefault(), r.ErrorCode, r.ErrorMessage, r.StatusCode, r.Data, r.Repetida);
    }

    public Task<ResultadoDeInventario<DetalleDeSesionDto>> ObtenerSesionAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DetalleDeSesionDto>(HttpMethod.Get, $"{Rutas.Sesiones}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<SesionAbiertaDto>> AbrirSesionAsync(AbrirSesionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SesionAbiertaDto>(HttpMethod.Post, Rutas.Sesiones, request, clave, ct);

    /// <summary>El esperado por medio; en arqueo ciego vienen nulos.</summary>
    public Task<ResultadoDeInventario<EsperadoDeSesionDto>> EsperadoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<EsperadoDeSesionDto>(HttpMethod.Get, $"{Rutas.Sesiones}/{id}/expected", null, null, ct);

    public Task<ResultadoDeInventario<ResultadoDeCierreDto>> CerrarSesionAsync(Guid id, CerrarSesionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeCierreDto>(HttpMethod.Post, $"{Rutas.Sesiones}/{id}/close", request, clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeCierreDto>> RecontarSesionAsync(Guid id, IReadOnlyList<ConteoPorMedioRequest> conteos, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeCierreDto>(HttpMethod.Post, $"{Rutas.Sesiones}/{id}/recount", new CerrarSesionRequest(conteos), clave, ct);

    /// <summary>El informe del arqueo en PDF (<c>CashCountReport</c>).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarArqueoAsync(Guid id, CancellationToken ct = default) =>
        Descargar($"{Rutas.Sesiones}/{id}/count-report", $"arqueo-{id:N}.pdf", ct);

    // ------------------------------------------------------------------------------- movimientos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<MovimientoDeCajaDto>>> ListarMovimientosAsync(Guid? sesion = null, Guid? punto = null, int? clase = null,
        int? estado = null, DateOnly? desde = null, DateOnly? hasta = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<MovimientoDeCajaDto>>(HttpMethod.Get, Q(Rutas.Movimientos,
            ("cashSession", Texto(sesion)), ("pointOfSale", Texto(punto)), ("kind", clase is { } k ? TextosDeVentas.NombreDeClaseDeMovimiento(k) : null),
            ("status", Texto(estado)), ("from", Texto(desde)), ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano))), null, null, ct);

    public Task<ResultadoDeInventario<MovimientoDeCajaDto>> ObtenerMovimientoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<MovimientoDeCajaDto>(HttpMethod.Get, $"{Rutas.Movimientos}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<MovimientoDeCajaDto>> GuardarMovimientoAsync(Guid? id, MovimientoDeCajaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } existente
            ? Enviar<MovimientoDeCajaDto>(HttpMethod.Put, $"{Rutas.Movimientos}/{existente}", request, clave, ct)
            : Enviar<MovimientoDeCajaDto>(HttpMethod.Post, Rutas.Movimientos, request, clave, ct);

    /// <summary>Confirmar: con la política del tipo queda en aprobación (sin número) hasta que decida quien la tenga.</summary>
    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> ConfirmarMovimientoAsync(Guid id, byte[]? rowVersion, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeConfirmacionDto>(HttpMethod.Post, $"{Rutas.Movimientos}/{id}/confirm", new ConfirmacionRequest(rowVersion), clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularMovimientoAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeAnulacionDto>(HttpMethod.Post, $"{Rutas.Movimientos}/{id}/void", new AnulacionRequest(motivo, null), clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarMovimientoAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Rutas.Movimientos}/{id}/discard", new MotivoRequest(motivo), clave, ct);

    /// <summary>El comprobante del movimiento con sus firmas, en PDF.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarComprobanteDeMovimientoAsync(Guid id, CancellationToken ct = default) =>
        Descargar($"{Rutas.Movimientos}/{id}/receipt", $"movimiento-de-caja-{id:N}.pdf", ct);

    // ----------------------------------------------------------------------------- cierre del día --

    public Task<ResultadoDeInventario<IReadOnlyList<CierreDelDiaDto>>> ListarCierresDelDiaAsync(Guid? punto = null, DateOnly? desde = null, DateOnly? hasta = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CierreDelDiaDto>>(HttpMethod.Get, Q(Rutas.CierresDelDia, ("pointOfSale", Texto(punto)), ("from", Texto(desde)), ("to", Texto(hasta))),
            null, null, ct);

    public Task<ResultadoDeInventario<DetalleDelCierreDelDiaDto>> ObtenerCierreDelDiaAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DetalleDelCierreDelDiaDto>(HttpMethod.Get, $"{Rutas.CierresDelDia}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<CierreDelDiaDto>> CerrarElDiaAsync(CierreDelDiaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CierreDelDiaDto>(HttpMethod.Post, Rutas.CierresDelDia, request, clave, ct);

    public Task<ResultadoDeInventario<CierreDelDiaDto>> ReabrirElDiaAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CierreDelDiaDto>(HttpMethod.Post, $"{Rutas.CierresDelDia}/{id}/reopen", new MotivoRequest(motivo), clave, ct);
}
