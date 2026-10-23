using System.Reflection;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Marca una escritura de negocio de Recaudo (feature 015, T0019; FR-150, FR-159): antes de la activación responde <c>Receipts.NotActivated</c>.
/// La configuración y la puesta en marcha no la llevan: se admiten antes de activar. (nuevo)
/// </summary>
public interface IEscrituraDeRecaudo;

/// <summary>Marca una escritura de negocio de la Tesorería nueva: antes de la activación responde <c>Treasury.NotActivated</c>. (nuevo)</summary>
public interface IEscrituraDeTesoreria;

/// <summary>
/// La guarda de módulo activo de Recaudo y Tesorería (feature 015, T0019, T0046; decisión del dueño del 2026-10-24: comportamiento de MediatR y no
/// filtro de ruta, para que valga también fuera de HTTP). Un comando que implementa <see cref="IEscrituraDeRecaudo"/> o
/// <see cref="IEscrituraDeTesoreria"/> pregunta el estado de <c>RCD_Setup</c> o <c>TES_Setup</c> y, sin activar, devuelve el fallo del módulo sin
/// llegar al manejador; lo demás pasa sin consultar nada. Va justo después de <c>ValidationBehavior</c>: un comando inválido dice primero qué
/// tiene mal; uno válido de un módulo sin activar no abre transacción ni consume clave de idempotencia. (nuevo)
/// </summary>
public sealed class ModuloActivoBehavior<TRequest, TResponse>(IApplicationDbContext db) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IEscrituraDeRecaudo
            && !await db.ReceiptsSetups.AsNoTracking().AnyAsync(s => s.Status == ReceiptsModuleStatus.Active, cancellationToken))
            return Fallo(Receipts.Common.ErroresDeRecaudo.NoActivado());

        if (request is IEscrituraDeTesoreria
            && !await db.TreasurySetups.AsNoTracking().AnyAsync(s => s.Status == TreasuryModuleStatus.Active, cancellationToken))
            return Fallo(Treasury.Common.ErroresDeTesoreria.NoActivado());

        return await next(cancellationToken);
    }

    /// <summary>El fallo como <see cref="Result"/> o <see cref="Result{T}"/>; una escritura marcada que no devuelve un resultado lanza.</summary>
    private static TResponse Fallo(Error error)
    {
        var tipo = typeof(TResponse);
        if (tipo == typeof(Result)) return (TResponse)(object)Result.Failure(error);
        if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var metodo = typeof(Result).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition && m.GetParameters() is [{ ParameterType: var p }] && p == typeof(Error))
                .MakeGenericMethod(tipo.GetGenericArguments()[0]);
            return (TResponse)metodo.Invoke(null, [error])!;
        }
        throw new InvalidOperationException($"{typeof(TRequest).Name} es una escritura de Recaudo o Tesorería y no devuelve Result: {error.Code}.");
    }
}
