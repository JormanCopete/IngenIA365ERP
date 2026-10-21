using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

/// <summary>
/// Registra la custodia del pagaré físico firmado (feature 013, US3, T0451; api.md §6.2, <c>POST /api/lending/promissory-notes/{id}/physical-custody</c>,
/// <c>Lending.PromissoryNotes.Manage</c>, con clave; adaptadores §3.8): sólo un físico creado o firmado (<c>Lending.PromissoryNote.InvalidState</c>)
/// y con el pagaré firmado ya adjunto como documento de la garantía (<c>LendingGuaranteeDocument</c> del pagaré, disponible;
/// <c>Lending.PromissoryNote.DocumentMissing</c>). Queda <c>InCustody</c> —perfeccionado para desembolsar— con la fecha de firma, el lugar de
/// custodia y el adjunto como copia. (nuevo)
/// </summary>
public sealed record RegisterPhysicalPromissoryNoteCustodyCommand(Guid PromissoryNotePublicId, DateTime SignedAt, string CustodyLocation, Guid AttachmentPublicId)
    : IRequest<Result<PromissoryNoteDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterPhysicalPromissoryNoteCustodyCommandValidator : AbstractValidator<RegisterPhysicalPromissoryNoteCustodyCommand>
{
    public RegisterPhysicalPromissoryNoteCustodyCommandValidator()
    {
        RuleFor(x => x.PromissoryNotePublicId).NotEmpty();
        RuleFor(x => x.SignedAt).NotEmpty().WithMessage("Indique cuándo se firmó el pagaré.");
        RuleFor(x => x.CustodyLocation).NotEmpty().WithMessage("Indique dónde queda custodiado el pagaré.").MaximumLength(120);
        RuleFor(x => x.AttachmentPublicId).NotEmpty().WithMessage("Adjunte el pagaré firmado.");
    }
}

public sealed class RegisterPhysicalPromissoryNoteCustodyCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterPhysicalPromissoryNoteCustodyCommand, Result<PromissoryNoteDto>>
{
    public async Task<Result<PromissoryNoteDto>> Handle(RegisterPhysicalPromissoryNoteCustodyCommand request, CancellationToken ct)
    {
        var leido = await DepositoDelPagare.LeerAsync(db, alcance, auditoria, request.PromissoryNotePublicId, seguir: true, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var p = leido.Value.Pagare;
        if (p.Kind != PromissoryNoteKind.Physical || p.Status is not (PromissoryNoteStatus.Created or PromissoryNoteStatus.Signed))
            return Falla(ErroresDePagare.EstadoInvalido(p.Status.ToString(), p.Kind == PromissoryNoteKind.Physical
                ? "sólo se registra la custodia de un pagaré físico creado o firmado."
                : "la custodia la informa el depósito: actualice el estado del pagaré desmaterializado."));

        var documento = await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.AttachmentPublicId
            && a.OwnerEntityType == AdjuntosDeModulo.DocumentoDeGarantia && a.OwnerEntityPublicId == p.PublicId && a.Status == EstadoDeAdjunto.Available, ct);
        if (!documento) return Falla(ErroresDePagare.SinDocumento());

        p.Status = PromissoryNoteStatus.InCustody;
        p.SignedAt = request.SignedAt;
        p.InCustodyAt = reloj.UtcNow;
        p.PhysicalLocation = request.CustodyLocation.Trim();
        p.DocumentAttachmentPublicId = request.AttachmentPublicId;
        await db.SaveChangesAsync(ct);
        return Result.Success((await DepositoDelPagare.DtosAsync(db, null, [p], ct))[0]);
    }

    private static Result<PromissoryNoteDto> Falla(Error e) => Result.Failure<PromissoryNoteDto>(e);
}
