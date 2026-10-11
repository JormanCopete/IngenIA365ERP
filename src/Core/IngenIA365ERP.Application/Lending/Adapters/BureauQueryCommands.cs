using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Adapters;

// Feature 013, US2 (bloque 6B; T0402; FR-006, FR-036, T69, T71–T73; api.md §4.4; adaptadores §3.8; data-model §2.7): las consultas a centrales de
// riesgo. Sin autorización vigente con evidencia no se consulta; dentro de la vigencia se reutiliza la última; el informe crudo es un adjunto de
// módulo no borrable; leerlo es una lectura sensible auditada. (nuevo)

/// <summary>Lo que responde una consulta (api.md §4.4): sólo el estado, nunca el contenido. (nuevo)</summary>
public sealed record BureauQueryCreatedDto(Guid BureauQueryPublicId, string Status, bool Simulated, DateTime QueriedAt, DateOnly ValidUntil, bool Reused);

/// <summary>El resumen de una consulta (api.md §4.4 <c>summary</c>). (nuevo)</summary>
public sealed record BureauQuerySummaryDto(decimal? Score, int? NegativeMarks);

/// <summary>El informe crudo de una consulta (api.md §4.4 <c>artifact</c>). (nuevo)</summary>
public sealed record BureauReportArtifactDto(Guid AttachmentPublicId, string FileName);

/// <summary>Una consulta a centrales (api.md §4.4). <see cref="Artifact"/> sólo en el detalle. (nuevo)</summary>
public sealed record BureauQueryDto(
    Guid PublicId,
    PersonaRefDto Person,
    string BureauCode,
    BureauQueryPurpose Purpose,
    string Status,
    bool Simulated,
    DateTime QueriedAt,
    DateOnly ValidUntil,
    BureauQuerySummaryDto? Summary,
    Guid? ApplicationPublicId,
    BureauReportArtifactDto? Artifact);

/// <summary>Los estados de una consulta en la respuesta (api.md §4.4). (nuevo)</summary>
public static class EstadosDeConsultaACentral
{
    public const string Ok = "Ok";
    public const string SinHistoria = "NoHistory";
    public const string CanalCaido = "ChannelUnavailable";
    public const string DatoInvalido = "InvalidData";

    public static string De(ExternalOutcome outcome) => outcome switch
    {
        ExternalOutcome.Ok => Ok,
        ExternalOutcome.NotFound => SinHistoria,
        ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess => CanalCaido,
        _ => DatoInvalido,
    };
}

/// <summary>
/// Consulta a una central de riesgo (api.md §4.4, <c>POST /api/lending/bureau-queries</c>, <c>Lending.CreditBureau.Query</c>, con clave):
/// <list type="number">
/// <item>la persona; si es para una solicitud, la solicitud en el alcance;</item>
/// <item>una autorización vigente con finalidad <c>CreditBureauQuery</c> (<c>Lending.Authorization.Missing</c>) y con evidencia: una migrada sin
/// ella no sirve (<c>Lending.Authorization.WithoutEvidence</c>, FR-006);</item>
/// <item>dentro de <c>Centrales.DiasDeVigenciaDeConsulta</c> se reutiliza la última consulta que respondió a la misma central;</item>
/// <item>el canal vigente de la central (<c>COR_ExternalChannelSettings</c>; sin él, <c>Core.ExternalChannel.NotConfigured</c>) con su credencial,
/// por el registro de centrales (circuito incluido); canal caído → <c>Lending.Adapter.Unavailable</c> con <c>channelCode</c>;</item>
/// <item>guarda <c>LND_BureauQueries</c> con el canal sellado, la autorización usada, el resumen, la bandera de simulado y el informe crudo como
/// adjunto de módulo <c>LendingBureauReport</c>. La respuesta trae sólo el estado.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record QueryCreditBureauCommand(
    Guid PersonPublicId,
    string BureauCode,
    string Purpose,
    Guid? ApplicationPublicId = null,
    Guid? CreditLimitPublicId = null) : IRequest<Result<BureauQueryCreatedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    public const string ParaSolicitud = "CreditApplication";
    public const string ParaCupo = "CreditLimit";
    public const string ParaRevision = "Review";
}

public sealed class QueryCreditBureauCommandValidator : AbstractValidator<QueryCreditBureauCommand>
{
    public QueryCreditBureauCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.BureauCode).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Indique el código de la central.");
        RuleFor(x => x.Purpose).Must(p => p is QueryCreditBureauCommand.ParaSolicitud or QueryCreditBureauCommand.ParaCupo or QueryCreditBureauCommand.ParaRevision)
            .WithMessage("El propósito es CreditApplication, CreditLimit o Review.");
        RuleFor(x => x.ApplicationPublicId).NotNull().When(x => x.Purpose == QueryCreditBureauCommand.ParaSolicitud)
            .WithMessage("La consulta para una solicitud nombra la solicitud.");
    }
}

public sealed class QueryCreditBureauCommandHandler(
    IApplicationDbContext db,
    ICentralesDeRiesgo centrales,
    ICredencialesDeCanalExterno credenciales,
    ICurrentTenantService tenant,
    IOptions<CanalesExternosOptions> opciones,
    IAutorizacionDeDatos autorizaciones,
    ReglasDeParametrosDeCartera parametros,
    ISender sender,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<QueryCreditBureauCommand, Result<BureauQueryCreatedDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<BureauQueryCreatedDto>> Handle(QueryCreditBureauCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var central = request.BureauCode.Trim().ToUpperInvariant();
        var hoy = reloj.HoyLocal;

        int? solicitud = null;
        if (request.ApplicationPublicId is { } pedida)
        {
            var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, pedida, seguir: false, ct);
            if (leida.IsFailure) return Falla(leida.Error);
            solicitud = leida.Value.Id;
        }

        var autorizacion = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditBureauQuery, null, ct);
        var nombre = ReferenciasDeGobierno.Nombre(persona);
        if (autorizacion is not { Autoriza: true })
            return Falla(ErroresDeOriginacion.SinAutorizacion(persona.PublicId, nombre, DataAuthorizationPurpose.CreditBureauQuery));
        if (!autorizacion.HasEvidence) return Falla(ErroresDeDecisionesYCupos.SinEvidencia(persona.PublicId, nombre));

        // Dentro de la vigencia se reutiliza la última consulta que respondió a la misma central (adaptadores §3.8).
        var anteriores = await db.BureauQueries.AsNoTracking().Where(q => q.PersonId == persona.Id && q.BureauCode == central && !q.IsDeleted).ToListAsync(ct);
        if (BureauQuery.UltimaVigente(anteriores.Where(q => q.Respondio), persona.Id, hoy) is { } vigente)
            return Result.Success(new BureauQueryCreatedDto(vigente.PublicId, EstadosDeConsultaACentral.De(vigente.Outcome), vigente.IsSimulated, vigente.QueriedAt,
                vigente.ValidUntil, Reused: true));

        var canal = await CanalVigenteAsync(central, hoy, ct);
        if (canal is null) return Falla(ErroresDeDecisionesYCupos.CanalNoConfigurado(central));
        CredencialDeCanalExterno? credencial = null;
        if (!CodigosDeCanalExterno.NoPideCredencial(canal))
        {
            var resuelta = await credenciales.ResolverAsync(canal, ct);
            if (resuelta.IsFailure) return Falla(resuelta.Error);
            credencial = resuelta.Value;
        }

        var publicId = Guid.NewGuid();
        var contexto = new ContextoDeCanalExterno(CooperativaDelCanal.Resuelta(tenant) ?? Guid.Empty, canal, central, credencial, publicId.ToString("N"),
            opciones.Value.AmbienteDePruebas);
        var r = await centrales.Resolver(canal).ConsultarAsync(new ConsultaACentral(persona.PublicId, persona.IdType ?? string.Empty, persona.TaxId ?? string.Empty,
            request.Purpose, autorizacion.ConsentPublicId ?? autorizacion.PolicyVersionPublicId), contexto, ct);
        if (r.Outcome is ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess) return Falla(ErroresDeDecisionesYCupos.CanalNoDisponible(r.ChannelCode, r.Detalle));

        // El informe crudo, como adjunto de módulo no borrable del dueño LendingBureauReport (antes de guardar la consulta, que es un hecho).
        Guid? informe = null;
        foreach (var artefacto in r.Artefactos.Take(1))
        {
            var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.InformeDeCentrales, publicId, artefacto.NombreDeArchivo, artefacto.ContentType,
                artefacto.Bytes), ct);
            if (subida.IsFailure) return Falla(subida.Error);
            informe = subida.Value;
        }

        var dias = await parametros.LeerAsync(ParametrosDeCartera.CentralesDiasDeVigenciaDeConsulta, hoy, ct: ct);
        if (dias.IsFailure) return Falla(dias.Error);
        var valor = r.Valor;
        var outcome = r.Outcome == ExternalOutcome.Ok && valor is { SinHistoria: true } ? ExternalOutcome.NotFound : r.Outcome;
        var consulta = new BureauQuery
        {
            PublicId = publicId,
            PersonId = persona.Id,
            Purpose = request.Purpose == QueryCreditBureauCommand.ParaSolicitud ? BureauQueryPurpose.Application : BureauQueryPurpose.CreditLimit,
            ApplicationId = solicitud,
            BureauCode = central,
            ChannelCode = r.ChannelCode,
            AuthorizationConsentPublicId = autorizacion.ConsentPublicId ?? autorizacion.PolicyVersionPublicId,
            Outcome = outcome,
            Score = valor?.Puntaje,
            NegativeMarks = valor?.MarcasNegativas.Count,
            Summary = valor is null
                ? null
                : JsonSerializer.Serialize(new
                {
                    score = valor.Puntaje, noHistory = valor.SinHistoria, negativeMarks = valor.MarcasNegativas, reference = r.ReferenciaExterna,
                    purpose = request.Purpose,
                }, Json),
            ReportAttachmentPublicId = informe,
            IsSimulated = r.Simulado || CodigosDeCanalExterno.EsSimulado(r.ChannelCode),
            QueriedAt = reloj.UtcNow,
            QueriedByUserId = usuario,
            ValidUntil = hoy.AddDays(dias.Value.Valor is int d ? d : 0),
        };
        db.BureauQueries.Add(consulta);
        await db.SaveChangesAsync(ct);
        return Result.Success(new BureauQueryCreatedDto(consulta.PublicId, EstadosDeConsultaACentral.De(consulta.Outcome), consulta.IsSimulated, consulta.QueriedAt,
            consulta.ValidUntil, Reused: false));
    }

    /// <summary>El canal vigente de una central (<c>COR_ExternalChannelSettings</c>, clase <c>CreditBureau</c> y su <c>BureauCode</c>), o nulo.</summary>
    private async Task<string?> CanalVigenteAsync(string central, DateOnly fecha, CancellationToken ct)
    {
        var configurados = await db.ExternalChannelSettings.AsNoTracking()
            .Where(s => s.Kind == ExternalChannelKind.CreditBureau && s.BureauCode == central && !s.IsDeleted).ToListAsync(ct);
        return configurados.Where(s => s.VigenteEn(fecha)).OrderByDescending(s => s.ValidFrom).Select(s => s.ChannelCode).FirstOrDefault();
    }

    private static Result<BureauQueryCreatedDto> Falla(Error e) => Result.Failure<BureauQueryCreatedDto>(e);
}

/// <summary>Arma los DTO de consultas a centrales y aplica el alcance de la solicitud. (nuevo)</summary>
public static class ConsultasACentrales
{
    public static async Task<IReadOnlyList<BureauQueryDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<BureauQuery> filas, bool conArtefacto,
        CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(f => f.PersonId), ct);
        var solicitudes = filas.Where(f => f.ApplicationId is not null).Select(f => f.ApplicationId!.Value).Distinct().ToList();
        var publicas = await db.CreditApplications.AsNoTracking().Where(a => solicitudes.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.PublicId, ct);
        var adjuntos = filas.Where(f => f.ReportAttachmentPublicId is not null).Select(f => f.ReportAttachmentPublicId!.Value).ToList();
        var nombres = conArtefacto
            ? await db.Attachments.AsNoTracking().Where(a => adjuntos.Contains(a.PublicId)).ToDictionaryAsync(a => a.PublicId, a => a.FileName, ct)
            : new Dictionary<Guid, string>();
        return filas.Select(f => new BureauQueryDto(f.PublicId, personas[f.PersonId], f.BureauCode, f.Purpose, EstadosDeConsultaACentral.De(f.Outcome),
            f.IsSimulated, f.QueriedAt, f.ValidUntil, f.Score is null && f.NegativeMarks is null ? null : new BureauQuerySummaryDto(f.Score, f.NegativeMarks),
            f.ApplicationId is { } a ? publicas.GetValueOrDefault(a) : null,
            conArtefacto && f.ReportAttachmentPublicId is { } id ? new BureauReportArtifactDto(id, nombres.GetValueOrDefault(id) ?? "informe-central") : null))
            .ToList();
    }

    /// <summary>La consulta por su <c>PublicId</c>; si es de una solicitud fuera del alcance, el mismo 404 (T62).</summary>
    public static async Task<Result<BureauQuery>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Guid publicId,
        CancellationToken ct)
    {
        var q = await db.BureauQueries.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        if (q is null) return Result.Failure<BureauQuery>(ErroresDeDecisionesYCupos.ConsultaACentralInexistente());
        if (q.ApplicationId is { } a && await db.CreditApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a, ct) is { } s)
        {
            var dentro = await alcance.AsegurarAsync(s.BranchId, s.CreditLineId, ErroresDeDecisionesYCupos.ConsultaACentralInexistente(), auditoria,
                "BureauQuery", q.PublicId, ct);
            if (dentro.IsFailure) return Result.Failure<BureauQuery>(dentro.Error);
        }
        return Result.Success(q);
    }
}

/// <summary>
/// Las consultas a centrales (api.md §4.4, <c>GET /api/lending/bureau-queries?personPublicId=&amp;applicationPublicId=&amp;page=</c>,
/// <c>Lending.CreditBureau.ReadReports</c>): con el resumen, sin el informe; las de solicitudes fuera del alcance de oficina y línea no aparecen. Las
/// más recientes primero. (nuevo)
/// </summary>
public sealed record ListBureauQueriesQuery(Guid? PersonPublicId = null, Guid? ApplicationPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<BureauQueryDto>>>;

public sealed class ListBureauQueriesQueryValidator : AbstractValidator<ListBureauQueriesQuery>;

public sealed class ListBureauQueriesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListBureauQueriesQuery, Result<PagedResult<BureauQueryDto>>>
{
    public async Task<Result<PagedResult<BureauQueryDto>>> Handle(ListBureauQueriesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var visibles = db.CreditApplications.AsNoTracking().PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var consulta = db.BureauQueries.AsNoTracking().Where(q => !q.IsDeleted && (q.ApplicationId == null || visibles.Contains(q.ApplicationId.Value)));
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(q => db.People.Any(p => p.Id == q.PersonId && p.PublicId == persona));
        if (request.ApplicationPublicId is { } solicitud)
            consulta = consulta.Where(q => db.CreditApplications.Any(a => a.Id == q.ApplicationId && a.PublicId == solicitud));

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(q => q.QueriedAt).ThenByDescending(q => q.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<BureauQueryDto>(await ConsultasACentrales.DtosAsync(db, filas, conArtefacto: false, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>
/// Una consulta con su informe (api.md §4.4, <c>GET /{id}</c>, <c>Lending.CreditBureau.ReadReports</c>): lectura sensible, deja
/// <c>Lending.CreditBureauReport.Viewed</c> por <see cref="LendingAuditEmitter"/> (T67). (nuevo)
/// </summary>
public sealed record GetBureauQueryQuery(Guid BureauQueryPublicId) : IRequest<Result<BureauQueryDto>>;

public sealed class GetBureauQueryQueryValidator : AbstractValidator<GetBureauQueryQuery>
{
    public GetBureauQueryQueryValidator() => RuleFor(x => x.BureauQueryPublicId).NotEmpty();
}

public sealed class GetBureauQueryQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetBureauQueryQuery, Result<BureauQueryDto>>
{
    public async Task<Result<BureauQueryDto>> Handle(GetBureauQueryQuery request, CancellationToken ct)
    {
        var leida = await ConsultasACentrales.LeerAsync(db, alcance, auditoria, request.BureauQueryPublicId, ct);
        if (leida.IsFailure) return Result.Failure<BureauQueryDto>(leida.Error);
        await auditoria.EmitirInformeDeCentralesVistoAsync(leida.Value.PublicId, leida.Value.BureauCode, ct);
        return Result.Success((await ConsultasACentrales.DtosAsync(db, [leida.Value], conArtefacto: true, ct))[0]);
    }
}

/// <summary>
/// El enlace firmado de 60 s al informe crudo (api.md §4.4, <c>POST /{id}/download-link</c>, <c>Lending.CreditBureau.ReadReports</c>, sin clave:
/// repetirlo emite otro, §1.5): obliga a guardar con nombre y tipo, y queda auditado como lectura del informe. Un informe del formato anterior se
/// baja por la API (<c>Direct = false</c>). (nuevo)
/// </summary>
public sealed record IssueBureauReportDownloadLinkCommand(Guid BureauQueryPublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class IssueBureauReportDownloadLinkCommandValidator : AbstractValidator<IssueBureauReportDownloadLinkCommand>
{
    public IssueBureauReportDownloadLinkCommandValidator() => RuleFor(x => x.BureauQueryPublicId).NotEmpty();
}

public sealed class IssueBureauReportDownloadLinkCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IOptions<LimitesDeAdjuntos> limites,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<IssueBureauReportDownloadLinkCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(IssueBureauReportDownloadLinkCommand request, CancellationToken ct)
    {
        var leida = await ConsultasACentrales.LeerAsync(db, alcance, auditoria, request.BureauQueryPublicId, ct);
        if (leida.IsFailure) return Result.Failure<EnlaceDeDescargaDto>(leida.Error);
        var q = leida.Value;
        var adjunto = q.ReportAttachmentPublicId is { } id ? await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == id, ct) : null;
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(new ErrorConDatos(ErroresDeDecisionesYCupos.CodigoConsultaACentralInexistente,
                "La consulta no tiene informe disponible para descargar.", new { bureauQueryPublicId = q.PublicId }));

        await auditoria.EmitirInformeDeCentralesVistoAsync(q.PublicId, q.BureauCode, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }
}
