using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeProductos;

namespace IngenIA365ERP.Application.Tests.Inventory.Imports;

/// <summary>
/// Feature 012, T194 (US1-1, US1-4, US1-6; FR-030; contracts/plantillas.md §0.5, §6; quickstart §3.2): la plantilla 6 con la
/// mecánica común —revisar no guarda, aplicar es todo o nada— y las reglas del alta unitaria. El libro de 500 filas con tres
/// errores (código de barras de otro producto, unidad inexistente, tarifa inexistente) se revisa con los tres errores por hoja,
/// fila y columna, no se aplica, y corregido crea las 500; volver a subirlo no cambia nada. Lo de I6 responde
/// <c>Import.Cell.NotYetAvailable</c>, un dígito de control errado sólo avisa, y el grupo contable de un producto sin
/// movimientos se cambia como una edición (con movimientos, la reclasificación de US3).
/// </summary>
public class ImportProductsCommandTests
{
    private static readonly string[] EncabezadosProductos =
    [
        P.Codigo, P.Nombre, P.Tipo, P.Categoria, P.Marca, P.UnidadBase, P.GrupoContable, P.Estado, P.ControlaLote, P.ControlaSerie,
        P.ControlaVencimiento, P.TratamientoIva, P.TarifaIva, P.ConceptoRetencion, P.Referencia, P.Peso, P.Volumen,
    ];

    private static readonly string[] EncabezadosCodigos = [P.Producto, P.CodigoDeBarras, P.Unidad];
    private static readonly string[] EncabezadosUnidades = [P.Producto, P.Unidad, P.Factor, P.Uso];
    private static readonly string[] EncabezadosImpuestos = [P.Producto, P.Tarifa, P.UnidadesGravables];

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    public ImportProductsCommandTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? string.Empty, out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
    }

    private void Hoja(string nombre, string[] encabezados, IEnumerable<string?[]> filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private void Productos(params string?[][] filas) => Hoja(P.HojaProductos, EncabezadosProductos, filas);

    private void Codigos(params string?[][] filas) => Hoja(P.HojaCodigos, EncabezadosCodigos, filas);

    private void Unidades(params string?[][] filas) => Hoja(P.HojaUnidades, EncabezadosUnidades, filas);

    private void Impuestos(params string?[][] filas) => Hoja(P.HojaImpuestos, EncabezadosImpuestos, filas);

    private static string?[] Producto(string codigo, string nombre = "Producto", string tipo = "Inventoriable", string categoria = "ABARROTES",
        string? marca = null, string unidad = "UND", string? grupo = "ABARR", string? estado = null, string? lote = null,
        string iva = "Excluded", string? tarifaIva = null, string concepto = "COMPRAS", string? referencia = null) =>
        [codigo, nombre, tipo, categoria, marca, unidad, grupo, estado, lote, null, null, iva, tarifaIva, concepto, referencia, null, null];

    private async Task<Result<ImportResultDto>> ImportarAsync(CatalogoDePrueba c, ModoDeImportacion modo, string motivo = "")
    {
        foreach (var (hoja, enc) in new[] { (P.HojaCodigos, EncabezadosCodigos), (P.HojaUnidades, EncabezadosUnidades), (P.HojaImpuestos, EncabezadosImpuestos) })
            if (!_hojas.ContainsKey(hoja)) Hoja(hoja, enc, []);
        var servicios = new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(c.Db, _lector, _permisos, servicios);
        var r = await new ImportProductsCommandHandler(c.Db, ejecutor, c.Reloj, Reclasificacion(c))
            .Handle(new ImportProductsCommand(modo, new ArchivoDeImportacion("productos.xlsx", [1, 2, 3]), motivo), default);
        c.Olvidar();
        return r;
    }

    /// <summary>La reclasificación real (US3) con el cerrojo y el actor sustituidos.</summary>
    private static ReclasificacionDeGrupo Reclasificacion(CatalogoDePrueba c)
    {
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(),
            "catalogo@coop.co", "catalogo@coop.co", ExecutionChannel.Web, "POST /api/inventory/products/import", "10.0.0.1", null));
        var lector = new LectorDeParametros(c.Db);
        return new ReclasificacionDeGrupo(c.Db, Substitute.For<ICerrojoDeInventario>(), new ValorizadoALaFecha(c.Db, lector),
            new EmisorDeMensajes(c.Db, actor, c.Reloj), lector, c.Reloj);
    }

    private static string Errores(ImportResultDto r) => string.Join(" | ", r.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column} {e.Code}: {e.Message}"));

    // --------------------------------------------------------------------------------------------- la plantilla --

    [Fact]
    public void La_plantilla_6_declara_sus_cuatro_hojas_y_es_la_de_CatalogoDePlantillas()
    {
        P.Definicion.Hojas.Select(h => h.Nombre).Should().Equal(P.HojaProductos, P.HojaCodigos, P.HojaUnidades, P.HojaImpuestos);
        P.Definicion.Hojas.Should().OnlyContain(h => h.Columnas.Count > 0);
        CatalogoDePlantillas.Por(CatalogoDePlantillas.ProductosClave).Definicion.Should().BeSameAs(P.Definicion);
        P.Definicion.Hoja(P.HojaProductos)!.Columna(P.Codigo)!.Largo.Should().Be(20, "el código de producto admite 20 (D1)");
    }

    // ------------------------------------------------------------------------------------- el libro de 500 filas --

    [Fact]
    public async Task El_libro_de_500_filas_con_tres_errores_se_revisa_no_se_aplica_y_corregido_crea_las_500()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        await c.ProductoAsync(c.Alta("EXIST", "Aceite existente", codigos: [new CodigoPedido("DUP-001", null)]));
        c.Olvidar();

        List<string?[]> Filas(bool conErrores) => Enumerable.Range(1, 500).Select(i =>
        {
            var codigo = $"P{i:000}";
            return (conErrores, i) switch
            {
                (true, 20) => Producto(codigo, $"Producto {i}", unidad: "NOEXISTE"),
                (true, 30) => Producto(codigo, $"Producto {i}", iva: "Taxed", tarifaIva: "IVA99"),
                _ => Producto(codigo, $"Producto {i}", iva: i % 2 == 0 ? "Taxed" : "Excluded", tarifaIva: i % 2 == 0 ? "IVA19" : null),
            };
        }).ToList();
        List<string?[]> Barras(bool conErrores) => Enumerable.Range(1, 500)
            .Select(i => (string?[])[$"P{i:000}", conErrores && i == 10 ? "DUP-001" : $"B-{i:0000}", null]).ToList();

        Productos([.. Filas(conErrores: true)]);
        Codigos([.. Barras(conErrores: true)]);

        var revision = await ImportarAsync(c, ModoDeImportacion.Review);
        revision.IsSuccess.Should().BeTrue();
        revision.Value.Valid.Should().BeFalse();
        revision.Value.Errors.Should().HaveCount(3, Errores(revision.Value));
        revision.Value.Errors.Should().Contain(e => e.Sheet == P.HojaProductos && e.Row == 21 && e.Column == P.UnidadBase && e.Code == ImportErrors.CellNotFound
            && e.Message.Contains("NOEXISTE") && e.Message.Contains("Unidades"));
        revision.Value.Errors.Should().Contain(e => e.Sheet == P.HojaProductos && e.Row == 31 && e.Column == P.TarifaIva && e.Code == ImportErrors.CellNotFound);
        revision.Value.Errors.Should().Contain(e => e.Sheet == P.HojaCodigos && e.Row == 11 && e.Column == P.CodigoDeBarras
            && e.Code == "Inventory.Barcode.Duplicate" && e.Message.Contains("EXIST"));
        (await c.ContarAsync<Product>()).Should().Be(1, "revisar no guarda nada");

        var aplicacion = await ImportarAsync(c, ModoDeImportacion.Apply);
        aplicacion.IsFailure.Should().BeTrue();
        aplicacion.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await c.ContarAsync<Product>()).Should().Be(1, "con un solo error no se guarda nada");
        (await c.ContarAsync<ProductBarcode>()).Should().Be(1);

        Productos([.. Filas(conErrores: false)]);
        Codigos([.. Barras(conErrores: false)]);
        var corregida = await ImportarAsync(c, ModoDeImportacion.Apply);
        corregida.IsSuccess.Should().BeTrue(corregida.IsFailure ? corregida.Error.Message : string.Empty);
        corregida.Value.Applied.Should().BeTrue();
        corregida.Value.Sheets.Single(h => h.Sheet == P.HojaProductos).Created.Should().Be(500);
        corregida.Value.Sheets.Single(h => h.Sheet == P.HojaCodigos).Created.Should().Be(500);
        (await c.ContarAsync<Product>()).Should().Be(501);
        var p2 = await c.Db.Products.Include(p => p.Taxes).Include(p => p.Barcodes).SingleAsync(p => p.Code == "P002");
        p2.Taxes.Should().ContainSingle(t => t.TaxRateCode == "IVA19");
        p2.SearchText.Should().Contain("B-0002").And.Contain("PRODUCTO 2");

        var otraVez = await ImportarAsync(c, ModoDeImportacion.Apply);
        otraVez.IsSuccess.Should().BeTrue(otraVez.IsFailure ? otraVez.Error.Message : string.Empty);
        otraVez.Value.Sheets.Should().OnlyContain(h => h.Created == 0 && h.Updated == 0);
        otraVez.Value.Sheets.Single(h => h.Sheet == P.HojaProductos).Unchanged.Should().Be(500);
        (await c.ContarAsync<Product>()).Should().Be(501, "volver a subir el mismo archivo no duplica nada");
    }

    // ---------------------------------------------------------------------------------------- reglas del alta --

    [Fact]
    public async Task Una_fila_existente_actualiza_con_las_mismas_reglas_del_alta_unitaria()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var existente = await c.ProductoAsync(c.Alta("P1", "Arroz"));
        await c.MovimientoAsync(existente.PublicId);
        c.Olvidar();

        Productos(Producto("P1", "Arroz Diana 500 g", referencia: "REF-9"), Producto("P2", "Otro", unidad: "KG"));
        var r = await ImportarAsync(c, ModoDeImportacion.Apply);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Changes.Should().Contain(x => x.Key == "P1" && x.Action == AccionDeImportacion.Update
            && x.Fields.Any(f => f.Column == P.Nombre && f.Before == "Arroz" && f.After == "Arroz Diana 500 g"));
        (await c.Db.Products.SingleAsync(p => p.Code == "P1")).SearchText.Should().Contain("REF-9");

        // Con movimientos la unidad base no cambia: el mismo código que la edición unitaria.
        Productos(Producto("P1", "Arroz Diana 500 g", unidad: "KG"));
        var bloqueada = await ImportarAsync(c, ModoDeImportacion.Review);
        bloqueada.Value.Errors.Should().ContainSingle(e => e.Column == P.UnidadBase && e.Code == "Inventory.Product.BaseUnitLocked");

        // Sin concepto de retención: el mismo código que el alta (la celda es obligatoria en la plantilla).
        Productos(Producto("P9", "Sin grupo", grupo: null));
        var sinGrupo = await ImportarAsync(c, ModoDeImportacion.Review);
        sinGrupo.Value.Errors.Should().ContainSingle(e => e.Column == P.GrupoContable && e.Code == "Inventory.Product.AccountingGroupRequired");
    }

    [Fact]
    public async Task Las_clases_y_el_seguimiento_de_I6_no_estan_disponibles()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        Productos(Producto("K1", tipo: "Combo"), Producto("L1", lote: "sí"), Producto("V1", tipo: "variante"));

        var r = await ImportarAsync(c, ModoDeImportacion.Review);

        r.Value.Errors.Should().HaveCount(3, Errores(r.Value));
        r.Value.Errors.Should().OnlyContain(e => e.Code == ImportErrors.CellNotYetAvailable && e.Message.Contains("I6"));
        r.Value.Errors.Select(e => e.Column).Should().BeEquivalentTo([P.Tipo, P.ControlaLote, P.Tipo]);
    }

    [Fact]
    public async Task Un_digito_de_control_errado_avisa_sin_bloquear_y_el_empaque_lleva_su_unidad()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        c.Db.UnitsOfMeasure.Add(new UnitOfMeasure { Code = "CAJA12", Name = "Caja x 12", AllowedDecimals = 0, IsActive = true });
        await c.Db.SaveChangesAsync();
        c.Olvidar();

        Productos(Producto("P1", "Aceite"));
        Unidades(["P1", "CAJA12", "12", "compra"]);
        Codigos(["P1", "7702001000015", null], ["P1", "7702001000012", "CAJA12"]);

        var r = await ImportarAsync(c, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Warnings.Should().ContainSingle(w => w.Code == ImportProductsCommandHandler.AvisoDeDigitoDeControl && w.Row == 2 && w.Sheet == P.HojaCodigos);
        var p = await c.Db.Products.Include(x => x.Units).ThenInclude(u => u.Unit).Include(x => x.Barcodes).ThenInclude(b => b.ProductUnit).SingleAsync();
        p.Units.Should().ContainSingle(u => u.Unit!.Code == "CAJA12" && u.Factor == 12m && u.Usage == ProductUnitUsage.Purchase && u.IsDefaultPurchase);
        p.Barcodes.Single(b => b.Barcode == "7702001000012").ProductUnit!.Factor.Should().Be(12m);
        p.Barcodes.Single(b => b.Barcode == "7702001000015").ProductUnitId.Should().BeNull();
        ImportProductsCommandHandler.DigitoDeControlErrado("7702001000012").Should().BeFalse();
    }

    [Fact]
    public async Task La_unidad_base_no_es_alterna_y_el_impuesto_por_unidad_pide_sus_unidades_gravables()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        Productos(Producto("P1", "Bolsa"));
        Unidades(["P1", "UND", "1", "Both"]);
        Impuestos(["P1", "BOLSA", null], ["P1", "RFCOMP25", null]);

        var r = await ImportarAsync(c, ModoDeImportacion.Review);

        r.Value.Errors.Should().Contain(e => e.Sheet == P.HojaUnidades && e.Code == "Inventory.ProductUnit.IsBaseUnit");
        r.Value.Errors.Should().Contain(e => e.Sheet == P.HojaImpuestos && e.Row == 2 && e.Code == "Inventory.ProductTax.UnitsRequired");
        r.Value.Errors.Should().Contain(e => e.Sheet == P.HojaImpuestos && e.Row == 3 && e.Code == "Inventory.ProductTax.WithholdingNotAllowed");
    }

    // ------------------------------------------------------------------------------------------ grupo contable --

    [Fact]
    public async Task Cambiar_el_grupo_de_un_producto_sin_movimientos_es_una_edicion()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        c.Db.AccountingGroups.Add(new AccountingGroup { Code = "AGRO", Name = "Agro", IsActive = true });
        await c.Db.SaveChangesAsync();
        await c.ProductoAsync(c.Alta("P1", "Arroz"));
        c.Olvidar();

        Productos(Producto("P1", "Arroz", grupo: "AGRO"));
        var r = await ImportarAsync(c, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.RequiresReason.Should().BeFalse();
        r.Value.Changes.Should().ContainSingle(x => x.Fields.Any(f => f.Column == P.GrupoContable && f.Before == "ABARR" && f.After == "AGRO"));
        (await c.Db.Products.Include(p => p.AccountingGroup).SingleAsync()).AccountingGroup!.Code.Should().Be("AGRO");
    }

    [Fact]
    public async Task Con_movimientos_el_grupo_cambia_por_la_reclasificacion_con_permiso_y_motivo()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        c.Db.AccountingGroups.Add(new AccountingGroup { Code = "AGRO", Name = "Agro", IsActive = true });
        await c.Db.SaveChangesAsync();
        var p = await c.ProductoAsync(c.Alta("P1", "Arroz"));
        await c.MovimientoAsync(p.PublicId);
        c.Olvidar();
        Productos(Producto("P1", "Arroz", grupo: "AGRO"));

        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(["Inventory.Catalog.Import"]));
        var sinPermiso = await ImportarAsync(c, ModoDeImportacion.Review);
        sinPermiso.Value.Errors.Should().ContainSingle(e => e.Column == P.GrupoContable && e.Code == ImportErrors.CellPermissionRequired
            && e.Message.Contains(P.PermisoDeReclasificar));

        _permisos.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<string>>(["Inventory.Catalog.Import", P.PermisoDeReclasificar]));
        var conPermiso = await ImportarAsync(c, ModoDeImportacion.Review);
        conPermiso.Value.RequiresReason.Should().BeTrue("sin motivo en la fila, el de la importación");
        conPermiso.Value.Errors.Should().BeEmpty(Errores(conPermiso.Value));
        conPermiso.Value.Changes.Should().ContainSingle(x => x.Fields.Any(f => f.Column == P.GrupoContable && f.Before == "ABARR" && f.After == "AGRO"));
        (await c.Db.ProductAccountingGroupChanges.CountAsync()).Should().Be(0, "revisar no reclasifica");

        // US3 (T288): la aplicación reclasifica con la misma regla que el comando, con fecha efectiva hoy.
        var aplicada = await ImportarAsync(c, ModoDeImportacion.Apply, "Cambio de línea comercial");
        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? aplicada.Error.Message : string.Empty);
        var cambio = await c.Db.ProductAccountingGroupChanges.SingleAsync();
        cambio.EffectiveDate.Should().Be(CatalogoDePrueba.Hoy);
        cambio.Reason.Should().Be("Cambio de línea comercial");
        cambio.Quantity.Should().Be(0m, "sin kardex no hay existencia que reclasificar");
        (await c.Db.Products.SingleAsync(x => x.Code == "P1")).AccountingGroupId.Should().Be(cambio.ToAccountingGroupId);
    }
}
