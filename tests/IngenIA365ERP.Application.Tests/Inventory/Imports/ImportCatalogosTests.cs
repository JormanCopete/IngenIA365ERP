using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using B = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeBodegas;

namespace IngenIA365ERP.Application.Tests.Inventory.Imports;

/// <summary>
/// Feature 012, T195 (contracts/plantillas.md §2–§5, §7, §0.7): las plantillas 2 a 5 —grupos, unidades, marcas y categorías— y
/// la 7 —bodegas y ubicaciones— con la mecánica común y las reglas del alta unitaria. Categorías: el padre puede venir en
/// cualquier fila, un ciclo nombra la cadena y ninguna pasa del nivel 5. Bodegas: nacen no activas y la plantilla nunca las
/// activa; la fila de tránsito sólo acompaña a la primera bodega operativa de su sucursal (si falta, la revisión anuncia el
/// código propuesto); <c>stockNegativo</c> es una vigencia con ámbito bodega que pide motivo y permiso; al terminar, cada
/// bodega tiene exactamente una ubicación por defecto.
/// </summary>
public class ImportCatalogosTests
{
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IExistenciasParaElCatalogo _existencias = Substitute.For<IExistenciasParaElCatalogo>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    public ImportCatalogosTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _existencias.DeBodegaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ExistenciaAgregada.Ninguna);
        _existencias.DeUbicacionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ExistenciaAgregada.Ninguna);
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? "Datos", out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
    }

    private void Hoja(string nombre, string[] encabezados, params string?[][] filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private void Datos(string[] encabezados, params string?[][] filas)
    {
        _hojas.Clear();
        Hoja("Datos", encabezados, filas);
    }

    private EjecutorDeImportacion Ejecutor(CatalogoDePrueba c) =>
        new(c.Db, _lector, _permisos, new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());

    private static ArchivoDeImportacion Archivo => new("catalogo.xlsx", [1, 2, 3]);

    private async Task<Result<ImportResultDto>> Correr(CatalogoDePrueba c, Func<EjecutorDeImportacion, Task<Result<ImportResultDto>>> importar)
    {
        var r = await importar(Ejecutor(c));
        c.Olvidar();
        return r;
    }

    private Task<Result<ImportResultDto>> Grupos(CatalogoDePrueba c, ModoDeImportacion modo, string motivo = "") =>
        Correr(c, e => new ImportAccountingGroupsCommandHandler(c.Db, e).Handle(new ImportAccountingGroupsCommand(modo, Archivo, motivo), default));

    private Task<Result<ImportResultDto>> Unidades(CatalogoDePrueba c, ModoDeImportacion modo) =>
        Correr(c, e => new ImportUnitsOfMeasureCommandHandler(c.Db, e, c.Reloj).Handle(new ImportUnitsOfMeasureCommand(modo, Archivo), default));

    private Task<Result<ImportResultDto>> Marcas(CatalogoDePrueba c, ModoDeImportacion modo) =>
        Correr(c, e => new ImportBrandsCommandHandler(c.Db, e).Handle(new ImportBrandsCommand(modo, Archivo), default));

    private Task<Result<ImportResultDto>> Categorias(CatalogoDePrueba c, ModoDeImportacion modo) =>
        Correr(c, e => new ImportProductCategoriesCommandHandler(c.Db, e).Handle(new ImportProductCategoriesCommand(modo, Archivo), default));

    private Task<Result<ImportResultDto>> Bodegas(CatalogoDePrueba c, ModoDeImportacion modo, string motivo = "") =>
        Correr(c, e => new ImportWarehousesCommandHandler(c.Db, e, c.Reloj, new LectorDeParametros(c.Db), _existencias)
            .Handle(new ImportWarehousesCommand(modo, Archivo, motivo), default));

    private static string Errores(ImportResultDto r) => string.Join(" | ", r.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column} {e.Code}: {e.Message}"));

    private static void Bien(Result<ImportResultDto> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Errors.Should().BeEmpty(Errores(r.Value));
    }

    // =========================================================================================== grupos contables --

    [Fact]
    public async Task Grupos_se_crean_se_vuelven_a_subir_sin_cambio_y_no_se_inactivan_en_uso()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        string[] enc = [PlantillaDeGruposContables.Codigo, PlantillaDeGruposContables.Nombre, PlantillaDeGruposContables.Descripcion, PlantillaDeGruposContables.Activo];
        Datos(enc, ["agro", "Insumos agropecuarios", null, null], ["SERVICIOS", "Servicios", "Sin existencia", null]);

        var primera = await Grupos(c, ModoDeImportacion.Apply);
        Bien(primera);
        primera.Value.Sheets.Single().Created.Should().Be(2);
        (await c.Db.AccountingGroups.AnyAsync(g => g.Code == "AGRO")).Should().BeTrue("el código se guarda en mayúsculas");

        var otraVez = await Grupos(c, ModoDeImportacion.Apply);
        Bien(otraVez);
        otraVez.Value.Sheets.Single().Unchanged.Should().Be(2);

        await c.ProductoAsync(c.Alta("P1"));
        c.Olvidar();
        Datos(enc, ["ABARR", "Abarrotes", null, "no"]);
        var enUso = await Grupos(c, ModoDeImportacion.Review);
        enUso.Value.Errors.Should().ContainSingle(e => e.Code == "Inventory.AccountingGroup.InUse" && e.Column == PlantillaDeGruposContables.Activo);
    }

    // ================================================================================================== unidades --

    [Fact]
    public async Task Unidades_validan_el_codigo_DIAN_y_no_bajan_decimales_en_uso()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        c.Db.UnitsOfMeasure.Add(new UnitOfMeasure { Code = "KL", Name = "Kilo", AllowedDecimals = 3, DianUnitCode = "KGM", IsActive = true });
        await c.Db.SaveChangesAsync();
        var p = await c.ProductoAsync(c.Alta("P1", unidadBase: "KL"));
        await c.MovimientoAsync(p.PublicId, 1.25m);
        c.Olvidar();

        string[] enc = [PlantillaDeUnidades.Codigo, PlantillaDeUnidades.Nombre, PlantillaDeUnidades.Decimales, PlantillaDeUnidades.CodigoDian, PlantillaDeUnidades.Activo];
        Datos(enc,
            ["KL", "Kilo", "0", "KGM", null],
            ["ZZ", "Rara", "0", "QQQ", null],
            ["UND", "Unidad (sembrada)", "0", "94", null],
            ["BULTO", "Bulto", "5", "BG", null]);

        var r = await Unidades(c, ModoDeImportacion.Review);

        r.Value.Errors.Should().Contain(e => e.Row == 2 && e.Code == "Inventory.Unit.DecimalsInUse");
        r.Value.Errors.Should().Contain(e => e.Row == 3 && e.Code == "Inventory.Unit.DianCodeUnknown");
        r.Value.Errors.Should().Contain(e => e.Row == 5 && e.Code == ImportErrors.CellFormat && e.Column == PlantillaDeUnidades.Decimales);
        r.Value.Changes.Should().Contain(x => x.Key == "UND" && x.Action == AccionDeImportacion.Update, "lo sembrado se actualiza por su código");
    }

    // ==================================================================================================== marcas --

    [Fact]
    public async Task Renombrar_una_marca_recalcula_la_busqueda_de_sus_productos()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("P1", "Arroz", marca: c.Diana.PublicId));
        c.Olvidar();
        Datos([PlantillaDeMarcas.Codigo, PlantillaDeMarcas.Nombre, PlantillaDeMarcas.Activo], ["DIANA", "Arroz Diana Premium", null], ["ROA", "Roa", null]);

        var r = await Marcas(c, ModoDeImportacion.Apply);

        Bien(r);
        (await c.Db.Products.SingleAsync()).SearchText.Should().Contain("PREMIUM");
        (await c.Db.Brands.CountAsync()).Should().Be(2);
    }

    // ================================================================================================ categorías --

    private static readonly string[] EncCategorias = [PlantillaDeCategorias.Codigo, PlantillaDeCategorias.Nombre, PlantillaDeCategorias.Padre, PlantillaDeCategorias.Activo];

    [Fact]
    public async Task El_padre_puede_venir_en_cualquier_fila_y_la_ruta_queda_escrita()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        Datos(EncCategorias, ["ARROZ", "Arroz", "GRANOS", null], ["GRANOS", "Granos", "ALIM", null], ["ALIM", "Alimentos", null, null]);

        var r = await Categorias(c, ModoDeImportacion.Apply);

        Bien(r);
        var todas = await c.Db.ProductCategories.ToDictionaryAsync(x => x.Code);
        todas["ARROZ"].Level.Should().Be(3);
        todas["ARROZ"].ParentId.Should().Be(todas["GRANOS"].Id);
        todas["ARROZ"].Path.Should().Be($"/{todas["ALIM"].Id}/{todas["GRANOS"].Id}/{todas["ARROZ"].Id}/");

        var otraVez = await Categorias(c, ModoDeImportacion.Apply);
        Bien(otraVez);
        otraVez.Value.Sheets.Single().Unchanged.Should().Be(3);
    }

    [Fact]
    public async Task Un_ciclo_nombra_la_cadena_y_ninguna_pasa_del_nivel_5()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        Datos(EncCategorias,
            ["CA", "A", "CB", null], ["CB", "B", "CA", null],
            ["N1", "1", null, null], ["N2", "2", "N1", null], ["N3", "3", "N2", null], ["N4", "4", "N3", null], ["N5", "5", "N4", null], ["N6", "6", "N5", null]);

        var r = await Categorias(c, ModoDeImportacion.Review);

        r.Value.Errors.Should().Contain(e => e.Row == 2 && e.Code == "Inventory.Category.Cycle" && e.Message.Contains("CA › CB › CA"));
        r.Value.Errors.Should().Contain(e => e.Row == 9 && e.Code == "Inventory.Category.TooDeep");
        r.Value.Errors.Where(e => e.Code == "Inventory.Category.TooDeep").Should().HaveCount(6, "la rama entera de N1 a N6 pasa del nivel 5: " + Errores(r.Value));
        (await c.Db.ProductCategories.CountAsync()).Should().Be(1, "revisar no guarda nada");
    }

    [Fact]
    public async Task Mover_una_rama_existente_cuenta_la_profundidad_de_sus_hijas()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        Datos(EncCategorias, ["N1", "1", null, null], ["N2", "2", "N1", null], ["N3", "3", "N2", null], ["N4", "4", "N3", null]);
        Bien(await Categorias(c, ModoDeImportacion.Apply));

        // Bajar N1 debajo de dos niveles nuevos dejaría a N4 en el nivel 6.
        Datos(EncCategorias, ["R1", "Raíz", null, null], ["R2", "Otra", "R1", null], ["N1", "1", "R2", null]);
        var r = await Categorias(c, ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Row == 4 && e.Code == "Inventory.Category.TooDeep", Errores(r.Value));
    }

    // ========================================================================================= bodegas y ubicaciones --

    private static readonly string[] EncBodegas = [B.Codigo, B.Nombre, B.Sucursal, B.Tipo, B.StockNegativo, B.Activa];
    private static readonly string[] EncUbicaciones = [B.Bodega, B.Codigo, B.Nombre, B.PorDefecto, B.Activa];

    private static async Task<(Branch Florida, Branch Palmira)> SucursalesAsync(CatalogoDePrueba c)
    {
        var florida = new Branch { Name = "Florida", LegacyCode = "01", MunicipalityDaneCode = "76275" };
        var palmira = new Branch { Name = "Palmira", LegacyCode = "02", MunicipalityDaneCode = null };
        c.Db.Branches.AddRange(florida, palmira);
        await c.Db.SaveChangesAsync();
        c.Olvidar();
        return (florida, palmira);
    }

    private void Bodegas(params string?[][] filas)
    {
        _hojas.Clear();
        Hoja(B.HojaBodegas, EncBodegas, filas);
        Hoja(B.HojaUbicaciones, EncUbicaciones);
    }

    [Fact]
    public async Task Las_bodegas_nacen_no_activas_y_la_fila_de_transito_fija_el_codigo_de_la_primera()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await SucursalesAsync(c);
        Bodegas(
            ["B01", "Bodega principal", "01", "PRINCIPAL", null, null],
            ["PV01", "Almacén Florida", "Florida", "PUNTOVENTA", null, null],
            ["TRF", "Tránsito Florida", "01", "TRANSITO", null, null],
            ["B02", "Bodega Palmira", "02", "PRINCIPAL", null, null]);

        var revision = await Bodegas(c, ModoDeImportacion.Review);
        Bien(revision);
        var anunciadas = revision.Value.Extra[ImportWarehousesCommandHandler.ExtraDeTransito]
            .Should().BeAssignableTo<IEnumerable<BodegaDeTransitoPropuestaDto>>().Subject.ToList();
        anunciadas.Should().Contain(t => t.Code == "TRF" && t.FromFile);
        anunciadas.Should().Contain(t => t.Code == "TR02" && !t.FromFile, "sin fila de tránsito, la revisión anuncia el código propuesto");
        revision.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Branch.MunicipalityMissing", "Palmira no tiene municipio");

        var r = await Bodegas(c, ModoDeImportacion.Apply);
        Bien(r);
        var bodegas = await c.Db.Warehouses.Include(w => w.Locations).ToListAsync();
        bodegas.Select(w => w.Code).Should().BeEquivalentTo(["B01", "PV01", "TRF", "B02", "TR02"]);
        bodegas.Should().OnlyContain(w => w.ActivationStatus == WarehouseActivationStatus.NotActivated, "la plantilla nunca activa");
        bodegas.Single(w => w.Code == "TRF").Behavior.Should().Be(WarehouseBehavior.Transit);
        bodegas.Should().OnlyContain(w => w.Locations.Count(l => l.IsDefault) == 1);
    }

    [Fact]
    public async Task Una_fila_de_transito_sin_su_primera_bodega_es_del_sistema()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await SucursalesAsync(c);
        Bodegas(["B01", "Bodega principal", "01", "PRINCIPAL", null, null]);
        Bien(await Bodegas(c, ModoDeImportacion.Apply));

        Bodegas(["TRX", "Otro tránsito", "01", "tránsito", null, null]);
        var r = await Bodegas(c, ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == "Inventory.WarehouseType.TransitIsSystem" && e.Sheet == B.HojaBodegas, Errores(r.Value));
    }

    [Fact]
    public async Task StockNegativo_es_una_vigencia_por_bodega_que_pide_motivo_y_permiso()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await SucursalesAsync(c);
        Bodegas(["B01", "Bodega principal", "01", "PRINCIPAL", "sí", null]);

        var revision = await Bodegas(c, ModoDeImportacion.Review);
        revision.Value.RequiresReason.Should().BeTrue();

        var sinMotivo = await Bodegas(c, ModoDeImportacion.Apply);
        sinMotivo.IsFailure.Should().BeTrue();
        var datos = (ImportResultDto)((ErrorConDatos)sinMotivo.Error).Data!;
        datos.Errors.Should().ContainSingle(e => e.Code == ImportErrors.CellRequired && e.Column == "reason" && e.Row == 0);
        (await c.Db.Warehouses.CountAsync()).Should().Be(0);

        var conMotivo = await Bodegas(c, ModoDeImportacion.Apply, "Bodega de consignación");
        Bien(conMotivo);
        var bodega = await c.Db.Warehouses.SingleAsync(w => w.Code == "B01");
        var vigencia = await c.Db.ParameterVersions.SingleAsync(v => v.ScopeKind == ParameterScopeKind.Warehouse);
        (vigencia.ScopeId, vigencia.Value, vigencia.Key, vigencia.Reason).Should()
            .Be((bodega.Id, "true", ParametrosDeInventario.ExistenciasStockNegativoPermitido, "Bodega de consignación"));
        vigencia.ValidFrom.Should().Be(CatalogoDePrueba.Hoy);

        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(["Inventory.Warehouses.Manage"]));
        Bodegas(["B01", "Bodega principal", "01", "PRINCIPAL", "no", null]);
        var sinPermiso = await Bodegas(c, ModoDeImportacion.Review);
        sinPermiso.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.CellPermissionRequired && e.Column == B.StockNegativo
            && e.Message.Contains(B.PermisoDeParametros));
    }

    [Fact]
    public async Task Al_terminar_cada_bodega_tiene_exactamente_una_ubicacion_por_defecto()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await SucursalesAsync(c);
        Bodegas(["B01", "Bodega principal", "01", "PRINCIPAL", null, null]);
        Hoja(B.HojaUbicaciones, EncUbicaciones,
            ["B01", "A-01", "Pasillo A", "sí", null],
            ["B01", "PATIO", "Patio de cargue", "no", null],
            ["TR01", "X", "En tránsito", null, null]);

        var r = await Bodegas(c, ModoDeImportacion.Review);
        r.Value.Errors.Should().ContainSingle(e => e.Code == "Inventory.Location.TransitHasOnlyDefault", Errores(r.Value));

        Hoja(B.HojaUbicaciones, EncUbicaciones, ["B01", "A-01", "Pasillo A", "sí", null], ["B01", "PATIO", "Patio de cargue", "sí", null]);
        var dos = await Bodegas(c, ModoDeImportacion.Review);
        dos.Value.Errors.Should().ContainSingle(e => e.Row == 3 && e.Column == B.PorDefecto);

        Hoja(B.HojaUbicaciones, EncUbicaciones, ["B01", "A-01", "Pasillo A", "sí", null], ["B01", "PATIO", "Patio de cargue", null, null]);
        Bien(await Bodegas(c, ModoDeImportacion.Apply));
        var ubicaciones = await c.Db.WarehouseLocations.Where(l => l.Warehouse!.Code == "B01").ToListAsync();
        ubicaciones.Should().HaveCount(3);
        ubicaciones.Should().ContainSingle(l => l.IsDefault).Which.Code.Should().Be("A-01");
    }
}
