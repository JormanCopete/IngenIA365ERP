using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Tareas;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// El escenario común de las pruebas de la cola de mensajes al deudor (feature 013, US17, bloque E; T0270–T0272): el de
/// <see cref="GobiernoDePrueba"/> (InMemory, actor, alcance, auditoría) con el reloj en hora de Colombia, Cartera activada, los cinco
/// techos <c>COBRANZA_*</c> de la Ley 2300, un calendario con los festivos que diga la prueba, autorizaciones por canal a pedido, canales
/// de mensajería de prueba (<see cref="CanalDePrueba"/>) y un crédito de Ana (deudora) con Beto de codeudor. Carla no está obligada: es
/// la persona que figura como referencia, y nunca recibe nada. Hoy es martes 1 de diciembre de 2026, 10:00 a. m. (nuevo)
/// </summary>
internal sealed class MensajesDePrueba : IDisposable
{
    public const int Norte = 1;
    public const int Sur = 2;
    public const int Linea = 1;

    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public MensajesDePrueba(bool activada = true)
    {
        EnLocal(new DateTime(2026, 12, 1, 10, 0, 0));
        G.Reloj.AhoraLocal.Returns(_ => new DateTimeOffset(DateTime.SpecifyKind(G.UtcNow, DateTimeKind.Utc)).ToOffset(Colombia));
        G.Reloj.HoyLocal.Returns(_ => DateOnly.FromDateTime(new DateTimeOffset(DateTime.SpecifyKind(G.UtcNow, DateTimeKind.Utc)).ToOffset(Colombia).DateTime));

        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, 7m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 19m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioSabado, 8m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinSabado, 15m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaContactosDia, 1m, "Ley 2300 de 2023");

        Calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new CalendarioHabil(Festivos, ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2))));
        Canales.Resolver(Arg.Any<string>()).Returns(ci =>
            _canales.TryGetValue(ci.Arg<string>() ?? string.Empty, out var canal) ? canal : new CanalesDeMensajeria.CanalNoInstalado(ci.Arg<string>()));
        Canales.Codigos.Returns(_ => _canales.Keys.ToList());
        Canales.CodigosPara(Arg.Any<ExternalChannelKind>()).Returns(ci =>
        {
            var canal = ci.Arg<ExternalChannelKind>() == ExternalChannelKind.Sms ? MessageChannel.Sms : MessageChannel.WhatsApp;
            return _canales.Values.Where(c => c.Capacidades.Atiende(canal)).Select(c => c.ChannelCode).ToList();
        });
        Tenant.TenantId.Returns(Cooperativa.ToString());
        Alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));

        Simulado = Canal(CodigosDeCanalExterno.Simulado, [MessageChannel.Sms, MessageChannel.WhatsApp], consultaEntrega: true);
        Correo = Canal(CodigosDeCanalExterno.Correo, [MessageChannel.Email], consultaEntrega: false);

        Ana = G.Persona("Ana", "Deudora", "52000004");
        Ana.Email = "ana@correo.test";
        Ana.Mobile = "3001234564";
        Ana.Address = "Calle 1 # 2-3";
        Beto = G.Persona("Beto", "Codeudor", "79000006");
        Beto.Email = "beto@correo.test";
        Beto.Mobile = "3009876546";
        Carla = G.Persona("Carla", "Referencia", "41000008");
        Carla.Email = "carla@correo.test";
        Carla.Mobile = "3005556664";
        G.Db.SaveChanges();

        Credito = NuevoCredito("CR-0001", Norte, Ana);
        G.Db.LoanParties.Add(new LoanParty { LoanId = Credito.Id, PersonId = Beto.Id, Role = CreditPartyRole.CoDebtor, JoinedOn = new DateOnly(2026, 1, 10) });
        G.Db.SaveChanges();

        if (activada) Activar(G.UtcNow.AddDays(-30));

        Plantilla("ABONOCAP", DebtorMessagePurpose.Informative, MessageChannel.Email, "Abono aplicado",
            "Hola {{NombreDelTitular}}: aplicamos {{ValorAplicado}} al crédito {{NumeroDelCredito}}.");
        Plantilla("ABONOCAP", DebtorMessagePurpose.Informative, MessageChannel.Sms, null,
            "{{NombreDelTitular}}: aplicamos {{ValorAplicado}} al credito {{NumeroDelCredito}}.");
        Plantilla("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "{{NombreDelTitular}}: a la fecha {{FechaDeCorte}} su credito {{NumeroDelCredito}} tiene {{ValorVencido}} vencido.");
        Plantilla("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Email, "Su crédito tiene saldo vencido",
            "A la fecha {{FechaDeCorte}} su crédito {{NumeroDelCredito}} tiene {{ValorVencido}} vencido.");
    }

    public GobiernoDePrueba G { get; } = new();
    public TestApplicationDbContext Db => G.Db;
    public Guid Cooperativa { get; } = Guid.Parse("6f7a1d3e-1b2c-4d5e-8f90-123456789abc");
    public ICalendarioHabil Calendario { get; } = Substitute.For<ICalendarioHabil>();
    public IAutorizacionDeDatos Autorizaciones { get; } = Substitute.For<IAutorizacionDeDatos>();
    public ICanalesDeMensajeria Canales { get; } = Substitute.For<ICanalesDeMensajeria>();
    public ICredencialesDeCanalExterno Credenciales { get; } = Substitute.For<ICredencialesDeCanalExterno>();
    public ICurrentTenantService Tenant { get; } = Substitute.For<ICurrentTenantService>();
    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();
    public CanalesExternosOptions Opciones { get; } = new() { AmbienteDePruebas = true };
    public List<DateOnly> Festivos { get; } = [];

    public Person Ana { get; }
    public Person Beto { get; }
    public Person Carla { get; }
    public Loan Credito { get; }
    public CanalDePrueba Simulado { get; }
    public CanalDePrueba Correo { get; }

    private readonly Dictionary<string, CanalDePrueba> _canales = new(StringComparer.OrdinalIgnoreCase);

    public void Dispose() => G.Dispose();

    // ------------------------------------------------------------------------------------------------------------ reloj --

    /// <summary>Pone el reloj en <paramref name="local"/> (hora de Colombia).</summary>
    public void EnLocal(DateTime local) => G.UtcNow = DateTime.SpecifyKind(local - Colombia, DateTimeKind.Utc);

    public static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local - Colombia, DateTimeKind.Utc);

    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc + Colombia, DateTimeKind.Unspecified);

    // ------------------------------------------------------------------------------------------------------------ datos --

    public void Activar(DateTime registradaUtc)
    {
        var setup = Db.LendingSetups.FirstOrDefault(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        if (setup is null)
        {
            setup = new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica };
            Db.LendingSetups.Add(setup);
        }
        setup.ActivationDate = DateOnly.FromDateTime(Local(registradaUtc));
        setup.ActivatedAt = registradaUtc;
        Db.SaveChanges();
    }

    public Loan NuevoCredito(string numero, int oficina, Person deudor)
    {
        var l = new Loan
        {
            Number = numero, BranchId = oficina, CreditLineId = Linea, DebtorPersonId = deudor.Id, OpenedOn = new DateOnly(2025, 1, 10),
            Status = LoanStatus.Active, ApprovedAmount = 1_000_000m,
        };
        Db.Loans.Add(l);
        Db.SaveChanges();
        return l;
    }

    public NotificationTemplate Plantilla(string code, DebtorMessagePurpose proposito, MessageChannel canal, string? asunto, string cuerpo,
        int version = 1, DateOnly? desde = null, DateOnly? hasta = null, string? providerTemplateId = null)
    {
        var t = new NotificationTemplate
        {
            TemplateName = $"{code} v{version}", Channel = canal.ToString(), Subject = asunto, BodyTemplate = cuerpo, IsActive = true, Code = code,
            Module = "Lending", Purpose = proposito.ToString(), Version = version, ValidFrom = desde ?? new DateOnly(2020, 1, 1), ValidTo = hasta,
            ProviderTemplateId = providerTemplateId, Variables = RenderizadorDePlantillasDeMensaje.VariablesComoJson(proposito), IsSeeded = true,
            ReviewPending = true, CreatedBy = "semilla",
        };
        Db.NotificationTemplates.Add(t);
        Db.SaveChanges();
        return t;
    }

    /// <summary>La persona autorizó <paramref name="canal"/> (con o sin evidencia) y el tratamiento general.</summary>
    public void Autorizar(Person p, MessageChannel canal, bool conEvidencia = true)
    {
        var consentimiento = new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, G.UtcNow.AddYears(-1), canal.ToString())
        {
            Purpose = DataAuthorizationPurpose.ContactChannel, HasEvidence = conEvidencia, ConsentPublicId = Guid.NewGuid(),
            Origin = conEvidencia ? DataAuthorizationOrigin.PersonRegistration : DataAuthorizationOrigin.Migration,
        };
        Autorizaciones.VigenteAsync(p.PublicId, DataAuthorizationPurpose.ContactChannel, canal.ToString(), Arg.Any<CancellationToken>())
            .Returns(consentimiento);
        AutorizarTratamiento(p);
    }

    public void AutorizarTratamiento(Person p, bool conEvidencia = true)
    {
        var general = new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, G.UtcNow.AddYears(-1), null)
        {
            HasEvidence = conEvidencia, ConsentPublicId = Guid.NewGuid(),
        };
        Autorizaciones.VigenteAsync(p.PublicId, DataAuthorizationPurpose.GeneralTreatment, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(general);
        Autorizaciones.VigenteAsync(p.PublicId, Arg.Any<CancellationToken>()).Returns(general);
    }

    /// <summary>La cooperativa configuró <paramref name="codigo"/> como su canal de SMS (o WhatsApp) desde siempre.</summary>
    public void ConfigurarCanal(ExternalChannelKind clase, string codigo)
    {
        Db.ExternalChannelSettings.Add(new ExternalChannelSetting
        {
            Kind = clase, SettingKey = ExternalChannelSetting.ClaveDe(clase, null), ChannelCode = codigo, ValidFrom = new DateOnly(2020, 1, 1),
            Reason = "Prueba", CreatedBy = "test",
        });
        Db.SaveChanges();
    }

    public CanalDePrueba Canal(string codigo, MessageChannel[] canales, bool consultaEntrega)
    {
        var canal = new CanalDePrueba(codigo, canales, consultaEntrega);
        _canales[codigo] = canal;
        return canal;
    }

    /// <summary>La posición del día del crédito (para «el crédito se puso al día»).</summary>
    public void Posicion(Loan credito, int diasDeMora, DateOnly? fecha = null) =>
        Db.LoanDailyPositions.Add(new IngenIA365ERP.Domain.Entities.Lending.Projections.LoanDailyPosition
        {
            LoanId = credito.Id, PositionDate = fecha ?? G.Reloj.HoyLocal, Version = 1, RegisteredAt = G.UtcNow, Status = LoanStatus.Active,
            BranchId = credito.BranchId, CreditLineId = credito.CreditLineId, ModalityCode = "CONSUMO", DaysPastDue = diasDeMora,
            InstallmentsPastDue = diasDeMora > 0 ? 1 : 0, PrincipalPastDue = diasDeMora > 0 ? 50_000m : 0m,
        });

    // ---------------------------------------------------------------------------------------------------------- piezas --

    public ColaDeMensajesAlDeudor Cola() =>
        new(Db, G.Reloj, new ReglasDeParametrosDeCartera(G.Valores, G.Parametros()), Calendario, Autorizaciones, Canales, Options.Create(Opciones));

    public RegisterDebtorMessageAttemptCommandHandler Registro() =>
        new(Db, Cola(), new ReglasDeParametrosDeCartera(G.Valores, G.Parametros()), Alertas, G.Reloj,
            NullLogger<RegisterDebtorMessageAttemptCommandHandler>.Instance);

    /// <summary>Los servicios del ámbito de la cooperativa que arma el ejecutor para una tarea (con un <c>ISender</c> real sobre el registro).</summary>
    public IServiceProvider Servicios()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RegisterDebtorMessageAttemptCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => Registro().Handle(ci.Arg<RegisterDebtorMessageAttemptCommand>(), ci.Arg<CancellationToken>()));
        Credenciales.ResolverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success(CredencialDeCanalExterno.Vacia(Cooperativa, ci.Arg<string>())));
        return new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(Db)
            .AddSingleton(G.Reloj)
            .AddSingleton(Cola())
            .AddSingleton(sender)
            .AddSingleton(Canales)
            .AddSingleton(Credenciales)
            .AddSingleton(Tenant)
            .AddSingleton(new GuardiaDeCanalesExternos(Options.Create(Opciones)))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    // ------------------------------------------------------------------------------------------------------- mensajes --

    public static PedidoDeMensajeAlDeudor Abono(Person p, Loan credito, string entidad = "op-1", MessageChannel canal = MessageChannel.Email) =>
        new(p.Id, credito.Id, DebtorMessagePurpose.Informative, canal, "ABONOCAP", "PaymentApplied", entidad, ActorKind.Person, 7)
        {
            OriginPublicId = Guid.NewGuid(),
            Valores = new Dictionary<string, string?> { ["ValorAplicado"] = "$ 100.000" },
        };

    public static PedidoDeMensajeAlDeudor Mora(Person p, Loan credito, string entidad = "etapa-1", MessageChannel canal = MessageChannel.Sms) =>
        new(p.Id, credito.Id, DebtorMessagePurpose.Collection, canal, "MORAADMIN", "CollectionStage", entidad, ActorKind.Process, null)
        {
            Valores = new Dictionary<string, string?> { ["ValorVencido"] = "$ 150.000" },
        };

    /// <summary>Encola y guarda, como lo haría quien lo motiva.</summary>
    public async Task<MensajeEncolado> EncolarYGuardarAsync(PedidoDeMensajeAlDeudor pedido)
    {
        var r = await Cola().EncolarAsync(pedido, default);
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        await Db.SaveChangesAsync();
        return r.Value;
    }

    /// <summary>Un mensaje que salió por <paramref name="canal"/> en <paramref name="enLocal"/> (cuenta en el historial).</summary>
    public async Task<DebtorMessage> YaEnviadoAsync(Person p, MessageChannel canal, DebtorMessagePurpose proposito, DateTime enLocal, string clave)
    {
        var m = DebtorMessage.Nacer(new BorradorDeMensajeAlDeudor(p.Id, p.Id == Ana.Id ? ContactRole.Debtor : ContactRole.CoDebtor, Credito.Id,
            proposito, canal, canal == MessageChannel.Email ? CodigosDeCanalExterno.Correo : CodigosDeCanalExterno.Simulado, "Texto ya enviado", clave,
            "Manual", Utc(enLocal), ActorKind.Process, null));
        Db.DebtorMessages.Add(m);
        await Db.SaveChangesAsync();
        var en = Utc(enLocal);
        var encolado = DebtorMessageEvent.Registrar(m.Id, null, new EventoDeMensaje(DebtorMessageEventKind.Queued, en, ActorKind.Process) { DueAt = en }, false);
        Db.DebtorMessageEvents.Add(encolado);
        await Db.SaveChangesAsync();
        Db.DebtorMessageEvents.Add(DebtorMessageEvent.Registrar(m.Id, encolado,
            new EventoDeMensaje(DebtorMessageEventKind.Sent, en, ActorKind.Process) { DestinationAddress = "destino", AttemptNumber = 1, ProviderReference = "REF" },
            false));
        await Db.SaveChangesAsync();
        return m;
    }

    public async Task<IReadOnlyList<DebtorMessageEvent>> EventosAsync(DebtorMessage m) =>
        await Db.DebtorMessageEvents.AsNoTracking().Where(e => e.DebtorMessageId == m.Id).OrderBy(e => e.Sequence).ToListAsync();

    public async Task<DebtorMessageEvent> UltimoAsync(DebtorMessage m) => (await EventosAsync(m))[^1];
}

/// <summary>
/// Un canal de mensajería de prueba: responde lo que la prueba encole (por defecto <c>Ok</c>), recuerda lo que envió y contesta la consulta
/// de entrega con <see cref="Entrega"/>. Simulado si su código es <c>SIMULADO</c>. (nuevo)
/// </summary>
internal sealed class CanalDePrueba(string codigo, MessageChannel[] canales, bool consultaEntrega) : ICanalDeMensajeria
{
    public string ChannelCode { get; } = codigo;

    public CapacidadesDeMensajeria Capacidades { get; } = new(canales.ToHashSet(), false, false, null, consultaEntrega);

    public Queue<ExternalOutcome> Respuestas { get; } = new();
    public List<MensajeSaliente> Enviados { get; } = [];
    public List<string> Consultados { get; } = [];
    public DeliveryState Entrega { get; set; } = DeliveryState.Delivered;

    private bool EsSimulado => CodigosDeCanalExterno.EsSimulado(ChannelCode);

    public Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        Enviados.Add(mensaje);
        var outcome = Respuestas.Count > 0 ? Respuestas.Dequeue() : ExternalOutcome.Ok;
        var referencia = $"{(EsSimulado ? "SIM" : "REF")}-{mensaje.MensajePublicId:N}";
        return Task.FromResult(outcome == ExternalOutcome.Ok
            ? ResultadoExterno<EnvioDeMensaje>.Ok(new EnvioDeMensaje(referencia, DateTimeOffset.UnixEpoch), ChannelCode, EsSimulado, referencia)
            : ResultadoExterno<EnvioDeMensaje>.Sin(outcome, ChannelCode, EsSimulado, $"Respuesta de prueba {outcome}", codigoDelProveedor: "P-" + outcome));
    }

    public Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        Consultados.Add(referenciaExterna);
        return Task.FromResult(ResultadoExterno<EstadoDeEntrega>.Ok(new EstadoDeEntrega(Entrega, DateTimeOffset.UnixEpoch, null), ChannelCode, EsSimulado,
            referenciaExterna));
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, ChannelCode, EsSimulado));
}
