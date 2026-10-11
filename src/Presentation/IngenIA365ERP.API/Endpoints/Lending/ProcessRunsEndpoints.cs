using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las corridas del proceso de Cartera (feature 013, US4, T0506; contracts/api.md §16.4; T5, T8): la lista y el detalle con sus errores, sin
/// datos del deudor (<c>Lending.Processes.View</c>), y ordenar o reintentar un ciclo diario (<c>Lending.Processes.Run</c>, con
/// <c>Idempotency-Key</c>). Ordenar <b>sólo encola</b>: responde 202 y la corrida la toma el procesador, con la persona como quien la pidió. El
/// cierre mensual y su reapertura se ordenan desde el cierre (§12.3) y la corrección por valor nacional la encola la réplica
/// (<c>Lending.ProcessRun.KindNotOrderable</c>). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ProcessRunsEndpoints : ICarterModule
{
    public const string Ver = "Lending.Processes.View";
    public const string Correr = "Lending.Processes.Run";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var corridas = LendingGroup.Grupo(app, "/process-runs", "Cartera Corridas");

        corridas.MapGet("/", async (LendingProcessKind? kind, LendingProcessRunStatus? status, DateOnly? from, DateOnly? to, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingProcessRunsQuery(kind, status, from, to, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_ProcessRuns_List")
            .RequirePermission(Ver);

        corridas.MapGet("/{id:guid}", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingProcessRunQuery(id, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_ProcessRuns_Get")
            .RequirePermission(Ver);

        corridas.MapPost("/", async (OrdenDeCorridaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RequestLendingProcessRunCommand(body.Kind, body.Date) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Accepted($"{LendingGroup.Prefijo}/process-runs/{r.Value.ProcessRunPublicId}", r.Value) : r;
            })
            .WithName("Lending_ProcessRuns_Request")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);

        corridas.MapPost("/{id:guid}/retry", async (Guid id, ReintentoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RetryLendingProcessRunCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Accepted($"{LendingGroup.Prefijo}/process-runs/{r.Value.ProcessRunPublicId}", r.Value) : r;
            })
            .WithName("Lending_ProcessRuns_Retry")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);
    }

    /// <summary><c>{ kind: DailyCycle, date? }</c> (§16.4); sin fecha, hoy.</summary>
    public sealed record OrdenDeCorridaRequest(LendingProcessKind Kind = LendingProcessKind.DailyCycle, DateOnly? Date = null);

    /// <summary>El motivo del reintento.</summary>
    public sealed record ReintentoRequest(string? Reason);
}
