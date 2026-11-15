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
///
/// <para>Consolidación de migraciones (2026-11-15): la prueba de la guarda de la migración destructiva, que bajaba la base hasta antes
/// del retiro, se retiró con la migración, absorbida por <c>EsquemaBase</c> (sigue en git). Quedan las de rutas y pantallas.</para>
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
