using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.GetAlertTypeHistory;
using IngenIA365ERP.Application.Common.Alerts.ListAlertTypes;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Alerts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Alerts;

/// <summary>
/// Feature 012, T410 (US12; FR-022, SC-022; contracts/api.md §16.2): los tipos de alerta con sus destinatarios activos y
/// la marca «sin destinatario» contra los usuarios activos con el permiso (<see cref="IDestinatariosPorPermiso"/>), y el
/// historial de vigencias de un tipo, la más reciente primero; un tipo fuera del catálogo cerrado de §2.13 es 404.
/// </summary>
public class AlertTypeQueriesTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 5);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDestinatariosPorPermiso _destinatarios = Substitute.For<IDestinatariosPorPermiso>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public AlertTypeQueriesTests()
    {
        Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _reloj.HoyLocal.Returns(Hoy);
        _destinatarios.ContarActivosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<string>>().Contains("Inventory.Purchases.Create") ? 3 : 0);
    }

    [Fact]
    public async Task Cada_tipo_trae_sus_destinatarios_activos_y_sin_destinatario_cuando_no_hay_ninguno()
    {
        var r = await new ListAlertTypesQueryHandler(_db, _destinatarios, _reloj).Handle(new ListAlertTypesQuery(), default);

        r.IsSuccess.Should().BeTrue();
        var quiebre = r.Value.Single(t => t.TypeCode == TiposDeAlerta.Quiebre);
        quiebre.ActiveRecipients.Should().Be(3);
        quiebre.WithoutRecipient.Should().BeFalse();
        var integridad = r.Value.Single(t => t.TypeCode == TiposDeAlerta.IncidenteDeIntegridad);
        integridad.ActiveRecipients.Should().Be(0);
        integridad.WithoutRecipient.Should().BeTrue("se iría a CompanyAdmin");
        var pendiente = r.Value.Single(t => t.TypeCode == TiposDeAlerta.AprobacionPendiente);
        pendiente.WithoutRecipient.Should().BeFalse("sus destinatarios son el permiso del nivel, no una lista");
        r.Value.Should().OnlyContain(t => t.Channels.Contains("InApp"), "la notificación en la aplicación siempre va");
    }

    [Fact]
    public async Task El_historial_devuelve_las_vigencias_la_mas_reciente_primero()
    {
        var sembrada = await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.Quiebre);
        sembrada.ValidTo = new DateOnly(2026, 10, 31);
        _db.AlertTypes.Add(new AlertType
        {
            TypeCode = sembrada.TypeCode, Module = sembrada.Module, Severity = sembrada.Severity, Channels = sembrada.Channels,
            RecipientPermissions = sembrada.RecipientPermissions, IsEnabled = true, ValidFrom = new DateOnly(2026, 11, 1), Reason = "Cambio de umbral",
        });
        await _db.SaveChangesAsync();

        var r = await new GetAlertTypeHistoryQueryHandler(_db, _destinatarios).Handle(new GetAlertTypeHistoryQuery(TiposDeAlerta.Quiebre), default);

        r.Value.Should().HaveCount(2);
        r.Value.Select(v => v.ValidFrom).Should().BeInDescendingOrder();
        r.Value[0].Reason.Should().Be("Cambio de umbral");
        r.Value[0].ActiveRecipients.Should().Be(3);
    }

    [Theory]
    [InlineData("Inventario.Inventado")]
    [InlineData("inventario.quiebre")]
    public async Task Un_tipo_fuera_del_catalogo_es_Alerts_Type_NotFound(string typeCode)
    {
        var r = await new GetAlertTypeHistoryQueryHandler(_db, _destinatarios).Handle(new GetAlertTypeHistoryQuery(typeCode), default);

        r.Error.Code.Should().Be("Alerts.Type.NotFound");
    }
}
