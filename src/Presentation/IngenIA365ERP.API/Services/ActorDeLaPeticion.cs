using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IActorActual"/> de la API (feature 012, T6, T041).
///
/// <para>
/// <b>En una petición</b> la persona es la fila de <c>SEC_Users</c> de la cooperativa activa, por
/// el mismo camino que la puerta de permisos (<see cref="PermisosDeLaPeticion.ResolverUsuarioYCooperativaAsync"/>:
/// identidad central, con el correo de respaldo). Se resuelve una vez y se memoriza en
/// <c>HttpContext.Items</c>: un comando que pregunte tres veces no consulta tres veces.
/// </para>
///
/// <para>
/// <b>En segundo plano</b> devuelve el <see cref="Actor"/> que fijó <see cref="ContextoAmbiental"/>
/// (<c>IEjecutorEnCooperativa</c>). Sin petición y sin contexto no hay nadie a quien atribuir la
/// operación y <b>lanza</b>: devolver un actor inventado firmaría la auditoría a nombre de nadie.
/// </para>
///
/// <para>
/// No toca <c>ICurrentUserService.UserId</c> (pregunta B1: su arreglo es tarea aparte del dueño)
/// y nunca crea un usuario técnico en <c>SEC_Users</c>.
/// </para>
/// </summary>
internal sealed class ActorDeLaPeticion(
    IHttpContextAccessor accessor,
    IOrigenDeLaPeticion origen,
    ICurrentCentralUserContext identidadCentral) : IActorActual
{
    private const string ClaveMemo = "__actor_de_la_peticion";

    public async Task<Actor> ObtenerAsync(CancellationToken ct = default)
    {
        var http = accessor.HttpContext;
        if (http is null)
        {
            return ContextoAmbiental.Actor ?? throw new InvalidOperationException(
                "Se pidió el actor fuera de una petición y sin ContextoAmbiental. Un trabajo de fondo " +
                "corre por IEjecutorEnCooperativa, que fija la cooperativa y el actor.");
        }

        if (http.Items.TryGetValue(ClaveMemo, out var memo) && memo is Actor yaResuelto) return yaResuelto;

        var actor = await ResolverAsync(http, ct);
        http.Items[ClaveMemo] = actor;
        return actor;
    }

    private async Task<Actor> ResolverAsync(HttpContext http, CancellationToken ct)
    {
        var permisos = http.RequestServices.GetRequiredService<PermisosDeLaPeticion>();
        var (idUsuario, _) = await permisos.ResolverUsuarioYCooperativaAsync(http, ct);

        var correo = identidadCentral.Email;
        Guid? publicId = null;
        var nombre = correo;

        if (idUsuario is not null)
        {
            var operativa = http.RequestServices.GetRequiredService<IApplicationDbContext>();
            var fila = await operativa.Users.AsNoTracking()
                .Where(u => u.Id == idUsuario)
                .Select(u => new
                {
                    u.PublicId,
                    u.Username,
                    u.Email,
                    Nombres = u.Person != null ? u.Person.FirstName : null,
                    Apellido = u.Person != null ? u.Person.LastName : null,
                })
                .FirstOrDefaultAsync(ct);

            if (fila is not null)
            {
                publicId = fila.PublicId;
                correo ??= fila.Email;
                var completo = $"{fila.Nombres} {fila.Apellido}".Trim();
                nombre = string.IsNullOrWhiteSpace(completo) ? (fila.Username ?? correo) : completo;
            }
        }

        return new Actor(
            ActorKind.Person,
            idUsuario,
            publicId,
            identidadCentral.CentralUserId,
            nombre ?? "(sin identificar)",
            correo,
            origen.Canal,
            origen.Endpoint ?? string.Empty,
            origen.Ip,
            null);
    }
}
