using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

/// <summary>
/// Anula un pagaré (feature 013, US3, T0451; api.md §6.2, <c>POST /api/lending/promissory-notes/{id}/void</c>, <c>Lending.PromissoryNotes.Manage</c>,
/// con clave y motivo; FR-082): el de un crédito, sólo con la cancelación en firme (<c>Lending.Collateral.LoanNotPaidOff</c> o <c>.NotFinal</c>,
/// la misma regla de liberar una garantía); el de una solicitud sin crédito, cuando se quiere reemplazar o la solicitud no siguió. El
/// desmaterializado se anula en el depósito por su canal sellado (canal caído, <c>Lending.Adapter.Unavailable</c>). Queda <c>Voided</c> con la
/// fecha, el motivo y, si tenía crédito, la fecha de devolución. Uno ya anulado responde <c>Lending.PromissoryNote.InvalidState</c>. (nuevo)
/// </summary>
public sealed record VoidPromissoryNoteCommand(Guid PromissoryNotePublicId, string Reason) : IRequest<Result<PromissoryNoteDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidPromissoryNoteCommandValidator : AbstractValidator<VoidPromissoryNoteCommand>
{
    public VoidPromissoryNoteCommandValidator()
    {
        RuleFor(x => x.PromissoryNotePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula el pagaré.").MaximumLength(300);
    }
}

public sealed class VoidPromissoryNoteCommandHandler(
    IApplicationDbContext db,
    DepositoDelPagare deposito,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<VoidPromissoryNoteCommand, Result<PromissoryNoteDto>>
{
    public async Task<Result<PromissoryNoteDto>> Handle(VoidPromissoryNoteCommand request, CancellationToken ct)
    {
        var leido = await DepositoDelPagare.LeerAsync(db, alcance, auditoria, request.PromissoryNotePublicId, seguir: true, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var p = leido.Value.Pagare;
        if (p.Status == PromissoryNoteStatus.Voided) return Falla(ErroresDePagare.EstadoInvalido(p.Status.ToString(), "ya está anulado."));

        var hoy = reloj.HoyLocal;
        if (p.LoanId is not null)
        {
            var (sinFirmeza, _) = await GarantiasDeCartera.FirmezaDeLaCancelacionAsync(db, p.LoanId, hoy, ct);
            if (sinFirmeza is not null) return Falla(sinFirmeza);
        }

        if (p.Kind == PromissoryNoteKind.Dematerialized && !string.IsNullOrWhiteSpace(p.CustodianChannelCode) && !string.IsNullOrWhiteSpace(p.ExternalNoteId))
        {
            var contexto = await deposito.ContextoAsync(p.CustodianChannelCode, p.PublicId, ct);
            if (contexto.IsFailure) return Falla(contexto.Error);
            var r = await deposito.Resolver(p.CustodianChannelCode).AnularPagareAsync(p.ExternalNoteId, request.Reason.Trim(), contexto.Value, ct);
            if (r.Outcome is ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess) return Falla(ErroresDePagare.CanalNoDisponible(r.ChannelCode, r.Detalle));
            if (!r.EsOk && r.Outcome != ExternalOutcome.NotFound) return Falla(ErroresDePagare.RechazadoPorElDeposito(r.ChannelCode, r.Detalle));
        }

        p.Status = PromissoryNoteStatus.Voided;
        p.VoidedAt = reloj.UtcNow;
        p.VoidReason = request.Reason.Trim();
        if (p.LoanId is not null) p.ReleasedOn = hoy;
        await db.SaveChangesAsync(ct);
        return Result.Success((await DepositoDelPagare.DtosAsync(db, null, [p], ct))[0]);
    }

    private static Result<PromissoryNoteDto> Falla(Error e) => Result.Failure<PromissoryNoteDto>(e);
}
