using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.PasesDeIngreso;

/// <summary>
/// Feature 027: quién puede conceder un pase de ingreso a quién. Puro: recibe los hechos ya leídos y devuelve el primer motivo de rechazo, o
/// nada si se puede conceder. Lo usan la concesión del administrador, la del maestro y la ficha del usuario (para mostrar o no el botón).
/// </summary>
public static class ReglasDelPaseDeIngreso
{
    /// <summary>Decisión del dueño del 2026-11-26: como mucho 5 pases a la misma persona en 7 días.</summary>
    public const int TopePorUsuario = 5;
    public static readonly TimeSpan VentanaPorUsuario = TimeSpan.FromDays(7);

    /// <summary>Decisión del dueño del 2026-11-26: como mucho 20 pases por cooperativa en un día.</summary>
    public const int TopePorCooperativa = 20;
    public static readonly TimeSpan VentanaPorCooperativa = TimeSpan.FromDays(1);

    public sealed record Hechos(
        Guid Actor,
        bool ActorEsMaestro,
        Guid Destinatario,
        bool DestinatarioEsMaestro,
        bool DestinatarioEsAdministrador,
        bool DestinatarioMiembroActivo,
        bool DestinatarioActivo,
        bool CorreoVerificado,
        bool TieneSegundoFactor,
        bool OpcionEncendida,
        int HorasMaximasDeLaCooperativa,
        TimeSpan DuracionPedida,
        int ConcedidosAlUsuarioEnLaVentana,
        int ConcedidosEnLaCooperativaEnLaVentana);

    public sealed record Rechazo(string Codigo, string Mensaje);

    public static Rechazo? Evaluar(Hechos h)
    {
        if (!h.OpcionEncendida)
            return new("LoginPass.Disabled",
                "Esta cooperativa no permite pases de ingreso. Se enciende en Seguridad → Política de segundo factor.");
        if (h.DestinatarioEsMaestro)
            return new("LoginPass.TargetIsMaster", "Al administrador maestro no se le puede dar un pase de ingreso.");
        if (h.Actor == h.Destinatario)
            return new("LoginPass.Self", "No puede darse un pase de ingreso a sí mismo.");
        if (!h.DestinatarioActivo || !h.DestinatarioMiembroActivo)
            return new("LoginPass.NotMember", "La persona no tiene una cuenta activa en esta cooperativa.");
        if (h.DestinatarioEsAdministrador && !h.ActorEsMaestro)
            return new("LoginPass.TargetIsAdmin",
                "La persona es administradora de la cooperativa: su pase de ingreso lo da el administrador maestro.");
        if (!h.CorreoVerificado)
            return new("LoginPass.EmailNotVerified",
                "El correo de esta persona no está verificado: el pase no se le puede enviar. Use el reset del segundo factor con dos aprobaciones.");
        if (!h.TieneSegundoFactor)
            return new("LoginPass.NoSecondFactor",
                "La persona no tiene segundo factor inscrito: no necesita pase. Al entrar, el sistema le pide inscribirlo.");

        var maximo = Math.Clamp(h.HorasMaximasDeLaCooperativa, 1, LoginPass.HorasMaximas);
        if (h.DuracionPedida <= TimeSpan.Zero || h.DuracionPedida > TimeSpan.FromHours(maximo))
            return new("LoginPass.HoursOutOfRange", $"El pase dura entre 1 y {maximo} horas.");

        if (h.ConcedidosAlUsuarioEnLaVentana >= TopePorUsuario)
            return new("LoginPass.TooMany",
                $"A esta persona ya se le dieron {TopePorUsuario} pases en los últimos 7 días. Si perdió su app, use el reset del segundo factor.");
        if (h.ConcedidosEnLaCooperativaEnLaVentana >= TopePorCooperativa)
            return new("LoginPass.TooMany", $"La cooperativa ya dio {TopePorCooperativa} pases en las últimas 24 horas.");

        return null;
    }
}
