using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Catalog;

namespace IngenIA365ERP.Domain.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, I6, T903 (data-model §1.11; FR-023): <see cref="ValidadorDeComponentes"/> revisa los componentes de un combo o kit
/// —cada uno <c>Inventoriable</c> o <c>Variant</c>, sin ciclos sobre el grafo recibido, cantidad &gt; 0 en la unidad base del
/// componente y con sus decimales— y un combo o kit sin componentes no es válido.
/// </summary>
public class ValidadorDeComponentesTests
{
    private const int Kit = 1, A = 2, B = 3, Servicio = 4, Plantilla = 5, OtroKit = 6, Combo = 7, Variante = 8, Metros = 9;

    private static readonly Dictionary<int, ProductoDelGrafo> Productos = new()
    {
        [Kit] = new(Kit, "KIT", ProductKind.Kit, 0),
        [A] = new(A, "A", ProductKind.Inventoriable, 0),
        [B] = new(B, "B", ProductKind.Inventoriable, 0),
        [Servicio] = new(Servicio, "FLETE", ProductKind.Service, 0),
        [Plantilla] = new(Plantilla, "CAMISA", ProductKind.Template, 0),
        [OtroKit] = new(OtroKit, "KIT2", ProductKind.Kit, 0),
        [Combo] = new(Combo, "COMBO", ProductKind.Combo, 0),
        [Variante] = new(Variante, "CAMISA-M", ProductKind.Variant, 0),
        [Metros] = new(Metros, "CABLE", ProductKind.Inventoriable, 2),
    };

    private static ResultadoDeComponentes Validar(int producto, IReadOnlyList<ComponentePropuesto> componentes,
        Dictionary<int, IReadOnlyList<int>>? grafo = null) =>
        ValidadorDeComponentes.Validar(new PedidoDeComponentes(producto, componentes, Productos, grafo ?? []));

    [Fact]
    public void Un_kit_con_inventariables_y_variantes_es_valido()
    {
        var r = Validar(Kit, [new(A, 2m), new(B, 1m), new(Variante, 3m), new(Metros, 1.25m)]);

        r.Valido.Should().BeTrue(string.Join(" | ", r.Errores.Select(e => e.Mensaje)));
    }

    [Theory]
    [InlineData(Servicio)]
    [InlineData(Plantilla)]
    [InlineData(OtroKit)]
    [InlineData(Combo)]
    public void Un_componente_solo_puede_ser_inventariable_o_variante(int componente)
    {
        var r = Validar(Kit, [new(A, 1m), new(componente, 1m)]);

        r.Valido.Should().BeFalse();
        r.Errores.Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoClaseInvalida && e.ComponentProductId == componente);
    }

    [Fact]
    public void Un_combo_o_kit_sin_componentes_no_es_valido()
    {
        Validar(Kit, []).Errores.Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoSinComponentes);
        Validar(Combo, []).Errores.Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoSinComponentes);
    }

    [Theory]
    [InlineData(ProductKind.Inventoriable)]
    [InlineData(ProductKind.Service)]
    [InlineData(ProductKind.Template)]
    [InlineData(ProductKind.Variant)]
    public void Solo_un_combo_o_un_kit_lleva_componentes(ProductKind clase)
    {
        var productos = new Dictionary<int, ProductoDelGrafo>(Productos) { [99] = new(99, "X", clase, 0) };
        var r = ValidadorDeComponentes.Validar(new PedidoDeComponentes(99, [new(A, 1m)], productos, new Dictionary<int, IReadOnlyList<int>>()));

        r.Errores.Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoNoEsComboNiKit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void La_cantidad_es_mayor_que_cero(int cantidad)
    {
        Validar(Kit, [new(A, cantidad)]).Errores
            .Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoCantidadInvalida && e.ComponentProductId == A);
    }

    [Fact]
    public void La_cantidad_respeta_los_decimales_de_la_unidad_base_del_componente()
    {
        Validar(Kit, [new(A, 1.5m)]).Errores
            .Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoCantidadInvalida, "A se cuenta en unidades enteras");
        Validar(Kit, [new(Metros, 1.255m)]).Errores
            .Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoCantidadInvalida, "el cable admite dos decimales");
        Validar(Kit, [new(Metros, 1.25m)]).Valido.Should().BeTrue();
    }

    [Fact]
    public void Un_producto_no_es_componente_de_si_mismo()
    {
        Validar(Kit, [new(Kit, 1m)]).Errores.Should().Contain(e => e.Codigo == ValidadorDeComponentes.CodigoCiclo);
    }

    [Fact]
    public void Un_ciclo_a_traves_del_grafo_recibido_se_rechaza()
    {
        // Datos heredados o una carga previa: VARIANTE ya lista al KIT como componente. Ponerla en el KIT cierra el ciclo.
        var grafo = new Dictionary<int, IReadOnlyList<int>> { [Variante] = [Kit] };

        var r = Validar(Kit, [new(A, 1m), new(Variante, 1m)], grafo);

        r.Errores.Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoCiclo && e.ComponentProductId == Variante);
    }

    [Fact]
    public void Los_componentes_actuales_del_propio_producto_se_reemplazan_y_no_cuentan_para_el_ciclo()
    {
        var grafo = new Dictionary<int, IReadOnlyList<int>> { [Kit] = [A, B] };

        Validar(Kit, [new(A, 2m)], grafo).Valido.Should().BeTrue();
    }

    [Fact]
    public void Un_componente_repetido_se_rechaza()
    {
        Validar(Kit, [new(A, 1m), new(A, 2m)]).Errores
            .Should().ContainSingle(e => e.Codigo == ValidadorDeComponentes.CodigoRepetido && e.ComponentProductId == A);
    }

    [Fact]
    public void Un_componente_que_no_esta_en_el_grafo_es_un_error_del_llamador()
    {
        var act = () => Validar(Kit, [new(12345, 1m)]);
        act.Should().Throw<ArgumentException>();
    }
}
