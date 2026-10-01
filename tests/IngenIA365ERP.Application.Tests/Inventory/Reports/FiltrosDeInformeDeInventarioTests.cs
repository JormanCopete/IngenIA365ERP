using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Reports;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// T182 (T44; contracts/api.md §27): los filtros comunes del centro de informes de inventario. El rango es de hasta
/// cinco años, como en la 009 (un rango de exactamente cinco años calendario, 01/01 a 31/12, cabe), y al revés es un
/// error; sin fechas, el rango por defecto es el mes en curso y el corte es hoy. Lo que la auditoría de la exportación
/// guarda es sólo lo que vino.
/// </summary>
public class FiltrosDeInformeDeInventarioTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    [Fact]
    public void Sin_fechas_el_rango_es_el_mes_en_curso_y_el_corte_es_hoy()
    {
        var f = new FiltrosDeInformeDeInventario();

        f.Desde(Hoy).Should().Be(new DateOnly(2026, 9, 1));
        f.Hasta(Hoy).Should().Be(Hoy);
        f.ALaFecha(Hoy).Should().Be(Hoy);
        f.ValidarRango(Hoy).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Un_rango_al_reves_es_un_error_con_su_codigo()
    {
        var r = new FiltrosDeInformeDeInventario { From = new DateOnly(2026, 9, 10), To = new DateOnly(2026, 9, 1) }.ValidarRango(Hoy);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(FiltrosDeInformeDeInventario.RangoInvalidoCodigo);
        FiltrosDeInformeDeInventario.RangoInvalidoCodigo.Should().Be("Inventory.Report.RangeInvalid");
    }

    [Theory]
    [InlineData("2021-01-01", "2025-12-31", true)]
    [InlineData("2021-01-01", "2026-01-01", false)]
    [InlineData("2020-06-15", "2026-06-15", false)]
    public void El_rango_es_de_hasta_cinco_anios(string desde, string hasta, bool valido)
    {
        var r = new FiltrosDeInformeDeInventario { From = DateOnly.Parse(desde), To = DateOnly.Parse(hasta) }.ValidarRango(Hoy);

        r.IsSuccess.Should().Be(valido);
        if (!valido)
        {
            r.Error.Code.Should().Be("Inventory.Report.RangeTooLong");
            r.Error.Message.Should().Contain("5 años");
        }
    }

    [Fact]
    public void Solo_con_desde_el_hasta_es_hoy_y_el_tope_se_mide_contra_hoy()
    {
        new FiltrosDeInformeDeInventario { From = new DateOnly(2019, 1, 1) }.ValidarRango(Hoy).IsFailure.Should().BeTrue();
        new FiltrosDeInformeDeInventario { From = new DateOnly(2026, 1, 1) }.ValidarRango(Hoy).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Para_la_auditoria_viaja_solo_lo_que_vino()
    {
        var bodega = Guid.NewGuid();
        var f = new FiltrosDeInformeDeInventario { From = new DateOnly(2026, 9, 1), Warehouse = bodega, Format = "xlsx" };

        var d = f.ParaAuditoria();

        d.Should().ContainKey("from").WhoseValue.Should().Be("2026-09-01");
        d.Should().ContainKey("warehouse").WhoseValue.Should().Be(bodega.ToString());
        d.Should().NotContainKey("to").And.NotContainKey("product").And.NotContainKey("format", "el formato va aparte en el evento");
    }
}
