using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

/// <summary>
/// Traduce la identidad central de quien llama a su fila en <c>SEC_Users</c>.
///
/// <para>
/// El reseteo con doble aprobación guarda solicitante y aprobadores como
/// <c>int</c> contra <c>SEC_Users.Id</c>, porque es una operación <b>de la
/// cooperativa</b>: la aprueban dos administradores de ahí dentro. Pero quien
/// llama llega autenticado por la identidad central, y el puente entre ambos
/// mundos es <c>SEC_Users.CentralUserId</c>.
/// </para>
///
/// <para>
/// Sin esto, los tres endpoints leían <c>ICurrentUserService.UserId</c>, que
/// parsea como <c>int</c> el claim <c>uid</c> —un claim que el emisor central no
/// pone—. El resultado era <c>null</c> siempre, así que las tres rutas
/// respondían <c>401 «No autenticado»</c> pasaran las credenciales que pasaran:
/// nadie podía radicar una solicitud, ni aprobarla, ni ver la cola. La pantalla
/// de aprobaciones existía y no servía para nada.
/// </para>
/// </summary>
internal static class QuienLlamaEnLaCooperativa
{
    /// <summary>
    /// Devuelve el <c>SEC_Users.Id</c> de quien llama, o <c>null</c> si no hay
    /// sesión central o si esa persona no está provisionada en la cooperativa
    /// activa — que es lo correcto: no se aprueba nada en una cooperativa a la
    /// que no se pertenece.
    /// </summary>
    public static async Task<int?> IdAsync(
        IApplicationDbContext db,
        ICurrentCentralUserContext currentUser,
        CancellationToken ct)
    {
        if (currentUser.CentralUserId is not { } centralUserId)
        {
            return null;
        }

        return await db.Users
            .Where(u => u.CentralUserId == centralUserId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
    }
}
