using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;
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
/// nunca el saldo a favor (D7); lo que sobra es excedente. 2026-11-16: con <see cref="SolicitudDeDistribucion.CuotaCompleta"/>, también el interés de
/// la cuota en curso que falta por causar (<see cref="DistribucionDePago.InteresPorCausar"/>, aviso <see cref="AvisoInteresDeLaCuotaCompleta"/>).</item>
/// <item><see cref="PaymentModality.PastDue"/>: sólo lo exigible.</item>
/// <item><see cref="PaymentModality.CycleInstallment"/>: lo exigible y la cuota del período; si la del período todavía no vence, lo que le
/// toca queda como <b>anticipo de esa cuota</b> y no reduce intereses (D8). Su valor es el capital pendiente más el interés y el seguro del
/// plan y los otros cargos que el libro ya lleve en ella (2026-11-16), menos el anticipo que ya la cubriría.</item>
/// <item><see cref="PaymentModality.Advance"/>: lo exigible y el resto como anticipo general, hasta lo que falta por vencer según el plan
/// (FR-074).</item>
/// <item><see cref="PaymentModality.ExtraordinaryPrincipal"/>: lo exigible y el resto a capital con la elección del deudor (plan
/// regenerado, FR-075); sin elección, saldo a favor (D24); si alcanza el capital por vencer, es una cancelación (D5).</item>
/// <item><see cref="PaymentModality.SpecificInstallment"/>: sólo esa cuota, si las anteriores exigibles están cubiertas o la prelación lo
/// permite (<c>Lending.Payment.SpecificInstallmentBlocked</c> con <c>pendingBefore</c>); si no vence todavía, como anticipo de esa cuota.</item>
/// <item><see cref="PaymentModality.FreeAmount"/>: lo exigible por la prelación y el resto como anticipo general.</item>
/// <item>2026-11-27 (feature 028, R4): <see cref="PaymentModality.PayoffExceptCurrentInstallment"/>: la cancelación de todo menos la cuota causada
/// (<see cref="CuotaCausada"/>), que queda tal como se envió y congelada; las cuotas siguientes se pagan a capital y salen del plan
/// (<see cref="DistribucionDePago.CuotaDejada"/>). Sin cuota causada, o en vivienda, es la cancelación total con su aviso. 2026-11-27 (D13 ajustada): dentro
/// del tope legal del prepago la cuota se deja con el interés proporcional (<see cref="SolicitudDeDistribucion.CuotaDejadaProporcional"/>).</item>
/// </list>
/// Con la gracia total repartida (<see cref="GraceInterestTreatment.Distributed"/>) el interés de la gracia que el libro lleva en la cuota
/// 1 se cobra con la cuota a la que el plan se lo carga (<see cref="PlanDePagos.AtribuirInteresDeLaPrimera"/>). 2026-11-16 (pedido del dueño): con el
/// orden de rubros de la empresa en la prelación (<see cref="Prelacion.ConRubros"/>) el capital de una cuota con prima va en el puesto de las cuotas
/// extras. Sin IO, sólo <c>decimal</c> y <c>DateOnly</c>, sin valores legales. (nuevo)
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

    /// <summary>
    /// 2026-11-16 (pedido del dueño): la cancelación cobró el interés completo de la cuota en curso, no sólo el causado hasta la fecha valor
    /// (<see cref="SolicitudDeDistribucion.CuotaCompleta"/>). Es lo que se le avisa al deudor en el comprobante. (nuevo)
    /// </summary>
    public const string AvisoInteresDeLaCuotaCompleta = "Lending.Payment.FullInstallmentInterestCharged";

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059): la cancelación salvo la cuota causada dejó esa cuota en el crédito tal como se envió (D13-A) y canceló lo demás.
    /// (nuevo)
    /// </summary>
    public const string AvisoCuotaCausadaDejada = "Lending.Payment.CurrentInstallmentLeft";

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059): a la fecha valor no hay cuota del período en curso que dejar —la fecha es un vencimiento, el crédito está en gracia o,
    /// por período calendario, el tramo de la próxima cuota no ha empezado— o la que pidió el pago ya no es esa: la cancelación salvo cuota se aplicó como
    /// cancelación total. (nuevo)
    /// </summary>
    public const string AvisoSinCuotaQueDejar = "Lending.Payment.NoInstallmentToLeave";

    /// <summary>
    /// 2026-11-27 (feature 028, T062): un crédito de vivienda (en UVR o en pesos) se recoge completo: dejar la cuota con el interés del período sobre todo el
    /// saldo cobraría interés del capital que se prepaga, y la Ley 546 de 1999 no admite penalidad por prepago. La cancelación salvo cuota se aplicó como
    /// cancelación total. (nuevo)
    /// </summary>
    public const string AvisoViviendaSeCancelaCompleto = "Lending.Payment.HousingPaidOffInFull";

    /// <summary>
    /// 2026-11-27 (feature 028, R4, D13-A): la regla con que la cancelación salvo cuota deja la cuota causada —tal como se envió, con el interés del período
    /// completo sobre el saldo con que se proyectó—; la sella la decisión del pago como constancia. (nuevo)
    /// </summary>
    public const string ReglaDeLaCuotaDejada = "TalComoSeEnvio";

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño; Ley 1555 de 2012): la regla con que la cancelación salvo cuota deja la cuota causada dentro del tope
    /// legal del prepago (o sin los valores nacionales para saberlo): con el <b>interés proporcional</b> —lo causado hasta la fecha valor sobre todo el saldo y,
    /// de ahí a su vencimiento, sólo sobre su propio capital—. La decide quien pide (<see cref="SolicitudDeDistribucion.CuotaDejadaProporcional"/>), la sella la
    /// decisión del pago y la reconstrucción la repite. (nuevo)
    /// </summary>
    public const string ReglaDeLaCuotaDejadaProporcional = "Proporcional";

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada): la cuota se dejó con el interés proporcional (dentro del tope legal del prepago): el interés de los días que le
    /// faltan sobre el capital que se canceló no se cobra. (nuevo)
    /// </summary>
    public const string AvisoCuotaDejadaProporcional = "Lending.Payment.LeftInstallmentProportional";

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
        // 2026-11-24 (feature 025, fase 5, D3): por período calendario, lo causado de la cuota del tramo en curso (todo menos su capital) cuenta como exigible
        // para todo pago que no cancela: el abono lo cubre primero y después lo que diga su modalidad.
        var delTramo = solicitud.PeriodoCalendario ? CuotasDelTramoEnCurso(estado.Plan, v) : new HashSet<int>();
        var exigibles = cubetas.Where(c => Exigible(estado.Plan, c.Orden, v)
                                           || (c.Concepto != LoanConcept.Principal && c.Orden is { } n && delTramo.Contains(n))).ToList();
        var salida = new DistribucionDePago { Modalidad = solicitud.Modalidad, Valor = solicitud.Valor, FechaValor = v, Eleccion = solicitud.Eleccion, Cuota = solicitud.Cuota };

        switch (solicitud.Modalidad)
        {
            case PaymentModality.FullPayoff:
                return Cancelacion(estado, cubetas, salida, [], solicitud.CuotaCompleta, solicitud.PeriodoCalendario);

            case PaymentModality.PayoffExceptCurrentInstallment:
                return CancelacionSalvoCuota(estado, cubetas, salida, solicitud);

            case PaymentModality.PastDue:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                return Cerrar(estado, cubetas, salida with { Lineas = lineas, Excedente = resto });
            }

            case PaymentModality.CycleInstallment:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                // 2026-11-24 (feature 025, fase 5): por período calendario, la cuota del período es la que vence en el tramo en curso; si ya venció (está en lo
                // exigible o pagada) no hay otra: pagar la siguiente es un anticipo (o la cuota específica).
                var delCiclo = solicitud.PeriodoCalendario
                    ? estado.Plan.Cuotas.FirstOrDefault(c => delTramo.Contains(c.Numero))
                    : estado.Plan.Cuotas.FirstOrDefault(c => c.Vencimiento >= v);
                if (delCiclo is null || delCiclo.Vencimiento == v || resto == 0m)
                    return Cerrar(estado, cubetas, salida with { Lineas = lineas, Excedente = resto });
                // Lo que el pago ya cubrió de la cuota del tramo como exigible (su causado, por período calendario) no se vuelve a anticipar.
                var falta = Math.Max(0m, ValorDeCuota(estado, delCiclo) - Anticipo(estado, delCiclo.Numero) - Anticipo(estado, null)
                                         - lineas.Where(l => l.Cuota == delCiclo.Numero).Sum(l => l.Monto));
                var anticipo = Math.Min(resto, falta);
                return Cerrar(estado, cubetas, salida with
                {
                    Lineas = lineas, Anticipo = anticipo, CuotaDelAnticipo = anticipo > 0m ? delCiclo.Numero : null, Excedente = resto - anticipo,
                    // 2026-11-16 (feature 024, fase 4): la cuota reconocida al inicio del ciclo se paga completa: no lleva el aviso del anticipo.
                    Avisos = anticipo > 0m && !Reconocida(estado, delCiclo.Numero) ? [AvisoAnticipoNoReduceIntereses] : [],
                });
            }

            case PaymentModality.Advance:
            case PaymentModality.FreeAmount:
            {
                var (lineas, resto) = Repartir(exigibles, solicitud.Valor);
                var porVencer = estado.Plan.Cuotas.Where(c => c.Vencimiento > v).Sum(c => ValorDeCuota(estado, c));
                // 2026-11-24 (feature 025, fase 5): menos lo que el pago ya cubrió como exigible de las cuotas por vencer (por período calendario).
                var cabe = Math.Max(0m, porVencer - estado.Anticipo - lineas.Where(l => l.Cuota is { } n && delTramo.Contains(n)).Sum(l => l.Monto));
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
                    var cancelacion = Cancelacion(estado, cubetas, salida, [], solicitud.CuotaCompleta, solicitud.PeriodoCalendario);
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
                    Avisos = anticipo > 0m && !Reconocida(estado, numero) ? [AvisoAnticipoNoReduceIntereses] : [],
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

    /// <summary>
    /// 2026-11-16 (pedido del dueño): lo que cuesta cancelar a la fecha del estado: <see cref="EstadoDelCredito.ValorParaCancelar"/> y, con
    /// <paramref name="cuotaCompleta"/>, el interés de la cuota en curso que falta por causar (<see cref="EstadoDelCredito.InteresPorCausarDeLaCuotaEnCurso"/>),
    /// nunca negativo. Es lo que la cancelación cobra: lo usan el listado y el detalle de la caja, la liquidación, la recogida y el cruce del retiro.
    /// <see cref="EstadoDelCredito.ValorParaCancelar"/> no cambia (entra en la huella y en las posiciones). (nuevo)
    /// </summary>
    /// <remarks>
    /// 2026-11-24 (feature 025, fase 5, D3): con <paramref name="periodoCalendario"/>, sin el interés causado de las cuotas cuyo tramo no ha empezado
    /// (<see cref="InteresSinTramo"/>) y con la cuota completa sólo de un tramo ya empezado: lo mismo que cobra la cancelación.
    /// </remarks>
    public static decimal ValorParaCancelar(EstadoDelCredito estado, bool cuotaCompleta, bool periodoCalendario = false)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var extra = cuotaCompleta ? InteresDeLaCuotaCompleta(estado, periodoCalendario).Sum(l => l.Monto) : 0m;
        var noCobrado = periodoCalendario && !estado.EnUvr ? InteresSinTramo(estado, estado.Fecha).Sum(l => l.Monto) : 0m;
        return Math.Max(0m, estado.ValorParaCancelar + extra - noCobrado);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, R3): lo que una cancelación con la cuota completa agrega a lo causado —el interés de la cuota en curso que falta por causar
    /// (<see cref="EstadoDelCredito.InteresPorCausarDeLaCuotaEnCurso"/>)—, por cuota y concepto; por período calendario, sólo el de un tramo ya empezado.
    /// Vacío en UVR. Es la misma regla con que cobran <see cref="ValorParaCancelar"/> y la cancelación: la liquidación de la recogida la usa para desglosar
    /// «el interés que se cobra por la cuota completa». (nuevo)
    /// </summary>
    public static IReadOnlyList<LineaDeDistribucion> InteresDeLaCuotaCompleta(EstadoDelCredito estado, bool periodoCalendario = false)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return estado.EnUvr ? [] : DeTramoEmpezado(estado, estado.InteresPorCausarDeLaCuotaEnCurso(), periodoCalendario);
    }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, HU5; FR-016): las cuotas del <b>tramo en curso</b> a <paramref name="fecha"/> por período calendario: las que vencen
    /// después de esa fecha dentro del tramo del calendario que la contiene (<see cref="PeriodoDeCausacion.TramoQueContiene"/>, con la periodicidad del plan).
    /// Es la «cuota del período» que la caja ofrece y cuyo causado un abono cubre primero. Vacía sin plan. (nuevo)
    /// </summary>
    public static IReadOnlySet<int> CuotasDelTramoEnCurso(PlanDePagos? plan, DateOnly fecha) =>
        plan is null
            ? new HashSet<int>()
            : plan.Cuotas.Where(c => c.Vencimiento > fecha && PeriodoDeCausacion.TramoEmpezado(c.Vencimiento, plan.Entrada.Periodicidad, fecha))
                .Select(c => c.Numero).ToHashSet();

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, D3): el interés corriente causado de las cuotas cuyo tramo del calendario no ha empezado a <paramref name="fecha"/>
    /// (vencen después y su tramo empieza después): lo que una cancelación por período calendario no cobra, por cuota del libro. (nuevo)
    /// </summary>
    public static IReadOnlyList<LineaDeDistribucion> InteresSinTramo(EstadoDelCredito estado, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (estado.Plan is not { } plan) return [];
        var sinEmpezar = plan.Cuotas.Where(c => c.Vencimiento > fecha && !PeriodoDeCausacion.TramoEmpezado(c.Vencimiento, plan.Entrada.Periodicidad, fecha))
            .Select(c => c.Numero).ToHashSet();
        return estado.Saldos.Where(s => s.Concepto == LoanConcept.CurrentInterest && s.Valor > 0m && s.Cuota is { } n && sinEmpezar.Contains(n))
            .OrderBy(s => s.Cuota)
            .Select(s => new LineaDeDistribucion(s.Cuota, s.Concepto, s.Valor))
            .ToList();
    }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5): de lo que la cuota completa cobraría, sólo lo de una cuota cuyo tramo ya empezó (por período calendario): la cuota
    /// completa es la del tramo causado; la de un tramo sin empezar nunca se cobra. Sin período calendario, todo. (nuevo)
    /// </summary>
    private static IReadOnlyList<LineaDeDistribucion> DeTramoEmpezado(EstadoDelCredito estado, IReadOnlyList<LineaDeDistribucion> lineas, bool periodoCalendario)
    {
        if (!periodoCalendario || estado.Plan is not { } plan || lineas.Count == 0) return lineas;
        return lineas.Where(l => l.Cuota is { } n && plan.Cuotas.FirstOrDefault(c => c.Numero == n) is { } q
                                 && PeriodoDeCausacion.TramoEmpezado(q.Vencimiento, plan.Entrada.Periodicidad, estado.Fecha))
            .ToList();
    }

    /// <summary>
    /// Una cancelación (D7): todo lo cobrable, primero con el anticipo del mismo crédito. 2026-11-16 (pedido del dueño): con
    /// <paramref name="cuotaCompleta"/>, también el interés de la cuota en curso que la causación todavía no causó
    /// (<see cref="EstadoDelCredito.InteresPorCausarDeLaCuotaEnCurso"/>), en una cubeta <b>al final</b>, después de todo lo demás y fuera del orden de la
    /// prelación: sólo se cobra si los fondos cubren todo lo causado (con el capital ya en cero, la causación no lo causaría nunca). Cobrado, va entero
    /// en <see cref="DistribucionDePago.InteresPorCausar"/> aunque los fondos no lo alcancen: lo que falte de él queda debiéndose en esa cuota. Si los
    /// fondos no cubren lo causado, la cancelación es la de siempre (parcial) y no se cobra.
    /// </summary>
    /// <remarks>
    /// 2026-11-24 (feature 025, fase 5, D3, FR-017): con <paramref name="periodoCalendario"/>, el interés causado de las cuotas cuyo tramo no ha empezado no
    /// se cobra: sale de lo que se cubre y la cancelación lo devuelve (<see cref="DistribucionDePago.InteresNoCobrado"/>); la cuota completa, sólo la del
    /// tramo ya empezado. Si así los fondos no cancelan, es la cancelación de siempre (parcial): lo de un tramo sin empezar sigue debiéndose.
    /// </remarks>
    /// <remarks>
    /// 2026-11-27 (feature 028, R4): con <paramref name="dejada"/> es la cancelación salvo esa cuota (<see cref="CancelacionSalvoCuota"/>): las cubetas ya vienen
    /// sin las suyas, la cuota completa no cobra la suya y el anticipo se usa en otro orden (<see cref="Anticipos"/>).
    /// </remarks>
    private static DistribucionDePago Cancelacion(EstadoDelCredito estado, List<Cubeta> cubetas, DistribucionDePago salida, IReadOnlyList<string> avisos,
        bool cuotaCompleta, bool periodoCalendario, int? dejada = null)
    {
        if (periodoCalendario && !estado.EnUvr && InteresSinTramo(estado, salida.FechaValor) is { Count: > 0 } noCobrado)
        {
            var sinEmpezar = noCobrado.Select(l => l.Cuota).ToHashSet();
            var cobrables = cubetas.Where(c => !(c.Concepto == LoanConcept.CurrentInterest && sinEmpezar.Contains(c.Libro))).ToList();
            var calendario = ConLaCuotaCompleta(estado, cobrables, salida, avisos, cuotaCompleta, periodoCalendario, dejada);
            if (calendario.Cancela) return calendario with { InteresNoCobrado = noCobrado };
        }
        return ConLaCuotaCompleta(estado, cubetas, salida, avisos, cuotaCompleta, periodoCalendario, dejada);
    }

    /// <summary>La cancelación con o sin la cuota completa (2026-11-16); por período calendario (2026-11-24), la cuota completa sólo de un tramo empezado.</summary>
    private static DistribucionDePago ConLaCuotaCompleta(EstadoDelCredito estado, List<Cubeta> cubetas, DistribucionDePago salida, IReadOnlyList<string> avisos,
        bool cuotaCompleta, bool periodoCalendario, int? dejada)
    {
        var corrida = Cancelacion(estado, cubetas, salida, avisos, dejada);
        // 2026-11-27 (feature 028, R4): la cuota que se deja no cobra su cuota completa: su interés del período se causa y queda en ella.
        var extra = cuotaCompleta ? InteresDeLaCuotaCompleta(estado, periodoCalendario).Where(l => dejada is null || l.Cuota != dejada).ToList() : [];
        if (extra.Count == 0) return corrida;
        var regulares = cubetas.Sum(c => c.Monto);
        var conLaCuota = Cancelacion(estado, [.. cubetas, .. extra.Select(e => new Cubeta(e.Cuota, e.Cuota, e.Concepto, e.Monto))], salida, avisos, dejada);
        var fondos = conLaCuota.AnticipoUsado.Sum(u => u.Monto) + conLaCuota.Aplicado;
        return fondos < regulares
            ? corrida
            : conLaCuota with { InteresPorCausar = extra, Avisos = [AvisoInteresDeLaCuotaCompleta, .. conLaCuota.Avisos] };
    }

    /// <summary>La cancelación sobre unas cubetas: todo lo cobrable, primero con el anticipo del mismo crédito (D7).</summary>
    private static DistribucionDePago Cancelacion(EstadoDelCredito estado, List<Cubeta> cubetas, DistribucionDePago salida, IReadOnlyList<string> avisos,
        int? dejada = null)
    {
        var total = cubetas.Sum(c => c.Monto);
        var usado = new List<LineaDeDistribucion>();
        var porUsar = total;
        foreach (var s in Anticipos(estado, dejada))
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

    /// <summary>
    /// El anticipo del mismo crédito, en el orden en que lo usa una cancelación: el general y después el de cada cuota (D7). 2026-11-27 (feature 028, R4): la
    /// cancelación salvo la cuota <paramref name="dejada"/> usa primero el de las cuotas que salen del plan, después el general, el de la cuota dejada y el de las
    /// anteriores: así la cuota queda debiéndose tal como se envió y su canal la cierra al peso, y lo que sobre no queda en una cuota que ya no existe.
    /// </summary>
    private static IEnumerable<SaldoDeCuota> Anticipos(EstadoDelCredito estado, int? dejada)
    {
        var anticipos = estado.Saldos.Where(s => s.Concepto == LoanConcept.Advance && s.Valor > 0m);
        return dejada is not { } n
            ? anticipos.OrderBy(s => s.Cuota is null ? 0 : 1).ThenBy(s => s.Cuota)
            : anticipos.OrderBy(s => s.Cuota is { } c && c > n ? 0 : s.Cuota is null ? 1 : s.Cuota == n ? 2 : 3).ThenBy(s => s.Cuota);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059; FR-019): la <b>cuota causada</b> que la cancelación salvo cuota deja a <paramref name="fecha"/>: la cuota del período
    /// en curso —la primera que vence después de la fecha y cuyo período ya empezó (su <c>Desde</c> es anterior a la fecha) y, con
    /// <paramref name="periodoCalendario"/>, cuyo tramo del calendario ya empezó (<see cref="CuotasDelTramoEnCurso"/>)—. Nula si no la hay (la fecha es un
    /// vencimiento, el crédito está en gracia, el tramo de la próxima no ha empezado) o el crédito es de vivienda (<see cref="AvisoViviendaSeCancelaCompleto"/>).
    /// Es la misma cuota cuyo interés por causar proyecta <see cref="EstadoDelCredito.InteresPorCausarDeLaCuotaEnCurso"/>. (nuevo)
    /// </summary>
    public static int? CuotaCausada(EstadoDelCredito estado, DateOnly fecha, bool periodoCalendario)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (estado.Plan is not { } plan || DejaLaCuotaEnVivienda(estado)) return null;
        var cuota = plan.Cuotas.FirstOrDefault(c => c.Vencimiento > fecha);
        if (cuota is null || cuota.Desde >= fecha) return null;
        if (periodoCalendario && !PeriodoDeCausacion.TramoEmpezado(cuota.Vencimiento, plan.Entrada.Periodicidad, fecha)) return null;
        return cuota.Numero;
    }

    /// <summary>¿Es un crédito de vivienda (en UVR o con las reglas de vivienda)? Se recoge completo (T062).</summary>
    private static bool DejaLaCuotaEnVivienda(EstadoDelCredito estado) => estado.EnUvr || VersionesDeReglas.EsDeVivienda(estado.RulesVersion);

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059; FR-019, D13-A): la cancelación de <b>todo menos la cuota causada</b> <c>n</c> (<see cref="CuotaCausada"/>, o la que trae
    /// la solicitud si es esa misma):
    /// <list type="bullet">
    /// <item>cubre, con la prelación de siempre, todo lo cobrable salvo los conceptos de <c>n</c> —con el modo de intereses de la cancelación para las demás
    /// cuotas (la cuota completa y, por período calendario, lo de un tramo sin empezar)— y el capital de las cuotas siguientes, que salen del plan
    /// (<see cref="GeneradorDePlan.CortarDespuesDe"/>);</item>
    /// <item>usa primero el anticipo del mismo crédito (<see cref="Anticipos"/>); lo que sobre del de las cuotas que salen pasa al anticipo general;</item>
    /// <item><c>n</c> queda <b>tal como se envió</b>: si la causación no le había causado todo el interés del período, se causa ahora lo que falta
    /// (<see cref="DistribucionDePago.InteresDeLaCuotaDejada"/>: lo mismo que cobraría la cuota completa, sobre el saldo con que se proyectó) y queda
    /// <b>congelada</b> (<see cref="DistribucionDePago.CuotaDejada"/>): no causa más interés corriente mientras no esté vencida. Por período calendario o al
    /// inicio del ciclo con el período ya causado, no hay nada que causar: sólo se congela.</item>
    /// </list>
    /// Sin cuota causada, con otra pedida o en vivienda es la cancelación total, con su aviso. Si los fondos no alcanzan lo demás, es una cancelación parcial
    /// sobre todo menos <c>n</c>: no se deja ni se congela nada.
    /// <para>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño; Ley 1555 de 2012): con <see cref="SolicitudDeDistribucion.CuotaDejadaProporcional"/> la cuota no queda
    /// tal como se envió sino con el <b>interés proporcional</b>: lo que se causa en ella es el interés de los días que le faltan sólo sobre su propio capital
    /// (<see cref="EstadoDelCredito.InteresPorCausarSobreSuCapital"/>); el de esos días sobre el capital que se cancela no se cobra. Lo que se cancela no cambia.
    /// La congelación fija ese valor. Aviso <see cref="AvisoCuotaDejadaProporcional"/>.
    /// </para>
    /// </summary>
    private static DistribucionDePago CancelacionSalvoCuota(EstadoDelCredito estado, List<Cubeta> cubetas, DistribucionDePago salida, SolicitudDeDistribucion s)
    {
        var v = s.FechaValor;
        var n = CuotaCausada(estado, v, s.PeriodoCalendario);
        if (n is null || (s.Cuota is { } pedida && pedida != n))
        {
            var total = Cancelacion(estado, cubetas, salida with { Cuota = null }, [], s.CuotaCompleta, s.PeriodoCalendario);
            return total with { Avisos = [.. total.Avisos, DejaLaCuotaEnVivienda(estado) ? AvisoViviendaSeCancelaCompleto : AvisoSinCuotaQueDejar] };
        }

        var dejada = n.Value;
        var resto = cubetas.Where(c => c.Orden != dejada).ToList();
        var pago = Cancelacion(estado, resto, salida with { Cuota = dejada }, [], s.CuotaCompleta, s.PeriodoCalendario, dejada);
        if (!pago.Cancela) return pago;

        // Lo que sobre del anticipo de las cuotas que salen del plan pasa al anticipo general (sale de su cuota y entra sin cuota).
        var usado = pago.AnticipoUsado.ToList();
        var alGeneral = 0m;
        foreach (var a in estado.Saldos.Where(x => x.Concepto == LoanConcept.Advance && x.Valor > 0m && x.Cuota is { } c && c > dejada))
        {
            var i = usado.FindIndex(u => u.Cuota == a.Cuota);
            var sobra = a.Valor - (i >= 0 ? usado[i].Monto : 0m);
            if (sobra <= 0m) continue;
            if (i >= 0) usado[i] = usado[i] with { Monto = a.Valor };
            else usado.Add(new LineaDeDistribucion(a.Cuota, LoanConcept.Advance, a.Valor));
            alGeneral += sobra;
        }

        var plan = estado.Plan!;
        return pago with
        {
            Cancela = false,
            CuotaDejada = dejada,
            InteresDeLaCuotaDejada = InteresQueSeFija(estado, dejada, s.CuotaDejadaProporcional),
            CuotaDejadaProporcional = s.CuotaDejadaProporcional,
            PlanNuevo = plan.Cuotas.Any(c => c.Numero > dejada) ? GeneradorDePlan.CortarDespuesDe(plan, dejada, v) : null,
            AnticipoUsado = usado,
            Anticipo = pago.Anticipo + alGeneral,
            CuotaDelAnticipo = null,
            Avisos = s.CuotaDejadaProporcional
                ? [AvisoCuotaCausadaDejada, AvisoCuotaDejadaProporcional, .. pago.Avisos]
                : [AvisoCuotaCausadaDejada, .. pago.Avisos],
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, R4; D13 ajustada): el interés que se causa en la cuota dejada en la fecha valor: tal como se envió (D13-A), lo que le falta del
    /// período sobre el saldo con que se proyectó (<see cref="EstadoDelCredito.InteresPorCausarDeLaCuotaEnCurso"/>); con el proporcional, lo de los días que le
    /// faltan sólo sobre su capital (<see cref="EstadoDelCredito.InteresPorCausarSobreSuCapital"/>). (nuevo)
    /// </summary>
    private static List<LineaDeDistribucion> InteresQueSeFija(EstadoDelCredito estado, int dejada, bool proporcional) =>
        (proporcional ? estado.InteresPorCausarSobreSuCapital(dejada) : estado.InteresPorCausarDeLaCuotaEnCurso())
        .Where(l => l.Cuota == dejada && l.Monto > 0m).ToList();

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059): lo que cuesta, a la fecha del estado, la cancelación de todo menos la cuota causada, sin aplicarla (la proyección de la
    /// recogida y la caja): el mismo reparto que <see cref="PaymentModality.PayoffExceptCurrentInstallment"/> con fondos de sobra. Devuelve la cuota que se deja
    /// (nula si no hay cuota causada, pidió otra o el crédito es de vivienda: entonces lo que cuesta es la cancelación total), lo que cuesta cancelar lo demás
    /// —con el anticipo del mismo crédito ya descontado—, lo que la cuota deberá al vencer —lo que debe hoy de capital, interés, mora y otros cargos, el interés
    /// del período que se causa, el seguro y la administración del plan, menos el anticipo que sobre: lo que su canal paga para cerrarla al peso—, ese interés
    /// por causar y los avisos. Gemelo de <see cref="ValorParaCancelar"/>. (nuevo)
    /// </summary>
    /// <param name="proporcional">2026-11-27 (feature 028, D13 ajustada): la cuota se deja con el interés proporcional
    /// (<see cref="SolicitudDeDistribucion.CuotaDejadaProporcional"/>); lo que cuesta lo demás no cambia, lo que queda en la cuota sí. El resultado dice también lo
    /// que debería si se dejara tal como se envió (<see cref="ValorSalvoCuota.QuedaTalComoSeEnvio"/>).</param>
    public static ValorSalvoCuota ValorParaCancelarSalvoCuota(EstadoDelCredito estado, int? cuota, bool cuotaCompleta, bool periodoCalendario = false,
        bool proporcional = false)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (estado.Plan is null) return new ValorSalvoCuota(null, 0m, 0m, [], []);
        var cubetas = Cubetas(estado, Prelacion.PorDefecto);
        var extra = cuotaCompleta ? InteresDeLaCuotaCompleta(estado, periodoCalendario).Sum(l => l.Monto) : 0m;
        // Fondos de sobra: todo lo cobrable más la cuota completa (lo que no se aplica vuelve como excedente y no cuenta).
        var fondos = cubetas.Sum(c => c.Monto) + extra;
        var salida = new DistribucionDePago
        {
            Modalidad = PaymentModality.PayoffExceptCurrentInstallment, Valor = fondos, FechaValor = estado.Fecha, Cuota = cuota,
        };
        var d = CancelacionSalvoCuota(estado, cubetas, salida,
            new SolicitudDeDistribucion(PaymentModality.PayoffExceptCurrentInstallment, fondos, estado.Fecha)
            {
                Cuota = cuota, CuotaCompleta = cuotaCompleta, PeriodoCalendario = periodoCalendario, CuotaDejadaProporcional = proporcional,
            });
        if (d.CuotaDejada is not { } n) return new ValorSalvoCuota(null, d.Aplicado, 0m, [], d.Avisos);
        // Lo que la cuota deberá al vencer: lo que debe hoy salvo el seguro y la administración, el interés del período que se le causa, el seguro y la
        // administración del plan (siguen causándose hasta su vencimiento, como se enviaron) y menos el anticipo que sobre.
        var cuotaDelPlan = estado.Plan.Cuotas.First(c => c.Numero == n);
        var anticipoQueQueda = estado.Anticipo - d.AnticipoUsado.Sum(u => u.Monto) + d.Anticipo;
        var sinElInteres = cubetas.Where(c => c.Orden == n && c.Concepto is not (LoanConcept.Insurance or LoanConcept.Administration)).Sum(c => c.Monto)
                           + cuotaDelPlan.Seguro + cuotaDelPlan.Administracion - anticipoQueQueda;
        var queda = sinElInteres + d.InteresDeLaCuotaDejada.Sum(l => l.Monto);
        // Tal como se envió (D13-A): con el interés del período sobre todo el saldo, lo que su canal tiene para pagar.
        var comoSeEnvio = proporcional ? sinElInteres + InteresQueSeFija(estado, n, proporcional: false).Sum(l => l.Monto) : queda;
        return new ValorSalvoCuota(n, d.Aplicado, Math.Max(0m, queda), d.InteresDeLaCuotaDejada, d.Avisos)
        {
            Proporcional = proporcional, QuedaTalComoSeEnvio = Math.Max(0m, comoSeEnvio),
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

    /// <summary>
    /// Lo cobrable con saldo positivo, en el orden de la prelación, con el interés de la gracia repartida en su cuota. 2026-11-16 (pedido del dueño): con
    /// el orden de rubros de la empresa, el capital de una cuota con prima (<see cref="CuotaDelPlan.Extraordinaria"/>) va en el puesto de las cuotas
    /// extras.
    /// </summary>
    private static List<Cubeta> Cubetas(EstadoDelCredito estado, Prelacion prelacion)
    {
        var plan = estado.Plan!;
        var primera = plan.Cuotas[0].Numero;
        var conPrima = plan.Cuotas.Where(q => q.Extraordinaria != 0m).Select(q => q.Numero).ToHashSet();
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
        return prelacion.Ordenar(cubetas, c => c.Orden, c => c.Concepto,
            c => c.Concepto == LoanConcept.Principal && c.Orden is { } n && conPrima.Contains(n)).ToList();
    }

    /// <summary>Una cuota (o un saldo sin cuota) es exigible si vence hasta la fecha valor.</summary>
    private static bool Exigible(PlanDePagos plan, int? cuota, DateOnly fecha)
    {
        if (cuota is not { } n) return true;
        var c = plan.Cuotas.FirstOrDefault(q => q.Numero == n);
        return c is null || c.Vencimiento <= fecha;
    }

    /// <summary>
    /// Lo que vale una cuota al vencer según el plan: su capital pendiente, su interés, su seguro y su administración (feature 024, fase 2: en su propia
    /// cubeta; un crédito anterior la lleva dentro del seguro). 2026-11-16 (pedido del dueño): también los otros cargos que el libro ya lleva en esa cuota —gastos de cobranza y
    /// otros cargos con su número de cuota, que sólo trae la carga de un crédito migrado; el plan no tiene columna de otros y un cargo nuevo va sin
    /// cuota—, porque el día del vencimiento <see cref="AplicarAnticipo"/> cubre <b>todo</b> lo de la cuota: sin ellos, el anticipo de una cuota
    /// completa la dejaba debiendo esos cargos. Pública para que el detalle de la obligación diga lo mismo que cobra la cuota específica.
    /// </summary>
    public static decimal ValorDeCuota(EstadoDelCredito estado, CuotaDelPlan cuota)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(cuota);
        // 2026-11-24 (feature 025, fase 3): más el interés que la cuota lleva fuera del plan —el que trajo en la carga y el ajuste del corte—: el plan de un
        // crédito cargado sólo lleva el interés desde el corte, y sin esto la caja y la cuota específica subestimaban la primera cuota.
        return estado.Saldo(LoanConcept.Principal, cuota.Numero) + cuota.Interes + cuota.Seguro + cuota.Administracion + OtrosDeCuota(estado, cuota.Numero)
               + estado.InteresFueraDelPlan.GetValueOrDefault(cuota.Numero);
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): ¿la cuota está reconocida al inicio del ciclo a la fecha del estado? Su pago antes del vencimiento es
    /// la cuota del período completa (lo que la caja ofrece como causado), así que no lleva el aviso de que el anticipo no rebaja intereses. El pago se
    /// aplica igual que siempre: queda como anticipo de esa cuota y la cubre el día que vence (el capital se abona ese día). (nuevo)
    /// </summary>
    public static bool Reconocida(EstadoDelCredito estado, int numero)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return estado.Reconocidos.Any(r => r.Cuota == numero && r.Pendiente > 0m);
    }

    /// <summary>2026-11-16: los otros cargos que el libro lleva en la cuota (gastos de cobranza y otros cargos con su número). (nuevo)</summary>
    public static decimal OtrosDeCuota(EstadoDelCredito estado, int numero)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return Math.Max(0m, estado.Saldo(LoanConcept.CollectionFee, numero)) + Math.Max(0m, estado.Saldo(LoanConcept.OtherCharge, numero));
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): lo que cuesta hoy anticipar la cuota <paramref name="numero"/> completa —lo que la cuota específica
    /// (<see cref="PaymentModality.SpecificInstallment"/>) toma como anticipo de esa cuota—: su valor al vencer menos lo que ya tiene anticipado. Cero si
    /// la cuota no existe o ya es exigible a la fecha del estado (entonces no se anticipa: se paga). El interés es el del plan, calculado sobre el saldo
    /// después del capital de la anterior, así que anticipar varias cuotas seguidas suma lo de cada una. (nuevo)
    /// </summary>
    public static decimal ValorParaAnticipar(EstadoDelCredito estado, int numero)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var cuota = estado.Plan?.Cuotas.FirstOrDefault(c => c.Numero == numero);
        if (cuota is null || cuota.Vencimiento <= estado.Fecha) return 0m;
        return Math.Max(0m, ValorDeCuota(estado, cuota) - Anticipo(estado, numero));
    }

    /// <summary>El anticipo de una cuota (o el general, con <paramref name="cuota"/> nula).</summary>
    private static decimal Anticipo(EstadoDelCredito estado, int? cuota) =>
        estado.Saldos.Where(s => s.Concepto == LoanConcept.Advance && s.Cuota == cuota).Sum(s => s.Valor);
}
