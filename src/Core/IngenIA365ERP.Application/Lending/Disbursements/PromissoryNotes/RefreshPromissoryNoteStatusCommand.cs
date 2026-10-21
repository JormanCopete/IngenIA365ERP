using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

/// <summary>
/// Actualiza el estado de un pagaré desmaterializado con lo que informa el depósito (feature 013, US3, T0451; api.md §6.2,
/// <c>POST /api/lending/promissory-notes/{id}/refresh</c>, <c>Lending.PromissoryNotes.Manage</c>; adaptadores §3.8): consulta por el canal
/// <b>sellado</b> (aunque la configuración haya cambiado), lleva la firma y la custodia sin retroceder y anota la sincronización. Una firma
/// rechazada (el dígito 1 del simulado) deja el pagaré sin perfeccionar y lo dice en <c>signatureRejected</c>; canal caído,
/// <c>Lending.Adapter.Unavailable</c>; un físico no se consulta (<c>Lending.PromissoryNote.InvalidState</c>). Consultar es idempotente: no lleva
/// clave. La tarea <c>cartera.pagares</c> hace lo mismo sola. (nuevo)
/// </summary>
public sealed record RefreshPromissoryNoteStatusCommand(Guid PromissoryNotePublicId) : IRequest<Result<PromissoryNoteDto>>;

public sealed class RefreshPromissoryNoteStatusCommandValidator : AbstractValidator<RefreshPromissoryNoteStatusCommand>
{
    public RefreshPromissoryNoteStatusCommandValidator() => RuleFor(x => x.PromissoryNotePublicId).NotEmpty();
}

public sealed class RefreshPromissoryNoteStatusCommandHandler(
    IApplicationDbContext db,
    DepositoDelPagare deposito,
    GuardiaDeCanalesExternos guardia,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RefreshPromissoryNoteStatusCommand, Result<PromissoryNoteDto>>
{
    public async Task<Result<PromissoryNoteDto>> Handle(RefreshPromissoryNoteStatusCommand request, CancellationToken ct)
    {
        var leido = await DepositoDelPagare.LeerAsync(db, alcance, auditoria, request.PromissoryNotePublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<PromissoryNoteDto>(leido.Error);
        var p = leido.Value.Pagare;

        var r = await deposito.SincronizarAsync(p, reloj.UtcNow, ct);
        if (r.IsFailure) return Result.Failure<PromissoryNoteDto>(r.Error);
        await db.SaveChangesAsync(ct);
        var rechazada = r.Value.Outcome == ExternalOutcome.Rejected && r.Value.Valor is { FirmaRechazada: true };
        return Result.Success((await DepositoDelPagare.DtosAsync(db, guardia, [p], ct, rechazada))[0]);
    }
}
