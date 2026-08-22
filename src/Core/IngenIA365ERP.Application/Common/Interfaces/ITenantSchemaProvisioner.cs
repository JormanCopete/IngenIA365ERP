namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Deja el esquema de una cooperativa listo para usarse: lo crea si falta, lo
/// lleva al nivel actual de migraciones y le siembra sus catálogos, permisos y
/// roles.
///
/// <para>
/// Hace falta porque registrar una cooperativa guardaba su fila y nada más. El
/// esquema físico aparecía en el arranque siguiente, cuando el inicializador
/// recorría <c>ADM_Tenants</c>. Mientras tanto la cooperativa existía en la
/// consola y no tenía dónde guardar nada: había que reiniciar la API entre
/// registrarla y usarla.
/// </para>
///
/// <para>
/// Vive en Application y se implementa en Persistence: el Principio II prohíbe
/// que Application conozca EF Core.
/// </para>
/// </summary>
public interface ITenantSchemaProvisioner
{
    /// <param name="esquema">Nombre del esquema, tal como queda en <c>ADM_Tenants.SchemaName</c>.</param>
    /// <param name="identificador">
    /// Identificador de la cooperativa. Lo usa el sembrado para saber sobre qué
    /// esquema aplicar los seeders de alcance Tenant.
    /// </param>
    Task AprovisionarAsync(string esquema, string identificador, CancellationToken ct);
}
