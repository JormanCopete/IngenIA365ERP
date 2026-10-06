using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.ExternalChannels.Circuit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Common.ExternalChannels;

/// <summary>
/// <c>CircuitoDeCanalExterno</c> (feature 013, T0081; T72; contracts/adaptadores-y-archivos.md §3.5): fallas seguidas por cooperativa y
/// canal; al umbral (<c>Canales.UmbralDeFallas</c>, defecto 5) abre y levanta <c>Cartera.CanalExternoNoDisponible</c> con
/// <c>DedupKey …:{clase}:{canal}</c>; abierto, no llama al proveedor; cada cinco minutos sondea con <c>ProbarAsync</c> y, si responde, cierra;
/// nunca lanza hacia quien avisa. (nuevo)
/// </summary>
public sealed class CircuitoDeCanalExternoTests
{
    private static readonly Guid CooperativaA = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");
    private static readonly Guid CooperativaB = Guid.Parse("7b9e2d10-0000-4000-8000-0000000000b2");
    private const string Canal = "PROV-SMS";

    private readonly EstadoDeLosCircuitosExternos _estado = new();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ILectorDeParametros _parametros = Substitute.For<ILectorDeParametros>();
    private DateTime _ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);

    public CircuitoDeCanalExternoTests()
    {
        _reloj.UtcNow.Returns(_ => _ahora);
        _reloj.HoyLocal.Returns(new DateOnly(2026, 12, 5));
        _parametros.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<ParameterScopeKind>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ValorDeParametro>("Parameters.KeyNotFound", "sin parámetro"));
        _alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AlertaLevantada>("Alerts.Type.NotFound", "sin tipo"));
    }

    private CircuitoDeCanalExterno Circuito(Guid cooperativa)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(cooperativa.ToString());
        return new CircuitoDeCanalExterno(_estado, tenant, _reloj, _parametros, _alertas, NullLogger<CircuitoDeCanalExterno>.Instance);
    }

    private static Func<CancellationToken, Task<ExternalOutcome>> Prueba(ExternalOutcome resultado) => _ => Task.FromResult(resultado);

    private async Task FallarAsync(CircuitoDeCanalExterno circuito, int veces, ExternalOutcome falla = ExternalOutcome.ChannelUnavailable)
    {
        for (var i = 0; i < veces; i++) await circuito.RegistrarAsync(ExternalChannelKind.Sms, Canal, falla, default);
    }

    [Fact]
    public async Task Abre_al_umbral_por_defecto_y_levanta_la_alerta_con_su_DedupKey()
    {
        var circuito = Circuito(CooperativaA);

        await FallarAsync(circuito, 4);
        Assert.Equal(EstadoDelCircuito.Closed, circuito.Estado(Canal));

        await FallarAsync(circuito, 1, ExternalOutcome.InProcess);
        Assert.Equal(EstadoDelCircuito.Open, circuito.Estado(Canal));
        await _alertas.Received(1).LevantarAsync(
            Arg.Is<AlertaALevantar>(a => a.TypeCode == "Cartera.CanalExternoNoDisponible" && a.DedupKey == "Cartera.CanalExternoNoDisponible:Sms:PROV-SMS"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_respuesta_reinicia_la_cuenta_de_fallas_seguidas()
    {
        var circuito = Circuito(CooperativaA);

        await FallarAsync(circuito, 4);
        await circuito.RegistrarAsync(ExternalChannelKind.Sms, Canal, ExternalOutcome.Rejected, default);
        await FallarAsync(circuito, 4);

        Assert.Equal(EstadoDelCircuito.Closed, circuito.Estado(Canal));
    }

    [Fact]
    public async Task Cuenta_por_cooperativa()
    {
        await FallarAsync(Circuito(CooperativaA), 5);

        Assert.Equal(EstadoDelCircuito.Open, Circuito(CooperativaA).Estado(Canal));
        Assert.Equal(EstadoDelCircuito.Closed, Circuito(CooperativaB).Estado(Canal));
    }

    [Fact]
    public async Task Abierto_no_llama_y_a_los_cinco_minutos_sondea_y_cierra_si_responde()
    {
        var circuito = Circuito(CooperativaA);
        await FallarAsync(circuito, 5);
        var sondeos = 0;
        Func<CancellationToken, Task<ExternalOutcome>> probar = _ => { sondeos++; return Task.FromResult(ExternalOutcome.Ok); };

        Assert.False(await circuito.PuedeLlamarAsync(ExternalChannelKind.Sms, Canal, probar, default));
        Assert.Equal(0, sondeos);

        _ahora = _ahora.AddMinutes(5);
        Assert.True(await circuito.PuedeLlamarAsync(ExternalChannelKind.Sms, Canal, probar, default));
        Assert.Equal(1, sondeos);
        Assert.Equal(EstadoDelCircuito.Closed, circuito.Estado(Canal));
        await _alertas.Received(1).AtenderPorProcesoAsync("Cartera.CanalExternoNoDisponible:Sms:PROV-SMS", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_sondeo_fallido_lo_deja_abierto_otros_cinco_minutos()
    {
        var circuito = Circuito(CooperativaA);
        await FallarAsync(circuito, 5);
        _ahora = _ahora.AddMinutes(6);

        Assert.False(await circuito.PuedeLlamarAsync(ExternalChannelKind.Sms, Canal, Prueba(ExternalOutcome.ChannelUnavailable), default));
        _ahora = _ahora.AddMinutes(2);
        var llamado = false;
        Assert.False(await circuito.PuedeLlamarAsync(ExternalChannelKind.Sms, Canal, _ => { llamado = true; return Task.FromResult(ExternalOutcome.Ok); }, default));
        Assert.False(llamado);
    }

    [Fact]
    public async Task El_umbral_sale_del_parametro()
    {
        var definicion = IngenIA365ERP.Domain.Lending.Parameters.ParametrosDeCartera.Definiciones
            .First(d => d.Clave == IngenIA365ERP.Domain.Lending.Parameters.ParametrosDeCartera.CanalesUmbralDeFallas);
        _parametros.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<ParameterScopeKind>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(definicion, "2", 2, null)));
        var circuito = Circuito(CooperativaA);

        await FallarAsync(circuito, 2);

        Assert.Equal(EstadoDelCircuito.Open, circuito.Estado(Canal));
    }

    [Fact]
    public async Task Nunca_lanza_aunque_fallen_las_alertas_ni_el_sondeo()
    {
        _alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin base"));
        var circuito = Circuito(CooperativaA);

        await FallarAsync(circuito, 5);
        _ahora = _ahora.AddMinutes(5);
        var puede = await circuito.PuedeLlamarAsync(ExternalChannelKind.Sms, Canal, _ => throw new HttpRequestException("caído"), default);

        Assert.False(puede);
        Assert.Equal(EstadoDelCircuito.Open, circuito.Estado(Canal));
    }

    [Fact]
    public async Task El_registro_de_mensajeria_no_llama_al_proveedor_con_el_circuito_abierto()
    {
        var circuito = Circuito(CooperativaA);
        await FallarAsync(circuito, 5);
        var proveedor = Substitute.For<ICanalDeMensajeria>();
        proveedor.ChannelCode.Returns(Canal);
        proveedor.Capacidades.Returns(new CapacidadesDeMensajeria(new HashSet<MessageChannel> { MessageChannel.Sms }, false, false, 160, true));
        proveedor.ProbarAsync(Arg.Any<ContextoDeCanalExterno>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoExterno<bool>.Sin(ExternalOutcome.ChannelUnavailable, Canal, false));
        var registro = new CanalesDeMensajeria([proveedor], circuito);
        var contexto = new ContextoDeCanalExterno(CooperativaA, Canal, null, null, "k", false);

        var r = await registro.Resolver(Canal).EnviarAsync(new MensajeSaliente(Guid.NewGuid(), "3001234567", null, "Hola", null, null, []), contexto, default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        await proveedor.DidNotReceive().EnviarAsync(Arg.Any<MensajeSaliente>(), Arg.Any<ContextoDeCanalExterno>(), Arg.Any<CancellationToken>());
    }
}
