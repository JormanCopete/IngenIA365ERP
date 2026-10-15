using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los archivos de descuentos de las pagadurías (feature 013, K2, US10, T0850; contracts/api.md §13.2; FR-113, T78, I9): la lista y el detalle con las
/// líneas del alcance (<c>Lending.PayrollDeductionFiles.Read</c>), la vista previa (<c>POST /preview</c>, sin clave: no escribe), generar o regenerar
/// antes del envío (con <c>Idempotency-Key</c>; sube el adjunto antes de guardar la fila y emite <c>DescuentosPorPagaduria</c> a Recaudo), el enlace
/// firmado de 60 s (sin clave: repetirlo emite otro; auditado) y anular desde <c>Generated</c> (con clave; 204), bajo
/// <c>Lending.PayrollDeductionFiles.Generate</c>. <c>Sent</c> lo marca el mensaje de Recaudo, no una ruta. (nuevo)
/// </summary>
public class PayrollDeductionFilesEndpoints : ICarterModule
{
    public const string Leer = "Lending.PayrollDeductionFiles.Read";
    public const string Generar = "Lending.PayrollDeductionFiles.Generate";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var archivos = LendingGroup.Grupo(app, "/payroll-deduction-files", "Cartera Libranza");

        archivos.MapGet("/", async (Guid? agreementPublicId, int? year, int? month, PayrollFileStatus? status, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListPayrollDeductionFilesQuery(agreementPublicId, year, month, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_PayrollDeductionFiles_List")
            .RequirePermission(Leer);

        archivos.MapGet("/{id:guid}", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPayrollDeductionFileQuery(id, page ?? 1, pageSize ?? 100), ct))
            .WithName("Lending_PayrollDeductionFiles_Get")
            .RequirePermission(Leer);

        archivos.MapPost("/preview", async (ArchivoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewPayrollDeductionFileQuery(body.AgreementPublicId, body.Year, body.Month, body.SubPeriod), ct))
            .WithName("Lending_PayrollDeductionFiles_Preview")
            .RequirePermission(Generar);

        archivos.MapPost("/", async (ArchivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new GeneratePayrollDeductionFileCommand(body.AgreementPublicId, body.Year, body.Month, body.SubPeriod,
                    body.Reason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), a => $"/api/lending/payroll-deduction-files/{a.PublicId}"))
            .WithName("Lending_PayrollDeductionFiles_Generate")
            .ConClaveDeOperacion()
            .RequirePermission(Generar);

        archivos.MapPost("/{id:guid}/download-link", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IssuePayrollDeductionFileDownloadLinkCommand(id), ct))
            .WithName("Lending_PayrollDeductionFiles_DownloadLink")
            .RequireRateLimiting(LimiteDeAdjuntos.Politica)
            .RequirePermission(Leer);

        archivos.MapPost("/{id:guid}/void", async (Guid id, AnulacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new VoidPayrollDeductionFileCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Lending_PayrollDeductionFiles_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Generar);
    }

    /// <summary>El archivo (§13.2): el convenio, el período (año, mes y, en quincenal, sub-período) y el motivo de regenerar.</summary>
    public sealed record ArchivoRequest(Guid AgreementPublicId, int Year, int Month, int? SubPeriod, string? Reason);

    /// <summary>El motivo de la anulación.</summary>
    public sealed record AnulacionRequest(string? Reason);
}
