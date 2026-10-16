using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Actions;

// Feature 013, K4 (US12, T0945; api.md §14.6; data-model §26.7–§26.8; adaptadores-y-archivos.md §4.8; FR-119, FR-122): las gestiones de
// cobranza —inmutables; una corrección es otra gestión que anula—, sus promesas con estado derivado y la ventana legal vigente sellada en cada una.
// (nuevo)

/// <summary>La promesa de una gestión (api.md §14.6 <c>promise { date, amount }</c>). (nuevo)</summary>
public sealed record PromiseInput(DateOnly Date, decimal Amount);

/// <summary>Una promesa como la muestran las listas (api.md §14.6): estado derivado, con lo abonado que cuenta y el día en que se cumplió. (nuevo)</summary>
public sealed record PaymentPromiseDto(
    Guid PublicId,
    CreditoRefDto Loan,
    PersonaRefDto Debtor,
    DateOnly Date,
    decimal Amount,
    decimal Tolerance,
    PaymentPromiseStatus? Status,
    bool Voided,
    decimal Paid,
    DateOnly? KeptOn,
    Guid ActionPublicId,
    UsuarioRefDto? Collector);

/// <summary>
/// Una gestión de cobranza (api.md §14.6). <see cref="WindowDecision"/>: lo que decía la ventana legal al registrarla. <see cref="Collector"/> es nulo en una
/// gestión de sistema (canal <c>System</c>, K4 bloque D). (nuevo)
/// </summary>
public sealed record CollectionActionDto(
    Guid PublicId,
    CreditoRefDto? Loan,
    DateTimeOffset At,
    CollectionActionChannel Channel,
    PersonaRefDto ContactedPerson,
    ContactRole ContactRole,
    bool IsDirectContact,
    CollectionActionResult Result,
    string? Notes,
    UsuarioRefDto? Collector,
    PaymentPromiseDto? Promise,
    Guid? VoidsActionPublicId,
    string? VoidReason,
    Guid? VoidedByPublicId,
    string? WindowDecision,
    IReadOnlyList<string> WindowReasons);

/// <summary>Lo común de gestiones y promesas: las proyecciones. (nuevo)</summary>
public static class GestionesDeCobranza
{
    public static async Task<IReadOnlyList<CollectionActionDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<CollectionAction> filas, DateOnly hoy,
        TimeSpan desfase, CancellationToken ct)
    {
        var ids = filas.Select(f => f.Id).ToList();
        var loanIds = filas.Where(f => f.LoanId != null).Select(f => f.LoanId!.Value).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(f => f.PersonId).Concat(creditos.Values.Select(c => c.DebtorPersonId)), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, filas.Select(f => f.CollectorUserId), ct);
        var promesas = await db.PaymentPromises.AsNoTracking().Where(p => ids.Contains(p.CollectionActionId)).ToDictionaryAsync(p => p.CollectionActionId, ct);
        var anuladas = await db.CollectionActions.AsNoTracking().Where(a => a.VoidsActionId != null && ids.Contains(a.VoidsActionId.Value))
            .ToDictionaryAsync(a => a.VoidsActionId!.Value, a => a.PublicId, ct);
        var anuladasPor = filas.Where(f => f.VoidsActionId != null).Select(f => f.VoidsActionId!.Value).ToList();
        var anulan = await db.CollectionActions.AsNoTracking().Where(a => anuladasPor.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.PublicId, ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, promesas.Values.Select(p => p.LoanId).Distinct().ToList(), ct);

        return filas.Select(f =>
        {
            PaymentPromiseDto? promesa = null;
            if (promesas.TryGetValue(f.Id, out var p) && creditos.TryGetValue(p.LoanId, out var cp))
                promesa = Promesa(p, cp, f, anuladas.ContainsKey(f.Id), pagos.GetValueOrDefault(p.LoanId) ?? [], hoy, personas, usuarios);
            return new CollectionActionDto(f.PublicId,
                f.LoanId is { } l && creditos.TryGetValue(l, out var c) ? new CreditoRefDto(c.PublicId, c.Number) : null,
                new DateTimeOffset(CobranzaDeCartera.ALocal(f.OccurredAt, desfase), desfase), f.Channel, personas[f.PersonId], f.ContactRole, f.IsDirectContact,
                f.Result, f.Notes, ReferenciasDeGobierno.Usuario(usuarios, f.CollectorUserId), promesa,
                f.VoidsActionId is { } v && anulan.TryGetValue(v, out var anula) ? anula : null, f.VoidReason,
                anuladas.TryGetValue(f.Id, out var por) ? por : null, f.WindowDecision,
                string.IsNullOrWhiteSpace(f.WindowReasonCodes) ? [] : f.WindowReasonCodes.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }).ToList();
    }

    public static PaymentPromiseDto Promesa(PaymentPromise p, Loan credito, CollectionAction gestion, bool anulada, IReadOnlyList<PagoParaCobranza> pagos, DateOnly hoy,
        IReadOnlyDictionary<int, PersonaRefDto> personas, IReadOnlyDictionary<int, UsuarioRefDto> usuarios)
    {
        var estado = EstadoDePromesa.Calcular(p.ParaEstado(gestion.OccurredAt, anulada), pagos, hoy);
        return new PaymentPromiseDto(p.PublicId, new CreditoRefDto(credito.PublicId, credito.Number), personas[credito.DebtorPersonId], p.PromisedDate,
            p.PromisedAmount, p.ToleranceAmount, estado.Estado, estado.Anulada, estado.Abonado, estado.CumplidaEl, gestion.PublicId,
            ReferenciasDeGobierno.Usuario(usuarios, gestion.CollectorUserId));
    }
}

// ---------------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// Registra una gestión de cobranza (feature 013, K4, T0945; api.md §14.6, <c>POST /api/lending/collection-actions</c>,
/// <c>Lending.Collections.RecordActivity</c>, con clave; FR-119, FR-122). La persona contactada es el deudor o un codeudor obligado del crédito, o la
/// pagaduría del crédito para la gestión ante ella (FR-114); nunca una referencia (<c>Lending.CollectionAction.ContactNotObligated</c>). La ventana legal
/// juzga la gestión en el instante en que ocurrió (Ley 2300): un contacto fuera de horario, un día no hábil, sobre el tope del día o por otro canal
/// que el de la semana se rechaza con los códigos de los mensajes (<c>Lending.DebtorMessage.OutsideLegalWindow</c>, <c>.WeeklyChannelUsed</c>,
/// <c>.ChannelNotAuthorized</c>) y la decisión vigente queda sellada en la gestión. La gestión ante la pagaduría no pasa por la ventana (no es el
/// titular). Un contacto directo fija el canal de la semana (lo cuenta el historial desde ese momento). Con <see cref="Promise"/> nace la promesa con
/// la tolerancia vigente (<c>Cobranza.ToleranciaDePromesa</c>) sellada. Quien la registra es el gestor. Un instante futuro no se registra. (nuevo)
/// </summary>
public sealed record RecordCollectionActionCommand(
    Guid LoanPublicId,
    DateTimeOffset At,
    CollectionActionChannel Channel,
    Guid ContactedPersonPublicId,
    bool IsDirectContact,
    CollectionActionResult ResultCode,
    string? Notes = null,
    PromiseInput? Promise = null) : IRequest<Result<CollectionActionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordCollectionActionCommandValidator : AbstractValidator<RecordCollectionActionCommand>
{
    public RecordCollectionActionCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.ContactedPersonPublicId).NotEmpty().WithMessage("Indique con quién fue la gestión.");
        RuleFor(x => x.Channel).IsInEnum().NotEqual(CollectionActionChannel.System)
            .WithMessage("Elija el canal por el que fue la gestión: el canal «sistema» sólo lo usa el proceso diario.");
        RuleFor(x => x.ResultCode).IsInEnum().NotEqual(CollectionActionResult.LeftCollection)
            .WithMessage("Elija el resultado de la gestión: «salió de cobranza» sólo lo registra el proceso diario.");
        RuleFor(x => x.Notes).MaximumLength(CollectionAction.LargoDeLasNotas);
        RuleFor(x => x.Promise!.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).When(x => x.Promise is not null)
            .WithMessage("Lo prometido es mayor que cero, con hasta dos decimales.");
        RuleFor(x => x.ResultCode).Equal(CollectionActionResult.PromiseToPay).When(x => x.Promise is not null)
            .WithMessage("Una promesa de pago va en una gestión con resultado «promesa de pago».");
        RuleFor(x => x.Promise!.Date).Must((x, fecha) => fecha >= DateOnly.FromDateTime(x.At.DateTime)).When(x => x.Promise is not null)
            .WithMessage("La fecha prometida no puede ser anterior a la gestión.");
        RuleFor(x => x.Promise).NotNull().When(x => x.ResultCode == CollectionActionResult.PromiseToPay)
            .WithMessage("Una gestión con resultado «promesa de pago» lleva la fecha y el valor prometidos.");
    }
}

public sealed class RecordCollectionActionCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    ReglasDeParametrosDeCartera parametros,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RecordCollectionActionCommand, Result<CollectionActionDto>>
{
    public async Task<Result<CollectionActionDto>> Handle(RecordCollectionActionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await CobranzaDeCartera.CreditoAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var credito = leido.Value;
        var hoy = reloj.HoyLocal;
        var ocurrio = request.At.UtcDateTime;
        if (ocurrio > reloj.UtcNow.AddMinutes(1)) return FueraDeTiempo(request.At);

        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.ContactedPersonPublicId && !p.IsDeleted)
            .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Falla(ErroresDeCobranza.ContactoNoObligado(credito.Number));
        var rol = await CobranzaDeCartera.PapelAsync(db, credito, persona.Value, hoy, ct);
        if (rol is null && await CobranzaDeCartera.PagaduriaAsync(db, credito.Id, ct) == persona) rol = ContactRole.Paymaster;
        if (rol is null) return Falla(ErroresDeCobranza.ContactoNoObligado(credito.Number));
        if (rol == ContactRole.Paymaster && request.IsDirectContact)
            return Falla(new ErrorConDatos(ErroresDeCobranza.CodigoContactoNoObligado,
                "La gestión ante la pagaduría no es un contacto directo con el titular: márquela sin contacto directo.", new { loanNumber = credito.Number }));
        // K4, bloque D: la gestión con el titular es de un crédito en cobranza (con etapa vigente); uno que nunca entró o que salió al ponerse al día no se
        // gestiona hasta que vuelva a entrar. La gestión ante la pagaduría (libranza) no depende de la etapa.
        if (rol != ContactRole.Paymaster && !(await CobranzaDeCartera.EtapaVigenteAsync(db, [credito.Id], ct)).ContainsKey(credito.Id))
            return Falla(ErroresDeCobranza.SinEtapa(credito.Number));

        DecisionDeContacto? decision = null;
        if (rol != ContactRole.Paymaster)
        {
            var ventana = await cola.EvaluarGestionAsync(persona.Value, credito.Id, request.Channel, request.IsDirectContact, ocurrio, ct);
            if (ventana.IsFailure) return Falla(ventana.Error);
            decision = ventana.Value.Decision;
            var canal = ColaDeMensajesAlDeudor.CanalDeMensaje(request.Channel) ?? MessageChannel.Letter;
            if (ColaDeMensajesAlDeudor.RechazoDelInmediato(ventana.Value, canal) is { } rechazo) return Falla(rechazo);
            if (decision is Excluido x) return Falla(Excluida(x, ventana.Value.NombreDelTitular));
        }

        decimal tolerancia = 0m;
        if (request.Promise is { } prometida)
        {
            var leida = await parametros.LeerAsync(ParametrosDeCartera.CobranzaToleranciaDePromesa, hoy, ct: ct);
            if (leida.IsFailure) return Falla(leida.Error);
            tolerancia = leida.Value.Valor is decimal t ? Math.Max(0m, t) : 0m;
            if (tolerancia >= prometida.Amount) tolerancia = 0m;
        }

        var gestion = CollectionAction.Registrar(credito.Id, persona.Value, rol.Value, request.Channel, ocurrio, request.IsDirectContact, request.ResultCode,
            usuario, request.Notes, decision: decision);
        db.CollectionActions.Add(gestion);
        await db.SaveChangesAsync(ct);
        if (request.Promise is { } promesa)
        {
            db.PaymentPromises.Add(PaymentPromise.Prometer(gestion.Id, credito.Id, promesa.Date, promesa.Amount, tolerancia));
            await db.SaveChangesAsync(ct);
        }
        return Result.Success((await GestionesDeCobranza.DtosAsync(db, [gestion], hoy, reloj.AhoraLocal.Offset, ct))[0]);
    }

    /// <summary>Lo que dice la ventana cuando excluye una gestión (fallecido con reclamación, insolvencia, cobro suspendido).</summary>
    private static Error Excluida(Excluido x, string titular) => new ErrorConDatos(ErroresDeMensajesAlDeudor.CodigoFueraDeVentana,
        x.Motivo switch
        {
            ContactWindowReason.DebtorDeceased => "El deudor falleció y la reclamación al seguro está en trámite: no se gestiona su cobro.",
            ContactWindowReason.Insolvency => $"{titular} está en un proceso de insolvencia en negociación: el cobro está suspendido.",
            ContactWindowReason.CollectionSuspended => "El cobro de este crédito está suspendido.",
            ContactWindowReason.NoWindow => "No hay un horario permitido por la ley en los próximos días para este contacto.",
            _ => "La ley no permite este contacto.",
        },
        new { reasons = new[] { x.Motivo.ToString() } });

    private static Result<CollectionActionDto> FueraDeTiempo(DateTimeOffset at) => Result.Failure<CollectionActionDto>(new ErrorConDatos(
        "Validation.CollectionAction.FutureInstant", "La gestión ya ocurrió: su instante no puede ser futuro.", new { at }));

    private static Result<CollectionActionDto> Falla(Error e) => Result.Failure<CollectionActionDto>(e);
}

// ---------------------------------------------------------------------------------------------------------------- anular --

/// <summary>
/// Anula una gestión (feature 013, K4, T0945; api.md §14.6, <c>POST /api/lending/collection-actions/{id}/void</c>, <c>Lending.Collections.RecordActivity</c>,
/// con clave y motivo; T77): la corrección es <b>otra gestión</b> que anula a ésta —mismo crédito, persona, canal e instante, sin contacto directo—: la
/// anulada deja de contar para la ventana y su promesa queda anulada. No se anula dos veces (<c>Lending.CollectionAction.AlreadyVoided</c>) ni se anula
/// una que anula (se corrige con otra gestión). Fuera del alcance, el mismo 404. (nuevo)
/// </summary>
public sealed record VoidCollectionActionCommand(Guid CollectionActionPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidCollectionActionCommandValidator : ValidadorConMotivo<VoidCollectionActionCommand>
{
    public VoidCollectionActionCommandValidator() => RuleFor(x => x.CollectionActionPublicId).NotEmpty();
}

public sealed class VoidCollectionActionCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual) : IRequestHandler<VoidCollectionActionCommand, Result>
{
    public async Task<Result> Handle(VoidCollectionActionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeGobierno.SinUsuario());
        var gestion = await db.CollectionActions.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.CollectionActionPublicId && !a.IsDeleted, ct);
        if (gestion is null) return Result.Failure(ErroresDeCobranza.GestionInexistente());
        if (gestion.LoanId is { } loanId)
        {
            var credito = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => new { l.BranchId, l.CreditLineId }).FirstAsync(ct);
            var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeCobranza.GestionInexistente(), auditoria,
                "CollectionAction", gestion.PublicId, ct);
            if (dentro.IsFailure) return dentro;
        }
        if (gestion.EsDeSistema) return Result.Failure(ErroresDeCobranza.GestionDeSistema());
        if (gestion.VoidsActionId is not null || await db.CollectionActions.AnyAsync(a => a.VoidsActionId == gestion.Id, ct))
            return Result.Failure(ErroresDeCobranza.GestionYaAnulada());
        db.CollectionActions.Add(CollectionAction.Registrar(gestion.LoanId, gestion.PersonId, gestion.ContactRole, gestion.Channel, gestion.OccurredAt,
            contactoDirecto: false, gestion.Result, usuario, notas: null, anulaA: gestion.Id, motivoDeAnulacion: request.Reason));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------------------------------------------------- listas --

/// <summary>
/// Las gestiones (feature 013, K4, T0945; api.md §14.6, <c>GET /api/lending/collection-actions?loanPublicId=&amp;collector=&amp;from=&amp;to=&amp;page=</c>,
/// <c>Lending.Collections.Read</c>): con su promesa y quién las anuló, la más reciente primero, filtradas en silencio por la oficina y la línea del
/// crédito. (nuevo)
/// </summary>
public sealed record ListCollectionActionsQuery(Guid? LoanPublicId = null, Guid? Collector = null, DateOnly? From = null, DateOnly? To = null, int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CollectionActionDto>>>;

public sealed class ListCollectionActionsQueryValidator : AbstractValidator<ListCollectionActionsQuery>;

public sealed class ListCollectionActionsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListCollectionActionsQuery, Result<PagedResult<CollectionActionDto>>>
{
    public async Task<Result<PagedResult<CollectionActionDto>>> Handle(ListCollectionActionsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.CollectionActions.AsNoTracking().Where(a => !a.IsDeleted && a.LoanId != null && creditos.Contains(a.LoanId.Value));
        if (request.LoanPublicId is { } loan)
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == loan).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(a => a.LoanId == id);
        }
        if (request.Collector is { } gestor)
        {
            var id = await db.Users.AsNoTracking().Where(u => u.PublicId == gestor).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(a => a.CollectorUserId == id);
        }
        var desfase = reloj.AhoraLocal.Offset;
        if (request.From is { } desde)
        {
            var utc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue) - desfase, DateTimeKind.Utc);
            consulta = consulta.Where(a => a.OccurredAt >= utc);
        }
        if (request.To is { } hasta)
        {
            var utc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) - desfase, DateTimeKind.Utc);
            consulta = consulta.Where(a => a.OccurredAt < utc);
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<CollectionActionDto>(await GestionesDeCobranza.DtosAsync(db, filas, reloj.HoyLocal, desfase, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>
/// Las promesas de pago (feature 013, K4, T0945; api.md §14.6, <c>GET /api/lending/promises?status=&amp;collector=&amp;page=</c>,
/// <c>Lending.Collections.Read</c>; FR-119): con su estado <b>derivado</b> a hoy contra los pagos vigentes (<see cref="EstadoDePromesa"/>) —cumplida sola
/// con el pago, incumplida al vencer—; las de una gestión anulada salen anuladas y no cuentan en ningún estado. Filtradas por el alcance; la fecha más
/// próxima primero. (nuevo)
/// </summary>
public sealed record ListPaymentPromisesQuery(PaymentPromiseStatus? Status = null, Guid? Collector = null, Guid? LoanPublicId = null, int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PaymentPromiseDto>>>;

public sealed class ListPaymentPromisesQueryValidator : AbstractValidator<ListPaymentPromisesQuery>
{
    public ListPaymentPromisesQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public sealed class ListPaymentPromisesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListPaymentPromisesQuery, Result<PagedResult<PaymentPromiseDto>>>
{
    public async Task<Result<PagedResult<PaymentPromiseDto>>> Handle(ListPaymentPromisesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = from p in db.PaymentPromises.AsNoTracking()
                       join a in db.CollectionActions.AsNoTracking() on p.CollectionActionId equals a.Id
                       where !p.IsDeleted && creditos.Contains(p.LoanId)
                       select new { Promesa = p, Gestion = a };
        if (request.Collector is { } gestor)
        {
            var id = await db.Users.AsNoTracking().Where(u => u.PublicId == gestor).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(x => x.Gestion.CollectorUserId == id);
        }
        if (request.LoanPublicId is { } loan)
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == loan).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(x => x.Promesa.LoanId == id);
        }
        var filas = await consulta.ToListAsync(ct);
        var hoy = reloj.HoyLocal;
        var gestionIds = filas.Select(f => f.Gestion.Id).ToList();
        var anuladas = (await db.CollectionActions.AsNoTracking().Where(a => a.VoidsActionId != null && gestionIds.Contains(a.VoidsActionId.Value))
            .Select(a => a.VoidsActionId!.Value).ToListAsync(ct)).ToHashSet();
        var loanIds = filas.Select(f => f.Promesa.LoanId).Distinct().ToList();
        var porCredito = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, loanIds, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, porCredito.Values.Select(c => c.DebtorPersonId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, filas.Select(f => f.Gestion.CollectorUserId), ct);
        var dtos = filas.Select(f => GestionesDeCobranza.Promesa(f.Promesa, porCredito[f.Promesa.LoanId], f.Gestion, anuladas.Contains(f.Gestion.Id),
                pagos.GetValueOrDefault(f.Promesa.LoanId) ?? [], hoy, personas, usuarios))
            .Where(d => request.Status is not { } estado || (!d.Voided && d.Status == estado))
            .OrderBy(d => d.Date).ThenBy(d => d.Loan.Number, StringComparer.Ordinal).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        return Result.Success(new PagedResult<PaymentPromiseDto>(dtos.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(),
            pagina.SafePage, pagina.SafePageSize, dtos.Count));
    }
}
