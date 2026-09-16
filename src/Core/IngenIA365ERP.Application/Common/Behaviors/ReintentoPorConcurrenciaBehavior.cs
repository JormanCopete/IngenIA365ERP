using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Marca un request cuyo handler puede repetirse entero si la base detecta una carrera
/// (<see cref="ConcurrencyConflictException"/>): el handler tiene que leer todo lo que toca
/// dentro de sí mismo, porque entre intentos se descarta el seguimiento del contexto.
/// </summary>
public interface IReintentableAnteConcurrencia;

/// <summary>
/// Feature 009 (R3). Sin transacciones explícitas, el número consecutivo de un comprobante se
/// asigna en la misma escritura que lo consume: <c>VoucherType.NextNumber</c> se incrementa en
/// memoria y su <c>RowVersion</c> convierte la carrera en <see cref="ConcurrencyConflictException"/>.
/// Este behavior la atrapa para los requests marcados, descarta los cambios pendientes y
/// vuelve a ejecutar el handler (hasta <see cref="MaxIntentos"/> veces, con una espera corta y
/// aleatoria). Va después de <c>AuditBehavior</c> a propósito: la auditoría ve un solo evento
/// con el resultado final, no uno por intento. Lo que sobrevive a los reintentos sale como
/// 409 <c>Concurrency.StaleRowVersion</c> (manejador global de la API).
///
/// <para>
/// El contexto operativo se pide <b>sólo al reintentar</b>, por <see cref="IServiceProvider"/>:
/// este behavior envuelve todo comando, también los que corren sin cooperativa resuelta (el
/// login), y resolver <see cref="IApplicationDbContext"/> en el constructor tumbaba esas rutas
/// con «se pidió la base operativa dentro de una petición sin cooperativa».
/// </para>
/// </summary>
public sealed class ReintentoPorConcurrenciaBehavior<TRequest, TResponse>(
    IServiceProvider servicios,
    ILogger<ReintentoPorConcurrenciaBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int MaxIntentos = 5;

    private static readonly bool EsReintentable = typeof(IReintentableAnteConcurrencia).IsAssignableFrom(typeof(TRequest));

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!EsReintentable)
            return await next(cancellationToken);

        for (var intento = 1; ; intento++)
        {
            try
            {
                return await next(cancellationToken);
            }
            catch (ConcurrencyConflictException ex) when (intento < MaxIntentos)
            {
                logger.LogWarning(ex,
                    "Conflicto de concurrencia en {Request} (intento {Intento} de {Maximo}): se descartan los cambios y se reintenta.",
                    typeof(TRequest).Name, intento, MaxIntentos);
                servicios.GetRequiredService<IApplicationDbContext>().DescartarCambios();
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(25, 125) * intento), cancellationToken);
            }
        }
    }
}
