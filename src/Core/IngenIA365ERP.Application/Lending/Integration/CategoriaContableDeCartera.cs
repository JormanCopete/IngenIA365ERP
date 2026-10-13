using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El lector único de la categoría contable vigente de un crédito (feature 013, T0228, C106; US9, T0702; contracts/contabilidad.md §3.4): la
/// <c>AccountingRating</c> de su línea en el <b>último cierre vigente</b> de Cartera —mensual o el cierre base— anterior a la fecha del hecho,
/// nunca la calificación diaria. Sin cierre, o si el crédito nació después del último, la de <b>crédito nuevo</b>
/// (<see cref="RatingCause.NewLoan"/>, la que se sella al desembolsar); un crédito novado que no tiene línea lleva la del anterior.
///
/// <para>Un cierre deja de contar al reabrirse (su fila en <c>LND_MonthlyCloseReopenings</c>); las líneas de un mensual se buscan por su corrida
/// y las del base por el cierre. Sin contexto (la firma estable de la fase 3, <c>new CategoriaContableDeCartera()</c>) responde siempre crédito
/// nuevo: los que construyen sin base son las pruebas de las piezas tempranas. (nuevo)</para>
/// </summary>
public sealed class CategoriaContableDeCartera(IApplicationDbContext? db = null)
{
    /// <summary>La categoría con que nace un crédito nuevo (Anexo 1: «A»; la norma la trae en <c>newLoan</c>).</summary>
    public const string CategoriaDeCreditoNuevo = "A";

    /// <summary>La categoría contable vigente del crédito <paramref name="loanId"/> para un hecho con fecha contable <paramref name="fecha"/>.</summary>
    public async Task<CategoriaContableVigente> VigenteAsync(int loanId, DateOnly fecha, CancellationToken ct) =>
        (await VigentesAsync([loanId], fecha, ct))[loanId];

    /// <summary>Las de varios créditos a la vez para hechos con fecha contable <paramref name="fecha"/> (las tandas del ciclo y las partes masivas).</summary>
    public Task<IReadOnlyDictionary<int, CategoriaContableVigente>> VigentesAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct) =>
        LeerAsync(loanIds, fecha, incluirElCierreDelDia: false, ct);

    /// <summary>
    /// Las de un saldo al cierre del día <paramref name="corte"/> (la conciliación): a diferencia de un hecho de ese día, el saldo al corte ya
    /// incluye la reclasificación del cierre fechado ese mismo día.
    /// </summary>
    public Task<IReadOnlyDictionary<int, CategoriaContableVigente>> AlCorteAsync(IReadOnlyCollection<int> loanIds, DateOnly corte, CancellationToken ct) =>
        LeerAsync(loanIds, corte, incluirElCierreDelDia: true, ct);

    private async Task<IReadOnlyDictionary<int, CategoriaContableVigente>> LeerAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, bool incluirElCierreDelDia,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loanIds);
        var resultado = loanIds.Distinct().ToDictionary(id => id, _ => CategoriaContableVigente.DeCreditoNuevo);
        if (db is null || resultado.Count == 0) return resultado;

        var cierre = await UltimoCierreVigenteAsync(db, incluirElCierreDelDia ? fecha.AddDays(1) : fecha, ct);
        if (cierre is null) return resultado;

        var ids = resultado.Keys.ToList();
        var origenes = await db.Loans.AsNoTracking().IgnoreQueryFilters()
            .Where(l => ids.Contains(l.Id) && l.OriginLoanId != null && l.OriginLinkKind == LoanLinkKind.Novation)
            .Select(l => new { l.Id, Origen = l.OriginLoanId!.Value })
            .ToDictionaryAsync(x => x.Id, x => x.Origen, ct);
        var buscados = ids.Concat(origenes.Values).Distinct().ToList();
        var lineas = await LineasDe(db, cierre).Where(l => buscados.Contains(l.LoanId))
            .Select(l => new { l.LoanId, l.AccountingRating, l.RatingCause })
            .ToDictionaryAsync(l => l.LoanId, ct);

        foreach (var id in ids)
        {
            if (lineas.TryGetValue(id, out var propia))
                resultado[id] = new CategoriaContableVigente(propia.AccountingRating.ToString(), propia.RatingCause, cierre.PublicId);
            else if (origenes.TryGetValue(id, out var anterior) && lineas.TryGetValue(anterior, out var delAnterior))
                resultado[id] = new CategoriaContableVigente(delAnterior.AccountingRating.ToString(), delAnterior.RatingCause, cierre.PublicId);
        }
        return resultado;
    }

    /// <summary>
    /// El último cierre vigente (no reabierto) con fecha de cierre anterior a <paramref name="fecha"/>: el que da la categoría de los hechos de esa
    /// fecha. Un hecho del último día de un mes ya cerrado no existe (la fecha contable salta al mes siguiente, T0711), salvo los del propio cierre,
    /// que llevan sus dimensiones explícitas.
    /// </summary>
    public static Task<MonthlyClose?> UltimoCierreVigenteAsync(IApplicationDbContext db, DateOnly fecha, CancellationToken ct) =>
        db.MonthlyCloses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CloseDate < fecha && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
            .OrderByDescending(c => c.CloseDate).ThenByDescending(c => c.Kind).ThenByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);

    /// <summary>Las líneas de un cierre: las del mensual por su corrida, las del base por el cierre.</summary>
    public static IQueryable<MonthlyCloseLoan> LineasDe(IApplicationDbContext db, MonthlyClose cierre) =>
        cierre.Kind == MonthlyCloseKind.Base || cierre.ProcessRunId is null
            ? db.MonthlyCloseLoans.AsNoTracking().Where(l => l.MonthlyCloseId == cierre.Id)
            : db.MonthlyCloseLoans.AsNoTracking().Where(l => l.ProcessRunId == cierre.ProcessRunId);
}

/// <summary>
/// La categoría contable de un crédito, por qué la tiene y de qué cierre sale (nulo: no hay cierre todavía, o nació después del último).
/// (nuevo)
/// </summary>
public sealed record CategoriaContableVigente(string Category, RatingCause Cause, Guid? MonthlyClosePublicId)
{
    public static readonly CategoriaContableVigente DeCreditoNuevo =
        new(CategoriaContableDeCartera.CategoriaDeCreditoNuevo, RatingCause.NewLoan, null);
}
