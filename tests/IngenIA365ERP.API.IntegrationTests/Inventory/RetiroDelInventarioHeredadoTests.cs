using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T028 — la guarda de la migración destructiva <c>RetiroDelInventarioHeredado</c> (T037; quickstart §2.1
/// paso 2; FR-092; Principio XII), en el motor de <c>DB_PROVIDER</c>. En una cooperativa aislada: se baja
/// la base hasta la migración anterior al retiro (el <c>Down()</c> recrea vacías las 23 tablas), se deja
/// una fila en <c>INV_ProductGroups</c> y el retiro se niega nombrando la tabla y la cantidad; con la fila
/// <c>COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'</c> pasa, las 23 tablas desaparecen y
/// <c>INV_Salespeople</c> conserva su vendedor.
///
/// <para>
/// Escribe a la base por debajo a propósito: las tablas heredadas ya no tienen entidad ni ruta, y lo que
/// se prueba es la migración, no la API. Al terminar la base queda otra vez en la última migración.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class RetiroDelInventarioHeredadoTests(CentralIdentityApiFixture fx)
{
    private const string Retiro = "RetiroDelInventarioHeredado";
    private const string Aprobacion = "INV.RetiroHeredado.Aprobado";

    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    [Fact]
    public async Task La_guarda_detiene_el_retiro_con_filas_y_sin_aprobacion_y_los_vendedores_sobreviven()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "retiro");
        using var http = fx.CreateClient();
        var persona = await ContabilidadE2E.CrearPersonaAsync(http, coop.TokenAdmin, "Vendedora");

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>()
            .Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migrador = db.GetService<IMigrator>();

        // Un vendedor vivo: lo único del inventario heredado que el retiro no toca.
        var personaId = await db.People.Where(p => p.PublicId == persona).Select(p => p.Id).SingleAsync();
        db.Salespeople.Add(new Salesperson { PersonId = personaId, AppliesCommission = true });
        await db.SaveChangesAsync();

        // (1) Bajar hasta la migración anterior al retiro: el Down() recrea vacías las 23 tablas.
        var migraciones = db.Database.GetMigrations().ToList();
        var indice = migraciones.FindIndex(m => m.EndsWith("_" + Retiro, StringComparison.Ordinal));
        indice.Should().BePositive($"la migración {Retiro} existe en el ensamblado de {CentralIdentityApiFixture.ProviderKey}");
        var anterior = migraciones[indice - 1];
        await migrador.MigrateAsync(anterior);
        (await ExisteTablaAsync(db, "INV_ProductGroups")).Should().BeTrue("el Down() del retiro recrea las tablas heredadas");

        // (2) Una fila en una tabla heredada y sin aprobación: la guarda se niega y nombra tabla y cantidad.
        await db.Database.ExecuteSqlRawAsync(EsSqlServer
            ? """
              INSERT INTO [dbo].[INV_ProductGroups] ([CreatedAt], [GroupCode], [IsDeleted], [MaxSalesQuantity], [Name], [PublicId], [RestrictsLimit])
              VALUES (SYSUTCDATETIME(), 1, 0, 0, N'Grupo heredado', NEWID(), 0)
              """
            : """
              INSERT INTO dbo."INV_ProductGroups" ("CreatedAt", "GroupCode", "IsDeleted", "MaxSalesQuantity", "Name", "PublicId", "RestrictsLimit")
              VALUES (now(), 1, false, 0, 'Grupo heredado', gen_random_uuid(), false)
              """);

        var mensaje = await MensajeDelFalloAsync(() => migrador.MigrateAsync(migraciones[indice]));
        mensaje.Should().NotBeNull("con filas y sin aprobación la migración tiene que detenerse");
        mensaje.Should().Contain("RetiroDelInventarioHeredado").And.Contain("INV_ProductGroups (1 filas)");
        if (EsSqlServer) mensaje.Should().Contain("50012");
        (await ExisteTablaAsync(db, "INV_ProductGroups")).Should().BeTrue("la guarda corre antes de borrar nada");
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotContain(migraciones[indice]);

        // (3) Con la aprobación del dueño para esta cooperativa, pasa.
        db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = Aprobacion, SettingValue = "true", ValueType = "Boolean", ModulePrefix = "INV",
            Description = "Aprobación del retiro del inventario heredado (prueba T028).",
        });
        await db.SaveChangesAsync();

        await migrador.MigrateAsync(migraciones[indice]);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(migraciones[indice]);
        foreach (var tabla in new[] { "INV_ProductGroups", "INV_Products", "INV_Warehouses", "INV_Documents", "INV_CommissionPriceParams" })
            (await ExisteTablaAsync(db, tabla)).Should().BeFalse($"{tabla} se retira");

        // (4) INV_Salespeople sigue ahí con su vendedor (FR-092).
        (await ExisteTablaAsync(db, "INV_Salespeople")).Should().BeTrue();
        (await db.Salespeople.IgnoreQueryFilters().CountAsync(s => s.PersonId == personaId)).Should().Be(1);

        // Deja la base en la última migración para el resto de la colección.
        await migrador.MigrateAsync();
    }

    private static async Task<bool> ExisteTablaAsync(ApplicationDbContext db, string tabla)
    {
        var conexion = db.Database.GetDbConnection();
        var abierta = conexion.State == System.Data.ConnectionState.Open;
        if (!abierta) await conexion.OpenAsync();
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'dbo' AND table_name = @t";
            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@t";
            parametro.Value = tabla;
            comando.Parameters.Add(parametro);
            return Convert.ToInt64(await comando.ExecuteScalarAsync()) > 0;
        }
        finally
        {
            if (!abierta) await conexion.CloseAsync();
        }
    }

    /// <summary>El mensaje del fallo con sus excepciones internas (y el número de error de SQL Server), o nulo si no falló.</summary>
    private static async Task<string?> MensajeDelFalloAsync(Func<Task> accion)
    {
        try
        {
            await accion();
            return null;
        }
        catch (Exception ex)
        {
            var partes = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
            {
                partes.Add(e.Message);
                if (e.GetType().GetProperty("Number")?.GetValue(e) is int numero) partes.Add(numero.ToString());
            }
            return string.Join(" | ", partes);
        }
    }
}
