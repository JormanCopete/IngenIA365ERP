using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.GetParameterHistory;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// Los parámetros de Recaudo (2026-11-25, pedido del dueño): las claves del módulo <c>RCD</c> de <c>COR_ParameterVersions</c> (la caja,
/// los recibos, la caja única, el arqueo) por las consultas y el comando de la plataforma, con el módulo fijo por la ruta. Hasta ese día no había
/// cómo cambiarlas: la ruta de Inventario las rechaza (<c>ModulosConRutaPropia</c>, feature 015 T0042) y la ruta propia nunca se hizo, así que
/// <c>Caja.SesionUnica</c>, <c>Caja.ArqueoEstricto</c> y las demás regían siempre con su defecto. Leer exige <c>Receipts.Setup.View</c> y registrar
/// una vigencia <c>Receipts.Setup.Manage</c> —los de la configuración de Recaudo, sin permisos nuevos— con <c>Idempotency-Key</c>. Las claves
/// de Recaudo son generales (sin ámbito); las reglas del módulo las aplica <c>ReglasDeParametrosDeRecaudo</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class ParametersEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Setup.View";
    public const string Administrar = "Receipts.Setup.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var parametros = ReceiptsGroup.Grupo(app, "/parameters", "Recaudo Parámetros");

        parametros.MapGet("/", async (string? key, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListParametersQuery(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, key, date), ct))
            .WithName("Receipts_Parameters_List")
            .RequirePermission(Ver);

        parametros.MapGet("/{key}/history", async (string key, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetParameterHistoryQuery(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, key), ct))
            .WithName("Receipts_Parameters_History")
            .RequirePermission(Ver);

        parametros.MapPost("/{key}/versions", async (string key, AgregarVigenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                ReceiptsGroup.Creado(await sender.Send(new AddParameterVersionCommand(
                    ParametrosDeRecaudoYTesoreria.ModuloRecaudo, key, ParameterScopeKind.None, null, null, body.Value ?? string.Empty, body.ValidFrom,
                    body.Reason ?? string.Empty, null)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), _ => $"{ReceiptsGroup.Prefijo}/parameters/{Uri.EscapeDataString(key)}/history"))
            .WithName("Receipts_Parameters_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo del alta: el valor, desde cuándo rige y el motivo. La clave va en la ruta.</summary>
    public sealed record AgregarVigenciaRequest(string? Value, DateOnly ValidFrom, string? Reason);
}
