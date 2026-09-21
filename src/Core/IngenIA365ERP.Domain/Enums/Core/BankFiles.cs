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

    /// <summary>Pagos a proveedores y terceros desde tesorería (reservado; llega con ese módulo).</summary>
    SupplierPayments = 3,
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
}
