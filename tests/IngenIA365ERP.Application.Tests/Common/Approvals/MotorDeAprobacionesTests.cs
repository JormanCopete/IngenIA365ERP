using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Approvals.ListMyPendingApprovals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Approvals;

/// <summary>
/// T015 (feature 012; decisiones-transversales T33, T34; contracts/api.md §15.2; data-model §21): el motor de
/// aprobaciones con <c>ILimitesPorPermiso</c>, <c>IActorActual</c> e <c>IAlcanceDeInventario</c> falsos y una fuente
/// de prueba. InMemory con reloj fijo. La segregación compara <c>SEC_Users.Id</c> del actor.
/// </summary>
public class MotorDeAprobacionesTests
{
    private const string Ajuste = "Inventory.Adjustments.Approve";
    private const string Supervisor = "Inventory.Approvals.Supervisor";
    private const string Gerencia = "Inventory.Approvals.Management";
    private const string Huella = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const int Creador = 10;
    private const int Cajero = 11;
    private const int Contador = 12;
    private const int Aprobador1 = 20;
    private const int Aprobador2 = 21;

    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 10, 5);
    private static readonly Guid TipoAjuste = Guid.NewGuid();
    private static readonly Guid Bodega = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly ILimitesPorPermiso _limites = Substitute.For<ILimitesPorPermiso>();
    private readonly IAutoridadDeOtroAprobador _otro = Substitute.For<IAutoridadDeOtroAprobador>();
    private readonly IAvisosDeAprobacion _avisos = Substitute.For<IAvisosDeAprobacion>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly FuenteDePrueba _fuente = new();

    public MotorDeAprobacionesTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        ComoUsuario(Cajero);
    }

    private void ComoUsuario(int? userId) =>
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(),
            $"usuario{userId}@coop.co", $"usuario{userId}@coop.co", ExecutionChannel.Web, "POST /api/inventory/approvals", "10.0.0.1", null));

    private MotorDeAprobaciones Motor() =>
        new(_db, _actor, _permisos, _alcance, _limites, _otro, _avisos, new VistaDeSolicitudes(_db, [_fuente]), _reloj);

    private async Task<ApprovalPolicy> PoliticaAsync(DateOnly desde, DateOnly? hasta, Guid? tipo, params (int Orden, decimal Umbral, string Permiso)[] niveles)
    {
        var p = new ApprovalPolicy
        {
            Subject = ApprovalSubjects.DocumentConfirmation,
            DocumentTypePublicId = tipo,
            PolicyKey = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, tipo),
            Version = 1,
            ValidFrom = desde,
            ValidTo = hasta,
            Reason = "Comité",
            Levels = niveles.Select(n => new ApprovalPolicyLevel { Order = (byte)n.Orden, Threshold = n.Umbral, PermissionCode = n.Permiso }).ToList(),
        };
        _db.ApprovalPolicies.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private static SolicitudDeAprobacion Pedido(decimal monto, DateOnly? fecha = null, Guid? bodega = null, string? permisoLimitado = null,
        params int[] participantes) =>
        new(ApprovalSubjects.DocumentConfirmation, FuenteDePrueba.Tipo, Guid.NewGuid(), "AJ-000123", TipoAjuste, bodega, null,
            monto, fecha ?? Hoy, Creador, participantes, Huella, permisoLimitado);

    /// <summary>Crea y guarda una solicitud pedida por el cajero.</summary>
    private async Task<ApprovalRequest> SolicitudGuardadaAsync(decimal monto, Guid? bodega = null, params int[] participantes)
    {
        ComoUsuario(Cajero);
        var r = await Motor().SolicitarAsync(Pedido(monto, bodega: bodega, participantes: participantes), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();
        _db.DescartarCambios();
        return r.Value!;
    }

    private Task<Result<DecisionResultDto>> DecidirAsync(ApprovalRequest s, int como, ApprovalDecisionKind decision = ApprovalDecisionKind.Approve,
        string? motivo = null, string huella = Huella, AprobadorPresente? presente = null)
    {
        ComoUsuario(como);
        return Motor().DecidirAsync(new DecisionDeAprobacion(s.PublicId, decision, motivo, huella, presente), CancellationToken.None);
    }

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    // ------------------------------------------------------------------------------------------ solicitar --

    [Fact]
    public async Task Solicitar_sella_la_politica_vigente_en_la_fecha_de_operacion_y_sus_niveles()
    {
        var v1 = await PoliticaAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30), null, (1, 0m, Ajuste));
        var v2 = await PoliticaAsync(new DateOnly(2026, 10, 1), null, null, (1, 0m, Supervisor), (2, 1_000_000m, Gerencia));

        var septiembre = await Motor().SolicitarAsync(Pedido(2_000_000m, new DateOnly(2026, 9, 15)), CancellationToken.None);
        var octubre = await Motor().SolicitarAsync(Pedido(2_000_000m, new DateOnly(2026, 10, 5)), CancellationToken.None);

        septiembre.Value!.PolicyId.Should().Be(v1.Id);
        septiembre.Value.NivelesRequeridos().Select(n => n.PermissionCode).Should().Equal(Ajuste);
        octubre.Value!.PolicyId.Should().Be(v2.Id);
        octubre.Value.NivelesRequeridos().Select(n => (n.Order, n.PermissionCode)).Should().Equal((1, Supervisor), (2, Gerencia));
        octubre.Value.CurrentLevel.Should().Be(1);
        octubre.Value.Status.Should().Be(ApprovalRequestStatus.Pending);
        octubre.Value.RequestedByUserId.Should().Be(Cajero, "quien pide es el SEC_Users.Id del actor");
        octubre.Value.RequestedAt.Should().Be(Ahora);
    }

    [Fact]
    public async Task Solicitar_no_guarda_y_avisa_al_permiso_del_primer_nivel()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));

        var r = await Motor().SolicitarAsync(Pedido(500m, participantes: Contador), CancellationToken.None);

        _db.Entry(r.Value!).State.Should().Be(EntityState.Added, "la solicitud se guarda con el documento que la pide");
        (await _db.ApprovalRequests.AsNoTracking().CountAsync()).Should().Be(0);
        r.Value!.Excluidos().Should().BeEquivalentTo([Creador, Cajero, Contador]);
        await _avisos.Received(1).PendienteAsync(r.Value, Supervisor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Solicitar_bajo_el_umbral_no_crea_nada()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 100_000m, Supervisor));

        var r = await Motor().SolicitarAsync(Pedido(99_999m), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull();
        _db.ChangeTracker.Entries<ApprovalRequest>().Should().BeEmpty();
    }

    [Fact]
    public async Task Sobre_el_maximo_del_permiso_sin_politica_es_AmountExceedsLimit()
    {
        _limites.MontoMaximoAsync("Inventory.Adjustments.Confirm", Hoy, Arg.Any<CancellationToken>()).Returns(50_000m);

        var r = await Motor().SolicitarAsync(Pedido(60_000m, permisoLimitado: "Inventory.Adjustments.Confirm"), CancellationToken.None);

        r.Error.Code.Should().Be("Inventory.Approval.AmountExceedsLimit");
        var datos = Datos(r.Error);
        datos.GetProperty("maxAmount").GetDecimal().Should().Be(50_000m);
        datos.GetProperty("amount").GetDecimal().Should().Be(60_000m);
        datos.GetProperty("permissionCode").GetString().Should().Be("Inventory.Adjustments.Confirm");
    }

    [Fact]
    public async Task Sobre_el_maximo_con_politica_fuerza_el_nivel_1()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 100_000m, Supervisor));
        _limites.MontoMaximoAsync("Inventory.Adjustments.Confirm", Hoy, Arg.Any<CancellationToken>()).Returns(50_000m);

        var r = await Motor().SolicitarAsync(Pedido(60_000m, permisoLimitado: "Inventory.Adjustments.Confirm"), CancellationToken.None);

        r.Value!.NivelesRequeridos().Select(n => n.Order).Should().Equal(1);
    }

    [Fact]
    public async Task Cambiar_la_politica_despues_no_altera_la_solicitud_pendiente()
    {
        var p = await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        var guardada = await _db.ApprovalPolicies.Include(x => x.Levels).SingleAsync(x => x.Id == p.Id);
        guardada.Levels.Single().PermissionCode = Gerencia;
        await _db.SaveChangesAsync();

        _permisos.HasPermissionAsync(Gerencia, Arg.Any<CancellationToken>()).Returns(false);
        (await DecidirAsync(s, Aprobador1)).IsSuccess.Should().BeTrue("decide con el permiso sellado, no con el de hoy");
    }

    // ---------------------------------------------------------------------------------------- segregación --

    [Theory]
    [InlineData(Creador, "Creator")]
    [InlineData(Cajero, "Requester")]
    [InlineData(Contador, "Participant")]
    public async Task La_segregacion_compara_el_SEC_Users_Id_del_actor(int decisor, string motivo)
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m, participantes: Contador);

        var r = await DecidirAsync(s, decisor);

        r.Error.Code.Should().Be("Approvals.SelfApprovalForbidden");
        Datos(r.Error).GetProperty("reason").GetString().Should().Be(motivo);
        (await _db.ApprovalDecisions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Quien_aprobo_un_nivel_no_decide_el_siguiente()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor), (2, 0m, Gerencia));
        var s = await SolicitudGuardadaAsync(500m);

        var primero = await DecidirAsync(s, Aprobador1);
        _db.DescartarCambios();
        var segundo = await DecidirAsync(s, Aprobador1);

        primero.Value.CurrentLevel.Should().Be(2);
        primero.Value.Status.Should().Be("Pending");
        segundo.Error.Code.Should().Be("Approvals.SelfApprovalForbidden");
        Datos(segundo.Error).GetProperty("reason").GetString().Should().Be("PreviousLevel");
        _fuente.Aprobadas.Should().BeEmpty("sólo el último nivel confirma");
        await _avisos.Received(1).PendienteAsync(Arg.Any<ApprovalRequest>(), Gerencia, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_persona_resuelta_la_solicitud_no_existe()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        ComoUsuario(null);
        var r = await Motor().DecidirAsync(new DecisionDeAprobacion(s.PublicId, ApprovalDecisionKind.Approve, null, Huella), CancellationToken.None);

        r.Error.Code.Should().Be("Approvals.Request.NotFound");
    }

    // ------------------------------------------------------------------------------------------- huella --

    [Fact]
    public async Task Una_huella_distinta_es_ContentChanged_con_la_actual()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        var r = await DecidirAsync(s, Aprobador1, huella: new string('b', 64));

        r.Error.Code.Should().Be("Approvals.Request.ContentChanged");
        Datos(r.Error).GetProperty("currentSha256").GetString().Should().Be(Huella);
        (await _db.ApprovalDecisions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cuando_lo_aprobado_cambia_la_fuente_invalida_la_solicitud()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        var invalidada = await Motor().InvalidarAsync(FuenteDePrueba.Tipo, s.SourcePublicId, ApprovalSubjects.DocumentConfirmation, CancellationToken.None);
        await _db.SaveChangesAsync();
        _db.DescartarCambios();
        var r = await DecidirAsync(s, Aprobador1);

        invalidada!.Status.Should().Be(ApprovalRequestStatus.Cancelled);
        r.Error.Code.Should().Be("Approvals.Request.NotPending");
        Datos(r.Error).GetProperty("status").GetString().Should().Be("Cancelled");
    }

    // ----------------------------------------------------------------------------------------- rechazar --

    [Fact]
    public async Task Rechazar_sin_motivo_es_Validation_Invalid()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        var r = await DecidirAsync(s, Aprobador1, ApprovalDecisionKind.Reject, motivo: "  ");
        var validador = new DecideApprovalCommandValidator();

        r.Error.Code.Should().Be("Validation.Invalid");
        validador.Validate(new DecideApprovalCommand(s.PublicId, ApprovalDecisionKind.Reject, null, ApprovalMethod.OwnSession, null, Huella))
            .IsValid.Should().BeFalse();
        validador.Validate(new DecideApprovalCommand(s.PublicId, ApprovalDecisionKind.Approve, null, ApprovalMethod.OwnSession, null, Huella))
            .IsValid.Should().BeTrue();
        _fuente.Devueltas.Should().BeEmpty();
    }

    [Fact]
    public async Task Rechazar_con_motivo_deja_Rejected_y_devuelve_a_borrador()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor), (2, 0m, Gerencia));
        var s = await SolicitudGuardadaAsync(500m);

        var r = await DecidirAsync(s, Aprobador1, ApprovalDecisionKind.Reject, motivo: "Faltan soportes");

        r.Value.Status.Should().Be("Rejected");
        _fuente.Devueltas.Should().Equal(("Faltan soportes", s.SourcePublicId));
        var guardada = await _db.ApprovalRequests.AsNoTracking().SingleAsync();
        guardada.Status.Should().Be(ApprovalRequestStatus.Rejected);
        guardada.DecidedAt.Should().Be(Ahora);
        var decision = await _db.ApprovalDecisions.AsNoTracking().SingleAsync();
        decision.Decision.Should().Be(ApprovalDecisionKind.Reject);
        decision.Reason.Should().Be("Faltan soportes");
        decision.DecidedByUserId.Should().Be(Aprobador1);
    }

    // ------------------------------------------------------------------------------------------ aprobar --

    [Fact]
    public async Task Aprobar_el_ultimo_nivel_confirma_por_la_fuente()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        var r = await DecidirAsync(s, Aprobador1);

        r.Value.Status.Should().Be("Approved");
        r.Value.Source.Status.Should().Be("Confirmed");
        r.Value.Source.DisplayNumber.Should().Be("AJ-000123");
        _fuente.Aprobadas.Should().Equal(s.SourcePublicId);
        var decision = await _db.ApprovalDecisions.AsNoTracking().SingleAsync();
        decision.Method.Should().Be(ApprovalMethod.OwnSession);
        decision.PermissionCodeUsed.Should().Be(Supervisor);
        decision.ContentSha256.Should().Be(Huella);
        decision.Level.Should().Be(1);
        (await _db.ApprovalRequests.AsNoTracking().SingleAsync()).Status.Should().Be(ApprovalRequestStatus.Approved);
    }

    [Fact]
    public async Task Si_la_fuente_no_puede_confirmar_la_decision_no_queda()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);
        _fuente.FallaAlAprobar = new Error("Inventory.Stock.Insufficient", "No alcanza la existencia.");

        var r = await DecidirAsync(s, Aprobador1);

        r.Error.Code.Should().Be("Inventory.Stock.Insufficient");
        _db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).Should().BeEmpty();
        (await _db.ApprovalDecisions.CountAsync()).Should().Be(0);
        (await _db.ApprovalRequests.AsNoTracking().SingleAsync()).Status.Should().Be(ApprovalRequestStatus.Pending);
    }

    [Fact]
    public async Task Una_solicitud_no_pendiente_es_NotPending()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);
        (await DecidirAsync(s, Aprobador1)).IsSuccess.Should().BeTrue();
        _db.DescartarCambios();

        var r = await DecidirAsync(s, Aprobador2);

        r.Error.Code.Should().Be("Approvals.Request.NotPending");
        Datos(r.Error).GetProperty("status").GetString().Should().Be("Approved");
    }

    // -------------------------------------------------------------------------------- permiso y alcance --

    [Fact]
    public async Task Sin_el_permiso_del_nivel_la_solicitud_no_existe()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);
        _permisos.HasPermissionAsync(Supervisor, Arg.Any<CancellationToken>()).Returns(false);

        var r = await DecidirAsync(s, Aprobador1);

        r.Error.Code.Should().Be("Approvals.Request.NotFound");
    }

    [Fact]
    public async Task Sin_alcance_sobre_la_bodega_la_solicitud_no_existe()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m, Bodega);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        var fuera = await DecidirAsync(s, Aprobador1);
        _fuente.BodegasAlAlcance.Add(Bodega);
        var dentro = await DecidirAsync(s, Aprobador1);

        fuera.Error.Code.Should().Be("Approvals.Request.NotFound");
        dentro.IsSuccess.Should().BeTrue("la fuente dice que la bodega está en el alcance");
    }

    [Fact]
    public async Task Una_solicitud_inexistente_es_NotFound()
    {
        ComoUsuario(Aprobador1);
        var r = await Motor().DecidirAsync(new DecisionDeAprobacion(Guid.NewGuid(), ApprovalDecisionKind.Approve, null, Huella), CancellationToken.None);

        r.Error.Code.Should().Be("Approvals.Request.NotFound");
    }

    [Fact]
    public async Task El_aprobador_presente_decide_con_su_permiso_y_queda_su_metodo()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);
        _permisos.HasPermissionAsync(Supervisor, Arg.Any<CancellationToken>()).Returns(false); // el cajero no lo tiene
        _otro.TienePermisoAsync(Aprobador2, Supervisor, Arg.Any<CancellationToken>()).Returns(true);
        _otro.AlcanceAsync(Aprobador2, Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        var llave = Guid.NewGuid();

        var r = await DecidirAsync(s, Cajero, presente: new AprobadorPresente(Aprobador2, "supervisor@coop.co", ApprovalMethod.InPersonPasskey, llave));

        r.Value.Status.Should().Be("Approved");
        var decision = await _db.ApprovalDecisions.AsNoTracking().SingleAsync();
        decision.DecidedByUserId.Should().Be(Aprobador2);
        decision.Method.Should().Be(ApprovalMethod.InPersonPasskey);
        decision.CredentialPublicId.Should().Be(llave);
    }

    // --------------------------------------------------------------------------------- bandeja y retiro --

    [Fact]
    public async Task Pendientes_para_mi_son_las_que_puedo_decidir_ahora()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var mia = await SolicitudGuardadaAsync(500m);
        var deOtroPermiso = await SolicitudGuardadaAsync(700m);
        var laCree = await SolicitudGuardadaAsync(900m);
        var sellada = await _db.ApprovalRequests.SingleAsync(r => r.Id == deOtroPermiso.Id);
        sellada.SellarNiveles([new NivelDeAprobacion(1, 0m, Gerencia)]);
        var creada = await _db.ApprovalRequests.SingleAsync(r => r.Id == laCree.Id);
        creada.CreatedByUserId = Aprobador1;
        await _db.SaveChangesAsync();
        _permisos.HasPermissionAsync(Gerencia, Arg.Any<CancellationToken>()).Returns(false);

        ComoUsuario(Aprobador1);
        var pendientes = await Motor().PendientesParaMiAsync(CancellationToken.None);

        pendientes.Select(p => p.PublicId).Should().Equal(mia.PublicId);
    }

    [Fact]
    public async Task La_bandeja_dice_si_puedo_decidir_y_por_que_no()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);
        var vista = new VistaDeSolicitudes(_db, [_fuente]);
        var consulta = new ListMyPendingApprovalsQueryHandler(_db, Motor(), vista, _actor, _permisos, _alcance);

        ComoUsuario(Cajero);
        var delCajero = await consulta.Handle(new ListMyPendingApprovalsQuery(Mine: false), CancellationToken.None);
        ComoUsuario(Aprobador1);
        var delAprobador = await consulta.Handle(new ListMyPendingApprovalsQuery(), CancellationToken.None);

        var item = delCajero.Value.Items.Should().ContainSingle().Subject;
        item.CanDecide.Should().BeFalse();
        item.ExcludedReason.Should().Be("Requester");
        item.Levels.Single().State.Should().Be("Pending");
        item.Source.Class.Should().Be("PositiveAdjustment", "lo describe la fuente");
        delAprobador.Value.Items.Should().ContainSingle().Which.CanDecide.Should().BeTrue();
        delAprobador.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task El_solicitante_retira_y_lo_aprobado_vuelve_a_borrador()
    {
        await PoliticaAsync(new DateOnly(2026, 1, 1), null, null, (1, 0m, Supervisor));
        var s = await SolicitudGuardadaAsync(500m);

        ComoUsuario(Aprobador1);
        var ajeno = await Motor().RetirarAsync(s.PublicId, "No corresponde", CancellationToken.None);
        ComoUsuario(Cajero);
        var propio = await Motor().RetirarAsync(s.PublicId, "Corrijo las cantidades", CancellationToken.None);
        var otraVez = await Motor().RetirarAsync(s.PublicId, "Corrijo las cantidades", CancellationToken.None);

        ajeno.Error.Code.Should().Be("Approvals.Request.NotFound");
        propio.IsSuccess.Should().BeTrue();
        otraVez.Error.Code.Should().Be("Approvals.Request.NotPending");
        (await _db.ApprovalRequests.AsNoTracking().SingleAsync()).Status.Should().Be(ApprovalRequestStatus.Cancelled);
        _fuente.Devueltas.Should().Equal(("Corrijo las cantidades", s.SourcePublicId));
    }

    /// <summary>Una fuente que confirma, devuelve y describe en memoria.</summary>
    private sealed class FuenteDePrueba : IFuenteDeAprobacion
    {
        public const string Tipo = ApprovalSourceTypes.InventoryDocument;

        public string SourceType => Tipo;
        public Error? FallaAlAprobar { get; set; }
        public List<Guid> Aprobadas { get; } = [];
        public List<(string Motivo, Guid Fuente)> Devueltas { get; } = [];
        public HashSet<Guid> BodegasAlAlcance { get; } = [];

        public Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
        {
            if (FallaAlAprobar is { } error) return Task.FromResult(Result.Failure<EstadoDeFuenteDto>(error));
            Aprobadas.Add(solicitud.SourcePublicId);
            return Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, "PositiveAdjustment", "Confirmed", solicitud.SourceLabel)));
        }

        public Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
        {
            Devueltas.Add((motivo, solicitud.SourcePublicId));
            return Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, "PositiveAdjustment", "Draft", null)));
        }

        public Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct) =>
            Task.FromResult(solicitud.ScopeWarehousePublicId is { } b && BodegasAlAlcance.Contains(b));

        public Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>>(ids.ToDictionary(id => id,
                id => new OrigenDeAprobacionDto(id, "PositiveAdjustment", new TipoDeOrigenDto("AJ", "Ajuste positivo"), "AJ-000123", Hoy,
                    null, null, "Ajuste de 3 productos", $"/inventario/ajustes/{id}")));
    }
}
