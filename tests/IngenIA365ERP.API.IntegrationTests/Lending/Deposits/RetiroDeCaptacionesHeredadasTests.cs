using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.RetiroDeLaCarteraHeredadaTests;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014 (T006; research R28, C19; FR-119; Principio XII): la guarda de la migración destructiva
/// <c>RetiroDeCaptacionesHeredadas</c>, en el motor de <c>DB_PROVIDER</c>, con el molde de <c>RetiroDeLaCarteraHeredadaTests</c> de la
/// 013. En una cooperativa aislada se baja la base hasta la migración anterior al retiro (el <c>Down()</c> recrea vacías las 45 tablas),
/// se deja una fila en <c>LND_SavingsAccounts</c> y otra en <c>CDT_Certificates</c>; el retiro se niega nombrando las dos con su cantidad;
/// con la fila <c>COR_SystemSettings.SettingKey = 'LND.CaptacionesHeredadas.RetiroAprobado'</c> pasa y las 45 desaparecen. Por HTTP,
/// las rutas heredadas responden 404 sin alias. <b>e2e en la corrida final</b> (decisión del dueño).
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class RetiroDeCaptacionesHeredadasTests(CentralIdentityApiFixture fx)
{
    private const string Retiro = "RetiroDeCaptacionesHeredadas";
    private const string Aprobacion = "LND.CaptacionesHeredadas.RetiroAprobado";

    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    /// <summary>Muestra de las 45 retiradas que se comprueba ausente después del retiro.</summary>
    private static readonly string[] Retiradas =
    [
        "LND_SavingsAccounts", "LND_DepositAccounts", "LND_ContributionReductions", "LND_AssociateWithdrawals", "LND_SiplaParameters",
        "LND_Cashiers", "LND_Zones", "LND_AuxiliaryApplications", "CDT_Certificates", "CDT_Parameters", "CDT_RatesByTerm",
    ];

    [Fact]
    public async Task La_guarda_detiene_el_retiro_con_filas_y_sin_aprobacion_y_con_ella_pasa()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "cap_retiro");

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>()
            .Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migrador = db.GetService<IMigrator>();

        // (1) Bajar hasta la migración anterior al retiro: el Down() recrea vacías las 45 tablas.
        var migraciones = db.Database.GetMigrations().ToList();
        var indice = migraciones.FindIndex(m => m.EndsWith("_" + Retiro, StringComparison.Ordinal));
        indice.Should().BePositive($"la migración {Retiro} existe en el ensamblado de {CentralIdentityApiFixture.ProviderKey}");
        await migrador.MigrateAsync(migraciones[indice - 1]);
        (await ExisteTablaAsync(db, "LND_SavingsAccounts")).Should().BeTrue("el Down() del retiro recrea las tablas heredadas");

        // (2) Un ahorro y un CDT heredados y sin aprobación: la guarda se niega y nombra las tablas y las cantidades.
        await InsertarFilaMinimaAsync(db, "LND_SavingsAccounts", new Dictionary<string, object> { ["AccountNumber"] = 900001 });
        await InsertarFilaMinimaAsync(db, "CDT_Certificates", new Dictionary<string, object>());

        var mensaje = await MensajeDelFalloAsync(() => migrador.MigrateAsync(migraciones[indice]));
        mensaje.Should().NotBeNull("con filas y sin aprobación la migración tiene que detenerse");
        mensaje.Should().Contain(Retiro).And.Contain("LND_SavingsAccounts (1 filas)").And.Contain("CDT_Certificates (1 filas)");
        if (EsSqlServer) mensaje.Should().Contain("50014");
        (await ExisteTablaAsync(db, "LND_SavingsAccounts")).Should().BeTrue("la guarda corre antes de borrar nada");
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotContain(migraciones[indice]);

        // (3) Con la aprobación del dueño para esta cooperativa, pasa.
        db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = Aprobacion, SettingValue = "true", ValueType = "Boolean", ModulePrefix = "LND",
            Description = "Aprobación del retiro de las captaciones heredadas (prueba de la 014, T006).",
        });
        await db.SaveChangesAsync();

        await migrador.MigrateAsync(migraciones[indice]);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(migraciones[indice]);
        foreach (var tabla in Retiradas)
            (await ExisteTablaAsync(db, tabla)).Should().BeFalse($"{tabla} se retira");

        // Deja la base en la última migración para el resto de la colección.
        await migrador.MigrateAsync();
    }

    [Fact]
    public async Task Por_HTTP_las_rutas_heredadas_responden_404_sin_alias()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "cap_retiro_http");
        using var http = fx.CreateClient();

        foreach (var ruta in new[] { "/api/savings/savings-accounts", "/api/savings/zones", "/api/savings/sipla-parameters",
                     "/api/cdt/parameters", "/api/cdt/rates-by-term", "/api/cdt/certificates",
                     $"/api/reports/savings/person-portfolio/{Guid.NewGuid()}" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
            var resp = await http.SendAsync(pedido);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta} se retiró sin alias (FR-120)");
        }
    }
}
