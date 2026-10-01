using FluentAssertions;
using IngenIA365ERP.Domain.Inventory.Catalog;

namespace IngenIA365ERP.Domain.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, I6, T902 (US15-1; data-model §1.6, §1.11): el motor puro <see cref="GeneradorDeVariantes"/> produce, de los atributos
/// elegidos y sus valores, las combinaciones que faltan: <c>VariantKey</c> normalizada y ordenada por código de atributo
/// (<c>COLOR=AZUL;TALLA=M</c>), código propuesto <c>{plantilla}-{valor}-{valor}</c> recortado al largo del código de producto y nombre
/// «{plantilla} {valor} {valor}»; agregar un valor genera sólo las combinaciones nuevas y una existente no se repite.
/// </summary>
public class GeneradorDeVariantesTests
{
    private static readonly AtributoParaVariantes Talla = new("TALLA", "Talla", [new("S", "S", 1), new("M", "M", 2)]);
    private static readonly AtributoParaVariantes Color = new("COLOR", "Color", [new("AZUL", "Azul", 1), new("ROJO", "Rojo", 2)]);

    private static PedidoDeVariantes Pedido(IReadOnlyList<AtributoParaVariantes> atributos, params string[] existentes) =>
        new("CAMISA", "Camisa polo", atributos, existentes);

    [Fact]
    public void Dos_tallas_por_dos_colores_son_cuatro_combinaciones()
    {
        var r = GeneradorDeVariantes.Generar(Pedido([Talla, Color]));

        r.Rechazo.Should().BeNull();
        r.Propuestas.Should().HaveCount(4);
        r.Propuestas.Select(p => p.VariantKey).Should().Equal(
            "COLOR=AZUL;TALLA=S", "COLOR=ROJO;TALLA=S", "COLOR=AZUL;TALLA=M", "COLOR=ROJO;TALLA=M");
    }

    [Fact]
    public void La_clave_va_ordenada_por_codigo_de_atributo_y_el_codigo_y_el_nombre_en_el_orden_elegido()
    {
        var r = GeneradorDeVariantes.Generar(Pedido([Talla, Color]));

        var primera = r.Propuestas[0];
        primera.VariantKey.Should().Be("COLOR=AZUL;TALLA=S", "la clave se ordena por código de atributo, no por el orden elegido");
        primera.Codigo.Should().Be("CAMISA-S-AZUL");
        primera.Nombre.Should().Be("Camisa polo S Azul");
        primera.Valores.Select(v => (v.Atributo, v.Valor)).Should().Equal(("TALLA", "S"), ("COLOR", "AZUL"));
        primera.CodigoRecortado.Should().BeFalse();
    }

    [Fact]
    public void Los_codigos_se_normalizan_a_mayusculas_sin_espacios_alrededor()
    {
        var talla = new AtributoParaVariantes(" talla ", "Talla", [new(" m ", "M", 1)]);
        var r = GeneradorDeVariantes.Generar(new PedidoDeVariantes("camisa", "Camisa", [talla], []));

        r.Propuestas.Single().VariantKey.Should().Be("TALLA=M");
        r.Propuestas.Single().Codigo.Should().Be("CAMISA-M");
    }

    [Fact]
    public void El_codigo_propuesto_se_recorta_al_largo_del_codigo_de_producto()
    {
        var r = GeneradorDeVariantes.Generar(new PedidoDeVariantes("CAMISETAMANGALARGA", "Camiseta manga larga", [Talla, Color], []));

        var propuesta = r.Propuestas[0];
        GeneradorDeVariantes.LargoDelCodigo.Should().Be(20, "el código de producto es CodigoDeCatalogo.LargoLargo (data-model §1.6)");
        propuesta.Codigo.Should().Be("CAMISETAMANGALARGA-S");
        propuesta.Codigo.Length.Should().BeLessThanOrEqualTo(GeneradorDeVariantes.LargoDelCodigo);
        propuesta.CodigoRecortado.Should().BeTrue("la pantalla avisa que el código propuesto se recortó y hay que revisarlo");
        propuesta.Codigo.Should().NotEndWith("-", "un recorte no deja el separador colgando");
        propuesta.CodigoRepetido.Should().BeTrue("S-AZUL y S-ROJO quedan con el mismo código recortado: la persona tiene que cambiar uno");
        GeneradorDeVariantes.Generar(Pedido([Talla, Color])).Propuestas.Should().OnlyContain(p => !p.CodigoRepetido);
    }

    [Fact]
    public void Agregar_un_valor_genera_solo_las_combinaciones_nuevas()
    {
        var existentes = new[] { "COLOR=AZUL;TALLA=S", "COLOR=ROJO;TALLA=S", "COLOR=AZUL;TALLA=M", "COLOR=ROJO;TALLA=M" };
        var talla = Talla with { Valores = [.. Talla.Valores, new("L", "L", 3)] };

        var r = GeneradorDeVariantes.Generar(Pedido([talla, Color], existentes));

        r.Propuestas.Select(p => p.VariantKey).Should().Equal("COLOR=AZUL;TALLA=L", "COLOR=ROJO;TALLA=L");
        r.Existentes.Should().HaveCount(4, "las combinaciones que ya existen se informan y no se proponen");
    }

    [Fact]
    public void Una_combinacion_existente_no_se_repite_aunque_su_clave_venga_en_otro_orden_o_en_minusculas()
    {
        var r = GeneradorDeVariantes.Generar(Pedido([Talla, Color], "talla=s;color=azul"));

        r.Propuestas.Should().HaveCount(3);
        r.Propuestas.Should().NotContain(p => p.VariantKey == "COLOR=AZUL;TALLA=S");
    }

    [Fact]
    public void La_clave_de_una_combinacion_es_la_misma_en_cualquier_orden()
    {
        GeneradorDeVariantes.ClaveDe([("TALLA", "M"), ("COLOR", "AZUL")]).Should().Be("COLOR=AZUL;TALLA=M");
        GeneradorDeVariantes.Normalizar("talla=m; color=azul").Should().Be("COLOR=AZUL;TALLA=M");
    }

    [Fact]
    public void Las_variantes_de_una_plantilla_llevan_los_mismos_atributos()
    {
        var r = GeneradorDeVariantes.Generar(Pedido([Talla, Color], "TALLA=S"));

        r.Rechazo.Should().NotBeNull();
        r.Rechazo!.Codigo.Should().Be(GeneradorDeVariantes.CodigoAtributosDistintos);
        r.Propuestas.Should().BeEmpty();
    }

    [Fact]
    public void Un_atributo_repetido_o_un_valor_repetido_no_se_admite()
    {
        GeneradorDeVariantes.Generar(Pedido([Talla, Talla])).Rechazo!.Codigo.Should().Be(GeneradorDeVariantes.CodigoAtributoRepetido);

        var repetido = Talla with { Valores = [new("S", "S", 1), new("s", "Small", 2)] };
        GeneradorDeVariantes.Generar(Pedido([repetido])).Rechazo!.Codigo.Should().Be(GeneradorDeVariantes.CodigoAtributoRepetido);
    }

    [Fact]
    public void Sin_atributos_o_con_un_atributo_sin_valores_no_hay_combinaciones()
    {
        GeneradorDeVariantes.Generar(Pedido([])).Rechazo!.Codigo.Should().Be(GeneradorDeVariantes.CodigoSinAtributos);
        GeneradorDeVariantes.Generar(Pedido([Talla, Color with { Valores = [] }])).Rechazo!.Codigo
            .Should().Be(GeneradorDeVariantes.CodigoSinAtributos);
    }

    [Fact]
    public void Los_valores_van_en_su_orden_de_presentacion()
    {
        var talla = new AtributoParaVariantes("TALLA", "Talla", [new("XL", "XL", 4), new("S", "S", 1), new("M", "M", 2)]);
        var r = GeneradorDeVariantes.Generar(Pedido([talla]));

        r.Propuestas.Select(p => p.Codigo).Should().Equal("CAMISA-S", "CAMISA-M", "CAMISA-XL");
    }
}
