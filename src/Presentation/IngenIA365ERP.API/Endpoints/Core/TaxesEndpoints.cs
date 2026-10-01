using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// El catálogo tributario de Core (feature 012, T22, T167; contracts/api.md §30): impuestos y retenciones
/// (<c>/api/core/taxes</c>), tarifas con vigencia (<c>/api/core/tax-rates</c>, <c>/{id}/close</c>, <c>/{id}/review</c>) y
/// conceptos de retención (<c>/api/core/withholding-concepts</c>), más la plantilla 1 (<c>/api/core/taxes/template.xlsx</c>,
/// <c>/api/core/taxes/import</c>). Consulta y plantilla vacía con <c>Core.Taxes.View</c>; escritura e importación con
/// <c>Core.Taxes.Manage</c> e <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class TaxesEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.Taxes.View";
    public const string PermisoDeEscritura = "Core.Taxes.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Impuestos(app);
        Tarifas(app);
        Conceptos(app);
    }

    private static void Impuestos(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/taxes")
            .WithTags("Core Taxes")
            .RequireAuthorization();

        group.MapGet("/", async (TaxKind? kind, bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTaxDefinitionsQuery(kind, active), ct))
            .WithName("Core_Taxes_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetTaxDefinitionQuery(id), ct))
            .WithName("Core_Taxes_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (CrearImpuestoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateTaxDefinitionCommand(
                    body.Code ?? string.Empty, body.Name ?? string.Empty, body.Kind, body.CalculationForm, body.TaxedOnTaxPublicId,
                    body.IsWithholding ?? false, body.DianTaxCode, body.Notes, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/core/taxes/{result.Value}", new { taxPublicId = result.Value }) : result;
            })
            .WithName("Core_Taxes_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, EditarImpuestoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateTaxDefinitionCommand(id, body.Name ?? string.Empty, body.DianTaxCode, body.IsActive, body.Notes, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_Taxes_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        // Plantilla 1 (contracts/plantillas.md §1): en Core basta el permiso de consulta para descargar con datos (§0.6).
        group.MapPlantilla(PermisoDeConsulta, PermisoDeEscritura, PlantillaDeImpuestos.Clave, "Core_Taxes",
            importar: (modo, archivo, motivo, clave) => new ImportTaxCatalogCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetTaxCatalogTemplateDataQuery());
    }

    private static void Tarifas(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/tax-rates")
            .WithTags("Core Taxes")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? tax, DateOnly? asOf, string? municipality, bool? reviewPending, bool? onlyCurrent, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTaxRatesQuery(tax, asOf, municipality, reviewPending, onlyCurrent), ct))
            .WithName("Core_TaxRates_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetTaxRateQuery(id), ct))
            .WithName("Core_TaxRates_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (TarifaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateTaxRateCommand(
                    body.TaxPublicId, body.Code, body.Name ?? string.Empty, body.Rate, body.AmountPerUnit, body.WithholdingConceptPublicId,
                    body.MunicipalityDaneCode, body.ActivityCode, body.MinimumBaseUvt, body.MinimumBasePesos, body.Conditions,
                    body.AppliesTo, body.Priority ?? 0, body.ValidFrom, body.ValidTo, body.LegalSource ?? string.Empty, body.Notes, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/core/tax-rates/{result.Value}", new { taxRatePublicId = result.Value }) : result;
            })
            .WithName("Core_TaxRates_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, TarifaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateTaxRateCommand(
                    id, body.Code, body.Name ?? string.Empty, body.Rate, body.AmountPerUnit, body.WithholdingConceptPublicId,
                    body.MunicipalityDaneCode, body.ActivityCode, body.MinimumBaseUvt, body.MinimumBasePesos, body.Conditions,
                    body.AppliesTo, body.Priority ?? 0, body.ValidFrom, body.ValidTo, body.LegalSource ?? string.Empty, body.Notes, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_TaxRates_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPost("/{id:guid}/close", async (Guid id, CerrarTarifaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseTaxRateCommand(id, body.ValidTo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_TaxRates_Close")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPost("/{id:guid}/review", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReviewTaxRateCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_TaxRates_Review")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    private static void Conceptos(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/withholding-concepts")
            .WithTags("Core Taxes")
            .RequireAuthorization();

        group.MapGet("/", async (bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListWithholdingConceptsQuery(active), ct))
            .WithName("Core_WithholdingConcepts_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (ConceptoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateWithholdingConceptCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Notes, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/core/withholding-concepts/{result.Value}", new { withholdingConceptPublicId = result.Value })
                    : result;
            })
            .WithName("Core_WithholdingConcepts_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, EditarConceptoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateWithholdingConceptCommand(id, body.Name ?? string.Empty, body.IsActive, body.Notes, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_WithholdingConcepts_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    /// <summary>El alta de un impuesto (§30).</summary>
    public sealed record CrearImpuestoRequest(
        string? Code, string? Name, TaxKind Kind, TaxCalculationForm CalculationForm, Guid? TaxedOnTaxPublicId,
        bool? IsWithholding, string? DianTaxCode, string? Notes, string? Reason);

    /// <summary>La edición de un impuesto (§30): el código, la clase y la forma de cálculo no cambian.</summary>
    public sealed record EditarImpuestoRequest(string? Name, string? DianTaxCode, bool IsActive, string? Notes, string? Reason);

    /// <summary>El alta o la corrección de una tarifa (§30: <c>TaxRateDto</c> sin id ni <c>reviewPending</c>, con motivo).</summary>
    public sealed record TarifaRequest(
        Guid TaxPublicId, string? Code, string? Name, decimal? Rate, decimal? AmountPerUnit, Guid? WithholdingConceptPublicId,
        string? MunicipalityDaneCode, string? ActivityCode, decimal? MinimumBaseUvt, decimal? MinimumBasePesos,
        TaxRateConditionsDto? Conditions, TaxAppliesTo AppliesTo, short? Priority, DateOnly ValidFrom, DateOnly? ValidTo,
        string? LegalSource, string? Notes, string? Reason);

    /// <summary>Cerrar una vigencia (§30).</summary>
    public sealed record CerrarTarifaRequest(DateOnly ValidTo, string? Reason);

    /// <summary>Marcar revisada (§30).</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El alta de un concepto (§30).</summary>
    public sealed record ConceptoRequest(string? Code, string? Name, string? Notes, string? Reason);

    /// <summary>La edición de un concepto (§30).</summary>
    public sealed record EditarConceptoRequest(string? Name, bool IsActive, string? Notes, string? Reason);
}
