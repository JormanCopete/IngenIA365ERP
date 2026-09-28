using FluentAssertions;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Purchasing;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Sales.Pricing;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I3, T601 (FR-053, FR-054, FR-017; data-model §14; T22, T26, T51): la precificación de las líneas de venta. Resuelve la
/// lista (sin precio → <c>Inventory.Price.NotFound</c>), lleva el precio de una lista con impuestos a <c>UnitPrice</c> sin impuestos
/// (<c>ListPrice ÷ 1,19</c>) con el residuo visible y el total igual al de la lista, mide los descuentos manuales contra el tope del
/// vendedor (sobre el tope, <c>RequiresApproval</c>; cambiar el precio es <c>IsPriceOverride</c>), prorratea el descuento por total
/// con suma exacta, calcula el IVA generado y marca bajo costo según <c>Ventas.BajoCosto</c>, con el disponible de la bodega.
/// </summary>
public class PrecificacionDeVentaTests
{
    private const int Vendedor = 7;
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private static async Task<(ComprasDePrueba C, PrecificacionDeVenta P, int P1, int P3, int Und)> EscenarioAsync(decimal? topeLinea = null, decimal? topeTotal = null)
    {
        var c = await ComprasDePrueba.CrearAsync();
        var db = c.C.Db;
        var cerrojo = Substitute.For<ICerrojoPorClave>();
        var general = (await new CreatePriceListCommandHandler(db, cerrojo).Handle(
            new CreatePriceListCommand("GENERAL", "General", false, null, new DateOnly(2026, 1, 1), null, "Lista"), default)).Value;
        var conIva = (await new CreatePriceListCommandHandler(db, cerrojo).Handle(
            new CreatePriceListCommand("MOSTR", "Mostrador con IVA", true, new PriceListScopeInput(BranchPublicId: c.K.Sucursal.PublicId),
                new DateOnly(2026, 1, 1), null, "Lista"), default)).Value;
        var und = c.C.Unidad("UND").PublicId;
        await new SetPriceListItemsCommandHandler(db).Handle(new SetPriceListItemsCommand(general,
            [new PriceListItemInput(c.P1, und, 2000m), new PriceListItemInput(c.P3, und, 10000m)], "Precios"), default);
        await new SetPriceListItemsCommandHandler(db).Handle(new SetPriceListItemsCommand(conIva,
            [new PriceListItemInput(c.P3, und, 11900m)], "Precios"), default);

        if (topeLinea is not null)
        {
            var rol = new Role { Code = "CAJERO", Name = "Cajeros", IsActive = true };
            db.Roles.Add(rol);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new UserRole { UserId = Vendedor, RoleId = rol.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba" });
            db.DiscountCaps.Add(new Domain.Entities.Inventory.Pricing.DiscountCap
            {
                RoleId = rol.Id, MaxLineRate = topeLinea.Value, MaxDocumentRate = topeTotal ?? 0m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "Comité",
            });
            await db.SaveChangesAsync();
        }

        var p = new PrecificacionDeVenta(db, new LectorDeCatalogoTributario(db, new LectorDeUvt(db), c.K.Lector()), c.K.Lector());
        return (c, p, c.K.ProductoId(c.P1), c.K.ProductoId(c.P3), c.C.Unidad("UND").Id);
    }

    private static PedidoDePrecificacion Pedido(ComprasDePrueba c, IReadOnlyList<LineaAPrecificar> lineas, bool conSucursal = false, decimal? descuentoTotal = null) =>
        new(Hoy, null, null, conSucursal ? c.K.Sucursal.Id : null, c.K.Principal.Id, Vendedor, lineas, descuentoTotal);

    [Fact]
    public async Task Sin_precio_en_ninguna_lista_responde_Price_NotFound()
    {
        var (c, p, p1, _, _) = await EscenarioAsync();

        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p1, c.C.Unidad("DOC").Id, 1m, 12m)]), default);

        r.Error.Code.Should().Be(ErroresDePrecios.PriceNotFoundCode);
    }

    [Fact]
    public async Task La_lista_sin_impuestos_da_el_precio_tal_cual_y_el_IVA_se_suma()
    {
        var (c, p, _, p3, und) = await EscenarioAsync();

        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p3, und, 2m, 2m)]), default);

        var linea = r.Value.Lineas.Single();
        (linea.UnitPrice, linea.GrossAmount, linea.TaxAmount, linea.ListPriceIncludesTaxes).Should().Be((10000m, 20000m, 3800m, false));
        r.Value.Totales.Total.Should().Be(23800m);
    }

    [Fact]
    public async Task La_lista_con_impuestos_se_lleva_a_precio_sin_impuestos_y_el_total_es_el_de_la_lista()
    {
        var (c, p, _, p3, und) = await EscenarioAsync();

        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p3, und, 3m, 3m)], conSucursal: true), default);

        var linea = r.Value.Lineas.Single();
        linea.ListPrice.Should().Be(11900m);
        linea.ListPriceIncludesTaxes.Should().BeTrue();
        linea.UnitPrice.Should().Be(10000m, "11.900 ÷ 1,19");
        r.Value.Totales.Total.Should().Be(35700m, "3 × 11.900: lo que dice la lista con impuestos");
        (linea.GrossAmount + linea.TaxAmount).Should().Be(35700m);
    }

    [Fact]
    public async Task El_residuo_de_una_lista_con_impuestos_queda_visible_y_el_total_cuadra()
    {
        var (c, p, _, p3, und) = await EscenarioAsync();
        var lista = c.C.Db.PriceListItems.Single(i => i.Price == 11900m);
        lista.Price = 9999m;
        await c.C.Db.SaveChangesAsync();

        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p3, und, 7m, 7m)], conSucursal: true), default);

        var linea = r.Value.Lineas.Single();
        r.Value.Totales.Total.Should().Be(69993m, "7 × 9.999");
        linea.UnitPrice.Should().Be(Math.Round(9999m / 1.19m, 6, MidpointRounding.AwayFromZero));
        (linea.GrossAmount + linea.TaxAmount).Should().Be(69993m);
        (linea.GrossAmount - linea.Residuo).Should().Be(Math.Round(7m * linea.UnitPrice, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task Un_descuento_sobre_el_tope_no_se_rechaza_queda_pidiendo_aprobacion()
    {
        var (c, p, p1, _, und) = await EscenarioAsync(topeLinea: 0.05m);

        var r = await p.PrecificarAsync(Pedido(c,
        [
            new LineaAPrecificar(1, p1, und, 1m, 1m, DescuentoPorcentaje: 0.05m),
            new LineaAPrecificar(2, p1, und, 1m, 1m, DescuentoPorcentaje: 0.10m),
            new LineaAPrecificar(3, p1, und, 2m, 2m, PrecioDigitado: 1800m),
        ]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var l = r.Value.Lineas;
        l[0].Descuentos.Single().Should().Match<DescuentoCalculado>(d => d.Amount == 100m && !d.RequiresApproval && d.CapRateApplied == 0.05m);
        l[1].Descuentos.Single().RequiresApproval.Should().BeTrue();
        l[2].Descuentos.Single().Should().Match<DescuentoCalculado>(d => d.IsPriceOverride && d.Amount == 400m && d.RequiresApproval,
            "cambiar el precio de lista es un descuento del 10 %");
        r.Value.RequiereAprobacion.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_tope_todo_descuento_pide_aprobacion()
    {
        var (c, p, p1, _, und) = await EscenarioAsync();

        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p1, und, 1m, 1m, DescuentoValor: 10m)]), default);

        r.Value.Lineas.Single().Descuentos.Single().Should().Match<DescuentoCalculado>(d => d.RequiresApproval && d.CapRateApplied == 0m);
    }

    [Fact]
    public async Task El_descuento_por_total_se_prorratea_con_suma_exacta_y_se_mide_contra_el_tope_por_total()
    {
        var (c, p, p1, p3, und) = await EscenarioAsync(topeLinea: 0.05m, topeTotal: 0.02m);

        var r = await p.PrecificarAsync(Pedido(c,
        [
            new LineaAPrecificar(1, p1, und, 1m, 1m),
            new LineaAPrecificar(2, p3, und, 1m, 1m),
            new LineaAPrecificar(3, p1, und, 1m, 1m),
        ], descuentoTotal: 1000m), default);

        var partes = r.Value.Lineas.SelectMany(l => l.Descuentos).Where(d => d.FromDocumentDiscount).ToList();
        partes.Sum(d => d.Amount).Should().Be(1000m);
        partes.Should().OnlyContain(d => d.RequiresApproval && d.CapRateApplied == 0.02m, "1.000 sobre 14.000 es más del 2 %");
        r.Value.DescuentoPorTotal!.Rate.Should().Be(Math.Round(1000m / 14000m, 6, MidpointRounding.AwayFromZero));
        r.Value.Totales.DiscountTotal.Should().Be(1000m);
    }

    [Fact]
    public async Task Bajo_costo_alerta_por_defecto_y_bloquea_si_la_politica_lo_dice()
    {
        var (c, p, p1, _, und) = await EscenarioAsync();
        await c.K.EntradaAsync(c.P1, 10m, 2500m);

        var alerta = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p1, und, 1m, 1m)]), default);
        c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasBajoCosto, "Bloquear");
        p = new PrecificacionDeVenta(c.C.Db, new LectorDeCatalogoTributario(c.C.Db, new LectorDeUvt(c.C.Db), c.K.Lector()), c.K.Lector());
        var bloqueo = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p1, und, 1m, 1m)]), default);

        var linea = alerta.Value.Lineas.Single();
        (linea.BelowCost, linea.AverageCost, linea.Available).Should().Be((true, 2500m, 10m));
        alerta.Value.Avisos.Should().ContainSingle(a => a.Contains("costo"));
        bloqueo.Error.Code.Should().Be(ErroresDePrecios.BelowCostCode);
    }

    [Fact]
    public async Task Una_linea_con_su_lista_fijada_no_se_vuelve_a_resolver()
    {
        var (c, p, p1, _, und) = await EscenarioAsync();

        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p1, und, 1m, 1m, ListaFijada: new PrecioFijado(null, 1500m, false))]), default);

        r.Value.Lineas.Single().Should().Match<LineaPrecificada>(l => l.ListPrice == 1500m && l.PriceListId == null);
    }

    [Fact]
    public async Task Aplicar_a_la_linea_escribe_precios_y_descuentos_y_da_de_baja_los_anteriores()
    {
        var (c, p, p1, _, und) = await EscenarioAsync(topeLinea: 0.05m);
        var r = await p.PrecificarAsync(Pedido(c, [new LineaAPrecificar(1, p1, und, 1m, 1m, DescuentoPorcentaje: 0.2m)]), default);
        var documento = new Domain.Entities.Inventory.Documents.InventoryDocument { Id = 5 };
        var linea = new Domain.Entities.Inventory.Documents.InventoryDocumentLine { Id = 9, LineNumber = 1 };
        var viejo = new Domain.Entities.Inventory.Documents.DocumentLineDiscount { Amount = 1m };

        var nuevos = PrecificacionDeVenta.AplicarALinea(documento, linea, r.Value.Lineas[0], [viejo], "Cliente frecuente");

        (linea.ListPrice, linea.UnitPrice, linea.GrossAmount, linea.DiscountAmount, linea.NetAmount).Should().Be((2000m, 2000m, 2000m, 400m, 1600m));
        viejo.IsDeleted.Should().BeTrue();
        nuevos.Single().Should().Match<Domain.Entities.Inventory.Documents.DocumentLineDiscount>(d =>
            d.DocumentId == 5 && d.RequiresApproval && d.Reason == "Cliente frecuente" && d.Rate == 0.2m);
    }
}
