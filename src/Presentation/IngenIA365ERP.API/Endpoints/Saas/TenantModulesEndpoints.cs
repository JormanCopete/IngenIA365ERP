using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Platform.Modules;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Saas;

/// <summary>
/// Los módulos habilitables por cooperativa (feature 020, US1; R1; contracts/api.md §1): <c>GET/PUT /api/saas/tenants/{tenantId}/modules</c>,
/// sólo del maestro con segundo factor (<see cref="RequireMasterAdminAttribute.Check"/>, sin código de permiso). Habilitar no escribe en
/// la base de la cooperativa: lo que el módulo necesita allí lo deja listo la tarea de aprovisionamiento en su siguiente pasada.
/// </summary>
public sealed class TenantModulesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/saas/tenants/{tenantId:guid}/modules")
            .WithTags("SaaS / Módulos por cooperativa")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .AddEndpointFilter(async (contexto, siguiente) =>
            {
                var guarda = RequireMasterAdminAttribute.Check(contexto.HttpContext);
                return guarda.IsAllowed ? await siguiente(contexto) : Result.Failure(guarda.ErrorCode!, guarda.Message!);
            });

        g.MapGet("/", async (Guid tenantId, ISender sender, CancellationToken ct) => await sender.Send(new GetTenantModulesQuery(tenantId), ct))
            .WithName("Saas_TenantModules_List");

        g.MapPut("/{code}", async (Guid tenantId, string code, CambioDeModuloRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SetTenantModuleCommand(tenantId, code, body.Enabled, body.Reason ?? string.Empty), ct);
                return r.IsSuccess ? Results.NoContent() : (object)r;
            })
            .WithName("Saas_TenantModules_Set");
    }

    public sealed record CambioDeModuloRequest(bool Enabled, string? Reason);
}
