using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Notifications;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// El modo de prueba del correo (2026-11-17). <c>GET /api/core/email-mode</c> lo lee cualquier usuario autenticado de la cooperativa: es la
/// franja de aviso del encabezado y no expone más que el modo y el buzón. La lista de correos retenidos con <c>Core.HeldEmails.View</c>;
/// reenviar y descartar con <c>Core.HeldEmails.Manage</c> e <c>Idempotency-Key</c>. El modo se cambia como cualquier parámetro
/// (<c>/api/inventory/parameters</c>, módulo <c>CORREO</c>). (nuevo)
/// </summary>
public class HeldEmailsEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.HeldEmails.View";
    public const string PermisoDeAdministracion = "Core.HeldEmails.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/core/email-mode", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new GetModoDeCorreoQuery(), ct))
            .WithTags("Core Correo")
            .WithName("Core_EmailMode_Get")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        var retenidos = app.MapGroup("/api/core/held-emails")
            .WithTags("Core Correo")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        retenidos.MapGet("/", async (HeldEmailStatus? status, int? take, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCorreosRetenidosQuery(status, take ?? 500), ct))
            .WithName("Core_HeldEmails_List")
            .RequirePermission(PermisoDeConsulta);

        retenidos.MapPost("/resend", async (SeleccionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ResendCorreosRetenidosCommand(body.PublicIds ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_HeldEmails_Resend")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);

        retenidos.MapPost("/discard", async (SeleccionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardCorreosRetenidosCommand(body.PublicIds ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_HeldEmails_Discard")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);
    }

    /// <summary>Los correos elegidos; vacío = todos los pendientes.</summary>
    public sealed record SeleccionRequest(IReadOnlyList<Guid>? PublicIds);
}
