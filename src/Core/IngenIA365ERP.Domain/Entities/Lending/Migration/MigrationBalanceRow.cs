using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBalances</c> (feature 013, K1; data-model §29.3, parte <c>balances</c>): un saldo al corte por concepto (suspenso,
/// anticipos y saldos a favor incluidos). Hecho de alto volumen (<see cref="AuditableEntityLong"/>, sin diff de auditoría). Al
/// activar nace la operación <c>OpeningBalance</c> con sus transacciones por concepto, fechadas al corte y <b>sin hechos</b> (FR-140).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationBalanceRow : AuditableEntityLong, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public LoanConcept Concept { get; init; }

    /// <summary>Cuota a la que pertenece el saldo; nula si es del crédito.</summary>
    public int? InstallmentNumber { get; init; }

    public decimal Amount { get; init; }
}
