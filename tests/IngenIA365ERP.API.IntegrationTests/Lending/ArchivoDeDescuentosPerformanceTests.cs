using System.Diagnostics;
using System.Globalization;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.LibranzaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0820 (feature 013, K2, US10; SC-016), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se reporta omitida con su
/// motivo): 5.000 obligaciones de una pagaduría en la cooperativa aislada «libranza_volumen».
/// <list type="bullet">
/// <item>el archivo de descuentos del período se genera en menos de un minuto (lectura en bloque, novedades, escritura, adjunto y mensaje);</item>
/// <item>la respuesta de la pagaduría con uno de cada diez no aplicado: el 100 % de los no aplicados queda con su motivo;</item>
/// <item>el ciclo diario del día exigible + la ventana de giro: ningún deudor con lo descontado pendiente de giro queda con mora imputada.</item>
/// </list>
/// La cartera es sintética, como en <see cref="CausacionDiariaPerformanceTests"/>: un crédito por libranza desembolsado por HTTP sirve de plantilla y se
/// copia en la base con su grafo —crédito, operación, condiciones, plan, libro, control, partes— y su vínculo con el convenio, en tandas de 500. El
/// tamaño se baja con <c>CARTERA_CREDITOS_DE_RENDIMIENTO</c> para probar el arnés; el umbral sólo se juzga con los 5.000. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ArchivoDeDescuentosPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int CreditosPorDefecto = 5_000;
    private const int TamanoDeLaSiembra = 500;
    private static readonly TimeSpan Umbral = TimeSpan.FromMinutes(1);

    [FactDeRendimiento]
    public async Task Cinco_mil_obligaciones_de_una_pagaduria_archivo_en_menos_de_un_minuto_y_sin_mora_en_la_ventana()
    {
        var cuantos = int.TryParse(Environment.GetEnvironmentVariable("CARTERA_CREDITOS_DE_RENDIMIENTO"), out var n) && n > 0 ? n : CreditosPorDefecto;
        var esc = await EscenarioAsync(fx, "libranza_volumen");
        var tenant = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var accesos = await AccesosAsync(http, esc);

        var plantilla = await CreditoPorLibranzaAsync(http, esc, accesos, "Plantilla");
        var siembra = Stopwatch.StartNew();
        await SembrarAsync(tenant, plantilla.Credito, cuantos - 1);
        salida.WriteLine($"Siembra de {cuantos - 1:N0} créditos de la pagaduría copiados de la plantilla: {siembra.Elapsed.TotalSeconds:N0} s.");

        var adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var (_, vence, _) = await PagosE2E.CuotaAsync(http, adm, plantilla.Credito);
        var exigible = await PagosE2E.ExigibleAsync(http, adm, plantilla.Credito);

        // El archivo del período: menos de un minuto.
        var reloj = Stopwatch.StartNew();
        var archivo = await GenerarAsync(http, adm, esc.Convenio, vence.Year, vence.Month);
        reloj.Stop();
        salida.WriteLine($"Archivo de {archivo.GetProperty("lines").GetInt32():N0} líneas: {reloj.Elapsed.TotalSeconds:N1} s.");
        archivo.GetProperty("lines").GetInt32().Should().Be(cuantos);
        if (cuantos == CreditosPorDefecto) reloj.Elapsed.Should().BeLessThan(Umbral, "SC-016: el archivo de 5.000 obligaciones en menos de un minuto");

        var archivoId = archivo.GetProperty("publicId").GetGuid();
        await ConfirmarEnvioAsync(fx, http, esc, archivoId, "RAD-VOL-0001");
        var (_, lineas) = await ArchivoAsync(http, adm, archivoId);
        lineas.Should().HaveCount(cuantos);

        // La respuesta: uno de cada diez no aplicado (licencia), los demás descontados el día exigible.
        var filas = lineas.Select((l, i) => (Credito: NumeroDe(l), Codigo: i % 10 == 0 ? "07" : "00",
            Descontado: i % 10 == 0 ? 0m : l.GetProperty("total").GetDecimal(), Fecha: i % 10 == 0 ? (DateOnly?)null : exigible)).ToList();
        var noAplicados = filas.Count(f => f.Codigo == "07");
        using (RelojEn(fx, exigible))
        {
            reloj.Restart();
            await ResponderAsync(fx, http, esc, archivoId, filas.Select(f => (f.Credito, f.Codigo, f.Descontado, f.Fecha)));
            salida.WriteLine($"Respuesta de {filas.Count:N0} líneas emitida y consumida: {reloj.Elapsed.TotalSeconds:N1} s.");

            var conMotivo = await EnLaBaseAsync(fx, tenant, db => (from x in db.InstallmentNonDeductions.AsNoTracking()
                                                                    join r in db.PayrollDeductionResults.AsNoTracking() on x.PayrollDeductionResultId equals r.Id
                                                                    join f in db.PayrollDeductionFiles.AsNoTracking() on r.PayrollDeductionFileId equals f.Id
                                                                    where f.PublicId == archivoId
                                                                    select x.Reason).ToListAsync());
            conMotivo.Should().HaveCount(noAplicados).And.OnlyContain(m => m != default, "el 100 % de los no aplicados con su motivo (SC-016)");
        }

        // El ciclo del último día de la ventana de giro: nadie con lo descontado pendiente de giro queda en mora.
        using (RelojEn(fx, exigible.AddDays(VentanaDeGiro)))
        {
            var jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
            reloj.Restart();
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            salida.WriteLine($"Ciclo diario hasta el fin de la ventana de giro: {reloj.Elapsed.TotalSeconds:N0} s.");
            var descontados = filas.Where(f => f.Codigo == "00").Select(f => f.Credito).ToHashSet(StringComparer.Ordinal);
            var enMora = await EnLaBaseAsync(fx, tenant, async db =>
            {
                var hoy = exigible.AddDays(VentanaDeGiro);
                return await (from p in db.LoanDailyPositions.AsNoTracking()
                              join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                              where p.PositionDate == hoy && p.DaysPastDue > 0
                              select l.Number).ToListAsync();
            });
            enMora.Where(descontados.Contains).Should().BeEmpty("dentro de la ventana de giro la mora de la pagaduría no se imputa al deudor");
        }
    }

    private static string NumeroDe(System.Text.Json.JsonElement linea) => linea.GetProperty("loanNumber").GetString()!;

    // ------------------------------------------------------------------------------------------- la siembra --

    /// <summary>
    /// Copia <paramref name="copias"/> veces el grafo del crédito plantilla —crédito, operación de desembolso, condiciones, plan con sus cuotas, libro,
    /// control, partes y el vínculo de libranza con el convenio— con números y PublicId propios. Sólo prueba de volumen.
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
                await db.LoanParties.AsNoTracking().Where(p => p.LoanId == loan.Id).ToListAsync(),
                await db.LoanPayrollLinks.AsNoTracking().Where(v => v.LoanId == loan.Id).ToListAsync());
        });

        for (var desde = 0; desde < copias; desde += TamanoDeLaSiembra)
        {
            var hasta = Math.Min(copias, desde + TamanoDeLaSiembra);
            await EnLaBaseAsync(fx, tenant, async db =>
            {
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                var creditos = Enumerable.Range(desde, hasta - desde)
                    .Select(i => Clonar(db, grafo.Credito, ("Number", $"LIB{i + 1:D9}"))).ToList();
                await db.SaveChangesAsync();
                var operaciones = new Dictionary<(int, long), LoanOperation>();
                for (var k = 0; k < creditos.Count; k++)
                    foreach (var o in grafo.Operaciones)
                        operaciones[(k, o.Id)] = Clonar(db, o, ("LoanId", creditos[k].Id), ("Number", $"LOP{desde + k + 1:D9}{o.Sequence}"),
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
                    foreach (var v in grafo.Vinculos)
                        Clonar(db, v, ("LoanId", creditos[k].Id), ("OperationId", v.OperationId is { } op ? operaciones[(k, op)].Id : null),
                            ("EmployeeIdAtPaymaster", $"VOL-{desde + k + 1:D6}"));
                }
                await db.SaveChangesAsync();
                return 0;
            });
        }
    }

    private sealed record Grafo(Loan Credito, List<LoanOperation> Operaciones, List<LoanTermsVersion> Condiciones, List<RepaymentScheduleVersion> Planes,
        List<RepaymentInstallment> Cuotas, List<LoanTransaction> Libro, LoanControl Control, List<LoanParty> Partes, List<LoanPayrollLink> Vinculos);

    /// <summary>Una copia de <paramref name="original"/> con Id y PublicId nuevos, sin token de concurrencia y con los cambios dados.</summary>
    private static T Clonar<T>(ApplicationDbContext db, T original, params (string Propiedad, object? Valor)[] cambios) where T : class
    {
        var valores = db.Entry(original).CurrentValues.Clone();
        var propiedades = valores.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        valores["Id"] = valores.Properties.Single(p => p.Name == "Id").ClrType == typeof(long) ? (object)0L : 0;
        if (propiedades.Contains("PublicId")) valores["PublicId"] = Guid.NewGuid();
        if (propiedades.Contains("RowVersion")) valores["RowVersion"] = null;
        foreach (var (propiedad, valor) in cambios.Where(c => propiedades.Contains(c.Propiedad))) valores[propiedad] = valor;
        var copia = (T)valores.ToObject();
        db.Add(copia);
        return copia;
    }
}
