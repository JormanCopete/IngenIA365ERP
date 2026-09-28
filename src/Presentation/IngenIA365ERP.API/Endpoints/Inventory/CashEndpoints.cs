using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La caja (feature 012; contracts/api.md §21; T631): sesiones (<c>/api/inventory/cash-sessions</c> con su esperado, cierre con arqueo,
/// recuento y el PDF del arqueo), movimientos de caja (<c>/cash-movements</c>: documentos de la clase <c>CashMovement</c> del grupo
/// <c>Cash</c>, con su recibo en PDF) y cierre del día (<c>/day-closes</c> y su reapertura). Permisos de §21: <c>CashSessions.{View,
/// Open, Close}</c>, <c>CashMovements.{Create, Approve}</c> y <c>DayClose.{Execute, Reopen}</c>; <c>ViewAll</c> lo mira la consulta
/// (una sesión ajena sin él es el 404 de lo inexistente). Toda escritura con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class CashEndpoints : ICarterModule
{
    public const string VerSesiones = "Inventory.CashSessions.View";
    public const string AbrirSesion = "Inventory.CashSessions.Open";
    public const string CerrarSesion = "Inventory.CashSessions.Close";
    public const string CrearMovimiento = "Inventory.CashMovements.Create";
    public const string AprobarMovimiento = "Inventory.CashMovements.Approve";
    public const string EjecutarCierreDelDia = "Inventory.DayClose.Execute";
    public const string ReabrirCierreDelDia = "Inventory.DayClose.Reopen";

    private const string TipoPdf = "application/pdf";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Sesiones(app);
        Movimientos(app);
        CierresDelDia(app);
    }

    private static void Sesiones(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/cash-sessions")
            .WithTags("Inventory Cash Sessions")
            .RequireAuthorization();

        group.MapGet("/", async (
                bool? mine, Guid? pointOfSale, Guid? cashRegister, CashSessionStatus? status, DateOnly? from, DateOnly? to, Guid? cashier,
                int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCashSessionsQuery(mine, pointOfSale, cashRegister, status, from, to, cashier,
                    new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_CashSessions_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCashSessionQuery(id), ct))
            .WithName("Inventory_CashSessions_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapPost("/", async (AbrirSesionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new OpenCashSessionCommand(body.CashRegisterPublicId ?? Guid.Empty, body.OpeningBase, body.Denominations)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/cash-sessions/{result.Value.Session.CashSessionPublicId}", result.Value)
                    : result;
            })
            .WithName("Inventory_CashSessions_Open")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(AbrirSesion);

        group.MapGet("/{id:guid}/expected", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCashSessionExpectedQuery(id), ct))
            .WithName("Inventory_CashSessions_Expected")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapPost("/{id:guid}/close", async (Guid id, CerrarSesionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseCashSessionCommand(id, body.Counts ?? [], body.ClosingWithdrawal) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_CashSessions_Close")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(CerrarSesion);

        group.MapPost("/{id:guid}/recount", async (Guid id, CerrarSesionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RecountCashSessionCommand(id, body.Counts ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_CashSessions_Recount")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(CerrarSesion);

        group.MapGet("/{id:guid}/count-report", async (Guid id, HttpContext http, ISender sender, IDocumentosDeCajaEnPdf pdf, CancellationToken ct) =>
            {
                var modelo = await sender.Send(new GetCashCountReportQuery(id), ct);
                if (modelo.IsFailure) return ErrorEnvelopeFilter.Translate(http, modelo);
                return Results.File(pdf.Arqueo(modelo.Value), TipoPdf, $"arqueo-{id:N}.pdf");
            })
            .WithName("Inventory_CashSessions_CountReport")
            .RequirePermission(VerSesiones);
    }

    private static void Movimientos(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/cash-movements")
            .WithTags("Inventory Cash Movements")
            .RequireAuthorization();

        group.MapGet("/", async (
                Guid? cashSession, Guid? pointOfSale, CashMovementKind? kind, DocumentStatus? status, DateOnly? from, DateOnly? to,
                int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCashMovementsQuery(cashSession, pointOfSale, kind, status, from, to, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_CashMovements_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCashMovementQuery(id), ct))
            .WithName("Inventory_CashMovements_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapPost("/", async (CashMovementInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var borrador = await BorradorAsync(body, sender, ct);
                var result = await sender.Send(new SaveInventoryDraftCommand(null, DocumentClassGroup.Cash, borrador) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                var movimiento = await sender.Send(new GetCashMovementQuery(result.Value.PublicId), ct);
                return Results.Created($"/api/inventory/cash-movements/{result.Value.PublicId}", movimiento.IsSuccess ? (object)movimiento.Value : result.Value);
            })
            .WithName("Inventory_CashMovements_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(CrearMovimiento);

        group.MapPut("/{id:guid}", async (Guid id, CashMovementInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var borrador = await BorradorAsync(body, sender, ct);
                var result = await sender.Send(new SaveInventoryDraftCommand(id, DocumentClassGroup.Cash, borrador) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (result.IsFailure) return (object)result;
                var movimiento = await sender.Send(new GetCashMovementQuery(id), ct);
                return movimiento.IsSuccess ? (object)movimiento.Value : result.Value;
            })
            .WithName("Inventory_CashMovements_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(CrearMovimiento);

        group.MapPost("/{id:guid}/confirm", async (Guid id, CicloDeDocumentoRutas.ConfirmarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmInventoryDocumentCommand(id, DocumentClassGroup.Cash)
                {
                    RowVersion = body?.RowVersion,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_CashMovements_Confirm")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(CrearMovimiento);

        group.MapPost("/{id:guid}/void", async (Guid id, CicloDeDocumentoRutas.AnularRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new VoidInventoryDocumentCommand(id, DocumentClassGroup.Cash, body.Reason ?? string.Empty, body.OperationDate)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"{http.Request.Path.Value}", result.Value) : result;
            })
            .WithName("Inventory_CashMovements_Void")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(AprobarMovimiento);

        group.MapPost("/{id:guid}/discard", async (Guid id, CicloDeDocumentoRutas.MotivoRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardInventoryDraftCommand(id, DocumentClassGroup.Cash, body?.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_CashMovements_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(CrearMovimiento);

        group.MapGet("/{id:guid}/receipt", async (Guid id, HttpContext http, ISender sender, IDocumentosDeCajaEnPdf pdf, CancellationToken ct) =>
            {
                var modelo = await sender.Send(new GetCashMovementReceiptQuery(id), ct);
                if (modelo.IsFailure) return ErrorEnvelopeFilter.Translate(http, modelo);
                return Results.File(pdf.ComprobanteDeMovimiento(modelo.Value), TipoPdf, $"movimiento-de-caja-{id:N}.pdf");
            })
            .WithName("Inventory_CashMovements_Receipt")
            .RequirePermission(VerSesiones);
    }

    private static void CierresDelDia(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/day-closes")
            .WithTags("Inventory Day Closes")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? pointOfSale, DateOnly? from, DateOnly? to, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDayClosesQuery(pointOfSale, from, to), ct))
            .WithName("Inventory_DayCloses_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDayCloseQuery(id), ct))
            .WithName("Inventory_DayCloses_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(VerSesiones);

        group.MapPost("/", async (CierreDelDiaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ExecuteDayCloseCommand(body.PointOfSalePublicId ?? Guid.Empty, body.OperatingDate ?? DateOnly.MinValue)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/inventory/day-closes/{result.Value.DayClosePublicId}", result.Value) : result;
            })
            .WithName("Inventory_DayCloses_Execute")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(EjecutarCierreDelDia);

        group.MapPost("/{id:guid}/reopen", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReopenDayCloseCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_DayCloses_Reopen")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(ReabrirCierreDelDia);
    }

    /// <summary>
    /// El borrador del ciclo común para un movimiento: sin <c>documentTypePublicId</c>, el tipo por defecto de la clase
    /// (<see cref="GetDefaultCashMovementTypeQuery"/>); si la cooperativa no tiene ninguno, el vacío, y el guardado responde el error del tipo.
    /// </summary>
    private static async Task<SaveInventoryDraftRequest> BorradorAsync(CashMovementInput body, ISender sender, CancellationToken ct)
    {
        var porDefecto = Guid.Empty;
        if (body.DocumentTypePublicId is null && await sender.Send(new GetDefaultCashMovementTypeQuery(), ct) is { IsSuccess: true, Value: { } tipo })
            porDefecto = tipo;
        return body.ComoBorrador(porDefecto);
    }

    /// <summary><c>POST /cash-sessions</c>: la caja, la base (obligatoria con base del día) y sus denominaciones.</summary>
    public sealed record AbrirSesionRequest(Guid? CashRegisterPublicId, decimal? OpeningBase, IReadOnlyList<DenominationCountInput>? Denominations);

    /// <summary><c>POST /cash-sessions/{id}/close</c> y <c>/recount</c>: lo contado por medio y, al cerrar, el retiro de cierre.</summary>
    public sealed record CerrarSesionRequest(IReadOnlyList<CashCountInput>? Counts, ClosingWithdrawalInput? ClosingWithdrawal);

    /// <summary><c>POST /day-closes</c>: el punto y la fecha operativa.</summary>
    public sealed record CierreDelDiaRequest(Guid? PointOfSalePublicId, DateOnly? OperatingDate);
}
