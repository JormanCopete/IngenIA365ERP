using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// De qué lado de la novedad está el crédito que se reconstruye (K3, T0874): el crédito de la novedad (el que conserva su número en una prórroga,
/// un cambio de fecha o de tasa; el anterior que se cierra en una novación o refinanciación) o el <b>enlazado</b> que nace de una novación o
/// refinanciación. (nuevo)
/// </summary>
public enum LadoDeLaNovedad
{
    /// <summary>El crédito de la novedad: regenera su plan o, en una novación o refinanciación, traslada todo lo que queda al enlazado.</summary>
    Credito = 1,

    /// <summary>El crédito enlazado de una novación o refinanciación: nace con lo que le trasladan los anteriores.</summary>
    Enlazado = 2,
}

/// <summary>Un importe positivo de un concepto en una cuota (nula: sin cuota) que mueve una novedad o una condonación (K3). (nuevo)</summary>
public sealed record PartidaDeNovedad(LoanConcept Concepto, int? Cuota, decimal Monto);

/// <summary>
/// La decisión de una novedad tal como la lee la reconstrucción (K3, T0874; FR-090, FR-091): el mecanismo, la novedad (la versión de condiciones
/// que la reconoce lleva su <c>NoveltyId</c>), las cuotas nuevas y lo que cambia —tasa, periodicidad, día de pago, primer vencimiento—, el conteo de
/// reestructuraciones que deja, lo que capitaliza por concepto y cuota y, en el enlazado, lo que recibe. Va en
/// <see cref="EntradaDeOperacion.Contenido"/> de la operación <c>TermsChange</c>; lo arma <c>CargadorDeReconstruccion</c> desde la decisión que
/// guarda la operación. (nuevo)
/// </summary>
public sealed record ContenidoDeNovedad(NoveltyMechanism Mecanismo, int Cuotas)
{
    public int? NoveltyId { get; init; }

    public decimal? TasaEfectivaAnual { get; init; }

    public RepaymentPeriodicity? Periodicidad { get; init; }

    public int? DiaDePago { get; init; }

    public DateOnly? PrimerVencimiento { get; init; }

    /// <summary>El conteo de reestructuraciones desde el día siguiente a la fecha valor; nulo = no cambia (una modificación).</summary>
    public int? ReestructuracionesDespues { get; init; }

    /// <summary>De qué lado está el crédito que se reconstruye.</summary>
    public LadoDeLaNovedad Lado { get; init; } = LadoDeLaNovedad.Credito;

    /// <summary>
    /// Lo que la novedad capitaliza, por concepto y cuota del crédito de la novedad (interés corriente o de mora, causado o en suspenso; cargos):
    /// sale de su concepto y entra como capital. En el enlazado son las mismas partidas, que entran como capital del crédito nuevo.
    /// </summary>
    public IReadOnlyList<PartidaDeNovedad> Capitalizaciones { get; init; } = [];

    /// <summary>
    /// Sólo en el enlazado: lo que le trasladan los créditos anteriores —el capital sin cuota y los demás conceptos en su primera cuota (anticipo y
    /// saldo a favor sin cuota)—. En el crédito anterior no se da: se traslada todo lo que queda a la fecha valor.
    /// </summary>
    public IReadOnlyList<PartidaDeNovedad> Traslados { get; init; } = [];

    /// <summary>El total capitalizado.</summary>
    public decimal InteresCapitalizado => Capitalizaciones.Sum(c => c.Monto);

    /// <summary>¿Cierra el crédito y crea un enlazado? Novación y refinanciación.</summary>
    public bool CreaEnlazado => Mecanismo is NoveltyMechanism.Novation or NoveltyMechanism.Refinancing;
}

/// <summary>
/// Cómo aplica la reconstrucción una operación <c>TermsChange</c> (K3, T0874), en sus tres formas:
/// <list type="bullet">
/// <item><b>Prórroga, cambio de fecha o de tasa</b> (el crédito conserva su número): primero la capitalización —filas <c>Capitalization</c>,
/// <c>−</c> cada interés que pasa a capital y <c>+Principal</c> sin cuota por el total—; después el plan se regenera <b>desde la fecha valor</b>
/// con <see cref="GeneradorDePlan.RegenerarPorNovedad"/> sobre <b>todo</b> el capital pendiente más lo capitalizado, y la versión completa
/// (<see cref="GeneradorDePlan.UnirConAnterior"/>) conserva las cuotas ya vencidas con su número; el capital que esas cuotas debían queda
/// consolidado en el plan nuevo (fuera del plan, en cero), y los ajustes de pagos de las cuotas que se regeneran también. El interés causado y la
/// mora que no se capitalizan siguen en sus cuotas: se siguen debiendo.</item>
/// <item><b>Novación o refinanciación, en el crédito anterior</b>: la capitalización sale del crédito (<c>−</c> en <c>Capitalization</c>) y todo
/// lo que queda a la fecha valor —capital de cada cuota, intereses, seguros, cargos, anticipos, saldos a favor— pasa al enlazado
/// (<c>TransferToLinkedLoan</c>, <c>−</c>): el crédito queda en cero y desde el día siguiente ya no causa nada
/// (<see cref="ContextoDeReconstruccion.Extinguir"/>).</item>
/// <item><b>Novación o refinanciación, en el enlazado</b>: es su origen. Nace con el plan pactado de su versión de condiciones (monto = lo
/// trasladado más lo capitalizado, fecha = la fecha valor) y recibe <c>TransferFromLinkedLoan</c> por el capital (sin cuota) y por los demás
/// conceptos tal como se los trasladaron, y <c>Capitalization</c> <c>+Principal</c> por lo capitalizado.</item>
/// </list>
/// La tasa nueva rige desde el día siguiente, igual que el conteo de reestructuraciones (más de una suspende la causación, T30). Lo capitalizado no
/// supera el saldo del concepto en su cuota a la fecha valor (<see cref="GeneradorDePlan.CodigoNovedadInvalida"/>).
/// </summary>
public sealed class InterpreteDeNovedad : IInterpreteDeOperacion
{
    /// <summary>Lo que se capitaliza o traslada como un saldo del deudor (no el capital, el anticipo ni el saldo a favor).</summary>
    public static IReadOnlySet<LoanConcept> Capitalizables { get; } = new HashSet<LoanConcept>
    {
        LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense,
        LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    };

    public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(operacion);
        var n = operacion.Contenido as ContenidoDeNovedad
            ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "La novedad no trae su decisión (mecanismo y condiciones nuevas): la reconstrucción no la puede aplicar.", "operation", operacion.Id));

        if (n.Lado == LadoDeLaNovedad.Enlazado)
        {
            if (!n.CreaEnlazado)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(GeneradorDePlan.CodigoNovedadInvalida,
                    "Sólo una novación o una refinanciación crean un crédito enlazado.", "mechanism", n.Mecanismo.ToString()));
            Nacer(contexto, operacion, n);
            return;
        }

        if (contexto.Plan is null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOrigenInvalido,
                "Una novedad cambia un crédito que ya existe: el crédito empieza con un desembolso o con un saldo inicial migrado.", "operation", operacion.Id));
        // T0883: el cambio de oficina no toca saldos, plan ni tasa (lo que cambia es la dimensión contable, que no es del libro).
        if (n.Mecanismo == NoveltyMechanism.BranchChange) return;
        var capitalizado = Capitalizar(contexto, operacion, n);
        if (n.CreaEnlazado)
        {
            Trasladar(contexto, operacion);
            if (n.ReestructuracionesDespues is { } conteoAnterior) contexto.CambiarReestructuraciones(conteoAnterior);
            return;
        }
        if (capitalizado != 0m)
            contexto.AgregarFila(new MovimientoDeLibro(operacion.FechaValor, LoanTransactionKind.Capitalization, LoanConcept.Principal, null, capitalizado)
            {
                OperacionId = operacion.Id,
            });
        Regenerar(contexto, operacion, n, capitalizado);
    }

    /// <summary>Saca de cada concepto y cuota lo que se capitaliza (sin pasar de su saldo a la fecha valor). Devuelve el total.</summary>
    private static decimal Capitalizar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion, ContenidoDeNovedad n)
    {
        if (n.Capitalizaciones.Count == 0) return 0m;
        var v = operacion.FechaValor;
        var estado = contexto.Instantanea(v);
        var total = 0m;
        foreach (var p in n.Capitalizaciones)
        {
            if (!Capitalizables.Contains(p.Concepto) || p.Monto <= 0m)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(GeneradorDePlan.CodigoNovedadInvalida,
                    $"Se capitaliza interés, seguro o cargos por un valor mayor que cero; no {p.Concepto}.", "concept", p.Concepto.ToString()));
            var disponible = estado.Saldos.Where(s => s.Concepto == p.Concepto && s.Cuota == p.Cuota).Sum(s => s.Valor);
            if (p.Monto > disponible)
                throw new ReglaDeCarteraException(new ProblemaDeCartera(GeneradorDePlan.CodigoNovedadInvalida,
                    $"Se pide capitalizar {ExplicacionDeCartera.Numero(p.Monto)} de {p.Concepto}"
                    + (p.Cuota is { } c ? $" de la cuota {c}" : string.Empty) + $" y el saldo es {ExplicacionDeCartera.Numero(disponible)}.",
                    new Dictionary<string, object?> { ["concept"] = p.Concepto.ToString(), ["installment"] = p.Cuota, ["balance"] = disponible }));
            contexto.Mover(v, LoanTransactionKind.Capitalization, p.Concepto, p.Cuota, -p.Monto, operacion.Id);
            total += p.Monto;
        }
        return total;
    }

    /// <summary>Prórroga, cambio de fecha o de tasa: el plan nuevo desde la fecha valor sobre todo el capital pendiente más lo capitalizado.</summary>
    private static void Regenerar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion, ContenidoDeNovedad n, decimal capitalizado)
    {
        var plan = contexto.Plan!;
        var v = operacion.FechaValor;
        var saldo = plan.Cuotas.Sum(contexto.CapitalPendiente);
        var vencidas = plan.Cuotas.Where(c => c.Vencimiento <= v).ToList();
        var primera = vencidas.Count == 0 ? plan.Cuotas[0].Numero : vencidas[^1].Numero + 1;
        var tasa = n.TasaEfectivaAnual ?? contexto.TasaCorrienteEA;
        var nuevo = GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(n.Mecanismo, v,
            plan.Entrada with { TasaEfectivaAnual = contexto.TasaCorrienteEA }, saldo, n.Cuotas)
        {
            InteresCapitalizado = capitalizado,
            TasaEfectivaAnual = n.TasaEfectivaAnual,
            Periodicidad = n.Periodicidad,
            DiaDePago = n.DiaDePago,
            PrimerVencimiento = n.PrimerVencimiento,
            PrimeraCuota = primera,
        });
        var completo = vencidas.Count == 0 ? nuevo : GeneradorDePlan.UnirConAnterior(plan, nuevo);

        // El capital pendiente de cada cuota queda en el plan nuevo: el de las vencidas se consolida y los ajustes de las que se regeneran
        // (que comparten número con las nuevas) se ponen en cero.
        foreach (var c in plan.Cuotas)
        {
            var objetivo = c.Vencimiento <= v ? -c.Capital : 0m;
            var ajuste = contexto.SaldoFueraDeCausacion(LoanConcept.Principal, c.Numero);
            if (ajuste != objetivo) contexto.SumarSaldo(LoanConcept.Principal, c.Numero, objetivo - ajuste);
        }
        if (tasa != contexto.TasaCorrienteEA) contexto.CambiarTasa(tasa);
        contexto.CambiarPlan(completo);
        if (n.ReestructuracionesDespues is { } conteo) contexto.CambiarReestructuraciones(conteo);
    }

    /// <summary>El crédito anterior de una novación o refinanciación: todo lo que queda pasa al enlazado y el crédito deja de causar.</summary>
    private static void Trasladar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
    {
        var v = operacion.FechaValor;
        var estado = contexto.Instantanea(v);
        foreach (var s in estado.Saldos.Where(s => s.Valor != 0m).OrderBy(s => s.Cuota ?? 0).ThenBy(s => (int)s.Concepto))
            contexto.Mover(v, LoanTransactionKind.TransferToLinkedLoan, s.Concepto, s.Cuota, -s.Valor, operacion.Id);
        contexto.Extinguir(v);
    }

    /// <summary>
    /// El origen del enlazado: el plan pactado de sus condiciones (monto = capital trasladado + capitalizado, fecha = la fecha valor), el capital
    /// sin cuota y los demás conceptos como llegan.
    /// </summary>
    private static void Nacer(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion, ContenidoDeNovedad n)
    {
        if (contexto.Plan is not null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOrigenInvalido,
                "El crédito enlazado nace con la novación o la refinanciación: es su primer hecho.", "operation", operacion.Id));
        var pactado = contexto.Entrada.Plan ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOrigenInvalido,
            "El crédito enlazado necesita su plan pactado.", "operation", operacion.Id));
        var v = operacion.FechaValor;
        var capital = n.Traslados.Where(t => t.Concepto == LoanConcept.Principal).Sum(t => t.Monto);
        var capitalizado = n.InteresCapitalizado;
        if (pactado.Monto != capital + capitalizado || pactado.FechaDesembolso != v)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOrigenInvalido,
                "El plan pactado del crédito enlazado es el de la novedad: su monto es el capital trasladado más lo capitalizado y su fecha, la de la novedad.",
                "operation", operacion.Id));
        if (n.Traslados.Any(t => t.Monto <= 0m) || n.Capitalizaciones.Any(c => c.Monto <= 0m))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(GeneradorDePlan.CodigoNovedadInvalida,
                "Lo que recibe el crédito enlazado son importes mayores que cero.", "operation", operacion.Id));

        contexto.CambiarPlan(GeneradorDePlan.Generar(pactado));
        if (capital != 0m)
            contexto.AgregarFila(new MovimientoDeLibro(v, LoanTransactionKind.TransferFromLinkedLoan, LoanConcept.Principal, null, capital)
            {
                OperacionId = operacion.Id,
            });
        if (capitalizado != 0m)
            contexto.AgregarFila(new MovimientoDeLibro(v, LoanTransactionKind.Capitalization, LoanConcept.Principal, null, capitalizado)
            {
                OperacionId = operacion.Id,
            });
        foreach (var t in n.Traslados.Where(t => t.Concepto != LoanConcept.Principal))
            contexto.Mover(v, LoanTransactionKind.TransferFromLinkedLoan, t.Concepto, t.Cuota, t.Monto, operacion.Id);
        if (n.ReestructuracionesDespues is { } conteo && conteo != contexto.Reestructuraciones) contexto.CambiarReestructuraciones(conteo);
    }
}

/// <summary>
/// Lo que condona una condonación aplicada, tal como la lee la reconstrucción (K3, T0884; FR-094): cada línea con su concepto, su cuota (la
/// condonación de «todas las cuotas» ya viene repartida en prelación) y su valor. Va en <see cref="EntradaDeOperacion.Contenido"/> de la operación
/// <c>Waiver</c>; lo arma <c>CargadorDeReconstruccion</c> desde la decisión que guarda la operación. (nuevo)
/// </summary>
public sealed record ContenidoDeCondonacion(IReadOnlyList<PartidaDeNovedad> Lineas);

/// <summary>
/// Cómo aplica la reconstrucción una condonación (K3, T0884): filas <c>Waiver</c> con <c>−</c> cada concepto y cuota condonados a la fecha valor,
/// sin pasar del saldo de ese concepto en esa cuota (<c>Lending.Waiver.ExceedsBalance</c>). El capital condonado baja el capital pendiente de su
/// cuota, como un pago a esa cuota; el plan no cambia. (nuevo)
/// </summary>
public sealed class InterpreteDeCondonacion : IInterpreteDeOperacion
{
    public const string CodigoExcedeElSaldo = "Lending.Waiver.ExceedsBalance";

    public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(operacion);
        var c = operacion.Contenido as ContenidoDeCondonacion
            ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "La condonación no trae lo que condona por concepto y cuota: la reconstrucción no la puede aplicar.", "operation", operacion.Id));
        if (contexto.Plan is null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOrigenInvalido,
                "Una condonación es sobre un crédito que ya existe.", "operation", operacion.Id));
        var v = operacion.FechaValor;
        var estado = contexto.Instantanea(v);
        foreach (var l in c.Lineas)
        {
            if (l.Monto <= 0m || l.Concepto is LoanConcept.Advance or LoanConcept.CreditBalance || (l.Concepto == LoanConcept.Principal && l.Cuota is null))
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoExcedeElSaldo,
                    $"La condonación de {l.Concepto} es por un valor mayor que cero" + (l.Concepto == LoanConcept.Principal ? " y en una cuota." : "."),
                    "concept", l.Concepto.ToString()));
            var disponible = estado.Saldos.Where(s => s.Concepto == l.Concepto && s.Cuota == l.Cuota).Sum(s => s.Valor);
            if (l.Monto > disponible)
                throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoExcedeElSaldo,
                    $"Se condonan {ExplicacionDeCartera.Numero(l.Monto)} de {l.Concepto}" + (l.Cuota is { } q ? $" en la cuota {q}" : string.Empty)
                    + $" y el saldo es {ExplicacionDeCartera.Numero(disponible)}.",
                    new Dictionary<string, object?> { ["concept"] = l.Concepto.ToString(), ["balance"] = disponible }));
            contexto.Mover(v, LoanTransactionKind.Waiver, l.Concepto, l.Cuota, -l.Monto, operacion.Id);
        }
    }
}
