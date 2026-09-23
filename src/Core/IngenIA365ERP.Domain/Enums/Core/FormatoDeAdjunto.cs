namespace IngenIA365ERP.Domain.Enums.Core;

/// <summary>
/// Cómo está guardado un adjunto (feature 011). Decide por dónde se baja: el formato anterior sólo
/// lo puede leer la API, porque lo cifró la aplicación con una llave que el almacén no tiene; el
/// directo lo baja el navegador con un enlace firmado, porque lo cifra el propio almacén.
/// </summary>
public enum FormatoDeAdjunto
{
    /// <summary>
    /// Formato anterior al 2026-09-23: cifrado por la aplicación (AES-256-GCM con clave por archivo,
    /// envuelta con DataProtection) antes de guardarlo. Se sigue leyendo por la API, sin migración.
    /// </summary>
    AppEncrypted = 1,

    /// <summary>Cifrado en reposo por el almacén (SSE-S3). Se sube y se baja con URLs prefirmadas.</summary>
    Direct = 2,
}
