using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// El único camino para que un trabajo de fondo opere sobre los datos de una cooperativa
/// (feature 012, T5, FR-083, D-03; <c>NingunTrabajoDeFondoOperaSinCooperativa</c>). Da a cada
/// llamada el mismo contexto que tendría una petición: su base, su auditoría y su actor.
///
/// <para>
/// Contrato de la implementación (<c>API/Integration/EjecutorEnCooperativa</c>): un ámbito de
/// servicios nuevo por llamada —por mensaje o por comando, nunca uno compartido entre
/// cooperativas—; fija el <see cref="ContextoAmbiental"/> y lo restaura al terminar; <b>lanza</b>
/// si hay <c>HttpContext</c> (una orden manual sólo encola; el trabajo lo corre el proceso) o si la
/// entrada no trae base; y <b>salta</b> la cooperativa cuya base tiene migraciones pendientes, con
/// aviso en el log y <see cref="ResultadoEnCooperativa.Omitida"/>, sin detener a las demás.
/// </para>
/// </summary>
public interface IEjecutorEnCooperativa
{
    /// <param name="cooperativa">La cooperativa, tal como la da <c>ITenantDirectory.ListActiveAsync</c>.</param>
    /// <param name="actor">Quién actúa: <see cref="Actor.ProcesoDeIntegracion"/> o la persona que ordenó el lote.</param>
    /// <param name="origen"><c>Mensaje:{id}</c>, <c>Lote:{número}</c> o <c>Tarea:{nombre}</c>.</param>
    /// <param name="trabajo">Lo que se corre, con los servicios del ámbito nuevo.</param>
    /// <param name="ct">Cancelación del trabajo de fondo.</param>
    Task<ResultadoEnCooperativa> EjecutarAsync(
        TenantDirectoryEntry cooperativa,
        Actor actor,
        string origen,
        Func<IServiceProvider, CancellationToken, Task> trabajo,
        CancellationToken ct = default);
}

/// <summary>Qué pasó con una llamada a <see cref="IEjecutorEnCooperativa.EjecutarAsync"/>.</summary>
public enum ResultadoEnCooperativa
{
    /// <summary>El trabajo corrió. Si lanzó, la excepción se propaga: no hay un tercer resultado mudo.</summary>
    Ejecutada = 1,

    /// <summary>No corrió porque la base de la cooperativa tiene migraciones pendientes.</summary>
    Omitida = 2,
}
