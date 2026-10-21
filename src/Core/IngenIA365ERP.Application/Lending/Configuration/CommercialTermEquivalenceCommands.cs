using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Configuration;

// Feature 013, US1 (T0355; api.md §3.2; data-model §1.9; FR-022, FR-050, T17): las equivalencias de Comercial traducen a Cartera la
// periodicidad y la unidad de plazo que llegan como texto en la venta a crédito. Son de la cooperativa, no de una línea, y se versionan
// como todo hecho de Cartera (VigenciaBitemporal). Una periodicidad sin equivalencia no es error de esta ruta: hace no convertible el
// mensaje de venta (T17), que se reprocesa cuando la fila existe. (nuevo)

/// <summary>Una equivalencia que se registra: clase, el texto de Comercial y su traducción (periodicidad o días por unidad).</summary>
public sealed record CommercialTermEquivalenceInput(CommercialTermKind Kind, string CommercialCode, RepaymentPeriodicity? Periodicity = null,
    int? DaysPerUnit = null);

/// <summary>Una equivalencia vigente (api.md §3.2).</summary>
public sealed record CommercialTermEquivalenceDto(
    Guid PublicId,
    CommercialTermKind Kind,
    string CommercialCode,
    RepaymentPeriodicity? Periodicity,
    int? DaysPerUnit,
    DateOnly ValidFrom,
    int VersionNumber,
    DateTime RegisteredAt);

/// <summary>
/// El único que registra equivalencias de Comercial (feature 013, US1, T0355): lo usan el comando y la hoja <c>Equivalencias</c> de la
/// plantilla <c>lending.credit-lines</c>. Valida el lote entero (<c>Duplicate</c>, <c>PeriodicityUnknown</c>) y agrega una versión por clase
/// y texto, con el número siguiente; no guarda. (nuevo)
/// </summary>
public static class RegistroDeEquivalencias
{
    /// <summary>Lo que está mal en el lote, o nulo.</summary>
    public static ProblemaDeCartera? Validar(IReadOnlyList<CommercialTermEquivalenceInput> items)
    {
        if (CommercialTermEquivalence.ValidarLote(items.Select(i => (i.Kind, i.CommercialCode ?? string.Empty))) is { } duplicado) return duplicado;
        foreach (var i in items)
            if (CommercialTermEquivalence.Validar(i.Kind, i.CommercialCode ?? string.Empty, i.Periodicity, i.DaysPerUnit) is { } problema) return problema;
        return null;
    }

    /// <summary>Agrega al contexto una versión por equivalencia del lote (ya validado). Devuelve las nuevas.</summary>
    public static async Task<IReadOnlyList<CommercialTermEquivalence>> AgregarAsync(IApplicationDbContext db,
        IReadOnlyList<CommercialTermEquivalenceInput> items, DateOnly validFrom, DateTime ahora, CancellationToken ct)
    {
        var existentes = await db.CommercialTermEquivalences.IgnoreQueryFilters().AsNoTracking()
            .Select(e => new { e.Kind, e.CommercialCode, e.VersionNumber }).ToListAsync(ct);
        var ultimos = existentes.GroupBy(e => (e.Kind, e.CommercialCode)).ToDictionary(g => g.Key, g => g.Max(e => e.VersionNumber));
        var nuevas = items.Select(i =>
        {
            var codigo = CommercialTermEquivalence.Normalizar(i.CommercialCode);
            var numero = (ultimos.TryGetValue((i.Kind, codigo), out var u) ? u : 0) + 1;
            return CommercialTermEquivalence.Nueva(i.Kind, codigo, i.Periodicity, i.DaysPerUnit, validFrom, numero, ahora);
        }).ToList();
        db.CommercialTermEquivalences.AddRange(nuevas);
        return nuevas;
    }

    public static CommercialTermEquivalenceDto Dto(CommercialTermEquivalence e) =>
        new(e.PublicId, e.Kind, e.CommercialCode, e.Periodicity, e.DaysPerUnit, e.ValidFrom, e.VersionNumber, e.RegisteredAt);
}

/// <summary>
/// El lector de las equivalencias de Comercial a la fecha de la venta (feature 013, US1, T0355; FR-051, T17): por clase y texto, la
/// versión que rige con <see cref="VigenciaBitemporal.VigenteDeCartera{T}"/>. Lo usa el consumidor de ventas de US6; un texto sin
/// equivalencia vigente devuelve nulo y el mensaje queda no convertible (<c>Lending.Sale.PeriodicityWithoutEquivalence</c>). (nuevo)
/// </summary>
public sealed class EquivalenciasDeComercio(IApplicationDbContext db)
{
    /// <summary>Todas las vigentes a la fecha, por clase y texto.</summary>
    public async Task<IReadOnlyList<CommercialTermEquivalence>> VigentesAsync(DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        var todas = await db.CommercialTermEquivalences.AsNoTracking().Where(e => !e.IsDeleted && e.ValidFrom <= fecha).ToListAsync(ct);
        return todas.GroupBy(e => (e.Kind, e.CommercialCode))
            .Select(g => VigenciaBitemporal.VigenteDeCartera(g, fecha, informacionHasta ?? DateTime.MaxValue))
            .OfType<CommercialTermEquivalence>()
            .OrderBy(e => e.Kind).ThenBy(e => e.CommercialCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>La periodicidad de Cartera que corresponde al texto de Comercial a la fecha de la venta; nula si no hay equivalencia.</summary>
    public async Task<RepaymentPeriodicity?> PeriodicidadAsync(string periodicidadComercial, DateOnly fechaDeVenta, CancellationToken ct = default) =>
        (await LeerAsync(CommercialTermKind.Periodicity, periodicidadComercial, fechaDeVenta, ct))?.Periodicity;

    /// <summary>Los días de una unidad de plazo de Comercial a la fecha de la venta; nulos si no hay equivalencia.</summary>
    public async Task<int?> DiasPorUnidadAsync(string unidadComercial, DateOnly fechaDeVenta, CancellationToken ct = default) =>
        (await LeerAsync(CommercialTermKind.TermUnit, unidadComercial, fechaDeVenta, ct))?.DaysPerUnit;

    private async Task<CommercialTermEquivalence?> LeerAsync(CommercialTermKind clase, string texto, DateOnly fecha, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var codigo = CommercialTermEquivalence.Normalizar(texto);
        var versiones = await db.CommercialTermEquivalences.AsNoTracking()
            .Where(e => e.Kind == clase && e.CommercialCode == codigo && !e.IsDeleted).ToListAsync(ct);
        return VigenciaBitemporal.VigenteDeCartera(versiones, fecha, DateTime.MaxValue);
    }
}

/// <summary>
/// Registra versiones de equivalencias de Comercial (api.md §3.2, <c>PUT /api/lending/commercial-term-equivalences</c>,
/// <c>Lending.CreditLines.Manage</c>) y devuelve el arreglo vigente. Puede regir desde antes de hoy: una venta rechazada por falta de
/// equivalencia se reprocesa a su fecha (T17). (nuevo)
/// </summary>
public sealed record SetCommercialTermEquivalencesCommand(IReadOnlyList<CommercialTermEquivalenceInput> Items, DateOnly ValidFrom, string Reason)
    : IRequest<Result<IReadOnlyList<CommercialTermEquivalenceDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetCommercialTermEquivalencesCommandValidator : ValidadorConMotivo<SetCommercialTermEquivalencesCommand>
{
    public SetCommercialTermEquivalencesCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Agregá al menos una equivalencia.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rigen.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(y => y.Kind).IsInEnum();
            i.RuleFor(y => y.CommercialCode).NotEmpty().MaximumLength(CommercialTermEquivalence.LargoDelCodigo)
                .WithMessage("El texto de Comercial va de 1 a 20 caracteres.");
        });
    }
}

/// <summary>Un texto repetido en el lote → <c>Lending.CommercialTermEquivalence.Duplicate</c>; periodicidad desconocida → <c>.PeriodicityUnknown</c>.</summary>
public sealed class SetCommercialTermEquivalencesCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<SetCommercialTermEquivalencesCommand, Result<IReadOnlyList<CommercialTermEquivalenceDto>>>
{
    public async Task<Result<IReadOnlyList<CommercialTermEquivalenceDto>>> Handle(SetCommercialTermEquivalencesCommand request, CancellationToken ct)
    {
        if (RegistroDeEquivalencias.Validar(request.Items) is { } problema)
            return Result.Failure<IReadOnlyList<CommercialTermEquivalenceDto>>(ErroresDeLineasDeCredito.De(problema));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await RegistroDeEquivalencias.AgregarAsync(db, request.Items, request.ValidFrom, reloj.UtcNow, ct);
            await db.SaveChangesAsync(ct);
            var vigentes = await new EquivalenciasDeComercio(db).VigentesAsync(Max(reloj.HoyLocal, request.ValidFrom), null, ct);
            return Result.Success<IReadOnlyList<CommercialTermEquivalenceDto>>(vigentes.Select(RegistroDeEquivalencias.Dto).ToList());
        }, ct);
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

/// <summary>
/// Las equivalencias vigentes a la fecha (api.md §3.2, <c>GET /api/lending/commercial-term-equivalences</c>,
/// <c>Lending.CreditLines.View</c>). Son de la cooperativa: no tienen oficina ni línea. (nuevo)
/// </summary>
public sealed record ListCommercialTermEquivalencesQuery(DateOnly? Date = null) : IRequest<Result<IReadOnlyList<CommercialTermEquivalenceDto>>>;

public sealed class ListCommercialTermEquivalencesQueryValidator : AbstractValidator<ListCommercialTermEquivalencesQuery>;

public sealed class ListCommercialTermEquivalencesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListCommercialTermEquivalencesQuery, Result<IReadOnlyList<CommercialTermEquivalenceDto>>>
{
    public async Task<Result<IReadOnlyList<CommercialTermEquivalenceDto>>> Handle(ListCommercialTermEquivalencesQuery request, CancellationToken ct)
    {
        var vigentes = await new EquivalenciasDeComercio(db).VigentesAsync(request.Date ?? reloj.HoyLocal, null, ct);
        return Result.Success<IReadOnlyList<CommercialTermEquivalenceDto>>(vigentes.Select(RegistroDeEquivalencias.Dto).ToList());
    }
}
