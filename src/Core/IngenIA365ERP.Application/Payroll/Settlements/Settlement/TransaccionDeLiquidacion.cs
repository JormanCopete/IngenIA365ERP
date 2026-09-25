using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;

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
/// <c>DbContext</c> real (en pruebas InMemory se ignora, como el resto de transacciones). Va
/// por la estrategia de ejecución del proveedor porque PostgreSQL y SQL Server corren con reintentos
/// (<c>EnableRetryOnFailure</c>) y ésos no admiten transacciones de usuario por fuera de ella. Un
/// <see cref="Result"/> fallido revierte; una excepción también, y sube.
/// </para>
///
/// <para>
/// Desde la feature 012 (T14, T052) delega en <see cref="TransaccionExplicita"/>, la única que abre
/// transacciones en Application, con su misma firma de siempre. Dos diferencias con la general, a
/// propósito: no descarta el <c>ChangeTracker</c> entre repeticiones de la estrategia, porque estos handlers
/// leen la ficha, la terminación y la corrida antes de abrir la transacción y las modifican adentro; y si
/// ya hay una transacción en curso se une a ella en vez de abrir otra.
/// </para>
/// </summary>
public static class TransaccionDeLiquidacion
{
    public static Task<Result<T>> EjecutarAsync<T>(IApplicationDbContext db, Func<Task<Result<T>>> trabajo, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, trabajo, descartarAlRepetir: false, ct);
}
