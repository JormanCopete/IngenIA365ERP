using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Attributions;

// Feature 013, T0293 (US17; T63, R132; api.md §3.5.1; data-model §3.1–§3.3): la matriz de atribuciones —publicar una versión completa,
// listarla, leerla y la vista previa «¿a qué nivel iría?»—. (nuevo)

/// <summary>Un órgano nombrado por un nivel colegiado (sólo código y nombre).</summary>
public sealed record GoverningBodyRefDto(Guid PublicId, string Code, string Name);

/// <summary>Un nivel de la matriz (api.md §3.5.1; <c>order</c> es el rango: mayor = más alto).</summary>
public sealed record AttributionLevelDto(
    string Code,
    string Name,
    int Order,
    AttributionLevelKind Kind,
    string? PermissionCode,
    GoverningBodyRefDto? GoverningBody,
    decimal? ConsolidatedAmountCeiling,
    bool IsExceptionLevel,
    bool IsBoardLevel);

/// <summary>Las condiciones de una regla; una condición nula no importa.</summary>
public sealed record AttributionRuleConditionsDto(
    Guid? CreditLinePublicId = null,
    string? ModalityCode = null,
    decimal? MinConsolidatedAmount = null,
    decimal? MaxConsolidatedAmount = null,
    int? MinTermMonths = null,
    int? MaxTermMonths = null,
    CollateralKind? CollateralKind = null,
    bool? ScoreBelowMinimum = null,
    bool? HasWarnings = null,
    bool? PolicyException = null,
    int? MinDaysPastDue = null,
    int? MaxDaysPastDue = null,
    LoanConcept? Concept = null);

/// <summary>Una regla de la matriz: sujeto, condiciones y el nivel mínimo que exige (por código).</summary>
public sealed record AttributionRuleDto(int Order, string Subject, AttributionRuleConditionsDto Conditions, string MinimumLevelCode);

/// <summary>Una versión de la matriz (api.md §3.5.1). <see cref="ValidTo"/> es la víspera de la versión siguiente, si la hay.</summary>
public sealed record AttributionMatrixDto(
    Guid PublicId,
    int Version,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? Source,
    string ContentSha256,
    DateTime RegisteredAt,
    IReadOnlyList<AttributionLevelDto> Levels,
    IReadOnlyList<AttributionRuleDto> Rules);

/// <summary>Un nivel que se publica. <see cref="Order"/> es el rango; el colegiado nombra su órgano por <c>PublicId</c>.</summary>
public sealed record AttributionLevelInput(
    string Code,
    string Name,
    int Order,
    AttributionLevelKind Kind,
    string? PermissionCode = null,
    Guid? GoverningBodyPublicId = null,
    decimal? ConsolidatedAmountCeiling = null,
    bool IsExceptionLevel = false,
    bool IsBoardLevel = false);

/// <summary>Una regla que se publica.</summary>
public sealed record AttributionRuleInput(int Order, string Subject, string MinimumLevelCode, AttributionRuleConditionsDto? Conditions = null);

/// <summary>
/// Publica una versión <b>completa</b> de la matriz de atribuciones (api.md §3.5.1, <c>POST /api/lending/attribution-matrices</c>,
/// <c>Lending.Attributions.Manage</c>). La anterior queda vigente hasta la víspera de <see cref="ValidFrom"/> (lectura bitemporal, sin
/// reescribirla); lo ya enrutado conserva la versión sellada. (nuevo)
/// </summary>
public sealed record AddAttributionMatrixVersionCommand(
    DateOnly ValidFrom,
    string Reason,
    string? Source,
    IReadOnlyList<AttributionLevelInput> Levels,
    IReadOnlyList<AttributionRuleInput> Rules) : IRequest<Result<AttributionMatrixDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AddAttributionMatrixVersionCommandValidator : ValidadorConMotivo<AddAttributionMatrixVersionCommand>
{
    public AddAttributionMatrixVersionCommandValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige la matriz.");
        RuleFor(x => x.Source).MaximumLength(300).WithMessage("La fuente admite hasta 300 caracteres.");
        RuleFor(x => x.Levels).NotNull().WithMessage("La matriz lleva sus niveles.");
        RuleFor(x => x.Rules).NotNull().WithMessage("La matriz lleva su lista de reglas (puede ir vacía).");
        RuleForEach(x => x.Levels).ChildRules(n =>
        {
            n.RuleFor(l => l.Code).NotEmpty().MaximumLength(10).Matches(CodigoDeCatalogo.Patron)
                .WithMessage("El código del nivel lleva hasta 10 letras o dígitos, sin espacios.");
            n.RuleFor(l => l.Name).NotEmpty().MaximumLength(80).WithMessage("El nombre del nivel lleva hasta 80 caracteres.");
            n.RuleFor(l => l.Kind).IsInEnum();
            n.RuleFor(l => l.ConsolidatedAmountCeiling!.Value).PrecisionScale(18, 2, true)
                .WithMessage("El techo admite hasta 2 decimales.").When(l => l.ConsolidatedAmountCeiling is not null);
        });
        RuleForEach(x => x.Rules).ChildRules(r =>
        {
            r.RuleFor(x => x.Subject).NotEmpty().MaximumLength(40);
            r.RuleFor(x => x.MinimumLevelCode).NotEmpty().MaximumLength(10);
        });
    }
}

/// <summary>
/// Resuelve los órganos y las líneas por su <c>PublicId</c> (inexistente: <c>Lending.GoverningBody.NotFound</c>,
/// <c>Lending.CreditLine.NotFound</c>), valida la matriz entera con <see cref="AttributionMatrix.Validar"/> contra <c>HoyLocal</c>
/// (<c>MatrixWithoutTopLevel</c>, <c>MatrixInvalid</c> con <c>data.reason</c>, <c>LevelUnknown</c>, órgano que no decide crédito) y la
/// guarda matriz → niveles → reglas en una sola <see cref="TransaccionExplicita"/> (las tablas no tienen navegaciones), con
/// <c>ContentSha256</c> sellado.
/// </summary>
public sealed class AddAttributionMatrixVersionCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<AddAttributionMatrixVersionCommand, Result<AttributionMatrixDto>>
{
    public async Task<Result<AttributionMatrixDto>> Handle(AddAttributionMatrixVersionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AttributionMatrixDto>(ErroresDeGobierno.SinUsuario());

        var publicIdsDeOrgano = request.Levels.Where(l => l.GoverningBodyPublicId is not null).Select(l => l.GoverningBodyPublicId!.Value).Distinct().ToList();
        var organos = await db.GoverningBodies.AsNoTracking().Where(b => publicIdsDeOrgano.Contains(b.PublicId) && !b.IsDeleted).ToListAsync(ct);
        var faltante = request.Levels.FirstOrDefault(l => l.GoverningBodyPublicId is { } p && organos.All(o => o.PublicId != p));
        if (faltante is not null) return Result.Failure<AttributionMatrixDto>(ErroresDeGobierno.OrganoDeNivelInvalido(faltante.Code));

        var publicIdsDeLinea = request.Rules.Where(r => r.Conditions?.CreditLinePublicId is not null)
            .Select(r => r.Conditions!.CreditLinePublicId!.Value).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().Where(l => publicIdsDeLinea.Contains(l.PublicId) && !l.IsDeleted)
            .ToDictionaryAsync(l => l.PublicId, l => l.Id, ct);
        if (publicIdsDeLinea.Any(p => !lineas.ContainsKey(p))) return Result.Failure<AttributionMatrixDto>(ErroresDeGobierno.LineaInexistente());

        var idDeOrgano = organos.ToDictionary(o => o.PublicId, o => o.Id);
        var borrador = new BorradorDeMatriz(request.ValidFrom, request.Reason.Trim(), request.Source,
            request.Levels.Select(l => new BorradorDeNivel(l.Code.Trim().ToUpperInvariant(), l.Name, l.Order, l.Kind, NuloSiVacio(l.PermissionCode),
                l.GoverningBodyPublicId is { } p ? idDeOrgano[p] : null, l.ConsolidatedAmountCeiling, l.IsExceptionLevel, l.IsBoardLevel)).ToList(),
            request.Rules.Select(r => Regla(r, lineas)).ToList());

        var paraValidar = organos.ToDictionary(o => o.Id, o => new OrganoDeMatriz(o.Kind, o.DecidesCredit, o.IsActive));
        if (AttributionMatrix.Validar(borrador, reloj.HoyLocal, paraValidar) is { } problema)
            return Result.Failure<AttributionMatrixDto>(ErroresDeGobierno.De(problema));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ultima = await db.AttributionMatrices.IgnoreQueryFilters().MaxAsync(m => (int?)m.VersionNumber, ct) ?? 0;
            var publicada = AttributionMatrix.Publicar(borrador, ultima + 1, reloj.HoyLocal, reloj.UtcNow, usuario, paraValidar);
            db.AttributionMatrices.Add(publicada.Matriz);
            await db.SaveChangesAsync(ct);

            var niveles = publicada.Niveles(publicada.Matriz.Id);
            db.AttributionLevels.AddRange(niveles);
            await db.SaveChangesAsync(ct);

            db.AttributionRules.AddRange(publicada.Reglas(publicada.Matriz.Id, niveles.ToDictionary(n => n.Code, n => n.Id)));
            await db.SaveChangesAsync(ct);

            return Result.Success(await ConsultasDeMatrices.DtoAsync(db, publicada.Matriz, ct));
        }, ct);
    }

    private static BorradorDeRegla Regla(AttributionRuleInput r, IReadOnlyDictionary<Guid, int> lineas)
    {
        var c = r.Conditions ?? new AttributionRuleConditionsDto();
        return new BorradorDeRegla(r.Order, r.Subject.Trim(), r.MinimumLevelCode.Trim().ToUpperInvariant())
        {
            CreditLineId = c.CreditLinePublicId is { } p ? lineas[p] : null,
            ModalityCode = NuloSiVacio(c.ModalityCode),
            MinConsolidatedAmount = c.MinConsolidatedAmount,
            MaxConsolidatedAmount = c.MaxConsolidatedAmount,
            MinTermMonths = c.MinTermMonths,
            MaxTermMonths = c.MaxTermMonths,
            CollateralKind = c.CollateralKind,
            ScoreBelowMinimum = c.ScoreBelowMinimum,
            HasWarnings = c.HasWarnings,
            PolicyException = c.PolicyException,
            MinDaysPastDue = c.MinDaysPastDue,
            MaxDaysPastDue = c.MaxDaysPastDue,
            Concept = c.Concept,
        };
    }

    private static string? NuloSiVacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// Las versiones de la matriz (api.md §3.5.1, <c>GET /api/lending/attribution-matrices?date=&amp;includeHistory=</c>,
/// <c>Lending.Attributions.View</c>): sin historia, la vigente en <see cref="Date"/> (hoy local si no viene) más las que empiezan
/// después; con historia, todas, de la más nueva a la más vieja. Catálogo sin oficina: no aplica el alcance (lo exceptúa
/// <c>LasConsultasDeCarteraRespetanElAlcance</c>). (nuevo)
/// </summary>
public sealed record ListAttributionMatricesQuery(DateOnly? Date = null, bool IncludeHistory = false) : IRequest<Result<IReadOnlyList<AttributionMatrixDto>>>;

public sealed class ListAttributionMatricesQueryValidator : AbstractValidator<ListAttributionMatricesQuery>;

public sealed class ListAttributionMatricesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListAttributionMatricesQuery, Result<IReadOnlyList<AttributionMatrixDto>>>
{
    public async Task<Result<IReadOnlyList<AttributionMatrixDto>>> Handle(ListAttributionMatricesQuery request, CancellationToken ct)
    {
        var versiones = await db.AttributionMatrices.AsNoTracking().Where(m => !m.IsDeleted).ToListAsync(ct);
        IEnumerable<AttributionMatrix> elegidas = versiones;
        if (!request.IncludeHistory)
        {
            var fecha = request.Date ?? reloj.HoyLocal;
            var vigente = Domain.Common.VigenciaBitemporal.VigenteDeCartera(versiones, fecha, reloj.UtcNow);
            elegidas = versiones.Where(v => v == vigente || v.ValidFrom > fecha);
        }

        var lista = new List<AttributionMatrixDto>();
        foreach (var m in elegidas.OrderByDescending(m => m.ValidFrom).ThenByDescending(m => m.VersionNumber))
            lista.Add(await ConsultasDeMatrices.DtoAsync(db, m, ct, versiones));
        return Result.Success<IReadOnlyList<AttributionMatrixDto>>(lista);
    }
}

/// <summary>Una versión de la matriz por su <c>PublicId</c> (api.md §3.5.1, <c>GET /{id}</c>; <c>Lending.AttributionMatrix.NotFound</c>). (nuevo)</summary>
public sealed record GetAttributionMatrixQuery(Guid MatrixPublicId) : IRequest<Result<AttributionMatrixDto>>;

public sealed class GetAttributionMatrixQueryValidator : AbstractValidator<GetAttributionMatrixQuery>
{
    public GetAttributionMatrixQueryValidator() => RuleFor(x => x.MatrixPublicId).NotEmpty();
}

public sealed class GetAttributionMatrixQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAttributionMatrixQuery, Result<AttributionMatrixDto>>
{
    public async Task<Result<AttributionMatrixDto>> Handle(GetAttributionMatrixQuery request, CancellationToken ct)
    {
        var matriz = await db.AttributionMatrices.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.MatrixPublicId && !m.IsDeleted, ct);
        return matriz is null
            ? Result.Failure<AttributionMatrixDto>(ErroresDeGobierno.MatrizInexistente())
            : Result.Success(await ConsultasDeMatrices.DtoAsync(db, matriz, ct));
    }
}

/// <summary>Una razón del enrutamiento (api.md §3.5.1): <c>AmountCeiling</c>, <c>Rule</c>, <c>RelatedParty</c> o <c>Exception</c>.</summary>
public sealed record RoutingReasonDto(RazonDeEnrutamiento Kind, string LevelCode, string Detail);

/// <summary>La respuesta de la vista previa (api.md §3.5.1).</summary>
public sealed record AttributionRoutingPreviewDto(
    AttributionLevelDto EffectiveLevel,
    IReadOnlyList<RoutingReasonDto> Reasons,
    decimal ConsolidatedAmount,
    IReadOnlyList<MotivoNoExceptuable> NonExceptionable,
    int MatrixVersion);

/// <summary>
/// La vista previa «¿a qué nivel iría?» (api.md §3.5.1, <c>POST /api/lending/attribution-matrices/preview</c>, consulta sin clave,
/// <c>Lending.Attributions.View</c>): enruta con la matriz vigente en <see cref="Date"/> (hoy local si no viene). El monto recibido es el
/// <b>consolidado</b>: US2 le suma la exposición del deudor. Con línea, la modalidad y el puntaje mínimo salen de su versión vigente
/// (puntaje bajo el mínimo = excepción, FR-037); con persona, un vínculo vigente la lleva al consejo (FR-040) y, desde US2 (T0395), al monto se le
/// suma su exposición consolidada (<c>ExposicionConsolidadaDelDeudor</c>: como deudor y codeudor, reservas y aprobadas), como en la solicitud.
/// El plazo en cuotas se toma en meses. No escribe nada. (nuevo)
/// </summary>
public sealed record PreviewAttributionRoutingQuery(
    string Subject,
    decimal Amount,
    Guid? CreditLinePublicId = null,
    Guid? PersonPublicId = null,
    int? TermInstallments = null,
    IReadOnlyList<CollateralKind>? CollateralKinds = null,
    decimal? Score = null,
    IReadOnlyList<string>? Alerts = null,
    int? DaysPastDue = null,
    LoanConcept? Concept = null,
    bool PolicyException = false,
    DateOnly? Date = null) : IRequest<Result<AttributionRoutingPreviewDto>>;

public sealed class PreviewAttributionRoutingQueryValidator : AbstractValidator<PreviewAttributionRoutingQuery>
{
    public PreviewAttributionRoutingQueryValidator()
    {
        RuleFor(x => x.Subject).Must(LendingDecisionSubjects.Existe).WithMessage("Elegí un sujeto de decisión de Cartera.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true).WithMessage("El monto admite hasta 2 decimales y no es negativo.");
        RuleFor(x => x.TermInstallments).GreaterThan(0).When(x => x.TermInstallments is not null);
        RuleFor(x => x.DaysPastDue).GreaterThanOrEqualTo(0).When(x => x.DaysPastDue is not null);
    }
}

public sealed class PreviewAttributionRoutingQueryHandler(
    IApplicationDbContext db,
    MatrizVigenteDeAtribuciones matrices,
    VinculadosVigentes vinculados,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    Origination.ExposicionConsolidadaDelDeudor? exposicion = null) : IRequestHandler<PreviewAttributionRoutingQuery, Result<AttributionRoutingPreviewDto>>
{
    public async Task<Result<AttributionRoutingPreviewDto>> Handle(PreviewAttributionRoutingQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var matriz = await matrices.LeerAsync(fecha, reloj.UtcNow, ct);
        if (matriz is null) return Result.Failure<AttributionRoutingPreviewDto>(ErroresDeGobierno.SinMatrizVigente(fecha));

        string? linea = null;
        string? modalidad = null;
        var puntajeBajo = false;
        if (request.CreditLinePublicId is { } lineaPublica)
        {
            var credito = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == lineaPublica && !l.IsDeleted, ct);
            if (credito is null) return Result.Failure<AttributionRoutingPreviewDto>(ErroresDeGobierno.LineaInexistente());
            // §1.3: una línea fuera del alcance es inexistente.
            var dentro = await alcanceDeLaPeticion.AsegurarAsync(null, credito.Id, ErroresDeGobierno.LineaInexistente(), auditoria, "CreditLine",
                credito.PublicId, ct);
            if (dentro.IsFailure) return Result.Failure<AttributionRoutingPreviewDto>(dentro.Error);

            linea = credito.Code;
            var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == credito.Id && !v.IsDeleted).ToListAsync(ct);
            var version = Domain.Common.VigenciaBitemporal.VigenteDeCartera(versiones, fecha, reloj.UtcNow);
            modalidad = version?.ModalityCode;
            puntajeBajo = request.Score is { } puntaje && version?.MinimumScore is { } minimo && puntaje < minimo;
        }

        var hayVinculado = false;
        var consolidado = request.Amount;
        if (request.PersonPublicId is { } personaPublica)
        {
            var persona = await db.People.AsNoTracking().Where(p => p.PublicId == personaPublica && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (persona is null) return Result.Failure<AttributionRoutingPreviewDto>(ErroresDeGobierno.PersonaInexistente());
            hayVinculado = (await vinculados.DeAsync([persona.Value], fecha, ct)).Count > 0;
            consolidado += (await (exposicion ?? new Origination.ExposicionConsolidadaDelDeudor(db)).DeAsync(persona.Value, fecha, null, ct)).Total;
        }

        var enrutado = EnrutadorDeAtribuciones.Enrutar(new SujetoAEnrutar(request.Subject, consolidado)
        {
            Linea = linea,
            Modalidad = modalidad,
            PlazoMeses = request.TermInstallments,
            ClasesDeGarantia = request.CollateralKinds ?? [],
            PuntajeBajoElMinimo = puntajeBajo,
            ConAlertas = request.Alerts is { Count: > 0 },
            ExcepcionALaPolitica = request.PolicyException,
            DiasDeMora = request.DaysPastDue,
            Concepto = request.Concept,
            HayVinculado = hayVinculado,
        }, matriz.ParaElMotor);

        var niveles = await ConsultasDeMatrices.NivelesAsync(db, matriz.Niveles, ct);
        return Result.Success(new AttributionRoutingPreviewDto(
            niveles.Single(n => n.Code == enrutado.NivelEfectivo.Codigo),
            enrutado.Razones.Select(r => new RoutingReasonDto(r.Clase, r.Nivel.Codigo, r.Detalle)).ToList(),
            consolidado,
            enrutado.NoExceptuables,
            matriz.Matriz.VersionNumber));
    }
}

/// <summary>Lo que comparten las consultas y el comando de la matriz. (nuevo)</summary>
internal static class ConsultasDeMatrices
{
    public static async Task<AttributionMatrixDto> DtoAsync(IApplicationDbContext db, AttributionMatrix m, CancellationToken ct,
        IReadOnlyList<AttributionMatrix>? versiones = null)
    {
        versiones ??= await db.AttributionMatrices.AsNoTracking().Where(v => !v.IsDeleted).ToListAsync(ct);
        var niveles = await db.AttributionLevels.AsNoTracking().Where(n => n.MatrixId == m.Id).OrderBy(n => n.Rank).ToListAsync(ct);
        var reglas = await db.AttributionRules.AsNoTracking().Where(r => r.MatrixId == m.Id).OrderBy(r => r.Order).ToListAsync(ct);
        var idsDeLinea = reglas.Where(r => r.CreditLineId is not null).Select(r => r.CreditLineId!.Value).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.PublicId, ct);
        var codigoDeNivel = niveles.ToDictionary(n => n.Id, n => n.Code);

        return new AttributionMatrixDto(m.PublicId, m.VersionNumber, m.ValidFrom, MatrizVigenteDeAtribuciones.VigenteHasta(m, versiones), m.Reason,
            m.LegalSource, m.ContentSha256, m.RegisteredAt,
            await NivelesAsync(db, niveles, ct),
            reglas.Select(r => new AttributionRuleDto(r.Order, r.Subject, new AttributionRuleConditionsDto(
                r.CreditLineId is { } l ? lineas[l] : null, r.ModalityCode, r.MinConsolidatedAmount, r.MaxConsolidatedAmount, r.MinTermMonths,
                r.MaxTermMonths, r.CollateralKind, r.ScoreBelowMinimum, r.HasWarnings, r.PolicyException, r.MinDaysPastDue, r.MaxDaysPastDue, r.Concept),
                codigoDeNivel[r.MinimumLevelId])).ToList());
    }

    public static async Task<IReadOnlyList<AttributionLevelDto>> NivelesAsync(IApplicationDbContext db, IReadOnlyList<AttributionLevel> niveles, CancellationToken ct)
    {
        var ids = niveles.Where(n => n.GoverningBodyId is not null).Select(n => n.GoverningBodyId!.Value).Distinct().ToList();
        var organos = await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new GoverningBodyRefDto(b.PublicId, b.Code, b.Name), ct);
        return niveles.OrderBy(n => n.Rank)
            .Select(n => new AttributionLevelDto(n.Code, n.Name, n.Rank, n.Kind, n.PermissionCode,
                n.GoverningBodyId is { } b ? organos.GetValueOrDefault(b) : null, n.MaxConsolidatedAmount, n.IsExceptionLevel, n.IsBoardLevel))
            .ToList();
    }
}
