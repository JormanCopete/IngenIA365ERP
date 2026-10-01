using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.API.Reports.Importadores;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.API.IntegrationTests.Reports;

/// <summary>
/// Feature 012, T155 y T158 (contracts/plantillas.md §0.3, §0.5): el lector abre una hoja por su nombre y lista las del
/// libro, y la plantilla arma libros de varias hojas con sus encabezados en la fila 1, la hoja «Instrucciones», la
/// descarga con datos y el libro de la revisión con <c>resultado</c> y <c>errores</c>. Sin contenedores: sólo ClosedXML.
/// </summary>
public class PlantillasDeVariasHojasTests
{
    private static readonly DefinicionDePlantilla Plantilla = new("test.p", "Productos de prueba", "Inventory",
    [
        new HojaDePlantilla("Productos",
        [
            new("codigo", TipoDeValor.Codigo, Obligatoria: true, Largo: 20, Ejemplo: "ARZ-001"),
            new("nombre", TipoDeValor.Texto, Obligatoria: true, Largo: 120),
            new("costo", TipoDeValor.Costo),
            new("tarifa", TipoDeValor.Porcentaje),
        ]),
        new HojaDePlantilla("CodigosDeBarras", [new("producto", TipoDeValor.Codigo), new("codigoDeBarras", TipoDeValor.Codigo)], Obligatoria: false),
    ]);

    private readonly ClosedXmlTabularFileReader _lector = new();

    [Fact]
    public async Task El_libro_trae_una_hoja_por_seccion_con_encabezados_en_la_fila_1_e_instrucciones_al_final()
    {
        var archivo = PlantillaDeImportacion.Xlsx(Plantilla, "p");

        var hojas = await _lector.ListarHojasAsync(archivo.Contenido, archivo.NombreArchivo);
        hojas.Value.Should().Equal("Productos", "CodigosDeBarras", PlantillaDeImportacion.HojaDeInstrucciones);

        var productos = await _lector.LeerHojaAsync(archivo.Contenido, archivo.NombreArchivo, "productos");
        productos.Value.Encabezados.Should().Equal("codigo", "nombre", "costo", "tarifa");
        productos.Value.Filas.Should().BeEmpty();

        using var libro = new XLWorkbook(new MemoryStream(archivo.Contenido));
        var ws = libro.Worksheet("Productos");
        ws.Column(1).Style.NumberFormat.Format.Should().Be("@");
        ws.Column(3).Style.NumberFormat.Format.Should().Be("0.000000");
        ws.Column(4).Style.NumberFormat.Format.Should().Be("0.0000");
        var instrucciones = libro.Worksheet(PlantillaDeImportacion.HojaDeInstrucciones);
        instrucciones.Cell(1, 1).GetString().Should().Be("Productos de prueba");
        instrucciones.CellsUsed().Select(c => c.GetString()).Should().Contain("ARZ-001").And.Contain("costo o factor (6 decimales)");
    }

    [Fact]
    public async Task Una_hoja_que_no_esta_es_Archivo_HojaFaltante_y_un_csv_es_la_hoja_Datos()
    {
        var archivo = PlantillaDeImportacion.Xlsx(Plantilla, "p");
        (await _lector.LeerHojaAsync(archivo.Contenido, archivo.NombreArchivo, "Ubicaciones")).Error.Code.Should().Be("Archivo.HojaFaltante");

        var csv = Encoding.UTF8.GetBytes("codigo;nombre\nA1;Uno\n");
        (await _lector.ListarHojasAsync(csv, "p.csv")).Value.Should().Equal("Datos");
        (await _lector.LeerHojaAsync(csv, "p.csv", "Datos")).Value.Filas.Should().ContainSingle();
        (await _lector.LeerHojaAsync(csv, "p.csv", "Productos")).Error.Code.Should().Be("Archivo.HojaFaltante");
    }

    [Fact]
    public async Task Con_datos_cada_hoja_se_llena_y_el_porcentaje_vuelve_en_puntos()
    {
        var datos = new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>
        {
            ["Productos"] = [["ARZ-001", "Arroz", 1234.567891m, 0.19m]],
            ["CodigosDeBarras"] = [["ARZ-001", "7701234567890"]],
        });

        var archivo = PlantillaDeImportacion.Xlsx(Plantilla, "p", datos);

        var productos = await _lector.LeerHojaAsync(archivo.Contenido, archivo.NombreArchivo, "Productos");
        productos.Value.Filas.Should().ContainSingle().Which.Celdas.Should().Equal("ARZ-001", "Arroz", "1234.567891", "19");
        var barras = await _lector.LeerHojaAsync(archivo.Contenido, archivo.NombreArchivo, "CodigosDeBarras");
        barras.Value.Filas.Single()[1].Should().Be("7701234567890");
    }

    [Fact]
    public async Task La_revision_en_excel_devuelve_el_mismo_libro_con_resultado_y_errores()
    {
        var datos = new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>
        {
            ["Productos"] = [["A1", "Uno", null, null], ["A2", null, null, null]],
        });
        var subido = PlantillaDeImportacion.Xlsx(Plantilla, "p", datos);
        var resultado = new ImportResultDto
        {
            Template = "test.p",
            Rows =
            [
                new ResultadoDeFila("Productos", 2, AccionDeImportacion.Create, []),
                new ResultadoDeFila("Productos", 3, null, ["nombre: La columna «nombre» es obligatoria."]),
            ],
        };

        var revisado = PlantillaDeImportacion.ConResultados(new ArchivoDeImportacion("mis-productos.xlsx", subido.Contenido), Plantilla, resultado);

        revisado.NombreArchivo.Should().Be("mis-productos-revision.xlsx");
        var hoja = await _lector.LeerHojaAsync(revisado.Contenido, revisado.NombreArchivo, "Productos");
        hoja.Value.Encabezados.Should().EndWith(["resultado", "errores"]);
        hoja.Value.Filas[0].Celdas[^2].Should().Be("Crear");
        hoja.Value.Filas[1].Celdas[^2].Should().Be("Con errores");
        hoja.Value.Filas[1].Celdas[^1].Should().Contain("obligatoria");

        // Revisar otra vez el libro revisado reescribe las dos columnas en su sitio, no agrega otras.
        var otraVez = PlantillaDeImportacion.ConResultados(new ArchivoDeImportacion("r.xlsx", revisado.Contenido), Plantilla, resultado);
        (await _lector.LeerHojaAsync(otraVez.Contenido, "r.xlsx", "Productos")).Value.Encabezados.Should().HaveCount(6);
    }

    [Fact]
    public void Los_exportadores_respetan_cantidad_y_costo()
    {
        ExportadorDeTablas.Texto(12.5m, TipoDeColumna.Cantidad).Should().Be("12,5000");
        ExportadorDeTablas.Texto(1.5m, TipoDeColumna.Costo).Should().Be("1,500000");
        ExportadorDeTablas.FormatoNumerico(TipoDeColumna.Cantidad).Should().Be("#,##0.0000");
        ExportadorDeTablas.FormatoNumerico(TipoDeColumna.Costo).Should().Be("#,##0.000000");
    }
}
