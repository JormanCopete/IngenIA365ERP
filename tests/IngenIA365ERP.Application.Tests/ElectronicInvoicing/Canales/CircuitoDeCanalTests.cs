using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.ElectronicInvoicing.Circuit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>
/// <c>CircuitoDeCanal</c> (feature 012, I4, T684; contracts/dian.md §7.2): cuenta las fallas <b>seguidas</b> del canal —un
/// <c>ChannelUnavailable</c> definitivo, un sondeo <c>ProbarAsync</c> fallido y una espera del POS vencida, que llegan todas por
/// <see cref="IRegistroDeFallasDelCanal"/> o por el sondeo— y al llegar a <c>Dian.UmbralFallasCircuito</c> abre un evento <c>Issuer03</c>
/// con <c>DetectedBy = Process</c>; una respuesta reinicia la cuenta; un <c>ProbarAsync</c> exitoso cierra el evento. (nuevo)
/// </summary>
public sealed class CircuitoDeCanalTests
{
    private const string Canal = "PROV";

    private readonly EmisionDePrueba _e = new();
    private readonly EstadoDeLosCircuitos _estado = new();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();

    public CircuitoDeCanalTests()
    {
        _e.Configuracion(Canal);
        Umbral(3);
    }

    private void Umbral(int fallas, DateOnly? desde = null)
    {
        _e.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.UmbralFallasCircuito,
            ScopeKind = ParameterScopeKind.None, Value = fallas.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValidFrom = desde ?? new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        _e.Db.SaveChanges();
    }

    private CircuitoDeCanal Circuito(ICurrentTenantService? tenant = null) => new(
        _estado, _e.Db, tenant ?? _e.Tenant, _e.Reloj, _e.Lector(), new ContingenciaDeLaDian(_e.Db, _e.Lector(), _alertas), _e.Canales,
        _e.Credenciales, NullLogger<CircuitoDeCanal>.Instance);

    private List<DianContingencyEvent> Eventos03() =>
        _e.Db.DianContingencyEvents.AsNoTracking().Where(e => e.Type == ContingencyType.Issuer03).ToList();

    [Fact]
    public async Task Abre_la_contingencia_03_al_llegar_al_umbral_de_fallas_seguidas()
    {
        var circuito = Circuito();

        await circuito.RegistrarFallaAsync(Canal, default);
        await circuito.RegistrarFallaAsync(Canal, default);
        Assert.Empty(Eventos03());
        await circuito.RegistrarFallaAsync(Canal, default);

        var evento = Assert.Single(Eventos03());
        Assert.Equal(Canal, evento.ChannelCode);
        Assert.Equal(ContingencyEventStatus.Open, evento.Status);
        Assert.Equal(ActorKind.Process, evento.DetectedByKind);
        Assert.Null(evento.DetectedByUserId);
        Assert.Equal(IngenIA365ERP.Application.Common.Execution.Actor.NombreDelProceso, evento.DetectedByName);
        Assert.Contains("3", evento.Reason, StringComparison.Ordinal);
        await _alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.ContingenciaAbierta), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_umbral_sale_del_parametro_vigente_no_de_una_constante()
    {
        Umbral(2, new DateOnly(2026, 12, 1));
        var circuito = Circuito();

        await circuito.RegistrarFallaAsync(Canal, default);
        await circuito.RegistrarFallaAsync(Canal, default);

        Assert.Single(Eventos03());
    }

    [Fact]
    public async Task Una_respuesta_del_canal_reinicia_la_cuenta()
    {
        var circuito = Circuito();

        await circuito.RegistrarFallaAsync(Canal, default);
        await circuito.RegistrarFallaAsync(Canal, default);
        await circuito.RegistrarRespuestaAsync(Canal, default);
        await circuito.RegistrarFallaAsync(Canal, default);
        await circuito.RegistrarFallaAsync(Canal, default);

        Assert.Empty(Eventos03());
        Assert.Equal(2, _estado.Fallas(_e.Cooperativa, Canal));
    }

    [Fact]
    public async Task Cuentan_juntos_el_ChannelUnavailable_el_sondeo_fallido_y_la_espera_del_POS_vencida()
    {
        _e.Proveedor.RespuestaDePrueba = ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable);
        var circuito = Circuito();

        await circuito.RegistrarFallaAsync(Canal, default);           // ChannelUnavailable definitivo de una emisión
        var sondeos = await circuito.SondearAsync(default);          // ProbarAsync fallido
        Assert.Empty(Eventos03());
        await Circuito().RegistrarFallaAsync(Canal, default);         // espera del POS vencida (otra petición, mismo circuito)

        var sondeo = Assert.Single(sondeos);
        Assert.Equal(Canal, sondeo.Canal);
        Assert.Equal(ChannelOutcome.ChannelUnavailable, sondeo.Resultado);
        Assert.Single(Eventos03());
    }

    [Fact]
    public async Task Un_ProbarAsync_exitoso_cierra_el_evento_que_abrio_el_circuito_y_reinicia_la_cuenta()
    {
        var circuito = Circuito();
        for (var i = 0; i < 3; i++) await circuito.RegistrarFallaAsync(Canal, default);
        _e.Proveedor.RespuestaDePrueba = ResultadoDeCanal.Sin(ChannelOutcome.Validated);

        var sondeos = await Circuito().SondearAsync(default);

        var evento = Assert.Single(Eventos03());
        Assert.Equal(ContingencyEventStatus.Closed, evento.Status);
        Assert.NotNull(evento.EndedAt);
        Assert.Equal(ActorKind.Process, evento.ClosedByKind);
        Assert.True(evento.DeadlineHoursApplied > 0);
        Assert.True(Assert.Single(sondeos).CerroContingencia);
        Assert.Equal(0, _estado.Fallas(_e.Cooperativa, Canal));
    }

    [Fact]
    public async Task Mientras_esta_abierto_un_sondeo_fallido_no_abre_otro_evento()
    {
        var circuito = Circuito();
        for (var i = 0; i < 3; i++) await circuito.RegistrarFallaAsync(Canal, default);
        _e.Proveedor.RespuestaDePrueba = ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable);

        await circuito.SondearAsync(default);
        await circuito.RegistrarFallaAsync(Canal, default);

        var evento = Assert.Single(Eventos03());
        Assert.Equal(ContingencyEventStatus.Open, evento.Status);
    }

    [Fact]
    public async Task No_cierra_la_contingencia_que_declaro_una_persona()
    {
        var manual = _e.Contingencia03(Canal);
        _e.Proveedor.RespuestaDePrueba = ResultadoDeCanal.Sin(ChannelOutcome.Validated);

        await Circuito().SondearAsync(default);

        Assert.Equal(ContingencyEventStatus.Open, _e.Db.DianContingencyEvents.AsNoTracking().Single(e => e.Id == manual.Id).Status);
    }

    [Fact]
    public async Task Con_una_03_ya_abierta_por_una_persona_llegar_al_umbral_no_abre_otra_ni_lanza()
    {
        _e.Contingencia03(Canal);
        var circuito = Circuito();

        for (var i = 0; i < 4; i++) await circuito.RegistrarFallaAsync(Canal, default);

        Assert.Single(Eventos03());
    }

    [Fact]
    public async Task Cada_cooperativa_tiene_su_propio_circuito()
    {
        var otra = Substitute.For<ICurrentTenantService>();
        otra.TenantId.Returns(Guid.NewGuid().ToString());

        await Circuito().RegistrarFallaAsync(Canal, default);
        await Circuito().RegistrarFallaAsync(Canal, default);
        await Circuito(otra).RegistrarFallaAsync(Canal, default);

        Assert.Empty(Eventos03());
        Assert.Equal(2, _estado.Fallas(_e.Cooperativa, Canal));
    }

    [Fact]
    public async Task Sin_fallas_ni_eventos_del_proceso_no_sondea()
    {
        var sondeos = await Circuito().SondearAsync(default);

        Assert.Empty(sondeos);
        Assert.Null(_e.Proveedor.UltimoContexto);
    }
}
