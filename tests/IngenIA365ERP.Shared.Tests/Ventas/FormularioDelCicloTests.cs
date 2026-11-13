using FluentAssertions;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T894–T898 (feature 012, I6): el formulario común de los documentos del ciclo comercial. Releer un borrador conserva la línea origen de cada
/// línea (sin ella, volver a guardar perdería el vínculo con el pedido o la remisión) y no relee el descuento de una promoción como manual
/// (el servidor lo rechazaría, <c>Inventory.Discount.PromotionApplied</c>); la línea dice qué promoción se aplicó; las remisiones se facturan
/// juntas sólo si son confirmadas y del mismo cliente. (nuevo)
/// </summary>
public class FormularioDelCicloTests
{
    private static readonly Guid Origen = Guid.NewGuid();

    private static LineaDeVentaDto Linea(params DescuentoDeLineaDeVentaDto[] descuentos) => new(
        Guid.NewGuid(), 1, new ReferenciaDeInventarioDto(Guid.NewGuid(), "ARZ-1", "Arroz"), new UnidadDeLineaDto(Guid.NewGuid(), "UND"), 1m, 3m, 3m, 0m,
        2000m, 2000m, false, null, descuentos, [], 4000m, 4000m, false, Origen, 3m);

    [Fact]
    public void Releer_conserva_el_origen_y_no_toma_la_promocion_como_manual()
    {
        var promocion = new DescuentoDeLineaDeVentaDto(TextosDeVentas.DescuentoDePromocion, null, 2000m, false, false, null, Guid.NewGuid(), "Lleve 3 pague 2");
        var linea = FormularioDelCiclo.Lineas(Documento(Linea(promocion))).Single();

        (linea.OriginLinePublicId, linea.Descuento, linea.ConPromocion, linea.Precio).Should().Be((Origen, (decimal?)null, true, (decimal?)null));

        linea.Descuento = 10m;
        var pedido = FormularioDelCiclo.Pedido([linea]).Single();
        pedido.OriginLinePublicId.Should().Be(Origen);
        pedido.Discount.Should().BeNull("una línea con promoción no admite descuento manual");
        pedido.LineNumber.Should().Be(1);
    }

    [Fact]
    public void El_descuento_manual_se_relee_en_porcentaje()
    {
        var manual = new DescuentoDeLineaDeVentaDto(1, 0.05m, 300m, false, false, null);
        var linea = FormularioDelCiclo.Lineas(Documento(Linea(manual))).Single();

        linea.Descuento.Should().Be(5m);
        FormularioDelCiclo.Pedido([linea]).Single().Discount!.Percent.Should().Be(0.05m);
    }

    [Fact]
    public void Con_una_lista_que_incluye_el_iva_releer_no_inventa_un_precio_digitado()
    {
        // Lista de 40.000 con IVA del 19 %: la línea guarda 33.613,45 sin impuestos y ningún descuento.
        var linea = LineaConIva(40000m, 33613.445378m);

        FormularioDelCiclo.Lineas(Documento(linea)).Single().Precio.Should().BeNull();
        FormularioDelCiclo.Pedido(FormularioDelCiclo.Lineas(Documento(linea))).Single().UnitPrice.Should().BeNull();
    }

    [Fact]
    public void El_precio_digitado_se_relee_en_la_base_de_la_lista()
    {
        // Se escribió 36.000 sobre una lista de 40.000 con IVA: descuento del 10 % por cambio de precio.
        var porPrecio = new DescuentoDeLineaDeVentaDto(1, 0.1m, 3361.34m, false, true, null);
        var linea = LineaConIva(40000m, 33613.445378m, porPrecio);

        FormularioDelCiclo.Lineas(Documento(linea)).Single().Precio.Should().Be(36000m);
    }

    [Fact]
    public void Al_releer_se_conserva_el_precio_exacto_que_se_escribio()
    {
        var porPrecio = new DescuentoDeLineaDeVentaDto(1, 0.333333m, 13333.33m, false, true, null);
        var linea = LineaConIva(40000m, 33613.445378m, porPrecio);
        var antes = FormularioDelCiclo.Lineas(Documento(linea));
        antes[0].Precio = 26666.67m;
        var despues = FormularioDelCiclo.Lineas(Documento(linea));

        FormularioDelCiclo.ConservarSeguimiento(antes, despues);

        despues[0].Precio.Should().Be(26666.67m);
    }

    private static LineaDeVentaDto LineaConIva(decimal lista, decimal unitario, params DescuentoDeLineaDeVentaDto[] descuentos) => new(
        Guid.NewGuid(), 1, new ReferenciaDeInventarioDto(Guid.NewGuid(), "CAM-1", "Camisa"), new UnidadDeLineaDto(Guid.NewGuid(), "UND"), 1m, 1m, 1m, 0m,
        lista, unitario, true, null, descuentos, [], unitario, lista, false);

    [Fact]
    public void La_linea_dice_que_promocion_se_aplico()
    {
        var promocion = new DescuentoDeLineaDeVentaDto(TextosDeVentas.DescuentoDePromocion, null, 2000m, false, false, null, Guid.NewGuid(), "Lleve 3 pague 2");

        FormularioDelCiclo.Promociones(Linea(promocion)).Should().Be($"Lleve 3 pague 2 · {2000m:N0}");
        FormularioDelCiclo.Promociones(Linea()).Should().BeEmpty();
    }

    [Fact]
    public void Las_remisiones_se_facturan_juntas_si_son_confirmadas_y_del_mismo_cliente()
    {
        var ana = Guid.NewGuid();
        ResumenDeVentaDto Remision(Guid? cliente, int estado = 2) => new(Guid.NewGuid(), TextosDeVentas.ClaseRemision, "REM", "REM", 1, estado,
            new DateOnly(2026, 9, 20), null, null, "Ana", null, 1000m, 1000m, [], null, null, false, false, cliente);

        FormularioDelCiclo.NoSePuedenFacturarJuntas([Remision(ana), Remision(ana)]).Should().BeNull();
        FormularioDelCiclo.NoSePuedenFacturarJuntas([Remision(ana), Remision(Guid.NewGuid())]).Should().NotBeNull();
        FormularioDelCiclo.NoSePuedenFacturarJuntas([Remision(ana, estado: 0)]).Should().NotBeNull();
        FormularioDelCiclo.NoSePuedenFacturarJuntas([]).Should().NotBeNull();
        FormularioDelCiclo.DiasDesde(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 29)).Should().Be(9);
    }

    private static DocumentoDeVentaDto Documento(LineaDeVentaDto linea) => Documento([linea]);

    private static DocumentoDeVentaDto Documento(IReadOnlyList<LineaDeVentaDto> lineas) => new(
        Guid.NewGuid(), TextosDeVentas.ClasePedido, new ReferenciaDeInventarioDto(Guid.NewGuid(), "PED", "Pedido"), "PED", null, 0, new DateOnly(2026, 9, 29), null,
        null, new ReferenciaDeInventarioDto(Guid.NewGuid(), "S1", "Sede"), null, null, null, null, null, null, null, [.. lineas], [],
        new TotalesDeVentaDto(4000m, 0m, 0m, 0m, 4000m, 4000m), [], [], null, new VinculosDeVentaDto(null, null, null, [], null, null),
        new UsuarioDeInventarioDto(null, "ana"), null, [], null);

    private static LineaDeVentaDto Guardada(int numero, Guid producto) => new(
        Guid.NewGuid(), numero, new ReferenciaDeInventarioDto(producto, "P" + numero, "Producto " + numero), new UnidadDeLineaDto(Guid.NewGuid(), "UND"), 1m, 1m, 1m, 0m,
        2000m, 2000m, false, null, [], [], 2000m, 2000m, false, null, null);

    // ------------------------------------------------------------- 2026-11-12: el guardado automático y lo que cambió mientras tanto --

    [Fact]
    public void Lo_guardado_mientras_se_seguia_escribiendo_solo_presta_los_identificadores_de_las_lineas_nuevas()
    {
        // Viajaron dos líneas nuevas; mientras el servidor guardaba, la persona agregó una tercera. La pantalla conserva sus tres
        // líneas y sólo toma los identificadores de las dos que viajaron: si no, el guardado siguiente las duplicaría.
        var arroz = new LineaDelCiclo { ProductPublicId = Guid.NewGuid(), Producto = "Arroz" };
        var azucar = new LineaDelCiclo { ProductPublicId = Guid.NewGuid(), Producto = "Azúcar" };
        var enviadas = new List<LineaDelCiclo> { arroz, azucar };
        var enPantalla = new List<LineaDelCiclo> { arroz, azucar, new() { ProductPublicId = Guid.NewGuid(), Producto = "Café" } };
        var guardadas = new[] { Guardada(1, arroz.ProductPublicId), Guardada(2, azucar.ProductPublicId) };

        FormularioDelCiclo.TomarIdentificadores(enviadas, Documento(guardadas));

        arroz.LinePublicId.Should().Be(guardadas[0].LinePublicId);
        azucar.LinePublicId.Should().Be(guardadas[1].LinePublicId);
        enPantalla.Should().HaveCount(3);
        enPantalla[2].LinePublicId.Should().BeNull("la que no viajó sigue siendo nueva");
    }

    [Fact]
    public void Tomar_identificadores_no_pisa_los_que_ya_tenia_ni_toma_el_de_otro_producto()
    {
        var conId = new LineaDelCiclo { LinePublicId = Guid.NewGuid(), ProductPublicId = Guid.NewGuid() };
        var otra = new LineaDelCiclo { ProductPublicId = Guid.NewGuid() };
        var antes = conId.LinePublicId;

        FormularioDelCiclo.TomarIdentificadores([conId, otra], Documento([Guardada(1, conId.ProductPublicId), Guardada(2, Guid.NewGuid())]));

        conId.LinePublicId.Should().Be(antes);
        otra.LinePublicId.Should().BeNull("en esa posición quedó otro producto");
    }

    [Fact]
    public void El_precio_de_lista_sale_de_la_busqueda_y_el_del_empaque_si_la_lista_lo_trae()
    {
        var empaque = Guid.NewGuid();
        var producto = new ProductoEncontradoDto(Guid.NewGuid(), "ARZ", "Arroz", null, null, "UND", 1, null, null, null, Price: 2500m,
            Units: [new UnidadDelProductoEncontradoDto(Guid.NewGuid(), null, "UND", 1m, true, 2500m), new UnidadDelProductoEncontradoDto(Guid.NewGuid(), empaque, "CAJA12", 12m, false, 28000m)]);

        FormularioDelCiclo.PrecioDeLista(new ProductoElegido(producto, null)).Should().Be(2500m);
        FormularioDelCiclo.PrecioDeLista(new ProductoElegido(producto, new EmpaqueDto(empaque, "CAJA12", 12m))).Should().Be(28000m);
        FormularioDelCiclo.PrecioDeLista(new ProductoElegido(producto with { Price = null, Units = null }, null)).Should().BeNull();
    }
}
