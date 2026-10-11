using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las garantías (feature 013, US3, T0457; contracts/api.md §6.1, FR-043, FR-082): lista y detalle (<c>Lending.Guarantees.Read</c>), registrar,
/// editar mientras el dueño no esté decidido y cambiar de estado (<c>.Manage</c>) y liberar con la cancelación en firme (<c>.Release</c>). Sus
/// documentos (avalúo, póliza, registro) se suben como adjuntos <c>LendingGuaranteeDocument</c> de la garantía por la ruta de adjuntos. Toda
/// escritura lleva <c>Idempotency-Key</c>. Fuera del alcance, el 404 de la garantía. (nuevo)
/// </summary>
public class CollateralsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Guarantees.Read";
    public const string Administrar = "Lending.Guarantees.Manage";
    public const string Liberar = "Lending.Guarantees.Release";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var garantias = LendingGroup.Grupo(app, "/collaterals", "Cartera Garantías");

        garantias.MapGet("/", async (Guid? loanPublicId, Guid? applicationPublicId, Guid? personPublicId, CollateralKind? kind, CollateralStatus? status,
                int? expiringWithinDays, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCollateralsQuery(loanPublicId, applicationPublicId, personPublicId, kind, status, expiringWithinDays, page ?? 1,
                    pageSize ?? 20), ct))
            .WithName("Lending_Collaterals_List")
            .RequirePermission(Leer);

        garantias.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetCollateralQuery(id), ct))
            .WithName("Lending_Collaterals_Get")
            .RequirePermission(Leer);

        garantias.MapPost("/", async (CollateralRegistrationInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterCollateralCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct),
                    g => $"/api/lending/collaterals/{g.PublicId}"))
            .WithName("Lending_Collaterals_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        garantias.MapPut("/{id:guid}", async (Guid id, CollateralRegistrationInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateCollateralCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Collaterals_Update")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        garantias.MapPost("/{id:guid}/status", async (Guid id, EstadoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ChangeCollateralStatusCommand(id, body.Status, body.Reason ?? string.Empty, body.SupportAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Collaterals_ChangeStatus")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        garantias.MapPost("/{id:guid}/release", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReleaseCollateralCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Collaterals_Release")
            .ConClaveDeOperacion()
            .RequirePermission(Liberar);
    }

    /// <summary>El estado nuevo (§6.1): <c>Constituted</c>, <c>Valid</c>, <c>Extinguished</c> o <c>Executed</c>, con motivo y soporte.</summary>
    public sealed record EstadoRequest(CollateralStatus Status, string? Reason, Guid? SupportAttachmentPublicId);

    /// <summary>El motivo de la liberación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
