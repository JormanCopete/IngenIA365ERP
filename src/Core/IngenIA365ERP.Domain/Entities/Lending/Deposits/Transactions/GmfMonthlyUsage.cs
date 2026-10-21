using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;

/// <summary>
/// <c>LND_GmfMonthlyUsage</c> (feature 014, US4, T110; data-model §4.7; FR-022, FR-053, FR-066, R07): el uso del tope exento y el GMF cobrado <b>por
/// persona y mes</b>, como una fila por disposición —no un contador: el acumulado del mes es Σ de sus filas, leído bajo el cerrojo de la persona, que es
/// lo que lo hace consistente entre cuentas—. Lo calcula <c>GravamenMovimientosFinancieros</c> y lo escribe sólo <c>RegistroDeCuentas</c>, en la misma
/// operación que la disposición. El reverso es otra fila con los valores en negativo y <see cref="ReversesUsageId"/>. Con tarifa cero o política «no
/// aplica» desde una fecha no se escriben filas desde esa fecha y lo anterior se conserva. Hecho de sólo inserción, sin diferencias de auditoría (§0.8).
/// (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class GmfMonthlyUsage : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Origen de una fila que escribe una operación.</summary>
    public const string OrigenOperacion = "Operation";

    /// <summary>Origen del acumulado del mes de corte que trae la migración (FR-113).</summary>
    public const string OrigenMigracion = "Migration";

    /// <summary>Largo del código del hecho generador (<c>nvarchar(40)</c>).</summary>
    public const int LargoDelHecho = 40;

    /// <summary>El titular de la cuenta debitada.</summary>
    public int PersonId { get; init; }

    public short Year { get; init; }

    public byte Month { get; init; }

    public int MemberAccountId { get; init; }

    /// <summary>La disposición (o su reverso).</summary>
    public long OperationId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado una fila con su operación todavía sin Id.</summary>
    public MemberAccountOperation? Operation { get; init; }

    /// <summary>El hecho generador según <c>Gmf.HechosGeneradores</c> (<c>HechosGeneradoresDeGmf</c>).</summary>
    public string TaxableEventCode { get; init; } = string.Empty;

    /// <summary>Lo dispuesto.</summary>
    public decimal DisposedAmount { get; init; }

    /// <summary>Lo que usó del tope exento del mes.</summary>
    public decimal ExemptAmount { get; init; }

    public decimal TaxableBase { get; init; }

    public decimal GmfAmount { get; init; }

    /// <summary>La política vigente en la fecha.</summary>
    public GmfExemptionPolicy Policy { get; init; }

    /// <summary><c>Gmf.Tarifa</c> (fracción; puede ser cero).</summary>
    public decimal Rate { get; init; }

    /// <summary>La fila que este reverso espeja.</summary>
    public long? ReversesUsageId { get; init; }

    /// <summary><see cref="OrigenOperacion"/> o <see cref="OrigenMigracion"/>.</summary>
    public string Origin { get; init; } = OrigenOperacion;
}
