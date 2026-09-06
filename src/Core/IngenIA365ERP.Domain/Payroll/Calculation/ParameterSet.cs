using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Los parámetros legales vigentes a una fecha, indexados por código. Resuelve cada
/// código a la vigencia correcta y, si falta, se niega nombrándolo (FR-011). Es lo
/// único que el motor sabe de los parámetros: códigos y vigencias, nunca valores.
/// </summary>
public sealed class ParameterSet
{
    private readonly Dictionary<string, PayrollLegalParameter> _byCode;
    public DateTime AsOf { get; }

    public ParameterSet(IEnumerable<PayrollLegalParameter> parameters, DateTime asOf)
    {
        AsOf = asOf.Date;
        // Si llegan varias vigencias del mismo código, manda la más reciente que ya aplique.
        _byCode = parameters
            .Where(p => !p.IsDeleted && p.IsValidAt(AsOf))
            .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ValidFrom).First(), StringComparer.OrdinalIgnoreCase);
    }

    public bool Has(string code) => _byCode.ContainsKey(code);

    public IReadOnlyList<string> Missing(IEnumerable<string> codes) => codes.Where(c => !Has(c)).ToList();

    public PayrollLegalParameter Get(string code) =>
        _byCode.TryGetValue(code, out var p)
            ? p
            : throw CalculationRefusedException.MissingParameters([code], AsOf);

    /// <summary>Valor escalar (Amount o Percent).</summary>
    public decimal Value(string code)
    {
        var p = Get(code);
        if (p.Kind == LegalParameterKind.RangeTable || p.Value is null)
            throw new CalculationRefusedException(
                $"El parámetro legal {code} (vigente desde {p.ValidFrom:yyyy-MM-dd}) no tiene valor escalar.", [code]);
        return p.Value.Value;
    }

    /// <summary>Porcentaje como fracción (4 → 0,04).</summary>
    public decimal Fraction(string code) => Value(code) / 100m;

    public PayrollLegalParameter Table(string code)
    {
        var p = Get(code);
        if (p.Kind != LegalParameterKind.RangeTable || p.Ranges.Count == 0)
            throw new CalculationRefusedException(
                $"El parámetro legal {code} (vigente desde {p.ValidFrom:yyyy-MM-dd}) no es una tabla por rangos con tramos.", [code]);
        return p;
    }

    public ExplanationParameter Describe(string code)
    {
        var p = Get(code);
        return new ExplanationParameter(p.Code, p.ValidFrom, p.Value);
    }
}

/// <summary>Las versiones de concepto vigentes a una fecha, por código.</summary>
public sealed class ConceptSet
{
    private readonly Dictionary<string, PayrollConceptDefinition> _byCode;
    public DateTime AsOf { get; }

    public ConceptSet(IEnumerable<PayrollConceptDefinition> concepts, DateTime asOf)
    {
        AsOf = asOf.Date;
        _byCode = concepts
            .Where(c => !c.IsDeleted && c.IsValidAt(AsOf))
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ValidFrom).First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<PayrollConceptDefinition> All => _byCode.Values;

    public bool Has(string code) => _byCode.ContainsKey(code);

    public PayrollConceptDefinition? Find(string code) => _byCode.GetValueOrDefault(code);

    public PayrollConceptDefinition Get(string code) =>
        _byCode.TryGetValue(code, out var c)
            ? c
            : throw new CalculationRefusedException(
                $"No hay una versión vigente al {AsOf:yyyy-MM-dd} del concepto {code}.", [code]);
}
