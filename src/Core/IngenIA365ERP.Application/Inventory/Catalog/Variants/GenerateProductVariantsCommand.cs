using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Variants;

/// <summary>Un atributo elegido para generar variantes y los valores que se combinan, en el orden en que la persona los eligió. (nuevo)</summary>
public sealed record AtributoElegido(Guid AttributePublicId, IReadOnlyList<Guid> ValuePublicIds);

/// <summary>
/// Lo que la persona cambia de una variante propuesta antes de crearla (por su <c>VariantKey</c>): el código, el nombre y un código
/// de barras opcional. Vacío = lo propuesto. (nuevo)
/// </summary>
public sealed record VarianteAjustada(string VariantKey, string? Code = null, string? Name = null, string? Barcode = null);

/// <summary>Un par atributo–valor de una variante. (nuevo)</summary>
public sealed record ValorDeVarianteDto(string AttributeCode, string AttributeName, string ValueCode, string ValueName);

/// <summary>Una variante de una plantilla: su código, nombre, combinación, valores y código de barras principal. (nuevo)</summary>
public sealed record ProductVariantDto(
    Guid PublicId, string Code, string Name, string VariantKey, ProductStatus Status, IReadOnlyList<ValorDeVarianteDto> Values, string? Barcode);

/// <summary>Lo que dejó la generación: las variantes creadas y las combinaciones que la plantilla ya tenía. (nuevo)</summary>
public sealed record VariantesGeneradasDto(Guid TemplatePublicId, IReadOnlyList<ProductVariantDto> Created, IReadOnlyList<string> AlreadyExisting);

/// <summary>
/// Generar las variantes de una plantilla (feature 012, I6, T919; FR-023, US15-1; data-model §1.6, §1.11): sobre un producto
/// <see cref="ProductKind.Template"/> (<c>Inventory.Variant.NotATemplate</c>), con los atributos activos y los valores elegidos,
/// <see cref="GeneradorDeVariantes"/> propone las combinaciones que faltan y aquí se crea cada una como un producto
/// <see cref="ProductKind.Variant"/> con <c>ParentProductId</c>, <c>VariantKey</c>, sus filas de <c>INV_ProductVariantValues</c>
/// y lo heredado de la plantilla: categoría, marca, unidad base y alternas, grupo contable, tratamiento de IVA e impuestos,
/// concepto de retención, seguimiento, referencia, peso, volumen y si se compra o se vende. Cada variante pasa por
/// <see cref="ReglasDeProducto"/> como cualquier alta (si la plantilla no tiene grupo contable o concepto de retención, la variante
/// no puede nacer: <c>Inventory.Product.AccountingGroupRequired</c> / <c>.WithholdingConceptRequired</c>). El código propuesto o
/// ajustado es único (<c>Catalogo.CodigoDuplicado</c>), el código de barras opcional también (<c>Inventory.Barcode.Duplicate</c>)
/// y el <c>SearchText</c> sale de <c>NormalizadorDeBusqueda</c>. Si todas las combinaciones pedidas ya existen, o un ajuste nombra
/// una existente, <c>Inventory.Variant.CombinationExists</c>; si sólo algunas existen, se crean las demás y se informan en
/// <see cref="VariantesGeneradasDto.AlreadyExisting"/>. Todo o nada, en un guardado. (nuevo)
/// </summary>
public sealed record GenerateProductVariantsCommand(
    Guid TemplatePublicId,
    IReadOnlyList<AtributoElegido> Attributes,
    IReadOnlyList<VarianteAjustada>? Adjustments = null)
    : IRequest<Result<VariantesGeneradasDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateProductVariantsCommandValidator : AbstractValidator<GenerateProductVariantsCommand>
{
    public GenerateProductVariantsCommandValidator()
    {
        RuleFor(x => x.TemplatePublicId).NotEmpty();
        RuleFor(x => x.Attributes).NotEmpty().WithMessage("Elija al menos un atributo.");
        RuleForEach(x => x.Attributes).ChildRules(a =>
        {
            a.RuleFor(x => x.AttributePublicId).NotEmpty();
            a.RuleFor(x => x.ValuePublicIds).NotEmpty().WithMessage("Elija al menos un valor del atributo.");
        });
        RuleForEach(x => x.Adjustments).ChildRules(a =>
        {
            a.RuleFor(x => x.VariantKey).NotEmpty().MaximumLength(Product.LargoDeLaClaveDeVariante);
            a.RuleFor(x => x.Code).MaximumLength(CodigoDeCatalogo.LargoLargo).Matches(CodigoDeCatalogo.Patron)
                .WithMessage(CodigoDeCatalogo.MensajeDePatron).When(x => !string.IsNullOrWhiteSpace(x.Code));
            a.RuleFor(x => x.Name).MaximumLength(200);
            a.RuleFor(x => x.Barcode).Must(ValidacionDeProducto.CodigoDeBarrasValido).When(x => !string.IsNullOrWhiteSpace(x.Barcode))
                .WithMessage($"El código de barras tiene hasta {ProductBarcode.MaxLength} letras, dígitos o guiones, sin espacios.");
        });
    }
}

public sealed class GenerateProductVariantsCommandHandler(IApplicationDbContext db)
    : IRequestHandler<GenerateProductVariantsCommand, Result<VariantesGeneradasDto>>
{
    public async Task<Result<VariantesGeneradasDto>> Handle(GenerateProductVariantsCommand request, CancellationToken ct)
    {
        var plantilla = await db.Products
            .Include(p => p.Category).Include(p => p.Brand).Include(p => p.BaseUnit).Include(p => p.AccountingGroup)
            .Include(p => p.WithholdingConcept).Include(p => p.Units).ThenInclude(u => u.Unit).Include(p => p.Taxes)
            .FirstOrDefaultAsync(p => p.PublicId == request.TemplatePublicId, ct);
        if (plantilla is null) return Falla(CatalogErrors.ProductNotFound());
        if (plantilla.Kind != ProductKind.Template) return Falla(CatalogErrors.VariantNotATemplate(plantilla.Code));

        // Los atributos y valores elegidos, activos y de su atributo.
        var idsDeAtributo = request.Attributes.Select(a => a.AttributePublicId).ToList();
        var atributos = await db.VariantAttributes.Include(a => a.Values).Where(a => idsDeAtributo.Contains(a.PublicId)).ToListAsync(ct);
        var elegidos = new List<(VariantAttribute Atributo, List<VariantAttributeValue> Valores)>();
        foreach (var pedido in request.Attributes)
        {
            var atributo = atributos.FirstOrDefault(a => a.PublicId == pedido.AttributePublicId);
            if (atributo is null) return Falla(CatalogErrors.VariantAttributeNotFound());
            if (!atributo.IsActive) return Falla(CatalogErrors.VariantAttributeInactive(atributo.Code));
            var valores = new List<VariantAttributeValue>();
            foreach (var idDeValor in pedido.ValuePublicIds.Distinct())
            {
                var valor = atributo.Values.FirstOrDefault(v => !v.IsDeleted && v.PublicId == idDeValor);
                if (valor is null) return Falla(CatalogErrors.VariantAttributeValueNotFound(atributo.Code));
                valores.Add(valor);
            }
            elegidos.Add((atributo, valores));
        }

        var clavesExistentes = await db.Products.Where(p => p.ParentProductId == plantilla.Id && p.VariantKey != null)
            .Select(p => p.VariantKey!).ToListAsync(ct);
        var generado = GeneradorDeVariantes.Generar(new PedidoDeVariantes(plantilla.Code, plantilla.Name,
            elegidos.Select(e => new AtributoParaVariantes(e.Atributo.Code, e.Atributo.Name,
                e.Valores.Select(v => new ValorDeAtributo(v.Code, v.Name, v.SortOrder)).ToList())).ToList(),
            clavesExistentes));
        if (generado.Rechazo is { } rechazo) return Falla(new Error(rechazo.Codigo, rechazo.Mensaje));

        var ajustes = new Dictionary<string, VarianteAjustada>(StringComparer.Ordinal);
        foreach (var ajuste in request.Adjustments ?? [])
        {
            string clave;
            try { clave = GeneradorDeVariantes.Normalizar(ajuste.VariantKey); }
            catch (ArgumentException) { return Falla(CatalogErrors.VariantAttributeValueNotFound(ajuste.VariantKey)); }
            if (generado.Existentes.Contains(clave)) return Falla(CatalogErrors.VariantCombinationExists([clave]));
            ajustes[clave] = ajuste;
        }
        if (generado.Propuestas.Count == 0) return Falla(CatalogErrors.VariantCombinationExists(generado.Existentes));

        // Códigos (propuestos o ajustados) y códigos de barras: únicos entre sí y contra la base.
        var finales = generado.Propuestas.Select(p =>
        {
            var ajuste = ajustes.GetValueOrDefault(p.VariantKey);
            return (Propuesta: p,
                Codigo: CodigoDeCatalogo.Normalizar(ajuste?.Code) ?? p.Codigo,
                Nombre: string.IsNullOrWhiteSpace(ajuste?.Name) ? p.Nombre : ajuste.Name.Trim(),
                Barras: string.IsNullOrWhiteSpace(ajuste?.Barcode) ? null : ProductBarcode.Normalizar(ajuste.Barcode));
        }).ToList();
        if (finales.GroupBy(f => f.Codigo, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } codigoRepetido)
            return Falla(CodigoDeCatalogo.Duplicado("otra variante propuesta", codigoRepetido.Key, codigoRepetido.First().Nombre));
        var codigos = finales.Select(f => f.Codigo).ToList();
        var yaUsado = await db.Products.Where(p => codigos.Contains(p.Code)).Select(p => new { p.Code, p.Name, p.PublicId }).FirstOrDefaultAsync(ct);
        if (yaUsado is not null) return Falla(CodigoDeCatalogo.Duplicado("un producto", yaUsado.Code, yaUsado.Name, yaUsado.PublicId));
        var barrasVistas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in finales.Where(f => f.Barras is not null))
        {
            if (!barrasVistas.Add(f.Barras!)) return Falla(CatalogErrors.BarcodeDuplicate(f.Barras!, Guid.Empty, f.Codigo, f.Nombre));
            if (await CodigosDeBarras.DuenoAsync(db, f.Barras!, ct) is { } dueno) return Falla(dueno);
        }

        var valoresPorClave = elegidos.SelectMany(e => e.Valores.Select(v => (e.Atributo, Valor: v)))
            .ToDictionary(x => (x.Atributo.Code, x.Valor.Code));
        var referencias = new ReferenciasDeProducto(plantilla.Category!, plantilla.Brand, plantilla.BaseUnit!, plantilla.AccountingGroup, plantilla.WithholdingConcept);
        var creadas = new List<Product>();
        foreach (var f in finales)
        {
            var variante = new Product
            {
                Code = f.Codigo, Status = ProductStatus.Active, ParentProduct = plantilla, ParentProductId = plantilla.Id,
                VariantKey = f.Propuesta.VariantKey,
            };
            var datos = new DatosDeProducto(f.Nombre, null, plantilla.Description, ProductKind.Variant, plantilla.Category!.PublicId,
                plantilla.Brand?.PublicId, plantilla.BaseUnit!.PublicId, plantilla.AccountingGroup?.PublicId, plantilla.VatSaleTreatment,
                plantilla.WithholdingConcept?.PublicId, plantilla.Reference, plantilla.Weight, plantilla.Volume, plantilla.TracksLot,
                plantilla.TracksSerial, plantilla.TracksExpiry, plantilla.IsPurchasable, plantilla.IsSellable);
            var reglas = ReglasDeProducto.Aplicar(variante, datos, referencias, HistoriaDeProducto.Nueva);
            if (reglas.IsFailure) return Falla(reglas.Error);

            foreach (var alterna in plantilla.Units.Where(u => !u.IsDeleted))
            {
                var copia = new ProductUnit
                {
                    Product = variante, UnitId = alterna.UnitId, Unit = alterna.Unit, Factor = alterna.Factor,
                    IsDefaultPurchase = alterna.IsDefaultPurchase, IsDefaultSale = alterna.IsDefaultSale,
                };
                copia.FijarUso(alterna.Usage);
                variante.Units.Add(copia);
            }
            foreach (var impuesto in plantilla.Taxes.Where(t => !t.IsDeleted))
                variante.Taxes.Add(new ProductTax
                {
                    Product = variante, TaxDefinitionId = impuesto.TaxDefinitionId, TaxRateCode = impuesto.TaxRateCode,
                    AppliesTo = impuesto.AppliesTo, TaxableUnitsPerBaseUnit = impuesto.TaxableUnitsPerBaseUnit,
                });
            foreach (var par in f.Propuesta.Valores)
            {
                var (atributo, valor) = valoresPorClave[(par.Atributo, par.Valor)];
                variante.VariantValues.Add(new ProductVariantValue
                {
                    Product = variante, VariantAttributeId = atributo.Id, VariantAttribute = atributo,
                    VariantAttributeValueId = valor.Id, VariantAttributeValue = valor,
                });
            }
            if (f.Barras is { } barras)
                variante.Barcodes.Add(new ProductBarcode { Product = variante, Barcode = barras, IsPrimary = true });

            variante.SearchText = ReglasDeProducto.TextoDeBusqueda(variante, plantilla.Brand?.Name, f.Barras is null ? [] : [f.Barras]);
            db.Products.Add(variante);
            creadas.Add(variante);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(new VariantesGeneradasDto(plantilla.PublicId, creadas.Select(VistaDeVariantes.Dto).ToList(), generado.Existentes));
    }

    private static Result<VariantesGeneradasDto> Falla(Error error) => Result.Failure<VariantesGeneradasDto>(error);
}

/// <summary>Las variantes de una plantilla (T919; para <c>GET /products/{id}/variants</c>, T934), por código. (nuevo)</summary>
public sealed record ListProductVariantsQuery(Guid TemplatePublicId) : IRequest<Result<IReadOnlyList<ProductVariantDto>>>;

public sealed class ListProductVariantsQueryValidator : AbstractValidator<ListProductVariantsQuery>
{
    public ListProductVariantsQueryValidator() => RuleFor(x => x.TemplatePublicId).NotEmpty();
}

public sealed class ListProductVariantsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListProductVariantsQuery, Result<IReadOnlyList<ProductVariantDto>>>
{
    public async Task<Result<IReadOnlyList<ProductVariantDto>>> Handle(ListProductVariantsQuery request, CancellationToken ct)
    {
        var plantilla = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.TemplatePublicId, ct);
        if (plantilla is null) return Result.Failure<IReadOnlyList<ProductVariantDto>>(CatalogErrors.ProductNotFound());
        if (plantilla.Kind != ProductKind.Template) return Result.Failure<IReadOnlyList<ProductVariantDto>>(CatalogErrors.VariantNotATemplate(plantilla.Code));

        var variantes = await db.Products.AsNoTracking()
            .Include(p => p.VariantValues).ThenInclude(v => v.VariantAttribute)
            .Include(p => p.VariantValues).ThenInclude(v => v.VariantAttributeValue)
            .Include(p => p.Barcodes)
            .Where(p => p.ParentProductId == plantilla.Id).OrderBy(p => p.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<ProductVariantDto>>(variantes.Select(VistaDeVariantes.Dto).ToList());
    }
}

/// <summary>Cómo se arma el <see cref="ProductVariantDto"/>: los valores por código de atributo, como la <c>VariantKey</c>. (nuevo)</summary>
public static class VistaDeVariantes
{
    public static ProductVariantDto Dto(Product p) => new(
        p.PublicId, p.Code, p.Name, p.VariantKey ?? string.Empty, p.Status,
        p.VariantValues.Where(v => !v.IsDeleted)
            .OrderBy(v => v.VariantAttribute?.Code, StringComparer.Ordinal)
            .Select(v => new ValorDeVarianteDto(v.VariantAttribute?.Code ?? string.Empty, v.VariantAttribute?.Name ?? string.Empty,
                v.VariantAttributeValue?.Code ?? string.Empty, v.VariantAttributeValue?.Name ?? string.Empty))
            .ToList(),
        p.Barcodes.Where(b => !b.IsDeleted).OrderByDescending(b => b.IsPrimary).Select(b => b.Barcode).FirstOrDefault());
}
