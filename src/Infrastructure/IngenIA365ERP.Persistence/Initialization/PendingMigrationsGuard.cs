using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Calcula migraciones pendientes por base (FR-011, FR-021, data-model §3). Con
/// AutoMigrate=false y pendientes, el inicializador hace fail-fast enumerandolas;
/// el seeding tambien se rehusa a correr sobre una base desactualizada.
///
/// <para>
/// Tiene dos modos y la diferencia importa. El de arranque mira TODAS las bases,
/// porque su pregunta es «¿esta el despliegue al dia?». El de sembrado mira solo
/// las bases que esa ejecucion va a tocar, porque su pregunta es otra: «¿puedo
/// escribir aqui?». Confundirlos es lo que hizo que una migracion pendiente en
/// coop_alfa abortara el aprovisionamiento de coop_beta.
/// </para>
/// </summary>
/// <para>
/// <b>No recibe <c>DbContextOptions</c> del contenedor.</b> Construirlas exige la
/// cooperativa del ambito, y este guarda corre tambien donde no hay ninguna:
/// dentro de POST /api/saas/tenants/with-admin, que es la peticion que CREA una
/// cooperativa. Pedirlas ahi lanzaba, el aprovisionamiento se abortaba, y la
/// cooperativa quedaba registrada sin base y sin roles — con su primer
/// administrador sin un solo permiso. Se reparaba sola en el siguiente arranque,
/// asi que en desarrollo parecia funcionar.
/// </para>
public sealed class PendingMigrationsGuard(
    AdminDbContext adminDb,
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

    /// <summary>
    /// Estado de TODO el despliegue: admin, plantilla y cada cooperativa activa.
    /// Es la pregunta del arranque.
    /// </summary>
    public Task<PendingReport> ComputeAsync(CancellationToken ct) => ComputeAsync(ct, null);

    /// <summary>
    /// Estado de las bases indicadas y nada mas.
    ///
    /// <para>
    /// <paramref name="soloEstas"/> son los objetivos que el llamador va a
    /// escribir, tal cual: <c>null</c> dentro de la lista significa la plantilla,
    /// igual que en el sembrado. Se comprueban con las MISMAS coordenadas con las
    /// que despues se abre la conexion —<c>SchemaName</c>, cadena propia, nombre—
    /// para que no pueda darse por buena una base y escribirse en otra.
    /// </para>
    ///
    /// <para>
    /// Una lista vacia comprueba solo la base administrativa. Pasar <c>null</c>
    /// como lista entera vuelve al modo de arranque.
    /// </para>
    /// </summary>
    public async Task<PendingReport> ComputeAsync(
        CancellationToken ct, IReadOnlyCollection<ErpTenantInfo?>? soloEstas)
    {
        var adminReachable = await adminDb.Database.CanConnectAsync(ct);
        var admin = await SafePendingAsync(adminDb, ct);

        var tenants = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        if (soloEstas is not null)
        {
            // Acotado: ni se consulta el directorio de cooperativas. Lo que no se
            // va a tocar no puede bloquear lo que si.
            var plantilla = soloEstas.Any(t => t is null)
                ? await PlantillaPendientesAsync(ct)
                : Array.Empty<string>();

            foreach (var t in soloEstas)
            {
                if (t is null) continue;
                await AnotarAsync(tenants, t.SchemaName, t.ConnectionString, t.Name, ct);
            }

            return new PendingReport(admin, plantilla, tenants);
        }

        // Anclado a dbo: el historial vive en dbo.__EFMigrationsHistory en los dos
        // proveedores, asi que un modelo apuntando al esquema de una cooperativa
        // leeria el historial de dbo y concluiria "sin pendientes" sin tocar nada.
        var application = await PlantillaPendientesAsync(ct);

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
            await AnotarAsync(tenants, c.DatabaseName ?? c.SchemaName, c.ConnectionString, c.Name, ct);

        return new PendingReport(admin, application, tenants);
    }

    private async Task<IReadOnlyList<string>> PlantillaPendientesAsync(CancellationToken ct)
    {
        var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
        configurador.Configure(
            constructor, resolutorDeConexion.Plantilla, Providers.MigrationsTarget.Application);

        await using var appDb = new ApplicationDbContext(constructor.Options);
        return await SafePendingAsync(appDb, ct);
    }

    /// <summary>Anota el estado de una base de cooperativa en el reporte.</summary>
    private async Task AnotarAsync(
        IDictionary<string, IReadOnlyList<string>> destino,
        string? baseDeDatos,
        string? cadenaPropia,
        string? nombre,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseDeDatos) ||
            baseDeDatos.Equals("dbo", StringComparison.OrdinalIgnoreCase))
        {
            return; // la base plantilla la cubre el chequeo "operativa"
        }

        var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
        configurador.Configure(
            constructor,
            resolutorDeConexion.Resolver(baseDeDatos, cadenaPropia, nombre),
            Providers.MigrationsTarget.Application);

        await using var db = new ApplicationDbContext(constructor.Options);
        destino[baseDeDatos] = await SafePendingAsync(db, ct);
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
