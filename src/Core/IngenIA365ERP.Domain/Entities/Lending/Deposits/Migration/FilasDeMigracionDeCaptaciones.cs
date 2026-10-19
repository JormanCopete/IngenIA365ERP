using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;

// Feature 014, US9 (T151; data-model §7.1; contracts/plantillas-y-archivos.md §4): las filas de preparación de la migración de aportes y captaciones.
// Cada parte de la carga escribe su tabla; una tanda reemplazada conserva sus filas y deja de contar (como la 013 §29.3). Son hechos de sólo inserción
// sin diferencias de auditoría (data-model §0.8): el evento resumen es el de la importación. Aplicar la carga sólo las valida entre partes; nada nace en
// el libro de cuentas hasta activar. Ninguna fila crea personas: <c>PersonId</c> es siempre una persona viva del maestro.

/// <summary>
/// <c>LND_MigrationLineEquivalenceRows</c> (parte <c>line-equivalences</c>, aportes y captaciones; research E115): la equivalencia de un producto
/// heredado con una línea de la naturaleza de la carga, para que las demás partes resuelvan la línea por el código heredado. Llave: el código heredado.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationLineEquivalenceRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El código del producto heredado, en mayúsculas (texto 20).</summary>
    public string SourceProductCode { get; init; } = string.Empty;

    public string? SourceDescription { get; init; }

    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>La línea resuelta: de la naturaleza de la carga y con versión vigente al corte.</summary>
    public int CreditLineId { get; init; }

    public LineNature Nature { get; init; }

    /// <summary>La clase de la línea de captación (nula en aportes).</summary>
    public DepositClass? DepositClass { get; init; }
}

/// <summary>
/// <c>LND_MigrationContributionAccountRows</c> (parte <c>contribution-accounts</c>, K7): la cuenta de aportes de un asociado al corte con sus saldos por
/// clase. Al activar nace la cuenta <c>Contribution</c> (<c>Origin = Migration</c>), su control con la marca en el corte y una operación
/// <c>OpeningBalance</c> con una transacción por clase de aporte, sin hechos contables (FR-114).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationContributionAccountRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El asociado; una sola cuenta de aportes por asociado en la carga.</summary>
    public int PersonId { get; init; }

    /// <summary>El número que conserva la cuenta; nulo = se numera al activar con el consecutivo de la línea.</summary>
    public string? SourceAccountNumber { get; init; }

    /// <summary>El código heredado por el que se resolvió la línea, si vino así.</summary>
    public string? SourceProductCode { get; init; }

    public string CreditLineCode { get; init; } = string.Empty;

    public int CreditLineId { get; init; }

    public Guid OfficePublicId { get; init; }

    /// <summary>Fecha de vinculación (antigüedad).</summary>
    public DateOnly OpenedOn { get; init; }

    public decimal MinimumBalance { get; init; }

    public decimal OrdinaryBalance { get; init; }

    public decimal ExtraordinaryBalance { get; init; }

    public decimal RevaluedBalance { get; init; }

    /// <summary>El mínimo no reducible individual, si el estatuto lo fija (informativo hasta que la cuenta lo guarde, research E120).</summary>
    public decimal? MinimumNonReducible { get; init; }

    /// <summary>El ingreso registrado, base de una cuota por porcentaje del ingreso (informativo, research E120).</summary>
    public decimal? RegisteredIncome { get; init; }

    /// <summary>La cuota pactada si difiere de la de la línea (aviso; informativo, research E120).</summary>
    public decimal? InstallmentAmount { get; init; }

    /// <summary>Σ de las cuatro clases.</summary>
    public decimal Total => MinimumBalance + OrdinaryBalance + ExtraordinaryBalance + RevaluedBalance;
}

/// <summary>
/// <c>LND_MigrationAccountRows</c> (parte <c>member-accounts</c>, K8): una cuenta de captación (a la vista o permanente; el CDAT va en
/// <c>term-deposits</c> y el contractual llega con K9) al corte. Al activar nace la cuenta, su control (marca de causación en el corte, última
/// liquidación y último movimiento del titular) y la operación <c>OpeningBalance</c> con una transacción por concepto, sin hechos.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationAccountRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El número que conserva la cuenta (llave).</summary>
    public string SourceAccountNumber { get; init; } = string.Empty;

    public int PersonId { get; init; }

    public string? SourceProductCode { get; init; }

    public string CreditLineCode { get; init; } = string.Empty;

    public int CreditLineId { get; init; }

    public Guid OfficePublicId { get; init; }

    public DepositClass DepositClass { get; init; }

    public DateOnly OpenedOn { get; init; }

    /// <summary><c>Active</c> o <c>Inactive</c>.</summary>
    public MemberAccountStatus Status { get; init; }

    public DateOnly? LastHolderActivityDate { get; init; }

    public HandlingMode HandlingMode { get; init; } = HandlingMode.Indistinct;

    /// <summary>Saldo de capital al corte (incluye lo que está en canje).</summary>
    public decimal Principal { get; init; }

    /// <summary>Causado no liquidado (hasta 6 decimales): se liquida en la primera liquidación sin volver a causarse.</summary>
    public decimal InterestPayable { get; init; }

    public DateOnly? LastSettledThrough { get; init; }

    public decimal ChargeReceivable { get; init; }

    /// <summary>De <see cref="Principal"/>, lo que sigue en canje al corte.</summary>
    public decimal ClearingAmount { get; init; }

    public string? SourceOfFundsReference { get; init; }
}

/// <summary>
/// <c>LND_MigrationAccountPartyRows</c> (parte <c>member-account-parties</c>, K7): una parte de una cuenta de la carga —cotitular, firmante,
/// representante legal o beneficiario; el titular va en la cuenta—. Llave: cuenta + persona + papel.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationAccountPartyRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El número de la cuenta de la carga (de <c>member-accounts</c> o de <c>contribution-accounts</c>).</summary>
    public string SourceAccountNumber { get; init; } = string.Empty;

    public int PersonId { get; init; }

    public MemberAccountPartyRole Role { get; init; }

    /// <summary>Proporción del cotitular o del beneficiario como <b>fracción</b> (la plantilla la recibe en puntos).</summary>
    public decimal? Share { get; init; }

    public decimal? SignatureLimit { get; init; }

    /// <summary>Fundamento de la representación (<c>Parent</c>, <c>Guardian</c>, <c>CourtOrder</c>, <c>LegalEntityRepresentative</c>).</summary>
    public string? RepresentationBasis { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; init; }

    /// <summary>La referencia del soporte (registro civil o decisión judicial); el adjunto se sube después (FR-033).</summary>
    public string? SupportReference { get; init; }
}

/// <summary>
/// <c>LND_MigrationAccountInstallmentRows</c> (parte <c>account-installments</c>, K7): una cuota atrasada al corte de aportes o de ahorro permanente.
/// Al activar nace la cuota exigible con lo pagado como saldo inicial aplicado a ella, y sigue exigible. Alto volumen (bigint).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationAccountInstallmentRow : AuditableEntityLong, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceAccountNumber { get; init; } = string.Empty;

    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    public byte SubPeriodNumber { get; init; } = 1;

    public DateOnly DueDate { get; init; }

    public decimal Amount { get; init; }

    public decimal PaidAmount { get; init; }

    public NonDeductionReason? NonDeductionReason { get; init; }
}

/// <summary>
/// <c>LND_MigrationGmfExemptionRows</c> (parte <c>gmf-exemptions</c>, K8): la cuenta de ahorro marcada exenta del GMF de una persona y lo exento usado en
/// el mes del corte. Al activar nace la marca y una fila de uso del mes con <c>Origin = Migration</c>. Llave: la persona.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationGmfExemptionRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    public string SourceAccountNumber { get; init; } = string.Empty;

    public DateOnly MarkedSince { get; init; }

    public decimal MonthExemptUsed { get; init; }
}

/// <summary>
/// <c>LND_MigrationTaxYearRows</c> (parte <c>tax-year-to-date</c>, K8): rendimientos, retención, GMF y componente inflacionario del año del corte
/// anteriores al corte, por titular y cuenta, para el certificado tributario. Se cargan como información, sin hechos. Llave: titular + cuenta + año +
/// concepto.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationTaxYearRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    public string SourceAccountNumber { get; init; } = string.Empty;

    public short TaxYear { get; init; }

    public MigratedTaxConcept Concept { get; init; }

    public decimal Amount { get; init; }

    public decimal? WithholdingBase { get; init; }
}
