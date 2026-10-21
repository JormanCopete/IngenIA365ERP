using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// El asunto de acta <c>WriteOffBatch</c> (feature 013, K3, US14, T0907; FR-095, T64): el lote de castigo que aprueba el <b>consejo de administración</b>
/// con acta (regla <c>Majority</c>: el lote no lleva solicitantes, así que el punto no pasa a la calificada de vinculados). Pendiente mientras el lote esté
/// enviado; lo decide sólo el consejo activo (en la sesión de otro órgano, <c>Lending.Session.SubjectNotForBody</c>); quien lo armó no vota su punto; la
/// huella cubre el lote y cada crédito incluido con sus montos y sus requisitos, así que un lote distinto del que se llevó a la sesión no se cierra con esa
/// acta. Al cerrar el acta, la aprobación (también con condiciones) lo deja <c>Approved</c> y cualquier otro resultado <c>Rejected</c>, con el punto; la
/// decisión con sujeto <c>WriteOffBatch</c> la escribe el cierre en <c>LND_CreditDecisions</c>. Aplicarlo es otro acto (<see cref="ApplyWriteOffBatchCommand"/>).
/// (nuevo)
/// </summary>
public sealed class AsuntoDeLoteDeCastigo(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.WriteOffBatch;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var lote = await db.WriteOffBatches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == subjectPublicId && !b.IsDeleted, ct);
        if (lote is null) return Result.Failure<AsuntoDeActa>(ErroresDeCastigo.Inexistente());
        var consejo = await CastigosDeCartera.ConsejoAsync(db, ct);
        if (consejo is null) return Result.Failure<AsuntoDeActa>(ErroresDeCastigo.SinConsejo());

        var items = await db.WriteOffBatchItems.AsNoTracking().Where(i => i.WriteOffBatchId == lote.Id && !i.IsDeleted && i.Status != WriteOffItemStatus.Rejected)
            .OrderBy(i => i.LoanId).ToListAsync(ct);
        var ids = items.Select(i => i.LoanId).ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new { l.Number, l.PublicId, l.DebtorPersonId }, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, creditos.Values.Select(c => c.DebtorPersonId).Distinct(), ct);
        var huella = new HuellaDeCartera()
            .Identificador("batch", lote.PublicId)
            .Texto("number", lote.Number)
            .Enumeracion("status", (WriteOffBatchStatus?)lote.Status)
            .Fecha("cutoffDate", lote.CutoffDate)
            .Lista("items", items, (h, i) => h
                .Identificador("loan", creditos[i.LoanId].PublicId)
                .Decimal("principal", i.PrincipalAmount)
                .Decimal("interest", i.InterestAmount)
                .Decimal("other", i.OtherAmount)
                .Decimal("impairment", i.ImpairmentAtCutoff)
                .Texto("requirements", i.RequirementsJson))
            .Calcular();

        var total = lote.TotalPrincipal + lote.TotalInterest + lote.TotalOther;
        var titulo = string.Create(CultureInfo.InvariantCulture,
            $"Castigo de cartera, lote {lote.Number}: {lote.LoanCount} crédito(s) por {ErroresDeOriginacion.Pesos(total)} (corte {lote.CutoffDate:yyyy-MM-dd})");
        var resumen = $"Motivo: {lote.Reason}. Créditos: " + string.Join("; ", items.Select(i =>
        {
            var c = creditos[i.LoanId];
            var deudor = personas.GetValueOrDefault(c.DebtorPersonId)?.FullName ?? string.Empty;
            return $"{c.Number} de {deudor}, saldo {ErroresDeOriginacion.Pesos(i.PrincipalAmount + i.InterestAmount + i.OtherAmount)}, deterioro "
                + ErroresDeOriginacion.Pesos(i.ImpairmentAtCutoff);
        })) + ". Cada crédito cumple los requisitos de la norma y de la política de castigo a la fecha de corte.";

        var personasDelAsunto = new List<PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == lote.PreparedByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct)
            is { } preparo)
            personasDelAsunto.Add(new PersonaDelAsunto(preparo, MotivosDeExclusion.Radicador));

        return Result.Success(new AsuntoDeActa(titulo, resumen, lote.Status == WriteOffBatchStatus.Submitted, lote.Status.ToString(),
            new ExpedienteDeCartera(CastigosDeCartera.Expediente, lote.PublicId), huella)
        {
            Personas = personasDelAsunto,
            GoverningBodyId = consejo,
            MontoConsolidado = total,
        });
    }

    public async Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct)
    {
        var lote = await db.WriteOffBatches.FirstOrDefaultAsync(b => b.PublicId == decision.Punto.SubjectPublicId && !b.IsDeleted, ct);
        if (lote is null) return Result.Failure(ErroresDeCastigo.Inexistente());
        if (lote.Status != WriteOffBatchStatus.Submitted) return Result.Failure(ErroresDeGobierno.SujetoNoPendiente(lote.Status.ToString()));
        try
        {
            if (decision.Resultado is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions) lote.Aprobar(decision.Punto.Id);
            else lote.Rechazar(decision.Punto.Id);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure(ErroresDeGobierno.De(ex.Problema));
        }
        return Result.Success();
    }
}
