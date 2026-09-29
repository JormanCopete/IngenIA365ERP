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

    private static DocumentoDeVentaDto Documento(LineaDeVentaDto linea) => new(
        Guid.NewGuid(), TextosDeVentas.ClasePedido, new ReferenciaDeInventarioDto(Guid.NewGuid(), "PED", "Pedido"), "PED", null, 0, new DateOnly(2026, 9, 29), null,
        null, new ReferenciaDeInventarioDto(Guid.NewGuid(), "S1", "Sede"), null, null, null, null, null, null, null, [linea], [],
        new TotalesDeVentaDto(4000m, 0m, 0m, 0m, 4000m, 4000m), [], [], null, new VinculosDeVentaDto(null, null, null, [], null, null),
        new UsuarioDeInventarioDto(null, "ana"), null, [], null);
}
