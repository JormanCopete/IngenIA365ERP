using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Accounting.Taxes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Impuestos (feature 009 E4, US10): los formularios tributarios con sus renglones y los certificados de retención (fuente, IVA, ICA).
/// La regla de impuesto de cada cuenta (clase, concepto, base y tarifas) se edita en el plan de cuentas, desde E1. Los informes
/// (<c>withholdings</c>, <c>vat</c>, <c>ica</c>, <c>gmf</c>, <c>tax-form/{code}</c>) están en <c>/api/reports/accounting</c>.
/// <c>Accounting.Taxes.View</c> para leer, <c>.Manage</c> para los formularios, <c>.Certificates</c> para generar, reexpedir y enviar.
/// </summary>
public class TaxesEndpoints : ICarterModule
{
    /// <summary>Cuerpo de <c>PUT /forms/{code}</c>.</summary>
    public sealed record GuardarFormularioRequest(string Name, bool IsActive, IReadOnlyList<TaxFormLineInput> Lines);

    /// <summary>Cuerpo de <c>POST /certificates/generate</c>: clase (Withholding, VatWithholding, IcaWithholding), año, meses opcionales y un tercero opcional.</summary>
    public sealed record GenerarCertificadosRequest(string Kind, int Year, byte? PeriodFrom, byte? PeriodTo, Guid? PersonPublicId);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/taxes")
            .WithTags("AccountingTaxes")
            .RequireAuthorization();

        group.MapGet("/forms", async (ISender sender, CancellationToken ct) => await sender.Send(new ListTaxFormsQuery(), ct))
            .WithName("Accounting_Taxes_Forms")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.View");

        group.MapPut("/forms/{code}", async (string code, GuardarFormularioRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveTaxFormCommand(code, body.Name ?? string.Empty, body.IsActive, body.Lines ?? []), ct))
            .WithName("Accounting_Taxes_SaveForm")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.Manage");

        group.MapPost("/forms/load-suggested", async (ISender sender, CancellationToken ct) => await sender.Send(new LoadSuggestedTaxFormsCommand(), ct))
            .WithName("Accounting_Taxes_LoadSuggestedForms")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.Manage");

        // El plano de formularios (2026-10-31): se baja lleno con lo configurado, se edita y se sube; cada formulario del archivo se reemplaza.
        group.MapGet("/forms/plano.xlsx", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var tabla = await sender.Send(new ExportTaxFormsQuery(), ct);
                if (tabla.IsFailure) return ErrorEnvelopeFilter.Translate(http, tabla);
                var archivo = PlantillaDeImportacion.Xlsx(tabla.Value, "formularios-tributarios");
                return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
            })
            .WithName("Accounting_Taxes_FormsFile")
            .RequirePermission("Accounting.Taxes.View");

        group.MapPost("/forms/import", async ([FromForm] IFormFile archivo, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var (contenido, rechazo) = await LeerArchivoAsync(archivo, http, ct);
                return rechazo ?? await sender.Send(new ImportTaxFormsCommand(archivo.FileName, contenido!), ct);
            })
            .WithName("Accounting_Taxes_ImportForms")
            .DisableAntiforgery()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.Manage");

        group.MapGet("/certificates", async ([FromQuery] string? kind, [FromQuery] int? year, [FromQuery] Guid? person, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCertificatesQuery(kind, year, person), ct))
            .WithName("Accounting_Taxes_Certificates")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.View");

        group.MapGet("/certificates/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetCertificateQuery(id), ct))
            .WithName("Accounting_Taxes_Certificate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.View");

        group.MapGet("/certificates/{id:guid}/pdf", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetCertificatePdfQuery(id), ct);
                return result.IsSuccess ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName) : (object)result;
            })
            .WithName("Accounting_Taxes_CertificatePdf")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.View");

        group.MapPost("/certificates/generate", async (GenerarCertificadosRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new GenerateWithholdingCertificatesCommand(body.Kind ?? string.Empty, body.Year, body.PeriodFrom, body.PeriodTo, body.PersonPublicId), ct))
            .WithName("Accounting_Taxes_GenerateCertificates")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.Certificates");

        group.MapPost("/certificates/{id:guid}/reissue", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ReissueCertificateCommand(id), ct))
            .WithName("Accounting_Taxes_ReissueCertificate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.Certificates");

        group.MapPost("/certificates/{id:guid}/send", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new SendCertificateCommand(id), ct))
            .WithName("Accounting_Taxes_SendCertificate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Taxes.Certificates");
    }

    /// <summary>El archivo de un plano subido por formulario (mismo molde que la carga de auxiliares): vacío es 400, más de 10 MB es 413.</summary>
    private static async Task<(byte[]? Contenido, object? Rechazo)> LeerArchivoAsync(IFormFile? archivo, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return (null, Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest));
        if (archivo.Length > OpeningEndpoints.MaxBytes)
            return (null, Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status413PayloadTooLarge));
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return (ms.ToArray(), null);
    }
}
