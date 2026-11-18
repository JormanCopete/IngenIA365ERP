using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;

namespace IngenIA365ERP.Domain.Tests.Lending.SignatureDocuments;

/// <summary>2026-11-18: el lenguaje de las plantillas de los documentos para firma (<see cref="PlantillaDeFirma"/>).</summary>
public class PlantillaDeFirmaTests
{
    private static DatosDePlantilla Datos(Dictionary<string, string?> campos, params IReadOnlyDictionary<string, string?>[] codeudores) =>
        new(campos, new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>> { [CamposDelSistema.Codeudores] = codeudores });

    private static IReadOnlyList<BloqueDeTexto> Armar(string texto, DatosDePlantilla datos)
    {
        PlantillaDeFirma.Validar(texto, CatalogoDeCampos.DelSistema).Should().BeEmpty();
        return PlantillaDeFirma.Armar(texto, datos).Bloques;
    }

    [Fact]
    public void Pone_los_campos_y_cambia_las_letras()
    {
        var bloques = Armar("Yo, {{DEUDOR_NOMBRE:MAYUSCULAS}}, con {{DEUDOR_TIPO_DOCUMENTO}} {{DEUDOR_DOCUMENTO}}.",
            Datos(new() { ["DEUDOR_NOMBRE"] = "María Pérez", ["DEUDOR_TIPO_DOCUMENTO"] = "C.C.", ["DEUDOR_DOCUMENTO"] = "31.234.567" }));

        bloques.Should().ContainSingle().Which.TextoPlano.Should().Be("Yo, MARÍA PÉREZ, con C.C. 31.234.567.");
    }

    [Fact]
    public void Un_campo_vacio_se_imprime_como_raya_y_se_informa()
    {
        var armado = PlantillaDeFirma.Armar("Correo: {{DEUDOR_CORREO}}", Datos(new()));

        armado.Bloques.Single().TextoPlano.Should().Be($"Correo: {PlantillaDeFirma.Raya}");
        armado.CamposVacios.Should().Equal("DEUDOR_CORREO");
    }

    [Fact]
    public void El_blanco_tiene_el_largo_pedido()
    {
        Armar("Valor: ${{BLANCO:10}}", DatosDePlantilla.Vacios).Single().TextoPlano.Should().Be("Valor: $__________");
        Armar("{{BLANCO}}", DatosDePlantilla.Vacios).Single().TextoPlano.Should().Be(new string('_', PlantillaDeFirma.BlancoPorDefecto));
    }

    [Fact]
    public void Si_sino_y_no_eligen_el_texto_sin_dejar_renglones_vacios()
    {
        const string texto = """
            Inicio
            {{#SI ES_LIBRANZA}}
            Por nómina
            {{#SINO}}
            Por caja
            {{/SI}}
            {{#NO HAY_CODEUDORES}}
            Sin codeudores
            {{/NO}}
            Fin
            """;

        Armar(texto, Datos(new() { ["ES_LIBRANZA"] = "SI" })).Select(b => b.TextoPlano).Should().Equal("Inicio", "Por nómina", "Sin codeudores", "Fin");
        Armar(texto, Datos(new() { ["ES_LIBRANZA"] = "NO", ["HAY_CODEUDORES"] = "SI" })).Select(b => b.TextoPlano).Should().Equal("Inicio", "Por caja", "Fin");
    }

    [Fact]
    public void Una_lista_es_verdadera_si_tiene_elementos()
    {
        var uno = new Dictionary<string, string?> { ["CODEUDOR_NOMBRE"] = "Juan" };
        Armar("{{#SI CODEUDORES}}con{{#SINO}}sin{{/SI}}", Datos(new(), uno)).Single().TextoPlano.Should().Be("con");
        Armar("{{#SI CODEUDORES}}con{{#SINO}}sin{{/SI}}", Datos(new())).Single().TextoPlano.Should().Be("sin");
    }

    [Fact]
    public void Cada_repite_por_elemento_con_su_orden_y_los_campos_generales()
    {
        const string texto = """
            {{#CADA CODEUDORES}}
            - {{ORDEN}}. {{CODEUDOR_NOMBRE}} ({{SOLICITUD_NUMERO}})
            {{/CADA}}
            """;
        var bloques = Armar(texto, Datos(new() { ["SOLICITUD_NUMERO"] = "SC1" },
            new Dictionary<string, string?> { ["CODEUDOR_NOMBRE"] = "Juan" }, new Dictionary<string, string?> { ["CODEUDOR_NOMBRE"] = "Ana" }));

        bloques.Should().HaveCount(2).And.OnlyContain(b => b.Tipo == TipoDeBloque.Vineta);
        bloques.Select(b => b.TextoPlano).Should().Equal("1. Juan (SC1)", "2. Ana (SC1)");
    }

    [Fact]
    public void Los_renglones_dan_titulo_subtitulo_centrado_salto_y_firmas()
    {
        const string texto = """
            # PAGARÉ No. {{PAGARE_NUMERO}}
            ## Cláusulas
            = Ciudad y fecha


            Texto
            ---
            {{FIRMAS}}
            """;
        var bloques = Armar(texto, Datos(new() { ["PAGARE_NUMERO"] = "SC1" }));

        bloques.Select(b => b.Tipo).Should().Equal(TipoDeBloque.Titulo, TipoDeBloque.Subtitulo, TipoDeBloque.Centrado, TipoDeBloque.Espacio,
            TipoDeBloque.Parrafo, TipoDeBloque.SaltoDePagina, TipoDeBloque.Firmas);
        bloques[0].TextoPlano.Should().Be("PAGARÉ No. SC1");
    }

    [Fact]
    public void La_negrita_viene_de_la_plantilla_no_de_los_valores()
    {
        var bloque = Armar("**Deudor:** {{DEUDOR_NOMBRE}}", Datos(new() { ["DEUDOR_NOMBRE"] = "**# Raro**" })).Single();

        bloque.Tipo.Should().Be(TipoDeBloque.Parrafo);
        bloque.Tramos.Should().Equal(new TramoDeTexto("Deudor:", true), new TramoDeTexto(" **# Raro**", false));
    }

    [Fact]
    public void Un_valor_que_empieza_con_numeral_no_vuelve_titulo_el_renglon()
    {
        Armar("{{DEUDOR_DIRECCION}}", Datos(new() { ["DEUDOR_DIRECCION"] = "# 5-20" })).Single().Tipo.Should().Be(TipoDeBloque.Parrafo);
    }

    [Theory]
    [InlineData("{{NO_EXISTE}}", "no existe")]
    [InlineData("{{#SI ES_LIBRANZA}}abierto", "Falta cerrar")]
    [InlineData("{{/SI}}", "no se abrió")]
    [InlineData("{{#SI ES_LIBRANZA}}x{{/CADA}}", "no cierra")]
    [InlineData("Firma: {{FIRMAS}}", "solo en su renglón")]
    [InlineData("{{BLANCO:500}}", "largo")]
    [InlineData("{{CODEUDOR_NOMBRE}}", "sólo existe dentro")]
    [InlineData("{{CODEUDORES}}", "es una lista")]
    [InlineData("{{#CADA DEUDOR_NOMBRE}}x{{/CADA}}", "no es una lista")]
    [InlineData("{{DEUDOR_NOMBRE:NEGRITA}}", "modificadores")]
    [InlineData("{{#REPETIR X}}", "no existe")]
    [InlineData("{{Deudor nombre}}", "no es un campo")]
    [InlineData("abre {{ sin cerrar", "Falta cerrar")]
    public void Valida_la_plantilla(string texto, string mensaje)
    {
        PlantillaDeFirma.Validar(texto, CatalogoDeCampos.DelSistema).Should().Contain(e => e.Mensaje.Contains(mensaje));
    }

    [Fact]
    public void Valida_los_campos_propios_y_dice_el_renglon()
    {
        var catalogo = new CatalogoDeCampos(["CIUDAD_PAGO"]);
        PlantillaDeFirma.Validar("{{CIUDAD_PAGO}}", catalogo).Should().BeEmpty();
        PlantillaDeFirma.Validar("uno\ndos\n{{OTRO}}", catalogo).Single().Renglon.Should().Be(3);
    }

    [Fact]
    public void Con_errores_no_arma()
    {
        var armar = () => PlantillaDeFirma.Armar("{{#SI X}}", DatosDePlantilla.Vacios);
        armar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dice_que_campos_usa()
    {
        PlantillaDeFirma.CamposUsados("{{A}} {{#SI B}}{{C}}{{/SI}} {{#CADA CODEUDORES}}{{D}}{{/CADA}}")
            .Should().BeEquivalentTo(["A", "B", "C", "CODEUDORES", "D"]);
    }

    [Fact]
    public void Los_ejemplos_del_sistema_arman_todos_los_campos()
    {
        var texto = string.Join("\n", CamposDelSistema.Campos.Select(c => $"{{{{{c.Codigo}}}}}"));
        PlantillaDeFirma.Armar(texto, CamposDelSistema.Ejemplo()).CamposVacios.Should().BeEmpty();
    }

    [Theory]
    [InlineData("CIUDAD_PAGO", true)]
    [InlineData("C1", true)]
    [InlineData("ciudad", false)]
    [InlineData("1CIUDAD", false)]
    [InlineData("A", false)]
    [InlineData("CIUDAD PAGO", false)]
    public void El_codigo_de_un_campo_propio(string codigo, bool valido) => CamposDelSistema.CodigoValido(codigo).Should().Be(valido);

    [Fact]
    public void La_plantilla_aplica_por_canal_y_por_linea()
    {
        var libranza = new SignatureDocumentTemplate { RepaymentChannels = "OwnPayroll, PaymasterDeduction" };
        libranza.Aplica(RepaymentChannel.OwnPayroll, "EMPL15").Should().BeTrue();
        libranza.Aplica(RepaymentChannel.Cashier, "EMPL15").Should().BeFalse();

        var deLinea = new SignatureDocumentTemplate { CreditLineCodes = "EMPL15" };
        deLinea.Aplica(RepaymentChannel.Cashier, "empl15").Should().BeTrue();
        deLinea.Aplica(RepaymentChannel.Cashier, "SOAT").Should().BeFalse();

        new SignatureDocumentTemplate { IsActive = false }.Aplica(RepaymentChannel.Cashier, "SOAT").Should().BeFalse();
    }

    [Fact]
    public void Cambiar_el_texto_sube_la_version_y_quita_la_validacion()
    {
        var plantilla = new SignatureDocumentTemplate { Body = "uno" };
        plantilla.Validar(7, DateTime.UtcNow, "ok");

        plantilla.CambiarTexto("uno");
        plantilla.Version.Should().Be(1);
        plantilla.IsVerified.Should().BeTrue();

        plantilla.CambiarTexto("dos");
        plantilla.Version.Should().Be(2);
        plantilla.IsVerified.Should().BeFalse();
        plantilla.VerifiedByUserId.Should().BeNull();
    }
}
