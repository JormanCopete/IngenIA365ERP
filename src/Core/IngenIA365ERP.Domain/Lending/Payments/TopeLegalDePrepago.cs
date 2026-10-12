namespace IngenIA365ERP.Domain.Lending.Payments;

/// <summary>El tope del prepago sin penalidad para un crédito: si su monto original cabe, el tope en pesos y cuántas veces el SMMLV rigió.</summary>
public sealed record ResultadoDeTopeDePrepago(bool DentroDelTope, decimal TopeEnPesos, decimal VecesDelSalarioMinimo);

/// <summary>
/// El <b>tope legal del abono sin penalidad</b> (feature 013, US5, T0513 caso 32; FR-075, Ley 1555 de 2012): un crédito cuyo monto
/// original no supera <c>PREPAGO_TOPE_SMMLV</c> veces el salario mínimo vigente al desembolsar se prepaga sin penalidad, comisión ni cargo
/// aunque la línea sellada pacte uno; por encima rige la regla de prepago sellada (sin regla, tampoco hay cargo). El límite tiene dirección
/// <c>Mayor</c>: si la cooperativa fija más veces que la ley, rige el suyo, que amplía el beneficio. Ni el salario mínimo ni las veces se
/// escriben aquí: llegan del lector de valores nacionales y del parámetro (<c>IValoresNacionales</c>, <c>ReglasDeParametrosDeCartera</c>).
/// Pura. (nuevo)
/// </summary>
public static class TopeLegalDePrepago
{
    /// <param name="montoOriginal">El monto original del crédito (todos los tramos pactados).</param>
    /// <param name="salarioMinimoAlDesembolso">El SMMLV vigente a la fecha del desembolso.</param>
    /// <param name="vecesNacional">Las veces del SMMLV que fija la ley.</param>
    /// <param name="vecesDeLaCooperativa">Las de la cooperativa, si fijó un valor propio; rige el mayor.</param>
    public static ResultadoDeTopeDePrepago Evaluar(decimal montoOriginal, decimal salarioMinimoAlDesembolso, decimal vecesNacional, decimal? vecesDeLaCooperativa = null)
    {
        if (montoOriginal <= 0m) throw new ArgumentOutOfRangeException(nameof(montoOriginal), montoOriginal, "El monto original es mayor que cero.");
        if (salarioMinimoAlDesembolso <= 0m)
            throw new ArgumentOutOfRangeException(nameof(salarioMinimoAlDesembolso), salarioMinimoAlDesembolso, "El salario mínimo es mayor que cero.");
        if (vecesNacional <= 0m) throw new ArgumentOutOfRangeException(nameof(vecesNacional), vecesNacional, "Las veces del tope son mayores que cero.");
        var veces = vecesDeLaCooperativa is { } propias && propias > vecesNacional ? propias : vecesNacional;
        var tope = veces * salarioMinimoAlDesembolso;
        return new ResultadoDeTopeDePrepago(montoOriginal <= tope, tope, veces);
    }
}
