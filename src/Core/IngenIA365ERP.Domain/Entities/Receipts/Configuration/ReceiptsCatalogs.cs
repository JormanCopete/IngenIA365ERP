using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Configuration;

// Feature 015, US1 (T0070; FR-023, FR-026, FR-027, FR-179, FR-184, FR-186, FR-198; data-model §1.5–§1.10): los catálogos de Recaudo. (nuevo)

/// <summary>
/// <c>RCD_PaymentMeansCollectionPoints</c> (data-model §1.5): un medio del catálogo común de la plataforma (<c>COR_PaymentMeans</c>) que se ofrece en un
/// punto, con su vigencia. Retirar cierra la vigencia; no se borra (un medio con pagos no se borra, FR-024). (nuevo)
/// </summary>
public class PaymentMeansCollectionPoint : AuditableEntity
{
    public int PaymentMeansId { get; set; }

    public int CollectionPointId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly fecha) => VigenciaDeRecaudo.Incluye(ValidFrom, ValidTo, fecha);
}

/// <summary>
/// <c>RCD_DocumentTypes</c> (data-model §1.6; FR-026): un tipo de documento de Recaudo con su clase, su código (la clave de su numeración
/// <c>RCD:{código}:{prefijo}</c> en <c>COR_Sequences</c>) y si usa el prefijo del punto. La semilla deja uno por clase de R0. (nuevo)
/// </summary>
public class ReceiptDocumentType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ReceiptDocumentKind Kind { get; set; }

    public bool UsesPointPrefix { get; set; }

    public ReceiptsCatalogStatus Status { get; set; } = ReceiptsCatalogStatus.Active;
}

/// <summary>
/// <c>RCD_MiscConcepts</c> (data-model §1.7; FR-027, FR-179): un concepto de ingreso o egreso no ligado a un producto. Los del sistema
/// (<see cref="IsSystem"/>: giro devuelto, sobrantes, reintegros, rendimientos) vienen sembrados y no se inactivan; uno gravado exige la factura de
/// Comercial como documento cruce (<see cref="RequiresInvoice"/>). Los egresos varios nacen apagados (R1, FR-055). (nuevo)
/// </summary>
public class MiscConcept : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public MiscConceptDirection Direction { get; set; } = MiscConceptDirection.Income;

    public bool RequiresThirdParty { get; set; }

    public bool RequiresCrossDocument { get; set; }

    /// <summary>No se cobra sin la factura electrónica de Comercial como documento cruce (FR-179).</summary>
    public bool RequiresInvoice { get; set; }

    public bool RequiresApproval { get; set; }

    /// <summary>El valor máximo que admite (Monto, opcional).</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>Sembrado por el sistema: no se inactiva.</summary>
    public bool IsSystem { get; set; }

    public ReceiptsCatalogStatus Status { get; set; } = ReceiptsCatalogStatus.Active;
}

/// <summary>
/// <c>RCD_ContingencyBooks</c> (data-model §1.8; FR-186): un talonario prenumerado de un punto para cuando la caja no puede operar. Los rangos de un punto no
/// se cruzan; los números usados se ven por <c>RCD_Receipts.ContingencyNumber</c>. (nuevo)
/// </summary>
public class ContingencyBook : AuditableEntity
{
    public int CollectionPointId { get; set; }

    public int FromNumber { get; set; }

    public int ToNumber { get; set; }

    /// <summary>Quien lo registró (nombre del actor).</summary>
    public string RegisteredBy { get; set; } = string.Empty;

    public ContingencyBookState Status { get; set; } = ContingencyBookState.Active;

    public bool Incluye(int numero) => numero >= FromNumber && numero <= ToNumber;
}

/// <summary>
/// <c>RCD_SupervisorDelegations</c> (data-model §1.9; FR-184): el supervisor de un punto delega en un suplente, con vigencia y motivo, aprobar diferencias y
/// cerrar el día. La segregación sigue siendo por persona. Terminarla cierra la vigencia. (nuevo)
/// </summary>
public class SupervisorDelegation : AuditableEntity
{
    public int CollectionPointId { get; set; }

    /// <summary><c>SEC_Users.Id</c> del supervisor.</summary>
    public int SupervisorUserId { get; set; }

    /// <summary><c>SEC_Users.Id</c> del suplente.</summary>
    public int DelegateUserId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string Reason { get; set; } = string.Empty;

    public bool VigenteEn(DateOnly fecha) => VigenciaDeRecaudo.Incluye(ValidFrom, ValidTo, fecha);
}

/// <summary>
/// <c>RCD_ChannelModalityRules</c> (data-model §1.10; FR-198, D38): con qué modalidad se aplica lo que entra por un canal no interactivo a un crédito
/// (<see cref="LoanModality"/>, el número de <c>ModalidadDePago</c> del contrato de obligaciones) o a una cuenta (<see cref="AccountModality"/>, el de
/// <c>ModalidadDeConsignacion</c>). Gana la de menor <see cref="Priority"/> vigente. La semilla: créditos <c>CycleInstallment</c>; cuentas
/// <c>Installments</c> si hay exigibles, si no <c>Free</c>. (nuevo)
/// </summary>
public class ChannelModalityRule : AuditableEntity
{
    public ReceiptChannel Channel { get; set; }

    public ApplicationTarget Target { get; set; }

    /// <summary>El número de <c>ModalidadDePago</c> (créditos).</summary>
    public int? LoanModality { get; set; }

    /// <summary>El número de <c>ModalidadDeConsignacion</c> (cuentas).</summary>
    public int? AccountModality { get; set; }

    /// <summary>Con cuentas: la modalidad sólo rige si la persona tiene cuotas exigibles (si no, la siguiente regla).</summary>
    public bool OnlyWhenInstallmentsDue { get; set; }

    public int Priority { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly fecha) => VigenciaDeRecaudo.Incluye(ValidFrom, ValidTo, fecha);
}
