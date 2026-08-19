namespace IngenIA365ERP.Persistence.Configuration;

/// <summary>
/// Dónde vive cada servicio de infraestructura en ESTA máquina.
///
/// <para>
/// Separa dos preguntas que antes estaban mezcladas en una sola cadena de
/// conexión: <b>qué motor</b> se usa —eso lo sigue decidiendo
/// <c>Database:Provider</c>— y <b>dónde corre</b>. Son independientes: se puede
/// usar PostgreSQL instalado en el sistema, PostgreSQL en un contenedor o
/// PostgreSQL dentro de WSL sin tocar una línea de código.
/// </para>
///
/// <para>
/// La elección es POR SERVICIO a propósito, no global. El caso que la motivó es
/// real: en Windows conviene la base y el correo instalados en el sistema, pero
/// Redis no tiene build oficial para Windows y se termina levantando en WSL o
/// en un contenedor. Un interruptor único obligaría a mover todo junto.
/// </para>
/// </summary>
public sealed class InfraestructuraOptions
{
    public const string SectionName = "Infraestructura";

    /// <summary>
    /// Servicio → destino elegido. Un servicio que no figure acá se deja como
    /// esté configurado por los medios de siempre; así un ambiente que no use
    /// esta sección (producción, que resuelve todo por variables de entorno)
    /// no cambia de comportamiento.
    /// </summary>
    public Dictionary<string, string> Destinos { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DestinoBaseDatos> PostgreSQL { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DestinoBaseDatos> SqlServer { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Redis { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> MongoDB { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DestinoSmtp> Smtp { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Una base necesita dos cadenas: el schema operativo de cada cooperativa y
    /// la base Admin, que está separada por el principio IV.
    /// </summary>
    public sealed class DestinoBaseDatos
    {
        public string? Operativa { get; set; }
        public string? Admin { get; set; }
    }

    public sealed class DestinoSmtp
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
    }
}

/// <summary>
/// Nombres de destino reconocidos. Se declaran para que un error de tipeo
/// —"docker" con mayúscula distinta da igual, pero "Contenedor" no— se detecte
/// al arrancar y no en la primera consulta.
/// </summary>
public static class DestinosInfraestructura
{
    /// <summary>Instalado en el sistema operativo (servicio de Windows, systemd, brew).</summary>
    public const string Local = "Local";

    /// <summary>Contenedor de Docker, normalmente publicado en un puerto de localhost.</summary>
    public const string Docker = "Docker";

    /// <summary>
    /// Dentro de WSL. Es destino propio y no un alias de Docker porque el punto
    /// de acceso puede diferir: WSL2 suele responder en localhost, pero cuando
    /// el servicio escucha sólo en la interfaz de WSL hay que apuntar a la IP
    /// del adaptador vEthernet, que además cambia al reiniciar.
    /// </summary>
    public const string Wsl = "Wsl";

    public static readonly IReadOnlySet<string> Conocidos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Local, Docker, Wsl };
}
