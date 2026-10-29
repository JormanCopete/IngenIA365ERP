using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// La conciliación bancaria mínima (feature 015, US25, T0280; contracts/api.md §12): la única conciliación bancaria de la cooperativa (E-04 a la 009). Abrir por cuenta y mes,
/// cruzar solo o a mano, deshacer un cruce de una abierta, registrar partidas conciliatorias, cerrar con su informe y reabrir con motivo como versión nueva. (nuevo)
/// </summary>
public class ReconciliationsEndpoints : ICarterModule
{
    public const string Ver = "Treasury.Reconciliation.View";
    public const string Trabajar = "Treasury.Reconciliation.Work";
    public const string Cerrar = "Treasury.Reconciliation.Close";
    public const string Reabrir = "Treasury.Reconciliation.Reopen";
    public const string Aprobar = "Treasury.Reconciliation.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/reconciliations", "Tesorería Conciliación");
        g.MapGet("/", async (Guid? account, int? year, int? month, ISender s, CancellationToken ct) => await s.Send(new ListReconciliationsQuery(account, year, month), ct))
            .WithName("Treasury_Reconciliations_List").RequirePermission(new AlgunPermiso(Ver, Trabajar));
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetReconciliationQuery(id), ct))
            .WithName("Treasury_Reconciliations_Get").RequirePermission(new AlgunPermiso(Ver, Trabajar));
        g.MapPost("/", async (AbrirRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new OpenReconciliationCommand(body.BankAccountPublicId, body.Year, body.Month, body.StatementBalance) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Reconciliations_Open").ConClaveDeOperacion().RequirePermission(Trabajar);
        g.MapPost("/{id:guid}/auto-match", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AutoMatchReconciliationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Reconciliations_AutoMatch").ConClaveDeOperacion().RequirePermission(Trabajar);
        g.MapPost("/{id:guid}/matches", async (Guid id, CruceRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AddManualMatchCommand(id, body.StatementLinePublicId, body.Records.Select(r => new RegistroElegido(r.Kind, r.PublicId)).ToList(), body.Note)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_Reconciliations_AddMatch").ConClaveDeOperacion().RequirePermission(Trabajar);
        g.MapDelete("/{id:guid}/matches/{mid:guid}", async (Guid id, Guid mid, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RemoveMatchCommand(id, mid) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Reconciliations_RemoveMatch").ConClaveDeOperacion().RequirePermission(Trabajar);
        g.MapPost("/{id:guid}/items", async (Guid id, PartidaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AddReconcilingItemCommand(id, body.Kind, body.Amount, body.Since, body.StatementLinePublicId, body.SourcePublicId, body.Note)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_Reconciliations_AddItem").ConClaveDeOperacion().RequirePermission(Trabajar);
        g.MapPost("/{id:guid}/close", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new CloseReconciliationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Reconciliations_Close").ConClaveDeOperacion().RequirePermission(Cerrar);
        // Segunda ronda del 2026-10-27 (D-l; FR-233): otra persona aprueba la conciliación cerrada; es el único paso con segregación.
        g.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveReconciliationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Reconciliations_Approve").ConClaveDeOperacion().RequirePermission(Aprobar);
        g.MapPost("/{id:guid}/reopen", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReopenReconciliationCommand(id, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Reconciliations_Reopen").ConClaveDeOperacion().RequirePermission(Reabrir);
    }

    public sealed record AbrirRequest(Guid BankAccountPublicId, int Year, int Month, decimal? StatementBalance);

    public sealed record RegistroRequest(ReconciliationRecordKind Kind, Guid PublicId);

    public sealed record CruceRequest(Guid StatementLinePublicId, IReadOnlyList<RegistroRequest> Records, string? Note);

    public sealed record PartidaRequest(ReconcilingItemKind Kind, decimal Amount, DateOnly? Since, Guid? StatementLinePublicId, Guid? SourcePublicId, string? Note);

    public sealed record MotivoRequest(string Reason);
}
