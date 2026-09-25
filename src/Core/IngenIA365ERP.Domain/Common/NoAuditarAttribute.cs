namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Feature 012 (decisiones-transversales T36, §2.16): el valor de esta propiedad no se copia a la
/// auditoría. El cambio sí se registra —la propiedad aparece en <c>changedFields</c>— pero antes y
/// después quedan enmascarados. Es para secretos y claves técnicas (<c>TechnicalKey</c>,
/// <c>CredentialKey</c>): la auditoría se conserva diez años y la leen personas que no deben verlos.
///
/// <para>
/// Quien lo lee es <c>AuditableEntityInterceptor</c> (T059). Para no auditar una entidad entera está
/// <see cref="SinDiffDeAuditoriaAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class NoAuditarAttribute : Attribute
{
    /// <summary>Lo que se escribe en la auditoría en lugar del valor.</summary>
    public const string Mascara = "***";
}
