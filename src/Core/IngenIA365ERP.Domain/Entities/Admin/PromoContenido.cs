using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Pieza promocional que se muestra en la pantalla de inicio de sesión. Mapea a
/// [dbo].[ADM_PromoContenidos], en la base Admin.
///
/// <para>
/// Es contenido de IngenIA365 hacia sus clientes, no de una cooperativa hacia
/// sus asociados: por eso vive en Admin y no tiene tenant. Lo administra el
/// personal de la empresa desde el propio ERP.
/// </para>
///
/// <para>
/// Lo lee gente SIN autenticar. Todo lo que se guarde acá es público de hecho,
/// aunque esté marcado como no publicado: no pongas nada que no pueda verse
/// desde fuera.
/// </para>
/// </summary>
public class PromoContenido : AuditableEntity
{
    [MaxLength(120)]
    public string Titulo { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Texto { get; set; }

    /// <summary>Texto del botón. Si va vacío, no se muestra botón.</summary>
    [MaxLength(60)]
    public string? TextoEnlace { get; set; }

    /// <summary>Destino del botón. Sólo https, o una ruta interna.</summary>
    [MaxLength(500)]
    public string? Enlace { get; set; }

    /// <summary>
    /// Imagen guardada en la propia base. Son pocas piezas y pequeñas, así que
    /// no compensa montar almacenamiento de objetos ni depender de un servicio
    /// externo que pueda caerse justo en la pantalla de entrada.
    /// </summary>
    public byte[]? Imagen { get; set; }

    [MaxLength(60)]
    public string? ImagenTipoMime { get; set; }

    /// <summary>
    /// Descripción de la imagen para lectores de pantalla. Obligatoria si hay
    /// imagen: la pantalla de entrada es lo primero que encuentra alguien que
    /// navega con lector, y una imagen sin descripción ahí no se puede saltar.
    /// </summary>
    [MaxLength(200)]
    public string? ImagenTextoAlternativo { get; set; }

    public int Orden { get; set; }

    public bool Publicado { get; set; }

    public DateTime? VigenteDesde { get; set; }

    public DateTime? VigenteHasta { get; set; }

    /// <summary>Tamaño máximo de la imagen. Viaja en cada carga del login.</summary>
    public const int MaximoBytesImagen = 400 * 1024;

    /// <summary>Tipos admitidos. No se acepta SVG: puede llevar script.</summary>
    public static readonly IReadOnlySet<string> TiposImagenAdmitidos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/webp", "image/avif",
        };

    /// <summary>
    /// Determina si la pieza debe mostrarse en el momento indicado. La vigencia
    /// se evalúa en el servidor y no en el navegador: si se filtrara en el
    /// cliente, el contenido caducado igual habría viajado.
    /// </summary>
    public bool EstaVigente(DateTime ahoraUtc) =>
        Publicado
        && (VigenteDesde is null || VigenteDesde <= ahoraUtc)
        && (VigenteHasta is null || VigenteHasta >= ahoraUtc);
}
