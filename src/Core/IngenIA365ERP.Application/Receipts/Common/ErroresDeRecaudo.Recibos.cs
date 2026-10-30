using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US3 (T0113–T0117; data-model §21; DT §2.7): los errores de la operación de caja y el recibo. Los de data-model §21 conservan su código; los
// demás nacen aquí (nuevo).
public static partial class ErroresDeRecaudo
{
    // data-model §21
    public const string CodigoMediosNoCuadran = "Receipts.Receipt.MeansTotalMismatch";
    public const string CodigoVueltasSoloEnEfectivo = "Receipts.Receipt.ChangeOnlyInCash";
    public const string CodigoItemRechazado = "Receipts.Receipt.ItemRejected";
    public const string CodigoDistribucionCambiada = "Receipts.Receipt.DistributionChanged";
    public const string CodigoExcedenteSinAsignar = "Receipts.Receipt.SurplusNotAllocated";
    public const string CodigoEleccionRequerida = "Receipts.Receipt.ChoiceRequired";
    public const string CodigoFacturaRequerida = "Receipts.Receipt.InvoiceRequired";
    public const string CodigoFechaAnteriorNoAdmitida = "Receipts.Receipt.BackdatedNotAllowed";
    public const string CodigoFechaAnteriorConAprobacion = "Receipts.Receipt.BackdatedNeedsApproval";
    public const string CodigoBonoUsado = "Receipts.PaymentMeans.VoucherAlreadyUsed";

    /// <summary>Feature 015, R1 (T0357; FR-024, FR-031) (nuevo): el pago con tarjeta sin datáfono del adquirente, lote o aprobación.</summary>
    public const string CodigoCapturaDeTarjetaIncompleta = "Receipts.PaymentMeans.CardCaptureIncomplete";

    /// <summary>Feature 015, R1 (T0357; FR-024) (nuevo): la referencia de una tarjeta parece el número de la tarjeta, que nunca se guarda.</summary>
    public const string CodigoNumeroDeTarjeta = "Receipts.PaymentMeans.CardNumberNotAllowed";

    /// <summary>Feature 015, R1 (T0357; FR-041) (nuevo): el bono sin número, o el medio que exige referencia sin ella.</summary>
    public const string CodigoReferenciaRequerida = "Receipts.PaymentMeans.ReferenceRequired";
    public const string CodigoListaVinculante = "Receipts.Compliance.BindingListMatch";
    public const string CodigoDeclaracionRequerida = "Receipts.Compliance.DeclarationRequired";
    public const string CodigoQuienEfectuaRequerido = "Receipts.Compliance.DeliveredByRequired";

    // (nuevo, DT §2.7)
    public const string CodigoOperacionNoEnPreparacion = "Receipts.CashOperation.NotPreparing";
    public const string CodigoOperacionSinItems = "Receipts.CashOperation.NoItems";
    public const string CodigoOperacionRetenida = "Receipts.CashOperation.Withheld";
    public const string CodigoAvisoNoAceptado = "Receipts.Receipt.AdvanceNoticeRequired";
    public const string CodigoImputacionInvalida = "Receipts.Receipt.ImputationInvalid";
    public const string CodigoConceptoInvalido = "Receipts.Receipt.MiscConceptInvalid";
    public const string CodigoCruceRequerido = "Receipts.Receipt.CrossDocumentRequired";
    public const string CodigoTerceroRequerido = "Receipts.Receipt.ThirdPartyRequired";
    public const string CodigoMedioNoOfrecido = "Receipts.Receipt.MeansNotOffered";
    public const string CodigoNoContabilizable = "Receipts.Accounting.NotPostable";
    public const string CodigoPersonaNoEncontrada = "Receipts.Payables.PersonNotFound";

    public static Error MediosNoCuadran() => new(CodigoMediosNoCuadran,
        "Los medios no cubren lo que se va a aplicar: el total de medios tiene que ser lo aplicado más las vueltas.");

    public static Error VueltasSoloEnEfectivo() => new(CodigoVueltasSoloEnEfectivo,
        "Las vueltas sólo salen del efectivo: un medio que no es efectivo no puede pasar de lo que cubre.");

    public static Error ImputacionInvalida() => new(CodigoImputacionInvalida,
        "La imputación fijada no cuadra: cada aplicación se cubre exacto y ningún medio se usa de más ni en cero.");

    /// <summary>Uno o más ítems rechazados por su módulo; <paramref name="items"/> trae por cada uno su identificador, código y mensaje.</summary>
    public static Error ItemRechazado(IReadOnlyList<object> items) => new ErrorConDatos(CodigoItemRechazado,
        "Uno o más pagos no se pudieron aplicar: revise cada uno; no se registró nada.", new { items });

    public static Error DistribucionCambiada(IReadOnlyList<object> simulaciones) => new ErrorConDatos(CodigoDistribucionCambiada,
        "La distribución cambió desde la simulación: revísela y confirme otra vez con la misma operación.", new { simulations = simulaciones });

    public static Error ExcedenteSinAsignar(decimal excedente) => new ErrorConDatos(CodigoExcedenteSinAsignar,
        $"Sobran {excedente:N0} que ninguna aplicación recibe: ajuste el valor o asígnelo a otra aplicación.", new { surplus = excedente });

    public static Error EleccionRequerida(Guid item) => new ErrorConDatos(CodigoEleccionRequerida,
        "El abono extraordinario exige la elección del deudor: reducir el plazo o reducir la cuota (Ley 2032 de 2020).", new { itemId = item });

    public static Error AvisoNoAceptado(Guid item) => new ErrorConDatos(CodigoAvisoNoAceptado,
        "El pago anticipado no abona a capital: el deudor tiene que aceptar el aviso expresamente.", new { itemId = item });

    public static Error FacturaRequerida(string concepto) => new ErrorConDatos(CodigoFacturaRequerida,
        $"El concepto {concepto} exige la factura.", new { concept = concepto });

    public static Error ConceptoInvalido() => new(CodigoConceptoInvalido, "El concepto vario no existe, está inactivo o es de egreso.");

    public static Error CruceRequerido(string concepto) => new ErrorConDatos(CodigoCruceRequerido,
        $"El concepto {concepto} exige el documento cruce.", new { concept = concepto });

    public static Error TerceroRequerido(string concepto) => new ErrorConDatos(CodigoTerceroRequerido,
        $"El concepto {concepto} exige el tercero.", new { concept = concepto });

    public static Error FechaAnteriorNoAdmitida(int maxDias) => new ErrorConDatos(CodigoFechaAnteriorNoAdmitida,
        maxDias == 0
            ? "La caja no admite fecha valor anterior a hoy."
            : $"La fecha valor no puede ser anterior en más de {maxDias} día(s).", new { maxDays = maxDias });

    public static Error FechaAnteriorConAprobacion() => new(CodigoFechaAnteriorConAprobacion,
        "La fecha valor anterior exige motivo, soporte y la aprobación de otra persona presente.");

    public static Error CapturaDeTarjetaIncompleta(string medio, string falta) => new ErrorConDatos(CodigoCapturaDeTarjetaIncompleta,
        $"El pago con {medio} necesita {falta}: tómelo del comprobante del datáfono.", new { means = medio, missing = falta });

    public static Error NumeroDeTarjeta(string medio) => new ErrorConDatos(CodigoNumeroDeTarjeta,
        $"La referencia del pago con {medio} parece el número de la tarjeta: registre sólo el lote y la aprobación, nunca el número.", new { means = medio });

    public static Error ReferenciaRequerida(string medio) => new ErrorConDatos(CodigoReferenciaRequerida,
        $"El medio {medio} exige su número o referencia.", new { means = medio });

    public static Error BonoUsado(string bono) => new ErrorConDatos(CodigoBonoUsado, $"El bono {bono} ya se usó en otro recibo.", new { voucher = bono });

    public static Error ListaVinculante() => new(CodigoListaVinculante,
        "La persona coincide con una lista vinculante: la operación queda bloqueada y se informa al oficial de cumplimiento.");

    public static Error DeclaracionRequerida(decimal efectivo) => new ErrorConDatos(CodigoDeclaracionRequerida,
        $"El efectivo de la operación ({efectivo:N0}) exige la declaración de origen de fondos con su soporte.", new { cash = efectivo });

    public static Error QuienEfectuaRequerido() => new(CodigoQuienEfectuaRequerido, "Registre quién efectúa el pago en la ventanilla.");

    /// <summary>
    /// Decisión del dueño del 2026-10-24 (T0115): quien efectúa es obligatorio con el efectivo en el umbral de la transacción individual o cuando el pagador no
    /// es el titular; <c>data.reason</c> dice cuál (<c>CashThreshold</c>, <c>PayerIsNotHolder</c>) y <c>data.threshold</c> el umbral vigente.
    /// </summary>
    public static Error QuienEfectuaRequerido(string motivo, decimal? umbral) => new ErrorConDatos(CodigoQuienEfectuaRequerido,
        motivo == "CashThreshold"
            ? $"El efectivo del recibo llega al umbral de la transacción individual ({umbral:N0}): registre quién efectúa el pago en la ventanilla."
            : "Quien paga no es el titular de lo que se paga: registre quién efectúa el pago en la ventanilla.",
        new { reason = motivo, threshold = umbral });

    public static Error OperacionNoEnPreparacion() => new(CodigoOperacionNoEnPreparacion, "La operación ya no está en preparación.");

    public static Error OperacionSinItems() => new(CodigoOperacionSinItems, "Elija al menos un pago.");

    public static Error OperacionRetenida() => new(CodigoOperacionRetenida,
        "La operación está retenida por una coincidencia con listas: espere la liberación del oficial de cumplimiento.");

    public static Error MedioNoOfrecido() => new(CodigoMedioNoOfrecido, "Ese medio no está sellado en la sesión de caja.");

    /// <summary>
    /// La caja no confirma: falta la regla contable de una combinación (US8-1; contabilidad.md §6). <c>data.errors[] { factType, role, combination, whoFixes, detail }</c>,
    /// como <c>Treasury.Prevalidation.NotPostable</c>; hasta la corrida e2e del 2026-10-27 iba como <c>data.missing</c>, sin la combinación ni quién la completa.
    /// </summary>
    public static Error NoContabilizable(IReadOnlyList<IngenIA365ERP.Application.Common.Integration.Accounting.CombinacionSinReglaDto> faltan) => new ErrorConDatos(
        CodigoNoContabilizable,
        "Falta la regla contable de lo que se va a registrar: pida a Contabilidad que la parametrice.",
        new
        {
            errors = faltan.Select(f => new { factType = f.FactType, role = f.Role, combination = f.Combination ?? f.Detail, whoFixes = "Contabilidad", detail = f.Detail })
                .ToList(),
        });

    public static Error PersonaNoEncontrada() => new(CodigoPersonaNoEncontrada, "No se encontró la persona con ese documento, código o número de obligación.");
}
