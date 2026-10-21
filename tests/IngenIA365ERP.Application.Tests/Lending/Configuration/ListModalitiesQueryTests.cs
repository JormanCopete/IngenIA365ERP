using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0335 (US1; api.md §3.2; T44): <c>GET /modalities</c> sale del documento <c>CatalogoDeModalidades</c> vigente por
/// <c>INormativaVigente</c>, con su identidad y su huella, y por modalidad los sub-segmentos, el segmento SARC, el de pérdida esperada, la
/// clave de <c>USURA</c> y si admite UVR. Sin documento vigente, <c>NationalValues.Missing</c>.
/// </summary>
public sealed class ListModalitiesQueryTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public void Dispose() => _l.Dispose();

    private Task<IngenIA365ERP.Application.Common.Models.Result<ModalitiesCatalogDto>> Listar(DateOnly? fecha = null) =>
        new ListModalitiesQueryHandler(_l.Normativa(), _l.G.Reloj).Handle(new ListModalitiesQuery(fecha), default);

    [Fact]
    public async Task Sale_del_catalogo_vigente_con_segmentos_usura_y_UVR()
    {
        await _l.NormativaAsync();

        var r = await Listar();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Code.Should().Be("CATMOD");
        r.Value.Sha256.Should().HaveLength(64);
        r.Value.ValidFrom.Should().Be(new DateOnly(2026, 1, 1));
        var consumo = r.Value.Modalities.Single(m => m.Code == "CONSUMO");
        consumo.Subsegments.Should().Equal("ConLibranza", "SinLibranza");
        consumo.SarcSegment.Should().Be("CONSUMO");
        consumo.UsuryKey.Should().Be(LineasDePrueba.ClaveConsumo);
        consumo.Combinations.Should().Contain(c => c.SubsegmentCode == "ConLibranza" && c.ExpectedLossSegment == "CONSUMO_LIBRANZA");
        var vivienda = r.Value.Modalities.Single(m => m.Code == "VIVIENDA");
        vivienda.AdmitsUvr.Should().BeTrue();
        vivienda.UsuryKey.Should().BeNull("vivienda lleva el tope de vivienda, no una usura certificada");
    }

    [Fact]
    public async Task Sin_documento_vigente_es_NationalValues_Missing()
    {
        await _l.NormativaAsync();

        var r = await Listar(new DateOnly(2025, 6, 1));

        r.Error.Code.Should().Be("NationalValues.Missing");
    }
}
