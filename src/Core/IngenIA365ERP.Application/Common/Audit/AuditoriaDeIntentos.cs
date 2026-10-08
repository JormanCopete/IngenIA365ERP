using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>Un campo que el validador rechazó: su nombre y el código de la regla, nunca el valor (T67, F9). (nuevo)</summary>
public sealed record CampoRechazado(string Campo, string Codigo);

/// <summary>El rechazo de forma de un comando: su nombre y los campos con sus códigos, sin valores (T67, F9). (nuevo)</summary>
public sealed record RechazoDeForma(string Comando, IReadOnlyList<CampoRechazado> Campos);

/// <summary>
/// El último rechazo de forma de la petición (feature 013, T67, F9; research R136). Scoped: lo anota <c>ValidationBehavior</c> cuando
/// el validador rechaza un comando —que nunca llega a <c>AuditBehavior</c>, porque la validación corre antes— y lo lee el metadato
/// <c>AuditarIntentoDenegado</c> del grupo de rutas, que lo registra como <c>{Módulo}.Request.Rejected</c>. Sólo guarda nombres de
/// campos y códigos de regla: el valor rechazado nunca entra a la auditoría. (nuevo)
/// </summary>
public sealed class RechazosDeFormaDeLaPeticion
{
    public RechazoDeForma? Ultimo { get; private set; }

    public void Anotar(string comando, IEnumerable<CampoRechazado> campos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comando);
        Ultimo = new RechazoDeForma(comando, campos.DistinctBy(c => (c.Campo, c.Codigo)).ToList());
    }
}

/// <summary>
/// Los intentos que no llegan a ser un comando y aun así se auditan en los grupos que lo piden (feature 013, T67, F9; api.md §1.3 y
/// §1.12): el 404 por falta de permiso (<c>{Módulo}.Access.Denied</c>, antes de responder) y el 400 del validador
/// (<c>{Módulo}.Request.Rejected</c>, con el comando, los campos y los códigos, sin valores). Los llama el filtro
/// <c>AuditoriaDeIntentoDenegadoFilter</c> de la API por <see cref="AuditoriaEncadenada.RegistrarAsync"/> y <b>nunca</b> propagan: un fallo
/// al auditar un intento no puede convertir el 404 en un 500 que delate la ruta. (nuevo)
/// </summary>
public static class AuditoriaDeIntentos
{
    /// <summary><c>{Módulo}.Access.Denied</c> (<c>Lending.Access.Denied</c>).</summary>
    public static string AccesoDenegado(string modulo) => $"{modulo}.Access.Denied";

    /// <summary><c>{Módulo}.Request.Rejected</c> (<c>Lending.Request.Rejected</c>).</summary>
    public static string RechazoDeFormaDe(string modulo) => $"{modulo}.Request.Rejected";

    /// <summary>El evento del intento sin permiso: la ruta (del contexto) y el 404 que se respondió.</summary>
    public static AuditLogCommand EventoDeAccesoDenegado(string modulo) => new()
    {
        Action = AccesoDenegado(modulo),
        EntityType = "Endpoint",
        Module = modulo,
        HttpStatusCode = 404,
    };

    /// <summary>El evento del rechazo de forma: el comando y cada campo con su código; el cuerpo de la petición no entra.</summary>
    public static AuditLogCommand EventoDeRechazoDeForma(string modulo, RechazoDeForma rechazo) => new()
    {
        Action = RechazoDeFormaDe(modulo),
        EntityType = rechazo.Comando,
        Module = modulo,
        HttpStatusCode = 400,
        NewValues = new
        {
            command = rechazo.Comando,
            fields = rechazo.Campos.Select(c => new { field = c.Campo, code = c.Codigo }).ToList(),
        },
    };

    public static Task RegistrarAccesoDenegadoAsync(IServiceProvider servicios, string modulo, ILogger logger, CancellationToken ct) =>
        RegistrarSinPropagarAsync(servicios, EventoDeAccesoDenegado(modulo), logger, ct);

    public static Task RegistrarRechazoDeFormaAsync(IServiceProvider servicios, string modulo, RechazoDeForma rechazo, ILogger logger, CancellationToken ct) =>
        RegistrarSinPropagarAsync(servicios, EventoDeRechazoDeForma(modulo, rechazo), logger, ct);

    private static async Task RegistrarSinPropagarAsync(IServiceProvider servicios, AuditLogCommand evento, ILogger logger, CancellationToken ct)
    {
        try
        {
            // Con una petición cancelada el intento igual se registra: el que se fue no borra lo que intentó.
            await AuditoriaEncadenada.RegistrarAsync(servicios, evento, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Auditoria.IntentoSinRegistrar] No se pudo registrar {Accion}; la respuesta sigue igual.", evento.Action);
        }
    }

    /// <summary>El rechazo de forma anotado en la petición, si lo hubo.</summary>
    public static RechazoDeForma? RechazoAnotado(IServiceProvider servicios) =>
        servicios.GetService<RechazosDeFormaDeLaPeticion>()?.Ultimo;
}
