using FluentAssertions;
using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Domain.Tests.Common.NationalValues;

/// <summary>
/// Feature 013, K5 (US16, T1002, T1007; FR-136): el dato opcional <c>sicsesReport</c> del documento <c>TiposDeOrganizacion</c> —por nivel de
/// supervisión, los meses que se reportan al SICSES, el plazo en días calendario desde el fin del mes y los días de aviso—, sin cambiar la versión de
/// esquema: sin él el documento se lee igual que antes y no hay plazo que vigilar. (nuevo)
/// </summary>
public class TiposDeOrganizacionDeK5Tests
{
    private static string Documento(string extra) => $$"""
        {
          "employeeFundCategories": [],
          "organizationTypes": [
            { "code": "COOP_AHORRO_CREDITO", "name": "Cooperativa", "isEmployeeFund": false, "expectedLoss": "Admitted"{{extra}} }
          ]
        }
        """;

    [Fact]
    public void Sin_el_dato_del_SICSES_no_hay_plazo()
    {
        var r = TiposDeOrganizacion.Leer(Documento(string.Empty), 1);

        r.Errores.Should().BeEmpty();
        var tipo = r.Documento!.Buscar("COOP_AHORRO_CREDITO")!;
        tipo.SicsesReport.Should().BeNull();
        tipo.SicsesDelNivel(1).Should().BeNull();
    }

    [Fact]
    public void Lee_el_reporte_por_nivel_con_su_plazo_y_su_aviso()
    {
        var r = TiposDeOrganizacion.Leer(Documento("""
            , "sicsesReport": [
                { "supervisionLevel": 1, "cutoffMonths": [1,2,3,4,5,6,7,8,9,10,11,12], "dueDays": 20, "warningDays": 5 },
                { "supervisionLevel": 2, "cutoffMonths": [3, 6, 9, 12], "dueDays": 30, "warningDays": 0 } ]
            """), 1);

        r.Errores.Should().BeEmpty();
        var tipo = r.Documento!.Buscar("COOP_AHORRO_CREDITO")!;
        var uno = tipo.SicsesDelNivel(1)!;
        uno.SeReporta(2).Should().BeTrue();
        uno.Limite(2027, 2).Should().Be(new DateOnly(2027, 3, 20), "el 28 de febrero más veinte días");
        uno.AvisoDesde(2027, 2).Should().Be(new DateOnly(2027, 3, 15));
        var dos = tipo.SicsesDelNivel(2)!;
        dos.SeReporta(2).Should().BeFalse("el nivel 2 se reporta por trimestre");
        dos.Limite(2027, 3).Should().Be(new DateOnly(2027, 4, 30));
        tipo.SicsesDelNivel(3).Should().BeNull("el documento no dice nada del nivel 3");
    }

    [Theory]
    [InlineData("""{ "supervisionLevel": 4, "cutoffMonths": [1], "dueDays": 20, "warningDays": 5 }""", "supervisionLevel")]
    [InlineData("""{ "supervisionLevel": 1, "cutoffMonths": [], "dueDays": 20, "warningDays": 5 }""", "cutoffMonths")]
    [InlineData("""{ "supervisionLevel": 1, "cutoffMonths": [13], "dueDays": 20, "warningDays": 5 }""", "cutoffMonths")]
    [InlineData("""{ "supervisionLevel": 1, "cutoffMonths": [1], "dueDays": 0, "warningDays": 5 }""", "dueDays")]
    [InlineData("""{ "supervisionLevel": 1, "cutoffMonths": [1], "dueDays": 20, "warningDays": -1 }""", "warningDays")]
    [InlineData("""{ "supervisionLevel": 1, "cutoffMonths": [1], "dueDays": 20 }""", "warningDays")]
    public void Un_nivel_invalido_es_error(string nivel, string campo)
    {
        var r = TiposDeOrganizacion.Leer(Documento($", \"sicsesReport\": [ {nivel} ]"), 1);

        r.Documento.Should().BeNull();
        r.Errores.Should().Contain(e => e.Ruta.Contains(campo));
    }

    [Fact]
    public void Los_plazos_especiales_de_diciembre_del_formato_9027()
    {
        // Decisión del dueño del 2026-10-16: nivel 1, diciembre al último día hábil de enero; nivel 2, diciembre en febrero (conservador: el 1).
        var r = TiposDeOrganizacion.Leer(Documento("""
            , "sicsesReport": [
                { "supervisionLevel": 1, "cutoffMonths": [1,2,3,4,5,6,7,8,9,10,11,12], "dueDays": 20, "warningDays": 5,
                  "dueExceptions": [ { "cutoffMonth": 12, "monthsAfterCutoff": 1, "lastBusinessDay": true } ] },
                { "supervisionLevel": 2, "cutoffMonths": [3, 6, 9, 12], "dueDays": 30, "warningDays": 5,
                  "dueExceptions": [ { "cutoffMonth": 12, "monthsAfterCutoff": 2, "day": 1 } ] },
                { "supervisionLevel": 3, "cutoffMonths": [12], "dueDays": 30, "warningDays": 5,
                  "dueExceptions": [ { "cutoffMonth": 12, "monthsAfterCutoff": 2, "day": 31 } ] } ]
            """), 1);

        r.Errores.Should().BeEmpty();
        var tipo = r.Documento!.Buscar("COOP_AHORRO_CREDITO")!;
        var uno = tipo.SicsesDelNivel(1)!;
        uno.Limite(2026, 11).Should().Be(new DateOnly(2026, 12, 20), "los demás meses siguen a 20 días");
        uno.Limite(2026, 12).Should().Be(new DateOnly(2027, 1, 29), "el 31 de enero de 2027 es domingo: el último hábil es el viernes 29");
        uno.Limite(2025, 12).Should().Be(new DateOnly(2026, 1, 30), "el 31 de enero de 2026 es sábado");
        uno.AvisoDesde(2026, 12).Should().Be(new DateOnly(2027, 1, 24));
        var dos = tipo.SicsesDelNivel(2)!;
        dos.Limite(2026, 9).Should().Be(new DateOnly(2026, 10, 30));
        dos.Limite(2026, 12).Should().Be(new DateOnly(2027, 2, 1));
        tipo.SicsesDelNivel(3)!.Limite(2026, 12).Should().Be(new DateOnly(2027, 2, 28), "el día 31 en febrero es su último día");
        tipo.SicsesDelNivel(3)!.SeReporta(6).Should().BeFalse("el nivel 3 es anual");
    }

    [Theory]
    [InlineData("""{ "cutoffMonth": 5, "monthsAfterCutoff": 1, "lastBusinessDay": true }""", "cutoffMonth")]
    [InlineData("""{ "cutoffMonth": 12, "monthsAfterCutoff": 0, "lastBusinessDay": true }""", "monthsAfterCutoff")]
    [InlineData("""{ "cutoffMonth": 12, "monthsAfterCutoff": 1, "day": 32 }""", "day")]
    [InlineData("""{ "cutoffMonth": 12, "monthsAfterCutoff": 1 }""", "dueExceptions[0]")]
    [InlineData("""{ "cutoffMonth": 12, "monthsAfterCutoff": 1, "day": 5, "lastBusinessDay": true }""", "dueExceptions[0]")]
    public void Un_plazo_especial_invalido_es_error(string especial, string campo)
    {
        var r = TiposDeOrganizacion.Leer(Documento($$"""
            , "sicsesReport": [ { "supervisionLevel": 2, "cutoffMonths": [3, 6, 9, 12], "dueDays": 30, "warningDays": 5, "dueExceptions": [ {{especial}} ] } ]
            """), 1);

        r.Documento.Should().BeNull();
        r.Errores.Should().Contain(e => e.Ruta.Contains(campo));
    }

    [Fact]
    public void Un_nivel_repetido_o_un_reporte_que_no_es_lista_es_error()
    {
        var repetido = TiposDeOrganizacion.Leer(Documento("""
            , "sicsesReport": [ { "supervisionLevel": 1, "cutoffMonths": [1], "dueDays": 20, "warningDays": 5 },
                                { "supervisionLevel": 1, "cutoffMonths": [2], "dueDays": 20, "warningDays": 5 } ]
            """), 1);
        repetido.Errores.Should().Contain(e => e.Ruta.EndsWith("[1].supervisionLevel") && e.Mensaje.Contains("repetido"));

        var objeto = TiposDeOrganizacion.Leer(Documento(""", "sicsesReport": { "supervisionLevel": 1 }"""), 1);
        objeto.Errores.Should().Contain(e => e.Ruta.EndsWith("sicsesReport"));
    }
}
