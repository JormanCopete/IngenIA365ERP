using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Las bodegas asignadas a un usuario (feature 012, T35, T088; data-model §21, <c>INV_UserWarehouseScopes</c>). (nuevo)
///
/// <para>
/// La plataforma lee y escribe el alcance <b>sólo</b> por aquí, así compila y corre antes de que existan las bodegas: la
/// tabla y su entidad (<c>UserWarehouseScope</c>, T204) son de la fase 4 (US1), que registra
/// <c>AsignacionesDeBodegaEnBase</c> (T224) en lugar de <see cref="SinAsignacionesDeBodega"/>. Nada de esto guarda: el
/// comando que reemplaza las asignaciones guarda con su unidad de trabajo.
/// </para>
/// </summary>
public interface IAsignacionesDeBodega
{
    /// <summary>Las bodegas vivas asignadas al usuario (<c>SEC_Users.Id</c>) y la que se propone por defecto.</summary>
    Task<AsignacionesDeAlcance> BodegasDelUsuarioAsync(int userId, CancellationToken ct);

    /// <summary>
    /// Las bodegas que existen entre las pedidas por <c>PublicId</c>, con su Id interno, código y nombre. Las que no
    /// están no vienen: quien pregunta responde el 404. (nuevo)
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct);

    /// <summary>
    /// Deja al usuario con exactamente estas asignaciones: las retiradas quedan de baja lógica, las nuevas se agregan y
    /// a lo sumo una es la por defecto (<c>Inventory.Scope.DefaultDuplicate</c>). Sin <c>SaveChanges</c> propio.
    /// </summary>
    Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct);
}

/// <summary>Una bodega o un punto de venta: Id interno, <c>PublicId</c>, código y nombre. (nuevo)</summary>
public sealed record ElementoDeAlcance(int Id, Guid PublicId, string Code, string Name);

/// <summary>Una asignación vigente: el elemento y si es el que se propone. (nuevo)</summary>
public sealed record AsignacionDeAlcance(ElementoDeAlcance Elemento, bool IsDefault);

/// <summary>Lo que se pide asignar: el Id interno y si es el por defecto. (nuevo)</summary>
public sealed record AsignacionPedida(int Id, bool IsDefault);

/// <summary>Las asignaciones vigentes de un usuario de una clase (bodegas o puntos). (nuevo)</summary>
public sealed record AsignacionesDeAlcance(IReadOnlyList<AsignacionDeAlcance> Items)
{
    public static AsignacionesDeAlcance Ninguna { get; } = new([]);

    /// <summary>Los Ids internos asignados.</summary>
    public IReadOnlySet<int> Ids => Items.Select(i => i.Elemento.Id).ToHashSet();

    /// <summary>El Id del que se propone, si hay.</summary>
    public int? PorDefecto => Items.FirstOrDefault(i => i.IsDefault)?.Elemento.Id;
}

/// <summary>
/// Sin tabla de asignaciones (antes de la fase 4): ningún usuario tiene bodegas asignadas y ninguna bodega existe para
/// asignar. Falla cerrado: el alcance de todos es vacío salvo <c>Inventory.Scope.AllWarehouses</c>. (nuevo)
/// </summary>
public sealed class SinAsignacionesDeBodega : IAsignacionesDeBodega
{
    public Task<AsignacionesDeAlcance> BodegasDelUsuarioAsync(int userId, CancellationToken ct) =>
        Task.FromResult(AsignacionesDeAlcance.Ninguna);

    public Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ElementoDeAlcance>>(new Dictionary<Guid, ElementoDeAlcance>());

    /// <summary>Dejar sin asignaciones es posible (ya lo está); asignar algo, no: no hay bodegas.</summary>
    public Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct) =>
        Task.FromResult(asignaciones.Count == 0 ? Result.Success() : Result.Failure(ErroresDeAlcance.BodegaInexistente()));
}
