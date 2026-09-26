using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, US11, T383 (FR-040, US11-1; contracts/api.md §12, <c>POST /counts/{id}/open</c>): abrir copia la foto de
/// <c>INV_StockDetails</c> del alcance (todo, categoría, ubicación, selección) a <c>INV_CountSnapshotLines</c> con el costo de su ámbito,
/// guarda <c>CountSnapshotAt</c> y <c>CountSnapshotKardexEntryId</c> sin número, toma la bodega en exclusivo y sella
/// <c>Conteo.BloquearMovimientos</c>; un producto ya en otro conteo abierto de la bodega es <c>Inventory.Count.Overlaps</c>, un alcance
/// sin existencias <c>.EmptyScope</c> y la clase ABC <c>.ScopeNotAvailable</c>.
/// </summary>
public class AbrirConteoTests
{
    [Fact]
    public async Task Abrir_copia_la_foto_de_la_ubicacion_sin_numero_y_sella_el_bloqueo()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        await c.EntradaAsync(c.K.P1, 3m, 1_000m, ubicacion: c.A01);
        var ultimoKardex = c.Db.KardexEntries.Max(k => k.Id);

        var definido = await c.DefinirAsync(c.Definicion(CountScope.Location));
        definido.IsSuccess.Should().BeTrue();
        definido.Value.State.Should().Be(EstadosDeConteo.Borrador);
        var abierto = await c.AbrirAsync(definido.Value.PublicId);

        abierto.IsSuccess.Should().BeTrue(abierto.IsFailure ? abierto.Error.Code : string.Empty);
        abierto.Value.Lines.Should().Be(2, "P1 y P2 en GENERAL; P1 en A-01 queda fuera de la ubicación");
        abierto.Value.BlocksMovements.Should().BeTrue("Conteo.BloquearMovimientos es verdadero por defecto");
        var conteo = c.Documento(definido.Value.PublicId);
        conteo.Status.Should().Be(DocumentStatus.Draft);
        conteo.Number.Should().BeNull("el conteo se numera al cerrar");
        conteo.CountSnapshotAt.Should().NotBeNull();
        conteo.CountSnapshotKardexEntryId.Should().Be(ultimoKardex);
        conteo.CountRound.Should().Be(1);
        var lineas = c.Lineas(definido.Value.PublicId);
        lineas.Should().HaveCount(2).And.OnlyContain(l => l.LocationId == c.General.Id && !l.AddedDuringCapture);
        lineas.Single(l => l.ProductId == c.K.ProductoId(c.K.P1)).Should().BeEquivalentTo(new { TheoreticalQuantity = 10m, SnapshotUnitCost = 1_000m },
            o => o.ExcludingMissingMembers());
        CriterioDelConteo.De(conteo.CountScopeJson).AbiertoPor.Should().Be(ConteosDePrueba.Jefe);
        c.K.Bloqueos.Should().Contain(p => p.BodegasEnExclusivo && p.Bodegas.Contains(c.PRIN.Id), "espera las confirmaciones en vuelo sobre la bodega");
    }

    [Theory]
    [InlineData(CountScope.All, 3)]
    [InlineData(CountScope.Category, 3)]
    [InlineData(CountScope.Selection, 2)]
    public async Task Cada_alcance_copia_lo_suyo(CountScope alcance, int lineasEsperadas)
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P1, 3m, 1_000m, ubicacion: c.A01);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        await c.EntradaAsync(c.K.P2, 9m, 500m, bodega: c.B2);

        var conteo = await c.AbiertoAsync(c.Definicion(alcance));

        c.Lineas(conteo).Should().HaveCount(lineasEsperadas, "la foto es de PRIN; la selección es sólo P1 (en GENERAL y A-01)");
        c.Lineas(conteo).Should().OnlyContain(l => c.Db.WarehouseLocations.Single(u => u.Id == l.LocationId).WarehouseId == c.PRIN.Id);
    }

    [Fact]
    public async Task Sin_bloqueo_sella_que_admite_movimientos()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoBloquearMovimientos, "false", c.PRIN);
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);

        var conteo = await c.AbiertoAsync();

        CriterioDelConteo.De(c.Documento(conteo).CountScopeJson).BloqueaMovimientos.Should().BeFalse();
    }

    [Fact]
    public async Task Un_producto_en_otro_conteo_abierto_de_la_bodega_es_Overlaps()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        var primero = await c.AbiertoAsync(c.Definicion(CountScope.Selection, productos: [c.K.P1]));

        var segundo = await c.DefinirAsync(c.Definicion(CountScope.All));
        var r = await c.AbrirAsync(segundo.Value.PublicId);

        r.Error.Code.Should().Be("Inventory.Count.Overlaps");
        r.Error.Should().BeOfType<Application.Common.Models.ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { countPublicId = primero, products = new[] { "P1" } });
        c.Lineas(segundo.Value.PublicId).Should().BeEmpty("no quedó nada de la foto");

        var enB2 = await c.DefinirAsync(c.Definicion(CountScope.Selection, bodega: c.B2, productos: [c.K.P1]));
        await c.EntradaAsync(c.K.P1, 2m, 1_000m, bodega: c.B2);
        (await c.AbrirAsync(enB2.Value.PublicId)).IsSuccess.Should().BeTrue("en otra bodega el mismo producto sí se cuenta");
    }

    [Fact]
    public async Task Un_alcance_sin_existencias_es_EmptyScope_y_la_clase_ABC_ScopeNotAvailable()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);

        var vacio = await c.DefinirAsync(c.Definicion(CountScope.Location, ubicaciones: [c.A01.PublicId]));
        (await c.AbrirAsync(vacio.Value.PublicId)).Error.Code.Should().Be("Inventory.Count.EmptyScope");

        var abc = await c.DefinirAsync(c.Definicion(CountScope.AbcClass));
        abc.IsSuccess.Should().BeTrue("la definición la admite; la clase ABC llega en I6");
        (await c.AbrirAsync(abc.Value.PublicId)).Error.Code.Should().Be("Inventory.Count.ScopeNotAvailable");
    }

    [Fact]
    public async Task Abrir_dos_veces_es_AlreadyOpen_y_la_definicion_abierta_no_se_edita()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync();

        (await c.AbrirAsync(conteo)).Error.Code.Should().Be("Inventory.Count.AlreadyOpen");
        var editado = await new UpdatePhysicalCountCommandHandler(c.Db, c.K.C.Reloj, c.Vista(), new DefinicionDeConteo(c.Db, c.K.Maestros(), c.K.Alcance), c.Detalle())
            .Handle(new UpdatePhysicalCountCommand(conteo, c.Definicion(CountScope.All)), default);
        editado.Error.Code.Should().Be("Inventory.Count.AlreadyOpen");
    }

    [Fact]
    public async Task La_definicion_valida_bodega_y_criterio()
    {
        var c = await ConteosDePrueba.CrearAsync();
        (await c.DefinirAsync(c.Definicion(bodega: c.K.Transito))).Error.Code.Should().Be("Inventory.Document.TransitNotAllowed");

        c.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Application.Common.Interfaces.Security.AlcanceDeInventario(false,
            new HashSet<int> { c.B2.Id }, null, false, new HashSet<int>(), null));
        (await c.DefinirAsync(c.Definicion())).Error.Code.Should().Be("Inventory.Warehouse.NotFound", "fuera del alcance, el mismo 404");
        c.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Application.Common.Interfaces.Security.AlcanceDeInventario.Total);

        (await c.DefinirAsync(c.Definicion(CountScope.Location, ubicaciones: [Guid.NewGuid()]))).Error.Code.Should().Be("Inventory.Location.NotFound");
        var enOtraBodega = c.Db.WarehouseLocations.Single(l => l.WarehouseId == c.B2.Id && l.IsDefault);
        (await c.DefinirAsync(c.Definicion(CountScope.Location, ubicaciones: [enOtraBodega.PublicId]))).Error.Code
            .Should().Be("Inventory.Location.NotInWarehouse");
        (await c.DefinirAsync(c.Definicion(contadores: [Guid.NewGuid()]))).Error.Code.Should().Be("Validation.Invalid");

        var conContadores = await c.DefinirAsync(c.Definicion(contadores: [c.UsuarioContadorA.PublicId]));
        conContadores.Value.Counters.Should().ContainSingle().Which.Name.Should().Be("bodega.a");
        (await c.Db.InventoryDocuments.CountAsync(d => d.Class == DocumentClass.PhysicalCount)).Should().Be(1);
    }
}
