using FluentAssertions;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using static IngenIA365ERP.Domain.Inventory.Documents.ClasesDeDocumento;

namespace IngenIA365ERP.Domain.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T100 (FR-036, T16, T17; decisiones-transversales §2.6 y §5): el catálogo fijo de las 34 clases. Cada
/// una tiene grupo (salvo la anulación, que toma el del original), efecto, si es fiscal y en qué sentido, sus mensajes,
/// su cadena de modo de paso, quién la numera y la entrega en que se vuelve operable. Si algo de esto cambia, cambian
/// rutas, permisos, mensajes a Contabilidad y numeración: se corrige el código, no la prueba.
/// </summary>
public class ClasesDeDocumentoTests
{
    [Fact]
    public void Estan_las_34_clases_y_cada_una_describe_la_suya()
    {
        Todas.Should().HaveCount(34);
        Todas.Select(d => d.Class).Should().BeEquivalentTo(Enum.GetValues<DocumentClass>());
        foreach (var clase in Enum.GetValues<DocumentClass>())
        {
            De(clase).Class.Should().Be(clase);
            De(clase).Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(DocumentClass.PurchaseRequest, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.PurchaseOrder, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.PurchaseReceipt, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.SupplierInvoice, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.SupplierNote, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.SupportDocument, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.SupportDocumentAdjustmentNote, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.LandedCost, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.SupplierReturn, DocumentClassGroup.Purchases)]
    [InlineData(DocumentClass.PositiveAdjustment, DocumentClassGroup.Adjustments)]
    [InlineData(DocumentClass.NegativeAdjustment, DocumentClassGroup.Adjustments)]
    [InlineData(DocumentClass.InternalConsumption, DocumentClassGroup.Adjustments)]
    [InlineData(DocumentClass.WriteOff, DocumentClassGroup.Adjustments)]
    [InlineData(DocumentClass.Assembly, DocumentClassGroup.Adjustments)]
    [InlineData(DocumentClass.LocationMove, DocumentClassGroup.Adjustments)]
    [InlineData(DocumentClass.OpeningBalance, DocumentClassGroup.OpeningBalance)]
    [InlineData(DocumentClass.TransferDispatch, DocumentClassGroup.Transfers)]
    [InlineData(DocumentClass.TransferReceipt, DocumentClassGroup.Transfers)]
    [InlineData(DocumentClass.PhysicalCount, DocumentClassGroup.Counts)]
    [InlineData(DocumentClass.CostAdjustment, DocumentClassGroup.Costing)]
    [InlineData(DocumentClass.CashMovement, DocumentClassGroup.Cash)]
    [InlineData(DocumentClass.CashCountDifference, DocumentClassGroup.Cash)]
    [InlineData(DocumentClass.SalesQuote, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.SalesOrder, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.Shipment, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.SalesInvoice, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.SalesInvoiceFromShipments, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.PosEquivalentDocument, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.NonElectronicSalesReceipt, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.NonElectronicSalesNote, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.CreditNote, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.PosAdjustmentNote, DocumentClassGroup.Sales)]
    [InlineData(DocumentClass.DebitNote, DocumentClassGroup.Sales)]
    public void Cada_clase_tiene_su_grupo(DocumentClass clase, DocumentClassGroup grupo)
    {
        De(clase).Group.Should().Be(grupo);
        GrupoDe(clase).Should().Be(grupo);
    }

    [Fact]
    public void La_anulacion_toma_el_grupo_del_original_y_no_se_anula_a_si_misma()
    {
        De(DocumentClass.Voiding).Group.Should().BeNull();
        GrupoDe(DocumentClass.Voiding, DocumentClass.PurchaseReceipt).Should().Be(DocumentClassGroup.Purchases);
        GrupoDe(DocumentClass.Voiding, DocumentClass.TransferDispatch).Should().Be(DocumentClassGroup.Transfers);
        GrupoDe(DocumentClass.Voiding, DocumentClass.OpeningBalance).Should().Be(DocumentClassGroup.OpeningBalance);

        var sinOriginal = () => GrupoDe(DocumentClass.Voiding);
        sinOriginal.Should().Throw<ArgumentException>();
        var deOtraAnulacion = () => GrupoDe(DocumentClass.Voiding, DocumentClass.Voiding);
        deOtraAnulacion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cada_grupo_lista_sus_clases_y_la_anulacion_no_esta_en_ninguno()
    {
        DelGrupo(DocumentClassGroup.Transfers).Should().Equal(DocumentClass.TransferDispatch, DocumentClass.TransferReceipt);
        DelGrupo(DocumentClassGroup.Counts).Should().Equal(DocumentClass.PhysicalCount);
        DelGrupo(DocumentClassGroup.Costing).Should().Equal(DocumentClass.CostAdjustment);
        Enum.GetValues<DocumentClassGroup>().SelectMany(DelGrupo).Should().HaveCount(33).And.NotContain(DocumentClass.Voiding);
    }

    [Theory]
    [InlineData(DocumentClass.PurchaseReceipt, InventoryEffect.Entry)]
    [InlineData(DocumentClass.SupplierInvoice, InventoryEffect.None)]
    [InlineData(DocumentClass.LandedCost, InventoryEffect.CostOnly)]
    [InlineData(DocumentClass.SupplierReturn, InventoryEffect.Exit)]
    [InlineData(DocumentClass.PositiveAdjustment, InventoryEffect.Entry)]
    [InlineData(DocumentClass.NegativeAdjustment, InventoryEffect.Exit)]
    [InlineData(DocumentClass.InternalConsumption, InventoryEffect.Exit)]
    [InlineData(DocumentClass.WriteOff, InventoryEffect.Exit)]
    [InlineData(DocumentClass.Assembly, InventoryEffect.Both)]
    [InlineData(DocumentClass.OpeningBalance, InventoryEffect.Entry)]
    [InlineData(DocumentClass.TransferDispatch, InventoryEffect.Both)]
    [InlineData(DocumentClass.TransferReceipt, InventoryEffect.Both)]
    [InlineData(DocumentClass.LocationMove, InventoryEffect.Both)]
    [InlineData(DocumentClass.PhysicalCount, InventoryEffect.None)]
    [InlineData(DocumentClass.CostAdjustment, InventoryEffect.CostOnly)]
    [InlineData(DocumentClass.CashMovement, InventoryEffect.None)]
    [InlineData(DocumentClass.SalesOrder, InventoryEffect.Reservation)]
    [InlineData(DocumentClass.Shipment, InventoryEffect.Exit)]
    [InlineData(DocumentClass.SalesInvoice, InventoryEffect.Exit)]
    [InlineData(DocumentClass.SalesInvoiceFromShipments, InventoryEffect.None)]
    [InlineData(DocumentClass.PosEquivalentDocument, InventoryEffect.Exit)]
    [InlineData(DocumentClass.CreditNote, InventoryEffect.Entry)]
    [InlineData(DocumentClass.DebitNote, InventoryEffect.None)]
    public void Cada_clase_tiene_su_efecto(DocumentClass clase, InventoryEffect efecto) =>
        De(clase).Effect.Should().Be(efecto);

    [Fact]
    public void Las_notas_de_venta_entran_solo_si_devuelven_mercancia_y_la_anulacion_invierte_el_original()
    {
        Todas.Where(d => d.EffectOnlyWhenReturningGoods).Select(d => d.Class).Should().BeEquivalentTo(
            [DocumentClass.NonElectronicSalesNote, DocumentClass.CreditNote, DocumentClass.PosAdjustmentNote]);

        EfectoDeLaAnulacion(DocumentClass.PurchaseReceipt).Should().Be(InventoryEffect.Exit);
        EfectoDeLaAnulacion(DocumentClass.NegativeAdjustment).Should().Be(InventoryEffect.Entry);
        EfectoDeLaAnulacion(DocumentClass.TransferDispatch).Should().Be(InventoryEffect.Both);
        EfectoDeLaAnulacion(DocumentClass.CostAdjustment).Should().Be(InventoryEffect.CostOnly);
    }

    [Fact]
    public void Fiscales_y_su_sentido()
    {
        var fiscales = Todas.Where(d => d.IsFiscal).ToDictionary(d => d.Class, d => d.FiscalDirection);
        fiscales.Should().BeEquivalentTo(new Dictionary<DocumentClass, FiscalDirection?>
        {
            [DocumentClass.SupplierInvoice] = FiscalDirection.Received,
            [DocumentClass.SupplierNote] = FiscalDirection.Received,
            [DocumentClass.SupportDocument] = FiscalDirection.Emitted,
            [DocumentClass.SupportDocumentAdjustmentNote] = FiscalDirection.Emitted,
            [DocumentClass.SalesInvoice] = FiscalDirection.Emitted,
            [DocumentClass.SalesInvoiceFromShipments] = FiscalDirection.Emitted,
            [DocumentClass.PosEquivalentDocument] = FiscalDirection.Emitted,
            [DocumentClass.CreditNote] = FiscalDirection.Emitted,
            [DocumentClass.PosAdjustmentNote] = FiscalDirection.Emitted,
            [DocumentClass.DebitNote] = FiscalDirection.Emitted,
        });
        De(DocumentClass.NonElectronicSalesReceipt).IsFiscal.Should().BeFalse("sólo en una cooperativa no obligada (FR-063)");
        De(DocumentClass.NonElectronicSalesNote).IsFiscal.Should().BeFalse();
    }

    [Fact]
    public void Numeran_con_resolucion_DIAN_solo_factura_factura_desde_remisiones_DEE_POS_y_documento_soporte()
    {
        Todas.Where(d => d.NumberedBy == NumberedBy.DianResolution).Select(d => d.Class).Should().BeEquivalentTo(
        [
            DocumentClass.SalesInvoice, DocumentClass.SalesInvoiceFromShipments, DocumentClass.PosEquivalentDocument,
            DocumentClass.SupportDocument,
        ], "las notas y todo lo no fiscal numeran con INV_DocumentSequences (T16)");
    }

    [Fact]
    public void Mensajes_de_cada_clase()
    {
        var esperados = new Dictionary<DocumentClass, string[]>
        {
            [DocumentClass.PurchaseRequest] = [],
            [DocumentClass.PurchaseOrder] = [],
            [DocumentClass.PurchaseReceipt] = [CompraRecibida],
            [DocumentClass.SupplierInvoice] = [FacturaProveedorRegistrada, AjusteDeCostoReconocido],
            [DocumentClass.SupplierNote] = [FacturaProveedorRegistrada, AjusteDeCostoReconocido],
            [DocumentClass.SupportDocument] = [FacturaProveedorRegistrada],
            [DocumentClass.SupportDocumentAdjustmentNote] = [FacturaProveedorRegistrada, AjusteDeCostoReconocido],
            [DocumentClass.LandedCost] = [AjusteDeCostoReconocido],
            [DocumentClass.SupplierReturn] = [DevolucionRegistrada, AjusteDeCostoReconocido],
            [DocumentClass.PositiveAdjustment] = [AjusteInventarioAprobado],
            [DocumentClass.NegativeAdjustment] = [AjusteInventarioAprobado],
            [DocumentClass.InternalConsumption] = [AjusteInventarioAprobado],
            [DocumentClass.WriteOff] = [AjusteInventarioAprobado],
            [DocumentClass.Assembly] = [AjusteInventarioAprobado],
            [DocumentClass.OpeningBalance] = [SaldoInicialCargado],
            [DocumentClass.TransferDispatch] = [TrasladoDespachado],
            [DocumentClass.TransferReceipt] = [TrasladoRecibido],
            [DocumentClass.LocationMove] = [],
            [DocumentClass.PhysicalCount] = [],
            [DocumentClass.CostAdjustment] = [AjusteDeCostoReconocido],
            [DocumentClass.CashMovement] = [MovimientoDeCajaRegistrado],
            [DocumentClass.CashCountDifference] = [DiferenciaDeArqueoAprobada],
            [DocumentClass.SalesQuote] = [],
            [DocumentClass.SalesOrder] = [],
            [DocumentClass.Shipment] = [CostoDeVentaReconocido],
            [DocumentClass.SalesInvoice] = [VentaFacturada, CostoDeVentaReconocido],
            [DocumentClass.SalesInvoiceFromShipments] = [VentaFacturada],
            [DocumentClass.PosEquivalentDocument] = [VentaFacturada, CostoDeVentaReconocido],
            [DocumentClass.NonElectronicSalesReceipt] = [VentaFacturada, CostoDeVentaReconocido],
            [DocumentClass.NonElectronicSalesNote] = [NotaCreditoEmitida, DevolucionRegistrada],
            [DocumentClass.CreditNote] = [NotaCreditoEmitida, DevolucionRegistrada],
            [DocumentClass.PosAdjustmentNote] = [NotaCreditoEmitida, DevolucionRegistrada],
            [DocumentClass.DebitNote] = [NotaDebitoEmitida],
            [DocumentClass.Voiding] = [DocumentoAnulado, AjusteDeCostoReconocido],
        };

        foreach (var (clase, mensajes) in esperados)
            De(clase).Messages.Should().BeEquivalentTo(mensajes, $"los mensajes de {clase} los fija FR-036 / §2.6");
    }

    [Fact]
    public void Los_nombres_de_mensaje_son_los_de_la_seccion_2_6_sin_tildes()
    {
        string[] canonicos =
        [
            "VentaFacturada", "CostoDeVentaReconocido", "CompraRecibida", "FacturaProveedorRegistrada",
            "AjusteInventarioAprobado", "TrasladoDespachado", "TrasladoRecibido", "DevolucionRegistrada", "DocumentoAnulado",
            "AjusteDeCostoReconocido", "NotaCreditoEmitida", "NotaDebitoEmitida", "MovimientoDeCajaRegistrado",
            "DiferenciaDeArqueoAprobada", "SaldoInicialCargado",
        ];
        Todas.SelectMany(d => d.Messages).Distinct().Should().BeSubsetOf(canonicos);
    }

    [Fact]
    public void Cadenas_de_modo_de_paso()
    {
        Todas.Where(d => d.Chain == PostingChain.Purchases).Select(d => d.Class).Should().BeEquivalentTo(
        [
            DocumentClass.PurchaseReceipt, DocumentClass.SupplierInvoice, DocumentClass.SupplierNote, DocumentClass.SupportDocument,
            DocumentClass.SupportDocumentAdjustmentNote, DocumentClass.SupplierReturn,
        ]);
        Todas.Where(d => d.Chain == PostingChain.Sales).Select(d => d.Class).Should().BeEquivalentTo(
        [
            DocumentClass.Shipment, DocumentClass.SalesInvoice, DocumentClass.SalesInvoiceFromShipments,
            DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt, DocumentClass.NonElectronicSalesNote,
            DocumentClass.CreditNote, DocumentClass.PosAdjustmentNote, DocumentClass.DebitNote,
        ]);
        Todas.Where(d => d.Chain == PostingChain.Transfers).Select(d => d.Class).Should().BeEquivalentTo(
            [DocumentClass.TransferDispatch, DocumentClass.TransferReceipt]);
    }

    [Fact]
    public void En_I1_son_operables_exactamente_las_quince_clases_del_nucleo()
    {
        DocumentClass[] i1 =
        [
            DocumentClass.PurchaseReceipt, DocumentClass.SupplierInvoice, DocumentClass.SupplierNote, DocumentClass.SupplierReturn,
            DocumentClass.PositiveAdjustment, DocumentClass.NegativeAdjustment, DocumentClass.InternalConsumption,
            DocumentClass.WriteOff, DocumentClass.OpeningBalance, DocumentClass.TransferDispatch, DocumentClass.TransferReceipt,
            DocumentClass.LocationMove, DocumentClass.PhysicalCount, DocumentClass.CostAdjustment, DocumentClass.Voiding,
        ];
        Todas.Where(d => d.AvailableFrom == EntregaDelComercio.I1).Select(d => d.Class).Should().BeEquivalentTo(i1);
        Todas.Where(d => d.Operable(EntregaDelComercio.I1)).Select(d => d.Class).Should().BeEquivalentTo(i1);
    }

    [Theory]
    [InlineData(DocumentClass.SalesInvoice, EntregaDelComercio.I3)]
    [InlineData(DocumentClass.PosEquivalentDocument, EntregaDelComercio.I3)]
    [InlineData(DocumentClass.CashMovement, EntregaDelComercio.I3)]
    [InlineData(DocumentClass.NonElectronicSalesReceipt, EntregaDelComercio.I3)]
    [InlineData(DocumentClass.SupportDocument, EntregaDelComercio.I4)]
    [InlineData(DocumentClass.SupportDocumentAdjustmentNote, EntregaDelComercio.I4)]
    [InlineData(DocumentClass.PurchaseOrder, EntregaDelComercio.I5)]
    [InlineData(DocumentClass.LandedCost, EntregaDelComercio.I5)]
    [InlineData(DocumentClass.SalesQuote, EntregaDelComercio.I6)]
    [InlineData(DocumentClass.Assembly, EntregaDelComercio.I6)]
    [InlineData(DocumentClass.DebitNote, EntregaDelComercio.I6)]
    public void Las_clases_de_entregas_posteriores_no_son_operables_todavia(DocumentClass clase, EntregaDelComercio entrega)
    {
        De(clase).AvailableFrom.Should().Be(entrega);
        De(clase).Operable(EntregaDelComercio.I1).Should().BeFalse();
        De(clase).Operable(entrega).Should().BeTrue();
    }

    [Fact]
    public void Bodegas_admitidas_y_centro_de_costo()
    {
        De(DocumentClass.OpeningBalance).Warehouses.Should().Be(AdmittedWarehouses.NotActivated);
        De(DocumentClass.Voiding).Warehouses.Should().HaveFlag(AdmittedWarehouses.NotActivated).And.HaveFlag(AdmittedWarehouses.Operational);
        De(DocumentClass.SupplierInvoice).Warehouses.Should().Be(AdmittedWarehouses.None);
        De(DocumentClass.CostAdjustment).Warehouses.Should().Be(AdmittedWarehouses.None);
        Todas.Where(d => d.Warehouses.HasFlag(AdmittedWarehouses.Transit)).Select(d => d.Class)
            .Should().Equal([DocumentClass.WriteOff], "el tránsito nunca es origen de una salida ni de un despacho, salvo la baja desde el tránsito");
        Todas.Where(d => d.RequiresCostCenter).Select(d => d.Class).Should().Equal(DocumentClass.InternalConsumption);
    }

    [Fact]
    public void El_ajuste_de_costo_y_la_anulacion_no_tienen_alta_manual()
    {
        Todas.Where(d => !d.ManualCreation).Select(d => d.Class).Should().BeEquivalentTo(
            [DocumentClass.CostAdjustment, DocumentClass.Voiding]);
    }
}
