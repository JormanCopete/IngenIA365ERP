using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US14 (T0240, T0243; data-model §21; DT §2.7): los errores del cumplimiento en la caja y de la contingencia. Los que data-model §21 no nombraba son (nuevo).
public static partial class ErroresDeRecaudo
{
    public const string CodigoRetencionRequerida = "Receipts.Compliance.WithholdRequired";

    public static Error RetencionRequerida() => new(CodigoRetencionRequerida,
        "Una persona de la operación coincide en una lista no vinculante y la cooperativa retiene: retenga el dinero en custodia; el oficial de cumplimiento lo libera o lo devuelve.");

    public static Error RetencionNoAplica() => new("Receipts.Compliance.WithholdNotApplicable",
        "La operación no se retiene: no hay coincidencia no vinculante, la cooperativa no retiene o el oficial ya la liberó.");

    public static Error RetencionSinEfectivo() => new("Receipts.Compliance.WithholdNoCash", "La operación no tiene efectivo que retener en la caja.");

    public static Error OperacionNoRetenida() => new("Receipts.Compliance.NotWithheld", "La operación no está retenida por cumplimiento.");

    public static Error LiberacionPorQuienRetuvo() => new("Receipts.Compliance.ReleaseBySameUser", "Quien retuvo el dinero no puede liberarlo: lo libera el oficial de cumplimiento.");

    public static Error LiberacionConSesionCerrada() => new("Receipts.Compliance.SessionClosed",
        "La sesión de la caja ya se cerró: la custodia se devuelve por orden de pago.");

    public static Error TalonarioNoUsable() => new("Receipts.Contingency.BookNotUsable", "El talonario no existe, no es del punto o no está activo.");

    public static Error NumeroFueraDelTalonario(int numero) => new ErrorConDatos("Receipts.Contingency.NumberOutOfRange",
        $"El número {numero} no está en el rango del talonario.", new { number = numero });

    public static Error NumeroDeTalonarioUsado(int numero) => new ErrorConDatos("Receipts.Contingency.NumberUsed",
        $"El recibo de contingencia {numero} ya se digitó.", new { number = numero });

    public static Error ImagenDeContingenciaRequerida() => new("Receipts.Contingency.ImageRequired", "Adjunte la imagen del recibo del talonario.");
}
