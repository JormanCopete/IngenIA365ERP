using System.Diagnostics;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Warmup;

/// <summary>Opciones de <c>Precalentamiento</c> (sección de appsettings). Todo con valor por defecto.</summary>
public sealed class PrecalentamientoOptions
{
    public const string SectionName = "Precalentamiento";

    /// <summary>Apagado: el pod queda listo sin ejercitar nada (para pruebas y herramientas).</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>
    /// Tope total. Pasado, el pod queda listo igual: un precalentamiento que no termina no debe dejar
    /// al pod fuera del servicio para siempre. Debe caber en el margen del Deployment.
    /// </summary>
    public int TopeSegundos { get; set; } = 90;

    /// <summary>Cooperativas extra a las que sólo se les abre el pool de conexiones (la primera corre las consultas).</summary>
    public int MaximoDeCooperativasParaElPool { get; set; } = 10;
}

/// <summary>El veredicto lo escribe <see cref="PrecalentamientoDeLaApi"/> y lo lee el health check.</summary>
public sealed class EstadoDePrecalentamiento
{
    private volatile bool _terminado;
    private volatile string _detalle = "Precalentamiento pendiente";

    public bool Terminado => _terminado;
    public string Detalle => _detalle;

    public void Terminar(string detalle)
    {
        _detalle = detalle;
        _terminado = true;
    }
}

/// <summary>
/// «Listo» sólo cuando el precalentamiento terminó (bien, mal o por tope). Es de readiness a propósito:
/// el pod no recibe tráfico hasta tener compilados los modelos y planes de consulta, y nunca de liveness,
/// porque un precalentamiento lento no es motivo para reiniciar el contenedor.
/// </summary>
public sealed class PrecalentamientoHealthCheck(EstadoDePrecalentamiento estado) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(estado.Terminado
            ? HealthCheckResult.Healthy(estado.Detalle)
            : HealthCheckResult.Unhealthy(estado.Detalle));
}

/// <summary>
/// Tras cada despliegue las primeras peticiones pesadas pagaban el costo de compilar el modelo de EF,
/// compilar los planes de las consultas y abrir conexiones (en PDN un cobro del POS tardó 15,8 s a los
/// cinco minutos de arrancar). Aquí se paga antes de pasar a <i>ready</i>.
///
/// <para>
/// Sólo lee: ninguna escritura, ningún <c>SaveChanges</c>, sin auditoría. Las consultas replican la FORMA
/// de las del camino caliente (cobro del POS, contabilizar, caja) con un identificador que no existe, así
/// que compilan y ejecutan y devuelven vacío. Corre en segundo plano, después de que Kestrel escucha, para
/// que <c>/health/live</c> responda mientras tanto. Cualquier fallo se registra y el pod queda listo igual.
/// </para>
/// </summary>
public sealed class PrecalentamientoDeLaApi(
    IServiceScopeFactory scopes,
    DatabaseReadiness baseDeDatos,
    EstadoDePrecalentamiento estado,
    IOptions<PrecalentamientoOptions> opciones,
    ILogger<PrecalentamientoDeLaApi> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // BackgroundService arranca dentro de StartAsync: ceder el hilo para no retener el arranque del host.
        await Task.Yield();

        var opts = opciones.Value;
        if (!opts.Habilitado)
        {
            estado.Terminar("Precalentamiento deshabilitado");
            return;
        }

        var reloj = Stopwatch.StartNew();
        using var tope = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        tope.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, opts.TopeSegundos)));
        try
        {
            // El inicializador migra antes de que arranque Kestrel; si aún no está listo, no hay nada que calentar.
            while (!baseDeDatos.IsReady) await Task.Delay(500, tope.Token);

            await using var ambito = scopes.CreateAsyncScope();
            var cooperativas = await CalentarAdminAsync(ambito.ServiceProvider, tope.Token);
            await CalentarCooperativasAsync(ambito.ServiceProvider, cooperativas, opts, tope.Token);

            estado.Terminar($"Precalentado en {reloj.ElapsedMilliseconds} ms");
            logger.LogInformation("[Precalentamiento] Terminado en {Ms} ms.", reloj.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            estado.Terminar($"Precalentamiento cortado por el tope de {opts.TopeSegundos} s");
            logger.LogWarning("[Precalentamiento] Tope de {Seg} s alcanzado tras {Ms} ms; el pod queda listo igual.",
                opts.TopeSegundos, reloj.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            estado.Terminar("Detenido");
        }
        catch (Exception ex)
        {
            estado.Terminar("Precalentamiento fallido (ignorado)");
            logger.LogWarning(ex, "[Precalentamiento] Falló tras {Ms} ms; el pod queda listo igual.", reloj.ElapsedMilliseconds);
        }
    }

    private static async Task<IReadOnlyList<TenantDirectoryEntry>> CalentarAdminAsync(IServiceProvider sp, CancellationToken ct)
    {
        var admin = sp.GetRequiredService<IAdminDbContext>();
        // Compila el modelo de admin y los planes de los caminos de ingreso y elección de cooperativa.
        _ = await admin.Tenants.AsNoTracking().Where(t => t.IsActive).Select(t => t.Id).Take(1).ToListAsync(ct);
        _ = await admin.TenantMemberships.AsNoTracking().Where(m => m.Id == 0).Select(m => m.Id).ToListAsync(ct);
        _ = await admin.PlatformMfaPolicies.AsNoTracking().Take(1).ToListAsync(ct);
        _ = await admin.TenantMfaPolicies.AsNoTracking().Where(p => p.Id == 0).ToListAsync(ct);

        return await sp.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
    }

    private async Task CalentarCooperativasAsync(
        IServiceProvider sp, IReadOnlyList<TenantDirectoryEntry> cooperativas, PrecalentamientoOptions opts, CancellationToken ct)
    {
        if (cooperativas.Count == 0)
        {
            logger.LogInformation("[Precalentamiento] Sin cooperativas activas: sólo se calentó la base administrativa.");
            return;
        }

        var fabrica = sp.GetRequiredService<ITenantDbContextFactory>();

        // El modelo de EF y los planes de consulta se comparten entre cooperativas (misma clave de modelo,
        // mismo esquema): basta una para compilarlos. Las demás sólo abren y devuelven una conexión al pool.
        var primera = cooperativas[0];
        var fase = Stopwatch.StartNew();
        await using (var ambito = fabrica.Abrir(primera.DatabaseName ?? primera.SchemaName, primera.ConnectionString))
        {
            await ConsultasCalientesAsync(ambito.Db, ct);
            logger.LogInformation("[Precalentamiento] Consultas del POS y la contabilidad en {Ms} ms.", fase.ElapsedMilliseconds);
        }
        fase.Restart();

        foreach (var otra in cooperativas.Skip(1).Take(opts.MaximoDeCooperativasParaElPool))
        {
            await using var ambito = fabrica.Abrir(otra.DatabaseName ?? otra.SchemaName, otra.ConnectionString);
            _ = await ambito.Db.Warehouses.AsNoTracking().Select(w => w.Id).Take(1).ToListAsync(ct);
        }
        logger.LogInformation("[Precalentamiento] Pool de {N} cooperativa(s) más en {Ms} ms.",
            Math.Min(cooperativas.Count - 1, opts.MaximoDeCooperativasParaElPool), fase.ElapsedMilliseconds);
    }

    /// <summary>
    /// Formas de consulta del camino del POS y la contabilización. Con seguimiento donde el camino real lo
    /// usa (el plan compilado distingue seguimiento de no seguimiento). Todas por un Guid o Id inexistente.
    /// </summary>
    private static async Task ConsultasCalientesAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var ninguno = Guid.Empty;

        // Documento de inventario: relectura al confirmar (VistaDeDocumentos / ConfirmacionDeDocumento).
        _ = await db.InventoryDocuments.Include(d => d.Lines).Include(d => d.DocumentType!).ThenInclude(t => t.Warehouses)
            .AsSplitQuery().FirstOrDefaultAsync(d => d.PublicId == ninguno, ct);
        _ = await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstOrDefaultAsync(t => t.PublicId == ninguno, ct);

        // Caja y puntos de venta.
        _ = await db.CashRegisters.AsNoTracking().Include(c => c.DocumentTypes.Where(t => !t.IsDeleted)).ThenInclude(t => t.DocumentType)
            .FirstOrDefaultAsync(c => c.PublicId == ninguno, ct);
        _ = await db.CashSessions.AsNoTracking().Where(s => s.PublicId == ninguno).Select(s => s.Id).ToListAsync(ct);

        // Productos y códigos de barras (búsqueda del POS).
        _ = await db.Products.AsNoTracking().Include(p => p.BaseUnit).FirstOrDefaultAsync(p => p.PublicId == ninguno, ct);
        _ = await db.ProductBarcodes.AsNoTracking().Include(b => b.ProductUnit).ThenInclude(u => u!.Unit)
            .Where(b => b.Id == 0).ToListAsync(ct);
        _ = await db.People.AsNoTracking().Where(p => p.PublicId == ninguno).Select(p => p.Id).ToListAsync(ct);

        // Contabilización y comprobantes.
        _ = await db.AccountingDocuments.Include(d => d.VoucherType).Include(d => d.Lines).ThenInclude(l => l.CrossDocumentType)
            .FirstOrDefaultAsync(d => d.PublicId == ninguno, ct);
        _ = await db.ChartOfAccounts.Include(c => c.TaxRates).Where(c => c.Id == 0).ToListAsync(ct);
        _ = await db.VoucherTypes.AsNoTracking().Take(1).ToListAsync(ct);
    }
}

public static class PrecalentamientoExtensions
{
    /// <remarks>El <c>AddHostedService</c> va en <c>API/Program.cs</c> (regla de arquitectura T47).</remarks>
    public static IServiceCollection AddPrecalentamiento(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PrecalentamientoOptions>(configuration.GetSection(PrecalentamientoOptions.SectionName));
        services.AddSingleton<EstadoDePrecalentamiento>();
        services.AddHealthChecks().AddCheck<PrecalentamientoHealthCheck>("precalentamiento", tags: [HealthChecks.HealthCheckExtensions.ReadyTag]);
        return services;
    }
}
