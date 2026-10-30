using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Setup;

// Feature 015, US1 (T0072; FR-184, FR-186, FR-198; contracts/api.md §2; data-model §1.8–§1.10): los talonarios de contingencia, las suplencias del
// supervisor y las reglas de modalidad por canal. (nuevo)

// ----------------------------------------------------------------------------------------------------------- talonarios --

/// <summary><c>GET /api/receipts/contingency-books</c>: los talonarios de los puntos al alcance. (nuevo)</summary>
public sealed record ListContingencyBooksQuery(Guid? CollectionPointPublicId = null) : IRequest<Result<IReadOnlyList<ContingencyBookDto>>>;

public sealed class ListContingencyBooksQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListContingencyBooksQuery, Result<IReadOnlyList<ContingencyBookDto>>>
{
    public async Task<Result<IReadOnlyList<ContingencyBookDto>>> Handle(ListContingencyBooksQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from t in db.ContingencyBooks.AsNoTracking()
                           join p in db.CollectionPoints.AsNoTracking() on t.CollectionPointId equals p.Id
                           where request.CollectionPointPublicId == null || p.PublicId == request.CollectionPointPublicId
                           orderby p.Code, t.FromNumber
                           select new { Talonario = t, Punto = p }).ToListAsync(ct);
        return Result.Success<IReadOnlyList<ContingencyBookDto>>(filas.Where(f => AlcanceDePuntos.VePunto(mio, f.Punto))
            .Select(f => Dto(f.Talonario, f.Punto)).ToList());
    }

    internal static ContingencyBookDto Dto(ContingencyBook t, CollectionPoint p) =>
        new(t.PublicId, p.PublicId, p.Code, t.FromNumber, t.ToNumber, t.RegisteredBy, t.Status, t.CreatedAt);
}

/// <summary>
/// <c>POST /api/receipts/contingency-books</c> (<c>Receipts.Setup.Manage</c>): un talonario prenumerado del punto; su rango no se cruza con otro vivo del
/// mismo punto (<c>Receipts.ContingencyBook.Overlaps</c>). (nuevo)
/// </summary>
public sealed record RegisterContingencyBookCommand(Guid CollectionPointPublicId, int FromNumber, int ToNumber) : IRequest<Result<ContingencyBookDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterContingencyBookCommandValidator : AbstractValidator<RegisterContingencyBookCommand>
{
    public RegisterContingencyBookCommandValidator()
    {
        RuleFor(x => x.CollectionPointPublicId).NotEmpty();
        RuleFor(x => x.FromNumber).GreaterThan(0).WithMessage("El primer número del talonario es mayor que cero.");
        RuleFor(x => x.ToNumber).GreaterThanOrEqualTo(x => x.FromNumber).WithMessage("El último número no puede ser menor que el primero.");
    }
}

public sealed class RegisterContingencyBookCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IActorActual actorActual)
    : IRequestHandler<RegisterContingencyBookCommand, Result<ContingencyBookDto>>
{
    public async Task<Result<ContingencyBookDto>> Handle(RegisterContingencyBookCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<ContingencyBookDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        var cruzado = await db.ContingencyBooks.AsNoTracking()
            .Where(t => t.CollectionPointId == punto.Id && t.Status != ContingencyBookState.Voided
                        && t.FromNumber <= request.ToNumber && request.FromNumber <= t.ToNumber)
            .FirstOrDefaultAsync(ct);
        if (cruzado is not null) return Result.Failure<ContingencyBookDto>(ErroresDeRecaudo.TalonarioSeCruza(cruzado.FromNumber, cruzado.ToNumber));
        var actor = await actorActual.ObtenerAsync(ct);
        var talonario = new ContingencyBook
        {
            CollectionPointId = punto.Id, FromNumber = request.FromNumber, ToNumber = request.ToNumber,
            RegisteredBy = actor.Name.Length > 150 ? actor.Name[..150] : actor.Name,
        };
        db.ContingencyBooks.Add(talonario);
        await db.SaveChangesAsync(ct);
        return Result.Success(ListContingencyBooksQueryHandler.Dto(talonario, punto));
    }
}

/// <summary><c>POST /api/receipts/contingency-books/{id}/void</c>: anula un talonario (perdido, dañado); queda visible. (nuevo)</summary>
public sealed record VoidContingencyBookCommand(Guid PublicId, string Reason) : IRequest<Result<ContingencyBookDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidContingencyBookCommandValidator : AbstractValidator<VoidContingencyBookCommand>
{
    public VoidContingencyBookCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula el talonario.").MaximumLength(500);
    }
}

public sealed class VoidContingencyBookCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<VoidContingencyBookCommand, Result<ContingencyBookDto>>
{
    public async Task<Result<ContingencyBookDto>> Handle(VoidContingencyBookCommand request, CancellationToken ct)
    {
        var talonario = await db.ContingencyBooks.FirstOrDefaultAsync(t => t.PublicId == request.PublicId, ct);
        if (talonario is null) return Result.Failure<ContingencyBookDto>(ErroresDeRecaudo.NoEncontrado("El talonario"));
        var punto = await db.CollectionPoints.AsNoTracking().FirstAsync(p => p.Id == talonario.CollectionPointId, ct);
        if (!AlcanceDePuntos.VePunto(await alcance.ObtenerAsync(ct), punto)) return Result.Failure<ContingencyBookDto>(ErroresDeRecaudo.NoEncontrado("El talonario"));
        talonario.Status = ContingencyBookState.Voided;
        await db.SaveChangesAsync(ct);
        return Result.Success(ListContingencyBooksQueryHandler.Dto(talonario, punto));
    }
}

// ------------------------------------------------------------------------------------------------------------ suplencias --

/// <summary><c>GET /api/receipts/supervisor-delegations</c>. (nuevo)</summary>
public sealed record ListSupervisorDelegationsQuery(Guid? CollectionPointPublicId = null, bool OnlyCurrent = false)
    : IRequest<Result<IReadOnlyList<SupervisorDelegationDto>>>;

public sealed class ListSupervisorDelegationsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListSupervisorDelegationsQuery, Result<IReadOnlyList<SupervisorDelegationDto>>>
{
    public async Task<Result<IReadOnlyList<SupervisorDelegationDto>>> Handle(ListSupervisorDelegationsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var hoy = reloj.HoyLocal;
        var filas = await (from d in db.SupervisorDelegations.AsNoTracking()
                           join p in db.CollectionPoints.AsNoTracking() on d.CollectionPointId equals p.Id
                           join s in db.Users.AsNoTracking().IgnoreQueryFilters() on d.SupervisorUserId equals s.Id
                           join u in db.Users.AsNoTracking().IgnoreQueryFilters() on d.DelegateUserId equals u.Id
                           where request.CollectionPointPublicId == null || p.PublicId == request.CollectionPointPublicId
                           where !request.OnlyCurrent || (d.ValidFrom <= hoy && (d.ValidTo == null || d.ValidTo >= hoy))
                           orderby d.ValidFrom descending
                           select new { Delegacion = d, Punto = p, SupervisorPublicId = s.PublicId, Supervisor = s.Username, DelegadoPublicId = u.PublicId, Delegado = u.Username })
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<SupervisorDelegationDto>>(filas.Where(f => AlcanceDePuntos.VePunto(mio, f.Punto))
            .Select(f => new SupervisorDelegationDto(f.Delegacion.PublicId, f.Punto.PublicId, f.Punto.Code, f.SupervisorPublicId, f.Supervisor, f.DelegadoPublicId,
                f.Delegado, f.Delegacion.ValidFrom, f.Delegacion.ValidTo, f.Delegacion.Reason)).ToList());
    }
}

/// <summary>
/// <c>POST /api/receipts/supervisor-delegations</c> (<c>Receipts.Setup.Manage</c>): el supervisor del punto delega en un suplente, con vigencia y motivo,
/// aprobar diferencias y cerrar el día (FR-184). El suplente es otra persona (<c>Receipts.Delegation.SamePerson</c>). (nuevo)
/// </summary>
public sealed record RegisterSupervisorDelegationCommand(
    Guid CollectionPointPublicId, Guid SupervisorUserPublicId, Guid DelegateUserPublicId, DateOnly ValidFrom, DateOnly? ValidTo, string Reason)
    : IRequest<Result<SupervisorDelegationDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterSupervisorDelegationCommandValidator : AbstractValidator<RegisterSupervisorDelegationCommand>
{
    public RegisterSupervisorDelegationCommandValidator()
    {
        RuleFor(x => x.CollectionPointPublicId).NotEmpty();
        RuleFor(x => x.SupervisorUserPublicId).NotEmpty().WithMessage("Indique el supervisor.");
        RuleFor(x => x.DelegateUserPublicId).NotEmpty().WithMessage("Indique el suplente.");
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x).Must(x => x.ValidTo is null || x.ValidTo >= x.ValidFrom).WithMessage("El fin de la vigencia no puede ser anterior a su inicio.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo de la suplencia.").MaximumLength(500);
    }
}

public sealed class RegisterSupervisorDelegationCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, SegregacionDeRecaudo segregacion)
    : IRequestHandler<RegisterSupervisorDelegationCommand, Result<SupervisorDelegationDto>>
{
    public async Task<Result<SupervisorDelegationDto>> Handle(RegisterSupervisorDelegationCommand request, CancellationToken ct)
    {
        var punto = await AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<SupervisorDelegationDto>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        var supervisor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.SupervisorUserPublicId, ct);
        var suplente = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.DelegateUserPublicId, ct);
        if (supervisor is null || suplente is null) return Result.Failure<SupervisorDelegationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await segregacion.MismaPersonaAsync(supervisor.Id, suplente.Id, ct)) return Result.Failure<SupervisorDelegationDto>(ErroresDeRecaudo.SuplenteMismaPersona());
        var delegacion = new SupervisorDelegation
        {
            CollectionPointId = punto.Id, SupervisorUserId = supervisor.Id, DelegateUserId = suplente.Id,
            ValidFrom = request.ValidFrom, ValidTo = request.ValidTo, Reason = request.Reason.Trim(),
        };
        db.SupervisorDelegations.Add(delegacion);
        await db.SaveChangesAsync(ct);
        return Result.Success(new SupervisorDelegationDto(delegacion.PublicId, punto.PublicId, punto.Code, supervisor.PublicId, supervisor.Username,
            suplente.PublicId, suplente.Username, delegacion.ValidFrom, delegacion.ValidTo, delegacion.Reason));
    }
}

/// <summary><c>POST /api/receipts/supervisor-delegations/{id}/end</c>: la suplencia termina hoy. (nuevo)</summary>
public sealed record EndSupervisorDelegationCommand(Guid PublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EndSupervisorDelegationCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<EndSupervisorDelegationCommand, Result>
{
    public async Task<Result> Handle(EndSupervisorDelegationCommand request, CancellationToken ct)
    {
        var delegacion = await db.SupervisorDelegations.FirstOrDefaultAsync(d => d.PublicId == request.PublicId, ct);
        if (delegacion is null) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La suplencia"));
        var punto = await db.CollectionPoints.AsNoTracking().FirstAsync(p => p.Id == delegacion.CollectionPointId, ct);
        if (!AlcanceDePuntos.VePunto(await alcance.ObtenerAsync(ct), punto)) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La suplencia"));
        var hoy = reloj.HoyLocal;
        if (delegacion.ValidFrom > hoy) delegacion.IsDeleted = true;
        else delegacion.ValidTo = hoy;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ----------------------------------------------------------------------------------------------- modalidad por canal --

/// <summary><c>GET /api/receipts/channel-modality-rules</c>: las reglas vigentes (o toda la historia). (nuevo)</summary>
public sealed record ListChannelModalityRulesQuery(bool IncludeHistory = false) : IRequest<Result<IReadOnlyList<ChannelModalityRuleDto>>>;

public sealed class ListChannelModalityRulesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListChannelModalityRulesQuery, Result<IReadOnlyList<ChannelModalityRuleDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelModalityRuleDto>>> Handle(ListChannelModalityRulesQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        return Result.Success<IReadOnlyList<ChannelModalityRuleDto>>(await db.ChannelModalityRules.AsNoTracking()
            .Where(r => request.IncludeHistory || (r.ValidFrom <= hoy && (r.ValidTo == null || r.ValidTo >= hoy)))
            .OrderBy(r => r.Channel).ThenBy(r => r.Target).ThenBy(r => r.Priority)
            .Select(r => new ChannelModalityRuleDto(r.PublicId, r.Channel, r.Target, r.LoanModality, r.AccountModality, r.OnlyWhenInstallmentsDue, r.Priority,
                r.ValidFrom, r.ValidTo))
            .ToListAsync(ct));
    }
}

/// <summary>Una regla de modalidad que se fija (data-model §1.10). (nuevo)</summary>
public sealed record ReglaDeModalidadInput(ReceiptChannel Channel, ApplicationTarget Target, int? LoanModality, int? AccountModality, bool OnlyWhenInstallmentsDue, int Priority);

/// <summary>
/// <c>PUT /api/receipts/channel-modality-rules</c> (<c>Receipts.Setup.Manage</c>): reemplaza las reglas vigentes desde hoy. Las de antes cierran su vigencia
/// ayer (las que empezaban hoy se dan de baja): lo que un recibo selló al nacer no cambia (FR-173). (nuevo)
/// </summary>
public sealed record SetChannelModalityRulesCommand(IReadOnlyList<ReglaDeModalidadInput> Rules) : IRequest<Result<IReadOnlyList<ChannelModalityRuleDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetChannelModalityRulesCommandValidator : AbstractValidator<SetChannelModalityRulesCommand>
{
    public SetChannelModalityRulesCommandValidator()
    {
        RuleFor(x => x.Rules).NotNull();
        RuleForEach(x => x.Rules).ChildRules(r =>
        {
            r.RuleFor(x => x.Channel).IsInEnum();
            r.RuleFor(x => x.Target).Must(t => t is ApplicationTarget.Loan or ApplicationTarget.Account).WithMessage("Una regla es de créditos o de cuentas.");
            r.RuleFor(x => x.LoanModality).NotNull().InclusiveBetween(1, 7).When(x => x.Target == ApplicationTarget.Loan)
                .WithMessage("Una regla de créditos lleva la modalidad de pago.");
            r.RuleFor(x => x.AccountModality).NotNull().InclusiveBetween(1, 4).When(x => x.Target == ApplicationTarget.Account)
                .WithMessage("Una regla de cuentas lleva la modalidad de consignación.");
            r.RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class SetChannelModalityRulesCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<SetChannelModalityRulesCommand, Result<IReadOnlyList<ChannelModalityRuleDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelModalityRuleDto>>> Handle(SetChannelModalityRulesCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        foreach (var vieja in await db.ChannelModalityRules.Where(r => r.ValidTo == null || r.ValidTo >= hoy).ToListAsync(ct))
        {
            if (vieja.ValidFrom >= hoy) vieja.IsDeleted = true;
            else vieja.ValidTo = hoy.AddDays(-1);
        }
        foreach (var r in request.Rules)
        {
            db.ChannelModalityRules.Add(new ChannelModalityRule
            {
                Channel = r.Channel, Target = r.Target,
                LoanModality = r.Target == ApplicationTarget.Loan ? r.LoanModality : null,
                AccountModality = r.Target == ApplicationTarget.Account ? r.AccountModality : null,
                OnlyWhenInstallmentsDue = r.Target == ApplicationTarget.Account && r.OnlyWhenInstallmentsDue,
                Priority = r.Priority, ValidFrom = hoy,
            });
        }
        await db.SaveChangesAsync(ct);
        return await new ListChannelModalityRulesQueryHandler(db, reloj).Handle(new ListChannelModalityRulesQuery(), ct);
    }
}
