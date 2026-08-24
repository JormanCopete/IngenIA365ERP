using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Seeding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IngenIA365ERP.API.IntegrationTests.MultiTenancy;

/// <summary>
/// Aprovisionar la base de una cooperativa, contra un PostgreSQL de verdad.
///
/// <para>
/// Es lo único de este trabajo que no se puede comprobar en memoria: crear una
/// base, migrarla y sembrarla depende del motor. EF InMemory no tiene bases, no
/// tiene migraciones y no tiene <c>CREATE DATABASE</c>.
/// </para>
///
/// <para>
/// Se salta sola si no hay servidor. La cadena entra por la variable de entorno
/// <c>ERP_TEST_PG</c> y no se codifica aquí: lleva credenciales.
/// </para>
/// </summary>
public class AprovisionarBaseDeCooperativaTests
{
    private const string Variable = "ERP_TEST_PG";
    private const string BaseDePrueba = "erp_prueba_aprovisionamiento";

    private static string? Cadena => Environment.GetEnvironmentVariable(Variable);

    private static ITenantDatabaseProvisioner Aprovisionador(string cadena)
    {
        var opciones = Options.Create(new DatabaseOptions
        {
            Provider = "PostgreSql",
            ConnectionStrings = new Dictionary<string, string?> { ["PostgreSQL"] = cadena },
        });

        var entorno = new EntornoDePrueba();

        return new TenantDatabaseProvisioner(
            opciones,
            new PostgreSqlProviderConfigurator(),
            seeders: [],
            entorno,
            NullLogger<TenantDatabaseProvisioner>.Instance);
    }

    private static async Task SoltarAsync(string cadena, string nombre)
    {
        var servidor = ConnectionStringTargeting.ANivelDeServidor(cadena, DatabaseProvider.PostgreSql);
        await using var conexion = new NpgsqlConnection(servidor);
        await conexion.OpenAsync();
        // En dos comandos: Npgsql envia varias sentencias juntas como una
        // transaccion implicita, y DROP DATABASE no corre dentro de una.
        await using (var corte = conexion.CreateCommand())
        {
            corte.CommandText =
                "select pg_terminate_backend(pid) from pg_stat_activity where datname = @n";
            var p = corte.CreateParameter();
            p.ParameterName = "@n";
            p.Value = nombre;
            corte.Parameters.Add(p);
            await corte.ExecuteNonQueryAsync();
        }

        await using var soltar = conexion.CreateCommand();
        soltar.CommandText = "drop database if exists \"" + nombre + "\"";
        await soltar.ExecuteNonQueryAsync();
    }

    private static async Task<int> TablasEnAsync(string cadena, string nombre)
    {
        await using var conexion = new NpgsqlConnection(
            ConnectionStringTargeting.ConCatalogo(cadena, nombre));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "select count(*) from information_schema.tables " +
            "where table_schema = 'dbo' and table_type = 'BASE TABLE'";
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _salida;

    public AprovisionarBaseDeCooperativaTests(Xunit.Abstractions.ITestOutputHelper salida)
        => _salida = salida;

    [Fact]
    public async Task CreaLaBase_LaMigra_YEsIdempotente()
    {
        if (string.IsNullOrWhiteSpace(Cadena))
        {
            // No hay skip dinamico en esta version de xunit. Se anuncia en la
            // salida para que no pase por verde silencioso.
            _salida.WriteLine(
                $"OMITIDA: sin la variable {Variable} no hay PostgreSQL contra el que probar. " +
                "Esta prueba solo corre en una maquina con el motor levantado.");
            return;
        }

        var cadena = Cadena!;
        await SoltarAsync(cadena, BaseDePrueba);

        try
        {
            var aprovisionador = Aprovisionador(cadena);

            var primera = await aprovisionador.AprovisionarAsync(
                BaseDePrueba, "prueba", cadenaPropia: null, CancellationToken.None);

            primera.BaseCreada.Should().BeTrue();
            primera.MigracionAplicada.Should().NotBeNullOrWhiteSpace(
                "la base recién creada tiene que quedar migrada, no vacía");

            var tablas = await TablasEnAsync(cadena, BaseDePrueba);
            tablas.Should().BeGreaterThan(200,
                "las migraciones del contexto operativo crean el esquema completo");

            // Idempotencia: aprovisionar dos veces no duplica ni falla. Importa
            // porque el endpoint de provisión existe justo para reparar.
            var segunda = await aprovisionador.AprovisionarAsync(
                BaseDePrueba, "prueba", cadenaPropia: null, CancellationToken.None);

            segunda.BaseCreada.Should().BeFalse("ya existía");
            segunda.MigracionAplicada.Should().Be(primera.MigracionAplicada);
            (await TablasEnAsync(cadena, BaseDePrueba)).Should().Be(tablas);
        }
        finally
        {
            await SoltarAsync(cadena, BaseDePrueba);
        }
    }

    [Theory]
    [InlineData("con-guion")]
    [InlineData("con espacio")]
    [InlineData("con\"comilla")]
    [InlineData("punto.y.coma; drop database x")]
    [InlineData("9empiezaConDigito")]
    [InlineData("")]
    public async Task NombreNoAdmitido_Rechazado(string nombre)
    {
        // CREATE DATABASE no acepta parametros, asi que el nombre se interpola.
        // La lista blanca es la unica barrera y tiene que ser estrecha.
        var accion = async () => await Aprovisionador("Host=x;Database=y;Username=u;Password=p")
            .AprovisionarAsync(nombre, "prueba", null, CancellationToken.None);

        await accion.Should().ThrowAsync<ArgumentException>();
    }
}
/// <summary>Lo mínimo de <see cref="IHostEnvironment"/> que el aprovisionador mira.</summary>
internal sealed class EntornoDePrueba : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Pruebas";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
        new Microsoft.Extensions.FileProviders.NullFileProvider();
}
