using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Interfaces.Storage;

/// <summary>
/// El almacén en disco imitando al de S3 para desarrollo (feature 011, research R16). Con
/// <c>AttachmentStorage:Provider = Local</c>, las autorizaciones que firma <see cref="IBlobStore"/>
/// apuntan a rutas de la propia API en lugar de a S3, y esas rutas llegan aquí.
///
/// <para>
/// Hace lo mismo que la política de S3: el token dice qué clave, qué tipo, qué tamaño y qué huella
/// se autorizaron y hasta cuándo, y lo que no coincida se rechaza. Así el cliente usa exactamente el
/// mismo flujo en local que en los ambientes. Las rutas no deciden nada: reenvían a un comando y a una
/// consulta de Application, que llaman aquí (Principios II y III).
/// </para>
/// </summary>
public interface IAlmacenLocal
{
    /// <summary>
    /// Recibe la subida de un formulario como el de S3: los campos, en el orden en que llegaron, y el
    /// archivo. Falla si el token no es válido o venció, o si algo no coincide con lo autorizado.
    /// </summary>
    Task<Result> RecibirAsync(string token, IReadOnlyDictionary<string, string> campos, Stream archivo, CancellationToken ct);

    /// <summary>Entrega un archivo con el nombre y el tipo que firmó el enlace de descarga.</summary>
    Task<Result<ArchivoLocal>> LeerAsync(string token, CancellationToken ct);
}

/// <summary>El contenido es propiedad de quien lo recibe, que debe disponerlo.</summary>
public sealed record ArchivoLocal(Stream Contenido, string NombreDeArchivo, string ContentType);
