using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Http;

/// <summary>
/// La clave de idempotencia de una operación de pantalla, del lado del cliente (feature 012,
/// decisiones-transversales T13, T057; contracts/api.md §2.3). Una pantalla crea una al <b>iniciar</b> la
/// operación —abrir el diálogo, cargar el borrador— y la manda en cada envío con <see cref="Aplicar"/>:
///
/// <list type="bullet">
/// <item><b>Se conserva</b> en todo reintento con el mismo contenido (doble clic, red caída, 401 renovado:
/// <c>RenovacionDeSesionHandler</c> clona las cabeceras), así el servidor ejecuta una sola vez y devuelve el
/// mismo resultado con <c>Idempotent-Replayed: true</c>.</item>
/// <item><b>Se renueva</b> sólo tras un éxito (<see cref="Exito"/>: la siguiente es otra operación) o si el
/// contenido cambió desde el último envío (reintentar con otro contenido y la misma clave sería
/// <c>Operation.KeyReused</c>).</item>
/// </list>
///
/// <para>Revisar una importación y aplicarla son dos operaciones: dos instancias.</para>
/// </summary>
public sealed class ClaveDeOperacion
{
    public const string Cabecera = "Idempotency-Key";
    public const string CabeceraDeRepeticion = "Idempotent-Replayed";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? _huellaDelUltimoEnvio;

    /// <summary>Empieza una operación con una clave nueva.</summary>
    public ClaveDeOperacion() => Valor = Guid.NewGuid();

    /// <summary>La clave vigente.</summary>
    public Guid Valor { get; private set; }

    /// <summary>
    /// La clave para enviar <paramref name="contenido"/>: la misma si es el primer envío o el contenido no
    /// cambió desde el anterior; una nueva si cambió.
    /// </summary>
    public Guid Para(object? contenido)
    {
        var huella = Huella(contenido);
        if (_huellaDelUltimoEnvio is not null && _huellaDelUltimoEnvio != huella)
            Valor = Guid.NewGuid();
        _huellaDelUltimoEnvio = huella;
        return Valor;
    }

    /// <summary>La operación salió bien: la próxima es otra y lleva otra clave.</summary>
    public void Exito()
    {
        Valor = Guid.NewGuid();
        _huellaDelUltimoEnvio = null;
    }

    /// <summary>Pone <c>Idempotency-Key</c> en la petición con la clave para su contenido.</summary>
    public HttpRequestMessage Aplicar(HttpRequestMessage peticion, object? contenido)
    {
        peticion.Headers.Remove(Cabecera);
        peticion.Headers.TryAddWithoutValidation(Cabecera, Para(contenido).ToString());
        return peticion;
    }

    /// <summary>La respuesta fue la guardada de un envío anterior con la misma clave.</summary>
    public static bool FueRepeticion(HttpResponseMessage respuesta) =>
        respuesta.Headers.TryGetValues(CabeceraDeRepeticion, out var valores)
        && valores.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

    private static string Huella(object? contenido)
    {
        var json = contenido is null ? "null" : JsonSerializer.Serialize(contenido, contenido.GetType(), Json);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
