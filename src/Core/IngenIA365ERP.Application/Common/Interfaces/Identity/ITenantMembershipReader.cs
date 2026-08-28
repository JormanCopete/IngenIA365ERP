using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Lectura cacheada de membresías activas para el flujo de login (research D-07).
/// Cache en Redis con TTL 60s + invalidación pub/sub vía
/// <see cref="IMembershipChangedNotifier"/> al mutar membresías o políticas.
/// </summary>
public interface ITenantMembershipReader
{
    /// <summary>Lista de membresías activas (Status=Active) del usuario, con flags
    /// de tenant admin y política MFA. Lee de cache si está caliente.</summary>
    Task<IReadOnlyList<ActiveMembershipInfo>> GetActiveMembershipsAsync(
        Guid centralUserId,
        CancellationToken ct);

    /// <summary>Devuelve true si el usuario tiene una membresía Active con el tenant indicado.</summary>
    Task<bool> IsMemberOfTenantAsync(
        Guid centralUserId,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>Invalida la cache local del proceso (no propaga pub/sub —
    /// para eso usar <see cref="IMembershipChangedNotifier.PublishAsync"/>).</summary>
    Task InvalidateLocalCacheAsync(Guid centralUserId, CancellationToken ct);
}

/// <param name="MetodosAceptados">
/// Qué métodos de segundo factor acepta esa cooperativa. Sólo se consulta cuando
/// <paramref name="IsMfaRequiredByTenant"/> es true.
///
/// <para>
/// Valor por defecto <c>Todos</c> y no <c>Ninguno</c>: este record se
/// <b>serializa a Redis</b>, y una entrada escrita por un binario anterior no
/// trae este campo. Con <c>Ninguno</c> por defecto, esas entradas se leerían como
/// «esta cooperativa no acepta nada» y encerrarían a todos sus miembros hasta que
/// caducara la caché. El prefijo de clave se subió a <c>v2</c> por el mismo motivo
/// —ver <c>RedisTenantMembershipReader</c>—; el default es el cinturón, el prefijo
/// son los tirantes.
/// </para>
/// </param>
/// <param name="PermiteRecuperacionPorCorreo">
/// Si esa cooperativa acepta que el segundo factor se recupere por correo.
/// <b>Por defecto false</b>, al revés que los métodos: aquí el valor seguro es el
/// restrictivo, porque una entrada de caché vieja que se leyera como «sí permite»
/// habilitaría durante un minuto una vía que la cooperativa tiene apagada.
/// </param>
/// <param name="HorasDeDemoraDeRecuperacion">
/// Cuánto espera una solicitud antes de poder ejecutarse. Sólo se lee cuando la
/// anterior es true.
/// </param>
public sealed record ActiveMembershipInfo(
    Guid TenantId,
    string TenantName,
    bool IsTenantAdmin,
    bool IsMfaRequiredByTenant,
    MetodosMfa MetodosAceptados = ConversionDeMetodosMfa.Todos,
    bool PermiteRecuperacionPorCorreo = false,
    int HorasDeDemoraDeRecuperacion = TenantMfaPolicy.DemoraPorDefectoEnHoras);
