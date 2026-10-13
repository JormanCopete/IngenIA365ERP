using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination.Queries;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las reservas de número que Comercial pide dentro de sus confirmaciones de venta a crédito (feature 013, US6, T0597; contracts/api.md §7.3; FR-057,
/// T22): aquí sólo se consultan (<c>Lending.NumberReservations.Read</c>, con el alcance de la oficina del crédito que la usó). Reservar y liberar no tienen
/// ruta: corren dentro de la unidad de trabajo de Comercial por <c>IReservasDeCartera</c>. (nuevo)
/// </summary>
public class NumberReservationsEndpoints : ICarterModule
{
    public const string LeerReservas = "Lending.NumberReservations.Read";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var reservas = LendingGroup.Grupo(app, "/number-reservations", "Cartera Reservas de número");

        reservas.MapGet("/", async (LoanNumberReservationStatus? status, string? number, DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListLoanNumberReservationsQuery(status, number, from, to, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_NumberReservations_List")
            .RequirePermission(LeerReservas);
    }
}
