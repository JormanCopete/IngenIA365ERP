using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>Puntos de venta, cajas y la venta en el POS (contracts/api.md §20.1–§20.2). (nuevo)</summary>
public sealed partial class VentasClient
{
    // ------------------------------------------------------------------------------------ puntos --

    public Task<ResultadoDeInventario<IReadOnlyList<PuntoDeVentaDto>>> ListarPuntosAsync(bool? activos = null, Guid? sucursal = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PuntoDeVentaDto>>(HttpMethod.Get, Q(Rutas.Puntos, ("branch", Texto(sucursal)), ("active", Texto(activos))), null, null, ct);

    /// <summary>El punto con sus cajas (y la sesión abierta de cada una).</summary>
    public Task<ResultadoDeInventario<PuntoDeVentaDto>> ObtenerPuntoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<PuntoDeVentaDto>(HttpMethod.Get, $"{Rutas.Puntos}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<PuntoDeVentaDto>> GuardarPuntoAsync(Guid? id, PuntoDeVentaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } existente
            ? Enviar<PuntoDeVentaDto>(HttpMethod.Put, $"{Rutas.Puntos}/{existente}", request, clave, ct)
            : Enviar<PuntoDeVentaDto>(HttpMethod.Post, Rutas.Puntos, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CajaDto>>> ListarCajasAsync(Guid punto, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CajaDto>>(HttpMethod.Get, $"{Rutas.Puntos}/{punto}/cash-registers", null, null, ct);

    public Task<ResultadoDeInventario<CajaDto>> GuardarCajaAsync(Guid punto, Guid? caja, CajaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        caja is { } existente
            ? Enviar<CajaDto>(HttpMethod.Put, $"{Rutas.Puntos}/{punto}/cash-registers/{existente}", request, clave, ct)
            : Enviar<CajaDto>(HttpMethod.Post, $"{Rutas.Puntos}/{punto}/cash-registers", request, clave, ct);

    // ----------------------------------------------------------------------------------- la venta --

    /// <summary>La búsqueda exacta del lector: 404 <c>Inventory.Product.NotFound</c> abre la búsqueda en la pantalla.</summary>
    public Task<ResultadoDeInventario<LecturaDelPosDto>> BuscarEnElPosAsync(string codigo, Guid sesion, CancellationToken ct = default) =>
        Enviar<LecturaDelPosDto>(HttpMethod.Get, Q($"{Rutas.Pos}/lookup", ("code", codigo), ("cashSession", Texto(sesion))), null, null, ct);

    /// <summary>Las ventas en borrador de la sesión o, con <paramref name="suspendidas"/>, las suspendidas del punto.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ResumenDeVentaPosDto>>> VentasDelPosAsync(Guid? sesion = null, Guid? punto = null, bool? suspendidas = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ResumenDeVentaPosDto>>(HttpMethod.Get,
            Q($"{Rutas.Pos}/drafts", ("cashSession", Texto(sesion)), ("pointOfSale", Texto(punto)), ("suspended", Texto(suspendidas))), null, null, ct);

    public Task<ResultadoDeInventario<VentaDelPosDto>> ObtenerVentaAsync(Guid id, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Get, $"{Rutas.Pos}/drafts/{id}", null, null, ct);

    public Task<ResultadoDeInventario<VentaDelPosDto>> AbrirVentaAsync(AbrirVentaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Post, $"{Rutas.Pos}/drafts", request, clave, ct);

    public Task<ResultadoDeInventario<VentaDelPosDto>> CambiarCabeceraAsync(Guid id, CabeceraDeVentaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Patch, $"{Rutas.Pos}/drafts/{id}", request, clave, ct);

    /// <summary>Una lectura: suma cantidad si el producto ya está con la misma unidad, precio y descuento.</summary>
    public Task<ResultadoDeInventario<VentaDelPosDto>> LeerAsync(Guid id, LecturaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Post, $"{Rutas.Pos}/drafts/{id}/lines", request, clave, ct);

    public Task<ResultadoDeInventario<VentaDelPosDto>> CambiarLineaAsync(Guid id, Guid linea, LineaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Patch, $"{Rutas.Pos}/drafts/{id}/lines/{linea}", request, clave, ct);

    public Task<ResultadoDeInventario<VentaDelPosDto>> QuitarLineaAsync(Guid id, Guid linea, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Delete, $"{Rutas.Pos}/drafts/{id}/lines/{linea}", null, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> SuspenderAsync(Guid id, string? rotulo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Rutas.Pos}/drafts/{id}/suspend", new RotuloRequest(rotulo), clave, ct);

    /// <summary>La liga a la sesión abierta del usuario; si cambió la fecha operativa, vuelve con el aviso <c>Inventory.Pos.Repriced</c>.</summary>
    public Task<ResultadoDeInventario<VentaDelPosDto>> RecuperarAsync(Guid id, Guid sesion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VentaDelPosDto>(HttpMethod.Post, $"{Rutas.Pos}/drafts/{id}/resume", new RecuperarVentaRequest(sesion), clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarVentaAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Rutas.Pos}/drafts/{id}/discard", new MotivoRequest(motivo), clave, ct);

    /// <summary>Cobrar: la misma clave en el reintento devuelve el mismo resultado, nunca una segunda venta (SC-002).</summary>
    public Task<ResultadoDeInventario<ResultadoDeCobroDto>> CobrarAsync(Guid id, CobroRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeCobroDto>(HttpMethod.Post, $"{Rutas.Pos}/drafts/{id}/checkout", request, clave, ct);

    // ------------------------------------------------------------------------ aprobador en persona --

    /// <summary>
    /// El desafío para que el aprobador presente decida en el equipo del cajero (§15.2). Una prueba fallida es 422
    /// <c>Approvals.Presence.Invalid</c>, nunca 401: la sesión del cajero no se toca.
    /// </summary>
    public Task<ResultadoDeInventario<DesafioDePresenciaDto>> DesafioDePresenciaAsync(Guid solicitud, string correoDelAprobador, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DesafioDePresenciaDto>(HttpMethod.Post, $"{Base}/approvals/{solicitud}/presence-challenge", new DesafioDePresenciaRequest(correoDelAprobador), clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeDecisionDto>> DecidirEnPersonaAsync(Guid solicitud, DecisionEnPersonaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeDecisionDto>(HttpMethod.Post, $"{Base}/approvals/{solicitud}/decide", request, clave, ct);
}
