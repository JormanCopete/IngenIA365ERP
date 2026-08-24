using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Crea, migra y siembra la base de datos de una cooperativa.
///
/// <para>
/// Convive con <see cref="TenantSchemaService"/>, que sigue haciendo lo mismo por
/// esquema. Están los dos a la vez a propósito: este se puede probar contra un
/// PostgreSQL real antes de que nada dependa de él, y el sistema sigue en pie
/// mientras tanto.
/// </para>
///
/// <para>
/// <b>Lo que desaparece respecto al modelo por esquema.</b> El aprovisionador
/// viejo generaba el script idempotente de migraciones y lo reescribía buscando
/// la cadena literal <c>dbo</c> para sustituirla por el esquema destino. De ahí
/// salía el fallo más peligroso del diseño anterior: si el contexto ambiente ya
/// apuntaba a otra cooperativa, el script salía calificado con ESA, no había nada
/// que traducir, y las 289 tablas se creaban dentro de la cooperativa
/// equivocada — sin excepción y sin registro. Aquí las migraciones se aplican con
/// EF normal contra la base destino. No hay nada que reescribir.
/// </para>
/// </summary>
internal sealed class TenantDatabaseProvisioner(
    IOptions<DatabaseOptions> opciones,
    IDbProviderConfigurator configurador,
    IEnumerable<IDataSeeder> seeders,
    IHostEnvironment entorno,
    ILogger<TenantDatabaseProvisioner> logger) : ITenantDatabaseProvisioner
{
    public async Task<ResultadoAprovisionamiento> AprovisionarAsync(
        string nombreDeBase, string identificador, string? cadenaPropia, CancellationToken ct)
    {
        ValidarNombre(nombreDeBase);

        var cadena = string.IsNullOrWhiteSpace(cadenaPropia)
            ? ConnectionStringTargeting.ConCatalogo(opciones.Value.GetActiveConnectionString(), nombreDeBase)
            : cadenaPropia;

        var creada = await CrearSiFaltaAsync(cadena, nombreDeBase, ct);

        await using var db = AbrirContexto(cadena);
        await db.Database.MigrateAsync(ct);

        var filas = await SembrarAsync(db, identificador, ct);
        var ultima = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();

        logger.LogInformation(
            "Cooperativa {Identificador}: base {Base} {Estado}, migrada hasta {Migracion}, " +
            "{Filas} fila(s) sembradas.",
            identificador, nombreDeBase, creada ? "creada" : "ya existia", ultima, filas);

        return new ResultadoAprovisionamiento(creada, ultima, filas);
    }

    /// <summary>
    /// <c>CREATE DATABASE</c> no admite parámetros ni corre dentro de una
    /// transacción, así que el nombre se valida a mano antes de interpolarlo. La
    /// lista blanca es deliberadamente estrecha.
    /// </summary>
    private static void ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 63)
        {
            throw new ArgumentException(
                "El nombre de la base es obligatorio y no puede pasar de 63 caracteres.", nameof(nombre));
        }

        if (!nombre.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException(
                $"Nombre de base no admitido: '{nombre}'. Solo letras ASCII, digitos y guion bajo. " +
                "CREATE DATABASE no acepta parametros, asi que el nombre se interpola y no puede " +
                "venir de texto libre.", nameof(nombre));
        }

        if (char.IsAsciiDigit(nombre[0]))
        {
            throw new ArgumentException(
                $"Nombre de base no admitido: '{nombre}'. No puede empezar por un digito.", nameof(nombre));
        }
    }

    private async Task<bool> CrearSiFaltaAsync(string cadena, string nombreDeBase, CancellationToken ct)
    {
        // Contra la base de sistema: no se puede crear una base desde dentro de
        // ella misma, y CREATE DATABASE tampoco corre dentro de una transaccion.
        var aNivelDeServidor = ConnectionStringTargeting.ANivelDeServidor(cadena, configurador.Provider);

        await using var conexion = configurador.CreateConnection(aNivelDeServidor);
        await conexion.OpenAsync(ct);

        await using (var consulta = conexion.CreateCommand())
        {
            // No hay CREATE DATABASE IF NOT EXISTS en PostgreSQL, asi que se
            // comprueba antes. El catalogo se llama distinto en cada motor.
            consulta.CommandText = configurador.Provider == DatabaseProvider.PostgreSql
                ? "select 1 from pg_database where datname = @nombre"
                : "select 1 from sys.databases where name = @nombre";
            var parametro = consulta.CreateParameter();
            parametro.ParameterName = "@nombre";
            parametro.Value = nombreDeBase;
            consulta.Parameters.Add(parametro);

            if (await consulta.ExecuteScalarAsync(ct) is not null) return false;
        }

        await using var creacion = conexion.CreateCommand();
        creacion.CommandText = "CREATE DATABASE \"" + nombreDeBase + "\"";
        creacion.CommandTimeout = 120;
        await creacion.ExecuteNonQueryAsync(ct);
        return true;
    }

    private ApplicationDbContext AbrirContexto(string cadena)
    {
        var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
        configurador.Configure(constructor, cadena, MigrationsTarget.Application);
        return new ApplicationDbContext(constructor.Options);
    }

    /// <summary>
    /// Corre los seeders paramétricos de alcance cooperativa contra la base recién
    /// creada. Se le pasa un tenant no nulo a propósito: los seeders lo usan para
    /// distinguir la base de una cooperativa de la plantilla, y así no se siembra
    /// ahí el usuario administrador heredado.
    /// </summary>
    private async Task<int> SembrarAsync(
        ApplicationDbContext db, string identificador, CancellationToken ct)
    {
        var contexto = new SeedContext
        {
            TenantDb = db,
            Tenant = new ErpTenantInfo { Identifier = identificador, SchemaName = "dbo" },
            EnvironmentName = entorno.EnvironmentName,
            Logger = logger,
        };

        var total = 0;
        foreach (var seeder in seeders
            .Where(s => s.Scope == SeedScope.Tenant && s.Category == SeedCategory.Parametric)
            .OrderBy(s => s.Order))
        {
            total += await seeder.SeedAsync(contexto, ct);
        }

        return total;
    }
}
