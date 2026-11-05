using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeActualizacionDeProductos;

namespace IngenIA365ERP.Application.Tests.Inventory.Imports;

/// <summary>
/// Feature 016: la actualización masiva de productos, por plantilla y por formulario, sobre el mismo <see cref="ActualizadorDeProductos"/>. Una celda
/// vacía no cambia nada, revisar no guarda y aplicar es todo o nada.
/// </summary>
public class ActualizacionMasivaDeProductosTests
{
    private static readonly string[] Encabezados =
    [
        P.Codigo, P.Nombre, P.UnidadBase, P.GrupoContable, P.Estado, P.TratamientoIva, P.TarifaIva, P.ListaDePrecios, P.Precio,
    ];

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private TablaLeida _tabla = new([], [], "xlsx");

    public ActualizacionMasivaDeProductosTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
    }

    private static string?[] Fila(string codigo, string? nombre = null, string? unidad = null, string? grupo = null, string? estado = null,
        string? iva = null, string? tarifa = null, string? lista = null, string? precio = null) =>
        [codigo, nombre, unidad, grupo, estado, iva, tarifa, lista, precio];

    private EjecutorDeImportacion Ejecutor(CatalogoDePrueba c) =>
        new(c.Db, _lector, _permisos, new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());

    private static ActualizadorDeProductos Actualizador(CatalogoDePrueba c)
    {
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(),
            "catalogo@coop.co", "catalogo@coop.co", ExecutionChannel.Web, "POST /api/inventory/products/bulk-update", "10.0.0.1", null));
        var lector = new LectorDeParametros(c.Db);
        return new ActualizadorDeProductos(c.Db, c.Reloj, new ReclasificacionDeGrupo(c.Db, Substitute.For<ICerrojoDeInventario>(),
            new ValorizadoALaFecha(c.Db, lector), new EmisorDeMensajes(c.Db, actor, c.Reloj), lector, c.Reloj));
    }

    private async Task<Result<ImportResultDto>> PlantillaAsync(CatalogoDePrueba c, ModoDeImportacion modo, string motivo, params string?[][] filas)
    {
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
        var r = await new ImportProductUpdatesCommandHandler(Ejecutor(c), Actualizador(c))
            .Handle(new ImportProductUpdatesCommand(modo, new ArchivoDeImportacion("actualizar.xlsx", [1, 2, 3]), motivo), default);
        c.Olvidar();
        return r;
    }

    private async Task<Result<ImportResultDto>> FormularioAsync(CatalogoDePrueba c, ModoDeImportacion modo, CambiosMasivos? cambios = null, PrecioMasivo? precio = null,
        FiltroDeProductos? filtro = null, IReadOnlyList<string>? excluidos = null)
    {
        var r = await new BulkUpdateProductsCommandHandler(c.Db, Ejecutor(c), Actualizador(c))
            .Handle(new BulkUpdateProductsCommand(modo, filtro, cambios, precio, excluidos, "ajuste de implantación"), default);
        c.Olvidar();
        return r;
    }

    private static async Task<PriceList> ListaAsync(CatalogoDePrueba c, string codigo = "GENERAL")
    {
        var lista = new PriceList { Code = codigo, Name = codigo, ValidFrom = CatalogoDePrueba.Hoy.AddDays(-30), IsActive = true };
        c.Db.PriceLists.Add(lista);
        await c.Db.SaveChangesAsync();
        c.Olvidar();
        return lista;
    }

    private static string Errores(ImportResultDto r) => string.Join(" | ", r.Errors.Select(e => $"{e.Row} {e.Column} {e.Code}: {e.Message}"));

    [Fact]
    public async Task Una_celda_vacia_no_cambia_nada_y_la_plantilla_no_crea_productos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1", "Aceite", referencia: "REF1"));
        c.Olvidar();

        (await PlantillaAsync(c, ModoDeImportacion.Apply, "", Fila("P1"))).Value.Sheets.Single().Unchanged.Should().Be(1);
        var producto = c.Producto(p.PublicId);
        (producto.Name, producto.Reference).Should().Be(("Aceite", "REF1"));

        var r = await PlantillaAsync(c, ModoDeImportacion.Review, "", Fila("NOEXISTE", nombre: "X"));
        r.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.CellNotFound && e.Column == P.Codigo);
    }

    [Fact]
    public async Task Revisar_no_guarda_y_aplicar_cambia_unidad_e_iva()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1"));
        c.Olvidar();
        var fila = Fila("P1", unidad: "KG", iva: "gravado", tarifa: "IVA19");

        var revision = await PlantillaAsync(c, ModoDeImportacion.Review, "", fila);

        revision.Value.Valid.Should().BeTrue(Errores(revision.Value));
        revision.Value.Changes.Single().Fields.Select(f => f.Column).Should().BeEquivalentTo([P.UnidadBase, P.TratamientoIva, P.TarifaIva]);
        c.Producto(p.PublicId).VatSaleTreatment.Should().Be(VatSaleTreatment.Excluded, "revisar no guarda");

        (await PlantillaAsync(c, ModoDeImportacion.Apply, "", fila)).IsSuccess.Should().BeTrue();

        var producto = await c.Db.Products.Include(x => x.BaseUnit).Include(x => x.Taxes).SingleAsync(x => x.PublicId == p.PublicId);
        producto.BaseUnit!.Code.Should().Be("KG");
        producto.VatSaleTreatment.Should().Be(VatSaleTreatment.Taxed);
        producto.Taxes.Should().ContainSingle(t => !t.IsDeleted && t.TaxRateCode == "IVA19");
    }

    [Fact]
    public async Task Pasar_a_excluido_retira_el_iva_y_un_gravado_sin_tarifa_es_error()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var gravado = await c.ProductoAsync(c.Alta("G1", tratamiento: VatSaleTreatment.Taxed, impuestos: c.Iva19()));
        await c.ProductoAsync(c.Alta("E1"));
        c.Olvidar();

        (await PlantillaAsync(c, ModoDeImportacion.Apply, "", Fila("G1", iva: "excluido"))).IsSuccess.Should().BeTrue();
        (await c.Db.Products.Include(x => x.Taxes).SingleAsync(x => x.PublicId == gravado.PublicId)).Taxes.Should().OnlyContain(t => t.IsDeleted);

        var r = await PlantillaAsync(c, ModoDeImportacion.Review, "", Fila("E1", iva: "gravado"));
        r.Value.Errors.Should().ContainSingle(e => e.Column == P.TarifaIva);
    }

    [Fact]
    public async Task Con_movimientos_la_unidad_no_cambia_y_el_grupo_pide_motivo()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var p = await c.ProductoAsync(c.Alta("P1"));
        await c.MovimientoAsync(p.PublicId);
        c.Db.AccountingGroups.Add(new AccountingGroup { Code = "ACEITES", Name = "Aceites", IsActive = true });
        await c.Db.SaveChangesAsync();
        c.Olvidar();

        var unidad = await PlantillaAsync(c, ModoDeImportacion.Review, "", Fila("P1", unidad: "KG"));
        unidad.Value.Errors.Should().ContainSingle(e => e.Column == P.UnidadBase);

        var grupo = await PlantillaAsync(c, ModoDeImportacion.Review, "", Fila("P1", grupo: "ACEITES"));
        grupo.Value.Valid.Should().BeTrue(Errores(grupo.Value));
        grupo.Value.RequiresReason.Should().BeTrue();
    }

    [Fact]
    public async Task El_precio_va_con_su_lista_y_exige_el_permiso_de_precios()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("P1"));
        await ListaAsync(c);
        c.Olvidar();

        (await PlantillaAsync(c, ModoDeImportacion.Review, "", Fila("P1", precio: "1000"))).Value.Errors
            .Should().ContainSingle(e => e.Code == ImportErrors.CellRequired);

        (await PlantillaAsync(c, ModoDeImportacion.Apply, "", Fila("P1", lista: "GENERAL", precio: "1000"))).IsSuccess.Should().BeTrue();
        (await c.Db.PriceListItems.SingleAsync()).Price.Should().Be(1000m);

        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(["Inventory.Catalog.Import"]);
        (await PlantillaAsync(c, ModoDeImportacion.Review, "", Fila("P1", lista: "GENERAL", precio: "1200"))).Value.Errors
            .Should().Contain(e => e.Code == ImportErrors.CellPermissionRequired);
    }

    [Fact]
    public async Task El_formulario_aplica_a_lo_que_filtra_menos_lo_excluido()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var a = await c.ProductoAsync(c.Alta("ACE1", "Aceite uno"));
        var b = await c.ProductoAsync(c.Alta("ACE2", "Aceite dos"));
        var d = await c.ProductoAsync(c.Alta("ARZ1", "Arroz"));
        c.Olvidar();

        var r = await FormularioAsync(c, ModoDeImportacion.Apply, new CambiosMasivos(BaseUnit: "KG"), filtro: new FiltroDeProductos(Search: "aceite"), excluidos: ["ACE2"]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var kg = c.Unidad("KG").Id;
        c.Producto(a.PublicId).BaseUnitId.Should().Be(kg);
        c.Producto(b.PublicId).BaseUnitId.Should().NotBe(kg);
        c.Producto(d.PublicId).BaseUnitId.Should().NotBe(kg);
    }

    [Fact]
    public async Task El_formulario_sube_un_porcentaje_redondeado_y_avisa_al_que_no_tiene_precio()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var a = await c.ProductoAsync(c.Alta("P1"));
        await c.ProductoAsync(c.Alta("P2"));
        await PlantillaPrecioAsync(c, a.PublicId);

        var r = await FormularioAsync(c, ModoDeImportacion.Apply, precio: new PrecioMasivo("GENERAL", Percentage: 10m, RoundTo: 50));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        (await c.Db.PriceListItems.SingleAsync()).Price.Should().Be(1150m, "1.030 + 10 % = 1.133, al múltiplo de 50");
        r.Value.Warnings.Should().ContainSingle(w => w.Code == BulkUpdateProductsCommandHandler.AvisoSinPrecio);
    }

    private async Task PlantillaPrecioAsync(CatalogoDePrueba c, Guid producto)
    {
        await ListaAsync(c);
        (await PlantillaAsync(c, ModoDeImportacion.Apply, "", Fila(c.Producto(producto).Code, lista: "GENERAL", precio: "1030"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_revision_del_formulario_nombra_los_productos_con_error_para_quitarlos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var conMovimientos = await c.ProductoAsync(c.Alta("MOV"));
        await c.ProductoAsync(c.Alta("LIBRE"));
        await c.MovimientoAsync(conMovimientos.PublicId);
        c.Olvidar();

        var r = await FormularioAsync(c, ModoDeImportacion.Review, new CambiosMasivos(BaseUnit: "KG"));

        r.Value.Valid.Should().BeFalse();
        r.Value.Extra[BulkUpdateProductsCommandHandler.ExtraConError].Should().BeEquivalentTo(new List<string> { "MOV" });
    }

    [Fact]
    public async Task Sin_productos_que_cumplan_el_filtro_no_corre()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        var r = await FormularioAsync(c, ModoDeImportacion.Review, new CambiosMasivos(Status: "Inactive"), filtro: new FiltroDeProductos(Search: "nada"));

        r.Error.Code.Should().Be(BulkUpdateProductsCommandHandler.CodigoSinProductos);
    }
}
