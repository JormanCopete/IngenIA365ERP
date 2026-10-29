using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// La matriz contable de Recaudo (feature 015, US8, T0265; api.md §16; contabilidad.md §6, §8): reglas con vigencia, plantilla de filas sugeridas sin cuenta (json y
/// Excel), completitud y el tipo de comprobante de cada hecho, bajo <c>/api/accounting/receipts</c> con <c>Accounting.ReceiptsMatrix.View</c> y <c>.Manage</c>. Las
/// rutas de Tesorería son las mismas bajo <c>/api/accounting/treasury</c> (<see cref="TreasuryIntegrationEndpoints"/>). (nuevo)
/// </summary>
public class ReceiptsIntegrationEndpoints : ICarterModule
{
    public const string Ver = "Accounting.ReceiptsMatrix.View";
    public const string Administrar = "Accounting.ReceiptsMatrix.Manage";

    public void AddRoutes(IEndpointRouteBuilder app) =>
        RutasDeLaMatriz.Publicar(app, "/api/accounting/receipts", "Accounting Receipts Integration", ModulosDeOrigen.Recaudo, Ver, Administrar, "Receipts");
}

/// <summary>La matriz contable de Tesorería, con <c>Accounting.TreasuryMatrix.View</c> y <c>.Manage</c> (T0265). (nuevo)</summary>
public class TreasuryIntegrationEndpoints : ICarterModule
{
    public const string Ver = "Accounting.TreasuryMatrix.View";
    public const string Administrar = "Accounting.TreasuryMatrix.Manage";

    public void AddRoutes(IEndpointRouteBuilder app) =>
        RutasDeLaMatriz.Publicar(app, "/api/accounting/treasury", "Accounting Treasury Integration", ModulosDeOrigen.Tesoreria, Ver, Administrar, "Treasury");
}

/// <summary>Las rutas comunes de las dos matrices. (nuevo)</summary>
public static class RutasDeLaMatriz
{
    public sealed record CrearReglaRequest(string? FactType, string? Role, DimensionesDeReglaDeModuloDto? Dimensions, Guid AccountPublicId, Guid? CostCenterPublicId,
        DateOnly ValidFrom, string? Notes, string? Reason);

    public sealed record DesactivarReglaRequest(DateOnly ValidTo, string? Reason);

    public sealed record MapeoRequest(Guid VoucherTypePublicId, Guid? CrossDocumentTypePublicId, string? Reason);

    public static void Publicar(IEndpointRouteBuilder app, string prefijo, string etiqueta, string modulo, string ver, string administrar, string nombre)
    {
        var g = app.MapGroup(prefijo).WithTags(etiqueta).RequireAuthorization().AddEndpointFilter<ErrorEnvelopeFilter>();

        g.MapGet("/rules", async (DateOnly? asOf, string? factType, string? role, ISender s, CancellationToken ct) =>
                await s.Send(new ListModulePostingRulesQuery(modulo, asOf, factType, role), ct))
            .WithName($"Accounting_{nombre}_Rules_List").RequirePermission(ver);
        g.MapPost("/rules", async (CrearReglaRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new CreateModulePostingRuleCommand(modulo, body.FactType, body.Role ?? string.Empty, body.Dimensions, body.AccountPublicId,
                    body.CostCenterPublicId, body.ValidFrom, body.Notes, body.Reason ?? string.Empty), ct))
            .WithName($"Accounting_{nombre}_Rules_Create").RequirePermission(administrar);
        g.MapPost("/rules/{id:guid}/deactivate", async (Guid id, DesactivarReglaRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new DeactivateModulePostingRuleCommand(modulo, id, body.ValidTo, body.Reason ?? string.Empty), ct))
            .WithName($"Accounting_{nombre}_Rules_Deactivate").RequirePermission(administrar);

        g.MapGet("/rules/template", async (ISender s, CancellationToken ct) => await s.Send(new GetModulePostingTemplateQuery(modulo), ct))
            .WithName($"Accounting_{nombre}_Rules_Template").RequirePermission(ver);
        g.MapGet("/rules/template.xlsx", async (HttpContext http, ISender s, CancellationToken ct) =>
            {
                var filas = await s.Send(new GetModulePostingTemplateQuery(modulo), ct);
                if (filas.IsFailure) return ErrorEnvelopeFilter.Translate(http, filas);
                var tabla = PlantillaDeLaMatriz.Tabla(MatricesDeRecaudoYTesoreria.De(modulo)!, filas.Value);
                var archivo = ExportadorDeTablas.Exportar(tabla, "xlsx", $"plantilla-matriz-contable-{nombre.ToLowerInvariant()}");
                return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
            })
            .WithName($"Accounting_{nombre}_Rules_TemplateFile").RequirePermission(ver);
        g.MapGet("/rules/completeness", async (DateOnly? asOf, ISender s, CancellationToken ct) => await s.Send(new GetModuleCompletenessQuery(modulo, asOf), ct))
            .WithName($"Accounting_{nombre}_Rules_Completeness").RequirePermission(ver);

        g.MapGet("/batches", async (DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) => await s.Send(new ListModuleBatchesQuery(modulo, from, to), ct))
            .WithName($"Accounting_{nombre}_Batches_List").RequirePermission(ver);

        g.MapGet("/voucher-types", async (ISender s, CancellationToken ct) => await s.Send(new ListModuleVoucherMappingsQuery(modulo), ct))
            .WithName($"Accounting_{nombre}_VoucherTypes_List").RequirePermission(ver);
        g.MapPut("/voucher-types/{factType}", async (string factType, MapeoRequest body, ISender s, CancellationToken ct) =>
                await s.Send(new SetModuleVoucherMappingCommand(modulo, factType, body.VoucherTypePublicId, body.CrossDocumentTypePublicId, body.Reason ?? string.Empty), ct))
            .WithName($"Accounting_{nombre}_VoucherTypes_Set").RequirePermission(administrar);
    }
}
