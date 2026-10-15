using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_LoanOperations</c> (feature 013; data-model §7.1; FR-001, FR-009, FR-073, FR-077, FR-082, FR-085, T21, T23): el hecho
/// primario —lo que decidió una persona o el proceso— y <b>el comprobante de la operación</b> (no hay <c>LND_PaymentApplications</c>,
/// CH27); su distribución son las filas del libro con su <c>OperationId</c>. Hecho de sólo inserción (<see cref="IHechoInmutable"/>,
/// <c>bigint</c>) que escribe <c>RegistroDeCartera</c> bajo el cerrojo de T34; sólo <see cref="ReversedByOperationId"/> se escribe una
/// vez. Las columnas <c>NoveltyId</c>, <c>WaiverId</c>, <c>WriteOffBatchItemId</c> (K3, <c>CarteraNovedades</c>) y
/// <c>PayrollDeductionResultId</c> (K2) nacen con su entrega.
/// </summary>
public class LoanOperation : AuditableEntityLong, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>Loan</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; init; }

    public LoanOperationKind Kind { get; init; }

    /// <summary>Comprobante; numerador <c>Operation</c> con ámbito = la clase. Único.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Por crédito, bajo el cerrojo; único con <see cref="LoanId"/>.</summary>
    public int Sequence { get; init; }

    public DateOnly ValueDate { get; init; }

    public DateOnly AccountingDate { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Valor de la operación (bruto del tramo, pagado, ajuste); 0 en las que no mueven dinero.</summary>
    public decimal Amount { get; init; }

    // ---------------------------------------------------------------------------------------------- quién actúa --

    /// <summary>Persona o proceso (T8).</summary>
    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    public Guid? OriginUserCentralId { get; init; }

    // ------------------------------------------------------------------------------------------------ recaudo --

    /// <summary><c>ClaseDeRecaudador</c> del puerto, como texto.</summary>
    public string? CollectorClass { get; init; }

    /// <summary>Código del participante (<c>NOM</c>, <c>TST</c>; <c>CAR</c> en la recogida).</summary>
    public string? CollectorCode { get; init; }

    /// <summary>Clave de negocio del recaudador (T21); única con <see cref="CollectorCode"/> si no es nula.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary><c>HuellaDeOperacion</c>.</summary>
    public string? RequestSha256 { get; init; }

    public PaymentModality? PaymentModality { get; init; }

    public PrepaymentChoice? PrepaymentChoice { get; init; }

    /// <summary>Si la elección salió de la preferencia vigente (D24).</summary>
    public bool? ChoiceFromPreference { get; init; }

    /// <summary><c>ClaseDeMedioDeRecaudo</c> del puerto, como texto (FR-082).</summary>
    public string? PaymentMeansClass { get; init; }

    /// <summary>Deudor, codeudor, pagaduría, aseguradora u otro (FR-085).</summary>
    public int? PayerPersonId { get; init; }

    /// <summary>Recibo del recaudador, sólo como referencia (FR-077).</summary>
    public string? ReceiptNumber { get; init; }

    public DateOnly? ReceiptDate { get; init; }

    public Guid? ReceiptBranchPublicId { get; init; }

    public string? ReceiptMeansReference { get; init; }

    /// <summary>K2: la línea «descontado pendiente de giro» que cubre (FR-085). Dato, sin FK.</summary>
    public string? PaymasterPendingLineReference { get; init; }

    /// <summary>
    /// K2 (<c>CarteraLibranza</c>; data-model §24.8): el resultado «descontado pendiente de giro» que este pago de la pagaduría gira. Un pago
    /// vigente (no reversado) que lo nombra levanta el pendiente; reversarlo lo vuelve a dejar.
    /// </summary>
    public int? PayrollDeductionResultId { get; init; }

    /// <summary>Huella de la simulación en la forma interactiva (T32).</summary>
    public string? SimulationId { get; init; }

    /// <summary>Excedente devuelto al recaudador; nunca entra al libro (FR-076).</summary>
    public decimal? ExcessAmount { get; init; }

    /// <summary>Firmeza: <see cref="ValueDate"/> + <c>Pagos.FirmezaDiasPorClaseDeMedio</c> hábiles (D23).</summary>
    public DateOnly? FinalityDate { get; init; }

    // ------------------------------------------------------------------------------------------------- enlaces --

    /// <summary>La operación que esta reversa; única si no es nula.</summary>
    public long? ReversesOperationId { get; init; }

    /// <summary>El reverso que la anuló; se escribe una sola vez.</summary>
    [EscrituraUnica]
    public long? ReversedByOperationId { get; set; }

    /// <summary>Recogida → desembolso; aplicación de saldo a favor → su resolución; nuevo giro → desembolso reversado.</summary>
    public long? RelatedOperationId { get; init; }

    public DisbursementForm? DisbursementForm { get; init; }

    public int? TrancheNumber { get; init; }

    /// <summary>Mensaje consumido que la originó (venta, ajuste, resultado del giro).</summary>
    public int? InboundMessageId { get; init; }

    /// <summary>Carga de migración (<c>OpeningBalance</c>).</summary>
    public int? MigrationLoadId { get; init; }

    /// <summary>Reclamación cerrada por el pago de la aseguradora (T102).</summary>
    public int? InsuranceClaimId { get; init; }

    /// <summary>K3 (<c>CarteraNovedades</c>; data-model §7.1): la novedad que aplica esta operación <c>TermsChange</c>.</summary>
    public int? NoveltyId { get; init; }

    /// <summary>K3: la condonación que aplica esta operación <c>Waiver</c>.</summary>
    public int? WaiverId { get; init; }

    /// <summary>K3: el ítem del lote de castigo que aplica esta operación <c>WriteOff</c>.</summary>
    public int? WriteOffBatchItemId { get; init; }

    // ------------------------------------------------------------------------------------------------ decisión --

    /// <summary>Obligatorio en reversos, cargos y cambios de preferencia.</summary>
    public string? Reason { get; init; }

    /// <summary>JSON canónico de las entradas de la decisión: modalidad, valor, fecha valor, elección, condiciones, tasa, ajuste.</summary>
    public string Payload { get; init; } = "{}";

    public string InputsSha256 { get; init; } = string.Empty;

    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>Sello de la validación previa contable (T56) como texto: <c>Postable</c>, <c>NotPostable</c>, <c>NoResponse</c>.</summary>
    public string? PrevalidationOutcome { get; init; }
}
