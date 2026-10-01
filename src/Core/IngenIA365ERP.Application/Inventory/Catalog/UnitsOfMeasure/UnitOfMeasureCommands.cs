using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.UnitsOfMeasure;

/// <summary>
/// Alta de una unidad de medida (feature 012, T214; contracts/api.md §3.1, <c>POST /api/inventory/units</c>; FR-017,
/// FR-025): código por <c>CodigoDeCatalogo</c>, decimales 0..4 y el código UN/ECE Rec. 20 validado contra
/// <c>CatalogoDian</c> (<c>Inventory.Unit.DianCodeUnknown</c>). (nuevo)
/// </summary>
public sealed record CreateUnitOfMeasureCommand(string Code, string Name, string? Symbol, int AllowedDecimals, string? DianUnitCode)
    : IRequest<Result<UnitOfMeasureDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Symbol).MaximumLength(10);
        RuleFor(x => x.AllowedDecimals).InclusiveBetween(0, UnitOfMeasure.MaxDecimals);
        RuleFor(x => x.DianUnitCode).MaximumLength(3);
    }
}

public sealed class CreateUnitOfMeasureCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<CreateUnitOfMeasureCommand, Result<UnitOfMeasureDto>>
{
    public async Task<Result<UnitOfMeasureDto>> Handle(CreateUnitOfMeasureCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.UnitsOfMeasure.Where(u => u.Code == codigo).Select(u => new { u.PublicId, u.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<UnitOfMeasureDto>(CodigoDeCatalogo.Duplicado("una unidad", codigo, existente.Name, existente.PublicId));

        var dian = ReglasDeUnidad.CodigoDian(request.DianUnitCode, reloj.HoyLocal);
        if (dian.IsFailure) return Result.Failure<UnitOfMeasureDto>(dian.Error);

        var unidad = new UnitOfMeasure
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Symbol = ReglasDeUnidad.Limpio(request.Symbol),
            AllowedDecimals = (byte)request.AllowedDecimals,
            DianUnitCode = dian.Value,
            IsActive = true,
        };
        db.UnitsOfMeasure.Add(unidad);
        await db.SaveChangesAsync(ct);
        return Result.Success(ReglasDeUnidad.Dto(unidad, 0));
    }
}

/// <summary>
/// Edición de una unidad (T214; §3.1, <c>PUT /{id}</c>): nombre, símbolo, decimales y código DIAN; el código no cambia.
/// Los decimales sólo bajan si ninguna cantidad registrada usa más (<c>Inventory.Unit.DecimalsInUse</c> con
/// <c>maxDecimalsUsed</c>). (nuevo)
/// </summary>
public sealed record UpdateUnitOfMeasureCommand(Guid UnitPublicId, string Name, string? Symbol, int AllowedDecimals, string? DianUnitCode)
    : IRequest<Result<UnitOfMeasureDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateUnitOfMeasureCommandValidator : AbstractValidator<UpdateUnitOfMeasureCommand>
{
    public UpdateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.UnitPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Symbol).MaximumLength(10);
        RuleFor(x => x.AllowedDecimals).InclusiveBetween(0, UnitOfMeasure.MaxDecimals);
        RuleFor(x => x.DianUnitCode).MaximumLength(3);
    }
}

public sealed class UpdateUnitOfMeasureCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<UpdateUnitOfMeasureCommand, Result<UnitOfMeasureDto>>
{
    public async Task<Result<UnitOfMeasureDto>> Handle(UpdateUnitOfMeasureCommand request, CancellationToken ct)
    {
        var unidad = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.PublicId == request.UnitPublicId, ct);
        if (unidad is null) return Result.Failure<UnitOfMeasureDto>(CatalogErrors.UnitNotFound());

        var dian = ReglasDeUnidad.CodigoDian(request.DianUnitCode, reloj.HoyLocal);
        if (dian.IsFailure) return Result.Failure<UnitOfMeasureDto>(dian.Error);

        if (request.AllowedDecimals < unidad.AllowedDecimals)
        {
            var usados = await ReglasDeUnidad.DecimalesUsadosAsync(db, unidad.Id, ct);
            if (usados > request.AllowedDecimals) return Result.Failure<UnitOfMeasureDto>(CatalogErrors.UnitDecimalsInUse(usados));
        }

        unidad.Name = request.Name.Trim();
        unidad.Symbol = ReglasDeUnidad.Limpio(request.Symbol);
        unidad.AllowedDecimals = (byte)request.AllowedDecimals;
        unidad.DianUnitCode = dian.Value;
        await db.SaveChangesAsync(ct);
        return Result.Success(ReglasDeUnidad.Dto(unidad, await ReglasDeUnidad.ProductosQueLaUsanAsync(db, unidad.Id, ct)));
    }
}

/// <summary>
/// Inactivar o reactivar una unidad con motivo (T214; §3.1, <c>/deactivate</c> · <c>/reactivate</c>). Una unidad que es base
/// o alterna de un producto que no está inactivo no se inactiva (<c>Inventory.Unit.InUse</c> con <c>products</c> y
/// <c>examples[]</c>). Una sembrada tampoco se elimina: sólo se inactiva (no hay ruta de borrado). (nuevo)
/// </summary>
public sealed record SetUnitOfMeasureActiveCommand(Guid UnitPublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetUnitOfMeasureActiveCommandValidator : ValidadorConMotivo<SetUnitOfMeasureActiveCommand>
{
    public SetUnitOfMeasureActiveCommandValidator() => RuleFor(x => x.UnitPublicId).NotEmpty();
}

public sealed class SetUnitOfMeasureActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetUnitOfMeasureActiveCommand, Result>
{
    public async Task<Result> Handle(SetUnitOfMeasureActiveCommand request, CancellationToken ct) =>
        await ActivacionDeCatalogo.CambiarAsync(
            await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.PublicId == request.UnitPublicId, ct),
            request.Active, u => u.IsActive, (u, a) => u.IsActive = a, CatalogErrors.UnitNotFound(),
            async u =>
            {
                var codigos = await db.Products
                    .Where(p => p.Status != ProductStatus.Inactive
                        && (p.BaseUnitId == u.Id || db.ProductUnits.Any(pu => pu.ProductId == p.Id && pu.UnitId == u.Id)))
                    .OrderBy(p => p.Code).Select(p => p.Code).ToListAsync(ct);
                return codigos.Count == 0 ? null : CatalogErrors.UnitInUse(codigos.Count, codigos.Take(5).ToList());
            },
            db.SaveChangesAsync, ct);
}

/// <summary>Las unidades (§3.1, <c>GET /?includeInactive=</c>), por código, con cuántos productos vivos las usan. (nuevo)</summary>
public sealed record ListUnitsOfMeasureQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<UnitOfMeasureDto>>>;

public sealed class ListUnitsOfMeasureQueryValidator : AbstractValidator<ListUnitsOfMeasureQuery>;

public sealed class ListUnitsOfMeasureQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListUnitsOfMeasureQuery, Result<IReadOnlyList<UnitOfMeasureDto>>>
{
    public async Task<Result<IReadOnlyList<UnitOfMeasureDto>>> Handle(ListUnitsOfMeasureQuery request, CancellationToken ct)
    {
        var unidades = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => request.IncludeInactive || u.IsActive)
            .OrderBy(u => u.Code)
            .ToListAsync(ct);
        var comoBase = await db.Products.AsNoTracking().GroupBy(p => p.BaseUnitId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var comoAlterna = await db.ProductUnits.AsNoTracking()
            .Where(pu => db.Products.Any(p => p.Id == pu.ProductId))
            .GroupBy(pu => pu.UnitId).Select(g => new { g.Key, N = g.Select(x => x.ProductId).Distinct().Count() }).ToListAsync(ct);
        var uso = comoBase.Concat(comoAlterna).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.N));
        return Result.Success<IReadOnlyList<UnitOfMeasureDto>>(unidades.Select(u => ReglasDeUnidad.Dto(u, uso.GetValueOrDefault(u.Id))).ToList());
    }
}

/// <summary>Lo que comparten los comandos de unidades y la plantilla de unidades (T214, T227). (nuevo)</summary>
public static class ReglasDeUnidad
{
    /// <summary>El código Rec. 20 en mayúsculas si está en <c>CatalogoDian</c> a la fecha; vacío = sin código.</summary>
    public static Result<string?> CodigoDian(string? codigo, DateOnly fecha)
    {
        var limpio = Limpio(codigo)?.ToUpperInvariant();
        if (limpio is null) return Result.Success<string?>(null);
        return CatalogoDian.Embebido.EsUnidadValida(limpio, fecha)
            ? Result.Success<string?>(limpio)
            : Result.Failure<string?>(CatalogErrors.UnitDianCodeUnknown(limpio));
    }

    /// <summary>
    /// Los decimales más altos con que se registró una cantidad en la unidad: las líneas digitadas en ella y las cantidades en
    /// base de los productos cuya base es ella (lo que el kardex guardará).
    /// </summary>
    public static async Task<int> DecimalesUsadosAsync(IApplicationDbContext db, int unidadId, CancellationToken ct)
    {
        var enLaUnidad = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.UnitId == unidadId).Select(l => l.Quantity).Distinct().ToListAsync(ct);
        var enBase = await db.InventoryDocumentLines.AsNoTracking()
            .Where(l => db.Products.Any(p => p.Id == l.ProductId && p.BaseUnitId == unidadId))
            .Select(l => l.QuantityBase).Distinct().ToListAsync(ct);
        return enLaUnidad.Concat(enBase).Select(ConversionDeUnidades.Decimales).DefaultIfEmpty(0).Max();
    }

    public static async Task<int> ProductosQueLaUsanAsync(IApplicationDbContext db, int unidadId, CancellationToken ct) =>
        await db.Products.CountAsync(p => p.BaseUnitId == unidadId || db.ProductUnits.Any(pu => pu.ProductId == p.Id && pu.UnitId == unidadId), ct);

    public static UnitOfMeasureDto Dto(UnitOfMeasure u, int productos) =>
        new(u.PublicId, u.Code, u.Name, u.Symbol, u.AllowedDecimals, u.DianUnitCode, u.IsSeeded, u.IsActive, productos);

    public static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
