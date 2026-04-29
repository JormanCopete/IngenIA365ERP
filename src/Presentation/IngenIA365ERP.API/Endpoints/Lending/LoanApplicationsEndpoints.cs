using Carter;
using IngenIA365ERP.Application.Lending.LoanApplications.Commands.ApproveLoanApplication;
using IngenIA365ERP.Application.Lending.LoanApplications.Commands.CreateLoanApplication;
using IngenIA365ERP.Application.Lending.LoanApplications.Commands.DisburseLoan;
using IngenIA365ERP.Application.Lending.LoanApplications.Commands.RejectLoanApplication;
using IngenIA365ERP.Application.Lending.LoanApplications.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class LoanApplicationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/loan-applications")
            .WithTags("LoanApplications")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListLoanApplicationsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListLoanApplications");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetLoanApplicationByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetLoanApplicationById");

        group.MapPost("/", async (CreateLoanApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/loan-applications/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateLoanApplication");

        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveRequest request, ISender sender) =>
        {
            var command = new ApproveLoanApplicationCommand
            {
                PublicId = id,
                ApprovedAmount = request.ApprovedAmount,
                ApprovedTerm = request.ApprovedTerm,
                ApprovedRate = request.ApprovedRate,
                ApproverNotes = request.ApproverNotes,
                MinutesNumber = request.MinutesNumber
            };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("ApproveLoanApplication");

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectRequest request, ISender sender) =>
        {
            var command = new RejectLoanApplicationCommand(id, request.RejectionReason);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("RejectLoanApplication");

        group.MapPost("/{id:guid}/disburse", async (Guid id, DisburseRequest request, ISender sender) =>
        {
            var command = new DisburseLoanCommand
            {
                ApplicationPublicId = id,
                DisbursementDate = request.DisbursementDate,
                BankAccountCode = request.BankAccountCode,
                VoucherTypeCode = request.VoucherTypeCode
            };
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("DisburseLoan");

        group.MapGet("/{id:guid}/amortization", async (Guid id, ISender sender) =>
        {
            // First get the application to extract amount/rate/term
            var appResult = await sender.Send(new GetLoanApplicationByIdQuery(id));
            if (appResult.IsFailure) return Results.NotFound(appResult.Error);

            var app = appResult.Value;
            var amount = app.ApprovedAmount > 0 ? app.ApprovedAmount : app.RequestedAmount;
            var rate = app.InterestRate;
            var term = app.Term;

            var result = await sender.Send(new GetAmortizationPreviewQuery(amount, rate, term));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetLoanApplicationAmortization");
    }
}

// Request DTOs for endpoints
public record ApproveRequest(
    decimal ApprovedAmount,
    int ApprovedTerm,
    decimal ApprovedRate,
    string? ApproverNotes,
    string? MinutesNumber);

public record RejectRequest(string RejectionReason);

public record DisburseRequest(
    DateOnly DisbursementDate,
    string? BankAccountCode,
    string? VoucherTypeCode);
