using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Pila;
using IngenIA365ERP.Domain.Payroll.Pila;

namespace IngenIA365ERP.Domain.Tests.Payroll.Pila;

/// <summary>
/// El layout embebido AT2 v30 (contracts/archivos.md §1): registros contiguos sin huecos ni
/// solapes, 22 campos y 98 campos, 693 posiciones el registro tipo 2 (97/686 hasta 2019 más
/// el campo 98 de la Res. 2520/2024), campos consecutivos desde 1, y el estado de cotejo
/// visible: mientras haya campos con <c>verified = false</c>, el catálogo lo dice y generar
/// deja la alerta <c>Pila.LayoutSinCotejar</c> (D-43).
/// </summary>
public class PilaLayoutTests
{
    [Fact]
    public void El_layout_v30_es_contiguo_y_tiene_los_campos_del_anexo()
    {
        var layout = PilaLayoutCatalog.ByCode("AT2-v30");
        layout.Should().NotBeNull();
        layout!.Validate().Should().BeEmpty();
        layout.Type1.Fields.Should().HaveCount(22);
        layout.Type2.Fields.Should().HaveCount(98);
        layout.Type2.Length.Should().Be(693);
        // El registro tipo 1 suma 358 con los campos reconstruidos de la resolución; el anexo declara 359: un
        // campo con una posición de diferencia queda por cotejar (T094) y por eso ningún campo del tipo 1 está verificado.
        layout.Type1.Length.Should().Be(layout.Type1.Fields.Sum(f => f.Length));
        layout.Type1.Fields.Should().OnlyContain(f => !f.Verified);
        layout.IsVerified.Should().BeFalse("hasta cotejar con el anexo y una planilla pagada");
        layout.IsValidAt(new DateOnly(2026, 12, 1)).Should().BeTrue();
        layout.Type2.Fields.Single(f => f.Number == 40).Start.Should().Be(192);
        layout.Type2.Fields.Single(f => f.Number == 76).Start.Should().Be(506);
        layout.Type2.Fields.Single(f => f.Number == 98).Start.Should().Be(687);
    }

    [Fact]
    public void Un_layout_con_hueco_o_solape_se_rechaza()
    {
        var bueno = PilaLayoutCatalog.ByCode("AT2-v30")!;
        var campos = bueno.Type2.Fields.Select(f => f.Number == 5 ? f with { Start = f.Start + 1 } : f).ToList();
        var malo = bueno with { Records = [bueno.Type1, new PilaRecordLayout(2, 693, campos)] };
        malo.Validate().Should().Contain(e => e.Contains("campo 5"));
    }

    [Fact]
    public void El_escritor_alinea_numericos_a_la_derecha_con_ceros_y_texto_a_la_izquierda_sin_tildes()
    {
        var n = new PilaFieldLayout(1, "n", 1, 9, "N", true, "Calculation", null, null, "Integer", [], false);
        var a = new PilaFieldLayout(2, "a", 10, 10, "A", true, "Profile", null, null, "Text", [], false);
        var tarifa = new PilaFieldLayout(3, "t", 20, 7, "N", true, "Calculation", null, null, "Rate7", [], false);
        var fecha = new PilaFieldLayout(4, "f", 27, 10, "A", false, "Calculation", null, null, "Date", [], false);
        PilaWriter.Formatear(n, 1333334m).Should().Be("001333334");
        PilaWriter.Formatear(a, "Peña Núñez").Should().Be("PENA NUNEZ");
        PilaWriter.Formatear(a, "Ángela María Del Carmen").Should().Be("ANGELA MAR");
        PilaWriter.Formatear(tarifa, 0.04m).Should().Be("0.04000");
        PilaWriter.Formatear(fecha, new DateTime(2026, 12, 10)).Should().Be("2026-12-10");
        PilaWriter.Formatear(fecha, null).Should().Be(new string(' ', 10));
        PilaWriter.Formatear(n, null).Should().Be("000000000");
    }
}
