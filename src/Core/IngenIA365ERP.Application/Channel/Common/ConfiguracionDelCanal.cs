using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Channel;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Common;

/// <summary>Feature 021: lectura de la configuración vigente del canal de la cooperativa (FR-014).</summary>
public static class ConfiguracionDelCanal
{
    /// <summary>La versión vigente: la de mayor número cuya vigencia ya empezó; nula si la cooperativa nunca configuró el canal.</summary>
    public static Task<ChannelSettings?> VigenteAsync(IApplicationDbContext db, DateTime ahoraUtc, CancellationToken ct) =>
        db.ChannelSettings.AsNoTracking()
            .Where(c => c.EffectiveFrom <= ahoraUtc)
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);

    /// <summary>¿El canal está encendido en la cooperativa?</summary>
    public static async Task<bool> EncendidoAsync(IApplicationDbContext db, DateTime ahoraUtc, CancellationToken ct) =>
        (await VigenteAsync(db, ahoraUtc, ct))?.IsEnabled == true;

    /// <summary>Los códigos de error del canal que la API de servicio traduce a HTTP.</summary>
    public static class Errores
    {
        public const string Apagado = "Channel.Disabled";
        public const string PersonaNoHabilitada = "Channel.Person.NotEnabled";
        public const string CooperativaDistinta = "Channel.Service.TenantMismatch";
        public const string TotalNoCuadra = "Channel.Notice.TotalMismatch";
    }
}
