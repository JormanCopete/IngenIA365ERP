using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.RetiroDeLaCarteraHeredadaTests;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, fase 46, T0471 (data-model §24; FR-161; Principio XII): la guarda de la migración destructiva <c>RetiroDeLaTesoreriaHeredada</c>, en el motor de
/// <c>DB_PROVIDER</c>, con el molde de <c>RetiroDeCaptacionesHeredadasTests</c> de la 014. La migración es <b>irreversible</b> —su <c>Down()</c> lanza—, así que la prueba no
/// baja la base: en una cooperativa aislada <see cref="SacarElRetiroAsync"/> vuelve a crear vacías las tres tablas tal como las dejó <c>TesoreriaAmpliada</c> y quita la
/// fila del retiro del historial; con un cheque sin cobrar y una factura pendiente y sin aprobación, el retiro se niega nombrando las tablas, sus cantidades y los
/// pendientes sin cargar; con un pendiente cargado lo cuenta menos; con la fila <c>COR_SystemSettings.SettingKey = 'TES.RetiroHeredado.Aprobado'</c> pasa y las tres
/// desaparecen. Por HTTP, las rutas heredadas responden 404 sin alias. <b>Prueba escrita, sin correr</b> (e2e en la corrida final).
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public class RetiroDeLaTesoreriaHeredadaTests(CentralIdentityApiFixture fx)
{
    internal const string Retiro = "RetiroDeLaTesoreriaHeredada";
    private const string Aprobacion = "TES.RetiroHeredado.Aprobado";

    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    private static readonly string[] Retiradas = ["TRS_Checks", "TRS_Concepts", "TRS_Invoices"];

    [Fact]
    public async Task La_guarda_detiene_el_retiro_con_filas_y_sin_aprobacion_y_con_ella_pasa()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "tes_retiro");

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migrador = db.GetService<IMigrator>();
        var migraciones = db.Database.GetMigrations().ToList();
        var retiro = migraciones.Single(m => m.EndsWith("_" + Retiro, StringComparison.Ordinal));

        // (1) Las tres tablas vuelven vacías y el retiro sale del historial (el Down() no existe: lanza).
        await SacarElRetiroAsync(db);
        foreach (var tabla in Retiradas) (await ExisteTablaAsync(db, tabla)).Should().BeTrue($"{tabla} vuelve vacía para probar la guarda");

        // (2) Un cheque girado sin cobrar y una factura pendiente, sin aprobación: la guarda se niega y nombra tablas, cantidades y pendientes.
        var cheque = await InsertarFilaMinimaAsync(db, "TRS_Checks", new Dictionary<string, object> { ["Amount"] = 450000 });
        var factura = await InsertarFilaMinimaAsync(db, "TRS_Invoices", new Dictionary<string, object> { ["Amount"] = 300000 });
        await EstadoAsync(db, "TRS_Checks", cheque, "E");
        await EstadoAsync(db, "TRS_Invoices", factura, "P");

        var mensaje = await MensajeDelFalloAsync(() => migrador.MigrateAsync(retiro));
        mensaje.Should().NotBeNull("con filas y sin aprobación la migración tiene que detenerse");
        mensaje.Should().Contain(Retiro).And.Contain("TRS_Checks (1 filas)").And.Contain("TRS_Invoices (1 filas)")
            .And.Contain("1 cheques sin cobrar").And.Contain("1 facturas por pagar");
        if (EsSqlServer) mensaje.Should().Contain("50016");
        (await ExisteTablaAsync(db, "TRS_Checks")).Should().BeTrue("la guarda corre antes de borrar nada");
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotContain(retiro);

        // (3) El cheque cargado con la plantilla ya no cuenta como pendiente; la tabla sigue con filas y la guarda sigue negándose.
        var usuario = await db.Users.Select(u => u.Id).FirstAsync();
        db.TreasuryLegacyPendingLoads.Add(new IngenIA365ERP.Domain.Entities.Treasury.Activation.LegacyPendingLoad
        {
            Kind = IngenIA365ERP.Domain.Enums.Treasury.LegacyPendingKind.UncashedCheck, LegacyReference = cheque.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Amount = 450000m, Date = new DateOnly(2026, 10, 1), LoadedByUserId = usuario, LoadedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var otra = await MensajeDelFalloAsync(() => migrador.MigrateAsync(retiro));
        otra.Should().Contain("0 cheques sin cobrar").And.Contain("1 facturas por pagar");

        // (4) Con la aprobación del dueño para esta cooperativa, pasa.
        db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = Aprobacion, SettingValue = "true", ValueType = "Boolean", ModulePrefix = "TES",
            Description = "Aprobación del retiro de la Tesorería anterior (prueba de la 015, fase 46).",
        });
        await db.SaveChangesAsync();

        await migrador.MigrateAsync(retiro);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(retiro);
        foreach (var tabla in Retiradas) (await ExisteTablaAsync(db, tabla)).Should().BeFalse($"{tabla} se retira");

        // Deja la base en la última migración para el resto de la colección.
        await migrador.MigrateAsync();
    }

    [Fact]
    public async Task El_retiro_es_irreversible()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "tes_retiro_down");
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migraciones = db.Database.GetMigrations().ToList();
        var indice = migraciones.FindIndex(m => m.EndsWith("_" + Retiro, StringComparison.Ordinal));

        var mensaje = await MensajeDelFalloAsync(() => db.GetService<IMigrator>().MigrateAsync(migraciones[indice - 1]));

        mensaje.Should().Contain("irreversible", "el Down() lanza antes de tocar la base");
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(migraciones[indice]);
    }

    [Fact]
    public async Task Por_HTTP_las_rutas_heredadas_responden_404_sin_alias()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "tes_retiro_http");
        using var http = fx.CreateClient();
        foreach (var ruta in new[] { "/api/treasury/checks", "/api/treasury/concepts", "/api/treasury/invoices", "/api/treasury/cash-flow?year=2026",
                     "/api/treasury/legacy-export/tables" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
            var resp = await http.SendAsync(pedido);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta} se retiró sin alias (FR-161)");
        }
    }

    /// <summary>El estado heredado («E» girado sin cobrar, «P» factura pendiente): la columna admite nulos y la fila mínima no la llena.</summary>
    private static Task EstadoAsync(ApplicationDbContext db, string tabla, long id, string estado) =>
        db.Database.ExecuteSqlRawAsync(EsSqlServer
            ? $"UPDATE [dbo].[{tabla}] SET [Status] = N'{estado}' WHERE [Id] = {id}"
            : $"UPDATE dbo.\"{tabla}\" SET \"Status\" = '{estado}' WHERE \"Id\" = {id}");

#pragma warning disable EF1001 // Las piezas de migraciones de EF son internas a propósito: aquí se usan para rehacer lo que el Down() irreversible no rehace.
    /// <summary>
    /// Saca el retiro de la Tesorería anterior de una base que ya lo tiene aplicado, para las pruebas que recorren el historial hacia atrás (las de los retiros de la
    /// 012, la 013 y la 014, y la de <c>ComercioAmpliado</c>): como su <c>Down()</c> lanza, vuelve a crear vacías las tres tablas tal como las describe el modelo de la
    /// migración anterior —las mismas operaciones que el <c>Down()</c> de EF habría generado— y quita su fila del historial. Al volver a la última migración, el retiro se
    /// aplica otra vez sobre tablas vacías. Sólo para bases de prueba.
    /// </summary>
    internal static async Task SacarElRetiroAsync(ApplicationDbContext db)
    {
        var aplicadas = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var retiro = aplicadas.SingleOrDefault(m => m.EndsWith("_" + Retiro, StringComparison.Ordinal));
        if (retiro is null) return;

        var ensamblado = db.GetService<IMigrationsAssembly>();
        var todas = ensamblado.Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var anterior = todas[todas.IndexOf(retiro) - 1];
        var proveedor = db.Database.ProviderName!;
        var inicializador = db.GetService<IModelRuntimeInitializer>();
        IModel Modelo(string id) => inicializador.Initialize(ensamblado.CreateMigration(ensamblado.Migrations[id], proveedor).TargetModel!, designTime: true);
        var antes = Modelo(anterior);
        var despues = Modelo(retiro);

        var operaciones = db.GetService<IMigrationsModelDiffer>()
            .GetDifferences(despues.GetRelationalModel(), antes.GetRelationalModel())
            .Where(o => o switch
            {
                CreateTableOperation t => t.Name.StartsWith("TRS_", StringComparison.Ordinal),
                CreateIndexOperation i => i.Table.StartsWith("TRS_", StringComparison.Ordinal),
                AddForeignKeyOperation f => f.Table.StartsWith("TRS_", StringComparison.Ordinal),
                _ => false,
            })
            .ToList();
        operaciones.OfType<CreateTableOperation>().Should().HaveCount(3, "el modelo anterior al retiro tiene las tres tablas TRS_");

        var comandos = db.GetService<IMigrationsSqlGenerator>().Generate(operaciones, antes);
        await db.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(comandos, db.GetService<IRelationalConnection>());
        await db.Database.ExecuteSqlRawAsync(db.GetService<IHistoryRepository>().GetDeleteScript(retiro));
    }
#pragma warning restore EF1001
}
