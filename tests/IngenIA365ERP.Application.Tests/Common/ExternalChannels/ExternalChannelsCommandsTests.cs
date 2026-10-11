using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Core.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Messaging;
using IngenIA365ERP.ExternalChannels.RestrictiveLists;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.ExternalChannels;

/// <summary>
/// La configuración por cooperativa (feature 013, T0080; T72, T73; contracts/api.md §21.2): configurar cierra la anterior la víspera;
/// <c>SIMULADO</c> se admite en producción; un código sin adaptador es <c>ChannelUnknown</c> con los disponibles; un canal real exige la
/// clave derivada; las vigencias no se cruzan; <c>CredentialKey</c> no viaja a la auditoría; verificar llama <c>ProbarAsync</c> y fija
/// <c>VerifiedAt</c>. Y la composición de <c>AddExternalChannels</c> (T0081): dos adaptadores con el mismo código hacen fallar el
/// arranque. (nuevo)
/// </summary>
public sealed class ExternalChannelsCommandsTests
{
    private static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");
    private static readonly DateOnly Enero = new(2026, 1, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentTenantService _tenant = Substitute.For<ICurrentTenantService>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly List<IRegistroDeCanalesExternos> _registros;

    /// <summary>Un adaptador de SMS de un proveedor, que el despliegue «tiene instalado».</summary>
    private sealed class SmsDelProveedor : ICanalDeMensajeria
    {
        public string ChannelCode => "PROV-SMS";

        public CapacidadesDeMensajeria Capacidades { get; } =
            new(new HashSet<Domain.Enums.Lending.MessageChannel> { Domain.Enums.Lending.MessageChannel.Sms }, false, false, 160, true);

        public Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(contexto.Credencial is { Valores.Count: > 0 }
                ? ResultadoExterno<bool>.Ok(true, ChannelCode, false)
                : ResultadoExterno<bool>.Sin(ExternalOutcome.Rejected, ChannelCode, false, "Credencial rechazada por el proveedor."));
    }

    public ExternalChannelsCommandsTests()
    {
        _tenant.TenantId.Returns(Cooperativa.ToString());
        _reloj.UtcNow.Returns(new DateTime(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc));
        _reloj.HoyLocal.Returns(new DateOnly(2026, 12, 5));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 3, Guid.NewGuid(), Guid.NewGuid(),
            "Administradora de ensayo", "admin@coop.test", ExecutionChannel.Web, "POST /api/core/external-channels", "10.0.0.2", null));
        var mensajeria = new CanalesDeMensajeria(
            [new MensajeriaSimulada(Options.Create(new ExternalChannelsOptions()), new MemoriaDeMensajeriaSimulada()), new SmsDelProveedor()],
            new CircuitoSiempreCerrado());
        var listas = new ProveedoresDeListas([new ListasSimuladas(Options.Create(new ExternalChannelsOptions()))], new CircuitoSiempreCerrado());
        _registros = [mensajeria, listas];
    }

    private ConfigureExternalChannelCommandHandler Configurar() => new(_db, _registros, new CircuitoSiempreCerrado(), _tenant, _actor, _reloj);

    private static ConfigureExternalChannelCommand Comando(string canal, DateOnly desde, string? clave = null,
        ExternalChannelKind clase = ExternalChannelKind.Sms) => new(clase, null, canal, desde, clave, "Contrato firmado con el proveedor");

    [Fact]
    public async Task El_simulado_se_configura_sin_credencial_y_se_marca_simulado()
    {
        var r = await Configurar().Handle(Comando("simulado", Enero), default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal("SIMULADO", r.Value.ChannelCode);
        Assert.True(r.Value.Simulated);
        Assert.Null(r.Value.CredentialKey);
        Assert.Equal(EstadoDelCircuito.Closed, r.Value.Circuit);
        var fila = await _db.ExternalChannelSettings.SingleAsync();
        Assert.Equal("Sms|*", fila.SettingKey);
    }

    [Fact]
    public async Task Configurar_otro_canal_cierra_el_vigente_la_vispera()
    {
        await Configurar().Handle(Comando("SIMULADO", Enero), default);

        var r = await Configurar().Handle(Comando("PROV-SMS", new DateOnly(2026, 7, 1), CredencialDeCanalExterno.ClaveDe(Cooperativa, "PROV-SMS")), default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        var filas = await _db.ExternalChannelSettings.OrderBy(s => s.ValidFrom).ToListAsync();
        Assert.Equal(new DateOnly(2026, 6, 30), filas[0].ValidTo);
        Assert.Null(filas[1].ValidTo);
        Assert.Equal($"{Cooperativa:D}.PROV-SMS.json", filas[1].CredentialKey);
    }

    [Fact]
    public async Task Las_vigencias_de_la_misma_clase_no_se_cruzan()
    {
        await Configurar().Handle(Comando("SIMULADO", new DateOnly(2026, 7, 1)), default);

        var r = await Configurar().Handle(Comando("SIMULADO", new DateOnly(2026, 3, 1)), default);

        Assert.Equal(ErroresDeCanalesExternos.CodigoSeCruza, r.Error.Code);
    }

    [Fact]
    public async Task Un_canal_sin_adaptador_en_el_despliegue_es_ChannelUnknown_con_los_disponibles()
    {
        var r = await Configurar().Handle(Comando("OTRO-PROV", Enero, "x"), default);

        Assert.Equal(ErroresDeCanalesExternos.CodigoCanalDesconocido, r.Error.Code);
        Assert.Contains("PROV-SMS", r.Error.Message, StringComparison.Ordinal);
        Assert.Contains("SIMULADO", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_carga_propia_es_solo_de_listas()
    {
        var sms = await Configurar().Handle(Comando("CARGA-PROPIA", Enero), default);

        Assert.Equal(ErroresDeCanalesExternos.CodigoCanalDesconocido, sms.Error.Code);
    }

    [Fact]
    public async Task Un_canal_real_exige_la_clave_derivada_de_la_cooperativa()
    {
        var sinClave = await Configurar().Handle(Comando("PROV-SMS", Enero), default);
        var deOtra = await Configurar().Handle(Comando("PROV-SMS", Enero, CredencialDeCanalExterno.ClaveDe(Guid.NewGuid(), "PROV-SMS")), default);

        Assert.Equal(ErroresDeCanalesExternos.CodigoFaltaLaClave, sinClave.Error.Code);
        Assert.Equal(ErroresDeCanalesExternos.CodigoClaveNoCoincide, deOtra.Error.Code);
    }

    [Fact]
    public void La_clave_de_la_credencial_no_viaja_a_la_auditoria()
    {
        var propiedad = typeof(ExternalChannelSetting).GetProperty(nameof(ExternalChannelSetting.CredentialKey))!;

        Assert.True(propiedad.IsDefined(typeof(NoAuditarAttribute), inherit: true));
    }

    [Fact]
    public async Task Verificar_llama_ProbarAsync_y_fija_VerifiedAt_solo_si_responde_Ok()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "canales-verificacion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directorio);
        try
        {
            var configurada = await Configurar().Handle(Comando("PROV-SMS", Enero, CredencialDeCanalExterno.ClaveDe(Cooperativa, "PROV-SMS")), default);
            var credenciales = new IngenIA365ERP.ExternalChannels.Credentials.CredencialesDeCanalExternoEnArchivo(
                Options.Create(new ExternalChannelsOptions { CredentialsPath = directorio }),
                new IngenIA365ERP.ExternalChannels.Credentials.CacheDeCredencialesExternas(), _tenant, _db, _reloj);
            var verificar = new VerifyExternalChannelCredentialCommandHandler(_db, credenciales, _registros, _tenant, _actor, _reloj,
                Options.Create(new CanalesExternosOptions()));

            var sinArchivo = await verificar.Handle(new VerifyExternalChannelCredentialCommand(configurada.Value.PublicId), default);
            Assert.Equal(ErroresDeCanalesExternos.CodigoSinCredencial, sinArchivo.Error.Code);

            File.WriteAllText(Path.Combine(directorio, CredencialDeCanalExterno.ClaveDe(Cooperativa, "PROV-SMS")), """{ "usuario": "u", "clave": "c" }""");
            var r = await verificar.Handle(new VerifyExternalChannelCredentialCommand(configurada.Value.PublicId), default);

            Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
            Assert.Equal(ExternalOutcome.Ok, r.Value.Outcome);
            var fila = await _db.ExternalChannelSettings.SingleAsync();
            Assert.Equal(new DateTime(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc), fila.VerifiedAt);
            Assert.Equal("Administradora de ensayo", fila.VerifiedByName);
            Assert.Equal(ExternalOutcome.Ok, fila.LastVerificationOutcome);
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }

    [Fact]
    public async Task La_lista_trae_simulated_y_el_estado_del_circuito()
    {
        await Configurar().Handle(Comando("SIMULADO", Enero), default);
        await Configurar().Handle(Comando("SIMULADO", Enero, clase: ExternalChannelKind.RestrictiveLists), default);

        var r = await new ListExternalChannelSettingsQueryHandler(_db, new CircuitoSiempreCerrado())
            .Handle(new ListExternalChannelSettingsQuery(ExternalChannelKind.RestrictiveLists), default);

        var fila = Assert.Single(r.Value);
        Assert.Equal(ExternalChannelKind.RestrictiveLists, fila.Kind);
        Assert.True(fila.Simulated);
        Assert.Equal(EstadoDelCircuito.Closed, fila.Circuit);
    }

    // ---------------------------------------------------------------------------------------------------------- composición --

    private static ServiceCollection Contenedor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        return services;
    }

    [Fact]
    public void AddExternalChannels_suma_los_simulados_y_la_carga_propia_a_los_registros()
    {
        var services = Contenedor();
        services.AddExternalChannels(new ConfigurationBuilder().Build());

        var registrados = services.Where(d => d.ImplementationInstance is CanalExternoRegistrado)
            .Select(d => (CanalExternoRegistrado)d.ImplementationInstance!).ToList();
        Assert.Contains(registrados, r => r is { Puerto: PuertosExternos.Mensajeria, ChannelCode: "CORREO" });
        Assert.Contains(registrados, r => r is { Puerto: PuertosExternos.Mensajeria, ChannelCode: "SIMULADO" });
        Assert.Contains(registrados, r => r is { Puerto: PuertosExternos.ListasRestrictivas, ChannelCode: "SIMULADO" });
        Assert.Contains(registrados, r => r is { Puerto: PuertosExternos.ListasRestrictivas, ChannelCode: "CARGA-PROPIA" });
        Assert.Contains(services, d => d.ServiceType == typeof(ICredencialesDeCanalExterno)
                                       && d.ImplementationType == typeof(IngenIA365ERP.ExternalChannels.Credentials.CredencialesDeCanalExternoEnArchivo));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ICredencialesDeCanalExterno)
                                             && d.ImplementationType == typeof(SinCredencialesDeCanalExterno));
        Assert.Contains(services, d => d.ServiceType == typeof(ICircuitoDeCanalExterno)
                                       && d.ImplementationType == typeof(IngenIA365ERP.ExternalChannels.Circuit.CircuitoDeCanalExterno));
    }

    [Fact]
    public void Dos_adaptadores_con_el_mismo_codigo_hacen_fallar_el_arranque()
    {
        var services = Contenedor();
        services.AddExternalChannels(new ConfigurationBuilder().Build());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            IngenIA365ERP.ExternalChannels.DependencyInjection.AgregarCanal(services, PuertosExternos.Mensajeria, "simulado", typeof(SmsDelProveedor)));
        Assert.Contains("SIMULADO", ex.Message, StringComparison.OrdinalIgnoreCase);
        // El mismo código en otro puerto no choca (US2 registró el SIMULADO de centrales: el depósito de valores lo suma US3).
        IngenIA365ERP.ExternalChannels.DependencyInjection.AgregarCanal(services, PuertosExternos.DepositoDeValores, "SIMULADO", typeof(SmsDelProveedor));
        Assert.Throws<InvalidOperationException>(() =>
            IngenIA365ERP.ExternalChannels.DependencyInjection.AgregarCanal(services, PuertosExternos.CentralesDeRiesgo, "SIMULADO", typeof(SmsDelProveedor)));
    }

    [Fact]
    public void Sin_AddExternalChannels_el_correo_es_el_unico_canal_de_mensajeria()
    {
        var services = Contenedor();
        services.AddScoped(_ => Substitute.For<IEmailSender>());
        services.AddScoped(_ => Substitute.For<IDateTimeService>());
        services.AddScoped(_ => Substitute.For<ICurrentTenantService>());
        using var proveedor = services.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();

        var mensajeria = ambito.ServiceProvider.GetRequiredService<ICanalesDeMensajeria>();
        var listas = ambito.ServiceProvider.GetRequiredService<IProveedoresDeListas>();

        Assert.Equal(["CORREO"], mensajeria.Codigos);
        Assert.Empty(listas.Codigos);
        Assert.Empty(mensajeria.CodigosPara(ExternalChannelKind.Sms));
    }
}
