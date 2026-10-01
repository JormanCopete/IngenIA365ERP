using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Persistence;

/// <summary>
/// La única forma de abrir una transacción de base de datos desde Application (feature 012,
/// decisiones-transversales T14). Generaliza <c>TransaccionDeLiquidacion</c> de la feature 010, que ahora
/// delega aquí.
///
/// <para>
/// <b>Estrategia de ejecución.</b> PostgreSQL y SQL Server corren con reintentos
/// (<c>EnableRetryOnFailure</c>), y ésos no admiten una transacción de usuario por fuera de la estrategia.
/// Así que el trabajo va dentro de <c>CreateExecutionStrategy().ExecuteAsync</c>: si el motor lo elige como
/// víctima de un interbloqueo (PostgreSQL 40P01/40001, SQL Server 1205), la estrategia repite el trabajo
/// <b>entero</b>. Antes de cada repetición se llama <see cref="IApplicationDbContext.DescartarCambios"/>:
/// lo que el intento anterior dejó en el <c>ChangeTracker</c> ya no existe en la base (se revirtió), y el
/// trabajo tiene que <b>releer todo</b> lo que toca. Un trabajo que lee algo antes de llamar aquí y lo
/// modifica adentro no es repetible: pierde esa entidad al descartar.
/// </para>
///
/// <para>
/// <b>Anidada.</b> Si ya hay una transacción en curso en el contexto (la abrió <c>IdempotencyBehavior</c>
/// alrededor del comando), el trabajo se une a ella: no abre otra ni confirma; decide la de afuera. Así un
/// handler puede usar <see cref="EjecutarAsync{T}(IApplicationDbContext, Func{Task{T}}, CancellationToken)"/>
/// sin saber si lo llamaron por una ruta idempotente o no.
/// </para>
///
/// <para>
/// <b>Qué confirma.</b> Un <see cref="Result"/> fallido revierte; una excepción también, y sube; cualquier
/// otro resultado confirma. El contexto de Application es una interfaz: sin un <see cref="DbContext"/> real
/// detrás (un doble de prueba) el trabajo corre tal cual, y en InMemory la transacción se ignora como todas.
/// Ningún handler envía por <c>ISender</c> un comando reintentable dentro de su transacción (la lección de
/// <c>TransaccionDeLiquidacion</c>: un reintento anidado vaciaba el <c>ChangeTracker</c> de afuera).
/// </para>
/// </summary>
public static class TransaccionExplicita
{
    /// <summary>
    /// Corre <paramref name="trabajo"/> en una transacción (o en la que ya está en curso). Cada repetición de
    /// la estrategia descarta los cambios pendientes antes de volver a llamar al trabajo.
    /// </summary>
    public static Task<T> EjecutarAsync<T>(IApplicationDbContext db, Func<Task<T>> trabajo, CancellationToken ct) =>
        EjecutarAsync(db, trabajo, descartarAlRepetir: true, ct);

    /// <summary>
    /// Igual que <see cref="EjecutarAsync{T}(IApplicationDbContext, Func{Task{T}}, CancellationToken)"/>, pero
    /// permite no descartar entre repeticiones. Sólo para el código de la feature 010
    /// (<c>TransaccionDeLiquidacion</c>), cuyos handlers leen la ficha, la terminación y la corrida
    /// <b>antes</b> de abrir la transacción y las modifican adentro: descartar las dejaría sin seguimiento.
    /// Todo lo nuevo usa la otra sobrecarga y relee dentro del trabajo.
    /// </summary>
    internal static async Task<T> EjecutarAsync<T>(IApplicationDbContext db, Func<Task<T>> trabajo, bool descartarAlRepetir, CancellationToken ct)
    {
        if (db is not DbContext ctx) return await trabajo();

        // Anidada: se une a la de afuera, que es la que confirma o revierte.
        if (ctx.Database.CurrentTransaction is not null) return await trabajo();

        var estrategia = ctx.Database.CreateExecutionStrategy();
        var intento = 0;
        return await estrategia.ExecuteAsync(async () =>
        {
            if (++intento > 1 && descartarAlRepetir) db.DescartarCambios();

            await using var tx = await ctx.Database.BeginTransactionAsync(ct);
            var resultado = await trabajo();
            if (resultado is Result { IsFailure: true }) await tx.RollbackAsync(ct);
            else await tx.CommitAsync(ct);
            return resultado;
        });
    }
}
