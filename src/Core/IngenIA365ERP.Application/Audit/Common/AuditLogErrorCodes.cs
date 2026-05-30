namespace IngenIA365ERP.Application.Audit.Common;

/// <summary>
/// Códigos canónicos del módulo audit-log (Principio VI/X — namespacing
/// <c>Modulo.Condicion</c>). El <c>ErrorEnvelopeFilter</c> infiere el HTTP
/// status del prefijo: <c>AuditLog.RangeTooLarge</c> → 400.
/// </summary>
public static class AuditLogErrorCodes
{
    /// <summary>
    /// Rango From-To excede el máximo permitido en consulta interactiva
    /// (6 meses). El cliente debe acotar el rango o usar el endpoint
    /// de export que devuelve streaming sin construir página completa.
    /// </summary>
    public const string RangeTooLarge = "AuditLog.RangeTooLarge";

    /// <summary>
    /// Firma HMAC del PDF no coincide con la recalculada por el verificador
    /// SaaS. Indica manipulación posterior al export o clave rotada.
    /// </summary>
    public const string SignatureInvalid = "AuditLog.SignatureInvalid";
}
