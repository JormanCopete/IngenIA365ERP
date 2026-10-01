using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Persistence;

/// <summary>
/// Lo que se hace <b>después del commit</b> de la petición (feature 012, I4, T734, T736; decisiones-transversales §1.3 paso 12): subir el
/// canónico de un documento electrónico recién registrado y, en el POS, el intento en línea ante la DIAN con su espera. Nada de eso puede ir
/// dentro de la transacción de la confirmación —una llamada de red con la fila bloqueada, o un artefacto que se sube de una venta que al final
/// se revierte—, y quien confirma no sabe cuándo termina la transacción (la abre <c>IdempotencyBehavior</c>, afuera del handler).
///
/// <para>
/// Quien confirma <b>anota</b> aquí lo que falta; <see cref="TrasElCommitBehavior{TRequest,TResponse}"/>, que envuelve a
/// <c>IdempotencyBehavior</c>, lo ejecuta cuando la petición más externa terminó bien y ya no hay transacción abierta. Una petición que falla
/// (o lanza) lo descarta: lo anotado era de algo que se revirtió. Una repetición idempotente no ejecuta el handler, así que no anota nada y
/// devuelve el resultado guardado. Las tareas nunca tumban la respuesta: un error queda en la bitácora.
/// </para>
///
/// <para>
/// Dos clases de tarea: las que sólo <b>hacen</b> algo (<see cref="Agregar"/>) y las que <b>completan la respuesta</b>
/// (<see cref="Completar{T}"/>): el cobro del POS devuelve la tirilla sólo si la DIAN validó dentro de la espera. Scoped: vive lo que dura la
/// petición. (nuevo)
/// </para>
/// </summary>
public sealed class TareasTrasElCommit(ILogger<TareasTrasElCommit>? logger = null)
{
    private readonly List<Func<CancellationToken, Task>> _tareas = [];
    private readonly List<(Type Tipo, Func<object, CancellationToken, Task<object>> Completar)> _completar = [];
    private int _profundidad;

    /// <summary>¿Hay algo anotado?</summary>
    public bool HayPendientes => _tareas.Count > 0 || _completar.Count > 0;

    /// <summary>Anota una tarea para después del commit.</summary>
    public void Agregar(Func<CancellationToken, Task> tarea)
    {
        ArgumentNullException.ThrowIfNull(tarea);
        _tareas.Add(tarea);
    }

    /// <summary>Anota cómo completar la respuesta de tipo <typeparamref name="T"/> después del commit.</summary>
    public void Completar<T>(Func<T, CancellationToken, Task<T>> completar) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(completar);
        _completar.Add((typeof(T), async (r, ct) => await completar((T)r, ct)));
    }

    /// <summary>Descarta lo anotado (la petición falló).</summary>
    public void Descartar()
    {
        _tareas.Clear();
        _completar.Clear();
    }

    /// <summary>Entra una petición (el behavior); las anidadas no ejecutan: sólo la más externa.</summary>
    public void Entrar() => _profundidad++;

    /// <summary>Sale una petición; <c>true</c> si era la más externa.</summary>
    public bool Salir()
    {
        if (_profundidad > 0) _profundidad--;
        return _profundidad == 0;
    }

    /// <summary>
    /// Ejecuta lo anotado, en orden: primero las tareas, después las que completan la respuesta. Nunca lanza (salvo la cancelación);
    /// devuelve la respuesta, completada si correspondía.
    /// </summary>
    public async Task<object?> EjecutarAsync(object? respuesta, CancellationToken ct)
    {
        var tareas = _tareas.ToList();
        var completar = _completar.ToList();
        Descartar();
        foreach (var tarea in tareas)
        {
            try
            {
                await tarea(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger?.LogError(ex, "[TrasElCommit] Falló una tarea posterior al commit; la operación ya quedó confirmada.");
            }
        }
        foreach (var (tipo, paso) in completar)
        {
            if (respuesta is null || !tipo.IsInstanceOfType(respuesta)) continue;
            try
            {
                respuesta = await paso(respuesta, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger?.LogError(ex, "[TrasElCommit] Falló completar la respuesta después del commit; se devuelve la de la transacción.");
            }
        }
        return respuesta;
    }
}

/// <summary>
/// Ejecuta <see cref="TareasTrasElCommit"/> cuando la petición más externa terminó bien y no queda transacción abierta (feature 012, I4, T734,
/// T736). Va entre <c>LoggingBehavior</c> e <c>IdempotencyBehavior</c>: envuelve la transacción que abre la clave de operación, así que al
/// volver ya está confirmada. Pide sus servicios sólo si hay algo anotado (envuelve también lo que corre sin cooperativa, como el login).
/// (nuevo)
/// </summary>
public sealed class TrasElCommitBehavior<TRequest, TResponse>(IServiceProvider servicios)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var tareas = servicios.GetService<TareasTrasElCommit>();
        if (tareas is null) return await next(cancellationToken);

        tareas.Entrar();
        TResponse respuesta;
        try
        {
            respuesta = await next(cancellationToken);
        }
        catch
        {
            if (tareas.Salir()) tareas.Descartar();
            throw;
        }

        if (!tareas.Salir() || !tareas.HayPendientes) return respuesta;
        if (respuesta is Result { IsFailure: true })
        {
            tareas.Descartar();
            return respuesta;
        }
        // Alguien de afuera del mediador tiene una transacción abierta (un trabajo de fondo, una prueba): todavía no hubo commit.
        if (servicios.GetService<IApplicationDbContext>() is DbContext ctx && ctx.Database.CurrentTransaction is not null) return respuesta;

        var completada = await tareas.EjecutarAsync(respuesta, cancellationToken);
        return completada is TResponse r ? r : respuesta;
    }
}
