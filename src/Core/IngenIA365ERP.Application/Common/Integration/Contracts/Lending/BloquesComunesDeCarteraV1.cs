namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

// Feature 013 (T0069; DT §2.11; contracts/mensajes.md §9, interfaces.md §4.2): los bloques comunes de los mensajes de Cartera,
// en v1. Se serializan con OpcionesDeMensajes (la de la 012): camelCase en el orden en que se declaran, enums como texto, nulos
// escritos, decimales exactos. Ninguno usa enums de Inventario. Los valores de los enums de Cartera (LoanConcept, RatingSubcategory,
// ImpairmentModel, ClaseDeRecaudador, ClaseDeMedioDeRecaudo…) viajan como texto con su nombre: hasta que exista el enum se
// declaran string, y el JSON es el mismo con el enum o sin él. Un cambio incompatible de un bloque es un bloque V2 y, con él, una
// V2 de cada mensaje que lo usa. UserRefV1 se reutiliza de Contracts/Inventory. Ningún bloque lleva cuentas contables (FR-126).

/// <summary><c>LoanRefV1</c> (§9): el crédito por su <c>PublicId</c> y su número, que es el documento cruce (FR-126).</summary>
public sealed record LoanRefV1
{
    public Guid LoanPublicId { get; init; }

    public string LoanNumber { get; init; } = string.Empty;
}

/// <summary>
/// <c>LoanFactDimensionsV1</c> (§9; FR-126, T52): todas las dimensiones de la matriz que no dependen del concepto, incluidas
/// las que piden K6a y K6b (modelo de deterioro, segmento y unidad), para no forzar una V2.
/// </summary>
public sealed record LoanFactDimensionsV1
{
    /// <summary>Código de la línea (<c>CodigoDeCatalogo</c>); dimensión <c>CreditLineCode</c>.</summary>
    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>Modalidad del documento <c>CatalogoDeModalidades</c> (<c>CONSUMO</c>, <c>COMERCIAL</c>, <c>MICROCREDITO</c>…).</summary>
    public string ModalityCode { get; init; } = string.Empty;

    /// <summary><c>ConLibranza</c>, <c>SinLibranza</c>, <c>PersonaNatural</c>, <c>PersonaJuridica</c>, <c>Empleados</c>, <c>Inmobiliario</c>, <c>Empresarial</c>.</summary>
    public string? SubsegmentCode { get; init; }

    /// <summary><c>A</c>..<c>E</c>: la oficial del último cierre o del cierre base, nunca la diaria (T48).</summary>
    public string RiskCategory { get; init; } = string.Empty;

    /// <summary>Valor de <c>RatingSubcategory</c> como texto: <c>None</c>, <c>E1</c>, <c>E2</c>.</summary>
    public string RiskSubcategory { get; init; } = string.Empty;

    /// <summary>Si el deudor es empleado, según la persona maestra hoy (C8).</summary>
    public bool IsEmployee { get; init; }

    public bool InGracePeriod { get; init; }

    public bool Restructured { get; init; }

    /// <summary>El estado <c>WrittenOff</c> del crédito a la fecha del hecho (C22).</summary>
    public bool WrittenOff { get; init; }

    /// <summary>Valor de <c>ClaseDeRecaudador</c>; sólo en los hechos que mueven el puente <c>RecaudoPorAplicar</c>.</summary>
    public string? CollectorClass { get; init; }

    /// <summary>Sucursal contable de la oficina del crédito.</summary>
    public Guid BranchPublicId { get; init; }

    /// <summary>Valor de <c>ImpairmentModel</c>: <c>ArrearsAnnex1</c> o <c>ExpectedLossAnnex2</c> (K6a).</summary>
    public string ImpairmentModel { get; init; } = string.Empty;

    /// <summary>Segmento SARC: <c>VIVIENDA</c>, <c>CONSUMO</c>, <c>MICROCREDITO</c>, <c>COMERCIAL_PN</c>, <c>COMERCIAL_PJ</c>.</summary>
    public string RiskSegment { get; init; } = string.Empty;

    /// <summary><c>Pesos</c>, o <c>Uvr</c> en vivienda (K6b).</summary>
    public string ValueUnit { get; init; } = string.Empty;
}

/// <summary><c>ConceptAmountV1</c> (§9): un valor por concepto (y cuota), con su signo (§4).</summary>
public sealed record ConceptAmountV1
{
    /// <summary>Valor de <c>LoanConcept</c> como texto (<c>Principal</c>, <c>CurrentInterest</c>, <c>DefaultInterest</c>…).</summary>
    public string Concept { get; init; } = string.Empty;

    /// <summary>Cuota; nula en lo que no es de una cuota (saldo a favor, deterioro, totales).</summary>
    public int? InstallmentNumber { get; init; }

    public decimal Amount { get; init; }

    /// <summary>Valor en UVR (K6b); nulo hasta entonces.</summary>
    public decimal? UvrAmount { get; init; }

    /// <summary>Si el importe (un seguro, un cargo) es por cuenta del asociado (C22).</summary>
    public bool OnBehalfOfAssociate { get; init; }

    /// <summary>Dimensión <c>ReasonCode</c> de la matriz cuando el rol la exige (forma de desembolso, motivo, movimiento del deterioro…).</summary>
    public string? ReasonCode { get; init; }
}

/// <summary><c>OriginalFactRefV1</c> (§9): el hecho del que éste depende: su mensaje, su tipo y su origen con su número o rótulo.</summary>
public sealed record OriginalFactRefV1
{
    public Guid MessageId { get; init; }

    public string Type { get; init; } = string.Empty;

    public Guid OriginPublicId { get; init; }

    public string Number { get; init; } = string.Empty;
}

/// <summary>
/// <c>ExternalDocumentRefV1</c> (§9): un documento de otro módulo, con la clase como <b>texto</b> y el módulo por su código
/// (<c>INV</c>, <c>NOM</c>, <c>TES</c>; <c>REC</c> cuando exista).
/// </summary>
public sealed record ExternalDocumentRefV1
{
    public string Module { get; init; } = string.Empty;

    public Guid PublicId { get; init; }

    public string DocumentClass { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;
}

/// <summary><c>LoanFactLineV1</c> (§9): una línea por crédito de un hecho masivo (o por crédito y día valor en el ajuste).</summary>
public sealed record LoanFactLineV1
{
    public LoanRefV1 Loan { get; init; } = new();

    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Otro tercero que la regla puede pedir: la aseguradora del seguro causado.</summary>
    public Guid? CounterpartyPersonPublicId { get; init; }

    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>En la reclasificación y en el reverso que la revierte: las dimensiones anteriores (C22).</summary>
    public LoanFactDimensionsV1? PreviousDimensions { get; init; }

    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El día valor de un ajuste de la corrida <c>NationalValueCorrection</c>.</summary>
    public DateOnly? ValueDate { get; init; }
}

/// <summary><c>LoanFactTotalV1</c> (§9, fijado allí): los totales de una parte por combinación de dimensiones (T14).</summary>
public sealed record LoanFactTotalV1
{
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    public int LoanCount { get; init; }
}

/// <summary><c>LoanPlanSummaryV1</c> (§9): el resumen del plan vigente tras el hecho.</summary>
public sealed record LoanPlanSummaryV1
{
    public int ScheduleVersion { get; init; }

    /// <summary>Valor de <c>AmortizationSystem</c>: <c>LevelPayment</c>, <c>ConstantPrincipal</c>, <c>Bullet</c>.</summary>
    public string AmortizationSystem { get; init; } = string.Empty;

    /// <summary>Periodicidad de la línea (<c>Weekly</c>, <c>Biweekly</c>, <c>Monthly</c>…).</summary>
    public string Periodicity { get; init; } = string.Empty;

    public int InstallmentCount { get; init; }

    public decimal InstallmentAmount { get; init; }

    public DateOnly FirstDueDate { get; init; }

    public DateOnly FinalDueDate { get; init; }

    /// <summary>Tasa pactada como fracción (<c>0.268200</c> es 26,82 % EA).</summary>
    public decimal AnnualEffectiveRate { get; init; }

    /// <summary>Valor de <c>DayCountConvention</c>: <c>Thirty360</c>, <c>Actual365</c>, <c>Actual360</c>.</summary>
    public string DayCountConvention { get; init; } = string.Empty;

    public string RulesVersion { get; init; } = string.Empty;
}

/// <summary><c>ReceiptRefV1</c> (§9, fijado allí): la referencia de recibo del contrato, tal como la dio el recaudador.</summary>
public sealed record ReceiptRefV1
{
    public string Number { get; init; } = string.Empty;

    public DateOnly Date { get; init; }

    public Guid? OfficePublicId { get; init; }

    public string? MeansReference { get; init; }

    public string? DocumentKind { get; init; }
}

/// <summary><c>CollectorRefV1</c> (§9, fijado allí): quién recaudó y con qué clave (interfaces.md §3.2, §3.7).</summary>
public sealed record CollectorRefV1
{
    /// <summary>Código del recaudador en el registro (<c>NOM</c>, <c>TST</c>…; <c>CAR</c> en la recogida, que no se registra).</summary>
    public string CollectorCode { get; init; } = string.Empty;

    /// <summary>Valor de <c>ClaseDeRecaudador</c>: <c>Receipts</c>, <c>Payroll</c>, <c>Contributions</c>, <c>OwnLoanPayoff</c>, <c>Test</c>.</summary>
    public string CollectorClass { get; init; } = string.Empty;

    /// <summary>La clave con que el recaudador aplicó (<c>NOM:{run:N}:{obligación:N}</c>, <c>TST:{guid:N}</c>…).</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>Valor de <c>ClaseDeMedioDeRecaudo</c>: la clase de medio, nunca el medio de la cooperativa (FR-082).</summary>
    public string MeansClass { get; init; } = string.Empty;

    public Guid PayerPersonPublicId { get; init; }

    public ReceiptRefV1 Receipt { get; init; } = new();
}

/// <summary><c>PayeeV1</c> (interfaces.md §4.2): el beneficiario sellado en la orden de giro, con su cuenta bancaria.</summary>
public sealed record PayeeV1
{
    public Guid PersonPublicId { get; init; }

    public string TaxIdType { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Código ACH (<c>COR_Banks.TransferCode</c>).</summary>
    public string? BankCode { get; init; }

    public string? BankAccountNumber { get; init; }

    public string? BankAccountType { get; init; }
}

/// <summary><c>PayoffDeductionV1</c> (interfaces.md §4.2): un crédito recogido en el desembolso, con su comprobante de aplicación.</summary>
public sealed record PayoffDeductionV1
{
    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Su operación de aplicación (<c>OwnLoanPayoff</c>).</summary>
    public Guid OperationPublicId { get; init; }

    public decimal Amount { get; init; }
}

/// <summary>
/// <c>GeneralImpairmentLineV1</c> (§10.17, fijado allí): el deterioro general o adicional por dimensión, en delta (T48);
/// <see cref="Kind"/> es el nivel y <see cref="ReasonCode"/> el movimiento.
/// </summary>
public sealed record GeneralImpairmentLineV1
{
    /// <summary><c>General</c> o <c>Additional</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public Guid BranchPublicId { get; init; }

    public string ModalityCode { get; init; } = string.Empty;

    public string? SubsegmentCode { get; init; }

    public string RiskSegment { get; init; } = string.Empty;

    /// <summary>Valor de <c>ImpairmentModel</c>.</summary>
    public string ImpairmentModel { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    /// <summary><c>Increase</c>, <c>DecreaseSameYear</c>, <c>DecreasePriorYears</c> o <c>Transfer</c>.</summary>
    public string ReasonCode { get; init; } = string.Empty;
}

/// <summary>La operación que puso al día la causación de un crédito (§10.0, <c>catchUpOperation</c>; C04).</summary>
public sealed record CatchUpOperationV1
{
    public Guid OperationPublicId { get; init; }

    public string DocumentClass { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;
}

/// <summary>Una redistribución de un pago posterior por una fecha valor anterior (§10.7, <c>reallocations</c>; FR-081, D4): suma cero por pago.</summary>
public sealed record ReallocationV1
{
    public OriginalFactRefV1 Payment { get; init; } = new();

    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];
}

/// <summary>Un tramo del desembolso (§10.1, <c>tranche</c>).</summary>
public sealed record TrancheV1
{
    public int Number { get; init; }

    public bool IsLast { get; init; }
}

/// <summary>El desembolso en especie desde una venta (§10.1, <c>inKind</c>; FR-050, FR-056).</summary>
public sealed record InKindDisbursementV1
{
    public Guid SaleMessageId { get; init; }

    public ExternalDocumentRefV1 Invoice { get; init; } = new();

    public Guid CreditPaymentPublicId { get; init; }

    /// <summary><c>Contabilidad</c> o <c>Cartera</c>: el sello de la venta, que nunca cambia en la cadena.</summary>
    public string AccountsReceivableRecordedBy { get; init; } = string.Empty;

    /// <summary>En un reemplazo (<c>Replacement</c>), el crédito anulado.</summary>
    public LoanRefV1? ReplacesLoan { get; init; }
}

/// <summary>El mensaje de Comercial al que responde una <c>ObligacionCreada</c> u <c>ObligacionRechazada</c> (§11.1, <c>saleMessage</c>).</summary>
public sealed record SaleMessageRefV1
{
    public Guid MessageId { get; init; }

    public string Type { get; init; } = string.Empty;

    public int Version { get; init; }
}

/// <summary>Un mes con su subperíodo, si lo tiene (§11.6, §12.6, <c>period</c>).</summary>
public sealed record PayrollPeriodRefV1
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int? SubPeriod { get; init; }
}
