using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.MigracionE2E;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0731 (feature 013, US8; C60; quickstart §1.8; SC-013), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se reporta
/// omitida con su motivo, nunca con un <c>return</c>): una carga aplicada de <b>50.000 créditos</b> sintéticos con 30 cuotas cada uno (≈1,5 millones de
/// cuotas), subida por HTTP en <b>varios archivos por parte</b> (10.000 créditos por archivo de <c>loans</c>, 60.000 filas por archivo de cuotas: el tope del lector), con el
/// cuadre aceptado con acta en una cooperativa aislada. Primero una falla provocada en la <b>última tanda</b> —una restricción <c>CHECK</c> temporal
/// sobre <c>LND_Loans</c> que rechaza el último número— deja la base como antes: ningún crédito, ningún cierre base, el destino <c>Lending</c>
/// inactivo y la carga <c>Applied</c>. Sin la restricción, <c>ActivateLendingCommand</c> termina sin agotar la memoria del host. Deja medidos el
/// tiempo de la subida, de la aplicación y de la activación (total y promedio por tanda de 1.000), la memoria máxima del proceso y las filas nacidas
/// por tabla. Sin umbral de tiempo (se anota en las notas de la entrega). <c>CARTERA_CREDITOS_DE_ACTIVACION</c> y
/// <c>CARTERA_CUOTAS_DE_ACTIVACION</c> bajan el tamaño para probar el arnés. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ActivacionDeCarteraPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int CreditosPorDefecto = 50_000;
    private const int CuotasPorDefecto = 30;
    private const int CreditosPorArchivo = 10_000;
    private const int FilasDeCuotasPorArchivo = 60_000; // el tope de filas por hoja del lector
    private const string Restriccion = "CK_LND_Loans_FallaProvocadaT0731";

    [FactDeRendimiento]
    public async Task La_activacion_de_cincuenta_mil_creditos_termina_y_una_falla_en_la_ultima_tanda_no_deja_nada()
    {
        var creditos = Entero("CARTERA_CREDITOS_DE_ACTIVACION", CreditosPorDefecto);
        var cuotas = Entero("CARTERA_CUOTAS_DE_ACTIVACION", CuotasPorDefecto);
        var corte = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "activacion_volumen", corte.Year);
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromHours(2); // la activación de 50.000 créditos tarda más que los 100 s por defecto
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop);
        await LineaAsync(fx, coop, corte);
        await CarteraE2E.RegistrarDoblesAsync(http, admin, corte);
        var oficina = await OficinaAsync(http, admin, tenant, "VOL");
        var deudores = new List<string>();
        for (var i = 0; i < Math.Min(500, creditos); i++) deudores.Add((await PersonaAsync(http, admin, $"Volumen{i:000}")).Documento);

        // ------------------------------------------------------------------------------- la carga, en varios archivos --
        var numeros = Enumerable.Range(1, creditos).Select(i => $"VOL-{i:000000}").ToList();
        var capitalPorCuota = 100_000m;
        var reloj = Stopwatch.StartNew();
        var carga = await CrearCargaAsync(http, admin, corte, "Volumen de la activación (e2e)");
        var archivo = 0;
        foreach (var lote in numeros.Chunk(CreditosPorArchivo))
        {
            archivo++;
            await CargarAsync(http, admin, carga, P.Loans, lote.Select((n, i) =>
                Credito(n, deudores[i % deudores.Count], oficina, corte.AddDays(-60), capitalPorCuota * (cuotas + 2), cuotas + 2)), archivo: $"creditos-{archivo}.csv");
            await CargarAsync(http, admin, carga, P.Balances, lote.Select(n => Saldo(n, "Principal", capitalPorCuota * cuotas)), archivo: $"saldos-{archivo}.csv");
            await CargarAsync(http, admin, carga, P.RiskData, lote.Select(Riesgo), archivo: $"riesgo-{archivo}.csv");
        }
        archivo = 0;
        foreach (var lote in numeros.Chunk(Math.Max(1, FilasDeCuotasPorArchivo / cuotas)))
        {
            archivo++;
            await CargarAsync(http, admin, carga, P.Installments, lote.SelectMany(n => Cuotas(n, corte, cuotas, capitalPorCuota)), archivo: $"cuotas-{archivo}.csv");
        }
        var subida = reloj.Elapsed;
        reloj.Restart();
        var aplicar = await AplicarAsync(http, admin, carga, "Carga de volumen");
        aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());
        var aplicacion = reloj.Elapsed;

        var punto = await CarteraE2E.ActaDeActivacionAsync(http, coop, carga, corte);
        var diferencias = (await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}/balance-check")).GetProperty("differences").EnumerateArray()
            .Where(d => d.GetProperty("acceptedAt").ValueKind == JsonValueKind.Null).Select(d => d.GetProperty("publicId").GetGuid()).ToList();
        if (diferencias.Count > 0)
        {
            var aceptar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cargas}/{carga}/differences/accept",
                new { differencePublicIds = diferencias, sessionItemPublicId = punto, reason = "Cuadre del volumen aceptado (e2e)" });
            aceptar.StatusCode.Should().Be(HttpStatusCode.OK, await aceptar.Content.ReadAsStringAsync());
        }

        object Orden() => new
        {
            migrationLoadPublicId = carga, activationDate = corte.AddDays(1), sessionItemPublicId = punto, acknowledgeWarnings = true,
            reason = "Activación del volumen (e2e)", payrollWarningAcknowledged = true,
        };

        using (new ConductorDelDespachador(fx).Adelantar(TimeSpan.FromDays(1)))
        {
            // ------------------------------------------------------------- la falla provocada en la última tanda --
            await RestriccionAsync(tenant, numeros[^1], poner: true);
            HttpResponseMessage fallida;
            try
            {
                fallida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden());
            }
            finally
            {
                await RestriccionAsync(tenant, numeros[^1], poner: false);
            }
            fallida.IsSuccessStatusCode.Should().BeFalse("la última tanda choca con la restricción provocada");
            var despues = await EnLaBaseAsync(fx, tenant, async db => new
            {
                Creditos = await db.Loans.CountAsync(),
                Bases = await db.MonthlyCloses.CountAsync(c => c.Kind == MonthlyCloseKind.Base),
                Activada = (await db.LendingSetups.SingleAsync()).ActivationDate,
                Carga = (await db.MigrationLoads.SingleAsync(l => l.PublicId == carga)).Status,
                Destino = await db.IntegrationParticipants.AnyAsync(p => p.Code == "CAR" && p.IsEnabled && p.ValidTo == null),
            });
            despues.Creditos.Should().Be(0, "la falla en la última tanda no deja ningún crédito");
            despues.Bases.Should().Be(0, "ni cierre base");
            despues.Activada.Should().BeNull();
            despues.Carga.Should().Be(MigrationLoadStatus.Applied);
            despues.Destino.Should().BeFalse("el destino Lending sigue inactivo");

            // ---------------------------------------------------------------------------------------- la activación --
            var proceso = Process.GetCurrentProcess();
            proceso.Refresh();
            var memoriaAntes = proceso.PeakWorkingSet64;
            // La activación fallida tarda más que el access de quince minutos: un ingreso nuevo del administrador.
            var ingreso = await http.PostAsJsonAsync("/api/auth/login", new { email = coop.CorreoAdmin, password = "Car-activacion_volumen-2026!" });
            ingreso.StatusCode.Should().Be(HttpStatusCode.OK, await ingreso.Content.ReadAsStringAsync());
            admin = (await CarteraE2E.LeerAsync(ingreso)).GetProperty("accessToken").GetString()!;
            reloj.Restart();
            var activar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden());
            var activacion = reloj.Elapsed;
            activar.StatusCode.Should().Be(HttpStatusCode.Created, await activar.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(activar)).GetProperty("loans").GetInt32().Should().Be(creditos);
            proceso.Refresh();

            var filas = await EnLaBaseAsync(fx, tenant, async db => new Dictionary<string, int>
            {
                ["LND_Loans"] = await db.Loans.CountAsync(),
                ["LND_LoanTermsVersions"] = await db.LoanTermsVersions.CountAsync(),
                ["LND_LoanOperations"] = await db.LoanOperations.CountAsync(),
                ["LND_LoanTransactions"] = await db.LoanTransactions.CountAsync(),
                ["LND_RepaymentInstallments"] = await db.RepaymentInstallments.CountAsync(),
                ["LND_LoanControls"] = await db.LoanControls.CountAsync(),
                ["LND_MonthlyCloseLoans"] = await db.MonthlyCloseLoans.CountAsync(),
            });
            var tandas = (int)Math.Ceiling(creditos / 1000d);
            salida.WriteLine($"T0731 ({Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "PostgreSql"}): {creditos:N0} créditos, {creditos * cuotas:N0} cuotas.");
            salida.WriteLine($"  subida de las partes: {subida.TotalSeconds:N1} s; aplicar: {aplicacion.TotalSeconds:N1} s.");
            salida.WriteLine($"  activación: {activacion.TotalSeconds:N1} s en {tandas} tandas de 1.000 (≈{activacion.TotalMilliseconds / tandas:N0} ms por tanda).");
            salida.WriteLine($"  memoria máxima del proceso: {memoriaAntes / 1_048_576d:N0} MiB antes, {proceso.PeakWorkingSet64 / 1_048_576d:N0} MiB después.");
            foreach (var (tabla, cuantas) in filas) salida.WriteLine($"  {tabla}: {cuantas:N0} filas.");
            filas["LND_Loans"].Should().Be(creditos);
            filas["LND_RepaymentInstallments"].Should().BeGreaterThanOrEqualTo(creditos * cuotas);
        }
    }

    /// <summary>Pone o quita la restricción <c>CHECK</c> que rechaza el último número (en los dos motores; no choca con el <c>OUTPUT</c> de SQL Server).</summary>
    private Task RestriccionAsync(Guid tenant, string numero, bool poner) => EnLaBaseAsync(fx, tenant, async db =>
    {
        var contexto = (DbContext)db;
        var postgres = contexto.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        var sql = (postgres, poner) switch
        {
            (true, true) => $"""ALTER TABLE dbo."LND_Loans" ADD CONSTRAINT "{Restriccion}" CHECK ("Number" <> '{numero}')""",
            (true, false) => $"""ALTER TABLE dbo."LND_Loans" DROP CONSTRAINT IF EXISTS "{Restriccion}" """,
            (false, true) => $"ALTER TABLE [dbo].[LND_Loans] ADD CONSTRAINT [{Restriccion}] CHECK ([Number] <> '{numero}')",
            (false, false) => $"ALTER TABLE [dbo].[LND_Loans] DROP CONSTRAINT IF EXISTS [{Restriccion}]",
        };
        await contexto.Database.ExecuteSqlRawAsync(sql);
        return true;
    });

    private static int Entero(string variable, int porDefecto) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out var n) && n > 0 ? n : porDefecto;
}
