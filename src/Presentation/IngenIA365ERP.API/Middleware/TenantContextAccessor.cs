using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.API.Middleware;

/// <summary>
/// La cooperativa de la petición en curso, leída de donde
/// <see cref="TenantResolutionMiddleware"/> la deja.
///
/// <para>
/// <b>Qué estaba roto.</b> Leía <c>Items["TenantInfo"] as ErpTenantInfo</c>, pero
/// el middleware guarda ahí un <see cref="Tenant"/> — dos tipos sin parentesco,
/// así que el <c>as</c> daba <c>null</c> en el 100% de las peticiones. Y sin
/// excepción: el aislamiento no se caía con un error, se degradaba mudo.
/// </para>
///
/// <para>
/// <b>Por qué importaba tanto.</b> <c>TenantId</c> devolvía cadena vacía, no
/// null. Aguas abajo la defensa está escrita como <c>?? "default"</c>, que
/// protege contra null y no contra vacío, así que el valor degradado se
/// propagaba tal cual. <c>MongoDbInitializer</c> compone
/// <c>$"audit_{tenantId}"</c>: el rastro de auditoría de todas las cooperativas
/// terminó en una única colección llamada literalmente <c>audit_</c>, con más de
/// treinta mil documentos. Devolver null hace que esos respaldos disparen.
/// </para>
///
/// <para>
/// Se leen las claves sueltas que el middleware ya escribe, en vez de exigirle
/// que además deposite un segundo objeto. El formato de <c>TenantId</c> es
/// <c>PublicId.ToString("N")</c>, la misma convención que ya usa
/// <c>RegisterTenantWithAdminCommandHandler</c> y que produjo las colecciones de
/// auditoría bien nombradas; unificarla aquí evita una tercera.
/// </para>
///
/// <para>
/// Singleton a propósito, y correcto: no cachea nada. Cada propiedad relee
/// <c>HttpContext.Items</c> en cada acceso.
/// </para>
/// </summary>
public class TenantContextAccessor(IHttpContextAccessor httpContext) : ICurrentTenantService
{
    private IDictionary<object, object?>? Items => httpContext.HttpContext?.Items;

    private T? Leer<T>(string clave) =>
        Items is not null && Items.TryGetValue(clave, out var valor) && valor is T tipado
            ? tipado
            : default;

    public string? TenantId =>
        Leer<Guid>("TenantPublicId") is var id && id != Guid.Empty ? id.ToString("N") : null;

    public string? TenantName => Leer<Tenant>("TenantInfo")?.Name;

    public string? Schema => Leer<string>("TenantSchema");

    /// <summary>
    /// Siempre null. La cadena por cooperativa existe en el modelo
    /// (<c>ErpTenantInfo.ConnectionString</c>) y nadie la lee nunca: todas las
    /// cooperativas comparten instancia y se separan por esquema. Devolver algo
    /// aquí insinuaría un aislamiento por instancia que no existe.
    /// </summary>
    public string? ConnectionString => null;
}
