using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Inventory;

/// <summary>
/// Implementación de <see cref="ICerrojoPorClave"/> (feature 012, I3, T597): un candado de aplicación ligado a la transacción
/// del propio <see cref="ApplicationDbContext"/> —<c>sp_getapplock</c> con <c>@LockOwner = 'Transaction'</c> en SQL Server,
/// <c>pg_advisory_xact_lock</c> sobre el <c>hashtext</c> de la clave en PostgreSQL—. La clave va como parámetro, nunca en el
/// texto del SQL. Sin transacción no bloquea: no habría nada que proteger. (nuevo)
/// </summary>
public sealed class CerrojoPorClave(ApplicationDbContext db) : ICerrojoPorClave
{
    /// <summary>El espacio de los candados de inventario en PostgreSQL (primer entero de la llave de dos partes).</summary>
    public const int EspacioDeInventario = 12012;

    public async Task BloquearAsync(string clave, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        if (db.Database.CurrentTransaction is null) return;

        var recurso = clave.Length > 255 ? clave[..255] : clave;
        if (db.Database.ProviderName == ProviderModelConventions.PostgreSqlProviderName)
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({EspacioDeInventario}, hashtext({{0}}))", [recurso], ct);
        else
            await db.Database.ExecuteSqlRawAsync(
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; "
                + "IF @r < 0 THROW 51012, 'No se pudo tomar el candado de la clave.', 1;", [recurso], ct);
    }
}
