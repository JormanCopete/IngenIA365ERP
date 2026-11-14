using System.Diagnostics;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IngenIA365ERP.API.IntegrationTests.Infrastructure;

/// <summary>
/// En PostgreSQL cada cooperativa de prueba nace <b>copiando una base plantilla ya migrada</b>
/// (<c>CREATE DATABASE … TEMPLATE</c>) en vez de aplicar las migraciones desde cero.
///
/// <para>
/// <b>Por qué.</b> Medido el 2026-11-12: la suite e2e completa en PostgreSQL tardaba 2–2,5 h con la
/// CPU casi ociosa. El tiempo se iba en migrar cada base de cooperativa —todas las migraciones EF,
/// unos 20 s por base— y una corrida crea del orden de 160 cooperativas
/// (<c>CooperativaAisladaAsync</c> de cada módulo y <c>/api/saas/tenants/with-admin</c>). Copiar la
/// plantilla, que está vacía, no cuesta casi nada: la colección <c>Inventory</c> (73 pruebas, una
/// cooperativa aislada por escenario) bajó de 11 min 12 s a 4 min 4 s el 2026-11-14, con los mismos
/// resultados.
/// </para>
///
/// <para>
/// <b>Qué no cambia.</b> El aprovisionador real corre entero después de la copia: ve que la base
/// existe, migra —no encuentra nada pendiente— y siembra la cooperativa con su identificador, igual
/// que antes. La plantilla la migra ese mismo aprovisionador, sin orquestador (o sea, sin semilla), así
/// que no hay un segundo camino de migración que pueda apartarse del de producción. Sólo vive en este
/// proyecto: el código de producción no sabe que existe.
/// </para>
///
/// <para>
/// SQL Server no tiene un <c>CREATE DATABASE … TEMPLATE</c> equivalente y sigue migrando cada base
/// (la fixture sólo registra esto con PostgreSQL). Una cooperativa con cadena propia vive en otro
/// servidor, donde la plantilla no está, y también pasa de largo.
/// </para>
/// </summary>
internal sealed class AprovisionadorDesdePlantilla(
    TenantDatabaseProvisioner real,
    PlantillaDeCooperativa plantilla) : ITenantDatabaseProvisioner
{
    /// <summary>Sustituye el registro de <see cref="ITenantDatabaseProvisioner"/> por este decorador.</summary>
    public static void Registrar(IServiceCollection services)
    {
        services.RemoveAll<ITenantDatabaseProvisioner>();
        services.AddScoped<TenantDatabaseProvisioner>();
        services.AddSingleton<PlantillaDeCooperativa>();
        services.AddScoped<ITenantDatabaseProvisioner, AprovisionadorDesdePlantilla>();
    }

    public async Task<ResultadoAprovisionamiento> AprovisionarAsync(
        string nombreDeBase, string identificador, string? cadenaPropia, CancellationToken ct)
    {
        var copiada = string.IsNullOrWhiteSpace(cadenaPropia)
            && await plantilla.CopiarSiFaltaAsync(nombreDeBase, ct);

        var resultado = await real.AprovisionarAsync(nombreDeBase, identificador, cadenaPropia, ct);

        // El real encuentra la base ya copiada y responde «ya existía». Para quien llamó la base sí
        // nació ahora, y el contrato (BaseCreada) se lo tiene que decir igual que sin plantilla.
        return copiada ? resultado with { BaseCreada = true } : resultado;
    }
}

/// <summary>
/// La base plantilla de un host de pruebas: una por fixture —cada fixture tiene su contenedor—, migrada
/// la primera vez que hace falta y copiada después por cada cooperativa.
/// </summary>
internal sealed class PlantillaDeCooperativa(
    IOptions<DatabaseOptions> opciones,
    ILogger<PlantillaDeCooperativa> logger)
{
    public const string Nombre = "plantilla_cooperativa_e2e";

    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private volatile bool _lista;

    private string CadenaDeServidor =>
        ConnectionStringTargeting.ANivelDeServidor(opciones.Value.GetActiveConnectionString(), DatabaseProvider.PostgreSql);

    /// <summary>
    /// Crea <paramref name="nombreDeBase"/> como copia de la plantilla si todavía no existe. Devuelve
    /// <c>false</c> si ya existía o si el nombre no es de los que se pueden interpolar: en ese caso decide
    /// el aprovisionador real, que lo rechaza con su propio mensaje.
    /// </summary>
    public async Task<bool> CopiarSiFaltaAsync(string nombreDeBase, CancellationToken ct)
    {
        if (!NombreAdmitido(nombreDeBase) || nombreDeBase == Nombre) return false;

        await using var servidor = new NpgsqlConnection(CadenaDeServidor);
        await servidor.OpenAsync(ct);
        if (await ExisteAsync(servidor, nombreDeBase, ct)) return false;

        await AsegurarPlantillaAsync(ct);

        var reloj = Stopwatch.StartNew();
        try
        {
            // CREATE DATABASE no admite parámetros: el nombre pasó la lista blanca de arriba.
            await EjecutarAsync(servidor, $"CREATE DATABASE \"{nombreDeBase}\" TEMPLATE \"{Nombre}\"", ct);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // Otro llamador la creó entre la consulta y la copia: aprovisionar es idempotente.
            return false;
        }

        logger.LogInformation("Base {Base} copiada de {Plantilla} en {Ms} ms.", nombreDeBase, Nombre, reloj.ElapsedMilliseconds);
        return true;
    }

    private async Task AsegurarPlantillaAsync(CancellationToken ct)
    {
        if (_lista) return;
        await _cerrojo.WaitAsync(ct);
        try
        {
            if (_lista) return;
            var reloj = Stopwatch.StartNew();

            // Sin pool: al terminar de migrar no puede quedar ninguna conexión ociosa contra la plantilla,
            // porque PostgreSQL se niega a copiar una base con alguien conectado («source database is being
            // accessed by other users»). Con pool, Npgsql dejaría abiertas las que usó la migración.
            var cadena = new NpgsqlConnectionStringBuilder(
                ConnectionStringTargeting.ConCatalogo(opciones.Value.GetActiveConnectionString(), Nombre))
            {
                Pooling = false,
            }.ConnectionString;

            // El mismo aprovisionador de producción, sin orquestador: crea y migra, no siembra. La semilla
            // lleva el identificador de cada cooperativa y la pone el aprovisionador real después de copiar.
            var migrador = new TenantDatabaseProvisioner(
                opciones, new PostgreSqlProviderConfigurator(), orquestador: null,
                NullLogger<TenantDatabaseProvisioner>.Instance);
            var resultado = await migrador.AprovisionarAsync(Nombre, Nombre, cadena, ct);

            // Cerrada a conexiones nuevas y sin ninguna viva: así ninguna copia puede tropezar con alguien
            // conectado. ALLOW_CONNECTIONS false no impide copiarla (template0 está igual).
            await using var servidor = new NpgsqlConnection(CadenaDeServidor);
            await servidor.OpenAsync(ct);
            await EjecutarAsync(servidor, $"ALTER DATABASE \"{Nombre}\" WITH IS_TEMPLATE true ALLOW_CONNECTIONS false", ct);
            await EjecutarAsync(servidor,
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{Nombre}' AND pid <> pg_backend_pid()", ct);

            _lista = true;
            logger.LogInformation(
                "Plantilla {Plantilla} migrada hasta {Migracion} en {Ms} ms; las cooperativas de este host se copian de ella.",
                Nombre, resultado.MigracionAplicada, reloj.ElapsedMilliseconds);
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    /// <summary>La misma lista blanca que el aprovisionador real: el nombre se interpola en el SQL.</summary>
    private static bool NombreAdmitido(string nombre) =>
        !string.IsNullOrWhiteSpace(nombre)
        && nombre.Length <= 63
        && !char.IsAsciiDigit(nombre[0])
        && nombre.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static async Task<bool> ExisteAsync(NpgsqlConnection servidor, string nombre, CancellationToken ct)
    {
        await using var consulta = servidor.CreateCommand();
        consulta.CommandText = "select 1 from pg_database where datname = @nombre";
        consulta.Parameters.AddWithValue("nombre", nombre);
        return await consulta.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task EjecutarAsync(NpgsqlConnection servidor, string sql, CancellationToken ct)
    {
        await using var comando = servidor.CreateCommand();
        comando.CommandText = sql;
        comando.CommandTimeout = 120;
        await comando.ExecuteNonQueryAsync(ct);
    }
}
