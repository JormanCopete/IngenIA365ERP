using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Índices únicos de un consecutivo. EF puede mandar el INSERT del documento antes que el UPDATE del
    /// contador; entonces la carrera no llega como conflicto de <c>RowVersion</c> sino como choque contra
    /// el número repetido, y es la misma carrera (feature 012, T186: salía 500 bajo carga).
    /// </summary>
    public static readonly IReadOnlyList<string> IndicesDeConsecutivo = ["UK_ACC_Documents_Type_Number"];

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
            catch (Exception ex) when (intento < MaxIntentos && (ex is ConcurrencyConflictException || EsChoqueDeConsecutivo(ex)))
            {
                logger.LogWarning(ex,
                    "Conflicto de concurrencia en {Request} (intento {Intento} de {Maximo}): se descartan los cambios y se reintenta.",
                    typeof(TRequest).Name, intento, MaxIntentos);
                servicios.GetRequiredService<IApplicationDbContext>().DescartarCambios();
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(25, 125) * intento), cancellationToken);
            }
        }
    }

    private static bool EsChoqueDeConsecutivo(Exception ex)
    {
        if (ex is not DbUpdateException) return false;
        for (var actual = ex; actual is not null; actual = actual.InnerException)
            if (IndicesDeConsecutivo.Any(i => actual.Message.Contains(i, StringComparison.OrdinalIgnoreCase)))
                return true;
        return false;
    }
}
