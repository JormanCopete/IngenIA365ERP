using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// Quién hace la operación en curso (feature 012, T6; decisiones-transversales §2.16). Lo devuelve
/// <see cref="Interfaces.Security.IActorActual"/>: en una petición, la persona resuelta en
/// <c>SEC_Users</c>; en segundo plano, el actor que fijó <see cref="ContextoAmbiental"/>.
///
/// <para>
/// <see cref="UserId"/> es <c>SEC_Users.Id</c> de la cooperativa, <b>nunca</b> el entero del token
/// (<c>ICurrentUserService.UserId</c>) ni el correo: la segregación de funciones y las aprobaciones
/// comparan esto (<c>LaSegregacionNoUsaUserIdDelToken</c>). El proceso automático no tiene fila en
/// <c>SEC_Users</c> y no se le crea un usuario técnico.
/// </para>
/// </summary>
/// <param name="Kind">Persona o proceso.</param>
/// <param name="UserId"><c>SEC_Users.Id</c> (interno); nulo para el proceso o si no se resolvió.</param>
/// <param name="UserPublicId"><c>SEC_Users.PublicId</c>.</param>
/// <param name="CentralUserId">La identidad central (<c>sub</c> del token).</param>
/// <param name="Name">Nombre a mostrar y a firmar en la auditoría.</param>
/// <param name="Email">Correo de la persona; nulo para el proceso.</param>
/// <param name="Channel">Por dónde entró la operación.</param>
/// <param name="Origin">Qué la originó: el endpoint en una petición; <c>Mensaje:{id}</c>, <c>Lote:{número}</c> o <c>Tarea:{nombre}</c> en segundo plano.</param>
/// <param name="Ip">IP de la petición; nula en segundo plano.</param>
/// <param name="Reason">Motivo declarado de la operación, si lo hay.</param>
public sealed record Actor(
    ActorKind Kind,
    int? UserId,
    Guid? UserPublicId,
    Guid? CentralUserId,
    string Name,
    string? Email,
    ExecutionChannel Channel,
    string Origin,
    string? Ip,
    string? Reason)
{
    /// <summary>El nombre con el que el proceso automático firma lo que hace.</summary>
    public const string NombreDelProceso = "Proceso de integración";

    private static readonly string[] PrefijosDeOrigen = ["Mensaje:", "Lote:", "Tarea:"];

    public bool EsProceso => Kind == ActorKind.Process;

    /// <summary>
    /// El actor del proceso automático: <c>Kind = Process</c>, «Proceso de integración», sin IP,
    /// canal <c>Process</c>. Los lotes manuales, reprocesos y envíos posteriores NO usan esto:
    /// corren con la persona que los ordenó, capturada en la orden.
    /// </summary>
    /// <param name="origen"><c>Mensaje:{id}</c>, <c>Lote:{número}</c> o <c>Tarea:{nombre}</c>.</param>
    /// <exception cref="ArgumentException">Si el origen no dice de qué mensaje, lote o tarea se trata.</exception>
    public static Actor ProcesoDeIntegracion(string origen)
    {
        ValidarOrigenDeProceso(origen);
        return new Actor(ActorKind.Process, null, null, null, NombreDelProceso, null, ExecutionChannel.Process, origen, null, null);
    }

    public static string OrigenDeMensaje(long mensajeId) => $"Mensaje:{mensajeId}";

    public static string OrigenDeLote(string numero) => $"Lote:{numero}";

    public static string OrigenDeTarea(string nombre) => $"Tarea:{nombre}";

    /// <summary>
    /// Un origen de segundo plano dice de qué se trata: sin eso, la auditoría de un proceso no se
    /// puede rastrear hasta el mensaje, el lote o la tarea que la produjo.
    /// </summary>
    /// <exception cref="ArgumentException">Si no empieza por <c>Mensaje:</c>, <c>Lote:</c> o <c>Tarea:</c> seguido de algo.</exception>
    public static void ValidarOrigenDeProceso(string origen)
    {
        var valido = !string.IsNullOrWhiteSpace(origen) && PrefijosDeOrigen.Any(p =>
            origen.StartsWith(p, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(origen[p.Length..]));
        if (!valido)
        {
            throw new ArgumentException(
                $"El origen de un proceso en segundo plano es «Mensaje:{{id}}», «Lote:{{número}}» o «Tarea:{{nombre}}»; llegó «{origen}».",
                nameof(origen));
        }
    }
}
