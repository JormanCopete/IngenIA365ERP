using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// El grupo base de la Tesorería nueva (feature 015, T0046; R-04; contracts/api.md §1): <c>/api/treasury/{área}</c> con
/// <c>RequireAuthorization()</c>, <see cref="ErrorEnvelopeFilter"/> y <c>.AuditarIntentoDenegado(ModuloDeAuditoria.Treasury)</c>; la guarda de
/// módulo activo la aplica <c>ModuloActivoBehavior</c> a toda escritura de negocio (<c>IEscrituraDeTesoreria</c>, <c>Treasury.NotActivated</c>).
/// Las áreas nuevas no chocan con las heredadas, que siguen en sus rutas sin cambios hasta su retiro después de T2:
/// <c>/api/treasury/{checks, concepts, invoices, cash-flow}</c> (<see cref="AreasHeredadas"/>). Este archivo no publica rutas. (nuevo)
/// </summary>
public static class TreasuryGroup
{
    /// <summary>El prefijo de todas las rutas de Tesorería.</summary>
    public const string Prefijo = "/api/treasury";

    /// <summary>Las áreas de la Tesorería heredada: ningún área nueva puede llamarse así.</summary>
    public static readonly IReadOnlyList<string> AreasHeredadas = ["/checks", "/concepts", "/invoices", "/cash-flow"];

    /// <summary>El grupo de un área de la Tesorería nueva.</summary>
    public static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string area, string etiqueta)
    {
        if (AreasHeredadas.Any(h => area.Equals(h, StringComparison.OrdinalIgnoreCase) || area.StartsWith(h + "/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"«{area}» es un área de la Tesorería heredada (R-04): la Tesorería nueva usa otra.");
        return app.MapGroup(Prefijo + area)
            .WithTags(etiqueta)
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.Treasury)
            .AddEndpointFilter<ErrorEnvelopeFilter>();
    }

    /// <summary>201 con la ubicación del recurso creado; un fallo pasa tal cual al sobre de error.</summary>
    public static object Creado<T>(Result<T> resultado, Func<T, string> ubicacion) =>
        resultado.IsSuccess ? Results.Created(ubicacion(resultado.Value), resultado.Value) : resultado;
}
