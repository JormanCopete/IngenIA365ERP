using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// El grupo base de Recaudo (feature 015, T0046; R-04; contracts/api.md §1): todo archivo de <c>Endpoints/Receipts/*.cs</c> arma sus rutas sobre
/// <see cref="Grupo"/>, que es <c>/api/receipts/{área}</c> con <c>RequireAuthorization()</c> (sesión y cooperativa resuelta por el middleware),
/// <see cref="ErrorEnvelopeFilter"/> y <c>.AuditarIntentoDenegado(ModuloDeAuditoria.Receipts)</c>. El limitador es el de la API para toda
/// cooperativa; la <b>guarda de módulo activo</b> no es un filtro de ruta: la aplica <c>ModuloActivoBehavior</c> en Application a toda escritura
/// de negocio (<c>IEscrituraDeRecaudo</c>), que responde <c>Receipts.NotActivated</c>, así la configuración y la puesta en marcha siguen
/// abiertas antes de activar. Cada ruta declara su permiso (<c>.RequirePermission(...)</c>; <c>LosEndpointsProtegidosExigenPermiso</c>) y sólo
/// usa <c>PublicId</c> en rutas y cuerpos (FR-017). Este archivo no publica rutas. (nuevo)
/// </summary>
public static class ReceiptsGroup
{
    /// <summary>El prefijo de todas las rutas de Recaudo.</summary>
    public const string Prefijo = "/api/receipts";

    /// <summary>El grupo de un área de Recaudo: <paramref name="area"/> relativa al prefijo (<c>"/cash-boxes"</c>) y la etiqueta de OpenAPI.</summary>
    public static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string area, string etiqueta) =>
        app.MapGroup(Prefijo + area)
            .WithTags(etiqueta)
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.Receipts)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

    /// <summary>201 con la ubicación del recurso creado; un fallo pasa tal cual al sobre de error.</summary>
    public static object Creado<T>(Result<T> resultado, Func<T, string> ubicacion) =>
        resultado.IsSuccess ? Results.Created(ubicacion(resultado.Value), resultado.Value) : resultado;
}
