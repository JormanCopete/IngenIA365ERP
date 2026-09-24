namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Los límites de los adjuntos que decide la plataforma (feature 011): tamaño máximo y cuánto vive cada
/// autorización. Se leen de la sección <c>AttachmentStorage</c>, la misma del almacén, pero viven aquí y
/// no en <c>AttachmentStorageSettings</c> porque quien los aplica es Application —los validadores y los
/// comandos que firman—, y Application no puede depender de Infrastructure (Principio II).
///
/// <para>
/// Desde que el archivo viaja directo entre el navegador y el almacén, el tamaño máximo <b>no depende de la
/// memoria del servidor</b>: es una decisión de negocio y de costo, y por eso es configurable.
/// </para>
/// </summary>
public sealed class LimitesDeAdjuntos
{
    public const string SectionName = "AttachmentStorage";

    /// <summary>Techo de un POST único al almacén (5 GiB): por encima haría falta subida multipart.</summary>
    public const long TechoDelAlmacen = 5L * 1024 * 1024 * 1024;

    /// <summary>Tamaño máximo de un adjunto. Por defecto, los 25 MB de siempre (<see cref="AttachmentPolicy.MaxBytes"/>).</summary>
    public long MaxBytes { get; set; } = AttachmentPolicy.MaxBytes;

    /// <summary>Minutos que vive una autorización de subida. Entre 1 y 5 (FR-011).</summary>
    public int SubidaMinutos { get; set; } = 5;

    /// <summary>Segundos que vive un enlace de descarga. Entre 10 y 300; 60 por defecto (FR-021).</summary>
    public int DescargaSegundos { get; set; } = 60;

    /// <summary>El primer problema de la configuración, o nulo si está bien. Se evalúa al arrancar.</summary>
    public string? Problema() =>
        MaxBytes is < 1 or > TechoDelAlmacen ? $"AttachmentStorage:MaxBytes debe estar entre 1 y {TechoDelAlmacen} (el techo de un POST único al almacén)."
        : SubidaMinutos is < 1 or > 5 ? "AttachmentStorage:SubidaMinutos debe estar entre 1 y 5 (FR-011)."
        : DescargaSegundos is < 10 or > 300 ? "AttachmentStorage:DescargaSegundos debe estar entre 10 y 300 (FR-021)."
        : null;

    /// <summary>El máximo en megabytes, para los mensajes.</summary>
    public string MaxBytesLegible => MaxBytes % (1024 * 1024) == 0 ? $"{MaxBytes / (1024 * 1024)} MB" : $"{MaxBytes:N0} bytes";
}
