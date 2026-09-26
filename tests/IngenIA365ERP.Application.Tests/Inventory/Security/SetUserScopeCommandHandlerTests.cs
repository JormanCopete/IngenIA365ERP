using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Security.Scopes;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Security;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Security;

/// <summary>
/// Feature 012, T408 (US12; T35; contracts/api.md §16.3): el reemplazo del alcance comercial de un usuario
/// (<see cref="SetUserCommercialScopeCommand"/>, <see cref="GetUserCommercialScopeQuery"/>) sobre la tabla real
/// <c>INV_UserWarehouseScopes</c> (<see cref="AsignacionesDeBodegaEnBase"/>) y sin puntos de venta todavía
/// (<see cref="SinAsignacionesDePuntoDeVenta"/>, antes de I3): reemplaza y da de baja lógica las retiradas, dos por defecto
/// es <c>Inventory.Scope.DefaultDuplicate</c>, fuera del alcance de quien administra es el 404 de la bodega, la bodega de
/// tránsito se asigna explícitamente, y <c>pointsOfSale</c> con elementos se rechaza con <c>Validation.Invalid</c>.
/// </summary>
public class SetUserScopeCommandHandlerTests
{
    private readonly IAlcanceDeInventario _alcanceDelAdministrador = Substitute.For<IAlcanceDeInventario>();
    private readonly IAutoridadDeOtroAprobador _permisos = Substitute.For<IAutoridadDeOtroAprobador>();

    public SetUserScopeCommandHandlerTests()
    {
        _alcanceDelAdministrador.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
    }

    private sealed record Escenario(CatalogoDePrueba C, User Usuario, Warehouse Prin, Warehouse Pv1, Warehouse Transito);

    private static async Task<Escenario> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var tipos = c.Db.WarehouseTypes.ToList();
        var operativo = tipos.First(t => t.Behavior == WarehouseBehavior.Operational).Id;
        var transito = tipos.First(t => t.Behavior == WarehouseBehavior.Transit).Id;
        var prin = new Warehouse { Code = "PRIN", Name = "Principal", BranchId = 1, WarehouseTypeId = operativo, Behavior = WarehouseBehavior.Operational, IsActive = true };
        var pv1 = new Warehouse { Code = "PV1", Name = "Punto 1", BranchId = 1, WarehouseTypeId = operativo, Behavior = WarehouseBehavior.Operational, IsActive = true };
        var tr = new Warehouse { Code = "TR01", Name = "Tránsito Florida", BranchId = 1, WarehouseTypeId = transito, Behavior = WarehouseBehavior.Transit, IsActive = true };
        var usuario = new User { Username = "bodega.b@coop.test", Email = "bodega.b@coop.test", PasswordHash = "x" };
        c.Db.AddRange(prin, pv1, tr, usuario);
        await c.Db.SaveChangesAsync();
        return new Escenario(c, usuario, prin, pv1, tr);
    }

    private VistaDeAlcanceComercial Vista(Escenario e) =>
        new(e.C.Db, new AsignacionesDeBodegaEnBase(e.C.Db, e.C.Reloj), new SinAsignacionesDePuntoDeVenta(), _alcanceDelAdministrador, _permisos);

    private Task<Result<UserCommercialScopeDto>> Fijar(Escenario e, IReadOnlyList<WarehouseScopeInput> bodegas, IReadOnlyList<PointOfSaleScopeInput>? puntos = null) =>
        new SetUserCommercialScopeCommandHandler(e.C.Db, new AsignacionesDeBodegaEnBase(e.C.Db, e.C.Reloj), new SinAsignacionesDePuntoDeVenta(),
                _alcanceDelAdministrador, Vista(e))
            .Handle(new SetUserCommercialScopeCommand(e.Usuario.PublicId, bodegas, puntos) { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    private static List<UserWarehouseScope> Filas(Escenario e) =>
        e.C.Db.UserWarehouseScopes.IgnoreQueryFilters().AsNoTracking().Where(s => s.UserId == e.Usuario.Id).ToList();

    [Fact]
    public async Task Reemplaza_las_asignaciones_y_da_de_baja_logica_las_retiradas()
    {
        var e = await EscenarioAsync();
        (await Fijar(e, [new(e.Prin.PublicId, true), new(e.Pv1.PublicId, false)])).IsSuccess.Should().BeTrue();

        var r = await Fijar(e, [new(e.Pv1.PublicId, true)]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Warehouses.Should().ContainSingle().Which.Should().Be(new WarehouseScopeDto(e.Pv1.PublicId, "PV1", "Punto 1", true));
        var filas = Filas(e);
        filas.Should().HaveCount(2, "la retirada no se borra");
        filas.Single(f => f.WarehouseId == e.Prin.Id).Should().Match<UserWarehouseScope>(f => f.IsDeleted && !f.IsDefault && f.DeletedAt != null);
        filas.Single(f => f.WarehouseId == e.Pv1.Id).Should().Match<UserWarehouseScope>(f => !f.IsDeleted && f.IsDefault);

        var leido = await new GetUserCommercialScopeQueryHandler(Vista(e)).Handle(new GetUserCommercialScopeQuery(e.Usuario.PublicId), default);
        leido.Value.Warehouses.Select(w => w.Code).Should().Equal("PV1");
    }

    [Fact]
    public async Task Dos_por_defecto_es_DefaultDuplicate_y_no_guarda_nada()
    {
        var e = await EscenarioAsync();

        var r = await Fijar(e, [new(e.Prin.PublicId, true), new(e.Pv1.PublicId, true)]);

        r.Error.Code.Should().Be("Inventory.Scope.DefaultDuplicate");
        r.Error.Should().BeOfType<ErrorConDatos>();
        Filas(e).Should().BeEmpty();
    }

    [Fact]
    public async Task Una_bodega_fuera_del_alcance_de_quien_administra_es_el_mismo_404_que_una_inexistente()
    {
        var e = await EscenarioAsync();
        _alcanceDelAdministrador.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(AlcanceDeInventario.Vacio with { Bodegas = new HashSet<int> { e.Pv1.Id } });

        var fuera = await Fijar(e, [new(e.Prin.PublicId, false)]);
        var inexistente = await Fijar(e, [new(Guid.NewGuid(), false)]);

        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
        inexistente.Error.Code.Should().Be(fuera.Error.Code);
        inexistente.Error.Message.Should().Be(fuera.Error.Message, "no se distingue lo ajeno de lo que no existe");
        Filas(e).Should().BeEmpty();
    }

    [Fact]
    public async Task Se_admite_asignar_explicitamente_una_bodega_de_transito()
    {
        var e = await EscenarioAsync();

        var r = await Fijar(e, [new(e.Prin.PublicId, true), new(e.Transito.PublicId, false)]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Warehouses.Select(w => w.Code).Should().Equal("PRIN", "TR01");
    }

    [Fact]
    public async Task Los_puntos_de_venta_se_rechazan_con_Validation_Invalid_mientras_no_exista_I3()
    {
        var e = await EscenarioAsync();

        var r = await Fijar(e, [new(e.Prin.PublicId, true)], [new(Guid.NewGuid(), true)]);

        r.Error.Code.Should().Be("Validation.Invalid");
        Filas(e).Should().BeEmpty("el rechazo es previo a cualquier cambio");
    }

    [Fact]
    public async Task Una_lista_vacia_de_puntos_no_es_error()
    {
        var e = await EscenarioAsync();

        var r = await Fijar(e, [new(e.Prin.PublicId, true)], []);

        r.IsSuccess.Should().BeTrue();
        r.Value.PointsOfSale.Should().BeEmpty();
    }
}
