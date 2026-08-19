using Microsoft.Extensions.Configuration;

namespace IngenIA365ERP.Persistence.Configuration;

/// <summary>
/// Resuelve la sección <c>Infraestructura</c> y la PROYECTA sobre las claves de
/// siempre (<c>Database:ConnectionStrings</c>, <c>ConnectionStrings:Redis</c>,
/// <c>Smtp</c>…).
///
/// <para>
/// Proyectar en vez de cambiar a los consumidores es deliberado: ni Caching, ni
/// Audit, ni Storage, ni Persistence se enteran de que esto existe. Siguen
/// leyendo la misma clave que leían. Eso evita tocar cinco proyectos y, sobre
/// todo, evita que un ambiente que NO use la sección —producción, donde todo
/// llega por variables de entorno— cambie de comportamiento: sin sección, esto
/// no hace nada.
/// </para>
/// </summary>
public static class InfraestructuraConfiguracion
{
    /// <summary>
    /// Agrega los archivos por sistema operativo y el de la máquina.
    ///
    /// <para>
    /// El orden es el que manda: lo último agregado gana. Por eso hay que
    /// re-agregar las variables de entorno DESPUÉS de llamar a esto, o un
    /// archivo del repositorio terminaría pisando lo que inyecta Kubernetes.
    /// </para>
    /// </summary>
    /// <param name="nombreAmbiente">Development, Staging, Production…</param>
    public static IConfigurationBuilder AgregarInfraestructuraDeLaMaquina(
        this IConfigurationBuilder builder, string nombreAmbiente)
    {
        var so = SistemaOperativoActual();

        // appsettings.Development.Windows.json — versionado: son las opciones
        // razonables por defecto para quien trabaje en ese sistema.
        builder.AddJsonFile($"appsettings.{nombreAmbiente}.{so}.json",
            optional: true, reloadOnChange: true);

        // appsettings.Development.local.json — NO versionado: cada máquina
        // ajusta lo suyo (la IP de WSL, un puerto ocupado) sin pelearse con el
        // resto del equipo por el mismo archivo.
        builder.AddJsonFile($"appsettings.{nombreAmbiente}.local.json",
            optional: true, reloadOnChange: true);

        return builder;
    }

    /// <summary>
    /// Nombre del sistema operativo tal como aparece en el nombre de archivo.
    /// </summary>
    public static string SistemaOperativoActual() =>
        OperatingSystem.IsWindows() ? "Windows"
        : OperatingSystem.IsMacOS() ? "macOS"
        : OperatingSystem.IsLinux() ? "Linux"
        : "Desconocido";

    /// <summary>
    /// Traduce los destinos elegidos a las claves que leen los consumidores.
    /// Devuelve los pares a inyectar; el llamador los agrega como fuente en
    /// memoria y vuelve a poner las variables de entorno encima.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si un servicio elige un destino que no está definido. Se falla al
    /// arrancar y no en la primera consulta: un destino mal escrito que
    /// degradara en silencio dejaría la aplicación apuntando a la base
    /// equivocada, que es peor que no arrancar (principio IX).
    /// </exception>
    public static Dictionary<string, string?> Resolver(IConfiguration configuracion)
    {
        var resueltos = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var seccion = configuracion.GetSection(InfraestructuraOptions.SectionName);
        if (!seccion.Exists())
            return resueltos;

        var opciones = seccion.Get<InfraestructuraOptions>() ?? new InfraestructuraOptions();

        foreach (var (servicio, destino) in opciones.Destinos)
        {
            if (string.IsNullOrWhiteSpace(destino))
                continue;

            switch (servicio.ToLowerInvariant())
            {
                case "postgresql":
                    ProyectarBase(resueltos, opciones.PostgreSQL, servicio, destino, "PostgreSQL");
                    break;
                case "sqlserver":
                    ProyectarBase(resueltos, opciones.SqlServer, servicio, destino, "SqlServer");
                    break;
                case "redis":
                    resueltos["ConnectionStrings:Redis"] =
                        Exigir(opciones.Redis, servicio, destino);
                    break;
                case "mongodb":
                    resueltos["ConnectionStrings:MongoDB"] =
                        Exigir(opciones.MongoDB, servicio, destino);
                    break;
                case "smtp":
                    var smtp = Exigir(opciones.Smtp, servicio, destino);
                    if (!string.IsNullOrWhiteSpace(smtp.Host))
                        resueltos["Smtp:Host"] = smtp.Host;
                    if (smtp.Port is { } puerto)
                        resueltos["Smtp:Port"] = puerto.ToString();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Infraestructura:Destinos define '{servicio}', que no es un servicio " +
                        "conocido. Servicios válidos: PostgreSQL, SqlServer, Redis, MongoDB, Smtp.");
            }
        }

        return resueltos;
    }

    private static void ProyectarBase(
        Dictionary<string, string?> resueltos,
        Dictionary<string, InfraestructuraOptions.DestinoBaseDatos> catalogo,
        string servicio, string destino, string claveMotor)
    {
        var elegido = Exigir(catalogo, servicio, destino);

        if (string.IsNullOrWhiteSpace(elegido.Operativa) || string.IsNullOrWhiteSpace(elegido.Admin))
        {
            throw new InvalidOperationException(
                $"Infraestructura:{servicio}:{destino} debe declarar 'Operativa' y 'Admin'. " +
                "Son dos bases distintas: la operativa lleva el schema de cada cooperativa y " +
                "la Admin el catálogo SaaS, y el principio IV exige que estén separadas.");
        }

        resueltos[$"Database:ConnectionStrings:{claveMotor}"] = elegido.Operativa;
        resueltos[$"Database:AdminConnectionStrings:{claveMotor}"] = elegido.Admin;
    }

    private static T Exigir<T>(Dictionary<string, T> catalogo, string servicio, string destino)
    {
        if (catalogo.TryGetValue(destino, out var valor) && valor is not null)
            return valor;

        var disponibles = catalogo.Count == 0
            ? "ninguno — falta declarar la sección"
            : string.Join(", ", catalogo.Keys.OrderBy(k => k));

        var pista = DestinosInfraestructura.Conocidos.Contains(destino)
            ? $"El destino '{destino}' es válido pero no está definido para {servicio}."
            : $"'{destino}' no es un destino conocido. Los habituales son " +
              $"{DestinosInfraestructura.Local}, {DestinosInfraestructura.Docker} y " +
              $"{DestinosInfraestructura.Wsl}.";

        throw new InvalidOperationException(
            $"Infraestructura:Destinos:{servicio} apunta a '{destino}'. {pista} " +
            $"Definidos para {servicio}: {disponibles}. " +
            $"Revisá appsettings.<Ambiente>.{SistemaOperativoActual()}.json " +
            "o el .local.json de esta máquina.");
    }

    /// <summary>
    /// Resumen de una línea por servicio, para el arranque. Sirve para no tener
    /// que adivinar contra qué está corriendo: fue justamente la pregunta
    /// "¿esto es el PostgreSQL local o el del contenedor?" la que originó todo
    /// esto, y con tres motores encendidos a la vez no es obvia.
    /// </summary>
    public static string Describir(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(InfraestructuraOptions.SectionName);
        if (!seccion.Exists())
            return "Infraestructura: sin sección — se usan las cadenas de conexión tal cual.";

        var opciones = seccion.Get<InfraestructuraOptions>() ?? new InfraestructuraOptions();
        if (opciones.Destinos.Count == 0)
            return $"Infraestructura ({SistemaOperativoActual()}): sin destinos elegidos.";

        var partes = opciones.Destinos
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}");

        return $"Infraestructura ({SistemaOperativoActual()}): {string.Join(" · ", partes)}";
    }
}
