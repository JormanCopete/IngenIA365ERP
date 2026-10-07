using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Transactions;

/// <summary>
/// <c>LND_LoanTransactions</c> (feature 013; data-model §5.1; FR-001, FR-002, FR-012, Principio XI, T23, T25, T33): el libro de
/// cartera, una fila por concepto y cuota con tres fechas. Saldo de un concepto = Σ de sus filas. Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>) y <c>bigint</c> (<see cref="AuditableEntityLong"/>) con un solo escritor, <c>RegistroDeCartera</c>.
/// Corregir es una fila espejo con <see cref="ReversesTransactionId"/> o una diferencia nueva; un espejo no se espeja. La escribe
/// exactamente una operación o una corrida (<c>CK_LND_LoanTransactions_Origin</c>). <b>No guarda dimensiones</b> (T58, C51).
/// </summary>
public class LoanTransaction : AuditableEntityLong, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>Loan</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; init; }

    /// <summary>La operación que la escribió (<c>LND_LoanOperations</c>); excluyente con <see cref="ProcessRunId"/>.</summary>
    public long? OperationId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>LoanOperation</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Operations.LoanOperation? Operation { get; init; }

    /// <summary>La corrida que la escribió (causación, anticipos al vencer, <c>NationalValueCorrection</c>); excluyente con <see cref="OperationId"/>.</summary>
    public int? ProcessRunId { get; init; }

    public LoanTransactionKind Kind { get; init; }

    public LoanConcept Concept { get; init; }

    /// <summary>Nulo: capital aún no asignado a cuota, anticipo general, saldo a favor, interés de gracia por distribuir.</summary>
    public int? InstallmentNumber { get; init; }

    /// <summary>Con signo y distinto de cero (<c>CK_LND_LoanTransactions_Amount</c>); en <c>Advance</c> y <c>CreditBalance</c>, + es lo que Cartera le debe al deudor.</summary>
    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    public DateOnly AccountingDate { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Fila espejo de un reverso (FR-002); única: un espejo por fila.</summary>
    public long? ReversesTransactionId { get; init; }

    /// <summary>Interés causado de un castigado, en control (T30; K3).</summary>
    public bool InControl { get; init; }

    /// <summary>El hecho que la llevó (bandeja, sin FK; T12, T14); nulo sólo en <c>OpeningBalance</c> (FR-140).</summary>
    public Guid? MessagePublicId { get; init; }
}
