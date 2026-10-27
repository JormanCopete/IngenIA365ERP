using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Application.Treasury.PaymentFiles;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// La bandeja única de órdenes de pago, los lotes y los archivos de pago (feature 015, US10, T0146; contracts/api.md §10, sin la respuesta ni los resultados, que
/// son de US11). Las escrituras llevan <c>Idempotency-Key</c>; cada ruta sólo reenvía al <see cref="ISender"/>. Las órdenes nacen por la bandeja de integración
/// (<c>DestinoTesoreria</c>), nunca por una ruta. (nuevo)
/// </summary>
public class PaymentsEndpoints : ICarterModule
{
    public const string VerOrdenes = "Treasury.Orders.View";
    public const string AdministrarOrdenes = "Treasury.Orders.Manage";
    public const string ResolverDestino = ResolucionDeDestinoDeOrdenes.Permiso;
    public const string PrepararLotes = "Treasury.Batches.Prepare";
    public const string AprobarLotes = "Treasury.Batches.Approve";
    public const string GenerarArchivo = "Treasury.PaymentFiles.Generate";
    public const string DescargarArchivo = "Treasury.PaymentFiles.Download";
    public const string MarcarEnviado = "Treasury.PaymentFiles.MarkSent";
    public const string AnularArchivo = "Treasury.PaymentFiles.Void";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var ordenes = TreasuryGroup.Grupo(app, "/payment-orders", "Tesorería Órdenes");
        ordenes.MapGet("/", async (PaymentOrderState? state, PaymentOrderOrigin? origin, int? age, string? search, ISender s, CancellationToken ct) =>
                await s.Send(new ListPaymentOrdersQuery(state, origin, age, search), ct))
            .WithName("Treasury_PaymentOrders_List").RequirePermission(VerOrdenes);
        ordenes.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetPaymentOrderQuery(id), ct))
            .WithName("Treasury_PaymentOrders_Get").RequirePermission(VerOrdenes);
        ordenes.MapGet("/{id:guid}/chain", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetPaymentOrderChainQuery(id), ct))
            .WithName("Treasury_PaymentOrders_Chain").RequirePermission(VerOrdenes);
        ordenes.MapPost("/{id:guid}/revalidate", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RevalidatePaymentOrderCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentOrders_Revalidate").ConClaveDeOperacion().RequirePermission(AdministrarOrdenes);
        // D-a (decisión del dueño del 2026-10-27; FR-229): la orden que llegó sin cuenta destino la resuelve Tesorería, completando la cuenta o girándola por cheque.
        ordenes.MapPost("/{id:guid}/destination", async (Guid id, DestinoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new CompletePaymentOrderDestinationCommand(id, body.PersonBankAccountPublicId ?? Guid.Empty, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_PaymentOrders_CompleteDestination").ConClaveDeOperacion().RequirePermission(ResolverDestino);
        ordenes.MapPost("/{id:guid}/pay-by-check", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new PayPaymentOrderByCheckCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentOrders_PayByCheck").ConClaveDeOperacion().RequirePermission(ResolverDestino);

        var lotes = TreasuryGroup.Grupo(app, "/payment-batches", "Tesorería Lotes");
        lotes.MapGet("/", async (PaymentBatchState? state, Guid? bankAccount, ISender s, CancellationToken ct) =>
                await s.Send(new ListPaymentBatchesQuery(state, bankAccount), ct))
            .WithName("Treasury_PaymentBatches_List").RequirePermission(new AlgunPermiso(PrepararLotes, AprobarLotes, GenerarArchivo, DescargarArchivo));
        lotes.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetPaymentBatchQuery(id), ct))
            .WithName("Treasury_PaymentBatches_Get").RequirePermission(new AlgunPermiso(PrepararLotes, AprobarLotes, GenerarArchivo, DescargarArchivo));
        lotes.MapPost("/", async (CreatePaymentBatchCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                TreasuryGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/treasury/payment-batches/{v.PublicId}"))
            .WithName("Treasury_PaymentBatches_Create").ConClaveDeOperacion().RequirePermission(PrepararLotes);
        lotes.MapPut("/{id:guid}/items", async (Guid id, ItemsRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdatePaymentBatchItemsCommand(id, body.Add ?? [], body.Remove ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentBatches_Items").ConClaveDeOperacion().RequirePermission(PrepararLotes);
        lotes.MapGet("/{id:guid}/preview", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new PreviewPaymentBatchQuery(id), ct))
            .WithName("Treasury_PaymentBatches_Preview").RequirePermission(new AlgunPermiso(PrepararLotes, AprobarLotes));
        lotes.MapPost("/{id:guid}/submit", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SubmitPaymentBatchCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentBatches_Submit").ConClaveDeOperacion().RequirePermission(PrepararLotes);
        lotes.MapPost("/{id:guid}/approve", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApprovePaymentBatchCommand(id, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentBatches_Approve").ConClaveDeOperacion().RequirePermission(AprobarLotes);
        lotes.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RejectPaymentBatchCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentBatches_Reject").ConClaveDeOperacion().RequirePermission(AprobarLotes);
        // US26 (T0166; FR-210): la revisión expresa de una orden marcada, por otra persona que quien preparó el lote.
        lotes.MapPost("/{id:guid}/items/{itemId:guid}/review", async (Guid id, Guid itemId, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReviewPaymentBatchItemCommand(id, itemId, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentBatches_ReviewItem").ConClaveDeOperacion().RequirePermission(AprobarLotes);
        lotes.MapPost("/{id:guid}/file", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new GeneratePaymentFileCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentBatches_File").ConClaveDeOperacion().RequirePermission(GenerarArchivo);
        // FR-213 (nuevo, DT §2.7): el lote de forma «portal del banco» se marca enviado sin archivo.
        lotes.MapPost("/{id:guid}/sent", async (Guid id, EnvioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkPaymentBatchSentByPortalCommand(id, body.UploadedAtBankBy ?? string.Empty, body.AuthorizedAtBankBy ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_PaymentBatches_SentByPortal").ConClaveDeOperacion().RequirePermission(MarcarEnviado);

        // US26 (T0169; FR-153): la vista de las órdenes a cuentas cambiadas; la fase 20 la lleva al centro de reportes con su exportación.
        var vistas = TreasuryGroup.Grupo(app, "/reports", "Tesorería Informes");
        vistas.MapGet("/" + IngenIA365ERP.Application.Treasury.Reports.VistasDeTesoreria.CambiosRecientesDeCuenta, async (DateOnly from, DateOnly to, ISender s, CancellationToken ct) =>
                await s.Send(new IngenIA365ERP.Application.Treasury.Reports.RecentAccountChangesQuery(from, to), ct))
            .WithName("Treasury_Reports_RecentAccountChanges").RequirePermission(VerOrdenes);

        var archivos = TreasuryGroup.Grupo(app, "/payment-files", "Tesorería Archivos de pago");
        archivos.MapGet("/", async (Guid? batch, ISender s, CancellationToken ct) => await s.Send(new ListPaymentFilesQuery(batch), ct))
            .WithName("Treasury_PaymentFiles_List").RequirePermission(new AlgunPermiso(GenerarArchivo, DescargarArchivo, MarcarEnviado, AnularArchivo));
        archivos.MapGet("/{id:guid}/download", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new DownloadPaymentFileCommand(id), ct))
            .WithName("Treasury_PaymentFiles_Download").RequirePermission(DescargarArchivo);
        archivos.MapPost("/{id:guid}/sent", async (Guid id, EnvioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkPaymentFileSentCommand(id, body.UploadedAtBankBy ?? string.Empty, body.AuthorizedAtBankBy ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_PaymentFiles_Sent").ConClaveDeOperacion().RequirePermission(MarcarEnviado);
        archivos.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VoidPaymentFileCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentFiles_Void").ConClaveDeOperacion().RequirePermission(AnularArchivo);
    }

    public sealed record ItemsRequest(IReadOnlyList<Guid>? Add, IReadOnlyList<Guid>? Remove);

    public sealed record MotivoRequest(string? Reason);

    public sealed record DestinoRequest(Guid? PersonBankAccountPublicId, string? Reason);

    public sealed record EnvioRequest(string? UploadedAtBankBy, string? AuthorizedAtBankBy);
}
