using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans/{id}</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>): el crédito con sus partes, condiciones
/// vigentes, montos, marcas (de la posición vigente), vínculos, garantías, pagaré y bloqueos vigentes del deudor. Fuera del alcance, el 404 genérico
/// (<c>Lending.Loan.NotFound</c>). (nuevo)
/// </summary>
public sealed record GetLoanQuery(Guid LoanPublicId) : IRequest<Result<LoanDto>>;

public sealed class GetLoanQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetLoanQuery, Result<LoanDto>>
{
    public async Task<Result<LoanDto>> Handle(GetLoanQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<LoanDto>(leido.Error);
        var c = leido.Value;

        var codeudores = await db.LoanParties.AsNoTracking()
            .Where(p => p.LoanId == c.Id && p.Role == CreditPartyRole.CoDebtor && p.ReleasedOn == null).Select(p => p.PersonId).ToListAsync(ct);
        var (personas, lineas, oficinas) = await ListLoansQueryHandler.ReferenciasAsync(db, [c], ct, codeudores);
        var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == c.Id).OrderByDescending(t => t.VersionNumber).FirstOrDefaultAsync(ct);
        var pactadas = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == c.Id && t.VersionNumber == 1).FirstOrDefaultAsync(ct);
        var preferencia = await db.PrepaymentPreferences.AsNoTracking().Where(p => p.LoanId == c.Id).OrderByDescending(p => p.VersionNumber)
            .Select(p => p.Choice == null ? null : p.Choice.ToString()).FirstOrDefaultAsync(ct);
        var posicion = await db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == c.Id).OrderByDescending(p => p.PositionDate)
            .ThenByDescending(p => p.Version).Select(p => new { p.IsSuspended, p.UsuryCapped }).FirstOrDefaultAsync(ct);
        var desembolsado = (await ListLoansQueryHandler.DesembolsadoAsync(db, [c.Id], ct)).GetValueOrDefault(c.Id);
        var garantias = await db.Collaterals.AsNoTracking().Where(g => g.LoanId == c.Id && !g.IsDeleted)
            .Select(g => new LoanCollateralDto(g.PublicId, g.Kind.ToString(), g.Description, g.AdmissibleValue, g.Status.ToString())).ToListAsync(ct);
        var pagare = await db.PromissoryNotes.AsNoTracking().Where(p => p.LoanId == c.Id && !p.IsDeleted && p.Status != PromissoryNoteStatus.Voided)
            .Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct);
        var bloqueos = await db.DebtorBlocks.AsNoTracking().Where(b => b.PersonId == c.DebtorPersonId && b.LiftedAt == null && !b.IsDeleted)
            .Select(b => new LoanBlockDto(b.PublicId, b.Reason.ToString(), b.StartedOn, b.IsAutomatic)).ToListAsync(ct);
        var solicitud = c.ApplicationId is { } s
            ? await db.CreditApplications.AsNoTracking().Where(a => a.Id == s).Select(a => (Guid?)a.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var origen = c.OriginLoanId is { } o
            ? await db.Loans.AsNoTracking().Where(l => l.Id == o).Select(l => (Guid?)l.PublicId).FirstOrDefaultAsync(ct)
            : null;

        var terminos = condiciones is null ? null : new LoanTermsDto(condiciones.VersionNumber, condiciones.RateKind.ToString(), condiciones.AnnualRate,
            condiciones.IndexCode, condiciones.Margin, condiciones.DayCount.ToString(), condiciones.RateEquivalence.ToString(),
            $"{condiciones.RoundingUnit}/{condiciones.RoundingMode}/{condiciones.RoundingMoment}", condiciones.AllocationOrder.ToString(),
            preferencia ?? condiciones.NoteRecalculation.ToString(), condiciones.RulesVersion);
        return Result.Success(new LoanDto(
            c.PublicId, c.Number, c.Status.ToString(), c.Origin.ToString(), personas[c.DebtorPersonId], codeudores.Select(p => personas[p]).ToList(),
            oficinas[c.BranchId], lineas[c.CreditLineId], pactadas?.ModalityCode ?? string.Empty, pactadas?.SubsegmentCode, terminos,
            new LoanAmountsDto(c.ApprovedAmount, desembolsado.Monto, desembolsado.Tramos),
            new LoanMarksDto((condiciones?.RestructuringCount ?? 0) > 0, condiciones?.RestructuringCount ?? 0, c.ValidationFailureCode is not null && c.ConfirmedByExceptionAt is null,
                posicion?.IsSuspended ?? false, posicion?.UsuryCapped ?? false),
            new LoanLinksDto(solicitud, origen, c.OriginLinkKind?.ToString()),
            garantias, pagare, bloqueos, c.SourceInvoiceNumber)
        {
            ValidationFailure = c.ValidationFailureCode is { } codigo ? new LoanValidationFailureDto(codigo, c.ValidationFailureReason, c.ConfirmedByExceptionAt) : null,
        });
    }
}
