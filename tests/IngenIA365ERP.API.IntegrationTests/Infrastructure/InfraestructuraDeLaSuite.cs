using System.Data.Common;
using System.Runtime.CompilerServices;
using IngenIA365ERP.Persistence.Configuration;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.IntegrationTests.Infrastructure;

/// <summary>
/// Esta suite trae su propia infraestructura y lo declara.
///
/// <para>
/// <b>Qué arregla.</b> Cada fixture levanta sus contenedores y le pasa las
/// cadenas al host con <c>UseSetting</c>. Pero el arranque de la aplicación
/// proyecta la sección <c>Infraestructura</c> —los destinos de la máquina— como
/// una fuente en memoria posterior, y esa proyección pisaba las cadenas del
/// host. No fallaba nada: la suite creía escribir en un PostgreSQL efímero y
/// escribía en la base de desarrollo de quien la ejecutara. Dejó seis
/// cooperativas, siete usuarios, nueve invitaciones y seis ranuras de Redis
/// quemadas antes de que alguien lo notara, y sólo se notó por la basura.
/// </para>
///
/// <para>
/// La asimetría que lo delató: las claves de Mongo <b>no</b> se proyectan, así
/// que el prefijo del fixture (<c>IngenIA365ERP_Audit_Test</c>) sí sobrevivía.
/// La auditoría de esas cooperativas acabó en el contenedor efímero y sus datos
/// en la base real.
/// </para>
/// </summary>
internal static class InfraestructuraDeLaSuite
{
    /// <summary>
    /// Corre una vez, antes que cualquier prueba del ensamblado, y antes de que
    /// ninguna fixture construya su host. Que sea un inicializador de módulo y
    /// no una línea en cada fixture es deliberado: una fixture nueva que se
    /// olvidara de ponerlo volvería a escribir en la base de desarrollo, y otra
    /// vez sin dar error.
    /// </summary>
    [ModuleInitializer]
    internal static void Declarar() =>
        Environment.SetEnvironmentVariable(
            InfraestructuraConfiguracion.VariableInfraestructuraPropia, "1");

    /// <summary>
    /// Comprueba que el host levantado habla con el contenedor y no con otra
    /// cosa. Es la red de seguridad de lo anterior.
    ///
    /// <para>
    /// Se ejecuta después de arrancar el host y antes de la primera escritura.
    /// Si algún día la precedencia de configuración vuelve a cambiar, esto falla
    /// en la primera prueba con un mensaje que dice qué pasó, en vez de sembrar
    /// una base ajena en silencio. Lo silencioso fue el problema entero.
    /// </para>
    /// </summary>
    /// <param name="servicios">Servicios del host ya construido.</param>
    /// <param name="delContenedor">Cadena del contenedor que esta fixture levantó.</param>
    public static void ExigirQueElHostHableConElContenedor(
        IServiceProvider servicios, string delContenedor)
    {
        using var alcance = servicios.CreateScope();
        var opciones = alcance.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
        var efectiva = opciones.Value.GetActiveConnectionString();

        var esperadas = Coordenadas(delContenedor);
        var reales = Coordenadas(efectiva);

        if (string.Equals(esperadas, reales, StringComparison.OrdinalIgnoreCase)) return;

        throw new InvalidOperationException(
            $"[Pruebas.InfraestructuraFugada] El host de pruebas resolvió {reales} y su " +
            $"contenedor está en {esperadas}. La suite estaría escribiendo en una base que " +
            "no es suya — que es exactamente el fallo que dejó seis cooperativas de prueba en " +
            "la base de desarrollo. Se aborta antes de escribir nada. Revisá que " +
            $"{InfraestructuraConfiguracion.VariableInfraestructuraPropia} siga apagando la " +
            "proyección de Infraestructura en InfraestructuraConfiguracion.Resolver.");
    }

    /// <summary>
    /// Servidor y puerto, nada más: identifica CON QUÉ se está hablando sin
    /// tocar usuario ni contraseña, que no tienen por qué acabar en un mensaje
    /// de error. Sirve igual para Npgsql (Host + Port) que para SQL Server
    /// (Server=host,puerto), porque las dos cadenas se leen con el mismo parser.
    /// </summary>
    private static string Coordenadas(string cadena)
    {
        var partes = new DbConnectionStringBuilder { ConnectionString = cadena };

        string? Leer(params string[] claves)
        {
            foreach (var clave in claves)
                if (partes.TryGetValue(clave, out var valor))
                    return valor?.ToString();
            return null;
        }

        var servidor = Leer("Host", "Server", "Data Source", "Address", "Addr") ?? "(sin servidor)";
        var puerto = Leer("Port");

        return puerto is null ? servidor : $"{servidor}:{puerto}";
    }
}
