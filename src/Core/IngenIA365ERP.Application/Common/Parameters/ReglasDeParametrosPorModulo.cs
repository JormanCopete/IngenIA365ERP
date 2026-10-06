using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// Las reglas de parámetros de un módulo (feature 013, T42, T43): <see cref="ReglasDeParametrosPorModulo"/> las corre para las altas
/// de sus claves en lugar de las de la plataforma de Inventario. Cartera registra <c>ReglasDeParametrosDeCartera</c> para <c>LND</c>. (nuevo)
/// </summary>
public interface IReglasDeParametrosDeModulo : IReglasDeParametros
{
    /// <summary>El módulo de las claves que evalúa (<c>COR_ParameterVersions.Module</c>).</summary>
    string Modulo { get; }
}

/// <summary>
/// El <see cref="IReglasDeParametros"/> de la plataforma (feature 013, T43): el alta de una clave de un módulo que registró sus reglas
/// (<see cref="IReglasDeParametrosDeModulo"/>) pasa sólo por ellas; las demás, por las de siempre (<c>ReglasDePlataformaDeInventario</c>,
/// que cubren INV, TAX y FE). Así una vigencia de Cartera no choca con el período de inventario cerrado y nada cambia para el
/// comercio. (nuevo)
/// </summary>
public sealed class ReglasDeParametrosPorModulo : IReglasDeParametros
{
    private readonly IReglasDeParametros _porDefecto;
    private readonly Dictionary<string, IReglasDeParametrosDeModulo> _porModulo = new(StringComparer.OrdinalIgnoreCase);

    public ReglasDeParametrosPorModulo(IReglasDeParametros porDefecto, IEnumerable<IReglasDeParametrosDeModulo> modulos)
    {
        _porDefecto = porDefecto;
        foreach (var reglas in modulos)
            if (!_porModulo.TryAdd(reglas.Modulo, reglas))
                throw new InvalidOperationException($"El módulo {reglas.Modulo} registró dos veces sus reglas de parámetros.");
    }

    public Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct) =>
        _porModulo.TryGetValue(alta.Definicion.Modulo, out var reglas)
            ? reglas.EvaluarAsync(alta, ct)
            : _porDefecto.EvaluarAsync(alta, ct);
}
