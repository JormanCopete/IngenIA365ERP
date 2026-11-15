using FluentAssertions;
using IngenIA365ERP.Domain.Transport;

namespace IngenIA365ERP.Domain.Tests.Transport;

/// <summary>Feature 020 (T080; R7; FR-022): vencido, por vencer según los días de aviso del tipo, vigente, y faltante si es obligatorio.</summary>
public class VencimientosDeDocumentosTests
{
    private static readonly DateOnly Corte = new(2026, 12, 1);
    private static readonly TipoParaVencimiento Soat = new(1, "SOAT", "SOAT", Obligatorio: true, DiasDeAviso: 30);
    private static readonly TipoParaVencimiento Rtm = new(2, "RTM", "Revisión técnico-mecánica", Obligatorio: true, DiasDeAviso: 15);
    private static readonly TipoParaVencimiento TodoRiesgo = new(3, "TODORIESGO", "Todo riesgo", Obligatorio: false, DiasDeAviso: 30);

    [Theory]
    [InlineData(-1, 30, SituacionDeDocumento.Vencido)]
    [InlineData(0, 30, SituacionDeDocumento.PorVencer)]
    [InlineData(30, 30, SituacionDeDocumento.PorVencer)]
    [InlineData(31, 30, SituacionDeDocumento.Vigente)]
    [InlineData(0, 0, SituacionDeDocumento.PorVencer)]
    [InlineData(1, 0, SituacionDeDocumento.Vigente)]
    public void La_situacion_sale_de_los_dias_para_vencer_y_los_de_aviso(int dias, int aviso, SituacionDeDocumento esperada) =>
        VencimientosDeDocumentos.Situacion(dias, aviso).Should().Be(esperada);

    [Fact]
    public void Un_obligatorio_sin_documento_falta_y_un_opcional_sin_documento_no_aparece()
    {
        var r = VencimientosDeDocumentos.Clasificar(Corte, [Soat, Rtm, TodoRiesgo], [new DocumentoParaVencimiento(1, "S-1", Corte.AddDays(10))]);

        r.Should().HaveCount(2);
        r.Single(x => x.Tipo.Codigo == "SOAT").Situacion.Should().Be(SituacionDeDocumento.PorVencer);
        r.Single(x => x.Tipo.Codigo == "RTM").Situacion.Should().Be(SituacionDeDocumento.Falta);
    }

    [Fact]
    public void Vencer_hoy_todavia_no_es_vencido()
    {
        var r = VencimientosDeDocumentos.Clasificar(Corte, [Soat], [new DocumentoParaVencimiento(1, "S-1", Corte)]);

        r.Single().Situacion.Should().Be(SituacionDeDocumento.PorVencer);
        r.Single().DiasParaVencer.Should().Be(0);
    }
}
