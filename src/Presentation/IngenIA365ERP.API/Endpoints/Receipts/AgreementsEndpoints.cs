using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Agreements;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// Los convenios de recaudo y los canales externos por archivo (feature 015, R2, fase 38, T0435; FR-088 a FR-093; contracts/api.md §8): los convenios con sus versiones,
/// la carga del archivo del convenio o de la pasarela (multipart, con revisión previa), su conciliación con el abono y la referencia de pago estable. Sin botón de pago en
/// línea (C2 = A). (nuevo)
/// </summary>
public class AgreementsEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Agreements.View";
    public const string Administrar = "Receipts.Agreements.Manage";
    public const string Cargar = "Receipts.Agreements.LoadFile";
    public const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/collection-agreements", "Recaudo Convenios");
        g.MapGet("/", async (bool? onlyActive, ISender s, CancellationToken ct) => await s.Send(new ListCollectionAgreementsQuery(onlyActive ?? false), ct))
            .WithName("Receipts_Agreements_List").RequirePermission(new AlgunPermiso(Ver, Administrar, Cargar));
        g.MapPost("/", async (ConvenioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(Comando(null, body) with { OperationKey = http.ClaveDeOperacion() }, ct), c => $"/api/receipts/collection-agreements/{c.PublicId}"))
            .WithName("Receipts_Agreements_Create").ConClaveDeOperacion().RequirePermission(Administrar);
        g.MapPut("/{id:guid}", async (Guid id, ConvenioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(Comando(id, body) with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Agreements_Update").ConClaveDeOperacion().RequirePermission(Administrar);
        g.MapPost("/{id:guid}/files", CargarAsync).WithName("Receipts_Agreements_LoadFile").DisableAntiforgery().ConClaveDeOperacion().RequirePermission(Cargar);
        g.MapGet("/files", async (Guid? agreement, ISender s, CancellationToken ct) => await s.Send(new ListCollectionFilesQuery(agreement), ct))
            .WithName("Receipts_Agreements_Files").RequirePermission(new AlgunPermiso(Ver, Cargar));
        g.MapPost("/{id:guid}/reconciliations", async (Guid id, ConciliacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReconcileCollectionFileCommand(id, body.FilePublicId ?? Guid.Empty, body.StatementLinePublicId ?? Guid.Empty, body.Commission ?? 0m,
                    body.Vat ?? 0m, body.Withholdings, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Agreements_Reconcile").ConClaveDeOperacion().RequirePermission(Administrar);
        g.MapGet("/reconciliations", async (Guid? agreement, AgreementReconciliationState? state, ISender s, CancellationToken ct) =>
                await s.Send(new ListAgreementReconciliationsQuery(agreement, state), ct))
            .WithName("Receipts_Agreements_Reconciliations").RequirePermission(new AlgunPermiso(Ver, Administrar));
        g.MapPost("/reconciliations/{id:guid}/approve", async (Guid id, NotaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveAgreementReconciliationCommand(id, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Agreements_ApproveReconciliation").ConClaveDeOperacion().RequirePermission(Administrar);

        var p = ReceiptsGroup.Grupo(app, "/payment-references", "Recaudo Referencias de pago");
        p.MapGet("/", async (Guid? person, string? reference, ISender s, CancellationToken ct) => await s.Send(new ListPaymentReferencesQuery(person, reference), ct))
            .WithName("Receipts_PaymentReferences_List").RequirePermission(new AlgunPermiso(Ver, Administrar));
        p.MapPost("/", async (ReferenciaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new IssuePaymentReferenceCommand(body.PersonPublicId ?? Guid.Empty, body.ProductPublicId, body.ProductNumber) { OperationKey = http.ClaveDeOperacion() },
                    ct))
            .WithName("Receipts_PaymentReferences_Issue").ConClaveDeOperacion().RequirePermission(Administrar);

        var gw = ReceiptsGroup.Grupo(app, "/gateway-files", "Recaudo Pasarelas");
        gw.MapGet("/", async (Guid? agreement, ISender s, CancellationToken ct) => await s.Send(new ListCollectionFilesQuery(agreement, Gateway: true), ct))
            .WithName("Receipts_GatewayFiles_List").RequirePermission(new AlgunPermiso(Ver, Cargar));
    }

    private static SaveCollectionAgreementCommand Comando(Guid? id, ConvenioRequest b) =>
        new(id, b.Code ?? string.Empty, b.Name ?? string.Empty, b.Kind ?? CollectionAgreementKind.Bank, b.OwnBankAccountPublicId ?? Guid.Empty, b.FormatPublicId ?? Guid.Empty,
            b.ReferenceMode ?? PaymentReferenceMode.ByProduct, b.ReferenceLength ?? 0, b.CheckDigit ?? CheckDigitAlgorithm.Modulo10, b.ValidFrom, b.PartnerPersonPublicId,
            b.PartnerAgreementCode, b.CommissionPerPayment, b.CommissionRate, b.IsActive ?? true, b.Note);

    private static async Task<object?> CargarAsync(Guid id, [FromForm] IFormFile archivo, [FromForm] bool? preview, [FromForm] bool? acceptIllegible,
        [FromForm] int? declaredLines, [FromForm] decimal? declaredTotal, ISender sender, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
        if (archivo.Length > MaxBytes)
            return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return await sender.Send(new LoadCollectionFileCommand(id, archivo.FileName, ms.ToArray(), preview ?? false, acceptIllegible ?? false, declaredLines, declaredTotal)
            { OperationKey = http.ClaveDeOperacion() }, ct);
    }

    public sealed record ConvenioRequest(string? Code, string? Name, CollectionAgreementKind? Kind, Guid? OwnBankAccountPublicId, Guid? FormatPublicId,
        PaymentReferenceMode? ReferenceMode, int? ReferenceLength, CheckDigitAlgorithm? CheckDigit, DateOnly? ValidFrom, Guid? PartnerPersonPublicId,
        string? PartnerAgreementCode, decimal? CommissionPerPayment, decimal? CommissionRate, bool? IsActive, string? Note);

    public sealed record ConciliacionRequest(Guid? FilePublicId, Guid? StatementLinePublicId, decimal? Commission, decimal? Vat, IReadOnlyList<RetencionPedida>? Withholdings,
        string? Note);

    public sealed record NotaRequest(string? Note);

    public sealed record ReferenciaRequest(Guid? PersonPublicId, Guid? ProductPublicId, string? ProductNumber);
}
