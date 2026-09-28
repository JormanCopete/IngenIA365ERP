using IngenIA365ERP.Shared.Services;

namespace IngenIA365ERP.App.Services
{
    /// <summary>
    /// La impresión de la app MAUI (feature 012, I3, T634): el WebView de Android no admite <c>window.print</c>, así que la tirilla se
    /// escribe como HTML en la caché de la app y se entrega al sistema con la hoja de compartir, desde donde el servicio de impresión
    /// del equipo (o la app de la impresora térmica) la imprime. El archivo sólo lleva la tirilla —lo mismo que el papel— y queda en la
    /// caché, que el sistema limpia. Se verifica a mano en el equipo (la verificación de MAUI está pendiente). (nuevo)
    /// </summary>
    public sealed class ImpresionNativa : IImpresionDeDocumentos
    {
        public async Task<bool> ImprimirAsync(string html, string titulo)
        {
            var nombre = $"tirilla-{DateTime.UtcNow:yyyyMMddHHmmssfff}.html";
            var ruta = Path.Combine(FileSystem.CacheDirectory, nombre);
            await File.WriteAllTextAsync(ruta, html);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = string.IsNullOrWhiteSpace(titulo) ? "Imprimir" : titulo,
                File = new ShareFile(ruta, "text/html"),
            });
            return true;
        }
    }
}
