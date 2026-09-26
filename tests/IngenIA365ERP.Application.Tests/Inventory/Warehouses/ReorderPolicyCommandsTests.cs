using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Warehouses;

/// <summary>
/// Feature 012, T193 (FR-035; contracts/api.md §4.4, §4.5; data-model §2.4): mínimo, máximo y punto de reorden por
/// (producto, bodega). El <c>PUT</c> crea o cambia la misma fila; <c>0 ≤ mínimo ≤ punto ≤ máximo</c>; ni tránsito ni
/// servicios; la bodega respeta el alcance; retirar es baja lógica y volver a fijar crea otra fila; la lista informa
/// disponible y posición sólo con <c>Inventory.Stock.View</c>.
/// </summary>
public class ReorderPolicyCommandsTests
{
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();

    public ReorderPolicyCommandsTests()
    {
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private sealed record Escenario(CatalogoDePrueba C, Warehouse Pv1, Warehouse Transito, Guid P2, Guid Flete);

    private static async Task<Escenario> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var tipos = c.Db.WarehouseTypes.ToList();
        var pv1 = new Warehouse { Code = "PV1", Name = "Punto 1", BranchId = 1, WarehouseTypeId = tipos.First(t => t.Behavior == WarehouseBehavior.Operational).Id };
        var transito = new Warehouse
        {
            Code = "TR01", Name = "Tránsito", BranchId = 1, Behavior = WarehouseBehavior.Transit,
            WarehouseTypeId = tipos.First(t => t.Behavior == WarehouseBehavior.Transit).Id,
        };
        c.Db.Warehouses.AddRange(pv1, transito);
        await c.Db.SaveChangesAsync();
        var p2 = await c.ProductoAsync(c.Alta("P2", "Frijol"));
        var flete = await c.ProductoAsync(c.Alta("FLETE", "Flete", ProductKind.Service, "SRV"));
        return new Escenario(c, pv1, transito, p2.PublicId, flete.PublicId);
    }

    private SetReorderPolicyCommandHandler Fijar(Escenario e) => new(e.C.Db, _alcance);

    private ListReorderPoliciesQueryHandler Listar(Escenario e) => new(e.C.Db, _alcance, _permisos, new Application.Inventory.Replenishment.PosicionDeReposicion(e.C.Db));

    [Fact]
    public async Task Fijar_crea_la_fila_y_volver_a_fijar_la_cambia_sin_duplicar()
    {
        var e = await EscenarioAsync();

        var alta = await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 10m, 50m, 15m), default);
        var cambio = await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 12m, 60m, 20m), default);

        alta.IsSuccess.Should().BeTrue(alta.IsFailure ? alta.Error.Message : string.Empty);
        cambio.Value.PublicId.Should().Be(alta.Value.PublicId);
        cambio.Value.ReorderPoint.Should().Be(20m);
        e.C.Db.ReorderPolicies.Should().ContainSingle();
    }

    [Theory]
    [InlineData(-1, 5, 10)]
    [InlineData(6, 5, 10)]
    [InlineData(1, 11, 10)]
    public async Task El_minimo_el_punto_y_el_maximo_van_en_orden(int minimo, int punto, int maximo)
    {
        var e = await EscenarioAsync();

        var r = await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, minimo, maximo, punto), default);

        r.Error.Code.Should().Be("Inventory.ReorderPolicy.Invalid");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { minimum = (decimal)minimo, reorderPoint = (decimal)punto, maximum = (decimal)maximo });
    }

    [Fact]
    public async Task Ni_en_transito_ni_para_servicios_ni_fuera_del_alcance()
    {
        var e = await EscenarioAsync();

        var transito = await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Transito.PublicId, 1m, 10m, 2m), default);
        var servicio = await Fijar(e).Handle(new SetReorderPolicyCommand(e.Flete, e.Pv1.PublicId, 1m, 10m, 2m), default);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);
        var fuera = await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 1m, 10m, 2m), default);

        transito.Error.Code.Should().Be("Inventory.ReorderPolicy.TransitNotAllowed");
        servicio.Error.Code.Should().Be("Inventory.Product.NotInventoriable");
        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    [Fact]
    public async Task Retirar_es_baja_logica_y_fijar_despues_crea_otra_fila()
    {
        var e = await EscenarioAsync();
        var primera = (await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 10m, 50m, 15m), default)).Value;

        (await new DeleteReorderPolicyCommandHandler(e.C.Db, _alcance, e.C.Reloj).Handle(new DeleteReorderPolicyCommand(primera.PublicId), default))
            .IsSuccess.Should().BeTrue();
        var segunda = (await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 5m, 20m, 8m), default)).Value;

        segunda.PublicId.Should().NotBe(primera.PublicId);
        e.C.Db.ReorderPolicies.IgnoreQueryFilters().Should().HaveCount(2);
        e.C.Db.ReorderPolicies.Should().ContainSingle();
        (await new DeleteReorderPolicyCommandHandler(e.C.Db, _alcance, e.C.Reloj).Handle(new DeleteReorderPolicyCommand(primera.PublicId), default))
            .Error.Code.Should().Be("Inventory.ReorderPolicy.NotFound");
    }

    [Fact]
    public async Task La_lista_informa_el_disponible_solo_con_permiso_de_existencias()
    {
        var e = await EscenarioAsync();
        await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 10m, 50m, 15m), default);
        var p2 = e.C.Producto(e.P2).Id;
        Existencia(e, p2, e.Pv1.Id, 12m);

        var con = await Listar(e).Handle(new ListReorderPoliciesQuery(e.Pv1.PublicId), default);
        _permisos.HasPermissionAsync("Inventory.Stock.View", Arg.Any<CancellationToken>()).Returns(false);
        var sin = await Listar(e).Handle(new ListReorderPoliciesQuery(e.Pv1.PublicId), default);

        con.Value.Items.Single().Available.Should().Be(12m);
        con.Value.Items.Single().Product.Code.Should().Be("P2");
        sin.Value.Items.Single().Available.Should().BeNull();
        sin.Value.Items.Single().Position.Should().BeNull();
    }

    [Fact]
    public async Task Sin_existencia_la_posicion_es_cero_y_queda_bajo_el_punto_de_reorden()
    {
        // US2 (T257): la posición la lee PosicionDeReposicion; sin fila en INV_StockBalances es cero.
        var e = await EscenarioAsync();
        await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 10m, 50m, 15m), default);

        var todas = await Listar(e).Handle(new ListReorderPoliciesQuery(), default);
        var bajo = await Listar(e).Handle(new ListReorderPoliciesQuery(BelowReorderPoint: true), default);

        todas.Value.TotalCount.Should().Be(1);
        todas.Value.Items.Single().Position.Should().Be(0m);
        bajo.Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Bajo_el_punto_de_reorden_deja_solo_las_que_tienen_posicion_en_o_bajo_el_punto()
    {
        // US2 (T257): el caso positivo que US1 dejó pendiente. Posición = disponible + en tránsito + por recibir (0 hasta I5).
        var e = await EscenarioAsync();
        await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 10m, 50m, 15m), default);
        var p3 = await e.C.ProductoAsync(e.C.Alta("P3", "Lenteja"));
        await Fijar(e).Handle(new SetReorderPolicyCommand(p3.PublicId, e.Pv1.PublicId, 10m, 50m, 15m), default);
        Existencia(e, e.C.Producto(e.P2).Id, e.Pv1.Id, 15m);
        Existencia(e, e.C.Producto(p3.PublicId).Id, e.Pv1.Id, 16m);

        var bajo = await Listar(e).Handle(new ListReorderPoliciesQuery(BelowReorderPoint: true), default);

        bajo.Value.Items.Should().ContainSingle().Which.Product.Code.Should().Be("P2");
        bajo.Value.Items.Single().Position.Should().Be(15m, "en el punto de reorden cuenta como bajo");
    }

    private static void Existencia(Escenario e, int producto, int bodega, decimal fisico)
    {
        e.C.Db.StockBalances.Add(new Domain.Entities.Inventory.Projections.StockBalance { ProductId = producto, WarehouseId = bodega, Physical = fisico });
        e.C.Db.SaveChanges();
    }

    [Fact]
    public async Task La_lista_filtra_por_el_alcance()
    {
        var e = await EscenarioAsync();
        await Fijar(e).Handle(new SetReorderPolicyCommand(e.P2, e.Pv1.PublicId, 10m, 50m, 15m), default);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        var r = await Listar(e).Handle(new ListReorderPoliciesQuery(), default);

        r.Value.Items.Should().BeEmpty();
        r.Value.TotalCount.Should().Be(0, "lo de afuera no cuenta");
    }
}
