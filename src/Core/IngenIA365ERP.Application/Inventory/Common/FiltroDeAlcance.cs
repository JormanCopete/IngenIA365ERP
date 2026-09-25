using System.Linq.Expressions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// El filtro de alcance por bodega y punto de venta de toda consulta de inventario, y la guarda de todo comando (feature
/// 012, T35, T087; FR-009; data-model §5.2, §21). Falla cerrado: sin asignaciones, nada, salvo el alcance total que dan
/// <c>Inventory.Scope.AllWarehouses</c> / <c>Inventory.Scope.AllPointsOfSale</c>. Es la <b>única</b> que existe: la
/// base de inventario (fase 3) la amplía si le falta algo, no crea otra. Lo vigila
/// <c>LasConsultasDeInventarioRespetanElAlcance</c>.
///
/// <list type="bullet">
/// <item>Existencias, kardex y todo lo que tiene una bodega: <see cref="PorBodega{T}"/>.</item>
/// <item>Documentos: <see cref="DocumentosPorBodega{T}"/>, por bodega de origen <b>o</b> de destino. Un documento
/// <b>sin bodega</b> (factura de proveedor de varias bodegas, costos adicionales, ajuste de costo, caja) se ve si
/// alguna bodega de sus orígenes (<c>INV_DocumentLinks</c>) está en el alcance
/// (<see cref="DocumentoSinBodegaVisible"/>) y se opera si todas lo están (<see cref="DocumentoSinBodegaOperable"/>).</item>
/// <item>Sesiones de caja: <see cref="PorPunto{T}"/>.</item>
/// <item>La bodega de tránsito se ve a través de los traslados (su destino y su origen son bodegas del alcance); su
/// existencia propia, sólo con alcance total o asignación explícita, que es lo que ya hace <see cref="PorBodega{T}"/>:
/// no está en el alcance de nadie que no la tenga asignada.</item>
/// <item>Un comando: <see cref="AsegurarAsync"/> con cada bodega y punto que toca; fuera, el 404 de la entidad.</item>
/// </list>
///
/// <para>
/// Las expresiones son genéricas por Id de bodega y punto, así compilan antes de que existan las entidades de la base
/// de inventario y las historias US1–US3, y EF las traduce (<c>Contains</c> sobre un arreglo y <c>Any</c> sobre una
/// navegación). Sobre <c>IQueryable</c> en memoria funcionan igual.
/// </para>
/// </summary>
public static class FiltroDeAlcance
{
    /// <summary>Filas de una bodega (existencias, kardex, ubicaciones, conteos): sólo las del alcance.</summary>
    public static IQueryable<T> PorBodega<T>(this IQueryable<T> consulta, AlcanceDeInventario alcance, Expression<Func<T, int>> bodega)
    {
        if (alcance.TodasLasBodegas) return consulta;
        var ids = alcance.Bodegas.ToArray();
        Expression<Func<int, bool>> dentro = b => ids.Contains(b);
        return consulta.Where(Componer(bodega, dentro));
    }

    /// <summary>Filas de un punto de venta (sesiones, cajas, arqueos): sólo las del alcance.</summary>
    public static IQueryable<T> PorPunto<T>(this IQueryable<T> consulta, AlcanceDeInventario alcance, Expression<Func<T, int>> punto)
    {
        if (alcance.TodosLosPuntos) return consulta;
        var ids = alcance.Puntos.ToArray();
        Expression<Func<int, bool>> dentro = p => ids.Contains(p);
        return consulta.Where(Componer(punto, dentro));
    }

    /// <summary>
    /// Documentos: visibles si su bodega de origen o la de destino está en el alcance; los que no tienen ninguna de las
    /// dos, si alguna bodega de sus orígenes lo está (data-model §5.2).
    /// </summary>
    /// <param name="origen">La bodega del documento (<c>WarehouseId</c>).</param>
    /// <param name="destino">La bodega de destino de un traslado (<c>DestinationWarehouseId</c>).</param>
    /// <param name="bodegasDeSusOrigenes">Las bodegas de los documentos de los que nace, por <c>INV_DocumentLinks</c>.</param>
    public static IQueryable<T> DocumentosPorBodega<T>(
        this IQueryable<T> consulta,
        AlcanceDeInventario alcance,
        Expression<Func<T, int?>> origen,
        Expression<Func<T, int?>> destino,
        Expression<Func<T, IEnumerable<int>>> bodegasDeSusOrigenes)
    {
        if (alcance.TodasLasBodegas) return consulta;
        var ids = alcance.Bodegas.ToArray();

        Expression<Func<int?, bool>> dentro = b => b != null && ids.Contains(b.Value);
        Expression<Func<int?, bool>> nula = b => b == null;
        Expression<Func<IEnumerable<int>, bool>> algunaDentro = bs => bs.Any(b => ids.Contains(b));

        var parametro = origen.Parameters[0];
        var porOrigen = Componer(origen, dentro).Body;
        var porDestino = Reemplazar(Componer(destino, dentro), parametro);
        var sinBodega = Expression.AndAlso(
            Componer(origen, nula).Body,
            Expression.AndAlso(Reemplazar(Componer(destino, nula), parametro), Reemplazar(Componer(bodegasDeSusOrigenes, algunaDentro), parametro)));

        var cuerpo = Expression.OrElse(Expression.OrElse(porOrigen, porDestino), sinBodega);
        return consulta.Where(Expression.Lambda<Func<T, bool>>(cuerpo, parametro));
    }

    /// <summary>Un documento sin bodega se ve si alguna bodega de sus orígenes está en el alcance.</summary>
    public static bool DocumentoSinBodegaVisible(AlcanceDeInventario alcance, IEnumerable<int> bodegasDeSusOrigenes) =>
        alcance.TodasLasBodegas || bodegasDeSusOrigenes.Any(alcance.IncluyeBodega);

    /// <summary>Un documento sin bodega se opera si todas las bodegas de sus orígenes están en el alcance (y tiene alguna).</summary>
    public static bool DocumentoSinBodegaOperable(AlcanceDeInventario alcance, IEnumerable<int> bodegasDeSusOrigenes)
    {
        if (alcance.TodasLasBodegas) return true;
        var bodegas = bodegasDeSusOrigenes.ToList();
        return bodegas.Count > 0 && bodegas.All(alcance.IncluyeBodega);
    }

    /// <summary>
    /// La guarda de un comando: cada bodega y cada punto que toca tiene que estar en el alcance de quien lo pide. Fuera,
    /// <paramref name="siFuera"/> —el 404 de la entidad que el comando busca, indistinguible de «no existe»—, o el 404
    /// de la bodega o del punto si no se indica.
    /// </summary>
    public static async Task<Result> AsegurarAsync(
        this IAlcanceDeInventario alcanceDeLaPeticion,
        IEnumerable<int> bodegas,
        IEnumerable<int>? puntos = null,
        Error? siFuera = null,
        CancellationToken ct = default)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!bodegas.All(alcance.IncluyeBodega))
            return Result.Failure(siFuera ?? ErroresDeAlcance.BodegaInexistente());
        if (puntos is not null && !puntos.All(alcance.IncluyePunto))
            return Result.Failure(siFuera ?? ErroresDeAlcance.PuntoInexistente());
        return Result.Success();
    }

    /// <summary><c>x =&gt; predicado(selector(x))</c>, sin <c>Invoke</c> (que EF no traduce).</summary>
    private static Expression<Func<T, bool>> Componer<T, TValor>(Expression<Func<T, TValor>> selector, Expression<Func<TValor, bool>> predicado) =>
        Expression.Lambda<Func<T, bool>>(
            new ReemplazoDeParametro(predicado.Parameters[0], selector.Body).Visit(predicado.Body),
            selector.Parameters);

    /// <summary>El cuerpo de <paramref name="lambda"/> con su parámetro cambiado por <paramref name="parametro"/>.</summary>
    private static Expression Reemplazar<T>(Expression<Func<T, bool>> lambda, ParameterExpression parametro) =>
        new ReemplazoDeParametro(lambda.Parameters[0], parametro).Visit(lambda.Body);

    private sealed class ReemplazoDeParametro(ParameterExpression de, Expression por) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == de ? por : base.VisitParameter(node);
    }
}
