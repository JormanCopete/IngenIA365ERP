using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Correccion.Consultas;
using IngenIA365ERP.Application.Identity.Correccion.CorregirDocumento;
using IngenIA365ERP.Application.Identity.Correccion.FusionarIdentidades;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Feature 017 (US6, US7): soporte de la plataforma sobre las identidades —buscar, ver el detalle con su
/// historial y sus casos, corregir el documento sin límite de cooperativas, fusionar dos identidades,
/// descartar casos y cambiar el correo de rescate—. Sólo el maestro: lo decide cada manejador
/// (<c>Saas.MasterOnly</c>), como el resto de <c>/api/saas</c>.
/// </summary>
public sealed class IdentidadesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var identidades = app.MapGroup("/api/saas/identities")
            .WithTags("SaaS / Identidades")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        identidades.MapGet("/", BuscarAsync).WithName("Identidades_Buscar");
        identidades.MapGet("/{id:guid}", DetalleAsync).WithName("Identidades_Detalle");
        identidades.MapPost("/{id:guid}/correct-document", CorregirAsync).WithName("Identidades_CorregirDocumento");
        identidades.MapPost("/{id:guid}/change-email", CambiarCorreoAsync).WithName("Identidades_CambiarCorreo");
        identidades.MapPost("/merge", FusionarAsync).WithName("Identidades_Fusionar");

        var casos = app.MapGroup("/api/saas/identity-cases")
            .WithTags("SaaS / Identidades")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        casos.MapGet("/", CasosAsync).WithName("Identidades_Casos");
        casos.MapPost("/{id:guid}/dismiss", DescartarAsync).WithName("Identidades_DescartarCaso");
    }

    private static async Task<object?> BuscarAsync(string? q, ISender sender, CancellationToken ct) =>
        await sender.Send(new BuscarIdentidadesQuery(q), ct);

    private static async Task<object?> DetalleAsync(Guid id, ISender sender, CancellationToken ct) =>
        await sender.Send(new GetIdentidadQuery(id), ct);

    private static async Task<object?> CorregirAsync(
        Guid id, [FromBody] CorregirDocumentoBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new CorregirDocumentoPorMaestroCommand(id, body.Comando(Guid.Empty)), ct);

    private static async Task<object?> CambiarCorreoAsync(
        Guid id, [FromBody] CambiarCorreoBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new IngenIA365ERP.Application.Identity.Correccion.CambiarCorreoPorMaestro.CambiarCorreoPorMaestroCommand(
            id, body.NewEmail, body.Reason), ct);

    private static async Task<object?> FusionarAsync(
        [FromBody] FusionarBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new FusionarIdentidadesCommand(body.SourceId, body.TargetId, body.Reason), ct);

    private static async Task<object?> CasosAsync(string? status, ISender sender, CancellationToken ct) =>
        await sender.Send(new ListarCasosDeIdentidadQuery(status), ct);

    private static async Task<object?> DescartarAsync(
        Guid id, [FromBody] DescartarBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new DescartarCasoDeIdentidadCommand(id, body.Reason), ct);

    public sealed record FusionarBody(Guid SourceId, Guid TargetId, string? Reason);
    public sealed record DescartarBody(string? Reason);
    public sealed record CambiarCorreoBody(string? NewEmail, string? Reason);
}
