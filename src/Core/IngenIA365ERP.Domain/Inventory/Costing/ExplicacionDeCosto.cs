using System.Globalization;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// Por qué un movimiento quedó con el costo que quedó (feature 012, T19, T281; FR-042, FR-044): un resumen y los pasos
/// —estado anterior, costo aplicado, regla, líneas agregadas— para que la contadora lo lea en el kardex y en el
/// impacto de un retroactivo. Molde de <c>ExplicacionTributaria</c>.
/// </summary>
public sealed class ExplicacionDeCosto
{
    public string Resumen { get; set; } = string.Empty;

    public List<PasoDeCosto> Pasos { get; init; } = [];

    public ExplicacionDeCosto Paso(string etiqueta, decimal valor)
    {
        Pasos.Add(new PasoDeCosto(etiqueta, valor, null));
        return this;
    }

    public ExplicacionDeCosto Nota(string etiqueta, string texto)
    {
        Pasos.Add(new PasoDeCosto(etiqueta, null, texto));
        return this;
    }

    /// <summary>Agrega los pasos de otra explicación (la del costo de entrada, la de cada movimiento recalculado).</summary>
    public ExplicacionDeCosto Agregar(ExplicacionDeCosto otra)
    {
        ArgumentNullException.ThrowIfNull(otra);
        if (!string.IsNullOrWhiteSpace(otra.Resumen)) Pasos.Add(new PasoDeCosto("Detalle", null, otra.Resumen));
        Pasos.AddRange(otra.Pasos);
        return this;
    }

    /// <summary>Todo en una línea, para leerlo y para las pruebas.</summary>
    public string Texto() =>
        Resumen + " | " + string.Join(" | ", Pasos.Select(p =>
            p.Valor is { } v ? $"{p.Etiqueta}: {v.ToString("0.######", CultureInfo.InvariantCulture)}" : $"{p.Etiqueta}: {p.Texto}"));
}

/// <summary>Un paso de la explicación del costo: etiqueta y valor o texto. (nuevo)</summary>
public sealed record PasoDeCosto(string Etiqueta, decimal? Valor, string? Texto);
