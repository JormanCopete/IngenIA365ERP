using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Stages;

/// <summary>La regla del gasto de cobranza de una etapa (api.md §14.5 <c>feeRule</c>): base, tarifa (fracción), tope en pesos y momento. (nuevo)</summary>
public sealed record CollectionFeeRuleDto(string Base, decimal Rate, decimal? Cap, string Moment)
{
    public ReglaDeGastoDeCobranza Regla() => new() { Base = Base, Rate = Rate, Cap = Cap, Moment = Moment };

    public static CollectionFeeRuleDto? De(ReglaDeGastoDeCobranza? r) => r is null ? null : new(r.Base, r.Rate, r.Cap, r.Moment);
}

/// <summary>Una etapa de cobranza vigente (api.md §14.5). <see cref="ReviewPending"/>: es de la semilla y la cooperativa no la ha validado. (nuevo)</summary>
public sealed record CollectionStageDto(
    Guid PublicId,
    string Code,
    string Name,
    int Order,
    int DaysFrom,
    int? DaysTo,
    bool IsLegal,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<string> TemplateCodes,
    CollectionFeeRuleDto? FeeRule,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool ReviewPending);

/// <summary>
/// La versión de las etapas vigente a una fecha (api.md §14.5): su número (cuántas fechas de vigencia distintas hay hasta ella), desde cuándo
/// rige y sus etapas en orden. Sin etapas, versión 0. (nuevo)
/// </summary>
public sealed record CollectionStagesVersionDto(int Version, DateOnly? ValidFrom, IReadOnlyList<CollectionStageDto> Stages);

/// <summary>Una etapa de la versión nueva (api.md §14.5 <c>stages[]</c>). (nuevo)</summary>
public sealed record CollectionStageInput(
    string Code,
    string Name,
    int DaysFrom,
    int? DaysTo,
    bool IsLegal = false,
    IReadOnlyList<string>? SuggestedActions = null,
    IReadOnlyList<string>? TemplateCodes = null,
    CollectionFeeRuleDto? FeeRule = null);

/// <summary>Lo común de las etapas: la proyección y los nombres. (nuevo)</summary>
public static class EtapasDeCartera
{
    /// <summary>Quién crea las etapas sembradas (el <c>CreatedBy</c> de la semilla paramétrica): sus filas quedan «pendiente de validar».</summary>
    public const string CreadoPorLaSemilla = "system:seed";

    public static CollectionStageDto Dto(CollectionStage s) => new(s.PublicId, s.Code, s.Name, s.Order, s.MinDaysPastDue, s.MaxDaysPastDue, s.IsLegal,
        AccionesDeEtapa.Leer(s.SuggestedActionsJson), AccionesDeEtapa.Leer(s.TemplateCodesJson),
        CollectionFeeRuleDto.De(ReglaDeGastoDeCobranza.Leer(s.CollectionFeeRuleJson)), s.ValidFrom, s.ValidTo,
        string.Equals(s.CreatedBy, CreadoPorLaSemilla, StringComparison.Ordinal));

    /// <summary>¿La etapa abre el aviso previo del episodio? (la acción «aviso previo», <c>adaptadores-y-archivos.md</c> §5.2)</summary>
    public static bool PideAvisoPrevio(CollectionStage s) => AccionesDeEtapa.Leer(s.SuggestedActionsJson).Contains(AccionesDeEtapa.AvisoPrevio, StringComparer.Ordinal);

    /// <summary>
    /// Lo que no sirve de una versión de etapas: rangos que se cruzan o dejan huecos (<c>Lending.CollectionStage.RangesInvalid</c>), una regla de gasto
    /// inválida (<c>.FeeRuleInvalid</c>). Nulo si sirve.
    /// </summary>
    public static ProblemaDeCartera? Validar(IReadOnlyList<CollectionStage> etapas)
    {
        var rangos = EtapasDeCobranza.Validar(etapas.Select(e => e.Definicion()).ToList());
        if (rangos is not null) return rangos;
        foreach (var e in etapas)
            if (ReglaDeGastoDeCobranza.Leer(e.CollectionFeeRuleJson) is { } regla && regla.Validar(e.Code) is { } problema)
                return problema;
        return null;
    }
}

/// <summary>
/// Las etapas vigentes a una fecha (feature 013, K4, T0941; api.md §14.5, <c>GET /api/lending/collection-stages?date=</c>,
/// <c>Lending.Collections.Read</c>). Las etapas son de la cooperativa, no de un crédito: no tienen alcance de oficina. (nuevo)
/// </summary>
public sealed record ListCollectionStagesQuery(DateOnly? Date = null) : IRequest<Result<CollectionStagesVersionDto>>;

public sealed class ListCollectionStagesQueryValidator : AbstractValidator<ListCollectionStagesQuery>;

public sealed class ListCollectionStagesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListCollectionStagesQuery, Result<CollectionStagesVersionDto>>
{
    public async Task<Result<CollectionStagesVersionDto>> Handle(ListCollectionStagesQuery request, CancellationToken ct)
    {
        var dia = request.Date ?? reloj.HoyLocal;
        var vigentes = await CobranzaDeCartera.EtapasVigentesAsync(db, dia, ct);
        if (vigentes.Count == 0) return Result.Success(new CollectionStagesVersionDto(0, null, []));
        var desde = vigentes.Max(s => s.ValidFrom);
        var version = await db.CollectionStages.AsNoTracking().Where(s => !s.IsDeleted && s.ValidFrom <= desde).Select(s => s.ValidFrom).Distinct().CountAsync(ct);
        return Result.Success(new CollectionStagesVersionDto(version, desde, vigentes.Select(EtapasDeCartera.Dto).ToList()));
    }
}

/// <summary>
/// Versiona las etapas de cobranza (feature 013, K4, T0941; api.md §14.5, <c>POST /api/lending/collection-stages/versions</c>,
/// <c>Lending.CollectionStages.Manage</c>, con clave y motivo; FR-117, FR-121): la versión nueva es el juego completo de etapas desde
/// <see cref="ValidFrom"/>; cada etapa vigente se cierra la víspera (sólo cambia su <c>ValidTo</c>, Principio XI). Los rangos de días no se cruzan ni
/// dejan huecos y sólo la última va sin tope (<c>Lending.CollectionStage.RangesInvalid</c>); cada regla de gasto es una regla válida
/// (<c>.FeeRuleInvalid</c>); cada plantilla es una plantilla de cobranza de Cartera que existe (<c>Lending.MessageTemplate.NotFound</c>); la versión
/// empieza después de la vigente (<c>.ValidityInvalid</c>). <b>El techo nacional del gasto</b> no se compara: el catálogo de límites legales no trae
/// un límite del gasto de cobranza (data-model §12.5), y no se inventa (decisión pendiente del dueño). (nuevo)
/// </summary>
public sealed record AddCollectionStageVersionCommand(IReadOnlyList<CollectionStageInput> Stages, DateOnly ValidFrom, string Reason)
    : IRequest<Result<CollectionStagesVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AddCollectionStageVersionCommandValidator : ValidadorConMotivo<AddCollectionStageVersionCommand>
{
    public AddCollectionStageVersionCommandValidator()
    {
        RuleFor(x => x.Stages).NotEmpty().WithMessage("La versión lleva al menos una etapa.");
        RuleForEach(x => x.Stages).ChildRules(e =>
        {
            e.RuleFor(x => x.Code).NotEmpty().MaximumLength(CollectionStage.LargoDelCodigo)
                .Must(c => CodigoDeCatalogo.Normalizar(c) is not null)
                .WithMessage($"El código de la etapa es alfanumérico, sin espacios y de hasta {CollectionStage.LargoDelCodigo} caracteres.");
            e.RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("El nombre de la etapa es obligatorio (hasta 100 caracteres).");
            e.RuleFor(x => x.DaysTo).Must((x, hasta) => hasta is null || hasta >= x.DaysFrom)
                .WithMessage("El máximo de días de la etapa no puede ser menor que el mínimo.");
            e.RuleForEach(x => x.SuggestedActions).Must(a => AccionesDeEtapa.Todas.Contains(a, StringComparer.Ordinal))
                .WithMessage($"Las acciones sugeridas son: {string.Join(", ", AccionesDeEtapa.Todas)}.");
        });
    }
}

public sealed class AddCollectionStageVersionCommandHandler(IApplicationDbContext db, ICerrojoDeCartera cerrojo, ReglasDeParametrosDeCartera parametros)
    : IRequestHandler<AddCollectionStageVersionCommand, Result<CollectionStagesVersionDto>>
{
    public async Task<Result<CollectionStagesVersionDto>> Handle(AddCollectionStageVersionCommand request, CancellationToken ct)
    {
        var nuevas = new List<CollectionStage>();
        var orden = 0;
        foreach (var e in request.Stages.OrderBy(s => s.DaysFrom))
        {
            orden++;
            var regla = e.FeeRule?.Regla();
            nuevas.Add(CollectionStage.Nueva(CodigoDeCatalogo.Normalizar(e.Code)!, e.Name, orden, e.DaysFrom, e.DaysTo, e.IsLegal, request.ValidFrom,
                AccionesDeEtapa.Escribir(e.SuggestedActions), AccionesDeEtapa.Escribir(e.TemplateCodes?.Select(c => CodigoDeCatalogo.Normalizar(c) ?? c)),
                regla?.Serializar()));
        }
        if (EtapasDeCartera.Validar(nuevas) is { } problema) return Falla(ErroresDeGobierno.De(problema));

        // K4, bloque D (decisión del dueño del 2026-10-16): ninguna regla de gasto con una tarifa por encima del tope de la cooperativa vigente al empezar.
        if (nuevas.Any(s => s.CollectionFeeRuleJson is not null))
        {
            var tope = await Fees.GastosDeCobranza.TopeAsync(parametros, request.ValidFrom, ct);
            if (tope.IsFailure) return Falla(tope.Error);
            foreach (var s in nuevas)
                if (ReglaDeGastoDeCobranza.Leer(s.CollectionFeeRuleJson) is { } regla && tope.Value.Validar(regla, s.Code) is { } encima)
                    return Falla(ErroresDeGobierno.De(encima));
        }

        // Las plantillas: códigos de Cartera de propósito cobranza que existen en algún canal.
        var codigos = nuevas.SelectMany(s => AccionesDeEtapa.Leer(s.TemplateCodesJson)).Distinct(StringComparer.Ordinal).ToList();
        if (codigos.Count > 0)
        {
            var existentes = await db.NotificationTemplates.AsNoTracking()
                .Where(t => t.Module == ColaDeMensajesAlDeudor.Modulo && codigos.Contains(t.Code) && t.Purpose == nameof(DebtorMessagePurpose.Collection)
                            && t.IsActive && !t.IsDeleted)
                .Select(t => t.Code).Distinct().ToListAsync(ct);
            var falta = codigos.FirstOrDefault(c => !existentes.Contains(c, StringComparer.Ordinal));
            if (falta is not null) return Falla(ErroresDeMensajesAlDeudor.PlantillaInexistente(falta, null));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: true, ct);
            var abiertas = await db.CollectionStages.Where(s => !s.IsDeleted && (s.ValidTo == null || s.ValidTo >= request.ValidFrom)).ToListAsync(ct);
            if (abiertas.FirstOrDefault(s => s.ValidFrom >= request.ValidFrom) is { } posterior)
                return Falla(ErroresDeGobierno.De(ProblemaDeCartera.Con(CollectionStage.CodigoVigenciaInvalida,
                    $"Ya hay una versión de etapas desde el {posterior.ValidFrom:yyyy-MM-dd}: la nueva empieza después.", "validFrom", posterior.ValidFrom)));
            try
            {
                foreach (var s in abiertas) s.CerrarVigencia(request.ValidFrom.AddDays(-1));
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            db.CollectionStages.AddRange(nuevas);
            await db.SaveChangesAsync(ct);
            var version = await db.CollectionStages.AsNoTracking().Where(s => !s.IsDeleted && s.ValidFrom <= request.ValidFrom).Select(s => s.ValidFrom)
                .Distinct().CountAsync(ct);
            return Result.Success(new CollectionStagesVersionDto(version, request.ValidFrom, nuevas.Select(EtapasDeCartera.Dto).ToList()));
        }, ct);
    }

    private static Result<CollectionStagesVersionDto> Falla(Error e) => Result.Failure<CollectionStagesVersionDto>(e);
}
