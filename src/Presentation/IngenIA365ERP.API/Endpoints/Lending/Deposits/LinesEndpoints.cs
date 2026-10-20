using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// El catálogo común de líneas de la Cartera y las versiones de captación y de aportes (feature 014, US1, T069; contracts/api.md §3 de la 014), sobre
/// <see cref="LendingGroup"/>: <c>/api/lending/lines</c> (lista con filtro de naturaleza, detalle con la versión a una fecha, alta de captación y de
/// aportes, versiones de cada naturaleza, cerrar y reabrir). Leer exige el <c>View</c> de alguna naturaleza (el comando filtra por las que tiene);
/// escribir, el <c>Manage</c> de la suya con <c>Idempotency-Key</c>. <c>/api/lending/credit-lines</c> sigue para colocación. Las reglas de tasa
/// negociada (K9), los cargos (K8), la cotización y las plantillas llegan con sus historias. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class LinesEndpoints : ICarterModule
{
    public const string VerCredito = PermisosDeLineas.VerCredito;
    public const string VerCaptacion = PermisosDeLineas.VerCaptacion;
    public const string AdministrarCaptacion = PermisosDeLineas.AdministrarCaptacion;
    public const string VerAportes = PermisosDeLineas.VerAportes;
    public const string AdministrarAportes = PermisosDeLineas.AdministrarAportes;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var lineas = LendingGroup.Grupo(app, "/lines", "Cartera Líneas (catálogo común)");

        lineas.MapGet("/", async (LineNature? nature, CreditLineStatus? status, DepositClass? depositClass, bool? includeClosed, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ListLendingLinesQuery(nature, status, depositClass, includeClosed), ct))
            .WithName("Lending_Lines_List")
            .RequirePermission(new AlgunPermiso(VerCredito, VerCaptacion, VerAportes));

        lineas.MapGet("/{id:guid}", async (Guid id, DateOnly? date, DateTime? recordedAsOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingLineQuery(id, date, recordedAsOf), ct))
            .WithName("Lending_Lines_Get")
            .RequirePermission(new AlgunPermiso(VerCredito, VerCaptacion, VerAportes));

        // Feature 014 (E251, respuesta 67 B): las plantillas de importación de líneas de captación y de aportes (GET /template.xlsx y POST /import).
        lineas.MapGroup("/deposit").MapPlantilla(
            () => IngenIA365ERP.Application.Lending.Deposits.Lines.PlantillasDeLineasDeCaptacion.Captacion,
            "plantilla-cartera-lineas-de-captacion", VerCaptacion, AdministrarCaptacion, "Lending_Lines_Deposit",
            "Lending.Reports.ExportPersonalData", IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) => new ImportDepositLinesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
        lineas.MapGroup("/contribution").MapPlantilla(
            () => IngenIA365ERP.Application.Lending.Deposits.Lines.PlantillasDeLineasDeCaptacion.Aportes,
            "plantilla-cartera-linea-de-aportes", VerAportes, AdministrarAportes, "Lending_Lines_Contribution",
            "Lending.Reports.ExportPersonalData", IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) => new ImportContributionLinesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });

        // ------------------------------------------------------------------------------------------------------- captación --
        lineas.MapPost("/deposit", async (CrearLineaDeCaptacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateDepositLineCommand(body.Code ?? string.Empty, body.Name ?? string.Empty,
                    body.DepositClass, body.Active, body.FirstVersion ?? new DepositLineVersionInput())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), l => $"/api/lending/lines/{l.PublicId}"))
            .WithName("Lending_Lines_CreateDeposit")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarCaptacion);

        // US4 (T123): el catálogo de cargos de la versión vigente a una fecha.
        lineas.MapGet("/{id:guid}/charges", async (Guid id, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new IngenIA365ERP.Application.Lending.Deposits.Charges.ListDepositLineChargesQuery(id, date), ct))
            .WithName("Lending_Lines_Charges")
            .RequirePermission(VerCaptacion);

        lineas.MapGet("/{id:guid}/deposit-versions", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingLineVersionsQuery(id, LineNature.Deposit), ct))
            .WithName("Lending_Lines_ListDepositVersions")
            .RequirePermission(VerCaptacion);

        lineas.MapGet("/{id:guid}/deposit-versions/{versionId:guid}", async (Guid id, Guid versionId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDepositLineVersionQuery(id, versionId), ct))
            .WithName("Lending_Lines_GetDepositVersion")
            .RequirePermission(VerCaptacion);

        lineas.MapPost("/{id:guid}/deposit-versions", async (Guid id, DepositLineVersionInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddDepositLineVersionCommand(id, body)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), v => $"/api/lending/lines/{id}/deposit-versions/{v.PublicId}"))
            .WithName("Lending_Lines_AddDepositVersion")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarCaptacion);

        // ------------------------------------------------------------------------- reglas de tasa negociada (US6, T178; api.md §3) --
        lineas.MapGet("/{id:guid}/negotiated-rate-rules", async (Guid id, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates.ListNegotiatedRateRulesQuery(id, date), ct))
            .WithName("Lending_Lines_NegotiatedRateRules")
            .RequirePermission(VerCaptacion);

        lineas.MapPost("/{id:guid}/negotiated-rate-rules", async (Guid id,
                    IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates.NegotiatedRateRuleInput body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates.AddNegotiatedRateRuleCommand(id, body)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), r => $"/api/lending/lines/{id}/negotiated-rate-rules/{r.PublicId}"))
            .WithName("Lending_Lines_AddNegotiatedRateRule")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarCaptacion);

        lineas.MapPost("/{id:guid}/negotiated-rate-rules/{ruleId:guid}/end", async (Guid id, Guid ruleId, TerminarReglaRequest body, HttpContext http,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates.EndNegotiatedRateRuleCommand(id, ruleId, body.ValidTo,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Lines_EndNegotiatedRateRule")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarCaptacion);

        // --------------------------------------------------------------------------------------------------------- aportes --
        lineas.MapPost("/contribution", async (CrearLineaDeAportesRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateContributionLineCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Active,
                    body.FirstVersion ?? new ContributionLineVersionInput())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), l => $"/api/lending/lines/{l.PublicId}"))
            .WithName("Lending_Lines_CreateContribution")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarAportes);

        lineas.MapGet("/{id:guid}/contribution-versions", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingLineVersionsQuery(id, LineNature.Contribution), ct))
            .WithName("Lending_Lines_ListContributionVersions")
            .RequirePermission(VerAportes);

        lineas.MapGet("/{id:guid}/contribution-versions/{versionId:guid}", async (Guid id, Guid versionId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetContributionLineVersionQuery(id, versionId), ct))
            .WithName("Lending_Lines_GetContributionVersion")
            .RequirePermission(VerAportes);

        lineas.MapPost("/{id:guid}/contribution-versions", async (Guid id, ContributionLineVersionInput body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddContributionLineVersionCommand(id, body)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), v => $"/api/lending/lines/{id}/contribution-versions/{v.PublicId}"))
            .WithName("Lending_Lines_AddContributionVersion")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarAportes);

        // ------------------------------------------------------------------------------------------------ cerrar y reabrir --
        // La ruta pide cualquiera de los dos Manage; el comando exige el de la naturaleza de la línea (422 Lending.Permission.Required).
        lineas.MapPost("/{id:guid}/close", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseLendingLineCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Lines_Close")
            .ConClaveDeOperacion()
            .RequirePermission(new AlgunPermiso(AdministrarCaptacion, AdministrarAportes));

        lineas.MapPost("/{id:guid}/reopen", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReopenLendingLineCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Lines_Reopen")
            .ConClaveDeOperacion()
            .RequirePermission(new AlgunPermiso(AdministrarCaptacion, AdministrarAportes));
    }

    /// <summary>El cuerpo de <c>POST /deposit</c>: <c>{ code, name, depositClass, active, firstVersion }</c>.</summary>
    public sealed record CrearLineaDeCaptacionRequest(string? Code, string? Name, DepositClass DepositClass, bool Active, DepositLineVersionInput? FirstVersion);

    /// <summary>El cuerpo de terminar una regla de tasa negociada: desde qué día deja de regir y el motivo (api.md §3).</summary>
    public sealed record TerminarReglaRequest(DateOnly ValidTo, string? Reason);

    /// <summary>El cuerpo de cerrar y reabrir: el motivo.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de <c>POST /contribution</c>: <c>{ code, name, active, firstVersion }</c>.</summary>
    public sealed record CrearLineaDeAportesRequest(string? Code, string? Name, bool Active, ContributionLineVersionInput? FirstVersion);
}
