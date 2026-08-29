using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.SystemSettings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Parámetros de configuración de la cooperativa activa (COR_SystemSettings).
///
/// <para>
/// Son por tenant: el país, la moneda funcional y los decimales contables los
/// decide cada cooperativa. El aislamiento lo garantiza el DbContext
/// tenant-aware, no este módulo.
/// </para>
///
/// <para>
/// No hay POST ni DELETE a propósito: las claves las declara el código que las
/// consume y las crea el sembrador paramétrico. Sólo se cambia el valor.
/// </para>
/// </summary>
public sealed class ParametrosModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/parametros")
            .WithTags("Admin / Parametros")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        grupo.MapGet("/", ListarAsync)
            .WithName("Parametros_Listar")
            .RequirePermission("Security.Parameters.View");

        grupo.MapPut("/{publicId:guid}", ActualizarAsync)
            .WithName("Parametros_Actualizar")
            .RequirePermission("Security.Parameters.Update");
    }

    private static async Task<object?> ListarAsync(
        [FromQuery] string? modulo, ISender sender, CancellationToken ct) =>
        await sender.Send(new ListarParametrosQuery(modulo), ct);

    private static async Task<object?> ActualizarAsync(
        Guid publicId, [FromBody] ActualizarParametroBody body,
        ISender sender, CancellationToken ct) =>
        await sender.Send(new ActualizarParametroCommand(publicId, body.Valor), ct);

    public sealed record ActualizarParametroBody(string? Valor);
}
