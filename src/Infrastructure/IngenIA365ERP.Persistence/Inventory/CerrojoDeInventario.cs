using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Inventory;

/// <summary>
/// Implementación de <see cref="ICerrojoDeInventario"/> (feature 012, T15, T138): ejecuta el SQL de
/// <see cref="SqlDelCerrojo"/> con el propio <see cref="ApplicationDbContext"/> —su conexión y su transacción, sin abrir
/// otra—. Exige una transacción en curso (la abre <c>TransaccionExplicita</c>): fuera de ella los candados se soltarían
/// al terminar cada sentencia y el cerrojo no protegería nada.
/// </summary>
public sealed class CerrojoDeInventario(ApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj) : ICerrojoDeInventario
{
    public async Task BloquearAsync(PedidoDeCerrojo pedido, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc);
        foreach (var sentencia in SqlDelCerrojo.Sentencias(Motor(), pedido, actor.Name, ahora))
            await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task BloquearNumeracionAsync(int documentSequenceId, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var sentencia = SqlDelCerrojo.Numeracion(Motor(), documentSequenceId);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    private DatabaseProvider Motor() => db.Database.ProviderName == ProviderModelConventions.PostgreSqlProviderName
        ? DatabaseProvider.PostgreSql
        : DatabaseProvider.SqlServer;

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El cerrojo de inventario sólo se toma dentro de una transacción (TransaccionExplicita): fuera de ella no protege nada.");
    }
}
