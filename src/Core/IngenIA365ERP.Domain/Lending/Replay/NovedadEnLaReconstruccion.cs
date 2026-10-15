using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// La decisión de una novedad que conserva el número del crédito, tal como la lee la reconstrucción (K3, T0874; FR-091): el mecanismo, la
/// novedad (la versión de condiciones que la reconoce lleva su <c>NoveltyId</c>), las cuotas nuevas y lo que cambia —tasa, periodicidad, día de
/// pago, primer vencimiento— y el conteo de reestructuraciones que deja. Va en <see cref="EntradaDeOperacion.Contenido"/> de la operación
/// <c>TermsChange</c>; lo arma <c>CargadorDeReconstruccion</c> desde la versión de condiciones con <c>NoveltyId</c>. (nuevo)
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

    /// <summary>Interés que la novedad capitaliza. La reconstrucción todavía no lo asienta (ver <see cref="InterpreteDeNovedad"/>).</summary>
    public decimal InteresCapitalizado { get; init; }
}

/// <summary>
/// Cómo aplica la reconstrucción una operación <c>TermsChange</c> (K3, T0874): el plan se regenera <b>desde la fecha valor</b> con
/// <see cref="GeneradorDePlan.RegenerarPorNovedad"/> sobre <b>todo</b> el capital pendiente —el vencido y el por vencer—, y la versión completa
/// (<see cref="GeneradorDePlan.UnirConAnterior"/>) conserva las cuotas ya vencidas con su número; el capital que esas cuotas debían queda
/// consolidado en el plan nuevo (fuera del plan, en cero), y los ajustes de pagos de las cuotas que se regeneran también. Su interés causado y
/// su mora siguen en ellas: lo que no se capitalice ni se condone se sigue debiendo. La tasa nueva rige desde el día siguiente, igual que el
/// conteo de reestructuraciones (más de una suspende la causación, T30).
///
/// <para>Lo que todavía no interpreta, con <see cref="ReconstruccionDeCredito.CodigoOperacionNoSoportada"/>: la novación o la refinanciación
/// en el crédito anterior (su saldo pasa al enlazado; el enlazado nace con un desembolso por ese saldo y sí se reconstruye) y la capitalización
/// de intereses, porque <c>LoanTransactionKind</c> no tiene clase de fila para ella (decisión pendiente del dueño).</para>
/// </summary>
public sealed class InterpreteDeNovedad : IInterpreteDeOperacion
{
    public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(operacion);
        var n = operacion.Contenido as ContenidoDeNovedad
            ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "La novedad no trae su decisión (mecanismo y condiciones nuevas): la reconstrucción no la puede aplicar.", "operation", operacion.Id));
        if (n.Mecanismo is NoveltyMechanism.Novation or NoveltyMechanism.Refinancing)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "La novación o la refinanciación cierran el crédito anterior: su saldo pasa al crédito enlazado, que es el que se reconstruye.",
                "mechanism", n.Mecanismo.ToString()));
        if (n.InteresCapitalizado != 0m)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "La reconstrucción todavía no asienta la capitalización de intereses de una novedad.", "capitalizedInterest", n.InteresCapitalizado));

        var plan = contexto.Plan ?? throw new InvalidOperationException("No hay plan que regenerar.");
        var v = operacion.FechaValor;
        var saldo = plan.Cuotas.Sum(contexto.CapitalPendiente);
        var vencidas = plan.Cuotas.Where(c => c.Vencimiento <= v).ToList();
        var primera = vencidas.Count == 0 ? plan.Cuotas[0].Numero : vencidas[^1].Numero + 1;
        var tasa = n.TasaEfectivaAnual ?? contexto.TasaCorrienteEA;
        var nuevo = GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(n.Mecanismo, v,
            plan.Entrada with { TasaEfectivaAnual = contexto.TasaCorrienteEA }, saldo, n.Cuotas)
        {
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
}
