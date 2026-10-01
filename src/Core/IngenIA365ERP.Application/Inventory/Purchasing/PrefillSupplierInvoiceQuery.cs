using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Prellenar la factura del proveedor desde su XML (feature 012, T345; api.md §14.4, <c>POST /supplier-invoices/prefill</c>):
/// es una <b>consulta</b> (sin clave de idempotencia) y el archivo <b>no se guarda</b> (T41, E10): se lee en memoria con
/// <see cref="LectorDeFacturaUbl"/> y se descarta. Devuelve el proveedor —con su persona si ya existe, por el NIT— y cada
/// línea con el producto sugerido por el código de barras del XML o por el código del proveedor (la referencia o el código del
/// producto). (nuevo)
/// </summary>
public sealed record PrefillSupplierInvoiceQuery(byte[] Contenido) : IRequest<Result<SupplierInvoicePrefillDto>>;

public sealed class PrefillSupplierInvoiceQueryValidator : AbstractValidator<PrefillSupplierInvoiceQuery>
{
    public PrefillSupplierInvoiceQueryValidator()
    {
        RuleFor(x => x.Contenido).NotNull().Must(c => c.Length > 0).WithMessage("Suba el XML, el AttachedDocument o el ZIP de la factura.");
    }
}

public sealed class PrefillSupplierInvoiceQueryHandler(IApplicationDbContext db, LectorDeFacturaUbl lector)
    : IRequestHandler<PrefillSupplierInvoiceQuery, Result<SupplierInvoicePrefillDto>>
{
    public async Task<Result<SupplierInvoicePrefillDto>> Handle(PrefillSupplierInvoiceQuery request, CancellationToken ct)
    {
        var leida = lector.Leer(request.Contenido);
        if (leida.IsFailure) return Result.Failure<SupplierInvoicePrefillDto>(leida.Error);
        var f = leida.Value;

        var nit = new string(f.SupplierTaxId.Where(char.IsDigit).ToArray());
        var persona = nit.Length == 0
            ? null
            : await db.People.AsNoTracking().Where(p => p.TaxId == nit || p.TaxId == f.SupplierTaxId).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct);

        var barras = f.Lines.Select(l => ProductBarcode.Normalizar(l.Barcode)).Where(b => b.Length > 0).Distinct().ToList();
        var porBarras = await db.ProductBarcodes.AsNoTracking().Where(b => barras.Contains(b.Barcode))
            .Join(db.Products.AsNoTracking(), b => b.ProductId, p => p.Id, (b, p) => new { b.Barcode, p.PublicId })
            .ToListAsync(ct);
        var codigos = f.Lines.Select(l => l.SupplierItemCode?.Trim()).OfType<string>().Where(c => c.Length > 0).Distinct().ToList();
        var porCodigo = await db.Products.AsNoTracking().Where(p => (p.Reference != null && codigos.Contains(p.Reference)) || codigos.Contains(p.Code))
            .Select(p => new { p.Code, p.Reference, p.PublicId }).ToListAsync(ct);

        var lineas = f.Lines.Select(l =>
        {
            var barra = ProductBarcode.Normalizar(l.Barcode);
            var sugerido = porBarras.FirstOrDefault(b => b.Barcode == barra)?.PublicId
                           ?? porCodigo.FirstOrDefault(p => p.Reference == l.SupplierItemCode)?.PublicId
                           ?? porCodigo.FirstOrDefault(p => p.Code == l.SupplierItemCode)?.PublicId;
            return new PrefillLineDto(l.SupplierItemCode, l.Description, l.Quantity, l.UnitCode, l.UnitPrice, l.Discount,
                l.Taxes.Select(t => new PrefillTaxDto(t.Code, t.Rate, t.Amount)).ToList(), sugerido);
        }).ToList();

        return Result.Success(new SupplierInvoicePrefillDto(
            new PrefillSupplierDto(f.SupplierTaxId, f.SupplierName, persona),
            f.Prefix, f.Number, f.Cufe, f.IssueDate, f.DueDate, f.PaymentForm, lineas, f.Totals));
    }
}
