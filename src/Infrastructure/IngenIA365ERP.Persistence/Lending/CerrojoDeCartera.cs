using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Lending;

/// <summary>
/// Implementación de <see cref="ICerrojoDeCartera"/> (feature 013, T34, T0206): sólo ejecuta el SQL de
/// <see cref="SqlDelCerrojoDeCartera"/> con el propio <see cref="ApplicationDbContext"/> —su conexión y su transacción, sin abrir
/// otra—. Exige una transacción en curso (la abre <c>TransaccionExplicita</c>): fuera de ella los candados se soltarían al terminar
/// cada sentencia y el cerrojo no protegería nada. El nombre del actor que firma las filas de deudor nuevas viaja como parámetro.
/// (nuevo)
/// </summary>
public sealed class CerrojoDeCartera(ApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj) : ICerrojoDeCartera
{
    public async Task TomarSetupAsync(bool exclusivo, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var sentencia = SqlDelCerrojoDeCartera.Setup(Motor(), exclusivo);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task BloquearDeudoresAsync(IReadOnlyCollection<int> personIds, ModoDeEspera modo, CancellationToken ct = default)
    {
        ExigirTransaccion();
        if (personIds.Count == 0) return;
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc);
        foreach (var sentencia in SqlDelCerrojoDeCartera.Deudores(Motor(), personIds, modo, actor.Name, ahora))
            await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task<IReadOnlyList<int>> BloquearCreditosAsync(IReadOnlyCollection<int> loanIds, ModoDeEspera modo, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var sentencia = SqlDelCerrojoDeCartera.Creditos(Motor(), loanIds, modo);
        if (sentencia is null) return [];
        return await db.Database.SqlQueryRaw<int>(sentencia.Sql, [.. sentencia.Parametros]).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> BloquearCuentasAsync(IReadOnlyCollection<int> memberAccountIds, ModoDeEspera modo, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var sentencia = SqlDelCerrojoDeCartera.Cuentas(Motor(), memberAccountIds, modo);
        if (sentencia is null) return [];
        return await db.Database.SqlQueryRaw<int>(sentencia.Sql, [.. sentencia.Parametros]).ToListAsync(ct);
    }

    public async Task BloquearNumeracionAsync(int numberSequenceId, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var sentencia = SqlDelCerrojoDeCartera.Numeracion(Motor(), numberSequenceId);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    private DatabaseProvider Motor() => db.Database.ProviderName == ProviderModelConventions.PostgreSqlProviderName
        ? DatabaseProvider.PostgreSql
        : DatabaseProvider.SqlServer;

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El cerrojo de Cartera sólo se toma dentro de una transacción (TransaccionExplicita): fuera de ella no protege nada.");
    }
}
