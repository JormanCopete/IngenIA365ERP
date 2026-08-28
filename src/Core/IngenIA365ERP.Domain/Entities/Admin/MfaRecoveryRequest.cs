using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Una solicitud de recuperación del segundo factor por correo. Vive en
/// <c>ADM_MfaRecoveryRequests</c>.
///
/// <para>
/// <b>La objeción, dicha por escrito: si el correo puede borrar el segundo factor,
/// el segundo factor se degrada al primero.</b> Quien tenga acceso al buzón entra.
/// Todo lo que hay en esta clase existe para que eso no sea cierto, y ninguna
/// pieza es opcional:
/// </para>
///
/// <list type="bullet">
///   <item><b>Nace con la contraseña ya acertada.</b> Se solicita desde el desafío
///   del segundo factor, nunca de forma anónima. Así no sirve de oráculo para
///   averiguar qué correos tienen cuenta.</item>
///
///   <item><b>Espera.</b> <see cref="EjecutableDesde"/> convierte un ataque
///   silencioso e instantáneo en uno ruidoso y con un día por delante para
///   reaccionar. Confirmar antes de esa hora no adelanta nada.</item>
///
///   <item><b>Se cancela sin contraseña.</b> Cancelar tiene que ser más fácil que
///   ejecutar; si pidiera lo mismo que ejecutar, la víctima que recibe el aviso a
///   las tres de la mañana no cancela.</item>
///
///   <item><b>Cualquier ingreso correcto la cancela.</b> Si pudo entrar, no la
///   necesitaba — y si no fue ella quien la pidió, se deshace sola.</item>
/// </list>
///
/// <para>
/// Y lo que <b>no</b> hace: no emite sesión. Recuperar no es entrar; es poder
/// volver a inscribir un segundo factor. Quien la ejecuta acaba en la pantalla de
/// inscripción, no dentro del sistema.
/// </para>
/// </summary>
public class MfaRecoveryRequest : AuditableEntity
{
    public Guid CentralUserId { get; private set; }

    /// <summary>SHA-256 del token que viaja en el enlace de confirmación.</summary>
    public byte[] TokenHash { get; private set; } = [];

    /// <summary>
    /// SHA-256 del token de cancelación. Es OTRO token, no el mismo: el enlace de
    /// cancelar no pide contraseña, así que si compartiera token con el de
    /// confirmar, quien lo tuviera podría ejecutar la recuperación pasada la
    /// espera. Son dos capacidades distintas y viajan por separado en el mismo
    /// correo.
    /// </summary>
    public byte[] CancelTokenHash { get; private set; } = [];

    /// <summary>
    /// Desde cuándo se puede confirmar. Antes de esta hora el enlace existe pero no
    /// hace nada, y decirlo es parte del diseño: quien lo abra a los cinco minutos
    /// tiene que leer «esto se puede completar el jueves a las 9».
    /// </summary>
    public DateTime EjecutableDesde { get; private set; }

    /// <summary>
    /// Cuándo deja de servir. Sin caducidad, un enlace olvidado en un buzón sigue
    /// siendo una llave meses después.
    /// </summary>
    public DateTime ExpiraEn { get; private set; }

    public DateTime? EjecutadaEn { get; private set; }
    public DateTime? CanceladaEn { get; private set; }

    /// <summary>
    /// Por qué se canceló. Interesa distinguir «la persona pulsó cancelar» de «entró
    /// con normalidad y ya no hacía falta»: lo primero puede ser un ataque en curso
    /// y merece mirarse.
    /// </summary>
    [MaxLength(64)]
    public string? MotivoDeCancelacion { get; private set; }

    [MaxLength(45)]
    public string? IpSolicitante { get; private set; }

    // EF Core
    private MfaRecoveryRequest() { }

    public static class Motivos
    {
        public const string LaPersonaCancelo = "cancelada-por-la-persona";
        public const string EntroConNormalidad = "ingreso-correcto";
        public const string Reemplazada = "reemplazada-por-otra-solicitud";
    }

    public static MfaRecoveryRequest Crear(
        Guid centralUserId,
        byte[] tokenHash,
        byte[] cancelTokenHash,
        DateTime ahora,
        TimeSpan demora,
        TimeSpan vigencia,
        string? ipSolicitante)
    {
        if (centralUserId == Guid.Empty)
            throw new ArgumentException("CentralUserId requerido.", nameof(centralUserId));
        if (tokenHash is null || tokenHash.Length == 0)
            throw new ArgumentException("TokenHash requerido.", nameof(tokenHash));
        if (cancelTokenHash is null || cancelTokenHash.Length == 0)
            throw new ArgumentException("CancelTokenHash requerido.", nameof(cancelTokenHash));

        // La vigencia tiene que dejar margen DESPUÉS de la espera. Si caducara
        // antes o justo al cumplirse, la solicitud sería inejecutable siempre y el
        // fallo aparecería un día después de crearla, cuando nadie está mirando.
        if (vigencia <= demora)
        {
            throw new ArgumentException(
                "La vigencia tiene que superar la demora; si no, la solicitud caduca antes de poder ejecutarse.",
                nameof(vigencia));
        }

        return new MfaRecoveryRequest
        {
            CentralUserId = centralUserId,
            TokenHash = tokenHash,
            CancelTokenHash = cancelTokenHash,
            EjecutableDesde = ahora.Add(demora),
            ExpiraEn = ahora.Add(vigencia),
            IpSolicitante = ipSolicitante,
        };
    }

    /// <summary>Ni ejecutada, ni cancelada, ni caducada.</summary>
    public bool EstaViva(DateTime ahora) =>
        EjecutadaEn is null && CanceladaEn is null && ExpiraEn > ahora;

    /// <summary>Viva y además ya pasó la espera.</summary>
    public bool SePuedeEjecutar(DateTime ahora) =>
        EstaViva(ahora) && EjecutableDesde <= ahora;

    public void MarcarEjecutada(DateTime ahora)
    {
        if (!SePuedeEjecutar(ahora))
        {
            throw new InvalidOperationException(
                "La solicitud no se puede ejecutar: está cancelada, caducada, ya ejecutada, " +
                "o todavía no pasó la espera.");
        }

        EjecutadaEn = ahora;
    }

    /// <summary>
    /// Idempotente a propósito. El enlace de cancelar no pide nada, así que se
    /// pulsa dos veces con facilidad —y lo hace también el ingreso correcto, que
    /// cancela sin preguntar—; que la segunda vez lance convertiría un acierto en
    /// un error en la cara de quien hizo lo correcto.
    /// </summary>
    public void Cancelar(DateTime ahora, string motivo)
    {
        if (CanceladaEn is not null || EjecutadaEn is not null) return;

        CanceladaEn = ahora;
        MotivoDeCancelacion = motivo;
    }
}
