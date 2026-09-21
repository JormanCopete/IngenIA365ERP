using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Formato de archivo plano de un banco (feature 010, N4; contracts/archivos.md §2): el layout es
/// <b>dato con vigencia</b>, no código. Vive en Core ligado a <c>COR_Banks</c> (D-42) para que lo
/// usen nómina, tesorería y contabilidad: cada módulo elige el formato vigente del banco pagador
/// para su <see cref="Scope"/> y el motor (<c>FlatFileWriter</c>) sólo sabe poner cada valor en
/// su posición. Un archivo ya generado recuerda con qué formato se escribió y no cambia cuando
/// llega una versión nueva (Principio XI).
/// </summary>
public class BankFileFormat : AuditableEntity
{
    /// <summary>Banco pagador; nulo = formato genérico que sirve con cualquier banco (p. ej. <c>CSV-GENERICO</c>).</summary>
    public int? BankId { get; set; }
    public Bank? Bank { get; set; }

    public BankFileScope Scope { get; set; } = BankFileScope.PayrollDisbursement;

    /// <summary>Nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>): mayúsculas, hasta 20, único entre vivos.</summary>
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public BankFileKind Kind { get; set; } = BankFileKind.FixedWidth;

    /// <summary>Sólo <see cref="BankFileKind.Delimited"/>.</summary>
    [MaxLength(5)]
    public string? Delimiter { get; set; }

    /// <summary>Sólo delimitado: encierra los textos entre comillas dobles.</summary>
    public bool QuoteText { get; set; }

    /// <summary><c>us-ascii</c>, <c>utf-8</c> o <c>windows-1252</c>.</summary>
    [MaxLength(20)]
    public string Encoding { get; set; } = "us-ascii";

    public BankFileLineEnding LineEnding { get; set; } = BankFileLineEnding.Crlf;

    public bool Uppercase { get; set; } = true;
    public bool StripAccents { get; set; } = true;

    public BankFileAmountFormat AmountFormat { get; set; } = BankFileAmountFormat.Integer;

    /// <summary>Plantilla del nombre del archivo con los mismos orígenes: <c>NOM{PaymentDate:yyyyMMdd}{Sequence:000}.txt</c>.</summary>
    [MaxLength(120)]
    public string FileNamePattern { get; set; } = "PAGO{PaymentDate:yyyyMMdd}.txt";

    [MaxLength(60)]
    public string ContentType { get; set; } = "text/plain";

    public bool HasHeader { get; set; } = true;
    public bool HasTrailer { get; set; } = true;

    /// <summary>Convenio o código de empresa en el banco; lo resuelve el origen <c>SourceAgreementCode</c>.</summary>
    [MaxLength(20)]
    public string? AgreementCode { get; set; }

    /// <summary>Sembrado o cargado por la cooperativa; lo sembrado se puede desactivar, no borrar.</summary>
    public bool IsSeeded { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<BankFileFormatField> Fields { get; set; } = [];

    public bool VigenteEn(DateOnly fecha) => IsActive && ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>Un campo de un registro del formato: dónde va, de dónde sale y cómo se escribe.</summary>
public class BankFileFormatField : AuditableEntity
{
    public int FormatId { get; set; }
    public BankFileFormat? Format { get; set; }

    public BankFileRecord Record { get; set; }

    /// <summary>1..N dentro del registro, sin huecos.</summary>
    public int Order { get; set; }

    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public BankFieldSource Source { get; set; }

    /// <summary>Sólo <see cref="BankFieldSource.Constant"/>.</summary>
    [MaxLength(120)]
    public string? ConstantValue { get; set; }

    /// <summary>Obligatorio en ancho fijo; máximo en delimitado (nulo = sin tope).</summary>
    public int? Length { get; set; }

    public BankFieldAlignment Alignment { get; set; } = BankFieldAlignment.Left;

    /// <summary>Espacio o cero.</summary>
    [MaxLength(1)]
    public string PadChar { get; set; } = " ";

    public BankFieldDataType DataType { get; set; } = BankFieldDataType.Text;

    /// <summary>Formato de fecha (<c>yyyyMMdd</c>) o de importe si difiere del general del formato.</summary>
    [MaxLength(40)]
    public string? ValueFormat { get; set; }

    /// <summary>Equivalencias del valor del ERP al del banco: <c>{"1":"S","2":"D"}</c>, <c>{"CC":"1","CE":"2"}</c>.</summary>
    [MaxLength(400)]
    public string? ValueMapJson { get; set; }

    /// <summary>Vacío en un campo requerido: el pago queda en pendientes con motivo, no sale un archivo a medias.</summary>
    public bool Required { get; set; }

    /// <summary>Texto más largo que <see cref="Length"/>: recortar (<c>true</c>) o rechazar la línea (<c>false</c>).</summary>
    public bool Truncate { get; set; } = true;
}
