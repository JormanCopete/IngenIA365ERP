using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La respuesta real de <see cref="IConsultasDeCartera.ProductosDeCaptacionAsync"/> (feature 014, T043; FR-095, E-04; contrato-de-cuentas §5): las cuentas
/// de captación y aportes de unas personas, leídas <b>en bloque</b> (personas, cuentas como titular y como parte vigente, líneas y saldos por concepto en
/// tres consultas, nunca una por cuenta) y sin el alcance de la Cartera. El saldo es la suma del libro con fecha valor hasta la fecha consultada (la
/// autoridad, R04): de captación, capital + interés por pagar + bonificación − cargo por cobrar; de aportes, las cuatro clases. El disponible es el de la
/// última posición del día o anterior; sin posición, el saldo.
///
/// <para>Lo que todavía no puede decir: el vencimiento (<c>MaturityDate</c>) nace con las condiciones del CDAT (K9) y la carga aplicada sin activar
/// (<c>MigratedNotActivated</c>) con la migración de captaciones (US9); hasta entonces van nulo y falso. El estado de cada naturaleza sale de su
/// activación (<c>LND_Activations.Nature</c>); T053 lo pasa a las fechas de <c>LND_Setup</c> (research E22).</para>
/// </summary>
public static class ProductosDeCaptacionDeCartera
{
    public static async Task<ProductosDeCaptacionDto> ConsultarAsync(IApplicationDbContext db, ConsultaDeProductosDeCaptacion consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(consulta);
        var activadas = await db.LendingActivations.AsNoTracking()
            .Where(a => a.Nature == LineNature.Deposit || a.Nature == LineNature.Contribution)
            .Select(a => a.Nature).Distinct().ToListAsync(ct);
        var captaciones = activadas.Contains(LineNature.Deposit) ? EstadoDeCartera.Active : EstadoDeCartera.NotActivated;
        var aportes = activadas.Contains(LineNature.Contribution) ? EstadoDeCartera.Active : EstadoDeCartera.NotActivated;

        var publicos = (consulta.PersonPublicIds ?? []).Distinct().ToList();
        if (publicos.Count == 0) return new ProductosDeCaptacionDto(captaciones, aportes, []);
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => publicos.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId }).ToListAsync(ct);
        var idDe = personas.ToDictionary(p => p.PublicId, p => p.Id);
        var ids = personas.Select(p => p.Id).ToList();
        var fecha = consulta.Date;

        // Las cuentas como titular y, si se pide, como parte vigente a la fecha (el titular también es parte: se toma el papel más fuerte).
        var comoTitular = await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.HolderPersonId))
            .Select(a => new { a.Id, PersonId = a.HolderPersonId, Role = MemberAccountPartyRole.Holder }).ToListAsync(ct);
        var comoParte = await db.MemberAccountParties.AsNoTracking()
            .Where(p => consulta.IncludeAsParty && ids.Contains(p.PersonId) && p.Role != MemberAccountPartyRole.Holder && p.ValidFrom <= fecha
                        && (p.ValidTo == null || p.ValidTo >= fecha))
            .Select(p => new { Id = p.MemberAccountId, p.PersonId, p.Role }).ToListAsync(ct);
        var papeles = comoTitular.Concat(comoParte)
            .GroupBy(x => (x.Id, x.PersonId))
            .Select(g => (g.Key.Id, g.Key.PersonId, Role: g.Min(x => x.Role)))
            .ToList();

        var cuentaIds = papeles.Select(p => p.Id).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => cuentaIds.Contains(a.Id))
            .Join(db.CreditLines.AsNoTracking().IgnoreQueryFilters(), a => a.CreditLineId, l => l.Id,
                (a, l) => new { a.Id, a.PublicId, a.Number, a.Nature, a.DepositClass, a.Status, a.ClosedOn, LineCode = l.Code, LineName = l.Name })
            .ToDictionaryAsync(a => a.Id, ct);
        var saldos = (await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => cuentaIds.Contains(t.MemberAccountId) && t.ValueDate <= fecha)
                .GroupBy(t => new { t.MemberAccountId, t.Concept })
                .Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(t => t.Amount) })
                .ToListAsync(ct))
            .ToLookup(s => s.MemberAccountId);
        var disponibles = (await db.MemberAccountDailyPositions.AsNoTracking()
                .Where(p => cuentaIds.Contains(p.MemberAccountId) && p.PositionDate <= fecha)
                .Select(p => new { p.MemberAccountId, p.PositionDate, p.Version, p.AvailableBalance })
                .ToListAsync(ct))
            .GroupBy(p => p.MemberAccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First().AvailableBalance);

        var resultado = new List<ProductosDePersonaDto>(publicos.Count);
        foreach (var publico in publicos)
        {
            if (!idDe.TryGetValue(publico, out var personaId))
            {
                resultado.Add(new ProductosDePersonaDto(publico, false, false, []));
                continue;
            }
            var productos = papeles.Where(p => p.PersonId == personaId && cuentas.ContainsKey(p.Id))
                .Select(p => (Papel: p.Role, Cuenta: cuentas[p.Id]))
                .Where(x => consulta.IncludeClosed || x.Cuenta.Status != MemberAccountStatus.Closed)
                .OrderBy(x => x.Cuenta.Nature).ThenBy(x => x.Cuenta.Number, StringComparer.Ordinal)
                .Select(x =>
                {
                    var porConcepto = saldos[x.Cuenta.Id].ToDictionary(s => s.Concept, s => s.Total);
                    var saldo = Saldo(x.Cuenta.Nature, porConcepto);
                    return new ProductoDeCaptacionDto(x.Cuenta.PublicId, x.Cuenta.Number, (NaturalezaDeCuenta)(int)x.Cuenta.Nature,
                        x.Cuenta.DepositClass is { } c ? (ClaseDeCaptacion)(int)c : null, x.Cuenta.LineCode, x.Cuenta.LineName, (RolEnLaCuenta)(int)x.Papel,
                        (EstadoDeCuenta)(int)x.Cuenta.Status, saldo, disponibles.TryGetValue(x.Cuenta.Id, out var d) ? d : saldo, null, x.Cuenta.ClosedOn);
                })
                .ToList();
            var vivas = productos.Any(p => p.Status != EstadoDeCuenta.Closed && p.Role is RolEnLaCuenta.Holder or RolEnLaCuenta.CoHolder);
            resultado.Add(new ProductosDePersonaDto(publico, vivas, false, productos));
        }
        return new ProductosDeCaptacionDto(captaciones, aportes, resultado);
    }

    /// <summary>El saldo del titular por familia de concepto (data-model §0.2).</summary>
    public static decimal Saldo(LineNature naturaleza, IReadOnlyDictionary<MemberAccountConcept, decimal> porConcepto)
    {
        ArgumentNullException.ThrowIfNull(porConcepto);
        decimal De(MemberAccountConcept c) => porConcepto.GetValueOrDefault(c);
        return naturaleza == LineNature.Contribution
            ? MemberAccountTransaction.ConceptosDeAportes.Sum(De)
            : De(MemberAccountConcept.Principal) + De(MemberAccountConcept.InterestPayable) + De(MemberAccountConcept.Bonus) - De(MemberAccountConcept.ChargeReceivable);
    }
}
