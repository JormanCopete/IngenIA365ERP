using IngenIA365ERP.Application.Payroll.Concepts;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding;

/// <summary>
/// Reaplica la semilla de nómina (conceptos, parámetros legales, comprobante NM) sobre la
/// base de la cooperativa ACTIVA —el <see cref="ApplicationDbContext"/> ya resuelto al
/// tenant de la petición—, con los mismos seeders idempotentes del arranque. Sólo inserta
/// lo que falta; nunca actualiza lo existente (D-10). Es lo que dispara
/// <c>POST /api/payroll/concept-definitions/seed</c> desde la pantalla.
/// </summary>
public sealed class PayrollSeedApplier(ApplicationDbContext tenantDb, IHostEnvironment environment, ILogger<PayrollSeedApplier> logger) : IPayrollSeedApplier
{
    public async Task<IReadOnlyList<(string Seeder, int Inserted)>> ReapplyAsync(CancellationToken ct)
    {
        var context = new SeedContext { TenantDb = tenantDb, EnvironmentName = environment.EnvironmentName, Logger = logger };
        IDataSeeder[] seeders = [new PayrollPlansSeeder(), new PayrollConceptDefinitionsSeeder(), new PayrollLegalParametersSeeder(), new PayrollVoucherTypeSeeder()];

        var resultado = new List<(string, int)>();
        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            var insertadas = await seeder.SeedAsync(context, ct);
            logger.LogInformation("Semilla de nómina reaplicada: {Seeder} insertó {Inserted} fila(s).", seeder.GetType().Name, insertadas);
            resultado.Add((seeder.GetType().Name, insertadas));
        }
        return resultado;
    }
}
