namespace IngenIA365ERP.Storage.Configuration;

/// <summary>
/// Configuración del backend local de adjuntos cifrados (T107).
/// Default DEV: <c>storage/attachments</c> relativo al directorio de la app.
/// En prod conviene apuntar a un volumen persistente / share dedicado.
/// </summary>
public sealed class AttachmentStorageSettings
{
    public const string SectionName = "AttachmentStorage";

    /// <summary>Ruta raíz absoluta o relativa donde el store escribe blobs.</summary>
    public string LocalRootPath { get; set; } = "storage/attachments";
}
