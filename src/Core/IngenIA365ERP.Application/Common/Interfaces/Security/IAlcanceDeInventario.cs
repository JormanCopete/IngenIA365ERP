namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Las bodegas y los puntos de venta sobre los que opera quien hace la petición (feature 012, T35; data-model §21).
/// <b>Falla cerrado</b>: sin asignaciones, ninguno, salvo el alcance total que dan
/// <c>Inventory.Scope.AllWarehouses</c> / <c>Inventory.Scope.AllPointsOfSale</c>. En segundo plano el alcance es total.
///
/// <para>
/// Adelanto de T087 (sección de alcance) hecho por la de aprobaciones (T083), que necesita el contrato para exigir
/// alcance al aprobador: aquí van sólo la interfaz y el record; <c>FiltroDeAlcance</c> y la implementación de la
/// petición (<c>AlcanceDeInventarioDeLaPeticion</c>, T089) son de esa sección. Mientras tanto la API registra
/// <see cref="AlcanceDeInventarioCerrado"/>.
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

    public bool IncluyeBodega(int bodegaId) => TodasLasBodegas || Bodegas.Contains(bodegaId);

    public bool IncluyePunto(int puntoId) => TodosLosPuntos || Puntos.Contains(puntoId);
}

/// <summary>
/// La implementación que falla cerrado mientras no exista la de la petición (T089): siempre
/// <see cref="AlcanceDeInventario.Vacio"/>. La registra la API con <c>TryAdd</c>. (nuevo)
/// </summary>
public sealed class AlcanceDeInventarioCerrado : IAlcanceDeInventario
{
    public Task<AlcanceDeInventario> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(AlcanceDeInventario.Vacio);
}
