using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Security.Scopes;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Warehouses;

/// <summary>
/// Feature 012, T192 (FR-032; contracts/api.md §4.1–§4.3, §4.5; data-model §2.1–§2.3): la primera bodega operativa de una
/// sucursal trae su bodega de tránsito en la misma transacción (código <c>TR</c> + sucursal o el que fije quien crea); el
/// tránsito no se da de alta aparte; toda bodega nace no activa con su ubicación <c>GENERAL</c>; el tipo sólo cambia por
/// otro del mismo comportamiento; no se inactiva con existencia; el aviso de municipio; las ubicaciones (una por defecto,
/// sólo la por defecto en tránsito, ninguna con existencia); los tipos en uso. Y el alcance por bodega sobre
/// <c>INV_UserWarehouseScopes</c> (T224) y el ámbito de parámetro por bodega (T226).
/// </summary>
public class WarehouseCommandsTests
{
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IExistenciasParaElCatalogo _existencias = Substitute.For<IExistenciasParaElCatalogo>();

    public WarehouseCommandsTests()
    {
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _existencias.DeBodegaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ExistenciaAgregada.Ninguna);
        _existencias.DeUbicacionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ExistenciaAgregada.Ninguna);
    }

    private sealed record Escenario(CatalogoDePrueba C, Branch Florida, Branch Palmira, WarehouseType Principal, WarehouseType PuntoDeVenta, WarehouseType Transito);

    private static async Task<Escenario> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var florida = new Branch { Name = "Florida", LegacyCode = "01", MunicipalityDaneCode = "76275" };
        var palmira = new Branch { Name = "Palmira", LegacyCode = null, MunicipalityDaneCode = null };
        c.Db.Branches.AddRange(florida, palmira);
        await c.Db.SaveChangesAsync();
        var tipos = c.Db.WarehouseTypes.ToDictionary(t => t.Code);
        return new Escenario(c, florida, palmira, tipos["PRINCIPAL"], tipos["PUNTOVENTA"], tipos[WarehouseTypesSeeder.CodigoDeTransito]);
    }

    private VistaDeBodegas Vista(CatalogoDePrueba c) => new(c.Db, _alcance, new LectorDeParametros(c.Db), c.Reloj);

    private Task<Result<CreateWarehouseResultDto>> CrearAsync(Escenario e, string codigo, Branch sucursal, WarehouseType? tipo = null, BodegaDeTransitoPedida? transito = null) =>
        new CreateWarehouseCommandHandler(e.C.Db, Vista(e.C))
            .Handle(new CreateWarehouseCommand(codigo, codigo + " bodega", sucursal.PublicId, (tipo ?? e.Principal).PublicId, null, transito), default);

    private static ErrorConDatos ConDatos(Error e) => e.Should().BeOfType<ErrorConDatos>().Subject;

    // ------------------------------------------------------------------------ alta y tránsito --

    [Fact]
    public async Task La_primera_bodega_de_la_sucursal_trae_su_transito_con_el_codigo_propuesto()
    {
        var e = await EscenarioAsync();

        var r = await CrearAsync(e, "prin", e.Florida);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Warehouse.Code.Should().Be("PRIN");
        r.Value.Warehouse.ActivationStatus.Should().Be(WarehouseActivationStatus.NotActivated);
        r.Value.Warehouse.Locations.Should().ContainSingle(l => l.Code == "GENERAL" && l.IsDefault);
        r.Value.Warehouse.TransitWarehouse!.Code.Should().Be("TR01");
        r.Value.TransitWarehouseCreated!.Code.Should().Be("TR01");
        var transito = e.C.Db.Warehouses.Single(w => w.Code == "TR01");
        transito.Behavior.Should().Be(WarehouseBehavior.Transit);
        transito.ActivationStatus.Should().Be(WarehouseActivationStatus.NotActivated);
        e.C.Db.WarehouseLocations.Count(l => l.WarehouseId == transito.Id && l.IsDefault).Should().Be(1);
        r.Value.Warnings.Should().BeEmpty("Florida tiene municipio");
    }

    [Fact]
    public async Task Quien_crea_la_primera_puede_fijar_el_codigo_del_transito_y_en_otra_no()
    {
        var e = await EscenarioAsync();

        var primera = await CrearAsync(e, "PV2", e.Palmira, e.PuntoDeVenta, new BodegaDeTransitoPedida("trpal", "Tránsito Palmira"));
        var segunda = await CrearAsync(e, "PV3", e.Palmira, e.PuntoDeVenta, new BodegaDeTransitoPedida("TROTRA", null));

        primera.Value.TransitWarehouseCreated!.Code.Should().Be("TRPAL");
        segunda.Error.Code.Should().Be("Validation.Invalid");
        e.C.Db.Warehouses.Count(w => w.BranchId == e.Palmira.Id && w.Behavior == WarehouseBehavior.Transit).Should().Be(1);
    }

    [Fact]
    public async Task La_segunda_bodega_no_crea_otro_transito()
    {
        var e = await EscenarioAsync();
        await CrearAsync(e, "PRIN", e.Florida);

        var r = await CrearAsync(e, "PV1", e.Florida, e.PuntoDeVenta);

        r.Value.TransitWarehouseCreated.Should().BeNull();
        e.C.Db.Warehouses.Count(w => w.Behavior == WarehouseBehavior.Transit).Should().Be(1);
    }

    [Fact]
    public async Task Sin_codigo_de_sucursal_hay_que_fijar_el_del_transito()
    {
        var e = await EscenarioAsync();

        var r = await CrearAsync(e, "PV2", e.Palmira, e.PuntoDeVenta);

        r.Error.Code.Should().Be("Validation.Invalid");
        e.C.Db.Warehouses.Should().BeEmpty("no quedó nada a medias");
    }

    [Fact]
    public async Task Una_bodega_de_tipo_transito_no_se_da_de_alta_a_mano()
    {
        var e = await EscenarioAsync();

        var bodega = await CrearAsync(e, "TRX", e.Florida, e.Transito);
        var tipo = await new CreateWarehouseTypeCommandHandler(e.C.Db).Handle(new CreateWarehouseTypeCommand("TRANS2", "Otro tránsito", WarehouseBehavior.Transit), default);

        bodega.Error.Code.Should().Be("Inventory.WarehouseType.TransitIsSystem");
        tipo.Error.Code.Should().Be("Inventory.WarehouseType.TransitIsSystem");
    }

    [Fact]
    public async Task Sin_municipio_en_la_sucursal_la_respuesta_avisa()
    {
        var e = await EscenarioAsync();

        var r = await CrearAsync(e, "PV2", e.Palmira, e.PuntoDeVenta, new BodegaDeTransitoPedida("TRPAL", null));

        var aviso = r.Value.Warnings.Should().ContainSingle().Subject;
        aviso.Code.Should().Be("Inventory.Branch.MunicipalityMissing");
        aviso.Data.Should().BeEquivalentTo(new { branchPublicId = e.Palmira.PublicId });
    }

    // ------------------------------------------------------------------------ edición y baja --

    [Fact]
    public async Task El_tipo_solo_cambia_por_otro_del_mismo_comportamiento()
    {
        var e = await EscenarioAsync();
        var prin = (await CrearAsync(e, "PRIN", e.Florida)).Value.Warehouse;
        var editar = new UpdateWarehouseCommandHandler(e.C.Db, _alcance, Vista(e.C));

        var aTransito = await editar.Handle(new UpdateWarehouseCommand(prin.PublicId, "Principal", e.Transito.PublicId), default);
        var aPunto = await editar.Handle(new UpdateWarehouseCommand(prin.PublicId, "Principal", e.PuntoDeVenta.PublicId), default);

        aTransito.Error.Code.Should().Be("Inventory.Warehouse.BehaviorLocked");
        aPunto.Value.Type.Code.Should().Be("PUNTOVENTA");
    }

    [Fact]
    public async Task Con_existencia_la_bodega_no_se_inactiva()
    {
        var e = await EscenarioAsync();
        var creada = (await CrearAsync(e, "PRIN", e.Florida)).Value;
        var prin = e.C.Db.Warehouses.Single(w => w.Code == "PRIN");
        var transito = e.C.Db.Warehouses.Single(w => w.Code == "TR01");
        _existencias.DeBodegaAsync(prin.Id, Arg.Any<CancellationToken>()).Returns(new ExistenciaAgregada(3, 42m));
        _existencias.DeBodegaAsync(transito.Id, Arg.Any<CancellationToken>()).Returns(new ExistenciaAgregada(1, 10m));
        var cambiar = new SetWarehouseActiveCommandHandler(e.C.Db, _alcance, _existencias);

        var r = await cambiar.Handle(new SetWarehouseActiveCommand(creada.Warehouse.PublicId, false, "Cierre"), default);
        var t = await cambiar.Handle(new SetWarehouseActiveCommand(creada.TransitWarehouseCreated!.PublicId, false, "Cierre"), default);

        r.Error.Code.Should().Be("Inventory.Warehouse.HasStock");
        ConDatos(r.Error).Data.Should().BeEquivalentTo(new { products = 3, quantity = 42m });
        t.Error.Code.Should().Be("Inventory.Warehouse.TransitHasStock");
    }

    [Fact]
    public async Task Fuera_del_alcance_la_bodega_no_existe()
    {
        var e = await EscenarioAsync();
        var prin = (await CrearAsync(e, "PRIN", e.Florida)).Value.Warehouse;
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        var r = await new GetWarehouseQueryHandler(e.C.Db, _alcance, Vista(e.C)).Handle(new GetWarehouseQuery(prin.PublicId), default);
        var lista = await new ListWarehousesQueryHandler(e.C.Db, _alcance, Vista(e.C)).Handle(new ListWarehousesQuery(IncludeTransit: true), default);

        r.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
        lista.Value.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------ ubicaciones --

    [Fact]
    public async Task Las_ubicaciones_tienen_una_sola_por_defecto_y_el_transito_solo_la_suya()
    {
        var e = await EscenarioAsync();
        var creada = (await CrearAsync(e, "PRIN", e.Florida)).Value;
        var crear = new CreateWarehouseLocationCommandHandler(e.C.Db, _alcance);

        var enTransito = await crear.Handle(new CreateWarehouseLocationCommand(creada.TransitWarehouseCreated!.PublicId, "A-01", "Pasillo"), default);
        var pasillo = await crear.Handle(new CreateWarehouseLocationCommand(creada.Warehouse.PublicId, "a-01", "Pasillo A", IsDefault: true), default);
        var repetida = await crear.Handle(new CreateWarehouseLocationCommand(creada.Warehouse.PublicId, "A-01", "Otra"), default);

        enTransito.Error.Code.Should().Be("Inventory.Location.TransitHasOnlyDefault");
        pasillo.Value.IsDefault.Should().BeTrue();
        repetida.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        var prin = e.C.Db.Warehouses.Single(w => w.Code == "PRIN");
        e.C.Db.WarehouseLocations.Where(l => l.WarehouseId == prin.Id && l.IsDefault).Select(l => l.Code).Should().Equal("A-01");
    }

    [Fact]
    public async Task La_ubicacion_por_defecto_no_se_inactiva_ni_se_desmarca_y_ninguna_con_existencia()
    {
        var e = await EscenarioAsync();
        var creada = (await CrearAsync(e, "PRIN", e.Florida)).Value;
        var general = creada.Warehouse.Locations!.Single();
        var patio = (await new CreateWarehouseLocationCommandHandler(e.C.Db, _alcance)
            .Handle(new CreateWarehouseLocationCommand(creada.Warehouse.PublicId, "PATIO", "Patio de cargue"), default)).Value;
        var patioId = e.C.Db.WarehouseLocations.Single(l => l.PublicId == patio.PublicId).Id;
        _existencias.DeUbicacionAsync(patioId, Arg.Any<CancellationToken>()).Returns(new ExistenciaAgregada(1, 5m));
        var cambiar = new SetWarehouseLocationActiveCommandHandler(e.C.Db, _alcance, _existencias);
        var editar = new UpdateWarehouseLocationCommandHandler(e.C.Db, _alcance);

        (await cambiar.Handle(new SetWarehouseLocationActiveCommand(creada.Warehouse.PublicId, general.PublicId, false, "x"), default))
            .Error.Code.Should().Be("Inventory.Location.IsDefault");
        (await editar.Handle(new UpdateWarehouseLocationCommand(creada.Warehouse.PublicId, general.PublicId, "General", false), default))
            .Error.Code.Should().Be("Inventory.Location.IsDefault");
        (await cambiar.Handle(new SetWarehouseLocationActiveCommand(creada.Warehouse.PublicId, patio.PublicId, false, "x"), default))
            .Error.Code.Should().Be("Inventory.Location.HasStock");
        (await editar.Handle(new UpdateWarehouseLocationCommand(creada.Warehouse.PublicId, patio.PublicId, "Patio", true), default))
            .Value.IsDefault.Should().BeTrue("marcar otra desmarca la anterior");
        e.C.Db.WarehouseLocations.Single(l => l.PublicId == general.PublicId).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Un_tipo_con_bodegas_activas_no_se_inactiva()
    {
        var e = await EscenarioAsync();
        await CrearAsync(e, "PRIN", e.Florida);

        var r = await new SetWarehouseTypeActiveCommandHandler(e.C.Db).Handle(new SetWarehouseTypeActiveCommand(e.Principal.PublicId, false, "x"), default);

        r.Error.Code.Should().Be("Inventory.WarehouseType.InUse");
    }

    // --------------------------------------------------------------- alcance y ámbito de parámetro --

    [Fact]
    public async Task Las_asignaciones_de_bodega_se_leen_y_se_reemplazan_sobre_la_tabla()
    {
        var e = await EscenarioAsync();
        await CrearAsync(e, "PRIN", e.Florida);
        await CrearAsync(e, "PV1", e.Florida, e.PuntoDeVenta);
        var prin = e.C.Db.Warehouses.Single(w => w.Code == "PRIN");
        var pv1 = e.C.Db.Warehouses.Single(w => w.Code == "PV1");
        var puerto = new AsignacionesDeBodegaEnBase(e.C.Db, e.C.Reloj);

        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(prin.Id, true), new AsignacionPedida(pv1.Id, true)], default))
            .Error.Code.Should().Be("Inventory.Scope.DefaultDuplicate");
        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(99999, false)], default)).Error.Code.Should().Be("Inventory.Warehouse.NotFound");
        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(prin.Id, true), new AsignacionPedida(pv1.Id, false)], default)).IsSuccess.Should().BeTrue();
        await e.C.Db.SaveChangesAsync();
        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(pv1.Id, true)], default)).IsSuccess.Should().BeTrue();
        await e.C.Db.SaveChangesAsync();

        var vigentes = await puerto.BodegasDelUsuarioAsync(7, default);
        vigentes.Ids.Should().BeEquivalentTo(new[] { pv1.Id });
        vigentes.PorDefecto.Should().Be(pv1.Id);
        (await puerto.BuscarAsync([prin.PublicId, Guid.NewGuid()], default)).Keys.Should().Equal(prin.PublicId);
    }

    [Fact]
    public async Task El_ambito_bodega_de_un_parametro_respeta_el_alcance()
    {
        var e = await EscenarioAsync();
        var prin = (await CrearAsync(e, "PRIN", e.Florida)).Value.Warehouse;
        var reglas = new ReglasDePlataformaDeInventario(e.C.Db, _alcance);

        var resuelta = await reglas.ResolverAsync(ParameterScopeKind.Warehouse, prin.PublicId, default);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);
        var fuera = await reglas.ResolverAsync(ParameterScopeKind.Warehouse, prin.PublicId, default);
        var otroAmbito = await reglas.ResolverAsync(ParameterScopeKind.DocumentType, prin.PublicId, default);

        resuelta.Value.Code.Should().Be("PRIN");
        resuelta.Value.Id.Should().Be(e.C.Db.Warehouses.Single(w => w.Code == "PRIN").Id);
        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
        otroAmbito.IsFailure.Should().BeTrue("el tipo de documento lo resuelve US3");
    }
}
