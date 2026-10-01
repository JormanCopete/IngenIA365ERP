using Microsoft.JSInterop;

namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Imprime un documento ya armado en HTML —la tirilla de una venta, el comprobante de un movimiento— (feature 012, I3, T634). En el
/// navegador lo hace <see cref="ImpresionEnElNavegador"/> con <c>pos.js</c> en un iframe oculto; en la app MAUI, una implementación
/// nativa, porque el WebView de Android no admite <c>window.print</c>. Devuelve si el diálogo de impresión se abrió. (nuevo)
/// </summary>
public interface IImpresionDeDocumentos
{
    Task<bool> ImprimirAsync(string html, string titulo);
}

/// <summary>La impresión del navegador: <c>window.ingeniaPos.imprimir</c> (pos.js). El interop se prueba a mano. (nuevo)</summary>
public sealed class ImpresionEnElNavegador(IJSRuntime js) : IImpresionDeDocumentos
{
    public async Task<bool> ImprimirAsync(string html, string titulo)
    {
        try
        {
            return await js.InvokeAsync<bool>("ingeniaPos.imprimir", html);
        }
        catch (JSException)
        {
            // pos.js no cargó (o el navegador bloqueó el iframe): la pantalla ofrece la carta en PDF.
            return false;
        }
    }
}
