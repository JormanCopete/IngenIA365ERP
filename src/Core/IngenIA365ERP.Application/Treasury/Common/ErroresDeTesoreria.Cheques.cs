using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, US19 (T0226, T0227; data-model §11, §21; DT §2.7): los errores de chequeras, cheques girados y remesas. Los que data-model §21 no nombraba son (nuevo).
public static partial class ErroresDeTesoreria
{
    public const string CodigoChequeraAgotada = "Treasury.Checkbook.Exhausted";
    public const string CodigoChequeraSeCruza = "Treasury.Checkbook.RangeOverlaps";

    public static Error ChequeraAgotada() => new(CodigoChequeraAgotada, "La chequera no tiene cheques suficientes: registre la siguiente.");

    public static Error ChequeraAnulada() => new("Treasury.Checkbook.Voided", "La chequera está anulada.");

    public static Error ChequeraSeCruza() => new(CodigoChequeraSeCruza, "El rango se cruza con otra chequera de la misma cuenta.");

    public static Error CuentaNoAdmiteChequera() => new("Treasury.Checkbook.AccountInactive", "La cuenta propia no está activa: no recibe chequeras.");

    public static Error ChequeraDeOtraCuenta() => new("Treasury.Checkbook.OtherAccount", "La chequera es de otra cuenta que la del lote.");

    public static Error LoteNoEsDeCheques() => new("Treasury.Batch.NotChecks", "El lote no es de forma «cheques».");

    public static Error LoteSinChequesPorEmitir() => new("Treasury.Batch.NoChecksToIssue", "Todas las órdenes del lote ya tienen su cheque.");

    public static Error ChequeNoEntregable(string estado) => new ErrorConDatos("Treasury.Check.NotDeliverable",
        $"El cheque está «{estado}»: sólo se entrega uno emitido (o se marca pagado uno entregado).", new { state = estado });

    public static Error ChequeNoAnulable(string estado) => new ErrorConDatos("Treasury.Check.NotVoidable",
        $"El cheque está «{estado}»: sólo se anula uno emitido o uno entregado que no se cobró.", new { state = estado });

    public static Error ChequeTodaviaCobrable(int dias) => new ErrorConDatos("Treasury.Check.StillCashable",
        $"El cheque entregado todavía se puede cobrar: se anula como «no cobrado» pasados {dias} días (Cheques.DiasSinCobrar).", new { days = dias });

    public static Error RemesaNoEnTransito(string estado) => new ErrorConDatos("Treasury.Remittance.NotInTransit",
        $"La remesa está «{estado}»: sólo se confirma una en tránsito.", new { state = estado });

    public static Error RemesaSinSoporte() => new("Treasury.Remittance.SupportRequired",
        "Confirme la remesa con su soporte (constancia del banco) o con la línea del extracto que la cruza.");

    public static Error RemesaDeOtraCuenta() => new("Treasury.Remittance.OtherAccount", "La línea del extracto es de otra cuenta que la de la remesa.");

    public static Error RemesaCuentaInvalida() => new("Treasury.Remittance.AccountNotAllowed",
        "La cuenta de la remesa tiene que estar activa y ser de recaudo (o del fondo de liquidez con su uso aprobado).");
}
