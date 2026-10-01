using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.GetAlertTypeHistory;
using IngenIA365ERP.Application.Common.Alerts.ListAlertTypes;
using IngenIA365ERP.Application.Common.Alerts.SaveAlertType;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Alerts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Alerts;

/// <summary>
/// T016 (feature 012; T39; contracts/api.md §16.2): configurar un tipo de alerta por versiones y consultarlo. Parte de
/// T016 en archivo propio (la tarea nombra dos; el alta de tipos pesaba lo suficiente para uno aparte). InMemory con el
/// catálogo de permisos y los tipos sembrados.
/// </summary>
public class SaveAlertTypeCommandTests
{
    private static readonly DateOnly Octubre = new(2026, 10, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDestinatariosPorPermiso _destinatarios = Substitute.For<IDestinatariosPorPermiso>();

    public SaveAlertTypeCommandTests()
    {
        _db.Permissions.AddRange(
            new Permission { Resource = "Inventory.Purchases", Action = "Create" },
            new Permission { Resource = "Inventory.Warehouses", Action = "Manage" },
            new Permission { Resource = "Inventory.Stock", Action = "View" });
        _db.SaveChanges();
        Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _destinatarios.ContarActivosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(3);
    }

    private Task<Result<AlertTypeDto>> Guardar(SaveAlertTypeCommand c) =>
        new SaveAlertTypeCommandHandler(_db, _destinatarios).Handle(c, CancellationToken.None);

    private static SaveAlertTypeCommand Version(
        string typeCode = TiposDeAlerta.Quiebre,
        IReadOnlyList<string>? permisos = null,
        IReadOnlyList<AlertChannels>? canales = null,
        IReadOnlyDictionary<string, decimal>? umbrales = null,
        DateOnly? desde = null) =>
        new(typeCode, permisos ?? ["Inventory.Warehouses.Manage"], canales ?? [AlertChannels.InApp, AlertChannels.Email], umbrales,
            desde ?? Octubre, "Acta 9: el jefe de bodega atiende los quiebres") { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Registra_la_version_y_cierra_la_semilla_la_vispera()
    {
        var r = await Guardar(Version());

        r.IsSuccess.Should().BeTrue();
        r.Value.RecipientPermissions.Should().Equal("Inventory.Warehouses.Manage");
        r.Value.Channels.Should().Equal("InApp", "Email");
        r.Value.Severity.Should().Be("Warning");
        r.Value.ActiveRecipients.Should().Be(3);
        r.Value.WithoutRecipient.Should().BeFalse();
        var versiones = await _db.AlertTypes.Where(t => t.TypeCode == TiposDeAlerta.Quiebre).OrderBy(t => t.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(new DateOnly(2026, 9, 30));
        versiones[1].ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Un_tipo_fuera_del_catalogo_es_NotFound()
    {
        (await Guardar(Version("Inventario.Inventada"))).Error.Code.Should().Be("Alerts.Type.NotFound");
    }

    [Fact]
    public async Task Un_permiso_que_no_existe_es_PermissionUnknown()
    {
        var r = await Guardar(Version(permisos: ["Inventory.Nada.Manage"]));

        r.Error.Code.Should().Be("Alerts.Type.PermissionUnknown");
        JsonSerializer.SerializeToElement(r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data)
            .GetProperty("permissionCode").GetString().Should().Be("Inventory.Nada.Manage");
    }

    [Fact]
    public async Task Un_permiso_de_consulta_no_puede_ser_destinatario()
    {
        (await Guardar(Version(permisos: ["Inventory.Stock.View"]))).Error.Code.Should().Be("Alerts.Type.ViewPermissionNotAllowed");
    }

    [Fact]
    public async Task Sin_destinatarios_es_RecipientsRequired_salvo_en_el_tipo_que_no_usa_lista()
    {
        (await Guardar(Version(permisos: []))).Error.Code.Should().Be("Alerts.Type.RecipientsRequired");
        (await Guardar(Version(TiposDeAlerta.AprobacionPendiente, permisos: []))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_la_aplicacion_es_InAppRequired()
    {
        (await Guardar(Version(canales: [AlertChannels.Email]))).Error.Code.Should().Be("Alerts.Type.InAppRequired");
    }

    [Fact]
    public async Task Un_umbral_que_el_tipo_no_admite_es_ThresholdsInvalid()
    {
        var r = await Guardar(Version(umbrales: new Dictionary<string, decimal> { ["dias"] = 3 }));

        r.Error.Code.Should().Be("Alerts.Type.ThresholdsInvalid");
        JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data).GetProperty("key").GetString().Should().Be("dias");
    }

    [Fact]
    public async Task Una_version_del_mismo_dia_o_anterior_a_la_ultima_es_Overlaps()
    {
        await Guardar(Version());

        (await Guardar(Version(desde: Octubre))).Error.Code.Should().Be("Alerts.Type.Overlaps");
        (await Guardar(Version(desde: new DateOnly(2026, 9, 1)))).Error.Code.Should().Be("Alerts.Type.Overlaps");
    }

    [Fact]
    public async Task Sin_usuarios_activos_con_el_permiso_el_tipo_sale_sin_destinatario()
    {
        _destinatarios.ContarActivosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(0);

        var r = await Guardar(Version());

        r.Value.WithoutRecipient.Should().BeTrue();
    }

    [Fact]
    public void El_validador_exige_motivo_y_fecha()
    {
        var v = new SaveAlertTypeCommandValidator();

        v.Validate(Version() with { Reason = " " }).IsValid.Should().BeFalse();
        v.Validate(Version() with { ValidFrom = default }).IsValid.Should().BeFalse();
        v.Validate(Version()).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_consulta_da_la_version_vigente_de_cada_tipo_y_el_historial_la_mas_reciente_primero()
    {
        await Guardar(Version());
        var reloj = Substitute.For<IDateTimeService>();

        var hoy = await new ListAlertTypesQueryHandler(_db, _destinatarios, reloj)
            .Handle(new ListAlertTypesQuery(new DateOnly(2026, 10, 15)), CancellationToken.None);
        var antes = await new ListAlertTypesQueryHandler(_db, _destinatarios, reloj)
            .Handle(new ListAlertTypesQuery(new DateOnly(2026, 9, 15)), CancellationToken.None);
        var historia = await new GetAlertTypeHistoryQueryHandler(_db, _destinatarios)
            .Handle(new GetAlertTypeHistoryQuery(TiposDeAlerta.Quiebre), CancellationToken.None);

        hoy.Value.Should().HaveCount(TiposDeAlerta.Todos.Count);
        hoy.Value.Single(t => t.TypeCode == TiposDeAlerta.Quiebre).RecipientPermissions.Should().Equal("Inventory.Warehouses.Manage");
        antes.Value.Single(t => t.TypeCode == TiposDeAlerta.Quiebre).RecipientPermissions.Should().HaveCount(2);
        historia.Value.Select(t => t.ValidFrom).Should().BeInDescendingOrder();
        (await new GetAlertTypeHistoryQueryHandler(_db, _destinatarios)
            .Handle(new GetAlertTypeHistoryQuery("Otra"), CancellationToken.None)).Error.Code.Should().Be("Alerts.Type.NotFound");
    }
}
