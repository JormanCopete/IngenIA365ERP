namespace IngenIA365ERP.Storage.Configuration;

/// <summary>
/// Dónde se guardan los adjuntos cifrados. <c>Provider = Local</c> (T107) escribe en el sistema de
/// archivos y es el de desarrollo; <c>Provider = S3</c> (2026-09-22) escribe en un bucket y es el de
/// los ambientes del clúster: el disco del nodo no tiene redundancia ni entra en los respaldos, y un
/// volumen RWO no se monta dos veces cuando haya un segundo nodo.
///
/// <para>
/// Las credenciales <b>no van aquí</b>: las toma el SDK de la cadena estándar (variables
/// <c>AWS_ACCESS_KEY_ID</c> / <c>AWS_SECRET_ACCESS_KEY</c>, que en el clúster vienen de un Secret).
/// Nada de esto se escribe en el repositorio.
/// </para>
/// </summary>
public sealed class AttachmentStorageSettings
{
    public const string SectionName = "AttachmentStorage";

    public const string ProveedorLocal = "Local";
    public const string ProveedorS3 = "S3";

    /// <summary><c>Local</c> o <c>S3</c>; cualquier otro valor hace fallar el arranque.</summary>
    public string Provider { get; set; } = ProveedorLocal;

    /// <summary>Ruta raíz absoluta o relativa donde el store local escribe blobs.</summary>
    public string LocalRootPath { get; set; } = "storage/attachments";

    public S3StorageSettings S3 { get; set; } = new();

    public bool EsS3 => string.Equals(Provider, ProveedorS3, StringComparison.OrdinalIgnoreCase);
}

/// <summary>El bucket de adjuntos. Es <b>otro</b> que el de respaldos: aquel tiene Object Lock a 40 días y un adjunto se borra cuando su dueño lo borra.</summary>
public sealed class S3StorageSettings
{
    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    /// <summary>Sólo para almacenes compatibles con S3 (MinIO, R2) o pruebas; vacío = AWS.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Con <see cref="ServiceUrl"/>, casi siempre hace falta.</summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>Prefijo común dentro del bucket; permite compartir bucket entre ambientes sin mezclarlos (<c>pdn/</c>, <c>qa/</c>).</summary>
    public string? Prefix { get; set; }

    /// <summary>Cifrado del lado del servidor, encima del nuestro. <c>AES256</c> por defecto; vacío lo apaga.</summary>
    public string ServerSideEncryption { get; set; } = "AES256";
}
