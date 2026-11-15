using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US13 (T142, T143; contracts/erp-api-de-servicio.md, «C6 — crédito en línea»): el asociado simula un crédito con las líneas que la
// cooperativa publica en el canal y envía una solicitud. La simulación es la del ERP (SimuladorDeCredito: misma cuota, misma usura). La solicitud
// queda en BORRADOR con canal de origen «Channel», a nombre del asociado (CreatedByMemberPublicId sale del actor, nunca del cuerpo), para que la
// oficina la complete y la radique: el canal no radica ni decide. La respuesta al asociado no lleva notas internas.

/// <summary>Una línea de crédito publicada en el canal.</summary>
public sealed record ChannelCreditLineDto(Guid PublicId, string Code, string Name);

/// <summary>Una solicitud del asociado vista por él: sin notas internas ni decisiones del comité.</summary>
public sealed record ChannelCreditApplicationDto(Guid PublicId, string? Number, string Status, DateTime CreatedAt, string CreditLineName, decimal Amount,
    int? Installments, string? Purpose);

/// <summary>Los errores del crédito en línea.</summary>
public static class ErroresDelCreditoEnLinea
{
    public const string LineaNoPublicada = "Channel.Credit.LineNotPublished";
    public const string NoEsElAsociado = "Channel.Credit.NotTheMember";

    public static Error NoPublicada() => new(LineaNoPublicada, "Esa línea de crédito no está disponible en el canal.");

    public static Error OtraPersona() => new(NoEsElAsociado, "La solicitud sólo la puede enviar el asociado que está en el canal.");
}

/// <summary>Las líneas publicadas: las de los conceptos de crédito del canal (sin línea = todas las activas de colocación).</summary>
internal static class LineasDelCanal
{
    public static async Task<List<Domain.Entities.Lending.Configuration.CreditLine>> PublicadasAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var publicadas = await db.ChannelPublishedConcepts.AsNoTracking().Where(c => c.Kind == ConceptKind.Loan).Select(c => c.LineCode).ToListAsync(ct);
        if (publicadas.Count == 0) return [];
        var todas = publicadas.Any(l => l == null);
        var codigos = publicadas.Where(l => l != null).Select(l => l!.ToUpperInvariant()).ToList();
        return await db.CreditLines.AsNoTracking()
            .Where(l => l.Nature == LineNature.Placement && l.Status == CreditLineStatus.Active && !l.IsDeleted && (todas || codigos.Contains(l.Code.ToUpper())))
            .OrderBy(l => l.Name).ToListAsync(ct);
    }
}

/// <summary><c>GET /tenants/{t}/credit-lines/published</c>.</summary>
public sealed record ListPublishedCreditLinesQuery : IRequest<Result<IReadOnlyList<ChannelCreditLineDto>>>;

public sealed class ListPublishedCreditLinesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListPublishedCreditLinesQuery, Result<IReadOnlyList<ChannelCreditLineDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelCreditLineDto>>> Handle(ListPublishedCreditLinesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<ChannelCreditLineDto>>((await LineasDelCanal.PublicadasAsync(db, ct)).Select(l => new ChannelCreditLineDto(l.PublicId, l.Code, l.Name)).ToList());
}

/// <summary><c>POST /tenants/{t}/people/{p}/credit-simulations</c>: la simulación del ERP para esa persona (su banda de tasas).</summary>
public sealed record SimulateChannelCreditQuery(Guid PersonPublicId, Guid CreditLinePublicId, decimal Amount, int Installments) : IRequest<Result<ScheduleSimulationDto>>;

public sealed class SimulateChannelCreditQueryValidator : AbstractValidator<SimulateChannelCreditQuery>
{
    public SimulateChannelCreditQueryValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m).Must(v => decimal.Round(v, 0) == v).WithMessage("El monto va en pesos enteros mayor que cero.");
        RuleFor(x => x.Installments).InclusiveBetween(1, 600);
    }
}

public sealed class SimulateChannelCreditQueryHandler(IApplicationDbContext db, SimuladorDeCredito simulador, IDateTimeService reloj)
    : IRequestHandler<SimulateChannelCreditQuery, Result<ScheduleSimulationDto>>
{
    public async Task<Result<ScheduleSimulationDto>> Handle(SimulateChannelCreditQuery request, CancellationToken ct)
    {
        var persona = await PersonaDelCanal.HabilitadaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ScheduleSimulationDto>(PersonaDelCanal.ErrorNoHabilitada);
        var linea = (await LineasDelCanal.PublicadasAsync(db, ct)).FirstOrDefault(l => l.PublicId == request.CreditLinePublicId);
        if (linea is null) return Result.Failure<ScheduleSimulationDto>(ErroresDelCreditoEnLinea.NoPublicada());
        var hoy = reloj.HoyLocal;
        var perfil = await PersonaDeOriginacion.PerfilAsync(db, persona, hoy, ct);
        var calculo = await simulador.SimularAsync(linea, new ScheduleSimulationInput(linea.PublicId, request.Amount, request.Installments, PersonPublicId: persona.PublicId),
            perfil, hoy, ct);
        return calculo.IsFailure ? Result.Failure<ScheduleSimulationDto>(calculo.Error) : Result.Success(SimulacionEnAlcance.Dto(calculo.Value, linea));
    }
}

/// <summary>
/// <c>POST /tenants/{t}/people/{p}/credit-applications</c>: la solicitud del asociado en borrador, con canal de origen. Las mismas reglas de la oficina
/// (<see cref="ArmadoDeSolicitud"/>: línea vigente, montos y plazos de la línea, usura, sólo asociados si la línea lo pide). Idempotente por la clave del canal.
/// </summary>
public sealed record CreateChannelCreditApplicationCommand(Guid PersonPublicId, Guid CreditLinePublicId, decimal Amount, int Installments, string? Purpose,
    decimal? MonthlyIncome, decimal? MonthlyExpenses) : IRequest<Result<ChannelCreditApplicationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateChannelCreditApplicationCommandValidator : AbstractValidator<CreateChannelCreditApplicationCommand>
{
    public CreateChannelCreditApplicationCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m).Must(v => decimal.Round(v, 0) == v).WithMessage("El monto va en pesos enteros mayor que cero.");
        RuleFor(x => x.Installments).InclusiveBetween(1, 600);
        RuleFor(x => x.Purpose).MaximumLength(300);
        RuleFor(x => x.MonthlyIncome).GreaterThanOrEqualTo(0m).When(x => x.MonthlyIncome is not null);
        RuleFor(x => x.MonthlyExpenses).GreaterThanOrEqualTo(0m).When(x => x.MonthlyExpenses is not null);
    }
}

public sealed class CreateChannelCreditApplicationCommandHandler(
    IApplicationDbContext db,
    SimuladorDeCredito simulador,
    IngenIA365ERP.Application.Common.NationalValues.IPerfilRegulatorio perfil,
    IngenIA365ERP.Application.Lending.Common.LendingAuditEmitter auditoriaDeCartera,
    IActorActual actorActual,
    AuditoriaDelCanal auditoria,
    IDateTimeService reloj) : IRequestHandler<CreateChannelCreditApplicationCommand, Result<ChannelCreditApplicationDto>>
{
    public const string EventoSolicitud = "Channel.CreditApplication.Created";

    public async Task<Result<ChannelCreditApplicationDto>> Handle(CreateChannelCreditApplicationCommand request, CancellationToken ct)
    {
        // T143: la solicitud la arma el asociado de la petición (ActorDeLaPeticion), y sólo para sí mismo.
        var actor = await actorActual.ObtenerAsync(ct);
        if (!actor.EsAsociado || actor.MemberPublicId != request.PersonPublicId) return Result.Failure<ChannelCreditApplicationDto>(ErroresDelCreditoEnLinea.OtraPersona());
        var persona = await PersonaDelCanal.HabilitadaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelCreditApplicationDto>(PersonaDelCanal.ErrorNoHabilitada);
        var linea = (await LineasDelCanal.PublicadasAsync(db, ct)).FirstOrDefault(l => l.PublicId == request.CreditLinePublicId);
        if (linea is null) return Result.Failure<ChannelCreditApplicationDto>(ErroresDelCreditoEnLinea.NoPublicada());

        // La oficina del asociado; si no tiene, la primera de la cooperativa (la oficina la cambia al revisarla).
        var oficinaId = await db.Associates.AsNoTracking().Where(a => a.PersonId == persona.Id && a.BranchId != null).Select(a => a.BranchId).FirstOrDefaultAsync(ct)
                        ?? await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == oficinaId).Select(b => b.PublicId).FirstOrDefaultAsync(ct);
        var entrada = new CreditApplicationInput
        {
            ApplicantPersonPublicId = persona.PublicId, OfficePublicId = oficina, CreditLinePublicId = linea.PublicId, Amount = request.Amount,
            Installments = request.Installments, Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(),
            PaymentForm = RepaymentChannel.Cashier,
            ApplicantCapacity = request.MonthlyIncome is null && request.MonthlyExpenses is null ? null
                : new CapacityInput(request.MonthlyIncome, request.MonthlyExpenses),
        };
        // El asociado no tiene alcance de oficina ni de línea: la línea ya se comprobó publicada y la persona es la suya.
        var armado = new ArmadoDeSolicitud(db, simulador, perfil, new AlcanceDelCanal(), auditoriaDeCartera);
        var armada = await armado.ArmarAsync(entrada, reloj.HoyLocal, ct);
        if (armada.IsFailure) return Result.Failure<ChannelCreditApplicationDto>(armada.Error);

        var s = new CreditApplication
        {
            Status = CreditApplicationStatus.Draft, CreatedByUserId = null, IntakeChannel = ApplicationIntakeChannel.Channel,
            CreatedByMemberPublicId = actor.MemberPublicId,
        };
        ArmadoDeSolicitud.Copiar(s, entrada, armada.Value);
        db.CreditApplications.Add(s);
        await db.SaveChangesAsync(ct);
        var solicitante = CreditApplicationParty.Nueva(s.Id, persona.Id, CreditPartyRole.Applicant, 0);
        solicitante.Declarar(request.MonthlyIncome, request.MonthlyExpenses, null);
        db.CreditApplicationParties.Add(solicitante);
        await auditoria.AgregarAsync(EventoSolicitud, nameof(CreditApplication), s.PublicId, null,
            new { line = linea.Code, amount = request.Amount, installments = request.Installments }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(Dto(s, linea.Name));
    }

    internal static ChannelCreditApplicationDto Dto(CreditApplication s, string linea) =>
        new(s.PublicId, s.Number, s.Status.ToString(), s.CreatedAt, linea, s.RequestedAmount, s.Installments, s.Purpose);

    /// <summary>Alcance total para el armado de la solicitud del canal (ver arriba).</summary>
    private sealed class AlcanceDelCanal : IAlcanceDeCartera
    {
        public Task<AlcanceDeCartera> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(AlcanceDeCartera.Total);
    }
}

/// <summary><c>GET /tenants/{t}/people/{p}/credit-applications</c>: las solicitudes que el asociado envió por el canal, la más reciente primero.</summary>
public sealed record ListChannelCreditApplicationsQuery(Guid PersonPublicId) : IRequest<Result<IReadOnlyList<ChannelCreditApplicationDto>>>;

public sealed class ListChannelCreditApplicationsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListChannelCreditApplicationsQuery, Result<IReadOnlyList<ChannelCreditApplicationDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelCreditApplicationDto>>> Handle(ListChannelCreditApplicationsQuery request, CancellationToken ct)
    {
        var filas = await (from s in db.CreditApplications.AsNoTracking()
                           join l in db.CreditLines.AsNoTracking() on s.CreditLineId equals l.Id
                           where s.IntakeChannel == ApplicationIntakeChannel.Channel && s.CreatedByMemberPublicId == request.PersonPublicId && !s.IsDeleted
                           orderby s.CreatedAt descending
                           select new { s, l.Name }).Take(50).ToListAsync(ct);
        return Result.Success<IReadOnlyList<ChannelCreditApplicationDto>>(filas.Select(x => CreateChannelCreditApplicationCommandHandler.Dto(x.s, x.Name)).ToList());
    }
}
