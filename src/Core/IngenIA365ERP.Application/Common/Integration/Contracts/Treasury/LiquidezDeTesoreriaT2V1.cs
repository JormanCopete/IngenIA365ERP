using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

// Feature 015, T2 (fase 43, T0461; E-02 e; FR-139; contratos-v2.md §3.5; mensajes.md): el saldo de las cuentas del fondo de liquidez que Tesorería informa a Captaciones.

/// <summary>Una cuenta propia del fondo de liquidez con su saldo en libros a la fecha y el último extracto cargado. (nuevo)</summary>
public sealed record CuentaDelFondoV1
{
    public Guid OwnBankAccountPublicId { get; init; }

    public string BankCode { get; init; } = string.Empty;

    public string AccountNumber { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public DateOnly? StatementDate { get; init; }
}

/// <summary>
/// <c>SaldoDelFondoDeLiquidez</c> v1 (<c>TES → Lending</c>; E-02 e; FR-139): las cuentas marcadas como fondo de liquidez y su saldo a la fecha. Nace con la foto de la
/// posición (<c>TES_LiquiditySnapshots</c>) que toma la persona que concilia o el tesorero; origen la foto, clave <c>Confirmation:{foto:N}</c>, modo <c>Always</c>. (nuevo)
/// </summary>
public sealed record SaldoDelFondoDeLiquidezV1
{
    public const string Type = "SaldoDelFondoDeLiquidez";

    public Guid SnapshotPublicId { get; init; }

    public DateOnly Date { get; init; }

    public IReadOnlyList<CuentaDelFondoV1> Accounts { get; init; } = [];

    public decimal Total { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();
}
