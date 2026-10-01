using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Los errores del crédito en la venta (feature 012, I3, T653–T655; contracts/api.md §23.1, §23.2; T32). La evaluación
/// (<c>reasons[]</c>) y el cobro usan <b>los mismos códigos</b>: <c>Inventory.Credit.PersonNotIdentified</c>, <c>.PersonInactive</c>,
/// <c>.NotAssociate</c>, <c>.NotCustomer</c>. <c>.TermsOutOfRange</c> (condiciones fuera de los máximos del medio) y
/// <c>.MeansNotCredit</c> (evaluar un medio que no es de crédito) son (nuevo). Los de Cartera habilitada (<c>.LendingBlocked</c>,
/// <c>.QuotaExceeded</c>, <c>.LendingNoResponse</c>) son de la entrega IC (T662, bloqueada por D-02).
/// </summary>
public static class ErroresDeCredito
{
    public const string PersonNotIdentifiedCode = "Inventory.Credit.PersonNotIdentified";
    public const string PersonInactiveCode = "Inventory.Credit.PersonInactive";
    public const string NotAssociateCode = "Inventory.Credit.NotAssociate";
    public const string NotCustomerCode = "Inventory.Credit.NotCustomer";
    public const string TermsOutOfRangeCode = "Inventory.Credit.TermsOutOfRange";
    public const string MeansNotCreditCode = "Inventory.Credit.MeansNotCredit";

    public static Error PersonNotIdentified() => new(PersonNotIdentifiedCode,
        "Una venta a crédito exige un cliente identificado: al consumidor final no se le vende a crédito.");

    public static Error PersonInactive(string nombre) => new ErrorConDatos(PersonInactiveCode,
        $"{nombre} está inactiva en el maestro de personas: no se le vende a crédito.", new { name = nombre });

    public static Error NotAssociate(string nombre) => new ErrorConDatos(NotAssociateCode,
        $"{nombre} no es asociado vigente (no es asociado o tiene retiro): el crédito a asociados no aplica.", new { name = nombre });

    public static Error NotCustomer(string nombre) => new ErrorConDatos(NotCustomerCode,
        $"{nombre} no está marcada como cliente: el crédito comercial no aplica.", new { name = nombre });

    public static Error TermsOutOfRange(string medio, int? paymentIndex, short? maxInstallments, short? maxTermDays) => new ErrorConDatos(TermsOutOfRangeCode,
        $"Las condiciones del crédito {medio} están fuera de lo que admite el medio (cuotas hasta {maxInstallments?.ToString() ?? "—"}, plazo hasta {maxTermDays?.ToString() ?? "—"} días).",
        new { paymentMeansCode = medio, paymentIndex, maxInstallments, maxTermDays });

    /// <summary>
    /// Nadie en la cooperativa —sin contar a quien cobra ni a quien creó la venta— tiene un monto máximo de <c>Inventory.Sales.SellOnCredit</c>
    /// que alcance lo financiado (§23.2): el código es el de los montos por permiso, <c>Inventory.Approval.AmountExceedsLimit</c>.
    /// </summary>
    public static Error NadiePuedeAprobar(decimal monto, decimal maximo) => new ErrorConDatos(IngenIA365ERP.Application.Common.Approvals.ErroresDeAprobaciones.CodigoExcedeLimite,
        $"Nadie en la cooperativa puede aprobar un crédito de {monto:N2}: el mayor monto autorizado es {maximo:N2}. Venda una parte de contado o pida ampliar el monto.",
        new { amount = monto, maxAmount = maximo, currency = "COP", permissionCode = AprobacionDeCredito.Permiso });

    /// <summary>El aprobador no tiene monto suficiente para lo financiado: la decisión no queda (§23.2).</summary>
    public static Error AprobadorSinMonto(decimal monto, decimal maximo) => new ErrorConDatos(IngenIA365ERP.Application.Common.Approvals.ErroresDeAprobaciones.CodigoExcedeLimite,
        $"Su monto máximo para vender a crédito es {maximo:N2} y el crédito es de {monto:N2}: lo aprueba alguien con un monto mayor.",
        new { amount = monto, maxAmount = maximo, currency = "COP", permissionCode = AprobacionDeCredito.Permiso });

    public static Error MeansNotCredit(string medio) => new ErrorConDatos(MeansNotCreditCode,
        $"El medio {medio} no es de crédito: sólo se evalúan los de crédito a asociados o comercial.", new { paymentMeansCode = medio });
}
