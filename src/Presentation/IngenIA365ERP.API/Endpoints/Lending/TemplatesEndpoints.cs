using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Lending.Imports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El registro de plantillas de Cartera (feature 013, T0313, T82; contracts/api.md §3.9, plantillas.md §0.1): la lista con lo que puede hacer
/// quien pregunta y la descarga del libro vacío de una plantilla ya construida (encabezados en la fila 1, instrucciones en otra hoja), las dos
/// con <c>Lending.Templates.View</c>. La importación de cada plantilla vive en la ruta de su recurso (<c>route</c>), con su permiso. Cada ruta
/// sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class TemplatesEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Templates.View";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var plantillas = LendingGroup.Grupo(app, "/templates", "Cartera Plantillas");

        plantillas.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListLendingTemplatesQuery(), ct))
            .WithName("Lending_Templates_List")
            .RequirePermission(Permiso);

        plantillas.MapGet("/{templateKey}/template.xlsx", async (string templateKey, ISender sender, CancellationToken ct) =>
            {
                var definicion = await sender.Send(new GetLendingTemplateQuery(templateKey), ct);
                if (definicion.IsFailure) return (object)definicion;
                var libro = PlantillaDeImportacion.Xlsx(definicion.Value, $"plantilla-{definicion.Value.Clave.Replace('.', '-')}");
                return Results.File(libro.Contenido, libro.TipoContenido, libro.NombreArchivo);
            })
            .WithName("Lending_Templates_Download")
            .RequirePermission(Permiso);
    }
}
