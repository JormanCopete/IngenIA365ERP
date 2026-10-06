using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T3, A10, C89): el espacio de nombres <c>Lending</c> es sólo de la Cartera nueva; lo
/// conservado de ahorros y aportes se muda a <c>Savings</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0005) y la llena la fase Fundacional B con los tipos conservados que se mudan; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class ElEspacioLendingEsSoloCartera
{
    /// <summary>Nombres completos de tipos conservados que deben vivir en <c>Savings</c>, no en <c>Lending</c>.</summary>
    private static readonly string[] TiposConservados = [];

    [Fact]
    public void Lo_conservado_vive_en_Savings()
    {
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Concat(typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes())
            .ToLookup(t => t.Name, t => t.Namespace ?? string.Empty, StringComparer.Ordinal);
        var infracciones = TiposConservados
            .Where(n => !tipos[n].Any() || tipos[n].Any(ns => !ns.Contains(".Savings", StringComparison.Ordinal)))
            .Select(n => $"{n}: no está en un espacio Savings")
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Lo conservado sigue en el espacio Lending (T3, A10)");
    }
}
