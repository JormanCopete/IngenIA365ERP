using Carter;
using IngenIA365ERP.Application.Treasury.Invoices.Commands.RegisterTreasuryInvoice;
using IngenIA365ERP.Application.Treasury.Invoices.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

public class TreasuryTransactionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var invoiceGroup = app.MapGroup("/api/treasury/invoices")
            .WithTags("TreasuryInvoices")
            .RequireAuthorization();

        invoiceGroup.MapGet("/", async ([AsParameters] ListTreasuryInvoicesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListTreasuryInvoices");

        invoiceGroup.MapPost("/", async (RegisterTreasuryInvoiceCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/treasury/invoices/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RegisterTreasuryInvoice");

        // Cash flow
        var cashFlowGroup = app.MapGroup("/api/treasury/cash-flow")
            .WithTags("CashFlow")
            .RequireAuthorization();

        cashFlowGroup.MapGet("/", async (int year, int? monthFrom, int? monthTo, ISender sender) =>
        {
            var query = new GetCashFlowQuery
            {
                Year = year,
                MonthFrom = monthFrom,
                MonthTo = monthTo
            };
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetCashFlow");
    }
}
