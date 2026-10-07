using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La categoría contable vigente de un crédito (feature 013, T0228, C106; contracts/contabilidad.md §3.4): la oficial del último cierre
/// vigente de Cartera (mensual, o el cierre base), nunca la calificación diaria. Esqueleto con firma estable: mientras no existan los
/// cierres (US9, <c>CarteraRiesgo</c>) todo crédito lleva la de <b>crédito nuevo</b> (<see cref="RatingCause.NewLoan"/>). US9 (T0702)
/// la completa con el último cierre vigente y la cablea en <c>EmisionDeCartera</c> para la dimensión <c>riskCategory</c>. (nuevo)
/// </summary>
public sealed class CategoriaContableDeCartera
{
    /// <summary>La categoría con que nace un crédito nuevo mientras el motor de calificación no exista (Anexo 1: «A»).</summary>
    public const string CategoriaDeCreditoNuevo = "A";

    /// <summary>La categoría contable vigente del crédito <paramref name="loanId"/> a <paramref name="fecha"/>.</summary>
    public Task<CategoriaContableVigente> VigenteAsync(int loanId, DateOnly fecha, CancellationToken ct) =>
        Task.FromResult(CategoriaContableVigente.DeCreditoNuevo);
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
