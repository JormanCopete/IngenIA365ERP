namespace IngenIA365ERP.Domain.Enums.Core;

/// <summary>
/// Para qué sirve un formato de archivo bancario (feature 010, N4; D-42). El formato es dato
/// ligado al banco (<c>COR_BankFileFormats</c>) y lo comparten los módulos: hoy lo consume la
/// nómina (dispersión y consignación de cesantías); tesorería y contabilidad agregan su
/// ámbito sin tocar el motor, sólo los orígenes que resuelven.
/// </summary>
public enum BankFileScope
{
    /// <summary>Pago de nómina a empleados desde la relación de pago de una corrida aprobada.</summary>
    PayrollDisbursement = 1,

    /// <summary>Consignación anual de cesantías al fondo (el fondo hace de «banco»).</summary>
    SeveranceDeposit = 2,

    /// <summary>
    /// El archivo de pago de un lote de Tesorería (feature 015, T0023; R-20; archivos-y-plantillas.md §1, §3). Antes se llamaba
    /// <c>SupplierPayments</c>, reservado sin filas: se renombra sin cambiar el número.
    /// </summary>
    TreasuryPayments = 3,

    /// <summary>
    /// Transferencias de desembolso de crédito desde Cartera (feature 013, K1, FR-047; contracts/adaptadores-y-archivos.md §6.1). Se guarda
    /// como entero sin CHECK en <c>COR_BankFileFormats.Scope</c>: agregarlo no pide migración.
    /// </summary>
    LoanDisbursement = 4,

    /// <summary>
    /// Archivo de descuentos de libranza que Cartera envía a una pagaduría (feature 013, K2, FR-113; adaptadores-y-archivos.md §6.3): siempre
    /// genérico (<c>BankId</c> nulo), uno por pagaduría, elegido por referencia desde la versión del convenio (<c>OutboundFormatId</c>).
    /// </summary>
    [SeleccionPorReferencia]
    PayrollDeductionRequest = 5,

    /// <summary>
    /// Archivo de respuesta de una pagaduría (K2, FR-115): lo lee Recaudo —y la vista previa del formato— con <c>FlatFileReader</c>; Cartera no lo
    /// lee, consume <c>ResultadoDeDescuentos</c>. Siempre genérico y elegido por referencia (<c>ResultFormatId</c>).
    /// </summary>
    [SeleccionPorReferencia]
    PayrollDeductionResult = 6,

    // --- Feature 015 (T0023; R-20; archivos-y-plantillas.md §1): ámbitos de Recaudo y Tesorería. Como el 4, se guardan como entero sin CHECK. ---

    /// <summary>Lectura: el extracto o archivo de movimientos de una cuenta bancaria propia (Tesorería, R0/T0).</summary>
    BankStatement = 7,

    /// <summary>Lectura: el archivo diario de un convenio de recaudo (Recaudo, R2).</summary>
    CollectionAgreementFile = 8,

    /// <summary>Lectura: la respuesta del banco a un archivo de pago (Tesorería, T0).</summary>
    TreasuryPaymentResponse = 9,

    /// <summary>Lectura: el reporte de una pasarela o de PSE (Recaudo, R2).</summary>
    PaymentGatewayFile = 10,

    /// <summary>Lectura: los abonos de la red de tarjetas (Tesorería, T2).</summary>
    AcquirerSettlement = 11,
}

/// <summary>
/// Marca un <see cref="BankFileScope"/> cuyo formato no se elige «el vigente del banco para el ámbito» sino <b>por referencia</b> desde otra
/// configuración (feature 013, K2, T78, DT §2.6): la versión del convenio de libranza nombra su formato de ida y el de vuelta. Varios formatos
/// genéricos del mismo ámbito conviven con vigencias cruzadas —uno por pagaduría— y <c>SolapadoAsync</c> no les aplica el cruce
/// (<c>LosFormatosElegidosPorReferenciaNoCompitenPorVigencia</c>). (nuevo)
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class SeleccionPorReferenciaAttribute : Attribute
{
    /// <summary>¿El ámbito lleva la marca?</summary>
    public static bool Tiene(BankFileScope ambito) =>
        typeof(BankFileScope).GetField(ambito.ToString())?.IsDefined(typeof(SeleccionPorReferenciaAttribute), inherit: false) == true;
}

/// <summary>Ancho fijo (cada campo en su posición) o delimitado (separador entre campos).</summary>
public enum BankFileKind
{
    FixedWidth = 1,
    Delimited = 2,
}

/// <summary>Los tres registros de un archivo plano: cabecera, detalle (uno por pago) y totales.</summary>
public enum BankFileRecord
{
    Header = 1,
    Detail = 2,
    Trailer = 3,
}

public enum BankFileLineEnding
{
    Crlf = 1,
    Lf = 2,
}

/// <summary>Cómo se escriben los montos: <c>1250000</c>, <c>125000000</c> (centavos implícitos) o <c>1250000.00</c>.</summary>
public enum BankFileAmountFormat
{
    Integer = 1,
    ImplicitCents = 2,
    Point2 = 3,
}

public enum BankFieldAlignment
{
    Left = 1,
    Right = 2,
}

public enum BankFieldDataType
{
    Text = 1,
    Integer = 2,
    Amount = 3,
    Date = 4,
}

/// <summary>
/// De dónde sale el valor de un campo. Lo que no está aquí se agrega al enum, al validador y al
/// escritor con su prueba —nunca como código por banco (contracts/archivos.md §2.1). Los orígenes
/// del beneficiario son genéricos (<c>Payee*</c>) para que tesorería pague a un proveedor con el
/// mismo formato con que nómina paga a un empleado; el JSON del contrato acepta también los
/// nombres <c>Employee*</c>, <c>NetAmount</c> y <c>PaymentConcept</c> como sinónimos.
/// </summary>
public enum BankFieldSource
{
    // --- todos los registros ---
    Constant = 1,
    Blank = 2,
    CompanyNit = 10,
    CompanyNitDv = 11,
    CompanyName = 12,
    SourceAccountNumber = 20,
    SourceAccountType = 21,
    SourceBankCode = 22,
    SourceAgreementCode = 23,
    PaymentDate = 30,
    GenerationDate = 31,
    GenerationTime = 32,
    Sequence = 33,
    BatchReference = 34,

    // --- sólo detalle ---
    LineNumber = 40,
    PayeeDocumentType = 41,
    PayeeDocument = 42,
    PayeeFullName = 43,
    PayeeFirstNames = 44,
    PayeeLastNames = 45,
    PayeeBankCode = 46,
    PayeeAccountType = 47,
    PayeeAccountNumber = 48,
    Amount = 49,
    Concept = 50,
    PayeeEmail = 51,

    // --- sólo totales ---
    LineCount = 60,
    TotalAmount = 61,

    // --- ámbito consignación de cesantías (contracts/archivos.md §3.2) ---
    FundNit = 70,
    FundPilaCode = 71,
    SeveranceDays = 72,
    SeveranceBaseSalary = 73,
    PayeeHireDate = 74,
    Year = 75,

    // --- Cartera (feature 013; data-model §10 fija los números; adaptadores-y-archivos.md §6.1) ---
    /// <summary>El número del crédito: concepto y referencia de la transferencia de desembolso (K1) y de la libranza (K2).</summary>
    LoanNumber = 81,

    // --- Libranza, archivo de ida (K2, PayrollDeductionRequest; data-model parte 3 §9.3 fija los números) ---
    /// <summary>La identificación del empleado ante la pagaduría (<c>LND_LoanPayrollLinks.EmployeeIdAtPaymaster</c>, FR-109).</summary>
    PayerEmployeeId = 80,

    /// <summary>El número de la cuota del período que se pide descontar.</summary>
    InstallmentNumber = 82,

    /// <summary>El vencimiento de esa cuota.</summary>
    DueDate = 83,

    /// <summary>La novedad de la línea frente al último archivo enviado (<c>PayrollNoveltyKind</c>: alta, cambio, cancelación, sin cambio).</summary>
    NoveltyKind = 84,

    /// <summary>Lo vencido que el convenio deja cobrar además de lo exigible del período (<c>IncludesPastDue</c>).</summary>
    PastDueAmount = 85,

    /// <summary>El año del período de nómina del archivo.</summary>
    PeriodYear = 86,

    /// <summary>El mes del período de nómina del archivo.</summary>
    PeriodMonth = 87,

    /// <summary>La fecha de la libranza (prelación cronológica, FR-111).</summary>
    LibranzaDate = 88,

    // --- Libranza, archivo de vuelta (K2, PayrollDeductionResult): los lee FlatFileReader, nunca los escribe Cartera ---
    /// <summary>El código de respuesta de la pagaduría; lo traduce <c>LND_PayrollAgreementResultCodes</c>.</summary>
    ResultCode = 90,

    /// <summary>Lo que la pagaduría descontó de la línea.</summary>
    DeductedAmount = 91,

    /// <summary>La fecha del descuento: fecha valor del pago que lo gire (FR-114, A9).</summary>
    DeductionDate = 92,

    // Feature 014 (T014; data-model §1.7): orígenes de los ámbitos 5 y 6 para las cuotas de cuentas y el código de la pagaduría.
    /// <summary>El número de la cuenta de captación o de aportes de la cuota.</summary>
    AccountNumber = 93,

    /// <summary>El período (aaaamm) de la cuota exigible de la cuenta.</summary>
    AccountInstallmentPeriod = 94,

    /// <summary>El código o concepto de la pagaduría que agrupa la línea (equivalencia del convenio).</summary>
    PaymasterConceptCode = 95,

    // --- Feature 015 (T0023; archivos-y-plantillas.md §2): extracto bancario (BankStatement), lectura ---
    /// <summary>La fecha del movimiento en el banco.</summary>
    MovementDate = 100,

    /// <summary>La fecha valor del movimiento, si el banco la informa aparte.</summary>
    ValueDate = 101,

    /// <summary>El sentido (débito o crédito) cuando el banco no lo da por el signo del valor.</summary>
    Direction = 102,

    /// <summary>El valor del débito, cuando el banco lo trae en su columna.</summary>
    Debit = 103,

    /// <summary>El valor del crédito, cuando el banco lo trae en su columna.</summary>
    Credit = 104,

    /// <summary>
    /// La referencia del movimiento (la del consignante o la del banco). Archivos-y-plantillas §2 la llama <c>Reference</c>, pero ese nombre
    /// ya es sinónimo de <see cref="BatchReference"/> en las definiciones JSON: por eso <c>MovementReference</c>.
    /// </summary>
    MovementReference = 105,

    /// <summary>La secuencia o número único del movimiento en el banco: la huella preferida (FR-197).</summary>
    BankSequence = 106,

    /// <summary>El documento de quien consignó.</summary>
    DepositorTaxId = 107,

    /// <summary>El nombre de quien consignó.</summary>
    DepositorName = 108,

    /// <summary>La descripción del movimiento.</summary>
    Description = 109,

    /// <summary>El saldo después del movimiento.</summary>
    Balance = 110,

    /// <summary>El código de la oficina del banco.</summary>
    OfficeCode = 111,

    // --- Feature 015 (T0023; §2): respuesta del banco a un archivo de pago (TreasuryPaymentResponse), lectura ---
    /// <summary>El documento del beneficiario de la línea.</summary>
    BeneficiaryTaxId = 120,

    /// <summary>El código del banco destino de la línea.</summary>
    BankCode = 121,

    /// <summary>El resultado de la línea en el código del banco (pagado, rechazado), traducido por el formato.</summary>
    Outcome = 122,

    /// <summary>La causal del banco.</summary>
    BankCause = 123,

    /// <summary>La fecha en que el banco aplicó o rechazó la línea.</summary>
    BankDate = 124,

    /// <summary>La referencia del banco para la línea.</summary>
    BankReference = 125,

    // --- liquidación de un adquirente (feature 015, T2, fase 44; FR-133; ámbito AcquirerSettlement): de lectura ---

    /// <summary>El código del datáfono en la red.</summary>
    TerminalCode = 130,

    /// <summary>El número del lote del datáfono.</summary>
    TerminalBatchNumber = 131,

    /// <summary>Lo vendido con tarjeta en el lote, en bruto.</summary>
    GrossAmount = 132,

    /// <summary>La comisión de la red.</summary>
    CommissionAmount = 133,

    /// <summary>El IVA de la comisión.</summary>
    CommissionVatAmount = 134,

    /// <summary>La retención en la fuente que practicó la red.</summary>
    WithholdingTaxAmount = 135,

    /// <summary>La retención de IVA que practicó la red.</summary>
    WithholdingVatAmount = 136,

    /// <summary>La retención de ICA que practicó la red.</summary>
    WithholdingIcaAmount = 137,

    /// <summary>Lo abonado por la red por el lote: bruto − comisión − IVA − retenciones.</summary>
    SettledAmount = 138,
}
