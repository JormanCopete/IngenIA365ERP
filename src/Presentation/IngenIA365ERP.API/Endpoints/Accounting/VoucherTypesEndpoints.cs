using Carter;
using IngenIA365ERP.Application.Accounting.VoucherTypes.Commands.CreateVoucherType;
using IngenIA365ERP.Application.Accounting.VoucherTypes.Commands.UpdateVoucherType;
using IngenIA365ERP.Application.Accounting.VoucherTypes.Commands.DeleteVoucherType;
using IngenIA365ERP.Application.Accounting.VoucherTypes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class VoucherTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/voucher-types")
            .WithTags("VoucherTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListVoucherTypesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListVoucherTypes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetVoucherTypeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetVoucherTypeById");

        group.MapPost("/", async (CreateVoucherTypeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/voucher-types/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateVoucherType");

        group.MapPut("/{id:guid}", async (Guid id, UpdateVoucherTypeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateVoucherType");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteVoucherTypeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteVoucherType");
    }
}
