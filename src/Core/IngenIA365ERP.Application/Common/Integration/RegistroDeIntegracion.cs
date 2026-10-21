using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// <see cref="IRegistroDeIntegracion"/> sobre <c>COR_IntegrationParticipants</c> y <c>COR_IntegrationParticipantAdoptions</c> (feature
/// 013, T0078; T16). Sólo lee. Los consumidores del despliegue los da la inyección (<see cref="IDestinoDeMensajes"/>) y el carácter de
/// producción, <see cref="IAmbienteDeDespliegue"/>. (nuevo)
/// </summary>
public sealed class RegistroDeIntegracion(
    IApplicationDbContext db,
    IEnumerable<IDestinoDeMensajes> destinos,
    IAmbienteDeDespliegue ambiente) : IRegistroDeIntegracion
{
    private readonly IReadOnlyList<IDestinoDeMensajes> _destinos = destinos.ToList();

    public async Task<ParticipanteVigente?> DestinoAsync(string destino, DateOnly fecha, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);
        var filas = await VigentesAsync(fecha, ct, IntegrationParticipantRole.MessageDestination, destino);
        return filas.Where(p => Cuenta(p, fecha)).OrderBy(p => p.IsTestDouble).ThenByDescending(p => p.ValidFrom)
            .Select(p => Vigente(p, fecha)).FirstOrDefault();
    }

    public async Task<bool> DestinoHabilitadoAsync(string destino, DateOnly fecha, CancellationToken ct) =>
        await DestinoAsync(destino, fecha, ct) is not null;

    public async Task<ParticipanteVigente?> RecaudadorAsync(string codigo, DateOnly fecha, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        var filas = await VigentesAsync(fecha, ct, IntegrationParticipantRole.ObligationsCollector, string.Empty);
        return filas.Where(p => p.Code == codigo && Cuenta(p, fecha)).OrderByDescending(p => p.ValidFrom)
            .Select(p => Vigente(p, fecha)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<ParticipanteVigente>> QueCuentanAsync(DateOnly fecha, CancellationToken ct) =>
        (await VigentesAsync(fecha, ct)).Where(p => Cuenta(p, fecha)).OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.Role)
            .Select(p => Vigente(p, fecha)).ToList();

    public bool Cuenta(IntegrationParticipant participante, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(participante);
        if (!participante.IsEnabled || !participante.VigenteEn(fecha)) return false;
        if (EsDoble(participante) && ambiente.EsProduccion) return false;
        return ImplementacionDisponible(participante, fecha);
    }

    public bool ImplementacionDisponible(IntegrationParticipant participante, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(participante);
        if (EsDoble(participante) && ambiente.EsProduccion) return false;
        var adoptadas = participante.Adoptions.Where(a => !a.IsDeleted && a.VigenteEn(fecha)).ToList();

        if (participante.Role == IntegrationParticipantRole.MessageDestination)
        {
            var consumidor = _destinos.FirstOrDefault(d => string.Equals(d.Destino, participante.Destination, StringComparison.Ordinal));
            return consumidor is not null && adoptadas.All(a => consumidor.Acepta(a.ContractName, a.Version));
        }

        return adoptadas.All(a => ContratosDeIntegracion.VersionesSoportadas(a.ContractName).Contains(a.Version));
    }

    /// <summary>Un doble de prueba: <c>IsTestDouble</c> o de clase <c>Test</c> (T20).</summary>
    public static bool EsDoble(IntegrationParticipant participante) =>
        participante.IsTestDouble || participante.Class == IntegrationParticipantClass.Test;

    private async Task<List<IntegrationParticipant>> VigentesAsync(
        DateOnly fecha, CancellationToken ct, IntegrationParticipantRole? rol = null, string? destino = null)
    {
        var consulta = db.IntegrationParticipants.AsNoTracking().Include(p => p.Adoptions)
            .Where(p => !p.IsDeleted && p.ValidFrom <= fecha && (p.ValidTo == null || p.ValidTo >= fecha));
        if (rol is { } r) consulta = consulta.Where(p => p.Role == r);
        if (destino is not null) consulta = consulta.Where(p => p.Destination == destino);
        return await consulta.ToListAsync(ct);
    }

    private static ParticipanteVigente Vigente(IntegrationParticipant p, DateOnly fecha) => new(
        p.PublicId, p.Code, p.Class, p.Role, p.Destination, p.IsTestDouble, p.ValidFrom, p.ValidTo,
        p.Adoptions.Where(a => !a.IsDeleted && a.VigenteEn(fecha)).OrderBy(a => a.ContractName, StringComparer.Ordinal).ThenBy(a => a.Version)
            .Select(a => new VersionAdoptada(a.ContractName, a.Version)).ToList());
}
