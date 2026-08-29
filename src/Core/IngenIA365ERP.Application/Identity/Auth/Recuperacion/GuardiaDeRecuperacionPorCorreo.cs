using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.Auth.Recuperacion;

/// <summary>
/// Decide si una persona puede recuperar su segundo factor por correo, y cuánto
/// tiene que esperar.
///
/// <para>
/// La pregunta no es trivial porque la recuperación ocurre <b>antes de elegir
/// cooperativa</b>: en ese momento la persona no está «en» ninguna, pero puede
/// pertenecer a varias con políticas distintas. Y lo que se recupera —el segundo
/// factor— es uno solo para todas.
/// </para>
///
/// <para>
/// <b>Manda la más estricta, y la demora es la más larga.</b> Basta una
/// cooperativa que exija segundo factor y no permita recuperarlo por correo para
/// que no se pueda: si bastara una permisiva, cualquiera podría entrar en la
/// cooperativa exigente pidiendo la recuperación «por» la otra. La política más
/// estricta dejaría de servir para nada en cuanto su gente perteneciera a una
/// segunda cooperativa, que es justamente lo normal en este sistema.
/// </para>
/// </summary>
public static class GuardiaDeRecuperacionPorCorreo
{
    /// <param name="Permitida">
    /// Falso también cuando la persona no pertenece a ninguna cooperativa que exija
    /// segundo factor: no hay política que autorice nada, y sin política explícita
    /// esto queda apagado. Es la dirección segura del error.
    /// </param>
    public readonly record struct Veredicto(bool Permitida, TimeSpan Demora, string? Motivo);

    /// <summary>Vigencia del enlace, contada desde que se emite.</summary>
    public static readonly TimeSpan VigenciaDelEnlace = TimeSpan.FromDays(7);

    public static Veredicto Evaluar(IReadOnlyList<ActiveMembershipInfo> cooperativas)
    {
        var exigentes = cooperativas.Where(m => m.IsMfaRequiredByTenant).ToList();

        if (exigentes.Count == 0)
        {
            // Sin ninguna cooperativa que exija segundo factor, esta vía no tiene
            // sentido: la persona puede retirarlo desde su perfil con la sesión
            // abierta, que es un camino mucho más corto y no depende del buzón.
            return new Veredicto(false, TimeSpan.Zero,
                "Ninguna de tus empresas exige segundo factor: podés gestionarlo desde tu perfil.");
        }

        var prohiben = exigentes.Where(m => !m.PermiteRecuperacionPorCorreo).ToList();
        if (prohiben.Count > 0)
        {
            return new Veredicto(false, TimeSpan.Zero,
                $"'{prohiben[0].TenantName}' no permite recuperar el segundo factor por correo. " +
                "Usá un código de respaldo, o pedile a dos administradores que lo restablezcan.");
        }

        // La más larga de las demoras. Con una cooperativa que espera 24 horas y
        // otra que espera 1, esperar 1 haría que la política de 24 no valiera nada
        // para quien pertenece a las dos.
        //
        // Y con suelo. La entidad ya impide guardar menos del mínimo, pero esta
        // lectura llega de una columna que también pudo escribir una migración o
        // un UPDATE directo, y un cero aquí significaría «sin espera» — que es el
        // diseño que se rechazó, no un caso límite. Se defiende en los dos sitios
        // porque el coste es una línea y el fallo es silencioso.
        var horas = Math.Max(
            exigentes.Max(m => m.HorasDeDemoraDeRecuperacion),
            TenantMfaPolicy.DemoraMinimaEnHoras);

        return new Veredicto(true, TimeSpan.FromHours(horas), null);
    }
}
