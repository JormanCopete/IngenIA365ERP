using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PersonBankAccounts.Imports;
using IngenIA365ERP.Application.Treasury.Imports;
using IngenIA365ERP.Application.Treasury.Activation;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>La activación de Tesorería (feature 015, T0183b; api.md §14): existe desde el bloque 2 con la activación mínima; la fase 19 le suma el cuadre. (nuevo)</summary>
public class TreasuryActivationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/activation", "Tesorería Activación");
        g.MapGet("/requirements", async (bool? minimal, DateOnly? cutoffDate, bool? offersChecks, ISender s, CancellationToken ct) =>
                await s.Send(new GetTreasuryActivationRequirementsQuery(minimal ?? false, cutoffDate, offersChecks ?? false), ct))
            .WithName("Treasury_Activation_Requirements").RequirePermission(new AlgunPermiso("Treasury.Activation.View", "Treasury.Activation.Activate"));
        g.MapPost("/", async (ActivacionRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ActivateTreasuryCommand(body?.CutoffDate, body?.Minimal ?? false, body?.OffersChecks ?? false) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Activation_Activate").ConClaveDeOperacion().RequirePermission("Treasury.Activation.Activate");

        // Feature 015, US13 (fase 18, T0289; api.md §14): el cuadre de apertura, las plantillas de puesta en marcha (y la de cuentas de personas) y las cifras.
        var cuadres = TreasuryGroup.Grupo(app, "/opening-squares", "Tesorería Puesta en marcha");
        cuadres.MapGet("/current", async (DateOnly? cutoffDate, ISender s, CancellationToken ct) => await s.Send(new GetTreasuryOpeningSquareQuery(cutoffDate), ct))
            .WithName("Treasury_OpeningSquares_Current")
            .RequirePermission(new AlgunPermiso("Treasury.Activation.View", "Treasury.Activation.Square", "Treasury.Activation.ProvisionalSquare"));
        cuadres.MapPost("/provisional", async (CuadreProvisionalRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterTreasuryProvisionalOpeningSquareCommand(body.Reason, body.DeadlineOn, body.CutoffDate) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_OpeningSquares_Provisional").ConClaveDeOperacion().RequirePermission("Treasury.Activation.ProvisionalSquare");
        cuadres.MapPost("/definitive", async (CuadreDefinitivoRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterTreasuryDefinitiveOpeningSquareCommand(body?.CutoffDate, body?.MinutesAttachmentPublicId, body?.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_OpeningSquares_Definitive").ConClaveDeOperacion().RequirePermission("Treasury.Activation.Square");

        var cifras = TreasuryGroup.Grupo(app, "/reference-figures", "Tesorería Puesta en marcha");
        cifras.MapGet("/", async (DateOnly? cutoffDate, ISender s, CancellationToken ct) => await s.Send(new ListTreasuryReferenceFiguresQuery(cutoffDate), ct))
            .WithName("Treasury_ReferenceFigures_List").RequirePermission(new AlgunPermiso("Treasury.Imports.Run", "Treasury.Activation.View"));

        var plantillas = TreasuryGroup.Grupo(app, "/imports/{code}", "Tesorería Puesta en marcha");
        plantillas.MapPlantillaPorRuta(
            http => Definicion(http) is { } d
                ? Result.Success(d)
                : Result.Failure<DefinicionDePlantilla>(new Error("Treasury.Imports.TemplateNotFound", "No hay una plantilla de puesta en marcha de Tesorería con ese código.")),
            http => $"plantilla-{http.Request.RouteValues["code"]}",
            "Treasury.Imports.Run", "Treasury.Imports.Run", "Treasury_Imports", RutasDePlantilla.PermisoDeDatosPersonales, "Treasury.Imports.TemplateExported",
            importar: (http, modo, archivo, motivo, clave) => Codigo(http) switch
            {
                PlantillaDeCuentasBancariasDePersonas.Clave => new ImportPersonBankAccountsCommand(modo, archivo, motivo) { OperationKey = clave },
                // Fase 46 (T0472): los pendientes de la Tesorería anterior, antes de su retiro.
                PlantillaDePendientesHeredados.Clave => new ImportLegacyTreasuryPendingCommand(modo, archivo, motivo) { OperationKey = clave },
                _ => new ImportTreasuryGoLiveTemplateCommand(Codigo(http), modo, archivo, motivo) { OperationKey = clave },
            });
    }

    private static string Codigo(HttpContext http) => http.Request.RouteValues["code"]?.ToString() ?? string.Empty;

    private static DefinicionDePlantilla? Definicion(HttpContext http) => Codigo(http) switch
    {
        PlantillaDeCuentasBancariasDePersonas.Clave => PlantillaDeCuentasBancariasDePersonas.Definicion,
        PlantillaDePendientesHeredados.Clave => PlantillaDePendientesHeredados.Definicion,
        var codigo => PlantillasDePuestaEnMarchaDeTesoreria.Por(codigo),
    };

    /// <summary>Fase 19 (T0304): la activación completa por defecto; <c>minimal</c> sólo fuera de producción; <c>offersChecks</c> si la cooperativa gira cheques.</summary>
    public sealed record ActivacionRequest(DateOnly? CutoffDate, bool? Minimal = null, bool? OffersChecks = null);

    public sealed record CuadreProvisionalRequest(string Reason, DateOnly DeadlineOn, DateOnly? CutoffDate);

    public sealed record CuadreDefinitivoRequest(DateOnly? CutoffDate, Guid? MinutesAttachmentPublicId, string? Reason);
}
