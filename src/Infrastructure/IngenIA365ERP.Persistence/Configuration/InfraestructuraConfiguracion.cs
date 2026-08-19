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
    /// Agrega el archivo de configuración propio de esta máquina.
    ///
    /// <para>
    /// Es UN solo archivo, no uno por sistema operativo: la elección no depende
    /// de la familia del sistema sino de cómo tenga montado cada quien su
    /// equipo. Dos personas con Windows pueden querer cosas distintas, y la
    /// misma persona cambia de idea al cambiar de máquina.
    /// </para>
    ///
    /// <para>
    /// El orden es el que manda: lo último agregado gana. Por eso hay que
    /// re-agregar las variables de entorno DESPUÉS de llamar a esto, o un
    /// archivo local terminaría pisando lo que inyecta el despliegue.
    /// </para>
    /// </summary>
    /// <param name="nombreAmbiente">Development, QA, Production…</param>
    public static IConfigurationBuilder AgregarInfraestructuraDeLaMaquina(
        this IConfigurationBuilder builder, string nombreAmbiente)
    {
        // appsettings.Development.local.json — NO versionado: cada máquina
        // ajusta lo suyo (la IP de WSL, un puerto ocupado, una instancia con
        // otro usuario) sin pelearse con el resto del equipo por el mismo
        // archivo.
        builder.AddJsonFile($"appsettings.{nombreAmbiente}.local.json",
            optional: true, reloadOnChange: true);

        return builder;
    }

    /// <summary>
    /// Sistema operativo, sólo para el resumen de arranque. Ya no elige
    /// archivo: la configuración por máquina va en un único .local.json.
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

        // Dentro de un contenedor esta sección no significa nada: "Local",
        // "Docker" y "Wsl" son categorías de una máquina de desarrollo. La
        // configuración real la inyecta el despliegue por variables.
        //
        // La guardia es por contenedor y NO por nombre de ambiente a propósito:
        // la VPS de DEV corre con ASPNETCORE_ENVIRONMENT=Development, o sea que
        // lee el MISMO appsettings.Development.json que un portátil. Confiar en
        // el nombre del ambiente dejaría a ese pod resolviendo contra
        // localhost; confiar en que las variables de entorno ganen por orden
        // funciona, pero se rompe el día que alguien olvide una. Esto no
        // depende de ninguna de las dos cosas.
        if (EstaEnContenedor())
            return resueltos;

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

    /// <summary>
    /// Las imágenes base de .NET fijan DOTNET_RUNNING_IN_CONTAINER=true. Se
    /// acepta también la variante ASPNETCORE_ por si la imagen es más vieja.
    /// </summary>
    public static bool EstaEnContenedor() =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_RUNNING_IN_CONTAINER"),
            "true", StringComparison.OrdinalIgnoreCase);

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
            "Revisá la sección Infraestructura de appsettings.<Ambiente>.json " +
            "o el appsettings.<Ambiente>.local.json de esta máquina.");
    }

    /// <summary>
    /// Resumen de una línea por servicio, para el arranque. Sirve para no tener
    /// que adivinar contra qué está corriendo: fue justamente la pregunta
    /// "¿esto es el PostgreSQL local o el del contenedor?" la que originó todo
    /// esto, y con tres motores encendidos a la vez no es obvia.
    /// </summary>
    public static string Describir(IConfiguration configuracion)
    {
        if (EstaEnContenedor())
            return "Infraestructura: en contenedor — la configuración llega por variables de entorno.";

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
