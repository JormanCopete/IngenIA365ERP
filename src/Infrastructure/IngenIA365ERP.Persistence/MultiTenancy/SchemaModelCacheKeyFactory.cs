using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Feature 004 (spike T025): el modelo de <see cref="ApplicationDbContext"/> se
/// parametriza por esquema de tenant (<c>HasDefaultSchema</c>), por lo que la
/// cache de modelos de EF DEBE incluir el esquema en la clave. Sin esto, el
/// modelo del primer tenant materializado quedaba cacheado para todos
/// (bug latente pre-004).
/// </summary>
public sealed class SchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(Microsoft.EntityFrameworkCore.DbContext context, bool designTime)
    {
        var schema = context is ApplicationDbContext app ? app.TenantSchema : null;
        return (context.GetType(), schema ?? "dbo", designTime);
    }
}
