using System.Text;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.CreditBureaus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.Lending.Adapters;

/// <summary>
/// <c>CentralDeRiesgoSimulada</c> cumple la batería (feature 013, T0380) y la tabla de adaptadores §3.7 / DT §2.22 por el último dígito del documento:
/// 1 reporte negativo, 2 sin historia, 3 puntaje bajo, 5 <c>ChannelUnavailable</c>, cualquier otro sin marcas. Marca «SIMULADO» en su referencia
/// (<c>SIM-…</c>) y en su informe crudo, recuerda por cooperativa lo que respondió y nunca lanza; <c>AddExternalChannels</c> la registra. (nuevo)
/// </summary>
public sealed class CentralDeRiesgoSimuladaConformidadTests : ConformidadDeCentral
{
    private static ConsultaACentral Consulta(string documento) => new(Guid.NewGuid(), "CC", documento, "CreditApplication", Guid.NewGuid());

    private static CentralDeRiesgoSimulada Crear(ExternalChannelsOptions? opciones = null) =>
        new(Options.Create(opciones ?? new ExternalChannelsOptions()), new MemoriaDeCentralSimulada());

    protected override ICentralDeRiesgo Canal { get; } = Crear();

    protected override bool EsSimulado => true;

    protected override ConsultaACentral ConHistoria => Consulta("52000004");

    protected override ConsultaACentral SinHistoria => Consulta("52000002");

    protected override ConsultaACentral CanalCaido => Consulta("52000005");

    protected override ICentralDeRiesgo CanalLento
    {
        get
        {
            var opciones = new ExternalChannelsOptions();
            opciones.Channels[CodigosDeCanalExterno.Simulado] = new OpcionesDeCanalExterno { TotalTimeoutSeconds = 1, DelayMilliseconds = 1_200 };
            return Crear(opciones);
        }
    }

    [Theory]
    [InlineData("1020301", CentralDeRiesgoSimulada.PuntajeConReporteNegativo, false, 1)]
    [InlineData("1020302", null, true, 0)]
    [InlineData("1020303", CentralDeRiesgoSimulada.PuntajeBajo, false, 0)]
    [InlineData("1020304", CentralDeRiesgoSimulada.PuntajeNormal, false, 0)]
    [InlineData("1020309", CentralDeRiesgoSimulada.PuntajeNormal, false, 0)]
    public async Task El_ultimo_digito_del_documento_decide(string documento, int? puntaje, bool sinHistoria, int marcas)
    {
        var r = await Canal.ConsultarAsync(Consulta(documento), Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, r.Outcome);
        Assert.Equal(puntaje, r.Valor!.Puntaje);
        Assert.Equal(sinHistoria, r.Valor.SinHistoria);
        Assert.Equal(marcas, r.Valor.MarcasNegativas.Count);
    }

    [Fact]
    public async Task El_5_es_ChannelUnavailable()
    {
        var r = await Canal.ConsultarAsync(Consulta("1020305"), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
    }

    [Fact]
    public async Task Marca_SIMULADO_en_la_referencia_y_en_el_informe_crudo()
    {
        var r = await Canal.ConsultarAsync(Consulta("52000001"), Contexto(), default);

        Assert.StartsWith("SIM-", r.ReferenciaExterna, StringComparison.Ordinal);
        var artefacto = Assert.Single(r.Artefactos);
        Assert.Contains("SIMULADO", artefacto.NombreDeArchivo, StringComparison.Ordinal);
        Assert.Contains("SIMULADO", Encoding.UTF8.GetString(artefacto.Bytes), StringComparison.Ordinal);
        Assert.Equal("application/json", artefacto.ContentType);
    }

    [Fact]
    public async Task Recuerda_por_cooperativa_lo_que_respondio()
    {
        var otra = Contexto() with { TenantPublicId = Guid.NewGuid() };

        var aqui = await Canal.ConsultarAsync(Consulta("77000004"), Contexto(), default);
        var alla = await Canal.ConsultarAsync(Consulta("77000004"), otra, default);
        var aquiOtraVez = await Canal.ConsultarAsync(Consulta("77000004"), Contexto(), default);

        Assert.Equal(aqui.ReferenciaExterna, aquiOtraVez.ReferenciaExterna);
        Assert.NotEqual(aqui.ReferenciaExterna, alla.ReferenciaExterna);
    }

    [Fact]
    public void AddExternalChannels_registra_el_simulado_en_el_registro_de_centrales()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddExternalChannels(new ConfigurationBuilder().Build());
        using var proveedor = servicios.BuildServiceProvider();

        var canales = proveedor.GetServices<ICentralDeRiesgo>().ToList();
        var registro = new CentralesDeRiesgo(canales, new CircuitoSiempreCerrado());

        Assert.Contains(CodigosDeCanalExterno.Simulado, registro.Codigos);
        Assert.Contains(proveedor.GetServices<CanalExternoRegistrado>(),
            c => c.Puerto == PuertosExternos.CentralesDeRiesgo && c.ChannelCode == CodigosDeCanalExterno.Simulado);
    }
}
