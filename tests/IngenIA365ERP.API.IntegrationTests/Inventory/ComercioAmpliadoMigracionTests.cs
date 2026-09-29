using System.Data.Common;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T850 — el par aditivo <c>ComercioAmpliado</c> (feature 012, I6; T860; data-model §1.11, §3.0, §14; decisiones-transversales
/// §2.15), en el motor de <c>DB_PROVIDER</c>. En una cooperativa aislada con un ajuste de I1 confirmado (líneas y kardex con
/// <c>LotId</c>/<c>SerialId</c> nulos): se baja la base hasta la migración anterior, se vuelve a subir y las filas siguen idénticas;
/// quedan los índices únicos filtrados y las FK de lote, serie y promoción; y el <c>Down()</c> quita sólo lo de I6 (lo de I5 sigue).
///
/// <para>
/// Mira la base por debajo a propósito: lo que se prueba es la migración, no la API. Al terminar la base queda otra vez en la
/// última migración.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ComercioAmpliadoMigracionTests(CentralIdentityApiFixture fx)
{
    private const string Migracion = "ComercioAmpliado";

    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    private static readonly string[] TablasDeI6 =
    [
        "INV_ProductComponents", "INV_VariantAttributes", "INV_VariantAttributeValues", "INV_ProductVariantValues",
        "INV_Lots", "INV_Serials", "INV_Reservations", "INV_Promotions", "INV_PromotionScopes", "INV_PromotionTiers",
    ];

    /// <summary>Los índices que fija data-model §1.6, §1.11 y §14.</summary>
    private static readonly string[] Indices =
    [
        "UK_INV_Lots_Product_Code",
        "UK_INV_Serials_Product_SerialNumber",
        "UK_INV_Products_Parent_VariantKey",
        "UK_INV_ProductComponents_Product_Component",
        "UK_INV_VariantAttributeValues_Attribute_Code",
        "UK_INV_ProductVariantValues_Product_Attribute",
        "UK_INV_PromotionTiers_Promotion_MinQuantity",
        "IX_INV_Reservations_ExpiresOn",
    ];

    /// <summary>Las FK nuevas sobre tablas que ya existían (data-model §3.0, §14).</summary>
    private static readonly string[] LlavesForaneas =
    [
        "FK_INV_KardexEntries_INV_Lots_LotId",
        "FK_INV_KardexEntries_INV_Serials_SerialId",
        "FK_INV_DocumentLines_INV_Lots_LotId",
        "FK_INV_DocumentLines_INV_Serials_SerialId",
        "FK_INV_StockDetails_INV_Lots_LotId",
        "FK_INV_CountSnapshotLines_INV_Lots_LotId",
        "FK_INV_DocumentLineDiscounts_INV_Promotions_PromotionId",
    ];

    [Fact]
    public async Task El_par_sube_y_baja_sin_tocar_las_filas_de_I1_y_deja_sus_indices_y_llaves()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "comercioampliado");
        using var http = fx.CreateClient();
        var ajuste = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 5, 800m)]);
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", ajuste);

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == esc.Coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migrador = db.GetService<IMigrator>();

        var migraciones = db.Database.GetMigrations().ToList();
        var indice = migraciones.FindIndex(m => m.EndsWith("_" + Migracion, StringComparison.Ordinal));
        indice.Should().BePositive($"la migración {Migracion} existe en el ensamblado de {CentralIdentityApiFixture.ProviderKey}");
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(migraciones[indice]);

        var foto = await FotoDelDocumentoAsync(db, ajuste);
        foto.Should().NotBeEmpty("el ajuste confirmado tiene líneas y kardex");
        foto.Should().OnlyContain(f => f.EndsWith("|L:|S:|", StringComparison.Ordinal),
            "hasta I6 lote y serie van siempre nulos");

        // (1) Down(): quita sólo lo de I6.
        await migrador.MigrateAsync(migraciones[indice - 1]);
        foreach (var tabla in TablasDeI6)
            (await ExisteTablaAsync(db, tabla)).Should().BeFalse($"{tabla} nace con {Migracion}");
        (await ExisteColumnaAsync(db, "INV_Products", "ParentProductId")).Should().BeFalse();
        (await ExisteColumnaAsync(db, "INV_Products", "VariantKey")).Should().BeFalse();
        (await ExisteColumnaAsync(db, "INV_DocumentLineDiscounts", "PromotionId")).Should().BeFalse();
        (await ExisteColumnaAsync(db, "INV_KardexEntries", "LotId")).Should().BeTrue("LotId nace en I1 sin FK (data-model §3.0)");
        (await ExisteTablaAsync(db, "INV_CostLayers")).Should().BeTrue("lo de I5 no se toca");
        (await FotoDelDocumentoAsync(db, ajuste)).Should().Equal(foto);

        // (2) Up(): las filas siguen idénticas y quedan los índices y las FK.
        await migrador.MigrateAsync(migraciones[indice]);
        (await FotoDelDocumentoAsync(db, ajuste)).Should().Equal(foto);
        foreach (var tabla in TablasDeI6)
            (await ExisteTablaAsync(db, tabla)).Should().BeTrue($"{tabla} nace con {Migracion}");
        foreach (var nombre in Indices)
            (await ExisteIndiceAsync(db, nombre)).Should().BeTrue($"{nombre} lo fija data-model");
        foreach (var nombre in LlavesForaneas)
            (await ExisteLlaveForaneaAsync(db, nombre)).Should().BeTrue($"{nombre} lo fija data-model §3.0/§14");
        (await FiltroDelIndiceAsync(db, "UK_INV_Products_Parent_VariantKey")).Should().Contain("ParentProductId").And.Contain("IsDeleted");
        (await FiltroDelIndiceAsync(db, "IX_INV_Reservations_ExpiresOn")).Should().Contain("Status");

        // Deja la base en la última migración para el resto de la colección.
        await migrador.MigrateAsync();
    }

    /// <summary>Líneas y kardex del documento como texto estable (Id, cantidades, costo, lote y serie).</summary>
    private static async Task<List<string>> FotoDelDocumentoAsync(ApplicationDbContext db, Guid documento)
    {
        var sql = EsSqlServer
            ? """
              SELECT CONCAT('L', l.[Id], '|', l.[QuantityBase], '|', l.[UnitCost], '|L:', l.[LotId], '|S:', l.[SerialId], '|')
              FROM [dbo].[INV_DocumentLines] l JOIN [dbo].[INV_Documents] d ON d.[Id] = l.[DocumentId] WHERE d.[PublicId] = @p
              UNION ALL
              SELECT CONCAT('K', k.[Id], '|', k.[QuantityBase], '|', k.[UnitCost], '|L:', k.[LotId], '|S:', k.[SerialId], '|')
              FROM [dbo].[INV_KardexEntries] k JOIN [dbo].[INV_Documents] d ON d.[Id] = k.[DocumentId] WHERE d.[PublicId] = @p
              """
            : """
              SELECT CONCAT('L', l."Id", '|', l."QuantityBase", '|', l."UnitCost", '|L:', l."LotId", '|S:', l."SerialId", '|')
              FROM dbo."INV_DocumentLines" l JOIN dbo."INV_Documents" d ON d."Id" = l."DocumentId" WHERE d."PublicId" = @p
              UNION ALL
              SELECT CONCAT('K', k."Id", '|', k."QuantityBase", '|', k."UnitCost", '|L:', k."LotId", '|S:', k."SerialId", '|')
              FROM dbo."INV_KardexEntries" k JOIN dbo."INV_Documents" d ON d."Id" = k."DocumentId" WHERE d."PublicId" = @p
              """;
        var filas = new List<string>();
        await ConComandoAsync(db, sql, documento, async comando =>
        {
            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync()) filas.Add(lector.GetString(0));
        });
        filas.Sort(StringComparer.Ordinal);
        return filas;
    }

    private static Task<bool> ExisteTablaAsync(ApplicationDbContext db, string tabla) =>
        ContarAsync(db, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'dbo' AND table_name = @p", tabla);

    private static async Task<bool> ExisteColumnaAsync(ApplicationDbContext db, string tabla, string columna) =>
        await ContarAsync(db,
            $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'dbo' AND table_name = '{tabla}' AND column_name = @p", columna);

    private static Task<bool> ExisteIndiceAsync(ApplicationDbContext db, string nombre) =>
        ContarAsync(db, EsSqlServer
            ? "SELECT COUNT(*) FROM sys.indexes WHERE name = @p"
            : "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'dbo' AND indexname = @p", nombre);

    private static Task<bool> ExisteLlaveForaneaAsync(ApplicationDbContext db, string nombre) =>
        ContarAsync(db,
            "SELECT COUNT(*) FROM information_schema.table_constraints WHERE constraint_type = 'FOREIGN KEY' AND constraint_name = @p", nombre);

    private static async Task<string> FiltroDelIndiceAsync(ApplicationDbContext db, string nombre)
    {
        var sql = EsSqlServer
            ? "SELECT filter_definition FROM sys.indexes WHERE name = @p"
            : "SELECT indexdef FROM pg_indexes WHERE schemaname = 'dbo' AND indexname = @p";
        var texto = string.Empty;
        await ConComandoAsync(db, sql, nombre, async comando => texto = Convert.ToString(await comando.ExecuteScalarAsync()) ?? string.Empty);
        return texto;
    }

    private static async Task<bool> ContarAsync(ApplicationDbContext db, string sql, object valor)
    {
        long cantidad = 0;
        await ConComandoAsync(db, sql, valor, async comando => cantidad = Convert.ToInt64(await comando.ExecuteScalarAsync()));
        return cantidad > 0;
    }

    private static async Task ConComandoAsync(ApplicationDbContext db, string sql, object valor, Func<DbCommand, Task> trabajo)
    {
        var conexion = db.Database.GetDbConnection();
        var abierta = conexion.State == System.Data.ConnectionState.Open;
        if (!abierta) await conexion.OpenAsync();
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = sql;
            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@p";
            parametro.Value = valor;
            comando.Parameters.Add(parametro);
            await trabajo(comando);
        }
        finally
        {
            if (!abierta) await conexion.CloseAsync();
        }
    }
}
