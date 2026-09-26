using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Inventory.Replenishment;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, US17, T946 (FR-086, FR-087; contracts/api.md §27): las vistas básicas de I1, <c>documents</c> y
/// <c>reorder-alerts</c> —columnas y ocultas exactas de §27, filtros comunes y propios, alcance por bodega de origen <b>o</b>
/// destino, el total de lo valorado al costo vacío sin <c>Inventory.Costs.Read</c> con la nota que lo dice, <c>documents</c> con
/// contraparte marca la necesidad de <c>Inventory.Reports.ExportPersonalData</c>, y el rango de más de cinco años—.
/// </summary>
public class VistasBasicasTests
{
    private static string[] Nombres(TablaExportable t) => t.Columnas.Select(c => c.Clave ?? c.Nombre).ToArray();

    private static object? Celda(TablaExportable t, FilaExportable f, string columna)
    {
        var i = t.Columnas.Select((c, i) => (c, i)).First(x => (x.c.Clave ?? x.c.Nombre) == columna).i;
        return f.Valores[i];
    }

    private static DocumentsReportQueryHandler Documentos(ReposicionDePrueba r) => new(r.K.C.Db, r.K.Alcance, r.K.Vista(), r.K.C.Reloj);

    private static ReorderAlertsReportQueryHandler Reorden(ReposicionDePrueba r) => new(r.K.C.Db, r.K.Alcance, r.Evaluacion());

    private static Task<Result<TablaExportable>> DocumentosAsync(ReposicionDePrueba r, FiltrosDeInformeDeInventario? f = null,
        DocumentClass? clase = null, DocumentStatus? estado = null) =>
        Documentos(r).Handle(new DocumentsReportQuery(f ?? new FiltrosDeInformeDeInventario(), clase, estado), default);

    // --------------------------------------------------------------------------------------------- documents --

    [Fact]
    public void Documents_tiene_las_columnas_de_la_27()
    {
        Nombres(new TablaExportable("", "", DocumentsReportQueryHandler.Columnas, [], null, [])).Should().Equal(
            "Fecha", "Clase", "Tipo", "Número", "Estado", "Bodega", "Bodega destino", "Contraparte", "Total", "Modo de paso",
            "Estado del mensaje", "Creado por", "Confirmado por", "_documento");
        DocumentsReportQueryHandler.Vista.Key.Should().Be("documents");
        DocumentsReportQueryHandler.Vista.OwnFilters.Should().BeEquivalentTo(["class", "status"]);
        DocumentsReportQueryHandler.Vista.Filters.Should().Contain(["warehouse", "documentType", "person"]);
    }

    [Fact]
    public async Task Documents_lista_con_clase_estado_numero_y_bodega_y_filtra_por_clase_y_estado()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        var entrada = await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 2m)]));
        await k.GuardarAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 1m)]));

        var todos = (await DocumentosAsync(r)).Value;
        todos.Filas.Should().HaveCount(3);
        var primera = todos.Filas.Single(f => (string?)Celda(todos, f, "_documento") == entrada.ToString());
        Celda(todos, primera, "Clase").Should().Be("Ajuste positivo");
        Celda(todos, primera, "Tipo").Should().Be("AJP");
        Celda(todos, primera, "Número").Should().Be("AJP1");
        Celda(todos, primera, "Estado").Should().Be("Confirmado");
        Celda(todos, primera, "Bodega").Should().Be("PRIN");
        Celda(todos, primera, "Fecha").Should().Be(Catalog.CatalogoDePrueba.Hoy);

        (await DocumentosAsync(r, clase: DocumentClass.NegativeAdjustment)).Value.Filas.Should().HaveCount(2);
        var borradores = (await DocumentosAsync(r, estado: DocumentStatus.Draft)).Value;
        borradores.Filas.Should().ContainSingle();
        Celda(borradores, borradores.Filas[0], "Estado").Should().Be("Borrador");
        Celda(borradores, borradores.Filas[0], "Número").Should().BeNull("un borrador no tiene número");

        var tipo = k.Tipo("AJP").PublicId;
        (await DocumentosAsync(r, new FiltrosDeInformeDeInventario { DocumentType = tipo })).Value.Filas.Should().ContainSingle();
    }

    [Fact]
    public async Task Documents_respeta_el_alcance_por_bodega_de_origen_o_de_destino()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 10m, 1000m, k.Segunda);
        r.EnTransito(k.P1, k.Segunda, k.Principal, 3m);
        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Principal.Id }, k.Principal.Id, false, new HashSet<int>(), null));

        var tabla = (await DocumentosAsync(r)).Value;

        tabla.Filas.Should().ContainSingle("el ajuste de B2 no está en su alcance; el despacho hacia PRIN sí, por su destino");
        Celda(tabla, tabla.Filas[0], "Bodega destino").Should().Be("PRIN");

        var fuera = await DocumentosAsync(r, new FiltrosDeInformeDeInventario { Warehouse = k.Segunda.PublicId });
        fuera.IsFailure.Should().BeTrue("filtrar por una bodega fuera del alcance es el 404 de la bodega");
    }

    [Fact]
    public async Task Sin_permiso_de_costos_el_total_de_lo_valorado_al_costo_sale_vacio_y_la_nota_lo_dice()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        await k.EntradaAsync(k.P1, 10m, 1000m);

        var con = (await DocumentosAsync(r)).Value;
        Celda(con, con.Filas[0], "Total").Should().Be(10000m);
        con.Notas.Should().NotContain(DocumentsReportQueryHandler.NotaSinCostos);

        k.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);
        var sin = (await DocumentosAsync(r)).Value;
        Celda(sin, sin.Filas[0], "Total").Should().BeNull();
        sin.Notas.Should().Contain(DocumentsReportQueryHandler.NotaSinCostos);
    }

    [Fact]
    public async Task Documents_con_contraparte_marca_la_necesidad_de_exportar_datos_personales()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        var documento = await k.EntradaAsync(k.P1, 10m, 1000m);

        var sinContraparte = (await DocumentosAsync(r)).Value;
        DocumentsReportQueryHandler.Vista.TablaTraeDatosPersonales(sinContraparte).Should().BeFalse();

        var id = k.C.Db.InventoryDocuments.Single(d => d.PublicId == documento).Id;
        k.C.Db.DocumentPartySnapshots.Add(new DocumentPartySnapshot { DocumentId = id, PersonId = 1, LegalName = "Proveedor S.A.S.", TaxId = "900123456" });
        await k.C.Db.SaveChangesAsync();

        var conContraparte = (await DocumentosAsync(r)).Value;
        Celda(conContraparte, conContraparte.Filas[0], "Contraparte").Should().Be("Proveedor S.A.S. (900123456)");
        DocumentsReportQueryHandler.Vista.TablaTraeDatosPersonales(conContraparte).Should().BeTrue();
        DocumentsReportQueryHandler.Vista.PersonalData.Should().BeFalse("sólo con contraparte, no siempre");
    }

    [Fact]
    public async Task Un_rango_de_mas_de_cinco_anios_no_se_consulta()
    {
        var r = await ReposicionDePrueba.CrearAsync();

        var resultado = await DocumentosAsync(r, new FiltrosDeInformeDeInventario { From = new DateOnly(2020, 1, 1), To = new DateOnly(2026, 1, 1) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(FiltrosDeInformeDeInventario.RangoDemasiadoLargoCodigo);
    }

    // ---------------------------------------------------------------------------------------- reorder-alerts --

    [Fact]
    public void Reorder_alerts_tiene_las_columnas_de_la_27()
    {
        Nombres(new TablaExportable("", "", ReorderAlertsReportQueryHandler.Columnas, [], null, [])).Should().Equal(
            "Bodega", "Producto", "Disponible", "En tránsito", "Por recibir", "Posición", "Mínimo", "Punto de reorden", "Máximo",
            "Sugerido", "Quiebre", "_producto", "_bodega");
        ReorderAlertsReportQueryHandler.Vista.Key.Should().Be("reorder-alerts");
        ReorderAlertsReportQueryHandler.Vista.Filters.Should().Contain(["warehouse", "category"]);
    }

    [Fact]
    public async Task Reorder_alerts_con_12_y_5_esta_vacia_y_con_8_y_5_trae_el_sugerido_37_y_el_quiebre()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        r.Politica(k.P2, k.Principal);
        r.Existencia(k.P2, k.Principal, 12m);
        r.EnTransito(k.P2, k.Segunda, k.Principal, 5m);

        (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario()), default)).Value.Filas.Should().BeEmpty();

        r.Existencia(k.P2, k.Principal, 8m);
        var tabla = (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;

        var fila = tabla.Filas.Should().ContainSingle().Which;
        fila.Valores.Should().Equal("PRIN", "P2 · Frijol", 8m, 5m, 0m, 13m, 10m, 15m, 50m, 37m, "Sí",
            k.C.Producto(k.P2).PublicId.ToString(), k.Principal.PublicId.ToString());
        fila.Resaltada.Should().BeTrue("el quiebre se resalta");
    }

    [Fact]
    public async Task Reorder_alerts_filtra_por_bodega_y_categoria_y_respeta_el_alcance()
    {
        var r = await ReposicionDePrueba.CrearAsync();
        var k = r.K;
        r.Politica(k.P1, k.Principal);
        r.Politica(k.P1, k.Segunda);
        r.Politica(k.P2, k.Segunda);

        (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario()), default)).Value.Filas.Should().HaveCount(3);
        (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario { Warehouse = k.Segunda.PublicId }), default))
            .Value.Filas.Should().HaveCount(2);
        (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario { Category = k.C.Abarrotes.PublicId }), default))
            .Value.Filas.Should().HaveCount(3, "P1 y P2 son de Abarrotes");
        (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario { Category = Guid.NewGuid() }), default))
            .Value.Filas.Should().BeEmpty();

        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Principal.Id }, k.Principal.Id, false, new HashSet<int>(), null));
        var propia = (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;
        propia.Filas.Should().ContainSingle().Which.Valores[0].Should().Be("PRIN");
        (await Reorden(r).Handle(new ReorderAlertsReportQuery(new FiltrosDeInformeDeInventario { Warehouse = k.Segunda.PublicId }), default))
            .IsFailure.Should().BeTrue("una bodega fuera del alcance es el 404 de la bodega");
    }
}
