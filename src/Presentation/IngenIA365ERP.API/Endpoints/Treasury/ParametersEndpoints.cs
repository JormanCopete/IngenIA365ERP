using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.GetParameterHistory;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Los parámetros de Tesorería (2026-11-25, pedido del dueño): las claves del módulo <c>TES</c> de <c>COR_ParameterVersions</c> (giros, cheques, cuentas
/// destino, remesas, conciliación, GMF, notas bancarias, liquidez, adquirentes, el paso a contabilidad) por las consultas y el comando de la plataforma, con
/// el módulo fijo por la ruta. Hasta ese día no había cómo cambiarlas: la ruta de Inventario las rechaza (<c>ModulosConRutaPropia</c>, feature 015 T0042) y la
/// ruta propia nunca se hizo. Leer exige <c>Treasury.Parameters.View</c> y registrar una vigencia <c>Treasury.Parameters.Manage</c>, con
/// <c>Idempotency-Key</c>. Las claves son generales (sin ámbito); las reglas del módulo las aplica <c>ReglasDeParametrosDeTesoreria</c>. Cada ruta sólo
/// reenvía al <see cref="ISender"/>. Es la misma de Recaudo (<c>Endpoints/Receipts/ParametersEndpoints.cs</c>). (nuevo)
/// </summary>
public class ParametersEndpoints : ICarterModule
{
    public const string Ver = "Treasury.Parameters.View";
    public const string Administrar = "Treasury.Parameters.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var parametros = TreasuryGroup.Grupo(app, "/parameters", "Tesorería Parámetros");

        parametros.MapGet("/", async (string? key, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListParametersQuery(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, key, date), ct))
            .WithName("Treasury_Parameters_List")
            .RequirePermission(Ver);

        parametros.MapGet("/{key}/history", async (string key, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetParameterHistoryQuery(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, key), ct))
            .WithName("Treasury_Parameters_History")
            .RequirePermission(Ver);

        parametros.MapPost("/{key}/versions", async (string key, AgregarVigenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                TreasuryGroup.Creado(await sender.Send(new AddParameterVersionCommand(
                    ParametrosDeRecaudoYTesoreria.ModuloTesoreria, key, ParameterScopeKind.None, null, null, body.Value ?? string.Empty, body.ValidFrom,
                    body.Reason ?? string.Empty, null)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), _ => $"{TreasuryGroup.Prefijo}/parameters/{Uri.EscapeDataString(key)}/history"))
            .WithName("Treasury_Parameters_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo del alta: el valor, desde cuándo rige y el motivo. La clave va en la ruta.</summary>
    public sealed record AgregarVigenciaRequest(string? Value, DateOnly ValidFrom, string? Reason);
}
