using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La <b>implementación real</b> de <see cref="IConsultaDeAportes"/> v2 (feature 014, US3, T084; research R20, E33; contrato-de-cuentas §4; FR-094): el
/// saldo de aportes por clase y del ahorro permanente de unas personas a una fecha D, su mínimo no reducible y sus cuotas vencidas sin pagar. Responde
/// <c>Available = true</c> <b>desde la activación de aportes</b> de la cooperativa (<c>LND_Setup.ContributionsActivationDate</c>); antes, «no disponible»
/// —lo mismo que <see cref="ConsultaDeAportesNoDisponible"/>—, y la Cartera sigue con la plantilla <c>lending.contribution-balances</c>
/// (<c>SaldoDeAportesVigente</c>). Desde que responde, esa plantilla deja de leerse para la cooperativa (fin de D19, US3-3).
///
/// <para>De dónde sale: la suma del libro de cuentas con fecha valor ≤ D (la reconstrucción de R04 con todo lo registrado), el mismo saldo que el
/// extracto de aportes (SC-010). <c>Contributions</c> = las cuatro clases de aporte; <c>PermanentSavings</c> = el capital de las cuentas de ahorro
/// permanente. Las posiciones diarias las escribe el paso de captaciones desde K8; mientras tanto la suma del libro es la fuente (research E57). Lee en
/// bloque, sin seguimiento ni alcance: es un puerto en proceso.</para> (nuevo)
/// </summary>
public sealed class ConsultaDeAportesDeCartera(IApplicationDbContext db, ReglasDeAportes reglas) : IConsultaDeAportes
{
    public async Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var activacion = (await ActivacionesDeCaptacion.LeerAsync(db, ct)).Aportes;
        if (activacion is null) return SaldosDeAportesDto.NoDisponible;

        var fecha = solicitud.Date;
        var publicos = solicitud.PersonPublicIds.Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => publicos.Contains(p.PublicId))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var ids = personas.Keys.ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.HolderPersonId) && !a.IsDeleted
                        && (a.Nature == LineNature.Contribution || (a.Nature == LineNature.Deposit && a.DepositClass == DepositClass.Permanent)))
            .Select(a => new { a.Id, a.HolderPersonId, a.Nature, a.CreditLineId, a.Status, a.OpenedOn })
            .ToListAsync(ct);
        var idsDeCuentas = cuentas.Select(c => c.Id).ToList();
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.Append(MemberAccountConcept.Principal).ToArray();
        var saldos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => idsDeCuentas.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= fecha && conceptos.Contains(t.Concept))
            .GroupBy(t => new { t.MemberAccountId, t.Concept })
            .Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var enMora = (await LectorDeCuotas.DeCuentasAsync(db, idsDeCuentas, ct)).Where(q => q.EnMoraEl(fecha))
            .GroupBy(q => q.Cuota.MemberAccountId).ToDictionary(g => g.Key, g => g.Count());

        var respuesta = new List<SaldoDeAportesDto>();
        foreach (var (persona, publico) in personas.OrderBy(p => p.Key))
        {
            var suyas = cuentas.Where(c => c.HolderPersonId == persona).ToList();
            if (suyas.Count == 0) continue;
            var deAportes = suyas.Where(c => c.Nature == LineNature.Contribution).Select(c => c.Id).ToHashSet();
            var permanentes = suyas.Where(c => c.Nature == LineNature.Deposit).Select(c => c.Id).ToHashSet();
            var aportes = saldos.Where(s => deAportes.Contains(s.MemberAccountId) && MemberAccountTransaction.ConceptosDeAportes.Contains(s.Concept)).Sum(s => s.Total);
            var permanente = saldos.Where(s => permanentes.Contains(s.MemberAccountId) && s.Concept == MemberAccountConcept.Principal).Sum(s => s.Total);

            var minimo = 0m;
            var viva = suyas.Where(c => c.Nature == LineNature.Contribution && c.Status != MemberAccountStatus.Closed).OrderByDescending(c => c.Id).FirstOrDefault();
            if (viva is not null
                && await Deposits.Lines.LectorDeLineasDeCaptacion.VigenteDeAportesAsync(db, viva.CreditLineId, fecha, null, ct) is { } version)
                minimo = await reglas.MinimoAsync(version, persona, fecha, ct);

            respuesta.Add(new SaldoDeAportesDto(publico, aportes, permanente, fecha)
            {
                Minimum = minimo,
                InstallmentsInArrears = suyas.Sum(c => enMora.GetValueOrDefault(c.Id)),
            });
        }
        return new SaldosDeAportesDto(true, respuesta);
    }
}
