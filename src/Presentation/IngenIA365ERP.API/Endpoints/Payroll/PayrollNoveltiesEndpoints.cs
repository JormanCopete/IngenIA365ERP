using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Novelties.CancelNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.CorrectNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.Queries;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterSalaryChange;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §3): novedades del período y cambios de salario. Importación y recurrentes van en su propio módulo (US6).</summary>
public sealed class PayrollNoveltiesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("PayrollNovelties")
            .RequireAuthorization();

        group.MapGet("/pay-periods/{periodId:guid}/novelties",
                async (Guid periodId, Guid? employeeId, string? conceptCode, string? status, string? origin, string? search, ISender sender, CancellationToken ct) =>
                    await sender.Send(new ListNoveltiesQuery(periodId, employeeId, conceptCode, status, origin, search), ct))
            .WithName("Payroll_Novelties_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.View");

        group.MapPost("/pay-periods/{periodId:guid}/novelties",
                async (Guid periodId, RegisterNoveltyCommand command, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(command with { PeriodPublicId = periodId }, ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/payroll/novelties/{result.Value}", new { publicId = result.Value })
                        : (object)result;
                })
            .WithName("Payroll_Novelties_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Create");

        group.MapPut("/novelties/{noveltyId:guid}",
                async (Guid noveltyId, CorrectNoveltyCommand command, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(command with { NoveltyPublicId = noveltyId }, ct);
                    return result.IsSuccess ? Results.Ok(new { publicId = result.Value }) : (object)result;
                })
            .WithName("Payroll_Novelties_Correct")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Update");

        group.MapPost("/novelties/{noveltyId:guid}/cancel",
                async (Guid noveltyId, CancelBody body, ISender sender, CancellationToken ct) =>
                    await sender.Send(new CancelNoveltyCommand(noveltyId, body.Reason), ct))
            .WithName("Payroll_Novelties_Cancel")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Cancel");

        group.MapGet("/novelties/{noveltyId:guid}/history",
                async (Guid noveltyId, ISender sender, CancellationToken ct) =>
                    await sender.Send(new GetNoveltyHistoryQuery(noveltyId), ct))
            .WithName("Payroll_Novelties_History")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.View");

        group.MapPost("/employees/{employeeId:guid}/salary-changes",
                async (Guid employeeId, SalaryChangeBody body, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new RegisterSalaryChangeCommand(employeeId, body.NewSalary, body.EffectiveFrom, body.Reason), ct);
                    return result.IsSuccess ? Results.Created($"/api/payroll/employees/{employeeId}/salary-changes", new { id = result.Value }) : (object)result;
                })
            .WithName("Payroll_SalaryChanges_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Create");

        group.MapGet("/employees/{employeeId:guid}/salary-changes",
                async (Guid employeeId, ISender sender, CancellationToken ct) =>
                    await sender.Send(new ListSalaryChangesQuery(employeeId), ct))
            .WithName("Payroll_SalaryChanges_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.View");
    }

    public sealed record CancelBody(string Reason);
    public sealed record SalaryChangeBody(decimal NewSalary, DateTime EffectiveFrom, string Reason);
}
