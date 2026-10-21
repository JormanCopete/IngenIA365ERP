using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Messaging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.Common.ExternalChannels;

/// <summary>Los escenarios que todo adaptador de mensajería sabe provocar para su batería de conformidad. (nuevo)</summary>
public enum CasoDeMensajeria
{
    /// <summary>El proveedor lo acepta y lo entrega.</summary>
    Entregado = 1,

    /// <summary>El proveedor lo acepta y la entrega falla después.</summary>
    EntregaFallida = 2,

    /// <summary>El destino es inválido: el proveedor lo rechaza antes de procesar.</summary>
    DatoInvalido = 3,

    /// <summary>La petición no salió.</summary>
    SinSalir = 4,

    /// <summary>La petición pudo haber llegado (tiempo agotado después de enviar).</summary>
    Ambiguo = 5,
}

/// <summary>
/// La batería reutilizable de conformidad de la mensajería (feature 013, T0031; T71; contracts/adaptadores-y-archivos.md §3.1, §3.9; DT
/// §7.6): las reglas de §3.1 que cumple todo <see cref="ICanalDeMensajeria"/>. La hereda <see cref="MensajeriaSimuladaConformidadTests"/> y la
/// heredará el adaptador de cada proveedor de SMS y WhatsApp con su escenario. Una clase derivada sólo dice cómo provocar cada
/// <see cref="CasoDeMensajeria"/> y si su canal es simulado. (nuevo)
/// </summary>
public abstract class ConformidadDeMensajeria
{
    protected static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");

    /// <summary>El adaptador bajo prueba.</summary>
    protected abstract ICanalDeMensajeria Canal { get; }

    /// <summary>¿El adaptador es el simulado? Sólo él dice <c>Simulado = true</c>.</summary>
    protected abstract bool EsSimulado { get; }

    /// <summary>Un mensaje que provoca <paramref name="caso"/>.</summary>
    protected abstract MensajeSaliente Mensaje(CasoDeMensajeria caso);

    /// <summary>El canal configurado para provocar <see cref="CasoDeMensajeria.Ambiguo"/>.</summary>
    protected abstract ICanalDeMensajeria CanalQueNoContestaATiempo { get; }

    protected ContextoDeCanalExterno Contexto(Guid? mensaje = null) =>
        new(Cooperativa, Canal.ChannelCode, null, null, (mensaje ?? Guid.NewGuid()).ToString("N"), AmbienteDePruebas: true);

    [Fact]
    public async Task Regla_1_un_rechazo_no_lanza_y_el_dato_invalido_es_InvalidData()
    {
        var r = await Canal.EnviarAsync(Mensaje(CasoDeMensajeria.DatoInvalido), Contexto(), default);

        Assert.Equal(ExternalOutcome.InvalidData, r.Outcome);
        Assert.Null(r.Valor);
    }

    [Fact]
    public async Task Regla_2_lo_que_no_salio_es_ChannelUnavailable_sin_lanzar()
    {
        var r = await Canal.EnviarAsync(Mensaje(CasoDeMensajeria.SinSalir), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
    }

    [Fact]
    public async Task Regla_2_lo_que_pudo_haber_llegado_es_InProcess_y_se_resuelve_consultando()
    {
        var mensaje = Mensaje(CasoDeMensajeria.Ambiguo);
        var canal = CanalQueNoContestaATiempo;

        var r = await canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);

        Assert.Equal(ExternalOutcome.InProcess, r.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(r.ReferenciaExterna));
        var consulta = await Canal.ConsultarEntregaAsync(r.ReferenciaExterna!, Contexto(), default);
        Assert.Equal(ExternalOutcome.Ok, consulta.Outcome);
    }

    [Fact]
    public async Task Regla_3_enviar_dos_veces_el_mismo_mensaje_no_es_error_y_devuelve_lo_mismo()
    {
        var mensaje = Mensaje(CasoDeMensajeria.Entregado);

        var primero = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);
        var segundo = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);

        Assert.Equal(ExternalOutcome.Ok, primero.Outcome);
        Assert.Equal(ExternalOutcome.Ok, segundo.Outcome);
        Assert.Equal(primero.ReferenciaExterna, segundo.ReferenciaExterna);
        Assert.Equal(primero.Valor!.AceptadoEn, segundo.Valor!.AceptadoEn);
    }

    [Fact]
    public async Task Un_mensaje_aceptado_se_entrega_en_la_consulta_siguiente()
    {
        var mensaje = Mensaje(CasoDeMensajeria.Entregado);
        var envio = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);

        var entrega = await Canal.ConsultarEntregaAsync(envio.ReferenciaExterna!, Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, entrega.Outcome);
        Assert.Equal(DeliveryState.Delivered, entrega.Valor!.Estado);
    }

    [Fact]
    public async Task Una_entrega_fallida_se_informa_en_la_consulta_siguiente()
    {
        var mensaje = Mensaje(CasoDeMensajeria.EntregaFallida);
        var envio = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);
        Assert.Equal(ExternalOutcome.Ok, envio.Outcome);

        var entrega = await Canal.ConsultarEntregaAsync(envio.ReferenciaExterna!, Contexto(), default);

        Assert.Equal(DeliveryState.Failed, entrega.Valor!.Estado);
    }

    [Fact]
    public async Task Una_referencia_desconocida_es_NotFound_sin_lanzar()
    {
        var r = await Canal.ConsultarEntregaAsync("REF-QUE-NO-EXISTE", Contexto(), default);

        Assert.Equal(ExternalOutcome.NotFound, r.Outcome);
    }

    [Fact]
    public async Task Regla_7_un_codigo_sellado_sin_adaptador_responde_ChannelUnavailable()
    {
        var registro = new CanalesDeMensajeria([Canal], new CircuitoSiempreCerrado());

        var canal = registro.Resolver("PROVEEDOR-QUE-NO-ESTA");
        var r = await canal.EnviarAsync(Mensaje(CasoDeMensajeria.Entregado), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        Assert.Contains("no está instalado", r.Detalle, StringComparison.Ordinal);
        Assert.Equal(ExternalOutcome.ChannelUnavailable, (await canal.ConsultarEntregaAsync("x", Contexto(), default)).Outcome);
    }

    [Fact]
    public void Regla_6_dos_adaptadores_con_el_mismo_codigo_no_arrancan()
    {
        Assert.Throws<InvalidOperationException>(() => new CanalesDeMensajeria([Canal, Canal], new CircuitoSiempreCerrado()));
    }

    [Fact]
    public async Task Todo_resultado_trae_el_ChannelCode_y_Simulado_solo_en_el_simulado()
    {
        foreach (var caso in Enum.GetValues<CasoDeMensajeria>().Where(c => c != CasoDeMensajeria.Ambiguo))
        {
            var r = await Canal.EnviarAsync(Mensaje(caso), Contexto(), default);
            Assert.Equal(Canal.ChannelCode, r.ChannelCode);
            Assert.Equal(EsSimulado, r.Simulado);
        }
        var prueba = await Canal.ProbarAsync(Contexto(), default);
        Assert.Equal(Canal.ChannelCode, prueba.ChannelCode);
        Assert.Equal(EsSimulado, prueba.Simulado);
    }
}

/// <summary>
/// <c>MensajeriaSimulada</c> cumple la batería de conformidad (feature 013, T0031) y la tabla del último dígito del documento de DT §2.22:
/// 1 <c>InvalidData</c>, 2 <c>Ok</c> y <c>Delivered</c> en la consulta siguiente, 3 <c>Ok</c> y <c>Failed</c>, 5 <c>ChannelUnavailable</c>,
/// cualquier otro <c>Ok</c> y <c>Delivered</c>. (nuevo)
/// </summary>
public sealed class MensajeriaSimuladaConformidadTests : ConformidadDeMensajeria
{
    private readonly MemoriaDeMensajeriaSimulada _memoria = new();
    private MensajeriaSimulada? _canal;

    private MensajeriaSimulada Simulada => _canal ??= new MensajeriaSimulada(Options.Create(new ExternalChannelsOptions()), _memoria);

    protected override ICanalDeMensajeria Canal => Simulada;

    protected override bool EsSimulado => true;

    protected override ICanalDeMensajeria CanalQueNoContestaATiempo
    {
        get
        {
            var opciones = new ExternalChannelsOptions();
            opciones.Channels[CodigosDeCanalExterno.Simulado] = new OpcionesDeCanalExterno { TotalTimeoutSeconds = 1, DelayMilliseconds = 1_500 };
            return new MensajeriaSimulada(Options.Create(opciones), _memoria);
        }
    }

    protected override MensajeSaliente Mensaje(CasoDeMensajeria caso) => MensajeConDocumento(caso switch
    {
        CasoDeMensajeria.Entregado => "10203042",
        CasoDeMensajeria.EntregaFallida => "10203043",
        CasoDeMensajeria.DatoInvalido => "10203041",
        CasoDeMensajeria.SinSalir => "10203045",
        CasoDeMensajeria.Ambiguo => "10203047",
        _ => throw new ArgumentOutOfRangeException(nameof(caso)),
    });

    private static MensajeSaliente MensajeConDocumento(string documento) =>
        new(Guid.NewGuid(), "3001234567", null, "Hola", null, null, [], documento);

    [Theory]
    [InlineData("71000001", ExternalOutcome.InvalidData, null)]
    [InlineData("71000002", ExternalOutcome.Ok, DeliveryState.Delivered)]
    [InlineData("71000003", ExternalOutcome.Ok, DeliveryState.Failed)]
    [InlineData("71000005", ExternalOutcome.ChannelUnavailable, null)]
    [InlineData("71000004", ExternalOutcome.Ok, DeliveryState.Delivered)]
    [InlineData("71000000", ExternalOutcome.Ok, DeliveryState.Delivered)]
    public async Task El_ultimo_digito_del_documento_decide(string documento, ExternalOutcome envio, DeliveryState? entrega)
    {
        var mensaje = MensajeConDocumento(documento);

        var r = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);

        Assert.Equal(envio, r.Outcome);
        if (entrega is null) return;
        Assert.StartsWith("SIM-", r.ReferenciaExterna, StringComparison.Ordinal);
        var consulta = await Canal.ConsultarEntregaAsync(r.ReferenciaExterna!, Contexto(), default);
        Assert.Equal(entrega, consulta.Valor!.Estado);
    }

    [Fact]
    public async Task Sin_documento_decide_el_ultimo_digito_del_destino()
    {
        var mensaje = new MensajeSaliente(Guid.NewGuid(), "3001234565", null, "Hola", null, null, []);

        var r = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
    }

    [Fact]
    public async Task Recuerda_por_cooperativa_lo_que_respondio()
    {
        var mensaje = MensajeConDocumento("10203043");
        var envio = await Canal.EnviarAsync(mensaje, Contexto(mensaje.MensajePublicId), default);

        var otraCooperativa = Contexto() with { TenantPublicId = Guid.NewGuid() };
        var consulta = await Canal.ConsultarEntregaAsync(envio.ReferenciaExterna!, otraCooperativa, default);

        Assert.Equal(ExternalOutcome.NotFound, consulta.Outcome);
        Assert.True(_memoria.Conoce(Cooperativa, envio.ReferenciaExterna!));
    }
}
