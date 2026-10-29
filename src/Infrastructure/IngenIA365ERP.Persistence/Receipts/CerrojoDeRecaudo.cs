using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Receipts;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Receipts;

/// <summary>
/// Implementación de <see cref="ICerrojoDeRecaudo"/> (feature 015, T0038): ejecuta el SQL de <see cref="SqlDelCerrojoDeRecaudo"/> con el propio
/// <see cref="ApplicationDbContext"/> —su conexión y su transacción— y anota cada nivel en <see cref="OrdenDeCerrojos"/>, que lanza si llega
/// fuera de orden. Exige una transacción en curso (la abre <c>TransaccionExplicita</c>). (nuevo)
/// </summary>
public sealed class CerrojoDeRecaudo(ApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj, OrdenDeCerrojos orden) : ICerrojoDeRecaudo
{
    public async Task TomarSetupAsync(bool exclusivo = false, CancellationToken ct = default)
    {
        ExigirTransaccion();
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Configuracion), SqlDelCerrojoDeRecaudo.TablaSetup);
        var sentencia = SqlDelCerrojoDeRecaudo.Setup(Motor(), SqlDelCerrojoDeRecaudo.TablaSetup, exclusivo);
        await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task BloquearAsync(NivelDeRecaudo nivel, string tabla, IReadOnlyCollection<long> ids, bool exclusivo = true, CancellationToken ct = default)
    {
        ExigirTransaccion();
        if (!SqlDelCerrojoDeRecaudo.TablasDelNivel.TryGetValue(nivel, out var tablas) || !tablas.Contains(tabla, StringComparer.Ordinal))
            throw new ArgumentException($"{tabla} no es una tabla del nivel {nivel} (data-model §0.5).", nameof(tabla));
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(nivel), tabla);
        var sentencia = SqlDelCerrojoDeRecaudo.Filas(Motor(), tabla, ids, exclusivo);
        if (sentencia is not null) await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task BloquearPagadoresAsync(IReadOnlyCollection<int> personIds, CancellationToken ct = default)
    {
        ExigirTransaccion();
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Pagadores), SqlDelCerrojoDeRecaudo.TablaPagadores);
        if (personIds.Count == 0) return;
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc);
        foreach (var sentencia in SqlDelCerrojoDeRecaudo.Pagadores(Motor(), personIds, actor.Name, ahora))
            await db.Database.ExecuteSqlRawAsync(sentencia.Sql, sentencia.Parametros, ct);
    }

    public async Task AsegurarNumeracionAsync(string clave, string prefijo, CancellationToken ct = default)
    {
        // Sin transacción el candado no protegería nada: el numerador agrega la fila como siempre.
        if (db.Database.CurrentTransaction is null) return;
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc);
        var sentencia = SqlDelCerrojoDeRecaudo.NumeracionNueva(Motor(), clave, prefijo, CatalogoDeRecaudo.Modulo, actor.Name, ahora);
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
                "El cerrojo de Recaudo sólo se toma dentro de una transacción (TransaccionExplicita): fuera de ella no protege nada.");
    }
}
