using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Calcula migraciones pendientes por base y por esquema de tenant (FR-011,
/// FR-021, data-model §3). Con AutoMigrate=false y pendientes, el inicializador
/// hace fail-fast enumerandolas; el seeding tambien se rehusa a correr sobre
/// esquema desactualizado.
/// </summary>
public sealed class PendingMigrationsGuard(
    AdminDbContext adminDb,
    DbContextOptions<ApplicationDbContext> appDbOptions,
    MultiTenancy.TenantConnectionResolver resolutorDeConexion,
    Providers.IDbProviderConfigurator configurador)
{
    public sealed record PendingReport(
        IReadOnlyList<string> Admin,
        IReadOnlyList<string> Application,
        IReadOnlyDictionary<string, IReadOnlyList<string>> TenantSchemas)
    {
        public bool HasAny => Admin.Count > 0 || Application.Count > 0 || TenantSchemas.Any(t => t.Value.Count > 0);

        public string Describe()
        {
            var parts = new List<string>();
            if (Admin.Count > 0) parts.Add($"admin: {string.Join(", ", Admin)}");
            if (Application.Count > 0) parts.Add($"operativa: {string.Join(", ", Application)}");
            foreach (var (schema, pending) in TenantSchemas.Where(t => t.Value.Count > 0))
                parts.Add($"tenant {schema}: {string.Join(", ", pending)}");
            return string.Join(" | ", parts);
        }
    }

    public async Task<PendingReport> ComputeAsync(CancellationToken ct)
    {
        var adminReachable = await adminDb.Database.CanConnectAsync(ct);
        var admin = await SafePendingAsync(adminDb, ct);
        // Anclado a dbo: el historial vive en dbo.__EFMigrationsHistory en los dos
        // proveedores, asi que un modelo apuntando al esquema de una cooperativa
        // leeria el historial de dbo y concluiria "sin pendientes" sin tocar nada.
        await using var appDb = new ApplicationDbContext(appDbOptions);
        var application = await SafePendingAsync(appDb, ct);

        var tenants = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!adminReachable)
        {
            // BD admin inexistente ⇒ no hay directorio de tenants que consultar;
            // el reporte "admin: todo pendiente" ya cuenta la historia completa.
            return new PendingReport(admin, application, tenants);
        }

        // Se comprueba la BASE de cada cooperativa, no su esquema.
        //
        // Antes preguntaba por esquemas, y eso dejo de significar nada al pasar a
        // base por cooperativa: seguia mirando esquemas que ya no existen y los
        // daba por sin migrar, bloqueando el sembrado y tumbando el arranque
        // entero. Mientras la basura de esquemas siguio ahi, el guarda estaba
        // validando basura y nadie lo noto.
        var cooperativas = await adminDb.Tenants
            .Where(t => t.IsActive)
            .Select(t => new { t.Name, t.DatabaseName, t.SchemaName, t.ConnectionString })
            .ToListAsync(ct);

        foreach (var c in cooperativas)
        {
            var baseDeDatos = c.DatabaseName ?? c.SchemaName;
            if (string.IsNullOrWhiteSpace(baseDeDatos) ||
                baseDeDatos.Equals("dbo", StringComparison.OrdinalIgnoreCase))
            {
                continue; // la base plantilla la cubre el chequeo "operativa"
            }

            var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
            configurador.Configure(
                constructor,
                resolutorDeConexion.Resolver(baseDeDatos, c.ConnectionString, c.Name),
                Providers.MigrationsTarget.Application);

            await using var db = new ApplicationDbContext(constructor.Options);
            tenants[baseDeDatos] = await SafePendingAsync(db, ct);
        }

        return new PendingReport(admin, application, tenants);
    }

    /// <summary>Base inexistente todavia ⇒ todas las migraciones estan pendientes.</summary>
    private static async Task<IReadOnlyList<string>> SafePendingAsync(
        Microsoft.EntityFrameworkCore.DbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct))
            return context.Database.GetMigrations().ToList();
        return (await context.Database.GetPendingMigrationsAsync(ct)).ToList();
    }
}
