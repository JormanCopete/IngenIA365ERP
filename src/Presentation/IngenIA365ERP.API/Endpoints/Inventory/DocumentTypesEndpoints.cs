using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.DocumentTypes;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Tipos de documento y su numeración (feature 012, T151; contracts/api.md §8): las clases fijas, la lista y el detalle
/// con <c>Inventory.DocumentTypes.View</c>; alta, edición, cambio de consecutivo, inactivar y reactivar con
/// <c>Inventory.DocumentTypes.Manage</c> e <c>Idempotency-Key</c>. La plantilla 8 (<c>template.xlsx</c>, <c>import</c>)
/// llega con la infraestructura común de importación (T153). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DocumentTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/document-types")
            .WithTags("Inventory Document Types")
            .RequireAuthorization();

        group.MapGet("/classes", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDocumentClassesQuery(), ct))
            .WithName("Inventory_DocumentTypes_Classes")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.DocumentTypes.View");

        group.MapGet("/", async ([Microsoft.AspNetCore.Mvc.FromQuery(Name = "group")] DocumentClassGroup? grupo, DocumentClass? @class, bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentTypesQuery(grupo, @class, includeInactive ?? false), ct))
            .WithName("Inventory_DocumentTypes_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.DocumentTypes.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetInventoryDocumentTypeQuery(id), ct))
            .WithName("Inventory_DocumentTypes_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.DocumentTypes.View");

        group.MapPost("/", async (CrearTipoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateInventoryDocumentTypeCommand(
                    body.Code ?? string.Empty, body.Name ?? string.Empty, body.Class,
                    body.RequiresCounterparty, body.RequiresCostCenter, body.RequiresReason, body.RequiresExternalReference,
                    body.WarehousePublicIds, body.SalesChannelPublicId,
                    body.IsTaxableWithdrawal ?? false, body.VatNonDeductible ?? false, body.AllowsFutureDate ?? false,
                    body.Prefix, body.FirstNumber, body.ValidFrom)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/inventory/document-types/{result.Value.PublicId}", result.Value) : result;
            })
            .WithName("Inventory_DocumentTypes_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.DocumentTypes.Manage");

        group.MapPut("/{id:guid}", async (Guid id, EditarTipoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateInventoryDocumentTypeCommand(
                    id, body.Name ?? string.Empty,
                    body.RequiresCounterparty, body.RequiresCostCenter, body.RequiresReason, body.RequiresExternalReference,
                    body.WarehousePublicIds, body.SalesChannelPublicId,
                    body.IsTaxableWithdrawal ?? false, body.VatNonDeductible ?? false, body.AllowsFutureDate ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_DocumentTypes_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.DocumentTypes.Manage");

        group.MapPost("/{id:guid}/sequences", async (Guid id, SecuenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new AddDocumentSequenceCommand(id, body.Prefix, body.NextValue, body.ValidFrom, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/inventory/document-types/{id}", result.Value) : result;
            })
            .WithName("Inventory_DocumentTypes_AddSequence")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.DocumentTypes.Manage");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateInventoryDocumentTypeCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_DocumentTypes_Deactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.DocumentTypes.Manage");

        group.MapPost("/{id:guid}/reactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReactivateInventoryDocumentTypeCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_DocumentTypes_Reactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.DocumentTypes.Manage");
    }

    /// <summary>El alta (§8).</summary>
    public sealed record CrearTipoRequest(
        string? Code,
        string? Name,
        DocumentClass Class,
        bool RequiresCounterparty,
        bool RequiresCostCenter,
        bool RequiresReason,
        bool RequiresExternalReference,
        IReadOnlyList<Guid>? WarehousePublicIds,
        Guid? SalesChannelPublicId,
        bool? IsTaxableWithdrawal,
        bool? VatNonDeductible,
        bool? AllowsFutureDate,
        string? Prefix,
        long? FirstNumber,
        DateOnly? ValidFrom);

    /// <summary>La edición (§8): sin código, clase, prefijo, primer número ni vigencia.</summary>
    public sealed record EditarTipoRequest(
        string? Name,
        bool RequiresCounterparty,
        bool RequiresCostCenter,
        bool RequiresReason,
        bool RequiresExternalReference,
        IReadOnlyList<Guid>? WarehousePublicIds,
        Guid? SalesChannelPublicId,
        bool? IsTaxableWithdrawal,
        bool? VatNonDeductible,
        bool? AllowsFutureDate);

    /// <summary>Un consecutivo nuevo o un cambio de número (§8).</summary>
    public sealed record SecuenciaRequest(string? Prefix, long NextValue, DateOnly ValidFrom, string? Reason);

    /// <summary>Inactivar o reactivar (§8).</summary>
    public sealed record MotivoRequest(string? Reason);
}
