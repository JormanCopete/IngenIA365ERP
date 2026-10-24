using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US3 (T0116; FR-141; contracts/contabilidad.md §3): el hecho del recibo de caja. Sin cuentas: roles y dimensiones por código.

/// <summary>Una aplicación del recibo en el hecho: el producto (o el concepto), su módulo y naturaleza, y su valor. (nuevo)</summary>
public sealed record AplicacionDeReciboV1
{
    public int Ordinal { get; init; }

    /// <summary><c>Loan</c>, <c>Account</c>, <c>MiscConcept</c>.</summary>
    public string Target { get; init; } = string.Empty;

    public Guid? ProductPublicId { get; init; }

    public string? ProductNumber { get; init; }

    public string? MiscConceptCode { get; init; }

    public Guid BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public string? ModuleVoucherNumber { get; init; }
}

/// <summary>
/// <c>ReciboEmitido</c> v1 (<c>RCE</c>): el recibo de caja, con una línea por porción de medio —débito al rol del medio (<c>Caja</c>, <c>BancoRecaudo</c>,
/// <c>ChequesEnCanje</c>, <c>MedioPorCobrar</c>) y crédito al puente del módulo (<c>PuenteDelModulo</c> por <c>TargetModule</c>, <c>TargetNature</c>,
/// <c>CollectorClass</c>) o al <c>ConceptoVario</c>—. Pasa por lotes resumidos por día, punto y medio (D25). (nuevo)
/// </summary>
public sealed record ReciboEmitidoV1
{
    public const string Type = "ReciboEmitido";

    public Guid ReceiptPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public DateOnly ValueDate { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public Guid PayerPersonPublicId { get; init; }

    public bool PayerIsThirdParty { get; init; }

    public Guid? SessionPublicId { get; init; }

    public decimal TotalApplied { get; init; }

    public decimal ChangeGiven { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<AplicacionDeReciboV1> Applications { get; init; } = [];

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
