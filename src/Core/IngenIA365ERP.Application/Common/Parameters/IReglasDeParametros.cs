using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// Lo que se va a registrar, ya validado contra la definición: el valor en texto canónico y el ámbito resuelto
/// (nulo si es general o si vino una cadena). (nuevo)
/// </summary>
public sealed record AltaDeParametro(
    DefinicionDeParametro Definicion,
    ParameterScopeKind ScopeKind,
    AmbitoDeParametro? Ambito,
    string? Chain,
    string Valor,
    DateOnly ValidFrom,
    bool ConfirmFiscalWithoutPosting);

/// <summary>
/// Lo que deciden las reglas del módulo: con <see cref="Ambitos"/> nulo se escribe la vigencia del ámbito pedido;
/// si no, una por cada entidad de la lista (la cadena de un modo de paso, FR-075), que también se informa como
/// <see cref="AfectadosPorTipoDeDocumento"/>. (nuevo)
/// </summary>
public sealed record DecisionDeReglasDeParametro(
    IReadOnlyList<AmbitoDeParametro>? Ambitos,
    IReadOnlyList<AmbitoDeParametro>? AfectadosPorTipoDeDocumento)
{
    public static DecisionDeReglasDeParametro Adelante { get; } = new(null, null);
}

/// <summary>
/// El gancho de reglas por módulo que <c>AddParameterVersionCommand</c> corre antes de guardar (feature 012, T21,
/// T071): cambio de método y ámbito de costeo sólo al inicio de un período abierto
/// (<c>Parameters.RequiresPeriodStart</c>), modo de paso por cadena y con confirmación para tipos fiscales,
/// <c>Parameters.ValidFromInClosedPeriod</c>. Lo implementa Inventario en US3 (T286); la plataforma deja
/// <see cref="ReglasDeParametrosVacias"/>. (nuevo)
/// </summary>
public interface IReglasDeParametros
{
    Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct);
}

/// <summary>Sin reglas de módulo (fases 1–2): todo alta que pasó la definición sigue adelante. (nuevo)</summary>
public sealed class ReglasDeParametrosVacias : IReglasDeParametros
{
    public Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct) =>
        Task.FromResult(Result.Success(DecisionDeReglasDeParametro.Adelante));
}

/// <summary>
/// Lo que un módulo hace <b>además</b> de guardar la vigencia (feature 012, I5, T841): corre dentro de <c>AddParameterVersionCommand</c>,
/// después de agregar las vigencias y antes del <c>SaveChanges</c>, en la misma transacción. Inventario lo usa para el cambio de método
/// o de ámbito de costeo (<c>CambioDeMetodoDeCosteo</c>, la única vía, T21): genera el documento de ajuste de costo del sistema y rehace
/// los estados de costo y las capas. Un fallo no deja nada guardado. (nuevo)
/// </summary>
public interface IEfectoDeAltaDeParametro
{
    Task<Result> AplicarAsync(AltaDeParametro alta, string motivo, string? fuenteLegal, CancellationToken ct);
}
