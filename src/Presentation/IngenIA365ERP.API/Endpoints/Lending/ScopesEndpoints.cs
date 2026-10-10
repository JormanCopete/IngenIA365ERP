using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El alcance de Cartera de un usuario (feature 013, T0313, T62; contracts/api.md §3.8): las oficinas de sólo lectura (las asigna Seguridad en
/// <c>SEC_UserBranchAssignments</c>) y las líneas de crédito que opera, que se reemplazan en bloque. Las dos rutas exigen
/// <c>Lending.Scopes.Manage</c>; el <c>PUT</c> lleva <c>Idempotency-Key</c>. Una línea fuera del alcance de quien asigna es el 404 de la
/// línea. Es la pestaña «Alcance de Cartera» de <c>/admin/usuarios</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ScopesEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Scopes.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var alcance = LendingGroup.Grupo(app, "/scopes", "Cartera Alcance");

        alcance.MapGet("/users/{userPublicId:guid}", async (Guid userPublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingUserScopeQuery(userPublicId), ct))
            .WithName("Lending_Scopes_GetUser")
            .RequirePermission(Permiso);

        alcance.MapPut("/users/{userPublicId:guid}", async (Guid userPublicId, FijarLineasRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new SetUserCreditLineScopesCommand(userPublicId, body.Lines ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Scopes_SetUser")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);
    }

    /// <summary>El cuerpo del reemplazo (§3.8): las líneas con a lo sumo una por defecto.</summary>
    public sealed record FijarLineasRequest(IReadOnlyList<LineaAsignadaInput>? Lines);
}
