using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0483 (feature 013, US4; quickstart §1.8; SC-007, SC-020), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se
/// reporta omitida con su motivo, nunca con un <c>return</c>): una pasada completa del ciclo diario sobre 50.000 créditos vivos de una cooperativa
/// aislada, tanda por tanda de 250 con <c>RunDailyAccrualCommand</c> como la conduce el procesador, en menos de 30 minutos; mientras corre, en otra
/// cooperativa del mismo host las consultas de estado de cuenta y de la lista siguen con p95 &lt; 1 s (SC-008). Deja medidos en la salida las
/// tandas, las partes, los mensajes, las líneas del resumido del día, las filas nuevas de <c>LND_LoanDailyPositions</c> (D13) y de
/// <c>COR_IntegrationMessages</c> (B3), y un día saltado puesto al día sin duplicados.
///
/// <para>
/// <b>La cartera es sintética mientras no exista la carga de US8</b> (fase 13; quickstart dice «cargados por la migración»): un crédito desembolsado
/// por HTTP sirve de plantilla y se copia en la base con sus condiciones, su plan, su operación de desembolso, su libro, su control y sus partes —el
/// mismo grafo que deja el desembolso—, en tandas de 500 con EF. Cuando la migración cargue créditos, la siembra pasa a ser la carga. Los pagos por
/// el contrato en la otra cooperativa llegan con US5 (fase 9); hasta entonces se miden las consultas. El tamaño se puede bajar con
/// <c>CARTERA_CREDITOS_DE_RENDIMIENTO</c> para probar el arnés; el umbral sólo se juzga con los 50.000. (nuevo)
/// </para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CausacionDiariaPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int CreditosPorDefecto = 50_000;
    private const int TamanoDeLaSiembra = 500;
    private static readonly TimeSpan Umbral = TimeSpan.FromMinutes(30);

    [FactDeRendimiento]
    public async Task Una_pasada_del_ciclo_sobre_cincuenta_mil_creditos_termina_en_menos_de_treinta_minutos()
    {
        var cuantos = int.TryParse(Environment.GetEnvironmentVariable("CARTERA_CREDITOS_DE_RENDIMIENTO"), out var n) && n > 0 ? n : CreditosPorDefecto;
        var esc = await DesembolsoE2E.EscenarioAsync(fx, "causacion_volumen");
        var otra = await DesembolsoE2E.EscenarioAsync(fx, "causacion_volumen_consultas");
        var tenant = esc.Coop.TenantPublicId;
        var h = esc.Hoy;
        using var http = fx.CreateClient();
        var jefe = esc.Usuarios.Jefe.Token;

        var plantilla = (await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "Plantilla", 3_000_000m)).Desembolso.GetProperty("loanPublicId").GetGuid();
        var consultado = (await DesembolsoE2E.CreditoDesembolsadoAsync(http, otra, "Consultado", 2_000_000m)).Desembolso.GetProperty("loanPublicId").GetGuid();

        var siembra = Stopwatch.StartNew();
        await SembrarAsync(tenant, plantilla, cuantos - 1);
        salida.WriteLine($"Siembra de {cuantos - 1:N0} créditos copiados de la plantilla: {siembra.Elapsed.TotalSeconds:N0} s.");
        jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);

        // La pasada del día h+1, tanda por tanda, mientras la otra cooperativa consulta.
        Guid corrida;
        TimeSpan duracion;
        var tiempos = new List<double>();
        using (RelojEn(fx, h.AddDays(1)))
        {
            corrida = await OrdenarCicloAsync(http, jefe);
            using var fin = new CancellationTokenSource();
            var consultas = ConsultarAsync(otra, consultado, tiempos, fin.Token);
            var reloj = Stopwatch.StartNew();
            var tandas = await CorrerHastaTerminarAsync(fx, tenant, corrida, tamano: 250);
            duracion = reloj.Elapsed;
            await fin.CancelAsync();
            await consultas;
            salida.WriteLine($"Pasada del ciclo de {cuantos:N0} créditos: {duracion.TotalSeconds:N0} s en {tandas:N0} tandas.");
        }

        var medidas = await EnLaBaseAsync(fx, tenant, async db =>
        {
            var run = await db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.PublicId == corrida);
            var partes = await db.AccountingFactParts.AsNoTracking().Where(p => p.ProcessRunId == run.Id).ToListAsync();
            var mensajesDePartes = partes.Sum(p => p.MessageCount);
            var lineas = partes.Where(p => !p.PerLoanMessages).Sum(p => p.LoanCount);
            var posiciones = await db.LoanDailyPositions.AsNoTracking().CountAsync(p => p.PositionDate == h.AddDays(1));
            var mensajes = await db.IntegrationMessages.AsNoTracking().CountAsync();
            return (run.Status, run.BatchCount, run.ProcessedLoans, run.FailedLoans, Partes: partes.Count, mensajesDePartes, lineas, posiciones, mensajes);
        });
        salida.WriteLine($"Corrida {medidas.Status}: {medidas.BatchCount:N0} tandas, {medidas.ProcessedLoans:N0} procesados, {medidas.FailedLoans:N0} fallidos.");
        salida.WriteLine($"Partes: {medidas.Partes:N0}; mensajes de las partes: {medidas.mensajesDePartes:N0}; líneas del resumido del día: {medidas.lineas:N0}.");
        salida.WriteLine($"Filas nuevas de LND_LoanDailyPositions: {medidas.posiciones:N0}; COR_IntegrationMessages en la cooperativa: {medidas.mensajes:N0}.");
        var p95 = P95(tiempos);
        salida.WriteLine($"Otra cooperativa durante la pasada: {tiempos.Count:N0} consultas, p95 {p95:N0} ms.");

        medidas.Status.Should().Be(Domain.Enums.Lending.LendingProcessRunStatus.Completed);
        medidas.ProcessedLoans.Should().Be(cuantos);
        medidas.posiciones.Should().Be(cuantos, "una posición por crédito y día (D13)");
        if (tiempos.Count > 0) p95.Should().BeLessThan(1_000, "SC-008: las consultas de la otra cooperativa siguen bajo un segundo");
        if (cuantos >= CreditosPorDefecto) duracion.Should().BeLessThan(Umbral, "SC-007: una pasada del ciclo sobre 50.000 créditos en menos de 30 minutos");

        // Un día saltado (h+2) y su puesta al día con el ciclo de h+3: cada día una vez. El access dura 15 minutos: se vuelve a ingresar.
        jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
        using (RelojEn(fx, h.AddDays(3)))
        {
            var reloj = Stopwatch.StartNew();
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            salida.WriteLine($"Puesta al día de dos días: {reloj.Elapsed.TotalSeconds:N0} s.");
        }
        var duplicados = await EnLaBaseAsync(fx, tenant, db => db.LoanTransactions.AsNoTracking()
            .Where(t => t.Kind == Domain.Enums.Lending.LoanTransactionKind.Accrual)
            .GroupBy(t => new { t.LoanId, t.Concept, t.InstallmentNumber, t.ValueDate }).CountAsync(g => g.Count() > 1));
        duplicados.Should().Be(0, "ningún día causado dos veces (SC-020)");
        var alDia = await EnLaBaseAsync(fx, tenant, db => db.LoanControls.AsNoTracking().CountAsync(c => c.LastAccruedDate == h.AddDays(3)));
        alDia.Should().Be(cuantos);
    }

    /// <summary>Consulta sin parar, hasta que termine la pasada, el estado de cuenta y la lista de la otra cooperativa; anota cada tiempo.</summary>
    private async Task ConsultarAsync(DesembolsoE2E.Escenario otra, Guid credito, List<double> tiempos, CancellationToken fin)
    {
        using var http = fx.CreateClient();
        var token = await UsuariosDeCartera.IngresarAsync(http, otra.Usuarios.Jefe);
        var ingreso = Stopwatch.StartNew();
        while (!fin.IsCancellationRequested)
        {
            // El access dura 15 minutos y la pasada sobre 50.000 créditos dura más: se vuelve a ingresar cada diez.
            if (ingreso.Elapsed > TimeSpan.FromMinutes(10))
            {
                token = await UsuariosDeCartera.IngresarAsync(http, otra.Usuarios.Jefe);
                ingreso.Restart();
            }
            foreach (var url in new[] { $"{DesembolsoE2E.Creditos}/{credito}/statement", $"{DesembolsoE2E.Creditos}?pageSize=20" })
            {
                var reloj = Stopwatch.StartNew();
                using var r = await CarteraE2E.MandarGetAsync(http, token, url);
                reloj.Stop();
                r.StatusCode.Should().Be(HttpStatusCode.OK);
                lock (tiempos) tiempos.Add(reloj.Elapsed.TotalMilliseconds);
            }
            try { await Task.Delay(TimeSpan.FromMilliseconds(200), fin); } catch (OperationCanceledException) { return; }
        }
    }

    // ------------------------------------------------------------------------------------------- la siembra --

    /// <summary>
    /// Copia <paramref name="copias"/> veces el grafo del crédito plantilla —crédito, operación de desembolso, condiciones, plan con sus cuotas,
    /// libro, control y partes— con números y PublicId propios, en tandas de <see cref="TamanoDeLaSiembra"/>. Sólo prueba de volumen.
    /// </summary>
    private async Task SembrarAsync(Guid tenant, Guid plantilla, int copias)
    {
        var grafo = await EnLaBaseAsync(fx, tenant, async db =>
        {
            var loan = await db.Loans.AsNoTracking().SingleAsync(l => l.PublicId == plantilla);
            return new Grafo(
                loan,
                await db.LoanOperations.AsNoTracking().Where(o => o.LoanId == loan.Id).ToListAsync(),
                await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == loan.Id).ToListAsync(),
                await db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == loan.Id).ToListAsync(),
                await (from i in db.RepaymentInstallments.AsNoTracking()
                       join v in db.RepaymentScheduleVersions.AsNoTracking() on i.ScheduleVersionId equals v.Id
                       where v.LoanId == loan.Id
                       select i).ToListAsync(),
                await db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == loan.Id).ToListAsync(),
                await db.LoanControls.AsNoTracking().SingleAsync(c => c.LoanId == loan.Id),
                await db.LoanParties.AsNoTracking().Where(p => p.LoanId == loan.Id).ToListAsync());
        });

        for (var desde = 0; desde < copias; desde += TamanoDeLaSiembra)
        {
            var hasta = Math.Min(copias, desde + TamanoDeLaSiembra);
            await EnLaBaseAsync(fx, tenant, async db =>
            {
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                var creditos = Enumerable.Range(desde, hasta - desde)
                    .Select(i => Clonar(db, grafo.Credito, ("Number", $"VOL{i + 1:D9}"))).ToList();
                await db.SaveChangesAsync();
                var operaciones = new Dictionary<(int, long), LoanOperation>();
                for (var k = 0; k < creditos.Count; k++)
                    foreach (var o in grafo.Operaciones)
                        operaciones[(k, o.Id)] = Clonar(db, o, ("LoanId", creditos[k].Id), ("Number", $"VOP{desde + k + 1:D9}{o.Sequence}"),
                            ("IdempotencyKey", o.IdempotencyKey is null ? null : Guid.NewGuid().ToString("N")));
                await db.SaveChangesAsync();
                var condiciones = new Dictionary<(int, int), LoanTermsVersion>();
                for (var k = 0; k < creditos.Count; k++)
                    foreach (var t in grafo.Condiciones)
                        condiciones[(k, t.Id)] = Clonar(db, t, ("LoanId", creditos[k].Id), ("OperationId", operaciones[(k, t.OperationId)].Id));
                await db.SaveChangesAsync();
                var planes = new Dictionary<(int, int), RepaymentScheduleVersion>();
                for (var k = 0; k < creditos.Count; k++)
                    foreach (var v in grafo.Planes)
                        planes[(k, v.Id)] = Clonar(db, v, ("LoanId", creditos[k].Id), ("TermsVersionId", condiciones[(k, v.TermsVersionId)].Id),
                            ("OperationId", operaciones[(k, v.OperationId)].Id));
                await db.SaveChangesAsync();
                for (var k = 0; k < creditos.Count; k++)
                {
                    foreach (var c in grafo.Cuotas) Clonar(db, c, ("ScheduleVersionId", planes[(k, c.ScheduleVersionId)].Id));
                    foreach (var t in grafo.Libro)
                        Clonar(db, t, ("LoanId", creditos[k].Id), ("OperationId", t.OperationId is { } op ? operaciones[(k, op)].Id : null));
                    Clonar(db, grafo.Control, ("LoanId", creditos[k].Id));
                    foreach (var p in grafo.Partes) Clonar(db, p, ("LoanId", creditos[k].Id));
                }
                await db.SaveChangesAsync();
                return 0;
            });
        }
    }

    private sealed record Grafo(Loan Credito, List<LoanOperation> Operaciones, List<LoanTermsVersion> Condiciones, List<RepaymentScheduleVersion> Planes,
        List<RepaymentInstallment> Cuotas, List<LoanTransaction> Libro, LoanControl Control, List<LoanParty> Partes);

    /// <summary>
    /// Una copia de <paramref name="original"/> agregada al contexto, con Id nuevo, PublicId nuevo, sin token de concurrencia (lo pone el motor) y
    /// con los cambios dados. Las propiedades de escritura única se copian como valores, no por sus <c>init</c>.
    /// </summary>
    private static T Clonar<T>(ApplicationDbContext db, T original, params (string Propiedad, object? Valor)[] cambios) where T : class
    {
        var valores = db.Entry(original).CurrentValues.Clone();
        var propiedades = valores.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        valores["Id"] = valores.Properties.Single(p => p.Name == "Id").ClrType == typeof(long) ? (object)0L : 0;
        valores["PublicId"] = Guid.NewGuid();
        if (propiedades.Contains("RowVersion")) valores["RowVersion"] = null;
        foreach (var (propiedad, valor) in cambios) valores[propiedad] = valor;
        var copia = (T)valores.ToObject();
        db.Add(copia);
        return copia;
    }

    private static double P95(List<double> tiempos)
    {
        if (tiempos.Count == 0) return 0;
        var ordenados = tiempos.Order().ToList();
        return ordenados[(int)Math.Ceiling(ordenados.Count * 0.95) - 1];
    }
}
