using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Replenishment;

/// <summary>
/// Feature 012, US17, T944 (FR-035, FR-022, SC-022): la revisión programada recorre cada <c>INV_ReorderPolicies</c> vivo de las
/// bodegas operativas y activadas, y levanta <c>Inventario.Reorden</c> e <c>Inventario.Quiebre</c> por <c>RaiseAlertCommand</c> con la
/// condición <c>{TypeCode}:{producto}:{bodega}</c> y la bodega como alcance; repetirla con la alerta pendiente sólo suma la ocurrencia;
/// el tránsito y las no activadas no se revisan; corre como el proceso, sin alcance de persona. Y la tarea
/// <see cref="TareaDeRevisionDeReorden"/> corre una vez al día desde su hora.
/// </summary>
public class RevisionDeReordenTests
{
    private static async Task<ReposicionDePrueba> ProcesoAsync()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        r.K.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion(Actor.OrigenDeTarea(TareaDeRevisionDeReorden.NombreDeLaTarea)));
        return r;
    }

    [Fact]
    public async Task Con_12_disponibles_y_5_en_transito_no_levanta_nada()
    {
        var r = await ProcesoAsync();
        var k = r.K;
        r.Politica(k.P2, k.Principal);
        r.Existencia(k.P2, k.Principal, 12m);
        r.EnTransito(k.P2, k.Segunda, k.Principal, 5m);

        var resultado = await r.Revision().RevisarAsync(default);

        resultado.Should().Be(new ResultadoDeRevisionDeReorden(1, 0));
        r.Enviadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_8_disponibles_y_5_en_transito_levanta_reorden_y_quiebre_por_RaiseAlertCommand()
    {
        var r = await ProcesoAsync();
        var k = r.K;
        r.Politica(k.P2, k.Principal);
        r.Existencia(k.P2, k.Principal, 8m);
        r.EnTransito(k.P2, k.Segunda, k.Principal, 5m);

        var resultado = await r.Revision().RevisarAsync(default);

        resultado.Levantadas.Should().Be(2);
        var producto = k.C.Producto(k.P2).PublicId;
        r.Enviadas.Select(e => e.Alerta.DedupKey).Should().BeEquivalentTo(
        [
            $"Inventario.Reorden:{producto:D}:{k.Principal.PublicId:D}",
            $"Inventario.Quiebre:{producto:D}:{k.Principal.PublicId:D}",
        ]);
        r.Enviadas.Should().OnlyContain(e => e.Alerta.ScopeWarehousePublicId == k.Principal.PublicId);
        r.Enviadas.Single(e => e.Alerta.TypeCode == TiposDeAlerta.Reorden).Alerta.Body.Should().Contain("13").And.Contain("37", "sugerido 50 − 13");

        var alertas = await r.AlertasAsync();
        alertas.Should().HaveCount(2);
        alertas.Should().OnlyContain(a => a.RaisedByKind == ActorKind.Process, "corre como el proceso de integración");
    }

    [Fact]
    public async Task Una_segunda_revision_con_la_alerta_pendiente_no_crea_otra()
    {
        var r = await ProcesoAsync();
        var k = r.K;
        r.Politica(k.P2, k.Principal);
        r.Existencia(k.P2, k.Principal, 8m);

        await r.Revision().RevisarAsync(default);
        await r.Revision().RevisarAsync(default);

        var alertas = await r.AlertasAsync();
        alertas.Should().HaveCount(2);
        alertas.Should().OnlyContain(a => a.OccurrenceCount == 2 && a.Status == AlertStatus.Pending);
    }

    [Fact]
    public async Task No_revisa_el_transito_ni_las_bodegas_no_activadas_ni_las_inactivas()
    {
        var r = await ProcesoAsync();
        var k = r.K;
        var db = k.C.Db;
        var operativa = db.WarehouseTypes.First(t => t.Behavior == WarehouseBehavior.Operational).Id;
        var sinActivar = new Warehouse
        {
            Code = "PV9", Name = "Sin activar", BranchId = k.Sucursal.Id, WarehouseTypeId = operativa,
            Behavior = WarehouseBehavior.Operational, ActivationStatus = WarehouseActivationStatus.NotActivated, IsActive = true,
        };
        var inactiva = new Warehouse
        {
            Code = "PV8", Name = "Cerrada", BranchId = k.Sucursal.Id, WarehouseTypeId = operativa,
            Behavior = WarehouseBehavior.Operational, ActivationStatus = WarehouseActivationStatus.Active, IsActive = false,
        };
        db.Warehouses.AddRange(sinActivar, inactiva);
        await db.SaveChangesAsync();
        r.Politica(k.P1, sinActivar);
        r.Politica(k.P1, inactiva);
        r.Politica(k.P1, k.Transito);

        var resultado = await r.Revision().RevisarAsync(default);

        resultado.Revisadas.Should().Be(0);
        r.Enviadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Revisa_todas_las_bodegas_operativas_sin_alcance_de_persona()
    {
        var r = await ProcesoAsync();
        var k = r.K;
        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Application.Common.Interfaces.Security.AlcanceDeInventario.Vacio);
        r.Politica(k.P1, k.Principal);
        r.Politica(k.P1, k.Segunda);

        var resultado = await r.Revision().RevisarAsync(default);

        resultado.Revisadas.Should().Be(2);
        r.Enviadas.Select(e => e.Alerta.ScopeWarehousePublicId).Distinct().Should().BeEquivalentTo([k.Principal.PublicId, k.Segunda.PublicId]);
    }

    [Fact]
    public void La_tarea_corre_una_vez_al_dia_desde_su_hora()
    {
        var tarea = new TareaDeRevisionDeReorden(new TimeOnly(5, 0));
        var zona = TimeSpan.FromHours(-5);

        tarea.Nombre.Should().Be("inventario.reorden");
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 25, 4, 59, 0, zona), null).Should().BeFalse();
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 25, 5, 0, 0, zona), null).Should().BeTrue();
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 25, 23, 0, 0, zona), new DateTimeOffset(2026, 9, 25, 5, 1, 0, zona)).Should().BeFalse();
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 26, 5, 30, 0, zona), new DateTimeOffset(2026, 9, 25, 5, 1, 0, zona)).Should().BeTrue();
        new TareaDeRevisionDeReorden().HoraDeInicio.Should().Be(TareaDeRevisionDeReorden.HoraPorDefecto);
    }
}
