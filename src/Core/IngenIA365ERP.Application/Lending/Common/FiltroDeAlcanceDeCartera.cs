using System.Linq.Expressions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// El filtro de alcance por oficina y línea de toda consulta de Cartera, y la guarda de todo comando (feature 013, T62, R131, T0285;
/// FR-004, SC-015; api.md §1.3). Falla cerrado: sin asignaciones, nada, salvo el alcance total de <c>Lending.Scope.AllOffices</c> /
/// <c>.AllLines</c>. Molde: <c>FiltroDeAlcance</c> de Inventario. Lo vigila <c>LasConsultasDeCarteraRespetanElAlcance</c>.
///
/// <list type="bullet">
/// <item>Las listas filtran <b>en silencio</b>: lo de fuera no aparece ni cuenta en <c>totalCount</c> (se filtra antes de contar).</item>
/// <item>Un comando llama <see cref="AsegurarAsync"/> con la oficina y la línea que toca: fuera, el <b>mismo</b> 404 del recurso
/// inexistente y el evento <c>Lending.Access.OutOfScope</c> (que sobrevive al rollback del comando).</item>
/// </list>
///
/// <para>Las expresiones son genéricas por Id de oficina (<c>COR_Branches</c>) y de línea, así compilan antes de que existan las
/// consultas de US1–US2, y EF las traduce (<c>Contains</c> sobre un arreglo).</para>
/// </summary>
public static class FiltroDeAlcanceDeCartera
{
    /// <summary>Filas de una oficina: sólo las del alcance.</summary>
    public static IQueryable<T> PorOficina<T>(this IQueryable<T> consulta, AlcanceDeCartera alcance, Expression<Func<T, int>> oficina)
    {
        if (alcance.TodasLasOficinas) return consulta;
        var ids = alcance.Oficinas.ToArray();
        Expression<Func<int, bool>> dentro = o => ids.Contains(o);
        return consulta.Where(Componer(oficina, dentro));
    }

    /// <summary>Filas de una línea de crédito: sólo las del alcance.</summary>
    public static IQueryable<T> PorLinea<T>(this IQueryable<T> consulta, AlcanceDeCartera alcance, Expression<Func<T, int>> linea)
    {
        if (alcance.TodasLasLineas) return consulta;
        var ids = alcance.Lineas.ToArray();
        Expression<Func<int, bool>> dentro = l => ids.Contains(l);
        return consulta.Where(Componer(linea, dentro));
    }

    /// <summary>Créditos, solicitudes, cupos y garantías: los de una oficina <b>y</b> una línea del alcance.</summary>
    public static IQueryable<T> PorOficinaYLinea<T>(
        this IQueryable<T> consulta, AlcanceDeCartera alcance, Expression<Func<T, int>> oficina, Expression<Func<T, int>> linea) =>
        consulta.PorOficina(alcance, oficina).PorLinea(alcance, linea);

    /// <summary>
    /// La guarda de un comando: la oficina y la línea que toca tienen que estar en el alcance (una nula no se mira: lo que no tiene
    /// línea, como un cupo general, sólo se mira por oficina). Fuera, <paramref name="siFuera"/> —el 404 del recurso, indistinguible de
    /// «no existe»— y el evento <c>Lending.Access.OutOfScope</c> por <paramref name="auditoria"/>.
    /// </summary>
    public static async Task<Result> AsegurarAsync(
        this IAlcanceDeCartera alcanceDeLaPeticion,
        int? oficina,
        int? linea,
        Error siFuera,
        LendingAuditEmitter auditoria,
        string recurso,
        Guid? recursoPublicId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(siFuera);
        ArgumentNullException.ThrowIfNull(auditoria);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var dentro = (oficina is not { } o || alcance.IncluyeOficina(o)) && (linea is not { } l || alcance.IncluyeLinea(l));
        if (dentro) return Result.Success();

        await auditoria.EmitirFueraDeAlcanceAsync(recurso, recursoPublicId, ct);
        return Result.Failure(siFuera);
    }

    /// <summary><c>x =&gt; predicado(selector(x))</c>, sin <c>Invoke</c> (que EF no traduce).</summary>
    private static Expression<Func<T, bool>> Componer<T, TValor>(Expression<Func<T, TValor>> selector, Expression<Func<TValor, bool>> predicado) =>
        Expression.Lambda<Func<T, bool>>(
            new ReemplazoDeParametro(predicado.Parameters[0], selector.Body).Visit(predicado.Body),
            selector.Parameters);

    private sealed class ReemplazoDeParametro(ParameterExpression de, Expression por) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == de ? por : base.VisitParameter(node);
    }
}
