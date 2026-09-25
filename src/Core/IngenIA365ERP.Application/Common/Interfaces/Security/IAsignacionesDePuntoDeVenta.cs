using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Los puntos de venta asignados a un usuario (feature 012, T35, T088; data-model §21,
/// <c>INV_UserPointOfSaleScopes</c>, I3). (nuevo) Misma forma que <see cref="IAsignacionesDeBodega"/>; la tabla y su
/// entidad (<c>UserPointOfSaleScope</c>, T575) son de la fase 13 (US5), que registra
/// <c>AsignacionesDePuntoDeVentaEnBase</c> (T596) en lugar de <see cref="SinAsignacionesDePuntoDeVenta"/>.
/// </summary>
public interface IAsignacionesDePuntoDeVenta
{
    /// <summary>Los puntos vivos asignados al usuario (<c>SEC_Users.Id</c>) y el que se propone por defecto.</summary>
    Task<AsignacionesDeAlcance> PuntosDelUsuarioAsync(int userId, CancellationToken ct);

    /// <summary>Los puntos que existen entre los pedidos por <c>PublicId</c>. (nuevo)</summary>
    Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct);

    /// <summary>Deja al usuario con exactamente estas asignaciones (bajas lógicas; a lo sumo una por defecto). Sin guardar.</summary>
    Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct);
}

/// <summary>Sin tabla de asignaciones (antes de I3): ningún punto asignado ni por asignar. Falla cerrado. (nuevo)</summary>
public sealed class SinAsignacionesDePuntoDeVenta : IAsignacionesDePuntoDeVenta
{
    public Task<AsignacionesDeAlcance> PuntosDelUsuarioAsync(int userId, CancellationToken ct) =>
        Task.FromResult(AsignacionesDeAlcance.Ninguna);

    public Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ElementoDeAlcance>>(new Dictionary<Guid, ElementoDeAlcance>());

    public Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct) =>
        Task.FromResult(asignaciones.Count == 0 ? Result.Success() : Result.Failure(ErroresDeAlcance.PuntoInexistente()));
}

/// <summary>
/// Los errores del alcance comercial (feature 012, T35, T090; contracts/api.md §16.3, §2.17). Lo que está fuera del
/// alcance de quien pregunta es el mismo 404 que lo inexistente. (nuevo)
/// </summary>
public static class ErroresDeAlcance
{
    public const string CodigoBodegaInexistente = "Inventory.Warehouse.NotFound";
    public const string CodigoPuntoInexistente = "Inventory.PointOfSale.NotFound";
    public const string CodigoPorDefectoRepetido = "Inventory.Scope.DefaultDuplicate";

    public static Error BodegaInexistente() => new(CodigoBodegaInexistente, "La bodega no existe.");

    public static Error PuntoInexistente() => new(CodigoPuntoInexistente, "El punto de venta no existe.");

    /// <param name="clase"><c>warehouse</c> o <c>pointOfSale</c>.</param>
    public static Error PorDefectoRepetido(string clase) => new ErrorConDatos(CodigoPorDefectoRepetido,
        clase == "warehouse"
            ? "Marcaste más de una bodega por defecto. Elegí una sola."
            : "Marcaste más de un punto de venta por defecto. Elegí uno solo.",
        new { kind = clase });
}
