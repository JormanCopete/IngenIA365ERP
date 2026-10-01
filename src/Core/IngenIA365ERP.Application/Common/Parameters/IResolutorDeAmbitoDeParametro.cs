using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>La entidad de un ámbito de parámetro: su Id interno (el <c>ScopeId</c> guardado) y cómo se muestra. (nuevo)</summary>
public sealed record AmbitoDeParametro(ParameterScopeKind Kind, int Id, Guid PublicId, string Code, string Name);

/// <summary>
/// El gancho que traduce el <c>scopePublicId</c> de un ámbito (bodega, tipo de documento, punto, caja, tipo de
/// tercero) a su Id interno respetando el alcance del usuario (feature 012, T21, T071; contracts/api.md §7). Lo
/// implementa cada módulo dueño de las entidades —Inventario: US1 la bodega (T226), US3 el tipo de documento
/// (T286), US5 punto y caja—; la plataforma sólo deja <see cref="ResolutorDeAmbitoVacio"/>. (nuevo)
/// </summary>
public interface IResolutorDeAmbitoDeParametro
{
    /// <summary>
    /// La entidad del ámbito. Inexistente o fuera del alcance del usuario: el <c>*.NotFound</c> del módulo (404,
    /// el mismo mensaje en los dos casos, contracts/api.md §2.2).
    /// </summary>
    Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct);

    /// <summary>Cómo mostrar las entidades de esos Ids (las que el usuario alcanza); las demás no vuelven.</summary>
    Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct);
}

/// <summary>
/// La implementación por defecto de la plataforma (fases 1–2): no resuelve ningún ámbito, así que sólo se pueden
/// registrar vigencias generales. La reemplaza <c>ReglasDePlataformaDeInventario</c> (T226). (nuevo)
/// </summary>
public sealed class ResolutorDeAmbitoVacio : IResolutorDeAmbitoDeParametro
{
    public Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct) =>
        Task.FromResult(Result.Failure<AmbitoDeParametro>(Error.NotFound));

    public Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AmbitoDeParametro>>([]);
}
