using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IngenIA365ERP.Application.Common.Persistence;

/// <summary>
/// Un punto de guardado con nombre sobre la transacción en curso (feature 013, US6, T0583; C25; DT §2.6), junto a <see cref="TransaccionExplicita"/>.
/// Lo usa quien corre <b>dentro</b> de la unidad de trabajo de otro y no puede tumbarla con una falla técnica propia —la reserva del número de una
/// venta a crédito dentro de la confirmación de Comercial—:
/// <list type="bullet">
/// <item><see cref="EjecutarAsync{T}"/> abre el punto, corre el trabajo y lo libera al terminar;</item>
/// <item>ante una excepción técnica vuelve al punto —en PostgreSQL eso además deja la transacción de afuera utilizable, que de otro modo quedaría
/// abortada; en SQL Server deshace sólo lo del trabajo— y devuelve lo que diga <c>alFallar</c>: la transacción de afuera sigue y decide ella;</item>
/// <item>la cancelación de quien llama sube sin tocar nada.</item>
/// </list>
/// Sin transacción en curso (InMemory, un doble) el trabajo corre tal cual y la falla sólo se captura. Nunca abre ni confirma una transacción. (nuevo)
/// </summary>
public static class PuntoDeGuardado
{
    /// <summary>
    /// Corre <paramref name="trabajo"/> bajo el punto de guardado <paramref name="nombre"/>; si lanza (salvo la cancelación de quien llama), vuelve al
    /// punto y devuelve <paramref name="alFallar"/> con la excepción.
    /// </summary>
    public static async Task<T> EjecutarAsync<T>(IApplicationDbContext db, string nombre, Func<Task<T>> trabajo, Func<Exception, T> alFallar,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentNullException.ThrowIfNull(trabajo);
        ArgumentNullException.ThrowIfNull(alFallar);
        var transaccion = EnCurso(db);
        var agregadasAntes = Agregadas(db);
        if (transaccion is not null) await transaccion.CreateSavepointAsync(nombre, ct);
        try
        {
            var resultado = await trabajo();
            if (transaccion is not null && transaccion.SupportsSavepoints) await LiberarAsync(transaccion, nombre, ct);
            return resultado;
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
        {
            if (transaccion is not null) await transaccion.RollbackToSavepointAsync(nombre, ct);
            DescartarLoQueQuedo(db, agregadasAntes);
            return alFallar(e);
        }
    }

    /// <summary>La transacción en curso del contexto, o nula (InMemory, un doble, sin transacción).</summary>
    public static IDbContextTransaction? EnCurso(IApplicationDbContext db) => (db as DbContext)?.Database.CurrentTransaction;

    /// <summary>
    /// Libera el punto. SQL Server no tiene <c>RELEASE SAVEPOINT</c> (su transacción de EF lo trata como nada); en PostgreSQL lo libera.
    /// </summary>
    private static Task LiberarAsync(IDbContextTransaction transaccion, string nombre, CancellationToken ct) => transaccion.ReleaseSavepointAsync(nombre, ct);

    /// <summary>Las entidades agregadas y todavía sin guardar antes de abrir el punto (las de quien llama).</summary>
    private static HashSet<object> Agregadas(IApplicationDbContext db) =>
        db is DbContext ctx
            ? ctx.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance)
            : [];

    /// <summary>
    /// Lo que el trabajo dejó agregado sin guardar (volvió al punto): se saca del <c>ChangeTracker</c> para que el <c>SaveChanges</c> de afuera no lo
    /// escriba. Lo que quien llama ya tenía agregado antes del punto no se toca.
    /// </summary>
    private static void DescartarLoQueQuedo(IApplicationDbContext db, HashSet<object> agregadasAntes)
    {
        if (db is not DbContext ctx) return;
        foreach (var entrada in ctx.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && !agregadasAntes.Contains(e.Entity)).ToList())
            entrada.State = EntityState.Detached;
    }
}
