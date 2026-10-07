using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Activation;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La activación de Cartera (feature 013, T0234, C107; contracts/api.md §19.3): el estado con sus requisitos y la activación, con
/// <c>Lending.Activation.Execute</c>. La activación exige <c>Idempotency-Key</c> y el punto del acta (<c>sessionItemPublicId</c>, C61).
/// US17 pasa el grupo a <c>LendingGroup</c>; US8 suma la plantilla y la revisión de cobros provisionales y los roles sugeridos (§19.3,
/// §19.4). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ActivationEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Activation.Execute";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var activacion = app.MapGroup("/api/lending/activation")
            .WithTags("Cartera Activación")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

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
    }

    /// <summary>El cuerpo de la activación (§19.3).</summary>
    public sealed record ActivarRequest(
        Guid MigrationLoadPublicId,
        DateOnly ActivationDate,
        Guid? ProvisionalCollectionsAttachmentPublicId,
        Guid? SessionItemPublicId,
        bool AcknowledgeWarnings,
        string? Reason);
}
