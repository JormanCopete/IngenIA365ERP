using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Application.Tests.Lending.Adapters;

/// <summary>
/// La batería reutilizable de conformidad de las centrales de riesgo (feature 013, T0380; T71; contracts/adaptadores-y-archivos.md §3.1, §3.7, §3.9;
/// DT §7.6), <c>ConformidadDeCentral</c> (fijada allí): se corre contra el simulado y contra cada adaptador real en su ambiente de pruebas. Cada canal
/// provee una consulta con historia, una sin historia y una que no llega (canal caído). Las siete reglas de §3.1 que aplican a una consulta —una
/// consulta no escribe— y que el resultado traiga su <c>ChannelCode</c> y <c>Simulado</c> sólo en el simulado. (nuevo)
/// </summary>
public abstract class ConformidadDeCentral
{
    protected static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c013");

    protected abstract ICentralDeRiesgo Canal { get; }

    protected abstract bool EsSimulado { get; }

    protected abstract ConsultaACentral ConHistoria { get; }

    protected abstract ConsultaACentral SinHistoria { get; }

    protected abstract ConsultaACentral CanalCaido { get; }

    /// <summary>El mismo canal con un retardo mayor que su tiempo total (regla 2: el transporte no sube como excepción).</summary>
    protected abstract ICentralDeRiesgo CanalLento { get; }

    protected ContextoDeCanalExterno Contexto(string? clave = null) =>
        new(Cooperativa, Canal.ChannelCode, "DATACREDITO", null, clave ?? Guid.NewGuid().ToString("N"), true);

    [Fact]
    public async Task Con_historia_responde_Ok_con_el_informe_y_su_artefacto()
    {
        var r = await Canal.ConsultarAsync(ConHistoria, Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, r.Outcome);
        Assert.NotNull(r.Valor);
        Assert.False(r.Valor!.SinHistoria);
        Assert.NotNull(r.Valor.Puntaje);
        if (Canal.Capacidades.ArtefactoCrudo) Assert.NotEmpty(r.Artefactos);
        Assert.False(string.IsNullOrWhiteSpace(r.ReferenciaExterna));
    }

    [Fact]
    public async Task Sin_historia_responde_Ok_sin_historia()
    {
        var r = await Canal.ConsultarAsync(SinHistoria, Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, r.Outcome);
        Assert.True(r.Valor!.SinHistoria);
    }

    [Fact]
    public async Task Regla_1_una_consulta_sin_documento_es_InvalidData_sin_lanzar()
    {
        var r = await Canal.ConsultarAsync(ConHistoria with { NumeroDeDocumento = "" }, Contexto(), default);

        Assert.Equal(ExternalOutcome.InvalidData, r.Outcome);
        Assert.Null(r.Valor);
    }

    [Fact]
    public async Task Regla_2_el_canal_caido_o_lento_es_ChannelUnavailable_sin_lanzar()
    {
        var caido = await Canal.ConsultarAsync(CanalCaido, Contexto(), default);
        var lento = await CanalLento.ConsultarAsync(ConHistoria, Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, caido.Outcome);
        Assert.Equal(ExternalOutcome.ChannelUnavailable, lento.Outcome);
    }

    [Fact]
    public async Task Regla_3_consultar_otra_vez_es_coherente_con_lo_que_respondio()
    {
        var primera = await Canal.ConsultarAsync(ConHistoria, Contexto(), default);
        var segunda = await Canal.ConsultarAsync(ConHistoria, Contexto(), default);

        Assert.Equal(primera.Valor!.Puntaje, segunda.Valor!.Puntaje);
        Assert.Equal(primera.Valor.MarcasNegativas, segunda.Valor.MarcasNegativas);
        Assert.Equal(primera.ReferenciaExterna, segunda.ReferenciaExterna);
    }

    [Fact]
    public async Task Regla_6_dos_adaptadores_con_el_mismo_codigo_no_arrancan()
    {
        Assert.Throws<InvalidOperationException>(() => new CentralesDeRiesgo([Canal, Canal], new CircuitoSiempreCerrado()));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Regla_7_un_codigo_sellado_sin_adaptador_responde_ChannelUnavailable()
    {
        var registro = new CentralesDeRiesgo([Canal], new CircuitoSiempreCerrado());

        var r = await registro.Resolver("CENTRAL-QUE-NO-ESTA").ConsultarAsync(ConHistoria, Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        Assert.Contains("no está instalado", r.Detalle, StringComparison.Ordinal);
        Assert.Equal(ExternalOutcome.Ok, (await registro.Resolver(Canal.ChannelCode.ToLowerInvariant()).ConsultarAsync(ConHistoria, Contexto(), default)).Outcome);
    }

    [Fact]
    public async Task Todo_resultado_trae_el_ChannelCode_y_Simulado_solo_en_el_simulado()
    {
        foreach (var consulta in new[] { ConHistoria, SinHistoria, CanalCaido, ConHistoria with { NumeroDeDocumento = "" } })
        {
            var r = await Canal.ConsultarAsync(consulta, Contexto(), default);
            Assert.Equal(Canal.ChannelCode, r.ChannelCode);
            Assert.Equal(EsSimulado, r.Simulado);
        }
        var prueba = await Canal.ProbarAsync(Contexto(), default);
        Assert.Equal(ExternalOutcome.Ok, prueba.Outcome);
        Assert.Equal(EsSimulado, prueba.Simulado);
    }
}
