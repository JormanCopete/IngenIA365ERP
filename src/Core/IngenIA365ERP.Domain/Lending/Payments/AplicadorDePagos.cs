using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Payments;

/// <summary>
/// El <b>aplicador de pagos</b> (feature 013, US5, T0534; T31, M10; FR-071–FR-076, D5–D8, D24): la <b>misma</b> función reparte un pago
/// cuando se simula y cuando se aplica (FR-072), sobre el estado del crédito a la fecha valor (<see cref="EstadoDelCredito"/>) y con la
/// prelación sellada (<see cref="Prelacion"/>). Lo exigible a la fecha valor son las cuotas con vencimiento hasta ese día (incluida la
/// que vence ese día) con todos sus conceptos; todo pago cubre primero lo exigible (FR-072).
/// <list type="number">
/// <item><see cref="PaymentModality.FullPayoff"/>: todo lo cobrable de todas las cuotas, usando primero el anticipo del mismo crédito y
/// nunca el saldo a favor (D7); lo que sobra es excedente.</item>
/// <item><see cref="PaymentModality.PastDue"/>: sólo lo exigible.</item>
/// <item><see cref="PaymentModality.CycleInstallment"/>: lo exigible y la cuota del período; si la del período todavía no vence, lo que le
/// toca queda como <b>anticipo de esa cuota</b> y no reduce intereses (D8). Su valor es el capital pendiente más el interés y el seguro del
/// plan, menos el anticipo que ya la cubriría.</item>
/// <item><see cref="PaymentModality.Advance"/>: lo exigible y el resto como anticipo general, hasta lo que falta por vencer según el plan
/// (FR-074).</item>
/// <item><see cref="PaymentModality.ExtraordinaryPrincipal"/>: lo exigible y el resto a capital con la elección del deudor (plan
/// regenerado, FR-075); sin elección, saldo a favor (D24); si alcanza el capital por vencer, es una cancelación (D5).</item>
/// <item><see cref="PaymentModality.SpecificInstallment"/>: sólo esa cuota, si las anteriores exigibles están cubiertas o la prelación lo
/// permite (<c>Lending.Payment.SpecificInstallmentBlocked</c> con <c>pendingBefore</c>); si no vence todavía, como anticipo de esa cuota.</item>
/// <item><see cref="PaymentModality.FreeAmount"/>: lo exigible por la prelación y el resto como anticipo general.</item>
/// </list>
/// Con la gracia total repartida (<see cref="GraceInterestTreatment.Distributed"/>) el interés de la gracia que el libro lleva en la cuota
/// 1 se cobra con la cuota a la que el plan se lo carga (<see cref="PlanDePagos.AtribuirInteresDeLaPrimera"/>). Sin IO, sólo
/// <c>decimal</c> y <c>DateOnly</c>, sin valores legales. (nuevo)
/// </summary>
public static class AplicadorDePagos
{
    public const string CodigoCuotaEspecificaBloqueada = "Lending.Payment.SpecificInstallmentBlocked";

    public const string CodigoModalidadNoAdmitida = "Lending.Payment.ModalityNotAllowed";

    /// <summary>Aviso: el anticipo cubre cada cuota en su vencimiento y no reduce intereses (FR-074).</summary>
    public const string AvisoAnticipoNoReduceIntereses = "Lending.Payment.AdvanceDoesNotReduceInterest";

    /// <summary>Aviso: abono extraordinario sin elección ni preferencia, queda como saldo a favor (D24).</summary>
    public const string AvisoPreferenciaAsumida = "Lending.Payment.PreferenceAssumed";

    /// <summary>Aviso: el abono extraordinario alcanzó el capital por vencer y se aplicó como cancelación (D5). (nuevo)</summary>
    public const string AvisoTratadoComoCancelacion = "Lending.Payment.TreatedAsPayoff";

    /// <summary>Lo que hay por cubrir de un concepto: la cuota con que se ordena y la del libro (difieren sólo con la gracia repartida).</summary>
    private readonly record struct Cubeta(int? Orden, int? Libro, LoanConcept Concepto, decimal Monto);

    /// <summary>
    /// Reparte un pago (FR-071, FR-072). Lanza <see cref="ReglaDeCarteraException"/> con <see cref="CodigoCuotaEspecificaBloqueada"/> o
    /// <see cref="CodigoModalidadNoAdmitida"/> si la modalidad no cabe; <see cref="Problema"/> lo dice sin lanzar.
    /// </summary>
    public static DistribucionDePago Distribuir(EstadoDelCredito estado, SolicitudDeDistribucion solicitud, Prelacion prelacion, RedondeoDeCartera redondeo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(prelacion);
        ArgumentNullException.ThrowIfNull(redondeo);
        if (solicitud.Valor <= 0m) throw new ArgumentOutOfRangeException(nameof(solicitud), solicitud.Valor, "El valor del pago es mayor que cero.");
        if (redondeo.RedondearAlCentavo(solicitud.Valor) != solicitud.Valor)
            throw new ArgumentException("El valor del pago va en pesos con centavos.", nameof(solicitud));
        if (estado.Plan is null) throw new ArgumentException("No hay crédito a la fecha valor.", nameof(estado));
        if (!Enum.IsDefined(solicitud.Modalidad)) throw new ArgumentOutOfRangeException(nameof(solicitud), solicitud.Modalidad, "Modalidad desconocida.");
        ReglaDeCarteraException.SiHay(Problema(estado, solicitud, prelacion));

        var v = solicitud.FechaValor;
        var cubetas = Cubetas(estado, prelacion);
        var exigibles = cubetas.Where(c => Exigible(estado.Plan, c.Orden, v)).ToList();
        var salida = new DistribucionDePago { Modalidad = solicitud.Modalidad, Valor = solicitud.Valor, FechaValor = v, Eleccion = solicitud.Eleccion, Cuota = solicitud.Cuota };

        switch (solicitud.Modalidad)
        {
            case PaymentModality.FullPayoff:
                return Cancelacion(estado, cubetas, salida, []);

            case PaymentModality.PastDue:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                return Cerrar(estado, cubetas, salida with { Lineas = lineas, Excedente = resto });
            }

            case PaymentModality.CycleInstallment:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                var delCiclo = estado.Plan.Cuotas.FirstOrDefault(c => c.Vencimiento >= v);
                if (delCiclo is null || delCiclo.Vencimiento == v || resto == 0m)
                    return Cerrar(estado, cubetas, salida with { Lineas = lineas, Excedente = resto });
                var falta = Math.Max(0m, ValorDeCuota(estado, delCiclo) - Anticipo(estado, delCiclo.Numero) - Anticipo(estado, null));
                var anticipo = Math.Min(resto, falta);
                return Cerrar(estado, cubetas, salida with
                {
                    Lineas = lineas, Anticipo = anticipo, CuotaDelAnticipo = anticipo > 0m ? delCiclo.Numero : null, Excedente = resto - anticipo,
                    Avisos = anticipo > 0m ? [AvisoAnticipoNoReduceIntereses] : [],
                });
            }

            case PaymentModality.Advance:
            case PaymentModality.FreeAmount:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                var porVencer = estado.Plan.Cuotas.Where(c => c.Vencimiento > v).Sum(c => ValorDeCuota(estado, c));
                var cabe = Math.Max(0m, porVencer - estado.Anticipo);
                var anticipo = Math.Min(resto, cabe);
                return Cerrar(estado, cubetas, salida with
                {
                    Lineas = lineas, Anticipo = anticipo, Excedente = resto - anticipo,
                    Avisos = anticipo > 0m || solicitud.Modalidad == PaymentModality.Advance ? [AvisoAnticipoNoReduceIntereses] : [],
                });
            }

            case PaymentModality.ExtraordinaryPrincipal:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                if (resto == 0m) return Cerrar(estado, cubetas, salida with { Lineas = lineas });
                var porVencer = estado.Plan.Cuotas.Where(c => c.Vencimiento > v).Sum(c => estado.Saldo(LoanConcept.Principal, c.Numero));
                if (resto >= porVencer)
                {
                    var cancelacion = Cancelacion(estado, cubetas, salida, []);
                    return cancelacion with { Avisos = [.. cancelacion.Avisos, AvisoTratadoComoCancelacion] };
                }
                if (solicitud.Eleccion is not { } eleccion)
                    return Cerrar(estado, cubetas, salida with { Lineas = lineas, SaldoAFavor = resto, Avisos = [AvisoPreferenciaAsumida] });
                var plan = GeneradorDePlan.Regenerar(estado.Plan, new EntradaDeRegeneracion(v) { CambioDeCapital = -resto, Eleccion = eleccion },
                    AjustesDeCapital(estado));
                return Cerrar(estado, cubetas, salida with { Lineas = [.. lineas, new LineaDeDistribucion(null, LoanConcept.Principal, resto)], PlanNuevo = plan });
            }

            case PaymentModality.SpecificInstallment:
            {
                var numero = solicitud.Cuota!.Value;
                var cuota = estado.Plan.Cuotas.First(c => c.Numero == numero);
                var deLaCuota = cubetas.Where(c => c.Orden == numero).ToList();
                if (cuota.Vencimiento <= v)
                {
                    var (lineas, resto) = Repartir(deLaCuota, solicitud.Valor);
                    return Cerrar(estado, cubetas, salida with { Lineas = lineas, Excedente = resto });
                }
                var falta = Math.Max(0m, ValorDeCuota(estado, cuota) - Anticipo(estado, numero));
                var anticipo = Math.Min(solicitud.Valor, falta);
                return Cerrar(estado, cubetas, salida with
                {
                    Anticipo = anticipo, CuotaDelAnticipo = anticipo > 0m ? numero : null, Excedente = solicitud.Valor - anticipo,
                    Avisos = anticipo > 0m ? [AvisoAnticipoNoReduceIntereses] : [],
                });
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(solicitud), solicitud.Modalidad, "Modalidad desconocida.");
        }
    }

    /// <summary>
    /// Lo que impide la modalidad pedida, sin lanzar: <see cref="CodigoModalidadNoAdmitida"/> si la cuota específica no se indica o no existe;
    /// <see cref="CodigoCuotaEspecificaBloqueada"/> con <c>pendingBefore</c> (las cuotas anteriores exigibles e impagas) si la prelación no
    /// permite pagarla fuera de orden.
    /// </summary>
    public static ProblemaDeCartera? Problema(EstadoDelCredito estado, SolicitudDeDistribucion solicitud, Prelacion prelacion)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(prelacion);
        if (solicitud.Modalidad != PaymentModality.SpecificInstallment) return null;
        if (estado.Plan is null || solicitud.Cuota is not { } numero || estado.Plan.Cuotas.All(c => c.Numero != numero))
            return ProblemaDeCartera.Con(CodigoModalidadNoAdmitida, "El pago a una cuota específica indica una cuota del plan.", "installmentNumber", solicitud.Cuota);
        if (prelacion.CuotaEspecificaFueraDeOrden) return null;
        var pendientes = Cubetas(estado, prelacion)
            .Where(c => (c.Orden ?? 0) < numero && Exigible(estado.Plan, c.Orden, solicitud.FechaValor) && c.Monto > 0m)
            .Select(c => c.Orden ?? 0).Distinct().Order().ToArray();
        return pendientes.Length == 0
            ? null
            : new ProblemaDeCartera(CodigoCuotaEspecificaBloqueada,
                $"Antes de la cuota {numero} hay cuotas exigibles sin cubrir ({string.Join(", ", pendientes)}): la línea no permite pagarla fuera de orden.",
                new Dictionary<string, object?> { ["pendingBefore"] = pendientes, ["installmentNumber"] = numero });
    }

    /// <summary>
    /// Las dos alternativas de un abono extraordinario sin elección (FR-075, SC-003): reducir el plazo y reducir la cuota, cada una con su
    /// plan; la simulación las muestra antes de confirmar.
    /// </summary>
    public static IReadOnlyList<DistribucionDePago> Alternativas(EstadoDelCredito estado, SolicitudDeDistribucion solicitud, Prelacion prelacion,
        RedondeoDeCartera redondeo)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.Modalidad != PaymentModality.ExtraordinaryPrincipal) return [];
        return [.. new[] { PrepaymentChoice.ReduceTerm, PrepaymentChoice.ReduceInstallment }
            .Select(e => Distribuir(estado, solicitud with { Eleccion = e }, prelacion, redondeo))];
    }

    /// <summary>
    /// El anticipo aplicado a la cuota <paramref name="numero"/> el día de su vencimiento (FR-074, D6, D8; paso (5) del ciclo diario): cubre
    /// sus conceptos con la prelación, primero con el anticipo de esa cuota y luego con el general, parcial si no alcanza. Lo que sobra del
    /// anticipo de la cuota, ya cubierta, pasa al general. Nula si no hay anticipo o la cuota no debe nada.
    /// </summary>
    public static AplicacionDeAnticipo? AplicarAnticipo(EstadoDelCredito estado, int numero, Prelacion prelacion)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(prelacion);
        var propio = Anticipo(estado, numero);
        var general = Anticipo(estado, null);
        if (propio <= 0m && general <= 0m) return null;
        var deLaCuota = Cubetas(estado, prelacion).Where(c => c.Orden == numero).ToList();
        var debe = deLaCuota.Sum(c => c.Monto);
        var usadoPropio = Math.Min(Math.Max(propio, 0m), debe);
        var usadoGeneral = Math.Min(Math.Max(general, 0m), debe - usadoPropio);
        var (lineas, _) = Repartir(deLaCuota, usadoPropio + usadoGeneral);
        var cubierta = usadoPropio + usadoGeneral == debe;
        var liberado = cubierta ? Math.Max(propio, 0m) - usadoPropio : 0m;
        if (lineas.Count == 0 && liberado == 0m) return null;
        var usado = new List<LineaDeDistribucion>();
        if (usadoPropio > 0m) usado.Add(new LineaDeDistribucion(numero, LoanConcept.Advance, usadoPropio));
        if (usadoGeneral > 0m) usado.Add(new LineaDeDistribucion(null, LoanConcept.Advance, usadoGeneral));
        return new AplicacionDeAnticipo(estado.Fecha, numero, lineas, usado, liberado) { Cubierta = cubierta };
    }

    /// <summary>Por cuota, el capital pendiente menos el del plan: lo que los pagos movieron fuera del plan.</summary>
    public static IReadOnlyDictionary<int, decimal> AjustesDeCapital(EstadoDelCredito estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var salida = new Dictionary<int, decimal>();
        foreach (var c in estado.Plan?.Cuotas ?? [])
        {
            var ajuste = estado.Saldo(LoanConcept.Principal, c.Numero) - c.Capital;
            if (ajuste != 0m) salida[c.Numero] = ajuste;
        }
        return salida;
    }

    /// <summary>Una cancelación (D7): todo lo cobrable, primero con el anticipo del mismo crédito.</summary>
    private static DistribucionDePago Cancelacion(EstadoDelCredito estado, List<Cubeta> cubetas, DistribucionDePago salida, IReadOnlyList<string> avisos)
    {
        var total = cubetas.Sum(c => c.Monto);
        var usado = new List<LineaDeDistribucion>();
        var porUsar = total;
        foreach (var s in estado.Saldos.Where(s => s.Concepto == LoanConcept.Advance && s.Valor > 0m).OrderBy(s => s.Cuota is null ? 0 : 1).ThenBy(s => s.Cuota))
        {
            var t = Math.Min(s.Valor, porUsar);
            if (t <= 0m) break;
            usado.Add(new LineaDeDistribucion(s.Cuota, LoanConcept.Advance, t));
            porUsar -= t;
        }
        var pago = Math.Min(salida.Valor, porUsar);
        var (lineas, _) = Repartir(cubetas, usado.Sum(u => u.Monto) + pago);
        return salida with
        {
            Lineas = lineas, AnticipoUsado = usado, Excedente = salida.Valor - pago, Cancela = pago == porUsar, Avisos = avisos,
        };
    }

    private static DistribucionDePago Cerrar(EstadoDelCredito estado, List<Cubeta> cubetas, DistribucionDePago d) =>
        d with { Cancela = d.PlanNuevo is null && cubetas.Sum(c => c.Monto) == d.Lineas.Sum(l => l.Monto) && cubetas.Count > 0 };

    /// <summary>Reparte <paramref name="fondos"/> sobre las cubetas en orden; las líneas se juntan por (cuota del libro, concepto).</summary>
    private static (IReadOnlyList<LineaDeDistribucion> Lineas, decimal Resto) Repartir(IEnumerable<Cubeta> cubetas, decimal fondos)
    {
        var lineas = new List<LineaDeDistribucion>();
        var resto = fondos;
        foreach (var c in cubetas)
        {
            if (resto <= 0m) break;
            var t = Math.Min(resto, c.Monto);
            if (t <= 0m) continue;
            resto -= t;
            var i = lineas.FindIndex(l => l.Cuota == c.Libro && l.Concepto == c.Concepto);
            if (i >= 0) lineas[i] = lineas[i] with { Monto = lineas[i].Monto + t };
            else lineas.Add(new LineaDeDistribucion(c.Libro, c.Concepto, t));
        }
        return (lineas, resto);
    }

    /// <summary>Lo cobrable con saldo positivo, en el orden de la prelación, con el interés de la gracia repartida en su cuota.</summary>
    private static List<Cubeta> Cubetas(EstadoDelCredito estado, Prelacion prelacion)
    {
        var plan = estado.Plan!;
        var primera = plan.Cuotas[0].Numero;
        var cubetas = new List<Cubeta>();
        foreach (var s in estado.Saldos)
        {
            if (s.Valor <= 0m || !Prelacion.Cobrables.Contains(s.Concepto)) continue;
            if (s.Concepto == LoanConcept.CurrentInterest && s.Cuota == primera && plan.RepartaLaGracia)
            {
                foreach (var (cuota, parte) in plan.AtribuirInteresDeLaPrimera(s.Valor))
                    if (parte > 0m) cubetas.Add(new Cubeta(cuota, s.Cuota, s.Concepto, parte));
                continue;
            }
            cubetas.Add(new Cubeta(s.Cuota, s.Cuota, s.Concepto, s.Valor));
        }
        return prelacion.Ordenar(cubetas, c => c.Orden, c => c.Concepto).ToList();
    }

    /// <summary>Una cuota (o un saldo sin cuota) es exigible si vence hasta la fecha valor.</summary>
    private static bool Exigible(PlanDePagos plan, int? cuota, DateOnly fecha)
    {
        if (cuota is not { } n) return true;
        var c = plan.Cuotas.FirstOrDefault(q => q.Numero == n);
        return c is null || c.Vencimiento <= fecha;
    }

    /// <summary>Lo que vale una cuota al vencer según el plan: su capital pendiente, su interés y su seguro.</summary>
    private static decimal ValorDeCuota(EstadoDelCredito estado, CuotaDelPlan cuota) =>
        estado.Saldo(LoanConcept.Principal, cuota.Numero) + cuota.Interes + cuota.Seguro;

    /// <summary>El anticipo de una cuota (o el general, con <paramref name="cuota"/> nula).</summary>
    private static decimal Anticipo(EstadoDelCredito estado, int? cuota) =>
        estado.Saldos.Where(s => s.Concepto == LoanConcept.Advance && s.Cuota == cuota).Sum(s => s.Valor);
}
