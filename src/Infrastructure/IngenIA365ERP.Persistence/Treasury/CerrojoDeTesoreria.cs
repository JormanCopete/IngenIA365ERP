using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Treasury;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Treasury;

/// <summary>
/// Implementación de <see cref="ICerrojoDeTesoreria"/> (feature 015, T0038): el SQL de <see cref="SqlDelCerrojoDeTesoreria"/> con el propio
/// <see cref="ApplicationDbContext"/>, anotando cada nivel en la misma <see cref="OrdenDeCerrojos"/> que Recaudo: así una fila de Recaudo pedida
/// después de una de Tesorería lanza (CH16). Exige una transacción en curso. (nuevo)
/// </summary>
public sealed class CerrojoDeTesoreria(ApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj, OrdenDeCerrojos orden) : ICerrojoDeTesoreria
{
    public async Task TomarSetupAsync(bool exclusivo = false, CancellationToken ct = default)
    {
        ExigirTransaccion();
        orden.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Configuracion), SqlDelCerrojoDeTesoreria.TablaSetup);
        var sentencia = SqlDelCerrojoDeTesoreria.Setup(Motor(), exclusivo);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task BloquearAsync(NivelDeTesoreria nivel, IReadOnlyCollection<long> ids, bool exclusivo = true, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var sentencia = SqlDelCerrojoDeTesoreria.Filas(Motor(), nivel, ids, exclusivo);
        orden.Tomar(OrdenDeCerrojos.DeTesoreria(nivel), SqlDelCerrojoDeTesoreria.TablaDelNivel[nivel]);
        if (sentencia is not null) await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task AsegurarNumeracionAsync(string clave, string prefijo, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null) return;
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc);
        var sentencia = SqlDelCerrojoDeRecaudo.NumeracionNueva(Motor(), clave, prefijo, CatalogoDeTesoreria.Modulo, actor.Name, ahora);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task BloquearNumeracionAsync(int sequenceId, CancellationToken ct = default)
    {
        ExigirTransaccion();
        orden.Tomar(OrdenDeCerrojos.Numeracion, SqlDelCerrojoDeRecaudo.TablaNumeracion);
        var sentencia = SqlDelCerrojoDeRecaudo.Numeracion(Motor(), sequenceId);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    private DatabaseProvider Motor() => db.Database.ProviderName == ProviderModelConventions.PostgreSqlProviderName
        ? DatabaseProvider.PostgreSql
        : DatabaseProvider.SqlServer;

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "El cerrojo de Tesorería sólo se toma dentro de una transacción (TransaccionExplicita): fuera de ella no protege nada.");
    }
}
