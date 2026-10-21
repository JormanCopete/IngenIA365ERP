using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.NotApplied;

// Feature 013, K2 (US10, bloque B; T0844; api.md §13.3; data-model §24.9, §24.10; FR-112, FR-115, C32, C88): los no aplicados —de la nómina propia
// (origen Contract, nacidos desde K1), de la activación y de las pagadurías— con su seguimiento. Respetan el alcance de oficina y línea del crédito.
// (nuevo)

/// <summary>Un no aplicado con su último seguimiento (api.md §13.3). (nuevo)</summary>
public sealed record InstallmentNonDeductionDto(
    Guid PublicId,
    Guid LoanPublicId,
    string LoanNumber,
    string Debtor,
    int InstallmentNumber,
    DateOnly DueDate,
    string? Collector,
    string Reason,
    string? ReasonText,
    string Origin,
    decimal ExpectedAmount,
    decimal NotDeductedAmount,
    string? LastAction,
    DateTime? NotifiedAt,
    DateTime Since,
    bool Resolved);

/// <summary>Los no aplicados y su seguimiento, para las respuestas. (nuevo)</summary>
public static class ConsultasDeNoAplicados
{
    public static async Task<IReadOnlyList<InstallmentNonDeductionDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<InstallmentNonDeduction> filas,
        CancellationToken ct)
    {
        var ids = filas.Select(f => f.Id).ToList();
        var seguimientos = (await db.NonDeductionFollowUps.AsNoTracking().Where(s => ids.Contains(s.InstallmentNonDeductionId) && !s.IsDeleted).ToListAsync(ct))
            .ToLookup(s => s.InstallmentNonDeductionId);
        var creditos = filas.Select(f => f.LoanId).Distinct().ToList();
        var deCredito = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Number, l.DebtorPersonId), ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, deCredito.Values.Select(c => c.DebtorPersonId), ct);
        return filas.Select(f =>
        {
            var suyos = seguimientos[f.Id].OrderBy(s => s.OccurredAt).ThenBy(s => s.Id).ToList();
            var c = deCredito[f.LoanId];
            return new InstallmentNonDeductionDto(f.PublicId, c.PublicId, c.Number, personas.GetValueOrDefault(c.DebtorPersonId)?.FullName ?? string.Empty,
                f.InstallmentNumber, f.DueDate, f.CollectorCode, f.Reason.ToString(), f.ReasonText, f.Origin.ToString(), f.ExpectedAmount, f.NotDeductedAmount,
                suyos.LastOrDefault()?.Action.ToString(),
                suyos.Where(s => s.Action == NonDeductionFollowUpAction.DebtorNotified).Select(s => (DateTime?)s.OccurredAt).LastOrDefault(),
                f.CreatedAt, suyos.Any(EsResolucion));
        }).ToList();
    }

    /// <summary>¿El seguimiento resuelve el no aplicado? El de una persona, o el cierre.</summary>
    public static bool EsResolucion(NonDeductionFollowUp s) => !s.IsAutomatic || s.Action == NonDeductionFollowUpAction.Closed;
}

// ---------------------------------------------------------------------------------------------------------------------------- listar --

/// <summary>
/// Los no aplicados (api.md §13.3, <c>GET /api/lending/payroll-not-applied?agreementPublicId=&amp;origin=&amp;status=&amp;page=</c>,
/// <c>Lending.PayrollDeductionFiles.Read</c>): de créditos del alcance, también los de la nómina propia nacidos en K1 y los de la activación; el
/// convenio filtra los de pagaduría por su archivo y los demás por el vínculo del crédito; <c>status</c> es <c>Open</c> o <c>Resolved</c>. (nuevo)
/// </summary>
public sealed record ListInstallmentNonDeductionsQuery(Guid? AgreementPublicId = null, NonDeductionOrigin? Origin = null, string? Status = null,
    int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<InstallmentNonDeductionDto>>>;

public sealed class ListInstallmentNonDeductionsQueryValidator : AbstractValidator<ListInstallmentNonDeductionsQuery>
{
    public ListInstallmentNonDeductionsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "Open" or "Resolved").WithMessage("El estado es Open o Resolved.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ListInstallmentNonDeductionsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListInstallmentNonDeductionsQuery, Result<PagedResult<InstallmentNonDeductionDto>>>
{
    public async Task<Result<PagedResult<InstallmentNonDeductionDto>>> Handle(ListInstallmentNonDeductionsQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.InstallmentNonDeductions.AsNoTracking().Where(n => !n.IsDeleted && creditos.Contains(n.LoanId));
        if (request.Origin is { } origen) consulta = consulta.Where(n => n.Origin == origen);
        if (request.AgreementPublicId is { } publico)
        {
            var convenio = await db.PayrollAgreements.AsNoTracking().Where(a => a.PublicId == publico).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct) ?? 0;
            consulta = consulta.Where(n =>
                (n.PayrollDeductionResultId != null && db.PayrollDeductionResults.Any(r => r.Id == n.PayrollDeductionResultId
                    && db.PayrollDeductionFiles.Any(f => f.Id == r.PayrollDeductionFileId && f.PayrollAgreementId == convenio)))
                || (n.PayrollDeductionResultId == null && db.LoanPayrollLinks.Any(v => v.LoanId == n.LoanId && v.PayrollAgreementId == convenio && !v.IsDeleted)));
        }
        if (request.Status is { } estado)
        {
            var resueltos = db.NonDeductionFollowUps.Where(s => !s.IsDeleted && (!s.IsAutomatic || s.Action == NonDeductionFollowUpAction.Closed))
                .Select(s => s.InstallmentNonDeductionId);
            consulta = estado == "Resolved" ? consulta.Where(n => resueltos.Contains(n.Id)) : consulta.Where(n => !resueltos.Contains(n.Id));
        }
        var total = await consulta.CountAsync(ct);
        var pagina = await consulta.OrderByDescending(n => n.DueDate).ThenByDescending(n => n.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<InstallmentNonDeductionDto>(await ConsultasDeNoAplicados.DtosAsync(db, pagina, ct), request.Page, request.PageSize,
            total));
    }
}

// --------------------------------------------------------------------------------------------------------------------------- resolver --

/// <summary>
/// Resuelve un no aplicado (api.md §13.3, <c>POST /api/lending/payroll-not-applied/{id}/resolve</c>, <c>Lending.PayrollNotApplied.Manage</c>, con clave;
/// FR-115): pasar a caja, enviar a cobranza o cerrar, con motivo; avisa al deudor por sus canales autorizados y deja el seguimiento con quien
/// resolvió. La cuota sigue exigible: resolver no la paga. Uno ya resuelto: <c>Lending.InstallmentNonDeduction.AlreadyResolved</c>; fuera del
/// alcance, el 404. (nuevo)
/// </summary>
public sealed record ResolveInstallmentNonDeductionCommand(Guid NonDeductionPublicId, NonDeductionFollowUpAction Action, string Reason)
    : IRequest<Result<InstallmentNonDeductionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ResolveInstallmentNonDeductionCommandValidator : AbstractValidator<ResolveInstallmentNonDeductionCommand>
{
    public ResolveInstallmentNonDeductionCommandValidator()
    {
        RuleFor(x => x.NonDeductionPublicId).NotEmpty();
        RuleFor(x => x.Action).Must(a => a is NonDeductionFollowUpAction.MovedToCashPayment or NonDeductionFollowUpAction.SentToCollection
                or NonDeductionFollowUpAction.Closed)
            .WithMessage("Se resuelve pasando a caja (MovedToCashPayment), enviando a cobranza (SentToCollection) o cerrando (Closed).");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo.").MaximumLength(500);
    }
}

public sealed class ResolveInstallmentNonDeductionCommandHandler(
    IApplicationDbContext db,
    SeguimientoDeNoAplicados seguimiento,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ResolveInstallmentNonDeductionCommand, Result<InstallmentNonDeductionDto>>
{
    public async Task<Result<InstallmentNonDeductionDto>> Handle(ResolveInstallmentNonDeductionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var noAplicado = await db.InstallmentNonDeductions.AsNoTracking().FirstOrDefaultAsync(n => n.PublicId == request.NonDeductionPublicId && !n.IsDeleted, ct);
        if (noAplicado is null) return Falla(ErroresDeLibranza.NoAplicadoInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == noAplicado.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeLibranza.NoAplicadoInexistente(), auditoria,
            nameof(InstallmentNonDeduction), noAplicado.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            if (await seguimiento.ResueltoAsync(noAplicado.Id, ct)) return Falla(ErroresDeLibranza.NoAplicadoYaResuelto());
            var ahora = reloj.UtcNow;
            var motivo = request.Reason.Trim();
            var aviso = await seguimiento.AvisarAsync(noAplicado, credito.DebtorPersonId, request.Action, ActorKind.Person, usuario, ct);
            if (aviso is not null)
            {
                await db.SaveChangesAsync(ct);
                db.NonDeductionFollowUps.Add(new NonDeductionFollowUp
                {
                    InstallmentNonDeductionId = noAplicado.Id, Action = NonDeductionFollowUpAction.DebtorNotified, IsAutomatic = false, DebtorMessageId = aviso.Id,
                    Reason = motivo, ActorUserId = usuario, OccurredAt = ahora,
                });
            }
            db.NonDeductionFollowUps.Add(new NonDeductionFollowUp
            {
                InstallmentNonDeductionId = noAplicado.Id, Action = request.Action, IsAutomatic = false, Reason = motivo, ActorUserId = usuario, OccurredAt = ahora,
            });
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeNoAplicados.DtosAsync(db, [noAplicado], ct))[0]);
        }, ct);
    }

    private static Result<InstallmentNonDeductionDto> Falla(Error e) => Result.Failure<InstallmentNonDeductionDto>(e);
}
