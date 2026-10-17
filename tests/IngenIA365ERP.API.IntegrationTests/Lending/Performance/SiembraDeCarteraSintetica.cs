using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Performance;

/// <summary>
/// La cartera sintética de las pruebas de volumen (feature 013; T0483, fase 24 T1069): copia en la base el grafo de un crédito desembolsado por HTTP
/// —crédito, operación de desembolso, condiciones, plan con sus cuotas, libro, control y partes— con números y PublicId propios, en tandas, para
/// medir el ciclo diario, el cierre y los informes sin pagar el tiempo de una carga de migración (que mide <see cref="ActivacionDeCarteraPerformanceTests"/>).
/// Todas las copias comparten el deudor de la plantilla. Sólo pruebas de volumen: escribe la base directamente, fuera de
/// <c>RegistroDeCartera</c>. (nuevo)
/// </summary>
internal static class SiembraDeCarteraSintetica
{
    public const int TamanoPorDefecto = 500;

    /// <summary>Los créditos de volumen pedidos en <c>CARTERA_CREDITOS_DE_RENDIMIENTO</c> o <paramref name="porDefecto"/>.</summary>
    public static int Creditos(int porDefecto) =>
        int.TryParse(Environment.GetEnvironmentVariable("CARTERA_CREDITOS_DE_RENDIMIENTO"), out var n) && n > 0 ? n : porDefecto;

    /// <summary>Copia <paramref name="copias"/> veces el grafo del crédito <paramref name="plantilla"/> en tandas de <paramref name="tamano"/>.</summary>
    public static async Task SembrarAsync(CentralIdentityApiFixture fx, Guid tenant, Guid plantilla, int copias, int tamano = TamanoPorDefecto)
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

        for (var desde = 0; desde < copias; desde += tamano)
        {
            var hasta = Math.Min(copias, desde + tamano);
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

    /// <summary>El percentil <paramref name="p"/> (0..1) de una muestra; cero si está vacía.</summary>
    public static double Percentil(IReadOnlyList<double> tiempos, double p)
    {
        if (tiempos.Count == 0) return 0;
        var ordenados = tiempos.Order().ToList();
        return ordenados[Math.Clamp((int)Math.Ceiling(ordenados.Count * p) - 1, 0, ordenados.Count - 1)];
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
}
