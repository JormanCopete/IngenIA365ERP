using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Una cuota pendiente al corte de la parte <c>installments</c>, con la fila de Excel de donde salió. (nuevo)</summary>
public sealed record CuotaMigrada(
    int Fila,
    int Numero,
    DateOnly Vencimiento,
    decimal Capital,
    decimal InteresCorriente = 0m,
    decimal InteresDeMora = 0m,
    decimal Seguro = 0m,
    decimal Otros = 0m)
{
    /// <summary>Lo que la cuota todavía debe al corte.</summary>
    public decimal Pendiente => Capital + InteresCorriente + InteresDeMora + Seguro + Otros;
}

/// <summary>Un saldo por concepto de la parte <c>balances</c>. (nuevo)</summary>
public sealed record SaldoMigrado(LoanConcept Concepto, decimal Valor);

/// <summary>Un concepto de <c>balances</c> que no cuadra con las cuotas: lo que declara y lo que suman las cuotas. (nuevo)</summary>
public sealed record DescuadreDeConcepto(string Concepto, decimal Declarado, decimal Calculado)
{
    public decimal Diferencia => Declarado - Calculado;
}

/// <summary>
/// Los cuadres de la migración (feature 013, US8, T0738; plantillas.md §5.3.2–§5.3.4, §5.3.9, §5.3.13; FR-139), sin IO: los usa
/// <c>ImportMigrationPartCommand</c> dentro de un archivo (fechas crecientes, interés fuera de la cuota en curso, la ecuación de la venta) y
/// <c>ApplyMigrationLoadCommand</c> entre partes (el plan contra el saldo de capital, los conceptos de <c>balances</c> contra las cuotas, la mora
/// declarada y el episodio vigente). Una cuota está <b>vencida e impaga</b> al corte si vence hasta el corte y todavía debe algo; la cuota
/// <b>en curso</b> es la primera que vence después del corte. (nuevo)
/// </summary>
public static class CuadresDeMigracion
{
    /// <summary>El capital del plan pendiente.</summary>
    public static decimal CapitalDelPlan(IEnumerable<CuotaMigrada> cuotas) => cuotas.Sum(c => c.Capital);

    /// <summary>
    /// Los conceptos de <c>balances</c> que no cuadran con las cuotas (plantillas.md §5.3.3): <c>CurrentInterest</c> +
    /// <c>CurrentInterestInSuspense</c> = Σ <c>interesCorriente</c>; <c>DefaultInterest</c> + <c>DefaultInterestInSuspense</c> = Σ
    /// <c>interesDeMora</c>; <c>Insurance</c> = Σ <c>seguro</c>; <c>CollectionFee</c> + <c>OtherCharge</c> = Σ <c>otros</c>. El capital lo cuadra
    /// <see cref="PlanDescuadrado"/> (con su propio código); <c>Advance</c> y <c>CreditBalance</c> sólo vienen en <c>balances</c>.
    /// </summary>
    public static IReadOnlyList<DescuadreDeConcepto> ConceptosDescuadrados(IReadOnlyCollection<CuotaMigrada> cuotas, IReadOnlyCollection<SaldoMigrado> saldos)
    {
        decimal Saldo(params LoanConcept[] conceptos) => saldos.Where(s => conceptos.Contains(s.Concepto)).Sum(s => s.Valor);
        var salida = new List<DescuadreDeConcepto>();
        void Comparar(string concepto, decimal declarado, decimal calculado)
        {
            if (declarado != calculado) salida.Add(new DescuadreDeConcepto(concepto, declarado, calculado));
        }

        Comparar(nameof(LoanConcept.CurrentInterest), Saldo(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense), cuotas.Sum(c => c.InteresCorriente));
        Comparar(nameof(LoanConcept.DefaultInterest), Saldo(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense), cuotas.Sum(c => c.InteresDeMora));
        Comparar(nameof(LoanConcept.Insurance), Saldo(LoanConcept.Insurance), cuotas.Sum(c => c.Seguro));
        Comparar(nameof(LoanConcept.OtherCharge), Saldo(LoanConcept.CollectionFee, LoanConcept.OtherCharge), cuotas.Sum(c => c.Otros));
        return salida;
    }

    /// <summary>El plan no suma el saldo de capital de <c>balances</c> (<c>Lending.Schedule.PrincipalMismatch</c>): nulo si cuadra.</summary>
    public static DescuadreDeConcepto? PlanDescuadrado(IReadOnlyCollection<CuotaMigrada> cuotas, decimal capital)
    {
        var plan = CapitalDelPlan(cuotas);
        return plan == capital ? null : new DescuadreDeConcepto(nameof(LoanConcept.Principal), capital, plan);
    }

    /// <summary>El saldo de una venta (plantillas.md §5.3.9): <c>valorOriginal</c> − <c>cobrosExternos</c> + <c>notasYAjustes</c>.</summary>
    public static decimal SaldoDeVenta(decimal valorOriginal, decimal cobrosExternos, decimal notasYAjustes) => valorOriginal - cobrosExternos + notasYAjustes;

    /// <summary>¿El saldo al corte de la venta cuadra con su ecuación?</summary>
    public static bool VentaCuadra(decimal valorOriginal, decimal cobrosExternos, decimal notasYAjustes, decimal saldoAlCorte) =>
        SaldoDeVenta(valorOriginal, cobrosExternos, notasYAjustes) == saldoAlCorte;

    /// <summary>El vencimiento de la primera cuota vencida e impaga al corte; nulo si el crédito está al día.</summary>
    public static DateOnly? PrimerVencimientoImpago(IEnumerable<CuotaMigrada> cuotas, DateOnly corte) =>
        cuotas.Where(c => c.Vencimiento <= corte && c.Pendiente > 0m).Select(c => (DateOnly?)c.Vencimiento).Min();

    /// <summary>Los días de mora al corte según las cuotas: desde la primera vencida e impaga.</summary>
    public static int MoraSegunCuotas(IEnumerable<CuotaMigrada> cuotas, DateOnly corte) =>
        PrimerVencimientoImpago(cuotas, corte) is { } desde ? corte.DayNumber - desde.DayNumber : 0;

    /// <summary>¿La mora declarada difiere de la que dicen las cuotas más allá de <paramref name="toleranciaEnDias"/>? (aviso, no error)</summary>
    public static bool MoraDistinta(int declarada, IEnumerable<CuotaMigrada> cuotas, DateOnly corte, int toleranciaEnDias = 0) =>
        Math.Abs(declarada - MoraSegunCuotas(cuotas, corte)) > toleranciaEnDias;

    /// <summary>¿El episodio de un aviso previo es el vigente según las cuotas (el vencimiento de la primera vencida e impaga)?</summary>
    public static bool EpisodioVigente(DateOnly episodio, IEnumerable<CuotaMigrada> cuotas, DateOnly corte) =>
        PrimerVencimientoImpago(cuotas, corte) == episodio;

    /// <summary>Las cuotas cuyo vencimiento no crece con el número (la fila de la que rompe el orden).</summary>
    public static IReadOnlyList<CuotaMigrada> FechasNoCrecientes(IEnumerable<CuotaMigrada> cuotas)
    {
        var ordenadas = cuotas.OrderBy(c => c.Numero).ToList();
        var malas = new List<CuotaMigrada>();
        for (var i = 1; i < ordenadas.Count; i++)
            if (ordenadas[i].Vencimiento <= ordenadas[i - 1].Vencimiento) malas.Add(ordenadas[i]);
        return malas;
    }

    /// <summary>Las cuotas con interés corriente que no están vencidas ni son la cuota en curso (la primera que vence después del corte).</summary>
    public static IReadOnlyList<CuotaMigrada> InteresFueraDeLaCuotaEnCurso(IEnumerable<CuotaMigrada> cuotas, DateOnly corte)
    {
        var lista = cuotas.ToList();
        var enCurso = lista.Where(c => c.Vencimiento > corte).Select(c => (DateOnly?)c.Vencimiento).Min();
        return lista.Where(c => c.InteresCorriente > 0m && c.Vencimiento > corte && c.Vencimiento != enCurso).ToList();
    }
}
