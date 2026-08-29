using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Abre un <see cref="ApplicationDbContext"/> sobre la base de una cooperativa
/// concreta, que no tiene por qué ser la de la petición en curso.
///
/// <para>
/// Los interceptores se reenganchan a mano: al construir las opciones aquí en
/// vez de tomarlas del contenedor, la auditoría, el borrado lógico y el control
/// de concurrencia no viajarían solos. Olvidarlo no daría error — daría filas
/// escritas sin rastro.
/// </para>
/// </summary>
internal sealed class TenantDbContextFactory(
    TenantConnectionResolver resolutor,
    IDbProviderConfigurator configurador,
    IEnumerable<ISaveChangesInterceptor> interceptores,
    ICurrentUserService? usuarioActual = null) : ITenantDbContextFactory
{
    public ITenantDbScope Abrir(string nombreDeBase, string? cadenaPropia = null)
    {
        var cadena = resolutor.Resolver(nombreDeBase, cadenaPropia);

        var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
        constructor.AddInterceptors(interceptores);
        configurador.Configure(constructor, cadena, MigrationsTarget.Application);

        return new Ambito(new ApplicationDbContext(
            constructor.Options,
            new ErpTenantInfo { SchemaName = "dbo", ConnectionString = cadena },
            usuarioActual));
    }

    private sealed class Ambito(ApplicationDbContext db) : ITenantDbScope
    {
        public IApplicationDbContext Db => db;

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
