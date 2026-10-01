namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// Una tarea que el <c>ProgramadorDeTareas</c> de la API corre por cooperativa (feature 012, T050;
/// decisiones-transversales T47): reorden, quiebre, eventos RADIAN faltantes, la verificación de
/// integridad nocturna, las alertas DIAN, liberar reservas vencidas. Cada historia registra la suya
/// como <b>singleton</b> (<c>services.AddSingleton&lt;ITareaProgramada, …&gt;()</c>); esta fase sólo deja el
/// contrato y el programador.
///
/// <para>
/// El programador toma el arrendamiento <c>scheduled.tasks</c> de la cooperativa, le pregunta a cada tarea
/// si le toca y la corre por <see cref="IEjecutorEnCooperativa"/> con el actor «Proceso de integración» y
/// origen <c>Tarea:{Nombre}</c>. Una tarea escribe <b>sólo por comandos</b> (<c>ISender</c> del ámbito que
/// recibe), nunca con <c>SaveChangesAsync</c> propio, y es idempotente: la última corrida se recuerda
/// por proceso, así que tras un reinicio o un cambio de réplica puede volver a correr.
/// </para>
/// </summary>
public interface ITareaProgramada
{
    /// <summary>Nombre corto y estable; va en el origen <c>Tarea:{Nombre}</c> de la auditoría.</summary>
    string Nombre { get; }

    /// <summary>
    /// Si le toca correr ahora. Pura: decide sólo con la hora local de la plataforma y la última vez que
    /// corrió bien en esta cooperativa (nula si no ha corrido desde que arrancó el proceso).
    /// </summary>
    bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida);

    /// <summary>Corre la tarea con los servicios del ámbito de la cooperativa que armó el ejecutor.</summary>
    Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct);
}
