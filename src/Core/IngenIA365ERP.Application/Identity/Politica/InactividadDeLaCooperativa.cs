using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Identity.Politica;

/// <summary>
/// 2026-11-23 (pedido del dueño): cuánto tiempo sin actividad cierra la sesión en una cooperativa. Lo fija su política de identidad
/// (<c>ADM_TenantIdentityPolicies.SessionInactivityMinutes</c>, de 15 a 120 minutos); sin valor, o sin cooperativa (el maestro en
/// la consola), rige el general del servidor (<c>Sesion:InactividadMinutos</c>). Es el único lector: lo usan el refresh, que lo hace
/// cumplir, y la consulta con la que la pestaña cuenta los mismos minutos.
/// </summary>
public interface IInactividadDeLaCooperativa
{
    Task<TimeSpan> DeAsync(Guid? tenantPublicId, CancellationToken ct);
}

/// <inheritdoc cref="IInactividadDeLaCooperativa"/>
public sealed class InactividadDeLaCooperativa(IAdminDbContext db, IOptions<PoliticaDeSesionOptions> opciones) : IInactividadDeLaCooperativa
{
    public async Task<TimeSpan> DeAsync(Guid? tenantPublicId, CancellationToken ct)
    {
        var general = opciones.Value.Inactividad;
        if (tenantPublicId is not { } tenant) return general;
        var minutos = await db.TenantIdentityPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenant)
            .Select(p => p.SessionInactivityMinutes)
            .FirstOrDefaultAsync(ct);
        if (minutos is not { } m) return general;
        // Nunca más que la duración máxima: la inactividad no puede alargar una sesión que ya terminó.
        var propia = TimeSpan.FromMinutes(m);
        return propia > opciones.Value.DuracionMaxima ? opciones.Value.DuracionMaxima : propia;
    }
}
