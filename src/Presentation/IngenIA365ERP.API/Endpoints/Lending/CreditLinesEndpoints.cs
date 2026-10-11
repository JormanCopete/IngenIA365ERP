using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las líneas de crédito y lo que las rodea (feature 013, US1, T0358; contracts/api.md §3.1–§3.3): <c>/credit-lines</c> (lista, detalle,
/// versión, alta, versiones, cerrar, reabrir, borrar, la consulta de usura y la plantilla <c>lending.credit-lines</c> por
/// <c>RutasDePlantilla.MapPlantilla</c>), <c>/credit-lines/{id}/scoring</c>, <c>/modalities</c> y <c>/commercial-term-equivalences</c>.
/// Leer exige <c>Lending.CreditLines.View</c> (el puntaje, <c>Lending.Scoring.View</c>); escribir, <c>.Manage</c> (el puntaje,
/// <c>Lending.Scoring.Manage</c>) con <c>Idempotency-Key</c>. <c>usury-check</c> es una consulta por POST y va sin clave (§1.5). Las líneas
/// son el eje del alcance por línea: fuera de él, el 404 de la línea. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CreditLinesEndpoints : ICarterModule
{
    public const string Ver = "Lending.CreditLines.View";
    public const string Administrar = "Lending.CreditLines.Manage";
    public const string VerPuntaje = "Lending.Scoring.View";
    public const string AdministrarPuntaje = "Lending.Scoring.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ------------------------------------------------------------------------------------------------ líneas §3.1 --
        var lineas = LendingGroup.Grupo(app, "/credit-lines", "Cartera Líneas de crédito");

        lineas.MapGet("/", async (CreditLineStatus? status, string? modality, bool? commercialOrigin, bool? includeClosed, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCreditLinesQuery(status, modality, commercialOrigin, includeClosed), ct))
            .WithName("Lending_CreditLines_List")
            .RequirePermission(Ver);

        lineas.MapGet("/{id:guid}", async (Guid id, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCreditLineQuery(id, date), ct))
            .WithName("Lending_CreditLines_Get")
            .RequirePermission(Ver);

        lineas.MapGet("/{id:guid}/versions/{versionId:guid}", async (Guid id, Guid versionId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCreditLineVersionQuery(id, versionId), ct))
            .WithName("Lending_CreditLines_GetVersion")
            .RequirePermission(Ver);

        lineas.MapPost("/", async (CrearLineaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateCreditLineCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Purpose,
                    body.FirstVersion ?? new CreditLineVersionInput())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), l => $"/api/lending/credit-lines/{l.PublicId}"))
            .WithName("Lending_CreditLines_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        lineas.MapPost("/{id:guid}/versions", async (Guid id, CreditLineVersionInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddCreditLineVersionCommand(id, body)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), v => $"/api/lending/credit-lines/{id}/versions/{v.PublicId}"))
            .WithName("Lending_CreditLines_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        lineas.MapPost("/{id:guid}/close", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseCreditLineCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditLines_Close")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        lineas.MapPost("/{id:guid}/reopen", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReopenCreditLineCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditLines_Reopen")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        lineas.MapDelete("/{id:guid}", async (Guid id, string? reason, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteCreditLineCommand(id, reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditLines_Delete")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        // La consulta de usura de la versión vigente: no escribe, sin clave (§1.5).
        lineas.MapPost("/{id:guid}/usury-check", async (Guid id, ConsultaDeUsuraRequest? body, ISender sender, CancellationToken ct) =>
                await sender.Send(new CheckCreditLineUsuryQuery(id, body?.Date, body?.Amount, body?.TermInstallments, body?.Periodicity), ct))
            .WithName("Lending_CreditLines_UsuryCheck")
            .RequirePermission(Ver);

        // La plantilla 1 (plantillas.md §1): GET /template.xlsx y POST /import?mode=review|apply, con Idempotency-Key.
        lineas.MapPlantilla(
            () => CatalogoDePlantillasDeCartera.LineasDeCredito,
            "plantilla-cartera-lineas-de-credito",
            Ver,
            Administrar,
            "Lending_CreditLines",
            "Lending.Reports.ExportPersonalData",
            AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) =>
                new ImportCreditLinesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });

        // ----------------------------------------------------------------------------------------------- puntaje §3.3 --
        lineas.MapGet("/{id:guid}/scoring", async (Guid id, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCreditLineScoringQuery(id, date), ct))
            .WithName("Lending_CreditLines_GetScoring")
            .RequirePermission(VerPuntaje);

        lineas.MapPost("/{id:guid}/scoring", async (Guid id, PuntajeRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SetCreditLineScoringCommand(id, body.ValidFrom, body.Reason ?? string.Empty, body.MinimumScore,
                    body.ExceptionLevelCode, body.Variables ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), v => $"/api/lending/credit-lines/{id}/versions/{v.PublicId}"))
            .WithName("Lending_CreditLines_SetScoring")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarPuntaje);

        // ------------------------------------------------------------------------------------------- modalidades §3.2 --
        var modalidades = LendingGroup.Grupo(app, "/modalities", "Cartera Líneas de crédito");

        modalidades.MapGet("/", async (DateOnly? date, ISender sender, CancellationToken ct) => await sender.Send(new ListModalitiesQuery(date), ct))
            .WithName("Lending_Modalities_List")
            .RequirePermission(Ver);

        // ------------------------------------------------------------------------- equivalencias de Comercial §3.2 --
        var equivalencias = LendingGroup.Grupo(app, "/commercial-term-equivalences", "Cartera Líneas de crédito");

        equivalencias.MapGet("/", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCommercialTermEquivalencesQuery(date), ct))
            .WithName("Lending_CommercialTermEquivalences_List")
            .RequirePermission(Ver);

        equivalencias.MapPut("/", async (EquivalenciasRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetCommercialTermEquivalencesCommand(body.Items ?? [], body.ValidFrom, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_CommercialTermEquivalences_Set")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo del alta (§3.1): código, nombre, destino y la primera versión.</summary>
    public sealed record CrearLineaRequest(string? Code, string? Name, string? Purpose, CreditLineVersionInput? FirstVersion);

    /// <summary>El motivo de cerrar o reabrir.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>La consulta de usura (§3.1): todo opcional; por defecto hoy, el monto y el plazo máximos y la periodicidad de defecto.</summary>
    public sealed record ConsultaDeUsuraRequest(DateOnly? Date, decimal? Amount, int? TermInstallments, RepaymentPeriodicity? Periodicity);

    /// <summary>El bloque de puntaje (§3.3).</summary>
    public sealed record PuntajeRequest(DateOnly ValidFrom, string? Reason, decimal? MinimumScore, string? ExceptionLevelCode,
        IReadOnlyList<ScoringVariableDto>? Variables);

    /// <summary>Las equivalencias de Comercial (§3.2).</summary>
    public sealed record EquivalenciasRequest(IReadOnlyList<CommercialTermEquivalenceInput>? Items, DateOnly ValidFrom, string? Reason);
}
