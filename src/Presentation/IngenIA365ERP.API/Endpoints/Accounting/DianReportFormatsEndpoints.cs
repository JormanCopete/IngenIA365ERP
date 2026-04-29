using Carter;
using IngenIA365ERP.Application.Accounting.DianReportFormats.Commands.CreateDianReportFormat;
using IngenIA365ERP.Application.Accounting.DianReportFormats.Commands.UpdateDianReportFormat;
using IngenIA365ERP.Application.Accounting.DianReportFormats.Commands.DeleteDianReportFormat;
using IngenIA365ERP.Application.Accounting.DianReportFormats.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class DianReportFormatsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/dian-report-formats")
            .WithTags("DianReportFormats")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDianReportFormatsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDianReportFormats");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDianReportFormatByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDianReportFormatById");

        group.MapPost("/", async (CreateDianReportFormatCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/dian-report-formats/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDianReportFormat");

        group.MapPut("/{id:guid}", async (Guid id, UpdateDianReportFormatCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateDianReportFormat");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDianReportFormatCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteDianReportFormat");
    }
}
