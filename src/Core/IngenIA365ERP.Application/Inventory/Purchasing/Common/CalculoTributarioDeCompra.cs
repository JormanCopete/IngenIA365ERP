using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>Los totales de una compra (T26): <c>AmountDue = Total − WithholdingTotal</c>. (nuevo)</summary>
public sealed record TotalesDeCompra(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue)
{
    public static TotalesDeCompra Cero { get; } = new(0m, 0m, 0m, 0m, 0m, 0m);
}

/// <summary>
/// Lo que devuelve el cálculo: los renglones del motor (por línea y de documento), los totales, las omisiones (lo que se
/// evaluó y no aplicó, con su razón) y si la cooperativa es responsable de IVA a la fecha (lo necesita el costo de
/// entrada). (nuevo)
/// </summary>
public sealed record CalculoDeCompra(
    IReadOnlyList<RenglonTributario> Renglones,
    TotalesDeCompra Totales,
    IReadOnlyList<string> Omisiones,
    bool CooperativaResponsableIva)
{
    /// <summary>Los impuestos de la línea <paramref name="numero"/> (sin retenciones).</summary>
    public IEnumerable<RenglonTributario> DeLinea(int numero) => Renglones.Where(r => r.Linea == numero && !r.EsRetencion);

    /// <summary>Lo que los impuestos de la línea sumaron al costo (IVA no descontable, INC).</summary>
    public decimal AlCostoDeLinea(int numero) => DeLinea(numero).Where(r => r.Treatment == TaxTreatment.AddedToCost).Sum(r => r.Amount);
}

/// <summary>
/// Los impuestos y retenciones de un documento de compra (feature 012, T338; FR-013, FR-044, FR-050; api.md §14.1; T22, T24,
/// T26). Arma lo que <see cref="MotorTributario"/> necesita y lo llama:
/// <list type="bullet">
/// <item>el perfil tributario del <b>proveedor</b> (sujeto: las marcas de <c>COR_People</c>) y el de la <b>cooperativa</b>
/// (agente: los parámetros <c>TAX</c> vigentes, en la foto de <see cref="LectorDeCatalogoTributario"/>);</item>
/// <item>por línea, la base neta de descuentos no condicionados, la cantidad en unidad base, cómo se vende el producto
/// (decide si el IVA es descontable), sus impuestos de compra (<c>INV_ProductTaxes</c>) y su concepto de retención;</item>
/// <item>el catálogo, las tarifas y la UVT a la <b>fecha de operación</b> (sin UVT vigente, <c>Taxation.Uvt.Missing</c>) y el
/// municipio de la operación (<c>OperationMunicipalityDaneCode</c>) para ReteICA;</item>
/// <item>en notas y devoluciones, la foto del original en proporción y sin volver a probar la base mínima (E9).</item>
/// </list>
/// Devuelve renglones y totales: <c>Total = Subtotal − Descuentos + Impuestos</c> y <c>AmountDue = Total − Retenciones</c>.
/// Un impuesto del producto cuya tarifa no está vigente responde <c>Inventory.ProductTax.RateNotInForce</c>. Sin un solo
/// valor legal escrito aquí (<c>ElComercioNoTieneValoresLegalesFijos</c>). (nuevo)
/// </summary>
public sealed class CalculoTributarioDeCompra(IApplicationDbContext db, LectorDeCatalogoTributario catalogo)
{
    /// <summary>
    /// Calcula sobre las líneas vivas de <paramref name="documento"/>. <paramref name="original"/>: en una nota, los renglones
    /// de la factura; <paramref name="lineaOriginal"/> da, para cada línea de la nota, la línea de la factura que corrige.
    /// </summary>
    public async Task<Result<CalculoDeCompra>> CalcularAsync(
        InventoryDocument documento,
        InventoryDocumentType tipo,
        CancellationToken ct,
        IReadOnlyList<RenglonTributario>? original = null,
        IReadOnlyDictionary<int, int>? lineaOriginal = null)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var subtotal = vivas.Sum(l => l.GrossAmount);
        var descuentos = vivas.Sum(l => l.DiscountAmount);
        if (vivas.Count == 0)
            return Result.Success(new CalculoDeCompra([], new TotalesDeCompra(subtotal, descuentos, 0m, 0m, subtotal - descuentos, subtotal - descuentos), [], false));

        var foto = await catalogo.FotoAsync(documento.OperationDate, ct);
        if (foto.IsFailure) return Result.Failure<CalculoDeCompra>(foto.Error);

        var proveedor = documento.CounterpartyPersonId is int personaId
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct)
            : null;

        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.VatSaleTreatment, p.WithholdingConceptId })
            .ToDictionaryAsync(p => p.Id, ct);
        var impuestos = await db.ProductTaxes.AsNoTracking().Where(t => productoIds.Contains(t.ProductId))
            .Select(t => new { t.ProductId, t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit })
            .ToListAsync(ct);

        // La tarifa fija del producto tiene que estar vigente a la fecha para compras (si no, el motor la omitiría callado).
        foreach (var linea in vivas.Where(_ => original is null))
        {
            foreach (var i in impuestos.Where(i => i.ProductId == linea.ProductId && i.TaxRateCode is not null && i.AppliesTo != TaxAppliesTo.Sales))
            {
                var definicion = foto.Value.Impuesto(i.TaxDefinitionId);
                if (definicion is null || definicion.IsWithholding) continue;
                var vigente = foto.Value.Tarifas.Any(t => t.TaxDefinitionId == i.TaxDefinitionId && t.VigenteEn(documento.OperationDate)
                    && t.AplicaA(TaxAppliesTo.Purchases) && string.Equals(t.Code, i.TaxRateCode, StringComparison.OrdinalIgnoreCase));
                if (!vigente) return Result.Failure<CalculoDeCompra>(ErroresDeCompras.RateNotInForce(linea.LineNumber, definicion.Code, documento.OperationDate));
            }
        }

        var entrada = new EntradaTributaria
        {
            Fecha = documento.OperationDate,
            Perspectiva = TaxAppliesTo.Purchases,
            Vendedor = Perfil(proveedor),
            Comprador = foto.Value.Cooperativa,
            MunicipioDane = documento.OperationMunicipalityDaneCode,
            TipoIvaNoDescontable = tipo.VatNonDeductible,
            Original = original,
            Lineas = vivas.Select(l =>
            {
                var producto = productos.GetValueOrDefault(l.ProductId);
                return new LineaTributaria(
                    l.LineNumber,
                    l.NetAmount,
                    l.QuantityBase,
                    producto?.VatSaleTreatment ?? VatSaleTreatment.Taxed,
                    impuestos.Where(i => i.ProductId == l.ProductId)
                        .Select(i => new ImpuestoDeLinea(i.TaxDefinitionId, i.TaxRateCode, i.AppliesTo, i.TaxableUnitsPerBaseUnit)).ToList(),
                    producto?.WithholdingConceptId,
                    lineaOriginal is not null && lineaOriginal.TryGetValue(l.LineNumber, out var o) ? o : null);
            }).ToList(),
        };

        var resultado = MotorTributario.Calcular(foto.Value, entrada);
        if (resultado.Rechazado)
        {
            var rechazo = resultado.Rechazos[0];
            return Result.Failure<CalculoDeCompra>(ErroresDeCompras.DelMotor(rechazo.Codigo, rechazo.Mensaje));
        }

        var impuestosTotal = resultado.TotalDeImpuestos;
        var retenciones = resultado.TotalDeRetenciones;
        var total = subtotal - descuentos + impuestosTotal;
        return Result.Success(new CalculoDeCompra(resultado.Renglones,
            new TotalesDeCompra(subtotal, descuentos, impuestosTotal, retenciones, total, total - retenciones),
            resultado.Omisiones, foto.Value.Cooperativa.IsVatResponsible));
    }

    /// <summary>Escribe los totales del cálculo en la cabecera (T26).</summary>
    public static void AplicarTotales(InventoryDocument documento, TotalesDeCompra totales)
    {
        documento.Subtotal = totales.Subtotal;
        documento.DiscountTotal = totales.DiscountTotal;
        documento.TaxTotal = totales.TaxTotal;
        documento.WithholdingTotal = totales.WithholdingTotal;
        documento.Total = totales.Total;
        documento.AmountDue = totales.AmountDue;
    }

    /// <summary>
    /// La foto de <c>INV_DocumentTaxLines</c> de un renglón: se escribe al confirmar. <paramref name="lineaId"/> es la línea del
    /// documento del renglón (nula en las retenciones).
    /// </summary>
    public static DocumentTaxLine Foto(int documentoId, int? lineaId, RenglonTributario r) => new()
    {
        DocumentId = documentoId,
        DocumentLineId = lineaId,
        TaxDefinitionId = r.TaxDefinitionId,
        TaxRateId = r.TaxRateId,
        TaxRateCode = r.TaxRateCode,
        Kind = r.Kind,
        Treatment = r.Treatment,
        WithholdingConceptId = r.WithholdingConceptId,
        MunicipalityDaneCode = r.MunicipalityDaneCode,
        Rate = r.Rate,
        AmountPerUnit = r.AmountPerUnit,
        TaxableUnits = r.TaxableUnits,
        Base = r.Base,
        Amount = r.Amount,
        DianTaxCode = r.DianTaxCode,
        ExplanationJson = JsonSerializer.Serialize(r.Explicacion),
    };

    /// <summary>
    /// Los renglones de una foto guardada, para una nota o una devolución (E9): la línea es el <c>LineNumber</c> del original.
    /// </summary>
    public static IReadOnlyList<RenglonTributario> DesdeLaFoto(IEnumerable<DocumentTaxLine> foto, IReadOnlyDictionary<int, int> numeroDeLinea) =>
        foto.Select(t => new RenglonTributario(
                t.DocumentLineId is int id && numeroDeLinea.TryGetValue(id, out var n) ? n : null,
                t.TaxDefinitionId, t.TaxRateId, t.TaxRateCode, t.Kind, t.Treatment, t.WithholdingConceptId, t.MunicipalityDaneCode,
                t.Rate, t.AmountPerUnit, t.TaxableUnits, t.Base, t.Amount, t.DianTaxCode, new ExplicacionTributaria()))
            .ToList();

    /// <summary>El perfil tributario del proveedor, con las marcas de <c>COR_People</c> (T172). Sin proveedor, un perfil vacío.</summary>
    public static PerfilTributario Perfil(Person? persona) => persona is null
        ? new PerfilTributario()
        : new PerfilTributario
        {
            PersonType = persona.PersonType,
            IsVatResponsible = persona.IsVatResponsible,
            IsIncomeTaxFiler = persona.IsIncomeTaxFiler,
            IsLargeContributor = persona.IsLargeContributor,
            IsSelfWithholder = persona.IsSelfWithholder,
            IsSimpleTaxRegime = persona.IsSimpleTaxRegime,
            IsVatWithholdingAgent = persona.IsVatWithholdingAgent,
            WithholdingExempt = persona.WithholdingExempt,
            IcaWithholdingExempt = persona.IcaWithholdingExempt,
            CiiuCode = persona.CiiuCode,
        };
}
