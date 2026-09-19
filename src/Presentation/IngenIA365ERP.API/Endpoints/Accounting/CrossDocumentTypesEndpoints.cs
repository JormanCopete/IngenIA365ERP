using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.VoucherTypes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>Tipos de documento cruce (feature 009, contracts/api.md §4); comparten permiso con los tipos de comprobante.</summary>
public class CrossDocumentTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/cross-document-types")
            .WithTags("AccountingCrossDocumentTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([FromQuery] bool includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCrossDocumentTypesQuery(includeInactive), ct))
            .WithName("Accounting_CrossDocumentTypes_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.View");

        group.MapPost("/", async (CreateCrossDocumentTypeCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/accounting/cross-document-types/{result.Value}", new { PublicId = result.Value }) : result;
            })
            .WithName("Accounting_CrossDocumentTypes_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.Manage");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCrossDocumentTypeCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command with { PublicId = id }, ct))
            .WithName("Accounting_CrossDocumentTypes_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.Manage");
    }
}
