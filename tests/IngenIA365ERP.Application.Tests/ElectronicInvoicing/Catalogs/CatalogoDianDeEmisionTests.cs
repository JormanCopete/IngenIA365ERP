using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Catalogs;

/// <summary>
/// Feature 012, I4, T702 (contracts/dian.md §4.3 y §4.4): lo que I4 suma a <see cref="CatalogoDian"/> —tipos de documento por
/// tipo y contingencia, tipos de operación, responsabilidades desde las marcas tributarias, tributos y esquema, tipo de persona,
/// forma de pago por clase y tipos de caja del DEE—, leído a una fecha desde los JSON embebidos. Los códigos 20, 94 y 95 están
/// marcados «por cotejar».
/// </summary>
public class CatalogoDianDeEmisionTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 5);
    private static readonly CatalogoDian Catalogo = CatalogoDian.Embebido;

    [Theory]
    [InlineData(ElectronicDocumentKind.Invoice, null, "01", "10", UniqueCodeKind.Cufe)]
    [InlineData(ElectronicDocumentKind.Invoice, ContingencyType.Dian04, "04", "10", UniqueCodeKind.Cufe)]
    [InlineData(ElectronicDocumentKind.Invoice, ContingencyType.Issuer03, "03", "10", UniqueCodeKind.Cufe)]
    [InlineData(ElectronicDocumentKind.CreditNote, null, "91", "20", UniqueCodeKind.Cude)]
    [InlineData(ElectronicDocumentKind.DebitNote, null, "92", "30", UniqueCodeKind.Cude)]
    [InlineData(ElectronicDocumentKind.PosEquivalent, null, "20", "10", UniqueCodeKind.Cude)]
    [InlineData(ElectronicDocumentKind.PosAdjustmentNote, null, "94", "20", UniqueCodeKind.Cude)]
    [InlineData(ElectronicDocumentKind.SupportDocument, null, "05", "10", UniqueCodeKind.Cuds)]
    [InlineData(ElectronicDocumentKind.SupportDocumentAdjustmentNote, null, "95", "10", UniqueCodeKind.Cuds)]
    [InlineData(ElectronicDocumentKind.RadianEvent030, null, "96", "", UniqueCodeKind.Cude)]
    public void El_tipo_de_documento_sale_del_catalogo_por_tipo_y_contingencia(ElectronicDocumentKind tipo, ContingencyType? contingencia,
        string codigo, string operacion, UniqueCodeKind codigoUnico)
    {
        var t = Catalogo.TipoDeDocumento(tipo, contingencia, Hoy)!;

        t.Codigo.Should().Be(codigo);
        t.TipoDeOperacion.Should().Be(operacion);
        t.CodigoUnico.Should().Be(codigoUnico);
        Catalogo.TiposDeDocumento(Hoy).Should().Contain(c => c.Codigo == codigo);
    }

    [Fact]
    public void La_contingencia_no_cambia_el_codigo_de_un_tipo_que_no_la_tiene()
    {
        Catalogo.TipoDeDocumento(ElectronicDocumentKind.CreditNote, ContingencyType.Dian04, Hoy)!.Codigo.Should().Be("91");
    }

    [Theory]
    [InlineData("20")]
    [InlineData("94")]
    [InlineData("95")]
    public void Los_codigos_del_DEE_y_de_la_nota_del_DS_estan_por_cotejar(string codigo)
    {
        Catalogo.EstaPorCotejar(CatalogoDian.Catalogos.TiposDeDocumento, codigo, Hoy).Should().BeTrue();
        Catalogo.EstaPorCotejar(CatalogoDian.Catalogos.TiposDeDocumento, "01", Hoy).Should().BeFalse();
    }

    [Fact]
    public void Los_tipos_de_operacion_dependen_del_tipo_de_documento()
    {
        Catalogo.TiposDeOperacion(ElectronicDocumentKind.CreditNote, Hoy).Select(c => c.Codigo).Should().BeEquivalentTo(["20", "22"]);
        Catalogo.TiposDeOperacion(ElectronicDocumentKind.SupportDocument, Hoy).Select(c => c.Nombre).Should().Contain("No residente");
    }

    [Fact]
    public void Las_marcas_tributarias_se_traducen_a_responsabilidades()
    {
        Catalogo.ResponsabilidadesDe(new MarcasTributarias(false, false, false, false), Hoy).Should().Equal("R-99-PN");
        Catalogo.ResponsabilidadesDe(new MarcasTributarias(true, true, true, true), Hoy).Should().Equal("O-13", "O-15", "O-23", "O-47");
        Catalogo.ResponsabilidadesDe(new MarcasTributarias(false, true, false, false), Hoy).Should().Equal("O-15");
    }

    [Fact]
    public void Tributos_esquema_tipo_de_persona_y_formas_de_pago()
    {
        Catalogo.EsTributoValido("01", Hoy).Should().BeTrue();
        Catalogo.EsTributoValido("06", Hoy).Should().BeTrue();
        Catalogo.EsTributoValido("99", Hoy).Should().BeFalse();
        Catalogo.EsquemaTributarioDe(true, Hoy).Should().Be("01");
        Catalogo.EsquemaTributarioDe(false, Hoy).Should().Be("ZZ");
        Catalogo.TipoDePersonaDe(true, Hoy).Should().Be("1");
        Catalogo.TipoDePersonaDe(false, Hoy).Should().Be("2");
        Catalogo.IdentificacionDeJuridica(Hoy).Should().Be("31");
        Catalogo.PaisPorDefecto(Hoy).Should().Be("CO");
        Catalogo.FormaDePago(credito: false, Hoy)!.Codigo.Should().Be("1");
        Catalogo.FormaDePago(credito: true, Hoy)!.Codigo.Should().Be("2");
    }

    [Fact]
    public void Con_la_tabla_de_tipos_de_caja_por_cotejar_acepta_el_codigo_de_la_caja()
    {
        Catalogo.TiposDeCaja(Hoy).Should().BeEmpty();
        Catalogo.EsTipoDeCajaValido("POS-1", Hoy).Should().BeTrue();
        Catalogo.EsTipoDeCajaValido(" ", Hoy).Should().BeFalse();
        Catalogo.Procedencia(CatalogoDian.Catalogos.TiposDeCaja, Hoy)!.Value.PorCotejar.Should().BeTrue();
    }

    [Fact]
    public void Antes_de_la_primera_vigencia_no_hay_catalogo()
    {
        var antes = new DateOnly(2018, 12, 31);
        Catalogo.TipoDeDocumento(ElectronicDocumentKind.Invoice, null, antes).Should().BeNull();
        Catalogo.ResponsabilidadesDe(new MarcasTributarias(false, false, false, false), antes).Should().BeEmpty();
        Catalogo.EsquemaTributarioDe(true, antes).Should().BeNull();
    }
}
