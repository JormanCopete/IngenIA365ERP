using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

/// <summary>
/// El disponible de un cupo a una fecha (FR-058): cupo − saldos vivos − reservas vigentes − aprobadas sin desembolsar, con cada parte por
/// separado (api.md §7.1 <c>{ limit, used, reserved, approvedNotDisbursed, available }</c>). (nuevo)
/// </summary>
public sealed record DisponibleDeUnCupo(
    CreditLimit Cupo,
    decimal Limite,
    decimal Usado,
    decimal Reservado,
    decimal AprobadoSinDesembolsar)
{
    public decimal Consumido => Usado + Reservado + AprobadoSinDesembolsar;

    public decimal Disponible => Math.Max(0m, Limite - Consumido);
}

/// <summary>Los disponibles de una persona: el consolidado, los de cada línea y el rotativo (los que rigen a la fecha). (nuevo)</summary>
public sealed record DisponibilidadDeCupos(
    DisponibleDeUnCupo? Consolidado,
    IReadOnlyList<DisponibleDeUnCupo> PorLinea,
    IReadOnlyList<DisponibleDeUnCupo> Rotativos);

/// <summary>
/// El disponible de cupo (feature 013, US2 bloque 6B; T0405; FR-049, FR-058; data-model §2.10): <b>se calcula, nunca se guarda</b>. Cada cupo que
/// rige a la fecha (la última versión firme, <see cref="VersionesDeCupo"/>) menos lo que lo consume:
/// <list type="bullet">
/// <item><b>consolidado</b>: el capital vivo de los créditos de la persona como deudora (suma de las filas de capital del libro), sus reservas de
/// número vigentes y sus solicitudes aprobadas sin desembolsar;</item>
/// <item><b>por línea</b>: lo mismo, sólo de esa línea;</item>
/// <item><b>rotativo</b>: los créditos y las reservas que tomaron alguna versión de ese cupo. Con la línea nula vale para toda línea de origen
/// comercial aplicable (C54).</item>
/// </list>
/// Para la originación (<see cref="IDisponibleDeCupo"/>) aplica el de la línea si lo hay; si no, el consolidado; si no, el rotativo de la línea
/// (o el general, si la línea admite origen comercial). (nuevo)
/// </summary>
public sealed class DisponibleDeCupo(IApplicationDbContext db, VersionVigenteDeLinea versiones) : IDisponibleDeCupo
{
    private static readonly LoanStatus[] Vivos = [LoanStatus.PendingDisbursement, LoanStatus.Active];

    public async Task<DisponibleDeCupoCalculado> CalcularAsync(int personId, int creditLineId, DateOnly fecha, CancellationToken ct)
    {
        var d = await DetalleAsync(personId, fecha, ct);
        var aplicable = d.PorLinea.FirstOrDefault(c => c.Cupo.CreditLineId == creditLineId)
                        ?? d.Consolidado
                        ?? d.Rotativos.FirstOrDefault(c => c.Cupo.CreditLineId == creditLineId)
                        ?? (await AdmiteOrigenComercialAsync(creditLineId, fecha, ct) ? d.Rotativos.FirstOrDefault(c => c.Cupo.CreditLineId is null) : null);
        return aplicable is null
            ? DisponibleDeCupoCalculado.SinCupo
            : new DisponibleDeCupoCalculado(true, aplicable.Limite, aplicable.Consumido, aplicable.Disponible, aplicable.Cupo.PublicId);
    }

    /// <summary>Los disponibles de los cupos de la persona que rigen a la fecha (los que están en cero no cuentan).</summary>
    public async Task<DisponibilidadDeCupos> DetalleAsync(int personId, DateOnly fecha, CancellationToken ct)
    {
        var todas = await VersionesDeCupo.DeAsync(db, [personId], ct);
        var vigentes = todas.GroupBy(v => (v.Kind, v.CreditLineId))
            .Select(g => VersionesDeCupo.Vigente(g, fecha))
            .OfType<CreditLimit>()
            .Where(v => v.Amount > 0m)
            .ToList();
        if (vigentes.Count == 0) return new DisponibilidadDeCupos(null, [], []);

        var creditos = await db.Loans.AsNoTracking().Where(l => !l.IsDeleted && l.DebtorPersonId == personId && Vivos.Contains(l.Status))
            .Select(l => new { l.Id, l.CreditLineId, l.CreditLimitId }).ToListAsync(ct);
        var ids = creditos.Select(c => c.Id).ToArray();
        var capital = ids.Length == 0
            ? new Dictionary<int, decimal>()
            : await db.LoanTransactions.AsNoTracking()
                .Where(t => ids.Contains(t.LoanId) && t.Concept == LoanConcept.Principal && !t.InControl && !t.IsDeleted)
                .GroupBy(t => t.LoanId).Select(g => new { g.Key, Capital = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(x => x.Key, x => Math.Max(0m, x.Capital), ct);
        var reservas = await db.LoanNumberReservations.AsNoTracking()
            .Where(r => !r.IsDeleted && r.PersonId == personId && r.Status == LoanNumberReservationStatus.Reserved && r.ExpiresOn >= fecha)
            .Select(r => new { r.Amount, r.CreditLineId, r.CreditLimitId }).ToListAsync(ct);
        var aprobadas = await db.CreditApplications.AsNoTracking()
            .Where(a => !a.IsDeleted && a.ApplicantPersonId == personId && a.Status == CreditApplicationStatus.Approved && a.Kind == ApplicationKind.Loan
                        && a.ApprovalValidUntil >= fecha)
            .Select(a => new { a.CreditLineId, Valor = a.ApprovedAmount ?? a.RequestedAmount }).ToListAsync(ct);

        DisponibleDeUnCupo Medir(CreditLimit cupo)
        {
            switch (cupo.Kind)
            {
                case CreditLimitKind.Consolidated:
                    return new(cupo, cupo.Amount, creditos.Sum(c => capital.GetValueOrDefault(c.Id)), reservas.Sum(r => r.Amount), aprobadas.Sum(a => a.Valor));
                case CreditLimitKind.ByLine:
                    return new(cupo, cupo.Amount, creditos.Where(c => c.CreditLineId == cupo.CreditLineId).Sum(c => capital.GetValueOrDefault(c.Id)),
                        reservas.Where(r => r.CreditLineId == cupo.CreditLineId).Sum(r => r.Amount),
                        aprobadas.Where(a => a.CreditLineId == cupo.CreditLineId).Sum(a => a.Valor));
                default:
                    var delCupo = todas.Where(v => VersionesDeCupo.MismoCupo(v, cupo)).Select(v => v.Id).Where(i => i > 0).ToHashSet();
                    return new(cupo, cupo.Amount, creditos.Where(c => c.CreditLimitId is { } l && delCupo.Contains(l)).Sum(c => capital.GetValueOrDefault(c.Id)),
                        reservas.Where(r => r.CreditLimitId is { } l && delCupo.Contains(l)).Sum(r => r.Amount), 0m);
            }
        }

        return new DisponibilidadDeCupos(
            vigentes.Where(v => v.Kind == CreditLimitKind.Consolidated).Select(Medir).FirstOrDefault(),
            vigentes.Where(v => v.Kind == CreditLimitKind.ByLine).Select(Medir).ToList(),
            vigentes.Where(v => v.Kind == CreditLimitKind.Revolving).Select(Medir).ToList());
    }

    /// <summary>¿La versión vigente de la línea admite el origen comercial? (para el rotativo sin línea, C54).</summary>
    public async Task<bool> AdmiteOrigenComercialAsync(int creditLineId, DateOnly fecha, CancellationToken ct) =>
        (await versiones.LeerAsync(creditLineId, fecha, ct: ct))?.Version.AllowsCommercialOrigin == true;
}
