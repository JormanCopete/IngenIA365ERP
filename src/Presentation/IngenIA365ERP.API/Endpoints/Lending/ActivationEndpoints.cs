using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La activación de Cartera (feature 013, T0234, C107; contracts/api.md §19.3): el estado con sus requisitos y la activación, con
/// <c>Lending.Activation.Execute</c>. La activación exige <c>Idempotency-Key</c> y el punto del acta (<c>sessionItemPublicId</c>, C61).
/// Desde US17 (T0312) va sobre <see cref="LendingGroup"/>; US8 suma la plantilla y la revisión de cobros provisionales (§19.3). Los roles sugeridos
/// (§19.4, T0300) van con <c>Security.Roles.Create</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ActivationEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Activation.Execute";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // US17 (T0312): sobre el grupo base de Cartera, que suma la auditoría del intento denegado y del rechazo de forma.
        var activacion = LendingGroup.Grupo(app, "/activation", "Cartera Activación");

        activacion.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new GetLendingActivationStatusQuery(), ct))
            .WithName("Lending_Activation_Status")
            .RequirePermission(Permiso);

        activacion.MapPost("/", async (ActivarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ActivateLendingCommand(
                    body.MigrationLoadPublicId, body.ActivationDate, body.ProvisionalCollectionsAttachmentPublicId,
                    body.SessionItemPublicId ?? Guid.Empty, body.AcknowledgeWarnings, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created("/api/lending/activation", r.Value) : r;
            })
            .WithName("Lending_Activation_Execute")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        // Feature 013, T0300 (T61; §19.4): los roles de Cartera desde sus plantillas, idempotente. El permiso es el de crear roles.
        activacion.MapPost("/suggested-roles", async (RolesSugeridosRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CreateSuggestedLendingRolesCommand(body?.TemplateKeys) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Activation_SuggestedRoles")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRoles);
    }

    /// <summary>El permiso de <c>POST /suggested-roles</c> (§19.4).</summary>
    public const string PermisoDeRoles = "Security.Roles.Create";

    /// <summary>El cuerpo de los roles sugeridos (§19.4): sin claves, las trece plantillas <c>cartera.*</c>.</summary>
    public sealed record RolesSugeridosRequest(IReadOnlyList<string>? TemplateKeys);

    /// <summary>El cuerpo de la activación (§19.3).</summary>
    public sealed record ActivarRequest(
        Guid MigrationLoadPublicId,
        DateOnly ActivationDate,
        Guid? ProvisionalCollectionsAttachmentPublicId,
        Guid? SessionItemPublicId,
        bool AcknowledgeWarnings,
        string? Reason);
}
