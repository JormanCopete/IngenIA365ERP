using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>Una unidad alterna pedida en el alta: la unidad de medida, su factor a la base y para qué sirve.</summary>
public sealed record UnidadPedida(Guid UnitPublicId, decimal Factor, ProductUnitUsage Usage);

/// <summary>Un código de barras pedido en el alta; <see cref="UnitPublicId"/> nulo = la unidad base.</summary>
public sealed record CodigoPedido(string Barcode, Guid? UnitPublicId);

/// <summary>
/// Alta de un producto (feature 012, T218; contracts/api.md §3.5, <c>POST /api/inventory/products</c>; FR-023 a FR-028) con
/// sus unidades alternas, códigos de barras e impuestos en la misma transacción y con las mismas reglas de sus
/// subrecursos (§3.6). Las reglas del producto las aplica <see cref="ReglasDeProducto"/>. (nuevo)
/// </summary>
public sealed record CreateProductCommand(
    string Code,
    string Name,
    string? ShortName,
    string? Description,
    ProductKind Kind,
    Guid CategoryPublicId,
    Guid? BrandPublicId,
    Guid BaseUnitPublicId,
    Guid? AccountingGroupPublicId,
    VatSaleTreatment VatSaleTreatment,
    Guid? WithholdingConceptPublicId,
    string? Reference,
    decimal? Weight,
    decimal? Volume,
    bool TracksLot,
    bool TracksSerial,
    bool TracksExpiry,
    IReadOnlyList<UnidadPedida>? Units,
    IReadOnlyList<CodigoPedido>? Barcodes,
    IReadOnlyList<ImpuestoPedido>? Taxes,
    bool IsPurchasable = true,
    bool IsSellable = true)
    : IRequest<Result<ProductDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    public DatosDeProducto Datos => new(Name, ShortName, Description, Kind, CategoryPublicId, BrandPublicId, BaseUnitPublicId,
        AccountingGroupPublicId, VatSaleTreatment, WithholdingConceptPublicId, Reference, Weight, Volume, TracksLot, TracksSerial,
        TracksExpiry, IsPurchasable, IsSellable);
}

/// <summary>Las reglas de forma del producto que comparten el alta y la edición.</summary>
public static class ValidacionDeProducto
{
    public const string PatronDeCodigoDeBarras = "^[A-Za-z0-9-]+$";

    public static bool FactorValido(decimal factor) => factor > 0 && ConversionDeUnidades.Decimales(factor) <= 6;

    public static bool CodigoDeBarrasValido(string? codigo)
    {
        var c = ProductBarcode.Normalizar(codigo);
        return c.Length > 0 && c.Length <= ProductBarcode.MaxLength && System.Text.RegularExpressions.Regex.IsMatch(c, PatronDeCodigoDeBarras);
    }

    public static bool Cuatro(decimal? valor) => valor is null || ConversionDeUnidades.Decimales(valor.Value) <= 4;
}

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoLargo)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).MaximumLength(40);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Reference).MaximumLength(60);
        RuleFor(x => x.Weight).GreaterThanOrEqualTo(0).Must(ValidacionDeProducto.Cuatro).WithMessage("El peso admite hasta 4 decimales.");
        RuleFor(x => x.Volume).GreaterThanOrEqualTo(0).Must(ValidacionDeProducto.Cuatro).WithMessage("El volumen admite hasta 4 decimales.");
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.VatSaleTreatment).IsInEnum();
        RuleFor(x => x.CategoryPublicId).NotEmpty();
        RuleFor(x => x.BaseUnitPublicId).NotEmpty();
        RuleForEach(x => x.Units).ChildRules(u =>
        {
            u.RuleFor(x => x.UnitPublicId).NotEmpty();
            u.RuleFor(x => x.Factor).Must(ValidacionDeProducto.FactorValido).WithMessage("El factor es mayor que cero, con hasta 6 decimales.");
            u.RuleFor(x => x.Usage).IsInEnum();
        });
        RuleForEach(x => x.Barcodes).ChildRules(b =>
            b.RuleFor(x => x.Barcode).Must(ValidacionDeProducto.CodigoDeBarrasValido)
                .WithMessage($"El código de barras tiene hasta {ProductBarcode.MaxLength} letras, dígitos o guiones, sin espacios."));
        RuleForEach(x => x.Taxes).ChildRules(t => t.RuleFor(x => x.TaxDefinitionPublicId).NotEmpty());
    }
}

public sealed class CreateProductCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.Products.Where(p => p.Code == codigo).Select(p => new { p.PublicId, p.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null) return Falla(CodigoDeCatalogo.Duplicado("un producto", codigo, existente.Name, existente.PublicId));

        var producto = new Product { Code = codigo, Status = ProductStatus.Active };
        var reglas = await ReglasDeProducto.AplicarAsync(db, producto, request.Datos, ct);
        if (reglas.IsFailure) return Falla(reglas.Error);

        // Unidades alternas: distintas de la base, sin repetir; la primera de compra y la primera de venta quedan por defecto.
        var alternas = new Dictionary<Guid, ProductUnit>();
        foreach (var pedida in request.Units ?? [])
        {
            var unidad = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.PublicId == pedida.UnitPublicId, ct);
            if (unidad is null) return Falla(CatalogErrors.UnitNotFound());
            if (unidad.Id == producto.BaseUnitId) return Falla(CatalogErrors.ProductUnitIsBaseUnit(unidad.Code));
            if (alternas.ContainsKey(unidad.PublicId)) return Falla(CatalogErrors.ProductUnitDuplicate(unidad.Code));
            var alterna = new ProductUnit { Product = producto, UnitId = unidad.Id, Unit = unidad, Factor = pedida.Factor };
            alterna.FijarUso(pedida.Usage);
            alterna.IsDefaultPurchase = alterna.UsedForPurchase && alternas.Values.All(a => !a.IsDefaultPurchase);
            alterna.IsDefaultSale = alterna.UsedForSale && alternas.Values.All(a => !a.IsDefaultSale);
            alternas[unidad.PublicId] = alterna;
            producto.Units.Add(alterna);
        }

        // Códigos de barras: únicos en la cooperativa entre los vivos; el empaque es la base o una alterna del pedido.
        var codigos = new List<string>();
        foreach (var pedido in request.Barcodes ?? [])
        {
            var barras = ProductBarcode.Normalizar(pedido.Barcode);
            if (codigos.Contains(barras)) return Falla(CatalogErrors.BarcodeDuplicate(barras, producto.PublicId, producto.Code, producto.Name));
            var dueno = await CodigosDeBarras.DuenoAsync(db, barras, ct);
            if (dueno is not null) return Falla(dueno);

            ProductUnit? empaque = null;
            if (pedido.UnitPublicId is { } up && !alternas.TryGetValue(up, out empaque)
                && !(producto.BaseUnit is { } b && b.PublicId == up))
                return Falla(CatalogErrors.ProductUnitNotFound());

            producto.Barcodes.Add(new ProductBarcode { Product = producto, Barcode = barras, ProductUnit = empaque, IsPrimary = codigos.Count == 0 });
            codigos.Add(barras);
        }

        // Impuestos, y el tratamiento de IVA contra ellos.
        var impuestos = await ReglasDeProducto.ResolverImpuestosAsync(db, request.Taxes ?? [], ct);
        if (impuestos.IsFailure) return Falla(impuestos.Error);
        var iva = ReglasDeProducto.ValidarIva(request.VatSaleTreatment, impuestos.Value);
        if (iva.IsFailure) return Falla(iva.Error);
        ReglasDeProducto.FijarImpuestos(producto, impuestos.Value, reloj.UtcNow);

        producto.SearchText = ReglasDeProducto.TextoDeBusqueda(producto, producto.Brand?.Name, codigos);
        db.Products.Add(producto);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeProductos.ArmarAsync(db, producto.Id, reloj.HoyLocal, ct));
    }

    private static Result<ProductDto> Falla(Error error) => Result.Failure<ProductDto>(error);
}

/// <summary>
/// Edición de un producto (T218; §3.5, <c>PUT /{id}</c>): lo mismo que el alta sin código, clase, unidades, códigos ni
/// impuestos (tienen sus rutas). Con movimientos la unidad base y el grupo contable no cambian por aquí; el tratamiento de
/// IVA se revisa contra los impuestos que ya tiene. (nuevo)
/// </summary>
public sealed record UpdateProductCommand(
    Guid ProductPublicId,
    string Name,
    string? ShortName,
    string? Description,
    Guid CategoryPublicId,
    Guid? BrandPublicId,
    Guid BaseUnitPublicId,
    Guid? AccountingGroupPublicId,
    VatSaleTreatment VatSaleTreatment,
    Guid? WithholdingConceptPublicId,
    string? Reference,
    decimal? Weight,
    decimal? Volume,
    bool TracksLot,
    bool TracksSerial,
    bool TracksExpiry,
    bool IsPurchasable = true,
    bool IsSellable = true)
    : IRequest<Result<ProductDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).MaximumLength(40);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Reference).MaximumLength(60);
        RuleFor(x => x.Weight).GreaterThanOrEqualTo(0).Must(ValidacionDeProducto.Cuatro).WithMessage("El peso admite hasta 4 decimales.");
        RuleFor(x => x.Volume).GreaterThanOrEqualTo(0).Must(ValidacionDeProducto.Cuatro).WithMessage("El volumen admite hasta 4 decimales.");
        RuleFor(x => x.VatSaleTreatment).IsInEnum();
        RuleFor(x => x.CategoryPublicId).NotEmpty();
        RuleFor(x => x.BaseUnitPublicId).NotEmpty();
    }
}

public sealed class UpdateProductCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var producto = await db.Products.FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Result.Failure<ProductDto>(CatalogErrors.ProductNotFound());

        var datos = new DatosDeProducto(request.Name, request.ShortName, request.Description, producto.Kind, request.CategoryPublicId,
            request.BrandPublicId, request.BaseUnitPublicId, request.AccountingGroupPublicId, request.VatSaleTreatment,
            request.WithholdingConceptPublicId, request.Reference, request.Weight, request.Volume, request.TracksLot, request.TracksSerial,
            request.TracksExpiry, request.IsPurchasable, request.IsSellable);
        var reglas = await ReglasDeProducto.AplicarAsync(db, producto, datos, ct);
        if (reglas.IsFailure) return Result.Failure<ProductDto>(reglas.Error);

        var iva = ReglasDeProducto.ValidarIva(request.VatSaleTreatment, await ReglasDeProducto.ImpuestosVivosAsync(db, producto.Id, ct));
        if (iva.IsFailure) return Result.Failure<ProductDto>(iva.Error);

        await ReglasDeProducto.RecalcularTextoDeBusquedaAsync(db, producto, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeProductos.ArmarAsync(db, producto.Id, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// Cambio de estado de un producto con motivo (T218; §3.5, <c>POST /{id}/status</c>; FR-028): activo, inactivo (no se
/// ofrece en documentos nuevos) o bloqueado (ningún movimiento salvo el conteo y la recepción de lo que ya viajaba). El
/// mismo estado responde <c>Inventory.Product.StatusUnchanged</c>. (nuevo)
/// </summary>
public sealed record SetProductStatusCommand(Guid ProductPublicId, ProductStatus Status, string Reason)
    : IRequest<Result<ProductDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetProductStatusCommandValidator : ValidadorConMotivo<SetProductStatusCommand>
{
    public SetProductStatusCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class SetProductStatusCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<SetProductStatusCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(SetProductStatusCommand request, CancellationToken ct)
    {
        var producto = await db.Products.FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Result.Failure<ProductDto>(CatalogErrors.ProductNotFound());
        if (producto.Status == request.Status) return Result.Failure<ProductDto>(CatalogErrors.ProductStatusUnchanged(request.Status));

        producto.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeProductos.ArmarAsync(db, producto.Id, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// Borrado de un producto (T218; §3.5, <c>DELETE /{id}</c>; FR-028, US1-3): sólo si nunca estuvo en un documento —ni en un
/// borrador— (las listas de precios llegan con I3); si no, <c>Inventory.Product.HasHistory</c> con
/// <c>alternatives: ["Inactive", "Blocked"]</c>. Es baja lógica del producto y de lo que cuelga de él: sus códigos de
/// barras quedan libres para otro. (nuevo)
/// </summary>
public sealed record DeleteProductCommand(Guid ProductPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class DeleteProductCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.Units).Include(p => p.Barcodes).Include(p => p.Taxes)
            .FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Result.Failure(CatalogErrors.ProductNotFound());
        if (await ReglasDeProducto.TieneMovimientosAsync(db, producto.Id, ct)) return Result.Failure(CatalogErrors.ProductHasHistory());

        var ahora = reloj.UtcNow;
        foreach (var fila in producto.Units.Cast<Domain.Common.BaseEntity>().Concat(producto.Barcodes).Concat(producto.Taxes).Append(producto))
        {
            fila.IsDeleted = true;
            fila.DeletedAt = ahora;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Quién tiene un código de barras vivo (FR-024): lo comparten el alta, el subrecurso y la plantilla. (nuevo)</summary>
public static class CodigosDeBarras
{
    /// <summary>Nulo si el código está libre; si no, <c>Inventory.Barcode.Duplicate</c> nombrando al producto que lo tiene.</summary>
    public static async Task<Error?> DuenoAsync(IApplicationDbContext db, string barras, CancellationToken ct)
    {
        var dueno = await db.ProductBarcodes.Where(b => b.Barcode == barras)
            .Join(db.Products, b => b.ProductId, p => p.Id, (b, p) => new { p.PublicId, p.Code, p.Name })
            .FirstOrDefaultAsync(ct);
        return dueno is null ? null : CatalogErrors.BarcodeDuplicate(barras, dueno.PublicId, dueno.Code, dueno.Name);
    }
}
