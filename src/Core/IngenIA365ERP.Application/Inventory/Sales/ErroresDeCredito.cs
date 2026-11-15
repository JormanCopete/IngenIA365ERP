using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Los errores del crédito en la venta (feature 012, I3, T653–T655; contracts/api.md §23.1, §23.2; T32). La evaluación
/// (<c>reasons[]</c>) y el cobro usan <b>los mismos códigos</b>: <c>Inventory.Credit.PersonNotIdentified</c>, <c>.PersonInactive</c>,
/// <c>.NotAssociate</c>, <c>.NotCustomer</c>. <c>.TermsOutOfRange</c> (condiciones fuera de los máximos del medio) y
/// <c>.MeansNotCredit</c> (evaluar un medio que no es de crédito) son (nuevo). Los de Cartera habilitada (<c>.LendingBlocked</c>,
/// <c>.QuotaExceeded</c>, <c>.LendingNoResponse</c>) los suma la feature 013 (T0176; contracts/interfaces.md §5.3, T95).
/// </summary>
public static class ErroresDeCredito
{
    public const string PersonNotIdentifiedCode = "Inventory.Credit.PersonNotIdentified";
    public const string PersonInactiveCode = "Inventory.Credit.PersonInactive";
    public const string NotAssociateCode = "Inventory.Credit.NotAssociate";
    public const string NotCustomerCode = "Inventory.Credit.NotCustomer";
    public const string TermsOutOfRangeCode = "Inventory.Credit.TermsOutOfRange";
    public const string MeansNotCreditCode = "Inventory.Credit.MeansNotCredit";
    public const string LendingBlockedCode = "Inventory.Credit.LendingBlocked";
    public const string QuotaExceededCode = "Inventory.Credit.QuotaExceeded";
    public const string LendingNoResponseCode = "Inventory.Credit.LendingNoResponse";
    public const string ArrearsConfirmationRequiredCode = "Inventory.Credit.ArrearsConfirmationRequired";
    public const string ArrearsApprovalRequiredCode = "Inventory.Credit.ArrearsApprovalRequired";
    public const string ArrearsApprovalUsedCode = "Inventory.Credit.ArrearsApprovalUsed";
    public const string ArrearsApprovalNotRequiredCode = "Inventory.Credit.ArrearsApprovalNotRequired";

    /// <summary>
    /// Decisión del dueño del 2026-11-12 (política «Advertir»): el deudor tiene más mora que la tolerancia y quien cobra no confirmó que la vio (o vio
    /// menos días de los que hay hoy). La pantalla lo pide en el panel de pago. (nuevo)
    /// </summary>
    public static Error ArrearsConfirmationRequired(string nombre, int dias) => new ErrorConDatos(ArrearsConfirmationRequiredCode,
        $"{nombre} tiene {dias} días de mora: para venderle a crédito confirme en el panel de pago que vio la mora.",
        new { name = nombre, daysPastDue = dias });

    /// <summary>Decisión del dueño del 2026-11-12 (política «Aprobacion»): falta la aprobación del supervisor presente, o ya no corresponde. (nuevo)</summary>
    public static Error ArrearsApprovalRequired(string nombre, int dias) => new ErrorConDatos(ArrearsApprovalRequiredCode,
        $"{nombre} tiene {dias} días de mora: venderle a crédito exige la aprobación de un supervisor presente. Pídala en el panel de pago "
        + "(si cambió el valor, la línea o la fecha, la aprobación anterior ya no vale).",
        new { name = nombre, daysPastDue = dias });

    /// <summary>La aprobación de la venta con mora ya cubrió otra venta: sirve a una sola. (nuevo)</summary>
    public static Error ArrearsApprovalUsed(Guid aprobacion) => new ErrorConDatos(ArrearsApprovalUsedCode,
        "La aprobación del supervisor para vender con mora ya se usó en otra venta: pida una nueva en el panel de pago.",
        new { approvalRequestPublicId = aprobacion });

    /// <summary>Se pidió la aprobación de una venta con mora que hoy no la exige (sin mora, dentro de la tolerancia, otra política o bloqueada). (nuevo)</summary>
    public static Error ArrearsApprovalNotRequired(string accion) => new ErrorConDatos(ArrearsApprovalNotRequiredCode,
        accion == AccionesPorMora.Blocked
            ? "Cartera no admite esta venta a crédito: un supervisor no puede aprobarla."
            : "Esta venta a crédito no exige la aprobación de un supervisor por mora.",
        new { arrearsAction = accion });

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

    /// <summary>Feature 013 (T0176, §5.3): Cartera dice que la persona está bloqueada o que su estado no admite la venta.</summary>
    public static Error LendingBlocked(string nombre, string? motivo) => new ErrorConDatos(LendingBlockedCode,
        string.IsNullOrWhiteSpace(motivo)
            ? $"Cartera no admite una venta a crédito a {nombre} por su estado crediticio."
            : $"Cartera no admite una venta a crédito a {nombre}: {motivo}",
        new { name = nombre, reason = motivo });

    /// <summary>Feature 013 (T0176, §5.3): lo financiado supera el cupo disponible que informa Cartera (<c>data.availableQuota</c>).</summary>
    public static Error QuotaExceeded(decimal monto, decimal? disponible) => new ErrorConDatos(QuotaExceededCode,
        disponible is { } d
            ? $"El crédito de {monto:N2} supera el cupo disponible en Cartera ({d:N2}). Venda una parte de contado o con otro medio."
            : $"El crédito de {monto:N2} supera el cupo disponible en Cartera. Venda una parte de contado o con otro medio.",
        new { amount = monto, availableQuota = disponible, currency = "COP" });

    /// <summary>Feature 013 (T0176, §5.3): Cartera no respondió en <c>Cartera.ConsultaSegundos</c> y la política es <c>Bloquear</c>.</summary>
    public static Error LendingNoResponse(int segundos) => new ErrorConDatos(LendingNoResponseCode,
        $"Cartera no respondió en {segundos} segundos y la política de la cooperativa es no vender a crédito sin su respuesta. Intente de nuevo en un momento.",
        new { seconds = segundos });

    public static Error MeansNotCredit(string medio) => new ErrorConDatos(MeansNotCreditCode,
        $"El medio {medio} no es de crédito: sólo se evalúan los de crédito a asociados o comercial.", new { paymentMeansCode = medio });
}
