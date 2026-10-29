using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US15 (T0213 a T0217; data-model §4.5; DT §2.7): los errores de los cheques recibidos. Los que data-model no nombraba son (nuevo).
public static partial class ErroresDeRecaudo
{
    public const string CodigoChequeDeTercero = "Receipts.Check.ThirdPartyNotAllowed";
    public const string CodigoChequePosfechado = "Receipts.Check.Postdated";
    public const string CodigoChequeFueraDePlazo = "Receipts.Check.PresentationDeadlinePassed";
    public const string CodigoChequeYaDevuelto = "Receipts.Check.AlreadyReturned";
    public const string CodigoChequeRepetido = "Receipts.Check.Duplicate";

    public static Error ChequeSinDatos() => new("Receipts.Check.DataRequired",
        "Un cheque se registra con banco librado, número, cuenta, fecha y valor.");

    public static Error ChequeDeTercero() => new(CodigoChequeDeTercero,
        "La cooperativa no recibe cheques de terceros (parámetro Caja.AdmiteChequeDeTercero): el girador tiene que ser el pagador.");

    public static Error ChequePosfechado(bool custodiaAdmitida) => new ErrorConDatos(CodigoChequePosfechado,
        custodiaAdmitida
            ? "El cheque es posfechado: no paga un recibo; recíbalo en custodia y regístrelo como medio cuando llegue su fecha."
            : "El cheque es posfechado: no se recibe (la cooperativa no admite cheques en custodia).",
        new { custodyAllowed = custodiaAdmitida });

    public static Error ChequeNoPosfechado() => new("Receipts.Check.NotPostdated",
        "En custodia sólo se recibe un cheque posfechado: uno con fecha de hoy o anterior se registra como medio del recibo.");

    public static Error ChequeFueraDePlazo(DateOnly plazo) => new ErrorConDatos(CodigoChequeFueraDePlazo,
        $"El cheque pasó su plazo de presentación ({plazo:dd/MM/yyyy}) y la cooperativa lo bloquea (Cheques.BloquearFueraDePlazo).", new { deadline = plazo });

    public static Error ChequeRepetido(string numero) => new ErrorConDatos(CodigoChequeRepetido,
        $"El cheque {numero} de esa cuenta y ese banco ya se recibió.", new { check = numero });

    public static Error ChequeYaDevuelto() => new(CodigoChequeYaDevuelto, "El cheque ya está devuelto.");

    public static Error ChequeTransicionInvalida(string desde, string hacia) => new ErrorConDatos("Receipts.Check.InvalidTransition",
        $"Un cheque «{desde}» no pasa a «{hacia}».", new { from = desde, to = hacia });

    public static Error CausalDeDevolucionDesconocida(string codigo) => new ErrorConDatos("Receipts.Check.UnknownReturnCause",
        $"La causal «{codigo}» no está en el catálogo de causales de devolución.", new { cause = codigo });

    public static Error DevolucionRechazadaPorContrato(IReadOnlyList<object> rechazos) => new ErrorConDatos("Receipts.Check.ReturnRejectedByContract",
        "Un módulo rechazó el reverso de una porción pagada con el cheque: no se devolvió nada. Revise las porciones señaladas.", new { rejections = rechazos });
}
