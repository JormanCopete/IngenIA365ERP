using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Alerts;

/// <summary>
/// T016 (feature 012; T39, FR-022, SC-022; data-model §22): levantar una alerta. InMemory con los tipos sembrados por
/// <c>AlertTypesSeeder</c>; destinatarios, actor, reloj y el envío de notificaciones son falsos, así se ve cada
/// <see cref="SendNotificationCommand"/> que sale.
/// </summary>
public class RaiseAlertCommandTests
{
    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Bodega = Guid.NewGuid();
    private static readonly Guid Producto = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDestinatariosPorPermiso _destinatarios = Substitute.For<IDestinatariosPorPermiso>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<SendNotificationCommand> _enviadas = [];

    private static readonly DestinatarioDeAlerta Comprador = new(7, Guid.NewGuid(), "Compras Uno", "compras@coop.test");
    private static readonly DestinatarioDeAlerta Jefe = new(8, Guid.NewGuid(), "Jefe de bodega", "jefe@coop.test");
    private static readonly DestinatarioDeAlerta Administrador = new(1, Guid.NewGuid(), "Admin", "admin@coop.test");

    public RaiseAlertCommandTests()
    {
        Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(DateOnly.FromDateTime(Ahora));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion("Tarea:revision-de-reorden"));
        _destinatarios.ResolverAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new DestinatariosDeAlerta([Comprador, Jefe], SinDestinatario: false));
        _sender.Send(Arg.Do<SendNotificationCommand>(_enviadas.Add), Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private Alertas Alertas() => new(_db, _destinatarios, _actor, _reloj, _sender, NullLogger<Alertas>.Instance);

    private Task<Result<AlertaLevantada>> Levantar(AlertaALevantar alerta) =>
        new RaiseAlertCommandHandler(Alertas()).Handle(new RaiseAlertCommand(alerta), CancellationToken.None);

    private static AlertaALevantar Quiebre(string cuerpo = "El disponible de ARROZ-500 en Bodega Norte quedó en 2 (mínimo 10). Pedí reposición.") =>
        new(TiposDeAlerta.Quiebre, "Quiebre de ARROZ-500", cuerpo,
            EntityType: "Product", EntityPublicId: Producto, ScopeWarehousePublicId: Bodega,
            DedupKey: $"{TiposDeAlerta.Quiebre}:{Producto:D}:{Bodega:D}");

    [Fact]
    public async Task Levanta_la_alerta_con_el_tipo_vigente_y_la_entrega_por_SendNotificationCommand()
    {
        var r = await Levantar(Quiebre());

        r.IsSuccess.Should().BeTrue();
        r.Value.Desenlace.Should().Be(DesenlaceDeAlerta.Levantada);
        var alerta = await _db.Alerts.SingleAsync();
        alerta.TypeCode.Should().Be(TiposDeAlerta.Quiebre);
        alerta.Module.Should().Be("Inventory");
        alerta.Severity.Should().Be(AlertSeverity.Warning);
        alerta.Status.Should().Be(AlertStatus.Pending);
        alerta.OccurrenceCount.Should().Be(1);
        alerta.RaisedAt.Should().Be(Ahora);
        alerta.RaisedByKind.Should().Be(ActorKind.Process);
        alerta.RaisedByName.Should().Be(Actor.NombreDelProceso);
        alerta.RecipientCount.Should().Be(2);
        alerta.WithoutRecipient.Should().BeFalse();
        alerta.AlertTypeId.Should().Be((await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.Quiebre)).Id);

        _enviadas.Should().HaveCount(2, "un SendNotificationCommand por destinatario");
        _enviadas.Select(e => e.Payload.RecipientUserPublicId).Should().BeEquivalentTo([Comprador.UserPublicId, Jefe.UserPublicId]);
        _enviadas.Should().OnlyContain(e => e.Payload.Type == NotificationType.Alert && e.Payload.AlertPublicId == alerta.PublicId);
        _enviadas.Should().OnlyContain(e => e.Payload.Channels == NotificationChannels.InApp, "Quiebre es sólo en la aplicación");
    }

    [Fact]
    public async Task Con_el_canal_de_correo_la_notificacion_lo_pide()
    {
        await Levantar(new AlertaALevantar(TiposDeAlerta.IncidenteDeIntegridad, "Diferencia en el kardex", "Verificá y reconstruí.",
            EntityPublicId: Producto));

        _enviadas.Should().OnlyContain(e => e.Payload.Channels == (NotificationChannels.InApp | NotificationChannels.Email));
    }

    [Fact]
    public async Task Los_destinatarios_se_piden_por_los_permisos_del_tipo_y_el_alcance_de_la_alerta()
    {
        await Levantar(Quiebre());

        await _destinatarios.Received(1).ResolverAsync(
            Arg.Is<IReadOnlyCollection<string>>(p => p.SequenceEqual(new[] { "Inventory.Purchases.Create", "Inventory.Warehouses.Manage" })),
            Bodega, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_misma_condicion_pendiente_suma_OccurrenceCount_y_LastOccurredAt_en_vez_de_crear_otra()
    {
        await Levantar(Quiebre());
        _reloj.UtcNow.Returns(Ahora.AddHours(3));

        var r = await Levantar(Quiebre("Ahora quedó en 1."));

        r.Value.Desenlace.Should().Be(DesenlaceDeAlerta.Repetida);
        var alerta = await _db.Alerts.SingleAsync();
        alerta.OccurrenceCount.Should().Be(2);
        alerta.LastOccurredAt.Should().Be(Ahora.AddHours(3));
        alerta.RaisedAt.Should().Be(Ahora);
        _enviadas.Should().HaveCount(2, "la repetición no vuelve a notificar");
    }

    [Fact]
    public async Task Atendida_la_alerta_la_misma_condicion_levanta_otra()
    {
        await Levantar(Quiebre());
        (await _db.Alerts.SingleAsync()).Atender(ActorKind.Person, 7, "Compras Uno", "Pedido 123 en camino", Ahora);
        await _db.SaveChangesAsync();

        var r = await Levantar(Quiebre());

        r.Value.Desenlace.Should().Be(DesenlaceDeAlerta.Levantada);
        (await _db.Alerts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task La_condicion_por_defecto_es_tipo_entidad_y_bodega()
    {
        await Levantar(new AlertaALevantar(TiposDeAlerta.Reorden, "Reorden", "Pedí.", EntityPublicId: Producto, ScopeWarehousePublicId: Bodega));

        (await _db.Alerts.SingleAsync()).DedupKey.Should().Be($"Inventario.Reorden:{Producto:D}:{Bodega:D}");
    }

    [Fact]
    public async Task Sin_destinatario_activo_va_a_los_titulares_de_CompanyAdmin_y_queda_WithoutRecipient()
    {
        _destinatarios.ResolverAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new DestinatariosDeAlerta([Administrador], SinDestinatario: true));

        var r = await Levantar(Quiebre());

        r.Value.WithoutRecipient.Should().BeTrue();
        var alerta = await _db.Alerts.SingleAsync();
        alerta.WithoutRecipient.Should().BeTrue();
        alerta.RecipientCount.Should().Be(1);
        _enviadas.Should().ContainSingle().Which.Payload.RecipientUserPublicId.Should().Be(Administrador.UserPublicId);
    }

    [Fact]
    public async Task Los_permisos_que_trae_la_alerta_reemplazan_a_los_del_tipo()
    {
        await Levantar(new AlertaALevantar(TiposDeAlerta.AprobacionPendiente, "Aprobación pendiente", "Decidí.",
            EntityPublicId: Guid.NewGuid(), RecipientPermissions: ["Inventory.Approvals.Supervisor"]));

        await _destinatarios.Received(1).ResolverAsync(
            Arg.Is<IReadOnlyCollection<string>>(p => p.SequenceEqual(new[] { "Inventory.Approvals.Supervisor" })),
            null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_tipo_fuera_del_catalogo_es_Alerts_Type_NotFound()
    {
        var r = await Levantar(new AlertaALevantar("Inventario.Inventada", "x", "y", EntityPublicId: Producto));

        r.Error.Code.Should().Be("Alerts.Type.NotFound");
        (await _db.Alerts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_tipo_deshabilitado_no_se_levanta()
    {
        var tipo = await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.Quiebre);
        tipo.IsEnabled = false;
        await _db.SaveChangesAsync();

        var r = await Levantar(Quiebre());

        r.IsSuccess.Should().BeTrue();
        r.Value.Desenlace.Should().Be(DesenlaceDeAlerta.TipoInactivo);
        (await _db.Alerts.CountAsync()).Should().Be(0);
        _enviadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_notificacion_que_falla_no_tumba_la_alerta()
    {
        _sender.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Failure("Generic.Conflict", "x"));

        var r = await Levantar(Quiebre());

        r.IsSuccess.Should().BeTrue();
        (await _db.Alerts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_proceso_la_atiende_solo_cuando_desaparece_la_causa()
    {
        await Levantar(Quiebre());

        var atendida = await Alertas().AtenderPorProcesoAsync($"{TiposDeAlerta.Quiebre}:{Producto:D}:{Bodega:D}", "Llegó la reposición.", CancellationToken.None);

        atendida.Should().BeTrue();
        var alerta = await _db.Alerts.SingleAsync();
        alerta.Status.Should().Be(AlertStatus.Attended);
        alerta.AttendedByKind.Should().Be(ActorKind.Process);
        alerta.AttendedByUserId.Should().BeNull();
        alerta.AttendNote.Should().Be("Llegó la reposición.");
        (await Alertas().AtenderPorProcesoAsync("Inventario.Quiebre:otra", "x", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task El_aviso_de_aprobacion_pendiente_va_al_permiso_del_nivel_con_el_alcance_de_la_solicitud()
    {
        var solicitud = new ApprovalRequest { SourceLabel = "AJ-0001", Subject = "DocumentConfirmation", CurrentLevel = 2, ScopeWarehousePublicId = Bodega };

        await new AvisosDeAprobacionPorAlertas(Alertas(), NullLogger<AvisosDeAprobacionPorAlertas>.Instance)
            .PendienteAsync(solicitud, "Inventory.Approvals.Management", CancellationToken.None);

        var alerta = await _db.Alerts.SingleAsync();
        alerta.TypeCode.Should().Be(TiposDeAlerta.AprobacionPendiente);
        alerta.EntityPublicId.Should().Be(solicitud.PublicId);
        alerta.DedupKey.Should().Be($"Aprobaciones.Pendiente:{solicitud.PublicId:D}:Inventory.Approvals.Management");
        await _destinatarios.Received(1).ResolverAsync(
            Arg.Is<IReadOnlyCollection<string>>(p => p.SequenceEqual(new[] { "Inventory.Approvals.Management" })),
            Bodega, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_validador_pide_la_condicion_o_la_entidad_y_un_cuerpo()
    {
        var v = new RaiseAlertCommandValidator();

        v.Validate(new RaiseAlertCommand(new AlertaALevantar(TiposDeAlerta.Reorden, "Reorden", "Pedí."))).IsValid.Should().BeFalse();
        v.Validate(new RaiseAlertCommand(new AlertaALevantar(TiposDeAlerta.Reorden, "Reorden", "", EntityPublicId: Producto))).IsValid.Should().BeFalse();
        v.Validate(new RaiseAlertCommand(new AlertaALevantar(TiposDeAlerta.Reorden, "Reorden", "Pedí.", DedupKey: "Inventario.Reorden:x"))).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_semilla_deja_todos_los_tipos_del_catalogo_y_no_pisa_lo_configurado()
    {
        (await _db.AlertTypes.CountAsync()).Should().Be(TiposDeAlerta.Todos.Count);
        var quiebre = await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.Quiebre);
        quiebre.RecipientPermissions = AlertType.PermisosComoJson(["Inventory.Integrity.Rebuild"]);
        await _db.SaveChangesAsync();

        var insertadas = await Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None);

        insertadas.Should().Be(0);
        (await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.Quiebre)).Permisos().Should().Equal("Inventory.Integrity.Rebuild");
        (await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.LoteNoCorrio)).Channels
            .Should().Be(AlertChannels.InApp | AlertChannels.Email);
    }
}
