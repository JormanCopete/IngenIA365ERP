namespace IngenIA365ERP.Domain.Enums.Lending;

// 2026-11-18 (pedido del dueño): los documentos para firma de una solicitud de crédito —pagaré, carta de instrucciones, libranza y los que cada
// cooperativa agregue— salen de plantillas configurables por cooperativa. (nuevo)

/// <summary>La clase de un documento para firma: ordena la impresión y dice qué hace el sistema con él.</summary>
public enum SignatureDocumentKind
{
    /// <summary>El pagaré (C. de Co. arts. 621, 709 a 711). Al generarlo, la solicitud queda con su pagaré físico.</summary>
    PromissoryNote = 1,

    /// <summary>La carta de instrucciones para llenar los espacios en blanco del pagaré (C. de Co. art. 622).</summary>
    InstructionLetter = 2,

    /// <summary>La libranza o autorización de descuento por nómina (Ley 1527 de 2012; Ley 79 de 1988, art. 142).</summary>
    PayrollDeductionAuthorization = 3,

    /// <summary>Cualquier otro documento que la cooperativa quiera hacer firmar con el crédito.</summary>
    Other = 4,
}

/// <summary>El tipo de dato de un campo propio de la cooperativa en los documentos para firma.</summary>
public enum SignatureFieldKind
{
    Text = 1,
    LongText = 2,
    Number = 3,

    /// <summary>Pesos: además del campo, la plantilla puede usar <c>{CAMPO}_LETRAS</c>.</summary>
    Money = 4,

    Date = 5,

    /// <summary>Sí o no: sirve en <c>{{#SI CAMPO}}</c>.</summary>
    YesNo = 6,
}

/// <summary>De dónde sale el valor de un campo propio.</summary>
public enum SignatureFieldSource
{
    /// <summary>Un valor fijo de la cooperativa (la ciudad de pago, el domicilio, el representante legal…).</summary>
    Constant = 1,

    /// <summary>Lo digita quien genera los documentos de cada solicitud.</summary>
    Typed = 2,
}

/// <summary>El estado de un juego de documentos generado.</summary>
public enum SignatureDocumentSetStatus
{
    Active = 1,

    /// <summary>Se volvió a generar: el juego nuevo lo reemplaza; éste se conserva.</summary>
    Superseded = 2,
}
