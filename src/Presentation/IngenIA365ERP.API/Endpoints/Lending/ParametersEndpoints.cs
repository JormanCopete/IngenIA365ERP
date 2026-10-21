using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.GetParameterHistory;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Application.Lending.Integration.Queries;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los parámetros de Cartera (feature 013, T0314, T42, T43; contracts/api.md §3.4): las claves del módulo <c>LND</c> de
/// <c>COR_ParameterVersions</c> (catálogo <see cref="ParametrosDeCartera"/>) por las consultas y el comando de la plataforma, con el módulo
/// fijo por la ruta. Leer exige <c>Lending.Parameters.View</c>; registrar una vigencia <c>Lending.Parameters.Manage</c> —y el permiso propio de
/// la clave, que revisa el comando (<c>Segregacion.Incompatibilidades</c> → <c>Lending.Segregation.Manage</c>)— con <c>Idempotency-Key</c>.
/// Los pisos y techos legales y la entrega de cada clave los aplica <c>ReglasDeParametrosDeCartera</c>. Los ámbitos son <c>None</c> y
/// <c>CreditLine</c> y, desde US7, <c>LendingFactType</c> con su lista (<c>GET /api/lending/fact-types</c>, <c>Parameters.View</c>). Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class ParametersEndpoints : ICarterModule
{
    public const string Ver = "Lending.Parameters.View";
    public const string Administrar = "Lending.Parameters.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var parametros = LendingGroup.Grupo(app, "/parameters", "Cartera Parámetros");

        parametros.MapGet("/", async (string? key, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListParametersQuery(ParametrosDeCartera.Modulo, key, date), ct))
            .WithName("Lending_Parameters_List")
            .RequirePermission(Ver);

        // US7: el catálogo de tipos de hecho (ámbito LendingFactType y filtro del envío posterior de la bandeja).
        LendingGroup.Grupo(app, "/fact-types", "Cartera Parámetros").MapGet("/", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingFactTypesQuery(), ct))
            .WithName("Lending_FactTypes_List")
            .RequirePermission(Ver);

        parametros.MapGet("/{key}/history", async (string key, ParameterScopeKind? scopeKind, Guid? scopePublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetParameterHistoryQuery(ParametrosDeCartera.Modulo, key, scopeKind, scopePublicId), ct))
            .WithName("Lending_Parameters_History")
            .RequirePermission(Ver);

        parametros.MapPost("/{key}/versions", async (string key, AgregarVigenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddParameterVersionCommand(
                    ParametrosDeCartera.Modulo, key, body.ScopeKind, body.ScopePublicId, null, body.Value ?? string.Empty, body.ValidFrom,
                    body.Reason ?? string.Empty, body.LegalSource)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/parameters/{Uri.EscapeDataString(key)}/history"))
            .WithName("Lending_Parameters_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo del alta (§3.4); la clave va en la ruta, sin el prefijo «Cartera.».</summary>
    public sealed record AgregarVigenciaRequest(
        ParameterScopeKind ScopeKind,
        Guid? ScopePublicId,
        string? Value,
        DateOnly ValidFrom,
        string? Reason,
        string? LegalSource);
}
