using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;

/// <summary>
/// <c>LND_WithholdingAccumulations</c> (feature 014, US5, T130; data-model §4.6; FR-063, FR-104, FR-105, SC-016): los rendimientos y la retención <b>por
/// titular, año y concepto</b> para el certificado tributario, como una fila por liquidación y parte —no un contador: el acumulado es la suma—, repartida por
/// la proporción de cada titular y cotitular vigente. El reverso de una liquidación escribe el espejo con <see cref="ReversesAccumulationId"/>. La escribe
/// sólo <c>RegistroDeCuentas</c>. Hecho de sólo inserción, sin diferencias de auditoría (§0.8). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class WithholdingAccumulation : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Origen de una fila que escribe una liquidación.</summary>
    public const string OrigenLiquidacion = "Settlement";

    /// <summary>Origen del año en curso que trae la migración (FR-113).</summary>
    public const string OrigenMigracion = "Migration";

    /// <summary>El titular o cotitular.</summary>
    public int PersonId { get; init; }

    public short TaxYear { get; init; }

    public int MemberAccountId { get; init; }

    /// <summary>El concepto del certificado: rendimientos de ahorro, de CDAT…</summary>
    public DepositClass? DepositClass { get; init; }

    public int? InterestSettlementId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado con la liquidación todavía sin Id.</summary>
    public InterestSettlement? InterestSettlement { get; init; }

    public long OperationId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado con la operación todavía sin Id.</summary>
    public MemberAccountOperation? Operation { get; init; }

    /// <summary>La proporción aplicada (1 si hay un solo titular).</summary>
    public decimal Share { get; init; }

    public decimal GrossInterest { get; init; }

    public decimal InflationaryComponentAmount { get; init; }

    public decimal WithholdingBase { get; init; }

    public decimal WithholdingAmount { get; init; }

    public long? ReversesAccumulationId { get; init; }

    /// <summary><see cref="OrigenLiquidacion"/> o <see cref="OrigenMigracion"/>.</summary>
    public string Origin { get; init; } = OrigenLiquidacion;
}
