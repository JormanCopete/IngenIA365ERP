namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Las bodegas y los puntos de venta sobre los que opera quien hace la petición (feature 012, T35; data-model §21).
/// <b>Falla cerrado</b>: sin asignaciones, ninguno, salvo el alcance total que dan
/// <c>Inventory.Scope.AllWarehouses</c> / <c>Inventory.Scope.AllPointsOfSale</c>. En segundo plano el alcance es total.
///
/// <para>
/// La implementación de la petición es <c>AlcanceDeInventarioDeLaPeticion</c> (API, T089), que lee sólo por
/// <see cref="IAsignacionesDeBodega"/> e <see cref="IAsignacionesDePuntoDeVenta"/>; las consultas lo aplican con
/// <c>FiltroDeAlcance</c> (<c>Application/Inventory/Common</c>, T087) y los comandos con su <c>AsegurarAsync</c>.
/// </para>
/// </summary>
public interface IAlcanceDeInventario
{
    /// <summary>El alcance de la petición (una sola lectura por petición).</summary>
    Task<AlcanceDeInventario> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>
/// El alcance de un usuario: todas las bodegas o las de <see cref="Bodegas"/> (Ids internos de
/// <c>INV_Warehouses</c>) con la que se propone, y lo mismo con los puntos de venta (I3).
/// </summary>
public sealed record AlcanceDeInventario(
    bool TodasLasBodegas,
    IReadOnlySet<int> Bodegas,
    int? BodegaPorDefecto,
    bool TodosLosPuntos,
    IReadOnlySet<int> Puntos,
    int? PuntoPorDefecto)
{
    /// <summary>Ninguna bodega ni punto: lo que ve un usuario sin asignaciones ni alcance total.</summary>
    public static AlcanceDeInventario Vacio { get; } = new(false, new HashSet<int>(), null, false, new HashSet<int>(), null);

    /// <summary>Todas las bodegas y todos los puntos (segundo plano, o los dos permisos de alcance total).</summary>
    public static AlcanceDeInventario Total { get; } = new(true, new HashSet<int>(), null, true, new HashSet<int>(), null);

    /// <summary>
    /// El alcance de un usuario a partir de sus dos permisos de alcance total y de sus asignaciones (T089). Con alcance
    /// total de una clase se ignoran las asignaciones de esa clase. (nuevo)
    /// </summary>
    public static AlcanceDeInventario De(bool todasLasBodegas, AsignacionesDeAlcance bodegas, bool todosLosPuntos, AsignacionesDeAlcance puntos) => new(
        todasLasBodegas,
        todasLasBodegas ? new HashSet<int>() : bodegas.Ids,
        bodegas.PorDefecto,
        todosLosPuntos,
        todosLosPuntos ? new HashSet<int>() : puntos.Ids,
        puntos.PorDefecto);

    public bool IncluyeBodega(int bodegaId) => TodasLasBodegas || Bodegas.Contains(bodegaId);

    public bool IncluyePunto(int puntoId) => TodosLosPuntos || Puntos.Contains(puntoId);
}

/// <summary>
/// La implementación que siempre falla cerrado: <see cref="AlcanceDeInventario.Vacio"/>. La API registra la de la
/// petición (T089); ésta queda para anfitriones sin petición ni asignaciones y para las pruebas. (nuevo)
/// </summary>
public sealed class AlcanceDeInventarioCerrado : IAlcanceDeInventario
{
    public Task<AlcanceDeInventario> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(AlcanceDeInventario.Vacio);
}
