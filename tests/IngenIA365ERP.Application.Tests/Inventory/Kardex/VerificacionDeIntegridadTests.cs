using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Kardex;

/// <summary>
/// Feature 012, T243 (US2-3, FR-003; contracts/api.md §6.2; data-model §3.7): sin diferencias la verificación no encuentra nada;
/// con <c>StockBalance.Physical</c>, <c>StockDetail.Quantity</c> o <c>CostState.Quantity/Value</c> alterados, un incidente por fila
/// y campo con lo esperado, lo real y la diferencia, y una sola alerta <c>Inventario.IncidenteDeIntegridad</c> pendiente por
/// alcance (<c>DedupKey</c>); el evento <c>Inventory.Integrity.Verified</c>; reconstruir corrige las proyecciones sin insertar,
/// modificar ni borrar un solo hecho del kardex.
/// </summary>
public class VerificacionDeIntegridadTests
{
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public VerificacionDeIntegridadTests()
    {
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(a => _levantadas.Add(a)), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    private VerifyInventoryIntegrityQueryHandler Verificar(KardexDePrueba k)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns((string?)null);
        var servicios = new ServiceCollection().AddSingleton(_auditoria).AddSingleton<IApplicationDbContext>(k.C.Db).AddSingleton(tenant)
            .AddSingleton(IngenIA365ERP.Application.Tests.Payroll.Common.NominaTestData.UsuarioDePrueba("bodega@coop", 7)).BuildServiceProvider();
        return new VerifyInventoryIntegrityQueryHandler(k.C.Db, k.Alcance, new VerificacionDeIntegridad(k.C.Db), _alertas,
            new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance), k.C.Reloj);
    }

    private static RebuildInventoryProjectionsCommandHandler Reconstruir(KardexDePrueba k) => new(k.C.Db, k.Alcance, k.Cerrojo, k.C.Reloj);

    private static async Task<KardexDePrueba> ConMovimientosAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P1, 10m, 1300m, k.Segunda);
        (await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m)]))).Confirmacion.IsSuccess.Should().BeTrue();
        return k;
    }

    [Fact]
    public async Task Sin_diferencias_no_hay_incidentes_ni_alerta()
    {
        var k = await ConMovimientosAsync();

        var r = await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Incidents.Should().BeEmpty();
        r.Value.AlertPublicId.Should().BeNull();
        r.Value.Checked.StockBalances.Should().Be(2);
        r.Value.Checked.StockDetails.Should().Be(2);
        r.Value.Checked.CostStates.Should().Be(1);
        _levantadas.Should().BeEmpty();
        await _auditoria.Received(1).LogAsync(Arg.Is<AuditLogCommand>(c => c.Action == AuditEventTypes.InventoryIntegrityVerified), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cada_proyeccion_alterada_da_un_incidente_con_lo_esperado_lo_real_y_la_diferencia()
    {
        var k = await ConMovimientosAsync();
        var p1 = k.ProductoId(k.P1);
        (await k.C.Db.StockBalances.SingleAsync(s => s.WarehouseId == k.Principal.Id)).Physical = 99m;
        (await k.C.Db.StockDetails.SingleAsync(s => s.WarehouseId == k.Segunda.Id)).Quantity = 7m;
        var costo = await k.C.Db.CostStates.SingleAsync();
        costo.Quantity = 15m;
        costo.Value = 1m;
        await k.C.Db.SaveChangesAsync();

        var r = await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(), default);

        r.Value.Incidents.Should().HaveCount(4);
        r.Value.Incidents.Should().ContainEquivalentOf(new
        {
            Kind = "StockBalance", Field = "Physical", Expected = 6m, Actual = 99m, Difference = 93m,
            Warehouse = new { k.Principal.PublicId, Code = "PRIN" },
        }, o => o.ExcludingMissingMembers());
        r.Value.Incidents.Should().ContainEquivalentOf(new { Kind = "StockDetail", Field = "Quantity", Expected = 10m, Actual = 7m, Difference = -3m },
            o => o.ExcludingMissingMembers());
        r.Value.Incidents.Should().ContainEquivalentOf(new { Kind = "CostState", Field = "Quantity", Expected = 16m, Actual = 15m }, o => o.ExcludingMissingMembers());
        r.Value.Incidents.Should().ContainEquivalentOf(new { Kind = "CostState", Field = "Value", Actual = 1m }, o => o.ExcludingMissingMembers());
        r.Value.Incidents.Should().OnlyContain(i => i.Product.Code == "P1");
        r.Value.AlertPublicId.Should().NotBeNull();
        _levantadas.Should().ContainSingle().Which.TypeCode.Should().Be(TiposDeAlerta.IncidenteDeIntegridad);
        _ = p1;
    }

    [Fact]
    public async Task La_alerta_es_una_por_alcance_verificado()
    {
        var k = await ConMovimientosAsync();
        (await k.C.Db.StockBalances.SingleAsync(s => s.WarehouseId == k.Principal.Id)).Physical = 99m;
        await k.C.Db.SaveChangesAsync();

        await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(), default);
        await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(), default);
        await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(WarehousePublicIds: [k.Principal.PublicId]), default);

        _levantadas.Should().HaveCount(3);
        _levantadas[0].DedupKey.Should().Be(_levantadas[1].DedupKey, "la misma verificación suma a la misma alerta pendiente");
        _levantadas[0].DedupKey.Should().Be("Inventario.IncidenteDeIntegridad:todo");
        _levantadas[2].DedupKey.Should().NotBe(_levantadas[0].DedupKey, "otro alcance, otra alerta");
    }

    [Fact]
    public async Task Una_bodega_fuera_del_alcance_es_404()
    {
        var k = await ConMovimientosAsync();
        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Segunda.Id }, null, false, new HashSet<int>(), null));

        var r = await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(WarehousePublicIds: [k.Principal.PublicId]), default);

        r.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    [Fact]
    public async Task Reconstruir_corrige_las_proyecciones_y_no_toca_el_kardex()
    {
        var k = await ConMovimientosAsync();
        var hechos = await k.C.Db.KardexEntries.AsNoTracking().OrderBy(e => e.Id)
            .Select(e => new { e.Id, e.QuantityBase, e.TotalCost, e.UnitCost }).ToListAsync();
        (await k.C.Db.StockBalances.SingleAsync(s => s.WarehouseId == k.Principal.Id)).Physical = 99m;
        (await k.C.Db.StockDetails.SingleAsync(s => s.WarehouseId == k.Segunda.Id)).Quantity = 7m;
        var costo = await k.C.Db.CostStates.SingleAsync();
        costo.Value = 1m;
        await k.C.Db.SaveChangesAsync();

        var r = await Reconstruir(k).Handle(new RebuildInventoryProjectionsCommand(null, null, "Diferencia de la verificación"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Corrected.Should().HaveCount(3);
        r.Value.Corrected.Should().ContainEquivalentOf(new { Kind = "StockBalance", Field = "Physical", Before = 99m, After = 6m }, o => o.ExcludingMissingMembers());
        (await Verificar(k).Handle(new VerifyInventoryIntegrityQuery(), default)).Value.Incidents.Should().BeEmpty("0 diferencias tras reconstruir");
        (await k.C.Db.CostStates.SingleAsync()).AverageCost.Should().Be(1150m);
        (await k.C.Db.KardexEntries.AsNoTracking().OrderBy(e => e.Id).Select(e => new { e.Id, e.QuantityBase, e.TotalCost, e.UnitCost }).ToListAsync())
            .Should().BeEquivalentTo(hechos, o => o.WithStrictOrdering(), "reconstruir nunca toca el kardex");
    }

    [Fact]
    public async Task Reconstruir_crea_la_proyeccion_que_falta_y_pone_en_cero_la_que_el_kardex_no_respalda()
    {
        var k = await ConMovimientosAsync();
        k.C.Db.StockBalances.Remove(await k.C.Db.StockBalances.SingleAsync(s => s.WarehouseId == k.Segunda.Id));
        k.C.Db.StockBalances.Add(new Domain.Entities.Inventory.Projections.StockBalance { ProductId = k.ProductoId(k.P2), WarehouseId = k.Principal.Id, Physical = 5m });
        await k.C.Db.SaveChangesAsync();

        var r = await Reconstruir(k).Handle(new RebuildInventoryProjectionsCommand(null, null, "prueba"), default);

        r.IsSuccess.Should().BeTrue();
        (await k.C.Db.StockBalances.SingleAsync(s => s.WarehouseId == k.Segunda.Id && s.ProductId == k.ProductoId(k.P1))).Physical.Should().Be(10m);
        (await k.C.Db.StockBalances.SingleAsync(s => s.ProductId == k.ProductoId(k.P2))).Physical.Should().Be(0m);
    }

    [Fact]
    public void La_tarea_nocturna_corre_una_vez_por_noche_desde_las_dos()
    {
        var tarea = new VerificacionNocturnaDeIntegridad();
        var bogota = TimeSpan.FromHours(-5);

        tarea.Nombre.Should().Be("inventario.integridad");
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 26, 1, 59, 0, bogota), null).Should().BeFalse("antes de las 02:00");
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 26, 2, 0, 0, bogota), null).Should().BeTrue();
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 26, 5, 0, 0, bogota), new DateTimeOffset(2026, 9, 26, 2, 1, 0, bogota)).Should().BeFalse("ya corrió hoy");
        tarea.DebeCorrer(new DateTimeOffset(2026, 9, 27, 2, 30, 0, bogota), new DateTimeOffset(2026, 9, 26, 2, 1, 0, bogota)).Should().BeTrue();
    }

    [Fact]
    public void Reconstruir_exige_motivo()
    {
        new RebuildInventoryProjectionsCommandValidator().Validate(new RebuildInventoryProjectionsCommand(null, null, " ")).IsValid.Should().BeFalse();
    }
}
