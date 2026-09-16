namespace IngenIA365ERP.Audit.Indexes;

/// <summary>
/// Cuánto se conserva cada evento de auditoría (feature 009, FR-052). SARLAFT exige cinco años
/// (constitución, Principio X); la norma contable colombiana exige diez para los registros
/// contables y su rastro, y el registro de ingreso a las opciones se conserva con ellos.
///
/// <para>
/// Hasta la 009 el TTL era un índice de cinco años sobre <c>occurredAt</c>, igual para todo.
/// Un TTL por módulo con índices parciales no es posible en MongoDB (el filtro parcial no
/// admite «no está en»), así que cada documento lleva su propia fecha de vencimiento
/// (<c>expiresAt</c>) y un único índice TTL con <c>expireAfterSeconds = 0</c> la aplica. Los
/// documentos anteriores a la 009 reciben la suya en el arranque
/// (<see cref="AuditIndexBootstrap"/>).
/// </para>
/// </summary>
public static class AuditRetention
{
    public static readonly TimeSpan Regulatoria = TimeSpan.FromDays(365 * 5);
    public static readonly TimeSpan Contable = TimeSpan.FromDays(365 * 10);

    /// <summary>Módulos cuyo rastro se conserva diez años.</summary>
    public static readonly string[] ModulosDeDiezAnios = ["Accounting", "Navigation"];

    public static TimeSpan Para(string? modulo) =>
        modulo is not null && ModulosDeDiezAnios.Contains(modulo, StringComparer.OrdinalIgnoreCase)
            ? Contable
            : Regulatoria;

    public static DateTime VenceEl(DateTime ocurridoEl, string? modulo) => ocurridoEl + Para(modulo);
}
