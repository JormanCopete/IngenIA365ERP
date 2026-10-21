using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Xunit.Abstractions;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// T161 (feature 014, US9; SC-011), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se reporta omitida con su motivo): la
/// revisión previa de <b>50.000 cuentas de ahorro</b> en un solo archivo de <c>member-accounts</c> (el tope de filas del lector es 60.000) termina en
/// menos de 10 minutos y no guarda nada. Los titulares son 200 asociados que reparten las cuentas (una persona puede tener varias cuentas de ahorro); la
/// línea nace con el reloj de la suite en el corte. Se mide también la aplicación de la tanda, sin umbral. <c>CAPTACIONES_CUENTAS_DE_MIGRACION</c> baja
/// el tamaño para probar el arnés.
/// <b>e2e en la corrida final</b> (decisión del dueño): se escribe y compila; no se corre por fase. (nuevo)
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public sealed class MigracionPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int CuentasPorDefecto = 50_000;
    private const int Titulares = 200;
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(10);

    [FactDeRendimiento]
    public async Task La_revision_de_cincuenta_mil_cuentas_termina_en_menos_de_diez_minutos_sin_guardar_nada()
    {
        var cuantas = int.TryParse(Environment.GetEnvironmentVariable("CAPTACIONES_CUENTAS_DE_MIGRACION"), out var n) && n > 0 ? n : CuentasPorDefecto;
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, "volumen_migracion");
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(30);
        var admin = coop.TokenAdmin;
        var corte = CarteraE2E.Hoy(fx).AddDays(-1);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var agencia = await MigracionE2E.OficinaAsync(http, admin, coop.TenantPublicId, "MIGVOL");
        using (CausacionE2E.RelojEn(fx, corte))
            await MovimientosTests.LineaALaVistaAsync(http, admin, corte, "AHMIGVOL");
        var titulares = new List<string>();
        for (var i = 1; i <= Math.Min(Titulares, cuantas); i++) titulares.Add((await MigracionDeCaptacionesTests.AsociadoAsync(http, admin, i)).Documento);

        var alta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/deposits/migration/loads",
            new { nature = "Deposit", cutoffDate = corte, notes = "Volumen de la migración (e2e)" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var carga = (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var archivo = MigracionDeCaptacionesTests.Csv(P.MemberAccounts, Enumerable.Range(1, cuantas).Select(i => MigracionDeCaptacionesTests.Fila(
            (C.Numero, $"MV-{i:0000000}"), (C.Titular, titulares[i % titulares.Count]), (C.Oficina, agencia), (C.Linea, "AHMIGVOL"), (C.Clase, "Demand"),
            (C.FechaDeApertura, "2020-01-15"), (C.Estado, "Active"), (C.SaldoCapital, "1000000"))));

        var reloj = Stopwatch.StartNew();
        var revision = await MigracionDeCaptacionesTests.ImportarAsync(http, admin, carga, P.MemberAccounts, archivo, "review");
        var tiempoDeRevision = reloj.Elapsed;
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(revision)).GetProperty("valid").GetBoolean().Should().BeTrue();
        salida.WriteLine($"Revisión de {cuantas} cuentas: {tiempoDeRevision}.");
        (await CarteraE2E.GetAsync(http, admin, $"/api/lending/deposits/migration/loads/{carga}")).GetProperty("parts").GetArrayLength()
            .Should().Be(0, "revisar no guarda nada");

        reloj.Restart();
        var aplicada = await MigracionDeCaptacionesTests.ImportarAsync(http, admin, carga, P.MemberAccounts, archivo);
        salida.WriteLine($"Aplicación de la tanda de {cuantas} cuentas: {reloj.Elapsed}.");
        aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());

        tiempoDeRevision.Should().BeLessThan(Limite, "SC-011: 50.000 cuentas se revisan en menos de 10 minutos");
    }
}
