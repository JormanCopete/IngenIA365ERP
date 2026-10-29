using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Reconciliation.Acquirers;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// La conciliación de adquirentes de T2 (feature 015, fase 44, T0464; FR-133, FR-218; contracts/api.md §13): la carga del archivo de liquidación de una red de tarjetas, su
/// lista y la decisión de la diferencia sobre la tolerancia. (nuevo)
/// </summary>
public class AcquirerSettlementsEndpoints : ICarterModule
{
    public const string Cargar = "Treasury.AcquirerSettlements.Load";
    public const string Aprobar = "Treasury.AcquirerSettlements.Approve";
    public const string Ver = "Treasury.Reconciliation.View";
    private const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/acquirer-settlements", "Tesorería Adquirentes");
        g.MapGet("/", async (Guid? account, AcquirerSettlementState? state, ISender s, CancellationToken ct) => await s.Send(new ListAcquirerSettlementsQuery(account, state), ct))
            .WithName("Treasury_AcquirerSettlements_List").RequirePermission(new AlgunPermiso(Ver, Cargar, Aprobar));
        g.MapPost("/", CargarAsync).WithName("Treasury_AcquirerSettlements_Load").DisableAntiforgery().ConClaveDeOperacion().RequirePermission(Cargar);
        g.MapPost("/{id:guid}/approve", async (Guid id, DecisionRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DecideAcquirerSettlementCommand(id, true, b.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_AcquirerSettlements_Approve").ConClaveDeOperacion().RequirePermission(Aprobar);
        g.MapPost("/{id:guid}/reject", async (Guid id, DecisionRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DecideAcquirerSettlementCommand(id, false, b.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_AcquirerSettlements_Reject").ConClaveDeOperacion().RequirePermission(Aprobar);
    }

    private static async Task<object?> CargarAsync([FromForm] IFormFile archivo, [FromForm] Guid acquirerPublicId, [FromForm] Guid bankAccountPublicId,
        [FromForm] Guid formatPublicId, [FromForm] DateOnly? settlementDate, [FromForm] Guid? statementLinePublicId, ISender sender, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
        if (archivo.Length > MaxBytes)
            return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return await sender.Send(new LoadAcquirerSettlementCommand(acquirerPublicId, bankAccountPublicId, formatPublicId, archivo.FileName, ms.ToArray(), settlementDate,
            statementLinePublicId) { OperationKey = http.ClaveDeOperacion() }, ct);
    }

    public sealed record DecisionRequest(string? Reason);
}
