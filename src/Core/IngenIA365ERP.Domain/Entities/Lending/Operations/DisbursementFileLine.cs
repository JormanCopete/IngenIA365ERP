using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementFileLines</c> (feature 013; data-model §7.7; FR-047): una línea del archivo de desembolsos, con los datos de la
/// orden sellada y el registro exacto que se escribió. Hecho de sólo inserción, hija del archivo (<see cref="IHechoInmutable"/>, C42).
/// </summary>
public class DisbursementFileLine : AuditableEntity, IHechoInmutable
{
    public int FileId { get; init; }

    public int LineNumber { get; init; }

    public int OrderId { get; init; }

    public int BeneficiaryPersonId { get; init; }

    public int? DestinationBankId { get; init; }

    public byte AccountType { get; init; }

    public string AccountNumber { get; init; } = string.Empty;

    /// <summary>Neto de la orden.</summary>
    public decimal Amount { get; init; }

    /// <summary>El registro exacto que se escribió; concepto = número del crédito.</summary>
    public string RecordText { get; init; } = string.Empty;
}
