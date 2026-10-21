using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// El ámbito de parámetro de Cartera por tipo de hecho (feature 013, T0653; T43, T56; contabilidad.md §10.1): resuelve
/// <see cref="ParameterScopeKind.LendingFactType"/> sobre <c>LND_FactTypes</c> para que <c>Contabilidad.ModoDePaso</c>,
/// <c>.Granularidad</c>, <c>.DisparadorDeLote</c>, <c>.HoraDeLote</c> y <c>.FaltaDeRegla</c> admitan su vigencia por tipo. Los tipos de hecho
/// son un catálogo de la cooperativa sin oficina ni línea: no hay alcance que mirar. El código del ámbito es el <c>Type</c> del mensaje, que
/// es lo que mira <see cref="ReglasDeParametrosDeCartera"/> para rechazar un acompañante (C01). (nuevo)
/// </summary>
public sealed class ResolutorDeAmbitoDeCartera(IApplicationDbContext db) : IResolutorDeAmbitoDeParametro
{
    /// <summary>Los ámbitos que Cartera resuelve.</summary>
    public static IReadOnlyCollection<ParameterScopeKind> Ambitos { get; } = [ParameterScopeKind.LendingFactType];

    public async Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct)
    {
        if (ambito != ParameterScopeKind.LendingFactType) return Result.Failure<AmbitoDeParametro>(Error.NotFound);
        var tipo = await db.LendingFactTypes.AsNoTracking().Where(f => f.PublicId == publicId && !f.IsDeleted)
            .Select(f => new { f.Id, f.PublicId, f.Code, f.Name }).FirstOrDefaultAsync(ct);
        return tipo is null
            ? Result.Failure<AmbitoDeParametro>(Error.NotFound)
            : Result.Success(new AmbitoDeParametro(ParameterScopeKind.LendingFactType, tipo.Id, tipo.PublicId, tipo.Code, tipo.Name));
    }

    public async Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ambito != ParameterScopeKind.LendingFactType || ids.Count == 0) return [];
        return await db.LendingFactTypes.AsNoTracking().Where(f => ids.Contains(f.Id))
            .Select(f => new AmbitoDeParametro(ParameterScopeKind.LendingFactType, f.Id, f.PublicId, f.Code, f.Name))
            .ToListAsync(ct);
    }
}
