using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.DebtorEvents;

/// <summary>Un evento del deudor como lo muestran las respuestas (api.md §11.4). (nuevo)</summary>
public sealed record DebtorEventDto(
    Guid PublicId,
    PersonaRefDto Person,
    DebtorBlockReason Kind,
    DateOnly EventDate,
    string? Notes,
    UsuarioRefDto? RegisteredBy,
    DateTime RegisteredAt,
    Guid? Support,
    DateTime? LiftedAt,
    IReadOnlyList<string> Effects);

/// <summary>
/// Los eventos del deudor (feature 013, K3, T0885; FR-097): el fallecimiento, el retiro de la cooperativa y el embargo. El evento <b>se guarda como
/// bloqueo del deudor</b> (<c>LND_DebtorBlocks</c>), así que la originación, los cupos y la venta a crédito lo ven por el lector único de bloqueos. (nuevo)
/// </summary>
public static class EventosDelDeudor
{
    /// <summary>Las clases de evento que se registran por esta vía.</summary>
    public static readonly IReadOnlySet<DebtorBlockReason> Clases =
        new HashSet<DebtorBlockReason> { DebtorBlockReason.Deceased, DebtorBlockReason.AssociateWithdrawal, DebtorBlockReason.Garnishment };

    /// <summary>El disparador de un evento registrado por pantalla: su propio <c>PublicId</c> con prefijo.</summary>
    public static string Disparador(Guid publicId) => $"DebtorEvent:{publicId:N}";

    public static async Task<IReadOnlyList<DebtorEventDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<DebtorBlock> eventos, CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, eventos.Select(e => e.PersonId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, eventos.Where(e => e.CreatedByUserId is not null).Select(e => e.CreatedByUserId!.Value), ct);
        var fallecidos = eventos.Where(e => e.Reason == DebtorBlockReason.Deceased).Select(e => e.PersonId).Distinct().ToList();
        var reclamaciones = fallecidos.Count == 0
            ? []
            : await (from c in db.InsuranceClaims.AsNoTracking()
                     join l in db.Loans.AsNoTracking() on c.LoanId equals l.Id
                     where fallecidos.Contains(c.DeceasedPersonId) && !c.IsDeleted
                     select new { c.DeceasedPersonId, l.Number, c.Status }).ToListAsync(ct);
        return eventos.Select(e => new DebtorEventDto(e.PublicId, personas[e.PersonId], e.Reason, e.StartedOn, e.Notes,
            e.CreatedByUserId is { } u ? usuarios.GetValueOrDefault(u) : null, e.CreatedAt, e.SupportAttachmentPublicId, e.LiftedAt,
            Efectos(e.Reason, reclamaciones.Where(r => r.DeceasedPersonId == e.PersonId).Select(r => $"Reclamación al seguro del crédito {r.Number}: {r.Status}.")
                .ToList()))).ToList();
    }

    /// <summary>Lo que el evento hace, dicho en palabras.</summary>
    public static IReadOnlyList<string> Efectos(DebtorBlockReason clase, IReadOnlyList<string> adicionales) => clase switch
    {
        DebtorBlockReason.Deceased =>
        [
            "No se origina ni se vende a crédito a la persona.",
            "Mientras haya reclamación al seguro en trámite no hay cobranza, mensajes de cobro ni reporte negativo.",
            .. adicionales,
        ],
        DebtorBlockReason.AssociateWithdrawal =>
        [
            "Bloquea el cupo: no se origina ni se vende a crédito a la persona.",
            "Queda constancia para el proceso de retiro: la consulta de obligaciones la informa.",
        ],
        _ => ["Bloquea el cupo: no se origina ni se vende a crédito a la persona."],
    };
}

/// <summary>
/// Los eventos del deudor (feature 013, K3, T0885; api.md §11.4, <c>GET /api/lending/debtor-events?personPublicId=&amp;kind=&amp;page=</c>,
/// <c>Lending.Loans.Read</c>): los bloqueos de fallecimiento, retiro y embargo, los más recientes primero. Son de la persona, no de una oficina. (nuevo)
/// </summary>
public sealed record ListDebtorEventsQuery(Guid? PersonPublicId = null, DebtorBlockReason? Kind = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<DebtorEventDto>>>;

public sealed class ListDebtorEventsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDebtorEventsQuery, Result<PagedResult<DebtorEventDto>>>
{
    public async Task<Result<PagedResult<DebtorEventDto>>> Handle(ListDebtorEventsQuery request, CancellationToken ct)
    {
        var clases = EventosDelDeudor.Clases.ToArray();
        var consulta = db.DebtorBlocks.AsNoTracking().Where(b => !b.IsDeleted && clases.Contains(b.Reason));
        if (request.Kind is { } clase) consulta = consulta.Where(b => b.Reason == clase);
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(b => db.People.Any(p => p.Id == b.PersonId && p.PublicId == persona));
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(b => b.StartedOn).ThenByDescending(b => b.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<DebtorEventDto>(await EventosDelDeudor.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// Registra un evento del deudor (feature 013, K3, T0885; api.md §11.4, <c>POST /api/lending/debtor-events</c>, <c>Lending.DebtorEvents.Manage</c>, con
/// clave; FR-097, T102). Se guarda como bloqueo del deudor; uno vigente de la misma clase → <c>Lending.DebtorEvent.Duplicate</c>. El <b>fallecimiento</b>
/// abre una reclamación <c>InProcess</c> por cada crédito activo del deudor con seguro de vida deudores en la versión de su línea vigente a la fecha
/// (aseguradora: la indicada o la del seguro) —desde ahí no hay cobranza, mensajes de cobro ni reporte negativo— y cancela los mensajes de cobro en cola,
/// todo en la misma transacción; la causación sigue. El <b>retiro</b> y el <b>embargo</b> bloquean el cupo con su soporte; el embargo lleva motivo. (nuevo)
/// </summary>
public sealed record RegisterDebtorEventCommand(
    Guid PersonPublicId,
    DebtorBlockReason Kind,
    DateOnly EventDate,
    Guid? SupportAttachmentPublicId,
    Guid? InsurerPersonPublicId = null,
    string? Notes = null) : IRequest<Result<DebtorEventDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDebtorEventCommandValidator : AbstractValidator<RegisterDebtorEventCommand>
{
    public RegisterDebtorEventCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Kind).Must(EventosDelDeudor.Clases.Contains)
            .WithMessage("Los eventos del deudor son el fallecimiento, el retiro de la cooperativa y el embargo.");
        RuleFor(x => x.EventDate).NotEmpty();
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte del evento.");
        RuleFor(x => x.Notes).NotEmpty().When(x => x.Kind == DebtorBlockReason.Garnishment).WithMessage("Diga el motivo del embargo.");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.InsurerPersonPublicId).Null().When(x => x.Kind != DebtorBlockReason.Deceased)
            .WithMessage("La aseguradora sólo se indica en el fallecimiento.");
    }
}

public sealed class RegisterDebtorEventCommandHandler(
    IApplicationDbContext db,
    BloqueosDelDeudor bloqueos,
    ColaDeMensajesAlDeudor cola,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterDebtorEventCommand, Result<DebtorEventDto>>
{
    public async Task<Result<DebtorEventDto>> Handle(RegisterDebtorEventCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        if (request.EventDate > reloj.HoyLocal) return Falla(ErroresDeEventosDelDeudor.Invalido("eventDate", "La fecha del evento no puede ser futura."));
        int? aseguradora = null;
        if (request.InsurerPersonPublicId is { } a)
        {
            aseguradora = await db.People.AsNoTracking().Where(p => p.PublicId == a && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (aseguradora is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Id], ModoDeEspera.Esperar, ct);
            var vigente = (await bloqueos.VigentesAsync([persona.Id], ct)).FirstOrDefault(b => b.Reason == request.Kind);
            if (vigente is not null) return Falla(ErroresDeEventosDelDeudor.EventoDuplicado(vigente.Reason, vigente.StartedOn, vigente.PublicId));

            var publicId = Guid.NewGuid();
            var evento = DebtorBlock.Nuevo(persona.Id, request.Kind, EventosDelDeudor.Disparador(publicId), request.EventDate, request.Notes, usuario,
                soporte: request.SupportAttachmentPublicId);
            evento.PublicId = publicId;
            db.DebtorBlocks.Add(evento);

            var efectos = new List<string>();
            if (request.Kind == DebtorBlockReason.Deceased)
            {
                var creditos = await db.Loans.AsNoTracking()
                    .Where(l => l.DebtorPersonId == persona.Id && l.Status == LoanStatus.Active && !l.IsDeleted)
                    .Select(l => new { l.Id, l.Number, l.CreditLineId }).ToListAsync(ct);
                var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(creditos.Select(c => c.CreditLineId).Distinct().ToList(), request.EventDate,
                    ct: ct);
                var idsDeCredito = creditos.Select(x => x.Id).ToList();
                var enTramite = await db.InsuranceClaims.AsNoTracking().Where(c => idsDeCredito.Contains(c.LoanId)
                                                                                 && c.Status == InsuranceClaimStatus.InProcess && !c.IsDeleted)
                    .Select(c => c.LoanId).ToListAsync(ct);
                foreach (var c in creditos.OrderBy(x => x.Id))
                {
                    var seguro = versiones.GetValueOrDefault(c.CreditLineId)?.Seguros.FirstOrDefault(s => s.IsDebtorLifeInsurance);
                    if (seguro is null)
                    {
                        efectos.Add($"El crédito {c.Number} no tiene seguro de vida deudores: sigue su cobro.");
                        continue;
                    }
                    if (enTramite.Contains(c.Id))
                    {
                        efectos.Add($"El crédito {c.Number} ya tenía una reclamación al seguro en trámite.");
                        continue;
                    }
                    db.InsuranceClaims.Add(new InsuranceClaim
                    {
                        LoanId = c.Id,
                        InsurerPersonId = aseguradora ?? seguro.InsurerPersonId,
                        DeceasedPersonId = persona.Id,
                        DeathDate = request.EventDate,
                        DeathCertificateAttachmentPublicId = request.SupportAttachmentPublicId,
                        FiledOn = reloj.HoyLocal,
                        Status = InsuranceClaimStatus.InProcess,
                    });
                    efectos.Add($"Reclamación al seguro abierta para el crédito {c.Number}.");
                }
                var canceladas = await cola.CancelarCobrosDelDeudorAsync(persona.Id, creditos.Select(c => c.Id).ToList(), "Fallecimiento del deudor",
                    actor.Kind, usuario, ct);
                if (canceladas > 0) efectos.Add($"{canceladas} mensajes de cobro en cola cancelados.");
            }
            await db.SaveChangesAsync(ct);
            var dto = (await EventosDelDeudor.DtosAsync(db, [evento], ct))[0];
            return Result.Success(dto with { Effects = [.. EventosDelDeudor.Efectos(request.Kind, []), .. efectos] });
        }, ct);
    }

    private static Result<DebtorEventDto> Falla(Error e) => Result.Failure<DebtorEventDto>(e);
}
