using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.GetParameterHistory;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Parámetros con vigencia (feature 012, T21, T072; contracts/api.md §7): una ruta para todas las claves de
/// <c>COR_ParameterVersions</c> (módulos <c>INV</c>, <c>TAX</c>, <c>EINV</c>). Leer exige
/// <c>Inventory.Parameters.View</c>; registrar una vigencia, <c>Inventory.Parameters.Manage</c> —y el permiso
/// propio de la clave, que revisa el comando— con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. Los permisos los siembra el catálogo de Inventario (fase 3, T48); hasta entonces sólo
/// entra el administrador maestro.
/// </summary>
public class ParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/parameters")
            .WithTags("Inventory Parameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListParametersQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Inventory_Parameters_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Parameters.View");

        group.MapGet("/{module}/{key}/history", async (string module, string key, ParameterScopeKind? scopeKind, Guid? scopePublicId,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new GetParameterHistoryQuery(module, key, scopeKind, scopePublicId), ct))
            .WithName("Inventory_Parameters_History")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Parameters.View");

        group.MapPost("/{module}/{key}/versions", async (string module, string key, AgregarVigenciaRequest body, HttpContext http,
                ISender sender, CancellationToken ct) =>
            {
                var command = new AddParameterVersionCommand(module, key, body.ScopeKind, body.ScopePublicId, body.Chain,
                    body.Value ?? string.Empty, body.ValidFrom, body.Reason ?? string.Empty, body.LegalSource,
                    body.ConfirmFiscalWithoutPosting ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                };
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/parameters/{module}/{key}/history", result.Value)
                    : result;
            })
            .WithName("Inventory_Parameters_AddVersion")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Parameters.Manage");
    }

    /// <summary>El cuerpo del alta (contracts/api.md §7); módulo y clave van en la ruta.</summary>
    public sealed record AgregarVigenciaRequest(
        ParameterScopeKind ScopeKind,
        Guid? ScopePublicId,
        string? Chain,
        string? Value,
        DateOnly ValidFrom,
        string? Reason,
        string? LegalSource,
        bool? ConfirmFiscalWithoutPosting);
}
