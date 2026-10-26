namespace IngenIA365ERP.Domain.Receipts.Convenios;

/// <summary>Una retención que el convenio, la red o el banco practicaron, con su clase (en la fuente, de IVA, de ICA) y su tercero. (nuevo)</summary>
public sealed record RetencionPracticada(string Clase, decimal Valor, Guid? TerceroPublicId);

/// <summary>
/// Lo que explica la diferencia entre lo informado y lo abonado (feature 015, R2 y T2; FR-093, FR-133, FR-218): comisión, IVA de la comisión y cada retención; lo que
/// sobra sin explicar y si exige aprobación por pasar la tolerancia. (nuevo)
/// </summary>
public sealed record ExplicacionDeDiferencia(
    decimal Informado,
    decimal Abonado,
    decimal Diferencia,
    decimal Comision,
    decimal Iva,
    decimal Retenciones,
    decimal Explicado,
    decimal SinExplicar,
    decimal Tolerancia,
    bool ExigeAprobacion);

/// <summary>
/// La cuenta de una conciliación neta (feature 015, R2, fase 38, y T2, fase 44; FR-093, FR-133, FR-218): lo informado por el convenio, la pasarela o la red contra lo
/// abonado en la cuenta; la diferencia se explica con la comisión, su IVA y las retenciones con su tercero; lo que quede sin explicar por encima de la tolerancia (un
/// parámetro con vigencia, nunca un literal) exige la aprobación de otra persona. Pura. (nuevo)
/// </summary>
public static class ConciliacionNeta
{
    public static ExplicacionDeDiferencia Explicar(decimal informado, decimal abonado, decimal comision, decimal iva, IReadOnlyList<RetencionPracticada> retenciones,
        decimal tolerancia)
    {
        ArgumentNullException.ThrowIfNull(retenciones);
        if (comision < 0m || iva < 0m || retenciones.Any(r => r.Valor < 0m))
            throw new ArgumentException("La comisión, el IVA y las retenciones no pueden ser negativos.");
        var diferencia = informado - abonado;
        var totalRetenciones = retenciones.Sum(r => r.Valor);
        var explicado = comision + iva + totalRetenciones;
        var sinExplicar = diferencia - explicado;
        var tope = Math.Abs(tolerancia);
        return new ExplicacionDeDiferencia(informado, abonado, diferencia, comision, iva, totalRetenciones, explicado, sinExplicar, tope, Math.Abs(sinExplicar) > tope);
    }
}
