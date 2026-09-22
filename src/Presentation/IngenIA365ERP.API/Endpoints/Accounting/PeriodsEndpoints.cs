using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.Periods;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Ejercicios y períodos (feature 009, contracts/api.md §5): abrir el ejercicio, cerrar y reabrir
/// cada mes, y desde E2 (US6) cerrar y reabrir el ejercicio entero: el cierre genera el comprobante
/// <c>CI</c> contra la cuenta de resultado y reabrir lo reversa en su misma fecha.
/// </summary>
public class PeriodsEndpoints : ICarterModule
{
    public sealed record AbrirEjercicioRequest(int Year);
    public sealed record MotivoRequest(string Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/periods")
            .WithTags("AccountingPeriods")
            .RequireAuthorization();

        group.MapGet("/", async ([FromQuery] int? year, ISender sender, CancellationToken ct) => await sender.Send(new ListPeriodsQuery(year), ct))
            .WithName("Accounting_Periods_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.View");

        group.MapGet("/years", async (ISender sender, CancellationToken ct) => await sender.Send(new ListFiscalYearsQuery(), ct))
            .WithName("Accounting_Periods_Years")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.View");

        group.MapPost("/years", async (AbrirEjercicioRequest body, ISender sender, CancellationToken ct) => await sender.Send(new OpenFiscalYearCommand(body.Year), ct))
            .WithName("Accounting_Periods_OpenYear")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.CloseYear");

        group.MapPost("/years/{year:int}/close", async (int year, ISender sender, CancellationToken ct) => await sender.Send(new CloseFiscalYearCommand(year), ct))
            .WithName("Accounting_Periods_CloseYear")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.CloseYear");

        group.MapPost("/years/{year:int}/reopen", async (int year, MotivoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReopenFiscalYearCommand(year, body.Reason), ct))
            .WithName("Accounting_Periods_ReopenYear")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.CloseYear");

        group.MapPost("/{year:int}/{month:int}/close", async (int year, int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new ClosePeriodCommand(year, month), ct))
            .WithName("Accounting_Periods_Close")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.Close");

        group.MapPost("/{year:int}/{month:int}/reopen", async (int year, int month, MotivoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReopenPeriodCommand(year, month, body.Reason), ct))
            .WithName("Accounting_Periods_Reopen")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Periods.Reopen");
    }
}
