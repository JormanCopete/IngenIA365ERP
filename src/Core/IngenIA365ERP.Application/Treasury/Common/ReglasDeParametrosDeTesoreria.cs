using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// Las reglas del alta de una vigencia de las claves <c>TES</c> (feature 015, T0042): por ahora ninguna propia más allá de la definición (tipo,
/// rango, la tarifa del GMF como fracción hasta 1, el piso de <c>Conservacion.Anios</c>). Existen para que una vigencia de Tesorería no pase por
/// las reglas de Inventario (período de inventario cerrado). (nuevo)
/// </summary>
public sealed class ReglasDeParametrosDeTesoreria : IReglasDeParametrosDeModulo
{
    public string Modulo => ParametrosDeRecaudoYTesoreria.ModuloTesoreria;

    public Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct) =>
        Task.FromResult(Result.Success(DecisionDeReglasDeParametro.Adelante));
}
