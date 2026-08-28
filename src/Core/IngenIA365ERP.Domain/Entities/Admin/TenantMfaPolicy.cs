using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Política "MFA obligatorio" por tenant (FR-003a a FR-003d).
/// Maps to [dbo].[ADM_TenantMfaPolicies]. Único por TenantId.
///
/// <para>
/// Desde que hay dos métodos de segundo factor, la política dice dos cosas: SI se
/// exige (<see cref="IsRequired"/>) y CUÁLES se aceptan
/// (<see cref="AllowedMethodsMask"/>). Son independientes: una cooperativa puede
/// no exigir nada y tener igualmente una lista de métodos preferida, que no le
/// hace daño a nadie porque la máscara sólo se consulta cuando se exige.
/// </para>
/// </summary>
public class TenantMfaPolicy : AuditableEntity
{
    public Guid TenantId { get; private set; }

    public bool IsRequired { get; private set; }

    /// <summary>
    /// Qué métodos acepta. Por defecto, todos: encender el segundo factor sin decir
    /// nada más significa «que tengan algo», no «que tengan justo esto».
    ///
    /// <para>
    /// <b>Nunca puede quedar vacía mientras se exija segundo factor.</b> Una
    /// cooperativa que exige MFA sin aceptar ningún método encierra a todos sus
    /// miembros a la vez, y no hay pantalla que los saque: no habría nada que
    /// inscribir. Lo defienden <see cref="Enable"/> y <see cref="PermitirMetodos"/>.
    /// </para>
    /// </summary>
    public MetodosMfa AllowedMethodsMask { get; private set; } = ConversionDeMetodosMfa.Todos;

    public DateTime? ActivatedAt { get; private set; }
    public Guid? ActivatedByUserId { get; private set; }

    public DateTime? DeactivatedAt { get; private set; }
    public Guid? DeactivatedByUserId { get; private set; }

    // EF Core
    private TenantMfaPolicy() { }

    /// <summary>Crea la política inicial para un tenant (por defecto deshabilitada).</summary>
    public static TenantMfaPolicy CreateForTenant(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            IsRequired = false,
            AllowedMethodsMask = ConversionDeMetodosMfa.Todos,
        };

    /// <summary>
    /// Cambia qué métodos acepta. Va aparte de <see cref="Enable"/>/<see cref="Disable"/>
    /// porque no es lo mismo: aquellos sellan <c>ActivatedAt</c>/<c>DeactivatedAt</c>,
    /// que describen cuándo se encendió o apagó la exigencia — no cuándo cambió el
    /// juego de métodos.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si se intenta dejar sin métodos una política que exige segundo factor.
    /// </exception>
    public void PermitirMetodos(MetodosMfa metodos)
    {
        if (IsRequired && metodos == MetodosMfa.Ninguno)
        {
            throw new InvalidOperationException(
                "Una cooperativa que exige segundo factor tiene que aceptar al menos un método. " +
                "Sin ninguno, sus miembros no podrían entrar ni tendrían nada que inscribir.");
        }

        AllowedMethodsMask = metodos;
    }

    /// <summary>Activa la política. Idempotente: si ya está activa, no hace nada.</summary>
    /// <exception cref="InvalidOperationException">
    /// Si la máscara está vacía. Se comprueba aquí y no sólo en
    /// <see cref="PermitirMetodos"/> porque el orden de las dos llamadas no está
    /// garantizado, y vaciar primero y exigir después llegaría al mismo estado
    /// imposible por la puerta de atrás.
    /// </exception>
    public void Enable(Guid byUserId, DateTime now)
    {
        if (AllowedMethodsMask == MetodosMfa.Ninguno)
        {
            throw new InvalidOperationException(
                "No se puede exigir segundo factor sin aceptar ningún método.");
        }

        if (IsRequired) return;

        IsRequired = true;
        ActivatedAt = now;
        ActivatedByUserId = byUserId;
        DeactivatedAt = null;
        DeactivatedByUserId = null;
    }

    /// <summary>Desactiva la política. Idempotente.</summary>
    public void Disable(Guid byUserId, DateTime now)
    {
        if (!IsRequired) return;

        IsRequired = false;
        DeactivatedAt = now;
        DeactivatedByUserId = byUserId;
    }
}
