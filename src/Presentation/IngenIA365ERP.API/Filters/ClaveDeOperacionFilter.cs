using IngenIA365ERP.Application.Common.Behaviors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// La cabecera <c>Idempotency-Key</c> de una ruta que envía un comando <see cref="IOperacionIdempotente"/>
/// (feature 012, decisiones-transversales T13, T056; contracts/api.md §2.3). Se aplica con
/// <see cref="ClaveDeOperacionExtensions.ConClaveDeOperacion{TBuilder}"/>:
///
/// <list type="bullet">
/// <item>Sin cabecera o con algo que no es un UUID (o el UUID vacío): 400 <c>Operation.KeyRequired</c> con el
/// sobre de siempre, sin llegar al handler.</item>
/// <item>Con clave: la deja en <see cref="EstadoDeLaOperacion.Clave"/> (y en <c>HttpContext.Items</c>) para
/// que la ruta la copie al comando con <see cref="ClaveDeOperacionExtensions.ClaveDeOperacion"/>.</item>
/// <item>Después de ejecutar, si <c>IdempotencyBehavior</c> respondió con el resultado guardado, agrega
/// <c>Idempotent-Replayed: true</c>. La cabecera se escribe antes de que el resultado se ejecute, así que
/// da igual el orden respecto de <see cref="ErrorEnvelopeFilter"/>.</item>
/// </list>
/// </summary>
public sealed class ClaveDeOperacionFilter : IEndpointFilter
{
    public const string Cabecera = "Idempotency-Key";
    public const string CabeceraDeRepeticion = "Idempotent-Replayed";

    internal const string ClaveEnItems = "ingenia365.operation-key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var valor = http.Request.Headers[Cabecera].ToString();
        if (!Guid.TryParse(valor, out var clave) || clave == Guid.Empty)
        {
            var error = ErroresDeOperacion.ClaveRequerida();
            return Results.Json(new { code = error.Code, message = error.Message, traceId = http.TraceIdentifier },
                statusCode: StatusCodes.Status400BadRequest);
        }

        http.Items[ClaveEnItems] = clave;
        var estado = http.RequestServices.GetService<EstadoDeLaOperacion>();
        if (estado is not null) estado.Clave = clave;

        var resultado = await next(context);

        if (estado is { EsRepeticion: true } && !http.Response.HasStarted)
            http.Response.Headers[CabeceraDeRepeticion] = "true";

        return resultado;
    }
}

public static class ClaveDeOperacionExtensions
{
    /// <summary>Exige <c>Idempotency-Key</c> en la ruta y marca la repetición en la respuesta.</summary>
    public static TBuilder ConClaveDeOperacion<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter<TBuilder, ClaveDeOperacionFilter>();

    /// <summary>
    /// La clave que leyó <see cref="ClaveDeOperacionFilter"/>, para copiarla a <c>OperationKey</c> del comando:
    /// <c>command with { OperationKey = http.ClaveDeOperacion() }</c>. <see cref="Guid.Empty"/> si la ruta no
    /// lleva el filtro, y entonces <c>IdempotencyBehavior</c> responde <c>Operation.KeyRequired</c>.
    /// </summary>
    public static Guid ClaveDeOperacion(this HttpContext http) =>
        http.Items.TryGetValue(ClaveDeOperacionFilter.ClaveEnItems, out var clave) && clave is Guid g ? g : Guid.Empty;
}
