using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Application.Inventory.Catalog;

/// <summary>
/// Inactivar y reactivar una fila de catálogo con motivo (feature 012, T214–T216; contracts/api.md §3: los catálogos no
/// se borran, se inactivan con <c>reason</c>). Lo único que cambia entre catálogos es qué impide inactivar: cada comando
/// lo dice en <c>alInactivar</c>. Reactivar no tiene condiciones. El motivo lo copia la auditoría del comando. (nuevo)
/// </summary>
internal static class ActivacionDeCatalogo
{
    public static async Task<Result> CambiarAsync<T>(
        T? fila, bool activa, Func<T, bool> estaActiva, Action<T, bool> fijar, Error noExiste,
        Func<T, Task<Error?>>? alInactivar, Func<CancellationToken, Task<int>> guardar, CancellationToken ct)
        where T : BaseEntity
    {
        if (fila is null) return Result.Failure(noExiste);
        if (estaActiva(fila) == activa) return Result.Success();
        if (!activa && alInactivar is not null && await alInactivar(fila) is { } impide) return Result.Failure(impide);
        fijar(fila, activa);
        await guardar(ct);
        return Result.Success();
    }
}
