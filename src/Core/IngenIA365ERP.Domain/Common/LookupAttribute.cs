namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// T099 — Marca una entidad como catálogo inmutable. Los <c>[Lookup]</c>:
/// <list type="bullet">
///   <item>NO heredan de <see cref="AuditableEntity"/> — su superficie es
///         de solo-lectura post-seed.</item>
///   <item>Quedan exentos del check del Principio VII (todos los demás
///         tipos en <c>Domain.Entities</c> SÍ deben ser auditables).</item>
///   <item>Se siembran una vez al provisionar y no se mutan en runtime.
///         Ejemplos: <c>Permission</c>, monedas, países, taxonomías ISO.</item>
/// </list>
///
/// <para>
/// Si un catálogo necesita evolucionar (agregar un permiso nuevo), se
/// hace via migración + re-seed idempotente — NUNCA con UPDATE/DELETE en
/// caliente desde un handler.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class LookupAttribute : Attribute;
