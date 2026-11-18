using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_SignatureDocumentTemplates</c> (2026-11-18, pedido del dueño): la plantilla de un documento para firma de la solicitud de crédito —el pagaré,
/// la carta de instrucciones, la libranza u otro que agregue la cooperativa—. El texto lleva campos <c>{{CAMPO}}</c>, bloques <c>{{#SI …}}</c> y
/// <c>{{#CADA …}}</c> y el lugar de las firmas (<c>Lending.SignatureDocuments.PlantillaDeFirma</c>). Cada cooperativa la ajusta: cambiar el texto sube
/// la versión y quita la validación del abogado, porque lo validado era el texto anterior. Lo ya generado no cambia: cada juego guarda su PDF y la
/// versión con que salió. (nuevo)
/// </summary>
public class SignatureDocumentTemplate : AuditableEntity
{
    public const int LargoDelCodigo = 20;
    public const int LargoDelNombre = 120;
    public const int LargoDelTexto = 40000;
    public const int LargoDeLaNota = 300;

    public SignatureDocumentKind Kind { get; set; }

    /// <summary>Código de la cooperativa (único entre las vivas).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>El texto de la plantilla.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Los canales de pago de la solicitud en que aplica (nombres de <see cref="RepaymentChannel"/> separados por coma). Nulo = todos. La libranza base
    /// aplica a <c>OwnPayroll,PaymasterDeduction</c>.
    /// </summary>
    public string? RepaymentChannels { get; set; }

    /// <summary>Los códigos de las líneas en que aplica, separados por coma. Nulo = todas.</summary>
    public string? CreditLineCodes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Orden de impresión dentro de su clase.</summary>
    public int PrintOrder { get; set; }

    /// <summary>Las firmas llevan el recuadro para la huella del índice derecho.</summary>
    public bool IncludeFingerprint { get; set; }

    /// <summary>Los codeudores también firman (el pagaré y la carta, sí; la libranza la firma sólo el deudor).</summary>
    public bool SignedByCoDebtors { get; set; } = true;

    /// <summary>Sube con cada cambio del texto.</summary>
    public int Version { get; set; } = 1;

    /// <summary>El abogado de la cooperativa validó esta versión del texto.</summary>
    public bool IsVerified { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public int? VerifiedByUserId { get; set; }

    public string? VerificationNote { get; set; }

    /// <summary>¿Aplica a una solicitud con este canal de pago y esta línea?</summary>
    public bool Aplica(RepaymentChannel canal, string? codigoDeLinea) =>
        IsActive && !IsDeleted
        && (Lista(RepaymentChannels) is not { Count: > 0 } canales || canales.Contains(canal.ToString(), StringComparer.OrdinalIgnoreCase))
        && (Lista(CreditLineCodes) is not { Count: > 0 } lineas
            || (codigoDeLinea is not null && lineas.Contains(codigoDeLinea.Trim(), StringComparer.OrdinalIgnoreCase)));

    /// <summary>Cambia el texto: sube la versión y quita la validación (lo validado era el texto anterior). Sin cambio, no hace nada.</summary>
    public void CambiarTexto(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        if (string.Equals(Body, texto, StringComparison.Ordinal)) return;
        Body = texto;
        Version++;
        IsVerified = false;
        VerifiedAt = null;
        VerifiedByUserId = null;
        VerificationNote = null;
    }

    /// <summary>Marca la versión vigente como validada por el abogado.</summary>
    public void Validar(int? usuario, DateTime instante, string? nota)
    {
        IsVerified = true;
        VerifiedAt = instante;
        VerifiedByUserId = usuario;
        VerificationNote = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
    }

    /// <summary>Una lista guardada como texto separado por comas.</summary>
    public static IReadOnlyList<string> Lista(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Una lista como texto separado por comas (nula si queda vacía).</summary>
    public static string? Texto(IEnumerable<string>? lista)
    {
        var valores = (lista ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return valores.Count == 0 ? null : string.Join(",", valores);
    }
}
