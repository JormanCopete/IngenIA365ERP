using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Settings;

/// <summary>
/// La configuración de emisión (feature 012, I4, T708; <c>GET /api/electronic-invoicing/settings</c>, api.md §24.1): la vigente hoy, el
/// historial de vigencias, los canales de la instalación con sus capacidades y el estado de la credencial del canal vigente —la clave
/// derivada, si el secreto la tiene y cuándo se verificó—. Nunca devuelve un valor de la credencial. (nuevo)
/// </summary>
public sealed record GetEmissionSettingsQuery : IRequest<Result<EmissionSettingsDto>>;

public sealed class GetEmissionSettingsQueryHandler(
    IApplicationDbContext db,
    ICanalesDeEmision canales,
    ICredencialesDeCanal credenciales,
    IDateTimeService reloj)
    : IRequestHandler<GetEmissionSettingsQuery, Result<EmissionSettingsDto>>
{
    public async Task<Result<EmissionSettingsDto>> Handle(GetEmissionSettingsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var historial = await db.ElectronicEmissionSettings.AsNoTracking().OrderByDescending(s => s.ValidFrom).ToListAsync(ct);
        var vigente = historial.FirstOrDefault(s => s.VigenteEn(hoy));

        var disponibles = canales.Codigos.OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => ConfiguracionDeEmision.Canal(canales.Resolver(c)))
            .ToList();

        var credencial = new CredencialDelCanalDto(null, false, null);
        if (vigente is not null)
        {
            var configurada = canales.Codigos.Contains(vigente.ChannelCode, StringComparer.OrdinalIgnoreCase)
                && (await credenciales.ResolverAsync(vigente.ChannelCode, ct)).IsSuccess;
            credencial = new CredencialDelCanalDto(vigente.CredentialKey, configurada, vigente.CredentialVerifiedAt);
        }

        return Result.Success(new EmissionSettingsDto(
            vigente is null ? null : ConfiguracionDeEmision.ADto(vigente),
            historial.Select(ConfiguracionDeEmision.ADto).ToList(),
            disponibles,
            credencial));
    }
}
