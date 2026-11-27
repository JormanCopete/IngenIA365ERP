using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// Los ámbitos de parámetro de Cartera (feature 013, T0653; T43, T56; contabilidad.md §10.1):
/// <list type="bullet">
/// <item><see cref="ParameterScopeKind.LendingFactType"/> sobre <c>LND_FactTypes</c>, para que <c>Contabilidad.ModoDePaso</c>, <c>.Granularidad</c>,
/// <c>.DisparadorDeLote</c>, <c>.HoraDeLote</c> y <c>.FaltaDeRegla</c> admitan su vigencia por tipo. El código del ámbito es el <c>Type</c> del
/// mensaje, que es lo que mira <see cref="ReglasDeParametrosDeCartera"/> para rechazar un acompañante (C01).</item>
/// <item><see cref="ParameterScopeKind.CreditLine"/> sobre <c>LND_CreditLines</c> (2026-11-26): la excepción por línea de toda clave que la
/// admite. Hasta ese día nadie lo resolvía y la pantalla de Parámetros respondía «Recurso no encontrado» a toda vigencia por línea; las
/// lecturas por línea ya existían (<c>LectorDeParametros</c> con el Id de la línea). Una línea cerrada también se resuelve: su excepción
/// sigue rigiendo los créditos que tiene.</item>
/// </list>
/// Ni los tipos de hecho ni las líneas tienen oficina: no hay alcance que mirar. (nuevo)
/// </summary>
public sealed class ResolutorDeAmbitoDeCartera(IApplicationDbContext db) : IResolutorDeAmbitoDeParametro
{
    /// <summary>Los ámbitos que Cartera resuelve.</summary>
    public static IReadOnlyCollection<ParameterScopeKind> Ambitos { get; } = [ParameterScopeKind.LendingFactType, ParameterScopeKind.CreditLine];

    public async Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct)
    {
        var encontrado = ambito switch
        {
            ParameterScopeKind.LendingFactType => await db.LendingFactTypes.AsNoTracking().Where(f => f.PublicId == publicId && !f.IsDeleted)
                .Select(f => new AmbitoDeParametro(ParameterScopeKind.LendingFactType, f.Id, f.PublicId, f.Code, f.Name)).FirstOrDefaultAsync(ct),
            ParameterScopeKind.CreditLine => await db.CreditLines.AsNoTracking().Where(l => l.PublicId == publicId && !l.IsDeleted)
                .Select(l => new AmbitoDeParametro(ParameterScopeKind.CreditLine, l.Id, l.PublicId, l.Code, l.Name)).FirstOrDefaultAsync(ct),
            _ => null,
        };
        return encontrado is null ? Result.Failure<AmbitoDeParametro>(Error.NotFound) : Result.Success(encontrado);
    }

    public async Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return ambito switch
        {
            ParameterScopeKind.LendingFactType => await db.LendingFactTypes.AsNoTracking().Where(f => ids.Contains(f.Id))
                .Select(f => new AmbitoDeParametro(ParameterScopeKind.LendingFactType, f.Id, f.PublicId, f.Code, f.Name)).ToListAsync(ct),
            ParameterScopeKind.CreditLine => await db.CreditLines.AsNoTracking().Where(l => ids.Contains(l.Id))
                .Select(l => new AmbitoDeParametro(ParameterScopeKind.CreditLine, l.Id, l.PublicId, l.Code, l.Name)).ToListAsync(ct),
            _ => [],
        };
    }
}
