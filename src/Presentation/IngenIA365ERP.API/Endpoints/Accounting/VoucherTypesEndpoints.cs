using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.VoucherTypes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>Tipos de comprobante (feature 009, contracts/api.md §4).</summary>
public class VoucherTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/voucher-types")
            .WithTags("AccountingVoucherTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([FromQuery] bool includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListVoucherTypesQuery(includeInactive), ct))
            .WithName("Accounting_VoucherTypes_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.View");

        group.MapPost("/", async (CreateVoucherTypeCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/accounting/voucher-types/{result.Value}", new { PublicId = result.Value }) : result;
            })
            .WithName("Accounting_VoucherTypes_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.Manage");

        group.MapPut("/{id:guid}", async (Guid id, UpdateVoucherTypeCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command with { PublicId = id }, ct))
            .WithName("Accounting_VoucherTypes_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.Manage");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new SetVoucherTypeActiveCommand(id, false), ct))
            .WithName("Accounting_VoucherTypes_Deactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.Manage");

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new SetVoucherTypeActiveCommand(id, true), ct))
            .WithName("Accounting_VoucherTypes_Activate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.VoucherTypes.Manage");
    }
}
