using System.Collections.Concurrent;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// <see cref="IEjecutorEnCooperativa"/> de la API (feature 012, T5, T046; FR-083, D-03). Da a un
/// trabajo de fondo el mismo contexto que una petición: la base, la auditoría y el actor de SU
/// cooperativa, y nada de ninguna otra.
///
/// <list type="bullet">
/// <item>Un <see cref="AsyncServiceScope"/> nuevo por llamada: el <c>ApplicationDbContext</c>, los
/// comandos y todo lo que tenga ámbito nacen y mueren con el trabajo, nunca compartidos entre
/// cooperativas.</item>
/// <item>Fija el <see cref="ContextoAmbiental"/> antes de crear el ámbito —la fábrica de
/// <c>ErpTenantInfo</c> lo lee al construir el <c>DbContext</c>— y lo restaura al terminar, lance o
/// no el trabajo.</item>
/// <item><b>Lanza</b> si hay <c>HttpContext</c>: una orden manual sólo encola y el trabajo lo corre
/// el proceso; ejecutar aquí dentro de una petición mezclaría el contexto de la petición con el de
/// otra cooperativa. <b>Lanza</b> también si la entrada no trae base ni cadena propia.</item>
/// <item><b>Salta</b> la cooperativa cuya base no está al día —inalcanzable o con migraciones
/// pendientes, el mismo criterio de <c>PendingMigrationsGuard</c>, que alimenta a
/// <c>DatabaseReadiness</c>—, con un aviso en el log y
/// <see cref="ResultadoEnCooperativa.Omitida"/>, sin detener a las demás: un trabajo sobre un
/// esquema viejo fallaría con «columna inexistente» en cada pasada.</item>
/// </list>
///
/// <para>
/// Una base al día se recuerda por proceso: las migraciones sólo se aplican al arrancar, así que
/// una cooperativa que estuvo al día lo sigue estando hasta el próximo despliegue. Las omitidas se
/// vuelven a comprobar en cada llamada, para retomarlas en cuanto alguien las migre.
/// </para>
/// </summary>
internal sealed class EjecutorEnCooperativa(
    IServiceScopeFactory ambitos,
    IHttpContextAccessor peticion,
    ILogger<EjecutorEnCooperativa> logger) : IEjecutorEnCooperativa
{
    private static readonly ConcurrentDictionary<Guid, bool> AlDia = new();

    public async Task<ResultadoEnCooperativa> EjecutarAsync(
        TenantDirectoryEntry cooperativa,
        Actor actor,
        string origen,
        Func<IServiceProvider, CancellationToken, Task> trabajo,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cooperativa);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(trabajo);

        if (peticion.HttpContext is { } http)
        {
            throw new InvalidOperationException(
                $"IEjecutorEnCooperativa se llamó dentro de una petición ({http.Request.Method} {http.Request.Path}). " +
                "Una orden manual encola el trabajo; lo ejecuta el proceso en segundo plano.");
        }

        if (string.IsNullOrWhiteSpace(cooperativa.DatabaseName) && string.IsNullOrWhiteSpace(cooperativa.ConnectionString))
        {
            throw new InvalidOperationException(
                $"La cooperativa {cooperativa.Name} ({cooperativa.PublicId}) no trae base de datos ni cadena propia; " +
                "no se sabe contra qué datos correr el trabajo y no se cae a la plantilla.");
        }

        using var contexto = ContextoAmbiental.Fijar(cooperativa, actor, origen);
        await using var ambito = ambitos.CreateAsyncScope();

        if (!await EstaAlDiaAsync(cooperativa, ambito.ServiceProvider, ct))
        {
            logger.LogWarning(
                "[Ejecucion.CooperativaOmitida] {Origen}: la base de {Cooperativa} ({TenantPublicId}) no está al día " +
                "(inalcanzable o con migraciones pendientes). Se salta hasta que se migre; las demás siguen.",
                origen, cooperativa.Name, cooperativa.PublicId);
            return ResultadoEnCooperativa.Omitida;
        }

        await trabajo(ambito.ServiceProvider, ct);
        return ResultadoEnCooperativa.Ejecutada;
    }

    private static async Task<bool> EstaAlDiaAsync(TenantDirectoryEntry cooperativa, IServiceProvider servicios, CancellationToken ct)
    {
        if (AlDia.ContainsKey(cooperativa.PublicId)) return true;

        var db = servicios.GetRequiredService<ApplicationDbContext>();
        if (!await db.Database.CanConnectAsync(ct)) return false;
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;

        AlDia[cooperativa.PublicId] = true;
        return true;
    }
}
