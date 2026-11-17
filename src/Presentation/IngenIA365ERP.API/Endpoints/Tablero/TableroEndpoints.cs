using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Tablero;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Tablero;

/// <summary>
/// 2026-11-17 (pedido del dueño): el tablero de inicio por usuario y por rol. Las lecturas del usuario no piden permiso propio: cada
/// indicador y pendiente se filtra por el permiso con que se consulta su pantalla (<see cref="CatalogoDelTablero"/>), y uno que no
/// puede ver responde 404. El tablero de cada rol lo ve quien ve los roles y lo cambia quien los administra.
/// </summary>
public class TableroEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        grupo.MapGet("/mine", async (ISender sender, CancellationToken ct) => await sender.Send(new GetMyDashboardQuery(), ct))
            .WithName("Dashboard_Mine")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        grupo.MapGet("/indicators/{codigo}", async (string codigo, bool? soloLoMio, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDashboardIndicatorQuery(codigo, soloLoMio ?? false), ct))
            .WithName("Dashboard_Indicator")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        grupo.MapGet("/pending", async (ISender sender, CancellationToken ct) => await sender.Send(new GetDashboardPendingQuery(), ct))
            .WithName("Dashboard_Pending")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        grupo.MapGet("/roles", async (ISender sender, CancellationToken ct) => await sender.Send(new GetRoleDashboardsQuery(), ct))
            .WithName("Dashboard_Roles")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Security.Roles.View");

        grupo.MapPut("/roles/{rol}", async (string rol, TableroDeRolRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetRoleDashboardCommand(rol, body.Tarjetas ?? []), ct))
            .WithName("Dashboard_Role_Set")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Security.Roles.Create");
    }

    /// <summary>El cuerpo de <c>PUT /api/dashboard/roles/{rol}</c>: las tarjetas en orden (vacío = el sugerido).</summary>
    public sealed record TableroDeRolRequest(IReadOnlyList<TarjetaDelTablero>? Tarjetas);
}
