using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Integration.DeadLetters;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Common;

/// <summary>
/// Los mensajes muertos (feature 023, D3, D6; contracts/api.md §4): documentos electrónicos, seguimientos y entregas de la bandeja que agotaron
/// sus intentos transitorios. Ver con <c>Platform.DeadLetters.View</c>; reprocesar, con motivo e <c>Idempotency-Key</c>, con
/// <c>Platform.DeadLetters.Reprocess</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DeadLettersEndpoints : ICarterModule
{
    public const string Ruta = "/api/platform/dead-letters";
    public const string Ver = "Platform.DeadLetters.View";
    public const string Reprocesar = "Platform.DeadLetters.Reprocess";

    /// <summary>El cuerpo de <c>POST /reprocess</c>.</summary>
    public sealed record ReprocesoRequest(IReadOnlyList<DeadLetterRef>? Items, string? Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Platform Dead Letters")
            .RequireAuthorization();

        group.MapGet("/", async (DeadLetterKind? kind, DateTime? from, DateTime? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDeadLettersQuery(kind, from, to, page ?? 1, pageSize ?? 50), ct))
            .WithName("Platform_DeadLetters_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapGet("/summary", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDeadLettersSummaryQuery(), ct))
            .WithName("Platform_DeadLetters_Summary")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapGet("/{kind}/{id:guid}", async (DeadLetterKind kind, Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDeadLetterQuery(kind, id), ct))
            .WithName("Platform_DeadLetters_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapPost("/reprocess", async (ReprocesoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReprocessDeadLettersCommand(body.Items ?? [], body.Reason ?? string.Empty)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Platform_DeadLetters_Reprocess")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Reprocesar);
    }
}
