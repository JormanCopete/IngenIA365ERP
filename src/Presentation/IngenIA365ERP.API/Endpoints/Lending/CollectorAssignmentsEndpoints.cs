using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Assignments;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El reparto de gestores y la agenda (feature 013, K4, US12, T0956; contracts/api.md §14.5, FR-118; data-model §26.6): la historia de asignaciones
/// (sólo inserción), asignar a mano, repartir por pedido con vista previa, las reglas de reparto con vigencia y la agenda del día. Leer exige
/// <c>Lending.Collections.Read</c> y escribir <c>Lending.Collections.Assign</c>, con <c>Idempotency-Key</c> y motivo. Todo filtra por el alcance de
/// oficina y línea. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CollectorAssignmentsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Collections.Read";
    public const string Asignar = "Lending.Collections.Assign";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var asignaciones = LendingGroup.Grupo(app, "/collector-assignments", "Cartera Reparto de gestores");

        asignaciones.MapGet("/", async ([AsParameters] ListCollectorAssignmentsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_CollectorAssignments_List")
            .RequirePermission(Leer);

        asignaciones.MapPost("/", async (AsignarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AssignCollectorCommand(body.LoanPublicIds ?? [], body.CollectorUserPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), "/api/lending/collector-assignments"))
            .WithName("Lending_CollectorAssignments_Assign")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);

        asignaciones.MapPost("/distribute", async (RepartirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DistributeCollectionsCommand(body.Basis, body.Filters ?? new DistributionFilters(), body.CollectorUserPublicIds ?? [],
                    body.PreviewOnly, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_CollectorAssignments_Distribute")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);

        asignaciones.MapGet("/rules", async (DateOnly? date, bool? includeClosed, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCollectorAssignmentRulesQuery(date, includeClosed ?? false), ct))
            .WithName("Lending_CollectorAssignments_Rules")
            .RequirePermission(Leer);

        asignaciones.MapPost("/rules", async (ReglaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateCollectorAssignmentRuleCommand(body.Priority, body.Distribution, body.Collectors ?? [],
                    body.ValidFrom, body.Reason ?? string.Empty, body.OfficePublicId, body.StageCode, body.CreditLinePublicId, body.PaymasterPersonPublicId,
                    body.MinBalance, body.MaxBalance, body.AssignWholeDebtor)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), r => $"/api/lending/collector-assignments/rules/{r.PublicId}"))
            .WithName("Lending_CollectorAssignments_CreateRule")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);

        asignaciones.MapPost("/rules/{id:guid}/close", async (Guid id, CerrarReglaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseCollectorAssignmentRuleCommand(id, body.ValidTo, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_CollectorAssignments_CloseRule")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);

        var agenda = LendingGroup.Grupo(app, "/agenda", "Cartera Agenda del gestor");

        agenda.MapGet("/", async (DateOnly? date, Guid? collector, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCollectorAgendaQuery(date, collector), ct))
            .WithName("Lending_Agenda_Get")
            .RequirePermission(Leer);
    }

    /// <summary>Asignar a mano (§14.5).</summary>
    public sealed record AsignarRequest(IReadOnlyList<Guid>? LoanPublicIds, Guid CollectorUserPublicId, string? Reason);

    /// <summary>Repartir por pedido; con <c>previewOnly</c> no escribe (§14.5).</summary>
    public sealed record RepartirRequest(AssignmentDistribution Basis, DistributionFilters? Filters, IReadOnlyList<Guid>? CollectorUserPublicIds, bool PreviewOnly,
        string? Reason);

    /// <summary>Una regla de reparto con vigencia (data-model §26.6).</summary>
    public sealed record ReglaRequest(
        int Priority,
        AssignmentDistribution Distribution,
        IReadOnlyList<CollectorWeightInput>? Collectors,
        DateOnly ValidFrom,
        string? Reason,
        Guid? OfficePublicId = null,
        string? StageCode = null,
        Guid? CreditLinePublicId = null,
        Guid? PaymasterPersonPublicId = null,
        decimal? MinBalance = null,
        decimal? MaxBalance = null,
        bool AssignWholeDebtor = false);

    /// <summary>Cerrar la vigencia de una regla.</summary>
    public sealed record CerrarReglaRequest(DateOnly ValidTo, string? Reason);
}
