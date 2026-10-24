using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, R1 (contracts/contabilidad.md §3; mensajes.md §2.5): los hechos de Recaudo que trae la entrega R1, con la forma común (líneas por rol, sin cuentas).
// Cada fase suma los suyos: la 25 el recaudo pendiente de aplicar. (nuevo)

/// <summary>
/// <c>RecaudoPendienteReconocido</c> v1 (<c>RPR</c>; FR-046, D14): el dinero de un recibo que no pudo ir a su contrato porque el módulo no está activado y la persona está
/// en una carga aplicada sin activar: rol del medio (caja, banco, medio por cobrar) → <c>RecaudoPendienteDeAplicar</c> con la persona como tercero. En línea. (nuevo)
/// </summary>
public sealed record RecaudoPendienteReconocidoV1
{
    public const string Type = "RecaudoPendienteReconocido";

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public Guid PendingApplicationPublicId { get; init; }

    public Guid PersonPublicId { get; init; }

    /// <summary><c>Loan</c> o <c>Account</c>.</summary>
    public string TargetModule { get; init; } = string.Empty;

    public string? ProductReference { get; init; }

    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>RecaudoPendienteAplicado</c> v1 (<c>RPA</c>; FR-046): el pendiente aplicado por el contrato del módulo ya activado: <c>RecaudoPendienteDeAplicar</c> →
/// <c>PuenteDelModulo</c>. En línea. (nuevo)
/// </summary>
public sealed record RecaudoPendienteAplicadoV1
{
    public const string Type = "RecaudoPendienteAplicado";

    public Guid PendingApplicationPublicId { get; init; }

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public Guid PersonPublicId { get; init; }

    public Guid ProductPublicId { get; init; }

    public string? ProductNumber { get; init; }

    public string? ModuleVoucherNumber { get; init; }

    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    public UserRefV1 AppliedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
