using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Tests.Enums;

/// <summary>
/// Las enumeraciones compartidas del comercio (feature 012, T025–T027 y T039) se guardan como
/// <c>int</c>: su valor numérico ES el dato en la base y en los mensajes guardados. Cambiar uno
/// cambia el significado de lo ya escrito sin que nada falle. Esta prueba fija, valor por valor,
/// lo que dice decisiones-transversales §2.5 (y data-model §26 para los que §2.5 no listaba).
/// Ninguna historia los vuelve a crear ni les cambia valores: si esto falla, se corrige el código,
/// no la prueba.
/// </summary>
public class EnumeracionesDelComercioTests
{
    private static void Fija<T>(params (T Valor, int Numero)[] esperados) where T : struct, Enum
    {
        var reales = Enum.GetValues<T>().Select(v => (Nombre: v.ToString(), Numero: Convert.ToInt32(v))).ToList();
        reales.Should().BeEquivalentTo(
            esperados.Select(e => (Nombre: e.Valor.ToString(), e.Numero)),
            $"los valores de {typeof(T).Name} son los de decisiones-transversales §2.5");
        typeof(T).GetEnumUnderlyingType().Should().Be(typeof(int), "toda enumeración se guarda como int");
    }

    // ------------------------------------------------------------------ Inventory (T025) --

    [Fact]
    public void Catalogo_y_bodegas()
    {
        Fija((ProductKind.Inventoriable, 1), (ProductKind.Service, 2), (ProductKind.Combo, 3),
             (ProductKind.Kit, 4), (ProductKind.Template, 5), (ProductKind.Variant, 6));
        Fija((ProductStatus.Active, 1), (ProductStatus.Inactive, 2), (ProductStatus.Blocked, 3));
        Fija((WarehouseBehavior.Operational, 1), (WarehouseBehavior.Transit, 2));
        Fija((WarehouseActivationStatus.NotActivated, 0), (WarehouseActivationStatus.Active, 1));
        Fija((ProductUnitUsage.Purchase, 1), (ProductUnitUsage.Sale, 2), (ProductUnitUsage.Both, 3));
    }

    [Fact]
    public void DocumentClass_tiene_las_34_clases_con_su_numero()
    {
        Fija((DocumentClass.PurchaseRequest, 1), (DocumentClass.PurchaseOrder, 2), (DocumentClass.PurchaseReceipt, 3),
             (DocumentClass.SupplierInvoice, 4), (DocumentClass.SupplierNote, 5), (DocumentClass.SupportDocument, 6),
             (DocumentClass.SupportDocumentAdjustmentNote, 7), (DocumentClass.LandedCost, 8), (DocumentClass.SupplierReturn, 9),
             (DocumentClass.PositiveAdjustment, 10), (DocumentClass.NegativeAdjustment, 11), (DocumentClass.InternalConsumption, 12),
             (DocumentClass.WriteOff, 13), (DocumentClass.Assembly, 14), (DocumentClass.OpeningBalance, 15),
             (DocumentClass.TransferDispatch, 16), (DocumentClass.TransferReceipt, 17), (DocumentClass.LocationMove, 18),
             (DocumentClass.PhysicalCount, 19), (DocumentClass.CostAdjustment, 20), (DocumentClass.CashMovement, 21),
             (DocumentClass.CashCountDifference, 22), (DocumentClass.SalesQuote, 23), (DocumentClass.SalesOrder, 24),
             (DocumentClass.Shipment, 25), (DocumentClass.SalesInvoice, 26), (DocumentClass.SalesInvoiceFromShipments, 27),
             (DocumentClass.PosEquivalentDocument, 28), (DocumentClass.NonElectronicSalesReceipt, 29),
             (DocumentClass.NonElectronicSalesNote, 30), (DocumentClass.CreditNote, 31), (DocumentClass.PosAdjustmentNote, 32),
             (DocumentClass.DebitNote, 33), (DocumentClass.Voiding, 34));
        Enum.GetValues<DocumentClass>().Should().HaveCount(34);
    }

    [Fact]
    public void Documentos_y_contabilizacion()
    {
        Fija((DocumentClassGroup.Purchases, 1), (DocumentClassGroup.Adjustments, 2), (DocumentClassGroup.Transfers, 3),
             (DocumentClassGroup.Counts, 4), (DocumentClassGroup.Sales, 5), (DocumentClassGroup.Cash, 6),
             (DocumentClassGroup.OpeningBalance, 7), (DocumentClassGroup.Costing, 8));
        Fija((DocumentStatus.Draft, 0), (DocumentStatus.PendingApproval, 1), (DocumentStatus.Confirmed, 2),
             (DocumentStatus.Voided, 3), (DocumentStatus.Discarded, 4));
        Fija((PostingMode.Online, 1), (PostingMode.Batch, 2), (PostingMode.NotPosted, 3));
        Fija((PostingGranularity.PerDocument, 1), (PostingGranularity.Summarized, 2));
        Fija((BatchScheduleKind.Daily, 1), (BatchScheduleKind.CashSessionClose, 2), (BatchScheduleKind.PeriodClose, 3));
        Fija((PostingChain.None, 0), (PostingChain.Purchases, 1), (PostingChain.Sales, 2), (PostingChain.Transfers, 3));
        Fija((DocumentLinkKind.Voids, 1), (DocumentLinkKind.FromOrder, 2), (DocumentLinkKind.FromShipment, 3),
             (DocumentLinkKind.NoteOf, 4), (DocumentLinkKind.ReceiptOf, 5), (DocumentLinkKind.InvoiceOfReceipt, 6),
             (DocumentLinkKind.ReturnOf, 7), (DocumentLinkKind.DispatchOf, 8), (DocumentLinkKind.CountAdjustmentOf, 9),
             (DocumentLinkKind.ReplacementOf, 10), (DocumentLinkKind.LandedCostOf, 11));
    }

    [Fact]
    public void Kardex_costeo_periodos_conteos_y_traslados()
    {
        Fija((KardexEntryKind.Entry, 1), (KardexEntryKind.Exit, 2), (KardexEntryKind.CostAdjustment, 3));
        Fija((KardexReason.Normal, 1), (KardexReason.Retroactive, 2), (KardexReason.PriceDifference, 3),
             (KardexReason.LandedCost, 4), (KardexReason.NegativeRegularization, 5), (KardexReason.VoidDifference, 6),
             (KardexReason.RoundingResidue, 7), (KardexReason.MethodChange, 8));
        Fija((CostMethod.WeightedAverage, 1), (CostMethod.Fifo, 2));
        Fija((CostScope.Cooperative, 1), (CostScope.Warehouse, 2));
        Fija((InventoryPeriodStatus.Open, 1), (InventoryPeriodStatus.Closed, 2));
        Fija((CountKind.Total, 1), (CountKind.Cyclic, 2));
        Fija((CountScope.All, 0), (CountScope.Category, 1), (CountScope.Location, 2), (CountScope.Selection, 3), (CountScope.AbcClass, 4));
        Fija((TransferDiscrepancyKind.Shortage, 1), (TransferDiscrepancyKind.Surplus, 2));
        Fija((TransferDiscrepancyResolution.ReturnToOrigin, 1), (TransferDiscrepancyResolution.WriteOffFromTransit, 2),
             (TransferDiscrepancyResolution.LateReceipt, 3), (TransferDiscrepancyResolution.SurplusAdjustment, 4));
    }

    [Fact]
    public void Caja_ventas_y_compras()
    {
        Fija((CashSessionStatus.Open, 1), (CashSessionStatus.Closed, 2));
        Fija((CashMovementKind.WithdrawalToSafe, 1), (CashMovementKind.WithdrawalToRegister, 2),
             (CashMovementKind.WithdrawalForDeposit, 3), (CashMovementKind.BaseIncome, 4),
             (CashMovementKind.ReclassificationBetweenMeans, 5));
        Fija((CashMovementDestination.Safe, 1), (CashMovementDestination.Register, 2), (CashMovementDestination.Deposit, 3));
        Fija((CashDifferenceTreatment.Surplus, 1), (CashDifferenceTreatment.ShortageToCashier, 2), (CashDifferenceTreatment.ShortageToExpense, 3));
        Fija((CashRegisterDocumentRole.PosSale, 1), (CashRegisterDocumentRole.InvoiceOnRequest, 2),
             (CashRegisterDocumentRole.PosAdjustmentNote, 3), (CashRegisterDocumentRole.InvoiceCreditNote, 4),
             (CashRegisterDocumentRole.PosSaleContingency, 5), (CashRegisterDocumentRole.InvoiceContingency, 6));
        Fija((CashDenominationKind.Bill, 1), (CashDenominationKind.Coin, 2));
        Fija((ReservationStatus.Active, 1), (ReservationStatus.Consumed, 2), (ReservationStatus.Released, 3), (ReservationStatus.Expired, 4));
        Fija((PaymentDirection.Received, 1), (PaymentDirection.Refunded, 2));
        Fija((DiscountSource.Manual, 1), (DiscountSource.Promotion, 2));
        Fija((VoucherRedemptionStatus.Active, 1), (VoucherRedemptionStatus.Released, 2));
        Fija((PromotionKind.Percent, 1), (PromotionKind.Amount, 2), (PromotionKind.BuyNPayM, 3),
             (PromotionKind.QuantityPrice, 4), (PromotionKind.BundlePrice, 5));
        Fija((PromotionScopeKind.Product, 1), (PromotionScopeKind.Category, 2), (PromotionScopeKind.Segment, 3), (PromotionScopeKind.Channel, 4));
        Fija((DayCloseStatus.Closed, 1), (DayCloseStatus.Reopened, 2));
        Fija((CreditOrigin.ProvisionalCredit, 1), (CreditOrigin.LendingNoResponse, 2), (CreditOrigin.Validated, 3));
        Fija((SupplierInvoiceEventCode.Receipt030, 30), (SupplierInvoiceEventCode.GoodsReceived032, 32));
        Fija((SupplierInvoiceEventStatus.Pending, 0), (SupplierInvoiceEventStatus.RegisteredExternally, 1),
             (SupplierInvoiceEventStatus.Emitted, 2), (SupplierInvoiceEventStatus.Rejected, 3), (SupplierInvoiceEventStatus.NotApplicable, 4));
    }

    // ------------------------------------------------------------- Core: impuestos (T026) --

    [Fact]
    public void Impuestos()
    {
        Fija((TaxKind.Iva, 1), (TaxKind.Inc, 2), (TaxKind.ReteFuente, 3), (TaxKind.ReteIva, 4),
             (TaxKind.ReteIca, 5), (TaxKind.Ica, 6), (TaxKind.Other, 99));
        Fija((TaxCalculationForm.PercentOfBase, 1), (TaxCalculationForm.PercentOfTax, 2), (TaxCalculationForm.AmountPerUnit, 3));
        Fija((TaxTreatment.Generated, 1), (TaxTreatment.Deductible, 2), (TaxTreatment.AddedToCost, 3),
             (TaxTreatment.WithholdingApplied, 4), (TaxTreatment.WithholdingSuffered, 5));
        Fija((TaxAppliesTo.Purchases, 1), (TaxAppliesTo.Sales, 2), (TaxAppliesTo.Both, 3));
        Fija((VatSaleTreatment.Taxed, 1), (VatSaleTreatment.Exempt, 2), (VatSaleTreatment.Excluded, 3));
    }

    // ---------------------------------------------------------- Core: medios de pago (T027) --

    [Fact]
    public void Medios_de_pago()
    {
        Fija((PaymentMeansClass.Cash, 1), (PaymentMeansClass.CreditCard, 2), (PaymentMeansClass.DebitCard, 3),
             (PaymentMeansClass.AssociateCredit, 4), (PaymentMeansClass.CustomerCredit, 5), (PaymentMeansClass.BankDeposit, 6),
             (PaymentMeansClass.Transfer, 7), (PaymentMeansClass.Voucher, 8), (PaymentMeansClass.Check, 9), (PaymentMeansClass.Other, 99));
        Fija((CashCountMethod.PhysicalCount, 1), (CashCountMethod.VoucherTotal, 2), (CashCountMethod.ByReference, 3), (CashCountMethod.None, 4));
        Fija((PaymentReferenceKind.Approval, 1), (PaymentReferenceKind.Receipt, 2), (PaymentReferenceKind.Deposit, 3),
             (PaymentReferenceKind.VoucherNumber, 4), (PaymentReferenceKind.CheckNumber, 5), (PaymentReferenceKind.Other, 99));
        Fija((CardKind.Credit, 1), (CardKind.Debit, 2), (CardKind.Both, 3));
    }

    // ---------------------------------------------------------------- Integración (T039) --

    [Fact]
    public void Integracion()
    {
        Fija((IntegrationMessageKind.Business, 1), (IntegrationMessageKind.Informational, 2));
        Fija((DeliveryStatus.Pending, 0), (DeliveryStatus.InBatch, 1), (DeliveryStatus.Processed, 2),
             (DeliveryStatus.Rejected, 3), (DeliveryStatus.NotApplicable, 4), (DeliveryStatus.ValidationFailed, 5));
        Fija((DeliveryMode.Online, 1), (DeliveryMode.Batch, 2), (DeliveryMode.NotPosted, 3), (DeliveryMode.Always, 4));
        Fija((BatchTrigger.Scheduled, 1), (BatchTrigger.CashSessionClose, 2), (BatchTrigger.PeriodClose, 3),
             (BatchTrigger.Manual, 4), (BatchTrigger.Reprocess, 5), (BatchTrigger.SendNotApplicable, 6));
        Fija((BatchStatus.Requested, 0), (BatchStatus.Running, 1), (BatchStatus.Completed, 2),
             (BatchStatus.CompletedWithRejections, 3), (BatchStatus.Empty, 4));
        Fija((ActorKind.Person, 1), (ActorKind.Process, 2));
        Fija((ExecutionChannel.Web, 1), (ExecutionChannel.App, 2), (ExecutionChannel.Pos, 3), (ExecutionChannel.Process, 4));
        Fija((PrevalidationOutcome.Postable, 1), (PrevalidationOutcome.NoResponse, 2), (PrevalidationOutcome.NotApplicable, 3));
        Fija((MessageOriginKind.Document, 1), (MessageOriginKind.Operation, 2));
        Fija((DeliveryAttemptOutcome.Processed, 1), (DeliveryAttemptOutcome.AlreadyProcessed, 2),
             (DeliveryAttemptOutcome.Rejected, 3), (DeliveryAttemptOutcome.Retry, 4));
    }

    [Fact]
    public void Los_destinos_son_constantes_de_texto()
    {
        IntegrationDestinations.Accounting.Should().Be("Accounting");
        IntegrationDestinations.Lending.Should().Be("Lending");
    }
}
