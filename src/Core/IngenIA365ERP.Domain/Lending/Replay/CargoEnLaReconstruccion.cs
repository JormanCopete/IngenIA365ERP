using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// Lo que cobra un cargo aplicado, tal como lo lee la reconstrucción (feature 013, K4, T0948; FR-121, T77): el gasto de cobranza ya informado al
/// deudor, por su valor y sin cuota. Va en <see cref="EntradaDeOperacion.Contenido"/> de la operación <c>Charge</c>; lo arma
/// <c>CargadorDeReconstruccion</c> desde la decisión que guarda la operación. (nuevo)
/// </summary>
public sealed record ContenidoDeCargo(LoanConcept Concepto, decimal Monto);

/// <summary>
/// Cómo aplica la reconstrucción un cargo (feature 013, K4, T0948; data-model §5: <c>Charge</c> · <c>+CollectionFee</c>/<c>+OtherCharge</c>): una fila
/// <c>Charge</c> con <c>+</c> el concepto a la fecha valor, sin cuota; el plan no cambia y no causa intereses. Sólo gasto de cobranza u otro cargo, por
/// un valor mayor que cero. El gasto sólo llega al libro después de informarlo al deudor (el comando lo exige; la prelación lo cobra como los demás
/// cargos). (nuevo)
/// </summary>
public sealed class InterpreteDeCargo : IInterpreteDeOperacion
{
    public const string CodigoCargoInvalido = "Lending.Charge.Invalid";

    public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(operacion);
        var c = operacion.Contenido as ContenidoDeCargo
            ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOperacionNoSoportada,
                "El cargo no trae su concepto y su valor: la reconstrucción no lo puede aplicar.", "operation", operacion.Id));
        if (contexto.Plan is null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(ReconstruccionDeCredito.CodigoOrigenInvalido,
                "Un cargo es sobre un crédito que ya existe.", "operation", operacion.Id));
        if (c.Monto <= 0m || c.Concepto is not (LoanConcept.CollectionFee or LoanConcept.OtherCharge))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCargoInvalido,
                "Un cargo es un gasto de cobranza u otro cargo por un valor mayor que cero.", "concept", c.Concepto.ToString()));
        contexto.Mover(operacion.FechaValor, LoanTransactionKind.Charge, c.Concepto, null, c.Monto, operacion.Id);
    }
}
