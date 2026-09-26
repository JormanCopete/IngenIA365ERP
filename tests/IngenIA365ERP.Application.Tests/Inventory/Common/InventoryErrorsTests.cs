using System.Reflection;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, T141 (contracts/api.md §2.7, §8, §9.6): el catálogo común de códigos del ciclo de documentos. Los
/// códigos son los del contrato, al pie de la letra, y <c>data</c> trae lo que la persona necesita para corregir.
/// </summary>
public class InventoryErrorsTests
{
    private static IEnumerable<Error> Todos()
    {
        var guid = Guid.NewGuid();
        var hoy = new DateOnly(2026, 9, 25);
        yield return InventoryErrors.DocumentNotFound();
        yield return InventoryErrors.NotDraft(DocumentStatus.Confirmed);
        yield return InventoryErrors.NotConfirmed(DocumentStatus.Draft);
        yield return InventoryErrors.TypeNotForRoute(DocumentClass.PurchaseReceipt, DocumentClassGroup.Purchases);
        yield return InventoryErrors.Empty();
        yield return InventoryErrors.TooManyLines();
        yield return InventoryErrors.FieldRequired("costCenter");
        yield return InventoryErrors.WarehouseNotAllowedForType("B01", "AJP");
        yield return InventoryErrors.TransitNotAllowed("TR01");
        yield return InventoryErrors.DateInFuture(hoy.AddDays(1), hoy);
        yield return InventoryErrors.DateBeforeCutoff(hoy);
        yield return InventoryErrors.AlreadyVoided(guid, "AN-1");
        yield return InventoryErrors.VoidingNotVoidable();
        yield return InventoryErrors.HasDependents([new(guid, "SupplierInvoice", "FP-1", "Confirmed")]);
        yield return InventoryErrors.FiscalUseCorrection();
        yield return InventoryErrors.DocumentTypeNotFound();
        yield return InventoryErrors.DocumentTypeInactive("AJP");
        yield return InventoryErrors.DocumentClassNotAvailable(DocumentClass.SalesInvoice);
        yield return InventoryErrors.FlagNotApplicable("vatNonDeductible", DocumentClass.PositiveAdjustment);
        yield return InventoryErrors.TypeTransitNotAllowed();
        yield return InventoryErrors.NumberedByResolution(DocumentClass.SalesInvoice);
        yield return InventoryErrors.HasOpenDocuments(2, 1);
        yield return InventoryErrors.RequiredBySystem(DocumentClass.Voiding);
        yield return InventoryErrors.SequenceNumberAlreadyIssued(1500);
        yield return InventoryErrors.SequenceOverlaps();
        yield return InventoryErrors.SequenceMissing("AJP", hoy);
        yield return InventoryErrors.PeriodClosed(2026, 8, new DateOnly(2026, 8, 31));
        yield return InventoryErrors.WarehouseNotActive(guid, "B01");
        yield return InventoryErrors.WarehouseInactive(guid, "B01");
        yield return InventoryErrors.ProductNotInventoriable(1, "P1");
        yield return InventoryErrors.ProductInactive(1, "P1");
        yield return InventoryErrors.ProductBlocked(1, "P1");
        yield return InventoryErrors.UnitNotForProduct(1, "P1", "CJ");
        yield return InventoryErrors.UnitDecimalsNotAllowed(1, "P1", "UND", 0, 1.5m);
        yield return InventoryErrors.LocationNotInWarehouse(1, "P1");
        yield return InventoryErrors.CountProductsLocked(guid, "CF-3", ["P1"]);
        yield return InventoryErrors.StockInsufficient([new(1, guid, "P1", guid, null, 5, 2)]);
        yield return InventoryErrors.AdjustmentUnitCostNotAllowed(1, "Inventory.Adjustments.SetUnitCost");
        yield return InventoryErrors.AdjustmentUnitCostOnlyOnEntries(1);
        yield return InventoryErrors.AdjustmentTaxableWithdrawalNotAvailable();
        yield return InventoryErrors.AmountExceedsLimit(1000, 500, "COP", "Inventory.Adjustments.Confirm");
        yield return InventoryErrors.PrevalidationNotPostable([new(1, "143505", "RequiresThirdParty", "Falta el tercero", null)]);
        yield return InventoryErrors.PrevalidationNoResponse();
        yield return InventoryErrors.CurrencyNotSupported();
    }

    [Fact]
    public void Los_codigos_son_los_del_contrato()
    {
        Todos().Select(e => e.Code).Should().BeEquivalentTo(
        [
            "Inventory.Document.NotFound", "Inventory.Document.NotDraft", "Inventory.Document.NotConfirmed",
            "Inventory.Document.TypeNotForRoute", "Inventory.Document.Empty", "Inventory.Document.TooManyLines",
            "Inventory.Document.FieldRequired", "Inventory.Document.WarehouseNotAllowedForType", "Inventory.Document.TransitNotAllowed",
            "Inventory.Document.DateInFuture", "Inventory.Document.DateBeforeCutoff", "Inventory.Document.AlreadyVoided",
            "Inventory.Document.VoidingNotVoidable", "Inventory.Document.HasDependents", "Inventory.Document.FiscalUseCorrection",
            "Inventory.DocumentType.NotFound", "Inventory.DocumentType.Inactive", "Inventory.DocumentClass.NotAvailable",
            "Inventory.DocumentType.FlagNotApplicable", "Inventory.DocumentType.TransitNotAllowed",
            "Inventory.DocumentType.NumberedByResolution", "Inventory.DocumentType.HasOpenDocuments",
            "Inventory.DocumentType.RequiredBySystem", "Inventory.Sequence.NumberAlreadyIssued", "Inventory.Sequence.Overlaps",
            "Inventory.Numbering.SequenceMissing", "Inventory.Period.Closed", "Inventory.Warehouse.NotActive",
            "Inventory.Warehouse.Inactive", "Inventory.Product.NotInventoriable", "Inventory.Product.Inactive",
            "Inventory.Product.Blocked", "Inventory.Unit.NotForProduct", "Inventory.Unit.DecimalsNotAllowed",
            "Inventory.Location.NotInWarehouse", "Inventory.Count.ProductsLocked", "Inventory.Stock.Insufficient",
            "Inventory.Adjustment.UnitCostNotAllowed", "Inventory.Adjustment.UnitCostOnlyOnEntries",
            "Inventory.Adjustment.TaxableWithdrawalNotAvailable",
            "Inventory.Approval.AmountExceedsLimit", "Inventory.Prevalidation.NotPostable", "Inventory.Prevalidation.NoResponse",
            "Inventory.Currency.NotSupported",
        ]);
        Todos().Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Message));
    }

    [Fact]
    public void Cubre_todas_las_fabricas_del_catalogo()
    {
        var fabricas = typeof(InventoryErrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => typeof(Error).IsAssignableFrom(m.ReturnType)).Select(m => m.Name).ToList();
        fabricas.Should().HaveCount(Todos().Count(), "cada fábrica de InventoryErrors está en esta prueba");
    }

    [Fact]
    public void La_data_lleva_lo_que_la_persona_necesita_para_corregir()
    {
        Datos(InventoryErrors.TooManyLines()).Should().BeEquivalentTo(new { max = 4000 });
        Datos(InventoryErrors.DocumentClassNotAvailable(DocumentClass.SalesInvoice))
            .Should().BeEquivalentTo(new { @class = "SalesInvoice", availableIn = "I3" });
        Datos(InventoryErrors.SequenceNumberAlreadyIssued(1500)).Should().BeEquivalentTo(new { lastIssued = 1500L });
        Datos(InventoryErrors.WarehouseNotActive(Guid.Empty, "B01")).Should().BeEquivalentTo(new
        {
            warehousePublicId = Guid.Empty, warehouseCode = "B01", allowedClasses = new[] { "OpeningBalance", "Voiding" },
        });

        var sinExistencia = Datos(InventoryErrors.StockInsufficient(
            [new(2, Guid.Empty, "P2", Guid.Empty, null, 5, 1), new(4, Guid.Empty, "P4", Guid.Empty, null, 3, 0)], "SupplierReturn"));
        sinExistencia.Should().BeEquivalentTo(new { lineNumber = 2, productCode = "P2", requested = 5m, available = 1m, suggestion = "SupplierReturn" },
            o => o.ExcludingMissingMembers());
    }

    private static object Datos(Error e) => e.Should().BeOfType<ErrorConDatos>().Which.Data;
}
