using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Promociones;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Contenido promocional de la pantalla de inicio de sesión.
///
/// <para>
/// La lectura es ANÓNIMA porque se muestra antes de entrar. Devuelve sólo lo
/// publicado y vigente, con tope de piezas, y nunca expone fechas de vigencia
/// ni estado: quien no entró no tiene por qué saber qué campañas hay
/// preparadas.
/// </para>
///
/// <para>La escritura exige ser administrador global de IngenIA365.</para>
/// </summary>
public sealed class PromocionesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var publico = app.MapGroup("/api/publico/promociones")
            .WithTags("Público / Promociones")
            .AllowAnonymous()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        publico.MapGet("/", ListarPublicasAsync).WithName("Promociones_ListarPublicas");

        // La imagen va fuera del filtro de sobre de error: devuelve bytes, no
        // JSON, y envolverla rompería la respuesta binaria.
        app.MapGet("/api/publico/promociones/{publicId:guid}/imagen", ObtenerImagenAsync)
            .WithTags("Público / Promociones")
            .AllowAnonymous()
            .WithName("Promociones_ObtenerImagen");

        var admin = app.MapGroup("/api/admin/promociones")
            .WithTags("Admin / Promociones")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        admin.MapGet("/", ListarAsync).WithName("Promociones_Listar");
        admin.MapPost("/", GuardarAsync).WithName("Promociones_Guardar");
        admin.MapDelete("/{publicId:guid}", EliminarAsync).WithName("Promociones_Eliminar");
    }

    private static async Task<object?> ListarPublicasAsync(ISender sender, CancellationToken ct) =>
        await sender.Send(new ListarPromocionesPublicasQuery(), ct);

    private static async Task<IResult> ObtenerImagenAsync(
        Guid publicId, ISender sender, CancellationToken ct)
    {
        var r = await sender.Send(new ObtenerImagenPromoQuery(publicId), ct);
        if (!r.IsSuccess || r.Value is null)
            return Results.NotFound();

        // Cache larga: la imagen es inmutable para un PublicId dado, y este
        // endpoint lo golpea cada visitante que abre el login.
        return Results.File(r.Value.Contenido, r.Value.TipoMime, enableRangeProcessing: false);
    }

    private static async Task<object?> ListarAsync(ISender sender, CancellationToken ct) =>
        await sender.Send(new ListarPromocionesQuery(), ct);

    private static async Task<object?> GuardarAsync(
        [FromBody] GuardarPromoBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new GuardarPromoCommand(
            body.PublicId, body.Titulo, body.Texto, body.TextoEnlace, body.Enlace,
            body.ImagenBase64, body.ImagenTipoMime, body.ImagenTextoAlternativo,
            body.Orden, body.Publicado, body.VigenteDesde, body.VigenteHasta), ct);

    private static async Task<object?> EliminarAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new EliminarPromoCommand(publicId), ct);

    public sealed record GuardarPromoBody(
        Guid? PublicId,
        string Titulo,
        string? Texto,
        string? TextoEnlace,
        string? Enlace,
        string? ImagenBase64,
        string? ImagenTipoMime,
        string? ImagenTextoAlternativo,
        int Orden,
        bool Publicado,
        DateTime? VigenteDesde,
        DateTime? VigenteHasta);
}
