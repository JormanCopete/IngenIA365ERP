using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// El código que tendría un producto nuevo (<c>GET /api/inventory/products/code-proposal?categoryPublicId=&amp;brandPublicId=</c>;
/// codificación automática, 2026-11-12). La pantalla del alta lo pide al abrir y cada vez que cambian la categoría o la marca: con
/// <c>Manual</c> el campo se digita; con <c>Automatica</c> queda deshabilitado y muestra este código. Es una propuesta: no bloquea ni
/// escribe, y si otro usuario toma el número antes de guardar, el alta asigna el siguiente. Lo que impide armarlo (falta la marca,
/// la categoría ya usó sus números) no es un error de la consulta: va en <c>message</c> sin código. (nuevo)
/// </summary>
public sealed record GetProductCodeProposalQuery(Guid? CategoryPublicId, Guid? BrandPublicId) : IRequest<Result<ProductCodeProposalDto>>;

/// <summary>
/// <c>mode</c> es <c>Manual</c> o <c>Automatica</c>; <c>code</c> el propuesto (nulo en manual o si no se puede armar); <c>segment</c> el
/// número de la marca en la categoría y <c>segmentIsNew</c> si se asignará al guardar; <c>message</c>, lo que la pantalla avisa.
/// </summary>
public sealed record ProductCodeProposalDto(string Mode, string? Code, string? Segment, bool SegmentIsNew, string? Message);

/// <summary>Los dos campos son opcionales: sin categoría la pantalla sólo pregunta el modo.</summary>
public sealed class GetProductCodeProposalQueryValidator : AbstractValidator<GetProductCodeProposalQuery>
{
}

public sealed class GetProductCodeProposalQueryHandler(IApplicationDbContext db, CodificacionDeProductos codificacion, IDateTimeService reloj)
    : IRequestHandler<GetProductCodeProposalQuery, Result<ProductCodeProposalDto>>
{
    public async Task<Result<ProductCodeProposalDto>> Handle(GetProductCodeProposalQuery request, CancellationToken ct)
    {
        var reglas = await codificacion.ReglasAsync(reloj.HoyLocal, ct);
        if (!reglas.Automatica)
            return Result.Success(new ProductCodeProposalDto(ParametrosDeInventario.CodificacionManual, null, null, false, null));

        const string automatica = ParametrosDeInventario.CodificacionAutomatica;
        if (request.CategoryPublicId is not { } categoriaPublica)
            return Result.Success(new ProductCodeProposalDto(automatica, null, null, false,
                "El código se arma solo con la categoría, la marca y el consecutivo: elija la categoría y la marca."));

        var categoria = await db.ProductCategories.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == categoriaPublica, ct);
        if (categoria is null) return Result.Failure<ProductCodeProposalDto>(CatalogErrors.CategoryNotFound());

        Domain.Entities.Inventory.Catalog.Brand? marca = null;
        if (request.BrandPublicId is { } marcaPublica)
        {
            marca = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == marcaPublica, ct);
            if (marca is null) return Result.Failure<ProductCodeProposalDto>(CatalogErrors.BrandNotFound());
        }

        var armado = await codificacion.ProponerAsync(categoria, marca, reglas, ct);
        if (armado.IsFailure)
            return Result.Success(new ProductCodeProposalDto(automatica, null, null, false, armado.Error.Message));

        var aviso = armado.Value.SegmentoNuevo
            ? $"La marca {marca!.Name} todavía no tiene número en la categoría {categoria.Name}: al guardar se le asigna el {armado.Value.Segmento}."
            : null;
        return Result.Success(new ProductCodeProposalDto(automatica, armado.Value.Codigo, armado.Value.Segmento, armado.Value.SegmentoNuevo, aviso));
    }
}
