using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Lo que el puerto responde de una referencia: el valor canónico que se guarda en la línea y cómo se muestra. Con
/// <paramref name="FaltaComplemento"/>, la referencia sirve pero pide un segundo dato (2026-11-26: el vehículo externo pide su placa además
/// del lateral) y el valor todavía no se puede confirmar: se completa digitando «referencia/complemento» (<see cref="ReferenciaDeLinea.Separador"/>).
/// (nuevo)
/// </summary>
public sealed record ReferenciaDeLineaResuelta(string Valor, string Etiqueta, string? FaltaComplemento = null);

/// <summary>
/// Un producto cuya línea de venta pide referencia y cómo se rotula el campo («Placa o lateral»); <paramref name="RotuloDelComplemento"/> es
/// el rótulo del segundo dato cuando la referencia lo pide («Placa del vehículo externo»). (nuevo)
/// </summary>
public sealed record ProductoConReferencia(Guid ProductPublicId, string Rotulo, string? RotuloDelComplemento = null);

/// <summary>
/// El puerto por el que otro módulo pide que la línea de venta de ciertos productos diga <b>qué</b> se vende (2026-11-18, feature 020, pedido
/// del dueño: en COOFLOPAL la radio y los demás servicios se venden en el POS y en la factura de oficina con la placa o el lateral del
/// vehículo). Comercial no sabe qué es la referencia: pregunta qué productos la piden, la hace validar y, ya confirmada la venta, avisa en la
/// misma transacción. Lo implementa Transporte (<c>VentasDeServiciosDeVehiculo</c>); sin implementación registrada, ningún producto la pide.
/// (nuevo)
/// </summary>
public interface IReferenciaDeLineaDeVenta
{
    /// <summary>Los productos cuyas líneas de venta piden referencia.</summary>
    Task<IReadOnlyList<ProductoConReferencia>> ProductosQueLaPidenAsync(CancellationToken ct);

    /// <summary>Lo que se digitó (la placa o el lateral) convertido al valor canónico, o el error que dice por qué no sirve.</summary>
    Task<Result<ReferenciaDeLineaResuelta>> ResolverAsync(Guid productoPublicId, string texto, CancellationToken ct);

    /// <summary>
    /// La venta quedó confirmada (ya guardada, en la misma transacción): registra lo suyo. <c>true</c> si agregó algo que hay que guardar.
    /// </summary>
    Task<Result<bool>> AlConfirmarVentaAsync(Guid documentoPublicId, CancellationToken ct);
}

/// <summary>Las reglas de Comercial sobre la referencia de la línea, en un solo sitio. (nuevo)</summary>
public static class ReferenciaDeLinea
{
    private static readonly HashSet<DocumentClass> Ventas =
    [
        DocumentClass.SalesInvoice, DocumentClass.SalesInvoiceFromShipments, DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt,
    ];

    /// <summary>
    /// Separa la referencia de su complemento en el valor guardado y en lo digitado («618/ABC123»): Comercial no sabe qué es cada parte, sólo
    /// guarda lo que el puerto devuelve. (2026-11-26)
    /// </summary>
    public const char Separador = '/';

    /// <summary>¿La clase es una venta que confirma lo vendido (factura, documento POS, comprobante)?</summary>
    public static bool EsVenta(DocumentClass clase) => Ventas.Contains(clase);

    /// <summary>
    /// Al confirmar una venta: cada línea viva de un producto que pide referencia la trae y es válida; la deja en su valor canónico (sin
    /// guardar). El primer error, o nulo.
    /// </summary>
    public static async Task<Error?> ValidarAsync(IApplicationDbContext db, IEnumerable<IReferenciaDeLineaDeVenta> puertos, InventoryDocument documento,
        CancellationToken ct)
    {
        if (!EsVenta(documento.Class)) return null;
        foreach (var puerto in puertos)
        {
            var piden = await puerto.ProductosQueLaPidenAsync(ct);
            if (piden.Count == 0) continue;
            var publicos = piden.Select(p => p.ProductPublicId).ToList();
            var productos = await db.Products.AsNoTracking().Where(p => publicos.Contains(p.PublicId))
                .Select(p => new { p.Id, p.PublicId, p.Code }).ToListAsync(ct);
            foreach (var linea in documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber))
            {
                var producto = productos.FirstOrDefault(p => p.Id == linea.ProductId);
                if (producto is null) continue;
                var rotulo = piden.First(p => p.ProductPublicId == producto.PublicId).Rotulo;
                if (string.IsNullOrWhiteSpace(linea.ItemReference)) return ErroresDeReferenciaDeLinea.Required(linea.LineNumber, producto.Code, rotulo);
                var resuelta = await puerto.ResolverAsync(producto.PublicId, linea.ItemReference, ct);
                if (resuelta.IsFailure) return ErroresDeReferenciaDeLinea.Invalid(linea.LineNumber, producto.Code, resuelta.Error.Message);
                if (resuelta.Value.FaltaComplemento is { } falta)
                    return ErroresDeReferenciaDeLinea.ComplementRequired(linea.LineNumber, producto.Code, falta, resuelta.Value.Etiqueta);
                linea.ItemReference = resuelta.Value.Valor;
            }
        }
        return null;
    }
}

/// <summary>Los errores de la referencia de la línea. (nuevo)</summary>
public static class ErroresDeReferenciaDeLinea
{
    public static Error Required(int lineNumber, string productCode, string rotulo) => new ErrorConDatos("Inventory.Line.ReferenceRequired",
        $"Línea {lineNumber} ({productCode}): falta {rotulo.ToLowerInvariant()}.", new { lineNumber, productCode, label = rotulo });

    public static Error ComplementRequired(int lineNumber, string productCode, string rotulo, string referencia) => new ErrorConDatos(
        "Inventory.Line.ReferenceComplementRequired",
        $"Línea {lineNumber} ({productCode}): falta {rotulo.ToLowerInvariant()} ({referencia}).", new { lineNumber, productCode, label = rotulo });

    public static Error Invalid(int lineNumber, string productCode, string motivo) => new ErrorConDatos("Inventory.Line.ReferenceInvalid",
        $"Línea {lineNumber} ({productCode}): {motivo}", new { lineNumber, productCode });
}

/// <summary>
/// Los productos cuyas líneas de venta piden referencia (<c>GET /api/inventory/sales/line-references/products</c>): la pantalla pide la placa
/// o el lateral al agregar uno. Vacío si ningún módulo la pide. (nuevo, 2026-11-18)
/// </summary>
public sealed record ListLineReferenceProductsQuery : MediatR.IRequest<Result<IReadOnlyList<ProductoConReferencia>>>;

public sealed class ListLineReferenceProductsQueryValidator : FluentValidation.AbstractValidator<ListLineReferenceProductsQuery>;

public sealed class ListLineReferenceProductsQueryHandler(IEnumerable<IReferenciaDeLineaDeVenta> puertos)
    : MediatR.IRequestHandler<ListLineReferenceProductsQuery, Result<IReadOnlyList<ProductoConReferencia>>>
{
    public async Task<Result<IReadOnlyList<ProductoConReferencia>>> Handle(ListLineReferenceProductsQuery request, CancellationToken ct)
    {
        var todos = new List<ProductoConReferencia>();
        foreach (var puerto in puertos) todos.AddRange(await puerto.ProductosQueLaPidenAsync(ct));
        return Result.Success<IReadOnlyList<ProductoConReferencia>>(todos.DistinctBy(p => p.ProductPublicId).ToList());
    }
}

/// <summary>
/// Lo digitado como referencia de la línea de un producto (<c>GET /api/inventory/sales/line-references/resolve?product=&amp;q=</c>): el valor
/// canónico y cómo se muestra, o el error (no existe, ambiguo). (nuevo, 2026-11-18)
/// </summary>
public sealed record ResolveLineReferenceQuery(Guid ProductPublicId, string Text) : MediatR.IRequest<Result<ReferenciaDeLineaResuelta>>;

public sealed class ResolveLineReferenceQueryValidator : FluentValidation.AbstractValidator<ResolveLineReferenceQuery>
{
    public ResolveLineReferenceQueryValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(40);
    }
}

public sealed class ResolveLineReferenceQueryHandler(IEnumerable<IReferenciaDeLineaDeVenta> puertos)
    : MediatR.IRequestHandler<ResolveLineReferenceQuery, Result<ReferenciaDeLineaResuelta>>
{
    public async Task<Result<ReferenciaDeLineaResuelta>> Handle(ResolveLineReferenceQuery request, CancellationToken ct)
    {
        foreach (var puerto in puertos)
        {
            var piden = await puerto.ProductosQueLaPidenAsync(ct);
            if (piden.Any(p => p.ProductPublicId == request.ProductPublicId)) return await puerto.ResolverAsync(request.ProductPublicId, request.Text, ct);
        }
        return Result.Failure<ReferenciaDeLineaResuelta>(new Error("Inventory.Line.ReferenceNotAsked", "Este producto no pide referencia en la línea."));
    }
}
