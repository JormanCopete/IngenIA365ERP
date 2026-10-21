using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_Loans</c> (feature 013; data-model §4.1; FR-001, FR-045, FR-050–FR-056, FR-069, FR-139–FR-143): el crédito u obligación.
/// <b>No guarda saldos</b> (FR-001, I-6): sólo identidad, origen, estado y marcas de origen. No es hecho: <see cref="Status"/> y las
/// fechas de estado las mueve sólo <c>RegistroDeCartera</c> en la transacción de la operación que causa la transición (§8.1).
/// Nunca se da de baja: «anulado» es un estado. Nace siempre con su <c>LND_LoanControls</c>. Forma sin comportamiento.
/// </summary>
public class Loan : AuditableEntity
{
    /// <summary>Del numerador, de la reserva o el de origen en la migración; único sin filtro.</summary>
    public string Number { get; set; } = string.Empty;

    public int DebtorPersonId { get; set; }

    public DebtorKind DebtorKind { get; set; }

    /// <summary>Oficina; su sucursal contable va en el sobre de cada hecho (T13). Hasta K3 no cambia (C51).</summary>
    public int BranchId { get; set; }

    public int CreditLineId { get; set; }

    public LoanOrigin Origin { get; set; }

    public LoanStatus Status { get; set; }

    /// <summary>Solicitud de la que nació (sólo en <c>Origination</c>).</summary>
    public int? ApplicationId { get; set; }

    /// <summary>Total aprobado (tramos); en Comercial, el valor del pago a crédito; en migración, el monto original.</summary>
    public decimal ApprovedAmount { get; set; }

    /// <summary>Fecha valor del primer desembolso, de la venta o del corte.</summary>
    public DateOnly OpenedOn { get; set; }

    /// <summary>Reserva de número usada (Comercial).</summary>
    public int? ReservationId { get; set; }

    /// <summary>Cupo rotativo consumido.</summary>
    public int? CreditLimitId { get; set; }

    /// <summary>Venta de Comercial (dato, sin FK).</summary>
    public Guid? SourceSalesDocumentPublicId { get; set; }

    /// <summary>Pago a crédito de Comercial (dato, sin FK); único si no es nulo: ninguna venta origina dos obligaciones (FR-055).</summary>
    public Guid? SourcePaymentPublicId { get; set; }

    /// <summary>Factura; la conciliación de FR-143 cruza por ella.</summary>
    public string? SourceInvoiceNumber { get; set; }

    /// <summary>Mensaje de venta que la creó (bandeja, sin FK).</summary>
    public Guid? SourceMessagePublicId { get; set; }

    /// <summary>Lo que Comercial selló: <c>Cartera</c> o <c>Contabilidad</c> (FR-056).</summary>
    public string? AccountsReceivableRecordedBy { get; set; }

    public SaleObligationStatus? SaleObligationStatus { get; set; }

    /// <summary>Marca «validación fallida» (FR-052); no altera causación, cobro ni calificación.</summary>
    public string? ValidationFailureCode { get; set; }

    public string? ValidationFailureReason { get; set; }

    /// <summary>Confirmación por excepción con atribución (sujeto <c>ValidationFailureException</c>).</summary>
    public DateTime? ConfirmedByExceptionAt { get; set; }

    public int? ConfirmedByExceptionDecisionId { get; set; }

    /// <summary>Crédito de origen: reemplazo de Comercial (K1), novación o refinanciación (K3).</summary>
    public int? OriginLoanId { get; set; }

    public LoanLinkKind? OriginLinkKind { get; set; }

    /// <summary>Carga de migración de la que nació.</summary>
    public int? MigrationLoadId { get; set; }

    public DateOnly? PaidOffOn { get; set; }

    public DateOnly? WrittenOffOn { get; set; }

    public DateOnly? NovatedOn { get; set; }

    public DateOnly? VoidedOn { get; set; }

    public string? VoidReason { get; set; }
}
