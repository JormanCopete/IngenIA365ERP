using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;

/// <summary>
/// <c>LND_MembershipWithdrawalLines</c> (feature 014, US10, T096; data-model §6.2; FR-076 a FR-078): una línea de la liquidación de una versión del
/// retiro —saldo de aportes por clase, saldo de ahorro por cuenta, cruce contra un crédito, descuento por pérdidas y devolución por cuenta— con las
/// operaciones que la aplicaron. Signo: <c>+</c> a favor del asociado, <c>−</c> lo que se descuenta; Σ de las líneas que no son <c>Refund</c> de la
/// versión = <c>RefundAmount</c> (research E65). Hecho de sólo inserción (<see cref="IHechoInmutable"/>): una reliquidación escribe otra versión. (nuevo)
/// </summary>
public class MembershipWithdrawalLine : AuditableEntity, IHechoInmutable
{
    public const int LargoDelConcepto = 30;
    public const int LargoDeLaNota = 300;

    public int MembershipWithdrawalId { get; init; }

    public int SettlementVersion { get; init; }

    public int LineNumber { get; init; }

    public MembershipWithdrawalLineKind Kind { get; init; }

    public int? MemberAccountId { get; init; }

    public int? LoanId { get; init; }

    /// <summary><c>MemberAccountConcept</c> o <c>LoanConcept</c> por nombre.</summary>
    public string? Concept { get; init; }

    /// <summary>Con signo: + a favor del asociado, − lo que se descuenta. Las de devolución, en positivo (informativas).</summary>
    public decimal Amount { get; init; }

    /// <summary>La operación de cuenta que la aplicó (el cruce, la pérdida o la devolución).</summary>
    public long? AccountOperationId { get; init; }

    /// <summary>El pago del crédito (<c>OwnLoanPayoff</c>) del cruce.</summary>
    public long? LoanOperationId { get; init; }

    public string? Notes { get; init; }
}
