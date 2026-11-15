using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Las migraciones que una migración base absorbió, declaradas por el ensamblado de migraciones de cada motor (una
/// subclase por contexto consolidado). Los identificadores son los del motor: el mismo nombre lógico lleva otra hora en
/// SQL Server y en PostgreSQL.
/// </summary>
public abstract class MigracionesAbsorbidas
{
    /// <summary>El contexto cuyo árbol se consolidó (<c>ApplicationDbContext</c>, por ahora el único).</summary>
    public abstract Type Contexto { get; }

    /// <summary>La migración que crea, sola, lo que creaban las absorbidas.</summary>
    public abstract Type MigracionBase { get; }

    /// <summary>Los identificadores de las absorbidas, en su orden.</summary>
    public abstract IReadOnlyList<string> Absorbidas { get; }

    /// <summary>El identificador de <see cref="MigracionBase"/>, tal como queda en el historial.</summary>
    public string IdDeLaBase => MigracionBase.GetCustomAttribute<MigrationAttribute>()?.Id
        ?? throw new InvalidOperationException($"{MigracionBase.Name} no lleva [Migration].");
}

/// <summary>Cómo está el historial de una base frente a la consolidación.</summary>
public enum EstadoDeConsolidacion
{
    /// <summary>El contexto no tiene árbol consolidado: nada que hacer.</summary>
    NoAplica,

    /// <summary>Base nueva o sin historial: EF aplicará la base, y después se sellan las absorbidas.</summary>
    BaseNueva,

    /// <summary>Tiene la base y todas las absorbidas: nada que hacer.</summary>
    Alineada,

    /// <summary>Vino por el camino viejo y tiene las absorbidas: falta sólo la fila de la base.</summary>
    PorMarcar,

    /// <summary>Tiene la base pero no todas las absorbidas (un sello interrumpido): faltan esas filas.</summary>
    PorSellar,

    /// <summary>Tiene historial pero le faltan absorbidas y no tiene la base: hay que ponerla al día con la versión
    /// anterior a la consolidación antes de desplegar ésta. No se toca.</summary>
    Incompleta,
}

/// <summary>El diagnóstico de una base, sin escribir nada.</summary>
public sealed record DiagnosticoDeConsolidacion(
    EstadoDeConsolidacion Estado,
    string? IdDeLaBase,
    IReadOnlyList<string> FaltanDeLasAbsorbidas,
    int FilasEnElHistorial);

/// <summary>
/// Pone el historial de migraciones de una base en el estado que la consolidación supone (2026-11-15).
///
/// <para>
/// <b>Por qué existe.</b> El árbol operativo se consolidó en una migración base por motor que crea, sola, lo que creaban
/// las absorbidas (90 el día de la consolidación). EF decide qué falta comparando el historial con las migraciones del
/// ensamblado, así que sin esto una base que ya tenía las absorbidas vería la base como pendiente e intentaría crear otra
/// vez sus 673 tablas.
/// </para>
///
/// <para>
/// <b>Qué hace, y siempre lo mismo en los dos sentidos.</b> Una base vieja con todas las absorbidas recibe la fila de la base;
/// una base nueva, después de que EF le aplica la base, recibe una fila por cada absorbida. Al final todas las bases tienen el mismo
/// historial, y eso tiene dos consecuencias buscadas: la versión anterior a la consolidación puede volver a desplegarse
/// sobre cualquiera de ellas sin intentar migrar nada (vuelta atrás), y los sembradores que preguntan por una migración
/// absorbida por su nombre (<c>CarteraNucleo</c>, <c>InventarioComercialNucleo</c>…) siguen encontrándola.
/// </para>
///
/// <para>
/// <b>Lo que nunca hace</b> es marcar una base a medias: si le faltan absorbidas y no tiene la base, lanza
/// <c>[Database.ConsolidacionIncompleta]</c> sin escribir. Marcarla le escondería para siempre las migraciones que no
/// tiene. Se pone al día con la versión anterior y se vuelve a intentar.
/// </para>
///
/// <para>
/// Escribe sólo en <c>__EFMigrationsHistory</c>, con el SQL que el propio EF genera para su historial, y es idempotente:
/// se llama antes y después de cada <c>MigrateAsync</c> (arranque con AutoMigrate, aprovisionador, DbMigrator).
/// </para>
/// </summary>
public static class ConsolidacionDeMigraciones
{
    /// <summary>La consolidación declarada para el contexto, o <c>null</c> si su árbol no se consolidó.</summary>
    public static MigracionesAbsorbidas? DelContexto(Microsoft.EntityFrameworkCore.DbContext db)
    {
        var ensamblado = db.GetService<IMigrationsAssembly>().Assembly;
        var tipo = db.GetType();
        return ensamblado.GetTypes()
            .Where(t => typeof(MigracionesAbsorbidas).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (MigracionesAbsorbidas)Activator.CreateInstance(t)!)
            .SingleOrDefault(c => c.Contexto.IsAssignableFrom(tipo));
    }

    /// <summary>Lee el historial y dice en qué estado está la base. No escribe.</summary>
    public static async Task<DiagnosticoDeConsolidacion> DiagnosticarAsync(
        Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct)
    {
        var consolidacion = DelContexto(db);
        if (consolidacion is null)
            return new DiagnosticoDeConsolidacion(EstadoDeConsolidacion.NoAplica, null, [], 0);

        var aplicadas = await LeerHistorialAsync(db, ct);
        var base_ = consolidacion.IdDeLaBase;
        if (aplicadas.Count == 0)
            return new DiagnosticoDeConsolidacion(EstadoDeConsolidacion.BaseNueva, base_, [], 0);

        var faltan = consolidacion.Absorbidas.Where(m => !aplicadas.Contains(m)).ToList();
        var estado = (aplicadas.Contains(base_), faltan.Count == 0) switch
        {
            (true, true) => EstadoDeConsolidacion.Alineada,
            (true, false) => EstadoDeConsolidacion.PorSellar,
            (false, true) => EstadoDeConsolidacion.PorMarcar,
            (false, false) => EstadoDeConsolidacion.Incompleta,
        };
        return new DiagnosticoDeConsolidacion(estado, base_, faltan, aplicadas.Count);
    }

    /// <summary>
    /// Marca o sella lo que haga falta (ver la clase). Devuelve el estado que encontró. Lanza
    /// <c>[Database.ConsolidacionIncompleta]</c> ante una base a medias, sin escribir.
    /// </summary>
    public static async Task<EstadoDeConsolidacion> AlinearAsync(
        Microsoft.EntityFrameworkCore.DbContext db, string etiqueta, ILogger logger, CancellationToken ct)
    {
        var diagnostico = await DiagnosticarAsync(db, ct);
        switch (diagnostico.Estado)
        {
            case EstadoDeConsolidacion.PorMarcar:
                await InsertarAsync(db, [diagnostico.IdDeLaBase!], etiqueta, logger, ct);
                logger.LogInformation(
                    "[Migraciones.BaseConsolidadaMarcada] BD {Etiqueta}: tenía las {Absorbidas} migraciones que absorbió " +
                    "{Base}; se anotó la base en el historial sin ejecutar nada.",
                    etiqueta, DelContexto(db)!.Absorbidas.Count, diagnostico.IdDeLaBase);
                break;

            case EstadoDeConsolidacion.PorSellar:
                await InsertarAsync(db, diagnostico.FaltanDeLasAbsorbidas, etiqueta, logger, ct);
                logger.LogInformation(
                    "[Migraciones.AbsorbidasSelladas] BD {Etiqueta}: se anotaron {Cuenta} migraciones absorbidas por " +
                    "{Base}, que ya creó lo suyo, para que el historial sea el mismo en todas las bases.",
                    etiqueta, diagnostico.FaltanDeLasAbsorbidas.Count, diagnostico.IdDeLaBase);
                break;

            case EstadoDeConsolidacion.Incompleta:
                throw new InvalidOperationException(
                    $"[Database.ConsolidacionIncompleta] La BD {etiqueta} tiene historial de migraciones pero le faltan " +
                    $"{diagnostico.FaltanDeLasAbsorbidas.Count} de las que absorbió {diagnostico.IdDeLaBase} " +
                    $"(la primera: {diagnostico.FaltanDeLasAbsorbidas[0]}). No se marca una base a medias: póngala al día " +
                    "con la versión anterior a la consolidación (DbMigrator migrate) y vuelva a desplegar ésta.");
        }

        return diagnostico.Estado;
    }

    private static async Task<HashSet<string>> LeerHistorialAsync(
        Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct)
    {
        if (!await db.Database.CanConnectAsync(ct)) return new HashSet<string>(StringComparer.Ordinal);

        var historial = db.GetService<IHistoryRepository>();
        if (!await historial.ExistsAsync(ct)) return new HashSet<string>(StringComparer.Ordinal);

        return (await historial.GetAppliedMigrationsAsync(ct))
            .Select(r => r.MigrationId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Inserta las filas en una sola transacción, con el SQL que EF genera para su historial (mismo esquema, misma tabla,
    /// mismo motor). Si otra instancia se adelantó, vuelve a leer y da por bueno lo que ya esté.
    /// </summary>
    private static async Task InsertarAsync(
        Microsoft.EntityFrameworkCore.DbContext db, IReadOnlyList<string> ids, string etiqueta, ILogger logger, CancellationToken ct)
    {
        var historial = db.GetService<IHistoryRepository>();
        var version = ProductInfo.GetVersion();
        var estrategia = db.Database.CreateExecutionStrategy();

        try
        {
            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(ct);
                foreach (var id in ids)
                {
                    var sql = historial.GetInsertScript(new HistoryRow(id, version));
                    await db.Database.ExecuteSqlRawAsync(sql, ct);
                }
                await transaccion.CommitAsync(ct);
            });
        }
        catch (Exception ex)
        {
            // Otra instancia pudo escribirlas entre la lectura y la escritura: si están todas, el resultado es el mismo
            // y se deja dicho; si no, el error es de verdad y sube.
            if (!await TodasEstanAsync(db, ids, ct)) throw;
            logger.LogInformation(ex,
                "[Migraciones.HistorialYaEscrito] BD {Etiqueta}: otra instancia anotó las mismas {Cuenta} fila(s) antes; " +
                "se da por bueno.", etiqueta, ids.Count);
        }
    }

    private static async Task<bool> TodasEstanAsync(
        Microsoft.EntityFrameworkCore.DbContext db, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var aplicadas = await LeerHistorialAsync(db, ct);
        return ids.All(aplicadas.Contains);
    }
}
