namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// La política de retención sobre lo que reparte la asamblea (respuesta 89 B del dueño, E260), tal como la fija cada cooperativa en <c>Asamblea.Retencion.*</c>:
/// el concepto, la tarifa de la revalorización de aportes y la de los retornos cooperativos (nulas = no se retiene) y la base mínima en UVT (nula o cero = sin
/// base mínima). Sin valores legales en el código: todo llega de parámetros y de la UVT vigente. (nuevo)
/// </summary>
public sealed record PoliticaDeRetencionDeAsamblea(string Concepto, decimal? TarifaRevalorizacion, decimal? TarifaExcedentes, decimal? BaseMinimaUvt)
{
    /// <summary>¿Hay concepto configurado? Sin él, ninguna tarifa rige.</summary>
    public bool Configurada => !string.IsNullOrWhiteSpace(Concepto);

    /// <summary>La tarifa de la clase de decisión (nula = no se retiene).</summary>
    public decimal? TarifaDe(Enums.Lending.AssemblyDistributionKind clase) => !Configurada ? null : clase switch
    {
        Enums.Lending.AssemblyDistributionKind.Revaluation => TarifaRevalorizacion,
        Enums.Lending.AssemblyDistributionKind.SurplusDistribution => TarifaExcedentes,
        _ => null,
    };
}

/// <summary>La retención de un asociado: la base, la tarifa, el valor y, si no se retuvo, por qué (<c>NonTaxpayer</c>, <c>SelfWithholder</c>, <c>BelowThreshold</c>, <c>ZeroRate</c>). (nuevo)</summary>
public sealed record RetencionDeAsambleaCalculada(decimal Base, decimal Tarifa, decimal Valor, decimal? UmbralEnPesos, string? MotivoDeExencion)
{
    public bool SeRetiene => Valor > 0m;
}

/// <summary>
/// El cálculo puro de la retención sobre lo que la asamblea reparte a un asociado (E260): al asociado que no es retenible (no contribuyente, autorretenedor) no
/// se le retiene; bajo la base mínima tampoco; si no, base × tarifa a la unidad de redondeo de la asamblea (mitad hacia arriba), sin pasar de la base. (nuevo)
/// </summary>
public static class RetencionDeAsamblea
{
    public static RetencionDeAsambleaCalculada Calcular(decimal bruto, decimal? tarifa, decimal? umbralEnPesos, PerfilDeRetencion perfil, decimal unidadDeRedondeo)
    {
        if (bruto < 0m) throw new ArgumentOutOfRangeException(nameof(bruto), bruto, "Lo que recibe el asociado no es negativo.");
        var t = tarifa ?? 0m;
        if (t <= 0m || bruto == 0m) return new RetencionDeAsambleaCalculada(bruto, t, 0m, umbralEnPesos, "ZeroRate");
        if (perfil == PerfilDeRetencion.NonTaxpayer) return new RetencionDeAsambleaCalculada(bruto, t, 0m, umbralEnPesos, "NonTaxpayer");
        if (perfil == PerfilDeRetencion.SelfWithholder) return new RetencionDeAsambleaCalculada(bruto, t, 0m, umbralEnPesos, "SelfWithholder");
        if (umbralEnPesos is > 0m && bruto < umbralEnPesos) return new RetencionDeAsambleaCalculada(bruto, t, 0m, umbralEnPesos, "BelowThreshold");
        var valor = Math.Min(bruto, RevalorizacionYExcedentes.Redondear(bruto * t, unidadDeRedondeo));
        return new RetencionDeAsambleaCalculada(bruto, t, valor, umbralEnPesos, null);
    }
}
