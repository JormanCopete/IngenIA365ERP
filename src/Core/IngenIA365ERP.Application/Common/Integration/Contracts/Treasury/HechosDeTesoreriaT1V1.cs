using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

// Feature 015, T1 (fase 32; FR-104, FR-220; contracts/contabilidad.md §4, §5, §8): los hechos de los traslados entre cuentas propias y del cierre del mes de los cheques
// girados, con el tipo de comprobante sugerido TB. Ninguno lleva una cuenta: roles y dimensiones por código, y cada línea de Banco con su cuenta propia por PublicId.

/// <summary>Los roles de Tesorería que llegan con T1 (contabilidad.md §5). (nuevo)</summary>
public static class RolesDeTesoreriaT1
{
    public const string FondosEnTransito = "FondosEnTransito";
    public const string ChequesPorEntregar = "ChequesPorEntregar";
    public const string CuentaPorPagarChequesNoCobrados = "CuentaPorPagarChequesNoCobrados";
}

/// <summary>
/// <c>TrasladoEntreCuentasEnviado</c> v1 (<c>TEE</c>): el traslado entre dos cuentas propias sale de la de origen y queda en tránsito hasta el extracto;
/// <c>FondosEnTransito</c> → <c>Banco</c> de la cuenta de origen. En línea. (nuevo)
/// </summary>
public sealed record TrasladoEntreCuentasEnviadoV1
{
    public const string Type = "TrasladoEntreCuentasEnviado";

    public Guid TransferPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid FromBankAccountPublicId { get; init; }

    public Guid ToBankAccountPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly SentOn { get; init; }

    public string? Reference { get; init; }

    public Guid? LiquidityFundUsePublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>TrasladoConfirmado</c> v1 (<c>TCF</c>): el traslado llegó a la cuenta de destino —por el cruce del extracto o con el soporte del banco—;
/// <c>Banco</c> de la cuenta de destino → <c>FondosEnTransito</c>. En línea, relacionado con el enviado. (nuevo)
/// </summary>
public sealed record TrasladoConfirmadoV1
{
    public const string Type = "TrasladoConfirmado";

    public Guid TransferPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid FromBankAccountPublicId { get; init; }

    public Guid ToBankAccountPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly ConfirmedOn { get; init; }

    public Guid? StatementLinePublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>ChequesReclasificadosAlCierre</c> v1 (<c>CRC</c>; FR-220): los cheques emitidos y no entregados al cierre del mes vuelven al efectivo de la cuenta —<c>Banco</c> →
/// <c>ChequesPorEntregar</c>, fechado el último día del mes— y su reverso (<see cref="IsReversal"/>) los devuelve el primer día del mes siguiente, con las mismas
/// líneas invertidas. Una línea por cheque, con el número como cruce. En línea. (nuevo)
/// </summary>
public sealed record ChequesReclasificadosAlCierreV1
{
    public const string Type = "ChequesReclasificadosAlCierre";

    public Guid MonthEndCheckRunPublicId { get; init; }

    public string RunNumber { get; init; } = string.Empty;

    public int Year { get; init; }

    public int Month { get; init; }

    public Guid BankAccountPublicId { get; init; }

    /// <summary>El reverso del primer día del mes siguiente.</summary>
    public bool IsReversal { get; init; }

    public DateOnly OccurredOn { get; init; }

    public int CheckCount { get; init; }

    public decimal Amount { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>ChequeNoCobradoACuentaPorPagar</c> v1 (<c>CNP</c>; FR-220): un cheque entregado y sin cobrar más allá de <c>Cheques.DiasACuentaPorPagar</c> pasa a cuenta por pagar
/// al beneficiario —<c>Banco</c> → <c>CuentaPorPagarChequesNoCobrados</c>, con el beneficiario como tercero y el número del cheque como cruce—, nunca a ingreso. En línea. (nuevo)
/// </summary>
public sealed record ChequeNoCobradoACuentaPorPagarV1
{
    public const string Type = "ChequeNoCobradoACuentaPorPagar";

    public Guid MonthEndCheckRunPublicId { get; init; }

    public string RunNumber { get; init; } = string.Empty;

    public Guid IssuedCheckPublicId { get; init; }

    public long CheckNumber { get; init; }

    public Guid BankAccountPublicId { get; init; }

    public Guid? BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly DeliveredOn { get; init; }

    public int DaysWithoutCashing { get; init; }

    public DateOnly OccurredOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}
