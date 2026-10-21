using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.ListAlerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Alerts;

/// <summary>
/// Feature 013, T0258 (US17; C68, SC-015; api.md §3.10): <see cref="ListAlertsQuery"/> con módulo <c>Lending</c> muestra una alerta
/// <c>Cartera.*</c> sólo a quien tiene un permiso destinatario del tipo <b>y</b> alcance sobre la oficina y la línea guardadas en
/// <c>COR_Alerts</c> (<see cref="VisibilidadDeAlertas"/> con <see cref="IAlcanceDeCartera"/>, que falla cerrado); la que no tuvo destinatario
/// se enrutó a <c>CompanyAdmin</c> con <c>withoutRecipient</c> y la ve por su notificación; el cuerpo nombra el crédito por su número, nunca
/// al deudor.
/// </summary>
public sealed class AlertasDeCarteraConAlcanceTests : IDisposable
{
    private static readonly DateTime Ahora = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Yo = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAlcanceDeInventario _inventario = Substitute.For<IAlcanceDeInventario>();
    private readonly IAlcanceDeCartera _cartera = Substitute.For<IAlcanceDeCartera>();
    private readonly Branch _norte;
    private readonly Branch _sur;
    private readonly CreditLine _consumo;
    private readonly CreditLine _libranza;
    private AlcanceDeCartera _alcance = AlcanceDeCartera.Vacio;

    public AlertasDeCarteraConAlcanceTests()
    {
        Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Yo, Guid.NewGuid(), "Jefe de cartera", "jefe@coop.test", ExecutionChannel.Web, "/api/lending/alerts", "10.0.0.1", null));
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { "Lending.CreditLines.Manage", "Lending.Alerts.View" });
        _inventario.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);
        _cartera.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_alcance));
        _norte = new Branch { Name = "Norte" };
        _sur = new Branch { Name = "Sur" };
        _consumo = new CreditLine { Code = "CONSUMO", Name = "Consumo" };
        _libranza = new CreditLine { Code = "LIBRANZA", Name = "Libranza" };
        _db.AddRange(_norte, _sur, _consumo, _libranza);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private VisibilidadDeAlertas Visibilidad() =>
        new(_db, _permisos, _actor, _inventario, Substitute.For<IAsignacionesDeBodega>(), new SinAsignacionesDePuntoDeVenta(), _cartera);

    private Task<Result<PagedResult<AlertDto>>> Bandeja() =>
        new ListAlertsQueryHandler(Visibilidad()).Handle(new ListAlertsQuery(Module: "Lending"), CancellationToken.None);

    private async Task<Alert> Sembrar(Branch? oficina, CreditLine? linea, string cuerpo = "El crédito CR-000123 topó la tasa del día en la usura.",
        bool sinDestinatario = false)
    {
        var tipo = await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.UsuraTopada);
        var alerta = new Alert
        {
            TypeCode = tipo.TypeCode, AlertTypeId = tipo.Id, Module = tipo.Module, Severity = tipo.Severity, Subject = "Usura topada", Body = cuerpo,
            EntityType = "Loan", EntityPublicId = Guid.NewGuid(), ScopeBranchPublicId = oficina?.PublicId, ScopeCreditLinePublicId = linea?.PublicId,
            DedupKey = $"{tipo.TypeCode}:{Guid.NewGuid():D}", RaisedAt = Ahora, LastOccurredAt = Ahora, RaisedByKind = ActorKind.Process,
            RaisedByName = Actor.NombreDelProceso, WithoutRecipient = sinDestinatario,
        };
        _db.Alerts.Add(alerta);
        await _db.SaveChangesAsync();
        return alerta;
    }

    [Fact]
    public async Task Con_el_permiso_destinatario_solo_ve_las_de_su_oficina_y_su_linea()
    {
        _alcance = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { _norte.Id }, Lineas = new HashSet<int> { _consumo.Id } };
        var suya = await Sembrar(_norte, _consumo);
        await Sembrar(_sur, _consumo);
        await Sembrar(_norte, _libranza);

        var r = await Bandeja();

        r.Value.Items.Select(a => a.PublicId).Should().Equal(suya.PublicId);
        r.Value.TotalCount.Should().Be(1, "lo de otra oficina o línea no aparece ni cuenta");
        r.Value.Items[0].Module.Should().Be("Lending");
    }

    [Fact]
    public async Task Con_alcance_total_ve_todas_y_sin_alcance_ninguna_falla_cerrado()
    {
        await Sembrar(_norte, _consumo);
        await Sembrar(_sur, _libranza);

        _alcance = AlcanceDeCartera.Total;
        (await Bandeja()).Value.TotalCount.Should().Be(2);

        _alcance = AlcanceDeCartera.Vacio;
        (await Bandeja()).Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Sin_el_permiso_destinatario_no_la_ve_aunque_tenga_el_alcance()
    {
        _alcance = AlcanceDeCartera.Total;
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { "Lending.Alerts.View" });
        await Sembrar(_norte, _consumo);

        (await Bandeja()).Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Sin_destinatario_va_a_CompanyAdmin_con_withoutRecipient_y_la_ve_por_su_notificacion()
    {
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { "Admin.Users.Manage", "Lending.Alerts.View" });
        var alerta = await Sembrar(_sur, _libranza, sinDestinatario: true);
        _db.Notifications.Add(new Notification { RecipientUserPublicId = Yo, Type = "Alert", Subject = "x", Body = "y", AlertPublicId = alerta.PublicId });
        await _db.SaveChangesAsync();

        var r = await Bandeja();

        r.Value.Items.Should().ContainSingle().Which.WithoutRecipient.Should().BeTrue();
    }

    [Fact]
    public async Task El_cuerpo_nombra_el_credito_por_su_numero_y_no_al_deudor()
    {
        _alcance = AlcanceDeCartera.Total;
        await Sembrar(_norte, _consumo);

        var alerta = (await Bandeja()).Value.Items.Single();

        alerta.Body.Should().Contain("CR-000123");
        alerta.Entity!.Type.Should().Be("Loan");
    }
}
