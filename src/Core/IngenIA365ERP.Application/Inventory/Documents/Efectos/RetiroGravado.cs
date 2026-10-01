using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// El retiro gravado (feature 012, I3, T615; FR-037; quickstart.md §5.12): el consumo interno de un tipo marcado
/// <c>IsTaxableWithdrawal</c> lleva base e IVA además del costo. La base es el precio de la lista <b>general</b> vigente a la fecha
/// (<see cref="ResolucionDePrecios.GeneralAsync"/>, el mismo resolutor de <c>ResolvePriceQuery</c>) por la cantidad de cada línea, sin
/// impuestos si la lista los incluye; el IVA es el que el producto genera en venta, calculado por <see cref="MotorTributario"/> con la
/// cooperativa en las dos puntas. Sin precio en la lista general → <c>Inventory.Price.NotFound</c>, que dice qué producto y unidad faltan.
/// Lo consume <see cref="EfectoDeConsumoInterno"/>: el mensaje <c>AjusteInventarioAprobado</c> sale con operación <c>RetiroGravado</c> y su
/// bloque <see cref="TaxableWithdrawalV1"/>. (nuevo)
/// </summary>
public sealed class RetiroGravado(IApplicationDbContext db, LectorDeCatalogoTributario catalogo)
{
    public async Task<Result<TaxableWithdrawalV1>> CalcularAsync(InventoryDocument documento, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted && l.Quantity > 0m).OrderBy(l => l.LineNumber).ToList();
        var foto = await catalogo.FotoAsync(documento.OperationDate, ct);
        if (foto.IsFailure) return Result.Failure<TaxableWithdrawalV1>(foto.Error);

        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.VatSaleTreatment }).ToDictionaryAsync(p => p.Id, ct);
        var unidadIds = vivas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);
        var impuestos = (await db.ProductTaxes.AsNoTracking().Where(t => productoIds.Contains(t.ProductId))
                .Select(t => new { t.ProductId, t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit }).ToListAsync(ct))
            .ToLookup(t => t.ProductId, t => new ImpuestoDeLinea(t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit));

        Guid? lista = null;
        var lineas = new List<LineaTributaria>(vivas.Count);
        var conImpuestos = new HashSet<int>();
        foreach (var l in vivas)
        {
            var resuelto = await ResolucionDePrecios.GeneralAsync(db, documento.OperationDate, l.ProductId, l.UnitId, ct);
            if (!resuelto.Precio.Found)
                return Result.Failure<TaxableWithdrawalV1>(ErroresDePrecios.PriceNotFound(productos.GetValueOrDefault(l.ProductId)?.Code ?? "?",
                    unidades.GetValueOrDefault(l.UnitId) ?? "?"));
            lista ??= resuelto.PriceListPublicId;
            if (resuelto.Precio.IncludesTaxes) conImpuestos.Add(l.LineNumber);
            lineas.Add(new LineaTributaria(l.LineNumber, Math.Round(l.Quantity * resuelto.Precio.Price!.Value, 2, MidpointRounding.AwayFromZero), l.QuantityBase,
                productos.GetValueOrDefault(l.ProductId)?.VatSaleTreatment ?? VatSaleTreatment.Taxed, impuestos[l.ProductId].ToList()));
        }

        // Con una lista que incluye impuestos, la base es el precio sin el IVA que el motor aplicaría a esa línea.
        if (conImpuestos.Count > 0)
        {
            var prueba = Calcular(foto.Value, documento.OperationDate, lineas);
            lineas = lineas.Select(l => conImpuestos.Contains(l.Numero)
                ? l with { Base = Math.Round(l.Base / (1m + prueba.Renglones.Where(r => r.Linea == l.Numero && Iva(r)).Sum(r => r.Rate ?? 0m)), 2, MidpointRounding.AwayFromZero) }
                : l).ToList();
        }
        var resultado = Calcular(foto.Value, documento.OperationDate, lineas);
        if (resultado.Rechazado) return Result.Failure<TaxableWithdrawalV1>(new Error(resultado.Rechazos[0].Codigo, resultado.Rechazos[0].Mensaje));

        var iva = resultado.Renglones.Where(Iva).ToList();
        var definiciones = iva.Select(r => r.TaxDefinitionId).Distinct().ToList();
        var codigos = await db.TaxDefinitions.AsNoTracking().Where(t => definiciones.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        return Result.Success(new TaxableWithdrawalV1
        {
            PriceListPublicId = lista ?? Guid.Empty,
            Base = lineas.Sum(l => l.Base),
            Taxes = iva.GroupBy(r => (r.TaxDefinitionId, r.TaxRateCode, r.Kind, r.Rate))
                .Select(g => new TaxLineV1
                {
                    TaxCode = codigos.GetValueOrDefault(g.Key.TaxDefinitionId) ?? string.Empty,
                    TaxKind = g.Key.Kind,
                    TaxRateCode = g.Key.TaxRateCode,
                    Rate = g.Key.Rate,
                    Treatment = TaxTreatment.Generated,
                    TaxableBase = g.Sum(r => r.Base),
                    Amount = g.Sum(r => r.Amount),
                    DocumentLines = g.Select(r => r.Linea).OfType<int>().Distinct().Order().ToList(),
                }).ToList(),
        });
    }

    private static bool Iva(RenglonTributario r) => r.Kind == TaxKind.Iva && r.Treatment == TaxTreatment.Generated;

    private static ResultadoTributario Calcular(TaxCatalogSnapshot foto, DateOnly fecha, IReadOnlyList<LineaTributaria> lineas) =>
        MotorTributario.Calcular(foto, new EntradaTributaria
        {
            Fecha = fecha,
            Perspectiva = TaxAppliesTo.Sales,
            Vendedor = foto.Cooperativa,
            Comprador = foto.Cooperativa,
            Lineas = lineas,
        });
}
