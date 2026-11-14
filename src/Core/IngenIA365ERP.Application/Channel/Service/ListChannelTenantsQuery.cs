using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Channel.Service;

/// <summary>La marca de una cooperativa en el canal (FR-007, FR-063).</summary>
public sealed record ChannelBrandDto(string? LogoUrl, string? Primary, string? Secondary);

/// <summary>El contacto de soporte de la cooperativa (FR-007, FR-019).</summary>
public sealed record ChannelSupportDto(string? Phone, string? Email, string? Hours);

/// <summary>
/// Una cooperativa con el canal encendido (contracts/erp-api-de-servicio.md, «Plataforma»). <see cref="PaymentSessionMinutes"/> es el
/// vencimiento de la sesión en la pasarela (FR-008) y <see cref="RequireOtpToPay"/> si se pide código al confirmar un pago (FR-009).
/// </summary>
public sealed record ChannelTenantDto(
    Guid TenantPublicId,
    string? ChannelCode,
    string DisplayName,
    ChannelBrandDto Brand,
    ChannelSupportDto Support,
    int InactivityMinutes,
    bool SmsEnabled,
    int PaymentSessionMinutes,
    bool RequireOtpToPay,
    int DueReminderDays = 0);

/// <summary>
/// Feature 021 (T020): las cooperativas con el canal encendido, para el directorio del canal. Recorre el directorio abriendo la base
/// de cada una (Principio IV: nunca un barrido desde una conexión). Una cooperativa cuya base todavía no tiene las tablas del canal
/// (migración pendiente) se omite con advertencia en el log, sin tumbar el listado de las demás.
/// </summary>
public sealed record ListChannelTenantsQuery : IRequest<Result<IReadOnlyList<ChannelTenantDto>>>;

public sealed class ListChannelTenantsQueryHandler(
    ITenantDirectory directorio,
    ITenantDbContextFactory fabrica,
    IDateTimeService reloj,
    ILogger<ListChannelTenantsQueryHandler> logger)
    : IRequestHandler<ListChannelTenantsQuery, Result<IReadOnlyList<ChannelTenantDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelTenantDto>>> Handle(ListChannelTenantsQuery request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var resultado = new List<ChannelTenantDto>();
        foreach (var cooperativa in await directorio.ListActiveAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(cooperativa.DatabaseName)) continue;
            try
            {
                await using var ambito = fabrica.Abrir(cooperativa.DatabaseName, cooperativa.ConnectionString);
                var config = await ConfiguracionDelCanal.VigenteAsync(ambito.Db, ahora, ct);
                if (config is not { IsEnabled: true }) continue;
                resultado.Add(new ChannelTenantDto(
                    cooperativa.PublicId,
                    config.ChannelCode,
                    string.IsNullOrWhiteSpace(config.DisplayName) ? cooperativa.Name : config.DisplayName,
                    new ChannelBrandDto(null, config.PrimaryColor, config.SecondaryColor),
                    new ChannelSupportDto(config.SupportPhone, config.SupportEmail, config.SupportHours),
                    config.InactivityMinutes,
                    config.SmsEnabled,
                    config.PaymentSessionMinutes,
                    config.RequireOtpToPay,
                    config.DueReminderDays));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[Canal.CooperativaOmitida] No se pudo leer la configuración del canal de la cooperativa {Tenant}; se omite del directorio.",
                    cooperativa.PublicId);
            }
        }
        return Result.Success<IReadOnlyList<ChannelTenantDto>>(resultado);
    }
}
