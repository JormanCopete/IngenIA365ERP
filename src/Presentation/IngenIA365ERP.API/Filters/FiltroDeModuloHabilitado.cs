using IngenIA365ERP.Application.Platform.Modules;
using IngenIA365ERP.Domain.Platform;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// La puerta de un módulo habilitable (feature 020, R1, capa 1; FR-002, SC-007): sin el módulo encendido en la cooperativa activa, toda
/// ruta del grupo responde <b>403 <c>Module.NotEnabled</c></b> con el sobre de error, sin mirar permisos. Por eso tampoco entra el maestro
/// (que se salta <c>RequirePermission</c>) ni un rol con <c>*</c>. Va en el grupo, antes que los filtros de cada ruta. (nuevo)
/// </summary>
public sealed class FiltroDeModuloHabilitado(string codigo) : IEndpointFilter
{
    public string Codigo { get; } = ModulosHabilitables.Buscar(codigo)?.Codigo
        ?? throw new ArgumentException($"«{codigo}» no es un módulo habilitable (ModulosHabilitables).", nameof(codigo));

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var cooperativa = http.User.FindFirst("active_tenant_id")?.Value;
        var habilitado = false;
        if (Guid.TryParse(cooperativa, out var tenantPublicId))
        {
            var modulos = http.RequestServices.GetRequiredService<IModulosHabilitados>();
            habilitado = await modulos.EstaHabilitadoAsync(tenantPublicId, Codigo, http.RequestAborted);
        }

        if (habilitado) return await next(context);

        var modulo = ModulosHabilitables.Buscar(Codigo)!;
        return Results.Json(
            new
            {
                code = "Module.NotEnabled",
                message = $"El módulo {modulo.Nombre} no está habilitado para esta cooperativa. Lo habilita el administrador de la plataforma.",
                data = new { module = modulo.Codigo },
                traceId = http.TraceIdentifier,
            },
            statusCode: StatusCodes.Status403Forbidden);
    }
}

/// <summary>Cómo un grupo declara su módulo habilitable.</summary>
public static class FiltroDeModuloHabilitadoExtensions
{
    /// <summary>Marca el grupo como del módulo <paramref name="codigo"/>: sin el módulo encendido, 403 <c>Module.NotEnabled</c>.</summary>
    public static RouteGroupBuilder RequireModulo(this RouteGroupBuilder grupo, string codigo)
    {
        var filtro = new FiltroDeModuloHabilitado(codigo);
        grupo.WithMetadata(filtro);
        grupo.AddEndpointFilter(filtro);
        return grupo;
    }
}
