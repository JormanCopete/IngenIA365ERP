using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Las chequeras y los cheques girados (feature 015, US19, T0230; contracts/api.md §11): registrar y anular chequeras, el cheque en blanco dañado, la emisión de los cheques
/// de un lote de forma «cheques», la entrega con constancia, la anulación, el cobro y la impresión. El área <c>/checks</c> era de la Tesorería heredada (retirada sin alias en la fase 46): los nuevos van en
/// <c>/checkbooks</c> e <c>/issued-checks</c>. Cada escritura lleva <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public class CheckbooksEndpoints : ICarterModule
{
    public const string VerChequeras = "Treasury.Checkbooks.View";
    public const string Chequeras = "Treasury.Checkbooks.Manage";
    public const string Ver = "Treasury.Checks.View";
    public const string Imprimir = "Treasury.Checks.Print";
    public const string Entregar = "Treasury.Checks.Deliver";
    public const string Anular = "Treasury.Checks.Void";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var k = TreasuryGroup.Grupo(app, "/checkbooks", "Tesorería Chequeras");
        k.MapGet("/", async (Guid? account, ISender s, CancellationToken ct) => await s.Send(new ListCheckbooksQuery(account), ct))
            .WithName("Treasury_Checkbooks_List").RequirePermission(new AlgunPermiso(VerChequeras, Chequeras, Imprimir));
        k.MapPost("/", async (ChequeraRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterCheckbookCommand(body.BankAccountPublicId, body.FromNumber, body.ToNumber, body.CustodianUserPublicId, body.CustodyAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_Checkbooks_Register").ConClaveDeOperacion().RequirePermission(Chequeras);
        k.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VoidCheckbookCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Checkbooks_Void").ConClaveDeOperacion().RequirePermission(Chequeras);
        k.MapPost("/{id:guid}/spoiled", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SpoilBlankCheckCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Checkbooks_Spoiled").ConClaveDeOperacion().RequirePermission(Imprimir);

        var c = TreasuryGroup.Grupo(app, "/issued-checks", "Tesorería Cheques girados");
        c.MapGet("/", async (Guid? checkbook, IssuedCheckState? state, ISender s, CancellationToken ct) => await s.Send(new ListIssuedChecksQuery(checkbook, state), ct))
            .WithName("Treasury_IssuedChecks_List").RequirePermission(new AlgunPermiso(Ver, Imprimir, Entregar, Anular));
        // Segunda ronda del 2026-10-27 (D-j): un cheque girado por su PublicId, como dice api.md.
        c.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetIssuedCheckQuery(id), ct))
            .WithName("Treasury_IssuedChecks_Get").RequirePermission(new AlgunPermiso(Ver, Imprimir, Entregar, Anular));
        c.MapPost("/batches/{batchId:guid}", async (Guid batchId, EmisionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new IssueChecksForBatchCommand(batchId, body.CheckbookPublicId, body.Crossed ?? true, body.FirstBeneficiaryOnly ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_IssuedChecks_IssueForBatch").ConClaveDeOperacion().RequirePermission(Imprimir);
        c.MapPost("/{id:guid}/deliver", async (Guid id, EntregaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DeliverIssuedCheckCommand(id, body.DeliveryReceiptAttachmentPublicId, body.DeliveredOn) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_IssuedChecks_Deliver").ConClaveDeOperacion().RequirePermission(Entregar);
        c.MapPost("/{id:guid}/cashed", async (Guid id, CobroRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkIssuedCheckCashedCommand(id, body?.CashedOn) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_IssuedChecks_Cashed").ConClaveDeOperacion().RequirePermission(Entregar);
        c.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VoidIssuedCheckCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_IssuedChecks_Void").ConClaveDeOperacion().RequirePermission(Anular);
        c.MapGet("/{id:guid}/document", async (Guid id, ISender s, IDocumentosDeTesoreriaEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetIssuedCheckDocumentQuery(id), ct);
                return r.IsSuccess ? Results.File(pdf.Cheque(r.Value), "application/pdf", $"cheque-{r.Value.CheckNumber}.pdf") : (object)r;
            })
            .WithName("Treasury_IssuedChecks_Document").RequirePermission(Imprimir);
    }

    public sealed record ChequeraRequest(Guid BankAccountPublicId, long FromNumber, long ToNumber, Guid? CustodianUserPublicId, Guid? CustodyAttachmentPublicId);

    public sealed record MotivoRequest(string? Reason);

    public sealed record EmisionRequest(Guid CheckbookPublicId, bool? Crossed, bool? FirstBeneficiaryOnly);

    public sealed record EntregaRequest(Guid DeliveryReceiptAttachmentPublicId, DateOnly? DeliveredOn);

    public sealed record CobroRequest(DateOnly? CashedOn);
}
