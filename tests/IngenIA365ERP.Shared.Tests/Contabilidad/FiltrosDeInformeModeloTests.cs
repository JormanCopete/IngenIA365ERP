using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>
/// Feature 009 E2 (US5): los filtros de pantalla viajan a la API con los nombres del contrato
/// (api.md §8) y vuelven de la query string cuando una pantalla llega a otra («ver el libro
/// auxiliar de esta cuenta con este tercero»). Si un nombre se desalinea, el filtro se ignora
/// en silencio y el informe sale sin acotar: por eso se fija aquí.
/// </summary>
public class FiltrosDeInformeModeloTests
{
    [Fact]
    public void ToQuery_usa_los_nombres_de_la_api_y_fechas_iso()
    {
        var cuenta = Guid.NewGuid();
        var tercero = Guid.NewGuid();
        var m = new FiltrosDeInformeModelo
        {
            From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 3, 31),
            AccountPublicId = cuenta, Person = tercero, CrossDocumentType = "fc", CrossDocumentNumber = "1001",
            VoucherType = "CG", Origin = "NOM", User = "ana", Level = 4, WithThirdParties = true, IncludeClosing = true,
        };

        var q = m.ToQuery();

        q.Should().Contain("from=2026-01-01").And.Contain("to=2026-03-31")
            .And.Contain($"accountPublicId={cuenta}").And.Contain($"person={tercero}")
            .And.Contain("crossDocument=FC%7C1001")
            .And.Contain("voucherType=CG").And.Contain("origin=NOM").And.Contain("user=ana").And.Contain("level=4")
            .And.Contain("withThirdParties=true").And.Contain("includeClosing=true");
        q.Should().NotStartWith("?").And.NotStartWith("&");
    }

    [Fact]
    public void ToQuery_omite_lo_que_no_esta_puesto()
    {
        new FiltrosDeInformeModelo().ToQuery().Should().BeEmpty();
        new FiltrosDeInformeModelo { AccountFrom = "  " }.ToQuery().Should().BeEmpty("un texto en blanco no es un filtro");
        new FiltrosDeInformeModelo { CrossDocumentNumber = "1001" }.ToQuery().Should().BeEmpty("el número sin tipo no identifica un documento");
    }

    [Fact]
    public void Ida_y_vuelta_por_la_query_string_conserva_todos_los_filtros()
    {
        var original = new FiltrosDeInformeModelo
        {
            From = new DateOnly(2026, 2, 1), To = new DateOnly(2026, 2, 28), AccountFrom = "1105", AccountTo = "1110",
            AccountPublicId = Guid.NewGuid(), Person = Guid.NewGuid(), CrossDocumentType = "RC", CrossDocumentNumber = "77",
            CostCenter = Guid.NewGuid(), Branch = Guid.NewGuid(), VoucherType = "NM", Origin = "CNT", User = "jorman",
            Level = 5, WithThirdParties = true, IncludeClosing = true,
        };

        var vuelta = FiltrosDeInformeModelo.DesdeQuery("?" + original.ToQuery() + "&vista=trial-balance&node=account%3A1105");

        vuelta.ToQuery().Should().Be(original.ToQuery());
        vuelta.CrossDocumentType.Should().Be("RC");
        vuelta.CrossDocumentNumber.Should().Be("77");
        vuelta.Cantidad.Should().Be(original.Cantidad);
    }

    [Fact]
    public void DesdeQuery_acepta_una_url_completa_y_valores_invalidos_sin_romperse()
    {
        var m = FiltrosDeInformeModelo.DesdeQuery("https://app/contabilidad/libro-auxiliar?from=ayer&person=no-es-guid&level=x&includeClosing=1&to=2026-06-30");

        m.From.Should().BeNull();
        m.Person.Should().BeNull();
        m.Level.Should().BeNull();
        m.IncludeClosing.Should().BeTrue();
        m.To.Should().Be(new DateOnly(2026, 6, 30));
        FiltrosDeInformeModelo.DesdeQuery(null).ToQuery().Should().BeEmpty();
    }

    [Fact]
    public void Cantidad_cuenta_los_filtros_que_acotan_pero_no_las_fechas()
    {
        var m = new FiltrosDeInformeModelo { From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 12, 31) };
        m.Cantidad.Should().Be(0);
        m.Person = Guid.NewGuid();
        m.Branch = Guid.NewGuid();
        m.IncludeClosing = true;
        m.Cantidad.Should().Be(3);
    }

    [Fact]
    public void Copia_es_independiente_del_original()
    {
        var original = new FiltrosDeInformeModelo { Person = Guid.NewGuid(), Level = 2 };
        var copia = original.Copia();
        copia.Person = null;
        copia.Level = 6;

        original.Level.Should().Be(2);
        original.Person.Should().NotBeNull();
    }

    [Theory]
    [InlineData("", "node=x", "node=x")]
    [InlineData("from=2026-01-01", "", "from=2026-01-01")]
    [InlineData("from=2026-01-01", "node=x", "from=2026-01-01&node=x")]
    public void Unir_pone_el_ampersand_solo_cuando_hace_falta(string a, string b, string esperado) =>
        FiltrosDeInformeModelo.Unir(a, b).Should().Be(esperado);
}
