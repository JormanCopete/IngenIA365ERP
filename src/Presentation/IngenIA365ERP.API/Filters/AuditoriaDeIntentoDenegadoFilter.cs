using IngenIA365ERP.Application.Common.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// El metadato de grupo <c>AuditarIntentoDenegado(módulo)</c> (feature 013, T67, F9, T0288; api.md §1.3 y §1.12; research R136): los
/// grupos que lo llevan (todo <c>/api/lending</c>) auditan dos intentos que nunca llegan a ser un comando:
/// <list type="bullet">
/// <item>el <b>404 por falta de permiso</b>: <see cref="PermissionAuthorizationFilter.NotFoundEnvelope"/> marca la petición y, antes de
/// que la respuesta salga, se registra <c>{módulo}.Access.Denied</c> con la ruta;</item>
/// <item>el <b>400 del validador</b>: como <c>ValidationBehavior</c> corre antes que <c>AuditBehavior</c>, el rechazo de forma lo anota
/// <see cref="RechazosDeFormaDeLaPeticion"/> y aquí se registra <c>{módulo}.Request.Rejected</c> con el comando, los campos y los
/// códigos, sin valores.</item>
/// </list>
/// Los dos van por <see cref="AuditoriaEncadenada.RegistrarAsync"/> y <b>sin propagar errores</b>: una auditoría que falla no convierte
/// el 404 en un 500 que delate la ruta. (nuevo)
/// </summary>
public sealed class AuditoriaDeIntentoDenegadoFilter(ILogger<AuditoriaDeIntentoDenegadoFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var resultado = await next(context);

        var http = context.HttpContext;
        var modulo = http.GetEndpoint()?.Metadata.GetMetadata<AuditarIntentoDenegadoAttribute>()?.Modulo;
        if (modulo is null) return resultado;

        if (PermissionAuthorizationFilter.FueDenegada(http))
            await AuditoriaDeIntentos.RegistrarAccesoDenegadoAsync(http.RequestServices, modulo, logger, http.RequestAborted);
        else if (AuditoriaDeIntentos.RechazoAnotado(http.RequestServices) is { } rechazo)
            await AuditoriaDeIntentos.RegistrarRechazoDeFormaAsync(http.RequestServices, modulo, rechazo, logger, http.RequestAborted);

        return resultado;
    }
}

/// <summary>El módulo de auditoría de los intentos de un grupo de rutas (<c>ModuloDeAuditoria.Lending</c>). (nuevo)</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuditarIntentoDenegadoAttribute(string modulo) : Attribute
{
    public string Modulo { get; } = modulo;
}

/// <summary>
/// <c>.AuditarIntentoDenegado(ModuloDeAuditoria.Lending)</c> sobre el grupo (DT §2.15): agrega el metadato y el filtro. Se encadena al
/// grupo <b>antes</b> de las rutas, así su filtro envuelve al de permisos de cada ruta y ve el 404 que éste responde. (nuevo)
/// </summary>
public static class AuditoriaDeIntentoDenegadoExtensions
{
    public static RouteGroupBuilder AuditarIntentoDenegado(this RouteGroupBuilder grupo, string modulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        grupo.WithMetadata(new AuditarIntentoDenegadoAttribute(modulo));
        grupo.AddEndpointFilter<AuditoriaDeIntentoDenegadoFilter>();
        return grupo;
    }

    public static RouteHandlerBuilder AuditarIntentoDenegado(this RouteHandlerBuilder ruta, string modulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        ruta.WithMetadata(new AuditarIntentoDenegadoAttribute(modulo));
        ruta.AddEndpointFilter<AuditoriaDeIntentoDenegadoFilter>();
        return ruta;
    }
}
