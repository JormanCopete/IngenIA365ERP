using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>
/// Una transacción de base de datos alrededor de un trabajo que hace más de un <c>SaveChanges</c>
/// (feature 010, US3). La definitiva lo necesita dos veces: al registrar la terminación —la fila
/// tiene que existir para que el cargador la lea y los descuentos tengan Id antes de enlazar las
/// líneas— y al aprobar, porque cada recaudo en Cartera pasa por <c>RecaudoDeCredito</c>, que
/// guarda por su cuenta. Sin esto, un fallo a mitad de camino dejaría la ficha cerrada sin recaudo o
/// un recaudo sin liquidación aprobada. El recaudo se llama <b>directo</b>, nunca por <c>ISender</c>:
/// un comando reintentable anidado aquí vaciaba el <c>ChangeTracker</c> al reintentar y la corrida
/// aprobada se quedaba fuera del <c>SaveChanges</c> mientras el recaudo sí se confirmaba.
///
/// <para>
/// El contexto de Application es una interfaz; la transacción sólo existe cuando detrás hay un
/// <see cref="DbContext"/> real (en pruebas InMemory se ignora, como el resto de transacciones). Va
/// por la estrategia de ejecución del proveedor porque PostgreSQL y SQL Server corren con reintentos
/// (<c>EnableRetryOnFailure</c>) y ésos no admiten transacciones de usuario por fuera de ella. Un
/// <see cref="Result"/> fallido revierte; una excepción también, y sube.
/// </para>
/// </summary>
public static class TransaccionDeLiquidacion
{
    public static async Task<Result<T>> EjecutarAsync<T>(IApplicationDbContext db, Func<Task<Result<T>>> trabajo, CancellationToken ct)
    {
        if (db is not DbContext ctx) return await trabajo();

        var estrategia = ctx.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync(ct);
            var resultado = await trabajo();
            if (resultado.IsSuccess) await tx.CommitAsync(ct);
            else await tx.RollbackAsync(ct);
            return resultado;
        });
    }
}
