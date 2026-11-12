using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Application.Inventory.Sales.Quotes;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-12 (decisión del dueño: «la cotización sólo es una base»): la cotización simula una venta con los mismos precios y cálculos, exige
/// un cliente, y en una venta se carga —«Cargar cotización»— en la factura de oficina o en la venta del POS: vigente a la fecha de la venta
/// respeta sus precios y descuentos; vencida, toma los del día y lo dice. Cada carga deja su vínculo <c>FromOrder</c> línea a línea, la
/// cotización puede cargarse más de una vez y dice dónde se usó; la venta sigue siendo un borrador normal (guardarla otra vez conserva lo
/// cargado).
/// </summary>
public class CotizacionEnLaVentaTests
{
    private static readonly DateOnly Hoy = Catalog.CatalogoDePrueba.Hoy;

    /// <summary>La cooperativa del ciclo con un tope de descuento del 20 % para quien vende: los descuentos de la cotización no piden aprobación.</summary>
    private static async Task<CicloComercialDePrueba> CicloAsync()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var rol = new Role { Code = "VENDEDOR", Name = "Vendedores", IsActive = true };
        c.Db.Roles.Add(rol);
        await c.Db.SaveChangesAsync();
        c.Db.UserRoles.Add(new UserRole { UserId = KardexDePrueba.Usuario, RoleId = rol.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba" });
        c.Db.DiscountCaps.Add(new DiscountCap { RoleId = rol.Id, MaxLineRate = 0.2m, MaxDocumentRate = 0.2m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "Comité" });
        await c.Db.SaveChangesAsync();
        return c;
    }

    /// <summary>La cotización de Ana confirmada: 6 de P1 a 1.800 digitado (lista 2.000) y 2 de P3 con 10 % de descuento.</summary>
    private static async Task<Guid> CotizacionAsync(CicloComercialDePrueba c, DateOnly vence)
    {
        var borrador = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.Documento("COT", lineas: [c.Linea(c.P1, 6m, 1800m), c.Linea(c.P3, 2m) with { Discount = new SalesDiscountInput(Percent: 0.1m) }]) with { ValidUntil = vence },
            RutasDeVenta.Cotizaciones));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(borrador.PublicId));
        return borrador.PublicId;
    }

    private static BorradorDelPos Pos(CicloComercialDePrueba c) =>
        new(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Alcance, c.V.K.Maestros(), c.V.Precificacion(), c.V.Aprobaciones(), c.V.K.Motor, c.V.K.Permisos);

    private static LoadQuoteIntoSaleCommandHandler Cargar(CicloComercialDePrueba c) =>
        new(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Vista(), Pos(c), c.Guardar());

    private static Task<IngenIA365ERP.Application.Common.Models.Result<CotizacionCargadaDto>> EnFacturaAsync(CicloComercialDePrueba c, Guid cotizacion,
        Guid? borrador = null) =>
        Cargar(c).Handle(new LoadQuoteIntoSaleCommand(cotizacion, DestinoDeCotizacion.Invoice, borrador, DocumentTypePublicId: c.V.Tipo("FV")), default);

    private static List<decimal> Netos(Domain.Entities.Inventory.Documents.InventoryDocument d) =>
        d.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l => l.NetAmount).ToList();

    // ---------------------------------------------------------------------------------------------- la regla --

    [Theory]
    [InlineData("2026-09-30", "2026-09-30", true)]
    [InlineData("2026-09-30", "2026-09-29", true)]
    [InlineData("2026-09-30", "2026-10-01", false)]
    [InlineData(null, "2026-09-01", false)]
    public void La_cotizacion_es_vigente_hasta_el_dia_de_su_vigencia_inclusive(string? vence, string venta, bool vigente) =>
        CotizacionEnLaVenta.Vigente(vence is null ? null : DateOnly.Parse(vence), DateOnly.Parse(venta)).Should().Be(vigente);

    [Fact]
    public async Task La_cotizacion_exige_un_cliente_identificado_para_confirmarse()
    {
        var c = await CicloAsync();
        var sinCliente = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.V.Venta("COT", cliente: null, lineas: [c.Linea(c.P1, 1m)]) with { ValidUntil = Hoy.AddDays(5) }, RutasDeVenta.Cotizaciones));

        var r = await c.ConfirmarAsync(sinCliente.PublicId);

        r.Error.Code.Should().Be(ErroresDelCicloComercial.QuoteCustomerRequiredCode);
    }

    // ------------------------------------------------------------------------------------------- en la factura --

    [Fact]
    public async Task Vigente_crea_la_factura_con_los_precios_y_descuentos_de_la_cotizacion_y_su_vinculo_por_linea()
    {
        var c = await CicloAsync();
        var cotizacion = await CotizacionAsync(c, Hoy.AddDays(5));

        var r = await EnFacturaAsync(c, cotizacion);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        (r.Value.Expired, r.Value.Repriced, r.Value.LinesAdded, r.Value.CustomerChanged).Should().Be((false, false, 2, true));
        var cot = c.Doc(cotizacion);
        var venta = c.Doc(r.Value.DraftPublicId);
        (venta.Class, venta.Status, venta.CounterpartyPersonId).Should().Be((DocumentClass.SalesInvoice, DocumentStatus.Draft, c.Ana.Id));
        Netos(venta).Should().Equal(Netos(cot), "vigente, la venta respeta precios y descuentos de la cotización");
        (venta.Total, venta.TaxTotal).Should().Be((cot.Total, cot.TaxTotal), "los mismos cálculos: impuestos y total");
        var vinculo = c.Db.DocumentLinks.Include(l => l.LineLinks).Single(l => l.TargetDocumentId == venta.Id);
        (vinculo.Kind, vinculo.SourceDocumentId, vinculo.LineLinks.Count).Should().Be((DocumentLinkKind.FromOrder, cot.Id, 2));
    }

    [Fact]
    public async Task Guardada_otra_vez_como_lo_hace_la_pantalla_la_factura_conserva_lo_cargado_de_la_cotizacion_vigente()
    {
        var c = await CicloAsync();
        var cotizacion = await CotizacionAsync(c, Hoy.AddDays(5));
        var cargada = CicloComercialDePrueba.Exito(await EnFacturaAsync(c, cotizacion));
        var cot = c.Doc(cotizacion);
        var origen = cot.Lines.OrderBy(l => l.LineNumber).ToList();
        var venta = c.Doc(cargada.DraftPublicId);
        var lineas = venta.Lines.OrderBy(l => l.LineNumber).ToList();

        // Lo que manda NuevaFactura al guardar: la línea con su origen, el precio digitado y el descuento releídos, y la cotización como origen.
        var guardada = await c.GuardarAsync(c.Documento("FV", origenes: [cotizacion], lineas:
        [
            c.Linea(c.P1, 6m, 1800m) with { LinePublicId = lineas[0].PublicId, OriginLinePublicId = origen[0].PublicId },
            c.Linea(c.P3, 2m) with { LinePublicId = lineas[1].PublicId, OriginLinePublicId = origen[1].PublicId, Discount = new SalesDiscountInput(Percent: 0.1m) },
        ]), RutasDeVenta.Facturas, venta.PublicId);

        guardada.IsSuccess.Should().BeTrue(guardada.IsFailure ? $"{guardada.Error.Code}: {guardada.Error.Message}" : null);
        var otraVez = c.Doc(venta.PublicId);
        Netos(otraVez).Should().Equal(Netos(cot));
        otraVez.Total.Should().Be(cot.Total);
    }

    [Fact]
    public async Task Vencida_a_la_fecha_de_la_venta_toma_los_precios_del_dia_y_lo_dice()
    {
        var c = await CicloAsync();
        var vence = Hoy.AddDays(2);
        var cotizacion = await CotizacionAsync(c, vence);
        c.V.Compras.C.Reloj.HoyLocal.Returns(vence.AddDays(1));

        var r = await EnFacturaAsync(c, cotizacion);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        (r.Value.Expired, r.Value.Repriced).Should().Be((true, true));
        r.Value.Warnings.Should().Contain(w => w.Contains("venció"));
        var venta = c.Doc(r.Value.DraftPublicId);
        venta.Lines.OrderBy(l => l.LineNumber).Select(l => (l.UnitPrice, l.DiscountAmount)).Should().Equal((2000m, 0m), (10000m, 0m));
        c.Db.DocumentLineDiscounts.Count(d => d.DocumentId == venta.Id && !d.IsDeleted).Should().Be(0, "vencida, ni el precio digitado ni el descuento");
        c.Db.DocumentLineLinks.Count(l => l.DocumentLink!.TargetDocumentId == venta.Id).Should().Be(2, "el vínculo con la cotización se conserva");
    }

    [Fact]
    public async Task En_un_borrador_existente_agrega_las_lineas_al_final_y_toma_el_cliente_si_era_el_consumidor_final()
    {
        var c = await CicloAsync();
        var cotizacion = await CotizacionAsync(c, Hoy.AddDays(5));
        var borrador = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.V.Venta("FV", cliente: null, lineas: [c.Linea(c.P1, 1m)]), RutasDeVenta.Facturas));

        var r = await EnFacturaAsync(c, cotizacion, borrador.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        (r.Value.DraftPublicId, r.Value.CustomerChanged).Should().Be((borrador.PublicId, true));
        var venta = c.Doc(borrador.PublicId);
        venta.CounterpartyPersonId.Should().Be(c.Ana.Id);
        venta.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l => (l.LineNumber, l.QuantityBase)).Should().Equal((1, 1m), (2, 6m), (3, 2m));
    }

    [Fact]
    public async Task La_venta_de_otro_cliente_no_se_pisa()
    {
        var c = await CicloAsync();
        var cotizacion = await CotizacionAsync(c, Hoy.AddDays(5));
        var otra = CicloComercialDePrueba.Exito(await c.GuardarAsync(c.Documento("FV", c.C.AsociadoX, lineas: [c.Linea(c.P1, 1m)]), RutasDeVenta.Facturas));

        var r = await EnFacturaAsync(c, cotizacion, otra.PublicId);

        r.Error.Code.Should().Be(ErroresDelCicloComercial.QuoteCustomerMismatchCode);
        c.Doc(otra.PublicId).Lines.Count(l => !l.IsDeleted).Should().Be(1);
    }

    [Fact]
    public async Task Sin_confirmar_no_se_carga()
    {
        var c = await CicloAsync();
        var borrador = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.Documento("COT", lineas: [c.Linea(c.P1, 1m)]) with { ValidUntil = Hoy.AddDays(5) }, RutasDeVenta.Cotizaciones));

        var r = await EnFacturaAsync(c, borrador.PublicId);

        r.Error.Code.Should().Be(ErroresDelCicloComercial.QuoteNotConfirmedCode);
    }

    [Fact]
    public async Task Se_carga_mas_de_una_vez_y_la_cotizacion_dice_donde_se_uso()
    {
        var c = await CicloAsync();
        var cotizacion = await CotizacionAsync(c, Hoy.AddDays(5));

        var primera = CicloComercialDePrueba.Exito(await EnFacturaAsync(c, cotizacion));
        var segunda = CicloComercialDePrueba.Exito(await EnFacturaAsync(c, cotizacion));

        primera.DraftPublicId.Should().NotBe(segunda.DraftPublicId);
        var detalle = await new GetSalesDocumentQueryHandler(c.Db, c.V.K.Vista(), c.V.K.Alcance).Handle(new GetSalesDocumentQuery(cotizacion), default);
        detalle.Value.Uses!.Select(u => u.DocumentPublicId).Should().BeEquivalentTo([primera.DraftPublicId, segunda.DraftPublicId]);
        var lista = await new ListSalesDocumentsQueryHandler(c.Db, c.V.K.Alcance, c.V.Compras.C.Reloj)
            .Handle(new ListSalesDocumentsQuery(DocumentClass.SalesQuote), default);
        lista.Value.Items.Single().UseCount.Should().Be(2);
    }

    // -------------------------------------------------------------------------------------------------- en el POS --

    [Fact]
    public async Task En_el_POS_abre_la_venta_en_la_sesion_y_respeta_los_precios_de_la_cotizacion_vigente()
    {
        var c = await CicloAsync();
        c.V.Caja.DocumentTypes.Add(new CashRegisterDocumentType { CashRegister = c.V.Caja, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = c.V.K.Tipo("FV").Id });
        await c.Db.SaveChangesAsync();
        var cotizacion = await CotizacionAsync(c, Hoy.AddDays(5));

        var r = await Cargar(c).Handle(new LoadQuoteIntoSaleCommand(cotizacion, DestinoDeCotizacion.Pos, CashSessionPublicId: c.V.Sesion.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var venta = c.Doc(r.Value.DraftPublicId);
        (venta.PointOfSaleId, venta.CashSessionId, venta.CounterpartyPersonId).Should().Be((c.V.Punto.Id, c.V.Sesion.Id, c.Ana.Id));
        Netos(venta).Should().Equal(Netos(c.Doc(cotizacion)));

        // Una segunda carga en la misma venta agrega las líneas y reúsa el vínculo (uno por par cotización–venta).
        var otra = await Cargar(c).Handle(new LoadQuoteIntoSaleCommand(cotizacion, DestinoDeCotizacion.Pos, r.Value.DraftPublicId), default);
        otra.IsSuccess.Should().BeTrue(otra.IsFailure ? $"{otra.Error.Code}: {otra.Error.Message}" : null);
        c.Doc(r.Value.DraftPublicId).Lines.Count(l => !l.IsDeleted).Should().Be(4);
        c.Db.DocumentLinks.Count(l => l.TargetDocumentId == venta.Id && l.Kind == DocumentLinkKind.FromOrder).Should().Be(1);
    }

    [Fact]
    public void El_cambio_de_la_linea_reproduce_el_precio_digitado_y_el_descuento_manual_de_la_cotizacion()
    {
        var linea = new Domain.Entities.Inventory.Documents.InventoryDocumentLine { Quantity = 6m, ListPrice = 2000m, UnitPrice = 2000m };
        var cambio = LoadQuoteIntoSaleCommandHandler.CambioDeLaCotizacion(linea,
        [
            new Domain.Entities.Inventory.Documents.DocumentLineDiscount { Source = DiscountSource.Manual, IsPriceOverride = true, Rate = 0.1m, Amount = 1200m },
            new Domain.Entities.Inventory.Documents.DocumentLineDiscount { Source = DiscountSource.Manual, Rate = 0.05m, Amount = 540m },
            new Domain.Entities.Inventory.Documents.DocumentLineDiscount { Source = DiscountSource.Promotion, Rate = 0.5m, Amount = 100m },
        ]);

        cambio.Should().NotBeNull();
        (cambio!.PrecioDigitado, cambio.Descuento!.Percent).Should().Be((1800m, 0.05m));
        LoadQuoteIntoSaleCommandHandler.CambioDeLaCotizacion(linea, []).Should().BeNull("al precio de lista y sin descuento, nada que pedir");
    }
}
