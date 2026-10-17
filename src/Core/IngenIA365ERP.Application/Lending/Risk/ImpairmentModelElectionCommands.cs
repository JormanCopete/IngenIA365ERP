using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Una elección voluntaria del modelo de deterioro (api.md §12.4). (nuevo)</summary>
public sealed record ImpairmentModelElectionDto(
    Guid PublicId,
    string Kind,
    DateOnly ValidFrom,
    IReadOnlyList<string> Segments,
    Guid? SessionItemPublicId,
    Guid ImpactStudyAttachmentPublicId,
    DateOnly SupervisorNoticeDate,
    Guid SupervisorNoticeAttachmentPublicId,
    int NoticeLeadDays,
    Guid RegulatoryDocumentPublicId,
    string Reason,
    string? RegisteredBy,
    DateTime RegisteredAt);

// ---------------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// <c>POST /api/lending/risk/impairment-model/elections</c> (feature 013, K6a, T1034; api.md §12.4, <c>Lending.ImpairmentModel.Manage</c>, con
/// <c>Idempotency-Key</c>; data-model §23.8; FR-105; R15): la adopción o el desistimiento voluntario del modelo de pérdida esperada por una
/// cooperativa no obligada. <see cref="ElectionPublicId"/> es el sujeto del punto del acta del consejo (<c>ImpairmentModelChange</c>): la pantalla lo
/// genera al proponer el cambio, el consejo lo decide en su sesión y la elección nace con ese <c>PublicId</c>. (nuevo)
/// </summary>
public sealed record RegisterImpairmentModelElectionCommand(
    Guid ElectionPublicId,
    ImpairmentModelElectionKind Kind,
    DateOnly ValidFrom,
    IReadOnlyList<string>? Segments,
    Guid SessionItemPublicId,
    Guid ImpactStudyAttachmentPublicId,
    DateOnly SupervisorNoticeDate,
    Guid SupervisorNoticeAttachmentPublicId,
    string Reason) : IRequest<Result<ImpairmentModelElectionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterImpairmentModelElectionCommandValidator : ValidadorConMotivo<RegisterImpairmentModelElectionCommand>
{
    public RegisterImpairmentModelElectionCommandValidator()
    {
        RuleFor(x => x.ElectionPublicId).NotEmpty().WithMessage("Indique la propuesta del cambio (el sujeto del punto del acta).");
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde qué cierre rige el cambio.");
        RuleFor(x => x.SessionItemPublicId).NotEmpty().WithMessage("Indique el punto del acta del consejo que aprobó el cambio.");
        RuleFor(x => x.ImpactStudyAttachmentPublicId).NotEmpty().WithMessage("Adjunte el estudio de impacto.");
        RuleFor(x => x.SupervisorNoticeDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha del aviso a la Superintendencia.");
        RuleFor(x => x.SupervisorNoticeAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte del aviso a la Superintendencia.");
    }
}

/// <summary>
/// Reglas (data-model §23.8; códigos de <see cref="ErroresDeRiesgo"/>), en este orden:
/// <list type="number">
/// <item>sin K6a desplegada, <c>Lending.ImpairmentModel.NotAvailable</c>;</item>
/// <item>los segmentos (por defecto los tres) son de pérdida esperada (<c>.SegmentInvalid</c>) y hay <c>ReglaDePerdidaEsperada</c> vigente en
/// <c>ValidFrom</c> (<c>.RuleMissing</c>): de ella salen la antelación del aviso y su copia, que se sellan;</item>
/// <item>una cooperativa obligada por esa regla y su perfil vigente en <c>ValidFrom</c> no adopta ni desiste (<c>.Obligated</c>);</item>
/// <item>adopción y desistimiento alternan (<c>.NotAlternating</c>); <c>ValidFrom</c> es posterior a la última elección (<c>.ValidFromTaken</c>) y al
/// último cierre de Cartera (<c>.RetroactiveValidFrom</c>: nunca se recalcula un cierre);</item>
/// <item><c>ValidFrom</c> no antes del aviso más la antelación (<c>.NoticeOutOfTerm</c>, con <c>earliestValidFrom</c>; pregunta E2);</item>
/// <item>al adoptar: el Anexo 2 vigente en <c>ValidFrom</c> trae la PI de la aplicación voluntaria en cada segmento (<c>.VoluntaryPdMissing</c>; la
/// semilla no la trae: la de la CE 100 está por registrar) y los créditos vivos de esos segmentos tienen el historial completo a la fecha del primer
/// cierre que la usa (<c>.IncompleteHistory</c>, con la lista);</item>
/// <item>el estudio de impacto y el soporte del aviso son adjuntos disponibles de la propuesta (dueño
/// <see cref="AdjuntosDeModulo.SoporteDeCambioDelModelo"/> con <see cref="RegisterImpairmentModelElectionCommand.ElectionPublicId"/>;
/// <c>.SupportInvalid</c>, bloque 3 de K6a);</item>
/// <item>el punto del acta: <c>Decision</c> aprobado de una sesión cerrada sobre <c>ImpairmentModelChange</c> y esta propuesta
/// (<c>Lending.Session.ItemMismatch</c>); quien registra tiene persona (<c>Lending.Security.UserWithoutPerson</c>) y queda como radicador.</item>
/// </list>
/// Registrada, audita <c>Lending.ImpairmentModel.ElectionRegistered</c> (módulo <c>Lending</c>) y no recalcula ningún cierre: la lee el siguiente.
/// </summary>
public sealed class RegisterImpairmentModelElectionCommandHandler(
    IApplicationDbContext db,
    INormativaVigente normativa,
    IPerfilRegulatorio perfiles,
    PuntoDeActaCerrado puntoDeActa,
    RegistroDeIntervenciones intervenciones,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterImpairmentModelElectionCommand, Result<ImpairmentModelElectionDto>>
{
    /// <summary>La clase del expediente (y el sujeto del acta) de una elección.</summary>
    public const string ClaseDeExpediente = LendingDecisionSubjects.ImpairmentModelChange;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ImpairmentModelElectionDto>> Handle(RegisterImpairmentModelElectionCommand request, CancellationToken ct)
    {
        if (!SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente))
            return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloNoDisponible());

        var segmentos = (request.Segments is { Count: > 0 } s ? s : ReglaDePerdidaEsperada.SegmentosConocidos)
            .Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var desconocidos = segmentos.Where(x => !ReglaDePerdidaEsperada.SegmentosConocidos.Contains(x, StringComparer.Ordinal)).ToList();
        if (desconocidos.Count > 0) return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloSegmentoInvalido(desconocidos));

        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, request.ValidFrom, ReglaDePerdidaEsperada.Leer, ct: ct);
        if (regla.IsFailure) return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloSinRegla(request.ValidFrom));
        var perfil = await perfiles.VigenteAsync(request.ValidFrom, null, ct);
        if (perfil is not null && regla.Value.Documento.Obliga(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory))
            return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloObligada());

        var ultima = await db.ImpairmentModelElections.AsNoTracking().Where(e => !e.IsDeleted).OrderByDescending(e => e.ValidFrom).FirstOrDefaultAsync(ct);
        var adoptada = ultima?.Kind == ImpairmentModelElectionKind.VoluntaryAdoption;
        if (request.Kind == ImpairmentModelElectionKind.VoluntaryAdoption ? adoptada : !adoptada)
            return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloNoAlterna(request.Kind));
        if (ultima is not null && request.ValidFrom <= ultima.ValidFrom) return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloFechaOcupada(ultima.ValidFrom));
        var ultimoCierre = await db.MonthlyCloses.AsNoTracking().Where(c => !c.IsDeleted).MaxAsync(c => (DateOnly?)c.CloseDate, ct);
        if (ultimoCierre is { } cerrado && request.ValidFrom <= cerrado)
            return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloRetroactivo(request.ValidFrom, cerrado));

        var antelacion = regla.Value.Documento.DiasDeAvisoVoluntario;
        var masTemprana = request.SupervisorNoticeDate.AddDays(antelacion);
        if (request.ValidFrom < masTemprana)
            return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloAvisoFueraDePlazo(request.ValidFrom, masTemprana, antelacion));

        IReadOnlyList<IncompleteArrearsHistoryDto> incompletos = [];
        if (request.Kind == ImpairmentModelElectionKind.VoluntaryAdoption)
        {
            var anexo2 = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo2, request.ValidFrom, Anexo2.Leer, ct: ct);
            var sinPi = anexo2.IsFailure ? segmentos : segmentos.Where(x => !anexo2.Value.Documento.TienePiVoluntaria(x)).ToList();
            if (sinPi.Count > 0) return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloSinPiVoluntaria(sinPi));

            // La fecha de referencia del historial es la del primer cierre en pérdida esperada: el fin del mes en que empieza a regir.
            var primerCierre = Domain.Entities.Lending.Risk.MonthlyClose.UltimoDiaDe(request.ValidFrom.Year, request.ValidFrom.Month);
            var catalogo = await normativa.CatalogoDeModalidadesAsync(primerCierre, null, ct);
            if (catalogo.IsSuccess)
            {
                var vivos = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), reloj.HoyLocal).ToListAsync(ct);
                incompletos = (await CompletitudDelHistorial.IncompletosAsync(db, vivos, regla.Value.Documento, catalogo.Value.Documento, primerCierre, null, ct))
                    .Where(i => segmentos.Contains(i.Segment, StringComparer.Ordinal)).ToList();
            }
            if (incompletos.Count > 0) return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloHistorialIncompleto(incompletos));
        }

        var soportes = await db.Attachments.AsNoTracking()
            .Where(a => (a.PublicId == request.ImpactStudyAttachmentPublicId || a.PublicId == request.SupervisorNoticeAttachmentPublicId)
                && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeCambioDelModelo && a.OwnerEntityPublicId == request.ElectionPublicId
                && a.Status == EstadoDeAdjunto.Available && !a.IsDeleted)
            .Select(a => a.PublicId).ToListAsync(ct);
        var invalidos = new List<string>();
        if (!soportes.Contains(request.ImpactStudyAttachmentPublicId)) invalidos.Add("impactStudyAttachmentPublicId");
        if (!soportes.Contains(request.SupervisorNoticeAttachmentPublicId)) invalidos.Add("noticeAttachmentPublicId");
        if (invalidos.Count > 0) return Result.Failure<ImpairmentModelElectionDto>(ErroresDeRiesgo.ModeloSoporteInvalido(invalidos));

        var punto = await puntoDeActa.ValidarAsync(request.SessionItemPublicId, ClaseDeExpediente, request.ElectionPublicId, ct);
        if (punto.IsFailure) return Result.Failure<ImpairmentModelElectionDto>(punto.Error);
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(ClaseDeExpediente, request.ElectionPublicId), AccionesDeSegregacion.Radicar,
            reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<ImpairmentModelElectionDto>(verificada.Error);

        var eleccion = new ImpairmentModelElection
        {
            PublicId = request.ElectionPublicId,
            Kind = request.Kind,
            ValidFrom = request.ValidFrom,
            SegmentCodes = string.Join(',', segmentos),
            SessionItemId = punto.Value.ItemId,
            ImpactStudyAttachmentPublicId = request.ImpactStudyAttachmentPublicId,
            SupervisorNoticeDate = request.SupervisorNoticeDate,
            SupervisorNoticeAttachmentPublicId = request.SupervisorNoticeAttachmentPublicId,
            NoticeLeadDays = antelacion,
            RegulatoryDocumentPublicId = regla.Value.PublicId,
            IncompleteHistoryCheckJson = JsonSerializer.Serialize(incompletos, Json),
            Reason = request.Reason.Trim(),
            RegisteredByUserId = verificada.Value.UsuarioId,
            RegisteredAt = reloj.UtcNow,
        };
        db.ImpairmentModelElections.Add(eleccion);
        intervenciones.Registrar(verificada.Value, ClaseDeExpediente, request.ElectionPublicId);
        await db.SaveChangesAsync(ct);
        await auditoria.EmitirEleccionDelModeloRegistradaAsync(eleccion.PublicId, new
        {
            kind = eleccion.Kind.ToString(), validFrom = eleccion.ValidFrom, segments = segmentos, noticeDate = eleccion.SupervisorNoticeDate,
            noticeLeadDays = antelacion, ruleDocument = regla.Value.Codigo, sessionItemPublicId = request.SessionItemPublicId,
        }, ct);
        return Result.Success(await ConsultasDeEleccionesDelModelo.DtoAsync(db, eleccion, ct));
    }
}

// ------------------------------------------------------------------------------------------------------------ listar --

/// <summary>
/// <c>GET /api/lending/risk/impairment-model/elections</c> (feature 013, K6a, T1034; api.md §12.4, <c>Lending.Ratings.Read</c>): las elecciones
/// voluntarias, la más reciente primero. Sin datos de deudores. (nuevo)
/// </summary>
public sealed record ListImpairmentModelElectionsQuery : IRequest<Result<IReadOnlyList<ImpairmentModelElectionDto>>>;

public sealed class ListImpairmentModelElectionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListImpairmentModelElectionsQuery, Result<IReadOnlyList<ImpairmentModelElectionDto>>>
{
    public async Task<Result<IReadOnlyList<ImpairmentModelElectionDto>>> Handle(ListImpairmentModelElectionsQuery request, CancellationToken ct)
    {
        var filas = await db.ImpairmentModelElections.AsNoTracking().Where(e => !e.IsDeleted).OrderByDescending(e => e.ValidFrom).ToListAsync(ct);
        var dtos = new List<ImpairmentModelElectionDto>(filas.Count);
        foreach (var e in filas) dtos.Add(await ConsultasDeEleccionesDelModelo.DtoAsync(db, e, ct));
        return Result.Success<IReadOnlyList<ImpairmentModelElectionDto>>(dtos);
    }
}

/// <summary>Lo que comparten el registro y la lista de elecciones. (nuevo)</summary>
public static class ConsultasDeEleccionesDelModelo
{
    public static async Task<ImpairmentModelElectionDto> DtoAsync(IApplicationDbContext db, ImpairmentModelElection e, CancellationToken ct)
    {
        var quien = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == e.RegisteredByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        var punto = await db.SessionItems.AsNoTracking().Where(i => i.Id == e.SessionItemId).Select(i => (Guid?)i.PublicId).FirstOrDefaultAsync(ct);
        return new ImpairmentModelElectionDto(e.PublicId, e.Kind.ToString(), e.ValidFrom, e.Segmentos, punto, e.ImpactStudyAttachmentPublicId,
            e.SupervisorNoticeDate, e.SupervisorNoticeAttachmentPublicId, e.NoticeLeadDays, e.RegulatoryDocumentPublicId, e.Reason, quien, e.RegisteredAt);
    }
}

// ------------------------------------------------------------------------------------------------------- asunto de acta --

/// <summary>
/// El asunto de acta <c>ImpairmentModelChange</c> (feature 013, K6a, T1034; C52, R15): la propuesta de adoptar o desistir de la pérdida esperada que
/// el consejo decide. El sujeto es el <c>PublicId</c> que tendrá la elección: mientras no se registre, el asunto está pendiente y describe la propuesta;
/// registrada, la describe con su huella (clase, vigencia, segmentos y aviso). Decidir no registra: lo hace
/// <see cref="RegisterImpairmentModelElectionCommand"/> con el punto cerrado. (nuevo)
/// </summary>
public sealed class AsuntoDeActaCambioDelModelo(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.ImpairmentModelChange;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var eleccion = await db.ImpairmentModelElections.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == subjectPublicId && !e.IsDeleted, ct);
        var expediente = new ExpedienteDeCartera(RegisterImpairmentModelElectionCommandHandler.ClaseDeExpediente, subjectPublicId);
        if (eleccion is null)
            return Result.Success(new AsuntoDeActa("Cambio del modelo de deterioro (propuesta por registrar)",
                "Adopción o desistimiento voluntario del modelo de pérdida esperada del Anexo 2, con su estudio de impacto y el aviso a la Superintendencia.",
                true, "Proposed", expediente, new HuellaDeCartera().Identificador("proposal", subjectPublicId).Calcular()));

        var huella = new HuellaDeCartera()
            .Identificador("election", eleccion.PublicId)
            .Texto("kind", eleccion.Kind.ToString())
            .Fecha("validFrom", eleccion.ValidFrom)
            .Texto("segments", eleccion.SegmentCodes)
            .Fecha("supervisorNoticeDate", eleccion.SupervisorNoticeDate)
            .Calcular();
        return Result.Success(new AsuntoDeActa(
            string.Create(CultureInfo.InvariantCulture,
                $"{(eleccion.Kind == ImpairmentModelElectionKind.VoluntaryAdoption ? "Adopción" : "Desistimiento")} voluntario de la pérdida esperada (rige desde {eleccion.ValidFrom:yyyy-MM-dd})"),
            eleccion.Reason, false, "Registered", expediente, huella));
    }

    public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct) => Task.FromResult(Result.Success());
}
