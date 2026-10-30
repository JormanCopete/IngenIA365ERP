using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Sustitutos;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 013, T0147 (C107, C61, C60; api.md §19.1, §19.3; data-model §29–§30): el camino mínimo de la activación para una cooperativa sin
/// cartera que migrar. <see cref="CreateMigrationLoadCommand"/> deja el borrador con su corte; <see cref="ApplyMigrationLoadCommand"/> sobre una
/// carga sin partes la deja <c>Applied</c> con conteos en cero, cuadrada y conciliada, con su plazo y el informativo
/// <c>CargaDeCarteraAplicada</c>; <see cref="ActivateLendingCommand"/> rechaza la fecha que no es corte + 1, el acta que no corresponde y los
/// requisitos que faltan (los dobles <c>TST</c>/<c>TES</c> cuentan fuera de producción y no en producción) y, si todo está, escribe la
/// activación, <c>LND_Setup</c>, la carga <c>Activated</c>, el destino <c>Lending</c> habilitado y <c>CarteraActivada</c>, una sola vez; la
/// misma clave y el mismo cuerpo devuelven la misma respuesta. InMemory con reloj fijo (hoy = 2026-12-01).
/// </summary>
public class ActivacionSinCarteraPreviaTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Corte = Hoy.AddDays(-1);
    private static readonly DateOnly Siempre = new(1900, 1, 1);
    private static readonly Guid Central = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly ICerrojoDeCartera _cerrojo = Substitute.For<ICerrojoDeCartera>();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly EstadoDeLaOperacion _estado = new();

    public ActivacionSinCarteraPreviaTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
        _ambiente.EsProduccion.Returns(false);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Central,
            "Jefa de cartera de ensayo", "jefa@coop.test", ExecutionChannel.Web, "POST /api/lending/activation", "10.0.0.7", null));

        _db.LendingSetups.Add(new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica, CreatedBy = "SYSTEM" });
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
        // El destino de Cartera, sembrado inhabilitado por PlataformaParaCartera (C35).
        Participante("CAR", IntegrationParticipantClass.Lending, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Lending,
            habilitado: false, ("VentaACreditoRegistrada", 2));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void Participante(string codigo, IntegrationParticipantClass clase, IntegrationParticipantRole rol, string destino, bool habilitado,
        params (string Contrato, short Version)[] adopciones)
    {
        var p = new IntegrationParticipant
        {
            Code = codigo, Class = clase, Role = rol, Destination = destino, IsEnabled = habilitado, ValidFrom = Siempre, Reason = "Semilla",
            RegisteredByKind = ActorKind.Process, RegisteredByName = "Prueba",
        };
        foreach (var (contrato, version) in adopciones)
            p.Adoptions.Add(new IntegrationParticipantAdoption { Participant = p, ContractName = contrato, Version = version, ValidFrom = Siempre, Reason = "Semilla" });
        _db.IntegrationParticipants.Add(p);
    }

    /// <summary>Recaudo (<c>REC</c>) y Tesorería (<c>TES</c>) reales; hasta la feature 015 (T0308) eran los dobles de clase <c>Test</c>.</summary>
    private void RegistrarDobles()
    {
        Participante("REC", IntegrationParticipantClass.Receipts, IntegrationParticipantRole.ObligationsCollector, string.Empty, habilitado: true,
            (ContratosDeIntegracion.ProveedorDeObligaciones, 1));
        Participante("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, habilitado: true,
            (OrdenDeGiroEmitidaV1.Type, 1), (AnulacionDeOrdenDeGiroV1.Type, 1));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    // ----------------------------------------------------------------------------------------------- armado --

    private ContabilidadParaCarteraProvisional Contabilidad() => new(_db);

    private EmisionDeCartera Emision()
    {
        var contabilidad = Contabilidad();
        return new EmisionDeCartera(_db, new EmisorDeMensajes(_db, _actor, _reloj), new FechaContable(contabilidad),
            new ModoDePasoDeCartera(_db, contabilidad), _actor);
    }

    private RequisitosDeActivacion Requisitos() =>
        new(new RegistroDeIntegracion(_db, [new TesoreriaDePrueba()], _ambiente), Contabilidad(), _ambiente);

    private CreateMigrationLoadCommandHandler Crear() => new(_db, new NumeradorDeCartera(_db, _cerrojo), _actor, _reloj);

    private ApplyMigrationLoadCommandHandler Aplicar() => new(_db, Emision(), new LectorDeParametros(_db), _actor, _reloj);

    private ActivateLendingCommandHandler Activar(bool conCierreBase = false) =>
        new(_db, _cerrojo, Requisitos(), new EscritorDelRegistroDeIntegracion(_db, _ambiente, _actor, _reloj), Emision(), new PuntoDeActaCerrado(_db), _actor, _reloj,
            conCierreBase ? new IngenIA365ERP.Application.Lending.Risk.CierreBase(_db, new IngenIA365ERP.Application.Common.NationalValues.LectorDeDocumentoNormativo(_db), _reloj) : null);

    private async Task<MigrationLoadDto> CargaAplicadaAsync()
    {
        var carga = (await Crear().Handle(new CreateMigrationLoadCommand(Corte, "ensayo"), default)).Value;
        var aplicada = await Aplicar().Handle(new ApplyMigrationLoadCommand(carga.PublicId, "Sin cartera que migrar"), default);
        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? aplicada.Error.Message : string.Empty);
        _db.ChangeTracker.Clear();
        return carga;
    }

    /// <summary>El acta de activación: un órgano, una sesión y un punto, con lo que la prueba quiera cambiar.</summary>
    private Guid Acta(Guid sujeto, SessionStatus estado = SessionStatus.Closed, SessionItemKind clase = SessionItemKind.Decision,
        CreditDecisionOutcome? resultado = CreditDecisionOutcome.Approve, string tipoDeSujeto = LendingDecisionSubjects.ActivationDifferences)
    {
        var organo = new GoverningBody { Code = "CONSEJO", Name = "Consejo de administración", Kind = GoverningBodyKind.BoardOfDirectors, MinutesPrefix = "CA-", CreatedBy = "test" };
        _db.GoverningBodies.Add(organo);
        _db.SaveChanges();
        var sesion = new Session { GoverningBodyId = organo.Id, Kind = SessionKind.Extraordinary, Mode = SessionMode.InPerson, HeldOn = Corte, Status = estado, CreatedBy = "test" };
        _db.LendingSessions.Add(sesion);
        _db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = clase, Rule = VotingRule.Majority, SubjectType = tipoDeSujeto, SubjectPublicId = sujeto,
            Title = "Activación de Cartera", Outcome = resultado, CreatedBy = "test",
        };
        _db.SessionItems.Add(punto);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    private static ActivateLendingCommand Orden(Guid carga, Guid punto, DateOnly? fecha = null) =>
        new(carga, fecha ?? Hoy, null, punto, AcknowledgeWarnings: false, "Arranque de Cartera") { OperationKey = Guid.NewGuid() };

    // ------------------------------------------------------------------------------------------- la carga --

    [Fact]
    public async Task Crear_deja_un_borrador_con_su_corte_y_un_segundo_reemplaza_al_primero()
    {
        var primera = (await Crear().Handle(new CreateMigrationLoadCommand(Corte, "ensayo 1"), default)).Value;
        var segunda = (await Crear().Handle(new CreateMigrationLoadCommand(Corte, "ensayo 2"), default)).Value;

        primera.Should().BeEquivalentTo(new { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Draft, Description = "ensayo 1" });
        segunda.Number.Should().Be(2);
        var guardadas = await _db.MigrationLoads.AsNoTracking().OrderBy(l => l.Number).ToListAsync();
        guardadas[0].Status.Should().Be(MigrationLoadStatus.Replaced);
        guardadas[0].ReplacedByLoadId.Should().Be(guardadas[1].Id);
        guardadas[1].Status.Should().Be(MigrationLoadStatus.Draft);
        guardadas[1].CreatedByUserId.Should().Be(7);
    }

    [Fact]
    public async Task Aplicar_una_carga_sin_partes_la_deja_cuadrada_conciliada_con_su_plazo_y_emite_CargaDeCarteraAplicada()
    {
        var carga = (await Crear().Handle(new CreateMigrationLoadCommand(Corte, null), default)).Value;

        var r = await Aplicar().Handle(new ApplyMigrationLoadCommand(carga.PublicId, "Sin cartera que migrar"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var guardada = await _db.MigrationLoads.AsNoTracking().SingleAsync();
        guardada.Should().BeEquivalentTo(new
        {
            Status = MigrationLoadStatus.Applied, LoanCount = 0, ChargedOffCount = 0, PersonCount = 0, IsBalanced = true, SalesReconciled = true,
            ActivationDeadline = (DateOnly?)Corte.AddDays(5), AppliedByUserId = (int?)7,
        }, o => o.ExcludingMissingMembers());
        var mensaje = await _db.IntegrationMessages.AsNoTracking().SingleAsync();
        mensaje.Type.Should().Be(CargaDeCarteraAplicadaV1.Type);
        mensaje.OriginPublicId.Should().Be(carga.PublicId);
        mensaje.Kind.Should().Be(IntegrationMessageKind.Informational);
        mensaje.OriginModule.Should().Be("CAR");
        (await _db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync()).Mode.Should().Be(DeliveryMode.Always);

        (await Aplicar().Handle(new ApplyMigrationLoadCommand(carga.PublicId, "otra vez"), default)).Error.Code.Should().Be("Lending.Migration.LoadNotDraft");
    }

    // ----------------------------------------------------------------------------------------- la activación --

    [Fact]
    public async Task Una_fecha_que_no_es_el_dia_siguiente_al_corte_responde_CutoffInvalid()
    {
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();

        var r = await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId), Hoy.AddDays(1)), default);

        r.Error.Code.Should().Be("Lending.Activation.CutoffInvalid");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { expected = Hoy });
    }

    [Fact]
    public async Task Sin_acta_o_con_un_punto_que_no_corresponde_responde_ItemMismatch()
    {
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();

        (await Activar().Handle(Orden(carga.PublicId, Guid.Empty), default)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId, estado: SessionStatus.Open)), default)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId, resultado: CreditDecisionOutcome.Deny)), default)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId, clase: SessionItemKind.Recommendation)), default)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await Activar().Handle(Orden(carga.PublicId, Acta(Guid.NewGuid())), default)).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        var otroSujeto = await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId, tipoDeSujeto: LendingDecisionSubjects.RiskPolicyVersion)), default);
        otroSujeto.Error.Code.Should().Be("Lending.Session.ItemMismatch");
        ((ErrorConDatos)otroSujeto.Error).Data.Should().BeEquivalentTo(new
        {
            expectedSubject = LendingDecisionSubjects.ActivationDifferences, itemSubject = LendingDecisionSubjects.RiskPolicyVersion,
        }, o => o.ExcludingMissingMembers());
        (await _db.LendingActivations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_Recaudo_ni_Tesoreria_en_el_registro_faltan_esos_requisitos()
    {
        var carga = await CargaAplicadaAsync();

        var r = await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId)), default);

        r.Error.Code.Should().Be("Lending.Activation.RequirementsMissing");
        Faltantes(r.Error).Should().BeEquivalentTo([RequisitosDeActivacion.RecaudadorRegistrado, RequisitosDeActivacion.TesoreriaRegistrada]);
    }

    [Fact]
    public async Task En_produccion_los_dobles_no_cuentan()
    {
        // Feature 015, T0308: RegistrarDobles registra ahora a Recaudo y Tesorería reales; los dobles de la 013 (clase Test) se siembran aquí, a mano, y no
        // cuentan en ningún ambiente.
        Participante("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.ObligationsCollector, string.Empty, habilitado: true,
            (ContratosDeIntegracion.ProveedorDeObligaciones, 1));
        Participante("TES", IntegrationParticipantClass.Test, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, habilitado: true,
            (OrdenDeGiroEmitidaV1.Type, 1), (AnulacionDeOrdenDeGiroV1.Type, 1));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        var carga = await CargaAplicadaAsync();
        _ambiente.EsProduccion.Returns(true);

        var r = await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId)), default);

        Faltantes(r.Error).Should().BeEquivalentTo([RequisitosDeActivacion.RecaudadorRegistrado, RequisitosDeActivacion.TesoreriaRegistrada]);
    }

    [Fact]
    public async Task Con_el_mes_de_C_mas_1_cerrado_en_Contabilidad_activa_con_el_aviso_y_fecha_lo_que_emite_en_el_primer_mes_abierto()
    {
        // Decisión del dueño (2026-10-14): el mes de C+1 cerrado no limita, sólo informa.
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();
        var diciembre = await _db.AccountingPeriods.SingleAsync(p => p.Month == 12);
        diciembre.Status = PeriodStatus.Closed;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var r = await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Warnings.Should().ContainSingle(a => a.Code == RequisitosDeActivacion.AvisoMesContableCerrado)
            .Which.Detail.Should().Contain("2026-12").And.Contain("procede");
        (await _db.LendingActivations.AsNoTracking().SingleAsync()).RequirementsJson.Should().Contain(EstadosDeRequisito.NotApplicable);
        var activada = await _db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CarteraActivadaV1.Type);
        activada.OperationDate.Should().Be(new DateOnly(2027, 1, 1), "FechaContable lleva lo que emite la activación al primer mes siguiente al cerrado");
        (await _db.LendingSetups.AsNoTracking().SingleAsync()).ActivationDate.Should().Be(Hoy, "la activación sigue siendo C+1");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_sigue_faltando_AccountingMonthOpen()
    {
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();
        _db.AccountingSetups.RemoveRange(_db.AccountingSetups);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var r = await Activar().Handle(Orden(carga.PublicId, Acta(carga.PublicId)), default);

        Faltantes(r.Error).Should().Equal(RequisitosDeActivacion.MesContableAbierto);
    }

    [Fact]
    public async Task Activar_escribe_la_activacion_el_setup_la_carga_el_destino_y_CarteraActivada_una_sola_vez()
    {
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();
        var punto = Acta(carga.PublicId);

        var r = await Activar().Handle(Orden(carga.PublicId, punto), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        await _cerrojo.Received(1).TomarSetupAsync(true, Arg.Any<CancellationToken>());
        var activacion = await _db.LendingActivations.AsNoTracking().SingleAsync();
        activacion.PublicId.Should().Be(r.Value.ActivationPublicId);
        activacion.SessionItemId.Should().Be((await _db.SessionItems.AsNoTracking().SingleAsync(i => i.PublicId == punto)).Id);
        activacion.ActivationDate.Should().Be(Hoy);
        activacion.CutoffDate.Should().Be(Corte);
        activacion.IsProductionEnvironment.Should().BeFalse();
        activacion.CountsJson.Should().Contain("\"loans\":0");
        activacion.RequirementsJson.Should().Contain(RequisitosDeActivacion.TesoreriaRegistrada);

        var setup = await _db.LendingSetups.AsNoTracking().SingleAsync();
        setup.ActivationDate.Should().Be(Hoy);
        setup.ActivationId.Should().Be(activacion.Id);
        setup.ActivatedAt.Should().NotBeNull();
        (await _db.MigrationLoads.AsNoTracking().SingleAsync()).Status.Should().Be(MigrationLoadStatus.Activated);

        var destinos = await _db.IntegrationParticipants.AsNoTracking().Where(p => p.Code == "CAR").OrderBy(p => p.ValidFrom).ToListAsync();
        destinos.Should().HaveCount(2);
        destinos[0].ValidTo.Should().Be(Hoy.AddDays(-1));
        destinos[1].Should().BeEquivalentTo(new { IsEnabled = true, ValidFrom = Hoy, ValidTo = (DateOnly?)null }, o => o.ExcludingMissingMembers());

        var activada = await _db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CarteraActivadaV1.Type);
        activada.OriginPublicId.Should().Be(activacion.PublicId);
        activada.PublicId.Should().Be(activacion.ActivatedMessagePublicId);
        activada.OperationDate.Should().Be(Hoy);
        r.Value.Messages.Should().ContainSingle(m => m.Type == CarteraActivadaV1.Type);

        var segunda = await Activar().Handle(Orden(carga.PublicId, punto), default);
        segunda.Error.Code.Should().Be("Lending.Activation.AlreadyActivated");
        (await _db.LendingActivations.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// US9 (T0679, T0708; C107, T49): con la carga vacía la activación crea el cierre base sin líneas, fechado al corte y enlazado a la activación,
    /// y lo nombra en <c>CarteraActivada</c>: el primer cierre mensual tiene así su <c>PreviousClose</c>. Sin hechos ni partes.
    /// </summary>
    [Fact]
    public async Task Activar_crea_el_cierre_base_sin_lineas_y_lo_nombra_en_CarteraActivada()
    {
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();

        var r = await Activar(conCierreBase: true).Handle(Orden(carga.PublicId, Acta(carga.PublicId)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var activacion = await _db.LendingActivations.AsNoTracking().SingleAsync();
        var cierre = await _db.MonthlyCloses.AsNoTracking().SingleAsync();
        cierre.Should().BeEquivalentTo(new
        {
            Kind = MonthlyCloseKind.Base, PeriodYear = (short)Corte.Year, PeriodMonth = (byte)Corte.Month, Version = 1, CloseDate = Corte,
            ActivationId = (int?)activacion.Id, ProcessRunId = (int?)null, PreviousCloseId = (int?)null, LoanCount = 0, TotalIndividualImpairment = 0m,
        }, o => o.ExcludingMissingMembers());
        r.Value.BaseClosePublicId.Should().Be(cierre.PublicId);
        (await _db.MonthlyCloseLoans.CountAsync()).Should().Be(0);
        (await _db.AccountingFactParts.CountAsync()).Should().Be(0, "el base no emite hechos (FR-140)");
        var activada = await _db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CarteraActivadaV1.Type);
        activada.PayloadJson.Should().Contain($"\"baseClosePublicId\":\"{cierre.PublicId:D}\"");
    }

    [Fact]
    public async Task La_misma_clave_con_el_mismo_cuerpo_devuelve_la_misma_respuesta()
    {
        RegistrarDobles();
        var carga = await CargaAplicadaAsync();
        var orden = Orden(carga.PublicId, Acta(carga.PublicId));

        var primera = await ConClaveAsync(orden);
        var repetida = await ConClaveAsync(orden);

        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Message : string.Empty);
        repetida.IsSuccess.Should().BeTrue();
        repetida.Value.ActivationPublicId.Should().Be(primera.Value.ActivationPublicId);
        (await _db.LendingActivations.CountAsync()).Should().Be(1);
        (await _db.IntegrationMessages.CountAsync(m => m.Type == CarteraActivadaV1.Type)).Should().Be(1);
    }

    private async Task<Result<LendingActivationDto>> ConClaveAsync(ActivateLendingCommand orden)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IApplicationDbContext>(_db);
        servicios.AddSingleton(_actor);
        servicios.AddSingleton(_auditoria);
        servicios.AddSingleton(_estado);
        var behavior = new IdempotencyBehavior<ActivateLendingCommand, Result<LendingActivationDto>>(servicios.BuildServiceProvider(),
            NullLogger<IdempotencyBehavior<ActivateLendingCommand, Result<LendingActivationDto>>>.Instance);
        var r = await behavior.Handle(orden, _ => Activar().Handle(orden, default), default);
        _db.ChangeTracker.Clear();
        return r;
    }

    private static IReadOnlyList<string> Faltantes(Error error)
    {
        error.Code.Should().Be("Lending.Activation.RequirementsMissing");
        var datos = System.Text.Json.JsonSerializer.SerializeToElement(((ErrorConDatos)error).Data);
        return datos.GetProperty("missing").EnumerateArray().Select(m => m.GetProperty("code").GetString()!).ToList();
    }
}
