using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.AttendAlert;
using IngenIA365ERP.Application.Common.Alerts.GetAlert;
using IngenIA365ERP.Application.Common.Alerts.ListAlerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Alerts;

/// <summary>
/// T016 (feature 012; T39, FR-022; contracts/api.md §16.1): atender una alerta y qué alertas alcanzan a quien pregunta
/// (<see cref="VisibilidadDeAlertas"/>, que comparten la bandeja, el detalle y atender). InMemory con los tipos
/// sembrados; permisos, actor, alcance y puertos de asignación falsos.
/// </summary>
public class AttendAlertCommandTests
{
    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly ElementoDeAlcance Norte = new(1, Guid.NewGuid(), "B01", "Bodega Norte");
    private static readonly ElementoDeAlcance Sur = new(2, Guid.NewGuid(), "B02", "Bodega Sur");
    private static readonly Guid Yo = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IAsignacionesDeBodega _bodegas = Substitute.For<IAsignacionesDeBodega>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public AttendAlertCommandTests()
    {
        Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _reloj.UtcNow.Returns(Ahora);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Yo, Guid.NewGuid(), "Compras Uno", "compras@coop.test", ExecutionChannel.Web, "/api/inventory/alerts", "10.0.0.1", null));
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { "Inventory.Purchases.Create", "Inventory.Alerts.View", "Inventory.Alerts.Attend" });
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio with { Bodegas = new HashSet<int> { Norte.Id } });
        _bodegas.BuscarAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyDictionary<Guid, ElementoDeAlcance>)new[] { Norte, Sur }
                .Where(e => c.Arg<IReadOnlyCollection<Guid>>().Contains(e.PublicId)).ToDictionary(e => e.PublicId));
    }

    private VisibilidadDeAlertas Visibilidad() =>
        new(_db, _permisos, _actor, _alcance, _bodegas, new SinAsignacionesDePuntoDeVenta());

    private Task<Result<AlertDto>> Atender(Guid alerta, string nota = "Pedido 123 en camino.") =>
        new AttendAlertCommandHandler(_db, Visibilidad(), _actor, _reloj)
            .Handle(new AttendAlertCommand(alerta, nota) { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    private async Task<Alert> Sembrar(string typeCode, Guid? bodega = null, bool sinDestinatario = false)
    {
        var tipo = await _db.AlertTypes.SingleAsync(t => t.TypeCode == typeCode);
        var alerta = new Alert
        {
            TypeCode = typeCode, AlertTypeId = tipo.Id, Module = tipo.Module, Severity = tipo.Severity,
            Subject = $"{typeCode} de prueba", Body = "Qué pasó y qué hacer.", EntityType = "Product", EntityPublicId = Guid.NewGuid(),
            ScopeWarehousePublicId = bodega, DedupKey = $"{typeCode}:{Guid.NewGuid():D}", RaisedAt = Ahora, LastOccurredAt = Ahora,
            RaisedByKind = ActorKind.Process, RaisedByName = Actor.NombreDelProceso, WithoutRecipient = sinDestinatario,
        };
        _db.Alerts.Add(alerta);
        await _db.SaveChangesAsync();
        return alerta;
    }

    // ------------------------------------------------------------------------------------------ atender --

    [Fact]
    public async Task Atender_deja_quien_cuando_y_la_nota_para_todos()
    {
        var alerta = await Sembrar(TiposDeAlerta.Reorden, Norte.PublicId);

        var r = await Atender(alerta.PublicId);

        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be("Attended");
        r.Value.AttendedBy.Should().Be("Compras Uno");
        r.Value.AttendNote.Should().Be("Pedido 123 en camino.");
        var guardada = await _db.Alerts.SingleAsync();
        guardada.Status.Should().Be(AlertStatus.Attended);
        guardada.AttendedAt.Should().Be(Ahora);
        guardada.AttendedByKind.Should().Be(ActorKind.Person);
        guardada.AttendedByUserId.Should().Be(7, "la persona de SEC_Users, no el entero del token");
    }

    [Fact]
    public async Task Atender_una_ya_atendida_es_AlreadyAttended_con_quien_y_cuando()
    {
        var alerta = await Sembrar(TiposDeAlerta.Reorden);
        await Atender(alerta.PublicId);

        var r = await Atender(alerta.PublicId, "Otra vez");

        r.Error.Code.Should().Be("Alerts.Alert.AlreadyAttended");
        var datos = JsonSerializer.SerializeToElement(r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data);
        datos.GetProperty("attendedBy").GetString().Should().Be("Compras Uno");
        datos.GetProperty("attendedAt").GetDateTime().Should().Be(Ahora);
        (await _db.Alerts.SingleAsync()).AttendNote.Should().Be("Pedido 123 en camino.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Atender_exige_nota(string nota)
    {
        new AttendAlertCommandValidator().Validate(new AttendAlertCommand(Guid.NewGuid(), nota)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Una_alerta_inexistente_es_Alerts_Alert_NotFound()
    {
        (await Atender(Guid.NewGuid())).Error.Code.Should().Be("Alerts.Alert.NotFound");
    }

    // ------------------------------------------------------------------------------------ visibilidad --

    [Fact]
    public async Task Sin_el_permiso_destinatario_la_alerta_no_existe_para_quien_pregunta()
    {
        var alerta = await Sembrar(TiposDeAlerta.IncidenteDeIntegridad);

        (await Atender(alerta.PublicId)).Error.Code.Should().Be("Alerts.Alert.NotFound");
        (await new GetAlertQueryHandler(Visibilidad()).Handle(new GetAlertQuery(alerta.PublicId), CancellationToken.None))
            .Error.Code.Should().Be("Alerts.Alert.NotFound");
    }

    [Fact]
    public async Task Una_alerta_de_una_bodega_fuera_del_alcance_no_existe_para_quien_pregunta()
    {
        var fuera = await Sembrar(TiposDeAlerta.Reorden, Sur.PublicId);
        var dentro = await Sembrar(TiposDeAlerta.Reorden, Norte.PublicId);
        var sinBodega = await Sembrar(TiposDeAlerta.Reorden);

        var bandeja = await new ListAlertsQueryHandler(Visibilidad()).Handle(new ListAlertsQuery(), CancellationToken.None);

        bandeja.Value.Items.Select(a => a.PublicId).Should().BeEquivalentTo([dentro.PublicId, sinBodega.PublicId]);
        (await Atender(fuera.PublicId)).Error.Code.Should().Be("Alerts.Alert.NotFound");
    }

    [Fact]
    public async Task Una_alerta_notificada_a_quien_pregunta_la_ve_aunque_no_tenga_el_permiso()
    {
        var alerta = await Sembrar(TiposDeAlerta.IncidenteDeIntegridad, sinDestinatario: true);
        _db.Notifications.Add(new Notification { RecipientUserPublicId = Yo, Type = "Alert", Subject = "x", Body = "y", AlertPublicId = alerta.PublicId });
        await _db.SaveChangesAsync();

        var r = await Atender(alerta.PublicId);

        r.IsSuccess.Should().BeTrue();
        r.Value.WithoutRecipient.Should().BeTrue();
    }

    [Fact]
    public async Task El_administrador_maestro_las_ve_todas()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        await Sembrar(TiposDeAlerta.IncidenteDeIntegridad, Sur.PublicId);

        var bandeja = await new ListAlertsQueryHandler(Visibilidad()).Handle(new ListAlertsQuery(), CancellationToken.None);

        bandeja.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task La_bandeja_filtra_por_estado_y_cuenta_las_pendientes_para_la_campana()
    {
        var atendida = await Sembrar(TiposDeAlerta.Reorden);
        await Sembrar(TiposDeAlerta.Reorden);
        await Sembrar(TiposDeAlerta.Reorden);
        await Atender(atendida.PublicId);

        var campana = await new ListAlertsQueryHandler(Visibilidad())
            .Handle(new ListAlertsQuery(Status: AlertStatus.Pending, PageSize: 1), CancellationToken.None);

        campana.Value.TotalCount.Should().Be(2);
        campana.Value.Items.Should().ContainSingle().Which.Status.Should().Be("Pending");
    }
}
