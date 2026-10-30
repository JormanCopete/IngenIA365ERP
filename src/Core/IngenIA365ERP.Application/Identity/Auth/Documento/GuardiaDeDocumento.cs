using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.Auth.Documento;

/// <summary>
/// Feature 017 (US5, FR-017): la decisión de si una persona puede seguir entrando sin documento vive en un
/// solo sitio, con el patrón de <see cref="Common.GuardiaDeMetodos"/>. Dos reglas que parecen detalles:
/// quien ya tiene documento <b>nunca</b> se mira (cero consultas, cero efecto el día del despliegue), y una
/// cooperativa sin fecha (<c>DocumentRequiredFrom</c> nula, el defecto) <b>nunca</b> bloquea. La fecha es
/// la de Colombia: el día que la cooperativa fijó empieza a medianoche de aquí, no de Greenwich.
/// </summary>
public static class GuardiaDeDocumento
{
    public enum Veredicto
    {
        Admite,
        FaltaDocumento,
    }

    public static Veredicto Evaluar(bool tieneDocumento, IEnumerable<DateOnly?> exigidoDesde, DateOnly hoy)
    {
        if (tieneDocumento) return Veredicto.Admite;
        return exigidoDesde.Any(f => f is { } desde && desde <= hoy)
            ? Veredicto.FaltaDocumento
            : Veredicto.Admite;
    }
}

/// <summary>
/// Lee las fechas de exigencia de las cooperativas indicadas y le pregunta a <see cref="GuardiaDeDocumento"/>.
/// La consultan las puertas que acuñan una sesión con cooperativa: el ingreso (con y sin segundo factor),
/// elegir y cambiar de cooperativa, el ascenso tras inscribir el segundo factor y la renovación.
/// </summary>
public interface IExigenciaDeDocumento
{
    Task<GuardiaDeDocumento.Veredicto> EvaluarAsync(CentralUser persona, IEnumerable<Guid> cooperativas, CancellationToken ct);
}

public sealed class ExigenciaDeDocumento(IAdminDbContext db, IDateTimeService clock) : IExigenciaDeDocumento
{
    public async Task<GuardiaDeDocumento.Veredicto> EvaluarAsync(
        CentralUser persona, IEnumerable<Guid> cooperativas, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(persona.DocumentUsername)) return GuardiaDeDocumento.Veredicto.Admite;

        var ids = cooperativas.Distinct().ToList();
        if (ids.Count == 0) return GuardiaDeDocumento.Veredicto.Admite;

        var fechas = await db.TenantIdentityPolicies.AsNoTracking()
            .Where(p => ids.Contains(p.TenantId) && p.DocumentRequiredFrom != null)
            .Select(p => p.DocumentRequiredFrom)
            .ToListAsync(ct);
        return GuardiaDeDocumento.Evaluar(false, fechas, clock.HoyLocal);
    }
}
