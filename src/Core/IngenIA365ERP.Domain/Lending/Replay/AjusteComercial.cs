using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// La clase de un ajuste de una venta a crédito que manda Comercial (<c>AjusteDeVentaACredito.adjustmentClass</c>, mensajes.md §12.3; FR-054).
/// <c>Replacement</c> no está: un reemplazo es un crédito nuevo, no un ajuste del anulado (C17). No es columna: viaja en el
/// <c>Payload</c> de la operación <c>CommercialAdjustment</c>. (nuevo)
/// </summary>
public enum ClaseDeAjusteComercial
{
    CreditNote = 1,
    Return = 2,
    DebitNote = 3,
    Voiding = 4,
    VoidingByDianRejection = 5,
}

/// <summary>
/// Lo que la reconstrucción lee de una operación <c>CommercialAdjustment</c> (feature 013, US6, T0566; FR-054): la clase, el valor con el signo
/// que manda Comercial (negativo baja, positivo sube) y cómo se recalcula el plan tras una nota crédito o una devolución
/// (<c>NoteRecalculation</c> sellado en las condiciones). (nuevo)
/// </summary>
public sealed record ContenidoDeAjusteComercial(ClaseDeAjusteComercial Clase, decimal Monto)
{
    /// <summary>La elección con que se regenera el plan tras una nota crédito o una devolución (la de la línea, <c>NoteRecalculation</c>).</summary>
    public PrepaymentChoice Recalculo { get; init; } = PrepaymentChoice.ReduceInstallment;

    /// <summary>¿Deja el crédito sin efecto desde su origen?</summary>
    public bool EsAnulacion => Clase is ClaseDeAjusteComercial.Voiding or ClaseDeAjusteComercial.VoidingByDianRejection;

    /// <summary>¿Baja el valor financiado (nota crédito o devolución)?</summary>
    public bool Baja => Clase is ClaseDeAjusteComercial.CreditNote or ClaseDeAjusteComercial.Return;

    /// <summary>
    /// La clase que manda Comercial como texto (<c>CreditNote</c>, <c>Return</c>, <c>DebitNote</c>, <c>Voiding</c>, <c>VoidingByDianRejection</c>);
    /// nula si no es una de ellas (<c>Replacement</c> incluido: no es un ajuste del crédito anulado).
    /// </summary>
    public static ClaseDeAjusteComercial? DeTexto(string? clase) =>
        Enum.TryParse<ClaseDeAjusteComercial>(clase, ignoreCase: false, out var c) && Enum.IsDefined(c) ? c : null;
}

/// <summary>
/// El intérprete de <c>CommercialAdjustment</c> (feature 013, US6, T0566; FR-054; mensajes.md §12.3), con el signo que manda Comercial y desde su
/// fecha valor (un hecho con fecha valor v rige desde v + 1):
/// <list type="bullet">
/// <item><b>Nota crédito o devolución</b> (valor X &lt; 0): baja el capital pendiente P en min(|X|, P); reversa, cuota por cuota, la parte del
/// interés corriente causado y no pagado que corresponde a lo devuelto (saldo × bajado ÷ P, redondeado por concepto); el capital por vencer se
/// baja regenerando el plan con la elección sellada (<c>NoteRecalculation</c>) —si la baja lo cubre entero, cada cuota por vencer queda en cero—,
/// lo que sobra baja el capital vencido de la cuota más antigua a la más nueva, y lo que supera P es saldo a favor.</item>
/// <item><b>Nota débito</b> (X &gt; 0): sube el capital y regenera el plan conservando las cuotas.</item>
/// <item><b>Anulación</b> (<c>Voiding</c>, <c>VoidingByDianRejection</c>): deja el crédito sin efecto desde su origen: cancela todo saldo cobrable
/// (capital, corriente, mora, suspensos, seguro y cargos) con filas <c>CommercialAdjustment</c> y deja como saldo a favor todo lo que se pagó
/// (lo aplicado por los pagos menos lo ya devuelto por resoluciones), con el anticipo que quedaba. Después no se causa nada: no queda base.</item>
/// </list>
/// La reconstrucción con R anterior al registro del ajuste no lo ve (T24). (nuevo)
/// </summary>
internal sealed class InterpreteDeAjusteComercial : IInterpreteDeOperacion
{
    private static readonly LoanConcept[] Cobrables =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    ];

    public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
    {
        var ajuste = operacion.Contenido as ContenidoDeAjusteComercial
            ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "El ajuste de Comercial no trae su clase y su valor: la reconstrucción no lo puede aplicar.", "operation", operacion.Id));
        if (ajuste.EsAnulacion) Anular(contexto, operacion);
        else if (ajuste.Baja) Bajar(contexto, operacion, -ajuste.Monto, ajuste.Recalculo);
        else Subir(contexto, operacion, ajuste.Monto);
    }

    private static void Bajar(ContextoDeReconstruccion ctx, EntradaDeOperacion op, decimal devuelto, PrepaymentChoice recalculo)
    {
        if (devuelto <= 0m) throw new ArgumentException("Una nota crédito o una devolución bajan el valor: llegan con signo negativo.", nameof(op));
        var v = op.FechaValor;
        var plan = ctx.Plan ?? throw new InvalidOperationException("No hay plan que ajustar.");
        var estado = ctx.Instantanea(v);
        var pendiente = plan.Cuotas.Sum(ctx.CapitalPendiente);
        var bajado = Math.Min(devuelto, pendiente);

        // El interés corriente causado sobre lo devuelto, cuota por cuota.
        if (bajado > 0m && pendiente > 0m)
        {
            foreach (var s in estado.Saldos.Where(s => s.Valor > 0m && s.Cuota is not null
                                                     && s.Concepto is LoanConcept.CurrentInterest or LoanConcept.CurrentInterestInSuspense)
                         .OrderBy(s => s.Cuota).ThenBy(s => (int)s.Concepto))
            {
                var reverso = ctx.Condiciones.Redondeo.Concepto(s.Valor * bajado / pendiente);
                ctx.Mover(v, LoanTransactionKind.CommercialAdjustment, s.Concepto, s.Cuota, -Math.Min(reverso, s.Valor), op.Id);
            }
        }

        // El capital por vencer: regenerando el plan, o en cero si la baja lo cubre entero.
        var porVencer = plan.Cuotas.Where(c => c.Vencimiento > v).ToList();
        var capitalPorVencer = porVencer.Sum(ctx.CapitalPendiente);
        var aPorVencer = Math.Min(bajado, capitalPorVencer);
        if (aPorVencer > 0m && aPorVencer < capitalPorVencer)
        {
            ctx.RegenerarPlan(new EntradaDeRegeneracion(v) { CambioDeCapital = -aPorVencer, Eleccion = recalculo });
            ctx.AgregarFila(new MovimientoDeLibro(v, LoanTransactionKind.CommercialAdjustment, LoanConcept.Principal, null, -aPorVencer) { OperacionId = op.Id });
        }
        else if (aPorVencer > 0m)
        {
            foreach (var c in porVencer)
                ctx.Mover(v, LoanTransactionKind.CommercialAdjustment, LoanConcept.Principal, c.Numero, -ctx.CapitalPendiente(c), op.Id);
        }

        // Lo que sobra baja el capital vencido, de la más antigua a la más nueva.
        var queda = bajado - aPorVencer;
        foreach (var c in ctx.Plan!.Cuotas.Where(c => c.Vencimiento <= v).OrderBy(c => c.Numero))
        {
            if (queda <= 0m) break;
            var t = Math.Min(queda, ctx.CapitalPendiente(c));
            if (t <= 0m) continue;
            ctx.Mover(v, LoanTransactionKind.CommercialAdjustment, LoanConcept.Principal, c.Numero, -t, op.Id);
            queda -= t;
        }

        var exceso = devuelto - bajado;
        if (exceso > 0m) ctx.Mover(v, LoanTransactionKind.CreditBalanceRecognized, LoanConcept.CreditBalance, null, exceso, op.Id);
    }

    private static void Subir(ContextoDeReconstruccion ctx, EntradaDeOperacion op, decimal monto)
    {
        if (monto <= 0m) throw new ArgumentException("Una nota débito sube el valor: llega con signo positivo.", nameof(op));
        var v = op.FechaValor;
        ctx.RegenerarPlan(new EntradaDeRegeneracion(v) { CambioDeCapital = monto });
        ctx.AgregarFila(new MovimientoDeLibro(v, LoanTransactionKind.CommercialAdjustment, LoanConcept.Principal, null, monto) { OperacionId = op.Id });
    }

    private static void Anular(ContextoDeReconstruccion ctx, EntradaDeOperacion op)
    {
        var v = op.FechaValor;
        var estado = ctx.Instantanea(v);
        foreach (var s in estado.Saldos.Where(s => s.Valor != 0m && Cobrables.Contains(s.Concepto)).OrderBy(s => s.Cuota ?? 0).ThenBy(s => (int)s.Concepto))
            ctx.Mover(v, LoanTransactionKind.CommercialAdjustment, s.Concepto, s.Cuota, -s.Valor, op.Id);

        // Lo pagado queda como saldo a favor: lo aplicado por los pagos menos lo ya devuelto (resoluciones), con el anticipo que quedaba.
        var aplicado = ctx.Pagos.Values.Sum(p => p.Aplicado);
        var resuelto = -ctx.Filas.Where(f => f.Clase == LoanTransactionKind.CreditBalanceSettled).Sum(f => f.Monto);
        var anticipos = ctx.SaldosFueraDeCausacion.Where(s => s.Key.Item1 == LoanConcept.Advance && s.Value != 0m).ToList();
        var saldoAFavor = ctx.SaldoFueraDeCausacion(LoanConcept.CreditBalance, null);
        foreach (var ((concepto, cuota), monto) in anticipos.OrderBy(a => a.Key.Item2 ?? 0))
            ctx.Mover(v, LoanTransactionKind.CommercialAdjustment, concepto, cuota, -monto, op.Id);
        var reconocer = aplicado - resuelto - saldoAFavor;
        if (reconocer > 0m) ctx.Mover(v, LoanTransactionKind.CreditBalanceRecognized, LoanConcept.CreditBalance, null, reconocer, op.Id);
    }
}
