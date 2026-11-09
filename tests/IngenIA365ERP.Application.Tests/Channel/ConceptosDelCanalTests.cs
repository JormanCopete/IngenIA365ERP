using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Channel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US3 (T081): los conceptos que ve el asociado salen del mismo listado que la caja, filtrados por lo publicado, y la
/// simulación sólo corre sobre un concepto publicado y una modalidad admitida.
/// </summary>
public class ConceptosDelCanalTests
{
    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 12, 5);

    private sealed class Escenario
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public IContratoDeObligaciones Obligaciones { get; } = Substitute.For<IContratoDeObligaciones>();
        public IContratoDeCuentas Cuentas { get; } = Substitute.For<IContratoDeCuentas>();
        public IConsultaDeObligaciones Consulta { get; } = Substitute.For<IConsultaDeObligaciones>();
        public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();

        public AuditoriaDelCanal Auditoria { get; } =
            new(new ServiceCollection().BuildServiceProvider(), NullLogger<AuditoriaDelCanal>.Instance);

        public Person Persona { get; }
        public ChannelPaymentDestination Destino { get; }
        public List<ObligacionListadaDto> Obligaciones_ { get; } = [];
        public List<CuentaConsultadaDto> Cuentas_ { get; } = [];
        public List<ExigibleDeCuentaDto> Exigibles { get; } = [];

        public Escenario(bool habilitada = true)
        {
            Reloj.UtcNow.Returns(Ahora);
            Reloj.HoyLocal.Returns(Hoy);
            Persona = new Person { FirstName = "Ana", LastName = "Prueba", TaxId = "1010", IdType = "C", Username = "1010", Status = "A", CreatedBy = "test" };
            Db.People.Add(Persona);
            Db.SaveChanges();
            var h = new ChannelEnablement { PersonId = Persona.Id };
            HabilitacionesDelCanal.Habilitar(h, Ahora, EnablementSource.Manual);
            if (!habilitada) HabilitacionesDelCanal.Inhabilitar(h, Ahora, "Prueba");
            Db.ChannelEnablements.Add(h);
            Destino = new ChannelPaymentDestination { Name = "Avalpay", CollectionAgreementId = 1 };
            Db.ChannelPaymentDestinations.Add(Destino);
            Db.SaveChanges();

            Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(_ =>
                new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "", "",
                    new ListadoDeObligacionesDto(EstadoDeCartera.Active, Persona.PublicId, Hoy, Obligaciones_)));
            Consulta.ObligacionesAsync(Arg.Any<ConsultaDeObligaciones>(), Arg.Any<CancellationToken>()).Returns(_ =>
                new ObligacionesDePersonasDto(EstadoDeCartera.Active, [new ObligacionesDePersonaDto(Persona.PublicId, false,
                    Obligaciones_.Select(o => new ObligacionDto(o.ObligationPublicId, o.Number, o.CreditLineCode, o.CreditLineName, ClaseDeObligacion.Standard, null,
                        RolEnLaObligacion.Debtor, o.Status, true, null, 5_400_000m, o.DueToday, 1, o.DaysPastDue, 5_000_000m, 0m, 12_500m, 20, 280_000m)).ToList())]));
            Consulta.CuotasDeNominaAsync(Arg.Any<ConsultaDeCuotasDeNomina>(), Arg.Any<CancellationToken>()).Returns(_ =>
                new CuotasDeNominaDto(EstadoDeCartera.Active, Obligaciones_.Select(o => new CuotaDeNominaDto(Persona.PublicId, o.ObligationPublicId, o.Number,
                    o.CreditLineCode, 7, new DateOnly(2026, 12, 15), 280_000m, 0m, 1)).ToList(), []));
            Cuentas.ConsultarAsync(Arg.Any<SolicitudDeConsultaDeCuentas>(), Arg.Any<CancellationToken>()).Returns(_ =>
                new RespuestaDelContratoDto<CuentasDePersonaDto>(ResultadoDelContrato.Answered, "", "",
                    new CuentasDePersonaDto(EstadoDeCartera.Active, EstadoDeCartera.Active, Persona.PublicId, Hoy, Cuentas_)));
            Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>()).Returns(_ =>
                new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "", "",
                    new ExigiblesDeCuentasDto(EstadoDeCartera.Active, EstadoDeCartera.Active, Exigibles, [])));
        }

        public ChannelPublishedConcept Publicar(ConceptKind clase, string? linea, bool conDestino = true, Action<ChannelPublishedConcept>? ajustar = null)
        {
            var c = new ChannelPublishedConcept { Kind = clase, LineCode = linea, DestinationId = conDestino ? Destino.Id : null };
            ajustar?.Invoke(c);
            Db.ChannelPublishedConcepts.Add(c);
            Db.SaveChanges();
            return c;
        }

        public ObligacionListadaDto Credito(string linea, params ModalidadDePago[] admitidas)
        {
            var o = new ObligacionListadaDto(Guid.NewGuid(), "CR-" + (Obligaciones_.Count + 123), linea, "Consumo", RolEnLaObligacion.Debtor,
                EstadoDeObligacion.Active, 12, 332_500m, 280_000m, 320_000m, 5_450_000m, 0m, 0m,
                admitidas.Length == 0 ? [ModalidadDePago.PastDue, ModalidadDePago.FreeAmount, ModalidadDePago.ExtraordinaryPrincipal, ModalidadDePago.FullPayoff] : admitidas,
                [], false, false);
            Obligaciones_.Add(o);
            return o;
        }

        public CuentaConsultadaDto Cuenta(NaturalezaDeCuenta naturaleza, ClaseDeCaptacion? clase, string linea)
        {
            var c = new CuentaConsultadaDto(Guid.NewGuid(), "AP-" + (Cuentas_.Count + 1), naturaleza, clase, linea, "Línea " + linea, RolEnLaCuenta.Holder,
                EstadoDeCuenta.Active, "Individual", [], new DisponibleDto(900_000m, 0m, 0m, 0m, 0m, 900_000m), true, false, false, []);
            Cuentas_.Add(c);
            return c;
        }

        public GetChannelPayablesQueryHandler Listar() => new(Db, Obligaciones, Cuentas, Consulta, Reloj, Auditoria);

        public SimulateChannelPaymentQueryHandler Simular() => new(Db, Obligaciones, Cuentas, Consulta, Reloj, Auditoria);
    }

    [Fact]
    public async Task Solo_se_ven_las_lineas_publicadas_con_los_valores_del_contrato()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, "CONSUMO");
        var visible = e.Credito("CONSUMO");
        e.Credito("VIVIENDA");

        var r = await e.Listar().Handle(new GetChannelPayablesQuery(e.Persona.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        var item = r.Value.Items.Should().ContainSingle().Subject;
        item.ConceptKey.Should().Be($"LOAN:{visible.ObligationPublicId}");
        item.Kind.Should().Be("Loan");
        // Los mismos valores que el listado del contrato de obligaciones, que es lo que ve la caja (SC-005).
        item.DueToday.Should().Be(visible.DueToday);
        item.PastDue.Should().Be(visible.PastDue);
        item.PayoffAmount.Should().Be(visible.PayoffAmount);
        item.CycleInstallment.Should().Be(visible.CycleInstallment);
        item.DaysPastDue.Should().Be(12);
        item.Balance.Should().Be(5_400_000m);
        item.DefaultInterest.Should().Be(12_500m);
        item.NextDueDate.Should().Be(new DateOnly(2026, 12, 15));
        item.DestinationPublicId.Should().Be(e.Destino.PublicId);
        item.Payable.Should().BeTrue();
        r.Value.AsOf.Should().Be(Hoy);
    }

    [Fact]
    public async Task Una_publicacion_sin_linea_cubre_todas_y_la_de_la_linea_manda()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, null, ajustar: c => c.AllowPayoff = true);
        e.Publicar(ConceptKind.Loan, "VIVIENDA", conDestino: false);
        e.Credito("CONSUMO");
        e.Credito("VIVIENDA");

        var r = await e.Listar().Handle(new GetChannelPayablesQuery(e.Persona.PublicId), default);

        r.Value.Items.Should().HaveCount(2);
        var consumo = r.Value.Items.Single(i => i.LineCode == "CONSUMO");
        consumo.Modes.Payoff.Should().BeTrue();
        consumo.Payable.Should().BeTrue();
        // Sin destino la línea es sólo de consulta (FR-004).
        var vivienda = r.Value.Items.Single(i => i.LineCode == "VIVIENDA");
        vivienda.DestinationPublicId.Should().BeNull();
        vivienda.Payable.Should().BeFalse();
        vivienda.Modes.Payoff.Should().BeFalse();
    }

    [Fact]
    public async Task Las_modalidades_son_las_publicadas_que_admite_Cartera()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, "CONSUMO", ajustar: c =>
        {
            c.AllowPartial = true;
            c.PartialMinimum = 20_000m;
            c.AllowPrepayment = true;
            c.DefaultPrepaymentChoice = ChannelPrepaymentChoice.ReduceInstallment;
            c.AllowPayoff = true;
        });
        e.Credito("CONSUMO", ModalidadDePago.PastDue, ModalidadDePago.FreeAmount, ModalidadDePago.ExtraordinaryPrincipal);

        var r = await e.Listar().Handle(new GetChannelPayablesQuery(e.Persona.PublicId), default);

        var modos = r.Value.Items.Single().Modes;
        modos.Due.Should().BeTrue();
        modos.Partial!.Minimum.Should().Be(20_000m);
        modos.Prepayment!.DefaultChoice.Should().Be("ReduceInstallment");
        modos.Payoff.Should().BeFalse("Cartera no admite la cancelación de ese crédito");
    }

    [Fact]
    public async Task Aportes_y_ahorro_contractual_con_su_cuota_y_sin_ahorro_a_la_vista()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Contribution, null);
        e.Publicar(ConceptKind.ContractualSaving, null, ajustar: c => c.AllowPartial = true);
        e.Publicar(ConceptKind.ProgrammedSaving, null);
        var aportes = e.Cuenta(NaturalezaDeCuenta.Contribution, null, "APORTES");
        e.Cuenta(NaturalezaDeCuenta.Deposit, ClaseDeCaptacion.Contractual, "NAVIDENO");
        e.Cuenta(NaturalezaDeCuenta.Deposit, ClaseDeCaptacion.Demand, "VISTA");
        e.Exigibles.Add(new ExigibleDeCuentaDto(e.Persona.PublicId, aportes.AccountPublicId, aportes.Number, NaturalezaDeCuenta.Contribution, null, "APORTES",
            Guid.NewGuid(), "2026-11", new DateOnly(2026, 11, 30), 50_000m, 50_000m, true, "Contribution", "APORTE", 1));
        e.Exigibles.Add(new ExigibleDeCuentaDto(e.Persona.PublicId, aportes.AccountPublicId, aportes.Number, NaturalezaDeCuenta.Contribution, null, "APORTES",
            Guid.NewGuid(), "2026-12", new DateOnly(2026, 12, 30), 50_000m, 50_000m, true, "Contribution", "APORTE", 2));

        var r = await e.Listar().Handle(new GetChannelPayablesQuery(e.Persona.PublicId), default);

        r.Value.Items.Select(i => i.Kind).Should().BeEquivalentTo(["Contribution", "ContractualSaving"]);
        var a = r.Value.Items.Single(i => i.Kind == "Contribution");
        a.ConceptKey.Should().Be($"ACCOUNT:{aportes.AccountPublicId}");
        a.Balance.Should().Be(900_000m);
        a.DueToday.Should().Be(50_000m, "sólo la cuota de noviembre ya venció");
        a.Installment!.Period.Should().Be("2026-11");
        a.NextDueDate.Should().Be(new DateOnly(2026, 11, 30));
        a.Modes.Due.Should().BeTrue();
        a.PayoffAmount.Should().BeNull();
        var contractual = r.Value.Items.Single(i => i.Kind == "ContractualSaving");
        contractual.Modes.Due.Should().BeFalse("no tiene cuotas pendientes");
        contractual.Modes.Partial.Should().NotBeNull();
        contractual.Payable.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_conceptos_publicados_no_pregunta_a_los_modulos()
    {
        var e = new Escenario();
        e.Credito("CONSUMO");

        var r = await e.Listar().Handle(new GetChannelPayablesQuery(e.Persona.PublicId), default);

        r.Value.Items.Should().BeEmpty();
        await e.Obligaciones.DidNotReceive().ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>());
        await e.Cuentas.DidNotReceive().ConsultarAsync(Arg.Any<SolicitudDeConsultaDeCuentas>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Persona_inhabilitada_responde_igual_que_inexistente()
    {
        var e = new Escenario(habilitada: false);
        e.Publicar(ConceptKind.Loan, null);

        var inhabilitada = await e.Listar().Handle(new GetChannelPayablesQuery(e.Persona.PublicId), default);
        var inexistente = await e.Listar().Handle(new GetChannelPayablesQuery(Guid.NewGuid()), default);

        inhabilitada.Error.Should().Be(inexistente.Error);
        inhabilitada.Error.Code.Should().Be(PersonaDelCanal.NoHabilitada);
    }

    [Fact]
    public async Task Simular_un_concepto_no_publicado_no_llega_a_Cartera()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, "CONSUMO");
        var oculto = e.Credito("VIVIENDA");

        var r = await e.Simular().Handle(new SimulateChannelPaymentQuery(e.Persona.PublicId, $"LOAN:{oculto.ObligationPublicId}", "Due", 100_000m), default);

        r.Error.Code.Should().Be(SimulateChannelPaymentQueryHandler.ConceptoNoEncontrado);
        await e.Obligaciones.DidNotReceive().SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Payoff", 5_450_000, SimulateChannelPaymentQueryHandler.ModalidadNoAdmitida)]
    [InlineData("Partial", 10_000, SimulateChannelPaymentQueryHandler.ValorInvalido)]
    [InlineData("Due", 1_000.5, SimulateChannelPaymentQueryHandler.ValorInvalido)]
    [InlineData("Lunar", 100_000, SimulateChannelPaymentQueryHandler.ModalidadNoAdmitida)]
    public async Task Simular_exige_modalidad_admitida_y_valor_valido(string modalidad, double valor, string error)
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, "CONSUMO", ajustar: c =>
        {
            c.AllowPartial = true;
            c.PartialMinimum = 20_000m;
        });
        var o = e.Credito("CONSUMO");

        var r = await e.Simular().Handle(new SimulateChannelPaymentQuery(e.Persona.PublicId, $"LOAN:{o.ObligationPublicId}", modalidad, (decimal)valor), default);

        r.Error.Code.Should().Be(error);
    }

    [Fact]
    public async Task El_abono_a_capital_simula_con_la_eleccion_y_devuelve_las_alternativas()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, "CONSUMO", ajustar: c =>
        {
            c.AllowPrepayment = true;
            c.DefaultPrepaymentChoice = ChannelPrepaymentChoice.ReduceTerm;
        });
        var o = e.Credito("CONSUMO");
        SimulacionDePagoDto Sim(EleccionDeAbono eleccion, int cuotas) => new("sim", o.ObligationPublicId, ModalidadDePago.ExtraordinaryPrincipal, 1_000_000m, Hoy,
            eleccion, 1_000_000m, 0m, 0m, 0m, [new LineaDeDistribucionDto(null, ConceptoDeObligacion.Principal, 1_000_000m)],
            new SaldosDeObligacionDto(4_000_000m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 4_050_000m, 0),
            new PlanResumidoDto(cuotas, 280_000m, new DateOnly(2026, 12, 15), null,
                Enumerable.Range(1, cuotas).Select(n => new CuotaDelPlanDto(n, new DateOnly(2026, 12, 15).AddMonths(n - 1), 0m, 0m, 0m, 0m, 280_000m)).ToList()),
            [], []);
        e.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "", "",
                Sim(EleccionDeAbono.ReduceTerm, 15) with { Alternatives = [Sim(EleccionDeAbono.ReduceTerm, 15), Sim(EleccionDeAbono.ReduceInstallment, 20)] }));

        var r = await e.Simular().Handle(new SimulateChannelPaymentQuery(e.Persona.PublicId, $"LOAN:{o.ObligationPublicId}", "Prepayment", 1_000_000m), default);

        r.IsSuccess.Should().BeTrue();
        await e.Obligaciones.Received(1).SimularAsync(Arg.Is<SolicitudDeSimulacion>(s =>
            s.Modality == ModalidadDePago.ExtraordinaryPrincipal && s.Choice == EleccionDeAbono.ReduceTerm && s.Amount == 1_000_000m && s.ValueDate == Hoy),
            Arg.Any<CancellationToken>());
        r.Value.NewPlan!.RemainingInstallments.Should().Be(15);
        r.Value.BalancesAfter.PayoffAmount.Should().Be(4_050_000m);
        r.Value.Alternatives.Select(a => a.Choice).Should().Equal("ReduceTerm", "ReduceInstallment");
        r.Value.Alternatives[1].NewPlan!.RemainingInstallments.Should().Be(20);
    }

    [Fact]
    public async Task Un_rechazo_de_Cartera_llega_con_su_mensaje()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Loan, "CONSUMO");
        var o = e.Credito("CONSUMO");
        e.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Rejected, "Lending.Payment.Blocked", "El crédito está bloqueado.", null));

        var r = await e.Simular().Handle(new SimulateChannelPaymentQuery(e.Persona.PublicId, $"LOAN:{o.ObligationPublicId}", "Due", 332_500m), default);

        r.Error.Code.Should().Be(SimulateChannelPaymentQueryHandler.Rechazada);
        r.Error.Message.Should().Be("El crédito está bloqueado.");
    }

    [Fact]
    public async Task La_cuota_de_aportes_simula_como_consignacion_por_cuotas()
    {
        var e = new Escenario();
        e.Publicar(ConceptKind.Contribution, null);
        var c = e.Cuenta(NaturalezaDeCuenta.Contribution, null, "APORTES");
        e.Exigibles.Add(new ExigibleDeCuentaDto(e.Persona.PublicId, c.AccountPublicId, c.Number, NaturalezaDeCuenta.Contribution, null, "APORTES",
            Guid.NewGuid(), "2026-12", new DateOnly(2026, 12, 30), 50_000m, 50_000m, true, "Contribution", "APORTE", 1));
        var disp = new DisponibleDto(950_000m, 0m, 0m, 0m, 0m, 950_000m);
        e.Cuentas.SimularAsync(Arg.Any<SolicitudDeSimulacionDeCuenta>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<SimulacionDeCuentaDto>(ResultadoDelContrato.Answered, "", "",
                new SimulacionDeCuentaDto("s", true, c.Available, disp, null, [], [], null, false, [new DistribucionEnCuotaDto(Guid.NewGuid(), "2026-12", 50_000m)], [])));

        var r = await e.Simular().Handle(new SimulateChannelPaymentQuery(e.Persona.PublicId, $"ACCOUNT:{c.AccountPublicId}", "Due", 50_000m), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.BalancesAfter.Balance.Should().Be(950_000m);
        r.Value.Distribution.Should().ContainSingle().Which.Period.Should().Be("2026-12");
        await e.Cuentas.Received(1).SimularAsync(Arg.Is<SolicitudDeSimulacionDeCuenta>(s =>
            s.Operation == "Deposit" && s.Modality == ModalidadDeConsignacion.Installments && s.MeansClass == ClaseDeMedioDeRecaudo.Transfer),
            Arg.Any<CancellationToken>());
    }
}
