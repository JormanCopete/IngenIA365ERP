using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Novelties.CancelNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.CorrectNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.ImportNovelties;
using IngenIA365ERP.Application.Payroll.Novelties.Queries;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterSalaryChange;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Payroll.Services;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §3): novedades del período y cambios de salario. Incluye importación desde archivo y novedades recurrentes (US6).</summary>
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

        // ------------------------------------------------------------ importación (US6) --

        group.MapGet("/novelties/import-template", (INoveltyFileParser parser) =>
                Results.File(parser.Template(), "text/csv; charset=utf-8", "plantilla_novedades.csv"))
            .WithName("Payroll_Novelties_ImportTemplate")
            .RequirePermission("Payroll.Novelties.Import");

        group.MapPost("/pay-periods/{periodId:guid}/novelties/import",
                async (Guid periodId, [FromForm] IFormFile file, ISender sender, HttpContext http, CancellationToken ct) =>
                {
                    if (file is null || file.Length == 0)
                        return Results.Json(new { code = "Payroll.ImportInvalid", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
                    if (file.Length > ImportNoveltiesCommandValidator.MaxBytes)
                        return Results.Json(new { code = "Payroll.ImportFileTooLarge", message = "El archivo pesa más de 5 MB. Divídalo en varios lotes.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status413PayloadTooLarge);

                    await using var stream = file.OpenReadStream();
                    var result = await sender.Send(new ImportNoveltiesCommand(periodId, stream, file.FileName, file.Length), ct);
                    if (result.IsFailure) return ErrorEnvelopeFilter.Translate(http, result);
                    // Con errores: 422 y el mismo DTO, applied = 0, nada persistido (contracts/api.md §3).
                    return result.Value.HasErrors ? Results.UnprocessableEntity(result.Value) : Results.Ok(result.Value);
                })
            .WithName("Payroll_Novelties_Import")
            .DisableAntiforgery() // multipart sin form anti-XSRF — endpoint API
            .RequirePermission("Payroll.Novelties.Import");

        // ------------------------------------------------------------- recurrentes (US6) --

        group.MapGet("/recurring-novelties", async (Guid? employeeId, bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRecurringNoveltiesQuery(employeeId, active), ct))
            .WithName("Payroll_RecurringNovelties_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.View");

        group.MapPost("/recurring-novelties", async (CreateRecurringNoveltyCommand command, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(command, ct);
                    return result.IsSuccess ? Results.Created($"/api/payroll/recurring-novelties/{result.Value}", new { publicId = result.Value }) : (object)result;
                })
            .WithName("Payroll_RecurringNovelties_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Create");

        group.MapPost("/recurring-novelties/{recurringId:guid}/deactivate", async (Guid recurringId, CancelBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateRecurringNoveltyCommand(recurringId, body.Reason), ct))
            .WithName("Payroll_RecurringNovelties_Deactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Cancel");
    }

    public sealed record CancelBody(string Reason);
    public sealed record SalaryChangeBody(decimal NewSalary, DateTime EffectiveFrom, string Reason);
}
