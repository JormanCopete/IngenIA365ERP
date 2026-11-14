using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing.Promotions;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.Quotes;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-13 (descuentos, fase 1): los descuentos no se pierden a lo largo del ciclo comercial.
/// <list type="bullet">
/// <item>lo pendiente de un pedido (o de una remisión) llega a la venta con el descuento manual de su línea origen, en proporción; antes
/// llegaba sólo con el precio de lista;</item>
/// <item>si a la fecha de la venta le aplica una promoción, ese descuento heredado cede a la promoción (no se rechaza el borrador);</item>
/// <item>la cotización vigente pasa también su descuento al total a la venta, y la consulta de la venta dice su fracción
/// (<c>documentDiscountRate</c>) para que la factura de oficina lo vuelva a mostrar.</item>
/// </list>
/// </summary>
public class DescuentosEnElCicloComercialTests
{
    private static readonly DateOnly Hoy = Catalog.CatalogoDePrueba.Hoy;

    /// <summary>El ciclo con un tope de descuento del 20 % para quien vende: los descuentos de estas pruebas no piden aprobación.</summary>
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

    private static async Task<Domain.Entities.Inventory.Documents.InventoryDocument> PedidoConDescuentoAsync(CicloComercialDePrueba c, Guid producto, decimal cantidad) =>
        await c.ConfirmadoAsync(c.Documento("PED", lineas: [c.Linea(producto, cantidad) with { Discount = new SalesDiscountInput(Percent: 0.1m) }]), RutasDeVenta.Pedidos);

    [Fact]
    public async Task Lo_pendiente_del_pedido_llega_a_la_factura_con_su_descuento_en_proporcion()
    {
        var c = await CicloAsync();
        var pedido = await PedidoConDescuentoAsync(c, c.P1, 10m);
        await c.RemisionAsync(4m, pedido);

        var factura = CicloComercialDePrueba.Exito(await c.GuardarAsync(c.Documento("FV", origenes: [pedido.PublicId]), RutasDeVenta.Facturas));

        var linea = c.Doc(factura.PublicId).Lines.Single(l => !l.IsDeleted);
        (linea.Quantity, linea.GrossAmount, linea.DiscountAmount).Should().Be((6m, 12_000m, 1_200m),
            "quedan 6 de 10 por despachar y llevan el 10 % del pedido, no sólo el precio de lista");
        c.Db.DocumentLineDiscounts.Single(d => d.DocumentLineId == linea.Id && !d.IsDeleted)
            .Should().Match<Domain.Entities.Inventory.Documents.DocumentLineDiscount>(d => d.Rate == 0.1m && d.Source == DiscountSource.Manual && !d.FromDocumentDiscount);
    }

    [Fact]
    public async Task El_descuento_heredado_del_pedido_cede_a_una_promocion_vigente_en_lugar_de_bloquear_la_factura()
    {
        var c = await CicloAsync();
        var pedido = await PedidoConDescuentoAsync(c, c.P3, 3m);
        var promocion = await new CreatePromotionCommandHandler(c.Db).Handle(new CreatePromotionCommand("3X2ACEITE", "Aceite lleve 3 pague 2", PromotionKind.BuyNPayM,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31), "Temporada", BuyQuantity: 3m, PayQuantity: 2m,
            Scopes: [new PromotionScopeInput(ProductPublicId: c.P3)]), default);
        promocion.IsSuccess.Should().BeTrue(promocion.IsFailure ? promocion.Error.Message : string.Empty);

        var factura = await c.GuardarAsync(c.Documento("FV", origenes: [pedido.PublicId]), RutasDeVenta.Facturas);

        factura.IsSuccess.Should().BeTrue(factura.IsFailure ? $"{factura.Error.Code}: {factura.Error.Message}" : string.Empty);
        var linea = c.Doc(factura.Value.PublicId).Lines.Single(l => !l.IsDeleted);
        c.Db.DocumentLineDiscounts.Where(d => d.DocumentLineId == linea.Id && !d.IsDeleted).Select(d => d.Source)
            .Should().Equal([DiscountSource.Promotion], "la promoción gana sobre el descuento que la persona no digitó");
    }

    [Fact]
    public async Task La_cotizacion_vigente_pasa_su_descuento_al_total_a_la_factura_y_la_consulta_dice_su_fraccion()
    {
        var c = await CicloAsync();
        var cotizacion = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.Documento("COT", lineas: [c.Linea(c.P1, 6m), c.Linea(c.P3, 2m)]) with
            {
                ValidUntil = Hoy.AddDays(5), DocumentDiscount = new SalesDiscountInput(Percent: 0.05m),
            }, RutasDeVenta.Cotizaciones));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(cotizacion.PublicId));
        var pos = new BorradorDelPos(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Alcance, c.V.K.Maestros(), c.V.Precificacion(), c.V.Aprobaciones(),
            c.V.K.Motor, c.V.K.Permisos);

        var cargada = CicloComercialDePrueba.Exito(await new LoadQuoteIntoSaleCommandHandler(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Vista(), pos, c.Guardar())
            .Handle(new LoadQuoteIntoSaleCommand(cotizacion.PublicId, DestinoDeCotizacion.Invoice, DocumentTypePublicId: c.V.Tipo("FV")), default));

        var venta = c.Doc(cargada.DraftPublicId);
        c.Db.DocumentLineDiscounts.Where(d => d.DocumentId == venta.Id && !d.IsDeleted && d.FromDocumentDiscount).Sum(d => d.Amount)
            .Should().Be(1_600m, "el 5 % de 12.000 + 20.000, como en la cotización");
        venta.Total.Should().Be(c.Doc(cotizacion.PublicId).Total);
        var detalle = await new GetSalesDocumentQueryHandler(c.Db, c.V.K.Vista(), c.V.K.Alcance).Handle(new GetSalesDocumentQuery(venta.PublicId), default);
        detalle.Value.DocumentDiscountRate.Should().Be(0.05m);
    }
}
