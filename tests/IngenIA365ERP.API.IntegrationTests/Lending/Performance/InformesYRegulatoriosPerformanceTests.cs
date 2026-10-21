using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Performance;

/// <summary>
/// T1069 (feature 013, K5, fase 24; quickstart §1.8), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se reporta
/// omitida con su motivo, nunca con un <c>return</c>): sobre una cartera de <b>50.000 créditos</b> con su cierre mensual hecho, mide la generación del
/// formato de cartera del SICSES y del archivo de una central de riesgo, y las vistas <c>aging</c>, <c>quality</c> y <c>vintages</c> del centro de
/// informes y el tablero. <b>La spec no fija umbral</b>: se mide y se anota en la salida (y en las notas de la entrega); si alguna pasa de lo que admite
/// una petición HTTP (los 100 s del cliente por defecto), se abre una tarea para llevarla a corrida en segundo plano.
///
/// <para>
/// La cartera es la sintética de <see cref="SiembraDeCarteraSintetica"/> (un crédito desembolsado por HTTP copiado con su grafo; todas las copias
/// con el mismo deudor). Como los layouts del SICSES y de las centrales rigen desde diciembre de 2026, el período medido es el primer mes desde esa
/// fecha: la cartera se causa y se cierra mes a mes hasta él, como en <c>SicsesYCentralesTests</c>. Generar puede responder con inconsistencias
/// bloqueantes (los datos sintéticos no son de un deudor reportable): igual se mide, porque la validación recorre el archivo entero. El tamaño se baja
/// con <c>CARTERA_CREDITOS_DE_RENDIMIENTO</c> para probar el arnés. (nuevo)
/// </para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class InformesYRegulatoriosPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int CreditosPorDefecto = 50_000;
    private const string Regulatorio = "/api/lending/regulatory";
    private const string Informes = "/api/reports/lending";
    private const string Central = "DATACREDITO";
    private static readonly DateOnly LayoutsDesde = new(2026, 12, 1);
    private static readonly TimeSpan LimiteDeUnaPeticion = TimeSpan.FromSeconds(100);

    [FactDeRendimiento]
    public async Task El_SICSES_la_central_las_vistas_y_el_tablero_sobre_cincuenta_mil_creditos_se_miden()
    {
        var cuantos = SiembraDeCarteraSintetica.Creditos(CreditosPorDefecto);
        var esc = await DesembolsoE2E.EscenarioAsync(fx, "informes_volumen");
        var tenant = esc.Coop.TenantPublicId;
        var admin = esc.Coop.TokenAdmin;
        var u = esc.Usuarios;
        var hoy = esc.Hoy;
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromHours(1); // se mide aunque pase del límite de una petición: eso es lo que se quiere saber
        var despachador = new ConductorDelDespachador(fx);

        var suscriptor = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/parameters/Centrales.CodigosDeSuscriptor/versions", new
        {
            scopeKind = 0, value = "DATACREDITO=123456;TRANSUNION=ABC123", validFrom = hoy.AddDays(-1), reason = "Suscriptor de las centrales del volumen (e2e)",
        });
        suscriptor.IsSuccessStatusCode.Should().BeTrue($"el código de suscriptor: «{await suscriptor.Content.ReadAsStringAsync()}»");

        var plantilla = (await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "Plantilla", 3_000_000m)).Desembolso.GetProperty("loanPublicId").GetGuid();
        var siembra = Stopwatch.StartNew();
        await SiembraDeCarteraSintetica.SembrarAsync(fx, tenant, plantilla, cuantos - 1);
        salida.WriteLine($"Siembra de {cuantos - 1:N0} créditos copiados de la plantilla: {siembra.Elapsed.TotalSeconds:N0} s.");

        // Los cierres, mes a mes, hasta el primer período con los layouts vigentes.
        var mesDeHoy = new DateOnly(hoy.Year, hoy.Month, 1);
        var periodo = mesDeHoy > LayoutsDesde ? mesDeHoy : LayoutsDesde;
        await despachador.PasadasAsync(tenant, 3);
        for (var m = mesDeHoy; m <= periodo; m = m.AddMonths(1))
        {
            var fin = new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month));
            using (RelojEn(fx, fin.AddDays(1)))
            {
                var jefe = await UsuariosDeCartera.IngresarAsync(http, u.Jefe);
                var reloj = Stopwatch.StartNew();
                await CicloDeHoyAsync(fx, http, jefe, tenant);
                await despachador.PasadasAsync(tenant, 3);
                var causado = reloj.Elapsed;
                jefe = await UsuariosDeCartera.IngresarAsync(http, u.Jefe);
                var corrida = await CierreE2E.OrdenarAsync(http, jefe, m.Year, m.Month);
                reloj.Restart();
                await CierreE2E.CorrerAsync(fx, http, jefe, tenant, corrida);
                salida.WriteLine($"{m:yyyy-MM}: causación al fin de mes {causado.TotalSeconds:N0} s; cierre mensual {reloj.Elapsed.TotalSeconds:N0} s.");
            }
        }

        var finDelPeriodo = new DateOnly(periodo.Year, periodo.Month, DateTime.DaysInMonth(periodo.Year, periodo.Month));
        var medidas = new List<(string Que, TimeSpan Tiempo, HttpStatusCode Estado)>();
        using (RelojEn(fx, finDelPeriodo.AddDays(1)))
        {
            var contadora = await UsuariosDeCartera.IngresarAsync(http, u.Contadora);
            var jefe = await UsuariosDeCartera.IngresarAsync(http, u.Jefe);

            async Task Medir(string que, Func<Task<HttpResponseMessage>> pedir, params HttpStatusCode[] admitidos)
            {
                var reloj = Stopwatch.StartNew();
                using var r = await pedir();
                reloj.Stop();
                var cuerpo = await r.Content.ReadAsStringAsync();
                (admitidos.Length == 0 ? [HttpStatusCode.OK] : admitidos).Should().Contain(r.StatusCode, $"{que}: «{cuerpo[..Math.Min(400, cuerpo.Length)]}»");
                medidas.Add((que, reloj.Elapsed, r.StatusCode));
            }

            // El SICSES y la central: generar valida el archivo entero; con datos sintéticos puede quedar «Validated» con bloqueantes (422).
            await Medir($"SICSES {periodo:yyyy-MM}: validar", () => CarteraE2E.EnviarAsync(http, contadora, HttpMethod.Post,
                $"{Regulatorio}/sicses/{periodo.Year}/{periodo.Month}/validate", null));
            await Medir($"SICSES {periodo:yyyy-MM}: generar", () => CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post,
                $"{Regulatorio}/sicses/{periodo.Year}/{periodo.Month}/generate", new { acknowledgeWarnings = true }),
                HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity);
            await Medir($"Central {Central} {periodo:yyyy-MM}: generar", () => CarteraE2E.ConClaveAsync(http, contadora, HttpMethod.Post,
                $"{Regulatorio}/bureaus/{Central}/{periodo.Year}/{periodo.Month}/generate", new { acknowledgeWarnings = true }),
                HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity);

            // Las vistas del centro de informes y el tablero, con el corte del período.
            foreach (var vista in new[] { "aging", "quality", "vintages" })
                await Medir($"Vista {vista}", () => CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/{vista}"));
            await Medir("Tablero", () => CarteraE2E.MandarGetAsync(http, jefe, "/api/lending/dashboard"));
        }

        salida.WriteLine($"T1069 ({Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "PostgreSql"}), {cuantos:N0} créditos, período {periodo:yyyy-MM}:");
        foreach (var (que, tiempo, estado) in medidas)
            salida.WriteLine($"  {que}: {tiempo.TotalSeconds:N1} s ({(int)estado}){(tiempo > LimiteDeUnaPeticion ? " — PASA del límite de una petición HTTP: abrir tarea" : string.Empty)}.");
    }
}
