using System.Data.Common;

namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// Apunta una cadena de conexión a otra base, sin tocar nada más de ella.
///
/// <para>
/// Es una operación pequeña y con una trampa: la clave del catálogo se llama
/// <c>Database</c> en PostgreSQL e <c>Initial Catalog</c> en SQL Server. Estaba
/// escrita dos veces —<c>DatabaseOptions.DeriveAdminConnectionString</c> y
/// <c>DatabaseInitializerHostedService.ToServerLevelConnectionString</c>—, en
/// capas distintas y sin saber la una de la otra.
/// </para>
///
/// <para>
/// Duplicarla importaba poco mientras sólo había dos bases. Con una base por
/// cooperativa, componer cadenas pasa a ser la operación central del
/// aislamiento: cada llamada decide contra qué datos va a hablar la aplicación.
/// Que exista un único sitio donde se hace, y que ese sitio esté probado, es la
/// diferencia entre una regla y una costumbre.
/// </para>
///
/// <para>
/// Nada de aquí registra ni devuelve la cadena completa en mensajes de error:
/// lleva credenciales.
/// </para>
/// </summary>
public static class ConnectionStringTargeting
{
    /// <summary>Nombre de la clave del catálogo, según el proveedor. Null si la cadena no lo declara.</summary>
    private static string? ClaveDeCatalogo(DbConnectionStringBuilder constructor) =>
        constructor.ContainsKey("Database") ? "Database"
        : constructor.ContainsKey("Initial Catalog") ? "Initial Catalog"
        : null;

    /// <summary>
    /// La misma cadena apuntando a la base indicada. Lanza si la original no
    /// declara catálogo: adivinarlo sería peor que fallar.
    /// </summary>
    public static string ConCatalogo(string cadena, string nombreDeBase)
    {
        if (string.IsNullOrWhiteSpace(nombreDeBase))
            throw new ArgumentException(
                "Hace falta el nombre de la base destino.", nameof(nombreDeBase));

        var constructor = new DbConnectionStringBuilder { ConnectionString = cadena };
        var clave = ClaveDeCatalogo(constructor)
            ?? throw new InvalidOperationException(
                "[Database.ConnectionStringMissing] La cadena de conexión no declara " +
                "'Database' ni 'Initial Catalog', así que no se puede apuntar a otra base.");

        constructor[clave] = nombreDeBase;
        return constructor.ConnectionString;
    }

    /// <summary>
    /// El nombre de la base que declara la cadena, o null si no declara ninguna.
    /// </summary>
    public static string? CatalogoDe(string cadena)
    {
        var constructor = new DbConnectionStringBuilder { ConnectionString = cadena };
        var clave = ClaveDeCatalogo(constructor);
        return clave is null ? null : constructor[clave]?.ToString();
    }

    /// <summary>
    /// La misma cadena apuntando a la base de sistema del motor (<c>postgres</c>
    /// o <c>master</c>). Hace falta para las operaciones que no se pueden
    /// ejecutar desde dentro de la base afectada: crearla, soltarla, o tomar un
    /// bloqueo de arranque.
    /// </summary>
    public static string ANivelDeServidor(string cadena, DatabaseProvider proveedor)
    {
        var constructor = new DbConnectionStringBuilder { ConnectionString = cadena };
        var clave = ClaveDeCatalogo(constructor);

        // A diferencia de ConCatalogo, aquí no se lanza: una cadena sin catálogo
        // ya apunta al servidor, que es justo lo que se pedía.
        if (clave is not null)
        {
            constructor[clave] = proveedor == DatabaseProvider.PostgreSql ? "postgres" : "master";
        }

        return constructor.ConnectionString;
    }
}
