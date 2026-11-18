using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_SignatureDocumentFields</c> (2026-11-18, pedido del dueño): un campo propio de la cooperativa para sus documentos para firma, además de los
/// del sistema (<c>CamposDelSistema</c>). Es un valor fijo de la cooperativa (la ciudad de pago, el domicilio, el registro como operador de libranza…)
/// o algo que se digita al generar los documentos de cada solicitud. Cualquier plantilla lo usa como <c>{{CODIGO}}</c>; uno en pesos da además
/// <c>{{CODIGO_LETRAS}}</c>. (nuevo)
/// </summary>
public class SignatureDocumentField : AuditableEntity
{
    public const int LargoDelCodigo = 40;
    public const int LargoDelNombre = 120;
    public const int LargoDelValor = 2000;
    public const int LargoDeLaAyuda = 300;

    /// <summary>El código que se escribe en la plantilla (mayúsculas, números y guion bajo; único entre los vivos).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Lo que ve quien lo digita.</summary>
    public string Label { get; set; } = string.Empty;

    public SignatureFieldKind Kind { get; set; } = SignatureFieldKind.Text;

    public SignatureFieldSource Source { get; set; } = SignatureFieldSource.Constant;

    /// <summary>El valor de un campo fijo.</summary>
    public string? ConstantValue { get; set; }

    /// <summary>El valor propuesto de un campo que se digita.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Un campo que se digita es obligatorio para generar.</summary>
    public bool IsRequired { get; set; }

    public string? HelpText { get; set; }

    public int Order { get; set; }

    public bool IsActive { get; set; } = true;
}
