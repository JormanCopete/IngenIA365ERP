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
    /// Cooperativas activas <b>y aprovisionadas</b> (<c>ProvisioningState = Ready</c>),
    /// con su base. Lo necesitan los trabajos de fondo, que no tienen peticion HTTP
    /// detras y por tanto ninguna cooperativa activa de la que tirar: con una base
    /// por cooperativa tienen que recorrerlas una a una, no barrer una tabla
    /// compartida.
    ///
    /// <para>
    /// Que excluya las que no estan en <c>Ready</c> no es un detalle: entre el alta y
    /// el final del aprovisionamiento la base existe sin tablas, y una que quedo en
    /// <c>Failed</c> no tiene nada que recorrer. Un trabajo de fondo que las abriera
    /// fallaria con «relation does not exist» cada quince segundos hasta que alguien
    /// mirara el log.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<TenantDirectoryEntry>> ListActiveAsync(CancellationToken ct);
}

/// <summary>Proyección mínima de un tenant para resolverlo en login.</summary>
public sealed record TenantDirectoryEntry(
    int Id,
    Guid PublicId,
    string Identifier,
    string Name,
    string SchemaName = "dbo",
    string? DatabaseName = null,
    string? ConnectionString = null);
