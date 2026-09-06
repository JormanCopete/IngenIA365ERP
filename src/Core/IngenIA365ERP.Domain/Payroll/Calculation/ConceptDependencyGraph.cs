using System.Globalization;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Orden de evaluación de los conceptos compuestos (D-01): un compuesto se calcula
/// después de sus componentes. Detecta ciclos y los nombra con el camino completo,
/// para que el validador de definiciones (FR-028) los rechace al guardar y el motor
/// nunca los encuentre.
/// </summary>
public static class ConceptDependencyGraph
{
    /// <summary>Un componente de un concepto compuesto: signo, código y peso.</summary>
    public sealed record Component(int Sign, string Code, decimal Weight);

    /// <summary>
    /// Interpreta <c>ComponentConceptCodes</c>: «+HEX_DIURNA*1;-OTROS_DESC*0.5». El
    /// signo es opcional (positivo) y el peso también (1).
    /// </summary>
    public static IReadOnlyList<Component> ParseComponents(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return [];
        var result = new List<Component>();
        foreach (var raw in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw;
            var sign = 1;
            if (token.StartsWith('-')) { sign = -1; token = token[1..]; }
            else if (token.StartsWith('+')) { token = token[1..]; }

            var weight = 1m;
            var star = token.IndexOf('*');
            if (star >= 0)
            {
                if (!decimal.TryParse(token[(star + 1)..], NumberStyles.Number, CultureInfo.InvariantCulture, out weight))
                    throw new CalculationRefusedException($"Peso inválido en el componente «{raw}».", [raw]);
                token = token[..star];
            }
            token = token.Trim();
            if (token.Length == 0)
                throw new CalculationRefusedException($"Componente vacío en «{spec}».", [spec]);
            result.Add(new Component(sign, token, weight));
        }
        return result;
    }

    /// <summary>
    /// Ordena los conceptos compuestos de modo que cada uno vaya después de los
    /// compuestos de los que depende. Los no compuestos no participan.
    /// </summary>
    public static IReadOnlyList<PayrollConceptDefinition> TopologicalOrder(IEnumerable<PayrollConceptDefinition> concepts)
    {
        var compuestos = concepts
            .Where(c => c.CalculationKind == CalculationKind.CompositeOfConcepts)
            .ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var ordenados = new List<PayrollConceptDefinition>();
        var estado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 1 = en curso, 2 = listo
        var camino = new Stack<string>();

        void Visitar(string code)
        {
            if (!compuestos.TryGetValue(code, out var def)) return;
            if (estado.TryGetValue(code, out var e))
            {
                if (e == 2) return;
                var ciclo = camino.Reverse()
                    .SkipWhile(c => !c.Equals(code, StringComparison.OrdinalIgnoreCase))
                    .Append(code);
                throw new CalculationRefusedException(
                    $"Los conceptos compuestos forman un ciclo: {string.Join(" -> ", ciclo)}.", [code]);
            }
            estado[code] = 1;
            camino.Push(code);
            foreach (var comp in ParseComponents(def.ComponentConceptCodes))
                Visitar(comp.Code);
            camino.Pop();
            estado[code] = 2;
            ordenados.Add(def);
        }

        foreach (var code in compuestos.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            Visitar(code);

        return ordenados;
    }

    /// <summary>Mensaje del ciclo si lo hay, para el validador de definiciones; nulo si no hay ciclo.</summary>
    public static string? FindCycle(IEnumerable<PayrollConceptDefinition> concepts)
    {
        try { TopologicalOrder(concepts); return null; }
        catch (CalculationRefusedException ex) when (ex.Message.Contains("ciclo", StringComparison.OrdinalIgnoreCase)) { return ex.Message; }
    }
}
