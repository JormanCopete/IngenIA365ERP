using IngenIA365ERP.Application.Common.Personas;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Prellenar la factura del proveedor desde su XML (feature 012, T345; api.md §14.4, <c>POST /supplier-invoices/prefill</c>):
/// es una <b>consulta</b> (sin clave de idempotencia) y el archivo <b>no se guarda</b> (T41, E10): se lee en memoria con
/// <see cref="LectorDeFacturaUbl"/> y se descarta. Devuelve el proveedor —con su persona si ya existe, por el NIT— y cada
/// línea con el producto sugerido. (nuevo)
/// <para>
/// 2026-11-14 (compra directa desde el XML): el producto lo sugiere <see cref="EquivalenciasDelProveedor"/> —la equivalencia recordada del
/// proveedor primero, luego el código de barras, la referencia y el código interno, éstos dos sólo si son de un único producto—, con la
/// unidad en que viene la línea y si el producto tiene la unidad del XML. <see cref="SupplierPublicId"/> es el proveedor que la persona ya
/// eligió (manda sobre el NIT del XML). Si ese proveedor ya tiene registrada la factura, o el CUFE ya está registrado,
/// <c>AlreadyRegistered</c> lo dice.
/// </para>
/// </summary>
public sealed record PrefillSupplierInvoiceQuery(byte[] Contenido, Guid? SupplierPublicId = null) : IRequest<Result<SupplierInvoicePrefillDto>>;

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
        // Feature 017: el XML no trae el tipo en lo que lee el lector; si el número es de varias personas (CC y CE del mismo
        // número) no se sugiere ninguna y la persona elige el proveedor en la pantalla.
        Guid? persona = null;
        if (nit.Length > 0)
        {
            var encontradas = await ResolutorDePersonaPorDocumento.Filtrar(db.People.AsNoTracking(), null, f.SupplierTaxId)
                .Select(p => new { p.PublicId, p.IdType }).Take(5).ToListAsync(ct);
            var elegida = ResolutorDePersonaPorDocumento.Elegir(encontradas, p => p.IdType);
            persona = elegida.IsSuccess ? elegida.Value?.PublicId : null;
        }

        var proveedorPublico = request.SupplierPublicId ?? persona;
        int? proveedorId = proveedorPublico is { } pp
            ? await db.People.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;

        var sugerencias = await EquivalenciasDelProveedor.SugerirAsync(db, proveedorId, f.Lines, ct);
        var lineas = f.Lines.Select((l, i) =>
        {
            var s = sugerencias[i];
            return new PrefillLineDto(l.SupplierItemCode, l.Description, l.Quantity, l.UnitCode, l.UnitPrice, l.Discount,
                l.Taxes.Select(t => new PrefillTaxDto(t.Code, t.Rate, t.Amount)).ToList(), s?.ProductPublicId)
            {
                Barcode = l.Barcode,
                SuggestedUnitPublicId = s?.UnitPublicId,
                SuggestedProductKind = s?.Kind,
                MatchedBy = s?.MatchedBy,
                UnitMatched = s?.UnitMatched ?? true,
            };
        }).ToList();

        // La factura ya registrada: por número si hay proveedor, por CUFE siempre (aunque el NIT no exista todavía).
        var registrada = await ColisionDeFacturaDeProveedor.BuscarAsync(db, new SupplierInvoiceDetail
        {
            DocumentClass = DocumentClass.SupplierInvoice,
            SupplierPersonId = proveedorId ?? 0,
            SupplierPrefix = SupplierInvoiceDetail.NormalizarNumero(f.Prefix),
            SupplierNumber = SupplierInvoiceDetail.NormalizarNumero(f.Number),
            Cufe = SupplierInvoiceDetail.NormalizarCufe(f.Cufe),
        }, ct);

        return Result.Success(new SupplierInvoicePrefillDto(
            new PrefillSupplierDto(f.SupplierTaxId, f.SupplierName, persona),
            f.Prefix, f.Number, f.Cufe, f.IssueDate, f.DueDate, f.PaymentForm, lineas, f.Totals, f.Warnings ?? [])
        {
            AlreadyRegistered = registrada?.Message,
        });
    }
}
