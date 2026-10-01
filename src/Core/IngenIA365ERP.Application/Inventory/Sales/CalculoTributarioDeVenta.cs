using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Los impuestos y las retenciones de un documento de venta ya precificado (feature 012, I3, T611, T612; FR-013; T22, T26): el mismo
/// <see cref="MotorTributario"/> que usa <see cref="PrecificacionDeVenta"/>, en perspectiva de venta (vendedor = la cooperativa,
/// comprador = el perfil de la contraparte, sus retenciones son <c>WithholdingSuffered</c>) y con el municipio de la sucursal. Sobre las
/// líneas vivas del documento —la base neta que ya dejó la precificación—, así lo que se guarda al confirmar es lo que la persona
/// vio. En una nota, la foto del original en proporción y sin volver a probar la base mínima (E9). No escribe nada:
/// <see cref="Foto"/> arma las filas de <c>INV_DocumentTaxLines</c>. (nuevo)
/// </summary>
public sealed class CalculoTributarioDeVenta(IApplicationDbContext db, LectorDeCatalogoTributario catalogo, ILectorDeParametros? parametros = null)
{
    /// <summary>Los renglones del documento y sus totales (T26). <paramref name="lineaOriginal"/>: línea de la nota → número de línea del original.</summary>
    public async Task<Result<(IReadOnlyList<RenglonTributario> Renglones, TotalesDeVenta Totales)>> CalcularAsync(
        InventoryDocument documento, CancellationToken ct,
        IReadOnlyList<RenglonTributario>? original = null, IReadOnlyDictionary<int, int>? lineaOriginal = null)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var subtotal = vivas.Sum(l => l.GrossAmount);
        var descuentos = vivas.Sum(l => l.DiscountAmount);
        if (vivas.Count == 0)
            return Result.Success<(IReadOnlyList<RenglonTributario>, TotalesDeVenta)>(([], new TotalesDeVenta(subtotal, descuentos, 0m, 0m, subtotal - descuentos, subtotal - descuentos)));

        var foto = await catalogo.FotoAsync(documento.OperationDate, ct);
        if (foto.IsFailure) return Result.Failure<(IReadOnlyList<RenglonTributario>, TotalesDeVenta)>(foto.Error);
        var comprador = documento.CounterpartyPersonId is int personaId
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct)
            : null;
        var municipio = await db.Branches.AsNoTracking().Where(b => b.Id == documento.BranchId).Select(b => b.MunicipalityDaneCode).FirstOrDefaultAsync(ct);

        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.VatSaleTreatment, p.WithholdingConceptId })
            .ToDictionaryAsync(p => p.Id, ct);
        var impuestos = (await db.ProductTaxes.AsNoTracking().Where(t => productoIds.Contains(t.ProductId))
                .Select(t => new { t.ProductId, t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit })
                .ToListAsync(ct))
            .ToLookup(t => t.ProductId, t => new ImpuestoDeLinea(t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit));

        // La precificación calcula el impuesto antes de sumar al bruto el residuo de una lista con impuestos (FR-017): la base
        // gravable es el neto sin ese residuo, para que lo guardado sea exactamente lo que la persona vio.
        var montos = RedondeoDeMontos.Centavo;
        if (original is null && parametros is not null)
        {
            var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, documento.OperationDate, ct: ct);
            if (leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto)) montos = Redondeo.MontosDesde(leido.Value.Texto);
        }
        decimal BaseGravable(InventoryDocumentLine l) => original is null && l.ListPriceIncludesTaxes && l.DiscountAmount == 0m
            ? l.NetAmount - (l.GrossAmount - Redondeo.Monto(l.Quantity * l.UnitPrice, montos))
            : l.NetAmount;

        var resultado = MotorTributario.Calcular(foto.Value, new EntradaTributaria
        {
            Fecha = documento.OperationDate,
            Perspectiva = TaxAppliesTo.Sales,
            Vendedor = foto.Value.Cooperativa,
            Comprador = CalculoTributarioDeCompra.Perfil(comprador),
            MunicipioDane = municipio,
            Original = original,
            Lineas = vivas.Select(l =>
            {
                var p = productos.GetValueOrDefault(l.ProductId);
                return new LineaTributaria(l.LineNumber, BaseGravable(l), l.QuantityBase, p?.VatSaleTreatment ?? VatSaleTreatment.Taxed,
                    impuestos[l.ProductId].ToList(), p?.WithholdingConceptId,
                    lineaOriginal is not null && lineaOriginal.TryGetValue(l.LineNumber, out var o) ? o : null);
            }).ToList(),
        });
        if (resultado.Rechazado)
            return Result.Failure<(IReadOnlyList<RenglonTributario>, TotalesDeVenta)>(new Error(resultado.Rechazos[0].Codigo, resultado.Rechazos[0].Mensaje));

        var impuestosTotal = resultado.Renglones.Where(r => !r.EsRetencion && r.Treatment == TaxTreatment.Generated).Sum(r => r.Amount);
        var retenciones = resultado.Renglones.Where(r => r.EsRetencion).Sum(r => r.Amount);
        var total = subtotal - descuentos + impuestosTotal;
        return Result.Success<(IReadOnlyList<RenglonTributario>, TotalesDeVenta)>(
            (resultado.Renglones, new TotalesDeVenta(subtotal, descuentos, impuestosTotal, retenciones, total, total - retenciones)));
    }

    /// <summary>Escribe los totales en la cabecera (T26): <c>Total = Subtotal − Descuentos + Impuestos</c>; <c>AmountDue = Total − Retenciones</c>.</summary>
    public static void AplicarTotales(InventoryDocument documento, TotalesDeVenta t)
    {
        documento.Subtotal = t.Subtotal;
        documento.DiscountTotal = t.DiscountTotal;
        documento.TaxTotal = t.TaxTotal;
        documento.WithholdingTotal = t.WithholdingTotal;
        documento.Total = t.Total;
        documento.AmountDue = t.AmountDue;
    }

    /// <summary>Las filas de <c>INV_DocumentTaxLines</c> de los renglones (se escriben al confirmar; T22).</summary>
    public static IReadOnlyList<DocumentTaxLine> Foto(InventoryDocument documento, IReadOnlyList<RenglonTributario> renglones)
    {
        var porNumero = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber, l => l.Id);
        return renglones.Select(r => CalculoTributarioDeCompra.Foto(documento.Id,
            r.Linea is int n && porNumero.TryGetValue(n, out var id) ? id : null, r)).ToList();
    }
}
