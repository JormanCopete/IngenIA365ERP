using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements.Files;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los archivos de desembolsos (feature 013, US3, T0457; contracts/api.md §9.3, FR-047, T78, C10): todo con
/// <c>Lending.DisbursementFiles.Generate</c>. La lista, las cuentas de origen del plan (por el puerto de Contabilidad), la vista previa
/// (<c>POST /preview</c>, sin clave: no escribe), generar (con <c>Idempotency-Key</c>; el adjunto <c>LendingDisbursementFile</c> se sube antes de
/// guardar la fila), el enlace firmado de 60 s (sin clave: repetirlo emite otro; auditado) y anular antes del primer resultado de Tesorería
/// (con clave; 204). Ningún archivo emite mensajes: la orden ya viajó al desembolsar. (nuevo)
/// </summary>
public class DisbursementFilesEndpoints : ICarterModule
{
    public const string Generar = "Lending.DisbursementFiles.Generate";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var archivos = LendingGroup.Grupo(app, "/disbursement-files", "Cartera Archivos de desembolsos");

        archivos.MapGet("/", async (string? status, DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDisbursementFilesQuery(status, from, to, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_DisbursementFiles_List")
            .RequirePermission(Generar);

        archivos.MapGet("/source-accounts", async (Guid? formatPublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDisbursementSourceAccountsQuery(formatPublicId), ct))
            .WithName("Lending_DisbursementFiles_SourceAccounts")
            .RequirePermission(Generar);

        archivos.MapPost("/preview", async (ArchivoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewDisbursementFileQuery(body.FormatPublicId, body.SourceAccountPublicId, body.OrderPublicIds, body.PaymentDate), ct))
            .WithName("Lending_DisbursementFiles_Preview")
            .RequirePermission(Generar);

        archivos.MapPost("/", async (ArchivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new GenerateLoanDisbursementFileCommand(body.FormatPublicId, body.SourceAccountPublicId ?? Guid.Empty,
                    body.OrderPublicIds, body.PaymentDate, body.Reason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), a => $"/api/lending/disbursement-files/{a.File.PublicId}"))
            .WithName("Lending_DisbursementFiles_Generate")
            .ConClaveDeOperacion()
            .RequirePermission(Generar);

        archivos.MapPost("/{id:guid}/download-link", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IssueDisbursementFileDownloadLinkCommand(id), ct))
            .WithName("Lending_DisbursementFiles_DownloadLink")
            .RequireRateLimiting(LimiteDeAdjuntos.Politica)
            .RequirePermission(Generar);

        archivos.MapPost("/{id:guid}/void", async (Guid id, AnulacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new VoidLoanDisbursementFileCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Lending_DisbursementFiles_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Generar);
    }

    /// <summary>
    /// El archivo (§9.3): el formato (ámbito <c>LoanDisbursement</c>), la cuenta de origen del plan, las órdenes (nulas en la vista previa = todas
    /// las elegibles del alcance), la fecha de pago (nula = hoy) y el motivo.
    /// </summary>
    public sealed record ArchivoRequest(
        Guid FormatPublicId,
        Guid? SourceAccountPublicId,
        IReadOnlyList<Guid>? OrderPublicIds,
        DateOnly? PaymentDate,
        string? Reason);

    /// <summary>El motivo de la anulación.</summary>
    public sealed record AnulacionRequest(string? Reason);
}
