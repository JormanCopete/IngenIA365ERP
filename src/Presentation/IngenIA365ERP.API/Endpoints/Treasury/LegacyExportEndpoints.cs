using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Imports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Feature 015, fase 46 (T0472; data-model §24 paso 2; FR-161): la exportación para archivo de la Tesorería anterior antes de su retiro. <c>GET …/tables</c> muestra las
/// tres tablas; <c>POST</c> arma un xlsx y un csv por tabla, los guarda como adjuntos no borrables con su huella y devuelve la lista. Se retira con
/// <c>RetiroDeLaTesoreriaHeredada</c>, en el mismo commit: después no queda nada que exportar. (nuevo)
/// </summary>
public class TreasuryLegacyExportEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/legacy-export", "Tesorería Retiro de lo anterior");
        g.MapGet("/tables", async (ISender s, CancellationToken ct) => await s.Send(new GetLegacyTreasuryTablesQuery(), ct))
            .WithName("Treasury_LegacyExport_Tables").RequirePermission("Treasury.Imports.Run");
        g.MapPost("/", async (HttpContext http, ISender s, CancellationToken ct) =>
            {
                var tablas = await s.Send(new GetLegacyTreasuryTablesQuery(), ct);
                if (tablas.IsFailure) return Result.Failure<LegacyTreasuryExportDto>(tablas.Error);
                var sello = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                var archivos = new List<ArchivoDeExportacionHeredada>();
                var filas = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (tabla, indice) in tablas.Value.Select((t, i) => (t, i)))
                {
                    var nombre = $"tesoreria-anterior-{indice + 1}-{Nombre(tabla.Titulo)}-{sello}";
                    var xlsx = ExportadorDeTablas.Exportar(tabla, "xlsx", nombre);
                    archivos.Add(new ArchivoDeExportacionHeredada(xlsx.NombreArchivo, xlsx.TipoContenido, xlsx.Contenido));
                    archivos.Add(new ArchivoDeExportacionHeredada(nombre + ".csv", "text/csv", CsvDeTablaHeredada.Escribir(tabla)));
                    filas[tabla.Titulo] = tabla.Filas.Count;
                }
                return await s.Send(new ArchiveLegacyTreasuryExportCommand(archivos, filas) { OperationKey = http.ClaveDeOperacion() }, ct);
            })
            .WithName("Treasury_LegacyExport_Archive").ConClaveDeOperacion().RequirePermission("Treasury.Imports.Run");
    }

    private static string Nombre(string titulo) =>
        titulo.StartsWith("Cheques", StringComparison.Ordinal) ? "cheques" : titulo.StartsWith("Conceptos", StringComparison.Ordinal) ? "conceptos" : "facturas";
}
