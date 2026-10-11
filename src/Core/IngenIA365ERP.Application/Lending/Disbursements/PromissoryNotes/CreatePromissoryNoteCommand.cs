using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

/// <summary>
/// Crea el pagaré de una solicitud o de un crédito (feature 013, US3, T0451; api.md §6.2, <c>POST /api/lending/promissory-notes</c>,
/// <c>Lending.PromissoryNotes.Manage</c>, con clave; FR-044):
/// <list type="bullet">
/// <item>un solo pagaré vivo (no anulado) por dueño (<c>Lending.PromissoryNote.AlreadyExists</c>); una solicitud negada, cancelada o ya desembolsada
/// y un crédito que no está vivo no lo reciben (<c>Lending.PromissoryNote.InvalidState</c>);</item>
/// <item>el <b>físico</b> nace <c>Created</c> con su número y su custodia; se perfecciona al registrar la custodia del firmado;</item>
/// <item>el <b>desmaterializado</b> se pide al depósito del canal vigente (<c>COR_ExternalChannelSettings</c>; sin él,
/// <c>Core.ExternalChannel.NotConfigured</c>) con el deudor y los codeudores como firmantes y el <c>PublicId</c> del pagaré como clave; canal caído o
/// circuito abierto → <c>Lending.Adapter.Unavailable</c>; un rechazo del depósito → <c>Lending.PromissoryNote.DepositoryRejected</c>. Sella el canal,
/// el id externo y si es simulado, y queda <c>PendingSignature</c>: después se resuelve siempre por el canal sellado.</item>
/// </list>
/// El documento cruce ante Contabilidad sigue siendo el número del crédito, no el del pagaré. (nuevo)
/// </summary>
public sealed record CreatePromissoryNoteCommand(
    string OwnerKind,
    Guid OwnerPublicId,
    PromissoryNoteKind Form,
    string? PhysicalNumber = null,
    string? CustodyLocation = null) : IRequest<Result<PromissoryNoteDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePromissoryNoteCommandValidator : AbstractValidator<CreatePromissoryNoteCommand>
{
    public CreatePromissoryNoteCommandValidator()
    {
        RuleFor(x => x.OwnerKind).Must(DuenosDeGarantia.EsValido).WithMessage("El dueño es Application o Loan.");
        RuleFor(x => x.OwnerPublicId).NotEmpty();
        RuleFor(x => x.Form).IsInEnum();
        RuleFor(x => x.PhysicalNumber).MaximumLength(40);
        RuleFor(x => x.CustodyLocation).MaximumLength(120);
    }
}

public sealed class CreatePromissoryNoteCommandHandler(
    IApplicationDbContext db,
    DepositoDelPagare deposito,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<CreatePromissoryNoteCommand, Result<PromissoryNoteDto>>
{
    public async Task<Result<PromissoryNoteDto>> Handle(CreatePromissoryNoteCommand request, CancellationToken ct)
    {
        var leido = await GarantiasDeCartera.DuenoAsync(db, alcance, auditoria, request.OwnerKind, request.OwnerPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var dueno = leido.Value;
        if (dueno.EstadoDeLaSolicitud is CreditApplicationStatus.Denied or CreditApplicationStatus.Cancelled or CreditApplicationStatus.Disbursed
                or CreditApplicationStatus.Granted
            || dueno.EstadoDelCredito is not (null or LoanStatus.Active or LoanStatus.PendingDisbursement))
            return Falla(ErroresDePagare.EstadoInvalido(dueno.EstadoDeLaSolicitud?.ToString() ?? dueno.EstadoDelCredito?.ToString() ?? string.Empty,
                "la solicitud o el crédito ya no recibe pagaré."));

        var esCredito = dueno.Clase == DuenosDeGarantia.Credito;
        var vivo = await db.PromissoryNotes.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status != PromissoryNoteStatus.Voided
                && (esCredito ? p.LoanId == dueno.LoanId : p.ApplicationId == dueno.ApplicationId && p.LoanId == null))
            .Select(p => new { p.PublicId, p.Status }).FirstOrDefaultAsync(ct);
        if (vivo is not null) return Falla(ErroresDePagare.YaExiste(vivo.PublicId, vivo.Status.ToString()));

        var hoy = reloj.HoyLocal;
        var publicId = Guid.NewGuid();
        PromissoryNote pagare;
        if (request.Form == PromissoryNoteKind.Physical)
        {
            pagare = new PromissoryNote
            {
                PublicId = publicId,
                Kind = PromissoryNoteKind.Physical,
                ApplicationId = dueno.ApplicationId,
                LoanId = esCredito ? dueno.LoanId : null,
                IssuedOn = hoy,
                Status = PromissoryNoteStatus.Created,
                Number = string.IsNullOrWhiteSpace(request.PhysicalNumber) ? null : request.PhysicalNumber.Trim(),
                PhysicalLocation = string.IsNullOrWhiteSpace(request.CustodyLocation) ? null : request.CustodyLocation.Trim(),
            };
        }
        else
        {
            var canal = await deposito.CanalVigenteAsync(hoy, ct);
            if (canal is null) return Falla(ErroresDePagare.CanalNoConfigurado());
            var contexto = await deposito.ContextoAsync(canal, publicId, ct);
            if (contexto.IsFailure) return Falla(contexto.Error);
            var solicitud = new SolicitudDePagare(publicId, dueno.Numero ?? dueno.PublicId.ToString("N"), dueno.Monto, hoy, await FirmantesAsync(dueno, ct));
            var r = await deposito.Resolver(canal).CrearPagareAsync(solicitud, contexto.Value, ct);
            if (r.Valor is null)
                return Falla(r.Outcome is ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess
                    ? ErroresDePagare.CanalNoDisponible(r.ChannelCode, r.Detalle)
                    : ErroresDePagare.RechazadoPorElDeposito(r.ChannelCode, r.Detalle));
            if (r.Outcome is ExternalOutcome.Rejected or ExternalOutcome.InvalidData)
                return Falla(ErroresDePagare.RechazadoPorElDeposito(r.ChannelCode, r.Detalle));

            // Sella el canal, el id externo (que es también su número) y si es simulado: lo que sigue se resuelve por el canal sellado.
            pagare = new PromissoryNote
            {
                PublicId = publicId,
                Kind = PromissoryNoteKind.Dematerialized,
                ApplicationId = dueno.ApplicationId,
                LoanId = esCredito ? dueno.LoanId : null,
                IssuedOn = hoy,
                Status = PromissoryNoteStatus.PendingSignature,
                Number = r.Valor.IdExterno,
                CustodianChannelCode = r.ChannelCode,
                ExternalNoteId = r.Valor.IdExterno,
                IsSimulated = r.Simulado || CodigosDeCanalExterno.EsSimulado(r.ChannelCode),
                LastSyncedAt = reloj.UtcNow,
            };
            DepositoDelPagare.Aplicar(pagare, r.Valor, reloj.UtcNow);
        }

        db.PromissoryNotes.Add(pagare);
        await db.SaveChangesAsync(ct);
        return Result.Success((await DepositoDelPagare.DtosAsync(db, null, [pagare], ct))[0]);
    }

    /// <summary>El deudor y los codeudores del dueño, con la copia de su documento y nombre (I-1).</summary>
    private async Task<IReadOnlyList<FirmanteDePagare>> FirmantesAsync(DuenoDeGarantia dueno, CancellationToken ct)
    {
        var codeudores = dueno.Clase == DuenosDeGarantia.Credito
            ? await db.LoanParties.AsNoTracking().Where(p => p.LoanId == dueno.LoanId && p.Role == CreditPartyRole.CoDebtor).Select(p => p.PersonId).ToListAsync(ct)
            : await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == dueno.ApplicationId && p.Role == CreditPartyRole.CoDebtor)
                .OrderBy(p => p.Order).Select(p => p.PersonId).ToListAsync(ct);
        var ids = new List<int> { dueno.DebtorPersonId };
        ids.AddRange(codeudores.Where(c => c != dueno.DebtorPersonId).Distinct());
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return ids.Where(personas.ContainsKey).Select(id =>
        {
            var p = personas[id];
            return new FirmanteDePagare(p.PublicId, id == dueno.DebtorPersonId ? FirmanteDePagare.Deudor : FirmanteDePagare.Codeudor,
                LoanDisbursementLines.TipoDeDocumento(p.IdType), p.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(p), p.Email);
        }).ToList();
    }

    private static Result<PromissoryNoteDto> Falla(Error e) => Result.Failure<PromissoryNoteDto>(e);
}
