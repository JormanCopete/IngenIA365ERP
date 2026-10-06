using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;

namespace IngenIA365ERP.API.IntegrationTests.NationalValues;

/// <summary>
/// T0040 (feature 013, fase 2; T93, FR-147, enmienda A7; data-model parte 3 §7.5), en el motor de <c>DB_PROVIDER</c>: una cooperativa
/// sembrada como antes de la réplica —SMMLV, UVT y festivos de Nómina con el origen de la semilla y sin copia de valores nacionales— recibe
/// la réplica sin fallar por <c>UK_PAY_LegalParameters_Code_ValidFrom</c> ni <c>UK_PAY_Holidays_Date</c>; lo igual queda marcado como
/// proyección; un SMMLV distinto se conserva y alerta <c>Nomina.ValorNacionalDistinto</c>; registrar el SMMLV por Nómina responde 422
/// <c>Payroll.LegalParameter.NationalValue</c>; y una nómina ordinaria calcula igual antes y después de la réplica. (nuevo)
///
/// <para>
/// La cooperativa «como hoy» se arma por debajo de la API: hoy toda cooperativa nueva nace con la réplica (semilla Order 88), así que se
/// borra su copia y se devuelven sus filas de Nómina al estado que dejaban las semillas 71 y 74. Escrita el 2026-10-06 sin poder correrla
/// (Docker no arranca en la máquina de desarrollo): compila; la corrida es T0098.
/// </para>
/// </summary>
[Collection(ValoresNacionalesCollection.Nombre)]
public sealed class ReplicaSobreCooperativaSembradaTests(CentralIdentityApiFixture fx)
{
    /// <summary>Un SMMLV distinto del de la central, el que la cooperativa «tenía sembrado».</summary>
    private const decimal SmmlvDeLaCooperativa = 1_700_000m;

    [Fact]
    public async Task La_replica_adopta_lo_igual_conserva_lo_distinto_y_la_nomina_calcula_igual()
    {
        using var http = fx.CreateClient();
        var coop = await ValoresNacionalesE2E.CooperativaAisladaAsync(fx, "sembrada");
        var admin = coop.TokenAdmin;
        var inv = new InventarioE2E.CooperativaAislada { TokenAdmin = admin, TenantPublicId = coop.TenantPublicId, SucursalPrincipal = coop.SucursalPrincipal };

        // (1) La cooperativa «como hoy»: sin copia de valores nacionales, festivos con el origen de la semilla y el SMMLV propio.
        await InventarioE2E.SqlEnLaCooperativaAsync(fx, inv,
            """
            DELETE FROM dbo."COR_NationalValueReviews";
            DELETE FROM dbo."COR_NationalValues";
            DELETE FROM dbo."COR_RegulatoryDocuments";
            DELETE FROM dbo."COR_NationalValueReplications";
            UPDATE dbo."PAY_Holidays" SET "Origin" = 1, "NationalValuePublicId" = NULL WHERE "Origin" = 6;
            UPDATE dbo."PAY_LegalParameters" SET "Source" = 'Semilla de nómina' WHERE "Code" IN ('SMMLV', 'UVT');
            UPDATE dbo."PAY_LegalParameters" SET "Value" = 1700000 WHERE "Code" = 'SMMLV';
            """,
            """
            DELETE FROM [dbo].[COR_NationalValueReviews];
            DELETE FROM [dbo].[COR_NationalValues];
            DELETE FROM [dbo].[COR_RegulatoryDocuments];
            DELETE FROM [dbo].[COR_NationalValueReplications];
            UPDATE [dbo].[PAY_Holidays] SET [Origin] = 1, [NationalValuePublicId] = NULL WHERE [Origin] = 6;
            UPDATE [dbo].[PAY_LegalParameters] SET [Source] = N'Semilla de nómina' WHERE [Code] IN (N'SMMLV', N'UVT');
            UPDATE [dbo].[PAY_LegalParameters] SET [Value] = 1700000 WHERE [Code] = N'SMMLV';
            """);
        (await EnteroAsync(inv, """SELECT COUNT(*) FROM dbo."PAY_Holidays" WHERE "Origin" = 6""",
            "SELECT COUNT(*) FROM [dbo].[PAY_Holidays] WHERE [Origin] = 6")).Should().Be(0);

        // (2) Antes de la réplica: una ordinaria de un empleado con salario de 2.500.000.
        var (empleado, _) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Sembrada", 2_500_000m, new DateTime(2025, 1, 15));
        empleado.Should().NotBeEmpty();
        var hoy = InventarioE2E.HoyEnColombia;
        var desde = new DateTime(hoy.Year, hoy.Month, 1);
        var periodo = await NominaE2E.CrearPeriodoAsync(http, admin, desde, desde.AddMonths(1).AddDays(-1));
        var antes = await NominaE2E.CalcularAsync(http, admin, periodo);

        // (3) La réplica no falla por los únicos de Nómina.
        var replica = await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
        replica.CentralDisponible.Should().BeTrue();
        replica.Valores.Should().BeGreaterThan(0, "copia otra vez todo lo de la central");

        // (4) Lo igual queda marcado como proyección: la UVT y los festivos.
        (await EnteroAsync(inv,
            """SELECT COUNT(*) FROM dbo."PAY_LegalParameters" WHERE "Code" = 'UVT' AND "Source" LIKE 'Valor nacional %'""",
            "SELECT COUNT(*) FROM [dbo].[PAY_LegalParameters] WHERE [Code] = N'UVT' AND [Source] LIKE N'Valor nacional %'"))
            .Should().BeGreaterThan(0, "la UVT igual se adopta como proyección");
        (await EnteroAsync(inv,
            """SELECT COUNT(*) FROM dbo."PAY_Holidays" WHERE "Origin" = 6 AND "NationalValuePublicId" IS NOT NULL""",
            "SELECT COUNT(*) FROM [dbo].[PAY_Holidays] WHERE [Origin] = 6 AND [NationalValuePublicId] IS NOT NULL"))
            .Should().BeGreaterThan(0, "los festivos de la semilla se adoptan como nacionales");
        (await EnteroAsync(inv,
            """SELECT COUNT(*) - COUNT(DISTINCT "Date") FROM dbo."PAY_Holidays" WHERE "IsDeleted" = FALSE""",
            "SELECT COUNT(*) - COUNT(DISTINCT [Date]) FROM [dbo].[PAY_Holidays] WHERE [IsDeleted] = 0"))
            .Should().Be(0, "no se duplicó ningún festivo");

        // (5) Lo distinto se conserva y alerta.
        (await EnteroAsync(inv,
            """SELECT COUNT(*) FROM dbo."PAY_LegalParameters" WHERE "Code" = 'SMMLV' AND "Value" = 1700000 AND "IsDeleted" = FALSE""",
            "SELECT COUNT(*) FROM [dbo].[PAY_LegalParameters] WHERE [Code] = N'SMMLV' AND [Value] = 1700000 AND [IsDeleted] = 0"))
            .Should().Be(1, $"el SMMLV de la cooperativa ({SmmlvDeLaCooperativa:N0}) se conserva");
        (await EnteroAsync(inv,
            """SELECT COUNT(*) FROM dbo."COR_Alerts" WHERE "TypeCode" = 'Nomina.ValorNacionalDistinto'""",
            "SELECT COUNT(*) FROM [dbo].[COR_Alerts] WHERE [TypeCode] = N'Nomina.ValorNacionalDistinto'"))
            .Should().BeGreaterThan(0, "y se alerta la diferencia");

        // (6) Nómina ya no registra el SMMLV ni la UVT.
        var smmlv = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/legal-parameters/SMMLV/versions", new
        {
            validFrom = new DateTime(hoy.Year + 1, 1, 1), value = 1_900_000m, source = "Decreto de prueba",
        });
        smmlv.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await smmlv.Content.ReadAsStringAsync());
        (await NominaE2E.CodigoDeErrorAsync(smmlv)).Should().Be("Payroll.LegalParameter.NationalValue");

        // (7) La ordinaria calcula igual después de la réplica (lo distinto se conservó; lo igual sólo cambió de marca).
        var despues = await NominaE2E.CalcularAsync(http, admin, periodo);
        despues.GetProperty("version").GetInt32().Should().BeGreaterThan(antes.GetProperty("version").GetInt32());
        despues.GetProperty("totals").GetProperty("earnings").GetDecimal().Should().Be(antes.GetProperty("totals").GetProperty("earnings").GetDecimal());
        despues.GetProperty("totals").GetProperty("deductions").GetDecimal().Should().Be(antes.GetProperty("totals").GetProperty("deductions").GetDecimal());
        despues.GetProperty("totals").GetProperty("net").GetDecimal().Should().Be(antes.GetProperty("totals").GetProperty("net").GetDecimal());
    }

    private async Task<int> EnteroAsync(InventarioE2E.CooperativaAislada coop, string sqlPostgres, string sqlSqlServer) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, coop, sqlPostgres, sqlSqlServer));
}
