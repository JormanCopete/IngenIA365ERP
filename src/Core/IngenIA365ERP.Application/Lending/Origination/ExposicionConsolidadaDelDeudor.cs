using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Un crédito que suma en la exposición: su número, el capital vivo y si la persona es codeudora. (nuevo)</summary>
public sealed record CreditoEnLaExposicion(int LoanId, string Number, int DebtorPersonId, decimal Capital, bool ComoCodeudor);

/// <summary>
/// El endeudamiento consolidado de una persona con la entidad a una fecha (feature 013, T0391; FR-036, FR-041, FR-058): el capital vivo como
/// deudor y como codeudor, las reservas de número vigentes y las solicitudes aprobadas sin desembolsar. <see cref="Total"/> es lo que suma
/// para el enrutamiento y la concentración por persona; <see cref="Directa"/> (sin lo que respalda como codeudor) es lo que suma cada miembro en
/// la exposición de un grupo conectado, para no contar dos veces un crédito con deudor y codeudor del mismo grupo (C112). (nuevo)
/// </summary>
public sealed record ExposicionDelDeudor(
    int PersonId,
    decimal ComoDeudor,
    decimal ComoCodeudor,
    decimal Reservas,
    decimal Aprobadas,
    IReadOnlyList<CreditoEnLaExposicion> Creditos)
{
    public decimal Total => ComoDeudor + ComoCodeudor + Reservas + Aprobadas;

    public decimal Directa => ComoDeudor + Reservas + Aprobadas;

    public static ExposicionDelDeudor Vacia(int personId) => new(personId, 0m, 0m, 0m, 0m, []);
}

/// <summary>
/// El lector del endeudamiento consolidado (feature 013, T0391; FR-001, FR-036, FR-041, FR-058; data-model §5.1): el capital vivo de cada
/// crédito vivo (<c>PendingDisbursement</c> o <c>Active</c>) es la suma de sus filas de capital en <c>LND_LoanTransactions</c> (sin las de
/// control), nunca un saldo guardado; como codeudor cuentan los créditos donde la persona sigue obligada (no liberada ni extinguida a la fecha);
/// las reservas <c>Reserved</c> que no han vencido y las solicitudes <c>Approved</c> vigentes (con el monto aprobado) suman como deudor. US8 le
/// suma la carga aplicada antes de activar (FR-142, T0746: <see cref="Migration.CargaVigenteDeCartera"/>, el capital al corte como deudor y como
/// codeudor; sus créditos llevan el Id de la fila de la carga en negativo). Es la pieza por persona que <c>GrupoConectadoDelDeudor</c> suma miembro a miembro. Sólo
/// lee, en una consulta por fuente. (nuevo)
/// </summary>
public sealed class ExposicionConsolidadaDelDeudor(IApplicationDbContext db)
{
    private static readonly LoanStatus[] Vivos = [LoanStatus.PendingDisbursement, LoanStatus.Active];

    /// <summary>La exposición de una persona; <paramref name="sinLaSolicitud"/> no cuenta (la que se está evaluando).</summary>
    public async Task<ExposicionDelDeudor> DeAsync(int personId, DateOnly fecha, int? sinLaSolicitud = null, CancellationToken ct = default) =>
        (await DeVariasAsync([personId], fecha, sinLaSolicitud, ct))[personId];

    /// <summary>La exposición de varias personas a la vez (una consulta por fuente).</summary>
    public async Task<IReadOnlyDictionary<int, ExposicionDelDeudor>> DeVariasAsync(IReadOnlyCollection<int> personas, DateOnly fecha,
        int? sinLaSolicitud = null, CancellationToken ct = default)
    {
        var ids = personas.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, ExposicionDelDeudor>();

        var comoDeudor = await db.Loans.AsNoTracking()
            .Where(l => !l.IsDeleted && ids.Contains(l.DebtorPersonId) && Vivos.Contains(l.Status))
            .Select(l => new { l.Id, l.Number, l.DebtorPersonId })
            .ToListAsync(ct);
        var codeudas = await (from p in db.LoanParties.AsNoTracking()
                              join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                              where !p.IsDeleted && !l.IsDeleted && ids.Contains(p.PersonId) && p.Role == CreditPartyRole.CoDebtor
                                    && (p.ReleasedOn == null || p.ReleasedOn > fecha) && (p.ExtinguishedOn == null || p.ExtinguishedOn > fecha)
                                    && Vivos.Contains(l.Status)
                              select new { p.PersonId, l.Id, l.Number, l.DebtorPersonId })
            .ToListAsync(ct);

        var creditos = comoDeudor.Select(c => c.Id).Concat(codeudas.Select(c => c.Id)).Distinct().ToArray();
        var capital = creditos.Length == 0
            ? new Dictionary<int, decimal>()
            : await db.LoanTransactions.AsNoTracking()
                .Where(t => creditos.Contains(t.LoanId) && t.Concept == LoanConcept.Principal && !t.InControl && !t.IsDeleted)
                .GroupBy(t => t.LoanId)
                .Select(g => new { g.Key, Capital = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(x => x.Key, x => x.Capital, ct);

        var reservas = await db.LoanNumberReservations.AsNoTracking()
            .Where(r => !r.IsDeleted && ids.Contains(r.PersonId) && r.Status == LoanNumberReservationStatus.Reserved && r.ExpiresOn >= fecha)
            .GroupBy(r => r.PersonId)
            .Select(g => new { g.Key, Valor = g.Sum(r => r.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Valor, ct);

        var aprobadas = await db.CreditApplications.AsNoTracking()
            .Where(a => !a.IsDeleted && ids.Contains(a.ApplicantPersonId) && a.Status == CreditApplicationStatus.Approved
                        && a.Kind == ApplicationKind.Loan && a.ApprovalValidUntil >= fecha && a.Id != (sinLaSolicitud ?? 0))
            .Select(a => new { a.ApplicantPersonId, Valor = a.ApprovedAmount ?? a.RequestedAmount })
            .ToListAsync(ct);

        // US8 (T0746, FR-142): antes de activar, la carga aplicada suma como si sus créditos hubieran nacido (LoanId negativo: la fila de la carga).
        var migrados = await new Migration.CargaVigenteDeCartera(db).ExposicionAsync(ids, ct);

        var resultado = new Dictionary<int, ExposicionDelDeudor>();
        foreach (var persona in ids)
        {
            var propios = comoDeudor.Where(c => c.DebtorPersonId == persona)
                .Select(c => new CreditoEnLaExposicion(c.Id, c.Number, c.DebtorPersonId, Math.Max(0m, capital.GetValueOrDefault(c.Id)), false));
            var respaldados = codeudas.Where(c => c.PersonId == persona && c.DebtorPersonId != persona)
                .Select(c => new CreditoEnLaExposicion(c.Id, c.Number, c.DebtorPersonId, Math.Max(0m, capital.GetValueOrDefault(c.Id)), true));
            var deLaCarga = (migrados.GetValueOrDefault(persona) ?? [])
                .Select(m => new CreditoEnLaExposicion(-m.MigrationLoanRowId, m.Number, m.DebtorPersonId, Math.Max(0m, m.Capital), m.ComoCodeudor));
            var lista = propios.Concat(respaldados).Concat(deLaCarga).OrderBy(c => c.Number, StringComparer.Ordinal).ToList();
            resultado[persona] = new ExposicionDelDeudor(persona,
                lista.Where(c => !c.ComoCodeudor).Sum(c => c.Capital),
                lista.Where(c => c.ComoCodeudor).Sum(c => c.Capital),
                reservas.GetValueOrDefault(persona),
                aprobadas.Where(a => a.ApplicantPersonId == persona).Sum(a => a.Valor),
                lista);
        }
        return resultado;
    }
}
