using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Concepts;

// Feature 021 (T042; FR-002 a FR-004, FR-012, FR-032a): los conceptos que la cooperativa publica en el canal. Lo no publicado no se
// muestra; un concepto sin destino se muestra sólo para consulta. Créditos por línea de colocación; aportes por línea de aportes;
// ahorro contractual (DepositClass.Contractual) y permanente o programado (DepositClass.Permanent) por línea de captación. Los otros
// cobros (cuentas por cobrar comerciales) llegan en la entrega C2b.

public sealed record ChannelConceptDto(
    Guid PublicId,
    ConceptKind Kind,
    string? LineCode,
    string? LineName,
    Guid? DestinationPublicId,
    string? DestinationName,
    bool AllowDue,
    bool AllowPartial,
    decimal? PartialMinimum,
    bool AllowPrepayment,
    bool AllowPayoff,
    bool CanCombine,
    ChannelPrepaymentChoice? DefaultPrepaymentChoice,
    bool PublishStatement,
    bool PublishCertificate,
    bool PublishTaxCertificate);

public sealed record ListChannelConceptsQuery : IRequest<Result<IReadOnlyList<ChannelConceptDto>>>;

public sealed class ListChannelConceptsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListChannelConceptsQuery, Result<IReadOnlyList<ChannelConceptDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelConceptDto>>> Handle(ListChannelConceptsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<ChannelConceptDto>>(await Conceptos.ProyectarAsync(db, db.ChannelPublishedConcepts.AsNoTracking(), ct));
}

public static class Conceptos
{
    public const string LineaInexistente = "Channel.Concept.LineNotFound";
    public const string Duplicado = "Channel.Concept.Duplicate";
    public const string NoExiste = "Channel.Concept.NotFound";
    public const string DestinoInexistente = "Channel.Concept.DestinationNotFound";
    public const string ReglaInvalida = "Channel.Concept.Invalid";

    /// <summary>La naturaleza de línea que corresponde a cada tipo de concepto; nula si el tipo no va por línea.</summary>
    public static LineNature? NaturalezaDe(ConceptKind tipo) => tipo switch
    {
        ConceptKind.Loan => LineNature.Placement,
        ConceptKind.Contribution => LineNature.Contribution,
        ConceptKind.ContractualSaving or ConceptKind.ProgrammedSaving => LineNature.Deposit,
        _ => null,
    };

    internal static async Task<List<ChannelConceptDto>> ProyectarAsync(IApplicationDbContext db, IQueryable<ChannelPublishedConcept> consulta, CancellationToken ct)
    {
        var conceptos = await consulta.OrderBy(c => c.Kind).ThenBy(c => c.LineCode).ToListAsync(ct);
        var codigos = conceptos.Where(c => c.LineCode != null).Select(c => c.LineCode!).Distinct().ToList();
        var lineas = codigos.Count == 0
            ? new Dictionary<string, string>()
            : await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => codigos.Contains(l.Code))
                .GroupBy(l => l.Code).Select(g => new { Code = g.Key, Name = g.Select(x => x.Name).First() })
                .ToDictionaryAsync(x => x.Code, x => x.Name, ct);
        var idsDestino = conceptos.Where(c => c.DestinationId != null).Select(c => c.DestinationId!.Value).Distinct().ToList();
        var destinos = idsDestino.Count == 0
            ? new Dictionary<int, (Guid, string)>()
            : (await db.ChannelPaymentDestinations.AsNoTracking().IgnoreQueryFilters().Where(d => idsDestino.Contains(d.Id))
                .Select(d => new { d.Id, d.PublicId, d.Name }).ToListAsync(ct)).ToDictionary(d => d.Id, d => (d.PublicId, d.Name));

        return conceptos.Select(c =>
        {
            (Guid PublicId, string Name)? destino = c.DestinationId is { } id && destinos.TryGetValue(id, out var d) ? d : null;
            return new ChannelConceptDto(c.PublicId, c.Kind, c.LineCode, c.LineCode is { } l && lineas.TryGetValue(l, out var n) ? n : null,
                destino?.PublicId, destino?.Name, c.AllowDue, c.AllowPartial, c.PartialMinimum, c.AllowPrepayment, c.AllowPayoff, c.CanCombine,
                c.DefaultPrepaymentChoice, c.PublishStatement, c.PublishCertificate, c.PublishTaxCertificate);
        }).ToList();
    }
}

/// <summary>Publica un concepto o cambia uno publicado (con <paramref name="PublicId"/>).</summary>
public sealed record SaveChannelConceptCommand(
    Guid? PublicId,
    ConceptKind Kind,
    string? LineCode,
    Guid? DestinationPublicId,
    bool AllowDue,
    bool AllowPartial,
    decimal? PartialMinimum,
    bool AllowPrepayment,
    bool AllowPayoff,
    bool CanCombine,
    ChannelPrepaymentChoice? DefaultPrepaymentChoice,
    bool PublishStatement,
    bool PublishCertificate,
    bool PublishTaxCertificate) : IRequest<Result<ChannelConceptDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveChannelConceptCommandValidator : AbstractValidator<SaveChannelConceptCommand>
{
    public SaveChannelConceptCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("El tipo de concepto no es válido.");
        // Feature 021, C2b (T130): las facturas de cuentas por cobrar comerciales se publican como un solo concepto, sin línea, y se pagan por su saldo o en parte.
        RuleFor(x => x.LineCode).Empty().When(x => x.Kind == ConceptKind.CommercialReceivable)
            .WithMessage("Las facturas se publican todas juntas, sin línea.");
        RuleFor(x => x.AllowPayoff).Equal(false).When(x => x.Kind == ConceptKind.CommercialReceivable)
            .WithMessage("Una factura se paga por su saldo (exigible) o en parte; no tiene cancelación total aparte.");
        RuleFor(x => x).Must(x => !x.PublishStatement && !x.PublishCertificate && !x.PublishTaxCertificate).When(x => x.Kind == ConceptKind.CommercialReceivable)
            .WithMessage("Las facturas no tienen extractos ni certificados en el canal.");
        RuleFor(x => x.LineCode).MaximumLength(20);
        RuleFor(x => x).Must(x => x.AllowDue || x.AllowPartial || x.AllowPrepayment || x.AllowPayoff)
            .WithMessage("Habilita al menos una forma de pago: exigible, parcial, abono anticipado o pago total.");
        RuleFor(x => x.PartialMinimum).NotNull().GreaterThan(0).When(x => x.AllowPartial)
            .WithMessage("Con pago parcial, escribe el valor mínimo (mayor que cero).");
        RuleFor(x => x.AllowPrepayment).Equal(false).When(x => x.Kind != ConceptKind.Loan)
            .WithMessage("El abono anticipado sólo aplica a créditos.");
        RuleFor(x => x.DefaultPrepaymentChoice).NotNull().When(x => x.Kind == ConceptKind.Loan && x.AllowPrepayment)
            .WithMessage("Con abono anticipado, elige la opción que el asociado verá marcada: reducir plazo o reducir cuota.");
        RuleFor(x => x.DefaultPrepaymentChoice).Null().When(x => x.Kind != ConceptKind.Loan)
            .WithMessage("La opción de prepago sólo aplica a créditos.");
    }
}

public sealed class SaveChannelConceptCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SaveChannelConceptCommand, Result<ChannelConceptDto>>
{
    public async Task<Result<ChannelConceptDto>> Handle(SaveChannelConceptCommand r, CancellationToken ct)
    {
        var linea = string.IsNullOrWhiteSpace(r.LineCode) ? null : r.LineCode.Trim().ToUpperInvariant();
        if (linea is not null && Conceptos.NaturalezaDe(r.Kind) is { } naturaleza
            && !await db.CreditLines.AsNoTracking().AnyAsync(l => l.Code == linea && l.Nature == naturaleza, ct))
            return Result.Failure<ChannelConceptDto>(Conceptos.LineaInexistente, $"No hay una línea «{linea}» de ese tipo en la cooperativa.");

        int? destino = null;
        if (r.DestinationPublicId is { } d)
        {
            destino = await db.ChannelPaymentDestinations.AsNoTracking().Where(x => x.PublicId == d).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (destino is null) return Result.Failure<ChannelConceptDto>(Conceptos.DestinoInexistente, "El destino de pago no existe.");
        }

        var otro = await db.ChannelPublishedConcepts.AsNoTracking()
            .AnyAsync(c => c.Kind == r.Kind && c.LineCode == linea && c.PublicId != (r.PublicId ?? Guid.Empty), ct);
        if (otro)
            return Result.Failure<ChannelConceptDto>(Conceptos.Duplicado, "Ese concepto ya está publicado; edita el existente.");

        ChannelPublishedConcept concepto;
        if (r.PublicId is { } id)
        {
            var existente = await db.ChannelPublishedConcepts.FirstOrDefaultAsync(c => c.PublicId == id, ct);
            if (existente is null) return Result.Failure<ChannelConceptDto>(Conceptos.NoExiste, "El concepto no existe.");
            concepto = existente;
        }
        else
        {
            concepto = new ChannelPublishedConcept();
            db.ChannelPublishedConcepts.Add(concepto);
        }

        concepto.Kind = r.Kind;
        concepto.LineCode = linea;
        concepto.DestinationId = destino;
        concepto.AllowDue = r.AllowDue;
        concepto.AllowPartial = r.AllowPartial;
        concepto.PartialMinimum = r.AllowPartial ? r.PartialMinimum : null;
        concepto.AllowPrepayment = r.AllowPrepayment;
        concepto.AllowPayoff = r.AllowPayoff;
        concepto.CanCombine = r.CanCombine;
        concepto.DefaultPrepaymentChoice = r.Kind == ConceptKind.Loan && r.AllowPrepayment ? r.DefaultPrepaymentChoice : null;
        concepto.PublishStatement = r.PublishStatement;
        concepto.PublishCertificate = r.PublishCertificate;
        concepto.PublishTaxCertificate = r.PublishTaxCertificate;
        await db.SaveChangesAsync(ct);

        var dto = await Conceptos.ProyectarAsync(db, db.ChannelPublishedConcepts.AsNoTracking().Where(c => c.Id == concepto.Id), ct);
        return Result.Success(dto.Single());
    }
}

/// <summary>Retira un concepto del canal (baja lógica): deja de verse y de pagarse desde la siguiente consulta.</summary>
public sealed record UnpublishChannelConceptCommand(Guid PublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UnpublishChannelConceptCommandValidator : AbstractValidator<UnpublishChannelConceptCommand>
{
    public UnpublishChannelConceptCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class UnpublishChannelConceptCommandHandler(IApplicationDbContext db, IDateTimeService reloj, ICurrentUserService usuario)
    : IRequestHandler<UnpublishChannelConceptCommand, Result>
{
    public async Task<Result> Handle(UnpublishChannelConceptCommand r, CancellationToken ct)
    {
        var concepto = await db.ChannelPublishedConcepts.FirstOrDefaultAsync(c => c.PublicId == r.PublicId, ct);
        if (concepto is null) return Result.Failure(Conceptos.NoExiste, "El concepto no existe.");
        concepto.IsDeleted = true;
        concepto.DeletedAt = reloj.UtcNow;
        concepto.DeletedBy = usuario.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
