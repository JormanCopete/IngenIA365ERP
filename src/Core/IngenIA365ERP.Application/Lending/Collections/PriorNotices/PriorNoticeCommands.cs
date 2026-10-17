using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.PriorNotices;

// Feature 013, K4 (US12, T0950; api.md §14.3; data-model §26.11; adaptadores-y-archivos.md §5; FR-123, SC-012, T77; Leyes 1266 de 2008 y 2157 de
// 2021): el aviso previo al reporte negativo por episodio de mora, con su constancia. Lo abre la acción «aviso previo» de la etapa en el ciclo diario o
// se envía a mano; la carta física se registra con su guía. El estado no se guarda: lo deriva AvisoPrevioPorEpisodio. (nuevo)

/// <summary>Una comunicación de un aviso previo (api.md §14.3 <c>communications[]</c>). (nuevo)</summary>
public sealed record PriorNoticeCommunicationDto(Guid DebtorMessagePublicId, MessageChannel Channel, DebtorMessageEventKind LastEvent, DateTimeOffset? SentAt,
    bool Evidence, bool ManualEvidence, string? TrackingNumber);

/// <summary>Un aviso previo con su estado derivado (api.md §14.3). <see cref="Origin"/>: <c>Migration</c> o <c>Process</c>. (nuevo)</summary>
public sealed record PriorNoticeDto(
    Guid PublicId,
    CreditoRefDto Loan,
    PersonaRefDto Person,
    DateOnly EpisodeStart,
    int RequiredCommunications,
    int MinimumDays,
    decimal? ThresholdAmount,
    IReadOnlyList<PriorNoticeCommunicationDto> Communications,
    DateOnly? EvidenceDate,
    DateOnly? EarliestReportDate,
    PriorNoticeStatus Status,
    DateOnly? EpisodeClosedOn,
    string Origin);

/// <summary>
/// Lo común del aviso previo (K4, T0950): abrirlo para un episodio con lo vigente sellado, derivar su estado con <see cref="AvisoPrevioPorEpisodio"/> y
/// proyectarlo. La constancia la decide <see cref="Adapters.GuardiaDeCanalesExternos"/>: en producción un envío <c>SIMULADO</c> no es constancia. (nuevo)
/// </summary>
public static class AvisosPrevios
{
    public const string Plantilla = "AVISOPREV";
    public const string Origen = "PriorNotice";
    public const string OrigenMigracion = "Migration";
    public const string OrigenProceso = "Process";

    /// <summary>
    /// El aviso del episodio del crédito (por la UK <c>(LoanId, PersonId, EpisodeStartDate)</c>) o uno nuevo con lo vigente sellado: los días mínimos
    /// (<c>AvisoPrevio.DiasMinimos</c>, con su piso legal) y el umbral (<c>AvisoPrevio.UmbralDosComunicaciones</c> en veces el SMMLV, por el SMMLV del día)
    /// que dicen si se exigen una o dos comunicaciones según el saldo de la obligación. El nuevo queda en la unidad de trabajo, sin guardar.
    /// <paramref name="titular"/> es el avisado: el deudor si no se dice, o un codeudor cuando <c>AvisoPrevio.IncluirCodeudores</c> lo pide (decisión del
    /// dueño del 2026-10-16), cada uno con su propio aviso y su propia constancia.
    /// </summary>
    public static async Task<Result<(PriorNotice Aviso, bool Nuevo)>> AbrirAsync(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros,
        IValoresNacionales valores, Loan credito, DateOnly inicioDelEpisodio, decimal saldo, DateOnly dia, CancellationToken ct, int? titular = null)
    {
        var persona = titular ?? credito.DebtorPersonId;
        var existente = db.PriorNotices.Local.FirstOrDefault(n => n.LoanId == credito.Id && n.PersonId == persona && n.EpisodeStartDate == inicioDelEpisodio)
                        ?? await db.PriorNotices.FirstOrDefaultAsync(n => n.LoanId == credito.Id && n.PersonId == persona
                                                                          && n.EpisodeStartDate == inicioDelEpisodio && !n.IsDeleted, ct);
        if (existente is not null) return Result.Success((existente, false));

        var dias = await parametros.LeerAsync(ParametrosDeCartera.AvisoPrevioDiasMinimos, dia, ct: ct);
        if (dias.IsFailure) return Result.Failure<(PriorNotice, bool)>(dias.Error);
        var umbral = await parametros.LeerAsync(ParametrosDeCartera.AvisoPrevioUmbralDosComunicaciones, dia, ct: ct);
        if (umbral.IsFailure) return Result.Failure<(PriorNotice, bool)>(umbral.Error);
        var smmlv = await valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, dia, null, ct);
        if (smmlv.IsFailure) return Result.Failure<(PriorNotice, bool)>(smmlv.Error);
        decimal? umbralEnPesos = umbral.Value.Valor is decimal veces && smmlv.Value.Valor is { } salario ? decimal.Round(veces * salario, 2) : null;
        var minimos = dias.Value.Valor is int d ? d : int.TryParse(dias.Value.Texto, out var t) ? t : 0;
        var aviso = new PriorNotice
        {
            LoanId = credito.Id,
            PersonId = persona,
            EpisodeStartDate = inicioDelEpisodio,
            RequiredCommunications = (byte)(umbralEnPesos is { } u ? AvisoPrevioPorEpisodio.ComunicacionesExigidas(saldo, u) : 1),
            MinimumDays = minimos,
            ThresholdAmount = umbralEnPesos,
        };
        db.PriorNotices.Add(aviso);
        return Result.Success((aviso, true));
    }

    /// <summary>¿El aviso previo incluye a los codeudores al <paramref name="dia"/> (<c>AvisoPrevio.IncluirCodeudores</c>)? Si no se puede leer, no.</summary>
    public static async Task<bool> IncluyeCodeudoresAsync(ReglasDeParametrosDeCartera parametros, DateOnly dia, CancellationToken ct)
    {
        var p = await parametros.LeerAsync(ParametrosDeCartera.AvisoPrevioIncluirCodeudores, dia, ct: ct);
        return p.IsSuccess && p.Value.Valor is true;
    }

    /// <summary>Las variables del aviso (adaptadores-y-archivos.md §4.7): la obligación, lo vencido y la fecha más temprana de reporte.</summary>
    public static Dictionary<string, string?> Variables(decimal obligacion, decimal vencido, DateOnly masTemprana) => new()
    {
        ["ValorDeLaObligacion"] = ErroresDeCobranza.Pesos(obligacion),
        ["ValorVencido"] = ErroresDeCobranza.Pesos(vencido),
        ["FechaMasTempranaDeReporte"] = ErroresDeCobranza.Fecha(masTemprana),
        ["Centrales"] = "las centrales de información financiera",
    };

    /// <summary>La serie de días de mora del crédito desde <paramref name="desde"/> (la última versión de cada posición diaria).</summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<MoraDelDia>>> SeriesAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds,
        DateOnly desde, CancellationToken ct)
    {
        var ids = loanIds.ToArray();
        var filas = await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.PositionDate >= desde)
            .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.DaysPastDue }).ToListAsync(ct);
        return ids.ToDictionary(id => id, id => (IReadOnlyList<MoraDelDia>)filas.Where(f => f.LoanId == id).GroupBy(f => f.PositionDate)
            .Select(g => g.OrderByDescending(f => f.Version).First()).Select(f => new MoraDelDia(f.PositionDate, f.DaysPastDue)).ToList());
    }

    /// <summary>El estado derivado de los avisos (con sus comunicaciones), con los episodios de las posiciones diarias y lo migrado.</summary>
    public static async Task<IReadOnlyDictionary<int, (EvaluacionDeAviso Evaluacion, IReadOnlyList<PriorNoticeCommunicationDto> Comunicaciones)>> EvaluarAsync(
        IApplicationDbContext db, IReadOnlyList<PriorNotice> avisos, bool enProduccion, TimeSpan desfase, CancellationToken ct)
    {
        var salida = new Dictionary<int, (EvaluacionDeAviso, IReadOnlyList<PriorNoticeCommunicationDto>)>();
        if (avisos.Count == 0) return salida;
        var ids = avisos.Select(a => a.Id).ToList();
        var mensajes = await db.DebtorMessages.AsNoTracking().Where(m => m.PriorNoticeId != null && ids.Contains(m.PriorNoticeId.Value)).ToListAsync(ct);
        var locales = db.DebtorMessages.Local.Where(m => m.PriorNoticeId is { } p && ids.Contains(p) && m.Id == 0).ToList();
        var mensajeIds = mensajes.Select(m => m.Id).ToList();
        var eventos = await db.DebtorMessageEvents.AsNoTracking().Where(e => mensajeIds.Contains(e.DebtorMessageId)).ToListAsync(ct);
        var series = await SeriesAsync(db, avisos.Select(a => a.LoanId).Distinct().ToList(), avisos.Min(a => a.EpisodeStartDate), ct);
        foreach (var a in avisos)
        {
            var comunicaciones = new List<PriorNoticeCommunicationDto>();
            var constancias = new List<ConstanciaDeAviso>();
            foreach (var m in mensajes.Where(m => m.PriorNoticeId == a.Id).Concat(locales.Where(m => m.PriorNoticeId == a.Id)).OrderBy(m => m.RequestedAt))
            {
                var historia = eventos.Where(e => e.DebtorMessageId == m.Id).OrderBy(e => e.Sequence).ToList();
                var ultimo = historia.LastOrDefault()?.Kind ?? DebtorMessageEventKind.Queued;
                var salio = historia.FirstOrDefault(e => e.Kind is DebtorMessageEventKind.Sent or DebtorMessageEventKind.Delivered
                    || (!enProduccion && e.Kind == DebtorMessageEventKind.Simulated));
                var simulado = CodigosDeCanalExterno.EsSimulado(m.ChannelCode);
                var manual = m.ChannelCode == ColaDeMensajesAlDeudor.CanalDeCartaManual;
                var conConstancia = salio is not null && ultimo != DebtorMessageEventKind.Failed && !(enProduccion && simulado);
                if (salio is not null && ultimo != DebtorMessageEventKind.Failed)
                    constancias.Add(new ConstanciaDeAviso(DateOnly.FromDateTime(CobranzaDeCartera.ALocal(salio.OccurredAt, desfase)), simulado));
                comunicaciones.Add(new PriorNoticeCommunicationDto(m.PublicId, m.Channel, ultimo,
                    salio is null ? null : new DateTimeOffset(CobranzaDeCartera.ALocal(salio.OccurredAt, desfase), desfase), conConstancia, manual,
                    manual ? salio?.ProviderReference : null));
            }
            var episodio = AvisoPrevioPorEpisodio.Episodios(series.GetValueOrDefault(a.LoanId) ?? []).FirstOrDefault(e => e.Inicio == a.EpisodeStartDate);
            var evaluacion = AvisoPrevioPorEpisodio.Evaluar(new AvisoParaEvaluar(a.EpisodeStartDate, Math.Max(1, (int)a.RequiredCommunications), a.MinimumDays)
            {
                MigradoPrimerEnvio = a.MigratedFirstSentOn,
                MigradoSegundoEnvio = a.MigratedSecondSentOn,
                ReportadoNegativoDesde = a.MigratedReportedNegativeSince,
            }, constancias, episodio, enProduccion);
            salida[a.Id] = (evaluacion, comunicaciones);
        }
        return salida;
    }

    public static async Task<IReadOnlyList<PriorNoticeDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<PriorNotice> avisos, bool enProduccion,
        TimeSpan desfase, CancellationToken ct)
    {
        var evaluados = await EvaluarAsync(db, avisos, enProduccion, desfase, ct);
        var loanIds = avisos.Select(a => a.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => loanIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new CreditoRefDto(l.PublicId, l.Number), ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, avisos.Select(a => a.PersonId), ct);
        return avisos.Select(a =>
        {
            var (e, comunicaciones) = evaluados[a.Id];
            return new PriorNoticeDto(a.PublicId, creditos[a.LoanId], personas[a.PersonId], a.EpisodeStartDate, a.RequiredCommunications, a.MinimumDays,
                a.ThresholdAmount, comunicaciones, e.DiasConConstancia.Count > 0 ? e.DiasConConstancia[^1] : null, e.FechaMasTempranaDeReporte, e.Estado, e.CerradoEl,
                a.MigrationLoadId is null ? OrigenProceso : OrigenMigracion);
        }).ToList();
    }

    /// <summary>El aviso con su crédito, del alcance (fuera, el mismo 404).</summary>
    public static async Task<Result<(PriorNotice Aviso, Loan Credito)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, CancellationToken ct)
    {
        var aviso = await db.PriorNotices.AsNoTracking().FirstOrDefaultAsync(n => n.PublicId == publicId && !n.IsDeleted, ct);
        if (aviso is null) return Result.Failure<(PriorNotice, Loan)>(ErroresDeCobranza.AvisoInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == aviso.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeCobranza.AvisoInexistente(), auditoria, "PriorNotice", aviso.PublicId,
            ct);
        return dentro.IsFailure ? Result.Failure<(PriorNotice, Loan)>(dentro.Error) : Result.Success((aviso, credito));
    }
}

// ---------------------------------------------------------------------------------------------------------------- enviar --

/// <summary>
/// Envía a mano una comunicación del aviso previo (feature 013, K4, T0950; api.md §14.3, <c>POST /api/lending/prior-notices/{id}/send</c>,
/// <c>Lending.PriorNotices.Send</c>, con clave): por el canal pedido, con la plantilla <c>AVISOPREV</c>, como un envío manual <b>inmediato</b> —fuera de la
/// ventana legal se rechaza con los códigos de los mensajes (<c>Lending.DebtorMessage.*</c>)—. Un aviso que ya tiene las constancias exigidas no necesita
/// otra (<c>Lending.PriorNotice.AlreadyEvidenced</c>); el de un episodio ya cerrado tampoco (<c>.EpisodeClosed</c>). Responde el mensaje. (nuevo)
/// </summary>
public sealed record SendPriorNoticeCommand(Guid PriorNoticePublicId, MessageChannel Channel) : IRequest<Result<DebtorMessageDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendPriorNoticeCommandValidator : AbstractValidator<SendPriorNoticeCommand>
{
    public SendPriorNoticeCommandValidator()
    {
        RuleFor(x => x.PriorNoticePublicId).NotEmpty();
        RuleFor(x => x.Channel).Must(c => c is MessageChannel.Email or MessageChannel.Sms or MessageChannel.WhatsApp)
            .WithMessage("El aviso previo se envía por correo, SMS o WhatsApp; la carta se registra con su guía.");
    }
}

public sealed class SendPriorNoticeCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<SendPriorNoticeCommand, Result<DebtorMessageDto>>
{
    public async Task<Result<DebtorMessageDto>> Handle(SendPriorNoticeCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await AvisosPrevios.LeerAsync(db, alcance, auditoria, request.PriorNoticePublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var (aviso, credito) = leido.Value;
        var desfase = reloj.AhoraLocal.Offset;
        var (evaluacion, comunicaciones) = (await AvisosPrevios.EvaluarAsync(db, [aviso], cola.EnProduccion, desfase, ct))[aviso.Id];
        if (evaluacion.Estado == PriorNoticeStatus.Closed) return Falla(ErroresDeCobranza.EpisodioCerrado(credito.Number));
        if (evaluacion.Estado == PriorNoticeStatus.Valid) return Falla(ErroresDeCobranza.AvisoCompleto(credito.Number));

        var posicion = await db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == credito.Id).OrderByDescending(p => p.PositionDate)
            .ThenByDescending(p => p.Version).FirstOrDefaultAsync(ct);
        var variables = AvisosPrevios.Variables(posicion?.PayoffAmount ?? 0m,
            posicion is null ? 0m : posicion.PrincipalPastDue + posicion.DefaultInterest + posicion.DefaultInterestInSuspense, reloj.HoyLocal.AddDays(aviso.MinimumDays));
        var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(aviso.PersonId, aviso.LoanId, DebtorMessagePurpose.PriorNotice, request.Channel,
            AvisosPrevios.Plantilla, AvisosPrevios.Origen, $"{aviso.PublicId:N}:{comunicaciones.Count + 1}", ActorKind.Person, usuario)
        {
            OriginPublicId = aviso.PublicId,
            PriorNoticeId = aviso.Id,
            Inmediato = true,
            Valores = variables,
        }, ct);
        if (r.IsFailure) return Falla(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ProyeccionDeMensajesAlDeudor.UnoAsync(db, r.Value.Mensaje.Id, ct));
    }

    private static Result<DebtorMessageDto> Falla(Error e) => Result.Failure<DebtorMessageDto>(e);
}

// ------------------------------------------------------------------------------------------------------- constancia manual --

/// <summary>
/// Registra la constancia de una carta del aviso previo (feature 013, K4, T0950; api.md §14.3, <c>POST /api/lending/prior-notices/{id}/evidence</c>,
/// <c>Lending.PriorNotices.RegisterEvidence</c>, con clave; adaptadores-y-archivos.md §5.3): la comunicación número <see cref="CommunicationNumber"/> (de
/// 1 a las exigidas), el instante de envío, la transportadora y la guía, con la guía adjunta como <c>LendingPriorNoticeEvidence</c> del aviso
/// (<c>Lending.PriorNotice.AttachmentInvalid</c>). Nace el mensaje <c>Letter</c> con su evento <c>Sent</c> y el adjunto: es constancia en producción. Una
/// comunicación ya registrada responde <c>.EvidenceDuplicate</c>; un aviso que ya tiene sus constancias, <c>.AlreadyEvidenced</c>. Responde el aviso.
/// (nuevo)
/// </summary>
public sealed record RegisterPriorNoticeEvidenceCommand(
    Guid PriorNoticePublicId,
    int CommunicationNumber,
    DateTimeOffset SentAt,
    string Carrier,
    string TrackingNumber,
    Guid AttachmentPublicId) : IRequest<Result<PriorNoticeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterPriorNoticeEvidenceCommandValidator : AbstractValidator<RegisterPriorNoticeEvidenceCommand>
{
    public RegisterPriorNoticeEvidenceCommandValidator()
    {
        RuleFor(x => x.PriorNoticePublicId).NotEmpty();
        RuleFor(x => x.CommunicationNumber).InclusiveBetween(1, 2).WithMessage("El aviso exige una o dos comunicaciones.");
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(60).WithMessage("Indique la transportadora.");
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(50).WithMessage("Indique el número de guía.");
        RuleFor(x => x.AttachmentPublicId).NotEmpty().WithMessage("Adjunte la guía de envío.");
    }
}

public sealed class RegisterPriorNoticeEvidenceCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterPriorNoticeEvidenceCommand, Result<PriorNoticeDto>>
{
    public async Task<Result<PriorNoticeDto>> Handle(RegisterPriorNoticeEvidenceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await AvisosPrevios.LeerAsync(db, alcance, auditoria, request.PriorNoticePublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var (aviso, credito) = leido.Value;
        if (request.CommunicationNumber > aviso.RequiredCommunications)
            return Falla(new ErrorConDatos("Validation.PriorNotice.CommunicationNumber",
                $"Este aviso exige {aviso.RequiredCommunications} comunicación(es): no hay una comunicación {request.CommunicationNumber}.",
                new { required = aviso.RequiredCommunications }));
        if (!await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.AttachmentPublicId && a.OwnerEntityType == AdjuntosDeModulo.ConstanciaDeAvisoPrevio
                && a.OwnerEntityPublicId == aviso.PublicId && a.Status == EstadoDeAdjunto.Available, ct))
            return Falla(ErroresDeCobranza.SoporteDeAvisoInvalido());
        var desfase = reloj.AhoraLocal.Offset;
        var (evaluacion, _) = (await AvisosPrevios.EvaluarAsync(db, [aviso], cola.EnProduccion, desfase, ct))[aviso.Id];
        if (evaluacion.Estado == PriorNoticeStatus.Valid) return Falla(ErroresDeCobranza.AvisoCompleto(credito.Number));
        var clave = $"PriorNotice:{aviso.PublicId:N}:carta-{request.CommunicationNumber}:{DebtorMessagePurpose.PriorNotice}";
        if (await db.DebtorMessages.AnyAsync(m => m.IdempotencyKey == clave, ct)) return Falla(ErroresDeCobranza.ConstanciaRepetida(request.CommunicationNumber));

        var persona = await db.People.AsNoTracking().FirstAsync(p => p.Id == aviso.PersonId, ct);
        var direccion = ColaDeMensajesAlDeudor.DestinoDe(persona, MessageChannel.Letter) ?? "Dirección registrada en la guía";
        var rol = credito.DebtorPersonId == aviso.PersonId ? ContactRole.Debtor : ContactRole.CoDebtor;
        cola.RegistrarCartaDeAviso(aviso, rol, request.CommunicationNumber, request.SentAt.UtcDateTime, request.Carrier, request.TrackingNumber,
            request.AttachmentPublicId, usuario, direccion);
        await db.SaveChangesAsync(ct);
        return Result.Success((await AvisosPrevios.DtosAsync(db, [aviso], cola.EnProduccion, desfase, ct))[0]);
    }

    private static Result<PriorNoticeDto> Falla(Error e) => Result.Failure<PriorNoticeDto>(e);
}

// ------------------------------------------------------------------------------------------------------------------- lista --

/// <summary>
/// Los avisos previos (feature 013, T0950 —la consulta de K1 que faltaba, ampliada con el estado derivado—; api.md §14.3,
/// <c>GET /api/lending/prior-notices?loanPublicId=&amp;status=&amp;page=</c>, <c>Lending.PriorNotices.Read</c>): con sus comunicaciones, la constancia, la
/// fecha más temprana de reporte y el estado <b>calculado</b> (<c>Pending</c>, <c>InProgress</c>, <c>Valid</c>, <c>Closed</c>) por
/// <see cref="AvisoPrevioPorEpisodio"/>; lo migrado cuenta (sus envíos y el reporte vigente). Filtrados en silencio por el alcance; el episodio más
/// reciente primero. (nuevo)
/// </summary>
public sealed record ListPriorNoticesQuery(Guid? LoanPublicId = null, PriorNoticeStatus? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PriorNoticeDto>>>;

public sealed class ListPriorNoticesQueryValidator : AbstractValidator<ListPriorNoticesQuery>
{
    public ListPriorNoticesQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public sealed class ListPriorNoticesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, ColaDeMensajesAlDeudor cola, IDateTimeService reloj)
    : IRequestHandler<ListPriorNoticesQuery, Result<PagedResult<PriorNoticeDto>>>
{
    public async Task<Result<PagedResult<PriorNoticeDto>>> Handle(ListPriorNoticesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.PriorNotices.AsNoTracking().Where(n => !n.IsDeleted && creditos.Contains(n.LoanId));
        if (request.LoanPublicId is { } loan)
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == loan).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(n => n.LoanId == id);
        }
        var avisos = await consulta.OrderByDescending(n => n.EpisodeStartDate).ThenByDescending(n => n.Id).ToListAsync(ct);
        var dtos = await AvisosPrevios.DtosAsync(db, avisos, cola.EnProduccion, reloj.AhoraLocal.Offset, ct);
        var filtrados = request.Status is { } estado ? dtos.Where(d => d.Status == estado).ToList() : dtos.ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        return Result.Success(new PagedResult<PriorNoticeDto>(filtrados.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(),
            pagina.SafePage, pagina.SafePageSize, filtrados.Count));
    }
}
