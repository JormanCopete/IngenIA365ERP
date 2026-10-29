using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Statements;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// El extracto bancario (feature 015, US6, T0198; R-06; contracts/api.md §12): la carga única —por multipart con el archivo (hasta 10 MB) o por captura a mano— con
/// revisión previa (<c>preview=true</c>) y error por línea, y su consulta. La usan la pantalla de Tesorería y la de Recaudo con <c>Treasury.Statements.Load</c>. (nuevo)
/// </summary>
public class StatementsEndpoints : ICarterModule
{
    public const string Cargar = "Treasury.Statements.Load";
    public const string Ver = "Treasury.Statements.View";
    public const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/statements", "Tesorería Extractos");
        g.MapPost("/", CargarAsync).WithName("Treasury_Statements_Load").DisableAntiforgery().ConClaveDeOperacion().RequirePermission(Cargar);
        g.MapPost("/captured", async (CapturaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new LoadBankStatementCommand(body.BankAccountPublicId, Captured: body.Lines ?? [], From: body.From, To: body.To,
                    OpeningBalance: body.OpeningBalance, ClosingBalance: body.ClosingBalance, Preview: body.Preview) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Statements_Capture").ConClaveDeOperacion().RequirePermission(Cargar);
        g.MapGet("/", async (Guid? account, ISender s, CancellationToken ct) => await s.Send(new ListBankStatementsQuery(account), ct))
            .WithName("Treasury_Statements_List").RequirePermission(new AlgunPermiso(Ver, Cargar));
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetBankStatementQuery(id), ct))
            .WithName("Treasury_Statements_Get").RequirePermission(new AlgunPermiso(Ver, Cargar));
        g.MapGet("/{id:guid}/lines", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new ListBankStatementLinesQuery(id), ct))
            .WithName("Treasury_Statements_Lines").RequirePermission(new AlgunPermiso(Ver, Cargar));
    }

    private static async Task<object?> CargarAsync([FromForm] Guid account, [FromForm] IFormFile archivo, [FromForm] Guid? format, [FromForm] bool? preview,
        [FromForm] bool? acceptIllegible, [FromForm] DateOnly? from, [FromForm] DateOnly? to, [FromForm] decimal? openingBalance, [FromForm] decimal? closingBalance,
        ISender sender, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
        if (archivo.Length > MaxBytes)
            return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return await sender.Send(new LoadBankStatementCommand(account, archivo.FileName, ms.ToArray(), format, null, from, to, openingBalance, closingBalance,
            preview ?? false, acceptIllegible ?? false) { OperationKey = http.ClaveDeOperacion() }, ct);
    }

    public sealed record CapturaRequest(Guid BankAccountPublicId, IReadOnlyList<MovimientoCapturado>? Lines, DateOnly? From, DateOnly? To, decimal? OpeningBalance,
        decimal? ClosingBalance, bool Preview);
}
