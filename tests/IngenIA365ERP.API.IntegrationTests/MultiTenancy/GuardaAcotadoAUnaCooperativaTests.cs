using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IngenIA365ERP.API.IntegrationTests.MultiTenancy;

/// <summary>
/// El guarda de migraciones, preguntado por una cooperativa concreta, no puede
/// contestar por otras.
///
/// <para>
/// <b>El fallo que fija.</b> El guarda era global y se consultaba dentro de una
/// operacion de una sola cooperativa. Al aprovisionar <c>coop_beta</c> encontro
/// una migracion pendiente en <c>coop_alfa</c> y aborto el sembrado de beta:
/// quedo creada, migrada y sin roles ni permisos. No dio error visible mas alla
/// de una linea en el log, y la cooperativa parecia lista. Es exactamente lo que
/// el Principio IV prohibe — el estado de una cooperativa decidiendo el de otra.
/// </para>
///
/// <para>
/// Se salta sola si no hay servidor. La cadena entra por <c>ERP_TEST_PG</c> y no
/// se codifica aqui: lleva credenciales.
/// </para>
/// </summary>
public class GuardaAcotadoAUnaCooperativaTests(Xunit.Abstractions.ITestOutputHelper salida)
{
    private const string Variable = "ERP_TEST_PG";
    private const string AlDia = "erp_prueba_guarda_al_dia";
    private const string Atrasada = "erp_prueba_guarda_atrasada";

    private static string? Cadena => Environment.GetEnvironmentVariable(Variable);

    private static DatabaseOptions Opciones(string cadena) => new()
    {
        Provider = "PostgreSql",
        ConnectionStrings = new Dictionary<string, string?> { ["PostgreSQL"] = cadena },
    };

    /// <summary>
    /// El guarda tal y como lo construye el contenedor. La base administrativa no
    /// hace falta que exista: estas pruebas miran el tramo de cooperativas del
    /// reporte, que es donde estaba el fallo.
    /// </summary>
    private static PendingMigrationsGuard Guarda(string cadena)
    {
        var configurador = new PostgreSqlProviderConfigurator();
        var opciones = Options.Create(Opciones(cadena));

        var admin = new DbContextOptionsBuilder<AdminDbContext>();
        configurador.Configure(admin, cadena, MigrationsTarget.Admin);

        // El guarda ya no recibe DbContextOptions: construirlas exige la
        // cooperativa del ambito, y corre tambien donde no hay ninguna.
        return new PendingMigrationsGuard(
            new AdminDbContext(admin.Options),
            new TenantConnectionResolver(opciones),
            configurador);
    }

    private static ErpTenantInfo Cooperativa(string baseDeDatos) =>
        new() { Name = baseDeDatos, SchemaName = baseDeDatos, Identifier = baseDeDatos };

    private static async Task EjecutarEnElServidorAsync(string cadena, string sentencia)
    {
        await using var conexion = new NpgsqlConnection(
            ConnectionStringTargeting.ANivelDeServidor(cadena, DatabaseProvider.PostgreSql));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sentencia;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task SoltarAsync(string cadena, string nombre)
    {
        await EjecutarEnElServidorAsync(cadena,
            $"select pg_terminate_backend(pid) from pg_stat_activity where datname = '{nombre}'");
        await EjecutarEnElServidorAsync(cadena, $@"drop database if exists ""{nombre}""");
    }

    /// <summary>Base creada y VACIA: existe, conecta, y no tiene ni una migracion aplicada.</summary>
    private static async Task CrearVaciaAsync(string cadena, string nombre)
        => await EjecutarEnElServidorAsync(cadena, $@"create database ""{nombre}""");

    private static async Task CrearMigradaAsync(string cadena, string nombre)
    {
        var aprovisionador = new TenantDatabaseProvisioner(
            Options.Create(Opciones(cadena)),
            new PostgreSqlProviderConfigurator(),
            orquestador: null,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantDatabaseProvisioner>.Instance);

        await aprovisionador.AprovisionarAsync(nombre, nombre, cadenaPropia: null, CancellationToken.None);
    }

    private bool SinServidor()
    {
        if (!string.IsNullOrWhiteSpace(Cadena)) return false;

        // No hay skip dinamico en esta version de xunit. Se anuncia en la salida
        // para que no pase por verde silencioso.
        salida.WriteLine(
            $"OMITIDA: sin la variable {Variable} no hay PostgreSQL contra el que probar.");
        return true;
    }

    [Fact]
    public async Task UnaBaseAtrasada_NoAparece_CuandoSePreguntaPorOtra()
    {
        if (SinServidor()) return;
        var cadena = Cadena!;

        await SoltarAsync(cadena, AlDia);
        await SoltarAsync(cadena, Atrasada);

        try
        {
            await CrearMigradaAsync(cadena, AlDia);
            await CrearVaciaAsync(cadena, Atrasada);

            var reporte = await Guarda(cadena).ComputeAsync(
                CancellationToken.None, [Cooperativa(AlDia)]);

            reporte.TenantSchemas.Should().NotContainKey(Atrasada,
                "preguntar por una cooperativa no puede traer el estado de otra: eso es " +
                "lo que aborto el sembrado de coop_beta por culpa de coop_alfa");

            reporte.TenantSchemas.Should().ContainKey(AlDia);
            reporte.TenantSchemas[AlDia].Should().BeEmpty(
                "la base recien aprovisionada esta al dia");
        }
        finally
        {
            await SoltarAsync(cadena, AlDia);
            await SoltarAsync(cadena, Atrasada);
        }
    }

    [Fact]
    public async Task AcotadoNoSignificaCiego_LaBaseAtrasadaSiSeDenuncia()
    {
        // La otra mitad del contrato. Un guarda que nunca encuentra nada tambien
        // habria pasado la prueba anterior, y seria peor que no tenerlo: dejaria
        // sembrar sobre una base sin tablas.
        if (SinServidor()) return;
        var cadena = Cadena!;

        await SoltarAsync(cadena, Atrasada);

        try
        {
            await CrearVaciaAsync(cadena, Atrasada);

            var reporte = await Guarda(cadena).ComputeAsync(
                CancellationToken.None, [Cooperativa(Atrasada)]);

            reporte.TenantSchemas.Should().ContainKey(Atrasada);
            reporte.TenantSchemas[Atrasada].Should().NotBeEmpty(
                "una base existente y sin migrar tiene TODAS las migraciones pendientes");
            reporte.Describe().Should().Contain(Atrasada);
        }
        finally
        {
            await SoltarAsync(cadena, Atrasada);
        }
    }

    [Fact]
    public async Task SinObjetivos_NoSeMiraNingunaCooperativa()
    {
        // El sembrado de alcance Admin no toca ninguna base de cooperativa, asi
        // que ninguna puede bloquearlo.
        if (SinServidor()) return;
        var cadena = Cadena!;

        await SoltarAsync(cadena, Atrasada);

        try
        {
            await CrearVaciaAsync(cadena, Atrasada);

            var reporte = await Guarda(cadena).ComputeAsync(
                CancellationToken.None, []);

            reporte.TenantSchemas.Should().BeEmpty();
        }
        finally
        {
            await SoltarAsync(cadena, Atrasada);
        }
    }
}
