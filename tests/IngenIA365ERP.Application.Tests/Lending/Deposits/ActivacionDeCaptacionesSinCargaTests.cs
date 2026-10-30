using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Sustitutos;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, T051–T055 (R10, R21; FR-093, FR-116, FR-117; api.md §16): la activación <b>sin carga</b> de aportes y captaciones, sus requisitos y el
/// bloqueo por entrega, las fechas en <c>LND_Setup</c> y lo que leen de ellas el paso de captaciones del ciclo diario y la sección de captaciones del
/// cierre. InMemory con reloj fijo (hoy = 2026-12-01), créditos activados ese día, fuera de producción. Desde la decisión del dueño del 2026-10-18
/// (D14, D18) los requisitos se exigen igual en todos los ambientes y la activación pide los cuatro roles de captaciones.
/// </summary>
public class ActivacionDeCaptacionesSinCargaTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Siempre = new(1900, 1, 1);
    private static readonly DateTime Ahora = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly ICerrojoDeCartera _cerrojo = Substitute.For<ICerrojoDeCartera>();

    public ActivacionDeCaptacionesSinCargaTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(Ahora);
        _ambiente.EsProduccion.Returns(false);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(),
            "Jefa de captaciones de ensayo", "jefa@coop.test", ExecutionChannel.Web, "POST /api/lending/deposits/activation", "10.0.0.7", null));

        _db.LendingSetups.Add(new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica, ActivationDate = Hoy, CreatedBy = "SYSTEM" });
        _db.Branches.Add(new Branch { Name = "Principal", CreatedBy = "test" });
        _db.AccountingSetups.Add(new AccountingSetup { NiifGroup = 2, MovementLevel = 5, FirstFiscalYear = 2026, InitializedBy = "test", CreatedBy = "test" });
        var ejercicio = new FiscalYear { Year = 2026, CreatedBy = "test" };
        _db.FiscalYears.Add(ejercicio);
        for (byte mes = 1; mes <= 12; mes++)
        {
            var inicio = new DateOnly(2026, mes, 1);
            _db.AccountingPeriods.Add(new AccountingPeriod
            {
                FiscalYear = ejercicio, Month = mes, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1), Status = PeriodStatus.Open, CreatedBy = "test",
            });
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void Participante(string codigo, IntegrationParticipantClass clase, IntegrationParticipantRole rol, string destino,
        params (string Contrato, short Version)[] adopciones)
    {
        var p = new IntegrationParticipant
        {
            Code = codigo, Class = clase, Role = rol, Destination = destino, IsEnabled = true, IsTestDouble = clase == IntegrationParticipantClass.Test,
            ValidFrom = Siempre, Reason = "Semilla", RegisteredByKind = ActorKind.Process, RegisteredByName = "Prueba",
        };
        foreach (var (contrato, version) in adopciones)
            p.Adoptions.Add(new IntegrationParticipantAdoption { Participant = p, ContractName = contrato, Version = version, ValidFrom = Siempre, Reason = "Semilla" });
        _db.IntegrationParticipants.Add(p);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    /// <summary>Recaudo real con el contrato de cuentas y Tesorería real (feature 015, T0308: antes, el recaudador y la tesorería de prueba).</summary>
    private void RegistrarDobles(bool conContratoDeCuentas = true)
    {
        (string, short)[] adopciones = conContratoDeCuentas
            ? [(ContratosDeIntegracion.ProveedorDeObligaciones, 1), (ContratosDeIntegracion.ProveedorDeCuentas, 1)]
            : [(ContratosDeIntegracion.ProveedorDeObligaciones, 1)];
        Participante("REC", IntegrationParticipantClass.Receipts, IntegrationParticipantRole.ObligationsCollector, string.Empty, adopciones);
        Participante("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury,
            (OrdenDeGiroEmitidaV1.Type, 1), (OrdenDeGiroEmitidaV2.Type, 2));
    }

    /// <summary>Los cuatro roles de captaciones y aportes (D18); con <paramref name="faltantes"/> se omiten los primeros.</summary>
    private void RolesDeCaptaciones(int faltantes = 0)
    {
        foreach (var plantilla in PerfilesSugeridos.DeCaptaciones().Skip(faltantes))
            _db.Roles.Add(new Role { Code = CreateSuggestedLendingRolesCommand.CodigoDeRol(plantilla.Key), Name = plantilla.Actor, CreatedBy = "test" });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private ContabilidadParaCarteraProvisional Contabilidad() => new(_db);

    private EmisionDeCartera Emision()
    {
        var contabilidad = Contabilidad();
        return new EmisionDeCartera(_db, new EmisorDeMensajes(_db, _actor, _reloj), new FechaContable(contabilidad),
            new ModoDePasoDeCartera(_db, contabilidad), _actor);
    }

    private RequisitosDeActivacionDeCaptaciones Requisitos(EntregaDeCartera? entrega = EntregaDeCartera.K11) =>
        new(_db, new RegistroDeIntegracion(_db, [new TesoreriaDePrueba()], _ambiente), Contabilidad(), _ambiente, entregaDesplegada: entrega);

    private ActivacionDeCaptacionesSinCarga Activacion(EntregaDeCartera? entrega = EntregaDeCartera.K11) =>
        new(_db, _cerrojo, Requisitos(entrega), Emision(), _actor, _reloj);

    // ------------------------------------------------------------------------------------------------- bloqueos --

    [Fact]
    public async Task Sin_la_entrega_que_la_trae_responde_DeliveryNotAvailable()
    {
        RegistrarDobles();

        var captaciones = await Activacion(EntregaDeCartera.K7).ActivarAsync(LineNature.Deposit, null, Hoy, false, "ensayo", default);
        var aportes = await Activacion(EntregaDeCartera.K6b).ActivarAsync(LineNature.Contribution, null, Hoy, false, "ensayo", default);

        captaciones.Error.Code.Should().Be("Lending.Savings.DeliveryNotAvailable", "sin K8 no hay captación (api.md §1.3)");
        aportes.Error.Code.Should().Be("Lending.Savings.DeliveryNotAvailable", "sin K7 no hay aportes");
        RequisitosDeActivacionDeCaptaciones.EntregaQueLaTrae(LineNature.Deposit).Should().Be(EntregaDeCartera.K8);
        (await _db.LendingActivations.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Los_creditos_van_primero_y_sin_carga_no_hay_carga_ni_fecha_futura()
    {
        RegistrarDobles();
        var setup = await _db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        await _db.SaveChangesAsync();

        (await Activacion().ActivarAsync(LineNature.Contribution, null, Hoy, false, "ensayo", default)).Error.Code
            .Should().Be("Lending.Activation.LoansFirst");

        setup.ActivationDate = Hoy;
        await _db.SaveChangesAsync();
        (await Activacion().ActivarAsync(LineNature.Contribution, null, Hoy.AddDays(-1), false, "ensayo", default)).Error.Code
            .Should().Be("Lending.Activation.LoansFirst", "antes que los créditos tampoco");
        (await Activacion().ActivarAsync(LineNature.Contribution, Guid.NewGuid(), Hoy, false, "ensayo", default)).Error.Code
            .Should().Be("Lending.Activation.LoadNotAvailable", "la activación con carga llega con US9");
        (await Activacion().ActivarAsync(LineNature.Contribution, null, Hoy.AddDays(1), false, "ensayo", default)).Error.Code
            .Should().Be("Lending.Activation.CutoffInvalid");
    }

    [Fact]
    public async Task Sin_recaudador_con_el_contrato_de_cuentas_faltan_requisitos()
    {
        RegistrarDobles(conContratoDeCuentas: false);

        var r = await Activacion().ActivarAsync(LineNature.Contribution, null, Hoy, false, "ensayo", default);

        r.Error.Code.Should().Be("Lending.Activation.RequirementsMissing");
        r.Error.Message.Should().Contain("contrato de cuentas");
    }

    // ---------------------------------------------------------------------------------------------- la activación --

    [Fact]
    public async Task Activa_aportes_sin_carga_una_vez_con_su_fila_su_fecha_y_su_informativo()
    {
        RegistrarDobles();
        RolesDeCaptaciones();

        var r = await Activacion().ActivarAsync(LineNature.Contribution, null, Hoy, true, "Cooperativa sin historia de aportes", default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Nature.Should().Be("Contribution");
        r.Value.Accounts.Should().Be(0, "sin carga no nace ninguna cuenta");
        r.Value.Messages.Should().ContainSingle(m => m.Type == CaptacionesActivadasV1.Type);
        r.Value.Requirements.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.CargaAplicada && q.Status == EstadosDeRequisito.NotApplicable);
        r.Value.Requirements.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.EntregasCompletas && q.Status == EstadosDeRequisito.Met);
        r.Value.Requirements.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.RolesDeCaptacionesCreados && q.Status == EstadosDeRequisito.Met,
            "los cuatro de captaciones; los trece del crédito no se piden aquí (D18)");
        r.Value.Warnings.Should().Contain(a => a.Code == RequisitosDeActivacionDeCaptaciones.ConceptosDeNominaListos);

        var setup = await _db.LendingSetups.AsNoTracking().SingleAsync();
        setup.ContributionsActivationDate.Should().Be(Hoy);
        setup.ContributionsActivatedAt.Should().Be(Ahora);
        setup.DepositsActivationDate.Should().BeNull("cada naturaleza se activa aparte");
        var activacion = await _db.LendingActivations.AsNoTracking().SingleAsync(a => a.Id == setup.ContributionsActivationId);
        activacion.Nature.Should().Be(LineNature.Contribution);
        activacion.MigrationLoadId.Should().BeNull("sin carga (FR-117)");
        activacion.SessionItemId.Should().BeNull("sin carga no hay diferencias que aprobar en acta");
        activacion.CutoffDate.Should().Be(Hoy.AddDays(-1), "corte + 1 = la fecha elegida");
        (await _db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CaptacionesActivadasV1.Type)).PayloadJson.Should().Contain("\"nature\":\"Contribution\"");

        var otra = await Activacion().ActivarAsync(LineNature.Contribution, null, Hoy, true, "otra vez", default);
        otra.Error.Code.Should().Be("Lending.Activation.AlreadyActivated");

        var fechas = await ActivacionesDeCaptacion.LeerAsync(_db, default);
        fechas.RigeEn(LineNature.Contribution, Hoy).Should().BeTrue();
        fechas.DeCaptacionEn(Hoy).Should().Equal(LineNature.Contribution);
    }

    [Fact]
    public async Task En_produccion_las_captaciones_exigen_el_perfil_que_autoriza_captar()
    {
        _ambiente.EsProduccion.Returns(true);
        Participante("REC", IntegrationParticipantClass.Receipts, IntegrationParticipantRole.ObligationsCollector, string.Empty,
            (ContratosDeIntegracion.ProveedorDeCuentas, 1));

        var evaluacion = await Requisitos().EvaluarAsync(LineNature.Deposit, Hoy, Hoy, default);

        evaluacion.Faltantes.Select(f => f.Code).Should().Contain([RequisitosDeActivacionDeCaptaciones.TesoreriaRegistrada]);
        evaluacion.Requisitos.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.RecaudadorDeCuentasRegistrado && q.Met);
        evaluacion.Requisitos.Should().Contain(q => q.Code == RequisitosDeActivacionDeCaptaciones.PerfilAutorizaClases,
            "sin perfil regulatorio que lo evalúe no se puede afirmar nada (lo inyecta la DI)");
        (await Requisitos(EntregaDeCartera.K9).EvaluarAsync(LineNature.Contribution, Hoy, Hoy, default)).Faltantes.Select(f => f.Code)
            .Should().Contain(RequisitosDeActivacionDeCaptaciones.EntregasCompletas, "en producción, K7 a K11 completas (Q1)");
    }

    [Fact]
    public async Task Fuera_de_produccion_lo_que_falta_bloquea_igual_y_cuentan_Recaudo_y_Tesoreria_reales()
    {
        // Decisión del dueño del 2026-10-18 (research §13 D14): ningún requisito se dispensa fuera de producción (antes quedaban Waived, E32).
        RegistrarDobles();
        RolesDeCaptaciones(faltantes: 1);

        var evaluacion = await Requisitos(EntregaDeCartera.K9).EvaluarAsync(LineNature.Contribution, Hoy, Hoy, default);

        evaluacion.Requisitos.Should().NotContain(q => q.Status == EstadosDeRequisito.Waived);
        evaluacion.Faltantes.Select(f => f.Code).Should().BeEquivalentTo(
            [RequisitosDeActivacionDeCaptaciones.EntregasCompletas, RequisitosDeActivacionDeCaptaciones.RolesDeCaptacionesCreados],
            "sin K11 desplegada y con un rol de captaciones sin crear no se activa en ningún ambiente; Recaudo y Tesorería reales cuentan");
        (await Activacion(EntregaDeCartera.K9).ActivarAsync(LineNature.Contribution, null, Hoy, true, "ensayo", default)).Error.Code
            .Should().Be("Lending.Activation.RequirementsMissing");
    }

    // ------------------------------------------------------------------------------------ ciclo diario y cierre --

    [Fact]
    public async Task El_paso_de_captaciones_solo_corre_con_una_naturaleza_activada_y_anota_las_cuentas_que_fallan()
    {
        var corrida = new LendingProcessRun { Kind = LendingProcessKind.DailyCycle, ProcessDate = Hoy, RequestedAt = Ahora };
        var subpaso = new SubpasoQueFallaLaSegunda();
        var paso = new PasoDeCaptaciones(_db, new ParametrosDeLaTandaDeCartera { TamanoDeTanda = 2 }, [subpaso]);

        (await paso.EjecutarAsync(corrida, Hoy, default)).Corrio.Should().BeFalse("sin aportes ni captaciones activados no corre");
        subpaso.Tandas.Should().Be(0);

        var setup = await _db.LendingSetups.SingleAsync();
        setup.ContributionsActivationDate = Hoy;
        await _db.SaveChangesAsync();
        var titular = new Person { FirstName = "Ana", LastName = "Ruiz", TaxId = "900", IdType = "CC" };
        _db.People.Add(titular);
        _db.CreditLines.Add(CreditLine.Nueva("APO", "Aportes", null, LineNature.Contribution));
        _db.SaveChanges();
        var linea = _db.CreditLines.Local.Single().Id;
        var cuentas = Enumerable.Range(1, 3).Select(i =>
        {
            var c = MemberAccount.Abrir(linea, LineNature.Contribution, null, titular.Id, 1, HandlingMode.Indistinct, Hoy, 7, Ahora);
            c.Number = $"AP-{i}";
            _db.MemberAccounts.Add(c);
            return c;
        }).ToList();
        _db.SaveChanges();

        (await paso.EjecutarAsync(corrida, Hoy.AddDays(-1), default)).Corrio.Should().BeFalse("la activación rige desde su fecha");
        var resultado = await paso.EjecutarAsync(corrida, Hoy, default);

        resultado.Should().Be(new ResultadoDelPasoDeCaptaciones(true, 3, 2, 1), "tres cuentas en tandas de dos; la segunda falla");
        corrida.CurrentStep.Should().Be(PasosDelCiclo.Captaciones);
        var error = await _db.LendingProcessRunErrors.AsNoTracking().SingleAsync();
        error.MemberAccountId.Should().Be(cuentas[1].Id);
        error.LoanId.Should().BeNull();
        error.Step.Should().Be("Captaciones");
        error.ErrorCode.Should().Be("Lending.Savings.Prueba");

        var seccion = await new SeccionDeCaptacionesDelCierre(_db).CerrarAsync(new DateOnly(2026, 12, 31), default);
        seccion.Value.Aplica.Should().BeTrue();
        seccion.Value.Naturalezas.Should().Equal(LineNature.Contribution);
        (await new SeccionDeCaptacionesDelCierre(_db).CerrarAsync(new DateOnly(2026, 11, 30), default)).Value.Aplica
            .Should().BeFalse("en noviembre todavía no había aportes activados");
    }

    private sealed class SubpasoQueFallaLaSegunda : ISubpasoDeCaptaciones
    {
        private int _vistas;

        public int Tandas { get; private set; }

        public string Nombre => "Prueba";

        public Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
        {
            Tandas++;
            var fallos = new List<FalloDeCuenta>();
            foreach (var cuenta in cuentas)
                if (++_vistas == 2) fallos.Add(new FalloDeCuenta(cuenta, "Lending.Savings.Prueba", "La cuenta no se pudo procesar."));
            return Task.FromResult<IReadOnlyList<FalloDeCuenta>>(fallos);
        }
    }
}
