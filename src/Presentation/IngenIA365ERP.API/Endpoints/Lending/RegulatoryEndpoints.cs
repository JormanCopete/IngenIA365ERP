using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los archivos regulatorios de Cartera (feature 013, K5, US16, T1008; contracts/api.md §18; FR-136, FR-137, SC-012, SC-019, D28): el formato de
/// cartera del SICSES y los archivos de las centrales de riesgo. Leer, listar y descargar exigen <c>Lending.RegulatoryFiles.Read</c> (traen deudores:
/// nunca un <c>View</c>, DT §2.25); validar y generar el SICSES, <c>Lending.RegulatoryReports.Generate</c>; los de una central,
/// <c>Lending.CreditBureauFiles.Generate</c>; marcar cargado, <c>Lending.RegulatoryFiles.MarkUploaded</c>. Validar es una consulta por POST (sin
/// clave: no guarda); generar y marcar cargado llevan <c>Idempotency-Key</c>. Una generación con bloqueantes deja la versión <c>Validated</c> sin
/// archivo y responde 422 <c>Lending.Regulatory.BlockingIssues</c> con <c>data { regulatoryFilePublicId, version, issues[] }</c>. (nuevo)
/// </summary>
public class RegulatoryEndpoints : ICarterModule
{
    public const string Leer = "Lending.RegulatoryFiles.Read";
    public const string GenerarSicses = "Lending.RegulatoryReports.Generate";
    public const string GenerarCentral = "Lending.CreditBureauFiles.Generate";
    public const string MarcarCargado = "Lending.RegulatoryFiles.MarkUploaded";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var regulatorios = LendingGroup.Grupo(app, "/regulatory", "Cartera Regulatorios");

        regulatorios.MapGet("/layouts", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRegulatoryLayoutsQuery(), ct))
            .WithName("Lending_Regulatory_Layouts")
            .RequirePermission(Leer);

        regulatorios.MapPost("/sicses/{year:int}/{month:int}/validate", async (int year, int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new ValidateSicsesFileQuery(year, month), ct))
            .WithName("Lending_Regulatory_Sicses_Validate")
            .RequirePermission(GenerarSicses);

        regulatorios.MapPost("/sicses/{year:int}/{month:int}/generate", async (int year, int month, GenerarRequest? body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                Generado(await sender.Send(new GenerateSicsesFileCommand(year, month, body?.AcknowledgeWarnings ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct)))
            .WithName("Lending_Regulatory_Sicses_Generate")
            .ConClaveDeOperacion()
            .RequirePermission(GenerarSicses);

        regulatorios.MapPost("/bureaus/{bureauCode}/{year:int}/{month:int}/validate", async (string bureauCode, int year, int month, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ValidateCreditBureauFileQuery(bureauCode, year, month), ct))
            .WithName("Lending_Regulatory_Bureau_Validate")
            .RequirePermission(GenerarCentral);

        regulatorios.MapPost("/bureaus/{bureauCode}/{year:int}/{month:int}/generate", async (string bureauCode, int year, int month, GenerarRequest? body,
                HttpContext http, ISender sender, CancellationToken ct) =>
                Generado(await sender.Send(new GenerateCreditBureauFileCommand(bureauCode, year, month, body?.AcknowledgeWarnings ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct)))
            .WithName("Lending_Regulatory_Bureau_Generate")
            .ConClaveDeOperacion()
            .RequirePermission(GenerarCentral);

        regulatorios.MapGet("/files", async (RegulatoryFileKind? kind, string? bureau, string? format, int? year, int? month, RegulatoryFileStatus? status, int? page,
                int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRegulatoryFilesQuery(kind, bureau, year, month, status, page ?? 1, pageSize ?? 20, format), ct))
            .WithName("Lending_Regulatory_Files_List")
            .RequirePermission(Leer);

        regulatorios.MapGet("/files/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRegulatoryFileQuery(id), ct))
            .WithName("Lending_Regulatory_Files_Get")
            .RequirePermission(Leer);

        regulatorios.MapPost("/files/{id:guid}/download-link", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IssueRegulatoryFileDownloadLinkCommand(id), ct))
            .WithName("Lending_Regulatory_Files_DownloadLink")
            .RequireRateLimiting(LimiteDeAdjuntos.Politica)
            .RequirePermission(Leer);

        regulatorios.MapPost("/files/{id:guid}/mark-uploaded", async (Guid id, MarcarCargadoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new MarkRegulatoryFileUploadedCommand(id, body.FilingNumber ?? string.Empty, body.UploadedAt ?? default,
                    body.FilingDate, body.ReceiptAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Regulatory_Files_MarkUploaded")
            .ConClaveDeOperacion()
            .RequirePermission(MarcarCargado);
    }

    /// <summary>
    /// 201 con la generación <c>Generated</c>; una <c>Validated</c> (con bloqueantes, que el comando deja guardada y responde con éxito para no revertir
    /// la fila ni la clave de la operación) se traduce a 422 <c>Lending.Regulatory.BlockingIssues</c> (api.md §18).
    /// </summary>
    internal static object Generado(Result<RegulatoryFileDetailDto> resultado)
    {
        if (resultado.IsFailure) return resultado;
        var generacion = resultado.Value;
        if (generacion.File.Status == RegulatoryFileStatus.Validated)
            return Result.Failure<RegulatoryFileDetailDto>(ErroresDeArchivosRegulatorios.Bloqueantes(generacion));
        return Results.Created($"{LendingGroup.Prefijo}/regulatory/files/{generacion.File.PublicId}", generacion);
    }

    /// <summary>Generar (§18): reconocer las advertencias (<c>LayoutSinCotejar</c>, <c>ModeloObligatorioNoDisponible</c>…).</summary>
    public sealed record GenerarRequest(bool AcknowledgeWarnings);

    /// <summary>Marcar cargado (§18): el radicado del portal, cuándo se cargó, la fecha del radicado y el comprobante adjunto.</summary>
    public sealed record MarcarCargadoRequest(string? FilingNumber, DateTime? UploadedAt, DateOnly? FilingDate, Guid? ReceiptAttachmentPublicId);
}
