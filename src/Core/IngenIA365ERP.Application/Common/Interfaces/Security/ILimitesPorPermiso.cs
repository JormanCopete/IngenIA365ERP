namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// El monto máximo de un permiso para quien hace la operación (feature 012, T34, T082; contracts/api.md §1.3;
/// pregunta C4). La implementación vive en la API (<c>LimitesPorPermiso</c>): de los roles activos del usuario de
/// <see cref="IActorActual"/> que conceden el permiso, el <b>mayor</b> límite vigente a la fecha; un rol que lo
/// concede sin fila vigente (o con <c>MaxAmount</c> nulo) significa sin límite.
/// </summary>
public interface ILimitesPorPermiso
{
    /// <summary>
    /// El máximo efectivo del permiso a la fecha de operación, o nulo si no hay límite (también si ningún rol del
    /// usuario lo concede: eso lo decide la puerta de la ruta, no este lector).
    /// </summary>
    Task<decimal?> MontoMaximoAsync(string permiso, DateOnly fecha, CancellationToken ct = default);
}
