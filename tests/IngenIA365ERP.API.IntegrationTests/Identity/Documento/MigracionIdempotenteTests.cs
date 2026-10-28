using System.Data.Common;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T091 y T022), en el motor de <c>DB_PROVIDER</c> y sobre una cooperativa aislada:
/// <list type="number">
///   <item>volver a aplicar las migraciones no cambia nada (no quedan pendientes y migrar otra vez no falla);</item>
///   <item>el relleno SQL de <c>COR_People.Username</c> da exactamente lo que <c>UsuarioDeDocumento.DerivarParaPersona</c>
///         sobre datos sucios (puntos, guiones, espacios, minúsculas, tipos cortos y uno desconocido): se baja la base
///         hasta antes de <c>UsuarioDeLaPersona</c>, se siembran las filas sin usuario y se vuelve a subir;</item>
///   <item>la reversa funciona sin convivencia y se niega, dejando la base en la última migración, cuando ya
///         conviven dos personas con el mismo número y distinto tipo (el límite que dice el runbook).</item>
/// </list>
/// La tarea de datos <c>identidad-documento</c> vive en el DbMigrator, que este proyecto no referencia: su
/// idempotencia queda para el recorrido a mano (quickstart).
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class MigracionIdempotenteTests(CentralIdentityApiFixture fx)
{
    private const string Migracion = "UsuarioDeLaPersona";
    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    private static readonly (string IdType, string TaxId)[] Sucios =
    [
        ("C", "63.160.101"),
        ("CC", " 63 160 102 "),
        ("E", "ab-163.103"),
        ("PT", "1-631-6104"),
        ("PE", "pe 163105"),
        ("P", "x1y2z3-6106"),
        ("T", "1.063.160.107"),
        ("R", "63160108"),
        ("N", "900.163.109-7"),
        ("XX", "63160110"),
    ];

    [Fact]
    public async Task Las_migraciones_se_reaplican_el_relleno_coincide_con_la_regla_y_la_reversa_respeta_su_limite()
    {
        var esc = new Escenario017(fx);
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coop = await esc.CooperativaAsync(http, maestro, "tenant_f017_migra", "900171601");

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;

        // (1) La base administrativa: nada pendiente y migrar otra vez no hace nada.
        var admin = servicios.GetRequiredService<AdminDbContext>();
        Assert.Empty(await admin.Database.GetPendingMigrationsAsync());
        await admin.Database.MigrateAsync();

        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migrador = db.GetService<IMigrator>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync();

        var migraciones = db.Database.GetMigrations().ToList();
        var indice = migraciones.FindIndex(m => m.EndsWith("_" + Migracion, StringComparison.Ordinal));
        Assert.True(indice > 0, $"La migración {Migracion} existe en el ensamblado de {CentralIdentityApiFixture.ProviderKey}.");
        Assert.Contains(migraciones[indice], await db.Database.GetAppliedMigrationsAsync());
        var plantilla = await UnaPersonaAsync(db);

        try
        {
            // (2) T022: el relleno coincide con la regla.
            await migrador.MigrateAsync(migraciones[indice - 1]);
            foreach (var (tipo, numero) in Sucios)
                await ClonarAsync(db, plantilla, tipo, numero);
            await migrador.MigrateAsync(migraciones[indice]);

            foreach (var (tipo, numero) in Sucios)
            {
                var usuario = await EscalarAsync(db,
                    EsSqlServer
                        ? "SELECT [Username] FROM [dbo].[COR_People] WHERE [TaxId] = @p"
                        : "SELECT \"Username\" FROM dbo.\"COR_People\" WHERE \"TaxId\" = @p", numero);
                Assert.Equal(UsuarioDeDocumento.DerivarParaPersona(tipo, numero), usuario);
            }

            // (3) Con dos personas del mismo número y distinto tipo, la reversa se niega y la base queda arriba.
            await ClonarAsync(db, plantilla, "C", "63160201", "63160201");
            await ClonarAsync(db, plantilla, "E", "63160201", "CE63160201");
            var reversa = await Record.ExceptionAsync(() => migrador.MigrateAsync(migraciones[indice - 1]));
            Assert.NotNull(reversa);
            Assert.Contains(migraciones[indice], await db.Database.GetAppliedMigrationsAsync());

            // Sin convivencia la reversa sí funciona (y la base vuelve a subir al final).
            await db.Database.ExecuteSqlRawAsync(EsSqlServer
                ? "DELETE FROM [dbo].[COR_People] WHERE [TaxId] = '63160201'"
                : "DELETE FROM dbo.\"COR_People\" WHERE \"TaxId\" = '63160201'");
            await migrador.MigrateAsync(migraciones[indice - 1]);
            Assert.DoesNotContain(migraciones[indice], await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await migrador.MigrateAsync();
        }
    }

    /// <summary>Una persona sembrada (el consumidor final) para copiar sus columnas obligatorias.</summary>
    private static async Task<long> UnaPersonaAsync(ApplicationDbContext db)
    {
        var id = await EscalarAsync(db, EsSqlServer
            ? "SELECT TOP 1 CAST([Id] AS nvarchar(40)) FROM [dbo].[COR_People] ORDER BY [Id]"
            : "SELECT CAST(\"Id\" AS text) FROM dbo.\"COR_People\" ORDER BY \"Id\" LIMIT 1", null);
        Assert.False(string.IsNullOrEmpty(id), "La cooperativa nueva debería tener al menos la persona del consumidor final.");
        return long.Parse(id!);
    }

    /// <summary>Copia la fila <paramref name="origen"/> con otro tipo, número y PublicId, sin pasar por EF (que conoce la última forma).</summary>
    private static async Task ClonarAsync(ApplicationDbContext db, long origen, string tipo, string numero, string usuario = "")
    {
        var columnas = new List<string>();
        var conexion = db.Database.GetDbConnection();
        await using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText = EsSqlServer
                ? "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'COR_People' AND COLUMNPROPERTY(OBJECT_ID('dbo.COR_People'), COLUMN_NAME, 'IsIdentity') = 0 AND COLUMNPROPERTY(OBJECT_ID('dbo.COR_People'), COLUMN_NAME, 'IsComputed') = 0"
                : "SELECT column_name FROM information_schema.columns WHERE table_schema = 'dbo' AND table_name = 'COR_People' AND is_identity = 'NO' AND is_generated = 'NEVER'";
            await AbrirAsync(conexion);
            await using var lector = await cmd.ExecuteReaderAsync();
            while (await lector.ReadAsync()) columnas.Add(lector.GetString(0));
        }
        columnas.Remove("Id");

        string Q(string c) => EsSqlServer ? $"[{c}]" : $"\"{c}\"";
        string Valor(string c) => c switch
        {
            "PublicId" => EsSqlServer ? "NEWID()" : "gen_random_uuid()",
            "IdType" => $"'{tipo}'",
            "TaxId" => $"'{numero}'",
            "Username" => $"'{usuario}'",
            "LegacyCode" => "NULL",
            "Email" => "NULL",
            _ => Q(c),
        };
        var tabla = EsSqlServer ? "[dbo].[COR_People]" : "dbo.\"COR_People\"";
        var sql = $"INSERT INTO {tabla} ({string.Join(", ", columnas.Select(Q))}) " +
                  $"SELECT {string.Join(", ", columnas.Select(Valor))} FROM {tabla} WHERE {Q("Id")} = {origen}";
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task<string?> EscalarAsync(ApplicationDbContext db, string sql, string? parametro)
    {
        var conexion = db.Database.GetDbConnection();
        await AbrirAsync(conexion);
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = sql;
        if (parametro is not null)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@p";
            p.Value = parametro;
            cmd.Parameters.Add(p);
        }
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    private static async Task AbrirAsync(DbConnection conexion)
    {
        if (conexion.State != System.Data.ConnectionState.Open) await conexion.OpenAsync();
    }
}
