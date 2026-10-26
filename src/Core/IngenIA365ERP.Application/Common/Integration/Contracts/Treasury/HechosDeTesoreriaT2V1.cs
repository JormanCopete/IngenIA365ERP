using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

// Feature 015, T2 (fases 41 a 44; FR-128 a FR-133, FR-218; contracts/contabilidad.md §4, §5, §8): los hechos de la Tesorería ampliada. Ninguno lleva una cuenta: roles y
// dimensiones por código, cada línea de Banco con su cuenta propia por PublicId y el banco, la red o el proveedor como tercero donde la cuenta lo exija. (nuevo)

/// <summary>Los roles de Tesorería que llegan con T2 (contabilidad.md §5). (nuevo)</summary>
public static class RolesDeTesoreriaT2
{
    public const string ComisionesBancarias = "ComisionesBancarias";
    public const string InteresesBancarios = "InteresesBancarios";
    public const string IvaDescontable = "IvaDescontable";
    public const string RetencionesAFavor = "RetencionesAFavor";

    /// <summary>Impuestos y otros cargos del banco que no son GMF ni comisión (nota bancaria «impuesto» u «otra»). (nuevo; no estaba en contabilidad.md §5)</summary>
    public const string OtrosCargosBancarios = "OtrosCargosBancarios";
}

/// <summary>
/// <c>NotaBancariaRegistrada</c> v1 (<c>NBR</c>; FR-130, FR-218): la nota bancaria aprobada. GMF: <c>GmfGasto</c> → <c>Banco</c>; comisión: <c>ComisionesBancarias</c> +
/// <c>IvaDescontable</c> → <c>Banco</c>; intereses: <c>Banco</c> + <c>RetencionesAFavor</c> → <c>InteresesBancarios</c> (en bruto); impuesto u otra:
/// <c>OtrosCargosBancarios</c> → <c>Banco</c>. Con el banco como tercero; el comprobante sugerido es <c>TB</c>. En línea. (nuevo)
/// </summary>
public sealed record NotaBancariaRegistradaV1
{
    public const string Type = "NotaBancariaRegistrada";

    public Guid BankNotePublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid BankAccountPublicId { get; init; }

    public string Kind { get; init; } = string.Empty;

    public DateOnly NoteDate { get; init; }

    public decimal Amount { get; init; }

    public decimal BaseAmount { get; init; }

    public decimal VatAmount { get; init; }

    public decimal WithholdingAmount { get; init; }

    public Guid? StatementLinePublicId { get; init; }

    public Guid? ThirdPartyPersonPublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public UserRefV1? ApprovedBy { get; init; }

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}
