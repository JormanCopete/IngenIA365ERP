using Carter;
using IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.CreateHealthInsuranceProvider;
using IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.UpdateHealthInsuranceProvider;
using IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.DeleteHealthInsuranceProvider;
using IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class HealthInsuranceProvidersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/health-insurance")
            .WithTags("HealthInsuranceProviders")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListHealthInsuranceProvidersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListHealthInsuranceProviders");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetHealthInsuranceProviderByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetHealthInsuranceProviderById");

        group.MapPost("/", async (CreateHealthInsuranceProviderCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/health-insurance/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateHealthInsuranceProvider");

        group.MapPut("/{id:guid}", async (Guid id, UpdateHealthInsuranceProviderCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateHealthInsuranceProvider");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteHealthInsuranceProviderCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteHealthInsuranceProvider");
    }
}
