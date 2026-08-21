using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Abre un <see cref="ApplicationDbContext"/> sobre el esquema que se le pida.
///
/// <para>
/// Es el mismo patrón que <c>SeedOrchestrator</c> ya usaba para sembrar cada
/// esquema: construir el contexto a mano pasándole el tenant, en vez de pedirlo
/// al contenedor. Las opciones salen del contenedor, así que los interceptores
/// de auditoría, borrado lógico y control de concurrencia viajan con ellas.
/// </para>
/// </summary>
internal sealed class TenantDbContextFactory(
    DbContextOptions<ApplicationDbContext> opciones,
    ICurrentUserService? usuarioActual = null) : ITenantDbContextFactory
{
    public ITenantDbScope Abrir(string esquema)
    {
        if (string.IsNullOrWhiteSpace(esquema))
        {
            throw new ArgumentException(
                "Hace falta el nombre del esquema. Abrir la base operativa sin decir de qué " +
                "cooperativa es escribiría en el esquema equivocado.", nameof(esquema));
        }

        return new Ambito(new ApplicationDbContext(
            opciones,
            new ErpTenantInfo { SchemaName = esquema },
            usuarioActual));
    }

    private sealed class Ambito(ApplicationDbContext db) : ITenantDbScope
    {
        public IApplicationDbContext Db => db;

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
