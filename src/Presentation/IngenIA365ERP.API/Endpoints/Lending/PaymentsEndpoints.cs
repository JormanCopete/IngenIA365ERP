using Carter;
using IngenIA365ERP.Application.Lending.Payments.Commands.ProcessPayment;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class PaymentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/payments")
            .WithTags("LendingPayments")
            .RequireAuthorization();

        group.MapPost("/", async (ProcessPaymentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("ProcessPayment");

        group.MapGet("/{id:guid}/receipt", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPaymentReceiptQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPaymentReceipt");
    }
}
