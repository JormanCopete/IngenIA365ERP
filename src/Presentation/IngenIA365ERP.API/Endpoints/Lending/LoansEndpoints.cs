using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los créditos (feature 013; contracts/api.md §8.1 y §8.4). US3 (T0457, FR-048, C11): anular un crédito pendiente de giro
/// (<c>Lending.Disbursements.Void</c>, con motivo e <c>Idempotency-Key</c>), que reversa en la misma transacción las recogidas del desembolso
/// por el contrato. US4 (T0506, FR-061–FR-070, C83): las consultas del crédito —la lista con los saldos de la posición diaria vigente, el
/// detalle, el estado de cuenta y la liquidación a <c>date</c> (D) con la información hasta <c>recordedAsOf</c> (R), el plan con sus versiones,
/// las operaciones con su comprobante de aplicación y el libro—, todas de lectura y sin clave. La liquidación exige
/// <c>Lending.Payoffs.Read</c> y se audita (<c>Lending.Payoff.Viewed</c>); lo demás, <c>Lending.Loans.Read</c>. Fuera del alcance de oficina o
/// línea, el mismo 404 que si el crédito no existiera. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class LoansEndpoints : ICarterModule
{
    public const string AnularCredito = "Lending.Disbursements.Void";
    public const string LeerCreditos = "Lending.Loans.Read";
    public const string Liquidar = "Lending.Payoffs.Read";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var creditos = LendingGroup.Grupo(app, "/loans", "Cartera Créditos");

        creditos.MapGet("/", async (string? search, LoanStatus? status, Guid? line, Guid? office, Guid? personPublicId, string? role, string? modality,
                int? minDaysPastDue, LoanOrigin? origin, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLoansQuery(search, status, line, office, personPublicId, role, modality, minDaysPastDue, origin, page ?? 1,
                    pageSize ?? 20), ct))
            .WithName("Lending_Loans_List")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLoanQuery(id), ct))
            .WithName("Lending_Loans_Get")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}/statement", async (Guid id, DateOnly? date, DateTime? recordedAsOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLoanStatementQuery(id, date, Utc(recordedAsOf)), ct))
            .WithName("Lending_Loans_Statement")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}/settlement", async (Guid id, DateOnly? date, DateTime? recordedAsOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLoanSettlementQuery(id, date, Utc(recordedAsOf)), ct))
            .WithName("Lending_Loans_Settlement")
            .RequirePermission(Liquidar);

        creditos.MapGet("/{id:guid}/schedule", async (Guid id, int? version, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRepaymentScheduleQuery(id, version), ct))
            .WithName("Lending_Loans_Schedule")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}/schedule/versions", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRepaymentScheduleVersionsQuery(id), ct))
            .WithName("Lending_Loans_ScheduleVersions")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}/operations", async (Guid id, LoanOperationKind? kind, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLoanOperationsQuery(id, kind, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Loans_Operations")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}/operations/{operationId:guid}", async (Guid id, Guid operationId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLoanOperationQuery(id, operationId), ct))
            .WithName("Lending_Loans_Operation")
            .RequirePermission(LeerCreditos);

        creditos.MapGet("/{id:guid}/transactions", async (Guid id, DateOnly? from, DateOnly? to, LoanConcept? concept, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLoanTransactionsQuery(id, from, to, concept, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Loans_Transactions")
            .RequirePermission(LeerCreditos);

        creditos.MapPost("/{id:guid}/void", async (Guid id, AnulacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidPendingLoanCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Loans_Void")
            .ConClaveDeOperacion()
            .RequirePermission(AnularCredito);
    }

    /// <summary>
    /// R llega como instante: con zona se lleva a UTC; sin zona se toma como UTC (es lo que devuelve la API en <c>registeredAt</c> y en la
    /// liquidación), nunca como hora local del servidor.
    /// </summary>
    internal static DateTime? Utc(DateTime? r) => r switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } u => u,
        { Kind: DateTimeKind.Local } l => l.ToUniversalTime(),
        { } u => DateTime.SpecifyKind(u, DateTimeKind.Utc),
    };

    /// <summary>El motivo de la anulación.</summary>
    public sealed record AnulacionRequest(string? Reason);
}
