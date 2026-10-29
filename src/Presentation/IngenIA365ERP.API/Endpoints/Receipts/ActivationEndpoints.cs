using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Receipts.Imports;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Reversals;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>La activación de Recaudo (feature 015, T0183b; api.md §14): existe desde el bloque 2 con la activación mínima; la fase 19 le suma el cuadre. (nuevo)</summary>
public class ReceiptsActivationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/activation", "Recaudo Activación");
        g.MapGet("/requirements", async (bool? minimal, DateOnly? cutoffDate, ISender s, CancellationToken ct) =>
                await s.Send(new Application.Receipts.Activation.GetReceiptsActivationRequirementsQuery(minimal ?? false, cutoffDate), ct))
            .WithName("Receipts_Activation_Requirements").RequirePermission(new AlgunPermiso("Receipts.Activation.View", "Receipts.Activation.Activate"));
        g.MapPost("/", async (ActivacionRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new Application.Receipts.Activation.ActivateReceiptsCommand(body?.CutoffDate, body?.Minimal ?? false) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Activation_Activate").ConClaveDeOperacion().RequirePermission("Receipts.Activation.Activate");
        // Feature 015, R1 (T0352; contratos-v2.md §1, §3.1): Recaudo adopta la v2 de los contratos de Cartera y Captaciones (la partida bancaria citada).
        g.MapPost("/contract-versions", async (VersionesRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AdoptContractVersionCommand(body?.Obligations ?? 2, body?.Accounts ?? 2) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Activation_AdoptContractVersions").ConClaveDeOperacion().RequirePermission("Receipts.Activation.Activate");

        // Feature 015, US13 (fase 18, T0289; api.md §14): el cuadre de apertura, las plantillas de puesta en marcha y las cifras de referencia.
        var cuadres = ReceiptsGroup.Grupo(app, "/opening-squares", "Recaudo Puesta en marcha");
        cuadres.MapGet("/current", async (DateOnly? cutoffDate, ISender s, CancellationToken ct) => await s.Send(new GetReceiptsOpeningSquareQuery(cutoffDate), ct))
            .WithName("Receipts_OpeningSquares_Current")
            .RequirePermission(new AlgunPermiso("Receipts.Activation.View", "Receipts.Activation.Square", "Receipts.Activation.ProvisionalSquare"));
        cuadres.MapPost("/provisional", async (CuadreProvisionalRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterProvisionalOpeningSquareCommand(body.Reason, body.DeadlineOn, body.CutoffDate) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_OpeningSquares_Provisional").ConClaveDeOperacion().RequirePermission("Receipts.Activation.ProvisionalSquare");
        cuadres.MapPost("/definitive", async (CuadreDefinitivoRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterDefinitiveOpeningSquareCommand(body?.CutoffDate, body?.MinutesAttachmentPublicId, body?.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_OpeningSquares_Definitive").ConClaveDeOperacion().RequirePermission("Receipts.Activation.Square");

        var cifras = ReceiptsGroup.Grupo(app, "/reference-figures", "Recaudo Puesta en marcha");
        cifras.MapGet("/", async (DateOnly? cutoffDate, ISender s, CancellationToken ct) => await s.Send(new ListReceiptsReferenceFiguresQuery(cutoffDate), ct))
            .WithName("Receipts_ReferenceFigures_List").RequirePermission(new AlgunPermiso("Receipts.Imports.Run", "Receipts.Activation.View"));

        // Las plantillas por código: GET /api/receipts/imports/{code}/template.xlsx y POST /api/receipts/imports/{code}/import?mode=review|apply.
        var plantillas = ReceiptsGroup.Grupo(app, "/imports/{code}", "Recaudo Puesta en marcha");
        plantillas.MapPlantillaPorRuta(
            http => PlantillasDePuestaEnMarchaDeRecaudo.Por(http.Request.RouteValues["code"]?.ToString() ?? string.Empty) is { } d
                ? Result.Success(d)
                : Result.Failure<DefinicionDePlantilla>(new Error("Receipts.Imports.TemplateNotFound", "No hay una plantilla de puesta en marcha de Recaudo con ese código.")),
            http => $"plantilla-{http.Request.RouteValues["code"]}",
            "Receipts.Imports.Run", "Receipts.Imports.Run", "Receipts_Imports", RutasDePlantilla.PermisoDeDatosPersonales, "Receipts.Imports.TemplateExported",
            importar: (http, modo, archivo, motivo, clave) =>
                new ImportReceiptsGoLiveTemplateCommand(http.Request.RouteValues["code"]?.ToString() ?? string.Empty, modo, archivo, motivo) { OperationKey = clave });
    }

    /// <summary>Fase 19 (T0304): la activación completa por defecto; <c>minimal</c> sólo fuera de producción (el paralelo de QA).</summary>
    public sealed record ActivacionRequest(DateOnly? CutoffDate, bool? Minimal = null);

    /// <summary>R1 (T0352): las versiones a adoptar; por defecto, la 2 de los dos contratos.</summary>
    public sealed record VersionesRequest(int? Obligations, int? Accounts);

    public sealed record CuadreProvisionalRequest(string Reason, DateOnly DeadlineOn, DateOnly? CutoffDate);

    public sealed record CuadreDefinitivoRequest(DateOnly? CutoffDate, Guid? MinutesAttachmentPublicId, string? Reason);
}
