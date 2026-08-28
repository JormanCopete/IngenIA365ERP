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

    /// <summary>
    /// Si esta cooperativa permite recuperar el segundo factor por correo.
    /// <b>Apagado por defecto</b>, y eso no es prudencia decorativa: encenderlo
    /// significa aceptar que quien controle un buzón pueda, con la contraseña y un
    /// día de espera, retirarle el segundo factor a una persona.
    /// </summary>
    public bool AllowEmailRecovery { get; private set; }

    /// <summary>
    /// Cuántas horas espera una solicitud antes de poder ejecutarse. Es lo que
    /// convierte el ataque de silencioso e instantáneo en ruidoso y con tiempo para
    /// reaccionar: durante la espera la persona recibe el aviso y puede cancelar de
    /// un clic.
    ///
    /// <para>
    /// Mínimo <see cref="DemoraMinimaEnHoras"/>. Poner cero equivaldría a no tener
    /// demora, que es exactamente el diseño que se rechazó.
    /// </para>
    /// </summary>
    public int EmailRecoveryDelayHours { get; private set; } = DemoraPorDefectoEnHoras;

    public const int DemoraPorDefectoEnHoras = 24;
    public const int DemoraMinimaEnHoras = 1;

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

    /// <summary>
    /// Configura la recuperación por correo.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si se pide una demora menor que <see cref="DemoraMinimaEnHoras"/> teniéndola
    /// habilitada. Cero horas no es «recuperación rápida», es el diseño sin demora
    /// que se descartó: sin espera no hay aviso que llegue a tiempo ni cancelación
    /// posible, y el segundo factor pasa a valer lo que valga el buzón.
    /// </exception>
    public void ConfigurarRecuperacionPorCorreo(bool permitir, int demoraEnHoras)
    {
        if (permitir && demoraEnHoras < DemoraMinimaEnHoras)
        {
            throw new ArgumentOutOfRangeException(
                nameof(demoraEnHoras),
                demoraEnHoras,
                $"La demora mínima es {DemoraMinimaEnHoras} hora. Sin espera no hay aviso " +
                "que llegue a tiempo ni forma de cancelar.");
        }

        AllowEmailRecovery = permitir;

        // La demora se conserva aunque se apague, para que volver a encender no
        // reinicie en silencio un valor que alguien eligió.
        if (permitir) EmailRecoveryDelayHours = demoraEnHoras;
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
