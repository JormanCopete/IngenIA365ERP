namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Directorio de cooperativas-tenant. Vive en la BD <c>IngenIA365ERP_Admin</c>
/// (separada de la BD operacional). Provee un canal de solo-lectura para que
/// los handlers de Application resuelvan un tenant por subdomain o NIT sin
/// acoplarse a EF/Persistence.
///
/// La implementación concreta se monta sobre <c>TenantDbContext</c> y la
/// cadena <c>ConnectionStrings:TenantConnection</c>.
/// </summary>
public interface ITenantDirectory
{
    Task<TenantDirectoryEntry?> FindBySubdomainOrNitAsync(string subdomainOrNit, CancellationToken ct);

    /// <summary>
    /// Cooperativas activas con su esquema. Lo necesitan los trabajos de fondo, que
    /// no tienen peticion HTTP detras y por tanto ninguna cooperativa activa de la
    /// que tirar: con aislamiento por esquema tienen que recorrerlas una a una, no
    /// barrer una tabla compartida.
    /// </summary>
    Task<IReadOnlyList<TenantDirectoryEntry>> ListActiveAsync(CancellationToken ct);
}

/// <summary>Proyección mínima de un tenant para resolverlo en login.</summary>
public sealed record TenantDirectoryEntry(
    int Id,
    Guid PublicId,
    string Identifier,
    string Name,
    string SchemaName = "dbo");
