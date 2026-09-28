using FluentAssertions;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Tests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I4, T675 (contracts/dian.md §7.3, FR-067, SC-015): el plazo para transmitir lo emitido en
/// contingencia. Factura, DEE y sus notas cuentan desde el fin del evento; el documento soporte y su nota, desde las
/// 00:00 del día siguiente en hora local (−05:00). Las horas y la norma llegan por parámetro
/// (<c>Dian.PlazoContingenciaHoras</c> con su <c>LegalSource</c>); la alerta sale
/// <c>Dian.AlertaHorasAntesDelPlazo</c> horas antes. Las horas de estas pruebas son de ejemplo, no la norma.
/// </summary>
public class PlazoDeContingenciaTests
{
    private static readonly TimeSpan Local = TimeSpan.FromHours(-5);

    /// <summary>El evento terminó el 5 de diciembre a las 22:40, hora de Colombia.</summary>
    private static readonly DateTimeOffset Fin = new(2026, 12, 5, 22, 40, 0, Local);

    private const string Norma = "ET art. 616-1; Res. 165/2023 y 167/2021 compiladas en la Res. 000227/2025";

    [Theory]
    [InlineData(ElectronicDocumentKind.Invoice)]
    [InlineData(ElectronicDocumentKind.CreditNote)]
    [InlineData(ElectronicDocumentKind.DebitNote)]
    [InlineData(ElectronicDocumentKind.PosEquivalent)]
    [InlineData(ElectronicDocumentKind.PosAdjustmentNote)]
    public void Factura_DEE_y_sus_notas_cuentan_desde_el_fin_del_evento(ElectronicDocumentKind tipo)
    {
        var plazo = PlazoDeContingencia.Calcular(tipo, Fin, 48, Norma);

        plazo.DesdeCuando.Should().Be(Fin);
        plazo.Plazo.Should().Be(new DateTimeOffset(2026, 12, 7, 22, 40, 0, Local));
        plazo.HorasAplicadas.Should().Be(48);
        plazo.FuenteLegal.Should().Be(Norma);
    }

    [Theory]
    [InlineData(ElectronicDocumentKind.SupportDocument)]
    [InlineData(ElectronicDocumentKind.SupportDocumentAdjustmentNote)]
    public void El_documento_soporte_y_su_nota_cuentan_desde_las_cero_horas_del_dia_siguiente(ElectronicDocumentKind tipo)
    {
        var plazo = PlazoDeContingencia.Calcular(tipo, Fin, 48, Norma);

        plazo.DesdeCuando.Should().Be(new DateTimeOffset(2026, 12, 6, 0, 0, 0, Local));
        plazo.Plazo.Should().Be(new DateTimeOffset(2026, 12, 8, 0, 0, 0, Local));
    }

    [Fact]
    public void El_dia_siguiente_es_el_de_la_hora_local_aunque_el_fin_llegue_en_UTC()
    {
        // 22:40 en Colombia son las 03:40 UTC del día 6: el día siguiente local sigue siendo el 6.
        var enUtc = Fin.ToUniversalTime();

        var plazo = PlazoDeContingencia.Calcular(ElectronicDocumentKind.SupportDocument, enUtc, 24, Norma, Local);

        plazo.DesdeCuando.Should().Be(new DateTimeOffset(2026, 12, 6, 0, 0, 0, Local));
        plazo.Plazo.Should().Be(new DateTimeOffset(2026, 12, 7, 0, 0, 0, Local));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    public void Las_horas_salen_del_parametro_recibido(int horas)
    {
        var plazo = PlazoDeContingencia.Calcular(ElectronicDocumentKind.Invoice, Fin, horas, Norma);

        plazo.Plazo.Should().Be(Fin.AddHours(horas));
        plazo.HorasAplicadas.Should().Be(horas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sin_horas_positivas_no_hay_plazo(int horas)
    {
        FluentActions.Invoking(() => PlazoDeContingencia.Calcular(ElectronicDocumentKind.Invoice, Fin, horas, Norma))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void La_norma_es_obligatoria()
    {
        FluentActions.Invoking(() => PlazoDeContingencia.Calcular(ElectronicDocumentKind.Invoice, Fin, 48, " "))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(6)]
    [InlineData(2)]
    public void La_alerta_sale_las_horas_del_parametro_antes_del_plazo(int horasAntes)
    {
        var plazo = PlazoDeContingencia.Calcular(ElectronicDocumentKind.Invoice, Fin, 48, Norma);

        PlazoDeContingencia.InstanteDeLaAlerta(plazo.Plazo, horasAntes).Should().Be(plazo.Plazo.AddHours(-horasAntes));
    }

    [Fact]
    public void Una_alerta_que_caeria_antes_del_inicio_del_conteo_sale_al_iniciar()
    {
        var plazo = PlazoDeContingencia.Calcular(ElectronicDocumentKind.Invoice, Fin, 4, Norma);

        PlazoDeContingencia.InstanteDeLaAlerta(plazo, 6).Should().Be(plazo.DesdeCuando);
    }
}
