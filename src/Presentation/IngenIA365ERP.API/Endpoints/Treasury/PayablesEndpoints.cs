using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Payables;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Los pagos a proveedores y de cuentas por pagar de T2 (feature 015, fase 42, T0456; FR-124 a FR-127; contracts/api.md §13): lo que se puede pagar, la conversión de una
/// cuenta por pagar contable en orden (también por una parte) y el calendario de pagos. Las facturas de Comercial llegan solas por la bandeja. (nuevo)
/// </summary>
public class PayablesEndpoints : ICarterModule
{
    public const string Ver = "Treasury.Payables.View";
    public const string Administrar = "Treasury.Payables.Manage";
    public const string Pagar = "Treasury.Payables.Pay";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/payables", "Tesorería Cuentas por pagar");
        g.MapGet("/", async (Guid? person, DateOnly? dueUntil, ISender s, CancellationToken ct) => await s.Send(new ListPayablesQuery(person, dueUntil), ct))
            .WithName("Treasury_Payables_List").RequirePermission(new AlgunPermiso(Ver, Administrar, Pagar));
        g.MapGet("/calendar", async (DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) => await s.Send(new GetPaymentCalendarQuery(from, to), ct))
            .WithName("Treasury_Payables_Calendar").RequirePermission(new AlgunPermiso(Ver, Administrar, Pagar));
        g.MapPost("/orders", async (ConversionRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ConvertAccountingPayableCommand(b.PersonPublicId ?? Guid.Empty, b.CrossDocument ?? string.Empty, b.Amount ?? 0m, b.PayDate, b.Concept)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Payables_Convert").ConClaveDeOperacion().RequirePermission(new AlgunPermiso(Administrar, Pagar));
    }

    public sealed record ConversionRequest(Guid? PersonPublicId, string? CrossDocument, decimal? Amount, DateOnly? PayDate, string? Concept);
}
