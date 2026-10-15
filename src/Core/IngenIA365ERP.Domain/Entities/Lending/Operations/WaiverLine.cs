using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_WaiverLines</c> (feature 013, K3, T0871; data-model §25.5; FR-094): lo que se condona de un concepto —mora, corriente, suspenso,
/// gasto de cobranza, seguro y, por excepción, capital—, en una cuota o en todas (cuota nula: el concepto en todas, en orden de prelación),
/// con el saldo disponible a la fecha de la liquidación base. Lo pedido no supera ese saldo (<see cref="CodigoExcedeElSaldo"/>). Único por
/// condonación, concepto y cuota. No es hecho: se arma en el borrador. (nuevo)
/// </summary>
public class WaiverLine : AuditableEntity
{
    /// <summary>Lo pedido supera el saldo del concepto a la fecha base (<c>data: { concept, balance }</c>).</summary>
    public const string CodigoExcedeElSaldo = "Lending.Waiver.ExceedsBalance";

    /// <summary>Los conceptos que se condonan (FR-094); el capital, sólo por excepción.</summary>
    public static IReadOnlySet<LoanConcept> Condonables { get; } = new HashSet<LoanConcept>
    {
        LoanConcept.DefaultInterest, LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense,
        LoanConcept.CollectionFee, LoanConcept.Insurance, LoanConcept.Principal,
    };

    public int WaiverId { get; init; }

    public LoanConcept Concept { get; init; }

    /// <summary>Nula = el concepto en todas las cuotas, en orden de prelación.</summary>
    public int? InstallmentNumber { get; init; }

    public decimal Amount { get; init; }

    /// <summary>El saldo del concepto a <c>AsOfDate</c>, sellado al pedirla.</summary>
    public decimal AvailableAtRequest { get; init; }

    /// <summary>Nulo si la línea cabe; si no, el problema (concepto no condonable, monto no positivo o mayor que el saldo).</summary>
    public static ProblemaDeCartera? Validar(LoanConcept concepto, decimal monto, decimal disponible)
    {
        if (!Condonables.Contains(concepto))
            return ProblemaDeCartera.Con(CodigoExcedeElSaldo, $"El concepto {concepto} no se condona.", "concept", concepto.ToString());
        if (monto <= 0m)
            return new ProblemaDeCartera(CodigoExcedeElSaldo, "Lo que se condona es mayor que cero.",
                new Dictionary<string, object?> { ["concept"] = concepto.ToString(), ["balance"] = disponible });
        return monto > disponible
            ? new ProblemaDeCartera(CodigoExcedeElSaldo,
                $"Se pide condonar {ExplicacionDeCartera.Numero(monto)} de {concepto} y el saldo es {ExplicacionDeCartera.Numero(disponible)}.",
                new Dictionary<string, object?> { ["concept"] = concepto.ToString(), ["balance"] = disponible })
            : null;
    }
}
