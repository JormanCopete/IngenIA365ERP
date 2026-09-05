namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// La explicación de una línea, en los términos de la forma que la calculó (FR-009,
/// FR-013): «cantidad × unidad», «base × porcentaje», «tramo de tabla», «suma de
/// conceptos», «salario por tramos». Se serializa tal cual a
/// <c>PayrollRunLine.ExplanationJson</c> y la pantalla la pinta paso a paso.
/// </summary>
public sealed class Explanation
{
    /// <summary>Nombre de la forma de cálculo, legible.</summary>
    public required string Form { get; init; }

    /// <summary>Una frase que resume el cálculo («21 días × $66.666,67 = $1.400.000»).</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Base usada, si la forma tiene base.</summary>
    public ExplanationBase? Base { get; set; }

    /// <summary>Factor multiplicador (porcentaje ya dividido, recargo, peso).</summary>
    public decimal? Factor { get; set; }

    /// <summary>Parámetro legal con su vigencia, si intervino uno.</summary>
    public ExplanationParameter? Parameter { get; set; }

    /// <summary>Tramo de tabla aplicado, si la forma es tabla.</summary>
    public ExplanationRange? Range { get; set; }

    /// <summary>Novedad de origen, si la hay.</summary>
    public ExplanationNovelty? Novelty { get; set; }

    /// <summary>Pasos en orden: cada uno con etiqueta y valor o texto.</summary>
    public List<ExplanationStep> Steps { get; init; } = [];

    public Explanation Step(string label, decimal value)
    {
        Steps.Add(new ExplanationStep(label, value, null));
        return this;
    }

    public Explanation Note(string label, string text)
    {
        Steps.Add(new ExplanationStep(label, null, text));
        return this;
    }
}

public sealed record ExplanationStep(string Label, decimal? Value, string? Text);

public sealed record ExplanationBase(string Kind, decimal Value);

public sealed record ExplanationParameter(string Code, DateTime ValidFrom, decimal? Value);

/// <summary>Tramo: desde/hasta en la unidad de la tabla (UVT, SMMLV o pesos), tarifa y fijo.</summary>
public sealed record ExplanationRange(decimal From, decimal? To, decimal? Rate, decimal? Fixed, string Unit, decimal BaseInUnits);

public sealed record ExplanationNovelty(Guid PublicId, string? Description);
