namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Feature 011 (research R6): contrasta el inicio real de un archivo con el tipo que se declaró al pedir
/// la subida. Es puro —recibe los primeros bytes, no toca el almacén— y lo usa la confirmación, que lee
/// sólo los primeros <see cref="BytesAExaminar"/> del objeto: la firma está al principio, y leer más
/// metería el archivo en la memoria del servidor, que es lo que la feature quiere evitar.
///
/// <para>
/// No es un antivirus: un PDF con contenido malicioso pasa. Lo que atrapa es la mentira sobre el tipo,
/// que es lo que permitiría servir un ejecutable como si fuera un soporte. Entre <c>.docx</c> y
/// <c>.xlsx</c> no distingue —comparten contenedor ZIP—, y confundirlos no es un problema de seguridad.
/// </para>
/// </summary>
public static class FirmaDeContenido
{
    /// <summary>Cuánto del principio del archivo se examina.</summary>
    public const int BytesAExaminar = 8 * 1024;

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Ole2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] TiposDeOffice = "[Content_Types].xml"u8.ToArray();
    private static readonly byte[] Ejecutable = "MZ"u8.ToArray();
    private static readonly byte[] Elf = [0x7F, 0x45, 0x4C, 0x46];

    /// <summary>
    /// Nulo si el inicio del archivo corresponde al tipo declarado; si no, el motivo, en palabras que la
    /// pantalla puede mostrar tal cual (cabe en <c>RejectionReason</c>).
    /// </summary>
    /// <param name="contentType">El tipo declarado; se ignoran los parámetros (<c>; charset=…</c>).</param>
    /// <param name="inicio">Los primeros bytes del archivo (hasta <see cref="BytesAExaminar"/>).</param>
    /// <param name="tamano">El tamaño total del archivo.</param>
    public static string? Motivo(string contentType, ReadOnlySpan<byte> inicio, long tamano)
    {
        if (tamano <= 0 || inicio.IsEmpty) return "El archivo está vacío.";

        var tipo = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        var (esperado, coincide) = tipo switch
        {
            "application/pdf" => ("un PDF", inicio.StartsWith(Pdf)),
            "image/png" => ("una imagen PNG", inicio.StartsWith(Png)),
            "image/jpeg" => ("una imagen JPEG", inicio.StartsWith(Jpeg)),
            "image/gif" => ("una imagen GIF", inicio.StartsWith(Gif87) || inicio.StartsWith(Gif89)),
            "image/webp" => ("una imagen WebP", EsWebp(inicio)),
            "application/msword" => ("un documento de Word (.doc)", inicio.StartsWith(Ole2)),
            "application/vnd.ms-excel" => ("una hoja de Excel (.xls)", inicio.StartsWith(Ole2)),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ("un documento de Word (.docx)", EsOffice(inicio)),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ("una hoja de Excel (.xlsx)", EsOffice(inicio)),
            "text/plain" => ("un texto", EsTexto(inicio)),
            "text/csv" => ("un CSV", EsTexto(inicio)),
            _ => (string.Empty, false),
        };

        if (esperado.Length == 0) return $"El tipo «{tipo}» no está admitido.";
        if (coincide) return null;
        return Detectar(inicio) is { } real
            ? $"Se declaró {esperado}, pero el contenido es {real}."
            : $"Se declaró {esperado}, pero el contenido no lo es.";
    }

    /// <summary>Qué parece ser el archivo, para que el motivo diga algo útil; nulo si no se reconoce.</summary>
    internal static string? Detectar(ReadOnlySpan<byte> inicio) =>
        inicio.StartsWith(Ejecutable) ? "un ejecutable de Windows"
        : inicio.StartsWith(Elf) ? "un ejecutable de Linux"
        : inicio.StartsWith(Pdf) ? "un PDF"
        : inicio.StartsWith(Png) ? "una imagen PNG"
        : inicio.StartsWith(Jpeg) ? "una imagen JPEG"
        : inicio.StartsWith(Gif87) || inicio.StartsWith(Gif89) ? "una imagen GIF"
        : EsWebp(inicio) ? "una imagen WebP"
        : inicio.StartsWith(Ole2) ? "un documento de Office antiguo"
        : EsOffice(inicio) ? "un documento de Office"
        : inicio.StartsWith(Zip) ? "un archivo comprimido ZIP"
        : EsTexto(inicio) ? "un texto"
        : null;

    private static bool EsWebp(ReadOnlySpan<byte> inicio) =>
        inicio.Length >= 12 && inicio.StartsWith(Riff) && inicio.Slice(8, 4).SequenceEqual(Webp);

    /// <summary>Un .docx o .xlsx es un ZIP cuya primera entrada suele ser <c>[Content_Types].xml</c>.</summary>
    private static bool EsOffice(ReadOnlySpan<byte> inicio) =>
        inicio.StartsWith(Zip) && inicio.IndexOf(TiposDeOffice) >= 0;

    /// <summary>
    /// Sin bytes de control binarios, salvo tabulador, retorno y salto de línea. Cualquier otro byte es
    /// texto en UTF-8 o, si no lo es, en Latin-1, que admite todos los demás.
    /// </summary>
    private static bool EsTexto(ReadOnlySpan<byte> inicio)
    {
        foreach (var b in inicio)
        {
            if (b is 0x09 or 0x0A or 0x0D) continue;
            if (b < 0x20 || b == 0x7F) return false;
        }
        return true;
    }
}
