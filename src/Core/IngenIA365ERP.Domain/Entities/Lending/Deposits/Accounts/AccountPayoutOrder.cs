using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_AccountPayoutOrders</c> (feature 014, K7; data-model §3.9; FR-079, FR-083, FR-091; research C05): la orden de giro a Tesorería de un
/// retiro por transferencia, una devolución, una entrega, intereses de CDAT a terceros, una redención, una consignación a juzgado o una
/// cancelación. Mismo mensaje que el desembolso (<c>OrdenDeGiroEmitida</c> v2) y mismos estados (<see cref="DisbursementOrderStatus"/>); tabla
/// propia porque <c>LND_DisbursementOrders</c> exige crédito. Beneficiario y cuenta destino sellados (I-1). Una orden por operación. Una orden
/// rechazada o anulada <b>reintegra</b> el saldo con una operación de reverso enlazada y la cuenta vuelve de <c>Closing</c> a su estado
/// anterior. No es hecho: el estado lo mueve el <c>ResultadoDelGiro</c> consumido.
///
/// <para><c>GarnishmentId</c> (K8) y <c>DeceasedHolderCaseId</c> (K10) nacen con sus tablas; <see cref="MembershipWithdrawalId"/> es de K7 y su
/// FK nace con <c>LND_MembershipWithdrawals</c> (US10, T096).</para>
/// </summary>
public class AccountPayoutOrder : AuditableEntity
{
    public const string CodigoResultadoInvalido = "Lending.PayoutOrder.InvalidResult";

    public int MemberAccountId { get; set; }

    /// <summary>La operación que debitó la cuenta (<c>LND_MemberAccountOperations</c>, bigint); una orden por operación.</summary>
    public long OperationId { get; set; }

    /// <summary>Numerador <c>DisbursementOrder</c> (013); único.</summary>
    public string Number { get; set; } = string.Empty;

    public PayoutPurpose Purpose { get; set; }

    public int BeneficiaryPersonId { get; set; }

    /// <summary>Copia tal como se emitió (I-1).</summary>
    public string BeneficiaryTaxIdType { get; set; } = string.Empty;

    public string BeneficiaryTaxId { get; set; } = string.Empty;

    public string BeneficiaryName { get; set; } = string.Empty;

    public int? DestinationBankId { get; set; }

    public byte? DestinationAccountType { get; set; }

    public string? DestinationAccountNumber { get; set; }

    /// <summary>Mayor que cero (<c>CK_LND_AccountPayoutOrders_Amount</c>).</summary>
    public decimal Amount { get; set; }

    public DisbursementOrderStatus Status { get; set; } = DisbursementOrderStatus.Issued;

    public DateTime? StatusChangedAt { get; set; }

    public DateTime IssuedAt { get; set; }

    /// <summary>Nulo = el proceso (intereses del CDAT a terceros).</summary>
    public int? IssuedByUserId { get; set; }

    /// <summary>Emisión + <c>Giros.DiasSinResultado</c> hábiles; pasado, alerta.</summary>
    public DateOnly ResultDueOn { get; set; }

    /// <summary>K7 (US10, T096): el retiro del asociado que la originó; la FK nace con su tabla.</summary>
    public int? MembershipWithdrawalId { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>Una orden emitida (§3.9): valor positivo y beneficiario sellado. El número lo pone el numerador después.</summary>
    public static AccountPayoutOrder Emitir(int cuentaId, long operacionId, PayoutPurpose proposito, int beneficiarioId, string tipoDeDocumento,
        string documento, string nombre, int? bancoId, byte? tipoDeCuenta, string? cuenta, decimal valor, DateOnly resultadoHasta, DateTime instante,
        int? usuarioId, int? retiroId = null)
    {
        if (valor <= 0m)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoResultadoInvalido, "La orden de giro lleva un valor mayor que cero.", "amount", valor));
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        return new AccountPayoutOrder
        {
            MemberAccountId = cuentaId,
            OperationId = operacionId,
            Purpose = proposito,
            BeneficiaryPersonId = beneficiarioId,
            BeneficiaryTaxIdType = tipoDeDocumento,
            BeneficiaryTaxId = documento,
            BeneficiaryName = nombre,
            DestinationBankId = bancoId,
            DestinationAccountType = tipoDeCuenta,
            DestinationAccountNumber = cuenta,
            Amount = valor,
            Status = DisbursementOrderStatus.Issued,
            IssuedAt = instante,
            IssuedByUserId = usuarioId,
            ResultDueOn = resultadoHasta,
            MembershipWithdrawalId = retiroId,
        };
    }

    /// <summary>¿Cabe el resultado <paramref name="hacia"/>? Desde <c>Issued</c>, girada, rechazada o anulada; cheque no cobrado, sólo girada.</summary>
    public bool AdmiteResultado(DisbursementOrderStatus hacia) => (Status, hacia) switch
    {
        (DisbursementOrderStatus.Issued, DisbursementOrderStatus.Disbursed) => true,
        (DisbursementOrderStatus.Issued, DisbursementOrderStatus.Rejected) => true,
        (DisbursementOrderStatus.Issued, DisbursementOrderStatus.Voided) => true,
        (DisbursementOrderStatus.Disbursed, DisbursementOrderStatus.CheckNotCashed) => true,
        _ => false,
    };

    /// <summary>Los resultados que reintegran el saldo a la cuenta (FR-091).</summary>
    public static bool Reintegra(DisbursementOrderStatus resultado) =>
        resultado is DisbursementOrderStatus.Rejected or DisbursementOrderStatus.CheckNotCashed or DisbursementOrderStatus.Voided;

    /// <summary>Mueve el estado por un resultado admitido.</summary>
    public void RegistrarResultado(DisbursementOrderStatus hacia, DateTime instante)
    {
        if (!AdmiteResultado(hacia))
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoResultadoInvalido, $"La orden en «{Status}» no admite el resultado «{hacia}».",
                new Dictionary<string, object?> { ["reason"] = "InvalidTransition", ["from"] = Status.ToString(), ["to"] = hacia.ToString() }));
        Status = hacia;
        StatusChangedAt = instante;
    }
}
