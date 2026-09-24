namespace IngenIA365ERP.Shared.Services.Adjuntos;

/// <summary>Los estados de un adjunto tal como viajan (números; contracts/api.md de la feature 011).</summary>
public static class EstadosDeAdjunto
{
    public const int Subiendo = 1;
    public const int Disponible = 2;
    public const int Rechazado = 3;
    public const int Incompleto = 4;
}

/// <summary>Los formatos: el anterior se baja por la API (hay que descifrarlo); el directo, por enlace.</summary>
public static class FormatosDeAdjunto
{
    public const int Anterior = 1;
    public const int Directo = 2;
}

/// <summary>
/// Los tipos que se admiten como soporte, los mismos de la lista blanca del servidor
/// (<c>AttachmentPolicy</c>). Aquí sólo sirven para avisar antes de subir; quien decide es el servidor.
/// </summary>
public static class TiposDeSoporte
{
    public static readonly IReadOnlySet<string> Admitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg", "image/webp", "image/gif",
        "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain", "text/csv",
    };

    /// <summary>Para el <c>accept</c> del selector de archivos.</summary>
    public const string Extensiones = ".pdf,.png,.jpg,.jpeg,.webp,.gif,.doc,.docx,.xls,.xlsx,.txt,.csv";

    /// <summary>El tope por defecto; el servidor puede tener otro y lo devuelve al autorizar.</summary>
    public const long MaxBytesPorDefecto = 25L * 1024 * 1024;
}

public sealed record AdjuntoDto(
    Guid PublicId,
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex,
    DateTime CreatedAt,
    string? CreatedBy,
    bool CanDelete,
    int Status,
    int Format,
    string? RejectionReason,
    bool NeedsConfirmation)
{
    public bool Disponible => Status == EstadosDeAdjunto.Disponible;
    public bool Incompleto => Status == EstadosDeAdjunto.Incompleto;
}

public sealed record SolicitudDeSubidaRequest(
    string OwnerEntityType, Guid OwnerEntityPublicId, string FileName, string ContentType, long SizeBytes, string Sha256Base64);

public sealed record AutorizacionDeSubidaDto(Guid AttachmentPublicId, SubidaAutorizadaDto Upload, long MaxBytes);

public sealed record SubidaAutorizadaDto(string Url, string Method, Dictionary<string, string> Fields, string FileField, DateTimeOffset ExpiresAt);

public sealed record ConfirmacionDeSubidaDto(int Status, string? RejectionReason, DateTime? ConfirmedAt);

public sealed record EnlaceDeDescargaDto(bool Direct, string? Url, DateTimeOffset? ExpiresAt);

/// <summary>Lo que devuelve <c>adjuntos.preparar</c>: la descripción del archivo elegido, sin sus bytes.</summary>
public sealed record ArchivoElegido(string Id, string Name, string Type, long Size, string Sha256);

/// <summary>Lo que devuelve <c>adjuntos.subir</c>: si el almacén aceptó el archivo, y si no, qué dijo.</summary>
public sealed record ResultadoDeSubida(bool Ok, int Status, string? Code, string? Error);
