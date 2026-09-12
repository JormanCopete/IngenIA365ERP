using Carter;
using IngenIA365ERP.Application.Common.Catalogos;
using MediatR;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Un solo endpoint para preguntar «¿ya existe este código?» en cualquier catálogo.
/// Lo llama el campo «Código» de las pantallas al perder el foco, antes de que la
/// persona escriba el resto del formulario.
/// </summary>
public class CatalogosEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalogos/{catalogo}/codigo/{codigo}", async (string catalogo, string codigo, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new BuscarCodigoDeCatalogoQuery(catalogo, codigo), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithTags("Catalogos")
        .WithName("BuscarCodigoDeCatalogo")
        .RequireAuthorization();
    }
}
