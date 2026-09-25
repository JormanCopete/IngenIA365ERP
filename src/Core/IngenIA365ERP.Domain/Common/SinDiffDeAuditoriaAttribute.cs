namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Feature 012 (decisiones-transversales T36, §2.16): la entidad no deja diferencias de campos en la
/// auditoría. Es para filas técnicas cuyo cambio no es un hecho de negocio —un arrendamiento que se
/// renueva cada dos minutos, una clave de operación, el estado de una entrega— y que llenarían la
/// auditoría de ruido sin decir nada. El evento del comando que las toca, si lo hay, sí se audita.
///
/// <para>
/// Quien lo lee es <c>AuditableEntityInterceptor</c>, que omite estas entidades (T059). Se marca la
/// clase, nunca una propiedad: para ocultar un valor sin dejar de auditar el cambio está
/// <c>NoAuditarAttribute</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SinDiffDeAuditoriaAttribute : Attribute;
