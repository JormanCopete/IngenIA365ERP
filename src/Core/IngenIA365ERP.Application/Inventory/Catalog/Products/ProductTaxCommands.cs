using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Reemplazar el tratamiento tributario de un producto (feature 012, T219; contracts/api.md §3.6.3, <c>PUT
/// /products/{id}/taxes</c>; FR-013, FR-027): tratamiento de IVA, concepto de retención y el conjunto de impuestos, con las
/// reglas de <see cref="ReglasDeProducto"/>. La tarifa se guarda por su código estable, no por la fila. El cambio rige para
/// lo que se confirme después: lo confirmado conserva su foto en <c>INV_DocumentTaxLines</c> (T22). (nuevo)
/// </summary>
public sealed record SetProductTaxesCommand(
    Guid ProductPublicId, VatSaleTreatment VatSaleTreatment, Guid? WithholdingConceptPublicId, IReadOnlyList<ImpuestoPedido> Taxes)
    : IRequest<Result<ProductTaxesDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetProductTaxesCommandValidator : AbstractValidator<SetProductTaxesCommand>
{
    public SetProductTaxesCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.VatSaleTreatment).IsInEnum();
        RuleFor(x => x.Taxes).NotNull();
        RuleForEach(x => x.Taxes).ChildRules(t =>
        {
            t.RuleFor(x => x.TaxDefinitionPublicId).NotEmpty();
            t.RuleFor(x => x.TaxableUnitsPerBaseUnit).Must(u => u is null || ValidacionDeProducto.FactorValido(u.Value))
                .WithMessage("Las unidades gravables son mayores que cero, con hasta 6 decimales.");
        });
    }
}

public sealed class SetProductTaxesCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<SetProductTaxesCommand, Result<ProductTaxesDto>>
{
    public async Task<Result<ProductTaxesDto>> Handle(SetProductTaxesCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.Taxes).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Falla(CatalogErrors.ProductNotFound());

        int? conceptoId = null;
        if (request.WithholdingConceptPublicId is { } cp)
        {
            conceptoId = await db.WithholdingConcepts.Where(c => c.PublicId == cp).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (conceptoId is null) return Falla(TaxErrors.ConceptNotFound());
        }
        else if (producto.Kind is not (ProductKind.Template or ProductKind.Combo))
            return Falla(CatalogErrors.ProductWithholdingConceptRequired());

        var impuestos = await ReglasDeProducto.ResolverImpuestosAsync(db, request.Taxes, ct);
        if (impuestos.IsFailure) return Falla(impuestos.Error);
        var iva = ReglasDeProducto.ValidarIva(request.VatSaleTreatment, impuestos.Value);
        if (iva.IsFailure) return Falla(iva.Error);

        producto.VatSaleTreatment = request.VatSaleTreatment;
        producto.WithholdingConceptId = conceptoId;
        ReglasDeProducto.FijarImpuestos(producto, impuestos.Value, reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeImpuestos.ArmarAsync(db, producto, reloj.HoyLocal, ct));
    }

    private static Result<ProductTaxesDto> Falla(Error e) => Result.Failure<ProductTaxesDto>(e);
}

/// <summary>El tratamiento tributario de un producto (T219; §3.6.3, <c>GET /products/{id}/taxes</c>). (nuevo)</summary>
public sealed record GetProductTaxesQuery(Guid ProductPublicId) : IRequest<Result<ProductTaxesDto>>;

public sealed class GetProductTaxesQueryValidator : AbstractValidator<GetProductTaxesQuery>
{
    public GetProductTaxesQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class GetProductTaxesQueryHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<GetProductTaxesQuery, Result<ProductTaxesDto>>
{
    public async Task<Result<ProductTaxesDto>> Handle(GetProductTaxesQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        return producto is null
            ? Result.Failure<ProductTaxesDto>(CatalogErrors.ProductNotFound())
            : Result.Success(await VistaDeImpuestos.ArmarAsync(db, producto, reloj.HoyLocal, ct));
    }
}

internal static class VistaDeImpuestos
{
    public static async Task<ProductTaxesDto> ArmarAsync(IApplicationDbContext db, Product producto, DateOnly hoy, CancellationToken ct)
    {
        var concepto = producto.WithholdingConceptId is int c
            ? await db.WithholdingConcepts.AsNoTracking().Where(x => x.Id == c).Select(x => new CatalogRefDto(x.PublicId, x.Code, x.Name)).FirstOrDefaultAsync(ct)
            : null;
        return new ProductTaxesDto(producto.VatSaleTreatment, concepto, await VistaDeProductos.ImpuestosAsync(db, producto.Id, hoy, ct));
    }
}
