using FluentAssertions;
using IngenIA365ERP.Domain.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Tests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I4, T674 (contracts/dian.md §8.2, FR-066): quien opera no elige entre el caso a y el b; lo decide la
/// huella económica. Todo campo económico (identificación de la contraparte; por línea producto, cantidad, base,
/// descuento, impuestos y retenciones; cada total) lleva al caso b y queda nombrado en <c>fields[]</c>; los datos no
/// económicos de la contraparte (nombre, dirección, correo, teléfono, ciudad y responsabilidades) dejan el caso a.
/// </summary>
public class ReglaDeCorreccionFiscalTests
{
    private static DatosFiscalesDelDocumento Base() => new(
        new ContraparteFiscal("13", "16000111", null, "ANA PÉREZ", "Cra 1 # 2-3", "76001", "ana@correo.co", "3001234567", ["R-99-PN"]),
        [
            new LineaFiscal(1, "ARZ-001", 2.0000m, 4200.00m, 210.00m,
                [new ImporteFiscal("01", 0.190000m, 3990.00m, 758.10m)],
                [new ImporteFiscal("06", 0.025000m, 3990.00m, 99.75m)]),
            new LineaFiscal(2, "ACE-002", 1.0000m, 9000.00m, 0.00m, [], []),
        ],
        [new ImporteFiscal("06", 0.025000m, 3990.00m, 99.75m)],
        new TotalesFiscales(13200.00m, 210.00m, 12990.00m, 758.10m, 13748.10m, 0.00m, 0.00m, 13748.10m, 99.75m, 13648.35m));

    private static ReglaDeCorreccionFiscal.CasoB DebeSerCasoB(DatosFiscalesDelDocumento nuevo) =>
        ReglaDeCorreccionFiscal.Decidir(Base(), nuevo).Should().BeOfType<ReglaDeCorreccionFiscal.CasoB>().Subject;

    private static ReglaDeCorreccionFiscal.CasoA DebeSerCasoA(DatosFiscalesDelDocumento nuevo) =>
        ReglaDeCorreccionFiscal.Decidir(Base(), nuevo).Should().BeOfType<ReglaDeCorreccionFiscal.CasoA>().Subject;

    private static DatosFiscalesDelDocumento ConLinea1(Func<LineaFiscal, LineaFiscal> cambio)
    {
        var b = Base();
        return b with { Lineas = [cambio(b.Lineas[0]), b.Lineas[1]] };
    }

    [Fact]
    public void El_mismo_documento_es_caso_a_sin_cambios_y_con_la_misma_huella()
    {
        DebeSerCasoA(Base()).CamposCambiados.Should().BeEmpty();
        HuellaEconomica.Calcular(Base()).Should().Be(HuellaEconomica.Calcular(Base()));
    }

    [Fact]
    public void La_huella_es_un_SHA256_hexadecimal_de_64_caracteres()
    {
        HuellaEconomica.Calcular(Base()).Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void La_escala_de_los_decimales_no_cambia_la_huella()
    {
        var mismaCantidadOtraEscala = ConLinea1(l => l with { Cantidad = 2m, Base = 4200m });

        HuellaEconomica.Calcular(mismaCantidadOtraEscala).Should().Be(HuellaEconomica.Calcular(Base()));
        DebeSerCasoA(mismaCantidadOtraEscala);
    }

    public static TheoryData<string, Func<DatosFiscalesDelDocumento, DatosFiscalesDelDocumento>> CamposEconomicos() => new()
    {
        { "counterparty.idTypeCode", d => d with { Contraparte = d.Contraparte with { TipoDeIdentificacion = "31" } } },
        { "counterparty.taxId", d => d with { Contraparte = d.Contraparte with { Identificacion = "16000112" } } },
        { "counterparty.checkDigit", d => d with { Contraparte = d.Contraparte with { DigitoDeVerificacion = "7" } } },
        { "lines[1].productCode", d => d with { Lineas = [d.Lineas[0] with { Producto = "ARZ-002" }, d.Lineas[1]] } },
        { "lines[1].quantity", d => d with { Lineas = [d.Lineas[0] with { Cantidad = 3m }, d.Lineas[1]] } },
        { "lines[1].base", d => d with { Lineas = [d.Lineas[0] with { Base = 4300m }, d.Lineas[1]] } },
        { "lines[1].discount", d => d with { Lineas = [d.Lineas[0] with { Descuento = 0m }, d.Lineas[1]] } },
        { "lines[1].taxes", d => d with { Lineas = [d.Lineas[0] with { Impuestos = [new ImporteFiscal("01", 0.190000m, 3990.00m, 758.11m)] }, d.Lineas[1]] } },
        { "lines[1].withholdings", d => d with { Lineas = [d.Lineas[0] with { Retenciones = [] }, d.Lineas[1]] } },
        { "withholdings", d => d with { Retenciones = [new ImporteFiscal("06", 0.035000m, 3990.00m, 139.65m)] } },
        { "totals.lineExtension", d => d with { Totales = d.Totales with { LineExtension = 1m } } },
        { "totals.allowances", d => d with { Totales = d.Totales with { Allowances = 1m } } },
        { "totals.taxExclusive", d => d with { Totales = d.Totales with { TaxExclusive = 1m } } },
        { "totals.taxes", d => d with { Totales = d.Totales with { Taxes = 1m } } },
        { "totals.taxInclusive", d => d with { Totales = d.Totales with { TaxInclusive = 1m } } },
        { "totals.charges", d => d with { Totales = d.Totales with { Charges = 1m } } },
        { "totals.rounding", d => d with { Totales = d.Totales with { Rounding = 1m } } },
        { "totals.payable", d => d with { Totales = d.Totales with { Payable = 1m } } },
        { "totals.withholdings", d => d with { Totales = d.Totales with { Withholdings = 1m } } },
        { "totals.amountDue", d => d with { Totales = d.Totales with { AmountDue = 1m } } },
    };

    [Theory]
    [MemberData(nameof(CamposEconomicos))]
    public void Cada_campo_economico_decide_el_caso_b_y_se_nombra(string campo, Func<DatosFiscalesDelDocumento, DatosFiscalesDelDocumento> cambio)
    {
        var nuevo = cambio(Base());

        DebeSerCasoB(nuevo).Fields.Should().Equal(campo);
        HuellaEconomica.Calcular(nuevo).Should().NotBe(HuellaEconomica.Calcular(Base()));
    }

    public static TheoryData<string, Func<DatosFiscalesDelDocumento, DatosFiscalesDelDocumento>> CamposNoEconomicos() => new()
    {
        { "counterparty.name", d => d with { Contraparte = d.Contraparte with { Nombre = "ANA MARÍA PÉREZ" } } },
        { "counterparty.address", d => d with { Contraparte = d.Contraparte with { Direccion = "Cll 5 # 6-7" } } },
        { "counterparty.email", d => d with { Contraparte = d.Contraparte with { Correo = "otra@correo.co" } } },
        { "counterparty.phone", d => d with { Contraparte = d.Contraparte with { Telefono = "6025551234" } } },
        { "counterparty.cityDaneCode", d => d with { Contraparte = d.Contraparte with { CiudadDane = "05001" } } },
        { "counterparty.responsibilities", d => d with { Contraparte = d.Contraparte with { Responsabilidades = ["O-13", "O-15"] } } },
    };

    [Theory]
    [MemberData(nameof(CamposNoEconomicos))]
    public void Los_datos_no_economicos_de_la_contraparte_dejan_el_caso_a(string campo, Func<DatosFiscalesDelDocumento, DatosFiscalesDelDocumento> cambio)
    {
        var nuevo = cambio(Base());

        DebeSerCasoA(nuevo).CamposCambiados.Should().Equal(campo);
        HuellaEconomica.Calcular(nuevo).Should().Be(HuellaEconomica.Calcular(Base()));
    }

    [Fact]
    public void Varios_cambios_economicos_se_nombran_todos_y_mandan_sobre_los_no_economicos()
    {
        var b = Base();
        var nuevo = b with
        {
            Contraparte = b.Contraparte with { Identificacion = "900123456", Nombre = "OTRA" },
            Totales = b.Totales with { Payable = 1m },
        };

        DebeSerCasoB(nuevo).Fields.Should().BeEquivalentTo(["counterparty.taxId", "totals.payable"]);
    }

    [Fact]
    public void Una_linea_de_mas_o_de_menos_es_economica()
    {
        var b = Base();

        DebeSerCasoB(b with { Lineas = [b.Lineas[0]] }).Fields.Should().Contain("lines[2].productCode");
    }

    [Fact]
    public void El_orden_de_las_responsabilidades_no_es_un_cambio()
    {
        var b = Base() with { Contraparte = Base().Contraparte with { Responsabilidades = ["O-15", "O-13"] } };
        var c = Base() with { Contraparte = Base().Contraparte with { Responsabilidades = ["O-13", "O-15"] } };

        ReglaDeCorreccionFiscal.Decidir(b, c).Should().BeOfType<ReglaDeCorreccionFiscal.CasoA>()
            .Which.CamposCambiados.Should().BeEmpty();
    }
}
