using IngenIA365ERP.Domain.Entities.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Búsqueda de tramo en una tabla por rangos: tabla + base + unidad → tramo, valor y
/// <see cref="ExplanationRange"/>. Es la pieza que <see cref="RangeTableRule"/> hacía
/// dentro de sí y que la feature 010 sacó para que las liquidaciones especiales (tabla
/// 383 sobre la prima sola, tabla del art. 206 num. 4 sobre el promedio de seis meses,
/// tabla del art. 64 con dos valores por tramo) y el procedimiento 2 no escriban la
/// misma búsqueda tres veces (research R1, R5). La unidad y el modo son datos del
/// parámetro (<c>RangeUnitParameterCode</c>, <c>RangeIsMarginal</c>); aquí no hay
/// ningún valor legal.
/// </summary>
public static class RangeTableLookup
{
    /// <summary>
    /// Lo que salió de buscar la base en la tabla. <see cref="Value"/> es el resultado en
    /// pesos según el modo de la tabla (marginal sobre el exceso más el fijo, o tarifa plana
    /// sobre toda la base); cuando la base no cae en ningún tramo vale cero y
    /// <see cref="Range"/> es nulo. Las tablas de dos valores por tramo (indemnización)
    /// leen <c>Range.FixedValue</c> y <c>Range.Rate</c> directamente.
    /// </summary>
    public sealed record Result(
        PayrollLegalParameter Table,
        decimal BaseValue,
        decimal BaseInUnits,
        decimal UnitValue,
        string UnitName,
        ExplanationParameter? UnitParameter,
        PayrollLegalParameterRange? Range,
        decimal Value)
    {
        public bool Found => Range is not null;

        /// <summary>Tarifa del tramo como fracción (19 → 0,19); cero si no hay tramo.</summary>
        public decimal Rate => (Range?.Rate ?? 0m) / 100m;

        public ExplanationRange? ExplanationRange => Range is null
            ? null
            : new ExplanationRange(Range.FromValue, Range.ToValue, Range.Rate, Range.FixedValue, UnitName, BaseInUnits);

        /// <summary>«desde a hasta unidad: tarifa x % más n fijos», como lo lee la contadora.</summary>
        public string RangeText()
        {
            if (Range is null) return $"La base ({Fmt.Num(BaseInUnits)} {UnitName}) no cae en ningún tramo de {Table.Code}: no aplica.";
            var hasta = Range.ToValue is { } t ? Fmt.Num(t) : "en adelante";
            return $"{Fmt.Num(Range.FromValue)} a {hasta} {UnitName}: tarifa {Fmt.Pct(Rate)}" +
                   (Range.FixedValue is { } f && f != 0m ? $" más {Fmt.Num(f)} {UnitName} fijos" : string.Empty);
        }
    }

    public static Result Find(string tableCode, decimal baseValue, ParameterSet parameters) =>
        Find(parameters.Table(tableCode), baseValue, parameters);

    public static Result Find(PayrollLegalParameter table, decimal baseValue, ParameterSet parameters)
    {
        var unidadValor = 1m;
        var unidadNombre = "pesos";
        ExplanationParameter? unidadParametro = null;
        if (!string.IsNullOrWhiteSpace(table.RangeUnitParameterCode))
        {
            unidadValor = parameters.Value(table.RangeUnitParameterCode);
            unidadNombre = table.RangeUnitParameterCode;
            unidadParametro = parameters.Describe(table.RangeUnitParameterCode);
        }

        var enUnidades = baseValue / unidadValor;
        var tramo = table.Ranges
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Order).ThenBy(r => r.FromValue)
            .FirstOrDefault(r => r.Contains(enUnidades));

        decimal valor = 0m;
        if (tramo is not null)
        {
            var tarifa = (tramo.Rate ?? 0m) / 100m;
            valor = table.RangeIsMarginal
                ? ((enUnidades - tramo.FromValue) * tarifa + (tramo.FixedValue ?? 0m)) * unidadValor
                : baseValue * tarifa + (tramo.FixedValue ?? 0m) * unidadValor;
        }

        return new Result(table, baseValue, enUnidades, unidadValor, unidadNombre, unidadParametro, tramo, valor);
    }

    /// <summary>
    /// Escribe en la explicación los pasos de la búsqueda, en el mismo orden y con las mismas
    /// palabras que siempre mostró la retención de la nómina ordinaria: la tabla de origen,
    /// la base en la unidad, el tramo y el cómputo (marginal o plano).
    /// </summary>
    public static void Explain(Result r, Explanation exp)
    {
        // De dónde salen los tramos: la norma, o la tabla propia del plan de nómina (Parámetros de retención).
        if (!string.IsNullOrWhiteSpace(r.Table.Source)) exp.Note("Tabla", r.Table.Source);

        if (r.UnitValue != 1m && r.UnitParameter is { } pu)
            exp.Step($"Base en {r.UnitName} (1 {r.UnitName} = {Fmt.Money(r.UnitValue)}, vigente desde {Fmt.Date(pu.ValidFrom)})", r.BaseInUnits);

        if (r.Range is null)
        {
            exp.Note("Tramo", r.RangeText());
            return;
        }

        exp.Range = r.ExplanationRange;
        exp.Note("Tramo", r.RangeText());

        if (r.Table.RangeIsMarginal)
        {
            var exceso = r.BaseInUnits - r.Range.FromValue;
            exp.Step($"Exceso sobre el inicio del tramo ({r.UnitName})", exceso);
            exp.Step($"Exceso × tarifa + fijo ({r.UnitName})", exceso * r.Rate + (r.Range.FixedValue ?? 0m));
            exp.Step("En pesos", r.Value);
        }
        else
        {
            exp.Step("Base × tarifa del tramo", r.Value);
        }
    }

    /// <summary>
    /// Aproxima al múltiplo que diga el parámetro opcional (<c>RETEFTE_REDONDEO</c>), si existe
    /// y es positivo, y lo deja escrito en la explicación. Devuelve el valor tal cual si no hay
    /// múltiplo o ya está aproximado.
    /// </summary>
    public static decimal RoundToParameterMultiple(decimal value, string multipleCode, ParameterSet parameters, Explanation exp)
    {
        if (!parameters.Has(multipleCode)) return value;
        var multiplo = parameters.Value(multipleCode);
        if (multiplo <= 0m) return value;
        var aproximado = Math.Round(value / multiplo, 0, MidpointRounding.AwayFromZero) * multiplo;
        if (aproximado != value)
            exp.Step($"Aproximado al múltiplo de {Fmt.Money(multiplo)} ({multipleCode})", aproximado);
        return aproximado;
    }
}
