using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.GetAlert;
using IngenIA365ERP.Application.Common.Alerts.ListAlerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Alerts;

/// <summary>
/// Feature 012, T409 (US12; FR-022, SC-022; contracts/api.md §16.1): la bandeja y el detalle de alertas
/// (<see cref="ListAlertsQuery"/>, <see cref="GetAlertQuery"/>) devuelven sólo las alertas cuyo tipo tiene un permiso
/// destinatario del usuario <b>y</b> cuya bodega está en su alcance —lo demás es el mismo 404 que lo inexistente—, la más
/// reciente primero, con el contador de la campana en <c>totalCount</c>; y la alerta sin destinatario, enrutada a
/// <c>CompanyAdmin</c> por notificación, la ve el administrador con su marca.
/// </summary>
public class AlertQueriesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly ElementoDeAlcance Prin = new(1, Guid.NewGuid(), "PRIN", "Principal");
    private static readonly ElementoDeAlcance Pv1 = new(2, Guid.NewGuid(), "PV1", "Punto 1");
    private static readonly Guid Yo = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IAsignacionesDeBodega _bodegas = Substitute.For<IAsignacionesDeBodega>();

    public AlertQueriesTests()
    {
        Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Yo, Guid.NewGuid(), "Bodega B", "bodega.b@coop.test", ExecutionChannel.Web, "/api/inventory/alerts", "10.0.0.1", null));
        // bodega.b: recibe Quiebre (Inventory.Warehouses.Manage) y sólo tiene PV1.
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { "Inventory.Warehouses.Manage", "Inventory.Alerts.View" });
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio with { Bodegas = new HashSet<int> { Pv1.Id } });
        _bodegas.BuscarAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyDictionary<Guid, ElementoDeAlcance>)new[] { Prin, Pv1 }
                .Where(e => c.Arg<IReadOnlyCollection<Guid>>().Contains(e.PublicId)).ToDictionary(e => e.PublicId));
    }

    private VisibilidadDeAlertas Visibilidad() => new(_db, _permisos, _actor, _alcance, _bodegas, new SinAsignacionesDePuntoDeVenta());

    private Task<Result<PagedResult<AlertDto>>> Bandeja(ListAlertsQuery q) =>
        new ListAlertsQueryHandler(Visibilidad()).Handle(q, CancellationToken.None);

    private Task<Result<AlertDto>> Detalle(Guid id) => new GetAlertQueryHandler(Visibilidad()).Handle(new GetAlertQuery(id), CancellationToken.None);

    private async Task<Alert> Sembrar(string typeCode, Guid? bodega, DateTime levantada, bool sinDestinatario = false)
    {
        var tipo = await _db.AlertTypes.SingleAsync(t => t.TypeCode == typeCode);
        var alerta = new Alert
        {
            TypeCode = typeCode, AlertTypeId = tipo.Id, Module = tipo.Module, Severity = tipo.Severity,
            Subject = $"{typeCode} {levantada:HH:mm}", Body = "Qué pasó y qué hacer.", EntityType = "Product", EntityPublicId = Guid.NewGuid(),
            ScopeWarehousePublicId = bodega, DedupKey = $"{typeCode}:{Guid.NewGuid():D}", RaisedAt = levantada, LastOccurredAt = levantada,
            RaisedByKind = ActorKind.Process, RaisedByName = Actor.NombreDelProceso, WithoutRecipient = sinDestinatario,
        };
        _db.Alerts.Add(alerta);
        await _db.SaveChangesAsync();
        return alerta;
    }

    [Fact]
    public async Task Solo_devuelve_las_de_un_tipo_destinado_al_usuario_y_de_una_bodega_de_su_alcance()
    {
        var dePv1 = await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora);
        var dePrin = await Sembrar(TiposDeAlerta.Quiebre, Prin.PublicId, Ahora);
        var sinPermiso = await Sembrar(TiposDeAlerta.IncidenteDeIntegridad, Pv1.PublicId, Ahora);

        var r = await Bandeja(new ListAlertsQuery());

        r.Value.Items.Select(a => a.PublicId).Should().Equal(dePv1.PublicId);
        r.Value.TotalCount.Should().Be(1, "lo ajeno no se cuenta");
        (await Detalle(dePv1.PublicId)).Value.TypeCode.Should().Be(TiposDeAlerta.Quiebre);
        (await Detalle(dePrin.PublicId)).Error.Code.Should().Be("Alerts.Alert.NotFound");
        (await Detalle(sinPermiso.PublicId)).Error.Code.Should().Be("Alerts.Alert.NotFound");
        (await Detalle(Guid.NewGuid())).Error.Code.Should().Be("Alerts.Alert.NotFound");
    }

    [Fact]
    public async Task Ordena_por_RaisedAt_descendente()
    {
        var vieja = await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora.AddHours(-5));
        var nueva = await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora);
        var media = await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora.AddHours(-1));

        var r = await Bandeja(new ListAlertsQuery());

        r.Value.Items.Select(a => a.PublicId).Should().Equal(nueva.PublicId, media.PublicId, vieja.PublicId);
    }

    [Fact]
    public async Task El_contador_de_la_campana_es_totalCount_de_las_pendientes_con_una_por_pagina()
    {
        await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora);
        await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora.AddMinutes(-1));
        var atendida = await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora.AddMinutes(-2));
        atendida.Atender(ActorKind.Person, 7, "Bodega B", "Repuesto.", Ahora);
        await Sembrar(TiposDeAlerta.Quiebre, Prin.PublicId, Ahora);
        await _db.SaveChangesAsync();

        var r = await Bandeja(new ListAlertsQuery(Status: AlertStatus.Pending, PageSize: 1));

        r.Value.TotalCount.Should().Be(2);
        r.Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Filtra_por_tipo_severidad_y_fecha()
    {
        await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora);
        await Sembrar(TiposDeAlerta.Quiebre, Pv1.PublicId, Ahora.AddDays(-3));

        (await Bandeja(new ListAlertsQuery(From: DateOnly.FromDateTime(Ahora)))).Value.TotalCount.Should().Be(1);
        (await Bandeja(new ListAlertsQuery(To: DateOnly.FromDateTime(Ahora.AddDays(-1))))).Value.TotalCount.Should().Be(1);
        (await Bandeja(new ListAlertsQuery(TypeCode: TiposDeAlerta.Reorden))).Value.TotalCount.Should().Be(0);
        (await Bandeja(new ListAlertsQuery(Severity: AlertSeverity.Critical))).Value.TotalCount.Should().Be(0);
        (await Bandeja(new ListAlertsQuery(Severity: AlertSeverity.Warning))).Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task La_alerta_sin_destinatario_la_ve_CompanyAdmin_con_su_marca()
    {
        // Integridad sin titulares de Inventory.Integrity.Rebuild: se enruta a CompanyAdmin por notificación.
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { "Admin.Users.Manage", "Inventory.Alerts.View" });
        var alerta = await Sembrar(TiposDeAlerta.IncidenteDeIntegridad, Prin.PublicId, Ahora, sinDestinatario: true);
        _db.Notifications.Add(new Notification { RecipientUserPublicId = Yo, Type = "Alert", Subject = "x", Body = "y", AlertPublicId = alerta.PublicId });
        await _db.SaveChangesAsync();

        var r = await Bandeja(new ListAlertsQuery());

        r.Value.Items.Should().ContainSingle().Which.WithoutRecipient.Should().BeTrue();
    }
}
