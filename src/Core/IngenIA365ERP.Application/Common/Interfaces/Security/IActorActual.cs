using IngenIA365ERP.Application.Common.Execution;

namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Quién hace la operación en curso (feature 012, T6). En una petición, la persona resuelta en
/// <c>SEC_Users</c> de la cooperativa (<c>Kind = Person</c>, memorizada por petición); en segundo
/// plano, el <see cref="Actor"/> que fijó <see cref="ContextoAmbiental"/>.
///
/// <para>
/// Es la identidad que comparan la segregación de funciones y las aprobaciones, y la que se guarda
/// en <c>CreatedByUserId</c>/<c>ConfirmedByUserId</c>. No reemplaza a
/// <c>ICurrentUserService.UserId</c>, que sigue igual en el resto de la plataforma (pregunta B1).
/// </para>
/// </summary>
public interface IActorActual
{
    /// <summary>
    /// El actor. En una petición sin persona resuelta (anónima, o sin fila activa en
    /// <c>SEC_Users</c>) devuelve una persona con <see cref="Actor.UserId"/> nulo: quien necesite
    /// la identidad para decidir tiene que tratar eso como «no se sabe quién es», nunca como otro.
    /// </summary>
    Task<Actor> ObtenerAsync(CancellationToken ct = default);
}
