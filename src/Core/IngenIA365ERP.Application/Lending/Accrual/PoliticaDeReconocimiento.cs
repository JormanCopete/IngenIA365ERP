using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>
/// Lo que el ciclo diario (o la puesta al día de una operación) necesita para reconocer la cuota del período al inicio de su ciclo en un crédito
/// (2026-11-16, pedido del dueño; feature 024, fase 4): contra qué se reconoce y qué rubros reconoce solo —los que están en «Automatica»; los de «Manual»
/// los reconoce el proceso de causación <c>PeriodRecognition</c>—. (nuevo)
/// </summary>
public sealed record ReconocimientoAutomatico(ModoDeReconocimiento Modo, IReadOnlyCollection<LoanConcept> Rubros);

/// <summary>
/// La política del reconocimiento al inicio del ciclo a una fecha (feature 024, fase 4): las líneas que reconocen al inicio del ciclo
/// (<c>Causacion.Momento = InicioDelCiclo</c>, el de la línea con caída al general), el modo (<c>Causacion.InicioDelCiclo.Reconocimiento</c>) y los rubros
/// en «Automatica» y en «Manual» (<c>Causacion.ModoInteresCorriente</c>, <c>.ModoSeguro</c>, <c>.ModoAdministracion</c>). De fábrica (todo «Diaria») no
/// aplica a ninguna línea y nada cambia. (nuevo)
/// </summary>
public sealed record PoliticaDeReconocimiento(
    IReadOnlySet<int> LineasAlInicioDelCiclo,
    ModoDeReconocimiento Modo,
    IReadOnlyCollection<LoanConcept> RubrosAutomaticos,
    IReadOnlyCollection<LoanConcept> RubrosManuales)
{
    public static PoliticaDeReconocimiento Diaria { get; } = new(new HashSet<int>(), ModoDeReconocimiento.IngresoDiferido, [], []);

    /// <summary>¿La línea reconoce al inicio del ciclo?</summary>
    public bool AlInicioDelCiclo(int lineaId) => LineasAlInicioDelCiclo.Contains(lineaId);

    /// <summary>Lo que el ciclo reconoce solo en un crédito de esa línea; nulo si la línea causa día a día o ningún rubro está en «Automatica».</summary>
    public ReconocimientoAutomatico? Automatico(int lineaId) =>
        AlInicioDelCiclo(lineaId) && RubrosAutomaticos.Count > 0 ? new ReconocimientoAutomatico(Modo, RubrosAutomaticos) : null;
}

/// <summary>
/// Lee la <see cref="PoliticaDeReconocimiento"/> a una fecha para unas líneas: una lectura del parámetro por línea (con su caída al general) y una por
/// cada parámetro general. Ilegible = de fábrica («Diaria», «IngresoDiferido», «Automatica»). (nuevo)
/// </summary>
public sealed class LectorDePoliticaDeReconocimiento(ReglasDeParametrosDeCartera parametros)
{
    /// <summary>Los rubros que se reconocen al inicio del ciclo con el parámetro de su modo.</summary>
    public static IReadOnlyList<(LoanConcept Rubro, string Parametro)> ModosDeLosRubros { get; } =
    [
        (LoanConcept.CurrentInterest, ParametrosDeCartera.CausacionModoInteresCorriente),
        (LoanConcept.Insurance, ParametrosDeCartera.CausacionModoSeguro),
        (LoanConcept.Administration, ParametrosDeCartera.CausacionModoAdministracion),
    ];

    public async Task<PoliticaDeReconocimiento> LeerAsync(DateOnly fecha, IEnumerable<int> lineas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        var alInicio = new HashSet<int>();
        foreach (var linea in lineas.Distinct())
            if (await AlInicioDelCicloAsync(fecha, linea, ct)) alInicio.Add(linea);
        if (alInicio.Count == 0) return PoliticaDeReconocimiento.Diaria;

        var modo = await parametros.LeerAsync(ParametrosDeCartera.CausacionInicioDelCicloReconocimiento, fecha, ct: ct);
        var automaticos = new List<LoanConcept>();
        var manuales = new List<LoanConcept>();
        foreach (var (rubro, clave) in ModosDeLosRubros)
        {
            var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
            (leido.IsSuccess && string.Equals(leido.Value.Texto?.Trim(), ParametrosDeCartera.CausacionManual, StringComparison.Ordinal) ? manuales : automaticos)
                .Add(rubro);
        }
        return new PoliticaDeReconocimiento(alInicio,
            modo.IsSuccess && string.Equals(modo.Value.Texto?.Trim(), ParametrosDeCartera.ReconocimientoIngresoDirecto, StringComparison.Ordinal)
                ? ModoDeReconocimiento.IngresoDirecto
                : ModoDeReconocimiento.IngresoDiferido,
            automaticos, manuales);
    }

    /// <summary>¿La línea reconoce al inicio del ciclo a esa fecha? El parámetro de la línea, con caída al general; ilegible = «Diaria».</summary>
    public async Task<bool> AlInicioDelCicloAsync(DateOnly fecha, int lineaId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CausacionMomento, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto?.Trim(), ParametrosDeCartera.CausacionAlInicioDelCiclo, StringComparison.Ordinal);
    }

    /// <summary>¿Alguna línea (o la empresa) reconoce al inicio del ciclo a esa fecha?</summary>
    public async Task<bool> AlgunaAsync(DateOnly fecha, IEnumerable<int> lineas, CancellationToken ct)
    {
        foreach (var linea in lineas.Distinct())
            if (await AlInicioDelCicloAsync(fecha, linea, ct)) return true;
        return false;
    }
}
