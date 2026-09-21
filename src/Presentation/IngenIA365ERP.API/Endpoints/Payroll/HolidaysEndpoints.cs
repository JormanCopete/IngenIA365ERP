using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Holidays;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 010 (contracts/api.md §10.2): calendario de festivos.</summary>
public sealed class HolidaysEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/holidays")
            .WithTags("PayrollHolidays")
            .RequireAuthorization();

        group.MapGet("/", async (int? year, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListHolidaysQuery(year), ct))
            .WithName("Payroll_Holidays_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Holidays.View");

        group.MapPost("/", async (CreateHolidayCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/holidays/{result.Value}", new { holidayPublicId = result.Value })
                    : (object)result;
            })
            .WithName("Payroll_Holidays_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Holidays.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteHolidayCommand(id), ct))
            .WithName("Payroll_Holidays_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Holidays.Manage");
    }
}
