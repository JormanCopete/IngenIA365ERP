using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

/// <summary>
/// Endosa un pagaré (feature 013, US3, T0451; api.md §6.2, <c>POST /api/lending/promissory-notes/{id}/endorse</c>, <c>Lending.PromissoryNotes.Manage</c>,
/// con clave y motivo): sólo uno firmado o en custodia (<c>Lending.PromissoryNote.InvalidState</c>). El desmaterializado va por el adaptador de su
/// canal sellado y, en producción, uno simulado no se endosa (<c>Lending.Adapter.SimulatedInProduction</c>: no tiene efecto legal); el físico
/// sólo se anota. Queda <c>Endorsed</c> con la fecha y el nombre del endosatario. (nuevo)
/// </summary>
public sealed record EndorsePromissoryNoteCommand(Guid PromissoryNotePublicId, Guid? EndorseePersonPublicId, string Reason)
    : IRequest<Result<PromissoryNoteDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EndorsePromissoryNoteCommandValidator : AbstractValidator<EndorsePromissoryNoteCommand>
{
    public EndorsePromissoryNoteCommandValidator()
    {
        RuleFor(x => x.PromissoryNotePublicId).NotEmpty();
        RuleFor(x => x.EndorseePersonPublicId).NotNull().WithMessage("Indique a quién se endosa el pagaré.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se endosa el pagaré.").MaximumLength(300);
    }
}

public sealed class EndorsePromissoryNoteCommandHandler(
    IApplicationDbContext db,
    DepositoDelPagare deposito,
    GuardiaDeCanalesExternos guardia,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<EndorsePromissoryNoteCommand, Result<PromissoryNoteDto>>
{
    public async Task<Result<PromissoryNoteDto>> Handle(EndorsePromissoryNoteCommand request, CancellationToken ct)
    {
        var leido = await DepositoDelPagare.LeerAsync(db, alcance, auditoria, request.PromissoryNotePublicId, seguir: true, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var p = leido.Value.Pagare;
        if (p.Status is not (PromissoryNoteStatus.Signed or PromissoryNoteStatus.InCustody))
            return Falla(ErroresDePagare.EstadoInvalido(p.Status.ToString(), "sólo se endosa un pagaré firmado o en custodia."));

        var endosatario = await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.EndorseePersonPublicId && !x.IsDeleted, ct);
        if (endosatario is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var nombre = ReferenciasDeGobierno.Nombre(endosatario);

        if (p.Kind == PromissoryNoteKind.Dematerialized)
        {
            if (guardia.Evaluar(EfectoLegalDeCanal.PromissoryNotePerfection, p.CustodianChannelCode, p.IsSimulated).ComoError() is { } simulado)
                return Falla(simulado);
            var contexto = await deposito.ContextoAsync(p.CustodianChannelCode!, p.PublicId, ct);
            if (contexto.IsFailure) return Falla(contexto.Error);
            var r = await deposito.Resolver(p.CustodianChannelCode!).EndosarPagareAsync(p.ExternalNoteId!, nombre, contexto.Value, ct);
            if (r.Outcome is ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess) return Falla(ErroresDePagare.CanalNoDisponible(r.ChannelCode, r.Detalle));
            if (!r.EsOk) return Falla(ErroresDePagare.RechazadoPorElDeposito(r.ChannelCode, r.Detalle));
        }

        p.Status = PromissoryNoteStatus.Endorsed;
        p.EndorsedAt = reloj.UtcNow;
        p.EndorseeName = nombre.Length > 200 ? nombre[..200] : nombre;
        await db.SaveChangesAsync(ct);
        return Result.Success((await DepositoDelPagare.DtosAsync(db, guardia, [p], ct))[0]);
    }

    private static Result<PromissoryNoteDto> Falla(Error e) => Result.Failure<PromissoryNoteDto>(e);
}
