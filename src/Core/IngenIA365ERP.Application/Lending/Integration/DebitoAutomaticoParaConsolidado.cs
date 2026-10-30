using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>La implementación de Cartera de <see cref="IDebitoAutomaticoParaConsolidado"/> (feature 015, R1, T0400; D26). Sólo lee tablas de Captaciones. (nuevo)</summary>
public sealed class DebitoAutomaticoParaConsolidado(IApplicationDbContext db) : IDebitoAutomaticoParaConsolidado
{
    public async Task<IReadOnlyList<DebitoAutomaticoDelDiaDto>> PorDiaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        await db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.Kind == MemberAccountOperationKind.DirectDebit && o.ReversedByOperationId == null && o.ValueDate >= desde && o.ValueDate <= hasta)
            .GroupBy(o => o.ValueDate).OrderBy(g => g.Key)
            .Select(g => new DebitoAutomaticoDelDiaDto(g.Key, g.Sum(o => o.Amount), g.Count())).ToListAsync(ct);
}
