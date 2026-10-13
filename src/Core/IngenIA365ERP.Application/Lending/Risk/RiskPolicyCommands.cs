using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Una versión de la política SARC tal como se lista (api.md §12.1). (nuevo)</summary>
public sealed record RiskPolicyVersionDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    string Status,
    string Reason,
    string OverridesSha256,
    DateTime RegisteredAt,
    string? RegisteredBy,
    DateTime? ValidatedAt,
    string? ValidatedBy,
    Guid? SessionItemPublicId);

/// <summary>Una celda de la política combinada: ruta, norma, propuesta y lo que rige. (nuevo)</summary>
public sealed record RiskPolicyCellDto(string Path, decimal Norm, decimal Proposed, decimal Effective);

/// <summary>El documento de la norma con lo que se sella de él. (nuevo)</summary>
public sealed record RiskPolicyNormDto(Guid DocumentPublicId, string Code, DateOnly ValidFrom, string Sha256, bool Verified, IReadOnlyList<string> UnverifiedTables);

/// <summary>La norma, la política vigente y la efectiva a una fecha, con todas las versiones (api.md §12.1). (nuevo)</summary>
public sealed record RiskPolicyDto(
    DateOnly Date,
    RiskPolicyNormDto? Anexo1,
    Guid? EffectiveVersionPublicId,
    string? Overrides,
    IReadOnlyList<RiskPolicyCellDto> Combined,
    decimal GeneralImpairmentAdditional,
    bool AdditionalCompensatesIndividual,
    IReadOnlyList<RiskPolicyVersionDto> Versions);

// -------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// <c>POST /api/lending/risk/policy/versions</c> (feature 013, US9, T0700; api.md §12.1, <c>Lending.RiskPolicy.Manage</c>; data-model §23.10;
/// T45): registra una versión de la política SARC del consejo en <c>PendingValidation</c>. Reglas: rige desde hoy o después
/// (<c>Lending.RiskPolicy.RetroactiveValidFrom</c>); el contenido cumple el esquema parcial del Anexo 1 (<see cref="PoliticaSarc"/>) y cada celda
/// existe en la norma vigente en <see cref="ValidFrom"/> (<c>Lending.RiskPolicy.SchemaInvalid</c>, con la ruta de cada error); ninguna celda es
/// menos estricta que esa norma (<c>Lending.RiskPolicy.LessStrict</c>, <c>data.cells[] { path, norm, proposed }</c>). Guarda el JSON canónico, su
/// SHA-256 y la copia del Anexo 1 contra la que se comparó, y deja la intervención <c>Radicar</c> del expediente: quien registra no valida
/// (segregación, regla fija). (nuevo)
/// </summary>
public sealed record AddRiskPolicyVersionCommand(DateOnly ValidFrom, string Overrides, string Reason)
    : IRequest<Result<RiskPolicyVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AddRiskPolicyVersionCommandValidator : ValidadorConMotivo<AddRiskPolicyVersionCommand>
{
    public AddRiskPolicyVersionCommandValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige la política.");
        RuleFor(x => x.Overrides).NotEmpty().WithMessage("Indicá las celdas que la política endurece.");
    }
}

public sealed class AddRiskPolicyVersionCommandHandler(
    IApplicationDbContext db,
    INormativaVigente normativa,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<AddRiskPolicyVersionCommand, Result<RiskPolicyVersionDto>>
{
    /// <summary>La clase del expediente de la segregación de una versión de la política.</summary>
    public const string ClaseDeExpediente = LendingDecisionSubjects.RiskPolicyVersion;

    public async Task<Result<RiskPolicyVersionDto>> Handle(AddRiskPolicyVersionCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if (request.ValidFrom < hoy) return Result.Failure<RiskPolicyVersionDto>(ErroresDeRiesgo.Retroactiva(request.ValidFrom, hoy));

        var leida = PoliticaSarc.Leer(request.Overrides, PoliticaSarc.VersionDeEsquemaVigente);
        if (!leida.EsValido) return Result.Failure<RiskPolicyVersionDto>(ErroresDeRiesgo.FueraDeEsquema(leida.Errores));

        var norma = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, request.ValidFrom, Anexo1.Leer, ct: ct);
        if (norma.IsFailure) return Result.Failure<RiskPolicyVersionDto>(norma.Error);

        var combinada = PoliticaSarcEfectiva.Combinar(norma.Value.Documento, leida.Documento);
        if (combinada.Errores.Count > 0) return Result.Failure<RiskPolicyVersionDto>(ErroresDeRiesgo.FueraDeEsquema(combinada.Errores));
        if (combinada.MenosEstrictas.Count > 0) return Result.Failure<RiskPolicyVersionDto>(ErroresDeRiesgo.MenosEstricta(combinada.MenosEstrictas));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<RiskPolicyVersionDto>(ErroresDeGobierno.SinUsuario());

        var publicId = Guid.NewGuid();
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(ClaseDeExpediente, publicId), AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Result.Failure<RiskPolicyVersionDto>(verificada.Error);

        var canonico = ContenidoCanonico.Canonizar(request.Overrides);
        var numero = (await db.RiskPolicyVersions.AsNoTracking().MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;
        var version = new RiskPolicyVersion
        {
            PublicId = publicId,
            VersionNumber = numero,
            ValidFrom = request.ValidFrom,
            RegisteredAt = reloj.UtcNow,
            Overrides = canonico,
            OverridesSha256 = ContenidoCanonico.Sha256(canonico),
            BaseDocumentPublicId = norma.Value.PublicId,
            BaseDocumentSha256 = norma.Value.Sha256,
            Reason = request.Reason.Trim(),
            RegisteredByUserId = usuario,
        };
        db.RiskPolicyVersions.Add(version);
        intervenciones.Registrar(verificada.Value, ClaseDeExpediente, publicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDePoliticaSarc.DtoAsync(db, version, ct));
    }
}

// -------------------------------------------------------------------------------------------------- validar --

/// <summary>
/// <c>POST /api/lending/risk/policy/versions/{id}/validate</c> (feature 013, US9, T0700; api.md §12.1, <c>Lending.RiskPolicy.Validate</c>; C52):
/// valida la versión con el punto de acta del consejo que la aprobó —un punto <c>Decision</c> aprobado de una sesión cerrada sobre el sujeto
/// <c>RiskPolicyVersion</c> y esta versión (<c>Lending.Session.ItemMismatch</c>, <see cref="PuntoDeActaCerrado"/>)—; quien la registró no la valida
/// (<c>Lending.Segregation.Conflict</c>). La validación se escribe una sola vez (<c>[EscrituraUnica]</c>): una validada responde
/// <c>Lending.RiskPolicy.AlreadyValidated</c>. Desde <c>ValidFrom</c> rige. (nuevo)
/// </summary>
public sealed record ValidateRiskPolicyVersionCommand(Guid VersionPublicId, Guid SessionItemPublicId)
    : IRequest<Result<RiskPolicyVersionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ValidateRiskPolicyVersionCommandValidator : AbstractValidator<ValidateRiskPolicyVersionCommand>
{
    public ValidateRiskPolicyVersionCommandValidator()
    {
        RuleFor(x => x.VersionPublicId).NotEmpty();
        RuleFor(x => x.SessionItemPublicId).NotEmpty().WithMessage("Indicá el punto del acta del consejo que aprobó la política.");
    }
}

public sealed class ValidateRiskPolicyVersionCommandHandler(
    IApplicationDbContext db,
    PuntoDeActaCerrado puntoDeActa,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ValidateRiskPolicyVersionCommand, Result<RiskPolicyVersionDto>>
{
    public async Task<Result<RiskPolicyVersionDto>> Handle(ValidateRiskPolicyVersionCommand request, CancellationToken ct)
    {
        var version = await db.RiskPolicyVersions.FirstOrDefaultAsync(v => v.PublicId == request.VersionPublicId && !v.IsDeleted, ct);
        if (version is null) return Result.Failure<RiskPolicyVersionDto>(ErroresDeRiesgo.PoliticaInexistente());
        if (version.ValidatedAt is not null) return Result.Failure<RiskPolicyVersionDto>(ErroresDeRiesgo.PoliticaYaValidada(version.VersionNumber));

        var punto = await puntoDeActa.ValidarAsync(request.SessionItemPublicId, LendingDecisionSubjects.RiskPolicyVersion, version.PublicId, ct);
        if (punto.IsFailure) return Result.Failure<RiskPolicyVersionDto>(punto.Error);

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<RiskPolicyVersionDto>(ErroresDeGobierno.SinUsuario());
        var expediente = new ExpedienteDeCartera(AddRiskPolicyVersionCommandHandler.ClaseDeExpediente, version.PublicId);
        var verificada = await intervenciones.VerificarAsync(expediente, AccionesDeSegregacion.Decidir, reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<RiskPolicyVersionDto>(verificada.Error);

        version.ValidatedAt = reloj.UtcNow;
        version.ValidatedByUserId = usuario;
        version.SessionItemId = punto.Value.ItemId;
        intervenciones.Registrar(verificada.Value, AddRiskPolicyVersionCommandHandler.ClaseDeExpediente, version.PublicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDePoliticaSarc.DtoAsync(db, version, ct));
    }
}

// ---------------------------------------------------------------------------------------------------- consulta --

/// <summary>
/// <c>GET /api/lending/risk/policy?date=</c> (feature 013, US9, T0700; api.md §12.1, <c>Lending.Parameters.View</c>): la norma vigente a la fecha
/// con su huella y sus tablas sin cotejar, la política validada que rige y la combinación celda por celda (<see cref="PoliticaSarcEfectiva"/>),
/// más todas las versiones con su estado calculado. No trae datos de deudores. (nuevo)
/// </summary>
public sealed record GetRiskPolicyQuery(DateOnly? Date = null) : IRequest<Result<RiskPolicyDto>>;

public sealed class GetRiskPolicyQueryHandler(IApplicationDbContext db, INormativaVigente normativa, IDateTimeService reloj)
    : IRequestHandler<GetRiskPolicyQuery, Result<RiskPolicyDto>>
{
    public async Task<Result<RiskPolicyDto>> Handle(GetRiskPolicyQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var versiones = await db.RiskPolicyVersions.AsNoTracking().Where(v => !v.IsDeleted).OrderByDescending(v => v.VersionNumber).ToListAsync(ct);
        var dtos = new List<RiskPolicyVersionDto>(versiones.Count);
        foreach (var v in versiones) dtos.Add(await ConsultasDePoliticaSarc.DtoAsync(db, v, ct));

        var norma = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, fecha, Anexo1.Leer, ct: ct);
        var vigente = await new PoliticaSarcVigente(db).LeerAsync(fecha, null, ct);
        if (norma.IsFailure)
            return Result.Success(new RiskPolicyDto(fecha, null, vigente?.Version.PublicId, vigente?.Version.Overrides, [], 0m, false, dtos));

        var efectiva = PoliticaSarcEfectiva.Combinar(norma.Value.Documento, vigente?.Politica);
        var doc = norma.Value;
        return Result.Success(new RiskPolicyDto(fecha,
            new RiskPolicyNormDto(doc.PublicId, doc.Codigo, doc.ValidFrom, doc.Sha256, doc.Verified, doc.Documento.TablasSinCotejar),
            vigente?.Version.PublicId, vigente?.Version.Overrides,
            efectiva.Celdas.Select(c => new RiskPolicyCellDto(c.Ruta, c.Norma, c.Propuesta, c.Efectiva)).ToList(),
            efectiva.DeterioroGeneralAdicional, efectiva.AdicionalCompensaIndividual, dtos));
    }
}

/// <summary>Lo que comparten los comandos y la consulta de la política. (nuevo)</summary>
public static class ConsultasDePoliticaSarc
{
    public static async Task<RiskPolicyVersionDto> DtoAsync(IApplicationDbContext db, RiskPolicyVersion v, CancellationToken ct)
    {
        var usuarios = new[] { v.RegisteredByUserId, v.ValidatedByUserId ?? 0 };
        var nombres = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarios.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        Guid? punto = v.SessionItemId is { } id
            ? await db.SessionItems.AsNoTracking().Where(i => i.Id == id).Select(i => (Guid?)i.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return new RiskPolicyVersionDto(v.PublicId, v.VersionNumber, v.ValidFrom, v.Status.ToString(), v.Reason, v.OverridesSha256, v.RegisteredAt,
            nombres.GetValueOrDefault(v.RegisteredByUserId), v.ValidatedAt, v.ValidatedByUserId is { } va ? nombres.GetValueOrDefault(va) : null, punto);
    }

    /// <summary>El contenido de la política, leído para mostrarlo (la API lo devuelve como JSON).</summary>
    public static JsonElement Contenido(string overrides)
    {
        using var doc = JsonDocument.Parse(overrides);
        return doc.RootElement.Clone();
    }
}
