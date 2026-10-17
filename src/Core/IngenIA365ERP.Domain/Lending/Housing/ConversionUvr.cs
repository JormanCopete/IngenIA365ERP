using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Housing;

/// <summary>
/// Las conversiones entre pesos y UVR de un crédito en UVR (feature 013, K6b, T1054; FR-024, D18; data-model §0.4): un valor en pesos pasa a UVR
/// dividido por la UVR del día y llevado a la diezmilésima (<c>ValorUvr</c>, 18,4); un valor en UVR pasa a pesos multiplicado por la UVR del día y
/// llevado al centavo. Las dos con el modo de redondeo que selló la línea, sobre el valor absoluto (el signo no cambia el redondeo).
///
/// <list type="bullet">
/// <item><see cref="Repartir"/>: varios valores en UVR que suman uno en pesos se reparten <b>por acumulado</b> —cada uno es
/// <c>U(acumulado hasta él) − U(acumulado hasta el anterior)</c>— y el último distinto de cero cuadra contra el total, así un pago en pesos queda
/// distribuido sin un centavo de más ni de menos.</item>
/// <item><see cref="SaldosEnPesos"/>: el capital de todas las cuotas se reparte por acumulado contra la valoración del capital total (así el capital
/// en pesos del estado es exactamente el del libro con su corrección monetaria); los demás conceptos, cada uno a la UVR del día.</item>
/// </list>
/// Puro. (nuevo)
/// </summary>
public static class ConversionUvr
{
    /// <summary>Pesos a UVR, a la diezmilésima.</summary>
    public static decimal AUvr(decimal pesos, decimal valorUvr, RoundingMode modo)
    {
        if (valorUvr <= 0m) throw new ArgumentOutOfRangeException(nameof(valorUvr), valorUvr, "La UVR es mayor que cero.");
        return Math.Sign(pesos) * RedondeoDeCartera.AUnidad(Math.Abs(pesos) / valorUvr, RedondeoDeCartera.DiezMilesima, modo);
    }

    /// <summary>UVR a pesos, al centavo.</summary>
    public static decimal APesos(decimal enUvr, decimal valorUvr, RoundingMode modo)
    {
        if (valorUvr <= 0m) throw new ArgumentOutOfRangeException(nameof(valorUvr), valorUvr, "La UVR es mayor que cero.");
        return Math.Sign(enUvr) * RedondeoDeCartera.AUnidad(Math.Abs(enUvr) * valorUvr, RedondeoDeCartera.Centavo, modo);
    }

    /// <summary>
    /// Reparte en pesos valores en UVR (no negativos) por acumulado. Con <paramref name="totalEnPesos"/>, el último distinto de cero cuadra contra él;
    /// sin él, el total es la conversión del acumulado.
    /// </summary>
    public static decimal[] Repartir(IReadOnlyList<decimal> enUvr, decimal valorUvr, RoundingMode modo, decimal? totalEnPesos = null)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        var salida = new decimal[enUvr.Count];
        var acumulado = 0m;
        var anterior = 0m;
        for (var i = 0; i < enUvr.Count; i++)
        {
            acumulado += enUvr[i];
            var ahora = APesos(acumulado, valorUvr, modo);
            salida[i] = ahora - anterior;
            anterior = ahora;
        }
        if (totalEnPesos is { } total)
        {
            var ultimo = -1;
            for (var i = enUvr.Count - 1; i >= 0; i--)
                if (enUvr[i] != 0m) { ultimo = i; break; }
            if (ultimo >= 0) salida[ultimo] += total - salida.Sum();
        }
        return salida;
    }

    /// <summary>
    /// Los saldos de un estado en UVR, en pesos a la UVR del día: el capital por acumulado contra el capital total (en el orden de los saldos), los
    /// demás conceptos uno a uno. Sin los que quedan en cero.
    /// </summary>
    public static IReadOnlyList<SaldoDeCuota> SaldosEnPesos(IReadOnlyList<SaldoDeCuota> enUvr, decimal valorUvr, RoundingMode modo)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        var capital = enUvr.Where(s => s.Concepto == LoanConcept.Principal).ToList();
        var capitalEnPesos = Repartir(capital.Select(s => s.Valor).ToList(), valorUvr, modo);
        var porCapital = capital.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => capitalEnPesos[x.i], ReferenceEqualityComparer.Instance);
        return enUvr
            .Select(s => s with { Valor = s.Concepto == LoanConcept.Principal ? porCapital[s] : APesos(s.Valor, valorUvr, modo) })
            .Where(s => s.Valor != 0m)
            .ToList();
    }

    /// <summary>El valor para cancelar en pesos de unos saldos en UVR a la UVR del día (el mismo que mostraría el estado en pesos).</summary>
    public static decimal ValorParaCancelarEnPesos(IReadOnlyList<SaldoDeCuota> enUvr, decimal valorUvr, RoundingMode modo) =>
        EstadoDelCredito.ValorParaCancelarDe(SaldosEnPesos(enUvr, valorUvr, modo));

    /// <summary>Un plan en UVR, en pesos a la UVR del día (cada valor de cada cuota, uno a uno); su entrada, en pesos y con el redondeo de la línea.</summary>
    public static PlanDePagos PlanEnPesos(PlanDePagos enUvr, decimal valorUvr, RedondeoDeCartera dePesos)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        ArgumentNullException.ThrowIfNull(dePesos);
        var modo = dePesos.Modo;
        decimal P(decimal v) => APesos(v, valorUvr, modo);
        var cuotas = enUvr.Cuotas.Select(c => c with
        {
            SaldoInicial = P(c.SaldoInicial),
            Capital = P(c.Capital),
            Interes = P(c.Interes),
            InteresDeGracia = P(c.InteresDeGracia),
            Seguros = c.Seguros.Select(s => s with { Valor = P(s.Valor) }).ToList(),
            SaldoFinal = P(c.SaldoFinal),
            Extraordinaria = P(c.Extraordinaria),
            Tramos = c.Tramos?.Select(t => t with { Base = P(t.Base) }).ToList(),
        }).ToList();
        var entrada = enUvr.Entrada with
        {
            Monto = P(enUvr.Entrada.Monto),
            Redondeo = dePesos,
            Extraordinarias = enUvr.Entrada.Extraordinarias.Select(x => x with { Valor = P(x.Valor) }).ToList(),
        };
        return enUvr with
        {
            Entrada = entrada,
            CuotaNivelada = enUvr.CuotaNivelada is { } n ? P(n) : null,
            InteresDeGracia = P(enUvr.InteresDeGracia),
            ResiduoDeLaUltimaCuota = P(enUvr.ResiduoDeLaUltimaCuota),
            Cuotas = cuotas,
        };
    }

    /// <summary>
    /// K6b (T1055): el plan en UVR de un crédito que se desembolsa, en pesos a la UVR del desembolso, con la entrada en pesos que lo originó: el capital
    /// de las cuotas por acumulado contra el monto desembolsado (Σ capital = monto al centavo, SC-017), lo demás uno a uno. Es el plan que se guarda en
    /// pesos (<c>LND_RepaymentInstallments</c>, con su copia en UVR) y el que muestra la simulación con la leyenda de que la UVR cambia cada día.
    /// </summary>
    public static PlanDePagos PlanAlDesembolso(PlanDePagos enUvr, decimal valorUvr, EntradaDePlan entradaEnPesos)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        ArgumentNullException.ThrowIfNull(entradaEnPesos);
        var enPesos = PlanEnPesos(enUvr, valorUvr, entradaEnPesos.Redondeo);
        var capital = Repartir(enUvr.Cuotas.Select(c => c.Capital).ToList(), valorUvr, entradaEnPesos.Redondeo.Modo, entradaEnPesos.Monto);
        var saldo = entradaEnPesos.Monto;
        var cuotas = new List<CuotaDelPlan>(enPesos.Cuotas.Count);
        for (var i = 0; i < enPesos.Cuotas.Count; i++)
        {
            var c = enPesos.Cuotas[i];
            cuotas.Add(c with { SaldoInicial = saldo, Capital = capital[i], SaldoFinal = saldo - capital[i] });
            saldo -= capital[i];
        }
        return enPesos with { Entrada = entradaEnPesos, Cuotas = cuotas };
    }

    /// <summary>
    /// Una distribución en UVR, en pesos a la UVR de su fecha valor: lo que usó del anticipo uno a uno; lo cubierto, el anticipo, el saldo a favor y el
    /// excedente por acumulado contra el valor recibido en pesos (<paramref name="valorEnPesos"/>) más lo usado del anticipo, así se cumple
    /// <c>Valor = Σ Lineas + Anticipo + SaldoAFavor + Excedente − Σ AnticipoUsado</c> al centavo.
    /// </summary>
    public static DistribucionDePago DistribucionEnPesos(DistribucionDePago enUvr, decimal valorEnPesos, decimal valorUvr, RedondeoDeCartera dePesos)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        var modo = dePesos.Modo;
        var usado = enUvr.AnticipoUsado.Select(u => u with { Monto = APesos(u.Monto, valorUvr, modo) }).ToList();
        var componentes = enUvr.Lineas.Select(l => l.Monto).Append(enUvr.Anticipo).Append(enUvr.SaldoAFavor).Append(enUvr.Excedente).ToList();
        var repartidos = Repartir(componentes, valorUvr, modo, valorEnPesos + usado.Sum(u => u.Monto));
        var n = enUvr.Lineas.Count;
        return enUvr with
        {
            Valor = valorEnPesos,
            Lineas = enUvr.Lineas.Select((l, i) => l with { Monto = repartidos[i] }).ToList(),
            AnticipoUsado = usado,
            Anticipo = repartidos[n],
            SaldoAFavor = repartidos[n + 1],
            Excedente = repartidos[n + 2],
            PlanNuevo = enUvr.PlanNuevo is { } plan ? PlanEnPesos(plan, valorUvr, dePesos) : null,
        };
    }

    /// <summary>Un anticipo aplicado en UVR, en pesos a la UVR de su fecha (cada valor uno a uno).</summary>
    public static AplicacionDeAnticipo AnticipoEnPesos(AplicacionDeAnticipo enUvr, decimal valorUvr, RoundingMode modo)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        return enUvr with
        {
            Lineas = enUvr.Lineas.Select(l => l with { Monto = APesos(l.Monto, valorUvr, modo) }).ToList(),
            Usado = enUvr.Usado.Select(u => u with { Monto = APesos(u.Monto, valorUvr, modo) }).ToList(),
            Liberado = APesos(enUvr.Liberado, valorUvr, modo),
        };
    }

    /// <summary>
    /// K6b (T1054): un pago en pesos sobre un crédito en UVR, como lo distribuyen la simulación, la aplicación y la reconstrucción —la misma función,
    /// así simular = aplicar (FR-072)—: el valor pasa a UVR a la UVR publicada de la fecha valor (<see cref="ValorDelPagoEnUvr"/>), se distribuye sobre
    /// el estado en UVR y vuelve a pesos con <see cref="DistribucionEnPesos"/>.
    /// </summary>
    public static DistribucionDePago Distribuir(ValoracionEnUvr valoracion, SolicitudDeDistribucion solicitud, Prelacion prelacion)
    {
        ArgumentNullException.ThrowIfNull(valoracion);
        ArgumentNullException.ThrowIfNull(solicitud);
        var u = valoracion.Serie.Requerida(solicitud.FechaValor);
        var enUvr = solicitud with { Valor = ValorDelPagoEnUvr(valoracion.Estado, solicitud.Valor, u, valoracion.RedondeoDePesos.Modo) };
        var d = AplicadorDePagos.Distribuir(valoracion.Estado, enUvr, prelacion, RedondeoDeCartera.EnUvr(valoracion.RedondeoDePesos));
        return DistribucionEnPesos(d, solicitud.Valor, u, valoracion.RedondeoDePesos);
    }

    /// <summary>Las dos alternativas de un abono extraordinario sobre un crédito en UVR (como <see cref="AplicadorDePagos.Alternativas"/>), en pesos.</summary>
    public static IReadOnlyList<DistribucionDePago> Alternativas(ValoracionEnUvr valoracion, SolicitudDeDistribucion solicitud, Prelacion prelacion)
    {
        ArgumentNullException.ThrowIfNull(valoracion);
        ArgumentNullException.ThrowIfNull(solicitud);
        var u = valoracion.Serie.Requerida(solicitud.FechaValor);
        var enUvr = solicitud with { Valor = ValorDelPagoEnUvr(valoracion.Estado, solicitud.Valor, u, valoracion.RedondeoDePesos.Modo) };
        return AplicadorDePagos.Alternativas(valoracion.Estado, enUvr, prelacion, RedondeoDeCartera.EnUvr(valoracion.RedondeoDePesos))
            .Select(d => DistribucionEnPesos(d, solicitud.Valor, u, valoracion.RedondeoDePesos)).ToList();
    }

    /// <summary>
    /// El valor en UVR de un pago en pesos sobre el estado en UVR a su fecha valor: la conversión a la diezmilésima; pero si el pago alcanza el
    /// valor para cancelar en pesos, cancela exactamente lo que se debe en UVR y lo que sobra se convierte aparte (así pagar lo que la pantalla
    /// muestra no deja una diezmilésima pendiente).
    /// </summary>
    public static decimal ValorDelPagoEnUvr(EstadoDelCredito enUvr, decimal pesos, decimal valorUvr, RoundingMode modo)
    {
        ArgumentNullException.ThrowIfNull(enUvr);
        var paraCancelarEnUvr = enUvr.ValorParaCancelar;
        if (paraCancelarEnUvr > 0m)
        {
            var paraCancelarEnPesos = ValorParaCancelarEnPesos(enUvr.Saldos, valorUvr, modo);
            if (pesos >= paraCancelarEnPesos) return paraCancelarEnUvr + AUvr(pesos - paraCancelarEnPesos, valorUvr, modo);
        }
        return AUvr(pesos, valorUvr, modo);
    }
}
